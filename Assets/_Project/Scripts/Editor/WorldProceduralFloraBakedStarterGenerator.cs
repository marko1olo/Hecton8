using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Generates editor-only baked flora starter prefabs as optimized combined-mesh assets.
    /// These are separate from proxy prefabs and exist only to seed the baked flora final pipeline.
    /// </summary>
    public static class WorldProceduralFloraBakedStarterGenerator
    {
        private const string GeneratedPrefix = "GEN_";
        private const string MaterialFolder = "Assets/_Project/Art/Materials/WorldProceduralProxy";

        [MenuItem("Hecton/Authoring/Generate Procedural Flora Baked Starters", priority = 178)]
        public static void Generate()
        {
            EnsureFolder("Assets/_Project/Prefabs/Nature");
            EnsureFolder("Assets/_Project/Prefabs/Nature/Flora");
            EnsureFolder(WorldProceduralFloraFinalVariantAuthoring.FloraFinalRootFolder);

            StarterSpec[] specs = BuildSpecs();
            HashSet<string> expectedAssetPaths = new HashSet<string>(specs.Length * 4, StringComparer.Ordinal);
            int generatedPrefabs = 0;
            int updatedMeshes = 0;
            int removedAssets = 0;
            int failures = 0;

            for (int i = 0; i < specs.Length; i++)
            {
                StarterSpec spec = specs[i];
                EnsureFolder(spec.FamilyFolderPath);
                expectedAssetPaths.Add(spec.PrefabPath);
                expectedAssetPaths.Add(spec.Lod0MeshPath);
                expectedAssetPaths.Add(spec.Lod1MeshPath);
                if (spec.HasExtendedLods)
                {
                    expectedAssetPaths.Add(spec.Lod2MeshPath);
                    expectedAssetPaths.Add(spec.Lod3MeshPath);
                }

                if (!TryGenerateStarter(spec, ref generatedPrefabs, ref updatedMeshes))
                    failures++;
            }

            removedAssets = RemoveStaleGeneratedAssets(expectedAssetPaths);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[WorldProceduralFloraBakedStarterGenerator] Baked flora starters generated. Prefabs={generatedPrefabs}, MeshesUpdated={updatedMeshes}, RemovedAssets={removedAssets}, Failures={failures}.");
        }

        private static bool TryGenerateStarter(StarterSpec spec, ref int generatedPrefabs, ref int updatedMeshes)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(spec.MaterialPath);
            if (material == null)
            {
                Debug.LogWarning($"[WorldProceduralFloraBakedStarterGenerator] Missing material '{spec.MaterialPath}' for '{spec.PrefabPath}'.");
                return false;
            }

            GameObject sourceRoot = null;
            GameObject bakedRoot = null;
            Mesh generatedLod0Mesh = null;
            Mesh generatedLod1Mesh = null;
            Mesh generatedLod2Mesh = null;
            Mesh generatedLod3Mesh = null;

            try
            {
                if (WorldProceduralSeaweedMeshBuilder.CanBuild(spec.RootToken))
                {
                    if (!WorldProceduralSeaweedMeshBuilder.TryBuild(spec.RootToken, spec.ShapeScale, lodLevel: 0, out generatedLod0Mesh)
                        || !WorldProceduralSeaweedMeshBuilder.TryBuild(spec.RootToken, spec.ShapeScale, lodLevel: 1, out generatedLod1Mesh)
                        || !WorldProceduralSeaweedMeshBuilder.TryBuild(spec.RootToken, spec.ShapeScale, lodLevel: 2, out generatedLod2Mesh)
                        || !WorldProceduralSeaweedMeshBuilder.TryBuild(spec.RootToken, spec.ShapeScale, lodLevel: 3, out generatedLod3Mesh))
                    {
                        Debug.LogWarning($"[WorldProceduralFloraBakedStarterGenerator] Could not generate kelp mesh starter for '{spec.RootToken}'.");
                        return false;
                    }
                }
                else
                {
                    if (!WorldProceduralFloraProxyShapeBuilder.TryBuild(spec.RootToken, spec.ShapeScale, material, out sourceRoot) || sourceRoot == null)
                    {
                        Debug.LogWarning($"[WorldProceduralFloraBakedStarterGenerator] Could not build flora starter source for '{spec.RootToken}'.");
                        return false;
                    }

                    generatedLod0Mesh = BuildCombinedMesh(sourceRoot, spec.Lod0MeshAssetName, includeReduced:false);
                    generatedLod1Mesh = BuildCombinedMesh(sourceRoot, spec.Lod1MeshAssetName, includeReduced:true);
                }

                if (generatedLod0Mesh == null || generatedLod1Mesh == null)
                {
                    Debug.LogWarning($"[WorldProceduralFloraBakedStarterGenerator] Could not combine mesh for '{spec.RootToken}'.");
                    return false;
                }

                generatedLod0Mesh.name = spec.Lod0MeshAssetName;
                generatedLod1Mesh.name = spec.Lod1MeshAssetName;
                if (generatedLod2Mesh != null)
                    generatedLod2Mesh.name = spec.Lod2MeshAssetName;

                if (generatedLod3Mesh != null)
                    generatedLod3Mesh.name = spec.Lod3MeshAssetName;

                Mesh bakedLod0Mesh = CreateOrUpdateMeshAsset(spec.Lod0MeshPath, generatedLod0Mesh);
                Mesh bakedLod1Mesh = CreateOrUpdateMeshAsset(spec.Lod1MeshPath, generatedLod1Mesh);
                updatedMeshes += 2;
                Mesh bakedLod2Mesh = null;
                Mesh bakedLod3Mesh = null;
                if (generatedLod2Mesh != null && !string.IsNullOrWhiteSpace(spec.Lod2MeshPath))
                {
                    bakedLod2Mesh = CreateOrUpdateMeshAsset(spec.Lod2MeshPath, generatedLod2Mesh);
                    updatedMeshes++;
                }

                if (generatedLod3Mesh != null && !string.IsNullOrWhiteSpace(spec.Lod3MeshPath))
                {
                    bakedLod3Mesh = CreateOrUpdateMeshAsset(spec.Lod3MeshPath, generatedLod3Mesh);
                    updatedMeshes++;
                }

                bakedRoot = new GameObject(spec.PrefabName);
                LODGroup lodGroup = bakedRoot.AddComponent<LODGroup>();
                lodGroup.animateCrossFading = false;
                lodGroup.fadeMode = LODFadeMode.None;

                Renderer lod0Renderer = CreateLodRenderer(bakedRoot.transform, "__LOD0", bakedLod0Mesh, material);
                Renderer lod1Renderer = CreateLodRenderer(bakedRoot.transform, "__LOD1", bakedLod1Mesh, material);
                if (bakedLod2Mesh != null && bakedLod3Mesh != null)
                {
                    Renderer lod2Renderer = CreateLodRenderer(bakedRoot.transform, "__LOD2", bakedLod2Mesh, material);
                    Renderer lod3Renderer = CreateLodRenderer(bakedRoot.transform, "__LOD3", bakedLod3Mesh, material);
                    lodGroup.SetLODs(new[]
                    {
                        new LOD(0.62f, new[] { lod0Renderer }),
                        new LOD(0.32f, new[] { lod1Renderer }),
                        new LOD(0.14f, new[] { lod2Renderer }),
                        new LOD(0.05f, new[] { lod3Renderer })
                    });
                }
                else
                {
                    lodGroup.SetLODs(new[]
                    {
                        new LOD(0.52f, new[] { lod0Renderer }),
                        new LOD(0.22f, new[] { lod1Renderer })
                    });
                }

                lodGroup.RecalculateBounds();

                PrefabUtility.SaveAsPrefabAsset(bakedRoot, spec.PrefabPath);
                generatedPrefabs++;
                return true;
            }
            finally
            {
                if (generatedLod0Mesh != null)
                    UnityEngine.Object.DestroyImmediate(generatedLod0Mesh);

                if (generatedLod1Mesh != null)
                    UnityEngine.Object.DestroyImmediate(generatedLod1Mesh);

                if (generatedLod2Mesh != null)
                    UnityEngine.Object.DestroyImmediate(generatedLod2Mesh);

                if (generatedLod3Mesh != null)
                    UnityEngine.Object.DestroyImmediate(generatedLod3Mesh);

                if (bakedRoot != null)
                    UnityEngine.Object.DestroyImmediate(bakedRoot);

                if (sourceRoot != null)
                    UnityEngine.Object.DestroyImmediate(sourceRoot);
            }
        }

        private static Renderer CreateLodRenderer(Transform parent, string name, Mesh mesh, Material material)
        {
            GameObject lodRoot = new GameObject(name);
            lodRoot.transform.SetParent(parent, false);

            MeshFilter meshFilter = lodRoot.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            MeshRenderer meshRenderer = lodRoot.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.allowOcclusionWhenDynamic = false;
            meshRenderer.sharedMaterial = material;
            return meshRenderer;
        }

        private static Mesh BuildCombinedMesh(GameObject sourceRoot, string meshName, bool includeReduced)
        {
            List<SourceMeshPart> parts = CollectSourceMeshParts(sourceRoot);
            if (parts.Count == 0)
                return null;

            if (includeReduced)
                ReducePartsForLod(parts);

            List<CombineInstance> combineInstances = new List<CombineInstance>(parts.Count);

            for (int i = 0; i < parts.Count; i++)
            {
                SourceMeshPart part = parts[i];
                CombineInstance combineInstance = new CombineInstance
                {
                    mesh = part.Mesh,
                    transform = part.LocalToRoot,
                    subMeshIndex = 0
                };
                combineInstances.Add(combineInstance);
            }

            if (combineInstances.Count == 0)
                return null;

            Mesh combinedMesh = new Mesh
            {
                name = meshName,
                indexFormat = IndexFormat.UInt32
            };
            combinedMesh.CombineMeshes(combineInstances.ToArray(), true, true, false);
            combinedMesh.RecalculateBounds();
            return combinedMesh;
        }

        private static List<SourceMeshPart> CollectSourceMeshParts(GameObject sourceRoot)
        {
            MeshFilter[] meshFilters = sourceRoot.GetComponentsInChildren<MeshFilter>(true);
            List<SourceMeshPart> parts = new List<SourceMeshPart>(meshFilters.Length);
            Matrix4x4 sourceRootWorldToLocal = sourceRoot.transform.worldToLocalMatrix;

            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                    continue;

                Vector3 localScale = meshFilter.transform.localScale;
                Vector3 boundsSize = meshFilter.sharedMesh.bounds.size;
                float importance = Mathf.Abs(localScale.x * boundsSize.x)
                    * Mathf.Abs(localScale.y * boundsSize.y)
                    * Mathf.Abs(localScale.z * boundsSize.z);

                parts.Add(new SourceMeshPart(
                    meshFilter.sharedMesh,
                    sourceRootWorldToLocal * meshFilter.transform.localToWorldMatrix,
                    importance));
            }

            return parts;
        }

        private static void ReducePartsForLod(List<SourceMeshPart> parts)
        {
            if (parts.Count <= 1)
                return;

            parts.Sort(static (left, right) => right.Importance.CompareTo(left.Importance));
            int keepCount = Mathf.Clamp((parts.Count + 1) / 2, 1, 3);
            if (parts.Count > keepCount)
                parts.RemoveRange(keepCount, parts.Count - keepCount);
        }

        private static Mesh CreateOrUpdateMeshAsset(string meshPath, Mesh sourceMesh)
        {
            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existingMesh == null)
            {
                Mesh createdMesh = UnityEngine.Object.Instantiate(sourceMesh);
                createdMesh.name = sourceMesh.name;
                AssetDatabase.CreateAsset(createdMesh, meshPath);
                return createdMesh;
            }

            EditorUtility.CopySerialized(sourceMesh, existingMesh);
            existingMesh.name = sourceMesh.name;
            EditorUtility.SetDirty(existingMesh);
            return existingMesh;
        }

        private static int RemoveStaleGeneratedAssets(HashSet<string> expectedAssetPaths)
        {
            string[] assetGuids = AssetDatabase.FindAssets(GeneratedPrefix, new[] { WorldProceduralFloraFinalVariantAuthoring.FloraFinalRootFolder });
            int removedAssets = 0;

            for (int i = 0; i < assetGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[i]);
                if (string.IsNullOrWhiteSpace(assetPath))
                    continue;

                string fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                if (string.IsNullOrWhiteSpace(fileName) || !fileName.StartsWith(GeneratedPrefix, StringComparison.Ordinal))
                    continue;

                if (expectedAssetPaths.Contains(assetPath))
                    continue;

                if (AssetDatabase.DeleteAsset(assetPath))
                    removedAssets++;
            }

            return removedAssets;
        }

        private static StarterSpec[] BuildSpecs()
        {
            return new[]
            {
                CreateSpec("family.kelp.tall", "family_kelp_tall__stalk", new Vector3(0.18f, 3.6f, 0.18f)),
                CreateSpec("family.kelp.tall", "family_kelp_tall__lean", new Vector3(0.16f, 3.1f, 0.16f)),
                CreateSpec("family.kelp.tall", "family_kelp_tall__ribbon", new Vector3(0.14f, 3.9f, 0.14f)),
                CreateSpec("family.kelp.patch.dense", "family_kelp_patch_dense__patch", new Vector3(0.18f, 2.8f, 0.18f)),
                CreateSpec("family.kelp.patch.dense", "family_kelp_patch_dense__patch_tall", new Vector3(0.2f, 3.1f, 0.2f)),
                CreateSpec("family.kelp.patch.dense", "family_kelp_patch_dense__ring", new Vector3(0.17f, 2.7f, 0.17f)),
                CreateSpec("family.kelp.canopy", "family_kelp_canopy__crown", new Vector3(0.22f, 4.2f, 0.22f)),
                CreateSpec("family.kelp.canopy", "family_kelp_canopy__frond", new Vector3(0.18f, 3.8f, 0.18f)),
                CreateSpec("family.kelp.canopy", "family_kelp_canopy__fan", new Vector3(0.2f, 4.05f, 0.2f)),
                CreateSpec("family.coral.low", "family_coral_low__bed", new Vector3(1.22f, 0.8f, 1.12f)),
                CreateSpec("family.coral.low", "family_coral_low__plate", new Vector3(1.06f, 0.74f, 1.02f)),
                CreateSpec("family.coral.low", "family_coral_low__knoll", new Vector3(1.14f, 0.86f, 1.08f)),
                CreateSpec("family.coral.branching", "family_coral_branching__branch", new Vector3(0.92f, 1.28f, 0.92f)),
                CreateSpec("family.coral.branching", "family_coral_branching__mass", new Vector3(1.02f, 1.18f, 1.02f)),
                CreateSpec("family.coral.branching", "family_coral_branching__fan", new Vector3(1f, 1.22f, 0.98f)),
                CreateSpec("family.coral.massive", "family_coral_massive__head", new Vector3(1.26f, 1.02f, 1.18f)),
                CreateSpec("family.coral.massive", "family_coral_massive__porous", new Vector3(1.16f, 0.94f, 1.1f)),
                CreateSpec("family.coral.massive", "family_coral_massive__boulder", new Vector3(1.28f, 1.08f, 1.22f)),
                CreateSpec("family.coral.plate", "family_coral_plate__ledge", new Vector3(1.24f, 0.98f, 1.18f)),
                CreateSpec("family.coral.plate", "family_coral_plate__shelf", new Vector3(1.12f, 0.9f, 1.08f)),
                CreateSpec("family.coral.plate", "family_coral_plate__stack", new Vector3(1.06f, 1.04f, 1f))
            };
        }

        private static StarterSpec CreateSpec(string familyId, string rootToken, Vector3 shapeScale)
        {
            string safeFamilyToken = familyId.Replace('.', '_');
            string prefabName = GeneratedPrefix + rootToken;
            string familyFolderPath = $"{WorldProceduralFloraFinalVariantAuthoring.FloraFinalRootFolder}/{safeFamilyToken}";
            string lod0MeshAssetName = prefabName + "_LOD0_Mesh";
            string lod1MeshAssetName = prefabName + "_LOD1_Mesh";
            bool hasExtendedLods = WorldProceduralSeaweedMeshBuilder.CanBuild(rootToken);
            string lod2MeshAssetName = prefabName + "_LOD2_Mesh";
            string lod3MeshAssetName = prefabName + "_LOD3_Mesh";

            return new StarterSpec(
                rootToken,
                shapeScale,
                $"{MaterialFolder}/MAT_{safeFamilyToken}.mat",
                familyFolderPath,
                $"{familyFolderPath}/{prefabName}.prefab",
                lod0MeshAssetName,
                $"{familyFolderPath}/{lod0MeshAssetName}.asset",
                lod1MeshAssetName,
                $"{familyFolderPath}/{lod1MeshAssetName}.asset",
                hasExtendedLods,
                hasExtendedLods ? lod2MeshAssetName : string.Empty,
                hasExtendedLods ? $"{familyFolderPath}/{lod2MeshAssetName}.asset" : string.Empty,
                hasExtendedLods ? lod3MeshAssetName : string.Empty,
                hasExtendedLods ? $"{familyFolderPath}/{lod3MeshAssetName}.asset" : string.Empty);
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            int lastSeparator = assetPath.LastIndexOf('/');
            if (lastSeparator <= 0)
                return;

            string parentPath = assetPath.Substring(0, lastSeparator);
            string folderName = assetPath.Substring(lastSeparator + 1);
            EnsureFolder(parentPath);

            if (!AssetDatabase.IsValidFolder(assetPath))
                AssetDatabase.CreateFolder(parentPath, folderName);
        }

        private readonly struct StarterSpec
        {
            public StarterSpec(
                string rootToken,
                Vector3 shapeScale,
                string materialPath,
                string familyFolderPath,
                string prefabPath,
                string lod0MeshAssetName,
                string lod0MeshPath,
                string lod1MeshAssetName,
                string lod1MeshPath,
                bool hasExtendedLods,
                string lod2MeshAssetName,
                string lod2MeshPath,
                string lod3MeshAssetName,
                string lod3MeshPath)
            {
                RootToken = rootToken;
                ShapeScale = shapeScale;
                MaterialPath = materialPath;
                FamilyFolderPath = familyFolderPath;
                PrefabPath = prefabPath;
                PrefabName = System.IO.Path.GetFileNameWithoutExtension(prefabPath);
                Lod0MeshAssetName = lod0MeshAssetName;
                Lod0MeshPath = lod0MeshPath;
                Lod1MeshAssetName = lod1MeshAssetName;
                Lod1MeshPath = lod1MeshPath;
                HasExtendedLods = hasExtendedLods;
                Lod2MeshAssetName = lod2MeshAssetName;
                Lod2MeshPath = lod2MeshPath;
                Lod3MeshAssetName = lod3MeshAssetName;
                Lod3MeshPath = lod3MeshPath;
            }

            public string RootToken { get; }
            public Vector3 ShapeScale { get; }
            public string MaterialPath { get; }
            public string FamilyFolderPath { get; }
            public string PrefabPath { get; }
            public string PrefabName { get; }
            public string Lod0MeshAssetName { get; }
            public string Lod0MeshPath { get; }
            public string Lod1MeshAssetName { get; }
            public string Lod1MeshPath { get; }
            public bool HasExtendedLods { get; }
            public string Lod2MeshAssetName { get; }
            public string Lod2MeshPath { get; }
            public string Lod3MeshAssetName { get; }
            public string Lod3MeshPath { get; }
        }

        private readonly struct SourceMeshPart
        {
            public SourceMeshPart(Mesh mesh, Matrix4x4 localToRoot, float importance)
            {
                Mesh = mesh;
                LocalToRoot = localToRoot;
                Importance = importance;
            }

            public Mesh Mesh { get; }
            public Matrix4x4 LocalToRoot { get; }
            public float Importance { get; }
        }
    }
}
