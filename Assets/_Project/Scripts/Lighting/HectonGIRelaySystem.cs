using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Environment;
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
    public sealed partial class HectonGIRelaySystem : MonoBehaviour, IGIRelaySystem, ISlowTickable, ILateFrameTickable, IWeatherEventListener, IGlobalRegistryHotSwapListener
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
        private const uint RelayContextHash = 0x47495245u;
        private const uint SHLayoutMismatchHash = 0x53484C4Fu;
        private const uint NonFiniteInputHash = 0x4749464Eu;
        private const string BlackBoxDumpPath = "Docs/AgentLogs/Dump_13KRA.bin";

        private static readonly int _WaterVolumeId = Shader.PropertyToID("_WaterVolume");
        private static readonly int _HectonGIRelaySHBufferId = Shader.PropertyToID("_HectonGIRelaySHBuffer");

        [Header("Global Reflection")]
        [SerializeField] private Cubemap waterVolumeLowResCubemap;

        [Header("Scalars")]
        [SerializeField, Range(0f, 2f)] private float daySHIntensity = 0.74f;
        [SerializeField, Range(0f, 1f)] private float nightSHIntensity = 0.16f;
        [SerializeField, Range(0f, 2f)] private float lightningL0Boost = 0.85f;
        [SerializeField, Range(0f, 1f)] private float depthPaletteStrength = 0.82f;

        private IDataVault _vault;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private BiomeMatrixDirector _cachedBiomeMatrix;
        private float _cachedGlobalQualityWeight01 = 1f;
        private VaultGenerationHandle<CelestialStateDTO> _celestialStateRead;
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
        private Cubemap _lastWaterVolumeCubemap;
        private bool _hasPendingSHJob;
        private bool _nativeStorageReady;
        private bool _registeredGIRelayRuntime;
        private bool _registeredSlowTick;
        private bool _registeredLateFrameTick;
        private bool _registeredWeatherListener;
        private bool _registeredHotSwap;
        private bool _ambientProbeAuthorityActive;
        private bool _restoreBaseProbeAfterLightning;
        private bool _waterVolumeCubemapDirty;
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
            CacheDataVaultCold();
            EnsureNativeStorage();
            RefreshColdRuntimeDependencies();
            CaptureBaselineShadowCascades();
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return;
#endif

            CacheDataVaultCold();
            EnsureNativeStorage();
            RefreshColdRuntimeDependencies();
            CaptureBaselineShadowCascades();
            if (!_registeredGIRelayRuntime)
            {
                GlobalRegistry.RegisterGIRelayRuntime(this);
                _registeredGIRelayRuntime = true;
            }

            TryRegisterHotSwapListener();
            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            WeatherEvents.Register(this);
            _registeredWeatherListener = true;
            InvalidateShaderStateCache();
            _waterVolumeCubemapDirty = true;
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
                return;

            BiomeGradientSignal biomeGradient = ResolveLatestBiomeGradientSignal();
            GIRelayRuntimeSnapshot nextSnapshot = BuildRuntimeSnapshot(in biomeGradient);
            if (!IsSnapshotFinite(in nextSnapshot))
            {
                RecordTelemetry(in nextSnapshot, GIRelayTelemetryFlags.NonFinite);
                DumpBlackBox();
                GlobalTelemetryBus.PublishPerformanceWarning(NonFiniteInputHash, RelayContextHash, nextSnapshot.DepthMeters);
                return;
            }

            _snapshot = nextSnapshot;
            ApplyShadowCascadeState(nextSnapshot.DepthMeters);
            _waterVolumeCubemapDirty = true;
            ScheduleSHJob(in nextSnapshot, in biomeGradient);
            RecordTelemetry(in nextSnapshot, GIRelayTelemetryFlags.Scheduled);
        }

        public void LateFrameTick()
        {
            if (_waterVolumeCubemapDirty)
            {
                _waterVolumeCubemapDirty = false;
                BindGlobalWaterVolumeCubemap();
            }

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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                bool restoreSlow = _registeredSlowTick;
                bool restoreLate = _registeredLateFrameTick || HasLateFrameWork();
                UnregisterTickLanes();
                if (currentService != null && isActiveAndEnabled)
                    RegisterTickLanes(restoreSlow, restoreLate);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                RebindDataVault(currentService as IDataVault);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.BiomeMatrixRuntime)
                _cachedBiomeMatrix = currentService as BiomeMatrixDirector;
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

            UnregisterTickLanes();
            TryUnregisterHotSwapListener();

            if (_registeredGIRelayRuntime && ReferenceEquals(GlobalRegistry.GIRelay, this))
                GlobalRegistry.UnregisterGIRelayRuntime(this);
            _registeredGIRelayRuntime = false;

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
            _lastWaterVolumeCubemap = null;
        }

        private void RegisterTickLanes(bool restoreSlow, bool restoreLate)
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (restoreSlow && !_registeredSlowTick)
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            if (restoreLate && !_registeredLateFrameTick)
                _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void UnregisterTickLanes()
        {
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
        }

        private bool HasLateFrameWork()
        {
            return _waterVolumeCubemapDirty
                || _hasPendingSHJob
                || _lightningFramesRemaining > 0
                || _restoreBaseProbeAfterLightning;
        }

        private void RebindDataVault(IDataVault nextVault)
        {
            if (ReferenceEquals(_vault, nextVault))
                return;

            if (_hasPendingSHJob)
            {
                DispatcherJobFence.TryComplete(ref _pendingSHJob, forceComplete: true);
                _hasPendingSHJob = false;
            }

            DisposeNativeStorage();
            _vault = nextVault;
            if (_vault != null && isActiveAndEnabled)
            {
                EnsureNativeStorage();
                RefreshColdRuntimeDependencies();
                _waterVolumeCubemapDirty = true;
            }
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

        private void EnsureNativeStorage()
        {
            if (_nativeStorageReady)
                return;

            _vault = ResolveDataVault();
            if (_vault == null || _vault.IsAllocationLocked)
                return;

            _shDay = AcquireBuffer<float>(SHDayBuffer, SHCoefficientCount, NativeArrayOptions.UninitializedMemory);
            _shNight = AcquireBuffer<float>(SHNightBuffer, SHCoefficientCount, NativeArrayOptions.UninitializedMemory);
            _shDiscreteStates = AcquireBuffer<float>(SHDiscreteStatesBuffer, SHCoefficientCount * SHStateCount, NativeArrayOptions.UninitializedMemory);
            _shOutput = AcquireBuffer<float>(SHOutputBuffer, SHCoefficientCount, NativeArrayOptions.UninitializedMemory);
            _shLightningScratch = AcquireBuffer<float>(SHLightningScratchBuffer, SHCoefficientCount, NativeArrayOptions.UninitializedMemory);
            _telemetryRing = AcquireBuffer<GIRelayTelemetryEntry>(SHTelemetryRingBuffer, TelemetryCapacity);
            if (!HasRequiredGIRelayStorage())
            {
                _nativeStorageReady = false;
                return;
            }

            if (!EnsureDayNightRelayNativeStorage())
            {
                _nativeStorageReady = false;
                return;
            }

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

            ReleaseDayNightRelayNativeStorage();
            ReleaseGIRelayVaultDescriptors();
            ReleaseShUploadBuffers();
            _vault = null;
            _cachedPlayerContext = null;
            _cachedBiomeMatrix = null;
            _cachedGlobalQualityWeight01 = 1f;
            _celestialStateRead = default;
            _telemetryCursor = 0;
            _telemetryCount = 0;
            _nativeStorageReady = false;
        }

        private VaultGenerationHandle<T> AcquireBuffer<T>(
            BufferID bufferId,
            int length,
            NativeArrayOptions options = NativeArrayOptions.ClearMemory) where T : struct
        {
            if (_vault == null)
                return default;

            VaultGenerationHandle<T> handle = _vault.EnsureGenerationHandle<T>(bufferId, length, MemoryOwner, options);
            if (!TryOpenGIRelayBuffer(in handle, bufferId, length, out NativeArray<T> buffer) || !buffer.IsCreated)
                return default;

            return handle;
        }

        private bool HasRequiredGIRelayStorage()
        {
            return TryOpenGIRelayBuffer(in _shDay, SHDayBuffer, SHCoefficientCount, out NativeArray<float> day) &&
                   day.IsCreated &&
                   TryOpenGIRelayBuffer(in _shNight, SHNightBuffer, SHCoefficientCount, out NativeArray<float> night) &&
                   night.IsCreated &&
                   TryOpenGIRelayBuffer(in _shDiscreteStates, SHDiscreteStatesBuffer, SHCoefficientCount * SHStateCount, out NativeArray<float> states) &&
                   states.IsCreated &&
                   TryOpenGIRelayBuffer(in _shOutput, SHOutputBuffer, SHCoefficientCount, out NativeArray<float> output) &&
                   output.IsCreated &&
                   TryOpenGIRelayBuffer(in _shLightningScratch, SHLightningScratchBuffer, SHCoefficientCount, out NativeArray<float> scratch) &&
                   scratch.IsCreated &&
                   TryOpenGIRelayBuffer(in _telemetryRing, SHTelemetryRingBuffer, TelemetryCapacity, out NativeArray<GIRelayTelemetryEntry> telemetry) &&
                   telemetry.IsCreated;
        }

        private IDataVault ResolveDataVault()
        {
            return _vault;
        }

        private void CacheDataVaultCold()
        {
            if (_vault == null)
                _vault = GlobalRegistry.DataVault;
        }

        private void RefreshColdRuntimeDependencies()
        {
            _cachedPlayerContext = GlobalRegistry.Player;
            _cachedBiomeMatrix = GlobalRegistry.BiomeMatrix;
            _cachedGlobalQualityWeight01 = ResolveGlobalQualityWeight();
            if (_vault != null &&
                _vault.TryGetGenerationHandle<CelestialStateDTO>(BufferID.Shinobu345CelestialStateRead, out VaultGenerationHandle<CelestialStateDTO> celestialHandle))
            {
                _celestialStateRead = celestialHandle;
            }
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
            if (AreShUploadBuffersReady())
                return;

            ReleaseShUploadBuffers();
            int stride = UnsafeUtility.SizeOf<float>();
            _shUploadBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, SHCoefficientCount, stride);
            _shUploadBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, SHCoefficientCount, stride);
        }

        private bool AreShUploadBuffersReady()
        {
            int stride = UnsafeUtility.SizeOf<float>();
            return _shUploadBufferA != null &&
                   _shUploadBufferB != null &&
                   _shUploadBufferA.IsValid() &&
                   _shUploadBufferB.IsValid() &&
                   _shUploadBufferA.count >= SHCoefficientCount &&
                   _shUploadBufferB.count >= SHCoefficientCount &&
                   _shUploadBufferA.stride == stride &&
                   _shUploadBufferB.stride == stride;
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

        private GIRelayRuntimeSnapshot BuildRuntimeSnapshot(in BiomeGradientSignal biomeGradient)
        {
            bool hasCelestialState = TryReadCelestialState(out CelestialStateDTO celestialState);
            float previousTime = math.isfinite(_snapshot.TimeOfDay01) ? math.saturate(_snapshot.TimeOfDay01) : 0.5f;
            float timeOfDay01 = hasCelestialState ? H8SaturateFinite(celestialState.TimeOfDay01) : previousTime;
            float previousEclipse = math.isfinite(_snapshot.EclipseScalar) ? math.saturate(_snapshot.EclipseScalar) : 0f;
            float eclipse01 = hasCelestialState ? H8SaturateFinite(celestialState.EclipseShadowScalar01) : previousEclipse;
            float moonPhase01 = math.isfinite(_snapshot.MoonPhase01) ? math.saturate(_snapshot.MoonPhase01) : 0.35f;
            double dayBase = math.isfinite(_snapshot.AbsoluteUniverseTime)
                ? math.floor(_snapshot.AbsoluteUniverseTime * SecondsPerDayRcp) * SecondsPerDay
                : 0d;
            double absoluteUniverseTime = dayBase + timeOfDay01 * SecondsPerDay;
            float depthMeters = ResolveDepthMetersAbsolute();
            float depth01 = math.saturate(depthMeters * DepthPaletteFullDepthMetersRcp);
            float fogLod = ResolveFogLod(depth01, eclipse01, biomeGradient.BlendFactor01);
            uint flags = (uint)GIRelayTelemetryFlags.Valid;
            if (_lightningFramesRemaining > 0)
                flags |= (uint)GIRelayTelemetryFlags.LightningActive;

            _sequence++;
            return new GIRelayRuntimeSnapshot
            {
                AbsoluteUniverseTime = absoluteUniverseTime,
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

        private bool TryReadCelestialState(out CelestialStateDTO state)
        {
            state = default;
            if (_vault == null ||
                _celestialStateRead.BufferID != unchecked((uint)(int)BufferID.Shinobu345CelestialStateRead) ||
                _celestialStateRead.Generation == 0u ||
                !_vault.TryReadOnlyHandle(in _celestialStateRead, out NativeArray<CelestialStateDTO>.ReadOnly states) ||
                !states.IsCreated ||
                states.Length <= 0)
            {
                return false;
            }

            state = states[0];
            return math.isfinite(state.TimeOfDay01) && math.isfinite(state.EclipseShadowScalar01);
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

        private float ResolveDepthMetersAbsolute()
        {
            BiomeMatrixDirector biomeMatrix = _cachedBiomeMatrix;
            if (biomeMatrix != null && math.isfinite(biomeMatrix.CurrentDepthMeters))
                return math.max(0f, biomeMatrix.CurrentDepthMeters);

            IPlayerRuntimeContext player = _cachedPlayerContext;
            var movement = player != null ? player.PlayerMovement : null;
            if (movement != null && math.isfinite(movement.CurrentDepth))
                return math.max(0f, movement.CurrentDepth);

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

        private float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            if (math.isfinite(weight))
            {
                _cachedGlobalQualityWeight01 = math.saturate(weight);
                return _cachedGlobalQualityWeight01;
            }

            return _cachedGlobalQualityWeight01;
        }

        private void ScheduleSHJob(in GIRelayRuntimeSnapshot snapshot, in BiomeGradientSignal biomeGradient)
        {
            NativeArray<float> day = OpenGIRelayArray(in _shDay, SHDayBuffer, SHCoefficientCount);
            NativeArray<float> night = OpenGIRelayArray(in _shNight, SHNightBuffer, SHCoefficientCount);
            NativeArray<float> states = OpenGIRelayArray(in _shDiscreteStates, SHDiscreteStatesBuffer, SHCoefficientCount * SHStateCount);
            NativeArray<float> output = OpenGIRelayArray(in _shOutput, SHOutputBuffer, SHCoefficientCount);
            NativeArray<LightingGradientProfileDTO> profiles = OpenDayNightRelayArray(in _lightingGradientProfiles, DayNightGradientProfilesBuffer, DayNightGradientProfileCapacity);
            NativeArray<int> profileCount = OpenDayNightRelayArray(in _lightingGradientProfileCount, DayNightGradientProfileCountBuffer, 1);
            if (!day.IsCreated || !night.IsCreated || !states.IsCreated || !output.IsCreated)
                return;

            NativeArray<EnvironmentLightingDTO> environment = OpenDayNightRelayArray(in _environmentLighting, DayNightEnvironmentLightingBuffer, 1);
            if (!environment.IsCreated)
                return;

            EvaluateGlobalIlluminationJob job = new EvaluateGlobalIlluminationJob
            {
                SHDay = day,
                SHNight = night,
                SHDiscreteStates = states,
                GradientProfiles = profiles,
                GradientProfileCount = profileCount,
                SHOutput = output,
                EnvironmentLighting = environment,
                BiomeGradient = biomeGradient,
                PlayerAup = ResolvePlayerAupDouble(),
                BiomeCenterAup = ResolveBiomeCenterAup(in biomeGradient),
                TimeOfDay01 = snapshot.TimeOfDay01,
                DepthMeters = snapshot.DepthMeters,
                Depth01 = snapshot.Depth01,
                Eclipse01 = snapshot.EclipseScalar,
                MoonPhase01 = snapshot.MoonPhase01,
                DepthPaletteStrength = math.saturate(depthPaletteStrength),
                QualityWeight = ResolveDayNightQualityWeight(),
                WaterExtinctionConstant = ResolveWaterExtinctionConstant(),
                EclipseDarkeningMultiplier = ResolveEclipseDarkeningMultiplier()
            };

            _pendingDayNightScheduleTicks = System.Diagnostics.Stopwatch.GetTimestamp();
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

            TryUploadDayNightLightingCBuffer();
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

            if (!AreShUploadBuffersReady())
                return false;

            GraphicsBuffer target = _shUploadWriteIndex == 0 ? _shUploadBufferA : _shUploadBufferB;
            if (target == null || target.count < SHCoefficientCount || target.stride != UnsafeUtility.SizeOf<float>())
                return false;

            NativeArray<float> mapped = target.LockBufferForWrite<float>(0, SHCoefficientCount);
            try
            {
                void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
                void* targetPtr = NativeArrayUnsafeUtility.GetUnsafePtr(mapped);
                UnsafeUtility.MemCpy(targetPtr, sourcePtr, expectedBytes);
            }
            finally
            {
                target.UnlockBufferAfterWrite<float>(SHCoefficientCount);
            }

            Shader.SetGlobalBuffer(_HectonGIRelaySHBufferId, target);
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
            int target = _shadowCascadeLevel < 0 ? _baselineShadowCascades : _shadowCascadeLevel;
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
                FrameIndex = unchecked((int)Hecton8.Core.SystemDispatcher.CurrentFrameId),
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

        private unsafe void DumpBlackBox()
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
                GIRelayDumpHeader header = new GIRelayDumpHeader
                {
                    Magic = 0x47495245u,
                    EntryStrideBytes = UnsafeUtility.SizeOf<GIRelayTelemetryEntry>(),
                    EntryCount = telemetryRing.Length,
                    Cursor = _telemetryCursor,
                    RecordedCount = _telemetryCount,
                    Sequence = _snapshot.Sequence,
                    Reserved0 = 0u,
                    Reserved1 = 0u
                };
                stream.Write(new ReadOnlySpan<byte>(UnsafeUtility.AddressOf(ref header), UnsafeUtility.SizeOf<GIRelayDumpHeader>()));
                int count = telemetryRing.Length;
                int startIndex = _telemetryCount >= count ? _telemetryCursor : 0;
                for (int i = 0; i < count; i++)
                {
                    int entryIndex = startIndex + i;
                    if (entryIndex >= count)
                        entryIndex -= count;

                    GIRelayTelemetryEntry entry = telemetryRing[entryIndex];
                    stream.Write(new ReadOnlySpan<byte>(UnsafeUtility.AddressOf(ref entry), UnsafeUtility.SizeOf<GIRelayTelemetryEntry>()));
                }
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogException(exception, this);
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

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct GIRelayDumpHeader
        {
            [FieldOffset(0)]
            public uint Magic;
            [FieldOffset(4)]
            public int EntryStrideBytes;
            [FieldOffset(8)]
            public int EntryCount;
            [FieldOffset(12)]
            public int Cursor;
            [FieldOffset(16)]
            public int RecordedCount;
            [FieldOffset(20)]
            public uint Sequence;
            [FieldOffset(24)]
            public uint Reserved0;
            [FieldOffset(28)]
            public uint Reserved1;
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
