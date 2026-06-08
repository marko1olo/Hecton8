using System;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

using SubmarineStructuralGrid = global::Hecton8.Physics.SubmarineStructuralGrid;

namespace Hecton8.Dev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Voxel Deformation Smoke Tester")]
    public sealed class VoxelDeformationSmokeTester : MonoBehaviour
    {
        private const string NativeMemoryOwner = nameof(VoxelDeformationSmokeTester);

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

#if UNITY_EDITOR
        public string DescribeStatus()
        {
            string issue = string.IsNullOrWhiteSpace(_debugLastIssue) ? "none" : _debugLastIssue;
            return $"run={_debugRunCount} pass={_debugLastPass} phase={_debugLastPhase} issue={issue}";
        }

        public string BuildJsonStatus()
        {
            string escapedIssue = (_debugLastIssue ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
            return "{\"tester\":\"VoxelDeformationSmokeTester\",\"run\":" + _debugRunCount +
                   ",\"pass\":" + (_debugLastPass ? "true" : "false") +
                   ",\"phase\":\"" + _debugLastPhase +
                   "\",\"issue\":\"" + escapedIssue + "\"}";
        }

        public static void RunBatchMode()
        {
            GameObject root = new GameObject("VoxelDeformationSmokeTester_Batch"); // COLD ALLOC: GameObject[1] - editor-only voxel deformation smoke root - owner: VoxelDeformationSmokeTester
            VoxelDeformationSmokeTester tester = root.AddComponent<VoxelDeformationSmokeTester>();
            bool pass = tester.TryRunImmediately();
            string json = tester.BuildJsonStatus();
            File.WriteAllText("Library/VoxelDeformationSmokeTester.json", json);
            Hecton8.Core.H8Debug.Log(json);
            UnityEngine.Object.DestroyImmediate(root);
            EditorApplication.Exit(pass ? 0 : 1);
        }
#endif

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

                _debugLastPhase = "BakePool";
                if (!ValidatePhysicsBakePoolPressure())
                    return;

                _debugLastPhase = "SdfMerge";
                if (!ValidateSdfMerge())
                    return;

                _debugLastPhase = "CompactionQueue";
                if (!ValidateCompactionQueuePriority())
                    return;

                _debugLastPhase = "PureVoidNav";
                if (!ValidatePureVoidNavGrid())
                    return;

                _debugLastPhase = "VoidChunkBounds";
                if (!ValidateVoidChunkBoundsEarlyExit())
                    return;

                _debugLastPhase = "VertexAo";
                if (!ValidateVertexAmbientOcclusion())
                    return;

                _debugLastPhase = "HullImpactDecal";
                if (!ValidateHullImpactDecalSizing())
                    return;

                _debugLastPhase = "NativeCarveQueue";
                if (!ValidateNativeCarveQueue())
                    return;

                _debugLastPhase = "VoxelBlackBox";
                if (!ValidateVoxelBlackBox())
                    return;

                _debugLastPhase = "VoxelChunkModifiedEvent";
                if (!ValidateVoxelChunkModifiedEvent())
                    return;

                _debugLastPhase = "AsyncCarveContracts";
                if (!ValidateAsyncCarveSourceContracts())
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

        private bool ValidatePhysicsBakePoolPressure()
        {
            int capacity = global::HectonVoxelEngine.DebugVoxelPhysicsBakeMeshPoolSize;
            bool notExhaustedBelowCapacity = global::HectonVoxelEngine.DebugResolveVoxelPhysicsBakePoolExhausted(capacity - 1);
            bool exhaustedAtCapacity = global::HectonVoxelEngine.DebugResolveVoxelPhysicsBakePoolExhausted(capacity);
            bool exhaustedAboveCapacity = global::HectonVoxelEngine.DebugResolveVoxelPhysicsBakePoolExhausted(capacity + 1);
            return Require(
                capacity > 0 &&
                !notExhaustedBelowCapacity &&
                exhaustedAtCapacity &&
                exhaustedAboveCapacity,
                "PhysX bake mesh pool exhaustion gate failed.");
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

        private bool ValidateCompactionQueuePriority()
        {
            bool replacesLowerDirtyChunk = VoxelDeltaProcessor.DebugShouldReplaceQueuedCompaction(32000, 28000);
            bool keepsHigherDirtyChunk = VoxelDeltaProcessor.DebugShouldReplaceQueuedCompaction(28000, 32000);
            bool keepsEqualDirtyChunk = VoxelDeltaProcessor.DebugShouldReplaceQueuedCompaction(30000, 30000);
            return Require(
                replacesLowerDirtyChunk &&
                !keepsHigherDirtyChunk &&
                !keepsEqualDirtyChunk,
                "Compaction queue priority replacement failed.");
        }

        private bool ValidatePureVoidNavGrid()
        {
            NativeArray<byte> passability = default;
            NativeArray<ushort> distance = default;
            NativeArray<int> pureVoidFlags = default;
            try
            {
                passability = AllocateTrackedTempJobArray<byte>(8, nameof(passability), NativeArrayOptions.ClearMemory);
                distance = AllocateTrackedTempJobArray<ushort>(8, nameof(distance), NativeArrayOptions.UninitializedMemory);
                pureVoidFlags = AllocateTrackedTempJobArray<int>(
                    VoxelDynamicNavGridRuntime.ResolvePureVoidBlockCount(passability.Length),
                    nameof(pureVoidFlags),
                    NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < passability.Length; i++)
                    passability[i] = VoxelDynamicNavGridRuntime.OpenCell;

                JobHandle handle = new VoxelDynamicNavGridRuntime.ClearanceDilationJob
                {
                    Passability = passability,
                    DistanceMap = distance,
                    Dimensions = new int3(2, 2, 2),
                    AgentRadiusCells = 1
                }.Schedule();
                handle = VoxelDynamicNavGridRuntime.SchedulePureVoidScan(
                    passability,
                    distance,
                    pureVoidFlags,
                    passability.Length,
                    handle);
                // COLD SYNC JOB: dev smoke tester validates a tiny nav dilation kernel outside gameplay.
                DispatcherJobFence.TryComplete(ref handle, forceComplete: true);

                for (int i = 0; i < passability.Length; i++)
                {
                    if (passability[i] != VoxelDynamicNavGridRuntime.OpenCell || distance[i] != ushort.MaxValue)
                        return Fail("Pure-void nav grid did not remain open with max distance.");
                }

                return Require(pureVoidFlags[0] == 1, "Burst pure-void block scan rejected open-water chunk.");
            }
            finally
            {
                DisposeTrackedTempJobArray(ref passability);
                DisposeTrackedTempJobArray(ref distance);
                DisposeTrackedTempJobArray(ref pureVoidFlags);
            }
        }

        private bool ValidateVoidChunkBoundsEarlyExit()
        {
            NativeArray<float> density = default;
            NativeArray<int> hasContent = default;
            try
            {
                density = AllocateTrackedTempJobArray<float>(8, nameof(density), NativeArrayOptions.UninitializedMemory);
                hasContent = AllocateTrackedTempJobArray<int>(1, nameof(hasContent), NativeArrayOptions.ClearMemory);
                for (int i = 0; i < density.Length; i++)
                    density[i] = -1f;

                JobHandle handle = new global::VoxelChunkBoundsContentJob
                {
                    ptsX = 2,
                    ptsY = 2,
                    ptsZ = 2,
                    density = density,
                    hasContent = hasContent
                }.Schedule();
                // COLD SYNC JOB: dev smoke tester validates the eight-corner void early-exit gate outside gameplay.
                DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
                bool pureVoidRejected = hasContent[0] == 0;

                density[7] = 1f;
                handle = new global::VoxelChunkBoundsContentJob
                {
                    ptsX = 2,
                    ptsY = 2,
                    ptsZ = 2,
                    density = density,
                    hasContent = hasContent
                }.Schedule();
                // COLD SYNC JOB: dev smoke tester validates non-void corner admission outside gameplay.
                DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
                return Require(pureVoidRejected && hasContent[0] == 1, "Voxel chunk bounds content gate failed.");
            }
            finally
            {
                DisposeTrackedTempJobArray(ref density);
                DisposeTrackedTempJobArray(ref hasContent);
            }
        }

        private bool ValidateVertexAmbientOcclusion()
        {
            NativeArray<sbyte> density = default;
            NativeArray<float3> positions = default;
            NativeArray<float3> normals = default;
            NativeArray<float> curvature = default;
            NativeArray<float> ambientOcclusion = default;
            try
            {
                density = AllocateTrackedTempJobArray<sbyte>(27, nameof(density), NativeArrayOptions.UninitializedMemory);
                positions = AllocateTrackedTempJobArray<float3>(1, nameof(positions), NativeArrayOptions.UninitializedMemory);
                normals = AllocateTrackedTempJobArray<float3>(1, nameof(normals), NativeArrayOptions.UninitializedMemory);
                curvature = AllocateTrackedTempJobArray<float>(1, nameof(curvature), NativeArrayOptions.UninitializedMemory);
                ambientOcclusion = AllocateTrackedTempJobArray<float>(1, nameof(ambientOcclusion), NativeArrayOptions.UninitializedMemory);

                int index = 0;
                for (int z = 0; z < 3; z++)
                for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++, index++)
                {
                    sbyte value = (sbyte)(x - 1);
                    density[index] = value;
                }

                positions[0] = new float3(1f, 1f, 1f);
                JobHandle handle = new global::VoxelNormalJob
                {
                    ptsX = 3,
                    ptsY = 3,
                    ptsZ = 3,
                    densityStrideY = 3,
                    densityStrideZ = 9,
                    volumeOrigin = float3.zero,
                    invVoxelStep = 1f,
                    densityField = density,
                    positions = positions,
                    normals = normals,
                    curvatureValues = curvature,
                    ambientOcclusionValues = ambientOcclusion
                }.Schedule(1, 1);
                // COLD SYNC JOB: dev smoke tester validates a one-vertex AO bake outside gameplay.
                DispatcherJobFence.TryComplete(ref handle, forceComplete: true);

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
                DisposeTrackedTempJobArray(ref density);
                DisposeTrackedTempJobArray(ref positions);
                DisposeTrackedTempJobArray(ref normals);
                DisposeTrackedTempJobArray(ref curvature);
                DisposeTrackedTempJobArray(ref ambientOcclusion);
            }
        }

        private bool ValidateHullImpactDecalSizing()
        {
            float small = SubmarineStructuralGrid.DebugResolveHullImpactDentDecalSize(0.35f, 0.95f, 0f, 0f);
            float severe = SubmarineStructuralGrid.DebugResolveHullImpactDentDecalSize(0.35f, 0.95f, 30f, 1f);
            return Require(
                math.abs(small - 0.35f) < 0.0001f &&
                math.abs(severe - 1.5f) < 0.0001f,
                "Hull impact decal sizing failed.");
        }

        private bool ValidateNativeCarveQueue()
        {
            double3 originAup = double3.zero;
            double3 firstHitAup = new double3(11d, -23d, 37d);
            double3 firstEndAup = new double3(12d, -23d, 37d);
            double3 firstHitLocal = firstHitAup - originAup;
            double3 firstEndLocal = firstEndAup - originAup;
            VoxelCarveEvent first = new VoxelCarveEvent
            {
                VolumeInstanceId = 17ul,
                AbsoluteHitPoint = new float3((float)firstHitLocal.x, (float)firstHitLocal.y, (float)firstHitLocal.z),
                AbsoluteSegmentEnd = new float3((float)firstEndLocal.x, (float)firstEndLocal.y, (float)firstEndLocal.z),
                AbsoluteHalfExtents = new float3(1f, 2f, 3f),
                AbsoluteImpulseDirection = new float3(0f, 0f, 1f),
                AbsoluteHitPointDouble = firstHitAup,
                AbsoluteSegmentEndDouble = firstEndAup,
                RadiusMeters = 2.5f,
                BlendStrengthMeters = 0.75f,
                Operation = (byte)VoxelCarveOperationType.Subtract,
                Shape = (byte)VoxelCarveShapeType.Sphere,
                MaterialId = 9,
                SourceFlags = 3
            };
            VoxelCarveEvent second = first;
            second.VolumeInstanceId = 23ul;
            second.Operation = (byte)VoxelCarveOperationType.Add;
            second.Shape = (byte)VoxelCarveShapeType.Capsule;
            second.RadiusMeters = 4f;

            VoxelCarveEvent observedFirst = first;
            VoxelCarveEvent observedSecond = second;
            int packetBytes = UnsafeUtility.SizeOf<VoxelCarveEvent>();
            bool queuePreservedPayload =
                observedFirst.VolumeInstanceId == 17ul &&
                observedSecond.VolumeInstanceId == 23ul &&
                observedFirst.Operation == (byte)VoxelCarveOperationType.Subtract &&
                observedSecond.Operation == (byte)VoxelCarveOperationType.Add &&
                observedFirst.Shape == (byte)VoxelCarveShapeType.Sphere &&
                observedSecond.Shape == (byte)VoxelCarveShapeType.Capsule &&
                math.abs(observedFirst.AbsoluteHitPoint.x - 11f) < 0.0001f &&
                math.abs(observedFirst.AbsoluteHitPointDouble.x - 11d) < 0.0000001d &&
                math.abs(observedFirst.RadiusMeters - 2.5f) < 0.0001f &&
                math.abs(observedSecond.RadiusMeters - 4f) < 0.0001f;

            int minimumDrainBudget = VoxelDeltaProcessor.DebugResolveQueuedCarveDrainBudget(0f);
            int weakDrainBudget = VoxelDeltaProcessor.DebugResolveQueuedCarveDrainBudget(0.24f);
            int middleDrainBudget = VoxelDeltaProcessor.DebugResolveQueuedCarveDrainBudget(0.5f);
            int highDrainBudget = VoxelDeltaProcessor.DebugResolveQueuedCarveDrainBudget(0.78f);
            int visualOverkillDrainBudget = VoxelDeltaProcessor.DebugResolveQueuedCarveDrainBudget(1f);
            bool continuousBudgetValid =
                minimumDrainBudget == 1 &&
                weakDrainBudget >= minimumDrainBudget &&
                middleDrainBudget >= weakDrainBudget &&
                highDrainBudget >= middleDrainBudget &&
                visualOverkillDrainBudget == 4;

            VoxelCarveEvent overflowCompatible = first;
            VoxelCarveEvent newestCompatible = first;
            double3 newestHitAup = new double3(19d, -23d, 37d);
            double3 newestEndAup = new double3(20d, -23d, 37d);
            double3 newestHitLocal = newestHitAup - originAup;
            double3 newestEndLocal = newestEndAup - originAup;
            newestCompatible.AbsoluteHitPoint = new float3((float)newestHitLocal.x, (float)newestHitLocal.y, (float)newestHitLocal.z);
            newestCompatible.AbsoluteSegmentEnd = new float3((float)newestEndLocal.x, (float)newestEndLocal.y, (float)newestEndLocal.z);
            newestCompatible.AbsoluteHitPointDouble = newestHitAup;
            newestCompatible.AbsoluteSegmentEndDouble = newestEndAup;
            newestCompatible.RadiusMeters = 3.5f;
            newestCompatible.BlendStrengthMeters = 1.25f;
            newestCompatible.SourceFlags = 5;
            VoxelCarveEvent coalesced = VoxelDeltaProcessor.DebugResolveOverflowQueuedCarveEvent(
                in overflowCompatible,
                in newestCompatible);
            bool overflowCoalescingValid =
                coalesced.VolumeInstanceId == first.VolumeInstanceId &&
                coalesced.Shape == (byte)VoxelCarveShapeType.Capsule &&
                math.abs(coalesced.AbsoluteHitPointDouble.x - 11d) < 0.0000001d &&
                math.abs(coalesced.AbsoluteSegmentEndDouble.x - 20d) < 0.0000001d &&
                math.abs(coalesced.RadiusMeters - 3.5f) < 0.0001f &&
                coalesced.SourceFlags == (byte)(first.SourceFlags | newestCompatible.SourceFlags);

            return Require(
                packetBytes > 0 &&
                packetBytes <= 128 &&
                queuePreservedPayload &&
                continuousBudgetValid &&
                overflowCoalescingValid,
                "Vault carve event packet or continuous quality budget failed.");
        }

        private bool ValidateVoxelBlackBox()
        {
            double3 originAup = double3.zero;
            double3 hitAup = new double3(1d, 2d, 3d);
            double3 endAup = new double3(2d, 2d, 3d);
            double3 hitLocal = hitAup - originAup;
            double3 endLocal = endAup - originAup;
            VoxelCarveEvent valid = new VoxelCarveEvent
            {
                AbsoluteHitPoint = new float3((float)hitLocal.x, (float)hitLocal.y, (float)hitLocal.z),
                AbsoluteSegmentEnd = new float3((float)endLocal.x, (float)endLocal.y, (float)endLocal.z),
                AbsoluteHalfExtents = new float3(1f, 1f, 1f),
                AbsoluteImpulseDirection = new float3(0f, 1f, 0f),
                AbsoluteHitPointDouble = hitAup,
                AbsoluteSegmentEndDouble = endAup,
                RadiusMeters = 1.5f,
                BlendStrengthMeters = 0.5f,
                Operation = (byte)VoxelCarveOperationType.Subtract,
                Shape = (byte)VoxelCarveShapeType.Sphere
            };
            VoxelCarveEvent invalid = valid;
            invalid.RadiusMeters = float.NaN;

            bool finiteGateValid =
                VoxelDeltaProcessor.DebugIsFiniteCarveEvent(in valid) &&
                !VoxelDeltaProcessor.DebugIsFiniteCarveEvent(in invalid);

#if UNITY_EDITOR
            string delta = ReadProjectFile("Assets/_Project/Scripts/VoxelDeltaProcessor.cs");
            bool dumpContract =
                delta.IndexOf("VaultBufferHandle<VoxelCarveTelemetryEntry> _blackBoxHandle", System.StringComparison.Ordinal) >= 0 &&
                delta.IndexOf("BufferID.ShinobuDeltaCrusherVoxelBlackBox", System.StringComparison.Ordinal) >= 0 &&
                delta.IndexOf("Dump_1304_Voxel.bin", System.StringComparison.Ordinal) >= 0 &&
                delta.IndexOf("DumpBlackBoxOnce", System.StringComparison.Ordinal) >= 0 &&
                delta.IndexOf("WriteBlackBoxSample", System.StringComparison.Ordinal) >= 0;
#else
            bool dumpContract = true;
#endif

            return Require(
                VoxelDeltaProcessor.DebugVoxelBlackBoxCapacity == 300 &&
                VoxelDeltaProcessor.DebugVoxelBlackBoxEntryBytes == 80 &&
                finiteGateValid &&
                dumpContract,
                "Voxel black-box telemetry contract failed.");
        }

        private bool ValidateVoxelChunkModifiedEvent()
        {
            DrainVoxelChunkModifiedEvents();

            VoxelChunkModifiedEvent expected = new VoxelChunkModifiedEvent
            {
                VolumeInstanceId = 99ul,
                MinAbsoluteCell = new int3(-2, 3, 5),
                MaxAbsoluteCell = new int3(4, 9, 11),
                VoxelSize = 0.5f,
                Frame = 42u,
                Operation = (byte)VoxelCarveOperationType.Subtract,
                Shape = (byte)VoxelCarveShapeType.Sphere,
                Flags = 7,
                StateHash = 0xBADC0DEu
            };

            bool published = VoxelChunkModifiedEvents.TryPublish(in expected);

            bool dequeued = VoxelChunkModifiedEvents.TryDequeue(out VoxelChunkModifiedEvent observed);
            bool noTail = !VoxelChunkModifiedEvents.TryDequeue(out _);
            bool payloadValid =
                published &&
                dequeued &&
                noTail &&
                VoxelChunkModifiedEvents.PendingCount == 0 &&
                observed.VolumeInstanceId == expected.VolumeInstanceId &&
                math.all(observed.MinAbsoluteCell == expected.MinAbsoluteCell) &&
                math.all(observed.MaxAbsoluteCell == expected.MaxAbsoluteCell) &&
                math.abs(observed.VoxelSize - expected.VoxelSize) < 0.0001f &&
                observed.Frame == expected.Frame &&
                observed.Operation == expected.Operation &&
                observed.Shape == expected.Shape &&
                observed.Flags == expected.Flags &&
                observed.StateHash == expected.StateHash;

            VoxelChunkModifiedEvent invalid = expected;
            invalid.VoxelSize = float.NaN;
            invalid.StateHash = 0xBADF00Du;
            int rejectedBefore = VoxelChunkModifiedEvents.DebugRejectedCount;
            bool invalidRejected =
                !VoxelChunkModifiedEvents.TryPublish(in invalid) &&
                VoxelChunkModifiedEvents.DebugRejectedCount == rejectedBefore + 1 &&
                VoxelChunkModifiedEvents.DebugLastRejectedStateHash == invalid.StateHash;

            DrainVoxelChunkModifiedEvents();
            int droppedBefore = VoxelChunkModifiedEvents.DebugDroppedCount;
            const uint OverflowBaseHash = 0xABC00000u;
            for (int i = 0; i <= VoxelChunkModifiedEvents.DebugCapacity; i++)
            {
                expected.Frame = (uint)i;
                expected.StateHash = OverflowBaseHash + (uint)i;
                if (!VoxelChunkModifiedEvents.TryPublish(in expected))
                    return Fail("Voxel chunk modified event overflow publish rejected.");
            }

            bool overflowDequeued = VoxelChunkModifiedEvents.TryDequeue(out VoxelChunkModifiedEvent overflowObserved);
            bool overflowValid =
                overflowDequeued &&
                overflowObserved.StateHash == OverflowBaseHash + 1u &&
                VoxelChunkModifiedEvents.DebugDroppedCount == droppedBefore + 1 &&
                VoxelChunkModifiedEvents.DebugLastDroppedStateHash == OverflowBaseHash;
            DrainVoxelChunkModifiedEvents();

#if UNITY_EDITOR
            string delta = ReadProjectFile("Assets/_Project/Scripts/VoxelDeltaProcessor.cs");
            string eventsSource = ReadProjectFile("Assets/_Project/Scripts/VoxelChunkModifiedEvents.cs");
            bool sourceContract =
                delta.IndexOf("PublishVoxelChunkModifiedEvent(volume, voxelSize)", System.StringComparison.Ordinal) >= 0 &&
                delta.IndexOf("VoxelChunkModifiedEvents.TryPublish(in modifiedEvent)", System.StringComparison.Ordinal) >= 0 &&
                eventsSource.IndexOf("public static bool TryPublish", System.StringComparison.Ordinal) >= 0 &&
                eventsSource.IndexOf("DebugRejectedCount", System.StringComparison.Ordinal) >= 0 &&
                eventsSource.IndexOf("DebugDroppedCount", System.StringComparison.Ordinal) >= 0 &&
                eventsSource.IndexOf("NativeQueue<VoxelChunkModifiedEvent> _events", System.StringComparison.Ordinal) >= 0 &&
                eventsSource.IndexOf("NativeMemorySentinel.RegisterNativeQueue", System.StringComparison.Ordinal) >= 0 &&
                eventsSource.IndexOf("private const int Capacity = 64", System.StringComparison.Ordinal) >= 0;
#else
            bool sourceContract = true;
#endif

            return Require(
                VoxelChunkModifiedEvents.DebugCapacity == 64 &&
                VoxelChunkModifiedEvents.DebugEventBytes == 64 &&
                VoxelChunkModifiedEvents.PendingCount == 0 &&
                payloadValid &&
                invalidRejected &&
                overflowValid &&
                sourceContract,
                "Voxel chunk modified event contract failed.");
        }

        private bool ValidateAsyncCarveSourceContracts()
        {
#if UNITY_EDITOR
            string delta = ReadProjectFile("Assets/_Project/Scripts/VoxelDeltaProcessor.cs");
            string chunkEvents = ReadProjectFile("Assets/_Project/Scripts/VoxelChunkModifiedEvents.cs");
            string engine = ReadProjectFile("Assets/_Project/Scripts/HectonVoxelEngine.cs");
            string shader = ReadProjectFile("Assets/_Project/Art/Shaders/Hecton_AbyssalVoxelRock.shader");
            return RequireContains(delta, "VaultGenerationHandle<VoxelCarveEvent> _queuedCarveEventsHandle", "Missing vault-owned carve ingress handle.") &&
                   RequireContains(delta, "BufferID.ShinobuDeltaCrusherCarveEventQueue", "Missing DataVault carve event queue route.") &&
                   RequireContains(delta, "public bool TryQueueCarveEvent(HectonVoxelVolume volume, in VoxelCarveEvent carveEvent)", "Missing public carve event enqueue contract.") &&
                   RequireContains(delta, "private static int ResolveQueuedCarveDrainBudget(float qualityWeight01)", "Missing continuous carve drain resolver.") &&
                   RequireContains(delta, "ResolveQueuedCarveDrainBudgetPerFrame(ResolveGlobalQualityWeight01())", "Runtime carve drain does not consume GlobalQualityWeight.") &&
                   RequireContains(delta, "math.lerp(", "Continuous carve drain interpolation missing.") &&
                   RequireNotContains(delta, "ResolveQualityWeightFromTier", "Carve drain must not map hardware tiers to quality weight.") &&
                   RequireNotContains(delta, "DebugResolveQueuedCarveDrainBudget(HectonQualityTier", "Carve debug proof must not require tier inputs.") &&
                   RequireContains(delta, "private unsafe struct CarveSdfJob : IJobParallelFor", "Missing Burst-scheduled parallel carve job.") &&
                   RequireContains(delta, "AxisWeightedLengthApprox", "Axis-weighted carve distance approximation missing.") &&
                   RequireContains(delta, "WriteDirtySparseRleNativeSnapshotChunk", "Dirty sparse RLE snapshot writer missing.") &&
                   RequireContains(delta, "WriteCompactedSparseRleNativeSnapshotChunk", "Compacted sparse RLE snapshot writer missing.") &&
                   RequireContains(delta, "VoxelDynamicNavGridRuntime.QueueLocalizedSdfPatch", "Localized nav-grid patch emission missing.") &&
                   RequireContains(delta, "VoxelChunkModifiedEvents.TryPublish(in modifiedEvent)", "Voxel chunk modified event publish missing.") &&
                   RequireContains(chunkEvents, "public struct VoxelChunkModifiedEvent", "Voxel chunk modified event payload missing.") &&
                   RequireContains(chunkEvents, "public static bool TryPublish", "Voxel chunk modified event validated publish path missing.") &&
                   RequireContains(chunkEvents, "DebugDroppedCount", "Voxel chunk modified overflow telemetry missing.") &&
                   RequireContains(chunkEvents, "DebugRejectedCount", "Voxel chunk modified rejection telemetry missing.") &&
                   RequireContains(chunkEvents, "NativeQueue<VoxelChunkModifiedEvent>", "Voxel chunk modified event native queue missing.") &&
                   RequireContains(delta, "PublishDebrisSpawnSignal(in request, radius)", "Immediate dust/debris signal dispatch missing.") &&
                   RequireNotContains(delta, "DecalProjector", "Voxel laser burn path must not depend on DecalProjector.") &&
                   RequireNotContains(engine, "Physics." + "BakeMesh", "Runtime PhysX mesh cooking must stay absent.") &&
                   RequireContains(engine, "modifiedCells = data.ModifiedCells", "Modified-cell map is not fed into vertex color job.") &&
                   RequireContains(engine, "colorPayload.x = 1f", "Vertex color R burn write missing.") &&
                   RequireContains(shader, "vertexBurnMask", "Shader vertex burn mask missing.") &&
                   RequireContains(shader, "terrainSplatColor.r", "Shader does not read vertex color R for burn.");
#else
            return true;
#endif
        }

        private static void DrainVoxelChunkModifiedEvents()
        {
            int limit = VoxelChunkModifiedEvents.DebugCapacity + 4;
            for (int i = 0; i < limit && VoxelChunkModifiedEvents.TryDequeue(out _); i++)
            {
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

#if UNITY_EDITOR
        private static string ReadProjectFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
        }

        private bool RequireContains(string source, string needle, string issue)
        {
            return Require(source.IndexOf(needle, System.StringComparison.Ordinal) >= 0, issue);
        }

        private bool RequireNotContains(string source, string needle, string issue)
        {
            return Require(source.IndexOf(needle, System.StringComparison.Ordinal) < 0, issue);
        }
#endif

        private bool Require(bool condition, string issue)
        {
            return condition || Fail(issue);
        }

        private bool Fail(string issue)
        {
            _debugLastIssue = issue;
            _debugLastPass = false;
#if UNITY_EDITOR
            Hecton8.Core.H8Debug.LogWarning(issue, this);
#endif
            return false;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void Log(string message)
        {
            if (verboseLogging)
                Hecton8.Core.H8Debug.Log(message, this);
        }
    }
}
