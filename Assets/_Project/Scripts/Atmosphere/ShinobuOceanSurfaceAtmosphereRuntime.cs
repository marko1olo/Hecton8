using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Atmosphere
{
    [DisallowMultipleComponent]
    public sealed unsafe class ShinobuOceanSurfaceAtmosphereRuntime : MonoBehaviour, IHectonOceanKinematics, IUpdatable, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private static int s_x001ShinobuOceanSurfaceAtmosphereRuntimeSignalPushDropCount;
#if UNITY_EDITOR
        private const int CsvScratchBytes = 16 * 1024;
#endif
        private const int DumpScratchBytes = 32 + (OceanSurfaceAtmosphereConstants.TelemetryFrameCount * 64);
#if UNITY_EDITOR
        private const int LegacyWaveRecordBytes = 20;
#endif
        private const float DefaultFoamThreshold = 0.72f;
        private const float SimulationTickDeltaSeconds = 1f / 60f;
        private const float MinWaveEvaluationHz = 5f;
        private const float MaxWaveEvaluationHz = 60f;
        private const int WaveSamplerThreadGroupSizeFallback = 64;
        private const int TelemetryDumpCooldownFrames = OceanSurfaceAtmosphereConstants.TelemetryFrameCount;
        private const uint QualityStepTuningHash = OceanSurfaceAtmosphereConstants.QualityStepTuningHash;
        private const string WaveHeightSamplerKernelName = "SampleWaveHeights";
        private const string WaveHeightSamplerComputeGuid = "60f3dfa702904496933e12041a3e1764";

        private static readonly int OceanTimeId = Shader.PropertyToID("_H8OceanSurfaceTime");
        private static readonly int OceanQualityId = Shader.PropertyToID("_H8OceanGlobalQualityWeight");
        private static readonly int OceanWaveCountId = Shader.PropertyToID("_H8OceanActiveWaveCount");
        private static readonly int OceanWeatherId = Shader.PropertyToID("_H8OceanWeather");
        private static readonly int OceanRainDisturbanceId = Shader.PropertyToID("_H8OceanRainDisturbance");
        private static readonly int OceanRayleighId = Shader.PropertyToID("_H8OceanRayleighBeta");
        private static readonly int OceanMieId = Shader.PropertyToID("_H8OceanMieBeta");
        private static readonly int OceanScatteringId = Shader.PropertyToID("_H8OceanScatteringParams");
        private static readonly int OceanPlanetId = Shader.PropertyToID("_H8OceanPlanetParams");
        private static readonly int OceanWaveBufferId = Shader.PropertyToID("_H8OceanWaveParameters");
        private static readonly int OceanLodId = Shader.PropertyToID("_H8OceanRadialGridLod");
        private static readonly int OceanLocalProjectionId = Shader.PropertyToID("_H8OceanCameraAupLocalProjection");
        private static readonly int OceanWavePhaseBase0Id = Shader.PropertyToID("_H8OceanWavePhaseBase0");
        private static readonly int OceanWavePhaseBase1Id = Shader.PropertyToID("_H8OceanWavePhaseBase1");
        private static readonly int GlobalFlowVectorId = Shader.PropertyToID("_GlobalFlowVector");
        private static readonly int H8GlobalFlowId = Shader.PropertyToID("_H8GlobalFlow");
        private static readonly int WaveSamplePositionsId = Shader.PropertyToID("_H8WaveSamplePositions");
        private static readonly int WaveSampleResultsId = Shader.PropertyToID("_H8WaveSampleResults");
        private static readonly int WaveSampleCountId = Shader.PropertyToID("_H8WaveSampleCount");
        private static readonly int WaveSampleSeaLevelId = Shader.PropertyToID("_H8WaveSeaLevel");
        private static readonly int WaveSampleLodId = Shader.PropertyToID("_H8WaveSampleLod");

        [Header("Ocean Authority")]
        [Tooltip("Optional camera transform used for AUP-local wave projection and waterline breach checks.")]
        [SerializeField] private Transform cameraTransform;
        [Tooltip("Registers this runtime as the active ocean kinematics provider through the Core OceanKinematicsRuntimeService.")]
        [SerializeField] private bool registerAsOceanAuthority = true;
#if UNITY_EDITOR
        [Tooltip("Editor-only source-data hydration for weather_profiles.csv; player runtime must use baked binary/default rows.")]
        [SerializeField] private bool loadWeatherProfilesCsv = true;
#endif
        [Tooltip("Forces narrative storm surge without waiting for the quest/global-state signal.")]
        [SerializeField] private bool forceStormSurge;
        [Tooltip("Fallback sea level used before the WeatherStateDTO is hydrated.")]
        [SerializeField] private float seaLevel = OceanSurfaceAtmosphereConstants.DefaultSeaLevel;
        [Tooltip("Provider priority for the global ocean kinematics selector. Higher wins.")]
        [SerializeField] private int providerPriority = 170;
        [Header("GPU Height Sampling")]
        [Tooltip("Compute shader that evaluates Gerstner height for the tiny physics query footprint.")]
        [SerializeField] private ComputeShader waveHeightSamplerCompute;
        [SerializeField, Range(0f, 1f)] private float qualityStepLimitMin = 0f;
        [SerializeField, Range(0f, 1f)] private float qualityStepLimitMax = 1f;

        private IDataVault _vault;
        private VaultGenerationHandle<WaveParametersDTO> _waveHandle;
        private VaultGenerationHandle<AtmosphereDTO> _atmosphereHandle;
        private VaultGenerationHandle<WeatherStateDTO> _weatherHandle;
        private VaultGenerationHandle<OceanSurfaceTelemetryEntry> _telemetryHandle;
#if UNITY_EDITOR
        private VaultGenerationHandle<byte> _csvScratchHandle;
#endif
        private VaultGenerationHandle<byte> _dumpScratchHandle;
        private VaultGenerationHandle<OceanSurfaceLodDTO> _lodHandle;
        private VaultGenerationHandle<float4> _readbackQueryHandle;
        private VaultGenerationHandle<float4> _readbackResultHandle;
        private VaultGenerationHandle<float4> _readbackCompletedQueryHandle;
        private VaultGenerationHandle<float4> _readbackRingQueryHandle;
        private VaultGenerationHandle<BeaufortProfileDTO> _beaufortProfileHandle;
        private VaultGenerationHandle<float4> _surfaceSwellHandle;
        private GraphicsBuffer _waveGraphicsBufferA;
        private GraphicsBuffer _waveGraphicsBufferB;
        private GraphicsBuffer _activeWaveGraphicsBuffer;
        private GraphicsBuffer _waveSampleQueryBuffer0;
        private GraphicsBuffer _waveSampleQueryBuffer1;
        private GraphicsBuffer _waveSampleQueryBuffer2;
        private GraphicsBuffer _waveSampleResultBuffer0;
        private GraphicsBuffer _waveSampleResultBuffer1;
        private GraphicsBuffer _waveSampleResultBuffer2;
        private AsyncGPUReadbackRequest _readbackRequest0;
        private AsyncGPUReadbackRequest _readbackRequest1;
        private AsyncGPUReadbackRequest _readbackRequest2;
        private static uint s_wavePayloadMutationVersion;
        private static int s_activeWaveParameterJobCount;
        private float _timeSeconds;
        private float _globalQualityWeight = 1f;
        private float _cachedSeaLevel = OceanSurfaceAtmosphereConstants.DefaultSeaLevel;
        private float3 _cachedSurfaceFlow;
        private bool _registeredUpdate;
        private bool _registeredSlow;
        private bool _registeredLate;
        private bool _registeredOcean;
        private bool _registeredHotSwap;
        private bool _initializedWeather;
#if UNITY_EDITOR
        private bool _loadedCsv;
#endif
        private bool _lastCameraAboveSurface;
        private bool _hasCameraState;
        private bool _cameraTransformResolvedFromPlayer;
        private bool _waveSamplerKernelResolved;
        private int _telemetryCursor;
        private int _lastUploadedWaveCount = -1;
        private int _waveGraphicsBufferWriteIndex;
        private int _waveSamplerKernel = -1;
        private int _waveSamplerThreadGroupSize = WaveSamplerThreadGroupSizeFallback;
        private int _readbackActiveMask;
        private int _readbackWriteIndex;
        private int _readbackFrame0;
        private int _readbackFrame1;
        private int _readbackFrame2;
        private int _readbackCount0;
        private int _readbackCount1;
        private int _readbackCount2;
        private int _queuedReadbackCount;
        private int _queryWriteCursor;
        private int _lastReadbackLatencyFrames;
        private int _lastReadbackSampleCount;
        private long _lastWaveComputeNs;
        private JobHandle _waveParameterJobHandle;
        private uint _lastStateHash;
        private uint _lastUploadedWaveHash;
        private uint _lastPublishedShaderStateHash;
        private uint _lastTelemetryDumpFrame;
        private uint _observedWavePayloadMutationVersion;
        private uint _simulationFrameCounter;
        private float _rawSimulationTimeSeconds;
        private bool _vaultBuffersReady;
        private bool _readbackDispatchEnabled = true;
        private bool _readbackDisposePending;
        private bool _telemetryDumpRequested;
        private bool _waveParameterJobScheduled;
        private bool _waveParameterPayloadDirty = true;
        private bool _shaderGlobalsDirty;
        private bool _waveHeightReadbackDispatchRequested;
        private IPlayerRuntimeContext _playerRuntimeContext;

        public int Priority => providerPriority;

        public bool IsAvailable => ResolveWaveBuffer(out NativeArray<WaveParametersDTO> waves) && waves.Length >= OceanSurfaceAtmosphereConstants.MinQualityWaveCount;

        public float SeaLevel => _cachedSeaLevel;

        public static bool IsWaveParameterMutationLocked => Volatile.Read(ref s_activeWaveParameterJobCount) != 0;

        private void OnEnable()
        {
            _readbackDispatchEnabled = true;
            ConfigureSignalLanes();
            TryRegisterHotSwapListener();
            CachePlayerRuntimeContext(Hecton8.Core.GlobalRegistry.Player);
            CacheDataVaultCold(GlobalRegistry.DataVault);
            ResolveCameraTransformCold();
            EnsureVaultBuffersCold();
            if (!_initializedWeather)
                LoadLegacyWeatherOrGenerateEmergency();

            RefreshCachedSurfaceSnapshot();
            EnsureWaveGraphicsBuffers();
            EnsureWaveReadbackGraphicsBuffers();
            UploadWaveBufferToGpu(false);

            if (registerAsOceanAuthority && !_registeredOcean)
            {
                OceanKinematicsRuntimeService.RegisterProvider(this);
                _registeredOcean = true;
            }

            _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            _registeredSlow = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredLate = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            PublishShaderGlobals();
        }

        private void OnDisable()
        {
            _readbackDispatchEnabled = false;
            CompleteWaveParameterKernelForShutdown();

            if (_registeredOcean)
            {
                OceanKinematicsRuntimeService.UnregisterProvider(this);
                _registeredOcean = false;
            }

            if (_registeredUpdate)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredUpdate = false;
            }

            if (_registeredSlow)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlow = false;
            }

            if (TryDisposeWaveReadbackGraphicsBuffers())
            {
                DisposeWaveGraphicsBuffers();
                if (_registeredLate)
                {
                    GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                    _registeredLate = false;
                }
            }

            TryUnregisterHotSwapListener();
            ClearRuntimePlayerContext();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            qualityStepLimitMin = math.saturate(qualityStepLimitMin);
            qualityStepLimitMax = math.saturate(qualityStepLimitMax);
            if (qualityStepLimitMax < qualityStepLimitMin)
                qualityStepLimitMax = qualityStepLimitMin;

            if (waveHeightSamplerCompute != null)
                return;

            string computePath = UnityEditor.AssetDatabase.GUIDToAssetPath(WaveHeightSamplerComputeGuid);
            if (!string.IsNullOrEmpty(computePath))
                waveHeightSamplerCompute = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(computePath);
        }
