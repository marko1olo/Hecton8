using System.Runtime.InteropServices;
using Hecton8.AI;
using Hecton8.Atmosphere;
using Hecton8.Celestial;
using Hecton8.Core;
using Hecton8.PDA;
using Hecton8.Physics;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.XR;

namespace Hecton8.Ecosystem
{
    /// <summary>
    /// Slow-tick owner for deterministic daily fauna migration pressure, global swarm flow, and kill-site scavenger bias.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6225)]
    [AddComponentMenu("Hecton8/Ecosystem/Migration Director")]
    public sealed class MigrationDirector : MonoBehaviour, ISlowTickable, ILateFrameTickable
    {
        private static MigrationDirector _instance;
        private const float DefaultMigrationDistanceMeters = 320f;
        private const float DefaultSlowTickIntervalSeconds = 0.5f;
        private const float DefaultColdTickIntervalSeconds = 5f;
        private const float GlobalMigrationCellSizeMeters = 100f;
        private const float BloodCloudPoiLifetimeGameSeconds = 3600f;
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
        private const string NativeMemoryOwner = nameof(MigrationDirector);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;

        private bool _registeredToTick;
        private bool _registeredLateFrameTick;
        private int _currentDayIndex = 1;
        private int3 _migrationGridResolution;
        private int _migrationGridCellCount;
        private float _coldTickAccumulator;
        private float _debugLastSeasonalPhase;
        private float _debugLastMigrationGridMagnitude;
        private int _debugBloodCloudPoiCount;
        private int _debugMigrationGridCellCount;
        private int _debugStatisticalSwarmSlotCount;
        private bool _debugVrSwarmScalingActive;
        private JobHandle _migrationFieldHandle;
        private bool _migrationFieldScheduled;

        private NativeArray<MigrationGridCell> _migrationGridFront;
        private NativeArray<MigrationGridCell> _migrationGridBack;
        private NativeArray<MigrationBloodCloudPoi> _bloodCloudPois;
        private NativeArray<MigrationSwarmState> _migrationSwarmStates;

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
        [Tooltip("Population multiplier applied to scavenger swarms near whale-fall POIs for one game-time hour.")]
        [SerializeField, Range(1f, 8f)] private float whaleFallScavengerPopulationMultiplier = 2.4f;
        [Tooltip("Optional species ids eligible for whale-fall density boost. Empty means every migration swarm species can scavenge.")]
        [SerializeField] private int[] scavengerMigrationSpeciesIds;

        [Header("PC/VR Swarm Scaling")]
        [Tooltip("VAT sway amplitude multiplier used when VR swarm count is reduced.")]
        [SerializeField, Range(1f, 2f)] private float vrVatSwayAmplitudeScale = 1.4f;

        [Header("Diagnostics")]
        [SerializeField] private float _debugLastMigrationTemperatureCelsius = 15f;
        [SerializeField] private Vector3 _debugLastMigrationDirection = Vector3.forward;
        [SerializeField] private Vector3 _debugLastMigrationGridDirection = Vector3.forward;

        /// <summary>Active runtime owner while the gameplay scene is loaded.</summary>
        public static MigrationDirector Instance => _instance;

