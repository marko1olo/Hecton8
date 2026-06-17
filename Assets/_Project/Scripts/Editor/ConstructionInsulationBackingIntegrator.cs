using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Editor
{
    /// <summary>
    /// Binds the Batch34 damp insulation source to visible opened/backing panels on construction ruin prefabs.
    /// </summary>
    public static class ConstructionInsulationBackingIntegrator
    {
        public const string MaterialPath = "Assets/_Project/Art/Materials/Construction/Mat_Module_InsulationBacking.mat";
        public const string MaterialId = "gemini_Batch20260608_TextureExpansion_b34_3421_damped_insulation_blanket_material";

        private const string GeminiAtlasRoot = "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases";

        private static readonly Assignment BackingAssignment = new Assignment(
            "Assets/_Project/Art/Materials/Construction/Mat_Module_InsulationBacking.mat",
            "gemini_Batch20260608_TextureExpansion_b34_3421_damped_insulation_blanket_material",
            0.85f,
            0.75f,
            0.0f,
            0.28f,
            0.006f,
            "Damaged backing panels: damp insulation blanket for opened interiors, equipment backs, and wreck cut faces.");

        private static readonly BackingPanelSpec[] Panels =
        {
            new BackingPanelSpec(
                "Assets/_Project/Prefabs/Construction/Final/PFB_Debris_WreckField.prefab",
                "LOD0",
                "InsulationBacking_HullInterior",
                new Vector3(-1.82f, 1.12f, 0.98f),
                new Vector3(0f, 20f, 9f),
                new Vector3(1.15f, 0.035f, 0.72f)),
            new BackingPanelSpec(
                "Assets/_Project/Prefabs/Construction/Final/PFB_Debris_WreckField.prefab",
                "LOD0",
                "InsulationBacking_ServicePlateTear",
                new Vector3(0.78f, 0.18f, -1.28f),
                new Vector3(0f, -18f, 0f),
                new Vector3(1.32f, 0.03f, 0.46f)),
            new BackingPanelSpec(
                "Assets/_Project/Prefabs/Construction/Final/PFB_Ruin_ClusterMedium.prefab",
                "LOD0",
                "InsulationBacking_ModuleBOpenFace",
                new Vector3(1.82f, 2.24f, 1.96f),
                new Vector3(0f, 20f, 0f),
                new Vector3(1.05f, 0.035f, 1.82f)),
            new BackingPanelSpec(
                "Assets/_Project/Prefabs/Construction/Final/PFB_Ruin_ClusterMedium.prefab",
                "LOD0",
                "InsulationBacking_BridgeUnderside",
                new Vector3(-0.18f, 1.68f, -0.52f),
                new Vector3(0f, 8f, 10f),
                new Vector3(0.72f, 0.032f, 1.24f)),
            new BackingPanelSpec(
                "Assets/_Project/Prefabs/Construction/Final/PFB_Ruin_Megastructure.prefab",
                "LOD0",
                "InsulationBacking_TowerCutFace",
                new Vector3(2.16f, 4.72f, 2.22f),
                new Vector3(0f, 0f, 0f),
                new Vector3(1.28f, 0.038f, 2.35f)),
            new BackingPanelSpec(
                "Assets/_Project/Prefabs/Construction/Final/PFB_Ruin_Megastructure.prefab",
                "LOD0",
                "InsulationBacking_BridgeCutFace",
                new Vector3(0.72f, 1.62f, 8.04f),
                new Vector3(0f, 18f, 0f),
                new Vector3(1.15f, 0.035f, 1.94f)),
        };

        [MenuItem("Hecton8/Art/Apply Batch34 Construction Insulation Backing")]
        public static void ExecuteMenu()
        {
            Apply();
        }

        public static void Apply()
        {
            Dictionary<string, ExternalPbrAsset> assets = LoadAllManifestAssets();
            ValidateRoute(assets);
            if (!assets.TryGetValue(BackingAssignment.MaterialId, out ExternalPbrAsset asset))
                throw new InvalidOperationException($"[ConstructionInsulationBackingIntegrator] Missing Gemini material id={BackingAssignment.MaterialId}");

            Material material = CreateOrUpdateMaterial(asset, BackingAssignment, out string materialFailure);
            if (material == null)
                throw new InvalidOperationException($"[ConstructionInsulationBackingIntegrator] Material build failed. first={materialFailure}");

            int changedPrefabs = 0;
            int failures = 0;
            string firstFailure = string.Empty;
            for (int i = 0; i < Panels.Length; i++)
            {
                if (ApplyPanel(Panels[i], material, out string failure))
                {
                    changedPrefabs++;
                }
                else
                {
                    RecordFailure(ref failures, ref firstFailure, failure);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (failures > 0)
                throw new InvalidOperationException($"[ConstructionInsulationBackingIntegrator] Insulation backing apply failed. failures={failures}, first={firstFailure}");

            Debug.Log($"[ConstructionInsulationBackingIntegrator] Material={BackingAssignment.MaterialPath}, Panels={Panels.Length}, PrefabWrites={changedPrefabs}");
        }

        private static bool ApplyPanel(BackingPanelSpec spec, Material material, out string failure)
        {
            failure = string.Empty;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath) == null)
            {
                failure = $"Missing prefab: {spec.PrefabPath}";
                return false;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(spec.PrefabPath);
            if (prefabRoot == null)
            {
                failure = $"Failed to load prefab contents: {spec.PrefabPath}";
                return false;
            }

            try
            {
                Transform parent = ResolveParent(prefabRoot.transform, spec.ParentPath);
                Transform existing = parent.Find(spec.ChildName);
                GameObject panel = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
                panel.name = spec.ChildName;
                panel.transform.SetParent(parent, false);
                panel.transform.localPosition = spec.LocalPosition;
                panel.transform.localRotation = Quaternion.Euler(spec.LocalEulerAngles);
                panel.transform.localScale = spec.LocalScale;

                if (panel.TryGetComponent(out Collider collider))
                    UnityEngine.Object.DestroyImmediate(collider);

                if (panel.TryGetComponent(out Renderer renderer))
                {
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                    renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                    EditorUtility.SetDirty(renderer);
                }

                EditorUtility.SetDirty(panel);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, spec.PrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static Transform ResolveParent(Transform root, string parentPath)
        {
            if (root == null || string.IsNullOrWhiteSpace(parentPath))
                return root;

            Transform parent = root.Find(parentPath);
            return parent != null ? parent : root;
        }

        private static Material CreateOrUpdateMaterial(ExternalPbrAsset asset, Assignment assignment, out string failure)
        {
            failure = string.Empty;
            if (asset.maps == null)
            {
                failure = $"Missing map payload for {asset.id}";
                return null;
            }

            Texture2D baseColor = LoadTexture(asset.maps.BaseColor);
            Texture2D normal = LoadTexture(asset.maps.NormalGL);
            Texture2D maskMap = LoadTexture(asset.maps.MaskMap_UnityURP);
            Texture2D height = LoadTexture(asset.maps.Height);
            if (baseColor == null || normal == null || maskMap == null)
            {
                failure = $"Missing required maps for {asset.id}";
                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(assignment.MaterialPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, assignment.MaterialPath);
            }

            if (shader != null)
                material.shader = shader;

            material.enableInstancing = true;
            material.doubleSidedGI = false;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            SetTextureIfPresent(material, "_BaseMap", baseColor);
            SetTextureIfPresent(material, "_MainTex", baseColor);
            SetTextureIfPresent(material, "_BumpMap", normal);
            SetTextureIfPresent(material, "_MetallicGlossMap", maskMap);
            SetTextureIfPresent(material, "_OcclusionMap", maskMap);
            SetTextureIfPresent(material, "_ParallaxMap", height);
            float tilingScale = Mathf.Clamp(asset.tilingScale * assignment.TilingMultiplier, 0.25f, 16f);
            SetTextureScaleIfPresent(material, "_BaseMap", tilingScale);
            SetTextureScaleIfPresent(material, "_MainTex", tilingScale);
            SetTextureScaleIfPresent(material, "_BumpMap", tilingScale);
            SetTextureScaleIfPresent(material, "_MetallicGlossMap", tilingScale);
            SetTextureScaleIfPresent(material, "_OcclusionMap", tilingScale);
            SetTextureScaleIfPresent(material, "_ParallaxMap", tilingScale);
            SetFloatIfPresent(material, "_BumpScale", assignment.NormalScale);
            SetFloatIfPresent(material, "_Metallic", assignment.Metallic);
            SetFloatIfPresent(material, "_Smoothness", assignment.Smoothness);
            SetFloatIfPresent(material, "_Parallax", assignment.HeightScale);
            SetFloatIfPresent(material, "_OcclusionStrength", 1f);
            SetFloatIfPresent(material, "_SmoothnessTextureChannel", 0f);
            SetKeyword(material, "_NORMALMAP", normal != null);
            SetKeyword(material, "_METALLICSPECGLOSSMAP", maskMap != null);
            SetKeyword(material, "_OCCLUSIONMAP", maskMap != null);
            SetKeyword(material, "_PARALLAXMAP", height != null);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Dictionary<string, ExternalPbrAsset> LoadAllManifestAssets()
        {
            Dictionary<string, ExternalPbrAsset> assets = new Dictionary<string, ExternalPbrAsset>(StringComparer.Ordinal);
            string resolvedAtlasRoot = ResolveProjectFilePath(GeminiAtlasRoot);
            if (!Directory.Exists(resolvedAtlasRoot))
                throw new InvalidOperationException($"[ConstructionInsulationBackingIntegrator] Missing Gemini atlas root: {GeminiAtlasRoot}");

            string[] manifests = Directory.GetFiles(resolvedAtlasRoot, "GeminiMaterialAtlas_Manifest.json", SearchOption.AllDirectories);
            Array.Sort(manifests, StringComparer.Ordinal);
            for (int i = 0; i < manifests.Length; i++)
                MergeManifestAssets(assets, manifests[i]);

            return assets;
        }

        private static void MergeManifestAssets(Dictionary<string, ExternalPbrAsset> assets, string manifestPath)
        {
            string resolvedManifestPath = ResolveProjectFilePath(manifestPath);
            if (!File.Exists(resolvedManifestPath))
                throw new InvalidOperationException($"[ConstructionInsulationBackingIntegrator] Missing manifest: {manifestPath}");

            ExternalPbrManifest manifest = JsonUtility.FromJson<ExternalPbrManifest>(File.ReadAllText(resolvedManifestPath));
            if (manifest == null || manifest.assets == null)
                throw new InvalidOperationException($"[ConstructionInsulationBackingIntegrator] Invalid manifest payload: {manifestPath}");

            for (int i = 0; i < manifest.assets.Length; i++)
            {
                ExternalPbrAsset asset = manifest.assets[i];
                if (asset == null || string.IsNullOrWhiteSpace(asset.id))
                    throw new InvalidOperationException($"[ConstructionInsulationBackingIntegrator] Invalid material asset entry in {manifestPath} at index {i}");

                if (assets.ContainsKey(asset.id))
                    throw new InvalidOperationException($"[ConstructionInsulationBackingIntegrator] Duplicate Gemini material id: {asset.id}");

                assets.Add(asset.id, asset);
            }
        }

        private static void ValidateRoute(Dictionary<string, ExternalPbrAsset> assets)
        {
            int failures = 0;
            string firstFailure = string.Empty;
            if (!assets.TryGetValue(BackingAssignment.MaterialId, out ExternalPbrAsset asset))
            {
                RecordFailure(ref failures, ref firstFailure, $"Missing Gemini material id={BackingAssignment.MaterialId}");
            }
            else if (asset.maps == null)
            {
                RecordFailure(ref failures, ref firstFailure, $"Missing map payload for {asset.id}");
            }
            else if (LoadTexture(asset.maps.BaseColor) == null || LoadTexture(asset.maps.NormalGL) == null || LoadTexture(asset.maps.MaskMap_UnityURP) == null)
            {
                RecordFailure(ref failures, ref firstFailure, $"Missing required maps for {asset.id}");
            }

            for (int i = 0; i < Panels.Length; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Panels[i].PrefabPath) == null)
                    RecordFailure(ref failures, ref firstFailure, $"Missing prefab: {Panels[i].PrefabPath}");
            }

            if (failures > 0)
                throw new InvalidOperationException($"[ConstructionInsulationBackingIntegrator] Invalid insulation backing route. failures={failures}, first={firstFailure}");
        }

        private static void RecordFailure(ref int failures, ref string firstFailure, string failure)
        {
            failures++;
            if (string.IsNullOrEmpty(firstFailure))
                firstFailure = failure;
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

        private static void SetTextureScaleIfPresent(Material material, string property, float scale)
        {
            if (material.HasProperty(property))
                material.SetTextureScale(property, new Vector2(scale, scale));
        }

        private static void SetFloatIfPresent(Material material, string property, float value)
        {
            if (material.HasProperty(property))
                material.SetFloat(property, value);
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }

        private sealed class Assignment
        {
            public readonly string MaterialPath;
            public readonly string MaterialId;
            public readonly float TilingMultiplier;
            public readonly float NormalScale;
            public readonly float Metallic;
            public readonly float Smoothness;
            public readonly float HeightScale;
            public readonly string Reason;

            public Assignment(
                string materialPath,
                string materialId,
                float tilingMultiplier,
                float normalScale,
                float metallic,
                float smoothness,
                float heightScale,
                string reason)
            {
                MaterialPath = materialPath;
                MaterialId = materialId;
                TilingMultiplier = tilingMultiplier;
                NormalScale = normalScale;
                Metallic = metallic;
                Smoothness = smoothness;
                HeightScale = heightScale;
                Reason = reason;
            }
        }

        private readonly struct BackingPanelSpec
        {
            public readonly string PrefabPath;
            public readonly string ParentPath;
            public readonly string ChildName;
            public readonly Vector3 LocalPosition;
            public readonly Vector3 LocalEulerAngles;
            public readonly Vector3 LocalScale;

            public BackingPanelSpec(
                string prefabPath,
                string parentPath,
                string childName,
                Vector3 localPosition,
                Vector3 localEulerAngles,
                Vector3 localScale)
            {
                PrefabPath = prefabPath;
                ParentPath = parentPath;
                ChildName = childName;
                LocalPosition = localPosition;
                LocalEulerAngles = localEulerAngles;
                LocalScale = localScale;
            }
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
            public float tilingScale;
            public ExternalPbrMaps maps;
        }

        [Serializable]
        private sealed class ExternalPbrMaps
        {
            public string BaseColor;
            public string NormalGL;
            public string MaskMap_UnityURP;
            public string Height;
        }
    }
}
