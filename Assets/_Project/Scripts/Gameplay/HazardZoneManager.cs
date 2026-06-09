using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Core.Memory.Layout;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [BinaryBlittableSafe]
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

    [BinaryBlittableSafe]
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

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct HazardZoneTelemetryEntry
    {
        [FieldOffset(0)] public ulong PackedOwner;
        [FieldOffset(8)] public uint FrameIndex;
        [FieldOffset(12)] public uint Sequence;
        [FieldOffset(16)] public uint StateHash;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public int ActiveZoneCount;
        [FieldOffset(28)] public int PendingMutationCount;
        [FieldOffset(32)] public int PublishedExposureMask;
        [FieldOffset(36)] public uint BufferGeneration;
        [FieldOffset(40)] public float ToxicityDose;
        [FieldOffset(44)] public float ToxicityPulseAccumulatorSeconds;
        [FieldOffset(48)] public float PlayerToxicity;
        [FieldOffset(52)] public float VehicleToxicity;
        [FieldOffset(56)] public float PlayerRadiation;
        [FieldOffset(60)] public float VehicleRadiation;
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
            int sampleOffset = curveLutOffset + sampleIndex;
            int nextOffset = curveLutOffset + nextIndex;
            if ((uint)sampleOffset >= (uint)curveLutSamples.Length ||
                (uint)nextOffset >= (uint)curveLutSamples.Length)
            {
                return ResolveSquaredDefaultCurveSample(normalizedDistanceSq);
            }

            float a = FiniteSaturate01(curveLutSamples[sampleOffset]);
            float b = FiniteSaturate01(curveLutSamples[nextOffset]);
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
    public sealed class HazardZoneManager : MonoBehaviour, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener, IHazardZoneReadModel, ISaveable
    {
        private static int s_x001HazardZoneManagerSignalPushDropCount;
        private const int HazardTypeCount = 4;
        private const int DefaultMaxZoneCount = 512;
        private const int MinZoneCapacity = 32;
        private const int PendingMutationCapacity = 64;
        private const int PendingUnregisterOverflowCapacity = 64;
        private const int TelemetryCapacity = 300;
        private const int TelemetryEntrySizeBytes = 64;
        private const float HazardStepIntervalSeconds = 0.1f;
        private const float MinHazardRadius = 0.01f;
        private const float MaxHazardRadius = 2500f;
        private const double HazardSpatialCellSizeMeters = 12d;
        private const int HazardSpatialQueryCapacity = 64;
        private const int HazardSpatialLayerMask = 1 << 30;
        private const int HazardTypeMaskAll = (1 << HazardTypeCount) - 1;
        private const int HazardTypeMaskRadiation = 1 << (int)HazardType.Radiation;
        private const int HazardTypeMaskNonRadiation = HazardTypeMaskAll & ~HazardTypeMaskRadiation;
        private const uint PendingMutationOverflowWarningHash = 0x485A4D51u; // HZMQ
        private const uint HazardManagerContextHash = 0x485A4D47u; // HZMG
        private const uint TelemetryDumpMagic = 0x4838485Au; // H8HZ
        private const int TelemetryDumpFormatVersion = 1;
        private const int TelemetryDumpHeaderBytes = 24;
        private const string TelemetryDumpPayloadLabel = "HazardZoneBlackBox";
        private const string TelemetryDumpRelativePathPrefix = "Docs/AgentLogs/Dump_HAZARD_ZONE_BLACKBOX_";
        private const string TelemetryDumpRelativePathSuffix = ".bin";
        private const uint TelemetryFlagJobRunning = 1u << 0;
        private const uint TelemetryFlagHazardStateGuardHeld = 1u << 1;
        private const uint TelemetryFlagExposureJobGuardHeld = 1u << 2;
        private const uint TelemetryFlagPendingDataVaultSwap = 1u << 3;
        private const uint TelemetryFlagPendingMutation = 1u << 4;
        private const uint TelemetryFlagPendingUnregisterOverflow = 1u << 5;
        private const uint TelemetryFlagNonFinite = 1u << 6;
        private const float ToxicityDoseThreshold = SaveData.HazardZoneToxicityDamageDoseThreshold;
        private const float ToxicityDoseDecayPerSecond = 0.18f;
        private const float ToxicityDamagePulseIntervalSeconds = SaveData.HazardZoneMaxPersistedToxicityPulseSeconds;
        private const float ToxicityDamagePerPulse = 1.1f;
        private const float ToxicityOverdoseDamageScale = 0.85f;
        private const float ToxicityPoisonStatusDurationSeconds = 5f;
        private const int MaxToxicityDamagePulsesPerTick = 4;
        private const float ToxicityExposureToxemiaScale = 0.08f;
        private const float MaxPersistedToxicityDose = SaveData.HazardZoneMaxPersistedToxicityDose;
        private const uint PlayerToxicityFallbackEntityHash = ToxicityExposureSignal.PlayerEntityFallbackHash;
        private const float RadiationClarityTransferScale = 0.85f;
        private const float ThermalClarityTransferDenominator = 18f;
        private const float ToxicClarityTransferScale = 1.35f;
        private const float HazardIntensityHardCap = 1000f;
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
        private VaultGenerationHandle<HazardZoneTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private IDataVault _dataVault;
        private HazardVaultArray<byte> _candidateVolumeFlags;
        private JobHandle _jobHandle;
        private HectonSpatialHash _spatialHash;
        private HazardVaultArray<int> _spatialQueryHandles;
        private bool _jobRunning;
        private bool _exposureJobGuardHeld;
        private bool _hazardStateGuardHeld;
        private bool _registered;
        private bool _serviceRegistered;
        private bool _saveRegistered;
        private bool _hotSwapRegistered;
        private bool _ownsJobResultHandle;
        private bool _ownsTelemetryRingHandle;
        private bool _ownsTelemetryCursorHandle;
        private bool _hazardBlackBoxDumped;
        private bool _hazardBlackBoxDumpAttempted;
        private bool _hazardBlackBoxUnavailableReported;
        private bool _pendingDataVaultSwap;
        private bool _lastExposureJobResultNonFinite;
        private int _activeCount;
        private int _telemetryWriteIndex;
        private uint _telemetrySequence;
        private float _toxicityDose;
        private float _toxicityPulseAccumulatorSeconds;
        private int _publishedExposureMask;

        private Transform _playerTransform;
        private IDataVault _pendingDataVault;
        private ISaveService _saveService;
        private ISaveService _registeredSaveService;
        private IDataVault _exposureJobGuardVault;
        private IDataVault _hazardStateGuardVault;
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
        // COLD ALLOC: int[64] - fail-closed unregister overflow lane for stale damaging hazard removal - owner: HazardZoneManager
        private readonly int[] _pendingOverflowUnregisterIds = new int[PendingUnregisterOverflowCapacity];
        private int _pendingMutationCount;
        private int _pendingOverflowUnregisterCount;

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
            if (environmentService == null)
                return null;

            environmentService.InitializeService();
            return environmentService.HazardZones;
        }

        /// <summary>
        /// Registers or updates a spherical hazard volume in runtime absolute-universe space.
        /// </summary>
        public bool RegisterZone(int id, Vector3 runtimePosition, float intensity, float radius, HazardType type, float visorGlitchBias = 1f)
        {
            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition positionAup))
            {
                UnregisterZone(id, type);
                return false;
            }

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
            {
                UnregisterZone(id, type);
                return false;
            }

            return RegisterZone(id, in positionAup, intensity, radius, type, visorGlitchBias, profile);
        }

        internal bool RegisterZone(int id, in AbsoluteUniversePosition positionAup, float intensity, float radius, HazardType type, float visorGlitchBias, HazardZoneProfile profile)
        {
            if (!IsValidHazardZoneInput(id, in positionAup, intensity, radius, type, visorGlitchBias))
            {
                UnregisterZone(id, type);
                return false;
            }

            if (type == HazardType.Radiation)
            {
                if (!Application.isPlaying || intensity <= 0f)
                {
                    RadiationHazardGrid.UnregisterSource(id);
                    return false;
                }

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
            if (id <= 0)
                return;

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
                if (id == 0)
                    return;

                RadiationHazardGrid.UnregisterSource(id);
                return;
            }

            if (id <= 0)
                return;

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
        /// Returns the bounded summed hazard intensity at the supplied runtime point.
        /// </summary>
        public float GetHazardIntensity(Vector3 runtimePoint, HazardType type)
        {
            if (!TryResolveAupFromRuntimeOrigin(runtimePoint, out AbsoluteUniversePosition pointAup))
                return 0f;

            return GetHazardIntensity(in pointAup, type);
        }

        /// <summary>
        /// Returns the bounded summed hazard intensity at the supplied absolute-universe point.
        /// </summary>
        public float GetHazardIntensity(in AbsoluteUniversePosition pointAup, HazardType type)
        {
            if (!IsFiniteAup(in pointAup))
                return 0f;

            if (type == HazardType.Radiation)
                return RadiationHazardGrid.TrySampleRadiationIntensity01(in pointAup, out float radiation01) ? ClampExposure(radiation01) : 0f;

            if (!_volumes.IsCreated || _activeCount <= 0)
                return 0f;

            if (!_volumes.TryReadOnly(out NativeArray<HazardVolumeData>.ReadOnly readVolumes) ||
                !_volumeIds.TryReadOnly(out NativeArray<int>.ReadOnly readVolumeIds))
            {
                return 0f;
            }

            _volumeCurveLutSamples.TryReadOnly(out NativeArray<float>.ReadOnly readCurveLutSamples);
            double3 absolutePoint = pointAup.ToAbsoluteDouble3();
            return ClampExposure(SumHazardIntensityLinear(
                absolutePoint,
                in pointAup,
                type,
                readVolumes,
                readVolumeIds,
                readCurveLutSamples));
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
        public float ToxicityDose => ClampPersistedToxicityDose(_toxicityDose);

        public int SavePriority => 55;
        public int LoadPriority => 55;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticRuntimeState()
        {
            Volatile.Write(ref s_x001HazardZoneManagerSignalPushDropCount, 0);
        }

        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            ref HazardZoneRuntimeDTO dto = ref data.hazardZones;
            dto.toxicityDose = ClampPersistedToxicityDose(_toxicityDose);
            dto.toxicityPulseAccumulatorSeconds = dto.toxicityDose > ToxicityDoseThreshold
                ? ClampPersistedToxicityPulseAccumulator(_toxicityPulseAccumulatorSeconds)
                : 0f;
        }

        public void LoadFromSaveData(SaveData data)
        {
            if (data == null)
            {
                _toxicityDose = 0f;
                _toxicityPulseAccumulatorSeconds = 0f;
                UpdateDiagnostics();
                return;
            }

            HazardZoneRuntimeDTO dto = data.hazardZones;
            if (data.version < SaveData.HazardZoneRuntimePersistenceVersion)
            {
                _toxicityDose = 0f;
                _toxicityPulseAccumulatorSeconds = 0f;
            }
            else
            {
                _toxicityDose = ClampPersistedToxicityDose(dto.toxicityDose);
                _toxicityPulseAccumulatorSeconds = _toxicityDose > ToxicityDoseThreshold
                    ? ClampPersistedToxicityPulseAccumulator(dto.toxicityPulseAccumulatorSeconds)
                    : 0f;
            }

            UpdateDiagnostics();
        }

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
            TryRegisterSaveParticipant();
            UpdateDiagnostics();
        }

        private void OnDisable()
        {
            PublishExposureMask(0);
            TryUnregisterSaveParticipant();
            TryUnregister();
            TryUnregisterService();
            TryUnregisterHotSwapListener();
            ClearRuntimeState();
            DisposeNativeState();
        }

        private void OnDestroy()
        {
            PublishExposureMask(0);
            TryUnregisterSaveParticipant();
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
                IPlayerRuntimeContext nextPlayerContext = currentService as IPlayerRuntimeContext;
                if (!IsPlayerRuntimeContextBound(nextPlayerContext))
                {
                    ClearPlayerRuntimeBindings();
                    UpdateDiagnostics();
                    return;
                }

                if (!ReferenceEquals(_playerRuntimeContext, nextPlayerContext))
                    ClearPlayerRuntimeBindings();

                _playerRuntimeContext = nextPlayerContext;
                ApplyPlayerContextReferences(
                    nextPlayerContext.PlayerTransform,
                    nextPlayerContext.PlayerCollider,
                    nextPlayerContext.PlayerHealth,
                    nextPlayerContext.SurvivalSystem,
                    nextPlayerContext.TraumaDispatcher,
                    nextPlayerContext.PlayerTransportCoordinator);
                RefreshActiveTransportOwner();
                UpdateDiagnostics();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Save)
            {
                TryUnregisterSaveParticipant();
                TryRegisterSaveParticipant();
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
            ClearExposureState();
            ClearPendingMutations();
            ReleaseHazardExposureResultBuffer();
            ReleaseHazardVaultBuffers();
            ReleaseHazardSpatialHash();
            CacheHazardVaultCold(nextVault);
            if (!_jobRunning)
                AllocateNativeState();
            UpdateDiagnostics();
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
            RecordHazardBlackBoxTelemetry();
        }

        private void AllocateNativeState()
        {
            if (_volumes.IsCreated)
                return;

            if (!AreHazardRuntimeLayoutsValid())
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

            _ = TryEnsureHazardTelemetryBuffers();
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
            ReleaseHazardSpatialHash();
        }

        private void ReleaseHazardSpatialHash()
        {
            _spatialHash?.Dispose();
            _spatialHash = null;
        }

        private void ResolvePlayerContext()
        {
            IPlayerRuntimeContext activeRuntimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;
            bool hasActiveRuntimeContext = activeRuntimeContext != null;
            if (IsPlayerRuntimeContextBound(activeRuntimeContext))
            {
                _playerRuntimeContext = activeRuntimeContext;
                ApplyPlayerContextReferences(
                    activeRuntimeContext.PlayerTransform,
                    activeRuntimeContext.PlayerCollider,
                    activeRuntimeContext.PlayerHealth,
                    activeRuntimeContext.SurvivalSystem,
                    activeRuntimeContext.TraumaDispatcher,
                    activeRuntimeContext.PlayerTransportCoordinator);
            }
            else if (hasActiveRuntimeContext)
            {
                ClearPlayerRuntimeBindings();
                RefreshActiveTransportOwner();
                return;
            }
            else
            {
                IPlayerRuntimeContext playerContext = _playerRuntimeContext;
                if (IsPlayerRuntimeContextBound(playerContext))
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

            RefreshActiveTransportOwner();
        }

        private void RefreshPlayerContextSnapshot()
        {
            IPlayerRuntimeContext activeRuntimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;
            bool hasActiveRuntimeContext = activeRuntimeContext != null;
            if (IsPlayerRuntimeContextBound(activeRuntimeContext))
            {
                _playerRuntimeContext = activeRuntimeContext;
                ApplyPlayerContextReferences(
                    activeRuntimeContext.PlayerTransform,
                    activeRuntimeContext.PlayerCollider,
                    activeRuntimeContext.PlayerHealth,
                    activeRuntimeContext.SurvivalSystem,
                    activeRuntimeContext.TraumaDispatcher,
                    activeRuntimeContext.PlayerTransportCoordinator);
            }
            else if (hasActiveRuntimeContext)
            {
                ClearPlayerRuntimeBindings();
                RefreshActiveTransportOwner();
                return;
            }
            else if (_playerRuntimeContext != null && !IsPlayerRuntimeContextBound(_playerRuntimeContext))
            {
                ClearPlayerRuntimeBindings();
                return;
            }

            RefreshActiveTransportOwner();
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
                _activeTransportOwner = null;
                _activeTransportBehaviour = null;
                _activeTransportCollider = null;
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

        private static bool IsPlayerRuntimeContextBound(IPlayerRuntimeContext playerContext)
        {
            return playerContext != null &&
                   playerContext.IsInitialized &&
                   playerContext.PlayerTransform != null;
        }

        private void ClearPlayerRuntimeBindings()
        {
            ClearExposureState();
            _playerRuntimeContext = null;
            _playerTransform = null;
            _playerCollider = null;
            _playerSurvival = null;
            _playerHealth = null;
            _playerTraumaDispatcher = null;
            _playerTransportCoordinator = null;
            _activeTransportOwner = null;
            _activeTransportBehaviour = null;
            _activeTransportCollider = null;
        }

        private void RefreshActiveTransportOwner()
        {
            if (_playerTransform == null)
            {
                _activeTransportOwner = null;
                _activeTransportBehaviour = null;
                _activeTransportCollider = null;
                return;
            }

            IPlayerTransportLifecycleOwner resolvedOwner = null;
            if (_playerTransportCoordinator != null)
                _playerTransportCoordinator.TryResolveTransportLifecycleOwner(out resolvedOwner);

            if (ReferenceEquals(_activeTransportOwner, resolvedOwner))
                return;

            _activeTransportOwner = resolvedOwner;
            _activeTransportBehaviour = resolvedOwner as MonoBehaviour;
            _activeTransportCollider = ResolveTransportColliderCold(_activeTransportBehaviour);
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
                    _lastExposureJobResultNonFinite = false;
                    return true;
                }

                HazardExposureJobResult result = jobResult[0];
                _lastExposureJobResultNonFinite = HasNonFiniteExposureJobResult(in result);
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
            float safeDose = math.min(FiniteNonNegativeOrZero(_toxicityDose), MaxPersistedToxicityDose);

            if (currentToxicityIntensity > 0.001f)
            {
                float resistance = ResolveToxicityResistance();
                _toxicityDose = math.min(safeDose + (currentToxicityIntensity / resistance) * safeDt, MaxPersistedToxicityDose);
            }
            else
            {
                _toxicityDose = math.max(0f, safeDose - ToxicityDoseDecayPerSecond * safeDt);
                if (_toxicityDose <= ToxicityDoseThreshold)
                    _toxicityPulseAccumulatorSeconds = 0f;
            }

            if (_toxicityDose <= ToxicityDoseThreshold)
            {
                _toxicityPulseAccumulatorSeconds = 0f;
                return;
            }

            if (_playerSurvival == null)
            {
                _toxicityPulseAccumulatorSeconds = ClampPersistedToxicityPulseAccumulator(_toxicityPulseAccumulatorSeconds);
                return;
            }

            float maxPulseAccumulatorSeconds = ToxicityDamagePulseIntervalSeconds * (MaxToxicityDamagePulsesPerTick + 1);
            _toxicityPulseAccumulatorSeconds = math.min(
                FiniteNonNegativeOrZero(_toxicityPulseAccumulatorSeconds) + safeDt,
                maxPulseAccumulatorSeconds);

            int pulseCount = math.min(
                MaxToxicityDamagePulsesPerTick,
                (int)math.floor(_toxicityPulseAccumulatorSeconds / ToxicityDamagePulseIntervalSeconds));
            _toxicityPulseAccumulatorSeconds = math.min(
                ToxicityDamagePulseIntervalSeconds,
                _toxicityPulseAccumulatorSeconds - pulseCount * ToxicityDamagePulseIntervalSeconds);

            for (int pulseIndex = 0; pulseIndex < pulseCount; pulseIndex++)
            {
                ApplyToxicityDamagePulse(currentToxicityIntensity);
            }
        }

        private void ApplyToxicityDamagePulse(float currentIntensity)
        {
            float safeCurrentIntensity = ClampExposure(currentIntensity);
            float safeDose = math.min(FiniteNonNegativeOrZero(_toxicityDose), MaxPersistedToxicityDose);
            float overdose = math.max(0f, safeDose - ToxicityDoseThreshold);
            float damageMagnitude = ToxicityDamagePerPulse *
                                    math.max(0.25f, safeCurrentIntensity) *
                                    (1f + overdose * ToxicityOverdoseDamageScale);

            int targetId = ResolvePlayerCombatTargetId();
            PublishToxicityExposureSignal(damageMagnitude, safeCurrentIntensity);
            _ = TryQueueToxicityPoisonStatus(targetId, damageMagnitude, safeCurrentIntensity);
        }

        private int ResolvePlayerCombatTargetId()
        {
            HectonPlayerHealth playerHealth = _playerHealth;
            return playerHealth != null
                ? CombatDamageRuntime.ResolveTargetId(playerHealth.gameObject)
                : 0;
        }

        private uint ResolvePlayerToxicitySignalEntityId()
        {
            GameObject playerObject = null;
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null)
                playerObject = playerContext.PlayerObject;
            if (playerObject == null && _playerTransform != null)
                playerObject = _playerTransform.gameObject;
            if (playerObject == null)
                playerObject = BootstrapState.CurrentPlayerObject;

            uint entityHash = playerObject != null ? unchecked((uint)EntityId.ToULong(playerObject.GetEntityId())) : 0u;
            return entityHash != 0u ? entityHash : PlayerToxicityFallbackEntityHash;
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

        private void PublishToxicityExposureSignal(float damageMagnitude, float currentIntensity)
        {
            uint signalEntityId = ResolvePlayerToxicitySignalEntityId();
            float exposure01 = FiniteSaturate01(currentIntensity, 0f);
            float safeDamageMagnitude = FiniteNonNegativeOrZero(damageMagnitude);
            float toxemiaDelta = math.saturate(exposure01 * math.max(0.1f, safeDamageMagnitude) * ToxicityExposureToxemiaScale);
            if (exposure01 <= 0.0001f && toxemiaDelta <= 0f)
                return;

            bool hasSourceAup = TryResolvePlayerPredictedAup(out AbsoluteUniversePosition playerAup) ||
                (_playerTransform != null && TryResolveAupFromRuntimeOrigin(_playerTransform.position, out playerAup));

            ToxicityExposureSignal signal = default;
            signal.Exposure01 = exposure01;
            signal.ToxemiaDelta = toxemiaDelta;
            signal.EntityId = signalEntityId;
            signal.ChemicalHash = ToxicityHazardChemicalHash;
            signal.Frame = TimeSliceScheduler.CurrentFrameId;
            if (hasSourceAup)
            {
                signal.AUP = playerAup.ToAbsoluteDouble3();
                signal.Flags = ToxicityExposureSignal.FlagHasSourceAup;
            }

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
                return;
            }

            bool keepJobGuard = false;
            int candidateCount;
            try
            {
                if (!_jobVolumes.TryResolveMutable(out NativeArray<HazardVolumeData> lockedJobVolumes))
                {
                    return;
                }

                if (!_volumes.TryReadOnly(out NativeArray<HazardVolumeData>.ReadOnly readVolumes) ||
                    !_volumeIds.TryReadOnly(out NativeArray<int>.ReadOnly readVolumeIds))
                {
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
                H8Memory.RegisterActiveJob(SystemID.GameplayHazards, _jobHandle);
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
            if (_hazardStateGuardHeld ||
                vault == null ||
                !vault.TryAcquireMutationGuard(HazardStateMutationGuardMask))
            {
                return false;
            }

            bool keepGuard = false;
            try
            {
                if (!_volumes.TryResolveMutable(out volumes) ||
                    !_volumeIds.TryResolveMutable(out volumeIds) ||
                    !_volumeSpatialHandles.TryResolveMutable(out volumeSpatialHandles) ||
                    !_volumeCurveLutSamples.TryResolveMutable(out volumeCurveLutSamples))
                {
                    volumes = default;
                    volumeIds = default;
                    volumeSpatialHandles = default;
                    volumeCurveLutSamples = default;
                    return false;
                }

                if (ResolveActiveVolumeCapacity(volumes, volumeIds, volumeSpatialHandles, volumeCurveLutSamples) <= 0)
                {
                    volumes = default;
                    volumeIds = default;
                    volumeSpatialHandles = default;
                    volumeCurveLutSamples = default;
                    return false;
                }

                _hazardStateGuardVault = vault;
                _hazardStateGuardHeld = true;
                keepGuard = true;
                return true;
            }
            finally
            {
                if (!keepGuard)
                    vault.ReleaseMutationGuard(HazardStateMutationGuardMask);
            }
        }

        private void ReleaseHazardStateWriteViews()
        {
            if (!_hazardStateGuardHeld)
                return;

            IDataVault vault = _hazardStateGuardVault;
            _hazardStateGuardVault = null;
            _hazardStateGuardHeld = false;
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
            ReleaseHazardStateWriteViews();
            ReleaseExposureJobLocks();
            _volumes.ReleaseBuffer();
            _volumeIds.ReleaseBuffer();
            _volumeSpatialHandles.ReleaseBuffer();
            _volumeCurveLutSamples.ReleaseBuffer();
            _jobVolumes.ReleaseBuffer();
            _candidateVolumeFlags.ReleaseBuffer();
            _spatialQueryHandles.ReleaseBuffer();
            ReleaseHazardTelemetryBuffers();
            _activeCount = 0;
        }

        private void ReleaseExposureJobLocks()
        {
            if (!_exposureJobGuardHeld)
                return;

            IDataVault vault = _exposureJobGuardVault;
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

        private bool TryEnsureHazardTelemetryBuffers()
        {
            bool ringReady = TryEnsureHazardTelemetryRing();
            bool cursorReady = TryEnsureHazardTelemetryCursor();
            bool ready = ringReady && cursorReady;
            if (ready)
                RestoreHazardTelemetryRuntimeStateFromVault();

            return ready;
        }

        private bool TryEnsureHazardTelemetryRing()
        {
            return TryEnsureHazardTelemetryBuffer(
                ref _telemetryRingHandle,
                BufferID.HazardZoneTelemetryRing,
                TelemetryCapacity,
                NativeArrayOptions.ClearMemory,
                ref _ownsTelemetryRingHandle);
        }

        private bool TryEnsureHazardTelemetryCursor()
        {
            return TryEnsureHazardTelemetryBuffer(
                ref _telemetryCursorHandle,
                BufferID.HazardZoneTelemetryCursor,
                1,
                NativeArrayOptions.ClearMemory,
                ref _ownsTelemetryCursorHandle);
        }

        private bool TryEnsureHazardTelemetryBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredCapacity,
            NativeArrayOptions options,
            ref bool ownsHandle)
            where T : struct
        {
            int safeCapacity = math.max(1, requiredCapacity);
            IDataVault vault = _dataVault;
            uint expectedBufferId = unchecked((uint)(int)bufferId);
            uint expectedSystemId = unchecked((uint)SystemID.GameplayHazards);
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (IsVaultHandleCreated(in handle) &&
                handle.BufferID == expectedBufferId &&
                handle.SystemID == expectedSystemId &&
                vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly existing) &&
                existing.IsCreated &&
                existing.Length >= safeCapacity)
            {
                return true;
            }

            handle = default;
            ownsHandle = false;
            if (vault.IsAllocationLocked)
                return false;

            VaultGenerationHandle<T> acquired = vault.EnsureGenerationHandle<T>(
                bufferId,
                safeCapacity,
                SystemID.GameplayHazards,
                options);
            if (!IsVaultHandleCreated(in acquired) ||
                acquired.BufferID != expectedBufferId ||
                acquired.SystemID != expectedSystemId ||
                !vault.TryReadOnlyHandle(in acquired, out NativeArray<T>.ReadOnly buffer) ||
                !buffer.IsCreated ||
                buffer.Length < safeCapacity)
            {
                return false;
            }

            handle = acquired;
            ownsHandle = true;
            return true;
        }

        private void RestoreHazardTelemetryRuntimeStateFromVault()
        {
            if (!IsHazardTelemetryRingReady() || !IsHazardTelemetryCursorReady())
                return;

            IDataVault vault = _dataVault;
            if (vault == null ||
                !vault.TryReadOnlyHandle(in _telemetryRingHandle, out NativeArray<HazardZoneTelemetryEntry>.ReadOnly telemetryRing) ||
                !vault.TryReadOnlyHandle(in _telemetryCursorHandle, out NativeArray<int>.ReadOnly cursorBuffer) ||
                !telemetryRing.IsCreated ||
                !cursorBuffer.IsCreated ||
                telemetryRing.Length < TelemetryCapacity ||
                cursorBuffer.Length <= 0)
            {
                return;
            }

            int telemetryLength = math.min(telemetryRing.Length, TelemetryCapacity);
            int restoredWriteIndex = NormalizeHazardTelemetryCursor(cursorBuffer[0], telemetryLength);
            uint restoredSequence = RestoreHazardTelemetrySequence(telemetryRing, telemetryLength, restoredWriteIndex);
            if (restoredSequence == 0u)
                restoredWriteIndex = 0;

            _telemetryWriteIndex = restoredWriteIndex;
            _telemetrySequence = restoredSequence;
        }

        private static uint RestoreHazardTelemetrySequence(
            NativeArray<HazardZoneTelemetryEntry>.ReadOnly telemetryRing,
            int telemetryLength,
            int nextWriteIndex)
        {
            if (!telemetryRing.IsCreated || telemetryLength <= 0)
                return 0u;

            int newestIndex = nextWriteIndex > 0 ? nextWriteIndex - 1 : telemetryLength - 1;
            if ((uint)newestIndex < (uint)telemetryLength)
            {
                uint newestSequence = telemetryRing[newestIndex].Sequence;
                if (newestSequence != 0u)
                    return newestSequence;
            }

            uint restoredSequence = 0u;
            int safeLength = math.min(telemetryRing.Length, telemetryLength);
            for (int i = 0; i < safeLength; i++)
            {
                uint sequence = telemetryRing[i].Sequence;
                if (sequence > restoredSequence)
                    restoredSequence = sequence;
            }

            return restoredSequence;
        }

        private void ReleaseHazardTelemetryBuffers()
        {
            ReleaseHazardTelemetryBuffer(
                ref _telemetryRingHandle,
                BufferID.HazardZoneTelemetryRing,
                ref _ownsTelemetryRingHandle);
            ReleaseHazardTelemetryBuffer(
                ref _telemetryCursorHandle,
                BufferID.HazardZoneTelemetryCursor,
                ref _ownsTelemetryCursorHandle);
            _telemetryWriteIndex = 0;
            _telemetrySequence = 0u;
            _hazardBlackBoxDumped = false;
            _hazardBlackBoxDumpAttempted = false;
            _hazardBlackBoxUnavailableReported = false;
        }

        private void ReleaseHazardTelemetryBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            ref bool ownsHandle)
            where T : struct
        {
            IDataVault vault = _dataVault;
            if (ownsHandle &&
                vault != null &&
                IsVaultHandleCreated(in handle) &&
                vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> current) &&
                current.Generation == handle.Generation &&
                current.SystemID == handle.SystemID)
            {
                vault.ReleaseBuffer(in handle);
            }

            handle = default;
            ownsHandle = false;
        }

        private void RecordHazardBlackBoxTelemetry()
        {
            HazardZoneTelemetryEntry entry = BuildHazardTelemetryEntry();
            bool hasFault = (entry.Flags & TelemetryFlagNonFinite) != 0u;
            _ = TryWriteHazardTelemetryEntry(ref entry);

            if (hasFault)
                DumpHazardBlackBoxOnce();
        }

        private HazardZoneTelemetryEntry BuildHazardTelemetryEntry()
        {
            uint flags = ComposeHazardTelemetryFlags();
            float toxicityDose = FiniteTelemetryValue(_toxicityDose, ref flags);
            float toxicityPulse = FiniteTelemetryValue(_toxicityPulseAccumulatorSeconds, ref flags);
            float playerToxicity = FiniteTelemetryValue(_playerHazardIntensity[(int)HazardType.Toxicity], ref flags);
            float vehicleToxicity = FiniteTelemetryValue(_vehicleHazardIntensity[(int)HazardType.Toxicity], ref flags);
            float playerRadiation = FiniteTelemetryValue(_playerHazardIntensity[(int)HazardType.Radiation], ref flags);
            float vehicleRadiation = FiniteTelemetryValue(_vehicleHazardIntensity[(int)HazardType.Radiation], ref flags);
            uint nextSequence = unchecked(_telemetrySequence + 1u);
            if (nextSequence == 0u)
                nextSequence = 1u;

            _telemetrySequence = nextSequence;
            HazardZoneTelemetryEntry entry = default;
            entry.FrameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            entry.Sequence = nextSequence;
            entry.Flags = flags;
            entry.ActiveZoneCount = _activeCount;
            entry.PendingMutationCount = _pendingMutationCount + _pendingOverflowUnregisterCount;
            entry.PublishedExposureMask = _publishedExposureMask;
            entry.ToxicityDose = toxicityDose;
            entry.ToxicityPulseAccumulatorSeconds = toxicityPulse;
            entry.PlayerToxicity = playerToxicity;
            entry.VehicleToxicity = vehicleToxicity;
            entry.PlayerRadiation = playerRadiation;
            entry.VehicleRadiation = vehicleRadiation;
            entry.StateHash = ComputeHazardTelemetryStateHash(in entry);
            return entry;
        }

        private uint ComposeHazardTelemetryFlags()
        {
            uint flags = 0u;
            if (_jobRunning)
                flags |= TelemetryFlagJobRunning;
            if (_hazardStateGuardHeld)
                flags |= TelemetryFlagHazardStateGuardHeld;
            if (_exposureJobGuardHeld)
                flags |= TelemetryFlagExposureJobGuardHeld;
            if (_pendingDataVaultSwap)
                flags |= TelemetryFlagPendingDataVaultSwap;
            if (_pendingMutationCount > 0)
                flags |= TelemetryFlagPendingMutation;
            if (_pendingOverflowUnregisterCount > 0)
                flags |= TelemetryFlagPendingUnregisterOverflow;
            if (_lastExposureJobResultNonFinite)
                flags |= TelemetryFlagNonFinite;

            return flags;
        }

        private bool TryWriteHazardTelemetryEntry(ref HazardZoneTelemetryEntry entry)
        {
            if (!IsHazardTelemetryRingReady())
                return false;

            IDataVault vault = _dataVault;
            if (vault == null ||
                !vault.TryAcquireWriteLock(in _telemetryRingHandle, SystemID.GameplayHazards, out NativeArray<HazardZoneTelemetryEntry> telemetryRing))
            {
                return false;
            }

            int nextWriteIndex = _telemetryWriteIndex;
            int telemetryLengthForCursor = TelemetryCapacity;
            bool wrote = false;
            try
            {
                if (!telemetryRing.IsCreated || telemetryRing.Length < TelemetryCapacity)
                    return false;

                int telemetryLength = math.min(telemetryRing.Length, TelemetryCapacity);
                telemetryLengthForCursor = telemetryLength;
                int writeIndex = NormalizeHazardTelemetryCursor(_telemetryWriteIndex, telemetryLength);

                nextWriteIndex = NormalizeHazardTelemetryCursor(writeIndex + 1, telemetryLength);

                entry.PackedOwner = ((ulong)_telemetryRingHandle.BufferID << 32) | _telemetryRingHandle.SystemID;
                entry.BufferGeneration = _telemetryRingHandle.Generation;
                telemetryRing[writeIndex] = entry;
                wrote = true;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _telemetryRingHandle, SystemID.GameplayHazards);
                if (wrote)
                {
                    _telemetryWriteIndex = nextWriteIndex;
                    _ = TryWriteHazardTelemetryCursor(nextWriteIndex, telemetryLengthForCursor);
                }
            }
        }

        private bool TryWriteHazardTelemetryCursor(int nextWriteIndex, int telemetryLength)
        {
            if (!IsHazardTelemetryCursorReady())
                return false;

            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsVaultHandleCreated(in _telemetryCursorHandle) ||
                !vault.TryAcquireWriteLock(in _telemetryCursorHandle, SystemID.GameplayHazards, out NativeArray<int> cursorBuffer))
            {
                return false;
            }

            try
            {
                if (!cursorBuffer.IsCreated || cursorBuffer.Length <= 0)
                    return false;

                cursorBuffer[0] = NormalizeHazardTelemetryCursor(nextWriteIndex, telemetryLength);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _telemetryCursorHandle, SystemID.GameplayHazards);
            }
        }

        private static int NormalizeHazardTelemetryCursor(int cursor, int telemetryLength)
        {
            return telemetryLength > 0 && (uint)cursor < (uint)telemetryLength
                ? cursor
                : 0;
        }

        private bool TryReadHazardTelemetryRing(out NativeArray<HazardZoneTelemetryEntry>.ReadOnly telemetryRing)
        {
            telemetryRing = default;
            IDataVault vault = _dataVault;
            return IsHazardTelemetryRingReady(vault) &&
                   vault.TryReadOnlyHandle(in _telemetryRingHandle, out telemetryRing) &&
                   telemetryRing.IsCreated &&
                   telemetryRing.Length >= TelemetryCapacity;
        }

        private bool IsHazardTelemetryRingReady()
        {
            return IsHazardTelemetryRingReady(_dataVault);
        }

        private bool IsHazardTelemetryRingReady(IDataVault vault)
        {
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   IsVaultHandleCreated(in _telemetryRingHandle) &&
                   _telemetryRingHandle.BufferID == unchecked((uint)(int)BufferID.HazardZoneTelemetryRing) &&
                   _telemetryRingHandle.SystemID == unchecked((uint)SystemID.GameplayHazards);
        }

        private bool IsHazardTelemetryCursorReady()
        {
            IDataVault vault = _dataVault;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   IsVaultHandleCreated(in _telemetryCursorHandle) &&
                   _telemetryCursorHandle.BufferID == unchecked((uint)(int)BufferID.HazardZoneTelemetryCursor) &&
                   _telemetryCursorHandle.SystemID == unchecked((uint)SystemID.GameplayHazards);
        }

        private void DumpHazardBlackBoxOnce()
        {
            if (_hazardBlackBoxDumped || _hazardBlackBoxDumpAttempted)
                return;

            if (!TryReadHazardTelemetryRing(out NativeArray<HazardZoneTelemetryEntry>.ReadOnly telemetryRing))
            {
                if (!_hazardBlackBoxUnavailableReported)
                {
                    _hazardBlackBoxUnavailableReported = true;
                    GlobalTelemetryBus.PublishUnityLogFault(TelemetryDumpMagic, 0u, 2u);
                }

                return;
            }

            _hazardBlackBoxDumpAttempted = true;
            NativeArray<byte> payload = default;
            int payloadBytes = 0;
            try
            {
                int entryCount = math.min(telemetryRing.Length, TelemetryCapacity);
                payloadBytes = TelemetryDumpHeaderBytes + entryCount * TelemetryEntrySizeBytes;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    payloadBytes,
                    nameof(HazardZoneManager),
                    TelemetryDumpPayloadLabel);

                int cursor = 0;
                if (!TryWriteHazardTelemetryDumpHeader(
                        payload,
                        ref cursor,
                        entryCount,
                        NormalizeHazardTelemetryCursor(_telemetryWriteIndex, entryCount),
                        _telemetrySequence))
                {
                    GlobalTelemetryBus.PublishUnityLogFault(TelemetryDumpMagic, 0u, 1u);
                    return;
                }

                for (int i = 0; i < entryCount; i++)
                {
                    HazardZoneTelemetryEntry rawEntry = telemetryRing[i];
                    HazardZoneTelemetryEntry entry = SanitizeHazardTelemetryDumpEntry(in rawEntry);
                    if (!TryWriteHazardTelemetryDumpEntry(payload, ref cursor, in entry))
                    {
                        GlobalTelemetryBus.PublishUnityLogFault(TelemetryDumpMagic, 0u, 1u);
                        return;
                    }
                }

                if (cursor != payloadBytes)
                {
                    GlobalTelemetryBus.PublishUnityLogFault(TelemetryDumpMagic, 0u, 1u);
                    return;
                }

                _hazardBlackBoxDumped = NativeFaultDumpWriter.TryWriteAll(BuildHazardTelemetryDumpRelativePath(), payload, cursor);
                if (!_hazardBlackBoxDumped)
                    GlobalTelemetryBus.PublishUnityLogFault(TelemetryDumpMagic, 0u, 1u);
            }
            catch (System.IO.IOException)
            {
                GlobalTelemetryBus.PublishUnityLogFault(TelemetryDumpMagic, 0u, 1u);
            }
            catch (System.UnauthorizedAccessException)
            {
                GlobalTelemetryBus.PublishUnityLogFault(TelemetryDumpMagic, 0u, 1u);
            }
            catch (System.ArgumentException)
            {
                GlobalTelemetryBus.PublishUnityLogFault(TelemetryDumpMagic, 0u, 1u);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(HazardZoneManager),
                    TelemetryDumpPayloadLabel);
            }
        }

        private static float FiniteTelemetryValue(float value, ref uint flags)
        {
            if (math.isfinite(value))
                return value;

            flags |= TelemetryFlagNonFinite;
            return 0f;
        }

        private static HazardZoneTelemetryEntry SanitizeHazardTelemetryDumpEntry(in HazardZoneTelemetryEntry entry)
        {
            if (!HasNonFiniteHazardTelemetryEntry(in entry))
                return entry;

            HazardZoneTelemetryEntry sanitized = entry;
            uint flags = sanitized.Flags;
            sanitized.ToxicityDose = FiniteTelemetryValue(sanitized.ToxicityDose, ref flags);
            sanitized.ToxicityPulseAccumulatorSeconds = FiniteTelemetryValue(sanitized.ToxicityPulseAccumulatorSeconds, ref flags);
            sanitized.PlayerToxicity = FiniteTelemetryValue(sanitized.PlayerToxicity, ref flags);
            sanitized.VehicleToxicity = FiniteTelemetryValue(sanitized.VehicleToxicity, ref flags);
            sanitized.PlayerRadiation = FiniteTelemetryValue(sanitized.PlayerRadiation, ref flags);
            sanitized.VehicleRadiation = FiniteTelemetryValue(sanitized.VehicleRadiation, ref flags);
            sanitized.Flags = flags;
            sanitized.StateHash = ComputeHazardTelemetryStateHash(in sanitized);
            return sanitized;
        }

        private static bool HasNonFiniteHazardTelemetryEntry(in HazardZoneTelemetryEntry entry)
        {
            return !math.isfinite(entry.ToxicityDose) ||
                   !math.isfinite(entry.ToxicityPulseAccumulatorSeconds) ||
                   !math.isfinite(entry.PlayerToxicity) ||
                   !math.isfinite(entry.VehicleToxicity) ||
                   !math.isfinite(entry.PlayerRadiation) ||
                   !math.isfinite(entry.VehicleRadiation);
        }

        private static bool HasNonFiniteExposureJobResult(in HazardExposureJobResult result)
        {
            return !math.isfinite(result.PlayerRadiation) ||
                   !math.isfinite(result.PlayerHeat) ||
                   !math.isfinite(result.PlayerToxicity) ||
                   !math.isfinite(result.PlayerBiohazard) ||
                   !math.isfinite(result.PlayerRadiationGlitchBias) ||
                   !math.isfinite(result.PlayerHeatGlitchBias) ||
                   !math.isfinite(result.PlayerToxicityGlitchBias) ||
                   !math.isfinite(result.PlayerBiohazardGlitchBias) ||
                   !math.isfinite(result.VehicleRadiation) ||
                   !math.isfinite(result.VehicleHeat) ||
                   !math.isfinite(result.VehicleToxicity) ||
                   !math.isfinite(result.VehicleBiohazard) ||
                   !math.isfinite(result.VehicleRadiationGlitchBias) ||
                   !math.isfinite(result.VehicleHeatGlitchBias) ||
                   !math.isfinite(result.VehicleToxicityGlitchBias) ||
                   !math.isfinite(result.VehicleBiohazardGlitchBias);
        }

        private static string BuildHazardTelemetryDumpRelativePath()
        {
            return TelemetryDumpRelativePathPrefix +
                   System.DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", System.Globalization.CultureInfo.InvariantCulture) +
                   TelemetryDumpRelativePathSuffix;
        }

        private static uint ComputeHazardTelemetryStateHash(in HazardZoneTelemetryEntry entry)
        {
            uint hash = 2166136261u;
            hash = MixTelemetryHash(hash, entry.Sequence);
            hash = MixTelemetryHash(hash, entry.Flags);
            hash = MixTelemetryHash(hash, unchecked((uint)entry.ActiveZoneCount));
            hash = MixTelemetryHash(hash, unchecked((uint)entry.PendingMutationCount));
            hash = MixTelemetryHash(hash, unchecked((uint)entry.PublishedExposureMask));
            hash = MixTelemetryHash(hash, math.asuint(entry.ToxicityDose));
            hash = MixTelemetryHash(hash, math.asuint(entry.ToxicityPulseAccumulatorSeconds));
            hash = MixTelemetryHash(hash, math.asuint(entry.PlayerToxicity));
            hash = MixTelemetryHash(hash, math.asuint(entry.VehicleToxicity));
            hash = MixTelemetryHash(hash, math.asuint(entry.PlayerRadiation));
            hash = MixTelemetryHash(hash, math.asuint(entry.VehicleRadiation));
            return hash;
        }

        private static uint MixTelemetryHash(uint hash, uint value)
        {
            return unchecked((hash ^ value) * 16777619u);
        }

        private static bool TryWriteHazardTelemetryDumpHeader(
            NativeArray<byte> target,
            ref int cursor,
            int entryCount,
            int writeIndex,
            uint sequence)
        {
            return TryWriteUInt32LittleEndian(target, ref cursor, TelemetryDumpMagic) &&
                   TryWriteInt32LittleEndian(target, ref cursor, TelemetryDumpFormatVersion) &&
                   TryWriteInt32LittleEndian(target, ref cursor, TelemetryEntrySizeBytes) &&
                   TryWriteInt32LittleEndian(target, ref cursor, entryCount) &&
                   TryWriteInt32LittleEndian(target, ref cursor, writeIndex) &&
                   TryWriteUInt32LittleEndian(target, ref cursor, sequence);
        }

        private static bool TryWriteHazardTelemetryDumpEntry(
            NativeArray<byte> target,
            ref int cursor,
            in HazardZoneTelemetryEntry entry)
        {
            return TryWriteUInt64LittleEndian(target, ref cursor, entry.PackedOwner) &&
                   TryWriteUInt32LittleEndian(target, ref cursor, entry.FrameIndex) &&
                   TryWriteUInt32LittleEndian(target, ref cursor, entry.Sequence) &&
                   TryWriteUInt32LittleEndian(target, ref cursor, entry.StateHash) &&
                   TryWriteUInt32LittleEndian(target, ref cursor, entry.Flags) &&
                   TryWriteInt32LittleEndian(target, ref cursor, entry.ActiveZoneCount) &&
                   TryWriteInt32LittleEndian(target, ref cursor, entry.PendingMutationCount) &&
                   TryWriteInt32LittleEndian(target, ref cursor, entry.PublishedExposureMask) &&
                   TryWriteUInt32LittleEndian(target, ref cursor, entry.BufferGeneration) &&
                   TryWriteFloatLittleEndian(target, ref cursor, entry.ToxicityDose) &&
                   TryWriteFloatLittleEndian(target, ref cursor, entry.ToxicityPulseAccumulatorSeconds) &&
                   TryWriteFloatLittleEndian(target, ref cursor, entry.PlayerToxicity) &&
                   TryWriteFloatLittleEndian(target, ref cursor, entry.VehicleToxicity) &&
                   TryWriteFloatLittleEndian(target, ref cursor, entry.PlayerRadiation) &&
                   TryWriteFloatLittleEndian(target, ref cursor, entry.VehicleRadiation);
        }

        private static bool TryWriteFloatLittleEndian(NativeArray<byte> target, ref int cursor, float value)
        {
            return TryWriteUInt32LittleEndian(target, ref cursor, math.asuint(value));
        }

        private static bool TryWriteInt32LittleEndian(NativeArray<byte> target, ref int cursor, int value)
        {
            return TryWriteUInt32LittleEndian(target, ref cursor, unchecked((uint)value));
        }

        private static bool TryWriteUInt32LittleEndian(NativeArray<byte> target, ref int cursor, uint value)
        {
            const int WriteBytes = sizeof(uint);
            if (!CanWriteLittleEndianBytes(target, cursor, WriteBytes))
                return false;

            target[cursor++] = (byte)value;
            target[cursor++] = (byte)(value >> 8);
            target[cursor++] = (byte)(value >> 16);
            target[cursor++] = (byte)(value >> 24);
            return true;
        }

        private static bool TryWriteUInt64LittleEndian(NativeArray<byte> target, ref int cursor, ulong value)
        {
            const int WriteBytes = sizeof(ulong);
            if (!CanWriteLittleEndianBytes(target, cursor, WriteBytes))
                return false;

            target[cursor++] = (byte)value;
            target[cursor++] = (byte)(value >> 8);
            target[cursor++] = (byte)(value >> 16);
            target[cursor++] = (byte)(value >> 24);
            target[cursor++] = (byte)(value >> 32);
            target[cursor++] = (byte)(value >> 40);
            target[cursor++] = (byte)(value >> 48);
            target[cursor++] = (byte)(value >> 56);
            return true;
        }

        private static bool CanWriteLittleEndianBytes(NativeArray<byte> target, int cursor, int byteCount)
        {
            return target.IsCreated &&
                   byteCount >= 0 &&
                   cursor >= 0 &&
                   cursor <= target.Length - byteCount;
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

        private static bool AreHazardRuntimeLayoutsValid()
        {
            return UnsafeUtility.SizeOf<HazardVolumeData>() == 64 &&
                   UnsafeUtility.SizeOf<HazardExposureJobResult>() == 128 &&
                   UnsafeUtility.SizeOf<HazardZoneTelemetryEntry>() == TelemetryEntrySizeBytes;
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
            IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;
            if (runtimeContext == null)
                return false;

            if (runtimeContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                (snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                IsFiniteAup(in snapshot.Aup))
            {
                playerAup = snapshot.Aup;
                return true;
            }

            if (!runtimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) ||
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u ||
                !IsFiniteAup(in movementState.PredictedAup))
            {
                return false;
            }

            playerAup = movementState.PredictedAup;
            return true;
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
            float safeRadius = NormalizeHazardRadius(radius);
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
                ReleaseExposureJobLocks();
            }

            ClearPendingMutations();
            _activeCount = 0;
            _toxicityDose = 0f;
            _toxicityPulseAccumulatorSeconds = 0f;
            _lastExposureJobResultNonFinite = false;
            ClearPlayerRuntimeBindings();

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
            _lastExposureJobResultNonFinite = false;
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
            mutation.Radius = NormalizeHazardRadius(radius);
            mutation.Type = type;
            mutation.VisorGlitchBias = visorGlitchBias;
            mutation.Profile = profile;
            return QueueMutation(in mutation);
        }

        private bool QueueUnregisterMutation(int id)
        {
            if (id <= 0)
                return false;

            PendingHazardZoneMutation mutation = default;
            mutation.Kind = PendingHazardZoneMutationKind.Unregister;
            mutation.Id = id;
            if (QueueMutation(in mutation))
                return true;

            return QueueOverflowUnregister(id);
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

        private bool QueueOverflowUnregister(int id)
        {
            if (id <= 0)
                return false;

            for (int i = 0; i < _pendingOverflowUnregisterCount; i++)
            {
                if (_pendingOverflowUnregisterIds[i] == id)
                    return true;
            }

            if (_pendingOverflowUnregisterCount >= _pendingOverflowUnregisterIds.Length)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    PendingMutationOverflowWarningHash,
                    HazardManagerContextHash,
                    _pendingMutationCount + _pendingOverflowUnregisterCount);
                return false;
            }

            _pendingOverflowUnregisterIds[_pendingOverflowUnregisterCount++] = id;
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

        private static float NormalizeHazardRadius(float value)
        {
            if (!math.isfinite(value) || value <= 0f)
                return MinHazardRadius;

            return math.clamp(value, MinHazardRadius, MaxHazardRadius);
        }

        private static float ClampPersistedToxicityDose(float value)
        {
            return math.clamp(FiniteNonNegativeOrZero(value), 0f, MaxPersistedToxicityDose);
        }

        private static float ClampPersistedToxicityPulseAccumulator(float value)
        {
            return math.clamp(FiniteNonNegativeOrZero(value), 0f, ToxicityDamagePulseIntervalSeconds);
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
            return math.isfinite(value) ? math.clamp(value, 0f, HazardIntensityHardCap) : 0f;
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
            if (_jobRunning || !_volumes.IsCreated)
                return;

            bool appliedOverflowUnregisters = ApplyOverflowUnregistersIfIdle();
            if (_pendingMutationCount <= 0)
            {
                if (appliedOverflowUnregisters)
                    UpdateDiagnostics();
                return;
            }

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

        private bool ApplyOverflowUnregistersIfIdle()
        {
            int pendingOverflowCount = _pendingOverflowUnregisterCount;
            if (pendingOverflowCount <= 0)
                return false;

            _pendingOverflowUnregisterCount = 0;
            for (int i = 0; i < pendingOverflowCount; i++)
            {
                int id = _pendingOverflowUnregisterIds[i];
                _pendingOverflowUnregisterIds[i] = 0;
                if (id > 0)
                    UnregisterZoneImmediate(id);
            }

            return true;
        }

        private void ClearPendingMutations()
        {
            for (int i = 0; i < _pendingMutationCount; i++)
                _pendingMutations[i] = default;

            for (int i = 0; i < _pendingOverflowUnregisterCount; i++)
                _pendingOverflowUnregisterIds[i] = 0;

            _pendingMutationCount = 0;
            _pendingOverflowUnregisterCount = 0;
        }

        private void PublishExposureMask(int nextMask)
        {
            nextMask &= HazardTypeMaskNonRadiation;
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
            signal.depth = ResolvePlayerSignalDepthMeters();
            signal.sourceID = DamageSourceIds.EnvironmentHazard;
            _playerTraumaDispatcher.OnClarityChanged(0f, clarityImpulse, signal);
        }

        private float ResolvePlayerSignalDepthMeters()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null &&
                playerContext.IsInitialized &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                math.isfinite(movementState.DepthMeters))
            {
                return math.max(0f, movementState.DepthMeters);
            }

            if (playerContext != null)
                return 0f;

            HectonSurvivalSystem survival = _playerSurvival;
            if (survival != null && math.isfinite(survival.Depth))
                return math.max(0f, survival.Depth);

            return 0f;
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

        private void TryRegisterSaveParticipant()
        {
            if (_saveRegistered || !Application.isPlaying || !isActiveAndEnabled)
                return;

            ISaveService saveService = _saveService;
            if (!IsSaveServiceUsable(saveService))
            {
                saveService = GlobalRegistry.Save;
                _saveService = saveService;
            }

            if (!IsSaveServiceUsable(saveService))
                return;

            saveService.Register(this);
            _registeredSaveService = saveService;
            _saveRegistered = true;
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
        }

        private void TryUnregisterSaveParticipant()
        {
            if (!_saveRegistered && _registeredSaveService == null)
                return;

            ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;
            if (saveService != null)
                saveService.Unregister(this);

            _registeredSaveService = null;
            _saveRegistered = false;
            _saveService = null;
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
            float playerToxicity = ClampExposure(_playerHazardIntensity[(int)HazardType.Toxicity]);
            float vehicleToxicity = ClampExposure(_vehicleHazardIntensity[(int)HazardType.Toxicity]);
            _debugActiveZoneCount = _activeCount;
            _debugToxicityDose = ToxicityDose;
            _debugPlayerToxicityIntensity = playerToxicity;
            _debugVehicleToxicityIntensity = vehicleToxicity;
            _debugJobRunning = _jobRunning;
            _debugPlayerExposureActive = (_publishedExposureMask & (1 << (int)HazardType.Toxicity)) != 0;
            _debugVehicleExposureActive = vehicleToxicity > 0.001f;
            _debugPendingMutationCount = _pendingMutationCount + _pendingOverflowUnregisterCount;
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
