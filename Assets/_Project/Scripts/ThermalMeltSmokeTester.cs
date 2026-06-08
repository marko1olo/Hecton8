using System;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Dev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Thermal Melt Smoke Tester")]
    public sealed class ThermalMeltSmokeTester : MonoBehaviour
    {
        private const string NativeMemoryOwner = nameof(ThermalMeltSmokeTester);

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
            NativeArray<VoxelModifiedCellEntry> modifiedCells = default;
            NativeArray<int> bucketHeads = default;
            NativeArray<int> bucketNext = default;
            NativeArray<float> dirty = default;
            try
            {
                positions = AllocateTrackedTempJobArray<float3>(2, nameof(positions), NativeArrayOptions.UninitializedMemory);
                dirty = AllocateTrackedTempJobArray<float>(2, nameof(dirty), NativeArrayOptions.ClearMemory);
                modifiedCells = AllocateTrackedTempJobArray<VoxelModifiedCellEntry>(1, nameof(modifiedCells), NativeArrayOptions.UninitializedMemory);
                bucketHeads = AllocateTrackedTempJobArray<int>(1, nameof(bucketHeads), NativeArrayOptions.UninitializedMemory);
                bucketNext = AllocateTrackedTempJobArray<int>(1, nameof(bucketNext), NativeArrayOptions.UninitializedMemory);
                positions[0] = new float3(0.5f, 0.5f, 0.5f);
                positions[1] = new float3(8.5f, 8.5f, 8.5f);
                modifiedCells[0] = new VoxelModifiedCellEntry
                {
                    AbsoluteCell = new int3(0, 0, 0),
                    Cell = new VoxelModifiedCell { Density = (half)1f, MaterialId = 1, Flags = VoxelDeltaProcessor.DebugAdditiveDeltaMode }
                };
                bucketHeads[0] = 0;
                bucketNext[0] = -1;

                JobHandle handle = new global::VoxelDirtyBlendJob
                {
                    positions = positions,
                    modifiedCells = modifiedCells,
                    modifiedCellBucketHeads = bucketHeads,
                    modifiedCellNext = bucketNext,
                    modifiedCellCount = 1,
                    modifiedCellBucketCount = 1,
                    voxelStep = 1f,
                    absoluteCellOffset = double3.zero,
                    dirtyBlendValues = dirty
                }.Schedule(2, 1);
                // COLD SYNC JOB: dev smoke tester validates a two-vertex UV2 kernel outside gameplay.
                DispatcherJobFence.TryComplete(ref handle, forceComplete: true);

                return Require(dirty[0] > 0.99f && dirty[1] < 0.001f, "Dirty blend UV2 kernel failed.");
            }
            finally
            {
                DisposeTrackedTempJobArray(ref positions);
                DisposeTrackedTempJobArray(ref modifiedCells);
                DisposeTrackedTempJobArray(ref bucketHeads);
                DisposeTrackedTempJobArray(ref bucketNext);
                DisposeTrackedTempJobArray(ref dirty);
            }
        }

        private bool ValidateAupRaymarchJob()
        {
            NativeArray<byte> sdf = default;
            NativeArray<VoxelSdfRaycastHit> result = default;
            try
            {
                sdf = AllocateTrackedTempJobArray<byte>(16, nameof(sdf), NativeArrayOptions.UninitializedMemory);
                result = AllocateTrackedTempJobArray<VoxelSdfRaycastHit>(1, nameof(result), NativeArrayOptions.ClearMemory);
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
                DisposeTrackedTempJobArray(ref sdf);
                DisposeTrackedTempJobArray(ref result);
            }
        }

        private bool ValidateLocalizedNavPatchJob()
        {
            NativeArray<float> density = default;
            NativeArray<byte> passability = default;
            try
            {
                density = AllocateTrackedTempJobArray<float>(8, nameof(density), NativeArrayOptions.ClearMemory);
                passability = AllocateTrackedTempJobArray<byte>(8, nameof(passability), NativeArrayOptions.ClearMemory);
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
                DisposeTrackedTempJobArray(ref density);
                DisposeTrackedTempJobArray(ref passability);
            }
        }

        private static NativeArray<T> AllocateTrackedTempJobArray<T>(int length, string label, NativeArrayOptions options) where T : struct
        {
            NativeArray<T> array = new NativeArray<T>(length, Allocator.TempJob, options);
            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob);
                if (sentinelId > 0)
                    return array;
            }
            catch
            {
                if (array.IsCreated)
                    array.Dispose();

                throw;
            }

            array.Dispose();
            throw new System.InvalidOperationException($"Native memory sentinel registration failed for {label}.");
        }

        private static unsafe void DisposeTrackedTempJobArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
            System.Exception nativeSentinelCleanupException0 = null;

            try
            {
                NativeMemorySentinel.UnregisterPointer(trackedPointer);
            }
            catch (System.Exception nativeSentinelException0)
            {
                nativeSentinelCleanupException0 = nativeSentinelException0;
            }

            try
            {
                array.Dispose();
            }
            catch (System.Exception nativeSentinelException0)
            {
                if (nativeSentinelCleanupException0 == null)
                    nativeSentinelCleanupException0 = nativeSentinelException0;
            }
            finally
            {
                array = default;
            }

            if (nativeSentinelCleanupException0 != null)
                throw nativeSentinelCleanupException0;
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
                Hecton8.Core.H8Debug.LogError("[ThermalMeltSmoke] " + issue, this);
            return false;
        }

        private void Log(string message)
        {
            if (verboseLogging)
                Hecton8.Core.H8Debug.Log(message, this);
        }
    }
}
