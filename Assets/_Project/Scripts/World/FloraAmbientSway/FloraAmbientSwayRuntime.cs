using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using AOT;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Hecton8.World.FloraAmbientSway
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct FloraSwayParamsDTO
    {
        [FieldOffset(0)] public float4 GlobalFlowVector;
        [FieldOffset(16)] public float4 SwayMathParams;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct FloraAmbientFlowStateDTO
    {
        [FieldOffset(0)] public float4 FlowDirectionSpeed;
        [FieldOffset(16)] public float4 SourceAndFrame;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct FloraSwayTuningDTO
    {
        [FieldOffset(0)] public float GlobalAmplitudeMeters;
        [FieldOffset(4)] public float Frequency;
        [FieldOffset(8)] public float PhaseSpatialOffset;
        [FieldOffset(12)] public float AlphaClip;
        [FieldOffset(16)] public float MockFlowSpeed;
        [FieldOffset(20)] public float MockFlowIntensity;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint ProfileHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct FloraBiomeSwayProfileDTO
    {
        [FieldOffset(0)] public uint BiomeHash;
        [FieldOffset(4)] public float GlobalAmplitudeMeters;
        [FieldOffset(8)] public float Frequency;
        [FieldOffset(12)] public float PhaseSpatialOffset;
        [FieldOffset(16)] public float AlphaClip;
        [FieldOffset(20)] public float MockFlowIntensity;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint StateHash;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
    public struct SwayTelemetryEntry
    {
        [System.Runtime.InteropServices.FieldOffset(0)]
        public uint Frame;
        [System.Runtime.InteropServices.FieldOffset(4)]
        public uint Flags;
        [System.Runtime.InteropServices.FieldOffset(8)]
        public float WrappedTime;
        [System.Runtime.InteropServices.FieldOffset(12)]
        public float FlowMagnitude;
        [System.Runtime.InteropServices.FieldOffset(16)]
        public float GlobalQualityWeight;
        [System.Runtime.InteropServices.FieldOffset(20)]
        public float AmplitudeMeters;
        [System.Runtime.InteropServices.FieldOffset(24)]
        public uint StateHash;
        [System.Runtime.InteropServices.FieldOffset(28)]
        public uint SourceHash;
        [System.Runtime.InteropServices.FieldOffset(32)]
        private byte _pad0;
        [System.Runtime.InteropServices.FieldOffset(33)]
        private byte _pad1;
        [System.Runtime.InteropServices.FieldOffset(34)]
        private byte _pad2;
        [System.Runtime.InteropServices.FieldOffset(35)]
        private byte _pad3;
        [System.Runtime.InteropServices.FieldOffset(36)]
        private byte _pad4;
        [System.Runtime.InteropServices.FieldOffset(37)]
        private byte _pad5;
        [System.Runtime.InteropServices.FieldOffset(38)]
        private byte _pad6;
        [System.Runtime.InteropServices.FieldOffset(39)]
        private byte _pad7;
        [System.Runtime.InteropServices.FieldOffset(40)]
        private byte _pad8;
        [System.Runtime.InteropServices.FieldOffset(41)]
        private byte _pad9;
        [System.Runtime.InteropServices.FieldOffset(42)]
        private byte _pad10;
        [System.Runtime.InteropServices.FieldOffset(43)]
        private byte _pad11;
        [System.Runtime.InteropServices.FieldOffset(44)]
        private byte _pad12;
        [System.Runtime.InteropServices.FieldOffset(45)]
        private byte _pad13;
        [System.Runtime.InteropServices.FieldOffset(46)]
        private byte _pad14;
        [System.Runtime.InteropServices.FieldOffset(47)]
        private byte _pad15;
        [System.Runtime.InteropServices.FieldOffset(48)]
        private byte _pad16;
        [System.Runtime.InteropServices.FieldOffset(49)]
        private byte _pad17;
        [System.Runtime.InteropServices.FieldOffset(50)]
        private byte _pad18;
        [System.Runtime.InteropServices.FieldOffset(51)]
        private byte _pad19;
        [System.Runtime.InteropServices.FieldOffset(52)]
        private byte _pad20;
        [System.Runtime.InteropServices.FieldOffset(53)]
        private byte _pad21;
        [System.Runtime.InteropServices.FieldOffset(54)]
        private byte _pad22;
        [System.Runtime.InteropServices.FieldOffset(55)]
        private byte _pad23;
        [System.Runtime.InteropServices.FieldOffset(56)]
        private byte _pad24;
        [System.Runtime.InteropServices.FieldOffset(57)]
        private byte _pad25;
        [System.Runtime.InteropServices.FieldOffset(58)]
        private byte _pad26;
        [System.Runtime.InteropServices.FieldOffset(59)]
        private byte _pad27;
        [System.Runtime.InteropServices.FieldOffset(60)]
        private byte _pad28;
        [System.Runtime.InteropServices.FieldOffset(61)]
        private byte _pad29;
        [System.Runtime.InteropServices.FieldOffset(62)]
        private byte _pad30;
        [System.Runtime.InteropServices.FieldOffset(63)]
        private byte _pad31;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockAmbientFlowJob : IJob
    {
        [NoAlias] public NativeArray<FloraAmbientFlowStateDTO> FlowState;
        public float WrappedTime;
        public float MockSpeed;
        public float MockIntensity;
        public uint Frame;

        public void Execute()
        {
            if (!FlowState.IsCreated || FlowState.Length == 0)
                return;

            float speed = math.max(0.001f, SanitizeFinite(MockSpeed, 0.18f));
            float intensity = math.max(0f, SanitizeFinite(MockIntensity, 0.75f));
            float phase = WrappedTime * speed;
            float3 rawDirection = math.float3(
                MathLodApproximation.ApproxSinBhaskara(phase),
                0.08f * MathLodApproximation.ApproxSinBhaskara(phase * 0.37f),
                MathLodApproximation.ApproxCosBhaskara(phase * 0.73f));
            float lengthSq = math.lengthsq(rawDirection);
            float3 direction = rawDirection * math.rsqrt(math.max(lengthSq, 0.0001f));
            if (!math.all(math.isfinite(direction)) || lengthSq < 0.0001f)
                direction = math.float3(1f, 0f, 0f);

            FloraAmbientFlowStateDTO state = default;
            state.FlowDirectionSpeed = math.float4(direction, speed * intensity);
            state.SourceAndFrame = math.float4((float)Frame, (float)0x4D4F434Bu, intensity, 0f);
            void* flowPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(FlowState);
            UnsafeUtility.AsRef<FloraAmbientFlowStateDTO>(flowPtr) = state;
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct CalculateFloraSwayParametersJob : IJob
    {
        [NoAlias] public NativeArray<FloraSwayParamsDTO> Params;
        [ReadOnly, NoAlias] public NativeArray<FloraAmbientFlowStateDTO>.ReadOnly FlowState;
        [ReadOnly, NoAlias] public NativeArray<FloraSwayTuningDTO>.ReadOnly Tuning;
        public float DeltaTime;
        public float GlobalQualityWeight;

        public void Execute()
        {
            if (!Params.IsCreated || Params.Length == 0 || !Tuning.IsCreated || Tuning.Length == 0)
                return;

            void* paramsPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Params);
            FloraSwayParamsDTO previous = UnsafeUtility.AsRef<FloraSwayParamsDTO>(paramsPtr);
            FloraSwayTuningDTO tuning = Tuning[0];
            float dt = math.clamp(SanitizeFinite(DeltaTime, 0f), 0f, 0.1f);
            float quality = math.saturate(SanitizeFinite(GlobalQualityWeight, 0f));
            float amplitude = math.max(0f, SanitizeFinite(tuning.GlobalAmplitudeMeters, 0.42f));
            float frequency = math.max(0.001f, SanitizeFinite(tuning.Frequency, 1.1f));
            float phaseSpatialOffset = SanitizeFinite(tuning.PhaseSpatialOffset, 0.85f);
            float3 flow = math.float3(1f, 0f, 0f);
            float flowSpeed = math.max(0.001f, SanitizeFinite(tuning.MockFlowSpeed, 0.18f));

            if (FlowState.IsCreated && FlowState.Length > 0)
            {
                float4 state = FlowState[0].FlowDirectionSpeed;
                if (math.all(math.isfinite(state)))
                {
                    float3 stateDirection = math.float3(state.x, state.y, state.z);
                    float lengthSq = math.lengthsq(stateDirection);
                    if (lengthSq > 0.0001f)
                        flow = stateDirection * math.rsqrt(math.max(lengthSq, 0.0001f));
                    flowSpeed = math.max(0.001f, math.abs(state.w));
                }
            }

            float wrapped = math.fmod(SanitizeFinite(previous.SwayMathParams.x, 0f) + dt * flowSpeed, 1000f);
            if (wrapped < 0f)
                wrapped += 1000f;

            float effectiveSpatialFrequency = frequency * math.max(0f, phaseSpatialOffset);
            FloraSwayParamsDTO next = default;
            next.GlobalFlowVector = math.float4(flow, flowSpeed);
            next.SwayMathParams = math.float4(wrapped, amplitude, effectiveSpatialFrequency, quality);
            UnsafeUtility.AsRef<FloraSwayParamsDTO>(paramsPtr) = next;
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void GenerateMockAmbientFlowKernelDelegate(GenerateMockAmbientFlowJob* job);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void CalculateFloraSwayParametersKernelDelegate(CalculateFloraSwayParametersJob* job);

    internal static unsafe class FloraAmbientSwayBurstKernelEntrypoints
    {
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        [MonoPInvokeCallback(typeof(GenerateMockAmbientFlowKernelDelegate))]
        internal static void GenerateMockAmbientFlow(GenerateMockAmbientFlowJob* job)
        {
            if (job == null)
                return;

            ref GenerateMockAmbientFlowJob jobRef = ref UnsafeUtility.AsRef<GenerateMockAmbientFlowJob>(job);
            jobRef.Execute();
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        [MonoPInvokeCallback(typeof(CalculateFloraSwayParametersKernelDelegate))]
        internal static void CalculateFloraSwayParameters(CalculateFloraSwayParametersJob* job)
        {
            if (job == null)
                return;

            ref CalculateFloraSwayParametersJob jobRef = ref UnsafeUtility.AsRef<CalculateFloraSwayParametersJob>(job);
            jobRef.Execute();
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-96)]
    public sealed unsafe class FloraAmbientSwayRuntime : MonoBehaviour, IDispatcherSystem, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private const string RuntimeHostName = "H8_FloraAmbientSwayRuntime";
        public const int FloraSwayParamsSizeBytes = 32;
        public const int SwayTelemetryCapacity = 300;
        public const int BiomeProfileCapacity = 64;
        public const int CsvScratchBytes = 64 * 1024;
        public const uint SystemHash = 0x53483236u;
        private const int FloraAmbientFlowStateSizeBytes = 32;
        private const int FloraSwayTuningSizeBytes = 32;
        private const int FloraBiomeSwayProfileSizeBytes = 32;

        private const BufferID FloraAmbientSwayParamsBufferId = BufferID.FloraAmbientSwayRuntime_FloraAmbientSwayParamsBufferId;
        private const BufferID FloraAmbientSwayFlowStateBufferId = BufferID.FloraAmbientSwayRuntime_FloraAmbientSwayFlowStateBufferId;
        private const BufferID FloraAmbientSwayTelemetryRingBufferId = BufferID.FloraAmbientSwayRuntime_FloraAmbientSwayTelemetryRingBufferId;
        private const BufferID FloraAmbientSwayTelemetryCursorBufferId = BufferID.FloraAmbientSwayRuntime_FloraAmbientSwayTelemetryCursorBufferId;
        private const BufferID FloraAmbientSwayTuningBufferId = BufferID.FloraAmbientSwayRuntime_FloraAmbientSwayTuningBufferId;
        private const BufferID FloraAmbientSwayBiomeProfilesBufferId = BufferID.FloraAmbientSwayRuntime_FloraAmbientSwayBiomeProfilesBufferId;
        private const BufferID FloraAmbientSwayCsvScratchBufferId = BufferID.FloraAmbientSwayRuntime_FloraAmbientSwayCsvScratchBufferId;
        private static readonly ulong TelemetryMutationGuardMask =
            FloraAmbientSwayMutationGuardBit(FloraAmbientSwayTelemetryRingBufferId) |
            FloraAmbientSwayMutationGuardBit(FloraAmbientSwayTelemetryCursorBufferId);
        private static readonly ulong ProfileCsvMutationGuardMask =
            FloraAmbientSwayMutationGuardBit(FloraAmbientSwayBiomeProfilesBufferId) |
            FloraAmbientSwayMutationGuardBit(FloraAmbientSwayCsvScratchBufferId);

        private const uint TuningFlagMockFlowEnabled = 1u << 0;
        private const uint TelemetryFlagVaultMissing = 1u << 0;
        private const uint TelemetryFlagConstantBufferUnsupported = 1u << 1;
        private const uint TelemetryFlagInvalidNumber = 1u << 2;
        private const uint TelemetryFlagUploadSkipped = 1u << 3;
        private const uint TelemetryFlagBurstKernelUnavailable = 1u << 4;
        private const uint TelemetrySourceHash = 0x53465759u;
        private const uint TelemetryDumpMagic = 0x37363253u; // "S267" little-endian bytes.
        private const uint TelemetryDumpVersion = 1u;
        private const uint SwayTelemetryEntrySizeBytes = 64u;

        private static int s_runtimeClaimed;
        private static FunctionPointer<GenerateMockAmbientFlowKernelDelegate> s_generateMockKernel;
        private static FunctionPointer<CalculateFloraSwayParametersKernelDelegate> s_calculateKernel;

        [SerializeField, Range(0f, 2f)] private float _globalAmplitudeMeters = 0.42f;
        [SerializeField, Range(0.001f, 8f)] private float _frequency = 1.1f;
        [SerializeField, Range(0f, 4f)] private float _phaseSpatialOffset = 0.85f;
        [SerializeField, Range(0f, 1f)] private float _alphaClip = 0.08f;
        [SerializeField, Range(0.001f, 2f)] private float _mockFlowSpeed = 0.18f;
        [SerializeField, Range(0f, 2f)] private float _mockFlowIntensity = 0.75f;
        [SerializeField] private bool _mockFlowEnabled = true;
        [SerializeField] private bool _loadBiomeProfilesOnEnable = true;

        private IDataVault _vault;
        private VaultGenerationHandle<FloraSwayParamsDTO> _paramsHandle;
        private VaultGenerationHandle<FloraAmbientFlowStateDTO> _flowStateHandle;
        private VaultGenerationHandle<FloraSwayTuningDTO> _tuningHandle;
        private VaultGenerationHandle<FloraBiomeSwayProfileDTO> _profileHandle;
        private VaultGenerationHandle<SwayTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;

        private GraphicsBuffer _shaderParamsBufferA;
        private GraphicsBuffer _shaderParamsBufferB;
        private GraphicsBuffer _activeShaderParamsBuffer;
        private VisualSyncUploadSystem _visualSyncSystem;
        private int _shaderWriteIndex;
        private bool _registered;
        private bool _visualRegistered;
        private bool _hotSwapRegistered;
        private bool _dumped;
        private bool _vaultReady;
        private bool _tuningDirty = true;
        private uint _fallbackFrameCounter;
        private uint _lastResolvedFrame;
        private int _runtimeClaimHeld;
        private bool _supportsConstantBuffers;

        private static readonly int GlobalFloraSwayCBufferId = Shader.PropertyToID("_GlobalFloraSway");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeClaim()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Volatile.Write(ref s_runtimeClaimed, 0);
            s_generateMockKernel = default;
            s_calculateKernel = default;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureSceneRuntime()
        {
            if (!Application.isPlaying)
                return;

            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName == "00_BOOTSTRAP" || sceneName == "01_MAIN_MENU" || sceneName == "01_ORBIT")
                return;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureSceneRuntimeInstance();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Application.isPlaying)
                EnsureSceneRuntimeInstance();
        }

        private static void EnsureSceneRuntimeInstance()
        {
            if (Volatile.Read(ref s_runtimeClaimed) != 0)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // H8Debug.LogWarning("[FloraAmbientSwayRuntime] Missing authored scene runtime. Add FloraAmbientSwayRuntime to the biome scene instead of relying on runtime host creation.");
#endif
        }

        public uint GetSystemIdHash() => SystemHash;
        public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.PreSimulation;
        public byte GetBucketId() => 0;
        public int GetDependencyCount() => 0;
        public uint GetDependencyHash(int dependencyIndex) => 0u;

        public float GlobalAmplitudeMetersForEditor => _globalAmplitudeMeters;
        public float FrequencyForEditor => _frequency;
        public float PhaseSpatialOffsetForEditor => _phaseSpatialOffset;
        public float AlphaClipForEditor => _alphaClip;
        public bool MockFlowEnabledForEditor => _mockFlowEnabled;

        private void OnEnable()
        {
            if (!TryClaimRuntime())
            {
                enabled = false;
                return;
            }

            CacheGraphicsCapabilitiesCold();
            TryColdBootstrapVault();

            _visualSyncSystem = new VisualSyncUploadSystem(this); // COLD ALLOC: VisualSyncUploadSystem[1] - dispatcher phase adapter - owner: FloraAmbientSwayRuntime.
            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
            TryRegisterDispatcherSystems();
        }

        private void Start()
        {
            CacheGraphicsCapabilitiesCold();
            if (!_vaultReady)
                TryColdBootstrapVault();
        }

        private void OnValidate()
        {
            _tuningDirty = true;
        }

        private void OnDisable()
        {
            OnServiceShutdown();
        }

        private void OnDestroy()
        {
            OnServiceShutdown();
        }

        public void OnServiceShutdown()
        {
            TryUnregisterDispatcherSystems();

            if (_hotSwapRegistered)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(this);
                _hotSwapRegistered = false;
            }

            _visualSyncSystem = null;
            ReleaseGraphicsBuffer(ref _shaderParamsBufferA);
            ReleaseGraphicsBuffer(ref _shaderParamsBufferB);
            _activeShaderParamsBuffer = null;

            IDataVault vault = _vault;
            if (vault != null)
                ReleaseOwnedVaultBuffers(vault);

            _vault = null;
            _vaultReady = false;
            _tuningDirty = true;
            _fallbackFrameCounter = 0u;
            _lastResolvedFrame = 0u;
            ReleaseRuntimeClaim();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregisterDispatcherSystems();
                if (currentService != null)
                    TryRegisterDispatcherSystems();
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            IDataVault previousVault = _vault;
            IDataVault currentVault = currentService as IDataVault;
            if (currentVault != null && ReferenceEquals(previousVault, currentVault))
            {
                TryColdBootstrapVault();
                return;
            }

            if (previousVault != null && !ReferenceEquals(previousVault, currentVault))
                ReleaseOwnedVaultBuffers(previousVault);

            _vault = currentVault;
            _vaultReady = false;
            _dumped = false;
            _tuningDirty = true;
            if (currentVault != null)
                TryColdBootstrapVault();
        }

        private void TryRegisterDispatcherSystems()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (_visualSyncSystem == null)
                _visualSyncSystem = new VisualSyncUploadSystem(this); // COLD ALLOC: VisualSyncUploadSystem[1] - dispatcher phase adapter - owner: FloraAmbientSwayRuntime.

            if (!_registered && GlobalRegistry.TryRegisterDispatcherSystem(this))
                _registered = true;
            if (!_visualRegistered && GlobalRegistry.TryRegisterDispatcherSystem(_visualSyncSystem))
                _visualRegistered = true;
        }

        private void TryUnregisterDispatcherSystems()
        {
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
        }

        private bool TryClaimRuntime()
        {
            if (Interlocked.CompareExchange(ref s_runtimeClaimed, 1, 0) != 0)
                return false;

            _runtimeClaimHeld = 1;
            return true;
        }

        private void ReleaseRuntimeClaim()
        {
            if (_runtimeClaimHeld == 0)
                return;

            _runtimeClaimHeld = 0;
            Volatile.Write(ref s_runtimeClaimed, 0);
        }

        public void PreSimulationTick(in DispatcherTimingDTO timing)
        {
            uint frame = AdvanceFrameId(in timing);
            IDataVault vault = _vault;
            if (vault == null || !_vaultReady)
            {
                RecordTelemetry(frame, TelemetryFlagVaultMissing, default);
                return;
            }

            WriteTuningToVault(force: false);

            if (!TryRead(vault, in _paramsHandle, out NativeArray<FloraSwayParamsDTO>.ReadOnly currentParameters) ||
                !currentParameters.IsCreated ||
                currentParameters.Length == 0)
            {
                RecordTelemetry(frame, TelemetryFlagVaultMissing, default);
                return;
            }

            float deltaTime = ResolveDeltaTime(in timing);
            float currentTime = currentParameters[0].SwayMathParams.x;
            if (_mockFlowEnabled)
            {
                if (!TryAcquireWrite(vault, in _flowStateHandle, out NativeArray<FloraAmbientFlowStateDTO> writableFlowState))
                {
                    RecordTelemetry(frame, TelemetryFlagVaultMissing, default);
                    return;
                }

                try
                {
                    GenerateMockAmbientFlowJob mockFlowJob = default;
                    mockFlowJob.FlowState = writableFlowState;
                    mockFlowJob.WrappedTime = currentTime;
                    mockFlowJob.MockSpeed = _mockFlowSpeed;
                    mockFlowJob.MockIntensity = _mockFlowIntensity;
                    mockFlowJob.Frame = frame;
                    if (!RunMockAmbientFlowKernel(mockFlowJob))
                    {
                        RecordTelemetry(frame, TelemetryFlagBurstKernelUnavailable, default);
                        return;
                    }
                }
                finally
                {
                    vault.ReleaseWriteLock(in _flowStateHandle, SystemID.FloraGenomics);
                }
            }

            if (!TryRead(vault, in _flowStateHandle, out NativeArray<FloraAmbientFlowStateDTO>.ReadOnly flowState) ||
                !TryRead(vault, in _tuningHandle, out NativeArray<FloraSwayTuningDTO>.ReadOnly tuning) ||
                flowState.Length == 0 ||
                tuning.Length == 0)
            {
                RecordTelemetry(frame, TelemetryFlagVaultMissing, default);
                return;
            }

            if (!TryAcquireWrite(vault, in _paramsHandle, out NativeArray<FloraSwayParamsDTO> parameters))
            {
                RecordTelemetry(frame, TelemetryFlagVaultMissing, default);
                return;
            }

            uint telemetryFlags = TelemetryFlagVaultMissing;
            FloraSwayParamsDTO telemetryDto = default;
            bool dumpTelemetry = false;
            try
            {
                if (parameters.IsCreated && parameters.Length > 0)
                {
                    CalculateFloraSwayParametersJob parametersJob = default;
                    parametersJob.Params = parameters;
                    parametersJob.FlowState = flowState;
                    parametersJob.Tuning = tuning;
                    parametersJob.DeltaTime = deltaTime;
                    parametersJob.GlobalQualityWeight = ResolveGlobalQualityWeight();
                    if (RunCalculateFloraSwayParametersKernel(parametersJob))
                    {
                        telemetryDto = parameters[0];
                        telemetryFlags = ValidateParams(in telemetryDto) ? 0u : TelemetryFlagInvalidNumber;
                        dumpTelemetry = (telemetryFlags & TelemetryFlagInvalidNumber) != 0u;
                    }
                    else
                    {
                        telemetryFlags = TelemetryFlagBurstKernelUnavailable;
                    }
                }
            }
            finally
            {
                vault.ReleaseWriteLock(in _paramsHandle, SystemID.FloraGenomics);
            }

            RecordTelemetry(frame, telemetryFlags, in telemetryDto);
            if (dumpTelemetry)
                DumpTelemetryOnce();
        }

        public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn)
        {
            return dependsOn;
        }

        public void PostSimulationTick(in DispatcherTimingDTO timing)
        {
        }

        public void VisualSyncTick(in DispatcherTimingDTO timing)
        {
            IDataVault vault = _vault;
            if (vault == null ||
                !TryRead(vault, in _paramsHandle, out NativeArray<FloraSwayParamsDTO>.ReadOnly parameters) ||
                !parameters.IsCreated ||
                parameters.Length == 0)
            {
                RecordTelemetry(AdvanceVisualFrameId(in timing), TelemetryFlagVaultMissing, default);
                return;
            }

            FloraSwayParamsDTO dto = parameters[0];
            if (!_supportsConstantBuffers)
            {
                ReleaseGraphicsBuffer(ref _shaderParamsBufferA);
                ReleaseGraphicsBuffer(ref _shaderParamsBufferB);
                RecordTelemetry(AdvanceVisualFrameId(in timing), TelemetryFlagConstantBufferUnsupported, in dto);
                return;
            }

            if (!ShaderParamsBuffersReady())
            {
                RecordTelemetry(AdvanceVisualFrameId(in timing), TelemetryFlagUploadSkipped, in dto);
                return;
            }

            GraphicsBuffer writeBuffer = AdvanceShaderParamsBuffer();
            NativeArray<FloraSwayParamsDTO> mapped = writeBuffer.LockBufferForWrite<FloraSwayParamsDTO>(0, 1);
            try
            {
                mapped[0] = dto;
            }
            finally
            {
                writeBuffer.UnlockBufferAfterWrite<FloraSwayParamsDTO>(1);
            }

            _activeShaderParamsBuffer = writeBuffer;
            Shader.SetGlobalConstantBuffer(GlobalFloraSwayCBufferId, _activeShaderParamsBuffer, 0, FloraSwayParamsSizeBytes);
        }

        public void ApplyEditorTuning(
            float globalAmplitudeMeters,
            float frequency,
            float phaseSpatialOffset,
            float alphaClip,
            bool mockFlowEnabled)
        {
            _globalAmplitudeMeters = Mathf.Clamp(globalAmplitudeMeters, 0f, 2f);
            _frequency = Mathf.Clamp(frequency, 0.001f, 8f);
            _phaseSpatialOffset = Mathf.Clamp(phaseSpatialOffset, 0f, 4f);
            _alphaClip = Mathf.Clamp01(alphaClip);
            _mockFlowEnabled = mockFlowEnabled;
            _tuningDirty = true;
            WriteTuningToVault(force: true);
        }

        public bool TryReadLatestParams(out FloraSwayParamsDTO dto)
        {
            dto = default;
            IDataVault vault = _vault;
            if (vault == null || !TryRead(vault, in _paramsHandle, out NativeArray<FloraSwayParamsDTO>.ReadOnly parameters) ||
                !parameters.IsCreated || parameters.Length == 0)
            {
                return false;
            }

            dto = parameters[0];
            return true;
        }

        public static bool ValidateFloraSwayLayouts(out int paramsSize, out int telemetrySize, out int profileSize)
        {
            return ValidateFloraSwayLayouts(out paramsSize, out _, out _, out telemetrySize, out profileSize);
        }

        public static bool ValidateFloraSwayLayouts(out int paramsSize, out int flowSize, out int tuningSize, out int telemetrySize, out int profileSize)
        {
            paramsSize = UnsafeUtility.SizeOf<FloraSwayParamsDTO>();
            flowSize = UnsafeUtility.SizeOf<FloraAmbientFlowStateDTO>();
            tuningSize = UnsafeUtility.SizeOf<FloraSwayTuningDTO>();
            telemetrySize = UnsafeUtility.SizeOf<SwayTelemetryEntry>();
            profileSize = UnsafeUtility.SizeOf<FloraBiomeSwayProfileDTO>();
            return paramsSize == FloraSwayParamsSizeBytes &&
                   UnsafeUtility.AlignOf<FloraSwayParamsDTO>() >= 4 &&
                   flowSize == FloraAmbientFlowStateSizeBytes &&
                   UnsafeUtility.AlignOf<FloraAmbientFlowStateDTO>() >= 4 &&
                   tuningSize == FloraSwayTuningSizeBytes &&
                   UnsafeUtility.AlignOf<FloraSwayTuningDTO>() >= 4 &&
                   telemetrySize == (int)SwayTelemetryEntrySizeBytes &&
                   UnsafeUtility.AlignOf<SwayTelemetryEntry>() >= 4 &&
                   profileSize == FloraBiomeSwayProfileSizeBytes &&
                   UnsafeUtility.AlignOf<FloraBiomeSwayProfileDTO>() >= 4 &&
                   GetFieldOffset<FloraSwayParamsDTO>(nameof(FloraSwayParamsDTO.GlobalFlowVector)) == 0 &&
                   GetFieldOffset<FloraSwayParamsDTO>(nameof(FloraSwayParamsDTO.SwayMathParams)) == 16 &&
                   GetFieldOffset<FloraAmbientFlowStateDTO>(nameof(FloraAmbientFlowStateDTO.FlowDirectionSpeed)) == 0 &&
                   GetFieldOffset<FloraAmbientFlowStateDTO>(nameof(FloraAmbientFlowStateDTO.SourceAndFrame)) == 16 &&
                   GetFieldOffset<FloraSwayTuningDTO>(nameof(FloraSwayTuningDTO.GlobalAmplitudeMeters)) == 0 &&
                   GetFieldOffset<FloraSwayTuningDTO>(nameof(FloraSwayTuningDTO.Frequency)) == 4 &&
                   GetFieldOffset<FloraSwayTuningDTO>(nameof(FloraSwayTuningDTO.PhaseSpatialOffset)) == 8 &&
                   GetFieldOffset<FloraSwayTuningDTO>(nameof(FloraSwayTuningDTO.AlphaClip)) == 12 &&
                   GetFieldOffset<FloraSwayTuningDTO>(nameof(FloraSwayTuningDTO.MockFlowSpeed)) == 16 &&
                   GetFieldOffset<FloraSwayTuningDTO>(nameof(FloraSwayTuningDTO.MockFlowIntensity)) == 20 &&
                   GetFieldOffset<FloraSwayTuningDTO>(nameof(FloraSwayTuningDTO.Flags)) == 24 &&
                   GetFieldOffset<FloraSwayTuningDTO>(nameof(FloraSwayTuningDTO.ProfileHash)) == 28 &&
                   GetFieldOffset<FloraBiomeSwayProfileDTO>(nameof(FloraBiomeSwayProfileDTO.BiomeHash)) == 0 &&
                   GetFieldOffset<FloraBiomeSwayProfileDTO>(nameof(FloraBiomeSwayProfileDTO.GlobalAmplitudeMeters)) == 4 &&
                   GetFieldOffset<FloraBiomeSwayProfileDTO>(nameof(FloraBiomeSwayProfileDTO.Frequency)) == 8 &&
                   GetFieldOffset<FloraBiomeSwayProfileDTO>(nameof(FloraBiomeSwayProfileDTO.PhaseSpatialOffset)) == 12 &&
                   GetFieldOffset<FloraBiomeSwayProfileDTO>(nameof(FloraBiomeSwayProfileDTO.AlphaClip)) == 16 &&
                   GetFieldOffset<FloraBiomeSwayProfileDTO>(nameof(FloraBiomeSwayProfileDTO.MockFlowIntensity)) == 20 &&
                   GetFieldOffset<FloraBiomeSwayProfileDTO>(nameof(FloraBiomeSwayProfileDTO.Flags)) == 24 &&
                   GetFieldOffset<FloraBiomeSwayProfileDTO>(nameof(FloraBiomeSwayProfileDTO.StateHash)) == 28 &&
                   GetFieldOffset<SwayTelemetryEntry>(nameof(SwayTelemetryEntry.Frame)) == 0 &&
                   GetFieldOffset<SwayTelemetryEntry>(nameof(SwayTelemetryEntry.Flags)) == 4 &&
                   GetFieldOffset<SwayTelemetryEntry>(nameof(SwayTelemetryEntry.WrappedTime)) == 8 &&
                   GetFieldOffset<SwayTelemetryEntry>(nameof(SwayTelemetryEntry.FlowMagnitude)) == 12 &&
                   GetFieldOffset<SwayTelemetryEntry>(nameof(SwayTelemetryEntry.GlobalQualityWeight)) == 16 &&
                   GetFieldOffset<SwayTelemetryEntry>(nameof(SwayTelemetryEntry.AmplitudeMeters)) == 20 &&
                   GetFieldOffset<SwayTelemetryEntry>(nameof(SwayTelemetryEntry.StateHash)) == 24 &&
                   GetFieldOffset<SwayTelemetryEntry>(nameof(SwayTelemetryEntry.SourceHash)) == 28;
        }

        private static int GetFieldOffset<T>(string fieldName) where T : struct
        {
            System.Reflection.FieldInfo field = typeof(T).GetField(fieldName);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }

#if UNITY_EDITOR
        public static unsafe bool TryParseBiomeProfiles(ReadOnlySpan<byte> csvBytes, NativeArray<FloraBiomeSwayProfileDTO> profiles, out int count)
        {
            count = 0;
            if (!profiles.IsCreated || profiles.Length == 0)
                return false;

            int cursor = 0;
            bool any = false;
            void* profilesPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(profiles);
            int profileSize = UnsafeUtility.SizeOf<FloraBiomeSwayProfileDTO>();
            while (cursor < csvBytes.Length && count < profiles.Length)
            {
                ReadOnlySpan<byte> line = ReadLine(csvBytes, ref cursor);
                Trim(ref line);
                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;
                if (IsHeader(line))
                    continue;

                if (TryParseProfileRow(line, out FloraBiomeSwayProfileDTO profile))
                {
                    int targetIndex = count;
                    void* profilePtr = (byte*)profilesPtr + (targetIndex * profileSize);
                    UnsafeUtility.AsRef<FloraBiomeSwayProfileDTO>(profilePtr) = profile;
                    count = targetIndex + 1;
                    any = true;
                }
            }

            return any;
        }
#endif

        private bool TryColdBootstrapVault()
        {
            _vault = GlobalRegistry.DataVault;
            IDataVault vault = _vault;
            if (vault == null)
                return false;

            _vaultReady = EnsureVaultBuffers(vault, clearExisting: false) && HasResolvedVaultBuffers(vault);
            if (!_vaultReady)
                return false;

            _dumped = false;
            _tuningDirty = true;
            EnsureBurstKernelsCold();
            WriteTuningToVault(force: true);
#if UNITY_EDITOR
            if (_loadBiomeProfilesOnEnable)
                TryLoadBiomeProfilesFromEditorCsv(vault);
#endif
            if (_supportsConstantBuffers)
                EnsureShaderParamsBuffers();

            return true;
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _supportsConstantBuffers = SystemInfo.supportsSetConstantBuffer;
        }

        private static void EnsureBurstKernelsCold()
        {
            if (!s_generateMockKernel.IsCreated)
            {
                s_generateMockKernel = BurstCompiler.CompileFunctionPointer<GenerateMockAmbientFlowKernelDelegate>(
                    FloraAmbientSwayBurstKernelEntrypoints.GenerateMockAmbientFlow);
            }

            if (!s_calculateKernel.IsCreated)
            {
                s_calculateKernel = BurstCompiler.CompileFunctionPointer<CalculateFloraSwayParametersKernelDelegate>(
                    FloraAmbientSwayBurstKernelEntrypoints.CalculateFloraSwayParameters);
            }
        }

        private static unsafe bool RunMockAmbientFlowKernel(GenerateMockAmbientFlowJob job)
        {
            if (!s_generateMockKernel.IsCreated)
                return false;

            s_generateMockKernel.Invoke(&job);
            return true;
        }

        private static unsafe bool RunCalculateFloraSwayParametersKernel(CalculateFloraSwayParametersJob job)
        {
            if (!s_calculateKernel.IsCreated)
                return false;

            s_calculateKernel.Invoke(&job);
            return true;
        }

        private bool EnsureVaultBuffers(IDataVault vault, bool clearExisting)
        {
            if (vault == null)
                return false;

            if (!clearExisting && HasResolvedVaultBuffers(vault))
                return true;

            if (vault.IsCompactionFenceActive || vault.IsAllocationLocked)
                return false;

            NativeArrayOptions options = clearExisting ? NativeArrayOptions.ClearMemory : NativeArrayOptions.UninitializedMemory;
            _paramsHandle = vault.EnsureGenerationHandle<FloraSwayParamsDTO>(
                FloraAmbientSwayParamsBufferId,
                1,
                SystemID.FloraGenomics,
                options);
            _flowStateHandle = vault.EnsureGenerationHandle<FloraAmbientFlowStateDTO>(
                FloraAmbientSwayFlowStateBufferId,
                1,
                SystemID.FloraGenomics,
                options);
            _tuningHandle = vault.EnsureGenerationHandle<FloraSwayTuningDTO>(
                FloraAmbientSwayTuningBufferId,
                1,
                SystemID.FloraGenomics,
                options);
            _profileHandle = vault.EnsureGenerationHandle<FloraBiomeSwayProfileDTO>(
                FloraAmbientSwayBiomeProfilesBufferId,
                BiomeProfileCapacity,
                SystemID.FloraGenomics,
                options);
            _telemetryHandle = vault.EnsureGenerationHandle<SwayTelemetryEntry>(
                FloraAmbientSwayTelemetryRingBufferId,
                SwayTelemetryCapacity,
                SystemID.FloraGenomics,
                options);
            _telemetryCursorHandle = vault.EnsureGenerationHandle<int>(
                FloraAmbientSwayTelemetryCursorBufferId,
                1,
                SystemID.FloraGenomics,
                options);
            _csvScratchHandle = vault.EnsureGenerationHandle<byte>(
                FloraAmbientSwayCsvScratchBufferId,
                CsvScratchBytes,
                SystemID.FloraGenomics,
                options);

            if (clearExisting)
            {
                ClearBuffer(vault, in _paramsHandle);
                ClearBuffer(vault, in _flowStateHandle);
                ClearBuffer(vault, in _tuningHandle);
                ClearBuffer(vault, in _profileHandle);
                ClearBuffer(vault, in _telemetryHandle);
                ClearBuffer(vault, in _telemetryCursorHandle);
                ClearBuffer(vault, in _csvScratchHandle);
                _dumped = false;
            }

            return true;
        }

        private bool HasResolvedVaultBuffers(IDataVault vault)
        {
            return TryRead(vault, in _paramsHandle, out NativeArray<FloraSwayParamsDTO>.ReadOnly parameters) &&
                   parameters.Length >= 1 &&
                   TryRead(vault, in _flowStateHandle, out NativeArray<FloraAmbientFlowStateDTO>.ReadOnly flowState) &&
                   flowState.Length >= 1 &&
                   TryRead(vault, in _tuningHandle, out NativeArray<FloraSwayTuningDTO>.ReadOnly tuning) &&
                   tuning.Length >= 1 &&
                   TryRead(vault, in _profileHandle, out NativeArray<FloraBiomeSwayProfileDTO>.ReadOnly profiles) &&
                   profiles.Length >= BiomeProfileCapacity &&
                   TryRead(vault, in _telemetryHandle, out NativeArray<SwayTelemetryEntry>.ReadOnly telemetry) &&
                   telemetry.Length >= SwayTelemetryCapacity &&
                   TryRead(vault, in _telemetryCursorHandle, out NativeArray<int>.ReadOnly cursor) &&
                   cursor.Length >= 1 &&
                   TryRead(vault, in _csvScratchHandle, out NativeArray<byte>.ReadOnly scratch) &&
                   scratch.Length >= CsvScratchBytes;
        }

        private void WriteTuningToVault(bool force)
        {
            if (!force && !_tuningDirty)
                return;

            IDataVault vault = _vault;
            if (vault == null || !TryAcquireWrite(vault, in _tuningHandle, out NativeArray<FloraSwayTuningDTO> tuning))
            {
                return;
            }

            try
            {
                if (!tuning.IsCreated || tuning.Length == 0)
                    return;

                FloraSwayTuningDTO dto = default;
                dto.GlobalAmplitudeMeters = math.max(0f, _globalAmplitudeMeters);
                dto.Frequency = math.max(0.001f, _frequency);
                dto.PhaseSpatialOffset = math.max(0f, _phaseSpatialOffset);
                dto.AlphaClip = math.saturate(_alphaClip);
                dto.MockFlowSpeed = math.max(0.001f, _mockFlowSpeed);
                dto.MockFlowIntensity = math.max(0f, _mockFlowIntensity);
                dto.Flags = _mockFlowEnabled ? TuningFlagMockFlowEnabled : 0u;
                dto.ProfileHash = 0u;
                tuning[0] = dto;
                _tuningDirty = false;
            }
            finally
            {
                vault.ReleaseWriteLock(in _tuningHandle, SystemID.FloraGenomics);
            }
        }

#if UNITY_EDITOR
        private bool TryLoadBiomeProfilesFromEditorCsv(IDataVault vault)
        {
            if (vault == null)
                return false;

            if (!TryAcquireFloraAmbientSwayMutationGuard(vault, ProfileCsvMutationGuardMask))
                return false;

            try
            {
                if (!TryResolveGuardedMutable(
                    vault,
                    in _csvScratchHandle,
                    FloraAmbientSwayCsvScratchBufferId,
                    CsvScratchBytes,
                    out NativeArray<byte> scratch))
                {
                    return false;
                }

                string path = Path.Combine(Directory.GetCurrentDirectory(), "Docs", "Data", "Profiles", "flora_biome_sway_profiles.csv");
                if (!File.Exists(path))
                    return false;

                int bytesRead = 0;
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

                if (!TryResolveGuardedMutable(
                    vault,
                    in _profileHandle,
                    FloraAmbientSwayBiomeProfilesBufferId,
                    BiomeProfileCapacity,
                    out NativeArray<FloraBiomeSwayProfileDTO> profiles))
                {
                    return false;
                }

                ClearNativeArray(profiles);

                void* readPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch);
                return TryParseBiomeProfiles(new ReadOnlySpan<byte>(readPtr, bytesRead), profiles, out _);
            }
            finally
            {
                ReleaseFloraAmbientSwayMutationGuard(vault, ProfileCsvMutationGuardMask);
            }
        }
#endif

        private void RecordTelemetry(uint frame, uint flags, in FloraSwayParamsDTO dto)
        {
            IDataVault vault = _vault;
            if (vault == null)
                return;

            if (!TryAcquireFloraAmbientSwayMutationGuard(vault, TelemetryMutationGuardMask))
                return;

            try
            {
                if (!TryResolveGuardedMutable(
                    vault,
                    in _telemetryHandle,
                    FloraAmbientSwayTelemetryRingBufferId,
                    SwayTelemetryCapacity,
                    out NativeArray<SwayTelemetryEntry> ring) ||
                    !TryResolveGuardedMutable(
                        vault,
                        in _telemetryCursorHandle,
                        FloraAmbientSwayTelemetryCursorBufferId,
                        1,
                        out NativeArray<int> cursorArray))
                {
                    return;
                }

                if (ring.Length == 0 || cursorArray.Length == 0)
                    return;

                int cursor = cursorArray[0];
                if ((uint)cursor >= (uint)ring.Length)
                    cursor = 0;

                float3 telemetryFlow = math.float3(dto.GlobalFlowVector.x, dto.GlobalFlowVector.y, dto.GlobalFlowVector.z);
                float flowLengthSq = math.max(math.lengthsq(telemetryFlow), 0f);
                float flowMagnitude = flowLengthSq * math.rsqrt(math.max(flowLengthSq, 0.0001f));
                uint stateHash = 2166136261u;
                stateHash = MixHash(stateHash, math.asuint(dto.GlobalFlowVector.x));
                stateHash = MixHash(stateHash, math.asuint(dto.GlobalFlowVector.y));
                stateHash = MixHash(stateHash, math.asuint(dto.GlobalFlowVector.z));
                stateHash = MixHash(stateHash, math.asuint(dto.GlobalFlowVector.w));
                stateHash = MixHash(stateHash, math.asuint(dto.SwayMathParams.x));
                stateHash = MixHash(stateHash, math.asuint(dto.SwayMathParams.y));
                stateHash = MixHash(stateHash, math.asuint(dto.SwayMathParams.z));
                stateHash = MixHash(stateHash, math.asuint(dto.SwayMathParams.w));
                stateHash = MixHash(stateHash, flags);

                SwayTelemetryEntry entry = default;
                entry.Frame = frame;
                entry.Flags = flags;
                entry.WrappedTime = dto.SwayMathParams.x;
                entry.FlowMagnitude = flowMagnitude;
                entry.GlobalQualityWeight = dto.SwayMathParams.w;
                entry.AmplitudeMeters = dto.SwayMathParams.y;
                entry.StateHash = stateHash;
                entry.SourceHash = TelemetrySourceHash;
                ring[cursor] = entry;

                cursor++;
                if (cursor >= ring.Length)
                    cursor = 0;
                cursorArray[0] = cursor;
            }
            finally
            {
                ReleaseFloraAmbientSwayMutationGuard(vault, TelemetryMutationGuardMask);
            }
        }

        private void DumpTelemetryOnce()
        {
            if (_dumped)
                return;

            IDataVault vault = _vault;
            if (vault == null ||
                !TryRead(vault, in _telemetryHandle, out NativeArray<SwayTelemetryEntry>.ReadOnly ring) ||
                !TryRead(vault, in _telemetryCursorHandle, out NativeArray<int>.ReadOnly cursorArray) ||
                !ring.IsCreated)
            {
                return;
            }

            NativeArray<byte> payload = default;
            try
            {
                const string path = "Docs/AgentLogs/Dump_SHINOBU_267.bin";
                int headerBytes = 24;
                int entryBytes = (int)SwayTelemetryEntrySizeBytes;
                int totalBytes = headerBytes + ring.Length * entryBytes;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    totalBytes,
                    nameof(FloraAmbientSwayRuntime),
                    "FloraAmbientSwayTelemetryDumpPayload");
                Span<byte> bytes = new Span<byte>(payload.GetUnsafePtr(), totalBytes);
                WriteUInt32LittleEndian(bytes, 0, TelemetryDumpMagic);
                WriteUInt32LittleEndian(bytes, 4, TelemetryDumpVersion);
                WriteUInt32LittleEndian(bytes, 8, TelemetrySourceHash);
                WriteUInt32LittleEndian(bytes, 12, SwayTelemetryEntrySizeBytes);
                WriteUInt32LittleEndian(bytes, 16, unchecked((uint)ring.Length));
                WriteUInt32LittleEndian(bytes, 20, unchecked((uint)(cursorArray.IsCreated && cursorArray.Length > 0 ? cursorArray[0] : 0)));
                int writeOffset = headerBytes;
                for (int i = 0; i < ring.Length; i++)
                {
                    SwayTelemetryEntry entry = ring[i];
                    WriteSwayTelemetryEntry(bytes.Slice(writeOffset, entryBytes), in entry);
                    writeOffset += entryBytes;
                }

                if (NativeFaultDumpWriter.TryWriteAll(path, payload, totalBytes))
                    _dumped = true;
                else
                    CrashTelemetryBuffer.ReportBlackBoxExportFailure();
            }
            catch (IOException)
            {
                CrashTelemetryBuffer.ReportBlackBoxExportFailure();
            }
            catch (NotSupportedException)
            {
                CrashTelemetryBuffer.ReportBlackBoxExportFailure();
            }
            catch (UnauthorizedAccessException)
            {
                CrashTelemetryBuffer.ReportBlackBoxExportFailure();
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(FloraAmbientSwayRuntime),
                    "FloraAmbientSwayTelemetryDumpPayload");
            }
        }

        private static void WriteSwayTelemetryEntry(Span<byte> destination, in SwayTelemetryEntry entry)
        {
            destination.Clear();
            WriteUInt32LittleEndian(destination, 0, entry.Frame);
            WriteUInt32LittleEndian(destination, 4, entry.Flags);
            WriteSingleLittleEndian(destination, 8, entry.WrappedTime);
            WriteSingleLittleEndian(destination, 12, entry.FlowMagnitude);
            WriteSingleLittleEndian(destination, 16, entry.GlobalQualityWeight);
            WriteSingleLittleEndian(destination, 20, entry.AmplitudeMeters);
            WriteUInt32LittleEndian(destination, 24, entry.StateHash);
            WriteUInt32LittleEndian(destination, 28, entry.SourceHash);
        }

        private static void WriteSingleLittleEndian(Span<byte> destination, int offset, float value)
        {
            WriteUInt32LittleEndian(destination, offset, math.asuint(value));
        }

        private static void WriteUInt32LittleEndian(Span<byte> destination, int offset, uint value)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, 4), value);
        }

        private static void WriteInt32LittleEndian(Stream stream, int value)
        {
            WriteUInt32LittleEndian(stream, unchecked((uint)value));
        }

        private static void WriteSingleLittleEndian(Stream stream, float value)
        {
            WriteUInt32LittleEndian(stream, math.asuint(value));
        }

        private static void WriteUInt32LittleEndian(Stream stream, uint value)
        {
            stream.WriteByte((byte)value);
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 24));
        }

        private bool EnsureShaderParamsBuffers()
        {
            if (ShaderParamsBuffersReady())
                return true;

            ReleaseGraphicsBuffer(ref _shaderParamsBufferA);
            ReleaseGraphicsBuffer(ref _shaderParamsBufferB);
            _shaderWriteIndex = 0;
            _shaderParamsBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, FloraSwayParamsSizeBytes); // COLD ALLOC: GraphicsBuffer[1] - global flora sway constant buffer A - owner: FloraAmbientSwayRuntime.
            _shaderParamsBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, FloraSwayParamsSizeBytes); // COLD ALLOC: GraphicsBuffer[1] - global flora sway constant buffer B - owner: FloraAmbientSwayRuntime.
            bool valid = _shaderParamsBufferA.IsValid() && _shaderParamsBufferB.IsValid();
            if (!valid)
            {
                ReleaseGraphicsBuffer(ref _shaderParamsBufferA);
                ReleaseGraphicsBuffer(ref _shaderParamsBufferB);
            }

            return valid;
        }

        private bool ShaderParamsBuffersReady()
        {
            return _shaderParamsBufferA != null && _shaderParamsBufferA.IsValid() &&
                   _shaderParamsBufferB != null && _shaderParamsBufferB.IsValid();
        }

        private GraphicsBuffer AdvanceShaderParamsBuffer()
        {
            _shaderWriteIndex ^= 1;
            return _shaderWriteIndex == 0 ? _shaderParamsBufferA : _shaderParamsBufferB;
        }

        private static float ResolveDeltaTime(in DispatcherTimingDTO timing)
        {
            float deltaTime = timing.FixedDelta;
            if (!math.isfinite(deltaTime) || deltaTime <= 0f || deltaTime > 1f)
                deltaTime = timing.FrameDelta;
            if (!math.isfinite(deltaTime) || deltaTime <= 0f || deltaTime > 1f)
                deltaTime = 1f / 60f;
            return math.clamp(deltaTime, 0f, 0.1f);
        }

        private uint AdvanceFrameId(in DispatcherTimingDTO timing)
        {
            uint frame = timing.FrameId;
            if (frame == 0u)
            {
                _fallbackFrameCounter++;
                frame = _fallbackFrameCounter == 0u ? 1u : _fallbackFrameCounter;
            }

            _lastResolvedFrame = frame;
            return frame;
        }

        private uint AdvanceVisualFrameId(in DispatcherTimingDTO timing)
        {
            uint frame = timing.FrameId;
            if (frame != 0u)
            {
                _lastResolvedFrame = frame;
                return frame;
            }

            return _lastResolvedFrame != 0u ? _lastResolvedFrame : AdvanceFrameId(in timing);
        }

        private static float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            if (math.isfinite(weight))
                return math.saturate(weight);

            return 0f;
        }

        private static bool ValidateParams(in FloraSwayParamsDTO dto)
        {
            return math.all(math.isfinite(dto.GlobalFlowVector)) &&
                   math.all(math.isfinite(dto.SwayMathParams)) &&
                   dto.SwayMathParams.x >= 0f &&
                   dto.SwayMathParams.x < 1000f;
        }

        private static uint MixHash(uint hash, uint value)
        {
            hash ^= value;
            hash *= 16777619u;
            return hash;
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private static bool TryRead<T>(IDataVault vault, in VaultGenerationHandle<T> handle, out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   handle.BufferID != 0u &&
                   !vault.IsCompactionFenceActive &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   !vault.IsCompactionFenceActive;
        }

        private static bool TryAcquireWrite<T>(IDataVault vault, in VaultGenerationHandle<T> handle, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || handle.BufferID == 0u || vault.IsCompactionFenceActive)
                return false;

            if (!vault.TryAcquireWriteLock(in handle, SystemID.FloraGenomics, out buffer))
                return false;

            bool releaseOnFailure = true;
            try
            {
                if (buffer.IsCreated && !vault.IsCompactionFenceActive)
                {
                    releaseOnFailure = false;
                    return true;
                }

                buffer = default;
                return false;
            }
            finally
            {
                if (releaseOnFailure)
                    vault.ReleaseWriteLock(in handle, SystemID.FloraGenomics);
            }
        }

        private static bool TryAcquireFloraAmbientSwayMutationGuard(IDataVault vault, ulong guardMask)
        {
            return guardMask != 0UL &&
                   vault != null &&
                   !vault.IsCompactionFenceActive &&
                   vault.TryAcquireMutationGuard(guardMask);
        }

        private static void ReleaseFloraAmbientSwayMutationGuard(IDataVault vault, ulong guardMask)
        {
            if (vault != null && guardMask != 0UL)
                vault.ReleaseMutationGuard(guardMask);
        }

        private static bool TryResolveGuardedMutable<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   handle.BufferID == (uint)expectedBufferId &&
                   !vault.IsCompactionFenceActive &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength &&
                   !vault.IsCompactionFenceActive;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong FloraAmbientSwayMutationGuardBit(BufferID bufferId)
        {
            return 1UL << ((int)bufferId & 63);
        }

        private void ReleaseOwnedVaultBuffers(IDataVault vault)
        {
            if (vault == null)
                return;

            ReleaseVaultBuffer(vault, ref _paramsHandle);
            ReleaseVaultBuffer(vault, ref _flowStateHandle);
            ReleaseVaultBuffer(vault, ref _tuningHandle);
            ReleaseVaultBuffer(vault, ref _profileHandle);
            ReleaseVaultBuffer(vault, ref _telemetryHandle);
            ReleaseVaultBuffer(vault, ref _telemetryCursorHandle);
            ReleaseVaultBuffer(vault, ref _csvScratchHandle);
        }

        private static void ReleaseVaultBuffer<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault != null && handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);
            handle = default;
        }

        private static void ClearBuffer<T>(IDataVault vault, in VaultGenerationHandle<T> handle) where T : struct
        {
            if (!TryAcquireWrite(vault, in handle, out NativeArray<T> buffer))
                return;

            try
            {
                ClearNativeArray(buffer);
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.FloraGenomics);
            }
        }

        private static unsafe void ClearNativeArray<T>(NativeArray<T> buffer) where T : struct
        {
            if (!buffer.IsCreated || buffer.Length == 0)
                return;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(buffer);
            UnsafeUtility.MemClear(ptr, (long)UnsafeUtility.SizeOf<T>() * buffer.Length);
        }

