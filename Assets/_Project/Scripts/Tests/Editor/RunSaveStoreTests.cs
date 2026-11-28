using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PokerDefense.Game;

namespace PokerDefense.Tests
{
    /**
     * RunSaveStoreTests
     *
     * 이어하기 저장 파일을 쓰고 읽은 값이 같은지, 깨졌거나 형식이 다른 저장을 걸러 내는지 검사하는 클래스
     */
    public class RunSaveStoreTests
    {
        string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "PokerDefenseSaveTest_" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        static RunSaveData Sample()
        {
            var round = new RoundContext(42);
            round.Draw();
            round.Exchange(new[] { 1, 3 });
            round.TryRevealCandidates(3);

            var data = new RunSaveData
            {
                life = 12,
                jokers = 2,
                waveIndex = 17,
                roundNumber = 18,
                elapsedSeconds = 612.5f,
                usedDevSkip = true,
                roundSeed = round.Seed,
                roundActions = round.Actions.Select(RoundActionData.From).ToList(),
                pendingUnitId = "unit_scout",
                pendingStar = 1,
                summons = 15,
                bestHand = 6,
                bestUnitId = "unit_king",
                bestUnitStar = 3,
            };
            data.boardUnitIds[0] = "unit_king";
            data.boardStars[0] = 3;
            data.boardUnitIds[7] = "unit_scout";
            data.boardStars[7] = 2;
            return data;
        }

        [Test]
        public void 저장한_진행을_다시_읽으면_값이_같다()
        {
            var saved = Sample();
            new RunSaveStore(directory).Save(saved);

            Assert.AreEqual(RunSaveLoadResult.Loaded, new RunSaveStore(directory).TryLoad(out RunSaveData loaded));

            Assert.AreEqual(saved.life, loaded.life);
            Assert.AreEqual(saved.jokers, loaded.jokers);
            Assert.AreEqual(saved.waveIndex, loaded.waveIndex);
            Assert.AreEqual(saved.roundNumber, loaded.roundNumber);
            Assert.AreEqual(saved.elapsedSeconds, loaded.elapsedSeconds);
            Assert.AreEqual(saved.usedDevSkip, loaded.usedDevSkip);
            Assert.AreEqual(saved.roundSeed, loaded.roundSeed);
            CollectionAssert.AreEqual(saved.boardUnitIds, loaded.boardUnitIds);
            CollectionAssert.AreEqual(saved.boardStars, loaded.boardStars);
            Assert.AreEqual(saved.pendingUnitId, loaded.pendingUnitId);
            Assert.AreEqual(saved.summons, loaded.summons);
            Assert.AreEqual(saved.bestHand, loaded.bestHand);
            Assert.AreEqual(saved.bestUnitId, loaded.bestUnitId);
            Assert.AreEqual(saved.bestUnitStar, loaded.bestUnitStar);

            // 형식이 같은 이전 규칙(hand-loop-v1) 저장도 이어서 함
            var previous = Sample();
            previous.ruleset = "hand-loop-v1";
            new RunSaveStore(directory).Save(previous);
            Assert.AreEqual(RunSaveLoadResult.Loaded, new RunSaveStore(directory).TryLoad(out _), "v1 저장");
        }

        [Test]
        public void 읽은_행동_기록으로_같은_라운드를_재현한다()
        {
            var saved = Sample();
            new RunSaveStore(directory).Save(saved);
            new RunSaveStore(directory).TryLoad(out RunSaveData loaded);

            var original = RoundContext.Replay(saved.roundSeed, saved.roundActions.Select(a => a.ToAction()).ToList());
            var restored = RoundContext.Replay(loaded.roundSeed, loaded.roundActions.Select(a => a.ToAction()).ToList());

            CollectionAssert.AreEqual(original.Hand, restored.Hand);
            CollectionAssert.AreEqual(original.Candidates, restored.Candidates);
            Assert.IsTrue(restored.IsChoosingCandidate);
        }

        [Test]
        public void 다시_저장하면_덮어쓰고_임시_파일이_남지_않는다()
        {
            var store = new RunSaveStore(directory);
            var data = Sample();
            store.Save(data);

            data.life = 5;
            store.Save(data);

            store.TryLoad(out RunSaveData loaded);
            Assert.AreEqual(5, loaded.life);
            CollectionAssert.AreEquivalent(new[] { "current.json" }, Directory.GetFiles(directory).Select(Path.GetFileName));
        }

        [Test]
        public void 저장이_없거나_지우면_None이다()
        {
            var store = new RunSaveStore(directory);
            Assert.AreEqual(RunSaveLoadResult.None, store.TryLoad(out _));

            store.Save(Sample());
            store.Delete();

            Assert.IsFalse(store.Exists);
            Assert.AreEqual(RunSaveLoadResult.None, store.TryLoad(out _));
        }

        [Test]
        public void 쓰다_만_임시_파일만_있으면_저장이_없는_것으로_본다()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "current.tmp"), "{\"life\":");

            Assert.AreEqual(RunSaveLoadResult.None, new RunSaveStore(directory).TryLoad(out _));
        }

        [TestCase("{\"life\":", TestName = "JSON이 잘림")]
        [TestCase("not json", TestName = "JSON이 아님")]
        [TestCase("{\"version\":999,\"ruleset\":\"hand-loop-v2\"}", TestName = "형식 버전이 다름")]
        [TestCase("{\"version\":1,\"ruleset\":\"economy-v0\"}", TestName = "규칙이 다름")]
        [TestCase("{\"version\":1,\"ruleset\":\"hand-loop-v2\",\"boardStars\":[1,2]}", TestName = "보드 칸 수가 다름")]
        public void 쓸_수_없는_저장은_Invalid이고_bad로_옮긴다(string json)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "current.json"), json);
            var store = new RunSaveStore(directory);

            Assert.AreEqual(RunSaveLoadResult.Invalid, store.TryLoad(out RunSaveData data));
            Assert.IsNull(data);
            Assert.IsFalse(store.Exists, "다음 실행에 다시 읽지 않는다");
            Assert.IsTrue(File.Exists(Path.Combine(directory, "current.bad")));
        }

        [Test]
        public void 알_수_없는_행동_종류는_예외를_던진다()
        {
            var data = new RoundActionData { type = 99 };

            Assert.Throws<InvalidOperationException>(() => data.ToAction());
        }
    }
}
