using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Hecton8.Graphics.Caustics
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9210)]
    public sealed class AnalyticalCausticsService : MonoBehaviour, ICausticsService, ILateFrameTickable, IWeatherEventListener, IOriginShiftListener, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private const int CausticsResolution = 512;
        private const int MaxWaveCount = 16;
        private const int WaveGpuStrideBytes = 32;
        private const int ThreadGroupSize = 8;
        private const float DefaultWorldSizeMeters = 128f;
        private const float DepthDisableY = -100f;
        private const float DepthEnableY = -95f;
        private const int BlackBoxCapacity = 300;
        private const string DumpPath = "Docs/AgentLogs/Dump_CAUSTICS_PROJECTION_ENGINEER.bin";
        private const uint TelemetryStateHash = 0x43415354u; // "CAST"
        private const uint TelemetryContextHash = 0x43415831u; // "CAX1"
        private const SystemID OwnerSystemId = SystemID.GraphicsScalability;
        private const BufferID WaveUploadScratchBufferId = (BufferID)0x43415841; // "CAXA"
        private const BufferID BlackBoxBufferId = (BufferID)0x43415842; // "CAXB"

        private static readonly int _ResultId = Shader.PropertyToID("_Result");
        private static readonly int _WaveDataId = Shader.PropertyToID("_WaveData");
        private static readonly int _WaveCountId = Shader.PropertyToID("_WaveCount");
        private static readonly int _CausticsAupId = Shader.PropertyToID("_CausticsAUP");
        private static readonly int _CausticsParamsId = Shader.PropertyToID("_CausticsParams");
        private static readonly int _CausticsChromaticId = Shader.PropertyToID("_CausticsChromatic");
        private static readonly int _HectonCausticsMapId = Shader.PropertyToID("_HectonCausticsMap");
        private static readonly int _HectonCausticsAupId = Shader.PropertyToID("_HectonCausticsAUP");
        private static readonly int _HectonCausticsRuntimeParamsId = Shader.PropertyToID("_HectonCausticsRuntimeParams");
        private static readonly int _ProjectedWorldRectId = Shader.PropertyToID("_HectonProjectedCausticsWorldRect");
        private static readonly int _ProjectedParamsId = Shader.PropertyToID("_HectonProjectedCausticsParams");
        private static readonly int _ProjectedColorId = Shader.PropertyToID("_HectonProjectedCausticsColor");
        private static readonly int _SimulationParamsAId = Shader.PropertyToID("_HectonCausticsSimulationParamsA");
        private static readonly int _SimulationParamsBId = Shader.PropertyToID("_HectonCausticsSimulationParamsB");
        private static readonly int _SimulationParamsCId = Shader.PropertyToID("_HectonCausticsSimulationParamsC");
        private static readonly int _OceanSurfaceWave0AId = Shader.PropertyToID("_HectonOceanSurfaceWave0A");
        private static readonly int _OceanSurfaceWave0BId = Shader.PropertyToID("_HectonOceanSurfaceWave0B");
        private static readonly int _OceanSurfaceWave1AId = Shader.PropertyToID("_HectonOceanSurfaceWave1A");
        private static readonly int _OceanSurfaceWave1BId = Shader.PropertyToID("_HectonOceanSurfaceWave1B");
        private static readonly int _OceanSurfaceWave2AId = Shader.PropertyToID("_HectonOceanSurfaceWave2A");
        private static readonly int _OceanSurfaceWave2BId = Shader.PropertyToID("_HectonOceanSurfaceWave2B");
        private static readonly int _OceanSurfaceWaveMetaId = Shader.PropertyToID("_HectonOceanSurfaceWaveMeta");

        private static AnalyticalCausticsService s_runtimeInstance;

        [Header("Projection")]
        [Tooltip("World-space square meters covered by the analytical caustics map.")]
        [SerializeField, Min(32f)] private float worldSizeMeters = DefaultWorldSizeMeters;
        [Tooltip("Base projected caustic intensity before weather and depth gates.")]
        [SerializeField, Min(0f)] private float baseIntensity = 0.42f;
        [Tooltip("How strongly storm/cloud state suppresses caustic intensity.")]
        [SerializeField, Range(0f, 1f)] private float cloudFadePenalty = 0.62f;
        [Tooltip("Meters of cheap RGB separation generated from the refraction vector.")]
        [SerializeField, Range(0f, 0.5f)] private float chromaticSplitMeters = 0.18f;
        [Tooltip("Tint applied by the CoreLit caustics scattering path.")]
        [SerializeField] private Color scatteringColor = new Color(0.12f, 0.34f, 0.42f, 1f);
        [Tooltip("Bootstrap-injected analytical caustics compute shader. Null means fragment fallback only.")]
        [SerializeField] private ComputeShader causticsCompute;

        private RenderTexture _causticsMap;
        private GraphicsBuffer _waveBuffer;
        private IDataVault _dataVault;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private Hecton8.Physics.HectonFluidEngine _fluidEngine;
        private NativeArray<CausticsWaveGpuData> _waveUploadScratch;
        private NativeArray<CausticTelemetryEntry> _blackBox;
        private VaultBufferHandle<CausticsWaveGpuData> _waveUploadScratchHandle;
        private VaultBufferHandle<CausticTelemetryEntry> _blackBoxHandle;
        private Vector4 _causticsAup;
        private Vector4 _worldRect;
        private Vector3 _lastAnchor;
        private int _kernelIndex = -1;
        private int _groupsX;
        private int _groupsY;
        private int _blackBoxCursor;
        private int _tickCount;
        private int _lastWaveMetaVersion = int.MinValue;
        private uint _lastTelemetryHash = uint.MaxValue;
        private float _weatherCloudCover01;
        private float _weatherIntensity01;
        private bool _blackBoxDumped;
        private bool _depthGateDisabled;
        private bool _isInitialized;
        private bool _registeredLateFrame;
        private bool _registeredWeather;
        private bool _registeredOriginShift;
        private bool _registeredHotSwap;
        private bool _ownsRegistrySlot;
        private bool _hasPublishedTexture;
        private bool _waveUploadDirty = true;
        private bool _computeKernelMissing;
        private bool _isComputeActive;

        public bool IsComputeActive => _isComputeActive;
        public RenderTexture CausticsMap => _causticsMap;
        public Vector4 CausticsAup => _causticsAup;
        public ServiceHeartbeatState HeartbeatState => _isInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;
        public bool IsServiceReady => _isInitialized;
        public int TickCount => _tickCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_runtimeInstance = null;
        }

        /// <summary>
        /// Injects the compute shader from bootstrap or scene-owned serialized references.
        /// </summary>
        public void AssignComputeShader(ComputeShader computeShader)
        {
            if (computeShader == null || ReferenceEquals(causticsCompute, computeShader))
                return;

            causticsCompute = computeShader;
            _kernelIndex = -1;
            _groupsX = 0;
            _groupsY = 0;
            _computeKernelMissing = false;
        }

        public static AnalyticalCausticsService EnsureRuntimeInstance()
        {
            if (GlobalRegistry.Caustics is AnalyticalCausticsService runtime)
                return runtime;

            if (s_runtimeInstance != null)
                return s_runtimeInstance;

            GameObject runtimeRoot = new GameObject("[AnalyticalCausticsService]"); // COLD ALLOC: GameObject[1] - bootstrap-owned analytical caustics root - owner: AnalyticalCausticsService
            return runtimeRoot.AddComponent<AnalyticalCausticsService>();
        }

        public void InitializeService()
        {
            EnsureSingletonOwnership();
            if (!ReferenceEquals(GlobalRegistry.Caustics, this))
                return;

            CacheRegistryServicesCold(forceRefresh: true);
            TryRegisterHotSwap();
            EnsureBlackBoxState();
            TryRegisterLateFrame();
            TryRegisterWeather();
            TryRegisterOriginShift();
            PublishDisabledGlobals();
            _isInitialized = true;
        }

        public void LateFrameTick()
        {
            _tickCount++;
            if (!_isInitialized)
            {
                InitializeService();
                if (!_isInitialized || !_ownsRegistrySlot)
                    return;
            }

            Vector3 anchor = ResolveRuntimeAnchor();
            float waterLevel = ResolveWaterLevel();
            float quality01 = ResolveGlobalQualityWeight01();
            float survivalPressure01 = ResolveSurvivalPressure01(quality01);
            bool depthDisabled = ResolveDepthGateDisabled(anchor.y);
            int maxDispatchWaveCount = ResolveDispatchWaveCount(MaxWaveCount, quality01);
            if (maxDispatchWaveCount <= 0 || depthDisabled)
                ReleaseComputeOnlyResources();

            int waveCount = 0;
            if (maxDispatchWaveCount > 0 && !depthDisabled)
            {
                EnsureComputeDispatchState();
                if (_waveUploadScratch.IsCreated)
                    waveCount = ResolveWaveUploadScratch();
            }

            int dispatchWaveCount = math.min(maxDispatchWaveCount, ResolveDispatchWaveCount(waveCount, quality01));
            uint stateFlags = ResolveStateFlags(anchor, waveCount, survivalPressure01, depthDisabled);
            bool computeAllowed = dispatchWaveCount > 0 &&
                                  (stateFlags & ((uint)CausticStateFlags.LowTierFallback | (uint)CausticStateFlags.DepthDisabled | (uint)CausticStateFlags.ComputeMissing)) == 0u;
            _isComputeActive = computeAllowed;

            PublishShaderGlobals(anchor, waterLevel, dispatchWaveCount, computeAllowed, survivalPressure01, depthDisabled);
            WriteBlackBox(anchor, waterLevel, waveCount, dispatchWaveCount, stateFlags);
            PublishStateTelemetryIfChanged(stateFlags, waveCount, dispatchWaveCount);

            if (!computeAllowed)
                return;

            DispatchCompute(dispatchWaveCount, waterLevel);
        }

        public void OnWeatherEvent(in WeatherEventPayload payload)
        {
            if (payload.EventType != (ushort)WeatherEventType.SnapshotUpdated)
                return;

            WeatherState state = (WeatherState)payload.StateMask;
            float stormCover = (state & WeatherState.Storm) != 0 ? 0.28f : 0f;
            _weatherIntensity01 = math.saturate(payload.WeatherIntensity);
            _weatherCloudCover01 = math.saturate(_weatherIntensity01 + stormCover);
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            _lastAnchor.x -= shiftData.ShiftOffset.x;
            _lastAnchor.y -= shiftData.ShiftOffset.y;
            _lastAnchor.z -= shiftData.ShiftOffset.z;
            _causticsAup.x -= shiftData.ShiftOffset.x;
            _causticsAup.y -= shiftData.ShiftOffset.z;
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        private void Awake()
        {
            if (s_runtimeInstance != null && !ReferenceEquals(s_runtimeInstance, this))
            {
                Destroy(gameObject);
                return;
            }

            s_runtimeInstance = this;
            CacheRegistryServicesCold(forceRefresh: true);
            TryRegisterHotSwap();
            EnsureSingletonOwnership();
        }

        private void OnEnable()
        {
            if (s_runtimeInstance != null && !ReferenceEquals(s_runtimeInstance, this))
            {
                Destroy(gameObject);
                return;
            }

            s_runtimeInstance = this;
            CacheRegistryServicesCold(forceRefresh: false);
            TryRegisterHotSwap();
            if (_isInitialized)
            {
                TryRegisterLateFrame();
                TryRegisterWeather();
                TryRegisterOriginShift();
                GlobalRegistry.RegisterCausticsService(this);
                _ownsRegistrySlot = true;
            }
        }

        private void OnDisable()
        {
            TryUnregisterLateFrame();
            TryUnregisterWeather();
            TryUnregisterOriginShift();
            TryUnregisterHotSwap();
            if (ReferenceEquals(GlobalRegistry.Caustics, this))
                GlobalRegistry.UnregisterCausticsService(this);
            _ownsRegistrySlot = false;
            if (ReferenceEquals(s_runtimeInstance, this))
                s_runtimeInstance = null;
            _isComputeActive = false;
            PublishDisabledGlobals();
            ReleaseComputeOnlyResources();
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    _dataVault = currentService as IDataVault;
                    _blackBox = default;
                    _blackBoxHandle = default;
                    _waveUploadScratch = default;
                    _waveUploadScratchHandle = default;
                    _lastWaveMetaVersion = int.MinValue;
                    _waveUploadDirty = true;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.FluidRuntime:
                    _fluidEngine = currentService as Hecton8.Physics.HectonFluidEngine;
                    break;
                case GlobalRegistryServiceSlot.CausticsRuntime:
                    _ownsRegistrySlot = ReferenceEquals(currentService, this);
                    break;
            }
        }

        private void OnDestroy()
        {
            ShutdownServiceState();
        }

        private void EnsureSingletonOwnership()
        {
            ICausticsService registered = GlobalRegistry.Caustics;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                _ownsRegistrySlot = false;
                Destroy(gameObject);
                return;
            }

            if (!ReferenceEquals(registered, this))
                GlobalRegistry.RegisterCausticsService(this);
            _ownsRegistrySlot = true;
        }

        private void EnsureBlackBoxState()
        {
            if (_blackBox.IsCreated)
                return;

            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            _blackBoxHandle = vault.GetBufferHandle<CausticTelemetryEntry>(
                BlackBoxBufferId,
                BlackBoxCapacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _blackBox = _blackBoxHandle.Resolve(vault);
        }

        private void EnsureComputeDispatchState()
        {
            EnsureComputeResources();
            if (causticsCompute == null || _kernelIndex < 0)
                return;

            EnsureRenderTexture();
            if (_causticsMap == null)
                return;

            if (!_waveUploadScratch.IsCreated)
            {
                IDataVault vault = _dataVault;
                if (vault == null)
                    return;

                _waveUploadScratchHandle = vault.GetBufferHandle<CausticsWaveGpuData>(
                    WaveUploadScratchBufferId,
                    MaxWaveCount,
                    OwnerSystemId,
                    NativeArrayOptions.ClearMemory);
                _waveUploadScratch = _waveUploadScratchHandle.Resolve(vault);
                if (!_waveUploadScratch.IsCreated || _waveUploadScratch.Length < MaxWaveCount)
                    return;

                _lastWaveMetaVersion = int.MinValue;
                _waveUploadDirty = true;
            }

            if (_waveBuffer == null)
                _waveBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MaxWaveCount, WaveGpuStrideBytes); // COLD ALLOC: GraphicsBuffer[16 structs] - analytical caustics wave upload - owner: AnalyticalCausticsService
        }

        private void EnsureComputeResources()
        {
            if (causticsCompute == null || _kernelIndex >= 0 || _computeKernelMissing)
                return;

            if (!causticsCompute.HasKernel("GenerateCaustics"))
            {
                _computeKernelMissing = true;
                return;
            }

            _kernelIndex = causticsCompute.FindKernel("GenerateCaustics");
            causticsCompute.GetKernelThreadGroupSizes(_kernelIndex, out uint sizeX, out uint sizeY, out _);
            int threadX = math.max(1, (int)sizeX);
            int threadY = math.max(1, (int)sizeY);
            _groupsX = (CausticsResolution + threadX - 1) / threadX;
            _groupsY = (CausticsResolution + threadY - 1) / threadY;
        }

        private void EnsureRenderTexture()
        {
            if (_causticsMap == null)
                return;

            _causticsMap.Release();
            Destroy(_causticsMap);
            _causticsMap = null;
        }

        private int ResolveWaveUploadScratch()
        {
            int waveCount = TryFillWaveUploadScratchFromVault(out bool vaultBound);
            if (waveCount <= 0)
                waveCount = TryFillWaveUploadScratchFromShaderGlobals();
            if (waveCount <= 0)
                waveCount = FillFallbackWaveUploadScratch();

            if (vaultBound)
                return waveCount;

            _waveUploadDirty = true;
            return waveCount;
        }

        private int TryFillWaveUploadScratchFromVault(out bool vaultBound)
        {
            vaultBound = false;
            IDataVault vault = _dataVault;
            if (vault == null ||
                !vault.TryGetBuffer(BufferID.OceanGerstnerWaves, out NativeArray<GerstnerWaveComponent> waves) ||
                !vault.TryGetBuffer(BufferID.OceanGerstnerWaveMeta, out NativeArray<OceanGerstnerWaveBufferMeta> meta) ||
                !waves.IsCreated || waves.Length < MaxWaveCount || !meta.IsCreated || meta.Length < 1)
            {
                return 0;
            }

            OceanGerstnerWaveBufferMeta waveMeta = meta[0];
            int waveCount = math.clamp(waveMeta.ActiveWaveCount, 0, MaxWaveCount);
            if (waveCount <= 0)
                return 0;

            vaultBound = true;
            if (waveMeta.Version == _lastWaveMetaVersion)
                return waveCount;

            _lastWaveMetaVersion = waveMeta.Version;
            for (int i = 0; i < MaxWaveCount; i++)
                WriteGpuWave(i, waves[i], i < waveCount);
            _waveUploadDirty = true;
            return waveCount;
        }

        private int TryFillWaveUploadScratchFromShaderGlobals()
        {
            Vector4 meta = Shader.GetGlobalVector(_OceanSurfaceWaveMetaId);
            int count = math.clamp((int)math.round(meta.x), 0, 3);
            if (count <= 0)
                return 0;

            WriteGpuWave(0, Shader.GetGlobalVector(_OceanSurfaceWave0AId), Shader.GetGlobalVector(_OceanSurfaceWave0BId), count > 0);
            WriteGpuWave(1, Shader.GetGlobalVector(_OceanSurfaceWave1AId), Shader.GetGlobalVector(_OceanSurfaceWave1BId), count > 1);
            WriteGpuWave(2, Shader.GetGlobalVector(_OceanSurfaceWave2AId), Shader.GetGlobalVector(_OceanSurfaceWave2BId), count > 2);
            for (int i = 3; i < MaxWaveCount; i++)
                WriteGpuWave(i, default, false);
            return count;
        }

        private int FillFallbackWaveUploadScratch()
        {
            float stormScale = 1f + _weatherIntensity01 * 0.2f;
            GerstnerWaveComponent wave0 = BuildFallbackWave(new float2(1f, 0.21f), 0.28f * stormScale, 16f, 0.42f, 0f, 0.82f);
            GerstnerWaveComponent wave1 = BuildFallbackWave(new float2(-0.38f, 0.92f), 0.18f * stormScale, 9.5f, 0.36f, 1.73f, 1.12f);
            GerstnerWaveComponent wave2 = BuildFallbackWave(new float2(0.72f, -0.69f), 0.11f * stormScale, 5.75f, 0.28f, 2.41f, 1.37f);
            WriteGpuWave(0, wave0, true);
            WriteGpuWave(1, wave1, true);
            WriteGpuWave(2, wave2, true);
            for (int i = 3; i < MaxWaveCount; i++)
                WriteGpuWave(i, default, false);
            return 3;
        }

        private static GerstnerWaveComponent BuildFallbackWave(float2 direction, float amplitude, float wavelength, float steepness, float phase, float speed)
        {
            GerstnerWaveComponent wave;
            wave.DirectionXZ = NormalizeDirectionFast(direction, new float2(1f, 0f));
            wave.Amplitude = amplitude;
            wave.Wavelength = wavelength;
            wave.Steepness = steepness;
            wave.PhaseOffset = phase;
            wave.SpeedMultiplier = speed;
            return wave;
        }

        private static float2 NormalizeDirectionFast(float2 direction, float2 fallback)
        {
            float lengthSq = math.lengthsq(direction);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return fallback;

            return direction * math.rsqrt(lengthSq);
        }

        private void WriteGpuWave(int index, in GerstnerWaveComponent wave, bool active)
        {
            float2 direction = NormalizeDirectionFast(wave.DirectionXZ, new float2(1f, 0f));
            float amplitude = math.isfinite(wave.Amplitude) ? math.max(0f, wave.Amplitude) : 0f;
            float wavelength = math.isfinite(wave.Wavelength) ? math.max(0.05f, wave.Wavelength) : 8f;
            float steepness = math.isfinite(wave.Steepness) ? math.clamp(wave.Steepness, 0f, 1.2f) : 0f;
            float phase = math.isfinite(wave.PhaseOffset) ? wave.PhaseOffset : 0f;
            float speed = math.isfinite(wave.SpeedMultiplier) ? math.max(0.01f, wave.SpeedMultiplier) : 1f;
            _waveUploadScratch[index] = new CausticsWaveGpuData
            {
                WaveA = new Vector4(direction.x, direction.y, amplitude, wavelength),
                WaveB = new Vector4(steepness, phase, speed, active ? 1f : 0f)
            };
        }

        private void WriteGpuWave(int index, Vector4 waveA, Vector4 waveB, bool active)
        {
            GerstnerWaveComponent wave;
            wave.DirectionXZ = new float2(waveA.x, waveA.y);
            wave.Amplitude = waveA.z;
            wave.Wavelength = waveA.w;
            wave.Steepness = waveB.x;
            wave.PhaseOffset = waveB.y;
            wave.SpeedMultiplier = waveB.z;
            WriteGpuWave(index, wave, active && waveB.w > 0.5f);
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 0f);
        }

        private static float Smooth01(float value)
        {
            value = math.saturate(value);
            return value * value * (3f - (2f * value));
        }

        private static int ResolveDispatchWaveCount(int waveCount, float quality01)
        {
            int clamped = math.clamp(waveCount, 0, MaxWaveCount);
            if (clamped <= 0)
                return 0;

            float quality = math.saturate(math.isfinite(quality01) ? quality01 : 0f);
            float waveCurve01 = Smooth01(math.saturate((quality - 0.24f) * 1.3157895f));
            int qualityBudget = (int)math.floor(math.lerp(0f, MaxWaveCount, waveCurve01) + 0.0001f);
            return math.min(clamped, math.clamp(qualityBudget, 0, MaxWaveCount));
        }

        private bool ResolveDepthGateDisabled(float playerAupY)
        {
            if (_depthGateDisabled)
            {
                if (playerAupY > DepthEnableY)
                    _depthGateDisabled = false;
            }
            else if (playerAupY < DepthDisableY)
            {
                _depthGateDisabled = true;
            }

            return _depthGateDisabled;
        }

        private uint ResolveStateFlags(in Vector3 anchor, int waveCount, float survivalPressure01, bool depthDisabled)
        {
            uint flags = (uint)CausticStateFlags.Initialized;
            if (causticsCompute == null || _kernelIndex < 0 || _causticsMap == null || _waveBuffer == null)
                flags |= (uint)CausticStateFlags.ComputeMissing;
            if (math.saturate(survivalPressure01) > 0.985f)
                flags |= (uint)CausticStateFlags.LowTierFallback;
            if (depthDisabled)
                flags |= (uint)CausticStateFlags.DepthDisabled;
            if (waveCount > 0)
                flags |= (uint)CausticStateFlags.WavesBound;
            if (_weatherCloudCover01 > 0.01f)
                flags |= (uint)CausticStateFlags.WeatherClouded;
            return flags;
        }

        private static float ResolveSurvivalPressure01(float quality01)
        {
            float quality = math.saturate(math.isfinite(quality01) ? quality01 : 0f);
            return 1f - Smooth01(math.saturate((quality - 0.12f) * 1.1363636f));
        }

        private void PublishShaderGlobals(in Vector3 anchor, float waterLevel, int waveCount, bool computeActive, float survivalPressure01, bool depthDisabled)
        {
            float size = math.max(32f, worldSizeMeters);
            float invSize = math.rcp(size);
            float halfSize = size * 0.5f;
            float survivalWeight = math.saturate(survivalPressure01);
            float intensity = depthDisabled ? 0f : baseIntensity * (1f - _weatherCloudCover01 * cloudFadePenalty) * (1f - survivalWeight);
            intensity = math.max(0f, intensity);

            _lastAnchor = anchor;
            _worldRect = new Vector4(anchor.x - halfSize, anchor.z - halfSize, invSize, invSize);
            _causticsAup = new Vector4(_worldRect.x, _worldRect.y, invSize, computeActive ? 1f : 0f);
            Vector4 projectedParams = new Vector4(intensity, waterLevel, 0f, 0.02f);
            Color linear = scatteringColor.linear;
            Vector4 color = new Vector4(linear.r, linear.g, linear.b, 1f);
            Vector4 simulationA = new Vector4(14f, 23f, 0.32f, 0.57f);
            Vector4 simulationB = new Vector4(3.1f, 0.42f, Time.time, waterLevel);
            Vector4 simulationC = new Vector4(
                _weatherIntensity01 * 2f,
                0.21f + _weatherCloudCover01 * 0.11f,
                -0.34f,
                Time.time * 0.07f);

            _hasPublishedTexture = false;
        }

        private void DispatchCompute(int waveCount, float waterLevel)
        {
            _waveUploadDirty = false;
        }

        private Vector3 ResolveRuntimeAnchor()
        {
            if (TryResolvePlayerAupRuntimePosition(out Vector3 aupRuntimePosition))
                return aupRuntimePosition;

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform))
                return playerTransform.position;

            return _lastAnchor;
        }

        private bool TryResolvePlayerAupRuntimePosition(out Vector3 runtimePosition)
        {
            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext))
            {
                PlayerMovementRuntimeState movementState = runtimeContext.MovementState;
                if ((movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
                {
                    float3 runtime = movementState.PredictedAup.ToRuntimeFloat3();
                    runtimePosition = new Vector3(runtime.x, runtime.y, runtime.z);
                    return true;
                }
            }

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            HectonPlayerMovement playerMovement = playerContext != null ? playerContext.PlayerMovement : null;
            if (playerMovement != null)
            {
                float3 runtime = playerMovement.CurrentAup.ToRuntimeFloat3();
                runtimePosition = new Vector3(runtime.x, runtime.y, runtime.z);
                return true;
            }

            runtimePosition = default;
            return false;
        }

        private float ResolveWaterLevel()
        {
            Hecton8.Physics.HectonFluidEngine fluidEngine = _fluidEngine;
            return fluidEngine != null ? fluidEngine.WaterLevel : 4900f;
        }

        private void PublishDisabledGlobals()
        {
            _causticsAup.w = 0f;
            _hasPublishedTexture = false;
        }

        private void WriteBlackBox(in Vector3 anchor, float waterLevel, int waveCount, int dispatchWaveCount, uint flags)
        {
            if (!_blackBox.IsCreated)
                return;

            bool finite = math.all(math.isfinite(new float4(anchor.x, anchor.y, anchor.z, waterLevel)));
            CausticTelemetryEntry entry;
            entry.FrameIndex = (uint)Time.frameCount;
            entry.StateHash = ResolveStateHash(flags, waveCount, dispatchWaveCount);
            entry.ContextHash = TelemetryContextHash;
            entry.Flags = flags | (finite ? 0u : (uint)CausticStateFlags.NonFinite);
            entry.AnchorX = anchor.x;
            entry.AnchorY = anchor.y;
            entry.AnchorZ = anchor.z;
            entry.WaterY = waterLevel;
            entry.WaveCount = waveCount;
            entry.DispatchWaveCount = dispatchWaveCount;
            entry.Intensity = baseIntensity;
            entry.CloudCover01 = _weatherCloudCover01;
            _blackBox[_blackBoxCursor] = entry;
            _blackBoxCursor = (_blackBoxCursor + 1) % BlackBoxCapacity;

            if (!finite && !_blackBoxDumped)
            {
                DumpBlackBox();
                _blackBoxDumped = true;
            }
            else if (finite)
            {
                _blackBoxDumped = false;
            }
        }

        private static uint ResolveStateHash(uint flags, int waveCount, int dispatchWaveCount)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ TelemetryStateHash) * 16777619u;
                hash = (hash ^ flags) * 16777619u;
                hash = (hash ^ (uint)waveCount) * 16777619u;
                hash = (hash ^ (uint)dispatchWaveCount) * 16777619u;
                return hash;
            }
        }

        private void DumpBlackBox()
        {
            if (!_blackBox.IsCreated)
                return;

            string root = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(root))
                return;

            string path = Path.Combine(root, DumpPath);
            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
                return;

            Directory.CreateDirectory(directory);
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(0x43584142u); // "BAXC"
                writer.Write(BlackBoxCapacity);
                writer.Write(_blackBoxCursor);
                for (int i = 0; i < _blackBox.Length; i++)
                {
                    int index = (_blackBoxCursor + i) % _blackBox.Length;
                    CausticTelemetryEntry entry = _blackBox[index];
                    writer.Write(entry.FrameIndex);
                    writer.Write(entry.StateHash);
                    writer.Write(entry.ContextHash);
                    writer.Write(entry.Flags);
                    writer.Write(entry.AnchorX);
                    writer.Write(entry.AnchorY);
                    writer.Write(entry.AnchorZ);
                    writer.Write(entry.WaterY);
                    writer.Write(entry.WaveCount);
                    writer.Write(entry.DispatchWaveCount);
                    writer.Write(entry.Intensity);
                    writer.Write(entry.CloudCover01);
                }
            }
        }

        private void PublishStateTelemetryIfChanged(uint stateFlags, int waveCount, int dispatchWaveCount)
        {
            uint telemetryHash = ResolveStateHash(stateFlags, waveCount, dispatchWaveCount);
            if (_lastTelemetryHash == telemetryHash)
                return;

            _lastTelemetryHash = telemetryHash;
            GlobalTelemetryBus.PublishPerformanceWarning(TelemetryStateHash, telemetryHash, dispatchWaveCount);
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrame = false;
        }

        private void TryRegisterWeather()
        {
            if (_registeredWeather || !Application.isPlaying)
                return;

            WeatherEvents.Register(this);
            _registeredWeather = true;
        }

        private void TryUnregisterWeather()
        {
            if (!_registeredWeather)
                return;

            WeatherEvents.Unregister(this);
            _registeredWeather = false;
        }

        private void TryRegisterOriginShift()
        {
            if (_registeredOriginShift || !Application.isPlaying)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _registeredOriginShift = true;
        }

        private void TryUnregisterOriginShift()
        {
            if (!_registeredOriginShift)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _registeredOriginShift = false;
        }

        private void TryRegisterHotSwap()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwap()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void CacheRegistryServicesCold(bool forceRefresh)
        {
            if (forceRefresh || _dataVault == null)
            {
                _dataVault = GlobalRegistry.DataVault;
                _lastWaveMetaVersion = int.MinValue;
                _waveUploadDirty = true;
            }

            if (forceRefresh || _playerRuntimeContext == null)
                _playerRuntimeContext = GlobalRegistry.Player;

            if (forceRefresh || _fluidEngine == null)
                _fluidEngine = GlobalRegistry.Fluid;
        }

        private void ShutdownServiceState()
        {
            TryUnregisterLateFrame();
            TryUnregisterWeather();
            TryUnregisterOriginShift();
            TryUnregisterHotSwap();
            if (ReferenceEquals(GlobalRegistry.Caustics, this))
                GlobalRegistry.UnregisterCausticsService(this);
            _ownsRegistrySlot = false;
            if (ReferenceEquals(s_runtimeInstance, this))
                s_runtimeInstance = null;

            ReleaseComputeOnlyResources();

            _blackBox = default;
            _blackBoxHandle = default;

            _isInitialized = false;
            _isComputeActive = false;
            _hasPublishedTexture = false;
            _blackBoxDumped = false;
            PublishDisabledGlobals();
        }

        private void ReleaseComputeOnlyResources()
        {
            if (_waveUploadScratch.IsCreated)
            {
                _waveUploadScratch = default;
            }
            _waveUploadScratchHandle = default;

            _waveBuffer?.Release();
            _waveBuffer = null;
            if (_causticsMap != null)
            {
                _causticsMap.Release();
                Destroy(_causticsMap);
                _causticsMap = null;
            }

            _hasPublishedTexture = false;
            _lastWaveMetaVersion = int.MinValue;
            _waveUploadDirty = true;
            _isComputeActive = false;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct CausticsWaveGpuData
        {
            [FieldOffset(0)]
            public Vector4 WaveA;
            [FieldOffset(16)]
            public Vector4 WaveB;
        }

        [StructLayout(LayoutKind.Explicit, Size = 48)]
        private struct CausticTelemetryEntry
        {
            [FieldOffset(0)]
            public uint FrameIndex;
            [FieldOffset(4)]
            public uint StateHash;
            [FieldOffset(8)]
            public uint ContextHash;
            [FieldOffset(12)]
            public uint Flags;
            [FieldOffset(16)]
            public float AnchorX;
            [FieldOffset(20)]
            public float AnchorY;
            [FieldOffset(24)]
            public float AnchorZ;
            [FieldOffset(28)]
            public float WaterY;
            [FieldOffset(32)]
            public int WaveCount;
            [FieldOffset(36)]
            public int DispatchWaveCount;
            [FieldOffset(40)]
            public float Intensity;
            [FieldOffset(44)]
            public float CloudCover01;
        }

        [System.Flags]
        private enum CausticStateFlags : uint
        {
            Initialized = 1u << 0,
            LowTierFallback = 1u << 1,
            DepthDisabled = 1u << 2,
            ComputeMissing = 1u << 3,
            WavesBound = 1u << 4,
            WeatherClouded = 1u << 5,
            NonFinite = 1u << 6
        }
    }
}
