using System;
using System.Runtime.InteropServices;
using Hecton8.AI;
using Hecton8.Atmosphere;
using Hecton8.Celestial;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.PDA;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Ecosystem
{
    /// <summary>
    /// Slow-tick owner for deterministic daily fauna migration pressure, global swarm flow, and kill-site scavenger bias.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6225)]
    [AddComponentMenu("Hecton8/Ecosystem/Migration Director")]
    public sealed class MigrationDirector : MonoBehaviour, ISlowTickable, ILateFrameTickable, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private const float DefaultMigrationDistanceMeters = 320f;
        private const float DefaultColdTickIntervalSeconds = 5f;
        private const float GlobalMigrationCellSizeMeters = 100f;
        private const float BloodCloudPoiLifetimeGameSeconds = 7200f;
        private const float MigrationSwarmStateLifetimeGameSeconds = 30f;
        private const float MigrationSwarmStateFutureToleranceGameSeconds = 2f;
        private const float VrSwarmPopulationScale = 0.6f;
        private const float Tau = 6.28318530718f;
        private const int BloodCloudPoiCapacity = 8;
        private const int MigrationSwarmCapacity = 128;
        private const int DefaultGridResolutionX = 64;
        private const int DefaultGridResolutionY = 16;
        private const int DefaultGridResolutionZ = 64;
        private const int MinimumGridResolutionXZ = 8;
        private const int MinimumGridResolutionY = 2;
        private const int MaximumGridResolutionXZ = 128;
        private const int MaximumGridResolutionY = 32;
        private const ushort MigrationCellFlagBloodCloud = 1 << 0;
        private const ushort MigrationCellFlagPopulation = 1 << 1;
        private const ushort MigrationCellFlagPopulationSaturated = 1 << 2;
        private const ushort MigrationCellFlagNoPopulation = unchecked((ushort)~MigrationCellFlagPopulation);
        private const ushort MigrationCellFlagNoPopulationSaturated = unchecked((ushort)~MigrationCellFlagPopulationSaturated);
        private const SystemID NativeMemorySystemId = SystemID.AIEcology;
        private const float AuthoritativeQualityWeight = 1f;
        private const float MigrationFallbackTimelineMaxSeconds = 16777215f;

        private bool _registeredToTick;
        private bool _registeredLateFrameTick;
        private int _currentDayIndex = 1;
        private int3 _migrationGridResolution;
        private int _migrationGridCellCount;
        private float _coldTickAccumulator;
        private float _fallbackTimelineGameSeconds;
        private float _lastColdTickRuntimeSeconds = -1f;
        private float _debugLastSeasonalPhase;
        private float _debugLastMigrationGridMagnitude;
        private int _debugBloodCloudPoiCount;
        private int _debugMigrationGridCellCount;
        private int _debugStatisticalSwarmSlotCount;
        private JobHandle _migrationFieldHandle;
        private bool _migrationFieldScheduled;
        private int _pendingBloodCloudPoiWriteCount;
        private bool _serviceRegistered;
        private bool _duplicateServiceSuppressed;
        private bool _migrationFieldBuffersLocked;
        private IDataVault _migrationVault;
        private IFaunaWorldSeedReadModel _faunaWorldSeedReadModel;
        private IAmbientCurrentReadModel _ambientCurrentReadModel;
        private HectonCelestialEngine _celestialEngine;
        private HectonMapMagicVegetationBridge _mapMagicVegetationBridge;
        private BufferID _migrationFieldWriteBufferId;
        private BufferID _migrationFieldPoiBufferId;
        private ulong _migrationFieldGuardMask;
        private IDataVault _migrationFieldGuardVault;
        private VaultGenerationHandle<MigrationGridCell> _migrationGridFrontHandle;
        private VaultGenerationHandle<MigrationGridCell> _migrationGridBackHandle;
        private VaultGenerationHandle<MigrationBloodCloudPoi> _bloodCloudPoisHandle;
        private VaultGenerationHandle<MigrationSwarmState> _migrationSwarmStatesHandle;
        private static MigrationDirector s_activeRuntime;

        private readonly MigrationBloodCloudPoi[] _bloodCloudPoiMirror = new MigrationBloodCloudPoi[BloodCloudPoiCapacity]; // COLD ALLOC: MigrationBloodCloudPoi[8] - main-thread POI mirror; NativeArray is job-owned during scheduled rebuilds - owner: MigrationDirector
        private readonly MigrationBloodCloudPoi[] _pendingBloodCloudPoiWrites = new MigrationBloodCloudPoi[BloodCloudPoiCapacity]; // COLD ALLOC: MigrationBloodCloudPoi[8] - deferred kill-site writes while migration job owns NativeArray - owner: MigrationDirector

        [Header("Temperature Migration")]
        [Tooltip("Optional authored temperature bands used to steer migration routes and herbivore relocation targets.")]
        [SerializeField] private EcosystemMigrationProfile migrationProfile;
        [Tooltip("Fallback route distance used when no authored temperature band matches the sampled water.")]
        [SerializeField, Min(1f)] private float fallbackMigrationDistanceMeters = DefaultMigrationDistanceMeters;
        [Tooltip("How strongly local water current bends the fallback route heading.")]
        [SerializeField, Range(0f, 1f)] private float fallbackCurrentAlignmentWeight = 0.55f;

        [Header("Global Migration Flow")]
        [Tooltip("AUP-local origin of the coarse 3D migration field. X/Z wrap; Y clamps.")]
        [SerializeField] private Vector3 migrationGridOriginAupLocal = new Vector3(-3200f, -1200f, -3200f);
        [Tooltip("Low-res migration vector cell size in meters. Mandate target is 100m.")]
        [SerializeField, Min(25f)] private float migrationCellSizeMeters = GlobalMigrationCellSizeMeters;
        [Tooltip("Coarse wrapped migration grid resolution on X.")]
        [SerializeField, Range(MinimumGridResolutionXZ, MaximumGridResolutionXZ)] private int migrationGridResolutionX = DefaultGridResolutionX;
        [Tooltip("Coarse migration grid resolution on Y.")]
        [SerializeField, Range(MinimumGridResolutionY, MaximumGridResolutionY)] private int migrationGridResolutionY = DefaultGridResolutionY;
        [Tooltip("Coarse wrapped migration grid resolution on Z.")]
        [SerializeField, Range(MinimumGridResolutionXZ, MaximumGridResolutionXZ)] private int migrationGridResolutionZ = DefaultGridResolutionZ;
        [Tooltip("Cold-tick interval for Burst migration field rebuilds.")]
        [SerializeField, Min(1f)] private float migrationFieldColdTickIntervalSeconds = DefaultColdTickIntervalSeconds;
        [Tooltip("Blend weight applied to the global migration grid when selecting migration targets.")]
        [SerializeField, Range(0f, 1f)] private float migrationFlowAlignmentWeight = 0.72f;
        [Tooltip("Radians of seasonal migration field rotation per game-time second.")]
        [SerializeField, Min(0f)] private float seasonalRadiansPerGameSecond = 0.00019f;
        [Tooltip("Vertical swim drift injected into the coarse migration field.")]
        [SerializeField, Range(0f, 0.35f)] private float migrationVerticalFlowWeight = 0.08f;

        [Header("Blood Cloud POIs")]
        [Tooltip("Apex kill POI radius used to bend the migration vector field toward the kill site.")]
        [SerializeField, Min(10f)] private float bloodCloudPoiRadiusMeters = 520f;
        [Tooltip("Attraction strength applied by kill-site blood clouds to local migration vectors.")]
        [SerializeField, Range(0f, 4f)] private float bloodCloudPoiStrength = 2.35f;
        [Tooltip("Population multiplier applied to scavenger swarms near whale-fall POIs for two game-time hours.")]
        [SerializeField, Range(1f, 50f)] private float whaleFallScavengerPopulationMultiplier = 50f;
        [Tooltip("Optional species ids eligible for whale-fall density boost. Empty means every migration swarm species can scavenge.")]
        [SerializeField] private int[] scavengerMigrationSpeciesIds;

        [Header("PC/VR Swarm Scaling")]
        [Tooltip("VAT sway amplitude multiplier used when VR swarm count is reduced.")]
        [SerializeField, Range(1f, 2f)] private float vrVatSwayAmplitudeScale = 1.4f;

        [Header("Diagnostics")]
        [SerializeField] private float _debugLastMigrationTemperatureCelsius = 15f;
        [SerializeField] private Vector3 _debugLastMigrationDirection = Vector3.forward;
        [SerializeField] private Vector3 _debugLastMigrationGridDirection = Vector3.forward;

        /// <summary>
        /// Coarse global migration cell. X/Z indices wrap in AUP-space; Y is clamped to the shelf water column.
        /// </summary>
        [StructLayout(LayoutKind.Explicit, Size = 32)]
        public struct MigrationGridCell
        {
            [FieldOffset(0)]
            public int3 AupCell;
            [FieldOffset(12)]
            public float3 Direction;
            [FieldOffset(24)]
            public float Magnitude;
            [FieldOffset(28)]
            public ushort PopulationCount;
            [FieldOffset(30)]
            public ushort Flags;
        }

        [StructLayout(LayoutKind.Explicit, Size = 80)]
        private struct MigrationBloodCloudPoi
        {
            [FieldOffset(0)]
            public AbsoluteUniversePositionBlit128 PositionAup;
            [FieldOffset(48)]
            public float3 PositionFieldLocalMeters;
            [FieldOffset(60)]
            public float RadiusMeters;
            [FieldOffset(64)]
            public float Strength;
            [FieldOffset(68)]
            public float ExpireGameTimeSeconds;
            [FieldOffset(72)]
            public int SourceId;
            [FieldOffset(76)]
            public int Flags;
        }

        [StructLayout(LayoutKind.Explicit, Size = 40)]
        private struct MigrationSwarmState
        {
            [FieldOffset(0)]
            public int3 AupCell;
            [FieldOffset(12)]
            public float3 LocalPosition;
            [FieldOffset(24)]
            public float RadiusMeters;
            [FieldOffset(28)]
            public float LastWriteGameTimeSeconds;
            [FieldOffset(32)]
            public uint Flags;
            [FieldOffset(36)]
            public ushort PopulationCount;
            [FieldOffset(38)]
            public ushort SpeciesId;
        }

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState
        {
            get
            {
                if (!_serviceRegistered)
                    return ServiceHeartbeatState.NotStarted;

                return IsMigrationNativeStateAllocated ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.Booting;
            }
        }

        /// <inheritdoc />
        public bool IsServiceReady => _serviceRegistered && IsMigrationNativeStateAllocated;

        private bool IsMigrationNativeStateAllocated =>
            TryResolveMigrationNativeViews(out _, out _, out _, out _);

        private void Awake()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            SanitizeMigrationSettings();
            AllocateMigrationNativeState();
            RefreshCurrentDay();
        }

        private void OnEnable()
        {
            if (_duplicateServiceSuppressed)
                return;

            if (TryAbortForUsableExistingRuntime())
                return;

            TryRegisterService();
            if (Application.isPlaying && (!_serviceRegistered || _duplicateServiceSuppressed))
                return;

            if (Application.isPlaying)
                GlobalRegistry.TryRegisterHotSwapListener(this);

            TryRegisterToTickManager();
            TryRegisterLateFrameTickManager();
            AllocateMigrationNativeState();
            RefreshCurrentDay();
        }

        private void Start()
        {
            if (_duplicateServiceSuppressed)
                return;

            if (TryAbortForUsableExistingRuntime())
                return;

            if (Application.isPlaying && !_serviceRegistered)
                return;

            TryRegisterToTickManager();
            TryRegisterLateFrameTickManager();
            AllocateMigrationNativeState();
            RefreshCurrentDay();
        }

        private void OnDisable()
        {
            OnServiceShutdown();
        }

        private void OnDestroy()
        {
            OnServiceShutdown();
        }

        /// <inheritdoc />
        public void OnServiceShutdown()
        {
            UnregisterFromTickManager();
            UnregisterLateFrameTickManager();
            GlobalRegistry.TryUnregisterHotSwapListener(this);
            DisposeMigrationNativeState();
            TryUnregisterService();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.FaunaGeneticsRuntime)
            {
                _faunaWorldSeedReadModel = currentService as IFaunaWorldSeedReadModel;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.FluidRuntime)
            {
                _ambientCurrentReadModel = currentService as IAmbientCurrentReadModel;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.CelestialEngineRuntime)
            {
                _celestialEngine = currentService as HectonCelestialEngine;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.MapMagicVegetationRuntime)
            {
                _mapMagicVegetationBridge = currentService as HectonMapMagicVegetationBridge;
                WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref _mapMagicVegetationBridge);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                DisposeMigrationNativeState();
                _migrationVault = currentService as IDataVault;
                if (_migrationVault != null && isActiveAndEnabled && !_duplicateServiceSuppressed)
                    AllocateMigrationNativeState();
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;
            UnregisterFromTickManager();
            UnregisterLateFrameTickManager();
            if (currentService == null || !isActiveAndEnabled)
                return;
            if (_duplicateServiceSuppressed || (Application.isPlaying && !_serviceRegistered))
                return;

            TryRegisterToTickManager();
            TryRegisterLateFrameTickManager();
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            RefreshCurrentDay();
            AdvanceMigrationFieldColdTick();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            CompleteMigrationFieldJob(forceComplete: false);
        }

        /// <summary>
        /// Resolves the current daily selection multiplier for one biome/archetype pair.
        /// </summary>
        public static float ResolveSelectionMultiplier(int biomeIndex, CreatureArchetypeData archetype)
        {
            MigrationDirector runtime = ResolveActiveRuntime();
            return runtime != null ? runtime.ResolveSelectionMultiplierInternal(biomeIndex, archetype) : 1f;
        }

        /// <summary>
        /// Resolves visible boid count from the O(1) swarm population state and platform-specific scaling.
        /// </summary>
        public static int ResolveVisibleBoidCount(int speciesId, Vector3 origin, int requestedPopulationCount)
        {
            MigrationDirector runtime = ResolveActiveRuntime();
            return runtime != null
                ? runtime.ResolveVisibleBoidCountInternal(speciesId, origin, requestedPopulationCount)
                : ResolveVisibleBoidCountFromMigrationPopulationStatic(requestedPopulationCount);
        }

        /// <summary>
        /// Refreshes an already-dematerialized statistical swarm population point without materialising boids.
        /// </summary>
        public static void RegisterStatisticalSwarmPopulation(int speciesId, Vector3 origin, int populationCount)
        {
            MigrationDirector runtime = ResolveActiveRuntime();
            if (runtime != null)
                runtime.RegisterStatisticalSwarmPopulationInternal(speciesId, origin, populationCount);
        }

        /// <summary>
        /// Resolves VAT sway amplitude compensation for VR swarm downscaling.
        /// </summary>
        public static float ResolveVatSwayAmplitudeScale()
        {
            MigrationDirector runtime = ResolveActiveRuntime();
            return runtime != null ? runtime.ResolveVatSwayAmplitudeScaleInternal() : 1f;
        }

        /// <summary>
        /// Registers one apex kill as a blood-cloud migration POI and one-hour whale-fall density source.
        /// </summary>
        public static void RegisterPredatorKillPoi(uint uniqueInstanceUid, Vector3 worldPosition, float fallbackRuntimeSeconds)
        {
            MigrationDirector runtime = ResolveActiveRuntime();
            if (runtime != null)
                runtime.RegisterPredatorKillPoiInternal(uniqueInstanceUid, worldPosition, fallbackRuntimeSeconds);
        }

        internal static bool TryResolveMigrationTarget(int speciesId, Vector3 origin, out Vector3 target)
        {
            target = origin;
            MigrationDirector runtime = ResolveActiveRuntime();
            return runtime != null && runtime.TryBuildMigrationTargetInternal(speciesId, origin, out target);
        }

        internal static int RegisterStatisticalSwarmPopulationAndResolveCount(int speciesId, Vector3 origin, int populationCount)
        {
            MigrationDirector runtime = ResolveActiveRuntime();
            return runtime != null
                ? runtime.RegisterStatisticalSwarmPopulationInternal(speciesId, origin, populationCount)
                : Mathf.Max(0, populationCount);
        }

        internal static int ResolveVisibleBoidCountFromMigrationPopulation(int migrationPopulationCount)
        {
            MigrationDirector runtime = ResolveActiveRuntime();
            return runtime != null
                ? runtime.ResolveVisibleBoidCountFromMigrationPopulationInternal(migrationPopulationCount)
                : ResolveVisibleBoidCountFromMigrationPopulationStatic(migrationPopulationCount);
        }

        internal static int3 ResolveMigrationPopulationAupCell(Vector3 origin)
        {
            MigrationDirector runtime = ResolveActiveRuntime();
            return runtime != null
                ? runtime.ResolveMigrationAupCell(origin)
                : ResolveMigrationAupCell(origin, GlobalMigrationCellSizeMeters);
        }

        private float ResolveSelectionMultiplierInternal(int biomeIndex, CreatureArchetypeData archetype)
        {
            if (biomeIndex < 0 || archetype == null)
                return 1f;

            if (archetype.roleType != CreatureRoleType.Ambient &&
                archetype.roleType != CreatureRoleType.Territorial)
            {
                return 1f;
            }

            IFaunaWorldSeedReadModel geneticsManager = _faunaWorldSeedReadModel;
            int worldSeed = geneticsManager != null ? geneticsManager.WorldSeed : 0;
            uint speciesHash = HashString(AsSpanOrEmpty(archetype.creatureId));
            uint hash = Hash((uint)worldSeed ^ (uint)_currentDayIndex * 0x9E3779B9u ^ (uint)biomeIndex * 0x85EBCA6Bu ^ speciesHash);
            if ((hash & 0x3u) != 0u)
                return 1f;

            bool abundanceWave = (hash & 0x10u) != 0u;
            float waveStrength = Hash01(hash ^ 0x7F4A7C15u);
            float dailyWave = abundanceWave
                ? math.lerp(1.12f, 1.6f, waveStrength)
                : math.lerp(0.58f, 0.92f, waveStrength);
            float currentBias = ResolveCurrentDrivenBias(biomeIndex, archetype, hash);
            return dailyWave * currentBias;
        }

        private void RefreshCurrentDay()
        {
            int dayIndex;
            float dayTimeHours;
            float playTimeSeconds;
            PDAClockUtility.CaptureStamp(out dayIndex, out dayTimeHours, out playTimeSeconds);
            _currentDayIndex = Mathf.Max(1, dayIndex);
        }

        private Vector3 SampleAmbientCurrent(Vector3 samplePosition)
        {
            IAmbientCurrentReadModel ambientCurrent = _ambientCurrentReadModel;
            if (ambientCurrent != null &&
                ambientCurrent.TrySampleCombinedCurrent(samplePosition, out Vector3 currentVector))
            {
                return currentVector;
            }

            return Vector3.zero;
        }

        private float ResolveCurrentDrivenBias(int biomeIndex, CreatureArchetypeData archetype, uint hash)
        {
            Vector3 probePosition = ResolveMigrationProbePosition(biomeIndex, hash);
            Vector3 currentVector = SampleAmbientCurrent(probePosition);
            currentVector.y = 0f;
            float sqrMagnitude = currentVector.sqrMagnitude;
            if (sqrMagnitude <= 0.0001f)
                return 1f;

            currentVector *= math.rsqrt(sqrMagnitude);

            float preferredHeadingRadians = Hash01(hash ^ 0xB5297A4Du) * Tau;
            MathLodApproximation.ApproxSinCosBhaskara(preferredHeadingRadians, out float preferredSin, out float preferredCos);
            Vector3 preferredHeading = new Vector3(
                preferredCos,
                0f,
                preferredSin);
            float alignment01 = math.saturate((Vector3.Dot(currentVector, preferredHeading) + 1f) * 0.5f);

            float roleBlend = archetype.roleType == CreatureRoleType.Ambient ? 0.82f : 0.64f;
            float weightedAlignment = math.lerp(0.5f, alignment01, roleBlend);
            return math.lerp(0.7f, 1.45f, weightedAlignment);
        }

        private bool TryBuildMigrationTargetInternal(int speciesId, Vector3 origin, out Vector3 target)
        {
            target = origin;
            if (!IsFiniteVector3(origin))
            {
                target = default;
                return false;
            }

            float sampledTemperature = ResolveWaterTemperature(origin);
            if (!math.isfinite(sampledTemperature))
                sampledTemperature = 15f;

            Vector3 currentVector = SampleAmbientCurrent(origin);
            if (!IsFiniteVector3(currentVector))
                currentVector = Vector3.zero;

            currentVector.y = 0f;

            EcosystemMigrationProfile.TemperatureRoute route = default;
            bool hasRoute = migrationProfile != null && migrationProfile.TryResolveRoute(sampledTemperature, out route);
            float routeDistance = SanitizePositiveFinite(hasRoute ? route.migrationDistanceMeters : fallbackMigrationDistanceMeters, 1f);
            float currentAlignmentWeight = Sanitize01Finite(hasRoute ? route.currentAlignmentWeight : fallbackCurrentAlignmentWeight);
            float depthBiasMeters = SanitizeFinite(hasRoute ? route.depthBiasMeters : 0f, 0f);

            int3 originAupCell = ResolveMigrationAupCell(origin);
            uint seed = Hash((uint)speciesId ^ (uint)_currentDayIndex * 0x9E3779B9u ^ HashInt3(originAupCell));
            Vector3 preferredDirection = hasRoute
                ? ResolvePreferredDirection(route.preferredPlanarDirection, seed)
                : ResolvePreferredDirection(Vector2.zero, seed);
            Vector3 migrationDirection = BlendRouteWithCurrent(preferredDirection, currentVector, currentAlignmentWeight);
            if (!IsFiniteVector3(migrationDirection))
                migrationDirection = preferredDirection;

            if (TrySampleMigrationFieldDirection(origin, out Vector3 gridDirection) && IsFiniteVector3(gridDirection))
            {
                migrationDirection = BlendDirectionsLinear(migrationDirection, gridDirection, migrationFlowAlignmentWeight);
                if (!IsFiniteVector3(migrationDirection))
                    migrationDirection = preferredDirection;

                _debugLastMigrationGridDirection = gridDirection;
                _debugLastMigrationGridMagnitude = Mathf.Max(Mathf.Abs(gridDirection.x), Mathf.Max(Mathf.Abs(gridDirection.y), Mathf.Abs(gridDirection.z)));
            }

            _debugLastMigrationTemperatureCelsius = sampledTemperature;
            _debugLastMigrationDirection = migrationDirection;

            target = BuildMigrationTargetRuntime(origin, migrationDirection, routeDistance, depthBiasMeters);
            return true;
        }

        private int ResolveVisibleBoidCountInternal(int speciesId, Vector3 origin, int requestedPopulationCount)
        {
            int migrationPopulationCount = ResolveEcologicalMigrationPopulationCountInternal(speciesId, origin, requestedPopulationCount);
            return ResolveVisibleBoidCountFromMigrationPopulationInternal(migrationPopulationCount);
        }

        private int RegisterStatisticalSwarmPopulationInternal(int speciesId, Vector3 origin, int populationCount)
        {
            if (populationCount <= 0)
            {
                if (TryResolveMigrationSwarmStates(out _))
                    WriteSwarmPopulationState(speciesId, origin, 0);

                return 0;
            }

            AllocateMigrationNativeState();
            int migrationPopulationCount = ResolveEcologicalMigrationPopulationCountInternal(speciesId, origin, populationCount);
            WriteSwarmPopulationState(speciesId, origin, migrationPopulationCount);
            return migrationPopulationCount;
        }

        private int ResolveEcologicalMigrationPopulationCountInternal(int speciesId, Vector3 origin, int requestedPopulationCount)
        {
            int safePopulationCount = Mathf.Max(0, requestedPopulationCount);
            if (safePopulationCount <= 0)
                return 0;

            float populationMultiplier = ResolveWhaleFallPopulationMultiplier(speciesId, origin, ResolveTimelineGameSeconds(0f));
            return RoundPositiveToIntSaturated(safePopulationCount * populationMultiplier);
        }

        private int ResolveVisibleBoidCountFromMigrationPopulationInternal(int migrationPopulationCount)
        {
            bool vrActive = IsVrSwarmScalingActive();
            return ResolveVisibleBoidCountFromMigrationPopulationStatic(migrationPopulationCount, vrActive);
        }

        private static int ResolveVisibleBoidCountFromMigrationPopulationStatic(int migrationPopulationCount)
        {
            return ResolveVisibleBoidCountFromMigrationPopulationStatic(migrationPopulationCount, IsVrSwarmScalingActive());
        }

        private static int ResolveVisibleBoidCountFromMigrationPopulationStatic(int migrationPopulationCount, bool vrActive)
        {
            int safePopulationCount = Mathf.Max(0, migrationPopulationCount);
            if (safePopulationCount <= 0 || !vrActive)
                return safePopulationCount;

            return RoundPositiveToIntSaturated(safePopulationCount * VrSwarmPopulationScale);
        }

        private static int RoundPositiveToIntSaturated(float value)
        {
            if (!math.isfinite(value) || value <= 0f)
                return 0;

            return value >= int.MaxValue ? int.MaxValue : (int)(value + 0.5f);
        }

        private float ResolveVatSwayAmplitudeScaleInternal()
        {
            bool vrActive = IsVrSwarmScalingActive();
            return vrActive ? SanitizePositiveFinite(vrVatSwayAmplitudeScale, 1f) : 1f;
        }

        private void RegisterPredatorKillPoiInternal(uint uniqueInstanceUid, Vector3 worldPosition, float fallbackRuntimeSeconds)
        {
            AllocateMigrationNativeState();
            if (!TryResolveBloodCloudPois(out NativeArray<MigrationBloodCloudPoi> bloodCloudPois) ||
                !bloodCloudPois.IsCreated ||
                bloodCloudPois.Length == 0)
            {
                return;
            }

            float gameTimeSeconds = ResolveTimelineGameSeconds(fallbackRuntimeSeconds);
            PruneExpiredMigrationSwarmStates(gameTimeSeconds);
            if (!TryBuildBloodCloudPoi(uniqueInstanceUid, worldPosition, gameTimeSeconds, out MigrationBloodCloudPoi poi))
                return;

            if (_migrationFieldScheduled)
            {
                EnqueuePendingBloodCloudPoiWrite(in poi, gameTimeSeconds);
                _debugBloodCloudPoiCount = CountActiveBloodCloudPois(gameTimeSeconds);
                return;
            }

            WriteBloodCloudPoiToNative(in poi, gameTimeSeconds, bloodCloudPois);
            RequestMigrationFieldRebuildSoon();
        }

        private bool TryBuildBloodCloudPoi(uint uniqueInstanceUid, Vector3 worldPosition, float gameTimeSeconds, out MigrationBloodCloudPoi poi)
        {
            poi = default;
            if (!math.isfinite(gameTimeSeconds) || gameTimeSeconds < 0f)
                return false;

            if (!TryResolveRuntimeAup(worldPosition, out AbsoluteUniversePosition positionAup))
                return false;

            float3 fieldLocalMeters = ResolveWrappedMigrationFieldPoint(worldPosition);
            if (!math.all(math.isfinite(fieldLocalMeters)))
                return false;

            float radiusMeters = math.max(10f, SanitizeFinite(bloodCloudPoiRadiusMeters, 10f));
            float strength = math.max(0f, SanitizeFinite(bloodCloudPoiStrength, 0f));
            float expireGameTimeSeconds = gameTimeSeconds + BloodCloudPoiLifetimeGameSeconds;
            if (!math.isfinite(radiusMeters) || !math.isfinite(strength) || !math.isfinite(expireGameTimeSeconds))
                return false;

            poi.PositionAup = positionAup.ToAlignedBlit();
            poi.PositionFieldLocalMeters = fieldLocalMeters;
            poi.RadiusMeters = radiusMeters;
            poi.Strength = strength;
            poi.ExpireGameTimeSeconds = expireGameTimeSeconds;
            poi.SourceId = unchecked((int)(uniqueInstanceUid & 0x7FFFFFFFu));
            poi.Flags = 1;
            return true;
        }

        private void WriteBloodCloudPoiToNative(
            in MigrationBloodCloudPoi poi,
            float gameTimeSeconds,
            NativeArray<MigrationBloodCloudPoi> bloodCloudPois)
        {
            if (!bloodCloudPois.IsCreated || bloodCloudPois.Length == 0)
                return;

            int sourceId = poi.SourceId;
            int capacity = math.min(bloodCloudPois.Length, _bloodCloudPoiMirror.Length);
            int selectedSlot = -1;
            int earliestSlot = 0;
            float earliestExpiry = float.MaxValue;
            for (int i = 0; i < capacity; i++)
            {
                MigrationBloodCloudPoi candidate = _bloodCloudPoiMirror[i];
                bool candidateActive = IsActiveBloodCloudPoi(in candidate, gameTimeSeconds);
                if (candidateActive && candidate.SourceId == sourceId)
                {
                    selectedSlot = i;
                    break;
                }

                if (!candidateActive)
                {
                    if (selectedSlot < 0)
                        selectedSlot = i;

                    continue;
                }

                if (candidate.ExpireGameTimeSeconds < earliestExpiry)
                {
                    earliestExpiry = candidate.ExpireGameTimeSeconds;
                    earliestSlot = i;
                }
            }

            if (selectedSlot < 0)
                selectedSlot = earliestSlot;

            bloodCloudPois[selectedSlot] = poi;
            _bloodCloudPoiMirror[selectedSlot] = poi;
            _debugBloodCloudPoiCount = CountActiveBloodCloudPois(gameTimeSeconds);
        }

        private void EnqueuePendingBloodCloudPoiWrite(in MigrationBloodCloudPoi poi, float gameTimeSeconds)
        {
            CompactPendingBloodCloudPoiWrites(gameTimeSeconds);
            int selectedSlot = -1;
            int replaceSlot = 0;
            float earliestExpiry = float.MaxValue;
            for (int i = 0; i < _pendingBloodCloudPoiWriteCount; i++)
            {
                MigrationBloodCloudPoi candidate = _pendingBloodCloudPoiWrites[i];
                bool candidateActive = IsActiveBloodCloudPoi(in candidate, gameTimeSeconds);
                if (candidateActive && candidate.SourceId == poi.SourceId)
                {
                    selectedSlot = i;
                    break;
                }

                if (!candidateActive)
                {
                    selectedSlot = i;
                    break;
                }

                if (candidate.ExpireGameTimeSeconds < earliestExpiry)
                {
                    earliestExpiry = candidate.ExpireGameTimeSeconds;
                    replaceSlot = i;
                }
            }

            if (selectedSlot < 0)
            {
                if (_pendingBloodCloudPoiWriteCount < _pendingBloodCloudPoiWrites.Length)
                    selectedSlot = _pendingBloodCloudPoiWriteCount++;
                else
                    selectedSlot = replaceSlot;
            }

            _pendingBloodCloudPoiWrites[selectedSlot] = poi;
        }

        private bool FlushPendingBloodCloudPoiWrites(float gameTimeSeconds)
        {
            CompactPendingBloodCloudPoiWrites(gameTimeSeconds);
            if (_migrationFieldScheduled || _pendingBloodCloudPoiWriteCount <= 0)
                return false;

            if (!TryResolveBloodCloudPois(out NativeArray<MigrationBloodCloudPoi> bloodCloudPois))
                return false;

            bool wroteAny = false;
            for (int i = 0; i < _pendingBloodCloudPoiWriteCount; i++)
            {
                MigrationBloodCloudPoi poi = _pendingBloodCloudPoiWrites[i];
                if (IsActiveBloodCloudPoi(in poi, gameTimeSeconds))
                {
                    WriteBloodCloudPoiToNative(in poi, gameTimeSeconds, bloodCloudPois);
                    wroteAny = true;
                }

                _pendingBloodCloudPoiWrites[i] = default;
            }

            _pendingBloodCloudPoiWriteCount = 0;
            return wroteAny;
        }

        private void RequestMigrationFieldRebuildSoon()
        {
            float currentAccumulator = math.max(0f, SanitizeFinite(_coldTickAccumulator, 0f));
            float rebuildInterval = ResolveMigrationFieldColdTickIntervalSeconds(ResolveMigrationQualityWeight());
            _coldTickAccumulator = math.max(currentAccumulator, SanitizeFinite(rebuildInterval, 1f));
        }

        private void CompactPendingBloodCloudPoiWrites(float gameTimeSeconds)
        {
            if (_pendingBloodCloudPoiWriteCount <= 0)
                return;

            int writeIndex = 0;
            for (int readIndex = 0; readIndex < _pendingBloodCloudPoiWriteCount; readIndex++)
            {
                MigrationBloodCloudPoi poi = _pendingBloodCloudPoiWrites[readIndex];
                if (!IsActiveBloodCloudPoi(in poi, gameTimeSeconds))
                    continue;

                _pendingBloodCloudPoiWrites[writeIndex++] = poi;
            }

            for (int i = writeIndex; i < _pendingBloodCloudPoiWriteCount; i++)
                _pendingBloodCloudPoiWrites[i] = default;

            _pendingBloodCloudPoiWriteCount = writeIndex;
        }

        private void AdvanceMigrationFieldColdTick()
        {
            if (!Application.isPlaying)
                return;

            AllocateMigrationNativeState();
            if (!TryResolveMigrationNativeViews(
                    out NativeArray<MigrationGridCell> frontGrid,
                    out NativeArray<MigrationGridCell> backGrid,
                    out _,
                    out _) ||
                !frontGrid.IsCreated ||
                !backGrid.IsCreated ||
                _migrationFieldScheduled)
            {
                return;
            }

            float qualityWeight = ResolveMigrationQualityWeight();
            float qualityScaledIntervalSeconds = ResolveMigrationFieldColdTickIntervalSeconds(qualityWeight);
            float coldDeltaSeconds = AdvanceColdTickDeltaSeconds(ResolveDispatcherRuntimeSeconds(), qualityScaledIntervalSeconds);
            AdvanceFallbackTimeline(coldDeltaSeconds);
            _coldTickAccumulator += coldDeltaSeconds;
            if (_coldTickAccumulator < qualityScaledIntervalSeconds)
                return;

            _coldTickAccumulator = 0f;
            ScheduleMigrationFieldBuild();
        }

        private void ScheduleMigrationFieldBuild()
        {
            // L19 hop2 LIVE: BuildMigrationVectorFieldJob left incomplete under -batchmode,
            // then aborts FrostTick/GasDynamics on deallocate (InvalidOperationException).
            // Migration vector field is not required for hop input validation.
            if (Application.isBatchMode)
                return;

            if (!TryResolveMigrationNativeViews(
                    out _,
                    out NativeArray<MigrationGridCell> backGrid,
                    out NativeArray<MigrationBloodCloudPoi> bloodCloudPois,
                    out _) ||
                !backGrid.IsCreated ||
                _migrationGridCellCount <= 0)
            {
                return;
            }

            float timelineSeconds = ResolveTimelineGameSeconds(0f);
            FlushPendingBloodCloudPoiWrites(timelineSeconds);
            PruneExpiredMigrationSwarmStates(timelineSeconds);
            float seasonalPhase = ResolveSeasonalPhase(timelineSeconds);
            _debugLastSeasonalPhase = seasonalPhase;
            _debugBloodCloudPoiCount = CountActiveBloodCloudPois(timelineSeconds);
            if (!TryLockMigrationFieldJobBuffers())
                return;

            BuildMigrationVectorFieldJob job = new BuildMigrationVectorFieldJob
            {
                Output = backGrid,
                BloodCloudPois = bloodCloudPois,
                Resolution = _migrationGridResolution,
                CellSizeMeters = ResolveSafeMigrationCellSizeMeters(),
                OriginAupMeters = ResolveSafeMigrationGridOriginAupMeters(),
                SeasonalPhase = seasonalPhase,
                VerticalFlowWeight = math.clamp(SanitizeFinite(migrationVerticalFlowWeight, 0f), 0f, 0.35f),
                CurrentGameTimeSeconds = timelineSeconds,
                GlobalQualityWeight = ResolveMigrationQualityWeight()
            };

            _migrationFieldHandle = job.Schedule(_migrationGridCellCount, 64);
            H8Memory.RegisterActiveJob(NativeMemorySystemId, _migrationFieldHandle);
            _migrationFieldScheduled = true;
        }

        private void CompleteMigrationFieldJob(bool forceComplete)
        {
            if (!_migrationFieldScheduled)
                return;

            if (!TryCompleteMigrationFieldHandle(forceComplete))
                return;

            VaultGenerationHandle<MigrationGridCell> handleSwap = _migrationGridFrontHandle;
            _migrationGridFrontHandle = _migrationGridBackHandle;
            _migrationGridBackHandle = handleSwap;
            UnlockMigrationFieldJobBuffers();
            _migrationFieldScheduled = false;
            if (TryResolveMigrationGridFront(out NativeArray<MigrationGridCell> frontGrid) &&
                frontGrid.IsCreated &&
                frontGrid.Length > 0)
            {
                MigrationGridCell firstCell = frontGrid[0];
                _debugLastMigrationGridDirection = math.all(math.isfinite(firstCell.Direction))
                    ? new Vector3(firstCell.Direction.x, firstCell.Direction.y, firstCell.Direction.z)
                    : Vector3.zero;
            }
            float gameTimeSeconds = ResolveTimelineGameSeconds(0f);
            PruneExpiredMigrationSwarmStates(gameTimeSeconds);
            ApplyMigrationSwarmPopulationCountsToFrontGrid();
            if (FlushPendingBloodCloudPoiWrites(gameTimeSeconds))
                RequestMigrationFieldRebuildSoon();
        }

        private bool TryCompleteMigrationFieldHandle(bool forceComplete)
        {
            if (!forceComplete)
                return DispatcherJobFence.TryComplete(ref _migrationFieldHandle, forceComplete: false);

            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                return DispatcherJobFence.TryComplete(ref _migrationFieldHandle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
        }

        private bool TrySampleMigrationFieldDirection(Vector3 origin, out Vector3 direction)
        {
            direction = Vector3.zero;
            if (!TryResolveMigrationGridFront(out NativeArray<MigrationGridCell> frontGrid) ||
                !frontGrid.IsCreated ||
                frontGrid.Length != _migrationGridCellCount ||
                _migrationGridCellCount <= 0)
            {
                return false;
            }

            double safeCellSize = ResolveSafeMigrationCellSizeMeters();
            double3 originAupMeters = ResolveAupMeters(origin);
            double3 gridOriginAupMeters = ResolveSafeMigrationGridOriginAupMeters();
            double gridX = ((originAupMeters.x - gridOriginAupMeters.x) / safeCellSize) - 0.5d;
            double gridY = ((originAupMeters.y - gridOriginAupMeters.y) / safeCellSize) - 0.5d;
            double gridZ = ((originAupMeters.z - gridOriginAupMeters.z) / safeCellSize) - 0.5d;
            int x0 = FastFloorToInt(gridX);
            int y0 = FastFloorToInt(gridY);
            int z0 = FastFloorToInt(gridZ);
            float tx = math.saturate((float)(gridX - x0));
            float ty = math.saturate((float)(gridY - y0));
            float tz = math.saturate((float)(gridZ - z0));
            int ix0 = WrapIndex(x0, _migrationGridResolution.x);
            int ix1 = WrapIndex(x0 + 1, _migrationGridResolution.x);
            int iy0 = math.clamp(y0, 0, _migrationGridResolution.y - 1);
            int iy1 = math.clamp(y0 + 1, 0, _migrationGridResolution.y - 1);
            int iz0 = WrapIndex(z0, _migrationGridResolution.z);
            int iz1 = WrapIndex(z0 + 1, _migrationGridResolution.z);
            float interpolationWeight = 1f;
            int nearestX = WrapIndex(x0 + (int)math.step(0.5f, tx), _migrationGridResolution.x);
            int nearestY = math.clamp(y0 + (int)math.step(0.5f, ty), 0, _migrationGridResolution.y - 1);
            int nearestZ = WrapIndex(z0 + (int)math.step(0.5f, tz), _migrationGridResolution.z);
            float3 nearest = frontGrid[BuildMigrationCellIndex(nearestX, nearestY, nearestZ, _migrationGridResolution)].Direction;
            if (interpolationWeight <= 0.0001f)
            {
                float nearestSq = math.lengthsq(nearest);
                if (!math.all(math.isfinite(nearest)) || !math.isfinite(nearestSq) || nearestSq <= 0.0001f)
                    return false;

                nearest *= math.rsqrt(nearestSq);
                direction = new Vector3(nearest.x, nearest.y, nearest.z);
                return true;
            }

            float3 c000 = frontGrid[BuildMigrationCellIndex(ix0, iy0, iz0, _migrationGridResolution)].Direction;
            float3 c100 = frontGrid[BuildMigrationCellIndex(ix1, iy0, iz0, _migrationGridResolution)].Direction;
            float3 c010 = frontGrid[BuildMigrationCellIndex(ix0, iy1, iz0, _migrationGridResolution)].Direction;
            float3 c110 = frontGrid[BuildMigrationCellIndex(ix1, iy1, iz0, _migrationGridResolution)].Direction;
            float3 c001 = frontGrid[BuildMigrationCellIndex(ix0, iy0, iz1, _migrationGridResolution)].Direction;
            float3 c101 = frontGrid[BuildMigrationCellIndex(ix1, iy0, iz1, _migrationGridResolution)].Direction;
            float3 c011 = frontGrid[BuildMigrationCellIndex(ix0, iy1, iz1, _migrationGridResolution)].Direction;
            float3 c111 = frontGrid[BuildMigrationCellIndex(ix1, iy1, iz1, _migrationGridResolution)].Direction;
            float3 c00 = math.lerp(c000, c100, tx);
            float3 c10 = math.lerp(c010, c110, tx);
            float3 c01 = math.lerp(c001, c101, tx);
            float3 c11 = math.lerp(c011, c111, tx);
            float3 c0 = math.lerp(c00, c10, ty);
            float3 c1 = math.lerp(c01, c11, ty);
            float3 sampled = math.lerp(nearest, math.lerp(c0, c1, tz), interpolationWeight);
            float sampledSq = math.lengthsq(sampled);
            if (!math.all(math.isfinite(sampled)) || !math.isfinite(sampledSq) || sampledSq <= 0.0001f)
                return false;

            sampled *= math.rsqrt(sampledSq);
            direction = new Vector3(sampled.x, sampled.y, sampled.z);
            return true;
        }

        private float ResolveWhaleFallPopulationMultiplier(int speciesId, Vector3 origin, float gameTimeSeconds)
        {
            if (!IsScavengerMigrationSpecies(speciesId))
                return 1f;

            CompactPendingBloodCloudPoiWrites(gameTimeSeconds);
            if (_pendingBloodCloudPoiWriteCount <= 0 && CountActiveBloodCloudPois(gameTimeSeconds) <= 0)
                return 1f;

            float multiplier = 1f;
            if (!TryResolveRuntimeAup(origin, out AbsoluteUniversePosition originAup))
                return multiplier;

            for (int i = 0; i < _bloodCloudPoiMirror.Length; i++)
            {
                MigrationBloodCloudPoi poi = _bloodCloudPoiMirror[i];
                if (HasPendingBloodCloudPoiSource(poi.SourceId, gameTimeSeconds))
                    continue;

                multiplier = ResolveWhaleFallPopulationMultiplierForPoi(in poi, in originAup, gameTimeSeconds, multiplier);
            }

            for (int i = 0; i < _pendingBloodCloudPoiWriteCount; i++)
            {
                MigrationBloodCloudPoi pendingPoi = _pendingBloodCloudPoiWrites[i];
                multiplier = ResolveWhaleFallPopulationMultiplierForPoi(in pendingPoi, in originAup, gameTimeSeconds, multiplier);
            }

            return multiplier;
        }

        private float ResolveWhaleFallPopulationMultiplierForPoi(
            in MigrationBloodCloudPoi poi,
            in AbsoluteUniversePosition originAup,
            float gameTimeSeconds,
            float currentMultiplier)
        {
            if (!IsActiveBloodCloudPoi(in poi, gameTimeSeconds))
                return currentMultiplier;

            double radius = SanitizePositiveFinite(poi.RadiusMeters, 1f);
            double radiusSq = radius * radius;
            double invRadiusSq = 1d / radiusSq;
            AbsoluteUniversePosition poiAup = AbsoluteUniversePosition.FromAlignedBlit(in poi.PositionAup);
            if (!poiAup.IsFinite())
                return currentMultiplier;

            double distSq = AbsoluteUniversePosition.DistanceSq(in originAup, in poiAup);
            if (!math.isfinite(distSq) || distSq > radiusSq)
                return currentMultiplier;

            float falloff = math.saturate((float)(1d - distSq * invRadiusSq));
            float safePopulationMultiplier = math.max(1f, SanitizeFinite(whaleFallScavengerPopulationMultiplier, 1f));
            float candidateMultiplier = math.lerp(1f, safePopulationMultiplier, falloff * falloff);
            if (!math.isfinite(candidateMultiplier))
                return currentMultiplier;

            return candidateMultiplier > currentMultiplier ? candidateMultiplier : currentMultiplier;
        }

        private bool IsScavengerMigrationSpecies(int speciesId)
        {
            if (scavengerMigrationSpeciesIds == null || scavengerMigrationSpeciesIds.Length == 0)
                return speciesId > 0;

            for (int i = 0; i < scavengerMigrationSpeciesIds.Length; i++)
            {
                if (scavengerMigrationSpeciesIds[i] == speciesId)
                    return true;
            }

            return false;
        }

        private void WriteSwarmPopulationState(int speciesId, Vector3 origin, int populationCount)
        {
            if (!TryResolveMigrationNativeViews(
                    out NativeArray<MigrationGridCell> frontGrid,
                    out _,
                    out _,
                    out NativeArray<MigrationSwarmState> swarmStates) ||
                !swarmStates.IsCreated ||
                swarmStates.Length == 0)
            {
                return;
            }

            float gameTimeSeconds = ResolveTimelineGameSeconds(0f);
            PruneExpiredMigrationSwarmStates(gameTimeSeconds);
            int3 aupCell = ResolveMigrationAupCell(origin);
            uint hash = Hash((uint)speciesId ^ HashInt3(aupCell));
            int safePopulationCount = Mathf.Clamp(populationCount, 0, ushort.MaxValue);
            if (safePopulationCount <= 0)
            {
                if (!TryFindMigrationSwarmStateSlot(swarmStates, hash, speciesId, in aupCell, out int clearSlot))
                    return;

                MigrationSwarmState clearedState = swarmStates[clearSlot];
                swarmStates[clearSlot] = default;
                RecomputeMigrationGridPopulationCell(frontGrid, swarmStates, in clearedState.AupCell);
                RefreshDebugStatisticalSwarmSlotCount(swarmStates);
                return;
            }

            int slot = ResolveMigrationSwarmStateSlot(swarmStates, hash, speciesId, in aupCell);
            MigrationSwarmState previousState = swarmStates[slot];
            bool hadPreviousState = previousState.Flags != 0;

            MigrationSwarmState state = default;
            state.AupCell = aupCell;
            state.LocalPosition = new float3(origin.x, origin.y, origin.z);
            state.RadiusMeters = 200f;
            state.LastWriteGameTimeSeconds = gameTimeSeconds;
            state.PopulationCount = (ushort)safePopulationCount;
            state.SpeciesId = (ushort)Mathf.Clamp(speciesId, 0, ushort.MaxValue);
            state.Flags = 1u;
            swarmStates[slot] = state;
            if (hadPreviousState && !AreSameAupCell(in previousState.AupCell, in state.AupCell))
                RecomputeMigrationGridPopulationCell(frontGrid, swarmStates, in previousState.AupCell);

            RecomputeMigrationGridPopulationCell(frontGrid, swarmStates, in state.AupCell);
            RefreshDebugStatisticalSwarmSlotCount(swarmStates);
        }

        private static int ResolveMigrationSwarmStateSlot(
            NativeArray<MigrationSwarmState> swarmStates,
            uint hash,
            int speciesId,
            in int3 aupCell)
        {
            int capacity = swarmStates.Length;
            int startSlot = (int)(hash % (uint)capacity);
            int emptySlot = -1;
            int oldestSlot = startSlot;
            float oldestWriteTime = float.MaxValue;
            ushort speciesKey = (ushort)Mathf.Clamp(speciesId, 0, ushort.MaxValue);
            for (int probe = 0; probe < capacity; probe++)
            {
                int slot = (startSlot + probe) % capacity;
                MigrationSwarmState candidate = swarmStates[slot];
                if (candidate.Flags == 0)
                {
                    if (emptySlot < 0)
                        emptySlot = slot;

                    continue;
                }

                if (candidate.SpeciesId == speciesKey &&
                    AreSameAupCell(in candidate.AupCell, in aupCell))
                {
                    return slot;
                }

                if (candidate.LastWriteGameTimeSeconds < oldestWriteTime)
                {
                    oldestWriteTime = candidate.LastWriteGameTimeSeconds;
                    oldestSlot = slot;
                }
            }

            return emptySlot >= 0 ? emptySlot : oldestSlot;
        }

        private static bool TryFindMigrationSwarmStateSlot(
            NativeArray<MigrationSwarmState> swarmStates,
            uint hash,
            int speciesId,
            in int3 aupCell,
            out int slot)
        {
            slot = -1;
            int capacity = swarmStates.Length;
            if (capacity <= 0)
                return false;

            int startSlot = (int)(hash % (uint)capacity);
            ushort speciesKey = (ushort)Mathf.Clamp(speciesId, 0, ushort.MaxValue);
            for (int probe = 0; probe < capacity; probe++)
            {
                int candidateSlot = (startSlot + probe) % capacity;
                MigrationSwarmState candidate = swarmStates[candidateSlot];
                if (candidate.Flags == 0)
                    continue;

                if (candidate.SpeciesId == speciesKey &&
                    AreSameAupCell(in candidate.AupCell, in aupCell))
                {
                    slot = candidateSlot;
                    return true;
                }
            }

            return false;
        }

        private void ApplyMigrationSwarmPopulationCountsToFrontGrid()
        {
            if (!TryResolveMigrationNativeViews(
                    out NativeArray<MigrationGridCell> frontGrid,
                    out _,
                    out _,
                    out NativeArray<MigrationSwarmState> swarmStates) ||
                !frontGrid.IsCreated ||
                !swarmStates.IsCreated)
            {
                return;
            }

            for (int i = 0; i < swarmStates.Length; i++)
            {
                MigrationSwarmState state = swarmStates[i];
                if (state.Flags != 0 && state.PopulationCount > 0)
                    ApplyMigrationGridPopulationDelta(frontGrid, in state, state.PopulationCount);
            }
        }

        private void PruneExpiredMigrationSwarmStates(float gameTimeSeconds)
        {
            if (!TryResolveMigrationNativeViews(
                    out NativeArray<MigrationGridCell> frontGrid,
                    out _,
                    out _,
                    out NativeArray<MigrationSwarmState> swarmStates) ||
                !swarmStates.IsCreated)
            {
                _debugStatisticalSwarmSlotCount = 0;
                return;
            }

            bool changed = false;
            for (int i = 0; i < swarmStates.Length; i++)
            {
                MigrationSwarmState state = swarmStates[i];
                if (state.Flags == 0)
                    continue;

                if (!IsMigrationSwarmStateExpired(in state, gameTimeSeconds))
                    continue;

                swarmStates[i] = default;
                RecomputeMigrationGridPopulationCell(frontGrid, swarmStates, in state.AupCell);
                changed = true;
            }

            if (changed)
                RefreshDebugStatisticalSwarmSlotCount(swarmStates);
        }

        private static bool IsMigrationSwarmStateExpired(in MigrationSwarmState state, float gameTimeSeconds)
        {
            float ageSeconds = gameTimeSeconds - state.LastWriteGameTimeSeconds;
            if (ageSeconds > MigrationSwarmStateLifetimeGameSeconds)
                return true;

            return ageSeconds < -MigrationSwarmStateFutureToleranceGameSeconds;
        }

        private void ApplyMigrationGridPopulationDelta(
            NativeArray<MigrationGridCell> frontGrid,
            in MigrationSwarmState state,
            int populationDelta)
        {
            if (!frontGrid.IsCreated ||
                frontGrid.Length != _migrationGridCellCount ||
                _migrationGridCellCount <= 0 ||
                populationDelta == 0 ||
                !TryResolveMigrationGridIndexFromAupCell(in state.AupCell, out int cellIndex))
            {
                return;
            }

            MigrationGridCell cell = frontGrid[cellIndex];
            int rawPopulation = cell.PopulationCount + populationDelta;
            int resolvedPopulation = math.clamp(rawPopulation, 0, ushort.MaxValue);
            cell.PopulationCount = (ushort)resolvedPopulation;
            if (resolvedPopulation > 0)
            {
                cell.Flags = (ushort)(cell.Flags | MigrationCellFlagPopulation);
                cell.Flags = rawPopulation > ushort.MaxValue
                    ? (ushort)(cell.Flags | MigrationCellFlagPopulationSaturated)
                    : (ushort)(cell.Flags & MigrationCellFlagNoPopulationSaturated);
            }
            else
            {
                cell.Flags = (ushort)(cell.Flags & MigrationCellFlagNoPopulation);
                cell.Flags = (ushort)(cell.Flags & MigrationCellFlagNoPopulationSaturated);
            }

            frontGrid[cellIndex] = cell;
        }

        private void RecomputeMigrationGridPopulationCell(
            NativeArray<MigrationGridCell> frontGrid,
            NativeArray<MigrationSwarmState> swarmStates,
            in int3 aupCell)
        {
            if (!frontGrid.IsCreated ||
                !swarmStates.IsCreated ||
                frontGrid.Length != _migrationGridCellCount ||
                _migrationGridCellCount <= 0 ||
                !TryResolveMigrationGridIndexFromAupCell(in aupCell, out int targetCellIndex))
            {
                return;
            }

            int population = 0;
            bool saturated = false;
            for (int i = 0; i < swarmStates.Length; i++)
            {
                MigrationSwarmState state = swarmStates[i];
                if (state.Flags == 0 || state.PopulationCount == 0)
                    continue;

                if (!TryResolveMigrationGridIndexFromAupCell(in state.AupCell, out int stateCellIndex) ||
                    stateCellIndex != targetCellIndex)
                {
                    continue;
                }

                int nextPopulation = population + state.PopulationCount;
                if (nextPopulation > ushort.MaxValue)
                {
                    population = ushort.MaxValue;
                    saturated = true;
                    break;
                }

                population = nextPopulation;
            }

            MigrationGridCell cell = frontGrid[targetCellIndex];
            cell.PopulationCount = (ushort)population;
            if (population > 0)
            {
                cell.Flags = (ushort)(cell.Flags | MigrationCellFlagPopulation);
                cell.Flags = saturated
                    ? (ushort)(cell.Flags | MigrationCellFlagPopulationSaturated)
                    : (ushort)(cell.Flags & MigrationCellFlagNoPopulationSaturated);
            }
            else
            {
                cell.Flags = (ushort)(cell.Flags & MigrationCellFlagNoPopulation);
                cell.Flags = (ushort)(cell.Flags & MigrationCellFlagNoPopulationSaturated);
            }

            frontGrid[targetCellIndex] = cell;
        }

        private bool TryResolveMigrationGridIndexFromAupCell(in int3 aupCell, out int cellIndex)
        {
            cellIndex = 0;
            if (_migrationGridResolution.x <= 0 || _migrationGridResolution.y <= 0 || _migrationGridResolution.z <= 0)
                return false;

            double safeCellSize = ResolveSafeMigrationCellSizeMeters();
            double3 gridOriginAupMeters = ResolveSafeMigrationGridOriginAupMeters();
            double cellCenterX = (aupCell.x + 0.5d) * safeCellSize;
            double cellCenterY = (aupCell.y + 0.5d) * safeCellSize;
            double cellCenterZ = (aupCell.z + 0.5d) * safeCellSize;
            int ix = WrapIndex(FastFloorToInt((cellCenterX - gridOriginAupMeters.x) / safeCellSize), _migrationGridResolution.x);
            int iy = Mathf.Clamp(FastFloorToInt((cellCenterY - gridOriginAupMeters.y) / safeCellSize), 0, _migrationGridResolution.y - 1);
            int iz = WrapIndex(FastFloorToInt((cellCenterZ - gridOriginAupMeters.z) / safeCellSize), _migrationGridResolution.z);
            cellIndex = BuildMigrationCellIndex(ix, iy, iz, _migrationGridResolution);
            return cellIndex >= 0 && cellIndex < _migrationGridCellCount;
        }

        private void RefreshDebugStatisticalSwarmSlotCount()
        {
            if (!TryResolveMigrationSwarmStates(out NativeArray<MigrationSwarmState> swarmStates) ||
                !swarmStates.IsCreated)
            {
                _debugStatisticalSwarmSlotCount = 0;
                return;
            }

            RefreshDebugStatisticalSwarmSlotCount(swarmStates);
        }

        private void RefreshDebugStatisticalSwarmSlotCount(NativeArray<MigrationSwarmState> swarmStates)
        {
            if (!swarmStates.IsCreated)
            {
                _debugStatisticalSwarmSlotCount = 0;
                return;
            }

            int count = 0;
            for (int i = 0; i < swarmStates.Length; i++)
            {
                if (swarmStates[i].Flags != 0)
                    count++;
            }

            _debugStatisticalSwarmSlotCount = count;
        }

        private int CountActiveBloodCloudPois(float gameTimeSeconds)
        {
            int count = 0;
            for (int i = 0; i < _bloodCloudPoiMirror.Length; i++)
            {
                MigrationBloodCloudPoi poi = _bloodCloudPoiMirror[i];
                if (IsActiveBloodCloudPoi(in poi, gameTimeSeconds))
                    count++;
            }

            for (int i = 0; i < _pendingBloodCloudPoiWriteCount; i++)
            {
                MigrationBloodCloudPoi pendingPoi = _pendingBloodCloudPoiWrites[i];
                if (!IsActiveBloodCloudPoi(in pendingPoi, gameTimeSeconds))
                    continue;

                if (HasActiveBloodCloudPoiSource(pendingPoi.SourceId, gameTimeSeconds))
                    continue;

                count++;
            }

            return count;
        }

        private bool HasPendingBloodCloudPoiSource(int sourceId, float gameTimeSeconds)
        {
            for (int i = 0; i < _pendingBloodCloudPoiWriteCount; i++)
            {
                MigrationBloodCloudPoi poi = _pendingBloodCloudPoiWrites[i];
                if (poi.SourceId == sourceId && IsActiveBloodCloudPoi(in poi, gameTimeSeconds))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasActiveBloodCloudPoiSource(int sourceId, float gameTimeSeconds)
        {
            for (int i = 0; i < _bloodCloudPoiMirror.Length; i++)
            {
                MigrationBloodCloudPoi poi = _bloodCloudPoiMirror[i];
                if (poi.SourceId == sourceId && IsActiveBloodCloudPoi(in poi, gameTimeSeconds))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsActiveBloodCloudPoi(in MigrationBloodCloudPoi poi, float gameTimeSeconds)
        {
            return poi.Flags != 0 &&
                math.isfinite(gameTimeSeconds) &&
                math.isfinite(poi.ExpireGameTimeSeconds) &&
                gameTimeSeconds < poi.ExpireGameTimeSeconds &&
                math.all(math.isfinite(poi.PositionFieldLocalMeters)) &&
                math.isfinite(poi.RadiusMeters) &&
                poi.RadiusMeters > 0f &&
                math.isfinite(poi.Strength) &&
                poi.Strength > 0f;
        }

        private float ResolveTimelineGameSeconds(float fallbackRuntimeSeconds)
        {
            HectonCelestialEngine celestialEngine = _celestialEngine;
            if (celestialEngine != null && math.isfinite(celestialEngine.GameTime))
                return math.max(0f, celestialEngine.GameTime);

            if (math.isfinite(fallbackRuntimeSeconds) && fallbackRuntimeSeconds > 0f)
                return fallbackRuntimeSeconds;

            return math.isfinite(_fallbackTimelineGameSeconds)
                ? math.max(0f, _fallbackTimelineGameSeconds)
                : 0f;
        }

        private void AdvanceFallbackTimeline(float deltaSeconds)
        {
            if (!math.isfinite(deltaSeconds) || deltaSeconds <= 0f)
                return;

            _fallbackTimelineGameSeconds = math.min(
                MigrationFallbackTimelineMaxSeconds,
                math.max(0f, _fallbackTimelineGameSeconds + deltaSeconds));
        }

        private float ResolveSeasonalPhase(float gameTimeSeconds)
        {
            HectonCelestialEngine celestialEngine = _celestialEngine;
            float planetPhase = celestialEngine != null ? SanitizeFinite(celestialEngine.PlanetPhase, 0f) : 0f;
            float celestialPhase = planetPhase * Tau;
            float safeGameTimeSeconds = math.max(0f, SanitizeFinite(gameTimeSeconds, 0f));
            float seasonalRate = math.max(0f, SanitizeFinite(seasonalRadiansPerGameSecond, 0f));
            return celestialPhase + safeGameTimeSeconds * seasonalRate;
        }

        private Vector3 ResolveMigrationProbePosition(int biomeIndex, uint hash)
        {
            float biomeOffset = biomeIndex * 173.31f;
            float dayOffset = _currentDayIndex * 41.7f;
            float x = MathLodApproximation.ApproxSinBhaskara(biomeOffset + dayOffset + Hash01(hash ^ 0x68E31DA4u) * Tau) * 420f;
            float z = MathLodApproximation.ApproxCosBhaskara(biomeOffset * 0.5f + dayOffset + Hash01(hash ^ 0xC2B2AE35u) * Tau) * 420f;
            float y = -math.lerp(24f, 220f, Hash01(hash ^ 0x9E3779B9u));
            return new Vector3(x, y, z);
        }

        private static Vector3 BlendRouteWithCurrent(Vector3 preferredDirection, Vector3 currentVector, float currentAlignmentWeight)
        {
            float3 current = new float3(currentVector.x, currentVector.y, currentVector.z);
            float currentSq = math.lengthsq(current);
            if (currentSq <= 0.0001f)
                return preferredDirection;

            current *= math.rsqrt(currentSq);
            Vector3 blended = BlendDirectionsLinear(
                preferredDirection,
                new Vector3(current.x, current.y, current.z),
                currentAlignmentWeight);
            blended.y = 0f;
            if (blended.sqrMagnitude <= 0.0001f)
                return preferredDirection;

            float3 planar = new float3(blended.x, 0f, blended.z);
            float3 planarFallback = NormalizeOrFallback(new float3(preferredDirection.x, 0f, preferredDirection.z), new float3(0f, 0f, 1f));
            planar = NormalizeOrFallback(planar, planarFallback);
            return new Vector3(planar.x, 0f, planar.z);
        }

        private static Vector3 BlendDirectionsLinear(Vector3 from, Vector3 to, float weight)
        {
            float3 a = NormalizeOrFallback(new float3(from.x, from.y, from.z), new float3(0f, 0f, 1f));
            float3 b = NormalizeOrFallback(new float3(to.x, to.y, to.z), a);
            float3 blended = NormalizeOrFallback(math.lerp(a, b, math.saturate(weight)), a);
            return new Vector3(blended.x, blended.y, blended.z);
        }

        private static Vector3 BuildMigrationTargetRuntime(Vector3 origin, Vector3 direction, float routeDistanceMeters, float depthBiasMeters)
        {
            double3 routeOffset = new double3(
                direction.x * (double)routeDistanceMeters,
                direction.y * (double)routeDistanceMeters - depthBiasMeters,
                direction.z * (double)routeDistanceMeters);
            if (!math.all(math.isfinite(routeOffset)))
                return origin;

            if (!TryResolveAupMeters(origin, out double3 originAbsolute))
            {
                return TryResolveFiniteRuntimeVector(routeOffset, out Vector3 fallbackOffset) &&
                    TryResolveFiniteRuntimeVector(new double3(
                        origin.x + (double)fallbackOffset.x,
                        origin.y + (double)fallbackOffset.y,
                        origin.z + (double)fallbackOffset.z), out Vector3 fallbackTarget)
                    ? fallbackTarget
                    : origin;
            }

            AbsoluteUniversePosition targetAup = AbsoluteUniversePosition.FromAbsolutePosition(originAbsolute + routeOffset);
            return TryResolveRuntimePosition(in targetAup, out Vector3 runtimeTarget)
                ? runtimeTarget
                : origin;
        }

        private static bool TryResolveRuntimePosition(in AbsoluteUniversePosition positionAup, out Vector3 runtimePosition)
        {
            runtimePosition = default;
            if (!AbsoluteUniversePosition.IsFinite(in positionAup))
                return false;

            float3 runtime = positionAup.ToRuntimeFloat3();
            if (!math.all(math.isfinite(runtime)))
                return false;

            runtimePosition = new Vector3(runtime.x, runtime.y, runtime.z);
            return true;
        }

        private static bool TryResolveFiniteRuntimeVector(double3 value, out Vector3 runtimeVector)
        {
            runtimeVector = default;
            if (!math.all(math.isfinite(value)))
                return false;

            float3 runtime = new float3((float)value.x, (float)value.y, (float)value.z);
            if (!math.all(math.isfinite(runtime)))
                return false;

            runtimeVector = new Vector3(runtime.x, runtime.y, runtime.z);
            return true;
        }

        private static bool IsFiniteVector3(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                float.IsFinite(value.y) &&
                float.IsFinite(value.z);
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static float Sanitize01Finite(float value)
        {
            return math.saturate(SanitizeFinite(value, 0f));
        }

        private static float SanitizePositiveFinite(float value, float fallback)
        {
            return math.max(fallback, SanitizeFinite(value, fallback));
        }

        private static float3 NormalizeOrFallback(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return lengthSq > 0.0001f ? value * math.rsqrt(lengthSq) : fallback;
        }

        private float ResolveWaterTemperature(Vector3 origin)
        {
            HectonMapMagicVegetationBridge bridge = _mapMagicVegetationBridge;
            return bridge != null ? bridge.GetWaterTemperature(origin) : 15f;
        }

        private static Vector3 ResolvePreferredDirection(Vector2 preferredPlanarDirection, uint seed)
        {
            float2 planarDirection = new float2(preferredPlanarDirection.x, preferredPlanarDirection.y);
            float planarSq = math.lengthsq(planarDirection);
            if (planarSq > 0.0001f)
            {
                planarDirection *= math.rsqrt(planarSq);
            }
            else
            {
                float headingRadians = Hash01(seed ^ 0x68E31DA4u) * Tau;
                MathLodApproximation.ApproxSinCosBhaskara(headingRadians, out float headingSin, out float headingCos);
                planarDirection = new float2(headingCos, headingSin);
            }

            return new Vector3(planarDirection.x, 0f, planarDirection.y);
        }

        private void SanitizeMigrationSettings()
        {
            fallbackMigrationDistanceMeters = SanitizePositiveFinite(fallbackMigrationDistanceMeters, 1f);
            fallbackCurrentAlignmentWeight = Sanitize01Finite(fallbackCurrentAlignmentWeight);
            migrationCellSizeMeters = SanitizePositiveFinite(migrationCellSizeMeters, GlobalMigrationCellSizeMeters);
            migrationGridResolutionX = Mathf.Clamp(migrationGridResolutionX, MinimumGridResolutionXZ, MaximumGridResolutionXZ);
            migrationGridResolutionY = Mathf.Clamp(migrationGridResolutionY, MinimumGridResolutionY, MaximumGridResolutionY);
            migrationGridResolutionZ = Mathf.Clamp(migrationGridResolutionZ, MinimumGridResolutionXZ, MaximumGridResolutionXZ);
            migrationFieldColdTickIntervalSeconds = SanitizePositiveFinite(migrationFieldColdTickIntervalSeconds, 1f);
            migrationFlowAlignmentWeight = Sanitize01Finite(migrationFlowAlignmentWeight);
            seasonalRadiansPerGameSecond = math.max(0f, SanitizeFinite(seasonalRadiansPerGameSecond, 0f));
            migrationVerticalFlowWeight = math.clamp(SanitizeFinite(migrationVerticalFlowWeight, 0f), 0f, 0.35f);
            bloodCloudPoiRadiusMeters = math.max(10f, SanitizeFinite(bloodCloudPoiRadiusMeters, 10f));
            bloodCloudPoiStrength = math.clamp(SanitizeFinite(bloodCloudPoiStrength, 0f), 0f, 4f);
            whaleFallScavengerPopulationMultiplier = math.clamp(SanitizeFinite(whaleFallScavengerPopulationMultiplier, 1f), 1f, 50f);
            vrVatSwayAmplitudeScale = math.clamp(SanitizeFinite(vrVatSwayAmplitudeScale, 1f), 1f, 2f);
        }

        private IDataVault CacheMigrationDataVault()
        {
            return _migrationVault;
        }

        private bool TryResolveMigrationNativeViews(
            out NativeArray<MigrationGridCell> frontGrid,
            out NativeArray<MigrationGridCell> backGrid,
            out NativeArray<MigrationBloodCloudPoi> bloodCloudPois,
            out NativeArray<MigrationSwarmState> swarmStates)
        {
            frontGrid = default;
            backGrid = default;
            bloodCloudPois = default;
            swarmStates = default;
            IDataVault vault = _migrationVault;
            if (vault == null)
                return false;

            if (!TryOpenMigrationGridBuffer(vault, in _migrationGridFrontHandle, _migrationGridCellCount, out frontGrid) ||
                !TryOpenMigrationGridBuffer(vault, in _migrationGridBackHandle, _migrationGridCellCount, out backGrid) ||
                !TryOpenVaultBuffer(vault, in _bloodCloudPoisHandle, BufferID.ShinobuMigrationBloodCloudPois, BloodCloudPoiCapacity, out bloodCloudPois) ||
                !TryOpenVaultBuffer(vault, in _migrationSwarmStatesHandle, BufferID.ShinobuMigrationSwarmStates, MigrationSwarmCapacity, out swarmStates) ||
                _migrationGridFrontHandle.BufferID == _migrationGridBackHandle.BufferID)
            {
                return false;
            }

            return frontGrid.IsCreated &&
                backGrid.IsCreated &&
                bloodCloudPois.IsCreated &&
                swarmStates.IsCreated;
        }

        private bool TryResolveMigrationGridFront(out NativeArray<MigrationGridCell> frontGrid)
        {
            frontGrid = default;
            IDataVault vault = _migrationVault;
            return vault != null &&
                TryOpenMigrationGridBuffer(vault, in _migrationGridFrontHandle, _migrationGridCellCount, out frontGrid);
        }

        private bool TryResolveBloodCloudPois(out NativeArray<MigrationBloodCloudPoi> bloodCloudPois)
        {
            bloodCloudPois = default;
            IDataVault vault = _migrationVault;
            return vault != null &&
                TryOpenVaultBuffer(vault, in _bloodCloudPoisHandle, BufferID.ShinobuMigrationBloodCloudPois, BloodCloudPoiCapacity, out bloodCloudPois);
        }

        private bool TryResolveMigrationSwarmStates(out NativeArray<MigrationSwarmState> swarmStates)
        {
            swarmStates = default;
            IDataVault vault = _migrationVault;
            return vault != null &&
                TryOpenVaultBuffer(vault, in _migrationSwarmStatesHandle, BufferID.ShinobuMigrationSwarmStates, MigrationSwarmCapacity, out swarmStates);
        }

        private static bool TryOpenMigrationGridBuffer(
            IDataVault vault,
            in VaultGenerationHandle<MigrationGridCell> handle,
            int requiredLength,
            out NativeArray<MigrationGridCell> buffer)
        {
            buffer = default;
            if (vault == null || requiredLength < 0 || !IsMigrationGridHandle(in handle))
                return false;

            if (!vault.TryResolveHandle(in handle, out buffer) || !buffer.IsCreated || buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool TryOpenVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength < 0 ||
                handle.BufferID != unchecked((uint)(int)bufferId) ||
                handle.Generation == 0u)
            {
                return false;
            }

            if (!vault.TryResolveHandle(in handle, out buffer) || !buffer.IsCreated || buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsMigrationGridHandle(in VaultGenerationHandle<MigrationGridCell> handle)
        {
            return handle.Generation != 0u &&
                   (handle.BufferID == unchecked((uint)(int)BufferID.ShinobuMigrationGridFront) ||
                    handle.BufferID == unchecked((uint)(int)BufferID.ShinobuMigrationGridBack));
        }

        private static BufferID ToBufferId(in VaultGenerationHandle<MigrationGridCell> handle)
        {
            return (BufferID)unchecked((int)handle.BufferID);
        }

        private static void ReleaseVaultBuffer<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null && handle.BufferID != 0u && handle.Generation != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool AreMigrationDtoLayoutsValid()
        {
            return UnsafeUtility.SizeOf<MigrationGridCell>() == 32 &&
                UnsafeUtility.SizeOf<MigrationBloodCloudPoi>() == 80 &&
                UnsafeUtility.SizeOf<MigrationSwarmState>() == 40 &&
                Marshal.OffsetOf<MigrationSwarmState>(nameof(MigrationSwarmState.Flags)).ToInt32() == 32 &&
                Marshal.OffsetOf<MigrationSwarmState>(nameof(MigrationSwarmState.PopulationCount)).ToInt32() == 36 &&
                Marshal.OffsetOf<MigrationSwarmState>(nameof(MigrationSwarmState.SpeciesId)).ToInt32() == 38;
        }

        private void AllocateMigrationNativeState()
        {
            SanitizeMigrationSettings();
            int3 requiredResolution = new int3(migrationGridResolutionX, migrationGridResolutionY, migrationGridResolutionZ);
            int requiredCellCount = requiredResolution.x * requiredResolution.y * requiredResolution.z;
            if (TryResolveMigrationNativeViews(out _, out _, out _, out _) &&
                _migrationGridCellCount == requiredCellCount &&
                math.all(_migrationGridResolution == requiredResolution))
            {
                return;
            }

            IDataVault vault = CacheMigrationDataVault();
            if (vault == null)
            {
                DisposeMigrationNativeState();
                return;
            }

            if (!AreMigrationDtoLayoutsValid())
            {
                DisposeMigrationNativeState();
                return;
            }

            DisposeMigrationNativeState();
            _migrationVault = vault;
            _migrationGridResolution = requiredResolution;
            _migrationGridCellCount = requiredCellCount;
            _debugMigrationGridCellCount = requiredCellCount;

            _migrationGridFrontHandle = vault.EnsureGenerationHandle<MigrationGridCell>(BufferID.ShinobuMigrationGridFront, requiredCellCount, NativeMemorySystemId, NativeArrayOptions.ClearMemory);
            _migrationGridBackHandle = vault.EnsureGenerationHandle<MigrationGridCell>(BufferID.ShinobuMigrationGridBack, requiredCellCount, NativeMemorySystemId, NativeArrayOptions.UninitializedMemory);
            _bloodCloudPoisHandle = vault.EnsureGenerationHandle<MigrationBloodCloudPoi>(BufferID.ShinobuMigrationBloodCloudPois, BloodCloudPoiCapacity, NativeMemorySystemId, NativeArrayOptions.ClearMemory);
            _migrationSwarmStatesHandle = vault.EnsureGenerationHandle<MigrationSwarmState>(BufferID.ShinobuMigrationSwarmStates, MigrationSwarmCapacity, NativeMemorySystemId, NativeArrayOptions.ClearMemory);

            if (!TryResolveMigrationNativeViews(
                    out NativeArray<MigrationGridCell> frontGrid,
                    out _,
                    out NativeArray<MigrationBloodCloudPoi> bloodCloudPois,
                    out NativeArray<MigrationSwarmState> swarmStates))
            {
                DisposeMigrationNativeState();
                return;
            }

            ClearMigrationNativeStateRows(frontGrid, bloodCloudPois, swarmStates);
            _debugStatisticalSwarmSlotCount = 0;
            _coldTickAccumulator = ResolveMigrationFieldColdTickIntervalSeconds(ResolveMigrationQualityWeight());
            _lastColdTickRuntimeSeconds = -1f;
        }

        private void DisposeMigrationNativeState()
        {
            CompleteMigrationFieldJob(forceComplete: true);
            UnlockMigrationFieldJobBuffers();
            IDataVault vault = _migrationVault;
            ReleaseVaultBuffer(vault, ref _migrationGridFrontHandle);
            ReleaseVaultBuffer(vault, ref _migrationGridBackHandle);
            ReleaseVaultBuffer(vault, ref _bloodCloudPoisHandle);
            ReleaseVaultBuffer(vault, ref _migrationSwarmStatesHandle);
            _migrationVault = null;
            _migrationGridCellCount = 0;
            _migrationGridResolution = int3.zero;
            _debugMigrationGridCellCount = 0;
            _debugBloodCloudPoiCount = 0;
            _debugStatisticalSwarmSlotCount = 0;
            for (int i = 0; i < _bloodCloudPoiMirror.Length; i++)
                _bloodCloudPoiMirror[i] = default;
            for (int i = 0; i < _pendingBloodCloudPoiWriteCount; i++)
                _pendingBloodCloudPoiWrites[i] = default;
            _pendingBloodCloudPoiWriteCount = 0;
            _coldTickAccumulator = 0f;
            _lastColdTickRuntimeSeconds = -1f;
            _migrationFieldHandle = default;
            _migrationFieldScheduled = false;
            _migrationFieldGuardMask = 0UL;
            _migrationFieldGuardVault = null;
        }

        private void ClearMigrationNativeStateRows(
            NativeArray<MigrationGridCell> frontGrid,
            NativeArray<MigrationBloodCloudPoi> bloodCloudPois,
            NativeArray<MigrationSwarmState> swarmStates)
        {
            for (int i = 0; i < _migrationGridCellCount; i++)
                frontGrid[i] = default;

            for (int i = 0; i < bloodCloudPois.Length; i++)
                bloodCloudPois[i] = default;

            for (int i = 0; i < swarmStates.Length; i++)
                swarmStates[i] = default;
        }

        private bool TryLockMigrationFieldJobBuffers()
        {
            IDataVault vault = CacheMigrationDataVault();
            if (vault == null ||
                !IsMigrationGridHandle(in _migrationGridBackHandle) ||
                _bloodCloudPoisHandle.BufferID != unchecked((uint)(int)BufferID.ShinobuMigrationBloodCloudPois) ||
                _bloodCloudPoisHandle.Generation == 0u)
            {
                return false;
            }

            BufferID writeBufferId = ToBufferId(in _migrationGridBackHandle);
            BufferID poiBufferId = BufferID.ShinobuMigrationBloodCloudPois;
            ulong guardMask = MigrationFieldGuardBit(writeBufferId) | MigrationFieldGuardBit(poiBufferId);
            if (!vault.TryAcquireMutationGuard(guardMask))
                return false;

            _migrationFieldWriteBufferId = writeBufferId;
            _migrationFieldPoiBufferId = poiBufferId;
            _migrationFieldGuardMask = guardMask;
            _migrationFieldGuardVault = vault;
            _migrationFieldBuffersLocked = true;
            return true;
        }

        private void UnlockMigrationFieldJobBuffers()
        {
            if (!_migrationFieldBuffersLocked)
                return;

            IDataVault vault = _migrationFieldGuardVault ?? CacheMigrationDataVault();
            ulong guardMask = _migrationFieldGuardMask;
            _migrationFieldWriteBufferId = BufferID.Unknown;
            _migrationFieldPoiBufferId = BufferID.Unknown;
            _migrationFieldGuardMask = 0UL;
            _migrationFieldGuardVault = null;
            _migrationFieldBuffersLocked = false;
            if (vault != null && guardMask != 0UL)
                vault.ReleaseMutationGuard(guardMask);
        }

        private static ulong MigrationFieldGuardBit(BufferID bufferId)
        {
            return 1UL << ((int)bufferId & 31);
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            if (TryAbortForUsableExistingRuntime())
                return;

            GlobalRegistry.RegisterMigrationDirectorRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.Migration, this);
            if (_serviceRegistered)
            {
                s_activeRuntime = this;
                _migrationVault = GlobalRegistry.DataVault;
                _faunaWorldSeedReadModel = GlobalRegistry.FaunaWorldSeed;
                _ambientCurrentReadModel = GlobalRegistry.AmbientCurrent;
                _celestialEngine = GlobalRegistry.CelestialEngine;
                WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref _mapMagicVegetationBridge);
            }
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            MigrationDirector active = s_activeRuntime;
            if (!ReferenceEquals(active, null) && !ReferenceEquals(active, this))
            {
                if (IsMigrationDirectorRuntimeUsable(active))
                {
                    SuppressDuplicateService();
                    return true;
                }

                if (ReferenceEquals(s_activeRuntime, active))
                    s_activeRuntime = null;

                if (ReferenceEquals(GlobalRegistry.Migration, active))
                    GlobalRegistry.UnregisterMigrationDirectorRuntime(active);
            }

            MigrationDirector registered = GlobalRegistry.Migration;
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsMigrationDirectorRuntimeUsable(registered))
            {
                s_activeRuntime = registered;
                SuppressDuplicateService();
                return true;
            }

            if (ReferenceEquals(s_activeRuntime, registered))
                s_activeRuntime = null;

            GlobalRegistry.UnregisterMigrationDirectorRuntime(registered);
            return false;
        }

        private static MigrationDirector ResolveActiveRuntime()
        {
            MigrationDirector active = s_activeRuntime;
            if (IsMigrationDirectorRuntimeUsable(active))
                return active;

            if (!ReferenceEquals(active, null))
            {
                if (ReferenceEquals(s_activeRuntime, active))
                    s_activeRuntime = null;

                if (ReferenceEquals(GlobalRegistry.Migration, active))
                    GlobalRegistry.UnregisterMigrationDirectorRuntime(active);
            }

            MigrationDirector registered = GlobalRegistry.Migration;
            if (IsMigrationDirectorRuntimeUsable(registered))
            {
                s_activeRuntime = registered;
                return registered;
            }

            if (!ReferenceEquals(registered, null))
            {
                if (ReferenceEquals(s_activeRuntime, registered))
                    s_activeRuntime = null;

                GlobalRegistry.UnregisterMigrationDirectorRuntime(registered);
            }

            return null;
        }

        private static bool IsMigrationDirectorRuntimeUsable(MigrationDirector director)
        {
            return director != null &&
                   director._serviceRegistered &&
                   !director._duplicateServiceSuppressed &&
                   director.isActiveAndEnabled;
        }

        private void SuppressDuplicateService()
        {
            _duplicateServiceSuppressed = true;
            _serviceRegistered = false;
            _registeredToTick = false;
            _registeredLateFrameTick = false;
            enabled = false;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterMigrationDirectorRuntime(this);
            _serviceRegistered = false;
            if (ReferenceEquals(s_activeRuntime, this))
                s_activeRuntime = null;
            _faunaWorldSeedReadModel = null;
            _ambientCurrentReadModel = null;
            _celestialEngine = null;
            _mapMagicVegetationBridge = null;
        }

        private void TryRegisterToTickManager()
        {
            if (_registeredToTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredToTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToTick = false;
        }

        private void TryRegisterLateFrameTickManager()
        {
            if (_registeredLateFrameTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void UnregisterLateFrameTickManager()
        {
            if (!_registeredLateFrameTick)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrameTick = false;
        }

        private static ReadOnlySpan<char> AsSpanOrEmpty(string value)
        {
            return value != null ? value.AsSpan() : ReadOnlySpan<char>.Empty;
        }

        private static uint HashString(ReadOnlySpan<char> value)
        {
            unchecked
            {
                if (value.Length == 0)
                    return 0x811C9DC5u;

                uint hash = 0x811C9DC5u;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }

                return hash;
            }
        }

        private static uint Hash(uint value)
        {
            unchecked
            {
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return value;
            }
        }

        private static uint HashInt3(int3 value)
        {
            unchecked
            {
                uint hash = 0x811C9DC5u;
                hash = Hash(hash ^ (uint)value.x);
                hash = Hash(hash ^ (uint)value.y);
                hash = Hash(hash ^ (uint)value.z);
                return hash;
            }
        }

        private static float Hash01(uint value)
        {
            return (Hash(value) & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private static int FastFloorToInt(float value)
        {
            if (!math.isfinite(value))
                return 0;

            if (value <= int.MinValue)
                return int.MinValue;

            if (value >= int.MaxValue)
                return int.MaxValue;

            int integer = (int)value;
            return value < integer ? integer - 1 : integer;
        }

        private static int FastFloorToInt(double value)
        {
            if (!math.isfinite(value))
                return 0;

            if (value <= int.MinValue)
                return int.MinValue;

            if (value >= int.MaxValue)
                return int.MaxValue;

            int integer = (int)value;
            return value < integer ? integer - 1 : integer;
        }

        private static int WrapIndex(int value, int length)
        {
            if (length <= 0)
                return 0;

            int wrapped = value % length;
            return wrapped < 0 ? wrapped + length : wrapped;
        }

        private static int BuildMigrationCellIndex(int x, int y, int z, int3 resolution)
        {
            return (y * resolution.z + z) * resolution.x + x;
        }

        private static bool AreSameAupCell(in int3 a, in int3 b)
        {
            return a.x == b.x && a.y == b.y && a.z == b.z;
        }

        private float ResolveMigrationFieldColdTickIntervalSeconds(float globalQualityWeight)
        {
            float quality = Smooth01(globalQualityWeight);
            float baseIntervalSeconds = SanitizePositiveFinite(migrationFieldColdTickIntervalSeconds, 1f);
            return baseIntervalSeconds * math.lerp(2.4f, 0.2f, quality);
        }

        private static float ResolveMigrationQualityWeight()
        {
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, AuthoritativeQualityWeight);

            return MathLodApproximation.SaturateFinite(HomeostasisBrain.GlobalQualityWeight, AuthoritativeQualityWeight);
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - 2f * x);
        }

        private static float ResolveDispatcherRuntimeSeconds()
        {
            double now = SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (double.IsNaN(now) || double.IsInfinity(now) || now <= 0d)
                return 0f;

            return now >= float.MaxValue ? float.MaxValue : (float)now;
        }

        private float AdvanceColdTickDeltaSeconds(float runtimeSeconds, float maxDeltaSeconds)
        {
            if (_lastColdTickRuntimeSeconds < 0f)
            {
                _lastColdTickRuntimeSeconds = runtimeSeconds;
                return 0f;
            }

            float deltaSeconds = runtimeSeconds - _lastColdTickRuntimeSeconds;
            _lastColdTickRuntimeSeconds = runtimeSeconds;
            if (!math.isfinite(deltaSeconds) || deltaSeconds <= 0f)
                return 0f;

            float safeMaxDeltaSeconds = math.max(0.1f, SanitizeFinite(maxDeltaSeconds, 0.1f));
            return math.min(deltaSeconds, safeMaxDeltaSeconds);
        }

        private int3 ResolveMigrationAupCell(Vector3 origin)
        {
            double safeCellSize = ResolveSafeMigrationCellSizeMeters();
            return ResolveMigrationAupCell(origin, safeCellSize);
        }

        private static int3 ResolveMigrationAupCell(Vector3 origin, double safeCellSize)
        {
            double3 originAupMeters = ResolveAupMeters(origin);
            return new int3(
                FastFloorToInt(originAupMeters.x / safeCellSize),
                FastFloorToInt(originAupMeters.y / safeCellSize),
                FastFloorToInt(originAupMeters.z / safeCellSize));
        }

        private float3 ResolveWrappedMigrationFieldPoint(Vector3 runtimePosition)
        {
            double safeCellSize = ResolveSafeMigrationCellSizeMeters();
            double3 aupMeters = ResolveAupMeters(runtimePosition);
            double extentX = safeCellSize * Mathf.Max(1, migrationGridResolutionX);
            double extentY = safeCellSize * Mathf.Max(1, migrationGridResolutionY);
            double extentZ = safeCellSize * Mathf.Max(1, migrationGridResolutionZ);
            double3 gridOriginAupMeters = ResolveSafeMigrationGridOriginAupMeters();
            double originX = gridOriginAupMeters.x;
            double originY = gridOriginAupMeters.y;
            double originZ = gridOriginAupMeters.z;
            float3 local = new float3(
                (float)WrapCoordinateToExtent(aupMeters.x, originX, extentX),
                (float)ClampCoordinate(aupMeters.y - originY, 0d, extentY),
                (float)WrapCoordinateToExtent(aupMeters.z, originZ, extentZ));
            return math.all(math.isfinite(local)) ? local : float3.zero;
        }

        private float ResolveSafeMigrationCellSizeMeters()
        {
            return SanitizePositiveFinite(migrationCellSizeMeters, GlobalMigrationCellSizeMeters);
        }

        private double3 ResolveSafeMigrationGridOriginAupMeters()
        {
            double3 originAupMeters = new double3(
                migrationGridOriginAupLocal.x,
                migrationGridOriginAupLocal.y,
                migrationGridOriginAupLocal.z);
            return math.all(math.isfinite(originAupMeters)) ? originAupMeters : double3.zero;
        }

        private static double3 ResolveAupMeters(Vector3 runtimePosition)
        {
            return TryResolveAupMeters(runtimePosition, out double3 aupMeters) ? aupMeters : double3.zero;
        }

        private static bool TryResolveAupMeters(Vector3 runtimePosition, out double3 aupMeters)
        {
            aupMeters = default;
            if (!TryResolveRuntimeAup(runtimePosition, out AbsoluteUniversePosition aup))
                return false;

            aupMeters = new double3(
                aup.GridX * (double)AbsoluteUniversePosition.CellSizeMeters + aup.LocalX,
                aup.GridY * (double)AbsoluteUniversePosition.CellSizeMeters + aup.LocalY,
                aup.GridZ * (double)AbsoluteUniversePosition.CellSizeMeters + aup.LocalZ);
            return math.all(math.isfinite(aupMeters));
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        private static double WrapCoordinateToExtent(double value, double origin, double extent)
        {
            if (!math.isfinite(value) || !math.isfinite(origin) || !math.isfinite(extent) || extent <= 0d)
                return 0d;

            double local = value - origin;
            double wrapped = local - math.floor(local / extent) * extent;
            return math.isfinite(wrapped) ? wrapped : 0d;
        }

        private static double ClampCoordinate(double value, double min, double max)
        {
            if (!math.isfinite(value) || !math.isfinite(min) || !math.isfinite(max))
                return 0d;

            if (value < min)
                return min;

            return value > max ? max : value;
        }

        private static bool IsVrSwarmScalingActive()
        {
            return HectonXRRuntimeState.IsXRActive;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct BuildMigrationVectorFieldJob : IJobParallelFor
        {
            [WriteOnly, NoAlias] public NativeArray<MigrationGridCell> Output;
            [ReadOnly, NoAlias] public NativeArray<MigrationBloodCloudPoi> BloodCloudPois;
            public int3 Resolution;
            public float CellSizeMeters;
            public double3 OriginAupMeters;
            public float SeasonalPhase;
            public float VerticalFlowWeight;
            public float CurrentGameTimeSeconds;
            public float GlobalQualityWeight;

            public void Execute(int index)
            {
                if (index < 0 ||
                    index >= Output.Length ||
                    Resolution.x <= 0 ||
                    Resolution.y <= 0 ||
                    Resolution.z <= 0)
                {
                    if ((uint)index < (uint)Output.Length)
                        Output[index] = default;
                    return;
                }

                long planeCellCountLong = (long)Resolution.x * Resolution.z;
                if (planeCellCountLong <= 0L || planeCellCountLong > int.MaxValue)
                {
                    Output[index] = default;
                    return;
                }

                long totalCellCountLong = planeCellCountLong * Resolution.y;
                if (totalCellCountLong <= 0L || totalCellCountLong > int.MaxValue || index >= totalCellCountLong)
                {
                    Output[index] = default;
                    return;
                }

                int planeCellCount = (int)planeCellCountLong;
                int x = index % Resolution.x;
                int z = (index / Resolution.x) % Resolution.z;
                int y = index / planeCellCount;
                float safeCellSize = SanitizePositiveFinite(CellSizeMeters, GlobalMigrationCellSizeMeters);
                float quality = Smooth01(Sanitize01Finite(GlobalQualityWeight));
                int poiStride = (int)math.clamp(math.round(math.lerp(4f, 1f, quality)), 1f, 4f);
                float safeSeasonalPhase = SanitizeFinite(SeasonalPhase, 0f);
                float safeVerticalFlowWeight = math.clamp(SanitizeFinite(VerticalFlowWeight, 0f), 0f, 0.35f);
                float safeGameTimeSeconds = math.max(0f, SanitizeFinite(CurrentGameTimeSeconds, 0f));
                double3 safeOriginAupMeters = math.all(math.isfinite(OriginAupMeters)) ? OriginAupMeters : double3.zero;

                float invX = Resolution.x > 0 ? 1f / Resolution.x : 0f;
                float invY = Resolution.y > 1 ? 1f / (Resolution.y - 1) : 0f;
                float invZ = Resolution.z > 0 ? 1f / Resolution.z : 0f;
                float u = x * invX;
                float v = z * invZ;
                float depth01 = y * invY;
                float seamAngleX = u * Tau;
                float seamAngleZ = v * Tau;
                float seasonalAngle = safeSeasonalPhase +
                    MathLodApproximation.ApproxSinBhaskara(seamAngleX) * 0.85f +
                    MathLodApproximation.ApproxCosBhaskara(seamAngleZ) * 0.65f +
                    depth01 * 0.72f;

                double3 cellAbsolute = safeOriginAupMeters + new double3(
                    (x + 0.5f) * safeCellSize,
                    (y + 0.5f) * safeCellSize,
                    (z + 0.5f) * safeCellSize);
                float3 cellPosition = new float3(
                    (x + 0.5f) * safeCellSize,
                    (y + 0.5f) * safeCellSize,
                    (z + 0.5f) * safeCellSize);

                float3 baseDirection = new float3(
                    MathLodApproximation.ApproxCosBhaskara(seasonalAngle) + MathLodApproximation.ApproxSinBhaskara(seamAngleZ + safeSeasonalPhase * 0.37f) * 0.23f,
                    MathLodApproximation.ApproxSinBhaskara(seasonalAngle * 0.53f + depth01 * Tau) * safeVerticalFlowWeight,
                    MathLodApproximation.ApproxSinBhaskara(seasonalAngle) + MathLodApproximation.ApproxCosBhaskara(seamAngleX - safeSeasonalPhase * 0.41f) * 0.23f);
                if (!math.all(math.isfinite(baseDirection)))
                    baseDirection = new float3(0f, 0f, 1f);

                float3 attraction = float3.zero;
                float attractionWeight = 0f;
                int poiStart = index % poiStride;
                for (int i = poiStart; i < BloodCloudPois.Length; i += poiStride)
                {
                    MigrationBloodCloudPoi poi = BloodCloudPois[i];
                    if (poi.Flags == 0 ||
                        !math.isfinite(poi.ExpireGameTimeSeconds) ||
                        safeGameTimeSeconds >= poi.ExpireGameTimeSeconds ||
                        !math.all(math.isfinite(poi.PositionFieldLocalMeters)) ||
                        !math.isfinite(poi.RadiusMeters) ||
                        !math.isfinite(poi.Strength))
                    {
                        continue;
                    }

                    float radius = SanitizePositiveFinite(poi.RadiusMeters, 1f);
                    float invRadius = 1f / radius;
                    float invRadiusSq = invRadius * invRadius;
                    float radiusSq = radius * radius;
                    float3 toPoi = poi.PositionFieldLocalMeters - cellPosition;
                    toPoi.x = WrapDeltaToExtent(toPoi.x, safeCellSize * math.max(1, Resolution.x));
                    toPoi.z = WrapDeltaToExtent(toPoi.z, safeCellSize * math.max(1, Resolution.z));
                    float distSq = math.lengthsq(toPoi);
                    if (!math.all(math.isfinite(toPoi)) ||
                        !math.isfinite(distSq) ||
                        distSq > radiusSq)
                    {
                        continue;
                    }

                    float falloff = math.saturate(1f - distSq * invRadiusSq);
                    float strength = math.max(0f, SanitizeFinite(poi.Strength, 0f)) * falloff * falloff * math.lerp(0.55f, 1.35f, quality);
                    if (!math.isfinite(strength))
                        continue;

                    attraction += toPoi * (strength * invRadius);
                    attractionWeight += strength;
                }

                float3 direction = NormalizeOrFallback(baseDirection + attraction, new float3(0f, 0f, 1f));
                MigrationGridCell cell = default;
                cell.AupCell = new int3(
                    FastFloorToInt(cellAbsolute.x / safeCellSize),
                    FastFloorToInt(cellAbsolute.y / safeCellSize),
                    FastFloorToInt(cellAbsolute.z / safeCellSize));
                cell.Direction = direction;
                cell.Magnitude = math.saturate(1f + attractionWeight * math.lerp(0.06f, 0.22f, quality));
                cell.PopulationCount = 0;
                cell.Flags = (ushort)(attractionWeight > 0.0001f ? MigrationCellFlagBloodCloud : 0);
                Output[index] = cell;
            }

            private static float WrapDeltaToExtent(float delta, float extent)
            {
                if (!math.isfinite(delta))
                    return 0f;

                if (!math.isfinite(extent) || extent <= 0f)
                    return delta;

                return delta - math.round(delta / extent) * extent;
            }

            private static float3 NormalizeOrFallback(float3 value, float3 fallback)
            {
                float lengthSq = math.lengthsq(value);
                return math.all(math.isfinite(value)) && math.isfinite(lengthSq) && lengthSq > 0.0001f
                    ? value * math.rsqrt(lengthSq)
                    : fallback;
            }

            private static float Smooth01(float value)
            {
                float x = math.saturate(value);
                return x * x * (3f - 2f * x);
            }

            private static float SanitizeFinite(float value, float fallback)
            {
                return math.isfinite(value) ? value : fallback;
            }

            private static float Sanitize01Finite(float value)
            {
                return math.saturate(SanitizeFinite(value, 0f));
            }

            private static float SanitizePositiveFinite(float value, float fallback)
            {
                return math.max(fallback, SanitizeFinite(value, fallback));
            }

            private static int FastFloorToInt(double value)
            {
                if (!math.isfinite(value))
                    return 0;

                if (value <= int.MinValue)
                    return int.MinValue;

                if (value >= int.MaxValue)
                    return int.MaxValue;

                return (int)math.floor(value);
            }
        }
    }
}
