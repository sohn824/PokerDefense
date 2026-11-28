using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PokerDefense.Game;

namespace PokerDefense.Tests
{
    /**
     * RunOutboxTests
     *
     * 결과 대기열 파일의 저장·삭제·거부 처리와, 깨진 파일·쓰다 만 파일을 다루는 방식을 검사하는 클래스
     */
    public class RunOutboxTests
    {
        string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "PokerDefenseOutboxTest_" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void 저장한_기록은_runId마다_하나씩_다시_읽힌다()
        {
            Assert.AreEqual(0, new RunOutbox(directory).LoadPending().Count, "폴더가 없으면 빈 목록이다");

            var outbox = new RunOutbox(directory);
            var sample = RunProtocolTests.Sample(Guid.NewGuid());
            outbox.Save(sample);
            outbox.Save(sample);

            var pending = new RunOutbox(directory).LoadPending();
            Assert.AreEqual(1, pending.Count, "같은 runId를 두 번 저장해도 하나만 남는다");
            Assert.AreEqual(sample.RunId, pending[0].RunId);
            Assert.AreEqual(sample.Wave, pending[0].Wave);
        }

        [Test]
        public void 받은_기록은_지우고_거부된_기록은_다시_보내지_않는다()
        {
            var outbox = new RunOutbox(directory);
            var accepted = RunProtocolTests.Sample(Guid.NewGuid());
            var rejected = RunProtocolTests.Sample(Guid.NewGuid());
            outbox.Save(accepted);
            outbox.Save(rejected);

            outbox.Remove(accepted.RunId);
            outbox.MarkRejected(rejected.RunId);

            Assert.AreEqual(0, outbox.LoadPending().Count);
            Assert.AreEqual(1, Directory.GetFiles(directory, "*.rejected").Length);
        }

        [Test]
        public void 깨지거나_쓰다_만_파일은_대기열에_섞이지_않는다()
        {
            var outbox = new RunOutbox(directory);
            var sample = RunProtocolTests.Sample(Guid.NewGuid());
            outbox.Save(sample);
            File.WriteAllBytes(Path.Combine(directory, Guid.NewGuid().ToString("N") + ".run"), new byte[] { 1, 2, 3 });
            var partial = RunProtocol.EncodeSubmit(RunProtocolTests.Sample(Guid.NewGuid())).Take(10).ToArray();
            File.WriteAllBytes(Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp"), partial);

            var pending = outbox.LoadPending();
            Assert.AreEqual(1, pending.Count, "멀쩡한 기록만 읽는다");
            Assert.AreEqual(sample.RunId, pending[0].RunId);
            Assert.AreEqual(1, Directory.GetFiles(directory, "*.bad").Length, "깨진 파일은 bad로 바꾼다");
        }
    }
}
