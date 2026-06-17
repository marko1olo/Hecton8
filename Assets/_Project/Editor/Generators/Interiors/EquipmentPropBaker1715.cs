#if UNITY_EDITOR
// ============================================================================
// HECTON-8 - EquipmentPropBaker1715.cs
// Offline hard-surface equipment baker with static interaction socket metadata.
// ============================================================================

namespace Hecton8.Editor.Interiors
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;
    using Hecton8.Editor.ColliderOptimization1716;
    using Hecton8.Interaction;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Rendering;

    public sealed class EquipmentPropBaker1715 : EditorWindow
    {
        private const string DefaultOutputFolder = "Assets/_Project/Prefabs/Equipment";
        private const string DefaultMeshFolder = "Assets/_Project/Art/Baked/Equipment";
        private const string DefaultOutputName = "GEN_Prop_1715_CockpitControlPanel";
        private const float MinimumTriangleArea = 0.000001f;
        private const int EquipmentPropVertexSizeBytes = 56;
        private const int EquipmentBakeMetricsSizeBytes = 32;
        private const int TopologyValidationResultSizeBytes = 32;
        private const int CableStrandCount = 5;
        private const int CylinderPrimitiveCount = 11;
        private const int RecessedPocketCount = 2;
        private const int CircularCsgCutCount = 2;
        private const float CableMinimumCenterY = 0.098f;
        private const float CableMinimumVertexY = 0.082f;
        private const string PreferredPressureMetalMaterialPath = "Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_WetPressureMetal.mat";
        private const string PreferredInstrumentMaterialPath = "Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_InstrumentCyan.mat";
        private const string PreferredHullMaterialPath = "Assets/_Project/Art/RuntimeShell1428/MAT_H8Shell_PressureHull.mat";
        private const string PreferredFallbackMaterialPath = "Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_BlackenedHull.mat";
        private const uint EquipmentId = 0x45513137u;
        private const uint MaterialBase = 1u;
        private const uint MaterialCavity = 2u;
        private const uint MaterialScreen = 3u;
        private const uint MaterialCable = 4u;
        private const uint MaterialGrip = 5u;
        private const uint MaterialWarning = 6u;

        private static readonly string[] ProjectMaterialSearchFolders = { "Assets/_Project" };
        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();
        private static Material s_cachedProjectMaterial;

        private Material _sharedMaterial;
        private string _outputFolder = DefaultOutputFolder;
        private string _meshFolder = DefaultMeshFolder;
        private string _outputName = DefaultOutputName;
        private int _seed = 1715;
        private float _globalQualityWeight = 0.72f;

        [MenuItem("Hecton8/Interiors/Equipment Prop Baker 1715")]
        public static void ShowWindow()
        {
            var window = GetWindow<EquipmentPropBaker1715>();
            window.titleContent = new GUIContent("Equipment Baker 1715");
            window.minSize = new Vector2(420f, 260f);
        }

        [MenuItem("Hecton8/Interiors/Bake Equipment Prop 1715 Now")]
        public static void BakeDefaultMenu()
        {
            EquipmentBakeSettings1715 settings = EquipmentBakeSettings1715.Default;
            settings.SharedMaterial = ResolveProjectMaterial();
            if (!Bake(settings, out EquipmentBakeResult1715 result))
                UnityEngine.Debug.LogError(result.FailureReason);
            else
                UnityEngine.Debug.Log(result.PrefabPath);
        }

        private void OnEnable()
        {
            if (_sharedMaterial == null)
                _sharedMaterial = ResolveProjectMaterial();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Offline Interactive Equipment Baker", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);
            _sharedMaterial = (Material)EditorGUILayout.ObjectField("Shared Material", _sharedMaterial, typeof(Material), false);
            _outputFolder = EditorGUILayout.TextField("Prefab Output Folder", _outputFolder);
            _meshFolder = EditorGUILayout.TextField("Mesh Output Folder", _meshFolder);
            _outputName = EditorGUILayout.TextField("Output Name", _outputName);
            _seed = EditorGUILayout.IntField("Seed", _seed);
            _globalQualityWeight = EditorGUILayout.Slider("GlobalQualityWeight", _globalQualityWeight, 0f, 1f);

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Bake Equipment Prop"))
            {
                EquipmentBakeSettings1715 settings = new EquipmentBakeSettings1715
                {
                    SharedMaterial = _sharedMaterial != null ? _sharedMaterial : ResolveProjectMaterial(),
                    OutputFolder = string.IsNullOrWhiteSpace(_outputFolder) ? DefaultOutputFolder : _outputFolder,
                    MeshFolder = string.IsNullOrWhiteSpace(_meshFolder) ? DefaultMeshFolder : _meshFolder,
                    OutputName = string.IsNullOrWhiteSpace(_outputName) ? DefaultOutputName : _outputName,
                    Seed = (uint)(_seed < 1 ? 1 : _seed),
                    GlobalQualityWeight = math.clamp(_globalQualityWeight, 0f, 1f)
                };

                if (!Bake(settings, out EquipmentBakeResult1715 result))
                    UnityEngine.Debug.LogError(result.FailureReason);
                else
                    UnityEngine.Debug.Log(result.PrefabPath);
            }
        }

        public static bool Bake(EquipmentBakeSettings1715 settings, out EquipmentBakeResult1715 result)
        {
            result = default;
            settings = Sanitize(settings);

            if (!ValidateStaticContracts(out string contractFailure))
            {
                result.FailureReason = contractFailure;
                return false;
            }

            if (settings.SharedMaterial == null)
            {
                result.FailureReason = "EquipmentPropBaker1715 shared material resolution failed.";
                return false;
            }

            EnsureAssetFolder(settings.OutputFolder);
            EnsureAssetFolder(settings.MeshFolder);

            var stopwatch = Stopwatch.StartNew();
            NativeList<EquipmentPropVertex1715> vertices = default;
            NativeList<int> indices = default;
            NativeArray<TopologyValidationResult1715> validation = default;

            InteractionAnchorData[] anchors = null;
            Mesh mesh = null;
            string meshPath = string.Empty;
            string prefabPath = string.Empty;
            string materialPath = settings.SharedMaterial != null ? AssetDatabase.GetAssetPath(settings.SharedMaterial) : string.Empty;
            uint bakeHash = ResolveBakeHash(settings);

            try
            {
                vertices = new NativeList<EquipmentPropVertex1715>(EstimateVertexCapacity(settings), Allocator.TempJob);
                indices = new NativeList<int>(EstimateIndexCapacity(settings), Allocator.TempJob);

                BuildCockpitEquipmentMesh(settings, vertices, indices, out anchors, out _);
                if (!EquipmentMetadata.ValidateAnchorSet(anchors, out string anchorFailure))
                {
                    result.FailureReason = anchorFailure;
                    return false;
                }

                var wearJob = new BakeVertexChannelsJob1715
                {
                    Vertices = vertices.AsArray(),
                    GlobalQualityWeight = settings.GlobalQualityWeight,
                    Seed = settings.Seed
                };
                wearJob.Run(vertices.Length);

                validation = new NativeArray<TopologyValidationResult1715>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                var validateJob = new TopologyValidationJob1715
                {
                    Vertices = vertices.AsArray(),
                    Indices = indices.AsArray(),
                    Result = validation,
                    MinimumTriangleArea = MinimumTriangleArea
                };
                validateJob.Run();

                TopologyValidationResult1715 validationResult = validation[0];
                if (validationResult.InvalidCount != 0u)
                {
                    result.FailureReason = "EquipmentPropBaker1715 topology validation failed.";
                    return false;
                }

                meshPath = settings.MeshFolder + "/" + settings.OutputName + "_Mesh.asset";
                mesh = CreateOrUpdateMeshAsset(meshPath, vertices.AsArray(), indices.AsArray(), out Bounds bounds);
                if (!IsFiniteNonZero(bounds))
                {
                    result.FailureReason = "EquipmentPropBaker1715 bounds validation failed.";
                    return false;
                }

                prefabPath = CreatePrefabAsset(settings, mesh, anchors, bakeHash, out int colliderProxyCount, out bool interactableLayerResolved);

                stopwatch.Stop();
                result.Success = true;
                result.MeshPath = meshPath;
                result.PrefabPath = prefabPath;
                result.VertexCount = vertices.Length;
                result.TriangleCount = indices.Length / 3;
                result.AnchorCount = anchors.Length;
                result.ColliderProxyCount = colliderProxyCount;
                result.MaterialPath = materialPath;
                result.Bounds = bounds;
                result.BakeMicros = TicksToMicros(stopwatch.ElapsedTicks);
                result.InteractableLayerResolved = interactableLayerResolved;
                return true;
            }
            catch (Exception exception)
            {
                result.FailureReason = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
            finally
            {
                if (validation.IsCreated)
                    validation.Dispose();
                if (indices.IsCreated)
                    indices.Dispose();
                if (vertices.IsCreated)
                    vertices.Dispose();
            }
        }

        private static void BuildCockpitEquipmentMesh(
            EquipmentBakeSettings1715 settings,
            NativeList<EquipmentPropVertex1715> vertices,
            NativeList<int> indices,
            out InteractionAnchorData[] anchors,
            out EquipmentBakeMetrics1715 metrics)
        {
            metrics = default;
            float q = math.clamp(settings.GlobalQualityWeight, 0f, 1f);
            ResolveGeometryProfile(q, out int radialSegments, out int torusSegments, out int tubeSegments, out int cableSegments, out int cableRing);

            float baseBevel = math.lerp(0.012f, 0.028f, q);
            AppendBeveledBox(vertices, indices, new float3(0f, 0f, 0f), new float3(0.58f, 0.075f, 0.34f), baseBevel, MaterialBase, PackColor(92, 98, 93, 0));
            AppendRecessedPocket(vertices, indices, new float2(-0.20f, 0.12f), new float2(0.19f, 0.105f), 0.076f, 0.034f, MaterialCavity, PackColor(24, 28, 29, 0));
            AppendScreenPlane(vertices, indices, new float3(-0.20f, 0.043f, 0.12f), new float2(0.155f, 0.075f), PackColor(35, 230, 184, 255));

            AppendRecessedPocket(vertices, indices, new float2(0.205f, 0.115f), new float2(0.09f, 0.08f), 0.076f, 0.028f, MaterialCavity, PackColor(28, 25, 23, 0));
            AppendCsgCircularCounterbore(vertices, indices, new float2(0.185f, -0.135f), 0.038f, 0.086f, 0.079f, 0.026f, radialSegments, MaterialCavity, PackColor(22, 24, 23, 0));
            AppendCsgCircularCounterbore(vertices, indices, new float2(-0.36f, -0.13f), 0.044f, 0.092f, 0.079f, 0.023f, radialSegments, MaterialCavity, PackColor(20, 23, 22, 0));
            AppendCylinder(vertices, indices, new float3(0.205f, 0.045f, 0.115f), new float3(0.205f, 0.103f, 0.115f), 0.066f, radialSegments, MaterialWarning, PackColor(126, 92, 45, 0));

            float3 leverStart = new float3(0.185f, 0.082f, -0.135f);
            float3 leverEnd = new float3(0.285f, 0.21f, -0.19f);
            float3 leverAxis = math.normalize(leverEnd - leverStart);
            AppendCylinder(vertices, indices, leverStart, leverEnd, 0.026f, radialSegments, MaterialGrip, PackColor(82, 76, 66, 0));
            AppendCylinder(vertices, indices, leverEnd - leverAxis * 0.045f, leverEnd + leverAxis * 0.055f, 0.052f, radialSegments, MaterialGrip, PackColor(122, 101, 73, 0));

            float3 wheelCenter = new float3(-0.36f, 0.115f, -0.13f);
            AppendCylinder(vertices, indices, wheelCenter + new float3(0f, -0.035f, 0f), wheelCenter + new float3(0f, 0.035f, 0f), 0.038f, radialSegments, MaterialBase, PackColor(72, 80, 78, 0));
            AppendTorus(vertices, indices, wheelCenter, new float3(0f, 1f, 0f), 0.118f, 0.015f, torusSegments, tubeSegments, MaterialGrip, PackColor(96, 86, 67, 0));
            AppendCylinder(vertices, indices, wheelCenter + new float3(-0.118f, 0f, 0f), wheelCenter + new float3(0.118f, 0f, 0f), 0.013f, radialSegments, MaterialGrip, PackColor(85, 77, 64, 0));
            AppendCylinder(vertices, indices, wheelCenter + new float3(0f, 0f, -0.118f), wheelCenter + new float3(0f, 0f, 0.118f), 0.013f, radialSegments, MaterialGrip, PackColor(85, 77, 64, 0));

            for (int i = 0; i < 5; i++)
            {
                float x = -0.02f + i * 0.055f;
                AppendCylinder(vertices, indices, new float3(x, 0.076f, 0.272f), new float3(x, 0.112f, 0.272f), 0.021f, radialSegments, MaterialScreen, PackColor((byte)(30 + i * 20), 190, 120, 255));
            }

            AppendCatenaryCableBundle(settings, vertices, indices, cableSegments, cableRing, out float minY, out float maxY);
            if (minY < CableMinimumVertexY)
                throw new InvalidOperationException("EquipmentPropBaker1715 cable clearance validation failed.");

            metrics.CatenaryMinY = minY;
            metrics.CatenaryMaxY = maxY;
            metrics.RadialSegments = radialSegments;
            metrics.TorusSegments = torusSegments;
            metrics.CableSegments = cableSegments;
            metrics.CableRingSegments = cableRing;
            metrics.BevelWidthMeters = baseBevel;

            anchors = new InteractionAnchorData[3];
            anchors[0] = CreateAnchor(
                "ANCHOR_LeverGrip_1715",
                leverEnd,
                leverAxis,
                ResolvePerpendicularUp(leverAxis),
                0.076f,
                InteractionAnchorData.FlagActive,
                InteractionAnchorData.HandMaskBoth,
                InteractionAnchorData.SurfaceKindLever);
            anchors[1] = CreateAnchor(
                "ANCHOR_ValveWheelRim_1715",
                wheelCenter + new float3(0.118f, 0f, 0f),
                new float3(1f, 0f, 0f),
                new float3(0f, 1f, 0f),
                0.082f,
                InteractionAnchorData.FlagActive | InteractionAnchorData.FlagTwoHanded,
                InteractionAnchorData.HandMaskBoth,
                InteractionAnchorData.SurfaceKindValve);
            anchors[2] = CreateAnchor(
                "ANCHOR_ToggleBank_1715",
                new float3(0.205f, 0.135f, 0.115f),
                new float3(0f, 1f, 0f),
                new float3(0f, 0f, 1f),
                0.058f,
                InteractionAnchorData.FlagActive,
                InteractionAnchorData.HandMaskBoth,
                InteractionAnchorData.SurfaceKindToggle);
        }

        private static void AppendCatenaryCableBundle(
            EquipmentBakeSettings1715 settings,
            NativeList<EquipmentPropVertex1715> vertices,
            NativeList<int> indices,
            int segments,
            int ringSegments,
            out float minY,
            out float maxY)
        {
            int rings = segments + 1;
            int cableVertexCount = CableStrandCount * rings * ringSegments;
            int cableIndexCount = CableStrandCount * segments * ringSegments * 6;
            var cableVertices = new NativeArray<EquipmentPropVertex1715>(cableVertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var cableIndices = new NativeArray<int>(cableIndexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var cableMetrics = new NativeArray<float2>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            try
            {
                var job = new CatenaryCableExtrusionJob1715
                {
                    Vertices = cableVertices,
                    Indices = cableIndices,
                    Metrics = cableMetrics,
                    Start = new float3(-0.50f, 0.178f, -0.278f),
                    End = new float3(0.48f, 0.176f, -0.274f),
                    Radius = 0.0125f,
                    BundleRadius = 0.024f,
                    SlackMeters = math.lerp(0.035f, 0.075f, settings.GlobalQualityWeight),
                    MinimumCenterY = CableMinimumCenterY,
                    Segments = segments,
                    RingSegments = ringSegments,
                    StrandCount = CableStrandCount
                };
                job.Run();

                int vertexOffset = vertices.Length;
                for (int i = 0; i < cableVertices.Length; i++)
                    vertices.Add(cableVertices[i]);
                for (int i = 0; i < cableIndices.Length; i++)
                    indices.Add(vertexOffset + cableIndices[i]);

                float2 range = cableMetrics[0];
                minY = range.x;
                maxY = range.y;
            }
            finally
            {
                if (cableMetrics.IsCreated)
                    cableMetrics.Dispose();
                if (cableIndices.IsCreated)
                    cableIndices.Dispose();
                if (cableVertices.IsCreated)
                    cableVertices.Dispose();
            }
        }

        private static void AppendBeveledBox(
            NativeList<EquipmentPropVertex1715> vertices,
            NativeList<int> indices,
            float3 center,
            float3 half,
            float bevel,
            uint materialId,
            uint color)
        {
            float b = math.clamp(bevel, 0.001f, math.cmin(half) * 0.45f);
            float x0 = center.x - half.x;
            float x1 = center.x + half.x;
            float y0 = center.y - half.y;
            float y1 = center.y + half.y;
            float z0 = center.z - half.z;
            float z1 = center.z + half.z;
            float xi0 = x0 + b;
            float xi1 = x1 - b;
            float yi0 = y0 + b;
            float yi1 = y1 - b;
            float zi0 = z0 + b;
            float zi1 = z1 - b;

            AppendQuad(vertices, indices, new float3(xi0, y1, zi0), new float3(xi1, y1, zi0), new float3(xi1, y1, zi1), new float3(xi0, y1, zi1), new float3(0f, 1f, 0f), materialId, color, new float2(0f, 0f), new float2(1f, 1f));
            AppendQuad(vertices, indices, new float3(xi0, y0, zi1), new float3(xi1, y0, zi1), new float3(xi1, y0, zi0), new float3(xi0, y0, zi0), new float3(0f, -1f, 0f), materialId, color, new float2(0f, 0f), new float2(1f, 1f));
            AppendQuad(vertices, indices, new float3(xi0, yi0, z1), new float3(xi1, yi0, z1), new float3(xi1, yi1, z1), new float3(xi0, yi1, z1), new float3(0f, 0f, 1f), materialId, color, new float2(0f, 0f), new float2(1f, 1f));
            AppendQuad(vertices, indices, new float3(xi1, yi0, z0), new float3(xi0, yi0, z0), new float3(xi0, yi1, z0), new float3(xi1, yi1, z0), new float3(0f, 0f, -1f), materialId, color, new float2(0f, 0f), new float2(1f, 1f));
            AppendQuad(vertices, indices, new float3(x0, yi0, zi0), new float3(x0, yi0, zi1), new float3(x0, yi1, zi1), new float3(x0, yi1, zi0), new float3(-1f, 0f, 0f), materialId, color, new float2(0f, 0f), new float2(1f, 1f));
            AppendQuad(vertices, indices, new float3(x1, yi0, zi1), new float3(x1, yi0, zi0), new float3(x1, yi1, zi0), new float3(x1, yi1, zi1), new float3(1f, 0f, 0f), materialId, color, new float2(0f, 0f), new float2(1f, 1f));

            AppendQuad(vertices, indices, new float3(xi0, y1, zi1), new float3(xi1, y1, zi1), new float3(xi1, yi1, z1), new float3(xi0, yi1, z1), math.normalize(new float3(0f, 1f, 1f)), materialId, color, new float2(0f, 0f), new float2(1f, 1f));
            AppendQuad(vertices, indices, new float3(xi1, y1, zi0), new float3(xi0, y1, zi0), new float3(xi0, yi1, z0), new float3(xi1, yi1, z0), math.normalize(new float3(0f, 1f, -1f)), materialId, color, new float2(0f, 0f), new float2(1f, 1f));
            AppendQuad(vertices, indices, new float3(xi0, y0, zi0), new float3(xi1, y0, zi0), new float3(xi1, yi0, z0), new float3(xi0, yi0, z0), math.normalize(new float3(0f, -1f, -1f)), materialId, color, new float2(0f, 0f), new float2(1f, 1f));
            AppendQuad(vertices, indices, new float3(xi1, y0, zi1), new float3(xi0, y0, zi1), new float3(xi0, yi0, z1), new float3(xi1, yi0, z1), math.normalize(new float3(0f, -1f, 1f)), materialId, color, new float2(0f, 0f), new float2(1f, 1f));

            AppendQuad(vertices, indices, new float3(xi0, y1, zi0), new float3(xi0, y1, zi1), new float3(x0, yi1, zi1), new float3(x0, yi1, zi0), math.normalize(new float3(-1f, 1f, 0f)), materialId, color, new float2(0f, 0f), new float2(1f, 1f));
            AppendQuad(vertices, indices, new float3(xi1, y1, zi1), new float3(xi1, y1, zi0), new float3(x1, yi1, zi0), new float3(x1, yi1, zi1), math.normalize(new float3(1f, 1f, 0f)), materialId, color, new float2(0f, 0f), new float2(1f, 1f));
            AppendQuad(vertices, indices, new float3(xi0, y0, zi1), new float3(xi0, y0, zi0), new float3(x0, yi0, zi0), new float3(x0, yi0, zi1), math.normalize(new float3(-1f, -1f, 0f)), materialId, color, new float2(0f, 0f), new float2(1f, 1f));
            AppendQuad(vertices, indices, new float3(xi1, y0, zi0), new float3(xi1, y0, zi1), new float3(x1, yi0, zi1), new float3(x1, yi0, zi0), math.normalize(new float3(1f, -1f, 0f)), materialId, color, new float2(0f, 0f), new float2(1f, 1f));

            AppendQuad(vertices, indices, new float3(x0, yi1, zi1), new float3(x1, yi1, zi1), new float3(x1, yi0, z1), new float3(x0, yi0, z1), math.normalize(new float3(0f, 0f, 1f)), materialId, color, new float2(0f, 0f), new float2(1f, 1f));
            AppendQuad(vertices, indices, new float3(x1, yi1, zi0), new float3(x0, yi1, zi0), new float3(x0, yi0, z0), new float3(x1, yi0, z0), math.normalize(new float3(0f, 0f, -1f)), materialId, color, new float2(0f, 0f), new float2(1f, 1f));

            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                float3 normal = math.normalize(new float3(sx, sy, sz));
                float3 corner = center + half * new float3(sx, sy, sz);
                float3 px = corner - new float3(sx * b, 0f, sz * b);
                float3 py = corner - new float3(sx * b, sy * b, 0f);
                float3 pz = corner - new float3(0f, sy * b, sz * b);
                AppendTriangle(vertices, indices, px, py, pz, normal, materialId, color, new float2(0f, 0f), new float2(1f, 0f), new float2(0.5f, 1f));
            }
        }

        private static void AppendRecessedPocket(
            NativeList<EquipmentPropVertex1715> vertices,
            NativeList<int> indices,
            float2 centerXZ,
            float2 halfXZ,
            float topY,
            float depth,
            uint materialId,
            uint color)
        {
            float x0 = centerXZ.x - halfXZ.x;
            float x1 = centerXZ.x + halfXZ.x;
            float z0 = centerXZ.y - halfXZ.y;
            float z1 = centerXZ.y + halfXZ.y;
            float bottomY = topY - depth;
            uint lipColor = PackColor(50, 55, 54, 0);
            AppendQuad(vertices, indices, new float3(x0, bottomY, z0), new float3(x1, bottomY, z0), new float3(x1, bottomY, z1), new float3(x0, bottomY, z1), new float3(0f, 1f, 0f), materialId, color, new float2(0.05f, 0.05f), new float2(0.95f, 0.95f));
            AppendQuad(vertices, indices, new float3(x0, topY, z0), new float3(x1, topY, z0), new float3(x1, bottomY, z0), new float3(x0, bottomY, z0), new float3(0f, 0f, -1f), MaterialCavity, lipColor, new float2(0f, 0f), new float2(1f, 1f));
            AppendQuad(vertices, indices, new float3(x1, topY, z1), new float3(x0, topY, z1), new float3(x0, bottomY, z1), new float3(x1, bottomY, z1), new float3(0f, 0f, 1f), MaterialCavity, lipColor, new float2(0f, 0f), new float2(1f, 1f));
            AppendQuad(vertices, indices, new float3(x0, topY, z1), new float3(x0, topY, z0), new float3(x0, bottomY, z0), new float3(x0, bottomY, z1), new float3(-1f, 0f, 0f), MaterialCavity, lipColor, new float2(0f, 0f), new float2(1f, 1f));
            AppendQuad(vertices, indices, new float3(x1, topY, z0), new float3(x1, topY, z1), new float3(x1, bottomY, z1), new float3(x1, bottomY, z0), new float3(1f, 0f, 0f), MaterialCavity, lipColor, new float2(0f, 0f), new float2(1f, 1f));
        }

        private static void AppendCsgCircularCounterbore(
            NativeList<EquipmentPropVertex1715> vertices,
            NativeList<int> indices,
            float2 centerXZ,
            float innerRadius,
            float outerRadius,
            float topY,
            float depth,
            int segments,
            uint materialId,
            uint color)
        {
            if (segments < 12)
                segments = 12;

            float safeInner = math.max(0.004f, innerRadius);
            float safeOuter = math.max(safeInner + 0.008f, outerRadius);
            float bottomY = topY - math.max(0.002f, depth);
            uint wallColor = PackColor(42, 47, 45, 0);
            for (int i = 0; i < segments; i++)
            {
                int next = i + 1;
                if (next == segments)
                    next = 0;

                float a0 = (math.PI * 2f * i) / segments;
                float a1 = (math.PI * 2f * next) / segments;
                float3 radial0 = new float3(math.cos(a0), 0f, math.sin(a0));
                float3 radial1 = new float3(math.cos(a1), 0f, math.sin(a1));
                float3 sideNormal = math.normalizesafe(radial0 + radial1, radial0);
                float3 center = new float3(centerXZ.x, 0f, centerXZ.y);

                float3 outerTop0 = center + radial0 * safeOuter + new float3(0f, topY, 0f);
                float3 outerTop1 = center + radial1 * safeOuter + new float3(0f, topY, 0f);
                float3 outerBottom0 = center + radial0 * safeOuter + new float3(0f, bottomY, 0f);
                float3 outerBottom1 = center + radial1 * safeOuter + new float3(0f, bottomY, 0f);
                float3 innerTop0 = center + radial0 * safeInner + new float3(0f, topY, 0f);
                float3 innerTop1 = center + radial1 * safeInner + new float3(0f, topY, 0f);
                float3 innerBottom0 = center + radial0 * safeInner + new float3(0f, bottomY, 0f);
                float3 innerBottom1 = center + radial1 * safeInner + new float3(0f, bottomY, 0f);

                AppendQuad(vertices, indices, outerTop1, outerTop0, outerBottom0, outerBottom1, -sideNormal, MaterialCavity, wallColor, new float2(0f, 0f), new float2(1f, 1f));
                AppendQuad(vertices, indices, innerTop0, innerTop1, innerBottom1, innerBottom0, sideNormal, MaterialCavity, wallColor, new float2(0f, 0f), new float2(1f, 1f));
                AppendQuad(vertices, indices, innerBottom0, innerBottom1, outerBottom1, outerBottom0, new float3(0f, 1f, 0f), materialId, color, new float2(0.12f, 0.12f), new float2(0.88f, 0.88f));
            }
        }

        private static void AppendScreenPlane(
            NativeList<EquipmentPropVertex1715> vertices,
            NativeList<int> indices,
            float3 center,
            float2 half,
            uint color)
        {
            AppendQuad(
                vertices,
                indices,
                new float3(center.x - half.x, center.y, center.z - half.y),
                new float3(center.x + half.x, center.y, center.z - half.y),
                new float3(center.x + half.x, center.y, center.z + half.y),
                new float3(center.x - half.x, center.y, center.z + half.y),
                new float3(0f, 1f, 0f),
                MaterialScreen,
                color,
                new float2(0.06f, 0.68f),
                new float2(0.31f, 0.94f));
        }

        private static void AppendCylinder(
            NativeList<EquipmentPropVertex1715> vertices,
            NativeList<int> indices,
            float3 start,
            float3 end,
            float radius,
            int segments,
            uint materialId,
            uint color)
        {
            if (segments < 6)
                segments = 6;
            radius = math.max(radius, 0.004f);
            float3 axis = end - start;
            float lengthSq = math.lengthsq(axis);
            if (lengthSq < 0.000001f)
                axis = new float3(0f, 1f, 0f);
            float3 tangent = math.normalize(axis);
            ResolveBasis(tangent, out float3 right, out float3 up);
            int baseIndex = vertices.Length;
            for (int i = 0; i < segments; i++)
            {
                float a = (math.PI * 2f * i) / segments;
                float s = math.sin(a);
                float c = math.cos(a);
                float3 normal = right * c + up * s;
                float2 uv0 = new float2(i / (float)segments, 0f);
                float2 uv1 = new float2(i / (float)segments, 1f);
                vertices.Add(CreateVertex(start + normal * radius, normal, tangent, uv0, color, materialId));
                vertices.Add(CreateVertex(end + normal * radius, normal, tangent, uv1, color, materialId));
            }

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                AppendIndexedQuad(indices, baseIndex + i * 2, baseIndex + next * 2, baseIndex + next * 2 + 1, baseIndex + i * 2 + 1);
            }

            int startCenter = vertices.Length;
            vertices.Add(CreateVertex(start, -tangent, right, new float2(0.5f, 0.5f), color, materialId));
            int endCenter = vertices.Length;
            vertices.Add(CreateVertex(end, tangent, right, new float2(0.5f, 0.5f), color, materialId));
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                AppendIndexedTriangle(indices, startCenter, baseIndex + next * 2, baseIndex + i * 2);
                AppendIndexedTriangle(indices, endCenter, baseIndex + i * 2 + 1, baseIndex + next * 2 + 1);
            }
        }

        private static void AppendTorus(
            NativeList<EquipmentPropVertex1715> vertices,
            NativeList<int> indices,
            float3 center,
            float3 axis,
            float majorRadius,
            float tubeRadius,
            int majorSegments,
            int tubeSegments,
            uint materialId,
            uint color)
        {
            if (majorSegments < 8)
                majorSegments = 8;
            if (tubeSegments < 6)
                tubeSegments = 6;
            float3 normalAxis = math.normalizesafe(axis, new float3(0f, 1f, 0f));
            ResolveBasis(normalAxis, out float3 right, out float3 forward);
            int baseIndex = vertices.Length;
            for (int i = 0; i < majorSegments; i++)
            {
                float majorAngle = (math.PI * 2f * i) / majorSegments;
                float3 radial = right * math.cos(majorAngle) + forward * math.sin(majorAngle);
                float3 ringCenter = center + radial * majorRadius;
                for (int j = 0; j < tubeSegments; j++)
                {
                    float tubeAngle = (math.PI * 2f * j) / tubeSegments;
                    float3 tubeNormal = radial * math.cos(tubeAngle) + normalAxis * math.sin(tubeAngle);
                    float3 tangent = math.normalize(math.cross(normalAxis, radial));
                    float2 uv = new float2(i / (float)majorSegments, j / (float)tubeSegments);
                    vertices.Add(CreateVertex(ringCenter + tubeNormal * tubeRadius, tubeNormal, tangent, uv, color, materialId));
                }
            }

            for (int i = 0; i < majorSegments; i++)
            {
                int nextI = (i + 1) % majorSegments;
                for (int j = 0; j < tubeSegments; j++)
                {
                    int nextJ = (j + 1) % tubeSegments;
                    AppendIndexedQuad(
                        indices,
                        baseIndex + i * tubeSegments + j,
                        baseIndex + nextI * tubeSegments + j,
                        baseIndex + nextI * tubeSegments + nextJ,
                        baseIndex + i * tubeSegments + nextJ);
                }
            }
        }

        private static void AppendQuad(
            NativeList<EquipmentPropVertex1715> vertices,
            NativeList<int> indices,
            float3 a,
            float3 b,
            float3 c,
            float3 d,
            float3 normal,
            uint materialId,
            uint color,
            float2 uvMin,
            float2 uvMax)
        {
            normal = math.normalizesafe(normal, new float3(0f, 1f, 0f));
            float3 tangent = ResolveTangent(normal);
            int start = vertices.Length;
            vertices.Add(CreateVertex(a, normal, tangent, new float2(uvMin.x, uvMin.y), color, materialId));
            vertices.Add(CreateVertex(b, normal, tangent, new float2(uvMax.x, uvMin.y), color, materialId));
            vertices.Add(CreateVertex(c, normal, tangent, new float2(uvMax.x, uvMax.y), color, materialId));
            vertices.Add(CreateVertex(d, normal, tangent, new float2(uvMin.x, uvMax.y), color, materialId));

            if (math.dot(math.cross(b - a, c - a), normal) >= 0f)
                AppendIndexedQuad(indices, start, start + 1, start + 2, start + 3);
            else
                AppendIndexedQuad(indices, start, start + 3, start + 2, start + 1);
        }

        private static void AppendTriangle(
            NativeList<EquipmentPropVertex1715> vertices,
            NativeList<int> indices,
            float3 a,
            float3 b,
            float3 c,
            float3 normal,
            uint materialId,
            uint color,
            float2 uvA,
            float2 uvB,
            float2 uvC)
        {
            normal = math.normalizesafe(normal, new float3(0f, 1f, 0f));
            float3 tangent = ResolveTangent(normal);
            int start = vertices.Length;
            vertices.Add(CreateVertex(a, normal, tangent, uvA, color, materialId));
            vertices.Add(CreateVertex(b, normal, tangent, uvB, color, materialId));
            vertices.Add(CreateVertex(c, normal, tangent, uvC, color, materialId));
            if (math.dot(math.cross(b - a, c - a), normal) >= 0f)
                AppendIndexedTriangle(indices, start, start + 1, start + 2);
            else
                AppendIndexedTriangle(indices, start, start + 2, start + 1);
        }

        private static void AppendIndexedQuad(NativeList<int> indices, int a, int b, int c, int d)
        {
            indices.Add(a);
            indices.Add(b);
            indices.Add(c);
            indices.Add(a);
            indices.Add(c);
            indices.Add(d);
        }

        private static void AppendIndexedTriangle(NativeList<int> indices, int a, int b, int c)
        {
            indices.Add(a);
            indices.Add(b);
            indices.Add(c);
        }

        private static EquipmentPropVertex1715 CreateVertex(float3 position, float3 normal, float3 tangent, float2 uv, uint color, uint materialId)
        {
            return new EquipmentPropVertex1715
            {
                Position = position,
                Normal = math.normalizesafe(normal, new float3(0f, 1f, 0f)),
                Tangent = new float4(math.normalizesafe(tangent, new float3(1f, 0f, 0f)), 1f),
                Uv0 = uv,
                ColorRgba = color,
                MaterialId = materialId
            };
        }

        private static InteractionAnchorData CreateAnchor(string key, float3 localPosition, float3 forward, float3 up, float snapRadius, uint flags, byte handMask, byte surfaceKind)
        {
            return new InteractionAnchorData
            {
                LocalPosition = localPosition,
                LocalForward = math.normalizesafe(forward, new float3(0f, 0f, 1f)),
                LocalUp = math.normalizesafe(up, new float3(0f, 1f, 0f)),
                SnapRadiusMeters = snapRadius,
                AnchorId = HashString(key),
                Flags = flags,
                HandMask = handMask,
                SurfaceKind = surfaceKind
            };
        }

        private static Mesh CreateOrUpdateMeshAsset(
            string meshPath,
            NativeArray<EquipmentPropVertex1715> vertices,
            NativeArray<int> indices,
            out Bounds bounds)
        {
            bounds = default;
            Mesh mesh = null;
            Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
            bool appliedMeshData = false;

            try
            {
                mesh = new Mesh
                {
                    name = Path.GetFileNameWithoutExtension(meshPath),
                    indexFormat = IndexFormat.UInt32
                };

                Mesh.MeshData meshData = meshDataArray[0];
                meshData.SetVertexBufferParams(vertices.Length, EquipmentPropVertex1715.Layout);
                meshData.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);

                NativeArray<EquipmentPropVertex1715> vertexData = meshData.GetVertexData<EquipmentPropVertex1715>();
                NativeArray<uint> indexData = meshData.GetIndexData<uint>();
                vertexData.CopyFrom(vertices);
                for (int i = 0; i < indices.Length; i++)
                    indexData[i] = (uint)indices[i];

                var descriptor = new SubMeshDescriptor(0, indices.Length, MeshTopology.Triangles)
                {
                    bounds = EstimateBounds(vertices),
                    vertexCount = vertices.Length
                };
                meshData.subMeshCount = 1;
                meshData.SetSubMesh(0, descriptor, MeshUpdateFlags.DontRecalculateBounds);
                Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh, MeshUpdateFlags.DontRecalculateBounds);
                appliedMeshData = true;
                mesh.RecalculateBounds();
                bounds = mesh.bounds;

                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (existing != null)
                {
                    EditorUtility.CopySerialized(mesh, existing);
                    existing.name = Path.GetFileNameWithoutExtension(meshPath);
                    EditorUtility.SetDirty(existing);
                    UnityEngine.Object.DestroyImmediate(mesh);
                    mesh = null;
                    AssetDatabase.SaveAssets();
                    return existing;
                }

                AssetDatabase.CreateAsset(mesh, meshPath);
                Mesh created = mesh;
                mesh = null;
                AssetDatabase.SaveAssets();
                return created;
            }
            catch
            {
                if (!appliedMeshData)
                    meshDataArray.Dispose();

                if (mesh != null)
                    UnityEngine.Object.DestroyImmediate(mesh);

                throw;
            }
        }

        private static string CreatePrefabAsset(
            EquipmentBakeSettings1715 settings,
            Mesh mesh,
            InteractionAnchorData[] anchors,
            uint bakeHash,
            out int colliderProxyCount,
            out bool interactableLayerResolved)
        {
            GameObject root = new GameObject(settings.OutputName);
            colliderProxyCount = 0;
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            interactableLayerResolved = interactableLayer >= 0;
            if (!interactableLayerResolved)
                interactableLayer = 0;

            try
            {
                root.layer = interactableLayer;
                root.isStatic = true;

                var filter = root.AddComponent<MeshFilter>();
                var renderer = root.AddComponent<MeshRenderer>();
                filter.sharedMesh = mesh;
                renderer.sharedMaterial = settings.SharedMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;

                var metadata = root.AddComponent<EquipmentMetadata>();
                metadata.SetEditorBakeData(EquipmentId, bakeHash, settings.GlobalQualityWeight, anchors);

                AddBoxColliderProxy(root.transform, "COL_BaseProxy_1715", interactableLayer, new float3(0f, 0f, 0f), new float3(1.14f, 0.14f, 0.66f));
                colliderProxyCount++;
                AddCapsuleColliderProxy(root.transform, "COL_LeverProxy_1715", interactableLayer, new float3(0.185f, 0.082f, -0.135f), new float3(0.285f, 0.21f, -0.19f), 0.055f);
                colliderProxyCount++;
                AddSphereColliderProxy(root.transform, "COL_ValveProxy_1715", interactableLayer, new float3(-0.36f, 0.115f, -0.13f), 0.135f);
                colliderProxyCount++;

                string prefabPath = settings.OutputFolder + "/" + settings.OutputName + ".prefab";
                if (!ColliderOptimizerEngine1716.ValidatePrefabColliderBudget(root, out string colliderFailure))
                    throw new InvalidOperationException("1716 collider validation failed before save: " + colliderFailure);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool success);
                if (!success)
                    throw new InvalidOperationException("Prefab serialization failed: " + prefabPath);

                if (!ColliderOptimizerEngine1716.ValidatePrefabAssetTopology(prefabPath, out colliderFailure))
                    throw new InvalidOperationException("1716 collider validation failed after save: " + colliderFailure);

                return prefabPath;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void AddBoxColliderProxy(Transform root, string name, int layer, float3 center, float3 size)
        {
            GameObject proxy = new GameObject(name);
            proxy.layer = layer;
            proxy.transform.SetParent(root, false);
            proxy.transform.localPosition = ToVector3(center);
            var collider = proxy.AddComponent<BoxCollider>();
            collider.size = ToVector3(size);
        }

        private static void AddSphereColliderProxy(Transform root, string name, int layer, float3 center, float radius)
        {
            GameObject proxy = new GameObject(name);
            proxy.layer = layer;
            proxy.transform.SetParent(root, false);
            proxy.transform.localPosition = ToVector3(center);
            var collider = proxy.AddComponent<SphereCollider>();
            collider.radius = radius;
        }

        private static void AddCapsuleColliderProxy(Transform root, string name, int layer, float3 start, float3 end, float radius)
        {
            GameObject proxy = new GameObject(name);
            proxy.layer = layer;
            proxy.transform.SetParent(root, false);
            float3 axis = end - start;
            float length = math.length(axis);
            float3 center = (start + end) * 0.5f;
            proxy.transform.localPosition = ToVector3(center);
            proxy.transform.localRotation = Quaternion.FromToRotation(Vector3.forward, ToVector3(math.normalizesafe(axis, new float3(0f, 0f, 1f))));
            var collider = proxy.AddComponent<CapsuleCollider>();
            collider.direction = 2;
            collider.radius = radius;
            collider.height = math.max(radius * 2f, length + radius * 2f);
        }

        private static EquipmentBakeSettings1715 Sanitize(EquipmentBakeSettings1715 settings)
        {
            settings.OutputFolder = SanitizeAssetFolder(settings.OutputFolder, DefaultOutputFolder);
            settings.MeshFolder = SanitizeAssetFolder(settings.MeshFolder, DefaultMeshFolder);
            settings.OutputName = SanitizeAssetName(settings.OutputName, DefaultOutputName);
            settings.Seed = settings.Seed == 0u ? 1u : settings.Seed;
            settings.GlobalQualityWeight = math.clamp(settings.GlobalQualityWeight, 0f, 1f);
            if (settings.SharedMaterial == null)
                settings.SharedMaterial = ResolveProjectMaterial();
            return settings;
        }

        private static void ResolveGeometryProfile(
            float qualityWeight,
            out int radialSegments,
            out int torusSegments,
            out int tubeSegments,
            out int cableSegments,
            out int cableRingSegments)
        {
            float q = math.clamp(qualityWeight, 0f, 1f);
            radialSegments = (int)math.clamp(math.round(math.lerp(12f, 24f, q)), 12f, 24f);
            torusSegments = (int)math.clamp(math.round(math.lerp(14f, 26f, q)), 14f, 26f);
            tubeSegments = (int)math.clamp(math.round(math.lerp(6f, 10f, q)), 6f, 10f);
            cableSegments = (int)math.clamp(math.round(math.lerp(8f, 22f, q)), 8f, 22f);
            cableRingSegments = (int)math.clamp(math.round(math.lerp(6f, 8f, q)), 6f, 8f);
        }

        private static int EstimateVertexCapacity(EquipmentBakeSettings1715 settings)
        {
            ResolveGeometryProfile(
                settings.GlobalQualityWeight,
                out int radialSegments,
                out int torusSegments,
                out int tubeSegments,
                out int cableSegments,
                out int cableRingSegments);

            int beveledBoxVertices = 88;
            int pocketVertices = RecessedPocketCount * 20;
            int circularCutVertices = CircularCsgCutCount * radialSegments * 12;
            int screenVertices = 4;
            int cylinderVertices = CylinderPrimitiveCount * ((radialSegments * 2) + 2);
            int torusVertices = torusSegments * tubeSegments;
            int cableVertices = CableStrandCount * (cableSegments + 1) * cableRingSegments;
            int capacity = beveledBoxVertices + pocketVertices + circularCutVertices + screenVertices + cylinderVertices + torusVertices + cableVertices + 128;
            return capacity < 512 ? 512 : capacity;
        }

        private static int EstimateIndexCapacity(EquipmentBakeSettings1715 settings)
        {
            ResolveGeometryProfile(
                settings.GlobalQualityWeight,
                out int radialSegments,
                out int torusSegments,
                out int tubeSegments,
                out int cableSegments,
                out int cableRingSegments);

            int beveledBoxIndices = 120;
            int pocketIndices = RecessedPocketCount * 30;
            int circularCutIndices = CircularCsgCutCount * radialSegments * 18;
            int screenIndices = 6;
            int cylinderIndices = CylinderPrimitiveCount * radialSegments * 12;
            int torusIndices = torusSegments * tubeSegments * 6;
            int cableIndices = CableStrandCount * cableSegments * cableRingSegments * 6;
            int capacity = beveledBoxIndices + pocketIndices + circularCutIndices + screenIndices + cylinderIndices + torusIndices + cableIndices + 256;
            return capacity < 1024 ? 1024 : capacity;
        }

        private static Material ResolveProjectMaterial()
        {
            if (s_cachedProjectMaterial != null)
                return s_cachedProjectMaterial;

            Material material = AssetDatabase.LoadAssetAtPath<Material>(PreferredPressureMetalMaterialPath);
            if (material != null)
                return CacheProjectMaterial(material);

            material = AssetDatabase.LoadAssetAtPath<Material>(PreferredInstrumentMaterialPath);
            if (material != null)
                return CacheProjectMaterial(material);

            material = AssetDatabase.LoadAssetAtPath<Material>(PreferredHullMaterialPath);
            if (material != null)
                return CacheProjectMaterial(material);

            material = AssetDatabase.LoadAssetAtPath<Material>(PreferredFallbackMaterialPath);
            if (material != null)
                return CacheProjectMaterial(material);

            string[] guids = AssetDatabase.FindAssets("t:Material", ProjectMaterialSearchFolders);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null)
                    return CacheProjectMaterial(material);
            }

            return CacheProjectMaterial(AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat"));
        }

        private static Material CacheProjectMaterial(Material material)
        {
            s_cachedProjectMaterial = material;
            return material;
        }

        private static string SanitizeAssetFolder(string folder, string fallback)
        {
            string safe = string.IsNullOrWhiteSpace(folder) ? fallback : folder.Replace('\\', '/').TrimEnd('/');
            return safe.StartsWith("Assets/", StringComparison.Ordinal) ? safe : fallback;
        }

        private static string SanitizeAssetName(string name, string fallback)
        {
            if (string.IsNullOrWhiteSpace(name))
                return fallback;

            var builder = new StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                builder.Append(Array.IndexOf(InvalidFileNameChars, c) >= 0 ? '_' : c);
            }

            return builder.Length == 0 ? fallback : builder.ToString();
        }

        private static void EnsureAssetFolder(string folder)
        {
            string safe = folder.Replace('\\', '/').Trim('/');
            if (AssetDatabase.IsValidFolder(safe))
                return;

            string[] parts = safe.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static Bounds EstimateBounds(NativeArray<EquipmentPropVertex1715> vertices)
        {
            if (vertices.Length == 0)
                return new Bounds(Vector3.zero, Vector3.one);

            float3 min = vertices[0].Position;
            float3 max = min;
            for (int i = 1; i < vertices.Length; i++)
            {
                float3 p = vertices[i].Position;
                min = math.min(min, p);
                max = math.max(max, p);
            }

            float3 center = (min + max) * 0.5f;
            float3 size = math.max(max - min, new float3(0.001f));
            return new Bounds(ToVector3(center), ToVector3(size));
        }

        private static bool IsFiniteNonZero(Bounds bounds)
        {
            Vector3 center = bounds.center;
            Vector3 size = bounds.size;
            return IsFinite(center.x) && IsFinite(center.y) && IsFinite(center.z) &&
                   IsFinite(size.x) && IsFinite(size.y) && IsFinite(size.z) &&
                   size.x > 0.001f && size.y > 0.001f && size.z > 0.001f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void ResolveBasis(float3 normal, out float3 right, out float3 up)
        {
            float3 helper = math.abs(normal.y) > 0.92f ? new float3(1f, 0f, 0f) : new float3(0f, 1f, 0f);
            right = math.normalizesafe(math.cross(helper, normal), new float3(1f, 0f, 0f));
            up = math.normalizesafe(math.cross(normal, right), new float3(0f, 1f, 0f));
        }

        private static float3 ResolveTangent(float3 normal)
        {
            ResolveBasis(normal, out float3 right, out _);
            return right;
        }

        private static float3 ResolvePerpendicularUp(float3 forward)
        {
            ResolveBasis(math.normalizesafe(forward, new float3(0f, 0f, 1f)), out _, out float3 up);
            return up;
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static uint PackColor(byte r, byte g, byte b, byte a)
        {
            return (uint)(r | (g << 8) | (b << 16) | (a << 24));
        }

        private static uint HashString(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }

                return hash;
            }
        }

        private static uint ResolveBakeHash(EquipmentBakeSettings1715 settings)
        {
            unchecked
            {
                uint hash = HashString(settings.OutputName);
                hash ^= settings.Seed + 0x9E3779B9u + (hash << 6) + (hash >> 2);

                uint qualityQ16 = (uint)math.round(math.clamp(settings.GlobalQualityWeight, 0f, 1f) * 65535f);
                hash ^= qualityQ16 + 0x9E3779B9u + (hash << 6) + (hash >> 2);

                string materialPath = settings.SharedMaterial != null
                    ? AssetDatabase.GetAssetPath(settings.SharedMaterial)
                    : string.Empty;
                hash ^= HashString(materialPath) + 0x9E3779B9u + (hash << 6) + (hash >> 2);
                return hash;
            }
        }

        private static long TicksToMicros(long ticks)
        {
            return (ticks * 1000000L) / Stopwatch.Frequency;
        }

        private static bool ValidateStaticContracts(out string failureReason)
        {
            int vertexSize = UnsafeUtility.SizeOf<EquipmentPropVertex1715>();
            int metricsSize = UnsafeUtility.SizeOf<EquipmentBakeMetrics1715>();
            int topologySize = UnsafeUtility.SizeOf<TopologyValidationResult1715>();

            if (!EquipmentMetadata.ValidateStaticLayout())
            {
                failureReason = "InteractionAnchorData layout invalid.";
                return false;
            }

            if (!IsExpectedEightByteSize(vertexSize, EquipmentPropVertexSizeBytes) ||
                OffsetOf<EquipmentPropVertex1715>(nameof(EquipmentPropVertex1715.Position)) != 0 ||
                OffsetOf<EquipmentPropVertex1715>(nameof(EquipmentPropVertex1715.Normal)) != 12 ||
                OffsetOf<EquipmentPropVertex1715>(nameof(EquipmentPropVertex1715.Tangent)) != 24 ||
                OffsetOf<EquipmentPropVertex1715>(nameof(EquipmentPropVertex1715.Uv0)) != 40 ||
                OffsetOf<EquipmentPropVertex1715>(nameof(EquipmentPropVertex1715.ColorRgba)) != 48 ||
                OffsetOf<EquipmentPropVertex1715>(nameof(EquipmentPropVertex1715.MaterialId)) != 52 ||
                EquipmentPropVertex1715.Layout.Length != 6)
            {
                failureReason = "EquipmentPropVertex1715 layout invalid.";
                return false;
            }

            if (!IsExpectedEightByteSize(metricsSize, EquipmentBakeMetricsSizeBytes) ||
                OffsetOf<EquipmentBakeMetrics1715>(nameof(EquipmentBakeMetrics1715.RadialSegments)) != 0 ||
                OffsetOf<EquipmentBakeMetrics1715>(nameof(EquipmentBakeMetrics1715.TorusSegments)) != 4 ||
                OffsetOf<EquipmentBakeMetrics1715>(nameof(EquipmentBakeMetrics1715.CableSegments)) != 8 ||
                OffsetOf<EquipmentBakeMetrics1715>(nameof(EquipmentBakeMetrics1715.CableRingSegments)) != 12 ||
                OffsetOf<EquipmentBakeMetrics1715>(nameof(EquipmentBakeMetrics1715.BevelWidthMeters)) != 16 ||
                OffsetOf<EquipmentBakeMetrics1715>(nameof(EquipmentBakeMetrics1715.CatenaryMinY)) != 20 ||
                OffsetOf<EquipmentBakeMetrics1715>(nameof(EquipmentBakeMetrics1715.CatenaryMaxY)) != 24)
            {
                failureReason = "EquipmentBakeMetrics1715 layout invalid.";
                return false;
            }

            if (!IsExpectedEightByteSize(topologySize, TopologyValidationResultSizeBytes) ||
                OffsetOf<TopologyValidationResult1715>(nameof(TopologyValidationResult1715.InvalidCount)) != 0 ||
                OffsetOf<TopologyValidationResult1715>(nameof(TopologyValidationResult1715.FirstFailureCode)) != 4 ||
                OffsetOf<TopologyValidationResult1715>(nameof(TopologyValidationResult1715.FirstTriangle)) != 8 ||
                OffsetOf<TopologyValidationResult1715>(nameof(TopologyValidationResult1715.MinimumAreaSeen)) != 12 ||
                OffsetOf<TopologyValidationResult1715>(nameof(TopologyValidationResult1715.MaximumNormalError)) != 16 ||
                OffsetOf<TopologyValidationResult1715>(nameof(TopologyValidationResult1715.Marker)) != 20)
            {
                failureReason = "TopologyValidationResult1715 layout invalid.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        private static bool IsExpectedEightByteSize(int size, int expected)
        {
            return size == expected && (size & 7) == 0;
        }

        private static int OffsetOf<T>(string fieldName)
            where T : struct
        {
            System.Reflection.FieldInfo field = typeof(T).GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }
    }

    public struct EquipmentBakeSettings1715
    {
        public Material SharedMaterial;
        public string OutputFolder;
        public string MeshFolder;
        public string OutputName;
        public uint Seed;
        public float GlobalQualityWeight;

        public static EquipmentBakeSettings1715 Default
        {
            get
            {
                return new EquipmentBakeSettings1715
                {
                    OutputFolder = "Assets/_Project/Prefabs/Equipment",
                    MeshFolder = "Assets/_Project/Art/Baked/Equipment",
                    OutputName = "GEN_Prop_1715_CockpitControlPanel",
                    Seed = 1715u,
                    GlobalQualityWeight = 0.72f,
                    SharedMaterial = null
                };
            }
        }
    }

    public struct EquipmentBakeResult1715
    {
        public bool Success;
        public string MeshPath;
        public string PrefabPath;
        public string MaterialPath;
        public string FailureReason;
        public int VertexCount;
        public int TriangleCount;
        public int AnchorCount;
        public int ColliderProxyCount;
        public long BakeMicros;
        public bool InteractableLayerResolved;
        public Bounds Bounds;
    }

    [StructLayout(LayoutKind.Explicit, Size = 56)]
    public struct EquipmentPropVertex1715
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float3 Normal;
        [FieldOffset(24)] public float4 Tangent;
        [FieldOffset(40)] public float2 Uv0;
        [FieldOffset(48)] public uint ColorRgba;
        [FieldOffset(52)] public uint MaterialId;

        public static readonly VertexAttributeDescriptor[] Layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.UInt32, 1)
        };
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct EquipmentBakeMetrics1715
    {
        [FieldOffset(0)] public int RadialSegments;
        [FieldOffset(4)] public int TorusSegments;
        [FieldOffset(8)] public int CableSegments;
        [FieldOffset(12)] public int CableRingSegments;
        [FieldOffset(16)] public float BevelWidthMeters;
        [FieldOffset(20)] public float CatenaryMinY;
        [FieldOffset(24)] public float CatenaryMaxY;
        [FieldOffset(28)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct TopologyValidationResult1715
    {
        [FieldOffset(0)] public uint InvalidCount;
        [FieldOffset(4)] public uint FirstFailureCode;
        [FieldOffset(8)] public int FirstTriangle;
        [FieldOffset(12)] public float MinimumAreaSeen;
        [FieldOffset(16)] public float MaximumNormalError;
        [FieldOffset(20)] public uint Marker;
        [FieldOffset(24)] private ulong _pad0;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    public struct CatenaryCableExtrusionJob1715 : IJob
    {
        [WriteOnly] public NativeArray<EquipmentPropVertex1715> Vertices;
        [WriteOnly] public NativeArray<int> Indices;
        [WriteOnly] public NativeArray<float2> Metrics;
        public float3 Start;
        public float3 End;
        public float Radius;
        public float BundleRadius;
        public float SlackMeters;
        public float MinimumCenterY;
        public int Segments;
        public int RingSegments;
        public int StrandCount;

        public void Execute()
        {
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            float3 path = End - Start;
            float3 tangentBase = math.normalizesafe(path, new float3(1f, 0f, 0f));
            ResolveBasis(tangentBase, out float3 right, out float3 up);

            for (int strand = 0; strand < StrandCount; strand++)
            {
                int strandDivisor = StrandCount > 1 ? StrandCount : 1;
                float strandAngle = (math.PI * 2f * strand) / strandDivisor;
                float3 strandOffset = right * math.cos(strandAngle) * BundleRadius + up * math.sin(strandAngle) * BundleRadius * 0.42f;
                int strandVertexBase = strand * (Segments + 1) * RingSegments;
                int strandIndexBase = strand * Segments * RingSegments * 6;

                for (int segment = 0; segment <= Segments; segment++)
                {
                    float t = segment / (float)Segments;
                    float nextT = math.min(1f, t + 1f / Segments);
                    float previousT = math.max(0f, t - 1f / Segments);
                    float3 center = ResolveCableCenter(t, strandOffset);
                    float3 nextCenter = ResolveCableCenter(nextT, strandOffset);
                    float3 prevCenter = ResolveCableCenter(previousT, strandOffset);
                    float3 tangent = math.normalizesafe(nextCenter - prevCenter, tangentBase);
                    ResolveBasis(tangent, out float3 ringRight, out float3 ringUp);
                    float pinch = PinchScale(segment, strand, Segments);
                    for (int ring = 0; ring < RingSegments; ring++)
                    {
                        float angle = (math.PI * 2f * ring) / RingSegments;
                        float3 normal = ringRight * math.cos(angle) + ringUp * math.sin(angle);
                        float3 position = center + normal * Radius * pinch;
                        minY = math.min(minY, position.y);
                        maxY = math.max(maxY, position.y);
                        uint color = pinch < 0.82f ? PackColor(56, 51, 44, 185) : PackColor(42, 44, 39, 0);
                        Vertices[strandVertexBase + segment * RingSegments + ring] = new EquipmentPropVertex1715
                        {
                            Position = position,
                            Normal = math.normalizesafe(normal, new float3(0f, 1f, 0f)),
                            Tangent = new float4(tangent, 1f),
                            Uv0 = new float2(t, ring / (float)RingSegments),
                            ColorRgba = color,
                            MaterialId = 4u
                        };
                    }
                }

                int write = strandIndexBase;
                for (int segment = 0; segment < Segments; segment++)
                {
                    for (int ring = 0; ring < RingSegments; ring++)
                    {
                        int nextRing = (ring + 1) % RingSegments;
                        int a = strandVertexBase + segment * RingSegments + ring;
                        int b = strandVertexBase + (segment + 1) * RingSegments + ring;
                        int c = strandVertexBase + (segment + 1) * RingSegments + nextRing;
                        int d = strandVertexBase + segment * RingSegments + nextRing;
                        Indices[write++] = a;
                        Indices[write++] = b;
                        Indices[write++] = c;
                        Indices[write++] = a;
                        Indices[write++] = c;
                        Indices[write++] = d;
                    }
                }
            }

            Metrics[0] = new float2(minY, maxY);
        }

        private static float CatenarySag(float t, float slack)
        {
            float x = (t - 0.5f) * 2f;
            float denominator = Cosh(1f) - 1f;
            return slack * ((Cosh(x) - Cosh(1f)) / denominator);
        }

        private float3 ResolveCableCenter(float t, float3 strandOffset)
        {
            float3 center = math.lerp(Start, End, t) + strandOffset;
            center.y = math.max(center.y + CatenarySag(t, SlackMeters), MinimumCenterY);
            return center;
        }

        private static float PinchScale(int segment, int strand, int segmentCount)
        {
            int pitch = segmentCount / 4;
            if (pitch < 3)
                pitch = 3;
            int phase = (segment + strand) % pitch;
            return phase == 0 ? 0.72f : 1f;
        }

        private static float Cosh(float value)
        {
            return (math.exp(value) + math.exp(-value)) * 0.5f;
        }

        private static void ResolveBasis(float3 normal, out float3 right, out float3 up)
        {
            float3 helper = math.abs(normal.y) > 0.92f ? new float3(1f, 0f, 0f) : new float3(0f, 1f, 0f);
            right = math.normalizesafe(math.cross(helper, normal), new float3(1f, 0f, 0f));
            up = math.normalizesafe(math.cross(normal, right), new float3(0f, 1f, 0f));
        }

        private static uint PackColor(byte r, byte g, byte b, byte a)
        {
            return (uint)(r | (g << 8) | (b << 16) | (a << 24));
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    public struct BakeVertexChannelsJob1715 : IJobParallelFor
    {
        public NativeArray<EquipmentPropVertex1715> Vertices;
        public float GlobalQualityWeight;
        public uint Seed;

        public void Execute(int index)
        {
            EquipmentPropVertex1715 vertex = Vertices[index];
            uint color = vertex.ColorRgba;
            byte r = (byte)(color & 0xFFu);
            byte g = (byte)((color >> 8) & 0xFFu);
            byte b = (byte)((color >> 16) & 0xFFu);
            byte a = (byte)((color >> 24) & 0xFFu);

            float topWear = math.saturate(vertex.Normal.y * 0.55f + 0.35f);
            float edgeWear = math.saturate(math.abs(vertex.Position.x) * 1.45f + math.abs(vertex.Position.z) * 0.95f - 0.52f);
            float cavity = math.saturate((0.09f - vertex.Position.y) * 5.5f);
            float hash = Hash01((uint)index ^ Seed);
            byte wear = (byte)math.clamp(math.round(math.max(r, (topWear * edgeWear + hash * 0.08f) * math.lerp(80f, 210f, GlobalQualityWeight))), 0f, 255f);
            byte grime = (byte)math.clamp(math.round(math.max(b, cavity * math.lerp(120f, 235f, GlobalQualityWeight))), 0f, 255f);

            if (vertex.MaterialId == 3u)
            {
                a = 255;
                wear = wear < 18 ? (byte)18 : wear;
            }
            else if (vertex.MaterialId == 4u)
            {
                grime = grime < 120 ? (byte)120 : grime;
            }

            vertex.ColorRgba = PackColor(wear, g, grime, a);
            Vertices[index] = vertex;
        }

        private static float Hash01(uint value)
        {
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            value *= 3266489917u;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }

        private static uint PackColor(byte r, byte g, byte b, byte a)
        {
            return (uint)(r | (g << 8) | (b << 16) | (a << 24));
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    public struct TopologyValidationJob1715 : IJob
    {
        [ReadOnly] public NativeArray<EquipmentPropVertex1715> Vertices;
        [ReadOnly] public NativeArray<int> Indices;
        [WriteOnly] public NativeArray<TopologyValidationResult1715> Result;
        public float MinimumTriangleArea;

        public void Execute()
        {
            TopologyValidationResult1715 result = new TopologyValidationResult1715
            {
                MinimumAreaSeen = float.MaxValue,
                Marker = 0x56313735u
            };

            if (Vertices.Length == 0 || Indices.Length == 0 || Indices.Length % 3 != 0)
            {
                result.InvalidCount = 1u;
                result.FirstFailureCode = 1u;
                Result[0] = result;
                return;
            }

            for (int i = 0; i < Indices.Length; i += 3)
            {
                int ia = Indices[i];
                int ib = Indices[i + 1];
                int ic = Indices[i + 2];
                if ((uint)ia >= (uint)Vertices.Length || (uint)ib >= (uint)Vertices.Length || (uint)ic >= (uint)Vertices.Length)
                {
                    MarkFailure(ref result, 2u, i / 3);
                    continue;
                }

                EquipmentPropVertex1715 a = Vertices[ia];
                EquipmentPropVertex1715 b = Vertices[ib];
                EquipmentPropVertex1715 c = Vertices[ic];
                if (!IsFinite(a) || !IsFinite(b) || !IsFinite(c))
                {
                    MarkFailure(ref result, 3u, i / 3);
                    continue;
                }

                float area = math.length(math.cross(b.Position - a.Position, c.Position - a.Position)) * 0.5f;
                result.MinimumAreaSeen = math.min(result.MinimumAreaSeen, area);
                if (area < MinimumTriangleArea)
                {
                    MarkFailure(ref result, 4u, i / 3);
                    continue;
                }

                float normalError = math.max(math.abs(1f - math.length(a.Normal)), math.max(math.abs(1f - math.length(b.Normal)), math.abs(1f - math.length(c.Normal))));
                result.MaximumNormalError = math.max(result.MaximumNormalError, normalError);
                if (normalError > 0.035f)
                    MarkFailure(ref result, 5u, i / 3);
            }

            Result[0] = result;
        }

        private static void MarkFailure(ref TopologyValidationResult1715 result, uint code, int triangle)
        {
            if (result.InvalidCount == 0u)
            {
                result.FirstFailureCode = code;
                result.FirstTriangle = triangle;
            }

            result.InvalidCount++;
        }

        private static bool IsFinite(EquipmentPropVertex1715 vertex)
        {
            return math.all(math.isfinite(vertex.Position)) &&
                   math.all(math.isfinite(vertex.Normal)) &&
                   math.all(math.isfinite(vertex.Tangent)) &&
                   math.all(math.isfinite(vertex.Uv0));
        }
    }
}
#endif
