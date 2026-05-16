using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Core.Memory
{
    /// <summary>
    /// Stable native-memory owner identifiers. Values below 256 are reserved for registry service slots.
    /// </summary>
    public enum SystemID : ushort
    {
        Unknown = 0,
        CoreDataVault = 1,
        H8Memory = 2,
        Bootstrap = 3,
        CoreDeterminism = 4,
        SystemDispatcher = 30,
        HardwareHomeostasis = 31,
        GlobalPhysicsStateManager = 32,
        Physics = 64,
        VehiclesPhysics = 65,
        Fluid = 66,
        GameplayLoot = 67,
        HabitatAtmosphere = 68,
        GameplayPlayer = 69,
        GameplayTools = 70,
        WorldStreaming = 128,
        TerrainSeams = 129,
        WorldSargassum = 130,
        AICognition = 144,
        AnimationFauna = 145,
        AIPathfinding = 146,
        AIEcology = 147,
        AISensory = 148,
        SimulationBucketer = 161,
        AmbientBiota = 162,
        Vfx = 192,
        GraphicsScalability = 193,
        ContentAuthority = 194,
        UI = 224,
        External = 65534
    }

    /// <summary>
    /// Allocation-free global data-vault buffer identifiers.
    /// </summary>
    public enum BufferID : int
    {
        Unknown = 0,
        Silt = 1,
        RigidbodyAUPs = 2,
        RigidbodyCullingState = 3,
        RigidbodyAwakeResults = 4,
        RigidbodyCullingCommands = 5,
        RigidbodyDistanceSq = 6,
        PhysicsCullingTelemetry = 7,
        DispatcherRaycastHits = 8,
        H8Time = 9,
        TerrainSeamHeightmap = 10,
        PlayerKinematicState = 11,
        RoomWaterLevels = 12,
        EntityAUPs = 13,
        VoxelSdfTexture3D = 14,
        RoomVolumes = 15,
        RoomLocalAUPs = 16,
        OceanGerstnerWaves = 17,
        OceanGerstnerWaveMeta = 18,
        WfcOutpostGrid = 19,
        LoreEntityAUPs = 20,
        LoreEntityHashes = 21,
        SubmarineBallastFill01 = 22,
        SubmarineBallastTankLocalPositions = 23,
        SubmarineBallastPidOutput = 24,
        SubmarineDynamicFloodMassOutput = 25,
        SubmarinePidTelemetry = 26,
        CarveDebris = 27,
        CarveDebrisVelocity = 28,
        EntityFlags = 29,
        EntityVelocities = 30,
        EntityItemHashes = 31,
        EntityQuantities = 32,
        EntityLootMagnetTelemetry = 33,
        EntityLootMagnetSignalEvents = 34,
        SubmarineFluidCompartmentFloodVolumes = 35,
        SubmarineFluidCompartmentViscosity01 = 36,
        SubmarineFluidCompartmentBaseMaxVolumes = 37,
        SubmarineFluidCompartmentMaxVolumes = 38,
        SubmarineFluidCompartmentBreachAreas = 39,
        SubmarineFluidCompartmentLocalCentroids = 40,
        SubmarineFluidCompartmentFlags = 41,
        SubmarineFluidBulkheadPairs = 42,
        SubmarineFluidBulkheadSealed = 43,
        SubmarineFluidBulkheadDoorAreas = 44,
        SubmarineFluidComAccumulatorFront = 45,
        SubmarineFluidComAccumulatorBack = 46,
        SubmarineFluidMassPropertiesFront = 47,
        SubmarineFluidMassPropertiesBack = 48,
        SubmarineFluidAngularVelocityHistoryLocal = 49,
        SubmarineFluidPreviousExteriorSampleSubmersionFactors = 50,
        SubmarineFluidJobFloodVolumes = 51,
        SubmarineFluidJobCompartmentFlags = 52,
        SubmarineFluidBulkheadTransferDeltas = 53,
        SubmarineHydroKinematicInput = 54,
        SubmarineHydroKinematicOutput = 55,
        SubmarineHydroBlackBox = 56,
        PlayerKinematicPositions = 57,
        PlayerKinematicVelocities = 58,
        PlayerKinematicIntendedMovements = 59,
        PlayerKinematicDragSolvedVelocities = 60,
        PlayerKinematicTelemetryRing = 61,
        PlayerCinematicFocusBlackBox = 62,
        HabitatBaseAwakeState = 63,
        CarveDebrisJobState = 64,
        CarveDebrisRequests = 65,
        CarveDebrisBlackBox = 66,
        TetherCablePositions = 67,
        TetherCablePreviousPositions = 68,
        TetherCableVelocities = 69,
        TetherCableMasses = 70,
        TetherCableSegmentTensions = 71,
        TetherCableBlackBox = 72,
        FloraScatterMatrices = 73,
        FloraScatterMetadata = 74,
        FloraScatterMotionVectors = 75,
        HullDents = 76,
        CompassState = 77,
        CompassHeadingOutput = 78,
        CompassBlackBox = 79,
        JawIkTargets = 80,
        CurrentJawPos = 81,
        BiteIkSolveEvents = 82,
        BiteIkTelemetryCursor = 83,
        WakeSources = 84,
        AlphaLeviathanCognitionState = 85,
        AlphaLeviathanSensoryStimulus = 86,
        AlphaLeviathanSteeringOutput = 87,
        AlphaLeviathanTelemetryRing = 88,
        AlphaLeviathanTelemetryCursor = 89,
        ResolutionScaleState = 90,
        BiotaAUPs = 91,
        BiotaVelocities = 92,
        BiotaStates = 93,
        ToolRuntimeHeat01 = 94,
        ToolRuntimeBatteryCharge = 95,
        WfcDoorCutProgress01 = 96,
        WfcLaserCutBlackBox = 97,
        SimulationBucketEntityFront = 98,
        SimulationBucketEntityWork = 99,
        SimulationBucketEntityCostEwma = 100,
        SimulationBucketLoadEwma = 101,
        SimulationBucketRebalanceResult = 102,
        SimulationBucketFrameState = 103,
        SimulationBucketRebalanceLoads = 104,
        SargassumStaticObstacleCache = 105,
        SargassumBoidState = 106,
        SargassumLeviathanPathScratch = 107,
        SargassumLeviathanNodeFront = 108,
        SargassumLeviathanNodeBack = 109,
        SargassumLeviathanNodeCount = 110,
        SargassumFoveatedSimulationInput = 111,
        SargassumFoveatedSimulationFront = 112,
        SargassumFoveatedSimulationBack = 113,
        SargassumSimulationFrame = 114,
        SargassumFoodChainTelemetryRing = 115,
        SargassumThreatGridUpload = 116,
        SargassumThreatVoxelUpload = 117,
        SargassumInactiveSwarmRing = 118,
        SargassumInactiveSwarmCenterRing = 119,
        SargassumBoidSensoryThreats = 120,
        LadderAUPs = 121,
        HardwareMetrics = 122,
        ShaderGlobalState = 123,
        HandTargetAUP = 124,
        HandActualAUP = 125,
        HandGrabState = 126,
        HandIkTelemetryRing = 127,
        HandIkTelemetryCursor = 128,
        FoveatedRenderBlackBox = 129,
        PredatorRetinalExposure = 130,
        PredatorRetinalBlindnessState = 131,
        PredatorRetinalLastPublishedBlindnessState = 132,
        PredatorRetinalLightSources = 133,
        PredatorRetinalTelemetryRing = 134,
        LockstepArrayHashes = 135,
        LockstepMasterStateHash = 136,
        LockstepMasterFlags = 137,
        LockstepTelemetryRing = 138,
        LockstepReplayInputRing = 139,
        LockstepRigidbodyElementHashes = 140,
        LockstepPlayerElementHashes = 141,
        LockstepRoomElementHashes = 142,
        LockstepEntityElementHashes = 143,
        LockstepRigidbodyElementFlags = 144,
        LockstepPlayerElementFlags = 145,
        LockstepRoomElementFlags = 146,
        LockstepEntityElementFlags = 147,
        LockstepGhostReplayHeaders = 148,
        LockstepGhostReplayInputs = 149,
        VehicleDockingActiveSplines = 150,
        VisorRefractionBlackBox = 151,
        WakeGlobalBuffer = 152,
        WakeVectorBuffer = 153,
        WakeBlackBox = 154,
        LadderClimbIkInput = 155,
        LadderClimbIkOutput = 156,
        LadderClimbIkTelemetryRing = 157,
        LadderClimbIkTelemetryCursor = 158,
        BiotaTelemetryRing = 159,
        BiotaTelemetryCursor = 160,
        FloraScatterBlackBox = 161,
        FloraScatterCpuFrustumPlanes = 162,
        FloraScatterCpuVisibilityMask = 163,
        HardwareFrameTimes = 164,
        HomeostasisBlackBox = 165,
        HardwareThermalSeverity = 166,
        HardwareThermalBlackBox = 167,
        PlayerKinematicFlowVelocity = 168,
        PlayerKinematicLastValidPositions = 169,
        PlayerKinematicSyncReadState = 170,
        PlayerKinematicSyncWriteState = 171,
        PlayerKinematicHandTargets = 172,
        PlayerKinematicSmoothedHandTargets = 173,
        PlayerKinematicRuntimeTelemetryRing = 174,
        PlayerKinematicRuntimeTelemetryCursor = 175,
        PlayerKinematicFaultFlags = 176,
        PlayerKinematicHandProbeCommands = 177,
        PlayerKinematicHandProbeHits = 178,
        PlayerKinematicSdfSqueezeResults = 179,
        LeviathanSegmentPositions = 180,
        LeviathanPreviousSegmentPositions = 181,
        LeviathanBoneMatrices = 182,
        LeviathanTerrainIkTelemetryRing = 183,
        LeviathanTerrainIkTelemetryCursor = 184,
        SargassumBoidSensoryBlackBox = 185,
        PlayerMotorScheduledSweepCommands = 186,
        PlayerMotorScheduledSweepResults = 187,
        PlayerMotorKinematicRepairTargetCommands = 188,
        PlayerMotorKinematicRepairTargetResults = 189,
        HandPresenceInput = 190,
        HandPresenceOutput = 191,
        LockstepMasterHashHistory = 192,
        LockstepMasterHashHistoryCursor = 193,
        SimulationBucketBlackBox = 194,
        PathFunnelActivePaths = 195,
        PathFunnelCellMasks = 196,
        PathFunnelInvalidations = 197,
        PathFunnelTelemetryRing = 198,
        PathFunnelRuntimeState = 199,
        BiolumProfileFloats = 200,
        BiolumGlobalStates = 201,
        BiolumBlackBox = 202,
        SubmarineStructuralBreaches = 203,
        SubmarineDamageControlBlackBox = 204,
        EcosystemPopulationCoefficients = 205,
        EcosystemPopulationSectorState = 206,
        EcosystemPopulationCullEvents = 207,
        EcosystemPopulationTelemetryRing = 208,
        EcosystemPopulationFreeRing = 209,
        EcosystemPopulationCounters = 210,
        ResolutionScaleTelemetry = 211,
        TetherCableBlackBoxHead = 212,
        MarineSnowWakeJobResult = 213,
        MarineSnowTelemetryRing = 214,
        EcosystemMacroSwarms = 215,
        EcosystemMacroSwarmArrivals = 216,
        EcosystemMacroSwarmCounters = 217,
        EcosystemMacroSwarmBlackBox = 218,
        EcosystemMacroSwarmMutationRadiation = 219,
        EcosystemMacroSwarmMutationToxicity = 220,
        EcosystemMacroSwarmMutationBrine = 221,
        EcosystemMacroSwarmMutationResults = 222,
        EcosystemMacroHydrationScratch = 223,
        EcosystemMacroDehydrationScratch = 224,
        BiotaMacroHydrationCounters = 225,
        ContentAuthorityBlackBox = 226,
        ContentAuthorityTelemetryCursor = 227,
        WakeTrailStampCommands = 228,
        AcousticEchoFrameTaps = 229,
        AcousticEchoTrailState = 230,
        AcousticEchoBlackBox = 231,
        TetherManagerBlackBox = 232,
        TetherManagerBlackBoxHead = 233
    }

    [Flags]
    public enum H8AllocationFlags : ushort
    {
        None = 0,
        NativeArray = 1 << 0,
        Raw = 1 << 1,
        Vault = 1 << 2,
        Alias = 1 << 3,
        Freed = 1 << 4,
        SubAllocatorRoot = 1 << 6
    }

    public enum H8BlockState : byte
    {
        Free = 0,
        Occupied = 1
    }

    [Flags]
    internal enum H8MemoryTelemetryFlags : ushort
    {
        None = 0,
        Initialized = 1 << 0,
        Allocated = 1 << 1,
        Released = 1 << 2,
        ForcedRelease = 1 << 3,
        SceneTransition = 1 << 4,
        BaselineMismatch = 1 << 5,
        Shutdown = 1 << 6,
        Fault = 1 << 7,
        Heartbeat = 1 << 8
    }

    /// <summary>
    /// Native memory-map descriptor for occupied/free regions owned by <see cref="H8Memory"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 40)]
    public struct BlockDescriptor
    {
        public IntPtr BasePointer;
        public long OffsetBytes;
        public long Bytes;
        public int OwnerKey;
        public int Generation;
        public SystemID Owner;
        public ushort Flags;
        public byte State;
        public byte Reserved;
        public ushort Reserved2;
    }

    /// <summary>
    /// Blittable record copied to crash dumps and leak-reap passes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]
    public struct H8AllocationRecord
    {
        public IntPtr Pointer;
        public long Bytes;
        public int Length;
        public int Stride;
        public int Alignment;
        public int AllocationIndex;
        public int Generation;
        public Allocator Allocator;
        public SystemID Owner;
        public ushort Flags;
        public ushort Reserved;
        public ushort Reserved2;
    }

    /// <summary>
    /// Fixed-size sentinel heartbeat copied into fatal memory dumps.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
    public struct H8MemoryTelemetryEntry
    {
        public long TotalBytes;
        public long TransitionBaselineBytes;
        public long LastTransitionReleasedBytes;
        public uint Sequence;
        public int ActiveAllocationCount;
        public int BlockDescriptorCount;
        public int AllocationGeneration;
        public int TransitionCutoffGeneration;
        public int TransitionSequence;
        public int LastTransitionReleasedCount;
        public int FatalLeakPreventedCount;
        public ushort Owner;
        public ushort Flags;
        public uint Frame;
    }

    public sealed class FatalMemoryException : InvalidOperationException
    {
        private FatalMemoryException(string message) : base(message)
        {
        }

        public static void ThrowUnknownFreeOwner()
        {
            throw new FatalMemoryException("H8Memory free owner is unknown.");
        }

        public static void ThrowUnknownAllocationOwner()
        {
            throw new FatalMemoryException("H8Memory allocation owner is unknown.");
        }

        public static void ThrowUnknownAliasReader()
        {
            throw new FatalMemoryException("H8Memory alias reader is unknown.");
        }

        public static void ThrowWrongFreeOwner()
        {
            throw new FatalMemoryException("H8Memory free owner mismatch.");
        }

        public static void ThrowUntrackedPointer()
        {
            throw new FatalMemoryException("H8Memory free pointer is untracked.");
        }

        public static void ThrowStaleVaultHandle()
        {
            throw new FatalMemoryException("GlobalDataVault handle generation mismatch.");
        }

        public static void ThrowVaultTypeMismatch()
        {
            throw new FatalMemoryException("GlobalDataVault buffer type mismatch.");
        }

        public static void ThrowAllocationSizeMismatch()
        {
            throw new FatalMemoryException("H8Memory reallocation size mismatch.");
        }

        public static void ThrowAllocationTrackingFailed()
        {
            throw new FatalMemoryException("H8Memory allocation tracking failed.");
        }

        public static void ThrowAbiLayoutMismatch()
        {
            throw new FatalMemoryException("H8Memory ABI layout mismatch.");
        }
    }

    /// <summary>
    /// Zero-managed-hot-path memory sentinel for native allocations.
    /// </summary>
    public static unsafe class H8Memory
    {
        private const int DefaultCapacity = 4096;
        private const int MaxTrackingCapacity = 65536;
        private const int OwnerByteSlots = 65536;
        private const int OwnerRegistryCapacity = 256;
        private const int DefaultOwnerPointerCapacity = 16;
        private const int BlackBoxFrameCount = 300;
        private const int MinimumRawAlignment = 16;
        private const int MaximumRawAlignment = 4096;
        private const long LowTierPoolCapBytes = 512L * 1024L * 1024L;
        private const int NoTransitionCutoffGeneration = -1;
        private const int BlockDescriptorSizeBytes = 40;
        private const int H8AllocationRecordSizeBytes = 48;
        private const int H8MemoryTelemetryEntrySizeBytes = 64;
        private const string AgentDumpFileName = "Dump_SENTINEL_DISPOSAL_GUARD.bin";

        private static NativeParallelHashMap<long, SystemID> _allocationOwners;
        private static NativeParallelHashMap<long, int> _allocationRecordIndices;
        private static NativeParallelHashMap<ushort, NativeList<IntPtr>> _ownerPointers;
        private static NativeParallelHashMap<ushort, JobHandle> _ownerJobHandles;
        private static NativeList<ushort> _ownerPointerKeys;
        private static NativeList<ushort> _ownerJobKeys;
        private static NativeArray<H8AllocationRecord> _records;
        private static NativeArray<long> _ownerBytes;
        private static NativeList<BlockDescriptor> _blockDescriptors;
        private static NativeArray<H8MemoryTelemetryEntry> _blackBox;
        private static NativeArray<H8MemoryTelemetryEntry> _eventBlackBox;
        private static int _recordCount;
        private static long _totalBytes;
        private static long _poolCapBytes = LowTierPoolCapBytes;
        private static int _fatalLeakPreventedCount;
        private static int _blackBoxCursor;
        private static int _eventBlackBoxCursor;
        private static uint _blackBoxSequence;
        private static uint _eventBlackBoxSequence;
        private static int _allocationGeneration = 1;
        private static int _transitionCutoffGeneration = NoTransitionCutoffGeneration;
        private static int _transitionSequence;
        private static int _lastTransitionReleasedCount;
        private static long _lastTransitionReleasedBytes;
        private static long _transitionBaselineBytes;
        private static bool _lastTransitionBaselineVerified = true;
        private static bool _sceneHooksRegistered;
        private static bool _initialized;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private static AtomicSafetyHandle _aliasSafetyHandle;
        private static bool _aliasSafetyHandleCreated;
#endif

        /// <summary>Tracked allocation count.</summary>
        public static int ActiveAllocationCount => _recordCount;

        /// <summary>Total tracked bytes.</summary>
        public static long TotalBytes => _totalBytes;

        /// <summary>Total tracked bytes. Scene-transition verification uses this alias.</summary>
        public static long TotalAllocatedBytes => _totalBytes;

        /// <summary>Tracked memory-map descriptor count.</summary>
        public static int BlockDescriptorCount => _blockDescriptors.IsCreated ? _blockDescriptors.Length : 0;

        /// <summary>Configured native pool cap in bytes.</summary>
        public static long PoolCapBytes => _poolCapBytes;

        /// <summary>Number of owner-unregister leaks force-reaped by the sentinel.</summary>
        public static int FatalLeakPreventedCount => _fatalLeakPreventedCount;

        /// <summary>True while a scene transition generation cutoff is awaiting verification.</summary>
        public static bool HasPendingSceneTransition => _transitionCutoffGeneration != NoTransitionCutoffGeneration;

        /// <summary>True when the last scene transition purge reached the exact computed baseline.</summary>
        public static bool LastTransitionBaselineVerified => _lastTransitionBaselineVerified;

        /// <summary>Bytes released by the last scene transition leak purge.</summary>
        public static long LastTransitionReleasedBytes => _lastTransitionReleasedBytes;

        /// <summary>Allocation records released by the last scene transition leak purge.</summary>
        public static int LastTransitionReleasedCount => _lastTransitionReleasedCount;

        /// <summary>
        /// Records the per-frame memory sentinel heartbeat into the fixed 300-entry blackbox ring.
        /// </summary>
        public static void RecordHeartbeat()
        {
            if (!_initialized)
                return;

            RecordBlackBox(SystemID.H8Memory, H8MemoryTelemetryFlags.Heartbeat);
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorShutdownHooks()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            UnityEditor.EditorApplication.quitting -= Shutdown;
            UnityEditor.EditorApplication.playModeStateChanged -= HandleEditorPlayModeStateChanged;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            UnityEditor.EditorApplication.quitting += Shutdown;
            UnityEditor.EditorApplication.playModeStateChanged += HandleEditorPlayModeStateChanged;
        }

        private static void HandleEditorPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                Shutdown();
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForSubsystemRegistration()
        {
            Shutdown();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterSceneHooksAfterSceneLoad()
        {
            RegisterSceneHooks();
        }

        /// <summary>
        /// Initializes native tracking tables. Safe to call more than once.
        /// </summary>
        public static void Initialize(int capacity = DefaultCapacity, long poolCapBytes = LowTierPoolCapBytes)
        {
            if (_initialized)
                return;

            ValidateAbiLayout();
            int safeCapacity = ResolveTrackingCapacity(capacity);
            // COLD ALLOC: NativeParallelHashMap<long,SystemID>[capacity] - pointer to owner registry - owner: H8Memory
            _allocationOwners = new NativeParallelHashMap<long, SystemID>(safeCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeParallelHashMap<long,int>[capacity] - pointer to allocation record index - owner: H8Memory
            _allocationRecordIndices = new NativeParallelHashMap<long, int>(safeCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeParallelHashMap<ushort,NativeList<IntPtr>>[256] - SystemID value to allocation pointer registry - owner: H8Memory
            _ownerPointers = new NativeParallelHashMap<ushort, NativeList<IntPtr>>(OwnerRegistryCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeParallelHashMap<ushort,JobHandle>[256] - SystemID value teardown job fences - owner: H8Memory
            _ownerJobHandles = new NativeParallelHashMap<ushort, JobHandle>(OwnerRegistryCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeList<ushort>[256] - SystemID value owner pointer registry keys for deterministic disposal - owner: H8Memory
            _ownerPointerKeys = new NativeList<ushort>(OwnerRegistryCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeList<ushort>[256] - SystemID value job fence registry keys for deterministic shutdown - owner: H8Memory
            _ownerJobKeys = new NativeList<ushort>(OwnerRegistryCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeArray<H8AllocationRecord>[capacity] - allocation table for leak reaping - owner: H8Memory
            _records = new NativeArray<H8AllocationRecord>(safeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<long>[65536] - bytes per SystemID slot - owner: H8Memory
            _ownerBytes = new NativeArray<long>(OwnerByteSlots, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeList<BlockDescriptor>[capacity] - native memory map descriptors - owner: H8Memory
            _blockDescriptors = new NativeList<BlockDescriptor>(safeCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeArray<H8MemoryTelemetryEntry>[300] - sentinel heartbeat ring - owner: H8Memory
            _blackBox = new NativeArray<H8MemoryTelemetryEntry>(BlackBoxFrameCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<H8MemoryTelemetryEntry>[300] - lifecycle snapshots for leak dumps - owner: H8Memory
            _eventBlackBox = new NativeArray<H8MemoryTelemetryEntry>(BlackBoxFrameCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _recordCount = 0;
            _totalBytes = 0L;
            _poolCapBytes = poolCapBytes > 0L ? poolCapBytes : LowTierPoolCapBytes;
            _fatalLeakPreventedCount = 0;
            _blackBoxCursor = 0;
            _eventBlackBoxCursor = 0;
            _blackBoxSequence = 0u;
            _eventBlackBoxSequence = 0u;
            _allocationGeneration = 1;
            _transitionCutoffGeneration = NoTransitionCutoffGeneration;
            _transitionSequence = 0;
            _lastTransitionReleasedCount = 0;
            _lastTransitionReleasedBytes = 0L;
            _transitionBaselineBytes = 0L;
            _lastTransitionBaselineVerified = true;
            _initialized = true;
            RegisterSceneHooks();
            RecordBlackBox(SystemID.H8Memory, H8MemoryTelemetryFlags.Initialized);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            _aliasSafetyHandle = AtomicSafetyHandle.Create();
            _aliasSafetyHandleCreated = true;
#endif
        }

        private static void ValidateAbiLayout()
        {
            if (UnsafeUtility.SizeOf<BlockDescriptor>() != BlockDescriptorSizeBytes ||
                UnsafeUtility.SizeOf<H8AllocationRecord>() != H8AllocationRecordSizeBytes ||
                UnsafeUtility.SizeOf<H8MemoryTelemetryEntry>() != H8MemoryTelemetryEntrySizeBytes)
            {
                FatalMemoryException.ThrowAbiLayoutMismatch();
            }
        }

        /// <summary>
        /// Applies the bootstrap memory ceiling after hardware classification without reallocating tracking tables.
        /// </summary>
        public static void ConfigurePoolCap(long poolCapBytes)
        {
            if (poolCapBytes <= 0L)
                poolCapBytes = LowTierPoolCapBytes;

            if (!_initialized)
            {
                Initialize(DefaultCapacity, poolCapBytes);
                return;
            }

            if (poolCapBytes >= _totalBytes)
                _poolCapBytes = poolCapBytes;
        }

        /// <summary>
        /// Allocates a native array and records its owner before it can be exposed to jobs.
        /// </summary>
        public static NativeArray<T> Allocate<T>(
            int length,
            SystemID owner,
            Allocator allocator,
            NativeArrayOptions options = NativeArrayOptions.ClearMemory) where T : struct
        {
            if (!_initialized)
                Initialize();

            if (length <= 0)
                return default;
            if (owner == SystemID.Unknown)
                FatalMemoryException.ThrowUnknownAllocationOwner();

            int stride = UnsafeUtility.SizeOf<T>();
            long bytes = (long)stride * length;
            if (!TryReserveBytes(owner, bytes) || !EnsureTrackingCapacity())
                return default;

            NativeArray<T> array = new NativeArray<T>(length, allocator, options);
            void* pointer = NativeArrayUnsafeUtility.GetUnsafePtr(array);
            if (!RegisterPointer(pointer, bytes, length, stride, UnsafeUtility.AlignOf<T>(), owner, allocator, H8AllocationFlags.NativeArray))
            {
                array.Dispose();
                FatalMemoryException.ThrowAllocationTrackingFailed();
                return default;
            }

            return array;
        }

        /// <summary>
        /// Releases a native array allocated by <see cref="Allocate{T}"/> and removes it from the leak tracker.
        /// </summary>
        [Obsolete("Use Release(ref NativeArray<T>, SystemID) so tracked memory is freed by its recorded owner.", true)]
        public static void Release<T>(ref NativeArray<T> array) where T : struct
        {
            Release(ref array, SystemID.Unknown);
        }

        /// <summary>
        /// Releases a native array only when the caller matches the recorded allocation owner.
        /// </summary>
        public static void Release<T>(ref NativeArray<T> array, SystemID owner) where T : struct
        {
            if (!array.IsCreated)
                return;
            if (owner == SystemID.Unknown)
                FatalMemoryException.ThrowUnknownFreeOwner();
            if (!_initialized)
                FatalMemoryException.ThrowUntrackedPointer();

            void* pointer = NativeArrayUnsafeUtility.GetUnsafePtr(array);
            UnregisterPointer(pointer, owner);
            array.Dispose();
            array = default;
        }

        /// <summary>
        /// Defers native-array disposal behind an active job dependency and retires leak ownership immediately.
        /// </summary>
        [Obsolete("Use Release(ref NativeArray<T>, JobHandle, SystemID) so tracked memory is freed by its recorded owner.", true)]
        public static JobHandle Release<T>(ref NativeArray<T> array, JobHandle dependency) where T : struct
        {
            return Release(ref array, dependency, SystemID.Unknown);
        }

        /// <summary>
        /// Defers native-array disposal behind an active job dependency when the caller matches the recorded owner.
        /// </summary>
        public static JobHandle Release<T>(ref NativeArray<T> array, JobHandle dependency, SystemID owner) where T : struct
        {
            if (!array.IsCreated)
                return dependency;
            if (owner == SystemID.Unknown)
                FatalMemoryException.ThrowUnknownFreeOwner();
            if (!_initialized)
                FatalMemoryException.ThrowUntrackedPointer();

            void* pointer = NativeArrayUnsafeUtility.GetUnsafePtr(array);
            UnregisterPointer(pointer, owner);
            JobHandle disposeHandle = array.Dispose(dependency);
            array = default;
            return disposeHandle;
        }

        /// <summary>
        /// Records an owner job fence so forced teardown can block only at scene-transition/owner-destruction boundaries.
        /// </summary>
        /// <param name="owner">Native allocation owner.</param>
        /// <param name="handle">Active job handle touching owner memory.</param>
        public static void RegisterActiveJob(SystemID owner, JobHandle handle)
        {
            if (owner == SystemID.Unknown)
                FatalMemoryException.ThrowUnknownAllocationOwner();
            if (!_initialized)
                Initialize();

            if (!_ownerJobHandles.IsCreated)
                return;

            ushort ownerKey = GetOwnerKey(owner);
            if (_ownerJobHandles.TryGetValue(ownerKey, out JobHandle existingHandle))
            {
                _ownerJobHandles[ownerKey] = JobHandle.CombineDependencies(existingHandle, handle);
                return;
            }

            if (!_ownerJobKeys.IsCreated || !_ownerJobHandles.TryAdd(ownerKey, handle))
                FatalMemoryException.ThrowAllocationTrackingFailed();

            _ownerJobKeys.Add(ownerKey);
        }

        /// <summary>
        /// Captures a generation cutoff before scene loading can allocate new Ocean memory.
        /// </summary>
        public static void BeginSceneTransitionPurge()
        {
            if (!_initialized)
                Initialize();

            int cutoffGeneration = _allocationGeneration;
            _allocationGeneration = AdvanceDescriptorGeneration(_allocationGeneration);
            _transitionCutoffGeneration = cutoffGeneration;
            _transitionBaselineBytes = ComputeSceneTransitionBaselineBytes(cutoffGeneration);
            _lastTransitionReleasedCount = 0;
            _lastTransitionReleasedBytes = 0L;
            _lastTransitionBaselineVerified = false;
            _transitionSequence = AdvanceDescriptorGeneration(_transitionSequence);
            RecordBlackBox(SystemID.H8Memory, H8MemoryTelemetryFlags.SceneTransition);
        }

        /// <summary>
        /// Completes scene-transition leak purging and validates that tracked bytes reached the captured baseline.
        /// </summary>
        /// <returns>True when the exact transition baseline was reached.</returns>
        public static bool CompleteSceneTransitionVerification()
        {
            if (!_initialized)
                return true;

            if (_transitionCutoffGeneration == NoTransitionCutoffGeneration)
                return _lastTransitionBaselineVerified;

            ReleaseSceneTransitionLeaks();
            bool verified = _totalBytes == _transitionBaselineBytes;
            _lastTransitionBaselineVerified = verified;
            if (!verified)
                WriteFatalLeakBlackBox(SystemID.Unknown, 0, _totalBytes - _transitionBaselineBytes, baselineMismatch: true);
            else
                RecordBlackBox(SystemID.H8Memory, H8MemoryTelemetryFlags.SceneTransition);

            _transitionCutoffGeneration = NoTransitionCutoffGeneration;
            return verified;
        }

        /// <summary>
        /// Force-releases every allocation owned by one system after completing its registered job fence.
        /// </summary>
        /// <param name="owner">Owner to purge.</param>
        /// <returns>Number of force-released allocations.</returns>
        public static int ReleaseAll(SystemID owner)
        {
            if (!_initialized || owner == SystemID.Unknown)
                return 0;

            return ReleaseAll(owner, int.MaxValue, writeBlackBox: true);
        }

        /// <summary>
        /// Allocates raw native memory for vault-owned buffers.
        /// </summary>
        public static void* AllocateRaw(
            long bytes,
            int alignment,
            SystemID owner,
            Allocator allocator,
            bool clearMemory,
            H8AllocationFlags extraFlags = H8AllocationFlags.None)
        {
            if (!_initialized)
                Initialize();

            if (bytes <= 0L)
                return null;
            if (owner == SystemID.Unknown)
                FatalMemoryException.ThrowUnknownAllocationOwner();

            int safeAlignment = ResolveSafeAlignment(alignment);
            if (!TryReserveBytes(owner, bytes) || !EnsureTrackingCapacity())
                return null;

            void* pointer = UnsafeUtility.Malloc(bytes, safeAlignment, allocator);
            if (pointer == null)
                return null;

            if (clearMemory)
                UnsafeUtility.MemClear(pointer, bytes);

            if (!RegisterPointer(pointer, bytes, 0, 0, safeAlignment, owner, allocator, H8AllocationFlags.Raw | extraFlags))
            {
                UnsafeUtility.Free(pointer, allocator);
                FatalMemoryException.ThrowAllocationTrackingFailed();
                return null;
            }

            return pointer;
        }

        /// <summary>
        /// Reallocates a raw vault buffer with copy/free semantics and refreshed sentinel ownership.
        /// </summary>
        public static void* ReallocateRaw(
            void* oldPointer,
            long oldBytes,
            long newBytes,
            int alignment,
            SystemID owner,
            Allocator allocator,
            bool clearExtendedBytes,
            H8AllocationFlags extraFlags = H8AllocationFlags.None)
        {
            if (!_initialized)
                Initialize();

            if (newBytes <= 0L)
                return null;
            if (owner == SystemID.Unknown)
                FatalMemoryException.ThrowUnknownAllocationOwner();

            if (oldPointer == null)
                return AllocateRaw(newBytes, alignment, owner, allocator, clearExtendedBytes, extraFlags);

            long trackedOldBytes = ValidateTrackedPointerOwner(oldPointer, owner);
            if (oldBytes > 0L && oldBytes != trackedOldBytes)
                FatalMemoryException.ThrowAllocationSizeMismatch();

            int safeAlignment = ResolveSafeAlignment(alignment);
            if (!TryReserveReplacementBytes(trackedOldBytes, newBytes) || !EnsureTrackingCapacity())
                return null;

            void* newPointer = UnsafeUtility.Malloc(newBytes, safeAlignment, allocator);
            if (newPointer == null)
                return null;

            long copyBytes = trackedOldBytes < newBytes ? trackedOldBytes : newBytes;
            UnsafeUtility.MemMove(newPointer, oldPointer, copyBytes);
            if (clearExtendedBytes && newBytes > copyBytes)
                UnsafeUtility.MemClear((byte*)newPointer + copyBytes, newBytes - copyBytes);

            if (!RegisterPointer(newPointer, newBytes, 0, 0, safeAlignment, owner, allocator, H8AllocationFlags.Raw | extraFlags))
            {
                UnsafeUtility.Free(newPointer, allocator);
                FatalMemoryException.ThrowAllocationTrackingFailed();
                return null;
            }

            UnregisterPointer(oldPointer, owner);
            UnsafeUtility.Free(oldPointer, allocator);

            return newPointer;
        }

        /// <summary>
        /// Legacy raw free entry point. Tracked memory must use the owner-tagged overload.
        /// </summary>
        [Obsolete("Use FreeRaw(pointer, allocator, SystemID) so tracked memory is freed by its recorded owner.", true)]
        public static void FreeRaw(void* pointer, Allocator allocator)
        {
            FreeRaw(pointer, allocator, SystemID.Unknown);
        }

        /// <summary>
        /// Frees raw native memory only when the caller matches the recorded allocation owner.
        /// </summary>
        public static void FreeRaw(void* pointer, Allocator allocator, SystemID requester)
        {
            if (pointer == null)
                return;
            if (requester == SystemID.Unknown)
                FatalMemoryException.ThrowUnknownFreeOwner();
            if (!_initialized)
                FatalMemoryException.ThrowUntrackedPointer();

            UnregisterPointer(pointer, requester);
            UnsafeUtility.Free(pointer, allocator);
        }

        /// <summary>
        /// Creates a read-only alias over an existing buffer without copying.
        /// </summary>
        public static NativeArray<T>.ReadOnly CreateAlias<T>(NativeArray<T> source, SystemID reader) where T : struct
        {
            if (reader == SystemID.Unknown)
                FatalMemoryException.ThrowUnknownAliasReader();

            if (!source.IsCreated)
                return default;

            return source.AsReadOnly();
        }

        /// <summary>
        /// Creates a read-only alias over raw vault memory without copying.
        /// </summary>
        internal static NativeArray<T>.ReadOnly CreateAlias<T>(void* pointer, int length, SystemID reader) where T : struct
        {
            if (reader == SystemID.Unknown)
                FatalMemoryException.ThrowUnknownAliasReader();

            NativeArray<T> array = CreateNativeArrayView<T>(pointer, length);
            return array.AsReadOnly();
        }

        /// <summary>
        /// Converts owned raw memory into a NativeArray view.
        /// </summary>
        internal static NativeArray<T> CreateNativeArrayView<T>(void* pointer, int length) where T : struct
        {
            if (pointer == null || length <= 0)
                return default;

            NativeArray<T> array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(pointer, length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (_aliasSafetyHandleCreated)
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, _aliasSafetyHandle);
#endif
            return array;
        }

        /// <summary>
        /// Force-frees all tracked memory for an unregistered owner.
        /// </summary>
        public static int ReapOwnerLeaks(SystemID owner)
        {
            return ReleaseAll(owner);
        }

        /// <summary>
        /// Dumps the current allocation table to a text file for post-mortem triage.
        /// </summary>
        public static bool DumpAllocationTableText(string path)
        {
            if (!_initialized || string.IsNullOrEmpty(path))
                return false;

            using (StreamWriter writer = new StreamWriter(path, false))
            {
                writer.WriteLine("H8MEMORY_ALLOCATION_TABLE");
                writer.Write("TotalBytes=");
                writer.WriteLine(_totalBytes);
                writer.Write("ActiveAllocationCount=");
                writer.WriteLine(_recordCount);
                for (int i = 0; i < _recordCount; i++)
                {
                    H8AllocationRecord record = _records[i];
                    writer.Write("Index=");
                    writer.Write(record.AllocationIndex);
                    writer.Write(" Ptr=");
                    writer.Write(record.Pointer.ToInt64());
                    writer.Write(" Bytes=");
                    writer.Write(record.Bytes);
                    writer.Write(" Owner=");
                    writer.Write((int)record.Owner);
                    writer.Write(" Allocator=");
                    writer.Write((int)record.Allocator);
                    writer.Write(" Flags=");
                    writer.WriteLine(record.Flags);
                }
            }

            return true;
        }

        /// <summary>
        /// Registers or reuses a memory-map descriptor slot. Cold path only.
        /// </summary>
        public static int RegisterBlockDescriptor(in BlockDescriptor descriptor)
        {
            if (!_initialized)
                Initialize();

            return RegisterBlockDescriptorNoInit(in descriptor);
        }

        /// <summary>
        /// Updates a memory-map descriptor in-place.
        /// </summary>
        public static bool TryUpdateBlockDescriptor(int index, in BlockDescriptor descriptor)
        {
            if (!_initialized || !_blockDescriptors.IsCreated || (uint)index >= (uint)_blockDescriptors.Length)
                return false;

            _blockDescriptors[index] = descriptor;
            return true;
        }

        /// <summary>
        /// Reads a memory-map descriptor without allocation.
        /// </summary>
        public static bool TryGetBlockDescriptor(int index, out BlockDescriptor descriptor)
        {
            descriptor = default;
            if (!_initialized || !_blockDescriptors.IsCreated || (uint)index >= (uint)_blockDescriptors.Length)
                return false;

            descriptor = _blockDescriptors[index];
            return true;
        }

        /// <summary>
        /// Shuts down tracking tables. Only call from service shutdown after users released their buffers.
        /// </summary>
        public static void Shutdown()
        {
            UnregisterSceneHooks();
            if (!_initialized)
                return;

            CompleteAllOwnerJobs();

            for (int i = _recordCount - 1; i >= 0; i--)
            {
                H8AllocationRecord record = _records[i];
                if (record.Pointer != IntPtr.Zero)
                    UnsafeUtility.Free(record.Pointer.ToPointer(), record.Allocator);
            }

            _recordCount = 0;
            _totalBytes = 0L;
            RecordBlackBox(SystemID.H8Memory, H8MemoryTelemetryFlags.Shutdown);
            DisposeOwnerPointerLists();
            if (_allocationOwners.IsCreated)
                _allocationOwners.Dispose();
            if (_allocationRecordIndices.IsCreated)
                _allocationRecordIndices.Dispose();
            if (_ownerPointers.IsCreated)
                _ownerPointers.Dispose();
            if (_ownerJobHandles.IsCreated)
                _ownerJobHandles.Dispose();
            if (_ownerPointerKeys.IsCreated)
                _ownerPointerKeys.Dispose();
            if (_ownerJobKeys.IsCreated)
                _ownerJobKeys.Dispose();
            if (_records.IsCreated)
                _records.Dispose();
            if (_ownerBytes.IsCreated)
                _ownerBytes.Dispose();
            if (_blockDescriptors.IsCreated)
                _blockDescriptors.Dispose();
            if (_blackBox.IsCreated)
                _blackBox.Dispose();
            if (_eventBlackBox.IsCreated)
                _eventBlackBox.Dispose();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (_aliasSafetyHandleCreated)
            {
                AtomicSafetyHandle.Release(_aliasSafetyHandle);
                _aliasSafetyHandleCreated = false;
            }
#endif
            _allocationGeneration = 1;
            _transitionCutoffGeneration = NoTransitionCutoffGeneration;
            _transitionSequence = 0;
            _lastTransitionReleasedCount = 0;
            _lastTransitionReleasedBytes = 0L;
            _transitionBaselineBytes = 0L;
            _lastTransitionBaselineVerified = true;
            _blackBoxCursor = 0;
            _eventBlackBoxCursor = 0;
            _blackBoxSequence = 0u;
            _eventBlackBoxSequence = 0u;
            _initialized = false;
        }

        private static void RegisterSceneHooks()
        {
            if (_sceneHooksRegistered)
                return;

            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            _sceneHooksRegistered = true;
        }

        private static void UnregisterSceneHooks()
        {
            if (!_sceneHooksRegistered)
                return;

            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            _sceneHooksRegistered = false;
        }

        private static void HandleSceneUnloaded(Scene scene)
        {
            CompleteSceneTransitionVerification();
        }

        private static int ReleaseAll(SystemID owner, int generationCutoff, bool writeBlackBox)
        {
            if (!_initialized || owner == SystemID.Unknown)
                return 0;

            CompleteOwnerJobs(owner);
            if (!_ownerPointers.IsCreated ||
                !_ownerPointers.TryGetValue(GetOwnerKey(owner), out NativeList<IntPtr> pointers) ||
                !pointers.IsCreated ||
                pointers.Length == 0)
            {
                return 0;
            }

            int releasedCount = 0;
            long releasedBytes = 0L;
            for (int pointerIndex = pointers.Length - 1; pointerIndex >= 0; pointerIndex--)
            {
                IntPtr pointer = pointers[pointerIndex];
                if (pointer == IntPtr.Zero)
                {
                    pointers.RemoveAtSwapBack(pointerIndex);
                    continue;
                }

                if (!TryFindRecordIndex(pointer, out int recordIndex))
                {
                    pointers.RemoveAtSwapBack(pointerIndex);
                    continue;
                }

                H8AllocationRecord record = _records[recordIndex];
                if (record.Owner != owner)
                {
                    pointers.RemoveAtSwapBack(pointerIndex);
                    continue;
                }

                if (generationCutoff != int.MaxValue && record.Generation > generationCutoff)
                    continue;

                if (ForceFreeRecordAt(recordIndex, removeOwnerPointer: false, out H8AllocationRecord releasedRecord))
                {
                    releasedCount++;
                    releasedBytes += releasedRecord.Bytes;
                }

                pointers.RemoveAtSwapBack(pointerIndex);
            }

            _ownerPointers[GetOwnerKey(owner)] = pointers;
            if (releasedCount <= 0)
                return 0;

            _fatalLeakPreventedCount += releasedCount;
            if (writeBlackBox)
                WriteFatalLeakBlackBox(owner, releasedCount, releasedBytes, baselineMismatch: false);

            return releasedCount;
        }

        private static int ReleaseSceneTransitionLeaks()
        {
            int cutoffGeneration = _transitionCutoffGeneration;
            if (cutoffGeneration == NoTransitionCutoffGeneration)
                return 0;

            CompleteSceneTransitionOwnerJobs();
            int releasedCount = 0;
            long releasedBytes = 0L;
            for (int index = _recordCount - 1; index >= 0; index--)
            {
                H8AllocationRecord record = _records[index];
                if (!IsSceneTransitionRecord(in record, cutoffGeneration))
                    continue;

                if (!ForceFreeRecordAt(index, removeOwnerPointer: true, out H8AllocationRecord releasedRecord))
                    continue;

                releasedCount++;
                releasedBytes += releasedRecord.Bytes;
            }

            _lastTransitionReleasedCount = releasedCount;
            _lastTransitionReleasedBytes = releasedBytes;
            if (releasedCount <= 0)
                return 0;

            _fatalLeakPreventedCount += releasedCount;
            WriteFatalLeakBlackBox(SystemID.Unknown, releasedCount, releasedBytes, baselineMismatch: false);
            return releasedCount;
        }

        private static void CompleteOwnerJobs(SystemID owner)
        {
            if (!_ownerJobHandles.IsCreated || owner == SystemID.Unknown)
                return;

            ushort ownerKey = GetOwnerKey(owner);
            if (!_ownerJobHandles.TryGetValue(ownerKey, out JobHandle ownerHandle))
                return;

            // [BLOCKING_SYNC_POINT] Scene transition and owner teardown may wait; gameplay Tick paths may not call this.
            ownerHandle.Complete();
            _ownerJobHandles.Remove(ownerKey);
            RemoveOwnerJobKey(ownerKey);
        }

        private static void CompleteAllOwnerJobs()
        {
            if (!_ownerJobHandles.IsCreated || !_ownerJobKeys.IsCreated)
                return;

            for (int i = 0; i < _ownerJobKeys.Length; i++)
            {
                ushort ownerKey = _ownerJobKeys[i];
                if (!_ownerJobHandles.TryGetValue(ownerKey, out JobHandle ownerHandle))
                    continue;

                // [BLOCKING_SYNC_POINT] Shutdown may wait; gameplay Tick paths may not call this.
                ownerHandle.Complete();
                _ownerJobHandles.Remove(ownerKey);
            }

            _ownerJobKeys.Clear();
        }

        private static void RemoveOwnerJobKey(ushort ownerKey)
        {
            if (!_ownerJobKeys.IsCreated)
                return;

            for (int i = _ownerJobKeys.Length - 1; i >= 0; i--)
            {
                if (_ownerJobKeys[i] != ownerKey)
                    continue;

                _ownerJobKeys.RemoveAtSwapBack(i);
                return;
            }
        }

        private static void CompleteSceneTransitionOwnerJobs()
        {
            if (!_ownerPointerKeys.IsCreated)
                return;

            for (int i = 0; i < _ownerPointerKeys.Length; i++)
            {
                SystemID owner = (SystemID)_ownerPointerKeys[i];
                if (IsSceneTransitionOwner(owner))
                    CompleteOwnerJobs(owner);
            }
        }

        private static long ComputeSceneTransitionBaselineBytes(int cutoffGeneration)
        {
            long releasableBytes = 0L;
            for (int i = 0; i < _recordCount; i++)
            {
                H8AllocationRecord record = _records[i];
                if (IsSceneTransitionRecord(in record, cutoffGeneration))
                    releasableBytes += record.Bytes;
            }

            long baseline = _totalBytes - releasableBytes;
            return baseline > 0L ? baseline : 0L;
        }

        private static bool IsSceneTransitionRecord(in H8AllocationRecord record, int cutoffGeneration)
        {
            return record.Pointer != IntPtr.Zero &&
                   record.Generation <= cutoffGeneration &&
                   IsSceneTransitionOwner(record.Owner);
        }

        private static bool IsSceneTransitionOwner(SystemID owner)
        {
            switch (owner)
            {
                case SystemID.Unknown:
                case SystemID.CoreDataVault:
                case SystemID.H8Memory:
                case SystemID.Bootstrap:
                case SystemID.CoreDeterminism:
                case SystemID.SystemDispatcher:
                case SystemID.HardwareHomeostasis:
                case SystemID.GlobalPhysicsStateManager:
                case SystemID.Physics:
                    return false;
                default:
                    return true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort GetOwnerKey(SystemID owner)
        {
            return (ushort)owner;
        }

        private static bool ForceFreeRecordAt(int index, bool removeOwnerPointer, out H8AllocationRecord releasedRecord)
        {
            releasedRecord = default;
            if ((uint)index >= (uint)_recordCount)
                return false;

            H8AllocationRecord record = _records[index];
            if (record.Pointer == IntPtr.Zero)
            {
                RemoveRecordAt(index, removeOwnerPointer, H8MemoryTelemetryFlags.ForcedRelease | H8MemoryTelemetryFlags.Fault);
                return false;
            }

            releasedRecord = record;
            UnsafeUtility.Free(record.Pointer.ToPointer(), record.Allocator);
            RemoveRecordAt(index, removeOwnerPointer, H8MemoryTelemetryFlags.ForcedRelease);
            return true;
        }

        private static bool RegisterOwnerPointer(SystemID owner, IntPtr pointer)
        {
            if (owner == SystemID.Unknown || pointer == IntPtr.Zero || !_ownerPointers.IsCreated)
                return false;

            ushort ownerKey = GetOwnerKey(owner);
            if (!_ownerPointers.TryGetValue(ownerKey, out NativeList<IntPtr> pointers))
            {
                // COLD ALLOC: NativeList<IntPtr>[16] - owner pointer lane for ReleaseAll(SystemID) - owner: H8Memory
                pointers = new NativeList<IntPtr>(DefaultOwnerPointerCapacity, Allocator.Persistent);
                if (!_ownerPointers.TryAdd(ownerKey, pointers))
                {
                    pointers.Dispose();
                    return false;
                }

                if (_ownerPointerKeys.IsCreated)
                    _ownerPointerKeys.Add(ownerKey);
            }

            pointers.Add(pointer);
            _ownerPointers[ownerKey] = pointers;
            return true;
        }

        private static void RemoveOwnerPointer(SystemID owner, IntPtr pointer)
        {
            if (owner == SystemID.Unknown ||
                pointer == IntPtr.Zero ||
                !_ownerPointers.IsCreated ||
                !_ownerPointers.TryGetValue(GetOwnerKey(owner), out NativeList<IntPtr> pointers) ||
                !pointers.IsCreated)
            {
                return;
            }

            for (int i = pointers.Length - 1; i >= 0; i--)
            {
                if (pointers[i] != pointer)
                    continue;

                pointers.RemoveAtSwapBack(i);
                _ownerPointers[GetOwnerKey(owner)] = pointers;
                return;
            }
        }

        private static void DisposeOwnerPointerLists()
        {
            if (!_ownerPointerKeys.IsCreated || !_ownerPointers.IsCreated)
                return;

            for (int i = 0; i < _ownerPointerKeys.Length; i++)
            {
                ushort ownerKey = _ownerPointerKeys[i];
                if (!_ownerPointers.TryGetValue(ownerKey, out NativeList<IntPtr> pointers) || !pointers.IsCreated)
                    continue;

                pointers.Dispose();
                _ownerPointers.Remove(ownerKey);
            }
        }

        private static bool TryFindRecordIndex(IntPtr pointer, out int index)
        {
            index = -1;
            if (pointer == IntPtr.Zero)
                return false;

            long pointerKey = pointer.ToInt64();
            if (_allocationRecordIndices.IsCreated &&
                _allocationRecordIndices.TryGetValue(pointerKey, out int mappedIndex) &&
                (uint)mappedIndex < (uint)_recordCount &&
                _records[mappedIndex].Pointer.ToInt64() == pointerKey)
            {
                index = mappedIndex;
                return true;
            }

            for (int i = _recordCount - 1; i >= 0; i--)
            {
                if (_records[i].Pointer.ToInt64() != pointerKey)
                    continue;

                index = i;
                if (_allocationRecordIndices.IsCreated)
                    _allocationRecordIndices[pointerKey] = i;
                return true;
            }

            return false;
        }

        private static void WriteFatalLeakBlackBox(SystemID owner, int releaseCount, long releasedBytes, bool baselineMismatch)
        {
            H8MemoryTelemetryFlags flags = H8MemoryTelemetryFlags.Fault;
            if (releaseCount > 0)
                flags |= H8MemoryTelemetryFlags.ForcedRelease;
            if (baselineMismatch)
                flags |= H8MemoryTelemetryFlags.BaselineMismatch;
            RecordBlackBox(owner, flags);

            string path = ResolveAgentDumpPath();
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write("[FATAL LEAK: SystemID]");
                    writer.Write((ushort)owner);
                    writer.Write(_transitionSequence);
                    writer.Write(releaseCount);
                    writer.Write(releasedBytes);
                    writer.Write(_totalBytes);
                    writer.Write(_transitionBaselineBytes);
                    writer.Write(baselineMismatch ? 1 : 0);
                    WriteBlackBoxEntries(writer);
                    writer.Write(_recordCount);
                    int dumpCount = _recordCount < 300 ? _recordCount : 300;
                    writer.Write(dumpCount);
                    for (int i = 0; i < dumpCount; i++)
                    {
                        H8AllocationRecord record = _records[i];
                        writer.Write(record.Pointer.ToInt64());
                        writer.Write(record.Bytes);
                        writer.Write(record.Length);
                        writer.Write(record.Stride);
                        writer.Write(record.Alignment);
                        writer.Write(record.AllocationIndex);
                        writer.Write(record.Generation);
                        writer.Write((ushort)record.Owner);
                        writer.Write((int)record.Allocator);
                        writer.Write(record.Flags);
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void RecordBlackBox(SystemID owner, H8MemoryTelemetryFlags flags)
        {
            if ((flags & H8MemoryTelemetryFlags.Heartbeat) != 0)
                RecordFrameHeartbeat(owner, flags);
            else
                RecordLifecycleEvent(owner, flags);
        }

        private static void RecordFrameHeartbeat(SystemID owner, H8MemoryTelemetryFlags flags)
        {
            if (!_blackBox.IsCreated || _blackBox.Length == 0)
                return;

            int cursor = _blackBoxCursor;
            if ((uint)cursor >= (uint)_blackBox.Length)
                cursor = 0;

            H8MemoryTelemetryEntry entry = BuildTelemetryEntry(owner, flags, ++_blackBoxSequence);
            _blackBox[cursor] = entry;

            cursor++;
            if (cursor >= _blackBox.Length)
                cursor = 0;
            _blackBoxCursor = cursor;
        }

        private static void RecordLifecycleEvent(SystemID owner, H8MemoryTelemetryFlags flags)
        {
            if (!_eventBlackBox.IsCreated || _eventBlackBox.Length == 0)
                return;

            int cursor = _eventBlackBoxCursor;
            if ((uint)cursor >= (uint)_eventBlackBox.Length)
                cursor = 0;

            H8MemoryTelemetryEntry entry = BuildTelemetryEntry(owner, flags, ++_eventBlackBoxSequence);
            _eventBlackBox[cursor] = entry;

            cursor++;
            if (cursor >= _eventBlackBox.Length)
                cursor = 0;
            _eventBlackBoxCursor = cursor;
        }

        private static H8MemoryTelemetryEntry BuildTelemetryEntry(SystemID owner, H8MemoryTelemetryFlags flags, uint sequence)
        {
            H8MemoryTelemetryEntry entry = default;
            entry.TotalBytes = _totalBytes;
            entry.TransitionBaselineBytes = _transitionBaselineBytes;
            entry.LastTransitionReleasedBytes = _lastTransitionReleasedBytes;
            entry.Sequence = sequence;
            entry.ActiveAllocationCount = _recordCount;
            entry.BlockDescriptorCount = _blockDescriptors.IsCreated ? _blockDescriptors.Length : 0;
            entry.AllocationGeneration = _allocationGeneration;
            entry.TransitionCutoffGeneration = _transitionCutoffGeneration;
            entry.TransitionSequence = _transitionSequence;
            entry.LastTransitionReleasedCount = _lastTransitionReleasedCount;
            entry.FatalLeakPreventedCount = _fatalLeakPreventedCount;
            entry.Owner = (ushort)owner;
            entry.Flags = (ushort)flags;
            entry.Frame = unchecked((uint)Time.frameCount);
            return entry;
        }

        private static void WriteBlackBoxEntries(BinaryWriter writer)
        {
            WriteBlackBoxRing(writer, _blackBox, _blackBoxSequence, _blackBoxCursor);
            WriteBlackBoxRing(writer, _eventBlackBox, _eventBlackBoxSequence, _eventBlackBoxCursor);
        }

        private static void WriteBlackBoxRing(
            BinaryWriter writer,
            NativeArray<H8MemoryTelemetryEntry> ring,
            uint sequence,
            int cursor)
        {
            if (!ring.IsCreated || ring.Length == 0)
            {
                writer.Write(0);
                return;
            }

            int recordedCount = sequence < (uint)ring.Length ? (int)sequence : ring.Length;
            writer.Write(recordedCount);

            int start = sequence < (uint)ring.Length ? 0 : cursor;
            for (int i = 0; i < recordedCount; i++)
            {
                int index = start + i;
                if (index >= ring.Length)
                    index -= ring.Length;

                H8MemoryTelemetryEntry entry = ring[index];
                writer.Write(entry.TotalBytes);
                writer.Write(entry.TransitionBaselineBytes);
                writer.Write(entry.LastTransitionReleasedBytes);
                writer.Write(entry.Sequence);
                writer.Write(entry.ActiveAllocationCount);
                writer.Write(entry.BlockDescriptorCount);
                writer.Write(entry.AllocationGeneration);
                writer.Write(entry.TransitionCutoffGeneration);
                writer.Write(entry.TransitionSequence);
                writer.Write(entry.LastTransitionReleasedCount);
                writer.Write(entry.FatalLeakPreventedCount);
                writer.Write(entry.Owner);
                writer.Write(entry.Flags);
                writer.Write(entry.Frame);
            }
        }

        private static string ResolveAgentDumpPath()
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            if (string.IsNullOrEmpty(currentDirectory))
                return null;

            string projectRoot = Path.GetFileName(currentDirectory) == "Hecton8"
                ? currentDirectory
                : Path.Combine(currentDirectory, "Hecton8");
            return Path.Combine(projectRoot, "Docs", "AgentLogs", AgentDumpFileName);
        }

        private static bool TryReserveBytes(SystemID owner, long bytes)
        {
            if (bytes <= 0L)
                return false;

            if (_poolCapBytes > 0L && bytes > _poolCapBytes - _totalBytes)
                return false;

            return true;
        }

        private static int ResolveTrackingCapacity(int capacity)
        {
            if (capacity <= 0)
                return DefaultCapacity;

            return capacity > MaxTrackingCapacity ? MaxTrackingCapacity : capacity;
        }

        private static bool TryReserveReplacementBytes(long oldBytes, long newBytes)
        {
            if (newBytes <= 0L)
                return false;

            if (_poolCapBytes <= 0L)
                return true;

            long retainedBytes = _totalBytes > oldBytes ? _totalBytes - oldBytes : 0L;
            return newBytes <= _poolCapBytes - retainedBytes;
        }

        private static bool EnsureTrackingCapacity()
        {
            if (_recordCount < _records.Length)
                return true;

            int oldCapacity = _records.Length;
            if (oldCapacity >= MaxTrackingCapacity)
                return false;

            int newCapacity = oldCapacity > 0 ? oldCapacity << 1 : DefaultCapacity;
            if (newCapacity < oldCapacity || newCapacity > MaxTrackingCapacity)
                newCapacity = MaxTrackingCapacity;

            NativeArray<H8AllocationRecord> newRecords =
                new NativeArray<H8AllocationRecord>(newCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeParallelHashMap<long, SystemID> newOwners =
                new NativeParallelHashMap<long, SystemID>(newCapacity, Allocator.Persistent);
            NativeParallelHashMap<long, int> newIndices =
                new NativeParallelHashMap<long, int>(newCapacity, Allocator.Persistent);

            for (int i = 0; i < _recordCount; i++)
            {
                H8AllocationRecord record = _records[i];
                newRecords[i] = record;
                if (record.Pointer == IntPtr.Zero)
                    continue;

                long pointerKey = record.Pointer.ToInt64();
                if (!newOwners.TryAdd(pointerKey, record.Owner) || !newIndices.TryAdd(pointerKey, i))
                {
                    newRecords.Dispose();
                    newOwners.Dispose();
                    newIndices.Dispose();
                    return false;
                }
            }

            if (_records.IsCreated)
                _records.Dispose();
            if (_allocationOwners.IsCreated)
                _allocationOwners.Dispose();
            if (_allocationRecordIndices.IsCreated)
                _allocationRecordIndices.Dispose();

            _records = newRecords;
            _allocationOwners = newOwners;
            _allocationRecordIndices = newIndices;
            EnsureBlockDescriptorCapacity(newCapacity);
            return true;
        }

        private static bool RegisterPointer(
            void* pointer,
            long bytes,
            int length,
            int stride,
            int alignment,
            SystemID owner,
            Allocator allocator,
            H8AllocationFlags flags)
        {
            if (pointer == null || bytes <= 0L || _recordCount >= _records.Length)
                return false;

            IntPtr pointerValue = (IntPtr)pointer;
            long pointerKey = pointerValue.ToInt64();
            int recordIndex = _recordCount;
            if (!_allocationOwners.TryAdd(pointerKey, owner))
                return false;

            if (!_allocationRecordIndices.TryAdd(pointerKey, recordIndex))
            {
                _allocationOwners.Remove(pointerKey);
                return false;
            }

            if (!RegisterOwnerPointer(owner, pointerValue))
            {
                _allocationOwners.Remove(pointerKey);
                _allocationRecordIndices.Remove(pointerKey);
                return false;
            }

            H8AllocationRecord record = new H8AllocationRecord
            {
                Pointer = pointerValue,
                Bytes = bytes,
                Length = length,
                Stride = stride,
                Alignment = alignment,
                AllocationIndex = recordIndex,
                Generation = _allocationGeneration,
                Owner = owner,
                Allocator = allocator,
                Flags = (ushort)flags
            };

            _records[_recordCount++] = record;
            _totalBytes += bytes;
            int ownerIndex = (int)owner;
            if ((uint)ownerIndex < (uint)_ownerBytes.Length)
                _ownerBytes[ownerIndex] += bytes;

            if ((flags & H8AllocationFlags.SubAllocatorRoot) != 0)
            {
                RecordBlackBox(owner, H8MemoryTelemetryFlags.Allocated);
                return true;
            }

            int descriptorIndex = RegisterBlockDescriptorNoInit(new BlockDescriptor
            {
                BasePointer = pointerValue,
                OffsetBytes = 0L,
                Bytes = bytes,
                OwnerKey = record.AllocationIndex,
                Generation = 1,
                Owner = owner,
                Flags = (ushort)flags,
                State = (byte)H8BlockState.Occupied
            });

            if (descriptorIndex >= 0)
            {
                RecordBlackBox(owner, H8MemoryTelemetryFlags.Allocated);
                return true;
            }

            RemoveRecordAt(recordIndex);
            return false;
        }

        private static void UnregisterPointer(void* pointer, SystemID requester)
        {
            UnregisterPointer(pointer, requester, requireOwnerMatch: true);
        }

        private static void UnregisterPointer(void* pointer, SystemID requester, bool requireOwnerMatch)
        {
            if (!_initialized || pointer == null)
                return;

            if (requireOwnerMatch && requester == SystemID.Unknown)
                FatalMemoryException.ThrowUnknownFreeOwner();

            long pointerKey = ((IntPtr)pointer).ToInt64();
            ValidateOwnerMap(pointerKey, requester, requireOwnerMatch);
            for (int i = _recordCount - 1; i >= 0; i--)
            {
                if (_records[i].Pointer.ToInt64() != pointerKey)
                    continue;

                if (requireOwnerMatch && _records[i].Owner != requester)
                    FatalMemoryException.ThrowWrongFreeOwner();

                RemoveRecordAt(i);
                return;
            }

            if (requireOwnerMatch)
                FatalMemoryException.ThrowUntrackedPointer();
        }

        private static long ValidateTrackedPointerOwner(void* pointer, SystemID requester)
        {
            if (!_initialized || pointer == null)
                return 0L;
            if (requester == SystemID.Unknown)
                FatalMemoryException.ThrowUnknownFreeOwner();

            long pointerKey = ((IntPtr)pointer).ToInt64();
            ValidateOwnerMap(pointerKey, requester, requireOwnerMatch: true);
            for (int i = _recordCount - 1; i >= 0; i--)
            {
                if (_records[i].Pointer.ToInt64() != pointerKey)
                    continue;

                if (_records[i].Owner != requester)
                    FatalMemoryException.ThrowWrongFreeOwner();

                return _records[i].Bytes;
            }

            FatalMemoryException.ThrowUntrackedPointer();
            return 0L;
        }

        private static void ValidateOwnerMap(long pointerKey, SystemID requester, bool requireOwnerMatch)
        {
            if (!_allocationOwners.IsCreated ||
                !_allocationOwners.TryGetValue(pointerKey, out SystemID mappedOwner))
            {
                if (requireOwnerMatch)
                    FatalMemoryException.ThrowUntrackedPointer();
                return;
            }

            if (requireOwnerMatch && mappedOwner != requester)
                FatalMemoryException.ThrowWrongFreeOwner();
        }

        private static int AdvanceDescriptorGeneration(int generation)
        {
            int nextGeneration = unchecked(generation + 1);
            return nextGeneration <= 0 ? 1 : nextGeneration;
        }

        private static int ResolveSafeAlignment(int alignment)
        {
            if (alignment <= MinimumRawAlignment)
                return MinimumRawAlignment;

            int resolved = MinimumRawAlignment;
            while (resolved < alignment && resolved < MaximumRawAlignment)
                resolved <<= 1;

            return resolved < alignment ? MaximumRawAlignment : resolved;
        }

        private static void RemoveRecordAt(int index)
        {
            RemoveRecordAt(index, removeOwnerPointer: true, H8MemoryTelemetryFlags.Released);
        }

        private static void RemoveRecordAt(int index, bool removeOwnerPointer)
        {
            RemoveRecordAt(index, removeOwnerPointer, H8MemoryTelemetryFlags.Released);
        }

        private static void RemoveRecordAt(int index, bool removeOwnerPointer, H8MemoryTelemetryFlags telemetryFlags)
        {
            H8AllocationRecord record = _records[index];
            long pointerKey = record.Pointer.ToInt64();
            _allocationOwners.Remove(pointerKey);
            if (_allocationRecordIndices.IsCreated)
                _allocationRecordIndices.Remove(pointerKey);
            if (removeOwnerPointer)
                RemoveOwnerPointer(record.Owner, record.Pointer);
            MarkBlockDescriptorFree(record.Pointer, 0L);
            _totalBytes -= record.Bytes;
            int ownerIndex = (int)record.Owner;
            if ((uint)ownerIndex < (uint)_ownerBytes.Length)
                _ownerBytes[ownerIndex] -= record.Bytes;

            _recordCount--;
            if (index != _recordCount)
            {
                H8AllocationRecord moved = _records[_recordCount];
                moved.AllocationIndex = index;
                _records[index] = moved;
                if (_allocationRecordIndices.IsCreated && moved.Pointer != IntPtr.Zero)
                    _allocationRecordIndices[moved.Pointer.ToInt64()] = index;
                UpdateBlockDescriptorOwnerKey(moved.Pointer, 0L, index);
            }

            _records[_recordCount] = default;
            RecordBlackBox(record.Owner, telemetryFlags);
        }

        private static int RegisterBlockDescriptorNoInit(in BlockDescriptor descriptor)
        {
            if (!_blockDescriptors.IsCreated)
                return -1;

            for (int i = 0; i < _blockDescriptors.Length; i++)
            {
                BlockDescriptor existing = _blockDescriptors[i];
                if (existing.Bytes != 0L)
                    continue;

                BlockDescriptor replacement = descriptor;
                int nextGeneration = AdvanceDescriptorGeneration(existing.Generation);
                if (replacement.Generation < nextGeneration)
                    replacement.Generation = nextGeneration;
                _blockDescriptors[i] = replacement;
                return i;
            }

            if (_blockDescriptors.Length >= _blockDescriptors.Capacity)
            {
                int oldCapacity = _blockDescriptors.Capacity;
                if (oldCapacity >= MaxTrackingCapacity)
                    return -1;

                int newCapacity = oldCapacity > 0 ? oldCapacity << 1 : DefaultCapacity;
                if (newCapacity < oldCapacity || newCapacity > MaxTrackingCapacity)
                    newCapacity = MaxTrackingCapacity;

                EnsureBlockDescriptorCapacity(newCapacity);
                if (_blockDescriptors.Length >= _blockDescriptors.Capacity)
                    return -1;
            }

            int index = _blockDescriptors.Length;
            _blockDescriptors.AddNoResize(descriptor);
            return index;
        }

        private static void EnsureBlockDescriptorCapacity(int requiredCapacity)
        {
            if (!_blockDescriptors.IsCreated || requiredCapacity <= _blockDescriptors.Capacity)
                return;

            _blockDescriptors.Capacity = requiredCapacity;
        }

        private static void MarkBlockDescriptorFree(IntPtr basePointer, long offsetBytes)
        {
            if (!_blockDescriptors.IsCreated || basePointer == IntPtr.Zero)
                return;

            for (int i = _blockDescriptors.Length - 1; i >= 0; i--)
            {
                BlockDescriptor descriptor = _blockDescriptors[i];
                if (descriptor.BasePointer != basePointer || descriptor.OffsetBytes != offsetBytes)
                    continue;

                descriptor.BasePointer = IntPtr.Zero;
                descriptor.OffsetBytes = 0L;
                descriptor.Bytes = 0L;
                descriptor.OwnerKey = 0;
                descriptor.Owner = SystemID.Unknown;
                descriptor.State = (byte)H8BlockState.Free;
                descriptor.Flags = (ushort)H8AllocationFlags.Freed;
                descriptor.Reserved = 0;
                descriptor.Generation = AdvanceDescriptorGeneration(descriptor.Generation);
                _blockDescriptors[i] = descriptor;
                return;
            }
        }

        private static void UpdateBlockDescriptorOwnerKey(IntPtr basePointer, long offsetBytes, int ownerKey)
        {
            if (!_blockDescriptors.IsCreated || basePointer == IntPtr.Zero)
                return;

            for (int i = _blockDescriptors.Length - 1; i >= 0; i--)
            {
                BlockDescriptor descriptor = _blockDescriptors[i];
                if (descriptor.BasePointer != basePointer || descriptor.OffsetBytes != offsetBytes)
                    continue;

                if (descriptor.State != (byte)H8BlockState.Occupied)
                    return;

                descriptor.OwnerKey = ownerKey;
                descriptor.Generation = AdvanceDescriptorGeneration(descriptor.Generation);
                _blockDescriptors[i] = descriptor;
                return;
            }
        }
    }
}
