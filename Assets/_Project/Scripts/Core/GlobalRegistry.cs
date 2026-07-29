using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Hecton.Localization;
using Hecton8.Animation.Locomotion;
using Hecton8.AtlasSignal;
using Hecton8.Atmosphere;
using Hecton8.Audio;
using Hecton8.Audio.Virtualization;
using Hecton8.Biolum;
using Hecton8.Bootstrap;
using Hecton8.Celestial;
using Hecton8.Construction;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Fluids;
using Hecton8.Core.Contracts.Signals;
using ScalabilityChangedEvent = Hecton8.Core.Contracts.Signals.ScalabilityChangedEvent;
using Hecton8.Core.Memory;
using Hecton8.Economy;
using Hecton8.Ecosystem;
using Hecton8.Environment;
using Hecton8.Dev;
using Hecton8.Quest;
using Hecton8.Gameplay;
using Hecton8.Input;
using Hecton8.Interaction;
using Hecton8.Meta;
using Hecton8.Modding;
using Hecton8.Narrative;
using Hecton8.Optimization;
using Hecton8.PDA;
using Hecton8.Physics;
using Hecton8.Systems.AI;
using Hecton8.Tools;
using Hecton8.UI;
using Hecton8.Visor;
using Hecton8.VFX;
using Hecton8.Vehicles.Automation;
using Hecton8.World;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Scripting;

namespace Hecton8.Core
{
    /// <summary>
    /// Raised when a GlobalRegistry service getter re-enters an active dependency resolution lane.
    /// </summary>
    public sealed class DependencyCycleException : InvalidOperationException
    {
        public DependencyCycleException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Raised when guarded registry access detects a boot-order contract breach.
    /// </summary>
    public sealed class CriticalBootException : InvalidOperationException
    {
        public CriticalBootException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Static runtime service locator and dense bucket registry for first-party core systems.
    /// </summary>
    [Preserve]
    public static partial class GlobalRegistry
    {
        /// <summary>
        /// BIOS lifecycle phase for the registry mutation gate.
        /// </summary>
        public enum RegistryPhase : byte
        {
            Uninitialized = 0,
            Registering = 1,
            Ready = 2
        }

        public enum BootConfigurationProfile : byte
        {
            Normal = 0,
            FallbackLowMemory = 1,
            SafeMode = 2
        }

        private const int ServiceSlotMaskWordCount = 4;
        private const int MathPrecisionTransitionFrameCount = 60;
        private const int MathPrecisionBlendScale = 1000;
        private const uint SystemKillSwitchBitsSignalSourceHash = HectonSignalLaneContract.SystemKillSwitchBitsSignalStableHash;
        public const uint SystemKillSwitchLane4VfxMask = 1u << 4;
        private static readonly int _mathLodLowBlendId = Shader.PropertyToID("_H8MathLodLowBlend");
        // COLD ALLOC: long[4] - requested service-slot bitset for ghost-service detection - owner: GlobalRegistry
        private static readonly long[] _requestedServiceSlotMask = new long[ServiceSlotMaskWordCount];
        // COLD ALLOC: long[4] - registered service-slot bitset for ghost-service detection - owner: GlobalRegistry
        private static readonly long[] _registeredServiceSlotMask = new long[ServiceSlotMaskWordCount];
        // COLD ALLOC: string[176] - allocation-free ghost-service slot names; index matches GlobalRegistryServiceSlot numeric value - owner: GlobalRegistry
        private static readonly string[] _serviceSlotNames =
        {
            nameof(GlobalRegistryServiceSlot.Input),
            nameof(GlobalRegistryServiceSlot.Physics),
            nameof(GlobalRegistryServiceSlot.Audio),
            nameof(GlobalRegistryServiceSlot.Scene),
            nameof(GlobalRegistryServiceSlot.Save),
            nameof(GlobalRegistryServiceSlot.UI),
            nameof(GlobalRegistryServiceSlot.ObjectPool),
            nameof(GlobalRegistryServiceSlot.Player),
            nameof(GlobalRegistryServiceSlot.PlayerInventory),
            nameof(GlobalRegistryServiceSlot.ModularEquipment),
            nameof(GlobalRegistryServiceSlot.PlayerSensory),
            nameof(GlobalRegistryServiceSlot.Environment),
            nameof(GlobalRegistryServiceSlot.Weather),
            nameof(GlobalRegistryServiceSlot.OceanKinematics),
            nameof(GlobalRegistryServiceSlot.PowerGrid),
            nameof(GlobalRegistryServiceSlot.Submarine),
            nameof(GlobalRegistryServiceSlot.SubmarineHullBreach),
            nameof(GlobalRegistryServiceSlot.InteractionSignals),
            nameof(GlobalRegistryServiceSlot.Debris),
            nameof(GlobalRegistryServiceSlot.EcosystemDirector),
            nameof(GlobalRegistryServiceSlot.ThermodynamicsService),
            nameof(GlobalRegistryServiceSlot.Logistics),
            nameof(GlobalRegistryServiceSlot.WorldGen),
            nameof(GlobalRegistryServiceSlot.EncounterDirector),
            nameof(GlobalRegistryServiceSlot.QuestSystem),
            nameof(GlobalRegistryServiceSlot.FluidRuntime),
            nameof(GlobalRegistryServiceSlot.ThermodynamicsRuntime),
            nameof(GlobalRegistryServiceSlot.NarrativeDirectorRuntime),
            nameof(GlobalRegistryServiceSlot.QuestRuntime),
            nameof(GlobalRegistryServiceSlot.TickManager),
            nameof(GlobalRegistryServiceSlot.Dispatcher),
            nameof(GlobalRegistryServiceSlot.RenderDispatcher),
            nameof(GlobalRegistryServiceSlot.PhysicsStateManager),
            nameof(GlobalRegistryServiceSlot.FaunaSimulation),
            nameof(GlobalRegistryServiceSlot.FluidSimulation),
            nameof(GlobalRegistryServiceSlot.PersistentWorldRegistry),
            nameof(GlobalRegistryServiceSlot.PDALogbook),
            nameof(GlobalRegistryServiceSlot.PlayerMotor),
            nameof(GlobalRegistryServiceSlot.Profile),
            nameof(GlobalRegistryServiceSlot.InputBinding),
            nameof(GlobalRegistryServiceSlot.CullingRuntime),
            nameof(GlobalRegistryServiceSlot.LODSystemRuntime),
            nameof(GlobalRegistryServiceSlot.DynamicResolutionRuntime),
            nameof(GlobalRegistryServiceSlot.ImpostorRuntime),
            nameof(GlobalRegistryServiceSlot.DepthZoneRuntime),
            nameof(GlobalRegistryServiceSlot.LocalizationRuntime),
            nameof(GlobalRegistryServiceSlot.AudioLogRuntime),
            nameof(GlobalRegistryServiceSlot.AtlasSignalRuntime),
            nameof(GlobalRegistryServiceSlot.FirstHourRuntime),
            nameof(GlobalRegistryServiceSlot.EmergencyRelayRuntime),
            nameof(GlobalRegistryServiceSlot.AtmosphereRuntime),
            nameof(GlobalRegistryServiceSlot.BeaconNetworkRuntime),
            nameof(GlobalRegistryServiceSlot.ScanLogRuntime),
            nameof(GlobalRegistryServiceSlot.ToolDurabilityRuntime),
            nameof(GlobalRegistryServiceSlot.LoreDatabaseRuntime),
            nameof(GlobalRegistryServiceSlot.AssetLifecycleRuntime),
            nameof(GlobalRegistryServiceSlot.AssetLoadDispatcherRuntime),
            nameof(GlobalRegistryServiceSlot.VRAMMonitorRuntime),
            nameof(GlobalRegistryServiceSlot.VRAMPressureRuntime),
            nameof(GlobalRegistryServiceSlot.RenderTextureLifecycleRuntime),
            nameof(GlobalRegistryServiceSlot.RenderTexturePoolRuntime),
            nameof(GlobalRegistryServiceSlot.WorldStateRuntime),
            nameof(GlobalRegistryServiceSlot.UserOptionsRuntime),
            nameof(GlobalRegistryServiceSlot.BiolumManagerRuntime),
            nameof(GlobalRegistryServiceSlot.AbyssalFluidDecalRuntime),
            nameof(GlobalRegistryServiceSlot.SargassumDragRuntime),
            nameof(GlobalRegistryServiceSlot.SargassumCutRuntime),
            nameof(GlobalRegistryServiceSlot.PlayerExpressionRuntime),
            nameof(GlobalRegistryServiceSlot.SpectrumRuntime),
            nameof(GlobalRegistryServiceSlot.SoundscapeRuntime),
            nameof(GlobalRegistryServiceSlot.AcousticZoneRuntime),
            nameof(GlobalRegistryServiceSlot.SurfaceWeatherRuntime),
            nameof(GlobalRegistryServiceSlot.EnvironmentalStrainRuntime),
            nameof(GlobalRegistryServiceSlot.EcosystemHealthRuntime),
            nameof(GlobalRegistryServiceSlot.FaunaGeneticsRuntime),
            nameof(GlobalRegistryServiceSlot.PlayerExplorationRuntime),
            nameof(GlobalRegistryServiceSlot.DiscoveryRuntime),
            nameof(GlobalRegistryServiceSlot.ResourceScarcityRuntime),
            nameof(GlobalRegistryServiceSlot.PDAExchangeRuntime),
            nameof(GlobalRegistryServiceSlot.PlayerActionRuntime),
            nameof(GlobalRegistryServiceSlot.PDAMarkerRuntime),
            nameof(GlobalRegistryServiceSlot.AmbientWaterMotionRuntime),
            nameof(GlobalRegistryServiceSlot.SuitUpgradeRuntime),
            nameof(GlobalRegistryServiceSlot.EndingRuntime),
            nameof(GlobalRegistryServiceSlot.Atlas6DirectiveRuntime),
            nameof(GlobalRegistryServiceSlot.HazardZoneRuntime),
            nameof(GlobalRegistryServiceSlot.MissionRuntime),
            nameof(GlobalRegistryServiceSlot.RockManagerRuntime),
            nameof(GlobalRegistryServiceSlot.CameraJuiceRuntime),
            nameof(GlobalRegistryServiceSlot.MusicDirectorRuntime),
            nameof(GlobalRegistryServiceSlot.SubtitleRuntime),
            nameof(GlobalRegistryServiceSlot.AtlasSignalDecoderRuntime),
            nameof(GlobalRegistryServiceSlot.ScrapRuntime),
            nameof(GlobalRegistryServiceSlot.AutonomousExtractorRuntime),
            nameof(GlobalRegistryServiceSlot.VisorRTRuntime),
            nameof(GlobalRegistryServiceSlot.CameraRTRuntime),
            nameof(GlobalRegistryServiceSlot.PostFXRTRuntime),
            nameof(GlobalRegistryServiceSlot.UIRTRuntime),
            nameof(GlobalRegistryServiceSlot.SettingsRuntime),
            nameof(GlobalRegistryServiceSlot.RuntimeWatchdogRuntime),
            nameof(GlobalRegistryServiceSlot.CrashTelemetryRuntime),
            nameof(GlobalRegistryServiceSlot.PlayerCriticalAudioRuntime),
            nameof(GlobalRegistryServiceSlot.MapMagicRuntime),
            nameof(GlobalRegistryServiceSlot.ProceduralFieldSamplerRuntime),
            nameof(GlobalRegistryServiceSlot.ResourceDistributionRuntime),
            nameof(GlobalRegistryServiceSlot.RandomEventRuntime),
            nameof(GlobalRegistryServiceSlot.EclipseGameplayRuntime),
            nameof(GlobalRegistryServiceSlot.WorldSeedProvider),
            nameof(GlobalRegistryServiceSlot.GeologyTerrainSeamRuntime),
            nameof(GlobalRegistryServiceSlot.GeologyVoxelBridgeRuntime),
            nameof(GlobalRegistryServiceSlot.SargassumMicroFaunaRuntime),
            nameof(GlobalRegistryServiceSlot.FloatingOriginRuntime),
            nameof(GlobalRegistryServiceSlot.PDAIntrusionRuntime),
            nameof(GlobalRegistryServiceSlot.CelestialEngineRuntime),
            nameof(GlobalRegistryServiceSlot.VoxelEngineRuntime),
            nameof(GlobalRegistryServiceSlot.BiomeMatrixRuntime),
            nameof(GlobalRegistryServiceSlot.UnderwaterVisualsRuntime),
            nameof(GlobalRegistryServiceSlot.DynamicDifficultyRuntime),
            nameof(GlobalRegistryServiceSlot.ToolHapticsRuntime),
            nameof(GlobalRegistryServiceSlot.ARWaypointRuntime),
            nameof(GlobalRegistryServiceSlot.VRSomaticProvider),
            nameof(GlobalRegistryServiceSlot.ConnectionSplineBatchRendererRuntime),
            nameof(GlobalRegistryServiceSlot.NativeInputManagerRuntime),
            nameof(GlobalRegistryServiceSlot.RaycastBatchRuntime),
            nameof(GlobalRegistryServiceSlot.FieldOperationLogRuntime),
            nameof(GlobalRegistryServiceSlot.CorporateOrderRuntime),
            nameof(GlobalRegistryServiceSlot.BiolumControllerRuntime),
            nameof(GlobalRegistryServiceSlot.UIAudioFeedbackRuntime),
            nameof(GlobalRegistryServiceSlot.UITooltipRuntime),
            nameof(GlobalRegistryServiceSlot.ScavengePopulatorRuntime),
            nameof(GlobalRegistryServiceSlot.RunModifierRuntime),
            nameof(GlobalRegistryServiceSlot.MigrationDirectorRuntime),
            nameof(GlobalRegistryServiceSlot.BasePollutionRuntime),
            nameof(GlobalRegistryServiceSlot.EntityChangeManagerRuntime),
            nameof(GlobalRegistryServiceSlot.PerformanceMonitorRuntime),
            nameof(GlobalRegistryServiceSlot.MapMagicVegetationRuntime),
            nameof(GlobalRegistryServiceSlot.ModWorldPersistenceRuntime),
            nameof(GlobalRegistryServiceSlot.LoadingScreenRuntime),
            nameof(GlobalRegistryServiceSlot.ModalWindowRuntime),
            nameof(GlobalRegistryServiceSlot.TerrainProviderRuntime),
            nameof(GlobalRegistryServiceSlot.ProceduralSwayDirectorRuntime),
            nameof(GlobalRegistryServiceSlot.SubmarineState),
            nameof(GlobalRegistryServiceSlot.VocalWarningRuntime),
            nameof(GlobalRegistryServiceSlot.HabitatDeconstructionRuntime),
            nameof(GlobalRegistryServiceSlot.SeismicDirectorRuntime),
            nameof(GlobalRegistryServiceSlot.FluidPipeGraph),
            nameof(GlobalRegistryServiceSlot.GasDynamicsRuntime),
            nameof(GlobalRegistryServiceSlot.SpatialTriggerRuntime),
            nameof(GlobalRegistryServiceSlot.GIRelayRuntime),
            nameof(GlobalRegistryServiceSlot.DataVault),
            nameof(GlobalRegistryServiceSlot.JobAdmissionRuntime),
            nameof(GlobalRegistryServiceSlot.StreamingBackpressureRuntime),
            nameof(GlobalRegistryServiceSlot.FoveatedSimulationDirector),
            nameof(GlobalRegistryServiceSlot.GroundRadarRuntime),
            nameof(GlobalRegistryServiceSlot.InertialNavigationRuntime),
            nameof(GlobalRegistryServiceSlot.ModdingBridgeRuntime),
            nameof(GlobalRegistryServiceSlot.InstanceCullingRuntime),
            nameof(GlobalRegistryServiceSlot.WorldResourceSpawnerRuntime),
            nameof(GlobalRegistryServiceSlot.MacroDatabase),
            nameof(GlobalRegistryServiceSlot.MetaCampaignRuntime),
            nameof(GlobalRegistryServiceSlot.OrbitalDirectorRuntime),
            nameof(GlobalRegistryServiceSlot.SimulationBucketerRuntime),
            nameof(GlobalRegistryServiceSlot.CausticsRuntime),
            nameof(GlobalRegistryServiceSlot.PlayerMovementContracts),
            nameof(GlobalRegistryServiceSlot.HardwareThermalService),
            nameof(GlobalRegistryServiceSlot.AudioVirtualization),
            nameof(GlobalRegistryServiceSlot.OutpostGenerationRuntime),
            nameof(GlobalRegistryServiceSlot.PrologueSequenceRuntime),
            nameof(GlobalRegistryServiceSlot.DebrisComputeRuntime),
            nameof(GlobalRegistryServiceSlot.ResolutionScalerService),
            nameof(GlobalRegistryServiceSlot.AmbientBiotaRuntime),
            nameof(GlobalRegistryServiceSlot.DockingAutopilotRuntime),
            nameof(GlobalRegistryServiceSlot.ProceduralLadderClimbRuntime),
            nameof(GlobalRegistryServiceSlot.ChemicalInfluenceRuntime),
            nameof(GlobalRegistryServiceSlot.DestructibleOrganicRuntime),
            nameof(GlobalRegistryServiceSlot.CablePhysics132Runtime)
        };
        private static int _registryPhase = (int)RegistryPhase.Uninitialized;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static bool _registeringGetViolationLogged;
        // Generation-stamped instead of a plain bool so the per-type latches below can be invalidated
        // wholesale on reset without enumerating them - a generic static cannot be iterated.
        private static int _readyLockViolationGeneration;
        private static int _readyLockViolationCount;

        /// <summary>
        /// Per-type log latch for ready-lock rejections. Zero allocation and no collection: the JIT
        /// gives each T its own static, which is the same idiom <c>ServiceSlotCache&lt;T&gt;</c> uses.
        /// Holds the generation it last logged at, so a registry reset re-arms every type at once.
        /// </summary>
        private static class ReadyLockViolationLatch<T> where T : class
        {
            internal static int LoggedGeneration = -1;
        }
#endif
        // COLD ALLOC: RegistryBucket<IUpdatable>[128] - global multi-instance update registry - owner: GlobalRegistry
        private static readonly RegistryBucket<IUpdatable> _updatables = new RegistryBucket<IUpdatable>(512);
        // COLD ALLOC: RegistryBucket<IRenderable>[64] - global multi-instance render registry - owner: GlobalRegistry
        private static readonly RegistryBucket<IRenderable> _renderables = new RegistryBucket<IRenderable>(64);
        private static readonly RegistryBucket<IFastTickable> _fastTickables = new RegistryBucket<IFastTickable>(256);
        private static readonly RegistryBucket<IFixedTickable> _fixedTickables = new RegistryBucket<IFixedTickable>(256);
        private static readonly RegistryBucket<ISlowTickable> _slowTickables = new RegistryBucket<ISlowTickable>(256);
        private static readonly RegistryBucket<IColdTickable> _coldTickables = new RegistryBucket<IColdTickable>(128);
        private static readonly RegistryBucket<IFrostTickable> _frostTickables = new RegistryBucket<IFrostTickable>(128);
        private static readonly RegistryBucket<IUnscaledFastTickable> _unscaledFastTickables = new RegistryBucket<IUnscaledFastTickable>(128);
        private static readonly RegistryBucket<IGlobalRegistryHotSwapListener> _hotSwapListeners = new RegistryBucket<IGlobalRegistryHotSwapListener>(256);
        private static readonly RegistryBucket<IRegistryEventListener> _registryEventListeners = new RegistryBucket<IRegistryEventListener>(64);
        // COLD ALLOC: NoOpInputService[1] - null-object fallback for premature GlobalRegistry.Input reads - owner: GlobalRegistry
        private static readonly IInputService _noOpInputService = new NoOpInputService();
        // COLD ALLOC: PcVRSomaticProvider[1] - null-object fallback for PC/console somatic reads - owner: GlobalRegistry
        private static readonly IVRSomaticProvider _noOpVRSomaticProvider = PcVRSomaticProvider.Shared;
        private static readonly uint _inputDependencyWarningHash = unchecked((uint)LocHash.Compute("GlobalRegistry.Input"));
        private static readonly uint _serviceReboundOverflowWarningHash = unchecked((uint)LocHash.Compute("GlobalRegistry.ServiceReboundOverflow"));
        private static readonly uint _coldResolvedSubstituteWarningHash = unchecked((uint)LocHash.Compute("GlobalRegistry.ColdResolvedSubstitute"));
        // ---------------------------------------------------------------------------------------------
        // Null-object substitution census.
        //
        // Exactly two slots hand out a NON-NULL substitute when they are read before they are filled:
        // Input (_noOpInputService, from the Input getter :956 and from ResolveSafeFallbackService :7879)
        // and VRSomaticProvider (_noOpVRSomaticProvider, from the VRSomatic getter :1921 and :7885). A
        // consumer that cold-resolves inside that window caches an object whose null check PASSES and whose
        // behaviour is nothing, so the failure surfaces later as "the feature is dead" with no error.
        //
        // Register/RegisterService only queue a rebound when the slot ALREADY held a service, so filling
        // an empty slot notifies nobody and the cached substitute is permanent. These two fields are the
        // evidence that the window was actually entered, so the first fill can report it and issue the
        // rebound the notifier previously skipped.
        //
        // Written ONLY from the substitution branch of a getter - a branch that is unreachable once the
        // slot holds a real service - and read only from registration, which is cold. A session that
        // never hands out a substitute never writes them and pays one already-latched bool test.
        private static object _inputNullObjectSubstitutionHandedOut;
        private static object _vrSomaticNullObjectSubstitutionHandedOut;
        private static readonly uint _globalRegistryTelemetryContextHash = unchecked((uint)LocHash.Compute("GlobalRegistry"));
        private const int MaxPendingServiceRebounds = 64;
        private const uint PlayerResolutionMask =
            (1u << (int)GlobalRegistryResolutionScope.PlayerContext) |
            (1u << (int)GlobalRegistryResolutionScope.PlayerInventory) |
            (1u << (int)GlobalRegistryResolutionScope.PlayerSensory);
        private const uint ForceOverrideTokenValue = 0x484F5453u; // "HOTS"
        [ThreadStatic] private static uint _resolutionMask;
        [ThreadStatic] private static IInputService _threadInput;
        [ThreadStatic] private static IPhysicsService _threadPhysics;
        [ThreadStatic] private static GameTickManager _threadTickManager;
        [ThreadStatic] private static CrashTelemetryBuffer _threadTelemetry;
        [ThreadStatic] private static IAudioService _threadAudio;
        [ThreadStatic] private static IAudioVirtualizationService _threadAudioVirtualization;
        private static BootConfigurationProfile _activeBootProfile = BootConfigurationProfile.Normal;
        private static bool _safeModeBootRequested;
        private static bool _lowMemoryProfileEnabled;
        private static uint _activeServiceTypeHash;
        private static long _absoluteUniverseTimeBits;
        private static int _mathPrecisionLevel = (int)MathPrecisionLevel.Low;
        private static int _mathPrecisionTargetLevel = (int)MathPrecisionLevel.Low;
        private static int _mathPrecisionTransitionFramesRemaining;
        private static int _mathPrecisionTransitionTotalFrames;
        private static int _mathPrecisionLowBlendMilli = MathPrecisionBlendScale;
        private static int _pendingMathPrecisionShaderLevel = (int)MathPrecisionLevel.Low;
        private static int _pendingMathPrecisionShaderLowBlendMilli = MathPrecisionBlendScale;
        private static int _mathPrecisionShaderDirty;
        private static int _sceneRuntimePublicationGateDepth;
        private static int _currentDomain = (int)Domain.Unknown;
        private static object _currentDomainOwner;

        [StructLayout(LayoutKind.Explicit, Size = 8)]
        public readonly struct ForceOverrideToken
        {
            [FieldOffset(0)]
            internal readonly uint Value;

            [FieldOffset(4)]
            private readonly uint _pad0;

            internal ForceOverrideToken(uint value)
            {
                Value = value;
                _pad0 = 0u;
            }

            internal bool IsValid => Value == ForceOverrideTokenValue;
        }

        public static BootConfigurationProfile ActiveBootProfile => _activeBootProfile;

        public static bool IsSafeModeBootRequested => _safeModeBootRequested;

        public static bool H8_LOW_MEMORY_PROFILE => _lowMemoryProfileEnabled;

        public static bool IsDevelopmentBuild
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return Debug.isDebugBuild;
#endif
            }
        }

        public static uint ActiveServiceTypeHash => _activeServiceTypeHash;

        public static int RegistryState => Volatile.Read(ref _registryPhase);

        public static double AbsoluteUniverseTime =>
            BitConverter.Int64BitsToDouble(Volatile.Read(ref _absoluteUniverseTimeBits));

        internal static void PublishAbsoluteUniverseTime(double universeTime)
        {
            Volatile.Write(ref _absoluteUniverseTimeBits, BitConverter.DoubleToInt64Bits(universeTime));
        }

        public static CelestialRuntimeSnapshot CelestialRuntimeSnapshot => _celestialRuntimeSnapshot;

        public static uint CelestialRuntimeSnapshotSequence =>
            unchecked((uint)Volatile.Read(ref _celestialRuntimeSnapshotSequence));

        public static ICelestialRuntimeSnapshotReadModel CelestialRuntimeSnapshotReadModel =>
            CelestialRuntimeSnapshotReadModelAdapter.Instance;

        public static CelestialLightReadabilitySnapshot CelestialLightReadabilitySnapshot =>
            _celestialLightReadabilitySnapshot;

        public static uint CelestialLightReadabilitySequence =>
            unchecked((uint)Volatile.Read(ref _celestialLightReadabilitySequence));

        public static ICelestialLightReadabilityReadModel CelestialLightReadabilityReadModel =>
            CelestialLightReadabilityReadModelAdapter.Instance;

        internal static void PublishCelestialRuntimeSnapshot(in CelestialRuntimeSnapshot snapshot)
        {
            _celestialRuntimeSnapshot = snapshot;
            Volatile.Write(ref _celestialRuntimeSnapshotSequence, unchecked((int)snapshot.Sequence));
        }

        internal static void PublishCelestialLightReadabilitySnapshot(in CelestialLightReadabilitySnapshot snapshot)
        {
            _celestialLightReadabilitySnapshot = snapshot;
            Volatile.Write(ref _celestialLightReadabilitySequence, unchecked((int)snapshot.Sequence));
        }

        public static void FlagFallbackLowMemoryProfile()
        {
            _lowMemoryProfileEnabled = true;
            if (_activeBootProfile == BootConfigurationProfile.Normal)
                _activeBootProfile = BootConfigurationProfile.FallbackLowMemory;
        }

        public static void RequestSafeModeBoot()
        {
            _safeModeBootRequested = true;
            _activeBootProfile = BootConfigurationProfile.SafeMode;
        }

        internal enum GlobalRegistryResolutionScope : byte
        {
            PlayerContext = 0,
            PlayerInventory = 1,
            PlayerSensory = 2,
            Settings = 3,
        }

        private struct RegistryReboundReferenceSlot
        {
            public object PreviousService;
            public object CurrentService;

            public void Clear()
            {
                PreviousService = null;
                CurrentService = null;
            }
        }

        // COLD ALLOC: RegistryReboundReferenceSlot[64] - service rebound managed sidecar slots - owner: GlobalRegistry
        private static readonly RegistryReboundReferenceSlot[] _serviceReboundReferenceSlots = new RegistryReboundReferenceSlot[MaxPendingServiceRebounds];
        // COLD ALLOC: bool[64] - service rebound sidecar occupancy map - owner: GlobalRegistry
        private static readonly bool[] _serviceReboundReferenceSlotOccupied = new bool[MaxPendingServiceRebounds];

        private static IInputService _input;
        private static IInputBindingService _inputBinding;
        private static INativeInputManagerRuntime _nativeInputManagerRuntime;
        private static RaycastBatchHelper _raycastBatchRuntime;
        private static IPhysicsService _physics;
        private static ICablePhysics132Service _cablePhysics132Runtime;
        private static IAudioService _audio;
        private static IAudioVirtualizationService _audioVirtualization;
        private static IVocalWarningSystem _vocalWarningRuntime;
        private static ISceneService _scene;
        private static ISaveService _save;
        private static IUIService _ui;
        private static IModalWindowService _modalWindowRuntime;
        private static IARWaypointService _arWaypoint;
        private static ISpatialTriggerSystem _spatialTriggerSystem;
        private static ObjectPoolManager _objectPool;
        private static IPlayerRuntimeContext _player;
        private static HectonPlayerMotor _playerMotor;
        private static IPlayerMovementContracts _playerMovementContracts;
        private static IPlayerInventoryService _playerInventory;
        private static float _playerInventoryMassKg;
        private static IModularEquipmentService _modularEquipment;
        private static IPlayerSensoryService _playerSensory;
        private static IEnvironmentRuntimeContext _environment;
        private static IChemicalInfluenceReadModel _chemicalInfluence;
        private static IOrganicToolHitService _organicToolHits;
        private static IWeatherService _weather;
        private static ISeismicDirector _seismicDirectorRuntime;
        private static CelestialRuntimeSnapshot _celestialRuntimeSnapshot;
        private static int _celestialRuntimeSnapshotSequence;
        private static CelestialLightReadabilitySnapshot _celestialLightReadabilitySnapshot;
        private static int _celestialLightReadabilitySequence;
        private sealed class CelestialRuntimeSnapshotReadModelAdapter : ICelestialRuntimeSnapshotReadModel
        {
            internal static readonly CelestialRuntimeSnapshotReadModelAdapter Instance = new CelestialRuntimeSnapshotReadModelAdapter();

            private CelestialRuntimeSnapshotReadModelAdapter()
            {
            }

            public CelestialRuntimeSnapshot RuntimeSnapshot => _celestialRuntimeSnapshot;

            public uint RuntimeSnapshotSequence => unchecked((uint)Volatile.Read(ref _celestialRuntimeSnapshotSequence));
        }

        private sealed class CelestialLightReadabilityReadModelAdapter : ICelestialLightReadabilityReadModel
        {
            internal static readonly CelestialLightReadabilityReadModelAdapter Instance = new CelestialLightReadabilityReadModelAdapter();

            private CelestialLightReadabilityReadModelAdapter()
            {
            }

            public CelestialLightReadabilitySnapshot LightReadabilitySnapshot => _celestialLightReadabilitySnapshot;

            public uint LightReadabilitySequence => unchecked((uint)Volatile.Read(ref _celestialLightReadabilitySequence));
        }

        private static IHectonOceanKinematicsService _oceanKinematics;
        private static IPowerGridService _powerGrid;
        private static ISubmarineRuntimeContext _submarine;
        private static ISubmarineState _submarineState;
        private static ISubmarineHullBreachReadModel _submarineHullBreach;
        private static IInertialNavigationService _inertialNavigation;
        private static IDockingAutopilotService _dockingAutopilot;
        private static IInteractionSignalService _interactionSignals;
        private static IDebrisService _debris;
        private static IDebrisComputeService _debrisCompute;
        private static IAmbientBiotaService _ambientBiotaRuntime;
        private static IEcosystemDirectorService _ecosystemDirector;
        private static IFaunaSim _faunaSimulation;
        private static IThermodynamicsService _thermodynamicsService;
        private static IFluidSim _fluidSimulation;
        private static ILogisticsService _logistics;
        private static IHabitatGraphService _habitatGraph;
        private static IHabitatDeconstructionSystem _habitatDeconstruction;
        private static IFluidPipeGraphService _fluidPipeGraph;
        private static IGasDynamicsSolver _gasDynamics;
        private static IWorldGenService _worldGen;
        private static IWorldSeedProvider _worldSeedProvider;
        private static PrefabRegistry _prefabRegistryRuntime;
        private static WorldProceduralFieldSampler _proceduralFieldSamplerRuntime;
        private static ResourceDistributionDirector _resourceDistributionRuntime;
        private static WorldGenerativeGeologyTerrainSeamApplier _geologyTerrainSeamRuntime;
        private static WorldGenerativeGeologyVoxelBridgeDirector _geologyVoxelBridgeRuntime;
        private static HectonVoxelEngine _voxelEngineRuntime;
        private static BiomeMatrixDirector _biomeMatrixRuntime;
        private static HectonUnderwaterVisuals _underwaterVisualsRuntime;
        private static IGIRelaySystem _giRelayRuntime;
        private static IProceduralSwayDirector _proceduralSwayDirectorRuntime;
        private static IEncounterDirectorService _encounterDirector;
        private static IQuestSystem _questSystem;
        private static PersistentWorldRegistry _persistentWorldRegistry;
        private static WorldStateManager _worldStateRuntime;
        private static IPDALogbookService _pdaLogbook;
        private static IProfileService _profile;
        private static HectonCelestialEngine _celestialEngineRuntime;
        private static IOrbitalDirector _orbitalDirectorRuntime;
        private static IPrologueSequenceService _prologueSequenceRuntime;
        private static EclipseGameplaySystem _eclipseGameplayRuntime;
        private static RandomEventSystem _randomEventRuntime;
        private static HectonFluidEngine _fluidRuntime;
        private static AbyssalThermalManager _thermodynamicsRuntime;
        private static HectonNarrativeDirector _narrativeDirectorRuntime;
        private static CorporateOrderSystem _corporateOrderRuntime;
        private static QuestManager _questRuntime;
        private static CullingManager _cullingRuntime;
        private static LODSystemManager _lodSystemRuntime;
        private static DynamicResolutionScaler _dynamicResolutionRuntime;
        private static IResolutionScalerService _resolutionScalerService;
        private static ImpostorSystem _impostorRuntime;
        private static DepthZoneDirector _depthZoneRuntime;
        private static HectonBiolumManager _biolumManagerRuntime;
        private static HectonBiolumController _biolumControllerRuntime;
        private static LocalizationManager _localizationRuntime;
        private static IBabelLocalization _babelLocalizationRuntime;
        private static AudioLogSystem _audioLogRuntime;
        private static AcousticZoneController _acousticZoneRuntime;
        private static HectonSurfaceWeatherDirector _surfaceWeatherRuntime;
        private static AtlasSignalSystem _atlasSignalRuntime;
        private static FirstHourDirector _firstHourRuntime;
        private static EmergencyServiceRelayDirector _emergencyRelayRuntime;
        private static HectonAtmosphereManager _atmosphereRuntime;
        private static ITerrainProvider _terrainProviderRuntime;
        private static MapMagicBridge _mapMagicRuntime;
        private static HectonMapMagicVegetationBridge _mapMagicVegetationRuntime;
        private static ScavengePopulator _scavengePopulatorRuntime;
        private static ModWorldPersistenceManager _modWorldPersistenceRuntime;
        private static IModdingBridge _moddingBridgeRuntime;
        private static RunModifierController _runModifierRuntime;
        private static IMetaCampaignService _metaCampaignRuntime;
        private static MigrationDirector _migrationDirectorRuntime;
        private static BasePollutionManager _basePollutionRuntime;
        private static EntityChangeManager _entityChangeManagerRuntime;
        private static PerformanceMonitor _performanceMonitorRuntime;
        private static BeaconNetworkSystem _beaconNetworkRuntime;
        private static ScanLogSystem _scanLogRuntime;
        private static ToolDurabilitySystem _toolDurabilityRuntime;
        private static ToolHapticsRuntime _toolHapticsRuntime;
        private static IVRSomaticProvider _vrSomaticProvider;
        private static LoreDatabaseManager _loreDatabaseRuntime;
        private static PlayerExpressionManager _playerExpressionRuntime;
        private static SpectrumSystem _spectrumRuntime;
        private static UserOptionsPersistence _userOptionsRuntime;
        private static AssetLifecycleGovernor _assetLifecycleRuntime;
        private static AssetLoadDispatcher _assetLoadDispatcherRuntime;
        private static VRAMMonitor _vramMonitorRuntime;
        private static VRAMPressureMonitor _vramPressureRuntime;
        private static RenderTextureLifecycleTracker _renderTextureLifecycleRuntime;
        private static RenderTexturePool _renderTexturePoolRuntime;
        private static AbyssalFluidDecalManager _abyssalFluidDecalRuntime;
        private static SargassumGlobalDragManager _sargassumDragRuntime;
        private static SargassumCutManager _sargassumCutRuntime;
        private static SargassumMicroFaunaBoids _sargassumMicroFaunaRuntime;
        private static HectonFloatingOrigin _floatingOriginRuntime;
        private static SoundscapeSystem _soundscapeRuntime;
        private static EnvironmentalStrainManager _environmentalStrainRuntime;
        private static EcosystemHealthDirector _ecosystemHealthRuntime;
        private static FaunaGeneticsManager _faunaGeneticsRuntime;
        private static PlayerExplorationTracker _playerExplorationRuntime;
        private static DynamicDifficultyDirector _dynamicDifficultyRuntime;
        private static HectonDiscoveryManager _discoveryRuntime;
        private static ResourceScarcityDirector _resourceScarcityRuntime;
        private static FieldOperationLogSystem _fieldOperationLogRuntime;
        private static PDAExchangeSystem _pdaExchangeRuntime;
        private static PlayerActionController _playerActionRuntime;
        private static PDAMarkerRegistry _pdaMarkerRuntime;
        private static PDAIntrusionManager _pdaIntrusionRuntime;
        private static AmbientWaterMotionManager _ambientWaterMotionRuntime;
        private static SuitUpgradeManager _suitUpgradeRuntime;
        private static UIAudioFeedback _uiAudioFeedbackRuntime;
        private static UITooltip _uiTooltipRuntime;
        private static LoadingScreenController _loadingScreenRuntime;
        private static EndingSystem _endingRuntime;
        private static Atlas6DirectiveSystem _atlas6DirectiveRuntime;
        private static HazardZoneManager _hazardZoneRuntime;
        private static MissionManager _missionRuntime;
        private static HectonRockManager _rockManagerRuntime;
        private static ICameraJuiceSystem _cameraJuiceRuntime;
        private static HectonMusicDirector _musicDirectorRuntime;
        private static SubtitleManager _subtitleRuntime;
        private static AtlasSignalDecoder _atlasSignalDecoderRuntime;
        private static ScrapManager _scrapRuntime;
        private static AutonomousExtractorSystem _autonomousExtractorRuntime;
        private static VisorRTManager _visorRTRuntime;
        private static CameraRTManager _cameraRTRuntime;
        private static PostFXRTManager _postFXRTRuntime;
        private static UIRTManager _uiRTRuntime;
        private static SettingsManager _settingsRuntime;
        private static RuntimeWatchdog _runtimeWatchdogRuntime;
        private static GCMonitor _gcMonitorRuntime;
        private static CrashTelemetryBuffer _crashTelemetryRuntime;
        private static PlayerCriticalProceduralAudioRenderer _playerCriticalAudioRuntime;
        private static ContextualPhysicalIkRuntime _contextualPhysicalIkRuntime;
        private static ProceduralLadderClimbRuntime _proceduralLadderClimbRuntime;
        private static GameTickManager _tickManager;
        private static SystemDispatcher _dispatcher;
        private static RenderDispatcher _renderDispatcher;
        private static GlobalPhysicsStateManager _physicsStateManager;
        private static IPhysicsCullingOverseer _physicsCullingOverseer;
        private static EnvironmentRuntimeContextService _environmentRuntimeContextRuntime;
        private static SceneRuntimeService _sceneRuntime;
        private static SceneInstantiationGate _sceneInstantiationGateRuntime;
        private static OceanKinematicsRuntimeService _oceanKinematicsRuntime;
        private static PlayerRuntimeContextService _playerRuntimeContextRuntime;
        private static PlayerSensoryManager _playerSensoryRuntime;
        private static RuntimePerformanceProfiler _runtimePerformanceProfilerRuntime;
        private static ConnectionSplineBatchRenderer _connectionSplineBatchRendererRuntime;
        private static IDataVault _dataVault;
        private static IMacroDatabaseService _macroDatabase;
        private static ICausticsService _causticsRuntime;
        private static IJobAdmissionService _jobAdmissionRuntime;
        private static ISimulationBucketer _simulationBucketerRuntime;
        private static IStreamingBackpressureService _streamingBackpressureRuntime;
        private static IFoveatedSimulationDirector _foveatedSimulationDirector;
        private static IHardwareThermalService _hardwareThermalService;
        private static IGroundRadarService _groundRadarRuntime;
        private static IWorldResourceSpawnerReadModel _worldResourceSpawnerRuntime;
        private static IInstanceCullingService _instanceCullingRuntime;
        private static IOutpostGenerationService _outpostGenerationRuntime;
        private static HectonHardwareProfile _hardwareProfile;
        private static int _scalabilityTierOverride = -1;
        private static bool _hasHardwareProfile;
        private static bool _dispatcherRegistrationErrorLogged;
        private static bool _inputFallbackWarningPublished;
        private static NativeQueue<RegistryEventPayload> _pendingServiceRebounds;
        private static NativeQueue<RegistryEventPayload> _nextFrameServiceRebounds;
        private static int _pendingServiceReboundsSentinelId;
        private static int _nextFrameServiceReboundsSentinelId;
        private static int _pendingServiceReboundCount;
        private static int _nextFrameServiceReboundCount;
        private static int _serviceReboundReferenceWriteIndex;
        private static int _serviceReboundReferencePendingCount;
        private static bool _serviceReboundOverflowLogged;
        private static bool _isDispatchingServiceRebounds;
        private static bool _suppressServiceReboundQueueing;
        private static GameBootstrapper _bootstrapperRuntime;

        static GlobalRegistry()
        {
            ConfigureBootstrapRegistryBridge();
        }

        private static void ConfigureBootstrapRegistryBridge()
        {
            BootstrapRegistryBridge.Configure(
                ResolveBootstrapRegistryBridgeService,
                RegisterBootstrapRegistryBridgeService,
                UnregisterBootstrapRegistryBridgeService);
            PlatformIntegrationBridge.Configure(
                ResolveCurrentScalabilityTierProfileByte,
                RegisterScalabilityTierOverride,
                PublishScalabilityChangedEvent);
        }

        private static byte ResolveCurrentScalabilityTierProfileByte()
        {
            return ScalabilityTierProfileByte;
        }

        private static void PublishScalabilityChangedEvent(byte previousTier, byte currentTier)
        {
            ScalabilityChangedEvent payload = new ScalabilityChangedEvent(previousTier, currentTier);
            ScalabilityEvents.Raise(in payload);
        }

        private static object ResolveBootstrapRegistryBridgeService(BootstrapRegistryBridgeSlot slot)
        {
            switch (slot)
            {
                case BootstrapRegistryBridgeSlot.NativeInputManagerRuntime:
                    return NativeInputRuntime;
                case BootstrapRegistryBridgeSlot.UserOptionsRuntime:
                    return _userOptionsRuntime;
                default:
                    return null;
            }
        }

        private static void RegisterBootstrapRegistryBridgeService(BootstrapRegistryBridgeSlot slot, object service)
        {
            switch (slot)
            {
                case BootstrapRegistryBridgeSlot.NativeInputManagerRuntime:
                    if (service is INativeInputManagerRuntime inputManager)
                        RegisterNativeInputManagerRuntime(inputManager);
                    return;
                case BootstrapRegistryBridgeSlot.UserOptionsRuntime:
                    if (service is UserOptionsPersistence userOptions)
                        RegisterUserOptionsRuntime(userOptions);
                    return;
            }
        }

        private static void UnregisterBootstrapRegistryBridgeService(BootstrapRegistryBridgeSlot slot, object service)
        {
            switch (slot)
            {
                case BootstrapRegistryBridgeSlot.NativeInputManagerRuntime:
                    if (service is INativeInputManagerRuntime inputManager)
                        UnregisterNativeInputManagerRuntime(inputManager);
                    return;
                case BootstrapRegistryBridgeSlot.UserOptionsRuntime:
                    if (service is UserOptionsPersistence userOptions)
                        UnregisterUserOptionsRuntime(userOptions);
                    return;
            }
        }

        /// <summary>
        /// Registry-owned bootstrap authority. This replaces local singleton ownership in GameBootstrapper.
        /// </summary>
        public static GameBootstrapper BootstrapperRuntime => _bootstrapperRuntime;

        /// <summary>
        /// Current mutation phase of the registry BIOS.
        /// </summary>
        public static RegistryPhase Phase => (RegistryPhase)Volatile.Read(ref _registryPhase);

        /// <summary>
        /// Registry-owned scene service component before and after interface registration.
        /// </summary>
        internal static SceneRuntimeService SceneRuntime => _sceneRuntime;

        /// <summary>
        /// Registry-owned shader-bent pipe/relay renderer.
        /// </summary>
        internal static ConnectionSplineBatchRenderer ConnectionSplineBatchRenderer => _connectionSplineBatchRendererRuntime;

        /// <summary>
        /// Registry-owned environment context component before and after interface registration.
        /// </summary>
        internal static EnvironmentRuntimeContextService EnvironmentRuntimeContextRuntime => _environmentRuntimeContextRuntime;

        /// <summary>
        /// Registry-owned scene instantiation gate.
        /// </summary>
        public static SceneInstantiationGate SceneInstantiationGateRuntime => _sceneInstantiationGateRuntime;

        /// <summary>
        /// Registry-owned global data vault for persistent native buffers.
        /// </summary>
        public static IDataVault DataVault => _dataVault;

        /// <summary>
        /// Registry-owned SHINOBU 132 cable physics service. Cold dependency route only.
        /// </summary>
        public static ICablePhysics132Service CablePhysics132 => _cablePhysics132Runtime;

        /// <summary>
        /// Registry-owned 100km macro database pager service.
        /// </summary>
        public static IMacroDatabaseService MacroDatabase => _macroDatabase;

        /// <summary>
        /// Registry-owned underwater caustics presentation service.
        /// </summary>
        public static ICausticsService Caustics => _causticsRuntime;

        /// <summary>
        /// Registry-owned Burst job admission gate.
        /// </summary>
        public static IJobAdmissionService JobAdmission => _jobAdmissionRuntime;

        /// <summary>
        /// Registry-owned modulo simulation time-slicer.
        /// </summary>
        public static ISimulationBucketer SimulationBucketer => _simulationBucketerRuntime;

        /// <summary>
        /// Registry-owned streaming IO backpressure read model.
        /// </summary>
        public static IStreamingBackpressureService StreamingBackpressure => _streamingBackpressureRuntime;

        /// <summary>
        /// Registry-owned procedural GPU instance culling runtime.
        /// </summary>
        public static IInstanceCullingService InstanceCulling => _instanceCullingRuntime;

        /// <summary>
        /// Registry-owned foveated AI simulation director.
        /// </summary>
        public static IFoveatedSimulationDirector FoveatedSimulationDirector => _foveatedSimulationDirector;

        /// <summary>
        /// Registry-owned hardware thermal and battery watchdog service.
        /// </summary>
        public static IHardwareThermalService HardwareThermal => _hardwareThermalService;

        /// <summary>
        /// Registry-owned subsurface GPR read model.
        /// </summary>
        public static IGroundRadarService GroundRadar => _groundRadarRuntime;

        /// <summary>
        /// Registry-owned world resource SoA read model.
        /// </summary>
        public static IWorldResourceSpawnerReadModel WorldResourceSpawner => _worldResourceSpawnerRuntime;

        /// <summary>
        /// Registry-owned deterministic outpost generation runtime.
        /// </summary>
        public static IOutpostGenerationService OutpostGeneration => _outpostGenerationRuntime;

        /// <summary>
        /// Runtime-wide emergency kill-switch mask. Callers must use stable bit constants.
        /// </summary>
        public static uint SystemKillSwitchMask => SignalBusRegistry.RuntimeKillSwitchMask;

        /// <summary>
        /// Atomically flips bits in the runtime-wide emergency kill-switch mask.
        /// </summary>
        public static void SetSystemKillSwitchBits(uint mask, bool enabled)
        {
            SignalBusRegistry.SetSystemKillSwitchBits(mask, enabled, SystemKillSwitchBitsSignalSourceHash);
        }

        /// <summary>
        /// Registry-owned prefab ID registry.
        /// </summary>
        public static PrefabRegistry PrefabRegistryRuntime => _prefabRegistryRuntime;

        /// <summary>
        /// Registry-owned ocean kinematics selector component before and after interface registration.
        /// </summary>
        internal static OceanKinematicsRuntimeService OceanKinematicsRuntime => _oceanKinematicsRuntime;

        /// <summary>
        /// Registry-owned player runtime context component before and after interface registration.
        /// </summary>
        internal static PlayerRuntimeContextService PlayerRuntimeContextRuntime => _playerRuntimeContextRuntime;

        /// <summary>
        /// Registry-owned player sensory component before and after interface registration.
        /// </summary>
        internal static PlayerSensoryManager PlayerSensoryRuntime => _playerSensoryRuntime;

        /// <summary>
        /// Registry-owned development GC sentinel.
        /// </summary>
        internal static GCMonitor GCMonitorRuntime => _gcMonitorRuntime;

        /// <summary>
        /// Registry-owned development runtime profiler.
        /// </summary>
        internal static RuntimePerformanceProfiler RuntimePerformanceProfilerRuntime => _runtimePerformanceProfilerRuntime;

        /// <summary>
        /// Registry-owned floating-origin authority.
        /// </summary>
        public static HectonFloatingOrigin FloatingOrigin => _floatingOriginRuntime;

        /// <summary>
        /// Registered input service slot.
        /// </summary>
        public static IInputService Input
        {
            get
            {
                IInputService cached = _threadInput;
                IInputService registered = _input;
                if (cached != null && ReferenceEquals(cached, registered))
                    return cached;

                if (registered != null)
                {
                    _threadInput = registered;
                    return registered;
                }

                PublishInputFallbackWarning();
                return _noOpInputService;
            }
        }

        /// <summary>
        /// Registry-owned deterministic input bridge. This aliases the authoritative input service without exposing a concrete singleton.
        /// </summary>
        public static IInputDeterminismService InputDeterminism => Input;

        /// <summary>
        /// Raw registered input service slot for bootstrap/service-owner validation.
        /// Callers outside service initialization should use <see cref="Input"/>.
        /// </summary>
        internal static IInputService RegisteredInput => _input;

        /// <summary>
        /// Bootstrap-owned native input action owner exposed through a narrow runtime contract.
        /// </summary>
        public static INativeInputManagerRuntime NativeInputRuntime
        {
            get
            {
                if (_nativeInputManagerRuntime != null)
                    return _nativeInputManagerRuntime;

                if (_input is InputDispatcher dispatcher)
                    return dispatcher.NativeInputRuntime;

                return _input as INativeInputManagerRuntime;
            }
        }

        /// <summary>
        /// Registered input binding service slot.
        /// </summary>
        public static IInputBindingService InputBinding => _inputBinding;

        /// <summary>
        /// Optional registry-owned batched raycast helper.
        /// </summary>
        public static RaycastBatchHelper RaycastBatch => _raycastBatchRuntime;

        /// <summary>
        /// Player-look query cache exposed as a core route so tool/UI code does not bind to the physics cache owner.
        /// </summary>
        public static IPlayerLookQueryCache PlayerLookQueryCache => global::Hecton8.Physics.GlobalQueryCacheManager.PlayerLook;

        /// <summary>
        /// Physics query diagnostics exposed without concrete physics counter reads.
        /// </summary>
        public static IPhysicsQueryTelemetryReadModel PhysicsQueryTelemetry => _raycastBatchRuntime;

        /// <summary>
        /// Registered input rebind service slot for mod/UI callers that should not know about Input System assets.
        /// </summary>
        public static IInputRebindService InputRebind => _inputBinding as IInputRebindService;

        /// <summary>
        /// Registered physics service slot.
        /// </summary>
        public static IPhysicsService Physics
        {
            get
            {
                IPhysicsService cached = _threadPhysics;
                IPhysicsService registered = _physics;
                if (cached != null && ReferenceEquals(cached, registered))
                    return cached;

                _threadPhysics = registered;
                return registered;
            }
        }

        /// <summary>
        /// Registered audio service slot.
        /// </summary>
        public static IAudioService Audio
        {
            get
            {
                IAudioService cached = _threadAudio;
                IAudioService registered = _audio;
                if (cached != null && ReferenceEquals(cached, registered))
                    return cached;

                _threadAudio = registered;
                return registered;
            }
        }

        /// <summary>
        /// Registered virtual voice scheduler. This owns acoustic signal ranking before physical DSP assignment.
        /// </summary>
        public static IAudioVirtualizationService AudioVirtualization
        {
            get
            {
                IAudioVirtualizationService cached = _threadAudioVirtualization;
                IAudioVirtualizationService registered = _audioVirtualization;
                if (cached != null && ReferenceEquals(cached, registered))
                    return cached;

                _threadAudioVirtualization = registered;
                return registered;
            }
        }

        /// <summary>
        /// Authoritative crash telemetry runtime owner.
        /// </summary>
        public static CrashTelemetryBuffer CrashTelemetry
        {
            get
            {
                CrashTelemetryBuffer cached = _threadTelemetry;
                CrashTelemetryBuffer registered = _crashTelemetryRuntime;
                if (cached != null && ReferenceEquals(cached, registered))
                    return cached;

                _threadTelemetry = registered;
                return registered;
            }
        }

        /// <summary>
        /// Authoritative player critical procedural audio runtime owner.
        /// </summary>
        public static PlayerCriticalProceduralAudioRenderer PlayerCriticalAudio => _playerCriticalAudioRuntime;

        /// <summary>
        /// Player-critical procedural DSP write route.
        /// </summary>
        public static IPlayerCriticalAudioSignalSink PlayerCriticalAudioSignals => _playerCriticalAudioRuntime;

        /// <summary>
        /// Player-critical sonar echo read model for cockpit UI.
        /// </summary>
        public static IPlayerCriticalSonarEchoReadModel PlayerCriticalSonarEcho => _playerCriticalAudioRuntime;

        /// <summary>
        /// Authoritative vocal warning queue/runtime owner.
        /// </summary>
        public static IVocalWarningSystem VocalWarnings => _vocalWarningRuntime;

        /// <summary>
        /// Registry-owned contextual physical IK runtime owner.
        /// </summary>
        internal static ContextualPhysicalIkRuntime ContextualPhysicalIkRuntime => _contextualPhysicalIkRuntime;

        /// <summary>
        /// Registry-owned procedural ladder climb IK runtime owner.
        /// </summary>
        internal static ProceduralLadderClimbRuntime ProceduralLadderClimbRuntime => _proceduralLadderClimbRuntime;

        /// <summary>
        /// Registered scene service slot.
        /// </summary>
        public static ISceneService Scene => _scene;

        /// <summary>
        /// Registered save service slot.
        /// </summary>
        public static ISaveService Save => _save;

        /// <summary>
        /// Registered async persistence service slot.
        /// </summary>
        public static IAsyncPersistenceService AsyncPersistence => _save as IAsyncPersistenceService;

        /// <summary>
        /// Registered concrete save runtime owner for compatibility during singleton migration.
        /// </summary>
        public static Hecton8.SaveSystem.SaveManager SaveRuntime => _save as Hecton8.SaveSystem.SaveManager;

        /// <summary>
        /// Registered UI service slot.
        /// </summary>
        public static IUIService UI => _ui;

        /// <summary>
        /// Registered AR waypoint projection service slot.
        /// </summary>
        public static IARWaypointService ARWaypoints => _arWaypoint;

        /// <summary>
        /// Registered AUP spatial trigger service slot.
        /// </summary>
        public static ISpatialTriggerSystem SpatialTriggerSystem => _spatialTriggerSystem;

        /// <summary>
        /// Registered object-pool runtime owner.
        /// </summary>
        public static ObjectPoolManager ObjectPool =>
            ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(_objectPool)
                ? _objectPool
                : null;

        /// <summary>
        /// Registered object-pool facade for cross-domain consumers.
        /// </summary>
        public static IObjectPoolService ObjectPoolService => ObjectPool;

        internal static ObjectPoolManager ObjectPoolRuntimeMirror => _objectPool;

        /// <summary>
        /// Registered player runtime context slot.
        /// </summary>
        public static IPlayerRuntimeContext Player
        {
            get
            {
                if (IsResolvingAny(PlayerResolutionMask))
                    ThrowDependencyCycle(GlobalRegistryResolutionScope.PlayerContext);

                return _player;
            }
        }

        internal static IPlayerRuntimeContext RegisteredPlayer => _player;

        /// <summary>
        /// Registered player motor service slot.
        /// </summary>
        public static HectonPlayerMotor PlayerMotor => _playerMotor;

        /// <summary>
        /// Narrow player motor command route for emergency seat-lock consumers.
        /// </summary>
        public static IPlayerSeatLockMotorSink PlayerSeatLockMotor => _playerMotor;

        /// <summary>
        /// Registered narrow player movement contract bundle.
        /// </summary>
        public static IPlayerMovementContracts PlayerMovementContracts => _playerMovementContracts;

        /// <summary>
        /// Registered player inventory/tooling service slot.
        /// </summary>
        public static IPlayerInventoryService PlayerInventory
        {
            get
            {
                if (IsResolving(GlobalRegistryResolutionScope.PlayerContext) ||
                    IsResolving(GlobalRegistryResolutionScope.PlayerInventory))
                {
                    ThrowDependencyCycle(GlobalRegistryResolutionScope.PlayerInventory);
                }

                return _playerInventory;
            }
        }

        internal static IPlayerInventoryService RegisteredPlayerInventory => _playerInventory;

        /// <summary>
        /// Registered concrete player inventory owner mirrored by <see cref="PlayerInventory"/>.
        /// </summary>
        public static Hecton8.Inventory.PlayerInventory PlayerInventoryRuntime
        {
            get
            {
                IPlayerInventoryService inventoryService = PlayerInventory;
                return inventoryService != null ? inventoryService.Inventory : null;
            }
        }

        /// <summary>
        /// Last Burst-derived player inventory mass published by PlayerInventory.
        /// </summary>
        public static float PlayerInventoryMassKg => _playerInventoryMassKg;

        public static void PublishPlayerInventoryMassKg(float totalMassKg)
        {
            _playerInventoryMassKg = float.IsFinite(totalMassKg) && totalMassKg > 0f ? totalMassKg : 0f;
        }

        /// <summary>
        /// Registered modular-equipment runtime service slot.
        /// </summary>
        public static IModularEquipmentService ModularEquipment => _modularEquipment;

        /// <summary>
        /// Registered player sensory/presentation service slot.
        /// </summary>
        public static IPlayerSensoryService PlayerSensory
        {
            get
            {
                if (IsResolving(GlobalRegistryResolutionScope.PlayerContext) ||
                    IsResolving(GlobalRegistryResolutionScope.PlayerSensory))
                {
                    ThrowDependencyCycle(GlobalRegistryResolutionScope.PlayerSensory);
                }

                return _playerSensory;
            }
        }

        internal static IPlayerSensoryService RegisteredPlayerSensory => _playerSensory;

        /// <summary>
        /// Registered environment runtime context slot.
        /// </summary>
        public static IEnvironmentRuntimeContext Environment => _environment;

        /// <summary>
        /// Registered chemical influence read-model slot.
        /// </summary>
        public static IChemicalInfluenceReadModel ChemicalInfluence => _chemicalInfluence;

        /// <summary>
        /// Registered organic tool-hit command slot.
        /// </summary>
        public static IOrganicToolHitService OrganicToolHits => _organicToolHits;

        /// <summary>
        /// Registered weather service slot.
        /// </summary>
        public static IWeatherService Weather => _weather;

        /// <summary>
        /// Registered deterministic seismic and harmonic-tide director.
        /// </summary>
        public static ISeismicDirector SeismicDirector => _seismicDirectorRuntime;

        /// <summary>
        /// Registered ocean-kinematics selector service slot.
        /// </summary>
        public static IHectonOceanKinematicsService OceanKinematics => _oceanKinematics;

        /// <summary>
        /// Registered power-grid runtime service slot.
        /// </summary>
        public static IPowerGridService PowerGrid => _powerGrid;

        /// <summary>
        /// Registered authoritative submarine runtime root slot.
        /// </summary>
        public static ISubmarineRuntimeContext Submarine => _submarine;

        /// <summary>
        /// Registered submarine ballast and stabilizer read-model slot.
        /// </summary>
        public static ISubmarineState SubmarineState => _submarineState;

        /// <summary>
        /// Registered submarine hull-breach read model slot.
        /// Front-buffer only. Writers must keep back-buffer private.
        /// </summary>
        public static ISubmarineHullBreachReadModel SubmarineHullBreach => _submarineHullBreach;

        /// <summary>
        /// Registered dead-reckoning inertial navigation service slot.
        /// </summary>
        public static IInertialNavigationService InertialNavigation => _inertialNavigation;

        /// <summary>
        /// Registered autonomous vehicle docking spline service slot.
        /// </summary>
        public static IDockingAutopilotService DockingAutopilot => _dockingAutopilot;

        /// <summary>
        /// Registered interaction signal service slot.
        /// </summary>
        public static IInteractionSignalService InteractionSignals => _interactionSignals;

        /// <summary>
        /// Registered debris service slot.
        /// </summary>
        public static IDebrisService Debris => _debris;

        /// <summary>
        /// Registered GPU-resident debris shard service slot.
        /// </summary>
        public static IDebrisComputeService DebrisCompute => _debrisCompute;

        /// <summary>
        /// Registered GPU-resident ambient biota service slot.
        /// </summary>
        public static IAmbientBiotaService AmbientBiota => _ambientBiotaRuntime;

        /// <summary>
        /// Registered ecosystem sector simulation service slot.
        /// </summary>
        public static IEcosystemDirectorService EcosystemDirector => _ecosystemDirector;

        /// <summary>
        /// Registered data-only fauna simulation service slot.
        /// </summary>
        public static IFaunaSim FaunaSimulation => _faunaSimulation;

        /// <summary>
        /// Registered thermodynamics service slot.
        /// </summary>
        public static IThermodynamicsService ThermodynamicsService => _thermodynamicsService;

        /// <summary>
        /// Registered data-only fluid simulation service slot.
        /// </summary>
        public static IFluidSim FluidSimulation => _fluidSimulation;

        /// <summary>
        /// Registered logistics/build-network service slot.
        /// </summary>
        public static ILogisticsService Logistics => _logistics;

        /// <summary>
        /// Registered habitat graph flood read model slot.
        /// </summary>
        public static IHabitatGraphService HabitatGraph => _habitatGraph;

        /// <summary>
        /// Registered construction-owned parasite graph route.
        /// </summary>
        public static IConstructionParasiteGraphService ConstructionParasiteGraph => _logistics as IConstructionParasiteGraphService;

        /// <summary>
        /// Registered habitat deconstruction validation and rollback service slot.
        /// </summary>
        public static IHabitatDeconstructionSystem HabitatDeconstruction => _habitatDeconstruction;

        /// <summary>
        /// Registered fluid pipe pressure graph slot.
        /// </summary>
        public static IFluidPipeGraphService FluidPipeGraph => _fluidPipeGraph;

        /// <summary>
        /// Registered Dalton gas dynamics solver slot.
        /// </summary>
        public static IGasDynamicsSolver GasDynamics => _gasDynamics;

        /// <summary>
        /// Registered concrete construction runtime owner for compatibility during singleton migration.
        /// </summary>
        public static ConstructionManager ConstructionRuntime => _logistics as ConstructionManager;

        /// <summary>
        /// Registered world-generation service slot.
        /// </summary>
        public static IWorldGenService WorldGen => _worldGen;

        /// <summary>
        /// Registered deterministic world-seed provider slot.
        /// </summary>
        public static IWorldSeedProvider WorldSeedProvider => _worldSeedProvider;

        /// <summary>
        /// Registered concrete procedural scatter runtime owner.
        /// </summary>
        public static WorldProceduralScatterDirector ProceduralScatter => _worldGen as WorldProceduralScatterDirector;

        /// <summary>
        /// Registered concrete procedural field sampler runtime owner.
        /// </summary>
        public static WorldProceduralFieldSampler ProceduralFieldSampler => _proceduralFieldSamplerRuntime;

        /// <summary>
        /// Read-only biome physics influence route exposed by the procedural field owner.
        /// </summary>
        public static IBiomePhysicsInfluenceReadModel BiomePhysicsInfluence => _proceduralFieldSamplerRuntime;

        /// <summary>
        /// Registered concrete resource-distribution runtime owner.
        /// </summary>
        public static ResourceDistributionDirector ResourceDistribution => _resourceDistributionRuntime;

        /// <summary>
        /// Read-only brine density route exposed by the resource-distribution owner.
        /// </summary>
        public static IBrineFluidDensityReadModel BrineFluidDensity => _resourceDistributionRuntime as IBrineFluidDensityReadModel;

        /// <summary>
        /// Read-only analytical flow route exposed by the fluid owner.
        /// </summary>
        public static IAnalyticalFlowReadModel AnalyticalFlow => _fluidRuntime;

        /// <summary>
        /// Read-only GPU abyssal-flow route exposed by the fluid owner.
        /// </summary>
        public static IAbyssalFlowGpuReadModel AbyssalFlowGpu => _fluidRuntime;

        /// <summary>
        /// RenderGraph fluid advection dispatch route exposed without binding presentation to the physics runtime type.
        /// </summary>
        public static IFluidAdvectionRenderGraphDispatchSource FluidAdvectionRenderGraph => _fluidRuntime;

        /// <summary>
        /// Read-only authored/global current route exposed by the fluid owner.
        /// </summary>
        public static IAmbientCurrentReadModel AmbientCurrent => _fluidRuntime;

        /// <summary>
        /// Read-only surface/current route exposed by the fluid owner.
        /// </summary>
        public static IFluidSurfaceCurrentReadModel FluidSurfaceCurrent => _fluidRuntime;

        /// <summary>
        /// Narrow advected-bubble command route exposed by the fluid owner.
        /// </summary>
        public static IFluidBubbleBurstSink FluidBubbleBurstSink => _fluidRuntime;

        /// <summary>
        /// Buoyancy object registration route exposed by the fluid owner.
        /// </summary>
        public static IBuoyancyObjectRegistry BuoyancyObjectRegistry => _fluidRuntime;

        /// <summary>
        /// Narrow weather-current write route exposed by the fluid owner.
        /// </summary>
        public static IFluidCurrentWriteSink FluidCurrentWriteSink => _fluidRuntime;

        /// <summary>
        /// Registered terrain/voxel seam applier runtime owner.
        /// </summary>
        public static WorldGenerativeGeologyTerrainSeamApplier GeologyTerrainSeam => _geologyTerrainSeamRuntime;

        /// <summary>
        /// Registered geology voxel bridge runtime owner.
        /// </summary>
        public static WorldGenerativeGeologyVoxelBridgeDirector GeologyVoxelBridge => _geologyVoxelBridgeRuntime;

        /// <summary>
        /// Registered voxel generation/runtime owner.
        /// </summary>
        public static HectonVoxelEngine VoxelEngine => _voxelEngineRuntime;

        /// <summary>
        /// Registered voxel sonar SDF read model exposed through a contract surface.
        /// </summary>
        public static Hecton8.Core.Contracts.IVoxelSonarSdfReadModel VoxelSonarSdf => _voxelEngineRuntime as Hecton8.Core.Contracts.IVoxelSonarSdfReadModel;

        /// <summary>
        /// Registered biome matrix runtime owner.
        /// </summary>
        public static BiomeMatrixDirector BiomeMatrix => _biomeMatrixRuntime;

        /// <summary>
        /// Registered underwater visuals runtime owner.
        /// </summary>
        public static HectonUnderwaterVisuals UnderwaterVisuals => _underwaterVisualsRuntime;

        /// <summary>
        /// Registered day/night GI relay runtime owner.
        /// </summary>
        public static IGIRelaySystem GIRelay => _giRelayRuntime;

        /// <summary>
        /// Registered procedural flora sway director.
        /// </summary>
        public static IProceduralSwayDirector ProceduralSwayDirector => _proceduralSwayDirectorRuntime;

        /// <summary>
        /// Registered mathematical wake displacement service.
        /// </summary>
        public static IWakeDisplacementService WakeDisplacement => _proceduralSwayDirectorRuntime;

        /// <summary>
        /// Registered encounter-direction service slot.
        /// </summary>
        public static IEncounterDirectorService EncounterDirector => _encounterDirector;

        /// <summary>
        /// Registered quest-system service slot.
        /// </summary>
        public static IQuestSystem QuestSystem => _questSystem;

        /// <summary>
        /// Registered persistent world registry owner.
        /// </summary>
        public static PersistentWorldRegistry PersistentWorldRegistry => _persistentWorldRegistry;

        /// <summary>
        /// Narrow dropped-item emission route owned by the persistent world registry.
        /// </summary>
        public static IPersistentDroppedItemRegistry PersistentDroppedItems => _persistentWorldRegistry;

        /// <summary>
        /// Registered nutrient-facing thermal vent snapshot read model.
        /// </summary>
        public static INutrientThermalVentReadModel NutrientThermalVents => _persistentWorldRegistry;

        /// <summary>
        /// Registered world-state persistence runtime owner.
        /// </summary>
        public static WorldStateManager WorldState => _worldStateRuntime;

        /// <summary>
        /// Registered PDA logbook append service.
        /// </summary>
        public static IPDALogbookService PDALogbook => _pdaLogbook;

        /// <summary>
        /// Registered global meta profile service slot.
        /// </summary>
        public static IProfileService Profile => _profile;

        /// <summary>
        /// Registered eclipse-gameplay runtime owner.
        /// </summary>
        public static EclipseGameplaySystem EclipseGameplay => _eclipseGameplayRuntime;

        /// <summary>
        /// Registered random-event runtime owner.
        /// </summary>
        public static RandomEventSystem RandomEvents => _randomEventRuntime;

        /// <summary>
        /// Registered fluid simulation runtime owner.
        /// </summary>
        public static HectonFluidEngine Fluid => _fluidRuntime;

        /// <summary>
        /// Registered thermodynamic simulation runtime owner.
        /// </summary>
        public static AbyssalThermalManager Thermodynamics => _thermodynamicsRuntime;

        /// <summary>
        /// Registered narrative director runtime owner.
        /// </summary>
        public static HectonNarrativeDirector NarrativeDirector => _narrativeDirectorRuntime;

        public static INarrativeDiscoveryReadModel NarrativeDiscoveryReadModel => _narrativeDirectorRuntime;

        /// <summary>
        /// Registered corporate-order runtime owner.
        /// </summary>
        public static CorporateOrderSystem CorporateOrders => _corporateOrderRuntime;

        /// <summary>
        /// Registered quest runtime owner.
        /// </summary>
        public static QuestManager Quest => _questRuntime;

        /// <summary>
        /// Registered world-culling runtime owner.
        /// </summary>
        public static CullingManager Culling => _cullingRuntime;

        /// <summary>
        /// Registered world LOD runtime owner.
        /// </summary>
        public static LODSystemManager LODSystem => _lodSystemRuntime;

        /// <summary>
        /// Registered dynamic-resolution runtime owner.
        /// </summary>
        public static DynamicResolutionScaler DynamicResolution => _dynamicResolutionRuntime;

        /// <summary>
        /// Contract-facing dynamic-resolution runtime owner.
        /// </summary>
        public static IDynamicResolutionRuntime DynamicResolutionRuntime => _dynamicResolutionRuntime;

        /// <summary>
        /// Contract-facing STP render-scale policy service.
        /// </summary>
        public static IResolutionScalerService ResolutionScaler => _resolutionScalerService;

        /// <summary>
        /// Registered impostor runtime owner.
        /// </summary>
        public static ImpostorSystem Impostors => _impostorRuntime;

        /// <summary>
        /// Registered depth-zone runtime owner.
        /// </summary>
        public static DepthZoneDirector DepthZone => _depthZoneRuntime;

        /// <summary>
        /// Read-only depth-zone route exposed without binding consumers to the owner type.
        /// </summary>
        public static IDepthZoneReadModel DepthZoneReadModel => _depthZoneRuntime;

        /// <summary>
        /// Registered world bioluminescence runtime owner.
        /// </summary>
        public static HectonBiolumManager BiolumManager => _biolumManagerRuntime;

        /// <summary>
        /// Registered bioluminescence shader-controller runtime owner.
        /// </summary>
        public static HectonBiolumController BiolumController => _biolumControllerRuntime;

        /// <summary>
        /// Registered localization runtime owner.
        /// </summary>
        public static LocalizationManager Localization => _localizationRuntime;

        /// <summary>
        /// Registered allocation-free Babel localization interface.
        /// </summary>
        public static IBabelLocalization BabelLocalization => _babelLocalizationRuntime ?? _localizationRuntime;

        /// <summary>
        /// Registered localization read model.
        /// </summary>
        public static ILocalizationTextReadModel LocalizationText =>
            (_babelLocalizationRuntime as ILocalizationTextReadModel) ?? _localizationRuntime;

        /// <summary>
        /// Registered localization expansion read model.
        /// </summary>
        public static ILocalizationTextExpansionReadModel LocalizationTextExpansion => _localizationRuntime;

        /// <summary>
        /// Registered localization language control command sink.
        /// </summary>
        public static ILocalizationLanguageControl LocalizationLanguageControl => _localizationRuntime;

        /// <summary>
        /// Registered localization stress/corrosion presentation read model.
        /// </summary>
        public static ILocalizationStressPresentationReadModel LocalizationStressPresentation => _localizationRuntime;

        /// <summary>
        /// Registered localization PDA madness presentation read model.
        /// </summary>
        public static ILocalizationMadnessPresentationReadModel LocalizationMadnessPresentation => _localizationRuntime;

        /// <summary>
        /// Registered localization stress HUD refresh command sink.
        /// </summary>
        public static ILocalizationStressHudRefreshSink LocalizationStressHudRefreshSink => _localizationRuntime;

        /// <summary>
        /// Registered PDA corrosion presentation command sink.
        /// </summary>
        public static IPdaCorrosionPresentationSink PdaCorrosionPresentationSink => _localizationRuntime;

        /// <summary>
        /// Registered localization transient override command sink.
        /// </summary>
        public static ILocalizationTransientOverrideSink LocalizationTransientOverrideSink => _localizationRuntime;

        /// <summary>
        /// Registered audio-log runtime owner.
        /// </summary>
        public static AudioLogSystem AudioLogs => _audioLogRuntime;

        /// <summary>
        /// Registered audio-log read/runtime contract.
        /// </summary>
        public static IAudioLogRuntime AudioLogRuntime => _audioLogRuntime;

        /// <summary>
        /// Registered acoustic-zone runtime owner.
        /// </summary>
        public static AcousticZoneController AcousticZone => _acousticZoneRuntime;

        /// <summary>
        /// Read-only acoustic-zone route exposed without binding consumers to the owner type.
        /// </summary>
        public static IAcousticZoneReadModel AcousticZoneReadModel => _acousticZoneRuntime;

        /// <summary>
        /// Narrow madness-whisper cue sink exposed without binding consumers to the owner type.
        /// </summary>
        public static IAcousticZoneMadnessCueSink AcousticZoneMadnessCueSink => _acousticZoneRuntime;

        /// <summary>
        /// Registered tool-facing acoustic cue service.
        /// </summary>
        public static IToolAcousticCueService ToolAcousticCues => _acousticZoneRuntime;

        /// <summary>
        /// Registered surface-weather runtime owner.
        /// </summary>
        public static HectonSurfaceWeatherDirector SurfaceWeather => _surfaceWeatherRuntime;

        /// <summary>
        /// Read-only surface-weather route exposed without binding consumers to the director type.
        /// </summary>
        public static ISurfaceWeatherReadModel SurfaceWeatherReadModel => _surfaceWeatherRuntime;

        /// <summary>
        /// Registered Atlas signal runtime owner.
        /// </summary>
        public static AtlasSignalSystem AtlasSignal => _atlasSignalRuntime;

        /// <summary>
        /// Registered Atlas signal read model.
        /// </summary>
        public static IAtlasSignalReadModel AtlasSignalReadModel => _atlasSignalRuntime;

        /// <summary>
        /// Narrow Atlas signal decode command sink.
        /// </summary>
        public static IAtlasSignalDecodeSink AtlasSignalDecodeSink => _atlasSignalRuntime;

        /// <summary>
        /// Registered first-hour pacing runtime owner.
        /// </summary>
        public static FirstHourDirector FirstHour => _firstHourRuntime;

        /// <summary>
        /// Registered first-hour read model.
        /// </summary>
        public static IFirstHourReadModel FirstHourReadModel => _firstHourRuntime;

        /// <summary>
        /// Registered emergency relay runtime owner.
        /// </summary>
        public static EmergencyServiceRelayDirector EmergencyRelay => _emergencyRelayRuntime;

        /// <summary>
        /// Registered emergency relay route read model.
        /// </summary>
        public static IEmergencyRelayRouteReadModel EmergencyRelayReadModel => _emergencyRelayRuntime;

        /// <summary>
        /// Registered atmosphere runtime owner.
        /// </summary>
        public static HectonAtmosphereManager Atmosphere => _atmosphereRuntime;

        /// <summary>
        /// Registered read-only atmosphere scalar provider.
        /// </summary>
        public static IAtmosphereReadModel AtmosphereReadModel => _atmosphereRuntime;

        /// <summary>
        /// Registered celestial runtime owner.
        /// </summary>
        public static HectonCelestialEngine CelestialEngine => _celestialEngineRuntime;

        /// <summary>
        /// Read-only celestial sky direction provider.
        /// </summary>
        public static ICelestialSkyDirectionReadModel CelestialSkyDirection => _celestialEngineRuntime;

        /// <summary>
        /// Read-only celestial resonance provider.
        /// </summary>
        public static ICelestialResonanceReadModel CelestialResonance => _celestialEngineRuntime;

        /// <summary>
        /// Registered terrain sampling provider. Gameplay must use this interface instead of MapMagic types.
        /// </summary>
        public static ITerrainProvider Terrain => _terrainProviderRuntime;

        /// <summary>
        /// Registered MapMagic bridge runtime owner.
        /// </summary>
        public static MapMagicBridge MapMagic => _mapMagicRuntime;

        /// <summary>
        /// Registered MapMagic vegetation runtime owner.
        /// </summary>
        public static HectonMapMagicVegetationBridge MapMagicVegetation => _mapMagicVegetationRuntime;

        /// <summary>
        /// Registered nutrient-facing abyssal flow volume read model.
        /// </summary>
        public static IAbyssalFlowVolumeReadModel AbyssalFlowVolume => _mapMagicVegetationRuntime;

        /// <summary>
        /// Read-only terrain height sample route exposed without binding consumers to vegetation owner type.
        /// </summary>
        public static ITerrainHeightSampleReadModel TerrainHeightSamples => _mapMagicVegetationRuntime;

        /// <summary>
        /// Read-only vegetation threat route exposed without binding consumers to vegetation owner type.
        /// </summary>
        public static IVegetationThreatReadModel VegetationThreat => _mapMagicVegetationRuntime;

        /// <summary>
        /// Vegetation threat pulse sink exposed without binding consumers to vegetation owner type.
        /// </summary>
        public static IVegetationThreatPulseSink VegetationThreatPulses => _mapMagicVegetationRuntime;

        /// <summary>
        /// Registered scavenge populator runtime owner.
        /// </summary>
        public static ScavengePopulator ScavengePopulator => _scavengePopulatorRuntime;

        /// <summary>
        /// Registered mod world persistence runtime owner.
        /// </summary>
        internal static ModWorldPersistenceManager ModWorldPersistence => _modWorldPersistenceRuntime;

        /// <summary>
        /// Registered native-to-managed mod projection bridge.
        /// </summary>
        public static IModdingBridge ModdingBridge => _moddingBridgeRuntime;

        /// <summary>
        /// Registered run-modifier runtime owner.
        /// </summary>
        public static RunModifierController RunModifiers => _runModifierRuntime;

        /// <summary>
        /// Registered meta-campaign progression runtime owner.
        /// </summary>
        public static IMetaCampaignService MetaCampaign => _metaCampaignRuntime;

        /// <summary>
        /// Registered global fauna migration runtime owner.
        /// </summary>
        public static MigrationDirector Migration => _migrationDirectorRuntime;

        /// <summary>
        /// Registered base-pollution runtime owner.
        /// </summary>
        public static BasePollutionManager BasePollution => _basePollutionRuntime;

        /// <summary>
        /// Registered entity-change manager runtime owner.
        /// </summary>
        public static EntityChangeManager EntityChanges => _entityChangeManagerRuntime;

        /// <summary>
        /// Registered core performance monitor runtime owner.
        /// </summary>
        public static PerformanceMonitor PerformanceMonitor => _performanceMonitorRuntime;

        /// <summary>
        /// Registered beacon-network runtime owner.
        /// </summary>
        public static BeaconNetworkSystem BeaconNetwork => _beaconNetworkRuntime;

        /// <summary>
        /// Contract-only beacon-network route for tools and VFX formation consumers.
        /// </summary>
        public static IBeaconNetworkService BeaconNetworkService => _beaconNetworkRuntime;

        /// <summary>
        /// Registered scan-log runtime owner.
        /// </summary>
        public static ScanLogSystem ScanLog => _scanLogRuntime;

        public static IScanLogService ScanLogService => _scanLogRuntime;

        /// <summary>
        /// Registered tool-durability runtime owner.
        /// </summary>
        public static ToolDurabilitySystem ToolDurability => _toolDurabilityRuntime;

        /// <summary>
        /// Contract-only durability route for tools, UI, equipment, and maintenance consumers.
        /// </summary>
        public static IToolDurabilityService ToolDurabilityService => _toolDurabilityRuntime;

        /// <summary>
        /// Registered tool haptics runtime owner.
        /// </summary>
        public static ToolHapticsRuntime ToolHaptics => _toolHapticsRuntime;

        /// <summary>
        /// Registered VR somatic provider, or the PC/console dummy provider when no VR owner is active.
        /// </summary>
        public static IVRSomaticProvider VRSomatic
        {
            get
            {
                IVRSomaticProvider registered = _vrSomaticProvider;
                if (registered != null)
                    return registered;

                // Deliberately silent. On a non-VR build this substitute is the permanent and CORRECT
                // answer, so a log here would fire on every PC boot and train everyone to ignore it. What
                // is NOT normal is a real VR provider registering AFTER this line ran - that transition is
                // what ReportFirstFillAfterNullObjectSubstitution reports, and only then.
                if (_vrSomaticNullObjectSubstitutionHandedOut == null)
                    _vrSomaticNullObjectSubstitutionHandedOut = _noOpVRSomaticProvider;

                return _noOpVRSomaticProvider;
            }
        }

        /// <summary>
        /// Raw registered VR somatic provider for bootstrap/service-owner validation.
        /// Callers outside service initialization should use <see cref="VRSomatic"/>.
        /// </summary>
        internal static IVRSomaticProvider RegisteredVRSomatic => _vrSomaticProvider;

        /// <summary>
        /// Registered lore database runtime owner.
        /// </summary>
        public static LoreDatabaseManager LoreDatabase => _loreDatabaseRuntime;

        /// <summary>
        /// Registered lore unlock read model.
        /// </summary>
        public static ILoreUnlockReadModel LoreUnlockReadModel => _loreDatabaseRuntime;

        /// <summary>
        /// Registered lore database read model.
        /// </summary>
        public static ILoreDatabaseReadModel LoreDatabaseReadModel => _loreDatabaseRuntime;

        /// <summary>
        /// Registered lore unlock command sink.
        /// </summary>
        public static ILoreUnlockSink LoreUnlockSink => _loreDatabaseRuntime;

        /// <summary>
        /// Registered player expression/profile runtime owner.
        /// </summary>
        public static PlayerExpressionManager PlayerExpression => _playerExpressionRuntime;

        /// <summary>
        /// Registered player expression/profile read model.
        /// </summary>
        public static IPlayerExpressionReadModel PlayerExpressionReadModel => _playerExpressionRuntime;

        /// <summary>
        /// Registered visor spectrum runtime owner.
        /// </summary>
        public static SpectrumSystem Spectrum => _spectrumRuntime;

        /// <summary>
        /// Registered user-options persistence runtime owner.
        /// </summary>
        public static UserOptionsPersistence UserOptions => _userOptionsRuntime;

        /// <summary>
        /// Registered asset residency governor runtime owner.
        /// </summary>
        public static AssetLifecycleGovernor AssetLifecycle => _assetLifecycleRuntime;

        /// <summary>
        /// Contract-only asset residency pressure/release route.
        /// </summary>
        public static IAssetLifecyclePressureSink AssetLifecyclePressureSink => _assetLifecycleRuntime;

        /// <summary>
        /// Registered asset load dispatcher runtime owner.
        /// </summary>
        public static AssetLoadDispatcher AssetLoadDispatcher => _assetLoadDispatcherRuntime;

        /// <summary>
        /// Registered VRAM monitor runtime owner.
        /// </summary>
        public static VRAMMonitor VRAMMonitor => _vramMonitorRuntime;

        /// <summary>
        /// Read-only VRAM budget counter route.
        /// </summary>
        public static IVramBudgetReadModel VRAMBudgetReadModel => _vramMonitorRuntime;

        /// <summary>
        /// Cold VRAM counter sampling route.
        /// </summary>
        public static IVramBudgetSampleSink VRAMBudgetSampleSink => _vramMonitorRuntime;

        /// <summary>
        /// Registered VRAM pressure response runtime owner.
        /// </summary>
        public static VRAMPressureMonitor VRAMPressure => _vramPressureRuntime;

        /// <summary>
        /// Read-only VRAM/RAM pressure response route for bootstrap and residency gates.
        /// </summary>
        public static IVramPressureReadModel VRAMPressureReadModel => _vramPressureRuntime;

        /// <summary>
        /// Cold command route for forcing an immediate VRAM/RAM pressure sample.
        /// </summary>
        public static IVramPressureSampleSink VRAMPressureSampleSink => _vramPressureRuntime;

        /// <summary>
        /// UI mip-bias feedback route for asset dispatch policy.
        /// </summary>
        public static IVramPressureMipBiasSink VRAMPressureMipBiasSink => _vramPressureRuntime;

        /// <summary>
        /// Registered RenderTexture lifecycle tracker runtime owner.
        /// </summary>
        public static RenderTextureLifecycleTracker RenderTextureLifecycle => _renderTextureLifecycleRuntime;

        /// <summary>
        /// Contract-only RenderTexture lifecycle route for cross-domain diagnostics.
        /// </summary>
        public static IRenderTextureLifecycleService RenderTextureLifecycleService => _renderTextureLifecycleRuntime;

        /// <summary>
        /// Registered RenderTexture pool runtime owner.
        /// </summary>
        public static RenderTexturePool RenderTexturePool => _renderTexturePoolRuntime;

        /// <summary>
        /// Contract-only RenderTexture pool route for cross-domain consumers.
        /// </summary>
        public static IRenderTexturePoolService RenderTexturePoolService => _renderTexturePoolRuntime;

        /// <summary>
        /// Registered abyssal fluid aftermath decal runtime owner.
        /// </summary>
        public static AbyssalFluidDecalManager AbyssalFluidDecals => _abyssalFluidDecalRuntime;

        /// <summary>
        /// Presentation-only fluid aftermath route for cross-domain visual requests.
        /// </summary>
        public static IFluidDecalPresentationSink FluidDecalPresentation => _abyssalFluidDecalRuntime;

        /// <summary>
        /// Registered sargassum global drag-field runtime owner.
        /// </summary>
        public static SargassumGlobalDragManager SargassumDrag => _sargassumDragRuntime;

        /// <summary>
        /// Read-only sargassum drag route exposed without binding consumers to the owner type.
        /// </summary>
        public static ISargassumDragReadModel SargassumDragReadModel => _sargassumDragRuntime;

        /// <summary>
        /// Registered sargassum cut-mask runtime owner.
        /// </summary>
        public static SargassumCutManager SargassumCut => _sargassumCutRuntime;

        /// <summary>
        /// Registered sargassum cut-mask command facade.
        /// </summary>
        public static ISargassumCutWriteService SargassumCutWrite => _sargassumCutRuntime;

        /// <summary>
        /// Registered sargassum micro-fauna boid runtime owner.
        /// </summary>
        public static SargassumMicroFaunaBoids SargassumMicroFauna => _sargassumMicroFaunaRuntime;

        /// <summary>
        /// Registered micro-fauna presentation pulse sink.
        /// </summary>
        public static IMicroFaunaPresentationPulseSink MicroFaunaPresentationPulses => _sargassumMicroFaunaRuntime;

        /// <summary>
        /// Registered environmental soundscape runtime owner.
        /// </summary>
        public static SoundscapeSystem Soundscape => _soundscapeRuntime;

        /// <summary>
        /// Registered environmental soundscape tier read model.
        /// </summary>
        public static ISoundscapeTierReadModel SoundscapeTierReadModel => _soundscapeRuntime;

        /// <summary>
        /// Registered environmental strain runtime owner.
        /// </summary>
        public static EnvironmentalStrainManager EnvironmentalStrain => _environmentalStrainRuntime;

        /// <summary>
        /// Registered environmental strain read model.
        /// </summary>
        public static IEnvironmentalStrainReadModel EnvironmentalStrainReadModel => _environmentalStrainRuntime;

        /// <summary>
        /// Registered environmental strain industrial-pollution sink.
        /// </summary>
        public static IEnvironmentalStrainIndustrialSink EnvironmentalStrainIndustrialSink => _environmentalStrainRuntime;

        /// <summary>
        /// Registered ecosystem health runtime owner.
        /// </summary>
        public static EcosystemHealthDirector EcosystemHealth => _ecosystemHealthRuntime;

        /// <summary>
        /// Registered fauna genetics runtime owner.
        /// </summary>
        public static FaunaGeneticsManager FaunaGenetics => _faunaGeneticsRuntime;

        /// <summary>
        /// Read-only deterministic fauna world-seed route.
        /// </summary>
        public static IFaunaWorldSeedReadModel FaunaWorldSeed => _faunaGeneticsRuntime;

        /// <summary>
        /// Registered player exploration runtime owner.
        /// </summary>
        public static PlayerExplorationTracker PlayerExploration => _playerExplorationRuntime;

        /// <summary>
        /// Read-only player exploration route for non-PDA systems.
        /// </summary>
        public static IPlayerExplorationChunkReadModel PlayerExplorationReadModel => _playerExplorationRuntime;

        /// <summary>
        /// Registered discovery runtime owner.
        /// </summary>
        public static HectonDiscoveryManager Discovery => _discoveryRuntime;

        /// <summary>
        /// Registered dynamic difficulty runtime owner.
        /// </summary>
        public static DynamicDifficultyDirector DynamicDifficulty => _dynamicDifficultyRuntime;

        /// <summary>
        /// Registered resource scarcity runtime owner.
        /// </summary>
        public static ResourceScarcityDirector ResourceScarcity => _resourceScarcityRuntime;

        /// <summary>
        /// Read-only scarcity inflation route.
        /// </summary>
        public static IResourceScarcityReadModel ResourceScarcityReadModel => _resourceScarcityRuntime;

        /// <summary>
        /// Registered field-operation log runtime owner.
        /// </summary>
        public static FieldOperationLogSystem FieldOperations => _fieldOperationLogRuntime;

        /// <summary>
        /// Registered PDA exchange runtime owner.
        /// </summary>
        public static PDAExchangeSystem PDAExchange => _pdaExchangeRuntime;

        /// <summary>
        /// Registered player action runtime owner.
        /// </summary>
        public static PlayerActionController PlayerActions => _playerActionRuntime;

        /// <summary>
        /// Registered player action interrupt route.
        /// </summary>
        public static IPlayerActionInterruptSink PlayerActionInterrupts => _playerActionRuntime;

        /// <summary>
        /// Registered PDA marker registry runtime owner.
        /// </summary>
        public static PDAMarkerRegistry PDAMarkers => _pdaMarkerRuntime;

        /// <summary>
        /// Registered PDA intrusion runtime owner.
        /// </summary>
        public static PDAIntrusionManager PDAIntrusion => _pdaIntrusionRuntime;

        /// <summary>
        /// Registered ambient water-motion runtime owner.
        /// </summary>
        public static AmbientWaterMotionManager AmbientWaterMotion => _ambientWaterMotionRuntime;

        /// <summary>
        /// Registered suit upgrade runtime owner.
        /// </summary>
        public static SuitUpgradeManager SuitUpgrades => _suitUpgradeRuntime;

        /// <summary>
        /// Registered UI audio feedback runtime owner.
        /// </summary>
        public static UIAudioFeedback UIAudioFeedback => _uiAudioFeedbackRuntime;

        /// <summary>
        /// Registered UI tooltip runtime owner.
        /// </summary>
        public static UITooltip UITooltip => _uiTooltipRuntime;

        /// <summary>
        /// Registered scene modal facade.
        /// </summary>
        public static IModalWindowService ModalWindow => _modalWindowRuntime;

        /// <summary>
        /// Registered loading screen runtime owner.
        /// </summary>
        public static LoadingScreenController LoadingScreen => _loadingScreenRuntime;

        /// <summary>
        /// Registered ending runtime owner.
        /// </summary>
        public static EndingSystem Ending => _endingRuntime;

        /// <summary>
        /// Narrow ending runtime service route.
        /// </summary>
        public static IEndingRuntimeService EndingRuntimeService => _endingRuntime as IEndingRuntimeService;

        /// <summary>
        /// Registered Atlas-6 directive runtime owner.
        /// </summary>
        public static Atlas6DirectiveSystem Atlas6Directive => _atlas6DirectiveRuntime;

        /// <summary>
        /// Narrow Atlas-6 directive command sink.
        /// </summary>
        public static IAtlas6DirectiveCommandSink Atlas6DirectiveCommandSink => _atlas6DirectiveRuntime;

        /// <summary>
        /// Registered hazard-zone runtime owner.
        /// </summary>
        public static HazardZoneManager HazardZones => _hazardZoneRuntime;

        /// <summary>
        /// Registered hazard-zone read model.
        /// </summary>
        public static IHazardZoneReadModel HazardZoneReadModel => _hazardZoneRuntime;

        /// <summary>
        /// Registered mission facade runtime owner.
        /// </summary>
        public static MissionManager Missions => _missionRuntime;

        /// <summary>
        /// Registered rock rendering/proximity runtime owner.
        /// </summary>
        public static HectonRockManager RockManager => _rockManagerRuntime;

        /// <summary>
        /// Registered camera presentation feedback runtime owner.
        /// </summary>
        public static ICameraJuiceSystem CameraJuice => _cameraJuiceRuntime;

        /// <summary>
        /// Registered orbital prologue director runtime owner.
        /// </summary>
        public static IOrbitalDirector OrbitalDirector => _orbitalDirectorRuntime;

        /// <summary>
        /// Registered awaitable prologue sequence runtime owner.
        /// </summary>
        public static IPrologueSequenceService PrologueSequence => _prologueSequenceRuntime;

        /// <summary>
        /// Registered adaptive music director runtime owner.
        /// </summary>
        public static HectonMusicDirector MusicDirector => _musicDirectorRuntime;

        /// <summary>
        /// Registered subtitle presentation runtime owner.
        /// </summary>
        public static SubtitleManager Subtitles => _subtitleRuntime;

        /// <summary>
        /// Registered Atlas signal decoder runtime owner.
        /// </summary>
        public static AtlasSignalDecoder AtlasSignalDecoder => _atlasSignalDecoderRuntime;

        /// <summary>
        /// Registered recycling/scrap runtime owner.
        /// </summary>
        public static ScrapManager Scrap => _scrapRuntime;

        /// <summary>
        /// Registered autonomous extractor SOA runtime owner.
        /// </summary>
        public static AutonomousExtractorSystem AutonomousExtractors => _autonomousExtractorRuntime;

        /// <summary>
        /// Registered visor RenderTexture budget monitor runtime owner.
        /// </summary>
        public static VisorRTManager VisorRT => _visorRTRuntime;

        /// <summary>
        /// Registered camera RenderTexture budget monitor runtime owner.
        /// </summary>
        public static CameraRTManager CameraRT => _cameraRTRuntime;

        /// <summary>
        /// Registered post-processing RenderTexture budget monitor runtime owner.
        /// </summary>
        public static PostFXRTManager PostFXRT => _postFXRTRuntime;

        /// <summary>
        /// Registered UI RenderTexture budget monitor runtime owner.
        /// </summary>
        public static UIRTManager UIRT => _uiRTRuntime;

        /// <summary>
        /// Registered user settings runtime owner.
        /// </summary>
        public static SettingsManager Settings => _settingsRuntime;

        /// <summary>
        /// Registered runtime liveness watchdog owner.
        /// </summary>
        public static RuntimeWatchdog RuntimeWatchdog => _runtimeWatchdogRuntime;

        /// <summary>
        /// Registered tick-manager owner.
        /// </summary>
        public static GameTickManager TickManager
        {
            get
            {
                GameTickManager cached = _threadTickManager;
                GameTickManager registered = _tickManager;
                if (cached != null && ReferenceEquals(cached, registered))
                    return cached;

                _threadTickManager = registered;
                return registered;
            }
        }

        /// <summary>
        /// Registered gameplay dispatcher owner.
        /// </summary>
        public static SystemDispatcher Dispatcher => _dispatcher;

        /// <summary>
        /// Registered time-dilation dispatcher contract.
        /// </summary>
        public static ITickDispatcher TickDispatcher => _dispatcher;

        /// <summary>
        /// Registered SRP render dispatcher owner.
        /// </summary>
        public static RenderDispatcher RenderDispatcher => _renderDispatcher;

        /// <summary>
        /// Registered global physics-state manager owner.
        /// </summary>
        public static GlobalPhysicsStateManager PhysicsStateManager => _physicsStateManager;

        /// <summary>
        /// Registered physics-state event route for impacts and temporary connection tracking.
        /// </summary>
        public static IPhysicsStateEventService PhysicsStateEvents => _physicsStateManager;

        /// <summary>
        /// Registered centralized physics culling overseer.
        /// </summary>
        public static IPhysicsCullingOverseer PhysicsCullingOverseer => _physicsCullingOverseer;

        /// <summary>
        /// True once boot captured immutable hardware facts.
        /// </summary>
        public static bool HasHardwareProfile => _hasHardwareProfile;

        /// <summary>
        /// Boot-time hardware profile captured before Environment services initialize.
        /// </summary>
        public static HectonHardwareProfile HardwareProfile => _hardwareProfile;

        /// <summary>
        /// Resolved quality tier captured during HardwareCheck.
        /// </summary>
        public static HectonQualityTier QualityTier => _hasHardwareProfile ? _hardwareProfile.QualityTier : HectonQualityTier.Unknown;

        /// <summary>
        /// Current isolated runtime domain. Domain-specific systems must gate hot execution on this value.
        /// </summary>
        public static Domain CurrentDomain => (Domain)Volatile.Read(ref _currentDomain);

        /// <summary>
        /// Attempts to claim the current runtime domain for an isolated scene owner.
        /// </summary>
        public static bool TryClaimCurrentDomain(Domain domain, object owner)
        {
            if (owner == null || domain == Domain.Unknown)
                return false;

            object currentOwner = _currentDomainOwner;
            if (currentOwner != null && !ReferenceEquals(currentOwner, owner))
                return false;

            _currentDomainOwner = owner;
            Volatile.Write(ref _currentDomain, (int)domain);
            return true;
        }

        /// <summary>
        /// Sets the current runtime domain when the caller owns the domain lane.
        /// </summary>
        public static void SetCurrentDomain(Domain domain, object owner)
        {
            TryClaimCurrentDomain(domain, owner);
        }

        /// <summary>
        /// Clears the current runtime domain if the calling owner still owns it.
        /// </summary>
        public static void ClearCurrentDomain(Domain domain, object owner)
        {
            if (owner == null)
                return;

            if ((Domain)Volatile.Read(ref _currentDomain) != domain || !ReferenceEquals(_currentDomainOwner, owner))
                return;

            _currentDomainOwner = null;
            Volatile.Write(ref _currentDomain, (int)Domain.Unknown);
        }

        /// <summary>
        /// BIOS-selected math precision level for shader/simulation branches.
        /// </summary>
        public static MathPrecisionLevel MathPrecision =>
            (MathPrecisionLevel)Volatile.Read(ref _mathPrecisionLevel);

        /// <summary>
        /// Current target precision during a watchdog-initiated degradation transition.
        /// </summary>
        public static MathPrecisionLevel TargetMathPrecision =>
            (MathPrecisionLevel)Volatile.Read(ref _mathPrecisionTargetLevel);

        /// <summary>
        /// Low-precision blend in [0..1], updated once per dispatcher frame during degradation.
        /// </summary>
        public static float MathPrecisionLowBlend01 =>
            Volatile.Read(ref _mathPrecisionLowBlendMilli) * 0.001f;

        private static void SetMathPrecisionLevelImmediate(MathPrecisionLevel level)
        {
            int lowBlendMilli = level == MathPrecisionLevel.Low ? MathPrecisionBlendScale : 0;
            Volatile.Write(ref _mathPrecisionLevel, (int)level);
            Volatile.Write(ref _mathPrecisionTargetLevel, (int)level);
            Volatile.Write(ref _mathPrecisionTransitionFramesRemaining, 0);
            Volatile.Write(ref _mathPrecisionTransitionTotalFrames, 0);
            ApplyMathPrecisionShaderState(level, lowBlendMilli);
        }

        private static void CompleteMathPrecisionTransition()
        {
            MathPrecisionLevel targetLevel = (MathPrecisionLevel)Volatile.Read(ref _mathPrecisionTargetLevel);
            int lowBlendMilli = targetLevel == MathPrecisionLevel.Low ? MathPrecisionBlendScale : 0;
            Volatile.Write(ref _mathPrecisionLevel, (int)targetLevel);
            Volatile.Write(ref _mathPrecisionTransitionFramesRemaining, 0);
            Volatile.Write(ref _mathPrecisionTransitionTotalFrames, 0);
            ApplyMathPrecisionShaderState(targetLevel, lowBlendMilli);
        }

        private static void ApplyMathPrecisionShaderState(MathPrecisionLevel level, int lowBlendMilli)
        {
            int clampedBlendMilli = lowBlendMilli < 0
                ? 0
                : lowBlendMilli > MathPrecisionBlendScale ? MathPrecisionBlendScale : lowBlendMilli;
            Volatile.Write(ref _mathPrecisionLowBlendMilli, clampedBlendMilli);
            QueueMathPrecisionShaderState(level, clampedBlendMilli);
        }

        private static void QueueMathPrecisionShaderState(MathPrecisionLevel level, int lowBlendMilli)
        {
            int clampedBlendMilli = lowBlendMilli < 0
                ? 0
                : lowBlendMilli > MathPrecisionBlendScale ? MathPrecisionBlendScale : lowBlendMilli;
            Volatile.Write(ref _pendingMathPrecisionShaderLevel, (int)level);
            Volatile.Write(ref _pendingMathPrecisionShaderLowBlendMilli, clampedBlendMilli);
            Volatile.Write(ref _mathPrecisionShaderDirty, 1);
        }

        internal static void FlushMathPrecisionShaderState()
        {
            if (Volatile.Read(ref _mathPrecisionShaderDirty) == 0)
                return;

            Volatile.Write(ref _mathPrecisionShaderDirty, 0);
            int clampedBlendMilli = Volatile.Read(ref _pendingMathPrecisionShaderLowBlendMilli);
            Shader.SetGlobalFloat(_mathLodLowBlendId, clampedBlendMilli * 0.001f);
        }

        /// <summary>
        /// System-steward scalability tier resolved from the persisted override when present, otherwise the BIOS hardware profile.
        /// </summary>
        public static HectonQualityTier ScalabilityTier
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                int overrideTier = Volatile.Read(ref _scalabilityTierOverride);
                return overrideTier >= 0 ? (HectonQualityTier)overrideTier : QualityTier;
            }
        }

        /// <summary>
        /// Persisted scalability profile byte. Legacy 0/1 and current low/middle/high/ultra values are supported.
        /// </summary>
        public static byte ScalabilityTierProfileByte =>
            ScalabilityTierRuntime.FromQualityTier(ScalabilityTier);

        /// <summary>
        /// Count of persistent native allocations registered with the memory sentinel.
        /// </summary>
        public static int NativeAllocationCount => NativeMemorySentinel.ActiveAllocationCount;

        /// <summary>
        /// Total persistent native bytes registered with the memory sentinel.
        /// </summary>
        public static long NativeTrackedBytes => NativeMemorySentinel.TrackedBytes;

        /// <summary>
        /// Dense multi-instance update registry.
        /// </summary>
        public static RegistryBucket<IUpdatable> Updatables => _updatables;

        /// <summary>
        /// Dense multi-instance render registry.
        /// </summary>
        public static RegistryBucket<IRenderable> Renderables => _renderables;

        /// <summary>
        /// Dense multi-instance fixed-update registry.
        /// </summary>
        public static RegistryBucket<IFixedTickable> FixedTickables => _fixedTickables;

        /// <summary>
        /// Dense multi-instance slow-tick registry.
        /// </summary>
        public static RegistryBucket<ISlowTickable> SlowTickables => _slowTickables;

        /// <summary>
        /// Dense multi-instance frost maintenance registry.
        /// </summary>
        public static RegistryBucket<IFrostTickable> FrostTickables => _frostTickables;

        /// <summary>
        /// Dense registry of explicit service hot-swap listeners.
        /// </summary>
        public static RegistryBucket<IGlobalRegistryHotSwapListener> HotSwapListeners => _hotSwapListeners;

        /// <summary>
        /// Dense registry of native registry-event listeners.
        /// </summary>
        public static RegistryBucket<IRegistryEventListener> RegistryEventListeners => _registryEventListeners;

        public static int PendingServiceReboundCount =>
            Volatile.Read(ref _pendingServiceReboundCount) +
            Volatile.Read(ref _nextFrameServiceReboundCount);

        /// <summary>
        /// Opens the only sanctioned service-registration window.
        /// </summary>
        public static void BeginRegistration()
        {
            int previousPhase = Volatile.Read(ref _registryPhase);
            if (previousPhase == (int)RegistryPhase.Registering)
                return;

            if (previousPhase == (int)RegistryPhase.Ready)
                throw new CriticalBootException("[GlobalRegistry] Ready-locked registry cannot re-open registration.");

            Interlocked.CompareExchange(
                ref _registryPhase,
                (int)RegistryPhase.Registering,
                (int)RegistryPhase.Uninitialized);
        }

        /// <summary>
        /// Locks the registry against further service publication and fails if a requested service never registered.
        /// </summary>
        public static void LockReady()
        {
            AssertNoGhostServicesOrThrow();
            Interlocked.Exchange(ref _registryPhase, (int)RegistryPhase.Ready);
        }

        /// <summary>
        /// Opens the narrow scene-load publication lane for scene-owned runtime services after bootstrap has locked.
        /// Core bootstrap slots remain immutable; only hot-swappable scene slots can publish while this gate is open.
        /// </summary>
        internal static void BeginSceneRuntimePublicationGate()
        {
            if (!Application.isPlaying)
                return;

            Interlocked.Increment(ref _sceneRuntimePublicationGateDepth);
        }

        /// <summary>
        /// Closes one scene-load publication lane opened by <see cref="BeginSceneRuntimePublicationGate"/>.
        /// </summary>
        internal static void EndSceneRuntimePublicationGate()
        {
            if (!Application.isPlaying)
                return;

            int current = Volatile.Read(ref _sceneRuntimePublicationGateDepth);
            while (current > 0)
            {
                int next = current - 1;
                int observed = Interlocked.CompareExchange(ref _sceneRuntimePublicationGateDepth, next, current);
                if (observed == current)
                    return;

                current = observed;
            }
        }

        /// <summary>
        /// Returns a registered service through the guarded BIOS access lane.
        /// Editor/development builds throw on premature access; release builds return a safe null-object or null fallback.
        /// </summary>
        /// <typeparam name="T">Registry-owned service type.</typeparam>
        /// <returns>Registered service or release fallback.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Preserve]
        public static T Get<T>() where T : class
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GuardGenericGetDuringRegistration<T>();
#endif
            GlobalRegistryServiceSlot serviceSlot = ResolveServiceSlot<T>();
            if (Volatile.Read(ref _registryPhase) != (int)RegistryPhase.Ready)
                MarkServiceRequested(serviceSlot);

            if (TryReadRegisteredService(serviceSlot, out T service))
                return service;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            throw new CriticalBootException("[GlobalRegistry] Premature service access: " + typeof(T).Name);
#else
            return ResolveSafeFallbackService<T>();
#endif
        }

        /// <summary>
        /// Attempts to read a registered service without triggering the critical boot guard.
        /// </summary>
        /// <typeparam name="T">Registry-owned service type.</typeparam>
        /// <param name="service">Registered service when present.</param>
        /// <returns>True when the service slot is registered.</returns>
        [Preserve]
        public static bool TryGet<T>(out T service) where T : class
        {
            GlobalRegistryServiceSlot serviceSlot = ResolveServiceSlot<T>();
            return TryReadRegisteredService(serviceSlot, out service);
        }

        private static bool TryReadRegisteredService<T>(GlobalRegistryServiceSlot serviceSlot, out T service) where T : class
        {
            if (typeof(T) == typeof(IBabelLocalization))
            {
                service = (_babelLocalizationRuntime ?? _localizationRuntime) as T;
                if (service != null)
                    return true;
            }

            service = ResolveRegisteredServiceObject(serviceSlot) as T;
            return service != null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ShutdownRegisteredServices();
            ConfigureBootstrapRegistryBridge();
            Interlocked.Exchange(ref _registryPhase, (int)RegistryPhase.Uninitialized);
            Array.Clear(_requestedServiceSlotMask, 0, _requestedServiceSlotMask.Length);
            Array.Clear(_registeredServiceSlotMask, 0, _registeredServiceSlotMask.Length);
            _activeBootProfile = BootConfigurationProfile.Normal;
            _safeModeBootRequested = false;
            _lowMemoryProfileEnabled = false;
            _activeServiceTypeHash = 0u;
            _absoluteUniverseTimeBits = 0L;
            _mathPrecisionLevel = (int)MathPrecisionLevel.Low;
            _mathPrecisionTargetLevel = (int)MathPrecisionLevel.Low;
            _mathPrecisionTransitionFramesRemaining = 0;
            _mathPrecisionTransitionTotalFrames = 0;
            _mathPrecisionLowBlendMilli = MathPrecisionBlendScale;
            _pendingMathPrecisionShaderLevel = (int)MathPrecisionLevel.Low;
            _pendingMathPrecisionShaderLowBlendMilli = MathPrecisionBlendScale;
            _mathPrecisionShaderDirty = 0;
            _sceneRuntimePublicationGateDepth = 0;
            BulkheadContainmentIntentBus.UnbindDataVault(null);
            SignalBusRegistry.ClearSystemKillSwitchBits();
            _currentDomain = (int)Domain.Unknown;
            _currentDomainOwner = null;
            ApplyMathPrecisionShaderState(MathPrecisionLevel.Low, MathPrecisionBlendScale);
            _celestialRuntimeSnapshot = default;
            _celestialRuntimeSnapshotSequence = 0;
            _celestialLightReadabilitySnapshot = default;
            _celestialLightReadabilitySequence = 0;
            _threadInput = null;
            _threadPhysics = null;
            _threadTickManager = null;
            _threadTelemetry = null;
            _threadAudio = null;
            _threadAudioVirtualization = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _registeringGetViolationLogged = false;
            // Bump the generation rather than clearing a flag: that re-arms every ReadyLockViolationLatch<T>
            // in one write, which matters for repeated in-editor Play sessions with no domain reload.
            Interlocked.Increment(ref _readyLockViolationGeneration);
            Volatile.Write(ref _readyLockViolationCount, 0);
#endif
            _input = null;
            _inputBinding = null;
            _nativeInputManagerRuntime = null;
            _raycastBatchRuntime = null;
            _physics = null;
            _cablePhysics132Runtime = null;
            _audio = null;
            _audioVirtualization = null;
            _vocalWarningRuntime = null;
            _scene = null;
            _save = null;
            _ui = null;
            _modalWindowRuntime = null;
            _objectPool = null;
            _player = null;
            _playerMovementContracts = null;
            _playerInventory = null;
            _modularEquipment = null;
            _playerSensory = null;
            _environment = null;
            _chemicalInfluence = null;
            _organicToolHits = null;
            _weather = null;
            _oceanKinematics = null;
            _powerGrid = null;
            _submarine = null;
            _submarineState = null;
            _submarineHullBreach = null;
            _dockingAutopilot = null;
            _interactionSignals = null;
            _debris = null;
            _debrisCompute = null;
            _ambientBiotaRuntime = null;
            _ecosystemDirector = null;
            _faunaSimulation = null;
            _thermodynamicsService = null;
            _fluidSimulation = null;
            _logistics = null;
            _habitatGraph = null;
            _habitatDeconstruction = null;
            _fluidPipeGraph = null;
            _gasDynamics = null;
            _worldGen = null;
            _worldSeedProvider = null;
            _prefabRegistryRuntime = null;
            _proceduralFieldSamplerRuntime = null;
            _resourceDistributionRuntime = null;
            _geologyTerrainSeamRuntime = null;
            _geologyVoxelBridgeRuntime = null;
            _voxelEngineRuntime = null;
            _biomeMatrixRuntime = null;
            _underwaterVisualsRuntime = null;
            _giRelayRuntime = null;
            _proceduralSwayDirectorRuntime = null;
            _encounterDirector = null;
            _questSystem = null;
            _persistentWorldRegistry = null;
            _worldStateRuntime = null;
            _pdaLogbook = null;
            _profile = null;
            _celestialEngineRuntime = null;
            _orbitalDirectorRuntime = null;
            _prologueSequenceRuntime = null;
            _fluidRuntime = null;
            _thermodynamicsRuntime = null;
            _narrativeDirectorRuntime = null;
            _corporateOrderRuntime = null;
            _questRuntime = null;
            _cullingRuntime = null;
            _lodSystemRuntime = null;
            _dynamicResolutionRuntime = null;
            _resolutionScalerService = null;
            _impostorRuntime = null;
            _depthZoneRuntime = null;
            _biolumManagerRuntime = null;
            _biolumControllerRuntime = null;
            _localizationRuntime = null;
            _babelLocalizationRuntime = null;
            _audioLogRuntime = null;
            _acousticZoneRuntime = null;
            _surfaceWeatherRuntime = null;
            _atlasSignalRuntime = null;
            _firstHourRuntime = null;
            _emergencyRelayRuntime = null;
            _atmosphereRuntime = null;
            _celestialEngineRuntime = null;
            _terrainProviderRuntime = null;
            _mapMagicRuntime = null;
            _mapMagicVegetationRuntime = null;
            _scavengePopulatorRuntime = null;
            _modWorldPersistenceRuntime = null;
            _moddingBridgeRuntime = null;
            _runModifierRuntime = null;
            _migrationDirectorRuntime = null;
            _basePollutionRuntime = null;
            _entityChangeManagerRuntime = null;
            _performanceMonitorRuntime = null;
            _beaconNetworkRuntime = null;
            _scanLogRuntime = null;
            _toolDurabilityRuntime = null;
            _toolHapticsRuntime = null;
            _vrSomaticProvider = null;
            _loreDatabaseRuntime = null;
            _playerExpressionRuntime = null;
            _spectrumRuntime = null;
            _userOptionsRuntime = null;
            _assetLifecycleRuntime = null;
            _assetLoadDispatcherRuntime = null;
            _vramMonitorRuntime = null;
            _vramPressureRuntime = null;
            _renderTextureLifecycleRuntime = null;
            _renderTexturePoolRuntime = null;
            _abyssalFluidDecalRuntime = null;
            _sargassumDragRuntime = null;
            _sargassumCutRuntime = null;
            _sargassumMicroFaunaRuntime = null;
            _floatingOriginRuntime = null;
            _soundscapeRuntime = null;
            _environmentalStrainRuntime = null;
            _ecosystemHealthRuntime = null;
            _faunaGeneticsRuntime = null;
            _playerExplorationRuntime = null;
            _dynamicDifficultyRuntime = null;
            _discoveryRuntime = null;
            _resourceScarcityRuntime = null;
            _fieldOperationLogRuntime = null;
            _pdaExchangeRuntime = null;
            _playerActionRuntime = null;
            _pdaMarkerRuntime = null;
            _pdaIntrusionRuntime = null;
            _ambientWaterMotionRuntime = null;
            _suitUpgradeRuntime = null;
            _uiAudioFeedbackRuntime = null;
            _uiTooltipRuntime = null;
            _loadingScreenRuntime = null;
            _endingRuntime = null;
            _atlas6DirectiveRuntime = null;
            _hazardZoneRuntime = null;
            _missionRuntime = null;
            _rockManagerRuntime = null;
            _cameraJuiceRuntime = null;
            _musicDirectorRuntime = null;
            _subtitleRuntime = null;
            _atlasSignalDecoderRuntime = null;
            _scrapRuntime = null;
            _autonomousExtractorRuntime = null;
            _visorRTRuntime = null;
            _cameraRTRuntime = null;
            _postFXRTRuntime = null;
            _uiRTRuntime = null;
            _settingsRuntime = null;
            _runtimeWatchdogRuntime = null;
            _gcMonitorRuntime = null;
            _crashTelemetryRuntime = null;
            _playerCriticalAudioRuntime = null;
            _contextualPhysicalIkRuntime = null;
            _tickManager = null;
            _dispatcher = null;
            _renderDispatcher = null;
            _physicsStateManager = null;
            _physicsCullingOverseer = null;
            _environmentRuntimeContextRuntime = null;
            _sceneRuntime = null;
            _sceneInstantiationGateRuntime = null;
            _oceanKinematicsRuntime = null;
            _playerRuntimeContextRuntime = null;
            _playerSensoryRuntime = null;
            _runtimePerformanceProfilerRuntime = null;
            _connectionSplineBatchRendererRuntime = null;
            _causticsRuntime = null;
            _simulationBucketerRuntime = null;
            _streamingBackpressureRuntime = null;
            _foveatedSimulationDirector = null;
            _hardwareThermalService = null;
            _groundRadarRuntime = null;
            _worldResourceSpawnerRuntime = null;
            _instanceCullingRuntime = null;
            _outpostGenerationRuntime = null;
            _prologueSequenceRuntime = null;
            _hardwareProfile = default;
            _scalabilityTierOverride = -1;
            _hasHardwareProfile = false;
            _dispatcherRegistrationErrorLogged = false;
            _inputFallbackWarningPublished = false;
            _inputNullObjectSubstitutionHandedOut = null;
            _vrSomaticNullObjectSubstitutionHandedOut = null;
            DisposeServiceReboundQueuesForShutdown();
            _suppressServiceReboundQueueing = false;
            _resolutionMask = 0u;
            _updatables.Clear();
            _fastTickables.Clear();
            _fixedTickables.Clear();
            _slowTickables.Clear();
            _coldTickables.Clear();
            _frostTickables.Clear();
            _unscaledFastTickables.Clear();
            _renderables.Clear();
            _hotSwapListeners.Clear();
            _registryEventListeners.Clear();
            SystemDispatcher.ClearAllLanes();
#if UNITY_EDITOR
            NativeMemorySentinel.ResetForSubsystemReload();
#else
            NativeMemorySentinel.AssertNoAllocationsAfterServiceShutdown(nameof(ResetStaticState));
            NativeMemorySentinel.ResetForSubsystemReload();
#endif
        }

        internal static void DisposeServiceReboundQueuesForShutdown()
        {
            _suppressServiceReboundQueueing = true;

            DisposeServiceReboundQueue(ref _pendingServiceRebounds, ref _pendingServiceReboundsSentinelId);
            DisposeServiceReboundQueue(ref _nextFrameServiceRebounds, ref _nextFrameServiceReboundsSentinelId);

            ClearServiceReboundReferenceSlots();
            _pendingServiceReboundCount = 0;
            _nextFrameServiceReboundCount = 0;
            _serviceReboundReferenceWriteIndex = 0;
            _serviceReboundReferencePendingCount = 0;
            _serviceReboundOverflowLogged = false;
            _isDispatchingServiceRebounds = false;
        }

        private static void DisposeServiceReboundQueue(ref NativeQueue<RegistryEventPayload> queue, ref int sentinelId)
        {
            Exception firstException = null;

            if (sentinelId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(sentinelId);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
                finally
                {
                    sentinelId = 0;
                }
            }

            if (queue.IsCreated)
            {
                try
                {
                    queue.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
                finally
                {
                    queue = default;
                }
            }
            else
            {
                queue = default;
            }

            if (firstException != null)
                throw firstException;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorServiceReboundTeardownHooks()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= DisposeServiceReboundQueuesForShutdown;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += DisposeServiceReboundQueuesForShutdown;
            UnityEditor.EditorApplication.quitting -= DisposeServiceReboundQueuesForShutdown;
            UnityEditor.EditorApplication.quitting += DisposeServiceReboundQueuesForShutdown;
        }
#endif

        /// <summary>
        /// Registers the immutable boot-time hardware profile before Environment services initialize.
        /// </summary>
        /// <param name="profile">Captured hardware profile.</param>
        public static void RegisterHardwareProfile(in HectonHardwareProfile profile)
        {
            _hardwareProfile = profile;
            _hasHardwareProfile = true;
            SetMathPrecisionLevelImmediate(ResolveEffectiveMathPrecisionLevel());
        }

        /// <summary>
        /// Applies a persisted scalability profile override without mutating immutable hardware facts.
        /// </summary>
        /// <param name="tier">Profile byte. Legacy 0/1 and current low/middle/high/ultra values are supported.</param>
        public static void RegisterScalabilityTierOverride(byte tier)
        {
            Volatile.Write(ref _scalabilityTierOverride, (int)ScalabilityTierRuntime.ToQualityTier(tier));
            SetMathPrecisionLevelImmediate(ResolveEffectiveMathPrecisionLevel());
        }

        /// <summary>
        /// Clears a persisted scalability override and returns to the boot hardware profile.
        /// </summary>
        public static void ClearScalabilityTierOverride()
        {
            Volatile.Write(ref _scalabilityTierOverride, -1);
            SetMathPrecisionLevelImmediate(ResolveEffectiveMathPrecisionLevel());
        }

        private static MathPrecisionLevel ResolveEffectiveMathPrecisionLevel()
        {
            int overrideTier = Volatile.Read(ref _scalabilityTierOverride);
            if (overrideTier >= 0)
            {
                HectonQualityTier qualityTier = (HectonQualityTier)overrideTier;
                return qualityTier == HectonQualityTier.High || qualityTier == HectonQualityTier.Ultra
                    ? MathPrecisionLevel.High
                    : MathPrecisionLevel.Low;
            }

            return _hasHardwareProfile ? _hardwareProfile.MathPrecisionLevel : MathPrecisionLevel.Low;
        }

        /// <summary>
        /// Registers the BIOS-selected math precision tier and queues the shader keyword contract for visual sync.
        /// </summary>
        /// <param name="level">Boot-time precision level.</param>
        public static void RegisterMathPrecisionLevel(MathPrecisionLevel level)
        {
            SetMathPrecisionLevelImmediate(level);
        }

        /// <summary>
        /// Begins the 60-frame high-to-low math-precision degradation ramp.
        /// </summary>
        public static void BeginMathPrecisionDegradation(int currentFrame)
        {
            if ((MathPrecisionLevel)Volatile.Read(ref _mathPrecisionTargetLevel) == MathPrecisionLevel.Low)
                return;

            Volatile.Write(ref _mathPrecisionTargetLevel, (int)MathPrecisionLevel.Low);
            Volatile.Write(ref _mathPrecisionTransitionTotalFrames, MathPrecisionTransitionFrameCount);
            Volatile.Write(ref _mathPrecisionTransitionFramesRemaining, MathPrecisionTransitionFrameCount);
            ApplyMathPrecisionShaderState(MathPrecisionLevel.Low, Volatile.Read(ref _mathPrecisionLowBlendMilli));
        }

        /// <summary>
        /// Advances the math-precision degradation blend. Called once from the dispatcher frame authority.
        /// </summary>
        public static void TickMathPrecisionTransition(int currentFrame)
        {
            int remaining = Volatile.Read(ref _mathPrecisionTransitionFramesRemaining);
            if (remaining <= 0)
                return;

            int total = Volatile.Read(ref _mathPrecisionTransitionTotalFrames);
            if (total <= 0)
            {
                CompleteMathPrecisionTransition();
                return;
            }

            int nextRemaining = remaining - 1;
            Volatile.Write(ref _mathPrecisionTransitionFramesRemaining, nextRemaining);

            int elapsed = total - nextRemaining;
            int blendMilli = elapsed >= total
                ? MathPrecisionBlendScale
                : (elapsed * MathPrecisionBlendScale) / total;
            Volatile.Write(ref _mathPrecisionLowBlendMilli, blendMilli);
            QueueMathPrecisionShaderState((MathPrecisionLevel)Volatile.Read(ref _mathPrecisionTargetLevel), blendMilli);

            if (nextRemaining <= 0)
                CompleteMathPrecisionTransition();
        }

        /// <summary>
        /// Registers the authoritative tick-manager owner.
        /// </summary>
        /// <param name="instance">Tick-manager instance.</param>
        public static void RegisterTickManager(GameTickManager instance)
        {
            RegisterService(ref _tickManager, instance);
        }

        /// <summary>
        /// Registers the authoritative gameplay dispatcher owner.
        /// </summary>
        /// <param name="instance">Dispatcher instance.</param>
        public static void RegisterSystemDispatcher(SystemDispatcher instance)
        {
            RegisterService(ref _dispatcher, instance);
        }

        /// <summary>
        /// Registers the authoritative SRP render dispatcher owner.
        /// </summary>
        /// <param name="instance">Render dispatcher instance.</param>
        public static void RegisterRenderDispatcher(RenderDispatcher instance)
        {
            RegisterService(ref _renderDispatcher, instance);
        }

        /// <summary>
        /// Registers the authoritative global physics-state manager owner.
        /// </summary>
        /// <param name="instance">Physics-state manager instance.</param>
        public static void RegisterPhysicsStateManager(GlobalPhysicsStateManager instance)
        {
            RegisterService(ref _physicsStateManager, instance);
        }

        /// <summary>
        /// Registers the centralized physics culling overseer.
        /// </summary>
        /// <param name="instance">Physics culling overseer instance.</param>
        public static void RegisterPhysicsCullingOverseer(IPhysicsCullingOverseer instance)
        {
            RegisterService(ref _physicsCullingOverseer, instance);
        }

        /// <summary>
        /// Registers the authoritative input service.
        /// </summary>
        /// <param name="instance">Input service instance.</param>
        public static void RegisterInputService(IInputService instance)
        {
            Register(ref _input, instance);
        }

        /// <summary>
        /// Registers the bootstrap-owned native input action owner.
        /// </summary>
        public static void RegisterNativeInputManagerRuntime(INativeInputManagerRuntime instance)
        {
            RegisterServiceAllowSameInstance(ref _nativeInputManagerRuntime, instance);
        }

        /// <summary>
        /// Registers the optional batched raycast helper.
        /// </summary>
        public static void RegisterRaycastBatchRuntime(RaycastBatchHelper instance)
        {
            RegisterServiceAllowSameInstance(ref _raycastBatchRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative input binding service.
        /// </summary>
        public static void RegisterInputBindingService(IInputBindingService instance)
        {
            RegisterService(ref _inputBinding, instance);
        }

        /// <summary>
        /// Registers the authoritative input rebind service through the existing binding slot.
        /// </summary>
        public static void RegisterInputRebindService(IInputRebindService instance)
        {
            RegisterInputBindingService(instance);
        }

        /// <summary>
        /// Registers the authoritative physics service.
        /// </summary>
        /// <param name="instance">Physics service instance.</param>
        public static void RegisterPhysicsService(IPhysicsService instance)
        {
            Register(ref _physics, instance);
        }

        /// <summary>
        /// Registers the authoritative audio service.
        /// </summary>
        /// <param name="instance">Audio service instance.</param>
        public static void RegisterAudioService(IAudioService instance)
        {
            Register(ref _audio, instance);
        }

        /// <summary>
        /// Registers the authoritative acoustic virtual voice scheduler.
        /// </summary>
        /// <param name="instance">Virtualization service instance.</param>
        public static void RegisterAudioVirtualizationService(IAudioVirtualizationService instance)
        {
            RegisterServiceAllowSameInstance(ref _audioVirtualization, instance);
        }

        /// <summary>
        /// Registers the authoritative scene service.
        /// </summary>
        /// <param name="instance">Scene service instance.</param>
        public static void RegisterSceneService(ISceneService instance)
        {
            Register(ref _scene, instance);
            _sceneRuntime = instance as SceneRuntimeService;
        }

        /// <summary>
        /// Registers the authoritative save service.
        /// </summary>
        /// <param name="instance">Save service instance.</param>
        public static void RegisterSaveService(ISaveService instance)
        {
            Register(ref _save, instance);
        }

        /// <summary>
        /// Registers the authoritative async persistence service.
        /// </summary>
        /// <param name="instance">Async persistence service instance.</param>
        public static void RegisterAsyncPersistenceService(IAsyncPersistenceService instance)
        {
            Register(ref _save, instance);
        }

        /// <summary>
        /// Registers the authoritative UI service.
        /// </summary>
        /// <param name="instance">UI service instance.</param>
        public static void RegisterUIService(IUIService instance)
        {
            Register(ref _ui, instance);
        }

        /// <summary>
        /// Registers the authoritative scene modal facade.
        /// </summary>
        /// <param name="instance">Modal facade instance.</param>
        public static void RegisterModalWindowService(IModalWindowService instance)
        {
            RegisterService(ref _modalWindowRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative AR waypoint projection service.
        /// </summary>
        /// <param name="instance">Waypoint projection service instance.</param>
        public static void RegisterARWaypointService(IARWaypointService instance)
        {
            RegisterService(ref _arWaypoint, instance);
        }

        /// <summary>
        /// Registers the authoritative AUP spatial trigger service.
        /// </summary>
        /// <param name="instance">Spatial trigger service instance.</param>
        public static void RegisterSpatialTriggerSystem(ISpatialTriggerSystem instance)
        {
            Register(ref _spatialTriggerSystem, instance);
        }

        /// <summary>
        /// Registers the authoritative object-pool runtime owner.
        /// </summary>
        /// <param name="instance">Object-pool owner instance.</param>
        public static void RegisterObjectPoolService(ObjectPoolManager instance)
        {
            RegisterService(ref _objectPool, instance);
        }

        /// <summary>
        /// Registers the authoritative player runtime context.
        /// </summary>
        /// <param name="instance">Player runtime context instance.</param>
        public static void RegisterPlayerRuntimeContext(IPlayerRuntimeContext instance)
        {
            RegisterService(ref _player, instance);
        }

        /// <summary>
        /// Registers the authoritative player motor service.
        /// </summary>
        /// <param name="instance">Player motor instance.</param>
        public static void RegisterPlayerMotorService(HectonPlayerMotor instance)
        {
            RegisterService(ref _playerMotor, instance);
        }

        /// <summary>
        /// Registers narrow player movement contracts for decoupled gameplay call sites.
        /// </summary>
        /// <param name="instance">Player movement contract owner.</param>
        public static void RegisterPlayerMovementContracts(IPlayerMovementContracts instance)
        {
            RegisterServiceAllowSameInstance(ref _playerMovementContracts, instance);
        }

        /// <summary>
        /// Registers the authoritative player inventory/tooling service.
        /// </summary>
        /// <param name="instance">Player inventory/tooling service instance.</param>
        public static void RegisterPlayerInventoryService(IPlayerInventoryService instance)
        {
            RegisterService(ref _playerInventory, instance);
        }

        /// <summary>
        /// Registers the authoritative modular-equipment runtime service.
        /// </summary>
        public static void RegisterModularEquipmentService(IModularEquipmentService instance)
        {
            RegisterService(ref _modularEquipment, instance);
        }

        /// <summary>
        /// Registers the authoritative player sensory/presentation service.
        /// </summary>
        /// <param name="instance">Player sensory service instance.</param>
        public static void RegisterPlayerSensoryService(IPlayerSensoryService instance)
        {
            RegisterService(ref _playerSensory, instance);
        }

        /// <summary>
        /// Registers the authoritative environment runtime context.
        /// </summary>
        /// <param name="instance">Environment runtime context instance.</param>
        public static void RegisterEnvironmentRuntimeContext(IEnvironmentRuntimeContext instance)
        {
            RegisterService(ref _environment, instance);
            _environmentRuntimeContextRuntime = instance as EnvironmentRuntimeContextService;
        }

        /// <summary>
        /// Registers the authoritative chemical influence read model.
        /// </summary>
        /// <param name="instance">Chemical influence owner instance.</param>
        public static void RegisterChemicalInfluenceReadModel(IChemicalInfluenceReadModel instance)
        {
            RegisterServiceAllowSameInstance(ref _chemicalInfluence, instance);
        }

        /// <summary>
        /// Registers the authoritative organic tool-hit command owner.
        /// </summary>
        /// <param name="instance">Organic command owner instance.</param>
        public static void RegisterOrganicToolHitService(IOrganicToolHitService instance)
        {
            RegisterServiceAllowSameInstance(ref _organicToolHits, instance);
        }

        /// <summary>
        /// Registers the authoritative weather service.
        /// </summary>
        /// <param name="instance">Weather service instance.</param>
        public static void RegisterWeatherService(IWeatherService instance)
        {
            RegisterService(ref _weather, instance);
        }

        /// <summary>
        /// Registers the deterministic seismic and harmonic-tide director.
        /// </summary>
        /// <param name="instance">Seismic director instance.</param>
        public static void RegisterSeismicDirector(ISeismicDirector instance)
        {
            RegisterService(ref _seismicDirectorRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative ocean-kinematics selector service.
        /// </summary>
        /// <param name="instance">Ocean-kinematics service instance.</param>
        public static void RegisterOceanKinematicsService(IHectonOceanKinematicsService instance)
        {
            Register(ref _oceanKinematics, instance);
            _oceanKinematicsRuntime = instance as OceanKinematicsRuntimeService;
        }

        /// <summary>
        /// Registers the authoritative power-grid runtime service.
        /// </summary>
        /// <param name="instance">Power-grid runtime service instance.</param>
        public static void RegisterPowerGridService(IPowerGridService instance)
        {
            RegisterService(ref _powerGrid, instance);
        }

        /// <summary>
        /// Registers the authoritative submarine runtime root.
        /// </summary>
        /// <param name="instance">Submarine runtime root instance.</param>
        public static void RegisterSubmarine(ISubmarineRuntimeContext instance)
        {
            RegisterService(ref _submarine, instance);
        }

        /// <summary>
        /// Registers the authoritative submarine ballast and stabilizer read model.
        /// </summary>
        /// <param name="instance">Submarine state owner instance.</param>
        public static void RegisterSubmarineState(ISubmarineState instance)
        {
            RegisterService(ref _submarineState, instance);
        }

        /// <summary>
        /// Registers the authoritative submarine hull-breach read model.
        /// </summary>
        /// <param name="instance">Submarine hull-breach read model instance.</param>
        public static void RegisterSubmarineHullBreach(ISubmarineHullBreachReadModel instance)
        {
            RegisterService(ref _submarineHullBreach, instance);
        }

        /// <summary>
        /// Registers the authoritative dead-reckoning inertial navigation service.
        /// </summary>
        /// <param name="instance">Inertial navigation service instance.</param>
        public static void RegisterInertialNavigationService(IInertialNavigationService instance)
        {
            RegisterService(ref _inertialNavigation, instance);
        }

        /// <summary>
        /// Registers the authoritative autonomous vehicle docking spline service.
        /// </summary>
        /// <param name="instance">Docking autopilot service instance.</param>
        public static void RegisterDockingAutopilotService(IDockingAutopilotService instance)
        {
            RegisterService(ref _dockingAutopilot, instance);
        }

        /// <summary>
        /// Registers the authoritative interaction signal service.
        /// </summary>
        /// <param name="instance">Interaction signal service instance.</param>
        public static void RegisterInteractionSignalService(IInteractionSignalService instance)
        {
            RegisterService(ref _interactionSignals, instance);
        }

        /// <summary>
        /// Registers the authoritative debris service.
        /// </summary>
        /// <param name="instance">Debris service instance.</param>
        public static void RegisterDebrisService(IDebrisService instance)
        {
            RegisterService(ref _debris, instance);
        }

        /// <summary>
        /// Registers the authoritative GPU-resident debris shard service.
        /// </summary>
        /// <param name="instance">GPU debris service instance.</param>
        public static void RegisterDebrisComputeService(IDebrisComputeService instance)
        {
            RegisterService(ref _debrisCompute, instance);
        }

        /// <summary>
        /// Registers the authoritative GPU-resident ambient biota service.
        /// </summary>
        /// <param name="instance">Ambient biota service instance.</param>
        public static void RegisterAmbientBiotaRuntime(IAmbientBiotaService instance)
        {
            RegisterServiceAllowSameInstance(ref _ambientBiotaRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative ecosystem sector simulation service.
        /// </summary>
        /// <param name="instance">Ecosystem director service instance.</param>
        public static void RegisterEcosystemDirectorService(IEcosystemDirectorService instance)
        {
            RegisterService(ref _ecosystemDirector, instance);
        }

        /// <summary>
        /// Registers the authoritative data-only fauna simulation service.
        /// </summary>
        /// <param name="instance">Fauna simulation service instance.</param>
        public static void RegisterFaunaSimulationService(IFaunaSim instance)
        {
            RegisterService(ref _faunaSimulation, instance);
        }

        /// <summary>
        /// Registers the authoritative thermodynamics service.
        /// </summary>
        /// <param name="instance">Thermodynamics service instance.</param>
        public static void RegisterThermodynamicsService(IThermodynamicsService instance)
        {
            RegisterServiceAllowSameInstance(ref _thermodynamicsService, instance);
        }

        /// <summary>
        /// Registers the authoritative data-only fluid simulation service.
        /// </summary>
        /// <param name="instance">Fluid simulation service instance.</param>
        public static void RegisterFluidSimulationService(IFluidSim instance)
        {
            RegisterService(ref _fluidSimulation, instance);
        }

        /// <summary>
        /// Registers the authoritative logistics/build-network service.
        /// </summary>
        /// <param name="instance">Logistics service instance.</param>
        public static void RegisterLogisticsService(ILogisticsService instance)
        {
            RegisterServiceAllowSameInstance(ref _logistics, instance);
        }

        /// <summary>
        /// Registers the authoritative habitat graph flood read model.
        /// </summary>
        /// <param name="instance">Habitat graph service instance.</param>
        public static void RegisterHabitatGraphService(IHabitatGraphService instance)
        {
            RegisterAllowSameInstance(ref _habitatGraph, instance);
        }

        /// <summary>
        /// Registers the authoritative habitat deconstruction validation and rollback service.
        /// </summary>
        /// <param name="instance">Habitat deconstruction service instance.</param>
        public static void RegisterHabitatDeconstructionSystem(IHabitatDeconstructionSystem instance)
        {
            RegisterServiceAllowSameInstance(ref _habitatDeconstruction, instance);
        }

        /// <summary>
        /// Registers the authoritative fluid pipe pressure graph service.
        /// </summary>
        /// <param name="instance">Fluid pipe graph service instance.</param>
        public static void RegisterFluidPipeGraphService(IFluidPipeGraphService instance)
        {
            RegisterServiceAllowSameInstance(ref _fluidPipeGraph, instance);
        }

        /// <summary>
        /// Registers the authoritative Dalton gas dynamics solver.
        /// </summary>
        /// <param name="instance">Gas dynamics solver instance.</param>
        public static void RegisterGasDynamicsSolver(IGasDynamicsSolver instance)
        {
            RegisterAllowSameInstance(ref _gasDynamics, instance);
        }

        /// <summary>
        /// Registers the authoritative world-generation service.
        /// </summary>
        /// <param name="instance">World-generation service instance.</param>
        public static void RegisterWorldGenService(IWorldGenService instance)
        {
            RegisterServiceAllowSameInstance(ref _worldGen, instance);
        }

        /// <summary>
        /// Registers the deterministic world-seed provider.
        /// </summary>
        /// <param name="instance">World-seed provider instance.</param>
        public static void RegisterWorldSeedProvider(IWorldSeedProvider instance)
        {
            RegisterServiceAllowSameInstance(ref _worldSeedProvider, instance);
        }

        /// <summary>
        /// Registers the authoritative prefab ID registry owner.
        /// </summary>
        public static void RegisterPrefabRegistryRuntime(PrefabRegistry instance)
        {
            RegisterServiceAllowSameInstance(ref _prefabRegistryRuntime, instance);
        }

        /// <summary>
        /// Clears the authoritative prefab ID registry owner when the owner matches.
        /// </summary>
        public static void ClearPrefabRegistryRuntime(PrefabRegistry instance)
        {
            if (instance == null || ReferenceEquals(_prefabRegistryRuntime, instance))
                _prefabRegistryRuntime = null;
        }

        /// <summary>
        /// Registers the authoritative procedural field sampler runtime owner.
        /// </summary>
        public static void RegisterProceduralFieldSampler(WorldProceduralFieldSampler instance)
        {
            RegisterServiceAllowSameInstance(ref _proceduralFieldSamplerRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative resource-distribution runtime owner.
        /// </summary>
        public static void RegisterResourceDistribution(ResourceDistributionDirector instance)
        {
            RegisterServiceAllowSameInstance(ref _resourceDistributionRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative terrain/voxel seam applier runtime owner.
        /// </summary>
        public static void RegisterGeologyTerrainSeamRuntime(WorldGenerativeGeologyTerrainSeamApplier instance)
        {
            RegisterServiceAllowSameInstance(ref _geologyTerrainSeamRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative geology voxel bridge runtime owner.
        /// </summary>
        public static void RegisterGeologyVoxelBridgeRuntime(WorldGenerativeGeologyVoxelBridgeDirector instance)
        {
            RegisterServiceAllowSameInstance(ref _geologyVoxelBridgeRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative voxel generation/runtime owner.
        /// </summary>
        public static void RegisterVoxelEngineRuntime(HectonVoxelEngine instance)
        {
            RegisterServiceAllowSameInstance(ref _voxelEngineRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative biome matrix runtime owner.
        /// </summary>
        public static void RegisterBiomeMatrixRuntime(BiomeMatrixDirector instance)
        {
            RegisterServiceAllowSameInstance(ref _biomeMatrixRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative underwater visuals runtime owner.
        /// </summary>
        public static void RegisterUnderwaterVisualsRuntime(HectonUnderwaterVisuals instance)
        {
            RegisterServiceAllowSameInstance(ref _underwaterVisualsRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative day/night GI relay runtime owner.
        /// </summary>
        public static void RegisterGIRelayRuntime(IGIRelaySystem instance)
        {
            RegisterServiceAllowSameInstance(ref _giRelayRuntime, instance);
            if (instance != null && ReferenceEquals(_giRelayRuntime, instance))
            {
                RenderDispatcher.BindGIRelayCold(instance);
                RenderSettingsLifecycleGuard.BindGIRelayCold(instance);
            }
        }

        /// <summary>
        /// Registers the authoritative procedural flora sway director.
        /// </summary>
        public static void RegisterProceduralSwayDirector(IProceduralSwayDirector instance)
        {
            RegisterAllowSameInstance(ref _proceduralSwayDirectorRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative mathematical wake displacement service.
        /// </summary>
        public static void RegisterWakeDisplacementService(IWakeDisplacementService instance)
        {
            if (instance is IProceduralSwayDirector proceduralSwayDirector)
                RegisterProceduralSwayDirector(proceduralSwayDirector);
        }

        /// <summary>
        /// Registers the authoritative encounter-direction service.
        /// </summary>
        /// <param name="instance">Encounter-direction service instance.</param>
        public static void RegisterEncounterDirectorService(IEncounterDirectorService instance)
        {
            RegisterServiceAllowSameInstance(ref _encounterDirector, instance);
        }

        /// <summary>
        /// Registers the authoritative quest-system service.
        /// </summary>
        /// <param name="instance">Quest-system service instance.</param>
        public static void RegisterQuestSystem(IQuestSystem instance)
        {
            RegisterServiceAllowSameInstance(ref _questSystem, instance);
        }

        /// <summary>
        /// Registers the authoritative persistent world registry owner.
        /// </summary>
        public static void RegisterPersistentWorldRegistry(PersistentWorldRegistry instance)
        {
            RegisterServiceAllowSameInstance(ref _persistentWorldRegistry, instance);
        }

        /// <summary>
        /// Registers the authoritative world-state persistence runtime owner.
        /// </summary>
        public static void RegisterWorldStateRuntime(WorldStateManager instance)
        {
            RegisterServiceAllowSameInstance(ref _worldStateRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative PDA logbook append service.
        /// </summary>
        public static void RegisterPDALogbookService(IPDALogbookService instance)
        {
            RegisterServiceAllowSameInstance(ref _pdaLogbook, instance);
        }

        /// <summary>
        /// Registers the authoritative global profile service.
        /// </summary>
        public static void RegisterProfileService(IProfileService instance)
        {
            RegisterServiceAllowSameInstance(ref _profile, instance);
        }

        /// <summary>
        /// Registers the authoritative celestial-engine runtime owner.
        /// </summary>
        public static void RegisterCelestialEngineRuntime(HectonCelestialEngine instance)
        {
            RegisterServiceAllowSameInstance(ref _celestialEngineRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative orbital prologue director runtime owner.
        /// </summary>
        public static void RegisterOrbitalDirectorRuntime(IOrbitalDirector instance)
        {
            RegisterServiceAllowSameInstance(ref _orbitalDirectorRuntime, instance);
        }

        /// <summary>
        /// Hot-swaps the scene-owned orbital prologue director after the bootstrap registration window is locked.
        /// </summary>
        public static void ReplaceOrbitalDirectorRuntime(IOrbitalDirector instance)
        {
            ReplaceService(ref _orbitalDirectorRuntime, instance, GlobalRegistryServiceSlot.OrbitalDirectorRuntime);
        }

        /// <summary>
        /// Registers the awaitable prologue sequence runtime owner.
        /// </summary>
        public static void RegisterPrologueSequenceRuntime(IPrologueSequenceService instance)
        {
            RegisterServiceAllowSameInstance(ref _prologueSequenceRuntime, instance);
        }

        /// <summary>
        /// Hot-swaps the scene-owned awaitable prologue sequence after the bootstrap registration window is locked.
        /// </summary>
        public static void ReplacePrologueSequenceRuntime(IPrologueSequenceService instance)
        {
            ReplaceService(ref _prologueSequenceRuntime, instance, GlobalRegistryServiceSlot.PrologueSequenceRuntime);
        }

        /// <summary>
        /// Registers the authoritative eclipse-gameplay runtime owner.
        /// </summary>
        public static void RegisterEclipseGameplayRuntime(EclipseGameplaySystem instance)
        {
            RegisterService(ref _eclipseGameplayRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative random-event runtime owner.
        /// </summary>
        public static void RegisterRandomEventRuntime(RandomEventSystem instance)
        {
            RegisterService(ref _randomEventRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative fluid simulation runtime owner.
        /// </summary>
        public static void RegisterFluidRuntime(HectonFluidEngine instance)
        {
            RegisterService(ref _fluidRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative thermodynamic simulation runtime owner.
        /// </summary>
        public static void RegisterThermodynamicsRuntime(AbyssalThermalManager instance)
        {
            RegisterServiceAllowSameInstance(ref _thermodynamicsRuntime, instance);

            if (instance is IThermodynamicsService thermodynamicsService)
                RegisterThermodynamicsService(thermodynamicsService);
        }

        /// <summary>
        /// Registers the authoritative narrative runtime owner.
        /// </summary>
        public static void RegisterNarrativeDirectorRuntime(HectonNarrativeDirector instance)
        {
            RegisterService(ref _narrativeDirectorRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative corporate-order runtime owner.
        /// </summary>
        public static void RegisterCorporateOrderRuntime(CorporateOrderSystem instance)
        {
            RegisterServiceAllowSameInstance(ref _corporateOrderRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative quest runtime owner.
        /// </summary>
        public static void RegisterQuestRuntime(QuestManager instance)
        {
            RegisterServiceAllowSameInstance(ref _questRuntime, instance);

            if (instance is IQuestSystem questSystem)
                RegisterQuestSystem(questSystem);
        }

        /// <summary>
        /// Registers the authoritative world-culling runtime owner.
        /// </summary>
        public static void RegisterCullingRuntime(CullingManager instance)
        {
            RegisterServiceAllowSameInstance(ref _cullingRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative world LOD runtime owner.
        /// </summary>
        public static void RegisterLODSystemRuntime(LODSystemManager instance)
        {
            RegisterServiceAllowSameInstance(ref _lodSystemRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative dynamic-resolution runtime owner.
        /// </summary>
        public static void RegisterDynamicResolutionRuntime(DynamicResolutionScaler instance)
        {
            RegisterServiceAllowSameInstance(ref _dynamicResolutionRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative STP render-scale policy service.
        /// </summary>
        public static void RegisterResolutionScalerService(IResolutionScalerService instance)
        {
            RegisterServiceAllowSameInstance(ref _resolutionScalerService, instance);
        }

        /// <summary>
        /// Registers the authoritative impostor runtime owner.
        /// </summary>
        public static void RegisterImpostorRuntime(ImpostorSystem instance)
        {
            RegisterServiceAllowSameInstance(ref _impostorRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative depth-zone runtime owner.
        /// </summary>
        public static void RegisterDepthZoneRuntime(DepthZoneDirector instance)
        {
            RegisterServiceAllowSameInstance(ref _depthZoneRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative world bioluminescence runtime owner.
        /// </summary>
        public static void RegisterBiolumManagerRuntime(HectonBiolumManager instance)
        {
            RegisterServiceAllowSameInstance(ref _biolumManagerRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative bioluminescence shader-controller runtime owner.
        /// </summary>
        public static void RegisterBiolumControllerRuntime(HectonBiolumController instance)
        {
            RegisterServiceAllowSameInstance(ref _biolumControllerRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative localization runtime owner.
        /// </summary>
        public static void RegisterLocalizationRuntime(LocalizationManager instance)
        {
            RegisterServiceAllowSameInstance(ref _localizationRuntime, instance);
        }

        /// <summary>
        /// Registers an allocation-free Babel localization provider without requiring the concrete localization manager.
        /// </summary>
        public static void RegisterBabelLocalizationRuntime(IBabelLocalization instance)
        {
            if (instance == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[GlobalRegistry] Cannot register null as IBabelLocalization.");
#endif
                return;
            }

            ForceOverrideToken effectiveToken = ResolveSceneRuntimePublicationToken(
                GlobalRegistryServiceSlot.LocalizationRuntime,
                default);
            GuardServicePublication<IBabelLocalization>(effectiveToken);
            IBabelLocalization previousService = Volatile.Read(ref _babelLocalizationRuntime);
            if (ReferenceEquals(previousService, instance))
            {
                MarkServiceRegistered(GlobalRegistryServiceSlot.LocalizationRuntime);
                return;
            }

            if (previousService != null && !effectiveToken.IsValid)
                ThrowSlotHijack(previousService, instance);

            if (effectiveToken.IsValid)
            {
                previousService = Interlocked.Exchange(ref _babelLocalizationRuntime, instance);
            }
            else
            {
                previousService = Interlocked.CompareExchange(ref _babelLocalizationRuntime, instance, null);
            }

            if (previousService != null && !ReferenceEquals(previousService, instance) && !effectiveToken.IsValid)
                ThrowSlotHijack(previousService, instance);

            MarkServiceRegistered(GlobalRegistryServiceSlot.LocalizationRuntime);
            if (previousService != null)
                QueueServiceRebound(GlobalRegistryServiceSlot.LocalizationRuntime, previousService, instance);
        }

        /// <summary>
        /// Registers the authoritative audio-log runtime owner.
        /// </summary>
        public static void RegisterAudioLogRuntime(AudioLogSystem instance)
        {
            RegisterServiceAllowSameInstance(ref _audioLogRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative crash telemetry runtime owner.
        /// </summary>
        public static void RegisterCrashTelemetryRuntime(CrashTelemetryBuffer instance)
        {
            RegisterServiceAllowSameInstance(ref _crashTelemetryRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative player critical procedural audio owner.
        /// </summary>
        public static void RegisterPlayerCriticalAudioRuntime(PlayerCriticalProceduralAudioRenderer instance)
        {
            RegisterServiceAllowSameInstance(ref _playerCriticalAudioRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative vocal warning queue/runtime owner.
        /// </summary>
        public static void RegisterVocalWarningRuntime(IVocalWarningSystem instance)
        {
            RegisterServiceAllowSameInstance(ref _vocalWarningRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative contextual physical IK runtime owner.
        /// </summary>
        internal static void RegisterContextualPhysicalIkRuntime(ContextualPhysicalIkRuntime instance)
        {
            RegisterServiceAllowSameInstance(ref _contextualPhysicalIkRuntime, instance);
        }

        /// <summary>
        /// Clears the authoritative contextual physical IK runtime owner.
        /// </summary>
        internal static void ClearContextualPhysicalIkRuntime(ContextualPhysicalIkRuntime instance)
        {
            if (instance == null || ReferenceEquals(_contextualPhysicalIkRuntime, instance))
                _contextualPhysicalIkRuntime = null;
        }

        /// <summary>
        /// Registers the authoritative procedural ladder climb IK runtime owner.
        /// </summary>
        internal static void RegisterProceduralLadderClimbRuntime(ProceduralLadderClimbRuntime instance)
        {
            RegisterServiceAllowSameInstance(ref _proceduralLadderClimbRuntime, instance);
        }

        /// <summary>
        /// Clears the authoritative procedural ladder climb IK runtime owner.
        /// </summary>
        internal static void ClearProceduralLadderClimbRuntime(ProceduralLadderClimbRuntime instance)
        {
            if (instance == null || ReferenceEquals(_proceduralLadderClimbRuntime, instance))
                _proceduralLadderClimbRuntime = null;
        }

        /// <summary>
        /// Registers the authoritative acoustic-zone runtime owner.
        /// </summary>
        public static void RegisterAcousticZoneRuntime(AcousticZoneController instance)
        {
            RegisterServiceAllowSameInstance(ref _acousticZoneRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative surface-weather runtime owner.
        /// </summary>
        public static void RegisterSurfaceWeatherRuntime(HectonSurfaceWeatherDirector instance)
        {
            RegisterServiceAllowSameInstance(ref _surfaceWeatherRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative Atlas signal runtime owner.
        /// </summary>
        public static void RegisterAtlasSignalRuntime(AtlasSignalSystem instance)
        {
            RegisterServiceAllowSameInstance(ref _atlasSignalRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative first-hour pacing runtime owner.
        /// </summary>
        public static void RegisterFirstHourRuntime(FirstHourDirector instance)
        {
            RegisterServiceAllowSameInstance(ref _firstHourRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative emergency relay runtime owner.
        /// </summary>
        public static void RegisterEmergencyRelayRuntime(EmergencyServiceRelayDirector instance)
        {
            RegisterServiceAllowSameInstance(ref _emergencyRelayRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative atmosphere runtime owner.
        /// </summary>
        public static void RegisterAtmosphereRuntime(HectonAtmosphereManager instance)
        {
            RegisterServiceAllowSameInstance(ref _atmosphereRuntime, instance);
            if (instance != null && ReferenceEquals(_atmosphereRuntime, instance))
                RenderSettingsLifecycleGuard.BindAtmosphereCold(instance);
        }

        /// <summary>
        /// Registers the authoritative terrain sampling provider.
        /// </summary>
        public static void RegisterTerrainProvider(ITerrainProvider instance)
        {
            RegisterAllowSameInstance(ref _terrainProviderRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative MapMagic bridge runtime owner.
        /// </summary>
        public static void RegisterMapMagicRuntime(MapMagicBridge instance)
        {
            RegisterServiceAllowSameInstance(ref _mapMagicRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative MapMagic vegetation runtime owner.
        /// </summary>
        public static void RegisterMapMagicVegetationRuntime(HectonMapMagicVegetationBridge instance)
        {
            RegisterServiceAllowSameInstance(ref _mapMagicVegetationRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative scavenge populator runtime owner.
        /// </summary>
        public static void RegisterScavengePopulatorRuntime(ScavengePopulator instance)
        {
            RegisterServiceAllowSameInstance(ref _scavengePopulatorRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative mod world persistence runtime owner.
        /// </summary>
        internal static void RegisterModWorldPersistenceRuntime(ModWorldPersistenceManager instance)
        {
            RegisterServiceAllowSameInstance(ref _modWorldPersistenceRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative native-to-managed mod projection bridge.
        /// </summary>
        public static void RegisterModdingBridgeRuntime(IModdingBridge instance)
        {
            RegisterServiceAllowSameInstance(ref _moddingBridgeRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative run-modifier runtime owner.
        /// </summary>
        public static void RegisterRunModifierRuntime(RunModifierController instance)
        {
            RegisterServiceAllowSameInstance(ref _runModifierRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative meta-campaign progression runtime owner.
        /// </summary>
        public static void RegisterMetaCampaignService(IMetaCampaignService instance)
        {
            RegisterServiceAllowSameInstance(ref _metaCampaignRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative global fauna migration runtime owner.
        /// </summary>
        public static void RegisterMigrationDirectorRuntime(MigrationDirector instance)
        {
            RegisterServiceAllowSameInstance(ref _migrationDirectorRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative base-pollution runtime owner.
        /// </summary>
        public static void RegisterBasePollutionRuntime(BasePollutionManager instance)
        {
            RegisterServiceAllowSameInstance(ref _basePollutionRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative entity-change manager runtime owner.
        /// </summary>
        public static void RegisterEntityChangeManagerRuntime(EntityChangeManager instance)
        {
            RegisterServiceAllowSameInstance(ref _entityChangeManagerRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative core performance monitor runtime owner.
        /// </summary>
        public static void RegisterPerformanceMonitorRuntime(PerformanceMonitor instance)
        {
            RegisterServiceAllowSameInstance(ref _performanceMonitorRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative beacon-network runtime owner.
        /// </summary>
        public static void RegisterBeaconNetworkRuntime(BeaconNetworkSystem instance)
        {
            RegisterServiceAllowSameInstance(ref _beaconNetworkRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative scan-log runtime owner.
        /// </summary>
        public static void RegisterScanLogRuntime(ScanLogSystem instance)
        {
            RegisterServiceAllowSameInstance(ref _scanLogRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative tool-durability runtime owner.
        /// </summary>
        public static void RegisterToolDurabilityRuntime(ToolDurabilitySystem instance)
        {
            RegisterServiceAllowSameInstance(ref _toolDurabilityRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative tool haptics runtime owner.
        /// </summary>
        public static void RegisterToolHapticsRuntime(ToolHapticsRuntime instance)
        {
            RegisterServiceAllowSameInstance(ref _toolHapticsRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative VR somatic provider.
        /// </summary>
        public static void RegisterVRSomaticProvider(IVRSomaticProvider instance)
        {
            RegisterServiceAllowSameInstance(ref _vrSomaticProvider, instance);
        }

        /// <summary>
        /// Registers the authoritative lore database runtime owner.
        /// </summary>
        public static void RegisterLoreDatabaseRuntime(LoreDatabaseManager instance)
        {
            RegisterServiceAllowSameInstance(ref _loreDatabaseRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative player expression/profile runtime owner.
        /// </summary>
        public static void RegisterPlayerExpressionRuntime(PlayerExpressionManager instance)
        {
            RegisterServiceAllowSameInstance(ref _playerExpressionRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative visor spectrum runtime owner.
        /// </summary>
        public static void RegisterSpectrumRuntime(SpectrumSystem instance)
        {
            RegisterServiceAllowSameInstance(ref _spectrumRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative user-options persistence runtime owner.
        /// </summary>
        public static void RegisterUserOptionsRuntime(UserOptionsPersistence instance)
        {
            RegisterServiceAllowSameInstance(ref _userOptionsRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative asset residency governor runtime owner.
        /// </summary>
        public static void RegisterAssetLifecycleRuntime(AssetLifecycleGovernor instance)
        {
            RegisterServiceAllowSameInstance(ref _assetLifecycleRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative asset load dispatcher runtime owner.
        /// </summary>
        public static void RegisterAssetLoadDispatcherRuntime(AssetLoadDispatcher instance)
        {
            RegisterServiceAllowSameInstance(ref _assetLoadDispatcherRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative VRAM monitor runtime owner.
        /// </summary>
        public static void RegisterVRAMMonitorRuntime(VRAMMonitor instance)
        {
            RegisterServiceAllowSameInstance(ref _vramMonitorRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative VRAM pressure response runtime owner.
        /// </summary>
        public static void RegisterVRAMPressureRuntime(VRAMPressureMonitor instance)
        {
            RegisterServiceAllowSameInstance(ref _vramPressureRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative RenderTexture lifecycle tracker runtime owner.
        /// </summary>
        public static void RegisterRenderTextureLifecycleRuntime(RenderTextureLifecycleTracker instance)
        {
            RegisterServiceAllowSameInstance(ref _renderTextureLifecycleRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative RenderTexture pool runtime owner.
        /// </summary>
        public static void RegisterRenderTexturePoolRuntime(RenderTexturePool instance)
        {
            RegisterServiceAllowSameInstance(ref _renderTexturePoolRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative abyssal fluid aftermath decal runtime owner.
        /// </summary>
        public static void RegisterAbyssalFluidDecalRuntime(AbyssalFluidDecalManager instance)
        {
            RegisterServiceAllowSameInstance(ref _abyssalFluidDecalRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative sargassum global drag-field runtime owner.
        /// </summary>
        public static void RegisterSargassumDragRuntime(SargassumGlobalDragManager instance)
        {
            RegisterServiceAllowSameInstance(ref _sargassumDragRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative sargassum cut-mask runtime owner.
        /// </summary>
        public static void RegisterSargassumCutRuntime(SargassumCutManager instance)
        {
            RegisterServiceAllowSameInstance(ref _sargassumCutRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative sargassum micro-fauna boid runtime owner.
        /// </summary>
        public static void RegisterSargassumMicroFaunaRuntime(SargassumMicroFaunaBoids instance)
        {
            RegisterServiceAllowSameInstance(ref _sargassumMicroFaunaRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative floating-origin runtime owner.
        /// </summary>
        public static void RegisterFloatingOriginRuntime(HectonFloatingOrigin instance)
        {
            RegisterServiceAllowSameInstance(ref _floatingOriginRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative environmental soundscape runtime owner.
        /// </summary>
        public static void RegisterSoundscapeRuntime(SoundscapeSystem instance)
        {
            RegisterServiceAllowSameInstance(ref _soundscapeRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative environmental strain runtime owner.
        /// </summary>
        public static void RegisterEnvironmentalStrainRuntime(EnvironmentalStrainManager instance)
        {
            RegisterServiceAllowSameInstance(ref _environmentalStrainRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative ecosystem health runtime owner.
        /// </summary>
        public static void RegisterEcosystemHealthRuntime(EcosystemHealthDirector instance)
        {
            RegisterServiceAllowSameInstance(ref _ecosystemHealthRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative fauna genetics runtime owner.
        /// </summary>
        public static void RegisterFaunaGeneticsRuntime(FaunaGeneticsManager instance)
        {
            RegisterServiceAllowSameInstance(ref _faunaGeneticsRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative player exploration runtime owner.
        /// </summary>
        public static void RegisterPlayerExplorationRuntime(PlayerExplorationTracker instance)
        {
            RegisterServiceAllowSameInstance(ref _playerExplorationRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative discovery runtime owner.
        /// </summary>
        public static void RegisterDiscoveryRuntime(HectonDiscoveryManager instance)
        {
            RegisterServiceAllowSameInstance(ref _discoveryRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative dynamic difficulty runtime owner.
        /// </summary>
        public static void RegisterDynamicDifficultyRuntime(DynamicDifficultyDirector instance)
        {
            RegisterServiceAllowSameInstance(ref _dynamicDifficultyRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative resource scarcity runtime owner.
        /// </summary>
        public static void RegisterResourceScarcityRuntime(ResourceScarcityDirector instance)
        {
            RegisterServiceAllowSameInstance(ref _resourceScarcityRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative field-operation log runtime owner.
        /// </summary>
        public static void RegisterFieldOperationLogRuntime(FieldOperationLogSystem instance)
        {
            RegisterServiceAllowSameInstance(ref _fieldOperationLogRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative PDA exchange runtime owner.
        /// </summary>
        public static void RegisterPDAExchangeRuntime(PDAExchangeSystem instance)
        {
            RegisterServiceAllowSameInstance(ref _pdaExchangeRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative player action runtime owner.
        /// </summary>
        public static void RegisterPlayerActionRuntime(PlayerActionController instance)
        {
            RegisterServiceAllowSameInstance(ref _playerActionRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative PDA marker registry runtime owner.
        /// </summary>
        public static void RegisterPDAMarkerRuntime(PDAMarkerRegistry instance)
        {
            RegisterServiceAllowSameInstance(ref _pdaMarkerRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative PDA intrusion runtime owner.
        /// </summary>
        public static void RegisterPDAIntrusionRuntime(PDAIntrusionManager instance)
        {
            RegisterServiceAllowSameInstance(ref _pdaIntrusionRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative ambient water-motion runtime owner.
        /// </summary>
        public static void RegisterAmbientWaterMotionRuntime(AmbientWaterMotionManager instance)
        {
            RegisterServiceAllowSameInstance(ref _ambientWaterMotionRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative suit upgrade runtime owner.
        /// </summary>
        public static void RegisterSuitUpgradeRuntime(SuitUpgradeManager instance)
        {
            RegisterServiceAllowSameInstance(ref _suitUpgradeRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative UI audio feedback runtime owner.
        /// </summary>
        public static void RegisterUIAudioFeedbackRuntime(UIAudioFeedback instance)
        {
            RegisterServiceAllowSameInstance(ref _uiAudioFeedbackRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative UI tooltip runtime owner.
        /// </summary>
        public static void RegisterUITooltipRuntime(UITooltip instance)
        {
            RegisterServiceAllowSameInstance(ref _uiTooltipRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative loading screen runtime owner.
        /// </summary>
        public static void RegisterLoadingScreenRuntime(LoadingScreenController instance)
        {
            RegisterServiceAllowSameInstance(ref _loadingScreenRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative ending runtime owner.
        /// </summary>
        public static void RegisterEndingRuntime(EndingSystem instance)
        {
            RegisterServiceAllowSameInstance(ref _endingRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative Atlas-6 directive runtime owner.
        /// </summary>
        public static void RegisterAtlas6DirectiveRuntime(Atlas6DirectiveSystem instance)
        {
            RegisterServiceAllowSameInstance(ref _atlas6DirectiveRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative hazard-zone runtime owner.
        /// </summary>
        public static void RegisterHazardZoneRuntime(HazardZoneManager instance)
        {
            RegisterServiceAllowSameInstance(ref _hazardZoneRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative mission facade runtime owner.
        /// </summary>
        public static void RegisterMissionRuntime(MissionManager instance)
        {
            RegisterServiceAllowSameInstance(ref _missionRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative rock rendering/proximity runtime owner.
        /// </summary>
        public static void RegisterRockManagerRuntime(HectonRockManager instance)
        {
            RegisterServiceAllowSameInstance(ref _rockManagerRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative camera presentation feedback runtime owner.
        /// </summary>
        public static void RegisterCameraJuiceRuntime(ICameraJuiceSystem instance)
        {
            RegisterServiceAllowSameInstance(ref _cameraJuiceRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative adaptive music director runtime owner.
        /// </summary>
        public static void RegisterMusicDirectorRuntime(HectonMusicDirector instance)
        {
            RegisterServiceAllowSameInstance(ref _musicDirectorRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative subtitle presentation runtime owner.
        /// </summary>
        public static void RegisterSubtitleRuntime(SubtitleManager instance)
        {
            RegisterServiceAllowSameInstance(ref _subtitleRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative Atlas signal decoder runtime owner.
        /// </summary>
        public static void RegisterAtlasSignalDecoderRuntime(AtlasSignalDecoder instance)
        {
            RegisterServiceAllowSameInstance(ref _atlasSignalDecoderRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative recycling/scrap runtime owner.
        /// </summary>
        public static void RegisterScrapRuntime(ScrapManager instance)
        {
            RegisterServiceAllowSameInstance(ref _scrapRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative autonomous extractor SOA runtime owner.
        /// </summary>
        public static void RegisterAutonomousExtractorRuntime(AutonomousExtractorSystem instance)
        {
            RegisterServiceAllowSameInstance(ref _autonomousExtractorRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative visor RenderTexture budget monitor runtime owner.
        /// </summary>
        public static void RegisterVisorRTRuntime(VisorRTManager instance)
        {
            RegisterServiceAllowSameInstance(ref _visorRTRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative camera RenderTexture budget monitor runtime owner.
        /// </summary>
        public static void RegisterCameraRTRuntime(CameraRTManager instance)
        {
            RegisterServiceAllowSameInstance(ref _cameraRTRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative post-processing RenderTexture budget monitor runtime owner.
        /// </summary>
        public static void RegisterPostFXRTRuntime(PostFXRTManager instance)
        {
            RegisterServiceAllowSameInstance(ref _postFXRTRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative UI RenderTexture budget monitor runtime owner.
        /// </summary>
        public static void RegisterUIRTRuntime(UIRTManager instance)
        {
            RegisterServiceAllowSameInstance(ref _uiRTRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative user settings runtime owner.
        /// </summary>
        public static void RegisterSettingsRuntime(SettingsManager instance)
        {
            RegisterServiceAllowSameInstance(ref _settingsRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative bootstrap runtime owner.
        /// </summary>
        public static void RegisterBootstrapperRuntime(GameBootstrapper instance)
        {
            RegisterServiceAllowSameInstance(ref _bootstrapperRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative global data-vault service.
        /// </summary>
        public static void RegisterDataVault(IDataVault instance)
        {
            RegisterService(ref _dataVault, instance);
            MathGuard.BindDataVaultCold(instance);
            SignalBusRegistry.BindDataVaultCold(instance);
            GlobalTelemetryBus.BindBlackboxDataVaultCold(instance);
        }

        /// <summary>
        /// Registers the authoritative SHINOBU 132 cable physics service.
        /// </summary>
        public static void RegisterCablePhysics132Runtime(ICablePhysics132Service instance)
        {
            RegisterServiceAllowSameInstance(ref _cablePhysics132Runtime, instance);
        }

        /// <summary>
        /// Clears the authoritative global data-vault service.
        /// </summary>
        public static void UnregisterDataVault(IDataVault instance)
        {
            if (ReferenceEquals(_dataVault, instance))
            {
                Arm64AlignmentTelemetry.ReleaseOwnedBuffers(instance);
                BulkheadContainmentIntentBus.UnbindDataVault(instance);
                ReleaseSignalDataVaultOwnedHandles();
                MathGuard.BindDataVaultCold(null);
                SignalBusRegistry.BindDataVaultCold(null);
                GlobalTelemetryBus.BindBlackboxDataVaultCold(null);
            }

            UnregisterService(ref _dataVault, instance);
        }

        private static void ReleaseSignalDataVaultOwnedHandles()
        {
            SignalTuningTable.ReleaseHandlesOnly();
            SignalTelemetryRingBuffer.ReleaseHandlesOnly();
            SignalThreadLocalScratchpad.ReleaseHandlesOnly();
        }

        /// <summary>
        /// Clears the authoritative SHINOBU 132 cable physics service.
        /// </summary>
        public static void UnregisterCablePhysics132Runtime(ICablePhysics132Service instance)
        {
            UnregisterService(ref _cablePhysics132Runtime, instance);
        }

        /// <summary>
        /// Registers the authoritative macro database pager service.
        /// </summary>
        public static void RegisterMacroDatabase(IMacroDatabaseService instance)
        {
            RegisterService(ref _macroDatabase, instance);
        }

        /// <summary>
        /// Clears the authoritative macro database pager service.
        /// </summary>
        public static void UnregisterMacroDatabase(IMacroDatabaseService instance)
        {
            UnregisterService(ref _macroDatabase, instance);
        }

        /// <summary>
        /// Registers the authoritative underwater caustics presentation service.
        /// </summary>
        public static void RegisterCausticsService(ICausticsService instance)
        {
            RegisterServiceAllowSameInstance(ref _causticsRuntime, instance);
        }

        /// <summary>
        /// Clears the authoritative underwater caustics presentation service.
        /// </summary>
        public static void UnregisterCausticsService(ICausticsService instance)
        {
            UnregisterService(ref _causticsRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative Burst job admission service.
        /// </summary>
        public static void RegisterJobAdmissionRuntime(IJobAdmissionService instance)
        {
            RegisterServiceAllowSameInstance(ref _jobAdmissionRuntime, instance);
        }

        /// <summary>
        /// Clears the authoritative Burst job admission service.
        /// </summary>
        public static void UnregisterJobAdmissionRuntime(IJobAdmissionService instance)
        {
            UnregisterService(ref _jobAdmissionRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative modulo simulation time-slicer service.
        /// </summary>
        public static void RegisterSimulationBucketerRuntime(ISimulationBucketer instance)
        {
            RegisterServiceAllowSameInstance(ref _simulationBucketerRuntime, instance);
        }

        /// <summary>
        /// Clears the authoritative modulo simulation time-slicer service.
        /// </summary>
        public static void UnregisterSimulationBucketerRuntime(ISimulationBucketer instance)
        {
            UnregisterService(ref _simulationBucketerRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative streaming IO backpressure service.
        /// </summary>
        public static void RegisterStreamingBackpressureRuntime(IStreamingBackpressureService instance)
        {
            RegisterServiceAllowSameInstance(ref _streamingBackpressureRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative subsurface GPR service.
        /// </summary>
        public static void RegisterGroundRadarService(IGroundRadarService instance)
        {
            RegisterServiceAllowSameInstance(ref _groundRadarRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative world resource SoA read model.
        /// </summary>
        public static void RegisterWorldResourceSpawner(IWorldResourceSpawnerReadModel instance)
        {
            RegisterServiceAllowSameInstance(ref _worldResourceSpawnerRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative procedural GPU instance culling service.
        /// </summary>
        public static void RegisterInstanceCullingService(IInstanceCullingService instance)
        {
            RegisterServiceAllowSameInstance(ref _instanceCullingRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative deterministic outpost generation service.
        /// </summary>
        public static void RegisterOutpostGenerationService(IOutpostGenerationService instance)
        {
            RegisterServiceAllowSameInstance(ref _outpostGenerationRuntime, instance);
        }

        /// <summary>
        /// Clears the authoritative streaming IO backpressure service.
        /// </summary>
        public static void UnregisterStreamingBackpressureRuntime(IStreamingBackpressureService instance)
        {
            UnregisterService(ref _streamingBackpressureRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative foveated AI simulation director.
        /// </summary>
        public static void RegisterFoveatedSimulationDirector(IFoveatedSimulationDirector instance)
        {
            RegisterServiceAllowSameInstance(ref _foveatedSimulationDirector, instance);
        }

        /// <summary>
        /// Registers the authoritative hardware thermal and battery watchdog service.
        /// </summary>
        public static void RegisterHardwareThermalService(IHardwareThermalService instance)
        {
            RegisterServiceAllowSameInstance(ref _hardwareThermalService, instance);
        }

        /// <summary>
        /// Hot-swaps the hardware thermal and battery watchdog after the bootstrap registration window is locked.
        /// </summary>
        public static void ReplaceHardwareThermalService(IHardwareThermalService instance)
        {
            ReplaceService(ref _hardwareThermalService, instance, GlobalRegistryServiceSlot.HardwareThermalService);
        }

        /// <summary>
        /// Clears the authoritative foveated AI simulation director.
        /// </summary>
        public static void UnregisterFoveatedSimulationDirector(IFoveatedSimulationDirector instance)
        {
            UnregisterService(ref _foveatedSimulationDirector, instance);
        }

        /// <summary>
        /// Clears the authoritative hardware thermal and battery watchdog service.
        /// </summary>
        public static void UnregisterHardwareThermalService(IHardwareThermalService instance)
        {
            UnregisterService(ref _hardwareThermalService, instance);
        }

        /// <summary>
        /// Clears the authoritative subsurface GPR service.
        /// </summary>
        public static void UnregisterGroundRadarService(IGroundRadarService instance)
        {
            UnregisterService(ref _groundRadarRuntime, instance);
        }

        /// <summary>
        /// Clears the authoritative world resource SoA read model.
        /// </summary>
        public static void UnregisterWorldResourceSpawner(IWorldResourceSpawnerReadModel instance)
        {
            UnregisterService(ref _worldResourceSpawnerRuntime, instance);
        }

        /// <summary>
        /// Clears the authoritative procedural GPU instance culling service.
        /// </summary>
        public static void UnregisterInstanceCullingService(IInstanceCullingService instance)
        {
            UnregisterService(ref _instanceCullingRuntime, instance);
        }

        /// <summary>
        /// Clears the authoritative deterministic outpost generation service.
        /// </summary>
        public static void UnregisterOutpostGenerationService(IOutpostGenerationService instance)
        {
            UnregisterService(ref _outpostGenerationRuntime, instance);
        }

        /// <summary>
        /// Clears the authoritative bootstrap runtime owner when the registered component is destroyed.
        /// </summary>
        public static void ClearBootstrapperRuntime(GameBootstrapper instance)
        {
            if (instance == null || ReferenceEquals(_bootstrapperRuntime, instance))
                _bootstrapperRuntime = null;
        }

        /// <summary>
        /// Registers the authoritative scene-instantiation gate.
        /// </summary>
        internal static void RegisterSceneInstantiationGateRuntime(SceneInstantiationGate instance)
        {
            RegisterServiceAllowSameInstance(ref _sceneInstantiationGateRuntime, instance);
        }

        /// <summary>
        /// Clears the authoritative scene-instantiation gate.
        /// </summary>
        internal static void ClearSceneInstantiationGateRuntime(SceneInstantiationGate instance)
        {
            if (instance == null || ReferenceEquals(_sceneInstantiationGateRuntime, instance))
                _sceneInstantiationGateRuntime = null;
        }

        /// <summary>
        /// Registers the authoritative scene runtime component.
        /// </summary>
        internal static void RegisterSceneRuntime(SceneRuntimeService instance)
        {
            RegisterServiceAllowSameInstance(ref _sceneRuntime, instance);
        }

        /// <summary>
        /// Clears the authoritative scene runtime component.
        /// </summary>
        internal static void ClearSceneRuntime(SceneRuntimeService instance)
        {
            if (instance == null || ReferenceEquals(_sceneRuntime, instance))
                _sceneRuntime = null;
        }

        /// <summary>
        /// Registers the authoritative shader-bent connection renderer runtime owner.
        /// </summary>
        internal static void RegisterConnectionSplineBatchRendererRuntime(ConnectionSplineBatchRenderer instance)
        {
            RegisterServiceAllowSameInstance(ref _connectionSplineBatchRendererRuntime, instance);
        }

        /// <summary>
        /// Clears the authoritative shader-bent connection renderer runtime owner.
        /// </summary>
        internal static void UnregisterConnectionSplineBatchRendererRuntime(ConnectionSplineBatchRenderer instance)
        {
            UnregisterService(ref _connectionSplineBatchRendererRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative environment runtime component before interface boot.
        /// </summary>
        internal static void RegisterEnvironmentRuntimeContextRuntime(EnvironmentRuntimeContextService instance)
        {
            RegisterServiceAllowSameInstance(ref _environmentRuntimeContextRuntime, instance);
        }

        /// <summary>
        /// Clears the authoritative environment runtime component.
        /// </summary>
        internal static void ClearEnvironmentRuntimeContextRuntime(EnvironmentRuntimeContextService instance)
        {
            if (instance == null || ReferenceEquals(_environmentRuntimeContextRuntime, instance))
                _environmentRuntimeContextRuntime = null;
        }

        /// <summary>
        /// Registers the authoritative ocean kinematics runtime component.
        /// </summary>
        internal static void RegisterOceanKinematicsRuntime(OceanKinematicsRuntimeService instance)
        {
            RegisterServiceAllowSameInstance(ref _oceanKinematicsRuntime, instance);
        }

        /// <summary>
        /// Clears the authoritative ocean kinematics runtime component.
        /// </summary>
        internal static void ClearOceanKinematicsRuntime(OceanKinematicsRuntimeService instance)
        {
            if (instance == null || ReferenceEquals(_oceanKinematicsRuntime, instance))
                _oceanKinematicsRuntime = null;
        }

        /// <summary>
        /// Registers the authoritative player runtime context component.
        /// </summary>
        internal static void RegisterPlayerRuntimeContextRuntime(PlayerRuntimeContextService instance)
        {
            RegisterServiceAllowSameInstance(ref _playerRuntimeContextRuntime, instance);
        }

        /// <summary>
        /// Clears the authoritative player runtime context component.
        /// </summary>
        internal static void ClearPlayerRuntimeContextRuntime(PlayerRuntimeContextService instance)
        {
            if (instance == null || ReferenceEquals(_playerRuntimeContextRuntime, instance))
                _playerRuntimeContextRuntime = null;
        }

        /// <summary>
        /// Registers the authoritative player sensory runtime component.
        /// </summary>
        internal static void RegisterPlayerSensoryRuntime(PlayerSensoryManager instance)
        {
            RegisterServiceAllowSameInstance(ref _playerSensoryRuntime, instance);
        }

        /// <summary>
        /// Clears the authoritative player sensory runtime component.
        /// </summary>
        internal static void ClearPlayerSensoryRuntime(PlayerSensoryManager instance)
        {
            if (instance == null || ReferenceEquals(_playerSensoryRuntime, instance))
                _playerSensoryRuntime = null;
        }

        /// <summary>
        /// Registers the authoritative runtime liveness watchdog owner.
        /// </summary>
        public static void RegisterRuntimeWatchdogRuntime(RuntimeWatchdog instance)
        {
            RegisterServiceAllowSameInstance(ref _runtimeWatchdogRuntime, instance);
        }

        /// <summary>
        /// Registers the authoritative development GC sentinel owner.
        /// </summary>
        internal static void RegisterGCMonitorRuntime(GCMonitor instance)
        {
            RegisterServiceAllowSameInstance(ref _gcMonitorRuntime, instance);
        }

        /// <summary>
        /// Clears the authoritative development GC sentinel owner.
        /// </summary>
        internal static void ClearGCMonitorRuntime(GCMonitor instance)
        {
            if (instance == null || ReferenceEquals(_gcMonitorRuntime, instance))
                _gcMonitorRuntime = null;
        }

        /// <summary>
        /// Registers the authoritative development runtime profiler.
        /// </summary>
        internal static void RegisterRuntimePerformanceProfilerRuntime(RuntimePerformanceProfiler instance)
        {
            RegisterServiceAllowSameInstance(ref _runtimePerformanceProfilerRuntime, instance);
        }

        /// <summary>
        /// Clears the authoritative development runtime profiler.
        /// </summary>
        internal static void ClearRuntimePerformanceProfilerRuntime(RuntimePerformanceProfiler instance)
        {
            if (instance == null || ReferenceEquals(_runtimePerformanceProfilerRuntime, instance))
                _runtimePerformanceProfilerRuntime = null;
        }

        /// <summary>
        /// Unregisters the current input service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterInputService(IInputService instance)
        {
            UnregisterService(ref _input, instance);
        }

        /// <summary>
        /// Unregisters the bootstrap-owned native input action owner if the owner matches.
        /// </summary>
        public static void UnregisterNativeInputManagerRuntime(INativeInputManagerRuntime instance)
        {
            UnregisterService(ref _nativeInputManagerRuntime, instance);
        }

        /// <summary>
        /// Unregisters the optional batched raycast helper if the owner matches.
        /// </summary>
        public static void UnregisterRaycastBatchRuntime(RaycastBatchHelper instance)
        {
            UnregisterService(ref _raycastBatchRuntime, instance);
        }

        /// <summary>
        /// Unregisters the authoritative input binding service.
        /// </summary>
        public static void UnregisterInputBindingService(IInputBindingService instance)
        {
            UnregisterService(ref _inputBinding, instance);
        }

        /// <summary>
        /// Unregisters the current physics service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterPhysicsService(IPhysicsService instance)
        {
            UnregisterService(ref _physics, instance);
        }

        /// <summary>
        /// Unregisters the current audio service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterAudioService(IAudioService instance)
        {
            UnregisterService(ref _audio, instance);
        }

        /// <summary>
        /// Unregisters the current acoustic virtual voice scheduler if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterAudioVirtualizationService(IAudioVirtualizationService instance)
        {
            UnregisterService(ref _audioVirtualization, instance);
        }

        /// <summary>
        /// Unregisters the current scene service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterSceneService(ISceneService instance)
        {
            UnregisterService(ref _scene, instance);
            if (ReferenceEquals(_sceneRuntime, instance))
                _sceneRuntime = null;
        }

        /// <summary>
        /// Unregisters the current save service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterSaveService(ISaveService instance)
        {
            UnregisterService(ref _save, instance);
        }

        /// <summary>
        /// Unregisters the current UI service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterUIService(IUIService instance)
        {
            UnregisterService(ref _ui, instance);
        }

        /// <summary>
        /// Unregisters the current scene modal facade if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterModalWindowService(IModalWindowService instance)
        {
            UnregisterService(ref _modalWindowRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current AR waypoint projection service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterARWaypointService(IARWaypointService instance)
        {
            UnregisterService(ref _arWaypoint, instance);
        }

        /// <summary>
        /// Unregisters the current AUP spatial trigger service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterSpatialTriggerSystem(ISpatialTriggerSystem instance)
        {
            UnregisterService(ref _spatialTriggerSystem, instance);
        }

        /// <summary>
        /// Unregisters the current object-pool runtime owner if the owner matches.
        /// </summary>
        /// <param name="instance">Object-pool owner requesting unregistration.</param>
        public static void UnregisterObjectPoolService(ObjectPoolManager instance)
        {
            UnregisterService(ref _objectPool, instance);
        }

        /// <summary>
        /// Unregisters the current player runtime context if the owner matches.
        /// </summary>
        /// <param name="instance">Context owner requesting unregistration.</param>
        public static void UnregisterPlayerRuntimeContext(IPlayerRuntimeContext instance)
        {
            UnregisterService(ref _player, instance);
            if (ReferenceEquals(_playerRuntimeContextRuntime, instance))
                _playerRuntimeContextRuntime = null;
        }

        /// <summary>
        /// Unregisters the current player motor service if the owner matches.
        /// </summary>
        /// <param name="instance">Player motor requesting unregistration.</param>
        public static void UnregisterPlayerMotorService(HectonPlayerMotor instance)
        {
            UnregisterService(ref _playerMotor, instance);
        }

        /// <summary>
        /// Unregisters current player movement contracts if the owner matches.
        /// </summary>
        /// <param name="instance">Player movement contract owner requesting unregistration.</param>
        public static void UnregisterPlayerMovementContracts(IPlayerMovementContracts instance)
        {
            UnregisterService(ref _playerMovementContracts, instance);
        }

        /// <summary>
        /// Unregisters the current player inventory/tooling service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterPlayerInventoryService(IPlayerInventoryService instance)
        {
            UnregisterService(ref _playerInventory, instance);
        }

        /// <summary>
        /// Unregisters the current modular-equipment runtime service if the owner matches.
        /// </summary>
        public static void UnregisterModularEquipmentService(IModularEquipmentService instance)
        {
            UnregisterService(ref _modularEquipment, instance);
        }

        /// <summary>
        /// Unregisters the current player sensory/presentation service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterPlayerSensoryService(IPlayerSensoryService instance)
        {
            UnregisterService(ref _playerSensory, instance);
            if (ReferenceEquals(_playerSensoryRuntime, instance))
                _playerSensoryRuntime = null;
        }

        /// <summary>
        /// Unregisters the current environment runtime context if the owner matches.
        /// </summary>
        /// <param name="instance">Context owner requesting unregistration.</param>
        public static void UnregisterEnvironmentRuntimeContext(IEnvironmentRuntimeContext instance)
        {
            UnregisterService(ref _environment, instance);
            if (ReferenceEquals(_environmentRuntimeContextRuntime, instance))
                _environmentRuntimeContextRuntime = null;
        }

        /// <summary>
        /// Unregisters the current chemical influence read model if the owner matches.
        /// </summary>
        /// <param name="instance">Read-model owner requesting unregistration.</param>
        public static void UnregisterChemicalInfluenceReadModel(IChemicalInfluenceReadModel instance)
        {
            UnregisterService(ref _chemicalInfluence, instance);
        }

        /// <summary>
        /// Unregisters the current organic tool-hit command owner if the owner matches.
        /// </summary>
        /// <param name="instance">Command owner requesting unregistration.</param>
        public static void UnregisterOrganicToolHitService(IOrganicToolHitService instance)
        {
            UnregisterService(ref _organicToolHits, instance);
        }

        /// <summary>
        /// Unregisters the current weather service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterWeatherService(IWeatherService instance)
        {
            UnregisterService(ref _weather, instance);
        }

        /// <summary>
        /// Unregisters the current seismic director if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterSeismicDirector(ISeismicDirector instance)
        {
            UnregisterService(ref _seismicDirectorRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current ocean-kinematics selector service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterOceanKinematicsService(IHectonOceanKinematicsService instance)
        {
            UnregisterService(ref _oceanKinematics, instance);
            if (ReferenceEquals(_oceanKinematicsRuntime, instance))
                _oceanKinematicsRuntime = null;
        }

        /// <summary>
        /// Unregisters the current power-grid runtime service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterPowerGridService(IPowerGridService instance)
        {
            UnregisterService(ref _powerGrid, instance);
        }

        /// <summary>
        /// Unregisters the current submarine runtime root if the owner matches.
        /// </summary>
        /// <param name="instance">Submarine runtime root requesting unregistration.</param>
        public static void UnregisterSubmarine(ISubmarineRuntimeContext instance)
        {
            UnregisterService(ref _submarine, instance);
        }

        /// <summary>
        /// Unregisters the current submarine ballast and stabilizer read model if the owner matches.
        /// </summary>
        /// <param name="instance">Read-model owner requesting unregistration.</param>
        public static void UnregisterSubmarineState(ISubmarineState instance)
        {
            UnregisterService(ref _submarineState, instance);
        }

        /// <summary>
        /// Unregisters the current submarine hull-breach read model if the owner matches.
        /// </summary>
        /// <param name="instance">Read-model owner requesting unregistration.</param>
        public static void UnregisterSubmarineHullBreach(ISubmarineHullBreachReadModel instance)
        {
            UnregisterService(ref _submarineHullBreach, instance);
        }

        /// <summary>
        /// Unregisters the current dead-reckoning inertial navigation service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterInertialNavigationService(IInertialNavigationService instance)
        {
            UnregisterService(ref _inertialNavigation, instance);
        }

        /// <summary>
        /// Unregisters the current autonomous vehicle docking spline service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterDockingAutopilotService(IDockingAutopilotService instance)
        {
            UnregisterService(ref _dockingAutopilot, instance);
        }

        /// <summary>
        /// Unregisters the current interaction signal service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterInteractionSignalService(IInteractionSignalService instance)
        {
            UnregisterService(ref _interactionSignals, instance);
        }

        /// <summary>
        /// Unregisters the current debris service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterDebrisService(IDebrisService instance)
        {
            UnregisterService(ref _debris, instance);
        }

        /// <summary>
        /// Unregisters the current GPU debris service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterDebrisComputeService(IDebrisComputeService instance)
        {
            UnregisterService(ref _debrisCompute, instance);
        }

        /// <summary>
        /// Unregisters the current ambient biota service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterAmbientBiotaRuntime(IAmbientBiotaService instance)
        {
            UnregisterService(ref _ambientBiotaRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current ecosystem sector simulation service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterEcosystemDirectorService(IEcosystemDirectorService instance)
        {
            UnregisterService(ref _ecosystemDirector, instance);
        }

        /// <summary>
        /// Unregisters the current data-only fauna simulation service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterFaunaSimulationService(IFaunaSim instance)
        {
            UnregisterService(ref _faunaSimulation, instance);
        }

        /// <summary>
        /// Unregisters the current thermodynamics service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterThermodynamicsService(IThermodynamicsService instance)
        {
            UnregisterService(ref _thermodynamicsService, instance);
        }

        /// <summary>
        /// Unregisters the current data-only fluid simulation service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterFluidSimulationService(IFluidSim instance)
        {
            UnregisterService(ref _fluidSimulation, instance);
        }

        /// <summary>
        /// Unregisters the current logistics/build-network service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterLogisticsService(ILogisticsService instance)
        {
            UnregisterService(ref _logistics, instance);
        }

        /// <summary>
        /// Unregisters the current habitat graph flood read model if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterHabitatGraphService(IHabitatGraphService instance)
        {
            UnregisterService(ref _habitatGraph, instance);
        }

        /// <summary>
        /// Unregisters the current habitat deconstruction service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterHabitatDeconstructionSystem(IHabitatDeconstructionSystem instance)
        {
            UnregisterService(ref _habitatDeconstruction, instance);
        }

        /// <summary>
        /// Unregisters the current fluid pipe pressure graph if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterFluidPipeGraphService(IFluidPipeGraphService instance)
        {
            UnregisterService(ref _fluidPipeGraph, instance);
        }

        /// <summary>
        /// Unregisters the current Dalton gas dynamics solver if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterGasDynamicsSolver(IGasDynamicsSolver instance)
        {
            UnregisterService(ref _gasDynamics, instance);
        }

        /// <summary>
        /// Unregisters the current world-generation service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterWorldGenService(IWorldGenService instance)
        {
            UnregisterService(ref _worldGen, instance);
        }

        /// <summary>
        /// Unregisters the current deterministic world-seed provider if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterWorldSeedProvider(IWorldSeedProvider instance)
        {
            UnregisterService(ref _worldSeedProvider, instance);
        }

        /// <summary>
        /// Unregisters the current prefab ID registry owner if the owner matches.
        /// </summary>
        public static void UnregisterPrefabRegistryRuntime(PrefabRegistry instance)
        {
            UnregisterService(ref _prefabRegistryRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current procedural field sampler runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterProceduralFieldSampler(WorldProceduralFieldSampler instance)
        {
            UnregisterService(ref _proceduralFieldSamplerRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current resource-distribution runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterResourceDistribution(ResourceDistributionDirector instance)
        {
            UnregisterService(ref _resourceDistributionRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current terrain/voxel seam applier runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterGeologyTerrainSeamRuntime(WorldGenerativeGeologyTerrainSeamApplier instance)
        {
            UnregisterService(ref _geologyTerrainSeamRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current geology voxel bridge runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterGeologyVoxelBridgeRuntime(WorldGenerativeGeologyVoxelBridgeDirector instance)
        {
            UnregisterService(ref _geologyVoxelBridgeRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current voxel generation/runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterVoxelEngineRuntime(HectonVoxelEngine instance)
        {
            UnregisterService(ref _voxelEngineRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current biome matrix runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterBiomeMatrixRuntime(BiomeMatrixDirector instance)
        {
            UnregisterService(ref _biomeMatrixRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current underwater visuals runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterUnderwaterVisualsRuntime(HectonUnderwaterVisuals instance)
        {
            UnregisterService(ref _underwaterVisualsRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current day/night GI relay runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterGIRelayRuntime(IGIRelaySystem instance)
        {
            if (ReferenceEquals(_giRelayRuntime, instance))
            {
                RenderDispatcher.BindGIRelayCold(null);
                RenderSettingsLifecycleGuard.BindGIRelayCold(null);
            }

            UnregisterService(ref _giRelayRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current procedural flora sway director if the owner matches.
        /// </summary>
        public static void UnregisterProceduralSwayDirector(IProceduralSwayDirector instance)
        {
            UnregisterService(ref _proceduralSwayDirectorRuntime, instance);
        }

        /// <summary>
        /// Unregisters the mathematical wake displacement service if the owner matches.
        /// </summary>
        public static void UnregisterWakeDisplacementService(IWakeDisplacementService instance)
        {
            if (instance is IProceduralSwayDirector proceduralSwayDirector)
                UnregisterProceduralSwayDirector(proceduralSwayDirector);
        }

        /// <summary>
        /// Unregisters the current encounter-direction service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterEncounterDirectorService(IEncounterDirectorService instance)
        {
            UnregisterService(ref _encounterDirector, instance);
        }

        /// <summary>
        /// Unregisters the current quest-system service if the owner matches.
        /// </summary>
        /// <param name="instance">Service owner requesting unregistration.</param>
        public static void UnregisterQuestSystem(IQuestSystem instance)
        {
            UnregisterService(ref _questSystem, instance);
        }

        /// <summary>
        /// Unregisters the current persistent world registry if the owner matches.
        /// </summary>
        public static void UnregisterPersistentWorldRegistry(PersistentWorldRegistry instance)
        {
            UnregisterService(ref _persistentWorldRegistry, instance);
        }

        /// <summary>
        /// Unregisters the current world-state persistence runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterWorldStateRuntime(WorldStateManager instance)
        {
            UnregisterService(ref _worldStateRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current PDA logbook service if the owner matches.
        /// </summary>
        public static void UnregisterPDALogbookService(IPDALogbookService instance)
        {
            UnregisterService(ref _pdaLogbook, instance);
        }

        /// <summary>
        /// Unregisters the authoritative global profile service.
        /// </summary>
        public static void UnregisterProfileService(IProfileService instance)
        {
            UnregisterService(ref _profile, instance);
        }

        /// <summary>
        /// Unregisters the current celestial-engine runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterCelestialEngineRuntime(HectonCelestialEngine instance)
        {
            UnregisterService(ref _celestialEngineRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current orbital prologue director runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterOrbitalDirectorRuntime(IOrbitalDirector instance)
        {
            UnregisterService(ref _orbitalDirectorRuntime, instance);
        }

        /// <summary>
        /// Unregisters the awaitable prologue sequence runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterPrologueSequenceRuntime(IPrologueSequenceService instance)
        {
            UnregisterService(ref _prologueSequenceRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current eclipse-gameplay runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterEclipseGameplayRuntime(EclipseGameplaySystem instance)
        {
            UnregisterService(ref _eclipseGameplayRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current random-event runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterRandomEventRuntime(RandomEventSystem instance)
        {
            UnregisterService(ref _randomEventRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current fluid simulation runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterFluidRuntime(HectonFluidEngine instance)
        {
            UnregisterService(ref _fluidRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current thermodynamic simulation runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterThermodynamicsRuntime(AbyssalThermalManager instance)
        {
            UnregisterService(ref _thermodynamicsRuntime, instance);

            if (instance is IThermodynamicsService thermodynamicsService)
                UnregisterThermodynamicsService(thermodynamicsService);
        }

        /// <summary>
        /// Unregisters the current narrative runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterNarrativeDirectorRuntime(HectonNarrativeDirector instance)
        {
            UnregisterService(ref _narrativeDirectorRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current corporate-order runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterCorporateOrderRuntime(CorporateOrderSystem instance)
        {
            UnregisterService(ref _corporateOrderRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current quest runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterQuestRuntime(QuestManager instance)
        {
            UnregisterService(ref _questRuntime, instance);

            if (instance is IQuestSystem questSystem)
                UnregisterQuestSystem(questSystem);
        }

        /// <summary>
        /// Unregisters the current world-culling runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterCullingRuntime(CullingManager instance)
        {
            UnregisterService(ref _cullingRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current world LOD runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterLODSystemRuntime(LODSystemManager instance)
        {
            UnregisterService(ref _lodSystemRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current dynamic-resolution runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterDynamicResolutionRuntime(DynamicResolutionScaler instance)
        {
            UnregisterService(ref _dynamicResolutionRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current STP render-scale policy service if the owner matches.
        /// </summary>
        public static void UnregisterResolutionScalerService(IResolutionScalerService instance)
        {
            UnregisterService(ref _resolutionScalerService, instance);
        }

        /// <summary>
        /// Unregisters the current impostor runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterImpostorRuntime(ImpostorSystem instance)
        {
            UnregisterService(ref _impostorRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current depth-zone runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterDepthZoneRuntime(DepthZoneDirector instance)
        {
            UnregisterService(ref _depthZoneRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current world bioluminescence runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterBiolumManagerRuntime(HectonBiolumManager instance)
        {
            UnregisterService(ref _biolumManagerRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current bioluminescence shader-controller runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterBiolumControllerRuntime(HectonBiolumController instance)
        {
            UnregisterService(ref _biolumControllerRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current localization runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterLocalizationRuntime(LocalizationManager instance)
        {
            UnregisterService(ref _localizationRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current Babel localization provider if the owner matches.
        /// </summary>
        public static void UnregisterBabelLocalizationRuntime(IBabelLocalization instance)
        {
            if (!ReferenceEquals(_babelLocalizationRuntime, instance))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[GlobalRegistry] Unregister mismatch for IBabelLocalization.");
#endif
                return;
            }

            Interlocked.CompareExchange(ref _babelLocalizationRuntime, null, instance);
        }

        /// <summary>
        /// Unregisters the current audio-log runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterAudioLogRuntime(AudioLogSystem instance)
        {
            UnregisterService(ref _audioLogRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current crash telemetry runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterCrashTelemetryRuntime(CrashTelemetryBuffer instance)
        {
            UnregisterService(ref _crashTelemetryRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current player critical procedural audio owner if the owner matches.
        /// </summary>
        public static void UnregisterPlayerCriticalAudioRuntime(PlayerCriticalProceduralAudioRenderer instance)
        {
            UnregisterService(ref _playerCriticalAudioRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current vocal warning runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterVocalWarningRuntime(IVocalWarningSystem instance)
        {
            UnregisterService(ref _vocalWarningRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current acoustic-zone runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterAcousticZoneRuntime(AcousticZoneController instance)
        {
            UnregisterService(ref _acousticZoneRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current surface-weather runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterSurfaceWeatherRuntime(HectonSurfaceWeatherDirector instance)
        {
            UnregisterService(ref _surfaceWeatherRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current Atlas signal runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterAtlasSignalRuntime(AtlasSignalSystem instance)
        {
            UnregisterService(ref _atlasSignalRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current first-hour pacing runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterFirstHourRuntime(FirstHourDirector instance)
        {
            UnregisterService(ref _firstHourRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current emergency relay runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterEmergencyRelayRuntime(EmergencyServiceRelayDirector instance)
        {
            UnregisterService(ref _emergencyRelayRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current atmosphere runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterAtmosphereRuntime(HectonAtmosphereManager instance)
        {
            if (ReferenceEquals(_atmosphereRuntime, instance))
                RenderSettingsLifecycleGuard.BindAtmosphereCold(null);

            UnregisterService(ref _atmosphereRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current terrain sampling provider if the owner matches.
        /// </summary>
        public static void UnregisterTerrainProvider(ITerrainProvider instance)
        {
            UnregisterService(ref _terrainProviderRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current MapMagic bridge runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterMapMagicRuntime(MapMagicBridge instance)
        {
            UnregisterService(ref _mapMagicRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current MapMagic vegetation runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterMapMagicVegetationRuntime(HectonMapMagicVegetationBridge instance)
        {
            UnregisterService(ref _mapMagicVegetationRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current scavenge populator runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterScavengePopulatorRuntime(ScavengePopulator instance)
        {
            UnregisterService(ref _scavengePopulatorRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current mod world persistence runtime owner if the owner matches.
        /// </summary>
        internal static void UnregisterModWorldPersistenceRuntime(ModWorldPersistenceManager instance)
        {
            UnregisterService(ref _modWorldPersistenceRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current native-to-managed mod projection bridge if the owner matches.
        /// </summary>
        public static void UnregisterModdingBridgeRuntime(IModdingBridge instance)
        {
            UnregisterService(ref _moddingBridgeRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current run-modifier runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterRunModifierRuntime(RunModifierController instance)
        {
            UnregisterService(ref _runModifierRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current meta-campaign progression runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterMetaCampaignService(IMetaCampaignService instance)
        {
            UnregisterService(ref _metaCampaignRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current global fauna migration runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterMigrationDirectorRuntime(MigrationDirector instance)
        {
            UnregisterService(ref _migrationDirectorRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current base-pollution runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterBasePollutionRuntime(BasePollutionManager instance)
        {
            UnregisterService(ref _basePollutionRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current entity-change manager runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterEntityChangeManagerRuntime(EntityChangeManager instance)
        {
            UnregisterService(ref _entityChangeManagerRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current core performance monitor runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterPerformanceMonitorRuntime(PerformanceMonitor instance)
        {
            UnregisterService(ref _performanceMonitorRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current beacon-network runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterBeaconNetworkRuntime(BeaconNetworkSystem instance)
        {
            UnregisterService(ref _beaconNetworkRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current scan-log runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterScanLogRuntime(ScanLogSystem instance)
        {
            UnregisterService(ref _scanLogRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current tool-durability runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterToolDurabilityRuntime(ToolDurabilitySystem instance)
        {
            UnregisterService(ref _toolDurabilityRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current tool haptics runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterToolHapticsRuntime(ToolHapticsRuntime instance)
        {
            UnregisterService(ref _toolHapticsRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current VR somatic provider if the owner matches.
        /// </summary>
        public static void UnregisterVRSomaticProvider(IVRSomaticProvider instance)
        {
            UnregisterService(ref _vrSomaticProvider, instance);
        }

        /// <summary>
        /// Unregisters the current lore database runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterLoreDatabaseRuntime(LoreDatabaseManager instance)
        {
            UnregisterService(ref _loreDatabaseRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current player expression/profile runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterPlayerExpressionRuntime(PlayerExpressionManager instance)
        {
            UnregisterService(ref _playerExpressionRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current visor spectrum runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterSpectrumRuntime(SpectrumSystem instance)
        {
            UnregisterService(ref _spectrumRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current user-options persistence runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterUserOptionsRuntime(UserOptionsPersistence instance)
        {
            UnregisterService(ref _userOptionsRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current asset residency governor runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterAssetLifecycleRuntime(AssetLifecycleGovernor instance)
        {
            UnregisterService(ref _assetLifecycleRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current asset load dispatcher runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterAssetLoadDispatcherRuntime(AssetLoadDispatcher instance)
        {
            UnregisterService(ref _assetLoadDispatcherRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current VRAM monitor runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterVRAMMonitorRuntime(VRAMMonitor instance)
        {
            UnregisterService(ref _vramMonitorRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current VRAM pressure response runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterVRAMPressureRuntime(VRAMPressureMonitor instance)
        {
            UnregisterService(ref _vramPressureRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current RenderTexture lifecycle tracker runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterRenderTextureLifecycleRuntime(RenderTextureLifecycleTracker instance)
        {
            UnregisterService(ref _renderTextureLifecycleRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current RenderTexture pool runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterRenderTexturePoolRuntime(RenderTexturePool instance)
        {
            UnregisterService(ref _renderTexturePoolRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current abyssal fluid aftermath decal runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterAbyssalFluidDecalRuntime(AbyssalFluidDecalManager instance)
        {
            UnregisterService(ref _abyssalFluidDecalRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current sargassum global drag-field runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterSargassumDragRuntime(SargassumGlobalDragManager instance)
        {
            UnregisterService(ref _sargassumDragRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current sargassum cut-mask runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterSargassumCutRuntime(SargassumCutManager instance)
        {
            UnregisterService(ref _sargassumCutRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current sargassum micro-fauna boid runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterSargassumMicroFaunaRuntime(SargassumMicroFaunaBoids instance)
        {
            UnregisterService(ref _sargassumMicroFaunaRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current floating-origin runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterFloatingOriginRuntime(HectonFloatingOrigin instance)
        {
            UnregisterService(ref _floatingOriginRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current environmental soundscape runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterSoundscapeRuntime(SoundscapeSystem instance)
        {
            UnregisterService(ref _soundscapeRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current environmental strain runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterEnvironmentalStrainRuntime(EnvironmentalStrainManager instance)
        {
            UnregisterService(ref _environmentalStrainRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current ecosystem health runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterEcosystemHealthRuntime(EcosystemHealthDirector instance)
        {
            UnregisterService(ref _ecosystemHealthRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current fauna genetics runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterFaunaGeneticsRuntime(FaunaGeneticsManager instance)
        {
            UnregisterService(ref _faunaGeneticsRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current player exploration runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterPlayerExplorationRuntime(PlayerExplorationTracker instance)
        {
            UnregisterService(ref _playerExplorationRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current discovery runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterDiscoveryRuntime(HectonDiscoveryManager instance)
        {
            UnregisterService(ref _discoveryRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current dynamic difficulty runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterDynamicDifficultyRuntime(DynamicDifficultyDirector instance)
        {
            UnregisterService(ref _dynamicDifficultyRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current resource scarcity runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterResourceScarcityRuntime(ResourceScarcityDirector instance)
        {
            UnregisterService(ref _resourceScarcityRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current field-operation log runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterFieldOperationLogRuntime(FieldOperationLogSystem instance)
        {
            UnregisterService(ref _fieldOperationLogRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current PDA exchange runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterPDAExchangeRuntime(PDAExchangeSystem instance)
        {
            UnregisterService(ref _pdaExchangeRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current player action runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterPlayerActionRuntime(PlayerActionController instance)
        {
            UnregisterService(ref _playerActionRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current PDA marker registry runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterPDAMarkerRuntime(PDAMarkerRegistry instance)
        {
            UnregisterService(ref _pdaMarkerRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current PDA intrusion runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterPDAIntrusionRuntime(PDAIntrusionManager instance)
        {
            UnregisterService(ref _pdaIntrusionRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current ambient water-motion runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterAmbientWaterMotionRuntime(AmbientWaterMotionManager instance)
        {
            UnregisterService(ref _ambientWaterMotionRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current suit upgrade runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterSuitUpgradeRuntime(SuitUpgradeManager instance)
        {
            UnregisterService(ref _suitUpgradeRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current UI audio feedback runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterUIAudioFeedbackRuntime(UIAudioFeedback instance)
        {
            UnregisterService(ref _uiAudioFeedbackRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current UI tooltip runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterUITooltipRuntime(UITooltip instance)
        {
            UnregisterService(ref _uiTooltipRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current loading screen runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterLoadingScreenRuntime(LoadingScreenController instance)
        {
            UnregisterService(ref _loadingScreenRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current ending runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterEndingRuntime(EndingSystem instance)
        {
            UnregisterService(ref _endingRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current Atlas-6 directive runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterAtlas6DirectiveRuntime(Atlas6DirectiveSystem instance)
        {
            UnregisterService(ref _atlas6DirectiveRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current hazard-zone runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterHazardZoneRuntime(HazardZoneManager instance)
        {
            UnregisterService(ref _hazardZoneRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current mission facade runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterMissionRuntime(MissionManager instance)
        {
            UnregisterService(ref _missionRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current rock rendering/proximity runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterRockManagerRuntime(HectonRockManager instance)
        {
            UnregisterService(ref _rockManagerRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current camera presentation feedback runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterCameraJuiceRuntime(ICameraJuiceSystem instance)
        {
            UnregisterService(ref _cameraJuiceRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current adaptive music director runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterMusicDirectorRuntime(HectonMusicDirector instance)
        {
            UnregisterService(ref _musicDirectorRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current subtitle presentation runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterSubtitleRuntime(SubtitleManager instance)
        {
            UnregisterService(ref _subtitleRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current Atlas signal decoder runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterAtlasSignalDecoderRuntime(AtlasSignalDecoder instance)
        {
            UnregisterService(ref _atlasSignalDecoderRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current recycling/scrap runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterScrapRuntime(ScrapManager instance)
        {
            UnregisterService(ref _scrapRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current autonomous extractor SOA runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterAutonomousExtractorRuntime(AutonomousExtractorSystem instance)
        {
            UnregisterService(ref _autonomousExtractorRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current visor RenderTexture budget monitor runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterVisorRTRuntime(VisorRTManager instance)
        {
            UnregisterService(ref _visorRTRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current camera RenderTexture budget monitor runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterCameraRTRuntime(CameraRTManager instance)
        {
            UnregisterService(ref _cameraRTRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current post-processing RenderTexture budget monitor runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterPostFXRTRuntime(PostFXRTManager instance)
        {
            UnregisterService(ref _postFXRTRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current UI RenderTexture budget monitor runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterUIRTRuntime(UIRTManager instance)
        {
            UnregisterService(ref _uiRTRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current user settings runtime owner if the owner matches.
        /// </summary>
        public static void UnregisterSettingsRuntime(SettingsManager instance)
        {
            UnregisterService(ref _settingsRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current runtime liveness watchdog owner if the owner matches.
        /// </summary>
        public static void UnregisterRuntimeWatchdogRuntime(RuntimeWatchdog instance)
        {
            UnregisterService(ref _runtimeWatchdogRuntime, instance);
        }

        /// <summary>
        /// Unregisters the current tick-manager owner if the owner matches.
        /// </summary>
        /// <param name="instance">Tick-manager owner requesting unregistration.</param>
        public static void UnregisterTickManager(GameTickManager instance)
        {
            UnregisterService(ref _tickManager, instance);
        }

        /// <summary>
        /// Unregisters the current gameplay dispatcher owner if the owner matches.
        /// </summary>
        /// <param name="instance">Dispatcher owner requesting unregistration.</param>
        public static void UnregisterSystemDispatcher(SystemDispatcher instance)
        {
            UnregisterService(ref _dispatcher, instance);
        }

        /// <summary>
        /// Unregisters the current SRP render dispatcher owner if the owner matches.
        /// </summary>
        /// <param name="instance">Render dispatcher owner requesting unregistration.</param>
        public static void UnregisterRenderDispatcher(RenderDispatcher instance)
        {
            UnregisterService(ref _renderDispatcher, instance);
        }

        /// <summary>
        /// Unregisters the current global physics-state manager owner if the owner matches.
        /// </summary>
        /// <param name="instance">Physics-state manager owner requesting unregistration.</param>
        public static void UnregisterPhysicsStateManager(GlobalPhysicsStateManager instance)
        {
            UnregisterService(ref _physicsStateManager, instance);
        }

        /// <summary>
        /// Unregisters the centralized physics culling overseer if the owner matches.
        /// </summary>
        /// <param name="instance">Physics culling overseer owner requesting unregistration.</param>
        public static void UnregisterPhysicsCullingOverseer(IPhysicsCullingOverseer instance)
        {
            UnregisterService(ref _physicsCullingOverseer, instance);
        }

        /// <summary>
        /// Registers an update owner into both the global bucket and its fixed dispatcher lane.
        /// </summary>
        /// <param name="item">Update owner.</param>
        /// <param name="layer">Dispatcher priority lane.</param>
        public static void RegisterUpdatable(IUpdatable item, PriorityLayer layer)
        {
            TryRegisterUpdatable(item, layer);
        }

        /// <summary>
        /// Registers a Kahn-sorted master dispatcher system without taking a direct domain dependency.
        /// </summary>
        public static bool TryRegisterDispatcherSystem(IDispatcherSystem item)
        {
            if (item == null)
                return false;

            if (!Application.isPlaying)
                return false;

            if (!TryEnsureDispatcherRegistration())
                return false;

            return SystemDispatcher.Register(item);
        }

        /// <summary>
        /// Registers a fixed-only master dispatcher system without mixing it into frame jobs.
        /// </summary>
        public static bool TryRegisterDispatcherFixedSystem(IDispatcherFixedSystem item)
        {
            if (item == null)
                return false;

            if (!Application.isPlaying)
                return false;

            if (!TryEnsureDispatcherRegistration())
                return false;

            return SystemDispatcher.Register(item);
        }

        /// <summary>
        /// Registers an update owner into both the global bucket and its dispatcher lane.
        /// </summary>
        /// <param name="item">Update owner.</param>
        /// <param name="layer">Dispatcher priority lane.</param>
        /// <returns>True when both registry and dispatcher lane accepted the item.</returns>
        public static bool TryRegisterUpdatable(IUpdatable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            if (!Application.isPlaying)
                return false;

            if (!TryEnsureDispatcherRegistration())
                return false;
            if (!_updatables.TryRegister(item))
                return false;

            if (!SystemDispatcher.Register(item, layer))
            {
                _updatables.Unregister(item);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Registers a 60 Hz fast-tick owner into both the global bucket and its dispatcher lane.
        /// </summary>
        public static void RegisterFastTickable(IFastTickable item, PriorityLayer layer)
        {
            TryRegisterFastTickable(item, layer);
        }

        /// <summary>
        /// Registers a 60 Hz fast-tick owner into both the global bucket and its dispatcher lane.
        /// </summary>
        public static bool TryRegisterFastTickable(IFastTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            if (!Application.isPlaying)
                return false;

            if (!TryEnsureDispatcherRegistration())
                return false;
            if (!_fastTickables.TryRegister(item))
                return false;

            if (!SystemDispatcher.Register(item, layer))
            {
                _fastTickables.Unregister(item);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Registers a fixed-update owner into both the global bucket and its fixed dispatcher lane.
        /// </summary>
        /// <param name="item">Fixed-update owner.</param>
        /// <param name="layer">Dispatcher priority lane.</param>
        public static void RegisterFixedTickable(IFixedTickable item, PriorityLayer layer)
        {
            TryRegisterFixedTickable(item, layer);
        }

        /// <summary>
        /// Registers a fixed-update owner into both the global bucket and its fixed dispatcher lane.
        /// </summary>
        /// <param name="item">Fixed-update owner.</param>
        /// <param name="layer">Dispatcher priority lane.</param>
        /// <returns>True when both registry and dispatcher lane accepted the item.</returns>
        public static bool TryRegisterFixedTickable(IFixedTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            if (!Application.isPlaying)
                return false;

            if (!TryEnsureDispatcherRegistration())
                return false;
            if (!_fixedTickables.TryRegister(item))
                return false;

            if (!SystemDispatcher.Register(item, layer))
            {
                _fixedTickables.Unregister(item);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Registers a slow-tick owner into both the global bucket and its dispatcher lane.
        /// </summary>
        /// <param name="item">Slow-tick owner.</param>
        /// <param name="layer">Dispatcher priority lane.</param>
        public static void RegisterSlowTickable(ISlowTickable item, PriorityLayer layer)
        {
            TryRegisterSlowTickable(item, layer);
        }

        /// <summary>
        /// Registers a slow-tick owner into both the global bucket and its dispatcher lane.
        /// </summary>
        /// <param name="item">Slow-tick owner.</param>
        /// <param name="layer">Dispatcher priority lane.</param>
        /// <returns>True when both registry and dispatcher lane accepted the item.</returns>
        public static bool TryRegisterSlowTickable(ISlowTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            if (!Application.isPlaying)
                return false;

            if (!TryEnsureDispatcherRegistration())
                return false;
            if (!_slowTickables.TryRegister(item))
                return false;

            if (!SystemDispatcher.Register(item, layer))
            {
                _slowTickables.Unregister(item);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Registers a 1 Hz cold-tick owner into both the global bucket and its dispatcher lane.
        /// </summary>
        public static void RegisterColdTickable(IColdTickable item, PriorityLayer layer)
        {
            TryRegisterColdTickable(item, layer);
        }

        /// <summary>
        /// Registers a 1 Hz cold-tick owner into both the global bucket and its dispatcher lane.
        /// </summary>
        public static bool TryRegisterColdTickable(IColdTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            if (!Application.isPlaying)
                return false;

            if (!TryEnsureDispatcherRegistration())
                return false;
            if (!_coldTickables.TryRegister(item))
                return false;

            if (!SystemDispatcher.Register(item, layer))
            {
                _coldTickables.Unregister(item);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Registers a frost maintenance owner into both the global bucket and its dispatcher lane.
        /// </summary>
        /// <param name="item">Frost-tick owner.</param>
        /// <param name="layer">Dispatcher priority lane.</param>
        public static void RegisterFrostTickable(IFrostTickable item, PriorityLayer layer)
        {
            TryRegisterFrostTickable(item, layer);
        }

        /// <summary>
        /// Registers a frost maintenance owner into both the global bucket and its dispatcher lane.
        /// </summary>
        /// <param name="item">Frost-tick owner.</param>
        /// <param name="layer">Dispatcher priority lane.</param>
        /// <returns>True when both registry and dispatcher lane accepted the item.</returns>
        public static bool TryRegisterFrostTickable(IFrostTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            if (!Application.isPlaying)
                return false;

            if (!TryEnsureDispatcherRegistration())
                return false;
            if (!_frostTickables.TryRegister(item))
                return false;

            if (!SystemDispatcher.Register(item, layer))
            {
                _frostTickables.Unregister(item);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Registers a 60 Hz unscaled UI/menu tick owner into both the global bucket and its dispatcher lane.
        /// </summary>
        public static void RegisterUnscaledFastTickable(IUnscaledFastTickable item, PriorityLayer layer)
        {
            TryRegisterUnscaledFastTickable(item, layer);
        }

        /// <summary>
        /// Registers a 60 Hz unscaled UI/menu tick owner into both the global bucket and its dispatcher lane.
        /// </summary>
        public static bool TryRegisterUnscaledFastTickable(IUnscaledFastTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            if (!Application.isPlaying)
                return false;

            if (!TryEnsureDispatcherRegistration())
                return false;
            if (!_unscaledFastTickables.TryRegister(item))
                return false;

            if (!SystemDispatcher.Register(item, layer))
            {
                _unscaledFastTickables.Unregister(item);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Registers an end-of-frame owner into its dispatcher late-frame lane.
        /// </summary>
        /// <param name="item">Late-frame owner.</param>
        /// <param name="layer">Dispatcher priority lane.</param>
        public static void RegisterLateFrameTickable(ILateFrameTickable item, PriorityLayer layer)
        {
            TryRegisterLateFrameTickable(item, layer);
        }

        /// <summary>
        /// Registers an end-of-frame owner into its dispatcher late-frame lane.
        /// </summary>
        /// <param name="item">Late-frame owner.</param>
        /// <param name="layer">Dispatcher priority lane.</param>
        /// <returns>True when the dispatcher lane accepted the item.</returns>
        public static bool TryRegisterLateFrameTickable(ILateFrameTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            if (!Application.isPlaying)
                return false;

            if (!TryEnsureDispatcherRegistration())
                return false;

            return SystemDispatcher.Register(item, layer);
        }

        /// <summary>
        /// Registers a post-fixed-step owner into its dispatcher lane.
        /// </summary>
        /// <param name="item">Post-fixed owner.</param>
        /// <param name="layer">Dispatcher priority lane.</param>
        public static void RegisterPostFixedTickable(IPostFixedTickable item, PriorityLayer layer)
        {
            TryRegisterPostFixedTickable(item, layer);
        }

        /// <summary>
        /// Registers a post-fixed-step owner into its dispatcher lane.
        /// </summary>
        /// <param name="item">Post-fixed owner.</param>
        /// <param name="layer">Dispatcher priority lane.</param>
        /// <returns>True when the dispatcher lane accepted the item.</returns>
        public static bool TryRegisterPostFixedTickable(IPostFixedTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            if (!Application.isPlaying)
                return false;

            if (!TryEnsureDispatcherRegistration())
                return false;

            return SystemDispatcher.Register(item, layer);
        }

        /// <summary>
        /// Registers a listener that rebinds cached dependencies when a service slot is replaced at runtime.
        /// </summary>
        /// <param name="listener">Hot-swap listener.</param>
        public static void RegisterHotSwapListener(IGlobalRegistryHotSwapListener listener)
        {
            TryRegisterHotSwapListener(listener);
        }

        /// <summary>
        /// Attempts to register a service hot-swap listener without a caller-side registry scan.
        /// </summary>
        /// <param name="listener">Hot-swap listener.</param>
        /// <returns>True when the listener was newly registered.</returns>
        public static bool TryRegisterHotSwapListener(IGlobalRegistryHotSwapListener listener)
        {
            return listener != null && _hotSwapListeners.TryRegister(listener);
        }

        /// <summary>
        /// Checks the service hot-swap listener lane without exposing the bucket to domain callers.
        /// </summary>
        /// <param name="listener">Hot-swap listener.</param>
        /// <returns>True when the listener is currently registered.</returns>
        public static bool IsHotSwapListenerRegistered(IGlobalRegistryHotSwapListener listener)
        {
            return listener != null && _hotSwapListeners.Contains(listener);
        }

        /// <summary>
        /// Registers a listener for deferred registry event payloads.
        /// </summary>
        public static void RegisterRegistryEventListener(IRegistryEventListener listener)
        {
            if (listener == null)
                return;

            _registryEventListeners.TryRegister(listener);
        }

        /// <summary>
        /// Unregisters an update owner from both the global bucket and its dispatcher lane.
        /// </summary>
        /// <param name="item">Update owner.</param>
        /// <param name="layer">Dispatcher priority lane.</param>
        public static void UnregisterUpdatable(IUpdatable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            _updatables.TryUnregister(item);
            SystemDispatcher.Unregister(item, layer);
        }

        /// <summary>
        /// Unregisters a master dispatcher system.
        /// </summary>
        public static void UnregisterDispatcherSystem(IDispatcherSystem item)
        {
            if (item == null)
                return;

            SystemDispatcher.Unregister(item);
        }

        /// <summary>
        /// Unregisters a fixed-only master dispatcher system.
        /// </summary>
        public static void UnregisterDispatcherFixedSystem(IDispatcherFixedSystem item)
        {
            if (item == null)
                return;

            SystemDispatcher.Unregister(item);
        }

        /// <summary>
        /// Unregisters a fast-tick owner from both the global bucket and its dispatcher lane.
        /// </summary>
        public static void UnregisterFastTickable(IFastTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            _fastTickables.TryUnregister(item);
            SystemDispatcher.Unregister(item, layer);
        }

        /// <summary>
        /// Unregisters a fixed-update owner from both the global bucket and its dispatcher lane.
        /// </summary>
        /// <param name="item">Fixed-update owner.</param>
        /// <param name="layer">Dispatcher priority lane.</param>
        public static void UnregisterFixedTickable(IFixedTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            _fixedTickables.TryUnregister(item);
            SystemDispatcher.Unregister(item, layer);
        }

        /// <summary>
        /// Unregisters a slow-tick owner from both the global bucket and its dispatcher lane.
        /// </summary>
        /// <param name="item">Slow-tick owner.</param>
        /// <param name="layer">Dispatcher priority lane.</param>
        public static void UnregisterSlowTickable(ISlowTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            _slowTickables.TryUnregister(item);
            SystemDispatcher.Unregister(item, layer);
        }

        /// <summary>
        /// Unregisters a cold-tick owner from both the global bucket and its dispatcher lane.
        /// </summary>
        public static void UnregisterColdTickable(IColdTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            _coldTickables.TryUnregister(item);
            SystemDispatcher.Unregister(item, layer);
        }

        /// <summary>
        /// Unregisters a frost maintenance owner from both the global bucket and its dispatcher lane.
        /// </summary>
        /// <param name="item">Frost-tick owner.</param>
        /// <param name="layer">Dispatcher priority lane.</param>
        public static void UnregisterFrostTickable(IFrostTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            _frostTickables.TryUnregister(item);
            SystemDispatcher.Unregister(item, layer);
        }

        /// <summary>
        /// Unregisters an unscaled fast-tick owner from both the global bucket and its dispatcher lane.
        /// </summary>
        public static void UnregisterUnscaledFastTickable(IUnscaledFastTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            _unscaledFastTickables.TryUnregister(item);
            SystemDispatcher.Unregister(item, layer);
        }

        /// <summary>
        /// Unregisters an end-of-frame owner from its dispatcher lane.
        /// </summary>
        /// <param name="item">Late-frame owner.</param>
        /// <param name="layer">Dispatcher priority lane.</param>
        public static void UnregisterLateFrameTickable(ILateFrameTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            SystemDispatcher.Unregister(item, layer);
        }

        /// <summary>
        /// Unregisters a post-fixed-step owner from its dispatcher lane.
        /// </summary>
        /// <param name="item">Post-fixed owner.</param>
        /// <param name="layer">Dispatcher priority lane.</param>
        public static void UnregisterPostFixedTickable(IPostFixedTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            SystemDispatcher.Unregister(item, layer);
        }

        /// <summary>
        /// Unregisters a previously registered service hot-swap listener.
        /// </summary>
        /// <param name="listener">Hot-swap listener.</param>
        public static void UnregisterHotSwapListener(IGlobalRegistryHotSwapListener listener)
        {
            TryUnregisterHotSwapListener(listener);
        }

        /// <summary>
        /// Attempts to unregister a service hot-swap listener without miss logging or a caller-side scan.
        /// </summary>
        /// <param name="listener">Hot-swap listener.</param>
        /// <returns>True when the listener was present and removed.</returns>
        public static bool TryUnregisterHotSwapListener(IGlobalRegistryHotSwapListener listener)
        {
            return listener != null && _hotSwapListeners.TryUnregister(listener);
        }

        /// <summary>
        /// Unregisters a deferred registry event listener.
        /// </summary>
        public static void UnregisterRegistryEventListener(IRegistryEventListener listener)
        {
            if (listener == null)
                return;

            _registryEventListeners.TryUnregister(listener);
        }

        /// <summary>
        /// Safely replaces the thermodynamics service slot and notifies explicit hot-swap listeners.
        /// </summary>
        /// <param name="instance">Replacement service instance, or null to clear the slot.</param>
        public static void ReplaceThermodynamicsService(IThermodynamicsService instance)
        {
            ReplaceService(ref _thermodynamicsService, instance, GlobalRegistryServiceSlot.ThermodynamicsService);
        }

        /// <summary>
        /// Safely replaces the logistics service slot and notifies explicit hot-swap listeners.
        /// </summary>
        /// <param name="instance">Replacement service instance, or null to clear the slot.</param>
        public static void ReplaceLogisticsService(ILogisticsService instance)
        {
            ReplaceService(ref _logistics, instance, GlobalRegistryServiceSlot.Logistics);
        }

        /// <summary>
        /// Safely replaces the world-generation service slot and notifies explicit hot-swap listeners.
        /// </summary>
        /// <param name="instance">Replacement service instance, or null to clear the slot.</param>
        public static void ReplaceWorldGenService(IWorldGenService instance)
        {
            ReplaceService(ref _worldGen, instance, GlobalRegistryServiceSlot.WorldGen);
        }

        /// <summary>
        /// Safely replaces the encounter-direction service slot and notifies explicit hot-swap listeners.
        /// </summary>
        /// <param name="instance">Replacement service instance, or null to clear the slot.</param>
        public static void ReplaceEncounterDirectorService(IEncounterDirectorService instance)
        {
            ReplaceService(ref _encounterDirector, instance, GlobalRegistryServiceSlot.EncounterDirector);
        }

        /// <summary>
        /// Safely replaces the quest-system service slot and notifies explicit hot-swap listeners.
        /// </summary>
        /// <param name="instance">Replacement service instance, or null to clear the slot.</param>
        public static void ReplaceQuestSystem(IQuestSystem instance)
        {
            ReplaceService(ref _questSystem, instance, GlobalRegistryServiceSlot.QuestSystem);
        }

        /// <summary>
        /// Clears all global multi-instance registries.
        /// </summary>
        public static void ClearRuntimeBuckets()
        {
            WorldSpatialHashGrid.ClearRuntimeState();
            NativeMemorySentinel.ReportSceneLifetimeLeaks(nameof(ClearRuntimeBuckets));
            _updatables.Clear();
            _fastTickables.Clear();
            _fixedTickables.Clear();
            _slowTickables.Clear();
            _coldTickables.Clear();
            _frostTickables.Clear();
            _unscaledFastTickables.Clear();
            _renderables.Clear();
            SystemDispatcher.ClearAllLanes();
        }

        /// <summary>
        /// Builds a cold-path diagnostic report for services that were requested but never registered.
        /// </summary>
        public static bool TryBuildGhostServiceReport(out string report)
        {
            StringBuilder builder = null;
            int ghostCount = 0;

            for (int wordIndex = 0; wordIndex < ServiceSlotMaskWordCount; wordIndex++)
            {
                ulong ghostMask = (ulong)(Volatile.Read(ref _requestedServiceSlotMask[wordIndex]) &
                    ~Volatile.Read(ref _registeredServiceSlotMask[wordIndex]));
                while (ghostMask != 0ul)
                {
                    int bitIndex = CountTrailingZeroBits(ghostMask);
                    int serviceSlotIndex = (wordIndex << 6) + bitIndex;
                    ghostMask &= ~(1ul << bitIndex);

                    if (serviceSlotIndex == (int)GlobalRegistryServiceSlot.Unknown)
                        continue;

                    if (builder == null)
                    {
                        builder = new StringBuilder(512);
                        builder.Append("[GlobalRegistry] Ghost service request(s) detected before Ready lock:");
                    }

                    builder.Append('\n')
                        .Append(" - ")
                        .Append(ResolveServiceSlotName(serviceSlotIndex));
                    ghostCount++;
                }
            }

            report = builder != null ? builder.ToString() : null;
            return ghostCount > 0;
        }

        private static string ResolveServiceSlotName(int serviceSlotIndex)
        {
            return (uint)serviceSlotIndex < (uint)_serviceSlotNames.Length
                ? _serviceSlotNames[serviceSlotIndex]
                : "Unknown";
        }

        /// <summary>
        /// Fails the current boot if any requested registry service slot remained empty.
        /// </summary>
        public static void AssertNoGhostServicesOrThrow()
        {
            if (!TryBuildGhostServiceReport(out string report))
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(report);
#endif
            throw new CriticalBootException(report);
        }

        private static bool TryEnsureDispatcherRegistration()
        {
            if (_dispatcher != null)
                return true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_dispatcherRegistrationErrorLogged)
            {
                _dispatcherRegistrationErrorLogged = true;
                Debug.LogError("[GlobalRegistry] SystemDispatcher is not registered. Bootstrap must create and register it before runtime tick registration.");
            }
#endif
            return false;
        }

        internal static bool TryBeginResolution(GlobalRegistryResolutionScope scope)
        {
            uint scopeMask = 1u << (int)scope;
            if ((_resolutionMask & scopeMask) != 0u)
            {
                ThrowDependencyCycle(scope);
            }

            if (IsPlayerResolutionScope(scope) && (_resolutionMask & PlayerResolutionMask) != 0u)
            {
                ThrowDependencyCycle(scope);
            }

            _resolutionMask |= scopeMask;
            return true;
        }

        internal static void EndResolution(GlobalRegistryResolutionScope scope)
        {
            _resolutionMask &= ~(1u << (int)scope);
        }

        private static bool IsResolving(GlobalRegistryResolutionScope scope)
        {
            return (_resolutionMask & (1u << (int)scope)) != 0u;
        }

        private static bool IsResolvingAny(uint mask)
        {
            return (_resolutionMask & mask) != 0u;
        }

        private static bool IsPlayerResolutionScope(GlobalRegistryResolutionScope scope)
        {
            return scope == GlobalRegistryResolutionScope.PlayerContext ||
                scope == GlobalRegistryResolutionScope.PlayerInventory ||
                scope == GlobalRegistryResolutionScope.PlayerSensory;
        }

        private static void ThrowDependencyCycle(GlobalRegistryResolutionScope requestedScope)
        {
            CrashTelemetryBuffer.ReportRecursiveCascadeCritical();
            throw new DependencyCycleException(ResolveDependencyCycleMessage(requestedScope));
        }

        private static void GuardGenericGetDuringRegistration<T>() where T : class
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Phase != RegistryPhase.Registering)
                return;

            if (!_registeringGetViolationLogged)
            {
                _registeringGetViolationLogged = true;
                Debug.LogError("[GlobalRegistry] Get<T>() during Registering is forbidden. requested=" + typeof(T).Name);
            }

            throw new CriticalBootException("[GlobalRegistry] Get<T>() during Registering is forbidden: " + typeof(T).Name);
#endif
        }

        private static ForceOverrideToken ResolveSceneRuntimePublicationToken(
            GlobalRegistryServiceSlot serviceSlot,
            ForceOverrideToken forceOverrideToken)
        {
            if (forceOverrideToken.IsValid ||
                Volatile.Read(ref _sceneRuntimePublicationGateDepth) <= 0 ||
                !CanIssueSceneRuntimePublicationToken(serviceSlot))
            {
                return forceOverrideToken;
            }

            return CreateHotSwapOverrideToken();
        }

        private static bool CanIssueSceneRuntimePublicationToken(GlobalRegistryServiceSlot serviceSlot)
        {
            RegistryPhase phase = Phase;
            if (phase == RegistryPhase.Uninitialized)
                return false;

            return IsSceneRuntimeHotSwapSlot(serviceSlot);
        }

        private static bool IsSceneRuntimeHotSwapSlot(GlobalRegistryServiceSlot serviceSlot)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Unknown:
                case GlobalRegistryServiceSlot.Input:
                case GlobalRegistryServiceSlot.InputBinding:
                case GlobalRegistryServiceSlot.NativeInputManagerRuntime:
                case GlobalRegistryServiceSlot.RaycastBatchRuntime:
                case GlobalRegistryServiceSlot.Physics:
                case GlobalRegistryServiceSlot.Audio:
                case GlobalRegistryServiceSlot.AudioVirtualization:
                case GlobalRegistryServiceSlot.Scene:
                case GlobalRegistryServiceSlot.Save:
                case GlobalRegistryServiceSlot.ObjectPool:
                case GlobalRegistryServiceSlot.TickManager:
                case GlobalRegistryServiceSlot.Dispatcher:
                case GlobalRegistryServiceSlot.RenderDispatcher:
                case GlobalRegistryServiceSlot.PhysicsStateManager:
                case GlobalRegistryServiceSlot.DataVault:
                case GlobalRegistryServiceSlot.UserOptionsRuntime:
                case GlobalRegistryServiceSlot.AssetLifecycleRuntime:
                case GlobalRegistryServiceSlot.AssetLoadDispatcherRuntime:
                case GlobalRegistryServiceSlot.MacroDatabase:
                case GlobalRegistryServiceSlot.JobAdmissionRuntime:
                case GlobalRegistryServiceSlot.StreamingBackpressureRuntime:
                case GlobalRegistryServiceSlot.SimulationBucketerRuntime:
                case GlobalRegistryServiceSlot.HardwareThermalService:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Pure predicate answering whether <typeparamref name="T"/> could be published right now, i.e.
        /// whether <see cref="GuardServicePublication{T}"/> would let a tokenless registration through.
        ///
        /// Runtime owners that must evict an incumbent before republishing themselves have to ask this
        /// FIRST. Without it they clear the live owner and only then hit the guard, so the throw leaves
        /// the slot empty and the evicted owner permanently disabled - a partial mutation followed by a
        /// CriticalBootException, which is strictly worse than declining ownership.
        ///
        /// Mirrors the guard's own conditions and must stay in sync with it. Side-effect free: no token
        /// is issued, no slot bit is set, nothing is published.
        /// </summary>
        internal static bool IsRuntimeServicePublicationOpen<T>() where T : class
        {
            return Phase != RegistryPhase.Ready || IsSceneRuntimeHotSwapSlot(ServiceSlotCache<T>.Slot);
        }

        private static void GuardServicePublication<T>(ForceOverrideToken forceOverrideToken) where T : class
        {
            if (forceOverrideToken.IsValid || Phase != RegistryPhase.Ready)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Log once PER TYPE, not once globally, and carry a running count.
            //
            // A single global latch made this defect effectively invisible. The one probe run that
            // actually failed rejected 31 DISTINCT services - MapMagicBridge, HectonPlayerMotor,
            // QuestManager, HectonCelestialEngine, DepthZoneDirector, EndingSystem, SoundscapeSystem
            // and 24 more - and emitted exactly ONE error naming ONE of them. The run summary looked
            // like a slow scene load, and the only way to see the real scale was grepping a 1 MB log
            // for the exception text. An intermittent boot abort that reports 1/31 of itself is a
            // diagnosability failure, not just a logging preference.
            //
            // The count is what makes it unmissable: "#31" in the message states the scale at the
            // first line read, without a grep.
            int violationGeneration = Volatile.Read(ref _readyLockViolationGeneration);
            int violationCount = Interlocked.Increment(ref _readyLockViolationCount);
            if (ReadyLockViolationLatch<T>.LoggedGeneration != violationGeneration)
            {
                ReadyLockViolationLatch<T>.LoggedGeneration = violationGeneration;
                Debug.LogError(
                    "[GlobalRegistry] Ready-locked registry rejected registration #" + violationCount +
                    ": " + typeof(T).Name);
            }
#endif
            throw new CriticalBootException("[GlobalRegistry] Ready-locked registry rejected registration: " + typeof(T).Name);
        }

        private static void MarkServiceRequested(GlobalRegistryServiceSlot serviceSlot)
        {
            SetServiceSlotBit(_requestedServiceSlotMask, serviceSlot);
        }

        private static void MarkServiceRegistered(GlobalRegistryServiceSlot serviceSlot)
        {
            SetServiceSlotBit(_registeredServiceSlotMask, serviceSlot);
        }

        private static void SetServiceSlotBit(long[] mask, GlobalRegistryServiceSlot serviceSlot)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Unknown)
                return;

            int serviceSlotIndex = (int)(byte)serviceSlot;
            int wordIndex = serviceSlotIndex >> 6;
            if ((uint)wordIndex >= (uint)ServiceSlotMaskWordCount)
                return;

            long bit = 1L << (serviceSlotIndex & 63);
            long current = Volatile.Read(ref mask[wordIndex]);
            while ((current & bit) == 0L)
            {
                long next = current | bit;
                long observed = Interlocked.CompareExchange(ref mask[wordIndex], next, current);
                if (observed == current)
                    return;

                current = observed;
            }
        }

        private static int CountTrailingZeroBits(ulong value)
        {
            int count = 0;
            while ((value & 1ul) == 0ul)
            {
                value >>= 1;
                count++;
            }

            return count;
        }

        private static string ResolveDependencyCycleMessage(GlobalRegistryResolutionScope requestedScope)
        {
            switch (requestedScope)
            {
                case GlobalRegistryResolutionScope.PlayerContext:
                    return "[GlobalRegistry] Dependency cycle while resolving PlayerContext.";
                case GlobalRegistryResolutionScope.PlayerInventory:
                    return "[GlobalRegistry] Dependency cycle while resolving PlayerInventory.";
                case GlobalRegistryResolutionScope.PlayerSensory:
                    return "[GlobalRegistry] Dependency cycle while resolving PlayerSensory.";
                case GlobalRegistryResolutionScope.Settings:
                    return "[GlobalRegistry] Dependency cycle while resolving Settings.";
                default:
                    return "[GlobalRegistry] Dependency cycle while resolving service.";
            }
        }

        /// <summary>
        /// Reports the Input substitution window ONCE per session.
        ///
        /// The telemetry publish alone was not enough: it is a hashed event on a bus nobody watches during
        /// a boot investigation, so a whole session ran on NoOpInputService while the route log cheerfully
        /// reported inputServiceRegistered=True, inputEnabled=True, blockMask=0x00000000. The console line
        /// below is the part that makes the window visible, and it is the only report that can fire when
        /// the Input slot is never filled at all - in that case
        /// ReportFirstFillAfterNullObjectSubstitution never runs, because there is no first fill.
        ///
        /// Cost: the existing `_inputFallbackWarningPublished` short-circuit already returns before any of
        /// this on every read after the first, so the Input getter's fallback branch is unchanged per frame.
        /// </summary>
        private static void PublishInputFallbackWarning()
        {
            if (_inputFallbackWarningPublished || !Application.isPlaying)
                return;

            _inputFallbackWarningPublished = true;
            _inputNullObjectSubstitutionHandedOut = _noOpInputService;
            GlobalTelemetryBus.PublishDependencyOrderWarning(_inputDependencyWarningHash, 0u);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(
                "[GlobalRegistry] Input slot READ BEFORE REGISTRATION. Handing out the non-null " +
                "NoOpInputService null object, whose IsInitialized is a hardcoded false. Any consumer " +
                "that caches this value keeps a dead input service, and its own null check will not catch " +
                "it - GlobalRegistry.InputDeterminism is a direct alias of this property, so a cached " +
                "IInputDeterminismService is the same dead object. If no follow-up GlobalRegistry line " +
                "names the Input slot before gameplay starts, then either the slot was never filled or " +
                "nobody was told the real service arrived, and every input override published this " +
                "session went unconsumed.");
#endif
        }

        private static void Register<T>(ref T slot, T instance) where T : class, ISystem
        {
            Register(ref slot, instance, default);
        }

        private static void Register<T>(ref T slot, T instance, ForceOverrideToken forceOverrideToken)
            where T : class, ISystem
        {
            if (instance == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(
                    "[GlobalRegistry] Cannot register null as " + typeof(T).Name + ".");
#endif
                return;
            }

            GlobalRegistryServiceSlot serviceSlot = ResolveServiceSlot<T>();
            ForceOverrideToken effectiveToken = ResolveSceneRuntimePublicationToken(serviceSlot, forceOverrideToken);
            GuardServicePublication<T>(effectiveToken);
            T previousService = Volatile.Read(ref slot);
            if (ReferenceEquals(previousService, instance))
            {
                MarkServiceRegistered(serviceSlot);
                return;
            }

            if (previousService != null && !effectiveToken.IsValid)
                ThrowSlotHijack(previousService, instance);

            if (effectiveToken.IsValid)
            {
                previousService = Interlocked.Exchange(ref slot, instance);
            }
            else
            {
                previousService = Interlocked.CompareExchange(ref slot, instance, null);
                if (previousService != null && !ReferenceEquals(previousService, instance))
                    ThrowSlotHijack(previousService, instance);
            }

            MarkServiceRegistered(serviceSlot);
            if (previousService != null)
            {
                QueueServiceRebound(serviceSlot, previousService, instance);
                return;
            }

            ReportFirstFillAfterNullObjectSubstitution(serviceSlot, instance);
        }

        private static void RegisterAllowSameInstance<T>(ref T slot, T instance) where T : class, ISystem
        {
            if (ReferenceEquals(slot, instance))
                return;

            Register(ref slot, instance);
        }

        [Preserve]
        private static void RegisterService<T>(ref T slot, T instance) where T : class
        {
            RegisterService(ref slot, instance, default);
        }

        [Preserve]
        private static void RegisterService<T>(ref T slot, T instance, ForceOverrideToken forceOverrideToken) where T : class
        {
            if (instance == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(
                    "[GlobalRegistry] Cannot register null as " + typeof(T).Name + ".");
#endif
                return;
            }

            GlobalRegistryServiceSlot serviceSlot = ResolveServiceSlot<T>();
            ForceOverrideToken effectiveToken = ResolveSceneRuntimePublicationToken(serviceSlot, forceOverrideToken);
            GuardServicePublication<T>(effectiveToken);
            T previousService = Volatile.Read(ref slot);
            if (ReferenceEquals(previousService, instance))
            {
                MarkServiceRegistered(serviceSlot);
                return;
            }

            if (previousService != null && !effectiveToken.IsValid)
                ThrowSlotHijack(previousService, instance);

            if (effectiveToken.IsValid)
            {
                previousService = Interlocked.Exchange(ref slot, instance);
            }
            else
            {
                previousService = Interlocked.CompareExchange(ref slot, instance, null);
                if (previousService != null && !ReferenceEquals(previousService, instance))
                    ThrowSlotHijack(previousService, instance);
            }

            MarkServiceRegistered(serviceSlot);
            if (previousService != null)
            {
                QueueServiceRebound(serviceSlot, previousService, instance);
                return;
            }

            ReportFirstFillAfterNullObjectSubstitution(serviceSlot, instance);
        }

        [Preserve]
        private static void RegisterServiceAllowSameInstance<T>(ref T slot, T instance) where T : class
        {
            if (ReferenceEquals(slot, instance))
                return;

            RegisterService(ref slot, instance);
        }

        private static void ReplaceService<T>(ref T slot, T instance, GlobalRegistryServiceSlot serviceSlot) where T : class
        {
            ReplaceService(ref slot, instance, serviceSlot, CreateHotSwapOverrideToken());
        }

        private static void ReplaceService<T>(
            ref T slot,
            T instance,
            GlobalRegistryServiceSlot serviceSlot,
            ForceOverrideToken forceOverrideToken) where T : class
        {
            if (!forceOverrideToken.IsValid)
                throw new InvalidOperationException("[GlobalRegistry] Invalid ForceOverride token for hot-swap.");

            T previousService = slot;
            if (ReferenceEquals(previousService, instance))
                return;

            Interlocked.Exchange(ref slot, instance);
            MarkServiceRegistered(serviceSlot);

            // This path queues unconditionally, including for a null previousService, so a substitution
            // window that ends here IS notified. Discharge the census entry so the first-fill report
            // cannot fire later on a stale record and claim a miss that did not happen.
            if (previousService == null)
                ConsumeNullObjectSubstitutionRecord(serviceSlot);

            QueueServiceRebound(serviceSlot, previousService, instance);
            if (previousService != null && instance == null)
                ReapMemoryForUnregisteredService(serviceSlot);
        }

        private static ForceOverrideToken CreateHotSwapOverrideToken()
        {
            return new ForceOverrideToken(ForceOverrideTokenValue);
        }

        private static void ThrowSlotHijack<T>(T previousService, T replacementService) where T : class
        {
            throw new InvalidOperationException(
                "[GlobalRegistry] Registry slot hijack blocked for " +
                typeof(T).Name +
                ". occupiedBy=" +
                previousService.GetType().Name +
                " replacement=" +
                replacementService.GetType().Name);
        }

        private static void UnregisterService<T>(ref T slot, T instance) where T : class
        {
            if (!ReferenceEquals(slot, instance))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[GlobalRegistry] Unregister mismatch for " + typeof(T).Name + ".");
#endif
                return;
            }

            T previousService = Interlocked.CompareExchange(ref slot, null, instance);
            if (!ReferenceEquals(previousService, instance))
                return;

            GlobalRegistryServiceSlot serviceSlot = ResolveServiceSlot<T>();
            QueueServiceRebound(serviceSlot, previousService, null);
            ReapMemoryForUnregisteredService(serviceSlot);
        }

        private static void ReapMemoryForUnregisteredService(GlobalRegistryServiceSlot serviceSlot)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
                return;

            SystemID owner = ResolveMemoryOwner(serviceSlot);
            int reaped = H8Memory.ReapOwnerLeaks(owner);
            if (reaped <= 0)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[FATAL LEAK PREVENTED] H8Memory reaped native allocations for " + serviceSlot + ".");
#endif
        }

        private static SystemID ResolveMemoryOwner(GlobalRegistryServiceSlot serviceSlot)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    return SystemID.SystemDispatcher;
                case GlobalRegistryServiceSlot.Physics:
                    return SystemID.Physics;
                case GlobalRegistryServiceSlot.PhysicsStateManager:
                    return SystemID.GlobalPhysicsStateManager;
                case GlobalRegistryServiceSlot.UI:
                    return SystemID.UI;
                case GlobalRegistryServiceSlot.DataVault:
                    return SystemID.CoreDataVault;
                case GlobalRegistryServiceSlot.CausticsRuntime:
                    return SystemID.Vfx;
                case GlobalRegistryServiceSlot.AmbientBiotaRuntime:
                    return SystemID.AmbientBiota;
                case GlobalRegistryServiceSlot.ResolutionScalerService:
                    return SystemID.GraphicsScalability;
                case GlobalRegistryServiceSlot.ProceduralLadderClimbRuntime:
                    return SystemID.AnimationLocomotion;
                default:
                    if (serviceSlot == GlobalRegistryServiceSlot.Unknown)
                        return SystemID.Unknown;

                    int ownerValue = 256 + (int)serviceSlot;
                    return ownerValue <= ushort.MaxValue ? (SystemID)ownerValue : SystemID.External;
            }
        }

        /// <summary>
        /// Flushes all queued service rebound events in one deterministic late-frame batch.
        /// </summary>
        public static void FlushPendingServiceReboundEvents()
        {
            if (!_pendingServiceRebounds.IsCreated)
                return;

            bool completed = false;
            _isDispatchingServiceRebounds = true;
            try
            {
                int scanBudget = _pendingServiceReboundCount > 0
                    ? _pendingServiceReboundCount
                    : MaxPendingServiceRebounds;
                while (scanBudget-- > 0 && !_pendingServiceRebounds.IsEmpty())
                {
                    if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                        return;

                    if (!_pendingServiceRebounds.TryDequeue(out RegistryEventPayload payload))
                    {
                        _pendingServiceReboundCount = 0;
                        break;
                    }

                    if (_pendingServiceReboundCount > 0)
                        _pendingServiceReboundCount--;

                    RegistryReboundReferenceSlot referenceSlot = default;
                    if ((uint)payload.ReferenceSlot < MaxPendingServiceRebounds &&
                        _serviceReboundReferenceSlotOccupied[payload.ReferenceSlot])
                    {
                        referenceSlot = _serviceReboundReferenceSlots[payload.ReferenceSlot];
                    }

                    DispatchRegistryEvent(in payload);
                    NotifyHotSwapListeners(
                        (GlobalRegistryServiceSlot)payload.ServiceSlot,
                        referenceSlot.PreviousService,
                        referenceSlot.CurrentService);
                    ReleaseServiceReboundReferenceSlot(payload.ReferenceSlot);
                }

                if (_pendingServiceRebounds.IsEmpty())
                    _pendingServiceReboundCount = 0;

                completed = true;
            }
            finally
            {
                _isDispatchingServiceRebounds = false;
            }

            if (!completed || !_pendingServiceRebounds.IsEmpty())
                return;

            PromoteNextFrameServiceRebounds();
        }

        private static void QueueServiceRebound(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Unknown)
                return;

            if (_suppressServiceReboundQueueing)
                return;

#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating)
            {
                return;
            }
#endif

            EnsureServiceReboundQueue();
            if (_pendingServiceReboundCount + _nextFrameServiceReboundCount >= MaxPendingServiceRebounds)
            {
                PublishServiceReboundOverflowWarning();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!_serviceReboundOverflowLogged)
                {
                    _serviceReboundOverflowLogged = true;
                    Debug.LogError("[GlobalRegistry] Service rebound queue overflow. Increase MaxPendingServiceRebounds.");
                }
#endif
                return;
            }

            int referenceSlot = ReserveServiceReboundReferenceSlot(previousService, currentService);
            if (referenceSlot < 0)
            {
                PublishServiceReboundOverflowWarning();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!_serviceReboundOverflowLogged)
                {
                    _serviceReboundOverflowLogged = true;
                    Debug.LogError("[GlobalRegistry] Service rebound queue overflow. Increase MaxPendingServiceRebounds.");
                }
#endif
                return;
            }

            RegistryEventPayload payload = new RegistryEventPayload
            {
                PreviousServiceHash = ComputeObjectHash(previousService),
                CurrentServiceHash = ComputeObjectHash(currentService),
                ReferenceSlot = referenceSlot,
                FrameIndex = SystemDispatcher.CurrentFrameId,
                ServiceSlot = (ushort)serviceSlot,
                EventType = (ushort)RegistryEventType.ServiceRebound
            };

            if (_isDispatchingServiceRebounds)
            {
                _nextFrameServiceRebounds.Enqueue(payload);
                _nextFrameServiceReboundCount++;
            }
            else
            {
                _pendingServiceRebounds.Enqueue(payload);
                _pendingServiceReboundCount++;
            }
        }

        private static void PublishServiceReboundOverflowWarning()
        {
            GlobalTelemetryBus.PublishPerformanceWarning(
                _serviceReboundOverflowWarningHash,
                _globalRegistryTelemetryContextHash,
                _pendingServiceReboundCount + _nextFrameServiceReboundCount);
        }

        private static void EnsureServiceReboundQueue()
        {
            if (!_pendingServiceRebounds.IsCreated)
                _pendingServiceRebounds = CreateServiceReboundQueue(nameof(_pendingServiceRebounds), out _pendingServiceReboundsSentinelId); // COLD ALLOC: NativeQueue<RegistryEventPayload>[64] - service rebound event lane - owner: GlobalRegistry

            if (!_nextFrameServiceRebounds.IsCreated)
                _nextFrameServiceRebounds = CreateServiceReboundQueue(nameof(_nextFrameServiceRebounds), out _nextFrameServiceReboundsSentinelId); // COLD ALLOC: NativeQueue<RegistryEventPayload>[64] - next-frame service rebound event lane - owner: GlobalRegistry
        }

        private static NativeQueue<RegistryEventPayload> CreateServiceReboundQueue(string label, out int sentinelId)
        {
            sentinelId = 0;
            NativeQueue<RegistryEventPayload> queue = new NativeQueue<RegistryEventPayload>(Allocator.Persistent);
            try
            {
                sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(
                    queue,
                    MaxPendingServiceRebounds,
                    nameof(GlobalRegistry),
                    label,
                    NativeAllocationLifetime.Session);
                if (sentinelId <= 0)
                    throw new InvalidOperationException("NativeMemorySentinel rejected GlobalRegistry service rebound queue registration.");

                PrewarmQueue(ref queue, MaxPendingServiceRebounds);
                return queue;
            }
            catch (Exception exception)
            {
                try
                {
                    DisposeServiceReboundQueue(ref queue, ref sentinelId);
                }
                catch (Exception releaseException)
                {
                    throw new AggregateException("GlobalRegistry service rebound queue allocation cleanup failed.", exception, releaseException);
                }

                throw;
            }
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

        private static void PromoteNextFrameServiceRebounds()
        {
            if (!_nextFrameServiceRebounds.IsCreated || _nextFrameServiceReboundCount <= 0)
                return;

            while (_nextFrameServiceReboundCount > 0 &&
                   _nextFrameServiceRebounds.TryDequeue(out RegistryEventPayload payload))
            {
                _nextFrameServiceReboundCount--;
                _pendingServiceRebounds.Enqueue(payload);
                _pendingServiceReboundCount++;
            }
        }

        private static int ReserveServiceReboundReferenceSlot(object previousService, object currentService)
        {
            if (_serviceReboundReferencePendingCount >= MaxPendingServiceRebounds)
                return -1;

            for (int attempt = 0; attempt < MaxPendingServiceRebounds; attempt++)
            {
                int slot = _serviceReboundReferenceWriteIndex;
                _serviceReboundReferenceWriteIndex = (_serviceReboundReferenceWriteIndex + 1) % MaxPendingServiceRebounds;
                if (_serviceReboundReferenceSlotOccupied[slot])
                    continue;

                _serviceReboundReferenceSlotOccupied[slot] = true;
                _serviceReboundReferenceSlots[slot].PreviousService = previousService;
                _serviceReboundReferenceSlots[slot].CurrentService = currentService;
                _serviceReboundReferencePendingCount++;
                return slot;
            }

            return -1;
        }

        private static void ReleaseServiceReboundReferenceSlot(int slot)
        {
            if ((uint)slot >= MaxPendingServiceRebounds || !_serviceReboundReferenceSlotOccupied[slot])
                return;

            _serviceReboundReferenceSlots[slot].Clear();
            _serviceReboundReferenceSlotOccupied[slot] = false;
            if (_serviceReboundReferencePendingCount > 0)
                _serviceReboundReferencePendingCount--;
        }

        private static void ClearServiceReboundReferenceSlots()
        {
            for (int index = 0; index < MaxPendingServiceRebounds; index++)
            {
                _serviceReboundReferenceSlots[index].Clear();
                _serviceReboundReferenceSlotOccupied[index] = false;
            }
        }

        private static void DispatchRegistryEvent(in RegistryEventPayload payload)
        {
            for (int index = _registryEventListeners.Count - 1; index >= 0; index--)
            {
                IRegistryEventListener listener = _registryEventListeners.GetAt(index);
                if (listener == null)
                    continue;

                listener.OnRegistryEvent(in payload);
            }
        }

        private static uint ComputeObjectHash(object value)
        {
            return value == null ? 0u : unchecked((uint)RuntimeHelpers.GetHashCode(value));
        }

        private static T ResolveSafeFallbackService<T>() where T : class
        {
            Type serviceType = typeof(T);
            if (serviceType == typeof(IInputService) || serviceType == typeof(IInputDeterminismService))
            {
                NoteNullObjectSubstitution(GlobalRegistryServiceSlot.Input, _noOpInputService);
                return _noOpInputService as T;
            }

            if (serviceType == typeof(IVRSomaticProvider))
            {
                NoteNullObjectSubstitution(GlobalRegistryServiceSlot.VRSomaticProvider, _noOpVRSomaticProvider);
                return _noOpVRSomaticProvider as T;
            }

            return null;
        }

        /// <summary>
        /// Records that a read handed out a non-null null-object substitute for an EMPTY slot.
        /// Called only from a substitution branch, which is unreachable once the slot is filled.
        /// </summary>
        private static void NoteNullObjectSubstitution(GlobalRegistryServiceSlot serviceSlot, object substitute)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Input:
                    if (_inputNullObjectSubstitutionHandedOut == null)
                        _inputNullObjectSubstitutionHandedOut = substitute;
                    break;

                case GlobalRegistryServiceSlot.VRSomaticProvider:
                    if (_vrSomaticNullObjectSubstitutionHandedOut == null)
                        _vrSomaticNullObjectSubstitutionHandedOut = substitute;
                    break;
            }
        }

        /// <summary>
        /// Takes and clears the substitution record for a slot. Returns null for the 174 slots that have no
        /// null-object substitute, which is every slot except Input and VRSomaticProvider.
        /// </summary>
        private static object ConsumeNullObjectSubstitutionRecord(GlobalRegistryServiceSlot serviceSlot)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Input:
                {
                    object substitute = _inputNullObjectSubstitutionHandedOut;
                    _inputNullObjectSubstitutionHandedOut = null;
                    return substitute;
                }

                case GlobalRegistryServiceSlot.VRSomaticProvider:
                {
                    object substitute = _vrSomaticNullObjectSubstitutionHandedOut;
                    _vrSomaticNullObjectSubstitutionHandedOut = null;
                    return substitute;
                }

                default:
                    return null;
            }
        }

        /// <summary>
        /// Runs when a slot is filled for the FIRST time - the case where Register/RegisterService skip
        /// QueueServiceRebound because previousService was null. Fills that hole for the only slots where a
        /// cold resolve can have cached something non-null and dead.
        ///
        /// WHY THIS IS NOT A BLANKET WIDENING OF THE REBOUND GATE. Two reasons, both from the code:
        ///
        /// 1. Queue capacity. GlobalRegistryServiceSlot has ~176 real slots; MaxPendingServiceRebounds is
        ///    64 and covers the pending and next-frame lanes together, and the drain is gated behind
        ///    SystemDispatcher.TryConsumeLateFrameEventDispatch, so nothing dequeues until the dispatcher
        ///    reaches its late-frame flush pass. Nothing in bootstrap throttles registrations to 64 slots
        ///    per frame. Notifying on every first fill would therefore risk hitting the overflow branch,
        ///    which DROPS payloads - including, on a bad frame, the Input notification this exists for.
        ///    Trading a silent miss for a noisy miss is not a fix.
        /// 2. Listener semantics. Several DataVault-slot listeners release handles from the vault they are
        ///    currently holding when previousService is null: SystemDispatcher.cs:4384
        ///    (`ReleaseSystemDispatcherVaultHandles(_dataVault ?? (previousService as IDataVault))`),
        ///    FabricationAssemblerRuntime.cs:929, DataArchaeologyRuntime.cs:972, HomeostasisBrain.cs:1224,
        ///    FluidPipeGraphRuntime.cs:200. A blanket null-previous first-fill rebound would hand those a
        ///    release of the live vault. Scoping to Input/VRSomaticProvider keeps the blast radius on
        ///    handlers that only reassign a cached field (SystemDispatcher.cs:4341-4343,
        ///    HectonPlayerMovement.cs:4295-4297, PlayerTool.cs:1005-1007).
        ///
        /// previousService is reported as the SUBSTITUTE rather than null on purpose: that object is
        /// literally what the consumers were holding, and it keeps any listener that assumes a non-null
        /// previousService on a rebound satisfied. Rebounds carrying a null previousService already ship via
        /// ReplaceService, so this shape is not new to the delivery path either.
        /// </summary>
        private static void ReportFirstFillAfterNullObjectSubstitution(
            GlobalRegistryServiceSlot serviceSlot,
            object instance)
        {
            object substitute = ConsumeNullObjectSubstitutionRecord(serviceSlot);
            if (substitute == null || ReferenceEquals(substitute, instance))
                return;

            GlobalTelemetryBus.PublishDependencyOrderWarning(
                _coldResolvedSubstituteWarningHash,
                (uint)(byte)serviceSlot);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(
                "[GlobalRegistry] COLD-RESOLVED NULL OBJECT LEAKED PAST FIRST REGISTRATION. Slot " +
                serviceSlot + " handed out the non-null " + substitute.GetType().Name +
                " while it was empty, and is only being filled now by " + instance.GetType().Name +
                ". Consumers that cached the substitute passed their null check and got a service that " +
                "does nothing. A rebound is being queued for this first fill so " +
                "IGlobalRegistryHotSwapListener consumers can re-resolve; anything that cached the " +
                "substitute WITHOUT implementing that interface stays dead for the rest of the session " +
                "and must re-read the property instead of caching it.");
#endif

            QueueServiceRebound(serviceSlot, substitute, instance);
        }

        internal static bool TryReplaceBootstrapServiceWithStableProxy(GlobalRegistryServiceSlot serviceSlot)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Input:
                    ReplaceService(ref _input, _noOpInputService, GlobalRegistryServiceSlot.Input);
                    return true;

                case GlobalRegistryServiceSlot.VRSomaticProvider:
                    ReplaceService(ref _vrSomaticProvider, _noOpVRSomaticProvider, GlobalRegistryServiceSlot.VRSomaticProvider);
                    return true;

                default:
                    return false;
            }
        }

        internal static void ReplaceAudioServiceForBootstrap(IAudioService instance)
        {
            if (instance == null)
                return;

            ReplaceService(ref _audio, instance, GlobalRegistryServiceSlot.Audio);
        }

        internal static object ResolveRegisteredServiceForHeartbeat(GlobalRegistryServiceSlot serviceSlot)
        {
            return ResolveRegisteredServiceObject(serviceSlot);
        }

        private static object ResolveRegisteredServiceObject(GlobalRegistryServiceSlot serviceSlot)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Input: return _input;
                case GlobalRegistryServiceSlot.NativeInputManagerRuntime: return _nativeInputManagerRuntime;
                case GlobalRegistryServiceSlot.RaycastBatchRuntime: return _raycastBatchRuntime;
                case GlobalRegistryServiceSlot.Physics: return _physics;
                case GlobalRegistryServiceSlot.Audio: return _audio;
                case GlobalRegistryServiceSlot.AudioVirtualization: return _audioVirtualization;
                case GlobalRegistryServiceSlot.Scene: return _scene;
                case GlobalRegistryServiceSlot.Save: return _save;
                case GlobalRegistryServiceSlot.UI: return _ui;
                case GlobalRegistryServiceSlot.ModalWindowRuntime: return _modalWindowRuntime;
                case GlobalRegistryServiceSlot.ARWaypointRuntime: return _arWaypoint;
                case GlobalRegistryServiceSlot.SpatialTriggerRuntime: return _spatialTriggerSystem;
                case GlobalRegistryServiceSlot.ObjectPool: return _objectPool;
                case GlobalRegistryServiceSlot.Player: return _player;
                case GlobalRegistryServiceSlot.PlayerInventory: return _playerInventory;
                case GlobalRegistryServiceSlot.ModularEquipment: return _modularEquipment;
                case GlobalRegistryServiceSlot.PlayerSensory: return _playerSensory;
                case GlobalRegistryServiceSlot.Environment: return _environment;
                case GlobalRegistryServiceSlot.ChemicalInfluenceRuntime: return _chemicalInfluence;
                case GlobalRegistryServiceSlot.DestructibleOrganicRuntime: return _organicToolHits;
                case GlobalRegistryServiceSlot.Weather: return _weather;
                case GlobalRegistryServiceSlot.SeismicDirectorRuntime: return _seismicDirectorRuntime;
                case GlobalRegistryServiceSlot.OceanKinematics: return _oceanKinematics;
                case GlobalRegistryServiceSlot.PowerGrid: return _powerGrid;
                case GlobalRegistryServiceSlot.Submarine: return _submarine;
                case GlobalRegistryServiceSlot.SubmarineState: return _submarineState;
                case GlobalRegistryServiceSlot.SubmarineHullBreach: return _submarineHullBreach;
                case GlobalRegistryServiceSlot.InertialNavigationRuntime: return _inertialNavigation;
                case GlobalRegistryServiceSlot.DockingAutopilotRuntime: return _dockingAutopilot;
                case GlobalRegistryServiceSlot.InteractionSignals: return _interactionSignals;
                case GlobalRegistryServiceSlot.Debris: return _debris;
                case GlobalRegistryServiceSlot.DebrisComputeRuntime: return _debrisCompute;
                case GlobalRegistryServiceSlot.AmbientBiotaRuntime: return _ambientBiotaRuntime;
                case GlobalRegistryServiceSlot.EcosystemDirector: return _ecosystemDirector;
                case GlobalRegistryServiceSlot.ThermodynamicsService: return _thermodynamicsService;
                case GlobalRegistryServiceSlot.Logistics: return _logistics;
                case GlobalRegistryServiceSlot.HabitatDeconstructionRuntime: return _habitatDeconstruction;
                case GlobalRegistryServiceSlot.FluidPipeGraph: return _fluidPipeGraph;
                case GlobalRegistryServiceSlot.GasDynamicsRuntime: return _gasDynamics;
                case GlobalRegistryServiceSlot.WorldGen: return _worldGen;
                case GlobalRegistryServiceSlot.EncounterDirector: return _encounterDirector;
                case GlobalRegistryServiceSlot.QuestSystem: return _questSystem;
                case GlobalRegistryServiceSlot.FluidRuntime: return _fluidRuntime;
                case GlobalRegistryServiceSlot.ThermodynamicsRuntime: return _thermodynamicsRuntime;
                case GlobalRegistryServiceSlot.NarrativeDirectorRuntime: return _narrativeDirectorRuntime;
                case GlobalRegistryServiceSlot.CorporateOrderRuntime: return _corporateOrderRuntime;
                case GlobalRegistryServiceSlot.QuestRuntime: return _questRuntime;
                case GlobalRegistryServiceSlot.TickManager: return _tickManager;
                case GlobalRegistryServiceSlot.Dispatcher: return _dispatcher;
                case GlobalRegistryServiceSlot.RenderDispatcher: return _renderDispatcher;
                case GlobalRegistryServiceSlot.PhysicsStateManager: return _physicsStateManager;
                case GlobalRegistryServiceSlot.FaunaSimulation: return _faunaSimulation;
                case GlobalRegistryServiceSlot.FluidSimulation: return _fluidSimulation;
                case GlobalRegistryServiceSlot.PersistentWorldRegistry: return _persistentWorldRegistry;
                case GlobalRegistryServiceSlot.PDALogbook: return _pdaLogbook;
                case GlobalRegistryServiceSlot.PlayerMotor: return _playerMotor;
                case GlobalRegistryServiceSlot.PlayerMovementContracts: return _playerMovementContracts;
                case GlobalRegistryServiceSlot.Profile: return _profile;
                case GlobalRegistryServiceSlot.InputBinding: return _inputBinding;
                case GlobalRegistryServiceSlot.CullingRuntime: return _cullingRuntime;
                case GlobalRegistryServiceSlot.LODSystemRuntime: return _lodSystemRuntime;
                case GlobalRegistryServiceSlot.DynamicResolutionRuntime: return _dynamicResolutionRuntime;
                case GlobalRegistryServiceSlot.ResolutionScalerService: return _resolutionScalerService;
                case GlobalRegistryServiceSlot.ImpostorRuntime: return _impostorRuntime;
                case GlobalRegistryServiceSlot.DepthZoneRuntime: return _depthZoneRuntime;
                case GlobalRegistryServiceSlot.LocalizationRuntime: return _localizationRuntime ?? _babelLocalizationRuntime;
                case GlobalRegistryServiceSlot.AudioLogRuntime: return _audioLogRuntime;
                case GlobalRegistryServiceSlot.AtlasSignalRuntime: return _atlasSignalRuntime;
                case GlobalRegistryServiceSlot.FirstHourRuntime: return _firstHourRuntime;
                case GlobalRegistryServiceSlot.EmergencyRelayRuntime: return _emergencyRelayRuntime;
                case GlobalRegistryServiceSlot.AtmosphereRuntime: return _atmosphereRuntime;
                case GlobalRegistryServiceSlot.TerrainProviderRuntime: return _terrainProviderRuntime;
                case GlobalRegistryServiceSlot.BeaconNetworkRuntime: return _beaconNetworkRuntime;
                case GlobalRegistryServiceSlot.ScanLogRuntime: return _scanLogRuntime;
                case GlobalRegistryServiceSlot.ToolDurabilityRuntime: return _toolDurabilityRuntime;
                case GlobalRegistryServiceSlot.ToolHapticsRuntime: return _toolHapticsRuntime;
                case GlobalRegistryServiceSlot.VRSomaticProvider: return _vrSomaticProvider;
                case GlobalRegistryServiceSlot.LoreDatabaseRuntime: return _loreDatabaseRuntime;
                case GlobalRegistryServiceSlot.AssetLifecycleRuntime: return _assetLifecycleRuntime;
                case GlobalRegistryServiceSlot.AssetLoadDispatcherRuntime: return _assetLoadDispatcherRuntime;
                case GlobalRegistryServiceSlot.VRAMMonitorRuntime: return _vramMonitorRuntime;
                case GlobalRegistryServiceSlot.VRAMPressureRuntime: return _vramPressureRuntime;
                case GlobalRegistryServiceSlot.RenderTextureLifecycleRuntime: return _renderTextureLifecycleRuntime;
                case GlobalRegistryServiceSlot.RenderTexturePoolRuntime: return _renderTexturePoolRuntime;
                case GlobalRegistryServiceSlot.WorldStateRuntime: return _worldStateRuntime;
                case GlobalRegistryServiceSlot.UserOptionsRuntime: return _userOptionsRuntime;
                case GlobalRegistryServiceSlot.BiolumManagerRuntime: return _biolumManagerRuntime;
                case GlobalRegistryServiceSlot.BiolumControllerRuntime: return _biolumControllerRuntime;
                case GlobalRegistryServiceSlot.AbyssalFluidDecalRuntime: return _abyssalFluidDecalRuntime;
                case GlobalRegistryServiceSlot.SargassumDragRuntime: return _sargassumDragRuntime;
                case GlobalRegistryServiceSlot.SargassumCutRuntime: return _sargassumCutRuntime;
                case GlobalRegistryServiceSlot.PlayerExpressionRuntime: return _playerExpressionRuntime;
                case GlobalRegistryServiceSlot.SpectrumRuntime: return _spectrumRuntime;
                case GlobalRegistryServiceSlot.SoundscapeRuntime: return _soundscapeRuntime;
                case GlobalRegistryServiceSlot.AcousticZoneRuntime: return _acousticZoneRuntime;
                case GlobalRegistryServiceSlot.SurfaceWeatherRuntime: return _surfaceWeatherRuntime;
                case GlobalRegistryServiceSlot.EnvironmentalStrainRuntime: return _environmentalStrainRuntime;
                case GlobalRegistryServiceSlot.EcosystemHealthRuntime: return _ecosystemHealthRuntime;
                case GlobalRegistryServiceSlot.FaunaGeneticsRuntime: return _faunaGeneticsRuntime;
                case GlobalRegistryServiceSlot.PlayerExplorationRuntime: return _playerExplorationRuntime;
                case GlobalRegistryServiceSlot.DynamicDifficultyRuntime: return _dynamicDifficultyRuntime;
                case GlobalRegistryServiceSlot.DiscoveryRuntime: return _discoveryRuntime;
                case GlobalRegistryServiceSlot.ResourceScarcityRuntime: return _resourceScarcityRuntime;
                case GlobalRegistryServiceSlot.FieldOperationLogRuntime: return _fieldOperationLogRuntime;
                case GlobalRegistryServiceSlot.PDAExchangeRuntime: return _pdaExchangeRuntime;
                case GlobalRegistryServiceSlot.PlayerActionRuntime: return _playerActionRuntime;
                case GlobalRegistryServiceSlot.PDAMarkerRuntime: return _pdaMarkerRuntime;
                case GlobalRegistryServiceSlot.PDAIntrusionRuntime: return _pdaIntrusionRuntime;
                case GlobalRegistryServiceSlot.AmbientWaterMotionRuntime: return _ambientWaterMotionRuntime;
                case GlobalRegistryServiceSlot.SuitUpgradeRuntime: return _suitUpgradeRuntime;
                case GlobalRegistryServiceSlot.UIAudioFeedbackRuntime: return _uiAudioFeedbackRuntime;
                case GlobalRegistryServiceSlot.UITooltipRuntime: return _uiTooltipRuntime;
                case GlobalRegistryServiceSlot.LoadingScreenRuntime: return _loadingScreenRuntime;
                case GlobalRegistryServiceSlot.EndingRuntime: return _endingRuntime;
                case GlobalRegistryServiceSlot.Atlas6DirectiveRuntime: return _atlas6DirectiveRuntime;
                case GlobalRegistryServiceSlot.HazardZoneRuntime: return _hazardZoneRuntime;
                case GlobalRegistryServiceSlot.MissionRuntime: return _missionRuntime;
                case GlobalRegistryServiceSlot.RockManagerRuntime: return _rockManagerRuntime;
                case GlobalRegistryServiceSlot.CameraJuiceRuntime: return _cameraJuiceRuntime;
                case GlobalRegistryServiceSlot.MusicDirectorRuntime: return _musicDirectorRuntime;
                case GlobalRegistryServiceSlot.SubtitleRuntime: return _subtitleRuntime;
                case GlobalRegistryServiceSlot.AtlasSignalDecoderRuntime: return _atlasSignalDecoderRuntime;
                case GlobalRegistryServiceSlot.ScrapRuntime: return _scrapRuntime;
                case GlobalRegistryServiceSlot.AutonomousExtractorRuntime: return _autonomousExtractorRuntime;
                case GlobalRegistryServiceSlot.VisorRTRuntime: return _visorRTRuntime;
                case GlobalRegistryServiceSlot.CameraRTRuntime: return _cameraRTRuntime;
                case GlobalRegistryServiceSlot.PostFXRTRuntime: return _postFXRTRuntime;
                case GlobalRegistryServiceSlot.UIRTRuntime: return _uiRTRuntime;
                case GlobalRegistryServiceSlot.SettingsRuntime: return _settingsRuntime;
                case GlobalRegistryServiceSlot.RuntimeWatchdogRuntime: return _runtimeWatchdogRuntime;
                case GlobalRegistryServiceSlot.CrashTelemetryRuntime: return _crashTelemetryRuntime;
                case GlobalRegistryServiceSlot.PlayerCriticalAudioRuntime: return _playerCriticalAudioRuntime;
                case GlobalRegistryServiceSlot.VocalWarningRuntime: return _vocalWarningRuntime;
                case GlobalRegistryServiceSlot.MapMagicRuntime: return _mapMagicRuntime;
                case GlobalRegistryServiceSlot.MapMagicVegetationRuntime: return _mapMagicVegetationRuntime;
                case GlobalRegistryServiceSlot.ScavengePopulatorRuntime: return _scavengePopulatorRuntime;
                case GlobalRegistryServiceSlot.ModWorldPersistenceRuntime: return _modWorldPersistenceRuntime;
                case GlobalRegistryServiceSlot.ModdingBridgeRuntime: return _moddingBridgeRuntime;
                case GlobalRegistryServiceSlot.RunModifierRuntime: return _runModifierRuntime;
                case GlobalRegistryServiceSlot.MetaCampaignRuntime: return _metaCampaignRuntime;
                case GlobalRegistryServiceSlot.MigrationDirectorRuntime: return _migrationDirectorRuntime;
                case GlobalRegistryServiceSlot.BasePollutionRuntime: return _basePollutionRuntime;
                case GlobalRegistryServiceSlot.EntityChangeManagerRuntime: return _entityChangeManagerRuntime;
                case GlobalRegistryServiceSlot.PerformanceMonitorRuntime: return _performanceMonitorRuntime;
                case GlobalRegistryServiceSlot.ProceduralFieldSamplerRuntime: return _proceduralFieldSamplerRuntime;
                case GlobalRegistryServiceSlot.ResourceDistributionRuntime: return _resourceDistributionRuntime;
                case GlobalRegistryServiceSlot.RandomEventRuntime: return _randomEventRuntime;
                case GlobalRegistryServiceSlot.EclipseGameplayRuntime: return _eclipseGameplayRuntime;
                case GlobalRegistryServiceSlot.CelestialEngineRuntime: return _celestialEngineRuntime;
                case GlobalRegistryServiceSlot.OrbitalDirectorRuntime: return _orbitalDirectorRuntime;
                case GlobalRegistryServiceSlot.PrologueSequenceRuntime: return _prologueSequenceRuntime;
                case GlobalRegistryServiceSlot.WorldSeedProvider: return _worldSeedProvider;
                case GlobalRegistryServiceSlot.GeologyTerrainSeamRuntime: return _geologyTerrainSeamRuntime;
                case GlobalRegistryServiceSlot.GeologyVoxelBridgeRuntime: return _geologyVoxelBridgeRuntime;
                case GlobalRegistryServiceSlot.VoxelEngineRuntime: return _voxelEngineRuntime;
                case GlobalRegistryServiceSlot.BiomeMatrixRuntime: return _biomeMatrixRuntime;
                case GlobalRegistryServiceSlot.UnderwaterVisualsRuntime: return _underwaterVisualsRuntime;
                case GlobalRegistryServiceSlot.GIRelayRuntime: return _giRelayRuntime;
                case GlobalRegistryServiceSlot.ProceduralSwayDirectorRuntime: return _proceduralSwayDirectorRuntime;
                case GlobalRegistryServiceSlot.SargassumMicroFaunaRuntime: return _sargassumMicroFaunaRuntime;
                case GlobalRegistryServiceSlot.FloatingOriginRuntime: return _floatingOriginRuntime;
                case GlobalRegistryServiceSlot.ConnectionSplineBatchRendererRuntime: return _connectionSplineBatchRendererRuntime;
                case GlobalRegistryServiceSlot.DataVault: return _dataVault;
                case GlobalRegistryServiceSlot.CablePhysics132Runtime: return _cablePhysics132Runtime;
                case GlobalRegistryServiceSlot.MacroDatabase: return _macroDatabase;
                case GlobalRegistryServiceSlot.CausticsRuntime: return _causticsRuntime;
                case GlobalRegistryServiceSlot.JobAdmissionRuntime: return _jobAdmissionRuntime;
                case GlobalRegistryServiceSlot.SimulationBucketerRuntime: return _simulationBucketerRuntime;
                case GlobalRegistryServiceSlot.StreamingBackpressureRuntime: return _streamingBackpressureRuntime;
                case GlobalRegistryServiceSlot.FoveatedSimulationDirector: return _foveatedSimulationDirector;
                case GlobalRegistryServiceSlot.HardwareThermalService: return _hardwareThermalService;
                case GlobalRegistryServiceSlot.GroundRadarRuntime: return _groundRadarRuntime;
                case GlobalRegistryServiceSlot.WorldResourceSpawnerRuntime: return _worldResourceSpawnerRuntime;
                case GlobalRegistryServiceSlot.InstanceCullingRuntime: return _instanceCullingRuntime;
                case GlobalRegistryServiceSlot.OutpostGenerationRuntime: return _outpostGenerationRuntime;
                default: return null;
            }
        }

        public static void ShutdownRegisteredServicesInReverseSlotOrder()
        {
            DisposeAllRegisteredServices();
        }

        public static void DisposeAllRegisteredServices()
        {
            for (int slot = (int)GlobalRegistryServiceSlot.Unknown - 1; slot >= 0; slot--)
                ShutdownRegisteredServiceSlot((GlobalRegistryServiceSlot)slot);
        }

        public static void GlobalReset()
        {
            ResetStaticState();
        }

        private static void ShutdownRegisteredServices()
        {
            ShutdownRegisteredServicesInReverseSlotOrder();
        }

        public static void ShutdownRegisteredServiceSlot(GlobalRegistryServiceSlot slot)
        {
            object service = ResolveRegisteredServiceObject(slot);
            if (service is IServiceShutdown shutdown)
                ShutdownRegisteredService(shutdown);
        }

        private static void ShutdownRegisteredService(IServiceShutdown shutdown)
        {
            try
            {
                shutdown.DisposeAll();
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogException(exception);
#endif
            }
        }

        public static uint CalculateActiveServiceTypeFnv1a()
        {
            const uint fnvOffset = 2166136261u;
            const uint fnvPrime = 16777619u;
            uint hash = fnvOffset;

            for (int slot = 0; slot < (int)GlobalRegistryServiceSlot.Unknown; slot++)
            {
                object service = ResolveRegisteredServiceObject((GlobalRegistryServiceSlot)slot);
                if (service == null)
                    continue;

                Type serviceType = service.GetType();
                string fullName = serviceType.FullName ?? serviceType.Name;
                for (int i = 0; i < fullName.Length; i++)
                {
                    hash ^= fullName[i];
                    hash *= fnvPrime;
                }

                hash ^= (uint)slot;
                hash *= fnvPrime;
            }

            _activeServiceTypeHash = hash;
            return hash;
        }

        private static void NotifyHotSwapListeners(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            for (int index = _hotSwapListeners.Count - 1; index >= 0; index--)
            {
                IGlobalRegistryHotSwapListener listener = _hotSwapListeners.GetAt(index);
                if (listener == null)
                    continue;

                object reboundService = currentService;
                if (listener is IGlobalRegistryHotSwapRefListener refListener)
                    refListener.OnGlobalRegistryServiceRebound(serviceSlot, ref reboundService);

                listener.OnGlobalRegistryServiceReplaced(serviceSlot, previousService, reboundService);
            }
        }

        [Preserve]
        private static GlobalRegistryServiceSlot ResolveServiceSlot<T>() where T : class
        {
            return ServiceSlotCache<T>.Slot;
        }

        [Preserve]
        private static readonly System.Collections.Generic.Dictionary<Type, GlobalRegistryServiceSlot> _serviceSlotMap = new System.Collections.Generic.Dictionary<Type, GlobalRegistryServiceSlot>
        {
            { typeof(IInputDeterminismService), GlobalRegistryServiceSlot.Input },
            { typeof(IInputService), GlobalRegistryServiceSlot.Input },
            { typeof(IInputBindingService), GlobalRegistryServiceSlot.InputBinding },
            { typeof(INativeInputManagerRuntime), GlobalRegistryServiceSlot.NativeInputManagerRuntime },
            { typeof(RaycastBatchHelper), GlobalRegistryServiceSlot.RaycastBatchRuntime },
            { typeof(IPhysicsService), GlobalRegistryServiceSlot.Physics },
            { typeof(IAudioService), GlobalRegistryServiceSlot.Audio },
            { typeof(ISpatialAudioImpactEmitterReadModel), GlobalRegistryServiceSlot.Audio },
            { typeof(ISpatialAudioWorldEmitterReadModel), GlobalRegistryServiceSlot.Audio },
            { typeof(ISpatialAudioListenerCaveReadModel), GlobalRegistryServiceSlot.Audio },
            { typeof(ISpatialAudioBinauralEmitterReadModel), GlobalRegistryServiceSlot.Audio },
            { typeof(IMeteorShowerAudioSink), GlobalRegistryServiceSlot.Audio },
            { typeof(ISpatialAudioLowPassPlayback), GlobalRegistryServiceSlot.Audio },
            { typeof(ISpatialAudioEnvironmentModulationSink), GlobalRegistryServiceSlot.Audio },
            { typeof(ISpatialAudioSfxMixerRouteReadModel), GlobalRegistryServiceSlot.Audio },
            { typeof(ISpatialAudioNarrativeRadioSink), GlobalRegistryServiceSlot.Audio },
            { typeof(ISpatialAudioInventoryRunawaySink), GlobalRegistryServiceSlot.Audio },
            { typeof(ISpatialAudioHarvestPlaybackSink), GlobalRegistryServiceSlot.Audio },
            { typeof(ISpatialAudioWeatherPlaybackSink), GlobalRegistryServiceSlot.Audio },
            { typeof(IPlayerCriticalAudioSignalSink), GlobalRegistryServiceSlot.PlayerCriticalAudioRuntime },
            { typeof(IPlayerCriticalSonarEchoReadModel), GlobalRegistryServiceSlot.PlayerCriticalAudioRuntime },
            { typeof(IAudioVirtualizationService), GlobalRegistryServiceSlot.AudioVirtualization },
            { typeof(IToolAcousticCueService), GlobalRegistryServiceSlot.AcousticZoneRuntime },
            { typeof(ISceneService), GlobalRegistryServiceSlot.Scene },
            // RegisterSceneRuntime infers T from the concrete _sceneRuntime field, so the concrete type
            // needs the same slot as its interface. Unmapped it resolves to Unknown, which
            // IsSceneRuntimeHotSwapSlot rejects, so the scene publication gate cannot issue a token for a
            // re-register that happens after LockReady. Same defect class as PlayerRuntimeContextService.
            { typeof(SceneRuntimeService), GlobalRegistryServiceSlot.Scene },
            { typeof(ISaveService), GlobalRegistryServiceSlot.Save },
            { typeof(IAsyncPersistenceService), GlobalRegistryServiceSlot.Save },
            { typeof(IUIService), GlobalRegistryServiceSlot.UI },
            { typeof(IModalWindowService), GlobalRegistryServiceSlot.ModalWindowRuntime },
            { typeof(IARWaypointService), GlobalRegistryServiceSlot.ARWaypointRuntime },
            { typeof(ISpatialTriggerSystem), GlobalRegistryServiceSlot.SpatialTriggerRuntime },
            { typeof(IObjectPoolService), GlobalRegistryServiceSlot.ObjectPool },
            { typeof(ObjectPoolManager), GlobalRegistryServiceSlot.ObjectPool },
            { typeof(IPlayerRuntimeContext), GlobalRegistryServiceSlot.Player },
            // Scene-owned services register by concrete type through RegisterPlayerRuntimeContextRuntime,
            // so the concrete type needs the same slot as its interface. Without it the slot resolves to
            // Unknown, which IsSceneRuntimeHotSwapSlot rejects, and the scene publication gate can never
            // issue a token for a scene load that happens after LockReady.
            { typeof(PlayerRuntimeContextService), GlobalRegistryServiceSlot.Player },
            { typeof(HectonPlayerMotor), GlobalRegistryServiceSlot.PlayerMotor },
            { typeof(IPlayerSeatLockMotorSink), GlobalRegistryServiceSlot.PlayerMotor },
            { typeof(IPlayerMovementContracts), GlobalRegistryServiceSlot.PlayerMovementContracts },
            { typeof(IPlayerMovementPoseReadModel), GlobalRegistryServiceSlot.PlayerMovementContracts },
            { typeof(IPlayerMovementForceSink), GlobalRegistryServiceSlot.PlayerMovementContracts },
            { typeof(IPlayerMovementTraumaSink), GlobalRegistryServiceSlot.PlayerMovementContracts },
            { typeof(IPlayerMovementEnvironmentSink), GlobalRegistryServiceSlot.PlayerMovementContracts },
            { typeof(IPlayerMovementSonarEmitter), GlobalRegistryServiceSlot.PlayerMovementContracts },
            { typeof(IPlayerInventoryService), GlobalRegistryServiceSlot.PlayerInventory },
            { typeof(IModularEquipmentService), GlobalRegistryServiceSlot.ModularEquipment },
            { typeof(IPlayerSensoryService), GlobalRegistryServiceSlot.PlayerSensory },
            // RegisterPlayerSensoryRuntime infers T from the concrete _playerSensoryRuntime field.
            { typeof(PlayerSensoryManager), GlobalRegistryServiceSlot.PlayerSensory },
            { typeof(IEnvironmentRuntimeContext), GlobalRegistryServiceSlot.Environment },
            // RegisterEnvironmentRuntimeContextRuntime infers T from the concrete backing field, and this
            // service is scene-owned, so it re-registers on every 02_HECTON_WORLD activation after LockReady.
            { typeof(EnvironmentRuntimeContextService), GlobalRegistryServiceSlot.Environment },
            { typeof(IChemicalInfluenceReadModel), GlobalRegistryServiceSlot.ChemicalInfluenceRuntime },
            { typeof(IOrganicToolHitService), GlobalRegistryServiceSlot.DestructibleOrganicRuntime },
            { typeof(IWeatherService), GlobalRegistryServiceSlot.Weather },
            { typeof(ISeismicDirector), GlobalRegistryServiceSlot.SeismicDirectorRuntime },
            { typeof(IHectonOceanKinematicsService), GlobalRegistryServiceSlot.OceanKinematics },
            // Registered by concrete type from OceanKinematicsRuntimeService.EnsureSingletonOwnership,
            // which runs from Crest4KinematicsAdapter.OnEnable while 02_HECTON_WORLD activates.
            { typeof(OceanKinematicsRuntimeService), GlobalRegistryServiceSlot.OceanKinematics },
            { typeof(IPowerGridService), GlobalRegistryServiceSlot.PowerGrid },
            { typeof(ISubmarineRuntimeContext), GlobalRegistryServiceSlot.Submarine },
            { typeof(ISubmarineState), GlobalRegistryServiceSlot.SubmarineState },
            { typeof(ISubmarineHullBreachReadModel), GlobalRegistryServiceSlot.SubmarineHullBreach },
            { typeof(IInertialNavigationService), GlobalRegistryServiceSlot.InertialNavigationRuntime },
            { typeof(IDockingAutopilotService), GlobalRegistryServiceSlot.DockingAutopilotRuntime },
            { typeof(ProceduralLadderClimbRuntime), GlobalRegistryServiceSlot.ProceduralLadderClimbRuntime },
            { typeof(IInteractionSignalService), GlobalRegistryServiceSlot.InteractionSignals },
            { typeof(IDebrisService), GlobalRegistryServiceSlot.Debris },
            { typeof(IDebrisComputeService), GlobalRegistryServiceSlot.DebrisComputeRuntime },
            { typeof(IAmbientBiotaService), GlobalRegistryServiceSlot.AmbientBiotaRuntime },
            { typeof(IEcosystemDirectorService), GlobalRegistryServiceSlot.EcosystemDirector },
            { typeof(IFaunaSim), GlobalRegistryServiceSlot.FaunaSimulation },
            { typeof(IThermodynamicsService), GlobalRegistryServiceSlot.ThermodynamicsService },
            { typeof(IFluidSim), GlobalRegistryServiceSlot.FluidSimulation },
            { typeof(ILogisticsService), GlobalRegistryServiceSlot.Logistics },
            { typeof(IHabitatGraphService), GlobalRegistryServiceSlot.Logistics },
            { typeof(IConstructionParasiteGraphService), GlobalRegistryServiceSlot.Logistics },
            { typeof(IHabitatDeconstructionSystem), GlobalRegistryServiceSlot.HabitatDeconstructionRuntime },
            { typeof(IFluidPipeGraphService), GlobalRegistryServiceSlot.FluidPipeGraph },
            { typeof(IGasDynamicsSolver), GlobalRegistryServiceSlot.GasDynamicsRuntime },
            { typeof(IWorldGenService), GlobalRegistryServiceSlot.WorldGen },
            { typeof(IWorldSeedProvider), GlobalRegistryServiceSlot.WorldSeedProvider },
            { typeof(IBiomePhysicsInfluenceReadModel), GlobalRegistryServiceSlot.ProceduralFieldSamplerRuntime },
            { typeof(WorldProceduralFieldSampler), GlobalRegistryServiceSlot.ProceduralFieldSamplerRuntime },
            { typeof(IBrineFluidDensityReadModel), GlobalRegistryServiceSlot.ResourceDistributionRuntime },
            { typeof(ResourceDistributionDirector), GlobalRegistryServiceSlot.ResourceDistributionRuntime },
            { typeof(ITerrainHeightSampleReadModel), GlobalRegistryServiceSlot.MapMagicVegetationRuntime },
            { typeof(IVegetationThreatReadModel), GlobalRegistryServiceSlot.MapMagicVegetationRuntime },
            { typeof(IVegetationThreatPulseSink), GlobalRegistryServiceSlot.MapMagicVegetationRuntime },
            { typeof(HectonMapMagicVegetationBridge), GlobalRegistryServiceSlot.MapMagicVegetationRuntime },
            { typeof(WorldGenerativeGeologyTerrainSeamApplier), GlobalRegistryServiceSlot.GeologyTerrainSeamRuntime },
            { typeof(WorldGenerativeGeologyVoxelBridgeDirector), GlobalRegistryServiceSlot.GeologyVoxelBridgeRuntime },
            { typeof(HectonVoxelEngine), GlobalRegistryServiceSlot.VoxelEngineRuntime },
            { typeof(BiomeMatrixDirector), GlobalRegistryServiceSlot.BiomeMatrixRuntime },
            { typeof(HectonUnderwaterVisuals), GlobalRegistryServiceSlot.UnderwaterVisualsRuntime },
            { typeof(IGIRelaySystem), GlobalRegistryServiceSlot.GIRelayRuntime },
            { typeof(IWakeDisplacementService), GlobalRegistryServiceSlot.ProceduralSwayDirectorRuntime },
            { typeof(IProceduralSwayDirector), GlobalRegistryServiceSlot.ProceduralSwayDirectorRuntime },
            { typeof(IEncounterDirectorService), GlobalRegistryServiceSlot.EncounterDirector },
            { typeof(IQuestSystem), GlobalRegistryServiceSlot.QuestSystem },
            { typeof(ISceneTransitionWorldResidencyBridge), GlobalRegistryServiceSlot.PersistentWorldRegistry },
            { typeof(IRuntimeWatchdogWorldHealthBridge), GlobalRegistryServiceSlot.PersistentWorldRegistry },
            { typeof(INutrientThermalVentReadModel), GlobalRegistryServiceSlot.PersistentWorldRegistry },
            { typeof(IFaunaPersistentWorldStateService), GlobalRegistryServiceSlot.PersistentWorldRegistry },
            { typeof(IPersistentDroppedItemRegistry), GlobalRegistryServiceSlot.PersistentWorldRegistry },
            { typeof(PersistentWorldRegistry), GlobalRegistryServiceSlot.PersistentWorldRegistry },
            { typeof(WorldStateManager), GlobalRegistryServiceSlot.WorldStateRuntime },
            { typeof(IPDALogbookService), GlobalRegistryServiceSlot.PDALogbook },
            { typeof(IProfileService), GlobalRegistryServiceSlot.Profile },
            { typeof(ICelestialSkyDirectionReadModel), GlobalRegistryServiceSlot.CelestialEngineRuntime },
            { typeof(ICelestialResonanceReadModel), GlobalRegistryServiceSlot.CelestialEngineRuntime },
            { typeof(HectonCelestialEngine), GlobalRegistryServiceSlot.CelestialEngineRuntime },
            { typeof(IOrbitalDirector), GlobalRegistryServiceSlot.OrbitalDirectorRuntime },
            { typeof(IPrologueSequenceService), GlobalRegistryServiceSlot.PrologueSequenceRuntime },
            { typeof(EclipseGameplaySystem), GlobalRegistryServiceSlot.EclipseGameplayRuntime },
            { typeof(RandomEventSystem), GlobalRegistryServiceSlot.RandomEventRuntime },
            { typeof(IAbyssalFlowGpuReadModel), GlobalRegistryServiceSlot.FluidRuntime },
            { typeof(IFluidAdvectionRenderGraphDispatchSource), GlobalRegistryServiceSlot.FluidRuntime },
            { typeof(IAnalyticalFlowReadModel), GlobalRegistryServiceSlot.FluidRuntime },
            { typeof(IAmbientCurrentReadModel), GlobalRegistryServiceSlot.FluidRuntime },
            { typeof(IFluidSurfaceCurrentReadModel), GlobalRegistryServiceSlot.FluidRuntime },
            { typeof(IFluidBubbleBurstSink), GlobalRegistryServiceSlot.FluidRuntime },
            { typeof(IFluidCurrentWriteSink), GlobalRegistryServiceSlot.FluidRuntime },
            { typeof(IBuoyancyObjectRegistry), GlobalRegistryServiceSlot.FluidRuntime },
            { typeof(HectonFluidEngine), GlobalRegistryServiceSlot.FluidRuntime },
            { typeof(AbyssalThermalManager), GlobalRegistryServiceSlot.ThermodynamicsRuntime },
            { typeof(INarrativeDiscoveryReadModel), GlobalRegistryServiceSlot.NarrativeDirectorRuntime },
            { typeof(HectonNarrativeDirector), GlobalRegistryServiceSlot.NarrativeDirectorRuntime },
            { typeof(CorporateOrderSystem), GlobalRegistryServiceSlot.CorporateOrderRuntime },
            { typeof(QuestManager), GlobalRegistryServiceSlot.QuestRuntime },
            { typeof(CullingManager), GlobalRegistryServiceSlot.CullingRuntime },
            { typeof(LODSystemManager), GlobalRegistryServiceSlot.LODSystemRuntime },
            { typeof(IDynamicResolutionRuntime), GlobalRegistryServiceSlot.DynamicResolutionRuntime },
            { typeof(DynamicResolutionScaler), GlobalRegistryServiceSlot.DynamicResolutionRuntime },
            { typeof(IResolutionScalerService), GlobalRegistryServiceSlot.ResolutionScalerService },
            { typeof(ImpostorSystem), GlobalRegistryServiceSlot.ImpostorRuntime },
            { typeof(IDepthZoneReadModel), GlobalRegistryServiceSlot.DepthZoneRuntime },
            { typeof(DepthZoneDirector), GlobalRegistryServiceSlot.DepthZoneRuntime },
            { typeof(HectonBiolumManager), GlobalRegistryServiceSlot.BiolumManagerRuntime },
            { typeof(HectonBiolumController), GlobalRegistryServiceSlot.BiolumControllerRuntime },
            { typeof(IBabelLocalization), GlobalRegistryServiceSlot.LocalizationRuntime },
            { typeof(ILocalizationTextReadModel), GlobalRegistryServiceSlot.LocalizationRuntime },
            { typeof(ILocalizationTextExpansionReadModel), GlobalRegistryServiceSlot.LocalizationRuntime },
            { typeof(ILocalizationLanguageControl), GlobalRegistryServiceSlot.LocalizationRuntime },
            { typeof(ILocalizationStressPresentationReadModel), GlobalRegistryServiceSlot.LocalizationRuntime },
            { typeof(ILocalizationMadnessPresentationReadModel), GlobalRegistryServiceSlot.LocalizationRuntime },
            { typeof(ILocalizationStressHudRefreshSink), GlobalRegistryServiceSlot.LocalizationRuntime },
            { typeof(IPdaCorrosionPresentationSink), GlobalRegistryServiceSlot.LocalizationRuntime },
            { typeof(ILocalizationTransientOverrideSink), GlobalRegistryServiceSlot.LocalizationRuntime },
            { typeof(LocalizationManager), GlobalRegistryServiceSlot.LocalizationRuntime },
            { typeof(IAudioLogRuntime), GlobalRegistryServiceSlot.AudioLogRuntime },
            { typeof(AudioLogSystem), GlobalRegistryServiceSlot.AudioLogRuntime },
            { typeof(CrashTelemetryBuffer), GlobalRegistryServiceSlot.CrashTelemetryRuntime },
            { typeof(PlayerCriticalProceduralAudioRenderer), GlobalRegistryServiceSlot.PlayerCriticalAudioRuntime },
            { typeof(IVocalWarningSystem), GlobalRegistryServiceSlot.VocalWarningRuntime },
            { typeof(VocalWarningSystem), GlobalRegistryServiceSlot.VocalWarningRuntime },
            { typeof(IAcousticZoneReadModel), GlobalRegistryServiceSlot.AcousticZoneRuntime },
            { typeof(IAcousticZoneMadnessCueSink), GlobalRegistryServiceSlot.AcousticZoneRuntime },
            { typeof(AcousticZoneController), GlobalRegistryServiceSlot.AcousticZoneRuntime },
            { typeof(ISurfaceWeatherReadModel), GlobalRegistryServiceSlot.SurfaceWeatherRuntime },
            { typeof(HectonSurfaceWeatherDirector), GlobalRegistryServiceSlot.SurfaceWeatherRuntime },
            { typeof(IAtlasSignalReadModel), GlobalRegistryServiceSlot.AtlasSignalRuntime },
            { typeof(IAtlasSignalDecodeSink), GlobalRegistryServiceSlot.AtlasSignalRuntime },
            { typeof(AtlasSignalSystem), GlobalRegistryServiceSlot.AtlasSignalRuntime },
            { typeof(IFirstHourReadModel), GlobalRegistryServiceSlot.FirstHourRuntime },
            { typeof(FirstHourDirector), GlobalRegistryServiceSlot.FirstHourRuntime },
            { typeof(IEmergencyRelayRouteReadModel), GlobalRegistryServiceSlot.EmergencyRelayRuntime },
            { typeof(EmergencyServiceRelayDirector), GlobalRegistryServiceSlot.EmergencyRelayRuntime },
            { typeof(IAtmosphereRenderSettingsBridge), GlobalRegistryServiceSlot.AtmosphereRuntime },
            { typeof(IAtmosphereReadModel), GlobalRegistryServiceSlot.AtmosphereRuntime },
            { typeof(HectonAtmosphereManager), GlobalRegistryServiceSlot.AtmosphereRuntime },
            { typeof(ITerrainProvider), GlobalRegistryServiceSlot.TerrainProviderRuntime },
            { typeof(MapMagicBridge), GlobalRegistryServiceSlot.MapMagicRuntime },
            { typeof(IAbyssalFlowVolumeReadModel), GlobalRegistryServiceSlot.MapMagicVegetationRuntime },
            { typeof(ScavengePopulator), GlobalRegistryServiceSlot.ScavengePopulatorRuntime },
            { typeof(ModWorldPersistenceManager), GlobalRegistryServiceSlot.ModWorldPersistenceRuntime },
            { typeof(IModdingBridge), GlobalRegistryServiceSlot.ModdingBridgeRuntime },
            { typeof(RunModifierController), GlobalRegistryServiceSlot.RunModifierRuntime },
            { typeof(IMetaCampaignService), GlobalRegistryServiceSlot.MetaCampaignRuntime },
            { typeof(MigrationDirector), GlobalRegistryServiceSlot.MigrationDirectorRuntime },
            { typeof(BasePollutionManager), GlobalRegistryServiceSlot.BasePollutionRuntime },
            { typeof(EntityChangeManager), GlobalRegistryServiceSlot.EntityChangeManagerRuntime },
            { typeof(PerformanceMonitor), GlobalRegistryServiceSlot.PerformanceMonitorRuntime },
            { typeof(IBeaconNetworkService), GlobalRegistryServiceSlot.BeaconNetworkRuntime },
            { typeof(BeaconNetworkSystem), GlobalRegistryServiceSlot.BeaconNetworkRuntime },
            { typeof(IScanLogService), GlobalRegistryServiceSlot.ScanLogRuntime },
            { typeof(ScanLogSystem), GlobalRegistryServiceSlot.ScanLogRuntime },
            { typeof(IToolDurabilityService), GlobalRegistryServiceSlot.ToolDurabilityRuntime },
            { typeof(ToolDurabilitySystem), GlobalRegistryServiceSlot.ToolDurabilityRuntime },
            { typeof(ToolHapticsRuntime), GlobalRegistryServiceSlot.ToolHapticsRuntime },
            { typeof(IVRSomaticProvider), GlobalRegistryServiceSlot.VRSomaticProvider },
            { typeof(ILoreUnlockReadModel), GlobalRegistryServiceSlot.LoreDatabaseRuntime },
            { typeof(ILoreDatabaseReadModel), GlobalRegistryServiceSlot.LoreDatabaseRuntime },
            { typeof(ILoreUnlockSink), GlobalRegistryServiceSlot.LoreDatabaseRuntime },
            { typeof(LoreDatabaseManager), GlobalRegistryServiceSlot.LoreDatabaseRuntime },
            { typeof(IPlayerExpressionReadModel), GlobalRegistryServiceSlot.PlayerExpressionRuntime },
            { typeof(PlayerExpressionManager), GlobalRegistryServiceSlot.PlayerExpressionRuntime },
            { typeof(IPlayerActionInterruptSink), GlobalRegistryServiceSlot.PlayerActionRuntime },
            { typeof(SpectrumSystem), GlobalRegistryServiceSlot.SpectrumRuntime },
            { typeof(UserOptionsPersistence), GlobalRegistryServiceSlot.UserOptionsRuntime },
            { typeof(IAssetLifecyclePressureSink), GlobalRegistryServiceSlot.AssetLifecycleRuntime },
            { typeof(AssetLifecycleGovernor), GlobalRegistryServiceSlot.AssetLifecycleRuntime },
            { typeof(AssetLoadDispatcher), GlobalRegistryServiceSlot.AssetLoadDispatcherRuntime },
            { typeof(IVramBudgetReadModel), GlobalRegistryServiceSlot.VRAMMonitorRuntime },
            { typeof(IVramBudgetSampleSink), GlobalRegistryServiceSlot.VRAMMonitorRuntime },
            { typeof(VRAMMonitor), GlobalRegistryServiceSlot.VRAMMonitorRuntime },
            { typeof(IVramPressureReadModel), GlobalRegistryServiceSlot.VRAMPressureRuntime },
            { typeof(IVramPressureSampleSink), GlobalRegistryServiceSlot.VRAMPressureRuntime },
            { typeof(IVramPressureMipBiasSink), GlobalRegistryServiceSlot.VRAMPressureRuntime },
            { typeof(VRAMPressureMonitor), GlobalRegistryServiceSlot.VRAMPressureRuntime },
            { typeof(RenderTextureLifecycleTracker), GlobalRegistryServiceSlot.RenderTextureLifecycleRuntime },
            { typeof(RenderTexturePool), GlobalRegistryServiceSlot.RenderTexturePoolRuntime },
            { typeof(IFluidDecalPresentationSink), GlobalRegistryServiceSlot.AbyssalFluidDecalRuntime },
            { typeof(AbyssalFluidDecalManager), GlobalRegistryServiceSlot.AbyssalFluidDecalRuntime },
            { typeof(ISargassumDragReadModel), GlobalRegistryServiceSlot.SargassumDragRuntime },
            { typeof(SargassumGlobalDragManager), GlobalRegistryServiceSlot.SargassumDragRuntime },
            { typeof(ISargassumCutWriteService), GlobalRegistryServiceSlot.SargassumCutRuntime },
            { typeof(SargassumCutManager), GlobalRegistryServiceSlot.SargassumCutRuntime },
            { typeof(IMicroFaunaPresentationPulseSink), GlobalRegistryServiceSlot.SargassumMicroFaunaRuntime },
            { typeof(SargassumMicroFaunaBoids), GlobalRegistryServiceSlot.SargassumMicroFaunaRuntime },
            { typeof(HectonFloatingOrigin), GlobalRegistryServiceSlot.FloatingOriginRuntime },
            { typeof(IConnectionSplineBatchRendererService), GlobalRegistryServiceSlot.ConnectionSplineBatchRendererRuntime },
            { typeof(ConnectionSplineBatchRenderer), GlobalRegistryServiceSlot.ConnectionSplineBatchRendererRuntime },
            { typeof(ISoundscapeTierReadModel), GlobalRegistryServiceSlot.SoundscapeRuntime },
            { typeof(SoundscapeSystem), GlobalRegistryServiceSlot.SoundscapeRuntime },
            { typeof(IEnvironmentalStrainReadModel), GlobalRegistryServiceSlot.EnvironmentalStrainRuntime },
            { typeof(IEnvironmentalStrainIndustrialSink), GlobalRegistryServiceSlot.EnvironmentalStrainRuntime },
            { typeof(EnvironmentalStrainManager), GlobalRegistryServiceSlot.EnvironmentalStrainRuntime },
            { typeof(EcosystemHealthDirector), GlobalRegistryServiceSlot.EcosystemHealthRuntime },
            { typeof(IFaunaWorldSeedReadModel), GlobalRegistryServiceSlot.FaunaGeneticsRuntime },
            { typeof(FaunaGeneticsManager), GlobalRegistryServiceSlot.FaunaGeneticsRuntime },
            { typeof(PlayerExplorationTracker), GlobalRegistryServiceSlot.PlayerExplorationRuntime },
            { typeof(DynamicDifficultyDirector), GlobalRegistryServiceSlot.DynamicDifficultyRuntime },
            { typeof(HectonDiscoveryManager), GlobalRegistryServiceSlot.DiscoveryRuntime },
            { typeof(IResourceScarcityReadModel), GlobalRegistryServiceSlot.ResourceScarcityRuntime },
            { typeof(ResourceScarcityDirector), GlobalRegistryServiceSlot.ResourceScarcityRuntime },
            { typeof(FieldOperationLogSystem), GlobalRegistryServiceSlot.FieldOperationLogRuntime },
            { typeof(PDAExchangeSystem), GlobalRegistryServiceSlot.PDAExchangeRuntime },
            { typeof(PlayerActionController), GlobalRegistryServiceSlot.PlayerActionRuntime },
            { typeof(PDAMarkerRegistry), GlobalRegistryServiceSlot.PDAMarkerRuntime },
            { typeof(PDAIntrusionManager), GlobalRegistryServiceSlot.PDAIntrusionRuntime },
            { typeof(AmbientWaterMotionManager), GlobalRegistryServiceSlot.AmbientWaterMotionRuntime },
            { typeof(SuitUpgradeManager), GlobalRegistryServiceSlot.SuitUpgradeRuntime },
            { typeof(UIAudioFeedback), GlobalRegistryServiceSlot.UIAudioFeedbackRuntime },
            { typeof(UITooltip), GlobalRegistryServiceSlot.UITooltipRuntime },
            { typeof(LoadingScreenController), GlobalRegistryServiceSlot.LoadingScreenRuntime },
            { typeof(IEndingRuntimeService), GlobalRegistryServiceSlot.EndingRuntime },
            { typeof(EndingSystem), GlobalRegistryServiceSlot.EndingRuntime },
            { typeof(IAtlas6DirectiveCommandSink), GlobalRegistryServiceSlot.Atlas6DirectiveRuntime },
            { typeof(Atlas6DirectiveSystem), GlobalRegistryServiceSlot.Atlas6DirectiveRuntime },
            { typeof(IHazardZoneReadModel), GlobalRegistryServiceSlot.HazardZoneRuntime },
            { typeof(HazardZoneManager), GlobalRegistryServiceSlot.HazardZoneRuntime },
            { typeof(MissionManager), GlobalRegistryServiceSlot.MissionRuntime },
            { typeof(HectonRockManager), GlobalRegistryServiceSlot.RockManagerRuntime },
            { typeof(ICameraJuiceSystem), GlobalRegistryServiceSlot.CameraJuiceRuntime },
            { typeof(CameraJuiceSystem), GlobalRegistryServiceSlot.CameraJuiceRuntime },
            { typeof(HectonMusicDirector), GlobalRegistryServiceSlot.MusicDirectorRuntime },
            { typeof(SubtitleManager), GlobalRegistryServiceSlot.SubtitleRuntime },
            { typeof(AtlasSignalDecoder), GlobalRegistryServiceSlot.AtlasSignalDecoderRuntime },
            { typeof(ScrapManager), GlobalRegistryServiceSlot.ScrapRuntime },
            { typeof(AutonomousExtractorSystem), GlobalRegistryServiceSlot.AutonomousExtractorRuntime },
            { typeof(VisorRTManager), GlobalRegistryServiceSlot.VisorRTRuntime },
            { typeof(CameraRTManager), GlobalRegistryServiceSlot.CameraRTRuntime },
            { typeof(PostFXRTManager), GlobalRegistryServiceSlot.PostFXRTRuntime },
            { typeof(UIRTManager), GlobalRegistryServiceSlot.UIRTRuntime },
            { typeof(SettingsManager), GlobalRegistryServiceSlot.SettingsRuntime },
            { typeof(RuntimeWatchdog), GlobalRegistryServiceSlot.RuntimeWatchdogRuntime },
            { typeof(GameTickManager), GlobalRegistryServiceSlot.TickManager },
            { typeof(SystemDispatcher), GlobalRegistryServiceSlot.Dispatcher },
            { typeof(RenderDispatcher), GlobalRegistryServiceSlot.RenderDispatcher },
            { typeof(GlobalPhysicsStateManager), GlobalRegistryServiceSlot.PhysicsStateManager },
            { typeof(IPhysicsStateEventService), GlobalRegistryServiceSlot.PhysicsStateManager },
            { typeof(IPhysicsCullingOverseer), GlobalRegistryServiceSlot.PhysicsStateManager },
            { typeof(IDataVault), GlobalRegistryServiceSlot.DataVault },
            { typeof(ICablePhysics132Service), GlobalRegistryServiceSlot.CablePhysics132Runtime },
            { typeof(GlobalDataVault), GlobalRegistryServiceSlot.DataVault },
            { typeof(IMacroDatabaseService), GlobalRegistryServiceSlot.MacroDatabase },
            { typeof(ICausticsService), GlobalRegistryServiceSlot.CausticsRuntime },
            { typeof(IJobAdmissionService), GlobalRegistryServiceSlot.JobAdmissionRuntime },
            { typeof(ISimulationBucketer), GlobalRegistryServiceSlot.SimulationBucketerRuntime },
            { typeof(IStreamingBackpressureService), GlobalRegistryServiceSlot.StreamingBackpressureRuntime },
            { typeof(IFoveatedSimulationDirector), GlobalRegistryServiceSlot.FoveatedSimulationDirector },
            { typeof(IHardwareThermalService), GlobalRegistryServiceSlot.HardwareThermalService },
            { typeof(IGroundRadarService), GlobalRegistryServiceSlot.GroundRadarRuntime },
            { typeof(IWorldResourceSpawnerReadModel), GlobalRegistryServiceSlot.WorldResourceSpawnerRuntime },
            { typeof(IInstanceCullingService), GlobalRegistryServiceSlot.InstanceCullingRuntime },
            { typeof(IOutpostGenerationService), GlobalRegistryServiceSlot.OutpostGenerationRuntime },
        };

        [Preserve]
        private static GlobalRegistryServiceSlot ResolveServiceSlotCold(Type serviceType)
        {
            if (_serviceSlotMap.TryGetValue(serviceType, out var slot))
            {
                return slot;
            }
            return GlobalRegistryServiceSlot.Unknown;
        }

        [Preserve]
        private static class ServiceSlotCache<T> where T : class
        {
            internal static readonly GlobalRegistryServiceSlot Slot = ResolveServiceSlotCold(typeof(T));
        }

        private sealed class NoOpInputService : IInputService
        {
            public bool IsInitialized => false;
            public bool IsPlayerInputEnabled => false;
            public int InputDelayFrames { get => 0; set { } }
            public InputState CurrentInputState => default;
            public InputState PreviousInputState => default;
            public Vector2 VisualLookDelta => Vector2.zero;

            public event Action OnInteract { add { } remove { } }
            public event Action OnToolSlot1 { add { } remove { } }
            public event Action OnToolSlot2 { add { } remove { } }
            public event Action OnToolSlot3 { add { } remove { } }
            public event Action OnToolSlot4 { add { } remove { } }
            public event Action OnPrimaryAction { add { } remove { } }
            public event Action OnSecondaryAction { add { } remove { } }
            public event Action OnPDA { add { } remove { } }
            public event Action OnInventory { add { } remove { } }
            public event Action OnCancel { add { } remove { } }
            public event Action OnTabNext { add { } remove { } }
            public event Action OnTabPrevious { add { } remove { } }

            public PlayerInputState GetState()
            {
                return default;
            }

            public void PreSimulationInputTick(float deltaTime)
            {
            }

            public bool TryGetInputState(uint frame, out InputState state)
            {
                state = default;
                return false;
            }

            public void BufferAction(PlayerBufferedAction action)
            {
            }

            public bool TryConsumeBufferedAction(PlayerBufferedAction action, float maxAgeSeconds)
            {
                return false;
            }

            public bool CheckBufferedInput(uint buttonBit, int frames)
            {
                return false;
            }

            public uint GetInputBlockMask()
            {
                return 0u;
            }

            public void SetInputBlockMask(uint mask)
            {
            }

            public void SwitchToPlayerInput()
            {
            }

            public void SwitchToUIInput()
            {
            }
        }
    
        #region JulesLink_DecompressionNitrogenLoadCalculator
        private static void JulesLink_DecompressionNitrogenLoadCalculator() { _ = typeof(Hecton8.PureLogic.Systems.DecompressionNitrogenLoadCalculator); }
        #endregion

        #region JulesLink_BloomTriggerThresholdCalculator
        private static void JulesLink_BloomTriggerThresholdCalculator() { _ = typeof(Hecton8.PureLogic.Ecosystem.BloomTriggerThresholdCalculator); }
        #endregion
}
}
