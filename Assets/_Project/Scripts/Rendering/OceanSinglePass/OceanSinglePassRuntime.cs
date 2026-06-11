using System;
using System.IO;
using System.Runtime.CompilerServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.VFX;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Hecton8.Rendering.OceanSinglePass
{
    [DisallowMultipleComponent]
    public sealed unsafe class OceanSinglePassRuntime : MonoBehaviour, IDispatcherSystem, IGlobalRegistryHotSwapListener
    {
        private const SystemID OwnerSystemId = SystemID.HabitatAtmosphere;
        private const uint DispatcherSystemHash = 0x53323632u;
        private const string ProductionWorldSceneName = "02_HECTON_WORLD";
        private const string RuntimeRootName = "H8_OceanSinglePassRuntime";

        private static OceanSinglePassRuntime s_runtime;
        private static GraphicsBuffer s_publishedConstantBuffer;
        private static uint s_publishedConstantBufferFrame;
        private static GraphicsBuffer s_publishedWakeEventBuffer;
        private static int s_publishedWakeEventCount;
        private static float4 s_publishedWakeScrollOffset;
        private static bool s_supportsSetConstantBufferCold;
        private static int s_publishedWakeResolution = OceanSinglePassConstants.WakeMinResolution;
        private static float s_publishedWakeResolutionScale = OceanSinglePassConstants.WakeMinResolution * (1f / OceanSinglePassConstants.WakeMaxResolution);
        private static Texture s_publishedWakeTexture;
        private static Vector4 s_publishedWakeTextureParams;
        private static GraphicsBuffer s_mockConstantBuffer;
        private static int s_mockRenderFrameBudget;

        [SerializeField] private Transform cameraAupReference;
        [SerializeField] private bool loadAestheticProfilesCsv = true;
        [SerializeField] private string aestheticProfilesRelativePath = "Assets/_Project/Data/ocean_aesthetic_profiles.csv";

        private IDataVault _vault;
        private VaultGenerationHandle<OceanVisualOverridesDTO> _visualOverridesHandle;
        private VaultGenerationHandle<OceanGuillotineTuningDTO> _tuningHandle;
        private VaultGenerationHandle<OceanRenderTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<OceanAestheticProfileDTO> _profilesHandle;
#if UNITY_EDITOR
        private VaultGenerationHandle<byte> _csvScratchHandle;
#endif
        private VaultGenerationHandle<OceanMockRenderStateDTO> _mockRenderStateHandle;
        private VaultGenerationHandle<PropwashEventDTO> _propwashEventHandle;
        private VaultGenerationHandle<PropwashRingCursorDTO> _propwashCursorHandle;
        private GraphicsBuffer _constantBufferA;
        private GraphicsBuffer _constantBufferB;
        private GraphicsBuffer _activeConstantBuffer;
        private GraphicsBuffer _wakeEventBufferA;
        private GraphicsBuffer _wakeEventBufferB;
        private GraphicsBuffer _activeWakeEventBuffer;
        private IHectonOceanKinematicsService _oceanKinematicsService;
        private ITerrainProvider _terrainProvider;
        private string _projectRootPath;
        private bool _registeredVisualSync;
        private bool _vaultReady;
        private bool _tuningSeeded;
        private bool _telemetrySeeded;
        private bool _telemetryCursorSeeded;
        private bool _profilesSeeded;
        private bool _csvLoaded;
        private int _constantBufferWriteIndex;
        private int _wakeEventBufferWriteIndex;
        private int _activeWakeEventCount;
        private uint _frame;
        private float _lastDepthPassMicroseconds;
        private float _lastWakeComputeMicroseconds;
        private float _lastCpuSubmitMicroseconds;
        private uint _lastRenderGraphFlags;
        private bool _registeredHotSwapListener;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_supportsSetConstantBufferCold = SystemInfo.supportsSetConstantBuffer;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (s_mockConstantBuffer != null && s_mockConstantBuffer.IsValid())
                s_mockConstantBuffer.Release();

            s_runtime = null;
            s_publishedConstantBuffer = null;
            s_publishedConstantBufferFrame = 0u;
            s_publishedWakeEventBuffer = null;
            s_publishedWakeEventCount = 0;
            s_publishedWakeScrollOffset = default;
            s_publishedWakeResolution = OceanSinglePassConstants.WakeMinResolution;
            s_publishedWakeResolutionScale = OceanSinglePassConstants.WakeMinResolution * (1f / OceanSinglePassConstants.WakeMaxResolution);
            s_publishedWakeTexture = null;
            s_publishedWakeTextureParams = default;
            s_mockConstantBuffer = null;
            s_mockRenderFrameBudget = 0;
            PublishFallbackShaderGlobals();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapAfterSceneLoad()
        {
            if (!Application.isPlaying)
                return;

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureSceneRuntime(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            if (!Application.isPlaying)
                return;

            EnsureSceneRuntime(scene);
        }

        private static void EnsureSceneRuntime(Scene scene)
        {
            if (s_runtime != null || !ShouldBootstrapForScene(scene))
                return;

            OceanSinglePassRuntime authoredRuntime = UnityEngine.Object.FindAnyObjectByType<OceanSinglePassRuntime>(FindObjectsInactive.Include);
            if (authoredRuntime != null)
            {
                authoredRuntime.gameObject.SetActive(true);
                authoredRuntime.enabled = true;
                return;
            }

            GameObject host = new GameObject(RuntimeRootName); // COLD ALLOC: GameObject[1] - missing ocean RenderGraph runtime owner fallback - owner: OceanSinglePassRuntime
            host.hideFlags = HideFlags.DontSave;
            host.AddComponent<OceanSinglePassRuntime>(); // COLD ALLOC: OceanSinglePassRuntime[1] - enables single-pass ocean depth/foam route - owner: OceanSinglePassRuntime
        }

        private static bool ShouldBootstrapForScene(Scene scene)
        {
            return scene.IsValid() &&
                   scene.isLoaded &&
                   string.Equals(scene.name, ProductionWorldSceneName, StringComparison.Ordinal);
        }

        private static void PublishFallbackShaderGlobals()
        {
            Shader.SetGlobalTexture(H8OceanSinglePassShaderIds.DepthFoamMaskId, Texture2D.blackTexture);
            Shader.SetGlobalTexture(H8OceanSinglePassShaderIds.WakeTextureId, Texture2D.blackTexture);
        }

        public static bool TryGetActiveConstantBuffer(out GraphicsBuffer constantBuffer, out uint frame)
        {
            GraphicsBuffer buffer = s_publishedConstantBuffer;
            if (buffer != null && buffer.IsValid())
            {
                constantBuffer = buffer;
                frame = s_publishedConstantBufferFrame;
                return true;
            }

            if (s_mockConstantBuffer != null && s_mockConstantBuffer.IsValid())
            {
                constantBuffer = s_mockConstantBuffer;
                frame = uint.MaxValue;
                return true;
            }

            constantBuffer = null;
            frame = 0u;
            return false;
        }

        public static bool HasRendererFeatureRuntimeGate()
        {
            return s_runtime != null || s_mockRenderFrameBudget > 0;
        }

        public static bool TryEnterRenderGraphRuntimeGate()
        {
            return s_runtime != null || ConsumeMockRenderFrameBudget();
        }

        public static bool TryGetWakeEventBuffer(out GraphicsBuffer buffer, out int eventCount)
        {
            GraphicsBuffer published = s_publishedWakeEventBuffer;
            if (published != null && published.IsValid())
            {
                buffer = published;
                eventCount = math.clamp(s_publishedWakeEventCount, 0, OceanSinglePassConstants.WakeEventGpuCapacity);
                return true;
            }

            buffer = null;
            eventCount = 0;
            return false;
        }

        public static bool TryGetWakeState(out int resolution, out float scale, out float4 scrollOffset)
        {
            resolution = math.clamp(s_publishedWakeResolution, OceanSinglePassConstants.WakeMinResolution, OceanSinglePassConstants.WakeMaxResolution);
            scale = math.saturate(s_publishedWakeResolutionScale);
            scrollOffset = s_publishedWakeScrollOffset;
            return resolution > 0;
        }

        public static bool TryGetActiveWakeTexture(out Texture texture, out Vector4 parameters)
        {
            texture = s_publishedWakeTexture;
            parameters = s_publishedWakeTextureParams;
            return texture != null;
        }

        public static bool IsMockRenderStateActive()
        {
            return s_mockRenderFrameBudget > 0;
        }

        public static bool ConsumeMockRenderFrameBudget()
        {
            if (s_mockRenderFrameBudget <= 0)
                return false;

            s_mockRenderFrameBudget--;
            return true;
        }

        public static bool TrySetEditorTuning(float jacobianFoamThreshold, float wakeLifespanSeconds, float shorelineDepthFadeMeters)
        {
            OceanSinglePassRuntime runtime = s_runtime;
            return runtime != null && runtime.TrySetEditorTuningInternal(jacobianFoamThreshold, wakeLifespanSeconds, shorelineDepthFadeMeters);
        }

        public static bool TryReadTelemetry(out NativeArray<OceanRenderTelemetryEntry>.ReadOnly telemetry, out int cursor)
        {
            telemetry = default;
            cursor = 0;
            OceanSinglePassRuntime runtime = s_runtime;
            if (runtime == null ||
                !runtime.TryResolveVaultBuffer(
                    in runtime._telemetryHandle,
                    OceanSinglePassConstants.TelemetryRingBuffer,
                    OceanSinglePassConstants.TelemetryCapacity,
                    out NativeArray<OceanRenderTelemetryEntry> mutableTelemetry))
            {
                return false;
            }

            telemetry = mutableTelemetry.AsReadOnly();
            if (runtime.TryResolveVaultBuffer(
                    in runtime._telemetryCursorHandle,
                    OceanSinglePassConstants.TelemetryCursorBuffer,
                    1,
                    out NativeArray<int> cursorArray))
                cursor = cursorArray[0];
            return true;
        }

        public static void PublishWakeTexture(Texture texture, int resolution, float scale, float4 scrollOffset)
        {
            s_publishedWakeTexture = texture;
            s_publishedWakeResolution = math.clamp(resolution, OceanSinglePassConstants.WakeMinResolution, OceanSinglePassConstants.WakeMaxResolution);
            s_publishedWakeResolutionScale = math.saturate(scale);
            s_publishedWakeScrollOffset = scrollOffset;
            s_publishedWakeTextureParams = new Vector4(
                scrollOffset.x,
                scrollOffset.y,
                OceanSinglePassConstants.WakeTextureWorldSizeMeters,
                s_publishedWakeResolutionScale);
        }

        public static void ReportRenderGraphTelemetry(
            float depthPassMicroseconds,
            float wakeComputeMicroseconds,
            float cpuSubmitMicroseconds,
            uint flags)
        {
            OceanSinglePassRuntime runtime = s_runtime;
            if (runtime == null)
                return;

            if (depthPassMicroseconds >= 0f)
                runtime._lastDepthPassMicroseconds = math.max(0f, depthPassMicroseconds);
            if (wakeComputeMicroseconds >= 0f)
                runtime._lastWakeComputeMicroseconds = math.max(0f, wakeComputeMicroseconds);
            if (cpuSubmitMicroseconds >= 0f)
                runtime._lastCpuSubmitMicroseconds = math.max(0f, cpuSubmitMicroseconds);
            runtime._lastRenderGraphFlags |= flags;
        }

        public static bool GenerateMockOceanRenderState()
        {
            OceanSinglePassRuntime runtime = s_runtime;
            if (runtime != null)
            {
                runtime.CacheColdServices();
                runtime.EnsureVaultState();
                runtime.EnsureGpuBuffersCold();
                if (runtime._vaultReady)
                {
                    DispatcherTimingDTO timing = default;
                    runtime.VisualSyncTick(in timing);
                }
            }

            IDataVault vault = runtime != null ? runtime._vault : GlobalRegistry.DataVault;
            if (vault == null)
                return PublishMockConstantBuffer() ||
                       (runtime != null && s_publishedConstantBuffer != null && s_publishedConstantBuffer.IsValid());

            VaultGenerationHandle<OceanMockRenderStateDTO> handle;
            if (!vault.TryGetGenerationHandle<OceanMockRenderStateDTO>(OceanSinglePassConstants.MockRenderStateBuffer, out handle) ||
                !IsOwnedHandle(in handle, OceanSinglePassConstants.MockRenderStateBuffer))
            {
                if (vault.IsCompactionFenceActive || vault.IsAllocationLocked)
                    return PublishMockConstantBuffer();

                handle = vault.EnsureGenerationHandle<OceanMockRenderStateDTO>(
                    OceanSinglePassConstants.MockRenderStateBuffer,
                    1,
                    OwnerSystemId,
                    NativeArrayOptions.UninitializedMemory);
            }

            if (!IsOwnedHandle(in handle, OceanSinglePassConstants.MockRenderStateBuffer) ||
                !vault.TryResolveHandle(in handle, out NativeArray<OceanMockRenderStateDTO> mockState) ||
                !mockState.IsCreated ||
                mockState.Length <= 0)
            {
                return PublishMockConstantBuffer();
            }

            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(mockState);
            ref OceanMockRenderStateDTO state = ref UnsafeUtility.AsRef<OceanMockRenderStateDTO>(ptr);
            state.PlaneCenterSize = new float4(0f, 0f, 0f, 128f);
            state.CameraLocalAup = new float4(0f, 4f, 0f, 1f);
            state.QualityFoamWakeSea = new float4(1f, 0.68f, 1f, 0f);
            state.Frame++;
            state.Flags = 1u;
            state.StateHash = OceanSinglePassMath.HashTelemetry(state.Frame, OceanSinglePassConstants.WakeMinResolution, 1f, 0f, 0f);
            return PublishMockConstantBuffer();
        }

        private static bool PublishMockConstantBuffer()
        {
            if (!s_supportsSetConstantBufferCold)
                return false;

            if (s_mockConstantBuffer == null || !s_mockConstantBuffer.IsValid())
            {
                s_mockConstantBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Constant,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    OceanSinglePassConstants.CBufferBytes); // COLD ALLOC: GraphicsBuffer[32B] - editor/CI mock ocean constants - owner: SHINOBU_262
            }

            OceanGuillotineTuningDTO tuning = OceanSinglePassMath.CreateDefaultTuning();
            OceanVisualOverridesDTO visual = OceanSinglePassMath.ResolveVisualOverrides(in tuning, 1f);
            NativeArray<OceanVisualOverridesDTO> mapped = s_mockConstantBuffer.LockBufferForWrite<OceanVisualOverridesDTO>(0, 1);
            try
            {
                void* destination = NativeArrayUnsafeUtility.GetUnsafePtr(mapped);
                UnsafeUtility.CopyStructureToPtr(ref visual, destination);
            }
            finally
            {
                s_mockConstantBuffer.UnlockBufferAfterWrite<OceanVisualOverridesDTO>(1);
            }

            s_publishedWakeScrollOffset = default;
            s_publishedWakeResolution = OceanSinglePassConstants.WakeMinResolution;
            s_publishedWakeResolutionScale = OceanSinglePassConstants.WakeMinResolution * (1f / OceanSinglePassConstants.WakeMaxResolution);
            s_mockRenderFrameBudget = 8;
            return true;
        }

        private void Awake()
        {
            if (s_runtime != null && !ReferenceEquals(s_runtime, this))
            {
                Destroy(gameObject);
                return;
            }

            s_runtime = this;
            CacheColdServices();
            EnsureProjectRootPathCold();
            EnsureVaultState();
            EnsureGpuBuffersCold();
        }

        private void OnEnable()
        {
            if (s_runtime != null && !ReferenceEquals(s_runtime, this))
            {
                Destroy(gameObject);
                return;
            }

            s_runtime = this;
            CacheColdServices();
            EnsureProjectRootPathCold();
            EnsureVaultState();
            EnsureGpuBuffersCold();
            TryRegisterHotSwapListener();
            TryRegisterVisualSync();
        }

        private void OnDisable()
        {
            TryUnregisterVisualSync();
            TryUnregisterHotSwapListener();
            if (ReferenceEquals(s_runtime, this))
                s_runtime = null;
            ClearPublishedBuffersIfOwnedByThis();
            _oceanKinematicsService = null;
        }

        private void OnDestroy()
        {
            ShutdownOwnedState();
        }

        public uint GetSystemIdHash()
        {
            return DispatcherSystemHash;
        }

        public DispatcherPhase GetDispatcherPhase()
        {
            return DispatcherPhase.VisualSync;
        }

        public byte GetBucketId()
        {
            return byte.MaxValue;
        }

        public int GetDependencyCount()
        {
            return 0;
        }

        public uint GetDependencyHash(int dependencyIndex)
        {
            return 0u;
        }

        public void PreSimulationTick(in DispatcherTimingDTO timing)
        {
        }

        public JobHandle ScheduleSimulation(
            in DispatcherTimingDTO timing,
            in DispatcherJobContext context,
            JobHandle dependsOn)
        {
            return dependsOn;
        }

        public void PostSimulationTick(in DispatcherTimingDTO timing)
        {
        }

        public void VisualSyncTick(in DispatcherTimingDTO timing)
        {
            if (!_vaultReady)
                return;

            _frame++;
            if (_frame == 0u)
                _frame = 1u;

            RefreshPropwashHandles();
            float quality = ResolveGlobalQualityWeight();
            double3 cameraAup = ResolveCameraAupLocal();
            s_publishedWakeScrollOffset = OceanSinglePassMath.ResolveWakeScrollOffset(cameraAup, OceanSinglePassConstants.WakeTextureWorldSizeMeters);
            s_publishedWakeResolution = OceanSinglePassMath.ResolveWakeResolution(quality);
            s_publishedWakeResolutionScale = OceanSinglePassMath.ResolveWakeResolutionScale(quality);
            double waterSurfaceAupY = ResolveWaterSurfaceAupY();

            if (TryResolveVaultBuffer(in _tuningHandle, OceanSinglePassConstants.TuningBuffer, 1, out NativeArray<OceanGuillotineTuningDTO> tuningArray) &&
                TryResolveVaultBuffer(in _visualOverridesHandle, OceanSinglePassConstants.VisualOverridesBuffer, 1, out NativeArray<OceanVisualOverridesDTO> visualArray))
            {
                void* visualPtr = NativeArrayUnsafeUtility.GetUnsafePtr(visualArray);
                ref OceanVisualOverridesDTO visual = ref UnsafeUtility.AsRef<OceanVisualOverridesDTO>(visualPtr);
                ref OceanGuillotineTuningDTO tuning = ref UnsafeUtility.AsRef<OceanGuillotineTuningDTO>(NativeArrayUnsafeUtility.GetUnsafePtr(tuningArray));
                float waterSurfaceY = (float)waterSurfaceAupY;
                if (math.isfinite(waterSurfaceY) && math.abs(tuning.ShorelineParams.y - waterSurfaceY) > 0.001f)
                {
                    tuning.ShorelineParams.y = waterSurfaceY;
                    tuning.Version = tuning.Version == uint.MaxValue ? 1u : tuning.Version + 1u;
                }

                visual = OceanSinglePassMath.ResolveVisualOverrides(in tuning, quality);
                UploadVisualOverridesToGpu(visualPtr);
            }

            ShorelineFoamGraftRuntime.VisualSyncTick(
                _vault,
                _projectRootPath,
                cameraAup,
                waterSurfaceAupY,
                quality,
                _frame,
                timing.FrameDelta,
                _lastDepthPassMicroseconds);
            UploadPropwashEventsToGpu();
            RecordTelemetry(quality);
        }

        private void CacheColdServices()
        {
            RebindDataVaultForLifecycle(GlobalRegistry.DataVault);
            _oceanKinematicsService = GlobalRegistry.OceanKinematics;
            _terrainProvider = GlobalRegistry.Terrain;
        }

        private void RebindDataVaultForLifecycle(IDataVault vault)
        {
            if (ReferenceEquals(_vault, vault))
                return;

            ShorelineFoamGraftRuntime.Shutdown(_vault);
            ReleaseAllVaultHandles(_vault);
            _vault = vault;
        }

        private void EnsureProjectRootPathCold()
        {
            if (!string.IsNullOrEmpty(_projectRootPath))
                return;

            DirectoryInfo parent = Directory.GetParent(Application.dataPath);
            _projectRootPath = parent != null ? parent.FullName : null;
        }

        private void EnsureVaultState()
        {
            IDataVault vault = _vault;
            if (vault == null)
                return;

            bool hasVisual = AcquireOrRefreshOwnedVaultBuffer(
                OceanSinglePassConstants.VisualOverridesBuffer,
                1,
                NativeArrayOptions.UninitializedMemory,
                ref _visualOverridesHandle,
                out NativeArray<OceanVisualOverridesDTO> visual);
            bool hasTuning = AcquireOrRefreshOwnedVaultBuffer(
                OceanSinglePassConstants.TuningBuffer,
                1,
                NativeArrayOptions.UninitializedMemory,
                ref _tuningHandle,
                out NativeArray<OceanGuillotineTuningDTO> tuning);
            bool hasTelemetry = AcquireOrRefreshOwnedVaultBuffer(
                OceanSinglePassConstants.TelemetryRingBuffer,
                OceanSinglePassConstants.TelemetryCapacity,
                NativeArrayOptions.UninitializedMemory,
                ref _telemetryHandle,
                out NativeArray<OceanRenderTelemetryEntry> telemetry);
            bool hasTelemetryCursor = AcquireOrRefreshOwnedVaultBuffer(
                OceanSinglePassConstants.TelemetryCursorBuffer,
                1,
                NativeArrayOptions.UninitializedMemory,
                ref _telemetryCursorHandle,
                out NativeArray<int> telemetryCursor);
            bool hasProfiles = AcquireOrRefreshOwnedVaultBuffer(
                OceanSinglePassConstants.AestheticProfilesBuffer,
                OceanSinglePassConstants.AestheticProfileCapacity,
                NativeArrayOptions.UninitializedMemory,
                ref _profilesHandle,
                out NativeArray<OceanAestheticProfileDTO> profiles);

            SeedTuningIfNeeded(tuning);
            SeedVisualIfNeeded(visual);
            SeedTelemetryIfNeeded(telemetry);
            SeedTelemetryCursorIfNeeded(telemetryCursor);
            SeedProfilesIfNeeded(profiles);
            LoadAestheticProfilesCsvIfNeeded();
            ShorelineFoamGraftRuntime.EnsureColdState(vault, _projectRootPath);

            _vaultReady = hasVisual &&
                          hasTuning &&
                          hasTelemetry &&
                          hasTelemetryCursor &&
                          hasProfiles &&
                          _tuningSeeded &&
                          _telemetrySeeded &&
                          _telemetryCursorSeeded &&
                          _profilesSeeded;
        }

        private void SeedTuningIfNeeded(NativeArray<OceanGuillotineTuningDTO> tuning)
        {
            if (_tuningSeeded || !tuning.IsCreated || tuning.Length <= 0)
                return;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(tuning);
            ref OceanGuillotineTuningDTO dto = ref UnsafeUtility.AsRef<OceanGuillotineTuningDTO>(ptr);
            dto = OceanSinglePassMath.CreateDefaultTuning();
            _tuningSeeded = true;
        }

        private void SeedVisualIfNeeded(NativeArray<OceanVisualOverridesDTO> visual)
        {
            if (!visual.IsCreated || visual.Length <= 0)
                return;

            if (!_tuningSeeded ||
                !TryResolveVaultBuffer(in _tuningHandle, OceanSinglePassConstants.TuningBuffer, 1, out NativeArray<OceanGuillotineTuningDTO> tuningArray))
                return;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(visual);
            ref OceanVisualOverridesDTO dto = ref UnsafeUtility.AsRef<OceanVisualOverridesDTO>(ptr);
            ref readonly OceanGuillotineTuningDTO tuning = ref UnsafeUtility.AsRef<OceanGuillotineTuningDTO>(NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(tuningArray));
            dto = OceanSinglePassMath.ResolveVisualOverrides(in tuning, ResolveGlobalQualityWeight());
        }

        private void SeedTelemetryIfNeeded(NativeArray<OceanRenderTelemetryEntry> telemetry)
        {
            if (_telemetrySeeded || !telemetry.IsCreated)
                return;

            for (int i = 0; i < telemetry.Length; i++)
                telemetry[i] = default;
            _telemetrySeeded = true;
        }

        private void SeedTelemetryCursorIfNeeded(NativeArray<int> telemetryCursor)
        {
            if (_telemetryCursorSeeded || !telemetryCursor.IsCreated || telemetryCursor.Length <= 0)
                return;

            telemetryCursor[0] = 0;
            _telemetryCursorSeeded = true;
        }

        private void SeedProfilesIfNeeded(NativeArray<OceanAestheticProfileDTO> profiles)
        {
            if (_profilesSeeded || !profiles.IsCreated)
                return;

            for (int i = 0; i < profiles.Length; i++)
                profiles[i] = default;
            _profilesSeeded = true;
        }

        private void LoadAestheticProfilesCsvIfNeeded()
        {
#if !UNITY_EDITOR
            _csvLoaded = true;
            return;
#else
            if (_csvLoaded || !loadAestheticProfilesCsv || string.IsNullOrEmpty(_projectRootPath))
                return;

            EnsureCsvScratch();
            if (!TryResolveVaultBuffer(in _csvScratchHandle, OceanSinglePassConstants.CsvScratchBuffer, OceanSinglePassConstants.CsvScratchBytes, out NativeArray<byte> scratch) ||
                !TryResolveVaultBuffer(in _profilesHandle, OceanSinglePassConstants.AestheticProfilesBuffer, OceanSinglePassConstants.AestheticProfileCapacity, out NativeArray<OceanAestheticProfileDTO> profiles))
            {
                return;
            }

            string path = Path.Combine(_projectRootPath, aestheticProfilesRelativePath);
            if (!File.Exists(path))
            {
                _csvLoaded = true;
                return;
            }

            int byteCount = LoadFileBytesIntoScratch(path, scratch);
            if (byteCount <= 0)
            {
                _csvLoaded = true;
                return;
            }

            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
            ReadOnlySpan<byte> csvBytes = new ReadOnlySpan<byte>(ptr, byteCount);
            OceanAestheticProfileCsvParser.TryParseProfiles(csvBytes, profiles, out _, out _);
            _csvLoaded = true;
#endif
        }

#if UNITY_EDITOR
        private void EnsureCsvScratch()
        {
            if (TryResolveVaultBuffer(in _csvScratchHandle, OceanSinglePassConstants.CsvScratchBuffer, OceanSinglePassConstants.CsvScratchBytes, out NativeArray<byte> _))
                return;

            AcquireOrRefreshOwnedVaultBuffer(
                OceanSinglePassConstants.CsvScratchBuffer,
                OceanSinglePassConstants.CsvScratchBytes,
                NativeArrayOptions.UninitializedMemory,
                ref _csvScratchHandle,
                out NativeArray<byte> _);
        }

        private static int LoadFileBytesIntoScratch(string absolutePath, NativeArray<byte> scratch)
        {
            if (string.IsNullOrEmpty(absolutePath) || !scratch.IsCreated || scratch.Length <= 0 || !File.Exists(absolutePath))
                return 0;

            using FileStream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            int length = (int)math.min(stream.Length, scratch.Length);
            byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
            Span<byte> target = new Span<byte>(destination, length);
            int total = 0;
            while (total < length)
            {
                int read = stream.Read(target.Slice(total));
                if (read <= 0)
                    break;
                total += read;
            }

            return total;
        }
#endif

        private void EnsureGpuBuffersCold()
        {
            if (s_supportsSetConstantBufferCold)
            {
                if (_constantBufferA == null || !_constantBufferA.IsValid())
                {
                    _constantBufferA = new GraphicsBuffer(
                        GraphicsBuffer.Target.Constant,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        1,
                        OceanSinglePassConstants.CBufferBytes); // COLD ALLOC: GraphicsBuffer[32B] - single-pass ocean constants A - owner: SHINOBU_262
                }

                if (_constantBufferB == null || !_constantBufferB.IsValid())
                {
                    _constantBufferB = new GraphicsBuffer(
                        GraphicsBuffer.Target.Constant,
                        GraphicsBuffer.UsageFlags.LockBufferForWrite,
                        1,
                        OceanSinglePassConstants.CBufferBytes); // COLD ALLOC: GraphicsBuffer[32B] - single-pass ocean constants B - owner: SHINOBU_262
                }
            }

            if (_wakeEventBufferA == null || !_wakeEventBufferA.IsValid())
            {
                _wakeEventBufferA = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    OceanSinglePassConstants.WakeEventGpuCapacity,
                    UnsafeUtility.SizeOf<PropwashEventDTO>()); // COLD ALLOC: GraphicsBuffer[PropwashEventDTO x512] - wake event upload A - owner: SHINOBU_262
            }

            if (_wakeEventBufferB == null || !_wakeEventBufferB.IsValid())
            {
                _wakeEventBufferB = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    OceanSinglePassConstants.WakeEventGpuCapacity,
                    UnsafeUtility.SizeOf<PropwashEventDTO>()); // COLD ALLOC: GraphicsBuffer[PropwashEventDTO x512] - wake event upload B - owner: SHINOBU_262
            }
        }

        private void UploadVisualOverridesToGpu(void* visualPtr)
        {
            if (!s_supportsSetConstantBufferCold ||
                _constantBufferA == null ||
                !_constantBufferA.IsValid() ||
                _constantBufferB == null ||
                !_constantBufferB.IsValid() ||
                visualPtr == null)
            {
                return;
            }

            GraphicsBuffer target = _constantBufferWriteIndex == 0 ? _constantBufferA : _constantBufferB;
            _constantBufferWriteIndex ^= 1;
            NativeArray<OceanVisualOverridesDTO> mapped = target.LockBufferForWrite<OceanVisualOverridesDTO>(0, 1);
            try
            {
                void* destination = NativeArrayUnsafeUtility.GetUnsafePtr(mapped);
                UnsafeUtility.MemCpy(destination, visualPtr, OceanSinglePassConstants.CBufferBytes);
            }
            finally
            {
                target.UnlockBufferAfterWrite<OceanVisualOverridesDTO>(1);
            }

            _activeConstantBuffer = target;
            s_publishedConstantBuffer = target;
            s_publishedConstantBufferFrame = _frame;
        }

        private void RefreshPropwashHandles()
        {
            RefreshExternalInputHandle(BufferID.PropwashGpuEventRing, PropwashGpuContracts.EventRingCapacity, ref _propwashEventHandle);
            RefreshExternalInputHandle(BufferID.PropwashGpuRingCursor, 1, ref _propwashCursorHandle);
        }

        private void UploadPropwashEventsToGpu()
        {
            _activeWakeEventCount = 0;
            if (_wakeEventBufferA == null ||
                !_wakeEventBufferA.IsValid() ||
                _wakeEventBufferB == null ||
                !_wakeEventBufferB.IsValid() ||
                !TryResolveExternalVaultBuffer(in _propwashEventHandle, BufferID.PropwashGpuEventRing, 1, out NativeArray<PropwashEventDTO> events))
            {
                s_publishedWakeEventBuffer = null;
                s_publishedWakeEventCount = 0;
                return;
            }

            int count = math.min(events.Length, OceanSinglePassConstants.WakeEventGpuCapacity);
            if (TryResolveExternalVaultBuffer(in _propwashCursorHandle, BufferID.PropwashGpuRingCursor, 1, out NativeArray<PropwashRingCursorDTO> cursorArray))
                count = math.clamp(cursorArray[0].EventCount, 0, math.min(events.Length, OceanSinglePassConstants.WakeEventGpuCapacity));

            if (count <= 0)
            {
                s_publishedWakeEventBuffer = null;
                s_publishedWakeEventCount = 0;
                return;
            }

            GraphicsBuffer target = _wakeEventBufferWriteIndex == 0 ? _wakeEventBufferA : _wakeEventBufferB;
            _wakeEventBufferWriteIndex ^= 1;
            NativeArray<PropwashEventDTO> mapped = target.LockBufferForWrite<PropwashEventDTO>(0, count);
            try
            {
                void* destination = NativeArrayUnsafeUtility.GetUnsafePtr(mapped);
                void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(events);
                UnsafeUtility.MemCpy(destination, source, count * UnsafeUtility.SizeOf<PropwashEventDTO>());
            }
            finally
            {
                target.UnlockBufferAfterWrite<PropwashEventDTO>(count);
            }

            _activeWakeEventBuffer = target;
            _activeWakeEventCount = count;
            s_publishedWakeEventBuffer = target;
            s_publishedWakeEventCount = count;
        }

        private void RecordTelemetry(float quality)
        {
            if (!TryResolveVaultBuffer(in _telemetryHandle, OceanSinglePassConstants.TelemetryRingBuffer, OceanSinglePassConstants.TelemetryCapacity, out NativeArray<OceanRenderTelemetryEntry> telemetry))
                return;

            int cursor = 0;
            if (TryResolveVaultBuffer(in _telemetryCursorHandle, OceanSinglePassConstants.TelemetryCursorBuffer, 1, out NativeArray<int> cursorArray))
            {
                cursor = cursorArray[0];
                cursorArray[0] = WrapIndex(cursor + 1, OceanSinglePassConstants.TelemetryCapacity);
            }

            int slot = WrapIndex(cursor, telemetry.Length);
            OceanRenderTelemetryEntry entry = default;
            entry.Frame = _frame;
            entry.Flags = _lastRenderGraphFlags;
            entry.DepthPassMicroseconds = _lastDepthPassMicroseconds;
            entry.WakeComputeMicroseconds = _lastWakeComputeMicroseconds;
            entry.WakeResolution = s_publishedWakeResolution;
            entry.WakeResolutionScale = s_publishedWakeResolutionScale;
            entry.GlobalQualityWeight = quality;
            entry.WakeEventCount = _activeWakeEventCount;
            entry.WakeScrollOffset = s_publishedWakeScrollOffset;
            entry.StateHash = OceanSinglePassMath.HashTelemetry(
                _frame,
                s_publishedWakeResolution,
                quality,
                _lastDepthPassMicroseconds,
                _lastWakeComputeMicroseconds);
            entry.ProfileHash = OceanSinglePassConstants.LayoutHash;
            entry.CpuSubmitMicroseconds = _lastCpuSubmitMicroseconds;
            telemetry[slot] = entry;

            if (_lastWakeComputeMicroseconds > OceanSinglePassConstants.RenderGraphSpikeDumpThresholdMicroseconds)
                OceanSinglePassTelemetryDump.TryWrite(_projectRootPath, telemetry, slot, OceanSinglePassConstants.TelemetryCapacity);

            _lastRenderGraphFlags = 0u;
        }

        private bool TrySetEditorTuningInternal(float jacobianFoamThreshold, float wakeLifespanSeconds, float shorelineDepthFadeMeters)
        {
            EnsureVaultState();
            if (!TryResolveVaultBuffer(in _tuningHandle, OceanSinglePassConstants.TuningBuffer, 1, out NativeArray<OceanGuillotineTuningDTO> tuningArray))
                return false;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(tuningArray);
            ref OceanGuillotineTuningDTO tuning = ref UnsafeUtility.AsRef<OceanGuillotineTuningDTO>(ptr);
            tuning.FoamParams.x = math.saturate(jacobianFoamThreshold);
            tuning.WakeParams.y = math.clamp(wakeLifespanSeconds, 0.05f, 24f);
            tuning.ShorelineParams.x = math.clamp(shorelineDepthFadeMeters, 0.1f, 128f);
            tuning.Version = tuning.Version == uint.MaxValue ? 1u : tuning.Version + 1u;
            return true;
        }

        private double3 ResolveCameraAupLocal()
        {
            if (cameraAupReference != null)
            {
                Vector3 p = cameraAupReference.position;
                return new double3(p.x, p.y, p.z);
            }

            return default;
        }

        private static float ResolveGlobalQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return OceanSinglePassMath.SanitizeQualityWeight(quality);
        }

        private float ResolveWaterSurfaceAupY()
        {
            if (TryResolveOceanWaterSurfaceAupY(out float oceanWaterSurfaceY))
                return oceanWaterSurfaceY;

            ITerrainProvider terrainProvider = _terrainProvider;
            if (terrainProvider != null && TryResolveWaterSurfaceAupY(terrainProvider.WaterSurfaceLevel, out float terrainWaterSurfaceY))
                return terrainWaterSurfaceY;

            return OceanSinglePassConstants.DefaultSeaLevelMeters;
        }

        private bool TryResolveOceanWaterSurfaceAupY(out float waterSurfaceY)
        {
            IHectonOceanKinematicsService oceanKinematicsService = _oceanKinematicsService;
            IHectonOceanKinematics oceanKinematics = oceanKinematicsService != null && oceanKinematicsService.IsInitialized
                ? oceanKinematicsService.ActiveProvider
                : null;
            if (oceanKinematics != null &&
                oceanKinematics.IsAvailable &&
                TryResolveOceanWaterSurfaceAupY(oceanKinematics.SeaLevel, out waterSurfaceY))
            {
                return true;
            }

            waterSurfaceY = OceanSinglePassConstants.DefaultSeaLevelMeters;
            return false;
        }

        private static bool TryResolveOceanWaterSurfaceAupY(float candidateWaterSurfaceY, out float waterSurfaceY)
        {
            if (math.isfinite(candidateWaterSurfaceY) &&
                math.abs(candidateWaterSurfaceY) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                waterSurfaceY = candidateWaterSurfaceY;
                return true;
            }

            waterSurfaceY = OceanSinglePassConstants.DefaultSeaLevelMeters;
            return false;
        }

        private static bool TryResolveWaterSurfaceAupY(float candidateWaterSurfaceY, out float waterSurfaceY)
        {
            if (math.isfinite(candidateWaterSurfaceY) &&
                math.abs(candidateWaterSurfaceY) > 0.0001f &&
                math.abs(candidateWaterSurfaceY) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                waterSurfaceY = candidateWaterSurfaceY;
                return true;
            }

            waterSurfaceY = OceanSinglePassConstants.DefaultSeaLevelMeters;
            return false;
        }

        private bool TryResolveVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = _vault;
            return vault != null &&
                   requiredLength > 0 &&
                   IsOwnedHandle(in handle, bufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private bool TryResolveExternalVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = _vault;
            return vault != null &&
                   requiredLength > 0 &&
                   IsExternalHandleValid(in handle, bufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private bool AcquireOrRefreshOwnedVaultBuffer<T>(
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            ref VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _vault;
            if (vault == null || requiredLength <= 0)
            {
                handle = default;
                return false;
            }

            if (IsOwnedHandle(in handle, bufferId) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existing) &&
                IsOwnedHandle(in existing, bufferId) &&
                vault.TryResolveHandle(in existing, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                handle = existing;
                return true;
            }

            if (vault.IsCompactionFenceActive || vault.IsAllocationLocked)
                return false;

            if (IsOwnedHandle(in handle, bufferId))
                ReleaseVaultHandle(vault, ref handle, bufferId);

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, OwnerSystemId, options);
            return IsOwnedHandle(in handle, bufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private bool RefreshExternalInputHandle<T>(
            BufferID bufferId,
            int requiredLength,
            ref VaultGenerationHandle<T> handle) where T : struct
        {
            IDataVault vault = _vault;
            if (vault == null || requiredLength <= 0)
            {
                handle = default;
                return false;
            }

            if (IsExternalHandleValid(in handle, bufferId) &&
                vault.TryResolveHandle(in handle, out NativeArray<T> buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            handle = default;
            if (!vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existing) ||
                !IsExternalHandleValid(in existing, bufferId))
            {
                return false;
            }

            handle = existing;
            return vault.TryResolveHandle(in handle, out NativeArray<T> refreshed) &&
                   refreshed.IsCreated &&
                   refreshed.Length >= requiredLength;
        }

        private void TryRegisterVisualSync()
        {
            if (_registeredVisualSync || !Application.isPlaying)
                return;

            _registeredVisualSync = GlobalRegistry.TryRegisterDispatcherSystem(this);
        }

        private void TryUnregisterVisualSync()
        {
            if (!_registeredVisualSync)
                return;

            GlobalRegistry.UnregisterDispatcherSystem(this);
            _registeredVisualSync = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregisterVisualSync();
                if (currentService != null && isActiveAndEnabled)
                    TryRegisterVisualSync();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.TerrainProviderRuntime)
            {
                _terrainProvider = currentService as ITerrainProvider;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.OceanKinematics)
            {
                _oceanKinematicsService = currentService as IHectonOceanKinematicsService;
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            RebindDataVaultForLifecycle(currentService as IDataVault);
            if (_vault == null || !isActiveAndEnabled)
                return;

            EnsureVaultState();
        }

        private void ShutdownOwnedState()
        {
            TryUnregisterVisualSync();
            TryUnregisterHotSwapListener();
            _constantBufferA?.Release();
            _constantBufferB?.Release();
            _wakeEventBufferA?.Release();
            _wakeEventBufferB?.Release();
            _constantBufferA = null;
            _constantBufferB = null;
            _wakeEventBufferA = null;
            _wakeEventBufferB = null;
            _activeConstantBuffer = null;
            _activeWakeEventBuffer = null;
            ClearPublishedBuffersIfOwnedByThis();
            ShorelineFoamGraftRuntime.Shutdown(_vault);
            ReleaseAllVaultHandles(_vault);
            if (ReferenceEquals(s_runtime, this))
                s_runtime = null;
            _oceanKinematicsService = null;
        }

        private void ReleaseAllVaultHandles(IDataVault vault)
        {
            ReleaseVaultHandle(vault, ref _visualOverridesHandle, OceanSinglePassConstants.VisualOverridesBuffer);
            ReleaseVaultHandle(vault, ref _tuningHandle, OceanSinglePassConstants.TuningBuffer);
            ReleaseVaultHandle(vault, ref _telemetryHandle, OceanSinglePassConstants.TelemetryRingBuffer);
            ReleaseVaultHandle(vault, ref _telemetryCursorHandle, OceanSinglePassConstants.TelemetryCursorBuffer);
            ReleaseVaultHandle(vault, ref _profilesHandle, OceanSinglePassConstants.AestheticProfilesBuffer);
#if UNITY_EDITOR
            ReleaseVaultHandle(vault, ref _csvScratchHandle, OceanSinglePassConstants.CsvScratchBuffer);
#endif
            ReleaseVaultHandle(vault, ref _mockRenderStateHandle, OceanSinglePassConstants.MockRenderStateBuffer);
            _propwashEventHandle = default;
            _propwashCursorHandle = default;
            _vaultReady = false;
            _tuningSeeded = false;
            _telemetrySeeded = false;
            _telemetryCursorSeeded = false;
            _profilesSeeded = false;
        }

        private void ClearPublishedBuffersIfOwnedByThis()
        {
            if (ReferenceEquals(s_publishedConstantBuffer, _constantBufferA) ||
                ReferenceEquals(s_publishedConstantBuffer, _constantBufferB))
            {
                s_publishedConstantBuffer = null;
                s_publishedConstantBufferFrame = 0u;
            }

            if (ReferenceEquals(s_publishedWakeEventBuffer, _wakeEventBufferA) ||
                ReferenceEquals(s_publishedWakeEventBuffer, _wakeEventBufferB))
            {
                s_publishedWakeEventBuffer = null;
                s_publishedWakeEventCount = 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsOwnedHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)OwnerSystemId &&
                   handle.Generation != 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsExternalHandleValid<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.Generation != 0u;
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            if (vault != null && IsOwnedHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WrapIndex(int value, int capacity)
        {
            int safeCapacity = math.max(1, capacity);
            int wrapped = value % safeCapacity;
            return wrapped < 0 ? wrapped + safeCapacity : wrapped;
        }
    }
}
