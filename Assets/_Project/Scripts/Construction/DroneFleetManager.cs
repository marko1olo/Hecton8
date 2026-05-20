using Hecton.Localization;
using Hecton8.Caves;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.Power;
using Hecton8.SaveSystem;
using Hecton8.Vehicles.Automation;
using Hecton8.World;
using System;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Construction
{
    internal enum DroneFleetTaskKind : byte
    {
        None = 0,
        RepairModule = 1,
        CutParasite = 2,
        MineNode = 3
    }

    internal readonly struct DroneFleetTask
    {
        public readonly DroneFleetTaskKind Kind;
        public readonly BaseModule Module;
        public readonly Vector3 Position;
        public readonly float Radius;

        public DroneFleetTask(DroneFleetTaskKind kind, BaseModule module, Vector3 position, float radius)
        {
            Kind = kind;
            Module = module;
            Position = position;
            Radius = radius;
        }

        public bool IsValid()
        {
            return Kind != DroneFleetTaskKind.None && Module != null;
        }
    }

    /// <summary>
    /// Read-only fleet snapshot consumed by diagnostics owners such as the submarine OS.
    /// </summary>
    public readonly struct HectonDroneFleetSnapshot
    {
        public readonly int ActiveHubCount;
        public readonly int ActiveDroneCount;
        public readonly int AssignedTaskCount;
        public readonly int DockedStasisSlotCount;
        public readonly int DestroyedDroneCount;
        public readonly bool EmergencyOverclockActive;
        public readonly SubmarineEmergencyLevel EmergencyLevel;
        public readonly float AverageBatteryPercent;
        public readonly int SolderReserve;
        public readonly int HostileDroneCount;
        public readonly int LogicLeechHijackCount;

        public HectonDroneFleetSnapshot(
            int activeHubCount,
            int activeDroneCount,
            int assignedTaskCount,
            int dockedStasisSlotCount,
            int destroyedDroneCount,
            bool emergencyOverclockActive,
            SubmarineEmergencyLevel emergencyLevel,
            float averageBatteryPercent,
            int solderReserve,
            int hostileDroneCount,
            int logicLeechHijackCount)
        {
            ActiveHubCount = activeHubCount;
            ActiveDroneCount = activeDroneCount;
            AssignedTaskCount = assignedTaskCount;
            DockedStasisSlotCount = dockedStasisSlotCount;
            DestroyedDroneCount = destroyedDroneCount;
            EmergencyOverclockActive = emergencyOverclockActive;
            EmergencyLevel = emergencyLevel;
            AverageBatteryPercent = averageBatteryPercent;
            SolderReserve = solderReserve;
            HostileDroneCount = hostileDroneCount;
            LogicLeechHijackCount = logicLeechHijackCount;
        }
    }

    /// <summary>
    /// Burst-accumulated fleet status payload published to the global telemetry ring and OS bridge.
    /// </summary>
    public readonly struct FleetStatusSnapshot
    {
        public readonly int TotalActive;
        public readonly float AverageBattery;
        public readonly int SolderReserve;
        public readonly int LostUnits;
        public readonly int HostileUnits;

        public FleetStatusSnapshot(int totalActive, float averageBattery, int solderReserve, int lostUnits, int hostileUnits)
        {
            TotalActive = totalActive;
            AverageBattery = averageBattery;
            SolderReserve = solderReserve;
            LostUnits = lostUnits;
            HostileUnits = hostileUnits;
        }
    }

    /// <summary>
    /// Fleet telemetry bridge. The submarine OS and any diegetic diagnostics can subscribe without scene scans.
    /// </summary>
    public interface IDroneFleetSnapshotEventListener
    {
        /// <summary>
        /// Receives one late-frame drone fleet snapshot update.
        /// </summary>
        /// <param name="snapshot">Read-only fleet snapshot.</param>
        void OnDroneFleetSnapshotUpdated(in HectonDroneFleetSnapshot snapshot);
    }

    /// <summary>
    /// Blittable snapshot payload queued before dispatch to fleet snapshot listeners.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 48)]
    public struct HectonDroneFleetSnapshotPayload
    {
        public int ActiveHubCount;
        public int ActiveDroneCount;
        public int AssignedTaskCount;
        public int DockedStasisSlotCount;
        public int DestroyedDroneCount;
        public int EmergencyLevel;
        public float AverageBatteryPercent;
        public int SolderReserve;
        public int HostileDroneCount;
        public int LogicLeechHijackCount;
        public byte EmergencyOverclockActive;
        private byte _padding0;
        private byte _padding1;
        private byte _padding2;
        private uint _paddingTail;
    }

    /// <summary>
    /// Vault-array-backed fleet telemetry bridge drained by <see cref="SystemDispatcher"/>.
    /// </summary>
    public static class HectonDroneFleetEvents
    {
        private const int ListenerCapacity = 8;
        private const int PendingEventCapacity = 64;
        private const BufferID PendingEventBufferId = (BufferID)70271;
        private const BufferID NextFrameEventBufferId = (BufferID)70272;

        private static readonly uint _overflowWarningHash = unchecked((uint)LocHash.Compute("HectonDroneFleetEvents.Overflow"));
        private static readonly uint _queueHash = unchecked((uint)LocHash.Compute("HectonDroneFleetEvents"));

        // COLD ALLOC: RegistryBucket<IDroneFleetSnapshotEventListener>[8] - fleet snapshot listeners drained by SystemDispatcher LateUpdate - owner: HectonDroneFleetEvents
        private static readonly RegistryBucket<IDroneFleetSnapshotEventListener> _listeners = new RegistryBucket<IDroneFleetSnapshotEventListener>(ListenerCapacity);

        private static NativeArray<HectonDroneFleetSnapshotPayload> _pendingEvents;
        private static NativeArray<HectonDroneFleetSnapshotPayload> _nextFrameEvents;
        private static VaultBufferHandle<HectonDroneFleetSnapshotPayload> _pendingEventsHandle;
        private static VaultBufferHandle<HectonDroneFleetSnapshotPayload> _nextFrameEventsHandle;
        private static bool _pendingEventsVaultBacked;
        private static bool _nextFrameEventsVaultBacked;
        private static int _pendingEventCount;
        private static int _pendingEventReadIndex;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;
        private static int _lastOverflowWarningFrame = -1;

        /// <summary>
        /// Number of pending fleet snapshot payloads waiting for late-frame dispatch.
        /// </summary>
        public static int PendingCount => math.max(0, _pendingEventCount - _pendingEventReadIndex) + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                DroneFleetManager.ReleaseDroneVaultBuffer(
                    ref _pendingEvents,
                    ref _pendingEventsHandle,
                    ref _pendingEventsVaultBacked,
                    nameof(_pendingEvents));
            }

            if (_nextFrameEvents.IsCreated)
            {
                DroneFleetManager.ReleaseDroneVaultBuffer(
                    ref _nextFrameEvents,
                    ref _nextFrameEventsHandle,
                    ref _nextFrameEventsVaultBacked,
                    nameof(_nextFrameEvents));
            }

            _listeners.Clear();
            _pendingEventCount = 0;
            _pendingEventReadIndex = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
            _lastOverflowWarningFrame = -1;
        }

        /// <summary>
        /// Registers a fleet snapshot listener.
        /// </summary>
        public static void Register(IDroneFleetSnapshotEventListener listener)
        {
            if (listener == null)
                return;

            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        /// <summary>
        /// Unregisters a fleet snapshot listener.
        /// </summary>
        public static void Unregister(IDroneFleetSnapshotEventListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        internal static void RaiseSnapshotUpdated(in HectonDroneFleetSnapshot snapshot)
        {
            if (_listeners.Count <= 0)
                return;

            Enqueue(new HectonDroneFleetSnapshotPayload
            {
                ActiveHubCount = snapshot.ActiveHubCount,
                ActiveDroneCount = snapshot.ActiveDroneCount,
                AssignedTaskCount = snapshot.AssignedTaskCount,
                DockedStasisSlotCount = snapshot.DockedStasisSlotCount,
                DestroyedDroneCount = snapshot.DestroyedDroneCount,
                EmergencyLevel = (int)snapshot.EmergencyLevel,
                AverageBatteryPercent = snapshot.AverageBatteryPercent,
                SolderReserve = snapshot.SolderReserve,
                HostileDroneCount = snapshot.HostileDroneCount,
                LogicLeechHijackCount = snapshot.LogicLeechHijackCount,
                EmergencyOverclockActive = snapshot.EmergencyOverclockActive ? (byte)1 : (byte)0
            });
        }

        /// <summary>
        /// Flushes pending fleet snapshots to registered listeners.
        /// </summary>
        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated)
                return;

            PromoteNextFrameEventsIfFrontEmpty();
            int scanBudget = math.max(0, _pendingEventCount - _pendingEventReadIndex);
            while (scanBudget-- > 0 && _pendingEventReadIndex < _pendingEventCount)
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                HectonDroneFleetSnapshotPayload payload = _pendingEvents[_pendingEventReadIndex++];

                HectonDroneFleetSnapshot snapshot = new HectonDroneFleetSnapshot(
                    payload.ActiveHubCount,
                    payload.ActiveDroneCount,
                    payload.AssignedTaskCount,
                    payload.DockedStasisSlotCount,
                    payload.DestroyedDroneCount,
                    payload.EmergencyOverclockActive != 0,
                    (SubmarineEmergencyLevel)payload.EmergencyLevel,
                    payload.AverageBatteryPercent,
                    payload.SolderReserve,
                    payload.HostileDroneCount,
                    payload.LogicLeechHijackCount);

                IDroneFleetSnapshotEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IDroneFleetSnapshotEventListener listener = rawArray[i];
                        if (listener != null)
                            listener.OnDroneFleetSnapshotUpdated(in snapshot);
                    }
                }
                finally
                {
                    _isDispatching = false;
                }
            }

            if (_pendingEventReadIndex >= _pendingEventCount)
            {
                _pendingEventCount = 0;
                _pendingEventReadIndex = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = DroneFleetManager.ResolveDroneVaultBuffer(
                    PendingEventBufferId,
                    PendingEventCapacity,
                    NativeArrayOptions.ClearMemory,
                    ref _pendingEventsHandle,
                    out _pendingEventsVaultBacked); // COLD ALLOC: NativeArray<HectonDroneFleetSnapshotPayload>[64] - deferred drone fleet snapshot lane flushed by SystemDispatcher LateUpdate - owner: GlobalDataVault/H8Memory fallback
                DroneFleetManager.RegisterNativeArrayIfFallback(
                    _pendingEvents,
                    _pendingEventsVaultBacked,
                    nameof(_pendingEvents));
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = DroneFleetManager.ResolveDroneVaultBuffer(
                    NextFrameEventBufferId,
                    PendingEventCapacity,
                    NativeArrayOptions.ClearMemory,
                    ref _nextFrameEventsHandle,
                    out _nextFrameEventsVaultBacked); // COLD ALLOC: NativeArray<HectonDroneFleetSnapshotPayload>[64] - next-frame drone fleet snapshot lane prevents same-frame reentrant dispatch - owner: GlobalDataVault/H8Memory fallback
                DroneFleetManager.RegisterNativeArrayIfFallback(
                    _nextFrameEvents,
                    _nextFrameEventsVaultBacked,
                    nameof(_nextFrameEvents));
            }
        }

        private static bool Enqueue(in HectonDroneFleetSnapshotPayload payload)
        {
            if (_listeners.Count <= 0)
                return false;

            if (PendingCount >= PendingEventCapacity)
            {
                ReportOverflowOncePerFrame();
                return false;
            }

            EnsureInitialized();
            if (_isDispatching)
            {
                if (_nextFrameEventCount >= PendingEventCapacity)
                {
                    ReportOverflowOncePerFrame();
                    return false;
                }

                _nextFrameEvents[_nextFrameEventCount] = payload;
                _nextFrameEventCount++;
                return true;
            }

            CompactPendingEventsIfNeeded();
            if (_pendingEventCount >= PendingEventCapacity)
            {
                ReportOverflowOncePerFrame();
                return false;
            }

            _pendingEvents[_pendingEventCount] = payload;
            _pendingEventCount++;
            return true;
        }

        private static void ReportOverflowOncePerFrame()
        {
            int frame = Time.frameCount;
            if (_lastOverflowWarningFrame == frame)
                return;

            _lastOverflowWarningFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(_overflowWarningHash, _queueHash, PendingEventCapacity);
        }

        private static void CompactPendingEventsIfNeeded()
        {
            if (!_pendingEvents.IsCreated || _pendingEventReadIndex <= 0)
                return;

            int activeCount = math.max(0, _pendingEventCount - _pendingEventReadIndex);
            for (int i = 0; i < activeCount; i++)
                _pendingEvents[i] = _pendingEvents[_pendingEventReadIndex + i];

            _pendingEventCount = activeCount;
            _pendingEventReadIndex = 0;
        }

        private static void PromoteNextFrameEventsIfFrontEmpty()
        {
            if (!_pendingEvents.IsCreated ||
                !_nextFrameEvents.IsCreated ||
                _pendingEventReadIndex < _pendingEventCount ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            for (int i = 0; i < _nextFrameEventCount; i++)
                _pendingEvents[i] = _nextFrameEvents[i];

            _pendingEventCount = _nextFrameEventCount;
            _pendingEventReadIndex = 0;
            _nextFrameEventCount = 0;
        }
    }

    /// <summary>
    /// Central zero-alloc fleet arbitration owner for repair drones.
    /// Runtime drone bodies are stored in native state arrays and rendered indirectly.
    /// </summary>
    internal static class DroneFleetManager
    {
        private const int InitialTaskCapacity = 64;
        private const int MaxOperationalDroneCount = 500;
        private const int HeadlessDroneCapacity = 512;
        private const int DroneJobBatchSize = 64;
        private const int PhantomDroneCount = 500;
        private const int LowTierPhantomDroneCount = 0;
        private const int MidTierPhantomDroneCount = 192;
        private const int HighTierPhantomDroneCount = 384;
        private const int PhantomDroneThreadGroupSize = 64;
        private const int HeadlessTaskCapacity = 64;
        private const int HeadlessPendingLaunchCapacity = HeadlessDroneCapacity;
        private const int DroneServiceCommandCapacity = HeadlessDroneCapacity * 3;
        private const int DroneSpatialBucketCapacity = 2048;
        private const int DroneAStarGridSide = 8;
        private const int DroneAStarNodeCapacity = DroneAStarGridSide * DroneAStarGridSide * DroneAStarGridSide;
        private const int DroneAStarTelemetryCapacity = 1;
        private const int DroneAStarRouteNodeStride = 8;
        private const int DroneAStarRouteNodeCapacity = HeadlessDroneCapacity * DroneAStarRouteNodeStride;
        private const int DroneAStarRouteDebugPointCount = 4;
        private const int DockingObstacleProbeMaxSegments = 3;
        private const int DockingRaycastCapacity = HeadlessDroneCapacity * DockingObstacleProbeMaxSegments;
        private const int DockingRaycastMinCommandsPerJob = 8;
        private const int DroneFleetBlackBoxFrameCapacity = 300;
        private const int MaxMainThreadTaskScanCount = 64;
        private const int MaxMainThreadHubScanCount = 8;
        private const int DefaultMaxClaimsPerTarget = 2;
        private const int InvalidHubId = 0;
        private const int EmptyTaskIndex = -1;
        private const string NativeMemoryOwner = nameof(DroneFleetManager);
        private const string DroneFleetBlackBoxDumpPath = "Docs/AgentLogs/Dump_FLEET_COMMANDER.bin";
        private const string DroneFleetLegacyBlackBoxDumpPath = "Docs/AgentLogs/Dump_DRONE_FLEET.bin";
        private const string DroneFleetBlackBoxH8DumpPath = "Docs/AgentLogs/Dump_DRONE_FLEET.h8dump";
        private const string DroneSpecsCsvFileName = "drone_chassis_specs.csv";
        private const string DroneSpecsCsvLegacyFileName = "drone_specs.csv";
        private const int DroneSpecsCsvMaxBytes = 16 * 1024;
        private const int DroneChassisSpecCapacity = 8;
        private const uint DroneChassisSpecValidFlag = 1u;
        private const uint DroneChassisRepairHash = 0x29520BB4u;
        private const uint DroneChassisMiningHash = 0x2FF741A1u;
        private const uint DroneChassisCombatHash = 0x1CE36E21u;
        private const uint DroneChassisCutParasiteHash = 0x64C86046u;
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;
        private const float MinimumScoreDistanceMeters = 0.75f;
        private const float MinimumScoreDistanceMetersSq = MinimumScoreDistanceMeters * MinimumScoreDistanceMeters;
        private const float RuptureCriticalityBonus = 2.5f;
        private const float FloodCriticalityBonus = 2f;
        private const float BreachCriticalityBonus = 3f;
        private const float CascadeCriticalityBonus = 1.5f;
        private const float ParasiteCriticalityBonus = 4f;
        private const float AirReserveCriticalityScale = 1.5f;
        private const float EmergencyCriticalityScale = 1.35f;
        private const float SeparationDistanceEpsilon = 0.0001f;
        private const float HeadlessTaskRebuildIntervalSeconds = 0.5f;
        private const float HeadlessDefaultSpeedMetersPerSecond = 6.5f;
        private const float HeadlessBatteryDrainPercentPerSecond = 2.5f;
        private const float HeadlessServiceRadiusMeters = 1f;
        private const float HeadlessWeldPowerNormalized = 0.75f;
        private const float HeadlessWeldRangeMeters = 1.25f;
        private const uint DroneRepairSparksSignalHash = 0x44525350u;
        private const int DroneInventoryCopperHash = 0x43555052;
        private const byte DroneRepairSparkDebrisKind = 1;
        private const float SolderIntegrityUnitsPerPack = 10f;
        private const float OrphanWanderDistanceMeters = 4f;
        private const float DroneFlowDragCoefficient = 0.85f;
        private const float DockingObstacleProbeEndpointTrimMeters = 0.35f;
        private const float DockingMinimumProbeDistanceMeters = 0.25f;
        private const int FleetTelemetryPublishFrameInterval = 60;
        private const string DroneCullingComputeAssetPath = "Assets/_Project/Art/Shaders/DroneCulling.compute";
        private const string PhantomDronesComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_PhantomDrones.compute";
        private const string DroneProceduralShaderName = "Hecton8/Construction/DroneFleetProcedural";
        private const uint DroneProceduralVerticesPerInstance = 36u;
        private const float DroneProceduralScaleMeters = 0.28f;
        private const BufferID DroneFleetStateDtoBufferId = (BufferID)70265;
        private const BufferID DroneFleetTargetDtoBufferId = (BufferID)70266;
        private const BufferID DroneFleetAssignmentTasksBufferId = (BufferID)70267;
        private const BufferID DroneFleetProceduralArgsBufferId = (BufferID)70268;
        private const BufferID DroneFleetServiceCommandsBufferId = (BufferID)70269;
        private const BufferID DroneFleetServiceCommandCursorBufferId = (BufferID)70270;
        private const BufferID DroneFleetSpatialBucketHeadsBufferId = (BufferID)70273;
        private const BufferID DroneFleetSpatialNextIndicesBufferId = (BufferID)70274;
        private const BufferID DroneFleetSpatialKeysBufferId = (BufferID)70275;
        private const BufferID DroneFleetChassisSpecsBufferId = (BufferID)12870276;
        private const BufferID DroneFleetCsvScratchBufferId = (BufferID)12870277;
        private const float DroneCullRadiusMeters = 1.25f;
        private const float LowTierDroneRenderDistanceMeters = 50f;
        private const float MidTierDroneRenderDistanceMeters = 100f;
        private const float HighTierDroneRenderDistanceMeters = 150f;
        private const float PhantomDroneOrbitRadiusMeters = 20f;
        private const float PhantomDroneVerticalAmplitudeMeters = 4.5f;
        private const float PhantomDroneScaleMeters = 0.18f;
        private const float PhantomDroneBoundsDiameterMeters = 64f;
        private const float PhantomDronePhaseWrapSeconds = 60f;
        private const float DroneRelaySubmarineDistanceMeters = 100f;
        private const float DroneRelayScanRadiusMeters = 160f;
        private const float DroneRelayPingRadiusMeters = 220f;
        private const float DroneRelayPingLifetimeSeconds = 4f;
        private const int MaxDroneRelayContacts = 16;

        [StructLayout(LayoutKind.Sequential, Size = 80)]
        private struct DroneRenderInstance
        {
            public float4x4 Matrix;
            public float TransactionProgress;
            public float3 Padding;
        }

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct DroneCullingStateGpu
        {
            public float3 Position;
            public uint PackedStateFactionCorridor;
        }

        private struct RepairTaskCandidate
        {
            public DroneFleetTaskKind Kind;
            public BaseModule Module;
            public int ModuleIndex;
            public Vector3 Position;
            public float Radius;
            public float Score;
            public float CriticalityWeight;
        }

        private struct PendingDroneLaunch
        {
            public bool Active;
            public int DroneSlot;
            public int DroneId;
            public RepairDroneHub Hub;
            public DroneFleetTask Task;
            public Vector3 HomePosition;
            public Quaternion HomeRotation;
            public float RepairRatePerSecond;
            public int LoadedSolderUnits;
        }

        [StructLayout(LayoutKind.Sequential, Size = 80)]
        private struct DroneFleetBlackBoxEntry
        {
            public int Frame;
            public int ActiveCount;
            public int StateHash;
            public int Flags;
            public float DeltaTime;
            public int DockingAborts;
            public int PathSolves;
            public int PathFailures;
            public int PathIterations;
            public float AveragePathfindingTimeMs;
            public int TasksCompleted;
            public float3 FirstPosition;
            public float3 BoundsCenter;
            public float3 BoundsExtents;
        }

        private sealed class HeadlessFleetDriver : IUpdatable, ILateFrameTickable, IRenderable
        {
            public void Tick(float deltaTime)
            {
                ScheduleHeadlessSimulation(deltaTime);
            }

            public void LateFrameTick()
            {
                CompleteHeadlessSimulationAndApply();
            }

            public void Render(float deltaTime)
            {
                RenderHeadlessFleet(deltaTime);
            }
        }

        // COLD ALLOC: HeadlessFleetDriver[1] - registry adapter for headless drone simulation and rendering - owner: DroneFleetManager
        private static readonly HeadlessFleetDriver s_HeadlessDriver = new HeadlessFleetDriver();
        private static NativeArray<int> s_TaskClaimCounts;
        private static NativeArray<HeadlessDroneState> s_DroneStates;
        private static NativeArray<HeadlessDroneState> s_DroneStateBackBuffer;
        private static NativeArray<float4x4> s_DroneRenderMatrices;
        private static NativeArray<float4x4> s_DroneRenderMatrixBackBuffer;
        private static NativeArray<DroneRenderInstance> s_DroneRenderInstances;
        private static NativeArray<DroneCullingStateGpu> s_DroneCullingStates;
        private static bool s_TaskClaimCountsVaultBacked;
        private static bool s_DroneStatesVaultBacked;
        private static bool s_DroneStateBackBufferVaultBacked;
        private static bool s_DroneRenderMatricesVaultBacked;
        private static bool s_DroneRenderMatrixBackBufferVaultBacked;
        private static bool s_DroneRenderInstancesVaultBacked;
        private static bool s_DroneCullingStatesVaultBacked;
        private static NativeArray<float3> s_DronePositionsSoA;
        private static NativeArray<byte> s_DroneStateBytes;
        private static NativeArray<DroneFleetBlackBoxEntry> s_DroneBlackBox;
        private static NativeArray<DroneFleetTuningConstants> s_DroneTuningConstants;
        private static NativeArray<PathWaypointDTO> s_DroneMacroWaypoints;
        private static NativeArray<byte> s_DroneMacroWaypointStates;
        private static NativeArray<DroneNativeMinHeapNode> s_DroneAStarOpenHeap;
        private static NativeArray<float> s_DroneAStarGCosts;
        private static NativeArray<int> s_DroneAStarCameFrom;
        private static NativeArray<byte> s_DroneAStarNodeStates;
        private static NativeArray<int> s_DroneMacroRouteNodes;
        private static NativeArray<byte> s_DroneMacroRouteCounts;
        private static NativeArray<DroneAStarTelemetry> s_DroneAStarTelemetry;
        private static NativeArray<int> s_HeadlessTaskClaimOwners;
        private static NativeArray<int> s_FleetTelemetryAccumulator;
        private static NativeArray<RaycastCommand> s_DockingRaycastCommands;
        private static NativeArray<RaycastHit> s_DockingRaycastHits;
        private static NativeArray<int> s_DockingRaycastSlots;
        private static JobHandle s_DockingRaycastHandle;
        private static int s_DockingRaycastCount;
        private static bool s_DockingRaycastScheduled;
        private static NativeArray<DroneTaskDTO> s_DroneTaskPriorityHeap;
        private static NativeArray<DroneStateDTO> s_DroneStateDtos;
        private static NativeArray<DroneTargetDTO> s_DroneTargetDtos;
        private static NativeArray<DroneTaskDTO> s_DroneAssignmentTasks;
        private static NativeArray<DroneProceduralIndirectArgsDTO> s_DroneProceduralArgs;
        private static NativeArray<int> s_DroneSpatialBucketHeads;
        private static NativeArray<int> s_DroneSpatialNextIndices;
        private static NativeArray<int> s_DroneSpatialKeys;
        private static NativeArray<DroneChassisSpecDTO> s_DroneChassisSpecs;
        private static NativeArray<byte> s_DroneSpecsCsvScratch;
        private static VaultBufferHandle<int> s_TaskClaimCountsHandle;
        private static VaultBufferHandle<HeadlessDroneState> s_DroneStatesHandle;
        private static VaultBufferHandle<HeadlessDroneState> s_DroneStateBackBufferHandle;
        private static VaultBufferHandle<float4x4> s_DroneRenderMatricesHandle;
        private static VaultBufferHandle<float4x4> s_DroneRenderMatrixBackBufferHandle;
        private static VaultBufferHandle<DroneRenderInstance> s_DroneRenderInstancesHandle;
        private static VaultBufferHandle<DroneCullingStateGpu> s_DroneCullingStatesHandle;
        private static VaultBufferHandle<float3> s_DronePositionsSoAHandle;
        private static VaultBufferHandle<byte> s_DroneStateBytesHandle;
        private static VaultBufferHandle<DroneFleetBlackBoxEntry> s_DroneBlackBoxHandle;
        private static VaultBufferHandle<DroneFleetTuningConstants> s_DroneTuningConstantsHandle;
        private static VaultBufferHandle<PathWaypointDTO> s_DroneMacroWaypointsHandle;
        private static VaultBufferHandle<byte> s_DroneMacroWaypointStatesHandle;
        private static VaultBufferHandle<DroneNativeMinHeapNode> s_DroneAStarOpenHeapHandle;
        private static VaultBufferHandle<float> s_DroneAStarGCostsHandle;
        private static VaultBufferHandle<int> s_DroneAStarCameFromHandle;
        private static VaultBufferHandle<byte> s_DroneAStarNodeStatesHandle;
        private static VaultBufferHandle<int> s_DroneMacroRouteNodesHandle;
        private static VaultBufferHandle<byte> s_DroneMacroRouteCountsHandle;
        private static VaultBufferHandle<DroneAStarTelemetry> s_DroneAStarTelemetryHandle;
        private static VaultBufferHandle<int> s_HeadlessTaskClaimOwnersHandle;
        private static VaultBufferHandle<int> s_FleetTelemetryAccumulatorHandle;
        private static VaultBufferHandle<RaycastCommand> s_DockingRaycastCommandsHandle;
        private static VaultBufferHandle<RaycastHit> s_DockingRaycastHitsHandle;
        private static VaultBufferHandle<int> s_DockingRaycastSlotsHandle;
        private static VaultBufferHandle<DroneTaskDTO> s_DroneTaskPriorityHeapHandle;
        private static VaultBufferHandle<DroneStateDTO> s_DroneStateDtosHandle;
        private static VaultBufferHandle<DroneTargetDTO> s_DroneTargetDtosHandle;
        private static VaultBufferHandle<DroneTaskDTO> s_DroneAssignmentTasksHandle;
        private static VaultBufferHandle<DroneProceduralIndirectArgsDTO> s_DroneProceduralArgsHandle;
        private static VaultBufferHandle<DroneServiceCommand> s_DroneServiceCommandsHandle;
        private static VaultBufferHandle<DroneServiceCommandCursor> s_DroneServiceCommandCursorHandle;
        private static VaultBufferHandle<int> s_DroneSpatialBucketHeadsHandle;
        private static VaultBufferHandle<int> s_DroneSpatialNextIndicesHandle;
        private static VaultBufferHandle<int> s_DroneSpatialKeysHandle;
        private static VaultBufferHandle<DroneChassisSpecDTO> s_DroneChassisSpecsHandle;
        private static VaultBufferHandle<byte> s_DroneSpecsCsvScratchHandle;
        private static bool s_DronePositionsSoAVaultBacked;
        private static bool s_DroneStateBytesVaultBacked;
        private static bool s_DroneBlackBoxVaultBacked;
        private static bool s_DroneTuningConstantsVaultBacked;
        private static bool s_DroneMacroWaypointsVaultBacked;
        private static bool s_DroneMacroWaypointStatesVaultBacked;
        private static bool s_DroneAStarOpenHeapVaultBacked;
        private static bool s_DroneAStarGCostsVaultBacked;
        private static bool s_DroneAStarCameFromVaultBacked;
        private static bool s_DroneAStarNodeStatesVaultBacked;
        private static bool s_DroneMacroRouteNodesVaultBacked;
        private static bool s_DroneMacroRouteCountsVaultBacked;
        private static bool s_DroneAStarTelemetryVaultBacked;
        private static bool s_HeadlessTaskClaimOwnersVaultBacked;
        private static bool s_FleetTelemetryAccumulatorVaultBacked;
        private static bool s_DockingRaycastCommandsVaultBacked;
        private static bool s_DockingRaycastHitsVaultBacked;
        private static bool s_DockingRaycastSlotsVaultBacked;
        private static bool s_DroneTaskPriorityHeapVaultBacked;
        private static bool s_DroneStateDtosVaultBacked;
        private static bool s_DroneTargetDtosVaultBacked;
        private static bool s_DroneAssignmentTasksVaultBacked;
        private static bool s_DroneProceduralArgsVaultBacked;
        private static bool s_DroneServiceCommandsVaultBacked;
        private static bool s_DroneServiceCommandCursorVaultBacked;
        private static bool s_DroneSpatialBucketHeadsVaultBacked;
        private static bool s_DroneSpatialNextIndicesVaultBacked;
        private static bool s_DroneSpatialKeysVaultBacked;
        private static bool s_DroneChassisSpecsVaultBacked;
        private static bool s_DroneSpecsCsvScratchVaultBacked;
        private static NativeArray<DroneServiceCommand> s_DroneServiceCommands;
        private static NativeArray<DroneServiceCommandCursor> s_DroneServiceCommandCursor;
        private static RepairDroneHub[] s_DroneHubs;
        private static int[] s_DroneSlotDroneIds;
        private static bool[] s_DroneSlotDestroyed;
        private static bool[] s_PendingAbortBySlot;
        private static bool[] s_PendingReleaseBySlot;
        private static bool[] s_PendingHostileBySlot;
        private static bool[] s_PendingResupplyGrantBySlot;
        private static bool[] s_PendingResupplyFailureBySlot;
        private static BaseModule[] s_TargetModulesByDroneSlot;
        private static HectonVoxelVolume[] s_TargetVoxelVolumesByDroneSlot;
        private static DroneFleetTaskKind[] s_DroneTaskKindsBySlot;
        private static Vector3[] s_DronePositions;
        private static BaseModule[] s_TaskModuleRefs;
        private static HectonVoxelVolume[] s_TaskVoxelVolumeRefs;
        private static DroneFleetTaskKind[] s_TaskKinds;
        private static PendingDroneLaunch[] s_PendingLaunches;
        private static int s_PendingLaunchCount;
        private static int s_DroneChassisSpecCount;
        private static int s_HeadlessTaskCount;
        private static int s_HeadlessDroneIdSequence;
        private static int s_HeadlessStasisSlotCount;
        private static bool s_Initialized;
        private static bool s_DockingSignalLanesConfigured;
        private static bool s_HeadlessDriverRegistered;
        private static bool s_HeadlessJobScheduled;
        private static JobHandle s_HeadlessJobHandle;
        private static bool s_FleetSacrificeRequested;
        private static int s_DestroyedDroneCount;
        private static SubmarineEmergencyLevel s_EmergencyLevel;
        private static HectonDroneFleetSnapshot s_LastSnapshot;
        private static FleetStatusSnapshot s_LastFleetStatusSnapshot;
        private static Material s_DroneProceduralMaterial;
        private static GraphicsBuffer s_DroneMatrixBuffer;
        private static GraphicsBuffer s_DroneMatrixBufferBackBuffer;
        private static GraphicsBuffer s_DroneStateGpuBuffer;
        private static GraphicsBuffer s_DroneRenderInstanceBuffer;
        private static GraphicsBuffer s_DroneVisibleMatrixBuffer;
        private static GraphicsBuffer s_DroneVisibleInstanceBuffer;
        private static GraphicsBuffer s_DroneVisibleIndexBuffer;
        private static GraphicsBuffer s_DroneProceduralArgsBuffer;
        private static GraphicsBuffer s_DroneDefaultColorBuffer;
        private static ComputeShader s_DroneCullingCompute;
        private static ComputeShader s_PhantomDronesCompute;
        private static GraphicsBuffer s_PhantomDroneMatrixBuffer;
        private static GraphicsBuffer s_PhantomDroneColorBuffer;
        private static GraphicsBuffer s_PhantomDroneArgsBuffer;
        private static Bounds s_DroneDrawBounds = new Bounds(Vector3.zero, new Vector3(2048f, 2048f, 2048f));
        private static Bounds s_PhantomDroneDrawBounds = new Bounds(Vector3.zero, new Vector3(PhantomDroneBoundsDiameterMeters, PhantomDroneBoundsDiameterMeters, PhantomDroneBoundsDiameterMeters));
        private static int s_DroneRenderLayer;
        private static float s_HeadlessTaskRebuildTimer;
        private static float s_LastHeadlessDeltaTime;
        private static float s_PhantomDronePhaseSeconds;
        private static int s_PhantomDroneLastDrawCount;
        private static int s_DroneMatrixUploadBufferIndex;
        private static int s_FleetTelemetryFrameCounter;
        private static int s_LogicLeechHijackCount;
        private static int s_DockingAbortCount;
        private static int s_DroneBlackBoxCursor;
        private static int s_LastDroneBlackBoxDumpFrame;
        private static int s_DroneFrameIndex;
        private static int s_DroneAStarSolvedCount;
        private static int s_DroneAStarFailureCount;
        private static int s_DroneAStarIterationCount;
        private static int s_LastDroneAStarStatus;
        private static float s_LastDroneAStarAveragePathfindingTimeMs;
        private static int s_DroneTasksCompletedCount;
        private static int s_LastDroneSteeringTickModulo = 1;
        private static DroneFleetFormationMode s_FleetFormationMode;
        private static bool s_DroneCullingKernelsResolved;
        private static bool s_PhantomDroneKernelResolved;
        private static bool s_DroneProceduralMaterialRuntimeOwned;
        private static int s_DroneCullKernel;
        private static int s_DroneClearArgsKernel;
        private static int s_PhantomDroneKernel;

        private static int DroneMatricesPropertyId => s_DroneMatricesPropertyId != 0 ? s_DroneMatricesPropertyId : (s_DroneMatricesPropertyId = Shader.PropertyToID("_DroneMatrices"));
        private static int InstanceMatricesPropertyId => s_InstanceMatricesPropertyId != 0 ? s_InstanceMatricesPropertyId : (s_InstanceMatricesPropertyId = Shader.PropertyToID("_InstanceMatrices"));
        private static int DroneStatesPropertyId => s_DroneStatesPropertyId != 0 ? s_DroneStatesPropertyId : (s_DroneStatesPropertyId = Shader.PropertyToID("_DroneStates"));
        private static int DroneRenderInstancesPropertyId => s_DroneRenderInstancesPropertyId != 0 ? s_DroneRenderInstancesPropertyId : (s_DroneRenderInstancesPropertyId = Shader.PropertyToID("_DroneRenderInstances"));
        private static int DroneVisibleInstancesPropertyId => s_DroneVisibleInstancesPropertyId != 0 ? s_DroneVisibleInstancesPropertyId : (s_DroneVisibleInstancesPropertyId = Shader.PropertyToID("_DroneVisibleInstances"));
        private static int DroneVisibleIndicesPropertyId => s_DroneVisibleIndicesPropertyId != 0 ? s_DroneVisibleIndicesPropertyId : (s_DroneVisibleIndicesPropertyId = Shader.PropertyToID("_DroneVisibleIndices"));
        private static int IndirectArgsBufferPropertyId => s_IndirectArgsBufferPropertyId != 0 ? s_IndirectArgsBufferPropertyId : (s_IndirectArgsBufferPropertyId = Shader.PropertyToID("_IndirectArgsBuffer"));
        private static int CameraFrustumPlanesPropertyId => s_CameraFrustumPlanesPropertyId != 0 ? s_CameraFrustumPlanesPropertyId : (s_CameraFrustumPlanesPropertyId = Shader.PropertyToID("_CameraFrustumPlanes"));
        private static int DroneCountPropertyId => s_DroneCountPropertyId != 0 ? s_DroneCountPropertyId : (s_DroneCountPropertyId = Shader.PropertyToID("_DroneCount"));
        private static int DroneCullRadiusPropertyId => s_DroneCullRadiusPropertyId != 0 ? s_DroneCullRadiusPropertyId : (s_DroneCullRadiusPropertyId = Shader.PropertyToID("_DroneCullRadius"));
        private static int CameraPositionPropertyId => s_CameraPositionPropertyId != 0 ? s_CameraPositionPropertyId : (s_CameraPositionPropertyId = Shader.PropertyToID("_CameraPositionWS"));
        private static int DroneRenderDistanceSqPropertyId => s_DroneRenderDistanceSqPropertyId != 0 ? s_DroneRenderDistanceSqPropertyId : (s_DroneRenderDistanceSqPropertyId = Shader.PropertyToID("_DroneRenderDistanceSq"));
        private static int PhantomMatricesPropertyId => s_PhantomMatricesPropertyId != 0 ? s_PhantomMatricesPropertyId : (s_PhantomMatricesPropertyId = Shader.PropertyToID("_PhantomMatrices"));
        private static int PhantomColorsPropertyId => s_PhantomColorsPropertyId != 0 ? s_PhantomColorsPropertyId : (s_PhantomColorsPropertyId = Shader.PropertyToID("_PhantomColors"));
        private static int PhantomAnchorPropertyId => s_PhantomAnchorPropertyId != 0 ? s_PhantomAnchorPropertyId : (s_PhantomAnchorPropertyId = Shader.PropertyToID("_PhantomAnchorWS"));
        private static int PhantomTimePropertyId => s_PhantomTimePropertyId != 0 ? s_PhantomTimePropertyId : (s_PhantomTimePropertyId = Shader.PropertyToID("_PhantomTime"));
        private static int PhantomCountPropertyId => s_PhantomCountPropertyId != 0 ? s_PhantomCountPropertyId : (s_PhantomCountPropertyId = Shader.PropertyToID("_PhantomCount"));
        private static int PhantomBaseRadiusPropertyId => s_PhantomBaseRadiusPropertyId != 0 ? s_PhantomBaseRadiusPropertyId : (s_PhantomBaseRadiusPropertyId = Shader.PropertyToID("_PhantomBaseRadius"));
        private static int PhantomVerticalAmplitudePropertyId => s_PhantomVerticalAmplitudePropertyId != 0 ? s_PhantomVerticalAmplitudePropertyId : (s_PhantomVerticalAmplitudePropertyId = Shader.PropertyToID("_PhantomVerticalAmplitude"));
        private static int PhantomScalePropertyId => s_PhantomScalePropertyId != 0 ? s_PhantomScalePropertyId : (s_PhantomScalePropertyId = Shader.PropertyToID("_PhantomScale"));
        private static int PhantomCapacityPropertyId => s_PhantomCapacityPropertyId != 0 ? s_PhantomCapacityPropertyId : (s_PhantomCapacityPropertyId = Shader.PropertyToID("_PhantomCapacity"));
        private static int DroneProceduralCameraOriginPropertyId => s_DroneProceduralCameraOriginPropertyId != 0 ? s_DroneProceduralCameraOriginPropertyId : (s_DroneProceduralCameraOriginPropertyId = Shader.PropertyToID("_DroneCameraOriginWS"));
        private static int UsePhantomColorsPropertyId => s_UsePhantomColorsPropertyId != 0 ? s_UsePhantomColorsPropertyId : (s_UsePhantomColorsPropertyId = Shader.PropertyToID("_UsePhantomColors"));
        private static int s_DroneMatricesPropertyId;
        private static int s_InstanceMatricesPropertyId;
        private static int s_DroneStatesPropertyId;
        private static int s_DroneRenderInstancesPropertyId;
        private static int s_DroneVisibleInstancesPropertyId;
        private static int s_DroneVisibleIndicesPropertyId;
        private static int s_IndirectArgsBufferPropertyId;
        private static int s_CameraFrustumPlanesPropertyId;
        private static int s_DroneCountPropertyId;
        private static int s_DroneCullRadiusPropertyId;
        private static int s_CameraPositionPropertyId;
        private static int s_DroneRenderDistanceSqPropertyId;
        private static int s_PhantomMatricesPropertyId;
        private static int s_PhantomColorsPropertyId;
        private static int s_PhantomAnchorPropertyId;
        private static int s_PhantomTimePropertyId;
        private static int s_PhantomCountPropertyId;
        private static int s_PhantomBaseRadiusPropertyId;
        private static int s_PhantomVerticalAmplitudePropertyId;
        private static int s_PhantomScalePropertyId;
        private static int s_PhantomCapacityPropertyId;
        private static int s_DroneProceduralCameraOriginPropertyId;
        private static int s_UsePhantomColorsPropertyId;
        // COLD ALLOC: Plane[6] - reusable camera frustum plane scratch for GPU drone culling upload - owner: DroneFleetManager
        private static readonly Plane[] s_CullingPlanes = new Plane[6];
        // COLD ALLOC: Vector4[6] - reusable camera frustum plane vector scratch for GPU drone culling upload - owner: DroneFleetManager
        private static readonly Vector4[] s_CullingPlaneVectors = new Vector4[6];
        // COLD ALLOC: SpatialQueryHit[16] - drone acoustic relay contact scratch buffer - owner: DroneFleetManager
        private static readonly SpatialQueryHit[] s_DroneRelayContacts = new SpatialQueryHit[MaxDroneRelayContacts];
        // COLD ALLOC: SubmarineOsEventBridge[1] - static fleet bridge into deferred submarine OS payloads - owner: DroneFleetManager
        private static readonly SubmarineOsEventBridge s_SubmarineOsEventBridge = new SubmarineOsEventBridge();
        // COLD ALLOC: StorageReservationCommitResolvedBridge[1] - static fleet bridge into deferred command queue acknowledgements - owner: DroneFleetManager
        private static readonly StorageReservationCommitResolvedBridge s_StorageReservationCommitResolvedBridge = new StorageReservationCommitResolvedBridge();

        private sealed class SubmarineOsEventBridge : ISubmarineOsEventListener
        {
            public void OnSubmarineOsEvent(in SubmarineOsEventPayload payload)
            {
                if (HectonSubmarineOsEvents.TryBuildSnapshot(in payload, out HectonSubmarineOsSnapshot snapshot))
                    HandleSubmarineSnapshotUpdated(in snapshot);
            }
        }

        private sealed class StorageReservationCommitResolvedBridge : ThreadSafeCommandQueue.IStorageReservationCommitResolvedListener
        {
            public void OnStorageReservationCommitResolved(in ThreadSafeCommandQueue.StorageReservationCommitResolvedPayload payload)
            {
                HandleStorageReservationCommitResolved(
                    payload.RequesterId,
                    payload.ReservationId,
                    payload.Committed != 0);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (s_Initialized)
            {
                HectonSubmarineOsEvents.Unregister(s_SubmarineOsEventBridge);
                ThreadSafeCommandQueue.Unregister(s_StorageReservationCommitResolvedBridge);
            }

            TryUnregisterHeadlessDriver();
            CompleteDockingObstacleAbortsForReset();
            CompletePendingHeadlessJobForReset();
            ReleaseHeadlessNativeMemory();
            ReleaseRenderBuffers();
            ReleasePhantomRenderResources();

            s_PendingLaunchCount = 0;
            s_HeadlessTaskCount = 0;
            s_HeadlessDroneIdSequence = 0;
            s_HeadlessStasisSlotCount = 0;
            s_FleetSacrificeRequested = false;
            s_DestroyedDroneCount = 0;
            s_EmergencyLevel = SubmarineEmergencyLevel.Nominal;
            s_LastSnapshot = default;
            s_LastFleetStatusSnapshot = default;
            s_Initialized = false;
            s_DockingSignalLanesConfigured = false;
            s_HeadlessJobScheduled = false;
            s_DroneProceduralMaterial = null;
            s_PhantomDronesCompute = null;
            s_DroneRenderLayer = 0;
            s_HeadlessTaskRebuildTimer = 0f;
            s_LastHeadlessDeltaTime = 0f;
            s_PhantomDronePhaseSeconds = 0f;
            s_PhantomDroneLastDrawCount = -1;
            s_DroneMatrixUploadBufferIndex = 0;
            s_FleetTelemetryFrameCounter = 0;
            s_LogicLeechHijackCount = 0;
            s_DockingAbortCount = 0;
            s_DroneBlackBoxCursor = 0;
            s_LastDroneBlackBoxDumpFrame = -1;
            s_DroneFrameIndex = 0;
            s_DroneAStarSolvedCount = 0;
            s_DroneAStarFailureCount = 0;
            s_DroneAStarIterationCount = 0;
            s_LastDroneAStarStatus = 0;
            s_LastDroneAStarAveragePathfindingTimeMs = 0f;
            s_DroneTasksCompletedCount = 0;
            s_FleetFormationMode = DroneFleetFormationMode.Repair;
            s_DroneCullingCompute = null;
            s_DroneCullingKernelsResolved = false;
            s_PhantomDroneKernelResolved = false;
            s_DroneProceduralMaterialRuntimeOwned = false;
            s_DroneCullKernel = 0;
            s_DroneClearArgsKernel = 0;
            s_PhantomDroneKernel = 0;
            s_DroneMatricesPropertyId = 0;
            s_InstanceMatricesPropertyId = 0;
            s_DroneStatesPropertyId = 0;
            s_DroneRenderInstancesPropertyId = 0;
            s_DroneVisibleInstancesPropertyId = 0;
            s_DroneVisibleIndicesPropertyId = 0;
            s_IndirectArgsBufferPropertyId = 0;
            s_CameraFrustumPlanesPropertyId = 0;
            s_DroneCountPropertyId = 0;
            s_DroneCullRadiusPropertyId = 0;
            s_CameraPositionPropertyId = 0;
            s_DroneRenderDistanceSqPropertyId = 0;
            s_PhantomMatricesPropertyId = 0;
            s_PhantomColorsPropertyId = 0;
            s_PhantomAnchorPropertyId = 0;
            s_PhantomTimePropertyId = 0;
            s_PhantomCountPropertyId = 0;
            s_PhantomBaseRadiusPropertyId = 0;
            s_PhantomVerticalAmplitudePropertyId = 0;
            s_PhantomScalePropertyId = 0;
            s_PhantomCapacityPropertyId = 0;
            s_DroneProceduralCameraOriginPropertyId = 0;
            s_UsePhantomColorsPropertyId = 0;

            ReleaseDroneVaultBuffer(ref s_TaskClaimCounts, ref s_TaskClaimCountsHandle, ref s_TaskClaimCountsVaultBacked, nameof(s_TaskClaimCounts));
        }

        internal static HectonDroneFleetSnapshot CurrentSnapshot
        {
            get
            {
                EnsureInitialized();
                return s_LastSnapshot;
            }
        }

        internal static bool IsEmergencyOverclockActive
        {
            get
            {
                EnsureInitialized();
                return s_EmergencyLevel == SubmarineEmergencyLevel.Evacuate;
            }
        }

        public static void RequestFleetSacrifice()
        {
            EnsureInitialized();
            s_FleetSacrificeRequested = true;
            PublishSnapshot();
        }

        /// <summary>
        /// Requests a tactical formation mode for idle drones without interrupting active repair/resupply sorties.
        /// </summary>
        public static void RequestFleetFormation(DroneFleetFormationMode formationMode)
        {
            EnsureInitialized();
            s_FleetFormationMode = formationMode;
        }

        /// <summary>
        /// Supplies the GPU culling compute shader used by the headless indirect renderer.
        /// </summary>
        public static void ConfigureHeadlessCulling(ComputeShader cullingCompute)
        {
            EnsureInitialized();
            s_DroneCullingCompute = cullingCompute;
            s_DroneCullingKernelsResolved = false;
            ResolveDroneCullingKernels();
        }

        internal static void ConfigureHeadlessRenderSource(GameObject dronePrefab)
        {
            if (dronePrefab == null)
                return;

            EnsureInitialized();

            s_DroneRenderLayer = dronePrefab.layer;
            s_PhantomDroneLastDrawCount = -1;
            EnsureRenderBuffers();
        }

        internal static void ConfigurePhantomSwarm(ComputeShader phantomCompute, Material phantomMaterial)
        {
            EnsureInitialized();

            if (phantomCompute != null)
            {
                s_PhantomDronesCompute = phantomCompute;
                s_PhantomDroneKernelResolved = false;
            }

            ResolvePhantomDroneKernel();
            EnsurePhantomRenderResources();
        }

        internal static bool TryLaunchHeadlessDrone(
            RepairDroneHub hub,
            in DroneFleetTask task,
            Vector3 homePosition,
            float repairRatePerSecond,
            int loadedSolderUnits,
            out int droneId)
        {
            droneId = 0;
            if (hub == null || !task.IsValid())
                return false;

            EnsureInitialized();
            TryRegisterHeadlessDriver();

            if (CountManagedHeadlessDrones() >= MaxOperationalDroneCount)
                return false;

            int slot = FindFreeHeadlessSlot();
            if (slot < 0 || s_PendingLaunchCount >= s_PendingLaunches.Length)
                return false;

            droneId = ++s_HeadlessDroneIdSequence;
            if (droneId <= 0)
                droneId = ++s_HeadlessDroneIdSequence;

            s_DroneSlotDroneIds[slot] = droneId;
            s_PendingReleaseBySlot[slot] = false;
            s_PendingAbortBySlot[slot] = false;
            s_PendingHostileBySlot[slot] = false;
            s_PendingLaunches[s_PendingLaunchCount++] = new PendingDroneLaunch
            {
                Active = true,
                DroneSlot = slot,
                DroneId = droneId,
                Hub = hub,
                Task = task,
                HomePosition = homePosition,
                HomeRotation = hub.DockRotation,
                RepairRatePerSecond = Mathf.Max(0.1f, repairRatePerSecond),
                LoadedSolderUnits = Mathf.Max(0, loadedSolderUnits)
            };

            PublishSnapshot();
            return true;
        }

        internal static bool IsHeadlessDroneActive(int droneId)
        {
            if (droneId <= 0 || s_DroneSlotDroneIds == null)
                return false;

            for (int i = 0; i < s_DroneSlotDroneIds.Length; i++)
            {
                if (s_DroneSlotDroneIds[i] == droneId && !s_PendingReleaseBySlot[i])
                    return true;
            }

            return false;
        }

        internal static void AbortHeadlessDrone(int droneId)
        {
            int slot = ResolveHeadlessSlot(droneId);
            if (slot < 0)
                return;

            s_PendingAbortBySlot[slot] = true;
        }

        internal static void ReleaseHeadlessDrone(int droneId)
        {
            int slot = ResolveHeadlessSlot(droneId);
            if (slot < 0)
                return;

            s_PendingReleaseBySlot[slot] = true;
        }

        internal static bool ReportLogicLeechContact(Vector3 contactPosition, float radiusMeters)
        {
            return TryHijackNearestDrone(contactPosition, radiusMeters);
        }

        internal static bool TryHijackNearestDrone(Vector3 contactPosition, float radiusMeters)
        {
            EnsureInitialized();
            if (s_DroneSlotDroneIds == null || radiusMeters <= 0.0001f)
                return false;

            float radiusSq = radiusMeters * radiusMeters;
            int bestSlot = -1;
            float bestDistanceSq = float.MaxValue;
            for (int i = 0; i < s_DroneSlotDroneIds.Length; i++)
            {
                if (s_DroneSlotDroneIds[i] <= 0 || s_PendingReleaseBySlot[i])
                    continue;

                float distanceSq = (s_DronePositions[i] - contactPosition).sqrMagnitude;
                if (distanceSq > radiusSq || distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                bestSlot = i;
            }

            if (bestSlot < 0)
                return false;

            s_PendingHostileBySlot[bestSlot] = true;
            PublishSnapshot();
            return true;
        }

        internal static void ReportDroneDestroyed()
        {
            EnsureInitialized();
            s_DestroyedDroneCount++;
            PublishSnapshot();
        }

        internal static void NotifyFleetStateChanged()
        {
            EnsureInitialized();
            TryRegisterHeadlessDriver();
            PublishSnapshot();
        }

        internal static bool TryAssignRepairTask(
            RepairDroneHub hub,
            float dispatchIntegrityThreshold,
            out BaseModule target,
            out float assignmentScore,
            out float criticalityWeight)
        {
            target = null;
            if (!TryAssignFleetTask(hub, dispatchIntegrityThreshold, out DroneFleetTask task, out assignmentScore, out criticalityWeight))
                return false;

            target = task.Module;
            return target != null;
        }

        internal static bool TryAssignFleetTask(
            RepairDroneHub hub,
            float dispatchIntegrityThreshold,
            out DroneFleetTask task,
            out float assignmentScore,
            out float criticalityWeight)
        {
            task = default;
            assignmentScore = 0f;
            criticalityWeight = 0f;

            if (hub == null)
                return false;

            EnsureInitialized();

            ConstructionManager manager = GlobalRegistry.ConstructionRuntime;
            int moduleCount = manager != null ? manager.SpawnedBaseModuleCount : 0;
            if (moduleCount == 0)
                return false;

            int scanModuleCount = Mathf.Min(moduleCount, MaxMainThreadTaskScanCount);
            EnsureTaskCapacity(scanModuleCount);
            ClearClaimCounts(scanModuleCount);
            RebuildActiveClaimCounts(manager, scanModuleCount);

            Vector3 hubPosition = hub.DockPosition;
            PowerGrid hubGrid = hub.CurrentGrid;
            FloraInteractionManager floraInteractionManager = FloraInteractionManager.ActiveRuntimeInstance;
            RepairTaskCandidate bestTask = default;
            bool hasBestTask = false;
            DroneTaskNativeMinHeap taskHeap = new DroneTaskNativeMinHeap
            {
                Nodes = s_DroneTaskPriorityHeap,
                Count = 0
            };

            for (int moduleIndex = 0; moduleIndex < scanModuleCount; moduleIndex++)
            {
                BaseModule module = manager.GetSpawnedBaseModuleAt(moduleIndex);
                if (module == null || !module.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (IsEligibleRepairTarget(hubGrid, module, dispatchIntegrityThreshold))
                {
                    Vector3 modulePosition = module.transform.position;
                    float distanceSq = (hubPosition - modulePosition).sqrMagnitude;
                    float taskCriticality = ResolveCriticalityWeight(module);
                    float taskScore = ComputeTaskAssignmentScoreFromDistanceSq(distanceSq, taskCriticality);
                    RepairTaskCandidate candidate = new RepairTaskCandidate
                    {
                        Kind = DroneFleetTaskKind.RepairModule,
                        Module = module,
                        ModuleIndex = moduleIndex,
                        Position = modulePosition,
                        Radius = 0f,
                        Score = taskScore,
                        CriticalityWeight = taskCriticality
                    };
                    TryPushTaskPriorityCandidate(ref taskHeap, in candidate);
                    ConsiderTaskCandidate(in candidate, ref bestTask, ref hasBestTask);
                }

                if (floraInteractionManager == null ||
                    module.ParasiteInfectionLevel <= 0.0001f ||
                    IsDifferentGrid(hubGrid, module) ||
                    !floraInteractionManager.TryResolveNearestModuleParasite(module, hubPosition, out FloraInteractionManager.ModuleParasiteTarget parasiteTarget))
                {
                    continue;
                }

                float parasiteDistanceSq = (hubPosition - parasiteTarget.Position).sqrMagnitude;
                float parasiteCriticality = ResolveParasiteCriticalityWeight(module, in parasiteTarget);
                float parasiteScore = ComputeTaskAssignmentScoreFromDistanceSq(parasiteDistanceSq, parasiteCriticality);
                RepairTaskCandidate parasiteCandidate = new RepairTaskCandidate
                {
                    Kind = DroneFleetTaskKind.CutParasite,
                    Module = module,
                    ModuleIndex = moduleIndex,
                    Position = parasiteTarget.Position,
                    Radius = parasiteTarget.Radius,
                    Score = parasiteScore,
                    CriticalityWeight = parasiteCriticality
                };
                TryPushTaskPriorityCandidate(ref taskHeap, in parasiteCandidate);
                ConsiderTaskCandidate(in parasiteCandidate, ref bestTask, ref hasBestTask);
            }

            if (TryResolvePriorityHeapTask(ref taskHeap, manager, out RepairTaskCandidate heapTask))
            {
                bestTask = heapTask;
                hasBestTask = true;
            }

            if (hasBestTask)
            {
                s_TaskClaimCounts[bestTask.ModuleIndex] = s_TaskClaimCounts[bestTask.ModuleIndex] + 1;
                task = new DroneFleetTask(
                    bestTask.Kind,
                    bestTask.Module,
                    bestTask.Position,
                    bestTask.Radius);
                assignmentScore = bestTask.Score;
                criticalityWeight = bestTask.CriticalityWeight;
                PublishSnapshot();
                return true;
            }

            PublishSnapshot();
            return false;
        }

        public static float ComputeTaskAssignmentScore(float distanceMeters, float criticalityWeight)
        {
            float clampedDistance = Mathf.Max(MinimumScoreDistanceMeters, distanceMeters);
            return math.rcp(clampedDistance) * Mathf.Max(0.1f, criticalityWeight);
        }

        private static float ComputeTaskAssignmentScoreFromDistanceSq(float distanceSq, float criticalityWeight)
        {
            float inverseDistance = math.rsqrt(math.max(MinimumScoreDistanceMetersSq, distanceSq));
            return inverseDistance * math.max(0.1f, criticalityWeight);
        }

        private static void EnsureInitialized()
        {
            EnsureDockingSignalLanes();
            if (!s_DroneStates.IsCreated)
                AllocateHeadlessNativeMemory();

            if (!s_Initialized)
            {
                HectonSubmarineOsEvents.Unregister(s_SubmarineOsEventBridge);
                HectonSubmarineOsEvents.Register(s_SubmarineOsEventBridge);
                ThreadSafeCommandQueue.Unregister(s_StorageReservationCommitResolvedBridge);
                ThreadSafeCommandQueue.Register(s_StorageReservationCommitResolvedBridge);
                s_Initialized = true;
            }

            TryRegisterHeadlessDriver();
        }

        private static void EnsureDockingSignalLanes()
        {
            if (s_DockingSignalLanesConfigured)
                return;

            GlobalSignals.InitializeAllQueues();
            SignalBus<DroneFleetMockRepairSignal>.Configure(64, maxFrameSignals: 64, lowTierFrameSignals: 16, laneHash: 0x44524D52u);
            SignalBus<DroneFleetMockRepairSignal>.EnsureInitialized();
            SignalBus<DroneFleetMockMiningSignal>.Configure(64, maxFrameSignals: 64, lowTierFrameSignals: 16, laneHash: 0x44524D4Eu);
            SignalBus<DroneFleetMockMiningSignal>.EnsureInitialized();
            SignalBus<DroneFleetInventoryTransactionSignal>.Configure(64, maxFrameSignals: 64, lowTierFrameSignals: 16, laneHash: 0x4452494Eu);
            SignalBus<DroneFleetInventoryTransactionSignal>.EnsureInitialized();
            s_DockingSignalLanesConfigured = true;
        }

        private static void AllocateHeadlessNativeMemory()
        {
            ValidateDroneFleetDtoLayouts();

            s_DroneStates = ResolveDroneVaultBuffer<HeadlessDroneState>(BufferID.ShinobuDroneFleetStates, HeadlessDroneCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneStatesHandle, out s_DroneStatesVaultBacked); // COLD ALLOC: NativeArray<HeadlessDroneState>[512] - authoritative drone state pool - owner: GlobalDataVault/H8Memory fallback
            s_DroneStateBackBuffer = ResolveDroneVaultBuffer<HeadlessDroneState>(BufferID.ShinobuDroneFleetStateBackBuffer, HeadlessDroneCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneStateBackBufferHandle, out s_DroneStateBackBufferVaultBacked); // COLD ALLOC: NativeArray<HeadlessDroneState>[512] - Burst double-buffer write lane - owner: GlobalDataVault/H8Memory fallback
            s_DroneRenderMatrices = ResolveDroneVaultBuffer<float4x4>(BufferID.ShinobuDroneFleetRenderMatrices, HeadlessDroneCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneRenderMatricesHandle, out s_DroneRenderMatricesVaultBacked); // COLD ALLOC: NativeArray<float4x4>[512] - indirect render front matrices - owner: GlobalDataVault/H8Memory fallback
            s_DroneRenderMatrixBackBuffer = ResolveDroneVaultBuffer<float4x4>(BufferID.ShinobuDroneFleetRenderMatrixBackBuffer, HeadlessDroneCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneRenderMatrixBackBufferHandle, out s_DroneRenderMatrixBackBufferVaultBacked); // COLD ALLOC: NativeArray<float4x4>[512] - indirect render back matrices - owner: GlobalDataVault/H8Memory fallback
            s_DroneRenderInstances = ResolveDroneVaultBuffer<DroneRenderInstance>(BufferID.ShinobuDroneFleetRenderInstances, HeadlessDroneCapacity, NativeArrayOptions.ClearMemory, ref s_DroneRenderInstancesHandle, out s_DroneRenderInstancesVaultBacked); // COLD ALLOC: NativeArray<DroneRenderInstance>[512] - render payload staging - owner: GlobalDataVault/H8Memory fallback
            s_DroneCullingStates = ResolveDroneCullingStatesBuffer(); // COLD ALLOC: NativeArray<DroneCullingStateGpu>[512] - Metal/Vulkan-safe compact drone culling payload - owner: GlobalDataVault/H8Memory fallback
            s_DronePositionsSoA = ResolveDroneVaultBuffer<float3>(BufferID.ShinobuDroneFleetPositionsSoA, HeadlessDroneCapacity, NativeArrayOptions.ClearMemory, ref s_DronePositionsSoAHandle, out s_DronePositionsSoAVaultBacked); // COLD ALLOC: NativeArray<float3>[512] - SoA positions for Burst fleet state - owner: GlobalDataVault/H8Memory fallback
            s_DroneStateBytes = ResolveDroneVaultBuffer<byte>(BufferID.ShinobuDroneFleetStateBytes, HeadlessDroneCapacity, NativeArrayOptions.ClearMemory, ref s_DroneStateBytesHandle, out s_DroneStateBytesVaultBacked); // COLD ALLOC: NativeArray<byte>[512] - compact state stream - owner: GlobalDataVault/H8Memory fallback
            s_DroneBlackBox = ResolveDroneVaultBuffer<DroneFleetBlackBoxEntry>(BufferID.ShinobuDroneFleetBlackBox, DroneFleetBlackBoxFrameCapacity, NativeArrayOptions.ClearMemory, ref s_DroneBlackBoxHandle, out s_DroneBlackBoxVaultBacked); // COLD ALLOC: NativeArray<DroneFleetBlackBoxEntry>[300] - fixed fleet black-box ring buffer - owner: GlobalDataVault/H8Memory fallback
            s_DroneTuningConstants = ResolveDroneVaultBuffer<DroneFleetTuningConstants>(BufferID.ShinobuDroneFleetTuningConstants, 1, NativeArrayOptions.ClearMemory, ref s_DroneTuningConstantsHandle, out s_DroneTuningConstantsVaultBacked); // COLD ALLOC: NativeArray<DroneFleetTuningConstants>[1] - runtime tuning DTO - owner: GlobalDataVault/H8Memory fallback
            s_DroneMacroWaypoints = ResolveDroneVaultBuffer<PathWaypointDTO>(BufferID.ShinobuDroneFleetMacroWaypoints, HeadlessDroneCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneMacroWaypointsHandle, out s_DroneMacroWaypointsVaultBacked); // COLD ALLOC: NativeArray<PathWaypointDTO>[512] - macro A* first-waypoint lane - owner: GlobalDataVault/H8Memory fallback
            s_DroneMacroWaypointStates = ResolveDroneVaultBuffer<byte>(BufferID.ShinobuDroneFleetMacroWaypointStates, HeadlessDroneCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneMacroWaypointStatesHandle, out s_DroneMacroWaypointStatesVaultBacked); // COLD ALLOC: NativeArray<byte>[512] - macro A* waypoint validity lane - owner: GlobalDataVault/H8Memory fallback
            s_DroneAStarOpenHeap = ResolveDroneVaultBuffer<DroneNativeMinHeapNode>(BufferID.ShinobuDroneFleetAStarOpenHeap, DroneAStarNodeCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneAStarOpenHeapHandle, out s_DroneAStarOpenHeapVaultBacked); // COLD ALLOC: NativeArray<DroneNativeMinHeapNode>[512] - native min-heap open set - owner: GlobalDataVault/H8Memory fallback
            s_DroneAStarGCosts = ResolveDroneVaultBuffer<float>(BufferID.ShinobuDroneFleetAStarGCosts, DroneAStarNodeCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneAStarGCostsHandle, out s_DroneAStarGCostsVaultBacked); // COLD ALLOC: NativeArray<float>[512] - A* g-cost scratch - owner: GlobalDataVault/H8Memory fallback
            s_DroneAStarCameFrom = ResolveDroneVaultBuffer<int>(BufferID.ShinobuDroneFleetAStarCameFrom, DroneAStarNodeCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneAStarCameFromHandle, out s_DroneAStarCameFromVaultBacked); // COLD ALLOC: NativeArray<int>[512] - A* parent scratch - owner: GlobalDataVault/H8Memory fallback
            s_DroneAStarNodeStates = ResolveDroneVaultBuffer<byte>(BufferID.ShinobuDroneFleetAStarNodeStates, DroneAStarNodeCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneAStarNodeStatesHandle, out s_DroneAStarNodeStatesVaultBacked); // COLD ALLOC: NativeArray<byte>[512] - A* open/closed scratch - owner: GlobalDataVault/H8Memory fallback
            s_DroneMacroRouteNodes = ResolveDroneVaultBuffer<int>(BufferID.ShinobuDroneFleetMacroRouteNodes, DroneAStarRouteNodeCapacity, NativeArrayOptions.ClearMemory, ref s_DroneMacroRouteNodesHandle, out s_DroneMacroRouteNodesVaultBacked); // COLD ALLOC: NativeArray<int>[512] - fixed NativeList-style macro route node stream - owner: GlobalDataVault/H8Memory fallback
            s_DroneMacroRouteCounts = ResolveDroneVaultBuffer<byte>(BufferID.ShinobuDroneFleetMacroRouteCounts, HeadlessDroneCapacity, NativeArrayOptions.ClearMemory, ref s_DroneMacroRouteCountsHandle, out s_DroneMacroRouteCountsVaultBacked); // COLD ALLOC: NativeArray<byte>[512] - per-drone macro route node counts - owner: GlobalDataVault/H8Memory fallback
            s_DroneAStarTelemetry = ResolveDroneVaultBuffer<DroneAStarTelemetry>(BufferID.ShinobuDroneFleetAStarTelemetry, DroneAStarTelemetryCapacity, NativeArrayOptions.ClearMemory, ref s_DroneAStarTelemetryHandle, out s_DroneAStarTelemetryVaultBacked); // COLD ALLOC: NativeArray<DroneAStarTelemetry>[1] - A* solve counters - owner: GlobalDataVault/H8Memory fallback
            s_HeadlessTaskClaimOwners = ResolveDroneVaultBuffer<int>(BufferID.ShinobuDroneFleetTaskClaimOwners, HeadlessTaskCapacity, NativeArrayOptions.ClearMemory, ref s_HeadlessTaskClaimOwnersHandle, out s_HeadlessTaskClaimOwnersVaultBacked); // COLD ALLOC: NativeArray<int>[64] - atomic task claim owners - owner: GlobalDataVault/H8Memory fallback
            s_FleetTelemetryAccumulator = ResolveDroneVaultBuffer<int>(BufferID.ShinobuDroneFleetTelemetryAccumulator, (int)DroneFleetTelemetryAccumulatorSlot.Count, NativeArrayOptions.ClearMemory, ref s_FleetTelemetryAccumulatorHandle, out s_FleetTelemetryAccumulatorVaultBacked); // COLD ALLOC: NativeArray<int>[6] - Burst fleet telemetry accumulator - owner: GlobalDataVault/H8Memory fallback
            s_DockingRaycastCommands = ResolveDroneVaultBuffer<RaycastCommand>(BufferID.ShinobuDroneFleetDockingRaycastCommands, DockingRaycastCapacity, NativeArrayOptions.ClearMemory, ref s_DockingRaycastCommandsHandle, out s_DockingRaycastCommandsVaultBacked); // COLD ALLOC: NativeArray<RaycastCommand>[192] - segmented docking corridor probes - owner: GlobalDataVault/H8Memory fallback
            s_DockingRaycastHits = ResolveDroneVaultBuffer<RaycastHit>(BufferID.ShinobuDroneFleetDockingRaycastHits, DockingRaycastCapacity, NativeArrayOptions.ClearMemory, ref s_DockingRaycastHitsHandle, out s_DockingRaycastHitsVaultBacked); // COLD ALLOC: NativeArray<RaycastHit>[192] - segmented docking probe results - owner: GlobalDataVault/H8Memory fallback
            s_DockingRaycastSlots = ResolveDroneVaultBuffer<int>(BufferID.ShinobuDroneFleetDockingRaycastSlots, DockingRaycastCapacity, NativeArrayOptions.ClearMemory, ref s_DockingRaycastSlotsHandle, out s_DockingRaycastSlotsVaultBacked); // COLD ALLOC: NativeArray<int>[192] - segmented docking slot lookup - owner: GlobalDataVault/H8Memory fallback
            s_DroneTaskPriorityHeap = ResolveDroneVaultBuffer<DroneTaskDTO>(BufferID.ShinobuDroneFleetTaskPriorityHeap, HeadlessTaskCapacity, NativeArrayOptions.ClearMemory, ref s_DroneTaskPriorityHeapHandle, out s_DroneTaskPriorityHeapVaultBacked); // COLD ALLOC: NativeArray<DroneTaskDTO>[64] - task priority min-heap storage - owner: GlobalDataVault/H8Memory fallback
            s_DroneStateDtos = ResolveDroneVaultBuffer<DroneStateDTO>(DroneFleetStateDtoBufferId, HeadlessDroneCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneStateDtosHandle, out s_DroneStateDtosVaultBacked); // COLD ALLOC: NativeArray<DroneStateDTO>[512] - exact 64B AUP fleet DTO lane - owner: GlobalDataVault/H8Memory fallback
            s_DroneTargetDtos = ResolveDroneVaultBuffer<DroneTargetDTO>(DroneFleetTargetDtoBufferId, HeadlessDroneCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneTargetDtosHandle, out s_DroneTargetDtosVaultBacked); // COLD ALLOC: NativeArray<DroneTargetDTO>[512] - current target AUP lane - owner: GlobalDataVault/H8Memory fallback
            s_DroneAssignmentTasks = ResolveDroneVaultBuffer<DroneTaskDTO>(DroneFleetAssignmentTasksBufferId, HeadlessTaskCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneAssignmentTasksHandle, out s_DroneAssignmentTasksVaultBacked); // COLD ALLOC: NativeArray<DroneTaskDTO>[64] - O(N*M) assignment task snapshot - owner: GlobalDataVault/H8Memory fallback
            s_DroneProceduralArgs = ResolveDroneVaultBuffer<DroneProceduralIndirectArgsDTO>(DroneFleetProceduralArgsBufferId, 1, NativeArrayOptions.UninitializedMemory, ref s_DroneProceduralArgsHandle, out s_DroneProceduralArgsVaultBacked); // COLD ALLOC: NativeArray<DroneProceduralIndirectArgsDTO>[1] - DrawProceduralIndirect args staging - owner: GlobalDataVault/H8Memory fallback
            s_DroneServiceCommands = ResolveDroneVaultBuffer<DroneServiceCommand>(DroneFleetServiceCommandsBufferId, DroneServiceCommandCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneServiceCommandsHandle, out s_DroneServiceCommandsVaultBacked); // COLD ALLOC: NativeArray<DroneServiceCommand>[1536] - bounded 64B service command lane - owner: GlobalDataVault/H8Memory fallback
            s_DroneServiceCommandCursor = ResolveDroneVaultBuffer<DroneServiceCommandCursor>(DroneFleetServiceCommandCursorBufferId, 1, NativeArrayOptions.ClearMemory, ref s_DroneServiceCommandCursorHandle, out s_DroneServiceCommandCursorVaultBacked); // COLD ALLOC: NativeArray<DroneServiceCommandCursor>[1] - 64B atomic service command cursor - owner: GlobalDataVault/H8Memory fallback
            s_DroneSpatialBucketHeads = ResolveDroneVaultBuffer<int>(DroneFleetSpatialBucketHeadsBufferId, DroneSpatialBucketCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneSpatialBucketHeadsHandle, out s_DroneSpatialBucketHeadsVaultBacked); // COLD ALLOC: NativeArray<int>[2048] - flat boid spatial hash bucket heads - owner: GlobalDataVault/H8Memory fallback
            s_DroneSpatialNextIndices = ResolveDroneVaultBuffer<int>(DroneFleetSpatialNextIndicesBufferId, HeadlessDroneCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneSpatialNextIndicesHandle, out s_DroneSpatialNextIndicesVaultBacked); // COLD ALLOC: NativeArray<int>[512] - flat boid spatial hash linked-list next indices - owner: GlobalDataVault/H8Memory fallback
            s_DroneSpatialKeys = ResolveDroneVaultBuffer<int>(DroneFleetSpatialKeysBufferId, HeadlessDroneCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneSpatialKeysHandle, out s_DroneSpatialKeysVaultBacked); // COLD ALLOC: NativeArray<int>[512] - exact spatial cell keys for bucket collision checks - owner: GlobalDataVault/H8Memory fallback
            s_DroneChassisSpecs = ResolveDroneVaultBuffer<DroneChassisSpecDTO>(DroneFleetChassisSpecsBufferId, DroneChassisSpecCapacity, NativeArrayOptions.ClearMemory, ref s_DroneChassisSpecsHandle, out s_DroneChassisSpecsVaultBacked); // COLD ALLOC: NativeArray<DroneChassisSpecDTO>[8] - hashed chassis tuning rows from drone_chassis_specs.csv - owner: GlobalDataVault/H8Memory fallback
            s_DroneSpecsCsvScratch = ResolveDroneVaultBuffer<byte>(DroneFleetCsvScratchBufferId, DroneSpecsCsvMaxBytes, NativeArrayOptions.UninitializedMemory, ref s_DroneSpecsCsvScratchHandle, out s_DroneSpecsCsvScratchVaultBacked); // COLD ALLOC: NativeArray<byte>[16KB] - unmanaged CSV scratch for cold designer reload - owner: GlobalDataVault/H8Memory fallback
            RegisterNativeArrayIfFallback(s_DroneStates, s_DroneStatesVaultBacked, nameof(s_DroneStates));
            RegisterNativeArrayIfFallback(s_DroneStateBackBuffer, s_DroneStateBackBufferVaultBacked, nameof(s_DroneStateBackBuffer));
            RegisterNativeArrayIfFallback(s_DroneRenderMatrices, s_DroneRenderMatricesVaultBacked, nameof(s_DroneRenderMatrices));
            RegisterNativeArrayIfFallback(s_DroneRenderMatrixBackBuffer, s_DroneRenderMatrixBackBufferVaultBacked, nameof(s_DroneRenderMatrixBackBuffer));
            RegisterNativeArrayIfFallback(s_DroneRenderInstances, s_DroneRenderInstancesVaultBacked, nameof(s_DroneRenderInstances));
            RegisterNativeArrayIfFallback(s_DroneCullingStates, s_DroneCullingStatesVaultBacked, nameof(s_DroneCullingStates));
            RegisterNativeArrayIfFallback(s_DronePositionsSoA, s_DronePositionsSoAVaultBacked, nameof(s_DronePositionsSoA));
            RegisterNativeArrayIfFallback(s_DroneStateBytes, s_DroneStateBytesVaultBacked, nameof(s_DroneStateBytes));
            RegisterNativeArrayIfFallback(s_DroneBlackBox, s_DroneBlackBoxVaultBacked, nameof(s_DroneBlackBox));
            RegisterNativeArrayIfFallback(s_DroneTuningConstants, s_DroneTuningConstantsVaultBacked, nameof(s_DroneTuningConstants));
            RegisterNativeArrayIfFallback(s_DroneMacroWaypoints, s_DroneMacroWaypointsVaultBacked, nameof(s_DroneMacroWaypoints));
            RegisterNativeArrayIfFallback(s_DroneMacroWaypointStates, s_DroneMacroWaypointStatesVaultBacked, nameof(s_DroneMacroWaypointStates));
            RegisterNativeArrayIfFallback(s_DroneAStarOpenHeap, s_DroneAStarOpenHeapVaultBacked, nameof(s_DroneAStarOpenHeap));
            RegisterNativeArrayIfFallback(s_DroneAStarGCosts, s_DroneAStarGCostsVaultBacked, nameof(s_DroneAStarGCosts));
            RegisterNativeArrayIfFallback(s_DroneAStarCameFrom, s_DroneAStarCameFromVaultBacked, nameof(s_DroneAStarCameFrom));
            RegisterNativeArrayIfFallback(s_DroneAStarNodeStates, s_DroneAStarNodeStatesVaultBacked, nameof(s_DroneAStarNodeStates));
            RegisterNativeArrayIfFallback(s_DroneMacroRouteNodes, s_DroneMacroRouteNodesVaultBacked, nameof(s_DroneMacroRouteNodes));
            RegisterNativeArrayIfFallback(s_DroneMacroRouteCounts, s_DroneMacroRouteCountsVaultBacked, nameof(s_DroneMacroRouteCounts));
            RegisterNativeArrayIfFallback(s_DroneAStarTelemetry, s_DroneAStarTelemetryVaultBacked, nameof(s_DroneAStarTelemetry));
            RegisterNativeArrayIfFallback(s_HeadlessTaskClaimOwners, s_HeadlessTaskClaimOwnersVaultBacked, nameof(s_HeadlessTaskClaimOwners));
            RegisterNativeArrayIfFallback(s_FleetTelemetryAccumulator, s_FleetTelemetryAccumulatorVaultBacked, nameof(s_FleetTelemetryAccumulator));
            RegisterNativeArrayIfFallback(s_DockingRaycastCommands, s_DockingRaycastCommandsVaultBacked, nameof(s_DockingRaycastCommands));
            RegisterNativeArrayIfFallback(s_DockingRaycastHits, s_DockingRaycastHitsVaultBacked, nameof(s_DockingRaycastHits));
            RegisterNativeArrayIfFallback(s_DockingRaycastSlots, s_DockingRaycastSlotsVaultBacked, nameof(s_DockingRaycastSlots));
            RegisterNativeArrayIfFallback(s_DroneTaskPriorityHeap, s_DroneTaskPriorityHeapVaultBacked, nameof(s_DroneTaskPriorityHeap));
            RegisterNativeArrayIfFallback(s_DroneStateDtos, s_DroneStateDtosVaultBacked, nameof(s_DroneStateDtos));
            RegisterNativeArrayIfFallback(s_DroneTargetDtos, s_DroneTargetDtosVaultBacked, nameof(s_DroneTargetDtos));
            RegisterNativeArrayIfFallback(s_DroneAssignmentTasks, s_DroneAssignmentTasksVaultBacked, nameof(s_DroneAssignmentTasks));
            RegisterNativeArrayIfFallback(s_DroneProceduralArgs, s_DroneProceduralArgsVaultBacked, nameof(s_DroneProceduralArgs));
            RegisterNativeArrayIfFallback(s_DroneServiceCommands, s_DroneServiceCommandsVaultBacked, nameof(s_DroneServiceCommands));
            RegisterNativeArrayIfFallback(s_DroneServiceCommandCursor, s_DroneServiceCommandCursorVaultBacked, nameof(s_DroneServiceCommandCursor));
            RegisterNativeArrayIfFallback(s_DroneSpatialBucketHeads, s_DroneSpatialBucketHeadsVaultBacked, nameof(s_DroneSpatialBucketHeads));
            RegisterNativeArrayIfFallback(s_DroneSpatialNextIndices, s_DroneSpatialNextIndicesVaultBacked, nameof(s_DroneSpatialNextIndices));
            RegisterNativeArrayIfFallback(s_DroneSpatialKeys, s_DroneSpatialKeysVaultBacked, nameof(s_DroneSpatialKeys));
            RegisterNativeArrayIfFallback(s_DroneChassisSpecs, s_DroneChassisSpecsVaultBacked, nameof(s_DroneChassisSpecs));
            RegisterNativeArrayIfFallback(s_DroneSpecsCsvScratch, s_DroneSpecsCsvScratchVaultBacked, nameof(s_DroneSpecsCsvScratch));
            s_DroneHubs = new RepairDroneHub[HeadlessDroneCapacity]; // COLD ALLOC: RepairDroneHub[512] - managed hub owner lookup for late-frame service commits - owner: DroneFleetManager
            s_DroneSlotDroneIds = new int[HeadlessDroneCapacity]; // COLD ALLOC: int[512] - managed active drone id slots safe during job execution - owner: DroneFleetManager
            s_DroneSlotDestroyed = new bool[HeadlessDroneCapacity]; // COLD ALLOC: bool[512] - permanently consumed suicide-weld slots - owner: DroneFleetManager
            s_PendingAbortBySlot = new bool[HeadlessDroneCapacity]; // COLD ALLOC: bool[512] - deferred abort control flags - owner: DroneFleetManager
            s_PendingReleaseBySlot = new bool[HeadlessDroneCapacity]; // COLD ALLOC: bool[512] - deferred release control flags - owner: DroneFleetManager
            s_PendingHostileBySlot = new bool[HeadlessDroneCapacity]; // COLD ALLOC: bool[512] - deferred Logic-Leech hijack flags - owner: DroneFleetManager
            s_PendingResupplyGrantBySlot = new bool[HeadlessDroneCapacity]; // COLD ALLOC: bool[512] - command-queue storage commit success acks - owner: DroneFleetManager
            s_PendingResupplyFailureBySlot = new bool[HeadlessDroneCapacity]; // COLD ALLOC: bool[512] - command-queue storage commit failure acks - owner: DroneFleetManager
            s_TargetModulesByDroneSlot = new BaseModule[HeadlessDroneCapacity]; // COLD ALLOC: BaseModule[512] - managed target lookup for late-frame repair application - owner: DroneFleetManager
            s_TargetVoxelVolumesByDroneSlot = new HectonVoxelVolume[HeadlessDroneCapacity]; // COLD ALLOC: HectonVoxelVolume[512] - managed voxel target lookup for weld/carve commits - owner: DroneFleetManager
            s_DroneTaskKindsBySlot = new DroneFleetTaskKind[HeadlessDroneCapacity]; // COLD ALLOC: DroneFleetTaskKind[512] - managed task kind mirror for service application - owner: DroneFleetManager
            s_DronePositions = new Vector3[HeadlessDroneCapacity]; // COLD ALLOC: Vector3[512] - last completed drone positions for non-job contact queries - owner: DroneFleetManager
            s_TaskModuleRefs = new BaseModule[HeadlessTaskCapacity]; // COLD ALLOC: BaseModule[64] - native task index to managed module lookup - owner: DroneFleetManager
            s_TaskVoxelVolumeRefs = new HectonVoxelVolume[HeadlessTaskCapacity]; // COLD ALLOC: HectonVoxelVolume[64] - native task index to managed voxel lookup - owner: DroneFleetManager
            s_TaskKinds = new DroneFleetTaskKind[HeadlessTaskCapacity]; // COLD ALLOC: DroneFleetTaskKind[64] - native task index to managed task kind lookup - owner: DroneFleetManager
            s_PendingLaunches = new PendingDroneLaunch[HeadlessPendingLaunchCapacity]; // COLD ALLOC: PendingDroneLaunch[512] - slow-tick launch queue applied after job completion - owner: DroneFleetManager
            ClearAllHeadlessSlots();
            if (s_DroneTuningConstants.IsCreated && s_DroneTuningConstants.Length > 0)
                s_DroneTuningConstants[0] = DroneFleetTuningConstants.CreateDefault();
            ClearDroneChassisSpecs();
            if (s_DroneMacroWaypointStates.IsCreated)
            {
                for (int i = 0; i < s_DroneMacroWaypointStates.Length; i++)
                    s_DroneMacroWaypointStates[i] = 0;
            }
        }

        private static void ValidateDroneFleetDtoLayouts()
        {
            if (DroneFleetLayoutSentinel.ValidateDroneStateDTO() &&
                DroneFleetLayoutSentinel.ValidateDroneTargetDTO() &&
                DroneFleetLayoutSentinel.ValidateDroneTaskDTO() &&
                DroneFleetLayoutSentinel.ValidateDroneChassisSpecDTO())
            {
                return;
            }

            throw new InvalidOperationException("SHINOBU_128 drone fleet DTO ABI validation failed.");
        }

        private static void ReleaseHeadlessNativeMemory()
        {
            CompleteDockingObstacleAbortsForReset();
            ReleaseDroneVaultBuffer(ref s_DroneStates, ref s_DroneStatesHandle, ref s_DroneStatesVaultBacked, nameof(s_DroneStates));
            ReleaseDroneVaultBuffer(ref s_DroneStateBackBuffer, ref s_DroneStateBackBufferHandle, ref s_DroneStateBackBufferVaultBacked, nameof(s_DroneStateBackBuffer));
            ReleaseDroneVaultBuffer(ref s_DroneRenderMatrices, ref s_DroneRenderMatricesHandle, ref s_DroneRenderMatricesVaultBacked, nameof(s_DroneRenderMatrices));
            ReleaseDroneVaultBuffer(ref s_DroneRenderMatrixBackBuffer, ref s_DroneRenderMatrixBackBufferHandle, ref s_DroneRenderMatrixBackBufferVaultBacked, nameof(s_DroneRenderMatrixBackBuffer));
            ReleaseDroneVaultBuffer(ref s_DroneRenderInstances, ref s_DroneRenderInstancesHandle, ref s_DroneRenderInstancesVaultBacked, nameof(s_DroneRenderInstances));
            ReleaseDroneCullingStatesBuffer();
            ReleaseDroneVaultBuffer(ref s_DronePositionsSoA, ref s_DronePositionsSoAHandle, ref s_DronePositionsSoAVaultBacked, nameof(s_DronePositionsSoA));
            ReleaseDroneVaultBuffer(ref s_DroneStateBytes, ref s_DroneStateBytesHandle, ref s_DroneStateBytesVaultBacked, nameof(s_DroneStateBytes));
            ReleaseDroneVaultBuffer(ref s_DroneBlackBox, ref s_DroneBlackBoxHandle, ref s_DroneBlackBoxVaultBacked, nameof(s_DroneBlackBox));
            ReleaseDroneVaultBuffer(ref s_DroneTuningConstants, ref s_DroneTuningConstantsHandle, ref s_DroneTuningConstantsVaultBacked, nameof(s_DroneTuningConstants));
            ReleaseDroneVaultBuffer(ref s_DroneMacroWaypoints, ref s_DroneMacroWaypointsHandle, ref s_DroneMacroWaypointsVaultBacked, nameof(s_DroneMacroWaypoints));
            ReleaseDroneVaultBuffer(ref s_DroneMacroWaypointStates, ref s_DroneMacroWaypointStatesHandle, ref s_DroneMacroWaypointStatesVaultBacked, nameof(s_DroneMacroWaypointStates));
            ReleaseDroneVaultBuffer(ref s_DroneAStarOpenHeap, ref s_DroneAStarOpenHeapHandle, ref s_DroneAStarOpenHeapVaultBacked, nameof(s_DroneAStarOpenHeap));
            ReleaseDroneVaultBuffer(ref s_DroneAStarGCosts, ref s_DroneAStarGCostsHandle, ref s_DroneAStarGCostsVaultBacked, nameof(s_DroneAStarGCosts));
            ReleaseDroneVaultBuffer(ref s_DroneAStarCameFrom, ref s_DroneAStarCameFromHandle, ref s_DroneAStarCameFromVaultBacked, nameof(s_DroneAStarCameFrom));
            ReleaseDroneVaultBuffer(ref s_DroneAStarNodeStates, ref s_DroneAStarNodeStatesHandle, ref s_DroneAStarNodeStatesVaultBacked, nameof(s_DroneAStarNodeStates));
            ReleaseDroneVaultBuffer(ref s_DroneMacroRouteNodes, ref s_DroneMacroRouteNodesHandle, ref s_DroneMacroRouteNodesVaultBacked, nameof(s_DroneMacroRouteNodes));
            ReleaseDroneVaultBuffer(ref s_DroneMacroRouteCounts, ref s_DroneMacroRouteCountsHandle, ref s_DroneMacroRouteCountsVaultBacked, nameof(s_DroneMacroRouteCounts));
            ReleaseDroneVaultBuffer(ref s_DroneAStarTelemetry, ref s_DroneAStarTelemetryHandle, ref s_DroneAStarTelemetryVaultBacked, nameof(s_DroneAStarTelemetry));
            ReleaseDroneVaultBuffer(ref s_HeadlessTaskClaimOwners, ref s_HeadlessTaskClaimOwnersHandle, ref s_HeadlessTaskClaimOwnersVaultBacked, nameof(s_HeadlessTaskClaimOwners));
            ReleaseDroneVaultBuffer(ref s_FleetTelemetryAccumulator, ref s_FleetTelemetryAccumulatorHandle, ref s_FleetTelemetryAccumulatorVaultBacked, nameof(s_FleetTelemetryAccumulator));
            ReleaseDroneVaultBuffer(ref s_DockingRaycastCommands, ref s_DockingRaycastCommandsHandle, ref s_DockingRaycastCommandsVaultBacked, nameof(s_DockingRaycastCommands));
            ReleaseDroneVaultBuffer(ref s_DockingRaycastHits, ref s_DockingRaycastHitsHandle, ref s_DockingRaycastHitsVaultBacked, nameof(s_DockingRaycastHits));
            ReleaseDroneVaultBuffer(ref s_DockingRaycastSlots, ref s_DockingRaycastSlotsHandle, ref s_DockingRaycastSlotsVaultBacked, nameof(s_DockingRaycastSlots));
            ReleaseDroneVaultBuffer(ref s_DroneTaskPriorityHeap, ref s_DroneTaskPriorityHeapHandle, ref s_DroneTaskPriorityHeapVaultBacked, nameof(s_DroneTaskPriorityHeap));
            ReleaseDroneVaultBuffer(ref s_DroneStateDtos, ref s_DroneStateDtosHandle, ref s_DroneStateDtosVaultBacked, nameof(s_DroneStateDtos));
            ReleaseDroneVaultBuffer(ref s_DroneTargetDtos, ref s_DroneTargetDtosHandle, ref s_DroneTargetDtosVaultBacked, nameof(s_DroneTargetDtos));
            ReleaseDroneVaultBuffer(ref s_DroneAssignmentTasks, ref s_DroneAssignmentTasksHandle, ref s_DroneAssignmentTasksVaultBacked, nameof(s_DroneAssignmentTasks));
            ReleaseDroneVaultBuffer(ref s_DroneProceduralArgs, ref s_DroneProceduralArgsHandle, ref s_DroneProceduralArgsVaultBacked, nameof(s_DroneProceduralArgs));
            ReleaseDroneVaultBuffer(ref s_DroneServiceCommands, ref s_DroneServiceCommandsHandle, ref s_DroneServiceCommandsVaultBacked, nameof(s_DroneServiceCommands));
            ReleaseDroneVaultBuffer(ref s_DroneServiceCommandCursor, ref s_DroneServiceCommandCursorHandle, ref s_DroneServiceCommandCursorVaultBacked, nameof(s_DroneServiceCommandCursor));
            ReleaseDroneVaultBuffer(ref s_DroneSpatialBucketHeads, ref s_DroneSpatialBucketHeadsHandle, ref s_DroneSpatialBucketHeadsVaultBacked, nameof(s_DroneSpatialBucketHeads));
            ReleaseDroneVaultBuffer(ref s_DroneSpatialNextIndices, ref s_DroneSpatialNextIndicesHandle, ref s_DroneSpatialNextIndicesVaultBacked, nameof(s_DroneSpatialNextIndices));
            ReleaseDroneVaultBuffer(ref s_DroneSpatialKeys, ref s_DroneSpatialKeysHandle, ref s_DroneSpatialKeysVaultBacked, nameof(s_DroneSpatialKeys));
            ReleaseDroneVaultBuffer(ref s_DroneChassisSpecs, ref s_DroneChassisSpecsHandle, ref s_DroneChassisSpecsVaultBacked, nameof(s_DroneChassisSpecs));
            ReleaseDroneVaultBuffer(ref s_DroneSpecsCsvScratch, ref s_DroneSpecsCsvScratchHandle, ref s_DroneSpecsCsvScratchVaultBacked, nameof(s_DroneSpecsCsvScratch));

            s_DroneHubs = null;
            s_DroneSlotDroneIds = null;
            s_DroneSlotDestroyed = null;
            s_PendingAbortBySlot = null;
            s_PendingReleaseBySlot = null;
            s_PendingHostileBySlot = null;
            s_PendingResupplyGrantBySlot = null;
            s_PendingResupplyFailureBySlot = null;
            s_TargetModulesByDroneSlot = null;
            s_TargetVoxelVolumesByDroneSlot = null;
            s_DroneTaskKindsBySlot = null;
            s_DronePositions = null;
            s_TaskModuleRefs = null;
            s_TaskVoxelVolumeRefs = null;
            s_TaskKinds = null;
            s_PendingLaunches = null;
            s_DroneChassisSpecCount = 0;
        }

        private static NativeArray<DroneCullingStateGpu> ResolveDroneCullingStatesBuffer()
        {
            return ResolveDroneVaultBuffer<DroneCullingStateGpu>(
                BufferID.DroneFleetCullingStates,
                HeadlessDroneCapacity,
                NativeArrayOptions.ClearMemory,
                ref s_DroneCullingStatesHandle,
                out s_DroneCullingStatesVaultBacked);
        }

        internal static NativeArray<T> ResolveDroneVaultBuffer<T>(
            BufferID bufferId,
            int length,
            NativeArrayOptions options,
            ref VaultBufferHandle<T> handle,
            out bool vaultBacked) where T : struct
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null && GlobalDataVault.TryGetLatestCreated(out GlobalDataVault latestVault))
                vault = latestVault;

            if (vault != null)
            {
                handle = vault.GetBufferHandle<T>(
                    bufferId,
                    length,
                    SystemID.Construction,
                    options);
                NativeArray<T> buffer = handle.Resolve(vault);
                if (buffer.IsCreated)
                {
                    vaultBacked = true;
                    return buffer;
                }

                handle = default;
            }

            vaultBacked = false;
            return H8Memory.Allocate<T>(
                length,
                SystemID.Construction,
                Allocator.Persistent,
                options);
        }

        private static void ReleaseDroneCullingStatesBuffer()
        {
            ReleaseDroneVaultBuffer(ref s_DroneCullingStates, ref s_DroneCullingStatesHandle, ref s_DroneCullingStatesVaultBacked, nameof(s_DroneCullingStates));
        }

        internal static void RegisterNativeArrayIfFallback<T>(NativeArray<T> array, bool vaultBacked, string label) where T : struct
        {
            if (!array.IsCreated || vaultBacked)
                return;

            RegisterNativeArray(array, label);
        }

        internal static void ReleaseDroneVaultBuffer<T>(
            ref NativeArray<T> array,
            ref VaultBufferHandle<T> handle,
            ref bool vaultBacked,
            string label) where T : struct
        {
            if (!array.IsCreated)
            {
                handle = default;
                vaultBacked = false;
                return;
            }

            if (vaultBacked)
            {
                array = default;
                handle = default;
                vaultBacked = false;
                return;
            }

            NativeMemorySentinel.UnregisterNativeArray(array);
            H8Memory.Release(ref array, SystemID.Construction);
            handle = default;
            vaultBacked = false;
        }

        private static void TryRegisterHeadlessDriver()
        {
            if (s_HeadlessDriverRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(s_HeadlessDriver, PriorityLayer.Environment);
            GlobalRegistry.RegisterLateFrameTickable(s_HeadlessDriver, PriorityLayer.Environment);
            GlobalRegistry.Renderables.Register(s_HeadlessDriver);
            s_HeadlessDriverRegistered = true;
        }

        private static void TryUnregisterHeadlessDriver()
        {
            if (!s_HeadlessDriverRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(s_HeadlessDriver, PriorityLayer.Environment);
            GlobalRegistry.UnregisterLateFrameTickable(s_HeadlessDriver, PriorityLayer.Environment);
            GlobalRegistry.Renderables.Unregister(s_HeadlessDriver);
            s_HeadlessDriverRegistered = false;
        }

        private static void ScheduleHeadlessSimulation(float deltaTime)
        {
            EnsureInitialized();
            if (s_HeadlessJobScheduled || CountManagedHeadlessDrones() <= 0)
                return;

            s_LastHeadlessDeltaTime = Mathf.Max(0f, deltaTime);
            BuildHeadlessTaskMap(deltaTime);
            BuildHeadlessSpatialHash();
            ClearHeadlessTaskClaims();
            ClearFleetTelemetryAccumulator();
            ApplyDockingRequestSignals();
            ResolveDockingObstacleAborts();

            bool hasPlayer = TryResolvePlayerPosition(out Vector3 playerPosition);
            bool hasFormationAnchor = TryResolveFormationAnchor(out Vector3 formationAnchorPosition);
            bool hasAbyssalFlow = TryResolveAbyssalFlowVolumePayload(
                out NativeArray<float3> abyssalFlowVolume,
                out Vector3 abyssalFlowCenter,
                out int abyssalFlowResolutionXZ,
                out int abyssalFlowResolutionY,
                out int abyssalFlowRingOffsetX,
                out int abyssalFlowRingOffsetY,
                out int abyssalFlowRingOffsetZ,
                out float abyssalFlowHorizontalCellSize,
                out float abyssalFlowVerticalCellSize,
                out float abyssalFlowSurfaceY,
                out float abyssalFlowDepthMeters);
            ResolveFluidCurrentSnapshot(
                out Vector3 baseFlowVelocity,
                out bool phantomFlowEnabled,
                out float phantomFlowNoiseScale,
                out float phantomFlowTimeScale,
                out float phantomFlowStrength,
                out float phantomFlowVerticalFactor);

            DroneFleetTuningConstants tuning = ResolveDroneTuning();
            int frameIndex = s_DroneFrameIndex++;
            int steeringTickModulo = ResolveDroneSteeringTickModulo(in tuning);
            int aStarSolveBudget = ResolveDroneAStarSolveBudget(in tuning);
            MockSDFGrid sdfGrid = BuildMockSdfGrid(in tuning);
            JobHandle macroAStarHandle = ScheduleDroneMacroAStar(frameIndex, aStarSolveBudget, in tuning, in sdfGrid);
            JobHandle assignmentHandle = macroAStarHandle;
            if (s_FleetFormationMode == DroneFleetFormationMode.Repair &&
                s_DroneAssignmentTasks.IsCreated &&
                s_DroneStateDtos.IsCreated &&
                s_DroneTargetDtos.IsCreated &&
                s_HeadlessTaskClaimOwners.IsCreated)
            {
                DroneTaskAssignmentJob assignmentJob = new DroneTaskAssignmentJob
                {
                    Drones = s_DroneStates,
                    DroneStatesDto = s_DroneStateDtos,
                    DroneTargets = s_DroneTargetDtos,
                    Tasks = s_DroneAssignmentTasks,
                    TaskClaimOwners = s_HeadlessTaskClaimOwners,
                    TaskCount = s_HeadlessTaskCount,
                    EmergencyOverclock = IsEmergencyOverclockActive ? 1 : 0
                };
                assignmentHandle = assignmentJob.Schedule(HeadlessDroneCapacity, DroneJobBatchSize, macroAStarHandle);
            }
            s_LastDroneSteeringTickModulo = steeringTickModulo;
            if (s_DroneServiceCommandCursor.IsCreated && s_DroneServiceCommandCursor.Length > 0)
                s_DroneServiceCommandCursor[0] = default;

            DroneCognitionJob job = default;
            job.ReadDrones = s_DroneStates;
            job.Drones = s_DroneStateBackBuffer;
            job.DroneStatesDto = s_DroneStateDtos;
            job.DroneTargets = s_DroneTargetDtos;
            job.RenderMatrices = s_DroneRenderMatrixBackBuffer;
            job.DronePositions = s_DronePositionsSoA;
            job.DroneStates = s_DroneStateBytes;
            job.DroneSpatialBucketHeads = s_DroneSpatialBucketHeads;
            job.DroneSpatialNextIndices = s_DroneSpatialNextIndices;
            job.DroneSpatialKeys = s_DroneSpatialKeys;
            job.AbyssalFlowVolume = abyssalFlowVolume;
            job.MacroWaypoints = s_DroneMacroWaypoints;
            job.MacroWaypointStates = s_DroneMacroWaypointStates;
            job.TaskClaimOwners = s_HeadlessTaskClaimOwners;
            job.TelemetryAccumulator = s_FleetTelemetryAccumulator;
            job.ServiceCommands = s_DroneServiceCommands;
            job.ServiceCommandCursor = s_DroneServiceCommandCursor;
            job.ServiceCommandCapacity = DroneServiceCommandCapacity;
            job.DeltaTime = s_LastHeadlessDeltaTime;
            job.ServiceQueueEnabled = s_DroneServiceCommands.IsCreated && s_DroneServiceCommandCursor.IsCreated ? 1 : 0;
            job.PlayerPosition = ToFloat3(playerPosition);
            job.PlayerPositionValid = hasPlayer ? 1 : 0;
            job.EmergencyOverclock = IsEmergencyOverclockActive ? 1 : 0;
            job.FormationMode = (int)s_FleetFormationMode;
            job.DroneSpatialBucketMask = DroneSpatialBucketCapacity - 1;
            job.FormationAnchorPosition = ToFloat3(formationAnchorPosition);
            job.FormationAnchorValid = hasFormationAnchor ? 1 : 0;
            job.AbyssalFlowVolumeValid = hasAbyssalFlow ? 1 : 0;
            job.AbyssalFlowResolutionXZ = abyssalFlowResolutionXZ;
            job.AbyssalFlowResolutionY = abyssalFlowResolutionY;
            job.AbyssalFlowRingOffsetX = abyssalFlowRingOffsetX;
            job.AbyssalFlowRingOffsetY = abyssalFlowRingOffsetY;
            job.AbyssalFlowRingOffsetZ = abyssalFlowRingOffsetZ;
            job.AbyssalFlowCenter = ToFloat3(abyssalFlowCenter);
            job.AbyssalFlowHorizontalCellSize = abyssalFlowHorizontalCellSize;
            job.AbyssalFlowVerticalCellSize = abyssalFlowVerticalCellSize;
            job.AbyssalFlowWaterLevel = abyssalFlowSurfaceY;
            job.AbyssalFlowDepthMeters = abyssalFlowDepthMeters;
            job.BaseFlowVelocity = ToFloat3(baseFlowVelocity);
            job.PhantomFlowTime = Time.time;
            job.PhantomFlowNoiseScale = phantomFlowNoiseScale;
            job.PhantomFlowTimeScale = phantomFlowTimeScale;
            job.PhantomFlowStrength = phantomFlowStrength;
            job.PhantomFlowVerticalFactor = phantomFlowVerticalFactor;
            job.PhantomFlowEnabled = phantomFlowEnabled ? 1 : 0;
            job.FlowDragCoefficient = DroneFlowDragCoefficient;
            job.CrossCurrentVisualSlipWeight = ResolveGlobalQualityWeight();
            job.SdfGrid = sdfGrid;
            job.FrameIndex = frameIndex;
            job.SteeringTickModulo = steeringTickModulo;
            job.SdfRepulsionStrength = tuning.SdfRepulsionStrength;
            JobHandle cognitionHandle = job.Schedule(HeadlessDroneCapacity, DroneJobBatchSize, assignmentHandle);
            DroneMetabolismJob metabolismJob = new DroneMetabolismJob
            {
                Drones = s_DroneStateBackBuffer,
                DroneStatesDto = s_DroneStateDtos,
                DroneTargets = s_DroneTargetDtos,
                DeltaTime = s_LastHeadlessDeltaTime,
                EmergencyOverclock = IsEmergencyOverclockActive ? 1 : 0
            };
            JobHandle metabolismHandle = metabolismJob.Schedule(HeadlessDroneCapacity, DroneJobBatchSize, cognitionHandle);
            ExtractDroneMatricesJob matrixJob = new ExtractDroneMatricesJob
            {
                Drones = s_DroneStateBackBuffer,
                DroneStatesDto = s_DroneStateDtos,
                Matrices = s_DroneRenderMatrixBackBuffer,
                CameraAup = ResolveDroneRenderReferenceAup(),
                ScaleMeters = DroneProceduralScaleMeters
            };
            JobHandle matrixHandle = matrixJob.Schedule(HeadlessDroneCapacity, DroneJobBatchSize, metabolismHandle);
            if (s_DroneProceduralArgs.IsCreated)
            {
                BuildDroneProceduralArgsJob argsJob = new BuildDroneProceduralArgsJob
                {
                    Args = s_DroneProceduralArgs,
                    VertexCountPerInstance = DroneProceduralVerticesPerInstance,
                    InstanceCount = (uint)HeadlessDroneCapacity
                };
                s_HeadlessJobHandle = argsJob.Schedule(matrixHandle);
            }
            else
            {
                s_HeadlessJobHandle = matrixHandle;
            }
            s_HeadlessJobScheduled = true;
        }

        private static JobHandle ScheduleDroneMacroAStar(
            int frameIndex,
            int solveBudget,
            in DroneFleetTuningConstants tuning,
            in MockSDFGrid sdfGrid)
        {
            if (!s_DroneMacroWaypoints.IsCreated || !s_DroneMacroWaypointStates.IsCreated)
            {
                return default;
            }

            ClearDroneMacroWaypointsJob job = new ClearDroneMacroWaypointsJob
            {
                Waypoints = s_DroneMacroWaypoints,
                WaypointStates = s_DroneMacroWaypointStates
            };

            return job.Schedule(HeadlessDroneCapacity, DroneJobBatchSize);
        }

        private static DroneFleetTuningConstants ResolveDroneTuning()
        {
            if (s_DroneTuningConstants.IsCreated && s_DroneTuningConstants.Length > 0)
                return SanitizeDroneTuning(s_DroneTuningConstants[0]);

            return DroneFleetTuningConstants.CreateDefault();
        }

        private static DroneFleetTuningConstants SanitizeDroneTuning(DroneFleetTuningConstants tuning)
        {
            DroneFleetTuningConstants fallback = DroneFleetTuningConstants.CreateDefault();
            if (tuning.MaxDroneSpeed <= 0f)
                tuning.MaxDroneSpeed = fallback.MaxDroneSpeed;
            if (tuning.BatteryDrainRate <= 0f)
                tuning.BatteryDrainRate = fallback.BatteryDrainRate;
            if (tuning.RepairSpeed <= 0f)
                tuning.RepairSpeed = fallback.RepairSpeed;
            if (tuning.CargoCapacity <= 0f)
                tuning.CargoCapacity = fallback.CargoCapacity;
            if (tuning.MiningHoldSeconds <= 0f)
                tuning.MiningHoldSeconds = fallback.MiningHoldSeconds;
            if (tuning.LowTierSteeringHz <= 0f)
                tuning.LowTierSteeringHz = fallback.LowTierSteeringHz;
            if (tuning.MidTierSteeringHz <= 0f)
                tuning.MidTierSteeringHz = fallback.MidTierSteeringHz;
            if (tuning.HighTierSteeringHz <= 0f)
                tuning.HighTierSteeringHz = fallback.HighTierSteeringHz;
            if (tuning.UltraTierSteeringHz <= 0f)
                tuning.UltraTierSteeringHz = fallback.UltraTierSteeringHz;
            if (tuning.AStarCellSize <= 0f)
                tuning.AStarCellSize = fallback.AStarCellSize;
            if (tuning.LowTierSolveBudget <= 0f)
                tuning.LowTierSolveBudget = fallback.LowTierSolveBudget;
            if (tuning.MidTierSolveBudget <= 0f)
                tuning.MidTierSolveBudget = fallback.MidTierSolveBudget;
            if (tuning.HighTierSolveBudget <= 0f)
                tuning.HighTierSolveBudget = fallback.HighTierSolveBudget;
            if (tuning.UltraTierSolveBudget <= 0f)
                tuning.UltraTierSolveBudget = fallback.UltraTierSolveBudget;

            tuning.MaxDroneSpeed = Mathf.Clamp(tuning.MaxDroneSpeed, 0.5f, 24f);
            tuning.BatteryDrainRate = Mathf.Clamp(tuning.BatteryDrainRate, 0.01f, 25f);
            tuning.SdfRepulsionStrength = Mathf.Clamp(tuning.SdfRepulsionStrength, 0f, 24f);
            tuning.RepairSpeed = Mathf.Clamp(tuning.RepairSpeed, 0.05f, 8f);
            tuning.CargoCapacity = Mathf.Clamp(tuning.CargoCapacity, 1f, 64f);
            tuning.MiningHoldSeconds = Mathf.Clamp(tuning.MiningHoldSeconds, 0.01f, 5f);
            tuning.LowTierSteeringHz = Mathf.Clamp(tuning.LowTierSteeringHz, 5f, 60f);
            tuning.MidTierSteeringHz = Mathf.Clamp(tuning.MidTierSteeringHz, 10f, 60f);
            tuning.HighTierSteeringHz = Mathf.Clamp(tuning.HighTierSteeringHz, 15f, 120f);
            tuning.UltraTierSteeringHz = Mathf.Clamp(tuning.UltraTierSteeringHz, 15f, 120f);
            tuning.AStarCellSize = Mathf.Clamp(tuning.AStarCellSize, 1f, 12f);
            tuning.LowTierSolveBudget = Mathf.Clamp(tuning.LowTierSolveBudget, 1f, HeadlessDroneCapacity);
            tuning.MidTierSolveBudget = Mathf.Clamp(tuning.MidTierSolveBudget, 1f, HeadlessDroneCapacity);
            tuning.HighTierSolveBudget = Mathf.Clamp(tuning.HighTierSolveBudget, 1f, HeadlessDroneCapacity);
            tuning.UltraTierSolveBudget = Mathf.Clamp(tuning.UltraTierSolveBudget, 1f, HeadlessDroneCapacity);
            return tuning;
        }

        private static void ClearDroneChassisSpecs()
        {
            s_DroneChassisSpecCount = 0;
            if (!s_DroneChassisSpecs.IsCreated)
                return;

            for (int i = 0; i < s_DroneChassisSpecs.Length; i++)
                s_DroneChassisSpecs[i] = default;
        }

        private static DroneChassisSpecDTO CreateFallbackDroneChassisSpec(uint typeHash, in DroneFleetTuningConstants tuning)
        {
            float speedScale = 1f;
            float drainScale = 1f;
            float repairScale = 1f;
            float cargoScale = 1f;
            float miningHoldScale = 1f;

            if (typeHash == DroneChassisMiningHash)
            {
                speedScale = 0.85f;
                drainScale = 0.9f;
                cargoScale = 1.5f;
                miningHoldScale = 0.85f;
            }
            else if (typeHash == DroneChassisCombatHash || typeHash == DroneChassisCutParasiteHash)
            {
                speedScale = 1.15f;
                drainScale = 1.25f;
                repairScale = 0.75f;
                cargoScale = 0.5f;
            }

            DroneChassisSpecDTO spec = new DroneChassisSpecDTO
            {
                TypeHash = typeHash,
                Flags = DroneChassisSpecValidFlag,
                MaxSpeed = tuning.MaxDroneSpeed * speedScale,
                BatteryCapacity = 100f,
                BatteryDrainRate = tuning.BatteryDrainRate * drainScale,
                RepairSpeed = tuning.RepairSpeed * repairScale,
                CargoCapacity = tuning.CargoCapacity * cargoScale,
                MiningHoldSeconds = tuning.MiningHoldSeconds * miningHoldScale,
                SdfRepulsionScale = 1f,
                Reserved0 = 0f
            };
            return SanitizeDroneChassisSpec(spec, in tuning);
        }

        private static DroneChassisSpecDTO SanitizeDroneChassisSpec(DroneChassisSpecDTO spec, in DroneFleetTuningConstants tuning)
        {
            if (spec.TypeHash == 0u)
                spec.TypeHash = DroneChassisRepairHash;

            if (spec.MaxSpeed <= 0f)
                spec.MaxSpeed = tuning.MaxDroneSpeed;
            if (spec.BatteryCapacity <= 0f)
                spec.BatteryCapacity = 100f;
            if (spec.BatteryDrainRate <= 0f)
                spec.BatteryDrainRate = tuning.BatteryDrainRate;
            if (spec.RepairSpeed <= 0f)
                spec.RepairSpeed = tuning.RepairSpeed;
            if (spec.CargoCapacity <= 0f)
                spec.CargoCapacity = tuning.CargoCapacity;
            if (spec.MiningHoldSeconds <= 0f)
                spec.MiningHoldSeconds = tuning.MiningHoldSeconds;
            if (spec.SdfRepulsionScale <= 0f)
                spec.SdfRepulsionScale = 1f;

            spec.MaxSpeed = Mathf.Clamp(spec.MaxSpeed, 0.5f, 24f);
            spec.BatteryCapacity = Mathf.Clamp(spec.BatteryCapacity, 1f, 100f);
            spec.BatteryDrainRate = Mathf.Clamp(spec.BatteryDrainRate, 0.01f, 25f);
            spec.RepairSpeed = Mathf.Clamp(spec.RepairSpeed, 0.05f, 8f);
            spec.CargoCapacity = Mathf.Clamp(spec.CargoCapacity, 1f, 64f);
            spec.MiningHoldSeconds = Mathf.Clamp(spec.MiningHoldSeconds, 0.01f, 5f);
            spec.SdfRepulsionScale = Mathf.Clamp(spec.SdfRepulsionScale, 0.1f, 4f);
            spec.Flags |= DroneChassisSpecValidFlag;
            spec.Reserved0 = 0f;
            spec._pad0 = 0ul;
            spec._pad1 = 0ul;
            spec._pad2 = 0ul;
            return spec;
        }

        private static void CommitDroneChassisSpecs(ReadOnlySpan<DroneChassisSpecDTO> stagedSpecs, int stagedCount)
        {
            if (!s_DroneChassisSpecs.IsCreated || s_DroneChassisSpecs.Length <= 0 || stagedCount <= 0)
                return;

            ClearDroneChassisSpecs();
            int count = Mathf.Min(stagedCount, s_DroneChassisSpecs.Length);
            for (int i = 0; i < count; i++)
                s_DroneChassisSpecs[i] = stagedSpecs[i];

            s_DroneChassisSpecCount = count;
        }

        private static bool TryUpsertStagedDroneChassisSpec(
            DroneChassisSpecDTO spec,
            in DroneFleetTuningConstants tuning,
            Span<DroneChassisSpecDTO> stagedSpecs,
            ref int stagedCount)
        {
            if (stagedSpecs.Length <= 0)
                return false;

            spec = SanitizeDroneChassisSpec(spec, in tuning);
            int count = Mathf.Clamp(stagedCount, 0, stagedSpecs.Length);
            stagedCount = count;
            for (int i = 0; i < count; i++)
            {
                if ((stagedSpecs[i].Flags & DroneChassisSpecValidFlag) == 0u ||
                    stagedSpecs[i].TypeHash != spec.TypeHash)
                {
                    continue;
                }

                stagedSpecs[i] = spec;
                return true;
            }

            if (count >= stagedSpecs.Length)
                return false;

            stagedSpecs[count] = spec;
            stagedCount = count + 1;
            return true;
        }

        private static bool TryResolveDroneChassisSpec(uint typeHash, out DroneChassisSpecDTO spec)
        {
            spec = default;
            if (!s_DroneChassisSpecs.IsCreated || s_DroneChassisSpecCount <= 0)
                return false;

            int count = Mathf.Min(s_DroneChassisSpecCount, s_DroneChassisSpecs.Length);
            for (int i = 0; i < count; i++)
            {
                DroneChassisSpecDTO candidate = s_DroneChassisSpecs[i];
                if ((candidate.Flags & DroneChassisSpecValidFlag) == 0u || candidate.TypeHash != typeHash)
                    continue;

                spec = candidate;
                return true;
            }

            return false;
        }

        private static uint ResolveDroneChassisHash(DroneFleetTaskKind kind)
        {
            if (kind == DroneFleetTaskKind.MineNode)
                return DroneChassisMiningHash;

            if (kind == DroneFleetTaskKind.CutParasite)
                return DroneChassisCombatHash;

            return DroneChassisRepairHash;
        }

        private static DroneChassisSpecDTO ResolveLaunchDroneChassisSpec(DroneFleetTaskKind kind, in DroneFleetTuningConstants tuning)
        {
            uint typeHash = ResolveDroneChassisHash(kind);
            if (TryResolveDroneChassisSpec(typeHash, out DroneChassisSpecDTO spec))
                return SanitizeDroneChassisSpec(spec, in tuning);

            if (kind == DroneFleetTaskKind.CutParasite &&
                TryResolveDroneChassisSpec(DroneChassisCutParasiteHash, out spec))
            {
                return SanitizeDroneChassisSpec(spec, in tuning);
            }

            return CreateFallbackDroneChassisSpec(typeHash, in tuning);
        }

        private static int ResolveDroneSteeringTickModulo(in DroneFleetTuningConstants tuning)
        {
            float quality = ResolveGlobalQualityWeight();
            float lowHz = Mathf.Max(1f, tuning.LowTierSteeringHz);
            float highHz = Mathf.Max(lowHz, tuning.UltraTierSteeringHz);
            float targetHz = math.lerp(lowHz, highHz, quality);
            return Mathf.Clamp(Mathf.RoundToInt(60f / Mathf.Max(1f, targetHz)), 1, 12);
        }

        private static int ResolveDroneAStarSolveBudget(in DroneFleetTuningConstants tuning)
        {
            float quality = ResolveGlobalQualityWeight();
            float smoothedQuality = quality * quality * (3f - (2f * quality));
            float budget = math.lerp(
                Mathf.Max(1f, tuning.LowTierSolveBudget),
                Mathf.Max(1f, tuning.UltraTierSolveBudget),
                smoothedQuality);
            return Mathf.Clamp(Mathf.RoundToInt(budget), 1, HeadlessDroneCapacity);
        }

        private static int ResolveDroneFramesBetweenUpdates()
        {
            float quality = ResolveGlobalQualityWeight();
            return Mathf.Clamp((int)math.lerp(5f, 60f, 1f - quality), 5, 60);
        }

        private static float ResolveDroneTaskRebuildIntervalSeconds()
        {
            return ResolveDroneFramesBetweenUpdates() * (1f / 60f);
        }

        private static float ResolveGlobalQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private static MockSDFGrid BuildMockSdfGrid(in DroneFleetTuningConstants tuning)
        {
            MockSDFGrid grid = MockSDFGrid.CreateDefault();
            grid.RepulsionDistance = Mathf.Max(0.5f, tuning.AStarCellSize * 0.65f);
            return grid;
        }

        private static void ReadDroneAStarTelemetry()
        {
            if (!s_DroneAStarTelemetry.IsCreated || s_DroneAStarTelemetry.Length <= 0)
                return;

            DroneAStarTelemetry telemetry = s_DroneAStarTelemetry[0];
            s_DroneAStarSolvedCount += telemetry.SolvedCount;
            s_DroneAStarFailureCount += telemetry.FailedCount;
            s_DroneAStarIterationCount += telemetry.IterationCount;
            s_LastDroneAStarStatus = telemetry.LastStatus;
            int attemptCount = telemetry.SolvedCount + telemetry.FailedCount;
            s_LastDroneAStarAveragePathfindingTimeMs = EstimateAStarAveragePathfindingTimeMs(telemetry.IterationCount, attemptCount);
        }

        private static float EstimateAStarAveragePathfindingTimeMs(int iterationCount, int attemptCount)
        {
            if (attemptCount <= 0 || iterationCount <= 0)
                return 0f;

            float averageIterations = iterationCount * math.rcp(math.max(1f, attemptCount));
            return averageIterations * 0.000045f;
        }

        private static void CompleteHeadlessSimulationAndApply()
        {
            if (!s_DroneStates.IsCreated)
                return;

            if (s_HeadlessJobScheduled)
            {
                if (!DispatcherJobSwap.TryComplete(ref s_HeadlessJobHandle, false))
                    return;

                s_HeadlessJobScheduled = false;
                NativeArray<HeadlessDroneState> swap = s_DroneStates;
                s_DroneStates = s_DroneStateBackBuffer;
                s_DroneStateBackBuffer = swap;
                VaultBufferHandle<HeadlessDroneState> stateHandleSwap = s_DroneStatesHandle;
                s_DroneStatesHandle = s_DroneStateBackBufferHandle;
                s_DroneStateBackBufferHandle = stateHandleSwap;
                bool stateVaultBackedSwap = s_DroneStatesVaultBacked;
                s_DroneStatesVaultBacked = s_DroneStateBackBufferVaultBacked;
                s_DroneStateBackBufferVaultBacked = stateVaultBackedSwap;
                NativeArray<float4x4> matrixSwap = s_DroneRenderMatrices;
                s_DroneRenderMatrices = s_DroneRenderMatrixBackBuffer;
                s_DroneRenderMatrixBackBuffer = matrixSwap;
                VaultBufferHandle<float4x4> matrixHandleSwap = s_DroneRenderMatricesHandle;
                s_DroneRenderMatricesHandle = s_DroneRenderMatrixBackBufferHandle;
                s_DroneRenderMatrixBackBufferHandle = matrixHandleSwap;
                bool matrixVaultBackedSwap = s_DroneRenderMatricesVaultBacked;
                s_DroneRenderMatricesVaultBacked = s_DroneRenderMatrixBackBufferVaultBacked;
                s_DroneRenderMatrixBackBufferVaultBacked = matrixVaultBackedSwap;
                ReadDroneAStarTelemetry();
            }

            ApplyPendingControls();
            ApplyCompletedHeadlessServices();
            DrainDroneServiceCommandQueue();
            ApplyPendingLaunches();
            RefreshHeadlessCounters();
            UpdateDrawBounds();
            CaptureFleetBlackBoxFrame();
            PublishSnapshot();
            PublishFleetTelemetryIfDue();
        }

        private static void CompletePendingHeadlessJobForReset()
        {
            if (!s_HeadlessJobScheduled)
                return;

            DispatcherJobSwap.TryComplete(ref s_HeadlessJobHandle, true);
            s_HeadlessJobScheduled = false;
        }

        internal static void ApplyOriginShift(Vector3 shiftOffset)
        {
            if (!IsFiniteVector(shiftOffset) || shiftOffset.sqrMagnitude <= 0.000001f)
                return;

            EnsureInitialized();
            if (!s_DroneStates.IsCreated || !s_DroneRenderMatrices.IsCreated)
                return;

            CompletePendingHeadlessJobForReset();
            float3 runtimeOffset = -ToFloat3(shiftOffset);
            DroneFleetOriginShiftJob job = new DroneFleetOriginShiftJob
            {
                DroneStates = s_DroneStates,
                DroneStateBackBuffer = s_DroneStateBackBuffer,
                RenderMatrices = s_DroneRenderMatrices,
                RenderMatrixBackBuffer = s_DroneRenderMatrixBackBuffer,
                DronePositions = s_DronePositionsSoA,
                RuntimeOffset = runtimeOffset
            };
            JobHandle handle = job.Schedule(HeadlessDroneCapacity, DroneJobBatchSize);
            DispatcherJobSwap.TryComplete(ref handle, true);

            if (s_DronePositions != null)
            {
                Vector3 managedOffset = new Vector3(runtimeOffset.x, runtimeOffset.y, runtimeOffset.z);
                for (int slot = 0; slot < s_DronePositions.Length; slot++)
                    s_DronePositions[slot] += managedOffset;

                for (int launchIndex = 0; launchIndex < s_PendingLaunchCount && launchIndex < s_PendingLaunches.Length; launchIndex++)
                {
                    PendingDroneLaunch launch = s_PendingLaunches[launchIndex];
                    if (!launch.Active)
                        continue;

                    launch.HomePosition += managedOffset;
                    DroneFleetTask task = launch.Task;
                    task = new DroneFleetTask(task.Kind, task.Module, task.Position + managedOffset, task.Radius);
                    launch.Task = task;
                    s_PendingLaunches[launchIndex] = launch;
                }
            }

            UpdateDrawBounds();
        }

        private static void ApplyPendingControls()
        {
            if (s_DroneSlotDroneIds == null)
                return;

            for (int slot = 0; slot < s_DroneSlotDroneIds.Length; slot++)
            {
                if (s_PendingReleaseBySlot[slot])
                {
                    ClearHeadlessSlot(slot, true);
                    s_PendingReleaseBySlot[slot] = false;
                    s_PendingAbortBySlot[slot] = false;
                    s_PendingHostileBySlot[slot] = false;
                    continue;
                }

                int droneId = s_DroneSlotDroneIds[slot];
                if (droneId <= 0)
                {
                    s_PendingAbortBySlot[slot] = false;
                    s_PendingHostileBySlot[slot] = false;
                    s_PendingResupplyGrantBySlot[slot] = false;
                    s_PendingResupplyFailureBySlot[slot] = false;
                    continue;
                }

                HeadlessDroneState drone = s_DroneStates[slot];
                if (s_PendingResupplyGrantBySlot[slot] &&
                    drone.State == (byte)HeadlessDroneRuntimeState.ResupplyCommitPending)
                {
                    GrantDroneResupply(ref drone, 1);
                    s_PendingResupplyGrantBySlot[slot] = false;
                }

                if (s_PendingResupplyFailureBySlot[slot] &&
                    drone.State == (byte)HeadlessDroneRuntimeState.ResupplyCommitPending)
                {
                    drone.SolderUnits = 0;
                    drone.TransactionProgress = 0f;
                    ReturnDroneToHub(ref drone);
                    s_PendingResupplyFailureBySlot[slot] = false;
                }

                if (drone.State != (byte)HeadlessDroneRuntimeState.ResupplyCommitPending)
                {
                    s_PendingResupplyGrantBySlot[slot] = false;
                    s_PendingResupplyFailureBySlot[slot] = false;
                }

                if (s_PendingHostileBySlot[slot])
                {
                    s_LogicLeechHijackCount++;
                    drone.FactionBit = (byte)HeadlessDroneFactionBit.Hostile;
                    if (TryResolvePlayerPosition(out Vector3 playerPosition))
                    {
                        drone.TargetPosition = ToFloat3(playerPosition);
                        drone.TargetAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(playerPosition);
                        drone.State = (byte)HeadlessDroneRuntimeState.Travel;
                    }
                }

                if (s_PendingAbortBySlot[slot] && drone.State != (byte)HeadlessDroneRuntimeState.Empty)
                {
                    drone.TargetTaskIndex = EmptyTaskIndex;
                    drone.TargetPosition = drone.HomePosition;
                    drone.TargetAup = drone.HomeAup;
                    drone.State = (byte)HeadlessDroneRuntimeState.Return;
                }

                s_PendingAbortBySlot[slot] = false;
                s_PendingHostileBySlot[slot] = false;
                s_DroneStates[slot] = drone;
            }
        }

        private static void ApplyDockingRequestSignals()
        {
            if (!s_DroneStates.IsCreated || s_DroneSlotDroneIds == null)
                return;

            System.ReadOnlySpan<DockingRequestSignal> requests = SignalBus<DockingRequestSignal>.GetFrameSnapshot();
            for (int i = 0; i < requests.Length; i++)
            {
                DockingRequestSignal request = requests[i];
                int slot = ResolveHeadlessSlot(request.DroneId);
                if (slot < 0)
                {
                    PublishDockingFailedForMissingDrone(in request);
                    continue;
                }

                HeadlessDroneState drone = s_DroneStates[slot];
                if (drone.State == (byte)HeadlessDroneRuntimeState.Empty ||
                    drone.State == (byte)HeadlessDroneRuntimeState.Sacrificed ||
                    drone.State == (byte)HeadlessDroneRuntimeState.Completed)
                {
                    PublishDockingFailed(slot, in drone, ToVector3(drone.Position), request.RequestId, DockingFailureReason.InvalidRequest);
                    continue;
                }

                if (request.HubGridId != 0 && request.HubGridId != drone.HubGridId)
                {
                    PublishDockingFailed(slot, in drone, ToVector3(drone.Position), request.RequestId, DockingFailureReason.InvalidRequest);
                    continue;
                }

                AbsoluteUniversePosition dockAup = request.DockAup.ToAup();
                float3 dockRuntime = dockAup.ToRuntimeFloat3();
                if (!IsFiniteFloat3(dockRuntime))
                {
                    PublishDockingFailed(slot, in drone, ToVector3(drone.Position), request.RequestId, DockingFailureReason.InvalidRequest);
                    continue;
                }

                float3 dockForward = NormalizeOrFallback(request.DockForward, ResolveForward(drone.HomeRotation));
                drone.HomePosition = dockRuntime;
                drone.HomeAup = dockAup.ToAbsoluteDouble3();
                drone.HomeRotation = quaternion.LookRotationSafe(dockForward, math.up());
                drone.TargetTaskIndex = EmptyTaskIndex;
                drone.TargetModuleId = 0;
                drone.TargetPosition = dockRuntime;
                drone.TargetAup = drone.HomeAup;
                drone.DockingRequestId = request.RequestId;
                DroneCognitionJob.BeginDocking(ref drone);

                s_DroneStates[slot] = drone;
                if (s_DroneStateBackBuffer.IsCreated)
                    s_DroneStateBackBuffer[slot] = drone;

                if (s_DronePositions != null)
                    s_DronePositions[slot] = ToVector3(drone.Position);

                MirrorDroneSoA(slot, in drone);
            }
        }

        private static void ResolveDockingObstacleAborts()
        {
            if (!s_DroneStates.IsCreated ||
                s_DroneSlotDroneIds == null ||
                s_PendingReleaseBySlot == null ||
                !s_DockingRaycastCommands.IsCreated ||
                !s_DockingRaycastHits.IsCreated ||
                !s_DockingRaycastSlots.IsCreated)
            {
                return;
            }

            TryFinalizeDockingObstacleAborts();
            if (s_DockingRaycastScheduled)
                return;

            int commandCount = 0;
            int segmentCount = ResolveDockingObstacleSegmentCount();
            float invSegmentCount = math.rcp((float)segmentCount);
            for (int slot = 0; slot < HeadlessDroneCapacity && commandCount < DockingRaycastCapacity; slot++)
            {
                if (s_DroneSlotDroneIds[slot] <= 0 || s_PendingReleaseBySlot[slot])
                    continue;

                HeadlessDroneState drone = s_DroneStates[slot];
                if (drone.State != (byte)HeadlessDroneRuntimeState.Docking)
                    continue;

                float3 p0 = IsFiniteDouble3(drone.DockControlP0) ? ToFloat3(drone.DockControlP0) : drone.Position;
                float3 p1 = IsFiniteDouble3(drone.DockControlP1) ? ToFloat3(drone.DockControlP1) : p0;
                float3 p2 = IsFiniteDouble3(drone.DockControlP2) ? ToFloat3(drone.DockControlP2) : drone.HomePosition;
                float3 p3 = IsFiniteDouble3(drone.DockControlP3) ? ToFloat3(drone.DockControlP3) : drone.HomePosition;
                if (!IsFiniteFloat3(p0) || !IsFiniteFloat3(p1) || !IsFiniteFloat3(p2) || !IsFiniteFloat3(p3))
                    continue;

                float startT = math.saturate(drone.DockingElapsed);
                if (startT >= 1f)
                    continue;

                float3 segmentStart = IsFiniteFloat3(drone.Position)
                    ? drone.Position
                    : EvaluateDockingObstacleBezier(p0, p1, p2, p3, startT);
                for (int segment = 1; segment <= segmentCount && commandCount < DockingRaycastCapacity; segment++)
                {
                    float segmentT = startT + ((1f - startT) * (segment * invSegmentCount));
                    float3 segmentEnd = EvaluateDockingObstacleBezier(p0, p1, p2, p3, segmentT);
                    TryAppendDockingRaycastCommand(
                        slot,
                        segmentStart,
                        segmentEnd,
                        segment == segmentCount,
                        ref commandCount);
                    segmentStart = segmentEnd;
                }
            }

            if (commandCount <= 0)
                return;

            NativeArray<RaycastCommand> commandBatch = s_DockingRaycastCommands.GetSubArray(0, commandCount);
            NativeArray<RaycastHit> hitBatch = s_DockingRaycastHits.GetSubArray(0, commandCount);
            s_DockingRaycastHandle = RaycastCommand.ScheduleBatch(commandBatch, hitBatch, DockingRaycastMinCommandsPerJob, default);
            s_DockingRaycastCount = commandCount;
            s_DockingRaycastScheduled = true;
            H8Memory.RegisterActiveJob(SystemID.Construction, s_DockingRaycastHandle);
            JobHandle.ScheduleBatchedJobs();
        }

        private static void CompleteDockingObstacleAbortsForReset()
        {
            if (!s_DockingRaycastScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref s_DockingRaycastHandle, true))
                return;

            FinishDockingObstacleAborts();
        }

        private static bool TryFinalizeDockingObstacleAborts()
        {
            if (!s_DockingRaycastScheduled)
                return true;

            if (!DispatcherJobSwap.TryFinalizeCompleted(ref s_DockingRaycastHandle))
                return false;

            FinishDockingObstacleAborts();
            return true;
        }

        private static void FinishDockingObstacleAborts()
        {
            int commandCount = math.min(s_DockingRaycastCount, DockingRaycastCapacity);
            s_DockingRaycastScheduled = false;
            s_DockingRaycastCount = 0;

            for (int i = 0; i < commandCount; i++)
            {
                RaycastHit hit = s_DockingRaycastHits[i];
                Collider collider = hit.collider;
                if (collider == null)
                    continue;

                int slot = s_DockingRaycastSlots[i];
                if (slot < 0 ||
                    slot >= HeadlessDroneCapacity ||
                    s_DroneSlotDroneIds[slot] <= 0 ||
                    IsDockingAllowedHit(slot, collider))
                {
                    continue;
                }

                HeadlessDroneState drone = s_DroneStates[slot];
                if (drone.State != (byte)HeadlessDroneRuntimeState.Docking)
                    continue;

                AbortDockingForObstacle(slot, ref drone, hit.point);
            }
        }

        private static void TryAppendDockingRaycastCommand(
            int slot,
            float3 segmentStart,
            float3 segmentEnd,
            bool isLastSegment,
            ref int commandCount)
        {
            float3 delta = segmentEnd - segmentStart;
            float lengthSq = math.lengthsq(delta);
            if (!IsFiniteFloat3(delta) ||
                !math.isfinite(lengthSq) ||
                lengthSq <= DockingMinimumProbeDistanceMeters * DockingMinimumProbeDistanceMeters)
            {
                return;
            }

            float lengthInv = math.rsqrt(lengthSq);
            float length = lengthSq * lengthInv;
            float endTrim = isLastSegment ? DockingObstacleProbeEndpointTrimMeters : 0f;
            float probeDistance = length - endTrim;
            if (!math.isfinite(probeDistance) || probeDistance <= DockingMinimumProbeDistanceMeters)
                return;

            Vector3 direction = ToVector3(delta * lengthInv);
            Vector3 probeOrigin = ToVector3(segmentStart);
            s_DockingRaycastCommands[commandCount] = CreateDockingRaycastCommand(probeOrigin, direction, probeDistance);
            s_DockingRaycastHits[commandCount] = default;
            s_DockingRaycastSlots[commandCount] = slot;
            commandCount++;
        }

        private static float3 EvaluateDockingObstacleBezier(float3 p0, float3 p1, float3 p2, float3 p3, float t)
        {
            float clampedT = math.saturate(t);
            float oneMinusT = 1f - clampedT;
            float oneMinusT2 = oneMinusT * oneMinusT;
            float t2 = clampedT * clampedT;
            return
                (oneMinusT2 * oneMinusT * p0) +
                (3f * oneMinusT2 * clampedT * p1) +
                (3f * oneMinusT * t2 * p2) +
                (t2 * clampedT * p3);
        }

        private static int ResolveDockingObstacleSegmentCount()
        {
            float quality = ResolveGlobalQualityWeight();
            return Mathf.Clamp(1 + Mathf.RoundToInt(quality * (DockingObstacleProbeMaxSegments - 1)), 1, DockingObstacleProbeMaxSegments);
        }

        private static RaycastCommand CreateDockingRaycastCommand(Vector3 origin, Vector3 direction, float distance)
        {
            return new RaycastCommand
            {
                from = origin,
                direction = direction,
                distance = distance,
                queryParameters = new QueryParameters
                {
                    layerMask = ~0,
                    hitTriggers = QueryTriggerInteraction.Ignore,
                    hitBackfaces = false,
                    hitMultipleFaces = false
                }
            };
        }

        private static bool IsDockingAllowedHit(int slot, Collider collider)
        {
            Transform hitTransform = collider != null ? collider.transform : null;
            if (hitTransform == null || s_DroneHubs == null)
                return false;

            RepairDroneHub hub = s_DroneHubs[slot];
            if (hub == null)
                return false;

            Transform hubTransform = hub.transform;
            return ReferenceEquals(hitTransform, hubTransform) ||
                   hitTransform.IsChildOf(hubTransform) ||
                   hubTransform.IsChildOf(hitTransform);
        }

        private static void AbortDockingForObstacle(int slot, ref HeadlessDroneState drone, Vector3 hitPoint)
        {
            drone.TargetTaskIndex = EmptyTaskIndex;
            drone.TargetModuleId = 0;
            drone.TargetPosition = ResolveOrphanWanderTarget(slot, drone.Position);
            drone.TargetAup = drone.PositionAup + ToDouble3(drone.TargetPosition - drone.Position);
            drone.DockingElapsed = 0f;
            drone.DockingFlags = 0;
            drone.DockingPathLengthMeters = 0f;
            drone.Velocity = float3.zero;
            drone.State = (byte)HeadlessDroneRuntimeState.Wander;
            s_DroneStates[slot] = drone;

            if (s_DroneStateBackBuffer.IsCreated)
                s_DroneStateBackBuffer[slot] = drone;

            if (s_DronePositions != null)
                s_DronePositions[slot] = ToVector3(drone.Position);

            IncrementDockingAbortTelemetry();
            MirrorDroneSoA(slot, in drone);
            PublishDockingFailed(slot, in drone, hitPoint, DockingFailureReason.ObstacleBlocked);
        }

        private static void IncrementDockingAbortTelemetry()
        {
            s_DockingAbortCount++;
            if (s_FleetTelemetryAccumulator.IsCreated &&
                s_FleetTelemetryAccumulator.Length > (int)DroneFleetTelemetryAccumulatorSlot.DockingAborts)
            {
                s_FleetTelemetryAccumulator[(int)DroneFleetTelemetryAccumulatorSlot.DockingAborts]++;
            }
        }

        private static void PublishDockingHatchOpen(int slot)
        {
            if (s_DroneHubs == null || slot < 0 || slot >= s_DroneHubs.Length)
                return;

            BaseAirlock airlock = s_DroneHubs[slot] != null ? s_DroneHubs[slot].DockingAirlock : null;
            if (airlock != null)
                BaseAirlockEvents.RaiseCycleStarted(airlock, null);
        }

        private static void PublishPendingDockingHatchOpen(int slot, ref HeadlessDroneState drone)
        {
            if ((drone.DockingFlags & DroneCognitionJob.DockingFlagHatchOpenQueued) == 0 ||
                (drone.DockingFlags & DroneCognitionJob.DockingFlagHatchOpenPublished) != 0)
            {
                return;
            }

            PublishDockingHatchOpen(slot);
            drone.DockingFlags |= DroneCognitionJob.DockingFlagHatchOpenPublished;
        }

        private static void PublishDockingComplete(in HeadlessDroneState drone)
        {
            Vector3 dockRuntime = IsFiniteFloat3(drone.HomePosition)
                ? ToVector3(drone.HomePosition)
                : ToVector3(drone.Position);
            AbsoluteUniversePosition dockAup = AbsoluteUniversePosition.FromRuntimePosition(dockRuntime);
            float3 dockForward = ResolveForward(drone.HomeRotation);

            DockingCompleteSignal signal = new DockingCompleteSignal
            {
                DroneId = drone.DroneId,
                HubGridId = drone.HubGridId,
                DockAup = AbsoluteUniversePositionBlit.FromAup(in dockAup),
                DockForward = dockForward,
                RequestId = drone.DockingRequestId,
                Flags = drone.DockingFlags,
                Reserved0 = 0,
                Reserved1 = 0,
                Reserved2 = 0,
                ReservedTail = 0u
            };
            SignalBus<DockingCompleteSignal>.Push(in signal);
        }

        private static void PublishDockingFailed(int slot, in HeadlessDroneState drone, Vector3 hitPoint, DockingFailureReason reason)
        {
            PublishDockingFailed(slot, in drone, hitPoint, drone.DockingRequestId, reason);
        }

        private static void PublishDockingFailed(int slot, in HeadlessDroneState drone, Vector3 hitPoint, uint requestId, DockingFailureReason reason)
        {
            AbsoluteUniversePosition lastAup = AbsoluteUniversePosition.FromRuntimePosition(ToVector3(drone.Position));
            Vector3 failureVector = hitPoint - ToVector3(drone.Position);
            float3 finiteFailureVector = IsFiniteVector(failureVector)
                ? ToFloat3(failureVector)
                : float3.zero;
            DockingFailedSignal signal = new DockingFailedSignal
            {
                DroneId = drone.DroneId,
                HubGridId = drone.HubGridId,
                LastAup = AbsoluteUniversePositionBlit.FromAup(in lastAup),
                FailureVector = finiteFailureVector,
                RequestId = requestId,
                Reason = (byte)reason,
                Flags = 0,
                Reserved0 = 0,
                Reserved1 = 0,
                ReservedTail = 0u
            };
            SignalBus<DockingFailedSignal>.Push(in signal);
        }

        private static void PublishDockingFailedForMissingDrone(in DockingRequestSignal request)
        {
            DockingFailedSignal signal = new DockingFailedSignal
            {
                DroneId = request.DroneId,
                HubGridId = request.HubGridId,
                LastAup = request.DockAup,
                FailureVector = float3.zero,
                RequestId = request.RequestId,
                Reason = (byte)DockingFailureReason.InvalidRequest,
                Flags = 0,
                Reserved0 = 0,
                Reserved1 = 0,
                ReservedTail = 0u
            };
            SignalBus<DockingFailedSignal>.Push(in signal);
        }

        private static void ApplyCompletedHeadlessServices()
        {
            for (int slot = 0; slot < HeadlessDroneCapacity; slot++)
            {
                int droneId = s_DroneSlotDroneIds[slot];
                if (droneId <= 0)
                    continue;

                HeadlessDroneState drone = s_DroneStates[slot];
                if (drone.State == (byte)HeadlessDroneRuntimeState.Empty)
                    continue;

                s_DronePositions[slot] = ToVector3(drone.Position);
                SyncManagedTaskReference(slot, ref drone);

                if (drone.State == (byte)HeadlessDroneRuntimeState.Completed)
                {
                    s_DroneTasksCompletedCount++;
                    PublishPendingDockingHatchOpen(slot, ref drone);
                    PublishDockingComplete(in drone);
                    ClearHeadlessSlot(slot, true);
                    continue;
                }

                if (drone.State == (byte)HeadlessDroneRuntimeState.Sacrificed)
                {
                    ClearHeadlessSlot(slot, true);
                    continue;
                }

                if (TryResolveHubOrphan(slot, ref drone))
                {
                    s_DroneStates[slot] = drone;
                    continue;
                }

                if (drone.State == (byte)HeadlessDroneRuntimeState.ResupplyDocked)
                {
                    ApplyHeadlessResupply(slot, ref drone);
                    s_DroneStates[slot] = drone;
                    continue;
                }

                if (drone.State == (byte)HeadlessDroneRuntimeState.Stasis)
                {
                    TryQueueStasisWakeRequest(slot, ref drone);
                    s_DroneStates[slot] = drone;
                    continue;
                }

                if (drone.State == (byte)HeadlessDroneRuntimeState.Repair ||
                    drone.State == (byte)HeadlessDroneRuntimeState.Attack)
                {
                    if (TryBeginHijackRebootIfSourceGone(slot, ref drone))
                    {
                        s_DroneStates[slot] = drone;
                        continue;
                    }

                    s_DroneStates[slot] = drone;
                }
            }
        }

        private static void DrainDroneServiceCommandQueue()
        {
            if (!s_DroneServiceCommands.IsCreated ||
                !s_DroneServiceCommandCursor.IsCreated ||
                s_DroneServiceCommandCursor.Length <= 0)
            {
                return;
            }

            int commandCount = Mathf.Clamp(s_DroneServiceCommandCursor[0].Count, 0, Mathf.Min(DroneServiceCommandCapacity, s_DroneServiceCommands.Length));
            for (int commandIndex = 0; commandIndex < commandCount; commandIndex++)
            {
                DroneServiceCommand command = s_DroneServiceCommands[commandIndex];
                int slot = command.Slot;
                if (slot < 0 || slot >= HeadlessDroneCapacity || s_DroneSlotDroneIds == null)
                    continue;

                if (command.DroneId <= 0 || s_DroneSlotDroneIds[slot] != command.DroneId)
                    continue;

                HeadlessDroneState drone = s_DroneStates[slot];
                if (command.Kind == (byte)DroneServiceCommandKind.DockingHatchOpen)
                {
                    PublishPendingDockingHatchOpen(slot, ref drone);
                    s_DroneStates[slot] = drone;

                    if (s_DroneStateBackBuffer.IsCreated)
                        s_DroneStateBackBuffer[slot] = drone;

                    continue;
                }

                if (drone.DroneId != command.DroneId ||
                    (drone.State != (byte)HeadlessDroneRuntimeState.Repair &&
                     drone.State != (byte)HeadlessDroneRuntimeState.Attack))
                {
                    continue;
                }

                if (TryBeginHijackRebootIfSourceGone(slot, ref drone))
                {
                    s_DroneStates[slot] = drone;
                    MirrorDroneSoA(slot, in drone);
                    continue;
                }

                float serviceDt = Mathf.Max(0f, command.DeltaTime);
                if (command.Kind == (byte)DroneServiceCommandKind.Attack ||
                    drone.FactionBit == (byte)HeadlessDroneFactionBit.Hostile)
                {
                    ApplyHostileHijackService(slot, ref drone, serviceDt);
                }
                else if (s_DroneTaskKindsBySlot[slot] == DroneFleetTaskKind.MineNode)
                {
                    ApplyMockMiningService(slot, ref drone, serviceDt);
                }
                else if (s_DroneTaskKindsBySlot[slot] == DroneFleetTaskKind.CutParasite)
                {
                    ApplyParasiteAttackService(slot, ref drone, serviceDt);
                }
                else
                {
                    ApplyFriendlyRepairService(slot, ref drone, serviceDt);
                }

                s_DroneStates[slot] = drone;
                MirrorDroneSoA(slot, in drone);
            }

            s_DroneServiceCommandCursor[0] = default;
        }

        private static void ApplyHeadlessResupply(int slot, ref HeadlessDroneState drone)
        {
            RepairDroneHub hub = s_DroneHubs[slot];
            if (hub == null || !hub.TryQueueDroneResupplyCommit(1, drone.DroneId, out bool committedImmediately))
            {
                drone.State = (byte)HeadlessDroneRuntimeState.Stasis;
                drone.Velocity = float3.zero;
                drone.TransactionProgress = 0f;
                return;
            }

            if (committedImmediately)
            {
                GrantDroneResupply(ref drone, 1);
                return;
            }

            drone.State = (byte)HeadlessDroneRuntimeState.ResupplyCommitPending;
            drone.Velocity = float3.zero;
            drone.TransactionProgress = 0.5f;
        }

        private static void GrantDroneResupply(ref HeadlessDroneState drone, int grantedUnits)
        {
            int units = Mathf.Max(1, grantedUnits);
            drone.SolderUnits += units;
            drone.LoadedSolderCapacity = Mathf.Max(drone.LoadedSolderCapacity, drone.SolderUnits);
            drone.TransactionProgress = 1f;
            drone.State = drone.TargetTaskIndex >= 0
                ? (byte)HeadlessDroneRuntimeState.Travel
                : (byte)HeadlessDroneRuntimeState.Idle;
            drone.Velocity = float3.zero;
            DroneFleetInventoryTransactionSignal signal = new DroneFleetInventoryTransactionSignal
            {
                DroneId = drone.DroneId,
                SourceId = drone.HubGridId,
                DestinationId = drone.DroneId,
                ItemHash = (int)DroneRepairSparksSignalHash,
                Quantity = units,
                Position = drone.Position,
                Flags = 1u,
                Reserved0 = 0u
            };
            SignalBus<DroneFleetInventoryTransactionSignal>.Push(in signal);
        }

        private static void TryQueueStasisWakeRequest(int slot, ref HeadlessDroneState drone)
        {
            RepairDroneHub hub = s_DroneHubs[slot];
            if (hub == null)
                return;

            if (!hub.TryResolveNearestSupplyEndpoint(ToVector3(drone.Position), out Vector3 endpointPosition))
                return;

            drone.SupplyPosition = ToFloat3(endpointPosition);
            drone.SupplyAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(endpointPosition);
            drone.State = (byte)HeadlessDroneRuntimeState.ResupplyTravel;
            drone.Velocity = float3.zero;
        }

        private static bool TryBeginHijackRebootIfSourceGone(int slot, ref HeadlessDroneState drone)
        {
            if (drone.FactionBit != (byte)HeadlessDroneFactionBit.Hostile)
                return false;

            if (s_DroneTaskKindsBySlot[slot] != DroneFleetTaskKind.CutParasite)
                return false;

            BaseModule target = s_TargetModulesByDroneSlot[slot];
            if (target != null && target.ParasiteInfectionLevel > 0.0001f)
                return false;

            drone.FactionBit = (byte)HeadlessDroneFactionBit.Friendly;
            drone.TargetTaskIndex = EmptyTaskIndex;
            drone.RebootElapsed = 0f;
            drone.Velocity = float3.zero;
            drone.State = (byte)HeadlessDroneRuntimeState.Reboot;
            return true;
        }

        private static bool TryResolveHubOrphan(int slot, ref HeadlessDroneState drone)
        {
            RepairDroneHub currentHub = s_DroneHubs[slot];
            if (currentHub != null && currentHub.isActiveAndEnabled)
                return false;

            drone.HubGridId = InvalidHubId;
            s_DroneHubs[slot] = null;
            if (TryAttachToAlternateHub(slot, ref drone))
                return true;

            drone.TargetTaskIndex = EmptyTaskIndex;
            drone.TargetPosition = ResolveOrphanWanderTarget(slot, drone.Position);
            drone.TargetAup = drone.PositionAup + ToDouble3(drone.TargetPosition - drone.Position);
            drone.State = (byte)HeadlessDroneRuntimeState.Wander;
            drone.Velocity = float3.zero;
            return true;
        }

        private static bool TryAttachToAlternateHub(int slot, ref HeadlessDroneState drone)
        {
            int hubCount = RepairDroneHub.ActiveHubCount;
            BaseModule target = s_TargetModulesByDroneSlot[slot];
            RepairDroneHub bestHub = null;
            float bestDistanceSq = float.MaxValue;
            Vector3 dronePosition = ToVector3(drone.Position);

            int scanHubCount = Mathf.Min(hubCount, MaxMainThreadHubScanCount);
            for (int i = 0; i < scanHubCount; i++)
            {
                RepairDroneHub candidate = RepairDroneHub.GetActiveHubAt(i);
                if (candidate == null || !candidate.isActiveAndEnabled || !candidate.HasOperationalPower)
                    continue;

                if (target != null && IsDifferentGrid(candidate.CurrentGrid, target))
                    continue;

                Vector3 candidateDock = candidate.DockPosition;
                float distanceSq = (candidateDock - dronePosition).sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                bestHub = candidate;
            }

            if (bestHub == null || !bestHub.TryAttachOrphanedDrone(drone.DroneId))
                return false;

            s_DroneHubs[slot] = bestHub;
            drone.HubGridId = ResolveHubTaskKey(bestHub);
            drone.HomePosition = ToFloat3(bestHub.DockPosition);
            drone.HomeAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(bestHub.DockPosition);
            drone.HomeRotation = ToQuaternion(bestHub.DockRotation);
            drone.TargetTaskIndex = EmptyTaskIndex;
            drone.TargetPosition = drone.HomePosition;
            drone.TargetAup = drone.HomeAup;
            drone.State = (byte)HeadlessDroneRuntimeState.Return;
            drone.Velocity = float3.zero;
            return true;
        }

        private static float3 ResolveOrphanWanderTarget(int slot, float3 position)
        {
            float angle = (slot * 2.3999631f) + 0.7853982f;
            return position + new float3(
                CinematicMath.FastCos(angle) * OrphanWanderDistanceMeters,
                0f,
                CinematicMath.FastSin(angle) * OrphanWanderDistanceMeters);
        }

        private static void ApplyFriendlyRepairService(int slot, ref HeadlessDroneState drone, float dt)
        {
            BaseModule target = s_TargetModulesByDroneSlot[slot];
            if (target == null)
            {
                ReturnDroneToHub(ref drone);
                return;
            }

            if (s_FleetSacrificeRequested && IsSacrificeEligible(target))
            {
                ExecuteSacrifice(slot, ref drone, target);
                return;
            }

            if (drone.SolderUnits <= 0)
            {
                RouteDroneToSupplyOrStasis(slot, ref drone);
                return;
            }

            float recoverableIntegrity = Mathf.Max(1f, target.MaxRecoverableIntegrity);
            if (target.CurrentIntegrity >= recoverableIntegrity && !target.IsFlooded && !target.HasCascadeFailure)
            {
                ReturnDroneToHub(ref drone);
                return;
            }

            float repairAmount = Mathf.Max(0f, drone.RepairRatePerSecond * dt);
            if (repairAmount <= 0f)
                return;

            DroneFleetMockRepairSignal repairSignal = new DroneFleetMockRepairSignal
            {
                DroneId = drone.DroneId,
                TargetModuleId = GetRuntimeId(target),
                RepairUnits = repairAmount,
                Position = drone.Position,
                Flags = 0u,
                Reserved0 = 0u
            };
            SignalBus<DroneFleetMockRepairSignal>.Push(in repairSignal);
            PublishHullRepairedByDrone(slot, in drone, target, repairAmount);
            DispatchRepairWeld(slot, in drone, target);
            ConsumeSolderByWork(ref drone, repairAmount, SolderIntegrityUnitsPerPack);
        }

        private static void ApplyMockMiningService(int slot, ref HeadlessDroneState drone, float dt)
        {
            DroneFleetTuningConstants tuning = ResolveDroneTuning();
            DroneChassisSpecDTO chassis = ResolveLaunchDroneChassisSpec(s_DroneTaskKindsBySlot[slot], in tuning);
            float holdSeconds = Mathf.Max(0.01f, chassis.MiningHoldSeconds);
            drone.RepairAccumulator = Mathf.Min(holdSeconds, drone.RepairAccumulator + Mathf.Max(0f, dt));
            drone.TransactionProgress = Mathf.Clamp01(drone.RepairAccumulator / holdSeconds);
            if (drone.RepairAccumulator < holdSeconds)
                return;

            int sourceId = drone.TargetModuleId != 0
                ? drone.TargetModuleId
                : unchecked((int)math.hash(drone.TargetPosition));
            DroneFleetInventoryTransactionSignal signal = new DroneFleetInventoryTransactionSignal
            {
                DroneId = drone.DroneId,
                SourceId = sourceId,
                DestinationId = drone.HubGridId,
                ItemHash = DroneInventoryCopperHash,
                Quantity = 1,
                Position = drone.Position,
                Flags = 2u,
                Reserved0 = 0u
            };
            SignalBus<DroneFleetInventoryTransactionSignal>.Push(in signal);
            SignalBus<InventoryCommandSignal>.Push(new InventoryCommandSignal
            {
                InventoryHash = (uint)Mathf.Max(0, drone.HubGridId),
                Frame = (uint)Mathf.Max(0, Time.frameCount),
                Sequence = (uint)Mathf.Max(0, drone.DroneId),
                Command = InventoryCommandSignalCommands.Sort,
                Flags = 2
            });
            drone.RepairAccumulator = 0f;
            drone.TransactionProgress = 1f;
            ReturnDroneToHub(ref drone);
        }

        private static void ApplyParasiteAttackService(int slot, ref HeadlessDroneState drone, float dt)
        {
            BaseModule target = s_TargetModulesByDroneSlot[slot];
            if (target == null || target.ParasiteInfectionLevel <= 0.0001f)
            {
                ReturnDroneToHub(ref drone);
                return;
            }

            if (drone.SolderUnits <= 0)
            {
                RouteDroneToSupplyOrStasis(slot, ref drone);
                return;
            }

            FloraInteractionManager floraInteractionManager = FloraInteractionManager.ActiveRuntimeInstance;
            if (floraInteractionManager == null)
            {
                ReturnDroneToHub(ref drone);
                return;
            }

            Vector3 hitPoint = ToVector3(drone.TargetPosition);
            Vector3 dronePosition = ToVector3(drone.Position);
            Vector3 direction = hitPoint - dronePosition;
            float directionDistanceSq = direction.sqrMagnitude;
            float deliveredDamage = Mathf.Max(0.1f, drone.RepairRatePerSecond * dt);
            floraInteractionManager.TryApplyDroneParasiteCut(
                hitPoint,
                directionDistanceSq > SeparationDistanceEpsilon ? direction * math.rsqrt(directionDistanceSq) : Vector3.down,
                deliveredDamage,
                drone.WeldPowerNormalized);

            ConsumeSolderByWork(ref drone, deliveredDamage, SolderIntegrityUnitsPerPack);
        }

        private static void ApplyHostileHijackService(int slot, ref HeadlessDroneState drone, float dt)
        {
            BaseModule target = s_TargetModulesByDroneSlot[slot];
            if (target == null)
            {
                if (TryResolvePlayerPosition(out Vector3 playerPosition))
                {
                    drone.TargetPosition = ToFloat3(playerPosition);
                    drone.TargetAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(playerPosition);
                }
                return;
            }

            float damage = Mathf.Max(0.1f, drone.RepairRatePerSecond * dt);
            target.ApplyDamage(damage);
            DispatchPlasmaCut(slot, in drone, target);
            drone.State = (byte)HeadlessDroneRuntimeState.Attack;
        }

        private static void PublishHullRepairedByDrone(int slot, in HeadlessDroneState drone, BaseModule target, float repairUnits)
        {
            if (target == null || repairUnits <= 0f)
                return;

            AbsoluteUniversePosition hitAup = IsFiniteDouble3(drone.TargetAup)
                ? AbsoluteUniversePosition.FromAbsolutePosition(drone.TargetAup)
                : AbsoluteUniversePosition.FromRuntimePosition(target.transform.position);
            HullRepairedSignal signal = new HullRepairedSignal
            {
                HitAup = hitAup,
                RoomId = 0,
                SourceHash = ComputeDroneTaskHash(s_DroneTaskKindsBySlot[slot], drone.DroneId, GetRuntimeId(target)),
                Frame = (uint)Mathf.Max(0, Time.frameCount),
                DentIndex = 0,
                DentsRepairedCount = 1,
                QualityTier = (byte)GlobalRegistry.ScalabilityTier,
                Flags = HullRepairedSignal.CompletedFlag
            };
            SignalBus<HullRepairedSignal>.Push(in signal);
        }

        private static void ExecuteSacrifice(int slot, ref HeadlessDroneState drone, BaseModule target)
        {
            float recoverableIntegrity = Mathf.Max(1f, target.MaxRecoverableIntegrity);
            float requestedRepair = Mathf.Max(0f, recoverableIntegrity - target.CurrentIntegrity);
            if (requestedRepair > 0f || target.IsFlooded)
                PublishHullRepairedByDrone(slot, in drone, target, Mathf.Max(1f, requestedRepair));

            s_FleetSacrificeRequested = false;
            s_DroneSlotDestroyed[slot] = true;
            s_DestroyedDroneCount++;
            drone.State = (byte)HeadlessDroneRuntimeState.Sacrificed;
            drone.Velocity = float3.zero;
        }

        private static bool IsSacrificeEligible(BaseModule module)
        {
            if (module == null)
                return false;

            return module.IsBreached || module.FloodLevel01 >= 0.8f || (module.IsFlooded && module.FloodLevel01 <= 0.001f);
        }

        private static void ConsumeSolderByWork(ref HeadlessDroneState drone, float workAmount, float unitsPerSolder)
        {
            if (workAmount <= 0f || drone.SolderUnits <= 0)
                return;

            drone.RepairAccumulator += workAmount;
            float safeUnitsPerSolder = Mathf.Max(1f, unitsPerSolder);
            float safeUnitsPerSolderInv = math.rcp(safeUnitsPerSolder);
            int consumedUnits = Mathf.Min(
                drone.SolderUnits,
                Mathf.FloorToInt(drone.RepairAccumulator * safeUnitsPerSolderInv));
            if (consumedUnits <= 0)
                return;

            drone.RepairAccumulator -= safeUnitsPerSolder * consumedUnits;
            drone.SolderUnits -= consumedUnits;
        }

        private static void RouteDroneToSupplyOrStasis(int slot, ref HeadlessDroneState drone)
        {
            RepairDroneHub hub = s_DroneHubs[slot];
            if (hub != null && hub.TryResolveNearestSupplyEndpoint(ToVector3(drone.Position), out Vector3 endpointPosition))
            {
                drone.SupplyPosition = ToFloat3(endpointPosition);
                drone.SupplyAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(endpointPosition);
                drone.State = (byte)HeadlessDroneRuntimeState.ResupplyTravel;
                return;
            }

            drone.State = (byte)HeadlessDroneRuntimeState.Stasis;
            drone.Velocity = float3.zero;
        }

        private static void ReturnDroneToHub(ref HeadlessDroneState drone)
        {
            drone.TargetTaskIndex = EmptyTaskIndex;
            drone.TargetPosition = drone.HomePosition;
            drone.TargetAup = drone.HomeAup;
            drone.DockingElapsed = 0f;
            drone.DockingFlags = 0;
            drone.DockingPathLengthMeters = 0f;
            drone.State = (byte)HeadlessDroneRuntimeState.Return;
        }

        private static void DispatchRepairWeld(int slot, in HeadlessDroneState drone, BaseModule target)
        {
            HectonVoxelVolume volume = s_TargetVoxelVolumesByDroneSlot[slot];
            if (volume == null || target == null)
                return;

            Vector3 dronePosition = ToVector3(drone.Position);
            Vector3 targetPosition = target.transform.position;
            Vector3 weldDirection = targetPosition - dronePosition;
            float weldDistanceSq = weldDirection.sqrMagnitude;
            if (weldDistanceSq <= SeparationDistanceEpsilon)
                return;

            Vector3 normalizedWeldDirection = weldDirection * math.rsqrt(weldDistanceSq);
            double3 absoluteHitPoint = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(dronePosition + (normalizedWeldDirection * 0.35f));
            volume.ApplyRepairWeldDda(
                absoluteHitPoint,
                normalizedWeldDirection,
                drone.WeldPowerNormalized,
                drone.WeldRangeMeters);
            PublishDroneRepairSparks(absoluteHitPoint, drone.DroneId, drone.WeldPowerNormalized);
        }

        private static void DispatchPlasmaCut(int slot, in HeadlessDroneState drone, BaseModule target)
        {
            HectonVoxelVolume volume = s_TargetVoxelVolumesByDroneSlot[slot];
            if (volume == null || target == null)
                return;

            Vector3 dronePosition = ToVector3(drone.Position);
            Vector3 targetPosition = target.transform.position;
            Vector3 cutDirection = targetPosition - dronePosition;
            float cutDistanceSq = cutDirection.sqrMagnitude;
            if (cutDistanceSq <= SeparationDistanceEpsilon)
                return;

            Vector3 normalizedCutDirection = cutDirection * math.rsqrt(cutDistanceSq);
            double3 absoluteHitPoint = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(dronePosition + (normalizedCutDirection * 0.35f));
            volume.ApplyPlasmaCutDda(
                absoluteHitPoint,
                normalizedCutDirection,
                drone.WeldPowerNormalized,
                drone.WeldRangeMeters);
        }

        private static void PublishDroneRepairSparks(double3 absoluteHitPoint, int droneId, float intensity01)
        {
            DebrisSpawnSignal signal = new DebrisSpawnSignal
            {
                PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(absoluteHitPoint),
                SpeciesHash = DroneRepairSparksSignalHash,
                SourceEntityId = (uint)Mathf.Max(0, droneId),
                Intensity01 = Mathf.Clamp01(intensity01),
                DebrisKind = DroneRepairSparkDebrisKind,
                Flags = 0
            };
            SignalBus<DebrisSpawnSignal>.Push(in signal);

        }

        private static void ApplyPendingLaunches()
        {
            if (s_PendingLaunchCount <= 0)
                return;

            DroneFleetTuningConstants tuning = ResolveDroneTuning();
            for (int i = 0; i < s_PendingLaunchCount; i++)
            {
                PendingDroneLaunch launch = s_PendingLaunches[i];
                if (!launch.Active)
                    continue;

                int slot = launch.DroneSlot;
                if (slot < 0 ||
                    slot >= HeadlessDroneCapacity ||
                    s_DroneSlotDroneIds[slot] != launch.DroneId ||
                    s_PendingReleaseBySlot[slot] ||
                    s_DroneSlotDestroyed[slot])
                {
                    ClearHeadlessSlot(slot, true);
                    continue;
                }

                BaseModule target = launch.Task.Module;
                HectonVoxelVolume targetVolume = TryResolveTargetVoxelVolume(target);
                s_DroneHubs[slot] = launch.Hub;
                s_TargetModulesByDroneSlot[slot] = target;
                s_TargetVoxelVolumesByDroneSlot[slot] = targetVolume;
                s_DroneTaskKindsBySlot[slot] = launch.Task.Kind;
                s_DronePositions[slot] = launch.HomePosition;
                quaternion homeRotation = ToQuaternion(launch.HomeRotation);
                double3 homeAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(launch.HomePosition);
                double3 targetAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(launch.Task.Position);
                uint launchTaskHash = ComputeDroneTaskHash(launch.Task.Kind, launch.DroneId, GetRuntimeId(target));
                DroneChassisSpecDTO chassis = ResolveLaunchDroneChassisSpec(launch.Task.Kind, in tuning);
                int tunedCargoCapacity = Mathf.Max(1, Mathf.RoundToInt(chassis.CargoCapacity));

                HeadlessDroneState state = new HeadlessDroneState
                {
                    DroneId = launch.DroneId,
                    HubGridId = ResolveHubTaskKey(launch.Hub),
                    HubSlot = slot,
                    TargetTaskIndex = launch.DroneId,
                    TargetModuleId = GetRuntimeId(target),
                    SolderUnits = Mathf.Max(0, launch.LoadedSolderUnits),
                    LoadedSolderCapacity = Mathf.Max(tunedCargoCapacity, launch.LoadedSolderUnits),
                    State = (byte)HeadlessDroneRuntimeState.Travel,
                    FactionBit = (byte)HeadlessDroneFactionBit.Friendly,
                    CorridorTight = ResolveCorridorFlag(launch.HomePosition),
                    BatteryPercent = chassis.BatteryCapacity,
                    RepairAccumulator = 0f,
                    DockingElapsed = 0f,
                    RebootElapsed = 0f,
                    AvoidanceHysteresisSeconds = 0f,
                    TransactionProgress = 0f,
                    ServiceRadius = Mathf.Max(HeadlessServiceRadiusMeters, launch.Task.Radius),
                    MaxSpeed = chassis.MaxSpeed,
                    BatteryDrainPerSecond = chassis.BatteryDrainRate,
                    RepairRatePerSecond = Mathf.Max(0.01f, launch.RepairRatePerSecond * chassis.RepairSpeed),
                    WeldPowerNormalized = HeadlessWeldPowerNormalized,
                    WeldRangeMeters = HeadlessWeldRangeMeters,
                    Position = ToFloat3(launch.HomePosition),
                    Velocity = float3.zero,
                    HomePosition = ToFloat3(launch.HomePosition),
                    TargetPosition = ToFloat3(launch.Task.Position),
                    SupplyPosition = ToFloat3(launch.HomePosition),
                    DockStartPosition = ToFloat3(launch.HomePosition),
                    Rotation = homeRotation,
                    HomeRotation = homeRotation,
                    DockStartRotation = homeRotation,
                    DockingPathLengthMeters = 0f,
                    DockingRequestId = 0u,
                    DockingFlags = 0,
                    DockControlP0 = ToDouble3(launch.HomePosition),
                    DockControlP1 = ToDouble3(launch.HomePosition),
                    DockControlP2 = ToDouble3(launch.HomePosition),
                    DockControlP3 = ToDouble3(launch.HomePosition),
                    PositionAup = homeAup,
                    HomeAup = homeAup,
                    TargetAup = targetAup,
                    SupplyAup = homeAup
                };
                s_DroneStates[slot] = state;
                s_DroneStateBackBuffer[slot] = state;
                if (s_DroneStateDtos.IsCreated)
                {
                    s_DroneStateDtos[slot] = new DroneStateDTO
                    {
                        AUP_Position = homeAup,
                        Velocity = float3.zero,
                        CurrentTaskHash = launchTaskHash,
                        BatteryLevel = chassis.BatteryCapacity,
                        Flags = ((uint)state.State) | ((uint)state.FactionBit << 8) | ((uint)state.CorridorTight << 16)
                    };
                }

                if (s_DroneTargetDtos.IsCreated)
                {
                    s_DroneTargetDtos[slot] = new DroneTargetDTO
                    {
                        TargetAUP = targetAup,
                        LocalPosition = state.TargetPosition,
                        TaskHash = launchTaskHash,
                        TaskIndex = state.TargetTaskIndex,
                        TargetModuleId = state.TargetModuleId,
                        Radius = state.ServiceRadius,
                        TaskKind = (uint)launch.Task.Kind,
                        Flags = 1u,
                        Reserved0 = 0u
                    };
                }

                s_DroneRenderMatrices[slot] = float4x4.TRS(state.Position, state.Rotation, new float3(1f, 1f, 1f));
                s_DroneRenderMatrixBackBuffer[slot] = s_DroneRenderMatrices[slot];
                MirrorDroneSoA(slot, in state);
                s_PendingLaunches[i] = default;
            }

            s_PendingLaunchCount = 0;
        }

        private static void SyncManagedTaskReference(int slot, ref HeadlessDroneState drone)
        {
            int taskIndex = drone.TargetTaskIndex;
            if (taskIndex < 0 || taskIndex >= s_HeadlessTaskCount || taskIndex >= s_TaskModuleRefs.Length)
                return;

            s_DroneTaskKindsBySlot[slot] = s_TaskKinds[taskIndex];
            BaseModule module = s_TaskModuleRefs[taskIndex];
            if (module == null)
            {
                s_TargetModulesByDroneSlot[slot] = null;
                s_TargetVoxelVolumesByDroneSlot[slot] = null;
                return;
            }

            s_TargetModulesByDroneSlot[slot] = module;
            s_TargetVoxelVolumesByDroneSlot[slot] = s_TaskVoxelVolumeRefs[taskIndex];
            drone.TargetModuleId = GetRuntimeId(module);
        }

        private static void ClearAllHeadlessSlots()
        {
            for (int slot = 0; slot < HeadlessDroneCapacity; slot++)
                ClearHeadlessSlot(slot, false);
        }

        private static void ClearHeadlessSlot(int slot, bool notifyHub)
        {
            if (slot < 0 || slot >= HeadlessDroneCapacity || s_DroneSlotDroneIds == null)
                return;

            int droneId = s_DroneSlotDroneIds[slot];
            RepairDroneHub hub = s_DroneHubs[slot];
            s_DroneSlotDroneIds[slot] = 0;
            s_DroneHubs[slot] = null;
            s_TargetModulesByDroneSlot[slot] = null;
            s_TargetVoxelVolumesByDroneSlot[slot] = null;
            s_DroneTaskKindsBySlot[slot] = DroneFleetTaskKind.None;
            s_DronePositions[slot] = Vector3.zero;
            HeadlessDroneState clearedState = default;
            s_DroneStates[slot] = clearedState;
            MirrorDroneSoA(slot, in clearedState);
            if (s_DroneStateBackBuffer.IsCreated)
                s_DroneStateBackBuffer[slot] = default;
            s_DroneRenderMatrices[slot] = float4x4.zero;
            if (s_DroneRenderMatrixBackBuffer.IsCreated)
                s_DroneRenderMatrixBackBuffer[slot] = float4x4.zero;
            if (s_DroneStateDtos.IsCreated)
                s_DroneStateDtos[slot] = default;
            if (s_DroneTargetDtos.IsCreated)
                s_DroneTargetDtos[slot] = default;
            s_PendingAbortBySlot[slot] = false;
            s_PendingReleaseBySlot[slot] = false;
            s_PendingHostileBySlot[slot] = false;
            s_PendingResupplyGrantBySlot[slot] = false;
            s_PendingResupplyFailureBySlot[slot] = false;

            if (notifyHub && hub != null && droneId > 0)
                hub.NotifyHeadlessDroneReturned(droneId);
        }

        private static int FindFreeHeadlessSlot()
        {
            for (int i = 0; i < MaxOperationalDroneCount; i++)
            {
                if (s_DroneSlotDestroyed[i])
                    continue;

                if (s_DroneSlotDroneIds[i] <= 0)
                    return i;
            }

            return -1;
        }

        private static void MirrorDroneSoA(int slot, in HeadlessDroneState drone)
        {
            if (slot < 0 || slot >= HeadlessDroneCapacity)
                return;

            if (s_DronePositionsSoA.IsCreated)
                s_DronePositionsSoA[slot] = drone.Position;

            if (s_DroneStateBytes.IsCreated)
            {
                s_DroneStateBytes[slot] = s_DroneTaskKindsBySlot != null &&
                    s_DroneTaskKindsBySlot[slot] == DroneFleetTaskKind.MineNode &&
                    drone.State == (byte)HeadlessDroneRuntimeState.Repair
                    ? (byte)DroneFleetSoaState.Mining
                    : ResolveDroneSoAState(in drone);
            }
        }

        private static byte ResolveDroneSoAState(in HeadlessDroneState drone)
        {
            if (drone.State == (byte)HeadlessDroneRuntimeState.Repair ||
                drone.State == (byte)HeadlessDroneRuntimeState.Attack)
            {
                return (byte)DroneFleetSoaState.Repairing;
            }

            if (drone.State == (byte)HeadlessDroneRuntimeState.Return ||
                drone.State == (byte)HeadlessDroneRuntimeState.Docking ||
                drone.State == (byte)HeadlessDroneRuntimeState.ResupplyTravel ||
                drone.State == (byte)HeadlessDroneRuntimeState.ResupplyDocked ||
                drone.State == (byte)HeadlessDroneRuntimeState.ResupplyCommitPending)
            {
                return (byte)DroneFleetSoaState.Returning;
            }

            return (byte)DroneFleetSoaState.Idle;
        }

        private static int ResolveHeadlessSlot(int droneId)
        {
            if (droneId <= 0 || s_DroneSlotDroneIds == null)
                return -1;

            for (int i = 0; i < s_DroneSlotDroneIds.Length; i++)
            {
                if (s_DroneSlotDroneIds[i] == droneId)
                    return i;
            }

            return -1;
        }

        private static int CountManagedHeadlessDrones()
        {
            if (s_DroneSlotDroneIds == null)
                return 0;

            int count = 0;
            for (int i = 0; i < s_DroneSlotDroneIds.Length; i++)
            {
                if (s_DroneSlotDroneIds[i] > 0 && !s_PendingReleaseBySlot[i])
                    count++;
            }

            return count;
        }

        private static void RefreshHeadlessCounters()
        {
            s_HeadlessStasisSlotCount = 0;
            if (s_DroneSlotDroneIds == null)
                return;

            for (int i = 0; i < s_DroneSlotDroneIds.Length; i++)
            {
                if (s_DroneSlotDroneIds[i] <= 0)
                    continue;

                HeadlessDroneState drone = s_DroneStates[i];
                if (drone.State == (byte)HeadlessDroneRuntimeState.Stasis)
                    s_HeadlessStasisSlotCount++;

                s_DronePositions[i] = ToVector3(drone.Position);
            }
        }

        private static void BuildHeadlessTaskMap(float deltaTime)
        {
            s_HeadlessTaskRebuildTimer -= Mathf.Max(0f, deltaTime);
            if (s_HeadlessTaskRebuildTimer > 0f && s_HeadlessTaskCount > 0)
                return;

            s_HeadlessTaskRebuildTimer = ResolveDroneTaskRebuildIntervalSeconds();
            s_HeadlessTaskCount = 0;
            ClearManagedTaskRefs();

            ConstructionManager manager = GlobalRegistry.ConstructionRuntime;
            int moduleCount = manager != null ? manager.SpawnedBaseModuleCount : 0;
            if (moduleCount == 0)
                return;

            int hubCount = Mathf.Min(RepairDroneHub.ActiveHubCount, MaxMainThreadHubScanCount);
            FloraInteractionManager floraInteractionManager = FloraInteractionManager.ActiveRuntimeInstance;
            System.ReadOnlySpan<DroneFleetMockMiningSignal> miningSignals = SignalBus<DroneFleetMockMiningSignal>.GetFrameSnapshot();
            int remainingModuleScans = MaxMainThreadTaskScanCount;
            for (int hubIndex = 0; hubIndex < hubCount; hubIndex++)
            {
                RepairDroneHub hub = RepairDroneHub.GetActiveHubAt(hubIndex);
                if (hub == null || !hub.isActiveAndEnabled)
                    continue;

                int hubKey = ResolveHubTaskKey(hub);
                PowerGrid hubGrid = hub.CurrentGrid;
                Vector3 hubPosition = hub.DockPosition;
                for (int moduleIndex = 0; moduleIndex < moduleCount && remainingModuleScans > 0 && s_HeadlessTaskCount < HeadlessTaskCapacity; moduleIndex++, remainingModuleScans--)
                {
                    BaseModule module = manager.GetSpawnedBaseModuleAt(moduleIndex);
                    if (module == null || !module.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    if (IsEligibleRepairTarget(hubGrid, module, 0.98f))
                    {
                        AppendHeadlessTask(
                            hubKey,
                            DroneFleetTaskKind.RepairModule,
                            module,
                            module.transform.position,
                            0f,
                            ResolveCriticalityWeight(module));
                    }

                    if (floraInteractionManager == null ||
                        module.ParasiteInfectionLevel <= 0.0001f ||
                        IsDifferentGrid(hubGrid, module) ||
                        !floraInteractionManager.TryResolveNearestModuleParasite(module, hubPosition, out FloraInteractionManager.ModuleParasiteTarget parasiteTarget))
                    {
                        continue;
                    }

                    AppendHeadlessTask(
                        hubKey,
                        DroneFleetTaskKind.CutParasite,
                        module,
                        parasiteTarget.Position,
                        parasiteTarget.Radius,
                        ResolveParasiteCriticalityWeight(module, in parasiteTarget));
                }

                if (remainingModuleScans <= 0)
                    break;

                AppendMockMiningTasksForHub(hubIndex, hubCount, hubKey, miningSignals);
            }
        }

        private static void AppendMockMiningTasksForHub(
            int hubIndex,
            int hubCount,
            int hubKey,
            System.ReadOnlySpan<DroneFleetMockMiningSignal> miningSignals)
        {
            if (miningSignals.Length <= 0)
                return;

            for (int i = 0; i < miningSignals.Length && s_HeadlessTaskCount < HeadlessTaskCapacity; i++)
            {
                DroneFleetMockMiningSignal signal = miningSignals[i];
                if (!IsFiniteFloat3(signal.Position) ||
                    ResolveNearestHubIndex(signal.Position, hubCount) != hubIndex)
                {
                    continue;
                }

                AppendHeadlessMockMiningTask(hubKey, in signal);
            }
        }

        private static int ResolveNearestHubIndex(float3 position, int hubCount)
        {
            int bestIndex = -1;
            float bestDistanceSq = float.MaxValue;
            Vector3 targetPosition = ToVector3(position);
            for (int i = 0; i < hubCount; i++)
            {
                RepairDroneHub hub = RepairDroneHub.GetActiveHubAt(i);
                if (hub == null || !hub.isActiveAndEnabled)
                    continue;

                float distanceSq = (hub.DockPosition - targetPosition).sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                bestIndex = i;
            }

            return bestIndex;
        }

        private static void AppendHeadlessTask(
            int hubKey,
            DroneFleetTaskKind kind,
            BaseModule module,
            Vector3 position,
            float radius,
            float criticalityWeight)
        {
            int taskIndex = s_HeadlessTaskCount;
            if (taskIndex < 0 || taskIndex >= HeadlessTaskCapacity || module == null)
                return;

            s_TaskModuleRefs[taskIndex] = module;
            s_TaskVoxelVolumeRefs[taskIndex] = TryResolveTargetVoxelVolume(module);
            s_TaskKinds[taskIndex] = kind;
            if (s_DroneAssignmentTasks.IsCreated && taskIndex < s_DroneAssignmentTasks.Length)
            {
                s_DroneAssignmentTasks[taskIndex] = new DroneTaskDTO
                {
                    TargetAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(position),
                    LocalPosition = ToFloat3(position),
                    Priority = 1f,
                    Score = 0f,
                    CriticalityWeight = Mathf.Max(0.1f, criticalityWeight),
                    Radius = Mathf.Max(HeadlessServiceRadiusMeters, radius),
                    ModuleIndex = taskIndex,
                    TaskKind = (int)kind,
                    Reserved0 = (uint)Mathf.Max(0, hubKey)
                };
            }
            s_HeadlessTaskCount++;
        }

        private static void AppendHeadlessMockMiningTask(int hubKey, in DroneFleetMockMiningSignal signal)
        {
            int taskIndex = s_HeadlessTaskCount;
            if (taskIndex < 0 || taskIndex >= HeadlessTaskCapacity)
                return;

            s_TaskModuleRefs[taskIndex] = null;
            s_TaskVoxelVolumeRefs[taskIndex] = null;
            s_TaskKinds[taskIndex] = DroneFleetTaskKind.MineNode;
            if (s_DroneAssignmentTasks.IsCreated && taskIndex < s_DroneAssignmentTasks.Length)
            {
                Vector3 targetPosition = ToVector3(signal.Position);
                s_DroneAssignmentTasks[taskIndex] = new DroneTaskDTO
                {
                    TargetAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(targetPosition),
                    LocalPosition = signal.Position,
                    Priority = 0.25f,
                    Score = 0f,
                    CriticalityWeight = 0.1f,
                    Radius = HeadlessServiceRadiusMeters,
                    ModuleIndex = taskIndex,
                    TaskKind = (int)DroneFleetTaskKind.MineNode,
                    Reserved0 = (uint)Mathf.Max(0, hubKey)
                };
            }
            s_HeadlessTaskCount++;
        }

        private static void BuildHeadlessSpatialHash()
        {
            if (!s_DroneSpatialBucketHeads.IsCreated ||
                !s_DroneSpatialNextIndices.IsCreated ||
                !s_DroneSpatialKeys.IsCreated)
            {
                return;
            }

            for (int i = 0; i < s_DroneSpatialBucketHeads.Length; i++)
                s_DroneSpatialBucketHeads[i] = -1;

            for (int i = 0; i < HeadlessDroneCapacity; i++)
            {
                if (s_DroneSlotDroneIds[i] <= 0)
                    continue;

                HeadlessDroneState drone = s_DroneStates[i];
                if (drone.State == (byte)HeadlessDroneRuntimeState.Empty ||
                    drone.State == (byte)HeadlessDroneRuntimeState.Sacrificed ||
                    drone.State == (byte)HeadlessDroneRuntimeState.Completed)
                {
                    continue;
                }

                drone.CorridorTight = ResolveCorridorFlag(ToVector3(drone.Position));
                s_DroneStates[i] = drone;
                int key = DroneCognitionJob.PackSpatialKey(drone.Position);
                int bucket = ResolveDroneSpatialBucket(key);
                s_DroneSpatialKeys[i] = key;
                s_DroneSpatialNextIndices[i] = s_DroneSpatialBucketHeads[bucket];
                s_DroneSpatialBucketHeads[bucket] = i;
            }
        }

        private static int ResolveDroneSpatialBucket(int key)
        {
            uint hash = (uint)key;
            hash ^= hash >> 16;
            hash *= 0x7feb352du;
            hash ^= hash >> 15;
            return (int)(hash & (uint)(DroneSpatialBucketCapacity - 1));
        }

        private static void ClearHeadlessTaskClaims()
        {
            for (int i = 0; i < s_HeadlessTaskClaimOwners.Length; i++)
                s_HeadlessTaskClaimOwners[i] = 0;

            for (int slot = 0; slot < HeadlessDroneCapacity; slot++)
            {
                if (s_DroneSlotDroneIds[slot] <= 0)
                    continue;

                HeadlessDroneState drone = s_DroneStates[slot];
                int taskIndex = drone.TargetTaskIndex;
                if (taskIndex < 0 || taskIndex >= s_HeadlessTaskCount || taskIndex >= s_HeadlessTaskClaimOwners.Length)
                    continue;

                if (s_HeadlessTaskClaimOwners[taskIndex] == 0)
                    s_HeadlessTaskClaimOwners[taskIndex] = drone.DroneId;
            }
        }

        private static void ClearFleetTelemetryAccumulator()
        {
            if (!s_FleetTelemetryAccumulator.IsCreated)
                return;

            for (int i = 0; i < s_FleetTelemetryAccumulator.Length; i++)
                s_FleetTelemetryAccumulator[i] = 0;

            if (s_FleetTelemetryAccumulator.Length > (int)DroneFleetTelemetryAccumulatorSlot.LostToHijack)
                s_FleetTelemetryAccumulator[(int)DroneFleetTelemetryAccumulatorSlot.LostToHijack] = s_LogicLeechHijackCount;
        }

        private static void PublishFleetTelemetryIfDue()
        {
            if (!s_FleetTelemetryAccumulator.IsCreated)
                return;

            s_FleetTelemetryFrameCounter++;
            if (s_FleetTelemetryFrameCounter < FleetTelemetryPublishFrameInterval)
                return;

            s_FleetTelemetryFrameCounter = 0;
            int activeCount = s_FleetTelemetryAccumulator[(int)DroneFleetTelemetryAccumulatorSlot.ActiveCount];
            int batteryMilliPercent = s_FleetTelemetryAccumulator[(int)DroneFleetTelemetryAccumulatorSlot.BatteryMilliPercent];
            float averageBattery = activeCount > 0
                ? Mathf.Clamp(batteryMilliPercent * math.rcp(activeCount * 1000f), 0f, 100f)
                : 0f;
            FleetStatusSnapshot snapshot = new FleetStatusSnapshot(
                activeCount,
                averageBattery,
                s_FleetTelemetryAccumulator[(int)DroneFleetTelemetryAccumulatorSlot.SolderReserve],
                s_DestroyedDroneCount,
                s_FleetTelemetryAccumulator[(int)DroneFleetTelemetryAccumulatorSlot.HostileCount]);

            s_LastFleetStatusSnapshot = snapshot;
            GlobalTelemetryBus.PublishDroneFleetStatus(
                snapshot.TotalActive,
                snapshot.AverageBattery,
                snapshot.SolderReserve,
                snapshot.LostUnits,
                snapshot.HostileUnits);
            PublishDominantAxisDroneTelemetryIfPresent();
            TryRelayLeviathanPing();
            PublishSnapshot();
        }

        private static void ClearManagedTaskRefs()
        {
            for (int i = 0; i < s_TaskModuleRefs.Length; i++)
            {
                s_TaskModuleRefs[i] = null;
                s_TaskVoxelVolumeRefs[i] = null;
                s_TaskKinds[i] = DroneFleetTaskKind.None;
                if (s_DroneAssignmentTasks.IsCreated && i < s_DroneAssignmentTasks.Length)
                    s_DroneAssignmentTasks[i] = default;
            }
        }

        private static int ResolveHubTaskKey(RepairDroneHub hub)
        {
            return GetRuntimeId(hub);
        }

        private static bool TryResolvePlayerPosition(out Vector3 position)
        {
            position = Vector3.zero;
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext == null)
                return false;

            if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                math.all(math.isfinite(snapshot.RuntimePosition)))
            {
                position = new Vector3(snapshot.RuntimePosition.x, snapshot.RuntimePosition.y, snapshot.RuntimePosition.z);
                return true;
            }

            var playerMovement = playerContext.PlayerMovement;
            if (playerMovement == null)
                return false;

            float3 runtime = playerMovement.CurrentAup.ToRuntimeFloat3();
            if (!math.all(math.isfinite(runtime)))
                return false;

            position = new Vector3(runtime.x, runtime.y, runtime.z);
            return true;
        }

        private static bool TryResolveFormationAnchor(out Vector3 position)
        {
            position = Vector3.zero;
            ISubmarineRuntimeContext submarine = GlobalRegistry.Submarine;
            Transform platformTransform = submarine != null ? submarine.PlatformTransform : null;
            if (platformTransform == null)
                return false;

            position = platformTransform.position;
            return true;
        }

        private static void TryRelayLeviathanPing()
        {
            if (s_DroneSlotDroneIds == null || !s_DroneStates.IsCreated || !TryResolveFormationAnchor(out Vector3 submarinePosition))
                return;

            float relayDistanceSq = DroneRelaySubmarineDistanceMeters * DroneRelaySubmarineDistanceMeters;
            for (int slot = 0; slot < s_DroneSlotDroneIds.Length; slot++)
            {
                if (s_DroneSlotDroneIds[slot] <= 0)
                    continue;

                HeadlessDroneState drone = s_DroneStates[slot];
                if (drone.State == (byte)HeadlessDroneRuntimeState.Empty ||
                    drone.State == (byte)HeadlessDroneRuntimeState.Sacrificed ||
                    drone.State == (byte)HeadlessDroneRuntimeState.Completed ||
                    drone.FactionBit == (byte)HeadlessDroneFactionBit.Hostile)
                {
                    continue;
                }

                Vector3 dronePosition = ToVector3(drone.Position);
                if ((dronePosition - submarinePosition).sqrMagnitude <= relayDistanceSq)
                    continue;

                if (!TryResolveRelayLeviathan(dronePosition, out SpatialQueryHit hit))
                    continue;

                AcousticPingEvent pingEvent = new AcousticPingEvent(
                    hit.Position,
                    DroneRelayPingRadiusMeters,
                    1f,
                    DroneRelayPingLifetimeSeconds,
                    FieldTargetRole.BioformAggressive,
                    hit.SpeciesId,
                    DroneRelayPingRadiusMeters * 48f);
                PhysicsEventBus.NotifyAcousticPing(in pingEvent);
                return;
            }
        }

        private static void PublishDominantAxisDroneTelemetryIfPresent()
        {
            if (s_DroneSlotDroneIds == null || !s_DroneStates.IsCreated || !TryResolveFormationAnchor(out Vector3 anchorPosition))
                return;

            float3 anchor = ToFloat3(anchorPosition);
            float quality = ResolveGlobalQualityWeight();
            float precisionWeight = quality * quality * (3f - (2f * quality));
            for (int slot = 0; slot < s_DroneSlotDroneIds.Length; slot++)
            {
                int droneId = s_DroneSlotDroneIds[slot];
                if (droneId <= 0)
                    continue;

                HeadlessDroneState drone = s_DroneStates[slot];
                if (drone.State == (byte)HeadlessDroneRuntimeState.Empty ||
                    drone.State == (byte)HeadlessDroneRuntimeState.Sacrificed ||
                    drone.State == (byte)HeadlessDroneRuntimeState.Completed)
                {
                    continue;
                }

                float exactDistanceSq = math.distancesq(drone.Position, anchor);
                float dominantAxisSq = DominantAxisMagnitudeSq(drone.Position - anchor);
                float distanceMetricSq = math.lerp(dominantAxisSq, exactDistanceSq, precisionWeight);
                GlobalTelemetryBus.PublishDominantAxisTelemetry(
                    unchecked((uint)droneId),
                    distanceMetricSq,
                    precisionWeight < 0.999f);
            }
        }

        private static float DominantAxisMagnitudeSq(float3 value)
        {
            if (!math.all(math.isfinite(value)))
                return 0f;

            float3 absValue = math.abs(value);
            float dominantMagnitude = math.cmax(absValue);
            return dominantMagnitude * dominantMagnitude;
        }

        private static bool TryResolveRelayLeviathan(Vector3 dronePosition, out SpatialQueryHit hit)
        {
            hit = default;
            int count = WorldSpatialHashGrid.CollectContactsNonAlloc(
                dronePosition,
                DroneRelayScanRadiusMeters,
                SpatialTargetKind.Bioform,
                s_DroneRelayContacts);

            float bestDistanceSq = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                SpatialQueryHit candidate = s_DroneRelayContacts[i];
                if (!(candidate.Owner is FaunaBrain brain) ||
                    brain.SpeciesProfile == null ||
                    !brain.SpeciesProfile.isLeviathan)
                {
                    continue;
                }

                Vector3 targetPosition = candidate.Position;
                Vector3 delta = targetPosition - dronePosition;
                float distanceSq = delta.sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                hit = candidate;
            }

            return bestDistanceSq < float.MaxValue;
        }

        private static bool TryResolveAbyssalFlowVolumePayload(
            out NativeArray<float3> flowVolume,
            out Vector3 center,
            out int resolutionXZ,
            out int resolutionY,
            out int ringOffsetX,
            out int ringOffsetY,
            out int ringOffsetZ,
            out float horizontalCellSize,
            out float verticalCellSize,
            out float surfaceY,
            out float depthMeters)
        {
            HectonMapMagicVegetationBridge bridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (bridge != null &&
                bridge.TryGetAbyssalFlowVolumePayload(
                    out flowVolume,
                    out center,
                    out resolutionXZ,
                    out resolutionY,
                    out ringOffsetX,
                    out ringOffsetY,
                    out ringOffsetZ,
                    out horizontalCellSize,
                    out verticalCellSize,
                    out surfaceY,
                    out depthMeters))
            {
                return true;
            }

            flowVolume = default;
            center = Vector3.zero;
            resolutionXZ = 0;
            resolutionY = 0;
            ringOffsetX = 0;
            ringOffsetY = 0;
            ringOffsetZ = 0;
            horizontalCellSize = 0f;
            verticalCellSize = 0f;
            surfaceY = 0f;
            depthMeters = 0f;
            return false;
        }

        private static void ResolveFluidCurrentSnapshot(
            out Vector3 baseFlowVelocity,
            out bool phantomFlowEnabled,
            out float phantomFlowNoiseScale,
            out float phantomFlowTimeScale,
            out float phantomFlowStrength,
            out float phantomFlowVerticalFactor)
        {
            HectonFluidEngine fluidEngine = GlobalRegistry.Fluid;
            if (fluidEngine == null)
            {
                baseFlowVelocity = Vector3.zero;
                phantomFlowEnabled = false;
                phantomFlowNoiseScale = 0f;
                phantomFlowTimeScale = 0f;
                phantomFlowStrength = 0f;
                phantomFlowVerticalFactor = 0f;
                return;
            }

            baseFlowVelocity = fluidEngine.CurrentVector * Mathf.Max(0f, fluidEngine.CurrentStrength);
            phantomFlowEnabled = fluidEngine.EnablePhantomCurrent;
            phantomFlowNoiseScale = Mathf.Max(0f, fluidEngine.CurrentNoiseScale);
            phantomFlowTimeScale = Mathf.Max(0f, fluidEngine.CurrentTimeScale);
            phantomFlowStrength = Mathf.Max(0f, fluidEngine.PhantomCurrentStrength);
            phantomFlowVerticalFactor = Mathf.Max(0f, fluidEngine.CurrentVerticalFactor);
        }

        private static byte ResolveCorridorFlag(Vector3 position)
        {
            return VoxelDynamicNavGridRuntime.TrySampleHybridNavigation(position, out VoxelDynamicNavGridRuntime.HybridNavigationSample sample) &&
                   sample.Mode == VoxelDynamicNavGridRuntime.HybridNavigationMode.CaveVoxel
                ? (byte)1
                : (byte)0;
        }

        private static HectonVoxelVolume TryResolveTargetVoxelVolume(BaseModule target)
        {
            return target != null ? target.CachedVoxelVolume : null;
        }

        private static void EnsureRenderBuffers()
        {
            EnsureDroneProceduralMaterial();
            if (s_DroneProceduralMaterial == null)
                return;

            if (s_DroneMatrixBuffer == null)
                s_DroneMatrixBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4x4>(HeadlessDroneCapacity); // COLD ALLOC: GraphicsBuffer[512] - real headless drone matrix upload buffer - owner: DroneFleetManager

            if (s_DroneMatrixBufferBackBuffer == null)
                s_DroneMatrixBufferBackBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4x4>(HeadlessDroneCapacity); // COLD ALLOC: GraphicsBuffer[512] - alternate real drone matrix upload buffer for GPU/CPU double-buffering - owner: DroneFleetManager

            if (s_DroneStateGpuBuffer == null)
                s_DroneStateGpuBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<DroneCullingStateGpu>(HeadlessDroneCapacity); // COLD ALLOC: GraphicsBuffer[512] - compact real drone culling upload buffer for GPU culling - owner: DroneFleetManager

            if (s_DroneRenderInstanceBuffer == null)
                s_DroneRenderInstanceBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<DroneRenderInstance>(HeadlessDroneCapacity); // COLD ALLOC: GraphicsBuffer[512] - real drone render instance upload buffer for VAT transaction parameters - owner: DroneFleetManager

            if (s_DroneVisibleMatrixBuffer == null)
                s_DroneVisibleMatrixBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, HeadlessDroneCapacity, UnsafeUtility.SizeOf<float4x4>()); // COLD ALLOC: GraphicsBuffer[512] - GPU-compacted visible real drone matrices - owner: DroneFleetManager

            if (s_DroneVisibleInstanceBuffer == null)
                s_DroneVisibleInstanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, HeadlessDroneCapacity, UnsafeUtility.SizeOf<DroneRenderInstance>()); // COLD ALLOC: GraphicsBuffer[512] - GPU-compacted visible real drone VAT instance data - owner: DroneFleetManager

            if (s_DroneVisibleIndexBuffer == null)
                s_DroneVisibleIndexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, HeadlessDroneCapacity, sizeof(int)); // COLD ALLOC: GraphicsBuffer[512] - visible real drone index append buffer for shader indirection/debug - owner: DroneFleetManager

            if (s_DroneProceduralArgsBuffer == null)
                s_DroneProceduralArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, UnsafeUtility.SizeOf<DroneProceduralIndirectArgsDTO>()); // COLD ALLOC: GraphicsBuffer[1] - headless drone procedural indirect draw arguments - owner: DroneFleetManager

            EnsureDroneDefaultColorBuffer();
            ResolveDroneCullingKernels();
        }

        private static void EnsureDroneProceduralMaterial()
        {
            if (s_DroneProceduralMaterial != null)
                return;

            Shader shader = Shader.Find(DroneProceduralShaderName);
            if (shader == null)
                return;

            s_DroneProceduralMaterial = new Material(shader)
            {
                name = "MAT_Runtime_DroneFleetProcedural",
                hideFlags = HideFlags.DontSave,
                enableInstancing = true
            }; // COLD ALLOC: Material[1] - generated drone procedural indirect material - owner: DroneFleetManager
            s_DroneProceduralMaterialRuntimeOwned = true;
        }

        private static void EnsureDroneDefaultColorBuffer()
        {
            if (s_DroneDefaultColorBuffer != null)
                return;

            s_DroneDefaultColorBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(1); // COLD ALLOC: GraphicsBuffer[1] - default white procedural color binding for real drones - owner: DroneFleetManager
            NativeArray<float4> mappedColor = s_DroneDefaultColorBuffer.LockBufferForWrite<float4>(0, 1);
            mappedColor[0] = new float4(1f, 1f, 1f, 1f);
            s_DroneDefaultColorBuffer.UnlockBufferAfterWrite<float4>(1);
        }

        private static void ReleaseRenderBuffers()
        {
            if (s_DroneMatrixBuffer != null)
            {
                s_DroneMatrixBuffer.Release();
                s_DroneMatrixBuffer = null;
            }

            if (s_DroneMatrixBufferBackBuffer != null)
            {
                s_DroneMatrixBufferBackBuffer.Release();
                s_DroneMatrixBufferBackBuffer = null;
            }

            if (s_DroneStateGpuBuffer != null)
            {
                s_DroneStateGpuBuffer.Release();
                s_DroneStateGpuBuffer = null;
            }

            if (s_DroneRenderInstanceBuffer != null)
            {
                s_DroneRenderInstanceBuffer.Release();
                s_DroneRenderInstanceBuffer = null;
            }

            if (s_DroneVisibleMatrixBuffer != null)
            {
                s_DroneVisibleMatrixBuffer.Release();
                s_DroneVisibleMatrixBuffer = null;
            }

            if (s_DroneVisibleInstanceBuffer != null)
            {
                s_DroneVisibleInstanceBuffer.Release();
                s_DroneVisibleInstanceBuffer = null;
            }

            if (s_DroneVisibleIndexBuffer != null)
            {
                s_DroneVisibleIndexBuffer.Release();
                s_DroneVisibleIndexBuffer = null;
            }

            if (s_DroneProceduralArgsBuffer != null)
            {
                s_DroneProceduralArgsBuffer.Release();
                s_DroneProceduralArgsBuffer = null;
            }

            if (s_DroneDefaultColorBuffer != null)
            {
                s_DroneDefaultColorBuffer.Release();
                s_DroneDefaultColorBuffer = null;
            }

            if (s_DroneProceduralMaterialRuntimeOwned && s_DroneProceduralMaterial != null)
                DestroyRuntimeObject(s_DroneProceduralMaterial);

            s_DroneProceduralMaterial = null;
            s_DroneProceduralMaterialRuntimeOwned = false;
        }

        private static bool EnsurePhantomRenderResources()
        {
            ResolvePhantomDroneKernel();
            if (s_PhantomDronesCompute == null || !s_PhantomDroneKernelResolved)
                return false;

            EnsureDroneProceduralMaterial();
            if (s_DroneProceduralMaterial == null)
                return false;

            if (s_PhantomDroneMatrixBuffer == null)
                s_PhantomDroneMatrixBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, PhantomDroneCount, UnsafeUtility.SizeOf<float4x4>()); // COLD ALLOC: GraphicsBuffer[500] - GPU-authored phantom drone matrices - owner: DroneFleetManager

            if (s_PhantomDroneColorBuffer == null)
                s_PhantomDroneColorBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, PhantomDroneCount, UnsafeUtility.SizeOf<float4>()); // COLD ALLOC: GraphicsBuffer[500] - GPU-authored phantom drone emissive colors - owner: DroneFleetManager

            if (s_PhantomDroneArgsBuffer == null)
            {
                s_PhantomDroneArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, UnsafeUtility.SizeOf<DroneProceduralIndirectArgsDTO>()); // COLD ALLOC: GraphicsBuffer[1] - phantom drone procedural indirect draw arguments - owner: DroneFleetManager
                s_PhantomDroneLastDrawCount = -1;
            }

            return true;
        }

        private static void ReleasePhantomRenderResources()
        {
            if (s_PhantomDroneMatrixBuffer != null)
            {
                s_PhantomDroneMatrixBuffer.Release();
                s_PhantomDroneMatrixBuffer = null;
            }

            if (s_PhantomDroneColorBuffer != null)
            {
                s_PhantomDroneColorBuffer.Release();
                s_PhantomDroneColorBuffer = null;
            }

            if (s_PhantomDroneArgsBuffer != null)
            {
                s_PhantomDroneArgsBuffer.Release();
                s_PhantomDroneArgsBuffer = null;
            }

            s_PhantomDroneLastDrawCount = -1;
        }

        private static int ResolvePhantomDroneDrawCount()
        {
            float quality = ResolveGlobalQualityWeight();
            return Mathf.Clamp(Mathf.RoundToInt(math.lerp(LowTierPhantomDroneCount, PhantomDroneCount, quality)), 0, PhantomDroneCount);
        }

        private static void UpdatePhantomDroneArgs()
        {
            if (s_PhantomDroneArgsBuffer == null)
                return;

            if (s_PhantomDroneLastDrawCount == PhantomDroneCount)
                return;

            NativeArray<DroneProceduralIndirectArgsDTO> mappedArgs = s_PhantomDroneArgsBuffer.LockBufferForWrite<DroneProceduralIndirectArgsDTO>(0, 1);
            mappedArgs[0] = new DroneProceduralIndirectArgsDTO
            {
                VertexCountPerInstance = DroneProceduralVerticesPerInstance,
                InstanceCount = (uint)PhantomDroneCount,
                StartVertex = 0u,
                StartInstance = 0u
            };
            s_PhantomDroneArgsBuffer.UnlockBufferAfterWrite<DroneProceduralIndirectArgsDTO>(1);
            s_PhantomDroneLastDrawCount = PhantomDroneCount;
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(target);
                return;
            }
