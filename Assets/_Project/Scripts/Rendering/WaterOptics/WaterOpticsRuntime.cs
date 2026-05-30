using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Rendering.WaterOptics
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct WaterOpticsDTO
    {
        [FieldOffset(0)] public float4 AbsorptionCoefficientsRGB;
        [FieldOffset(16)] public float4 ScatteringCoefficientsRGB;
        [FieldOffset(32)] public float4 DirectionalLightColorAndIntensity;
        [FieldOffset(48)] public float4 QualityAndDepthLimits;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct WaterOpticsProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public float MaxDistanceMeters;
        [FieldOffset(12)] public float QualityBias;
        [FieldOffset(16)] public float4 AbsorptionCoefficientsRGB;
        [FieldOffset(32)] public float4 ScatteringCoefficientsRGB;
        [FieldOffset(48)] public float4 DirectionalLightColorAndIntensity;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct WaterOpticsTuningDTO
    {
        [FieldOffset(0)] public float4 AbsorptionCoefficientsRGB;
        [FieldOffset(16)] public float4 ScatteringCoefficientsRGB;
        [FieldOffset(32)] public float4 DirectionalLightColorAndIntensity;
        [FieldOffset(48)] public float4 MaxDistanceQualityFlagsProfile;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct WaterOpticsTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public float EstimatedOpaqueGpuMicroseconds;
        [FieldOffset(12)] public float ActiveSpectralWeight;
        [FieldOffset(16)] public float4 AbsorptionCoefficientsRGB;
        [FieldOffset(32)] public float4 ScatteringCoefficientsRGB;
        [FieldOffset(48)] public float4 QualityAndDepthLimits;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct WaterOpticsDumpHeader
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public int Capacity;
        [FieldOffset(12)] public int RowSizeBytes;
        [FieldOffset(16)] public int Cursor;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public ulong Reserved;
    }

    public static class WaterOpticsNativeLayout
    {
        public const int OpticsSizeBytes = 64;
        public const int ProfileSizeBytes = 64;
        public const int TuningSizeBytes = 64;
        public const int TelemetrySizeBytes = 64;
        public const int DumpHeaderSizeBytes = 32;

        public static bool Validate(out int opticsSize, out int profileSize, out int tuningSize, out int telemetrySize)
        {
            opticsSize = UnsafeUtility.SizeOf<WaterOpticsDTO>();
            profileSize = UnsafeUtility.SizeOf<WaterOpticsProfileDTO>();
            tuningSize = UnsafeUtility.SizeOf<WaterOpticsTuningDTO>();
            telemetrySize = UnsafeUtility.SizeOf<WaterOpticsTelemetryEntry>();
            return opticsSize == OpticsSizeBytes &&
                   profileSize == ProfileSizeBytes &&
                   tuningSize == TuningSizeBytes &&
                   telemetrySize == TelemetrySizeBytes &&
                   Marshal.OffsetOf(typeof(WaterOpticsDTO), nameof(WaterOpticsDTO.AbsorptionCoefficientsRGB)).ToInt32() == 0 &&
                   Marshal.OffsetOf(typeof(WaterOpticsDTO), nameof(WaterOpticsDTO.ScatteringCoefficientsRGB)).ToInt32() == 16 &&
                   Marshal.OffsetOf(typeof(WaterOpticsDTO), nameof(WaterOpticsDTO.DirectionalLightColorAndIntensity)).ToInt32() == 32 &&
                   Marshal.OffsetOf(typeof(WaterOpticsDTO), nameof(WaterOpticsDTO.QualityAndDepthLimits)).ToInt32() == 48 &&
                   Marshal.OffsetOf(typeof(WaterOpticsTuningDTO), nameof(WaterOpticsTuningDTO.MaxDistanceQualityFlagsProfile)).ToInt32() == 48;
        }

        public static bool ValidateDumpHeader(out int headerSize)
        {
            headerSize = UnsafeUtility.SizeOf<WaterOpticsDumpHeader>();
            return headerSize == DumpHeaderSizeBytes;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-92)]
    public sealed unsafe class WaterOpticsRuntime : MonoBehaviour, IDispatcherSystem, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        public const int OpticsSizeBytes = WaterOpticsNativeLayout.OpticsSizeBytes;
        public const int ProfileCapacity = 64;
        public const int TelemetryCapacity = 300;
        public const int CsvScratchBytes = 64 * 1024;
        public const uint SystemHash = 0x53323635u;
        public const uint VisualSystemHash = 0x56323635u;

        private const uint TuningFlagActive = 1u << 0;
        private const uint TelemetryFlagVaultMissing = 1u << 0;
        private const uint TelemetryFlagConstantBufferUnsupported = 1u << 1;
        private const uint TelemetryFlagInvalidNumber = 1u << 2;
        private const uint TelemetryFlagUploadSkipped = 1u << 3;
        private const uint TelemetryFlagProfileMissing = 1u << 4;
        private const uint TelemetryFlagEstimatedGpuBudgetBreach = 1u << 6;
        private const uint TelemetryFlagUploadUnchanged = 1u << 7;
        private const uint TelemetrySourceHash = 0x574F5054u;
        private const uint TelemetryDumpVersion = 1u;
        private const string TelemetryDumpRelativePath = "Docs/AgentLogs/Dump_13KRA.bin";

        [SerializeField, Range(0f, 4f)] private float _absorptionR = 0.42f;
        [SerializeField, Range(0f, 4f)] private float _absorptionG = 0.105f;
        [SerializeField, Range(0f, 4f)] private float _absorptionB = 0.028f;
        [SerializeField, Range(0f, 8f)] private float _extinctionMultiplier = 1f;
        [SerializeField, Range(0f, 2f)] private float _scatteringR = 0.035f;
        [SerializeField, Range(0f, 2f)] private float _scatteringG = 0.09f;
        [SerializeField, Range(0f, 2f)] private float _scatteringB = 0.16f;
        [SerializeField, Range(-0.85f, 0.85f)] private float _anisotropy = 0.42f;
        [SerializeField, Range(0f, 4f)] private float _lightR = 0.09f;
        [SerializeField, Range(0f, 4f)] private float _lightG = 0.42f;
        [SerializeField, Range(0f, 4f)] private float _lightB = 0.70f;
        [SerializeField, Range(0f, 8f)] private float _lightIntensity = 0.85f;
        [SerializeField, Range(-1f, 1f)] private float _qualityBias;
        [SerializeField, Range(1f, 12000f)] private float _maxDistanceMeters = 5000f;
        [SerializeField] private float _oceanSurfaceWorldY;
        [SerializeField] private bool _active = true;
        [SerializeField] private bool _loadProfilesOnEnable = true;
        [SerializeField, Range(1f, 250f)] private float _opaqueGpuBudgetMicroseconds = 80f;
        [SerializeField] private Camera _camera;

        private static WaterOpticsRuntime s_instance;
        private IDataVault _vault;
        private VaultGenerationHandle<WaterOpticsDTO> _paramsHandle;
        private VaultGenerationHandle<WaterOpticsTuningDTO> _tuningHandle;
        private VaultGenerationHandle<WaterOpticsProfileDTO> _profileHandle;
        private VaultGenerationHandle<WaterOpticsTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private GraphicsBuffer _shaderParamsBufferA;
        private GraphicsBuffer _shaderParamsBufferB;
        private GraphicsBuffer _activeShaderParamsBuffer;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private VisualSyncUploadSystem _visualSyncSystem;
        private int _shaderWriteIndex;
        private uint _loadedProfileCount;
        private bool _cameraRuntimeResolved;
        private bool _registered;
        private bool _visualRegistered;
        private bool _hotSwapRegistered;
        private bool _vaultBootstrapped;
        private bool _profilesLoadAttempted;
        private bool _tuningDirty = true;
        private bool _dumped;
        private bool _telemetryDumpPending;
        private uint _lastTelemetryDumpHash;
        private bool _hasUploadedDto;
        private bool _supportsConstantBuffers;
        private WaterOpticsDTO _lastUploadedDto;

        private static readonly int GlobalWaterOpticsCBufferId = Shader.PropertyToID("_GlobalWaterOptics");

        public uint GetSystemIdHash() => SystemHash;
        public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.PreSimulation;
        public byte GetBucketId() => 0;
        public int GetDependencyCount() => 0;
        public uint GetDependencyHash(int dependencyIndex) => 0u;

        public static bool TryGetRuntimeInstance(out WaterOpticsRuntime runtime)
        {
            runtime = s_instance;
            return runtime != null;
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                enabled = false;
                return;
            }

            s_instance = this;
            CacheGraphicsCapabilitiesCold();
            TryColdBootstrapVault(clearExisting: true);
            TryColdBootstrapShaderParamsBuffers();
            RefreshPlayerCameraBindingCold();
        }

        private void OnEnable()
        {
            if (s_instance != null && s_instance != this)
            {
                enabled = false;
                return;
            }

            s_instance = this;
            CacheGraphicsCapabilitiesCold();
            TryColdBootstrapVault(clearExisting: !_vaultBootstrapped);
            TryColdBootstrapShaderParamsBuffers();
            RefreshPlayerCameraBindingCold();

            _visualSyncSystem = new VisualSyncUploadSystem(this); // COLD ALLOC: IDispatcherSystem[1] - 13KRA VisualSync constant-buffer upload bridge.
            _registered = GlobalRegistry.TryRegisterDispatcherSystem(this);
            _visualRegistered = GlobalRegistry.TryRegisterDispatcherSystem(_visualSyncSystem);
            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void Start()
        {
            CacheGraphicsCapabilitiesCold();
            TryColdBootstrapVault(clearExisting: !_vaultBootstrapped);
            TryColdBootstrapShaderParamsBuffers();
        }

        private void OnDisable()
        {
            OnServiceShutdown();
        }

        public void OnServiceShutdown()
        {
            FlushPendingTelemetryDump();

            if (_registered)
            {
                GlobalRegistry.UnregisterDispatcherSystem(this);
                _registered = false;
            }

            if (_visualRegistered)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_visualSyncSystem);
                _visualRegistered = false;
            }

            if (_hotSwapRegistered)
            {
                GlobalRegistry.UnregisterHotSwapListener(this);
                _hotSwapRegistered = false;
            }

            _visualSyncSystem = null;
            ReleaseGraphicsBuffer(ref _shaderParamsBufferA);
            ReleaseGraphicsBuffer(ref _shaderParamsBufferB);
            _activeShaderParamsBuffer = null;
            _shaderWriteIndex = 0;
            _hasUploadedDto = false;
            _lastUploadedDto = default;
            _playerRuntimeContext = null;
            if (_cameraRuntimeResolved)
                _camera = null;
            _cameraRuntimeResolved = false;

            IDataVault vault = _vault;
            if (vault != null)
                ReleaseOwnedVaultBuffers(vault);

            _vault = null;
            _loadedProfileCount = 0u;
            _vaultBootstrapped = false;
            _profilesLoadAttempted = false;
            _tuningDirty = true;
            if (s_instance == this)
                s_instance = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                CachePlayerRuntimeContext(currentService as IPlayerRuntimeContext);
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            IDataVault previousVault = _vault;
            IDataVault currentVault = currentService as IDataVault;
            if (currentVault != null && ReferenceEquals(previousVault, currentVault))
            {
                TryColdBootstrapVault(clearExisting: false);
                return;
            }

            if (previousVault != null && !ReferenceEquals(previousVault, currentVault))
                ReleaseOwnedVaultBuffers(previousVault);

            _vault = currentVault;
            _vaultBootstrapped = false;
            _profilesLoadAttempted = false;
            _loadedProfileCount = 0u;
            if (currentVault != null)
                TryColdBootstrapVault(clearExisting: true);
        }

        public void PreSimulationTick(in DispatcherTimingDTO timing)
        {
            IDataVault vault = _vault;
            if (vault == null ||
                !_vaultBootstrapped ||
                !TryResolve(vault, in _paramsHandle, out NativeArray<WaterOpticsDTO> parameters) ||
                !parameters.IsCreated ||
                parameters.Length == 0 ||
                !TryResolve(vault, in _tuningHandle, out NativeArray<WaterOpticsTuningDTO> tuning) ||
                !tuning.IsCreated ||
                tuning.Length == 0)
            {
                RecordTelemetry(timing.FrameId, TelemetryFlagVaultMissing, default);
                return;
            }

            if (_tuningDirty)
                WriteTuningToVault(force: false);

            WriteMockOpticsParams(parameters, tuning);
        }

        public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn)
        {
            return dependsOn;
        }

        public void PostSimulationTick(in DispatcherTimingDTO timing)
        {
            FlushPendingTelemetryDump();
        }

        public void VisualSyncTick(in DispatcherTimingDTO timing)
        {
            uint frameIndex = timing.FrameId;
            IDataVault vault = _vault;
            if (vault == null ||
                !TryResolve(vault, in _paramsHandle, out NativeArray<WaterOpticsDTO> parameters) ||
                !parameters.IsCreated ||
                parameters.Length == 0)
            {
                RecordTelemetry(frameIndex, TelemetryFlagVaultMissing, default);
                return;
            }

            if (!_supportsConstantBuffers)
            {
                WaterOpticsDTO unsupportedDto = ReadFirstWaterOpticsDto(parameters);
                RecordTelemetry(frameIndex, TelemetryFlagConstantBufferUnsupported, in unsupportedDto);
                return;
            }

            if (!HasValidShaderParamsBuffers())
            {
                WaterOpticsDTO skippedDto = ReadFirstWaterOpticsDto(parameters);
                RecordTelemetry(frameIndex, TelemetryFlagUploadSkipped, in skippedDto);
                return;
            }

            WaterOpticsDTO dto = ReadFirstWaterOpticsDto(parameters);
            uint flags = BuildVisualSyncTelemetryFlags(in dto);
            if ((flags & TelemetryFlagInvalidNumber) != 0u)
            {
                flags = RecordTelemetry(frameIndex, flags, in dto);
                RequestTelemetryDump();
                return;
            }

            if (_hasUploadedDto &&
                _activeShaderParamsBuffer != null &&
                _activeShaderParamsBuffer.IsValid() &&
                WaterOpticsDtoEquals(in dto, in _lastUploadedDto))
            {
                flags = RecordTelemetry(frameIndex, flags | TelemetryFlagUploadUnchanged, in dto);
                if ((flags & TelemetryFlagEstimatedGpuBudgetBreach) != 0u)
                    RequestTelemetryDump();
                return;
            }

            GraphicsBuffer writeBuffer = ResolveNextShaderParamsBuffer();
            NativeArray<WaterOpticsDTO> mapped = writeBuffer.LockBufferForWrite<WaterOpticsDTO>(0, 1);
            try
            {
                void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(parameters);
                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafePtr(mapped);
                UnsafeUtility.MemCpy(destinationPtr, sourcePtr, OpticsSizeBytes);
            }
            finally
            {
                writeBuffer.UnlockBufferAfterWrite<WaterOpticsDTO>(1);
            }

            _activeShaderParamsBuffer = writeBuffer;
            Shader.SetGlobalConstantBuffer(GlobalWaterOpticsCBufferId, _activeShaderParamsBuffer, 0, OpticsSizeBytes);
            _lastUploadedDto = dto;
            _hasUploadedDto = true;

            flags = RecordTelemetry(frameIndex, flags, in dto);
            if ((flags & (TelemetryFlagInvalidNumber | TelemetryFlagEstimatedGpuBudgetBreach)) != 0u)
                RequestTelemetryDump();
        }

        public void ApplyEditorTuning(
            Vector4 absorptionAndMultiplier,
            Vector4 scatteringAndAnisotropy,
            Vector4 lightAndIntensity,
            float oceanSurfaceWorldY,
            float maxDistanceMeters,
            float qualityBias,
            bool active)
        {
            _absorptionR = Mathf.Max(0f, absorptionAndMultiplier.x);
            _absorptionG = Mathf.Max(0f, absorptionAndMultiplier.y);
            _absorptionB = Mathf.Max(0f, absorptionAndMultiplier.z);
            _extinctionMultiplier = Mathf.Max(0f, absorptionAndMultiplier.w);
            _scatteringR = Mathf.Max(0f, scatteringAndAnisotropy.x);
            _scatteringG = Mathf.Max(0f, scatteringAndAnisotropy.y);
            _scatteringB = Mathf.Max(0f, scatteringAndAnisotropy.z);
            _anisotropy = Mathf.Clamp(scatteringAndAnisotropy.w, -0.85f, 0.85f);
            _lightR = Mathf.Max(0f, lightAndIntensity.x);
            _lightG = Mathf.Max(0f, lightAndIntensity.y);
            _lightB = Mathf.Max(0f, lightAndIntensity.z);
            _lightIntensity = Mathf.Max(0f, lightAndIntensity.w);
            _oceanSurfaceWorldY = oceanSurfaceWorldY;
            _maxDistanceMeters = Mathf.Max(1f, maxDistanceMeters);
            _qualityBias = Mathf.Clamp(qualityBias, -1f, 1f);
            _active = active;
            _tuningDirty = true;
            WriteTuningToVault(force: true);
        }

        public bool TryReadLatestParams(out WaterOpticsDTO dto)
        {
            dto = default;
            IDataVault vault = _vault;
            if (vault == null ||
                !TryReadOnly(vault, in _paramsHandle, out NativeArray<WaterOpticsDTO>.ReadOnly parameters) ||
                !parameters.IsCreated ||
                parameters.Length == 0)
            {
                return false;
            }

            dto = parameters[0];
            return true;
        }

        public bool TryReadLatestTuning(out WaterOpticsTuningDTO dto)
        {
            dto = default;
            IDataVault vault = _vault;
            if (vault == null ||
                !TryReadOnly(vault, in _tuningHandle, out NativeArray<WaterOpticsTuningDTO>.ReadOnly tuning) ||
                !tuning.IsCreated ||
                tuning.Length == 0)
            {
                return false;
            }

            dto = tuning[0];
            return true;
        }

        public bool TryReadLatestTelemetry(out WaterOpticsTelemetryEntry dto)
        {
            dto = default;
            IDataVault vault = _vault;
            if (vault == null ||
                !TryReadOnly(vault, in _telemetryHandle, out NativeArray<WaterOpticsTelemetryEntry>.ReadOnly ring) ||
                !TryReadOnly(vault, in _telemetryCursorHandle, out NativeArray<int>.ReadOnly cursorArray) ||
                !ring.IsCreated ||
                ring.Length == 0 ||
                !cursorArray.IsCreated ||
                cursorArray.Length == 0)
            {
                return false;
            }

            int cursor = cursorArray[0] - 1;
            if (cursor < 0)
                cursor = ring.Length - 1;
            dto = ring[cursor];
            return dto.FrameIndex != 0u || dto.Flags != 0u;
        }

        public bool TryReadTelemetryEntry(int framesBack, out WaterOpticsTelemetryEntry dto)
        {
            dto = default;
            IDataVault vault = _vault;
            if (framesBack < 0 ||
                vault == null ||
                !TryReadOnly(vault, in _telemetryHandle, out NativeArray<WaterOpticsTelemetryEntry>.ReadOnly ring) ||
                !TryReadOnly(vault, in _telemetryCursorHandle, out NativeArray<int>.ReadOnly cursorArray) ||
                !ring.IsCreated ||
                ring.Length == 0 ||
                !cursorArray.IsCreated ||
                cursorArray.Length == 0)
            {
                return false;
            }

            int boundedBack = math.min(framesBack, ring.Length - 1);
            int cursor = cursorArray[0] - 1 - boundedBack;
            while (cursor < 0)
                cursor += ring.Length;
            dto = ring[cursor];
            return dto.FrameIndex != 0u || dto.Flags != 0u;
        }

        public bool TryApplyProfileHash(uint profileHash)
        {
            IDataVault vault = _vault;
            if (profileHash == 0u ||
                vault == null ||
                !TryResolve(vault, in _profileHandle, out NativeArray<WaterOpticsProfileDTO> profiles) ||
                !profiles.IsCreated)
            {
                return false;
            }

            int count = math.min((int)_loadedProfileCount, profiles.Length);
            for (int i = 0; i < count; i++)
            {
                WaterOpticsProfileDTO profile = ReadProfileAt(profiles, i);
                if (profile.ProfileHash != profileHash)
                    continue;

                ApplyProfile(in profile);
                _tuningDirty = true;
                WriteTuningToVault(force: true);
                return true;
            }

            return false;
        }

