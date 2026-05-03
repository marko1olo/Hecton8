using TMPro;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.UI
{
    /// <summary>
    /// Cold-path recovery bootstrap for TMP font assets that lost atlas or material links after Unity/TMP migrations.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-950)]
    public sealed class FontAssetRecovery : MonoBehaviour
    {
        private const string LiberationSansAssetPath = "Assets/_Project/Data/LiberationSans SDF.asset";
        private const string PrimaryTextAssetPath = "Assets/_Project/Art/Materials/Fonts/\u0442\u0435\u043a\u0441\u0442 SDF.asset";
        private const string NumericTextAssetPath = "Assets/_Project/Art/Materials/Fonts/\u0446\u0438\u0444\u0440\u044b SDF.asset";
        private const string NotoSansRegularAssetPath = "Assets/_Project/Art/Materials/Fonts/NotoSans-Regular SDF.asset";
        private const string NotoSansArabicRegularAssetPath = "Assets/_Project/Art/Materials/Fonts/NotoSansArabic-Regular SDF.asset";
        private const string NotoSansCjkScAssetPath = "Assets/_Project/Art/Materials/Fonts/NotoSansCJKsc-Regular SDF.asset";
        private const string NotoSansCjkJpAssetPath = "Assets/_Project/Art/Materials/Fonts/NotoSansCJKjp-Regular SDF.asset";
        private const string NotoSansArabicPrimeAssetPath = "Assets/_Project/Art/Materials/Fonts/NotoSansArabic-Prime SDF.asset";
        private const string PrimaryTextFontName = "текст SDF";
        private const string NumericTextFontName = "цифры SDF";
        private const string LiberationSansFontName = "LiberationSans SDF";
        private const string GlyphSeed =
            " 0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
            ".,:;!?+-=*/%()[]{}<>_|\\/@#$&\"'~";

        private static readonly string[] _knownFontAssetPaths =
        {
            PrimaryTextAssetPath,
            NumericTextAssetPath,
            LiberationSansAssetPath,
            NotoSansRegularAssetPath,
            NotoSansArabicRegularAssetPath,
            NotoSansCjkScAssetPath,
            NotoSansCjkJpAssetPath,
            NotoSansArabicPrimeAssetPath,
        };

#if UNITY_EDITOR
        private const string EditorAssetRepairCompletedSessionKey =
            "Hecton8.FontAssetRecovery.EditorAssetRepairCompleted";
        private const string FullSweepMenuPath = "Hecton8/UI/Repair TMP Font Assets (Full Sweep)";

        private static bool _editorAssetRepairCompleted;
        private static bool _editorAssetRepairQueued;

        [InitializeOnLoadMethod]
        private static void BootstrapEditorAssetRepair()
        {
            if (Application.isBatchMode)
                return;

            if (SessionState.GetBool(EditorAssetRepairCompletedSessionKey, false))
                return;

            QueueEditorAssetRepair();
        }

        private static void QueueEditorAssetRepair()
        {
            if (Application.isBatchMode)
                return;

            if (_editorAssetRepairQueued)
                return;

            _editorAssetRepairQueued = true;
            EditorApplication.delayCall -= RepairKnownAssetImports;
            EditorApplication.delayCall += RepairKnownAssetImports;
        }

        [MenuItem(FullSweepMenuPath)]
        private static void RepairKnownAssetImportsFullSweep()
        {
            if (!CanRunEditorAssetRepair())
            {
                EditorApplication.delayCall -= RepairKnownAssetImportsFullSweep;
                EditorApplication.delayCall += RepairKnownAssetImportsFullSweep;
                return;
            }

            RepairKnownAssetImports(includeProjectWideSweep: true, ignoreSessionGate: true);
        }
#endif

        /// <summary>
        /// Creates a transient recovery owner after each scene load so Awake executes without scene wiring.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
#if UNITY_EDITOR
            RepairKnownAssetImports();
#endif
        }

        private void Awake()
        {
            Destroy(gameObject);
        }

        private static void RecoverFontAssets()
        {
            // COLD ALLOC: TMP_FontAsset[loaded count] - loaded font recovery scan - owner: FontAssetRecovery
            TMP_FontAsset[] loadedFonts = System.Array.Empty<TMP_FontAsset>();
            for (int fontIndex = 0; fontIndex < loadedFonts.Length; fontIndex++)
                RecoverFontAsset(loadedFonts[fontIndex]);

            // COLD ALLOC: TMP_Text[loaded count] - loaded text refresh scan - owner: FontAssetRecovery
            TMP_Text[] loadedTextComponents = System.Array.Empty<TMP_Text>();
            for (int textIndex = 0; textIndex < loadedTextComponents.Length; textIndex++)
                RefreshTextComponent(loadedTextComponents[textIndex]);

#if UNITY_EDITOR
            RepairKnownAssetImports();
#endif
        }

        private static void RecoverFontAsset(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null)
                return;

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.isMultiAtlasTexturesEnabled = !IsBoundedPrimaryFont(fontAsset);
            EnsureDynamicAtlasReady(fontAsset);

            bool hasAtlasBinding = ResolveAtlasTexture(fontAsset) != null && fontAsset.material != null;
            if (!hasAtlasBinding)
                PrimeDynamicAtlas(fontAsset);

            TryRepairAtlasBinding(fontAsset);
        }

        private static void PrimeDynamicAtlas(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null)
                return;

            EnsureDynamicAtlasReady(fontAsset);
            try
            {
                fontAsset.TryAddCharacters(GlyphSeed, out _);
            }
            catch (MissingReferenceException)
            {
#if UNITY_EDITOR
                ResetBrokenAtlasTextureReferences(fontAsset);
#endif
            }
            catch (UnassignedReferenceException)
            {
#if UNITY_EDITOR
                ResetBrokenAtlasTextureReferences(fontAsset);
#endif
            }
        }

        private static bool TryRepairAtlasBinding(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null)
                return false;

            Texture atlasTexture = ResolveAtlasTexture(fontAsset);
            Material fontMaterial = fontAsset.material;
            if (atlasTexture == null || fontMaterial == null)
                return false;

            bool changed = false;
            if (fontMaterial.GetTexture(ShaderUtilities.ID_MainTex) != atlasTexture)
            {
                fontMaterial.SetTexture(ShaderUtilities.ID_MainTex, atlasTexture);
                changed = true;
            }

            if (!Mathf.Approximately(fontMaterial.GetFloat(ShaderUtilities.ID_TextureWidth), atlasTexture.width))
            {
                fontMaterial.SetFloat(ShaderUtilities.ID_TextureWidth, atlasTexture.width);
                changed = true;
            }

            if (!Mathf.Approximately(fontMaterial.GetFloat(ShaderUtilities.ID_TextureHeight), atlasTexture.height))
            {
                fontMaterial.SetFloat(ShaderUtilities.ID_TextureHeight, atlasTexture.height);
                changed = true;
            }

            if (!Mathf.Approximately(fontMaterial.GetFloat(ShaderUtilities.ID_GradientScale), 10f))
            {
                fontMaterial.SetFloat(ShaderUtilities.ID_GradientScale, 10f);
                changed = true;
            }

            return changed;
        }

        private static Texture ResolveAtlasTexture(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null)
                return null;

            try
            {
                Texture[] atlasTextures = fontAsset.atlasTextures;
                if (atlasTextures == null || atlasTextures.Length == 0)
                    return fontAsset.material != null
                        ? fontAsset.material.GetTexture(ShaderUtilities.ID_MainTex)
                        : null;

                return atlasTextures[0];
            }
            catch (MissingReferenceException)
            {
                return fontAsset.material != null
                    ? fontAsset.material.GetTexture(ShaderUtilities.ID_MainTex)
                    : null;
            }
            catch (UnassignedReferenceException)
            {
                return fontAsset.material != null
                    ? fontAsset.material.GetTexture(ShaderUtilities.ID_MainTex)
                    : null;
            }
        }

        private static void RefreshTextComponent(TMP_Text textComponent)
        {
            if (textComponent == null || !textComponent.gameObject.scene.IsValid())
                return;

            TMP_FontAsset fontAsset = textComponent.font != null ? textComponent.font : TMP_Settings.defaultFontAsset;
            if (fontAsset == null)
                return;

            RecoverFontAsset(fontAsset);
            textComponent.font = fontAsset;
            if (fontAsset.material != null)
                textComponent.fontSharedMaterial = fontAsset.material;

            textComponent.havePropertiesChanged = true;
            textComponent.UpdateMeshPadding();
            textComponent.ForceMeshUpdate(true, true);
        }

