using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PokerDefense.Game
{
    // SubmitStatus - 전송 한 번의 결과
    // Accepted·Duplicate·Rejected는 서버 ACK의 status(RunAckStatus)를 그대로 옮긴 것
    // Failed는 ACK를 못 받은 경우라 클라이언트에만 있음
    public enum SubmitStatus
    {
        Accepted,   // 새로 저장됨
        Duplicate,  // 이미 저장된 기록 (저장된 것으로 봄)
        Rejected,   // 서버 값 검사에 걸림 - 다시 보내도 같음
        Failed,     // 연결·전송·응답 실패 - 나중에 다시 보냄
    }

    // 전송 한 번의 결과 (실패도 예외 대신 이 값으로)
    public readonly struct SubmitOutcome
    {
        public SubmitOutcome(SubmitStatus status, int rank, int total, string error)
        {
            Status = status;
            Rank = rank;
            Total = total;
            Error = error;
        }

        public SubmitStatus Status { get; }
        public int Rank { get; }       // 순위 (Accepted·Duplicate일 때만 유효, 그 밖에는 0)
        public int Total { get; }      // 전체 기록 수
        public string Error { get; }   // 실패 이유 (디버깅용, 성공이면 null)

        // 서버에 저장됨 (대기열에서 지워도 됨)
        public bool IsStored => Status == SubmitStatus.Accepted || Status == SubmitStatus.Duplicate;

        public static SubmitOutcome Failed(string error) => new SubmitOutcome(SubmitStatus.Failed, 0, 0, error);
    }

    /**
     * RunSubmitter - 결과 하나를 서버에 보내고 ACK를 받을 때까지 기다림
     *
     * 워커 스레드에서 돌도록 Unity API를 쓰지 않음
     * 시간 제한(timeout)이 지나면 소켓을 닫아 기다림을 끝냄
     * await마다 ConfigureAwait(false) - 메인 스레드로 돌아가지 않아, 메인이 멈춰 있어도 끝까지 진행됨
     */
    public sealed class RunSubmitter
    {
        readonly string host;
        readonly int port;
        readonly TimeSpan timeout;

        public RunSubmitter(string host, int port, TimeSpan timeout)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            this.port = port;
            this.timeout = timeout;
        }

        // SubmitAsync - 연결 -> 결과 전송 -> ACK 수신 -> runId 확인 순서로 진행
        // 다 보냈다고 성공이 아니라 ACK를 받아야 성공 (못 받으면 Failed - RunReporter가 나중에 다시 보냄)
        public async Task<SubmitOutcome> SubmitAsync(RunResult result, CancellationToken cancellation)
        {
            using (CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                deadline.CancelAfter(timeout);
                // 소켓 함수(ConnectAsync·SendAsync·ReceiveAsync)는 취소 토큰을 받지 않아 기다림을 직접 멈출 수 없음
                // 그래서 deadline이 취소되면(시간 초과 또는 밖에서 취소) 소켓을 닫아, 기다리던 작업이 예외로 끝나게 함
                using (deadline.Token.Register(socket.Close))
                {
                    try
                    {
                        IPAddress address = await ResolveAsync().ConfigureAwait(false);
                        await socket.ConnectAsync(address, port).ConfigureAwait(false);

                        // SendAsync는 한 번에 일부만 보낼 수 있어, 다 보낼 때까지 남은 부분을 이어서 보냄
                        byte[] frame = RunProtocol.Frame(RunProtocol.EncodeSubmit(result));
                        int sent = 0;
                        while (sent < frame.Length)
                        {
                            sent += await socket.SendAsync(new ArraySegment<byte>(frame, sent, frame.Length - sent),
                                SocketFlags.None).ConfigureAwait(false);
                        }

                        byte[] payload = await ReceiveFrameAsync(socket).ConfigureAwait(false);
                        if (payload == null)
                        {
                            return SubmitOutcome.Failed("응답을 받지 못함 (연결 끊김 또는 잘못된 길이)");
                        }

                        if (RunProtocol.TryDecodeAck(payload, out RunAck ack) == false)
                        {
                            return SubmitOutcome.Failed("응답을 해석할 수 없음");
                        }

                        if (ack.RunId != result.RunId)
                        {
                            return SubmitOutcome.Failed("다른 기록의 응답 (runId 불일치)");
                        }

                        // 서버 ACK의 status를 SubmitStatus로 옮김 (Failed는 여기서 나오지 않음)
                        SubmitStatus status = ack.Status == RunAckStatus.Accepted ? SubmitStatus.Accepted
                            : ack.Status == RunAckStatus.Duplicate ? SubmitStatus.Duplicate
                            : SubmitStatus.Rejected;
                        return new SubmitOutcome(status, ack.Rank, ack.Total, null);
                    }
                    // 연결 실패·끊김·시간 초과·취소는 모두 Failed로 돌려주고 나중에 다시 보냄
                    //   SocketException - 연결 실패, 연결 끊김, 주소 조회 실패
                    //   ObjectDisposedException - 시간 제한·취소로 소켓을 닫아 끝난 경우
                    //   OperationCanceledException - 작업이 취소로 끝난 경우
                    catch (Exception error) when (error is SocketException || error is ObjectDisposedException
                        || error is OperationCanceledException)
                    {
                        return SubmitOutcome.Failed(deadline.IsCancellationRequested ? "시간 초과" : error.Message);
                    }
                }
            }
        }

        // ResolveAsync - host 문자열을 접속할 IP 주소로 바꿈
        //   "127.0.0.1"처럼 IP면 그대로, "localhost"처럼 이름이면 DNS(Domain Name System)로 조회
        //   조회 결과 중 IPv4만 고름 (소켓을 IPv4로 만들었고, 서버도 IPv4로만 받음)
        //   없으면 SocketException
        async Task<IPAddress> ResolveAsync()
        {
            if (IPAddress.TryParse(host, out IPAddress parsed))
            {
                return parsed;
            }

            IPAddress[] addresses = await Dns.GetHostAddressesAsync(host).ConfigureAwait(false);
            foreach (IPAddress address in addresses)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork)
                {
                    return address;
                }
            }

            throw new SocketException((int)SocketError.HostNotFound);
        }

        // ReceiveFrameAsync - 서버 응답 메시지 하나를 다 받을 때까지 읽어서 본문(payload)을 돌려줌
        // (TCP라 메시지가 조각나서 올 수 있어, RunFrameReader에 쌓으며 다 모일 때까지 반복)
        // 다 받기 전에 연결이 끊기거나 응답 길이가 잘못됐으면 null
        static async Task<byte[]> ReceiveFrameAsync(Socket socket)
        {
            RunFrameReader reader = new RunFrameReader();
            // ReceiveAsync 한 번에 최대 256바이트까지 받음
            // (ACK 메시지는 길이 4바이트를 합쳐도 32바이트라 대부분 한 번에 다 들어옴)
            byte[] buffer = new byte[256];
            for (;;)
            {
                // 지금까지 쌓인 바이트로 메시지 하나가 다 모였는지 먼저 확인 (처음엔 비어 있어 NeedMore)
                RunFrameReader.Result next = reader.Next(out byte[] payload);
                if (next == RunFrameReader.Result.Frame)
                {
                    return payload;
                }

                // 길이가 잘못된 메시지라 더 받아도 소용없음
                if (next == RunFrameReader.Result.Error)
                {
                    return null;
                }

                // Frame도 Error도 아니면 NeedMore(아직 덜 모임) - 더 받아서 쌓음
                int received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None)
                    .ConfigureAwait(false);
                // ReceiveAsync가 0이면 서버가 연결을 닫은 것 (메시지가 덜 왔으므로 실패)
                if (received == 0)
                {
                    return null;
                }

                reader.Append(buffer, received);
            }
        }
    }
}