#if UNITY_EDITOR
        public bool TryReloadEditorProfilesCsv()
        {
            if (_vault == null && !TryColdBootstrapVault(clearExisting: false))
                return false;

            IDataVault vault = _vault;
            if (vault == null || !EnsureVaultBuffers(vault, clearExisting: false))
                return false;

            _profilesLoadAttempted = false;
            bool loaded = TryLoadProfilesCsv(vault);
            _profilesLoadAttempted = true;
            return loaded;
        }
#endif

        public static bool ValidateLayouts(out int opticsSize, out int profileSize, out int tuningSize, out int telemetrySize)
        {
            return WaterOpticsNativeLayout.Validate(out opticsSize, out profileSize, out tuningSize, out telemetrySize);
        }

        public static bool ValidateDumpHeaderLayout(out int headerSize)
        {
            return WaterOpticsNativeLayout.ValidateDumpHeader(out headerSize);
        }

#if UNITY_EDITOR
        public static bool TryParseProfiles(ReadOnlySpan<byte> csvBytes, NativeArray<WaterOpticsProfileDTO> profiles, out int count)
        {
            count = 0;
            if (!profiles.IsCreated || profiles.Length == 0)
                return false;

            int cursor = 0;
            bool any = false;
            byte* profilePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(profiles);
            while (cursor < csvBytes.Length && count < profiles.Length)
            {
                ReadOnlySpan<byte> line = ReadLine(csvBytes, ref cursor);
                Trim(ref line);
                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;
                if (IsHeader(line))
                    continue;

                if (TryParseProfileRow(line, out WaterOpticsProfileDTO profile))
                {
                    ref WaterOpticsProfileDTO target = ref UnsafeUtility.AsRef<WaterOpticsProfileDTO>(
                        profilePtr + count * WaterOpticsNativeLayout.ProfileSizeBytes);
                    target = profile;
                    count++;
                    any = true;
                }
            }

            return any;
        }
