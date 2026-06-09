using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class Batch34TerrainLayerAssetBuilder
    {
        private const string BatchRoot = "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases/Batch20260608_TextureExpansion";
        private const string TilesRoot = BatchRoot + "/Tiles";
        private const string ManifestPath = BatchRoot + "/GeminiMaterialAtlas_Manifest.json";
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
            new TerrainLayerSpec(
                "gemini_Batch20260608_TextureExpansion_b34_3409_limestone_cave_ceiling_mineral_drip",
                "L_B34_3409_LimestoneCaveCeilingMineralDrip.terrainlayer",
                0.42f, 0.43f, 0.39f,
                0.74f, 0.76f, 0.68f),
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

            RequireExistingFolder(BatchRoot, "Batch34 generated texture root");
            RequireExistingFolder(TilesRoot, "Batch34 generated texture tiles");

            TerrainManifest manifest = LoadManifest();
            ValidateTerrainInputs(manifest);
            EnsureFolder(OutputRoot);

            int created = 0;
            int rebound = 0;
            for (int i = 0; i < Specs.Length; i++)
            {
                TerrainLayerSpec spec = Specs[i];
                TerrainAsset asset = RequireTerrainAsset(manifest, spec);
                bool createdLayer = BuildLayer(spec, asset);
                if (createdLayer)
                    created++;
                else
                    rebound++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Batch34TerrainLayerAssetBuilder] Built={Specs.Length}, created={created}, rebound={rebound}, output={OutputRoot}");
        }

        private static TerrainManifest LoadManifest()
        {
            string manifestFilePath = ResolveProjectFilePath(ManifestPath);
            if (!File.Exists(manifestFilePath))
                throw new InvalidOperationException($"[Batch34TerrainLayerAssetBuilder] Missing manifest: {ManifestPath}");

            TerrainManifest manifest = JsonUtility.FromJson<TerrainManifest>(File.ReadAllText(manifestFilePath));
            if (manifest == null || manifest.assets == null || manifest.assets.Length == 0)
                throw new InvalidOperationException("[Batch34TerrainLayerAssetBuilder] Empty Batch34 manifest.");

            return manifest;
        }

        private static void ValidateTerrainInputs(TerrainManifest manifest)
        {
            for (int i = 0; i < Specs.Length; i++)
            {
                TerrainLayerSpec spec = Specs[i];
                TerrainAsset asset = RequireTerrainAsset(manifest, spec);
                RequireTexture(asset.maps.BaseColor, spec.MaterialId, "BaseColor");
                RequireTexture(asset.maps.NormalGL, spec.MaterialId, "NormalGL");
                RequireTexture(asset.maps.MaskMap_UnityURP, spec.MaterialId, "MaskMap_UnityURP");
            }
        }

        private static TerrainAsset RequireTerrainAsset(TerrainManifest manifest, TerrainLayerSpec spec)
        {
            for (int i = 0; i < manifest.assets.Length; i++)
            {
                TerrainAsset asset = manifest.assets[i];
                if (asset == null || !string.Equals(asset.id, spec.MaterialId, StringComparison.Ordinal))
                    continue;

                if (!asset.geologyAllowed)
                    throw new InvalidOperationException($"[Batch34TerrainLayerAssetBuilder] Missing terrain-ready asset: material={spec.MaterialId} geologyAllowed=false");
                if (asset.maps == null)
                    throw new InvalidOperationException($"[Batch34TerrainLayerAssetBuilder] Missing terrain-ready asset: material={spec.MaterialId} maps=null");

                return asset;
            }

            throw new InvalidOperationException($"[Batch34TerrainLayerAssetBuilder] Missing terrain-ready asset: material={spec.MaterialId}");
        }

        private static bool BuildLayer(TerrainLayerSpec spec, TerrainAsset asset)
        {
            Texture2D diffuse = RequireTexture(asset.maps.BaseColor, spec.MaterialId, "BaseColor");
            Texture2D normal = RequireTexture(asset.maps.NormalGL, spec.MaterialId, "NormalGL");
            Texture2D mask = RequireTexture(asset.maps.MaskMap_UnityURP, spec.MaterialId, "MaskMap_UnityURP");

            string outputPath = $"{OutputRoot}/{spec.OutputName}";
            ReportDuplicateLayerPaths(spec, outputPath);

            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(outputPath);
            bool created = layer == null;
            if (created)
            {
                if (File.Exists(ResolveProjectFilePath(outputPath)))
                    throw new InvalidOperationException($"[Batch34TerrainLayerAssetBuilder] Stale TerrainLayer path is occupied by a non-TerrainLayer asset: material={spec.MaterialId}, path={outputPath}");

                layer = new TerrainLayer();
                layer.name = spec.LayerName;
                AssetDatabase.CreateAsset(layer, outputPath);
            }

            float tileSize = Mathf.Clamp(asset.tilingScale, 0.5f, 8f);
            layer.name = spec.LayerName;
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
            return created;
        }

        private static Texture2D RequireTexture(string assetPath, string materialId, string mapKey)
        {
            assetPath = NormalizeAssetPath(assetPath);
            string missingLabel = mapKey == "BaseColor"
                ? "Missing BaseColor map"
                : mapKey == "NormalGL"
                    ? "Missing NormalGL map"
                    : mapKey == "MaskMap_UnityURP"
                        ? "Missing MaskMap_UnityURP map"
                        : "Missing required terrain map";
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new InvalidOperationException($"[Batch34TerrainLayerAssetBuilder] {missingLabel}: material={materialId}. Missing required terrain map.");
            if (!IsProjectAssetPath(assetPath))
                throw new InvalidOperationException($"[Batch34TerrainLayerAssetBuilder] Stale {mapKey} map path outside Assets: material={materialId}, path={assetPath}. {missingLabel}. Missing required terrain map.");
            if (!File.Exists(ResolveProjectFilePath(assetPath)))
                throw new InvalidOperationException($"[Batch34TerrainLayerAssetBuilder] {missingLabel} file: material={materialId}, path={assetPath}. Missing required terrain map.");

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null)
                throw new InvalidOperationException($"[Batch34TerrainLayerAssetBuilder] Missing required terrain map: material={materialId}, map={mapKey}, path={assetPath}");

            return texture;
        }

        private static void ReportDuplicateLayerPaths(TerrainLayerSpec spec, string canonicalPath)
        {
            string[] guids = AssetDatabase.FindAssets(spec.LayerName + " t:TerrainLayer");
            if (guids == null || guids.Length == 0)
                return;

            string[] duplicatePaths = new string[guids.Length];
            int duplicateCount = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (string.IsNullOrWhiteSpace(path) ||
                    string.Equals(path, canonicalPath, StringComparison.Ordinal))
                    continue;

                string fileName = Path.GetFileName(path);
                string layerName = Path.GetFileNameWithoutExtension(path);
                if (string.Equals(fileName, spec.OutputName, StringComparison.Ordinal) ||
                    string.Equals(layerName, spec.LayerName, StringComparison.Ordinal))
                    duplicatePaths[duplicateCount++] = path;
            }

            if (duplicateCount == 0)
                return;

            Array.Sort(duplicatePaths, 0, duplicateCount, StringComparer.Ordinal);
            throw new InvalidOperationException($"[Batch34TerrainLayerAssetBuilder] Duplicate TerrainLayer path for material={spec.MaterialId}: canonical={canonicalPath}, duplicate={duplicatePaths[0]}");
        }

        private static void RequireExistingFolder(string path, string label)
        {
            string normalizedPath = NormalizeAssetPath(path);
            if (!IsProjectAssetPath(normalizedPath))
                throw new InvalidOperationException($"[Batch34TerrainLayerAssetBuilder] Invalid {label} folder path: {normalizedPath}");
            if (!Directory.Exists(ResolveProjectFilePath(normalizedPath)))
                throw new InvalidOperationException($"[Batch34TerrainLayerAssetBuilder] Missing {label} folder: {normalizedPath}");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException($"[Batch34TerrainLayerAssetBuilder] Invalid folder path: {path}");

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
            if (!AssetDatabase.IsValidFolder(path))
                throw new InvalidOperationException($"[Batch34TerrainLayerAssetBuilder] Failed to create output folder: {path}");
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

        private readonly struct TerrainLayerSpec
        {
            public readonly string MaterialId;
            public readonly string OutputName;
            public readonly string LayerName;
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
                LayerName = Path.GetFileNameWithoutExtension(outputName);
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
            public string Height;
            public string MaskMap_UnityURP;
        }
    }
}