#endif

            UnityEngine.Object.Destroy(target);
        }

        private static void ResolveDroneCullingKernels()
        {
            if (s_DroneCullingKernelsResolved)
                return;

            s_DroneCullKernel = -1;
            s_DroneClearArgsKernel = -1;

#if UNITY_EDITOR
            if (s_DroneCullingCompute == null)
                s_DroneCullingCompute = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(DroneCullingComputeAssetPath);
#endif

            if (s_DroneCullingCompute == null)
                return;

            if (s_DroneCullingCompute.HasKernel("CS_ClearArgs"))
                s_DroneClearArgsKernel = s_DroneCullingCompute.FindKernel("CS_ClearArgs");
            else if (s_DroneCullingCompute.HasKernel("ClearIndirectArgs"))
                s_DroneClearArgsKernel = s_DroneCullingCompute.FindKernel("ClearIndirectArgs");

            if (s_DroneCullingCompute.HasKernel("CS_CullDrones"))
                s_DroneCullKernel = s_DroneCullingCompute.FindKernel("CS_CullDrones");
            else if (s_DroneCullingCompute.HasKernel("CullDrones"))
                s_DroneCullKernel = s_DroneCullingCompute.FindKernel("CullDrones");

            s_DroneCullingKernelsResolved = s_DroneCullKernel >= 0 && s_DroneClearArgsKernel >= 0;
        }

        private static void ResolvePhantomDroneKernel()
        {
            if (s_PhantomDroneKernelResolved)
                return;

            s_PhantomDroneKernel = -1;

#if UNITY_EDITOR
            if (s_PhantomDronesCompute == null)
                s_PhantomDronesCompute = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(PhantomDronesComputeAssetPath);
#endif

            if (s_PhantomDronesCompute == null)
                return;

            if (s_PhantomDronesCompute.HasKernel("CS_UpdatePhantomDrones"))
                s_PhantomDroneKernel = s_PhantomDronesCompute.FindKernel("CS_UpdatePhantomDrones");
            else if (s_PhantomDronesCompute.HasKernel("UpdatePhantomDrones"))
                s_PhantomDroneKernel = s_PhantomDronesCompute.FindKernel("UpdatePhantomDrones");

            s_PhantomDroneKernelResolved = s_PhantomDroneKernel >= 0;
        }

        private static void RenderHeadlessFleet(float deltaTime)
        {
            RenderRealHeadlessFleet();
            RenderPhantomSwarm(deltaTime);
        }

        private static void RenderRealHeadlessFleet()
        {
            if (CountManagedHeadlessDrones() <= 0)
                return;

            EnsureRenderBuffers();
            GraphicsBuffer matrixBuffer = s_DroneMatrixUploadBufferIndex == 0
                ? s_DroneMatrixBuffer
                : s_DroneMatrixBufferBackBuffer;

            if (matrixBuffer == null ||
                s_DroneProceduralArgsBuffer == null ||
                s_DroneDefaultColorBuffer == null ||
                s_DroneProceduralMaterial == null ||
                !s_DroneRenderMatrices.IsCreated)
            {
                return;
            }

            GraphicsBufferUploadUtility.UploadNativeArray(matrixBuffer, s_DroneRenderMatrices, HeadlessDroneCapacity);
            PrepareDroneRenderInstances();
            if (s_DroneStateGpuBuffer != null && s_DroneCullingStates.IsCreated)
                GraphicsBufferUploadUtility.UploadNativeArray(s_DroneStateGpuBuffer, s_DroneCullingStates, HeadlessDroneCapacity);
            if (s_DroneRenderInstanceBuffer != null && s_DroneRenderInstances.IsCreated)
                GraphicsBufferUploadUtility.UploadNativeArray(s_DroneRenderInstanceBuffer, s_DroneRenderInstances, HeadlessDroneCapacity);

            if (s_DroneProceduralArgs.IsCreated && s_DroneProceduralArgs.Length > 0)
            {
                DroneProceduralIndirectArgsDTO args = s_DroneProceduralArgs[0];
                if (args.VertexCountPerInstance == 0u ||
                    args.InstanceCount == 0u)
                {
                    return;
                }
            }
            else
                return;

            GraphicsBufferUploadUtility.UploadNativeArray(s_DroneProceduralArgsBuffer, s_DroneProceduralArgs, 1);
            s_DroneProceduralMaterial.SetBuffer(DroneMatricesPropertyId, matrixBuffer);
            s_DroneProceduralMaterial.SetBuffer(InstanceMatricesPropertyId, matrixBuffer);
            s_DroneProceduralMaterial.SetBuffer(PhantomColorsPropertyId, s_DroneDefaultColorBuffer);
            if (s_DroneRenderInstanceBuffer != null)
                s_DroneProceduralMaterial.SetBuffer(DroneRenderInstancesPropertyId, s_DroneRenderInstanceBuffer);

            Vector3 origin = ResolveDroneRenderReferencePosition();
            s_DroneProceduralMaterial.SetVector(DroneProceduralCameraOriginPropertyId, new Vector4(origin.x, origin.y, origin.z, 0f));
            s_DroneProceduralMaterial.SetInt(UsePhantomColorsPropertyId, 0);

            UnityEngine.Graphics.DrawProceduralIndirect(
                s_DroneProceduralMaterial,
                s_DroneDrawBounds,
                MeshTopology.Triangles,
                s_DroneProceduralArgsBuffer,
                0,
                null,
                null,
                ShadowCastingMode.Off,
                false,
                s_DroneRenderLayer);
            s_DroneMatrixUploadBufferIndex ^= 1;
        }

        private static void RenderPhantomSwarm(float deltaTime)
        {
            int phantomDrawCount = ResolvePhantomDroneDrawCount();
            if (phantomDrawCount <= 0)
                return;

            if (!TryResolvePhantomAnchor(out Vector3 anchor) || !EnsurePhantomRenderResources())
                return;

            phantomDrawCount = Mathf.Min(phantomDrawCount, PhantomDroneCount);
            UpdatePhantomDroneArgs();
            s_PhantomDronePhaseSeconds += Mathf.Max(0f, deltaTime);
            if (s_PhantomDronePhaseSeconds >= PhantomDronePhaseWrapSeconds)
                s_PhantomDronePhaseSeconds = Mathf.Repeat(s_PhantomDronePhaseSeconds, PhantomDronePhaseWrapSeconds);

            s_PhantomDroneDrawBounds = new Bounds(
                anchor,
                new Vector3(
                    PhantomDroneBoundsDiameterMeters,
                    PhantomDroneBoundsDiameterMeters,
                    PhantomDroneBoundsDiameterMeters));

            s_PhantomDronesCompute.SetInt(PhantomCountPropertyId, phantomDrawCount);
            s_PhantomDronesCompute.SetVector(PhantomAnchorPropertyId, new Vector4(anchor.x, anchor.y, anchor.z, 0f));
            s_PhantomDronesCompute.SetFloat(PhantomTimePropertyId, s_PhantomDronePhaseSeconds);
            s_PhantomDronesCompute.SetFloat(PhantomBaseRadiusPropertyId, PhantomDroneOrbitRadiusMeters);
            s_PhantomDronesCompute.SetFloat(PhantomVerticalAmplitudePropertyId, PhantomDroneVerticalAmplitudeMeters);
            s_PhantomDronesCompute.SetFloat(PhantomScalePropertyId, PhantomDroneScaleMeters);
            s_PhantomDronesCompute.SetInt(PhantomCapacityPropertyId, PhantomDroneCount);
            s_PhantomDronesCompute.SetBuffer(s_PhantomDroneKernel, PhantomMatricesPropertyId, s_PhantomDroneMatrixBuffer);
            s_PhantomDronesCompute.SetBuffer(s_PhantomDroneKernel, PhantomColorsPropertyId, s_PhantomDroneColorBuffer);
            s_PhantomDronesCompute.Dispatch(
                s_PhantomDroneKernel,
                (PhantomDroneCount + PhantomDroneThreadGroupSize - 1) / PhantomDroneThreadGroupSize,
                1,
                1);

            s_DroneProceduralMaterial.SetBuffer(DroneMatricesPropertyId, s_PhantomDroneMatrixBuffer);
            s_DroneProceduralMaterial.SetBuffer(InstanceMatricesPropertyId, s_PhantomDroneMatrixBuffer);
            s_DroneProceduralMaterial.SetBuffer(PhantomColorsPropertyId, s_PhantomDroneColorBuffer);
            s_DroneProceduralMaterial.SetVector(DroneProceduralCameraOriginPropertyId, Vector4.zero);
            s_DroneProceduralMaterial.SetInt(UsePhantomColorsPropertyId, 1);

            UnityEngine.Graphics.DrawProceduralIndirect(
                s_DroneProceduralMaterial,
                s_PhantomDroneDrawBounds,
                MeshTopology.Triangles,
                s_PhantomDroneArgsBuffer,
                0,
                null,
                null,
                ShadowCastingMode.Off,
                false,
                s_DroneRenderLayer);
        }

        private static bool TryResolvePhantomAnchor(out Vector3 position)
        {
            if (TryResolveFormationAnchor(out position))
                return true;

            RepairDroneHub hub = RepairDroneHub.GetActiveHubAt(0);
            if (hub != null)
            {
                position = hub.DockPosition;
                return true;
            }

            return TryResolvePlayerPosition(out position);
        }

        private static void PrepareDroneRenderInstances()
        {
            if (!s_DroneRenderInstances.IsCreated || !s_DroneRenderMatrices.IsCreated)
                return;

            for (int i = 0; i < HeadlessDroneCapacity; i++)
            {
                float transactionProgress = 0f;
                HeadlessDroneState drone = default;
                bool hasDroneState = s_DroneStates.IsCreated;
                if (hasDroneState)
                {
                    drone = s_DroneStates[i];
                    transactionProgress = Mathf.Clamp01(drone.TransactionProgress);
                }

                s_DroneRenderInstances[i] = new DroneRenderInstance
                {
                    Matrix = s_DroneRenderMatrices[i],
                    TransactionProgress = transactionProgress,
                    Padding = float3.zero
                };

                if (s_DroneCullingStates.IsCreated)
                {
                    s_DroneCullingStates[i] = hasDroneState
                        ? new DroneCullingStateGpu
                        {
                            Position = drone.Position,
                            PackedStateFactionCorridor = PackStateFactionCorridor(in drone)
                        }
                        : default;
                }
            }
        }

        private static uint PackStateFactionCorridor(in HeadlessDroneState drone)
        {
            return ((uint)drone.State) |
                   ((uint)drone.FactionBit << 8) |
                   ((uint)drone.CorridorTight << 16);
        }

        private static bool TryRenderGpuCulledFleet(GraphicsBuffer matrixBuffer)
        {
            return false;
        }

        private static float ResolveDroneRenderDistanceMeters()
        {
            float quality = ResolveGlobalQualityWeight();
            return math.lerp(LowTierDroneRenderDistanceMeters, HighTierDroneRenderDistanceMeters, quality);
        }

        private static void UpdateDrawBounds()
        {
            if (s_DroneSlotDroneIds == null)
                return;

            bool found = false;
            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;
            for (int i = 0; i < s_DroneSlotDroneIds.Length; i++)
            {
                if (s_DroneSlotDroneIds[i] <= 0)
                    continue;

                Vector3 position = s_DronePositions[i];
                if (!found)
                {
                    min = position;
                    max = position;
                    found = true;
                    continue;
                }

                min = Vector3.Min(min, position);
                max = Vector3.Max(max, position);
            }

            if (!found)
                return;

            Vector3 center = (min + max) * 0.5f;
            Vector3 size = (max - min) + new Vector3(16f, 16f, 16f);
            s_DroneDrawBounds = new Bounds(center, size);
        }

        private static void CaptureFleetBlackBoxFrame()
        {
            if (!s_DroneBlackBox.IsCreated || s_DroneSlotDroneIds == null || !s_DroneStates.IsCreated)
                return;

            int activeCount = 0;
            int stateHash = 17;
            int flags = 0;
            float3 firstPosition = float3.zero;
            for (int slot = 0; slot < HeadlessDroneCapacity; slot++)
            {
                int droneId = s_DroneSlotDroneIds[slot];
                if (droneId <= 0)
                    continue;

                HeadlessDroneState drone = s_DroneStates[slot];
                if (activeCount == 0)
                    firstPosition = drone.Position;

                activeCount++;
                stateHash = unchecked((stateHash * 31) ^ droneId);
                stateHash = unchecked((stateHash * 31) ^ drone.State);
                stateHash = unchecked((stateHash * 31) ^ (int)math.hash(drone.Position));
                stateHash = unchecked((stateHash * 31) ^ (int)math.hash(drone.TargetPosition));

                if (!IsFiniteFloat3(drone.Position) ||
                    !IsFiniteFloat3(drone.TargetPosition) ||
                    !IsFiniteFloat3(drone.Velocity))
                {
                    flags |= 1;
                }
            }

            if (s_LastDroneAStarStatus == 2)
                flags |= 2;

            int index = s_DroneBlackBoxCursor;
            if ((uint)index >= (uint)s_DroneBlackBox.Length)
                index = 0;

            s_DroneBlackBox[index] = new DroneFleetBlackBoxEntry
            {
                Frame = Time.frameCount,
                ActiveCount = activeCount,
                StateHash = stateHash,
                Flags = flags,
                DeltaTime = s_LastHeadlessDeltaTime,
                DockingAborts = s_DockingAbortCount,
                PathSolves = s_DroneAStarSolvedCount,
                PathFailures = s_DroneAStarFailureCount,
                PathIterations = s_DroneAStarIterationCount,
                AveragePathfindingTimeMs = s_LastDroneAStarAveragePathfindingTimeMs,
                TasksCompleted = s_DroneTasksCompletedCount,
                FirstPosition = firstPosition,
                BoundsCenter = ToFloat3(s_DroneDrawBounds.center),
                BoundsExtents = ToFloat3(s_DroneDrawBounds.extents)
            };
            s_DroneBlackBoxCursor = (index + 1) % s_DroneBlackBox.Length;

            if ((flags & 1) != 0)
                DumpDroneBlackBoxOncePerFrame();
        }

        private static void DumpDroneBlackBoxOncePerFrame()
        {
            int frame = Time.frameCount;
            if (s_LastDroneBlackBoxDumpFrame == frame)
                return;

            s_LastDroneBlackBoxDumpFrame = frame;
            TryDumpDroneBlackBox();
        }

        private static void TryDumpDroneBlackBox()
        {
            if (!s_DroneBlackBox.IsCreated)
                return;

            TryWriteDroneBlackBoxFile(DroneFleetBlackBoxDumpPath);
            TryWriteDroneBlackBoxFile(DroneFleetLegacyBlackBoxDumpPath);
            TryWriteDroneBlackBoxFile(DroneFleetBlackBoxH8DumpPath);
        }

        private static void TryWriteDroneBlackBoxFile(string relativePath)
        {
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string dumpPath = Path.Combine(projectRoot, relativePath);
                string directory = Path.GetDirectoryName(dumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                using BinaryWriter writer = new BinaryWriter(stream);
                writer.Write(DroneFleetBlackBoxFrameCapacity);
                writer.Write(s_DroneBlackBoxCursor);
                for (int i = 0; i < s_DroneBlackBox.Length; i++)
                    WriteBlackBoxEntry(writer, s_DroneBlackBox[i]);
            }
            catch (System.Exception)
            {
            }
        }

        private static void WriteBlackBoxEntry(BinaryWriter writer, in DroneFleetBlackBoxEntry entry)
        {
            writer.Write(entry.Frame);
            writer.Write(entry.ActiveCount);
            writer.Write(entry.StateHash);
            writer.Write(entry.Flags);
            writer.Write(entry.DeltaTime);
            writer.Write(entry.DockingAborts);
            writer.Write(entry.PathSolves);
            writer.Write(entry.PathFailures);
            writer.Write(entry.PathIterations);
            writer.Write(entry.AveragePathfindingTimeMs);
            writer.Write(entry.TasksCompleted);
            WriteFloat3(writer, entry.FirstPosition);
            WriteFloat3(writer, entry.BoundsCenter);
            WriteFloat3(writer, entry.BoundsExtents);
        }

        private static void WriteFloat3(BinaryWriter writer, float3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }

        internal static bool TryGetDroneFleetTuningConstants(out DroneFleetTuningConstants constants)
        {
            constants = ResolveDroneTuning();
            return s_DroneTuningConstants.IsCreated;
        }

        internal static void ApplyDroneFleetTuningConstants(in DroneFleetTuningConstants constants)
        {
            EnsureInitialized();
            if (!s_DroneTuningConstants.IsCreated || s_DroneTuningConstants.Length <= 0)
                return;

            s_DroneTuningConstants[0] = SanitizeDroneTuning(constants);
        }

        internal static bool TryGetDroneFleetAutomationStats(out DroneFleetAutomationStats stats)
        {
            DroneFleetTuningConstants tuning = ResolveDroneTuning();
            stats = new DroneFleetAutomationStats
            {
                ActiveDrones = CountManagedHeadlessDrones(),
                PathSolves = s_DroneAStarSolvedCount,
                PathFailures = s_DroneAStarFailureCount,
                PathIterations = s_DroneAStarIterationCount,
                TasksCompleted = s_DroneTasksCompletedCount,
                LastAStarStatus = s_LastDroneAStarStatus,
                SteeringTickModulo = s_LastDroneSteeringTickModulo,
                ChassisSpecCount = s_DroneChassisSpecCount,
                AveragePathfindingTimeMs = s_LastDroneAStarAveragePathfindingTimeMs,
                SdfRepulsionStrength = tuning.SdfRepulsionStrength,
                AStarCellSize = tuning.AStarCellSize,
                AverageBatteryPercent = s_LastFleetStatusSnapshot.AverageBattery
            };

            return s_DroneStates.IsCreated;
        }

        internal static int CopyDroneFleetDebugRoutes(DroneFleetDebugRoute[] buffer)
        {
            if (buffer == null ||
                buffer.Length <= 0 ||
                !s_DroneStates.IsCreated ||
                s_DroneSlotDroneIds == null ||
                s_HeadlessJobScheduled)
            {
                return 0;
            }

            DroneFleetTuningConstants tuning = ResolveDroneTuning();
            MockSDFGrid sdfGrid = BuildMockSdfGrid(in tuning);
            int count = 0;
            int limit = Mathf.Min(buffer.Length, HeadlessDroneCapacity);
            for (int slot = 0; slot < HeadlessDroneCapacity && count < limit; slot++)
            {
                if (s_DroneSlotDroneIds[slot] <= 0)
                    continue;

                HeadlessDroneState drone = s_DroneStates[slot];
                if (drone.State == (byte)HeadlessDroneRuntimeState.Empty ||
                    drone.State == (byte)HeadlessDroneRuntimeState.Sacrificed ||
                    drone.State == (byte)HeadlessDroneRuntimeState.Completed)
                {
                    continue;
                }

                float3 waypoint = drone.TargetPosition;
                int pathStatus = 0;
                if (s_DroneMacroWaypoints.IsCreated &&
                    s_DroneMacroWaypointStates.IsCreated &&
                    slot < s_DroneMacroWaypoints.Length &&
                    slot < s_DroneMacroWaypointStates.Length &&
                    s_DroneMacroWaypointStates[slot] != 0)
                {
                    waypoint = s_DroneMacroWaypoints[slot].LocalPosition;
                    pathStatus = s_DroneMacroWaypointStates[slot];
                }

                float3 sdfNormal = float3.zero;
                byte flags = 0;
                if (sdfGrid.TrySampleRepulsion(drone.Position, out sdfNormal, out _))
                    flags |= 1;

                int routePointCount = ResolveDebugRoutePoints(
                    slot,
                    drone.Position,
                    tuning.AStarCellSize,
                    out float3 routePoint0,
                    out float3 routePoint1,
                    out float3 routePoint2,
                    out float3 routePoint3);
                buffer[count++] = new DroneFleetDebugRoute
                {
                    Position = drone.Position,
                    Target = drone.TargetPosition,
                    Waypoint = waypoint,
                    SdfNormal = sdfNormal,
                    Velocity = drone.Velocity,
                    RoutePoint0 = routePoint0,
                    RoutePoint1 = routePoint1,
                    RoutePoint2 = routePoint2,
                    RoutePoint3 = routePoint3,
                    RoutePointCount = routePointCount,
                    DroneId = drone.DroneId,
                    PathStatus = pathStatus,
                    BatteryPercent = drone.BatteryPercent,
                    State = drone.State,
                    Flags = flags,
                    Reserved0 = 0,
                    Reserved1 = 0u,
                    Reserved2 = 0u,
                    Reserved3 = 0u
                };
            }

            return count;
        }

        private static int ResolveDebugRoutePoints(
            int slot,
            float3 origin,
            float cellSize,
            out float3 routePoint0,
            out float3 routePoint1,
            out float3 routePoint2,
            out float3 routePoint3)
        {
            routePoint0 = origin;
            routePoint1 = origin;
            routePoint2 = origin;
            routePoint3 = origin;
            if (!s_DroneMacroRouteNodes.IsCreated ||
                !s_DroneMacroRouteCounts.IsCreated ||
                slot < 0 ||
                slot >= s_DroneMacroRouteCounts.Length)
            {
                return 0;
            }

            int nodeCount = math.min(s_DroneMacroRouteCounts[slot], DroneAStarRouteNodeStride);
            int pointCount = math.min(nodeCount, DroneAStarRouteDebugPointCount);
            int offset = slot * DroneAStarRouteNodeStride;
            if (nodeCount <= 0 || offset < 0 || offset >= s_DroneMacroRouteNodes.Length)
                return 0;

            float cell = Mathf.Max(0.5f, cellSize);
            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                int sourceIndex = nodeCount - 1 - pointIndex;
                int packedNode = s_DroneMacroRouteNodes[offset + sourceIndex];
                float3 routePoint = ResolveAStarRoutePoint(packedNode, origin, cell);
                if (pointIndex == 0)
                    routePoint0 = routePoint;
                else if (pointIndex == 1)
                    routePoint1 = routePoint;
                else if (pointIndex == 2)
                    routePoint2 = routePoint;
                else
                    routePoint3 = routePoint;
            }

            return pointCount;
        }

        private static float3 ResolveAStarRoutePoint(int packedNode, float3 origin, float cell)
        {
            int z = packedNode / (DroneAStarGridSide * DroneAStarGridSide);
            int remainder = packedNode - (z * DroneAStarGridSide * DroneAStarGridSide);
            int y = remainder / DroneAStarGridSide;
            int x = remainder - (y * DroneAStarGridSide);
            float3 coord = new float3(x, y, z) - new float3(DroneAStarGridSide >> 1, DroneAStarGridSide >> 1, DroneAStarGridSide >> 1);
            return origin + (coord * cell);
        }

        internal static bool TryAutoApplyDroneSpecsCsv(out int keysApplied)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string primaryPath = Path.Combine(projectRoot, DroneSpecsCsvFileName);
            if (TryApplyDroneSpecsCsv(primaryPath, out keysApplied))
                return true;

            string legacyPath = Path.Combine(projectRoot, DroneSpecsCsvLegacyFileName);
            return TryApplyDroneSpecsCsv(legacyPath, out keysApplied);
        }

        internal static bool TryApplyDroneSpecsCsv(string path, out int keysApplied)
        {
            keysApplied = 0;
            string resolvedPath = string.IsNullOrWhiteSpace(path)
                ? Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), DroneSpecsCsvFileName)
                : path;

            if (!File.Exists(resolvedPath))
                return false;

            EnsureInitialized();
            if (!s_DroneSpecsCsvScratch.IsCreated || s_DroneSpecsCsvScratch.Length <= 0)
                return false;

            DroneFleetTuningConstants tuning = ResolveDroneTuning();
            Span<DroneChassisSpecDTO> stagedChassisSpecs = stackalloc DroneChassisSpecDTO[DroneChassisSpecCapacity];
            int stagedChassisSpecCount = 0;
            unsafe
            {
                int bytesRead;
                byte* scratchPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(s_DroneSpecsCsvScratch);
                Span<byte> scratch = new Span<byte>(scratchPtr, s_DroneSpecsCsvScratch.Length);
                using (FileStream stream = File.OpenRead(resolvedPath))
                {
                    bytesRead = stream.Read(scratch);
                }

                if (bytesRead <= 0)
                    return false;

                ReadOnlySpan<byte> bytes = new ReadOnlySpan<byte>(scratchPtr, bytesRead);
                int lineStart = 0;
                for (int i = 0; i <= bytesRead; i++)
                {
                    if (i < bytesRead && bytes[i] != (byte)'\n')
                        continue;

                    if (TryApplyDroneSpecLine(bytes, lineStart, i, ref tuning))
                        keysApplied++;
                    lineStart = i + 1;
                }

                tuning = SanitizeDroneTuning(tuning);
                lineStart = 0;
                for (int i = 0; i <= bytesRead; i++)
                {
                    if (i < bytesRead && bytes[i] != (byte)'\n')
                        continue;

                    if (TryApplyDroneChassisSpecLine(bytes, lineStart, i, in tuning, stagedChassisSpecs, ref stagedChassisSpecCount))
                        keysApplied++;
                    lineStart = i + 1;
                }
            }

            if (keysApplied <= 0)
                return false;

            ApplyDroneFleetTuningConstants(in tuning);
            if (stagedChassisSpecCount > 0)
                CommitDroneChassisSpecs(stagedChassisSpecs, stagedChassisSpecCount);
            return true;
        }

        private static bool TryApplyDroneSpecLine(ReadOnlySpan<byte> bytes, int lineStart, int lineEnd, ref DroneFleetTuningConstants tuning)
        {
            int start = TrimAsciiLeft(bytes, lineStart, lineEnd);
            int end = TrimAsciiRight(bytes, start, lineEnd);
            if (start >= end || bytes[start] == (byte)'#')
                return false;

            int separator = FindKeyValueSeparator(bytes, start, end);

            if (separator <= start || separator >= end - 1)
                return false;

            int secondSeparator = FindCsvSeparator(bytes, separator + 1, end);
            if (secondSeparator > separator)
                return false;

            int keyStart = TrimAsciiLeft(bytes, start, separator);
            int keyEnd = TrimAsciiRight(bytes, keyStart, separator);
            int valueStart = TrimAsciiLeft(bytes, separator + 1, end);
            int valueEnd = TrimAsciiRight(bytes, valueStart, end);
            if (keyStart >= keyEnd || !TryParseAsciiFloat(bytes, valueStart, valueEnd, out float value))
                return false;

            return TryApplyDroneSpecKey(bytes, keyStart, keyEnd, value, ref tuning);
        }

        private static bool TryApplyDroneChassisSpecLine(
            ReadOnlySpan<byte> bytes,
            int lineStart,
            int lineEnd,
            in DroneFleetTuningConstants tuning,
            Span<DroneChassisSpecDTO> stagedSpecs,
            ref int stagedCount)
        {
            int typeEnd = FindCsvSeparator(bytes, lineStart, lineEnd);
            if (typeEnd <= lineStart)
                return false;

            int secondSeparator = FindCsvSeparator(bytes, typeEnd + 1, lineEnd);
            if (secondSeparator <= typeEnd)
                return false;

            int typeStart = TrimAsciiLeft(bytes, lineStart, typeEnd);
            int trimmedTypeEnd = TrimAsciiRight(bytes, typeStart, typeEnd);
            if (typeStart >= trimmedTypeEnd ||
                AsciiEqualsIgnoreCase(bytes, typeStart, trimmedTypeEnd, "Type") ||
                AsciiEqualsIgnoreCase(bytes, typeStart, trimmedTypeEnd, "DroneType") ||
                AsciiEqualsIgnoreCase(bytes, typeStart, trimmedTypeEnd, "Chassis"))
            {
                return false;
            }

            if (!IsKnownDroneChassisName(bytes, typeStart, trimmedTypeEnd) &&
                IsReservedDroneSpecKeyName(bytes, typeStart, trimmedTypeEnd))
            {
                return false;
            }

            uint typeHash = ComputeAsciiFnv1aLower(bytes, typeStart, trimmedTypeEnd);
            DroneChassisSpecDTO spec = CreateFallbackDroneChassisSpec(typeHash, in tuning);
            int cursor = typeEnd + 1;
            bool parsedAnyValue = false;

            if (TryReadDelimitedFloat(bytes, ref cursor, lineEnd, out float maxSpeed))
            {
                spec.MaxSpeed = maxSpeed;
                parsedAnyValue = true;
            }

            if (TryReadDelimitedFloat(bytes, ref cursor, lineEnd, out float batteryCapacity))
            {
                spec.BatteryCapacity = batteryCapacity;
                parsedAnyValue = true;
            }

            if (TryReadDelimitedFloat(bytes, ref cursor, lineEnd, out float batteryDrainRate))
            {
                spec.BatteryDrainRate = batteryDrainRate;
                parsedAnyValue = true;
            }

            if (TryReadDelimitedFloat(bytes, ref cursor, lineEnd, out float repairSpeed))
            {
                spec.RepairSpeed = repairSpeed;
                parsedAnyValue = true;
            }

            if (TryReadDelimitedFloat(bytes, ref cursor, lineEnd, out float cargoCapacity))
            {
                spec.CargoCapacity = cargoCapacity;
                parsedAnyValue = true;
            }

            if (TryReadDelimitedFloat(bytes, ref cursor, lineEnd, out float miningHoldSeconds))
            {
                spec.MiningHoldSeconds = miningHoldSeconds;
                parsedAnyValue = true;
            }

            if (TryReadDelimitedFloat(bytes, ref cursor, lineEnd, out float sdfRepulsionScale))
            {
                spec.SdfRepulsionScale = sdfRepulsionScale;
                parsedAnyValue = true;
            }

            if (!parsedAnyValue)
                return false;

            return TryUpsertStagedDroneChassisSpec(spec, in tuning, stagedSpecs, ref stagedCount);
        }

        private static bool IsKnownDroneChassisName(ReadOnlySpan<byte> bytes, int start, int end)
        {
            return AsciiEqualsIgnoreCase(bytes, start, end, "Repair") ||
                AsciiEqualsIgnoreCase(bytes, start, end, "Mining") ||
                AsciiEqualsIgnoreCase(bytes, start, end, "Combat") ||
                AsciiEqualsIgnoreCase(bytes, start, end, "CutParasite");
        }

        private static bool IsReservedDroneSpecKeyName(ReadOnlySpan<byte> bytes, int start, int end)
        {
            return AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.MaxDroneSpeed)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, "Speed") ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.BatteryDrainRate)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, "BatteryDrain") ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.SdfRepulsionStrength)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, "SDF") ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.RepairSpeed)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, "Repair") ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.CargoCapacity)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, "Cargo") ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.MiningHoldSeconds)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.LowTierSteeringHz)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.MidTierSteeringHz)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.HighTierSteeringHz)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.UltraTierSteeringHz)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.AStarCellSize)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.LowTierSolveBudget)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.MidTierSolveBudget)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.HighTierSolveBudget)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.UltraTierSolveBudget));
        }

        private static bool TryApplyDroneSpecKey(ReadOnlySpan<byte> bytes, int keyStart, int keyEnd, float value, ref DroneFleetTuningConstants tuning)
        {
            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.MaxDroneSpeed)) ||
                AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, "Speed"))
            {
                tuning.MaxDroneSpeed = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.BatteryDrainRate)) ||
                AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, "BatteryDrain"))
            {
                tuning.BatteryDrainRate = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.SdfRepulsionStrength)) ||
                AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, "SDF"))
            {
                tuning.SdfRepulsionStrength = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.RepairSpeed)) ||
                AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, "Repair"))
            {
                tuning.RepairSpeed = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.CargoCapacity)) ||
                AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, "Cargo"))
            {
                tuning.CargoCapacity = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.MiningHoldSeconds)))
            {
                tuning.MiningHoldSeconds = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.LowTierSteeringHz)))
            {
                tuning.LowTierSteeringHz = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.MidTierSteeringHz)))
            {
                tuning.MidTierSteeringHz = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.HighTierSteeringHz)))
            {
                tuning.HighTierSteeringHz = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.UltraTierSteeringHz)))
            {
                tuning.UltraTierSteeringHz = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.AStarCellSize)))
            {
                tuning.AStarCellSize = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.LowTierSolveBudget)))
            {
                tuning.LowTierSolveBudget = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.MidTierSolveBudget)))
            {
                tuning.MidTierSolveBudget = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.HighTierSolveBudget)))
            {
                tuning.HighTierSolveBudget = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.UltraTierSolveBudget)))
            {
                tuning.UltraTierSolveBudget = value;
                return true;
            }

            return false;
        }

        private static int FindKeyValueSeparator(ReadOnlySpan<byte> bytes, int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                byte token = bytes[i];
                if (token == (byte)',' || token == (byte)'=' || token == (byte)';' || token == (byte)'\t')
                    return i;
            }

            return -1;
        }

        private static int FindCsvSeparator(ReadOnlySpan<byte> bytes, int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                byte token = bytes[i];
                if (token == (byte)',' || token == (byte)';' || token == (byte)'\t')
                    return i;
            }

            return -1;
        }

        private static bool TryReadDelimitedFloat(ReadOnlySpan<byte> bytes, ref int cursor, int end, out float value)
        {
            value = 0f;
            if (cursor >= end)
                return false;

            int fieldEnd = FindCsvSeparator(bytes, cursor, end);
            if (fieldEnd < 0)
                fieldEnd = end;

            int valueStart = TrimAsciiLeft(bytes, cursor, fieldEnd);
            int valueEnd = TrimAsciiRight(bytes, valueStart, fieldEnd);
            cursor = fieldEnd < end ? fieldEnd + 1 : end;
            return valueStart < valueEnd && TryParseAsciiFloat(bytes, valueStart, valueEnd, out value);
        }

        private static uint ComputeAsciiFnv1aLower(ReadOnlySpan<byte> bytes, int start, int end)
        {
            uint hash = 2166136261u;
            for (int i = start; i < end; i++)
            {
                hash ^= ToAsciiLower(bytes[i]);
                hash *= 16777619u;
            }

            return hash;
        }

        private static int TrimAsciiLeft(ReadOnlySpan<byte> bytes, int start, int end)
        {
            while (start < end && IsAsciiWhitespace(bytes[start]))
                start++;

            return start;
        }

        private static int TrimAsciiRight(ReadOnlySpan<byte> bytes, int start, int end)
        {
            while (end > start && IsAsciiWhitespace(bytes[end - 1]))
                end--;

            return end;
        }

        private static bool TryParseAsciiFloat(ReadOnlySpan<byte> bytes, int start, int end, out float value)
        {
            value = 0f;
            if (start >= end)
                return false;

            int i = start;
            bool negative = false;
            if (bytes[i] == (byte)'+' || bytes[i] == (byte)'-')
            {
                negative = bytes[i] == (byte)'-';
                i++;
            }

            float result = 0f;
            bool hasDigit = false;
            while (i < end && IsAsciiDigit(bytes[i]))
            {
                result = (result * 10f) + (bytes[i] - (byte)'0');
                hasDigit = true;
                i++;
            }

            if (i < end && bytes[i] == (byte)'.')
            {
                i++;
                float scale = 0.1f;
                while (i < end && IsAsciiDigit(bytes[i]))
                {
                    result += (bytes[i] - (byte)'0') * scale;
                    scale *= 0.1f;
                    hasDigit = true;
                    i++;
                }
            }

            int exponent = 0;
            if (hasDigit && i < end && (bytes[i] == (byte)'e' || bytes[i] == (byte)'E'))
            {
                i++;
                bool exponentNegative = false;
                if (i < end && (bytes[i] == (byte)'+' || bytes[i] == (byte)'-'))
                {
                    exponentNegative = bytes[i] == (byte)'-';
                    i++;
                }

                bool hasExponentDigit = false;
                while (i < end && IsAsciiDigit(bytes[i]))
                {
                    exponent = math.min(38, (exponent * 10) + (bytes[i] - (byte)'0'));
                    hasExponentDigit = true;
                    i++;
                }

                if (!hasExponentDigit)
                    return false;

                if (exponentNegative)
                    exponent = -exponent;
            }

            if (!hasDigit || i != end)
                return false;

            if (exponent != 0)
                result *= ResolvePow10(exponent);

            value = negative ? -result : result;
            return math.isfinite(value);
        }

        private static float ResolvePow10(int exponent)
        {
            int steps = math.min(38, math.abs(exponent));
            float scale = 1f;
            for (int i = 0; i < steps; i++)
                scale *= 10f;

            return exponent < 0 ? math.rcp(scale) : scale;
        }

        private static bool AsciiEqualsIgnoreCase(ReadOnlySpan<byte> bytes, int start, int end, string expected)
        {
            int length = end - start;
            if (length != expected.Length)
                return false;

            for (int i = 0; i < length; i++)
            {
                byte actual = ToAsciiLower(bytes[start + i]);
                byte target = ToAsciiLower((byte)expected[i]);
                if (actual != target)
                    return false;
            }

            return true;
        }

        private static bool IsAsciiWhitespace(byte value)
        {
            return value == (byte)' ' ||
                   value == (byte)'\t' ||
                   value == (byte)'\r' ||
                   value == (byte)'\n';
        }

        private static bool IsAsciiDigit(byte value)
        {
            return value >= (byte)'0' && value <= (byte)'9';
        }

        private static byte ToAsciiLower(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }

        private static void HandleSubmarineSnapshotUpdated(in HectonSubmarineOsSnapshot snapshot)
        {
            s_EmergencyLevel = snapshot.EmergencyLevel;
            PublishSnapshot();
        }

        private static void HandleStorageReservationCommitResolved(int requesterId, int reservationId, bool committed)
        {
            if (requesterId <= 0 || reservationId <= 0 || s_DroneSlotDroneIds == null)
                return;

            int slot = ResolveHeadlessSlot(requesterId);
            if (slot < 0)
                return;

            if (committed)
            {
                s_PendingResupplyGrantBySlot[slot] = true;
                s_PendingResupplyFailureBySlot[slot] = false;
            }
            else
            {
                s_PendingResupplyGrantBySlot[slot] = false;
                s_PendingResupplyFailureBySlot[slot] = true;
            }
        }

        private static bool IsEligibleRepairTarget(PowerGrid hubGrid, BaseModule module, float dispatchIntegrityThreshold)
        {
            if (module == null)
                return false;

            float recoverableIntegrity = Mathf.Max(1f, module.MaxRecoverableIntegrity);
            float integrity01 = Mathf.Clamp01(module.CurrentIntegrity * math.rcp(recoverableIntegrity));
            bool graphRuptured = BaseDegradationSystem.IsModuleRuptured(module);
            bool belowThreshold = integrity01 < dispatchIntegrityThreshold;

            if (!belowThreshold && !module.IsFlooded && !module.HasCascadeFailure && !graphRuptured)
                return false;

            if (IsDifferentGrid(hubGrid, module))
                return false;

            return module.CurrentIntegrity < recoverableIntegrity || module.IsFlooded || module.HasCascadeFailure || graphRuptured;
        }

        private static bool IsDifferentGrid(PowerGrid hubGrid, BaseModule module)
        {
            if (hubGrid == null || module == null)
                return false;

            PowerGrid moduleGrid = module.CachedPowerGrid;
            if (moduleGrid == null)
                return false;

            return !ReferenceEquals(moduleGrid, hubGrid);
        }

        private static float ResolveCriticalityWeight(BaseModule module)
        {
            float recoverableIntegrity = Mathf.Max(1f, module.MaxRecoverableIntegrity);
            float integrity01 = Mathf.Clamp01(module.CurrentIntegrity * math.rcp(recoverableIntegrity));
            float integrityDeficit01 = 1f - integrity01;
            float weight = 1f + (integrityDeficit01 * 4f);

            if (module.IsFlooded)
                weight += FloodCriticalityBonus;

            if (module.IsBreached)
                weight += BreachCriticalityBonus;

            if (module.HasCascadeFailure)
                weight += CascadeCriticalityBonus;

            if (BaseDegradationSystem.IsModuleRuptured(module))
                weight += RuptureCriticalityBonus;

            weight += (1f - Mathf.Clamp01(module.AirReserveNormalized)) * AirReserveCriticalityScale;

            if (s_EmergencyLevel == SubmarineEmergencyLevel.Evacuate)
                weight *= EmergencyCriticalityScale;

            return weight;
        }

        private static float ResolveParasiteCriticalityWeight(BaseModule module, in FloraInteractionManager.ModuleParasiteTarget parasiteTarget)
        {
            float moduleAirRisk = module != null ? 1f - Mathf.Clamp01(module.AirReserveNormalized) : 0f;
            float infection = Mathf.Clamp01(parasiteTarget.InfectionLevel);
            float weight = ParasiteCriticalityBonus + (infection * 6f) + (moduleAirRisk * AirReserveCriticalityScale);
            if (module != null && module.HasCascadeFailure)
                weight += CascadeCriticalityBonus;

            if (s_EmergencyLevel == SubmarineEmergencyLevel.Evacuate)
                weight *= EmergencyCriticalityScale;

            return weight;
        }

        private static void EnsureTaskCapacity(int requiredCount)
        {
            if (requiredCount <= 0)
                return;

            if (!s_TaskClaimCounts.IsCreated || s_TaskClaimCounts.Length < requiredCount)
            {
                ReleaseDroneVaultBuffer(ref s_TaskClaimCounts, ref s_TaskClaimCountsHandle, ref s_TaskClaimCountsVaultBacked, nameof(s_TaskClaimCounts));

                int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(requiredCount, InitialTaskCapacity));
                s_TaskClaimCounts = ResolveDroneVaultBuffer<int>(
                    BufferID.ShinobuDroneFleetTaskClaimCounts,
                    nextCapacity,
                    NativeArrayOptions.ClearMemory,
                    ref s_TaskClaimCountsHandle,
                    out s_TaskClaimCountsVaultBacked); // COLD ALLOC: NativeArray<int>[nextCapacity] - per-module active-claim locks - owner: GlobalDataVault/H8Memory fallback
                RegisterNativeArrayIfFallback(s_TaskClaimCounts, s_TaskClaimCountsVaultBacked, nameof(s_TaskClaimCounts));
            }
        }

        private static void RegisterNativeArray<T>(NativeArray<T> array, string label) where T : struct
        {
            NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime);
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        private static void ConsiderTaskCandidate(
            in RepairTaskCandidate candidate,
            ref RepairTaskCandidate bestTask,
            ref bool hasBestTask)
        {
            if (candidate.Module == null ||
                candidate.ModuleIndex < 0 ||
                !s_TaskClaimCounts.IsCreated ||
                candidate.ModuleIndex >= s_TaskClaimCounts.Length ||
                s_TaskClaimCounts[candidate.ModuleIndex] >= DefaultMaxClaimsPerTarget)
            {
                return;
            }

            if (hasBestTask && candidate.Score <= bestTask.Score)
                return;

            bestTask = candidate;
            hasBestTask = true;
        }

        private static void TryPushTaskPriorityCandidate(ref DroneTaskNativeMinHeap heap, in RepairTaskCandidate candidate)
        {
            if (!heap.Nodes.IsCreated ||
                candidate.Module == null ||
                candidate.ModuleIndex < 0 ||
                !s_TaskClaimCounts.IsCreated ||
                candidate.ModuleIndex >= s_TaskClaimCounts.Length ||
                s_TaskClaimCounts[candidate.ModuleIndex] >= DefaultMaxClaimsPerTarget)
            {
                return;
            }

            DroneTaskDTO dto = new DroneTaskDTO
            {
                TargetAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(candidate.Position),
                LocalPosition = ToFloat3(candidate.Position),
                Priority = ResolveTaskPriority(candidate.Kind),
                Score = candidate.Score,
                CriticalityWeight = candidate.CriticalityWeight,
                Radius = candidate.Radius,
                ModuleIndex = candidate.ModuleIndex,
                TaskKind = (int)candidate.Kind,
                Reserved0 = 0u
            };
            heap.TryPush(in dto);
        }

        private static bool TryResolvePriorityHeapTask(
            ref DroneTaskNativeMinHeap heap,
            ConstructionManager manager,
            out RepairTaskCandidate candidate)
        {
            candidate = default;
            if (manager == null)
                return false;

            while (heap.TryPop(out DroneTaskDTO dto))
            {
                if (dto.ModuleIndex < 0 ||
                    !s_TaskClaimCounts.IsCreated ||
                    dto.ModuleIndex >= s_TaskClaimCounts.Length ||
                    s_TaskClaimCounts[dto.ModuleIndex] >= DefaultMaxClaimsPerTarget)
                {
                    continue;
                }

                BaseModule module = manager.GetSpawnedBaseModuleAt(dto.ModuleIndex);
                if (module == null || !module.gameObject.activeInHierarchy)
                    continue;

                DroneFleetTaskKind kind = (DroneFleetTaskKind)dto.TaskKind;
                if (kind == DroneFleetTaskKind.None)
                    continue;

                candidate = new RepairTaskCandidate
                {
                    Kind = kind,
                    Module = module,
                    ModuleIndex = dto.ModuleIndex,
                    Position = ToVector3(dto.LocalPosition),
                    Radius = dto.Radius,
                    Score = dto.Score,
                    CriticalityWeight = dto.CriticalityWeight
                };
                return true;
            }

            return false;
        }

        private static float ResolveTaskPriority(DroneFleetTaskKind kind)
        {
            if (kind == DroneFleetTaskKind.RepairModule)
                return 1f;

            if (kind == DroneFleetTaskKind.CutParasite)
                return 10f;

            if (kind == DroneFleetTaskKind.MineNode)
                return 10f;

            return 1024f;
        }

        private static void ClearClaimCounts(int moduleCount)
        {
            for (int i = 0; i < moduleCount; i++)
                s_TaskClaimCounts[i] = 0;
        }

        private static void RebuildActiveClaimCounts(ConstructionManager manager, int moduleCount)
        {
            if (manager == null)
                return;

            if (s_DroneSlotDroneIds != null)
            {
                for (int slot = 0; slot < s_DroneSlotDroneIds.Length; slot++)
                {
                    if (s_DroneSlotDroneIds[slot] <= 0)
                        continue;

                    IncrementClaimForTarget(manager, moduleCount, s_TargetModulesByDroneSlot[slot]);
                }
            }

            if (s_PendingLaunches != null)
            {
                for (int i = 0; i < s_PendingLaunchCount; i++)
                {
                    if (!s_PendingLaunches[i].Active)
                        continue;

                    IncrementClaimForTarget(manager, moduleCount, s_PendingLaunches[i].Task.Module);
                }
            }
        }

        private static void IncrementClaimForTarget(ConstructionManager manager, int moduleCount, BaseModule target)
        {
            if (manager == null || target == null)
                return;

            for (int moduleIndex = 0; moduleIndex < moduleCount; moduleIndex++)
            {
                BaseModule module = manager.GetSpawnedBaseModuleAt(moduleIndex);
                if (module == null || !ReferenceEquals(module, target))
                {
                    continue;
                }

                s_TaskClaimCounts[moduleIndex] = s_TaskClaimCounts[moduleIndex] + 1;
                break;
            }
        }

        private static void PublishSnapshot()
        {
            int activeHubCount = 0;
            int dockedStasisSlotCount = s_HeadlessStasisSlotCount;
            int hubCount = Mathf.Min(RepairDroneHub.ActiveHubCount, MaxMainThreadHubScanCount);
            for (int i = 0; i < hubCount; i++)
            {
                RepairDroneHub hub = RepairDroneHub.GetActiveHubAt(i);
                if (hub == null || !hub.isActiveAndEnabled)
                    continue;

                activeHubCount++;
                dockedStasisSlotCount += hub.ResolveDockedStasisSlotCount();
            }

            int activeDroneCount = CountManagedHeadlessDrones();
            int assignedTaskCount = activeDroneCount;

            HectonDroneFleetSnapshot nextSnapshot = new HectonDroneFleetSnapshot(
                activeHubCount,
                activeDroneCount,
                assignedTaskCount,
                dockedStasisSlotCount,
                s_DestroyedDroneCount,
                IsEmergencyOverclockActive,
                s_EmergencyLevel,
                s_LastFleetStatusSnapshot.AverageBattery,
                s_LastFleetStatusSnapshot.SolderReserve,
                s_LastFleetStatusSnapshot.HostileUnits,
                s_LogicLeechHijackCount);

            if (AreSnapshotsEqual(in s_LastSnapshot, in nextSnapshot))
                return;

            s_LastSnapshot = nextSnapshot;
            HectonDroneFleetEvents.RaiseSnapshotUpdated(in nextSnapshot);
        }

        private static bool AreSnapshotsEqual(in HectonDroneFleetSnapshot a, in HectonDroneFleetSnapshot b)
        {
            return a.ActiveHubCount == b.ActiveHubCount &&
                   a.ActiveDroneCount == b.ActiveDroneCount &&
                   a.AssignedTaskCount == b.AssignedTaskCount &&
                   a.DockedStasisSlotCount == b.DockedStasisSlotCount &&
                   a.DestroyedDroneCount == b.DestroyedDroneCount &&
                   a.EmergencyOverclockActive == b.EmergencyOverclockActive &&
                   a.EmergencyLevel == b.EmergencyLevel &&
                   Mathf.Approximately(a.AverageBatteryPercent, b.AverageBatteryPercent) &&
                   a.SolderReserve == b.SolderReserve &&
                   a.HostileDroneCount == b.HostileDroneCount &&
                   a.LogicLeechHijackCount == b.LogicLeechHijackCount;
        }

        private static int GetRuntimeId(Component component)
        {
            return component == null
                ? 0
                : unchecked((int)EntityId.ToULong(component.GetEntityId()));
        }

        private static uint ComputeDroneTaskHash(DroneFleetTaskKind kind, int primaryId, int secondaryId)
        {
            return math.hash(new uint3(
                (uint)math.max(0, (int)kind),
                (uint)math.max(0, primaryId),
                (uint)math.max(0, secondaryId)));
        }

        private static double3 ResolveDroneRenderReferenceAup()
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null)
            {
                if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                    MathGuard.IsFinite(in snapshot.Aup))
                {
                    return snapshot.Aup.ToAbsoluteDouble3();
                }

                var playerMovement = playerContext.PlayerMovement;
                if (playerMovement != null)
                {
                    AbsoluteUniversePosition currentAup = playerMovement.CurrentAup;
                    if (MathGuard.IsFinite(in currentAup))
                        return currentAup.ToAbsoluteDouble3();
                }
            }

            if (TryResolvePlayerPosition(out Vector3 playerPosition))
                return HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(playerPosition);

            if (TryResolveFormationAnchor(out Vector3 formationAnchor))
                return HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(formationAnchor);

            RepairDroneHub hub = RepairDroneHub.GetActiveHubAt(0);
            return HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(hub != null ? hub.DockPosition : Vector3.zero);
        }

        private static Vector3 ResolveDroneRenderReferencePosition()
        {
            Camera camera = Camera.current != null ? Camera.current : Camera.main;
            if (camera != null)
                return camera.transform.position;

            if (TryResolvePlayerPosition(out Vector3 playerPosition))
                return playerPosition;

            if (TryResolveFormationAnchor(out Vector3 formationAnchor))
                return formationAnchor;

            RepairDroneHub hub = RepairDroneHub.GetActiveHubAt(0);
            return hub != null ? hub.DockPosition : Vector3.zero;
        }

        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        private static float3 ToFloat3(double3 value)
        {
            return new float3((float)value.x, (float)value.y, (float)value.z);
        }

        private static double3 ToDouble3(Vector3 value)
        {
            return new double3(value.x, value.y, value.z);
        }

        private static double3 ToDouble3(float3 value)
        {
            return new double3(value.x, value.y, value.z);
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
        }

        private static bool IsFiniteFloat3(float3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
        }

        private static bool IsFiniteDouble3(double3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
        }

        private static float3 ResolveForward(quaternion rotation)
        {
            return NormalizeOrFallback(math.mul(rotation, new float3(0f, 0f, 1f)), new float3(0f, 0f, 1f));
        }

        private static float3 NormalizeOrFallback(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.0001f)
            {
                float fallbackLengthSq = math.lengthsq(fallback);
                return IsFiniteFloat3(fallback) && math.isfinite(fallbackLengthSq) && fallbackLengthSq > 0.0001f
                    ? fallback * math.rsqrt(fallbackLengthSq)
                    : new float3(0f, 0f, 1f);
            }

            return value * math.rsqrt(lengthSq);
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static quaternion ToQuaternion(Quaternion value)
        {
            return new quaternion(value.x, value.y, value.z, value.w);
        }
    }

    public static class DroneFleetAutomationFacade
    {
        public const int MaxDebugRoutes = 64;

        public static bool TryGetTuningConstants(out DroneFleetTuningConstants constants)
        {
            return DroneFleetManager.TryGetDroneFleetTuningConstants(out constants);
        }

        public static void ApplyTuningConstants(in DroneFleetTuningConstants constants)
        {
            DroneFleetManager.ApplyDroneFleetTuningConstants(in constants);
        }

        public static bool TryGetStats(out DroneFleetAutomationStats stats)
        {
            return DroneFleetManager.TryGetDroneFleetAutomationStats(out stats);
        }

        public static int CopyDebugRoutes(DroneFleetDebugRoute[] buffer)
        {
            return DroneFleetManager.CopyDroneFleetDebugRoutes(buffer);
        }

        public static bool TryApplyDroneSpecsCsv(string path, out int keysApplied)
        {
            return DroneFleetManager.TryApplyDroneSpecsCsv(path, out keysApplied);
        }

        public static bool TryAutoApplyDroneSpecsCsv(out int keysApplied)
        {
            return DroneFleetManager.TryAutoApplyDroneSpecsCsv(out keysApplied);
        }
    }
}