#endif

        private bool TryColdBootstrapVault(bool clearExisting)
        {
            IDataVault vault = _vault;
            if (vault == null)
            {
                vault = GlobalRegistry.DataVault;
                if (vault == null)
                    return false;

                _vault = vault;
            }

            bool shouldClear = clearExisting || !_vaultBootstrapped;
            if (!EnsureVaultBuffers(vault, shouldClear))
                return false;

            WriteTuningToVault(force: true);
            if (_loadProfilesOnEnable && !_profilesLoadAttempted)
            {
                TryLoadProfilesCsv(vault);
                _profilesLoadAttempted = true;
            }

            _vaultBootstrapped = true;
            return true;
        }

        private bool EnsureVaultBuffers(IDataVault vault, bool clearExisting)
        {
            if (vault == null)
                return false;

            bool hadResolvedBuffers = HasResolvedVaultBuffers(vault);
            if (!clearExisting && hadResolvedBuffers)
                return true;

            if (vault.IsCompactionFenceActive || vault.IsAllocationLocked)
                return false;

            NativeArrayOptions options = NativeArrayOptions.UninitializedMemory;
            _paramsHandle = vault.EnsureGenerationHandle<WaterOpticsDTO>(
                BufferID.ShinobuWaterOpticsParams,
                1,
                SystemID.Vfx,
                options);
            _tuningHandle = vault.EnsureGenerationHandle<WaterOpticsTuningDTO>(
                BufferID.ShinobuWaterOpticsTuning,
                1,
                SystemID.Vfx,
                options);
            _profileHandle = vault.EnsureGenerationHandle<WaterOpticsProfileDTO>(
                BufferID.ShinobuWaterOpticsProfiles,
                ProfileCapacity,
                SystemID.Vfx,
                options);
            _telemetryHandle = vault.EnsureGenerationHandle<WaterOpticsTelemetryEntry>(
                BufferID.ShinobuWaterOpticsTelemetryRing,
                TelemetryCapacity,
                SystemID.Vfx,
                options);
            _telemetryCursorHandle = vault.EnsureGenerationHandle<int>(
                BufferID.ShinobuWaterOpticsTelemetryCursor,
                1,
                SystemID.Vfx,
                options);
            _csvScratchHandle = vault.EnsureGenerationHandle<byte>(
                BufferID.ShinobuWaterOpticsCsvScratch,
                CsvScratchBytes,
                SystemID.Vfx,
                options);

            if (clearExisting || !hadResolvedBuffers)
            {
                InitializeParams(vault);
                ClearBuffer(vault, in _tuningHandle);
                _tuningDirty = true;
                WriteTuningToVault(force: true);
                ClearBuffer(vault, in _profileHandle);
                ClearBuffer(vault, in _telemetryHandle);
                ClearBuffer(vault, in _telemetryCursorHandle);
                _dumped = false;
                _loadedProfileCount = 0u;
            }

            return HasResolvedVaultBuffers(vault);
        }

        private void ReleaseOwnedVaultBuffers(IDataVault vault)
        {
            ReleaseVaultBuffer(vault, in _paramsHandle);
            ReleaseVaultBuffer(vault, in _tuningHandle);
            ReleaseVaultBuffer(vault, in _profileHandle);
            ReleaseVaultBuffer(vault, in _telemetryHandle);
            ReleaseVaultBuffer(vault, in _telemetryCursorHandle);
            ReleaseVaultBuffer(vault, in _csvScratchHandle);

            _paramsHandle = default;
            _tuningHandle = default;
            _profileHandle = default;
            _telemetryHandle = default;
            _telemetryCursorHandle = default;
            _csvScratchHandle = default;
        }

        private bool HasResolvedVaultBuffers(IDataVault vault)
        {
            return TryResolve(vault, in _paramsHandle, out NativeArray<WaterOpticsDTO> parameters) &&
                   parameters.Length >= 1 &&
                   TryResolve(vault, in _tuningHandle, out NativeArray<WaterOpticsTuningDTO> tuning) &&
                   tuning.Length >= 1 &&
                   TryResolve(vault, in _profileHandle, out NativeArray<WaterOpticsProfileDTO> profiles) &&
                   profiles.Length >= ProfileCapacity &&
                   TryResolve(vault, in _telemetryHandle, out NativeArray<WaterOpticsTelemetryEntry> telemetry) &&
                   telemetry.Length >= TelemetryCapacity &&
                   TryResolve(vault, in _telemetryCursorHandle, out NativeArray<int> cursor) &&
                   cursor.Length >= 1 &&
                   TryResolve(vault, in _csvScratchHandle, out NativeArray<byte> scratch) &&
                   scratch.Length >= CsvScratchBytes;
        }

        private void WriteMockOpticsParams(NativeArray<WaterOpticsDTO> parameters, NativeArray<WaterOpticsTuningDTO> tuning)
        {
            if (!parameters.IsCreated || parameters.Length == 0)
                return;

            WaterOpticsTuningDTO tuningDto = tuning.IsCreated && tuning.Length > 0 ? ReadFirstTuningDto(tuning) : DefaultTuning();
            WaterOpticsDTO dto = BuildWaterOpticsDto(in tuningDto, ResolveGlobalQualityWeight(), ResolveLocalSurfaceY());
            WriteFirstWaterOpticsDto(parameters, in dto);
        }

        private static WaterOpticsDTO BuildWaterOpticsDto(in WaterOpticsTuningDTO tuning, float globalQualityWeight, float localSurfaceY)
        {
            float qualityBias = math.isfinite(tuning.MaxDistanceQualityFlagsProfile.y)
                ? math.clamp(tuning.MaxDistanceQualityFlagsProfile.y, -1f, 1f)
                : 0f;
            float quality = math.saturate((math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f) + qualityBias);
            float maxDistance = math.max(1f, math.isfinite(tuning.MaxDistanceQualityFlagsProfile.x) ? tuning.MaxDistanceQualityFlagsProfile.x : 5000f);
            float active = tuning.MaxDistanceQualityFlagsProfile.z > 0.5f ? 1f : 0f;
            WaterOpticsDTO dto = default;
            dto.AbsorptionCoefficientsRGB = SanitizeCoefficients(tuning.AbsorptionCoefficientsRGB, new float4(0.42f, 0.105f, 0.028f, 1f));
            dto.ScatteringCoefficientsRGB = SanitizeCoefficients(tuning.ScatteringCoefficientsRGB, new float4(0.035f, 0.09f, 0.16f, 0.42f));
            dto.DirectionalLightColorAndIntensity = SanitizeLight(tuning.DirectionalLightColorAndIntensity);
            dto.QualityAndDepthLimits = new float4(
                quality,
                math.isfinite(localSurfaceY) ? localSurfaceY : 0f,
                maxDistance,
                active);
            return dto;
        }

        private static WaterOpticsTuningDTO DefaultTuning()
        {
            return new WaterOpticsTuningDTO
            {
                AbsorptionCoefficientsRGB = new float4(0.42f, 0.105f, 0.028f, 1f),
                ScatteringCoefficientsRGB = new float4(0.035f, 0.09f, 0.16f, 0.42f),
                DirectionalLightColorAndIntensity = new float4(0.09f, 0.42f, 0.70f, 0.85f),
                MaxDistanceQualityFlagsProfile = new float4(5000f, 0f, 1f, 0f)
            };
        }

        private static float4 SanitizeCoefficients(float4 value, float4 fallback)
        {
            bool valid = math.all(math.isfinite(value));
            float4 safe = valid ? value : fallback;
            safe.xyz = math.max(safe.xyz, 0f);
            safe.w = math.max(safe.w, 0f);
            return safe;
        }

        private static float4 SanitizeLight(float4 value)
        {
            float4 safe = math.all(math.isfinite(value)) ? value : new float4(0.09f, 0.42f, 0.70f, 0.85f);
            safe.xyz = math.max(safe.xyz, 0f);
            safe.w = math.max(safe.w, 0f);
            return safe;
        }

        private void InitializeParams(IDataVault vault)
        {
            if (!TryResolve(vault, in _paramsHandle, out NativeArray<WaterOpticsDTO> parameters) ||
                !parameters.IsCreated ||
                parameters.Length == 0)
            {
                return;
            }

            WaterOpticsTuningDTO tuning = new WaterOpticsTuningDTO
            {
                AbsorptionCoefficientsRGB = new float4(_absorptionR, _absorptionG, _absorptionB, _extinctionMultiplier),
                ScatteringCoefficientsRGB = new float4(_scatteringR, _scatteringG, _scatteringB, _anisotropy),
                DirectionalLightColorAndIntensity = new float4(_lightR, _lightG, _lightB, _lightIntensity),
                MaxDistanceQualityFlagsProfile = new float4(_maxDistanceMeters, _qualityBias, _active ? 1f : 0f, 0f)
            };
            WaterOpticsDTO dto = BuildWaterOpticsDto(in tuning, ResolveGlobalQualityWeight(), ResolveLocalSurfaceY());
            WriteFirstWaterOpticsDto(parameters, in dto);
        }

        private bool WriteTuningToVault(bool force)
        {
            if (!force && !_tuningDirty)
                return true;

            IDataVault vault = _vault;
            if (vault == null ||
                !TryResolve(vault, in _tuningHandle, out NativeArray<WaterOpticsTuningDTO> tuning) ||
                !tuning.IsCreated ||
                tuning.Length == 0)
            {
                return false;
            }

            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(tuning);
            ref WaterOpticsTuningDTO target = ref UnsafeUtility.AsRef<WaterOpticsTuningDTO>(ptr);
            target = new WaterOpticsTuningDTO
            {
                AbsorptionCoefficientsRGB = new float4(
                    math.max(0f, _absorptionR),
                    math.max(0f, _absorptionG),
                    math.max(0f, _absorptionB),
                    math.max(0f, _extinctionMultiplier)),
                ScatteringCoefficientsRGB = new float4(
                    math.max(0f, _scatteringR),
                    math.max(0f, _scatteringG),
                    math.max(0f, _scatteringB),
                    math.clamp(_anisotropy, -0.85f, 0.85f)),
                DirectionalLightColorAndIntensity = new float4(
                    math.max(0f, _lightR),
                    math.max(0f, _lightG),
                    math.max(0f, _lightB),
                    math.max(0f, _lightIntensity)),
                MaxDistanceQualityFlagsProfile = new float4(
                    math.max(1f, _maxDistanceMeters),
                    math.clamp(_qualityBias, -1f, 1f),
                    _active ? 1f : 0f,
                    0f)
            };

            _tuningDirty = false;
            return true;
        }

        private bool TryLoadProfilesCsv(IDataVault vault)
        {
#if UNITY_EDITOR
            if (vault == null ||
                !TryResolve(vault, in _csvScratchHandle, out NativeArray<byte> scratch) ||
                !TryResolve(vault, in _profileHandle, out NativeArray<WaterOpticsProfileDTO> profiles) ||
                !scratch.IsCreated ||
                !profiles.IsCreated)
            {
                return false;
            }

            string path = Path.Combine(ResolveProjectRoot(), "Docs", "Data", "Profiles", "water_optics_profiles.csv");
            if (!File.Exists(path))
                return false;

            int bytesRead;
            try
            {
                using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int limit = scratch.Length;
                    if (stream.Length < limit)
                        limit = (int)stream.Length;
                    void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
                    bytesRead = stream.Read(new Span<byte>(ptr, limit));
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            if (bytesRead <= 0)
                return false;

            void* readPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch);
            bool parsed = TryParseProfiles(new ReadOnlySpan<byte>(readPtr, bytesRead), profiles, out int count);
            _loadedProfileCount = parsed ? (uint)count : 0u;
            return parsed;
#else
            return false;
#endif
        }

        private void ApplyProfile(in WaterOpticsProfileDTO profile)
        {
            _absorptionR = profile.AbsorptionCoefficientsRGB.x;
            _absorptionG = profile.AbsorptionCoefficientsRGB.y;
            _absorptionB = profile.AbsorptionCoefficientsRGB.z;
            _extinctionMultiplier = profile.AbsorptionCoefficientsRGB.w;
            _scatteringR = profile.ScatteringCoefficientsRGB.x;
            _scatteringG = profile.ScatteringCoefficientsRGB.y;
            _scatteringB = profile.ScatteringCoefficientsRGB.z;
            _anisotropy = math.clamp(profile.ScatteringCoefficientsRGB.w, -0.85f, 0.85f);
            _lightR = profile.DirectionalLightColorAndIntensity.x;
            _lightG = profile.DirectionalLightColorAndIntensity.y;
            _lightB = profile.DirectionalLightColorAndIntensity.z;
            _lightIntensity = profile.DirectionalLightColorAndIntensity.w;
            _maxDistanceMeters = math.max(1f, profile.MaxDistanceMeters);
            _qualityBias = math.clamp(profile.QualityBias, -1f, 1f);
        }

        private float ResolveLocalSurfaceY()
        {
            Camera camera = ResolveActiveCamera();
            if (camera == null)
                return 0f;

            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            Vector3 cameraPosition = camera.transform.position;
            if (!math.isfinite(origin.y) ||
                !math.isfinite(cameraPosition.y) ||
                !math.isfinite(_oceanSurfaceWorldY))
            {
                return 0f;
            }

            double cameraAupY = origin.y + cameraPosition.y;
            double surfaceAupY = origin.y + _oceanSurfaceWorldY;
            double local = surfaceAupY - cameraAupY;
            return math.isfinite(local) ? (float)math.clamp(local, -100000d, 100000d) : 0f;
        }

        private void RefreshPlayerCameraBindingCold()
        {
            CachePlayerRuntimeContext(GlobalRegistry.Player);
        }

        private void CachePlayerRuntimeContext(IPlayerRuntimeContext playerRuntimeContext)
        {
            _playerRuntimeContext = playerRuntimeContext;
            TryRefreshRuntimeCamera();
        }

        private Camera ResolveActiveCamera()
        {
            TryRefreshRuntimeCamera();
            return _camera;
        }

        private bool TryRefreshRuntimeCamera()
        {
            IPlayerRuntimeContext playerRuntimeContext = _playerRuntimeContext;
            Camera playerCamera = playerRuntimeContext != null ? playerRuntimeContext.PlayerCamera : null;
            if (playerCamera == null)
                return false;

            if (_camera != null && !_cameraRuntimeResolved)
                return false;

            _camera = playerCamera;
            _cameraRuntimeResolved = true;
            return true;
        }

        private static float ResolveGlobalQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(quality, 1f, !math.isfinite(quality)));
        }

        private static float ResolveSpectralAdmissionWeight(float quality)
        {
            float admitted = math.saturate((quality - 0.28f) * 1.3888889f);
            return admitted * admitted * (3f - 2f * admitted);
        }

        private static bool ValidateParams(in WaterOpticsDTO dto)
        {
            return math.all(math.isfinite(dto.AbsorptionCoefficientsRGB)) &&
                   math.all(math.isfinite(dto.ScatteringCoefficientsRGB)) &&
                   math.all(math.isfinite(dto.DirectionalLightColorAndIntensity)) &&
                   math.all(math.isfinite(dto.QualityAndDepthLimits)) &&
                   math.all(dto.AbsorptionCoefficientsRGB.xyz >= 0f) &&
                   math.all(dto.ScatteringCoefficientsRGB.xyz >= 0f) &&
                   dto.QualityAndDepthLimits.z >= 1f;
        }

        private uint BuildVisualSyncTelemetryFlags(in WaterOpticsDTO dto)
        {
            uint flags = ValidateParams(in dto) ? 0u : TelemetryFlagInvalidNumber;
            if (_loadedProfileCount == 0u)
                flags |= TelemetryFlagProfileMissing;
            return flags;
        }

        private static bool WaterOpticsDtoEquals(in WaterOpticsDTO left, in WaterOpticsDTO right)
        {
            return math.all(left.AbsorptionCoefficientsRGB == right.AbsorptionCoefficientsRGB) &&
                   math.all(left.ScatteringCoefficientsRGB == right.ScatteringCoefficientsRGB) &&
                   math.all(left.DirectionalLightColorAndIntensity == right.DirectionalLightColorAndIntensity) &&
                   math.all(left.QualityAndDepthLimits == right.QualityAndDepthLimits);
        }

        private uint RecordTelemetry(uint frame, uint flags, in WaterOpticsDTO dto)
        {
            IDataVault vault = _vault;
            if (vault == null ||
                !TryResolve(vault, in _telemetryHandle, out NativeArray<WaterOpticsTelemetryEntry> ring) ||
                !TryResolve(vault, in _telemetryCursorHandle, out NativeArray<int> cursorArray) ||
                !ring.IsCreated ||
                ring.Length == 0 ||
                !cursorArray.IsCreated ||
                cursorArray.Length == 0)
            {
                return flags;
            }

            void* cursorPtr = NativeArrayUnsafeUtility.GetUnsafePtr(cursorArray);
            ref int cursorRef = ref UnsafeUtility.AsRef<int>(cursorPtr);
            int cursor = cursorRef;
            if ((uint)cursor >= (uint)ring.Length)
                cursor = 0;

            float quality = math.saturate(dto.QualityAndDepthLimits.x);
            float spectralWeight = ResolveSpectralAdmissionWeight(quality);
            float estimatedGpuUsec = math.lerp(2.8f, 9.4f, spectralWeight);
            if (math.isfinite(_opaqueGpuBudgetMicroseconds) && estimatedGpuUsec > math.max(1f, _opaqueGpuBudgetMicroseconds))
                flags |= TelemetryFlagEstimatedGpuBudgetBreach;

            byte* ringPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(ring);
            ref WaterOpticsTelemetryEntry telemetry = ref UnsafeUtility.AsRef<WaterOpticsTelemetryEntry>(
                ringPtr + cursor * WaterOpticsNativeLayout.TelemetrySizeBytes);
            telemetry = new WaterOpticsTelemetryEntry
            {
                FrameIndex = frame,
                Flags = flags,
                EstimatedOpaqueGpuMicroseconds = estimatedGpuUsec,
                ActiveSpectralWeight = spectralWeight,
                AbsorptionCoefficientsRGB = dto.AbsorptionCoefficientsRGB,
                ScatteringCoefficientsRGB = dto.ScatteringCoefficientsRGB,
                QualityAndDepthLimits = dto.QualityAndDepthLimits
            };

            cursor++;
            if (cursor >= ring.Length)
                cursor = 0;
            cursorRef = cursor;
            return flags;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _tuningDirty = true;
        }
