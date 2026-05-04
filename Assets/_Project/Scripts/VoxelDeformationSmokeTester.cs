using Hecton8.Caves;
using Hecton8.Physics;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Dev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Voxel Deformation Smoke Tester")]
    public sealed class VoxelDeformationSmokeTester : MonoBehaviour
    {
        [Header("Execution")]
        [SerializeField] private bool runOnStart;
        [SerializeField] private bool verboseLogging;

        [Header("Diagnostics")]
        [SerializeField] private int _debugRunCount;
        [SerializeField] private bool _debugLastPass;
        [SerializeField] private string _debugLastPhase = "Idle";
        [SerializeField] private string _debugLastIssue = string.Empty;

        private bool _isRunning;

        public bool DebugLastPass => _debugLastPass;
        public string DebugLastPhase => _debugLastPhase;
        public string DebugLastIssue => _debugLastIssue;

        private void Start()
        {
            if (!runOnStart || _isRunning)
                return;

            RunSmokePass();
        }

        [ContextMenu("Run Voxel Deformation Smoke Pass")]
        public void RunFromContextMenu()
        {
            if (_isRunning)
                return;

            RunSmokePass();
        }

        public bool TryRunImmediately()
        {
            if (_isRunning)
                return false;

            RunSmokePass();
            return _debugLastPass;
        }

        public string DescribeStatus()
        {
            string issue = string.IsNullOrWhiteSpace(_debugLastIssue) ? "none" : _debugLastIssue;
            return $"run={_debugRunCount} pass={_debugLastPass} phase={_debugLastPhase} issue={issue}";
        }

        private void RunSmokePass()
        {
            _isRunning = true;
            _debugRunCount++;
            _debugLastPass = false;
            _debugLastIssue = string.Empty;
            _debugLastPhase = "Backpressure";

            try
            {
                if (!ValidateBackpressureGuard())
                    return;

                _debugLastPhase = "SdfMerge";
                if (!ValidateSdfMerge())
                    return;

                _debugLastPhase = "PureVoidNav";
                if (!ValidatePureVoidNavGrid())
                    return;

                _debugLastPhase = "VertexAo";
                if (!ValidateVertexAmbientOcclusion())
                    return;

                _debugLastPhase = "SeamNormal";
                if (!ValidateSeamDensityPerturbation())
                    return;

                _debugLastPhase = "HullDent";
                if (!ValidateGaussianHullDent())
                    return;

                _debugLastPhase = "Passed";
                _debugLastPass = true;
                Log("[VoxelDeformationSmoke] PASS");
            }
            finally
            {
                _isRunning = false;
            }
        }

        private bool ValidateBackpressureGuard()
        {
            bool inactiveAtThreshold = global::HectonVoxelEngine.DebugResolveDeferredVoxelPhysicsBakeBackpressureState(64, false);
            bool activeAboveThreshold = global::HectonVoxelEngine.DebugResolveDeferredVoxelPhysicsBakeBackpressureState(65, false);
            bool holdsUntilRelease = global::HectonVoxelEngine.DebugResolveDeferredVoxelPhysicsBakeBackpressureState(33, true);
            bool releasesAtLowWater = global::HectonVoxelEngine.DebugResolveDeferredVoxelPhysicsBakeBackpressureState(32, true);
            return Require(!inactiveAtThreshold && activeAboveThreshold && holdsUntilRelease && !releasesAtLowWater, "Backpressure hysteresis failed.");
        }

        private bool ValidateSdfMerge()
        {
            byte subtractive = VoxelDeltaProcessor.DebugSubtractiveDeltaMode;
            byte additive = VoxelDeltaProcessor.DebugAdditiveDeltaMode;
            float subtractiveMerged = VoxelDeltaProcessor.DebugMergeSdfDensity(0.25f, subtractive, -0.6f, subtractive);
            float additiveMerged = VoxelDeltaProcessor.DebugMergeSdfDensity(-0.4f, additive, 0.35f, additive);
            float mixedMerged = VoxelDeltaProcessor.DebugMergeSdfDensity(0.5f, additive, -0.2f, subtractive);
            float subtractiveCompactedVoid = VoxelDeltaProcessor.DebugBakeDeltaIntoBaseDensity(-0.8f, 0.25f, subtractive);
            float subtractiveCompactedCarve = VoxelDeltaProcessor.DebugBakeDeltaIntoBaseDensity(0.6f, -0.2f, subtractive);
            float additiveCompactedRepair = VoxelDeltaProcessor.DebugBakeDeltaIntoBaseDensity(-0.6f, 0.2f, additive);
            return Require(
                math.abs(subtractiveMerged + 0.6f) < 0.0001f &&
                math.abs(additiveMerged - 0.35f) < 0.0001f &&
                math.abs(mixedMerged + 0.2f) < 0.0001f &&
                math.abs(subtractiveCompactedVoid + 0.8f) < 0.0001f &&
                math.abs(subtractiveCompactedCarve + 0.2f) < 0.0001f &&
                math.abs(additiveCompactedRepair - 0.2f) < 0.0001f,
                "Additive/subtractive SDF merge failed.");
        }

        private bool ValidatePureVoidNavGrid()
        {
            NativeArray<byte> passability = default;
            NativeArray<ushort> distance = default;
            try
            {
                passability = new NativeArray<byte>(8, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                distance = new NativeArray<ushort>(8, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < passability.Length; i++)
                    passability[i] = VoxelDynamicNavGridRuntime.OpenCell;

                JobHandle handle = new VoxelDynamicNavGridRuntime.ClearanceDilationJob
                {
                    Passability = passability,
                    DistanceMap = distance,
                    Dimensions = new int3(2, 2, 2),
                    AgentRadiusCells = 1
                }.Schedule();
                // COLD SYNC JOB: dev smoke tester validates a tiny nav dilation kernel outside gameplay.
                handle.Complete();

                for (int i = 0; i < passability.Length; i++)
                {
                    if (passability[i] != VoxelDynamicNavGridRuntime.OpenCell || distance[i] != ushort.MaxValue)
                        return Fail("Pure-void nav grid did not remain open with max distance.");
                }

                return true;
            }
            finally
            {
                if (passability.IsCreated)
                    passability.Dispose();
                if (distance.IsCreated)
                    distance.Dispose();
            }
        }

        private bool ValidateVertexAmbientOcclusion()
        {
            NativeArray<float> density = default;
            NativeArray<float> smoothDensity = default;
            NativeArray<float3> positions = default;
            NativeArray<float3> normals = default;
            NativeArray<float> curvature = default;
            NativeArray<float> ambientOcclusion = default;
            try
            {
                density = new NativeArray<float>(27, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                smoothDensity = new NativeArray<float>(27, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                positions = new NativeArray<float3>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                normals = new NativeArray<float3>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                curvature = new NativeArray<float>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                ambientOcclusion = new NativeArray<float>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                int index = 0;
                for (int z = 0; z < 3; z++)
                for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++, index++)
                {
                    float value = x - 1f;
                    density[index] = value;
                    smoothDensity[index] = value;
                }

                positions[0] = new float3(1f, 1f, 1f);
                JobHandle handle = new global::VoxelNormalJob
                {
                    ptsX = 3,
                    ptsY = 3,
                    ptsZ = 3,
                    volumeOrigin = float3.zero,
                    voxelStep = 1f,
                    densityField = density,
                    smoothDensityField = smoothDensity,
                    positions = positions,
                    normals = normals,
                    curvatureValues = curvature,
                    ambientOcclusionValues = ambientOcclusion
                }.Schedule(1, 1);
                // COLD SYNC JOB: dev smoke tester validates a one-vertex AO bake outside gameplay.
                handle.Complete();

                float ao = ambientOcclusion[0];
                float3 normal = normals[0];
                return Require(
                    math.isfinite(ao) &&
                    ao >= 0f &&
                    ao <= 1f &&
                    math.all(math.isfinite(normal)) &&
                    math.lengthsq(normal) > 0.5f,
                    "Vertex AO or normal output was invalid.");
            }
            finally
            {
                if (density.IsCreated)
                    density.Dispose();
                if (smoothDensity.IsCreated)
                    smoothDensity.Dispose();
                if (positions.IsCreated)
                    positions.Dispose();
                if (normals.IsCreated)
                    normals.Dispose();
                if (curvature.IsCreated)
                    curvature.Dispose();
                if (ambientOcclusion.IsCreated)
                    ambientOcclusion.Dispose();
            }
        }

        private bool ValidateSeamDensityPerturbation()
        {
            float perturbation = VoxelSeamDirector.ComputeCaveMouthDensityPerturbation(
                new float3(0f, 0.5f, 0f),
                float3.zero,
                new float3(0f, 1f, 0f),
                1f,
                2f,
                0.5f);
            return Require(perturbation > 0.01f && perturbation < 0.08f, "Cave-mouth terrain-normal perturbation failed.");
        }

        private bool ValidateGaussianHullDent()
        {
            float3 dented = SubmarineStructuralGrid.DebugEvaluateHullDentVertex(
                float3.zero,
                float3.zero,
                new float3(0f, 1f, 0f),
                1f,
                0.1f,
                0.5f,
                0.05f);
            return Require(dented.y < -0.09f && math.abs(dented.x) < 0.0001f && math.abs(dented.z) < 0.0001f, "Gaussian hull dent failed.");
        }

        private bool Require(bool condition, string issue)
        {
            return condition || Fail(issue);
        }

        private bool Fail(string issue)
        {
            _debugLastIssue = issue;
            _debugLastPass = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[VoxelDeformationSmoke] FAIL " + issue, this);
#endif
            return false;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void Log(string message)
        {
            if (verboseLogging)
                Debug.Log(message, this);
        }
    }
}
