using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class ExternalPbrTexturePackImporter
    {
        private const string PolyHavenManifestPath = "Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/PolyHavenExternalPBR_Manifest.json";
        private const string GeminiAtlasRoot = "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases";
        private const string GeminiSingleManifestPath = "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialIntake_20260607/GeminiSingleMaterials_Manifest.json";
        private const string GeminiBiomeManifestPath = "Assets/_Project/Art/TEXTURES/Generated/GeminiBiomeMaterialIntake_20260607/GeminiBiomeMaterials_Manifest.json";
        private const string MaterialRoot = "Assets/_Project/Art/Materials/Generated/ExternalPBR_20260607";

        [MenuItem("Hecton8/Art/Import External PBR Texture Packs")]
        public static void ExecuteMenu()
        {
            ImportExternalPbrTexturePacks();
        }

        public static void ImportExternalPbrTexturePacks()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EnsureFolder(MaterialRoot);
            int imported = 0;
            int materials = 0;

            imported += ImportManifest(PolyHavenManifestPath, "PolyHaven", ref materials);
            imported += ImportManifest(GeminiSingleManifestPath, "GeminiSingles_20260607", ref materials);
            imported += ImportManifest(GeminiBiomeManifestPath, "GeminiBiome_20260607", ref materials);
            imported += ImportGeminiAtlases(ref materials);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ExternalPbrTexturePackImporter] Imported textures={imported}, materials={materials}, root={MaterialRoot}");
        }

        private static int ImportGeminiAtlases(ref int materials)
        {
            string resolvedAtlasRoot = ResolveProjectFilePath(GeminiAtlasRoot);
            if (!Directory.Exists(resolvedAtlasRoot))
                throw new InvalidOperationException($"[ExternalPbrTexturePackImporter] Missing Gemini atlas root: {GeminiAtlasRoot}");

            int imported = 0;
            string[] manifests = Directory.GetFiles(resolvedAtlasRoot, "GeminiMaterialAtlas_Manifest.json", SearchOption.AllDirectories);
            Array.Sort(manifests, StringComparer.Ordinal);
            for (int i = 0; i < manifests.Length; i++)
            {
                string manifestPath = NormalizeAssetPath(manifests[i]);
                string batchName = Path.GetFileName(Path.GetDirectoryName(manifestPath));
                string providerName = "Gemini_" + SanitizeProviderName(batchName);
                imported += ImportManifest(manifestPath, providerName, ref materials);
            }

            return imported;
        }

        private static int ImportManifest(string manifestPath, string providerName, ref int materials)
        {
            string resolvedManifestPath = ResolveProjectFilePath(manifestPath);
            if (!File.Exists(resolvedManifestPath))
                throw new InvalidOperationException($"[ExternalPbrTexturePackImporter] Missing manifest: {manifestPath}");

            ExternalPbrManifest manifest = JsonUtility.FromJson<ExternalPbrManifest>(File.ReadAllText(resolvedManifestPath));
            if (manifest == null || manifest.assets == null)
                throw new InvalidOperationException($"[ExternalPbrTexturePackImporter] Invalid manifest payload: {manifestPath}");

            EnsureFolder($"{MaterialRoot}/{providerName}");

            int imported = 0;
            for (int i = 0; i < manifest.assets.Length; i++)
            {
                ExternalPbrAsset asset = manifest.assets[i];
                if (asset == null || asset.maps == null || string.IsNullOrWhiteSpace(asset.id))
                    throw new InvalidOperationException($"[ExternalPbrTexturePackImporter] Invalid material asset entry in {manifestPath} at index {i}");

                imported += ImportRequiredTexture(asset.maps.BaseColor, TextureImporterType.Default, true, asset.id, "BaseColor");
                imported += ImportRequiredTexture(asset.maps.NormalGL, TextureImporterType.NormalMap, false, asset.id, "NormalGL");
                imported += ImportRequiredTexture(asset.maps.MaskMap_UnityURP, TextureImporterType.Default, false, asset.id, "MaskMap_UnityURP");
                imported += ImportRequiredTexture(asset.maps.Height, TextureImporterType.Default, false, asset.id, "Height");
                imported += ImportRequiredTexture(asset.maps.ARM_AO_Rough_Metal, TextureImporterType.Default, false, asset.id, "ARM_AO_Rough_Metal");
                imported += ImportOptionalTexture(asset.maps.Roughness, TextureImporterType.Default, false, asset.id, "Roughness");
                imported += ImportOptionalTexture(asset.maps.AO, TextureImporterType.Default, false, asset.id, "AO");
                imported += ImportOptionalTexture(asset.maps.Metalness, TextureImporterType.Default, false, asset.id, "Metalness");

                CreateMaterial(providerName, asset);
                materials++;
            }

            return imported;
        }

        private static int ImportRequiredTexture(string assetPath, TextureImporterType textureType, bool sRgb, string materialId, string mapKey)
        {
            assetPath = NormalizeAssetPath(assetPath);
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new InvalidOperationException($"[ExternalPbrTexturePackImporter] Missing required texture map {mapKey} for {materialId}");

            return ImportTexture(assetPath, textureType, sRgb, materialId, mapKey);
        }

        private static int ImportOptionalTexture(string assetPath, TextureImporterType textureType, bool sRgb, string materialId, string mapKey)
        {
            assetPath = NormalizeAssetPath(assetPath);
            if (string.IsNullOrWhiteSpace(assetPath))
                return 0;

            return ImportTexture(assetPath, textureType, sRgb, materialId, mapKey);
        }

        private static int ImportTexture(string assetPath, TextureImporterType textureType, bool sRgb, string materialId, string mapKey)
        {
            assetPath = NormalizeAssetPath(assetPath);
            if (!File.Exists(ResolveProjectFilePath(assetPath)))
                throw new InvalidOperationException($"[ExternalPbrTexturePackImporter] Missing texture map {mapKey} for {materialId}: {assetPath}");

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException($"[ExternalPbrTexturePackImporter] Missing TextureImporter for {mapKey} {materialId}: {assetPath}");

            importer.textureType = textureType;
            importer.sRGBTexture = sRgb;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = 2048;
            importer.alphaIsTransparency = false;
            TextureImporterFormat standaloneFormat = textureType == TextureImporterType.NormalMap
                ? TextureImporterFormat.BC5
                : TextureImporterFormat.BC7;
            SetPlatformSettings(importer, "Standalone", 2048, standaloneFormat);
            SetPlatformSettings(importer, "Android", 2048, TextureImporterFormat.ASTC_6x6);
            SetPlatformSettings(importer, "iPhone", 2048, TextureImporterFormat.ASTC_6x6);
            importer.SaveAndReimport();
            return 1;
        }

        private static void SetPlatformSettings(
            TextureImporter importer,
            string platform,
            int maxTextureSize,
            TextureImporterFormat format)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
            settings.overridden = true;
            settings.maxTextureSize = maxTextureSize;
            settings.format = format;
            settings.textureCompression = TextureImporterCompression.CompressedHQ;
            settings.compressionQuality = 100;
            importer.SetPlatformTextureSettings(settings);
        }

        private static void CreateMaterial(string providerName, ExternalPbrAsset asset)
        {
            Texture2D baseColor = RequireTexture(asset.maps.BaseColor, providerName, asset.id, "BaseColor");
            Texture2D normal = RequireTexture(asset.maps.NormalGL, providerName, asset.id, "NormalGL");
            Texture2D maskMap = RequireTexture(asset.maps.MaskMap_UnityURP, providerName, asset.id, "MaskMap_UnityURP");
            Texture2D height = RequireTexture(asset.maps.Height, providerName, asset.id, "Height");

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException("[ExternalPbrTexturePackImporter] No supported Lit shader found.");

            string materialPath = $"{MaterialRoot}/{providerName}/MAT_EXT_{providerName}_{asset.id}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else
            {
                material.shader = shader;
            }

            SetTextureIfPresent(material, "_BaseMap", baseColor);
            SetTextureIfPresent(material, "_MainTex", baseColor);
            SetTextureIfPresent(material, "_BumpMap", normal);
            SetTextureIfPresent(material, "_MetallicGlossMap", maskMap);
            SetTextureIfPresent(material, "_OcclusionMap", maskMap);
            SetTextureIfPresent(material, "_ParallaxMap", height);
            SetTextureScaleIfPresent(material, "_BaseMap", TilingScale(asset));
            SetTextureScaleIfPresent(material, "_MainTex", TilingScale(asset));
            SetTextureScaleIfPresent(material, "_BumpMap", TilingScale(asset));
            SetTextureScaleIfPresent(material, "_MetallicGlossMap", TilingScale(asset));
            SetTextureScaleIfPresent(material, "_OcclusionMap", TilingScale(asset));
            SetTextureScaleIfPresent(material, "_ParallaxMap", TilingScale(asset));
            SetFloatIfPresent(material, "_BumpScale", NormalScale(asset));
            SetFloatIfPresent(material, "_Metallic", Metallic(asset));
            SetFloatIfPresent(material, "_Smoothness", Smoothness(asset));
            SetFloatIfPresent(material, "_SmoothnessTextureChannel", 0f);
            SetFloatIfPresent(material, "_OcclusionStrength", 1f);
            SetFloatIfPresent(material, "_Parallax", HeightScale(asset));
            SetKeyword(material, "_NORMALMAP", normal != null);
            SetKeyword(material, "_METALLICSPECGLOSSMAP", maskMap != null);
            SetKeyword(material, "_OCCLUSIONMAP", maskMap != null);
            SetKeyword(material, "_PARALLAXMAP", height != null);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
        }

        private static Texture2D LoadTexture(string assetPath)
        {
            assetPath = NormalizeAssetPath(assetPath);
            return string.IsNullOrWhiteSpace(assetPath) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static Texture2D RequireTexture(string assetPath, string providerName, string materialId, string mapKey)
        {
            Texture2D texture = LoadTexture(assetPath);
            if (texture == null)
                throw new InvalidOperationException($"[ExternalPbrTexturePackImporter] Cannot create material for {providerName}/{materialId}: missing {mapKey} map source={NormalizeAssetPath(assetPath)}");

            return texture;
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace("\\", "/").Trim();
        }

        private static string ResolveProjectFilePath(string assetOrFilePath)
        {
            string normalized = NormalizeAssetPath(assetOrFilePath);
            if (string.IsNullOrWhiteSpace(normalized) || Path.IsPathRooted(normalized))
                return normalized;

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, normalized);
        }

        private static void SetTextureIfPresent(Material material, string property, Texture texture)
        {
            if (texture != null && material.HasProperty(property))
                material.SetTexture(property, texture);
        }

        private static void SetFloatIfPresent(Material material, string property, float value)
        {
            if (material.HasProperty(property))
                material.SetFloat(property, value);
        }

        private static void SetTextureScaleIfPresent(Material material, string property, float scale)
        {
            if (material.HasProperty(property))
                material.SetTextureScale(property, new Vector2(scale, scale));
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }

        private static float TilingScale(ExternalPbrAsset asset)
        {
            return asset.catalogVersion > 0 ? Mathf.Clamp(asset.tilingScale, 0.25f, 16f) : 1f;
        }

        private static float Metallic(ExternalPbrAsset asset)
        {
            return asset.catalogVersion > 0 ? Mathf.Clamp01(asset.metallic) : DefaultMetallic(asset.id);
        }

        private static float Smoothness(ExternalPbrAsset asset)
        {
            return asset.catalogVersion > 0 ? Mathf.Clamp01(asset.smoothness) : DefaultSmoothness(asset.id);
        }

        private static float NormalScale(ExternalPbrAsset asset)
        {
            return asset.catalogVersion > 0 ? Mathf.Clamp(asset.normalScale, 0f, 2f) : 0.85f;
        }

        private static float HeightScale(ExternalPbrAsset asset)
        {
            return asset.catalogVersion > 0 ? Mathf.Clamp(asset.heightScale, 0f, 0.05f) : 0.012f;
        }

        private static float DefaultMetallic(string id)
        {
            id = id ?? string.Empty;
            if (id.Contains("metal") || id.Contains("shutter") || id.Contains("factory") || id.Contains("corrugated") || id.Contains("iron") || id.Contains("container") || id.Contains("grate"))
                return 0.75f;
            return 0f;
        }

        private static float DefaultSmoothness(string id)
        {
            id = id ?? string.Empty;
            if (id.Contains("rubber"))
                return 0.18f;
            if (id.Contains("plastic"))
                return 0.32f;
            if (id.Contains("blue_metal"))
                return 0.38f;
            if (id.Contains("metal") || id.Contains("shutter") || id.Contains("factory") || id.Contains("corrugated") || id.Contains("iron") || id.Contains("container") || id.Contains("grate"))
                return 0.30f;
            return 0.35f;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException($"Invalid folder path: {path}");

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static string SanitizeProviderName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Atlas";

            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                    chars[i] = '_';
            }

            return new string(chars);
        }

        [Serializable]
        private sealed class ExternalPbrManifest
        {
            public ExternalPbrAsset[] assets;
        }

        [Serializable]
        private sealed class ExternalPbrAsset
        {
            public string id;
            public string title;
            public string source;
            public string license;
            public string role;
            public int catalogVersion;
            public string surfaceClass;
            public bool heldToolAllowed;
            public bool stationPropAllowed;
            public bool salvageAllowed;
            public bool worldPanelAllowed;
            public float tilingScale;
            public float metallic;
            public float smoothness;
            public float normalScale;
            public float heightScale;
            public ExternalPbrMaps maps;
        }

        [Serializable]
        private sealed class ExternalPbrMaps
        {
            public string BaseColor;
            public string NormalGL;
            public string ARM_AO_Rough_Metal;
            public string Height;
            public string MaskMap_UnityURP;
            public string Roughness;
            public string AO;
            public string Metalness;
        }
    }
}
