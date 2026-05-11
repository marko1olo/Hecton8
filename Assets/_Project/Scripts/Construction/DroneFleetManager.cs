using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Caves;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.Power;
using Hecton8.SaveSystem;
using Hecton8.World;
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
        CutParasite = 2
    }

    internal readonly struct DroneFleetTask
    {
        public DroneFleetTask(DroneFleetTaskKind kind, BaseModule module, Vector3 position, float radius)
        {
            Kind = kind;
            Module = module;
            Position = position;
            Radius = radius;
        }

        public DroneFleetTaskKind Kind { get; }
        public BaseModule Module { get; }
        public Vector3 Position { get; }
        public float Radius { get; }
        public bool IsValid => Kind != DroneFleetTaskKind.None && Module != null;
    }

    /// <summary>
    /// Read-only fleet snapshot consumed by diagnostics owners such as the submarine OS.
    /// </summary>
    public readonly struct HectonDroneFleetSnapshot
    {
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

        public int ActiveHubCount { get; }
        public int ActiveDroneCount { get; }
        public int AssignedTaskCount { get; }
        public int DockedStasisSlotCount { get; }
        public int DestroyedDroneCount { get; }
        public bool EmergencyOverclockActive { get; }
        public SubmarineEmergencyLevel EmergencyLevel { get; }
        public float AverageBatteryPercent { get; }
        public int SolderReserve { get; }
        public int HostileDroneCount { get; }
        public int LogicLeechHijackCount { get; }
    }

    /// <summary>
    /// Burst-accumulated fleet status payload published to the global telemetry ring and OS bridge.
    /// </summary>
    public readonly struct FleetStatusSnapshot
    {
        public FleetStatusSnapshot(int totalActive, float averageBattery, int solderReserve, int lostUnits, int hostileUnits)
        {
            TotalActive = totalActive;
            AverageBattery = averageBattery;
            SolderReserve = solderReserve;
            LostUnits = lostUnits;
            HostileUnits = hostileUnits;
        }

        public int TotalActive { get; }
        public float AverageBattery { get; }
        public int SolderReserve { get; }
        public int LostUnits { get; }
        public int HostileUnits { get; }
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
    [StructLayout(LayoutKind.Sequential)]
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
    }

    /// <summary>
    /// NativeQueue-backed fleet telemetry bridge drained by <see cref="SystemDispatcher"/>.
    /// </summary>
    public static class HectonDroneFleetEvents
    {
        private const int ListenerCapacity = 8;
        private const int PendingEventCapacity = 64;

        private static readonly uint _overflowWarningHash = unchecked((uint)LocHash.Compute("HectonDroneFleetEvents.Overflow"));
        private static readonly uint _queueHash = unchecked((uint)LocHash.Compute("HectonDroneFleetEvents"));

        // COLD ALLOC: RegistryBucket<IDroneFleetSnapshotEventListener>[8] - fleet snapshot listeners drained by SystemDispatcher LateUpdate - owner: HectonDroneFleetEvents
        private static readonly RegistryBucket<IDroneFleetSnapshotEventListener> _listeners = new RegistryBucket<IDroneFleetSnapshotEventListener>(ListenerCapacity);

        private static NativeQueue<HectonDroneFleetSnapshotPayload> _pendingEvents;
        private static NativeQueue<HectonDroneFleetSnapshotPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;
        private static int _lastOverflowWarningFrame = -1;

        /// <summary>
        /// Number of pending fleet snapshot payloads waiting for late-frame dispatch.
        /// </summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(HectonDroneFleetEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(HectonDroneFleetEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            _pendingEventCount = 0;
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
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out HectonDroneFleetSnapshotPayload payload))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

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

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<HectonDroneFleetSnapshotPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<HectonDroneFleetSnapshotPayload>[64] - deferred drone fleet snapshot lane flushed by SystemDispatcher LateUpdate - owner: HectonDroneFleetEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(HectonDroneFleetEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<HectonDroneFleetSnapshotPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<HectonDroneFleetSnapshotPayload>[64] - next-frame drone fleet snapshot lane prevents same-frame reentrant dispatch - owner: HectonDroneFleetEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(HectonDroneFleetEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
            }
        }

        private static bool Enqueue(in HectonDroneFleetSnapshotPayload payload)
        {
            if (_listeners.Count <= 0)
                return false;

            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportOverflowOncePerFrame();
                return false;
            }

            EnsureInitialized();
            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(payload);
                _nextFrameEventCount++;
                return true;
            }

            _pendingEvents.Enqueue(payload);
            _pendingEventCount++;
            return true;
        }

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)
            where T : unmanaged
        {
            if (!queue.IsCreated || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
            }
        }

        private static void ReportOverflowOncePerFrame()
        {
            int frame = Time.frameCount;
            if (_lastOverflowWarningFrame == frame)
                return;

            _lastOverflowWarningFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(_overflowWarningHash, _queueHash, PendingEventCapacity);
        }

        private static void PromoteNextFrameEventsIfFrontEmpty()
        {
            if (!_pendingEvents.IsCreated ||
                !_nextFrameEvents.IsCreated ||
                _pendingEventCount > 0 ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            NativeQueue<HectonDroneFleetSnapshotPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
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
        private const int HeadlessDroneCapacity = 8;
        private const int PhantomDroneCount = 500;
        private const int PhantomDroneThreadGroupSize = 64;
        private const int HeadlessTaskCapacity = 64;
        private const int HeadlessPendingLaunchCapacity = HeadlessDroneCapacity;
        private const int MaxMainThreadTaskScanCount = 64;
        private const int MaxMainThreadHubScanCount = 8;
        private const int DefaultMaxClaimsPerTarget = 2;
        private const int InvalidHubId = 0;
        private const int EmptyTaskIndex = -1;
        private const string NativeMemoryOwner = nameof(DroneFleetManager);
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
        private const float HeadlessServiceRadiusMeters = 0.9f;
        private const float HeadlessWeldPowerNormalized = 0.75f;
        private const float HeadlessWeldRangeMeters = 1.25f;
        private const float SolderIntegrityUnitsPerPack = 10f;
        private const float OrphanWanderDistanceMeters = 4f;
        private const float DroneFlowDragCoefficient = 0.85f;
        private const int FleetTelemetryPublishFrameInterval = 60;
        private const string DroneCullingComputeAssetPath = "Assets/_Project/Art/Shaders/DroneCulling.compute";
        private const string PhantomDronesComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_PhantomDrones.compute";
        private const string PhantomDronesShaderName = "Hecton8/VFX/PhantomDrones";
        private const float DroneCullRadiusMeters = 1.25f;
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

        [StructLayout(LayoutKind.Sequential)]
        private struct DroneRenderInstance
        {
            public float4x4 Matrix;
            public float TransactionProgress;
            public float3 Padding;
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
        // COLD ALLOC: IndirectDrawIndexedArgs[1] - indirect drone draw argument upload cache - owner: DroneFleetManager
        private static readonly GraphicsBuffer.IndirectDrawIndexedArgs[] s_DroneArgsUpload = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        // COLD ALLOC: IndirectDrawIndexedArgs[1] - indirect phantom drone draw argument upload cache - owner: DroneFleetManager
        private static readonly GraphicsBuffer.IndirectDrawIndexedArgs[] s_PhantomDroneArgsUpload = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        private static NativeArray<int> s_TaskClaimCounts;
        private static NativeArray<HeadlessDroneState> s_DroneStates;
        private static NativeArray<HeadlessDroneState> s_DroneStateBackBuffer;
        private static NativeArray<float4x4> s_DroneRenderMatrices;
        private static NativeArray<float4x4> s_DroneRenderMatrixBackBuffer;
        private static NativeArray<DroneRenderInstance> s_DroneRenderInstances;
        private static NativeArray<int> s_HeadlessTaskClaimOwners;
        private static NativeArray<int> s_FleetTelemetryAccumulator;
        private static NativeParallelMultiHashMap<int, HeadlessDroneTask> s_HeadlessTasksByHub;
        private static NativeParallelMultiHashMap<int, int> s_HeadlessDroneSpatialHash;
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
        private static int s_HeadlessTaskCount;
        private static int s_HeadlessDroneIdSequence;
        private static int s_HeadlessStasisSlotCount;
        private static bool s_Initialized;
        private static bool s_HeadlessDriverRegistered;
        private static bool s_HeadlessJobScheduled;
        private static JobHandle s_HeadlessJobHandle;
        private static bool s_FleetSacrificeRequested;
        private static int s_DestroyedDroneCount;
        private static SubmarineEmergencyLevel s_EmergencyLevel;
        private static HectonDroneFleetSnapshot s_LastSnapshot;
        private static FleetStatusSnapshot s_LastFleetStatusSnapshot;
        private static Mesh s_DroneRenderMesh;
        private static Material s_DroneRenderMaterial;
        private static GraphicsBuffer s_DroneMatrixBuffer;
        private static GraphicsBuffer s_DroneMatrixBufferBackBuffer;
        private static GraphicsBuffer s_DroneStateGpuBuffer;
        private static GraphicsBuffer s_DroneRenderInstanceBuffer;
        private static GraphicsBuffer s_DroneVisibleMatrixBuffer;
        private static GraphicsBuffer s_DroneVisibleInstanceBuffer;
        private static GraphicsBuffer s_DroneVisibleIndexBuffer;
        private static GraphicsBuffer s_DroneArgsBuffer;
        private static ComputeShader s_DroneCullingCompute;
        private static Material s_PhantomDroneMaterial;
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
        private static int s_DroneMatrixUploadBufferIndex;
        private static int s_FleetTelemetryFrameCounter;
        private static int s_LogicLeechHijackCount;
        private static DroneFleetFormationMode s_FleetFormationMode;
        private static bool s_DroneCullingKernelsResolved;
        private static bool s_PhantomDroneKernelResolved;
        private static bool s_PhantomDroneMaterialRuntimeOwned;
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
        private static int PhantomMatricesPropertyId => s_PhantomMatricesPropertyId != 0 ? s_PhantomMatricesPropertyId : (s_PhantomMatricesPropertyId = Shader.PropertyToID("_PhantomMatrices"));
        private static int PhantomColorsPropertyId => s_PhantomColorsPropertyId != 0 ? s_PhantomColorsPropertyId : (s_PhantomColorsPropertyId = Shader.PropertyToID("_PhantomColors"));
        private static int PhantomAnchorPropertyId => s_PhantomAnchorPropertyId != 0 ? s_PhantomAnchorPropertyId : (s_PhantomAnchorPropertyId = Shader.PropertyToID("_PhantomAnchorWS"));
        private static int PhantomTimePropertyId => s_PhantomTimePropertyId != 0 ? s_PhantomTimePropertyId : (s_PhantomTimePropertyId = Shader.PropertyToID("_PhantomTime"));
        private static int PhantomCountPropertyId => s_PhantomCountPropertyId != 0 ? s_PhantomCountPropertyId : (s_PhantomCountPropertyId = Shader.PropertyToID("_PhantomCount"));
        private static int PhantomBaseRadiusPropertyId => s_PhantomBaseRadiusPropertyId != 0 ? s_PhantomBaseRadiusPropertyId : (s_PhantomBaseRadiusPropertyId = Shader.PropertyToID("_PhantomBaseRadius"));
        private static int PhantomVerticalAmplitudePropertyId => s_PhantomVerticalAmplitudePropertyId != 0 ? s_PhantomVerticalAmplitudePropertyId : (s_PhantomVerticalAmplitudePropertyId = Shader.PropertyToID("_PhantomVerticalAmplitude"));
        private static int PhantomScalePropertyId => s_PhantomScalePropertyId != 0 ? s_PhantomScalePropertyId : (s_PhantomScalePropertyId = Shader.PropertyToID("_PhantomScale"));
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
        private static int s_PhantomMatricesPropertyId;
        private static int s_PhantomColorsPropertyId;
        private static int s_PhantomAnchorPropertyId;
        private static int s_PhantomTimePropertyId;
        private static int s_PhantomCountPropertyId;
        private static int s_PhantomBaseRadiusPropertyId;
        private static int s_PhantomVerticalAmplitudePropertyId;
        private static int s_PhantomScalePropertyId;
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
            s_HeadlessJobScheduled = false;
            s_DroneRenderMesh = null;
            s_DroneRenderMaterial = null;
            s_PhantomDronesCompute = null;
            s_DroneRenderLayer = 0;
            s_HeadlessTaskRebuildTimer = 0f;
            s_LastHeadlessDeltaTime = 0f;
            s_PhantomDronePhaseSeconds = 0f;
            s_DroneMatrixUploadBufferIndex = 0;
            s_FleetTelemetryFrameCounter = 0;
            s_LogicLeechHijackCount = 0;
            s_FleetFormationMode = DroneFleetFormationMode.Repair;
            s_DroneCullingCompute = null;
            s_DroneCullingKernelsResolved = false;
            s_PhantomDroneKernelResolved = false;
            s_PhantomDroneMaterialRuntimeOwned = false;
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
            s_PhantomMatricesPropertyId = 0;
            s_PhantomColorsPropertyId = 0;
            s_PhantomAnchorPropertyId = 0;
            s_PhantomTimePropertyId = 0;
            s_PhantomCountPropertyId = 0;
            s_PhantomBaseRadiusPropertyId = 0;
            s_PhantomVerticalAmplitudePropertyId = 0;
            s_PhantomScalePropertyId = 0;

            DisposeNativeArray(ref s_TaskClaimCounts);
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

            MeshFilter meshFilter = dronePrefab.GetComponentInChildren<MeshFilter>(true);
            Renderer renderer = dronePrefab.GetComponentInChildren<Renderer>(true);
            if (meshFilter != null && meshFilter.sharedMesh != null)
                s_DroneRenderMesh = meshFilter.sharedMesh;

            if (renderer != null && renderer.sharedMaterial != null)
                s_DroneRenderMaterial = renderer.sharedMaterial;

            s_DroneRenderLayer = dronePrefab.layer;
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

            if (phantomMaterial != null)
            {
                if (s_PhantomDroneMaterialRuntimeOwned && s_PhantomDroneMaterial != null)
                    DestroyRuntimeObject(s_PhantomDroneMaterial);

                s_PhantomDroneMaterial = phantomMaterial;
                s_PhantomDroneMaterialRuntimeOwned = false;
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
            if (hub == null || !task.IsValid)
                return false;

            EnsureInitialized();
            TryRegisterHeadlessDriver();

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
                    ConsiderTaskCandidate(new RepairTaskCandidate
                    {
                        Kind = DroneFleetTaskKind.RepairModule,
                        Module = module,
                        ModuleIndex = moduleIndex,
                        Position = modulePosition,
                        Radius = 0f,
                        Score = taskScore,
                        CriticalityWeight = taskCriticality
                    }, ref bestTask, ref hasBestTask);
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
                ConsiderTaskCandidate(new RepairTaskCandidate
                {
                    Kind = DroneFleetTaskKind.CutParasite,
                    Module = module,
                    ModuleIndex = moduleIndex,
                    Position = parasiteTarget.Position,
                    Radius = parasiteTarget.Radius,
                    Score = parasiteScore,
                    CriticalityWeight = parasiteCriticality
                }, ref bestTask, ref hasBestTask);
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
            return (1f / clampedDistance) * Mathf.Max(0.1f, criticalityWeight);
        }

        private static float ComputeTaskAssignmentScoreFromDistanceSq(float distanceSq, float criticalityWeight)
        {
            float inverseDistance = math.rsqrt(math.max(MinimumScoreDistanceMetersSq, distanceSq));
            return inverseDistance * math.max(0.1f, criticalityWeight);
        }

        private static void EnsureInitialized()
        {
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

        private static void AllocateHeadlessNativeMemory()
        {
            s_DroneStates = new NativeArray<HeadlessDroneState>(HeadlessDroneCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<HeadlessDroneState>[8] - authoritative real headless drone state pool - owner: DroneFleetManager
            s_DroneStateBackBuffer = new NativeArray<HeadlessDroneState>(HeadlessDroneCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<HeadlessDroneState>[8] - real headless drone write buffer for Burst double buffering - owner: DroneFleetManager
            s_DroneRenderMatrices = new NativeArray<float4x4>(HeadlessDroneCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float4x4>[8] - indirect real drone render front matrices consumed by renderer - owner: DroneFleetManager
            s_DroneRenderMatrixBackBuffer = new NativeArray<float4x4>(HeadlessDroneCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float4x4>[8] - indirect real drone render back matrices written by Burst - owner: DroneFleetManager
            s_DroneRenderInstances = new NativeArray<DroneRenderInstance>(HeadlessDroneCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<DroneRenderInstance>[8] - real drone matrix and transaction-progress upload staging - owner: DroneFleetManager
            s_HeadlessTaskClaimOwners = new NativeArray<int>(HeadlessTaskCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[64] - atomic task claim owners for Burst arbitration - owner: DroneFleetManager
            s_FleetTelemetryAccumulator = new NativeArray<int>((int)DroneFleetTelemetryAccumulatorSlot.Count, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[5] - Burst fleet telemetry accumulator - owner: DroneFleetManager
            s_HeadlessTasksByHub = new NativeParallelMultiHashMap<int, HeadlessDroneTask>(HeadlessTaskCapacity, Allocator.Persistent); // COLD ALLOC: NativeParallelMultiHashMap<int,HeadlessDroneTask>[64] - hub-keyed drone task fanout - owner: DroneFleetManager
            s_HeadlessDroneSpatialHash = new NativeParallelMultiHashMap<int, int>(HeadlessDroneCapacity, Allocator.Persistent); // COLD ALLOC: NativeParallelMultiHashMap<int,int>[8] - real drone boid spatial hash - owner: DroneFleetManager
            RegisterNativeArray(s_DroneStates, nameof(s_DroneStates));
            RegisterNativeArray(s_DroneStateBackBuffer, nameof(s_DroneStateBackBuffer));
            RegisterNativeArray(s_DroneRenderMatrices, nameof(s_DroneRenderMatrices));
            RegisterNativeArray(s_DroneRenderMatrixBackBuffer, nameof(s_DroneRenderMatrixBackBuffer));
            RegisterNativeArray(s_DroneRenderInstances, nameof(s_DroneRenderInstances));
            RegisterNativeArray(s_HeadlessTaskClaimOwners, nameof(s_HeadlessTaskClaimOwners));
            RegisterNativeArray(s_FleetTelemetryAccumulator, nameof(s_FleetTelemetryAccumulator));
            RegisterNativeParallelMultiHashMap(s_HeadlessTasksByHub, nameof(s_HeadlessTasksByHub));
            RegisterNativeParallelMultiHashMap(s_HeadlessDroneSpatialHash, nameof(s_HeadlessDroneSpatialHash));
            s_DroneHubs = new RepairDroneHub[HeadlessDroneCapacity]; // COLD ALLOC: RepairDroneHub[8] - managed hub owner lookup for late-frame service commits - owner: DroneFleetManager
            s_DroneSlotDroneIds = new int[HeadlessDroneCapacity]; // COLD ALLOC: int[8] - managed active drone id slots safe during job execution - owner: DroneFleetManager
            s_DroneSlotDestroyed = new bool[HeadlessDroneCapacity]; // COLD ALLOC: bool[8] - permanently consumed suicide-weld slots - owner: DroneFleetManager
            s_PendingAbortBySlot = new bool[HeadlessDroneCapacity]; // COLD ALLOC: bool[8] - deferred abort control flags - owner: DroneFleetManager
            s_PendingReleaseBySlot = new bool[HeadlessDroneCapacity]; // COLD ALLOC: bool[8] - deferred release control flags - owner: DroneFleetManager
            s_PendingHostileBySlot = new bool[HeadlessDroneCapacity]; // COLD ALLOC: bool[8] - deferred Logic-Leech hijack flags - owner: DroneFleetManager
            s_PendingResupplyGrantBySlot = new bool[HeadlessDroneCapacity]; // COLD ALLOC: bool[8] - command-queue storage commit success acks - owner: DroneFleetManager
            s_PendingResupplyFailureBySlot = new bool[HeadlessDroneCapacity]; // COLD ALLOC: bool[8] - command-queue storage commit failure acks - owner: DroneFleetManager
            s_TargetModulesByDroneSlot = new BaseModule[HeadlessDroneCapacity]; // COLD ALLOC: BaseModule[8] - managed target lookup for late-frame repair application - owner: DroneFleetManager
            s_TargetVoxelVolumesByDroneSlot = new HectonVoxelVolume[HeadlessDroneCapacity]; // COLD ALLOC: HectonVoxelVolume[8] - managed voxel target lookup for weld/carve commits - owner: DroneFleetManager
            s_DroneTaskKindsBySlot = new DroneFleetTaskKind[HeadlessDroneCapacity]; // COLD ALLOC: DroneFleetTaskKind[8] - managed task kind mirror for service application - owner: DroneFleetManager
            s_DronePositions = new Vector3[HeadlessDroneCapacity]; // COLD ALLOC: Vector3[8] - last completed drone positions for non-job contact queries - owner: DroneFleetManager
            s_TaskModuleRefs = new BaseModule[HeadlessTaskCapacity]; // COLD ALLOC: BaseModule[64] - native task index to managed module lookup - owner: DroneFleetManager
            s_TaskVoxelVolumeRefs = new HectonVoxelVolume[HeadlessTaskCapacity]; // COLD ALLOC: HectonVoxelVolume[64] - native task index to managed voxel lookup - owner: DroneFleetManager
            s_TaskKinds = new DroneFleetTaskKind[HeadlessTaskCapacity]; // COLD ALLOC: DroneFleetTaskKind[64] - native task index to managed task kind lookup - owner: DroneFleetManager
            s_PendingLaunches = new PendingDroneLaunch[HeadlessPendingLaunchCapacity]; // COLD ALLOC: PendingDroneLaunch[8] - slow-tick launch queue applied after job completion - owner: DroneFleetManager
        }

        private static void ReleaseHeadlessNativeMemory()
        {
            DisposeNativeArray(ref s_DroneStates);
            DisposeNativeArray(ref s_DroneStateBackBuffer);
            DisposeNativeArray(ref s_DroneRenderMatrices);
            DisposeNativeArray(ref s_DroneRenderMatrixBackBuffer);
            DisposeNativeArray(ref s_DroneRenderInstances);
            DisposeNativeArray(ref s_HeadlessTaskClaimOwners);
            DisposeNativeArray(ref s_FleetTelemetryAccumulator);
            DisposeNativeParallelMultiHashMap(ref s_HeadlessTasksByHub, nameof(s_HeadlessTasksByHub));
            DisposeNativeParallelMultiHashMap(ref s_HeadlessDroneSpatialHash, nameof(s_HeadlessDroneSpatialHash));

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

            DroneCognitionJob job = default;
            job.ReadDrones = s_DroneStates;
            job.Drones = s_DroneStateBackBuffer;
            job.RenderMatrices = s_DroneRenderMatrixBackBuffer;
            job.TasksByGrid = s_HeadlessTasksByHub;
            job.DroneSpatialHash = s_HeadlessDroneSpatialHash;
            job.AbyssalFlowVolume = abyssalFlowVolume;
            job.TaskClaimOwners = s_HeadlessTaskClaimOwners;
            job.TelemetryAccumulator = s_FleetTelemetryAccumulator;
            job.DeltaTime = s_LastHeadlessDeltaTime;
            job.PlayerPosition = ToFloat3(playerPosition);
            job.PlayerPositionValid = hasPlayer ? 1 : 0;
            job.EmergencyOverclock = IsEmergencyOverclockActive ? 1 : 0;
            job.FormationMode = (int)s_FleetFormationMode;
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
            s_HeadlessJobHandle = job.Schedule(HeadlessDroneCapacity, HeadlessDroneCapacity);
            s_HeadlessJobScheduled = true;
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
                NativeArray<float4x4> matrixSwap = s_DroneRenderMatrices;
                s_DroneRenderMatrices = s_DroneRenderMatrixBackBuffer;
                s_DroneRenderMatrixBackBuffer = matrixSwap;
            }

            ApplyPendingControls();
            ApplyCompletedHeadlessServices();
            ApplyPendingLaunches();
            RefreshHeadlessCounters();
            UpdateDrawBounds();
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
                        drone.State = (byte)HeadlessDroneRuntimeState.Travel;
                    }
                }

                if (s_PendingAbortBySlot[slot] && drone.State != (byte)HeadlessDroneRuntimeState.Empty)
                {
                    drone.TargetTaskIndex = EmptyTaskIndex;
                    drone.TargetPosition = drone.HomePosition;
                    drone.State = (byte)HeadlessDroneRuntimeState.Return;
                }

                s_PendingAbortBySlot[slot] = false;
                s_PendingHostileBySlot[slot] = false;
                s_DroneStates[slot] = drone;
            }
        }

        private static void ApplyCompletedHeadlessServices()
        {
            float serviceDt = Mathf.Max(0f, s_LastHeadlessDeltaTime);
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

                if (drone.State == (byte)HeadlessDroneRuntimeState.Completed ||
                    drone.State == (byte)HeadlessDroneRuntimeState.Sacrificed)
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

                    if (drone.FactionBit == (byte)HeadlessDroneFactionBit.Hostile)
                        ApplyHostileHijackService(slot, ref drone, serviceDt);
                    else if (s_DroneTaskKindsBySlot[slot] == DroneFleetTaskKind.CutParasite)
                        ApplyParasiteAttackService(slot, ref drone, serviceDt);
                    else
                        ApplyFriendlyRepairService(slot, ref drone, serviceDt);

                    s_DroneStates[slot] = drone;
                }
            }
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
            drone.SolderUnits += Mathf.Max(1, grantedUnits);
            drone.LoadedSolderCapacity = Mathf.Max(drone.LoadedSolderCapacity, drone.SolderUnits);
            drone.TransactionProgress = 1f;
            drone.State = drone.TargetTaskIndex >= 0
                ? (byte)HeadlessDroneRuntimeState.Travel
                : (byte)HeadlessDroneRuntimeState.Idle;
            drone.Velocity = float3.zero;
        }

        private static void TryQueueStasisWakeRequest(int slot, ref HeadlessDroneState drone)
        {
            RepairDroneHub hub = s_DroneHubs[slot];
            if (hub == null)
                return;

            if (!hub.TryResolveNearestSupplyEndpoint(ToVector3(drone.Position), out Vector3 endpointPosition))
                return;

            drone.SupplyPosition = ToFloat3(endpointPosition);
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
            drone.HomeRotation = ToQuaternion(bestHub.DockRotation);
            drone.TargetTaskIndex = EmptyTaskIndex;
            drone.TargetPosition = drone.HomePosition;
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

            float repairAmount = drone.RepairRatePerSecond * dt;
            float previousIntegrity = target.CurrentIntegrity;
            target.Repair(repairAmount);
            DispatchRepairWeld(slot, in drone, target);

            float repaired = Mathf.Max(0f, target.CurrentIntegrity - previousIntegrity);
            ConsumeSolderByWork(ref drone, repaired, SolderIntegrityUnitsPerPack);

            if (target.CurrentIntegrity >= recoverableIntegrity && !target.IsFlooded && !target.HasCascadeFailure)
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
            float deliveredDamage = Mathf.Max(0.1f, drone.RepairRatePerSecond * dt);
            floraInteractionManager.TryApplyDroneParasiteCut(
                hitPoint,
                direction.sqrMagnitude > SeparationDistanceEpsilon ? direction.normalized : Vector3.down,
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
                    drone.TargetPosition = ToFloat3(playerPosition);
                return;
            }

            float damage = Mathf.Max(0.1f, drone.RepairRatePerSecond * dt);
            target.ApplyDamage(damage);
            DispatchPlasmaCut(slot, in drone, target);
            drone.State = (byte)HeadlessDroneRuntimeState.Attack;
        }

        private static void ExecuteSacrifice(int slot, ref HeadlessDroneState drone, BaseModule target)
        {
            float recoverableIntegrity = Mathf.Max(1f, target.MaxRecoverableIntegrity);
            if (target.CurrentIntegrity < recoverableIntegrity)
                target.Repair(recoverableIntegrity);

            if (target.IsFlooded)
                target.ForceDrainComplete();

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
            int consumedUnits = Mathf.Min(
                drone.SolderUnits,
                Mathf.FloorToInt(drone.RepairAccumulator / safeUnitsPerSolder));
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
            if (weldDirection.sqrMagnitude <= SeparationDistanceEpsilon)
                return;

            Vector3 absoluteHitPoint = HectonFloatingOrigin.ToAbsoluteUniversePosition(dronePosition + (weldDirection.normalized * 0.35f));
            volume.ApplyRepairWeldDda(
                absoluteHitPoint,
                weldDirection.normalized,
                drone.WeldPowerNormalized,
                drone.WeldRangeMeters);
        }

        private static void DispatchPlasmaCut(int slot, in HeadlessDroneState drone, BaseModule target)
        {
            HectonVoxelVolume volume = s_TargetVoxelVolumesByDroneSlot[slot];
            if (volume == null || target == null)
                return;

            Vector3 dronePosition = ToVector3(drone.Position);
            Vector3 targetPosition = target.transform.position;
            Vector3 cutDirection = targetPosition - dronePosition;
            if (cutDirection.sqrMagnitude <= SeparationDistanceEpsilon)
                return;

            Vector3 absoluteHitPoint = HectonFloatingOrigin.ToAbsoluteUniversePosition(dronePosition + (cutDirection.normalized * 0.35f));
            volume.ApplyPlasmaCutDda(
                absoluteHitPoint,
                cutDirection.normalized,
                drone.WeldPowerNormalized,
                drone.WeldRangeMeters);
        }

        private static void ApplyPendingLaunches()
        {
            if (s_PendingLaunchCount <= 0)
                return;

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

                HeadlessDroneState state = new HeadlessDroneState
                {
                    DroneId = launch.DroneId,
                    HubGridId = ResolveHubTaskKey(launch.Hub),
                    HubSlot = slot,
                    TargetTaskIndex = launch.DroneId,
                    TargetModuleId = GetRuntimeId(target),
                    SolderUnits = Mathf.Max(0, launch.LoadedSolderUnits),
                    LoadedSolderCapacity = Mathf.Max(0, launch.LoadedSolderUnits),
                    State = (byte)HeadlessDroneRuntimeState.Travel,
                    FactionBit = (byte)HeadlessDroneFactionBit.Friendly,
                    CorridorTight = ResolveCorridorFlag(launch.HomePosition),
                    BatteryPercent = 100f,
                    RepairAccumulator = 0f,
                    DockingElapsed = 0f,
                    RebootElapsed = 0f,
                    AvoidanceHysteresisSeconds = 0f,
                    TransactionProgress = 0f,
                    ServiceRadius = Mathf.Max(HeadlessServiceRadiusMeters, launch.Task.Radius),
                    MaxSpeed = HeadlessDefaultSpeedMetersPerSecond,
                    BatteryDrainPerSecond = HeadlessBatteryDrainPercentPerSecond,
                    RepairRatePerSecond = launch.RepairRatePerSecond,
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
                    DockStartRotation = homeRotation
                };
                s_DroneStates[slot] = state;
                s_DroneStateBackBuffer[slot] = state;
                s_DroneRenderMatrices[slot] = float4x4.TRS(state.Position, state.Rotation, new float3(1f, 1f, 1f));
                s_DroneRenderMatrixBackBuffer[slot] = s_DroneRenderMatrices[slot];
                s_PendingLaunches[i] = default;
            }

            s_PendingLaunchCount = 0;
        }

        private static void SyncManagedTaskReference(int slot, ref HeadlessDroneState drone)
        {
            int taskIndex = drone.TargetTaskIndex;
            if (taskIndex < 0 || taskIndex >= s_HeadlessTaskCount || taskIndex >= s_TaskModuleRefs.Length)
                return;

            BaseModule module = s_TaskModuleRefs[taskIndex];
            if (module == null)
                return;

            s_TargetModulesByDroneSlot[slot] = module;
            s_TargetVoxelVolumesByDroneSlot[slot] = s_TaskVoxelVolumeRefs[taskIndex];
            s_DroneTaskKindsBySlot[slot] = s_TaskKinds[taskIndex];
            drone.TargetModuleId = GetRuntimeId(module);
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
            s_DroneStates[slot] = default;
            if (s_DroneStateBackBuffer.IsCreated)
                s_DroneStateBackBuffer[slot] = default;
            s_DroneRenderMatrices[slot] = float4x4.zero;
            if (s_DroneRenderMatrixBackBuffer.IsCreated)
                s_DroneRenderMatrixBackBuffer[slot] = float4x4.zero;
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
            for (int i = 0; i < HeadlessDroneCapacity; i++)
            {
                if (s_DroneSlotDestroyed[i])
                    continue;

                if (s_DroneSlotDroneIds[i] <= 0)
                    return i;
            }

            return -1;
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

            s_HeadlessTaskRebuildTimer = HeadlessTaskRebuildIntervalSeconds;
            s_HeadlessTasksByHub.Clear();
            s_HeadlessTaskCount = 0;
            ClearManagedTaskRefs();

            ConstructionManager manager = GlobalRegistry.ConstructionRuntime;
            int moduleCount = manager != null ? manager.SpawnedBaseModuleCount : 0;
            if (moduleCount == 0)
                return;

            int hubCount = Mathf.Min(RepairDroneHub.ActiveHubCount, MaxMainThreadHubScanCount);
            FloraInteractionManager floraInteractionManager = FloraInteractionManager.ActiveRuntimeInstance;
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
            }
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
            s_HeadlessTasksByHub.Add(hubKey, new HeadlessDroneTask
            {
                TaskIndex = taskIndex,
                ModuleId = GetRuntimeId(module),
                HubGridId = hubKey,
                Kind = (byte)kind,
                RequiredFaction = (byte)HeadlessDroneFactionBit.Friendly,
                Criticality = Mathf.Max(0.1f, criticalityWeight),
                Radius = Mathf.Max(HeadlessServiceRadiusMeters, radius),
                Position = ToFloat3(position)
            });
            s_HeadlessTaskCount++;
        }

        private static void BuildHeadlessSpatialHash()
        {
            s_HeadlessDroneSpatialHash.Clear();
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
                s_HeadlessDroneSpatialHash.Add(DroneCognitionJob.PackSpatialKey(drone.Position), i);
            }
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
                ? Mathf.Clamp(batteryMilliPercent / (activeCount * 1000f), 0f, 100f)
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
            Transform playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
            if (playerTransform == null)
                return false;

            position = playerTransform.position;
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
            if (s_DroneRenderMesh == null || s_DroneRenderMaterial == null)
                return;

            if (s_DroneMatrixBuffer == null)
                s_DroneMatrixBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4x4>(HeadlessDroneCapacity); // COLD ALLOC: GraphicsBuffer[8] - real headless drone matrix upload buffer - owner: DroneFleetManager

            if (s_DroneMatrixBufferBackBuffer == null)
                s_DroneMatrixBufferBackBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4x4>(HeadlessDroneCapacity); // COLD ALLOC: GraphicsBuffer[8] - alternate real drone matrix upload buffer for GPU/CPU double-buffering - owner: DroneFleetManager

            if (s_DroneStateGpuBuffer == null)
                s_DroneStateGpuBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<HeadlessDroneState>(HeadlessDroneCapacity); // COLD ALLOC: GraphicsBuffer[8] - real headless drone state upload buffer for GPU culling - owner: DroneFleetManager

            if (s_DroneRenderInstanceBuffer == null)
                s_DroneRenderInstanceBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<DroneRenderInstance>(HeadlessDroneCapacity); // COLD ALLOC: GraphicsBuffer[8] - real drone render instance upload buffer for VAT transaction parameters - owner: DroneFleetManager

            if (s_DroneVisibleMatrixBuffer == null)
                s_DroneVisibleMatrixBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, HeadlessDroneCapacity, UnsafeUtility.SizeOf<float4x4>()); // COLD ALLOC: GraphicsBuffer[8] - GPU-compacted visible real drone matrices - owner: DroneFleetManager

            if (s_DroneVisibleInstanceBuffer == null)
                s_DroneVisibleInstanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, HeadlessDroneCapacity, UnsafeUtility.SizeOf<DroneRenderInstance>()); // COLD ALLOC: GraphicsBuffer[8] - GPU-compacted visible real drone VAT instance data - owner: DroneFleetManager

            if (s_DroneVisibleIndexBuffer == null)
                s_DroneVisibleIndexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, HeadlessDroneCapacity, sizeof(int)); // COLD ALLOC: GraphicsBuffer[8] - visible real drone index append buffer for shader indirection/debug - owner: DroneFleetManager

            if (s_DroneArgsBuffer == null)
                s_DroneArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - headless drone indirect indexed draw arguments - owner: DroneFleetManager

            ResolveDroneCullingKernels();
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

            if (s_DroneArgsBuffer != null)
            {
                s_DroneArgsBuffer.Release();
                s_DroneArgsBuffer = null;
            }
        }

        private static bool EnsurePhantomRenderResources()
        {
            ResolvePhantomDroneKernel();
            if (s_PhantomDronesCompute == null || !s_PhantomDroneKernelResolved)
                return false;

            EnsurePhantomDroneMaterial();
            if (s_DroneRenderMesh == null || s_PhantomDroneMaterial == null)
                return false;

            if (s_PhantomDroneMatrixBuffer == null)
                s_PhantomDroneMatrixBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, PhantomDroneCount, UnsafeUtility.SizeOf<float4x4>()); // COLD ALLOC: GraphicsBuffer[500] - GPU-authored phantom drone matrices - owner: DroneFleetManager

            if (s_PhantomDroneColorBuffer == null)
                s_PhantomDroneColorBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, PhantomDroneCount, UnsafeUtility.SizeOf<float4>()); // COLD ALLOC: GraphicsBuffer[500] - GPU-authored phantom drone emissive colors - owner: DroneFleetManager

            if (s_PhantomDroneArgsBuffer == null)
                s_PhantomDroneArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - phantom drone indirect indexed draw arguments - owner: DroneFleetManager

            s_PhantomDroneArgsUpload[0].indexCountPerInstance = s_DroneRenderMesh.GetIndexCount(0);
            s_PhantomDroneArgsUpload[0].instanceCount = PhantomDroneCount;
            s_PhantomDroneArgsUpload[0].startIndex = s_DroneRenderMesh.GetIndexStart(0);
            s_PhantomDroneArgsUpload[0].baseVertexIndex = (uint)Mathf.Max(0, s_DroneRenderMesh.GetBaseVertex(0));
            s_PhantomDroneArgsUpload[0].startInstance = 0u;
            s_PhantomDroneArgsBuffer.SetData(s_PhantomDroneArgsUpload);
            return true;
        }

        private static void EnsurePhantomDroneMaterial()
        {
            if (s_PhantomDroneMaterial != null)
                return;

            Shader shader = Shader.Find(PhantomDronesShaderName);
            if (shader == null)
                return;

            s_PhantomDroneMaterial = new Material(shader)
            {
                name = "MAT_Runtime_PhantomDrones",
                hideFlags = HideFlags.DontSave,
                enableInstancing = true
            }; // COLD ALLOC: Material[1] - generated phantom drone indirect material - owner: DroneFleetManager
            s_PhantomDroneMaterialRuntimeOwned = true;
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

            if (s_PhantomDroneMaterialRuntimeOwned && s_PhantomDroneMaterial != null)
                DestroyRuntimeObject(s_PhantomDroneMaterial);

            s_PhantomDroneMaterial = null;
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
            if (CountManagedHeadlessDrones() <= 0 || s_DroneRenderMesh == null || s_DroneRenderMaterial == null)
                return;

            EnsureRenderBuffers();
            GraphicsBuffer matrixBuffer = s_DroneMatrixUploadBufferIndex == 0
                ? s_DroneMatrixBuffer
                : s_DroneMatrixBufferBackBuffer;

            if (matrixBuffer == null || s_DroneArgsBuffer == null || !s_DroneRenderMatrices.IsCreated)
                return;

            GraphicsBufferUploadUtility.UploadNativeArray(matrixBuffer, s_DroneRenderMatrices, HeadlessDroneCapacity);
            PrepareDroneRenderInstances();
            if (s_DroneStateGpuBuffer != null && s_DroneStates.IsCreated)
                GraphicsBufferUploadUtility.UploadNativeArray(s_DroneStateGpuBuffer, s_DroneStates, HeadlessDroneCapacity);
            if (s_DroneRenderInstanceBuffer != null && s_DroneRenderInstances.IsCreated)
                GraphicsBufferUploadUtility.UploadNativeArray(s_DroneRenderInstanceBuffer, s_DroneRenderInstances, HeadlessDroneCapacity);

            s_DroneArgsUpload[0].indexCountPerInstance = s_DroneRenderMesh.GetIndexCount(0);
            s_DroneArgsUpload[0].instanceCount = HeadlessDroneCapacity;
            s_DroneArgsUpload[0].startIndex = s_DroneRenderMesh.GetIndexStart(0);
            s_DroneArgsUpload[0].baseVertexIndex = (uint)Mathf.Max(0, s_DroneRenderMesh.GetBaseVertex(0));
            s_DroneArgsUpload[0].startInstance = 0u;
            s_DroneArgsBuffer.SetData(s_DroneArgsUpload);

            if (TryRenderGpuCulledFleet(matrixBuffer))
            {
                s_DroneMatrixUploadBufferIndex ^= 1;
                return;
            }

            s_DroneRenderMaterial.SetBuffer(DroneMatricesPropertyId, matrixBuffer);
            s_DroneRenderMaterial.SetBuffer(InstanceMatricesPropertyId, matrixBuffer);
            if (s_DroneRenderInstanceBuffer != null)
                s_DroneRenderMaterial.SetBuffer(DroneRenderInstancesPropertyId, s_DroneRenderInstanceBuffer);

            RenderParams renderParams = new RenderParams(s_DroneRenderMaterial)
            {
                worldBounds = s_DroneDrawBounds,
                layer = s_DroneRenderLayer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                motionVectorMode = MotionVectorGenerationMode.Camera
            };
            Graphics.RenderMeshIndirect(renderParams, s_DroneRenderMesh, s_DroneArgsBuffer, 1, 0);
            s_DroneMatrixUploadBufferIndex ^= 1;
        }

        private static void RenderPhantomSwarm(float deltaTime)
        {
            if (!TryResolvePhantomAnchor(out Vector3 anchor) || !EnsurePhantomRenderResources())
                return;

            s_PhantomDronePhaseSeconds += Mathf.Max(0f, deltaTime);
            if (s_PhantomDronePhaseSeconds >= PhantomDronePhaseWrapSeconds)
                s_PhantomDronePhaseSeconds = Mathf.Repeat(s_PhantomDronePhaseSeconds, PhantomDronePhaseWrapSeconds);

            s_PhantomDroneDrawBounds = new Bounds(
                anchor,
                new Vector3(
                    PhantomDroneBoundsDiameterMeters,
                    PhantomDroneBoundsDiameterMeters,
                    PhantomDroneBoundsDiameterMeters));

            s_PhantomDronesCompute.SetInt(PhantomCountPropertyId, PhantomDroneCount);
            s_PhantomDronesCompute.SetVector(PhantomAnchorPropertyId, new Vector4(anchor.x, anchor.y, anchor.z, 0f));
            s_PhantomDronesCompute.SetFloat(PhantomTimePropertyId, s_PhantomDronePhaseSeconds);
            s_PhantomDronesCompute.SetFloat(PhantomBaseRadiusPropertyId, PhantomDroneOrbitRadiusMeters);
            s_PhantomDronesCompute.SetFloat(PhantomVerticalAmplitudePropertyId, PhantomDroneVerticalAmplitudeMeters);
            s_PhantomDronesCompute.SetFloat(PhantomScalePropertyId, PhantomDroneScaleMeters);
            s_PhantomDronesCompute.SetBuffer(s_PhantomDroneKernel, PhantomMatricesPropertyId, s_PhantomDroneMatrixBuffer);
            s_PhantomDronesCompute.SetBuffer(s_PhantomDroneKernel, PhantomColorsPropertyId, s_PhantomDroneColorBuffer);
            s_PhantomDronesCompute.Dispatch(
                s_PhantomDroneKernel,
                (PhantomDroneCount + PhantomDroneThreadGroupSize - 1) / PhantomDroneThreadGroupSize,
                1,
                1);

            s_PhantomDroneMaterial.SetBuffer(PhantomMatricesPropertyId, s_PhantomDroneMatrixBuffer);
            s_PhantomDroneMaterial.SetBuffer(PhantomColorsPropertyId, s_PhantomDroneColorBuffer);

            Graphics.DrawMeshInstancedIndirect(
                s_DroneRenderMesh,
                0,
                s_PhantomDroneMaterial,
                s_PhantomDroneDrawBounds,
                s_PhantomDroneArgsBuffer,
                0,
                null,
                ShadowCastingMode.Off,
                false,
                s_DroneRenderLayer,
                Camera.current);
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
                if (s_DroneStates.IsCreated)
                    transactionProgress = Mathf.Clamp01(s_DroneStates[i].TransactionProgress);

                s_DroneRenderInstances[i] = new DroneRenderInstance
                {
                    Matrix = s_DroneRenderMatrices[i],
                    TransactionProgress = transactionProgress,
                    Padding = float3.zero
                };
            }
        }

        private static bool TryRenderGpuCulledFleet(GraphicsBuffer matrixBuffer)
        {
            if (s_DroneCullingCompute == null ||
                !s_DroneCullingKernelsResolved ||
                s_DroneStateGpuBuffer == null ||
                s_DroneRenderInstanceBuffer == null ||
                s_DroneVisibleMatrixBuffer == null ||
                s_DroneVisibleInstanceBuffer == null ||
                s_DroneVisibleIndexBuffer == null ||
                s_DroneArgsBuffer == null)
            {
                return false;
            }

            Camera camera = Camera.current;
            if (camera == null)
                return false;

            GeometryUtility.CalculateFrustumPlanes(camera, s_CullingPlanes);
            for (int i = 0; i < s_CullingPlanes.Length; i++)
            {
                Plane plane = s_CullingPlanes[i];
                Vector3 normal = plane.normal;
                s_CullingPlaneVectors[i] = new Vector4(normal.x, normal.y, normal.z, plane.distance);
            }

            s_DroneVisibleMatrixBuffer.SetCounterValue(0u);
            s_DroneVisibleInstanceBuffer.SetCounterValue(0u);
            s_DroneVisibleIndexBuffer.SetCounterValue(0u);

            s_DroneCullingCompute.SetBuffer(s_DroneClearArgsKernel, IndirectArgsBufferPropertyId, s_DroneArgsBuffer);
            s_DroneCullingCompute.Dispatch(s_DroneClearArgsKernel, 1, 1, 1);

            s_DroneCullingCompute.SetInt(DroneCountPropertyId, HeadlessDroneCapacity);
            s_DroneCullingCompute.SetFloat(DroneCullRadiusPropertyId, DroneCullRadiusMeters);
            s_DroneCullingCompute.SetVectorArray(CameraFrustumPlanesPropertyId, s_CullingPlaneVectors);
            s_DroneCullingCompute.SetBuffer(s_DroneCullKernel, DroneStatesPropertyId, s_DroneStateGpuBuffer);
            s_DroneCullingCompute.SetBuffer(s_DroneCullKernel, DroneMatricesPropertyId, matrixBuffer);
            s_DroneCullingCompute.SetBuffer(s_DroneCullKernel, DroneRenderInstancesPropertyId, s_DroneRenderInstanceBuffer);
            s_DroneCullingCompute.SetBuffer(s_DroneCullKernel, DroneVisibleInstancesPropertyId, s_DroneVisibleInstanceBuffer);
            s_DroneCullingCompute.SetBuffer(s_DroneCullKernel, DroneVisibleIndicesPropertyId, s_DroneVisibleIndexBuffer);
            s_DroneCullingCompute.SetBuffer(s_DroneCullKernel, InstanceMatricesPropertyId, s_DroneVisibleMatrixBuffer);
            int groups = Mathf.CeilToInt(HeadlessDroneCapacity / 64f);
            s_DroneCullingCompute.Dispatch(s_DroneCullKernel, groups, 1, 1);
            GraphicsBuffer.CopyCount(s_DroneVisibleMatrixBuffer, s_DroneArgsBuffer, 4);

            s_DroneRenderMaterial.SetBuffer(DroneMatricesPropertyId, s_DroneVisibleMatrixBuffer);
            s_DroneRenderMaterial.SetBuffer(InstanceMatricesPropertyId, s_DroneVisibleMatrixBuffer);
            s_DroneRenderMaterial.SetBuffer(DroneRenderInstancesPropertyId, s_DroneVisibleInstanceBuffer);
            s_DroneRenderMaterial.SetBuffer(DroneVisibleIndicesPropertyId, s_DroneVisibleIndexBuffer);

            RenderParams renderParams = new RenderParams(s_DroneRenderMaterial)
            {
                worldBounds = s_DroneDrawBounds,
                layer = s_DroneRenderLayer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                motionVectorMode = MotionVectorGenerationMode.Camera
            };
            Graphics.RenderMeshIndirect(renderParams, s_DroneRenderMesh, s_DroneArgsBuffer, 1, 0);
            return true;
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
            float integrity01 = Mathf.Clamp01(module.CurrentIntegrity / recoverableIntegrity);
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
            float integrity01 = Mathf.Clamp01(module.CurrentIntegrity / recoverableIntegrity);
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
                DisposeNativeArray(ref s_TaskClaimCounts);

                int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(requiredCount, InitialTaskCapacity));
                s_TaskClaimCounts = new NativeArray<int>(nextCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[nextCapacity] - per-module active-claim locks for fleet dispatch - owner: DroneFleetManager
                RegisterNativeArray(s_TaskClaimCounts, nameof(s_TaskClaimCounts));
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

        private static void RegisterNativeParallelMultiHashMap<TKey, TValue>(NativeParallelMultiHashMap<TKey, TValue> map, string label)
            where TKey : unmanaged, System.IEquatable<TKey>
            where TValue : unmanaged
        {
            NativeMemorySentinel.RegisterNativeParallelMultiHashMap(map, NativeMemoryOwner, label, NativeMemoryLifetime);
        }

        private static void DisposeNativeParallelMultiHashMap<TKey, TValue>(
            ref NativeParallelMultiHashMap<TKey, TValue> map,
            string label)
            where TKey : unmanaged, System.IEquatable<TKey>
            where TValue : unmanaged
        {
            if (!map.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeParallelMultiHashMap(NativeMemoryOwner, label);
            map.Dispose();
            map = default;
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

        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
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
}
