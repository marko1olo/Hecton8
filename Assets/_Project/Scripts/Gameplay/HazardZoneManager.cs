using System.Diagnostics;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct HazardVolumeData
    {
        [FieldOffset(0)] public double3 AbsoluteUniversePosition;
        [FieldOffset(24)] public float Radius;
        [FieldOffset(28)] public float InvRadius;
        [FieldOffset(32)] public float InvRadiusSqr;
        [FieldOffset(36)] public float Intensity;
        [FieldOffset(40)] public float VisorGlitchBias;
        [FieldOffset(44)] public int CurveLutOffset;
        [FieldOffset(48)] public HazardType Type;
        [FieldOffset(52)] public byte RequiresToxicMudBroadphase;
        [FieldOffset(53)] public byte PlayerToxicMudBroadphase;
        [FieldOffset(54)] public byte VehicleToxicMudBroadphase;
        [FieldOffset(55)] private byte _pad0;
        [FieldOffset(56)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct HazardExposureJobResult
    {
        [FieldOffset(0)] public float PlayerRadiation;
        [FieldOffset(4)] public float PlayerHeat;
        [FieldOffset(8)] public float PlayerToxicity;
        [FieldOffset(12)] public float PlayerBiohazard;
        [FieldOffset(16)] public float PlayerRadiationGlitchBias;
        [FieldOffset(20)] public float PlayerHeatGlitchBias;
        [FieldOffset(24)] public float PlayerToxicityGlitchBias;
        [FieldOffset(28)] public float PlayerBiohazardGlitchBias;
        [FieldOffset(32)] public float VehicleRadiation;
        [FieldOffset(36)] public float VehicleHeat;
        [FieldOffset(40)] public float VehicleToxicity;
        [FieldOffset(44)] public float VehicleBiohazard;
        [FieldOffset(48)] public float VehicleRadiationGlitchBias;
        [FieldOffset(52)] public float VehicleHeatGlitchBias;
        [FieldOffset(56)] public float VehicleToxicityGlitchBias;
        [FieldOffset(60)] public float VehicleBiohazardGlitchBias;
        [FieldOffset(64)] public byte PlayerExposureMask;
        [FieldOffset(65)] public byte VehicleExposureMask;
        [FieldOffset(66)] private ushort _pad0;
        [FieldOffset(68)] private uint _pad1;
        [FieldOffset(72)] private ulong _pad2;
        [FieldOffset(80)] private ulong _pad3;
        [FieldOffset(88)] private ulong _pad4;
        [FieldOffset(96)] private ulong _pad5;
        [FieldOffset(104)] private ulong _pad6;
        [FieldOffset(112)] private ulong _pad7;
        [FieldOffset(120)] private ulong _pad8;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct EvaluateHazardExposureJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<HazardVolumeData> Volumes;
        [ReadOnly, NoAlias] public NativeArray<float> CurveLutSamples;
        public int CurveLutSampleCount;
        public int VolumeCount;
        public byte HasPlayerBounds;
        public byte HasVehicleBounds;
        public double3 PlayerCenter;
        public float3 PlayerHalfExtents;
        public double3 VehicleCenter;
        public float3 VehicleHalfExtents;
        [NoAlias] public NativeSlice<HazardExposureJobResult> Result;

        public void Execute()
        {
            HazardExposureJobResult result = default;
            for (int i = 0; i < VolumeCount; i++)
            {
                HazardVolumeData volume = Volumes[i];

                bool requiresToxicMudBroadphase = volume.Type == HazardType.Toxicity && volume.RequiresToxicMudBroadphase != 0;
                if (HasPlayerBounds != 0 && (!requiresToxicMudBroadphase || volume.PlayerToxicMudBroadphase != 0))
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

                if (HasVehicleBounds != 0 && (!requiresToxicMudBroadphase || volume.VehicleToxicMudBroadphase != 0))
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
            double3 aabbCenter,
            float3 aabbHalfExtents,
            in HazardVolumeData volume,
            NativeArray<float> curveLutSamples,
            int curveLutSampleCount)
        {
            double3 halfExtents = new double3(aabbHalfExtents.x, aabbHalfExtents.y, aabbHalfExtents.z);
            double3 min = aabbCenter - halfExtents;
            double3 max = aabbCenter + halfExtents;
            double3 closestPoint = math.clamp(volume.AbsoluteUniversePosition, min, max);
            double3 offset = closestPoint - volume.AbsoluteUniversePosition;
            double distSqr = math.lengthsq(offset);
            double radiusSq = (double)volume.Radius * volume.Radius;
            if (distSqr >= radiusSq)
                return 0f;

            if (volume.Type == HazardType.Toxicity && volume.RequiresToxicMudBroadphase != 0)
            {
                float normalizedDistanceSq = (float)math.clamp(distSqr * volume.InvRadiusSqr, 0d, 1d);
                return volume.Intensity * ResolveSquaredDefaultCurveSample(normalizedDistanceSq);
            }

            float normalizedDistanceSqForCurve = (float)math.clamp(distSqr * volume.InvRadiusSqr, 0d, 1d);
            float attenuation = SampleIntensityCurveByDistanceSq(
                curveLutSamples,
                curveLutSampleCount,
                volume.CurveLutOffset,
                normalizedDistanceSqForCurve);
            return volume.Intensity * attenuation;
        }

        private static float SampleIntensityCurveByDistanceSq(
            NativeArray<float> curveLutSamples,
            int curveLutSampleCount,
            int curveLutOffset,
            float normalizedDistanceSq)
        {
            if (!curveLutSamples.IsCreated || curveLutSampleCount <= 1)
                return ResolveSquaredDefaultCurveSample(normalizedDistanceSq);

            float safeDistanceSq = FiniteSaturate01(normalizedDistanceSq);
            float scaledIndex = safeDistanceSq * (curveLutSampleCount - 1);
            int sampleIndex = (int)math.floor(scaledIndex);
            int nextIndex = math.min(curveLutSampleCount - 1, sampleIndex + 1);
            float fraction = scaledIndex - sampleIndex;
            float a = FiniteSaturate01(curveLutSamples[curveLutOffset + sampleIndex]);
            float b = FiniteSaturate01(curveLutSamples[curveLutOffset + nextIndex]);
            return math.lerp(a, b, fraction);
        }

        private static float ResolveSquaredDefaultCurveSample(float normalizedDistanceSq)
        {
            float attenuation = 1f - FiniteSaturate01(normalizedDistanceSq);
            return attenuation > 0f ? attenuation * attenuation : 0f;
        }

        private static float FiniteSaturate01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-5695)]
    public sealed class HazardZoneManager : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener, IHazardZoneReadModel
    {
        private const int HazardTypeCount = 4;
        private const int DefaultMaxZoneCount = 512;
        private const int MinZoneCapacity = 32;
        private const int PendingMutationCapacity = 64;
        private const int MaxStepIterationsPerTick = 4;
        private const float HazardStepIntervalSeconds = 0.1f;
        private const float MaxHazardAccumulatedSeconds = HazardStepIntervalSeconds * MaxStepIterationsPerTick;
        private const float MinHazardRadius = 0.01f;
        private const double HazardSpatialCellSizeMeters = 12d;
        private const int HazardSpatialQueryCapacity = 64;
        private const int HazardSpatialLayerMask = 1 << 30;
        private const int HazardTypeMaskAll = (1 << HazardTypeCount) - 1;
        private const int HazardTypeMaskRadiation = 1 << (int)HazardType.Radiation;
        private const int HazardTypeMaskNonRadiation = HazardTypeMaskAll & ~HazardTypeMaskRadiation;
        private const uint PendingMutationOverflowWarningHash = 0x485A4D51u; // HZMQ
        private const uint HazardManagerContextHash = 0x485A4D47u; // HZMG
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
        private const float ConservativeAabbSphereFactor = 1.7320508f;
        private const Allocator DataVaultExemptSceneScratchAllocator = Allocator.Persistent;
        private static readonly Vector3 DefaultPlayerBoundsSize = new Vector3(0.9f, 1.9f, 0.9f);
        private static readonly Vector3 DefaultTransportBoundsSize = new Vector3(2.2f, 1.6f, 3.8f);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const string OverflowLogText = "[HazardZoneManager] Hazard registry capacity exceeded.";
#endif

        [Header("Capacity")]
        [Tooltip("Maximum simultaneous hazard volumes stored in the runtime registry.")]
        [SerializeField, Min(MinZoneCapacity)] private int maxZoneCount = DefaultMaxZoneCount;

        [Header("Diagnostics")]
        [SerializeField] private int _debugActiveZoneCount;
        [SerializeField] private float _debugToxicityDose;
        [SerializeField] private float _debugPlayerToxicityIntensity;
        [SerializeField] private float _debugVehicleToxicityIntensity;
        [SerializeField] private bool _debugJobRunning;
        [SerializeField] private bool _debugPlayerExposureActive;
        [SerializeField] private bool _debugVehicleExposureActive;
        [SerializeField] private int _debugPendingMutationCount;

        private NativeArray<HazardVolumeData> _volumes;
        private NativeArray<int> _volumeIds;
        private NativeArray<int> _volumeSpatialHandles;
        private NativeArray<float> _volumeCurveLutSamples;
        private NativeArray<HazardVolumeData> _jobVolumes;
        private VaultGenerationHandle<HazardExposureJobResult> _jobResultHandle;
        private IDataVault _dataVault;
        private NativeArray<byte> _candidateVolumeFlags;
        private JobHandle _jobHandle;
        private HectonSpatialHash _spatialHash;
        private NativeList<int> _spatialQueryHandles;
        private bool _jobRunning;
        private bool _registered;
        private bool _serviceRegistered;
        private bool _hotSwapRegistered;
        private bool _ownsJobResultHandle;
        private bool _pendingDataVaultSwap;
        private int _activeCount;
        private float _stepAccumulator;
        private float _toxicityDose;
        private float _toxicityDamageTimer;
        private int _publishedExposureMask;

        private Transform _playerTransform;
        private IDataVault _pendingDataVault;
        private HectonSurvivalSystem _playerSurvival;
        private TraumaDispatcher _playerTraumaDispatcher;
        private PlayerTransportCoordinator _playerTransportCoordinator;
        private Collider _playerCollider;
        private IPlayerTransportLifecycleOwner _activeTransportOwner;
        private MonoBehaviour _activeTransportBehaviour;
        private Collider _activeTransportCollider;

        // COLD ALLOC: float[4] - cached player hazard intensities by HazardType - owner: HazardZoneManager
        private readonly float[] _playerHazardIntensity = new float[HazardTypeCount];
        // COLD ALLOC: float[4] - cached vehicle hazard intensities by HazardType - owner: HazardZoneManager
        private readonly float[] _vehicleHazardIntensity = new float[HazardTypeCount];
        // COLD ALLOC: float[4] - cached player hazard glitch bias by HazardType - owner: HazardZoneManager
        private readonly float[] _playerHazardGlitchBias = new float[HazardTypeCount];
        // COLD ALLOC: float[4] - cached vehicle hazard glitch bias by HazardType - owner: HazardZoneManager
        private readonly float[] _vehicleHazardGlitchBias = new float[HazardTypeCount];
        // COLD ALLOC: PendingHazardZoneMutation[64] - deferred register/unregister mutations while exposure job reads LUTs - owner: HazardZoneManager
        private readonly PendingHazardZoneMutation[] _pendingMutations = new PendingHazardZoneMutation[PendingMutationCapacity];
        private int _pendingMutationCount;

        /// <summary>
        /// Ensures the runtime hazard host exists and returns the active manager.
        /// </summary>
        public static HazardZoneManager EnsureRuntimeInstance()
        {
            HazardZoneManager registeredInstance = GlobalRegistry.HazardZones;
            if (registeredInstance != null)
                return registeredInstance;

            EnvironmentRuntimeContextService environmentService = EnvironmentRuntimeContextService.EnsureRuntimeInstance();
            environmentService.InitializeService();
            return environmentService.HazardZones;
        }

        /// <summary>
        /// Registers or updates a spherical hazard volume in runtime absolute-universe space.
        /// </summary>
        public bool RegisterZone(int id, Vector3 runtimePosition, float intensity, float radius, HazardType type, float visorGlitchBias = 1f)
        {
            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition positionAup))
                return false;

            return RegisterZone(id, in positionAup, intensity, radius, type, visorGlitchBias, null);
        }

        /// <summary>
        /// Registers or updates a spherical hazard volume in absolute-universe space.
        /// </summary>
        public bool RegisterZone(int id, in AbsoluteUniversePosition positionAup, float intensity, float radius, HazardType type, float visorGlitchBias = 1f)
        {
            return RegisterZone(id, in positionAup, intensity, radius, type, visorGlitchBias, null);
        }

        internal bool RegisterZone(int id, Vector3 runtimePosition, float intensity, float radius, HazardType type, float visorGlitchBias, HazardZoneProfile profile)
        {
            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition positionAup))
                return false;

            return RegisterZone(id, in positionAup, intensity, radius, type, visorGlitchBias, profile);
        }

        internal bool RegisterZone(int id, in AbsoluteUniversePosition positionAup, float intensity, float radius, HazardType type, float visorGlitchBias, HazardZoneProfile profile)
        {
            if (!IsValidHazardZoneInput(id, in positionAup, intensity, radius, type, visorGlitchBias))
                return false;

            if (type == HazardType.Radiation)
            {
                RadiationHazardGrid.RegisterSource(id, in positionAup, intensity, radius);
                return true;
            }

            if (!_volumes.IsCreated)
                return false;

            if (!TryPrepareVolumeMutation())
                return QueueRegisterMutation(id, in positionAup, intensity, radius, type, visorGlitchBias, profile);

            return RegisterZoneImmediate(id, in positionAup, intensity, radius, type, visorGlitchBias, profile);
        }

        private bool RegisterZoneImmediate(int id, in AbsoluteUniversePosition positionAup, float intensity, float radius, HazardType type, float visorGlitchBias, HazardZoneProfile profile)
        {
            int existingIndex = FindZoneIndex(id);
            if (existingIndex >= 0)
            {
                HazardVolumeData data = BuildVolumeData(existingIndex, id, in positionAup, intensity, radius, type, visorGlitchBias);
                WriteVolumeCurveLut(existingIndex, profile);
                _volumes[existingIndex] = data;
                UpdateSpatialEntry(existingIndex, id, in data);
                return true;
            }

            if (_activeCount >= _volumes.Length)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LogRegistryOverflow();
#endif
                return false;
            }

            HazardVolumeData newData = BuildVolumeData(_activeCount, id, in positionAup, intensity, radius, type, visorGlitchBias);
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

            if (!TryPrepareVolumeMutation())
            {
                QueueUnregisterMutation(id);
                return;
            }

            UnregisterZoneImmediate(id);
        }

        public void UnregisterZone(int id, HazardType type)
        {
            if (type == HazardType.Radiation)
            {
                RadiationHazardGrid.UnregisterSource(id);
                return;
            }

            UnregisterZone(id);
        }

        private void UnregisterZoneImmediate(int id)
        {
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
            if (!TryResolveAupFromRuntimeOrigin(runtimePoint, out AbsoluteUniversePosition pointAup))
                return 0f;

            return GetHazardIntensity(in pointAup, type);
        }

        /// <summary>
        /// Returns the summed hazard intensity at the supplied absolute-universe point.
        /// </summary>
        public float GetHazardIntensity(in AbsoluteUniversePosition pointAup, HazardType type)
        {
            if (!IsFiniteAup(in pointAup))
                return 0f;

            if (type == HazardType.Radiation)
                return RadiationHazardGrid.TrySampleRadiationIntensity01(in pointAup, out float radiation01) ? radiation01 : 0f;

            if (!_volumes.IsCreated || _activeCount <= 0)
                return 0f;

            double3 absolutePoint = pointAup.ToAbsoluteDouble3();
            if (_spatialHash == null || !_spatialQueryHandles.IsCreated)
                return SumHazardIntensityLinear(absolutePoint, in pointAup, type);

            int candidateCount = _spatialHash.CollectSphere(
                pointAup,
                MinHazardRadius,
                HazardSpatialLayerMask,
                _spatialQueryHandles);
            if (IsSpatialQuerySaturated(candidateCount))
                return SumHazardIntensityLinear(absolutePoint, in pointAup, type);

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

                totalIntensity += EvaluatePointContribution(
                    volume,
                    absolutePoint,
                    IsPointEligibleForToxicMudVolume(index, in volume, in pointAup));
            }

            return totalIntensity;
        }

        public float GetToxicityIntensity(in AbsoluteUniversePosition pointAup)
        {
            return GetHazardIntensity(in pointAup, HazardType.Toxicity);
        }

        internal bool TrySampleHazardAvoidance(Vector3 runtimePoint, float sampleRadius, out Vector3 fleeDirection, out float hazardPressure01)
        {
            fleeDirection = Vector3.zero;
            hazardPressure01 = 0f;
            if (!TryResolveAupFromRuntimeOrigin(runtimePoint, out AbsoluteUniversePosition pointAup))
                return false;

            return TrySampleHazardAvoidance(in pointAup, sampleRadius, out fleeDirection, out hazardPressure01);
        }

        internal bool TrySampleHazardAvoidance(in AbsoluteUniversePosition pointAup, float sampleRadius, out Vector3 fleeDirection, out float hazardPressure01)
        {
            fleeDirection = Vector3.zero;
            hazardPressure01 = 0f;
            if (_spatialHash == null ||
                !_spatialQueryHandles.IsCreated ||
                _activeCount <= 0 ||
                !IsFiniteAup(in pointAup) ||
                !math.isfinite(sampleRadius) ||
                sampleRadius <= 0.001f)
                return false;

            double3 absolutePoint = pointAup.ToAbsoluteDouble3();
            int candidateCount = _spatialHash.CollectSphere(
                pointAup,
                sampleRadius,
                HazardSpatialLayerMask,
                _spatialQueryHandles);
            bool querySaturated = IsSpatialQuerySaturated(candidateCount);
            if (candidateCount <= 0 && !querySaturated)
                return false;

            float3 accumulatedAway = float3.zero;
            float peakPressure = 0f;
            if (querySaturated)
            {
                for (int i = 0; i < _activeCount; i++)
                {
                    AccumulateAvoidanceContribution(
                        i,
                        _volumes[i],
                        in pointAup,
                        absolutePoint,
                        ref accumulatedAway,
                        ref peakPressure);
                }
            }
            else
            {
                for (int i = 0; i < candidateCount; i++)
                {
                    if (!_spatialHash.TryGetEntry(_spatialQueryHandles[i], out HectonSpatialHash.SpatialEntry entry))
                        continue;

                    int index = FindZoneIndex(entry.PayloadId);
                    if (index < 0)
                        continue;

                    AccumulateAvoidanceContribution(
                        index,
                        _volumes[index],
                        in pointAup,
                        absolutePoint,
                        ref accumulatedAway,
                        ref peakPressure);
                }
            }

            if (peakPressure <= 0.001f ||
                !math.all(math.isfinite(accumulatedAway)) ||
                math.lengthsq(accumulatedAway) <= 0.0001f)
                return false;

            fleeDirection = ResolveCheapAvoidanceDirection(accumulatedAway);
            hazardPressure01 = math.saturate(peakPressure);
            return true;
        }

        /// <summary>Current toxicity dose accumulated by the local player.</summary>
        public float ToxicityDose => _toxicityDose;

        private void Awake()
        {
            HazardZoneManager registeredInstance = GlobalRegistry.HazardZones;
            if (registeredInstance != null && registeredInstance != this)
            {
                Destroy(gameObject);
                return;
            }

            CacheHazardVaultCold(GlobalRegistry.DataVault);
            AllocateNativeState();
            ResolvePlayerContext();
            UpdateDiagnostics();
        }

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            CacheHazardVaultCold(GlobalRegistry.DataVault);
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
            TryUnregisterHotSwapListener();
            ClearRuntimeState();
            DisposeNativeState();
        }

        private void OnDestroy()
        {
            PublishExposureMask(0);
            TryUnregister();
            TryUnregisterService();
            TryUnregisterHotSwapListener();
            ClearRuntimeState();
            DisposeNativeState();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            if (ReferenceEquals(_dataVault, currentService))
                return;

            IDataVault nextVault = currentService as IDataVault;
            if (_jobRunning)
            {
                _pendingDataVault = nextVault;
                _pendingDataVaultSwap = true;
                return;
            }

            ApplyDataVaultSwap(nextVault);
        }

        private void ApplyDataVaultSwap(IDataVault nextVault)
        {
            ReleaseHazardExposureResultBuffer();
            CacheHazardVaultCold(nextVault);
            if (_volumes.IsCreated && !_jobRunning)
                _ = TryPrepareHazardExposureResultBuffer(out _, allowAllocation: true);
        }

        private void TryApplyPendingDataVaultSwap()
        {
            if (!_pendingDataVaultSwap || _jobRunning)
                return;

            IDataVault nextVault = _pendingDataVault;
            _pendingDataVault = null;
            _pendingDataVaultSwap = false;
            ApplyDataVaultSwap(nextVault);
        }

        /// <summary>
        /// Runs the 10Hz hazard step without using MonoBehaviour Update.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_volumes.IsCreated)
                return;

            float safeDeltaTime = FiniteNonNegativeOrZero(deltaTime);
            if (safeDeltaTime <= 0f)
                return;

            _stepAccumulator = math.min(
                FiniteNonNegativeOrZero(_stepAccumulator) + safeDeltaTime,
                MaxHazardAccumulatedSeconds);
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
            RefreshPlayerContextSnapshot();
            ApplyToxicityDose(dt);
            ApplyPendingMutationsIfIdle();
            ScheduleExposureJob();
            UpdateDiagnostics();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            ConsumeCompletedJob();
            ApplyPendingMutationsIfIdle();
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
            _candidateVolumeFlags = new NativeArray<byte>(safeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _ = TryPrepareHazardExposureResultBuffer(out _, allowAllocation: true);
            _spatialHash = new HectonSpatialHash(
                safeCapacity,
                safeCapacity * 6,
                HazardSpatialCellSizeMeters,
                NativeAllocationLifetime.Session);
            _spatialQueryHandles = new NativeList<int>(HazardSpatialQueryCapacity, DataVaultExemptSceneScratchAllocator);
            NativeMemorySentinel.RegisterNativeArray(_volumes, nameof(HazardZoneManager), nameof(_volumes), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_volumeIds, nameof(HazardZoneManager), nameof(_volumeIds), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_volumeSpatialHandles, nameof(HazardZoneManager), nameof(_volumeSpatialHandles), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_volumeCurveLutSamples, nameof(HazardZoneManager), nameof(_volumeCurveLutSamples), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_jobVolumes, nameof(HazardZoneManager), nameof(_jobVolumes), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_candidateVolumeFlags, nameof(HazardZoneManager), nameof(_candidateVolumeFlags), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeList(_spatialQueryHandles, nameof(HazardZoneManager), nameof(_spatialQueryHandles), NativeAllocationLifetime.Session);
        }

        private void DisposeNativeState()
        {
            if (_jobRunning)
            {
                DispatcherJobFence.TryComplete(ref _jobHandle, forceComplete: true);
                _jobRunning = false;
            }

            _jobHandle = default;
            _pendingDataVault = null;
            _pendingDataVaultSwap = false;
            ReleaseHazardExposureResultBuffer();
            JobHandle disposeHandle = default;

            if (_volumes.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_volumes);
                _volumes.Dispose();
                _volumes = default;
            }

            if (_volumeIds.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_volumeIds);
                _volumeIds.Dispose();
                _volumeIds = default;
            }

            if (_volumeSpatialHandles.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_volumeSpatialHandles);
                _volumeSpatialHandles.Dispose();
                _volumeSpatialHandles = default;
            }

            if (_volumeCurveLutSamples.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_volumeCurveLutSamples);
                _volumeCurveLutSamples.Dispose(disposeHandle);
                _volumeCurveLutSamples = default;
            }

            if (_jobVolumes.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_jobVolumes);
                _jobVolumes.Dispose(disposeHandle);
                _jobVolumes = default;
            }

            if (_candidateVolumeFlags.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_candidateVolumeFlags);
                _candidateVolumeFlags.Dispose(disposeHandle);
                _candidateVolumeFlags = default;
            }

            if (_spatialQueryHandles.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeList(nameof(HazardZoneManager), nameof(_spatialQueryHandles));
                _spatialQueryHandles.Dispose();
                _spatialQueryHandles = default;
            }

            _spatialHash?.Dispose();
            _spatialHash = null;
        }

        private void ResolvePlayerContext()
        {
            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext))
            {
                ApplyPlayerContextReferences(
                    runtimeContext.PlayerTransform,
                    runtimeContext.PlayerCollider,
                    runtimeContext.SurvivalSystem,
                    runtimeContext.TraumaDispatcher,
                    runtimeContext.PlayerTransportCoordinator);
            }
            else
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                if (playerContext != null)
                {
                    ApplyPlayerContextReferences(
                        playerContext.PlayerTransform,
                        playerContext.PlayerCollider,
                        playerContext.SurvivalSystem,
                        playerContext.TraumaDispatcher,
                        playerContext.PlayerTransportCoordinator);
                }
            }

            if (_playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref _playerTransform);

            if (_playerTransform == null)
                return;

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

        private void RefreshPlayerContextSnapshot()
        {
            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext))
            {
                ApplyPlayerContextReferences(
                    runtimeContext.PlayerTransform,
                    runtimeContext.PlayerCollider,
                    runtimeContext.SurvivalSystem,
                    runtimeContext.TraumaDispatcher,
                    runtimeContext.PlayerTransportCoordinator);
            }

            if (_playerTransform == null)
                return;

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

        private void ApplyPlayerContextReferences(
            Transform playerTransform,
            Collider playerCollider,
            HectonSurvivalSystem survivalSystem,
            TraumaDispatcher traumaDispatcher,
            PlayerTransportCoordinator transportCoordinator)
        {
            if (playerTransform != null && !ReferenceEquals(_playerTransform, playerTransform))
            {
                _playerTransform = playerTransform;
                _playerCollider = null;
                _playerSurvival = null;
                _playerTraumaDispatcher = null;
                _playerTransportCoordinator = null;
            }

            if (playerCollider != null)
                _playerCollider = playerCollider;

            if (survivalSystem != null)
                _playerSurvival = survivalSystem;

            if (traumaDispatcher != null)
                _playerTraumaDispatcher = traumaDispatcher;

            if (transportCoordinator != null)
                _playerTransportCoordinator = transportCoordinator;
        }

        private void ConsumeCompletedJob()
        {
            if (TryConsumeCompletedJobResult())
                TryApplyPendingDataVaultSwap();
        }

        private bool TryConsumeCompletedJobResult()
        {
            if (!_jobRunning)
                return true;

            if (!DispatcherJobSwap.TryFinalizeCompleted(ref _jobHandle))
                return false;

            _jobRunning = false;

            if (!TryPrepareHazardExposureResultBuffer(out NativeArray<HazardExposureJobResult> jobResult, allowAllocation: false))
            {
                _ = TryPrepareHazardExposureResultBuffer(out _, allowAllocation: true);
                ClearExposureState();
                return true;
            }

            HazardExposureJobResult result = jobResult[0];
            _playerHazardIntensity[(int)HazardType.Radiation] = 0f;
            _playerHazardIntensity[(int)HazardType.Heat] = ClampExposure(result.PlayerHeat);
            _playerHazardIntensity[(int)HazardType.Toxicity] = ClampExposure(result.PlayerToxicity);
            _playerHazardIntensity[(int)HazardType.Biohazard] = ClampExposure(result.PlayerBiohazard);
            _playerHazardGlitchBias[(int)HazardType.Radiation] = 0f;
            _playerHazardGlitchBias[(int)HazardType.Heat] = ClampGlitchBias(result.PlayerHeatGlitchBias);
            _playerHazardGlitchBias[(int)HazardType.Toxicity] = ClampGlitchBias(result.PlayerToxicityGlitchBias);
            _playerHazardGlitchBias[(int)HazardType.Biohazard] = ClampGlitchBias(result.PlayerBiohazardGlitchBias);
            _vehicleHazardIntensity[(int)HazardType.Radiation] = 0f;
            _vehicleHazardIntensity[(int)HazardType.Heat] = ClampExposure(result.VehicleHeat);
            _vehicleHazardIntensity[(int)HazardType.Toxicity] = ClampExposure(result.VehicleToxicity);
            _vehicleHazardIntensity[(int)HazardType.Biohazard] = ClampExposure(result.VehicleBiohazard);
            _vehicleHazardGlitchBias[(int)HazardType.Radiation] = 0f;
            _vehicleHazardGlitchBias[(int)HazardType.Heat] = ClampGlitchBias(result.VehicleHeatGlitchBias);
            _vehicleHazardGlitchBias[(int)HazardType.Toxicity] = ClampGlitchBias(result.VehicleToxicityGlitchBias);
            _vehicleHazardGlitchBias[(int)HazardType.Biohazard] = ClampGlitchBias(result.VehicleBiohazardGlitchBias);

            PublishExposureMask((result.PlayerExposureMask | result.VehicleExposureMask) & HazardTypeMaskNonRadiation);
            DispatchClarityTraumaSignals();
            return true;
        }

        private void ApplyToxicityDose(float dt)
        {
            float safeDt = FiniteNonNegativeOrZero(dt);
            if (safeDt <= 0f)
                return;

            float currentToxicityIntensity = ClampExposure(math.max(
                _playerHazardIntensity[(int)HazardType.Toxicity],
                _vehicleHazardIntensity[(int)HazardType.Toxicity]));

            if (currentToxicityIntensity > 0.001f)
            {
                float resistance = ResolveToxicityResistance();
                _toxicityDose += (currentToxicityIntensity / resistance) * safeDt;
            }
            else
            {
                _toxicityDose = math.max(0f, FiniteNonNegativeOrZero(_toxicityDose) - ToxicityDoseDecayPerSecond * safeDt);
                if (_toxicityDose <= ToxicityDoseThreshold)
                    _toxicityDamageTimer = 0f;
            }

            if (_toxicityDose <= ToxicityDoseThreshold || _playerSurvival == null)
                return;

            _toxicityDamageTimer += safeDt;
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
            float integrityDeltaNormalized = math.abs(nextIntegrityNormalized - previousIntegrityNormalized);
            if (_playerTraumaDispatcher == null || integrityDeltaNormalized <= 0.0001f)
                return;

            HabitatDamageSignal signal = default;
            signal.magnitude = damageMagnitude;
            signal.localPoint = float3.zero;
            signal.damageType = (uint)DamageTypeMask.Parasite;
            signal.integrityDelta = (byte)math.clamp(
                (int)math.round(integrityDeltaNormalized * byte.MaxValue),
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

            return math.clamp(
                FiniteAtLeast(_playerSurvival.ResolveEnvironmentalResistance(HazardType.Toxicity), 1f, MinResistance),
                MinResistance,
                MaxProtectedResistance);
        }

        private void ScheduleExposureJob()
        {
            if (_jobRunning || !_jobVolumes.IsCreated)
                return;

            bool hasPlayerAupSnapshot = TryResolvePlayerPredictedAup(out AbsoluteUniversePosition playerAupSnapshot);
            bool hasPlayerBounds = TryBuildQueryBounds(
                _playerCollider,
                DefaultPlayerBoundsSize,
                hasPlayerAupSnapshot,
                in playerAupSnapshot,
                out float3 playerHalfExtents,
                out AbsoluteUniversePosition playerCenterAup);
            bool hasVehicleBounds = TryBuildVehicleQueryBounds(
                out float3 vehicleHalfExtents,
                out AbsoluteUniversePosition vehicleCenterAup);
            if (!hasPlayerBounds && !hasVehicleBounds)
            {
                ClearExposureState();
                return;
            }

            int candidateCount = CollectCandidateVolumes(
                hasPlayerBounds,
                in playerCenterAup,
                playerHalfExtents,
                hasVehicleBounds,
                in vehicleCenterAup,
                vehicleHalfExtents);
            if (candidateCount <= 0)
            {
                ClearExposureState();
                return;
            }

            if (!TryPrepareHazardExposureResultBuffer(out NativeArray<HazardExposureJobResult> jobResult, allowAllocation: false))
            {
                ClearExposureState();
                return;
            }

            jobResult[0] = default;
            EvaluateHazardExposureJob job = new EvaluateHazardExposureJob
            {
                Volumes = _jobVolumes,
                CurveLutSamples = _volumeCurveLutSamples,
                CurveLutSampleCount = HazardZoneProfile.IntensityLutSampleCount,
                VolumeCount = candidateCount,
                HasPlayerBounds = hasPlayerBounds ? (byte)1 : (byte)0,
                HasVehicleBounds = hasVehicleBounds ? (byte)1 : (byte)0,
                PlayerCenter = playerCenterAup.ToAbsoluteDouble3(),
                PlayerHalfExtents = playerHalfExtents,
                VehicleCenter = vehicleCenterAup.ToAbsoluteDouble3(),
                VehicleHalfExtents = vehicleHalfExtents,
                Result = new NativeSlice<HazardExposureJobResult>(jobResult)
            };

            _jobHandle = job.Schedule();
            _jobRunning = true;
        }

        private bool TryPrepareHazardExposureResultBuffer(out NativeArray<HazardExposureJobResult> jobResult, bool allowAllocation)
        {
            jobResult = default;
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
            {
                ClearHazardExposureResultDescriptor();
                return false;
            }

            if (IsVaultHandleCreated(in _jobResultHandle) &&
                vault.TryResolveHandle(in _jobResultHandle, out jobResult) &&
                jobResult.IsCreated &&
                jobResult.Length >= 1)
            {
                return true;
            }

            ClearHazardExposureResultDescriptor();
            if (vault.TryGetGenerationHandle(
                    BufferID.HazardExposureJobResult,
                    out VaultGenerationHandle<HazardExposureJobResult> existing) &&
                vault.TryResolveHandle(in existing, out jobResult) &&
                jobResult.IsCreated &&
                jobResult.Length >= 1)
            {
                _jobResultHandle = existing;
                return true;
            }

            if (!allowAllocation || vault.IsAllocationLocked)
                return false;

            VaultGenerationHandle<HazardExposureJobResult> acquired = vault.GetGenerationHandle<HazardExposureJobResult>(
                BufferID.HazardExposureJobResult,
                1,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);
            if (!IsVaultHandleCreated(in acquired) ||
                !vault.TryResolveHandle(in acquired, out jobResult) ||
                !jobResult.IsCreated ||
                jobResult.Length < 1)
            {
                return false;
            }

            _jobResultHandle = acquired;
            _ownsJobResultHandle = true;
            return true;
        }

        private void CacheHazardVaultCold(IDataVault vault)
        {
            if (ReferenceEquals(_dataVault, vault))
                return;

            ClearHazardExposureResultDescriptor();
            _dataVault = vault;
        }

        private void ReleaseHazardExposureResultBuffer()
        {
            IDataVault vault = _dataVault;
            if (!_ownsJobResultHandle ||
                _jobRunning ||
                vault == null ||
                !IsVaultHandleCreated(in _jobResultHandle) ||
                !vault.TryGetGenerationHandle(
                    BufferID.HazardExposureJobResult,
                    out VaultGenerationHandle<HazardExposureJobResult> current) ||
                current.Generation != _jobResultHandle.Generation ||
                current.SystemID != _jobResultHandle.SystemID)
            {
                ClearHazardExposureResultDescriptor();
                return;
            }

            vault.ReleaseBuffer(in _jobResultHandle);
            ClearHazardExposureResultDescriptor();
        }

        private void ClearHazardExposureResultDescriptor()
        {
            _jobResultHandle = default;
            _ownsJobResultHandle = false;
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private bool TryBuildVehicleQueryBounds(out float3 halfExtents, out AbsoluteUniversePosition centerAup)
        {
            halfExtents = default;
            centerAup = default;

            if (_activeTransportBehaviour == null)
                return false;

            bool hasTransportAup = TryResolveActiveTransportAup(out AbsoluteUniversePosition transportAup);
            return TryBuildQueryBounds(
                _activeTransportCollider,
                DefaultTransportBoundsSize,
                hasTransportAup,
                in transportAup,
                out halfExtents,
                out centerAup);
        }

        private bool TryResolveActiveTransportAup(out AbsoluteUniversePosition transportAup)
        {
            transportAup = default;
            return _activeTransportBehaviour is VehicleMotor vehicleMotor &&
                   vehicleMotor.TryResolveSubmarineAup(out transportAup);
        }

        private static bool TryBuildQueryBounds(
            Collider targetCollider,
            Vector3 fallbackSize,
            bool hasFallbackCenterAup,
            in AbsoluteUniversePosition fallbackCenterAup,
            out float3 halfExtents,
            out AbsoluteUniversePosition centerAup)
        {
            halfExtents = default;
            centerAup = default;
            if (targetCollider == null && !hasFallbackCenterAup)
                return false;

            Bounds bounds;
            if (targetCollider != null)
            {
                bounds = targetCollider.bounds;
                if (!IsFiniteBounds(bounds) || bounds.size.sqrMagnitude <= 0.0001f)
                    return TryBuildFallbackQueryBounds(fallbackSize, hasFallbackCenterAup, in fallbackCenterAup, out halfExtents, out centerAup);
            }
            else
            {
                return TryBuildFallbackQueryBounds(fallbackSize, hasFallbackCenterAup, in fallbackCenterAup, out halfExtents, out centerAup);
            }

            bool useFallbackCenter = hasFallbackCenterAup && IsFiniteAup(in fallbackCenterAup);
            if (useFallbackCenter)
            {
                centerAup = fallbackCenterAup;
            }
            else if (!TryResolveAupFromRuntimeOrigin(bounds.center, out centerAup))
            {
                return TryBuildFallbackQueryBounds(fallbackSize, hasFallbackCenterAup, in fallbackCenterAup, out halfExtents, out centerAup);
            }

            Vector3 extents = bounds.extents;
            halfExtents = new float3(extents.x, extents.y, extents.z);
            return math.all(math.isfinite(halfExtents)) && math.all(halfExtents > 0f);
        }

        private static bool TryBuildFallbackQueryBounds(
            Vector3 fallbackSize,
            bool hasFallbackCenterAup,
            in AbsoluteUniversePosition fallbackCenterAup,
            out float3 halfExtents,
            out AbsoluteUniversePosition centerAup)
        {
            if (!IsPositiveRuntimeSize(fallbackSize) ||
                !hasFallbackCenterAup ||
                !IsFiniteAup(in fallbackCenterAup))
            {
                halfExtents = default;
                centerAup = default;
                return false;
            }

            centerAup = fallbackCenterAup;
            halfExtents = new float3(
                fallbackSize.x * 0.5f,
                fallbackSize.y * 0.5f,
                fallbackSize.z * 0.5f);
            return true;
        }

        private static bool TryResolvePlayerPredictedAup(out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;
            if (!PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext))
                return false;

            PlayerMovementRuntimeState movementState = runtimeContext.MovementState;
            if ((movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u)
                return false;

            playerAup = movementState.PredictedAup;
            return IsFiniteAup(in playerAup);
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
            in AbsoluteUniversePosition playerCenterAup,
            float3 playerHalfExtents,
            bool hasVehicleBounds,
            in AbsoluteUniversePosition vehicleCenterAup,
            float3 vehicleHalfExtents)
        {
            if (_spatialHash == null || !_candidateVolumeFlags.IsCreated || !_spatialQueryHandles.IsCreated)
            {
                return CopyAllActiveVolumes(
                    hasPlayerBounds,
                    in playerCenterAup,
                    playerHalfExtents,
                    hasVehicleBounds,
                    in vehicleCenterAup,
                    vehicleHalfExtents);
            }

            for (int i = 0; i < _activeCount; i++)
                _candidateVolumeFlags[i] = 0;

            int candidateCount = 0;
            bool querySaturated = false;
            if (hasPlayerBounds)
            {
                candidateCount = AppendCandidateVolumes(
                    in playerCenterAup,
                    ResolveConservativeBroadphaseRadius(playerHalfExtents),
                    candidateCount,
                    hasPlayerBounds,
                    in playerCenterAup,
                    playerHalfExtents,
                    hasVehicleBounds,
                    in vehicleCenterAup,
                    vehicleHalfExtents,
                    out bool playerQuerySaturated);
                querySaturated |= playerQuerySaturated;
            }

            if (hasVehicleBounds)
            {
                candidateCount = AppendCandidateVolumes(
                    in vehicleCenterAup,
                    ResolveConservativeBroadphaseRadius(vehicleHalfExtents),
                    candidateCount,
                    hasPlayerBounds,
                    in playerCenterAup,
                    playerHalfExtents,
                    hasVehicleBounds,
                    in vehicleCenterAup,
                    vehicleHalfExtents,
                    out bool vehicleQuerySaturated);
                querySaturated |= vehicleQuerySaturated;
            }

            if (querySaturated)
            {
                return CopyAllActiveVolumes(
                    hasPlayerBounds,
                    in playerCenterAup,
                    playerHalfExtents,
                    hasVehicleBounds,
                    in vehicleCenterAup,
                    vehicleHalfExtents);
            }

            return candidateCount;
        }

        private void AccumulateAvoidanceContribution(
            int zoneIndex,
            HazardVolumeData volume,
            in AbsoluteUniversePosition pointAup,
            double3 absolutePoint,
            ref float3 accumulatedAway,
            ref float peakPressure)
        {
            float contribution = EvaluatePointContribution(
                volume,
                absolutePoint,
                IsPointEligibleForToxicMudVolume(zoneIndex, in volume, in pointAup));
            if (contribution <= 0.001f)
                return;

            float pressure = NormalizeHazardClarityContribution(volume.Type, contribution);
            if (pressure <= 0.001f)
                return;

            double3 away = absolutePoint - volume.AbsoluteUniversePosition;
            double awaySqr = math.lengthsq(away);
            if (awaySqr <= 0.0001d)
            {
                accumulatedAway.y += pressure;
                if (pressure > peakPressure)
                    peakPressure = pressure;

                return;
            }

            float distanceWeight = (float)math.clamp(1d - (awaySqr * volume.InvRadiusSqr), 0d, 1d);
            float weightedPressure = pressure * math.max(0.125f, distanceWeight);
            accumulatedAway += new float3(
                (float)away.x,
                (float)away.y,
                (float)away.z) * weightedPressure;
            if (pressure > peakPressure)
                peakPressure = pressure;
        }

        private static float ResolveConservativeBroadphaseRadius(float3 halfExtents)
        {
            if (!math.all(math.isfinite(halfExtents)))
                return MinHazardRadius;

            float maxExtent = math.cmax(math.abs(halfExtents));
            return math.max(MinHazardRadius, maxExtent * ConservativeAabbSphereFactor);
        }

        private static float ResolveConservativeVerticalHalfExtent(float3 halfExtents)
        {
            if (!math.isfinite(halfExtents.y))
                return MinHazardRadius;

            return math.max(MinHazardRadius, math.abs(halfExtents.y));
        }

        private int AppendCandidateVolumes(
            in AbsoluteUniversePosition absoluteCenter,
            float queryRadius,
            int candidateCount,
            bool hasPlayerBounds,
            in AbsoluteUniversePosition playerCenterAup,
            float3 playerHalfExtents,
            bool hasVehicleBounds,
            in AbsoluteUniversePosition vehicleCenterAup,
            float3 vehicleHalfExtents,
            out bool querySaturated)
        {
            int handleCount = _spatialHash.CollectSphere(
                absoluteCenter,
                queryRadius,
                HazardSpatialLayerMask,
                _spatialQueryHandles);
            querySaturated = IsSpatialQuerySaturated(handleCount);

            for (int i = 0; i < handleCount; i++)
            {
                if (!_spatialHash.TryGetEntry(_spatialQueryHandles[i], out HectonSpatialHash.SpatialEntry entry))
                    continue;

                int zoneIndex = FindZoneIndex(entry.PayloadId);
                if (zoneIndex < 0 || _candidateVolumeFlags[zoneIndex] != 0)
                    continue;

                if (!TryBuildJobVolume(
                        zoneIndex,
                        hasPlayerBounds,
                        in playerCenterAup,
                        playerHalfExtents,
                        hasVehicleBounds,
                        in vehicleCenterAup,
                        vehicleHalfExtents,
                        out HazardVolumeData jobVolume))
                {
                    continue;
                }

                if (candidateCount >= _jobVolumes.Length)
                {
                    querySaturated = true;
                    break;
                }

                _candidateVolumeFlags[zoneIndex] = 1;
                _jobVolumes[candidateCount] = jobVolume;
                candidateCount++;
            }

            return candidateCount;
        }

        private int CopyAllActiveVolumes(
            bool hasPlayerBounds,
            in AbsoluteUniversePosition playerCenterAup,
            float3 playerHalfExtents,
            bool hasVehicleBounds,
            in AbsoluteUniversePosition vehicleCenterAup,
            float3 vehicleHalfExtents)
        {
            int count = 0;
            int sourceCount = math.min(_activeCount, _jobVolumes.Length);
            for (int i = 0; i < sourceCount; i++)
            {
                if (!TryBuildJobVolume(
                        i,
                        hasPlayerBounds,
                        in playerCenterAup,
                        playerHalfExtents,
                        hasVehicleBounds,
                        in vehicleCenterAup,
                        vehicleHalfExtents,
                        out HazardVolumeData jobVolume))
                {
                    continue;
                }

                _jobVolumes[count++] = jobVolume;
            }

            return count;
        }

        private bool TryBuildJobVolume(
            int zoneIndex,
            bool hasPlayerBounds,
            in AbsoluteUniversePosition playerCenterAup,
            float3 playerHalfExtents,
            bool hasVehicleBounds,
            in AbsoluteUniversePosition vehicleCenterAup,
            float3 vehicleHalfExtents,
            out HazardVolumeData jobVolume)
        {
            jobVolume = _volumes[zoneIndex];
            jobVolume.PlayerToxicMudBroadphase = 0;
            jobVolume.VehicleToxicMudBroadphase = 0;

            if (jobVolume.Type != HazardType.Toxicity || jobVolume.RequiresToxicMudBroadphase == 0)
                return true;

            int volumeId = _volumeIds[zoneIndex];
            if (hasPlayerBounds &&
                HectonBrineToxicMudGrid.OverlapsAupSubmergedCell(
                    volumeId,
                    in playerCenterAup,
                    ResolveConservativeBroadphaseRadius(playerHalfExtents),
                    ResolveConservativeVerticalHalfExtent(playerHalfExtents)))
            {
                jobVolume.PlayerToxicMudBroadphase = 1;
            }

            if (hasVehicleBounds &&
                HectonBrineToxicMudGrid.OverlapsAupSubmergedCell(
                    volumeId,
                    in vehicleCenterAup,
                    ResolveConservativeBroadphaseRadius(vehicleHalfExtents),
                    ResolveConservativeVerticalHalfExtent(vehicleHalfExtents)))
            {
                jobVolume.VehicleToxicMudBroadphase = 1;
            }

            return jobVolume.PlayerToxicMudBroadphase != 0 || jobVolume.VehicleToxicMudBroadphase != 0;
        }

        private bool IsSpatialQuerySaturated(int handleCount)
        {
            return _spatialQueryHandles.IsCreated &&
                   _spatialQueryHandles.Capacity > 0 &&
                   handleCount >= _spatialQueryHandles.Capacity;
        }

        private float EvaluatePointContribution(HazardVolumeData volume, double3 absolutePoint, bool toxicMudPointBroadphase)
        {
            if (volume.Type == HazardType.Toxicity &&
                volume.RequiresToxicMudBroadphase != 0 &&
                !toxicMudPointBroadphase)
            {
                return 0f;
            }

            double3 offset = volume.AbsoluteUniversePosition - absolutePoint;
            double distSqr = math.lengthsq(offset);
            double radiusSq = (double)volume.Radius * volume.Radius;
            if (distSqr >= radiusSq)
                return 0f;

            if (volume.Type == HazardType.Toxicity && volume.RequiresToxicMudBroadphase != 0)
            {
                float normalizedDistanceSq = (float)math.clamp(distSqr * volume.InvRadiusSqr, 0d, 1d);
                return volume.Intensity * ResolveSquaredVolumeCurveSample(normalizedDistanceSq);
            }

            float normalizedDistanceSqForCurve = (float)math.clamp(distSqr * volume.InvRadiusSqr, 0d, 1d);
            float attenuation = ResolveSquaredVolumeCurveSample(normalizedDistanceSqForCurve);
            if (_volumeCurveLutSamples.IsCreated)
                attenuation = SampleIntensityCurveByDistanceSq(volume.CurveLutOffset, normalizedDistanceSqForCurve);

            return volume.Intensity * attenuation;
        }

        private bool IsPointEligibleForToxicMudVolume(
            int zoneIndex,
            in HazardVolumeData volume,
            in AbsoluteUniversePosition pointAup)
        {
            if (volume.Type != HazardType.Toxicity || volume.RequiresToxicMudBroadphase == 0)
                return true;
            if (zoneIndex < 0 || !_volumeIds.IsCreated || zoneIndex >= _volumeIds.Length)
                return false;

            return HectonBrineToxicMudGrid.ContainsAupSubmergedCell(_volumeIds[zoneIndex], in pointAup);
        }

        private HazardVolumeData BuildVolumeData(int volumeIndex, int volumeId, in AbsoluteUniversePosition positionAup, float intensity, float radius, HazardType type, float visorGlitchBias)
        {
            float safeRadius = math.max(MinHazardRadius, FiniteNonNegativeOrZero(radius));
            HazardVolumeData data = default;
            data.AbsoluteUniversePosition = positionAup.ToAbsoluteDouble3();
            data.Radius = safeRadius;
            data.InvRadius = 1f / safeRadius;
            data.InvRadiusSqr = 1f / (safeRadius * safeRadius);
            data.Intensity = ClampExposure(intensity);
            data.VisorGlitchBias = ClampGlitchBias(visorGlitchBias);
            data.CurveLutOffset = volumeIndex * HazardZoneProfile.IntensityLutSampleCount;
            data.Type = type;
            data.RequiresToxicMudBroadphase = HectonBrineToxicMudGrid.IsRegisteredCell(volumeId)
                ? (byte)1
                : (byte)0;
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
            {
                float normalizedDistance = HazardZoneProfile.IntensityLutSampleCount > 1
                    ? i / (float)(HazardZoneProfile.IntensityLutSampleCount - 1)
                    : 0f;
                _volumeCurveLutSamples[lutOffset + i] = FiniteSaturate01(
                    bakedLut[i],
                    ResolveVolumeCurveSample(normalizedDistance));
            }
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

        private float SampleIntensityCurveByDistanceSq(int curveLutOffset, float normalizedDistanceSq)
        {
            if (!_volumeCurveLutSamples.IsCreated)
                return ResolveSquaredVolumeCurveSample(normalizedDistanceSq);

            float safeDistanceSq = FiniteSaturate01(normalizedDistanceSq, 0f);
            float scaledIndex = safeDistanceSq * (HazardZoneProfile.IntensityLutSampleCount - 1);
            int sampleIndex = (int)math.floor(scaledIndex);
            int nextIndex = math.min(HazardZoneProfile.IntensityLutSampleCount - 1, sampleIndex + 1);
            float fraction = scaledIndex - sampleIndex;
            float a = FiniteSaturate01(_volumeCurveLutSamples[curveLutOffset + sampleIndex], 0f);
            float b = FiniteSaturate01(_volumeCurveLutSamples[curveLutOffset + nextIndex], 0f);
            return math.lerp(a, b, fraction);
        }

        private static float ResolveVolumeCurveSample(float normalizedDistance)
        {
            float safeDistance = FiniteSaturate01(normalizedDistance, 0f);
            float attenuation = 1f - (safeDistance * safeDistance);
            return attenuation > 0f ? attenuation * attenuation : 0f;
        }

        private static float ResolveSquaredVolumeCurveSample(float normalizedDistanceSq)
        {
            float attenuation = 1f - FiniteSaturate01(normalizedDistanceSq, 0f);
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
            if (_jobRunning && DispatcherJobSwap.TryFinalizeCompleted(ref _jobHandle))
            {
                _jobRunning = false;
            }

            ClearPendingMutations();
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

        private bool TryPrepareVolumeMutation()
        {
            if (_jobRunning && !TryConsumeCompletedJobResult())
                return false;

            ApplyPendingMutationsIfIdle();
            return !_jobRunning;
        }

        private bool QueueRegisterMutation(int id, in AbsoluteUniversePosition positionAup, float intensity, float radius, HazardType type, float visorGlitchBias, HazardZoneProfile profile)
        {
            if (!IsValidHazardZoneInput(id, in positionAup, intensity, radius, type, visorGlitchBias))
                return false;

            PendingHazardZoneMutation mutation = default;
            mutation.Kind = PendingHazardZoneMutationKind.Register;
            mutation.Id = id;
            mutation.PositionAup = positionAup;
            mutation.Intensity = intensity;
            mutation.Radius = radius;
            mutation.Type = type;
            mutation.VisorGlitchBias = visorGlitchBias;
            mutation.Profile = profile;
            return QueueMutation(in mutation);
        }

        private void QueueUnregisterMutation(int id)
        {
            if (id <= 0)
                return;

            PendingHazardZoneMutation mutation = default;
            mutation.Kind = PendingHazardZoneMutationKind.Unregister;
            mutation.Id = id;
            QueueMutation(in mutation);
        }

        private bool QueueMutation(in PendingHazardZoneMutation mutation)
        {
            for (int i = 0; i < _pendingMutationCount; i++)
            {
                if (_pendingMutations[i].Id != mutation.Id)
                    continue;

                _pendingMutations[i] = mutation;
                UpdateDiagnostics();
                return true;
            }

            if (_pendingMutationCount >= _pendingMutations.Length)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    PendingMutationOverflowWarningHash,
                    HazardManagerContextHash,
                    _pendingMutationCount);
                return false;
            }

            _pendingMutations[_pendingMutationCount++] = mutation;
            UpdateDiagnostics();
            return true;
        }

        private static bool IsValidHazardZoneInput(
            int id,
            in AbsoluteUniversePosition positionAup,
            float intensity,
            float radius,
            HazardType type,
            float visorGlitchBias)
        {
            int typeIndex = (int)type;
            return id > 0 &&
                   (uint)typeIndex < (uint)HazardTypeCount &&
                   IsFiniteAup(in positionAup) &&
                   math.isfinite(intensity) &&
                   math.isfinite(radius) &&
                   math.isfinite(visorGlitchBias) &&
                   radius > 0f &&
                   intensity >= 0f &&
                   visorGlitchBias >= 0f;
        }

        private static bool IsFiniteRuntimePosition(Vector3 runtimePosition)
        {
            return math.isfinite(runtimePosition.x) &&
                   math.isfinite(runtimePosition.y) &&
                   math.isfinite(runtimePosition.z);
        }

        private static bool TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup)
        {
            originAup = GlobalSignals.CurrentRuntimeOriginAup();
            return IsFiniteAup(in originAup);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFiniteRuntimePosition(runtimePosition) ||
                !TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup))
            {
                return false;
            }

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return IsFiniteAup(in positionAup);
        }

        private static float FiniteNonNegativeOrZero(float value)
        {
            return math.isfinite(value) && value > 0f ? value : 0f;
        }

        private static float FiniteSaturate01(float value, float fallback)
        {
            return math.isfinite(value) ? math.saturate(value) : math.saturate(fallback);
        }

        private static float FiniteAtLeast(float value, float fallback, float minimum)
        {
            return math.isfinite(value) ? math.max(minimum, value) : math.max(minimum, fallback);
        }

        private static float ClampExposure(float value)
        {
            return FiniteNonNegativeOrZero(value);
        }

        private static float ClampGlitchBias(float value)
        {
            return math.clamp(FiniteNonNegativeOrZero(value), 0f, 2f);
        }

        private static Vector3 ResolveCheapAvoidanceDirection(float3 accumulatedAway)
        {
            if (!math.all(math.isfinite(accumulatedAway)))
                return Vector3.up;

            float absX = math.abs(accumulatedAway.x);
            float absY = math.abs(accumulatedAway.y);
            float absZ = math.abs(accumulatedAway.z);

            if (absX >= absY && absX >= absZ)
                return accumulatedAway.x >= 0f ? Vector3.right : Vector3.left;

            if (absY >= absZ)
                return accumulatedAway.y >= 0f ? Vector3.up : Vector3.down;

            return accumulatedAway.z >= 0f ? Vector3.forward : Vector3.back;
        }

        private static bool IsPositiveRuntimeSize(Vector3 runtimeSize)
        {
            return math.isfinite(runtimeSize.x) &&
                   math.isfinite(runtimeSize.y) &&
                   math.isfinite(runtimeSize.z) &&
                   runtimeSize.x > 0f &&
                   runtimeSize.y > 0f &&
                   runtimeSize.z > 0f;
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition positionAup)
        {
            return math.isfinite(positionAup.LocalX) &&
                   math.isfinite(positionAup.LocalY) &&
                   math.isfinite(positionAup.LocalZ);
        }

        private static bool IsFiniteBounds(Bounds bounds)
        {
            return IsFiniteRuntimePosition(bounds.center) &&
                   IsFiniteRuntimePosition(bounds.size) &&
                   IsFiniteRuntimePosition(bounds.extents);
        }

        private void ApplyPendingMutationsIfIdle()
        {
            if (_jobRunning || _pendingMutationCount <= 0 || !_volumes.IsCreated)
                return;

            int pendingCount = _pendingMutationCount;
            _pendingMutationCount = 0;
            for (int i = 0; i < pendingCount; i++)
            {
                PendingHazardZoneMutation mutation = _pendingMutations[i];
                _pendingMutations[i] = default;
                if (mutation.Kind == PendingHazardZoneMutationKind.Register)
                {
                    RegisterZoneImmediate(
                        mutation.Id,
                        in mutation.PositionAup,
                        mutation.Intensity,
                        mutation.Radius,
                        mutation.Type,
                        mutation.VisorGlitchBias,
                        mutation.Profile);
                }
                else if (mutation.Kind == PendingHazardZoneMutationKind.Unregister)
                {
                    UnregisterZoneImmediate(mutation.Id);
                }
            }

            UpdateDiagnostics();
        }

        private void ClearPendingMutations()
        {
            for (int i = 0; i < _pendingMutationCount; i++)
                _pendingMutations[i] = default;

            _pendingMutationCount = 0;
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
                clarityImpulse = math.saturate(clarityImpulse * visorBias);

            if (clarityImpulse <= 0.001f)
                return;

            HabitatDamageSignal signal = default;
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

            return math.clamp(
                FiniteAtLeast(_playerSurvival.ResolveEnvironmentalResistance(hazardType), 1f, MinResistance),
                MinResistance,
                MaxProtectedResistance);
        }

        private static float NormalizeHazardClarityContribution(HazardType hazardType, float exposure)
        {
            float safeExposure = FiniteNonNegativeOrZero(exposure);
            switch (hazardType)
            {
                case HazardType.Radiation:
                    return ResolveCheapExposureCurve(safeExposure * RadiationClarityTransferScale);

                case HazardType.Heat:
                    return ResolveCheapExposureCurve(safeExposure / math.max(0.01f, ThermalClarityTransferDenominator));

                case HazardType.Toxicity:
                    return ResolveCheapExposureCurve(safeExposure * ToxicClarityTransferScale);

                default:
                    return math.saturate(safeExposure);
            }
        }

        private static float ResolveCheapExposureCurve(float exposure)
        {
            float x = FiniteNonNegativeOrZero(exposure);
            return math.saturate(x / (1f + x));
        }

        private float SumHazardIntensityLinear(double3 absolutePoint, in AbsoluteUniversePosition pointAup, HazardType type)
        {
            float totalIntensity = 0f;
            for (int i = 0; i < _activeCount; i++)
            {
                HazardVolumeData volume = _volumes[i];
                if (volume.Type != type)
                    continue;

                totalIntensity += EvaluatePointContribution(
                    volume,
                    absolutePoint,
                    IsPointEligibleForToxicMudVolume(i, in volume, in pointAup));
            }

            return totalIntensity;
        }

        private int RegisterSpatialEntry(int id, in HazardVolumeData data)
        {
            if (_spatialHash == null)
                return 0;

            return _spatialHash.Register(
                AbsoluteUniversePosition.FromAbsolutePosition(data.AbsoluteUniversePosition),
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

            if (!_spatialHash.TryUpdateEntry(
                handle,
                AbsoluteUniversePosition.FromAbsolutePosition(data.AbsoluteUniversePosition),
                new float3(data.Radius, data.Radius, data.Radius),
                ResolveSpatialKindMask(data.Type),
                0u,
                id))
            {
                _spatialHash.Unregister(handle);
                _volumeSpatialHandles[index] = 0;
            }
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
            _registered = SystemDispatcher.GetLateFrameLane(PriorityLayer.Environment).Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void TryRegisterService()
        {
            HazardZoneManager registeredInstance = GlobalRegistry.HazardZones;
            if (_serviceRegistered || !Application.isPlaying || (registeredInstance != null && registeredInstance != this))
                return;

            GlobalRegistry.RegisterHazardZoneRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.HazardZones, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterHazardZoneRuntime(this);
            _serviceRegistered = false;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogRegistryOverflow()
        {
            UnityEngine.Debug.LogWarning(OverflowLogText);
        }
#endif

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
            _debugPendingMutationCount = _pendingMutationCount;
        }

        private enum PendingHazardZoneMutationKind : byte
        {
            None = 0,
            Register = 1,
            Unregister = 2
        }

        private struct PendingHazardZoneMutation
        {
            public AbsoluteUniversePosition PositionAup;
            public HazardZoneProfile Profile;
            public HazardType Type;
            public float Intensity;
            public float Radius;
            public float VisorGlitchBias;
            public int Id;
            public PendingHazardZoneMutationKind Kind;
        }
    }
}
