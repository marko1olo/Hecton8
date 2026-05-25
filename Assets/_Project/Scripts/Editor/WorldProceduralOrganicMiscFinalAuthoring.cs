using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Authors real procedural finals for organic families that are outside the baked kelp/coral pipeline.
    /// </summary>
    public static class WorldProceduralOrganicMiscFinalAuthoring
    {
        private const string MaterialFolder = "Assets/_Project/Art/Materials/Nature/ProceduralOrganicMisc";
        private const string FinalPrefabFolder = "Assets/_Project/Prefabs/Nature/OrganicMisc/Final";
        private const string UrpLitShaderName = "Universal Render Pipeline/Lit";
        private const string StandardShaderName = "Standard";
        private const float Lod0Threshold = 0.6f;
        private const float Lod1Threshold = 0.15f;
        private const float Lod2Threshold = 0.04f;

        [MenuItem("Hecton/Authoring/Rebuild Procedural Organic Misc Finals", priority = 180)]
        public static void RebuildOrganicMiscFinals()
        {
            EnsureFolder("Assets/_Project/Art");
            EnsureFolder("Assets/_Project/Art/Materials");
            EnsureFolder("Assets/_Project/Art/Materials/Nature");
            EnsureFolder(MaterialFolder);
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder("Assets/_Project/Prefabs/Nature");
            EnsureFolder("Assets/_Project/Prefabs/Nature/OrganicMisc");
            EnsureFolder(FinalPrefabFolder);

            Material eggShellMat = CreateOrUpdateMaterial($"{MaterialFolder}/Mat_Organic_EggShell.mat", new Color(0.90f, 0.84f, 0.70f, 1f), 0.18f);
            Material eggNestMat = CreateOrUpdateMaterial($"{MaterialFolder}/Mat_Organic_EggNest.mat", new Color(0.36f, 0.30f, 0.24f, 1f), 0.10f);
            Material plantStemMat = CreateOrUpdateMaterial($"{MaterialFolder}/Mat_Organic_PlantStem.mat", new Color(0.12f, 0.42f, 0.28f, 1f), 0.18f);
            Material plantCanopyMat = CreateOrUpdateMaterial($"{MaterialFolder}/Mat_Organic_PlantCanopy.mat", new Color(0.22f, 0.66f, 0.42f, 1f), 0.26f);
            Material plantBudMat = CreateOrUpdateMaterial($"{MaterialFolder}/Mat_Organic_PlantBud.mat", new Color(0.44f, 0.80f, 0.60f, 1f), 0.32f);

            int createdCount = 0;
            if (CreateCompositeFinalPrefab($"{FinalPrefabFolder}/PFB_Organic_EggCluster.prefab", new Vector3(3.0f, 1.6f, 3.0f), BuildEggClusterLods(eggShellMat, eggNestMat)) != null)
                createdCount++;

            if (CreateCompositeFinalPrefab($"{FinalPrefabFolder}/PFB_Organic_PlantGiant.prefab", new Vector3(8.4f, 15.0f, 8.4f), BuildPlantGiantLods(plantStemMat, plantCanopyMat, plantBudMat)) != null)
                createdCount++;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[WorldProceduralOrganicMiscFinalAuthoring] Rebuilt organic misc final prefabs. Created={createdCount}.");
        }

        private static CompositeLodSpec[] BuildEggClusterLods(Material eggShellMat, Material eggNestMat)
        {
            return new[]
            {
                new CompositeLodSpec(
                    Lod0Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("NestBase", PrimitiveType.Cylinder, new Vector3(1.4f, 0.22f, 1.4f), new Vector3(0f, 0.11f, 0f), Vector3.zero, eggNestMat),
                        new VisualPrimitiveSpec("EggA", PrimitiveType.Sphere, new Vector3(0.52f, 0.72f, 0.52f), new Vector3(-0.46f, 0.36f, 0.24f), new Vector3(-10f, 0f, 12f), eggShellMat),
                        new VisualPrimitiveSpec("EggB", PrimitiveType.Sphere, new Vector3(0.56f, 0.78f, 0.56f), new Vector3(0.18f, 0.38f, -0.34f), new Vector3(12f, 0f, -8f), eggShellMat),
                        new VisualPrimitiveSpec("EggC", PrimitiveType.Sphere, new Vector3(0.48f, 0.66f, 0.48f), new Vector3(0.52f, 0.32f, 0.22f), new Vector3(6f, 0f, -14f), eggShellMat),
                        new VisualPrimitiveSpec("EggD", PrimitiveType.Sphere, new Vector3(0.42f, 0.56f, 0.42f), new Vector3(-0.14f, 0.28f, 0.52f), new Vector3(-8f, 0f, 10f), eggShellMat),
                        new VisualPrimitiveSpec("NestRidgeA", PrimitiveType.Cylinder, new Vector3(0.16f, 0.8f, 0.16f), new Vector3(-0.88f, 0.18f, -0.42f), new Vector3(90f, 18f, 0f), eggNestMat),
                        new VisualPrimitiveSpec("NestRidgeB", PrimitiveType.Cylinder, new Vector3(0.14f, 0.7f, 0.14f), new Vector3(0.86f, 0.16f, 0.36f), new Vector3(90f, -12f, 0f), eggNestMat),
                    }),
                new CompositeLodSpec(
                    Lod1Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("NestBase", PrimitiveType.Cylinder, new Vector3(1.3f, 0.18f, 1.3f), new Vector3(0f, 0.09f, 0f), Vector3.zero, eggNestMat),
                        new VisualPrimitiveSpec("EggMassA", PrimitiveType.Sphere, new Vector3(0.72f, 0.86f, 0.72f), new Vector3(-0.26f, 0.42f, 0.12f), Vector3.zero, eggShellMat),
                        new VisualPrimitiveSpec("EggMassB", PrimitiveType.Sphere, new Vector3(0.66f, 0.78f, 0.66f), new Vector3(0.34f, 0.38f, -0.18f), Vector3.zero, eggShellMat),
                    }),
                new CompositeLodSpec(
                    Lod2Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("EggSilhouette", PrimitiveType.Sphere, new Vector3(1.16f, 0.92f, 1.16f), new Vector3(0f, 0.44f, 0f), Vector3.zero, eggShellMat),
                    }),
            };
        }

        private static CompositeLodSpec[] BuildPlantGiantLods(Material stemMat, Material canopyMat, Material budMat)
        {
            return new[]
            {
                new CompositeLodSpec(
                    Lod0Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("StemCore", PrimitiveType.Cylinder, new Vector3(1.4f, 10.8f, 1.4f), new Vector3(0f, 5.4f, 0f), Vector3.zero, stemMat),
                        new VisualPrimitiveSpec("StemBulb", PrimitiveType.Sphere, new Vector3(2.2f, 2.6f, 2.2f), new Vector3(0f, 1.2f, 0f), Vector3.zero, stemMat),
                        new VisualPrimitiveSpec("CanopyA", PrimitiveType.Capsule, new Vector3(1.6f, 6.8f, 1.6f), new Vector3(-2.4f, 9.4f, 0.8f), new Vector3(18f, 0f, 42f), canopyMat),
                        new VisualPrimitiveSpec("CanopyB", PrimitiveType.Capsule, new Vector3(1.5f, 6.2f, 1.5f), new Vector3(2.6f, 8.8f, -0.6f), new Vector3(-18f, 0f, -40f), canopyMat),
                        new VisualPrimitiveSpec("CanopyC", PrimitiveType.Capsule, new Vector3(1.4f, 5.8f, 1.4f), new Vector3(0.6f, 10.2f, 2.4f), new Vector3(22f, 0f, 8f), canopyMat),
                        new VisualPrimitiveSpec("CanopyD", PrimitiveType.Capsule, new Vector3(1.3f, 5.2f, 1.3f), new Vector3(-0.8f, 8.6f, -2.2f), new Vector3(-20f, 0f, -12f), canopyMat),
                        new VisualPrimitiveSpec("BudA", PrimitiveType.Sphere, new Vector3(1.6f, 1.8f, 1.6f), new Vector3(-3.6f, 11.8f, 1.4f), Vector3.zero, budMat),
                        new VisualPrimitiveSpec("BudB", PrimitiveType.Sphere, new Vector3(1.4f, 1.6f, 1.4f), new Vector3(3.4f, 11.0f, -1.2f), Vector3.zero, budMat),
                    }),
                new CompositeLodSpec(
                    Lod1Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("Stem", PrimitiveType.Cylinder, new Vector3(1.5f, 9.6f, 1.5f), new Vector3(0f, 4.8f, 0f), Vector3.zero, stemMat),
                        new VisualPrimitiveSpec("CanopyMass", PrimitiveType.Capsule, new Vector3(2.8f, 7.6f, 2.8f), new Vector3(0f, 9.4f, 0f), new Vector3(0f, 0f, 90f), canopyMat),
                        new VisualPrimitiveSpec("Bud", PrimitiveType.Sphere, new Vector3(2.0f, 2.0f, 2.0f), new Vector3(0f, 12.2f, 0f), Vector3.zero, budMat),
                    }),
                new CompositeLodSpec(
                    Lod2Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("PlantSilhouette", PrimitiveType.Capsule, new Vector3(3.4f, 12.4f, 3.4f), new Vector3(0f, 6.2f, 0f), Vector3.zero, canopyMat),
                    }),
            };
        }

        private static GameObject CreateCompositeFinalPrefab(string prefabPath, Vector3 colliderSize, CompositeLodSpec[] lodSpecs)
        {
            if (lodSpecs == null || lodSpecs.Length <= 0)
                return null;

            GameObject root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                BoxCollider collider = root.AddComponent<BoxCollider>();
                collider.size = colliderSize;
                collider.center = new Vector3(0f, colliderSize.y * 0.5f, 0f);

                BuildCompositeVisuals(root.transform, lodSpecs);
                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return savedPrefab != null ? savedPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildCompositeVisuals(Transform parent, CompositeLodSpec[] lodSpecs)
        {
            LOD[] lods = new LOD[lodSpecs.Length];
            for (int i = 0; i < lodSpecs.Length; i++)
            {
                GameObject lodRoot = new GameObject($"LOD{i}");
                lodRoot.transform.SetParent(parent, false);

                VisualPrimitiveSpec[] visuals = lodSpecs[i].Visuals;
                List<Renderer> renderers = new List<Renderer>(visuals != null ? visuals.Length : 0);
                BuildCompositeVisualGroup(lodRoot.transform, visuals, renderers);
                lods[i] = new LOD(lodSpecs[i].ScreenRelativeTransitionHeight, renderers.ToArray());
            }

            LODGroup lodGroup = parent.gameObject.AddComponent<LODGroup>();
            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = true;
            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();
        }

        private static void BuildCompositeVisualGroup(Transform parent, VisualPrimitiveSpec[] visuals, List<Renderer> renderers)
        {
            if (visuals == null || visuals.Length <= 0)
                return;

            for (int i = 0; i < visuals.Length; i++)
            {
                Renderer renderer = CreateVisualPrimitive(
                    parent,
                    visuals[i].Name,
                    visuals[i].PrimitiveType,
                    visuals[i].Scale,
                    visuals[i].Material,
                    visuals[i].LocalPosition,
                    visuals[i].LocalEulerAngles);

                if (renderer != null)
                    renderers.Add(renderer);
            }
        }

        private static Renderer CreateVisualPrimitive(
            Transform parent,
            string name,
            PrimitiveType primitiveType,
            Vector3 scale,
            Material material,
            Vector3 localPosition,
            Vector3 localEulerAngles)
        {
            GameObject visual = GameObject.CreatePrimitive(primitiveType);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localRotation = Quaternion.Euler(localEulerAngles);
            visual.transform.localScale = scale;

            if (visual.TryGetComponent(out Collider visualCollider))
                Object.DestroyImmediate(visualCollider);

            if (visual.TryGetComponent(out Renderer renderer))
                renderer.sharedMaterial = material;

            return renderer;
        }

        private static Material CreateOrUpdateMaterial(string path, Color baseColor, float smoothness)
        {
            Shader shader = Shader.Find(UrpLitShaderName);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader != null ? shader : Shader.Find(StandardShaderName));
                AssetDatabase.CreateAsset(material, path);
            }

            material.name = System.IO.Path.GetFileNameWithoutExtension(path);
            material.shader = shader != null ? shader : material.shader;
            material.enableInstancing = true;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", baseColor);

            if (material.HasProperty("_Color"))
                material.SetColor("_Color", baseColor);

            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);

            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", null);

            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 0f);

            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 1f);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int separatorIndex = path.LastIndexOf('/');
            string parentFolder = separatorIndex > 0 ? path.Substring(0, separatorIndex) : string.Empty;
            string newFolderName = separatorIndex > 0 ? path.Substring(separatorIndex + 1) : path;
            if (!string.IsNullOrEmpty(parentFolder) && !AssetDatabase.IsValidFolder(parentFolder))
                EnsureFolder(parentFolder);

            AssetDatabase.CreateFolder(parentFolder, newFolderName);
        }

        private readonly struct CompositeLodSpec
        {
            public CompositeLodSpec(float screenRelativeTransitionHeight, VisualPrimitiveSpec[] visuals)
            {
                ScreenRelativeTransitionHeight = screenRelativeTransitionHeight;
                Visuals = visuals;
            }

            public float ScreenRelativeTransitionHeight { get; }
            public VisualPrimitiveSpec[] Visuals { get; }
        }

        private readonly struct VisualPrimitiveSpec
        {
            public VisualPrimitiveSpec(
                string name,
                PrimitiveType primitiveType,
                Vector3 scale,
                Vector3 localPosition,
                Vector3 localEulerAngles,
                Material material)
            {
                Name = name;
                PrimitiveType = primitiveType;
                Scale = scale;
                LocalPosition = localPosition;
                LocalEulerAngles = localEulerAngles;
                Material = material;
            }

            public string Name { get; }
            public PrimitiveType PrimitiveType { get; }
            public Vector3 Scale { get; }
            public Vector3 LocalPosition { get; }
            public Vector3 LocalEulerAngles { get; }
            public Material Material { get; }
        }
    }
}
