using System.Collections.Generic;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Power;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Collections;
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
            SubmarineEmergencyLevel emergencyLevel)
        {
            ActiveHubCount = activeHubCount;
            ActiveDroneCount = activeDroneCount;
            AssignedTaskCount = assignedTaskCount;
            DockedStasisSlotCount = dockedStasisSlotCount;
            DestroyedDroneCount = destroyedDroneCount;
            EmergencyOverclockActive = emergencyOverclockActive;
            EmergencyLevel = emergencyLevel;
        }

        public int ActiveHubCount { get; }
        public int ActiveDroneCount { get; }
        public int AssignedTaskCount { get; }
        public int DockedStasisSlotCount { get; }
        public int DestroyedDroneCount { get; }
        public bool EmergencyOverclockActive { get; }
        public SubmarineEmergencyLevel EmergencyLevel { get; }
    }

    /// <summary>
    /// Fleet telemetry bridge. The submarine OS and any diegetic diagnostics can subscribe without scene scans.
    /// </summary>
    public static class HectonDroneFleetEvents
    {
        public delegate void SnapshotUpdatedHandler(in HectonDroneFleetSnapshot snapshot);

        public static event SnapshotUpdatedHandler OnSnapshotUpdated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            OnSnapshotUpdated = null;
        }

        internal static void RaiseSnapshotUpdated(in HectonDroneFleetSnapshot snapshot)
        {
            OnSnapshotUpdated?.Invoke(snapshot);
        }
    }

    /// <summary>
    /// Central zero-alloc fleet arbitration owner for repair drones.
    /// Runtime drone bodies are stored in native state arrays and rendered indirectly.
    /// </summary>
    internal static class DroneFleetManager
    {
        private const int InitialTaskCapacity = 64;
        private const int HeadlessDroneCapacity = 512;
        private const int HeadlessTaskCapacity = 512;
        private const int HeadlessPendingLaunchCapacity = 64;
        private const int DefaultMaxClaimsPerTarget = 2;
        private const int EmptyTaskIndex = -1;
        private const float MinimumScoreDistanceMeters = 0.75f;
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
                RenderHeadlessFleet();
            }
        }

        // COLD ALLOC: HeadlessFleetDriver[1] - registry adapter for headless drone simulation and rendering - owner: DroneFleetManager
        private static readonly HeadlessFleetDriver s_HeadlessDriver = new HeadlessFleetDriver();
        // COLD ALLOC: IndirectDrawIndexedArgs[1] - indirect drone draw argument upload cache - owner: DroneFleetManager
        private static readonly GraphicsBuffer.IndirectDrawIndexedArgs[] s_DroneArgsUpload = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        // COLD ALLOC: RepairTaskCandidate[64] - binary-heap backing store for rupture repair arbitration - owner: DroneFleetManager
        private static RepairTaskCandidate[] s_TaskHeap = new RepairTaskCandidate[InitialTaskCapacity];

        private static NativeArray<int> s_TaskClaimCounts;
        private static NativeArray<HeadlessDroneState> s_DroneStates;
        private static NativeArray<HeadlessDroneState> s_DroneStateBackBuffer;
        private static NativeArray<float4x4> s_DroneRenderMatrices;
        private static NativeArray<int> s_HeadlessTaskClaimOwners;
        private static NativeParallelMultiHashMap<int, HeadlessDroneTask> s_HeadlessTasksByHub;
        private static NativeParallelMultiHashMap<int, int> s_HeadlessDroneSpatialHash;
        private static RepairDroneHub[] s_DroneHubs;
        private static int[] s_DroneSlotDroneIds;
        private static bool[] s_DroneSlotDestroyed;
        private static bool[] s_PendingAbortBySlot;
        private static bool[] s_PendingReleaseBySlot;
        private static bool[] s_PendingHostileBySlot;
        private static BaseModule[] s_TargetModulesByDroneSlot;
        private static HectonVoxelVolume[] s_TargetVoxelVolumesByDroneSlot;
        private static DroneFleetTaskKind[] s_DroneTaskKindsBySlot;
        private static Vector3[] s_DronePositions;
        private static BaseModule[] s_TaskModuleRefs;
        private static HectonVoxelVolume[] s_TaskVoxelVolumeRefs;
        private static DroneFleetTaskKind[] s_TaskKinds;
        private static PendingDroneLaunch[] s_PendingLaunches;
        private static int s_TaskHeapCount;
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
        private static Mesh s_DroneRenderMesh;
        private static Material s_DroneRenderMaterial;
        private static GraphicsBuffer s_DroneMatrixBuffer;
        private static GraphicsBuffer s_DroneArgsBuffer;
        private static Bounds s_DroneDrawBounds = new Bounds(Vector3.zero, new Vector3(2048f, 2048f, 2048f));
        private static int s_DroneRenderLayer;
        private static float s_HeadlessTaskRebuildTimer;
        private static float s_LastHeadlessDeltaTime;

        private static int DroneMatricesPropertyId => s_DroneMatricesPropertyId != 0 ? s_DroneMatricesPropertyId : (s_DroneMatricesPropertyId = Shader.PropertyToID("_DroneMatrices"));
        private static int InstanceMatricesPropertyId => s_InstanceMatricesPropertyId != 0 ? s_InstanceMatricesPropertyId : (s_InstanceMatricesPropertyId = Shader.PropertyToID("_InstanceMatrices"));
        private static int s_DroneMatricesPropertyId;
        private static int s_InstanceMatricesPropertyId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (s_Initialized)
                HectonSubmarineOsEvents.OnSnapshotUpdated -= HandleSubmarineSnapshotUpdated;

            TryUnregisterHeadlessDriver();
            CompletePendingHeadlessJobForReset();
            ReleaseHeadlessNativeMemory();
            ReleaseRenderBuffers();

            s_TaskHeapCount = 0;
            s_PendingLaunchCount = 0;
            s_HeadlessTaskCount = 0;
            s_HeadlessDroneIdSequence = 0;
            s_HeadlessStasisSlotCount = 0;
            s_FleetSacrificeRequested = false;
            s_DestroyedDroneCount = 0;
            s_EmergencyLevel = SubmarineEmergencyLevel.Nominal;
            s_LastSnapshot = default;
            s_Initialized = false;
            s_HeadlessJobScheduled = false;
            s_DroneRenderMesh = null;
            s_DroneRenderMaterial = null;
            s_DroneRenderLayer = 0;
            s_HeadlessTaskRebuildTimer = 0f;
            s_LastHeadlessDeltaTime = 0f;
            s_DroneMatricesPropertyId = 0;
            s_InstanceMatricesPropertyId = 0;

            if (s_TaskClaimCounts.IsCreated)
            {
                s_TaskClaimCounts.Dispose();
                s_TaskClaimCounts = default;
            }
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
            IReadOnlyList<GameObject> modules = manager != null ? manager.SpawnedModules : null;
            if (modules == null || modules.Count == 0)
                return false;

            EnsureTaskCapacity(modules.Count * 2);
            ClearClaimCounts(modules.Count);
            RebuildActiveClaimCounts(modules, modules.Count);
            ResetHeap();

            Vector3 hubPosition = hub.DockPosition;
            PowerGrid hubGrid = hub.CurrentGrid;
            FloraInteractionManager floraInteractionManager = FloraInteractionManager.ActiveRuntimeInstance;

            for (int moduleIndex = 0; moduleIndex < modules.Count; moduleIndex++)
            {
                GameObject moduleObject = modules[moduleIndex];
                if (moduleObject == null ||
                    !moduleObject.activeInHierarchy ||
                    !moduleObject.TryGetComponent(out BaseModule module))
                {
                    continue;
                }

                if (IsEligibleRepairTarget(hubGrid, module, dispatchIntegrityThreshold))
                {
                    float distanceMeters = Vector3.Distance(hubPosition, module.transform.position);
                    float taskCriticality = ResolveCriticalityWeight(module);
                    float taskScore = ComputeTaskAssignmentScore(distanceMeters, taskCriticality);
                    PushTask(new RepairTaskCandidate
                    {
                        Kind = DroneFleetTaskKind.RepairModule,
                        Module = module,
                        ModuleIndex = moduleIndex,
                        Position = module.transform.position,
                        Radius = 0f,
                        Score = taskScore,
                        CriticalityWeight = taskCriticality
                    });
                }

                if (floraInteractionManager == null ||
                    module.ParasiteInfectionLevel <= 0.0001f ||
                    IsDifferentGrid(hubGrid, module) ||
                    !floraInteractionManager.TryResolveNearestModuleParasite(module, hubPosition, out FloraInteractionManager.ModuleParasiteTarget parasiteTarget))
                {
                    continue;
                }

                float parasiteDistanceMeters = Vector3.Distance(hubPosition, parasiteTarget.Position);
                float parasiteCriticality = ResolveParasiteCriticalityWeight(module, in parasiteTarget);
                float parasiteScore = ComputeTaskAssignmentScore(parasiteDistanceMeters, parasiteCriticality);
                PushTask(new RepairTaskCandidate
                {
                    Kind = DroneFleetTaskKind.CutParasite,
                    Module = module,
                    ModuleIndex = moduleIndex,
                    Position = parasiteTarget.Position,
                    Radius = parasiteTarget.Radius,
                    Score = parasiteScore,
                    CriticalityWeight = parasiteCriticality
                });
            }

            while (TryPopTask(out RepairTaskCandidate bestTask))
            {
                if (bestTask.Module == null)
                    continue;

                if (s_TaskClaimCounts[bestTask.ModuleIndex] >= DefaultMaxClaimsPerTarget)
                    continue;

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

        private static void EnsureInitialized()
        {
            if (!s_DroneStates.IsCreated)
                AllocateHeadlessNativeMemory();

            if (!s_Initialized)
            {
                HectonSubmarineOsEvents.OnSnapshotUpdated -= HandleSubmarineSnapshotUpdated;
                HectonSubmarineOsEvents.OnSnapshotUpdated += HandleSubmarineSnapshotUpdated;
                s_Initialized = true;
            }

            TryRegisterHeadlessDriver();
        }

        private static void AllocateHeadlessNativeMemory()
        {
            s_DroneStates = new NativeArray<HeadlessDroneState>(HeadlessDroneCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<HeadlessDroneState>[512] - authoritative headless drone state pool - owner: DroneFleetManager
            s_DroneStateBackBuffer = new NativeArray<HeadlessDroneState>(HeadlessDroneCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<HeadlessDroneState>[512] - headless drone write buffer for Burst double buffering - owner: DroneFleetManager
            s_DroneRenderMatrices = new NativeArray<float4x4>(HeadlessDroneCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float4x4>[512] - indirect drone render matrices - owner: DroneFleetManager
            s_HeadlessTaskClaimOwners = new NativeArray<int>(HeadlessTaskCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[512] - atomic task claim owners for Burst arbitration - owner: DroneFleetManager
            s_HeadlessTasksByHub = new NativeParallelMultiHashMap<int, HeadlessDroneTask>(HeadlessTaskCapacity, Allocator.Persistent); // COLD ALLOC: NativeParallelMultiHashMap<int,HeadlessDroneTask>[512] - hub-keyed drone task fanout - owner: DroneFleetManager
            s_HeadlessDroneSpatialHash = new NativeParallelMultiHashMap<int, int>(HeadlessDroneCapacity, Allocator.Persistent); // COLD ALLOC: NativeParallelMultiHashMap<int,int>[512] - drone boid spatial hash - owner: DroneFleetManager
            s_DroneHubs = new RepairDroneHub[HeadlessDroneCapacity]; // COLD ALLOC: RepairDroneHub[512] - managed hub owner lookup for late-frame service commits - owner: DroneFleetManager
            s_DroneSlotDroneIds = new int[HeadlessDroneCapacity]; // COLD ALLOC: int[512] - managed active drone id slots safe during job execution - owner: DroneFleetManager
            s_DroneSlotDestroyed = new bool[HeadlessDroneCapacity]; // COLD ALLOC: bool[512] - permanently consumed suicide-weld slots - owner: DroneFleetManager
            s_PendingAbortBySlot = new bool[HeadlessDroneCapacity]; // COLD ALLOC: bool[512] - deferred abort control flags - owner: DroneFleetManager
            s_PendingReleaseBySlot = new bool[HeadlessDroneCapacity]; // COLD ALLOC: bool[512] - deferred release control flags - owner: DroneFleetManager
            s_PendingHostileBySlot = new bool[HeadlessDroneCapacity]; // COLD ALLOC: bool[512] - deferred Logic-Leech hijack flags - owner: DroneFleetManager
            s_TargetModulesByDroneSlot = new BaseModule[HeadlessDroneCapacity]; // COLD ALLOC: BaseModule[512] - managed target lookup for late-frame repair application - owner: DroneFleetManager
            s_TargetVoxelVolumesByDroneSlot = new HectonVoxelVolume[HeadlessDroneCapacity]; // COLD ALLOC: HectonVoxelVolume[512] - managed voxel target lookup for weld/carve commits - owner: DroneFleetManager
            s_DroneTaskKindsBySlot = new DroneFleetTaskKind[HeadlessDroneCapacity]; // COLD ALLOC: DroneFleetTaskKind[512] - managed task kind mirror for service application - owner: DroneFleetManager
            s_DronePositions = new Vector3[HeadlessDroneCapacity]; // COLD ALLOC: Vector3[512] - last completed drone positions for non-job contact queries - owner: DroneFleetManager
            s_TaskModuleRefs = new BaseModule[HeadlessTaskCapacity]; // COLD ALLOC: BaseModule[512] - native task index to managed module lookup - owner: DroneFleetManager
            s_TaskVoxelVolumeRefs = new HectonVoxelVolume[HeadlessTaskCapacity]; // COLD ALLOC: HectonVoxelVolume[512] - native task index to managed voxel lookup - owner: DroneFleetManager
            s_TaskKinds = new DroneFleetTaskKind[HeadlessTaskCapacity]; // COLD ALLOC: DroneFleetTaskKind[512] - native task index to managed task kind lookup - owner: DroneFleetManager
            s_PendingLaunches = new PendingDroneLaunch[HeadlessPendingLaunchCapacity]; // COLD ALLOC: PendingDroneLaunch[64] - slow-tick launch queue applied after job completion - owner: DroneFleetManager
        }

        private static void ReleaseHeadlessNativeMemory()
        {
            if (s_DroneStates.IsCreated)
            {
                s_DroneStates.Dispose();
                s_DroneStates = default;
            }

            if (s_DroneStateBackBuffer.IsCreated)
            {
                s_DroneStateBackBuffer.Dispose();
                s_DroneStateBackBuffer = default;
            }

            if (s_DroneRenderMatrices.IsCreated)
            {
                s_DroneRenderMatrices.Dispose();
                s_DroneRenderMatrices = default;
            }

            if (s_HeadlessTaskClaimOwners.IsCreated)
            {
                s_HeadlessTaskClaimOwners.Dispose();
                s_HeadlessTaskClaimOwners = default;
            }

            if (s_HeadlessTasksByHub.IsCreated)
            {
                s_HeadlessTasksByHub.Dispose();
                s_HeadlessTasksByHub = default;
            }

            if (s_HeadlessDroneSpatialHash.IsCreated)
            {
                s_HeadlessDroneSpatialHash.Dispose();
                s_HeadlessDroneSpatialHash = default;
            }

            s_DroneHubs = null;
            s_DroneSlotDroneIds = null;
            s_DroneSlotDestroyed = null;
            s_PendingAbortBySlot = null;
            s_PendingReleaseBySlot = null;
            s_PendingHostileBySlot = null;
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

            bool hasPlayer = TryResolvePlayerPosition(out Vector3 playerPosition);
            DroneCognitionJob job = default;
            job.ReadDrones = s_DroneStates;
            job.Drones = s_DroneStateBackBuffer;
            job.RenderMatrices = s_DroneRenderMatrices;
            job.TasksByGrid = s_HeadlessTasksByHub;
            job.DroneSpatialHash = s_HeadlessDroneSpatialHash;
            job.TaskClaimOwners = s_HeadlessTaskClaimOwners;
            job.DeltaTime = s_LastHeadlessDeltaTime;
            job.PlayerPosition = ToFloat3(playerPosition);
            job.PlayerPositionValid = hasPlayer ? 1 : 0;
            job.EmergencyOverclock = IsEmergencyOverclockActive ? 1 : 0;
            s_HeadlessJobHandle = job.Schedule(HeadlessDroneCapacity, 32);
            s_HeadlessJobScheduled = true;
        }

        private static void CompleteHeadlessSimulationAndApply()
        {
            if (!s_DroneStates.IsCreated)
                return;

            if (s_HeadlessJobScheduled)
            {
                s_HeadlessJobHandle.Complete();
                s_HeadlessJobHandle = default;
                s_HeadlessJobScheduled = false;
                NativeArray<HeadlessDroneState> swap = s_DroneStates;
                s_DroneStates = s_DroneStateBackBuffer;
                s_DroneStateBackBuffer = swap;
            }

            ApplyPendingControls();
            ApplyCompletedHeadlessServices();
            ApplyPendingLaunches();
            RefreshHeadlessCounters();
            UpdateDrawBounds();
            PublishSnapshot();
        }

        private static void CompletePendingHeadlessJobForReset()
        {
            if (!s_HeadlessJobScheduled)
                return;

            s_HeadlessJobHandle.Complete();
            s_HeadlessJobHandle = default;
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
                    continue;
                }

                HeadlessDroneState drone = s_DroneStates[slot];
                if (s_PendingHostileBySlot[slot])
                {
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

                if (drone.State == (byte)HeadlessDroneRuntimeState.ResupplyDocked)
                {
                    ApplyHeadlessResupply(slot, ref drone);
                    s_DroneStates[slot] = drone;
                    continue;
                }

                if (drone.State == (byte)HeadlessDroneRuntimeState.Repair ||
                    drone.State == (byte)HeadlessDroneRuntimeState.Attack)
                {
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
            if (hub == null || !hub.TryAcquireDroneResupply(1, out int grantedUnits))
            {
                drone.State = (byte)HeadlessDroneRuntimeState.Stasis;
                drone.Velocity = float3.zero;
                return;
            }

            drone.SolderUnits += Mathf.Max(1, grantedUnits);
            drone.LoadedSolderCapacity = Mathf.Max(drone.LoadedSolderCapacity, drone.SolderUnits);
            drone.State = drone.TargetTaskIndex >= 0
                ? (byte)HeadlessDroneRuntimeState.Travel
                : (byte)HeadlessDroneRuntimeState.Idle;
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

            float recoverableIntegrity = Mathf.Max(1f, module.MaxRecoverableIntegrity);
            float integrity01 = Mathf.Clamp01(module.CurrentIntegrity / recoverableIntegrity);
            return module.IsBreached || (module.IsFlooded && integrity01 <= 0.2f);
        }

        private static void ConsumeSolderByWork(ref HeadlessDroneState drone, float workAmount, float unitsPerSolder)
        {
            if (workAmount <= 0f || drone.SolderUnits <= 0)
                return;

            drone.RepairAccumulator += workAmount;
            float safeUnitsPerSolder = Mathf.Max(1f, unitsPerSolder);
            while (drone.SolderUnits > 0 && drone.RepairAccumulator >= safeUnitsPerSolder)
            {
                drone.RepairAccumulator -= safeUnitsPerSolder;
                drone.SolderUnits--;
            }
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
                    SupplyPosition = ToFloat3(launch.HomePosition)
                };
                s_DroneStates[slot] = state;
                s_DroneStateBackBuffer[slot] = state;
                s_DroneRenderMatrices[slot] = float4x4.TRS(state.Position, quaternion.identity, new float3(1f, 1f, 1f));
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
            s_PendingAbortBySlot[slot] = false;
            s_PendingReleaseBySlot[slot] = false;
            s_PendingHostileBySlot[slot] = false;

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
            IReadOnlyList<GameObject> modules = manager != null ? manager.SpawnedModules : null;
            if (modules == null || modules.Count == 0)
                return;

            List<RepairDroneHub> hubs = RepairDroneHub.ActiveHubs;
            FloraInteractionManager floraInteractionManager = FloraInteractionManager.ActiveRuntimeInstance;
            for (int hubIndex = 0; hubIndex < hubs.Count; hubIndex++)
            {
                RepairDroneHub hub = hubs[hubIndex];
                if (hub == null || !hub.isActiveAndEnabled)
                    continue;

                int hubKey = ResolveHubTaskKey(hub);
                PowerGrid hubGrid = hub.CurrentGrid;
                Vector3 hubPosition = hub.DockPosition;
                for (int moduleIndex = 0; moduleIndex < modules.Count && s_HeadlessTaskCount < HeadlessTaskCapacity; moduleIndex++)
                {
                    GameObject moduleObject = modules[moduleIndex];
                    if (moduleObject == null ||
                        !moduleObject.activeInHierarchy ||
                        !moduleObject.TryGetComponent(out BaseModule module))
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

        private static byte ResolveCorridorFlag(Vector3 position)
        {
            return VoxelDynamicNavGridRuntime.TrySampleHybridNavigation(position, out VoxelDynamicNavGridRuntime.HybridNavigationSample sample) &&
                   sample.Mode == VoxelDynamicNavGridRuntime.HybridNavigationMode.CaveVoxel
                ? (byte)1
                : (byte)0;
        }

        private static HectonVoxelVolume TryResolveTargetVoxelVolume(BaseModule target)
        {
            if (target == null)
                return null;

            if (target.TryGetComponent(out HectonVoxelVolume localVolume))
                return localVolume;

            return target.GetComponentInParent<HectonVoxelVolume>();
        }

        private static void EnsureRenderBuffers()
        {
            if (s_DroneRenderMesh == null || s_DroneRenderMaterial == null)
                return;

            if (s_DroneMatrixBuffer == null)
                s_DroneMatrixBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4x4>(HeadlessDroneCapacity); // COLD ALLOC: GraphicsBuffer[512] - headless drone matrix upload buffer - owner: DroneFleetManager

            if (s_DroneArgsBuffer == null)
                s_DroneArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - headless drone indirect indexed draw arguments - owner: DroneFleetManager
        }

        private static void ReleaseRenderBuffers()
        {
            if (s_DroneMatrixBuffer != null)
            {
                s_DroneMatrixBuffer.Release();
                s_DroneMatrixBuffer = null;
            }

            if (s_DroneArgsBuffer != null)
            {
                s_DroneArgsBuffer.Release();
                s_DroneArgsBuffer = null;
            }
        }

        private static void RenderHeadlessFleet()
        {
            if (CountManagedHeadlessDrones() <= 0 || s_DroneRenderMesh == null || s_DroneRenderMaterial == null)
                return;

            EnsureRenderBuffers();
            if (s_DroneMatrixBuffer == null || s_DroneArgsBuffer == null || !s_DroneRenderMatrices.IsCreated)
                return;

            GraphicsBufferUploadUtility.UploadNativeArray(s_DroneMatrixBuffer, s_DroneRenderMatrices, HeadlessDroneCapacity);
            s_DroneArgsUpload[0].indexCountPerInstance = s_DroneRenderMesh.GetIndexCount(0);
            s_DroneArgsUpload[0].instanceCount = HeadlessDroneCapacity;
            s_DroneArgsUpload[0].startIndex = s_DroneRenderMesh.GetIndexStart(0);
            s_DroneArgsUpload[0].baseVertexIndex = (uint)Mathf.Max(0, s_DroneRenderMesh.GetBaseVertex(0));
            s_DroneArgsUpload[0].startInstance = 0u;
            s_DroneArgsBuffer.SetData(s_DroneArgsUpload);

            s_DroneRenderMaterial.SetBuffer(DroneMatricesPropertyId, s_DroneMatrixBuffer);
            s_DroneRenderMaterial.SetBuffer(InstanceMatricesPropertyId, s_DroneMatrixBuffer);

            RenderParams renderParams = new RenderParams(s_DroneRenderMaterial)
            {
                worldBounds = s_DroneDrawBounds,
                layer = s_DroneRenderLayer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                motionVectorMode = MotionVectorGenerationMode.Camera
            };
            Graphics.RenderMeshIndirect(renderParams, s_DroneRenderMesh, s_DroneArgsBuffer, 1, 0);
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

            if (!module.TryGetComponent(out PowerNode modulePowerNode) || modulePowerNode.Grid == null)
                return false;

            return !ReferenceEquals(modulePowerNode.Grid, hubGrid);
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

            if (s_TaskHeap == null || s_TaskHeap.Length < requiredCount)
            {
                int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(requiredCount, InitialTaskCapacity));
                s_TaskHeap = new RepairTaskCandidate[nextCapacity]; // COLD ALLOC: RepairTaskCandidate[nextCapacity] - fleet repair-task max-heap storage - owner: DroneFleetManager
            }

            if (!s_TaskClaimCounts.IsCreated || s_TaskClaimCounts.Length < requiredCount)
            {
                if (s_TaskClaimCounts.IsCreated)
                    s_TaskClaimCounts.Dispose();

                int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(requiredCount, InitialTaskCapacity));
                s_TaskClaimCounts = new NativeArray<int>(nextCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[nextCapacity] - per-module active-claim locks for fleet dispatch - owner: DroneFleetManager
            }
        }

        private static void ClearClaimCounts(int moduleCount)
        {
            for (int i = 0; i < moduleCount; i++)
                s_TaskClaimCounts[i] = 0;
        }

        private static void RebuildActiveClaimCounts(IReadOnlyList<GameObject> modules, int moduleCount)
        {
            if (s_DroneSlotDroneIds != null)
            {
                for (int slot = 0; slot < s_DroneSlotDroneIds.Length; slot++)
                {
                    if (s_DroneSlotDroneIds[slot] <= 0)
                        continue;

                    IncrementClaimForTarget(modules, moduleCount, s_TargetModulesByDroneSlot[slot]);
                }
            }

            if (s_PendingLaunches != null)
            {
                for (int i = 0; i < s_PendingLaunchCount; i++)
                {
                    if (!s_PendingLaunches[i].Active)
                        continue;

                    IncrementClaimForTarget(modules, moduleCount, s_PendingLaunches[i].Task.Module);
                }
            }
        }

        private static void IncrementClaimForTarget(IReadOnlyList<GameObject> modules, int moduleCount, BaseModule target)
        {
            if (target == null)
                return;

            for (int moduleIndex = 0; moduleIndex < moduleCount; moduleIndex++)
            {
                GameObject moduleObject = modules[moduleIndex];
                if (moduleObject == null ||
                    !moduleObject.TryGetComponent(out BaseModule module) ||
                    !ReferenceEquals(module, target))
                {
                    continue;
                }

                s_TaskClaimCounts[moduleIndex] = s_TaskClaimCounts[moduleIndex] + 1;
                break;
            }
        }

        private static void ResetHeap()
        {
            s_TaskHeapCount = 0;
        }

        private static void PushTask(in RepairTaskCandidate candidate)
        {
            int index = s_TaskHeapCount++;
            s_TaskHeap[index] = candidate;
            while (index > 0)
            {
                int parent = (index - 1) >> 1;
                if (s_TaskHeap[parent].Score >= s_TaskHeap[index].Score)
                    break;

                RepairTaskCandidate swap = s_TaskHeap[parent];
                s_TaskHeap[parent] = s_TaskHeap[index];
                s_TaskHeap[index] = swap;
                index = parent;
            }
        }

        private static bool TryPopTask(out RepairTaskCandidate candidate)
        {
            if (s_TaskHeapCount <= 0)
            {
                candidate = default;
                return false;
            }

            candidate = s_TaskHeap[0];
            s_TaskHeapCount--;
            if (s_TaskHeapCount <= 0)
                return true;

            s_TaskHeap[0] = s_TaskHeap[s_TaskHeapCount];
            int index = 0;
            while (true)
            {
                int left = (index << 1) + 1;
                if (left >= s_TaskHeapCount)
                    break;

                int right = left + 1;
                int bestChild = right < s_TaskHeapCount && s_TaskHeap[right].Score > s_TaskHeap[left].Score
                    ? right
                    : left;

                if (s_TaskHeap[index].Score >= s_TaskHeap[bestChild].Score)
                    break;

                RepairTaskCandidate swap = s_TaskHeap[index];
                s_TaskHeap[index] = s_TaskHeap[bestChild];
                s_TaskHeap[bestChild] = swap;
                index = bestChild;
            }

            return true;
        }

        private static void PublishSnapshot()
        {
            int activeHubCount = 0;
            int dockedStasisSlotCount = s_HeadlessStasisSlotCount;
            List<RepairDroneHub> hubs = RepairDroneHub.ActiveHubs;
            for (int i = 0; i < hubs.Count; i++)
            {
                RepairDroneHub hub = hubs[i];
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
                s_EmergencyLevel);

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
                   a.EmergencyLevel == b.EmergencyLevel;
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
    }
}
