using System.Diagnostics;
using Hecton8.Core;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    internal struct HazardVolumeData
    {
        public float3 AbsoluteUniversePosition;
        public float Radius;
        public float InvRadius;
        public float InvRadiusSqr;
        public float Intensity;
        public float VisorGlitchBias;
        public int CurveLutOffset;
        public HazardType Type;
    }

    internal struct HazardExposureJobResult
    {
        public float PlayerRadiation;
        public float PlayerHeat;
        public float PlayerToxicity;
        public float PlayerBiohazard;
        public float PlayerRadiationGlitchBias;
        public float PlayerHeatGlitchBias;
        public float PlayerToxicityGlitchBias;
        public float PlayerBiohazardGlitchBias;
        public float VehicleRadiation;
        public float VehicleHeat;
        public float VehicleToxicity;
        public float VehicleBiohazard;
        public float VehicleRadiationGlitchBias;
        public float VehicleHeatGlitchBias;
        public float VehicleToxicityGlitchBias;
        public float VehicleBiohazardGlitchBias;
        public byte PlayerExposureMask;
        public byte VehicleExposureMask;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct EvaluateHazardExposureJob : IJob
    {
        [ReadOnly] public NativeArray<HazardVolumeData> Volumes;
        [ReadOnly] public NativeArray<float> CurveLutSamples;
        public int CurveLutSampleCount;
        public int VolumeCount;
        public bool HasPlayerBounds;
        public bool HasVehicleBounds;
        public float3 PlayerCenter;
        public float3 PlayerHalfExtents;
        public float3 VehicleCenter;
        public float3 VehicleHalfExtents;
        public NativeArray<HazardExposureJobResult> Result;

        public void Execute()
        {
            HazardExposureJobResult result = default;
            for (int i = 0; i < VolumeCount; i++)
            {
                HazardVolumeData volume = Volumes[i];

                if (HasPlayerBounds)
                {
                    float playerContribution = EvaluateAabbSphereContribution(
                        PlayerCenter,
                        PlayerHalfExtents,
                        in volume,
                        CurveLutSamples,
                        CurveLutSampleCount);

                    if (playerContribution > 0f)
                        AddContribution(ref result, volume.Type, playerContribution, volume.VisorGlitchBias, true);
                }

                if (HasVehicleBounds)
                {
                    float vehicleContribution = EvaluateAabbSphereContribution(
                        VehicleCenter,
                        VehicleHalfExtents,
                        in volume,
                        CurveLutSamples,
                        CurveLutSampleCount);

                    if (vehicleContribution > 0f)
                        AddContribution(ref result, volume.Type, vehicleContribution, volume.VisorGlitchBias, false);
                }
            }

            Result[0] = result;
        }

        private static void AddContribution(ref HazardExposureJobResult result, HazardType hazardType, float contribution, float visorGlitchBias, bool player)
        {
            int maskBit = 1 << (int)hazardType;
            if (player)
            {
                result.PlayerExposureMask = (byte)(result.PlayerExposureMask | maskBit);
                switch (hazardType)
                {
                    case HazardType.Radiation:
                        result.PlayerRadiation += contribution;
                        result.PlayerRadiationGlitchBias = math.max(result.PlayerRadiationGlitchBias, visorGlitchBias);
                        break;
                    case HazardType.Heat:
                        result.PlayerHeat += contribution;
                        result.PlayerHeatGlitchBias = math.max(result.PlayerHeatGlitchBias, visorGlitchBias);
                        break;
                    case HazardType.Toxicity:
                        result.PlayerToxicity += contribution;
                        result.PlayerToxicityGlitchBias = math.max(result.PlayerToxicityGlitchBias, visorGlitchBias);
                        break;
                    case HazardType.Biohazard:
                        result.PlayerBiohazard += contribution;
                        result.PlayerBiohazardGlitchBias = math.max(result.PlayerBiohazardGlitchBias, visorGlitchBias);
                        break;
                }

                return;
            }

            result.VehicleExposureMask = (byte)(result.VehicleExposureMask | maskBit);
            switch (hazardType)
            {
                case HazardType.Radiation:
                    result.VehicleRadiation += contribution;
                    result.VehicleRadiationGlitchBias = math.max(result.VehicleRadiationGlitchBias, visorGlitchBias);
                    break;
                case HazardType.Heat:
                    result.VehicleHeat += contribution;
                    result.VehicleHeatGlitchBias = math.max(result.VehicleHeatGlitchBias, visorGlitchBias);
                    break;
                case HazardType.Toxicity:
                    result.VehicleToxicity += contribution;
                    result.VehicleToxicityGlitchBias = math.max(result.VehicleToxicityGlitchBias, visorGlitchBias);
                    break;
                case HazardType.Biohazard:
                    result.VehicleBiohazard += contribution;
                    result.VehicleBiohazardGlitchBias = math.max(result.VehicleBiohazardGlitchBias, visorGlitchBias);
                    break;
            }
        }

        private static float EvaluateAabbSphereContribution(
            float3 aabbCenter,
            float3 aabbHalfExtents,
            in HazardVolumeData volume,
            NativeArray<float> curveLutSamples,
            int curveLutSampleCount)
        {
            float3 min = aabbCenter - aabbHalfExtents;
            float3 max = aabbCenter + aabbHalfExtents;
            float3 closestPoint = math.clamp(volume.AbsoluteUniversePosition, min, max);
            float3 offset = closestPoint - volume.AbsoluteUniversePosition;
            float distSqr = math.lengthsq(offset);
            if (distSqr >= volume.Radius * volume.Radius)
                return 0f;

            float normalizedDistance = math.saturate(math.sqrt(distSqr) * volume.InvRadius);
            float attenuation = SampleIntensityCurve(curveLutSamples, curveLutSampleCount, volume.CurveLutOffset, normalizedDistance);
            return volume.Intensity * attenuation;
        }

        private static float SampleIntensityCurve(NativeArray<float> curveLutSamples, int curveLutSampleCount, int curveLutOffset, float normalizedDistance)
        {
            if (!curveLutSamples.IsCreated || curveLutSampleCount <= 1)
                return ResolveDefaultCurveSample(normalizedDistance);

            float scaledIndex = normalizedDistance * (curveLutSampleCount - 1);
            int sampleIndex = (int)math.floor(scaledIndex);
            int nextIndex = math.min(curveLutSampleCount - 1, sampleIndex + 1);
            float fraction = scaledIndex - sampleIndex;
            float a = curveLutSamples[curveLutOffset + sampleIndex];
            float b = curveLutSamples[curveLutOffset + nextIndex];
            return math.lerp(a, b, fraction);
        }

        private static float ResolveDefaultCurveSample(float normalizedDistance)
        {
            float attenuation = 1f - (normalizedDistance * normalizedDistance);
            return attenuation > 0f ? attenuation * attenuation : 0f;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-5695)]
    public sealed class HazardZoneManager : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable
    {
        private const int HazardTypeCount = 4;
        private const int DefaultMaxZoneCount = 512;
        private const int MinZoneCapacity = 32;
        private const int MaxStepIterationsPerTick = 4;
        private const float HazardStepIntervalSeconds = 0.1f;
        private const float MinHazardRadius = 0.01f;
        private const double HazardSpatialCellSizeMeters = 12d;
        private const int HazardSpatialQueryCapacity = 64;
        private const int HazardSpatialLayerMask = 1 << 30;
        private const int HazardTypeMaskAll = (1 << HazardTypeCount) - 1;
        private const float ToxicityDoseThreshold = 1f;
        private const float ToxicityDoseDecayPerSecond = 0.18f;
        private const float ToxicityDamagePulseIntervalSeconds = 0.5f;
        private const float ToxicityDamagePerPulse = 1.1f;
        private const float ToxicityOverdoseDamageScale = 0.85f;
        private const float RadiationClarityTransferScale = 0.85f;
        private const float ThermalClarityTransferDenominator = 18f;
        private const float ToxicClarityTransferScale = 1.35f;
        private const float MinResistance = 0.1f;
        private const float MaxProtectedResistance = 1000f;
        private static readonly Vector3 DefaultPlayerBoundsSize = new Vector3(0.9f, 1.9f, 0.9f);
        private static readonly Vector3 DefaultTransportBoundsSize = new Vector3(2.2f, 1.6f, 3.8f);
        private static readonly string OverflowLogText = "[HazardZoneManager] Hazard registry capacity exceeded.";

        public static HazardZoneManager Instance { get; private set; }

        [Header("â”€â”€ Capacity â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Maximum simultaneous hazard volumes stored in the runtime registry.")]
        [SerializeField, Min(MinZoneCapacity)] private int maxZoneCount = DefaultMaxZoneCount;

        [Header("â”€â”€ Diagnostics â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField] private int _debugActiveZoneCount;
        [SerializeField] private float _debugToxicityDose;
        [SerializeField] private float _debugPlayerToxicityIntensity;
        [SerializeField] private float _debugVehicleToxicityIntensity;
        [SerializeField] private bool _debugJobRunning;
        [SerializeField] private bool _debugPlayerExposureActive;
        [SerializeField] private bool _debugVehicleExposureActive;

        private NativeArray<HazardVolumeData> _volumes;
        private NativeArray<int> _volumeIds;
        private NativeArray<int> _volumeSpatialHandles;
        private NativeArray<float> _volumeCurveLutSamples;
        private NativeArray<HazardVolumeData> _jobVolumes;
        private NativeArray<HazardExposureJobResult> _jobResult;
        private NativeArray<byte> _candidateVolumeFlags;
        private JobHandle _jobHandle;
        private HectonSpatialHash _spatialHash;
        private NativeList<int> _spatialQueryHandles;
        private bool _jobRunning;
        private bool _registered;
        private bool _serviceRegistered;
        private int _activeCount;
        private float _stepAccumulator;
        private float _toxicityDose;
        private float _toxicityDamageTimer;
        private int _publishedExposureMask;

        private Transform _playerTransform;
        private HectonSurvivalSystem _playerSurvival;
        private TraumaDispatcher _playerTraumaDispatcher;
        private PlayerTransportCoordinator _playerTransportCoordinator;
        private Collider _playerCollider;
        private IPlayerTransportLifecycleOwner _activeTransportOwner;
        private MonoBehaviour _activeTransportBehaviour;
        private Collider _activeTransportCollider;

        // COLD ALLOC: float[4] â€” cached player hazard intensities by HazardType â€” owner: HazardZoneManager
        private readonly float[] _playerHazardIntensity = new float[HazardTypeCount];
        // COLD ALLOC: float[4] â€” cached vehicle hazard intensities by HazardType â€” owner: HazardZoneManager
        private readonly float[] _vehicleHazardIntensity = new float[HazardTypeCount];
        // COLD ALLOC: float[4] â€” cached player hazard glitch bias by HazardType â€” owner: HazardZoneManager
        private readonly float[] _playerHazardGlitchBias = new float[HazardTypeCount];
        // COLD ALLOC: float[4] â€” cached vehicle hazard glitch bias by HazardType â€” owner: HazardZoneManager
        private readonly float[] _vehicleHazardGlitchBias = new float[HazardTypeCount];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        /// <summary>
        /// Ensures the runtime hazard host exists and returns the active manager.
        /// </summary>
        public static HazardZoneManager EnsureRuntimeInstance()
        {
            if (Instance != null)
                return Instance;

            EnvironmentRuntimeContextService environmentService = EnvironmentRuntimeContextService.EnsureRuntimeInstance();
            environmentService.InitializeService();
            return environmentService.HazardZones;
        }

        /// <summary>
        /// Registers or updates a spherical hazard volume in runtime absolute-universe space.
        /// </summary>
        public bool RegisterZone(int id, Vector3 runtimePosition, float intensity, float radius, HazardType type, float visorGlitchBias = 1f)
        {
            return RegisterZone(id, runtimePosition, intensity, radius, type, visorGlitchBias, null);
        }

        internal bool RegisterZone(int id, Vector3 runtimePosition, float intensity, float radius, HazardType type, float visorGlitchBias, HazardZoneProfile profile)
        {
            if (!_volumes.IsCreated)
                return false;

            int existingIndex = FindZoneIndex(id);
            if (existingIndex >= 0)
            {
                HazardVolumeData data = BuildVolumeData(existingIndex, runtimePosition, intensity, radius, type, visorGlitchBias);
                WriteVolumeCurveLut(existingIndex, profile);
                _volumes[existingIndex] = data;
                UpdateSpatialEntry(existingIndex, id, in data);
                return true;
            }

            if (_activeCount >= _volumes.Length)
            {
                LogRegistryOverflow();
                return false;
            }

            HazardVolumeData newData = BuildVolumeData(_activeCount, runtimePosition, intensity, radius, type, visorGlitchBias);
            _volumeIds[_activeCount] = id;
            _volumes[_activeCount] = newData;
            WriteVolumeCurveLut(_activeCount, profile);
            _volumeSpatialHandles[_activeCount] = RegisterSpatialEntry(id, in newData);
            _activeCount++;
            UpdateDiagnostics();
            return true;
        }

        /// <summary>
        /// Removes a previously registered hazard volume.
        /// </summary>
        public void UnregisterZone(int id)
        {
            if (!_volumes.IsCreated)
                return;

            int index = FindZoneIndex(id);
            if (index < 0)
                return;

            int lastIndex = _activeCount - 1;
            UnregisterSpatialEntry(index);
            if (index != lastIndex)
            {
                _volumeIds[index] = _volumeIds[lastIndex];
                _volumes[index] = _volumes[lastIndex];
                _volumeSpatialHandles[index] = _volumeSpatialHandles[lastIndex];
                CopyVolumeCurveLut(lastIndex, index);
                HazardVolumeData movedVolume = _volumes[index];
                movedVolume.CurveLutOffset = index * HazardZoneProfile.IntensityLutSampleCount;
                _volumes[index] = movedVolume;
                UpdateSpatialEntry(index, _volumeIds[index], in movedVolume);
            }

            _volumeIds[lastIndex] = 0;
            _volumes[lastIndex] = default;
            _volumeSpatialHandles[lastIndex] = 0;
            _activeCount = math.max(0, lastIndex);
            UpdateDiagnostics();
        }

        /// <summary>
        /// Returns the summed hazard intensity at the supplied runtime point.
        /// </summary>
        public float GetHazardIntensity(Vector3 runtimePoint, HazardType type)
        {
            if (!_volumes.IsCreated || _activeCount <= 0)
                return 0f;

            float3 absolutePoint = HectonFloatingOrigin.ToAbsoluteUniversePosition(runtimePoint);
            if (_spatialHash == null || !_spatialQueryHandles.IsCreated)
                return SumHazardIntensityLinear(absolutePoint, type);

            int candidateCount = _spatialHash.CollectSphere(
                AbsoluteUniversePosition.FromAbsolutePosition(new double3(absolutePoint.x, absolutePoint.y, absolutePoint.z)),
                MinHazardRadius,
                HazardSpatialLayerMask,
                _spatialQueryHandles);
            float totalIntensity = 0f;
            for (int i = 0; i < candidateCount; i++)
            {
                if (!_spatialHash.TryGetEntry(_spatialQueryHandles[i], out HectonSpatialHash.SpatialEntry entry))
                    continue;

                int index = FindZoneIndex(entry.PayloadId);
                if (index < 0)
                    continue;

                HazardVolumeData volume = _volumes[index];
                if (volume.Type != type)
                    continue;

                totalIntensity += EvaluatePointContribution(volume, absolutePoint);
            }

            return totalIntensity;
        }

        internal bool TrySampleHazardAvoidance(Vector3 runtimePoint, float sampleRadius, out Vector3 fleeDirection, out float hazardPressure01)
        {
            fleeDirection = Vector3.zero;
            hazardPressure01 = 0f;
            if (_spatialHash == null || !_spatialQueryHandles.IsCreated || _activeCount <= 0 || sampleRadius <= 0.001f)
                return false;

            float3 absolutePoint = HectonFloatingOrigin.ToAbsoluteUniversePosition(runtimePoint);
            int candidateCount = _spatialHash.CollectSphere(
                AbsoluteUniversePosition.FromAbsolutePosition(new double3(absolutePoint.x, absolutePoint.y, absolutePoint.z)),
                sampleRadius,
                HazardSpatialLayerMask,
                _spatialQueryHandles);
            if (candidateCount <= 0)
                return false;

            float3 accumulatedAway = float3.zero;
            float peakPressure = 0f;
            for (int i = 0; i < candidateCount; i++)
            {
                if (!_spatialHash.TryGetEntry(_spatialQueryHandles[i], out HectonSpatialHash.SpatialEntry entry))
                    continue;

                int index = FindZoneIndex(entry.PayloadId);
                if (index < 0)
                    continue;

                HazardVolumeData volume = _volumes[index];
                float contribution = EvaluatePointContribution(volume, absolutePoint);
                if (contribution <= 0.001f)
                    continue;

                float pressure = NormalizeHazardClarityContribution(volume.Type, contribution);
                if (pressure <= 0.001f)
                    continue;

                float3 away = absolutePoint - volume.AbsoluteUniversePosition;
                if (math.lengthsq(away) <= 0.0001f)
                    away = new float3(0f, 1f, 0f);

                accumulatedAway += math.normalizesafe(away, new float3(0f, 1f, 0f)) * pressure;
                if (pressure > peakPressure)
                    peakPressure = pressure;
            }

            if (peakPressure <= 0.001f || math.lengthsq(accumulatedAway) <= 0.0001f)
                return false;

            fleeDirection = math.normalizesafe(accumulatedAway, new float3(0f, 1f, 0f));
            hazardPressure01 = peakPressure;
            return true;
        }

        /// <summary>Current toxicity dose accumulated by the local player.</summary>
        public float ToxicityDose => _toxicityDose;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            AllocateNativeState();
            ResolvePlayerContext();
            UpdateDiagnostics();
        }

        private void OnEnable()
        {
            if (Instance == null)
                Instance = this;

            AllocateNativeState();
            ResolvePlayerContext();
            TryRegister();
            TryRegisterService();
            UpdateDiagnostics();
        }

        private void OnDisable()
        {
            PublishExposureMask(0);
            TryUnregister();
            TryUnregisterService();
            ClearRuntimeState();
            DisposeNativeState();
        }

        private void OnDestroy()
        {
            PublishExposureMask(0);
            TryUnregister();
            TryUnregisterService();
            ClearRuntimeState();
            DisposeNativeState();
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Runs the 10Hz hazard step without using MonoBehaviour Update.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || !_volumes.IsCreated)
                return;

            _stepAccumulator += deltaTime;
            int iterations = 0;
            while (_stepAccumulator >= HazardStepIntervalSeconds && iterations < MaxStepIterationsPerTick)
            {
                AdvanceHazardStep(HazardStepIntervalSeconds);
                _stepAccumulator -= HazardStepIntervalSeconds;
                iterations++;
            }
        }

        private void AdvanceHazardStep(float dt)
        {
            ResolvePlayerContext();
            ApplyToxicityDose(dt);
            ScheduleExposureJob();
            UpdateDiagnostics();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            ConsumeCompletedJob();
        }

        private void AllocateNativeState()
        {
            if (_volumes.IsCreated)
                return;

            int safeCapacity = math.max(MinZoneCapacity, maxZoneCount);
            _volumes = new NativeArray<HazardVolumeData>(safeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _volumeIds = new NativeArray<int>(safeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _volumeSpatialHandles = new NativeArray<int>(safeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _volumeCurveLutSamples = new NativeArray<float>(safeCapacity * HazardZoneProfile.IntensityLutSampleCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _jobVolumes = new NativeArray<HazardVolumeData>(safeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _jobResult = new NativeArray<HazardExposureJobResult>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _candidateVolumeFlags = new NativeArray<byte>(safeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _spatialHash = new HectonSpatialHash(safeCapacity, safeCapacity * 6, HazardSpatialCellSizeMeters);
            _spatialQueryHandles = new NativeList<int>(HazardSpatialQueryCapacity, Allocator.Persistent);
        }

        private void DisposeNativeState()
        {
            if (_volumes.IsCreated)
            {
                _volumes.Dispose();
                _volumes = default;
            }

            if (_volumeIds.IsCreated)
            {
                _volumeIds.Dispose();
                _volumeIds = default;
            }

            if (_volumeSpatialHandles.IsCreated)
            {
                _volumeSpatialHandles.Dispose();
                _volumeSpatialHandles = default;
            }

            if (_volumeCurveLutSamples.IsCreated)
            {
                _volumeCurveLutSamples.Dispose();
                _volumeCurveLutSamples = default;
            }

            JobHandle disposeHandle = _jobRunning ? _jobHandle : default;
            _jobRunning = false;

            if (_jobVolumes.IsCreated)
            {
                _jobVolumes.Dispose(disposeHandle);
                _jobVolumes = default;
            }

            if (_jobResult.IsCreated)
            {
                _jobResult.Dispose(disposeHandle);
                _jobResult = default;
            }

            if (_candidateVolumeFlags.IsCreated)
            {
                _candidateVolumeFlags.Dispose(disposeHandle);
                _candidateVolumeFlags = default;
            }

            if (_spatialQueryHandles.IsCreated)
            {
                _spatialQueryHandles.Dispose();
                _spatialQueryHandles = default;
            }

            _spatialHash?.Dispose();
            _spatialHash = null;
        }

        private void ResolvePlayerContext()
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null)
            {
                if (playerContext.PlayerTransform != null)
                    _playerTransform = playerContext.PlayerTransform;

                if (playerContext.PlayerCollider != null)
                    _playerCollider = playerContext.PlayerCollider;
            }

            if (_playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref _playerTransform);

            if (_playerTransform == null)
                return;

            if (_playerSurvival == null || !ReferenceEquals(_playerSurvival.transform, _playerTransform))
                _playerTransform.TryGetComponent(out _playerSurvival);

            if (_playerTraumaDispatcher == null || !ReferenceEquals(_playerTraumaDispatcher.transform, _playerTransform))
                _playerTransform.TryGetComponent(out _playerTraumaDispatcher);

            if (_playerTransportCoordinator == null || !ReferenceEquals(_playerTransportCoordinator.transform, _playerTransform))
                _playerTransform.TryGetComponent(out _playerTransportCoordinator);

            if (_playerCollider == null)
                _playerCollider = ResolvePrimaryCollider(_playerTransform);

            IPlayerTransportLifecycleOwner resolvedOwner = null;
            if (_playerTransportCoordinator != null)
                _playerTransportCoordinator.TryResolveTransportLifecycleOwner(out resolvedOwner);

            if (ReferenceEquals(_activeTransportOwner, resolvedOwner))
                return;

            _activeTransportOwner = resolvedOwner;
            _activeTransportBehaviour = resolvedOwner as MonoBehaviour;
            _activeTransportCollider = _activeTransportBehaviour != null
                ? ResolvePrimaryCollider(_activeTransportBehaviour.transform)
                : null;
        }

        private void ConsumeCompletedJob()
        {
            if (!_jobRunning)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _jobHandle, forceComplete: false))
                return;

            _jobRunning = false;

            HazardExposureJobResult result = _jobResult[0];
            _playerHazardIntensity[(int)HazardType.Radiation] = result.PlayerRadiation;
            _playerHazardIntensity[(int)HazardType.Heat] = result.PlayerHeat;
            _playerHazardIntensity[(int)HazardType.Toxicity] = result.PlayerToxicity;
            _playerHazardIntensity[(int)HazardType.Biohazard] = result.PlayerBiohazard;
            _playerHazardGlitchBias[(int)HazardType.Radiation] = result.PlayerRadiationGlitchBias;
            _playerHazardGlitchBias[(int)HazardType.Heat] = result.PlayerHeatGlitchBias;
            _playerHazardGlitchBias[(int)HazardType.Toxicity] = result.PlayerToxicityGlitchBias;
            _playerHazardGlitchBias[(int)HazardType.Biohazard] = result.PlayerBiohazardGlitchBias;
            _vehicleHazardIntensity[(int)HazardType.Radiation] = result.VehicleRadiation;
            _vehicleHazardIntensity[(int)HazardType.Heat] = result.VehicleHeat;
            _vehicleHazardIntensity[(int)HazardType.Toxicity] = result.VehicleToxicity;
            _vehicleHazardIntensity[(int)HazardType.Biohazard] = result.VehicleBiohazard;
            _vehicleHazardGlitchBias[(int)HazardType.Radiation] = result.VehicleRadiationGlitchBias;
            _vehicleHazardGlitchBias[(int)HazardType.Heat] = result.VehicleHeatGlitchBias;
            _vehicleHazardGlitchBias[(int)HazardType.Toxicity] = result.VehicleToxicityGlitchBias;
            _vehicleHazardGlitchBias[(int)HazardType.Biohazard] = result.VehicleBiohazardGlitchBias;

            PublishExposureMask(result.PlayerExposureMask | result.VehicleExposureMask);
            DispatchClarityTraumaSignals();
        }

        private void ApplyToxicityDose(float dt)
        {
            float currentToxicityIntensity = math.max(
                _playerHazardIntensity[(int)HazardType.Toxicity],
                _vehicleHazardIntensity[(int)HazardType.Toxicity]);

            if (currentToxicityIntensity > 0.001f)
            {
                float resistance = ResolveToxicityResistance();
                _toxicityDose += (currentToxicityIntensity / resistance) * dt;
            }
            else
            {
                _toxicityDose = math.max(0f, _toxicityDose - ToxicityDoseDecayPerSecond * dt);
                if (_toxicityDose <= ToxicityDoseThreshold)
                    _toxicityDamageTimer = 0f;
            }

            if (_toxicityDose <= ToxicityDoseThreshold || _playerSurvival == null)
                return;

            _toxicityDamageTimer += dt;
            while (_toxicityDamageTimer >= ToxicityDamagePulseIntervalSeconds)
            {
                _toxicityDamageTimer -= ToxicityDamagePulseIntervalSeconds;
                ApplyToxicityDamagePulse(currentToxicityIntensity);
            }
        }

        private void ApplyToxicityDamagePulse(float currentIntensity)
        {
            if (_playerSurvival == null)
                return;

            float previousIntegrityNormalized = _playerSurvival.IntegrityNormalized;
            float overdose = math.max(0f, _toxicityDose - ToxicityDoseThreshold);
            float damageMagnitude = ToxicityDamagePerPulse *
                                    math.max(0.25f, currentIntensity) *
                                    (1f + overdose * ToxicityOverdoseDamageScale);

            _playerSurvival.TakeDamage(damageMagnitude);
            float nextIntegrityNormalized = _playerSurvival.IntegrityNormalized;
            if (_playerTraumaDispatcher == null || Mathf.Abs(nextIntegrityNormalized - previousIntegrityNormalized) <= 0.0001f)
                return;

            DamageSignal signal = default;
            signal.magnitude = damageMagnitude;
            signal.localPoint = float3.zero;
            signal.damageType = (uint)DamageTypeMask.Parasite;
            signal.integrityDelta = (byte)Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Abs(nextIntegrityNormalized - previousIntegrityNormalized) * byte.MaxValue),
                0,
                byte.MaxValue);
            signal.depth = _playerSurvival.Depth;
            signal.sourceID = DamageSourceIds.EnvironmentHazard;

            _playerTraumaDispatcher.OnIntegrityChanged(previousIntegrityNormalized, nextIntegrityNormalized, signal);
        }

        private float ResolveToxicityResistance()
        {
            if (_playerSurvival == null)
                return 1f;

            return Mathf.Clamp(
                _playerSurvival.ResolveEnvironmentalResistance(HazardType.Toxicity),
                MinResistance,
                MaxProtectedResistance);
        }

        private void ScheduleExposureJob()
        {
            if (_jobRunning || !_jobVolumes.IsCreated)
                return;

            bool hasPlayerBounds = TryBuildQueryBounds(
                _playerTransform,
                _playerCollider,
                DefaultPlayerBoundsSize,
                out float3 playerCenter,
                out float3 playerHalfExtents);
            bool hasVehicleBounds = TryBuildVehicleQueryBounds(out float3 vehicleCenter, out float3 vehicleHalfExtents);
            if (!hasPlayerBounds && !hasVehicleBounds)
            {
                ClearExposureState();
                return;
            }

            int candidateCount = CollectCandidateVolumes(
                hasPlayerBounds,
                playerCenter,
                playerHalfExtents,
                hasVehicleBounds,
                vehicleCenter,
                vehicleHalfExtents);
            if (candidateCount <= 0)
            {
                ClearExposureState();
                return;
            }

            _jobResult[0] = default;
            EvaluateHazardExposureJob job = new EvaluateHazardExposureJob
            {
                Volumes = _jobVolumes,
                CurveLutSamples = _volumeCurveLutSamples,
                CurveLutSampleCount = HazardZoneProfile.IntensityLutSampleCount,
                VolumeCount = candidateCount,
                HasPlayerBounds = hasPlayerBounds,
                HasVehicleBounds = hasVehicleBounds,
                PlayerCenter = playerCenter,
                PlayerHalfExtents = playerHalfExtents,
                VehicleCenter = vehicleCenter,
                VehicleHalfExtents = vehicleHalfExtents,
                Result = _jobResult
            };

            _jobHandle = job.Schedule();
            _jobRunning = true;
        }

        private bool TryBuildVehicleQueryBounds(out float3 center, out float3 halfExtents)
        {
            center = default;
            halfExtents = default;

            if (_activeTransportBehaviour == null)
                return false;

            return TryBuildQueryBounds(
                _activeTransportBehaviour.transform,
                _activeTransportCollider,
                DefaultTransportBoundsSize,
                out center,
                out halfExtents);
        }

        private static bool TryBuildQueryBounds(
            Transform targetTransform,
            Collider targetCollider,
            Vector3 fallbackSize,
            out float3 center,
            out float3 halfExtents)
        {
            center = default;
            halfExtents = default;
            if (targetTransform == null)
                return false;

            Bounds bounds;
            if (targetCollider != null)
            {
                bounds = targetCollider.bounds;
                if (bounds.size.sqrMagnitude <= 0.0001f)
                    bounds = new Bounds(targetTransform.position, fallbackSize);
            }
            else
            {
                bounds = new Bounds(targetTransform.position, fallbackSize);
            }

            center = HectonFloatingOrigin.ToAbsoluteUniversePosition(bounds.center);
            halfExtents = bounds.extents;
            return math.all(halfExtents > 0f);
        }

        private static Collider ResolvePrimaryCollider(Transform root)
        {
            if (root == null)
                return null;

            root.TryGetComponent(out Collider directCollider);
            return directCollider;
        }

        private int CollectCandidateVolumes(
            bool hasPlayerBounds,
            float3 playerCenter,
            float3 playerHalfExtents,
            bool hasVehicleBounds,
            float3 vehicleCenter,
            float3 vehicleHalfExtents)
        {
            if (_spatialHash == null || !_candidateVolumeFlags.IsCreated || !_spatialQueryHandles.IsCreated)
            {
                for (int i = 0; i < _activeCount; i++)
                    _jobVolumes[i] = _volumes[i];

                return _activeCount;
            }

            for (int i = 0; i < _activeCount; i++)
                _candidateVolumeFlags[i] = 0;

            int candidateCount = 0;
            if (hasPlayerBounds)
            {
                candidateCount = AppendCandidateVolumes(
                    playerCenter,
                    math.max(MinHazardRadius, math.length(playerHalfExtents)),
                    candidateCount);
            }

            if (hasVehicleBounds)
            {
                candidateCount = AppendCandidateVolumes(
                    vehicleCenter,
                    math.max(MinHazardRadius, math.length(vehicleHalfExtents)),
                    candidateCount);
            }

            return candidateCount;
        }

        private int AppendCandidateVolumes(float3 absoluteCenter, float queryRadius, int candidateCount)
        {
            int handleCount = _spatialHash.CollectSphere(
                AbsoluteUniversePosition.FromAbsolutePosition(new double3(absoluteCenter.x, absoluteCenter.y, absoluteCenter.z)),
                queryRadius,
                HazardSpatialLayerMask,
                _spatialQueryHandles);
            for (int i = 0; i < handleCount; i++)
            {
                if (!_spatialHash.TryGetEntry(_spatialQueryHandles[i], out HectonSpatialHash.SpatialEntry entry))
                    continue;

                int zoneIndex = FindZoneIndex(entry.PayloadId);
                if (zoneIndex < 0 || _candidateVolumeFlags[zoneIndex] != 0)
                    continue;

                _candidateVolumeFlags[zoneIndex] = 1;
                _jobVolumes[candidateCount] = _volumes[zoneIndex];
                candidateCount++;
            }

            return candidateCount;
        }

        private static float EvaluatePointContribution(HazardVolumeData volume, float3 absolutePoint)
        {
            float3 offset = volume.AbsoluteUniversePosition - absolutePoint;
            float distSqr = math.lengthsq(offset);
            if (distSqr >= volume.Radius * volume.Radius)
                return 0f;

            float normalizedDistance = math.saturate(math.sqrt(distSqr) * volume.InvRadius);
            float attenuation = ResolveVolumeCurveSample(normalizedDistance);
            if (Instance != null && Instance._volumeCurveLutSamples.IsCreated)
                attenuation = Instance.SampleIntensityCurve(volume.CurveLutOffset, normalizedDistance);

            return volume.Intensity * attenuation;
        }

        private HazardVolumeData BuildVolumeData(int volumeIndex, Vector3 runtimePosition, float intensity, float radius, HazardType type, float visorGlitchBias)
        {
            float safeRadius = radius > MinHazardRadius ? radius : MinHazardRadius;
            HazardVolumeData data = default;
            data.AbsoluteUniversePosition = HectonFloatingOrigin.ToAbsoluteUniversePosition(runtimePosition);
            data.Radius = safeRadius;
            data.InvRadius = 1f / safeRadius;
            data.InvRadiusSqr = 1f / (safeRadius * safeRadius);
            data.Intensity = Mathf.Max(0f, intensity);
            data.VisorGlitchBias = Mathf.Clamp(visorGlitchBias, 0f, 2f);
            data.CurveLutOffset = volumeIndex * HazardZoneProfile.IntensityLutSampleCount;
            data.Type = type;
            return data;
        }

        private void WriteVolumeCurveLut(int volumeIndex, HazardZoneProfile profile)
        {
            if (!_volumeCurveLutSamples.IsCreated)
                return;

            int lutOffset = volumeIndex * HazardZoneProfile.IntensityLutSampleCount;
            float[] bakedLut = profile != null ? profile.BakedIntensityLut : null;
            if (bakedLut == null || bakedLut.Length < HazardZoneProfile.IntensityLutSampleCount)
            {
                WriteDefaultCurveLut(lutOffset);
                return;
            }

            for (int i = 0; i < HazardZoneProfile.IntensityLutSampleCount; i++)
                _volumeCurveLutSamples[lutOffset + i] = Mathf.Clamp01(bakedLut[i]);
        }

        private void CopyVolumeCurveLut(int sourceIndex, int targetIndex)
        {
            if (!_volumeCurveLutSamples.IsCreated || sourceIndex == targetIndex)
                return;

            int sampleCount = HazardZoneProfile.IntensityLutSampleCount;
            int sourceOffset = sourceIndex * sampleCount;
            int targetOffset = targetIndex * sampleCount;
            for (int i = 0; i < sampleCount; i++)
                _volumeCurveLutSamples[targetOffset + i] = _volumeCurveLutSamples[sourceOffset + i];
        }

        private void WriteDefaultCurveLut(int lutOffset)
        {
            if (!_volumeCurveLutSamples.IsCreated)
                return;

            for (int i = 0; i < HazardZoneProfile.IntensityLutSampleCount; i++)
            {
                float normalizedDistance = HazardZoneProfile.IntensityLutSampleCount > 1
                    ? i / (float)(HazardZoneProfile.IntensityLutSampleCount - 1)
                    : 0f;
                _volumeCurveLutSamples[lutOffset + i] = ResolveVolumeCurveSample(normalizedDistance);
            }
        }

        private float SampleIntensityCurve(int curveLutOffset, float normalizedDistance)
        {
            if (!_volumeCurveLutSamples.IsCreated)
                return ResolveVolumeCurveSample(normalizedDistance);

            float scaledIndex = Mathf.Clamp01(normalizedDistance) * (HazardZoneProfile.IntensityLutSampleCount - 1);
            int sampleIndex = Mathf.FloorToInt(scaledIndex);
            int nextIndex = Mathf.Min(HazardZoneProfile.IntensityLutSampleCount - 1, sampleIndex + 1);
            float fraction = scaledIndex - sampleIndex;
            float a = _volumeCurveLutSamples[curveLutOffset + sampleIndex];
            float b = _volumeCurveLutSamples[curveLutOffset + nextIndex];
            return Mathf.Lerp(a, b, fraction);
        }

        private static float ResolveVolumeCurveSample(float normalizedDistance)
        {
            float safeDistance = math.saturate(normalizedDistance);
            float attenuation = 1f - (safeDistance * safeDistance);
            return attenuation > 0f ? attenuation * attenuation : 0f;
        }

        private int FindZoneIndex(int id)
        {
            for (int i = 0; i < _activeCount; i++)
            {
                if (_volumeIds[i] == id)
                    return i;
            }

            return -1;
        }

        private void ClearRuntimeState()
        {
            if (_jobRunning && DispatcherJobSwap.TryComplete(ref _jobHandle, forceComplete: false))
            {
                _jobRunning = false;
            }

            _activeCount = 0;
            _stepAccumulator = 0f;
            _toxicityDose = 0f;
            _toxicityDamageTimer = 0f;
            _publishedExposureMask = 0;
            _activeTransportOwner = null;
            _activeTransportBehaviour = null;
            _activeTransportCollider = null;
            _playerCollider = null;

            for (int i = 0; i < HazardTypeCount; i++)
            {
                _playerHazardIntensity[i] = 0f;
                _vehicleHazardIntensity[i] = 0f;
                _playerHazardGlitchBias[i] = 0f;
                _vehicleHazardGlitchBias[i] = 0f;
            }
        }

        private void ClearExposureState()
        {
            for (int i = 0; i < HazardTypeCount; i++)
            {
                _playerHazardIntensity[i] = 0f;
                _vehicleHazardIntensity[i] = 0f;
                _playerHazardGlitchBias[i] = 0f;
                _vehicleHazardGlitchBias[i] = 0f;
            }

            PublishExposureMask(0);
        }

        private void PublishExposureMask(int nextMask)
        {
            if (nextMask == _publishedExposureMask)
                return;

            int enteredMask = nextMask & ~_publishedExposureMask;
            int exitedMask = _publishedExposureMask & ~nextMask;
            EmitExposureMaskDelta(enteredMask, true);
            EmitExposureMaskDelta(exitedMask, false);
            _publishedExposureMask = nextMask;
        }

        private static void EmitExposureMaskDelta(int mask, bool entering)
        {
            if (mask == 0)
                return;

            for (int i = 0; i < HazardTypeCount; i++)
            {
                if ((mask & (1 << i)) == 0)
                    continue;

                HazardType type = (HazardType)i;
                if (entering)
                    HazardExposureNotifier.Enter(type);
                else
                    HazardExposureNotifier.Exit(type);
            }
        }

        private void DispatchClarityTraumaSignals()
        {
            if (_playerTraumaDispatcher == null)
                return;

            DispatchClarityHazardSignal(HazardType.Radiation, (uint)DamageTypeMask.Radioactive);
            DispatchClarityHazardSignal(HazardType.Heat, (uint)DamageTypeMask.Thermal);
            DispatchClarityHazardSignal(HazardType.Toxicity, (uint)DamageTypeMask.Toxic);
        }

        private void DispatchClarityHazardSignal(HazardType hazardType, uint damageMask)
        {
            int hazardIndex = (int)hazardType;
            float intensity = math.max(_playerHazardIntensity[hazardIndex], _vehicleHazardIntensity[hazardIndex]);
            if (intensity <= 0.001f)
                return;

            float resistance = ResolveHazardResistance(hazardType);
            float clarityImpulse = NormalizeHazardClarityContribution(hazardType, intensity / resistance);
            float visorBias = math.max(_playerHazardGlitchBias[hazardIndex], _vehicleHazardGlitchBias[hazardIndex]);
            if (visorBias > 0.001f)
                clarityImpulse = Mathf.Clamp01(clarityImpulse * visorBias);

            if (clarityImpulse <= 0.001f)
                return;

            DamageSignal signal = default;
            signal.magnitude = clarityImpulse;
            signal.localPoint = float3.zero;
            signal.damageType = damageMask;
            signal.integrityDelta = 0;
            signal.depth = _playerSurvival != null ? _playerSurvival.Depth : 0f;
            signal.sourceID = DamageSourceIds.EnvironmentHazard;
            _playerTraumaDispatcher.OnClarityChanged(0f, clarityImpulse, signal);
        }

        private float ResolveHazardResistance(HazardType hazardType)
        {
            if (_playerSurvival == null)
                return 1f;

            return Mathf.Clamp(
                _playerSurvival.ResolveEnvironmentalResistance(hazardType),
                MinResistance,
                MaxProtectedResistance);
        }

        private static float NormalizeHazardClarityContribution(HazardType hazardType, float exposure)
        {
            float safeExposure = math.max(0f, exposure);
            switch (hazardType)
            {
                case HazardType.Radiation:
                    return 1f - math.exp(-(safeExposure * RadiationClarityTransferScale));

                case HazardType.Heat:
                    return 1f - math.exp(-(safeExposure / math.max(0.01f, ThermalClarityTransferDenominator)));

                case HazardType.Toxicity:
                    return 1f - math.exp(-(safeExposure * ToxicClarityTransferScale));

                default:
                    return math.saturate(safeExposure);
            }
        }

        private float SumHazardIntensityLinear(float3 absolutePoint, HazardType type)
        {
            float totalIntensity = 0f;
            for (int i = 0; i < _activeCount; i++)
            {
                HazardVolumeData volume = _volumes[i];
                if (volume.Type != type)
                    continue;

                totalIntensity += EvaluatePointContribution(volume, absolutePoint);
            }

            return totalIntensity;
        }

        private int RegisterSpatialEntry(int id, in HazardVolumeData data)
        {
            if (_spatialHash == null)
                return 0;

            return _spatialHash.Register(
                AbsoluteUniversePosition.FromAbsolutePosition(new double3(
                    data.AbsoluteUniversePosition.x,
                    data.AbsoluteUniversePosition.y,
                    data.AbsoluteUniversePosition.z)),
                new float3(data.Radius, data.Radius, data.Radius),
                ResolveSpatialKindMask(data.Type),
                0u,
                id);
        }

        private void UpdateSpatialEntry(int index, int id, in HazardVolumeData data)
        {
            if (_spatialHash == null || !_volumeSpatialHandles.IsCreated || index < 0 || index >= _volumeSpatialHandles.Length)
                return;

            int handle = _volumeSpatialHandles[index];
            if (handle <= 0)
            {
                _volumeSpatialHandles[index] = RegisterSpatialEntry(id, in data);
                return;
            }

            _spatialHash.UpdateEntry(
                handle,
                AbsoluteUniversePosition.FromAbsolutePosition(new double3(
                    data.AbsoluteUniversePosition.x,
                    data.AbsoluteUniversePosition.y,
                    data.AbsoluteUniversePosition.z)),
                new float3(data.Radius, data.Radius, data.Radius),
                ResolveSpatialKindMask(data.Type),
                0u,
                id);
        }

        private void UnregisterSpatialEntry(int index)
        {
            if (_spatialHash == null || !_volumeSpatialHandles.IsCreated || index < 0 || index >= _volumeSpatialHandles.Length)
                return;

            int handle = _volumeSpatialHandles[index];
            if (handle <= 0)
                return;

            _spatialHash.Unregister(handle);
            _volumeSpatialHandles[index] = 0;
        }

        private static int ResolveSpatialKindMask(HazardType type)
        {
            return HazardSpatialLayerMask | ResolveHazardTypeMask(type);
        }

        private static int ResolveHazardTypeMask(HazardType type)
        {
            return 1 << (int)type;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Environment);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying || Instance != this)
                return;

            GlobalRegistry.RegisterHazardZoneRuntime(this);
            _serviceRegistered = true;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterHazardZoneRuntime(this);
            _serviceRegistered = false;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogRegistryOverflow()
        {
            UnityEngine.Debug.LogWarning(OverflowLogText);
        }

        [Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics()
        {
            _debugActiveZoneCount = _activeCount;
            _debugToxicityDose = _toxicityDose;
            _debugPlayerToxicityIntensity = _playerHazardIntensity[(int)HazardType.Toxicity];
            _debugVehicleToxicityIntensity = _vehicleHazardIntensity[(int)HazardType.Toxicity];
            _debugJobRunning = _jobRunning;
            _debugPlayerExposureActive = (_publishedExposureMask & (1 << (int)HazardType.Toxicity)) != 0;
            _debugVehicleExposureActive = _vehicleHazardIntensity[(int)HazardType.Toxicity] > 0.001f;
        }
    }
}
