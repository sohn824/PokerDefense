using System;
using System.Linq;
using NUnit.Framework;
using PokerDefense.Game;
using PokerDefense.Poker;

namespace PokerDefense.Tests
{
    /**
     * RunProtocolTests
     *
     * 결과 전송 메시지를 바이트로 만들고 다시 읽는 과정이 서버와 같은 형식인지 검사하는 클래스
     */
    public class RunProtocolTests
    {
        // 서버와 클라이언트가 같은 바이트를 만드는지 확인하는 기준
        // C++ 서버 테스트(Server/tests/Tests.cpp)에도 같은 기록과 같은 정답 바이트열이 들어 있고,
        // 양쪽이 각자의 인코딩 결과를 이 바이트열과 비교함 - 형식이 어긋나면 어긋난 쪽 테스트가 실패
        // [길이 45][버전 1][종류 1][runId 00~0f][ruleset 길이 12]["hand-loop-v2"]
        // [wave 37][total 50][cleared 0][life 0][summons 37][bestHand 7][elapsedMs 812345]
        const string SampleFrameHex =
            "0000002d" + "0101" + "000102030405060708090a0b0c0d0e0f" + "0c" + "68616e642d6c6f6f702d7632"
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
        public void 제출과_ACK를_다시_읽으면_같은_값이다()
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

            var noHand = new RunResult(Guid.NewGuid(), RunResult.CurrentRuleset, 1, 50, false, 0, 0, null, 0);
            Assert.IsTrue(RunProtocol.TryDecodeSubmit(RunProtocol.EncodeSubmit(noHand), out var decodedNoHand));
            Assert.IsNull(decodedNoHand.BestHand, "족보가 없는 기록도 그대로 돌아온다");

            var ack = new RunAck(sample.RunId, RunAckStatus.Duplicate, 0, 3, 12);
            Assert.IsTrue(RunProtocol.TryDecodeAck(RunProtocol.EncodeAck(ack), out var decodedAck));
            Assert.AreEqual(ack.RunId, decodedAck.RunId);
            Assert.AreEqual(RunAckStatus.Duplicate, decodedAck.Status);
            Assert.AreEqual(3, decodedAck.Rank);
            Assert.AreEqual(12, decodedAck.Total);

            // 제출 메시지를 ACK로 읽으면 안 됨
            Assert.IsFalse(RunProtocol.TryDecodeAck(RunProtocol.EncodeSubmit(sample), out _));
        }

        [Test]
        public void 길이가_맞지_않는_메시지는_읽지_않는다()
        {
            var payload = RunProtocol.EncodeSubmit(Sample());
            Assert.IsFalse(RunProtocol.TryDecodeSubmit(payload.Take(payload.Length - 1).ToArray(), out _), "모자란 바이트");
            Assert.IsFalse(RunProtocol.TryDecodeSubmit(payload.Concat(new byte[] { 0 }).ToArray(), out _), "남는 바이트");
            Assert.IsFalse(RunProtocol.TryDecodeSubmit(Array.Empty<byte>(), out _));

            var zero = new RunFrameReader();
            zero.Append(new byte[] { 0, 0, 0, 0 }, 4);
            Assert.AreEqual(RunFrameReader.Result.Error, zero.Next(out _), "길이 0");

            // 본문이 오기 전에 길이만 보고 거부해야 함
            var huge = new RunFrameReader();
            huge.Append(new byte[] { 0x7F, 0xFF, 0xFF, 0xFF }, 4);
            Assert.AreEqual(RunFrameReader.Result.Error, huge.Next(out _), "너무 큰 길이");
            Assert.AreEqual(RunFrameReader.Result.Error, huge.Next(out _));
        }

        [Test]
        public void 프레임은_조각나거나_붙어서_와도_하나씩_꺼낸다()
        {
            var frame = FromHex(SampleFrameHex);

            var bytewise = new RunFrameReader();
            for (int i = 0; i < frame.Length - 1; i++)
            {
                bytewise.Append(new[] { frame[i] }, 1);
                Assert.AreEqual(RunFrameReader.Result.NeedMore, bytewise.Next(out _));
            }

            bytewise.Append(new[] { frame[frame.Length - 1] }, 1);
            Assert.AreEqual(RunFrameReader.Result.Frame, bytewise.Next(out var payload));
            CollectionAssert.AreEqual(RunProtocol.EncodeSubmit(Sample()), payload);

            var twice = frame.Concat(frame).ToArray();
            var joined = new RunFrameReader();
            joined.Append(twice, twice.Length);
            Assert.AreEqual(RunFrameReader.Result.Frame, joined.Next(out _));
            Assert.AreEqual(RunFrameReader.Result.Frame, joined.Next(out _));
            Assert.AreEqual(RunFrameReader.Result.NeedMore, joined.Next(out _));
        }
    }
}
