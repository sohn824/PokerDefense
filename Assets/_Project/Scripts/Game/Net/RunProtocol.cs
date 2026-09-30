using System;
using System.Collections.Generic;
using System.Text;
using PokerDefense.Poker;

namespace PokerDefense.Game
{
    // 클라이언트 처리: Accepted·Duplicate → 기록 삭제, Rejected → 다시 보내지 않음
    public enum RunAckStatus : byte
    {
        Accepted = 0,   // 새로 저장
        Duplicate = 1,  // 이미 저장된 runId (성공으로 취급)
        Rejected = 2,   // 규칙 위반
    }

    // RunAck - 제출에 대한 서버 응답 (저장까지 끝났다는 확인, TCP의 도착 확인과 다름)
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
     * C++ 서버의 Protocol.h와 같은 형식 (양쪽 테스트가 같은 바이트열로 확인)
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
            WriteU16(bytes, result.Wave);
            WriteU16(bytes, result.TotalWaves);
            bytes.Add(result.Cleared ? (byte)1 : (byte)0);
            WriteU16(bytes, result.Life);
            WriteU16(bytes, result.Summons);
            bytes.Add(result.BestHand.HasValue ? (byte)result.BestHand.Value : NoHand);
            WriteU32(bytes, (uint)Math.Max(0, result.ElapsedMs));
            return bytes.ToArray();
        }

        // TryDecodeSubmit - 저장된 바이트 -> RunResult (길이 4바이트 제외, outbox 파일을 읽을 때 사용)
        // 형식이 어긋나면 false
        public static bool TryDecodeSubmit(byte[] payload, out RunResult result)
        {
            result = null;
            Reader reader = new Reader(payload);
            if (reader.U8() != Version || reader.U8() != SubmitRunType)
            {
                return false;
            }

            Guid runId = new Guid(reader.Bytes(16));
            int rulesetLength = reader.U8();
            string ruleset = Encoding.ASCII.GetString(reader.Bytes(rulesetLength));
            int wave = reader.U16();
            int totalWaves = reader.U16();
            int cleared = reader.U8();
            int life = reader.U16();
            int summons = reader.U16();
            byte bestHand = reader.U8();
            uint elapsed = reader.U32();

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
            WriteU32(bytes, (uint)ack.Rank);
            WriteU32(bytes, (uint)ack.Total);
            return bytes.ToArray();
        }

        // TryDecodeAck - 받은 바이트 -> RunAck (길이 4바이트 제외)
        // 형식이 어긋나거나 모르는 상태 값이면 false
        public static bool TryDecodeAck(byte[] payload, out RunAck ack)
        {
            ack = default;
            Reader reader = new Reader(payload);
            if (reader.U8() != Version || reader.U8() != SubmitAckType)
            {
                return false;
            }

            Guid runId = new Guid(reader.Bytes(16));
            byte status = reader.U8();
            byte reason = reader.U8();
            uint rank = reader.U32();
            uint total = reader.U32();

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

        // 범위를 넘는 값은 잘라서 씀 (거부 판단은 서버 Validate)
        static void WriteU16(List<byte> bytes, int value)
        {
            int clamped = Math.Max(0, Math.Min(ushort.MaxValue, value));
            bytes.Add((byte)(clamped >> 8));
            bytes.Add((byte)clamped);
        }

        static void WriteU32(List<byte> bytes, uint value)
        {
            bytes.Add((byte)(value >> 24));
            bytes.Add((byte)(value >> 16));
            bytes.Add((byte)(value >> 8));
            bytes.Add((byte)value);
        }

        // Reader - 본문을 앞에서부터 차례로 읽음
        // 바이트가 모자라면 Ok = false, 이후 읽기는 0을 돌려줌 (그래서 확인은 마지막에 한 번만)
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

            public byte U8()
            {
                return Require(1) ? data[position++] : (byte)0;
            }

            public int U16()
            {
                if (Require(2) == false)
                {
                    return 0;
                }

                int value = (data[position] << 8) | data[position + 1];
                position += 2;
                return value;
            }

            public uint U32()
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

            public byte[] Bytes(int count)
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
     * 받은 바이트를 [길이][본문] 프레임 단위로 잘라냄
     * TCP는 보낸 단위를 지키지 않음 (반쪽만 오거나 두 개가 붙어 옴)
     * 사용법: 받은 만큼 Append → NeedMore가 나올 때까지 Next 반복
     */
    public sealed class RunFrameReader
    {
        public enum Result
        {
            NeedMore,
            Frame,
            Error,
        }

        readonly List<byte> buffer = new List<byte>();  // 아직 꺼내지 않은 바이트
        bool failed;  // 잘못된 길이를 받으면 이후 계속 Error

        // Append - recv로 받은 바이트를 뒤에 붙임
        public void Append(byte[] data, int count)
        {
            if (failed == false)
            {
                buffer.AddRange(new ArraySegment<byte>(data, 0, count));
            }
        }

        // Next - 프레임 하나가 다 모였으면 꺼내고, 아니면 NeedMore
        public Result Next(out byte[] payload)
        {
            payload = null;
            if (failed)
            {
                return Result.Error;
            }

            // 길이 4바이트가 다 와야 판단 가능
            if (buffer.Count < 4)
            {
                return Result.NeedMore;
            }

            uint length = ((uint)buffer[0] << 24) | ((uint)buffer[1] << 16) | ((uint)buffer[2] << 8) | buffer[3];

            // 본문을 기다리기 전에 길이부터 검사 (거대한 길이로 메모리를 잡는 입력 차단)
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
