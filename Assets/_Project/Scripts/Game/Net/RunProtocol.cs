using System;
using System.Collections.Generic;
using System.Text;
using PokerDefense.Poker;

namespace PokerDefense.Game
{
    // 서버가 제출을 처리한 결과 (SubmitAck의 status)
    // 클라이언트는 이 값을 보고 결과 파일을 대기열에서 뺌 (파일 형식: <runId>.run)
    //   Accepted·Duplicate -> 서버에 저장됐으므로 대기 파일 삭제
    //   Rejected           -> 다시 보내도 같으므로 확장자를 .rejected로 바꿔 대기열에서 뺌 (파일은 남김)
    public enum RunAckStatus : byte
    {
        Accepted = 0,   // 새로 저장
        Duplicate = 1,  // 이미 저장된 runId (성공으로 취급)
        Rejected = 2,   // 규칙 위반
    }

    // RunAck - 서버가 제출을 끝까지 처리한 뒤 보내는 답장
    //   Accepted·Duplicate는 파일 저장까지 끝난 뒤, Rejected는 검사에서 걸린 뒤 보냄
    //   TCP 도착 확인은 상대 OS가 바이트를 받았다는 뜻일 뿐 저장까지 보장하지 않으므로
    //   클라이언트는 이 Ack를 받아야만 결과 파일을 대기열에서 뺌 (삭제하거나 .rejected로 변경)
    //   (저장에 실패했거나 해석할 수 없는 메시지면 Ack 없이 연결을 끊음
    //    -> 클라이언트는 결과 파일을 남겨 두고 다음 판 시작·종료 때 다시 보냄)
    public readonly struct RunAck
    {
        public RunAck(Guid runId, RunAckStatus status, byte reason, int rank, int total)
        {
            RunId = runId;
            Status = status;
            Reason = reason;
            Rank = rank;
            Total = total;
        }

        public Guid RunId { get; }
        public RunAckStatus Status { get; }
        public byte Reason { get; }
        public int Rank { get; }
        public int Total { get; }
    }

    /**
     * RunProtocol
     *
     * 결과 제출 프로토콜 v1 인코딩·디코딩 (연결은 하지 않고 바이트 변환만)
     * C++ 서버의 Protocol.h와 같은 형식
     * (프로토콜 형식을 바꾸면 서버와 클라이언트 양쪽의 테스트용 SampleFrameHex도 같이 고쳐야 함)
     *
     * 메시지 하나 = [길이 4바이트][본문]
     * TCP는 메시지 경계가 없어서, 받는 쪽이 본문 끝을 알 수 있게 길이를 먼저 보냄
     * 1바이트는 0~255(256가지)라 큰 수는 여러 바이트로 나눠 씀
     * 숫자는 큰 자리 바이트부터 씀 (빅엔디언, 예: 300 = 1×256 + 44 → 01 2C)
     *
     * 메시지 본문은 Encode 함수에 적힌 순서대로 값을 이어 붙임
     * Encode = 객체 → 바이트, Decode = 바이트 → 객체
     */
    public static class RunProtocol
    {
        // 프로토콜 형식이 바뀌면 이 값을 올림
        public const byte Version = 1;

        // 본문 최대 크기, 넘으면 오류
        public const int MaxPayloadSize = 256;

        // 족보를 한 번도 확정하지 않은 판의 bestHand
        const byte NoHand = 0xFF;

        // 메시지 종류 - 본문 [버전][종류][…] 중 두 번째 바이트, 받는 쪽이 이 값으로 제출/답장을 구분
        const byte SubmitRunType = 1;
        const byte SubmitAckType = 2;

        // EncodeSubmit - RunResult -> 보낼 바이트 (길이 4바이트 제외)
        // 전송할 때와 outbox 파일에 저장할 때 같이 사용
        public static byte[] EncodeSubmit(RunResult result)
        {
            byte[] ruleset = Encoding.ASCII.GetBytes(result.Ruleset);
            if (ruleset.Length > byte.MaxValue)
            {
                throw new ArgumentException("ruleset 이름이 너무 김", nameof(result));
            }

            List<byte> bytes = new List<byte>(64);
            bytes.Add(Version);
            bytes.Add(SubmitRunType);
            bytes.AddRange(result.RunId.ToByteArray());
            bytes.Add((byte)ruleset.Length);
            bytes.AddRange(ruleset);
            WriteUInt16(bytes, result.Wave);
            WriteUInt16(bytes, result.TotalWaves);
            bytes.Add(result.Cleared ? (byte)1 : (byte)0);
            WriteUInt16(bytes, result.Life);
            WriteUInt16(bytes, result.Summons);
            bytes.Add(result.BestHand.HasValue ? (byte)result.BestHand.Value : NoHand);
            WriteUInt32(bytes, (uint)Math.Max(0, result.ElapsedMs));
            return bytes.ToArray();
        }