        /// <summary>
        /// Coarse global migration cell. X/Z indices wrap in AUP-space; Y is clamped to the shelf water column.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct MigrationGridCell
        {
            public int3 AupCell;
            public float3 Direction;
            public float Magnitude;
            public ushort PopulationCount;
            public ushort Flags;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct MigrationBloodCloudPoi
        {
            public AbsoluteUniversePositionBlit128 PositionAup;
            public float3 PositionWS;
            public float RadiusMeters;
            public float Strength;
            public float ExpireGameTimeSeconds;
            public int SourceId;
            public int Flags;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct MigrationSwarmState
        {
            public int3 AupCell;
            public float3 LocalPosition;
            public float RadiusMeters;
            public float LastWriteGameTimeSeconds;
            public ushort PopulationCount;
            public ushort SpeciesId;
            public uint Flags;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            SanitizeMigrationSettings();
            AllocateMigrationNativeState();
            RefreshCurrentDay();
        }

        private void OnEnable()
        {
            TryRegisterToTickManager();
            TryRegisterLateFrameTickManager();
            AllocateMigrationNativeState();
            RefreshCurrentDay();
        }

        private void Start()
        {
            TryRegisterToTickManager();
            TryRegisterLateFrameTickManager();
            AllocateMigrationNativeState();
            RefreshCurrentDay();
        }

        private void OnDisable()
        {
            UnregisterFromTickManager();
            UnregisterLateFrameTickManager();
            DisposeMigrationNativeState();
        }

        private void OnDestroy()
        {
            UnregisterFromTickManager();
            UnregisterLateFrameTickManager();
            DisposeMigrationNativeState();
            if (_instance == this)
                _instance = null;
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
            return _instance != null ? _instance.ResolveSelectionMultiplierInternal(biomeIndex, archetype) : 1f;
        }

        /// <summary>
        /// Resolves visible boid count from the O(1) swarm population state and platform-specific scaling.
        /// </summary>
        public static int ResolveVisibleBoidCount(int speciesId, Vector3 origin, int requestedPopulationCount)
        {
            return _instance != null
                ? _instance.ResolveVisibleBoidCountInternal(speciesId, origin, requestedPopulationCount)
                : Mathf.Max(0, requestedPopulationCount);
        }

        /// <summary>
        /// Resolves VAT sway amplitude compensation for VR swarm downscaling.
        /// </summary>
        public static float ResolveVatSwayAmplitudeScale()
        {
            return _instance != null ? _instance.ResolveVatSwayAmplitudeScaleInternal() : 1f;
        }

        /// <summary>
        /// Registers one apex kill as a blood-cloud migration POI and one-hour whale-fall density source.
        /// </summary>
        public static void RegisterPredatorKillPoi(uint uniqueInstanceUid, Vector3 worldPosition, float fallbackRuntimeSeconds)
        {
            if (_instance != null)
                _instance.RegisterPredatorKillPoiInternal(uniqueInstanceUid, worldPosition, fallbackRuntimeSeconds);
        }

        internal static bool TryResolveMigrationTarget(int speciesId, Vector3 origin, out Vector3 target)
        {
            target = origin;
            return _instance != null && _instance.TryResolveMigrationTargetInternal(speciesId, origin, out target);
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
                ? Mathf.Lerp(1.12f, 1.6f, waveStrength)
                : Mathf.Lerp(0.58f, 0.92f, waveStrength);
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

            currentVector.Normalize();

            float preferredHeadingRadians = Hash01(hash ^ 0xB5297A4Du) * Mathf.PI * 2f;
            Vector3 preferredHeading = new Vector3(
                Mathf.Cos(preferredHeadingRadians),
                0f,
                Mathf.Sin(preferredHeadingRadians));
            float alignment01 = Mathf.InverseLerp(-1f, 1f, Vector3.Dot(currentVector, preferredHeading));

            float roleBlend = archetype.roleType == CreatureRoleType.Ambient ? 0.82f : 0.64f;
            float weightedAlignment = Mathf.Lerp(0.5f, alignment01, roleBlend);
            return Mathf.Lerp(0.7f, 1.45f, weightedAlignment);
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

            uint seed = Hash((uint)speciesId ^ (uint)_currentDayIndex * 0x9E3779B9u ^ HashFloat3(origin));
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

            target = origin + (migrationDirection * routeDistance) + (Vector3.down * depthBiasMeters);
            return true;
        }

        private int ResolveVisibleBoidCountInternal(int speciesId, Vector3 origin, int requestedPopulationCount)
        {
            int safePopulationCount = Mathf.Max(0, requestedPopulationCount);
            float populationMultiplier = ResolveWhaleFallPopulationMultiplier(speciesId, origin, ResolveTimelineGameSeconds(0f));
            int resolvedPopulationCount = Mathf.Clamp(Mathf.RoundToInt(safePopulationCount * populationMultiplier), 0, int.MaxValue);
            if (IsVrSwarmScalingActive())
                resolvedPopulationCount = Mathf.Clamp(Mathf.RoundToInt(resolvedPopulationCount * VrSwarmPopulationScale), 0, int.MaxValue);

            WriteSwarmPopulationState(speciesId, origin, resolvedPopulationCount);
            return resolvedPopulationCount;
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
            int selectedSlot = 0;
            float earliestExpiry = float.MaxValue;
            for (int i = 0; i < _bloodCloudPois.Length; i++)
            {
                MigrationBloodCloudPoi candidate = _bloodCloudPois[i];
                if (candidate.Flags == 0 || gameTimeSeconds >= candidate.ExpireGameTimeSeconds)
                {
                    selectedSlot = i;
                    break;
                }

                if (candidate.ExpireGameTimeSeconds < earliestExpiry)
                {
                    earliestExpiry = candidate.ExpireGameTimeSeconds;
                    selectedSlot = i;
                }
            }

            MigrationBloodCloudPoi poi = default;
            poi.PositionWS = new float3(worldPosition.x, worldPosition.y, worldPosition.z);
            poi.PositionAup = AbsoluteUniversePosition.FromRuntimePosition(worldPosition).ToAlignedBlit();
            poi.RadiusMeters = Mathf.Max(10f, bloodCloudPoiRadiusMeters);
            poi.Strength = Mathf.Max(0f, bloodCloudPoiStrength);
            poi.ExpireGameTimeSeconds = gameTimeSeconds + BloodCloudPoiLifetimeGameSeconds;
            poi.SourceId = unchecked((int)(uniqueInstanceUid & 0x7FFFFFFFu));
            poi.Flags = 1;
            _bloodCloudPois[selectedSlot] = poi;
            _debugBloodCloudPoiCount = CountActiveBloodCloudPois(gameTimeSeconds);
        }

        private void AdvanceMigrationFieldColdTick()
        {
            if (!Application.isPlaying)
                return;

            AllocateMigrationNativeState();
            if (!_migrationGridFront.IsCreated || !_migrationGridBack.IsCreated || _migrationFieldScheduled)
                return;

            _coldTickAccumulator += DefaultSlowTickIntervalSeconds;
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
        }

        private bool TrySampleMigrationFieldDirection(Vector3 origin, out Vector3 direction)
        {
            direction = Vector3.zero;
            if (!_migrationGridFront.IsCreated || _migrationGridFront.Length != _migrationGridCellCount || _migrationGridCellCount <= 0)
                return false;

            double safeCellSize = Mathf.Max(1f, migrationCellSizeMeters);
            double3 originAupMeters = ResolveAupMeters(origin);
            double x = (originAupMeters.x - migrationGridOriginAupLocal.x) / safeCellSize;
            double y = (originAupMeters.y - migrationGridOriginAupLocal.y) / safeCellSize;
            double z = (originAupMeters.z - migrationGridOriginAupLocal.z) / safeCellSize;

            int ix = WrapIndex(FastFloorToInt(x), _migrationGridResolution.x);
            int iy = Mathf.Clamp(FastFloorToInt(y), 0, _migrationGridResolution.y - 1);
            int iz = WrapIndex(FastFloorToInt(z), _migrationGridResolution.z);
            float3 sampled = _migrationGridFront[BuildMigrationCellIndex(ix, iy, iz, _migrationGridResolution)].Direction;
            direction = new Vector3(sampled.x, sampled.y, sampled.z);
            return direction.sqrMagnitude > 0.0001f;
        }

        private float ResolveWhaleFallPopulationMultiplier(int speciesId, Vector3 origin, float gameTimeSeconds)
        {
            if (!_bloodCloudPois.IsCreated || !IsScavengerMigrationSpecies(speciesId))
                return 1f;

            float multiplier = 1f;
            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromRuntimePosition(origin);
            for (int i = 0; i < _bloodCloudPois.Length; i++)
            {
                MigrationBloodCloudPoi poi = _bloodCloudPois[i];
                if (poi.Flags == 0 || gameTimeSeconds >= poi.ExpireGameTimeSeconds)
                    continue;

                double radius = math.max(1f, poi.RadiusMeters);
                double radiusSq = radius * radius;
                AbsoluteUniversePosition poiAup = AbsoluteUniversePosition.FromAlignedBlit(in poi.PositionAup);
                double distSq = AbsoluteUniversePosition.DistanceSq(in originAup, in poiAup);
                if (distSq > radiusSq)
                    continue;

                float falloff = 1f - math.saturate((float)(distSq / radiusSq));
                float candidateMultiplier = Mathf.Lerp(1f, Mathf.Max(1f, whaleFallScavengerPopulationMultiplier), falloff * falloff);
                if (candidateMultiplier > multiplier)
                    multiplier = candidateMultiplier;
            }

            return multiplier;
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

            uint hash = Hash((uint)speciesId ^ HashFloat3(origin));
            int slot = (int)(hash % (uint)_migrationSwarmStates.Length);
            MigrationSwarmState state = default;
            state.AupCell = ResolveMigrationAupCell(origin);
            state.LocalPosition = new float3(origin.x, origin.y, origin.z);
            state.RadiusMeters = 200f;
            state.LastWriteGameTimeSeconds = ResolveTimelineGameSeconds(Time.time);
            state.PopulationCount = (ushort)Mathf.Clamp(populationCount, 0, ushort.MaxValue);
            state.SpeciesId = (ushort)Mathf.Clamp(speciesId, 0, ushort.MaxValue);
            state.Flags = 1u;
            _migrationSwarmStates[slot] = state;
            _debugStatisticalSwarmSlotCount = Mathf.Min(_migrationSwarmStates.Length, _debugStatisticalSwarmSlotCount + 1);
        }

        private int CountActiveBloodCloudPois(float gameTimeSeconds)
        {
            if (!_bloodCloudPois.IsCreated)
                return 0;

            int count = 0;
            for (int i = 0; i < _bloodCloudPois.Length; i++)
            {
                MigrationBloodCloudPoi poi = _bloodCloudPois[i];
                if (poi.Flags != 0 && gameTimeSeconds < poi.ExpireGameTimeSeconds)
                    count++;
            }

            return count;
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
            float x = Mathf.Sin(biomeOffset + dayOffset + Hash01(hash ^ 0x68E31DA4u) * 6.28318f) * 420f;
            float z = Mathf.Cos(biomeOffset * 0.5f + dayOffset + Hash01(hash ^ 0xC2B2AE35u) * 6.28318f) * 420f;
            float y = -Mathf.Lerp(24f, 220f, Hash01(hash ^ 0x9E3779B9u));
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
            planar = math.normalizesafe(planar, new float3(preferredDirection.x, 0f, preferredDirection.z));
            return new Vector3(planar.x, 0f, planar.z);
        }

        private static Vector3 BlendDirectionsLinear(Vector3 from, Vector3 to, float weight)
        {
            float3 a = math.normalizesafe(new float3(from.x, from.y, from.z), new float3(0f, 0f, 1f));
            float3 b = math.normalizesafe(new float3(to.x, to.y, to.z), a);
            float3 blended = math.normalizesafe(math.lerp(a, b, math.saturate(weight)), a);
            return new Vector3(blended.x, blended.y, blended.z);
        }

        private float ResolveWaterTemperature(Vector3 origin)
        {
            HectonMapMagicVegetationBridge bridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            return bridge != null ? bridge.GetWaterTemperature(origin) : 15f;
        }

        private static Vector3 ResolvePreferredDirection(Vector2 preferredPlanarDirection, uint seed)
        {
            Vector2 planarDirection = preferredPlanarDirection.sqrMagnitude > 0.0001f
                ? preferredPlanarDirection.normalized
                : new Vector2(
                    Mathf.Cos(Hash01(seed ^ 0x68E31DA4u) * Mathf.PI * 2f),
                    Mathf.Sin(Hash01(seed ^ 0xC2B2AE35u) * Mathf.PI * 2f));
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
            whaleFallScavengerPopulationMultiplier = Mathf.Clamp(whaleFallScavengerPopulationMultiplier, 1f, 8f);
            vrVatSwayAmplitudeScale = Mathf.Clamp(vrVatSwayAmplitudeScale, 1f, 2f);
        }

        private void AllocateMigrationNativeState()
        {
            SanitizeMigrationSettings();
            int3 requiredResolution = new int3(migrationGridResolutionX, migrationGridResolutionY, migrationGridResolutionZ);
            int requiredCellCount = requiredResolution.x * requiredResolution.y * requiredResolution.z;
            if (_migrationGridFront.IsCreated && _migrationGridCellCount == requiredCellCount)
                return;

            DisposeMigrationNativeState();
            _migrationGridResolution = requiredResolution;
            _migrationGridCellCount = requiredCellCount;
            _debugMigrationGridCellCount = requiredCellCount;

            // COLD ALLOC: NativeArray<MigrationGridCell>[65536 max] — double-buffered global migration flow field — owner: MigrationDirector
            _migrationGridFront = new NativeArray<MigrationGridCell>(requiredCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(_migrationGridFront, NativeMemoryOwner, nameof(_migrationGridFront), NativeMemoryLifetime);
            // COLD ALLOC: NativeArray<MigrationGridCell>[65536 max] — Burst write target for seasonal migration flow updates — owner: MigrationDirector
            _migrationGridBack = new NativeArray<MigrationGridCell>(requiredCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(_migrationGridBack, NativeMemoryOwner, nameof(_migrationGridBack), NativeMemoryLifetime);
            // COLD ALLOC: NativeArray<MigrationBloodCloudPoi>[8] — kill-site vector distortion sources — owner: MigrationDirector
            _bloodCloudPois = new NativeArray<MigrationBloodCloudPoi>(BloodCloudPoiCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(_bloodCloudPois, NativeMemoryOwner, nameof(_bloodCloudPois), NativeMemoryLifetime);
            // COLD ALLOC: NativeArray<MigrationSwarmState>[128] — O(1) statistical swarm population points — owner: MigrationDirector
            _migrationSwarmStates = new NativeArray<MigrationSwarmState>(MigrationSwarmCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(_migrationSwarmStates, NativeMemoryOwner, nameof(_migrationSwarmStates), NativeMemoryLifetime);
            _debugStatisticalSwarmSlotCount = 0;
        }

        private void DisposeMigrationNativeState()
        {
            CompleteMigrationFieldJob(forceComplete: true);
            if (_migrationGridFront.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_migrationGridFront);
                _migrationGridFront.Dispose();
                _migrationGridFront = default;
            }

            if (_migrationGridBack.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_migrationGridBack);
                _migrationGridBack.Dispose();
                _migrationGridBack = default;
            }

            if (_bloodCloudPois.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_bloodCloudPois);
                _bloodCloudPois.Dispose();
                _bloodCloudPois = default;
            }

            if (_migrationSwarmStates.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_migrationSwarmStates);
                _migrationSwarmStates.Dispose();
                _migrationSwarmStates = default;
            }

            _migrationGridCellCount = 0;
            _debugMigrationGridCellCount = 0;
            _migrationFieldScheduled = false;
        }

        private void TryRegisterToTickManager()
        {
            if (_registeredToTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToTick = GlobalRegistry.SlowTickables.Contains(this);
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

            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrameTick = SystemDispatcher.GetLateFrameLane(PriorityLayer.Environment).Contains(this);
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

        private static uint HashFloat3(Vector3 value)
        {
            unchecked
            {
                uint hash = 0x811C9DC5u;
                hash = Hash(hash ^ (uint)Mathf.RoundToInt(value.x * 10f));
                hash = Hash(hash ^ (uint)Mathf.RoundToInt(value.y * 10f));
                hash = Hash(hash ^ (uint)Mathf.RoundToInt(value.z * 10f));
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

        private int3 ResolveMigrationAupCell(Vector3 origin)
        {
            double safeCellSize = Mathf.Max(1f, migrationCellSizeMeters);
            double3 originAupMeters = ResolveAupMeters(origin);
            return new int3(
                FastFloorToInt(originAupMeters.x / safeCellSize),
                FastFloorToInt(originAupMeters.y / safeCellSize),
                FastFloorToInt(originAupMeters.z / safeCellSize));
        }

        private static double3 ResolveAupMeters(Vector3 runtimePosition)
        {
            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            return new double3(
                aup.GridX * (double)AbsoluteUniversePosition.CellSizeMeters + aup.LocalX,
                aup.GridY * (double)AbsoluteUniversePosition.CellSizeMeters + aup.LocalY,
                aup.GridZ * (double)AbsoluteUniversePosition.CellSizeMeters + aup.LocalZ);
        }

        private static bool IsVrSwarmScalingActive()
        {
            return HectonXRRuntimeState.IsXRActive || (XRSettings.enabled && XRSettings.isDeviceActive);
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
                    (x + 0.5f) * CellSizeMeters,
                    (y + 0.5f) * CellSizeMeters,
                    (z + 0.5f) * CellSizeMeters);

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
                    float radiusSq = radius * radius;
                    float3 toPoi = poi.PositionWS - cellPosition;
                    float distSq = math.lengthsq(toPoi);
                    if (distSq > radiusSq)
                        continue;

                    float falloff = 1f - math.saturate(distSq / radiusSq);
                    float strength = math.max(0f, poi.Strength) * falloff * falloff;
                    attraction += math.normalizesafe(toPoi, float3.zero) * strength;
                    attractionWeight += strength;
                }

                float3 direction = math.normalizesafe(baseDirection + attraction, new float3(0f, 0f, 1f));
                MigrationGridCell cell = default;
                cell.AupCell = new int3(x, y, z);
                cell.Direction = direction;
                cell.Magnitude = math.saturate(1f + attractionWeight * 0.18f);
                cell.PopulationCount = 0;
                cell.Flags = (ushort)(attractionWeight > 0.0001f ? 1 : 0);
                Output[index] = cell;
            }
        }
    }
}
