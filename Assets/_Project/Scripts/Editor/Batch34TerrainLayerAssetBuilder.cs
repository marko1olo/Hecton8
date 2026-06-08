using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class Batch34TerrainLayerAssetBuilder
    {
        private const string ManifestPath = "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases/Batch20260608_TextureExpansion/GeminiMaterialAtlas_Manifest.json";
        private const string OutputRoot = "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases/Batch20260608_TextureExpansion/TerrainLayers";

        private static readonly TerrainLayerSpec[] Specs =
        {
            new TerrainLayerSpec(
                "gemini_Batch20260608_TextureExpansion_b34_3401_photic_limestone_rubble_shelf",
                "L_B34_3401_PhoticLimestoneRubbleShelf.terrainlayer",
                0.52f, 0.50f, 0.44f,
                0.82f, 0.78f, 0.68f),
            new TerrainLayerSpec(
                "gemini_Batch20260608_TextureExpansion_b34_3402_shallow_seagrass_root_mat_substrate",
                "L_B34_3402_ShallowSeagrassRootMat.terrainlayer",
                0.28f, 0.34f, 0.24f,
                0.58f, 0.66f, 0.48f),
            new TerrainLayerSpec(
                "gemini_Batch20260608_TextureExpansion_b34_3403_brine_canyon_salt_crust_silt",
                "L_B34_3403_BrineCanyonSaltCrustSilt.terrainlayer",
                0.30f, 0.34f, 0.38f,
                0.62f, 0.66f, 0.68f),
            new TerrainLayerSpec(
                "gemini_Batch20260608_TextureExpansion_b34_3404_abyssal_manganese_nodule_plain",
                "L_B34_3404_AbyssalManganeseNodulePlain.terrainlayer",
                0.16f, 0.15f, 0.14f,
                0.36f, 0.34f, 0.30f),
            new TerrainLayerSpec(
                "gemini_Batch20260608_TextureExpansion_b34_3405_methane_hydrate_crack_vein",
                "L_B34_3405_MethaneHydrateCrackVein.terrainlayer",
                0.24f, 0.30f, 0.38f,
                0.58f, 0.66f, 0.76f),
            new TerrainLayerSpec(
                "gemini_Batch20260608_TextureExpansion_b34_3406_serpentinite_fault_rock",
                "L_B34_3406_SerpentiniteFaultRock.terrainlayer",
                0.10f, 0.16f, 0.14f,
                0.36f, 0.46f, 0.38f),
            new TerrainLayerSpec(
                "gemini_Batch20260608_TextureExpansion_b34_3408_clay_silt_turbidity_slope",
                "L_B34_3408_ClaySiltTurbiditySlope.terrainlayer",
                0.32f, 0.30f, 0.26f,
                0.60f, 0.58f, 0.52f),
        };

        [MenuItem("Hecton8/Art/Build Batch34 Gemini Terrain Layers")]
        public static void ExecuteMenu()
        {
            BuildTerrainLayers();
        }

        public static void BuildTerrainLayers()
        {
            BuildTerrainLayers(true);
        }

        public static void BuildTerrainLayers(bool importFirst)
        {
            if (importFirst)
                ExternalPbrTexturePackImporter.ImportExternalPbrTexturePacks();

            string manifestFilePath = ResolveProjectFilePath(ManifestPath);
            if (!File.Exists(manifestFilePath))
                throw new InvalidOperationException($"[Batch34TerrainLayerAssetBuilder] Missing manifest: {ManifestPath}");

            TerrainManifest manifest = JsonUtility.FromJson<TerrainManifest>(File.ReadAllText(manifestFilePath));
            if (manifest == null || manifest.assets == null || manifest.assets.Length == 0)
                throw new InvalidOperationException("[Batch34TerrainLayerAssetBuilder] Empty Batch34 manifest.");

            ValidateTerrainInputs(manifest);

            EnsureFolder(OutputRoot);

            int built = 0;
            for (int i = 0; i < Specs.Length; i++)
            {
                TerrainLayerSpec spec = Specs[i];
                TerrainAsset asset = FindAsset(manifest, spec.MaterialId);
                BuildLayer(spec, asset);
                built++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Batch34TerrainLayerAssetBuilder] Built={built}, output={OutputRoot}");
        }

        private static void BuildLayer(TerrainLayerSpec spec, TerrainAsset asset)
        {
            Texture2D diffuse = RequireTexture(asset.maps.BaseColor, spec.MaterialId, "BaseColor");
            Texture2D normal = RequireTexture(asset.maps.NormalGL, spec.MaterialId, "NormalGL");
            Texture2D mask = RequireTexture(asset.maps.MaskMap_UnityURP, spec.MaterialId, "MaskMap_UnityURP");

            string outputPath = $"{OutputRoot}/{spec.OutputName}";
            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(outputPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, outputPath);
            }

            float tileSize = Mathf.Clamp(asset.tilingScale, 0.5f, 8f);
            layer.diffuseTexture = diffuse;
            layer.normalMapTexture = normal;
            layer.maskMapTexture = mask;
            layer.tileSize = new Vector2(tileSize, tileSize);
            layer.tileOffset = Vector2.zero;
            layer.normalScale = Mathf.Clamp(asset.normalScale, 0f, 2f);
            layer.metallic = Mathf.Clamp01(asset.metallic);
            layer.smoothness = Mathf.Clamp01(asset.smoothness);
            layer.diffuseRemapMin = spec.DiffuseRemapMin;
            layer.diffuseRemapMax = spec.DiffuseRemapMax;
            layer.maskMapRemapMin = Vector4.zero;
            layer.maskMapRemapMax = Vector4.one;
            EditorUtility.SetDirty(layer);
        }

        private static void ValidateTerrainInputs(TerrainManifest manifest)
        {
            for (int i = 0; i < Specs.Length; i++)
            {
                TerrainLayerSpec spec = Specs[i];
                TerrainAsset asset = FindAsset(manifest, spec.MaterialId);
                if (asset == null || asset.maps == null || !asset.geologyAllowed)
                    throw new InvalidOperationException($"[Batch34TerrainLayerAssetBuilder] Missing terrain-ready asset: {spec.MaterialId}");

                if (LoadTexture(asset.maps.BaseColor) == null)
                    throw new InvalidOperationException($"[Batch34TerrainLayerAssetBuilder] Missing BaseColor map: {spec.MaterialId}");
                if (LoadTexture(asset.maps.NormalGL) == null)
                    throw new InvalidOperationException($"[Batch34TerrainLayerAssetBuilder] Missing NormalGL map: {spec.MaterialId}");
                if (LoadTexture(asset.maps.MaskMap_UnityURP) == null)
                    throw new InvalidOperationException($"[Batch34TerrainLayerAssetBuilder] Missing MaskMap_UnityURP map: {spec.MaterialId}");
            }
        }

        private static TerrainAsset FindAsset(TerrainManifest manifest, string id)
        {
            for (int i = 0; i < manifest.assets.Length; i++)
            {
                TerrainAsset asset = manifest.assets[i];
                if (asset != null && string.Equals(asset.id, id, StringComparison.Ordinal))
                    return asset;
            }

            return null;
        }

        private static Texture2D LoadTexture(string assetPath)
        {
            assetPath = NormalizeAssetPath(assetPath);
            return string.IsNullOrWhiteSpace(assetPath) ||
                   !IsProjectAssetPath(assetPath) ||
                   !File.Exists(ResolveProjectFilePath(assetPath))
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static Texture2D RequireTexture(string assetPath, string materialId, string mapKey)
        {
            Texture2D texture = LoadTexture(assetPath);
            if (texture == null)
                throw new InvalidOperationException($"[Batch34TerrainLayerAssetBuilder] Missing required terrain map {mapKey}: {materialId} path={assetPath}");

            return texture;
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace("\\", "/").Trim();
        }

        private static bool IsProjectAssetPath(string path)
        {
            return path.StartsWith("Assets/", StringComparison.Ordinal) || path == "Assets";
        }

        private static string ResolveProjectFilePath(string assetPath)
        {
            if (Path.IsPathRooted(assetPath))
                return assetPath;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
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

        private readonly struct TerrainLayerSpec
        {
            public readonly string MaterialId;
            public readonly string OutputName;
            public readonly Vector4 DiffuseRemapMin;
            public readonly Vector4 DiffuseRemapMax;

            public TerrainLayerSpec(
                string materialId,
                string outputName,
                float minR,
                float minG,
                float minB,
                float maxR,
                float maxG,
                float maxB)
            {
                MaterialId = materialId;
                OutputName = outputName;
                DiffuseRemapMin = new Vector4(minR, minG, minB, 1f);
                DiffuseRemapMax = new Vector4(maxR, maxG, maxB, 1f);
            }
        }

        [Serializable]
        private sealed class TerrainManifest
        {
            public TerrainAsset[] assets;
        }

        [Serializable]
        private sealed class TerrainAsset
        {
            public string id;
            public bool geologyAllowed;
            public float tilingScale;
            public float metallic;
            public float smoothness;
            public float normalScale;
            public TerrainMaps maps;
        }

        [Serializable]
        private sealed class TerrainMaps
        {
            public string BaseColor;
            public string NormalGL;
            public string MaskMap_UnityURP;
        }
    }
}
