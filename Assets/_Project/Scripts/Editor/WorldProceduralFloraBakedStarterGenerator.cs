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
        private const float Lod0Threshold = 0.6f;
        private const float Lod1Threshold = 0.15f;
        private const float Lod2Threshold = 0.04f;

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
                expectedAssetPaths.Add(spec.Lod2MeshPath);

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

            try
            {
                if (WorldProceduralSeaweedMeshBuilder.CanBuild(spec.RootToken))
                {
                    if (!WorldProceduralSeaweedMeshBuilder.TryBuild(spec.RootToken, spec.ShapeScale, lodLevel: 0, out generatedLod0Mesh)
                        || !WorldProceduralSeaweedMeshBuilder.TryBuild(spec.RootToken, spec.ShapeScale, lodLevel: 1, out generatedLod1Mesh)
                        || !WorldProceduralSeaweedMeshBuilder.TryBuild(spec.RootToken, spec.ShapeScale, lodLevel: 2, out generatedLod2Mesh))
                    {
                        Debug.LogWarning($"[WorldProceduralFloraBakedStarterGenerator] Could not generate kelp mesh starter for '{spec.RootToken}'.");
                        return false;
                    }
                }
                else if (WorldProceduralCoralMeshBuilder.CanBuild(spec.RootToken))
                {
                    if (!WorldProceduralCoralMeshBuilder.TryBuild(spec.RootToken, spec.ShapeScale, lodLevel: 0, out generatedLod0Mesh)
                        || !WorldProceduralCoralMeshBuilder.TryBuild(spec.RootToken, spec.ShapeScale, lodLevel: 1, out generatedLod1Mesh)
                        || !WorldProceduralCoralMeshBuilder.TryBuild(spec.RootToken, spec.ShapeScale, lodLevel: 2, out generatedLod2Mesh))
                    {
                        Debug.LogWarning($"[WorldProceduralFloraBakedStarterGenerator] Could not generate coral mesh starter for '{spec.RootToken}'.");
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

                if (generatedLod0Mesh == null || generatedLod1Mesh == null || generatedLod2Mesh == null)
                {
                    Debug.LogWarning($"[WorldProceduralFloraBakedStarterGenerator] Starter '{spec.RootToken}' is missing the required 3-visible-LOD mesh set.");
                    return false;
                }

                SanitizeGeneratedMesh(generatedLod0Mesh);
                SanitizeGeneratedMesh(generatedLod1Mesh);
                SanitizeGeneratedMesh(generatedLod2Mesh);

                generatedLod0Mesh.name = spec.Lod0MeshAssetName;
                generatedLod1Mesh.name = spec.Lod1MeshAssetName;
                generatedLod2Mesh.name = spec.Lod2MeshAssetName;

                Mesh bakedLod0Mesh = CreateOrUpdateMeshAsset(spec.Lod0MeshPath, generatedLod0Mesh);
                Mesh bakedLod1Mesh = CreateOrUpdateMeshAsset(spec.Lod1MeshPath, generatedLod1Mesh);
                Mesh bakedLod2Mesh = CreateOrUpdateMeshAsset(spec.Lod2MeshPath, generatedLod2Mesh);
                updatedMeshes += 3;

                bakedRoot = new GameObject(spec.PrefabName);
                LODGroup lodGroup = bakedRoot.AddComponent<LODGroup>();
                lodGroup.animateCrossFading = true;
                lodGroup.fadeMode = LODFadeMode.CrossFade;

                Renderer lod0Renderer = CreateLodRenderer(bakedRoot.transform, "__LOD0", bakedLod0Mesh, material);
                Renderer lod1Renderer = CreateLodRenderer(bakedRoot.transform, "__LOD1", bakedLod1Mesh, material);
                Renderer lod2Renderer = CreateLodRenderer(bakedRoot.transform, "__LOD2", bakedLod2Mesh, material);
                lodGroup.SetLODs(new[]
                {
                    new LOD(Lod0Threshold, new[] { lod0Renderer }),
                    new LOD(Lod1Threshold, new[] { lod1Renderer }),
                    new LOD(Lod2Threshold, new[] { lod2Renderer })
                });

                ApplyManualLodGroupBounds(lodGroup, bakedLod0Mesh, bakedLod1Mesh, bakedLod2Mesh);

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

        private static void SanitizeGeneratedMesh(Mesh mesh)
        {
            if (mesh == null)
                return;

            List<Vector3> vertices = new List<Vector3>(mesh.vertexCount);
            mesh.GetVertices(vertices);
            if (vertices.Count == 0)
            {
                mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 0.01f);
                return;
            }

            bool changedVertices = false;
            int firstFiniteIndex = -1;
            for (int i = 0; i < vertices.Count; i++)
            {
                if (IsFiniteVector3(vertices[i]))
                {
                    firstFiniteIndex = i;
                    break;
                }
            }

            Vector3 fallbackVertex = firstFiniteIndex >= 0 ? vertices[firstFiniteIndex] : Vector3.zero;
            for (int i = 0; i < vertices.Count; i++)
            {
                if (IsFiniteVector3(vertices[i]))
                    continue;

                vertices[i] = fallbackVertex;
                changedVertices = true;
            }

            if (changedVertices)
                mesh.SetVertices(vertices);

            Bounds bounds = new Bounds(fallbackVertex, Vector3.zero);
            bool hasFiniteVertex = firstFiniteIndex >= 0;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 vertex = vertices[i];
                if (!IsFiniteVector3(vertex))
                    continue;

                if (!hasFiniteVertex)
                {
                    bounds = new Bounds(vertex, Vector3.zero);
                    hasFiniteVertex = true;
                }
                else
                {
                    bounds.Encapsulate(vertex);
                }
            }

            if (!hasFiniteVertex)
                bounds = new Bounds(Vector3.zero, Vector3.one * 0.01f);

            if (bounds.size.sqrMagnitude < 0.000001f)
                bounds.Expand(0.01f);

            mesh.bounds = bounds;
        }

        private static void ApplyManualLodGroupBounds(LODGroup lodGroup, Mesh lod0Mesh, Mesh lod1Mesh, Mesh lod2Mesh)
        {
            if (lodGroup == null)
                return;

            Mesh[] meshes = { lod0Mesh, lod1Mesh, lod2Mesh };
            bool hasBounds = false;
            Bounds combinedBounds = default;

            for (int i = 0; i < meshes.Length; i++)
            {
                Mesh mesh = meshes[i];
                if (mesh == null)
                    continue;

                Bounds meshBounds = mesh.bounds;
                if (!IsFiniteBounds(meshBounds))
                    continue;

                if (!hasBounds)
                {
                    combinedBounds = meshBounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(meshBounds.min);
                    combinedBounds.Encapsulate(meshBounds.max);
                }
            }

            if (!hasBounds)
                return;

            lodGroup.localReferencePoint = combinedBounds.center;
            lodGroup.size = Mathf.Max(combinedBounds.size.x, combinedBounds.size.y, combinedBounds.size.z);
        }

        private static bool IsFiniteBounds(Bounds bounds)
        {
            return IsFiniteVector3(bounds.center) && IsFiniteVector3(bounds.size);
        }

        private static bool IsFiniteVector3(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
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

            parts.Sort((left, right) => right.Importance.CompareTo(left.Importance));
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
                CreateSpec("family.kelp.tall", "family_kelp_tall__lamina", new Vector3(0.2f, 3.45f, 0.2f)),
                CreateSpec("family.kelp.tall", "family_kelp_tall__rope", new Vector3(0.14f, 3.35f, 0.14f)),
                CreateSpec("family.kelp.tall", "family_kelp_tall__banner", new Vector3(0.19f, 3.7f, 0.19f)),
                CreateSpec("family.kelp.tall", "family_kelp_tall__lance", new Vector3(0.13f, 3.5f, 0.13f)),
                CreateSpec("family.kelp.tall", "family_kelp_tall__seedling__s55-90", new Vector3(0.08f, 0.72f, 0.08f)),
                CreateSpec("family.kelp.tall", "family_kelp_tall__tower__s130-185", new Vector3(0.32f, 8.4f, 0.32f)),
                CreateSpec("family.kelp.tall", "family_kelp_tall__colossus__s160-240", new Vector3(0.46f, 21f, 0.46f)),
                CreateSpec("family.kelp.tall", "family_kelp_tall__sail__s115-175", new Vector3(0.3f, 7.2f, 0.3f)),
                CreateSpec("family.kelp.tall", "family_kelp_tall__paddle__s90-150", new Vector3(0.24f, 5.2f, 0.24f)),
                CreateSpec("family.kelp.tall", "family_kelp_tall__broadleaf__s110-170", new Vector3(0.34f, 6f, 0.34f)),
                CreateSpec("family.kelp.tall", "family_kelp_tall__frondcrest__s105-165", new Vector3(0.24f, 6.4f, 0.24f)),
                CreateSpec("family.kelp.patch.dense", "family_kelp_patch_dense__patch", new Vector3(0.18f, 2.8f, 0.18f)),
                CreateSpec("family.kelp.patch.dense", "family_kelp_patch_dense__patch_tall", new Vector3(0.2f, 3.1f, 0.2f)),
                CreateSpec("family.kelp.patch.dense", "family_kelp_patch_dense__ring", new Vector3(0.17f, 2.7f, 0.17f)),
                CreateSpec("family.kelp.patch.dense", "family_kelp_patch_dense__brush", new Vector3(0.16f, 2.45f, 0.16f)),
                CreateSpec("family.kelp.patch.dense", "family_kelp_patch_dense__sheet", new Vector3(0.19f, 2.9f, 0.19f)),
                CreateSpec("family.kelp.patch.dense", "family_kelp_patch_dense__tuft", new Vector3(0.16f, 2.25f, 0.16f)),
                CreateSpec("family.kelp.patch.dense", "family_kelp_patch_dense__drape", new Vector3(0.2f, 2.75f, 0.2f)),
                CreateSpec("family.kelp.patch.dense", "family_kelp_patch_dense__nest__s65-105", new Vector3(0.12f, 1.35f, 0.12f)),
                CreateSpec("family.kelp.patch.dense", "family_kelp_patch_dense__sheetwall__s120-185", new Vector3(0.26f, 5.6f, 0.26f)),
                CreateSpec("family.kelp.patch.dense", "family_kelp_patch_dense__bladder__s80-135", new Vector3(0.18f, 3.1f, 0.18f)),
                CreateSpec("family.kelp.patch.dense", "family_kelp_patch_dense__paddlespray__s70-120", new Vector3(0.18f, 2.9f, 0.18f)),
                CreateSpec("family.kelp.patch.dense", "family_kelp_patch_dense__frilltuft__s75-125", new Vector3(0.19f, 3f, 0.19f)),
                CreateSpec("family.kelp.canopy", "family_kelp_canopy__crown", new Vector3(0.22f, 4.2f, 0.22f)),
                CreateSpec("family.kelp.canopy", "family_kelp_canopy__frond", new Vector3(0.18f, 3.8f, 0.18f)),
                CreateSpec("family.kelp.canopy", "family_kelp_canopy__fan", new Vector3(0.2f, 4.05f, 0.2f)),
                CreateSpec("family.kelp.canopy", "family_kelp_canopy__mantle", new Vector3(0.24f, 4.15f, 0.24f)),
                CreateSpec("family.kelp.canopy", "family_kelp_canopy__splay", new Vector3(0.22f, 3.95f, 0.22f)),
                CreateSpec("family.kelp.canopy", "family_kelp_canopy__veil", new Vector3(0.24f, 4.1f, 0.24f)),
                CreateSpec("family.kelp.canopy", "family_kelp_canopy__rosette", new Vector3(0.21f, 3.65f, 0.21f)),
                CreateSpec("family.kelp.canopy", "family_kelp_canopy__laminaria__s105-165", new Vector3(0.28f, 6.8f, 0.28f)),
                CreateSpec("family.kelp.canopy", "family_kelp_canopy__sheetwall__s150-230", new Vector3(0.34f, 10.5f, 0.34f)),
                CreateSpec("family.kelp.canopy", "family_kelp_canopy__tapestry__s160-240", new Vector3(0.38f, 11.2f, 0.38f)),
                CreateSpec("family.kelp.canopy", "family_kelp_canopy__windrow__s145-230", new Vector3(0.34f, 10.8f, 0.34f)),
                CreateSpec("family.kelp.canopy", "family_kelp_canopy__tanglemat__s130-205", new Vector3(0.3f, 8.4f, 0.3f)),
                CreateSpec("family.kelp.canopy", "family_kelp_canopy__oar__s110-180", new Vector3(0.26f, 5.8f, 0.26f)),
                CreateSpec("family.kelp.canopy", "family_kelp_canopy__paddlefan__s120-190", new Vector3(0.3f, 7.6f, 0.3f)),
                CreateSpec("family.kelp.canopy", "family_kelp_canopy__featherfan__s120-200", new Vector3(0.3f, 8.2f, 0.3f)),
                CreateSpec("family.kelp.abyssal", "family_kelp_abyssal__strap", new Vector3(0.16f, 3.45f, 0.16f)),
                CreateSpec("family.kelp.abyssal", "family_kelp_abyssal__shroud", new Vector3(0.18f, 3.8f, 0.18f)),
                CreateSpec("family.kelp.abyssal", "family_kelp_abyssal__nodule", new Vector3(0.17f, 3.6f, 0.17f)),
                CreateSpec("family.kelp.abyssal", "family_kelp_abyssal__whip", new Vector3(0.14f, 3.95f, 0.14f)),
                CreateSpec("family.kelp.abyssal", "family_kelp_abyssal__mantle", new Vector3(0.2f, 4.05f, 0.2f)),
                CreateSpec("family.kelp.abyssal", "family_kelp_abyssal__braid", new Vector3(0.18f, 3.88f, 0.18f)),
                CreateSpec("family.kelp.abyssal", "family_kelp_abyssal__pennant", new Vector3(0.22f, 4.18f, 0.22f)),
                CreateSpec("family.kelp.abyssal", "family_kelp_abyssal__reed__s80-140", new Vector3(0.12f, 2.4f, 0.12f)),
                CreateSpec("family.kelp.abyssal", "family_kelp_abyssal__cathedral__s140-240", new Vector3(0.28f, 9.6f, 0.28f)),
                CreateSpec("family.kelp.abyssal", "family_kelp_abyssal__cowl__s110-180", new Vector3(0.24f, 6.9f, 0.24f)),
                CreateSpec("family.kelp.abyssal", "family_kelp_abyssal__veilwall__s150-240", new Vector3(0.3f, 10.8f, 0.3f)),
                CreateSpec("family.kelp.abyssal", "family_kelp_abyssal__lantern__s100-180", new Vector3(0.22f, 6.2f, 0.22f)),
                CreateSpec("family.kelp.abyssal", "family_kelp_abyssal__petal__s100-170", new Vector3(0.24f, 6f, 0.24f)),
                CreateSpec("family.kelp.abyssal", "family_kelp_abyssal__tatterveil__s110-185", new Vector3(0.24f, 6.6f, 0.24f)),
                CreateSpec("family.coral.low", "family_coral_low__bed", new Vector3(1.22f, 0.8f, 1.12f)),
                CreateSpec("family.coral.low", "family_coral_low__plate", new Vector3(1.06f, 0.74f, 1.02f)),
                CreateSpec("family.coral.low", "family_coral_low__knoll", new Vector3(1.14f, 0.86f, 1.08f)),
                CreateSpec("family.coral.low", "family_coral_low__spread", new Vector3(1.38f, 0.62f, 1.26f)),
                CreateSpec("family.coral.low", "family_coral_low__mound", new Vector3(1.02f, 0.98f, 0.98f)),
                CreateSpec("family.coral.low", "family_coral_low__saucer", new Vector3(1.18f, 0.68f, 1.2f)),
                CreateSpec("family.coral.branching", "family_coral_branching__branch", new Vector3(0.92f, 1.28f, 0.92f)),
                CreateSpec("family.coral.branching", "family_coral_branching__mass", new Vector3(1.02f, 1.18f, 1.02f)),
                CreateSpec("family.coral.branching", "family_coral_branching__fan", new Vector3(1f, 1.22f, 0.98f)),
                CreateSpec("family.coral.branching", "family_coral_branching__crest", new Vector3(1.08f, 1.36f, 0.96f)),
                CreateSpec("family.coral.branching", "family_coral_branching__bouquet", new Vector3(0.98f, 1.46f, 0.98f)),
                CreateSpec("family.coral.branching", "family_coral_branching__thicket", new Vector3(1.12f, 1.34f, 1.08f)),
                CreateSpec("family.coral.massive", "family_coral_massive__head", new Vector3(1.26f, 1.02f, 1.18f)),
                CreateSpec("family.coral.massive", "family_coral_massive__porous", new Vector3(1.16f, 0.94f, 1.1f)),
                CreateSpec("family.coral.massive", "family_coral_massive__boulder", new Vector3(1.28f, 1.08f, 1.22f)),
                CreateSpec("family.coral.massive", "family_coral_massive__dome", new Vector3(1.34f, 0.96f, 1.3f)),
                CreateSpec("family.coral.massive", "family_coral_massive__lobed", new Vector3(1.18f, 1.1f, 1.14f)),
                CreateSpec("family.coral.massive", "family_coral_massive__buttress", new Vector3(1.42f, 1.18f, 1.08f)),
                CreateSpec("family.coral.plate", "family_coral_plate__ledge", new Vector3(1.24f, 0.98f, 1.18f)),
                CreateSpec("family.coral.plate", "family_coral_plate__shelf", new Vector3(1.12f, 0.9f, 1.08f)),
                CreateSpec("family.coral.plate", "family_coral_plate__stack", new Vector3(1.06f, 1.04f, 1f)),
                CreateSpec("family.coral.plate", "family_coral_plate__terrace", new Vector3(1.34f, 1.08f, 1.26f)),
                CreateSpec("family.coral.plate", "family_coral_plate__canopy", new Vector3(1.2f, 1.12f, 1.14f)),
                CreateSpec("family.coral.plate", "family_coral_plate__bastion", new Vector3(1.3f, 1.22f, 1.16f)),
                CreateSpec("family.coral.brittle", "family_coral_brittle__sprig", new Vector3(0.92f, 1.26f, 0.92f)),
                CreateSpec("family.coral.brittle", "family_coral_brittle__fan", new Vector3(1.04f, 1.3f, 1f)),
                CreateSpec("family.coral.brittle", "family_coral_brittle__spire", new Vector3(0.88f, 1.42f, 0.88f)),
                CreateSpec("family.coral.brittle", "family_coral_brittle__lace", new Vector3(1.02f, 1.36f, 0.98f)),
                CreateSpec("family.coral.brittle", "family_coral_brittle__crown", new Vector3(0.94f, 1.5f, 0.94f)),
                CreateSpec("family.coral.brittle", "family_coral_brittle__thicket", new Vector3(0.98f, 1.48f, 0.96f)),
                CreateSpec("family.coral.brittle", "family_coral_brittle__halo", new Vector3(1.08f, 1.34f, 1.02f)),
                CreateSpec("family.coral.brittle", "family_coral_brittle__candelabra", new Vector3(1.06f, 1.62f, 1.0f)),
                CreateSpec("family.coral.brittle", "family_coral_brittle__wreath", new Vector3(1.14f, 1.28f, 1.12f)),
                CreateSpec("family.coral.brittle", "family_coral_brittle__cathedral", new Vector3(1.0f, 1.74f, 0.98f))
            };
        }

        private static StarterSpec CreateSpec(string familyId, string rootToken, Vector3 shapeScale)
        {
            string safeFamilyToken = familyId.Replace('.', '_');
            string prefabName = GeneratedPrefix + rootToken;
            string familyFolderPath = $"{WorldProceduralFloraFinalVariantAuthoring.FloraFinalRootFolder}/{safeFamilyToken}";
            string lod0MeshAssetName = prefabName + "_LOD0_Mesh";
            string lod1MeshAssetName = prefabName + "_LOD1_Mesh";
            string lod2MeshAssetName = prefabName + "_LOD2_Mesh";

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
                lod2MeshAssetName,
                $"{familyFolderPath}/{lod2MeshAssetName}.asset");
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
                string lod2MeshAssetName,
                string lod2MeshPath)
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
                Lod2MeshAssetName = lod2MeshAssetName;
                Lod2MeshPath = lod2MeshPath;
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
            public string Lod2MeshAssetName { get; }
            public string Lod2MeshPath { get; }
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