#endif

        public void Tick(float deltaTime)
        {
            _ = deltaTime;
            if (!_vaultBuffersReady || _readbackDisposePending)
                return;

            AdvanceSimulationClock(SimulationTickDeltaSeconds);
            _globalQualityWeight = ResolveGlobalQualityWeight();
            _timeSeconds = ResolveWaveEvaluationTime(_rawSimulationTimeSeconds, _globalQualityWeight);

            if (!TryCompleteWaveParameterKernel())
                return;

            EvaluateCameraWaterline();
            RecordTelemetry();
            _shaderGlobalsDirty = true;
            _waveHeightReadbackDispatchRequested = true;
            ScheduleWaveParameterKernel();
        }

        public void SlowTick()
        {
            if (!EnsureVaultBuffersCold())
                return;

            ResolveCameraTransformCold();
            if (!TryCompleteWaveParameterKernel())
                return;

#if UNITY_EDITOR
            if (loadWeatherProfilesCsv && !_loadedCsv)
                _loadedCsv = TryLoadWeatherProfilesCsv();
#endif

            ApplyStormSurgeIfNarrativeRequiresIt();
            _shaderGlobalsDirty = true;
        }

        public void LateFrameTick()
        {
            if (_readbackDisposePending)
            {
                if (TryDisposeWaveReadbackGraphicsBuffers())
                {
                    DisposeWaveGraphicsBuffers();
                    if (_registeredLate && !_registeredUpdate && !_registeredSlow)
                    {
                        GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                        _registeredLate = false;
                    }
                }

                return;
            }

            ConsumeWaveHeightReadbacks();
            EnsureWaveGraphicsBuffers();
            EnsureWaveReadbackGraphicsBuffers();
            UploadWaveBufferToGpu(false);

            if (_lastWaveComputeNs > OceanSurfaceAtmosphereConstants.TelemetryDumpBudgetNs)
                _telemetryDumpRequested = true;
            if (_telemetryDumpRequested && TryDumpTelemetryToDiskThrottled())
                _telemetryDumpRequested = false;

            if (_shaderGlobalsDirty)
            {
                PublishShaderGlobals();
                _shaderGlobalsDirty = false;
            }

            if (_waveHeightReadbackDispatchRequested)
            {
                DispatchWaveHeightReadback();
                _waveHeightReadbackDispatchRequested = false;
            }
        }

        public bool TryGetSurfaceWeatherState(out HectonOceanSurfaceWeatherState state)
        {
            state = default;
            if (!TryCompleteWaveParameterKernel())
                return false;

            if (!ResolveWeather(out WeatherStateDTO weather))
                return false;

            state.WindSpeed = weather.WindDirectionSpeedStorm.z;
            state.FoamStrength = HectonOceanSurfaceMath.ResolveFoamScalar(0.45f, weather.SurfaceScalars.z, _globalQualityWeight);
            state.FoamCoverage = math.saturate(weather.WindDirectionSpeedStorm.w);
            state.FoamScale = math.max(0.01f, weather.SurfaceScalars.y);
            state.Flags = (uint)(
                HectonOceanSurfaceWeatherStateFlags.SupportsWindSpeed |
                HectonOceanSurfaceWeatherStateFlags.SupportsFoamStrength |
                HectonOceanSurfaceWeatherStateFlags.SupportsFoamCoverage |
                HectonOceanSurfaceWeatherStateFlags.SupportsFoamScale);
            return true;
        }

        public bool ApplySurfaceWeatherState(in HectonOceanSurfaceWeatherState state)
        {
            if (!TryCompleteWaveParameterKernel())
                return false;

            if (!ResolveWeatherArray(out NativeArray<WeatherStateDTO> weatherArray))
                return false;

            WeatherStateDTO weather = weatherArray[0];
            if ((state.Flags & (uint)HectonOceanSurfaceWeatherStateFlags.SupportsWindSpeed) != 0u)
                weather.WindDirectionSpeedStorm.z = math.max(0f, state.WindSpeed);
            if ((state.Flags & (uint)HectonOceanSurfaceWeatherStateFlags.SupportsFoamCoverage) != 0u)
                weather.WindDirectionSpeedStorm.w = math.saturate(state.FoamCoverage);
            if ((state.Flags & (uint)HectonOceanSurfaceWeatherStateFlags.SupportsFoamScale) != 0u)
                weather.SurfaceScalars.y = math.max(0.01f, state.FoamScale);
            if ((state.Flags & (uint)HectonOceanSurfaceWeatherStateFlags.SupportsFoamStrength) != 0u)
            weather.SurfaceScalars.z = math.saturate(state.FoamStrength);
            weatherArray[0] = weather;
            RefreshCachedSurfaceSnapshot();
            _shaderGlobalsDirty = true;
            return true;
        }

        public bool TryAssignPrimaryLight(Light primaryLight)
        {
            if (!TryCompleteWaveParameterKernel())
                return false;

            if (primaryLight == null || !ResolveAtmosphereArray(out NativeArray<AtmosphereDTO> atmosphereArray))
                return false;

            AtmosphereDTO dto = atmosphereArray[0];
            dto.ScatteringParams.x = math.max(0f, primaryLight.intensity);
            atmosphereArray[0] = dto;
            _shaderGlobalsDirty = true;
            return true;
        }

        public bool TrySampleWaveHeight(float3 position, float minSpatialLength, out float waterHeight)
        {
            return TrySampleWaveKinematics(position, minSpatialLength, out waterHeight, out _, out _, out _);
        }

        public bool TrySampleSurfaceFlow(float3 position, float minSpatialLength, out float3 surfaceFlow)
        {
            surfaceFlow = ResolveSurfaceFlow();
            return true;
        }

        public bool TrySampleWaterVelocity(float3 position, float minSpatialLength, out float3 waterVelocity)
        {
            waterVelocity = ResolveSurfaceFlow();
            return true;
        }

        public bool TrySampleWaveKinematics(
            float3 position,
            float minSpatialLength,
            out float waterHeight,
            out float3 waveNormal,
            out float3 surfaceVelocity,
            out float3 displacement)
        {
            waterHeight = SeaLevel;
            waveNormal = new float3(0f, 1f, 0f);
            surfaceVelocity = ResolveSurfaceFlow();
            displacement = float3.zero;

            QueueWaveHeightSample(position);
            if (!TryResolveCompletedWaveSample(position, minSpatialLength, out float relativeHeight, out waveNormal))
                return false;

            waterHeight = SeaLevel + relativeHeight;
            displacement.y = relativeHeight;
            return true;
        }

        public bool GetWaterHeight(NativeArray<Vector3> samplePositions, int sampleCount, float minSpatialLength, NativeArray<float> waterHeights)
        {
            if (!samplePositions.IsCreated || !waterHeights.IsCreated || sampleCount <= 0)
                return false;

            int count = math.min(sampleCount, math.min(samplePositions.Length, waterHeights.Length));
            float currentSeaLevel = SeaLevel;
            for (int i = 0; i < count; i++)
            {
                Vector3 position = samplePositions[i];
                float3 sample = new float3(position.x, position.y, position.z);
                QueueWaveHeightSample(sample);
                TryResolveCompletedWaveSample(sample, minSpatialLength, out float relativeHeight, out _);
                waterHeights[i] = currentSeaLevel + relativeHeight;
            }

            return true;
        }

        public bool GetWaterHeight(Vector3[] samplePositions, int sampleCount, float minSpatialLength, float[] waterHeights)
        {
            if (samplePositions == null || waterHeights == null || sampleCount <= 0)
                return false;

            int count = math.min(sampleCount, math.min(samplePositions.Length, waterHeights.Length));
            float currentSeaLevel = SeaLevel;
            for (int i = 0; i < count; i++)
            {
                Vector3 position = samplePositions[i];
                float3 sample = new float3(position.x, position.y, position.z);
                QueueWaveHeightSample(sample);
                TryResolveCompletedWaveSample(sample, minSpatialLength, out float relativeHeight, out _);
                waterHeights[i] = currentSeaLevel + relativeHeight;
            }

            return true;
        }

        public bool GetSurfaceFlow(NativeArray<Vector3> samplePositions, int sampleCount, float minSpatialLength, NativeArray<Vector3> surfaceFlows)
        {
            if (!surfaceFlows.IsCreated || sampleCount <= 0)
                return false;

            int count = math.min(sampleCount, surfaceFlows.Length);
            float3 flow = ResolveSurfaceFlow();
            Vector3 vector = new Vector3(flow.x, flow.y, flow.z);
            for (int i = 0; i < count; i++)
                surfaceFlows[i] = vector;

            return true;
        }

        public bool GetSurfaceFlow(Vector3[] samplePositions, int sampleCount, float minSpatialLength, Vector3[] surfaceFlows)
        {
            if (surfaceFlows == null || sampleCount <= 0)
                return false;

            int count = math.min(sampleCount, surfaceFlows.Length);
            float3 flow = ResolveSurfaceFlow();
            Vector3 vector = new Vector3(flow.x, flow.y, flow.z);
            for (int i = 0; i < count; i++)
                surfaceFlows[i] = vector;

            return true;
        }

        public bool GetWaveNormal(
            NativeArray<Vector3> samplePositions,
            int sampleCount,
            float minSpatialLength,
            NativeArray<Vector3> waveNormals,
            NativeArray<Vector3> surfaceVelocities,
            NativeArray<Vector3> displacements)
        {
            if (!samplePositions.IsCreated || !waveNormals.IsCreated || !surfaceVelocities.IsCreated || !displacements.IsCreated || sampleCount <= 0)
                return false;

            int count = math.min(sampleCount, math.min(samplePositions.Length, math.min(waveNormals.Length, math.min(surfaceVelocities.Length, displacements.Length))));
            float3 flow = ResolveSurfaceFlow();
            Vector3 flowVector = new Vector3(flow.x, flow.y, flow.z);
            for (int i = 0; i < count; i++)
            {
                Vector3 position = samplePositions[i];
                float3 sample = new float3(position.x, position.y, position.z);
                QueueWaveHeightSample(sample);
                TryResolveCompletedWaveSample(sample, minSpatialLength, out float relativeHeight, out float3 normal);
                float3 displacement = new float3(0f, relativeHeight, 0f);
                waveNormals[i] = new Vector3(normal.x, normal.y, normal.z);
                surfaceVelocities[i] = flowVector;
                displacements[i] = new Vector3(displacement.x, displacement.y, displacement.z);
            }

            return true;
        }

        public bool GetWaveNormal(
            Vector3[] samplePositions,
            int sampleCount,
            float minSpatialLength,
            Vector3[] waveNormals,
            Vector3[] surfaceVelocities,
            Vector3[] displacements)
        {
            if (samplePositions == null || waveNormals == null || surfaceVelocities == null || displacements == null || sampleCount <= 0)
                return false;

            int count = math.min(sampleCount, math.min(samplePositions.Length, math.min(waveNormals.Length, math.min(surfaceVelocities.Length, displacements.Length))));
            float3 flow = ResolveSurfaceFlow();
            Vector3 flowVector = new Vector3(flow.x, flow.y, flow.z);
            for (int i = 0; i < count; i++)
            {
                Vector3 position = samplePositions[i];
                float3 sample = new float3(position.x, position.y, position.z);
                QueueWaveHeightSample(sample);
                TryResolveCompletedWaveSample(sample, minSpatialLength, out float relativeHeight, out float3 normal);
                float3 displacement = new float3(0f, relativeHeight, 0f);
                waveNormals[i] = new Vector3(normal.x, normal.y, normal.z);
                surfaceVelocities[i] = flowVector;
                displacements[i] = new Vector3(displacement.x, displacement.y, displacement.z);
            }

            return true;
        }

        public float3 GetFlowAt(float3 position)
        {
            return ResolveSurfaceFlow();
        }

        public float GetWaveHeight(float3 position)
        {
            return TrySampleWaveHeight(position, 1f, out float waterHeight) ? waterHeight : SeaLevel;
        }

        public void AssignWaveHeightSamplerCompute(ComputeShader computeShader)
        {
            waveHeightSamplerCompute = computeShader;
            _waveSamplerKernelResolved = false;
            _waveSamplerKernel = -1;
        }

        public static bool TryGetVaultSnapshot(
            out NativeArray<WaveParametersDTO>.ReadOnly waves,
            out NativeArray<WeatherStateDTO>.ReadOnly weather,
            out NativeArray<AtmosphereDTO>.ReadOnly atmosphere)
        {
            waves = default;
            weather = default;
            atmosphere = default;
            if (!TryResolveRegisteredVault(out IDataVault vault))
                return false;

            bool hasWaves = TryReadExistingVaultView(vault, BufferID.ShinobuOceanWaveParameters, out waves);
            bool hasWeather = TryReadExistingVaultView(vault, BufferID.ShinobuOceanWeatherState, out weather);
            bool hasAtmosphere = TryReadExistingVaultView(vault, BufferID.ShinobuOceanAtmosphere, out atmosphere);
            return hasWaves && hasWeather && hasAtmosphere;
        }

        public static bool TryGetReadbackDebugSnapshot(
            out NativeArray<float4>.ReadOnly completedQueries,
            out NativeArray<float4>.ReadOnly completedResults,
            out NativeArray<OceanSurfaceTelemetryEntry>.ReadOnly telemetry)
        {
            completedQueries = default;
            completedResults = default;
            telemetry = default;
            if (!TryResolveDiagnosticVault(out IDataVault vault))
                return false;

            bool hasQueries = TryReadExistingVaultView(vault, BufferID.ShinobuOceanWaveReadbackCompletedQueries, out completedQueries);
            bool hasResults = TryReadExistingVaultView(vault, BufferID.ShinobuOceanWaveReadbackResults, out completedResults);
            bool hasTelemetry = TryReadExistingVaultView(vault, BufferID.ShinobuOceanTelemetryRing, out telemetry);
            return hasQueries && hasResults && hasTelemetry;
        }

        public static bool TryApplyTunerValues(float windSpeed, float waveSteepness, float gasGiantGlow, float foamThreshold, float qualityMin = 0f, float qualityMax = 1f)
        {
            if (IsWaveParameterMutationLocked)
                return false;

            if (!TryResolveRegisteredVault(out IDataVault vault) ||
                !TryAcquireTunerWriteView(vault, BufferID.ShinobuOceanWaveParameters, OceanSurfaceAtmosphereConstants.WaveCapacity, out VaultGenerationHandle<WaveParametersDTO> wavesHandle, out NativeArray<WaveParametersDTO> waves))
                return false;

            bool weatherLocked = false;
            bool atmosphereLocked = false;
            bool profilesLocked = false;
            VaultGenerationHandle<WeatherStateDTO> weatherHandle = default;
            VaultGenerationHandle<AtmosphereDTO> atmosphereHandle = default;
            VaultGenerationHandle<BeaufortProfileDTO> profilesHandle = default;
            try
            {
                if (!TryAcquireTunerWriteView(vault, BufferID.ShinobuOceanWeatherState, 1, out weatherHandle, out NativeArray<WeatherStateDTO> weather))
                    return false;
                weatherLocked = true;

                if (!TryAcquireTunerWriteView(vault, BufferID.ShinobuOceanAtmosphere, 1, out atmosphereHandle, out NativeArray<AtmosphereDTO> atmosphere))
                    return false;
                atmosphereLocked = true;

                for (int i = 0; i < waves.Length; i++)
                {
                    WaveParametersDTO wave = waves[i];
                    for (int laneIndex = 0; laneIndex < OceanSurfaceAtmosphereConstants.WavesPerParameters; laneIndex++)
                    {
                        float4 lane = HectonOceanSurfaceMath.GetWaveLane(wave, laneIndex);
                        lane.y = math.saturate(waveSteepness);
                        HectonOceanSurfaceMath.SetWaveLane(ref wave, laneIndex, lane);
                    }

                    waves[i] = HectonOceanSurfaceMath.SanitizeWave(wave);
                }

                WeatherStateDTO state = weather[0];
                state.WindDirectionSpeedStorm.z = math.max(0f, windSpeed);
                state.SurfaceScalars.z = math.saturate(foamThreshold);
                weather[0] = state;

                AtmosphereDTO dto = atmosphere[0];
                dto.ScatteringParams.y = math.max(0f, gasGiantGlow);
                atmosphere[0] = dto;

                if (TryAcquireTunerWriteView(vault, BufferID.ShinobuOceanBeaufortProfiles, OceanSurfaceAtmosphereConstants.BeaufortProfileCapacity, out profilesHandle, out NativeArray<BeaufortProfileDTO> profiles))
                {
                    profilesLocked = true;
                    if (profiles.Length > 0)
                    {
                        BeaufortProfileDTO tuning = profiles[0];
                        tuning.StateHash = QualityStepTuningHash;
                        tuning.BaseSteepness = math.saturate(qualityMin);
                        tuning.StormIntensity = math.saturate(waveSteepness);
                        tuning.FoamThreshold = math.saturate(foamThreshold);
                        tuning.FrequencyScale = math.saturate(qualityMax);
                        tuning.Flags = 1u;
                        profiles[0] = tuning;
                    }
                }

                unchecked
                {
                    s_wavePayloadMutationVersion++;
                }

                return true;
            }
            finally
            {
                if (profilesLocked)
                    vault.ReleaseWriteLock(in profilesHandle, SystemID.CoreDiagnostics);
                if (atmosphereLocked)
                    vault.ReleaseWriteLock(in atmosphereHandle, SystemID.CoreDiagnostics);
                if (weatherLocked)
                    vault.ReleaseWriteLock(in weatherHandle, SystemID.CoreDiagnostics);
                vault.ReleaseWriteLock(in wavesHandle, SystemID.CoreDiagnostics);
            }
        }

        private static bool TryResolveRegisteredVault(out IDataVault vault)
        {
            vault = GlobalRegistry.DataVault;
            return vault != null;
        }

        private static bool TryResolveDiagnosticVault(out IDataVault vault)
        {
            return TryResolveRegisteredVault(out vault);
        }

        private static bool TryReadExistingVaultView<T>(IDataVault vault, BufferID bufferId, out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null ||
                !vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle) ||
                !vault.TryReadHandle(in handle, out NativeArray<T> mutableBuffer) ||
                !mutableBuffer.IsCreated)
            {
                return false;
            }

            buffer = mutableBuffer.AsReadOnly();
            return true;
        }

        private static bool TryAcquireTunerWriteView<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            out VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer)
            where T : struct
        {
            handle = default;
            buffer = default;
            int required = math.max(1, requiredLength);
            if (vault == null)
                return false;

            if (vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> existing) &&
                vault.TryReadHandle(in existing, out NativeArray<T> existingBuffer) &&
                existingBuffer.IsCreated &&
                existingBuffer.Length >= required)
            {
                handle = existing;
            }
            else
            {
                if (vault.IsAllocationLocked)
                    return false;

                handle = vault.EnsureGenerationHandle<T>(
                    bufferId,
                    required,
                    SystemID.HabitatAtmosphere,
                    NativeArrayOptions.UninitializedMemory);
            }

            if (!vault.TryAcquireWriteLock(in handle, SystemID.CoreDiagnostics, out buffer))
                return false;

            if (buffer.IsCreated && buffer.Length >= required)
                return true;

            vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            buffer = default;
            return false;
        }

        private static void ConfigureSignalLanes()
        {
            SignalBus<WaterlineBreachSignal>.Configure(
                expectedCapacity: 4,
                maxFrameSignals: 8,
                lowTierFrameSignals: 8,
                laneHash: OceanSurfaceAtmosphereConstants.WaterlineBreachLaneHash);
            SignalBus<WaterlineBreachSignal>.EnsureInitialized();
        }

        private bool EnsureVaultBuffersCold()
        {
            if (_vaultBuffersReady)
                return true;

            IDataVault vault = _vault;
            if (vault == null)
                return false;

            _vault = vault;
            if (!IsHandleValid(in _waveHandle))
                _waveHandle = vault.EnsureGenerationHandle<WaveParametersDTO>(BufferID.ShinobuOceanWaveParameters, OceanSurfaceAtmosphereConstants.WaveCapacity, SystemID.HabitatAtmosphere, NativeArrayOptions.UninitializedMemory);
            if (!IsHandleValid(in _atmosphereHandle))
                _atmosphereHandle = vault.EnsureGenerationHandle<AtmosphereDTO>(BufferID.ShinobuOceanAtmosphere, 1, SystemID.HabitatAtmosphere, NativeArrayOptions.UninitializedMemory);
            if (!IsHandleValid(in _weatherHandle))
                _weatherHandle = vault.EnsureGenerationHandle<WeatherStateDTO>(BufferID.ShinobuOceanWeatherState, 1, SystemID.HabitatAtmosphere, NativeArrayOptions.UninitializedMemory);
            if (!IsHandleValid(in _telemetryHandle))
                _telemetryHandle = vault.EnsureGenerationHandle<OceanSurfaceTelemetryEntry>(BufferID.ShinobuOceanTelemetryRing, OceanSurfaceAtmosphereConstants.TelemetryFrameCount, SystemID.HabitatAtmosphere, NativeArrayOptions.UninitializedMemory);
#if UNITY_EDITOR
            if (!IsHandleValid(in _csvScratchHandle))
                _csvScratchHandle = vault.EnsureGenerationHandle<byte>(BufferID.ShinobuOceanCsvScratch, CsvScratchBytes, SystemID.HabitatAtmosphere, NativeArrayOptions.UninitializedMemory);
#endif
            if (!IsHandleValid(in _dumpScratchHandle))
                _dumpScratchHandle = vault.EnsureGenerationHandle<byte>(BufferID.ShinobuOceanDumpScratch, DumpScratchBytes, SystemID.HabitatAtmosphere, NativeArrayOptions.UninitializedMemory);
            if (!IsHandleValid(in _lodHandle))
                _lodHandle = vault.EnsureGenerationHandle<OceanSurfaceLodDTO>(BufferID.ShinobuOceanLodState, 1, SystemID.HabitatAtmosphere, NativeArrayOptions.UninitializedMemory);
            if (!IsHandleValid(in _readbackQueryHandle))
                _readbackQueryHandle = vault.EnsureGenerationHandle<float4>(BufferID.ShinobuOceanWaveReadbackQueries, OceanSurfaceAtmosphereConstants.WaveReadbackSampleCapacity, SystemID.HabitatAtmosphere, NativeArrayOptions.UninitializedMemory);
            if (!IsHandleValid(in _readbackResultHandle))
                _readbackResultHandle = vault.EnsureGenerationHandle<float4>(BufferID.ShinobuOceanWaveReadbackResults, OceanSurfaceAtmosphereConstants.WaveReadbackSampleCapacity, SystemID.HabitatAtmosphere, NativeArrayOptions.UninitializedMemory);
            if (!IsHandleValid(in _readbackCompletedQueryHandle))
                _readbackCompletedQueryHandle = vault.EnsureGenerationHandle<float4>(BufferID.ShinobuOceanWaveReadbackCompletedQueries, OceanSurfaceAtmosphereConstants.WaveReadbackSampleCapacity, SystemID.HabitatAtmosphere, NativeArrayOptions.UninitializedMemory);
            if (!IsHandleValid(in _readbackRingQueryHandle))
                _readbackRingQueryHandle = vault.EnsureGenerationHandle<float4>(BufferID.ShinobuOceanWaveReadbackRingQueries, OceanSurfaceAtmosphereConstants.WaveReadbackSampleCapacity * OceanSurfaceAtmosphereConstants.WaveReadbackRingSize, SystemID.HabitatAtmosphere, NativeArrayOptions.UninitializedMemory);
            if (!IsHandleValid(in _beaufortProfileHandle))
                _beaufortProfileHandle = vault.EnsureGenerationHandle<BeaufortProfileDTO>(BufferID.ShinobuOceanBeaufortProfiles, OceanSurfaceAtmosphereConstants.BeaufortProfileCapacity, SystemID.HabitatAtmosphere, NativeArrayOptions.UninitializedMemory);
            if (!IsHandleValid(in _surfaceSwellHandle))
                _surfaceSwellHandle = vault.EnsureGenerationHandle<float4>(BufferID.ShinobuOceanSurfaceSwell, 1, SystemID.HabitatAtmosphere, NativeArrayOptions.UninitializedMemory);

            _vaultBuffersReady = ResolveWaveBuffer(out _) && ResolveWeatherArray(out _) && ResolveAtmosphereArray(out _);
            return _vaultBuffersReady;
        }

        private void LoadLegacyWeatherOrGenerateEmergency()
        {
            bool loaded = false;
#if UNITY_EDITOR
            try
            {
                loaded =
                    TryLoadLegacyWeatherFile("Docs/Archive/gerstner_wave_weather.bin") ||
                    TryLoadLegacyWeatherFile("StreamingAssets/gerstner_wave_weather.bin") ||
                    TryLoadLegacyWeatherFile("Assets/StreamingAssets/gerstner_wave_weather.bin") ||
                    TryLoadLegacyWeatherFile("Data/Precomputed/gerstner_wave_weather.bin");
            }
            catch (IOException)
            {
                loaded = false;
            }
            catch (UnauthorizedAccessException)
            {
                loaded = false;
            }
#endif

            if (!loaded)
                GenerateEmergencyMockWeather();
            else
                EnsureAtmosphereDefaults();

            _initializedWeather = true;
            UploadWaveBufferToGpu(false);
        }

        private bool TryLoadLegacyWeatherFile(string relativePath)
        {
#if !UNITY_EDITOR
            return false;
#else
            string root = ResolveProjectRoot();
            if (string.IsNullOrEmpty(root))
                return false;

            string path = Path.Combine(root, relativePath);
            if (!File.Exists(path) || !ResolveWaveBuffer(out NativeArray<WaveParametersDTO> waves))
                return false;

            if (!ResolveCsvScratch(out NativeArray<byte> scratch))
                return false;

            int requiredBytes = OceanSurfaceAtmosphereConstants.MaxWaveOctaves * LegacyWaveRecordBytes;
            int read;
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            read = ReadStreamToScratch(stream, scratch, math.min(requiredBytes, scratch.Length));

            if (read < requiredBytes)
                return false;

            for (int i = 0; i < OceanSurfaceAtmosphereConstants.MaxWaveOctaves; i++)
            {
                int offset = i * LegacyWaveRecordBytes;
                float dirX = ReadFloatLE(scratch, offset);
                float dirZ = ReadFloatLE(scratch, offset + 4);
                float amplitude = math.abs(ReadFloatLE(scratch, offset + 8));
                float wavelength = math.abs(ReadFloatLE(scratch, offset + 12));
                float steepness = math.abs(ReadFloatLE(scratch, offset + 16));
                float safeWavelength = math.max(OceanSurfaceAtmosphereConstants.MinimumWavelength, wavelength);
                float waveNumber = OceanSurfaceAtmosphereConstants.TwoPi / safeWavelength;

                int waveIndex = i / OceanSurfaceAtmosphereConstants.WavesPerParameters;
                int laneIndex = i - (waveIndex * OceanSurfaceAtmosphereConstants.WavesPerParameters);
                WaveParametersDTO wave = waves[waveIndex];
                float resolvedSteepness = math.max(math.saturate(steepness), math.saturate(amplitude * waveNumber));
                float phaseSpeed = math.sqrt(9.81f * waveNumber);
                float4 lane = HectonOceanSurfaceMath.CreateWaveLaneFromDirection(new float2(dirX, dirZ), resolvedSteepness, safeWavelength, phaseSpeed);
                HectonOceanSurfaceMath.SetWaveLane(ref wave, laneIndex, lane);
                waves[waveIndex] = HectonOceanSurfaceMath.SanitizeWave(wave);
            }

            EnsureWeatherDefaultsFromWaves();
            _waveParameterPayloadDirty = true;
            return true;
#endif
        }

        private void GenerateEmergencyMockWeather()
        {
            if (!ResolveWaveBuffer(out NativeArray<WaveParametersDTO> waves) ||
                !ResolveWeatherArray(out NativeArray<WeatherStateDTO> weather) ||
                !ResolveAtmosphereArray(out NativeArray<AtmosphereDTO> atmosphere))
            {
                return;
            }

            NativeArray<float4> swell = default;
            ResolveSurfaceSwellArray(out swell);
            GenerateMockStormJob mockStormJob = new GenerateMockStormJob
            {
                Waves = waves,
                Weather = weather,
                Atmosphere = atmosphere,
                SurfaceSwell = swell,
                SeaLevel = seaLevel,
                TimeSeconds = _timeSeconds,
                GlobalQualityWeight = ResolveGlobalQualityWeight(),
                SimulationFrame = _simulationFrameCounter
            };
            mockStormJob.Execute();
            _waveParameterPayloadDirty = true;
            RefreshCachedSurfaceSnapshot();
        }

        private void EnsureAtmosphereDefaults()
        {
            if (!ResolveAtmosphereArray(out NativeArray<AtmosphereDTO> atmosphere))
                return;

            AtmosphereDTO atmo = atmosphere[0];
            if (!math.all(math.isfinite(atmo.RayleighBeta.xyz)) || math.lengthsq(atmo.RayleighBeta.xyz) <= 0.0000001f)
            {
                atmo.RayleighBeta = new float4(0.0048f, 0.0118f, 0.0285f, 0f);
                atmo.MieBeta = new float4(0.021f, 0.018f, 0.014f, 0.72f);
                atmo.ScatteringParams = new float4(2.4f, 1.15f, 0.82f, 0.34f);
                atmo.PlanetParams = new float4(0.62f, 0.17f, 0.88f, 0f);
                atmosphere[0] = atmo;
            }

            EnsureWeatherDefaultsFromWaves();
        }

        private void EnsureWeatherDefaultsFromWaves()
        {
            if (!ResolveWaveBuffer(out NativeArray<WaveParametersDTO> waves) ||
                !ResolveWeatherArray(out NativeArray<WeatherStateDTO> weather))
            {
                return;
            }

            WeatherStateDTO state = weather[0];
            if (!math.all(math.isfinite(state.WindDirectionSpeedStorm)) || state.WindDirectionSpeedStorm.z <= 0f)
                state.WindDirectionSpeedStorm = new float4(0.78f, 0.62f, 11f, 0.42f);
            if (!math.all(math.isfinite(state.SurfaceScalars)) || state.SurfaceScalars.z <= 0f)
                state.SurfaceScalars = new float4(seaLevel, 1f, DefaultFoamThreshold, 0.25f);
            if (!math.all(math.isfinite(state.SkyTintAndSurge)))
                state.SkyTintAndSurge = new float4(0.33f, 0.21f, 0.48f, 0.12f);

            state.GlobalQualityWeight = ResolveGlobalQualityWeight();
            state.MaxWaveAmplitude = HectonOceanSurfaceMath.ResolveMaxAmplitude(waves);
            weather[0] = state;
        }

        private bool TryLoadWeatherProfilesCsv()
        {
#if !UNITY_EDITOR
            return true;
#else
            if (!ResolveCsvScratch(out NativeArray<byte> scratch) ||
                !ResolveWaveBuffer(out NativeArray<WaveParametersDTO> waves) ||
                !ResolveWeatherArray(out NativeArray<WeatherStateDTO> weather) ||
                !ResolveAtmosphereArray(out NativeArray<AtmosphereDTO> atmosphere))
            {
                return false;
            }

            string root = ResolveProjectRoot();
            if (string.IsNullOrEmpty(root))
                return false;

            string path = Path.Combine(root, "Assets/_SourceData/Atmosphere/weather_profiles.csv");
            if (!File.Exists(path))
                path = Path.Combine(root, "Data/Precomputed/weather_profiles.csv");
            if (!File.Exists(path))
                return TryLoadBeaufortProfilesCsv(root, scratch);

            try
            {
                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                int read = ReadStreamToScratch(stream, scratch, scratch.Length);
                bool changed = OceanWeatherCsvParser.TryApply(scratch, read, waves, weather, atmosphere);
                changed |= TryLoadBeaufortProfilesCsv(root, scratch);
                if (changed)
                {
                    _waveParameterPayloadDirty = true;
                    RefreshCachedSurfaceSnapshot();
                }
                return changed;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
#endif
        }

#if UNITY_EDITOR
        private bool TryLoadBeaufortProfilesCsv(string root, NativeArray<byte> scratch)
        {
            if (string.IsNullOrEmpty(root) ||
                !scratch.IsCreated ||
                !ResolveBeaufortProfiles(out NativeArray<BeaufortProfileDTO> profiles))
            {
                return false;
            }

            string path = Path.Combine(root, "Assets/_SourceData/Atmosphere/beaufort_scale_profiles.csv");
            if (!File.Exists(path))
                path = Path.Combine(root, "Data/Precomputed/beaufort_scale_profiles.csv");
            if (!File.Exists(path))
                return false;

            try
            {
                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                int read = ReadStreamToScratch(stream, scratch, scratch.Length);
                byte* scratchPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch);
                return OceanWeatherCsvParser.TryApplyBeaufort(new ReadOnlySpan<byte>(scratchPtr, read), profiles);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
#endif

#if UNITY_EDITOR
        private int ReadStreamToScratch(FileStream stream, NativeArray<byte> scratch, int maxBytes)
        {
            if (!scratch.IsCreated || maxBytes <= 0)
                return 0;

            int safeBytes = math.min(maxBytes, scratch.Length);
            byte* ptr = (byte*)scratch.GetUnsafePtr();
            Span<byte> span = new Span<byte>(ptr, safeBytes);
            return stream.Read(span);
        }

        private static float ReadFloatLE(NativeArray<byte> bytes, int offset)
        {
            return ReadFloat32(bytes, offset, true);
        }

        private static float ReadFloat32(NativeArray<byte> bytes, int offset, bool sourceLittleEndian)
        {
            if (!bytes.IsCreated || offset < 0 || offset + 3 >= bytes.Length)
                return 0f;

            uint raw =
                bytes[offset] |
                ((uint)bytes[offset + 1] << 8) |
                ((uint)bytes[offset + 2] << 16) |
                ((uint)bytes[offset + 3] << 24);
            if (!sourceLittleEndian)
                raw = ReverseUInt32(raw);

            return math.asfloat(raw);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReverseUInt32(uint value)
        {
            return ((value & 0x000000FFu) << 24) |
                   ((value & 0x0000FF00u) << 8) |
                   ((value & 0x00FF0000u) >> 8) |
                   ((value & 0xFF000000u) >> 24);
        }
#endif

        private void ScheduleWaveParameterKernel()
        {
            if (_waveParameterJobScheduled)
                return;

            if (!ResolveWaveBuffer(out NativeArray<WaveParametersDTO> waves) ||
                !ResolveWeatherArray(out NativeArray<WeatherStateDTO> weather))
            {
                return;
            }

            NativeArray<float4> swell = default;
            NativeArray<BeaufortProfileDTO> tuningProfiles = default;
            ResolveSurfaceSwellArray(out swell);
            ResolveBeaufortProfiles(out tuningProfiles);
            CalculateWaveParametersJob job = new CalculateWaveParametersJob
            {
                Waves = waves,
                Weather = weather,
                SurfaceSwell = swell,
                TuningProfiles = tuningProfiles,
                TimeSeconds = _timeSeconds,
                GlobalQualityWeight = _globalQualityWeight
            };
            _waveParameterJobHandle = job.Schedule();
            _waveParameterJobScheduled = true;
            Interlocked.Increment(ref s_activeWaveParameterJobCount);
        }

        private bool TryCompleteWaveParameterKernel()
        {
            if (!_waveParameterJobScheduled)
                return true;

            if (!_waveParameterJobHandle.IsCompleted)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _waveParameterJobHandle))
                return false;

            _waveParameterJobScheduled = false;
            ReleaseWaveParameterJobLease();
            _waveParameterPayloadDirty = true;
            RefreshCachedSurfaceSnapshot();
            return true;
        }

        private void CompleteWaveParameterKernelForShutdown()
        {
            if (!_waveParameterJobScheduled)
                return;

            DispatcherJobFence.TryComplete(ref _waveParameterJobHandle, forceComplete: true);
            _waveParameterJobScheduled = false;
            ReleaseWaveParameterJobLease();
            _waveParameterPayloadDirty = true;
            RefreshCachedSurfaceSnapshot();
        }

        private void QueueWaveHeightSample(float3 position)
        {
            if (!ResolveReadbackQueries(out NativeArray<float4> queries))
                return;

            int budget = ResolveReadbackSampleBudget(_globalQualityWeight);
            int cursor = _queryWriteCursor;
            if (cursor < 0 || cursor >= budget)
                cursor = 0;

            Vector3 cameraPosition = cameraTransform != null ? cameraTransform.position : Vector3.zero;
            float localX = position.x - cameraPosition.x;
            float localZ = position.z - cameraPosition.z;
            queries[cursor] = new float4(localX, localZ, position.x, position.z);
            _queryWriteCursor = cursor + 1 >= budget ? 0 : cursor + 1;
            _queuedReadbackCount = math.min(budget, math.max(_queuedReadbackCount, cursor + 1));
        }

        private bool TryResolveCompletedWaveSample(float3 position, float minSpatialLength, out float relativeHeight, out float3 normal)
        {
            relativeHeight = 0f;
            normal = new float3(0f, 1f, 0f);
            if (_lastReadbackSampleCount <= 0 ||
                !ResolveReadbackCompletedQueries(out NativeArray<float4> queries) ||
                !ResolveReadbackResults(out NativeArray<float4> results))
            {
                return false;
            }

            float threshold = math.max(0.25f, minSpatialLength);
            float thresholdSq = threshold * threshold;
            int count = math.min(_lastReadbackSampleCount, math.min(queries.Length, results.Length));
            int bestIndex = -1;
            float bestDistanceSq = thresholdSq;
            for (int i = 0; i < count; i++)
            {
                float4 query = queries[i];
                float dx = query.z - position.x;
                float dz = query.w - position.z;
                float distanceSq = (dx * dx) + (dz * dz);
                if (distanceSq <= bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
                return false;

            float4 sample = results[bestIndex];
            if (!math.isfinite(sample.x))
                return false;

            relativeHeight = sample.x;
            float nx = math.isfinite(sample.y) ? sample.y : 0f;
            float nz = math.isfinite(sample.z) ? sample.z : 0f;
            float lateralSq = (nx * nx) + (nz * nz);
            if (lateralSq <= 0.000001f)
            {
                normal = new float3(0f, 1f, 0f);
                return true;
            }

            float ny = math.sqrt(math.max(0.0001f, 1f - math.saturate(lateralSq)));
            normal = math.normalize(new float3(nx, ny, nz));
            return math.all(math.isfinite(normal));
        }

        private void ConsumeWaveHeightReadbacks()
        {
            for (int slot = 0; slot < OceanSurfaceAtmosphereConstants.WaveReadbackRingSize; slot++)
            {
                if (!IsReadbackSlotActive(slot))
                    continue;

                ref AsyncGPUReadbackRequest request = ref ResolveReadbackRequest(slot);
                if (!request.done)
                    continue;

                ClearReadbackSlotActive(slot);
                int readCount = ResolveReadbackCount(slot);
                if (request.hasError || readCount <= 0)
                    continue;

                if (!ResolveReadbackResults(out NativeArray<float4> results) ||
                    !ResolveReadbackCompletedQueries(out NativeArray<float4> completedQueries) ||
                    !ResolveReadbackRingQueries(out NativeArray<float4> ringQueries))
                {
                    continue;
                }

                NativeArray<float4> readbackData = request.GetData<float4>();
                int count = math.min(readCount, math.min(results.Length, readbackData.Length));
                int ringBase = slot * OceanSurfaceAtmosphereConstants.WaveReadbackSampleCapacity;
                for (int i = 0; i < count; i++)
                {
                    results[i] = readbackData[i];
                    completedQueries[i] = ringQueries[ringBase + i];
                }

                _lastReadbackSampleCount = count;
                _lastReadbackLatencyFrames = math.max(0, unchecked((int)_simulationFrameCounter) - ResolveReadbackFrame(slot));
                if (_lastReadbackLatencyFrames > 4)
                    _telemetryDumpRequested = true;
            }
        }

        private void DispatchWaveHeightReadback()
        {
            if (!_readbackDispatchEnabled ||
                !TryResolveWaveSamplerKernel() ||
                !ResolveWaveBuffer(out NativeArray<WaveParametersDTO> waves) ||
                !ResolveReadbackQueries(out NativeArray<float4> queries))
            {
                return;
            }

            int budget = ResolveReadbackSampleBudget(_globalQualityWeight);
            int count = math.min(_queuedReadbackCount, budget);
            if (count <= 0)
            {
                Transform cam = cameraTransform;
                if (cam == null)
                    return;

                Vector3 position = cam.position;
                QueueWaveHeightSample(new float3(position.x, position.y, position.z));
                count = math.min(_queuedReadbackCount, budget);
                if (count <= 0)
                    return;
            }

            if (!HasWaveGraphicsBuffers() || !HasWaveReadbackGraphicsBuffers())
                return;

            double3 cameraAup = ResolveCameraAupDouble();
            float maxWavelength = HectonOceanSurfaceMath.ResolveMaxWavelength(waves);
            int activeWaveCount = HectonOceanSurfaceMath.ResolveFullWaveCount(_globalQualityWeight, OceanSurfaceAtmosphereConstants.MaxWaveOctaves);
            OceanWaveAupPhaseDTO phase = HectonOceanSurfaceMath.ResolveAupPhaseBases(cameraAup, waves, _simulationFrameCounter, _globalQualityWeight, activeWaveCount);
            UploadWaveBufferToGpu(false);
            if (_activeWaveGraphicsBuffer == null)
                return;

            int slot = _readbackWriteIndex;
            if (IsReadbackSlotActive(slot))
                return;

            if (ResolveReadbackRingQueries(out NativeArray<float4> ringQueries))
            {
                int ringBase = slot * OceanSurfaceAtmosphereConstants.WaveReadbackSampleCapacity;
                for (int i = 0; i < count; i++)
                    ringQueries[ringBase + i] = queries[i];
            }

            GraphicsBuffer queryBuffer = ResolveWaveSampleQueryBuffer(slot);
            GraphicsBuffer resultBuffer = ResolveWaveSampleResultBuffer(slot);
            if (queryBuffer == null || resultBuffer == null)
                return;

            GraphicsBufferUploadUtility.UploadNativeArray(queryBuffer, queries, count);
            OceanSurfaceLodDTO lod = HectonOceanSurfaceMath.ResolveRadialGridLod(cameraAup, _globalQualityWeight, maxWavelength);

            waveHeightSamplerCompute.SetBuffer(_waveSamplerKernel, OceanWaveBufferId, _activeWaveGraphicsBuffer);
            waveHeightSamplerCompute.SetBuffer(_waveSamplerKernel, WaveSamplePositionsId, queryBuffer);
            waveHeightSamplerCompute.SetBuffer(_waveSamplerKernel, WaveSampleResultsId, resultBuffer);
            waveHeightSamplerCompute.SetInt(WaveSampleCountId, count);
            waveHeightSamplerCompute.SetFloat(WaveSampleSeaLevelId, SeaLevel);
            waveHeightSamplerCompute.SetFloat(OceanTimeId, _timeSeconds);
            waveHeightSamplerCompute.SetFloat(OceanQualityId, _globalQualityWeight);
            waveHeightSamplerCompute.SetInt(OceanWaveCountId, activeWaveCount);
            waveHeightSamplerCompute.SetVector(OceanLocalProjectionId, ToVector4(lod.CameraAupLocalXZ));
            waveHeightSamplerCompute.SetVector(OceanWavePhaseBase0Id, ToVector4(phase.PhaseBase0));
            waveHeightSamplerCompute.SetVector(OceanWavePhaseBase1Id, ToVector4(phase.PhaseBase1));
            waveHeightSamplerCompute.SetVector(WaveSampleLodId, new Vector4(maxWavelength, activeWaveCount, _globalQualityWeight, _timeSeconds));

            int groupSize = math.max(1, _waveSamplerThreadGroupSize);
            int groupCount = math.max(1, (count + groupSize - 1) / groupSize);
            waveHeightSamplerCompute.Dispatch(_waveSamplerKernel, groupCount, 1, 1);
            ref AsyncGPUReadbackRequest request = ref ResolveReadbackRequest(slot);
            request = AsyncGPUReadback.Request(resultBuffer);
            SetReadbackFrame(slot, unchecked((int)_simulationFrameCounter));
            SetReadbackCount(slot, count);
            SetReadbackSlotActive(slot);
            _readbackWriteIndex = (_readbackWriteIndex + 1) % OceanSurfaceAtmosphereConstants.WaveReadbackRingSize;
            _queuedReadbackCount = 0;
        }

        private bool TryResolveWaveSamplerKernel()
        {
            if (_waveSamplerKernelResolved)
                return _waveSamplerKernel >= 0 && waveHeightSamplerCompute != null;

            _waveSamplerKernelResolved = true;
            _waveSamplerKernel = -1;
            if (waveHeightSamplerCompute == null)
                return false;

            if (!waveHeightSamplerCompute.HasKernel(WaveHeightSamplerKernelName))
                return false;

            _waveSamplerKernel = waveHeightSamplerCompute.FindKernel(WaveHeightSamplerKernelName);
            waveHeightSamplerCompute.GetKernelThreadGroupSizes(_waveSamplerKernel, out uint threadGroupX, out _, out _);
            _waveSamplerThreadGroupSize = threadGroupX > 0u ? math.clamp((int)threadGroupX, 1, 1024) : WaveSamplerThreadGroupSizeFallback;
            return _waveSamplerKernel >= 0;
        }

        private bool EnsureWaveReadbackGraphicsBuffers()
        {
            if (_waveSampleQueryBuffer0 == null)
            {
                // COLD ALLOC: GraphicsBuffer[64 float4] - targeted wave height query upload buffer slot 0 - owner: ShinobuOceanSurfaceAtmosphereRuntime
                _waveSampleQueryBuffer0 = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(OceanSurfaceAtmosphereConstants.WaveReadbackSampleCapacity);
            }

            if (_waveSampleQueryBuffer1 == null)
            {
                // COLD ALLOC: GraphicsBuffer[64 float4] - targeted wave height query upload buffer slot 1 - owner: ShinobuOceanSurfaceAtmosphereRuntime
                _waveSampleQueryBuffer1 = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(OceanSurfaceAtmosphereConstants.WaveReadbackSampleCapacity);
            }

            if (_waveSampleQueryBuffer2 == null)
            {
                // COLD ALLOC: GraphicsBuffer[64 float4] - targeted wave height query upload buffer slot 2 - owner: ShinobuOceanSurfaceAtmosphereRuntime
                _waveSampleQueryBuffer2 = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(OceanSurfaceAtmosphereConstants.WaveReadbackSampleCapacity);
            }

            if (_waveSampleResultBuffer0 == null)
            {
                // COLD ALLOC: GraphicsBuffer[64 float4] - targeted wave height async readback result buffer slot 0 - owner: ShinobuOceanSurfaceAtmosphereRuntime
                _waveSampleResultBuffer0 = new GraphicsBuffer(GraphicsBuffer.Target.Structured, OceanSurfaceAtmosphereConstants.WaveReadbackSampleCapacity, UnsafeUtility.SizeOf<float4>());
            }

            if (_waveSampleResultBuffer1 == null)
            {
                // COLD ALLOC: GraphicsBuffer[64 float4] - targeted wave height async readback result buffer slot 1 - owner: ShinobuOceanSurfaceAtmosphereRuntime
                _waveSampleResultBuffer1 = new GraphicsBuffer(GraphicsBuffer.Target.Structured, OceanSurfaceAtmosphereConstants.WaveReadbackSampleCapacity, UnsafeUtility.SizeOf<float4>());
            }

            if (_waveSampleResultBuffer2 == null)
            {
                // COLD ALLOC: GraphicsBuffer[64 float4] - targeted wave height async readback result buffer slot 2 - owner: ShinobuOceanSurfaceAtmosphereRuntime
                _waveSampleResultBuffer2 = new GraphicsBuffer(GraphicsBuffer.Target.Structured, OceanSurfaceAtmosphereConstants.WaveReadbackSampleCapacity, UnsafeUtility.SizeOf<float4>());
            }

            return _waveSampleQueryBuffer0 != null &&
                _waveSampleQueryBuffer1 != null &&
                _waveSampleQueryBuffer2 != null &&
                _waveSampleResultBuffer0 != null &&
                _waveSampleResultBuffer1 != null &&
                _waveSampleResultBuffer2 != null;
        }

        private bool HasWaveReadbackGraphicsBuffers()
        {
            return _waveSampleQueryBuffer0 != null &&
                _waveSampleQueryBuffer1 != null &&
                _waveSampleQueryBuffer2 != null &&
                _waveSampleResultBuffer0 != null &&
                _waveSampleResultBuffer1 != null &&
                _waveSampleResultBuffer2 != null;
        }

        private bool TryDisposeWaveReadbackGraphicsBuffers()
        {
            ConsumeWaveHeightReadbacks();
            if (HasPendingReadbackRequest())
            {
                _readbackDisposePending = true;
                return false;
            }

            DisposeGraphicsBuffer(ref _waveSampleQueryBuffer0);
            DisposeGraphicsBuffer(ref _waveSampleQueryBuffer1);
            DisposeGraphicsBuffer(ref _waveSampleQueryBuffer2);
            DisposeGraphicsBuffer(ref _waveSampleResultBuffer0);
            DisposeGraphicsBuffer(ref _waveSampleResultBuffer1);
            DisposeGraphicsBuffer(ref _waveSampleResultBuffer2);

            _readbackDisposePending = false;
            _readbackActiveMask = 0;
            _readbackWriteIndex = 0;
            _lastReadbackLatencyFrames = 0;
            _lastReadbackSampleCount = 0;
            _queuedReadbackCount = 0;
            _queryWriteCursor = 0;
            return true;
        }

        private bool HasPendingReadbackRequest()
        {
            for (int slot = 0; slot < OceanSurfaceAtmosphereConstants.WaveReadbackRingSize; slot++)
            {
                if (!IsReadbackSlotActive(slot))
                    continue;

                ref AsyncGPUReadbackRequest request = ref ResolveReadbackRequest(slot);
                if (!request.done)
                    return true;

                ClearReadbackSlotActive(slot);
            }

            return false;
        }

        private static void DisposeGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Dispose();
            buffer = null;
        }

        private GraphicsBuffer ResolveWaveSampleQueryBuffer(int slot)
        {
            if (slot == 0)
                return _waveSampleQueryBuffer0;
            if (slot == 1)
                return _waveSampleQueryBuffer1;
            return _waveSampleQueryBuffer2;
        }

        private GraphicsBuffer ResolveWaveSampleResultBuffer(int slot)
        {
            if (slot == 0)
                return _waveSampleResultBuffer0;
            if (slot == 1)
                return _waveSampleResultBuffer1;
            return _waveSampleResultBuffer2;
        }

        private int ResolveReadbackSampleBudget(float globalQualityWeight)
        {
            float q = HectonOceanSurfaceMath.SanitizeQualityWeight(globalQualityWeight);
            float low = math.min(qualityStepLimitMin, qualityStepLimitMax);
            float high = math.max(qualityStepLimitMin, qualityStepLimitMax);
            if (ResolveBeaufortProfiles(out NativeArray<BeaufortProfileDTO> profiles) &&
                profiles.Length > 0 &&
                profiles[0].StateHash == QualityStepTuningHash)
            {
                low = math.saturate(profiles[0].BaseSteepness);
                high = math.saturate(profiles[0].FrequencyScale);
            }

            q = math.saturate(math.lerp(low, high, q));
            float curve = q * q * (3f - (2f * q));
            return math.clamp((int)math.ceil(math.lerp(4f, OceanSurfaceAtmosphereConstants.WaveReadbackSampleCapacity, curve)), 1, OceanSurfaceAtmosphereConstants.WaveReadbackSampleCapacity);
        }

        private bool IsReadbackSlotActive(int slot)
        {
            return (_readbackActiveMask & (1 << slot)) != 0;
        }

        private void SetReadbackSlotActive(int slot)
        {
            _readbackActiveMask |= 1 << slot;
        }

        private void ClearReadbackSlotActive(int slot)
        {
            _readbackActiveMask &= ~(1 << slot);
        }

        private ref AsyncGPUReadbackRequest ResolveReadbackRequest(int slot)
        {
            if (slot == 0)
                return ref _readbackRequest0;
            if (slot == 1)
                return ref _readbackRequest1;
            return ref _readbackRequest2;
        }

        private int ResolveReadbackFrame(int slot)
        {
            if (slot == 0)
                return _readbackFrame0;
            if (slot == 1)
                return _readbackFrame1;
            return _readbackFrame2;
        }

        private void SetReadbackFrame(int slot, int frame)
        {
            if (slot == 0)
                _readbackFrame0 = frame;
            else if (slot == 1)
                _readbackFrame1 = frame;
            else
                _readbackFrame2 = frame;
        }

        private int ResolveReadbackCount(int slot)
        {
            if (slot == 0)
                return _readbackCount0;
            if (slot == 1)
                return _readbackCount1;
            return _readbackCount2;
        }

        private void SetReadbackCount(int slot, int count)
        {
            if (slot == 0)
                _readbackCount0 = count;
            else if (slot == 1)
                _readbackCount1 = count;
            else
                _readbackCount2 = count;
        }

        private void EvaluateCameraWaterline()
        {
            Transform cam = cameraTransform;
            if (cam == null)
                return;

            Vector3 runtime = cam.position;
            if (!TryResolveAbsoluteFromRuntimeOrigin(runtime, out double3 aup))
                return;

            long start = Stopwatch.GetTimestamp();
            QueueWaveHeightSample(new float3(runtime.x, runtime.y, runtime.z));
            bool hasCompleted = TryResolveCompletedWaveSample(new float3(runtime.x, runtime.y, runtime.z), 6f, out float relativeHeight, out float3 normal);
            long end = Stopwatch.GetTimestamp();
            _lastWaveComputeNs = TicksToNanoseconds(end - start);
            if (!hasCompleted)
                return;

            float surfaceY = SeaLevel + relativeHeight;
            bool aboveSurface = runtime.y > surfaceY;
            if (!_hasCameraState || aboveSurface != _lastCameraAboveSurface)
            {
                WaterlineBreachSignal signal = default;
                signal.CameraAUP = aup;
                signal.RuntimePosition = new float3(runtime.x, runtime.y, runtime.z);
                signal.SurfaceY = surfaceY;
                signal.CameraY = runtime.y;
                signal.Intensity01 = math.saturate(math.abs(runtime.y - surfaceY) * 0.5f);
                signal.SourceId = OceanSurfaceAtmosphereConstants.SourceHash;
                signal.Frame = _simulationFrameCounter;
                signal.IsAboveSurface = aboveSurface ? (byte)1 : (byte)0;
                signal.Flags = math.all(math.isfinite(normal)) ? (byte)1 : (byte)2;
                SignalBus<WaterlineBreachSignal>.TryPushTracked(in signal, ref s_x001ShinobuOceanSurfaceAtmosphereRuntimeSignalPushDropCount);
                _lastCameraAboveSurface = aboveSurface;
                _hasCameraState = true;
            }
        }

        private void ApplyStormSurgeIfNarrativeRequiresIt()
        {
            bool active = forceStormSurge || HasSeedShipNarrativeSignal() || HasSeedShipQuestMask();
            if (!active ||
                !ResolveWaveBuffer(out NativeArray<WaveParametersDTO> waves) ||
                !ResolveWeatherArray(out NativeArray<WeatherStateDTO> weather) ||
                !ResolveAtmosphereArray(out NativeArray<AtmosphereDTO> atmosphere))
            {
                return;
            }

            for (int i = 0; i < waves.Length; i++)
            {
                WaveParametersDTO wave = waves[i];
                for (int laneIndex = 0; laneIndex < OceanSurfaceAtmosphereConstants.WavesPerParameters; laneIndex++)
                {
                    float4 lane = HectonOceanSurfaceMath.GetWaveLane(wave, laneIndex);
                    lane.y = math.max(lane.y, 0.84f);
                    lane.z = math.max(lane.z, 72f + (laneIndex * 36f));
                    HectonOceanSurfaceMath.SetWaveLane(ref wave, laneIndex, lane);
                }

                waves[i] = HectonOceanSurfaceMath.SanitizeWave(wave);
            }

            WeatherStateDTO state = weather[0];
            state.WindDirectionSpeedStorm.z = math.max(state.WindDirectionSpeedStorm.z, 26f);
            state.WindDirectionSpeedStorm.w = 1f;
            state.SurfaceScalars.w = 1f;
            state.SkyTintAndSurge = new float4(0.42f, 0.19f, 0.55f, 1f);
            state.MaxWaveAmplitude = 15f;
            state.StateMask |= 1u << 7;
            weather[0] = state;

            AtmosphereDTO dto = atmosphere[0];
            dto.ScatteringParams.y = math.max(dto.ScatteringParams.y, 2.4f);
            dto.PlanetParams.z = math.max(dto.PlanetParams.z, 1.4f);
            atmosphere[0] = dto;
            RefreshCachedSurfaceSnapshot();
        }

        private bool HasSeedShipNarrativeSignal()
        {
            ReadOnlySpan<NarrativePoiStateSignal> signals = SignalBus<NarrativePoiStateSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                if ((signals[i].StateMask & OceanSurfaceAtmosphereConstants.SeedShipActivatedNarrativeMask) != 0UL)
                    return true;
            }

            return false;
        }

        private bool HasSeedShipQuestMask()
        {
            if (_vault == null || !TryReadExistingVaultView(_vault, BufferID.QuestDagGlobalStateMasks, out NativeArray<ulong>.ReadOnly masks))
                return false;

            for (int i = 0; i < masks.Length; i++)
            {
                if ((masks[i] & OceanSurfaceAtmosphereConstants.SeedShipActivatedNarrativeMask) != 0UL)
                    return true;
            }

            return false;
        }

        private void RecordTelemetry()
        {
            if (!ResolveTelemetry(out NativeArray<OceanSurfaceTelemetryEntry> telemetry) ||
                !ResolveWaveBuffer(out NativeArray<WaveParametersDTO> waves) ||
                !ResolveWeather(out WeatherStateDTO weather))
            {
                return;
            }

            float maxAmplitude = HectonOceanSurfaceMath.ResolveMaxAmplitude(waves);
            int limit = math.min(waves.Length * OceanSurfaceAtmosphereConstants.WavesPerParameters, OceanSurfaceAtmosphereConstants.MaxWaveOctaves);

            OceanSurfaceTelemetryEntry entry = default;
            entry.Frame = _simulationFrameCounter;
            entry.MaxWaveHeight = maxAmplitude;
            entry.StormIntensity = math.saturate(weather.WindDirectionSpeedStorm.w);
            entry.WaveComputeTimeNs = _lastWaveComputeNs;
            entry.GlobalQualityWeight = _globalQualityWeight;
            entry.ActiveWaveCount = HectonOceanSurfaceMath.ResolveFullWaveCount(_globalQualityWeight, limit);
            entry.SurfaceDisturbance = weather.SurfaceScalars.w;
            entry.FoamScalar = math.saturate((weather.WindDirectionSpeedStorm.w + weather.SurfaceScalars.w) * 0.5f);
            entry.LastNormal = new float3(0f, 1f, 0f);
            entry.StateHash = HectonOceanSurfaceMath.HashWaveState(waves, math.min(waves.Length, OceanSurfaceAtmosphereConstants.WaveCapacity), _timeSeconds, _globalQualityWeight);
            entry.Flags = _lastReadbackLatencyFrames > 4 || _lastWaveComputeNs > OceanSurfaceAtmosphereConstants.TelemetryDumpBudgetNs ? 1u : 0u;
            entry.ReadbackLatencyFrames = _lastReadbackLatencyFrames;
            entry.ReadbackSampleCount = _lastReadbackSampleCount;
            _lastStateHash = entry.StateHash;

            int index = math.clamp(_telemetryCursor, 0, telemetry.Length - 1);
            telemetry[index] = entry;
            _telemetryCursor = index + 1 >= telemetry.Length ? 0 : index + 1;
        }

        private bool DumpTelemetryToDisk()
        {
            if (!ResolveTelemetry(out NativeArray<OceanSurfaceTelemetryEntry> telemetry))
                return false;

            try
            {
                string root = ResolveProjectRoot();
                if (string.IsNullOrEmpty(root))
                    return false;

                string path = Path.Combine(root, "Docs/AgentLogs/Dump_SHINOBU_147.bin");
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                Span<byte> header = stackalloc byte[32];
                WriteUInt32LE(header, 0, 0x53555246u);
                WriteUInt32LE(header, 4, 0x36325F57u);
                WriteUInt32LE(header, 8, OceanSurfaceAtmosphereConstants.TelemetryFrameCount);
                WriteUInt32LE(header, 12, unchecked((uint)UnsafeUtility.SizeOf<OceanSurfaceTelemetryEntry>()));
                WriteUInt32LE(header, 16, _lastStateHash);
                WriteUInt32LE(header, 20, unchecked((uint)_telemetryCursor));
                stream.Write(header);

                byte* ptr = (byte*)telemetry.GetUnsafeReadOnlyPtr();
                int byteCount = telemetry.Length * UnsafeUtility.SizeOf<OceanSurfaceTelemetryEntry>();
                stream.Write(new ReadOnlySpan<byte>(ptr, byteCount));
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private bool TryDumpTelemetryToDiskThrottled()
        {
            uint frame = _simulationFrameCounter == 0u ? 1u : _simulationFrameCounter;
            if (_lastTelemetryDumpFrame != 0u && unchecked((int)(frame - _lastTelemetryDumpFrame)) < TelemetryDumpCooldownFrames)
                return false;

            if (!DumpTelemetryToDisk())
                return false;

            _lastTelemetryDumpFrame = frame;
            return true;
        }

        private void PublishShaderGlobals()
        {
            if (!ResolveWaveBuffer(out NativeArray<WaveParametersDTO> waves) ||
                !ResolveWeather(out WeatherStateDTO weather) ||
                !ResolveAtmosphere(out AtmosphereDTO atmosphere))
            {
                return;
            }

            double3 cameraAup = ResolveCameraAupDouble();
            float maxWavelength = HectonOceanSurfaceMath.ResolveMaxWavelength(waves);
            OceanSurfaceLodDTO lod = HectonOceanSurfaceMath.ResolveRadialGridLod(cameraAup, _globalQualityWeight, maxWavelength);
            lod.Frame = _simulationFrameCounter;
            if (ResolveLodArray(out NativeArray<OceanSurfaceLodDTO> lodArray))
                lodArray[0] = lod;

            int activeWaveCount = HectonOceanSurfaceMath.ResolveFullWaveCount(_globalQualityWeight, math.min(waves.Length * OceanSurfaceAtmosphereConstants.WavesPerParameters, OceanSurfaceAtmosphereConstants.MaxWaveOctaves));
            OceanWaveAupPhaseDTO phase = HectonOceanSurfaceMath.ResolveAupPhaseBases(cameraAup, waves, _simulationFrameCounter, _globalQualityWeight, activeWaveCount);
            Vector4 weatherVector = new Vector4(
                weather.WindDirectionSpeedStorm.x,
                weather.WindDirectionSpeedStorm.y,
                weather.WindDirectionSpeedStorm.z,
                weather.WindDirectionSpeedStorm.w);
            Vector4 flowVector = new Vector4(
                weather.WindDirectionSpeedStorm.x * weather.WindDirectionSpeedStorm.z,
                0f,
                weather.WindDirectionSpeedStorm.y * weather.WindDirectionSpeedStorm.z,
                weather.WindDirectionSpeedStorm.w);

            uint shaderStateHash = HashShaderState(
                _timeSeconds,
                _globalQualityWeight,
                activeWaveCount,
                weather,
                atmosphere,
                lod,
                phase);
            if (_lastPublishedShaderStateHash == shaderStateHash)
            {
                UploadWaveBufferToGpu(false);
                BindWaveBufferShaderGlobal();
                return;
            }

            _lastPublishedShaderStateHash = shaderStateHash;

            Shader.SetGlobalFloat(OceanTimeId, _timeSeconds);
            Shader.SetGlobalFloat(OceanQualityId, _globalQualityWeight);
            Shader.SetGlobalInt(OceanWaveCountId, activeWaveCount);
            Shader.SetGlobalVector(OceanWeatherId, weatherVector);
            Shader.SetGlobalVector(OceanRainDisturbanceId, new Vector4(weather.SurfaceScalars.w, weather.SkyTintAndSurge.w, weather.SurfaceScalars.z, 0f));
            Shader.SetGlobalVector(OceanRayleighId, ToVector4(atmosphere.RayleighBeta));
            Shader.SetGlobalVector(OceanMieId, ToVector4(atmosphere.MieBeta));
            Shader.SetGlobalVector(OceanScatteringId, ToVector4(atmosphere.ScatteringParams));
            Shader.SetGlobalVector(OceanPlanetId, ToVector4(atmosphere.PlanetParams));
            Shader.SetGlobalVector(OceanLodId, ToVector4(lod.GridParams));
            Shader.SetGlobalVector(OceanLocalProjectionId, ToVector4(lod.CameraAupLocalXZ));
            Shader.SetGlobalVector(OceanWavePhaseBase0Id, ToVector4(phase.PhaseBase0));
            Shader.SetGlobalVector(OceanWavePhaseBase1Id, ToVector4(phase.PhaseBase1));
            Shader.SetGlobalVector(GlobalFlowVectorId, flowVector);
            Shader.SetGlobalVector(H8GlobalFlowId, flowVector);

            UploadWaveBufferToGpu(false);
            BindWaveBufferShaderGlobal();
        }

        private double3 ResolveCameraAupDouble()
        {
            IPlayerRuntimeContext player = _playerRuntimeContext;
            if (player != null)
            {
                if (player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                    snapshot.Aup.IsFinite())
                {
                    return snapshot.Aup.ToAbsoluteDouble3();
                }

                var playerMovement = player.PlayerMovement;
                if (playerMovement != null)
                {
                    AbsoluteUniversePosition currentAup = playerMovement.CurrentAup;
                    if (currentAup.IsFinite())
                        return currentAup.ToAbsoluteDouble3();
                }
            }

            Hecton8.World.AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            return originAup.IsFinite() ? originAup.ToAbsoluteDouble3() : double3.zero;
        }

        private static bool TryResolveAbsoluteFromRuntimeOrigin(Vector3 runtimePosition, out double3 absolutePosition)
        {
            absolutePosition = default;
            float3 localRuntime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(localRuntime)))
                return false;

            Hecton8.World.AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            absolutePosition = Hecton8.World.AbsoluteUniversePosition.OffsetAbsoluteMeters(
                in originAup,
                new double3(localRuntime.x, localRuntime.y, localRuntime.z));
            return math.all(math.isfinite(absolutePosition));
        }

        private void UploadWaveBufferToGpu(bool allowColdCreate)
        {
            if (!ResolveWaveBuffer(out NativeArray<WaveParametersDTO> waves))
                return;

            ObserveExternalWavePayloadMutation();
            if (_waveGraphicsBufferA == null || _waveGraphicsBufferB == null)
            {
                if (!allowColdCreate || !EnsureWaveGraphicsBuffers())
                    return;
            }

            int count = math.min(waves.Length, OceanSurfaceAtmosphereConstants.WaveCapacity);
            uint waveHash = HashWavePayload(waves, count);
            if (!_waveParameterPayloadDirty && _lastUploadedWaveCount == count && _lastUploadedWaveHash == waveHash)
            {
                if (_activeWaveGraphicsBuffer == null)
                    _activeWaveGraphicsBuffer = _waveGraphicsBufferWriteIndex == 0 ? _waveGraphicsBufferB : _waveGraphicsBufferA;
                return;
            }

            GraphicsBuffer target = _waveGraphicsBufferWriteIndex == 0 ? _waveGraphicsBufferA : _waveGraphicsBufferB;
            GraphicsBufferUploadUtility.UploadNativeArray(target, waves, count);
            _activeWaveGraphicsBuffer = target;
            _waveGraphicsBufferWriteIndex ^= 1;
            _lastUploadedWaveCount = count;
            _lastUploadedWaveHash = waveHash;
            _waveParameterPayloadDirty = false;
        }

        private void BindWaveBufferShaderGlobal()
        {
            if (_activeWaveGraphicsBuffer != null)
                Shader.SetGlobalBuffer(OceanWaveBufferId, _activeWaveGraphicsBuffer);
        }

        private bool EnsureWaveGraphicsBuffers()
        {
            if (_waveGraphicsBufferA == null)
            {
                // COLD ALLOC: GraphicsBuffer[2 WaveParametersDTO] - ocean wave upload buffer A - owner: ShinobuOceanSurfaceAtmosphereRuntime
                _waveGraphicsBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<WaveParametersDTO>(OceanSurfaceAtmosphereConstants.WaveCapacity);
            }

            if (_waveGraphicsBufferB == null)
            {
                // COLD ALLOC: GraphicsBuffer[2 WaveParametersDTO] - ocean wave upload buffer B - owner: ShinobuOceanSurfaceAtmosphereRuntime
                _waveGraphicsBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<WaveParametersDTO>(OceanSurfaceAtmosphereConstants.WaveCapacity);
            }

            return _waveGraphicsBufferA != null && _waveGraphicsBufferB != null;
        }

        private bool HasWaveGraphicsBuffers()
        {
            return _waveGraphicsBufferA != null && _waveGraphicsBufferB != null;
        }

        private void DisposeWaveGraphicsBuffers()
        {
            if (_waveGraphicsBufferA != null)
            {
                _waveGraphicsBufferA.Dispose();
                _waveGraphicsBufferA = null;
            }

            if (_waveGraphicsBufferB != null)
            {
                _waveGraphicsBufferB.Dispose();
                _waveGraphicsBufferB = null;
            }

            _lastUploadedWaveCount = -1;
            _lastUploadedWaveHash = 0u;
            _lastPublishedShaderStateHash = 0u;
            _waveGraphicsBufferWriteIndex = 0;
            _activeWaveGraphicsBuffer = null;
        }

        private float3 ResolveSurfaceFlow()
        {
            return _cachedSurfaceFlow;
        }

        private void RefreshCachedSurfaceSnapshot()
        {
            if (!ResolveWeather(out WeatherStateDTO weather))
            {
                _cachedSeaLevel = math.isfinite(seaLevel) ? seaLevel : OceanSurfaceAtmosphereConstants.DefaultSeaLevel;
                _cachedSurfaceFlow = float3.zero;
                return;
            }

            _cachedSeaLevel = math.isfinite(weather.SurfaceScalars.x) ? weather.SurfaceScalars.x : seaLevel;
            _cachedSurfaceFlow = CalculateSurfaceFlow(weather);
        }

        private static float3 CalculateSurfaceFlow(in WeatherStateDTO weather)
        {
            float2 direction = HectonOceanSurfaceMath.Normalize2OrDefault(weather.WindDirectionSpeedStorm.xy, new float2(1f, 0f));
            float speed = math.max(0f, weather.WindDirectionSpeedStorm.z) * math.lerp(0.08f, 0.42f, math.saturate(weather.WindDirectionSpeedStorm.w));
            return new float3(direction.x * speed, 0f, direction.y * speed);
        }

        private static void ReleaseWaveParameterJobLease()
        {
            if (Volatile.Read(ref s_activeWaveParameterJobCount) > 0)
                Interlocked.Decrement(ref s_activeWaveParameterJobCount);
        }

        private bool ResolveWaveBuffer(out NativeArray<WaveParametersDTO> waves)
        {
            waves = default;
            return _vault != null &&
                   IsHandleValid(in _waveHandle) &&
                   _vault.TryResolveHandle(in _waveHandle, out waves) &&
                   waves.IsCreated;
        }

        private bool ResolveWeatherArray(out NativeArray<WeatherStateDTO> weather)
        {
            weather = default;
            return _vault != null &&
                   IsHandleValid(in _weatherHandle) &&
                   _vault.TryResolveHandle(in _weatherHandle, out weather) &&
                   weather.IsCreated &&
                   weather.Length > 0;
        }

        private bool ResolveAtmosphereArray(out NativeArray<AtmosphereDTO> atmosphere)
        {
            atmosphere = default;
            return _vault != null &&
                   IsHandleValid(in _atmosphereHandle) &&
                   _vault.TryResolveHandle(in _atmosphereHandle, out atmosphere) &&
                   atmosphere.IsCreated &&
                   atmosphere.Length > 0;
        }

        private bool ResolveTelemetry(out NativeArray<OceanSurfaceTelemetryEntry> telemetry)
        {
            telemetry = default;
            return _vault != null &&
                   IsHandleValid(in _telemetryHandle) &&
                   _vault.TryResolveHandle(in _telemetryHandle, out telemetry) &&
                   telemetry.IsCreated &&
                   telemetry.Length > 0;
        }

#if UNITY_EDITOR
        private bool ResolveCsvScratch(out NativeArray<byte> scratch)
        {
            scratch = default;
            return _vault != null &&
                   IsHandleValid(in _csvScratchHandle) &&
                   _vault.TryResolveHandle(in _csvScratchHandle, out scratch) &&
                   scratch.IsCreated &&
                   scratch.Length > 0;
        }
#endif

        private bool ResolveLodArray(out NativeArray<OceanSurfaceLodDTO> lod)
        {
            lod = default;
            return _vault != null &&
                   IsHandleValid(in _lodHandle) &&
                   _vault.TryResolveHandle(in _lodHandle, out lod) &&
                   lod.IsCreated &&
                   lod.Length > 0;
        }

        private bool ResolveReadbackQueries(out NativeArray<float4> queries)
        {
            queries = default;
            return _vault != null &&
                   IsHandleValid(in _readbackQueryHandle) &&
                   _vault.TryResolveHandle(in _readbackQueryHandle, out queries) &&
                   queries.IsCreated &&
                   queries.Length > 0;
        }

        private bool ResolveReadbackResults(out NativeArray<float4> results)
        {
            results = default;
            return _vault != null &&
                   IsHandleValid(in _readbackResultHandle) &&
                   _vault.TryResolveHandle(in _readbackResultHandle, out results) &&
                   results.IsCreated &&
                   results.Length > 0;
        }

        private bool ResolveReadbackCompletedQueries(out NativeArray<float4> queries)
        {
            queries = default;
            return _vault != null &&
                   IsHandleValid(in _readbackCompletedQueryHandle) &&
                   _vault.TryResolveHandle(in _readbackCompletedQueryHandle, out queries) &&
                   queries.IsCreated &&
                   queries.Length > 0;
        }

        private bool ResolveReadbackRingQueries(out NativeArray<float4> queries)
        {
            queries = default;
            return _vault != null &&
                   IsHandleValid(in _readbackRingQueryHandle) &&
                   _vault.TryResolveHandle(in _readbackRingQueryHandle, out queries) &&
                   queries.IsCreated &&
                   queries.Length >= OceanSurfaceAtmosphereConstants.WaveReadbackSampleCapacity * OceanSurfaceAtmosphereConstants.WaveReadbackRingSize;
        }

        private bool ResolveBeaufortProfiles(out NativeArray<BeaufortProfileDTO> profiles)
        {
            profiles = default;
            return _vault != null &&
                   IsHandleValid(in _beaufortProfileHandle) &&
                   _vault.TryResolveHandle(in _beaufortProfileHandle, out profiles) &&
                   profiles.IsCreated &&
                   profiles.Length > 0;
        }

        private bool ResolveSurfaceSwellArray(out NativeArray<float4> swell)
        {
            swell = default;
            return _vault != null &&
                   IsHandleValid(in _surfaceSwellHandle) &&
                   _vault.TryResolveHandle(in _surfaceSwellHandle, out swell) &&
                   swell.IsCreated &&
                   swell.Length > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsHandleValid<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return handle.BufferID != 0u;
        }

        private bool ResolveWeather(out WeatherStateDTO weather)
        {
            weather = default;
            if (!ResolveWeatherArray(out NativeArray<WeatherStateDTO> array))
                return false;

            weather = array[0];
            return true;
        }

        private bool ResolveAtmosphere(out AtmosphereDTO atmosphere)
        {
            atmosphere = default;
            if (!ResolveAtmosphereArray(out NativeArray<AtmosphereDTO> array))
                return false;

            atmosphere = array[0];
            return true;
        }

        private float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return HectonOceanSurfaceMath.SanitizeQualityWeight(weight);
        }

        private void AdvanceSimulationClock(float simulationTickDeltaSeconds)
        {
            _simulationFrameCounter++;
            if (_simulationFrameCounter == 0u)
                _simulationFrameCounter = 1u;

            float safeDelta = math.isfinite(simulationTickDeltaSeconds) ? math.max(0f, simulationTickDeltaSeconds) : SimulationTickDeltaSeconds;
            _rawSimulationTimeSeconds += safeDelta;
            if (!math.isfinite(_rawSimulationTimeSeconds) || _rawSimulationTimeSeconds > 86400f)
                _rawSimulationTimeSeconds = 0f;
        }

        private static float ResolveWaveEvaluationTime(float rawSimulationTimeSeconds, float globalQualityWeight)
        {
            float safeTime = math.max(0f, math.isfinite(rawSimulationTimeSeconds) ? rawSimulationTimeSeconds : 0f);
            float step = ResolveWaveEvaluationStepSeconds(globalQualityWeight);
            return math.floor((safeTime + 0.000001f) / step) * step;
        }

        private static float ResolveWaveEvaluationStepSeconds(float globalQualityWeight)
        {
            float q = HectonOceanSurfaceMath.SanitizeQualityWeight(globalQualityWeight);
            float curve = q * q * (3f - (2f * q));
            float hz = math.lerp(MinWaveEvaluationHz, MaxWaveEvaluationHz, curve);
            return 1f / math.max(MinWaveEvaluationHz, hz);
        }

        private void ResolveCameraTransformCold()
        {
            if (cameraTransform != null && !_cameraTransformResolvedFromPlayer)
                return;

            IPlayerRuntimeContext player = _playerRuntimeContext;
            Camera camera = player != null ? player.PlayerCamera : null;
            if (camera != null)
            {
                cameraTransform = camera.transform;
                _cameraTransformResolvedFromPlayer = true;
            }
            else if (_cameraTransformResolvedFromPlayer)
            {
                cameraTransform = null;
                _cameraTransformResolvedFromPlayer = false;
            }
        }

        private void CachePlayerRuntimeContext(IPlayerRuntimeContext playerContext)
        {
            _playerRuntimeContext = playerContext;
        }

        private void ClearRuntimePlayerContext()
        {
            _playerRuntimeContext = null;
            if (_cameraTransformResolvedFromPlayer)
            {
                cameraTransform = null;
                _cameraTransformResolvedFromPlayer = false;
            }
        }

        private void CacheDataVaultCold(IDataVault vault)
        {
            if (ReferenceEquals(_vault, vault))
                return;

            _vault = vault;
            _vaultBuffersReady = false;
            _waveHandle = default;
            _atmosphereHandle = default;
            _weatherHandle = default;
            _telemetryHandle = default;
#if UNITY_EDITOR
            _csvScratchHandle = default;
#endif
            _dumpScratchHandle = default;
            _lodHandle = default;
            _readbackQueryHandle = default;
            _readbackResultHandle = default;
            _readbackCompletedQueryHandle = default;
            _readbackRingQueryHandle = default;
            _beaufortProfileHandle = default;
            _surfaceSwellHandle = default;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    CachePlayerRuntimeContext(currentService as IPlayerRuntimeContext);
                    ResolveCameraTransformCold();
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    CompleteWaveParameterKernelForShutdown();
                    CacheDataVaultCold(currentService as IDataVault);
                    EnsureVaultBuffersCold();
                    RefreshCachedSurfaceSnapshot();
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    _registeredUpdate = false;
                    _registeredSlow = false;
                    _registeredLate = false;
                    if (currentService != null && isActiveAndEnabled)
                    {
                        if (!_registeredUpdate)
                            _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
                        if (!_registeredSlow)
                            _registeredSlow = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
                        if (!_registeredLate)
                            _registeredLate = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
                    }
                    break;
            }
        }

        private static Vector4 ToVector4(float4 value)
        {
            return new Vector4(value.x, value.y, value.z, value.w);
        }

        private static long TicksToNanoseconds(long ticks)
        {
            return (long)((ticks * 1000000000.0) / Stopwatch.Frequency);
        }

        private static uint HashWavePayload(NativeArray<WaveParametersDTO> waves, int count)
        {
            uint hash = 2166136261u;
            int safeCount = math.min(count, waves.IsCreated ? waves.Length : 0);
            for (int i = 0; i < safeCount; i++)
            {
                WaveParametersDTO wave = waves[i];
                hash = HashFloat4(hash, wave.Wave1);
                hash = HashFloat4(hash, wave.Wave2);
                hash = HashFloat4(hash, wave.Wave3);
                hash = HashFloat4(hash, wave.GlobalWindAndStorm);
            }

            return Hash(hash, (uint)safeCount);
        }

        private void ObserveExternalWavePayloadMutation()
        {
            uint version = s_wavePayloadMutationVersion;
            if (_observedWavePayloadMutationVersion == version)
                return;

            _observedWavePayloadMutationVersion = version;
            _waveParameterPayloadDirty = true;
        }

        private static uint HashShaderState(
            float timeSeconds,
            float globalQualityWeight,
            int activeWaveCount,
            in WeatherStateDTO weather,
            in AtmosphereDTO atmosphere,
            in OceanSurfaceLodDTO lod,
            in OceanWaveAupPhaseDTO phase)
        {
            uint hash = 2166136261u;
            hash = Hash(hash, math.asuint(timeSeconds));
            hash = Hash(hash, math.asuint(globalQualityWeight));
            hash = Hash(hash, unchecked((uint)activeWaveCount));
            hash = Hash(hash, weather.StateMask);
            hash = Hash(hash, weather.Flags);
            hash = HashFloat4(hash, weather.WindDirectionSpeedStorm);
            hash = HashFloat4(hash, weather.SurfaceScalars);
            hash = HashFloat4(hash, weather.SkyTintAndSurge);
            hash = HashFloat4(hash, atmosphere.RayleighBeta);
            hash = HashFloat4(hash, atmosphere.MieBeta);
            hash = HashFloat4(hash, atmosphere.ScatteringParams);
            hash = HashFloat4(hash, atmosphere.PlanetParams);
            hash = HashFloat4(hash, lod.CameraAupLocalXZ);
            hash = HashFloat4(hash, lod.GridParams);
            hash = HashFloat4(hash, lod.RingParams);
            hash = HashFloat4(hash, phase.PhaseBase0);
            hash = HashFloat4(hash, phase.PhaseBase1);
            return hash;
        }

        private static uint HashFloat4(uint hash, float4 value)
        {
            hash = Hash(hash, math.asuint(value.x));
            hash = Hash(hash, math.asuint(value.y));
            hash = Hash(hash, math.asuint(value.z));
            return Hash(hash, math.asuint(value.w));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash(uint current, uint value)
        {
            current ^= value;
            return current * 16777619u;
        }

        private static string ResolveProjectRoot()
        {
            string dataPath = Application.dataPath;
            DirectoryInfo parent = Directory.GetParent(dataPath);
            return parent != null ? parent.FullName : string.Empty;
        }

        private static void WriteUInt32LE(Span<byte> bytes, int offset, uint value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }
    }
}
