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
    public sealed class AnalyticalCausticsService : MonoBehaviour, ICausticsService, ILateFrameTickable, IWeatherEventListener, IOriginShiftListener, IServiceHeartbeat, IServiceShutdown
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
        private NativeArray<CausticsWaveGpuData> _waveUploadScratch;
        private NativeArray<CausticTelemetryEntry> _blackBox;
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
                if (!_isInitialized || !ReferenceEquals(GlobalRegistry.Caustics, this))
                    return;
            }

            Vector3 anchor = ResolveRuntimeAnchor();
            float waterLevel = ResolveWaterLevel();
            bool lowTier = IsLowTier();
            bool depthDisabled = ResolveDepthGateDisabled(anchor.y);
            if (lowTier || depthDisabled)
                ReleaseComputeOnlyResources();

            int waveCount = 0;
            if (!lowTier && !depthDisabled)
            {
                EnsureComputeDispatchState();
                if (_waveUploadScratch.IsCreated)
                    waveCount = ResolveWaveUploadScratch();
            }

            int dispatchWaveCount = ResolveDispatchWaveCount(waveCount);
            uint stateFlags = ResolveStateFlags(anchor, waveCount, lowTier, depthDisabled);
            bool computeAllowed = dispatchWaveCount > 0 &&
                                  (stateFlags & ((uint)CausticStateFlags.LowTierFallback | (uint)CausticStateFlags.DepthDisabled | (uint)CausticStateFlags.ComputeMissing)) == 0u;
            _isComputeActive = computeAllowed;

            PublishShaderGlobals(anchor, waterLevel, dispatchWaveCount, computeAllowed, depthDisabled);
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
            if (_isInitialized)
            {
                TryRegisterLateFrame();
                TryRegisterWeather();
                TryRegisterOriginShift();
                GlobalRegistry.RegisterCausticsService(this);
            }
        }

        private void OnDisable()
        {
            TryUnregisterLateFrame();
            TryUnregisterWeather();
            TryUnregisterOriginShift();
            if (ReferenceEquals(GlobalRegistry.Caustics, this))
                GlobalRegistry.UnregisterCausticsService(this);
            if (ReferenceEquals(s_runtimeInstance, this))
                s_runtimeInstance = null;
            _isComputeActive = false;
            PublishDisabledGlobals();
            ReleaseComputeOnlyResources();
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
                Destroy(gameObject);
                return;
            }

            if (!ReferenceEquals(registered, this))
                GlobalRegistry.RegisterCausticsService(this);
        }

        private void EnsureBlackBoxState()
        {
            if (!_blackBox.IsCreated)
            {
                _blackBox = new NativeArray<CausticTelemetryEntry>(BlackBoxCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<CausticTelemetryEntry>[300] - caustics black box - owner: AnalyticalCausticsService
                NativeMemorySentinel.RegisterNativeArray(_blackBox, nameof(AnalyticalCausticsService), nameof(_blackBox), NativeAllocationLifetime.Session);
            }
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
                _waveUploadScratch = new NativeArray<CausticsWaveGpuData>(MaxWaveCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<CausticsWaveGpuData>[16] - compute upload scratch - owner: AnalyticalCausticsService
                NativeMemorySentinel.RegisterNativeArray(_waveUploadScratch, nameof(AnalyticalCausticsService), nameof(_waveUploadScratch), NativeAllocationLifetime.Session);
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
            if (_causticsMap != null)
                return;

            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(CausticsResolution, CausticsResolution, GraphicsFormat.R8G8B8A8_UNorm, 0)
            {
                enableRandomWrite = true,
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = false
            };
            _causticsMap = new RenderTexture(descriptor)
            {
                name = "Hecton Analytical Caustics 512",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            _causticsMap.Create();
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
            IDataVault vault = GlobalRegistry.DataVault;
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

        private static int ResolveDispatchWaveCount(int waveCount)
        {
            int clamped = math.clamp(waveCount, 0, MaxWaveCount);
            if (clamped <= 0)
                return 0;

            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            if (tier == HectonQualityTier.Ultra)
                return clamped;
            if (tier == HectonQualityTier.High)
                return math.min(clamped, 12);
            if (tier == HectonQualityTier.Mid)
                return math.min(clamped, 8);
            return 0;
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

        private uint ResolveStateFlags(in Vector3 anchor, int waveCount, bool lowTier, bool depthDisabled)
        {
            uint flags = (uint)CausticStateFlags.Initialized;
            if (causticsCompute == null || _kernelIndex < 0 || _causticsMap == null || _waveBuffer == null)
                flags |= (uint)CausticStateFlags.ComputeMissing;
            if (lowTier)
                flags |= (uint)CausticStateFlags.LowTierFallback;
            if (depthDisabled)
                flags |= (uint)CausticStateFlags.DepthDisabled;
            if (waveCount > 0)
                flags |= (uint)CausticStateFlags.WavesBound;
            if (_weatherCloudCover01 > 0.01f)
                flags |= (uint)CausticStateFlags.WeatherClouded;
            return flags;
        }

        private static bool IsLowTier()
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            return GlobalRegistry.H8_LOW_MEMORY_PROFILE ||
                   tier == HectonQualityTier.Unknown ||
                   tier == HectonQualityTier.Low ||
                   tier == HectonQualityTier.Mx350;
        }

        private void PublishShaderGlobals(in Vector3 anchor, float waterLevel, int waveCount, bool computeActive, bool depthDisabled)
        {
            float size = math.max(32f, worldSizeMeters);
            float invSize = math.rcp(size);
            float halfSize = size * 0.5f;
            float intensity = depthDisabled ? 0f : baseIntensity * (1f - _weatherCloudCover01 * cloudFadePenalty);
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

            Shader.SetGlobalVector(_ProjectedWorldRectId, _worldRect);
            Shader.SetGlobalVector(_ProjectedParamsId, projectedParams);
            Shader.SetGlobalVector(_ProjectedColorId, color);
            Shader.SetGlobalVector(_SimulationParamsAId, simulationA);
            Shader.SetGlobalVector(_SimulationParamsBId, simulationB);
            Shader.SetGlobalVector(_SimulationParamsCId, simulationC);
            Shader.SetGlobalVector(_HectonCausticsAupId, _causticsAup);
            Shader.SetGlobalVector(_HectonCausticsRuntimeParamsId, new Vector4(computeActive ? 1f : 0f, waveCount, _weatherCloudCover01, intensity));

            if (_causticsMap != null && !_hasPublishedTexture)
            {
                Shader.SetGlobalTexture(_HectonCausticsMapId, _causticsMap);
                _hasPublishedTexture = true;
            }
        }

        private void DispatchCompute(int waveCount, float waterLevel)
        {
            if (_waveUploadDirty)
            {
                _waveBuffer.SetData(_waveUploadScratch);
                _waveUploadDirty = false;
            }

            causticsCompute.SetTexture(_kernelIndex, _ResultId, _causticsMap);
            causticsCompute.SetBuffer(_kernelIndex, _WaveDataId, _waveBuffer);
            causticsCompute.SetInt(_WaveCountId, math.clamp(waveCount, 1, MaxWaveCount));
            causticsCompute.SetVector(_CausticsAupId, _causticsAup);
            causticsCompute.SetVector(_CausticsParamsId, new Vector4(Time.time, math.max(0f, baseIntensity), waterLevel, DefaultWorldSizeMeters));
            causticsCompute.SetVector(_CausticsChromaticId, new Vector4(chromaticSplitMeters, _weatherCloudCover01, _weatherIntensity01, 0f));
            causticsCompute.Dispatch(_kernelIndex, _groupsX > 0 ? _groupsX : (CausticsResolution / ThreadGroupSize), _groupsY > 0 ? _groupsY : (CausticsResolution / ThreadGroupSize), 1);
        }

        private Vector3 ResolveRuntimeAnchor()
        {
            if (TryResolvePlayerAupRuntimePosition(out Vector3 aupRuntimePosition))
                return aupRuntimePosition;

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform))
                return playerTransform.position;

            return _lastAnchor;
        }

        private static bool TryResolvePlayerAupRuntimePosition(out Vector3 runtimePosition)
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

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
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

        private static float ResolveWaterLevel()
        {
            Hecton8.Physics.HectonFluidEngine fluidEngine = GlobalRegistry.Fluid;
            return fluidEngine != null ? fluidEngine.WaterLevel : 4900f;
        }

        private void PublishDisabledGlobals()
        {
            _causticsAup.w = 0f;
            Shader.SetGlobalVector(_HectonCausticsAupId, _causticsAup);
            Shader.SetGlobalVector(_ProjectedParamsId, Vector4.zero);
            Shader.SetGlobalVector(_HectonCausticsRuntimeParamsId, Vector4.zero);
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

        private void ShutdownServiceState()
        {
            TryUnregisterLateFrame();
            TryUnregisterWeather();
            TryUnregisterOriginShift();
            if (ReferenceEquals(GlobalRegistry.Caustics, this))
                GlobalRegistry.UnregisterCausticsService(this);
            if (ReferenceEquals(s_runtimeInstance, this))
                s_runtimeInstance = null;

            ReleaseComputeOnlyResources();

            if (_blackBox.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_blackBox);
                _blackBox.Dispose();
            }

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
                NativeMemorySentinel.UnregisterNativeArray(_waveUploadScratch);
                _waveUploadScratch.Dispose();
            }

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

        [StructLayout(LayoutKind.Sequential)]
        private struct CausticsWaveGpuData
        {
            public Vector4 WaveA;
            public Vector4 WaveB;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CausticTelemetryEntry
        {
            public uint FrameIndex;
            public uint StateHash;
            public uint ContextHash;
            public uint Flags;
            public float AnchorX;
            public float AnchorY;
            public float AnchorZ;
            public float WaterY;
            public int WaveCount;
            public int DispatchWaveCount;
            public float Intensity;
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
