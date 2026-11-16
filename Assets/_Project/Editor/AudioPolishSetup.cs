using System;
using PokerDefense.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PokerDefense.Editor
{
    /**
     * AudioPolishSetup
     *
     * 오디오 클립의 임포트 설정과 AudioManager의 효과음·BGM 값을 코드에 적힌 값으로 맞추는 클래스
     * 효과음마다 클립·볼륨·재생 간격·음높이 흔들림을 정함
     * 실행하면 인스펙터에서 직접 고친 값은 덮어씀
     */
    public static class AudioPolishSetup
    {
        const string Folder = "Assets/_Project/Audio/";

        // Sfx 순서와 같은 순서의 파일 이름
        public static readonly string[] CueFiles = {
            "ui_button", "ui_card_deal", "unit_place", "merge_star2", "merge_star3",
            "fire_rapid", "impact_hit", "enemy_death", "hand_high", "state_life_loss", "state_wave_clear",
            "ui_card_flip", "ui_card_select", "ui_exchange", "unit_summon",
            "joker_promote", "fire_heavy", "fire_multi", "fire_splash", "fire_pierce",
            "impact_splash", "impact_pierce", "enemy_death_big", "boss_appear", "hand_low", "hand_mid",
            "state_game_over", "state_stage_clear", "fire_royal_rifle", "fire_pistol"
        };

        [MenuItem("Tools/Poker Defense/Apply Audio Polish")]
        public static void Apply()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before applying audio assets.");
            }

            AudioManager manager = UnityEngine.Object.FindAnyObjectByType<AudioManager>();

            if (manager == null)
            {
                throw new InvalidOperationException("Open Game scene first.");
            }

            // BGM은 메모리를 아끼도록 압축해 재생 중에 읽고, 효과음은 바로 나도록 압축 없이 모노로 미리 올려 둠
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Folder.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AudioImporter importer = (AudioImporter)AssetImporter.GetAtPath(path);
                bool music = System.IO.Path.GetFileName(path).StartsWith("bgm_", StringComparison.Ordinal);

                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
                settings.quality = 0.85f;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                settings.preloadAudioData = !music;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = !music;
                importer.loadInBackground = music;
                importer.SaveAndReimport();
            }

            Undo.RecordObject(manager, "Configure audio polish");

            SerializedObject so = new SerializedObject(manager);
            SerializedProperty clips = so.FindProperty("cues");
            clips.arraySize = CueFiles.Length;

            for (int i = 0; i < CueFiles.Length; i++)
            {
                SerializedProperty cue = clips.GetArrayElementAtIndex(i);
                cue.FindPropertyRelative("id").enumValueIndex = i;

                bool fire = CueFiles[i].StartsWith("fire_", StringComparison.Ordinal);
                bool paper = CueFiles[i].StartsWith("ui_card_", StringComparison.Ordinal) || CueFiles[i] == "ui_exchange";
                bool battle = AudioManager.IsCombatCue((AudioManager.Sfx)i);
                AudioManager.Sfx id = (AudioManager.Sfx)i;
                bool musical = id == AudioManager.Sfx.MergeStar2 || id == AudioManager.Sfx.MergeStar3
                    || id == AudioManager.Sfx.HandHigh || id == AudioManager.Sfx.LifeLoss || id == AudioManager.Sfx.WaveClear
                    || id == AudioManager.Sfx.UnitSummon || id == AudioManager.Sfx.JokerPromote
                    || id >= AudioManager.Sfx.EnemyDeathBig;

                SerializedProperty variants = cue.FindPropertyRelative("variants");
                variants.arraySize = fire || paper ? 3 : 1;

                for (int v = 0; v < variants.arraySize; v++)
                {
                    variants.GetArrayElementAtIndex(v).objectReferenceValue =
                        Required("sfx_" + CueFiles[i] + (v == 0 ? "" : "_v" + (v + 1)));
                }

                cue.FindPropertyRelative("gain").floatValue = fire ? 0.22f : battle ? 0.11f : musical ? 0.33f : 0.20f;

                // 자주 나는 소리는 작게, 드문 이벤트의 소리는 크게 함
                switch ((AudioManager.Sfx)i)
                {
                    case AudioManager.Sfx.UiButton: cue.FindPropertyRelative("gain").floatValue = 0.12f; break;
                    case AudioManager.Sfx.CardDeal: cue.FindPropertyRelative("gain").floatValue = 0.18f; break;
                    case AudioManager.Sfx.CardSelect: cue.FindPropertyRelative("gain").floatValue = 0.25f; break;
                    case AudioManager.Sfx.CardFlip:
                    case AudioManager.Sfx.Exchange: cue.FindPropertyRelative("gain").floatValue = 0.22f; break;
                    case AudioManager.Sfx.UnitSummon: cue.FindPropertyRelative("gain").floatValue = 0.12f; break;
                    case AudioManager.Sfx.HandLow: cue.FindPropertyRelative("gain").floatValue = 0.14f; break;
                    case AudioManager.Sfx.WaveClear: cue.FindPropertyRelative("gain").floatValue = 0.16f; break;
                    case AudioManager.Sfx.HandMid: cue.FindPropertyRelative("gain").floatValue = 0.20f; break;
                    case AudioManager.Sfx.MergeStar2: cue.FindPropertyRelative("gain").floatValue = 0.24f; break;
                    case AudioManager.Sfx.MergeStar3:
                    case AudioManager.Sfx.HandHigh: cue.FindPropertyRelative("gain").floatValue = 0.28f; break;
                }

                cue.FindPropertyRelative("cooldown").floatValue = fire ? 0.075f : battle ? 0.085f : musical ? 0.3f : 0.035f;

                if ((AudioManager.Sfx)i == AudioManager.Sfx.CardDeal)
                {
                    cue.FindPropertyRelative("cooldown").floatValue = 0.45f;
                }

                if ((AudioManager.Sfx)i == AudioManager.Sfx.FireRoyalRifle)
                {
                    cue.FindPropertyRelative("gain").floatValue = 0.24f;
                    cue.FindPropertyRelative("cooldown").floatValue = AudioManager.RoyalRifleCooldown;
                }

                cue.FindPropertyRelative("pitchVariation").floatValue = musical || paper ? 0f : fire ? 0.035f : 0.025f;
            }

            so.FindProperty("bgmPlan").objectReferenceValue = Required("bgm_combat");
            so.FindProperty("bgmCombat").objectReferenceValue = Required("bgm_combat");
            so.FindProperty("bgmBoss").objectReferenceValue = Required("bgm_combat");
            so.FindProperty("bgmResult").objectReferenceValue = Required("bgm_combat");
            so.FindProperty("combat").objectReferenceValue = UnityEngine.Object.FindAnyObjectByType<PokerDefense.Game.CombatController>();
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            EditorSceneManager.SaveScene(manager.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Audio polish V2 configured: 30 cues / 52 SFX clips, 1 streaming music clip.");
        }

        static AudioClip Required(string name)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + name + ".wav");

            if (clip == null)
            {
                throw new InvalidOperationException("Missing audio clip: " + name);
            }

            return clip;
        }
    }
}
