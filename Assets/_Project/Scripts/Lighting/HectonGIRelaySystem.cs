using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Signals;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Lighting
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-2600)]
    public sealed class HectonGIRelaySystem : MonoBehaviour, IGIRelaySystem, ISlowTickable, ILateFrameTickable, IWeatherEventListener
    {
        private const int SHCoefficientCount = 27;
        private const int SHChannelCoefficientCount = 9;
        private const int SHStateCount = 4;
        private const int TelemetryCapacity = 300;
        private const int LightningFrameBudget = 2;
        private const float SecondsPerDay = 86400f;
        private const double SecondsPerDayRcp = 1d / SecondsPerDay;
        private const float DepthPaletteFullDepthMeters = 500f;
        private const float DepthPaletteFullDepthMetersRcp = 1f / DepthPaletteFullDepthMeters;
        private const float CascadeOneEnterDepthMeters = 200f;
        private const float CascadeOneExitDepthMeters = 170f;
        private const float CascadeZeroEnterDepthMeters = 500f;
        private const float CascadeZeroExitDepthMeters = 450f;
        private const float ShaderColorEpsilon = 0.0005f;
        private const uint SHJobLowTierSnapMask = 1u << 0;
        private const uint RelayContextHash = 0x47495245u;
        private const uint SHLayoutMismatchHash = 0x53484C4Fu;
        private const uint NonFiniteInputHash = 0x4749464Eu;
        private const string BlackBoxDumpPath = "Docs/AgentLogs/Dump_RENDER_GI_RELAY_SYNC.bin";

        private static readonly int _HectonAtmosphereColorId = Shader.PropertyToID("_HectonAtmosphereColor");
        private static readonly int _HectonFogLodId = Shader.PropertyToID("_HectonFogLOD");
        private static readonly int _FaunaEmissiveMultiplierId = Shader.PropertyToID("_FaunaEmissiveMultiplier");
        private static readonly int _HectonWaterSurfaceEmissionId = Shader.PropertyToID("_HectonWaterSurfaceEmission");
        private static readonly int _HectonUnderwaterSurfaceColorId = Shader.PropertyToID("_HectonUnderwaterSurfaceColor");
        private static readonly int _HectonGIProbeTintId = Shader.PropertyToID("_HectonGIProbeTint");
        private static readonly int _HectonGIRelayStateId = Shader.PropertyToID("_HectonGIRelayState");
        private static readonly int _HectonBiomeGradientStateId = Shader.PropertyToID("_HectonBiomeGradientState");
        private static readonly int _WaterVolumeId = Shader.PropertyToID("_WaterVolume");
        private static readonly int _WaterVolumeDepthPaletteId = Shader.PropertyToID("_WaterVolumeDepthPalette");

        [Header("Global Reflection")]
        [SerializeField] private Cubemap waterVolumeLowResCubemap;

        [Header("Scalars")]
        [SerializeField, Range(0f, 2f)] private float daySHIntensity = 0.74f;
        [SerializeField, Range(0f, 1f)] private float nightSHIntensity = 0.16f;
        [SerializeField, Range(0f, 2f)] private float lightningL0Boost = 0.85f;
        [SerializeField, Range(0f, 1f)] private float depthPaletteStrength = 0.82f;

        // COLD ALLOC: NativeArray<float>[27] - day SH coefficient profile - owner: HectonGIRelaySystem
        private NativeArray<float> _shDay;
        // COLD ALLOC: NativeArray<float>[27] - night SH coefficient profile - owner: HectonGIRelaySystem
        private NativeArray<float> _shNight;
        // COLD ALLOC: NativeArray<float>[108] - four low-tier discrete SH states - owner: HectonGIRelaySystem
        private NativeArray<float> _shDiscreteStates;
        // COLD ALLOC: NativeArray<float>[27] - async job SH output - owner: HectonGIRelaySystem
        private NativeArray<float> _shOutput;
        // COLD ALLOC: NativeArray<float>[27] - two-frame lightning overlay scratch - owner: HectonGIRelaySystem
        private NativeArray<float> _shLightningScratch;
        // COLD ALLOC: NativeArray<GIRelayTelemetryEntry>[300] - fixed black-box circular buffer - owner: HectonGIRelaySystem
        private NativeArray<GIRelayTelemetryEntry> _telemetryRing;

        private JobHandle _pendingSHJob;
        private SphericalHarmonicsL2 _ambientProbe;
        private GIRelayRuntimeSnapshot _snapshot;
        private Color _lastAtmosphereColor;
        private Color _lastSurfaceEmissionColor;
        private Color _lastDepthPaletteColor;
        private HectonUnderwaterVisuals _lastSurfaceEmissionTarget;
        private Cubemap _lastWaterVolumeCubemap;
        private Vector4 _lastRelayState;
        private Vector4 _lastBiomeGradientState;
        private float _lastFogLod;
        private float _lastFaunaEmissive;
        private bool _lastAtmosphereColorValid;
        private bool _lastSurfaceEmissionColorValid;
        private bool _lastDepthPaletteColorValid;
        private bool _lastRelayStateValid;
        private bool _lastBiomeGradientStateValid;
        private bool _lastFogLodValid;
        private bool _lastFaunaEmissiveValid;
        private bool _hasPendingSHJob;
        private bool _nativeStorageReady;
        private bool _registeredSlowTick;
        private bool _registeredLateFrameTick;
        private bool _registeredWeatherListener;
        private bool _ambientProbeAuthorityActive;
        private bool _restoreBaseProbeAfterLightning;
        private bool _globalReflectionBound;
        private int _baselineShadowCascades;
        private int _shadowCascadeLevel = -1;
        private int _lightningFramesRemaining;
        private float _lightningScalar;
        private int _telemetryCursor;
        private int _telemetryCount;
        private int _tickCount;
        private uint _sequence;

        public bool IsAmbientProbeAuthorityActive => _ambientProbeAuthorityActive && _nativeStorageReady;

        public GIRelayRuntimeSnapshot Snapshot => _snapshot;

        public int ShadowCascadeLevel => _shadowCascadeLevel;

        public float LastAppliedDepthMeters => _snapshot.DepthMeters;

        public uint LastAppliedSequence => _snapshot.Sequence;

        public int TickCount => _tickCount;

        private void Awake()
        {
            EnsureNativeStorage();
            CaptureBaselineShadowCascades();
            if (Application.isPlaying)
                GlobalRegistry.RegisterGIRelayRuntime(this);
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return;
#endif

            EnsureNativeStorage();
            CaptureBaselineShadowCascades();
            GlobalRegistry.RegisterGIRelayRuntime(this);
            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            WeatherEvents.Register(this);
            _registeredWeatherListener = true;
            InvalidateShaderStateCache();
            _ambientProbeAuthorityActive = true;
        }

        private void OnDisable()
        {
            ShutdownRuntime();
        }

        private void OnDestroy()
        {
            ShutdownRuntime();
            DisposeNativeStorage();
        }

        public void SlowTick()
        {
            _tickCount++;
            if (!_nativeStorageReady)
                return;

            if (_hasPendingSHJob)
            {
                if (!_pendingSHJob.IsCompleted)
                    return;

                CompleteAndPushPendingSHJob();
            }

            BiomeGradientSignal biomeGradient = ResolveLatestBiomeGradientSignal();
            GIRelayRuntimeSnapshot nextSnapshot = ResolveRuntimeSnapshot(in biomeGradient);
            if (!IsSnapshotFinite(in nextSnapshot))
            {
                RecordTelemetry(in nextSnapshot, GIRelayTelemetryFlags.NonFinite);
                DumpBlackBox();
                GlobalTelemetryBus.PublishPerformanceWarning(NonFiniteInputHash, RelayContextHash, nextSnapshot.DepthMeters);
                return;
            }

            _snapshot = nextSnapshot;
            ApplyShadowCascadeState(nextSnapshot.DepthMeters);
            ApplyShaderRelayState(in nextSnapshot, in biomeGradient);
            BindGlobalWaterVolumeCubemap();
            ScheduleSHJob(in nextSnapshot, in biomeGradient);
            RecordTelemetry(in nextSnapshot, GIRelayTelemetryFlags.Scheduled);
        }

        public void LateFrameTick()
        {
            if (_hasPendingSHJob && _pendingSHJob.IsCompleted)
                CompleteAndPushPendingSHJob();

            if (_lightningFramesRemaining > 0)
            {
                PushLightningProbeOverlay();
                _lightningFramesRemaining--;
                _restoreBaseProbeAfterLightning = _lightningFramesRemaining == 0;
                return;
            }

            if (_restoreBaseProbeAfterLightning)
            {
                TryPushAmbientProbeFrom(_shOutput);
                _restoreBaseProbeAfterLightning = false;
                _lightningScalar = 0f;
            }
        }

        public void OnWeatherEvent(in WeatherEventPayload payload)
        {
            if (payload.EventType != (ushort)WeatherEventType.Lightning)
                return;

            float lightningIntensity = math.saturate(payload.WeatherIntensity);
            _lightningScalar = _lightningFramesRemaining > 0
                ? math.max(_lightningScalar, lightningIntensity)
                : lightningIntensity;
            _lightningFramesRemaining = LightningFrameBudget;
        }

        public bool ValidateSphericalHarmonicsLayout(out int expectedBytes, out int actualBytes)
        {
            expectedBytes = SHCoefficientCount * UnsafeUtility.SizeOf<float>();
            actualBytes = UnsafeUtility.SizeOf<SphericalHarmonicsL2>();
            return expectedBytes == actualBytes;
        }

        private void ShutdownRuntime()
        {
            if (_registeredWeatherListener)
            {
                WeatherEvents.Unregister(this);
                _registeredWeatherListener = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }

            if (_registeredLateFrameTick)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrameTick = false;
            }

            if (ReferenceEquals(GlobalRegistry.GIRelay, this))
                GlobalRegistry.UnregisterGIRelayRuntime(this);

            _ambientProbeAuthorityActive = false;
            if (_hasPendingSHJob)
            {
                _pendingSHJob.Complete();
                _hasPendingSHJob = false;
            }

            InvalidateShaderStateCache();
        }

        private void InvalidateShaderStateCache()
        {
            _lastAtmosphereColorValid = false;
            _lastSurfaceEmissionColorValid = false;
            _lastDepthPaletteColorValid = false;
            _lastRelayStateValid = false;
            _lastBiomeGradientStateValid = false;
            _lastFogLodValid = false;
            _lastFaunaEmissiveValid = false;
            _lastSurfaceEmissionTarget = null;
            _lastWaterVolumeCubemap = null;
            _globalReflectionBound = false;
        }

        private void EnsureNativeStorage()
        {
            if (_nativeStorageReady)
                return;

            _shDay = new NativeArray<float>(SHCoefficientCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _shNight = new NativeArray<float>(SHCoefficientCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _shDiscreteStates = new NativeArray<float>(SHCoefficientCount * SHStateCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _shOutput = new NativeArray<float>(SHCoefficientCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _shLightningScratch = new NativeArray<float>(SHCoefficientCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _telemetryRing = new NativeArray<GIRelayTelemetryEntry>(TelemetryCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            NativeMemorySentinel.RegisterNativeArray(_shDay, nameof(HectonGIRelaySystem), nameof(_shDay), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_shNight, nameof(HectonGIRelaySystem), nameof(_shNight), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_shDiscreteStates, nameof(HectonGIRelaySystem), nameof(_shDiscreteStates), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_shOutput, nameof(HectonGIRelaySystem), nameof(_shOutput), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_shLightningScratch, nameof(HectonGIRelaySystem), nameof(_shLightningScratch), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_telemetryRing, nameof(HectonGIRelaySystem), nameof(_telemetryRing), NativeAllocationLifetime.Scene);

            BuildSHProfiles();
            _nativeStorageReady = true;
        }

        private void DisposeNativeStorage()
        {
            if (_hasPendingSHJob)
            {
                _pendingSHJob.Complete();
                _hasPendingSHJob = false;
            }

            DisposeNativeArray(ref _shDay);
            DisposeNativeArray(ref _shNight);
            DisposeNativeArray(ref _shDiscreteStates);
            DisposeNativeArray(ref _shOutput);
            DisposeNativeArray(ref _shLightningScratch);
            DisposeNativeArray(ref _telemetryRing);
            _telemetryCursor = 0;
            _telemetryCount = 0;
            _nativeStorageReady = false;
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        private void BuildSHProfiles()
        {
            WriteDirectionalAmbient(_shDay, 0, new float3(0.62f, 0.78f, 0.96f) * daySHIntensity, 0.08f);
            WriteDirectionalAmbient(_shNight, 0, new float3(0.055f, 0.075f, 0.125f) * nightSHIntensity, 0.025f);
            WriteDirectionalAmbient(_shDiscreteStates, 0, new float3(0.045f, 0.065f, 0.120f) * nightSHIntensity, 0.015f);
            WriteDirectionalAmbient(_shDiscreteStates, SHCoefficientCount, new float3(0.32f, 0.49f, 0.63f) * daySHIntensity, 0.045f);
            WriteDirectionalAmbient(_shDiscreteStates, SHCoefficientCount * 2, new float3(0.62f, 0.78f, 0.96f) * daySHIntensity, 0.08f);
            WriteDirectionalAmbient(_shDiscreteStates, SHCoefficientCount * 3, new float3(0.38f, 0.27f, 0.20f) * daySHIntensity, 0.04f);
        }

        private static void WriteDirectionalAmbient(NativeArray<float> target, int offset, float3 l0Color, float directionalStrength)
        {
            for (int i = 0; i < SHCoefficientCount; i++)
                target[offset + i] = 0f;

            target[offset] = l0Color.x;
            target[offset + SHChannelCoefficientCount] = l0Color.y;
            target[offset + SHChannelCoefficientCount * 2] = l0Color.z;

            target[offset + 2] = l0Color.x * directionalStrength;
            target[offset + SHChannelCoefficientCount + 2] = l0Color.y * directionalStrength;
            target[offset + SHChannelCoefficientCount * 2 + 2] = l0Color.z * directionalStrength;

            target[offset + 6] = l0Color.x * directionalStrength * -0.35f;
            target[offset + SHChannelCoefficientCount + 6] = l0Color.y * directionalStrength * -0.35f;
            target[offset + SHChannelCoefficientCount * 2 + 6] = l0Color.z * directionalStrength * -0.35f;
        }

        private GIRelayRuntimeSnapshot ResolveRuntimeSnapshot(in BiomeGradientSignal biomeGradient)
        {
            CelestialRuntimeSnapshot celestial = GlobalRegistry.CelestialRuntimeSnapshot;
            float depthMeters = ResolveDepthMetersAbsolute();
            float depth01 = math.saturate(depthMeters * DepthPaletteFullDepthMetersRcp);
            float timeOfDay01 = ResolveTimeOfDay01(celestial.AbsoluteUniverseTime);
            float moonPhase01 = math.saturate(math.max(celestial.Moon0Phase01, celestial.Moon1Phase01));
            float eclipse01 = math.saturate(celestial.EclipseOcclusion01);
            float fogLod = ResolveFogLod(depth01, eclipse01, biomeGradient.BlendFactor01);
            uint flags = (uint)GIRelayTelemetryFlags.Valid;
            if (IsLowTier())
                flags |= (uint)GIRelayTelemetryFlags.LowTierSnap;
            if (_lightningFramesRemaining > 0)
                flags |= (uint)GIRelayTelemetryFlags.LightningActive;

            _sequence++;
            return new GIRelayRuntimeSnapshot
            {
                AbsoluteUniverseTime = celestial.AbsoluteUniverseTime,
                TimeOfDay01 = timeOfDay01,
                DepthMeters = depthMeters,
                Depth01 = depth01,
                EclipseScalar = eclipse01,
                MoonPhase01 = moonPhase01,
                FogLod = fogLod,
                LightningScalar = _lightningFramesRemaining > 0 ? _lightningScalar : 0f,
                ShadowCascadeLevel = _shadowCascadeLevel,
                Flags = flags,
                Sequence = _sequence
            };
        }

        private static float ResolveTimeOfDay01(double absoluteUniverseTime)
        {
            if (!math.isfinite(absoluteUniverseTime))
                return 0.5f;

            double daySeconds = absoluteUniverseTime % SecondsPerDay;
            if (daySeconds < 0d)
                daySeconds += SecondsPerDay;

            return math.saturate((float)(daySeconds * SecondsPerDayRcp));
        }

        private static float ResolveDepthMetersAbsolute()
        {
            BiomeMatrixDirector biomeMatrix = GlobalRegistry.BiomeMatrix;
            if (biomeMatrix != null && math.isfinite(biomeMatrix.CurrentDepthMeters))
                return math.max(0f, biomeMatrix.CurrentDepthMeters);

            IPlayerRuntimeContext player = GlobalRegistry.Player;
            HectonPlayerMovement movement = player != null ? player.PlayerMovement : null;
            if (movement != null && math.isfinite(movement.CurrentDepth))
                return math.max(0f, movement.CurrentDepth);

            HectonUnderwaterVisuals underwaterVisuals = GlobalRegistry.UnderwaterVisuals;
            if (underwaterVisuals != null && math.isfinite(underwaterVisuals.CurrentDepth))
                return math.max(0f, underwaterVisuals.CurrentDepth);

            if (movement != null)
            {
                double3 absolute = movement.CurrentAup.ToAbsoluteDouble3();
                if (math.all(math.isfinite(absolute)))
                    return math.max(0f, (float)-absolute.y);
            }

            return 0f;
        }

        private static float ResolveFogLod(float depth01, float eclipse01, float biomeBlend01)
        {
            return math.saturate((depth01 * 0.72f) + (eclipse01 * 0.22f) + math.saturate(biomeBlend01) * 0.06f);
        }

        private static bool IsSnapshotFinite(in GIRelayRuntimeSnapshot snapshot)
        {
            return math.isfinite(snapshot.TimeOfDay01) &&
                   math.isfinite(snapshot.DepthMeters) &&
                   math.isfinite(snapshot.Depth01) &&
                   math.isfinite(snapshot.EclipseScalar) &&
                   math.isfinite(snapshot.MoonPhase01) &&
                   math.isfinite(snapshot.FogLod) &&
                   math.isfinite(snapshot.LightningScalar);
        }

        private bool IsLowTier()
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            return tier == HectonQualityTier.Unknown ||
                   tier == HectonQualityTier.Low ||
                   tier == HectonQualityTier.Mx350 ||
                   GlobalRegistry.H8_LOW_MEMORY_PROFILE;
        }

        private void ScheduleSHJob(in GIRelayRuntimeSnapshot snapshot, in BiomeGradientSignal biomeGradient)
        {
            GIRelaySHLerpJob job = new GIRelaySHLerpJob
            {
                SHDay = _shDay,
                SHNight = _shNight,
                SHDiscreteStates = _shDiscreteStates,
                SHOutput = _shOutput,
                TimeOfDay01 = snapshot.TimeOfDay01,
                Depth01 = snapshot.Depth01,
                Eclipse01 = snapshot.EclipseScalar,
                MoonPhase01 = snapshot.MoonPhase01,
                BiomeBlend01 = math.saturate(biomeGradient.BlendFactor01),
                DepthPaletteStrength = math.saturate(depthPaletteStrength),
                Flags = IsLowTier() ? SHJobLowTierSnapMask : 0u
            };

            _pendingSHJob = job.Schedule();
            _hasPendingSHJob = true;
        }

        private void CompleteAndPushPendingSHJob()
        {
            _pendingSHJob.Complete();
            _hasPendingSHJob = false;

            if (!TryPushAmbientProbeFrom(_shOutput))
            {
                RecordTelemetry(in _snapshot, GIRelayTelemetryFlags.SHLayoutMismatch);
                DumpBlackBox();
                return;
            }

            _snapshot.ShadowCascadeLevel = _shadowCascadeLevel;
            RecordTelemetry(in _snapshot, GIRelayTelemetryFlags.Pushed);
        }

        private unsafe bool TryPushAmbientProbeFrom(NativeArray<float> source)
        {
            if (!source.IsCreated || source.Length != SHCoefficientCount)
                return false;

            if (!ValidateSphericalHarmonicsLayout(out int expectedBytes, out int actualBytes))
            {
                GlobalTelemetryBus.PublishPerformanceWarning(SHLayoutMismatchHash, RelayContextHash, actualBytes);
                return false;
            }

            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
            void* targetPtr = UnsafeUtility.AddressOf(ref _ambientProbe);
            UnsafeUtility.MemCpy(targetPtr, sourcePtr, expectedBytes);
            if (RenderSettings.ambientMode != AmbientMode.Custom)
                RenderSettings.ambientMode = AmbientMode.Custom;
            RenderSettings.ambientProbe = _ambientProbe;
            return true;
        }

        private unsafe void PushLightningProbeOverlay()
        {
            if (!_shOutput.IsCreated || !_shLightningScratch.IsCreated)
                return;

            int bytes = SHCoefficientCount * UnsafeUtility.SizeOf<float>();
            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_shOutput);
            void* targetPtr = NativeArrayUnsafeUtility.GetUnsafePtr(_shLightningScratch);
            UnsafeUtility.MemCpy(targetPtr, sourcePtr, bytes);

            float scalar = math.saturate(_lightningScalar) * math.max(0f, lightningL0Boost);
            _shLightningScratch[0] += scalar;
            _shLightningScratch[SHChannelCoefficientCount] += scalar * 0.92f;
            _shLightningScratch[SHChannelCoefficientCount * 2] += scalar * 0.78f;
            TryPushAmbientProbeFrom(_shLightningScratch);
        }

        private void CaptureBaselineShadowCascades()
        {
            _baselineShadowCascades = math.max(1, QualitySettings.shadowCascades);
            if (_shadowCascadeLevel < 0)
                _shadowCascadeLevel = QualitySettings.shadowCascades;
        }

        private void ApplyShadowCascadeState(float depthMeters)
        {
            int target = _shadowCascadeLevel < 0 ? QualitySettings.shadowCascades : _shadowCascadeLevel;
            if (target <= 0)
            {
                if (depthMeters <= CascadeZeroExitDepthMeters)
                    target = 1;
            }
            else if (target <= 1)
            {
                if (depthMeters >= CascadeZeroEnterDepthMeters)
                    target = 0;
                else if (depthMeters <= CascadeOneExitDepthMeters)
                    target = _baselineShadowCascades;
            }
            else
            {
                if (depthMeters >= CascadeZeroEnterDepthMeters)
                    target = 0;
                else if (depthMeters >= CascadeOneEnterDepthMeters)
                    target = 1;
            }

            if (target == _shadowCascadeLevel)
                return;

            _shadowCascadeLevel = target;
            if (QualitySettings.shadowCascades != target)
                QualitySettings.shadowCascades = target;
        }

        private void ApplyShaderRelayState(in GIRelayRuntimeSnapshot snapshot, in BiomeGradientSignal biomeGradient)
        {
            Color atmosphereColor = ResolveAtmosphereColor(snapshot.Depth01, snapshot.EclipseScalar);
            if (!_lastAtmosphereColorValid || HasColorShift(atmosphereColor, _lastAtmosphereColor))
            {
                Shader.SetGlobalColor(_HectonAtmosphereColorId, atmosphereColor);
                _lastAtmosphereColor = atmosphereColor;
                _lastAtmosphereColorValid = true;
            }

            if (!_lastFogLodValid || math.abs(snapshot.FogLod - _lastFogLod) > 0.0001f)
            {
                Shader.SetGlobalFloat(_HectonFogLodId, snapshot.FogLod);
                _lastFogLod = snapshot.FogLod;
                _lastFogLodValid = true;
            }

            float faunaEmissive = snapshot.EclipseScalar > 0.5f
                ? 1f + math.saturate((snapshot.EclipseScalar - 0.5f) * 2f) * 1.35f
                : 1f;
            if (!_lastFaunaEmissiveValid || math.abs(faunaEmissive - _lastFaunaEmissive) > 0.0001f)
            {
                Shader.SetGlobalFloat(_FaunaEmissiveMultiplierId, faunaEmissive);
                _lastFaunaEmissive = faunaEmissive;
                _lastFaunaEmissiveValid = true;
            }

            Color surfaceEmission = ResolveSurfaceEmission(snapshot.MoonPhase01, snapshot.EclipseScalar);
            bool surfaceEmissionChanged = !_lastSurfaceEmissionColorValid ||
                HasColorShift(surfaceEmission, _lastSurfaceEmissionColor);
            if (surfaceEmissionChanged)
            {
                Shader.SetGlobalColor(_HectonWaterSurfaceEmissionId, surfaceEmission);
                Shader.SetGlobalColor(_HectonUnderwaterSurfaceColorId, surfaceEmission);
                _lastSurfaceEmissionColor = surfaceEmission;
                _lastSurfaceEmissionColorValid = true;
            }

            HectonUnderwaterVisuals underwaterVisuals = GlobalRegistry.UnderwaterVisuals;
            if (underwaterVisuals != null &&
                (surfaceEmissionChanged || !ReferenceEquals(underwaterVisuals, _lastSurfaceEmissionTarget)))
            {
                underwaterVisuals.ApplyGIRelaySurfaceEmission(surfaceEmission);
                _lastSurfaceEmissionTarget = underwaterVisuals;
            }
            else if (underwaterVisuals == null)
            {
                _lastSurfaceEmissionTarget = null;
            }

            Color depthPalette = ResolveDepthPaletteColor(snapshot.Depth01);
            if (!_lastDepthPaletteColorValid || HasColorShift(depthPalette, _lastDepthPaletteColor))
            {
                Shader.SetGlobalColor(_HectonGIProbeTintId, depthPalette);
                Shader.SetGlobalColor(_WaterVolumeDepthPaletteId, depthPalette);
                _lastDepthPaletteColor = depthPalette;
                _lastDepthPaletteColorValid = true;
            }

            Vector4 relayState = new Vector4(snapshot.Depth01, snapshot.TimeOfDay01, snapshot.EclipseScalar, snapshot.FogLod);
            Vector4 relayDelta = relayState - _lastRelayState;
            float relayDeltaSq =
                (relayDelta.x * relayDelta.x) +
                (relayDelta.y * relayDelta.y) +
                (relayDelta.z * relayDelta.z) +
                (relayDelta.w * relayDelta.w);
            if (!_lastRelayStateValid || relayDeltaSq > ShaderColorEpsilon)
            {
                Shader.SetGlobalVector(_HectonGIRelayStateId, relayState);
                _lastRelayState = relayState;
                _lastRelayStateValid = true;
            }

            Vector4 biomeGradientState = new Vector4(
                biomeGradient.BiomeA,
                biomeGradient.BiomeB,
                math.saturate(biomeGradient.BlendFactor01),
                math.max(0f, biomeGradient.BoundaryDistanceMeters));
            Vector4 biomeDelta = biomeGradientState - _lastBiomeGradientState;
            float biomeDeltaSq =
                (biomeDelta.x * biomeDelta.x) +
                (biomeDelta.y * biomeDelta.y) +
                (biomeDelta.z * biomeDelta.z) +
                (biomeDelta.w * biomeDelta.w);
            if (!_lastBiomeGradientStateValid || biomeDeltaSq > ShaderColorEpsilon)
            {
                Shader.SetGlobalVector(_HectonBiomeGradientStateId, biomeGradientState);
                _lastBiomeGradientState = biomeGradientState;
                _lastBiomeGradientStateValid = true;
            }
        }

        private void BindGlobalWaterVolumeCubemap()
        {
            if (waterVolumeLowResCubemap == null)
            {
                _lastWaterVolumeCubemap = null;
                return;
            }

            if (!ReferenceEquals(_lastWaterVolumeCubemap, waterVolumeLowResCubemap))
            {
                Shader.SetGlobalTexture(_WaterVolumeId, waterVolumeLowResCubemap);
                _lastWaterVolumeCubemap = waterVolumeLowResCubemap;
            }

            if (!_globalReflectionBound || RenderSettings.customReflectionTexture != waterVolumeLowResCubemap)
            {
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = waterVolumeLowResCubemap;
                _globalReflectionBound = true;
            }
        }

        private static Color ResolveAtmosphereColor(float depth01, float eclipse01)
        {
            Color shallow = new Color(0.035f, 0.46f, 0.58f, 1f);
            Color deep = new Color(0.001f, 0.002f, 0.004f, 1f);
            Color eclipse = new Color(0.018f, 0.024f, 0.048f, 1f);
            Color depthColor = Color.Lerp(shallow, deep, depth01);
            depthColor = Color.Lerp(depthColor, eclipse, eclipse01 * 0.55f);
            depthColor.a = 1f;
            return depthColor;
        }

        private static Color ResolveSurfaceEmission(float moonPhase01, float eclipse01)
        {
            Color lunar = Color.Lerp(
                new Color(0.015f, 0.025f, 0.045f, 1f),
                new Color(0.16f, 0.23f, 0.34f, 1f),
                math.saturate(moonPhase01));
            Color eclipse = new Color(0.04f, 0.08f, 0.14f, 1f);
            Color result = Color.Lerp(lunar, eclipse, math.saturate(eclipse01));
            result.a = 1f;
            return result;
        }

        private static Color ResolveDepthPaletteColor(float depth01)
        {
            Color shallow = new Color(0.08f, 0.82f, 0.94f, 1f);
            Color deep = new Color(0.002f, 0.003f, 0.006f, 1f);
            Color result = Color.Lerp(shallow, deep, math.saturate(depth01));
            result.a = 1f;
            return result;
        }

        private static bool HasColorShift(Color lhs, Color rhs)
        {
            float dr = lhs.r - rhs.r;
            float dg = lhs.g - rhs.g;
            float db = lhs.b - rhs.b;
            return (dr * dr) + (dg * dg) + (db * db) > ShaderColorEpsilon;
        }

        private static BiomeGradientSignal ResolveLatestBiomeGradientSignal()
        {
            ReadOnlySpan<BiomeGradientSignal> signals = SignalBus<BiomeGradientSignal>.GetFrameSnapshot();
            return signals.Length > 0 ? signals[signals.Length - 1] : default;
        }

        private void RecordTelemetry(in GIRelayRuntimeSnapshot snapshot, GIRelayTelemetryFlags eventFlags)
        {
            if (!_telemetryRing.IsCreated || _telemetryRing.Length <= 0)
                return;

            int index = _telemetryCursor;
            _telemetryRing[index] = new GIRelayTelemetryEntry
            {
                FrameIndex = Time.frameCount,
                Sequence = snapshot.Sequence,
                Flags = snapshot.Flags | (uint)eventFlags,
                StateHash = HashTelemetrySnapshot(in snapshot, _shadowCascadeLevel, eventFlags),
                ShadowCascadeLevel = _shadowCascadeLevel,
                DepthMeters = snapshot.DepthMeters,
                TimeOfDay01 = snapshot.TimeOfDay01,
                EclipseScalar = snapshot.EclipseScalar,
                FogLod = snapshot.FogLod,
                LightningScalar = snapshot.LightningScalar
            };

            index++;
            _telemetryCursor = index >= _telemetryRing.Length ? 0 : index;
            if (_telemetryCount < _telemetryRing.Length)
                _telemetryCount++;
        }

        private void DumpBlackBox()
        {
            if (!_telemetryRing.IsCreated || _telemetryRing.Length <= 0)
                return;

            try
            {
                string fullPath = Path.Combine(Application.dataPath, "..", BlackBoxDumpPath);
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using FileStream stream = File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                using BinaryWriter writer = new BinaryWriter(stream);
                int count = _telemetryRing.Length;
                int startIndex = _telemetryCount >= count ? _telemetryCursor : 0;
                for (int i = 0; i < count; i++)
                {
                    int entryIndex = startIndex + i;
                    if (entryIndex >= count)
                        entryIndex -= count;

                    GIRelayTelemetryEntry entry = _telemetryRing[entryIndex];
                    writer.Write(entry.FrameIndex);
                    writer.Write(entry.Sequence);
                    writer.Write(entry.Flags);
                    writer.Write(entry.StateHash);
                    writer.Write(entry.ShadowCascadeLevel);
                    writer.Write(entry.DepthMeters);
                    writer.Write(entry.TimeOfDay01);
                    writer.Write(entry.EclipseScalar);
                    writer.Write(entry.FogLod);
                    writer.Write(entry.LightningScalar);
                }
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[HectonGIRelaySystem] Black-box dump failed: " + exception.Message, this);
#endif
            }
        }

        private static uint HashTelemetrySnapshot(in GIRelayRuntimeSnapshot snapshot, int shadowCascadeLevel, GIRelayTelemetryFlags eventFlags)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = HashUInt(hash, snapshot.Sequence);
                hash = HashUInt(hash, snapshot.Flags | (uint)eventFlags);
                hash = HashInt(hash, shadowCascadeLevel);
                hash = HashInt(hash, QuantizeTelemetryFloat(snapshot.DepthMeters, 10f));
                hash = HashInt(hash, QuantizeTelemetryFloat(snapshot.TimeOfDay01, 10000f));
                hash = HashInt(hash, QuantizeTelemetryFloat(snapshot.EclipseScalar, 10000f));
                hash = HashInt(hash, QuantizeTelemetryFloat(snapshot.FogLod, 10000f));
                hash = HashInt(hash, QuantizeTelemetryFloat(snapshot.LightningScalar, 10000f));
                return hash;
            }
        }

        private static int QuantizeTelemetryFloat(float value, float scale)
        {
            if (!math.isfinite(value))
                return 0;

            return (int)math.round(value * scale);
        }

        private static uint HashInt(uint hash, int value)
        {
            return HashUInt(hash, (uint)value);
        }

        private static uint HashUInt(uint hash, uint value)
        {
            unchecked
            {
                hash = (hash ^ value) * 16777619u;
                hash = (hash ^ (value >> 16)) * 16777619u;
                return hash;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        private struct GIRelaySHLerpJob : IJob
        {
            [ReadOnly] public NativeArray<float> SHDay;
            [ReadOnly] public NativeArray<float> SHNight;
            [ReadOnly] public NativeArray<float> SHDiscreteStates;
            [WriteOnly] public NativeArray<float> SHOutput;
            public float TimeOfDay01;
            public float Depth01;
            public float Eclipse01;
            public float MoonPhase01;
            public float BiomeBlend01;
            public float DepthPaletteStrength;
            public uint Flags;

            public void Execute()
            {
                float daylight01 = ResolveDaylight(TimeOfDay01, Eclipse01);
                float3 depthTint = ResolveDepthTint(Depth01, DepthPaletteStrength, MoonPhase01, BiomeBlend01);
                if ((Flags & SHJobLowTierSnapMask) != 0u)
                {
                    int state = ResolveDiscreteState(TimeOfDay01, daylight01);
                    int offset = state * SHCoefficientCount;
                    for (int i = 0; i < SHCoefficientCount; i++)
                        SHOutput[i] = SHDiscreteStates[offset + i] * ResolveChannelTint(i, depthTint);
                    return;
                }

                for (int i = 0; i < SHCoefficientCount; i++)
                {
                    float value = math.lerp(SHNight[i], SHDay[i], daylight01);
                    SHOutput[i] = value * ResolveChannelTint(i, depthTint);
                }
            }

            private static float ResolveDaylight(float timeOfDay01, float eclipse01)
            {
                float daylight = math.saturate(1f - math.abs(timeOfDay01 - 0.5f) * 2f);
                return daylight * (1f - math.saturate(eclipse01) * 0.72f);
            }

            private static int ResolveDiscreteState(float timeOfDay01, float daylight01)
            {
                if (daylight01 < 0.18f)
                    return 0;
                if (timeOfDay01 < 0.5f)
                    return 1;
                if (daylight01 > 0.66f)
                    return 2;
                return 3;
            }

            private static float3 ResolveDepthTint(float depth01, float strength, float moonPhase01, float biomeBlend01)
            {
                float3 shallow = new float3(0.34f, 0.94f, 1f);
                float3 deep = new float3(0.006f, 0.008f, 0.014f);
                float3 palette = math.lerp(shallow, deep, math.saturate(depth01));
                palette += new float3(0.015f, 0.025f, 0.04f) * math.saturate(moonPhase01) * (1f - math.saturate(depth01));
                palette = math.lerp(palette, new float3(0.08f, 0.20f, 0.16f), math.saturate(biomeBlend01) * 0.18f);
                return math.lerp(new float3(1f, 1f, 1f), palette, math.saturate(strength));
            }

            private static float ResolveChannelTint(int coefficientIndex, float3 tint)
            {
                if (coefficientIndex < SHChannelCoefficientCount)
                    return tint.x;
                if (coefficientIndex < SHChannelCoefficientCount * 2)
                    return tint.y;
                return tint.z;
            }
        }

        [Flags]
        private enum GIRelayTelemetryFlags : uint
        {
            None = 0u,
            Valid = 1u << 0,
            LowTierSnap = 1u << 1,
            LightningActive = 1u << 2,
            Scheduled = 1u << 3,
            Pushed = 1u << 4,
            NonFinite = 1u << 5,
            SHLayoutMismatch = 1u << 6
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GIRelayTelemetryEntry
        {
            public int FrameIndex;
            public uint Sequence;
            public uint Flags;
            public uint StateHash;
            public int ShadowCascadeLevel;
            public float DepthMeters;
            public float TimeOfDay01;
            public float EclipseScalar;
            public float FogLod;
            public float LightningScalar;
        }
    }
}
