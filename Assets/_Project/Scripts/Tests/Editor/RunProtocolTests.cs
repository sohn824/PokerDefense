using System;
using System.Linq;
using NUnit.Framework;
using PokerDefense.Game;
using PokerDefense.Poker;

namespace PokerDefense.Tests
{
    public class RunProtocolTests
    {
        // 서버와 클라이언트가 같은 바이트를 만드는지 확인하는 기준
        // C++ 서버 테스트(Server/tests/Tests.cpp)에도 같은 기록과 같은 정답 바이트열이 들어 있고,
        // 양쪽이 각자의 인코딩 결과를 이 바이트열과 비교함 - 형식이 어긋나면 어긋난 쪽 테스트가 실패
        // [길이 45][버전 1][종류 1][runId 00~0f][ruleset 길이 12]["hand-loop-v1"]
        // [wave 37][total 50][cleared 0][life 0][summons 37][bestHand 7][elapsedMs 812345]
        const string SampleFrameHex =
            "0000002d" + "0101" + "000102030405060708090a0b0c0d0e0f" + "0c" + "68616e642d6c6f6f702d7631"
            + "0025" + "0032" + "00" + "0000" + "0025" + "07" + "000c6539";

        internal static RunResult Sample(Guid? runId = null)
        {
            Guid id = runId ?? new Guid(Enumerable.Range(0, 16).Select(i => (byte)i).ToArray());
            return new RunResult(id, RunResult.CurrentRuleset, 37, 50, false, 0, 37, HandCategory.FourOfAKind, 812345);
        }

        static byte[] FromHex(string hex)
        {
            return Enumerable.Range(0, hex.Length / 2).Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16)).ToArray();
        }

        [Test]
        public void 제출_메시지는_서버와_같은_고정_바이트열이다()
        {
            CollectionAssert.AreEqual(FromHex(SampleFrameHex), RunProtocol.Frame(RunProtocol.EncodeSubmit(Sample())));
        }

        [Test]
        public void 제출_본문을_다시_읽으면_같은_값이다()
        {
            var sample = Sample();
            Assert.IsTrue(RunProtocol.TryDecodeSubmit(RunProtocol.EncodeSubmit(sample), out var decoded));
            Assert.AreEqual(sample.RunId, decoded.RunId);
            Assert.AreEqual(sample.Ruleset, decoded.Ruleset);
            Assert.AreEqual(37, decoded.Wave);
            Assert.AreEqual(50, decoded.TotalWaves);
            Assert.IsFalse(decoded.Cleared);
            Assert.AreEqual(37, decoded.Summons);
            Assert.AreEqual(HandCategory.FourOfAKind, decoded.BestHand);
            Assert.AreEqual(812345, decoded.ElapsedMs);
        }

        [Test]
        public void 족보가_없는_기록도_그대로_돌아온다()
        {
            var sample = new RunResult(Guid.NewGuid(), RunResult.CurrentRuleset, 1, 50, false, 0, 0, null, 0);
            Assert.IsTrue(RunProtocol.TryDecodeSubmit(RunProtocol.EncodeSubmit(sample), out var decoded));
            Assert.IsNull(decoded.BestHand);
        }

        [Test]
        public void 모자라거나_남는_바이트가_있으면_읽지_않는다()
        {
            var payload = RunProtocol.EncodeSubmit(Sample());
            Assert.IsFalse(RunProtocol.TryDecodeSubmit(payload.Take(payload.Length - 1).ToArray(), out _));
            Assert.IsFalse(RunProtocol.TryDecodeSubmit(payload.Concat(new byte[] { 0 }).ToArray(), out _));
            Assert.IsFalse(RunProtocol.TryDecodeSubmit(Array.Empty<byte>(), out _));
        }

        [Test]
        public void ACK를_다시_읽으면_같은_값이다()
        {
            var ack = new RunAck(Sample().RunId, RunAckStatus.Duplicate, 0, 3, 12);
            Assert.IsTrue(RunProtocol.TryDecodeAck(RunProtocol.EncodeAck(ack), out var decoded));
            Assert.AreEqual(ack.RunId, decoded.RunId);
            Assert.AreEqual(RunAckStatus.Duplicate, decoded.Status);
            Assert.AreEqual(3, decoded.Rank);
            Assert.AreEqual(12, decoded.Total);

            // 제출 메시지를 ACK로 읽으면 안 됨
            Assert.IsFalse(RunProtocol.TryDecodeAck(RunProtocol.EncodeSubmit(Sample()), out _));
        }

        [Test]
        public void 한_바이트씩_와도_프레임을_조립한다()
        {
            var frame = FromHex(SampleFrameHex);
            var reader = new RunFrameReader();
            for (int i = 0; i < frame.Length - 1; i++)
            {
                reader.Append(new[] { frame[i] }, 1);
                Assert.AreEqual(RunFrameReader.Result.NeedMore, reader.Next(out _));
            }

            reader.Append(new[] { frame[frame.Length - 1] }, 1);
            Assert.AreEqual(RunFrameReader.Result.Frame, reader.Next(out var payload));
            CollectionAssert.AreEqual(RunProtocol.EncodeSubmit(Sample()), payload);
        }

        [Test]
        public void 붙어서_온_두_프레임을_나눠_꺼낸다()
        {
            var frame = FromHex(SampleFrameHex);
            var twice = frame.Concat(frame).ToArray();
            var reader = new RunFrameReader();
            reader.Append(twice, twice.Length);
            Assert.AreEqual(RunFrameReader.Result.Frame, reader.Next(out _));
            Assert.AreEqual(RunFrameReader.Result.Frame, reader.Next(out _));
            Assert.AreEqual(RunFrameReader.Result.NeedMore, reader.Next(out _));
        }

        [Test]
        public void 길이가_0이거나_너무_크면_오류다()
        {
            var zero = new RunFrameReader();
            zero.Append(new byte[] { 0, 0, 0, 0 }, 4);
            Assert.AreEqual(RunFrameReader.Result.Error, zero.Next(out _));

            // 본문이 오기 전에 길이만 보고 거부해야 함
            var huge = new RunFrameReader();
            huge.Append(new byte[] { 0x7F, 0xFF, 0xFF, 0xFF }, 4);
            Assert.AreEqual(RunFrameReader.Result.Error, huge.Next(out _));
            Assert.AreEqual(RunFrameReader.Result.Error, huge.Next(out _));
        }
    }
}
