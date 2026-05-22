using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Dev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Thermal Melt Smoke Tester")]
    public sealed class ThermalMeltSmokeTester : MonoBehaviour
    {
        [SerializeField] private bool runOnStart;
        [SerializeField] private bool verboseLogging;
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
            if (runOnStart && !_isRunning)
                RunSmokePass();
        }

        [ContextMenu("Run Thermal Melt Smoke Pass")]
        public void RunFromContextMenu()
        {
            if (!_isRunning)
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
            try
            {
                _debugLastPhase = "ThermalProgress";
                if (!ValidateThermalProgress())
                    return;

                _debugLastPhase = "DistanceLod";
                if (!ValidateDistanceLod())
                    return;

                _debugLastPhase = "DirtyBlendUv2";
                if (!ValidateDirtyBlendJob())
                    return;

                _debugLastPhase = "AupRaymarch";
                if (!ValidateAupRaymarchJob())
                    return;

                _debugLastPhase = "LocalizedNavPatch";
                if (!ValidateLocalizedNavPatchJob())
                    return;

                _debugLastPhase = "Passed";
                _debugLastPass = true;
                Log("[ThermalMeltSmoke] PASS");
            }
            finally
            {
                _isRunning = false;
            }
        }

        private bool ValidateThermalProgress()
        {
            float start = VoxelDeltaProcessor.DebugResolveThermalMeltProgress(0f);
            float mid = VoxelDeltaProcessor.DebugResolveThermalMeltProgress(2.5f);
            float end = VoxelDeltaProcessor.DebugResolveThermalMeltProgress(5f);
            return Require(math.abs(start) < 0.0001f && mid > 0.49f && mid < 0.51f && math.abs(end - 1f) < 0.0001f, "Thermal melt five-second crater expansion invalid.");
        }

        private bool ValidateDistanceLod()
        {
            int nearLod = global::HectonVoxelEngine.DebugResolveDistanceBasedVoxelLodLevel(Vector3.zero, new Vector3(0f, 0f, 199f));
            int farLod = global::HectonVoxelEngine.DebugResolveDistanceBasedVoxelLodLevel(Vector3.zero, new Vector3(0f, 0f, 201f));
            return Require(nearLod == 0 && farLod == 1, "Distance-based voxel LOD threshold invalid.");
        }

        private bool ValidateDirtyBlendJob()
        {
            NativeArray<float3> positions = default;
            NativeParallelHashMap<int3, VoxelModifiedCell> modifiedCells = default;
            NativeArray<float> dirty = default;
            try
            {
                positions = new NativeArray<float3>(2, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                dirty = new NativeArray<float>(2, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                modifiedCells = new NativeParallelHashMap<int3, VoxelModifiedCell>(1, Allocator.TempJob);
                positions[0] = new float3(0.5f, 0.5f, 0.5f);
                positions[1] = new float3(8.5f, 8.5f, 8.5f);
                modifiedCells.TryAdd(new int3(0, 0, 0), new VoxelModifiedCell { Density = (half)1f, MaterialId = 1, Flags = VoxelDeltaProcessor.DebugAdditiveDeltaMode });

                JobHandle handle = new global::VoxelDirtyBlendJob
                {
                    positions = positions,
                    modifiedCells = modifiedCells,
                    voxelStep = 1f,
                    absoluteUniverseOffset = float3.zero,
                    dirtyBlendValues = dirty
                }.Schedule(2, 1);
                // COLD SYNC JOB: dev smoke tester validates a two-vertex UV2 kernel outside gameplay.
                DispatcherJobFence.TryComplete(ref handle, forceComplete: true);

                return Require(dirty[0] > 0.99f && dirty[1] < 0.001f, "Dirty blend UV2 kernel failed.");
            }
            finally
            {
                if (positions.IsCreated)
                    positions.Dispose();
                if (modifiedCells.IsCreated)
                    modifiedCells.Dispose();
                if (dirty.IsCreated)
                    dirty.Dispose();
            }
        }

        private bool ValidateAupRaymarchJob()
        {
            NativeArray<byte> sdf = default;
            NativeArray<VoxelSdfRaycastHit> result = default;
            try
            {
                sdf = new NativeArray<byte>(16, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                result = new NativeArray<VoxelSdfRaycastHit>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                for (int i = 0; i < sdf.Length; i++)
                {
                    int x = i & 3;
                    sdf[i] = x < 2 ? (byte)0 : (byte)255;
                }

                JobHandle handle = new VoxelSdfRaymarchJob
                {
                    EncodedSdf = sdf.AsReadOnly(),
                    GridDimensions = new int3(4, 2, 2),
                    VolumeOrigin = float3.zero,
                    CellSize = new float3(1f, 1f, 1f),
                    SdfRange = 1f,
                    Origin = new float3(0f, 0f, 0f),
                    Direction = new float3(1f, 0f, 0f),
                    MaxDistance = 3f,
                    StepMeters = 0.5f,
                    Result = result
                }.Schedule();
                // COLD SYNC JOB: dev smoke tester validates scanner SDF raymarch kernel outside gameplay.
                DispatcherJobFence.TryComplete(ref handle, forceComplete: true);

                return Require(result[0].Hit != 0 && result[0].Distance > 0.5f && result[0].Distance < 2.5f, "AUP SDF raymarch missed open-to-solid crossing.");
            }
            finally
            {
                if (sdf.IsCreated)
                    sdf.Dispose();
                if (result.IsCreated)
                    result.Dispose();
            }
        }

        private bool ValidateLocalizedNavPatchJob()
        {
            NativeArray<float> density = default;
            NativeArray<byte> passability = default;
            try
            {
                density = new NativeArray<float>(8, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                passability = new NativeArray<byte>(8, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                density[7] = 1f;
                JobHandle handle = new VoxelDynamicNavGridRuntime.UpdateNavCellsJob
                {
                    DensityField = density,
                    Passability = passability,
                    Dimensions = new int3(2, 2, 2),
                    RegionMin = new int3(1, 1, 1),
                    RegionSize = new int3(1, 1, 1),
                    SolidThreshold = 0f
                }.Schedule(1, 1);
                // COLD SYNC JOB: dev smoke tester validates one-cell localized nav update outside gameplay.
                DispatcherJobFence.TryComplete(ref handle, forceComplete: true);

                return Require(passability[7] == VoxelDynamicNavGridRuntime.SolidCell && passability[0] == 0, "Localized nav patch mutated wrong cells.");
            }
            finally
            {
                if (density.IsCreated)
                    density.Dispose();
                if (passability.IsCreated)
                    passability.Dispose();
            }
        }

        private bool Require(bool condition, string issue)
        {
            if (condition)
                return true;

            return Fail(issue);
        }

        private bool Fail(string issue)
        {
            _debugLastIssue = issue;
            if (verboseLogging)
                Debug.LogError("[ThermalMeltSmoke] " + issue, this);
            return false;
        }

        private void Log(string message)
        {
            if (verboseLogging)
                Debug.Log(message, this);
        }
    }
}
