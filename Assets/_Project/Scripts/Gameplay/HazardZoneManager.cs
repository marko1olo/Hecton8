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
        public float InvRadiusSqr;
        public float Intensity;
        public HazardType Type;
    }

    internal struct HazardExposureJobResult
    {
        public float PlayerRadiation;
        public float PlayerHeat;
        public float PlayerToxicity;
        public float PlayerBiohazard;
        public float VehicleRadiation;
        public float VehicleHeat;
        public float VehicleToxicity;
        public float VehicleBiohazard;
        public byte PlayerExposureMask;
        public byte VehicleExposureMask;
    }

    [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast)]
    internal struct EvaluateHazardExposureJob : IJob
    {
        [ReadOnly] public NativeArray<HazardVolumeData> Volumes;
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
                        volume.AbsoluteUniversePosition,
                        volume.InvRadiusSqr,
                        volume.Radius,
                        volume.Intensity);

                    if (playerContribution > 0f)
                        AddContribution(ref result, volume.Type, playerContribution, true);
                }

                if (HasVehicleBounds)
                {
                    float vehicleContribution = EvaluateAabbSphereContribution(
                        VehicleCenter,
                        VehicleHalfExtents,
                        volume.AbsoluteUniversePosition,
                        volume.InvRadiusSqr,
                        volume.Radius,
                        volume.Intensity);

                    if (vehicleContribution > 0f)
                        AddContribution(ref result, volume.Type, vehicleContribution, false);
                }
            }

            Result[0] = result;
        }

        private static void AddContribution(ref HazardExposureJobResult result, HazardType hazardType, float contribution, bool player)
        {
            int maskBit = 1 << (int)hazardType;
            if (player)
            {
                result.PlayerExposureMask = (byte)(result.PlayerExposureMask | maskBit);
                switch (hazardType)
                {
                    case HazardType.Radiation:
                        result.PlayerRadiation += contribution;
                        break;
                    case HazardType.Heat:
                        result.PlayerHeat += contribution;
                        break;
                    case HazardType.Toxicity:
                        result.PlayerToxicity += contribution;
                        break;
                    case HazardType.Biohazard:
                        result.PlayerBiohazard += contribution;
                        break;
                }

                return;
            }

            result.VehicleExposureMask = (byte)(result.VehicleExposureMask | maskBit);
            switch (hazardType)
            {
                case HazardType.Radiation:
                    result.VehicleRadiation += contribution;
                    break;
                case HazardType.Heat:
                    result.VehicleHeat += contribution;
                    break;
                case HazardType.Toxicity:
                    result.VehicleToxicity += contribution;
                    break;
                case HazardType.Biohazard:
                    result.VehicleBiohazard += contribution;
                    break;
            }
        }

        private static float EvaluateAabbSphereContribution(
            float3 aabbCenter,
            float3 aabbHalfExtents,
            float3 sphereCenter,
            float invRadiusSqr,
            float radius,
            float intensity)
        {
            float3 min = aabbCenter - aabbHalfExtents;
            float3 max = aabbCenter + aabbHalfExtents;
            float3 closestPoint = math.clamp(sphereCenter, min, max);
            float3 offset = closestPoint - sphereCenter;
            float distSqr = math.lengthsq(offset);
            if (distSqr >= radius * radius)
                return 0f;

            float attenuation = 1f - (distSqr * invRadiusSqr);
            return intensity * (attenuation * attenuation);
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-5695)]
    public sealed class HazardZoneManager : MonoBehaviour, ITickable, IUpdatable
    {
        private const int HazardTypeCount = 4;
        private const int DefaultMaxZoneCount = 512;
        private const int MinZoneCapacity = 32;
        private const int MaxStepIterationsPerTick = 4;
        private const float HazardStepIntervalSeconds = 0.1f;
        private const float MinHazardRadius = 0.01f;
        private const float ToxicityDoseThreshold = 1f;
        private const float ToxicityDoseDecayPerSecond = 0.18f;
        private const float ToxicityDamagePulseIntervalSeconds = 0.5f;
        private const float ToxicityDamagePerPulse = 1.1f;
        private const float ToxicityOverdoseDamageScale = 0.85f;
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
        private NativeArray<HazardVolumeData> _jobVolumes;
        private NativeArray<HazardExposureJobResult> _jobResult;
        private JobHandle _jobHandle;
        private bool _jobRunning;
        private bool _registered;
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
        public bool RegisterZone(int id, Vector3 runtimePosition, float intensity, float radius, HazardType type)
        {
            if (!_volumes.IsCreated)
                return false;

            int existingIndex = FindZoneIndex(id);
            HazardVolumeData data = BuildVolumeData(runtimePosition, intensity, radius, type);
            if (existingIndex >= 0)
            {
                _volumes[existingIndex] = data;
                return true;
            }

            if (_activeCount >= _volumes.Length)
            {
                LogRegistryOverflow();
                return false;
            }

            _volumeIds[_activeCount] = id;
            _volumes[_activeCount] = data;
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
            if (index != lastIndex)
            {
                _volumeIds[index] = _volumeIds[lastIndex];
                _volumes[index] = _volumes[lastIndex];
            }

            _volumeIds[lastIndex] = 0;
            _volumes[lastIndex] = default;
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
            UpdateDiagnostics();
        }

        private void OnDisable()
        {
            PublishExposureMask(0);
            TryUnregister();
            ClearRuntimeState();
            DisposeNativeState();
        }

        private void OnDestroy()
        {
            PublishExposureMask(0);
            TryUnregister();
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
            ConsumeCompletedJob();
            ApplyToxicityDose(dt);
            ScheduleExposureJob();
            UpdateDiagnostics();
        }

        private void AllocateNativeState()
        {
            if (_volumes.IsCreated)
                return;

            int safeCapacity = math.max(MinZoneCapacity, maxZoneCount);
            _volumes = new NativeArray<HazardVolumeData>(safeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _volumeIds = new NativeArray<int>(safeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _jobVolumes = new NativeArray<HazardVolumeData>(safeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _jobResult = new NativeArray<HazardExposureJobResult>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
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
            if (!_jobRunning || !_jobHandle.IsCompleted)
                return;

            _jobHandle.Complete();
            _jobRunning = false;

            HazardExposureJobResult result = _jobResult[0];
            _playerHazardIntensity[(int)HazardType.Radiation] = result.PlayerRadiation;
            _playerHazardIntensity[(int)HazardType.Heat] = result.PlayerHeat;
            _playerHazardIntensity[(int)HazardType.Toxicity] = result.PlayerToxicity;
            _playerHazardIntensity[(int)HazardType.Biohazard] = result.PlayerBiohazard;
            _vehicleHazardIntensity[(int)HazardType.Radiation] = result.VehicleRadiation;
            _vehicleHazardIntensity[(int)HazardType.Heat] = result.VehicleHeat;
            _vehicleHazardIntensity[(int)HazardType.Toxicity] = result.VehicleToxicity;
            _vehicleHazardIntensity[(int)HazardType.Biohazard] = result.VehicleBiohazard;

            PublishExposureMask(result.PlayerExposureMask | result.VehicleExposureMask);
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
                return;

            for (int i = 0; i < _activeCount; i++)
                _jobVolumes[i] = _volumes[i];

            _jobResult[0] = default;
            EvaluateHazardExposureJob job = new EvaluateHazardExposureJob
            {
                Volumes = _jobVolumes,
                VolumeCount = _activeCount,
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

        private static float EvaluatePointContribution(HazardVolumeData volume, float3 absolutePoint)
        {
            float3 offset = volume.AbsoluteUniversePosition - absolutePoint;
            float distSqr = math.lengthsq(offset);
            if (distSqr >= volume.Radius * volume.Radius)
                return 0f;

            float attenuation = 1f - (distSqr * volume.InvRadiusSqr);
            return volume.Intensity * (attenuation * attenuation);
        }

        private HazardVolumeData BuildVolumeData(Vector3 runtimePosition, float intensity, float radius, HazardType type)
        {
            float safeRadius = radius > MinHazardRadius ? radius : MinHazardRadius;
            HazardVolumeData data = default;
            data.AbsoluteUniversePosition = HectonFloatingOrigin.ToAbsoluteUniversePosition(runtimePosition);
            data.Radius = safeRadius;
            data.InvRadiusSqr = 1f / (safeRadius * safeRadius);
            data.Intensity = Mathf.Max(0f, intensity);
            data.Type = type;
            return data;
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
            if (_jobRunning && _jobHandle.IsCompleted)
            {
                _jobHandle.Complete();
                _jobRunning = false;
                _jobHandle = default;
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
            }
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

        private void TryRegister()
        {
            if (_registered)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registered = false;
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
