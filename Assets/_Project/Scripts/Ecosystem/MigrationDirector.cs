using System.Runtime.InteropServices;
using Hecton8.AI;
using Hecton8.Atmosphere;
using Hecton8.Celestial;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.PDA;
using Hecton8.Physics;
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
    public sealed class MigrationDirector : MonoBehaviour, ISlowTickable, ILateFrameTickable, IServiceHeartbeat, IServiceShutdown
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
        private const string NativeMemoryOwner = nameof(MigrationDirector);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;
        private const SystemID NativeMemorySystemId = SystemID.AIEcology;

        private bool _registeredToTick;
        private bool _registeredLateFrameTick;
        private int _currentDayIndex = 1;
        private int3 _migrationGridResolution;
        private int _migrationGridCellCount;
        private float _coldTickAccumulator;
        private float _lastColdTickRuntimeSeconds = -1f;
        private float _debugLastSeasonalPhase;
        private float _debugLastMigrationGridMagnitude;
        private int _debugBloodCloudPoiCount;
        private int _debugMigrationGridCellCount;
        private int _debugStatisticalSwarmSlotCount;
        private bool _debugVrSwarmScalingActive;
        private JobHandle _migrationFieldHandle;
        private bool _migrationFieldScheduled;
        private int _pendingBloodCloudPoiWriteCount;
        private bool _serviceRegistered;
        private bool _duplicateServiceSuppressed;

        private NativeArray<MigrationGridCell> _migrationGridFront;
        private NativeArray<MigrationGridCell> _migrationGridBack;
        private NativeArray<MigrationBloodCloudPoi> _bloodCloudPois;
        private NativeArray<MigrationSwarmState> _migrationSwarmStates;
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
            public float3 PositionFieldAupMeters;
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
            public ushort PopulationCount;
            [FieldOffset(34)]
            public ushort SpeciesId;
            [FieldOffset(36)]
            public uint Flags;
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
            _migrationGridFront.IsCreated &&
            _migrationGridBack.IsCreated &&
            _bloodCloudPois.IsCreated &&
            _migrationSwarmStates.IsCreated;

        private void Awake()
        {
            MigrationDirector registered = GlobalRegistry.Migration;
            if (registered != null && registered != this)
            {
                SuppressDuplicateService();
                return;
            }

            SanitizeMigrationSettings();
            AllocateMigrationNativeState();
            RefreshCurrentDay();
        }

        private void OnEnable()
        {
            if (_duplicateServiceSuppressed)
                return;

            TryRegisterService();
            if (Application.isPlaying && (!_serviceRegistered || _duplicateServiceSuppressed))
                return;

            TryRegisterToTickManager();
            TryRegisterLateFrameTickManager();
            AllocateMigrationNativeState();
            RefreshCurrentDay();
        }

        private void Start()
        {
            if (_duplicateServiceSuppressed || (Application.isPlaying && !_serviceRegistered))
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
            DisposeMigrationNativeState();
            TryUnregisterService();
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
            MigrationDirector runtime = GlobalRegistry.Migration;
            return runtime != null ? runtime.ResolveSelectionMultiplierInternal(biomeIndex, archetype) : 1f;
        }

        /// <summary>
        /// Resolves visible boid count from the O(1) swarm population state and platform-specific scaling.
        /// </summary>
        public static int ResolveVisibleBoidCount(int speciesId, Vector3 origin, int requestedPopulationCount)
        {
            MigrationDirector runtime = GlobalRegistry.Migration;
            return runtime != null
                ? runtime.ResolveVisibleBoidCountInternal(speciesId, origin, requestedPopulationCount)
                : ResolveVisibleBoidCountFromMigrationPopulationStatic(requestedPopulationCount);
        }

        /// <summary>
        /// Refreshes an already-dematerialized statistical swarm population point without materialising boids.
        /// </summary>
        public static void RegisterStatisticalSwarmPopulation(int speciesId, Vector3 origin, int populationCount)
        {
            MigrationDirector runtime = GlobalRegistry.Migration;
            if (runtime != null)
                runtime.RegisterStatisticalSwarmPopulationInternal(speciesId, origin, populationCount);
        }

        /// <summary>
        /// Resolves VAT sway amplitude compensation for VR swarm downscaling.
        /// </summary>
        public static float ResolveVatSwayAmplitudeScale()
        {
            MigrationDirector runtime = GlobalRegistry.Migration;
            return runtime != null ? runtime.ResolveVatSwayAmplitudeScaleInternal() : 1f;
        }

        /// <summary>
        /// Registers one apex kill as a blood-cloud migration POI and one-hour whale-fall density source.
        /// </summary>
        public static void RegisterPredatorKillPoi(uint uniqueInstanceUid, Vector3 worldPosition, float fallbackRuntimeSeconds)
        {
            MigrationDirector runtime = GlobalRegistry.Migration;
            if (runtime != null)
                runtime.RegisterPredatorKillPoiInternal(uniqueInstanceUid, worldPosition, fallbackRuntimeSeconds);
        }

        internal static bool TryResolveMigrationTarget(int speciesId, Vector3 origin, out Vector3 target)
        {
            target = origin;
            MigrationDirector runtime = GlobalRegistry.Migration;
            return runtime != null && runtime.TryResolveMigrationTargetInternal(speciesId, origin, out target);
        }

        internal static int RegisterStatisticalSwarmPopulationAndResolveCount(int speciesId, Vector3 origin, int populationCount)
        {
            MigrationDirector runtime = GlobalRegistry.Migration;
            return runtime != null
                ? runtime.RegisterStatisticalSwarmPopulationInternal(speciesId, origin, populationCount)
                : Mathf.Max(0, populationCount);
        }

        internal static int ResolveVisibleBoidCountFromMigrationPopulation(int migrationPopulationCount)
        {
            MigrationDirector runtime = GlobalRegistry.Migration;
            return runtime != null
                ? runtime.ResolveVisibleBoidCountFromMigrationPopulationInternal(migrationPopulationCount)
                : ResolveVisibleBoidCountFromMigrationPopulationStatic(migrationPopulationCount);
        }

        internal static int3 ResolveMigrationPopulationAupCell(Vector3 origin)
        {
            MigrationDirector runtime = GlobalRegistry.Migration;
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

            FaunaGeneticsManager geneticsManager = GlobalRegistry.FaunaGenetics;
            int worldSeed = geneticsManager != null ? geneticsManager.WorldSeed : 0;
            uint hash = Hash((uint)worldSeed ^ (uint)_currentDayIndex * 0x9E3779B9u ^ (uint)biomeIndex * 0x85EBCA6Bu ^ HashString(archetype.creatureId));
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

        private float ResolveCurrentDrivenBias(int biomeIndex, CreatureArchetypeData archetype, uint hash)
        {
            Vector3 probePosition = ResolveMigrationProbePosition(biomeIndex, hash);
            Vector3 currentVector = CurrentVolume.SampleCombinedCurrent(probePosition);
            currentVector.y = 0f;
            float sqrMagnitude = currentVector.sqrMagnitude;
            if (sqrMagnitude <= 0.0001f)
                return 1f;

            currentVector *= math.rsqrt(sqrMagnitude);

            float preferredHeadingRadians = Hash01(hash ^ 0xB5297A4Du) * Tau;
            Vector3 preferredHeading = new Vector3(
                math.cos(preferredHeadingRadians),
                0f,
                math.sin(preferredHeadingRadians));
            float alignment01 = math.saturate((Vector3.Dot(currentVector, preferredHeading) + 1f) * 0.5f);

            float roleBlend = archetype.roleType == CreatureRoleType.Ambient ? 0.82f : 0.64f;
            float weightedAlignment = math.lerp(0.5f, alignment01, roleBlend);
            return math.lerp(0.7f, 1.45f, weightedAlignment);
        }

        private bool TryResolveMigrationTargetInternal(int speciesId, Vector3 origin, out Vector3 target)
        {
            target = origin;
            float sampledTemperature = ResolveWaterTemperature(origin);
            Vector3 currentVector = CurrentVolume.SampleCombinedCurrent(origin);
            currentVector.y = 0f;

            EcosystemMigrationProfile.TemperatureRoute route = default;
            bool hasRoute = migrationProfile != null && migrationProfile.TryResolveRoute(sampledTemperature, out route);
            float routeDistance = hasRoute ? Mathf.Max(1f, route.migrationDistanceMeters) : Mathf.Max(1f, fallbackMigrationDistanceMeters);
            float currentAlignmentWeight = hasRoute ? Mathf.Clamp01(route.currentAlignmentWeight) : Mathf.Clamp01(fallbackCurrentAlignmentWeight);
            float depthBiasMeters = hasRoute ? route.depthBiasMeters : 0f;

            int3 originAupCell = ResolveMigrationAupCell(origin);
            uint seed = Hash((uint)speciesId ^ (uint)_currentDayIndex * 0x9E3779B9u ^ HashInt3(originAupCell));
            Vector3 preferredDirection = hasRoute
                ? ResolvePreferredDirection(route.preferredPlanarDirection, seed)
                : ResolvePreferredDirection(Vector2.zero, seed);
            Vector3 migrationDirection = BlendRouteWithCurrent(preferredDirection, currentVector, currentAlignmentWeight);
            if (TrySampleMigrationFieldDirection(origin, out Vector3 gridDirection))
            {
                migrationDirection = BlendDirectionsLinear(migrationDirection, gridDirection, migrationFlowAlignmentWeight);

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
            WriteSwarmPopulationState(speciesId, origin, migrationPopulationCount);
            return ResolveVisibleBoidCountFromMigrationPopulationInternal(migrationPopulationCount);
        }

        private int RegisterStatisticalSwarmPopulationInternal(int speciesId, Vector3 origin, int populationCount)
        {
            if (populationCount <= 0)
            {
                if (_migrationSwarmStates.IsCreated)
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
            _debugVrSwarmScalingActive = vrActive;
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
            _debugVrSwarmScalingActive = vrActive;
            return vrActive ? Mathf.Max(1f, vrVatSwayAmplitudeScale) : 1f;
        }

        private void RegisterPredatorKillPoiInternal(uint uniqueInstanceUid, Vector3 worldPosition, float fallbackRuntimeSeconds)
        {
            AllocateMigrationNativeState();
            if (!_bloodCloudPois.IsCreated || _bloodCloudPois.Length == 0)
                return;

            float gameTimeSeconds = ResolveTimelineGameSeconds(fallbackRuntimeSeconds);
            PruneExpiredMigrationSwarmStates(gameTimeSeconds);
            MigrationBloodCloudPoi poi = BuildBloodCloudPoi(uniqueInstanceUid, worldPosition, gameTimeSeconds);
            if (_migrationFieldScheduled)
            {
                EnqueuePendingBloodCloudPoiWrite(in poi, gameTimeSeconds);
                _debugBloodCloudPoiCount = CountActiveBloodCloudPois(gameTimeSeconds);
                return;
            }

            WriteBloodCloudPoiToNative(in poi, gameTimeSeconds);
            RequestMigrationFieldRebuildSoon();
        }

        private MigrationBloodCloudPoi BuildBloodCloudPoi(uint uniqueInstanceUid, Vector3 worldPosition, float gameTimeSeconds)
        {
            MigrationBloodCloudPoi poi = default;
            poi.PositionAup = AbsoluteUniversePosition.FromRuntimePosition(worldPosition).ToAlignedBlit();
            poi.PositionFieldAupMeters = ResolveWrappedMigrationFieldPoint(worldPosition);
            poi.RadiusMeters = Mathf.Max(10f, bloodCloudPoiRadiusMeters);
            poi.Strength = Mathf.Max(0f, bloodCloudPoiStrength);
            poi.ExpireGameTimeSeconds = gameTimeSeconds + BloodCloudPoiLifetimeGameSeconds;
            poi.SourceId = unchecked((int)(uniqueInstanceUid & 0x7FFFFFFFu));
            poi.Flags = 1;
            return poi;
        }

        private void WriteBloodCloudPoiToNative(in MigrationBloodCloudPoi poi, float gameTimeSeconds)
        {
            if (!_bloodCloudPois.IsCreated || _bloodCloudPois.Length == 0)
                return;

            int sourceId = poi.SourceId;
            int capacity = math.min(_bloodCloudPois.Length, _bloodCloudPoiMirror.Length);
            int selectedSlot = -1;
            int earliestSlot = 0;
            float earliestExpiry = float.MaxValue;
            for (int i = 0; i < capacity; i++)
            {
                MigrationBloodCloudPoi candidate = _bloodCloudPoiMirror[i];
                if (candidate.Flags != 0 && candidate.SourceId == sourceId)
                {
                    selectedSlot = i;
                    break;
                }

                if (candidate.Flags == 0 || gameTimeSeconds >= candidate.ExpireGameTimeSeconds)
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

            _bloodCloudPois[selectedSlot] = poi;
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
                if (candidate.Flags != 0 && candidate.SourceId == poi.SourceId)
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

            bool wroteAny = false;
            for (int i = 0; i < _pendingBloodCloudPoiWriteCount; i++)
            {
                MigrationBloodCloudPoi poi = _pendingBloodCloudPoiWrites[i];
                if (poi.Flags != 0)
                {
                    WriteBloodCloudPoiToNative(in poi, gameTimeSeconds);
                    wroteAny = true;
                }

                _pendingBloodCloudPoiWrites[i] = default;
            }

            _pendingBloodCloudPoiWriteCount = 0;
            return wroteAny;
        }

        private void RequestMigrationFieldRebuildSoon()
        {
            _coldTickAccumulator = Mathf.Max(_coldTickAccumulator, migrationFieldColdTickIntervalSeconds);
        }

        private void CompactPendingBloodCloudPoiWrites(float gameTimeSeconds)
        {
            if (_pendingBloodCloudPoiWriteCount <= 0)
                return;

            int writeIndex = 0;
            for (int readIndex = 0; readIndex < _pendingBloodCloudPoiWriteCount; readIndex++)
            {
                MigrationBloodCloudPoi poi = _pendingBloodCloudPoiWrites[readIndex];
                if (poi.Flags == 0 || gameTimeSeconds >= poi.ExpireGameTimeSeconds)
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
            if (!_migrationGridFront.IsCreated || !_migrationGridBack.IsCreated || _migrationFieldScheduled)
                return;

            _coldTickAccumulator += ResolveColdTickDeltaSeconds(Time.unscaledTime);
            if (_coldTickAccumulator < migrationFieldColdTickIntervalSeconds)
                return;

            _coldTickAccumulator = 0f;
            ScheduleMigrationFieldBuild();
        }

        private void ScheduleMigrationFieldBuild()
        {
            if (!_migrationGridBack.IsCreated || _migrationGridCellCount <= 0)
                return;

            float timelineSeconds = ResolveTimelineGameSeconds(Time.time);
            FlushPendingBloodCloudPoiWrites(timelineSeconds);
            PruneExpiredMigrationSwarmStates(timelineSeconds);
            float seasonalPhase = ResolveSeasonalPhase(timelineSeconds);
            _debugLastSeasonalPhase = seasonalPhase;
            _debugBloodCloudPoiCount = CountActiveBloodCloudPois(timelineSeconds);

            BuildMigrationVectorFieldJob job = new BuildMigrationVectorFieldJob
            {
                Output = _migrationGridBack,
                BloodCloudPois = _bloodCloudPois,
                Resolution = _migrationGridResolution,
                CellSizeMeters = Mathf.Max(1f, migrationCellSizeMeters),
                OriginAupLocal = new float3(migrationGridOriginAupLocal.x, migrationGridOriginAupLocal.y, migrationGridOriginAupLocal.z),
                SeasonalPhase = seasonalPhase,
                VerticalFlowWeight = Mathf.Clamp01(migrationVerticalFlowWeight),
                CurrentGameTimeSeconds = timelineSeconds
            };

            _migrationFieldHandle = job.Schedule(_migrationGridCellCount, 64);
            H8Memory.RegisterActiveJob(NativeMemorySystemId, _migrationFieldHandle);
            _migrationFieldScheduled = true;
        }

        private void CompleteMigrationFieldJob(bool forceComplete)
        {
            if (!_migrationFieldScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _migrationFieldHandle, forceComplete))
                return;

            NativeArray<MigrationGridCell> swap = _migrationGridFront;
            _migrationGridFront = _migrationGridBack;
            _migrationGridBack = swap;
            _migrationFieldScheduled = false;
            if (_migrationGridFront.IsCreated && _migrationGridFront.Length > 0)
            {
                MigrationGridCell firstCell = _migrationGridFront[0];
                _debugLastMigrationGridDirection = new Vector3(firstCell.Direction.x, firstCell.Direction.y, firstCell.Direction.z);
            }
            float gameTimeSeconds = ResolveTimelineGameSeconds(Time.time);
            PruneExpiredMigrationSwarmStates(gameTimeSeconds);
            ApplyMigrationSwarmPopulationCountsToFrontGrid();
            if (FlushPendingBloodCloudPoiWrites(gameTimeSeconds))
                RequestMigrationFieldRebuildSoon();
        }

        private bool TrySampleMigrationFieldDirection(Vector3 origin, out Vector3 direction)
        {
            direction = Vector3.zero;
            if (!_migrationGridFront.IsCreated || _migrationGridFront.Length != _migrationGridCellCount || _migrationGridCellCount <= 0)
                return false;

            double safeCellSize = Mathf.Max(1f, migrationCellSizeMeters);
            double3 originAupMeters = ResolveAupMeters(origin);
            double gridX = ((originAupMeters.x - migrationGridOriginAupLocal.x) / safeCellSize) - 0.5d;
            double gridY = ((originAupMeters.y - migrationGridOriginAupLocal.y) / safeCellSize) - 0.5d;
            double gridZ = ((originAupMeters.z - migrationGridOriginAupLocal.z) / safeCellSize) - 0.5d;
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

            float3 c000 = _migrationGridFront[BuildMigrationCellIndex(ix0, iy0, iz0, _migrationGridResolution)].Direction;
            float3 c100 = _migrationGridFront[BuildMigrationCellIndex(ix1, iy0, iz0, _migrationGridResolution)].Direction;
            float3 c010 = _migrationGridFront[BuildMigrationCellIndex(ix0, iy1, iz0, _migrationGridResolution)].Direction;
            float3 c110 = _migrationGridFront[BuildMigrationCellIndex(ix1, iy1, iz0, _migrationGridResolution)].Direction;
            float3 c001 = _migrationGridFront[BuildMigrationCellIndex(ix0, iy0, iz1, _migrationGridResolution)].Direction;
            float3 c101 = _migrationGridFront[BuildMigrationCellIndex(ix1, iy0, iz1, _migrationGridResolution)].Direction;
            float3 c011 = _migrationGridFront[BuildMigrationCellIndex(ix0, iy1, iz1, _migrationGridResolution)].Direction;
            float3 c111 = _migrationGridFront[BuildMigrationCellIndex(ix1, iy1, iz1, _migrationGridResolution)].Direction;
            float3 c00 = math.lerp(c000, c100, tx);
            float3 c10 = math.lerp(c010, c110, tx);
            float3 c01 = math.lerp(c001, c101, tx);
            float3 c11 = math.lerp(c011, c111, tx);
            float3 c0 = math.lerp(c00, c10, ty);
            float3 c1 = math.lerp(c01, c11, ty);
            float3 sampled = math.lerp(c0, c1, tz);
            float sampledSq = math.lengthsq(sampled);
            if (sampledSq <= 0.0001f)
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
            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromRuntimePosition(origin);
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
            if (poi.Flags == 0 || gameTimeSeconds >= poi.ExpireGameTimeSeconds)
                return currentMultiplier;

            double radius = math.max(1f, poi.RadiusMeters);
            double radiusSq = radius * radius;
            double invRadiusSq = 1d / radiusSq;
            AbsoluteUniversePosition poiAup = AbsoluteUniversePosition.FromAlignedBlit(in poi.PositionAup);
            double distSq = AbsoluteUniversePosition.DistanceSq(in originAup, in poiAup);
            if (distSq > radiusSq)
                return currentMultiplier;

            float falloff = math.saturate((float)(1d - distSq * invRadiusSq));
            float candidateMultiplier = math.lerp(1f, math.max(1f, whaleFallScavengerPopulationMultiplier), falloff * falloff);
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
            if (!_migrationSwarmStates.IsCreated || _migrationSwarmStates.Length == 0)
                return;

            float gameTimeSeconds = ResolveTimelineGameSeconds(Time.time);
            PruneExpiredMigrationSwarmStates(gameTimeSeconds);
            int3 aupCell = ResolveMigrationAupCell(origin);
            uint hash = Hash((uint)speciesId ^ HashInt3(aupCell));
            int safePopulationCount = Mathf.Clamp(populationCount, 0, ushort.MaxValue);
            if (safePopulationCount <= 0)
            {
                if (!TryFindMigrationSwarmStateSlot(hash, speciesId, in aupCell, out int clearSlot))
                    return;

                MigrationSwarmState clearedState = _migrationSwarmStates[clearSlot];
                _migrationSwarmStates[clearSlot] = default;
                RecomputeMigrationGridPopulationCell(in clearedState.AupCell);
                RefreshDebugStatisticalSwarmSlotCount();
                return;
            }

            int slot = ResolveMigrationSwarmStateSlot(hash, speciesId, in aupCell);
            MigrationSwarmState previousState = _migrationSwarmStates[slot];
            bool hadPreviousState = previousState.Flags != 0;

            MigrationSwarmState state = default;
            state.AupCell = aupCell;
            state.LocalPosition = new float3(origin.x, origin.y, origin.z);
            state.RadiusMeters = 200f;
            state.LastWriteGameTimeSeconds = gameTimeSeconds;
            state.PopulationCount = (ushort)safePopulationCount;
            state.SpeciesId = (ushort)Mathf.Clamp(speciesId, 0, ushort.MaxValue);
            state.Flags = 1u;
            _migrationSwarmStates[slot] = state;
            if (hadPreviousState && !AreSameAupCell(in previousState.AupCell, in state.AupCell))
                RecomputeMigrationGridPopulationCell(in previousState.AupCell);

            RecomputeMigrationGridPopulationCell(in state.AupCell);
            RefreshDebugStatisticalSwarmSlotCount();
        }

        private int ResolveMigrationSwarmStateSlot(uint hash, int speciesId, in int3 aupCell)
        {
            int capacity = _migrationSwarmStates.Length;
            int startSlot = (int)(hash % (uint)capacity);
            int emptySlot = -1;
            int oldestSlot = startSlot;
            float oldestWriteTime = float.MaxValue;
            ushort speciesKey = (ushort)Mathf.Clamp(speciesId, 0, ushort.MaxValue);
            for (int probe = 0; probe < capacity; probe++)
            {
                int slot = (startSlot + probe) % capacity;
                MigrationSwarmState candidate = _migrationSwarmStates[slot];
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

        private bool TryFindMigrationSwarmStateSlot(uint hash, int speciesId, in int3 aupCell, out int slot)
        {
            slot = -1;
            int capacity = _migrationSwarmStates.Length;
            if (capacity <= 0)
                return false;

            int startSlot = (int)(hash % (uint)capacity);
            ushort speciesKey = (ushort)Mathf.Clamp(speciesId, 0, ushort.MaxValue);
            for (int probe = 0; probe < capacity; probe++)
            {
                int candidateSlot = (startSlot + probe) % capacity;
                MigrationSwarmState candidate = _migrationSwarmStates[candidateSlot];
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
            if (!_migrationGridFront.IsCreated || !_migrationSwarmStates.IsCreated)
                return;

            for (int i = 0; i < _migrationSwarmStates.Length; i++)
            {
                MigrationSwarmState state = _migrationSwarmStates[i];
                if (state.Flags != 0 && state.PopulationCount > 0)
                    ApplyMigrationGridPopulationDelta(in state, state.PopulationCount);
            }
        }

        private void PruneExpiredMigrationSwarmStates(float gameTimeSeconds)
        {
            if (!_migrationSwarmStates.IsCreated)
            {
                _debugStatisticalSwarmSlotCount = 0;
                return;
            }

            bool changed = false;
            for (int i = 0; i < _migrationSwarmStates.Length; i++)
            {
                MigrationSwarmState state = _migrationSwarmStates[i];
                if (state.Flags == 0)
                    continue;

                if (!IsMigrationSwarmStateExpired(in state, gameTimeSeconds))
                    continue;

                _migrationSwarmStates[i] = default;
                RecomputeMigrationGridPopulationCell(in state.AupCell);
                changed = true;
            }

            if (changed)
                RefreshDebugStatisticalSwarmSlotCount();
        }

        private static bool IsMigrationSwarmStateExpired(in MigrationSwarmState state, float gameTimeSeconds)
        {
            float ageSeconds = gameTimeSeconds - state.LastWriteGameTimeSeconds;
            if (ageSeconds > MigrationSwarmStateLifetimeGameSeconds)
                return true;

            return ageSeconds < -MigrationSwarmStateFutureToleranceGameSeconds;
        }

        private void ApplyMigrationGridPopulationDelta(in MigrationSwarmState state, int populationDelta)
        {
            if (!_migrationGridFront.IsCreated ||
                _migrationGridFront.Length != _migrationGridCellCount ||
                _migrationGridCellCount <= 0 ||
                populationDelta == 0 ||
                !TryResolveMigrationGridIndexFromAupCell(in state.AupCell, out int cellIndex))
            {
                return;
            }

            MigrationGridCell cell = _migrationGridFront[cellIndex];
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

            _migrationGridFront[cellIndex] = cell;
        }

        private void RecomputeMigrationGridPopulationCell(in int3 aupCell)
        {
            if (!_migrationGridFront.IsCreated ||
                !_migrationSwarmStates.IsCreated ||
                _migrationGridFront.Length != _migrationGridCellCount ||
                _migrationGridCellCount <= 0 ||
                !TryResolveMigrationGridIndexFromAupCell(in aupCell, out int targetCellIndex))
            {
                return;
            }

            int population = 0;
            bool saturated = false;
            for (int i = 0; i < _migrationSwarmStates.Length; i++)
            {
                MigrationSwarmState state = _migrationSwarmStates[i];
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

            MigrationGridCell cell = _migrationGridFront[targetCellIndex];
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

            _migrationGridFront[targetCellIndex] = cell;
        }

        private bool TryResolveMigrationGridIndexFromAupCell(in int3 aupCell, out int cellIndex)
        {
            cellIndex = 0;
            if (_migrationGridResolution.x <= 0 || _migrationGridResolution.y <= 0 || _migrationGridResolution.z <= 0)
                return false;

            double safeCellSize = Mathf.Max(1f, migrationCellSizeMeters);
            double cellCenterX = (aupCell.x + 0.5d) * safeCellSize;
            double cellCenterY = (aupCell.y + 0.5d) * safeCellSize;
            double cellCenterZ = (aupCell.z + 0.5d) * safeCellSize;
            int ix = WrapIndex(FastFloorToInt((cellCenterX - migrationGridOriginAupLocal.x) / safeCellSize), _migrationGridResolution.x);
            int iy = Mathf.Clamp(FastFloorToInt((cellCenterY - migrationGridOriginAupLocal.y) / safeCellSize), 0, _migrationGridResolution.y - 1);
            int iz = WrapIndex(FastFloorToInt((cellCenterZ - migrationGridOriginAupLocal.z) / safeCellSize), _migrationGridResolution.z);
            cellIndex = BuildMigrationCellIndex(ix, iy, iz, _migrationGridResolution);
            return cellIndex >= 0 && cellIndex < _migrationGridCellCount;
        }

        private void RefreshDebugStatisticalSwarmSlotCount()
        {
            if (!_migrationSwarmStates.IsCreated)
            {
                _debugStatisticalSwarmSlotCount = 0;
                return;
            }

            int count = 0;
            for (int i = 0; i < _migrationSwarmStates.Length; i++)
            {
                if (_migrationSwarmStates[i].Flags != 0)
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
                if (poi.Flags != 0 && gameTimeSeconds < poi.ExpireGameTimeSeconds)
                    count++;
            }

            for (int i = 0; i < _pendingBloodCloudPoiWriteCount; i++)
            {
                MigrationBloodCloudPoi pendingPoi = _pendingBloodCloudPoiWrites[i];
                if (pendingPoi.Flags == 0 || gameTimeSeconds >= pendingPoi.ExpireGameTimeSeconds)
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
                if (poi.Flags != 0 &&
                    poi.SourceId == sourceId &&
                    gameTimeSeconds < poi.ExpireGameTimeSeconds)
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
                if (poi.Flags != 0 &&
                    poi.SourceId == sourceId &&
                    gameTimeSeconds < poi.ExpireGameTimeSeconds)
                {
                    return true;
                }
            }

            return false;
        }

        private float ResolveTimelineGameSeconds(float fallbackRuntimeSeconds)
        {
            var celestialEngine = GlobalRegistry.CelestialEngine;
            if (celestialEngine != null)
                return celestialEngine.GameTime;

            return fallbackRuntimeSeconds > 0f ? fallbackRuntimeSeconds : Time.time;
        }

        private float ResolveSeasonalPhase(float gameTimeSeconds)
        {
            var celestialEngine = GlobalRegistry.CelestialEngine;
            float celestialPhase = celestialEngine != null ? celestialEngine.PlanetPhase * Tau : 0f;
            return celestialPhase + gameTimeSeconds * Mathf.Max(0f, seasonalRadiansPerGameSecond);
        }

        private Vector3 ResolveMigrationProbePosition(int biomeIndex, uint hash)
        {
            float biomeOffset = biomeIndex * 173.31f;
            float dayOffset = _currentDayIndex * 41.7f;
            float x = math.sin(biomeOffset + dayOffset + Hash01(hash ^ 0x68E31DA4u) * Tau) * 420f;
            float z = math.cos(biomeOffset * 0.5f + dayOffset + Hash01(hash ^ 0xC2B2AE35u) * Tau) * 420f;
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
            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromRuntimePosition(origin);
            double3 originAbsolute = originAup.ToAbsoluteDouble3();
            double3 routeOffset = new double3(
                direction.x * (double)routeDistanceMeters,
                direction.y * (double)routeDistanceMeters - depthBiasMeters,
                direction.z * (double)routeDistanceMeters);
            AbsoluteUniversePosition targetAup = AbsoluteUniversePosition.FromAbsolutePosition(originAbsolute + routeOffset);
            float3 runtimeTarget = targetAup.ToRuntimeFloat3();
            return new Vector3(runtimeTarget.x, runtimeTarget.y, runtimeTarget.z);
        }

        private static float3 NormalizeOrFallback(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return lengthSq > 0.0001f ? value * math.rsqrt(lengthSq) : fallback;
        }

        private float ResolveWaterTemperature(Vector3 origin)
        {
            HectonMapMagicVegetationBridge bridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
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
                planarDirection = new float2(math.cos(headingRadians), math.sin(headingRadians));
            }

            return new Vector3(planarDirection.x, 0f, planarDirection.y);
        }

        private void SanitizeMigrationSettings()
        {
            fallbackMigrationDistanceMeters = Mathf.Max(1f, fallbackMigrationDistanceMeters);
            fallbackCurrentAlignmentWeight = Mathf.Clamp01(fallbackCurrentAlignmentWeight);
            migrationCellSizeMeters = Mathf.Max(1f, migrationCellSizeMeters);
            migrationGridResolutionX = Mathf.Clamp(migrationGridResolutionX, MinimumGridResolutionXZ, MaximumGridResolutionXZ);
            migrationGridResolutionY = Mathf.Clamp(migrationGridResolutionY, MinimumGridResolutionY, MaximumGridResolutionY);
            migrationGridResolutionZ = Mathf.Clamp(migrationGridResolutionZ, MinimumGridResolutionXZ, MaximumGridResolutionXZ);
            migrationFieldColdTickIntervalSeconds = Mathf.Max(1f, migrationFieldColdTickIntervalSeconds);
            migrationFlowAlignmentWeight = Mathf.Clamp01(migrationFlowAlignmentWeight);
            seasonalRadiansPerGameSecond = Mathf.Max(0f, seasonalRadiansPerGameSecond);
            migrationVerticalFlowWeight = Mathf.Clamp(migrationVerticalFlowWeight, 0f, 0.35f);
            bloodCloudPoiRadiusMeters = Mathf.Max(10f, bloodCloudPoiRadiusMeters);
            bloodCloudPoiStrength = Mathf.Clamp(bloodCloudPoiStrength, 0f, 4f);
            whaleFallScavengerPopulationMultiplier = Mathf.Clamp(whaleFallScavengerPopulationMultiplier, 1f, 50f);
            vrVatSwayAmplitudeScale = Mathf.Clamp(vrVatSwayAmplitudeScale, 1f, 2f);
        }

        private void AllocateMigrationNativeState()
        {
            SanitizeMigrationSettings();
            int3 requiredResolution = new int3(migrationGridResolutionX, migrationGridResolutionY, migrationGridResolutionZ);
            int requiredCellCount = requiredResolution.x * requiredResolution.y * requiredResolution.z;
            if (_migrationGridFront.IsCreated &&
                _migrationGridBack.IsCreated &&
                _bloodCloudPois.IsCreated &&
                _migrationSwarmStates.IsCreated &&
                _migrationGridCellCount == requiredCellCount &&
                math.all(_migrationGridResolution == requiredResolution))
            {
                return;
            }

            DisposeMigrationNativeState();
            _migrationGridResolution = requiredResolution;
            _migrationGridCellCount = requiredCellCount;
            _debugMigrationGridCellCount = requiredCellCount;

            // COLD ALLOC: NativeArray<MigrationGridCell>[65536 max] — double-buffered global migration flow field — owner: MigrationDirector
            _migrationGridFront = H8Memory.Allocate<MigrationGridCell>(requiredCellCount, NativeMemorySystemId, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(_migrationGridFront, NativeMemoryOwner, nameof(_migrationGridFront), NativeMemoryLifetime);
            // COLD ALLOC: NativeArray<MigrationGridCell>[65536 max] — Burst write target for seasonal migration flow updates — owner: MigrationDirector
            _migrationGridBack = H8Memory.Allocate<MigrationGridCell>(requiredCellCount, NativeMemorySystemId, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            NativeMemorySentinel.RegisterNativeArray(_migrationGridBack, NativeMemoryOwner, nameof(_migrationGridBack), NativeMemoryLifetime);
            // COLD ALLOC: NativeArray<MigrationBloodCloudPoi>[8] — kill-site vector distortion sources — owner: MigrationDirector
            _bloodCloudPois = H8Memory.Allocate<MigrationBloodCloudPoi>(BloodCloudPoiCapacity, NativeMemorySystemId, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(_bloodCloudPois, NativeMemoryOwner, nameof(_bloodCloudPois), NativeMemoryLifetime);
            // COLD ALLOC: NativeArray<MigrationSwarmState>[128] — O(1) statistical swarm population points — owner: MigrationDirector
            _migrationSwarmStates = H8Memory.Allocate<MigrationSwarmState>(MigrationSwarmCapacity, NativeMemorySystemId, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(_migrationSwarmStates, NativeMemoryOwner, nameof(_migrationSwarmStates), NativeMemoryLifetime);
            if (!IsMigrationNativeStateAllocated)
            {
                DisposeMigrationNativeState();
                return;
            }

            _debugStatisticalSwarmSlotCount = 0;
            _coldTickAccumulator = migrationFieldColdTickIntervalSeconds;
            _lastColdTickRuntimeSeconds = -1f;
        }

        private void DisposeMigrationNativeState()
        {
            bool hadScheduledFieldJob = _migrationFieldScheduled;
            JobHandle disposeDependency = hadScheduledFieldJob ? _migrationFieldHandle : default;
            bool scheduledDispose = false;
            scheduledDispose |= DisposeNativeArrayDeferred(ref _migrationGridFront, disposeDependency);
            scheduledDispose |= DisposeNativeArrayDeferred(ref _migrationGridBack, disposeDependency);
            scheduledDispose |= DisposeNativeArrayDeferred(ref _bloodCloudPois, disposeDependency);
            scheduledDispose |= DisposeNativeArrayDeferred(ref _migrationSwarmStates, disposeDependency);
            if (scheduledDispose)
                JobHandle.ScheduleBatchedJobs();

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
        }

        private static bool DisposeNativeArrayDeferred<T>(ref NativeArray<T> array, JobHandle dependency) where T : struct
        {
            if (!array.IsCreated)
                return false;

            NativeMemorySentinel.UnregisterNativeArray(array);
            H8Memory.Release(ref array, dependency, NativeMemorySystemId);
            array = default;
            return true;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            MigrationDirector registered = GlobalRegistry.Migration;
            if (registered != null && registered != this)
            {
                SuppressDuplicateService();
                return;
            }

            GlobalRegistry.RegisterMigrationDirectorRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.Migration, this);
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

        private static uint HashString(string value)
        {
            unchecked
            {
                if (string.IsNullOrEmpty(value))
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
            int integer = (int)value;
            return value < integer ? integer - 1 : integer;
        }

        private static int FastFloorToInt(double value)
        {
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

        private float ResolveColdTickDeltaSeconds(float runtimeSeconds)
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

            return math.min(deltaSeconds, migrationFieldColdTickIntervalSeconds);
        }

        private int3 ResolveMigrationAupCell(Vector3 origin)
        {
            double safeCellSize = Mathf.Max(1f, migrationCellSizeMeters);
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
            double safeCellSize = Mathf.Max(1f, migrationCellSizeMeters);
            double3 aupMeters = ResolveAupMeters(runtimePosition);
            double extentX = safeCellSize * Mathf.Max(1, migrationGridResolutionX);
            double extentY = safeCellSize * Mathf.Max(1, migrationGridResolutionY);
            double extentZ = safeCellSize * Mathf.Max(1, migrationGridResolutionZ);
            double originX = migrationGridOriginAupLocal.x;
            double originY = migrationGridOriginAupLocal.y;
            double originZ = migrationGridOriginAupLocal.z;
            return new float3(
                (float)WrapCoordinateToExtent(aupMeters.x, originX, extentX),
                (float)ClampCoordinate(aupMeters.y, originY, originY + extentY),
                (float)WrapCoordinateToExtent(aupMeters.z, originZ, extentZ));
        }

        private static double3 ResolveAupMeters(Vector3 runtimePosition)
        {
            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            return new double3(
                aup.GridX * (double)AbsoluteUniversePosition.CellSizeMeters + aup.LocalX,
                aup.GridY * (double)AbsoluteUniversePosition.CellSizeMeters + aup.LocalY,
                aup.GridZ * (double)AbsoluteUniversePosition.CellSizeMeters + aup.LocalZ);
        }

        private static double WrapCoordinateToExtent(double value, double origin, double extent)
        {
            if (extent <= 0d)
                return origin;

            double local = value - origin;
            double wrapped = local - math.floor(local / extent) * extent;
            return origin + wrapped;
        }

        private static double ClampCoordinate(double value, double min, double max)
        {
            if (value < min)
                return min;

            return value > max ? max : value;
        }

        private static bool IsVrSwarmScalingActive()
        {
            return HectonXRRuntimeState.IsXRActive;
        }

        [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
        private struct BuildMigrationVectorFieldJob : IJobParallelFor
        {
            [WriteOnly] public NativeArray<MigrationGridCell> Output;
            [ReadOnly] public NativeArray<MigrationBloodCloudPoi> BloodCloudPois;
            public int3 Resolution;
            public float CellSizeMeters;
            public float3 OriginAupLocal;
            public float SeasonalPhase;
            public float VerticalFlowWeight;
            public float CurrentGameTimeSeconds;

            public void Execute(int index)
            {
                int x = index % Resolution.x;
                int z = (index / Resolution.x) % Resolution.z;
                int y = index / (Resolution.x * Resolution.z);
                float safeCellSize = math.max(1f, CellSizeMeters);

                float invX = Resolution.x > 0 ? 1f / Resolution.x : 0f;
                float invY = Resolution.y > 1 ? 1f / (Resolution.y - 1) : 0f;
                float invZ = Resolution.z > 0 ? 1f / Resolution.z : 0f;
                float u = x * invX;
                float v = z * invZ;
                float depth01 = y * invY;
                float seamAngleX = u * Tau;
                float seamAngleZ = v * Tau;
                float seasonalAngle = SeasonalPhase + math.sin(seamAngleX) * 0.85f + math.cos(seamAngleZ) * 0.65f + depth01 * 0.72f;

                float3 cellPosition = OriginAupLocal + new float3(
                    (x + 0.5f) * safeCellSize,
                    (y + 0.5f) * safeCellSize,
                    (z + 0.5f) * safeCellSize);

                float3 baseDirection = new float3(
                    math.cos(seasonalAngle) + math.sin(seamAngleZ + SeasonalPhase * 0.37f) * 0.23f,
                    math.sin(seasonalAngle * 0.53f + depth01 * Tau) * VerticalFlowWeight,
                    math.sin(seasonalAngle) + math.cos(seamAngleX - SeasonalPhase * 0.41f) * 0.23f);

                float3 attraction = float3.zero;
                float attractionWeight = 0f;
                for (int i = 0; i < BloodCloudPois.Length; i++)
                {
                    MigrationBloodCloudPoi poi = BloodCloudPois[i];
                    if (poi.Flags == 0 || CurrentGameTimeSeconds >= poi.ExpireGameTimeSeconds)
                        continue;

                    float radius = math.max(1f, poi.RadiusMeters);
                    float invRadius = 1f / radius;
                    float invRadiusSq = invRadius * invRadius;
                    float radiusSq = radius * radius;
                    float3 toPoi = poi.PositionFieldAupMeters - cellPosition;
                    toPoi.x = WrapDeltaToExtent(toPoi.x, safeCellSize * math.max(1, Resolution.x));
                    toPoi.z = WrapDeltaToExtent(toPoi.z, safeCellSize * math.max(1, Resolution.z));
                    float distSq = math.lengthsq(toPoi);
                    if (distSq > radiusSq)
                        continue;

                    float falloff = math.saturate(1f - distSq * invRadiusSq);
                    float strength = math.max(0f, poi.Strength) * falloff * falloff;
                    attraction += toPoi * (strength * invRadius);
                    attractionWeight += strength;
                }

                float3 direction = NormalizeOrFallback(baseDirection + attraction, new float3(0f, 0f, 1f));
                MigrationGridCell cell = default;
                cell.AupCell = (int3)math.floor(cellPosition / safeCellSize);
                cell.Direction = direction;
                cell.Magnitude = math.saturate(1f + attractionWeight * 0.18f);
                cell.PopulationCount = 0;
                cell.Flags = (ushort)(attractionWeight > 0.0001f ? MigrationCellFlagBloodCloud : 0);
                Output[index] = cell;
            }

            private static float WrapDeltaToExtent(float delta, float extent)
            {
                if (extent <= 0f)
                    return delta;

                return delta - math.round(delta / extent) * extent;
            }

            private static float3 NormalizeOrFallback(float3 value, float3 fallback)
            {
                float lengthSq = math.lengthsq(value);
                return lengthSq > 0.0001f ? value * math.rsqrt(lengthSq) : fallback;
            }
        }
    }
}
