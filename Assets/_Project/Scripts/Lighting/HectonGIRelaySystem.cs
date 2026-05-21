using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
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
        private const SystemID MemoryOwner = SystemID.GraphicsScalability;
        private const BufferID SHDayBuffer = (BufferID)0x630820;
        private const BufferID SHNightBuffer = (BufferID)0x630821;
        private const BufferID SHDiscreteStatesBuffer = (BufferID)0x630822;
        private const BufferID SHOutputBuffer = (BufferID)0x630823;
        private const BufferID SHLightningScratchBuffer = (BufferID)0x630824;
        private const BufferID SHTelemetryRingBuffer = (BufferID)0x630825;
        private const float SecondsPerDay = 86400f;
        private const double SecondsPerDayRcp = 1d / SecondsPerDay;
        private const float DepthPaletteFullDepthMeters = 500f;
        private const float DepthPaletteFullDepthMetersRcp = 1f / DepthPaletteFullDepthMeters;
        private const float CascadeOneEnterDepthMeters = 200f;
        private const float CascadeOneExitDepthMeters = 170f;
        private const float CascadeZeroEnterDepthMeters = 500f;
        private const float CascadeZeroExitDepthMeters = 450f;
        private const float ShaderColorEpsilon = 0.0005f;
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
        private static readonly int _HectonGIRelaySHBufferId = Shader.PropertyToID("_HectonGIRelaySHBuffer");
        private static readonly int _HectonGIRelaySHStateId = Shader.PropertyToID("_HectonGIRelaySHState");

        [Header("Global Reflection")]
        [SerializeField] private Cubemap waterVolumeLowResCubemap;

        [Header("Scalars")]
        [SerializeField, Range(0f, 2f)] private float daySHIntensity = 0.74f;
        [SerializeField, Range(0f, 1f)] private float nightSHIntensity = 0.16f;
        [SerializeField, Range(0f, 2f)] private float lightningL0Boost = 0.85f;
        [SerializeField, Range(0f, 1f)] private float depthPaletteStrength = 0.82f;

        private IDataVault _vault;
        private VaultGenerationHandle<float> _shDay;
        private VaultGenerationHandle<float> _shNight;
        private VaultGenerationHandle<float> _shDiscreteStates;
        private VaultGenerationHandle<float> _shOutput;
        private VaultGenerationHandle<float> _shLightningScratch;
        private VaultGenerationHandle<GIRelayTelemetryEntry> _telemetryRing;
        private GraphicsBuffer _shUploadBufferA;
        private GraphicsBuffer _shUploadBufferB;
        private int _shUploadWriteIndex;

        private JobHandle _pendingSHJob;
        private GIRelayRuntimeSnapshot _snapshot;
        private Color _lastAtmosphereColor;
        private Color _lastSurfaceEmissionColor;
        private Color _lastDepthPaletteColor;
        private UnityEngine.Object _lastSurfaceEmissionTarget;
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
                NativeArray<float> output = OpenGIRelayArray(in _shOutput, SHOutputBuffer, SHCoefficientCount);
                TryPushAmbientProbeFrom(output);
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
            actualBytes = SHCoefficientCount * UnsafeUtility.SizeOf<float>();
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
                DispatcherJobFence.TryComplete(ref _pendingSHJob, forceComplete: true);
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

            _vault = ResolveDataVault();
            if (_vault == null)
                return;

            _shDay = AcquireBuffer<float>(SHDayBuffer, SHCoefficientCount);
            _shNight = AcquireBuffer<float>(SHNightBuffer, SHCoefficientCount);
            _shDiscreteStates = AcquireBuffer<float>(SHDiscreteStatesBuffer, SHCoefficientCount * SHStateCount);
            _shOutput = AcquireBuffer<float>(SHOutputBuffer, SHCoefficientCount);
            _shLightningScratch = AcquireBuffer<float>(SHLightningScratchBuffer, SHCoefficientCount);
            _telemetryRing = AcquireBuffer<GIRelayTelemetryEntry>(SHTelemetryRingBuffer, TelemetryCapacity);
            EnsureShUploadBuffers();

            BuildSHProfiles();
            _nativeStorageReady = true;
        }

        private void DisposeNativeStorage()
        {
            if (_hasPendingSHJob)
            {
                DispatcherJobFence.TryComplete(ref _pendingSHJob, forceComplete: true);
                _hasPendingSHJob = false;
            }

            ReleaseGIRelayVaultDescriptors();
            ReleaseShUploadBuffers();
            _vault = null;
            _telemetryCursor = 0;
            _telemetryCount = 0;
            _nativeStorageReady = false;
        }

        private VaultGenerationHandle<T> AcquireBuffer<T>(BufferID bufferId, int length) where T : struct
        {
            VaultGenerationHandle<T> handle = _vault.GetGenerationHandle<T>(bufferId, length, MemoryOwner, NativeArrayOptions.ClearMemory);
            if (!TryOpenGIRelayBuffer(in handle, bufferId, length, out NativeArray<T> buffer) || !buffer.IsCreated)
                throw new InvalidOperationException("GI relay DataVault buffer acquisition failed.");

            return handle;
        }

        private IDataVault ResolveDataVault()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            return vault;
        }

        private NativeArray<T> OpenGIRelayArray<T>(in VaultGenerationHandle<T> handle, BufferID bufferId, int requiredLength) where T : struct
        {
            return TryOpenGIRelayBuffer(in handle, bufferId, requiredLength, out NativeArray<T> buffer)
                ? buffer
                : default;
        }

        private bool TryOpenGIRelayBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (_vault == null ||
                requiredLength <= 0 ||
                !IsGIRelayVaultHandle(in handle, bufferId) ||
                !_vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsGIRelayVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)MemoryOwner &&
                   handle.Generation != 0u;
        }

        private void ReleaseGIRelayVaultDescriptors()
        {
            ReleaseGIRelayDescriptor(in _shDay, SHDayBuffer);
            ReleaseGIRelayDescriptor(in _shNight, SHNightBuffer);
            ReleaseGIRelayDescriptor(in _shDiscreteStates, SHDiscreteStatesBuffer);
            ReleaseGIRelayDescriptor(in _shOutput, SHOutputBuffer);
            ReleaseGIRelayDescriptor(in _shLightningScratch, SHLightningScratchBuffer);
            ReleaseGIRelayDescriptor(in _telemetryRing, SHTelemetryRingBuffer);
            _shDay = default;
            _shNight = default;
            _shDiscreteStates = default;
            _shOutput = default;
            _shLightningScratch = default;
            _telemetryRing = default;
        }

        private void ReleaseGIRelayDescriptor<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            if (_vault == null || !IsGIRelayVaultHandle(in handle, bufferId))
                return;

            _vault.ReleaseBuffer(in handle);
        }

        private void EnsureShUploadBuffers()
        {
            if (_shUploadBufferA != null && _shUploadBufferB != null)
                return;

            int stride = UnsafeUtility.SizeOf<float>();
            _shUploadBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, SHCoefficientCount, stride);
            _shUploadBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, SHCoefficientCount, stride);
        }

        private void ReleaseShUploadBuffers()
        {
            if (_shUploadBufferA != null)
            {
                _shUploadBufferA.Release();
                _shUploadBufferA = null;
            }

            if (_shUploadBufferB != null)
            {
                _shUploadBufferB.Release();
                _shUploadBufferB = null;
            }

            _shUploadWriteIndex = 0;
        }

        private void BuildSHProfiles()
        {
            NativeArray<float> day = OpenGIRelayArray(in _shDay, SHDayBuffer, SHCoefficientCount);
            NativeArray<float> night = OpenGIRelayArray(in _shNight, SHNightBuffer, SHCoefficientCount);
            NativeArray<float> states = OpenGIRelayArray(in _shDiscreteStates, SHDiscreteStatesBuffer, SHCoefficientCount * SHStateCount);
            WriteDirectionalAmbient(day, 0, new float3(0.62f, 0.78f, 0.96f) * daySHIntensity, 0.08f);
            WriteDirectionalAmbient(night, 0, new float3(0.055f, 0.075f, 0.125f) * nightSHIntensity, 0.025f);
            WriteDirectionalAmbient(states, 0, new float3(0.045f, 0.065f, 0.120f) * nightSHIntensity, 0.015f);
            WriteDirectionalAmbient(states, SHCoefficientCount, new float3(0.32f, 0.49f, 0.63f) * daySHIntensity, 0.045f);
            WriteDirectionalAmbient(states, SHCoefficientCount * 2, new float3(0.62f, 0.78f, 0.96f) * daySHIntensity, 0.08f);
            WriteDirectionalAmbient(states, SHCoefficientCount * 3, new float3(0.38f, 0.27f, 0.20f) * daySHIntensity, 0.04f);
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
            var biomeMatrix = GlobalRegistry.BiomeMatrix;
            if (biomeMatrix != null && math.isfinite(biomeMatrix.CurrentDepthMeters))
                return math.max(0f, biomeMatrix.CurrentDepthMeters);

            IPlayerRuntimeContext player = GlobalRegistry.Player;
            var movement = player != null ? player.PlayerMovement : null;
            if (movement != null && math.isfinite(movement.CurrentDepth))
                return math.max(0f, movement.CurrentDepth);

            var underwaterVisuals = GlobalRegistry.UnderwaterVisuals;
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

        private static float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            if (math.isfinite(weight))
                return math.saturate(weight);

            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            float tierIndex = math.clamp((float)tier, (float)HectonQualityTier.Low, (float)HectonQualityTier.Ultra);
            float normalized = math.saturate((tierIndex - (float)HectonQualityTier.Low) / math.max(0.0001f, (float)HectonQualityTier.Ultra - (float)HectonQualityTier.Low));
            return normalized * normalized * (3f - 2f * normalized);
        }

        private void ScheduleSHJob(in GIRelayRuntimeSnapshot snapshot, in BiomeGradientSignal biomeGradient)
        {
            NativeArray<float> day = OpenGIRelayArray(in _shDay, SHDayBuffer, SHCoefficientCount);
            NativeArray<float> night = OpenGIRelayArray(in _shNight, SHNightBuffer, SHCoefficientCount);
            NativeArray<float> states = OpenGIRelayArray(in _shDiscreteStates, SHDiscreteStatesBuffer, SHCoefficientCount * SHStateCount);
            NativeArray<float> output = OpenGIRelayArray(in _shOutput, SHOutputBuffer, SHCoefficientCount);
            if (!day.IsCreated || !night.IsCreated || !states.IsCreated || !output.IsCreated)
                return;

            GIRelaySHLerpJob job = new GIRelaySHLerpJob
            {
                SHDay = day,
                SHNight = night,
                SHDiscreteStates = states,
                SHOutput = output,
                TimeOfDay01 = snapshot.TimeOfDay01,
                Depth01 = snapshot.Depth01,
                Eclipse01 = snapshot.EclipseScalar,
                MoonPhase01 = snapshot.MoonPhase01,
                BiomeBlend01 = math.saturate(biomeGradient.BlendFactor01),
                DepthPaletteStrength = math.saturate(depthPaletteStrength),
                QualityWeight = ResolveGlobalQualityWeight()
            };

            _pendingSHJob = job.Schedule();
            _hasPendingSHJob = true;
        }

        private void CompleteAndPushPendingSHJob()
        {
            if (!DispatcherJobFence.TryFinalizeCompleted(ref _pendingSHJob))
                return;

            _hasPendingSHJob = false;

            NativeArray<float> output = OpenGIRelayArray(in _shOutput, SHOutputBuffer, SHCoefficientCount);
            if (!TryPushAmbientProbeFrom(output))
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

            EnsureShUploadBuffers();
            GraphicsBuffer target = _shUploadWriteIndex == 0 ? _shUploadBufferA : _shUploadBufferB;
            if (target == null || target.count < SHCoefficientCount || target.stride != UnsafeUtility.SizeOf<float>())
                return false;

            NativeArray<float> mapped = target.LockBufferForWrite<float>(0, SHCoefficientCount);
            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
            void* targetPtr = NativeArrayUnsafeUtility.GetUnsafePtr(mapped);
            UnsafeUtility.MemCpy(targetPtr, sourcePtr, expectedBytes);
            target.UnlockBufferAfterWrite<float>(SHCoefficientCount);
            Shader.SetGlobalBuffer(_HectonGIRelaySHBufferId, target);
            Shader.SetGlobalVector(_HectonGIRelaySHStateId, new Vector4(SHCoefficientCount, _sequence, _snapshot.Depth01, ResolveGlobalQualityWeight()));
            _shUploadWriteIndex ^= 1;
            return true;
        }

        private unsafe void PushLightningProbeOverlay()
        {
            NativeArray<float> output = OpenGIRelayArray(in _shOutput, SHOutputBuffer, SHCoefficientCount);
            NativeArray<float> scratch = OpenGIRelayArray(in _shLightningScratch, SHLightningScratchBuffer, SHCoefficientCount);
            if (!output.IsCreated || !scratch.IsCreated)
                return;

            int bytes = SHCoefficientCount * UnsafeUtility.SizeOf<float>();
            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(output);
            void* targetPtr = NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
            UnsafeUtility.MemCpy(targetPtr, sourcePtr, bytes);

            float scalar = math.saturate(_lightningScalar) * math.max(0f, lightningL0Boost);
            scratch[0] += scalar;
            scratch[SHChannelCoefficientCount] += scalar * 0.92f;
            scratch[SHChannelCoefficientCount * 2] += scalar * 0.78f;
            TryPushAmbientProbeFrom(scratch);
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

            var underwaterVisuals = GlobalRegistry.UnderwaterVisuals;
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
            NativeArray<GIRelayTelemetryEntry> telemetryRing = OpenGIRelayArray(in _telemetryRing, SHTelemetryRingBuffer, TelemetryCapacity);
            if (!telemetryRing.IsCreated || telemetryRing.Length <= 0)
                return;

            int index = _telemetryCursor;
            telemetryRing[index] = new GIRelayTelemetryEntry
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
            _telemetryCursor = index >= telemetryRing.Length ? 0 : index;
            if (_telemetryCount < telemetryRing.Length)
                _telemetryCount++;
        }

        private void DumpBlackBox()
        {
            NativeArray<GIRelayTelemetryEntry> telemetryRing = OpenGIRelayArray(in _telemetryRing, SHTelemetryRingBuffer, TelemetryCapacity);
            if (!telemetryRing.IsCreated || telemetryRing.Length <= 0)
                return;

            try
            {
                string fullPath = Path.Combine(Application.dataPath, "..", BlackBoxDumpPath);
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using FileStream stream = File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                using BinaryWriter writer = new BinaryWriter(stream);
                int count = telemetryRing.Length;
                int startIndex = _telemetryCount >= count ? _telemetryCursor : 0;
                for (int i = 0; i < count; i++)
                {
                    int entryIndex = startIndex + i;
                    if (entryIndex >= count)
                        entryIndex -= count;

                    GIRelayTelemetryEntry entry = telemetryRing[entryIndex];
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct GIRelaySHLerpJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<float> SHDay;
            [ReadOnly, NoAlias] public NativeArray<float> SHNight;
            [ReadOnly, NoAlias] public NativeArray<float> SHDiscreteStates;
            [WriteOnly, NoAlias] public NativeArray<float> SHOutput;
            public float TimeOfDay01;
            public float Depth01;
            public float Eclipse01;
            public float MoonPhase01;
            public float BiomeBlend01;
            public float DepthPaletteStrength;
            public float QualityWeight;

            public void Execute()
            {
                float daylight01 = ResolveDaylight(TimeOfDay01, Eclipse01);
                float3 depthTint = ResolveDepthTint(Depth01, DepthPaletteStrength, MoonPhase01, BiomeBlend01);
                int state = ResolveDiscreteState(TimeOfDay01, daylight01);
                int offset = state * SHCoefficientCount;
                float discreteWeight = 1f - Smooth01((math.saturate(QualityWeight) - 0.18f) * 5.0f);

                for (int i = 0; i < SHCoefficientCount; i++)
                {
                    float continuous = math.lerp(SHNight[i], SHDay[i], daylight01);
                    float snapped = SHDiscreteStates[offset + i];
                    float value = math.lerp(continuous, snapped, discreteWeight);
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

            private static float Smooth01(float value)
            {
                float x = math.saturate(value);
                return x * x * (3f - 2f * x);
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

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct GIRelayTelemetryEntry
        {
            [FieldOffset(0)]
            public int FrameIndex;
            [FieldOffset(4)]
            public uint Sequence;
            [FieldOffset(8)]
            public uint Flags;
            [FieldOffset(12)]
            public uint StateHash;
            [FieldOffset(16)]
            public int ShadowCascadeLevel;
            [FieldOffset(20)]
            public float DepthMeters;
            [FieldOffset(24)]
            public float TimeOfDay01;
            [FieldOffset(28)]
            public float EclipseScalar;
            [FieldOffset(32)]
            public float FogLod;
            [FieldOffset(36)]
            public float LightningScalar;
            [FieldOffset(40)]
            private ulong _pad0;
            [FieldOffset(48)]
            private ulong _pad1;
            [FieldOffset(56)]
            private ulong _pad2;
        }
    }
}