        // TryDecodeSubmit - 저장된 바이트 -> RunResult (길이 4바이트 제외, outbox 파일을 읽을 때 사용)
        // 형식이 어긋나면 false 반환
        public static bool TryDecodeSubmit(byte[] payload, out RunResult result)
        {
            result = null;
            Reader reader = new Reader(payload);
            if (reader.ReadByte() != Version || reader.ReadByte() != SubmitRunType)
            {
                return false;
            }

            Guid runId = new Guid(reader.ReadBytes(16));
            int rulesetLength = reader.ReadByte();
            string ruleset = Encoding.ASCII.GetString(reader.ReadBytes(rulesetLength));
            int wave = reader.ReadUInt16();
            int totalWaves = reader.ReadUInt16();
            int cleared = reader.ReadByte();
            int life = reader.ReadUInt16();
            int summons = reader.ReadUInt16();
            byte bestHand = reader.ReadByte();
            uint elapsed = reader.ReadUInt32();

            // 바이트가 모자라거나 남으면 형식 오류
            if (reader.Ok == false || reader.Remaining != 0 || cleared > 1 || elapsed > int.MaxValue)
            {
                return false;
            }

            HandCategory? hand = bestHand == NoHand ? (HandCategory?)null : (HandCategory)bestHand;
            result = new RunResult(runId, ruleset, wave, totalWaves, cleared == 1, life, summons, hand, (int)elapsed);
            return true;
        }

        // EncodeAck - RunAck -> 보낼 바이트 (길이 4바이트 제외, 테스트의 가짜 서버 전용)
        public static byte[] EncodeAck(RunAck ack)
        {
            List<byte> bytes = new List<byte>(32);
            bytes.Add(Version);
            bytes.Add(SubmitAckType);
            bytes.AddRange(ack.RunId.ToByteArray());
            bytes.Add((byte)ack.Status);
            bytes.Add(ack.Reason);
            WriteUInt32(bytes, (uint)ack.Rank);
            WriteUInt32(bytes, (uint)ack.Total);
            return bytes.ToArray();
        }

        // TryDecodeAck - 받은 바이트 -> RunAck (길이 4바이트 제외)
        // 형식이 어긋나거나 모르는 상태 값이면 false
        public static bool TryDecodeAck(byte[] payload, out RunAck ack)
        {
            ack = default;
            Reader reader = new Reader(payload);
            if (reader.ReadByte() != Version || reader.ReadByte() != SubmitAckType)
            {
                return false;
            }

            Guid runId = new Guid(reader.ReadBytes(16));
            byte status = reader.ReadByte();
            byte reason = reader.ReadByte();
            uint rank = reader.ReadUInt32();
            uint total = reader.ReadUInt32();

            if (reader.Ok == false || reader.Remaining != 0 || status > (byte)RunAckStatus.Rejected
                || rank > int.MaxValue || total > int.MaxValue)
            {
                return false;
            }

            ack = new RunAck(runId, (RunAckStatus)status, reason, (int)rank, (int)total);
            return true;
        }

        // Frame - 본문 앞에 길이 4바이트를 붙여 보낼 메시지로 만듦
        public static byte[] Frame(byte[] payload)
        {
            byte[] frame = new byte[payload.Length + 4];
            frame[0] = (byte)(payload.Length >> 24);
            frame[1] = (byte)(payload.Length >> 16);
            frame[2] = (byte)(payload.Length >> 8);
            frame[3] = (byte)payload.Length;
            Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);
            return frame;
        }

        // 쓰기 - 정수를 큰 자리 바이트부터 bytes 끝에 덧붙임 (빅엔디언, CPU와 상관없이 같은 바이트)

        // 2바이트. RunResult 값은 int라 0~65535 밖일 수 있음 - 예외를 던지면 결과 파일 저장까지 실패하므로
        // 경계값으로 잘라 씀 (잘린 값은 규칙 범위를 벗어나 서버 Validate가 거부함)
        static void WriteUInt16(List<byte> bytes, int value)
        {
            int clamped = Math.Max(0, Math.Min(ushort.MaxValue, value));
            bytes.Add((byte)(clamped >> 8));
            bytes.Add((byte)clamped);
        }

        // 4바이트
        static void WriteUInt32(List<byte> bytes, uint value)
        {
            bytes.Add((byte)(value >> 24));
            bytes.Add((byte)(value >> 16));
            bytes.Add((byte)(value >> 8));
            bytes.Add((byte)value);
        }

        // Reader - 본문을 앞에서부터 차례로 읽음
        // 바이트가 모자라면 실패 상태(Ok = false)가 되고, 그 읽기부터는 모두 0을 돌려줌
        // 그래서 읽는 쪽은 매번 확인하지 않고 마지막에 Ok를 한 번만 확인
        sealed class Reader
        {
            readonly byte[] data;
            int position;

            public Reader(byte[] data)
            {
                this.data = data ?? Array.Empty<byte>();
            }

            public bool Ok { get; private set; } = true;
            public int Remaining => data.Length - position;

