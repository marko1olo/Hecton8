using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Atmosphere;
using Hecton.Localization;
using Hecton8.Audio;
using Hecton8.Building;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.Power;
using Hecton8.World;
using CoreCombatDamageSignal = Hecton8.Core.Contracts.Signals.CombatDamageSignal;
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

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    internal struct HabitatSiegeTargetSnapshot
    {
        [FieldOffset(0)]
        public float3 ModuleCenter;
        [FieldOffset(12)]
        public float3 WeakPoint;
        [FieldOffset(24)]
        public float Integrity01;
        [FieldOffset(28)]
        public float Vulnerability01;
        [FieldOffset(32)]
        public uint NodeId;
        [FieldOffset(36)]
        public byte Flags;
        [FieldOffset(37)]
        public byte Reserved0;
        [FieldOffset(38)]
        public byte Reserved1;
        [FieldOffset(39)]
        public byte Reserved2;
        [FieldOffset(40)]
        private byte _pad0;
        [FieldOffset(41)]
        private byte _pad1;
        [FieldOffset(42)]
        private byte _pad2;
        [FieldOffset(43)]
        private byte _pad3;
        [FieldOffset(44)]
        private byte _pad4;
        [FieldOffset(45)]
        private byte _pad5;
        [FieldOffset(46)]
        private byte _pad6;
        [FieldOffset(47)]
        private byte _pad7;
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

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
    internal struct HabitatFloodBlackBoxEntry
    {
        [System.Runtime.InteropServices.FieldOffset(0)]
        public int Frame;
        [System.Runtime.InteropServices.FieldOffset(4)]
        public float BaseTotalStress;
        [System.Runtime.InteropServices.FieldOffset(8)]
        public float MaxWaterLevel01;
        [System.Runtime.InteropServices.FieldOffset(12)]
        public float TotalWaterVolumeM3;
        [System.Runtime.InteropServices.FieldOffset(16)]
        public float PeakModuleStress;
        [System.Runtime.InteropServices.FieldOffset(20)]
        public uint Flags;
        [System.Runtime.InteropServices.FieldOffset(24)]
        public uint StateHash;
        [System.Runtime.InteropServices.FieldOffset(28)]
        public uint DeformationSequence;
        [System.Runtime.InteropServices.FieldOffset(32)]
        public ushort NodeCount;
        [System.Runtime.InteropServices.FieldOffset(34)]
        public ushort EdgeCount;
        [System.Runtime.InteropServices.FieldOffset(36)]
        public ushort FloodedRoomCount;
        [System.Runtime.InteropServices.FieldOffset(38)]
        public ushort Reserved0;
        [System.Runtime.InteropServices.FieldOffset(40)]
        private byte _pad0;
        [System.Runtime.InteropServices.FieldOffset(41)]
        private byte _pad1;
        [System.Runtime.InteropServices.FieldOffset(42)]
        private byte _pad2;
        [System.Runtime.InteropServices.FieldOffset(43)]
        private byte _pad3;
        [System.Runtime.InteropServices.FieldOffset(44)]
        private byte _pad4;
        [System.Runtime.InteropServices.FieldOffset(45)]
        private byte _pad5;
        [System.Runtime.InteropServices.FieldOffset(46)]
        private byte _pad6;
        [System.Runtime.InteropServices.FieldOffset(47)]
        private byte _pad7;
        [System.Runtime.InteropServices.FieldOffset(48)]
        private byte _pad8;
        [System.Runtime.InteropServices.FieldOffset(49)]
        private byte _pad9;
        [System.Runtime.InteropServices.FieldOffset(50)]
        private byte _pad10;
        [System.Runtime.InteropServices.FieldOffset(51)]
        private byte _pad11;
        [System.Runtime.InteropServices.FieldOffset(52)]
        private byte _pad12;
        [System.Runtime.InteropServices.FieldOffset(53)]
        private byte _pad13;
        [System.Runtime.InteropServices.FieldOffset(54)]
        private byte _pad14;
        [System.Runtime.InteropServices.FieldOffset(55)]
        private byte _pad15;
        [System.Runtime.InteropServices.FieldOffset(56)]
        private byte _pad16;
        [System.Runtime.InteropServices.FieldOffset(57)]
        private byte _pad17;
        [System.Runtime.InteropServices.FieldOffset(58)]
        private byte _pad18;
        [System.Runtime.InteropServices.FieldOffset(59)]
        private byte _pad19;
        [System.Runtime.InteropServices.FieldOffset(60)]
        private byte _pad20;
        [System.Runtime.InteropServices.FieldOffset(61)]
        private byte _pad21;
        [System.Runtime.InteropServices.FieldOffset(62)]
        private byte _pad22;
        [System.Runtime.InteropServices.FieldOffset(63)]
        private byte _pad23;
    }

    /// <summary>
    /// Rebuilds the placed habitat into a CSR adjacency graph for downstream power and atmosphere solvers.
    /// Owns only base-module topology. Point-to-point crate pipes remain under LogisticsPipeNode.
    /// </summary>
    public sealed class HabitatGraphManager : IDisposable
    {
        private static int s_x001HabitatGraphManagerSignalPushDropCount;
        private const float DefaultSocketQuantization = 0.05f;
        private const float OppositeDirectionDotThreshold = -0.85f;
        private const float EdgeResistancePerMeter = 0.05f;
        private const float MinimumEdgeResistance = 0.1f;
        private const float SurfacePressureKPa = HectonSurvivalContract.KPaPerAtmosphere;
        private const float SeawaterDensityKilogramsPerCubicMeter = HectonPhysicsContract.WaterDensityKgPerCubicMeterConst;
        private const float GravityAccelerationMetersPerSecondSquared = HectonPhysicsContract.GravityMetersPerSecondSquaredConst;
        private static readonly float GravityAccelerationMetersPerSecondSquaredInv = HectonPhysicsContract.OneOverGravityMetersPerSecondSquared;
        private const float HydrostaticPressureKPaPerMeter = HectonPhysicsContract.HydrostaticPressureKPaPerMeter;
        private const float DefaultHydroShearThresholdKilograms = 18000f;
        private const int PressureRootLutSize = 32;
        private const float PressureRootLutMaxKPa = 12000f;
        private const float PressureRootLutStepKPa = PressureRootLutMaxKPa / PressureRootLutSize;
        private const float PressureRootLutStepKPaInv = 1f / PressureRootLutStepKPa;
        private const float PressureRootExcessLinearScale = 0.5f;
        private const int GraphFloodMinTraversalNodesPerTick = 64;
        private const int GraphFloodMaxTraversalNodesPerTick = 512;
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
        private const float AnalyticalShaderCenterEpsilonMeters = 0.05f;
        private const float AnalyticalShaderCenterEpsilonSq = AnalyticalShaderCenterEpsilonMeters * AnalyticalShaderCenterEpsilonMeters;
        private const float AnalyticalShaderRadiusEpsilonMeters = 0.05f;
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
        private const float ModuleStressQualityEpsilon = 0.0025f;
        private const float ModuleStressNearestSignalPaddingMeters = 3f;
        private const float ModuleStressNearestSignalFallbackRadiusMeters = 8f;
        private const float ModuleStressNearestSignalMaxRadiusMeters = 36f;
        private const int ModuleStressShaderCapacity = 64;
        private const float AnalyticalLowDetailFeedbackThreshold01 = 0.42f;
        private const float AnalyticalLowDetailFeedbackCooldownSeconds = 3.5f;
        private const float AnalyticalEmergencyRemainingIntegrityThreshold01 = 0.2f;
        private const float HabitatClockMaxSeconds = 16777215f;
        private const int AnalyticalBreachMinimumThreshold = 4;
        private const int AnalyticalBreachMaximumThreshold = 96;
        private const float HabitatVibrationDecayPerSecond = 0.75f;
        private const float HabitatVibrationImpulseScale = 0.0015f;
        private const float HabitatVibrationPublishEpsilon = 0.002f;
        private const float PressureBucklingCompressionDeltaThreshold = 0.15f;
        private const float RuptureCascadeNeighborStressMultiplier = 0.5f;
        private const float StructuralGroanStressThreshold01 = 0.8f;
        private const float StructuralGroanPitchRange = 0.32f;
        private const float CondensationInteriorTemperatureCelsius = 30f;
        private const float CondensationExternalTemperatureCelsius = 5f;
        private const float SupportCaptureRadiusMeters = 3f;
        private const float SupportCaptureRadiusSq = SupportCaptureRadiusMeters * SupportCaptureRadiusMeters;
        private const int InitialNodeCapacity = 64;
        private const int InitialEdgeCapacity = 128;
        private const int InitialTemporaryBypassCapacity = 16;
        internal const int MaxSiegeTargetCount = 64;
        private const int FloodBlackBoxCapacity = 300;
        private const BufferID HabitatFloodBlackBoxBufferId = (BufferID)72120;
        private const BufferID HabitatFloodPropagationSummaryBufferId = (BufferID)72121;
        private const BufferID HabitatSiegeTargetsBufferId = (BufferID)72122;
        private const BufferID HabitatModuleStressScalarsBufferId = (BufferID)72123;
        private const BufferID HabitatPreviousModuleStressScalarsBufferId = (BufferID)72124;
        private const BufferID HabitatModuleImpactStressSpikesBufferId = (BufferID)72125;
        private const BufferID HabitatModuleCompromisedFlagsBufferId = (BufferID)72126;
        private const BufferID HabitatRoomWaterLevelsBufferId = (BufferID)72127;
        private const BufferID HabitatRoomVolumesBufferId = (BufferID)72128;
        private const BufferID HabitatRoomFloodDeltaLevelsBufferId = (BufferID)72129;
        private const BufferID HabitatRoomFlagsBufferId = (BufferID)72130;
        private const BufferID HabitatGraphNodesBufferId = (BufferID)72131;
        private const BufferID HabitatGraphEdgeOffsetsBufferId = (BufferID)72132;
        private const BufferID HabitatGraphEdgeDestinationsBufferId = (BufferID)72133;
        private const BufferID HabitatGraphEdgeResistanceBufferId = (BufferID)72134;
        private const BufferID HabitatGraphEdgeWriteCursorBufferId = (BufferID)72135;
        private const BufferID HabitatGraphAnchorReachabilityBufferId = (BufferID)72136;
        private const BufferID HabitatGraphTraversalVisitedBufferId = (BufferID)72137;
        private const BufferID HabitatGraphAnchorTraversalQueueBufferId = (BufferID)72138;
        private const BufferID HabitatGraphEdgeFlagsBufferId = (BufferID)72139;
        private const ulong HabitatGraphMutationGuardMask = 0x0000000000000FF8UL;
        private const ulong HabitatFloodRoomMutationGuardMask = 0x0000000080000007UL;
        private const ulong HabitatModuleStressMutationGuardMask = 0x0000000078000000UL;
        private const ulong HabitatFloodPropagationMutationGuardMask = 0x0000000082000FFFUL;
        private const uint FloodBlackBoxMagic = 0x48464C44u; // "HFLD"
        private const uint FloodBlackBoxVersion = 3u;
        private const uint FloodBlackBoxNonFiniteFlag = 1u << 0;
        private const uint FloodBlackBoxOverflowClampedFlag = 1u << 1;
        private const uint FloodBlackBoxTraversalOverflowFlag = 1u << 2;
        private const uint FloodBlackBoxTopologyInvalidFlag = 1u << 3;
        private const uint FloodBlackBoxModuleStressInvalidFlag = 1u << 4;
        private const string FloodBlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_1306_Construction_HabitatIntegrity.bin";
        private const string ModuleStressBlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_1306_Construction_ModuleStress.bin";
        private const float SiegeVulnerableIntegrityThreshold01 = 0.72f;
        private static readonly int CarbonFilterItemHashId = LocHash.Compute("Data_CarbonFilter");
        private static readonly uint RuptureCascadeEventHash = unchecked((uint)LocHash.Compute("HabitatGraphManager.RuptureCascade"));
        private static readonly int HabitatStressCenterRadiusId = Shader.PropertyToID("_HectonHabitatStressCenterRadius");
        private static readonly int HabitatStressParamsId = Shader.PropertyToID("_HectonHabitatStressParams");
        private static readonly int HabitatModuleStressBufferId = Shader.PropertyToID("_HectonHabitatModuleStressBuffer");
        private static readonly int HabitatModuleStressParamsId = Shader.PropertyToID("_HectonHabitatModuleStressParams");
        private static readonly int HabitatVibrationId = Shader.PropertyToID("_HectonHabitatVibration01");
        private static readonly int BaseEmergencyStateId = Shader.PropertyToID("_BaseEmergencyState");
        // COLD ALLOC: float[33] - pressure ingress sqrt lookup table - owner: HabitatGraphManager
        private static readonly float[] s_pressureRootLut =
        {
            0f, 19.364917f, 27.386128f, 33.54102f, 38.729833f, 43.30127f, 47.434165f, 51.234754f,
            54.772256f, 58.09475f, 61.237244f, 64.226163f, 67.082039f, 69.8212f, 72.456884f, 75f,
            77.459667f, 79.843597f, 82.158384f, 84.409715f, 86.60254f, 88.741197f, 90.829511f, 92.870878f,
            94.86833f, 96.824584f, 98.742088f, 100.623059f, 102.469508f, 104.283268f, 106.066017f, 107.819293f,
            109.544512f
        };
        private static readonly Color PipeSplineColor = new Color(0.30f, 0.82f, 0.95f, 0.88f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticSiegeTargets()
        {
            s_latestSiegeTargetOwner = null;
            s_latestSiegeTargetCount = 0;
        }

        private readonly List<ModuleRecord> _moduleBuffer;
        private readonly List<EdgeRecord> _edgeBuffer;
        private readonly List<TemporaryBypassRecord> _temporaryBypassBuffer;
        private readonly List<long> _submittedLinkIds;
        private readonly List<long> _emittedRuptureEdgeVfxKeys;
        private readonly HashSet<long> _emittedRuptureEdgeVfxLookup;
        private readonly List<uint> _ruptureCascadeAppliedNodeIds;
        private readonly Dictionary<uint, int> _moduleIndexByNodeId;
        private readonly Dictionary<SocketKey, SocketMatchEntry> _socketLookup;
        private readonly int _moduleIndexCapacity;
        private readonly int _socketLookupCapacity;

        private VaultGenerationHandle<LogisticsNetworkGraph.LogisticsNode> _nodesHandle;
        private VaultGenerationHandle<int> _edgeOffsetsHandle;
        private VaultGenerationHandle<int> _edgeDestinationsHandle;
        private VaultGenerationHandle<float> _edgeResistanceHandle;
        private VaultGenerationHandle<int> _edgeWriteCursorHandle;
        private VaultGenerationHandle<byte> _anchorReachabilityHandle;
        private VaultGenerationHandle<byte> _traversalVisitedHandle;
        private VaultGenerationHandle<int> _anchorTraversalQueueHandle;
        private VaultGenerationHandle<byte> _edgeFlagsHandle;
        private VaultGenerationHandle<HabitatFloodBlackBoxEntry> _floodBlackBoxHandle;
        private VaultGenerationHandle<HabitatFloodPropagationSummary> _floodPropagationSummaryHandle;
        private VaultGenerationHandle<HabitatSiegeTargetSnapshot> _siegeTargetsHandle;
        private VaultGenerationHandle<float> _moduleStressScalarsHandle;
        private VaultGenerationHandle<float> _previousModuleStressScalarsHandle;
        private VaultGenerationHandle<float> _moduleImpactStressSpikesHandle;
        private VaultGenerationHandle<byte> _moduleCompromisedFlagsHandle;
        private VaultGenerationHandle<float> _roomWaterLevelsHandle;
        private VaultGenerationHandle<float> _roomVolumesHandle;
        private VaultGenerationHandle<float> _roomFloodDeltaLevelsHandle;
        private VaultGenerationHandle<byte> _roomFlagsHandle;
        private JobHandle _floodPropagationHandle;
        private bool _floodPropagationPending;
        private bool _floodPropagationSummaryWriteLockHeld;
        private IDataVault _floodPropagationSummaryWriteLockVault;
        private bool _floodPropagationRoomWriteLockHeld;
        private IDataVault _floodPropagationRoomWriteLockVault;
        private bool _floodPropagationGraphWriteLockHeld;
        private IDataVault _floodPropagationGraphWriteLockVault;
        private bool _floodPropagationGuardHeld;
        private IDataVault _floodPropagationGuardVault;
        private bool _deconstructionGraphWriteLockHeld;
        private IDataVault _deconstructionGraphWriteLockVault;
        private int _pendingFloodPropagationModuleCount;
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
        private float3 _lastPublishedAnalyticalCenter;
        private float _lastPublishedAnalyticalRadius = -1f;
        private float _habitatClockSeconds;
        private float _nextAnalyticalLowDetailFeedbackTime;
        private uint _lastPublishedAnalyticalBreachNodeId;
        private uint _analyticalBreachNodeId;
        private float _habitatVibration01;
        private float _lastPublishedHabitatVibration01 = -1f;
        private int _lastPublishedBaseEmergencyState = int.MinValue;
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
        private float _lastUploadedModuleStressLowBlend;
        private float _lastUploadedModuleStressQualityWeight = -1f;
        private int _lastUploadedModuleStressCount = -1;
        private int _lastProcessedModuleStressSignalFrame = -1;
        private GraphicsBuffer _moduleStressBufferA;
        private GraphicsBuffer _moduleStressBufferB;
        private GraphicsBuffer _activeModuleStressBuffer;
        private int _moduleStressBufferWriteIndex;
        private bool _pendingHabitatVibrationShaderDirty;
        private float _pendingHabitatVibrationShader01;
        private bool _pendingBaseEmergencyShaderDirty;
        private int _pendingBaseEmergencyShaderState;
        private bool _pendingAnalyticalStressShaderDirty;
        private float3 _pendingAnalyticalStressCenter;
        private float _pendingAnalyticalStressRadius;
        private float _pendingAnalyticalStress01;
        private float _pendingAnalyticalStressDisplacementMaxMeters;
        private uint _pendingAnalyticalStressBreachNodeId;
        private bool _pendingModuleStressShaderDirty;
        private int _pendingModuleStressCount;
        private float _pendingModuleStressPeak01;
        private float _pendingModuleStressQualityWeight;
        private IAtmosphereReadModel _atmosphereReadModel;
        private IAmbientCurrentReadModel _ambientCurrentReadModel;
        private IAudioService _audioService;
        private IFluidDecalPresentationSink _fluidDecals;
        private IDataVault _dataVault;

        private ref struct HabitatGraphWriteViews
        {
            internal NativeArray<LogisticsNetworkGraph.LogisticsNode> Nodes;
            internal NativeArray<int> EdgeOffsets;
            internal NativeArray<int> EdgeDestinations;
            internal NativeArray<float> EdgeResistance;
            internal NativeArray<int> EdgeWriteCursor;
            internal NativeArray<byte> AnchorReachability;
            internal NativeArray<byte> TraversalVisited;
            internal NativeArray<int> AnchorTraversalQueue;
            internal NativeArray<byte> EdgeFlags;
        }

        private ref struct HabitatFloodGraphJobViews
        {
            internal NativeArray<int> EdgeOffsets;
            internal NativeArray<int> EdgeDestinations;
            internal NativeArray<float> EdgeResistance;
            internal NativeArray<byte> EdgeFlags;
        }

        internal HabitatGraphManager(int initialModuleCapacity, IDataVault dataVault)
        {
            _dataVault = dataVault;
            int safeModuleCapacity = math.max(1, initialModuleCapacity);
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
            _moduleIndexCapacity = safeModuleCapacity;
            _socketLookupCapacity = InitialEdgeCapacity;

            _graph = new LogisticsNetworkGraph(safeModuleCapacity, InitialEdgeCapacity * 2, 0);
            AllocateNativeBuffers(safeModuleCapacity, InitialEdgeCapacity * 2);
        }

        internal int NodeCount => _nodeCount;
        internal int EdgeCount => _edgeCount;
        internal NativeArray<LogisticsNetworkGraph.LogisticsNode>.ReadOnly Nodes => TryReadHabitatVaultBuffer(HabitatGraphNodesBufferId, 1, in _nodesHandle, out NativeArray<LogisticsNetworkGraph.LogisticsNode>.ReadOnly nodes) ? nodes : default;
        internal NativeArray<int>.ReadOnly EdgeOffsets => TryReadHabitatVaultBuffer(HabitatGraphEdgeOffsetsBufferId, 1, in _edgeOffsetsHandle, out NativeArray<int>.ReadOnly edgeOffsets) ? edgeOffsets : default;
        internal NativeArray<int>.ReadOnly EdgeDestinations => TryReadHabitatVaultBuffer(HabitatGraphEdgeDestinationsBufferId, 1, in _edgeDestinationsHandle, out NativeArray<int>.ReadOnly edgeDestinations) ? edgeDestinations : default;
        internal NativeArray<float>.ReadOnly EdgeResistance => TryReadHabitatVaultBuffer(HabitatGraphEdgeResistanceBufferId, 1, in _edgeResistanceHandle, out NativeArray<float>.ReadOnly edgeResistance) ? edgeResistance : default;
        internal NativeArray<float>.ReadOnly RoomWaterLevels => TryReadHabitatVaultBuffer(HabitatRoomWaterLevelsBufferId, 1, in _roomWaterLevelsHandle, out NativeArray<float>.ReadOnly roomWaterLevels) ? roomWaterLevels : default;
        internal NativeArray<float>.ReadOnly RoomVolumes => TryReadHabitatVaultBuffer(HabitatRoomVolumesBufferId, 1, in _roomVolumesHandle, out NativeArray<float>.ReadOnly roomVolumes) ? roomVolumes : default;
        internal NativeArray<byte>.ReadOnly RoomFlags => TryReadHabitatVaultBuffer(HabitatRoomFlagsBufferId, 1, in _roomFlagsHandle, out NativeArray<byte>.ReadOnly roomFlags) ? roomFlags : default;
        internal NativeArray<byte>.ReadOnly EdgeFlags => TryReadHabitatVaultBuffer(HabitatGraphEdgeFlagsBufferId, 1, in _edgeFlagsHandle, out NativeArray<byte>.ReadOnly edgeFlags) ? edgeFlags : default;
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

        internal void PublishRoomSubmergedFractionsToGas(IGasDynamicsSolver gasDynamics)
        {
            if (gasDynamics == null ||
                _moduleBuffer == null ||
                _nodeCount <= 0 ||
                !TryReadHabitatVaultBuffer(
                    HabitatRoomWaterLevelsBufferId,
                    1,
                    in _roomWaterLevelsHandle,
                    out NativeArray<float>.ReadOnly roomWaterLevels))
            {
                return;
            }

            int roomLimit = math.min(_nodeCount, _moduleBuffer.Count);
            roomLimit = math.min(roomLimit, roomWaterLevels.Length);
            roomLimit = math.min(roomLimit, math.max(0, gasDynamics.RoomCount));
            for (int roomId = 0; roomId < roomLimit; roomId++)
            {
                BaseModule baseModule = _moduleBuffer[roomId].BaseModule;
                if (baseModule == null || !baseModule.isActiveAndEnabled)
                    continue;

                float fill01 = ResolveAuthoritativeRoomWaterLevel01(roomId, baseModule, roomWaterLevels);
                gasDynamics.TrySetRoomSubmergedFraction(roomId, fill01);
            }
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

        internal static bool TryGetLatestSiegeTargets(out NativeArray<HabitatSiegeTargetSnapshot>.ReadOnly targets, out int count)
        {
            targets = default;
            count = 0;
            int publishedCount = s_latestSiegeTargetCount;
            HabitatGraphManager owner = s_latestSiegeTargetOwner;
            if (owner == null ||
                publishedCount <= 0 ||
                !owner.TryReadHabitatVaultBuffer(
                    HabitatSiegeTargetsBufferId,
                    publishedCount,
                    in owner._siegeTargetsHandle,
                    out targets))
            {
                return false;
            }

            count = Mathf.Min(publishedCount, targets.Length);
            if (count <= 0)
                return false;

            return true;
        }

        public void Dispose()
        {
            CompleteFloodPropagationJobForTeardown();
            PublishAnalyticalStressShader(float3.zero, 0f, 0f, 0f, true);
            PublishBaseEmergencyState(0, true);
            _habitatVibration01 = 0f;
            PublishHabitatVibration(true);
            FlushVisualSync();
            ClearVisualLinks();
            DisposeNativeBuffers();
            _atmosphereReadModel = null;
            _ambientCurrentReadModel = null;
            _audioService = null;
            _fluidDecals = null;
            _dataVault = null;
            _graph.Dispose();
        }

        internal void SetDataVault(IDataVault dataVault)
        {
            _dataVault = dataVault;
        }

        internal void SetRuntimeServices(
            IAtmosphereReadModel atmosphereReadModel,
            IAmbientCurrentReadModel ambientCurrentReadModel,
            IAudioService audioService,
            IFluidDecalPresentationSink fluidDecals)
        {
            _atmosphereReadModel = atmosphereReadModel;
            _ambientCurrentReadModel = ambientCurrentReadModel;
            _audioService = audioService;
            _fluidDecals = fluidDecals;
        }

        internal void SetAtmosphereReadModel(IAtmosphereReadModel atmosphereReadModel)
        {
            _atmosphereReadModel = atmosphereReadModel;
        }

        internal void SetAmbientCurrentReadModel(IAmbientCurrentReadModel ambientCurrentReadModel)
        {
            _ambientCurrentReadModel = ambientCurrentReadModel;
        }

        internal void SetAudioService(IAudioService audioService)
        {
            _audioService = audioService;
        }

        internal void SetFluidDecalPresentation(IFluidDecalPresentationSink fluidDecals)
        {
            _fluidDecals = fluidDecals;
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
            int directedEdgeCapacity = math.max(1, _edgeBuffer.Count * 2);
            if (!TryAcquireGraphWriteBuffers(
                    _nodeCount,
                    directedEdgeCapacity,
                    out HabitatGraphWriteViews graphViews,
                    out IDataVault graphVault))
            {
                ClearSiegeTargetSnapshot();
                ClearFloodRoomStateSnapshot();
                _graph.BeginBuild(LogisticsNetworkType.OxygenPressure, 1, 1, 0);
                BaseDegradationSystem.EndRuptureSync();
                return;
            }

            try
            {
                BuildNodeRecords(ref graphViews);
                PruneRuptureCascadeState(ref graphViews);
                BuildEdgeRecords(ref graphViews, directedEdgeCapacity);
                EvaluateAnchorReachability(ref graphViews);
                PublishAnchorState(ref graphViews);
                PublishComponentPowerState(ref graphViews);
                PublishEmergencyLockdownState(ref graphViews);
                SyncFloodRoomStateSnapshot();
                PublishDegradationState(ref graphViews);
                PublishSiegeTargetSnapshot(ref graphViews);
                PublishGraphKernel(ref graphViews);
            }
            finally
            {
                ReleaseGraphWriteLocks(graphVault);
            }

            PublishVisualLinks();
            BaseDegradationSystem.EndRuptureSync();
        }

        internal void ApplyHydrodynamicStress(float deltaTime)
        {
            if (!math.isfinite(deltaTime))
            {
                RecordNonFinitePressureIngress();
                return;
            }

            if (deltaTime <= 0f)
                return;

            AdvanceHabitatClock(deltaTime);
            if (_moduleBuffer.Count <= 0)
                return;

            float globalQualityWeight = ResolveHabitatGraphQualityWeight();
            _runtimeSeaLevelY = ResolveRuntimeSeaLevelY();
            UpdateHabitatVibration(deltaTime);
            ApplyGraphFluidIncursion(deltaTime, globalQualityWeight);
            ApplyWaterPumpDrainage(deltaTime);
            ApplyOxygenScrubberFilterConsumption(deltaTime);
            ApplyThermalCondensationState();
            QueueFloodMassLoads(deltaTime);
            bool runtimeTopologyChanged = EvaluateBulkheadFloodStress(deltaTime);
            runtimeTopologyChanged |= EvaluatePressureBucklingStress(deltaTime);
            runtimeTopologyChanged |= EvaluateDetachedDebrisState();
            if (runtimeTopologyChanged)
                PublishRuntimeRuptureTopologyState();

            EvaluateAnalyticalIntegrityStress(globalQualityWeight);
            UpdateHabitatModuleStressMatrix(deltaTime, globalQualityWeight);
            SyncFloodRoomStateSnapshot();
            WriteFloodBlackBoxSample(0u);
            PublishSiegeTargetSnapshot();
        }

        internal void FlushVisualSync()
        {
            FlushHabitatVibrationShader();
            FlushBaseEmergencyShader();
            FlushAnalyticalStressShader();
            FlushModuleStressShader();
        }

        internal float AnalyticalStress => _analyticalStress;
        internal float AnalyticalIntegrity => _analyticalIntegrity;

        private void AdvanceHabitatClock(float deltaTime)
        {
            _habitatClockSeconds = math.min(HabitatClockMaxSeconds, _habitatClockSeconds + deltaTime);
        }

        private float ResolveHabitatClockSeconds()
        {
            return _habitatClockSeconds;
        }

        internal void RegisterSeismicVibration(Vector3 epicenter, float radiusMeters, float impulseMagnitude)
        {
            if (!float.IsFinite(impulseMagnitude) ||
                !float.IsFinite(epicenter.x) ||
                !float.IsFinite(epicenter.y) ||
                !float.IsFinite(epicenter.z))
            {
                RecordNonFinitePressureIngress();
                return;
            }

            if (impulseMagnitude <= 0f || _moduleBuffer.Count <= 0)
                return;

            if (!float.IsFinite(radiusMeters))
            {
                RecordNonFinitePressureIngress();
                radiusMeters = 1f;
            }

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

        private void PublishHabitatVibration(bool force = false)
        {
            float publishValue = _habitatVibration01 <= HabitatVibrationPublishEpsilon
                ? 0f
                : _habitatVibration01;
            if (!force && publishValue > 0f)
            {
                if (math.abs(publishValue - _lastPublishedHabitatVibration01) <= HabitatVibrationPublishEpsilon)
                    return;
            }
            else if (!force && _lastPublishedHabitatVibration01 == 0f)
            {
                return;
            }

            _lastPublishedHabitatVibration01 = publishValue;
            _pendingHabitatVibrationShader01 = publishValue;
            _pendingHabitatVibrationShaderDirty = true;
        }

        private void FlushHabitatVibrationShader()
        {
            if (!_pendingHabitatVibrationShaderDirty)
                return;

            _pendingHabitatVibrationShaderDirty = false;
            Shader.SetGlobalFloat(HabitatVibrationId, _pendingHabitatVibrationShader01);
        }

        private void EvaluateAnalyticalIntegrityStress(float globalQualityWeight)
        {
            int moduleCount = math.min(_nodeCount, _moduleBuffer.Count);
            if (moduleCount <= 0)
            {
                ResetAnalyticalIntegrityStress(globalQualityWeight);
                return;
            }

            EvaluateContinuousAnalyticalIntegrityStress(moduleCount, globalQualityWeight);
        }

        private void EvaluateContinuousAnalyticalIntegrityStress(int moduleCount, float globalQualityWeight)
        {
            float detailWeight = ResolveAnalyticalDetailWeight(globalQualityWeight);
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
                float depthMeters = ResolveAnalyticalModuleDepthMeters(module, baseModule);
                float depthScale = ResolveAnalyticalDepthScale(depthMeters);
                bool useDetailedStress = detailWeight > ModuleStressQualityEpsilon;
                float currentScale = useDetailedStress
                    ? ResolveAnalyticalLocalCurrentScale(module.Position, depthMeters) * detailWeight
                    : 0f;
                float moduleStress = moduleIntegrity * (depthScale + currentScale);
                if (useDetailedStress && IsAnalyticalGrounded(module))
                    moduleStress = math.lerp(moduleStress, moduleStress * AnalyticalGroundedStressScale, detailWeight);

                stressSum += moduleStress;
                reinforcementSum += useDetailedStress
                    ? ResolveAnalyticalReinforcementValue(nodeIndex, module, baseModule, moduleIntegrity) * detailWeight
                    : 0f;
                integritySum += moduleIntegrity;
                centerSum += module.Position;
                activeModuleCount++;
            }

            if (activeModuleCount <= 0)
            {
                ResetAnalyticalIntegrityStress(globalQualityWeight);
                return;
            }

            float netStress = math.max(0f, stressSum - reinforcementSum);
            CommitAnalyticalStressResult(moduleCount, activeModuleCount, centerSum, netStress, integritySum, globalQualityWeight);
        }

        private void ResetAnalyticalIntegrityStress(float globalQualityWeight)
        {
            _analyticalStress = 0f;
            _analyticalIntegrity = 0f;
            _analyticalBreachNodeId = 0u;
            PublishBaseEmergencyState(0);
            PublishAnalyticalStressShader(float3.zero, 0f, 0f, globalQualityWeight);
        }

        private void CommitAnalyticalStressResult(
            int moduleCount,
            int activeModuleCount,
            float3 centerSum,
            float netStress,
            float integritySum,
            float globalQualityWeight)
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
            PublishBaseEmergencyState(stress01 >= 1f - AnalyticalEmergencyRemainingIntegrityThreshold01 ? 1 : 0);
            PublishAnalyticalStressShader(center, radius, stress01, globalQualityWeight);
            TryPublishLowDetailAnalyticalStressFeedback(center, stress01, globalQualityWeight);
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

        private float ResolveAnalyticalLocalCurrentScale(float3 runtimePosition, float depthMeters)
        {
            Vector3 current = Vector3.zero;
            IAmbientCurrentReadModel ambientCurrentReadModel = GetCachedAmbientCurrentReadModel();
            if (ambientCurrentReadModel != null)
            {
                ambientCurrentReadModel.TrySampleCombinedCurrent(
                    new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z),
                    out current);
            }

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

            if (TryReadHabitatVaultBuffer(
                    HabitatGraphAnchorReachabilityBufferId,
                    math.max(1, nodeIndex + 1),
                    in _anchorReachabilityHandle,
                    out NativeArray<byte>.ReadOnly anchorReachability) &&
                nodeIndex >= 0 &&
                nodeIndex < anchorReachability.Length &&
                anchorReachability[nodeIndex] != 0)
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
            IAtmosphereReadModel atmosphereReadModel = GetCachedAtmosphereReadModel();
            return atmosphereReadModel != null && math.isfinite(atmosphereReadModel.SeaLevelY)
                ? atmosphereReadModel.SeaLevelY
                : 0f;
        }

        private IAtmosphereReadModel GetCachedAtmosphereReadModel()
        {
            return _atmosphereReadModel;
        }

        private IAmbientCurrentReadModel GetCachedAmbientCurrentReadModel()
        {
            return _ambientCurrentReadModel;
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

        private void PublishBaseEmergencyState(int emergencyState, bool force = false)
        {
            int safeState = emergencyState != 0 ? 1 : 0;
            if (!force && _lastPublishedBaseEmergencyState == safeState)
                return;

            _lastPublishedBaseEmergencyState = safeState;
            _pendingBaseEmergencyShaderState = safeState;
            _pendingBaseEmergencyShaderDirty = true;
        }

        private void FlushBaseEmergencyShader()
        {
            if (!_pendingBaseEmergencyShaderDirty)
                return;

            _pendingBaseEmergencyShaderDirty = false;
            Shader.SetGlobalInt(BaseEmergencyStateId, _pendingBaseEmergencyShaderState);
        }

        private void PublishAnalyticalStressShader(float3 center, float radius, float stress01, float globalQualityWeight, bool force = false)
        {
            bool validCenter = math.all(math.isfinite(center));
            if (!validCenter)
                center = float3.zero;

            float safeRadius = math.isfinite(radius)
                ? math.max(0f, radius)
                : 0f;
            float sourceStress01 = validCenter && math.isfinite(stress01)
                ? math.saturate(stress01)
                : 0f;
            float visibleStress01 = sourceStress01 > AnalyticalShaderStressEpsilon
                ? sourceStress01
                : 0f;
            float displacementMaxMeters = visibleStress01 > 0f
                ? ResolveAnalyticalDisplacementMaxMeters(globalQualityWeight)
                : 0f;
            bool stressStable = visibleStress01 > 0f
                ? math.abs(visibleStress01 - _lastPublishedAnalyticalStress01) <= AnalyticalShaderStressEpsilon
                : _lastPublishedAnalyticalStress01 == 0f;
            bool spatialStable = visibleStress01 <= 0f ||
                                 (math.lengthsq(center - _lastPublishedAnalyticalCenter) <= AnalyticalShaderCenterEpsilonSq &&
                                  math.abs(safeRadius - _lastPublishedAnalyticalRadius) <= AnalyticalShaderRadiusEpsilonMeters);
            if (!force &&
                _lastPublishedAnalyticalBreachNodeId == _analyticalBreachNodeId &&
                stressStable &&
                spatialStable &&
                math.abs(displacementMaxMeters - _lastPublishedAnalyticalDisplacementMaxMeters) <= 0.00001f)
            {
                return;
            }

            _lastPublishedAnalyticalStress01 = visibleStress01;
            _lastPublishedAnalyticalDisplacementMaxMeters = displacementMaxMeters;
            _lastPublishedAnalyticalCenter = center;
            _lastPublishedAnalyticalRadius = safeRadius;
            _lastPublishedAnalyticalBreachNodeId = _analyticalBreachNodeId;
            _pendingAnalyticalStressCenter = center;
            _pendingAnalyticalStressRadius = safeRadius;
            _pendingAnalyticalStress01 = visibleStress01;
            _pendingAnalyticalStressDisplacementMaxMeters = displacementMaxMeters;
            _pendingAnalyticalStressBreachNodeId = _analyticalBreachNodeId;
            _pendingAnalyticalStressShaderDirty = true;
        }

        private void FlushAnalyticalStressShader()
        {
            if (!_pendingAnalyticalStressShaderDirty)
                return;

            _pendingAnalyticalStressShaderDirty = false;
            float3 center = _pendingAnalyticalStressCenter;
            Shader.SetGlobalVector(
                HabitatStressCenterRadiusId,
                new Vector4(center.x, center.y, center.z, _pendingAnalyticalStressRadius));
            Shader.SetGlobalVector(
                HabitatStressParamsId,
                new Vector4(
                    _pendingAnalyticalStress01,
                    _pendingAnalyticalStressDisplacementMaxMeters,
                    AnalyticalShaderGridScale,
                    (float)(_pendingAnalyticalStressBreachNodeId & 1023u)));
        }

        private void UpdateHabitatModuleStressMatrix(float deltaTime, float globalQualityWeight)
        {
            int moduleCount = math.min(BaseModule.ActiveModuleCount, ModuleStressShaderCapacity);
            if (moduleCount <= 0)
            {
                ClearModuleStressState();
                return;
            }

            if (!EnsureModuleStressHandles(moduleCount))
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

            if (!TryAcquireModuleStressWriteBuffers(
                    stressCount,
                    out NativeArray<float> moduleStressScalars,
                    out NativeArray<float> previousModuleStressScalars,
                    out NativeArray<float> moduleImpactStressSpikes,
                    out NativeArray<byte> moduleCompromisedFlags,
                    out IDataVault vault))
            {
                ClearModuleStressState();
                return;
            }

            try
            {
                ConsumeModuleStressSignals(stressCount, moduleImpactStressSpikes);
                TryReadHabitatVaultBuffer(
                    HabitatRoomWaterLevelsBufferId,
                    stressCount,
                    in _roomWaterLevelsHandle,
                    out NativeArray<float>.ReadOnly roomWaterLevels);

                float safeDeltaTime = math.max(0.0001f, deltaTime);
                float peakStress01 = 0f;
                float qualityWeight = SanitizeQualityWeight(globalQualityWeight);
                byte moduleStressQualityProfile = ResolveModuleStressQualityProfileByte(qualityWeight);
                float lowTierBlend = ResolveModuleStressLowBlend(qualityWeight);
                bool changed = orderChanged ||
                               stressCount != _lastUploadedModuleStressCount ||
                               math.abs(lowTierBlend - _lastUploadedModuleStressLowBlend) > ModuleStressQualityEpsilon ||
                               math.abs(qualityWeight - _lastUploadedModuleStressQualityWeight) > ModuleStressQualityEpsilon;
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
                        float floodStress01 = ResolveActiveModuleFloodStress01(baseModule, graphNodeIndex, hasGraphRecord, roomWaterLevels);
                        stress01 = ResolveModuleStress01(nodeIndex, baseModule, depthMeters, floodStress01, safeDeltaTime, moduleImpactStressSpikes, out bool invalidStressInput);
                        if (invalidStressInput)
                        {
                            WriteFloodBlackBoxSample(FloodBlackBoxModuleStressInvalidFlag);
                            DumpModuleStressBlackBoxOnce(FloodBlackBoxModuleStressInvalidFlag);
                        }
                    }

                    bool finiteStress = math.isfinite(stress01);
                    stress01 = finiteStress ? math.saturate(stress01) : 0f;
                    if (!finiteStress)
                    {
                        stress01 = 0f;
                        WriteFloodBlackBoxSample(FloodBlackBoxModuleStressInvalidFlag);
                        DumpModuleStressBlackBoxOnce(FloodBlackBoxModuleStressInvalidFlag);
                    }

                    float previousStress01 = previousModuleStressScalars[nodeIndex];
                    float deltaPerSecond = math.abs(stress01 - previousStress01) * math.rcp(safeDeltaTime);
                    if (deltaPerSecond > loudestDeltaPerSecond)
                    {
                        loudestDeltaPerSecond = deltaPerSecond;
                        loudestStress01 = stress01;
                        loudestPosition = modulePosition;
                        loudestDepthMeters = depthMeters;
                    }

                    if (math.abs(stress01 - moduleStressScalars[nodeIndex]) > ModuleStressUploadEpsilon)
                        changed = true;

                    moduleStressScalars[nodeIndex] = stress01;
                    previousModuleStressScalars[nodeIndex] = stress01;
                    peakStress01 = math.max(peakStress01, stress01);

                    if (baseModule != null && stress01 >= ModuleStressCompromisedThreshold01)
                        TryPublishBaseModuleCompromisedSignal(nodeIndex, baseModule, module, hasGraphRecord, modulePosition, stress01, peakStress01, depthMeters, qualityWeight, moduleStressQualityProfile, moduleCompromisedFlags);
                    else if (nodeIndex < moduleCompromisedFlags.Length && stress01 < ModuleStressCompromisedThreshold01 * 0.82f)
                        moduleCompromisedFlags[nodeIndex] = 0;
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
                    UploadModuleStressMatrix(stressCount, peakStress01, qualityWeight);
            }
            finally
            {
                ReleaseModuleStressWriteLocks(vault);
            }
        }

        private void ClearModuleStressState()
        {
            ClearModuleStressState(true);
        }

        private void ClearModuleStressState(bool publishShaderClear)
        {
            bool shouldPublishClear = _lastUploadedModuleStressCount != 0 ||
                                      _lastUploadedPeakModuleStress01 > ModuleStressUploadEpsilon ||
                                      _lastUploadedModuleStressLowBlend > ModuleStressQualityEpsilon;
            _peakModuleStress01 = 0f;
            _lastUploadedPeakModuleStress01 = 0f;
            _lastUploadedModuleStressCount = 0;
            _lastUploadedModuleStressLowBlend = 0f;
            _lastUploadedModuleStressQualityWeight = -1f;
            _moduleStressOrderHash = 0u;
            if (TryAcquireModuleStressWriteBuffers(
                    1,
                    out NativeArray<float> moduleStressScalars,
                    out NativeArray<float> previousModuleStressScalars,
                    out NativeArray<float> moduleImpactStressSpikes,
                    out NativeArray<byte> moduleCompromisedFlags,
                    out IDataVault vault))
            {
                try
                {
                    int clearCount = moduleStressScalars.Length;
                    for (int i = 0; i < clearCount; i++)
                        moduleStressScalars[i] = 0f;

                    clearCount = previousModuleStressScalars.Length;
                    for (int i = 0; i < clearCount; i++)
                        previousModuleStressScalars[i] = 0f;

                    clearCount = moduleImpactStressSpikes.Length;
                    for (int i = 0; i < clearCount; i++)
                        moduleImpactStressSpikes[i] = 0f;

                    clearCount = moduleCompromisedFlags.Length;
                    for (int i = 0; i < clearCount; i++)
                        moduleCompromisedFlags[i] = 0;
                }
                finally
                {
                    ReleaseModuleStressWriteLocks(vault);
                }
            }

            if (publishShaderClear && shouldPublishClear)
            {
                ReleaseModuleStressBuffer(false);
                _pendingModuleStressCount = 0;
                _pendingModuleStressPeak01 = 0f;
                _pendingModuleStressQualityWeight = 0f;
                _pendingModuleStressShaderDirty = true;
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

        private float ResolveActiveModuleFloodStress01(
            BaseModule baseModule,
            int graphNodeIndex,
            bool hasGraphRecord,
            NativeArray<float>.ReadOnly roomWaterLevels)
        {
            if (hasGraphRecord && roomWaterLevels.IsCreated && (uint)graphNodeIndex < (uint)roomWaterLevels.Length)
                return math.saturate(roomWaterLevels[graphNodeIndex]);

            return baseModule != null && baseModule.IsFlooded ? 1f : 0f;
        }

        private float ResolveModuleStress01(
            int nodeIndex,
            BaseModule baseModule,
            float depthMeters,
            float floodStress01,
            float deltaTime,
            NativeArray<float> moduleImpactStressSpikes,
            out bool invalidState)
        {
            invalidState = false;
            if (!math.isfinite(depthMeters))
            {
                invalidState = true;
                depthMeters = 0f;
            }

            float depth01 = math.saturate(depthMeters * AnalyticalFullStressDepthInv);
            float ambientPressure01 = math.saturate(ResolveAnalyticalDepthScale(depthMeters) * depth01);
            float maxIntegrity = baseModule.MaxIntegrity;
            float currentIntegrity = baseModule.CurrentIntegrity;
            float integrity01 = 1f;
            if (math.isfinite(maxIntegrity) && maxIntegrity > 0.01f && math.isfinite(currentIntegrity))
                integrity01 = math.saturate(currentIntegrity * math.rcp(maxIntegrity));
            else if (!math.isfinite(maxIntegrity) || !math.isfinite(currentIntegrity))
                invalidState = true;

            float impactDamage01 = math.saturate(1f - integrity01);
            impactDamage01 = math.max(impactDamage01, SaturateFinite01(baseModule.JointShearStress01, ref invalidState));
            impactDamage01 = math.max(impactDamage01, SaturateFinite01(baseModule.PressureCompressionAlpha01, ref invalidState));

            impactDamage01 = math.max(impactDamage01, SaturateFinite01(floodStress01, ref invalidState) * ModuleStressFloodWeight);

            float spike01 = 0f;
            if (moduleImpactStressSpikes.IsCreated && (uint)nodeIndex < (uint)moduleImpactStressSpikes.Length)
            {
                float storedSpike01 = moduleImpactStressSpikes[nodeIndex];
                if (math.isfinite(storedSpike01))
                {
                    spike01 = math.saturate(storedSpike01);
                    moduleImpactStressSpikes[nodeIndex] = math.max(0f, spike01 - ModuleStressImpactSpikeDecayPerSecond * deltaTime);
                }
                else
                {
                    invalidState = true;
                    moduleImpactStressSpikes[nodeIndex] = 0f;
                }
            }

            float stress01 = (ambientPressure01 * ModuleStressDepthWeight) + (impactDamage01 * ModuleStressDamageWeight) + spike01;
            return math.saturate(stress01);
        }

        private static float SaturateFinite01(float value, ref bool invalidState)
        {
            if (math.isfinite(value))
                return math.saturate(value);

            invalidState = true;
            return 0f;
        }

        private void ConsumeModuleStressSignals(int moduleCount, NativeArray<float> moduleImpactStressSpikes)
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
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

                InjectModuleStressSpike(moduleIndex, math.max(signal.Intensity01, signal.Depth), moduleImpactStressSpikes);
            }

            ReadOnlySpan<CoreCombatDamageSignal> damageSignals = SignalBus<CoreCombatDamageSignal>.GetFrameSnapshot();
            for (int i = 0; i < damageSignals.Length; i++)
            {
                CoreCombatDamageSignal signal = damageSignals[i];
                if ((signal.Flags & CoreCombatDamageSignal.VisualOnlyFlag) != 0)
                    continue;

                if (!IsModuleImpactStressSignal(signal.SourceId, signal.DamageType, signal.Magnitude))
                    continue;

                float3 runtimePoint = default;
                bool allowNearest = (signal.Flags & CoreCombatDamageSignal.LegacyMirrorFlag) == 0 &&
                                    CombatDamageSignalCodec.TryToRuntimePoint(in signal, out runtimePoint);
                if (!TryResolveModuleStressIndex(signal.TargetHash, signal.TargetId, runtimePoint, allowNearest, moduleCount, out int moduleIndex))
                    continue;

                InjectModuleStressSpike(moduleIndex, signal.Magnitude, moduleImpactStressSpikes);
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

        private void InjectModuleStressSpike(int moduleIndex, float magnitude, NativeArray<float> moduleImpactStressSpikes)
        {
            if (!moduleImpactStressSpikes.IsCreated || (uint)moduleIndex >= (uint)moduleImpactStressSpikes.Length)
                return;

            if (!math.isfinite(magnitude))
            {
                WriteFloodBlackBoxSample(FloodBlackBoxModuleStressInvalidFlag);
                DumpModuleStressBlackBoxOnce(FloodBlackBoxModuleStressInvalidFlag);
                return;
            }

            float previousSpike01 = moduleImpactStressSpikes[moduleIndex];
            if (!math.isfinite(previousSpike01))
            {
                WriteFloodBlackBoxSample(FloodBlackBoxModuleStressInvalidFlag);
                DumpModuleStressBlackBoxOnce(FloodBlackBoxModuleStressInvalidFlag);
                previousSpike01 = 0f;
            }

            float spike01 = math.saturate(math.max(0f, magnitude) * ModuleStressImpactSpikeStrength);
            moduleImpactStressSpikes[moduleIndex] = math.max(previousSpike01, spike01);
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
            float globalQualityWeight,
            byte tierProfile,
            NativeArray<byte> moduleCompromisedFlags)
        {
            if (!moduleCompromisedFlags.IsCreated || (uint)moduleIndex >= (uint)moduleCompromisedFlags.Length)
                return;

            if (moduleCompromisedFlags[moduleIndex] != 0)
                return;

            moduleCompromisedFlags[moduleIndex] = 1;
            BaseModuleCompromisedSignal signal = new BaseModuleCompromisedSignal
            {
                ModuleCenter = modulePosition,
                Stress01 = math.saturate(stress01),
                PeakStress01 = math.saturate(peakStress01),
                DepthMeters = math.max(0f, depthMeters),
                NodeId = hasGraphRecord ? module.NodeId : 0u,
                ModuleHash = ResolveModuleStressRuntimeKey(baseModule, module, hasGraphRecord),
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                Sequence = ++_moduleStressSequence,
                SourceId = DamageSourceIds.HabitatIntegrity,
                Flags = ResolveModuleStressDisplacementMaxMeters(globalQualityWeight) <= 0f
                    ? BaseModuleCompromisedSignal.LowTierVisualOnlyFlag
                    : BaseModuleCompromisedSignal.MaxDeformationFlag,
                StressIndex = (byte)math.min(byte.MaxValue, moduleIndex),
                QualityTier = tierProfile
            };
            SignalBus<BaseModuleCompromisedSignal>.TryPushTracked(in signal, ref s_x001HabitatGraphManagerSignalPushDropCount);
        }

        private void UploadModuleStressMatrix(int moduleCount, float peakStress01, float globalQualityWeight)
        {
            int safeModuleCount = math.min(math.max(0, moduleCount), ModuleStressShaderCapacity);
            float safePeakStress01 = math.isfinite(peakStress01) ? math.saturate(peakStress01) : 0f;
            float qualityWeight = SanitizeQualityWeight(globalQualityWeight);
            float lowTierBlend = safeModuleCount > 0 ? ResolveModuleStressLowBlend(qualityWeight) : 0f;
            _pendingModuleStressCount = safeModuleCount;
            _pendingModuleStressPeak01 = safePeakStress01;
            _pendingModuleStressQualityWeight = qualityWeight;
            _pendingModuleStressShaderDirty = true;
            _lastUploadedModuleStressCount = safeModuleCount;
            _lastUploadedPeakModuleStress01 = safePeakStress01;
            _lastUploadedModuleStressLowBlend = lowTierBlend;
            _lastUploadedModuleStressQualityWeight = qualityWeight;
            _moduleStressSequence++;
        }

        private void FlushModuleStressShader()
        {
            if (!_pendingModuleStressShaderDirty)
                return;

            _pendingModuleStressShaderDirty = false;
            int safeModuleCount = _pendingModuleStressCount;
            float safePeakStress01 = _pendingModuleStressPeak01;
            float qualityWeight = SanitizeQualityWeight(_pendingModuleStressQualityWeight);
            float lowTierBlend = safeModuleCount > 0 ? ResolveModuleStressLowBlend(qualityWeight) : 0f;
            bool hasVisibleStress = safeModuleCount > 0 &&
                                    ResolveModuleStressDisplacementMaxMeters(qualityWeight) > 0f &&
                                    safePeakStress01 > ModuleStressUploadEpsilon;
            if (hasVisibleStress)
            {
                EnsureModuleStressBuffer(safeModuleCount);
                GraphicsBuffer writeBuffer = ResolveModuleStressWriteBuffer();
                if (writeBuffer != null &&
                    EnsureModuleStressHandles(safeModuleCount) &&
                    TryAcquireHabitatVaultWriteBuffer(
                        HabitatModuleStressScalarsBufferId,
                        safeModuleCount,
                        in _moduleStressScalarsHandle,
                        out NativeArray<float> moduleStressScalars,
                        out IDataVault vault))
                {
                    try
                    {
                        GraphicsBufferUploadUtility.UploadNativeArray(writeBuffer, moduleStressScalars, safeModuleCount);
                        _activeModuleStressBuffer = writeBuffer;
                        _moduleStressBufferWriteIndex ^= 1;
                        Shader.SetGlobalBuffer(HabitatModuleStressBufferId, _activeModuleStressBuffer);
                    }
                    finally
                    {
                        vault.ReleaseWriteLock(in _moduleStressScalarsHandle, SystemID.Construction);
                    }
                }
                else
                {
                    ReleaseModuleStressBuffer(false);
                    safeModuleCount = 0;
                    safePeakStress01 = 0f;
                    qualityWeight = 0f;
                    lowTierBlend = 0f;
                    _lastUploadedModuleStressCount = -1;
                }
            }

            PublishModuleStressShaderVisualSync(safeModuleCount, safePeakStress01, qualityWeight, lowTierBlend);
        }

        private void PublishModuleStressShaderVisualSync(int moduleCount, float peakStress01, float globalQualityWeight, float lowTierBlend)
        {
            int safeModuleCount = math.min(math.max(0, moduleCount), ModuleStressShaderCapacity);
            float safePeakStress01 = math.isfinite(peakStress01) ? math.saturate(peakStress01) : 0f;
            float safeLowBlend = math.saturate(math.isfinite(lowTierBlend) ? lowTierBlend : 0f);
            bool hasVisibleStress = safeModuleCount > 0 &&
                                    ResolveModuleStressDisplacementMaxMeters(globalQualityWeight) > 0f &&
                                    safePeakStress01 > ModuleStressUploadEpsilon;
            float displacementMaxMeters = hasVisibleStress
                ? ResolveModuleStressDisplacementMaxMeters(globalQualityWeight)
                : 0f;
            Shader.SetGlobalVector(
                HabitatModuleStressParamsId,
                new Vector4(
                    safeModuleCount,
                    displacementMaxMeters,
                    safeLowBlend,
                    safePeakStress01));
        }

        private void EnsureModuleStressBuffer(int moduleCount)
        {
            int safeCount = math.max(1, moduleCount);
            if (_moduleStressBufferA != null &&
                _moduleStressBufferA.count >= safeCount &&
                _moduleStressBufferB != null &&
                _moduleStressBufferB.count >= safeCount)
            {
                if (_activeModuleStressBuffer == null)
                    _activeModuleStressBuffer = _moduleStressBufferA;
                return;
            }

            ReleaseModuleStressBuffer(false);
            int capacity = NextPowerOfTwo(math.max(safeCount, InitialNodeCapacity));
            _moduleStressBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float>(capacity);
            _moduleStressBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float>(capacity);
            _activeModuleStressBuffer = _moduleStressBufferA;
            _moduleStressBufferWriteIndex = 0;
            _lastUploadedModuleStressCount = -1;
        }

        private GraphicsBuffer ResolveModuleStressWriteBuffer()
        {
            GraphicsBuffer writeBuffer = _moduleStressBufferWriteIndex == 0
                ? _moduleStressBufferA
                : _moduleStressBufferB;
            if (writeBuffer != null)
                return writeBuffer;

            return ReferenceEquals(_activeModuleStressBuffer, _moduleStressBufferA)
                ? _moduleStressBufferB
                : _moduleStressBufferA;
        }

        private static float ResolveAnalyticalDetailWeight(float globalQualityWeight)
        {
            float q = SanitizeQualityWeight(globalQualityWeight);
            return math.smoothstep(0f, 1f, q);
        }

        private static float ResolveAnalyticalDisplacementMaxMeters(float globalQualityWeight)
        {
            return AnalyticalShaderDisplacementMaxMeters * ResolveAnalyticalDetailWeight(globalQualityWeight);
        }

        private static float ResolveModuleStressLowBlend(float globalQualityWeight)
        {
            return 1f - ResolveAnalyticalDetailWeight(globalQualityWeight);
        }

        private static float ResolveModuleStressDisplacementMaxMeters(float globalQualityWeight)
        {
            float q = SanitizeQualityWeight(globalQualityWeight);
            float curve = math.smoothstep(0f, 1f, q);
            return math.lerp(0f, ModuleStressUltraDisplacementMaxMeters, curve);
        }

        private static byte ResolveModuleStressQualityProfileByte(float globalQualityWeight)
        {
            float q = SanitizeQualityWeight(globalQualityWeight);
            return (byte)math.clamp((int)math.round(q * byte.MaxValue), 0, byte.MaxValue);
        }

        private void TryPublishLowDetailAnalyticalStressFeedback(float3 center, float stress01, float globalQualityWeight)
        {
            if (ResolveAnalyticalDetailWeight(globalQualityWeight) >= 1f - ModuleStressQualityEpsilon ||
                stress01 < AnalyticalLowDetailFeedbackThreshold01)
            {
                return;
            }

            float now = ResolveHabitatClockSeconds();
            if (now < _nextAnalyticalLowDetailFeedbackTime)
                return;

            _nextAnalyticalLowDetailFeedbackTime = now + AnalyticalLowDetailFeedbackCooldownSeconds;
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

            CameraJuiceSignals.TryPublishImpact(
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

            TryReadHabitatVaultBuffer(
                HabitatRoomWaterLevelsBufferId,
                1,
                in _roomWaterLevelsHandle,
                out NativeArray<float>.ReadOnly roomWaterLevels);
            int moduleCount = math.min(math.max(0, _nodeCount), _moduleBuffer.Count);
            for (int nodeIndex = 0; nodeIndex < moduleCount; nodeIndex++)
            {
                ModuleRecord module = _moduleBuffer[nodeIndex];
                BaseModule baseModule = module.BaseModule;
                if (baseModule == null || !baseModule.isActiveAndEnabled)
                    continue;

                float jointStress01 = math.saturate(baseModule.JointShearStress01);
                float compressionStress01 = math.saturate(baseModule.PressureCompressionAlpha01);
                float floodStress01 = roomWaterLevels.IsCreated && nodeIndex < roomWaterLevels.Length
                    ? math.saturate(roomWaterLevels[nodeIndex])
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
            IAudioService audioService = GetCachedAudioService();
            if (audioService != null && audioService.QueueHullStressSignal(in signal))
                return;

            ProceduralAudioEvents.TryRaiseHullStressSignal(in signal);
        }

        private IAudioService GetCachedAudioService()
        {
            return _audioService;
        }

        private void TryFlagAnalyticalIntegrityLeak(int moduleCount, float stress)
        {
            if (_analyticalBreachNodeId != 0u && ContainsAnalyticalBreachNode(moduleCount, _analyticalBreachNodeId))
                return;

            byte threshold = ResolveAnalyticalBreachThreshold(stress);
            uint timeSeconds = (uint)Mathf.Max(0, Mathf.FloorToInt(ResolveHabitatClockSeconds()));
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

        private void ApplyGraphFluidIncursion(float deltaTime, float globalQualityWeight)
        {
            if (deltaTime <= 0f ||
                _nodeCount <= 0 ||
                !EnsureGraphHandles(_nodeCount, math.max(1, _edgeCount)))
            {
                return;
            }

            int moduleCount = math.min(_nodeCount, _moduleBuffer.Count);
            if (moduleCount <= 0)
                return;

            int graphFloodNodeBudget = ResolveGraphFloodNodeBudget(globalQualityWeight);
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
                        ResolvePressureRootLut(pressureDeltaKPa));
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
            bool finalizedChanged = TryFinalizeFloodPropagationJobNoWait();
            if (_floodPropagationPending)
                return finalizedChanged;

            if (moduleCount <= 0 ||
                deltaTime <= 0f ||
                !EnsureFloodRoomHandles(moduleCount) ||
                !EnsureGraphHandles(moduleCount, math.max(1, _edgeCount)) ||
                !EnsureFloodPropagationSummaryHandle())
            {
                return false;
            }

            if (!TryAcquireHabitatVaultWriteBuffer(
                    HabitatFloodPropagationSummaryBufferId,
                    1,
                    in _floodPropagationSummaryHandle,
                    out NativeArray<HabitatFloodPropagationSummary> floodPropagationSummary,
                    out IDataVault floodPropagationSummaryVault))
            {
                return false;
            }

            if (!TryAcquireFloodRoomWriteBuffers(
                    moduleCount,
                    out NativeArray<float> roomWaterLevels,
                    out NativeArray<float> roomVolumes,
                    out NativeArray<float> roomFloodDeltaLevels,
                    out NativeArray<byte> roomFlags,
                    out IDataVault floodRoomVault))
            {
                floodPropagationSummaryVault.ReleaseWriteLock(in _floodPropagationSummaryHandle, SystemID.Construction);
                return false;
            }

            if (!TryAcquireFloodGraphJobBuffers(
                    moduleCount,
                    math.max(1, _edgeCount),
                    out HabitatFloodGraphJobViews graph,
                    out IDataVault floodGraphVault))
            {
                floodPropagationSummaryVault.ReleaseWriteLock(in _floodPropagationSummaryHandle, SystemID.Construction);
                ReleaseFloodRoomWriteLocks(floodRoomVault);
                return false;
            }

            floodPropagationSummary[0] = default;
            HabitatFloodPropagationJob job = new HabitatFloodPropagationJob
            {
                NodeCount = moduleCount,
                EdgeCount = math.min(_edgeCount, graph.EdgeFlags.Length),
                StartNodeIndex = startNodeIndex,
                ProcessNodeCount = processNodeCount,
                DeltaTime = deltaTime,
                FlowRate01PerSecond = GraphFloodTransferRateM3PerSecond,
                MaxTransferPerEdgeM3 = GraphFloodMaxTransferPerEdgeM3,
                WaterEpsilon01 = GraphFloodWaterEpsilonM3,
                EdgeOffsets = graph.EdgeOffsets,
                EdgeDestinations = graph.EdgeDestinations,
                EdgeResistance = graph.EdgeResistance,
                RoomWaterLevels = roomWaterLevels,
                RoomVolumes = roomVolumes,
                RoomFlags = roomFlags,
                EdgeFlags = graph.EdgeFlags,
                RoomDeltaLevels = roomFloodDeltaLevels,
                Result = floodPropagationSummary
            };

            bool scheduled = false;
            try
            {
                JobHandle pendingHandle = job.Schedule();
                _floodPropagationHandle = pendingHandle;
                _floodPropagationPending = true;
                _floodPropagationSummaryWriteLockHeld = true;
                _floodPropagationSummaryWriteLockVault = floodPropagationSummaryVault;
                _floodPropagationRoomWriteLockHeld = true;
                _floodPropagationRoomWriteLockVault = floodRoomVault;
                _floodPropagationGraphWriteLockHeld = true;
                _floodPropagationGraphWriteLockVault = floodGraphVault;
                _pendingFloodPropagationModuleCount = moduleCount;
                scheduled = true;
                H8Memory.RegisterActiveJob(SystemID.Construction, pendingHandle);
                return finalizedChanged;
            }
            finally
            {
                if (!scheduled)
                {
                    floodPropagationSummaryVault.ReleaseWriteLock(in _floodPropagationSummaryHandle, SystemID.Construction);
                    ReleaseFloodRoomWriteLocks(floodRoomVault);
                    ReleaseFloodGraphWriteLocks(floodGraphVault, 4);
                }
            }
        }

        private bool TryFinalizeFloodPropagationJobNoWait()
        {
            if (!_floodPropagationPending)
                return false;

            if (!_floodPropagationHandle.IsCompleted)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _floodPropagationHandle))
                return false;

            return FinishFloodPropagationJob();
        }

        private bool CompleteFloodPropagationJobForTeardown()
        {
            if (!_floodPropagationPending)
                return false;

            if (!DispatcherJobFence.TryComplete(ref _floodPropagationHandle, forceComplete: true))
                return false;

            return FinishFloodPropagationJob();
        }

        private bool FinishFloodPropagationJob()
        {
            _floodPropagationPending = false;

            if (!TryOpenHabitatVaultBuffer(
                    _floodPropagationSummaryWriteLockVault,
                    in _floodPropagationSummaryHandle,
                    HabitatFloodPropagationSummaryBufferId,
                    1,
                    out NativeArray<HabitatFloodPropagationSummary> floodPropagationSummary))
            {
                ReleaseFloodPropagationSummaryWriteLock();
                ReleaseFloodPropagationRoomWriteLocks();
                ReleaseFloodPropagationGraphWriteLocks();
                return false;
            }

            if (!TryOpenHabitatVaultBuffer(
                    _floodPropagationRoomWriteLockVault,
                    in _roomVolumesHandle,
                    HabitatRoomVolumesBufferId,
                    _pendingFloodPropagationModuleCount,
                    out NativeArray<float> roomVolumes) ||
                !TryOpenHabitatVaultBuffer(
                    _floodPropagationRoomWriteLockVault,
                    in _roomFloodDeltaLevelsHandle,
                    HabitatRoomFloodDeltaLevelsBufferId,
                    _pendingFloodPropagationModuleCount,
                    out NativeArray<float> roomFloodDeltaLevels))
            {
                ReleaseFloodPropagationSummaryWriteLock();
                ReleaseFloodPropagationRoomWriteLocks();
                ReleaseFloodPropagationGraphWriteLocks();
                return false;
            }

            HabitatFloodPropagationSummary summary = floodPropagationSummary[0];
            ReleaseFloodPropagationSummaryWriteLock();
            if (summary.NonFiniteCount > 0)
            {
                WriteFloodBlackBoxSample(FloodBlackBoxNonFiniteFlag);
                DumpFloodBlackBoxOnce(FloodBlackBoxNonFiniteFlag);
            }
            if (summary.InvalidConnectionCount > 0)
            {
                WriteFloodBlackBoxSample(FloodBlackBoxTopologyInvalidFlag);
            }

            bool changed;
            try
            {
                changed = ApplyFloodPropagationDeltas(_pendingFloodPropagationModuleCount, roomVolumes, roomFloodDeltaLevels);
            }
            finally
            {
                ReleaseFloodPropagationRoomWriteLocks();
                ReleaseFloodPropagationGraphWriteLocks();
            }

            return changed || summary.FlowedEdgeCount > 0;
        }

        private bool ApplyFloodPropagationDeltas(
            int moduleCount,
            NativeArray<float> roomVolumes,
            NativeArray<float> roomFloodDeltaLevels)
        {
            int safeCount = math.min(
                math.min(moduleCount, _moduleBuffer.Count),
                math.min(roomFloodDeltaLevels.Length, roomVolumes.Length));
            bool changed = false;

            for (int nodeIndex = 0; nodeIndex < safeCount; nodeIndex++)
            {
                float deltaLevel01 = roomFloodDeltaLevels[nodeIndex];
                if (!math.isfinite(deltaLevel01) || deltaLevel01 == 0f)
                    continue;

                BaseModule baseModule = _moduleBuffer[nodeIndex].BaseModule;
                if (baseModule == null || !baseModule.isActiveAndEnabled)
                    continue;

                float roomVolumeM3 = math.max(0.001f, roomVolumes[nodeIndex]);
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

        private bool IsFloodEdgeSealed(int csrEdgeIndex)
        {
            return TryReadHabitatVaultBuffer(
                       HabitatGraphEdgeFlagsBufferId,
                       math.max(1, csrEdgeIndex + 1),
                       in _edgeFlagsHandle,
                       out NativeArray<byte>.ReadOnly edgeFlags) &&
                   csrEdgeIndex >= 0 &&
                   csrEdgeIndex < edgeFlags.Length &&
                   (edgeFlags[csrEdgeIndex] & (byte)HabitatEdgeFloodFlags.Sealed) != 0;
        }

        private bool IsFloodAutoSealActive(int nodeIndex, BaseModule baseModule)
        {
            return baseModule != null &&
                   baseModule.HasPower &&
                   ResolveAuthoritativeRoomWaterLevel01(nodeIndex, baseModule) > GraphFloodAutoSealThreshold01;
        }

        private float ResolveAuthoritativeRoomWaterLevel01(int nodeIndex, BaseModule baseModule)
        {
            if (TryReadHabitatVaultBuffer(
                    HabitatRoomWaterLevelsBufferId,
                    math.max(1, nodeIndex + 1),
                    in _roomWaterLevelsHandle,
                    out NativeArray<float>.ReadOnly roomWaterLevels))
            {
                return ResolveAuthoritativeRoomWaterLevel01(nodeIndex, baseModule, roomWaterLevels);
            }

            if (baseModule == null)
                return 0f;

            float roomCapacityM3 = math.max(0.001f, baseModule.ResolveFloodCapacityM3());
            float waterVolumeM3 = baseModule.WaterVolumeM3;
            if (!math.isfinite(roomCapacityM3) || !math.isfinite(waterVolumeM3))
                return 0f;

            return math.saturate(waterVolumeM3 * math.rcp(roomCapacityM3));
        }

        private static float ResolveAuthoritativeRoomWaterLevel01(
            int nodeIndex,
            BaseModule baseModule,
            NativeArray<float>.ReadOnly roomWaterLevels)
        {
            if (roomWaterLevels.IsCreated &&
                nodeIndex >= 0 &&
                nodeIndex < roomWaterLevels.Length)
            {
                float roomLevel01 = roomWaterLevels[nodeIndex];
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
            if (!TryAcquireFloodGraphJobBuffers(
                    math.max(1, _nodeCount),
                    math.max(1, _edgeCount),
                    out HabitatFloodGraphJobViews graph,
                    out IDataVault vault))
                return;

            try
            {
                SetFloodEdgeFlag(ref graph, csrEdgeIndex, flag);
            }
            finally
            {
                ReleaseFloodGraphWriteLocks(vault, 4);
            }
        }

        private static void SetFloodEdgeFlag(ref HabitatGraphWriteViews graph, int csrEdgeIndex, HabitatEdgeFloodFlags flag)
        {
            if (!graph.EdgeFlags.IsCreated ||
                csrEdgeIndex < 0 ||
                csrEdgeIndex >= graph.EdgeFlags.Length)
            {
                return;
            }

            graph.EdgeFlags[csrEdgeIndex] = (byte)(graph.EdgeFlags[csrEdgeIndex] | (byte)flag);
        }

        private static void SetFloodEdgeFlag(ref HabitatFloodGraphJobViews graph, int csrEdgeIndex, HabitatEdgeFloodFlags flag)
        {
            if (!graph.EdgeFlags.IsCreated ||
                csrEdgeIndex < 0 ||
                csrEdgeIndex >= graph.EdgeFlags.Length)
            {
                return;
            }

            graph.EdgeFlags[csrEdgeIndex] = (byte)(graph.EdgeFlags[csrEdgeIndex] | (byte)flag);
        }

        private void ClearActiveFloodEdgeFlags()
        {
            if (!TryAcquireFloodGraphJobBuffers(
                    math.max(1, _nodeCount),
                    math.max(1, _edgeCount),
                    out HabitatFloodGraphJobViews graph,
                    out IDataVault vault))
                return;

            try
            {
                ClearActiveFloodEdgeFlags(ref graph);
            }
            finally
            {
                ReleaseFloodGraphWriteLocks(vault, 4);
            }
        }

        private void ClearActiveFloodEdgeFlags(ref HabitatGraphWriteViews graph)
        {
            if (!graph.EdgeFlags.IsCreated)
                return;

            int edgeCount = math.min(math.max(0, _edgeCount), graph.EdgeFlags.Length);
            for (int edgeIndex = 0; edgeIndex < edgeCount; edgeIndex++)
                graph.EdgeFlags[edgeIndex] = 0;
        }

        private void ClearActiveFloodEdgeFlags(ref HabitatFloodGraphJobViews graph)
        {
            if (!graph.EdgeFlags.IsCreated)
                return;

            int edgeCount = math.min(math.max(0, _edgeCount), graph.EdgeFlags.Length);
            for (int edgeIndex = 0; edgeIndex < edgeCount; edgeIndex++)
                graph.EdgeFlags[edgeIndex] = 0;
        }

        private void SyncFloodRoomStateSnapshot()
        {
            if (!TryAcquireFloodRoomWriteBuffers(
                    math.max(1, _nodeCount),
                    out NativeArray<float> roomWaterLevels,
                    out NativeArray<float> roomVolumes,
                    out NativeArray<float> roomFloodDeltaLevels,
                    out NativeArray<byte> roomFlagsBuffer,
                    out IDataVault vault))
            {
                return;
            }

            try
            {
                int moduleCount = math.min(
                    math.min(_nodeCount, _moduleBuffer.Count),
                    math.min(roomWaterLevels.Length, math.min(roomVolumes.Length, roomFlagsBuffer.Length)));
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

                    roomWaterLevels[nodeIndex] = roomWaterLevel01;
                    roomVolumes[nodeIndex] = roomVolumeM3;
                    roomFlagsBuffer[nodeIndex] = (byte)roomFlags;

                    stateHash = HashFloodBlackBox(stateHash, module.NodeId);
                    stateHash = HashFloodBlackBox(stateHash, (uint)roomFlagsBuffer[nodeIndex]);
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

                if (TryReadHabitatVaultBuffer(
                        HabitatGraphEdgeFlagsBufferId,
                        math.max(1, _edgeCount),
                        in _edgeFlagsHandle,
                        out NativeArray<byte>.ReadOnly edgeFlags))
                {
                    int edgeFlagCount = math.min(math.max(0, _edgeCount), edgeFlags.Length);
                    stateHash = HashFloodBlackBox(stateHash, (uint)edgeFlagCount);
                    for (int edgeIndex = 0; edgeIndex < edgeFlagCount; edgeIndex++)
                        stateHash = HashFloodBlackBox(stateHash, edgeFlags[edgeIndex]);
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
            finally
            {
                ReleaseFloodRoomWriteLocks(vault);
            }
        }

        private void ClearFloodRoomStateSnapshot()
        {
            _floodedRoomCount = 0;
            _baseTotalStress = 0f;
            _maxRoomWaterLevel01 = 0f;
            _totalRoomWaterVolumeM3 = 0f;
            _floodBlackBoxStateHash = 0u;

            if (TryAcquireFloodRoomWriteBuffers(
                    1,
                    out NativeArray<float> roomWaterLevels,
                    out NativeArray<float> roomVolumes,
                    out NativeArray<float> roomFloodDeltaLevels,
                    out NativeArray<byte> roomFlags,
                    out IDataVault vault))
            {
                try
                {
                    int clearCount = math.min(roomWaterLevels.Length, math.min(roomVolumes.Length, roomFlags.Length));
                    for (int nodeIndex = 0; nodeIndex < clearCount; nodeIndex++)
                    {
                        roomWaterLevels[nodeIndex] = 0f;
                        roomVolumes[nodeIndex] = 0f;
                        roomFlags[nodeIndex] = 0;
                        if (nodeIndex < roomFloodDeltaLevels.Length)
                            roomFloodDeltaLevels[nodeIndex] = 0f;
                    }
                }
                finally
                {
                    ReleaseFloodRoomWriteLocks(vault);
                }
            }
        }

        private void RecordNonFinitePressureIngress()
        {
            WriteFloodBlackBoxSample(FloodBlackBoxNonFiniteFlag);
        }

        private void WriteFloodBlackBoxSample(uint reasonFlags)
        {
            if (!EnsureFloodBlackBoxHandle() ||
                !TryAcquireHabitatVaultWriteBuffer(
                    HabitatFloodBlackBoxBufferId,
                    FloodBlackBoxCapacity,
                    in _floodBlackBoxHandle,
                    out NativeArray<HabitatFloodBlackBoxEntry> floodBlackBox,
                    out IDataVault vault))
            {
                return;
            }

            uint flags = reasonFlags;
            bool shouldDump = false;
            try
            {
                if (!math.isfinite(_baseTotalStress) ||
                    !math.isfinite(_maxRoomWaterLevel01) ||
                    !math.isfinite(_totalRoomWaterVolumeM3))
                {
                    flags |= FloodBlackBoxNonFiniteFlag;
                }

                int cursor = _floodBlackBoxCursor;
                if ((uint)cursor >= (uint)floodBlackBox.Length)
                    cursor = 0;

                HabitatFloodBlackBoxEntry entry = default;
                entry.Frame = unchecked((int)Hecton8.Core.SystemDispatcher.CurrentFrameId);
                entry.NodeCount = (ushort)math.min(ushort.MaxValue, math.max(0, _nodeCount));
                entry.EdgeCount = (ushort)math.min(ushort.MaxValue, math.max(0, _edgeCount));
                entry.FloodedRoomCount = (ushort)math.min(ushort.MaxValue, math.max(0, _floodedRoomCount));
                entry.Reserved0 = 0;
                entry.BaseTotalStress = _baseTotalStress;
                entry.MaxWaterLevel01 = _maxRoomWaterLevel01;
                entry.TotalWaterVolumeM3 = _totalRoomWaterVolumeM3;
                entry.PeakModuleStress = _peakModuleStress01;
                entry.Flags = flags;
                entry.StateHash = _floodBlackBoxStateHash;
                entry.DeformationSequence = _moduleStressSequence;
                floodBlackBox[cursor] = entry;

                _floodBlackBoxCursor = (cursor + 1) % floodBlackBox.Length;
                shouldDump = (flags & FloodBlackBoxNonFiniteFlag) != 0u;
            }
            finally
            {
                vault.ReleaseWriteLock(in _floodBlackBoxHandle, SystemID.Construction);
            }

            if (shouldDump)
                DumpFloodBlackBoxOnce(flags);
        }

        private void DumpFloodBlackBoxOnce(uint reasonFlags)
        {
            if (_floodBlackBoxDumped ||
                !EnsureFloodBlackBoxHandle() ||
                !TryReadHabitatVaultBuffer(
                    HabitatFloodBlackBoxBufferId,
                    FloodBlackBoxCapacity,
                    in _floodBlackBoxHandle,
                    out NativeArray<HabitatFloodBlackBoxEntry>.ReadOnly _))
            {
                return;
            }

            _floodBlackBoxDumped = true;
            DumpFloodBlackBox(reasonFlags);
        }

        private void DumpModuleStressBlackBoxOnce(uint reasonFlags)
        {
            if (_moduleStressBlackBoxDumped ||
                !EnsureFloodBlackBoxHandle() ||
                !TryReadHabitatVaultBuffer(
                    HabitatFloodBlackBoxBufferId,
                    FloodBlackBoxCapacity,
                    in _floodBlackBoxHandle,
                    out NativeArray<HabitatFloodBlackBoxEntry>.ReadOnly _))
            {
                return;
            }

            _moduleStressBlackBoxDumped = true;
            DumpFloodBlackBox(reasonFlags, ModuleStressBlackBoxDumpRelativePath);
        }

        private void DumpFloodBlackBox(uint reasonFlags)
        {
            DumpFloodBlackBox(reasonFlags, FloodBlackBoxDumpRelativePath);
        }

        private void DumpFloodBlackBox(uint reasonFlags, string relativePath)
        {
            if (!EnsureFloodBlackBoxHandle() ||
                !TryReadHabitatVaultBuffer(
                    HabitatFloodBlackBoxBufferId,
                    FloodBlackBoxCapacity,
                    in _floodBlackBoxHandle,
                    out NativeArray<HabitatFloodBlackBoxEntry>.ReadOnly floodBlackBox))
            {
                return;
            }

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
                    for (int offset = 0; offset < floodBlackBox.Length; offset++)
                    {
                        int index = (_floodBlackBoxCursor + offset) % floodBlackBox.Length;
                        WriteFloodBlackBoxEntry(writer, floodBlackBox[index]);
                    }
                }
            }
            catch (Exception exception)
            {
                _ = exception;
                Hecton8.Core.H8Debug.LogWarning("Habitat flood blackbox dump failed.");
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

        private static int ResolveGraphFloodNodeBudget(float globalQualityWeight)
        {
            float q = SanitizeQualityWeight(globalQualityWeight);
            float curve = math.smoothstep(0f, 1f, q);
            int budget = (int)math.round(math.lerp(
                (float)GraphFloodMinTraversalNodesPerTick,
                GraphFloodMaxTraversalNodesPerTick,
                curve));
            return math.clamp(budget, GraphFloodMinTraversalNodesPerTick, GraphFloodMaxTraversalNodesPerTick);
        }

        private static float ResolvePressureRootLut(float pressureDeltaKPa)
        {
            if (pressureDeltaKPa <= 0f || !math.isfinite(pressureDeltaKPa))
                return 0f;

            float clampedDeltaKPa = math.min(pressureDeltaKPa, PressureRootLutMaxKPa);
            float scaledIndex = clampedDeltaKPa * PressureRootLutStepKPaInv;
            int lowerIndex = math.clamp((int)scaledIndex, 0, PressureRootLutSize - 1);
            bool exceedsLut = pressureDeltaKPa > PressureRootLutMaxKPa;

            float t = math.min(1f, scaledIndex - lowerIndex);
            float root = math.lerp(s_pressureRootLut[lowerIndex], s_pressureRootLut[lowerIndex + 1], t);
            if (!exceedsLut)
                return root;

            float excessKPa = pressureDeltaKPa - PressureRootLutMaxKPa;
            return root + (excessKPa * PressureRootExcessLinearScale * math.rcp(math.max(1f, root)));
        }

        private static float ResolveHabitatGraphQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return SanitizeQualityWeight(quality);
        }

        private static float SanitizeQualityWeight(float globalQualityWeight)
        {
            return math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
        }

        private void ApplyWaterPumpDrainage(float deltaTime)
        {
            // SHINOBU_340: recursive object pump drainage is retired.
            // SumpPumpPipeGridRuntime consumes Vault pump DTOs and drains Fluid Incursion buffers.
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
                if (edge.Severed == 0)
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
            NativeArray<int> dfsStack,
            NativeArray<byte> dfsVisited,
            NativeArray<int> dfsResult,
            out byte rejectReason)
        {
            rejectReason = 0;
            if (targetModule == null || !TryResolveModuleNodeIndex(targetModule, out int removedNodeIndex))
            {
                rejectReason = 1;
                return false;
            }

            if (!TryAcquireFloodGraphJobBuffers(
                    math.max(1, _nodeCount),
                    math.max(1, _edgeCount),
                    out HabitatFloodGraphJobViews graph,
                    out IDataVault vault))
            {
                rejectReason = 3;
                return false;
            }

            try
            {
                if (HasDependentWindowCollapse(removedNodeIndex, ref graph))
                {
                    rejectReason = 2;
                    return false;
                }

                int nodeCount = math.min(_nodeCount, math.min(_moduleBuffer.Count, graph.EdgeOffsets.Length - 1));
                if (!dfsStack.IsCreated ||
                    !dfsVisited.IsCreated ||
                    !dfsResult.IsCreated ||
                    dfsStack.Length < nodeCount ||
                    dfsVisited.Length < nodeCount ||
                    dfsResult.Length < 3)
                {
                    rejectReason = 3;
                    return false;
                }

                if (nodeCount <= 2)
                {
                    int expectedCount = math.max(0, nodeCount - 1);
                    dfsResult[0] = 1;
                    dfsResult[1] = expectedCount;
                    dfsResult[2] = expectedCount;
                    return true;
                }

                DeconstructionDfsValidationJob job = new DeconstructionDfsValidationJob
                {
                    EdgeOffsets = graph.EdgeOffsets,
                    EdgeDestinations = graph.EdgeDestinations,
                    Stack = dfsStack,
                    Visited = dfsVisited,
                    Result = dfsResult,
                    NodeCount = nodeCount,
                    RemovedNodeIndex = removedNodeIndex,
                    EdgeCount = _edgeCount
                };

                job.Execute(); // COLD SYNC JOB: player-triggered deconstruction validation, not a per-frame path.
                if (dfsResult[0] != 1)
                {
                    rejectReason = 4;
                    return false;
                }

                return true;
            }
            finally
            {
                ReleaseFloodGraphWriteLocks(vault, 4);
            }
        }

        internal bool TryGetDeconstructionCsrLanes(
            BaseModule targetModule,
            out NativeArray<int> edgeOffsets,
            out NativeArray<int> edgeDestinations,
            out NativeArray<float> edgeStrength,
            out NativeArray<byte> edgeFlags,
            out int targetNodeIndex,
            out int nodeCount,
            out int edgeCount)
        {
            edgeOffsets = default;
            edgeDestinations = default;
            edgeStrength = default;
            edgeFlags = default;
            targetNodeIndex = -1;
            nodeCount = 0;
            edgeCount = 0;

            if (targetModule == null ||
                !TryResolveModuleNodeIndex(targetModule, out targetNodeIndex) ||
                !TryAcquireFloodGraphJobBuffers(
                    math.max(1, _nodeCount),
                    math.max(1, _edgeCount),
                    out HabitatFloodGraphJobViews graph,
                    out IDataVault vault))
            {
                return false;
            }

            edgeOffsets = graph.EdgeOffsets;
            edgeDestinations = graph.EdgeDestinations;
            edgeStrength = graph.EdgeResistance;
            edgeFlags = graph.EdgeFlags;
            nodeCount = math.min(_nodeCount, math.min(_moduleBuffer.Count, graph.EdgeOffsets.Length - 1));
            edgeCount = math.min(_edgeCount, math.min(graph.EdgeDestinations.Length, graph.EdgeResistance.Length));
            bool valid = targetNodeIndex >= 0 &&
                   targetNodeIndex < nodeCount &&
                   nodeCount > 0 &&
                   edgeCount >= 0;
            if (!valid)
            {
                ReleaseFloodGraphWriteLocks(vault, 4);
                return false;
            }

            ReleaseDeconstructionCsrLanes();
            _deconstructionGraphWriteLockHeld = true;
            _deconstructionGraphWriteLockVault = vault;
            return true;
        }

        internal int MarkDeconstructionEdgesSevered(int targetNodeIndex)
        {
            if (targetNodeIndex < 0 || targetNodeIndex >= _nodeCount)
                return 0;

            if (!TryAcquireGraphWriteBuffers(
                    _nodeCount,
                    math.max(1, _edgeCount),
                    out HabitatGraphWriteViews graph,
                    out IDataVault vault))
                return 0;

            try
            {
                int severedCount = 0;
                for (int edgeIndex = 0; edgeIndex < _edgeBuffer.Count; edgeIndex++)
                {
                    EdgeRecord edge = _edgeBuffer[edgeIndex];
                    if (edge.SourceIndex != targetNodeIndex && edge.DestinationIndex != targetNodeIndex)
                        continue;

                    if (edge.Severed == 0)
                    {
                        MarkEdgeRuptured(ref edge, ref graph);
                        severedCount++;
                    }
                    else
                    {
                        InvalidateRuntimeCsrEdge(edge.ForwardCsrIndex, ref graph);
                        if (edge.DirectedOnly == 0)
                            InvalidateRuntimeCsrEdge(edge.ReverseCsrIndex, ref graph);
                    }

                    _edgeBuffer[edgeIndex] = edge;
                }

                MarkNodeRuptured(targetNodeIndex, ref graph);
                return severedCount;
            }
            finally
            {
                ReleaseGraphWriteLocks(vault);
            }
        }

        private bool HasDependentWindowCollapse(int removedNodeIndex)
        {
            if (!TryAcquireFloodGraphJobBuffers(
                    math.max(1, _nodeCount),
                    math.max(1, _edgeCount),
                    out HabitatFloodGraphJobViews graph,
                    out IDataVault vault))
                return false;

            try
            {
                return HasDependentWindowCollapse(removedNodeIndex, ref graph);
            }
            finally
            {
                ReleaseFloodGraphWriteLocks(vault, 4);
            }
        }

        private bool HasDependentWindowCollapse(int removedNodeIndex, ref HabitatFloodGraphJobViews graph)
        {
            if (removedNodeIndex < 0 ||
                !graph.EdgeOffsets.IsCreated ||
                !graph.EdgeDestinations.IsCreated ||
                removedNodeIndex + 1 >= graph.EdgeOffsets.Length)
                return false;

            int edgeLimit = math.min(math.max(0, _edgeCount), graph.EdgeDestinations.Length);
            int edgeStart = math.clamp(graph.EdgeOffsets[removedNodeIndex], 0, edgeLimit);
            int edgeEnd = math.clamp(graph.EdgeOffsets[removedNodeIndex + 1], edgeStart, edgeLimit);
            for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
            {
                int destinationIndex = graph.EdgeDestinations[edgeIndex];
                if (!IsValidDeconstructionNode(destinationIndex) || !IsWindowModule(destinationIndex))
                    continue;

                if (CountLiveRoomConnectionsExcluding(destinationIndex, removedNodeIndex, ref graph) <= 0)
                    return true;
            }

            return false;
        }

        private int CountLiveRoomConnectionsExcluding(int nodeIndex, int removedNodeIndex)
        {
            if (!TryAcquireFloodGraphJobBuffers(
                    math.max(1, _nodeCount),
                    math.max(1, _edgeCount),
                    out HabitatFloodGraphJobViews graph,
                    out IDataVault vault))
                return 0;

            try
            {
                return CountLiveRoomConnectionsExcluding(nodeIndex, removedNodeIndex, ref graph);
            }
            finally
            {
                ReleaseFloodGraphWriteLocks(vault, 4);
            }
        }

        private int CountLiveRoomConnectionsExcluding(
            int nodeIndex,
            int removedNodeIndex,
            ref HabitatFloodGraphJobViews graph)
        {
            if (!graph.EdgeOffsets.IsCreated ||
                !graph.EdgeDestinations.IsCreated ||
                nodeIndex < 0 ||
                nodeIndex + 1 >= graph.EdgeOffsets.Length)
            {
                return 0;
            }

            int count = 0;
            int edgeLimit = math.min(math.max(0, _edgeCount), graph.EdgeDestinations.Length);
            int edgeStart = math.clamp(graph.EdgeOffsets[nodeIndex], 0, edgeLimit);
            int edgeEnd = math.clamp(graph.EdgeOffsets[nodeIndex + 1], edgeStart, edgeLimit);
            for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
            {
                int destinationIndex = graph.EdgeDestinations[edgeIndex];
                if (destinationIndex == removedNodeIndex || !IsValidDeconstructionNode(destinationIndex))
                    continue;

                count++;
            }

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
                if (edge.Severed != 0)
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
                if (edge.Severed != 0)
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

                float3 midpoint3 = (edge.StartSocketPosition + edge.EndSocketPosition) * 0.5f;
                Vector3 midpoint = new Vector3(midpoint3.x, midpoint3.y, midpoint3.z);
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
            if (!TryAcquireGraphWriteBuffers(
                    _nodeCount,
                    math.max(1, _edgeCount),
                    out HabitatGraphWriteViews graph,
                    out IDataVault vault))
                return;

            try
            {
                ApplyRuptureCascadeStressFromRupturedNodes(ref graph);
            }
            finally
            {
                ReleaseGraphWriteLocks(vault);
            }
        }

        private void ApplyRuptureCascadeStressFromRupturedNodes(ref HabitatGraphWriteViews graph)
        {
            if (_nodeCount <= 0 ||
                !graph.Nodes.IsCreated ||
                !graph.EdgeOffsets.IsCreated ||
                !graph.EdgeDestinations.IsCreated)
                return;

            int maxNodeCount = math.min(
                math.min(_nodeCount, _moduleBuffer.Count),
                math.min(graph.Nodes.Length, graph.EdgeOffsets.Length - 1));
            int edgeLimit = math.min(_edgeCount, graph.EdgeDestinations.Length);
            if (maxNodeCount <= 0 || edgeLimit <= 0)
                return;

            for (int nodeIndex = 0; nodeIndex < maxNodeCount; nodeIndex++)
            {
                LogisticsNodeFlags sourceFlags = graph.Nodes[nodeIndex].Flags;
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

                int sourceIslandId = ResolveNodeIslandId(nodeIndex, ref graph);
                int edgeStart = math.clamp(graph.EdgeOffsets[nodeIndex], 0, edgeLimit);
                int edgeEnd = math.clamp(graph.EdgeOffsets[nodeIndex + 1], edgeStart, edgeLimit);
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    int neighborNodeIndex = graph.EdgeDestinations[edgeIndex];
                    if (neighborNodeIndex < 0 || neighborNodeIndex >= maxNodeCount)
                        continue;

                    LogisticsNodeFlags neighborFlags = graph.Nodes[neighborNodeIndex].Flags;
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

            if (!TryReadHabitatVaultBuffer(
                    HabitatGraphNodesBufferId,
                    math.max(1, nodeIndex + 1),
                    in _nodesHandle,
                    out NativeArray<LogisticsNetworkGraph.LogisticsNode>.ReadOnly nodes))
                return 0;

            return nodeIndex < nodes.Length ? nodes[nodeIndex].NetworkId : 0;
        }

        private static int ResolveNodeIslandId(int nodeIndex, ref HabitatGraphWriteViews graph)
        {
            if (nodeIndex < 0 || !graph.Nodes.IsCreated || nodeIndex >= graph.Nodes.Length)
                return 0;

            return graph.Nodes[nodeIndex].NetworkId;
        }

        private void EnsureRuptureCascadeStateCapacity(int requiredCapacity)
        {
            int safeCapacity = NextPowerOfTwo(math.max(1, requiredCapacity));
            if (_ruptureCascadeAppliedNodeIds.Capacity >= safeCapacity)
                return;

            WriteFloodBlackBoxSample(FloodBlackBoxOverflowClampedFlag | FloodBlackBoxTopologyInvalidFlag);
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
            if (!TryAcquireGraphWriteBuffers(
                    _nodeCount,
                    math.max(1, _edgeCount),
                    out HabitatGraphWriteViews graph,
                    out IDataVault vault))
                return;

            try
            {
                PruneRuptureCascadeState(ref graph);
            }
            finally
            {
                ReleaseGraphWriteLocks(vault);
            }
        }

        private void PruneRuptureCascadeState(ref HabitatGraphWriteViews graph)
        {
            for (int i = _ruptureCascadeAppliedNodeIds.Count - 1; i >= 0; i--)
            {
                uint nodeId = _ruptureCascadeAppliedNodeIds[i];
                if (nodeId != 0u && IsRuptureCascadeSourceStillRuptured(nodeId, ref graph))
                    continue;

                int lastIndex = _ruptureCascadeAppliedNodeIds.Count - 1;
                _ruptureCascadeAppliedNodeIds[i] = _ruptureCascadeAppliedNodeIds[lastIndex];
                _ruptureCascadeAppliedNodeIds.RemoveAt(lastIndex);
            }
        }

        private bool IsRuptureCascadeSourceStillRuptured(uint nodeId)
        {
            if (!TryAcquireGraphWriteBuffers(
                    _nodeCount,
                    math.max(1, _edgeCount),
                    out HabitatGraphWriteViews graph,
                    out IDataVault vault))
                return false;

            try
            {
                return IsRuptureCascadeSourceStillRuptured(nodeId, ref graph);
            }
            finally
            {
                ReleaseGraphWriteLocks(vault);
            }
        }

        private bool IsRuptureCascadeSourceStillRuptured(uint nodeId, ref HabitatGraphWriteViews graph)
        {
            int moduleCount = math.min(_nodeCount, _moduleBuffer.Count);
            for (int moduleIndex = 0; moduleIndex < moduleCount; moduleIndex++)
            {
                ModuleRecord module = _moduleBuffer[moduleIndex];
                if (module.NodeId != nodeId)
                    continue;

                LogisticsNodeFlags nodeFlags = moduleIndex < graph.Nodes.Length ? graph.Nodes[moduleIndex].Flags : LogisticsNodeFlags.None;
                BaseModule baseModule = module.BaseModule;
                return (nodeFlags & LogisticsNodeFlags.Ruptured) != 0 ||
                       (baseModule != null && baseModule.IntegrityState == BaseModuleIntegrityState.Ruptured);
            }

            return false;
        }

        private void RuptureConnectedEdges(int nodeIndex)
        {
            if (!TryAcquireGraphWriteBuffers(
                    _nodeCount,
                    math.max(1, _edgeCount),
                    out HabitatGraphWriteViews graph,
                    out IDataVault vault))
                return;

            try
            {
                RuptureConnectedEdges(nodeIndex, ref graph);
            }
            finally
            {
                ReleaseGraphWriteLocks(vault);
            }
        }

        private void RuptureConnectedEdges(int nodeIndex, ref HabitatGraphWriteViews graph)
        {
            for (int edgeIndex = 0; edgeIndex < _edgeBuffer.Count; edgeIndex++)
            {
                EdgeRecord edge = _edgeBuffer[edgeIndex];
                if (edge.Severed != 0)
                    continue;

                if (edge.SourceIndex != nodeIndex && edge.DestinationIndex != nodeIndex)
                    continue;

                MarkEdgeRuptured(ref edge, ref graph);
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
                _nodeCount <= 0)
            {
                return false;
            }

            if (!TryAcquireGraphWriteBuffers(
                    _nodeCount,
                    math.max(1, _edgeCount),
                    out HabitatGraphWriteViews graph,
                    out IDataVault vault))
                return false;

            try
            {
                if (!graph.Nodes.IsCreated ||
                    !graph.EdgeOffsets.IsCreated ||
                    !graph.EdgeDestinations.IsCreated ||
                    !graph.TraversalVisited.IsCreated ||
                    !graph.AnchorTraversalQueue.IsCreated)
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
                    math.min(math.min(graph.Nodes.Length, graph.TraversalVisited.Length), graph.AnchorTraversalQueue.Length));
                if (startNodeIndex >= safeNodeCount || safeNodeCount <= 0)
                    return false;

                for (int nodeIndex = 0; nodeIndex < safeNodeCount; nodeIndex++)
                    graph.TraversalVisited[nodeIndex] = 0;

                bool traversalOverflowed = false;
                int queueHead = 0;
                int queueTail = 0;
                graph.TraversalVisited[startNodeIndex] = 1;
                graph.AnchorTraversalQueue[queueTail++] = startNodeIndex;

                float bestScore = 0f;
                float bestPotential = 0f;
                BaseModule bestModule = null;
                while (queueHead < queueTail)
                {
                    int currentNodeIndex = graph.AnchorTraversalQueue[queueHead++];
                    byte currentDepth = graph.TraversalVisited[currentNodeIndex];
                    ModuleRecord currentRecord = _moduleBuffer[currentNodeIndex];
                    if (currentNodeIndex != startNodeIndex)
                    {
                        BaseModule currentModule = currentRecord.BaseModule;
                        if (currentModule != null && currentModule.isActiveAndEnabled)
                        {
                            float rawPotential = ResolveFungalMindPotentialScore(currentRecord, graph.Nodes[currentNodeIndex]);
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

                    if (currentNodeIndex + 1 >= graph.EdgeOffsets.Length)
                        continue;

                    int edgeLimit = math.min(_edgeCount, graph.EdgeDestinations.Length);
                    int edgeStart = math.clamp(graph.EdgeOffsets[currentNodeIndex], 0, edgeLimit);
                    int edgeEnd = math.clamp(graph.EdgeOffsets[currentNodeIndex + 1], edgeStart, edgeLimit);
                    for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                    {
                        int neighborNodeIndex = graph.EdgeDestinations[edgeIndex];
                        if (neighborNodeIndex < 0 ||
                            neighborNodeIndex >= safeNodeCount ||
                            graph.TraversalVisited[neighborNodeIndex] != 0)
                        {
                            continue;
                        }

                        if (queueTail >= safeNodeCount)
                        {
                            traversalOverflowed = true;
                            break;
                        }

                        graph.TraversalVisited[neighborNodeIndex] = (byte)math.min(255, currentDepth + 1);
                        graph.AnchorTraversalQueue[queueTail++] = neighborNodeIndex;
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
            finally
            {
                ReleaseGraphWriteLocks(vault);
            }
        }

        private void PopulateModuleBuffer(IReadOnlyList<GameObject> modules)
        {
            int count = modules.Count;
            if (count > _moduleBuffer.Capacity)
                WriteFloodBlackBoxSample(FloodBlackBoxOverflowClampedFlag | FloodBlackBoxTopologyInvalidFlag);

            for (int i = 0; i < count && _moduleBuffer.Count < _moduleBuffer.Capacity; i++)
            {
                GameObject moduleObject = modules[i];
                if (moduleObject == null)
                    continue;

                ModuleMarker marker = moduleObject.TryGetComponent(out ModuleMarker resolvedMarker) ? resolvedMarker : null;
                BaseModule baseModule = moduleObject.TryGetComponent(out BaseModule resolvedBaseModule) ? resolvedBaseModule : null;
                TransitionHatchMeshState hatchMeshState = moduleObject.TryGetComponent(out TransitionHatchMeshState resolvedHatchMeshState) ? resolvedHatchMeshState : null;
                if (baseModule != null && baseModule.IsDetachedDebris)
                    continue;

                EntityId entityId = moduleObject.GetEntityId();
                uint nodeId = unchecked((uint)EntityId.ToULong(entityId));
                Vector3 modulePosition = moduleObject.transform.position;
                bool insertingNodeIndex = !_moduleIndexByNodeId.ContainsKey(nodeId);
                if (insertingNodeIndex && _moduleIndexByNodeId.Count >= _moduleIndexCapacity)
                {
                    WriteFloodBlackBoxSample(FloodBlackBoxOverflowClampedFlag | FloodBlackBoxTopologyInvalidFlag);
                    return;
                }

                _moduleBuffer.Add(new ModuleRecord
                {
                    ModuleObject = moduleObject,
                    Marker = marker,
                    BaseModule = baseModule,
                    HatchMeshState = hatchMeshState,
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

            sourceModuleHashId = CaptureTemporaryBypassModuleHashId(sourceModule, sourceModuleHashId);
            destinationModuleHashId = CaptureTemporaryBypassModuleHashId(destinationModule, destinationModuleHashId);
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

        private static int CaptureTemporaryBypassModuleHashId(GameObject module, int capturedModuleHashId)
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
            TryReadHabitatVaultBuffer(
                HabitatGraphEdgeOffsetsBufferId,
                math.max(1, _nodeCount + 1),
                in _edgeOffsetsHandle,
                out NativeArray<int>.ReadOnly edgeOffsets);
            TryReadHabitatVaultBuffer(
                HabitatGraphEdgeWriteCursorBufferId,
                math.max(1, _nodeCount),
                in _edgeWriteCursorHandle,
                out NativeArray<int>.ReadOnly edgeWriteCursor);

            if (_nodeCount <= 0 ||
                !edgeOffsets.IsCreated ||
                !edgeWriteCursor.IsCreated ||
                !_moduleIndexByNodeId.TryGetValue(sourceNodeId, out int sourceIndex) ||
                !_moduleIndexByNodeId.TryGetValue(destinationNodeId, out int destinationIndex) ||
                sourceIndex == destinationIndex ||
                sourceIndex < 0 ||
                destinationIndex < 0 ||
                sourceIndex >= _nodeCount ||
                destinationIndex >= _nodeCount ||
                sourceIndex >= edgeOffsets.Length - 1 ||
                destinationIndex >= edgeOffsets.Length - 1 ||
                _nodeCount > edgeWriteCursor.Length ||
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
                Severed = 0,
                DirectedOnly = 1
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
                    Severed = 0,
                    DirectedOnly = 1
                });
            }
        }

        private void BuildSocketAdjacency()
        {
            int quantizationScale = math.max(1, (int)math.round(1f / DefaultSocketQuantization));
            IDataVault catalogVault = ResolveHabitatDataVaultForColdPath();
            for (int moduleIndex = 0; moduleIndex < _moduleBuffer.Count; moduleIndex++)
                IndexSockets(moduleIndex, _moduleBuffer[moduleIndex], quantizationScale, catalogVault);
        }

        private void IndexSockets(int moduleIndex, ModuleRecord module, int quantizationScale, IDataVault catalogVault)
        {
            BuildableData data = module.Marker != null ? module.Marker.Data : null;
            BaseModuleTemplate template = data != null ? data.ModuleTemplate : null;
            if (template == null)
                return;

            Vector3 rootPosition = module.Position;
            Quaternion rootRotation = module.ModuleObject != null ? module.ModuleObject.transform.rotation : Quaternion.identity;
            uint prefabHash = unchecked((uint)template.TemplateHashId);
            if (BaseModuleCatalogRuntime.TryGetModuleSocketRangeFromVault(
                    catalogVault,
                    prefabHash,
                    out NativeArray<SocketDefinitionDTO>.ReadOnly catalogSockets,
                    out int socketStart,
                    out int socketCount,
                    out _))
            {
                IndexSocketRange(moduleIndex, rootPosition, rootRotation, catalogSockets, socketStart, socketCount, quantizationScale);
                return;
            }

            if (Application.isPlaying)
                return;

            BaseModuleTemplate.SocketDefinition[] definitions = template.SocketDefinitions;
            if (definitions == null || definitions.Length == 0)
                return;

            for (int i = 0; i < definitions.Length; i++)
            {
                if (!BaseModuleCatalogRuntime.TryBuildSocketFromTemplate(template, i, out SocketDefinitionDTO socket))
                    continue;

                IndexSocket(moduleIndex, rootPosition, rootRotation, socket, quantizationScale);
            }
        }

        private void IndexSocketRange(int moduleIndex, Vector3 rootPosition, Quaternion rootRotation, NativeArray<SocketDefinitionDTO>.ReadOnly sockets, int socketStart, int socketCount, int quantizationScale)
        {
            int end = math.min(socketStart + socketCount, sockets.Length);
            for (int i = socketStart; i < end; i++)
                IndexSocket(moduleIndex, rootPosition, rootRotation, sockets[i], quantizationScale);
        }

        private void IndexSocket(int moduleIndex, Vector3 rootPosition, Quaternion rootRotation, in SocketDefinitionDTO socket, int quantizationScale)
        {
            if (!TryResolveSocketPose(rootPosition, rootRotation, in socket, out double3 socketAup, out Vector3 socketPosition, out Vector3 socketForward))
                return;

            int axis = QuantizeAxis(socketForward);
            SocketKey oppositeKey = SocketKey.Create(socketAup, OppositeAxis(axis), quantizationScale);

            if (_socketLookup.TryGetValue(oppositeKey, out SocketMatchEntry existing))
            {
                if (existing.ModuleIndex != moduleIndex &&
                    BaseModuleCatalogRuntime.AreSocketMasksCompatible(existing.CompatibilityMask, socket.AllowedConnectionsMask) &&
                    Vector3.Dot(existing.Forward, socketForward) <= OppositeDirectionDotThreshold)
                {
                    if (_edgeBuffer.Count >= _edgeBuffer.Capacity)
                    {
                        WriteFloodBlackBoxSample(FloodBlackBoxOverflowClampedFlag | FloodBlackBoxTopologyInvalidFlag);
                        return;
                    }

                    _edgeBuffer.Add(new EdgeRecord
                    {
                        SourceIndex = existing.ModuleIndex,
                        DestinationIndex = moduleIndex,
                        StartSocketPosition = existing.Position,
                        EndSocketPosition = socketPosition,
                        StartForward = existing.Forward,
                        EndForward = socketForward,
                        Flags = PipeRenderFlags.None
                    });
                }

                return;
            }

            SocketKey ownKey = SocketKey.Create(socketAup, axis, quantizationScale);
            if (!_socketLookup.ContainsKey(ownKey) && _socketLookup.Count >= _socketLookupCapacity)
            {
                WriteFloodBlackBoxSample(FloodBlackBoxOverflowClampedFlag | FloodBlackBoxTopologyInvalidFlag);
                return;
            }

            _socketLookup[ownKey] = new SocketMatchEntry(moduleIndex, socket.AllowedConnectionsMask, socketPosition, socketForward);
        }

        private static bool TryResolveSocketPose(
            Vector3 rootPosition,
            Quaternion rootRotation,
            in SocketDefinitionDTO socket,
            out double3 socketAup,
            out Vector3 runtimePosition,
            out Vector3 socketForward)
        {
            socketAup = default;
            runtimePosition = Vector3.zero;
            socketForward = Vector3.forward;

            quaternion rotation = new quaternion(rootRotation.x, rootRotation.y, rootRotation.z, rootRotation.w);
            float3 worldNormal = math.rotate(rotation, socket.Normal);
            if (!math.all(math.isfinite(socket.LocalOffset)) || !math.all(math.isfinite(worldNormal)))
                return false;

            if (!TryResolveAbsoluteFromRuntimeOrigin(rootPosition, out double3 rootAup, out double3 originAup))
                return false;

            socketAup = BaseModuleCatalogRuntime.ResolveSocketAup(rootAup, rotation, in socket);
            if (!math.all(math.isfinite(socketAup)))
                return false;

            double3 localDelta = socketAup - originAup;
            if (!math.all(math.isfinite(localDelta)) ||
                math.any(math.abs(localDelta) > (double)float.MaxValue))
            {
                return false;
            }

            runtimePosition = new Vector3((float)localDelta.x, (float)localDelta.y, (float)localDelta.z);
            socketForward = new Vector3(worldNormal.x, worldNormal.y, worldNormal.z);
            return true;
        }

        private static bool TryResolveAbsoluteFromRuntimeOrigin(Vector3 runtimePosition, out double3 absolutePosition, out double3 originAupDouble)
        {
            absolutePosition = default;
            originAupDouble = default;
            float3 localRuntime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(localRuntime)))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            originAupDouble = originAup.ToAbsoluteDouble3();
            if (!math.all(math.isfinite(originAupDouble)))
                return false;

            absolutePosition = AbsoluteUniversePosition.OffsetAbsoluteMeters(
                in originAup,
                new double3(localRuntime.x, localRuntime.y, localRuntime.z));
            return math.all(math.isfinite(absolutePosition));
        }

        private void BuildNodeRecords()
        {
            int directedEdgeCapacity = math.max(1, _edgeBuffer.Count * 2);
            if (!TryAcquireGraphWriteBuffers(
                    _nodeCount,
                    directedEdgeCapacity,
                    out HabitatGraphWriteViews graph,
                    out IDataVault vault))
                return;

            try
            {
                BuildNodeRecords(ref graph);
            }
            finally
            {
                ReleaseGraphWriteLocks(vault);
            }
        }

        private void BuildNodeRecords(ref HabitatGraphWriteViews graph)
        {
            if (!graph.Nodes.IsCreated)
                return;

            int maxNodeCount = math.min(math.max(0, _nodeCount), math.min(_moduleBuffer.Count, graph.Nodes.Length));
            for (int nodeIndex = 0; nodeIndex < maxNodeCount; nodeIndex++)
            {
                ModuleRecord module = _moduleBuffer[nodeIndex];
                graph.Nodes[nodeIndex] = new LogisticsNetworkGraph.LogisticsNode
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
            if (!TryAcquireGraphWriteBuffers(
                    _nodeCount,
                    reservedDirectedEdgeCapacity,
                    out HabitatGraphWriteViews graph,
                    out IDataVault vault))
            {
                _edgeCount = 0;
                return;
            }

            try
            {
                BuildEdgeRecords(ref graph, reservedDirectedEdgeCapacity);
            }
            finally
            {
                ReleaseGraphWriteLocks(vault);
            }
        }

        private void BuildEdgeRecords(ref HabitatGraphWriteViews graph, int reservedDirectedEdgeCapacity)
        {
            ResetFloodConnectionState(ref graph, reservedDirectedEdgeCapacity);
            if (!graph.EdgeOffsets.IsCreated ||
                !graph.EdgeWriteCursor.IsCreated ||
                graph.EdgeOffsets.Length <= 0 ||
                _nodeCount <= 0)
            {
                _edgeCount = 0;
                return;
            }

            int safeOffsetNodeCount = math.max(0, math.min(_nodeCount, graph.EdgeOffsets.Length - 1));
            int safeWriteNodeCount = math.min(safeOffsetNodeCount, graph.EdgeWriteCursor.Length);
            int logicalDirectedEdgeCount = 0;
            float unsupportedSpanMeters = LogisticsPipeBuilder.UnsupportedSpanMeters;
            float unsupportedSpanSq = unsupportedSpanMeters * unsupportedSpanMeters;

            for (int nodeIndex = 0; nodeIndex <= safeOffsetNodeCount; nodeIndex++)
                graph.EdgeOffsets[nodeIndex] = 0;

            for (int edgeIndex = 0; edgeIndex < _edgeBuffer.Count; edgeIndex++)
            {
                EdgeRecord edge = _edgeBuffer[edgeIndex];
                edge.ForwardCsrIndex = -1;
                edge.ReverseCsrIndex = -1;
                if (!IsValidEdgeEndpoint(ref graph, edge.SourceIndex) ||
                    !IsValidEdgeEndpoint(ref graph, edge.DestinationIndex))
                {
                    edge.Severed = 1;
                    _edgeBuffer[edgeIndex] = edge;
                    continue;
                }

                float3 socketDelta = edge.EndSocketPosition - edge.StartSocketPosition;
                float distanceSq = math.lengthsq(socketDelta);
                bool unsupported = distanceSq > unsupportedSpanSq &&
                                   !HasIntermediateSupport(edge.SourceIndex, edge.DestinationIndex, edge.StartSocketPosition, edge.EndSocketPosition);

                if (unsupported || HasImplodedEndpoint(edge))
                    MarkEdgeRuptured(ref edge, ref graph);

                if (edge.Severed == 0 && TryApplyHydroShearRupture(ref edge))
                    MarkEdgeRuptured(ref edge, ref graph);

                edge.Resistance = edge.Severed != 0
                    ? 0f
                    : math.max(MinimumEdgeResistance, ResolveFastLengthFromSq(distanceSq) * EdgeResistancePerMeter);
                _edgeBuffer[edgeIndex] = edge;

                if (edge.Severed != 0)
                    continue;

                graph.EdgeOffsets[edge.SourceIndex + 1] = graph.EdgeOffsets[edge.SourceIndex + 1] + 1;
                if (edge.DirectedOnly != 0)
                {
                    logicalDirectedEdgeCount++;
                }
                else
                {
                    graph.EdgeOffsets[edge.DestinationIndex + 1] = graph.EdgeOffsets[edge.DestinationIndex + 1] + 1;
                    logicalDirectedEdgeCount += 2;
                }
            }

            for (int nodeIndex = 1; nodeIndex <= safeOffsetNodeCount; nodeIndex++)
                graph.EdgeOffsets[nodeIndex] = graph.EdgeOffsets[nodeIndex] + graph.EdgeOffsets[nodeIndex - 1];

            for (int nodeIndex = 0; nodeIndex < safeWriteNodeCount; nodeIndex++)
                graph.EdgeWriteCursor[nodeIndex] = graph.EdgeOffsets[nodeIndex];

            int writtenDirectedEdgeCount = 0;
            for (int edgeIndex = 0; edgeIndex < _edgeBuffer.Count; edgeIndex++)
            {
                EdgeRecord edge = _edgeBuffer[edgeIndex];
                if (edge.Severed != 0 ||
                    !IsValidEdgeEndpoint(ref graph, edge.SourceIndex) ||
                    !IsValidEdgeEndpoint(ref graph, edge.DestinationIndex))
                {
                    edge.ForwardCsrIndex = -1;
                    edge.ReverseCsrIndex = -1;
                    _edgeBuffer[edgeIndex] = edge;
                    continue;
                }

                int forwardWriteIndex = graph.EdgeWriteCursor[edge.SourceIndex];
                if (!IsValidCsrWriteIndex(ref graph, forwardWriteIndex))
                {
                    edge.ForwardCsrIndex = -1;
                    edge.ReverseCsrIndex = -1;
                    _edgeBuffer[edgeIndex] = edge;
                    continue;
                }

                graph.EdgeWriteCursor[edge.SourceIndex] = forwardWriteIndex + 1;
                graph.EdgeDestinations[forwardWriteIndex] = edge.DestinationIndex;
                graph.EdgeResistance[forwardWriteIndex] = edge.Resistance;
                edge.ForwardCsrIndex = forwardWriteIndex;
                writtenDirectedEdgeCount = math.max(writtenDirectedEdgeCount, forwardWriteIndex + 1);

                if (edge.DirectedOnly != 0)
                {
                    _edgeBuffer[edgeIndex] = edge;
                    continue;
                }

                int reverseWriteIndex = graph.EdgeWriteCursor[edge.DestinationIndex];
                if (!IsValidCsrWriteIndex(ref graph, reverseWriteIndex))
                {
                    edge.ReverseCsrIndex = -1;
                    _edgeBuffer[edgeIndex] = edge;
                    continue;
                }

                graph.EdgeWriteCursor[edge.DestinationIndex] = reverseWriteIndex + 1;
                graph.EdgeDestinations[reverseWriteIndex] = edge.SourceIndex;
                graph.EdgeResistance[reverseWriteIndex] = edge.Resistance;
                edge.ReverseCsrIndex = reverseWriteIndex;
                writtenDirectedEdgeCount = math.max(writtenDirectedEdgeCount, reverseWriteIndex + 1);
                _edgeBuffer[edgeIndex] = edge;
            }

            _edgeCount = math.min(logicalDirectedEdgeCount, writtenDirectedEdgeCount);
        }

        private bool IsValidEdgeEndpoint(int nodeIndex)
        {
            if (!TryAcquireGraphWriteBuffers(
                    _nodeCount,
                    math.max(1, _edgeCount),
                    out HabitatGraphWriteViews graph,
                    out IDataVault vault))
                return false;

            try
            {
                return IsValidEdgeEndpoint(ref graph, nodeIndex);
            }
            finally
            {
                ReleaseGraphWriteLocks(vault);
            }
        }

        private bool IsValidEdgeEndpoint(ref HabitatGraphWriteViews graph, int nodeIndex)
        {
            return graph.EdgeOffsets.IsCreated &&
                   graph.EdgeWriteCursor.IsCreated &&
                   nodeIndex >= 0 &&
                   nodeIndex < _nodeCount &&
                   nodeIndex < _moduleBuffer.Count &&
                   nodeIndex + 1 < graph.EdgeOffsets.Length &&
                   nodeIndex < graph.EdgeWriteCursor.Length;
        }

        private bool IsValidCsrWriteIndex(int edgeIndex)
        {
            if (!TryAcquireGraphWriteBuffers(
                    _nodeCount,
                    math.max(1, _edgeCount),
                    out HabitatGraphWriteViews graph,
                    out IDataVault vault))
                return false;

            try
            {
                return IsValidCsrWriteIndex(ref graph, edgeIndex);
            }
            finally
            {
                ReleaseGraphWriteLocks(vault);
            }
        }

        private static bool IsValidCsrWriteIndex(ref HabitatGraphWriteViews graph, int edgeIndex)
        {
            return graph.EdgeDestinations.IsCreated &&
                   graph.EdgeResistance.IsCreated &&
                   edgeIndex >= 0 &&
                   edgeIndex < graph.EdgeDestinations.Length &&
                   edgeIndex < graph.EdgeResistance.Length;
        }

        private void ResetFloodConnectionState(int directedEdgeCapacity)
        {
            if (!TryAcquireGraphWriteBuffers(
                    _nodeCount,
                    math.max(1, directedEdgeCapacity),
                    out HabitatGraphWriteViews graph,
                    out IDataVault vault))
                return;

            try
            {
                ResetFloodConnectionState(ref graph, directedEdgeCapacity);
            }
            finally
            {
                ReleaseGraphWriteLocks(vault);
            }
        }

        private static void ResetFloodConnectionState(ref HabitatGraphWriteViews graph, int directedEdgeCapacity)
        {
            if (graph.EdgeFlags.IsCreated)
            {
                int clearCount = math.min(math.max(0, directedEdgeCapacity), graph.EdgeFlags.Length);
                for (int edgeIndex = 0; edgeIndex < clearCount; edgeIndex++)
                    graph.EdgeFlags[edgeIndex] = 0;
            }

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
            if (!TryAcquireGraphWriteBuffers(
                    _nodeCount,
                    math.max(1, _edgeCount),
                    out HabitatGraphWriteViews graph,
                    out IDataVault vault))
                return;

            try
            {
                MarkEdgeRuptured(ref edge, ref graph);
            }
            finally
            {
                ReleaseGraphWriteLocks(vault);
            }
        }

        private void MarkEdgeRuptured(ref EdgeRecord edge, ref HabitatGraphWriteViews graph)
        {
            InvalidateRuntimeCsrEdge(edge.ForwardCsrIndex, ref graph);
            if (edge.DirectedOnly == 0)
                InvalidateRuntimeCsrEdge(edge.ReverseCsrIndex, ref graph);

            edge.Flags |= PipeRenderFlags.MaskRuptured;
            edge.Severed = 1;
            edge.ForwardCsrIndex = -1;
            edge.ReverseCsrIndex = -1;
            RegisterSeveredEdgeRuptureVfx(in edge);
        }

        private void InvalidateRuntimeCsrEdge(int csrIndex)
        {
            if (!TryAcquireGraphWriteBuffers(
                    _nodeCount,
                    math.max(1, _edgeCount),
                    out HabitatGraphWriteViews graph,
                    out IDataVault vault))
                return;

            try
            {
                InvalidateRuntimeCsrEdge(csrIndex, ref graph);
            }
            finally
            {
                ReleaseGraphWriteLocks(vault);
            }
        }

        private static void InvalidateRuntimeCsrEdge(int csrIndex, ref HabitatGraphWriteViews graph)
        {
            if (csrIndex < 0 || !graph.EdgeDestinations.IsCreated || csrIndex >= graph.EdgeDestinations.Length)
                return;

            graph.EdgeDestinations[csrIndex] = -1;
            if (graph.EdgeResistance.IsCreated && csrIndex < graph.EdgeResistance.Length)
                graph.EdgeResistance[csrIndex] = 0f;
        }

        private void MarkNodeRuptured(int nodeIndex)
        {
            if (!TryAcquireGraphWriteBuffers(
                    _nodeCount,
                    math.max(1, _edgeCount),
                    out HabitatGraphWriteViews graph,
                    out IDataVault vault))
                return;

            try
            {
                MarkNodeRuptured(nodeIndex, ref graph);
            }
            finally
            {
                ReleaseGraphWriteLocks(vault);
            }
        }

        private void MarkNodeRuptured(int nodeIndex, ref HabitatGraphWriteViews graph)
        {
            if (nodeIndex < 0 || nodeIndex >= _nodeCount || nodeIndex >= graph.Nodes.Length)
                return;

            LogisticsNetworkGraph.LogisticsNode node = graph.Nodes[nodeIndex];
            node.Flags |= LogisticsNodeFlags.Ruptured;
            graph.Nodes[nodeIndex] = node;

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

            IFluidDecalPresentationSink fluidDecals = GetCachedFluidDecalPresentation();
            if (fluidDecals == null || _emittedRuptureEdgeVfxKeys.Count >= _emittedRuptureEdgeVfxKeys.Capacity)
                return;

            float3 midpoint3 = (edge.StartSocketPosition + edge.EndSocketPosition) * 0.5f;
            Vector3 midpointRuntime = new Vector3(midpoint3.x, midpoint3.y, midpoint3.z);
            float3 spanDelta = edge.EndSocketPosition - edge.StartSocketPosition;
            float spanSq = math.lengthsq(spanDelta);
            float unsupportedSpanMeters = LogisticsPipeBuilder.UnsupportedSpanMeters;
            float unsupportedSpanSq = unsupportedSpanMeters * unsupportedSpanMeters;
            float radiusScale = math.lerp(0.65f, 1.2f, math.saturate(spanSq * math.rcp(math.max(0.0001f, unsupportedSpanSq))));
            fluidDecals.RegisterRuptureFluid(midpointRuntime, radiusScale);
            _emittedRuptureEdgeVfxKeys.Add(linkId);
            _emittedRuptureEdgeVfxLookup.Add(linkId);
        }

        private IFluidDecalPresentationSink GetCachedFluidDecalPresentation()
        {
            return _fluidDecals;
        }

        private void EvaluateAnchorReachability()
        {
            if (!TryAcquireGraphWriteBuffers(
                    _nodeCount,
                    math.max(1, _edgeCount),
                    out HabitatGraphWriteViews graph,
                    out IDataVault vault))
                return;

            try
            {
                EvaluateAnchorReachability(ref graph);
            }
            finally
            {
                ReleaseGraphWriteLocks(vault);
            }
        }

        private void EvaluateAnchorReachability(ref HabitatGraphWriteViews graph)
        {
            if (_nodeCount <= 0 ||
                !graph.Nodes.IsCreated ||
                !graph.EdgeOffsets.IsCreated ||
                !graph.EdgeDestinations.IsCreated ||
                !graph.AnchorReachability.IsCreated ||
                !graph.AnchorTraversalQueue.IsCreated)
                return;

            int safeNodeCount = math.min(
                math.min(_nodeCount, _moduleBuffer.Count),
                math.min(math.min(graph.Nodes.Length, graph.AnchorReachability.Length), graph.AnchorTraversalQueue.Length));
            if (safeNodeCount <= 0)
                return;

            for (int nodeIndex = 0; nodeIndex < safeNodeCount; nodeIndex++)
            {
                graph.AnchorReachability[nodeIndex] = 0;
                LogisticsNetworkGraph.LogisticsNode node = graph.Nodes[nodeIndex];
                node.Flags &= ~LogisticsNodeFlags.Isolated;
                graph.Nodes[nodeIndex] = node;
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

                graph.AnchorReachability[nodeIndex] = 1;
                graph.AnchorTraversalQueue[queueTail++] = nodeIndex;
            }

            while (queueHead < queueTail)
            {
                int currentNodeIndex = graph.AnchorTraversalQueue[queueHead++];
                if (currentNodeIndex + 1 >= graph.EdgeOffsets.Length)
                    continue;

                int edgeLimit = math.min(_edgeCount, graph.EdgeDestinations.Length);
                int edgeStart = math.clamp(graph.EdgeOffsets[currentNodeIndex], 0, edgeLimit);
                int edgeEnd = math.clamp(graph.EdgeOffsets[currentNodeIndex + 1], edgeStart, edgeLimit);
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    int neighborNodeIndex = graph.EdgeDestinations[edgeIndex];
                    if (neighborNodeIndex < 0 || neighborNodeIndex >= safeNodeCount)
                        continue;

                    if (graph.AnchorReachability[neighborNodeIndex] != 0)
                        continue;

                    if (queueTail >= safeNodeCount)
                    {
                        traversalOverflowed = true;
                        break;
                    }

                    graph.AnchorReachability[neighborNodeIndex] = 1;
                    graph.AnchorTraversalQueue[queueTail++] = neighborNodeIndex;
                }
            }

            if (traversalOverflowed)
                WriteFloodBlackBoxSample(FloodBlackBoxTraversalOverflowFlag);

            for (int nodeIndex = 0; nodeIndex < safeNodeCount; nodeIndex++)
            {
                bool anchored = graph.AnchorReachability[nodeIndex] != 0;
                LogisticsNetworkGraph.LogisticsNode node = graph.Nodes[nodeIndex];
                if (!anchored)
                    node.Flags |= LogisticsNodeFlags.Isolated;

                node.Reserved = (byte)ResolveReservedState(
                    _moduleBuffer[nodeIndex].BaseModule,
                    _moduleBuffer[nodeIndex].IsAnchorNode,
                    anchored,
                    false);
                graph.Nodes[nodeIndex] = node;
            }
        }

        private void PublishAnchorState()
        {
            if (!TryAcquireGraphWriteBuffers(
                    _nodeCount,
                    math.max(1, _edgeCount),
                    out HabitatGraphWriteViews graph,
                    out IDataVault vault))
                return;

            try
            {
                PublishAnchorState(ref graph);
            }
            finally
            {
                ReleaseGraphWriteLocks(vault);
            }
        }

        private void PublishAnchorState(ref HabitatGraphWriteViews graph)
        {
            int safeNodeCount = graph.AnchorReachability.IsCreated
                ? math.min(_nodeCount, math.min(_moduleBuffer.Count, graph.AnchorReachability.Length))
                : 0;
            for (int nodeIndex = 0; nodeIndex < safeNodeCount; nodeIndex++)
            {
                BaseModule baseModule = _moduleBuffer[nodeIndex].BaseModule;
                if (baseModule != null)
                    baseModule.SetAnchoredState(graph.AnchorReachability[nodeIndex] != 0);
            }
        }

        private void PublishComponentPowerState()
        {
            if (!TryAcquireGraphWriteBuffers(
                    _nodeCount,
                    math.max(1, _edgeCount),
                    out HabitatGraphWriteViews graph,
                    out IDataVault vault))
                return;

            try
            {
                PublishComponentPowerState(ref graph);
            }
            finally
            {
                ReleaseGraphWriteLocks(vault);
            }
        }

        private void PublishComponentPowerState(ref HabitatGraphWriteViews graph)
        {
            if (_nodeCount <= 0 ||
                !graph.Nodes.IsCreated ||
                !graph.EdgeOffsets.IsCreated ||
                !graph.EdgeDestinations.IsCreated ||
                !graph.TraversalVisited.IsCreated ||
                !graph.AnchorTraversalQueue.IsCreated)
                return;

            int safeNodeCount = math.min(
                math.min(_nodeCount, _moduleBuffer.Count),
                math.min(math.min(graph.Nodes.Length, graph.TraversalVisited.Length), graph.AnchorTraversalQueue.Length));
            if (safeNodeCount <= 0)
                return;

            for (int nodeIndex = 0; nodeIndex < safeNodeCount; nodeIndex++)
                graph.TraversalVisited[nodeIndex] = 0;

            int componentIslandOrdinal = 0;
            bool traversalOverflowed = false;
            for (int startNodeIndex = 0; startNodeIndex < safeNodeCount; startNodeIndex++)
            {
                if (graph.TraversalVisited[startNodeIndex] != 0)
                    continue;

                int queueHead = 0;
                int queueTail = 0;
                graph.TraversalVisited[startNodeIndex] = 1;
                graph.AnchorTraversalQueue[queueTail++] = startNodeIndex;

                float componentSupply = 0f;
                float componentDraw = 0f;

                while (queueHead < queueTail)
                {
                    int currentNodeIndex = graph.AnchorTraversalQueue[queueHead++];
                    float powerRating = ResolveModulePowerRating(_moduleBuffer[currentNodeIndex]);
                    if (powerRating >= 0f)
                        componentSupply += powerRating;
                    else
                        componentDraw -= powerRating;

                    if (currentNodeIndex + 1 >= graph.EdgeOffsets.Length)
                        continue;

                    int edgeLimit = math.min(_edgeCount, graph.EdgeDestinations.Length);
                    int edgeStart = math.clamp(graph.EdgeOffsets[currentNodeIndex], 0, edgeLimit);
                    int edgeEnd = math.clamp(graph.EdgeOffsets[currentNodeIndex + 1], edgeStart, edgeLimit);
                    for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                    {
                        int neighborNodeIndex = graph.EdgeDestinations[edgeIndex];
                        if (neighborNodeIndex < 0 || neighborNodeIndex >= safeNodeCount)
                            continue;

                        if (graph.TraversalVisited[neighborNodeIndex] != 0)
                            continue;

                        if (queueTail >= safeNodeCount)
                        {
                            traversalOverflowed = true;
                            break;
                        }

                        graph.TraversalVisited[neighborNodeIndex] = 1;
                        graph.AnchorTraversalQueue[queueTail++] = neighborNodeIndex;
                    }
                }

                bool componentLowPower = componentDraw > componentSupply + 0.001f &&
                                         PowerGridManager.ResolveProjectedBrownoutTier(componentSupply, componentDraw) != LogisticsBrownoutTier.None;
                byte componentIslandId = (byte)math.min(componentIslandOrdinal, byte.MaxValue);
                for (int queueIndex = 0; queueIndex < queueTail; queueIndex++)
                {
                    int componentNodeIndex = graph.AnchorTraversalQueue[queueIndex];
                    LogisticsNetworkGraph.LogisticsNode node = graph.Nodes[componentNodeIndex];
                    node.NetworkId = componentIslandId;
                    if (componentLowPower)
                        node.Flags |= LogisticsNodeFlags.Brownout;
                    else
                        node.Flags &= ~LogisticsNodeFlags.Brownout;

                    graph.Nodes[componentNodeIndex] = node;

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
            if (!TryAcquireGraphWriteBuffers(
                    _nodeCount,
                    math.max(1, _edgeCount),
                    out HabitatGraphWriteViews graph,
                    out IDataVault vault))
                return;

            try
            {
                PublishEmergencyLockdownState(ref graph);
            }
            finally
            {
                ReleaseGraphWriteLocks(vault);
            }
        }

        private void PublishEmergencyLockdownState(ref HabitatGraphWriteViews graph)
        {
            if (_nodeCount <= 0 || !graph.Nodes.IsCreated)
                return;

            int maxNodeCount = math.min(_nodeCount, _moduleBuffer.Count);
            maxNodeCount = math.min(maxNodeCount, graph.Nodes.Length);
            if (graph.EdgeOffsets.IsCreated)
                maxNodeCount = math.min(maxNodeCount, graph.EdgeOffsets.Length - 1);

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
                int edgeLimit = math.min(_edgeCount, graph.EdgeDestinations.IsCreated ? graph.EdgeDestinations.Length : 0);
                int edgeStart = 0;
                int edgeEnd = 0;
                if (graph.EdgeOffsets.IsCreated && nodeIndex + 1 < graph.EdgeOffsets.Length)
                {
                    edgeStart = math.clamp(graph.EdgeOffsets[nodeIndex], 0, edgeLimit);
                    edgeEnd = math.clamp(graph.EdgeOffsets[nodeIndex + 1], edgeStart, edgeLimit);
                }

                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    int adjacentNodeIndex = graph.EdgeDestinations[edgeIndex];
                    if (adjacentNodeIndex < 0 || adjacentNodeIndex >= maxNodeCount)
                        continue;

                    hasAdjacent = true;
                    LogisticsNodeFlags adjacentFlags = graph.Nodes[adjacentNodeIndex].Flags;
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
                TransitionHatchMeshState hatchMeshState = module.HatchMeshState;
                if (hatchMeshState != null)
                {
                    hatchMeshState.ApplyAdjacentFlags(TransitionHatchMeshState.BuildAdjacentFlags(
                        hasAdjacent,
                        adjacentFloodedForHatch,
                        adjacentRupturedForHatch,
                        shouldLock));
                }

                LogisticsNetworkGraph.LogisticsNode node = graph.Nodes[nodeIndex];
                bool anchorReachable = graph.AnchorReachability.IsCreated &&
                                       nodeIndex < graph.AnchorReachability.Length &&
                                       graph.AnchorReachability[nodeIndex] != 0;
                node.Reserved = (byte)ResolveReservedState(
                    baseModule,
                    module.IsAnchorNode,
                    anchorReachable,
                    shouldLock);
                graph.Nodes[nodeIndex] = node;
            }

            PublishFloodEdgeFlags(ref graph);
        }

        private void PublishFloodEdgeFlags()
        {
            if (!TryAcquireGraphWriteBuffers(
                    _nodeCount,
                    math.max(1, _edgeCount),
                    out HabitatGraphWriteViews graph,
                    out IDataVault vault))
                return;

            try
            {
                PublishFloodEdgeFlags(ref graph);
            }
            finally
            {
                ReleaseGraphWriteLocks(vault);
            }
        }

        private void PublishFloodEdgeFlags(ref HabitatGraphWriteViews graph)
        {
            ClearActiveFloodEdgeFlags(ref graph);

            if (!graph.EdgeOffsets.IsCreated ||
                !graph.EdgeDestinations.IsCreated ||
                !graph.Nodes.IsCreated ||
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
                if (nodeIndex + 1 >= graph.EdgeOffsets.Length)
                    break;

                int edgeLimit = math.min(_edgeCount, graph.EdgeDestinations.Length);
                int edgeStart = math.clamp(graph.EdgeOffsets[nodeIndex], 0, edgeLimit);
                int edgeEnd = math.clamp(graph.EdgeOffsets[nodeIndex + 1], edgeStart, edgeLimit);
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    int adjacentNodeIndex = graph.EdgeDestinations[edgeIndex];
                    if (adjacentNodeIndex < 0 || adjacentNodeIndex >= maxNodeCount)
                        continue;

                    BaseModule adjacentModule = _moduleBuffer[adjacentNodeIndex].BaseModule;
                    LogisticsNodeFlags adjacentFlags = graph.Nodes[adjacentNodeIndex].Flags;
                    bool adjacentRuptured = (adjacentFlags & LogisticsNodeFlags.Ruptured) != 0 ||
                                            (adjacentModule != null && adjacentModule.IntegrityState == BaseModuleIntegrityState.Ruptured);

                    if (moduleAutoSealActive ||
                        IsFloodAutoSealActive(adjacentNodeIndex, adjacentModule) ||
                        moduleLocked ||
                        (adjacentModule != null && adjacentModule.IsEmergencyBulkheadLockedDown))
                    {
                        SetFloodEdgeFlag(ref graph, edgeIndex, HabitatEdgeFloodFlags.Sealed);
                    }

                    if (adjacentRuptured)
                        SetFloodEdgeFlag(ref graph, edgeIndex, HabitatEdgeFloodFlags.Ruptured);
                }
            }
        }

        private void PublishDegradationState()
        {
            if (!TryAcquireGraphWriteBuffers(
                    _nodeCount,
                    math.max(1, _edgeCount),
                    out HabitatGraphWriteViews graph,
                    out IDataVault vault))
                return;

            try
            {
                PublishDegradationState(ref graph);
            }
            finally
            {
                ReleaseGraphWriteLocks(vault);
            }
        }

        private void PublishDegradationState(ref HabitatGraphWriteViews graph)
        {
            if (!graph.Nodes.IsCreated)
                return;

            int maxNodeCount = math.min(math.max(0, _nodeCount), math.min(_moduleBuffer.Count, graph.Nodes.Length));
            for (int nodeIndex = 0; nodeIndex < maxNodeCount; nodeIndex++)
            {
                ModuleRecord module = _moduleBuffer[nodeIndex];
                if (module.ModuleObject == null)
                    continue;

                BaseDegradationSystem.SynchronizeNode(
                    module.ModuleObject,
                    module.NodeId,
                    graph.Nodes[nodeIndex].Flags,
                    ResolveNodeRuptureWorldPoint(nodeIndex));
            }
        }

        private void PublishSiegeTargetSnapshot()
        {
            if (!TryAcquireGraphWriteBuffers(
                    _nodeCount,
                    math.max(1, _edgeCount),
                    out HabitatGraphWriteViews graph,
                    out IDataVault graphVault))
            {
                _siegeTargetCount = 0;
                if (ReferenceEquals(s_latestSiegeTargetOwner, this))
                {
                    s_latestSiegeTargetOwner = null;
                    s_latestSiegeTargetCount = 0;
                }

                return;
            }

            try
            {
                PublishSiegeTargetSnapshot(ref graph);
            }
            finally
            {
                ReleaseGraphWriteLocks(graphVault);
            }
        }

        private void PublishSiegeTargetSnapshot(ref HabitatGraphWriteViews graph)
        {
            if (!EnsureSiegeTargetsHandle() ||
                !TryAcquireHabitatVaultWriteBuffer(
                    HabitatSiegeTargetsBufferId,
                    MaxSiegeTargetCount,
                    in _siegeTargetsHandle,
                    out NativeArray<HabitatSiegeTargetSnapshot> siegeTargets,
                    out IDataVault vault))
            {
                _siegeTargetCount = 0;
                if (ReferenceEquals(s_latestSiegeTargetOwner, this))
                {
                    s_latestSiegeTargetOwner = null;
                    s_latestSiegeTargetCount = 0;
                }

                return;
            }

            try
            {
                int writeCount = 0;
                int maxNodeCount = math.min(_nodeCount, _moduleBuffer.Count);
                for (int nodeIndex = 0; nodeIndex < maxNodeCount && writeCount < MaxSiegeTargetCount; nodeIndex++)
                {
                    ModuleRecord module = _moduleBuffer[nodeIndex];
                    BaseModule baseModule = module.BaseModule;
                    if (baseModule == null || !baseModule.isActiveAndEnabled)
                        continue;

                    LogisticsNodeFlags nodeFlags = graph.Nodes.IsCreated && nodeIndex < graph.Nodes.Length
                        ? graph.Nodes[nodeIndex].Flags
                        : LogisticsNodeFlags.None;
                    float integrity01 = math.saturate(baseModule.IntegrityStateNormalized);
                    HabitatSiegeTargetFlags siegeFlags = ResolveSiegeTargetFlags(module, baseModule, nodeFlags, integrity01);
                    if ((siegeFlags & HabitatSiegeTargetFlags.Vulnerable) == 0)
                        continue;

                    HabitatSiegeTargetSnapshot snapshot = default;
                    snapshot.ModuleCenter = module.Position;
                    snapshot.WeakPoint = ResolveNodeRuptureWorldPoint(nodeIndex);
                    snapshot.Integrity01 = integrity01;
                    snapshot.Vulnerability01 = ResolveSiegeVulnerability01(baseModule, nodeFlags, integrity01);
                    snapshot.NodeId = module.NodeId;
                    snapshot.Flags = (byte)siegeFlags;
                    siegeTargets[writeCount++] = snapshot;
                }

                for (int i = writeCount; i < _siegeTargetCount && i < siegeTargets.Length; i++)
                    siegeTargets[i] = default;

                _siegeTargetCount = writeCount;
                s_latestSiegeTargetOwner = this;
                s_latestSiegeTargetCount = writeCount;
            }
            finally
            {
                vault.ReleaseWriteLock(in _siegeTargetsHandle, SystemID.Construction);
            }
        }

        private void ClearSiegeTargetSnapshot()
        {
            if (TryAcquireHabitatVaultWriteBuffer(
                    HabitatSiegeTargetsBufferId,
                    MaxSiegeTargetCount,
                    in _siegeTargetsHandle,
                    out NativeArray<HabitatSiegeTargetSnapshot> siegeTargets,
                    out IDataVault vault))
            {
                try
                {
                    int clearCount = math.min(_siegeTargetCount, siegeTargets.Length);
                    for (int i = 0; i < clearCount; i++)
                        siegeTargets[i] = default;
                }
                finally
                {
                    vault.ReleaseWriteLock(in _siegeTargetsHandle, SystemID.Construction);
                }
            }

            _siegeTargetCount = 0;
            if (ReferenceEquals(s_latestSiegeTargetOwner, this))
            {
                s_latestSiegeTargetOwner = null;
                s_latestSiegeTargetCount = 0;
            }
        }

        private void PublishGraphKernel()
        {
            if (!TryAcquireGraphWriteBuffers(
                    _nodeCount,
                    math.max(1, _edgeCount),
                    out HabitatGraphWriteViews graph,
                    out IDataVault vault))
            {
                _graph.BeginBuild(LogisticsNetworkType.OxygenPressure, 1, 1, 0);
                return;
            }

            try
            {
                PublishGraphKernel(ref graph);
            }
            finally
            {
                ReleaseGraphWriteLocks(vault);
            }
        }

        private void PublishGraphKernel(ref HabitatGraphWriteViews graph)
        {
            int maxNodeCount = graph.Nodes.IsCreated
                ? math.min(math.max(0, _nodeCount), math.min(_moduleBuffer.Count, graph.Nodes.Length))
                : 0;
            _graph.BeginBuild(LogisticsNetworkType.OxygenPressure, maxNodeCount, math.max(1, _edgeCount), 0);

            for (int nodeIndex = 0; nodeIndex < maxNodeCount; nodeIndex++)
            {
                LogisticsNetworkGraph.LogisticsNode node = graph.Nodes[nodeIndex];
                _graph.AddNode(node.Id, node.Capacity, node.Resistance, node.Priority, node.Flags, node.Reserved);
            }

            for (int edgeIndex = 0; edgeIndex < _edgeBuffer.Count; edgeIndex++)
            {
                EdgeRecord edge = _edgeBuffer[edgeIndex];
                if (edge.Severed != 0 ||
                    edge.SourceIndex < 0 ||
                    edge.DestinationIndex < 0 ||
                    edge.SourceIndex >= maxNodeCount ||
                    edge.DestinationIndex >= maxNodeCount)
                {
                    continue;
                }

                _graph.AddEdge(edge.SourceIndex, edge.DestinationIndex, edge.Resistance);
                if (edge.DirectedOnly == 0)
                    _graph.AddEdge(edge.DestinationIndex, edge.SourceIndex, edge.Resistance);
            }

            _graph.FinalizeBuild();
        }

        private void PublishRuntimeRuptureTopologyState()
        {
            int directedEdgeCapacity = math.max(1, _edgeBuffer.Count * 2);
            if (!TryAcquireGraphWriteBuffers(
                    _nodeCount,
                    directedEdgeCapacity,
                    out HabitatGraphWriteViews graph,
                    out IDataVault vault))
                return;

            try
            {
                BuildEdgeRecords(ref graph, directedEdgeCapacity);
                EvaluateAnchorReachability(ref graph);
                PublishAnchorState(ref graph);
                PublishComponentPowerState(ref graph);
                PublishEmergencyLockdownState(ref graph);
                PublishDegradationState(ref graph);
                PublishSiegeTargetSnapshot(ref graph);
                PublishGraphKernel(ref graph);
            }
            finally
            {
                ReleaseGraphWriteLocks(vault);
            }

            ClearVisualLinks();
            PublishVisualLinks();
        }

        private void PublishVisualLinks()
        {
            int edgeCount = _edgeBuffer.Count;
            if (edgeCount > _submittedLinkIds.Capacity)
                WriteFloodBlackBoxSample(FloodBlackBoxOverflowClampedFlag);

            for (int edgeIndex = 0; edgeIndex < edgeCount; edgeIndex++)
            {
                if (_submittedLinkIds.Count >= _submittedLinkIds.Capacity)
                    return;

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
            EnsureGraphHandles(nodeCapacity, edgeCapacity);
            // COLD ALLOC: NativeArray<Int32>[65] — habitat CSR edge-offset buffer — owner: HabitatGraphManager
            // COLD ALLOC: NativeArray<Int32>[128] — habitat CSR destination buffer — owner: HabitatGraphManager
            // COLD ALLOC: NativeArray<Single>[128] — habitat CSR edge-resistance buffer — owner: HabitatGraphManager
            // COLD ALLOC: NativeArray<Int32>[64] — CSR write-cursor scratch buffer — owner: HabitatGraphManager
            // COLD ALLOC: NativeArray<Byte>[64] — authoritative anchor reachability state for habitat graph consumers — owner: HabitatGraphManager
            // COLD ALLOC: NativeArray<Byte>[64] — graph traversal visited scratch, separate from anchor-state truth — owner: HabitatGraphManager
            // COLD ALLOC: NativeArray<Int32>[64] — reusable BFS traversal queue for graph component walks — owner: HabitatGraphManager
            // COLD ALLOC: NativeArray<Byte>[128] - habitat directed edge flood flags - owner: HabitatGraphManager
            EnsureHabitatVaultBuffer(
                HabitatFloodBlackBoxBufferId,
                FloodBlackBoxCapacity,
                NativeArrayOptions.ClearMemory,
                ref _floodBlackBoxHandle);
            EnsureHabitatVaultBuffer(
                HabitatFloodPropagationSummaryBufferId,
                1,
                NativeArrayOptions.ClearMemory,
                ref _floodPropagationSummaryHandle);
            EnsureHabitatVaultBuffer(
                HabitatSiegeTargetsBufferId,
                MaxSiegeTargetCount,
                NativeArrayOptions.ClearMemory,
                ref _siegeTargetsHandle);
            EnsureModuleStressHandles(nodeCapacity);
            EnsureFloodRoomHandles(nodeCapacity);
            _floodBlackBoxCursor = 0;
            _floodBlackBoxDumped = false;
            _moduleStressBlackBoxDumped = false;
            _lastUploadedModuleStressCount = -1;
            _lastUploadedPeakModuleStress01 = -1f;
            _lastUploadedModuleStressLowBlend = 0f;
            _lastUploadedModuleStressQualityWeight = -1f;
            _moduleStressOrderHash = 0u;
            _peakModuleStress01 = 0f;
        }

        private void EnsureNodeCapacity(int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            int edgeCapacity = NextPowerOfTwo(math.max(safeLength * 4, InitialEdgeCapacity));
            if (EnsureGraphHandles(safeLength, edgeCapacity) &&
                EnsureSiegeTargetsHandle() &&
                EnsureFloodRoomHandles(safeLength) &&
                EnsureModuleStressHandles(safeLength) &&
                EnsureFloodBlackBoxHandle() &&
                EnsureFloodPropagationSummaryHandle())
                return;

            DisposeNativeBuffers();
            int nodeCapacity = NextPowerOfTwo(math.max(safeLength, InitialNodeCapacity));
            AllocateNativeBuffers(nodeCapacity, edgeCapacity);
        }

        private void EnsureEdgeCapacity(int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            int edgeCapacity = NextPowerOfTwo(math.max(safeLength, InitialEdgeCapacity));
            EnsureGraphHandles(math.max(1, _nodeCount), edgeCapacity);
        }

        private void DisposeNativeBuffers()
        {
            ClearSiegeTargetSnapshot();

            ReleaseFloodPropagationGraphWriteLocks();
            ReleaseDeconstructionCsrLanes();
            ReleaseHabitatVaultHandle(ref _nodesHandle);
            ReleaseHabitatVaultHandle(ref _edgeOffsetsHandle);
            ReleaseHabitatVaultHandle(ref _edgeDestinationsHandle);
            ReleaseHabitatVaultHandle(ref _edgeResistanceHandle);
            ReleaseHabitatVaultHandle(ref _edgeWriteCursorHandle);
            ReleaseHabitatVaultHandle(ref _anchorReachabilityHandle);
            ReleaseHabitatVaultHandle(ref _traversalVisitedHandle);
            ReleaseHabitatVaultHandle(ref _anchorTraversalQueueHandle);
            ReleaseHabitatVaultHandle(ref _edgeFlagsHandle);
            ReleaseHabitatVaultHandle(ref _floodBlackBoxHandle);
            ReleaseFloodPropagationSummaryWriteLock();
            ReleaseFloodPropagationRoomWriteLocks();
            ReleaseHabitatVaultHandle(ref _floodPropagationSummaryHandle);
            ReleaseHabitatVaultHandle(ref _siegeTargetsHandle);
            ReleaseHabitatVaultHandle(ref _moduleStressScalarsHandle);
            ReleaseHabitatVaultHandle(ref _previousModuleStressScalarsHandle);
            ReleaseHabitatVaultHandle(ref _moduleImpactStressSpikesHandle);
            ReleaseHabitatVaultHandle(ref _moduleCompromisedFlagsHandle);
            ReleaseHabitatVaultHandle(ref _roomWaterLevelsHandle);
            ReleaseHabitatVaultHandle(ref _roomVolumesHandle);
            ReleaseHabitatVaultHandle(ref _roomFloodDeltaLevelsHandle);
            ReleaseHabitatVaultHandle(ref _roomFlagsHandle);
            ReleaseModuleStressBuffer();
        }

        private bool EnsureHabitatVaultBuffer<T>(
            BufferID bufferId,
            int length,
            NativeArrayOptions allocationNativeArrayOptions,
            ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            IDataVault vault = ResolveHabitatDataVaultForColdPath();
            if (vault != null)
            {
                if (TryOpenHabitatVaultBuffer(vault, in handle, bufferId, length, out NativeArray<T> buffer))
                    return true;

                if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existingHandle))
                {
                    handle = existingHandle;
                    if (TryOpenHabitatVaultBuffer(vault, in handle, bufferId, length, out buffer))
                        return true;
                }

                handle = vault.EnsureGenerationHandle<T>(
                    bufferId,
                    length,
                    SystemID.Construction,
                    allocationNativeArrayOptions);
                if (TryOpenHabitatVaultBuffer(vault, in handle, bufferId, length, out buffer))
                    return true;
            }

            handle = default;
            return false;
        }

        private bool EnsureFloodBlackBoxHandle()
        {
            if (IsHabitatVaultHandle(in _floodBlackBoxHandle, HabitatFloodBlackBoxBufferId))
                return true;

            return EnsureHabitatVaultBuffer(
                HabitatFloodBlackBoxBufferId,
                FloodBlackBoxCapacity,
                NativeArrayOptions.ClearMemory,
                ref _floodBlackBoxHandle);
        }

        private bool EnsureFloodPropagationSummaryHandle()
        {
            if (IsHabitatVaultHandle(in _floodPropagationSummaryHandle, HabitatFloodPropagationSummaryBufferId))
                return true;

            return EnsureHabitatVaultBuffer(
                HabitatFloodPropagationSummaryBufferId,
                1,
                NativeArrayOptions.ClearMemory,
                ref _floodPropagationSummaryHandle);
        }

        private bool EnsureSiegeTargetsHandle()
        {
            IDataVault vault = ResolveHabitatDataVaultForColdPath();
            if (IsHabitatVaultHandle(in _siegeTargetsHandle, HabitatSiegeTargetsBufferId) &&
                TryOpenHabitatVaultBuffer(
                    vault,
                    in _siegeTargetsHandle,
                    HabitatSiegeTargetsBufferId,
                    MaxSiegeTargetCount,
                    out NativeArray<HabitatSiegeTargetSnapshot> _))
            {
                return true;
            }

            return EnsureHabitatVaultBuffer(
                HabitatSiegeTargetsBufferId,
                MaxSiegeTargetCount,
                NativeArrayOptions.ClearMemory,
                ref _siegeTargetsHandle);
        }

        private bool EnsureGraphHandles(int requiredNodeLength, int requiredEdgeLength)
        {
            int safeNodeLength = math.max(1, requiredNodeLength);
            int safeEdgeLength = math.max(1, requiredEdgeLength);
            return EnsureHabitatVaultBuffer(
                       HabitatGraphNodesBufferId,
                       safeNodeLength,
                       NativeArrayOptions.ClearMemory,
                       ref _nodesHandle) &&
                   EnsureHabitatVaultBuffer(
                       HabitatGraphEdgeOffsetsBufferId,
                       safeNodeLength + 1,
                       NativeArrayOptions.ClearMemory,
                       ref _edgeOffsetsHandle) &&
                   EnsureHabitatVaultBuffer(
                       HabitatGraphEdgeDestinationsBufferId,
                       safeEdgeLength,
                       NativeArrayOptions.ClearMemory,
                       ref _edgeDestinationsHandle) &&
                   EnsureHabitatVaultBuffer(
                       HabitatGraphEdgeResistanceBufferId,
                       safeEdgeLength,
                       NativeArrayOptions.ClearMemory,
                       ref _edgeResistanceHandle) &&
                   EnsureHabitatVaultBuffer(
                       HabitatGraphEdgeWriteCursorBufferId,
                       safeNodeLength,
                       NativeArrayOptions.ClearMemory,
                       ref _edgeWriteCursorHandle) &&
                   EnsureHabitatVaultBuffer(
                       HabitatGraphAnchorReachabilityBufferId,
                       safeNodeLength,
                       NativeArrayOptions.ClearMemory,
                       ref _anchorReachabilityHandle) &&
                   EnsureHabitatVaultBuffer(
                       HabitatGraphTraversalVisitedBufferId,
                       safeNodeLength,
                       NativeArrayOptions.ClearMemory,
                       ref _traversalVisitedHandle) &&
                   EnsureHabitatVaultBuffer(
                       HabitatGraphAnchorTraversalQueueBufferId,
                       safeNodeLength,
                       NativeArrayOptions.ClearMemory,
                       ref _anchorTraversalQueueHandle) &&
                   EnsureHabitatVaultBuffer(
                       HabitatGraphEdgeFlagsBufferId,
                       safeEdgeLength,
                       NativeArrayOptions.ClearMemory,
                       ref _edgeFlagsHandle);
        }

        private bool TryAcquireGraphWriteBuffers(
            int requiredNodeLength,
            int requiredEdgeLength,
            out HabitatGraphWriteViews graph,
            out IDataVault vault)
        {
            graph = default;
            vault = null;
            int safeNodeLength = math.max(1, requiredNodeLength);
            int safeEdgeLength = math.max(1, requiredEdgeLength);
            if (!EnsureGraphHandles(safeNodeLength, safeEdgeLength))
                return false;

            vault = _dataVault;
            if (vault == null || !vault.TryAcquireMutationGuard(HabitatGraphMutationGuardMask))
            {
                vault = null;
                return false;
            }

            bool acquired = false;
            try
            {
                acquired =
                    TryOpenHabitatVaultBuffer(vault, in _nodesHandle, HabitatGraphNodesBufferId, safeNodeLength, out graph.Nodes) &&
                    TryOpenHabitatVaultBuffer(vault, in _edgeOffsetsHandle, HabitatGraphEdgeOffsetsBufferId, safeNodeLength + 1, out graph.EdgeOffsets) &&
                    TryOpenHabitatVaultBuffer(vault, in _edgeDestinationsHandle, HabitatGraphEdgeDestinationsBufferId, safeEdgeLength, out graph.EdgeDestinations) &&
                    TryOpenHabitatVaultBuffer(vault, in _edgeResistanceHandle, HabitatGraphEdgeResistanceBufferId, safeEdgeLength, out graph.EdgeResistance) &&
                    TryOpenHabitatVaultBuffer(vault, in _edgeWriteCursorHandle, HabitatGraphEdgeWriteCursorBufferId, safeNodeLength, out graph.EdgeWriteCursor) &&
                    TryOpenHabitatVaultBuffer(vault, in _anchorReachabilityHandle, HabitatGraphAnchorReachabilityBufferId, safeNodeLength, out graph.AnchorReachability) &&
                    TryOpenHabitatVaultBuffer(vault, in _traversalVisitedHandle, HabitatGraphTraversalVisitedBufferId, safeNodeLength, out graph.TraversalVisited) &&
                    TryOpenHabitatVaultBuffer(vault, in _anchorTraversalQueueHandle, HabitatGraphAnchorTraversalQueueBufferId, safeNodeLength, out graph.AnchorTraversalQueue) &&
                    TryOpenHabitatVaultBuffer(vault, in _edgeFlagsHandle, HabitatGraphEdgeFlagsBufferId, safeEdgeLength, out graph.EdgeFlags);
                return acquired;
            }
            finally
            {
                if (!acquired)
                {
                    vault.ReleaseMutationGuard(HabitatGraphMutationGuardMask);
                    graph = default;
                    vault = null;
                }
            }
        }

        private bool TryAcquireFloodGraphJobBuffers(
            int requiredNodeLength,
            int requiredEdgeLength,
            out HabitatFloodGraphJobViews graph,
            out IDataVault vault)
        {
            graph = default;
            vault = null;
            int safeNodeLength = math.max(1, requiredNodeLength);
            int safeEdgeLength = math.max(1, requiredEdgeLength);
            if (!EnsureGraphHandles(safeNodeLength, safeEdgeLength))
                return false;

            vault = _dataVault;
            if (vault == null || !vault.TryAcquireMutationGuard(HabitatGraphMutationGuardMask))
            {
                vault = null;
                return false;
            }

            bool acquired = false;
            try
            {
                acquired =
                    TryOpenHabitatVaultBuffer(vault, in _edgeOffsetsHandle, HabitatGraphEdgeOffsetsBufferId, safeNodeLength + 1, out graph.EdgeOffsets) &&
                    TryOpenHabitatVaultBuffer(vault, in _edgeDestinationsHandle, HabitatGraphEdgeDestinationsBufferId, safeEdgeLength, out graph.EdgeDestinations) &&
                    TryOpenHabitatVaultBuffer(vault, in _edgeResistanceHandle, HabitatGraphEdgeResistanceBufferId, safeEdgeLength, out graph.EdgeResistance) &&
                    TryOpenHabitatVaultBuffer(vault, in _edgeFlagsHandle, HabitatGraphEdgeFlagsBufferId, safeEdgeLength, out graph.EdgeFlags);
                return acquired;
            }
            finally
            {
                if (!acquired)
                {
                    vault.ReleaseMutationGuard(HabitatGraphMutationGuardMask);
                    graph = default;
                    vault = null;
                }
            }
        }

        private void ReleaseGraphWriteLocks(IDataVault vault)
        {
            ReleaseGraphWriteLocks(vault, 9);
        }

        private void ReleaseGraphWriteLocks(IDataVault vault, int acquiredCount)
        {
            if (vault == null || acquiredCount <= 0)
                return;

            vault.ReleaseMutationGuard(HabitatGraphMutationGuardMask);
        }

        private void ReleaseFloodGraphWriteLocks(IDataVault vault, int acquiredCount)
        {
            if (vault == null || acquiredCount <= 0)
                return;

            vault.ReleaseMutationGuard(HabitatGraphMutationGuardMask);
        }

        private void ReleaseFloodPropagationGraphWriteLocks()
        {
            if (!_floodPropagationGraphWriteLockHeld)
                return;

            ReleaseFloodGraphWriteLocks(_floodPropagationGraphWriteLockVault, 4);
            _floodPropagationGraphWriteLockHeld = false;
            _floodPropagationGraphWriteLockVault = null;
        }

        internal void ReleaseDeconstructionCsrLanes()
        {
            if (!_deconstructionGraphWriteLockHeld)
                return;

            ReleaseFloodGraphWriteLocks(_deconstructionGraphWriteLockVault, 4);
            _deconstructionGraphWriteLockHeld = false;
            _deconstructionGraphWriteLockVault = null;
        }

        private bool EnsureFloodRoomHandles(int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            return EnsureHabitatVaultBuffer(
                       HabitatRoomWaterLevelsBufferId,
                       safeLength,
                       NativeArrayOptions.ClearMemory,
                       ref _roomWaterLevelsHandle) &&
                   EnsureHabitatVaultBuffer(
                       HabitatRoomVolumesBufferId,
                       safeLength,
                       NativeArrayOptions.ClearMemory,
                       ref _roomVolumesHandle) &&
                   EnsureHabitatVaultBuffer(
                       HabitatRoomFloodDeltaLevelsBufferId,
                       safeLength,
                       NativeArrayOptions.ClearMemory,
                       ref _roomFloodDeltaLevelsHandle) &&
                   EnsureHabitatVaultBuffer(
                       HabitatRoomFlagsBufferId,
                       safeLength,
                       NativeArrayOptions.ClearMemory,
                       ref _roomFlagsHandle);
        }

        private bool TryAcquireFloodRoomWriteBuffers(
            int requiredLength,
            out NativeArray<float> roomWaterLevels,
            out NativeArray<float> roomVolumes,
            out NativeArray<float> roomFloodDeltaLevels,
            out NativeArray<byte> roomFlags,
            out IDataVault vault)
        {
            roomWaterLevels = default;
            roomVolumes = default;
            roomFloodDeltaLevels = default;
            roomFlags = default;
            vault = null;

            int safeLength = math.max(1, requiredLength);
            if (!EnsureFloodRoomHandles(safeLength))
                return false;

            vault = _dataVault;
            if (vault == null || !vault.TryAcquireMutationGuard(HabitatFloodRoomMutationGuardMask))
            {
                vault = null;
                return false;
            }

            bool acquired = false;
            try
            {
                acquired =
                    TryOpenHabitatVaultBuffer(vault, in _roomWaterLevelsHandle, HabitatRoomWaterLevelsBufferId, safeLength, out roomWaterLevels) &&
                    TryOpenHabitatVaultBuffer(vault, in _roomVolumesHandle, HabitatRoomVolumesBufferId, safeLength, out roomVolumes) &&
                    TryOpenHabitatVaultBuffer(vault, in _roomFloodDeltaLevelsHandle, HabitatRoomFloodDeltaLevelsBufferId, safeLength, out roomFloodDeltaLevels) &&
                    TryOpenHabitatVaultBuffer(vault, in _roomFlagsHandle, HabitatRoomFlagsBufferId, safeLength, out roomFlags);
                return acquired;
            }
            finally
            {
                if (!acquired)
                {
                    vault.ReleaseMutationGuard(HabitatFloodRoomMutationGuardMask);
                    roomWaterLevels = default;
                    roomVolumes = default;
                    roomFloodDeltaLevels = default;
                    roomFlags = default;
                    vault = null;
                }
            }
        }

        private void ReleaseFloodRoomWriteLocks(IDataVault vault)
        {
            ReleaseFloodRoomWriteLocks(vault, true, true, true, true);
        }

        private void ReleaseFloodRoomWriteLocks(
            IDataVault vault,
            bool waterLocked,
            bool volumeLocked,
            bool deltaLocked,
            bool flagsLocked)
        {
            if (vault == null)
                return;

            if (waterLocked || volumeLocked || deltaLocked || flagsLocked)
                vault.ReleaseMutationGuard(HabitatFloodRoomMutationGuardMask);
        }

        private void ReleaseFloodPropagationRoomWriteLocks()
        {
            if (!_floodPropagationRoomWriteLockHeld)
                return;

            ReleaseFloodRoomWriteLocks(_floodPropagationRoomWriteLockVault);
            _floodPropagationRoomWriteLockHeld = false;
            _floodPropagationRoomWriteLockVault = null;
        }

        private bool EnsureModuleStressHandles(int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            return EnsureHabitatVaultBuffer(
                       HabitatModuleStressScalarsBufferId,
                       safeLength,
                       NativeArrayOptions.ClearMemory,
                       ref _moduleStressScalarsHandle) &&
                   EnsureHabitatVaultBuffer(
                       HabitatPreviousModuleStressScalarsBufferId,
                       safeLength,
                       NativeArrayOptions.ClearMemory,
                       ref _previousModuleStressScalarsHandle) &&
                   EnsureHabitatVaultBuffer(
                       HabitatModuleImpactStressSpikesBufferId,
                       safeLength,
                       NativeArrayOptions.ClearMemory,
                       ref _moduleImpactStressSpikesHandle) &&
                   EnsureHabitatVaultBuffer(
                       HabitatModuleCompromisedFlagsBufferId,
                       safeLength,
                       NativeArrayOptions.ClearMemory,
                       ref _moduleCompromisedFlagsHandle);
        }

        private bool TryAcquireModuleStressWriteBuffers(
            int requiredLength,
            out NativeArray<float> moduleStressScalars,
            out NativeArray<float> previousModuleStressScalars,
            out NativeArray<float> moduleImpactStressSpikes,
            out NativeArray<byte> moduleCompromisedFlags,
            out IDataVault vault)
        {
            moduleStressScalars = default;
            previousModuleStressScalars = default;
            moduleImpactStressSpikes = default;
            moduleCompromisedFlags = default;
            vault = null;

            int safeLength = math.max(1, requiredLength);
            if (!EnsureModuleStressHandles(safeLength))
                return false;

            vault = _dataVault;
            if (vault == null || !vault.TryAcquireMutationGuard(HabitatModuleStressMutationGuardMask))
            {
                vault = null;
                return false;
            }

            bool acquired = false;
            try
            {
                acquired =
                    TryOpenHabitatVaultBuffer(vault, in _moduleStressScalarsHandle, HabitatModuleStressScalarsBufferId, safeLength, out moduleStressScalars) &&
                    TryOpenHabitatVaultBuffer(vault, in _previousModuleStressScalarsHandle, HabitatPreviousModuleStressScalarsBufferId, safeLength, out previousModuleStressScalars) &&
                    TryOpenHabitatVaultBuffer(vault, in _moduleImpactStressSpikesHandle, HabitatModuleImpactStressSpikesBufferId, safeLength, out moduleImpactStressSpikes) &&
                    TryOpenHabitatVaultBuffer(vault, in _moduleCompromisedFlagsHandle, HabitatModuleCompromisedFlagsBufferId, safeLength, out moduleCompromisedFlags);
                return acquired;
            }
            finally
            {
                if (!acquired)
                {
                    vault.ReleaseMutationGuard(HabitatModuleStressMutationGuardMask);
                    moduleStressScalars = default;
                    previousModuleStressScalars = default;
                    moduleImpactStressSpikes = default;
                    moduleCompromisedFlags = default;
                    vault = null;
                }
            }
        }

        private void ReleaseModuleStressWriteLocks(IDataVault vault)
        {
            ReleaseModuleStressWriteLocks(vault, true, true, true, true);
        }

        private void ReleaseModuleStressWriteLocks(
            IDataVault vault,
            bool moduleStressLocked,
            bool previousStressLocked,
            bool impactStressLocked,
            bool compromisedFlagsLocked)
        {
            if (vault == null)
                return;

            if (moduleStressLocked || previousStressLocked || impactStressLocked || compromisedFlagsLocked)
                vault.ReleaseMutationGuard(HabitatModuleStressMutationGuardMask);
        }

        private void ReleaseFloodPropagationSummaryWriteLock()
        {
            if (!_floodPropagationSummaryWriteLockHeld)
                return;

            IDataVault vault = _floodPropagationSummaryWriteLockVault;
            if (vault != null)
                vault.ReleaseWriteLock(in _floodPropagationSummaryHandle, SystemID.Construction);

            _floodPropagationSummaryWriteLockHeld = false;
            _floodPropagationSummaryWriteLockVault = null;
        }

        private IDataVault ResolveHabitatDataVaultForColdPath()
        {
            return _dataVault;
        }

        private bool TryReadHabitatVaultBuffer<T>(
            BufferID bufferId,
            int requiredLength,
            in VaultGenerationHandle<T> handle,
            out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.Generation != 0u &&
                   requiredLength > 0 &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.Length >= requiredLength;
        }

        private bool TryAcquireHabitatVaultWriteBuffer<T>(
            BufferID bufferId,
            int requiredLength,
            in VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer,
            out IDataVault vault)
            where T : struct
        {
            buffer = default;
            vault = _dataVault;
            if (!TryOpenHabitatVaultBuffer(vault, in handle, bufferId, requiredLength, out NativeArray<T> _))
                return false;

            bool locked = vault.TryAcquireWriteLock(in handle, SystemID.Construction, out buffer);
            if (!locked ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                if (locked)
                    vault.ReleaseWriteLock(in handle, SystemID.Construction);
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool TryOpenHabitatVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null ||
                !IsHabitatVaultHandle(in handle, bufferId) ||
                requiredLength <= 0)
            {
                return false;
            }

            return vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool IsHabitatVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)SystemID.Construction &&
                   handle.Generation != 0u;
        }

        private static void ReleaseHabitatVaultHandle<T>(ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            handle = default;
        }

        private void ReleaseModuleStressBuffer()
        {
            ReleaseModuleStressBuffer(true);
        }

        private void ReleaseModuleStressBuffer(bool clearShaderParams)
        {
            if (_moduleStressBufferA != null)
            {
                _moduleStressBufferA.Release();
                _moduleStressBufferA = null;
            }

            if (_moduleStressBufferB != null)
            {
                _moduleStressBufferB.Release();
                _moduleStressBufferB = null;
            }

            _activeModuleStressBuffer = null;
            _moduleStressBufferWriteIndex = 0;
            _lastUploadedModuleStressCount = -1;
            if (clearShaderParams)
                Shader.SetGlobalVector(HabitatModuleStressParamsId, Vector4.zero);
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct DeconstructionDfsValidationJob : IJob
        {
            [ReadOnly] [NoAlias] public NativeArray<int> EdgeOffsets;
            [ReadOnly] [NoAlias] public NativeArray<int> EdgeDestinations;
            [NoAlias] public NativeArray<int> Stack;
            [NoAlias] public NativeArray<byte> Visited;
            [NoAlias] public NativeArray<int> Result;
            public int NodeCount;
            public int RemovedNodeIndex;
            public int EdgeCount;

            public void Execute()
            {
                Result[0] = 0;
                Result[1] = 0;
                Result[2] = math.max(0, NodeCount - 1);
                int boundedNodeCount = math.min(NodeCount, math.min(Stack.IsCreated ? Stack.Length : 0, Visited.IsCreated ? Visited.Length : 0));
                for (int i = 0; i < boundedNodeCount; i++)
                    Visited[i] = 0;

                if (boundedNodeCount < NodeCount || Result.Length < 3)
                    return;

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
                int stackLength = 0;
                int visitedCount = 1;
                Stack[stackLength++] = (int)startNodeKey;
                Visited[startNode] = 1;

                while (stackLength > 0)
                {
                    int currentKey = Stack[--stackLength];

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

                        if (Visited[neighborNode] != 0)
                            continue;

                        Visited[neighborNode] = 1;
                        visitedCount++;
                        if (stackLength >= Stack.Length)
                        {
                            Result[0] = 0;
                            Result[1] = visitedCount;
                            Result[2] = math.max(0, NodeCount - 1);
                            return;
                        }

                        Stack[stackLength++] = neighborNode;
                    }
                }

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
            public TransitionHatchMeshState HatchMeshState;
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
            public byte Severed;
            public byte DirectedOnly;
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

            public static SocketKey Create(double3 socketAup, int axis, int quantizationScale)
            {
                double scale = quantizationScale > 0 ? quantizationScale : 1d;
                double3 scaledPosition = socketAup * scale;
                int3 quantizedPosition = new int3(
                    QuantizeScaledAup(scaledPosition.x),
                    QuantizeScaledAup(scaledPosition.y),
                    QuantizeScaledAup(scaledPosition.z));
                return new SocketKey(quantizedPosition.x, quantizedPosition.y, quantizedPosition.z, axis);
            }

            private static int QuantizeScaledAup(double value)
            {
                if (!math.isfinite(value))
                    return 0;

                double rounded = value >= 0d ? math.floor(value + 0.5d) : math.ceil(value - 0.5d);
                if (rounded > int.MaxValue)
                    return int.MaxValue;
                if (rounded < int.MinValue)
                    return int.MinValue;

                return (int)rounded;
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
            public readonly uint CompatibilityMask;
            public readonly float3 Position;
            public readonly float3 Forward;

            public SocketMatchEntry(int moduleIndex, uint compatibilityMask, float3 position, float3 forward)
            {
                ModuleIndex = moduleIndex;
                CompatibilityMask = compatibilityMask;
                Position = position;
                Forward = forward;
            }
        }
    }
}