#if UNITY_EDITOR
        private static bool IsHeader(ReadOnlySpan<byte> line)
        {
            int cursor = 0;
            ReadOnlySpan<byte> token = ReadCell(line, ref cursor);
            Trim(ref token);
            return EqualsAsciiLower(token, "biome") || EqualsAsciiLower(token, "biome_hash");
        }

        private static bool TryParseProfileRow(ReadOnlySpan<byte> line, out FloraBiomeSwayProfileDTO profile)
        {
            profile = default;
            int cursor = 0;
            ReadOnlySpan<byte> biome = ReadCell(line, ref cursor);
            ReadOnlySpan<byte> amplitude = ReadCell(line, ref cursor);
            ReadOnlySpan<byte> frequency = ReadCell(line, ref cursor);
            ReadOnlySpan<byte> phase = ReadCell(line, ref cursor);
            ReadOnlySpan<byte> alpha = ReadCell(line, ref cursor);
            ReadOnlySpan<byte> intensity = ReadCell(line, ref cursor);
            Trim(ref biome);
            Trim(ref amplitude);
            Trim(ref frequency);
            Trim(ref phase);
            Trim(ref alpha);
            Trim(ref intensity);
            if (biome.Length == 0)
                return false;

            profile.BiomeHash = HashLowerAscii(biome);
            profile.GlobalAmplitudeMeters = ParseFloat(amplitude, 0.42f);
            profile.Frequency = math.max(0.001f, ParseFloat(frequency, 1.1f));
            profile.PhaseSpatialOffset = math.max(0f, ParseFloat(phase, 0.85f));
            profile.AlphaClip = math.saturate(ParseFloat(alpha, 0.08f));
            profile.MockFlowIntensity = math.max(0f, ParseFloat(intensity, 0.75f));
            profile.Flags = 0u;
            uint stateHash = 2166136261u;
            stateHash = MixHash(stateHash, profile.BiomeHash);
            stateHash = MixHash(stateHash, math.asuint(profile.GlobalAmplitudeMeters));
            stateHash = MixHash(stateHash, math.asuint(profile.Frequency));
            stateHash = MixHash(stateHash, math.asuint(profile.PhaseSpatialOffset));
            stateHash = MixHash(stateHash, math.asuint(profile.AlphaClip));
            stateHash = MixHash(stateHash, math.asuint(profile.MockFlowIntensity));
            profile.StateHash = stateHash;
            return true;
        }

        private static ReadOnlySpan<byte> ReadLine(ReadOnlySpan<byte> text, ref int cursor)
        {
            int start = cursor;
            while (cursor < text.Length && text[cursor] != (byte)'\n' && text[cursor] != (byte)'\r')
                cursor++;

            int end = cursor;
            while (cursor < text.Length && (text[cursor] == (byte)'\n' || text[cursor] == (byte)'\r'))
                cursor++;

            return text.Slice(start, end - start);
        }

        private static ReadOnlySpan<byte> ReadCell(ReadOnlySpan<byte> line, ref int cursor)
        {
            if (cursor >= line.Length)
                return ReadOnlySpan<byte>.Empty;

            int start = cursor;
            while (cursor < line.Length && line[cursor] != (byte)',')
                cursor++;

            int end = cursor;
            if (cursor < line.Length && line[cursor] == (byte)',')
                cursor++;

            return line.Slice(start, end - start);
        }

        private static void Trim(ref ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && IsWhite(value[start]))
                start++;
            while (end >= start && IsWhite(value[end]))
                end--;
            value = start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool IsWhite(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r' || value == (byte)'\n';
        }

        private static bool EqualsAsciiLower(ReadOnlySpan<byte> value, string text)
        {
            if (value.Length != text.Length)
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                byte b = value[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                if (b != (byte)text[i])
                    return false;
            }

            return true;
        }

        private static uint HashLowerAscii(ReadOnlySpan<byte> value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                byte b = value[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                hash ^= b;
                hash *= 16777619u;
            }

            return hash;
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
#endif
        private sealed class VisualSyncUploadSystem : IDispatcherSystem
        {
            private const uint VisualSystemHash = 0x53483237u;
            private readonly FloraAmbientSwayRuntime _owner;

            public VisualSyncUploadSystem(FloraAmbientSwayRuntime owner)
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