            // 1바이트
            public byte ReadByte()
            {
                return Require(1) ? data[position++] : (byte)0;
            }

            // 부호 없는 16비트 정수 = 2바이트, RunResult 필드에 맞춰 int로 돌려줌
            public int ReadUInt16()
            {
                if (Require(2) == false)
                {
                    return 0;
                }

                int value = (data[position] << 8) | data[position + 1];
                position += 2;
                return value;
            }

            // 부호 없는 32비트 정수 = 4바이트
            public uint ReadUInt32()
            {
                if (Require(4) == false)
                {
                    return 0;
                }

                uint value = ((uint)data[position] << 24) | ((uint)data[position + 1] << 16)
                    | ((uint)data[position + 2] << 8) | data[position + 3];
                position += 4;
                return value;
            }

            // count바이트를 그대로 꺼냄 (runId·문자열용)
            public byte[] ReadBytes(int count)
            {
                byte[] bytes = new byte[count];
                if (Require(count))
                {
                    Buffer.BlockCopy(data, position, bytes, 0, count);
                    position += count;
                }

                return bytes;
            }

            bool Require(int count)
            {
                if (Ok == false || Remaining < count)
                {
                    Ok = false;
                    return false;
                }

                return true;
            }
        }
    }

    /**
     * RunFrameReader
     *
     * TCP로 받은 이어진 바이트에서 메시지 경계를 찾아, 프레임([길이 4바이트][본문]) 하나씩 떼어 냄
     * 꺼낸 payload(메시지 본문)는 길이 4바이트를 뺀 부분
     *
     * TCP는 보낸 단위를 지키지 않음 - 메시지 하나가 받기(ReceiveAsync) 여러 번에 나뉘어 오거나,
     * 메시지 두 개가 받기 한 번에 붙어 올 수 있음
     * 그래서 받은 바이트를 쌓아 두고, 앞의 길이만큼 본문이 다 모였을 때만 하나씩 꺼냄
     *
     * 사용법: 받을 때마다 Append -> Next를 반복 호출, Next의 반환값(Result)에 따라 다음과 같이 처리
     *   Frame    = 프레임 하나가 다 모여 메시지 하나를 꺼냄, 본문은 payload에 담김
     *              (뒤에 더 붙어 있을 수 있으니 다시 Next 호출)
     *   NeedMore = 아직 덜 옴, 더 받아서 Append
     *   Error    = 길이가 0이거나 최대 크기(MaxPayloadSize, 256바이트)를 넘음, 연결을 끊어야 함 (한 번 나면 이후 계속 Error)
     *
     * 사용 예: RunSubmitter.ReceiveFrameAsync
     *            제출 하나에 서버 답장(Ack)은 하나뿐이라, 첫 Frame을 꺼내면 받기를 멈추고 그 본문을 돌려줌
     *          서버 FrameReader의 TcpServer::Receive
     *            한 연결에 제출 메시지가 붙어 올 수 있어, NeedMore가 나올 때까지 메시지를 계속 꺼내 하나씩 처리
     */
    public sealed class RunFrameReader
    {
        public enum Result
        {
            NeedMore,  // 프레임이 아직 다 오지 않음
            Frame,     // 프레임 하나가 다 모여 payload로 꺼냄
            Error,     // 길이가 잘못됨 (0이거나 MaxPayloadSize 초과), 이후 계속 Error
        }

        readonly List<byte> buffer = new List<byte>();  // 아직 꺼내지 않은 바이트
        bool failed;  // 잘못된 길이를 받으면 이후 계속 Error

        // Append - 소켓에서 받은 바이트(ReceiveAsync)를 뒤에 붙임
        public void Append(byte[] data, int count)
        {
            if (failed == false)
            {
                buffer.AddRange(new ArraySegment<byte>(data, 0, count));
            }
        }

        // Next - 프레임 하나가 다 모였으면 꺼내고(Frame), 덜 왔으면 NeedMore, 길이가 잘못됐으면 Error
        public Result Next(out byte[] payload)
        {
            payload = null;
            if (failed)
            {
                return Result.Error;
            }

            // 길이 4바이트가 다 오기 전에는 판단할 수 없음
            if (buffer.Count < 4)
            {
                return Result.NeedMore;
            }

            uint length = ((uint)buffer[0] << 24) | ((uint)buffer[1] << 16) | ((uint)buffer[2] << 8) | buffer[3];

            // 본문이 다 오기를 기다리기 전에 길이부터 검사
            // 거대한 길이를 그대로 믿으면 그만큼 받을 때까지 버퍼에 쌓으며 메모리를 잡아 두게 됨
            if (length == 0 || length > RunProtocol.MaxPayloadSize)
            {
                failed = true;
                return Result.Error;
            }

            if (buffer.Count < 4 + length)
            {
                return Result.NeedMore;
            }

            payload = buffer.GetRange(4, (int)length).ToArray();
            buffer.RemoveRange(0, 4 + (int)length);
            return Result.Frame;
        }
    }
}