#endif

        private void RequestTelemetryDump()
        {
            if (!_dumped)
                _telemetryDumpPending = true;
        }

        private void FlushPendingTelemetryDump()
        {
            if (!_telemetryDumpPending || _dumped)
                return;

            _telemetryDumpPending = false;
            DumpTelemetryOnce();
            if (!_dumped)
                _telemetryDumpPending = true;
        }

        private void DumpTelemetryOnce()
        {
            if (_dumped)
                return;

            IDataVault vault = _vault;
            if (vault == null ||
                !TryResolve(vault, in _telemetryHandle, out NativeArray<WaterOpticsTelemetryEntry> ring) ||
                !TryResolve(vault, in _telemetryCursorHandle, out NativeArray<int> cursorArray) ||
                !ring.IsCreated)
            {
                return;
            }

            int cursor = cursorArray.IsCreated && cursorArray.Length > 0 ? ReadFirstInt(cursorArray) : 0;
            if ((uint)cursor >= (uint)ring.Length)
                cursor = 0;

            var header = new WaterOpticsDumpHeader
            {
                Magic = TelemetrySourceHash,
                Version = TelemetryDumpVersion,
                Capacity = ring.Length,
                RowSizeBytes = WaterOpticsNativeLayout.TelemetrySizeBytes,
                Cursor = cursor,
                Flags = 0u,
                Reserved = 0UL
            };

            uint hash = TelemetrySourceHash ^ (uint)TelemetryDumpVersion ^ (uint)ring.Length ^ (uint)cursor;
            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(ring);
            int byteCount = ring.Length * WaterOpticsNativeLayout.TelemetrySizeBytes;
            for (int i = 0; i < byteCount; i++)
                hash = (hash * 16777619u) ^ basePtr[i];

            int headerBytes = UnsafeUtility.SizeOf<WaterOpticsDumpHeader>();
            if (headerBytes != WaterOpticsNativeLayout.DumpHeaderSizeBytes ||
                byteCount > int.MaxValue - headerBytes)
            {
                GlobalTelemetryBus.PublishUnityLogFault(TelemetrySourceHash, 0u, 1u);
                return;
            }

            NativeArray<byte> payload = default;
            try
            {
                int payloadBytes = headerBytes + byteCount;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    payloadBytes,
                    nameof(WaterOpticsRuntime),
                    "waterOpticsTelemetryDumpPayload");
                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                UnsafeUtility.MemCpy(destination, &header, headerBytes);
                UnsafeUtility.MemCpy(destination + headerBytes, basePtr, byteCount);

                if (NativeFaultDumpWriter.TryWriteAll(TelemetryDumpRelativePath, payload, payloadBytes))
                {
                    _lastTelemetryDumpHash = hash ^ (uint)headerBytes ^ (uint)header.RowSizeBytes;
                    _dumped = true;
                }
                else
                {
                    GlobalTelemetryBus.PublishUnityLogFault(TelemetrySourceHash, 0u, 1u);
                }
            }
            catch (IOException)
            {
                GlobalTelemetryBus.PublishUnityLogFault(TelemetrySourceHash, 0u, 1u);
            }
            catch (UnauthorizedAccessException)
            {
                GlobalTelemetryBus.PublishUnityLogFault(TelemetrySourceHash, 0u, 1u);
            }
            catch (ArgumentException)
            {
                GlobalTelemetryBus.PublishUnityLogFault(TelemetrySourceHash, 0u, 1u);
            }
            catch (InvalidOperationException)
            {
                GlobalTelemetryBus.PublishUnityLogFault(TelemetrySourceHash, 0u, 1u);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(WaterOpticsRuntime),
                    "waterOpticsTelemetryDumpPayload");
            }
        }

        private bool TryColdBootstrapShaderParamsBuffers()
        {
            if (!_supportsConstantBuffers)
                return false;

            if (HasValidShaderParamsBuffers())
                return true;

            ReleaseGraphicsBuffer(ref _shaderParamsBufferA);
            ReleaseGraphicsBuffer(ref _shaderParamsBufferB);
            _shaderWriteIndex = 0;
            _activeShaderParamsBuffer = null;
            _hasUploadedDto = false;
            _lastUploadedDto = default;
            _shaderParamsBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, OpticsSizeBytes); // COLD ALLOC: GraphicsBuffer[1] - global water optics constant buffer A.
            _shaderParamsBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, OpticsSizeBytes); // COLD ALLOC: GraphicsBuffer[1] - global water optics constant buffer B.
            bool valid = HasValidShaderParamsBuffers();
            if (!valid)
            {
                ReleaseGraphicsBuffer(ref _shaderParamsBufferA);
                ReleaseGraphicsBuffer(ref _shaderParamsBufferB);
                _activeShaderParamsBuffer = null;
                _hasUploadedDto = false;
                _lastUploadedDto = default;
            }

            return valid;
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _supportsConstantBuffers = SystemInfo.supportsSetConstantBuffer;
        }

        private bool HasValidShaderParamsBuffers()
        {
            return _shaderParamsBufferA != null && _shaderParamsBufferA.IsValid() &&
                   _shaderParamsBufferB != null && _shaderParamsBufferB.IsValid();
        }

        private GraphicsBuffer ResolveNextShaderParamsBuffer()
        {
            _shaderWriteIndex ^= 1;
            return _shaderWriteIndex == 0 ? _shaderParamsBufferA : _shaderParamsBufferB;
        }

