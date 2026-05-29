using System.Diagnostics;
using System.Runtime.InteropServices;
using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
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
        [ReadOnly, NoAlias] public NativeArray<HazardVolumeData>.ReadOnly Volumes;
        [ReadOnly, NoAlias] public NativeArray<float>.ReadOnly CurveLutSamples;
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
            NativeArray<float>.ReadOnly curveLutSamples,
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
            NativeArray<float>.ReadOnly curveLutSamples,
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
    public sealed class HazardZoneManager : MonoBehaviour, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener, IHazardZoneReadModel
    {
        private static int s_x001HazardZoneManagerSignalPushDropCount;
        private const int HazardTypeCount = 4;
        private const int DefaultMaxZoneCount = 512;
        private const int MinZoneCapacity = 32;
        private const int PendingMutationCapacity = 64;
        private const float HazardStepIntervalSeconds = 0.1f;
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
        private const float ToxicityPoisonStatusDurationSeconds = 5f;
        private const float ToxicityExposureToxemiaScale = 0.08f;
        private const float RadiationClarityTransferScale = 0.85f;
        private const float ThermalClarityTransferDenominator = 18f;
        private const float ToxicClarityTransferScale = 1.35f;
        private const float MinResistance = 0.1f;
        private const float MaxProtectedResistance = 1000f;
        private const float ConservativeAabbSphereFactor = 1.7320508f;
        private const uint ToxicityHazardChemicalHash = 0x544F5848u; // TOXH
        private const ulong HazardStateMutationGuardMask =
            (1UL << ((int)BufferID.HazardZoneVolumes & 31)) |
            (1UL << ((int)BufferID.HazardZoneVolumeIds & 31)) |
            (1UL << ((int)BufferID.HazardZoneSpatialHandles & 31)) |
            (1UL << ((int)BufferID.HazardZoneCurveLutSamples & 31));
        private const ulong ExposureJobMutationGuardMask =
            (1UL << ((int)BufferID.HazardZoneJobVolumes & 31)) |
            (1UL << ((int)BufferID.HazardZoneCurveLutSamples & 31)) |
            (1UL << ((int)BufferID.HazardExposureJobResult & 31));
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

        private HazardVaultArray<HazardVolumeData> _volumes;
        private HazardVaultArray<int> _volumeIds;
        private HazardVaultArray<int> _volumeSpatialHandles;
        private HazardVaultArray<float> _volumeCurveLutSamples;
        private HazardVaultArray<HazardVolumeData> _jobVolumes;
        private VaultGenerationHandle<HazardExposureJobResult> _jobResultHandle;
        private IDataVault _dataVault;
        private HazardVaultArray<byte> _candidateVolumeFlags;
        private JobHandle _jobHandle;
        private HectonSpatialHash _spatialHash;
        private HazardVaultArray<int> _spatialQueryHandles;
        private bool _jobRunning;
        private bool _exposureJobGuardHeld;
        private bool _registered;
        private bool _serviceRegistered;
        private bool _hotSwapRegistered;
        private bool _ownsJobResultHandle;
        private bool _pendingDataVaultSwap;
        private int _activeCount;
        private float _toxicityDose;
        private float _toxicityPulseAccumulatorSeconds;
        private int _publishedExposureMask;

        private Transform _playerTransform;
        private IDataVault _pendingDataVault;
        private IDataVault _exposureJobGuardVault;
        private HectonSurvivalSystem _playerSurvival;
        private HectonPlayerHealth _playerHealth;
        private TraumaDispatcher _playerTraumaDispatcher;
        private PlayerTransportCoordinator _playerTransportCoordinator;
        private Collider _playerCollider;
        private IPlayerRuntimeContext _playerRuntimeContext;
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

        private struct HazardVaultArray<T> where T : struct
        {
            private IDataVault _vault;
            private VaultGenerationHandle<T> _handle;
            private int _requiredLength;

            public bool IsCreated => TryReadOnly(out NativeArray<T>.ReadOnly buffer) && buffer.Length >= _requiredLength;

            public int Length => TryReadOnly(out NativeArray<T>.ReadOnly buffer) ? buffer.Length : 0;

            public int Capacity => _requiredLength;

            public void Bind(IDataVault vault, in VaultGenerationHandle<T> handle, int requiredLength)
            {
                _vault = vault;
                _handle = handle;
                _requiredLength = math.max(0, requiredLength);
            }

            public bool TryReadOnly(out NativeArray<T>.ReadOnly buffer)
            {
                buffer = default;
                return _vault != null &&
                       IsVaultHandleCreated(in _handle) &&
                       _vault.TryReadOnlyHandle(in _handle, out buffer) &&
                       buffer.IsCreated &&
                       buffer.Length >= _requiredLength;
            }

            public bool TryResolveMutable(out NativeArray<T> buffer)
            {
                buffer = default;
                return _vault != null &&
                       IsVaultHandleCreated(in _handle) &&
                       _vault.TryResolveHandle(in _handle, out buffer) &&
                       buffer.IsCreated &&
                       buffer.Length >= _requiredLength;
            }

            public void ReleaseBuffer()
            {
                if (_vault != null && IsVaultHandleCreated(in _handle))
                    _vault.ReleaseBuffer(in _handle);

                _vault = null;
                _handle = default;
                _requiredLength = 0;
            }
        }

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
            if (!TryAcquireHazardStateWriteViews(
                    out NativeArray<HazardVolumeData> volumes,
                    out NativeArray<int> volumeIds,
                    out NativeArray<int> volumeSpatialHandles,
                    out NativeArray<float> volumeCurveLutSamples))
            {
                return false;
            }

            try
            {
                return RegisterZoneImmediate(
                    id,
                    in positionAup,
                    intensity,
                    radius,
                    type,
                    visorGlitchBias,
                    profile,
                    volumes,
                    volumeIds,
                    volumeSpatialHandles,
                    volumeCurveLutSamples);
            }
            finally
            {
                ReleaseHazardStateWriteViews();
            }
        }

        private bool RegisterZoneImmediate(
            int id,
            in AbsoluteUniversePosition positionAup,
            float intensity,
            float radius,
            HazardType type,
            float visorGlitchBias,
            HazardZoneProfile profile,
            NativeArray<HazardVolumeData> volumes,
            NativeArray<int> volumeIds,
            NativeArray<int> volumeSpatialHandles,
            NativeArray<float> volumeCurveLutSamples)
        {
            int capacity = ResolveActiveVolumeCapacity(volumes, volumeIds, volumeSpatialHandles, volumeCurveLutSamples);
            if (capacity <= 0)
                return false;

            int existingIndex = FindZoneIndex(id, volumeIds);
            if (existingIndex >= 0)
            {
                HazardVolumeData data = BuildVolumeData(existingIndex, id, in positionAup, intensity, radius, type, visorGlitchBias);
                WriteVolumeCurveLut(existingIndex, profile, volumeCurveLutSamples);
                volumes[existingIndex] = data;
                UpdateSpatialEntry(existingIndex, id, in data, volumeSpatialHandles);
                return true;
            }

            if (_activeCount >= capacity)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LogRegistryOverflow();
#endif
                return false;
            }

            HazardVolumeData newData = BuildVolumeData(_activeCount, id, in positionAup, intensity, radius, type, visorGlitchBias);
            volumeIds[_activeCount] = id;
            volumes[_activeCount] = newData;
            WriteVolumeCurveLut(_activeCount, profile, volumeCurveLutSamples);
            volumeSpatialHandles[_activeCount] = RegisterSpatialEntry(id, in newData);
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
            if (!TryAcquireHazardStateWriteViews(
                    out NativeArray<HazardVolumeData> volumes,
                    out NativeArray<int> volumeIds,
                    out NativeArray<int> volumeSpatialHandles,
                    out NativeArray<float> volumeCurveLutSamples))
            {
                return;
            }

            try
            {
                UnregisterZoneImmediate(id, volumes, volumeIds, volumeSpatialHandles, volumeCurveLutSamples);
            }
            finally
            {
                ReleaseHazardStateWriteViews();
            }
        }

        private void UnregisterZoneImmediate(
            int id,
            NativeArray<HazardVolumeData> volumes,
            NativeArray<int> volumeIds,
            NativeArray<int> volumeSpatialHandles,
            NativeArray<float> volumeCurveLutSamples)
        {
            int index = FindZoneIndex(id, volumeIds);
            if (index < 0)
                return;

            int lastIndex = _activeCount - 1;
            UnregisterSpatialEntry(index, volumeSpatialHandles);
            if (index != lastIndex)
            {
                volumeIds[index] = volumeIds[lastIndex];
                volumes[index] = volumes[lastIndex];
                volumeSpatialHandles[index] = volumeSpatialHandles[lastIndex];
                CopyVolumeCurveLut(lastIndex, index, volumeCurveLutSamples);
                HazardVolumeData movedVolume = volumes[index];
                movedVolume.CurveLutOffset = index * HazardZoneProfile.IntensityLutSampleCount;
                volumes[index] = movedVolume;
                UpdateSpatialEntry(index, volumeIds[index], in movedVolume, volumeSpatialHandles);
            }

            volumeIds[lastIndex] = 0;
            volumes[lastIndex] = default;
            volumeSpatialHandles[lastIndex] = 0;
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

            if (!_volumes.TryReadOnly(out NativeArray<HazardVolumeData>.ReadOnly readVolumes) ||
                !_volumeIds.TryReadOnly(out NativeArray<int>.ReadOnly readVolumeIds))
            {
                return 0f;
            }

            _volumeCurveLutSamples.TryReadOnly(out NativeArray<float>.ReadOnly readCurveLutSamples);
            double3 absolutePoint = pointAup.ToAbsoluteDouble3();
            return SumHazardIntensityLinear(
                absolutePoint,
                in pointAup,
                type,
                readVolumes,
                readVolumeIds,
                readCurveLutSamples);
        }

        public float GetToxicityIntensity(in AbsoluteUniversePosition pointAup)
        {
            return GetHazardIntensity(in pointAup, HazardType.Toxicity);
        }

        public bool TrySampleHazardAvoidance(Vector3 runtimePoint, float sampleRadius, out Vector3 fleeDirection, out float hazardPressure01)
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
            if (_activeCount <= 0 ||
                !_volumes.TryReadOnly(out NativeArray<HazardVolumeData>.ReadOnly readVolumes) ||
                !_volumeIds.TryReadOnly(out NativeArray<int>.ReadOnly readVolumeIds) ||
                !IsFiniteAup(in pointAup) ||
                !math.isfinite(sampleRadius) ||
                sampleRadius <= 0.001f)
                return false;

            _volumeCurveLutSamples.TryReadOnly(out NativeArray<float>.ReadOnly readCurveLutSamples);
            double3 absolutePoint = pointAup.ToAbsoluteDouble3();
            float3 accumulatedAway = float3.zero;
            float peakPressure = 0f;
            int readCount = math.min(_activeCount, math.min(readVolumes.Length, readVolumeIds.Length));
            for (int i = 0; i < readCount; i++)
            {
                HazardVolumeData volume = readVolumes[i];
                double3 offset = volume.AbsoluteUniversePosition - absolutePoint;
                double effectiveQueryRadius = sampleRadius + math.max(MinHazardRadius, volume.Radius);
                if (math.lengthsq(offset) > effectiveQueryRadius * effectiveQueryRadius)
                    continue;

                AccumulateAvoidanceContribution(
                    i,
                    volume,
                    in pointAup,
                    absolutePoint,
                    readVolumeIds,
                    readCurveLutSamples,
                    ref accumulatedAway,
                    ref peakPressure);
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
            CachePlayerRuntimeContextCold();
            ResolvePlayerContext();
            UpdateDiagnostics();
        }

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            CacheHazardVaultCold(GlobalRegistry.DataVault);
            AllocateNativeState();
            CachePlayerRuntimeContextCold();
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
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                if (_playerRuntimeContext != null)
                    ApplyPlayerContextReferences(
                        _playerRuntimeContext.PlayerTransform,
                        _playerRuntimeContext.PlayerCollider,
                        _playerRuntimeContext.PlayerHealth,
                        _playerRuntimeContext.SurvivalSystem,
                        _playerRuntimeContext.TraumaDispatcher,
                        _playerRuntimeContext.PlayerTransportCoordinator);
                return;
            }

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
            ReleaseHazardVaultBuffers();
            CacheHazardVaultCold(nextVault);
            if (!_jobRunning)
                AllocateNativeState();
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
        /// Runs the hazard/toxicity authority step on the dispatcher slow cadence.
        /// </summary>
        public void SlowTick()
        {
            if (!_volumes.IsCreated)
                return;

            AdvanceHazardStep(HazardStepIntervalSeconds);
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

            IDataVault vault = _dataVault;
            if (vault == null || vault.IsAllocationLocked)
                return;

            int safeCapacity = math.max(MinZoneCapacity, maxZoneCount);
            int curveSampleCapacity = safeCapacity * HazardZoneProfile.IntensityLutSampleCount;
            bool buffersReady =
                TryEnsureHazardVaultArray(ref _volumes, BufferID.HazardZoneVolumes, safeCapacity, NativeArrayOptions.ClearMemory) &&
                TryEnsureHazardVaultArray(ref _volumeIds, BufferID.HazardZoneVolumeIds, safeCapacity, NativeArrayOptions.ClearMemory) &&
                TryEnsureHazardVaultArray(ref _volumeSpatialHandles, BufferID.HazardZoneSpatialHandles, safeCapacity, NativeArrayOptions.ClearMemory) &&
                TryEnsureHazardVaultArray(ref _volumeCurveLutSamples, BufferID.HazardZoneCurveLutSamples, curveSampleCapacity, NativeArrayOptions.ClearMemory) &&
                TryEnsureHazardVaultArray(ref _jobVolumes, BufferID.HazardZoneJobVolumes, safeCapacity, NativeArrayOptions.ClearMemory) &&
                TryEnsureHazardVaultArray(ref _candidateVolumeFlags, BufferID.HazardZoneCandidateVolumeFlags, safeCapacity, NativeArrayOptions.ClearMemory) &&
                TryEnsureHazardVaultArray(ref _spatialQueryHandles, BufferID.HazardZoneSpatialQueryHandles, HazardSpatialQueryCapacity, NativeArrayOptions.ClearMemory);
            if (!buffersReady)
            {
                ReleaseHazardVaultBuffers();
                return;
            }

            _ = TryPrepareHazardExposureResultBuffer(out _, allowAllocation: true);
            _spatialHash = new HectonSpatialHash(
                safeCapacity,
                safeCapacity * 6,
                HazardSpatialCellSizeMeters,
                NativeAllocationLifetime.Session);
        }

        private void DisposeNativeState()
        {
            if (_jobRunning)
            {
                ForceCompleteExposureJobInPostSimulationWindow();
                _jobRunning = false;
            }

            _jobHandle = default;
            ReleaseExposureJobLocks();
            _pendingDataVault = null;
            _pendingDataVaultSwap = false;
            ReleaseHazardExposureResultBuffer();
            ReleaseHazardVaultBuffers();

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
                    runtimeContext.PlayerHealth,
                    runtimeContext.SurvivalSystem,
                    runtimeContext.TraumaDispatcher,
                    runtimeContext.PlayerTransportCoordinator);
            }
            else
            {
                IPlayerRuntimeContext playerContext = _playerRuntimeContext;
                if (playerContext != null)
                {
                    ApplyPlayerContextReferences(
                        playerContext.PlayerTransform,
                        playerContext.PlayerCollider,
                        playerContext.PlayerHealth,
                        playerContext.SurvivalSystem,
                        playerContext.TraumaDispatcher,
                        playerContext.PlayerTransportCoordinator);
                }
            }

            if (_playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref _playerTransform);

            if (_playerTransform == null)
                return;

            IPlayerTransportLifecycleOwner resolvedOwner = null;
            if (_playerTransportCoordinator != null)
                _playerTransportCoordinator.TryResolveTransportLifecycleOwner(out resolvedOwner);

            if (ReferenceEquals(_activeTransportOwner, resolvedOwner))
                return;

            _activeTransportOwner = resolvedOwner;
            _activeTransportBehaviour = resolvedOwner as MonoBehaviour;
            _activeTransportCollider = ResolveTransportColliderCold(_activeTransportBehaviour);
        }

        private void RefreshPlayerContextSnapshot()
        {
            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext))
            {
                ApplyPlayerContextReferences(
                    runtimeContext.PlayerTransform,
                    runtimeContext.PlayerCollider,
                    runtimeContext.PlayerHealth,
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
            _activeTransportCollider = ResolveTransportColliderCold(_activeTransportBehaviour);
        }

        private void ApplyPlayerContextReferences(
            Transform playerTransform,
            Collider playerCollider,
            HectonPlayerHealth playerHealth,
            HectonSurvivalSystem survivalSystem,
            TraumaDispatcher traumaDispatcher,
            PlayerTransportCoordinator transportCoordinator)
        {
            if (playerTransform != null && !ReferenceEquals(_playerTransform, playerTransform))
            {
                _playerTransform = playerTransform;
                _playerCollider = null;
                _playerSurvival = null;
                _playerHealth = null;
                _playerTraumaDispatcher = null;
                _playerTransportCoordinator = null;
            }

            if (playerCollider != null)
                _playerCollider = playerCollider;

            if (playerHealth != null)
                _playerHealth = playerHealth;

            if (survivalSystem != null)
                _playerSurvival = survivalSystem;

            if (traumaDispatcher != null)
                _playerTraumaDispatcher = traumaDispatcher;

            if (transportCoordinator != null)
                _playerTransportCoordinator = transportCoordinator;
        }

        private static Collider ResolveTransportColliderCold(MonoBehaviour transportBehaviour)
        {
            return transportBehaviour != null
                ? ComponentReferenceUtility.ResolveOwnedComponent<Collider>(transportBehaviour.transform)
                : null;
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

            try
            {
                if (!TryPrepareHazardExposureResultBuffer(out NativeArray<HazardExposureJobResult> jobResult, allowAllocation: false))
                {
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
            finally
            {
                ReleaseExposureJobLocks();
            }
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
                    _toxicityPulseAccumulatorSeconds = 0f;
            }

            if (_toxicityDose <= ToxicityDoseThreshold || _playerSurvival == null)
                return;

            _toxicityPulseAccumulatorSeconds += safeDt;
            while (_toxicityPulseAccumulatorSeconds >= ToxicityDamagePulseIntervalSeconds)
            {
                _toxicityPulseAccumulatorSeconds -= ToxicityDamagePulseIntervalSeconds;
                ApplyToxicityDamagePulse(currentToxicityIntensity);
            }
        }

        private void ApplyToxicityDamagePulse(float currentIntensity)
        {
            float overdose = math.max(0f, _toxicityDose - ToxicityDoseThreshold);
            float damageMagnitude = ToxicityDamagePerPulse *
                                    math.max(0.25f, currentIntensity) *
                                    (1f + overdose * ToxicityOverdoseDamageScale);

            int targetId = ResolvePlayerCombatTargetId();
            PublishToxicityExposureSignal(targetId, damageMagnitude, currentIntensity);
            _ = TryQueueToxicityPoisonStatus(targetId, damageMagnitude, currentIntensity);
        }

        private int ResolvePlayerCombatTargetId()
        {
            HectonPlayerHealth playerHealth = _playerHealth;
            return playerHealth != null
                ? CombatDamageRuntime.ResolveTargetId(playerHealth.gameObject)
                : 0;
        }

        private static bool TryQueueToxicityPoisonStatus(int targetId, float damageMagnitude, float currentIntensity)
        {
            if (targetId == 0 || !CombatDamageRuntime.IsTargetRegistered(targetId))
                return false;

            float severity01 = math.saturate(math.max(currentIntensity, damageMagnitude * 0.05f));
            float durationSeconds = ToxicityPoisonStatusDurationSeconds * math.max(0.25f, severity01);
            return CombatDamageRuntime.TryQueueStatusEffect(
                targetId,
                CombatStatusBits.Poisoned64,
                durationSeconds,
                DamageSourceIds.EnvironmentHazard,
                severity01);
        }

        private void PublishToxicityExposureSignal(int targetId, float damageMagnitude, float currentIntensity)
        {
            if (targetId == 0)
                return;

            if (!TryResolvePlayerPredictedAup(out AbsoluteUniversePosition playerAup) &&
                (_playerTransform == null || !TryResolveAupFromRuntimeOrigin(_playerTransform.position, out playerAup)))
            {
                return;
            }

            float exposure01 = math.saturate(currentIntensity);
            float toxemiaDelta = math.saturate(exposure01 * math.max(0.1f, damageMagnitude) * ToxicityExposureToxemiaScale);
            if (exposure01 <= 0.0001f && toxemiaDelta <= 0f)
                return;

            ToxicityExposureSignal signal = default;
            signal.AUP = playerAup.ToAbsoluteDouble3();
            signal.Exposure01 = exposure01;
            signal.ToxemiaDelta = toxemiaDelta;
            signal.EntityId = unchecked((uint)targetId);
            signal.ChemicalHash = ToxicityHazardChemicalHash;
            signal.Frame = TimeSliceScheduler.CurrentFrameId;
            signal.Flags = 1;
            SignalBus<ToxicityExposureSignal>.TryPushTracked(in signal, ref s_x001HazardZoneManagerSignalPushDropCount);
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

            if (!TryPrepareHazardExposureResultBuffer(out _, allowAllocation: false) ||
                !TryAcquireExposureJobGuard())
            {
                ClearExposureState();
                return;
            }

            bool keepJobGuard = false;
            int candidateCount;
            try
            {
                if (!_jobVolumes.TryResolveMutable(out NativeArray<HazardVolumeData> lockedJobVolumes))
                {
                    ClearExposureState();
                    return;
                }

                if (!_volumes.TryReadOnly(out NativeArray<HazardVolumeData>.ReadOnly readVolumes) ||
                    !_volumeIds.TryReadOnly(out NativeArray<int>.ReadOnly readVolumeIds))
                {
                    ClearExposureState();
                    return;
                }

                candidateCount = CopyAllActiveVolumes(
                    hasPlayerBounds,
                    in playerCenterAup,
                    playerHalfExtents,
                    hasVehicleBounds,
                    in vehicleCenterAup,
                    vehicleHalfExtents,
                    lockedJobVolumes,
                    readVolumes,
                    readVolumeIds);

                if (candidateCount <= 0)
                {
                    ClearExposureState();
                    return;
                }

                if (!_jobVolumes.TryReadOnly(out NativeArray<HazardVolumeData>.ReadOnly readJobVolumes) ||
                    !readJobVolumes.IsCreated ||
                    readJobVolumes.Length < candidateCount ||
                    !_volumeCurveLutSamples.TryReadOnly(out NativeArray<float>.ReadOnly readCurveLutSamples) ||
                    !readCurveLutSamples.IsCreated ||
                    readCurveLutSamples.Length < HazardZoneProfile.IntensityLutSampleCount ||
                    !TryPrepareHazardExposureResultBuffer(out NativeArray<HazardExposureJobResult> jobResult, allowAllocation: false) ||
                    !jobResult.IsCreated ||
                    jobResult.Length < 1)
                {
                    ClearExposureState();
                    return;
                }

                jobResult[0] = default;
                EvaluateHazardExposureJob job = new EvaluateHazardExposureJob
                {
                    Volumes = readJobVolumes,
                    CurveLutSamples = readCurveLutSamples,
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
                keepJobGuard = true;
            }
            finally
            {
                if (!keepJobGuard)
                    ReleaseExposureJobLocks();
            }
        }

        private bool TryPrepareHazardExposureResultBuffer(out NativeArray<HazardExposureJobResult> jobResult, bool allowAllocation)
        {
            jobResult = default;
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
            {
                ClearHazardExposureResultDescriptorIfUnlocked();
                return false;
            }

            if (IsVaultHandleCreated(in _jobResultHandle) &&
                vault.TryResolveHandle(in _jobResultHandle, out jobResult) &&
                jobResult.IsCreated &&
                jobResult.Length >= 1)
            {
                return true;
            }

            if (_exposureJobGuardHeld)
                return false;

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

            VaultGenerationHandle<HazardExposureJobResult> acquired = vault.EnsureGenerationHandle<HazardExposureJobResult>(
                BufferID.HazardExposureJobResult,
                1,
                SystemID.GameplayHazards,
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

        private bool TryAcquireHazardStateWriteViews(
            out NativeArray<HazardVolumeData> volumes,
            out NativeArray<int> volumeIds,
            out NativeArray<int> volumeSpatialHandles,
            out NativeArray<float> volumeCurveLutSamples)
        {
            volumes = default;
            volumeIds = default;
            volumeSpatialHandles = default;
            volumeCurveLutSamples = default;

            IDataVault vault = _dataVault;
            if (vault == null || !vault.TryAcquireMutationGuard(HazardStateMutationGuardMask))
                return false;

            if (!_volumes.TryResolveMutable(out volumes) ||
                !_volumeIds.TryResolveMutable(out volumeIds) ||
                !_volumeSpatialHandles.TryResolveMutable(out volumeSpatialHandles) ||
                !_volumeCurveLutSamples.TryResolveMutable(out volumeCurveLutSamples))
            {
                vault.ReleaseMutationGuard(HazardStateMutationGuardMask);
                volumes = default;
                volumeIds = default;
                volumeSpatialHandles = default;
                volumeCurveLutSamples = default;
                return false;
            }

            if (ResolveActiveVolumeCapacity(volumes, volumeIds, volumeSpatialHandles, volumeCurveLutSamples) <= 0)
            {
                vault.ReleaseMutationGuard(HazardStateMutationGuardMask);
                volumes = default;
                volumeIds = default;
                volumeSpatialHandles = default;
                volumeCurveLutSamples = default;
                return false;
            }

            return true;
        }

        private void ReleaseHazardStateWriteViews()
        {
            IDataVault vault = _dataVault;
            if (vault != null)
                vault.ReleaseMutationGuard(HazardStateMutationGuardMask);
        }

        private static int ResolveActiveVolumeCapacity(
            NativeArray<HazardVolumeData> volumes,
            NativeArray<int> volumeIds,
            NativeArray<int> volumeSpatialHandles,
            NativeArray<float> volumeCurveLutSamples)
        {
            if (!volumes.IsCreated ||
                !volumeIds.IsCreated ||
                !volumeSpatialHandles.IsCreated ||
                !volumeCurveLutSamples.IsCreated)
            {
                return 0;
            }

            int curveCapacity = volumeCurveLutSamples.Length / HazardZoneProfile.IntensityLutSampleCount;
            return math.min(volumes.Length, math.min(volumeIds.Length, math.min(volumeSpatialHandles.Length, curveCapacity)));
        }

        private bool TryEnsureHazardVaultArray<T>(
            ref HazardVaultArray<T> target,
            BufferID bufferId,
            int requiredCapacity,
            NativeArrayOptions options)
            where T : struct
        {
            int safeCapacity = math.max(1, requiredCapacity);
            if (target.IsCreated && target.Length >= safeCapacity)
                return true;

            IDataVault vault = _dataVault;
            if (vault == null || vault.IsAllocationLocked)
                return false;

            VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                safeCapacity,
                SystemID.GameplayHazards,
                options);
            if (!IsVaultHandleCreated(in handle) ||
                !vault.TryResolveHandle(in handle, out NativeArray<T> buffer) ||
                !buffer.IsCreated ||
                buffer.Length < safeCapacity)
            {
                return false;
            }

            target.Bind(vault, in handle, safeCapacity);
            return true;
        }

        private void CacheHazardVaultCold(IDataVault vault)
        {
            if (ReferenceEquals(_dataVault, vault))
                return;

            ClearHazardExposureResultDescriptor();
            _dataVault = vault;
        }

        private void ReleaseHazardVaultBuffers()
        {
            ReleaseExposureJobLocks();
            _volumes.ReleaseBuffer();
            _volumeIds.ReleaseBuffer();
            _volumeSpatialHandles.ReleaseBuffer();
            _volumeCurveLutSamples.ReleaseBuffer();
            _jobVolumes.ReleaseBuffer();
            _candidateVolumeFlags.ReleaseBuffer();
            _spatialQueryHandles.ReleaseBuffer();
            _activeCount = 0;
        }

        private void ReleaseExposureJobLocks()
        {
            if (!_exposureJobGuardHeld)
                return;

            IDataVault vault = _exposureJobGuardVault ?? _dataVault;
            _exposureJobGuardVault = null;
            _exposureJobGuardHeld = false;
            if (vault != null)
                vault.ReleaseMutationGuard(ExposureJobMutationGuardMask);
        }

        private void CachePlayerRuntimeContextCold()
        {
            _playerRuntimeContext = GlobalRegistry.Player;
        }

        private void ReleaseHazardExposureResultBuffer()
        {
            IDataVault vault = _dataVault;
            if (!_ownsJobResultHandle ||
                _jobRunning ||
                _exposureJobGuardHeld ||
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

        private void ClearHazardExposureResultDescriptorIfUnlocked()
        {
            if (!_exposureJobGuardHeld)
                ClearHazardExposureResultDescriptor();
        }

        private bool TryAcquireExposureJobGuard()
        {
            if (_exposureJobGuardHeld)
                return true;

            IDataVault vault = _dataVault;
            if (vault == null || !vault.TryAcquireMutationGuard(ExposureJobMutationGuardMask))
                return false;

            _exposureJobGuardVault = vault;
            _exposureJobGuardHeld = true;
            return true;
        }

        private void ForceCompleteExposureJobInPostSimulationWindow()
        {
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                DispatcherJobFence.TryComplete(ref _jobHandle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
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

        private int CollectCandidateVolumes(
            bool hasPlayerBounds,
            in AbsoluteUniversePosition playerCenterAup,
            float3 playerHalfExtents,
            bool hasVehicleBounds,
            in AbsoluteUniversePosition vehicleCenterAup,
            float3 vehicleHalfExtents,
            NativeArray<HazardVolumeData> jobVolumes,
            NativeArray<byte> candidateVolumeFlags,
            NativeArray<int> spatialQueryHandles,
            NativeArray<HazardVolumeData>.ReadOnly readVolumes,
            NativeArray<int>.ReadOnly readVolumeIds)
        {
            if (_spatialHash == null ||
                !candidateVolumeFlags.IsCreated ||
                !jobVolumes.IsCreated ||
                !readVolumes.IsCreated ||
                !readVolumeIds.IsCreated ||
                !spatialQueryHandles.IsCreated)
            {
                return CopyAllActiveVolumes(
                    hasPlayerBounds,
                    in playerCenterAup,
                    playerHalfExtents,
                    hasVehicleBounds,
                    in vehicleCenterAup,
                    vehicleHalfExtents,
                    jobVolumes,
                    readVolumes,
                    readVolumeIds);
            }

            for (int i = 0; i < _activeCount; i++)
                candidateVolumeFlags[i] = 0;

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
                    jobVolumes,
                    candidateVolumeFlags,
                    spatialQueryHandles,
                    readVolumes,
                    readVolumeIds,
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
                    jobVolumes,
                    candidateVolumeFlags,
                    spatialQueryHandles,
                    readVolumes,
                    readVolumeIds,
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
                    vehicleHalfExtents,
                    jobVolumes,
                    readVolumes,
                    readVolumeIds);
            }

            return candidateCount;
        }

        private void AccumulateAvoidanceContribution(
            int zoneIndex,
            HazardVolumeData volume,
            in AbsoluteUniversePosition pointAup,
            double3 absolutePoint,
            NativeArray<int>.ReadOnly volumeIds,
            NativeArray<float>.ReadOnly curveLutSamples,
            ref float3 accumulatedAway,
            ref float peakPressure)
        {
            float contribution = EvaluatePointContribution(
                volume,
                absolutePoint,
                IsPointEligibleForToxicMudVolume(zoneIndex, in volume, in pointAup, volumeIds),
                curveLutSamples);
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
            NativeArray<HazardVolumeData> jobVolumes,
            NativeArray<byte> candidateVolumeFlags,
            NativeArray<int> spatialQueryHandles,
            NativeArray<HazardVolumeData>.ReadOnly readVolumes,
            NativeArray<int>.ReadOnly readVolumeIds,
            out bool querySaturated)
        {
            int handleCount = _spatialHash.CollectSphere(
                absoluteCenter,
                queryRadius,
                HazardSpatialLayerMask,
                spatialQueryHandles);
            querySaturated = IsSpatialQuerySaturated(handleCount, spatialQueryHandles);

            for (int i = 0; i < handleCount; i++)
            {
                if (!_spatialHash.TryGetEntry(spatialQueryHandles[i], out HectonSpatialHash.SpatialEntry entry))
                    continue;

                int zoneIndex = FindZoneIndex(entry.PayloadId, readVolumeIds);
                if (zoneIndex < 0 ||
                    zoneIndex >= readVolumes.Length ||
                    zoneIndex >= candidateVolumeFlags.Length ||
                    candidateVolumeFlags[zoneIndex] != 0)
                    continue;

                if (!TryBuildJobVolume(
                        zoneIndex,
                        hasPlayerBounds,
                        in playerCenterAup,
                        playerHalfExtents,
                        hasVehicleBounds,
                        in vehicleCenterAup,
                        vehicleHalfExtents,
                        readVolumes,
                        readVolumeIds,
                        out HazardVolumeData jobVolume))
                {
                    continue;
                }

                if (candidateCount >= jobVolumes.Length)
                {
                    querySaturated = true;
                    break;
                }

                candidateVolumeFlags[zoneIndex] = 1;
                jobVolumes[candidateCount] = jobVolume;
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
            float3 vehicleHalfExtents,
            NativeArray<HazardVolumeData> jobVolumes,
            NativeArray<HazardVolumeData>.ReadOnly readVolumes,
            NativeArray<int>.ReadOnly readVolumeIds)
        {
            int count = 0;
            int sourceCount = math.min(_activeCount, math.min(jobVolumes.Length, math.min(readVolumes.Length, readVolumeIds.Length)));
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
                        readVolumes,
                        readVolumeIds,
                        out HazardVolumeData jobVolume))
                {
                    continue;
                }

                jobVolumes[count++] = jobVolume;
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
            NativeArray<HazardVolumeData>.ReadOnly readVolumes,
            NativeArray<int>.ReadOnly readVolumeIds,
            out HazardVolumeData jobVolume)
        {
            jobVolume = readVolumes[zoneIndex];
            jobVolume.PlayerToxicMudBroadphase = 0;
            jobVolume.VehicleToxicMudBroadphase = 0;

            if (jobVolume.Type != HazardType.Toxicity || jobVolume.RequiresToxicMudBroadphase == 0)
                return true;

            int volumeId = readVolumeIds[zoneIndex];
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

        private static bool IsSpatialQuerySaturated(int handleCount, NativeArray<int> spatialQueryHandles)
        {
            return spatialQueryHandles.IsCreated &&
                   spatialQueryHandles.Length > 0 &&
                   handleCount >= spatialQueryHandles.Length;
        }

        private float EvaluatePointContribution(
            HazardVolumeData volume,
            double3 absolutePoint,
            bool toxicMudPointBroadphase,
            NativeArray<float>.ReadOnly curveLutSamples)
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
            if (curveLutSamples.IsCreated)
                attenuation = SampleIntensityCurveByDistanceSq(volume.CurveLutOffset, normalizedDistanceSqForCurve, curveLutSamples);

            return volume.Intensity * attenuation;
        }

        private bool IsPointEligibleForToxicMudVolume(
            int zoneIndex,
            in HazardVolumeData volume,
            in AbsoluteUniversePosition pointAup,
            NativeArray<int>.ReadOnly volumeIds)
        {
            if (volume.Type != HazardType.Toxicity || volume.RequiresToxicMudBroadphase == 0)
                return true;
            if (zoneIndex < 0)
                return false;

            return volumeIds.IsCreated &&
                   zoneIndex < volumeIds.Length &&
                   HectonBrineToxicMudGrid.ContainsAupSubmergedCell(volumeIds[zoneIndex], in pointAup);
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

        private void WriteVolumeCurveLut(int volumeIndex, HazardZoneProfile profile, NativeArray<float> volumeCurveLutSamples)
        {
            if (!volumeCurveLutSamples.IsCreated)
                return;

            int lutOffset = volumeIndex * HazardZoneProfile.IntensityLutSampleCount;
            float[] bakedLut = profile != null ? profile.BakedIntensityLut : null;
            if (bakedLut == null || bakedLut.Length < HazardZoneProfile.IntensityLutSampleCount)
            {
                WriteDefaultCurveLut(lutOffset, volumeCurveLutSamples);
                return;
            }

            for (int i = 0; i < HazardZoneProfile.IntensityLutSampleCount; i++)
            {
                float normalizedDistance = HazardZoneProfile.IntensityLutSampleCount > 1
                    ? i / (float)(HazardZoneProfile.IntensityLutSampleCount - 1)
                    : 0f;
                volumeCurveLutSamples[lutOffset + i] = FiniteSaturate01(
                    bakedLut[i],
                    ResolveVolumeCurveSample(normalizedDistance));
            }
        }

        private void CopyVolumeCurveLut(int sourceIndex, int targetIndex, NativeArray<float> volumeCurveLutSamples)
        {
            if (!volumeCurveLutSamples.IsCreated || sourceIndex == targetIndex)
                return;

            int sampleCount = HazardZoneProfile.IntensityLutSampleCount;
            int sourceOffset = sourceIndex * sampleCount;
            int targetOffset = targetIndex * sampleCount;
            for (int i = 0; i < sampleCount; i++)
                volumeCurveLutSamples[targetOffset + i] = volumeCurveLutSamples[sourceOffset + i];
        }

        private void WriteDefaultCurveLut(int lutOffset, NativeArray<float> volumeCurveLutSamples)
        {
            if (!volumeCurveLutSamples.IsCreated)
                return;

            for (int i = 0; i < HazardZoneProfile.IntensityLutSampleCount; i++)
            {
                float normalizedDistance = HazardZoneProfile.IntensityLutSampleCount > 1
                    ? i / (float)(HazardZoneProfile.IntensityLutSampleCount - 1)
                    : 0f;
                volumeCurveLutSamples[lutOffset + i] = ResolveVolumeCurveSample(normalizedDistance);
            }
        }

        private static float SampleIntensityCurveByDistanceSq(
            int curveLutOffset,
            float normalizedDistanceSq,
            NativeArray<float>.ReadOnly curveLutSamples)
        {
            if (!curveLutSamples.IsCreated)
                return ResolveSquaredVolumeCurveSample(normalizedDistanceSq);

            float safeDistanceSq = FiniteSaturate01(normalizedDistanceSq, 0f);
            float scaledIndex = safeDistanceSq * (HazardZoneProfile.IntensityLutSampleCount - 1);
            int sampleIndex = (int)math.floor(scaledIndex);
            int nextIndex = math.min(HazardZoneProfile.IntensityLutSampleCount - 1, sampleIndex + 1);
            float fraction = scaledIndex - sampleIndex;
            int sampleOffset = curveLutOffset + sampleIndex;
            int nextOffset = curveLutOffset + nextIndex;
            if ((uint)sampleOffset >= (uint)curveLutSamples.Length ||
                (uint)nextOffset >= (uint)curveLutSamples.Length)
            {
                return ResolveSquaredVolumeCurveSample(normalizedDistanceSq);
            }

            float a = FiniteSaturate01(curveLutSamples[sampleOffset], 0f);
            float b = FiniteSaturate01(curveLutSamples[nextOffset], 0f);
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
            if (!_volumeIds.TryReadOnly(out NativeArray<int>.ReadOnly readVolumeIds))
                return -1;

            return FindZoneIndex(id, readVolumeIds);
        }

        private int FindZoneIndex(int id, NativeArray<int> volumeIds)
        {
            int count = math.min(_activeCount, volumeIds.Length);
            for (int i = 0; i < count; i++)
            {
                if (volumeIds[i] == id)
                    return i;
            }

            return -1;
        }

        private int FindZoneIndex(int id, NativeArray<int>.ReadOnly volumeIds)
        {
            int count = math.min(_activeCount, volumeIds.Length);
            for (int i = 0; i < count; i++)
            {
                if (volumeIds[i] == id)
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
            _toxicityDose = 0f;
            _toxicityPulseAccumulatorSeconds = 0f;
            _publishedExposureMask = 0;
            _activeTransportOwner = null;
            _activeTransportBehaviour = null;
            _activeTransportCollider = null;
            _playerCollider = null;
            _playerRuntimeContext = null;

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
            originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
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

        private float SumHazardIntensityLinear(
            double3 absolutePoint,
            in AbsoluteUniversePosition pointAup,
            HazardType type,
            NativeArray<HazardVolumeData>.ReadOnly readVolumes,
            NativeArray<int>.ReadOnly readVolumeIds,
            NativeArray<float>.ReadOnly readCurveLutSamples)
        {
            float totalIntensity = 0f;
            int readCount = math.min(_activeCount, math.min(readVolumes.Length, readVolumeIds.Length));
            for (int i = 0; i < readCount; i++)
            {
                HazardVolumeData volume = readVolumes[i];
                if (volume.Type != type)
                    continue;

                totalIntensity += EvaluatePointContribution(
                    volume,
                    absolutePoint,
                    IsPointEligibleForToxicMudVolume(i, in volume, in pointAup, readVolumeIds),
                    readCurveLutSamples);
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

        private void UpdateSpatialEntry(int index, int id, in HazardVolumeData data, NativeArray<int> volumeSpatialHandles)
        {
            if (_spatialHash == null || !volumeSpatialHandles.IsCreated || index < 0 || index >= volumeSpatialHandles.Length)
                return;

            int handle = volumeSpatialHandles[index];
            if (handle <= 0)
            {
                volumeSpatialHandles[index] = RegisterSpatialEntry(id, in data);
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
                volumeSpatialHandles[index] = 0;
            }
        }

        private void UnregisterSpatialEntry(int index, NativeArray<int> volumeSpatialHandles)
        {
            if (_spatialHash == null || !volumeSpatialHandles.IsCreated || index < 0 || index >= volumeSpatialHandles.Length)
                return;

            int handle = volumeSpatialHandles[index];
            if (handle <= 0)
                return;

            _spatialHash.Unregister(handle);
            volumeSpatialHandles[index] = 0;
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

            bool slowRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            bool lateRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            _registered = slowRegistered && lateRegistered;
            if (_registered)
                return;

            if (slowRegistered)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            if (lateRegistered)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
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
            Hecton8.Core.H8Debug.LogWarning(OverflowLogText);
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
