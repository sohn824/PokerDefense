using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace PokerDefense.Editor
{
    /**
     * FontBakeSetup
     *
     * 게임에 나오는 글자를 모아 Maplestory Light·Bold 폰트 에셋에 미리 굽는 클래스
     * 구운 에셋은 정적이라 실행 중에 아틀라스가 늘거나 파일이 바뀌지 않음
     * 목록에 없는 글자는 동적 폴백 에셋(... SDF Dynamic)이 그때그때 채움
     * 문구를 추가했으면 다시 실행함
     */
    public static class FontBakeSetup
    {
        const string Folder = "Assets/_Project/Art/Fonts/";
        // 지금 글자 수(약 470자)가 아틀라스 한 장에 여유 있게 들어가는 크기
        // 여백 비율은 기존 90pt·9px과 같은 10%로 유지함
        const int AtlasSize = 2048;
        const int SamplingSize = 80;
        const int Padding = 8;

        static readonly string[] Styles = { "Light", "Bold" };

        // 화면에 나오는 문구가 들어 있는 폴더
        static readonly string[] SourceFolders = {
            "Assets/_Project/Scripts", "Assets/_Project/Scenes", "Assets/_Project/Prefabs", "Assets/_Project/Data"
        };

        // 스크립트의 문자열·문자 리터럴
        static readonly Regex Literal = new Regex("\"(?:\\\\.|[^\"\\\\\\n])*\"|'(?:\\\\.|[^'\\\\\\n])'");
        // 씬·에셋 파일은 한글을 \uXXXX로 적음
        static readonly Regex Escape =new Regex(@"\\u([0-9a-fA-F]{4})");

        [MenuItem("Tools/Poker Defense/Bake Static Fonts")]
        public static void Bake()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before baking fonts.");
            }

            string characters = CollectCharacters();
            var report = new StringBuilder($"Baked {characters.Length} characters");
            foreach (string style in Styles)
            {
                TMP_FontAsset fallback = LoadOrCreateDynamic(style);
                TMP_FontAsset font = BakeStatic(style, characters, fallback, out string missing);
                report.Append($"\n{font.name}: {font.atlasTextures.Length} atlas");
                if (missing.Length > 0)
                {
                    report.Append($", missing in font file: {missing}");
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log(report.ToString());
        }

        // 출력할 수 있는 ASCII 전부와, 문구 파일에 나오는 ASCII 밖의 글자
        // 스크립트는 주석을 빼고 문자열·문자 리터럴 안만 봄
        // 말줄임표는 문구에 없어도 TMP가 넘친 글자를 줄일 때 넣음
        static string CollectCharacters()
        {
            var set = new SortedSet<char> { '…' };
            for (char c = ' '; c <= '~'; c++)
            {
                set.Add(c);
            }

            foreach (string folder in SourceFolders)
            {
                foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                {
                    string extension = Path.GetExtension(path);
                    if (extension == ".cs")
                    {
                        if (path.Replace('\\', '/').Contains("/Tests/"))
                        {
                            continue;
                        }
                        foreach (string line in File.ReadLines(path))
                        {
                            string trimmed = line.TrimStart();
                            if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*"))
                            {
                                continue;
                            }
                            foreach (Match match in Literal.Matches(line))
                            {
                                AddNonAscii(set, match.Value);
                            }
                        }
                    }
                    else if (extension == ".unity" || extension == ".prefab" || extension == ".asset")
                    {
                        AddNonAscii(set, File.ReadAllText(path));
                    }
                }
            }
            return new string(set.ToArray());
        }

        static void AddNonAscii(SortedSet<char> set, string text)
        {
            text = Escape.Replace(text, m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
            foreach (char c in text)
            {
                if (c > '~' && char.IsControl(c) == false && char.IsSurrogate(c) == false)
                {
                    set.Add(c);
                }
            }
        }

        // 폴백용 동적 에셋을 비워서 돌려줌 (없으면 정적 에셋을 복사해 만듦)
        static TMP_FontAsset LoadOrCreateDynamic(string style)
        {
            string path = Folder + $"Maplestory {style} SDF Dynamic.asset";
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path) == null)
            {
                AssetDatabase.CopyAsset(StaticPath(style), path);
            }

            TMP_FontAsset dynamic = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            dynamic.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            dynamic.fallbackFontAssetTable = new List<TMP_FontAsset>();
            dynamic.ClearFontAssetData(true);
            RemoveUnusedTextures(dynamic);
            EditorUtility.SetDirty(dynamic);
            return dynamic;
        }

        // 기존 에셋을 그대로 고쳐 GUID·머티리얼 참조를 유지함
        static TMP_FontAsset BakeStatic(string style, string characters, TMP_FontAsset fallback, out string missing)
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(StaticPath(style));

            // 글리프를 그리려면 원본 폰트 파일이 필요해 잠시 동적으로 돌림
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            FontEngine.InitializeFontEngine();
            FontEngine.LoadFontFace(font.sourceFontFile, SamplingSize);
            font.faceInfo = FontEngine.GetFaceInfo();
            var serialized = new SerializedObject(font);
            serialized.FindProperty("m_AtlasWidth").intValue = AtlasSize;
            serialized.FindProperty("m_AtlasHeight").intValue = AtlasSize;
            serialized.FindProperty("m_AtlasPadding").intValue = Padding;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            font.ClearFontAssetData();
            font.TryAddCharacters(characters, out missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            font.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };

            font.material.SetFloat(ShaderUtilities.ID_TextureWidth, AtlasSize);
            font.material.SetFloat(ShaderUtilities.ID_TextureHeight, AtlasSize);
            font.material.SetFloat(ShaderUtilities.ID_GradientScale, Padding + 1);
            RemoveUnusedTextures(font);

            EditorUtility.SetDirty(font);
            EditorUtility.SetDirty(font.material);
            foreach (Texture2D texture in font.atlasTextures)
            {
                EditorUtility.SetDirty(texture);
            }
            return font;
        }

        static string StaticPath(string style) => Folder + $"Maplestory {style} SDF.asset";

        // 아틀라스 목록에 없는 텍스처 서브에셋을 지움 (예전에 늘었다 남은 아틀라스)
        static void RemoveUnusedTextures(TMP_FontAsset font)
        {
            string path = AssetDatabase.GetAssetPath(font);
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Texture2D texture && Array.IndexOf(font.atlasTextures, texture) < 0)
                {
                    AssetDatabase.RemoveObjectFromAsset(texture);
                    UnityEngine.Object.DestroyImmediate(texture, true);
                }
            }
        }
    }
}
