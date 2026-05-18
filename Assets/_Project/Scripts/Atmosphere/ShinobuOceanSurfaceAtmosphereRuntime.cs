using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Physics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Atmosphere
{
    [DisallowMultipleComponent]
    public sealed unsafe class ShinobuOceanSurfaceAtmosphereRuntime : MonoBehaviour, IHectonOceanKinematics, IUpdatable, ISlowTickable, ILateFrameTickable
    {
        private const int CsvScratchBytes = 16 * 1024;
        private const int DumpScratchBytes = 32 + (OceanSurfaceAtmosphereConstants.TelemetryFrameCount * 64);
        private const int LegacyWaveRecordBytes = 20;
        private const float DefaultFoamThreshold = 0.72f;
        private const float SimulationTickDeltaSeconds = 1f / 60f;
        private const float MinWaveEvaluationHz = 5f;
        private const float MaxWaveEvaluationHz = 60f;

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
        private static readonly int GlobalFlowVectorId = Shader.PropertyToID("_GlobalFlowVector");
        private static readonly int H8GlobalFlowId = Shader.PropertyToID("_H8GlobalFlow");

        [Header("Ocean Authority")]
        [Tooltip("Optional camera transform used for AUP-local wave projection and waterline breach checks.")]
        [SerializeField] private Transform cameraTransform;
        [Tooltip("Registers this runtime as the active ocean kinematics provider through the Core OceanKinematicsRuntimeService.")]
        [SerializeField] private bool registerAsOceanAuthority = true;
        [Tooltip("Loads weather_profiles.csv once through the native byte parser when the runtime is active.")]
        [SerializeField] private bool loadWeatherProfilesCsv = true;
        [Tooltip("Forces narrative storm surge without waiting for the quest/global-state signal.")]
        [SerializeField] private bool forceStormSurge;
        [Tooltip("Fallback sea level used before the WeatherStateDTO is hydrated.")]
        [SerializeField] private float seaLevel = OceanSurfaceAtmosphereConstants.DefaultSeaLevel;
        [Tooltip("Provider priority for the global ocean kinematics selector. Higher wins.")]
        [SerializeField] private int providerPriority = 170;

        private IDataVault _vault;
        private VaultBufferHandle<WaveParametersDTO> _waveHandle;
        private VaultBufferHandle<AtmosphereDTO> _atmosphereHandle;
        private VaultBufferHandle<WeatherStateDTO> _weatherHandle;
        private VaultBufferHandle<MockBuoyancyQuery> _mockQueryHandle;
        private VaultBufferHandle<MockBuoyancyResult> _mockResultHandle;
        private VaultBufferHandle<OceanSurfaceTelemetryEntry> _telemetryHandle;
        private VaultBufferHandle<byte> _csvScratchHandle;
        private VaultBufferHandle<byte> _dumpScratchHandle;
        private VaultBufferHandle<OceanSurfaceLodDTO> _lodHandle;
        private GraphicsBuffer _waveGraphicsBufferA;
        private GraphicsBuffer _waveGraphicsBufferB;
        private float _timeSeconds;
        private float _globalQualityWeight = 1f;
        private bool _registeredUpdate;
        private bool _registeredSlow;
        private bool _registeredLate;
        private bool _registeredOcean;
        private bool _initializedWeather;
        private bool _loadedCsv;
        private bool _lastCameraAboveSurface;
        private bool _hasCameraState;
        private int _telemetryCursor;
        private int _lastUploadedWaveCount = -1;
        private int _waveGraphicsBufferWriteIndex;
        private long _lastWaveComputeNs;
        private uint _lastStateHash;
        private uint _lastUploadedWaveHash;
        private uint _lastPublishedShaderStateHash;
        private uint _simulationFrameCounter;
        private float _rawSimulationTimeSeconds;

        public int Priority => providerPriority;

        public bool IsAvailable => ResolveWaveBuffer(out NativeArray<WaveParametersDTO> waves) && waves.Length >= OceanSurfaceAtmosphereConstants.MinQualityWaveCount;

        public float SeaLevel => ResolveWeather(out WeatherStateDTO state) ? state.SurfaceScalars.x : seaLevel;

        private void OnEnable()
        {
            ConfigureSignalLanes();
            ResolveCameraTransformCold();
            EnsureVaultBuffers();
            if (!_initializedWeather)
                LoadLegacyWeatherOrGenerateEmergency();

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

            if (_registeredLate)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLate = false;
            }

            DisposeWaveGraphicsBuffers();
        }

        public void Tick(float deltaTime)
        {
            if (!EnsureVaultBuffers())
                return;

            AdvanceSimulationClock();
            _globalQualityWeight = ResolveGlobalQualityWeight();
            _timeSeconds = ResolveWaveEvaluationTime(_rawSimulationTimeSeconds, _globalQualityWeight);

            EvaluateCameraWaterline();
            RecordTelemetry();
            PublishShaderGlobals();
        }

        public void SlowTick()
        {
            if (!EnsureVaultBuffers())
                return;

            ResolveCameraTransformCold();
            EnsureWaveGraphicsBuffers();
            if (loadWeatherProfilesCsv && !_loadedCsv)
                _loadedCsv = TryLoadWeatherProfilesCsv();

            ApplyStormSurgeIfNarrativeRequiresIt();
            PublishShaderGlobals();
        }

        public void LateFrameTick()
        {
            if (_lastWaveComputeNs > OceanSurfaceAtmosphereConstants.TelemetryDumpBudgetNs)
                DumpTelemetryToDisk();
        }

        public bool TryGetSurfaceWeatherState(out HectonOceanSurfaceWeatherState state)
        {
            state = default;
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
            PublishShaderGlobals();
            return true;
        }

        public bool TryAssignPrimaryLight(Light primaryLight)
        {
            if (primaryLight == null || !ResolveAtmosphereArray(out NativeArray<AtmosphereDTO> atmosphereArray))
                return false;

            AtmosphereDTO dto = atmosphereArray[0];
            dto.ScatteringParams.x = math.max(0f, primaryLight.intensity);
            atmosphereArray[0] = dto;
            PublishShaderGlobals();
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

            if (!ResolveWaveBuffer(out NativeArray<WaveParametersDTO> waves))
                return false;

            double3 aup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(new Vector3(position.x, position.y, position.z));
            HectonOceanSurfaceMath.EvaluateWavesDetailed(
                aup,
                _timeSeconds,
                waves,
                _globalQualityWeight,
                out float relativeHeight,
                out waveNormal,
                out displacement,
                out _,
                out _);
            waterHeight = SeaLevel + relativeHeight;
            return true;
        }

        public bool GetWaterHeight(NativeArray<Vector3> samplePositions, int sampleCount, float minSpatialLength, NativeArray<float> waterHeights)
        {
            if (!samplePositions.IsCreated || !waterHeights.IsCreated || sampleCount <= 0)
                return false;

            int count = math.min(sampleCount, math.min(samplePositions.Length, waterHeights.Length));
            if (!ResolveWaveBuffer(out NativeArray<WaveParametersDTO> waves))
                return false;

            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            float currentSeaLevel = SeaLevel;
            for (int i = 0; i < count; i++)
            {
                Vector3 position = samplePositions[i];
                double3 aup = origin + new double3(position.x, position.y, position.z);
                HectonOceanSurfaceMath.EvaluateWaves(aup, _timeSeconds, waves, _globalQualityWeight, out float relativeHeight, out _);
                waterHeights[i] = currentSeaLevel + relativeHeight;
            }

            return true;
        }

        public bool GetWaterHeight(Vector3[] samplePositions, int sampleCount, float minSpatialLength, float[] waterHeights)
        {
            if (samplePositions == null || waterHeights == null || sampleCount <= 0)
                return false;

            int count = math.min(sampleCount, math.min(samplePositions.Length, waterHeights.Length));
            if (!ResolveWaveBuffer(out NativeArray<WaveParametersDTO> waves))
                return false;

            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            float currentSeaLevel = SeaLevel;
            for (int i = 0; i < count; i++)
            {
                Vector3 position = samplePositions[i];
                double3 aup = origin + new double3(position.x, position.y, position.z);
                HectonOceanSurfaceMath.EvaluateWaves(aup, _timeSeconds, waves, _globalQualityWeight, out float relativeHeight, out _);
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
            if (!ResolveWaveBuffer(out NativeArray<WaveParametersDTO> waves))
                return false;

            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            float3 flow = ResolveSurfaceFlow();
            Vector3 flowVector = new Vector3(flow.x, flow.y, flow.z);
            for (int i = 0; i < count; i++)
            {
                Vector3 position = samplePositions[i];
                double3 aup = origin + new double3(position.x, position.y, position.z);
                HectonOceanSurfaceMath.EvaluateWavesDetailed(
                    aup,
                    _timeSeconds,
                    waves,
                    _globalQualityWeight,
                    out _,
                    out float3 normal,
                    out float3 displacement,
                    out _,
                    out _);
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
            if (!ResolveWaveBuffer(out NativeArray<WaveParametersDTO> waves))
                return false;

            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            float3 flow = ResolveSurfaceFlow();
            Vector3 flowVector = new Vector3(flow.x, flow.y, flow.z);
            for (int i = 0; i < count; i++)
            {
                Vector3 position = samplePositions[i];
                double3 aup = origin + new double3(position.x, position.y, position.z);
                HectonOceanSurfaceMath.EvaluateWavesDetailed(
                    aup,
                    _timeSeconds,
                    waves,
                    _globalQualityWeight,
                    out _,
                    out float3 normal,
                    out float3 displacement,
                    out _,
                    out _);
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

        public static bool TryGetVaultSnapshot(
            out NativeArray<WaveParametersDTO> waves,
            out NativeArray<WeatherStateDTO> weather,
            out NativeArray<AtmosphereDTO> atmosphere)
        {
            waves = default;
            weather = default;
            atmosphere = default;
            if (!TryResolveVault(out IDataVault vault))
                return false;

            bool hasWaves = vault.TryGetBuffer(BufferID.ShinobuOceanWaveParameters, out waves) && waves.IsCreated;
            bool hasWeather = vault.TryGetBuffer(BufferID.ShinobuOceanWeatherState, out weather) && weather.IsCreated;
            bool hasAtmosphere = vault.TryGetBuffer(BufferID.ShinobuOceanAtmosphere, out atmosphere) && atmosphere.IsCreated;
            return hasWaves && hasWeather && hasAtmosphere;
        }

        public static bool TryApplyTunerValues(float windSpeed, float waveSteepness, float gasGiantGlow, float foamThreshold)
        {
            if (!TryResolveVault(out IDataVault vault) ||
                !vault.TryGetBuffer(BufferID.ShinobuOceanWaveParameters, out NativeArray<WaveParametersDTO> waves) ||
                !vault.TryGetBuffer(BufferID.ShinobuOceanWeatherState, out NativeArray<WeatherStateDTO> weather) ||
                !vault.TryGetBuffer(BufferID.ShinobuOceanAtmosphere, out NativeArray<AtmosphereDTO> atmosphere))
            {
                return false;
            }

            for (int i = 0; i < waves.Length; i++)
            {
                WaveParametersDTO wave = waves[i];
                wave.DirectionAndSteepness.w = math.saturate(waveSteepness);
                waves[i] = HectonOceanSurfaceMath.SanitizeWave(wave);
            }

            WeatherStateDTO state = weather[0];
            state.WindDirectionSpeedStorm.z = math.max(0f, windSpeed);
            state.SurfaceScalars.z = math.saturate(foamThreshold);
            weather[0] = state;

            AtmosphereDTO dto = atmosphere[0];
            dto.ScatteringParams.y = math.max(0f, gasGiantGlow);
            atmosphere[0] = dto;
            return true;
        }

        private static bool TryResolveVault(out IDataVault vault)
        {
            vault = GlobalRegistry.DataVault;
            if (vault != null)
                return true;

            if (GlobalDataVault.TryGetLatestCreated(out GlobalDataVault latest))
            {
                vault = latest;
                return true;
            }

            return false;
        }

        private static void ConfigureSignalLanes()
        {
            SignalBus<WaterlineBreachSignal>.Configure(
                expectedCapacity: 4,
                maxFrameSignals: 8,
                lowTierFrameSignals: 2,
                laneHash: OceanSurfaceAtmosphereConstants.WaterlineBreachLaneHash);
            SignalBus<WaterlineBreachSignal>.EnsureInitialized();
        }

        private bool EnsureVaultBuffers()
        {
            if (!TryResolveVault(out IDataVault vault))
                return false;

            _vault = vault;
            if (!_waveHandle.IsCreated)
                _waveHandle = vault.GetBufferHandle<WaveParametersDTO>(BufferID.ShinobuOceanWaveParameters, OceanSurfaceAtmosphereConstants.WaveCapacity, SystemID.HabitatAtmosphere, NativeArrayOptions.UninitializedMemory);
            if (!_atmosphereHandle.IsCreated)
                _atmosphereHandle = vault.GetBufferHandle<AtmosphereDTO>(BufferID.ShinobuOceanAtmosphere, 1, SystemID.HabitatAtmosphere, NativeArrayOptions.UninitializedMemory);
            if (!_weatherHandle.IsCreated)
                _weatherHandle = vault.GetBufferHandle<WeatherStateDTO>(BufferID.ShinobuOceanWeatherState, 1, SystemID.HabitatAtmosphere, NativeArrayOptions.UninitializedMemory);
            if (!_mockQueryHandle.IsCreated)
                _mockQueryHandle = vault.GetBufferHandle<MockBuoyancyQuery>(BufferID.ShinobuOceanMockBuoyancyQueries, OceanSurfaceAtmosphereConstants.MockBuoyancyQueryCount, SystemID.HabitatAtmosphere, NativeArrayOptions.UninitializedMemory);
            if (!_mockResultHandle.IsCreated)
                _mockResultHandle = vault.GetBufferHandle<MockBuoyancyResult>(BufferID.ShinobuOceanMockBuoyancyResults, OceanSurfaceAtmosphereConstants.MockBuoyancyQueryCount, SystemID.HabitatAtmosphere, NativeArrayOptions.UninitializedMemory);
            if (!_telemetryHandle.IsCreated)
                _telemetryHandle = vault.GetBufferHandle<OceanSurfaceTelemetryEntry>(BufferID.ShinobuOceanTelemetryRing, OceanSurfaceAtmosphereConstants.TelemetryFrameCount, SystemID.HabitatAtmosphere, NativeArrayOptions.UninitializedMemory);
            if (!_csvScratchHandle.IsCreated)
                _csvScratchHandle = vault.GetBufferHandle<byte>(BufferID.ShinobuOceanCsvScratch, CsvScratchBytes, SystemID.HabitatAtmosphere, NativeArrayOptions.UninitializedMemory);
            if (!_dumpScratchHandle.IsCreated)
                _dumpScratchHandle = vault.GetBufferHandle<byte>(BufferID.ShinobuOceanDumpScratch, DumpScratchBytes, SystemID.HabitatAtmosphere, NativeArrayOptions.UninitializedMemory);
            if (!_lodHandle.IsCreated)
                _lodHandle = vault.GetBufferHandle<OceanSurfaceLodDTO>(BufferID.ShinobuOceanLodState, 1, SystemID.HabitatAtmosphere, NativeArrayOptions.UninitializedMemory);

            return ResolveWaveBuffer(out _) && ResolveWeatherArray(out _) && ResolveAtmosphereArray(out _);
        }

        private void LoadLegacyWeatherOrGenerateEmergency()
        {
            bool loaded = false;
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

            if (!loaded)
                GenerateEmergencyMockWeather();
            else
                EnsureAtmosphereDefaults();

            _initializedWeather = true;
            UploadWaveBufferToGpu(true);
        }

        private bool TryLoadLegacyWeatherFile(string relativePath)
        {
            string root = ResolveProjectRoot();
            if (string.IsNullOrEmpty(root))
                return false;

            string path = Path.Combine(root, relativePath);
            if (!File.Exists(path) || !ResolveWaveBuffer(out NativeArray<WaveParametersDTO> waves))
                return false;

            if (!ResolveCsvScratch(out NativeArray<byte> scratch))
                return false;

            int requiredBytes = OceanSurfaceAtmosphereConstants.WaveCapacity * LegacyWaveRecordBytes;
            int read;
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            read = ReadStreamToScratch(stream, scratch, math.min(requiredBytes, scratch.Length));

            if (read < requiredBytes)
                return false;

            for (int i = 0; i < OceanSurfaceAtmosphereConstants.WaveCapacity; i++)
            {
                int offset = i * LegacyWaveRecordBytes;
                float dirX = ReadFloatLE(scratch, offset);
                float dirZ = ReadFloatLE(scratch, offset + 4);
                float amplitude = math.abs(ReadFloatLE(scratch, offset + 8));
                float wavelength = math.abs(ReadFloatLE(scratch, offset + 12));
                float steepness = math.abs(ReadFloatLE(scratch, offset + 16));
                float safeWavelength = math.max(OceanSurfaceAtmosphereConstants.MinimumWavelength, wavelength);
                float waveNumber = OceanSurfaceAtmosphereConstants.TwoPi / safeWavelength;

                WaveParametersDTO wave = default;
                wave.DirectionAndSteepness = new float4(dirX, dirZ, i * 0.6180339f, math.saturate(steepness));
                wave.PhaseSpeed = math.sqrt(9.81f * waveNumber);
                wave.Amplitude = math.max(0.01f, amplitude);
                wave.Wavelength = safeWavelength;
                waves[i] = HectonOceanSurfaceMath.SanitizeWave(wave);
            }

            EnsureWeatherDefaultsFromWaves();
            return true;
        }

        private void GenerateEmergencyMockWeather()
        {
            if (!ResolveWaveBuffer(out NativeArray<WaveParametersDTO> waves) ||
                !ResolveWeatherArray(out NativeArray<WeatherStateDTO> weather) ||
                !ResolveAtmosphereArray(out NativeArray<AtmosphereDTO> atmosphere))
            {
                return;
            }

            float maxAmplitude = 0f;
            for (int i = 0; i < OceanSurfaceAtmosphereConstants.WaveCapacity; i++)
            {
                float t = i * (1f / (OceanSurfaceAtmosphereConstants.WaveCapacity - 1));
                float angle = (i * 2.3999631f) + 0.37f;
                math.sincos(angle, out float s, out float c);
                float wavelength = math.lerp(18f, 180f, t);
                float waveNumber = OceanSurfaceAtmosphereConstants.TwoPi / wavelength;
                float amplitude = math.lerp(0.28f, 2.4f, t * t);
                maxAmplitude = math.max(maxAmplitude, amplitude);

                WaveParametersDTO wave = default;
                wave.DirectionAndSteepness = new float4(c, s, i * 0.7548777f, math.lerp(0.18f, 0.78f, t));
                wave.PhaseSpeed = math.sqrt(9.81f * waveNumber) * math.lerp(0.72f, 1.18f, t);
                wave.Amplitude = amplitude;
                wave.Wavelength = wavelength;
                waves[i] = HectonOceanSurfaceMath.SanitizeWave(wave);
            }

            WeatherStateDTO state = default;
            state.WindDirectionSpeedStorm = new float4(0.78f, 0.62f, 11f, 0.42f);
            state.SurfaceScalars = new float4(seaLevel, 1f, DefaultFoamThreshold, 0.25f);
            state.SkyTintAndSurge = new float4(0.33f, 0.21f, 0.48f, 0.12f);
            state.StateMask = 1u;
            state.GlobalQualityWeight = ResolveGlobalQualityWeight();
            state.MaxWaveAmplitude = maxAmplitude;
            weather[0] = state;

            AtmosphereDTO atmo = default;
            atmo.RayleighBeta = new float4(0.0048f, 0.0118f, 0.0285f, 0f);
            atmo.MieBeta = new float4(0.021f, 0.018f, 0.014f, 0.72f);
            atmo.ScatteringParams = new float4(2.4f, 1.15f, 0.82f, 0.34f);
            atmo.PlanetParams = new float4(0.62f, 0.17f, 0.88f, 0f);
            atmosphere[0] = atmo;
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

            float maxAmplitude = 0f;
            for (int i = 0; i < math.min(waves.Length, OceanSurfaceAtmosphereConstants.WaveCapacity); i++)
                maxAmplitude = math.max(maxAmplitude, math.abs(waves[i].Amplitude));

            WeatherStateDTO state = weather[0];
            if (!math.all(math.isfinite(state.WindDirectionSpeedStorm)) || state.WindDirectionSpeedStorm.z <= 0f)
                state.WindDirectionSpeedStorm = new float4(0.78f, 0.62f, 11f, 0.42f);
            if (!math.all(math.isfinite(state.SurfaceScalars)) || state.SurfaceScalars.z <= 0f)
                state.SurfaceScalars = new float4(seaLevel, 1f, DefaultFoamThreshold, 0.25f);
            if (!math.all(math.isfinite(state.SkyTintAndSurge)))
                state.SkyTintAndSurge = new float4(0.33f, 0.21f, 0.48f, 0.12f);

            state.GlobalQualityWeight = ResolveGlobalQualityWeight();
            state.MaxWaveAmplitude = maxAmplitude;
            weather[0] = state;
        }

        private bool TryLoadWeatherProfilesCsv()
        {
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

            string path = Path.Combine(root, "Assets/StreamingAssets/weather_profiles.csv");
            if (!File.Exists(path))
                path = Path.Combine(root, "StreamingAssets/weather_profiles.csv");
            if (!File.Exists(path))
                path = Path.Combine(root, "Data/Precomputed/weather_profiles.csv");
            if (!File.Exists(path))
                return false;

            try
            {
                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                int read = ReadStreamToScratch(stream, scratch, scratch.Length);
                bool changed = OceanWeatherCsvParser.TryApply(scratch, read, waves, weather, atmosphere);
                if (changed)
                    UploadWaveBufferToGpu(true);
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
        }

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
                raw = math.reversebytes(raw);

            return math.asfloat(raw);
        }

        private void EvaluateCameraWaterline()
        {
            Transform cam = cameraTransform;
            if (cam == null || !ResolveWaveBuffer(out NativeArray<WaveParametersDTO> waves))
                return;

            Vector3 runtime = cam.position;
            double3 aup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(runtime);
            long start = Stopwatch.GetTimestamp();
            HectonOceanSurfaceMath.EvaluateWavesDetailed(
                aup,
                _timeSeconds,
                waves,
                _globalQualityWeight,
                out float relativeHeight,
                out float3 normal,
                out _,
                out float jacobian,
                out _);
            long end = Stopwatch.GetTimestamp();
            _lastWaveComputeNs = TicksToNanoseconds(end - start);

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
                signal.Flags = math.isfinite(jacobian) && math.all(math.isfinite(normal)) ? (byte)1 : (byte)2;
                SignalBus<WaterlineBreachSignal>.Push(in signal);
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
                wave.Amplitude = math.max(15f, wave.Amplitude);
                wave.DirectionAndSteepness.w = math.max(wave.DirectionAndSteepness.w, 0.84f);
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
            UploadWaveBufferToGpu(true);
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
            if (_vault == null || !_vault.TryGetBuffer<ulong>(BufferID.QuestDagGlobalStateMasks, out NativeArray<ulong> masks) || !masks.IsCreated)
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

            float maxAmplitude = 0f;
            int limit = math.min(waves.Length, OceanSurfaceAtmosphereConstants.WaveCapacity);
            for (int i = 0; i < limit; i++)
                maxAmplitude = math.max(maxAmplitude, math.abs(waves[i].Amplitude));

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
            entry.StateHash = HectonOceanSurfaceMath.HashWaveState(waves, limit, _timeSeconds, _globalQualityWeight);
            entry.Flags = _lastWaveComputeNs > OceanSurfaceAtmosphereConstants.TelemetryDumpBudgetNs ? 1u : 0u;
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

                string path = Path.Combine(root, "Docs/AgentLogs/Dump_SURFACE_SURGEON.bin");
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

        private void PublishShaderGlobals()
        {
            if (!ResolveWaveBuffer(out NativeArray<WaveParametersDTO> waves) ||
                !ResolveWeather(out WeatherStateDTO weather) ||
                !ResolveAtmosphere(out AtmosphereDTO atmosphere))
            {
                return;
            }

            double3 cameraAup = cameraTransform != null
                ? HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(cameraTransform.position)
                : HectonFloatingOrigin.CurrentTotalOffsetDouble;
            OceanSurfaceLodDTO lod = HectonOceanSurfaceMath.ResolveRadialGridLod(cameraAup, _globalQualityWeight);
            lod.Frame = _simulationFrameCounter;
            if (ResolveLodArray(out NativeArray<OceanSurfaceLodDTO> lodArray))
                lodArray[0] = lod;

            int activeWaveCount = HectonOceanSurfaceMath.ResolveFullWaveCount(_globalQualityWeight, math.min(waves.Length, OceanSurfaceAtmosphereConstants.WaveCapacity));
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
                lod);
            if (_lastPublishedShaderStateHash == shaderStateHash)
            {
                UploadWaveBufferToGpu(false);
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
            Shader.SetGlobalVector(GlobalFlowVectorId, flowVector);
            Shader.SetGlobalVector(H8GlobalFlowId, flowVector);

            UploadWaveBufferToGpu(false);
        }

        private void UploadWaveBufferToGpu(bool allowColdCreate)
        {
            if (!ResolveWaveBuffer(out NativeArray<WaveParametersDTO> waves))
                return;

            if (_waveGraphicsBufferA == null || _waveGraphicsBufferB == null)
            {
                if (!allowColdCreate || !EnsureWaveGraphicsBuffers())
                    return;
            }

            int count = math.min(waves.Length, OceanSurfaceAtmosphereConstants.WaveCapacity);
            uint waveHash = HashWavePayload(waves, count);
            if (_lastUploadedWaveCount == count && _lastUploadedWaveHash == waveHash)
                return;

            GraphicsBuffer target = _waveGraphicsBufferWriteIndex == 0 ? _waveGraphicsBufferA : _waveGraphicsBufferB;
            GraphicsBufferUploadUtility.UploadNativeArray(target, waves, count);
            Shader.SetGlobalBuffer(OceanWaveBufferId, target);
            _waveGraphicsBufferWriteIndex ^= 1;
            _lastUploadedWaveCount = count;
            _lastUploadedWaveHash = waveHash;
        }

        private bool EnsureWaveGraphicsBuffers()
        {
            if (_waveGraphicsBufferA == null)
            {
                // COLD ALLOC: GraphicsBuffer[16 WaveParametersDTO] - ocean wave upload buffer A - owner: ShinobuOceanSurfaceAtmosphereRuntime
                _waveGraphicsBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<WaveParametersDTO>(OceanSurfaceAtmosphereConstants.WaveCapacity);
            }

            if (_waveGraphicsBufferB == null)
            {
                // COLD ALLOC: GraphicsBuffer[16 WaveParametersDTO] - ocean wave upload buffer B - owner: ShinobuOceanSurfaceAtmosphereRuntime
                _waveGraphicsBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<WaveParametersDTO>(OceanSurfaceAtmosphereConstants.WaveCapacity);
            }

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
        }

        private float3 ResolveSurfaceFlow()
        {
            if (!ResolveWeather(out WeatherStateDTO weather))
                return float3.zero;

            float2 direction = HectonOceanSurfaceMath.Normalize2OrDefault(weather.WindDirectionSpeedStorm.xy, new float2(1f, 0f));
            float speed = math.max(0f, weather.WindDirectionSpeedStorm.z) * math.lerp(0.08f, 0.42f, math.saturate(weather.WindDirectionSpeedStorm.w));
            return new float3(direction.x * speed, 0f, direction.y * speed);
        }

        private bool ResolveWaveBuffer(out NativeArray<WaveParametersDTO> waves)
        {
            waves = default;
            return _vault != null && _waveHandle.IsCreated && (waves = _waveHandle.Resolve(_vault)).IsCreated;
        }

        private bool ResolveWeatherArray(out NativeArray<WeatherStateDTO> weather)
        {
            weather = default;
            return _vault != null && _weatherHandle.IsCreated && (weather = _weatherHandle.Resolve(_vault)).IsCreated && weather.Length > 0;
        }

        private bool ResolveAtmosphereArray(out NativeArray<AtmosphereDTO> atmosphere)
        {
            atmosphere = default;
            return _vault != null && _atmosphereHandle.IsCreated && (atmosphere = _atmosphereHandle.Resolve(_vault)).IsCreated && atmosphere.Length > 0;
        }

        private bool ResolveTelemetry(out NativeArray<OceanSurfaceTelemetryEntry> telemetry)
        {
            telemetry = default;
            return _vault != null && _telemetryHandle.IsCreated && (telemetry = _telemetryHandle.Resolve(_vault)).IsCreated && telemetry.Length > 0;
        }

        private bool ResolveCsvScratch(out NativeArray<byte> scratch)
        {
            scratch = default;
            return _vault != null && _csvScratchHandle.IsCreated && (scratch = _csvScratchHandle.Resolve(_vault)).IsCreated && scratch.Length > 0;
        }

        private bool ResolveLodArray(out NativeArray<OceanSurfaceLodDTO> lod)
        {
            lod = default;
            return _vault != null && _lodHandle.IsCreated && (lod = _lodHandle.Resolve(_vault)).IsCreated && lod.Length > 0;
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
            return math.isfinite(weight) && weight > 0f ? math.saturate(weight) : 1f;
        }

        private void AdvanceSimulationClock()
        {
            _simulationFrameCounter++;
            if (_simulationFrameCounter == 0u)
                _simulationFrameCounter = 1u;

            _rawSimulationTimeSeconds += SimulationTickDeltaSeconds;
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
            float q = math.saturate(globalQualityWeight);
            float curve = q * q * (3f - (2f * q));
            float hz = math.lerp(MinWaveEvaluationHz, MaxWaveEvaluationHz, curve);
            return 1f / math.max(MinWaveEvaluationHz, hz);
        }

        private void ResolveCameraTransformCold()
        {
            if (cameraTransform != null)
                return;

            IPlayerRuntimeContext player = GlobalRegistry.Player;
            Camera camera = player != null ? player.PlayerCamera : null;
            if (camera != null)
                cameraTransform = camera.transform;
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
                hash = Hash(hash, math.asuint(wave.DirectionAndSteepness.x));
                hash = Hash(hash, math.asuint(wave.DirectionAndSteepness.y));
                hash = Hash(hash, math.asuint(wave.DirectionAndSteepness.z));
                hash = Hash(hash, math.asuint(wave.DirectionAndSteepness.w));
                hash = Hash(hash, math.asuint(wave.PhaseSpeed));
                hash = Hash(hash, math.asuint(wave.Amplitude));
                hash = Hash(hash, math.asuint(wave.Wavelength));
            }

            return Hash(hash, (uint)safeCount);
        }

        private static uint HashShaderState(
            float timeSeconds,
            float globalQualityWeight,
            int activeWaveCount,
            in WeatherStateDTO weather,
            in AtmosphereDTO atmosphere,
            in OceanSurfaceLodDTO lod)
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
