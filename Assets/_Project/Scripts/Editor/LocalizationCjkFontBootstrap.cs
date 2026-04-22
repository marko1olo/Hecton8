using System.IO;
using System.Text;
using System.Collections.Generic;
using Hecton.Localization;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Hecton8.Editor
{
    /// <summary>
    /// Creates and wires CJK TMP fallback assets for first-party localization fonts.
    /// </summary>
    public static class LocalizationCjkFontBootstrap
    {
        private const string SourceScPath = "Assets/_Project/Art/Fonts/NotoSansCJKsc-Regular.otf";
        private const string SourceJpPath = "Assets/_Project/Art/Fonts/NotoSansCJKjp-Regular.otf";
        private const string SourceArabicPath = "Assets/_Project/Art/Fonts/NotoSansArabic-Regular.ttf";
        private const string AssetScPath = "Assets/_Project/Art/Materials/Fonts/NotoSansCJKsc-Regular SDF.asset";
        private const string AssetJpPath = "Assets/_Project/Art/Materials/Fonts/NotoSansCJKjp-Regular SDF.asset";
        private const string AssetArabicPrimePath = "Assets/_Project/Art/Materials/Fonts/NotoSansArabic-Prime SDF.asset";
        private const string PrimaryTextAssetPath = "Assets/_Project/Art/Materials/Fonts/текст SDF.asset";
        private const string NumericTextAssetPath = "Assets/_Project/Art/Materials/Fonts/Ñ†Ð¸Ñ„Ñ€Ñ‹ SDF.asset";
        private const string LiberationSansAssetPath = "Assets/_Project/Data/LiberationSans SDF.asset";
        private const string ChineseLocalizationPath = "Assets/_Project/Scripts/ChineseSimplified.json";
        private const string JapaneseLocalizationPath = "Assets/_Project/Scripts/Japanese.json";
        private const string ArabicLocalizationPath = "Assets/_Project/Scripts/Arabic.json";
        private const string PrimaryTextAssetPathUtf = "Assets/_Project/Art/Materials/Fonts/\u0442\u0435\u043a\u0441\u0442 SDF.asset";
        private const string NumericTextAssetPathUtf = "Assets/_Project/Art/Materials/Fonts/\u0446\u0438\u0444\u0440\u044b SDF.asset";
        private const int SamplingPointSize = 90;
        private const int AtlasPadding = 9;
        private const int AtlasWidth = 2048;
        private const int AtlasHeight = 2048;
        private const int DefaultDynamicAtlasSize = 1024;
        private const string PrimeScCharacters = "氧压深海看着你不要玻璃名字";
        private const string PrimeJpCharacters = "再生中圧力見るな息深海ガラス";
        private const string PrimeArabicCharacters = "ابتداءضغطتحذيرمراقبةتنفس";

        private static readonly string[] TargetFontAssetPaths =
        {
            "Assets/_Project/Art/Materials/Fonts/текст SDF.asset",
            "Assets/_Project/Art/Materials/Fonts/цифры SDF.asset",
            "Assets/_Project/Art/Materials/Fonts/NotoSans-Regular SDF.asset",
            "Assets/_Project/Art/Materials/Fonts/NotoSansArabic-Regular SDF.asset",
            "Assets/_Project/Data/LiberationSans SDF.asset",
        };

        [MenuItem("Hecton8/Localization/Bootstrap CJK TMP Fallbacks")]
        public static void RunFromMenu()
        {
            Run();
        }

        /// <summary>
        /// Creates CJK TMP assets and wires them into TMP fallback chains.
        /// </summary>
        public static void Run()
        {
            AssetDatabase.Refresh();

            string[] targetFontAssetPaths =
            {
                PrimaryTextAssetPathUtf,
                NumericTextAssetPathUtf,
                "Assets/_Project/Art/Materials/Fonts/NotoSans-Regular SDF.asset",
                "Assets/_Project/Art/Materials/Fonts/NotoSansArabic-Regular SDF.asset",
                LiberationSansAssetPath,
            };

            Font sourceSc = AssetDatabase.LoadAssetAtPath<Font>(SourceScPath);
            Font sourceJp = AssetDatabase.LoadAssetAtPath<Font>(SourceJpPath);
            Font sourceArabic = AssetDatabase.LoadAssetAtPath<Font>(SourceArabicPath);
            if (sourceSc == null || sourceJp == null || sourceArabic == null)
                throw new System.InvalidOperationException("Required localization source fonts are missing from Assets/_Project/Art/Fonts.");

            TMP_FontAsset scAsset = EnsureFontAsset(sourceSc, AssetScPath, "NotoSansCJKsc-Regular SDF");
            TMP_FontAsset jpAsset = EnsureFontAsset(sourceJp, AssetJpPath, "NotoSansCJKjp-Regular SDF");
            TMP_FontAsset arabicAsset = EnsureFontAsset(sourceArabic, AssetArabicPrimePath, "NotoSansArabic-Prime SDF");
            string scCharacters = BuildLocalizedCharacterSeed(ChineseLocalizationPath, PrimeScCharacters);
            string jpCharacters = BuildLocalizedCharacterSeed(JapaneseLocalizationPath, PrimeJpCharacters);
            string arabicCharacters = BuildLocalizedCharacterSeed(ArabicLocalizationPath, PrimeArabicCharacters);
            PrimeAtlas(scAsset, scCharacters);
            PrimeAtlas(jpAsset, jpCharacters);
            PrimeAtlas(arabicAsset, arabicCharacters);

            EnsureGlobalFallback(scAsset);
            EnsureGlobalFallback(jpAsset);
            EnsureGlobalFallback(arabicAsset);
            TMP_FontAsset liberationAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationSansAssetPath);

            for (int i = 0; i < targetFontAssetPaths.Length; i++)
            {
                TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(targetFontAssetPaths[i]);
                if (fontAsset == null)
                    continue;

                EnsureFontMaterial(fontAsset, fontAsset.name);
                RemoveFallback(fontAsset.fallbackFontAssetTable, "NotoSans-Regular SDF");
                RemoveFallback(fontAsset.fallbackFontAssetTable, "NotoSansArabic-Regular SDF");
                RemoveFallback(fontAsset.fallbackFontAssetTable, "NotoSansCJKsc-Regular SDF");
                RemoveFallback(fontAsset.fallbackFontAssetTable, "NotoSansCJKjp-Regular SDF");
                RemoveFallback(fontAsset.fallbackFontAssetTable, "NotoSansArabic-Prime SDF");
                RemoveFallback(fontAsset.fallbackFontAssetTable, "LiberationSans SDF");
                EnsureFallback(fontAsset.fallbackFontAssetTable, scAsset);
                EnsureFallback(fontAsset.fallbackFontAssetTable, jpAsset);
                EnsureFallback(fontAsset.fallbackFontAssetTable, arabicAsset);
                if (liberationAsset != null && !ReferenceEquals(fontAsset, liberationAsset))
                    EnsureFallback(fontAsset.fallbackFontAssetTable, liberationAsset);
                ApplyTargetFontRuntimePolicy(targetFontAssetPaths[i], fontAsset);
                EditorUtility.SetDirty(fontAsset);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Localization] CJK TMP fallback assets created and wired.");
        }

        private static TMP_FontAsset EnsureFontAsset(Font sourceFont, string assetPath, string assetName)
        {
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (fontAsset != null && ShouldRecreateGeneratedFontAsset(fontAsset))
            {
                AssetDatabase.DeleteAsset(assetPath);
                fontAsset = null;
            }

            if (fontAsset == null)
            {
                fontAsset = TMP_FontAsset.CreateFontAsset(
                    sourceFont,
                    SamplingPointSize,
                    AtlasPadding,
                    GlyphRenderMode.SDFAA,
                    AtlasWidth,
                    AtlasHeight,
                    AtlasPopulationMode.Dynamic,
                    true);

                fontAsset.name = assetName;
                fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                fontAsset.isMultiAtlasTexturesEnabled = true;
                AssetDatabase.CreateAsset(fontAsset, assetPath);
            }
            else
            {
                fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                fontAsset.isMultiAtlasTexturesEnabled = true;
                EditorUtility.SetDirty(fontAsset);
            }

            SetClearDynamicDataOnBuild(fontAsset, false);
            EnsureFontMaterial(fontAsset, assetName);
            EnsureAtlasReadable(fontAsset);
            return fontAsset;
        }

        private static void EnsureGlobalFallback(TMP_FontAsset fontAsset)
        {
            TMP_Settings settings = TMP_Settings.instance;
            if (settings == null || fontAsset == null)
                return;

            EnsureFallback(TMP_Settings.fallbackFontAssets, fontAsset);
            EditorUtility.SetDirty(settings);
        }

        private static void EnsureFallback(List<TMP_FontAsset> targetList, TMP_FontAsset fallbackAsset)
        {
            if (targetList == null || fallbackAsset == null)
                return;

            for (int i = 0; i < targetList.Count; i++)
            {
                if (targetList[i] == fallbackAsset)
                    return;
            }

            targetList.Add(fallbackAsset);
        }

        private static void RemoveFallback(List<TMP_FontAsset> targetList, string assetName)
        {
            if (targetList == null || string.IsNullOrEmpty(assetName))
                return;

            for (int i = targetList.Count - 1; i >= 0; i--)
            {
                TMP_FontAsset fontAsset = targetList[i];
                if (fontAsset != null && string.Equals(fontAsset.name, assetName, System.StringComparison.Ordinal))
                    targetList.RemoveAt(i);
            }
        }

        private static void EnsureFontMaterial(TMP_FontAsset fontAsset, string assetName)
        {
            if (fontAsset == null)
                return;

            EnsureDynamicAtlasReady(fontAsset);

            Material material = fontAsset.material;
            if (material == null)
            {
                Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(fontAsset));
                for (int i = 0; i < subAssets.Length; i++)
                {
                    material = subAssets[i] as Material;
                    if (material != null)
                        break;
                }
            }

            if (material == null)
            {
                Shader shader = Shader.Find("TextMeshPro/Distance Field");
                if (shader == null)
                    throw new System.InvalidOperationException("TextMeshPro/Distance Field shader not found.");

                material = new Material(shader)
                {
                    name = assetName.Replace(" SDF", " Atlas Material")
                };

                AssetDatabase.AddObjectToAsset(material, fontAsset);
            }

            material.SetTexture(ShaderUtilities.ID_MainTex, ResolveAtlasTexture(fontAsset));
            material.SetFloat(ShaderUtilities.ID_TextureWidth, fontAsset.atlasWidth);
            material.SetFloat(ShaderUtilities.ID_TextureHeight, fontAsset.atlasHeight);
            material.SetFloat(ShaderUtilities.ID_GradientScale, 10f);

            SerializedObject serializedFontAsset = new SerializedObject(fontAsset);
            SerializedProperty materialProperty = serializedFontAsset.FindProperty("m_Material");
            if (materialProperty != null)
            {
                materialProperty.objectReferenceValue = material;
                serializedFontAsset.ApplyModifiedPropertiesWithoutUndo();
            }

            fontAsset.material = material;
            EditorUtility.SetDirty(material);
            EditorUtility.SetDirty(fontAsset);
        }

        private static Texture ResolveAtlasTexture(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null)
                return null;

            try
            {
                Texture[] atlasTextures = fontAsset.atlasTextures;
                if (atlasTextures == null || atlasTextures.Length == 0)
                    return null;

                return atlasTextures[0];
            }
            catch (MissingReferenceException)
            {
                return null;
            }
        }

        private static void PrimeAtlas(TMP_FontAsset fontAsset, string authoredCharacters)
        {
            if (fontAsset == null || string.IsNullOrEmpty(authoredCharacters))
                return;

            EnsureDynamicAtlasReady(fontAsset);
            fontAsset.TryAddCharacters(authoredCharacters, out _);
            SetClearDynamicDataOnBuild(fontAsset, false);
            EnsureAtlasReadable(fontAsset);

            if (fontAsset.material != null)
                fontAsset.material.SetTexture(ShaderUtilities.ID_MainTex, ResolveAtlasTexture(fontAsset));

            EditorUtility.SetDirty(fontAsset);
            if (fontAsset.material != null)
                EditorUtility.SetDirty(fontAsset.material);
        }

        private static void ApplyTargetFontRuntimePolicy(string assetPath, TMP_FontAsset fontAsset)
        {
            if (fontAsset == null)
                return;

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;

            bool isBoundedPrimaryFont =
                string.Equals(assetPath, LiberationSansAssetPath, System.StringComparison.Ordinal) ||
                string.Equals(assetPath, PrimaryTextAssetPathUtf, System.StringComparison.Ordinal) ||
                string.Equals(assetPath, NumericTextAssetPathUtf, System.StringComparison.Ordinal);
            fontAsset.isMultiAtlasTexturesEnabled = !isBoundedPrimaryFont;
            SetClearDynamicDataOnBuild(fontAsset, false);
            if (!isBoundedPrimaryFont)
                return;

            SerializedObject serializedFontAsset = new SerializedObject(fontAsset);
            SerializedProperty atlasWidthProperty = serializedFontAsset.FindProperty("m_AtlasWidth");
            SerializedProperty atlasHeightProperty = serializedFontAsset.FindProperty("m_AtlasHeight");
            if (atlasWidthProperty != null)
                atlasWidthProperty.intValue = DefaultDynamicAtlasSize;

            if (atlasHeightProperty != null)
                atlasHeightProperty.intValue = DefaultDynamicAtlasSize;

            serializedFontAsset.ApplyModifiedPropertiesWithoutUndo();
            EnsureAtlasReadable(fontAsset);
        }

        private static string BuildLocalizedCharacterSeed(string tableAssetPath, string authoredSeed)
        {
            // COLD ALLOC: HashSet<char>[estimated localization glyph set] — unique seeded localization glyphs — owner: LocalizationCjkFontBootstrap
            var characters = new HashSet<char>();
            AppendSeedCharacters(authoredSeed, characters);

            string fullPath = ResolveProjectPath(tableAssetPath);
            if (!File.Exists(fullPath))
                return authoredSeed;

            string json = File.ReadAllText(fullPath);
            Dictionary<string, string> table = LocalizationManager.ParseFlatJsonTable(json);
            Dictionary<string, string>.Enumerator enumerator = table.GetEnumerator();
            while (enumerator.MoveNext())
                AppendSeedCharacters(enumerator.Current.Value, characters);

            // COLD ALLOC: StringBuilder[unique glyph count] — assembled glyph seed string — owner: LocalizationCjkFontBootstrap
            var builder = new StringBuilder(characters.Count);
            HashSet<char>.Enumerator characterEnumerator = characters.GetEnumerator();
            while (characterEnumerator.MoveNext())
                builder.Append(characterEnumerator.Current);

            return builder.ToString();
        }

        private static void AppendSeedCharacters(string source, HashSet<char> characters)
        {
            if (string.IsNullOrEmpty(source) || characters == null)
                return;

            bool insideTag = false;
            for (int i = 0; i < source.Length; i++)
            {
                char character = source[i];
                if (character == '<')
                {
                    insideTag = true;
                    continue;
                }

                if (character == '>')
                {
                    insideTag = false;
                    continue;
                }

                if (insideTag || char.IsControl(character))
                    continue;

                characters.Add(character);
            }
        }

        private static string ResolveProjectPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return string.Empty;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void EnsureAtlasReadable(TMP_FontAsset fontAsset)
        {
            Texture atlasTexture = ResolveAtlasTexture(fontAsset);
            if (atlasTexture == null)
                return;

            SerializedObject serializedTexture = new SerializedObject(atlasTexture);
            SerializedProperty readableProperty = serializedTexture.FindProperty("m_IsReadable");
            if (readableProperty == null || readableProperty.boolValue)
                return;

            readableProperty.boolValue = true;
            serializedTexture.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(atlasTexture);
        }

        private static void EnsureDynamicAtlasReady(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null || ResolveAtlasTexture(fontAsset) != null)
                return;

            ResetBrokenAtlasTextureReferences(fontAsset);
            fontAsset.ClearFontAssetData(false);
            SetClearDynamicDataOnBuild(fontAsset, false);
            EditorUtility.SetDirty(fontAsset);
        }

        private static bool ShouldRecreateGeneratedFontAsset(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null)
                return false;

            try
            {
                Texture atlasTexture = ResolveAtlasTexture(fontAsset);
                return atlasTexture == null;
            }
            catch (MissingReferenceException)
            {
                return true;
            }
        }

        private static void SetClearDynamicDataOnBuild(TMP_FontAsset fontAsset, bool clearDynamicDataOnBuild)
        {
            if (fontAsset == null)
                return;

            SerializedObject serializedFontAsset = new SerializedObject(fontAsset);
            SerializedProperty clearDynamicDataProperty = serializedFontAsset.FindProperty("m_ClearDynamicDataOnBuild");
            if (clearDynamicDataProperty == null || clearDynamicDataProperty.boolValue == clearDynamicDataOnBuild)
                return;

            clearDynamicDataProperty.boolValue = clearDynamicDataOnBuild;
            serializedFontAsset.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fontAsset);
        }

        private static void ResetBrokenAtlasTextureReferences(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null)
                return;

            Texture2D atlasTexture = FindOrCreateAtlasTextureSubAsset(fontAsset);
            SerializedObject serializedFontAsset = new SerializedObject(fontAsset);
            SerializedProperty atlasTexturesProperty = serializedFontAsset.FindProperty("m_AtlasTextures");
            if (atlasTexturesProperty != null)
            {
                if (atlasTexturesProperty.arraySize == 0)
                    atlasTexturesProperty.arraySize = 1;

                atlasTexturesProperty.GetArrayElementAtIndex(0).objectReferenceValue = atlasTexture;
            }

            SerializedProperty atlasTextureIndexProperty = serializedFontAsset.FindProperty("m_AtlasTextureIndex");
            if (atlasTextureIndexProperty != null && atlasTextureIndexProperty.intValue != 0)
                atlasTextureIndexProperty.intValue = 0;

            serializedFontAsset.ApplyModifiedPropertiesWithoutUndo();

            if (fontAsset.material != null &&
                atlasTexture != null)
            {
                fontAsset.material.SetTexture(ShaderUtilities.ID_MainTex, atlasTexture);
                EditorUtility.SetDirty(fontAsset.material);
            }
        }

        private static Texture2D FindOrCreateAtlasTextureSubAsset(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null)
                return null;

            string assetPath = AssetDatabase.GetAssetPath(fontAsset);
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int assetIndex = 0; assetIndex < subAssets.Length; assetIndex++)
            {
                Texture2D texture = subAssets[assetIndex] as Texture2D;
                if (texture != null)
                    return texture;
            }

            int atlasWidth = Mathf.Max(1, fontAsset.atlasWidth);
            int atlasHeight = Mathf.Max(1, fontAsset.atlasHeight);
            var atlasTexture = new Texture2D(atlasWidth, atlasHeight, TextureFormat.Alpha8, false)
            {
                name = fontAsset.name.Replace(" SDF", " Atlas"),
                hideFlags = HideFlags.HideInHierarchy
            };

            AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
            EditorUtility.SetDirty(atlasTexture);
            return atlasTexture;
        }
    }
}
