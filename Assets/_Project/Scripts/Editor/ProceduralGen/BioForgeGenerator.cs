using System;
using System.IO;
using System.Text;
using Hecton8.Core;
using Hecton8.Editor.ColliderOptimization1716;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Editor.ProceduralGen
{
    internal static class BioForgeGenerator
    {
        private const int LodCount = 3;
        private const int MandatedBatchCount = 100;
        private const int MaxExpansionChars = 250000;
        private const float MinTriangleAreaSq = 1e-10f;
        private const string DefaultRuleFolder = "Assets/_Project/Data/ProceduralGen";
        private const string DefaultGeneratedMaterialPath = "Assets/_Project/Art/Generated/Flora/MAT_BioForge_Default.mat";
        private const float MinimumColliderAxisMeters1716 = 0.05f;
        private const string NativeMemoryOwner = nameof(BioForgeGenerator);

        private static bool _deferAssetSave;

        public static BioRuleData CreateDefaultRuleAsset()
        {
            EnsureAssetFolder(DefaultRuleFolder);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{DefaultRuleFolder}/BioRuleData_DefaultKelp.asset");
            BioRuleData rule = ScriptableObject.CreateInstance<BioRuleData>();
            AssetDatabase.CreateAsset(rule, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(rule);
            return rule;
        }

        public static void GenerateFlora(BioRuleData rule, int seed, string nameOverride)
        {
            if (rule == null)
            {
                Debug.LogError("[BioForge] Missing BioRuleData. Generation aborted.");
                return;
            }

            string assetStem = ResolveAssetStem(rule, seed, nameOverride, "Flora");

            NativeList<Matrix4x4> branchMatrices = default;
            NativeList<BioForgeBranch> branches = default;
            try
            {
                branchMatrices = AllocateTrackedNativeList<Matrix4x4>(rule.MaxBranches, Allocator.TempJob, nameof(branchMatrices));
                branches = AllocateTrackedNativeList<BioForgeBranch>(rule.MaxBranches, Allocator.TempJob, nameof(branches));
                string expanded = ExpandAxiom(rule);
                ParseLSystem(rule, expanded, seed, branchMatrices, branches);

                if (branches.Length == 0)
                {
                    Debug.LogError("[BioForge] L-system produced zero branch segments. Generation aborted.");
                    return;
                }

                BuildBounds(branches.AsArray(), rule.BoundsPadding, out float3 boundsMin, out float3 boundsMax);
                int sdfModeFlags = rule.SdfProfile == BioForgeSdfProfile.RibbonFlora ? BioForgeSdfBuildJob.ModeFlagRibbon : 0;
                Mesh[] lodMeshes = BuildMeshesFromSdf(rule, seed, branches.AsArray(), boundsMin, boundsMax, sdfModeFlags);
                SaveMeshesAndPrefab(rule, assetStem, lodMeshes, boundsMin, boundsMax, false);
            }
            finally
            {
                DisposeTrackedNativeList(ref branches, nameof(branches));
                DisposeTrackedNativeList(ref branchMatrices, nameof(branchMatrices));
            }
        }

        public static void GenerateRock(BioRuleData rule, int seed, string nameOverride)
        {
            if (rule == null)
            {
                Debug.LogError("[BioForge] Missing BioRuleData. Rock generation aborted.");
                return;
            }

            string assetStem = ResolveAssetStem(rule, seed, nameOverride, "Rock");
            float radius = rule.RockRadius + rule.RockNoiseAmplitude + rule.BoundsPadding;
            float3 boundsMin = new float3(-radius, -radius, -radius);
            float3 boundsMax = new float3(radius, radius, radius);

            NativeArray<BioForgeBranch> emptyBranches = default;
            try
            {
                emptyBranches = AllocateTrackedNativeArray<BioForgeBranch>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory, nameof(emptyBranches));
                int sdfModeFlags = BioForgeSdfBuildJob.ModeFlagRock;
                if (rule.SdfProfile == BioForgeSdfProfile.PorousRock)
                    sdfModeFlags |= BioForgeSdfBuildJob.ModeFlagPorous;
                Mesh[] lodMeshes = BuildMeshesFromSdf(rule, seed, emptyBranches, boundsMin, boundsMax, sdfModeFlags);
                SaveMeshesAndPrefab(rule, assetStem, lodMeshes, boundsMin, boundsMax, true);
            }
            finally
            {
                DisposeTrackedNativeArray(ref emptyBranches);
            }
        }

        public static void GenerateFloraBatch(BioRuleData rule, int seed, string nameOverride)
        {
            GenerateFloraBatch(rule, seed, nameOverride, MandatedBatchCount);
        }

        public static void GenerateFloraBatch(BioRuleData rule, int seed, string nameOverride, int count)
        {
            if (rule == null)
            {
                Debug.LogError("[BioForge] Missing BioRuleData. Batch generation aborted.");
                return;
            }

            int safeCount = math.clamp(count, 1, MandatedBatchCount);
            bool previousDefer = _deferAssetSave;
            bool startedAssetEditing = false;
            try
            {
                _deferAssetSave = true;
                if (!previousDefer)
                {
                    AssetDatabase.StartAssetEditing();
                    startedAssetEditing = true;
                }

                for (int i = 0; i < safeCount; i++)
                {
                    int variationSeed = unchecked(seed + (i * 265443576));
                    if (!Application.isBatchMode && EditorUtility.DisplayCancelableProgressBar("Bio-Forge", $"Generating flora {i + 1}/{safeCount}", (i + 1f) * math.rcp(safeCount)))
                    {
                        Debug.LogWarning($"[BioForge] Flora batch cancelled after {i} generated variants.");
                        break;
                    }

                    GenerateFlora(rule, variationSeed, string.IsNullOrWhiteSpace(nameOverride) ? null : $"{nameOverride}_{i:000}");
                }
            }
            finally
            {
                _deferAssetSave = previousDefer;
                if (!Application.isBatchMode)
                    EditorUtility.ClearProgressBar();
                if (startedAssetEditing)
                    AssetDatabase.StopAssetEditing();
                if (!previousDefer)
                    AssetDatabase.SaveAssets();
            }
        }

        public static void GenerateRockBatch(BioRuleData rule, int seed, string nameOverride)
        {
            GenerateRockBatch(rule, seed, nameOverride, MandatedBatchCount);
        }

        public static void GenerateRockBatch(BioRuleData rule, int seed, string nameOverride, int count)
        {
            if (rule == null)
            {
                Debug.LogError("[BioForge] Missing BioRuleData. Rock batch generation aborted.");
                return;
            }

            int safeCount = math.clamp(count, 1, MandatedBatchCount);
            bool previousDefer = _deferAssetSave;
            bool startedAssetEditing = false;
            try
            {
                _deferAssetSave = true;
                if (!previousDefer)
                {
                    AssetDatabase.StartAssetEditing();
                    startedAssetEditing = true;
                }

                for (int i = 0; i < safeCount; i++)
                {
                    int variationSeed = unchecked(seed + 0x51ED270B + (i * 1103515245));
                    if (!Application.isBatchMode && EditorUtility.DisplayCancelableProgressBar("Bio-Forge", $"Generating rock {i + 1}/{safeCount}", (i + 1f) * math.rcp(safeCount)))
                    {
                        Debug.LogWarning($"[BioForge] Rock batch cancelled after {i} generated variants.");
                        break;
                    }

                    GenerateRock(rule, variationSeed, string.IsNullOrWhiteSpace(nameOverride) ? null : $"{nameOverride}_{i:000}");
                }
            }
            finally
            {
                _deferAssetSave = previousDefer;
                if (!Application.isBatchMode)
                    EditorUtility.ClearProgressBar();
                if (startedAssetEditing)
                    AssetDatabase.StopAssetEditing();
                if (!previousDefer)
                    AssetDatabase.SaveAssets();
            }
        }

        private static Mesh[] BuildMeshesFromSdf(BioRuleData rule, int seed, NativeArray<BioForgeBranch> branches, float3 boundsMin, float3 boundsMax, int sdfModeFlags)
        {
            int cells = rule.SdfResolution;
            int points = cells + 1;
            int pointCount = points * points * points;
            int cellCount = cells * cells * cells;
            float3 extent = math.max(boundsMax - boundsMin, new float3(0.1f));
            float voxelStep = math.cmax(extent) * math.rcp(cells);
            float3 step = new float3(voxelStep);
            boundsMax = boundsMin + step * cells;

            NativeArray<float> density = default;
            NativeList<BioForgeRawVertex> rawVertices = default;
            NativeArray<int> overflow = default;
            NativeArray<BioForgeMeshVertex> bakedVertices = default;

            try
            {
                // COLD ALLOC: NativeArray<float>[pointCount] - editor SDF density scratch - owner: BioForgeGenerator
                density = AllocateTrackedNativeArray<float>(pointCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, nameof(density));
                int rawCapacity = math.min(cellCount * 18, 3000000);
                // COLD ALLOC: NativeList<BioForgeRawVertex>[rawCapacity] - editor marching-cubes raw vertex output - owner: BioForgeGenerator
                rawVertices = AllocateTrackedNativeList<BioForgeRawVertex>(rawCapacity, Allocator.TempJob, nameof(rawVertices));
                // COLD ALLOC: NativeArray<int>[1] - editor MC overflow flag - owner: BioForgeGenerator
                overflow = AllocateTrackedNativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory, nameof(overflow));

                var sdfJob = new BioForgeSdfBuildJob
                {
                    Branches = branches,
                    BranchCount = (sdfModeFlags & BioForgeSdfBuildJob.ModeFlagRock) != 0 ? 0 : branches.Length,
                    Density = density,
                    PointResolution = points,
                    ModeFlags = sdfModeFlags,
                    Seed = (uint)seed,
                    BoundsMin = boundsMin,
                    Step = step,
                    SmoothMinK = rule.SmoothMinK,
                    RibbonThicknessScale = rule.RibbonThicknessScale,
                    RibbonWidthScale = rule.RibbonWidthScale,
                    RockRadius = rule.RockRadius,
                    RockNoiseAmplitude = rule.RockNoiseAmplitude,
                    RockNoiseFrequency = rule.RockNoiseFrequency,
                    RockPoreCount = rule.RockPoreCount,
                    RockPoreRadius = rule.RockPoreRadius,
                    RockPoreSurfaceBias = rule.RockPoreSurfaceBias
                };

                JobHandle sdfHandle = sdfJob.Schedule(pointCount, 64);
                var marchingJob = new BioForgeMarchingCubesJob
                {
                    Density = density,
                    RawVertices = rawVertices,
                    Overflow = overflow,
                    Cells = cells,
                    Points = points,
                    BoundsMin = boundsMin,
                    Step = step
                };

                JobHandle extractHandle = marchingJob.Schedule(sdfHandle);
                // COLD SYNC JOB: Editor asset bake converts completed MC output into persistent mesh assets.
                extractHandle.Complete();

                int rawCount = rawVertices.Length;
                if (overflow[0] != 0)
                    Debug.LogWarning("[BioForge] Marching cubes raw vertex buffer saturated. Increase capacity or reduce SDF resolution.");

                if (rawCount <= 0)
                    return CreateEmptyLods(rule.AssetPrefix);

                // COLD ALLOC: NativeArray<BioForgeMeshVertex>[rawCount] - editor vertex attribute bake - owner: BioForgeGenerator
                bakedVertices = AllocateTrackedNativeArray<BioForgeMeshVertex>(rawCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, nameof(bakedVertices));

                var bakeJob = new BioForgeVertexBakeJob
                {
                    RawVertices = rawVertices.AsArray(),
                    Density = density,
                    Vertices = bakedVertices,
                    BoundsMin = boundsMin,
                    BoundsMax = boundsMax,
                    InvStep = math.rcp(step),
                    PointResolution = points
                };

                JobHandle bakeHandle = bakeJob.Schedule(rawCount, 64);
                // COLD SYNC JOB: Editor mesh construction requires completed vertex attributes.
                bakeHandle.Complete();

                return BuildLodMeshes(rule, bakedVertices, rawCount);
            }
            finally
            {
                DisposeTrackedNativeArray(ref bakedVertices);
                DisposeTrackedNativeList(ref rawVertices, nameof(rawVertices));
                DisposeTrackedNativeArray(ref overflow);
                DisposeTrackedNativeArray(ref density);
            }
        }

        private static Mesh[] BuildLodMeshes(BioRuleData rule, NativeArray<BioForgeMeshVertex> sourceVertices, int sourceVertexCount)
        {
            int sourceTriangles = sourceVertexCount / 3;
            Mesh[] lods = new Mesh[LodCount];
            lods[0] = BuildLodMesh("LOD0", sourceVertices, sourceTriangles, math.min(sourceTriangles, rule.Lod0TriangleBudget), 0.005f);
            lods[1] = BuildLodMesh("LOD1", sourceVertices, sourceTriangles, math.min(sourceTriangles, rule.Lod1TriangleBudget), 0.025f);
            lods[2] = BuildLodMesh("LOD2", sourceVertices, sourceTriangles, math.min(sourceTriangles, rule.Lod2TriangleBudget), 0.075f);
            return lods;
        }

        private static Mesh BuildLodMesh(string lodName, NativeArray<BioForgeMeshVertex> sourceVertices, int sourceTriangles, int targetTriangles, float collapseThreshold)
        {
            int safeTargetTriangles = math.max(1, targetTriangles);
            int outputCount = safeTargetTriangles * 3;
            NativeArray<BioForgeMeshVertex> outputVertices = default;

            try
            {
                // COLD ALLOC: NativeArray<BioForgeMeshVertex>[outputCount] - editor LOD output vertices - owner: BioForgeGenerator
                outputVertices = AllocateTrackedNativeArray<BioForgeMeshVertex>(outputCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory, nameof(outputVertices));

                var decimateJob = new BioForgeEdgeCollapseDecimationJob
                {
                    SourceVertices = sourceVertices,
                    OutputVertices = outputVertices,
                    SourceTriangleCount = math.max(1, sourceTriangles),
                    OutputTriangleCount = safeTargetTriangles,
                    CollapseCellSize = collapseThreshold
                };

                JobHandle handle = decimateJob.Schedule(safeTargetTriangles, 64);
                // COLD SYNC JOB: Editor LOD asset creation consumes completed decimation output immediately.
                handle.Complete();

                return CreateUnityMesh(lodName, outputVertices, outputCount);
            }
            finally
            {
                DisposeTrackedNativeArray(ref outputVertices);
            }
        }

        private static NativeArray<T> AllocateTrackedNativeArray<T>(int length, Allocator allocator, NativeArrayOptions options, string label) where T : struct
        {
            if (length <= 0)
                return default;

            NativeArray<T> array = new NativeArray<T>(length, allocator, options);
            if (!array.IsCreated)
                throw new InvalidOperationException("[BioForge] NativeArray allocation failed for " + label + ".");

            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, ResolveNativeAllocationLifetime(allocator));
                if (sentinelId <= 0)
                    throw new InvalidOperationException("[BioForge] NativeMemorySentinel rejected NativeArray registration for " + label + ".");
            }
            catch
            {
                array.Dispose();
                throw;
            }

            return array;
        }

        private static void DisposeTrackedNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            try
            {
                NativeMemorySentinel.UnregisterNativeArray(array);
            }
            finally
            {
                array.Dispose();
                array = default;
            }
        }

        private static NativeList<T> AllocateTrackedNativeList<T>(int capacity, Allocator allocator, string label) where T : unmanaged
        {
            int safeCapacity = math.max(1, capacity);
            NativeList<T> list = new NativeList<T>(safeCapacity, allocator);
            if (!list.IsCreated)
                throw new InvalidOperationException("[BioForge] NativeList allocation failed for " + label + ".");

            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeList(list, NativeMemoryOwner, label, ResolveNativeAllocationLifetime(allocator));
                if (sentinelId <= 0)
                    throw new InvalidOperationException("[BioForge] NativeMemorySentinel rejected NativeList registration for " + label + ".");
            }
            catch
            {
                list.Dispose();
                throw;
            }

            return list;
        }

        private static void DisposeTrackedNativeList<T>(ref NativeList<T> list, string label) where T : unmanaged
        {
            if (!list.IsCreated)
                return;

            try
            {
                NativeMemorySentinel.UnregisterNativeList(NativeMemoryOwner, label);
            }
            finally
            {
                list.Dispose();
                list = default;
            }
        }

        private static NativeAllocationLifetime ResolveNativeAllocationLifetime(Allocator allocator)
        {
            switch (allocator)
            {
                case Allocator.Temp:
                    return NativeAllocationLifetime.Temp;
                case Allocator.TempJob:
                    return NativeAllocationLifetime.TempJob;
                case Allocator.Persistent:
                    return NativeAllocationLifetime.Session;
                default:
                    return NativeAllocationLifetime.Session;
            }
        }

        private static Mesh CreateUnityMesh(string lodName, NativeArray<BioForgeMeshVertex> vertices, int count)
        {
            int validTriangles = 0;
            for (int i = 0; i + 2 < count; i += 3)
            {
                if (IsValidTriangle(vertices[i], vertices[i + 1], vertices[i + 2]))
                    validTriangles++;
            }

            int validCount = validTriangles * 3;
            if (validCount == 0)
            {
                return new Mesh
                {
                    name = $"BioForge_{lodName}",
                    indexFormat = IndexFormat.UInt16
                };
            }

            Vector3[] positions = new Vector3[validCount];
            Vector3[] normals = new Vector3[validCount];
            Vector2[] uvs = new Vector2[validCount];
            Color[] colors = new Color[validCount];
            int[] managedIndices = new int[validCount];

            int dst = 0;
            for (int i = 0; i + 2 < count; i += 3)
            {
                BioForgeMeshVertex a = vertices[i];
                BioForgeMeshVertex b = vertices[i + 1];
                BioForgeMeshVertex c = vertices[i + 2];
                if (!IsValidTriangle(a, b, c))
                    continue;

                WriteManagedVertex(a, dst, positions, normals, uvs, colors, managedIndices);
                WriteManagedVertex(b, dst + 1, positions, normals, uvs, colors, managedIndices);
                WriteManagedVertex(c, dst + 2, positions, normals, uvs, colors, managedIndices);
                dst += 3;
            }

            NormalizeColorGradientFromFinalBounds(positions, colors);

            Mesh mesh = new Mesh
            {
                name = $"BioForge_{lodName}",
                indexFormat = validCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(managedIndices, 0, true);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private static void NormalizeColorGradientFromFinalBounds(Vector3[] positions, Color[] colors)
        {
            if (positions == null || colors == null || positions.Length == 0 || colors.Length != positions.Length)
                return;

            float minY = positions[0].y;
            float maxY = positions[0].y;
            for (int i = 1; i < positions.Length; i++)
            {
                float y = positions[i].y;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            float invRange = 1f / Mathf.Max(0.0001f, maxY - minY);
            for (int i = 0; i < colors.Length; i++)
            {
                Color color = colors[i];
                color.r = Mathf.Clamp01((positions[i].y - minY) * invRange);
                color.g = 0f;
                color.b = 0f;
                color.a = 1f;
                colors[i] = color;
            }
        }

        private static bool IsValidTriangle(BioForgeMeshVertex a, BioForgeMeshVertex b, BioForgeMeshVertex c)
        {
            if (!IsFinite(a) || !IsFinite(b) || !IsFinite(c))
                return false;

            float3 normal = math.cross(b.Position - a.Position, c.Position - a.Position);
            return math.lengthsq(normal) > MinTriangleAreaSq;
        }

        private static bool IsFinite(BioForgeMeshVertex vertex)
        {
            return math.all(math.isfinite(vertex.Position)) &&
                   math.all(math.isfinite(vertex.Normal)) &&
                   math.all(math.isfinite(vertex.Uv)) &&
                   math.all(math.isfinite(vertex.Color));
        }

        private static void WriteManagedVertex(BioForgeMeshVertex vertex, int index, Vector3[] positions, Vector3[] normals, Vector2[] uvs, Color[] colors, int[] managedIndices)
        {
            positions[index] = new Vector3(vertex.Position.x, vertex.Position.y, vertex.Position.z);
            normals[index] = new Vector3(vertex.Normal.x, vertex.Normal.y, vertex.Normal.z);
            uvs[index] = new Vector2(vertex.Uv.x, vertex.Uv.y);
            colors[index] = new Color(vertex.Color.x, vertex.Color.y, vertex.Color.z, vertex.Color.w);
            managedIndices[index] = index;
        }

        private static void SaveMeshesAndPrefab(BioRuleData rule, string assetStem, Mesh[] lodMeshes, float3 boundsMin, float3 boundsMax, bool addRockCollider)
        {
            EnsureAssetFolder(rule.MeshOutputFolder);
            EnsureAssetFolder(rule.PrefabOutputFolder);

            for (int i = 0; i < lodMeshes.Length; i++)
            {
                string path = $"{rule.MeshOutputFolder}/{assetStem}_LOD{i}.asset";
                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                lodMeshes[i].name = $"{assetStem}_LOD{i}";
                if (existing != null)
                {
                    Mesh generated = lodMeshes[i];
                    EditorUtility.CopySerialized(generated, existing);
                    UnityEngine.Object.DestroyImmediate(generated);
                    lodMeshes[i] = existing;
                }
                else
                {
                    AssetDatabase.CreateAsset(lodMeshes[i], path);
                }
            }

            GameObject root = BuildPrefabRoot(rule, assetStem, lodMeshes, boundsMin, boundsMax, addRockCollider);
            string prefabPath = $"{rule.PrefabOutputFolder}/{assetStem}.prefab";
            try
            {
                if (!ColliderOptimizerEngine1716.ValidatePrefabColliderBudget(root, out string colliderFailure))
                    throw new InvalidOperationException("1716 collider validation failed before save: " + colliderFailure);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

                if (!ColliderOptimizerEngine1716.ValidatePrefabAssetTopology(prefabPath, out colliderFailure))
                    throw new InvalidOperationException("1716 collider validation failed after save: " + colliderFailure);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            if (!_deferAssetSave)
                AssetDatabase.SaveAssets();

            Debug.LogFormat(
                LogType.Log,
                LogOption.NoStacktrace,
                null,
                "[BioForge] Generated {0}: LOD0={1}, LOD1={2}, LOD2={3}, prefab={4}",
                assetStem,
                ResolveTriangleCount(lodMeshes[0]),
                ResolveTriangleCount(lodMeshes[1]),
                ResolveTriangleCount(lodMeshes[2]),
                prefabPath);
        }

        private static GameObject BuildPrefabRoot(BioRuleData rule, string assetStem, Mesh[] lodMeshes, float3 boundsMin, float3 boundsMax, bool addRockCollider)
        {
            GameObject root = new GameObject(assetStem);
            var lodGroup = root.AddComponent<LODGroup>();
            Material material = ResolveMaterial(rule);

            Renderer[] lod0 = { CreateLodChild(root.transform, "LOD0", lodMeshes[0], material) };
            Renderer[] lod1 = { CreateLodChild(root.transform, "LOD1", lodMeshes[1], material) };
            Renderer[] lod2 = { CreateLodChild(root.transform, "LOD2", lodMeshes[2], material) };
            Vector3 geometryOffset = ResolveGeometryOffset(lodMeshes, boundsMin, boundsMax);
            lod0[0].transform.localPosition = geometryOffset;
            lod1[0].transform.localPosition = geometryOffset;
            lod2[0].transform.localPosition = geometryOffset;

            LOD[] lods =
            {
                new LOD(0.6f, lod0),
                new LOD(0.15f, lod1),
                new LOD(0.04f, lod2)
            };
            lods[0].fadeTransitionWidth = 0.08f;
            lods[1].fadeTransitionWidth = 0.08f;
            lods[2].fadeTransitionWidth = 0.04f;
            lodGroup.SetLODs(lods);
            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = true;
            lodGroup.RecalculateBounds();
            if (addRockCollider)
            {
                GameObject colliderObject = new GameObject("COL_CompoundProxy_1716");
                colliderObject.transform.SetParent(root.transform, false);
                colliderObject.transform.localPosition = geometryOffset;
                BoxCollider collider = colliderObject.AddComponent<BoxCollider>();
                Bounds bounds = ResolveColliderBounds(lodMeshes, boundsMin, boundsMax);
                collider.center = bounds.center;
                collider.size = new Vector3(
                    Mathf.Max(bounds.size.x, MinimumColliderAxisMeters1716),
                    Mathf.Max(bounds.size.y, MinimumColliderAxisMeters1716),
                    Mathf.Max(bounds.size.z, MinimumColliderAxisMeters1716));
            }

            return root;
        }

        private static Bounds ResolveColliderBounds(Mesh[] lodMeshes, float3 boundsMin, float3 boundsMax)
        {
            if (lodMeshes != null && lodMeshes.Length > 0 && lodMeshes[0] != null && lodMeshes[0].vertexCount > 0)
                return lodMeshes[0].bounds;

            Vector3 min = new Vector3(boundsMin.x, boundsMin.y, boundsMin.z);
            Vector3 max = new Vector3(boundsMax.x, boundsMax.y, boundsMax.z);
            if (!IsFinite(min) || !IsFinite(max))
                return new Bounds(Vector3.zero, Vector3.one);

            Bounds bounds = new Bounds((min + max) * 0.5f, max - min);
            return IsFinite(bounds.center) && IsFinite(bounds.extents) ? bounds : new Bounds(Vector3.zero, Vector3.one);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static Vector3 ResolveGeometryOffset(Mesh[] lodMeshes, float3 boundsMin, float3 boundsMax)
        {
            Vector3 center;
            if (lodMeshes != null && lodMeshes.Length > 0 && lodMeshes[0] != null && lodMeshes[0].vertexCount > 0)
            {
                center = lodMeshes[0].bounds.center;
            }
            else
            {
                float3 fallbackCenter = (boundsMin + boundsMax) * 0.5f;
                center = new Vector3(fallbackCenter.x, fallbackCenter.y, fallbackCenter.z);
            }

            return new Vector3(-center.x, 0f, -center.z);
        }

        private static Renderer CreateLodChild(Transform parent, string name, Mesh mesh, Material material)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            MeshFilter filter = child.AddComponent<MeshFilter>();
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.allowOcclusionWhenDynamic = false;
            return renderer;
        }

        private static Material ResolveMaterial(BioRuleData rule)
        {
            if (rule.Material != null)
                return rule.Material;

            Material fallback = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
            if (fallback != null)
                return fallback;

            fallback = AssetDatabase.LoadAssetAtPath<Material>(DefaultGeneratedMaterialPath);
            if (fallback != null)
                return fallback;

            EnsureAssetFolder("Assets/_Project/Art/Generated/Flora");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("[BioForge] No fallback shader found. Assign a material on BioRuleData.");
                return null;
            }

            fallback = new Material(shader)
            {
                name = "MAT_BioForge_Default"
            };
            AssetDatabase.CreateAsset(fallback, DefaultGeneratedMaterialPath);
            return fallback;
        }

        private static string ExpandAxiom(BioRuleData rule)
        {
            string current = rule.Axiom;
            for (int iteration = 0; iteration < rule.Iterations; iteration++)
            {
                var builder = new StringBuilder(math.min(MaxExpansionChars, math.max(64, current.Length * 2)));
                for (int i = 0; i < current.Length && builder.Length < MaxExpansionChars; i++)
                {
                    char symbol = current[i];
                    if (rule.TryGetReplacement(symbol, out string replacement))
                        AppendCapped(builder, replacement);
                    else
                        AppendCapped(builder, symbol);
                }

                current = builder.ToString();
            }

            return current;
        }

        private static void AppendCapped(StringBuilder builder, string text)
        {
            if (string.IsNullOrEmpty(text) || builder.Length >= MaxExpansionChars)
                return;

            int remaining = MaxExpansionChars - builder.Length;
            if (text.Length <= remaining)
            {
                builder.Append(text);
                return;
            }

            builder.Append(text, 0, remaining);
        }

        private static void AppendCapped(StringBuilder builder, char symbol)
        {
            if (builder.Length < MaxExpansionChars)
                builder.Append(symbol);
        }

        private static void ParseLSystem(BioRuleData rule, string expanded, int seed, NativeList<Matrix4x4> branchMatrices, NativeList<BioForgeBranch> branches)
        {
            NativeList<TurtleState> stateStack = AllocateTrackedNativeList<TurtleState>(math.max(64, rule.MaxBranches), Allocator.Temp, nameof(stateStack));
            try
            {
                float angleRad = math.radians(rule.AngleDegrees + (Hash01((uint)seed, 17u) - 0.5f) * 4f);
                TurtleState state = new TurtleState
                {
                    Position = float3.zero,
                    Rotation = quaternion.identity,
                    Depth = 0
                };

                for (int i = 0; i < expanded.Length && branches.Length < rule.MaxBranches; i++)
                {
                    char symbol = expanded[i];
                    if (symbol == 'F')
                    {
                        EmitBranch(rule, ref state, branchMatrices, branches);
                    }
                    else if (symbol == '+')
                    {
                        state.Rotation = math.mul(state.Rotation, quaternion.AxisAngle(new float3(0f, 0f, 1f), angleRad));
                    }
                    else if (symbol == '-')
                    {
                        state.Rotation = math.mul(state.Rotation, quaternion.AxisAngle(new float3(0f, 0f, 1f), -angleRad));
                    }
                    else if (symbol == '&')
                    {
                        state.Rotation = math.mul(state.Rotation, quaternion.AxisAngle(new float3(1f, 0f, 0f), angleRad));
                    }
                    else if (symbol == '^')
                    {
                        state.Rotation = math.mul(state.Rotation, quaternion.AxisAngle(new float3(1f, 0f, 0f), -angleRad));
                    }
                    else if (symbol == '/')
                    {
                        state.Rotation = math.mul(state.Rotation, quaternion.AxisAngle(new float3(0f, 1f, 0f), angleRad));
                    }
                    else if (symbol == '\\')
                    {
                        state.Rotation = math.mul(state.Rotation, quaternion.AxisAngle(new float3(0f, 1f, 0f), -angleRad));
                    }
                    else if (symbol == '[')
                    {
                        stateStack.Add(state);
                        state.Depth++;
                    }
                    else if (symbol == ']' && stateStack.Length > 0)
                    {
                        state = stateStack[stateStack.Length - 1];
                        stateStack.RemoveAt(stateStack.Length - 1);
                    }
                }
            }
            finally
            {
                DisposeTrackedNativeList(ref stateStack, nameof(stateStack));
            }
        }

        private static void EmitBranch(BioRuleData rule, ref TurtleState state, NativeList<Matrix4x4> branchMatrices, NativeList<BioForgeBranch> branches)
        {
            float depthLengthScale = PowByDepth(rule.LengthTaper, state.Depth);
            float depthRadiusScale = PowByDepth(rule.RadiusTaper, state.Depth);
            float length = rule.StepLength * depthLengthScale;
            float radius0 = math.max(rule.MinimumRadius, rule.RootRadius * depthRadiusScale);
            float radius1 = math.max(rule.MinimumRadius, radius0 * rule.RadiusTaper);

            float3 direction = math.mul(state.Rotation, new float3(0f, 1f, 0f));
            if (!math.all(math.isfinite(direction)) || math.lengthsq(direction) < 1e-8f)
                direction = new float3(0f, 1f, 0f);

            float3 start = state.Position;
            float invDirectionLength = math.rsqrt(math.max(1e-8f, math.lengthsq(direction)));
            float3 end = start + direction * invDirectionLength * length;
            float maxRadius = math.max(radius0, radius1);
            float3 branchBoundsMin = math.min(start, end) - maxRadius;
            float3 branchBoundsMax = math.max(start, end) + maxRadius;
            float3 mid = (start + end) * 0.5f;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, new Vector3(direction.x, direction.y, direction.z));
            Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(mid.x, mid.y, mid.z), rotation, new Vector3(radius0, length, radius0));

            branchMatrices.Add(matrix);
            branches.Add(new BioForgeBranch
            {
                Start = start,
                End = end,
                BoundsMin = branchBoundsMin,
                BoundsMax = branchBoundsMax,
                RadiusStart = radius0,
                RadiusEnd = radius1,
                MaxRadius = maxRadius
            });

            state.Position = end;
        }

        private static float PowByDepth(float value, int depth)
        {
            int count = math.max(0, depth);
            float result = 1f;
            float multiplier = math.max(0f, value);
            for (int i = 0; i < count; i++)
                result *= multiplier;

            return math.isfinite(result) ? result : 0f;
        }

        private static void BuildBounds(NativeArray<BioForgeBranch> branches, float padding, out float3 boundsMin, out float3 boundsMax)
        {
            boundsMin = new float3(float.MaxValue);
            boundsMax = new float3(float.MinValue);
            float safePadding = math.max(0f, padding);

            for (int i = 0; i < branches.Length; i++)
            {
                BioForgeBranch branch = branches[i];
                boundsMin = math.min(boundsMin, branch.BoundsMin - safePadding);
                boundsMax = math.max(boundsMax, branch.BoundsMax + safePadding);
            }

            if (!math.all(math.isfinite(boundsMin)) || !math.all(math.isfinite(boundsMax)))
            {
                boundsMin = new float3(-1f, -0.1f, -1f);
                boundsMax = new float3(1f, 2f, 1f);
            }
        }

        private static Mesh[] CreateEmptyLods(string assetPrefix)
        {
            Mesh[] meshes = new Mesh[LodCount];
            for (int i = 0; i < meshes.Length; i++)
            {
                meshes[i] = new Mesh { name = $"{assetPrefix}_Empty_LOD{i}" };
            }

            return meshes;
        }

        private static int ResolveTriangleCount(Mesh mesh)
        {
            return mesh != null && mesh.subMeshCount > 0 ? (int)(mesh.GetIndexCount(0) / 3) : 0;
        }

        private static string ResolveAssetStem(BioRuleData rule, int seed, string nameOverride, string kind)
        {
            string prefix = string.IsNullOrWhiteSpace(nameOverride) ? rule.AssetPrefix : nameOverride;
            return $"{SanitizeFileName(prefix)}_{kind}_{unchecked((uint)seed):X8}";
        }

        private static string SanitizeFileName(string input)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            string safe = input;
            for (int i = 0; i < invalid.Length; i++)
                safe = safe.Replace(invalid[i], '_');

            return safe.Replace(' ', '_');
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static float Hash01(uint seed, uint salt)
        {
            uint h = seed ^ (salt * 0x9E3779B9u);
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h * 2.3283064e-10f;
        }

        private struct TurtleState
        {
            public float3 Position;
            public quaternion Rotation;
            public int Depth;
        }
    }
}
