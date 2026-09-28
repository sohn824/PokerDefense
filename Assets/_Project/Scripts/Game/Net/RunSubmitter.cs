using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PokerDefense.Game
{
    public enum SubmitStatus
    {
        Accepted,
        Duplicate,
        Rejected,
        Failed,  // 연결·전송·응답 실패 - 나중에 다시 보냄
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
        public int Rank { get; }       // 순위 (저장됐을 때만)
        public int Total { get; }      // 전체 기록 수
        public string Error { get; }   // 실패 이유 (디버깅용)

        // 서버에 저장됨 - 대기열에서 지워도 됨
        public bool IsStored => Status == SubmitStatus.Accepted || Status == SubmitStatus.Duplicate;

        public static SubmitOutcome Failed(string error) => new SubmitOutcome(SubmitStatus.Failed, 0, 0, error);
    }

    /**
     * RunSubmitter
     *
     * 결과 하나를 보내고 ACK를 기다림 (Unity API 없음 - 워커 스레드용)
     * 전체 시간 제한이 지나면 소켓을 닫아 대기를 끝냄
     * ConfigureAwait(false) - 메인 스레드로 돌아오지 않아 메인이 멈춰 있어도 끝남
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

        // SubmitAsync - 연결 → 전송 → ACK 수신 → runId 확인
        // 전송 완료가 아니라 ACK를 받아야 성공 - 못 받으면 실패로 보고 나중에 재전송
        public async Task<SubmitOutcome> SubmitAsync(RunResult result, CancellationToken cancellation)
        {
            using (CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                deadline.CancelAfter(timeout);
                // 소켓 작업은 토큰을 받지 않음 - 시간이 되면 소켓을 닫아 예외로 끝냄
                using (deadline.Token.Register(socket.Close))
                {
                    try
                    {
                        IPAddress address = await ResolveAsync().ConfigureAwait(false);
                        await socket.ConnectAsync(address, port).ConfigureAwait(false);

                        // 일부만 나갈 수 있어 다 보낼 때까지 반복
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
                            return SubmitOutcome.Failed("응답 전에 연결이 끊김");
                        }

                        // 다른 기록의 ACK면 저장됐다고 볼 수 없음
                        if (RunProtocol.TryDecodeAck(payload, out RunAck ack) == false || ack.RunId != result.RunId)
                        {
                            return SubmitOutcome.Failed("응답 형식이 맞지 않음");
                        }

                        SubmitStatus status = ack.Status == RunAckStatus.Accepted ? SubmitStatus.Accepted
                            : ack.Status == RunAckStatus.Duplicate ? SubmitStatus.Duplicate
                            : SubmitStatus.Rejected;
                        return new SubmitOutcome(status, ack.Rank, ack.Total, null);
                    }
                    // 연결 실패·끊김·시간 초과는 모두 나중에 다시 보냄
                    catch (Exception error) when (error is SocketException || error is ObjectDisposedException
                        || error is OperationCanceledException)
                    {
                        return SubmitOutcome.Failed(deadline.IsCancellationRequested ? "시간 초과" : error.Message);
                    }
                }
            }
        }

        // ResolveAsync - IP면 그대로, 이름이면 DNS 조회 (IPv4만)
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

        // ReceiveFrameAsync - 프레임 하나를 끝까지 받음 (조각나서 오므로 쌓으며 반복). 먼저 끊기면 null
        static async Task<byte[]> ReceiveFrameAsync(Socket socket)
        {
            RunFrameReader reader = new RunFrameReader();
            byte[] buffer = new byte[256];
            for (;;)
            {
                RunFrameReader.Result next = reader.Next(out byte[] payload);
                if (next == RunFrameReader.Result.Frame)
                {
                    return payload;
                }

                if (next == RunFrameReader.Result.Error)
                {
                    return null;
                }

                int received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None)
                    .ConfigureAwait(false);
                if (received == 0)
                {
                    return null;
                }

                reader.Append(buffer, received);
            }
        }
    }
}