#if UNITY_EDITOR
        private static bool TryParseProfileRow(ReadOnlySpan<byte> line, out WaterOpticsProfileDTO profile)
        {
            profile = default;
            int cursor = 0;
            ReadOnlySpan<byte> id = NextField(line, ref cursor);
            uint hash = HashAsciiLower(id);
            if (hash == 0u)
                return false;

            profile.ProfileHash = hash;
            profile.Flags = 1u;
            profile.AbsorptionCoefficientsRGB = new float4(
                ParseFloat(NextField(line, ref cursor), 0.42f),
                ParseFloat(NextField(line, ref cursor), 0.105f),
                ParseFloat(NextField(line, ref cursor), 0.028f),
                ParseFloat(NextField(line, ref cursor), 1f));
            profile.ScatteringCoefficientsRGB = new float4(
                ParseFloat(NextField(line, ref cursor), 0.035f),
                ParseFloat(NextField(line, ref cursor), 0.09f),
                ParseFloat(NextField(line, ref cursor), 0.16f),
                ParseFloat(NextField(line, ref cursor), 0.42f));
            profile.DirectionalLightColorAndIntensity = new float4(
                ParseFloat(NextField(line, ref cursor), 0.09f),
                ParseFloat(NextField(line, ref cursor), 0.42f),
                ParseFloat(NextField(line, ref cursor), 0.70f),
                ParseFloat(NextField(line, ref cursor), 0.85f));
            profile.MaxDistanceMeters = math.max(1f, ParseFloat(NextField(line, ref cursor), 5000f));
            profile.QualityBias = math.clamp(ParseFloat(NextField(line, ref cursor), 0f), -1f, 1f);
            return true;
        }

        private static ReadOnlySpan<byte> ReadLine(ReadOnlySpan<byte> bytes, ref int cursor)
        {
            int start = cursor;
            while (cursor < bytes.Length && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                cursor++;
            int end = cursor;
            while (cursor < bytes.Length && (bytes[cursor] == (byte)'\n' || bytes[cursor] == (byte)'\r'))
                cursor++;
            return bytes.Slice(start, end - start);
        }

        private static ReadOnlySpan<byte> NextField(ReadOnlySpan<byte> line, ref int cursor)
        {
            int start = cursor;
            while (cursor < line.Length && line[cursor] != (byte)',')
                cursor++;
            int end = cursor;
            if (cursor < line.Length && line[cursor] == (byte)',')
                cursor++;
            ReadOnlySpan<byte> field = line.Slice(start, end - start);
            Trim(ref field);
            return field;
        }

        private static bool IsHeader(ReadOnlySpan<byte> line)
        {
            if (line.Length < 7)
                return false;

            return ToLowerAscii(line[0]) == (byte)'p' &&
                   ToLowerAscii(line[1]) == (byte)'r' &&
                   ToLowerAscii(line[2]) == (byte)'o' &&
                   ToLowerAscii(line[3]) == (byte)'f' &&
                   ToLowerAscii(line[4]) == (byte)'i' &&
                   ToLowerAscii(line[5]) == (byte)'l' &&
                   ToLowerAscii(line[6]) == (byte)'e';
        }

        private static void Trim(ref ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start < value.Length && IsWhite(value[start]))
                start++;
            while (end >= start && IsWhite(value[end]))
                end--;
            value = start > end ? ReadOnlySpan<byte>.Empty : value.Slice(start, end - start + 1);
        }

        private static bool IsWhite(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t';
        }

        private static float ParseFloat(ReadOnlySpan<byte> value, float fallback)
        {
            Trim(ref value);
            if (value.Length == 0)
                return fallback;

            int index = 0;
            float sign = 1f;
            if (value[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }

            float result = 0f;
            bool any = false;
            while (index < value.Length && value[index] >= (byte)'0' && value[index] <= (byte)'9')
            {
                result = result * 10f + value[index] - (byte)'0';
                index++;
                any = true;
            }

            if (index < value.Length && value[index] == (byte)'.')
            {
                index++;
                float scale = 0.1f;
                while (index < value.Length && value[index] >= (byte)'0' && value[index] <= (byte)'9')
                {
                    result += (value[index] - (byte)'0') * scale;
                    scale *= 0.1f;
                    index++;
                    any = true;
                }
            }

            float parsed = result * sign;
            return any && math.isfinite(parsed) ? parsed : fallback;
        }

        private static uint HashAsciiLower(ReadOnlySpan<byte> value)
        {
            uint hash = 2166136261u;
            bool any = false;
            for (int i = 0; i < value.Length; i++)
            {
                byte c = ToLowerAscii(value[i]);
                if (c == (byte)' ' || c == (byte)'\t')
                    continue;
                hash ^= c;
                hash *= 16777619u;
                any = true;
            }

            return any ? hash : 0u;
        }

        private static byte ToLowerAscii(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }
#endif

        private static string ResolveProjectRoot()
        {
            string root = Directory.GetCurrentDirectory();
            if (LooksLikeProjectRoot(root))
                return root;

            string child = Path.Combine(root, "Hecton8");
            return LooksLikeProjectRoot(child) ? child : root;
        }

        private static bool LooksLikeProjectRoot(string path)
        {
            return Directory.Exists(Path.Combine(path, "Assets")) &&
                   Directory.Exists(Path.Combine(path, "ProjectSettings"));
        }

        private static void ClearBuffer<T>(IDataVault vault, in VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault == null || !TryResolve(vault, in handle, out NativeArray<T> buffer) || !buffer.IsCreated)
                return;
            UnsafeUtility.MemClear(NativeArrayUnsafeUtility.GetUnsafePtr(buffer), UnsafeUtility.SizeOf<T>() * buffer.Length);
        }

        private static WaterOpticsDTO ReadFirstWaterOpticsDto(NativeArray<WaterOpticsDTO> buffer)
        {
            void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(buffer);
            return UnsafeUtility.AsRef<WaterOpticsDTO>(ptr);
        }

        private static void WriteFirstWaterOpticsDto(NativeArray<WaterOpticsDTO> buffer, in WaterOpticsDTO dto)
        {
            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(buffer);
            ref WaterOpticsDTO target = ref UnsafeUtility.AsRef<WaterOpticsDTO>(ptr);
            target = dto;
        }

        private static WaterOpticsTuningDTO ReadFirstTuningDto(NativeArray<WaterOpticsTuningDTO> buffer)
        {
            void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(buffer);
            return UnsafeUtility.AsRef<WaterOpticsTuningDTO>(ptr);
        }

        private static WaterOpticsProfileDTO ReadProfileAt(NativeArray<WaterOpticsProfileDTO> buffer, int index)
        {
            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(buffer);
            return UnsafeUtility.AsRef<WaterOpticsProfileDTO>(
                ptr + index * WaterOpticsNativeLayout.ProfileSizeBytes);
        }

        private static WaterOpticsTelemetryEntry ReadTelemetryEntryAt(NativeArray<WaterOpticsTelemetryEntry> buffer, int index)
        {
            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(buffer);
            return UnsafeUtility.AsRef<WaterOpticsTelemetryEntry>(
                ptr + index * WaterOpticsNativeLayout.TelemetrySizeBytes);
        }

        private static int ReadFirstInt(NativeArray<int> buffer)
        {
            void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(buffer);
            return UnsafeUtility.AsRef<int>(ptr);
        }

        private static bool TryResolve<T>(IDataVault vault, in VaultGenerationHandle<T> handle, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null && handle.BufferID != 0u && handle.Generation != 0u && vault.TryResolveHandle(in handle, out buffer) && buffer.IsCreated;
        }

        private static bool TryReadOnly<T>(IDataVault vault, in VaultGenerationHandle<T> handle, out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   handle.BufferID != 0u &&
                   handle.Generation != 0u &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        private static void ReleaseVaultBuffer<T>(IDataVault vault, in VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault != null && handle.BufferID != 0u && handle.Generation != 0u)
                vault.ReleaseBuffer(in handle);
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private sealed class VisualSyncUploadSystem : IDispatcherSystem
        {
            private readonly WaterOpticsRuntime _owner;

            public VisualSyncUploadSystem(WaterOpticsRuntime owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => VisualSystemHash;
            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.VisualSync;
            public byte GetBucketId() => 0;
            public int GetDependencyCount() => 0;
            public uint GetDependencyHash(int dependencyIndex) => 0u;
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) => dependsOn;
            public void PostSimulationTick(in DispatcherTimingDTO timing) { }
            public void VisualSyncTick(in DispatcherTimingDTO timing)
            {
                _owner?.VisualSyncTick(in timing);
            }
        }
    }
}
