using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class ExternalPbrTexturePackImporter
    {
        private const string PolyHavenManifestPath = "Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/PolyHavenExternalPBR_Manifest.json";
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

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ExternalPbrTexturePackImporter] Imported textures={imported}, materials={materials}, root={MaterialRoot}");
        }

        private static int ImportManifest(string manifestPath, string providerName, ref int materials)
        {
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning($"[ExternalPbrTexturePackImporter] Missing manifest: {manifestPath}");
                return 0;
            }

            ExternalPbrManifest manifest = JsonUtility.FromJson<ExternalPbrManifest>(File.ReadAllText(manifestPath));
            if (manifest == null || manifest.assets == null)
                return 0;

            EnsureFolder($"{MaterialRoot}/{providerName}");

            int imported = 0;
            for (int i = 0; i < manifest.assets.Length; i++)
            {
                ExternalPbrAsset asset = manifest.assets[i];
                if (asset == null || asset.maps == null || string.IsNullOrWhiteSpace(asset.id))
                    continue;

                imported += ImportTexture(asset.maps.BaseColor, TextureImporterType.Default, true);
                imported += ImportTexture(asset.maps.NormalGL, TextureImporterType.NormalMap, false);
                imported += ImportTexture(asset.maps.MaskMap_UnityURP, TextureImporterType.Default, false);
                imported += ImportTexture(asset.maps.Height, TextureImporterType.Default, false);
                imported += ImportTexture(asset.maps.ARM_AO_Rough_Metal, TextureImporterType.Default, false);
                imported += ImportTexture(asset.maps.Roughness, TextureImporterType.Default, false);
                imported += ImportTexture(asset.maps.AO, TextureImporterType.Default, false);
                imported += ImportTexture(asset.maps.Metalness, TextureImporterType.Default, false);

                if (CreateMaterial(providerName, asset))
                    materials++;
            }

            return imported;
        }

        private static int ImportTexture(string assetPath, TextureImporterType textureType, bool sRgb)
        {
            assetPath = NormalizeAssetPath(assetPath);
            if (string.IsNullOrWhiteSpace(assetPath))
                return 0;
            if (!File.Exists(assetPath))
            {
                Debug.LogWarning($"[ExternalPbrTexturePackImporter] Missing texture: {assetPath}");
                return 0;
            }

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return 0;

            importer.textureType = textureType;
            importer.sRGBTexture = sRgb;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = 2048;
            importer.alphaIsTransparency = false;
            importer.SaveAndReimport();
            return 1;
        }

        private static bool CreateMaterial(string providerName, ExternalPbrAsset asset)
        {
            Texture2D baseColor = LoadTexture(asset.maps.BaseColor);
            Texture2D normal = LoadTexture(asset.maps.NormalGL);
            Texture2D maskMap = LoadTexture(asset.maps.MaskMap_UnityURP);
            Texture2D arm = LoadTexture(asset.maps.ARM_AO_Rough_Metal);
            Texture2D height = LoadTexture(asset.maps.Height);
            if (baseColor == null || normal == null || maskMap == null)
            {
                Debug.LogWarning($"[ExternalPbrTexturePackImporter] Cannot create material for {providerName}/{asset.id}: missing base, normal, or mask map.");
                return false;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogWarning("[ExternalPbrTexturePackImporter] No supported Lit shader found.");
                return false;
            }

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
            SetTextureIfPresent(material, "_OcclusionMap", arm);
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
            SetKeyword(material, "_OCCLUSIONMAP", arm != null);
            SetKeyword(material, "_PARALLAXMAP", height != null);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return true;
        }

        private static Texture2D LoadTexture(string assetPath)
        {
            assetPath = NormalizeAssetPath(assetPath);
            return string.IsNullOrWhiteSpace(assetPath) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace("\\", "/").Trim();
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
