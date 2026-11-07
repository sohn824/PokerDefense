using NUnit.Framework;
using PokerDefense.Game;
using PokerDefense.UI;
using UnityEditor;
using UnityEngine;

namespace PokerDefense.Tests
{
    /**
     * AudioPolishTests
     *
     * 오디오 큐 매핑과 쿨다운 규칙, 그리고 에셋 팩이 enum과 어긋나지 않는지를 고정함
     */
    public sealed class AudioPolishTests
    {
        [TestCase("Scout", AudioManager.Sfx.FirePistol)]
        [TestCase("Gunslinger", AudioManager.Sfx.FirePistol)]
        [TestCase("Ace", AudioManager.Sfx.FireRapid)]
        public void 권총_발사음은_지정_유닛에만_쓰인다(string unitName, AudioManager.Sfx cue)
        {
            var unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Project/Data/Units/Unit_" + unitName + ".asset");
            Assert.IsNotNull(unit);
            Assert.AreEqual(cue, AudioManager.FireCue(unit));
            Assert.IsTrue(AudioManager.IsCombatCue(cue));
        }

        [Test]
        public void 쿨다운은_같은_큐만_막는다()
        {
            var gate = new AudioCueGate();

            // 같은 큐는 쿨다운이 끝나기 전까지 막고, 다른 큐(19)는 영향을 받지 않음
            gate.MarkPlayed(6, 10, 0.075);
            Assert.IsFalse(gate.IsReady(6, 10));
            Assert.IsFalse(gate.IsReady(6, 10.074));
            Assert.IsTrue(gate.IsReady(6, 10.075));
            Assert.IsTrue(gate.IsReady(19, 10));

            gate.Clear();
            Assert.IsTrue(gate.IsReady(6, 0));
        }

        [TestCase(AttackPattern.Rapid, AudioManager.Sfx.FireRapid)]
        [TestCase(AttackPattern.Heavy, AudioManager.Sfx.FireHeavy)]
        [TestCase(AttackPattern.Multi, AudioManager.Sfx.FireMulti)]
        [TestCase(AttackPattern.Splash, AudioManager.Sfx.FireSplash)]
        [TestCase(AttackPattern.Pierce, AudioManager.Sfx.FirePierce)]
        public void 공격_패턴마다_발사음이_따로_있다(AttackPattern pattern, AudioManager.Sfx expected)
            => Assert.AreEqual(expected, AudioManager.FireCue(pattern));

        [Test]
        public void 중요한_알림음은_전투_보이스를_쓰지_않는다()
        {
            // 중요한 알림음은 전투 보이스 풀에 들어가면 안 됨 (발사음에 밀리므로)
            foreach (var cue in new[] { AudioManager.Sfx.HandHigh, AudioManager.Sfx.BossAppear,
                AudioManager.Sfx.GameOver, AudioManager.Sfx.StageClear, AudioManager.Sfx.MergeStar3,
                AudioManager.Sfx.UiButton, AudioManager.Sfx.EnemyDeathBig })
                Assert.IsFalse(AudioManager.IsCombatCue(cue), cue.ToString());
        }

        [Test]
        public void 로열_라이플은_전용_발사음이고_최고_공격속도를_따라간다()
        {
            var royal = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Project/Data/Units/Unit_Sovereign.asset");
            Assert.IsNotNull(royal);
            Assert.AreEqual(AudioManager.Sfx.FireRoyalRifle, AudioManager.FireCue(royal));
            Assert.IsTrue(AudioManager.IsCombatCue(AudioManager.Sfx.FireRoyalRifle));
            Assert.AreEqual(AudioManager.Sfx.FireMulti, AudioManager.FireCue(AttackPattern.Multi));

            float cooldown = AudioManager.RoyalRifleCooldown;
            Assert.Greater(cooldown, 0);
            Assert.Less(cooldown, 1f / (royal.AttacksPerSecond * 2.25f));

            var gate = new AudioCueGate();
            gate.MarkPlayed((int)AudioManager.Sfx.FireRoyalRifle, 10, cooldown);
            Assert.IsFalse(gate.IsReady((int)AudioManager.Sfx.FireRoyalRifle, 10));
            Assert.IsTrue(gate.IsReady((int)AudioManager.Sfx.FireRoyalRifle, 10 + 1.0 / 13.5));
        }

        [Test]
        public void 오디오_팩에_모든_큐와_변형_클립이_있다()
        {
            Assert.AreEqual(System.Enum.GetValues(typeof(AudioManager.Sfx)).Length,
                PokerDefense.Editor.AudioPolishSetup.CueFiles.Length);

            foreach (string name in PokerDefense.Editor.AudioPolishSetup.CueFiles)
            {
                int count = name.StartsWith("fire_") || name.StartsWith("ui_card_") || name == "ui_exchange" ? 3 : 1;

                for (int v = 0; v < count; v++)
                {
                    string path = "Assets/_Project/Audio/sfx_" + name + (v == 0 ? "" : "_v" + (v + 1)) + ".wav";
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    Assert.IsNotNull(clip, path);
                    Assert.AreEqual(1, clip.channels, path);
                    Assert.AreEqual(48000, clip.frequency, path);
                    Assert.Greater(clip.length, 0.02f, path);
                }
            }
        }

        [Test]
        public void 전투_BGM_길이가_루프_마디에_정확히_맞는다()
        {
            string path = "Assets/_Project/Audio/bgm_combat.wav";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            Assert.IsNotNull(clip, path);
            Assert.AreEqual(2, clip.channels, path);
            // Arena Breaks - 40마디, 마디당 4박, 124 BPM, 48 kHz
            Assert.AreEqual(48000, clip.frequency, path);
            int expectedSamples = (int)System.Math.Round(40 * 4 * 60.0 / 124 * 48000);
            Assert.AreEqual(expectedSamples, clip.samples, path);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            Assert.AreEqual(AudioClipLoadType.Streaming, importer.defaultSampleSettings.loadType, path);
        }
    }
}
