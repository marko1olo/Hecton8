using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Atmosphere;
using Hecton.Localization;
using Hecton8.Audio;
using Hecton8.Building;
using Hecton8.Core;
using Hecton8.Core.Signals;
using Hecton8.Gameplay;
using Hecton8.Power;
using Hecton8.World;
using CoreCombatDamageSignal = Hecton8.Core.Signals.CombatDamageSignal;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    [Flags]
    internal enum HabitatSiegeTargetFlags : byte
    {
        None = 0,
        Vulnerable = 1 << 0,
        EmergencyAirlock = 1 << 1,
        Flooded = 1 << 2,
        Ruptured = 1 << 3,
        Brownout = 1 << 4,
        Isolated = 1 << 5,
        CascadeFailure = 1 << 6
    }

    internal struct HabitatSiegeTargetSnapshot
    {
        public float3 ModuleCenter;
        public float3 WeakPoint;
        public float Integrity01;
        public float Vulnerability01;
        public uint NodeId;
        public byte Flags;
        public byte Reserved0;
        public byte Reserved1;
        public byte Reserved2;
    }

    [Flags]
    internal enum HabitatRoomFloodFlags : byte
    {
        None = 0,
        Breached = 1 << 0,
        Flooded = 1 << 1,
        Powered = 1 << 2,
        OxygenDisabled = 1 << 3,
        OverflowClamped = 1 << 4
    }

    [Flags]
    internal enum HabitatEdgeFloodFlags : byte
    {
        None = 0,
        Sealed = 1 << 0,
        Ruptured = 1 << 1
    }

    internal struct HabitatFloodConnection
    {
        public int DestinationIndex;
        public int CsrEdgeIndex;
        public float FlowResistance;
        public uint Reserved0;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct HabitatFloodBlackBoxEntry
    {
        public int Frame;
        public ushort NodeCount;
        public ushort EdgeCount;
        public ushort FloodedRoomCount;
        public ushort Reserved0;
        public float BaseTotalStress;
        public float MaxWaterLevel01;
        public float TotalWaterVolumeM3;
        public float PeakModuleStress;
        public uint Flags;
        public uint StateHash;
        public uint DeformationSequence;
    }

    /// <summary>
    /// Rebuilds the placed habitat into a CSR adjacency graph for downstream power and atmosphere solvers.
    /// Owns only base-module topology. Point-to-point crate pipes remain under LogisticsPipeNode.
    /// </summary>
    internal sealed class HabitatGraphManager : IDisposable
    {
        private const float DefaultSocketQuantization = 0.05f;
        private const float OppositeDirectionDotThreshold = -0.85f;
        private const float EdgeResistancePerMeter = 0.05f;
        private const float MinimumEdgeResistance = 0.1f;
        private const float SurfacePressureKPa = 101.325f;
        private const float SeawaterDensityKilogramsPerCubicMeter = 1025f;
        private const float GravityAccelerationMetersPerSecondSquared = 9.81f;
        private const float GravityAccelerationMetersPerSecondSquaredInv = 1f / GravityAccelerationMetersPerSecondSquared;
        private const float HydrostaticPressureKPaPerMeter = SeawaterDensityKilogramsPerCubicMeter * GravityAccelerationMetersPerSecondSquared * 0.001f;
        private const float DefaultHydroShearThresholdKilograms = 18000f;
        private const int PressureRootLutSize = 32;
        private const float PressureRootLutMaxKPa = 12000f;
        private const float PressureRootLutStepKPa = PressureRootLutMaxKPa / PressureRootLutSize;
        private const float PressureRootLutStepKPaInv = 1f / PressureRootLutStepKPa;
        private const float PressureRootExcessLinearScale = 0.5f;
        private const int GraphFloodMaxTraversalNodesPerTick = 512;
        private const int GraphFloodMidTraversalNodesPerTick = 256;
        private const int GraphFloodLowTraversalNodesPerTick = 128;
        private const float GraphFloodTransferRateM3PerSecond = 0.35f;
        private const float GraphFloodMaxTransferPerEdgeM3 = 0.1f;
        private const float GraphFloodWaterEpsilonM3 = 0.0001f;
        private const float GraphFloodAutoSealThreshold01 = 0.1f;
        private const float GraphFloodOxygenDisabledThreshold01 = 0.8f;
        private const float EmergencyFloodLockdownThreshold01 = 0.9f;
        private const float HumidityFogPressureThresholdAtm = 1.2f;
        private const float HumidityFogWaterThreshold01 = 0.001f;
        private const float ThermalPressureReferenceCelsius = 20f;
        private const float ThermalPressurePerCelsius = 0.0034f;
        private const float ThermalPressureMaxScale = 1.25f;
        private const float AnalyticalFullStressDepthMeters = 4000f;
        private const float AnalyticalFullStressDepthInv = 1f / AnalyticalFullStressDepthMeters;
        private const float AnalyticalCurrentFullStressMetersPerSecond = 6f;
        private const float AnalyticalCurrentFullStressInvSq = 1f / (AnalyticalCurrentFullStressMetersPerSecond * AnalyticalCurrentFullStressMetersPerSecond);
        private const float AnalyticalCurrentStressScale = 0.35f;
        private const float AnalyticalMinimumDepthWeightScale = 0.15f;
        private const float AnalyticalMaximumDepthWeightScale = 1.15f;
        private const float AnalyticalAnchorReinforcementScale = 0.35f;
        private const float AnalyticalReachableReinforcementScale = 0.25f;
        private const float AnalyticalGroundedStressScale = 0.5f;
        private const float AnalyticalGroundProbeMeters = 1f;
        private const float AnalyticalShaderStressEpsilon = 0.0025f;
        private const float AnalyticalShaderRadiusPaddingMeters = 2f;
        private const float AnalyticalShaderDisplacementMaxMeters = 0.055f;
        private const float ModuleStressMidDisplacementMaxMeters = 0.036f;
        private const float ModuleStressUltraDisplacementMaxMeters = 0.075f;
        private const float AnalyticalShaderGridScale = 0.085f;
        private const float ModuleStressUploadEpsilon = 0.0015f;
        private const float ModuleStressDepthWeight = 0.58f;
        private const float ModuleStressDamageWeight = 0.42f;
        private const float ModuleStressFloodWeight = 0.28f;
        private const float ModuleStressImpactSpikeDecayPerSecond = 1f;
        private const float ModuleStressImpactSpikeStrength = 1f;
        private const float ModuleStressFastDeltaGroanThresholdPerSecond = 0.9f;
        private const float ModuleStressCompromisedThreshold01 = 0.985f;
        private const float ModuleStressNearestSignalPaddingMeters = 3f;
        private const float ModuleStressNearestSignalFallbackRadiusMeters = 8f;
        private const float ModuleStressNearestSignalMaxRadiusMeters = 36f;
        private const int ModuleStressShaderCapacity = 64;
        private const float AnalyticalLowTierFeedbackThreshold01 = 0.42f;
        private const float AnalyticalLowTierFeedbackCooldownSeconds = 3.5f;
        private const float AnalyticalEmergencyRemainingIntegrityThreshold01 = 0.2f;
        private const int AnalyticalBreachMinimumThreshold = 4;
        private const int AnalyticalBreachMaximumThreshold = 96;
        private const float HabitatVibrationDecayPerSecond = 0.75f;
        private const float HabitatVibrationImpulseScale = 0.0015f;
        private const float PressureBucklingCompressionDeltaThreshold = 0.15f;
        private const float RuptureCascadeNeighborStressMultiplier = 0.5f;
        private const float StructuralGroanStressThreshold01 = 0.8f;
        private const float StructuralGroanPitchRange = 0.32f;
        private const float CondensationInteriorTemperatureCelsius = 30f;
        private const float CondensationExternalTemperatureCelsius = 5f;
        private const float SupportCaptureRadiusMeters = 3f;
        private const float SupportCaptureRadiusSq = SupportCaptureRadiusMeters * SupportCaptureRadiusMeters;
        private const int InitialSocketCapacity = 32;
        private const int InitialNodeCapacity = 64;
        private const int InitialEdgeCapacity = 128;
        private const int InitialTemporaryBypassCapacity = 16;
        internal const int MaxSiegeTargetCount = 64;
        private const int FloodBlackBoxCapacity = 300;
        private const uint FloodBlackBoxMagic = 0x48464C44u; // "HFLD"
        private const uint FloodBlackBoxVersion = 3u;
        private const uint FloodBlackBoxNonFiniteFlag = 1u << 0;
        private const uint FloodBlackBoxOverflowClampedFlag = 1u << 1;
        private const uint FloodBlackBoxTraversalOverflowFlag = 1u << 2;
        private const uint FloodBlackBoxTopologyInvalidFlag = 1u << 3;
        private const uint FloodBlackBoxModuleStressInvalidFlag = 1u << 4;
        private const string FloodBlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_HABITAT_INTEGRITY.bin";
        private const string ModuleStressBlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_VOLUMETRIC_PRESSURE_SOLVER.bin";
        private const float SiegeVulnerableIntegrityThreshold01 = 0.72f;
        private static readonly int CarbonFilterItemHashId = LocHash.Compute("Data_CarbonFilter");
        private static readonly uint RuptureCascadeEventHash = unchecked((uint)LocHash.Compute("HabitatGraphManager.RuptureCascade"));
        private static readonly int HabitatStressCenterRadiusId = Shader.PropertyToID("_HectonHabitatStressCenterRadius");
        private static readonly int HabitatStressParamsId = Shader.PropertyToID("_HectonHabitatStressParams");
        private static readonly int HabitatModuleStressBufferId = Shader.PropertyToID("_HectonHabitatModuleStressBuffer");
        private static readonly int HabitatModuleStressParamsId = Shader.PropertyToID("_HectonHabitatModuleStressParams");
        private static readonly int HabitatVibrationId = Shader.PropertyToID("_HectonHabitatVibration01");
        private static readonly int BaseEmergencyStateId = Shader.PropertyToID("_BaseEmergencyState");
        private const string NativeMemoryOwner = nameof(HabitatGraphManager);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;
        // COLD ALLOC: float[33] - pressure ingress sqrt lookup table - owner: HabitatGraphManager
        private static readonly float[] s_pressureRootLut =
        {
            0f, 19.364917f, 27.386128f, 33.54102f, 38.729833f, 43.30127f, 47.434165f, 51.234754f,
            54.772256f, 58.09475f, 61.237244f, 64.226163f, 67.082039f, 69.8212f, 72.456884f, 75f,
            77.459667f, 79.843597f, 82.158384f, 84.409715f, 86.60254f, 88.741197f, 90.829511f, 92.870878f,
            94.86833f, 96.824584f, 98.742088f, 100.623059f, 102.469508f, 104.283268f, 106.066017f, 107.819293f,
            109.544512f
        };
        // COLD ALLOC: float[33] - low-tier reciprocal pressure lookup, avoids hot-path rcp on office CPUs - owner: HabitatGraphManager
        private static readonly float[] s_pressureRootInvLut =
        {
            1f, 0.05163978f, 0.03651484f, 0.02981424f, 0.02581989f, 0.02309401f, 0.02108185f, 0.019518f,
            0.01825742f, 0.01721326f, 0.01632993f, 0.01556998f, 0.01490712f, 0.0143223f, 0.01380131f, 0.01333333f,
            0.01290994f, 0.01252449f, 0.01217161f, 0.01184698f, 0.01154701f, 0.01126872f, 0.01100964f, 0.01076764f,
            0.01054093f, 0.01032796f, 0.01012739f, 0.00993808f, 0.009759f, 0.00958927f, 0.00942809f, 0.00927478f,
            0.00912871f
        };
        private static readonly Color PipeSplineColor = new Color(0.30f, 0.82f, 0.95f, 0.88f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticSiegeTargets()
        {
            s_latestSiegeTargets = default;
            s_latestSiegeTargetOwner = null;
            s_latestSiegeTargetCount = 0;
        }

        private readonly List<ModuleSocket> _socketBuffer;
        private readonly List<ModuleRecord> _moduleBuffer;
        private readonly List<EdgeRecord> _edgeBuffer;
        private readonly List<TemporaryBypassRecord> _temporaryBypassBuffer;
        private readonly List<long> _submittedLinkIds;
        private readonly List<long> _emittedRuptureEdgeVfxKeys;
        private readonly HashSet<long> _emittedRuptureEdgeVfxLookup;
        private readonly List<uint> _ruptureCascadeAppliedNodeIds;
        private readonly Dictionary<uint, int> _moduleIndexByNodeId;
        private readonly Dictionary<SocketKey, SocketMatchEntry> _socketLookup;

        private NativeArray<LogisticsNetworkGraph.LogisticsNode> _nodes;
        private NativeArray<int> _edgeOffsets;
        private NativeArray<int> _edgeDestinations;
        private NativeArray<float> _edgeResistance;
        private NativeArray<int> _edgeWriteCursor;
        private NativeArray<byte> _anchorReachability;
        private NativeArray<byte> _traversalVisited;
        private NativeArray<int> _anchorTraversalQueue;
        private NativeArray<HabitatSiegeTargetSnapshot> _siegeTargets;
        private NativeArray<float> _roomWaterLevels;
        private NativeArray<float> _roomVolumes;
        private NativeArray<float> _roomFloodDeltaLevels;
        private NativeArray<float> _moduleStressScalars;
        private NativeArray<float> _previousModuleStressScalars;
        private NativeArray<float> _moduleImpactStressSpikes;
        private NativeArray<byte> _moduleCompromisedFlags;
        private NativeArray<byte> _roomFlags;
        private NativeArray<byte> _edgeFlags;
        private NativeArray<HabitatFloodBlackBoxEntry> _floodBlackBox;
        private NativeArray<HabitatFloodPropagationSummary> _floodPropagationSummary;
        private NativeParallelMultiHashMap<int, HabitatFloodConnection> _roomConnections;
        private static NativeArray<HabitatSiegeTargetSnapshot> s_latestSiegeTargets;
        private static HabitatGraphManager s_latestSiegeTargetOwner;
        private static int s_latestSiegeTargetCount;

        private readonly LogisticsNetworkGraph _graph;
        private int _nodeCount;
        private int _edgeCount;
        private int _siegeTargetCount;
        private float _analyticalStress;
        private float _analyticalIntegrity;
        private float _lastPublishedAnalyticalStress01 = -1f;
        private float _lastPublishedAnalyticalDisplacementMaxMeters = -1f;
        private float _nextAnalyticalLowTierFeedbackTime;
        private uint _lastPublishedAnalyticalBreachNodeId;
        private uint _analyticalBreachNodeId;
        private float _habitatVibration01;
        private float _lastPublishedHabitatVibration01 = -1f;
        private float _runtimeSeaLevelY;
        private int _graphFloodSliceCursor;
        private int _floodedRoomCount;
        private int _floodBlackBoxCursor;
        private float _baseTotalStress;
        private float _maxRoomWaterLevel01;
        private float _totalRoomWaterVolumeM3;
        private float _peakModuleStress01;
        private float _lastUploadedPeakModuleStress01 = -1f;
        private uint _floodBlackBoxStateHash;
        private uint _moduleStressSequence;
        private uint _moduleStressOrderHash;
        private bool _floodBlackBoxDumped;
        private bool _moduleStressBlackBoxDumped;
        private bool _lastUploadedModuleStressLowTier;
        private HectonQualityTier _lastUploadedModuleStressTier = HectonQualityTier.Unknown;
        private int _lastUploadedModuleStressCount = -1;
        private int _lastProcessedModuleStressSignalFrame = -1;
        private GraphicsBuffer _moduleStressBuffer;
        private HectonAtmosphereManager _atmosphereManager;
        private IAudioService _audioService;
        private AbyssalFluidDecalManager _fluidDecals;

        internal HabitatGraphManager(int initialModuleCapacity)
        {
            int safeModuleCapacity = math.max(1, initialModuleCapacity);
            // COLD ALLOC: List<ModuleSocket>[32] — reusable module socket scan buffer for base graph rebuilds — owner: HabitatGraphManager
            _socketBuffer = new List<ModuleSocket>(InitialSocketCapacity);
            // COLD ALLOC: List<ModuleRecord>[64] — reusable module staging buffer for CSR rebuilds — owner: HabitatGraphManager
            _moduleBuffer = new List<ModuleRecord>(safeModuleCapacity);
            // COLD ALLOC: List<EdgeRecord>[128] — reusable undirected base-link staging buffer for CSR rebuilds — owner: HabitatGraphManager
            _edgeBuffer = new List<EdgeRecord>(InitialEdgeCapacity);
            // COLD ALLOC: List<TemporaryBypassRecord>[16] — authored runtime bypass links appended into habitat CSR rebuilds — owner: HabitatGraphManager
            _temporaryBypassBuffer = new List<TemporaryBypassRecord>(InitialTemporaryBypassCapacity);
            // COLD ALLOC: List<Int64>[128] — submitted visual spline link ids for removal during rebuild — owner: HabitatGraphManager
            _submittedLinkIds = new List<long>(InitialEdgeCapacity);
            // COLD ALLOC: List<Int64>[128] - emitted rupture edge VFX keys - owner: HabitatGraphManager
            _emittedRuptureEdgeVfxKeys = new List<long>(InitialEdgeCapacity);
            // COLD ALLOC: HashSet<Int64>[256] - capped duplicate guard for rupture edge VFX keys - owner: HabitatGraphManager
            _emittedRuptureEdgeVfxLookup = new HashSet<long>(InitialEdgeCapacity * 2);
            // COLD ALLOC: List<UInt32>[64] - one-shot rupture cascade source guard - owner: HabitatGraphManager
            _ruptureCascadeAppliedNodeIds = new List<uint>(safeModuleCapacity);
            // COLD ALLOC: Dictionary<UInt32,Int32>[64] — node-id to module-index lookup for temporary bypass stitching — owner: HabitatGraphManager
            _moduleIndexByNodeId = new Dictionary<uint, int>(safeModuleCapacity);
            // COLD ALLOC: Dictionary<SocketKey,SocketMatchEntry>[128] — quantized socket lookup for zero-GC adjacency assembly — owner: HabitatGraphManager
            _socketLookup = new Dictionary<SocketKey, SocketMatchEntry>(InitialEdgeCapacity);

            _graph = new LogisticsNetworkGraph(safeModuleCapacity, InitialEdgeCapacity * 2, 0);
            AllocateNativeBuffers(safeModuleCapacity, InitialEdgeCapacity * 2);
        }

        internal int NodeCount => _nodeCount;
        internal int EdgeCount => _edgeCount;
        internal NativeArray<LogisticsNetworkGraph.LogisticsNode> Nodes => _nodes;
        internal NativeArray<int> EdgeOffsets => _edgeOffsets;
        internal NativeArray<int> EdgeDestinations => _edgeDestinations;
        internal NativeArray<float> EdgeResistance => _edgeResistance;
        internal NativeArray<float> RoomWaterLevels => _roomWaterLevels;
        internal NativeArray<float> RoomVolumes => _roomVolumes;
        internal NativeArray<byte> RoomFlags => _roomFlags;
        internal NativeArray<byte> EdgeFlags => _edgeFlags;
        internal NativeParallelMultiHashMap<int, HabitatFloodConnection> RoomConnections => _roomConnections;
        internal int FloodedRoomCount => _floodedRoomCount;
        internal float BaseTotalStress => _baseTotalStress;
        internal uint FloodStateSequence => _floodBlackBoxStateHash;
        internal LogisticsNetworkGraph Graph => _graph;

        internal bool TryGetAcousticNodePosition(int nodeIndex, out float3 position)
        {
            position = float3.zero;
            if (_moduleBuffer == null || (uint)nodeIndex >= (uint)_moduleBuffer.Count)
                return false;

            position = _moduleBuffer[nodeIndex].Position;
            return math.all(math.isfinite(position));
        }

        internal bool TryResolveRoomWaterline(
            Vector3 runtimePosition,
            int cachedRoomId,
            out HabitatRoomWaterlineSnapshot snapshot)
        {
            snapshot = default;
            if (!math.all(math.isfinite((float3)runtimePosition)) || _moduleBuffer == null)
                return false;

            if (TryResolveCachedRoomWaterline(runtimePosition, cachedRoomId, out snapshot))
                return true;

            int roomLimit = math.min(math.max(0, _nodeCount), _moduleBuffer.Count);
            for (int roomId = 0; roomId < roomLimit; roomId++)
            {
                if (roomId == cachedRoomId)
                    continue;

                BaseModule baseModule = _moduleBuffer[roomId].BaseModule;
                if (baseModule == null || !baseModule.isActiveAndEnabled)
                    continue;

                if (!baseModule.TryContainsInteriorRuntimePoint(runtimePosition))
                    continue;

                return TryGetRoomWaterline(roomId, out snapshot);
            }

            return false;
        }

        internal bool TryGetRoomWaterline(int roomId, out HabitatRoomWaterlineSnapshot snapshot)
        {
            snapshot = default;
            if (_moduleBuffer == null || (uint)roomId >= (uint)_moduleBuffer.Count)
                return false;

            BaseModule baseModule = _moduleBuffer[roomId].BaseModule;
            if (baseModule == null || !baseModule.isActiveAndEnabled)
                return false;

            float fill01 = ResolveAuthoritativeRoomWaterLevel01(roomId, baseModule);
            return baseModule.TryBuildRoomWaterlineSnapshot(roomId, fill01, _floodBlackBoxStateHash, out snapshot);
        }

        private bool TryResolveCachedRoomWaterline(
            Vector3 runtimePosition,
            int cachedRoomId,
            out HabitatRoomWaterlineSnapshot snapshot)
        {
            snapshot = default;
            if (cachedRoomId < 0 || _moduleBuffer == null || cachedRoomId >= _moduleBuffer.Count)
                return false;

            BaseModule baseModule = _moduleBuffer[cachedRoomId].BaseModule;
            if (baseModule == null || !baseModule.isActiveAndEnabled)
                return false;

            return baseModule.TryContainsInteriorRuntimePoint(runtimePosition) &&
                   TryGetRoomWaterline(cachedRoomId, out snapshot);
        }

        internal static bool TryGetLatestSiegeTargets(out NativeArray<HabitatSiegeTargetSnapshot> targets, out int count)
        {
            targets = s_latestSiegeTargets;
            count = s_latestSiegeTargetCount;
            return s_latestSiegeTargetOwner != null && targets.IsCreated && count > 0;
        }

        public void Dispose()
        {
            PublishAnalyticalStressShader(float3.zero, 0f, 0f, HectonQualityTier.Unknown);
            Shader.SetGlobalInt(BaseEmergencyStateId, 0);
            Shader.SetGlobalFloat(HabitatVibrationId, 0f);
            ClearVisualLinks();
            DisposeNativeBuffers();
            _atmosphereManager = null;
            _audioService = null;
            _fluidDecals = null;
            _graph.Dispose();
        }

        internal int TemporaryBypassCount => _temporaryBypassBuffer.Count;

        internal void Rebuild(IReadOnlyList<GameObject> modules)
        {
            ClearVisualLinks();
            _moduleBuffer.Clear();
            _edgeBuffer.Clear();
            _moduleIndexByNodeId.Clear();
            _socketLookup.Clear();
            _nodeCount = 0;
            _edgeCount = 0;
            ClearModuleStressState();
            _runtimeSeaLevelY = ResolveRuntimeSeaLevelY();
            BaseDegradationSystem.BeginRuptureSync();

            if (modules == null || modules.Count <= 0)
            {
                ClearSiegeTargetSnapshot();
                ClearFloodRoomStateSnapshot();
                _graph.BeginBuild(LogisticsNetworkType.OxygenPressure, 1, 1, 0);
                BaseDegradationSystem.EndRuptureSync();
                return;
            }

            PopulateModuleBuffer(modules);
            _nodeCount = _moduleBuffer.Count;
            EnsureRuptureCascadeStateCapacity(_nodeCount);
            if (_nodeCount <= 0)
            {
                ClearSiegeTargetSnapshot();
                ClearFloodRoomStateSnapshot();
                _graph.BeginBuild(LogisticsNetworkType.OxygenPressure, 1, 1, 0);
                BaseDegradationSystem.EndRuptureSync();
                return;
            }

            EnsureNodeCapacity(_nodeCount);
            BuildSocketAdjacency();
            AppendTemporaryBypassEdges();
            BuildNodeRecords();
            PruneRuptureCascadeState();
            BuildEdgeRecords();
            EvaluateAnchorReachability();
            PublishAnchorState();
            PublishComponentPowerState();
            PublishEmergencyLockdownState();
            SyncFloodRoomStateSnapshot();
            PublishDegradationState();
            PublishSiegeTargetSnapshot();
            PublishGraphKernel();
            PublishVisualLinks();
            BaseDegradationSystem.EndRuptureSync();
        }

        internal void ApplyHydrodynamicStress(float deltaTime)
        {
            if (deltaTime <= 0f || _moduleBuffer.Count <= 0)
                return;

            HectonQualityTier scalabilityTier = GlobalRegistry.ScalabilityTier;
            _runtimeSeaLevelY = ResolveRuntimeSeaLevelY();
            UpdateHabitatVibration(deltaTime);
            ApplyGraphFluidIncursion(deltaTime, scalabilityTier);
            ApplyWaterPumpDrainage(deltaTime);
            ApplyOxygenScrubberFilterConsumption(deltaTime);
            ApplyThermalCondensationState();
            QueueFloodMassLoads(deltaTime);
            bool runtimeTopologyChanged = EvaluateBulkheadFloodStress(deltaTime);
            runtimeTopologyChanged |= EvaluatePressureBucklingStress(deltaTime);
            runtimeTopologyChanged |= EvaluateDetachedDebrisState();
            if (runtimeTopologyChanged)
                PublishRuntimeRuptureTopologyState();

            EvaluateAnalyticalIntegrityStress(scalabilityTier);
            UpdateHabitatModuleStressMatrix(deltaTime, scalabilityTier);
            SyncFloodRoomStateSnapshot();
            WriteFloodBlackBoxSample(0u);
            PublishSiegeTargetSnapshot();
        }

        internal float AnalyticalStress => _analyticalStress;
        internal float AnalyticalIntegrity => _analyticalIntegrity;

        internal void RegisterSeismicVibration(Vector3 epicenter, float radiusMeters, float impulseMagnitude)
        {
            if (!float.IsFinite(impulseMagnitude) || impulseMagnitude <= 0f || _moduleBuffer.Count <= 0)
                return;

            float safeRadiusMeters = float.IsFinite(radiusMeters) && radiusMeters > 0f ? radiusMeters : 1f;
            float radiusSq = Mathf.Max(1f, safeRadiusMeters * safeRadiusMeters);
            float closestDistanceSq = float.MaxValue;
            float3 epicenter3 = new float3(epicenter.x, epicenter.y, epicenter.z);
            int moduleCount = math.min(_nodeCount, _moduleBuffer.Count);
            for (int nodeIndex = 0; nodeIndex < moduleCount; nodeIndex++)
            {
                BaseModule baseModule = _moduleBuffer[nodeIndex].BaseModule;
                if (baseModule == null || !baseModule.isActiveAndEnabled)
                    continue;

                float distanceSq = math.lengthsq(_moduleBuffer[nodeIndex].Position - epicenter3);
                if (distanceSq < closestDistanceSq)
                    closestDistanceSq = distanceSq;
            }

            if (closestDistanceSq == float.MaxValue)
                return;

            float distanceAttenuation = 1f - math.saturate(closestDistanceSq / radiusSq);
            float vibration01 = math.saturate(distanceAttenuation * impulseMagnitude * HabitatVibrationImpulseScale);
            _habitatVibration01 = math.max(_habitatVibration01, vibration01);
            PublishHabitatVibration();
        }

        private void UpdateHabitatVibration(float deltaTime)
        {
            if (_habitatVibration01 > 0f)
                _habitatVibration01 = math.max(0f, _habitatVibration01 - (HabitatVibrationDecayPerSecond * deltaTime));

            PublishHabitatVibration();
        }

        private void PublishHabitatVibration()
        {
            if (math.abs(_habitatVibration01 - _lastPublishedHabitatVibration01) <= 0.002f)
                return;

            _lastPublishedHabitatVibration01 = _habitatVibration01;
            Shader.SetGlobalFloat(HabitatVibrationId, _habitatVibration01);
        }

        private void EvaluateAnalyticalIntegrityStress(HectonQualityTier scalabilityTier)
        {
            int moduleCount = math.min(_nodeCount, _moduleBuffer.Count);
            if (moduleCount <= 0)
            {
                ResetAnalyticalIntegrityStress(scalabilityTier);
                return;
            }

            if (IsAnalyticalHighScalabilityTier(scalabilityTier))
            {
                EvaluateHighTierAnalyticalIntegrityStress(moduleCount, scalabilityTier);
                return;
            }

            EvaluateLowTierAnalyticalIntegrityStress(moduleCount, scalabilityTier);
        }

        private void EvaluateHighTierAnalyticalIntegrityStress(int moduleCount, HectonQualityTier scalabilityTier)
        {
            float stressSum = 0f;
            float reinforcementSum = 0f;
            float integritySum = 0f;
            float3 centerSum = float3.zero;
            int activeModuleCount = 0;

            for (int nodeIndex = 0; nodeIndex < moduleCount; nodeIndex++)
            {
                ModuleRecord module = _moduleBuffer[nodeIndex];
                BaseModule baseModule = module.BaseModule;
                if (baseModule == null || !baseModule.isActiveAndEnabled || baseModule.IsBreached)
                    continue;

                float moduleIntegrity = math.max(1f, baseModule.MaxIntegrity);
                float moduleStress = ResolveHighTierAnalyticalModuleStress(module, baseModule, moduleIntegrity);
                if (IsAnalyticalGrounded(module))
                    moduleStress *= AnalyticalGroundedStressScale;

                stressSum += moduleStress;
                reinforcementSum += ResolveAnalyticalReinforcementValue(nodeIndex, module, baseModule, moduleIntegrity);
                integritySum += moduleIntegrity;
                centerSum += module.Position;
                activeModuleCount++;
            }

            if (activeModuleCount <= 0)
            {
                ResetAnalyticalIntegrityStress(scalabilityTier);
                return;
            }

            float netStress = math.max(0f, stressSum - reinforcementSum);
            CommitAnalyticalStressResult(moduleCount, activeModuleCount, centerSum, netStress, integritySum, scalabilityTier);
        }

        private void EvaluateLowTierAnalyticalIntegrityStress(int moduleCount, HectonQualityTier scalabilityTier)
        {
            float integritySum = 0f;
            float depthSum = 0f;
            float3 centerSum = float3.zero;
            int activeModuleCount = 0;

            for (int nodeIndex = 0; nodeIndex < moduleCount; nodeIndex++)
            {
                ModuleRecord module = _moduleBuffer[nodeIndex];
                BaseModule baseModule = module.BaseModule;
                if (baseModule == null || !baseModule.isActiveAndEnabled || baseModule.IsBreached)
                    continue;

                integritySum += math.max(1f, baseModule.MaxIntegrity);
                depthSum += ResolveAnalyticalModuleDepthMeters(module, baseModule);
                centerSum += module.Position;
                activeModuleCount++;
            }

            if (activeModuleCount <= 0)
            {
                ResetAnalyticalIntegrityStress(scalabilityTier);
                return;
            }

            float averageDepthMeters = depthSum * math.rcp(activeModuleCount);
            float netStress = integritySum * ResolveAnalyticalDepthScale(averageDepthMeters);
            CommitAnalyticalStressResult(moduleCount, activeModuleCount, centerSum, netStress, integritySum, scalabilityTier);
        }

        private static bool IsAnalyticalHighScalabilityTier(HectonQualityTier scalabilityTier)
        {
            return scalabilityTier == HectonQualityTier.High || scalabilityTier == HectonQualityTier.Ultra;
        }

        private void ResetAnalyticalIntegrityStress(HectonQualityTier scalabilityTier)
        {
            _analyticalStress = 0f;
            _analyticalIntegrity = 0f;
            _analyticalBreachNodeId = 0u;
            Shader.SetGlobalInt(BaseEmergencyStateId, 0);
            PublishAnalyticalStressShader(float3.zero, 0f, 0f, scalabilityTier);
        }

        private void CommitAnalyticalStressResult(
            int moduleCount,
            int activeModuleCount,
            float3 centerSum,
            float netStress,
            float integritySum,
            HectonQualityTier scalabilityTier)
        {
            _analyticalStress = math.isfinite(netStress) ? math.max(0f, netStress) : 0f;
            _analyticalIntegrity = math.isfinite(integritySum) ? math.max(1f, integritySum) : 1f;

            float stress01 = math.saturate(_analyticalStress * math.rcp(_analyticalIntegrity));
            if (_analyticalStress > _analyticalIntegrity)
                TryFlagAnalyticalIntegrityLeak(moduleCount, _analyticalStress);
            else
                _analyticalBreachNodeId = 0u;

            float3 center = centerSum * math.rcp(activeModuleCount);
            float radius = ResolveAnalyticalBaseRadius(center, moduleCount) + AnalyticalShaderRadiusPaddingMeters;
            Shader.SetGlobalInt(
                BaseEmergencyStateId,
                stress01 >= 1f - AnalyticalEmergencyRemainingIntegrityThreshold01 ? 1 : 0);
            PublishAnalyticalStressShader(center, radius, stress01, scalabilityTier);
            TryPublishLowTierAnalyticalStressFeedback(center, stress01, scalabilityTier);
        }

        private float ResolveHighTierAnalyticalModuleStress(ModuleRecord module, BaseModule baseModule, float moduleIntegrity)
        {
            float depthMeters = ResolveAnalyticalModuleDepthMeters(module, baseModule);
            float depthScale = ResolveAnalyticalDepthScale(depthMeters);
            float currentScale = ResolveAnalyticalLocalCurrentScale(module.Position, depthMeters);
            return moduleIntegrity * (depthScale + currentScale);
        }

        private float ResolveAnalyticalModuleDepthMeters(ModuleRecord module, BaseModule baseModule)
        {
            float depthMeters = baseModule.PressureCompressionDepthMeters;
            if (depthMeters <= 0.25f || !math.isfinite(depthMeters))
                depthMeters = ResolveRuntimeDepthMeters(module.Position);

            return math.max(0f, depthMeters);
        }

        private static float ResolveAnalyticalDepthScale(float depthMeters)
        {
            float depth01 = math.saturate(depthMeters * AnalyticalFullStressDepthInv);
            return math.lerp(AnalyticalMinimumDepthWeightScale, AnalyticalMaximumDepthWeightScale, depth01);
        }

        private static float ResolveAnalyticalLocalCurrentScale(float3 runtimePosition, float depthMeters)
        {
            Vector3 current = Hecton8.Physics.CurrentVolume.SampleCombinedCurrent(
                new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            float currentSpeedSq = current.x * current.x + current.y * current.y + current.z * current.z;
            if (!math.isfinite(currentSpeedSq) || currentSpeedSq <= 0.0001f)
                return 0f;

            float current01 = math.saturate(currentSpeedSq * AnalyticalCurrentFullStressInvSq);
            float depth01 = math.saturate(depthMeters * AnalyticalFullStressDepthInv);
            return current01 * depth01 * AnalyticalCurrentStressScale;
        }

        private float ResolveAnalyticalReinforcementValue(int nodeIndex, ModuleRecord module, BaseModule baseModule, float moduleIntegrity)
        {
            float reinforcement = 0f;
            if (module.IsAnchorNode)
                reinforcement += moduleIntegrity * AnalyticalAnchorReinforcementScale;

            if (_anchorReachability.IsCreated &&
                nodeIndex >= 0 &&
                nodeIndex < _anchorReachability.Length &&
                _anchorReachability[nodeIndex] != 0)
            {
                reinforcement += moduleIntegrity * AnalyticalReachableReinforcementScale;
            }

            float yieldMassKilograms = ResolveYieldStrengthNewtons(baseModule) * GravityAccelerationMetersPerSecondSquaredInv;
            if (math.isfinite(yieldMassKilograms) && yieldMassKilograms > 0f)
                reinforcement += math.min(moduleIntegrity * 0.5f, yieldMassKilograms * 0.0005f);

            return reinforcement;
        }

        private float ResolveRuntimeSeaLevelY()
        {
            HectonAtmosphereManager atmosphereManager = ResolveAtmosphereManager();
            return atmosphereManager != null && math.isfinite(atmosphereManager.SeaLevelY)
                ? atmosphereManager.SeaLevelY
                : 0f;
        }

        private HectonAtmosphereManager ResolveAtmosphereManager()
        {
            if (_atmosphereManager != null)
                return _atmosphereManager;

            _atmosphereManager = GlobalRegistry.Atmosphere;
            return _atmosphereManager;
        }

        private float ResolveRuntimeDepthMeters(float3 runtimePosition)
        {
            return math.max(0f, _runtimeSeaLevelY - runtimePosition.y);
        }

        private static bool IsAnalyticalGrounded(ModuleRecord module)
        {
            return IsGroundedHybridSample(module.Position, AnalyticalGroundProbeMeters * 2f);
        }

        private static bool IsGroundedHybridSample(float3 position, float probeMeters)
        {
            if (!VoxelDynamicNavGridRuntime.TrySampleHybridNavigation(position, out VoxelDynamicNavGridRuntime.HybridNavigationSample sample))
                return false;

            if (sample.Mode == VoxelDynamicNavGridRuntime.HybridNavigationMode.SolidVoxel)
                return true;

            if (sample.HasTerrainHeight != 0 && math.abs(position.y - sample.TerrainHeight) <= probeMeters)
                return true;

            return sample.Mode == VoxelDynamicNavGridRuntime.HybridNavigationMode.CaveVoxel &&
                   math.abs(position.y - sample.FloorBoundaryY) <= probeMeters;
        }

        private float ResolveAnalyticalBaseRadius(float3 center, int moduleCount)
        {
            float radiusSq = 1f;
            for (int nodeIndex = 0; nodeIndex < moduleCount; nodeIndex++)
            {
                BaseModule baseModule = _moduleBuffer[nodeIndex].BaseModule;
                if (baseModule == null || !baseModule.isActiveAndEnabled)
                    continue;

                float distanceSq = math.lengthsq(_moduleBuffer[nodeIndex].Position - center);
                if (distanceSq > radiusSq)
                    radiusSq = distanceSq;
            }

            return ResolveFastLengthFromSq(radiusSq);
        }

        private void PublishAnalyticalStressShader(float3 center, float radius, float stress01, HectonQualityTier scalabilityTier)
        {
            float displacementMaxMeters = IsAnalyticalHighScalabilityTier(scalabilityTier)
                ? AnalyticalShaderDisplacementMaxMeters
                : 0f;
            if (_lastPublishedAnalyticalBreachNodeId == _analyticalBreachNodeId &&
                math.abs(stress01 - _lastPublishedAnalyticalStress01) <= AnalyticalShaderStressEpsilon &&
                math.abs(displacementMaxMeters - _lastPublishedAnalyticalDisplacementMaxMeters) <= 0.00001f &&
                stress01 > 0f)
            {
                return;
            }

            _lastPublishedAnalyticalStress01 = stress01;
            _lastPublishedAnalyticalDisplacementMaxMeters = displacementMaxMeters;
            _lastPublishedAnalyticalBreachNodeId = _analyticalBreachNodeId;
            Shader.SetGlobalVector(
                HabitatStressCenterRadiusId,
                new Vector4(center.x, center.y, center.z, math.max(0f, radius)));
            Shader.SetGlobalVector(
                HabitatStressParamsId,
                new Vector4(
                    stress01,
                    displacementMaxMeters,
                    AnalyticalShaderGridScale,
                    (float)(_analyticalBreachNodeId & 1023u)));
        }

        private void UpdateHabitatModuleStressMatrix(float deltaTime, HectonQualityTier moduleStressTier)
        {
            if (!_moduleStressScalars.IsCreated)
            {
                ClearModuleStressState();
                return;
            }

            int moduleCount = math.min(
                math.min(BaseModule.ActiveModuleCount, _moduleStressScalars.Length),
                ModuleStressShaderCapacity);
            if (moduleCount <= 0)
            {
                ClearModuleStressState();
                return;
            }

            int stressCount = moduleCount;
            uint activeOrderHash = ResolveActiveModuleStressOrderHash(stressCount);
            bool orderChanged = activeOrderHash != _moduleStressOrderHash;
            if (orderChanged)
            {
                ClearModuleStressState(false);
                _moduleStressOrderHash = activeOrderHash;
            }

            ConsumeModuleStressSignals(stressCount);

            float safeDeltaTime = math.max(0.0001f, deltaTime);
            float peakStress01 = 0f;
            byte moduleStressTierProfile = ResolveModuleStressQualityTierProfileByte(moduleStressTier);
            bool lowTier = stressCount > 0 && IsModuleStressLowTier(moduleStressTier);
            bool changed = orderChanged ||
                           stressCount != _lastUploadedModuleStressCount ||
                           lowTier != _lastUploadedModuleStressLowTier ||
                           moduleStressTier != _lastUploadedModuleStressTier;
            float3 loudestPosition = float3.zero;
            float loudestDeltaPerSecond = 0f;
            float loudestStress01 = 0f;
            float loudestDepthMeters = 0f;

            for (int nodeIndex = 0; nodeIndex < stressCount; nodeIndex++)
            {
                BaseModule baseModule = BaseModule.GetActiveModuleAt(nodeIndex);
                float stress01 = 0f;
                float depthMeters = 0f;
                float3 modulePosition = float3.zero;
                bool hasGraphRecord = TryResolveGraphModuleRecord(baseModule, nodeIndex, out int graphNodeIndex, out ModuleRecord module);
                if (baseModule != null && baseModule.isActiveAndEnabled)
                {
                    modulePosition = hasGraphRecord ? module.Position : ResolveActiveModulePosition(baseModule);
                    depthMeters = ResolveActiveModuleDepthMeters(baseModule, modulePosition);
                    float floodStress01 = ResolveActiveModuleFloodStress01(baseModule, graphNodeIndex, hasGraphRecord);
                    stress01 = ResolveModuleStress01(nodeIndex, baseModule, depthMeters, floodStress01, safeDeltaTime);
                }

                bool finiteStress = math.isfinite(stress01);
                stress01 = finiteStress ? math.saturate(stress01) : 0f;
                if (!finiteStress)
                {
                    stress01 = 0f;
                    WriteFloodBlackBoxSample(FloodBlackBoxModuleStressInvalidFlag);
                    DumpModuleStressBlackBoxOnce(FloodBlackBoxModuleStressInvalidFlag);
                }

                float previousStress01 = _previousModuleStressScalars[nodeIndex];
                float deltaPerSecond = math.abs(stress01 - previousStress01) * math.rcp(safeDeltaTime);
                if (deltaPerSecond > loudestDeltaPerSecond)
                {
                    loudestDeltaPerSecond = deltaPerSecond;
                    loudestStress01 = stress01;
                    loudestPosition = modulePosition;
                    loudestDepthMeters = depthMeters;
                }

                if (math.abs(stress01 - _moduleStressScalars[nodeIndex]) > ModuleStressUploadEpsilon)
                    changed = true;

                _moduleStressScalars[nodeIndex] = stress01;
                _previousModuleStressScalars[nodeIndex] = stress01;
                peakStress01 = math.max(peakStress01, stress01);

                if (baseModule != null && stress01 >= ModuleStressCompromisedThreshold01)
                    TryPublishBaseModuleCompromisedSignal(nodeIndex, baseModule, module, hasGraphRecord, modulePosition, stress01, peakStress01, depthMeters, moduleStressTier, moduleStressTierProfile);
                else if (nodeIndex < _moduleCompromisedFlags.Length && stress01 < ModuleStressCompromisedThreshold01 * 0.82f)
                    _moduleCompromisedFlags[nodeIndex] = 0;
            }

            _peakModuleStress01 = peakStress01;
            if (math.abs(peakStress01 - _lastUploadedPeakModuleStress01) > ModuleStressUploadEpsilon)
                changed = true;

            if (!orderChanged &&
                loudestDeltaPerSecond >= ModuleStressFastDeltaGroanThresholdPerSecond &&
                loudestStress01 > 0.08f)
            {
                PublishHullStressSignal(
                    new Vector3(loudestPosition.x, loudestPosition.y, loudestPosition.z),
                    loudestStress01,
                    math.saturate(loudestDeltaPerSecond * 0.25f),
                    loudestDepthMeters,
                    1f + (math.saturate(loudestStress01 + loudestDeltaPerSecond * 0.08f) * StructuralGroanPitchRange));
            }

            if (changed)
                UploadModuleStressMatrix(stressCount, peakStress01, moduleStressTier);
        }

        private void ClearModuleStressState()
        {
            ClearModuleStressState(true);
        }

        private void ClearModuleStressState(bool publishShaderClear)
        {
            bool shouldPublishClear = _lastUploadedModuleStressCount != 0 ||
                                      _lastUploadedPeakModuleStress01 > ModuleStressUploadEpsilon ||
                                      _lastUploadedModuleStressLowTier;
            _peakModuleStress01 = 0f;
            _lastUploadedPeakModuleStress01 = 0f;
            _lastUploadedModuleStressCount = 0;
            _lastUploadedModuleStressLowTier = false;
            _lastUploadedModuleStressTier = HectonQualityTier.Unknown;
            _moduleStressOrderHash = 0u;
            if (_moduleStressScalars.IsCreated)
            {
                int clearCount = _moduleStressScalars.Length;
                for (int i = 0; i < clearCount; i++)
                    _moduleStressScalars[i] = 0f;
            }

            if (_previousModuleStressScalars.IsCreated)
            {
                int clearCount = _previousModuleStressScalars.Length;
                for (int i = 0; i < clearCount; i++)
                    _previousModuleStressScalars[i] = 0f;
            }

            if (_moduleImpactStressSpikes.IsCreated)
            {
                int clearCount = _moduleImpactStressSpikes.Length;
                for (int i = 0; i < clearCount; i++)
                    _moduleImpactStressSpikes[i] = 0f;
            }

            if (_moduleCompromisedFlags.IsCreated)
            {
                int clearCount = _moduleCompromisedFlags.Length;
                for (int i = 0; i < clearCount; i++)
                    _moduleCompromisedFlags[i] = 0;
            }

            if (publishShaderClear && shouldPublishClear)
            {
                ReleaseModuleStressBuffer(false);
                PublishModuleStressShader(0, 0f, HectonQualityTier.Unknown);
                _lastUploadedModuleStressCount = 0;
            }
        }

        private float ResolveActiveModuleDepthMeters(BaseModule baseModule, float3 runtimePosition)
        {
            float depthMeters = baseModule != null ? baseModule.PressureCompressionDepthMeters : 0f;
            if (depthMeters <= 0.25f || !math.isfinite(depthMeters))
                depthMeters = ResolveRuntimeDepthMeters(runtimePosition);

            return math.max(0f, depthMeters);
        }

        private static float3 ResolveActiveModulePosition(BaseModule baseModule)
        {
            if (baseModule == null)
                return float3.zero;

            Transform transform = baseModule.transform;
            if (transform == null)
                return float3.zero;

            Vector3 position = transform.position;
            return new float3(position.x, position.y, position.z);
        }

        private bool TryResolveGraphModuleRecord(BaseModule baseModule, out int nodeIndex, out ModuleRecord module)
        {
            return TryResolveGraphModuleRecord(baseModule, -1, out nodeIndex, out module);
        }

        private bool TryResolveGraphModuleRecord(BaseModule baseModule, int indexHint, out int nodeIndex, out ModuleRecord module)
        {
            nodeIndex = -1;
            module = default;
            if (baseModule == null || _moduleBuffer == null)
                return false;

            int moduleCount = math.min(_nodeCount, _moduleBuffer.Count);
            if ((uint)indexHint < (uint)moduleCount)
            {
                ModuleRecord hinted = _moduleBuffer[indexHint];
                if (hinted.BaseModule == baseModule)
                {
                    nodeIndex = indexHint;
                    module = hinted;
                    return true;
                }
            }

            for (int i = 0; i < moduleCount; i++)
            {
                if (i == indexHint)
                    continue;

                ModuleRecord candidate = _moduleBuffer[i];
                if (candidate.BaseModule != baseModule)
                    continue;

                nodeIndex = i;
                module = candidate;
                return true;
            }

            return false;
        }

        private uint ResolveActiveModuleStressOrderHash(int moduleCount)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < moduleCount; i++)
            {
                BaseModule baseModule = BaseModule.GetActiveModuleAt(i);
                bool hasGraphRecord = TryResolveGraphModuleRecord(baseModule, i, out _, out ModuleRecord module);
                uint moduleHash = ResolveModuleStressRuntimeKey(baseModule, module, hasGraphRecord);
                if (moduleHash == 0u)
                    moduleHash = (uint)(i + 1);

                hash ^= moduleHash;
                hash *= 16777619u;
            }

            return hash;
        }

        private float ResolveActiveModuleFloodStress01(BaseModule baseModule, int graphNodeIndex, bool hasGraphRecord)
        {
            if (hasGraphRecord && _roomWaterLevels.IsCreated && (uint)graphNodeIndex < (uint)_roomWaterLevels.Length)
                return math.saturate(_roomWaterLevels[graphNodeIndex]);

            return baseModule != null && baseModule.IsFlooded ? 1f : 0f;
        }

        private float ResolveModuleStress01(int nodeIndex, BaseModule baseModule, float depthMeters, float floodStress01, float deltaTime)
        {
            float depth01 = math.saturate(depthMeters * AnalyticalFullStressDepthInv);
            float ambientPressure01 = math.saturate(ResolveAnalyticalDepthScale(depthMeters) * depth01);
            float integrity01 = baseModule.MaxIntegrity > 0.01f
                ? math.saturate(baseModule.CurrentIntegrity * math.rcp(baseModule.MaxIntegrity))
                : 1f;
            float impactDamage01 = math.saturate(1f - integrity01);
            impactDamage01 = math.max(impactDamage01, math.saturate(baseModule.JointShearStress01));
            impactDamage01 = math.max(impactDamage01, math.saturate(baseModule.PressureCompressionAlpha01));

            impactDamage01 = math.max(impactDamage01, math.saturate(floodStress01) * ModuleStressFloodWeight);

            float spike01 = 0f;
            if (_moduleImpactStressSpikes.IsCreated && nodeIndex < _moduleImpactStressSpikes.Length)
            {
                spike01 = math.saturate(_moduleImpactStressSpikes[nodeIndex]);
                _moduleImpactStressSpikes[nodeIndex] = math.max(0f, spike01 - ModuleStressImpactSpikeDecayPerSecond * deltaTime);
            }

            float stress01 = (ambientPressure01 * ModuleStressDepthWeight) + (impactDamage01 * ModuleStressDamageWeight) + spike01;
            return math.saturate(stress01);
        }

        private void ConsumeModuleStressSignals(int moduleCount)
        {
            int frame = Time.frameCount;
            if (_lastProcessedModuleStressSignalFrame == frame || moduleCount <= 0)
                return;

            _lastProcessedModuleStressSignalFrame = frame;
            ReadOnlySpan<HullDeformedSignal> hullSignals = SignalBus<HullDeformedSignal>.GetFrameSnapshot();
            for (int i = 0; i < hullSignals.Length; i++)
            {
                HullDeformedSignal signal = hullSignals[i];
                if (!IsModuleImpactStressSignal(signal.SourceId, signal.DamageType, signal.Intensity01))
                    continue;

                if (!TryResolveModuleStressIndex(signal.TargetHash, signal.TargetId, float3.zero, false, moduleCount, out int moduleIndex))
                    continue;

                InjectModuleStressSpike(moduleIndex, math.max(signal.Intensity01, signal.Depth));
            }

            ReadOnlySpan<CoreCombatDamageSignal> damageSignals = SignalBus<CoreCombatDamageSignal>.GetFrameSnapshot();
            for (int i = 0; i < damageSignals.Length; i++)
            {
                CoreCombatDamageSignal signal = damageSignals[i];
                if (!IsModuleImpactStressSignal(signal.SourceId, signal.DamageType, signal.Magnitude))
                    continue;

                bool allowNearest = (signal.Flags & CoreCombatDamageSignal.LegacyMirrorFlag) == 0 &&
                                    math.all(math.isfinite(signal.WorldPoint));
                if (!TryResolveModuleStressIndex(signal.TargetHash, signal.TargetId, signal.WorldPoint, allowNearest, moduleCount, out int moduleIndex))
                    continue;

                InjectModuleStressSpike(moduleIndex, signal.Magnitude);
            }
        }

        private static bool IsModuleImpactStressSignal(ushort sourceId, uint damageType, float magnitude)
        {
            if (!math.isfinite(magnitude) || magnitude <= 0f)
                return false;

            if (sourceId == DamageSourceIds.FaunaLeviathanBite)
                return true;

            return (damageType & (CombatDamageTypes.Impact | CombatDamageTypes.Pressure | CombatDamageTypes.MicroFracture)) != 0u;
        }

        private bool TryResolveModuleStressIndex(
            uint targetHash,
            ushort targetId,
            float3 worldPoint,
            bool allowNearest,
            int moduleCount,
            out int moduleIndex)
        {
            moduleIndex = -1;
            if (moduleCount <= 0)
                return false;

            int targetIdMatchIndex = -1;
            int targetIdMatchCount = 0;
            int interiorMatchIndex = -1;
            int nearestMatchIndex = -1;
            bool hasTargetIdentity = targetHash != 0u || targetId != 0;
            bool canResolveNearest = allowNearest && math.all(math.isfinite(worldPoint));
            if (!hasTargetIdentity && !canResolveNearest)
                return false;

            float bestDistanceSq = float.MaxValue;
            Vector3 runtimePoint = canResolveNearest
                ? new Vector3(worldPoint.x, worldPoint.y, worldPoint.z)
                : Vector3.zero;

            for (int nodeIndex = 0; nodeIndex < moduleCount; nodeIndex++)
            {
                BaseModule baseModule = BaseModule.GetActiveModuleAt(nodeIndex);
                if (baseModule == null || !baseModule.isActiveAndEnabled)
                    continue;

                bool hasGraphRecord = false;
                ModuleRecord module = default;
                if (hasTargetIdentity)
                {
                    hasGraphRecord = TryResolveGraphModuleRecord(baseModule, nodeIndex, out _, out module);
                    uint moduleHash = ResolveModuleStressHash(baseModule, module, hasGraphRecord);
                    if (targetHash != 0u && moduleHash == targetHash)
                    {
                        moduleIndex = nodeIndex;
                        return true;
                    }

                    uint entityKey = ResolveModuleStressEntityKey(baseModule);
                    if (targetHash != 0u && entityKey == targetHash)
                    {
                        moduleIndex = nodeIndex;
                        return true;
                    }

                    if (IsModuleStressTargetIdMatch(targetId, moduleHash, entityKey, module, hasGraphRecord))
                    {
                        targetIdMatchIndex = nodeIndex;
                        targetIdMatchCount++;
                    }
                }

                if (!canResolveNearest)
                    continue;

                if (interiorMatchIndex < 0 &&
                    baseModule.TryContainsInteriorRuntimePoint(runtimePoint))
                {
                    if (!hasTargetIdentity)
                    {
                        moduleIndex = nodeIndex;
                        return true;
                    }

                    interiorMatchIndex = nodeIndex;
                    continue;
                }

                if (interiorMatchIndex >= 0)
                    continue;

                if (!hasTargetIdentity)
                    hasGraphRecord = TryResolveGraphModuleRecord(baseModule, nodeIndex, out _, out module);

                float3 modulePosition = hasGraphRecord ? module.Position : ResolveActiveModulePosition(baseModule);
                float allowedRadiusMeters = ModuleStressNearestSignalFallbackRadiusMeters;
                if (baseModule.TryGetInteriorHazardBounds(out Vector3 interiorCenter, out float interiorRadius) &&
                    math.all(math.isfinite((float3)interiorCenter)) &&
                    math.isfinite(interiorRadius) &&
                    interiorRadius > 0f)
                {
                    modulePosition = (float3)interiorCenter;
                    allowedRadiusMeters = math.max(ModuleStressNearestSignalFallbackRadiusMeters, interiorRadius);
                }

                allowedRadiusMeters = math.min(
                    ModuleStressNearestSignalMaxRadiusMeters,
                    allowedRadiusMeters + ModuleStressNearestSignalPaddingMeters);
                float distanceSq = math.lengthsq(modulePosition - worldPoint);
                float allowedDistanceSq = allowedRadiusMeters * allowedRadiusMeters;
                if (distanceSq > allowedDistanceSq || distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                nearestMatchIndex = nodeIndex;
            }

            if (targetIdMatchCount == 1)
            {
                moduleIndex = targetIdMatchIndex;
                return true;
            }

            if (interiorMatchIndex >= 0)
            {
                moduleIndex = interiorMatchIndex;
                return true;
            }

            if (nearestMatchIndex >= 0)
            {
                moduleIndex = nearestMatchIndex;
                return true;
            }

            return false;
        }

        private static uint ResolveModuleStressHash(BaseModule baseModule, ModuleRecord module, bool hasGraphRecord)
        {
            if (hasGraphRecord && module.Marker != null && module.Marker.Data != null)
                return unchecked((uint)module.Marker.Data.ModuleHashId);

            if (hasGraphRecord)
                return module.NodeId;

            return 0u;
        }

        private static uint ResolveModuleStressRuntimeKey(BaseModule baseModule, ModuleRecord module, bool hasGraphRecord)
        {
            uint stableHash = ResolveModuleStressHash(baseModule, module, hasGraphRecord);
            if (stableHash != 0u || baseModule == null)
                return stableHash;

            return ResolveModuleStressEntityKey(baseModule);
        }

        private static bool IsModuleStressTargetIdMatch(
            ushort targetId,
            uint moduleHash,
            uint entityKey,
            ModuleRecord module,
            bool hasGraphRecord)
        {
            if (targetId == 0)
                return false;

            if (moduleHash != 0u && (ushort)(moduleHash & 0xFFFFu) == targetId)
                return true;

            if (entityKey != 0u && (ushort)(entityKey & 0xFFFFu) == targetId)
                return true;

            return hasGraphRecord && (ushort)(module.NodeId & 0xFFFFu) == targetId;
        }

        private static uint ResolveModuleStressEntityKey(BaseModule baseModule)
        {
            if (baseModule == null)
                return 0u;

            uint entityKey = unchecked((uint)EntityId.ToULong(baseModule.GetEntityId()));
            return entityKey != 0u ? entityKey : 0u;
        }

        private void InjectModuleStressSpike(int moduleIndex, float magnitude)
        {
            if (!_moduleImpactStressSpikes.IsCreated || (uint)moduleIndex >= (uint)_moduleImpactStressSpikes.Length)
                return;

            float spike01 = math.saturate(math.max(0f, magnitude) * ModuleStressImpactSpikeStrength);
            _moduleImpactStressSpikes[moduleIndex] = math.max(_moduleImpactStressSpikes[moduleIndex], spike01);
        }

        private void TryPublishBaseModuleCompromisedSignal(
            int moduleIndex,
            BaseModule baseModule,
            ModuleRecord module,
            bool hasGraphRecord,
            float3 modulePosition,
            float stress01,
            float peakStress01,
            float depthMeters,
            HectonQualityTier tier,
            byte tierProfile)
        {
            if (!_moduleCompromisedFlags.IsCreated || (uint)moduleIndex >= (uint)_moduleCompromisedFlags.Length)
                return;

            if (_moduleCompromisedFlags[moduleIndex] != 0)
                return;

            _moduleCompromisedFlags[moduleIndex] = 1;
            BaseModuleCompromisedSignal signal = new BaseModuleCompromisedSignal
            {
                ModuleCenter = modulePosition,
                Stress01 = math.saturate(stress01),
                PeakStress01 = math.saturate(peakStress01),
                DepthMeters = math.max(0f, depthMeters),
                NodeId = hasGraphRecord ? module.NodeId : 0u,
                ModuleHash = ResolveModuleStressRuntimeKey(baseModule, module, hasGraphRecord),
                Frame = unchecked((uint)Time.frameCount),
                Sequence = ++_moduleStressSequence,
                SourceId = DamageSourceIds.HabitatIntegrity,
                Flags = IsModuleStressLowTier(tier)
                    ? BaseModuleCompromisedSignal.LowTierVisualOnlyFlag
                    : BaseModuleCompromisedSignal.MaxDeformationFlag,
                StressIndex = (byte)math.min(byte.MaxValue, moduleIndex),
                QualityTier = tierProfile
            };
            GlobalSignals.Publish(in signal);
        }

        private void UploadModuleStressMatrix(int moduleCount, float peakStress01, HectonQualityTier tier)
        {
            int safeModuleCount = math.max(0, moduleCount);
            bool lowTier = safeModuleCount > 0 && IsModuleStressLowTier(tier);
            bool hasVisibleStress = safeModuleCount > 0 && !lowTier && peakStress01 > ModuleStressUploadEpsilon;
            if (hasVisibleStress)
            {
                EnsureModuleStressBuffer(safeModuleCount);
                if (_moduleStressBuffer != null)
                {
                    GraphicsBufferUploadUtility.UploadNativeArray(_moduleStressBuffer, _moduleStressScalars, safeModuleCount);
                    Shader.SetGlobalBuffer(HabitatModuleStressBufferId, _moduleStressBuffer);
                }
            }

            PublishModuleStressShader(safeModuleCount, peakStress01, tier);
            _lastUploadedModuleStressCount = safeModuleCount;
            _lastUploadedPeakModuleStress01 = peakStress01;
            _lastUploadedModuleStressLowTier = lowTier;
            _lastUploadedModuleStressTier = tier;
            _moduleStressSequence++;
        }

        private void PublishModuleStressShader(int moduleCount, float peakStress01, HectonQualityTier tier)
        {
            int safeModuleCount = math.max(0, moduleCount);
            bool lowTier = safeModuleCount > 0 && IsModuleStressLowTier(tier);
            float displacementMaxMeters = safeModuleCount > 0
                ? ResolveModuleStressDisplacementMaxMeters(tier)
                : 0f;
            Shader.SetGlobalVector(
                HabitatModuleStressParamsId,
                new Vector4(
                    safeModuleCount,
                    displacementMaxMeters,
                    lowTier ? 1f : 0f,
                    math.saturate(peakStress01)));
        }

        private void EnsureModuleStressBuffer(int moduleCount)
        {
            int safeCount = math.max(1, moduleCount);
            if (_moduleStressBuffer != null && _moduleStressBuffer.count >= safeCount)
                return;

            ReleaseModuleStressBuffer(false);
            _moduleStressBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float>(
                NextPowerOfTwo(math.max(safeCount, InitialNodeCapacity)));
            _lastUploadedModuleStressCount = -1;
        }

        private static bool IsModuleStressLowTier(HectonQualityTier tier)
        {
            return tier == HectonQualityTier.Low ||
                   tier == HectonQualityTier.Mx350 ||
                   tier == HectonQualityTier.Unknown;
        }

        private static float ResolveModuleStressDisplacementMaxMeters(HectonQualityTier tier)
        {
            switch (tier)
            {
                case HectonQualityTier.Mid:
                    return ModuleStressMidDisplacementMaxMeters;
                case HectonQualityTier.High:
                    return AnalyticalShaderDisplacementMaxMeters;
                case HectonQualityTier.Ultra:
                    return ModuleStressUltraDisplacementMaxMeters;
                default:
                    return 0f;
            }
        }

        private static byte ResolveModuleStressQualityTierProfileByte(HectonQualityTier tier)
        {
            return tier == HectonQualityTier.High || tier == HectonQualityTier.Ultra
                ? (byte)1
                : (byte)0;
        }

        private void TryPublishLowTierAnalyticalStressFeedback(float3 center, float stress01, HectonQualityTier scalabilityTier)
        {
            if (IsAnalyticalHighScalabilityTier(scalabilityTier) ||
                stress01 < AnalyticalLowTierFeedbackThreshold01)
            {
                return;
            }

            float now = Time.time;
            if (now < _nextAnalyticalLowTierFeedbackTime)
                return;

            _nextAnalyticalLowTierFeedbackTime = now + AnalyticalLowTierFeedbackCooldownSeconds;
            Vector3 worldPosition = new Vector3(center.x, center.y, center.z);
            float safeStress = math.saturate(stress01);
            float pressureDelta = safeStress;
            float depthMeters = 0f;
            if (TryResolveMostStressedRoomPosition(out Vector3 stressedRoomPosition, out float roomStress01, out float roomDepthMeters))
            {
                worldPosition = stressedRoomPosition;
                safeStress = math.max(safeStress, roomStress01);
                pressureDelta = math.max(pressureDelta, roomStress01);
                depthMeters = roomDepthMeters;
            }

            PublishHullStressSignal(
                worldPosition,
                safeStress,
                pressureDelta,
                depthMeters,
                1f + (safeStress * StructuralGroanPitchRange));

            CameraJuiceSignals.PublishImpact(
                math.saturate(safeStress * 0.35f),
                worldPosition,
                Vector3.zero);
        }

        private bool TryResolveMostStressedRoomPosition(out Vector3 position, out float stress01, out float depthMeters)
        {
            position = default;
            stress01 = 0f;
            depthMeters = 0f;
            if (_moduleBuffer == null || _moduleBuffer.Count <= 0)
                return false;

            int moduleCount = math.min(math.max(0, _nodeCount), _moduleBuffer.Count);
            for (int nodeIndex = 0; nodeIndex < moduleCount; nodeIndex++)
            {
                ModuleRecord module = _moduleBuffer[nodeIndex];
                BaseModule baseModule = module.BaseModule;
                if (baseModule == null || !baseModule.isActiveAndEnabled)
                    continue;

                float jointStress01 = math.saturate(baseModule.JointShearStress01);
                float compressionStress01 = math.saturate(baseModule.PressureCompressionAlpha01);
                float floodStress01 = _roomWaterLevels.IsCreated && nodeIndex < _roomWaterLevels.Length
                    ? math.saturate(_roomWaterLevels[nodeIndex])
                    : 0f;
                float candidateStress01 = math.max(jointStress01, math.max(compressionStress01, floodStress01));
                if (!math.isfinite(candidateStress01) || candidateStress01 <= stress01)
                    continue;

                stress01 = candidateStress01;
                position = new Vector3(module.Position.x, module.Position.y, module.Position.z);
                depthMeters = ResolveAnalyticalModuleDepthMeters(module, baseModule);
            }

            return stress01 > 0f;
        }

        private void PublishHullStressSignal(
            Vector3 worldPosition,
            float stress01,
            float pressureDelta,
            float depthMeters,
            float pitchScale)
        {
            HullStressSignal signal = new HullStressSignal(
                worldPosition,
                stress01,
                pressureDelta,
                depthMeters,
                pitchScale);
            IAudioService audioService = ResolveAudioService();
            if (audioService != null && audioService.QueueHullStressSignal(in signal))
                return;

            ProceduralAudioEvents.RaiseHullStressSignal(in signal);
        }

        private IAudioService ResolveAudioService()
        {
            if (_audioService != null)
                return _audioService;

            _audioService = GlobalRegistry.Audio;
            return _audioService;
        }

        private void TryFlagAnalyticalIntegrityLeak(int moduleCount, float stress)
        {
            if (_analyticalBreachNodeId != 0u && ContainsAnalyticalBreachNode(moduleCount, _analyticalBreachNodeId))
                return;

            byte threshold = ResolveAnalyticalBreachThreshold(stress);
            uint timeSeconds = (uint)Mathf.Max(0, Mathf.FloorToInt(Time.time));
            int startIndex = moduleCount > 0 ? (int)(timeSeconds % (uint)moduleCount) : 0;
            for (int offset = 0, nodeIndex = startIndex; offset < moduleCount; offset++, nodeIndex++)
            {
                if (nodeIndex == moduleCount)
                    nodeIndex = 0;

                ModuleRecord module = _moduleBuffer[nodeIndex];
                BaseModule baseModule = module.BaseModule;
                if (baseModule == null ||
                    !baseModule.isActiveAndEnabled ||
                    baseModule.IsBreached ||
                    baseModule.CurrentFailureMode != BaseModuleFailureMode.None)
                {
                    continue;
                }

                uint baseIdHash = ResolveAnalyticalBaseIdHash(nodeIndex, module);
                if (!PassDeterministicAnalyticalBreachGate(baseIdHash, timeSeconds, threshold))
                    continue;

                baseModule.SetState(
                    baseModule.CurrentIntegrity,
                    baseModule.IsFlooded,
                    BaseModuleFailureMode.OxygenLeak,
                    baseModule.MaxRecoverableIntegrity,
                    baseModule.AirReserveNormalized,
                    baseModule.Co2Normalized,
                    baseModule.FloodedReefFloodSeconds,
                    baseModule.InteriorReefInfestationActive);
                _analyticalBreachNodeId = ResolveAnalyticalNodeKey(nodeIndex, module.NodeId);
                return;
            }
        }

        private byte ResolveAnalyticalBreachThreshold(float stress)
        {
            float overshoot01 = _analyticalIntegrity > 1f
                ? math.saturate((stress - _analyticalIntegrity) * math.rcp(_analyticalIntegrity))
                : 0f;
            int threshold = Mathf.RoundToInt(Mathf.Lerp(
                AnalyticalBreachMinimumThreshold,
                AnalyticalBreachMaximumThreshold,
                overshoot01));
            return (byte)Mathf.Clamp(threshold, 1, 255);
        }

        private static bool PassDeterministicAnalyticalBreachGate(uint baseIdHash, uint timeSeconds, byte threshold)
        {
            return ((baseIdHash ^ timeSeconds) & 255u) < threshold;
        }

        private static uint ResolveAnalyticalBaseIdHash(int nodeIndex, ModuleRecord module)
        {
            uint hash = 2166136261u;
            hash = FoldAnalyticalBaseHash(hash, ResolveAnalyticalNodeKey(nodeIndex, module.NodeId));
            if (module.Marker != null && module.Marker.Data != null)
                hash = FoldAnalyticalBaseHash(hash, (uint)module.Marker.Data.ModuleHashId);

            uint x = math.asuint(module.Position.x);
            uint y = math.asuint(module.Position.y);
            uint z = math.asuint(module.Position.z);
            hash = FoldAnalyticalBaseHash(hash, x);
            hash = FoldAnalyticalBaseHash(hash, y);
            hash = FoldAnalyticalBaseHash(hash, z);
            return hash;
        }

        private static uint FoldAnalyticalBaseHash(uint hash, uint value)
        {
            return (hash ^ value) * 16777619u;
        }

        private bool ContainsAnalyticalBreachNode(int moduleCount, uint nodeId)
        {
            for (int nodeIndex = 0; nodeIndex < moduleCount; nodeIndex++)
            {
                ModuleRecord module = _moduleBuffer[nodeIndex];
                BaseModule baseModule = module.BaseModule;
                if (ResolveAnalyticalNodeKey(nodeIndex, module.NodeId) == nodeId &&
                    baseModule != null &&
                    baseModule.isActiveAndEnabled &&
                    baseModule.CurrentFailureMode != BaseModuleFailureMode.None)
                {
                    return true;
                }
            }

            return false;
        }

        private static uint ResolveAnalyticalNodeKey(int nodeIndex, uint nodeId)
        {
            return nodeId != 0u ? nodeId : (uint)(nodeIndex + 1);
        }

        private uint ComposeAnalyticalBreachSeed(int moduleCount, float stress)
        {
            uint seed = unchecked((uint)moduleCount * 1664525u + 1013904223u);
            seed ^= (uint)math.clamp((int)math.round(math.max(0f, stress) * 17f), 0, int.MaxValue);
            for (int nodeIndex = 0; nodeIndex < moduleCount; nodeIndex++)
                seed = unchecked(seed * 1664525u + ResolveAnalyticalNodeKey(nodeIndex, _moduleBuffer[nodeIndex].NodeId) + 1013904223u);

            return seed;
        }

        private void ApplyGraphFluidIncursion(float deltaTime, HectonQualityTier scalabilityTier)
        {
            if (deltaTime <= 0f ||
                _nodeCount <= 0 ||
                !_edgeOffsets.IsCreated ||
                !_edgeDestinations.IsCreated ||
                !_edgeResistance.IsCreated ||
                !_traversalVisited.IsCreated ||
                !_anchorTraversalQueue.IsCreated)
            {
                return;
            }

            int moduleCount = math.min(_nodeCount, _moduleBuffer.Count);
            if (moduleCount <= 0)
                return;

            int graphFloodNodeBudget = ResolveGraphFloodNodeBudget(scalabilityTier);
            bool anyFloodStateChanged = false;
            int seedBudget = moduleCount > graphFloodNodeBudget
                ? graphFloodNodeBudget
                : moduleCount;
            int startNodeIndex = moduleCount > graphFloodNodeBudget
                ? math.clamp(_graphFloodSliceCursor, 0, moduleCount - 1)
                : 0;

            for (int offset = 0; offset < seedBudget; offset++)
            {
                int nodeIndex = startNodeIndex + offset;
                if (nodeIndex >= moduleCount)
                    nodeIndex -= moduleCount;

                ModuleRecord module = _moduleBuffer[nodeIndex];
                BaseModule baseModule = module.BaseModule;
                if (baseModule == null || !baseModule.isActiveAndEnabled)
                    continue;

                float thermalPressureScale = ResolveThermalGasPressureScale(baseModule);
                float airPocketPressureAtm = baseModule.ResolveGraphBoyleAirPocketPressureAtm(thermalPressureScale);
                baseModule.ApplyGraphAirPocketCompressionStress(airPocketPressureAtm, deltaTime);
                anyFloodStateChanged |= baseModule.TryExtinguishFloodedFire();

                if (baseModule.IsGraphBreachIngressSource)
                {
                    float pressureDeltaKPa = ResolveGraphIngressPressureDeltaKPa(module, baseModule, airPocketPressureAtm);
                    float ingressM3 = baseModule.ApplyGraphPressureIngress(
                        deltaTime,
                        ResolvePressureRootLut(pressureDeltaKPa, scalabilityTier));
                    anyFloodStateChanged |= ingressM3 > GraphFloodWaterEpsilonM3;
                }
            }

            if (moduleCount > graphFloodNodeBudget)
                _graphFloodSliceCursor = (startNodeIndex + seedBudget) % moduleCount;
            else
                _graphFloodSliceCursor = 0;

            SyncFloodRoomStateSnapshot();
            if (anyFloodStateChanged)
                PublishEmergencyLockdownState();

            anyFloodStateChanged |= RunFloodPropagationJob(moduleCount, startNodeIndex, seedBudget, deltaTime);

            if (anyFloodStateChanged)
                PublishEmergencyLockdownState();
        }

        private bool RunFloodPropagationJob(int moduleCount, int startNodeIndex, int processNodeCount, float deltaTime)
        {
            if (moduleCount <= 0 ||
                deltaTime <= 0f ||
                !_roomWaterLevels.IsCreated ||
                !_roomVolumes.IsCreated ||
                !_roomFlags.IsCreated ||
                !_roomFloodDeltaLevels.IsCreated ||
                !_edgeFlags.IsCreated ||
                !_roomConnections.IsCreated ||
                !_floodPropagationSummary.IsCreated)
            {
                return false;
            }

            _floodPropagationSummary[0] = default;
            HabitatFloodPropagationJob job = new HabitatFloodPropagationJob
            {
                NodeCount = moduleCount,
                EdgeCount = math.min(_edgeCount, _edgeFlags.Length),
                StartNodeIndex = startNodeIndex,
                ProcessNodeCount = processNodeCount,
                DeltaTime = deltaTime,
                FlowRate01PerSecond = GraphFloodTransferRateM3PerSecond,
                MaxTransferPerEdgeM3 = GraphFloodMaxTransferPerEdgeM3,
                WaterEpsilon01 = GraphFloodWaterEpsilonM3,
                RoomWaterLevels = _roomWaterLevels,
                RoomVolumes = _roomVolumes,
                RoomFlags = _roomFlags,
                EdgeFlags = _edgeFlags,
                Connections = _roomConnections,
                RoomDeltaLevels = _roomFloodDeltaLevels,
                Result = _floodPropagationSummary
            };

            job.Run();

            HabitatFloodPropagationSummary summary = _floodPropagationSummary[0];
            if (summary.NonFiniteCount > 0)
            {
                WriteFloodBlackBoxSample(FloodBlackBoxNonFiniteFlag);
                DumpFloodBlackBoxOnce(FloodBlackBoxNonFiniteFlag);
            }
            if (summary.InvalidConnectionCount > 0)
            {
                WriteFloodBlackBoxSample(FloodBlackBoxTopologyInvalidFlag);
            }

            bool changed = ApplyFloodPropagationDeltas(moduleCount);
            return changed || summary.FlowedEdgeCount > 0;
        }

        private bool ApplyFloodPropagationDeltas(int moduleCount)
        {
            int safeCount = math.min(
                math.min(moduleCount, _moduleBuffer.Count),
                math.min(_roomFloodDeltaLevels.Length, _roomVolumes.Length));
            bool changed = false;

            for (int nodeIndex = 0; nodeIndex < safeCount; nodeIndex++)
            {
                float deltaLevel01 = _roomFloodDeltaLevels[nodeIndex];
                if (!math.isfinite(deltaLevel01) || deltaLevel01 == 0f)
                    continue;

                BaseModule baseModule = _moduleBuffer[nodeIndex].BaseModule;
                if (baseModule == null || !baseModule.isActiveAndEnabled)
                    continue;

                float roomVolumeM3 = math.max(0.001f, _roomVolumes[nodeIndex]);
                float deltaVolumeM3 = deltaLevel01 * roomVolumeM3;
                if (!math.isfinite(deltaVolumeM3) || math.abs(deltaVolumeM3) <= GraphFloodWaterEpsilonM3)
                    continue;

                float appliedVolumeM3 = deltaVolumeM3 > 0f
                    ? baseModule.AddWaterVolumeM3(deltaVolumeM3)
                    : baseModule.DrainWaterVolumeM3(-deltaVolumeM3);
                if (appliedVolumeM3 <= GraphFloodWaterEpsilonM3)
                    continue;

                baseModule.TryExtinguishFloodedFire();
                changed = true;
            }

            return changed;
        }

        private bool CanGraphFluidTraverseEdge(int sourceNodeIndex, int destinationNodeIndex, int csrEdgeIndex)
        {
            if (csrEdgeIndex < 0 ||
                csrEdgeIndex >= _edgeDestinations.Length ||
                csrEdgeIndex >= _edgeResistance.Length ||
                _edgeDestinations[csrEdgeIndex] < 0 ||
                _edgeResistance[csrEdgeIndex] <= 0f)
            {
                return false;
            }

            if (IsFloodEdgeSealed(csrEdgeIndex))
                return false;

            BaseModule sourceModule = _moduleBuffer[sourceNodeIndex].BaseModule;
            BaseModule destinationModule = _moduleBuffer[destinationNodeIndex].BaseModule;
            if (sourceModule == null || destinationModule == null)
                return false;

            return !sourceModule.IsEmergencyBulkheadLockedDown &&
                   !destinationModule.IsEmergencyBulkheadLockedDown;
        }

        private bool IsFloodEdgeSealed(int csrEdgeIndex)
        {
            return _edgeFlags.IsCreated &&
                   csrEdgeIndex >= 0 &&
                   csrEdgeIndex < _edgeFlags.Length &&
                   (_edgeFlags[csrEdgeIndex] & (byte)HabitatEdgeFloodFlags.Sealed) != 0;
        }

        private bool IsFloodAutoSealActive(int nodeIndex, BaseModule baseModule)
        {
            return baseModule != null &&
                   baseModule.HasPower &&
                   ResolveAuthoritativeRoomWaterLevel01(nodeIndex, baseModule) > GraphFloodAutoSealThreshold01;
        }

        private float ResolveAuthoritativeRoomWaterLevel01(int nodeIndex, BaseModule baseModule)
        {
            if (_roomWaterLevels.IsCreated &&
                nodeIndex >= 0 &&
                nodeIndex < _roomWaterLevels.Length)
            {
                float roomLevel01 = _roomWaterLevels[nodeIndex];
                return math.isfinite(roomLevel01) ? math.saturate(roomLevel01) : 0f;
            }

            if (baseModule == null)
                return 0f;

            float roomCapacityM3 = math.max(0.001f, baseModule.ResolveFloodCapacityM3());
            float waterVolumeM3 = baseModule.WaterVolumeM3;
            if (!math.isfinite(roomCapacityM3) || !math.isfinite(waterVolumeM3))
                return 0f;

            return math.saturate(waterVolumeM3 * math.rcp(roomCapacityM3));
        }

        private void SetFloodEdgeFlag(int csrEdgeIndex, HabitatEdgeFloodFlags flag)
        {
            if (!_edgeFlags.IsCreated ||
                csrEdgeIndex < 0 ||
                csrEdgeIndex >= _edgeFlags.Length)
            {
                return;
            }

            _edgeFlags[csrEdgeIndex] = (byte)(_edgeFlags[csrEdgeIndex] | (byte)flag);
        }

        private void ClearActiveFloodEdgeFlags()
        {
            if (!_edgeFlags.IsCreated)
                return;

            int edgeCount = math.min(math.max(0, _edgeCount), _edgeFlags.Length);
            for (int edgeIndex = 0; edgeIndex < edgeCount; edgeIndex++)
                _edgeFlags[edgeIndex] = 0;
        }

        private void SyncFloodRoomStateSnapshot()
        {
            if (!_roomWaterLevels.IsCreated ||
                !_roomVolumes.IsCreated ||
                !_roomFlags.IsCreated)
            {
                return;
            }

            int moduleCount = math.min(
                math.min(_nodeCount, _moduleBuffer.Count),
                math.min(_roomWaterLevels.Length, math.min(_roomVolumes.Length, _roomFlags.Length)));
            int floodedRoomCount = 0;
            float totalWaterVolumeM3 = 0f;
            float maxWaterLevel01 = 0f;
            float floodPressureStress = 0f;
            uint blackBoxFlags = 0u;
            uint stateHash = 2166136261u;

            for (int nodeIndex = 0; nodeIndex < moduleCount; nodeIndex++)
            {
                ModuleRecord module = _moduleBuffer[nodeIndex];
                BaseModule baseModule = module.BaseModule;
                float roomWaterLevel01 = 0f;
                float roomVolumeM3 = 0f;
                float roomWaterVolumeM3 = 0f;
                HabitatRoomFloodFlags roomFlags = HabitatRoomFloodFlags.None;

                if (baseModule != null && baseModule.isActiveAndEnabled)
                {
                    roomVolumeM3 = math.max(0.001f, baseModule.ResolveFloodCapacityM3());
                    roomWaterVolumeM3 = baseModule.WaterVolumeM3;
                    if (!math.isfinite(roomVolumeM3) || !math.isfinite(roomWaterVolumeM3))
                    {
                        roomVolumeM3 = 0.001f;
                        roomWaterVolumeM3 = 0f;
                        blackBoxFlags |= FloodBlackBoxNonFiniteFlag;
                    }
                    else if (roomWaterVolumeM3 > roomVolumeM3 + GraphFloodWaterEpsilonM3)
                    {
                        roomWaterVolumeM3 = roomVolumeM3;
                        roomFlags |= HabitatRoomFloodFlags.OverflowClamped;
                        blackBoxFlags |= FloodBlackBoxOverflowClampedFlag;
                    }

                    float volumeLevel01 = roomVolumeM3 > GraphFloodWaterEpsilonM3
                        ? math.saturate(roomWaterVolumeM3 * math.rcp(roomVolumeM3))
                        : 0f;
                    roomWaterLevel01 = volumeLevel01;
                    if (baseModule.IsGraphBreachIngressSource)
                        roomFlags |= HabitatRoomFloodFlags.Breached;
                    if (baseModule.HasPower)
                        roomFlags |= HabitatRoomFloodFlags.Powered;
                    if (roomWaterLevel01 >= GraphFloodOxygenDisabledThreshold01)
                    {
                        roomFlags |= HabitatRoomFloodFlags.Flooded | HabitatRoomFloodFlags.OxygenDisabled;
                        floodedRoomCount++;
                    }

                    float depthMeters = ResolveAnalyticalModuleDepthMeters(module, baseModule);
                    float pressureKPa = math.max(0f, depthMeters * HydrostaticPressureKPaPerMeter);
                    floodPressureStress += roomWaterLevel01 * roomVolumeM3 * pressureKPa;
                    totalWaterVolumeM3 += roomWaterVolumeM3;
                    maxWaterLevel01 = math.max(maxWaterLevel01, roomWaterLevel01);
                }

                _roomWaterLevels[nodeIndex] = roomWaterLevel01;
                _roomVolumes[nodeIndex] = roomVolumeM3;
                _roomFlags[nodeIndex] = (byte)roomFlags;

                stateHash = HashFloodBlackBox(stateHash, module.NodeId);
                stateHash = HashFloodBlackBox(stateHash, (uint)_roomFlags[nodeIndex]);
                stateHash = HashFloodBlackBox(stateHash, QuantizeFloodBlackBoxFloat(roomWaterLevel01));
                stateHash = HashFloodBlackBox(stateHash, QuantizeFloodBlackBoxFloat(roomVolumeM3));
            }

            _floodedRoomCount = floodedRoomCount;
            _totalRoomWaterVolumeM3 = math.isfinite(totalWaterVolumeM3) ? math.max(0f, totalWaterVolumeM3) : 0f;
            _maxRoomWaterLevel01 = math.isfinite(maxWaterLevel01) ? math.saturate(maxWaterLevel01) : 0f;

            float analyticalStress = math.isfinite(_analyticalStress) ? math.max(0f, _analyticalStress) : 0f;
            if (!math.isfinite(floodPressureStress))
            {
                floodPressureStress = 0f;
                blackBoxFlags |= FloodBlackBoxNonFiniteFlag;
            }

            _baseTotalStress = analyticalStress + math.max(0f, floodPressureStress);
            if (!math.isfinite(_baseTotalStress))
            {
                _baseTotalStress = analyticalStress;
                blackBoxFlags |= FloodBlackBoxNonFiniteFlag;
            }

            if (_edgeFlags.IsCreated)
            {
                int edgeFlagCount = math.min(math.max(0, _edgeCount), _edgeFlags.Length);
                stateHash = HashFloodBlackBox(stateHash, (uint)edgeFlagCount);
                for (int edgeIndex = 0; edgeIndex < edgeFlagCount; edgeIndex++)
                    stateHash = HashFloodBlackBox(stateHash, _edgeFlags[edgeIndex]);
            }
            else
            {
                stateHash = HashFloodBlackBox(stateHash, 0u);
            }

            stateHash = HashFloodBlackBox(stateHash, (uint)floodedRoomCount);
            stateHash = HashFloodBlackBox(stateHash, QuantizeFloodBlackBoxFloat(_baseTotalStress));
            stateHash = HashFloodBlackBox(stateHash, QuantizeFloodBlackBoxFloat(_totalRoomWaterVolumeM3));
            _floodBlackBoxStateHash = stateHash;

            if ((blackBoxFlags & FloodBlackBoxNonFiniteFlag) != 0u)
            {
                WriteFloodBlackBoxSample(blackBoxFlags);
                DumpFloodBlackBoxOnce(blackBoxFlags);
            }
        }

        private void ClearFloodRoomStateSnapshot()
        {
            _floodedRoomCount = 0;
            _baseTotalStress = 0f;
            _maxRoomWaterLevel01 = 0f;
            _totalRoomWaterVolumeM3 = 0f;
            _floodBlackBoxStateHash = 0u;

            if (_roomWaterLevels.IsCreated && _roomVolumes.IsCreated && _roomFlags.IsCreated)
            {
                int clearCount = math.min(_roomWaterLevels.Length, math.min(_roomVolumes.Length, _roomFlags.Length));
                for (int nodeIndex = 0; nodeIndex < clearCount; nodeIndex++)
                {
                    _roomWaterLevels[nodeIndex] = 0f;
                    _roomVolumes[nodeIndex] = 0f;
                    _roomFlags[nodeIndex] = 0;
                    if (_roomFloodDeltaLevels.IsCreated && nodeIndex < _roomFloodDeltaLevels.Length)
                        _roomFloodDeltaLevels[nodeIndex] = 0f;
                }
            }

            if (_roomConnections.IsCreated)
                _roomConnections.Clear();
        }

        private void WriteFloodBlackBoxSample(uint reasonFlags)
        {
            if (!_floodBlackBox.IsCreated || _floodBlackBox.Length <= 0)
                return;

            uint flags = reasonFlags;
            if (!math.isfinite(_baseTotalStress) ||
                !math.isfinite(_maxRoomWaterLevel01) ||
                !math.isfinite(_totalRoomWaterVolumeM3))
            {
                flags |= FloodBlackBoxNonFiniteFlag;
            }

            int cursor = _floodBlackBoxCursor;
            if ((uint)cursor >= (uint)_floodBlackBox.Length)
                cursor = 0;

            _floodBlackBox[cursor] = new HabitatFloodBlackBoxEntry
            {
                Frame = Time.frameCount,
                NodeCount = (ushort)math.min(ushort.MaxValue, math.max(0, _nodeCount)),
                EdgeCount = (ushort)math.min(ushort.MaxValue, math.max(0, _edgeCount)),
                FloodedRoomCount = (ushort)math.min(ushort.MaxValue, math.max(0, _floodedRoomCount)),
                Reserved0 = 0,
                BaseTotalStress = _baseTotalStress,
                MaxWaterLevel01 = _maxRoomWaterLevel01,
                TotalWaterVolumeM3 = _totalRoomWaterVolumeM3,
                PeakModuleStress = _peakModuleStress01,
                Flags = flags,
                StateHash = _floodBlackBoxStateHash,
                DeformationSequence = _moduleStressSequence
            };

            _floodBlackBoxCursor = (cursor + 1) % _floodBlackBox.Length;
            if ((flags & FloodBlackBoxNonFiniteFlag) != 0u)
                DumpFloodBlackBoxOnce(flags);
        }

        private void DumpFloodBlackBoxOnce(uint reasonFlags)
        {
            if (_floodBlackBoxDumped || !_floodBlackBox.IsCreated)
                return;

            _floodBlackBoxDumped = true;
            DumpFloodBlackBox(reasonFlags);
        }

        private void DumpModuleStressBlackBoxOnce(uint reasonFlags)
        {
            if (_moduleStressBlackBoxDumped || !_floodBlackBox.IsCreated)
                return;

            _moduleStressBlackBoxDumped = true;
            DumpFloodBlackBox(reasonFlags, ModuleStressBlackBoxDumpRelativePath);
        }

        private void DumpFloodBlackBox(uint reasonFlags)
        {
            DumpFloodBlackBox(reasonFlags, FloodBlackBoxDumpRelativePath);
        }

        private void DumpFloodBlackBox(uint reasonFlags, string relativePath)
        {
            if (!_floodBlackBox.IsCreated)
                return;

            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))
                {
                    writer.Write(FloodBlackBoxMagic);
                    writer.Write(FloodBlackBoxVersion);
                    writer.Write((uint)FloodBlackBoxCapacity);
                    writer.Write((uint)_floodBlackBoxCursor);
                    writer.Write(reasonFlags);
                    for (int offset = 0; offset < _floodBlackBox.Length; offset++)
                    {
                        int index = (_floodBlackBoxCursor + offset) % _floodBlackBox.Length;
                        WriteFloodBlackBoxEntry(writer, _floodBlackBox[index]);
                    }
                }
            }
            catch (Exception exception)
            {
                _ = exception;
                Debug.LogWarning("Habitat flood blackbox dump failed.");
            }
        }

        private static void WriteFloodBlackBoxEntry(BinaryWriter writer, HabitatFloodBlackBoxEntry entry)
        {
            writer.Write(entry.Frame);
            writer.Write(entry.NodeCount);
            writer.Write(entry.EdgeCount);
            writer.Write(entry.FloodedRoomCount);
            writer.Write(entry.Reserved0);
            writer.Write(entry.BaseTotalStress);
            writer.Write(entry.MaxWaterLevel01);
            writer.Write(entry.TotalWaterVolumeM3);
            writer.Write(entry.PeakModuleStress);
            writer.Write(entry.Flags);
            writer.Write(entry.StateHash);
            writer.Write(entry.DeformationSequence);
        }

        private static uint QuantizeFloodBlackBoxFloat(float value)
        {
            if (!math.isfinite(value))
                return 0xFFFFFFFFu;

            return (uint)math.clamp((int)math.round(value * 1000f), 0, int.MaxValue);
        }

        private static uint HashFloodBlackBox(uint hash, uint value)
        {
            return unchecked((hash ^ value) * 16777619u);
        }

        private float ResolveGraphIngressPressureDeltaKPa(
            ModuleRecord module,
            BaseModule baseModule,
            float internalPressureAtm)
        {
            float depthMeters = baseModule.PressureCompressionDepthMeters;
            if (depthMeters <= 0.25f || !math.isfinite(depthMeters))
                depthMeters = ResolveRuntimeDepthMeters(module.Position);

            float externalPressureKPa = SurfacePressureKPa + (math.max(0f, depthMeters) * HydrostaticPressureKPaPerMeter);
            float internalPressureKPa = SurfacePressureKPa * math.max(1f, internalPressureAtm);
            return math.max(0f, externalPressureKPa - internalPressureKPa);
        }

        private static float ResolveThermalGasPressureScale(BaseModule baseModule)
        {
            float temperatureCelsius = baseModule.ResolveHostRoomTemperatureCelsius();
            if (!math.isfinite(temperatureCelsius))
                return 1f;

            float overTemperatureCelsius = math.max(0f, temperatureCelsius - ThermalPressureReferenceCelsius);
            return math.min(ThermalPressureMaxScale, 1f + (overTemperatureCelsius * ThermalPressurePerCelsius));
        }

        private static int ResolveGraphFloodNodeBudget(HectonQualityTier scalabilityTier)
        {
            if (scalabilityTier == HectonQualityTier.High || scalabilityTier == HectonQualityTier.Ultra)
                return GraphFloodMaxTraversalNodesPerTick;

            if (scalabilityTier == HectonQualityTier.Mid)
                return GraphFloodMidTraversalNodesPerTick;

            return GraphFloodLowTraversalNodesPerTick;
        }

        private static float ResolvePressureRootLut(float pressureDeltaKPa, HectonQualityTier scalabilityTier)
        {
            if (pressureDeltaKPa <= 0f || !math.isfinite(pressureDeltaKPa))
                return 0f;

            float clampedDeltaKPa = math.min(pressureDeltaKPa, PressureRootLutMaxKPa);
            float scaledIndex = clampedDeltaKPa * PressureRootLutStepKPaInv;
            int lowerIndex = math.clamp((int)scaledIndex, 0, PressureRootLutSize - 1);
            bool exceedsLut = pressureDeltaKPa > PressureRootLutMaxKPa;

            if (!IsAnalyticalHighScalabilityTier(scalabilityTier))
            {
                int sampleIndex = scaledIndex >= PressureRootLutSize ? PressureRootLutSize : lowerIndex;
                float nearestRoot = s_pressureRootLut[sampleIndex];
                if (!exceedsLut)
                    return nearestRoot;

                float lowTierExcessKPa = pressureDeltaKPa - PressureRootLutMaxKPa;
                return nearestRoot + (lowTierExcessKPa * PressureRootExcessLinearScale * s_pressureRootInvLut[sampleIndex]);
            }

            float t = math.min(1f, scaledIndex - lowerIndex);
            float root = math.lerp(s_pressureRootLut[lowerIndex], s_pressureRootLut[lowerIndex + 1], t);
            if (!exceedsLut)
                return root;

            float excessKPa = pressureDeltaKPa - PressureRootLutMaxKPa;
            return root + (excessKPa * PressureRootExcessLinearScale * math.rcp(math.max(1f, root)));
        }

        private void ApplyWaterPumpDrainage(float deltaTime)
        {
            if (deltaTime <= 0f ||
                _nodeCount <= 0 ||
                !_traversalVisited.IsCreated ||
                !_anchorTraversalQueue.IsCreated ||
                !_edgeOffsets.IsCreated ||
                !_edgeDestinations.IsCreated)
            {
                return;
            }

            int pumpCount = WaterPumpModule.ActivePumpCount;
            for (int pumpIndex = 0; pumpIndex < pumpCount; pumpIndex++)
            {
                WaterPumpModule pump = WaterPumpModule.GetActivePump(pumpIndex);
                int startNodeIndex;
                if (pump == null || !pump.CanPump || !TryResolveModuleNodeIndex(pump.HostModule, out startNodeIndex))
                    continue;

                float remainingDrainM3 = pump.ResolveDrainBudgetM3(deltaTime);
                if (remainingDrainM3 <= 0f)
                    continue;

                DrainConnectedFloodComponent(startNodeIndex, ref remainingDrainM3);
            }
        }

        private void DrainConnectedFloodComponent(int startNodeIndex, ref float remainingDrainM3)
        {
            if (remainingDrainM3 <= 0f || startNodeIndex < 0 || startNodeIndex >= _nodeCount)
                return;

            int safeNodeCount = math.min(
                math.min(_nodeCount, _moduleBuffer.Count),
                math.min(_traversalVisited.Length, _anchorTraversalQueue.Length));
            if (startNodeIndex >= safeNodeCount || safeNodeCount <= 0)
                return;

            for (int nodeIndex = 0; nodeIndex < safeNodeCount; nodeIndex++)
                _traversalVisited[nodeIndex] = 0;

            bool traversalOverflowed = false;
            int queueHead = 0;
            int queueTail = 0;
            _traversalVisited[startNodeIndex] = 1;
            _anchorTraversalQueue[queueTail++] = startNodeIndex;

            while (queueHead < queueTail && remainingDrainM3 > 0f)
            {
                int currentNodeIndex = _anchorTraversalQueue[queueHead++];
                BaseModule baseModule = _moduleBuffer[currentNodeIndex].BaseModule;
                if (baseModule != null && baseModule.isActiveAndEnabled)
                    remainingDrainM3 -= baseModule.DrainWaterVolumeM3(remainingDrainM3);

                if (currentNodeIndex + 1 >= _edgeOffsets.Length)
                    continue;

                int edgeLimit = math.min(_edgeCount, _edgeDestinations.Length);
                int edgeStart = math.clamp(_edgeOffsets[currentNodeIndex], 0, edgeLimit);
                int edgeEnd = math.clamp(_edgeOffsets[currentNodeIndex + 1], edgeStart, edgeLimit);
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    int neighborNodeIndex = _edgeDestinations[edgeIndex];
                    if (neighborNodeIndex < 0 ||
                        neighborNodeIndex >= safeNodeCount ||
                        _traversalVisited[neighborNodeIndex] != 0 ||
                        !CanGraphFluidTraverseEdge(currentNodeIndex, neighborNodeIndex, edgeIndex))
                    {
                        continue;
                    }

                    if (queueTail >= safeNodeCount)
                    {
                        traversalOverflowed = true;
                        break;
                    }

                    _traversalVisited[neighborNodeIndex] = 1;
                    _anchorTraversalQueue[queueTail++] = neighborNodeIndex;
                }
            }

            if (traversalOverflowed)
                WriteFloodBlackBoxSample(FloodBlackBoxTraversalOverflowFlag);
        }

        private void ApplyOxygenScrubberFilterConsumption(float deltaTime)
        {
            int moduleCount = math.min(_nodeCount, _moduleBuffer.Count);
            for (int moduleIndex = 0; moduleIndex < moduleCount; moduleIndex++)
            {
                BaseModule baseModule = _moduleBuffer[moduleIndex].BaseModule;
                if (baseModule == null || !baseModule.isActiveAndEnabled)
                    continue;

                if ((ResolveLifeSupportPowerMask(baseModule) & 1) != 0)
                    baseModule.UpdateCarbonFilterLogistics(deltaTime, CarbonFilterItemHashId);
                else
                    baseModule.SetCarbonFilterAvailable(false);
            }
        }

        private static byte ResolveLifeSupportPowerMask(BaseModule baseModule)
        {
            return baseModule != null && baseModule.CachedPowerSupplyRatio > 0f ? (byte)1 : (byte)0;
        }

        private void ApplyThermalCondensationState()
        {
            int moduleCount = math.min(_nodeCount, _moduleBuffer.Count);
            for (int moduleIndex = 0; moduleIndex < moduleCount; moduleIndex++)
            {
                BaseModule baseModule = _moduleBuffer[moduleIndex].BaseModule;
                if (baseModule == null || !baseModule.isActiveAndEnabled)
                    continue;

                float internalTemperatureCelsius = baseModule.ResolveHostRoomTemperatureCelsius();
                float externalTemperatureCelsius = baseModule.PressureCompressionDepthMeters > 100f
                    ? 2f
                    : 12f;
                float airPocketPressureAtm = baseModule.ResolveGraphBoyleAirPocketPressureAtm(ResolveThermalGasPressureScale(baseModule));
                bool humidityFogActive = baseModule.FloodLevel01 > HumidityFogWaterThreshold01 &&
                                         airPocketPressureAtm > HumidityFogPressureThresholdAtm;
                baseModule.SetCondensationState(
                    humidityFogActive ||
                    (internalTemperatureCelsius > CondensationInteriorTemperatureCelsius &&
                     externalTemperatureCelsius < CondensationExternalTemperatureCelsius));
            }
        }

        private bool EvaluateDetachedDebrisState()
        {
            bool topologyChanged = false;
            int moduleCount = math.min(_nodeCount, _moduleBuffer.Count);
            for (int nodeIndex = 0; nodeIndex < moduleCount; nodeIndex++)
            {
                BaseModule baseModule = _moduleBuffer[nodeIndex].BaseModule;
                if (baseModule == null ||
                    !baseModule.isActiveAndEnabled ||
                    baseModule.IsDetachedDebris ||
                    baseModule.CurrentIntegrity > 0f)
                {
                    continue;
                }

                if (!AreConnectingEdgesSevered(nodeIndex))
                    continue;

                topologyChanged |= baseModule.TryDetachAsSinkingDebris();
            }

            return topologyChanged;
        }

        private bool AreConnectingEdgesSevered(int nodeIndex)
        {
            bool hasConnection = false;
            for (int edgeIndex = 0; edgeIndex < _edgeBuffer.Count; edgeIndex++)
            {
                EdgeRecord edge = _edgeBuffer[edgeIndex];
                if (edge.SourceIndex != nodeIndex && edge.DestinationIndex != nodeIndex)
                    continue;

                hasConnection = true;
                if (!edge.Severed)
                    return false;
            }

            return hasConnection;
        }

        private bool TryResolveModuleNodeIndex(BaseModule module, out int nodeIndex)
        {
            nodeIndex = -1;
            if (module == null)
                return false;

            uint nodeId = unchecked((uint)EntityId.ToULong(module.GetEntityId()));
            return nodeId != 0u &&
                   _moduleIndexByNodeId.TryGetValue(nodeId, out nodeIndex) &&
                   nodeIndex >= 0 &&
                   nodeIndex < _nodeCount;
        }

        internal bool TryValidateDeconstructionRollback(
            BaseModule targetModule,
            bool skipIsolationDfs,
            NativeList<long> dfsStack,
            NativeParallelHashSet<long> dfsVisited,
            NativeArray<int> dfsResult,
            out byte rejectReason)
        {
            rejectReason = 0;
            if (targetModule == null || !TryResolveModuleNodeIndex(targetModule, out int removedNodeIndex))
            {
                rejectReason = 1;
                return false;
            }

            if (HasDependentWindowCollapse(removedNodeIndex))
            {
                rejectReason = 2;
                return false;
            }

            if (skipIsolationDfs)
                return true;

            if (!dfsStack.IsCreated || !dfsVisited.IsCreated || !dfsResult.IsCreated || dfsResult.Length < 3)
            {
                rejectReason = 3;
                return false;
            }

            int nodeCount = math.min(_nodeCount, math.min(_moduleBuffer.Count, _edgeOffsets.IsCreated ? _edgeOffsets.Length - 1 : 0));
            if (nodeCount <= 2)
                return true;

            DeconstructionDfsValidationJob job = new DeconstructionDfsValidationJob
            {
                EdgeOffsets = _edgeOffsets,
                EdgeDestinations = _edgeDestinations,
                Stack = dfsStack,
                Visited = dfsVisited,
                Result = dfsResult,
                NodeCount = nodeCount,
                RemovedNodeIndex = removedNodeIndex,
                EdgeCount = _edgeCount
            };

            job.Run(); // COLD SYNC JOB: player-triggered deconstruction validation, not a per-frame path.
            if (dfsResult[0] != 1)
            {
                rejectReason = 4;
                return false;
            }

            return true;
        }

        private bool HasDependentWindowCollapse(int removedNodeIndex)
        {
            if (!_roomConnections.IsCreated || removedNodeIndex < 0)
                return false;

            NativeParallelMultiHashMapIterator<int> iterator;
            if (!_roomConnections.TryGetFirstValue(removedNodeIndex, out HabitatFloodConnection connection, out iterator))
                return false;

            do
            {
                int destinationIndex = connection.DestinationIndex;
                if (!IsValidDeconstructionNode(destinationIndex) || !IsWindowModule(destinationIndex))
                    continue;

                if (CountLiveRoomConnectionsExcluding(destinationIndex, removedNodeIndex) <= 0)
                    return true;
            }
            while (_roomConnections.TryGetNextValue(out connection, ref iterator));

            return false;
        }

        private int CountLiveRoomConnectionsExcluding(int nodeIndex, int removedNodeIndex)
        {
            NativeParallelMultiHashMapIterator<int> iterator;
            if (!_roomConnections.TryGetFirstValue(nodeIndex, out HabitatFloodConnection connection, out iterator))
                return 0;

            int count = 0;
            do
            {
                int destinationIndex = connection.DestinationIndex;
                if (destinationIndex == removedNodeIndex || !IsValidDeconstructionNode(destinationIndex))
                    continue;

                count++;
            }
            while (_roomConnections.TryGetNextValue(out connection, ref iterator));

            return count;
        }

        private bool IsWindowModule(int nodeIndex)
        {
            if (!IsValidDeconstructionNode(nodeIndex))
                return false;

            ModuleRecord record = _moduleBuffer[nodeIndex];
            ModuleMarker marker = record.Marker;
            if (marker != null)
            {
                if (ContainsTopologyToken(marker.PrefabId, "Window") ||
                    ContainsTopologyToken(marker.PrefabId, "Observation") ||
                    (marker.Data != null && ContainsTopologyToken(marker.Data.moduleName, "Window")) ||
                    (marker.Data != null && ContainsTopologyToken(marker.Data.PersistentId, "Window")))
                {
                    return true;
                }
            }

            BaseModule baseModule = record.BaseModule;
            BaseModuleTemplate template = baseModule != null ? baseModule.ModuleTemplate : null;
            return template != null &&
                   (ContainsTopologyToken(template.PersistentId, "Window") ||
                    ContainsTopologyToken(template.PersistentId, "Observation"));
        }

        private bool IsValidDeconstructionNode(int nodeIndex)
        {
            return nodeIndex >= 0 &&
                   nodeIndex < _nodeCount &&
                   nodeIndex < _moduleBuffer.Count;
        }

        private static bool ContainsTopologyToken(string value, string token)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void QueueFloodMassLoads(float deltaTime)
        {
            for (int moduleIndex = 0; moduleIndex < _moduleBuffer.Count; moduleIndex++)
            {
                ModuleRecord module = _moduleBuffer[moduleIndex];
                BaseModule baseModule = module.BaseModule;
                if (baseModule == null || !baseModule.isActiveAndEnabled)
                    continue;

                float floodWaterMassKilograms = baseModule.ResolveFloodWaterMassKilograms();
                float parasiteMassKilograms = baseModule.ResolveParasiteAddedMassKilograms();
                float structuralMassKilograms = floodWaterMassKilograms + parasiteMassKilograms;
                if (structuralMassKilograms <= 0f || !math.isfinite(structuralMassKilograms))
                    continue;

                baseModule.QueueHydroStructuralLoad(structuralMassKilograms, module.Position, deltaTime);
            }
        }

        private bool EvaluateBulkheadFloodStress(float deltaTime)
        {
            bool topologyChanged = false;
            for (int moduleIndex = 0; moduleIndex < _moduleBuffer.Count; moduleIndex++)
            {
                BaseModule baseModule = _moduleBuffer[moduleIndex].BaseModule;
                if (baseModule != null && baseModule.isActiveAndEnabled)
                    baseModule.DecayBulkheadFloodStress(deltaTime);
            }

            for (int edgeIndex = 0; edgeIndex < _edgeBuffer.Count; edgeIndex++)
            {
                EdgeRecord edge = _edgeBuffer[edgeIndex];
                if (edge.Severed)
                    continue;

                BaseModule sourceModule = _moduleBuffer[edge.SourceIndex].BaseModule;
                BaseModule destinationModule = _moduleBuffer[edge.DestinationIndex].BaseModule;
                bool ruptured = ApplyBulkheadFloodStress(sourceModule, destinationModule, deltaTime);
                ruptured |= ApplyBulkheadFloodStress(destinationModule, sourceModule, deltaTime);
                if (ruptured)
                {
                    MarkEdgeRuptured(ref edge);
                    _edgeBuffer[edgeIndex] = edge;
                    topologyChanged = true;
                }
            }

            return topologyChanged;
        }

        private static bool ApplyBulkheadFloodStress(BaseModule floodedModule, BaseModule candidateAirlock, float deltaTime)
        {
            if (!IsFloodedForHydroStress(floodedModule) || !IsPristineForHydroStress(candidateAirlock))
                return false;

            float floodWaterMassKilograms = floodedModule.ResolveFloodWaterMassKilograms();
            if (floodWaterMassKilograms <= 0f || !math.isfinite(floodWaterMassKilograms))
                return false;

            return candidateAirlock.AccumulateBulkheadFloodStress(floodWaterMassKilograms, deltaTime);
        }

        private bool EvaluatePressureBucklingStress(float deltaTime)
        {
            bool topologyChanged = ApplyQueuedRuptureCascadeFailures();

            for (int moduleIndex = 0; moduleIndex < _moduleBuffer.Count; moduleIndex++)
            {
                BaseModule baseModule = _moduleBuffer[moduleIndex].BaseModule;
                if (baseModule != null && baseModule.isActiveAndEnabled)
                    baseModule.DecayJointShearStress(deltaTime);
            }

            for (int edgeIndex = 0; edgeIndex < _edgeBuffer.Count; edgeIndex++)
            {
                EdgeRecord edge = _edgeBuffer[edgeIndex];
                if (edge.Severed)
                    continue;

                BaseModule sourceModule = _moduleBuffer[edge.SourceIndex].BaseModule;
                BaseModule destinationModule = _moduleBuffer[edge.DestinationIndex].BaseModule;
                if (sourceModule == null || destinationModule == null)
                    continue;

                float compressionDelta = math.abs(sourceModule.PressureCompressionAlpha01 - destinationModule.PressureCompressionAlpha01);
                if (compressionDelta <= PressureBucklingCompressionDeltaThreshold || !math.isfinite(compressionDelta))
                    continue;

                bool sourceDamaged = sourceModule.ApplyJointShearStress(compressionDelta, deltaTime);
                bool destinationDamaged = destinationModule.ApplyJointShearStress(compressionDelta, deltaTime);
                if (!sourceDamaged && !destinationDamaged)
                    continue;

                float stress01 = math.max(sourceModule.JointShearStress01, destinationModule.JointShearStress01);
                if (stress01 < StructuralGroanStressThreshold01)
                    continue;

                bool sourceGroanAllowed = sourceModule.TryConsumeJointShearGroanCooldown();
                bool destinationGroanAllowed = destinationModule.TryConsumeJointShearGroanCooldown();
                if (!sourceGroanAllowed && !destinationGroanAllowed)
                    continue;

                double3 startAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3((Vector3)edge.StartSocketPosition);
                double3 endAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3((Vector3)edge.EndSocketPosition);
                Vector3 midpoint = HectonFloatingOrigin.ToRuntimePosition((startAup + endAup) * 0.5d);
                float depthMeters = math.max(
                    ResolveAnalyticalModuleDepthMeters(_moduleBuffer[edge.SourceIndex], sourceModule),
                    ResolveAnalyticalModuleDepthMeters(_moduleBuffer[edge.DestinationIndex], destinationModule));
                PublishHullStressSignal(
                    midpoint,
                    stress01,
                    compressionDelta,
                    depthMeters,
                    1f + (math.saturate(stress01) * StructuralGroanPitchRange));
            }

            ApplyRuptureCascadeStressFromRupturedNodes();
            return topologyChanged;
        }

        private bool ApplyQueuedRuptureCascadeFailures()
        {
            bool topologyChanged = false;
            int maxNodeCount = math.min(_nodeCount, _moduleBuffer.Count);
            for (int nodeIndex = 0; nodeIndex < maxNodeCount; nodeIndex++)
            {
                BaseModule baseModule = _moduleBuffer[nodeIndex].BaseModule;
                if (baseModule == null || !baseModule.isActiveAndEnabled)
                    continue;

                if (!baseModule.TryConsumePendingRuptureCascadeFailure())
                    continue;

                if (!SystemDispatcher.TryConsumeBaseStressCascadeEvent(ResolveNodeIslandId(nodeIndex), RuptureCascadeEventHash))
                    continue;

                MarkNodeRuptured(nodeIndex);
                RuptureConnectedEdges(nodeIndex);
                topologyChanged = true;
            }

            return topologyChanged;
        }

        private void ApplyRuptureCascadeStressFromRupturedNodes()
        {
            if (_nodeCount <= 0 ||
                !_nodes.IsCreated ||
                !_edgeOffsets.IsCreated ||
                !_edgeDestinations.IsCreated)
            {
                return;
            }

            int maxNodeCount = math.min(
                math.min(_nodeCount, _moduleBuffer.Count),
                math.min(_nodes.Length, _edgeOffsets.Length - 1));
            int edgeLimit = math.min(_edgeCount, _edgeDestinations.Length);
            if (maxNodeCount <= 0 || edgeLimit <= 0)
                return;

            for (int nodeIndex = 0; nodeIndex < maxNodeCount; nodeIndex++)
            {
                LogisticsNodeFlags sourceFlags = _nodes[nodeIndex].Flags;
                BaseModule sourceModule = _moduleBuffer[nodeIndex].BaseModule;
                bool sourceRuptured = (sourceFlags & LogisticsNodeFlags.Ruptured) != 0 ||
                                      (sourceModule != null && sourceModule.IntegrityState == BaseModuleIntegrityState.Ruptured);
                if (!sourceRuptured)
                    continue;

                uint sourceNodeId = _moduleBuffer[nodeIndex].NodeId;
                if (sourceNodeId != 0u && HasRuptureCascadeBeenApplied(sourceNodeId))
                    continue;

                if (sourceNodeId != 0u)
                    MarkRuptureCascadeApplied(sourceNodeId);

                int sourceIslandId = ResolveNodeIslandId(nodeIndex);
                int edgeStart = math.clamp(_edgeOffsets[nodeIndex], 0, edgeLimit);
                int edgeEnd = math.clamp(_edgeOffsets[nodeIndex + 1], edgeStart, edgeLimit);
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    int neighborNodeIndex = _edgeDestinations[edgeIndex];
                    if (neighborNodeIndex < 0 || neighborNodeIndex >= maxNodeCount)
                        continue;

                    LogisticsNodeFlags neighborFlags = _nodes[neighborNodeIndex].Flags;
                    if ((neighborFlags & LogisticsNodeFlags.Ruptured) != 0)
                        continue;

                    BaseModule neighborModule = _moduleBuffer[neighborNodeIndex].BaseModule;
                    if (neighborModule == null ||
                        !neighborModule.isActiveAndEnabled ||
                        neighborModule.IntegrityState == BaseModuleIntegrityState.Ruptured)
                    {
                        continue;
                    }

                    if (!SystemDispatcher.TryConsumeBaseStressCascadeEvent(sourceIslandId, RuptureCascadeEventHash))
                        continue;

                    neighborModule.ApplyRuptureCascadeStress(RuptureCascadeNeighborStressMultiplier);
                }
            }
        }

        private int ResolveNodeIslandId(int nodeIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= _nodeCount)
                return 0;

            return _nodes[nodeIndex].NetworkId;
        }

        private void EnsureRuptureCascadeStateCapacity(int requiredCapacity)
        {
            int safeCapacity = NextPowerOfTwo(math.max(1, requiredCapacity));
            if (_ruptureCascadeAppliedNodeIds.Capacity >= safeCapacity)
                return;

            _ruptureCascadeAppliedNodeIds.Capacity = safeCapacity;
        }

        private bool HasRuptureCascadeBeenApplied(uint nodeId)
        {
            for (int i = 0; i < _ruptureCascadeAppliedNodeIds.Count; i++)
            {
                if (_ruptureCascadeAppliedNodeIds[i] == nodeId)
                    return true;
            }

            return false;
        }

        private void MarkRuptureCascadeApplied(uint nodeId)
        {
            if (nodeId == 0u || HasRuptureCascadeBeenApplied(nodeId))
                return;

            if (_ruptureCascadeAppliedNodeIds.Count < _ruptureCascadeAppliedNodeIds.Capacity)
                _ruptureCascadeAppliedNodeIds.Add(nodeId);
        }

        private void PruneRuptureCascadeState()
        {
            for (int i = _ruptureCascadeAppliedNodeIds.Count - 1; i >= 0; i--)
            {
                uint nodeId = _ruptureCascadeAppliedNodeIds[i];
                if (nodeId != 0u && IsRuptureCascadeSourceStillRuptured(nodeId))
                    continue;

                int lastIndex = _ruptureCascadeAppliedNodeIds.Count - 1;
                _ruptureCascadeAppliedNodeIds[i] = _ruptureCascadeAppliedNodeIds[lastIndex];
                _ruptureCascadeAppliedNodeIds.RemoveAt(lastIndex);
            }
        }

        private bool IsRuptureCascadeSourceStillRuptured(uint nodeId)
        {
            int moduleCount = math.min(_nodeCount, _moduleBuffer.Count);
            for (int moduleIndex = 0; moduleIndex < moduleCount; moduleIndex++)
            {
                ModuleRecord module = _moduleBuffer[moduleIndex];
                if (module.NodeId != nodeId)
                    continue;

                LogisticsNodeFlags nodeFlags = moduleIndex < _nodes.Length ? _nodes[moduleIndex].Flags : LogisticsNodeFlags.None;
                BaseModule baseModule = module.BaseModule;
                return (nodeFlags & LogisticsNodeFlags.Ruptured) != 0 ||
                       (baseModule != null && baseModule.IntegrityState == BaseModuleIntegrityState.Ruptured);
            }

            return false;
        }

        private void RuptureConnectedEdges(int nodeIndex)
        {
            for (int edgeIndex = 0; edgeIndex < _edgeBuffer.Count; edgeIndex++)
            {
                EdgeRecord edge = _edgeBuffer[edgeIndex];
                if (edge.Severed)
                    continue;

                if (edge.SourceIndex != nodeIndex && edge.DestinationIndex != nodeIndex)
                    continue;

                MarkEdgeRuptured(ref edge);
                _edgeBuffer[edgeIndex] = edge;
            }
        }

        internal void NotifyModuleEmergencyStateChanged(BaseModule module)
        {
            if (module == null || _nodeCount <= 0)
                return;

            PublishEmergencyLockdownState();
            PublishSiegeTargetSnapshot();
        }

        internal bool TryResolveFungalMindTarget(BaseModule sourceModule, out BaseModule targetModule, out float targetPotential)
        {
            targetModule = null;
            targetPotential = 0f;
            if (sourceModule == null ||
                _nodeCount <= 0 ||
                !_nodes.IsCreated ||
                !_edgeOffsets.IsCreated ||
                !_edgeDestinations.IsCreated ||
                !_traversalVisited.IsCreated ||
                !_anchorTraversalQueue.IsCreated)
            {
                return false;
            }

            uint sourceNodeId = unchecked((uint)EntityId.ToULong(sourceModule.GetEntityId()));
            if (sourceNodeId == 0u ||
                !_moduleIndexByNodeId.TryGetValue(sourceNodeId, out int startNodeIndex) ||
                startNodeIndex < 0 ||
                startNodeIndex >= _nodeCount)
            {
                return false;
            }

            int safeNodeCount = math.min(
                math.min(_nodeCount, _moduleBuffer.Count),
                math.min(math.min(_nodes.Length, _traversalVisited.Length), _anchorTraversalQueue.Length));
            if (startNodeIndex >= safeNodeCount || safeNodeCount <= 0)
                return false;

            for (int nodeIndex = 0; nodeIndex < safeNodeCount; nodeIndex++)
                _traversalVisited[nodeIndex] = 0;

            bool traversalOverflowed = false;
            int queueHead = 0;
            int queueTail = 0;
            _traversalVisited[startNodeIndex] = 1;
            _anchorTraversalQueue[queueTail++] = startNodeIndex;

            float bestScore = 0f;
            float bestPotential = 0f;
            BaseModule bestModule = null;
            while (queueHead < queueTail)
            {
                int currentNodeIndex = _anchorTraversalQueue[queueHead++];
                byte currentDepth = _traversalVisited[currentNodeIndex];
                ModuleRecord currentRecord = _moduleBuffer[currentNodeIndex];
                if (currentNodeIndex != startNodeIndex)
                {
                    BaseModule currentModule = currentRecord.BaseModule;
                    if (currentModule != null && currentModule.isActiveAndEnabled)
                    {
                        float rawPotential = ResolveFungalMindPotentialScore(currentRecord, _nodes[currentNodeIndex]);
                        float depthPenalty = 1f + (math.max(0, currentDepth - 1) * 0.08f);
                        float score = rawPotential * math.rcp(depthPenalty);
                        if (score > bestScore && math.isfinite(score))
                        {
                            bestScore = score;
                            bestPotential = rawPotential;
                            bestModule = currentModule;
                        }
                    }
                }

                if (currentNodeIndex + 1 >= _edgeOffsets.Length)
                    continue;

                int edgeLimit = math.min(_edgeCount, _edgeDestinations.Length);
                int edgeStart = math.clamp(_edgeOffsets[currentNodeIndex], 0, edgeLimit);
                int edgeEnd = math.clamp(_edgeOffsets[currentNodeIndex + 1], edgeStart, edgeLimit);
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    int neighborNodeIndex = _edgeDestinations[edgeIndex];
                    if (neighborNodeIndex < 0 ||
                        neighborNodeIndex >= safeNodeCount ||
                        _traversalVisited[neighborNodeIndex] != 0)
                    {
                        continue;
                    }

                    if (queueTail >= safeNodeCount)
                    {
                        traversalOverflowed = true;
                        break;
                    }

                    _traversalVisited[neighborNodeIndex] = (byte)math.min(255, currentDepth + 1);
                    _anchorTraversalQueue[queueTail++] = neighborNodeIndex;
                }
            }

            if (traversalOverflowed)
                WriteFloodBlackBoxSample(FloodBlackBoxTraversalOverflowFlag);

            if (bestModule == null || bestScore <= 0f)
                return false;

            targetModule = bestModule;
            targetPotential = bestPotential;
            return true;
        }

        private void PopulateModuleBuffer(IReadOnlyList<GameObject> modules)
        {
            int count = modules.Count;
            if (_moduleBuffer.Capacity < count)
                _moduleBuffer.Capacity = count;

            for (int i = 0; i < count; i++)
            {
                GameObject moduleObject = modules[i];
                if (moduleObject == null)
                    continue;

                ModuleMarker marker = moduleObject.TryGetComponent(out ModuleMarker resolvedMarker) ? resolvedMarker : null;
                BaseModule baseModule = moduleObject.TryGetComponent(out BaseModule resolvedBaseModule) ? resolvedBaseModule : null;
                if (baseModule != null && baseModule.IsDetachedDebris)
                    continue;

                EntityId entityId = moduleObject.GetEntityId();
                uint nodeId = unchecked((uint)EntityId.ToULong(entityId));
                Vector3 modulePosition = moduleObject.transform.position;

                _moduleBuffer.Add(new ModuleRecord
                {
                    ModuleObject = moduleObject,
                    Marker = marker,
                    BaseModule = baseModule,
                    Position = modulePosition,
                    NodeId = nodeId,
                    IsAnchorNode = ResolveStructuralAnchorState(baseModule, marker),
                    IsEmergencyAirlock = ResolveEmergencyAirlockState(baseModule, marker)
                });

                _moduleIndexByNodeId[nodeId] = _moduleBuffer.Count - 1;
            }
        }

        internal bool TryAddTemporaryBypass(GameObject sourceModule, GameObject destinationModule)
        {
            bool injectedDirectly;
            return TryAddTemporaryBypass(sourceModule, destinationModule, out injectedDirectly);
        }

        internal bool TryAddTemporaryBypass(GameObject sourceModule, GameObject destinationModule, out bool injectedDirectly)
        {
            return TryAddTemporaryBypass(sourceModule, destinationModule, 0, 0, out injectedDirectly);
        }

        internal bool TryAddTemporaryBypass(
            GameObject sourceModule,
            GameObject destinationModule,
            int sourceModuleHashId,
            int destinationModuleHashId,
            out bool injectedDirectly)
        {
            injectedDirectly = false;
            if (sourceModule == null || destinationModule == null || ReferenceEquals(sourceModule, destinationModule))
                return false;

            uint sourceNodeId = unchecked((uint)EntityId.ToULong(sourceModule.GetEntityId()));
            uint destinationNodeId = unchecked((uint)EntityId.ToULong(destinationModule.GetEntityId()));
            if (sourceNodeId == 0u || destinationNodeId == 0u || sourceNodeId == destinationNodeId)
                return false;

            sourceModuleHashId = ResolveTemporaryBypassModuleHashId(sourceModule, sourceModuleHashId);
            destinationModuleHashId = ResolveTemporaryBypassModuleHashId(destinationModule, destinationModuleHashId);
            if (sourceModuleHashId == 0 || destinationModuleHashId == 0)
                return false;

            for (int i = 0; i < _temporaryBypassBuffer.Count; i++)
            {
                TemporaryBypassRecord existing = _temporaryBypassBuffer[i];
                if (existing.SourceNodeId == sourceNodeId && existing.DestinationNodeId == destinationNodeId)
                    return false;
            }

            if (_temporaryBypassBuffer.Count >= _temporaryBypassBuffer.Capacity)
                return false;

            if (!TryResolveModuleGraphPosition(sourceNodeId, sourceModule, out Vector3 sourcePosition) ||
                !TryResolveModuleGraphPosition(destinationNodeId, destinationModule, out Vector3 destinationPosition))
            {
                return false;
            }

            int recordIndex = _temporaryBypassBuffer.Count;
            _temporaryBypassBuffer.Add(new TemporaryBypassRecord
            {
                SourceNodeId = sourceNodeId,
                DestinationNodeId = destinationNodeId,
                SourceModuleHashId = sourceModuleHashId,
                DestinationModuleHashId = destinationModuleHashId,
                SourcePosition = sourcePosition,
                DestinationPosition = destinationPosition
            });

            injectedDirectly = TryInjectTemporaryBypassIntoLiveCsr(sourceNodeId, destinationNodeId, sourcePosition, destinationPosition);
            if (injectedDirectly)
                return true;

            _temporaryBypassBuffer.RemoveAt(recordIndex);
            return false;
        }

        private static int ResolveTemporaryBypassModuleHashId(GameObject module, int capturedModuleHashId)
        {
            if (capturedModuleHashId != 0)
                return capturedModuleHashId;

            if (module != null &&
                module.TryGetComponent(out ModuleMarker marker) &&
                marker != null &&
                marker.Data != null)
            {
                return marker.Data.ModuleHashId;
            }

            return 0;
        }

        private bool TryResolveModuleGraphPosition(uint nodeId, GameObject fallbackModule, out Vector3 position)
        {
            if (_moduleIndexByNodeId.TryGetValue(nodeId, out int moduleIndex) &&
                moduleIndex >= 0 &&
                moduleIndex < _moduleBuffer.Count)
            {
                position = _moduleBuffer[moduleIndex].Position;
                return true;
            }

            if (fallbackModule == null)
            {
                position = default;
                return false;
            }

            position = fallbackModule.transform.position;
            return true;
        }

        private bool TryInjectTemporaryBypassIntoLiveCsr(uint sourceNodeId, uint destinationNodeId, Vector3 sourcePosition, Vector3 destinationPosition)
        {
            if (_nodeCount <= 0 ||
                !_edgeOffsets.IsCreated ||
                !_edgeWriteCursor.IsCreated ||
                !_moduleIndexByNodeId.TryGetValue(sourceNodeId, out int sourceIndex) ||
                !_moduleIndexByNodeId.TryGetValue(destinationNodeId, out int destinationIndex) ||
                sourceIndex == destinationIndex ||
                sourceIndex < 0 ||
                destinationIndex < 0 ||
                sourceIndex >= _nodeCount ||
                destinationIndex >= _nodeCount ||
                sourceIndex >= _edgeOffsets.Length - 1 ||
                destinationIndex >= _edgeOffsets.Length - 1 ||
                _nodeCount > _edgeWriteCursor.Length ||
                _edgeBuffer.Count >= _edgeBuffer.Capacity)
            {
                return false;
            }

            Vector3 direction = destinationPosition - sourcePosition;
            float sqrMagnitude = direction.sqrMagnitude;
            Vector3 forward = ResolveFastDirection(direction, sqrMagnitude);
            float resistance = math.max(MinimumEdgeResistance, ResolveFastLengthFromSq(sqrMagnitude) * EdgeResistancePerMeter);

            _edgeBuffer.Add(new EdgeRecord
            {
                SourceIndex = sourceIndex,
                DestinationIndex = destinationIndex,
                StartSocketPosition = sourcePosition,
                EndSocketPosition = destinationPosition,
                StartForward = forward,
                EndForward = -forward,
                Resistance = resistance,
                Flags = PipeRenderFlags.None,
                Severed = false,
                DirectedOnly = true
            });

            BuildEdgeRecords();
            EvaluateAnchorReachability();
            PublishAnchorState();
            PublishComponentPowerState();
            PublishEmergencyLockdownState();
            PublishDegradationState();
            PublishSiegeTargetSnapshot();
            PublishGraphKernel();
            return true;
        }

        private void AppendTemporaryBypassEdges()
        {
            for (int bypassIndex = 0; bypassIndex < _temporaryBypassBuffer.Count; bypassIndex++)
            {
                TemporaryBypassRecord bypass = _temporaryBypassBuffer[bypassIndex];
                if (_edgeBuffer.Count >= _edgeBuffer.Capacity ||
                    !_moduleIndexByNodeId.TryGetValue(bypass.SourceNodeId, out int sourceIndex) ||
                    !_moduleIndexByNodeId.TryGetValue(bypass.DestinationNodeId, out int destinationIndex) ||
                    sourceIndex == destinationIndex)
                {
                    continue;
                }

                Vector3 sourcePosition = _moduleBuffer[sourceIndex].Position;
                Vector3 destinationPosition = _moduleBuffer[destinationIndex].Position;
                Vector3 direction = destinationPosition - sourcePosition;
                float sqrMagnitude = direction.sqrMagnitude;
                Vector3 forward = ResolveFastDirection(direction, sqrMagnitude);

                _edgeBuffer.Add(new EdgeRecord
                {
                    SourceIndex = sourceIndex,
                    DestinationIndex = destinationIndex,
                    StartSocketPosition = sourcePosition,
                    EndSocketPosition = destinationPosition,
                    StartForward = forward,
                    EndForward = -forward,
                    Flags = PipeRenderFlags.None,
                    Severed = false,
                    DirectedOnly = true
                });
            }
        }

        private void BuildSocketAdjacency()
        {
            int quantizationScale = math.max(1, (int)math.round(1f / DefaultSocketQuantization));
            for (int moduleIndex = 0; moduleIndex < _moduleBuffer.Count; moduleIndex++)
                IndexSockets(moduleIndex, _moduleBuffer[moduleIndex].ModuleObject, quantizationScale);
        }

        private void IndexSockets(int moduleIndex, GameObject root, int quantizationScale)
        {
            if (root == null)
                return;

            _socketBuffer.Clear();
            root.GetComponentsInChildren(true, _socketBuffer);

            for (int i = 0; i < _socketBuffer.Count; i++)
            {
                ModuleSocket socket = _socketBuffer[i];
                if (socket == null)
                    continue;

                Transform socketTransform = socket.transform;
                int axis = QuantizeAxis(socketTransform.forward);
                SocketKey oppositeKey = SocketKey.Create(socketTransform.position, OppositeAxis(axis), quantizationScale);

                if (_socketLookup.TryGetValue(oppositeKey, out SocketMatchEntry existing))
                {
                    if (existing.ModuleIndex != moduleIndex &&
                        ModuleSocketTopology.AreCompatible(existing.CompatibleType, existing.Direction, socket.CompatibleType, socket.Direction) &&
                        Vector3.Dot(existing.Forward, socketTransform.forward) <= OppositeDirectionDotThreshold)
                    {
                        _edgeBuffer.Add(new EdgeRecord
                        {
                            SourceIndex = existing.ModuleIndex,
                            DestinationIndex = moduleIndex,
                            StartSocketPosition = existing.Position,
                            EndSocketPosition = socketTransform.position,
                            StartForward = existing.Forward,
                            EndForward = socketTransform.forward,
                            Flags = PipeRenderFlags.None
                        });
                    }

                    continue;
                }

                SocketKey ownKey = SocketKey.Create(socketTransform.position, axis, quantizationScale);
                _socketLookup[ownKey] = new SocketMatchEntry(moduleIndex, socket.CompatibleType, socket.Direction, socketTransform.position, socketTransform.forward);
            }
        }

        private void BuildNodeRecords()
        {
            if (!_nodes.IsCreated)
                return;

            int maxNodeCount = math.min(math.max(0, _nodeCount), math.min(_moduleBuffer.Count, _nodes.Length));
            for (int nodeIndex = 0; nodeIndex < maxNodeCount; nodeIndex++)
            {
                ModuleRecord module = _moduleBuffer[nodeIndex];
                _nodes[nodeIndex] = new LogisticsNetworkGraph.LogisticsNode
                {
                    Id = module.NodeId,
                    Capacity = ResolveNodeCapacity(module.Marker, module.BaseModule),
                    Resistance = ResolveNodeResistance(module.BaseModule),
                    CurrentLoad = ResolveHydroStructuralLoadNewtons(module.BaseModule),
                    Potential = 0f,
                    Priority = ResolveNodePriority(module.Marker),
                    Flags = ResolveNodeFlags(module.BaseModule),
                    NetworkId = 0,
                    Reserved = (byte)ResolveReservedState(module.BaseModule, module.IsAnchorNode, false, false)
                };
            }
        }

        private void BuildEdgeRecords()
        {
            int reservedDirectedEdgeCapacity = math.max(1, _edgeBuffer.Count * 2);
            EnsureEdgeCapacity(reservedDirectedEdgeCapacity);
            ResetFloodConnectionState(reservedDirectedEdgeCapacity);
            if (!_edgeOffsets.IsCreated ||
                !_edgeWriteCursor.IsCreated ||
                _edgeOffsets.Length <= 0 ||
                _nodeCount <= 0)
            {
                _edgeCount = 0;
                return;
            }

            int safeOffsetNodeCount = math.max(0, math.min(_nodeCount, _edgeOffsets.Length - 1));
            int safeWriteNodeCount = math.min(safeOffsetNodeCount, _edgeWriteCursor.Length);
            int logicalDirectedEdgeCount = 0;
            float unsupportedSpanMeters = LogisticsPipeBuilder.UnsupportedSpanMeters;
            float unsupportedSpanSq = unsupportedSpanMeters * unsupportedSpanMeters;

            for (int nodeIndex = 0; nodeIndex <= safeOffsetNodeCount; nodeIndex++)
                _edgeOffsets[nodeIndex] = 0;

            for (int edgeIndex = 0; edgeIndex < _edgeBuffer.Count; edgeIndex++)
            {
                EdgeRecord edge = _edgeBuffer[edgeIndex];
                edge.ForwardCsrIndex = -1;
                edge.ReverseCsrIndex = -1;
                if (!IsValidEdgeEndpoint(edge.SourceIndex) ||
                    !IsValidEdgeEndpoint(edge.DestinationIndex))
                {
                    edge.Severed = true;
                    _edgeBuffer[edgeIndex] = edge;
                    continue;
                }

                float3 socketDelta = edge.EndSocketPosition - edge.StartSocketPosition;
                float distanceSq = math.lengthsq(socketDelta);
                bool unsupported = distanceSq > unsupportedSpanSq &&
                                   !HasIntermediateSupport(edge.SourceIndex, edge.DestinationIndex, edge.StartSocketPosition, edge.EndSocketPosition);

                if (unsupported || HasImplodedEndpoint(edge))
                    MarkEdgeRuptured(ref edge);

                if (!edge.Severed && TryApplyHydroShearRupture(ref edge))
                    MarkEdgeRuptured(ref edge);

                edge.Resistance = edge.Severed
                    ? 0f
                    : math.max(MinimumEdgeResistance, ResolveFastLengthFromSq(distanceSq) * EdgeResistancePerMeter);
                _edgeBuffer[edgeIndex] = edge;

                if (edge.Severed)
                    continue;

                _edgeOffsets[edge.SourceIndex + 1] = _edgeOffsets[edge.SourceIndex + 1] + 1;
                if (edge.DirectedOnly)
                {
                    logicalDirectedEdgeCount++;
                }
                else
                {
                    _edgeOffsets[edge.DestinationIndex + 1] = _edgeOffsets[edge.DestinationIndex + 1] + 1;
                    logicalDirectedEdgeCount += 2;
                }
            }

            for (int nodeIndex = 1; nodeIndex <= safeOffsetNodeCount; nodeIndex++)
                _edgeOffsets[nodeIndex] = _edgeOffsets[nodeIndex] + _edgeOffsets[nodeIndex - 1];

            for (int nodeIndex = 0; nodeIndex < safeWriteNodeCount; nodeIndex++)
                _edgeWriteCursor[nodeIndex] = _edgeOffsets[nodeIndex];

            int writtenDirectedEdgeCount = 0;
            for (int edgeIndex = 0; edgeIndex < _edgeBuffer.Count; edgeIndex++)
            {
                EdgeRecord edge = _edgeBuffer[edgeIndex];
                if (edge.Severed ||
                    !IsValidEdgeEndpoint(edge.SourceIndex) ||
                    !IsValidEdgeEndpoint(edge.DestinationIndex))
                {
                    edge.ForwardCsrIndex = -1;
                    edge.ReverseCsrIndex = -1;
                    _edgeBuffer[edgeIndex] = edge;
                    continue;
                }

                int forwardWriteIndex = _edgeWriteCursor[edge.SourceIndex];
                if (!IsValidCsrWriteIndex(forwardWriteIndex))
                {
                    edge.ForwardCsrIndex = -1;
                    edge.ReverseCsrIndex = -1;
                    _edgeBuffer[edgeIndex] = edge;
                    continue;
                }

                _edgeWriteCursor[edge.SourceIndex] = forwardWriteIndex + 1;
                _edgeDestinations[forwardWriteIndex] = edge.DestinationIndex;
                _edgeResistance[forwardWriteIndex] = edge.Resistance;
                edge.ForwardCsrIndex = forwardWriteIndex;
                AddFloodConnection(edge.SourceIndex, edge.DestinationIndex, forwardWriteIndex, edge.Resistance);
                writtenDirectedEdgeCount = math.max(writtenDirectedEdgeCount, forwardWriteIndex + 1);

                if (edge.DirectedOnly)
                {
                    _edgeBuffer[edgeIndex] = edge;
                    continue;
                }

                int reverseWriteIndex = _edgeWriteCursor[edge.DestinationIndex];
                if (!IsValidCsrWriteIndex(reverseWriteIndex))
                {
                    edge.ReverseCsrIndex = -1;
                    _edgeBuffer[edgeIndex] = edge;
                    continue;
                }

                _edgeWriteCursor[edge.DestinationIndex] = reverseWriteIndex + 1;
                _edgeDestinations[reverseWriteIndex] = edge.SourceIndex;
                _edgeResistance[reverseWriteIndex] = edge.Resistance;
                edge.ReverseCsrIndex = reverseWriteIndex;
                AddFloodConnection(edge.DestinationIndex, edge.SourceIndex, reverseWriteIndex, edge.Resistance);
                writtenDirectedEdgeCount = math.max(writtenDirectedEdgeCount, reverseWriteIndex + 1);
                _edgeBuffer[edgeIndex] = edge;
            }

            _edgeCount = math.min(logicalDirectedEdgeCount, writtenDirectedEdgeCount);
        }

        private bool IsValidEdgeEndpoint(int nodeIndex)
        {
            return _edgeOffsets.IsCreated &&
                   _edgeWriteCursor.IsCreated &&
                   nodeIndex >= 0 &&
                   nodeIndex < _nodeCount &&
                   nodeIndex < _moduleBuffer.Count &&
                   nodeIndex + 1 < _edgeOffsets.Length &&
                   nodeIndex < _edgeWriteCursor.Length;
        }

        private bool IsValidCsrWriteIndex(int edgeIndex)
        {
            return _edgeDestinations.IsCreated &&
                   _edgeResistance.IsCreated &&
                   edgeIndex >= 0 &&
                   edgeIndex < _edgeDestinations.Length &&
                   edgeIndex < _edgeResistance.Length;
        }

        private void ResetFloodConnectionState(int directedEdgeCapacity)
        {
            if (_edgeFlags.IsCreated)
            {
                int clearCount = math.min(math.max(0, directedEdgeCapacity), _edgeFlags.Length);
                for (int edgeIndex = 0; edgeIndex < clearCount; edgeIndex++)
                    _edgeFlags[edgeIndex] = 0;
            }

            if (_roomConnections.IsCreated)
                _roomConnections.Clear();
        }

        private void AddFloodConnection(int sourceIndex, int destinationIndex, int csrEdgeIndex, float resistance)
        {
            if (!_roomConnections.IsCreated ||
                sourceIndex < 0 ||
                destinationIndex < 0 ||
                csrEdgeIndex < 0)
            {
                return;
            }

            _roomConnections.Add(sourceIndex, new HabitatFloodConnection
            {
                DestinationIndex = destinationIndex,
                CsrEdgeIndex = csrEdgeIndex,
                FlowResistance = math.max(MinimumEdgeResistance, resistance),
                Reserved0 = 0u
            });
        }

        private bool TryApplyHydroShearRupture(ref EdgeRecord edge)
        {
            ModuleRecord sourceRecord = _moduleBuffer[edge.SourceIndex];
            ModuleRecord destinationRecord = _moduleBuffer[edge.DestinationIndex];
            if (sourceRecord.IsEmergencyAirlock || destinationRecord.IsEmergencyAirlock)
                return false;

            BaseModule sourceModule = sourceRecord.BaseModule;
            BaseModule destinationModule = destinationRecord.BaseModule;
            if (sourceModule == null || destinationModule == null)
                return false;

            bool sourceFlooded = IsFloodedForHydroStress(sourceModule);
            bool destinationFlooded = IsFloodedForHydroStress(destinationModule);
            bool sourcePristine = IsPristineForHydroStress(sourceModule);
            bool destinationPristine = IsPristineForHydroStress(destinationModule);
            if (!((sourceFlooded && destinationPristine) || (destinationFlooded && sourcePristine)))
                return false;

            float sourceFloodMassKilograms = sourceModule.ResolveFloodWaterMassKilograms();
            float destinationFloodMassKilograms = destinationModule.ResolveFloodWaterMassKilograms();
            float massDeltaKilograms = math.abs(sourceFloodMassKilograms - destinationFloodMassKilograms);
            if (massDeltaKilograms <= 0f || !math.isfinite(massDeltaKilograms))
                return false;

            float shearThresholdKilograms = ResolveHydroShearThresholdKilograms(sourceModule, destinationModule);
            return massDeltaKilograms > shearThresholdKilograms;
        }

        private bool HasImplodedEndpoint(EdgeRecord edge)
        {
            BaseModule sourceModule = _moduleBuffer[edge.SourceIndex].BaseModule;
            BaseModule destinationModule = _moduleBuffer[edge.DestinationIndex].BaseModule;
            return (sourceModule != null && sourceModule.HasImploded) ||
                   (destinationModule != null && destinationModule.HasImploded);
        }

        private void MarkEdgeRuptured(ref EdgeRecord edge)
        {
            InvalidateRuntimeCsrEdge(edge.ForwardCsrIndex);
            if (!edge.DirectedOnly)
                InvalidateRuntimeCsrEdge(edge.ReverseCsrIndex);

            edge.Flags |= PipeRenderFlags.MaskRuptured;
            edge.Severed = true;
            edge.ForwardCsrIndex = -1;
            edge.ReverseCsrIndex = -1;
            RegisterSeveredEdgeRuptureVfx(in edge);
        }

        private void InvalidateRuntimeCsrEdge(int csrIndex)
        {
            if (csrIndex < 0 || !_edgeDestinations.IsCreated || csrIndex >= _edgeDestinations.Length)
                return;

            _edgeDestinations[csrIndex] = -1;
            if (_edgeResistance.IsCreated && csrIndex < _edgeResistance.Length)
                _edgeResistance[csrIndex] = 0f;
        }

        private void MarkNodeRuptured(int nodeIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= _nodeCount)
                return;

            LogisticsNetworkGraph.LogisticsNode node = _nodes[nodeIndex];
            node.Flags |= LogisticsNodeFlags.Ruptured;
            _nodes[nodeIndex] = node;

            if (nodeIndex < _moduleBuffer.Count)
                ConnectionSplineBatchRenderer.SetPipeNodeRuptured(_moduleBuffer[nodeIndex].NodeId, true);
        }

        private void RegisterSeveredEdgeRuptureVfx(in EdgeRecord edge)
        {
            if (edge.SourceIndex < 0 ||
                edge.SourceIndex >= _moduleBuffer.Count ||
                edge.DestinationIndex < 0 ||
                edge.DestinationIndex >= _moduleBuffer.Count)
            {
                return;
            }

            long linkId = ComposeLinkId(_moduleBuffer[edge.SourceIndex].NodeId, _moduleBuffer[edge.DestinationIndex].NodeId);
            if (_emittedRuptureEdgeVfxLookup.Contains(linkId))
                return;

            AbyssalFluidDecalManager fluidDecals = ResolveFluidDecalManager();
            if (fluidDecals == null || _emittedRuptureEdgeVfxKeys.Count >= _emittedRuptureEdgeVfxKeys.Capacity)
                return;

            double3 startAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3((Vector3)edge.StartSocketPosition);
            double3 endAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3((Vector3)edge.EndSocketPosition);
            Vector3 midpointRuntime = HectonFloatingOrigin.ToRuntimePosition((startAup + endAup) * 0.5d);
            float3 spanDelta = edge.EndSocketPosition - edge.StartSocketPosition;
            float spanSq = math.lengthsq(spanDelta);
            float unsupportedSpanMeters = LogisticsPipeBuilder.UnsupportedSpanMeters;
            float unsupportedSpanSq = unsupportedSpanMeters * unsupportedSpanMeters;
            float radiusScale = math.lerp(0.65f, 1.2f, math.saturate(spanSq * math.rcp(math.max(0.0001f, unsupportedSpanSq))));
            fluidDecals.RegisterRuptureFluid(midpointRuntime, radiusScale);
            _emittedRuptureEdgeVfxKeys.Add(linkId);
            _emittedRuptureEdgeVfxLookup.Add(linkId);
        }

        private AbyssalFluidDecalManager ResolveFluidDecalManager()
        {
            if (_fluidDecals != null)
                return _fluidDecals;

            _fluidDecals = Hecton8.Core.GlobalRegistry.AbyssalFluidDecals;
            return _fluidDecals;
        }

        private void EvaluateAnchorReachability()
        {
            if (_nodeCount <= 0 ||
                !_nodes.IsCreated ||
                !_edgeOffsets.IsCreated ||
                !_edgeDestinations.IsCreated ||
                !_anchorReachability.IsCreated ||
                !_anchorTraversalQueue.IsCreated)
                return;

            int safeNodeCount = math.min(
                math.min(_nodeCount, _moduleBuffer.Count),
                math.min(math.min(_nodes.Length, _anchorReachability.Length), _anchorTraversalQueue.Length));
            if (safeNodeCount <= 0)
                return;

            for (int nodeIndex = 0; nodeIndex < safeNodeCount; nodeIndex++)
            {
                _anchorReachability[nodeIndex] = 0;
                LogisticsNetworkGraph.LogisticsNode node = _nodes[nodeIndex];
                node.Flags &= ~LogisticsNodeFlags.Isolated;
                _nodes[nodeIndex] = node;
            }

            int queueHead = 0;
            int queueTail = 0;
            bool traversalOverflowed = false;
            for (int nodeIndex = 0; nodeIndex < safeNodeCount; nodeIndex++)
            {
                if (!_moduleBuffer[nodeIndex].IsAnchorNode)
                    continue;

                if (queueTail >= safeNodeCount)
                {
                    traversalOverflowed = true;
                    break;
                }

                _anchorReachability[nodeIndex] = 1;
                _anchorTraversalQueue[queueTail++] = nodeIndex;
            }

            while (queueHead < queueTail)
            {
                int currentNodeIndex = _anchorTraversalQueue[queueHead++];
                if (currentNodeIndex + 1 >= _edgeOffsets.Length)
                    continue;

                int edgeLimit = math.min(_edgeCount, _edgeDestinations.Length);
                int edgeStart = math.clamp(_edgeOffsets[currentNodeIndex], 0, edgeLimit);
                int edgeEnd = math.clamp(_edgeOffsets[currentNodeIndex + 1], edgeStart, edgeLimit);
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    int neighborNodeIndex = _edgeDestinations[edgeIndex];
                    if (neighborNodeIndex < 0 || neighborNodeIndex >= safeNodeCount)
                        continue;

                    if (_anchorReachability[neighborNodeIndex] != 0)
                        continue;

                    if (queueTail >= safeNodeCount)
                    {
                        traversalOverflowed = true;
                        break;
                    }

                    _anchorReachability[neighborNodeIndex] = 1;
                    _anchorTraversalQueue[queueTail++] = neighborNodeIndex;
                }
            }

            if (traversalOverflowed)
                WriteFloodBlackBoxSample(FloodBlackBoxTraversalOverflowFlag);

            for (int nodeIndex = 0; nodeIndex < safeNodeCount; nodeIndex++)
            {
                bool anchored = _anchorReachability[nodeIndex] != 0;
                LogisticsNetworkGraph.LogisticsNode node = _nodes[nodeIndex];
                if (!anchored)
                    node.Flags |= LogisticsNodeFlags.Isolated;

                node.Reserved = (byte)ResolveReservedState(
                    _moduleBuffer[nodeIndex].BaseModule,
                    _moduleBuffer[nodeIndex].IsAnchorNode,
                    anchored,
                    false);
                _nodes[nodeIndex] = node;
            }
        }

        private void PublishAnchorState()
        {
            int safeNodeCount = _anchorReachability.IsCreated
                ? math.min(_nodeCount, math.min(_moduleBuffer.Count, _anchorReachability.Length))
                : 0;
            for (int nodeIndex = 0; nodeIndex < safeNodeCount; nodeIndex++)
            {
                BaseModule baseModule = _moduleBuffer[nodeIndex].BaseModule;
                if (baseModule != null)
                    baseModule.SetAnchoredState(_anchorReachability[nodeIndex] != 0);
            }
        }

        private void PublishComponentPowerState()
        {
            if (_nodeCount <= 0 ||
                !_nodes.IsCreated ||
                !_edgeOffsets.IsCreated ||
                !_edgeDestinations.IsCreated ||
                !_traversalVisited.IsCreated ||
                !_anchorTraversalQueue.IsCreated)
                return;

            int safeNodeCount = math.min(
                math.min(_nodeCount, _moduleBuffer.Count),
                math.min(math.min(_nodes.Length, _traversalVisited.Length), _anchorTraversalQueue.Length));
            if (safeNodeCount <= 0)
                return;

            for (int nodeIndex = 0; nodeIndex < safeNodeCount; nodeIndex++)
                _traversalVisited[nodeIndex] = 0;

            int componentIslandOrdinal = 0;
            bool traversalOverflowed = false;
            for (int startNodeIndex = 0; startNodeIndex < safeNodeCount; startNodeIndex++)
            {
                if (_traversalVisited[startNodeIndex] != 0)
                    continue;

                int queueHead = 0;
                int queueTail = 0;
                _traversalVisited[startNodeIndex] = 1;
                _anchorTraversalQueue[queueTail++] = startNodeIndex;

                float componentSupply = 0f;
                float componentDraw = 0f;

                while (queueHead < queueTail)
                {
                    int currentNodeIndex = _anchorTraversalQueue[queueHead++];
                    float powerRating = ResolveModulePowerRating(_moduleBuffer[currentNodeIndex]);
                    if (powerRating >= 0f)
                        componentSupply += powerRating;
                    else
                        componentDraw -= powerRating;

                    if (currentNodeIndex + 1 >= _edgeOffsets.Length)
                        continue;

                    int edgeLimit = math.min(_edgeCount, _edgeDestinations.Length);
                    int edgeStart = math.clamp(_edgeOffsets[currentNodeIndex], 0, edgeLimit);
                    int edgeEnd = math.clamp(_edgeOffsets[currentNodeIndex + 1], edgeStart, edgeLimit);
                    for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                    {
                        int neighborNodeIndex = _edgeDestinations[edgeIndex];
                        if (neighborNodeIndex < 0 || neighborNodeIndex >= safeNodeCount)
                            continue;

                        if (_traversalVisited[neighborNodeIndex] != 0)
                            continue;

                        if (queueTail >= safeNodeCount)
                        {
                            traversalOverflowed = true;
                            break;
                        }

                        _traversalVisited[neighborNodeIndex] = 1;
                        _anchorTraversalQueue[queueTail++] = neighborNodeIndex;
                    }
                }

                bool componentLowPower = componentDraw > componentSupply + 0.001f &&
                                         PowerGridManager.ResolveProjectedBrownoutTier(componentSupply, componentDraw) != LogisticsBrownoutTier.None;
                byte componentIslandId = (byte)math.min(componentIslandOrdinal, byte.MaxValue);
                for (int queueIndex = 0; queueIndex < queueTail; queueIndex++)
                {
                    int componentNodeIndex = _anchorTraversalQueue[queueIndex];
                    LogisticsNetworkGraph.LogisticsNode node = _nodes[componentNodeIndex];
                    node.NetworkId = componentIslandId;
                    if (componentLowPower)
                        node.Flags |= LogisticsNodeFlags.Brownout;
                    else
                        node.Flags &= ~LogisticsNodeFlags.Brownout;

                    _nodes[componentNodeIndex] = node;

                    BaseModule baseModule = _moduleBuffer[componentNodeIndex].BaseModule;
                    if (baseModule != null)
                        baseModule.SetAmbientLightsBrownout(componentLowPower);
                }

                componentIslandOrdinal++;
            }

            if (traversalOverflowed)
                WriteFloodBlackBoxSample(FloodBlackBoxTraversalOverflowFlag);
        }

        private void PublishEmergencyLockdownState()
        {
            if (_nodeCount <= 0 || !_nodes.IsCreated)
                return;

            int maxNodeCount = math.min(_nodeCount, _moduleBuffer.Count);
            maxNodeCount = math.min(maxNodeCount, _nodes.Length);
            if (_edgeOffsets.IsCreated)
                maxNodeCount = math.min(maxNodeCount, _edgeOffsets.Length - 1);

            if (maxNodeCount <= 0)
                return;

            for (int nodeIndex = 0; nodeIndex < maxNodeCount; nodeIndex++)
            {
                ModuleRecord module = _moduleBuffer[nodeIndex];
                BaseModule baseModule = module.BaseModule;
                if (baseModule == null)
                    continue;

                bool shouldLock = false;
                bool blockManualOverride = false;
                bool hasAdjacent = false;
                bool adjacentFloodedForHatch = false;
                bool adjacentRupturedForHatch = false;
                int edgeLimit = math.min(_edgeCount, _edgeDestinations.IsCreated ? _edgeDestinations.Length : 0);
                int edgeStart = 0;
                int edgeEnd = 0;
                if (_edgeOffsets.IsCreated && nodeIndex + 1 < _edgeOffsets.Length)
                {
                    edgeStart = math.clamp(_edgeOffsets[nodeIndex], 0, edgeLimit);
                    edgeEnd = math.clamp(_edgeOffsets[nodeIndex + 1], edgeStart, edgeLimit);
                }

                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    int adjacentNodeIndex = _edgeDestinations[edgeIndex];
                    if (adjacentNodeIndex < 0 || adjacentNodeIndex >= maxNodeCount)
                        continue;

                    hasAdjacent = true;
                    LogisticsNodeFlags adjacentFlags = _nodes[adjacentNodeIndex].Flags;
                    BaseModule adjacentModule = _moduleBuffer[adjacentNodeIndex].BaseModule;
                    bool adjacentRuptured = (adjacentFlags & LogisticsNodeFlags.Ruptured) != 0 ||
                                            (adjacentModule != null && adjacentModule.IntegrityState == BaseModuleIntegrityState.Ruptured);
                    bool adjacentFlooded = adjacentModule != null && adjacentModule.IsFlooded;
                    adjacentRupturedForHatch |= adjacentRuptured;
                    adjacentFloodedForHatch |= adjacentFlooded;

                    if (!module.IsEmergencyAirlock || (!adjacentRuptured && !adjacentFlooded))
                        continue;

                    shouldLock = true;
                    if (ResolveAuthoritativeRoomWaterLevel01(adjacentNodeIndex, adjacentModule) >= EmergencyFloodLockdownThreshold01)
                    {
                        blockManualOverride = true;
                        break;
                    }
                }

                baseModule.SetEmergencyBulkheadLockdown(shouldLock, blockManualOverride);
                if (baseModule.TryGetComponent(out TransitionHatchMeshState hatchMeshState))
                {
                    hatchMeshState.ApplyAdjacentFlags(TransitionHatchMeshState.BuildAdjacentFlags(
                        hasAdjacent,
                        adjacentFloodedForHatch,
                        adjacentRupturedForHatch,
                        shouldLock));
                }

                LogisticsNetworkGraph.LogisticsNode node = _nodes[nodeIndex];
                bool anchorReachable = _anchorReachability.IsCreated &&
                                       nodeIndex < _anchorReachability.Length &&
                                       _anchorReachability[nodeIndex] != 0;
                node.Reserved = (byte)ResolveReservedState(
                    baseModule,
                    module.IsAnchorNode,
                    anchorReachable,
                    shouldLock);
                _nodes[nodeIndex] = node;
            }

            PublishFloodEdgeFlags();
        }

        private void PublishFloodEdgeFlags()
        {
            ClearActiveFloodEdgeFlags();

            if (!_edgeOffsets.IsCreated ||
                !_edgeDestinations.IsCreated ||
                !_nodes.IsCreated ||
                _nodeCount <= 0)
            {
                return;
            }

            int maxNodeCount = math.min(_nodeCount, _moduleBuffer.Count);
            for (int nodeIndex = 0; nodeIndex < maxNodeCount; nodeIndex++)
            {
                BaseModule baseModule = _moduleBuffer[nodeIndex].BaseModule;
                bool moduleAutoSealActive = IsFloodAutoSealActive(nodeIndex, baseModule);
                bool moduleLocked = baseModule != null && baseModule.IsEmergencyBulkheadLockedDown;
                if (nodeIndex + 1 >= _edgeOffsets.Length)
                    break;

                int edgeLimit = math.min(_edgeCount, _edgeDestinations.Length);
                int edgeStart = math.clamp(_edgeOffsets[nodeIndex], 0, edgeLimit);
                int edgeEnd = math.clamp(_edgeOffsets[nodeIndex + 1], edgeStart, edgeLimit);
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    int adjacentNodeIndex = _edgeDestinations[edgeIndex];
                    if (adjacentNodeIndex < 0 || adjacentNodeIndex >= maxNodeCount)
                        continue;

                    BaseModule adjacentModule = _moduleBuffer[adjacentNodeIndex].BaseModule;
                    LogisticsNodeFlags adjacentFlags = _nodes[adjacentNodeIndex].Flags;
                    bool adjacentRuptured = (adjacentFlags & LogisticsNodeFlags.Ruptured) != 0 ||
                                            (adjacentModule != null && adjacentModule.IntegrityState == BaseModuleIntegrityState.Ruptured);

                    if (moduleAutoSealActive ||
                        IsFloodAutoSealActive(adjacentNodeIndex, adjacentModule) ||
                        moduleLocked ||
                        (adjacentModule != null && adjacentModule.IsEmergencyBulkheadLockedDown))
                    {
                        SetFloodEdgeFlag(edgeIndex, HabitatEdgeFloodFlags.Sealed);
                    }

                    if (adjacentRuptured)
                        SetFloodEdgeFlag(edgeIndex, HabitatEdgeFloodFlags.Ruptured);
                }
            }
        }

        private void PublishDegradationState()
        {
            if (!_nodes.IsCreated)
                return;

            int maxNodeCount = math.min(math.max(0, _nodeCount), math.min(_moduleBuffer.Count, _nodes.Length));
            for (int nodeIndex = 0; nodeIndex < maxNodeCount; nodeIndex++)
            {
                ModuleRecord module = _moduleBuffer[nodeIndex];
                if (module.ModuleObject == null)
                    continue;

                BaseDegradationSystem.SynchronizeNode(
                    module.ModuleObject,
                    module.NodeId,
                    _nodes[nodeIndex].Flags,
                    ResolveNodeRuptureWorldPoint(nodeIndex));
            }
        }

        private void PublishSiegeTargetSnapshot()
        {
            if (!_siegeTargets.IsCreated)
                return;

            int writeCount = 0;
            int maxNodeCount = math.min(_nodeCount, _moduleBuffer.Count);
            for (int nodeIndex = 0; nodeIndex < maxNodeCount && writeCount < MaxSiegeTargetCount; nodeIndex++)
            {
                ModuleRecord module = _moduleBuffer[nodeIndex];
                BaseModule baseModule = module.BaseModule;
                if (baseModule == null || !baseModule.isActiveAndEnabled)
                    continue;

                LogisticsNodeFlags nodeFlags = _nodes.IsCreated && nodeIndex < _nodes.Length
                    ? _nodes[nodeIndex].Flags
                    : LogisticsNodeFlags.None;
                float integrity01 = math.saturate(baseModule.IntegrityStateNormalized);
                HabitatSiegeTargetFlags siegeFlags = ResolveSiegeTargetFlags(module, baseModule, nodeFlags, integrity01);
                if ((siegeFlags & HabitatSiegeTargetFlags.Vulnerable) == 0)
                    continue;

                _siegeTargets[writeCount++] = new HabitatSiegeTargetSnapshot
                {
                    ModuleCenter = module.Position,
                    WeakPoint = ResolveNodeRuptureWorldPoint(nodeIndex),
                    Integrity01 = integrity01,
                    Vulnerability01 = ResolveSiegeVulnerability01(baseModule, nodeFlags, integrity01),
                    NodeId = module.NodeId,
                    Flags = (byte)siegeFlags
                };
            }

            for (int i = writeCount; i < _siegeTargetCount; i++)
                _siegeTargets[i] = default;

            _siegeTargetCount = writeCount;
            s_latestSiegeTargets = _siegeTargets;
            s_latestSiegeTargetOwner = this;
            s_latestSiegeTargetCount = writeCount;
        }

        private void ClearSiegeTargetSnapshot()
        {
            if (_siegeTargets.IsCreated)
            {
                for (int i = 0; i < _siegeTargetCount; i++)
                    _siegeTargets[i] = default;
            }

            _siegeTargetCount = 0;
            if (ReferenceEquals(s_latestSiegeTargetOwner, this))
            {
                s_latestSiegeTargets = default;
                s_latestSiegeTargetOwner = null;
                s_latestSiegeTargetCount = 0;
            }
        }

        private void PublishGraphKernel()
        {
            int maxNodeCount = _nodes.IsCreated
                ? math.min(math.max(0, _nodeCount), math.min(_moduleBuffer.Count, _nodes.Length))
                : 0;
            _graph.BeginBuild(LogisticsNetworkType.OxygenPressure, maxNodeCount, math.max(1, _edgeCount), 0);

            for (int nodeIndex = 0; nodeIndex < maxNodeCount; nodeIndex++)
            {
                LogisticsNetworkGraph.LogisticsNode node = _nodes[nodeIndex];
                _graph.AddNode(node.Id, node.Capacity, node.Resistance, node.Priority, node.Flags, node.Reserved);
            }

            for (int edgeIndex = 0; edgeIndex < _edgeBuffer.Count; edgeIndex++)
            {
                EdgeRecord edge = _edgeBuffer[edgeIndex];
                if (edge.Severed ||
                    edge.SourceIndex < 0 ||
                    edge.DestinationIndex < 0 ||
                    edge.SourceIndex >= maxNodeCount ||
                    edge.DestinationIndex >= maxNodeCount)
                {
                    continue;
                }

                _graph.AddEdge(edge.SourceIndex, edge.DestinationIndex, edge.Resistance);
                if (!edge.DirectedOnly)
                    _graph.AddEdge(edge.DestinationIndex, edge.SourceIndex, edge.Resistance);
            }

            _graph.FinalizeBuild();
        }

        private void PublishRuntimeRuptureTopologyState()
        {
            BuildEdgeRecords();
            EvaluateAnchorReachability();
            PublishAnchorState();
            PublishComponentPowerState();
            PublishEmergencyLockdownState();
            PublishDegradationState();
            PublishSiegeTargetSnapshot();
            PublishGraphKernel();
            ClearVisualLinks();
            PublishVisualLinks();
        }

        private void PublishVisualLinks()
        {
            int edgeCount = _edgeBuffer.Count;
            if (_submittedLinkIds.Capacity < edgeCount)
                _submittedLinkIds.Capacity = edgeCount;

            for (int edgeIndex = 0; edgeIndex < edgeCount; edgeIndex++)
            {
                EdgeRecord edge = _edgeBuffer[edgeIndex];
                long linkId = ComposeLinkId(_moduleBuffer[edge.SourceIndex].NodeId, _moduleBuffer[edge.DestinationIndex].NodeId);
                SplineDescriptor descriptor = LogisticsPipeBuilder.CreateSocketDescriptor(
                    edge.StartSocketPosition,
                    edge.EndSocketPosition,
                    edge.StartForward,
                    edge.EndForward,
                    LogisticsPipeBuilder.DefaultPipeRadiusMeters,
                    edge.Flags);

                ConnectionSplineBatchRenderer.SubmitPipeLink(linkId, descriptor, PipeSplineColor);
                _submittedLinkIds.Add(linkId);
            }
        }

        private void ClearVisualLinks()
        {
            for (int i = 0; i < _submittedLinkIds.Count; i++)
                ConnectionSplineBatchRenderer.RemovePipeLink(_submittedLinkIds[i]);

            _submittedLinkIds.Clear();
        }

        private Vector3 ResolveNodeRuptureWorldPoint(int nodeIndex)
        {
            for (int edgeIndex = 0; edgeIndex < _edgeBuffer.Count; edgeIndex++)
            {
                EdgeRecord edge = _edgeBuffer[edgeIndex];
                if (!LogisticsPipeBuilder.HasRupturedMask(edge.Flags))
                    continue;

                if (edge.SourceIndex == nodeIndex)
                    return edge.StartSocketPosition;

                if (edge.DestinationIndex == nodeIndex)
                    return edge.EndSocketPosition;
            }

            return _moduleBuffer[nodeIndex].Position;
        }

        private bool HasIntermediateSupport(int sourceIndex, int destinationIndex, float3 start, float3 end)
        {
            for (int moduleIndex = 0; moduleIndex < _moduleBuffer.Count; moduleIndex++)
            {
                if (moduleIndex == sourceIndex || moduleIndex == destinationIndex)
                    continue;

                if (!IsPipeSpanSupportModule(_moduleBuffer[moduleIndex]))
                    continue;

                float projection;
                float distanceSq = DistancePointToSegmentSq(_moduleBuffer[moduleIndex].Position, start, end, out projection);
                if (projection > 0.1f &&
                    projection < 0.9f &&
                    distanceSq <= SupportCaptureRadiusSq)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPipeSpanSupportModule(ModuleRecord module)
        {
            if (module.IsAnchorNode)
                return true;

            ModuleMarker marker = module.Marker;
            string persistentId = marker != null ? marker.PrefabId : string.Empty;
            return string.Equals(persistentId, "Build_Utility_Pylon", StringComparison.Ordinal);
        }

        private static float DistancePointToSegmentSq(float3 point, float3 start, float3 end, out float projection)
        {
            float3 segment = end - start;
            float segmentLengthSq = math.lengthsq(segment);
            if (segmentLengthSq <= 0.000001f)
            {
                projection = 0f;
                return math.lengthsq(point - start);
            }

            projection = math.saturate(math.dot(point - start, segment) * math.rcp(segmentLengthSq));
            float3 closestPoint = start + segment * projection;
            return math.lengthsq(point - closestPoint);
        }

        private static float ResolveNodeCapacity(ModuleMarker marker, BaseModule baseModule)
        {
            float capacity = 8f;
            if (marker != null && marker.Data != null)
                capacity += math.abs(marker.Data.powerRating) * 0.01f;

            if (baseModule != null)
                capacity += math.max(0f, baseModule.MaxIntegrity * 0.05f);

            return math.max(1f, capacity);
        }

        private static float ResolveHydroStructuralLoadNewtons(BaseModule baseModule)
        {
            if (baseModule == null)
                return 0f;

            float floodWaterMassKilograms = baseModule.ResolveFloodWaterMassKilograms();
            float parasiteMassKilograms = baseModule.ResolveParasiteAddedMassKilograms();
            float structuralMassKilograms = floodWaterMassKilograms + parasiteMassKilograms;
            if (structuralMassKilograms <= 0f || !math.isfinite(structuralMassKilograms))
                return 0f;

            float loadNewtons = structuralMassKilograms * GravityAccelerationMetersPerSecondSquared;
            return math.isfinite(loadNewtons) ? math.max(0f, loadNewtons) : 0f;
        }

        private static float ResolveNodeResistance(BaseModule baseModule)
        {
            if (baseModule == null)
                return 0.25f;

            float resistance = 0.15f;
            if (baseModule.IsFlooded)
                resistance += 0.15f;

            if (!baseModule.HasPower)
                resistance += 0.1f;

            return resistance;
        }

        private static float ResolveProjectedDragAreaSquareMeters(BaseModule baseModule)
        {
            if (baseModule == null)
                return 0f;

            Hecton8.Building.BaseModuleTemplate template = baseModule.ModuleTemplate;
            if (template != null)
                return math.max(0.1f, template.ProjectedDragAreaSquareMeters);

            return 12f;
        }

        private static float ResolveYieldStrengthNewtons(BaseModule baseModule)
        {
            if (baseModule == null)
                return 0f;

            Hecton8.Building.BaseModuleTemplate template = baseModule.ModuleTemplate;
            if (template != null)
                return math.max(1f, template.ModuleYieldStrengthNewtons);

            return 180000f;
        }

        private static float ResolveHydroShearThresholdKilograms(BaseModule sourceModule, BaseModule destinationModule)
        {
            float sourceYieldMassKilograms = ResolveYieldStrengthNewtons(sourceModule) * GravityAccelerationMetersPerSecondSquaredInv;
            float destinationYieldMassKilograms = ResolveYieldStrengthNewtons(destinationModule) * GravityAccelerationMetersPerSecondSquaredInv;
            float weakestYieldMassKilograms = math.min(sourceYieldMassKilograms, destinationYieldMassKilograms);
            if (weakestYieldMassKilograms <= 0f || !math.isfinite(weakestYieldMassKilograms))
                weakestYieldMassKilograms = DefaultHydroShearThresholdKilograms;

            return math.max(1f, math.min(DefaultHydroShearThresholdKilograms, weakestYieldMassKilograms));
        }

        private static bool IsFloodedForHydroStress(BaseModule baseModule)
        {
            if (baseModule == null)
                return false;

            BaseModuleIntegrityState state = baseModule.IntegrityState;
            return baseModule.IsFlooded ||
                   state == BaseModuleIntegrityState.Flooded ||
                   state == BaseModuleIntegrityState.Ruptured;
        }

        private static bool IsPristineForHydroStress(BaseModule baseModule)
        {
            return baseModule != null &&
                   !baseModule.IsFlooded &&
                   !baseModule.IsBreached &&
                   baseModule.IntegrityState == BaseModuleIntegrityState.Pristine;
        }

        private static byte ResolveNodePriority(ModuleMarker marker)
        {
            if (marker == null || marker.Data == null)
                return 48;

            switch (marker.Data.family)
            {
                case BuildableFamily.Habitat:
                    return 12;

                case BuildableFamily.Utility:
                    return 24;

                case BuildableFamily.Logistics:
                    return 36;

                default:
                    return 48;
            }
        }

        private static LogisticsNodeFlags ResolveNodeFlags(BaseModule baseModule)
        {
            LogisticsNodeFlags flags = LogisticsNodeFlags.Active;
            if (baseModule != null && baseModule.HasCascadeFailure)
                flags |= LogisticsNodeFlags.Dirty;
            if (baseModule != null && (baseModule.HasImploded || BaseDegradationSystem.IsModuleRuptured(baseModule)))
                flags |= LogisticsNodeFlags.Ruptured;

            return flags;
        }

        private static float ResolveFungalMindPotentialScore(ModuleRecord module, LogisticsNetworkGraph.LogisticsNode node)
        {
            float nodePotential = math.abs(node.Potential);
            float nodeLoad = math.abs(node.CurrentLoad);
            float modulePower = math.abs(ResolveModulePowerRating(module));
            float score = math.max(nodePotential, math.max(nodeLoad, modulePower));
            return math.isfinite(score) ? score : 0f;
        }

        private static float ResolveModulePowerRating(ModuleRecord module)
        {
            if (module.BaseModule != null)
                return module.BaseModule.PowerRatingForHabitatGraph;

            if (module.Marker != null && module.Marker.Data != null)
                return module.Marker.Data.powerRating;

            return 0f;
        }

        private static LogisticsModuleStatusBits ResolveReservedState(BaseModule baseModule, bool isAnchorNode, bool isAnchored, bool emergencyLockdown)
        {
            LogisticsModuleStatusBits bits = LogisticsModuleStatusBits.None;
            if (baseModule != null && baseModule.HasPower)
                bits |= LogisticsModuleStatusBits.Powered;
            bits |= (LogisticsModuleStatusBits)math.select(
                0,
                (int)LogisticsModuleStatusBits.Flooded,
                baseModule != null && baseModule.IsFlooded);
            if (baseModule != null && baseModule.CurrentIntegrity < baseModule.MaxIntegrity)
                bits |= LogisticsModuleStatusBits.Damaged;
            if (isAnchorNode)
                bits |= LogisticsModuleStatusBits.AnchorNode;
            if (isAnchored)
                bits |= LogisticsModuleStatusBits.Anchored;
            else
                bits |= LogisticsModuleStatusBits.Unmoored;
            if (emergencyLockdown)
                bits |= LogisticsModuleStatusBits.EmergencyLockdown;

            return bits;
        }

        private static HabitatSiegeTargetFlags ResolveSiegeTargetFlags(
            ModuleRecord module,
            BaseModule baseModule,
            LogisticsNodeFlags nodeFlags,
            float integrity01)
        {
            HabitatSiegeTargetFlags flags = HabitatSiegeTargetFlags.None;
            if (module.IsEmergencyAirlock)
                flags |= HabitatSiegeTargetFlags.EmergencyAirlock;

            if (baseModule.IsFlooded || baseModule.IntegrityState == BaseModuleIntegrityState.Flooded)
                flags |= HabitatSiegeTargetFlags.Flooded;

            if (baseModule.IsBreached || baseModule.IntegrityState == BaseModuleIntegrityState.Ruptured)
                flags |= HabitatSiegeTargetFlags.Ruptured;

            if (baseModule.HasCascadeFailure || baseModule.CurrentFailureMode != BaseModuleFailureMode.None)
                flags |= HabitatSiegeTargetFlags.CascadeFailure;

            if ((nodeFlags & LogisticsNodeFlags.Brownout) != 0 || !baseModule.HasPower)
                flags |= HabitatSiegeTargetFlags.Brownout;

            if ((nodeFlags & LogisticsNodeFlags.Isolated) != 0)
                flags |= HabitatSiegeTargetFlags.Isolated;

            bool vulnerable =
                integrity01 <= SiegeVulnerableIntegrityThreshold01 ||
                (flags & (HabitatSiegeTargetFlags.Flooded |
                          HabitatSiegeTargetFlags.Ruptured |
                          HabitatSiegeTargetFlags.Brownout |
                          HabitatSiegeTargetFlags.Isolated |
                          HabitatSiegeTargetFlags.CascadeFailure)) != 0;
            if (vulnerable)
                flags |= HabitatSiegeTargetFlags.Vulnerable;

            return flags;
        }

        private static float ResolveSiegeVulnerability01(BaseModule baseModule, LogisticsNodeFlags nodeFlags, float integrity01)
        {
            float vulnerability = 1f - integrity01;
            if (baseModule.IsFlooded || baseModule.IntegrityState == BaseModuleIntegrityState.Flooded)
                vulnerability += 0.35f;

            if (baseModule.IsBreached || baseModule.IntegrityState == BaseModuleIntegrityState.Ruptured)
                vulnerability += 0.55f;

            if (baseModule.HasCascadeFailure || baseModule.CurrentFailureMode != BaseModuleFailureMode.None)
                vulnerability += 0.2f;

            if ((nodeFlags & LogisticsNodeFlags.Brownout) != 0 || !baseModule.HasPower)
                vulnerability += 0.15f;

            if ((nodeFlags & LogisticsNodeFlags.Isolated) != 0)
                vulnerability += 0.15f;

            return math.saturate(vulnerability);
        }

        private static bool ResolveStructuralAnchorState(BaseModule baseModule, ModuleMarker marker)
        {
            if (baseModule != null && baseModule.ResolveStructuralAnchorRole(marker))
                return true;

            string persistentId = marker != null ? marker.PrefabId : string.Empty;
            return string.Equals(persistentId, "Build_Foundation_Platform", StringComparison.Ordinal) ||
                   string.Equals(persistentId, "Build_Utility_Pylon", StringComparison.Ordinal);
        }

        private static bool ResolveEmergencyAirlockState(BaseModule baseModule, ModuleMarker marker)
        {
            if (baseModule != null && baseModule.ResolveEmergencyAirlockRole(marker))
                return true;

            string persistentId = marker != null ? marker.PrefabId : string.Empty;
            return string.Equals(persistentId, "Build_Airlock_Hatch", StringComparison.Ordinal) ||
                   string.Equals(persistentId, "base.module.airlock", StringComparison.Ordinal);
        }

        private static int QuantizeAxis(Vector3 direction)
        {
            if (!math.isfinite(direction.x) || !math.isfinite(direction.y) || !math.isfinite(direction.z))
                return 4;

            float absX = math.abs(direction.x);
            float absY = math.abs(direction.y);
            float absZ = math.abs(direction.z);
            if ((absX + absY + absZ) <= 0.0001f)
                return 4;

            if (absX >= absY && absX >= absZ)
                return direction.x >= 0f ? 0 : 1;

            if (absY >= absX && absY >= absZ)
                return direction.y >= 0f ? 2 : 3;

            return direction.z >= 0f ? 4 : 5;
        }

        private static Vector3 ResolveFastDirection(Vector3 direction, float sqrMagnitude)
        {
            if (!math.isfinite(sqrMagnitude) || sqrMagnitude <= 0.0001f)
                return Vector3.up;

            return direction * math.rsqrt(sqrMagnitude);
        }

        private static float ResolveFastLengthFromSq(float sqrMagnitude)
        {
            float safeSq = math.max(0f, sqrMagnitude);
            return math.isfinite(safeSq) && safeSq > 0.0001f
                ? safeSq * math.rsqrt(safeSq)
                : 0f;
        }

        private static int OppositeAxis(int axis)
        {
            switch (axis)
            {
                case 0: return 1;
                case 1: return 0;
                case 2: return 3;
                case 3: return 2;
                case 4: return 5;
                default: return 4;
            }
        }

        private static long ComposeLinkId(uint left, uint right)
        {
            uint min = math.min(left, right);
            uint max = math.max(left, right);
            return ((long)min << 32) | max;
        }

        private void AllocateNativeBuffers(int nodeCapacity, int edgeCapacity)
        {
            // COLD ALLOC: NativeArray<LogisticsNode>[64] — habitat node snapshot buffer — owner: HabitatGraphManager
            _nodes = new NativeArray<LogisticsNetworkGraph.LogisticsNode>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Int32>[65] — habitat CSR edge-offset buffer — owner: HabitatGraphManager
            _edgeOffsets = new NativeArray<int>(nodeCapacity + 1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Int32>[128] — habitat CSR destination buffer — owner: HabitatGraphManager
            _edgeDestinations = new NativeArray<int>(edgeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Single>[128] — habitat CSR edge-resistance buffer — owner: HabitatGraphManager
            _edgeResistance = new NativeArray<float>(edgeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Int32>[64] — CSR write-cursor scratch buffer — owner: HabitatGraphManager
            _edgeWriteCursor = new NativeArray<int>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Byte>[64] — authoritative anchor reachability state for habitat graph consumers — owner: HabitatGraphManager
            _anchorReachability = new NativeArray<byte>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Byte>[64] — graph traversal visited scratch, separate from anchor-state truth — owner: HabitatGraphManager
            _traversalVisited = new NativeArray<byte>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Int32>[64] — reusable BFS traversal queue for graph component walks — owner: HabitatGraphManager
            _anchorTraversalQueue = new NativeArray<int>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<HabitatSiegeTargetSnapshot>[64] — capped habitat weak-point snapshot for headless predator siege jobs — owner: HabitatGraphManager
            _siegeTargets = new NativeArray<HabitatSiegeTargetSnapshot>(MaxSiegeTargetCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Single>[64] - habitat room water level SoA lane - owner: HabitatGraphManager
            _roomWaterLevels = new NativeArray<float>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Single>[64] - habitat room volume SoA lane - owner: HabitatGraphManager
            _roomVolumes = new NativeArray<float>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Single>[64] - Burst flood propagation delta lane - owner: HabitatGraphManager
            _roomFloodDeltaLevels = new NativeArray<float>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Single>[64] - per-module shader pressure stress lane - owner: HabitatGraphManager
            _moduleStressScalars = new NativeArray<float>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Single>[64] - stress delta lane for structural acoustics sync - owner: HabitatGraphManager
            _previousModuleStressScalars = new NativeArray<float>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Single>[64] - one-second impact spike lane for deformation solver - owner: HabitatGraphManager
            _moduleImpactStressSpikes = new NativeArray<float>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Byte>[64] - compromised-event hysteresis lane - owner: HabitatGraphManager
            _moduleCompromisedFlags = new NativeArray<byte>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Byte>[64] - habitat room flood flags SoA lane - owner: HabitatGraphManager
            _roomFlags = new NativeArray<byte>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Byte>[128] - habitat directed edge flood flags - owner: HabitatGraphManager
            _edgeFlags = new NativeArray<byte>(edgeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeParallelMultiHashMap<Int32,HabitatFloodConnection>[128] - room connection index for flood jobs - owner: HabitatGraphManager
            _roomConnections = new NativeParallelMultiHashMap<int, HabitatFloodConnection>(edgeCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeArray<HabitatFloodBlackBoxEntry>[300] - fixed habitat flood telemetry ring - owner: HabitatGraphManager
            _floodBlackBox = new NativeArray<HabitatFloodBlackBoxEntry>(FloodBlackBoxCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<HabitatFloodPropagationSummary>[1] - Burst flood job result slot - owner: HabitatGraphManager
            _floodPropagationSummary = new NativeArray<HabitatFloodPropagationSummary>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _floodBlackBoxCursor = 0;
            _floodBlackBoxDumped = false;
            _moduleStressBlackBoxDumped = false;
            _lastUploadedModuleStressCount = -1;
            _lastUploadedPeakModuleStress01 = -1f;
            _lastUploadedModuleStressLowTier = false;
            _moduleStressOrderHash = 0u;
            _peakModuleStress01 = 0f;
            RegisterNativeMemorySentinel();
        }

        private void EnsureNodeCapacity(int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            if (_nodes.IsCreated &&
                _nodes.Length >= safeLength &&
                _edgeOffsets.Length >= safeLength + 1 &&
                _edgeWriteCursor.Length >= safeLength &&
                _anchorReachability.Length >= safeLength &&
                _traversalVisited.IsCreated &&
                _traversalVisited.Length >= safeLength &&
                _anchorTraversalQueue.Length >= safeLength &&
                _siegeTargets.IsCreated &&
                _siegeTargets.Length >= MaxSiegeTargetCount &&
                _roomWaterLevels.IsCreated &&
                _roomWaterLevels.Length >= safeLength &&
                _roomVolumes.IsCreated &&
                _roomVolumes.Length >= safeLength &&
                _roomFloodDeltaLevels.IsCreated &&
                _roomFloodDeltaLevels.Length >= safeLength &&
                _moduleStressScalars.IsCreated &&
                _moduleStressScalars.Length >= safeLength &&
                _previousModuleStressScalars.IsCreated &&
                _previousModuleStressScalars.Length >= safeLength &&
                _moduleImpactStressSpikes.IsCreated &&
                _moduleImpactStressSpikes.Length >= safeLength &&
                _moduleCompromisedFlags.IsCreated &&
                _moduleCompromisedFlags.Length >= safeLength &&
                _roomFlags.IsCreated &&
                _roomFlags.Length >= safeLength &&
                _floodBlackBox.IsCreated &&
                _floodBlackBox.Length >= FloodBlackBoxCapacity &&
                _floodPropagationSummary.IsCreated &&
                _floodPropagationSummary.Length >= 1)
                return;

            DisposeNativeBuffers();
            int nodeCapacity = NextPowerOfTwo(math.max(safeLength, InitialNodeCapacity));
            int edgeCapacity = NextPowerOfTwo(math.max(nodeCapacity * 4, InitialEdgeCapacity));
            AllocateNativeBuffers(nodeCapacity, edgeCapacity);
        }

        private void EnsureEdgeCapacity(int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            if (_edgeDestinations.IsCreated &&
                _edgeDestinations.Length >= safeLength &&
                _edgeResistance.Length >= safeLength &&
                _edgeFlags.IsCreated &&
                _edgeFlags.Length >= safeLength &&
                _roomConnections.IsCreated &&
                _roomConnections.Capacity >= safeLength)
            {
                return;
            }

            DisposeNativeArray(ref _edgeDestinations);
            DisposeNativeArray(ref _edgeResistance);
            DisposeNativeArray(ref _edgeFlags);
            DisposeNativeParallelMultiHashMap(ref _roomConnections, nameof(_roomConnections));

            int edgeCapacity = NextPowerOfTwo(math.max(safeLength, InitialEdgeCapacity));
            // COLD ALLOC: NativeArray<Int32>[edgeCapacity] - expanded habitat CSR destination buffer - owner: HabitatGraphManager
            _edgeDestinations = new NativeArray<int>(edgeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Single>[edgeCapacity] - expanded habitat CSR edge-resistance buffer - owner: HabitatGraphManager
            _edgeResistance = new NativeArray<float>(edgeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Byte>[edgeCapacity] - expanded habitat directed edge flood flags - owner: HabitatGraphManager
            _edgeFlags = new NativeArray<byte>(edgeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeParallelMultiHashMap<Int32,HabitatFloodConnection>[edgeCapacity] - expanded room connection index - owner: HabitatGraphManager
            _roomConnections = new NativeParallelMultiHashMap<int, HabitatFloodConnection>(edgeCapacity, Allocator.Persistent);
            NativeMemorySentinel.RegisterNativeArray(_edgeDestinations, NativeMemoryOwner, nameof(_edgeDestinations), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_edgeResistance, NativeMemoryOwner, nameof(_edgeResistance), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_edgeFlags, NativeMemoryOwner, nameof(_edgeFlags), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeParallelMultiHashMap(_roomConnections, NativeMemoryOwner, nameof(_roomConnections), NativeMemoryLifetime);
        }

        private void DisposeNativeBuffers()
        {
            ClearSiegeTargetSnapshot();

            DisposeNativeArray(ref _nodes);
            DisposeNativeArray(ref _edgeOffsets);
            DisposeNativeArray(ref _edgeDestinations);
            DisposeNativeArray(ref _edgeResistance);
            DisposeNativeArray(ref _edgeWriteCursor);
            DisposeNativeArray(ref _anchorReachability);
            DisposeNativeArray(ref _traversalVisited);
            DisposeNativeArray(ref _anchorTraversalQueue);
            DisposeNativeArray(ref _siegeTargets);
            DisposeNativeArray(ref _roomWaterLevels);
            DisposeNativeArray(ref _roomVolumes);
            DisposeNativeArray(ref _roomFloodDeltaLevels);
            DisposeNativeArray(ref _moduleStressScalars);
            DisposeNativeArray(ref _previousModuleStressScalars);
            DisposeNativeArray(ref _moduleImpactStressSpikes);
            DisposeNativeArray(ref _moduleCompromisedFlags);
            DisposeNativeArray(ref _roomFlags);
            DisposeNativeArray(ref _edgeFlags);
            DisposeNativeArray(ref _floodBlackBox);
            DisposeNativeArray(ref _floodPropagationSummary);
            DisposeNativeParallelMultiHashMap(ref _roomConnections, nameof(_roomConnections));
            ReleaseModuleStressBuffer();
        }

        private void RegisterNativeMemorySentinel()
        {
            NativeMemorySentinel.RegisterNativeArray(_nodes, NativeMemoryOwner, nameof(_nodes), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_edgeOffsets, NativeMemoryOwner, nameof(_edgeOffsets), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_edgeDestinations, NativeMemoryOwner, nameof(_edgeDestinations), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_edgeResistance, NativeMemoryOwner, nameof(_edgeResistance), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_edgeWriteCursor, NativeMemoryOwner, nameof(_edgeWriteCursor), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_anchorReachability, NativeMemoryOwner, nameof(_anchorReachability), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_traversalVisited, NativeMemoryOwner, nameof(_traversalVisited), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_anchorTraversalQueue, NativeMemoryOwner, nameof(_anchorTraversalQueue), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_siegeTargets, NativeMemoryOwner, nameof(_siegeTargets), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_roomWaterLevels, NativeMemoryOwner, nameof(_roomWaterLevels), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_roomVolumes, NativeMemoryOwner, nameof(_roomVolumes), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_roomFloodDeltaLevels, NativeMemoryOwner, nameof(_roomFloodDeltaLevels), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_moduleStressScalars, NativeMemoryOwner, nameof(_moduleStressScalars), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_previousModuleStressScalars, NativeMemoryOwner, nameof(_previousModuleStressScalars), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_moduleImpactStressSpikes, NativeMemoryOwner, nameof(_moduleImpactStressSpikes), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_moduleCompromisedFlags, NativeMemoryOwner, nameof(_moduleCompromisedFlags), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_roomFlags, NativeMemoryOwner, nameof(_roomFlags), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_edgeFlags, NativeMemoryOwner, nameof(_edgeFlags), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_floodBlackBox, NativeMemoryOwner, nameof(_floodBlackBox), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_floodPropagationSummary, NativeMemoryOwner, nameof(_floodPropagationSummary), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeParallelMultiHashMap(_roomConnections, NativeMemoryOwner, nameof(_roomConnections), NativeMemoryLifetime);
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        private void ReleaseModuleStressBuffer()
        {
            ReleaseModuleStressBuffer(true);
        }

        private void ReleaseModuleStressBuffer(bool clearShaderParams)
        {
            if (_moduleStressBuffer == null)
                return;

            _moduleStressBuffer.Release();
            _moduleStressBuffer = null;
            _lastUploadedModuleStressCount = -1;
            if (clearShaderParams)
                Shader.SetGlobalVector(HabitatModuleStressParamsId, Vector4.zero);
        }

        private static void DisposeNativeParallelMultiHashMap<TKey, TValue>(
            ref NativeParallelMultiHashMap<TKey, TValue> map,
            string label)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            if (!map.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeParallelMultiHashMap(NativeMemoryOwner, label);
            map.Dispose();
            map = default;
        }

        private static int NextPowerOfTwo(int value)
        {
            if (value <= 1)
                return 1;

            int power = 1;
            while (power < value && power > 0)
                power <<= 1;

            return power > 0 ? power : int.MaxValue;
        }

        [BurstCompile(FloatPrecision.Low, FloatMode.Fast, CompileSynchronously = false)]
        private struct DeconstructionDfsValidationJob : IJob
        {
            [ReadOnly] public NativeArray<int> EdgeOffsets;
            [ReadOnly] public NativeArray<int> EdgeDestinations;
            public NativeList<long> Stack;
            public NativeParallelHashSet<long> Visited;
            public NativeArray<int> Result;
            public int NodeCount;
            public int RemovedNodeIndex;
            public int EdgeCount;

            public void Execute()
            {
                Result[0] = 0;
                Result[1] = 0;
                Result[2] = math.max(0, NodeCount - 1);
                Stack.Clear();
                Visited.Clear();

                if (NodeCount <= 2)
                {
                    Result[0] = 1;
                    Result[1] = math.max(0, NodeCount - 1);
                    return;
                }

                int startNode = -1;
                for (int nodeIndex = 0; nodeIndex < NodeCount; nodeIndex++)
                {
                    if (nodeIndex == RemovedNodeIndex)
                        continue;

                    startNode = nodeIndex;
                    break;
                }

                if (startNode < 0)
                {
                    Result[0] = 1;
                    return;
                }

                long startNodeKey = startNode;
                Stack.Add(startNodeKey);
                Visited.Add(startNodeKey);

                while (Stack.Length > 0)
                {
                    int lastIndex = Stack.Length - 1;
                    long currentKey = Stack[lastIndex];
                    Stack.RemoveAtSwapBack(lastIndex);

                    int currentNode = (int)currentKey;
                    if (currentNode < 0 || currentNode >= NodeCount || currentNode + 1 >= EdgeOffsets.Length)
                        continue;

                    int edgeLimit = math.min(EdgeCount, EdgeDestinations.Length);
                    int edgeStart = math.clamp(EdgeOffsets[currentNode], 0, edgeLimit);
                    int edgeEnd = math.clamp(EdgeOffsets[currentNode + 1], edgeStart, edgeLimit);
                    for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                    {
                        int neighborNode = EdgeDestinations[edgeIndex];
                        if (neighborNode < 0 || neighborNode >= NodeCount || neighborNode == RemovedNodeIndex)
                            continue;

                        long neighborKey = neighborNode;
                        if (Visited.Contains(neighborKey))
                            continue;

                        Visited.Add(neighborKey);
                        Stack.Add(neighborKey);
                    }
                }

                int visitedCount = Visited.Count();
                int expectedCount = math.max(0, NodeCount - 1);
                Result[0] = visitedCount == expectedCount ? 1 : 0;
                Result[1] = visitedCount;
                Result[2] = expectedCount;
            }
        }

        private struct ModuleRecord
        {
            public GameObject ModuleObject;
            public ModuleMarker Marker;
            public BaseModule BaseModule;
            public float3 Position;
            public uint NodeId;
            public bool IsAnchorNode;
            public bool IsEmergencyAirlock;
        }

        private struct EdgeRecord
        {
            public int SourceIndex;
            public int DestinationIndex;
            public float3 StartSocketPosition;
            public float3 EndSocketPosition;
            public float3 StartForward;
            public float3 EndForward;
            public float Resistance;
            public int ForwardCsrIndex;
            public int ReverseCsrIndex;
            public PipeRenderFlags Flags;
            public bool Severed;
            public bool DirectedOnly;
        }

        private struct TemporaryBypassRecord
        {
            public uint SourceNodeId;
            public uint DestinationNodeId;
            public int SourceModuleHashId;
            public int DestinationModuleHashId;
            public float3 SourcePosition;
            public float3 DestinationPosition;
        }

        private readonly struct SocketKey : IEquatable<SocketKey>
        {
            private readonly int _x;
            private readonly int _y;
            private readonly int _z;
            private readonly int _axis;

            private SocketKey(int x, int y, int z, int axis)
            {
                _x = x;
                _y = y;
                _z = z;
                _axis = axis;
            }

            public static SocketKey Create(Vector3 position, int axis, int quantizationScale)
            {
                float scale = quantizationScale > 0 ? quantizationScale : 1f;
                float3 scaledPosition = (float3)position * scale;
                int3 quantizedPosition = (int3)math.round(scaledPosition);
                return new SocketKey(quantizedPosition.x, quantizedPosition.y, quantizedPosition.z, axis);
            }

            public bool Equals(SocketKey other)
            {
                return _x == other._x &&
                       _y == other._y &&
                       _z == other._z &&
                       _axis == other._axis;
            }

            public override bool Equals(object obj)
            {
                return obj is SocketKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = _x;
                    hash = (hash * 397) ^ _y;
                    hash = (hash * 397) ^ _z;
                    hash = (hash * 397) ^ _axis;
                    return hash;
                }
            }
        }

        private readonly struct SocketMatchEntry
        {
            public readonly int ModuleIndex;
            public readonly string CompatibleType;
            public readonly ModuleSocketDirection Direction;
            public readonly float3 Position;
            public readonly float3 Forward;

            public SocketMatchEntry(int moduleIndex, string compatibleType, ModuleSocketDirection direction, float3 position, float3 forward)
            {
                ModuleIndex = moduleIndex;
                CompatibleType = compatibleType;
                Direction = direction;
                Position = position;
                Forward = forward;
            }
        }
    }
}
