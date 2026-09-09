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
     * Audio 폴더의 클립 임포트 설정과 씬의 AudioManager cues 배열을 한 번에 맞춰 주는 에디터 도구
     * 값은 코드로 고정해 두고 메뉴에서 다시 적용할 수 있게 한다 (손으로 인스펙터를 채우지 않는다)
     */
    public static class AudioPolishSetup
    {
        const string Folder = "Assets/_Project/Audio/";

        // AudioManager.Sfx enum 순서와 1:1로 맞춘 파일 이름 (sfx_ 접두사와 .wav 확장자는 뺀 것)
        public static readonly string[] CueFiles = {
            "ui_button", "ui_card_deal", "ui_chip_gain", "unit_place", "merge_star2", "merge_star3",
            "fire_rapid", "impact_hit", "enemy_death", "hand_high", "state_life_loss", "state_wave_clear",
            "ui_card_flip", "ui_card_select", "ui_exchange", "ui_holdbonus", "ui_shop_open", "unit_summon",
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

            // BGM은 스트리밍 + Vorbis, SFX는 통째로 풀어 두는 PCM + 모노
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
                bool musical = i == 4 || i == 5 || i == 9 || i == 10 || i == 11 || i >= 25
                    || i == 15 || i == 16 || i == 17 || i == 18;

                // 발사음과 종이 넘기는 계열만 변형 클립 3개, 나머지는 1개
                SerializedProperty variants = cue.FindPropertyRelative("variants");
                variants.arraySize = fire || paper ? 3 : 1;

                for (int v = 0; v < variants.arraySize; v++)
                {
                    variants.GetArrayElementAtIndex(v).objectReferenceValue =
                        Required("sfx_" + CueFiles[i] + (v == 0 ? "" : "_v" + (v + 1)));
                }

                cue.FindPropertyRelative("gain").floatValue = fire ? 0.22f : battle ? 0.11f : musical ? 0.33f : 0.20f;

                // 자주 울리는 피드백은 작고 담백하게, 큰 강세는 드문 이벤트에만 남겨 둔다
                switch ((AudioManager.Sfx)i)
                {
                    case AudioManager.Sfx.UiButton: cue.FindPropertyRelative("gain").floatValue = 0.12f; break;
                    case AudioManager.Sfx.CardDeal: cue.FindPropertyRelative("gain").floatValue = 0.18f; break;
                    case AudioManager.Sfx.CardSelect: cue.FindPropertyRelative("gain").floatValue = 0.25f; break;
                    case AudioManager.Sfx.CardFlip:
                    case AudioManager.Sfx.Exchange: cue.FindPropertyRelative("gain").floatValue = 0.22f; break;
                    case AudioManager.Sfx.HoldBonus:
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
                    // 한 번 공격에 타격 이벤트가 여러 개 나오므로 그 중복은 쳐 내되,
                    // Sovereign의 초당 13.5발 최대 발사 속도는 그대로 통과시킨다
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
            Debug.Log("Audio polish V2 configured: 31 cues / 49 SFX clips, 4 streaming music arrangements.");
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