#if UNITY_EDITOR
        private static void RepairKnownAssetImports()
        {
            RepairKnownAssetImports(includeProjectWideSweep: false, ignoreSessionGate: false);
        }

        private static void RepairKnownAssetImports(bool includeProjectWideSweep, bool ignoreSessionGate)
        {
            _editorAssetRepairQueued = false;
            if (!ignoreSessionGate &&
                (_editorAssetRepairCompleted ||
                 SessionState.GetBool(EditorAssetRepairCompletedSessionKey, false)))
                return;

            if (!CanRunEditorAssetRepair())
            {
                QueueEditorAssetRepair();
                return;
            }

            _editorAssetRepairCompleted = true;
            bool assetsChanged = false;
            assetsChanged |= includeProjectWideSweep
                ? DisableProjectDynamicClearDataOnBuild()
                : DisableKnownDynamicClearDataOnBuild();

            for (int pathIndex = 0; pathIndex < _knownFontAssetPaths.Length; pathIndex++)
            {
                TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(_knownFontAssetPaths[pathIndex]);
                if (fontAsset == null)
                    continue;

                assetsChanged |= RepairAssetBackedFont(fontAsset);
            }

            if (assetsChanged)
                AssetDatabase.SaveAssets();

            SessionState.SetBool(EditorAssetRepairCompletedSessionKey, true);
        }

        private static bool CanRunEditorAssetRepair()
        {
            return !Application.isBatchMode &&
                   !EditorApplication.isCompiling &&
                   !EditorApplication.isUpdating;
        }

        private static bool RepairAssetBackedFont(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null)
                return false;

            bool assetChanged = false;
            assetChanged |= EnsureEditorFontMaterial(fontAsset);
            assetChanged |= SetClearDynamicDataOnBuild(fontAsset, false);

            if (fontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            {
                fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                assetChanged = true;
            }

            bool allowMultiAtlas = !IsBoundedPrimaryFont(fontAsset);
            if (fontAsset.isMultiAtlasTexturesEnabled != allowMultiAtlas)
            {
                fontAsset.isMultiAtlasTexturesEnabled = allowMultiAtlas;
                assetChanged = true;
            }

            EnsureDynamicAtlasReady(fontAsset);
            PrimeDynamicAtlas(fontAsset);

            SerializedObject serializedFontAsset = new SerializedObject(fontAsset);
            SerializedProperty atlasTexturesProperty = serializedFontAsset.FindProperty("m_AtlasTextures");
            if (atlasTexturesProperty != null &&
                (atlasTexturesProperty.arraySize == 0 || atlasTexturesProperty.GetArrayElementAtIndex(0).objectReferenceValue == null))
            {
                Texture atlasTexture = ResolveAtlasTexture(fontAsset);
                if (atlasTexture != null)
                {
                    if (atlasTexturesProperty.arraySize == 0)
                        atlasTexturesProperty.arraySize = 1;

                    atlasTexturesProperty.GetArrayElementAtIndex(0).objectReferenceValue = atlasTexture;
                    serializedFontAsset.ApplyModifiedPropertiesWithoutUndo();
                    assetChanged = true;
                }
            }

            Texture atlas = ResolveAtlasTexture(fontAsset);
            if (atlas != null)
                assetChanged |= EnsureReadableTexture(atlas);

            assetChanged |= TryRepairAtlasBinding(fontAsset);

            if (assetChanged)
            {
                EditorUtility.SetDirty(fontAsset);
                if (fontAsset.material != null)
                    EditorUtility.SetDirty(fontAsset.material);
            }

            return assetChanged;
        }

        private static void EnsureDynamicAtlasReady(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null || ResolveAtlasTexture(fontAsset) != null)
                return;

            ResetBrokenAtlasTextureReferences(fontAsset);
            try
            {
                fontAsset.ClearFontAssetData(false);
            }
            catch (MissingReferenceException)
            {
                ResetBrokenAtlasTextureReferences(fontAsset);
            }
            catch (UnassignedReferenceException)
            {
                ResetBrokenAtlasTextureReferences(fontAsset);
            }
        }

        private static bool DisableProjectDynamicClearDataOnBuild()
        {
            bool assetsChanged = false;
            string[] fontAssetGuids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            for (int assetIndex = 0; assetIndex < fontAssetGuids.Length; assetIndex++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(fontAssetGuids[assetIndex]);
                TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
                if (fontAsset == null)
                    continue;

                if (SetClearDynamicDataOnBuild(fontAsset, false))
                    assetsChanged = true;
            }

            return assetsChanged;
        }

        private static bool DisableKnownDynamicClearDataOnBuild()
        {
            bool assetsChanged = false;
            for (int pathIndex = 0; pathIndex < _knownFontAssetPaths.Length; pathIndex++)
            {
                TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(_knownFontAssetPaths[pathIndex]);
                if (fontAsset != null && SetClearDynamicDataOnBuild(fontAsset, false))
                    assetsChanged = true;
            }

            return assetsChanged;
        }

        private static bool SetClearDynamicDataOnBuild(TMP_FontAsset fontAsset, bool clearDynamicDataOnBuild)
        {
            if (fontAsset == null)
                return false;

            SerializedObject serializedFontAsset = new SerializedObject(fontAsset);
            SerializedProperty clearDynamicDataProperty = serializedFontAsset.FindProperty("m_ClearDynamicDataOnBuild");
            if (clearDynamicDataProperty == null || clearDynamicDataProperty.boolValue == clearDynamicDataOnBuild)
                return false;

            clearDynamicDataProperty.boolValue = clearDynamicDataOnBuild;
            serializedFontAsset.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fontAsset);
            return true;
        }

        private static bool EnsureEditorFontMaterial(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null || fontAsset.material != null)
                return false;

            Material material = null;
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(fontAsset));
            for (int assetIndex = 0; assetIndex < subAssets.Length; assetIndex++)
            {
                material = subAssets[assetIndex] as Material;
                if (material != null)
                    break;
            }

            if (material == null)
            {
                Shader shader = Shader.Find("TextMeshPro/Distance Field");
                if (shader == null)
                    return false;

                material = new Material(shader)
                {
                    name = fontAsset.name.Replace(" SDF", " Atlas Material")
                };
                AssetDatabase.AddObjectToAsset(material, fontAsset);
            }

            fontAsset.material = material;
            EditorUtility.SetDirty(material);
            EditorUtility.SetDirty(fontAsset);
            return true;
        }

        private static bool EnsureReadableTexture(Texture atlasTexture)
        {
            if (atlasTexture == null)
                return false;

            SerializedObject serializedTexture = new SerializedObject(atlasTexture);
            SerializedProperty readableProperty = serializedTexture.FindProperty("m_IsReadable");
            bool changed = false;
            if (readableProperty != null && !readableProperty.boolValue)
            {
                readableProperty.boolValue = true;
                serializedTexture.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(atlasTexture);
                changed = true;
            }

            if (changed)
                return true;

            string assetPath = AssetDatabase.GetAssetPath(atlasTexture);
            if (string.IsNullOrEmpty(assetPath))
                return false;

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null || importer.isReadable)
                return false;

            importer.isReadable = true;
            importer.SaveAndReimport();
            return true;
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

        private static bool IsBoundedPrimaryFont(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null)
                return false;

            string fontName = fontAsset.name;
            return string.Equals(fontName, "\u0442\u0435\u043a\u0441\u0442 SDF", System.StringComparison.Ordinal) ||
                   string.Equals(fontName, "\u0446\u0438\u0444\u0440\u044b SDF", System.StringComparison.Ordinal) ||
                   string.Equals(fontName, LiberationSansFontName, System.StringComparison.Ordinal);
        }
#endif
    }
}
