using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PokerDefense.Game;

namespace PokerDefense.Tests
{
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
        public void 저장한_기록을_다시_읽는다()
        {
            var outbox = new RunOutbox(directory);
            var sample = RunProtocolTests.Sample(Guid.NewGuid());
            outbox.Save(sample);

            var pending = new RunOutbox(directory).LoadPending();
            Assert.AreEqual(1, pending.Count);
            Assert.AreEqual(sample.RunId, pending[0].RunId);
            Assert.AreEqual(sample.Wave, pending[0].Wave);
        }

        [Test]
        public void 같은_runId를_두_번_저장해도_하나만_남는다()
        {
            var outbox = new RunOutbox(directory);
            var sample = RunProtocolTests.Sample(Guid.NewGuid());
            outbox.Save(sample);
            outbox.Save(sample);
            Assert.AreEqual(1, outbox.LoadPending().Count);
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
        public void 깨진_파일은_옆으로_치우고_나머지는_읽는다()
        {
            var outbox = new RunOutbox(directory);
            var sample = RunProtocolTests.Sample(Guid.NewGuid());
            outbox.Save(sample);
            File.WriteAllBytes(Path.Combine(directory, Guid.NewGuid().ToString("N") + ".run"), new byte[] { 1, 2, 3 });

            var pending = outbox.LoadPending();
            Assert.AreEqual(1, pending.Count);
            Assert.AreEqual(sample.RunId, pending[0].RunId);
            Assert.AreEqual(1, Directory.GetFiles(directory, "*.bad").Length);
        }

        [Test]
        public void 쓰다_만_임시_파일은_대기열에_섞이지_않는다()
        {
            Directory.CreateDirectory(directory);
            var partial = RunProtocol.EncodeSubmit(RunProtocolTests.Sample(Guid.NewGuid())).Take(10).ToArray();
            File.WriteAllBytes(Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp"), partial);

            Assert.AreEqual(0, new RunOutbox(directory).LoadPending().Count);
        }

        [Test]
        public void 폴더가_없으면_빈_목록이다()
        {
            Assert.AreEqual(0, new RunOutbox(directory).LoadPending().Count);
        }
    }
}
