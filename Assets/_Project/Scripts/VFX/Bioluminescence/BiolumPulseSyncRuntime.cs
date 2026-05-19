using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using ScalabilityChangedEvent = Hecton8.Core.Contracts.Signals.ScalabilityChangedEvent;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.VFX.Bioluminescence
{
    /// <summary>
    /// Per-instance coral glow state. Four records fit exactly in one 64-byte cache line.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct GlowStateDTO
    {
        public uint PackedColor;
        public float Phase;
        public float Frequency;
        public uint SpeciesHash;
    }

    /// <summary>
    /// Global bioluminescence wave trigger using double precision AUP before local float math.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct SyncPulseDTO
    {
        public double3 OriginAUP;
        public float WaveSpeed;
        public uint ColorOverride;
    }

    /// <summary>
    /// Local weather and survival mock input for the bioluminescence domain.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct MockWeatherSignal
    {
        public float AmbientLightLevel;
        public float O2Level01;
        public float SystemHealthIndex01;
        public uint CurrentBiomeHash;
    }

    /// <summary>
    /// Designer-tunable species row stored in unmanaged DataVault memory.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct BiolumSpeciesTuningDTO
    {
        [FieldOffset(0)]
        public uint SpeciesHash;
        [FieldOffset(4)]
        public uint PackedColor;
        [FieldOffset(8)]
        public float Frequency;
        [FieldOffset(12)]
        public float WaveSpeed;
        [FieldOffset(16)]
        public float BiomeBlend01;
#pragma warning disable 0169
        [FieldOffset(20)]
        private uint _pad0;
#pragma warning restore 0169
    }

    /// <summary>
    /// Local predator proximity mock. This protects the glow domain from fauna compile churn.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 40)]
    public partial struct MockPredatorProximitySignal
    {
        public double3 OriginAUP;
        public float RadiusMeters;
        public float Strength01;
        public uint SpeciesMask;
        public uint FrameStamp;
    }

    /// <summary>
    /// Local combat damage mock for visual flicker without combat-domain coupling.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 40)]
    public partial struct MockCombatDamageSignal
    {
        public double3 OriginAUP;
        public float RadiusMeters;
        public float AgeSeconds;
        public uint PackedDamageColor;
        public uint FrameStamp;
    }

    public static class BiolumPackedColorUtility
    {
        /// <summary>
        /// Packs normalized linear RGB plus alpha into RGB10_A2.
        /// </summary>
        public static uint PackRgb10A2(float3 linearRgb, float alpha01)
        {
            uint r = (uint)math.round(math.saturate(linearRgb.x) * 1023f);
            uint g = (uint)math.round(math.saturate(linearRgb.y) * 1023f);
            uint b = (uint)math.round(math.saturate(linearRgb.z) * 1023f);
            uint a = (uint)math.round(math.saturate(alpha01) * 3f);
            return (r & 1023u) | ((g & 1023u) << 10) | ((b & 1023u) << 20) | ((a & 3u) << 30);
        }

        /// <summary>
        /// Unpacks RGB10_A2 into normalized linear RGB.
        /// </summary>
        public static float3 UnpackRgb10A2(uint packed)
        {
            const float inv1023 = 1f / 1023f;
            return new float3(
                (packed & 1023u) * inv1023,
                ((packed >> 10) & 1023u) * inv1023,
                ((packed >> 20) & 1023u) * inv1023);
        }

        /// <summary>
        /// Blends two RGB10_A2 values without UnityEngine.Color.
        /// </summary>
        public static uint LerpPackedColor(uint from, uint to, float t)
        {
            float s = math.saturate(t);
            uint ar = from & 1023u;
            uint ag = (from >> 10) & 1023u;
            uint ab = (from >> 20) & 1023u;
            uint aa = (from >> 30) & 3u;
            uint br = to & 1023u;
            uint bg = (to >> 10) & 1023u;
            uint bb = (to >> 20) & 1023u;
            uint ba = (to >> 30) & 3u;
            uint r = (uint)math.round(math.lerp((float)ar, (float)br, s));
            uint g = (uint)math.round(math.lerp((float)ag, (float)bg, s));
            uint b = (uint)math.round(math.lerp((float)ab, (float)bb, s));
            uint a = (uint)math.round(math.lerp((float)aa, (float)ba, s));
            return (r & 1023u) | ((g & 1023u) << 10) | ((b & 1023u) << 20) | ((a & 3u) << 30);
        }
    }

    /// <summary>
    /// Global shader heartbeat for flora/fauna bioluminescence. Visual authority only.
    /// </summary>
    [DefaultExecutionOrder(-2520)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/VFX/Bioluminescence/Pulse Sync Runtime")]
    public sealed class BiolumPulseSyncRuntime : MonoBehaviour,
        IUpdatable,
        ILateFrameTickable,
        IScalabilityChangedEventListener,
        IGlobalRegistryHotSwapListener,
        IGlobalRegistryHotSwapRefListener,
        IDisposable
    {
        public const int MaxGlobalBiolumStates = 16;
        public const int MaxGlowInstances = 50000;
        public const int MaxSpeciesTuningCount = 150;
        public const int SyncGroupCount = 4;

        private const int ProfileFloatStride = 8;
        private const int ProfileFloatCount = MaxGlobalBiolumStates * ProfileFloatStride;
        private const int ProfileByteCount = ProfileFloatCount * 4;
        private const int SyncPulseCapacity = 16;
        private const int CsvScratchByteCount = 16 * 1024;
        private const int BlackBoxFrameCount = 300;
        private const int BiolumJobInnerLoopBatchCount = 64; // 64 uint writes = 256 bytes; avoids cache-line boundary churn.
        private const int CsvWorkerIdle = 0;
        private const int CsvWorkerRequested = 1;
        private const int CsvWorkerApplying = 2;
        private const float StrobeDurationSeconds = 0.1f;
        private const float StrobeFadeSeconds = 0.16f;
        private const float OverloadUpdateIntervalSeconds = 1f / 15f;
        private const float LowQualityUpdateIntervalSeconds = 1f / 5f;
        private const float NormalUpdateIntervalSeconds = 0f;
        private const float MaxHdrIntensity = 10f;
        private const float DefaultPingRadiusMeters = 80f;
        private const int JobOverrunDumpFrameThreshold = BlackBoxFrameCount;
        private const byte TelemetryFlagNonFinite = 1;
        private const byte TelemetryFlagJobOverrun = 2;
        private const byte TelemetryFlagAupInvalid = 4;
        private const ushort BlackBoxEntrySizeBytes = 32;
        private const uint BlackBoxMagic = 0x42505359u; // BPSY
        private const uint ProfileFallbackHash = 0x424C4642u; // BLFB
        private const uint ProfileBinaryHash = 0x424C554Du; // BLUM
        private const uint EmergencyNeonBluePacked = 0xFBBE1000u;
        private const string ProfileFileName = "Biolum_Profiles.bin";
        private const string ProfileH8BinFileName = "Biolum_Profiles.h8bin";
        private const string LegacyPaletteArchiveName = "biolum_color_palettes.h8bin";
        private const string LegacyPulseArchiveName = "flora_pulse_rates.bin";
        private const string CsvOverrideFileName = "biolum_profiles.csv";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_BIOLUM_SYNC.bin";
        private const string DumpMirrorRelativePath = "Docs/AgentLogs/Dump_BIOLUM_SYNC.h8dump";

        private static readonly ProfilerMarker _tickMarker = new ProfilerMarker("H8.VFX.BiolumPulseSync.Tick");
        private static readonly ProfilerMarker _lateFrameMarker = new ProfilerMarker("H8.VFX.BiolumPulseSync.LateFrame");
        private static readonly int _GlobalBiolumDearLieGroupsId = Shader.PropertyToID("_GlobalBiolumDearLieGroups");
        private static readonly int _GlobalBiolumParamsId = Shader.PropertyToID("_GlobalBiolumParams");
        private static readonly int _GlobalBiolumClockId = Shader.PropertyToID("_GlobalBiolumClock");
        private static readonly int _GlobalBiolumAupOffsetId = Shader.PropertyToID("_GlobalBiolumAupOffset");
        private static readonly int _BiolumIntensityId = Shader.PropertyToID("_BiolumIntensity");
        private static readonly int _BiolumGpuColorBufferId = Shader.PropertyToID("_BiolumGpuColorBuffer");
        private static int s_runtimeClaimed;

        private IDataVault _dataVault;
        private VaultBufferHandle<float> _profileFloatsHandle;
        private VaultBufferHandle<float4> _jobStatesHandle;
        private VaultBufferHandle<BiolumPulseTelemetryEntry> _blackBoxHandle;
        private VaultBufferHandle<GlowStateDTO> _glowStatesHandle;
        private VaultBufferHandle<uint> _gpuColorFrontHandle;
        private VaultBufferHandle<uint> _gpuColorBackHandle;
        private VaultBufferHandle<double3> _glowAupOriginsHandle;
        private VaultBufferHandle<SyncPulseDTO> _syncPulsesHandle;
        private VaultBufferHandle<float> _syncPulseAgesHandle;
        private VaultBufferHandle<MockWeatherSignal> _mockWeatherSignalHandle;
        private VaultBufferHandle<MockPredatorProximitySignal> _mockPredatorSignalHandle;
        private VaultBufferHandle<MockCombatDamageSignal> _mockDamageSignalHandle;
        private VaultBufferHandle<BiolumSpeciesTuningDTO> _speciesTuningHandle;
        private VaultBufferHandle<byte> _csvScratchHandle;
        private ITickDispatcher _tickDispatcher;
        private GraphicsBuffer _gpuColorBufferA;
        private GraphicsBuffer _gpuColorBufferB;
        private string _csvOverridePath;
        private FileSystemWatcher _csvWatcher;
        private JobHandle _stateJobHandle;
        private HectonQualityTier _qualityTier = HectonQualityTier.Unknown;
        private float3 _aupOriginOffset;
        private Matrix4x4 _dearLieGroupMatrix = Matrix4x4.zero;
        private double _localTimeSeconds;
        private double3 _lastPulseOriginAUP;
        private float _updateAccumulatorSeconds;
        private float _overloadHoldSeconds;
        private float _strobeTimerSeconds;
        private float _strobePeak01;
        private float _lastOscillatorComputeTimeMs;
        private float _biomeBlend01;
        private float _globalQualityWeight = 1f;
        private float _individualGlowWeight01;
        private float _dearLieBlend01 = 1f;
        private uint _frameCounter;
        private uint _profileSourceHash = ProfileFallbackHash;
        private uint _vaultGenerationId;
        private uint _lastBiomeHash;
        private uint _lastPredatorSignalFrame;
        private int _activeStateCount = 1;
        private int _publishedGlobalStateCount = SyncGroupCount;
        private int _activeGlowInstanceCount = MaxGlowInstances;
        private int _scheduledGpuColorCount = SyncGroupCount;
        private int _publishedGpuColorCount;
        private int _activeSyncPulseCount;
        private int _activeBiolumProfileId;
        private int _blackBoxCursor;
        private int _jobOverrunFrames;
        private long _stateJobScheduleTimestamp;
        private long _csvLastWriteTicks;
        private int _gpuColorFrontIndex;
        private int _csvWorkerState;
        private int _lastGlobalDamageSignalSequence;
        private int _lastGlobalLightLevelSignalSequence;
        private int _lastGlobalSurvivalVitalsSequence;
        private byte _pendingTelemetryFlags;
        private bool _registeredUpdate;
        private bool _registeredLateFrame;
        private bool _registeredScalability;
        private bool _registeredHotSwap;
        private bool _stateJobScheduled;
        private bool _jobLocksHeld;
        private bool _mockGlowsInitialized;
        private bool _gpuColorBufferUploaded;
        private bool _disposed;
        private bool _dumpedFault;
        private bool _forceSchedule = true;
        private bool _profilesLoaded;
        private bool _runtimeClaimHeld;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeClaim()
        {
            Volatile.Write(ref s_runtimeClaimed, 0);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureSceneRuntime()
        {
            if (!Application.isPlaying)
                return;

            if (Volatile.Read(ref s_runtimeClaimed) != 0)
                return;

            // COLD ALLOC: GameObject[1] - scene-local visual director host when authoring has not placed the component - owner: BIOLUM_PULSE_SYNC
            GameObject host = new GameObject("H8_BiolumPulseSyncRuntime");
            host.hideFlags = HideFlags.DontSave;
            host.AddComponent<BiolumPulseSyncRuntime>();
        }

        private bool TryClaimRuntimeOwner()
        {
            if (_runtimeClaimHeld)
                return true;

            if (Interlocked.CompareExchange(ref s_runtimeClaimed, 1, 0) != 0)
                return false;

            _runtimeClaimHeld = true;
            return true;
        }

        private void OnEnable()
        {
            _disposed = false;
            if (!TryClaimRuntimeOwner())
            {
                enabled = false;
                return;
            }

            if (!AreSyncLayoutsValid())
            {
                ReleaseRuntimeOwnerClaim();
                enabled = false;
                return;
            }

            TryRegisterHotSwapListener();
            RefreshQualityTier(GlobalRegistry.ScalabilityTier);
            EnsureVaultBuffers();
            EnsureGpuColorBuffer();
            EnsureCsvBackgroundWatcher();
            if (!_profilesLoaded)
                LoadProfilesFromDiskOrDefaults();
            if (!_mockGlowsInitialized)
                GenerateEmergencyMockGlows();
            TryRegisterScalabilityEvents();
            TryRegisterUpdate();
            TryRegisterLateFrame();
            EvaluateColdStartStates();
            UploadShaderGlobals(forceStateArray: true);
            _forceSchedule = true;
        }

        private void OnDisable()
        {
            TryUnregisterUpdate();
            TryUnregisterLateFrame();

            if (_registeredScalability)
            {
                ScalabilityEvents.Unregister(this);
                _registeredScalability = false;
            }

            TryUnregisterHotSwapListener();
            CompleteScheduledJob();
            ClearShaderGlobals();
            StopCsvBackgroundWatcher();
            ReleaseVaultHandlesOnly(invalidateProfiles: false);
            ReleaseGpuColorBuffer();
            _tickDispatcher = null;
            ReleaseRuntimeOwnerClaim();
        }

        private void OnDestroy()
        {
            if (!_disposed)
                Dispose();
        }

        public void Tick(float deltaTime)
        {
            using (_tickMarker.Auto())
            {
                if (!HasVaultBuffers() && !TryRefreshExistingVaultHandlesNoAllocate())
                    return;

                float dt = SanitizeDelta(deltaTime);
                AdvanceSimulationFrameCounter();
                AdvanceTime(dt);
                ConsumeAupShiftSignals();
                ConsumeGlobalSignalMirrors();
                ConsumeFrameTimeSignals(dt);
                ConsumeAcousticPingSignals();
                AdvanceStrobe(dt);
                RefreshGlobalQualityWeight();
                UpdateBiomeBlendState(dt);
                AdvanceMockPredatorSignal(dt);
                ConsumeMockPredatorSignalToPulse();
                AdvanceSyncPulseAges(dt);
                AdvanceMockDamageAge(dt);
                ApplyCsvOverridesIfReady();
                UploadShaderScalars();

                float cadence = ResolveUpdateCadenceSeconds(_globalQualityWeight, _overloadHoldSeconds);
                _updateAccumulatorSeconds += dt;
                bool scheduleDue = _registeredLateFrame && !_stateJobScheduled && (_forceSchedule || cadence <= 0f || _updateAccumulatorSeconds >= cadence);
                if (scheduleDue)
                {
                    _forceSchedule = false;
                    _updateAccumulatorSeconds = 0f;
                    ScheduleStateJob(cadence, dt);
                }

                RecordTelemetry(_pendingTelemetryFlags);
                _pendingTelemetryFlags = 0;
            }
        }

        public void LateFrameTick()
        {
            using (_lateFrameMarker.Auto())
            {
                if (!HasVaultBuffers() && !TryRefreshExistingVaultHandlesNoAllocate())
                    return;

                CompleteScheduledJobAndPublish();
            }
        }

        public void OnScalabilityChanged(in ScalabilityChangedEvent payload)
        {
            RefreshQualityTier(payload.CurrentQualityTier);
            _forceSchedule = true;
        }

        public void Dispose()
        {
            TryUnregisterUpdate();
            TryUnregisterLateFrame();
            CompleteScheduledJob();
            TryUnregisterHotSwapListener();
            StopCsvBackgroundWatcher();
            ReleaseVaultHandlesOnly(invalidateProfiles: true);
            ReleaseGpuColorBuffer();
            _dataVault = null;
            _tickDispatcher = null;
            ReleaseRuntimeOwnerClaim();
            _disposed = true;
        }

        private void ReleaseRuntimeOwnerClaim()
        {
            if (!_runtimeClaimHeld)
                return;

            _runtimeClaimHeld = false;
            Volatile.Write(ref s_runtimeClaimed, 0);
        }

        /// <summary>
        /// Reads one live species tuning row from DataVault for editor tooling.
        /// </summary>
        public static bool TryReadEditorSpeciesTuning(int index, out BiolumSpeciesTuningDTO tuning)
        {
            tuning = default;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsCompactionFenceActive || index < 0)
                return false;

            if (!vault.TryGetBufferHandle(BufferID.BiolumSpeciesTuning, out VaultBufferHandle<BiolumSpeciesTuningDTO> handle) ||
                index >= handle.Length)
            {
                return false;
            }

            if (!vault.TryLockBuffer(BufferID.BiolumSpeciesTuning, SystemID.Vfx))
                return false;

            try
            {
                NativeArray<BiolumSpeciesTuningDTO> species = handle.Resolve(vault);
                if (!species.IsCreated || index >= species.Length)
                    return false;

                tuning = species[index];
                return true;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.BiolumSpeciesTuning, SystemID.Vfx);
            }
        }

        /// <summary>
        /// Writes one live species tuning row into DataVault for editor tooling.
        /// </summary>
        public static bool TryWriteEditorSpeciesTuning(int index, in BiolumSpeciesTuningDTO tuning)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsCompactionFenceActive || index < 0)
                return false;

            if (!vault.TryGetBufferHandle(BufferID.BiolumSpeciesTuning, out VaultBufferHandle<BiolumSpeciesTuningDTO> handle) ||
                index >= handle.Length)
            {
                return false;
            }

            vault.TryGetBufferHandle(BufferID.BiolumGlowStates, out VaultBufferHandle<GlowStateDTO> glowHandle);

            bool lockedGlowStates = false;
            bool lockedSpecies = false;
            try
            {
                lockedGlowStates = glowHandle.Length > 0 && vault.TryLockBuffer(BufferID.BiolumGlowStates, SystemID.Vfx);
                lockedSpecies = vault.TryLockBuffer(BufferID.BiolumSpeciesTuning, SystemID.Vfx);
                if (!lockedSpecies)
                    return false;

                NativeArray<BiolumSpeciesTuningDTO> species = handle.Resolve(vault);
                if (!species.IsCreated || index >= species.Length)
                    return false;

                species[index] = tuning;
                if (lockedGlowStates)
                {
                    NativeArray<GlowStateDTO> glowStates = glowHandle.Resolve(vault);
                    ApplySpeciesTuningToGlowStates(glowStates, tuning);
                }

                return true;
            }
            finally
            {
                if (lockedSpecies)
                    vault.TryUnlockBuffer(BufferID.BiolumSpeciesTuning, SystemID.Vfx);
                if (lockedGlowStates)
                    vault.TryUnlockBuffer(BufferID.BiolumGlowStates, SystemID.Vfx);
            }
        }

        /// <summary>
        /// Reads the live mock weather row used by the bioluminescence oscillator.
        /// </summary>
        public static bool TryReadEditorMockWeather(out MockWeatherSignal signal)
        {
            signal = default;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!vault.TryGetBufferHandle(BufferID.BiolumMockWeatherSignal, out VaultBufferHandle<MockWeatherSignal> handle) ||
                handle.Length <= 0)
            {
                return false;
            }

            if (!vault.TryLockBuffer(BufferID.BiolumMockWeatherSignal, SystemID.Vfx))
                return false;

            try
            {
                NativeArray<MockWeatherSignal> weather = handle.Resolve(vault);
                if (!weather.IsCreated || weather.Length <= 0)
                    return false;

                signal = weather[0];
                return true;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.BiolumMockWeatherSignal, SystemID.Vfx);
            }
        }

        /// <summary>
        /// Writes the live mock weather row used by the bioluminescence oscillator.
        /// </summary>
        public static bool TryWriteEditorMockWeather(in MockWeatherSignal signal)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!vault.TryGetBufferHandle(BufferID.BiolumMockWeatherSignal, out VaultBufferHandle<MockWeatherSignal> handle) ||
                handle.Length <= 0)
            {
                return false;
            }

            if (!vault.TryLockBuffer(BufferID.BiolumMockWeatherSignal, SystemID.Vfx))
                return false;

            try
            {
                NativeArray<MockWeatherSignal> weather = handle.Resolve(vault);
                if (!weather.IsCreated || weather.Length <= 0)
                    return false;

                weather[0] = signal;
                return true;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.BiolumMockWeatherSignal, SystemID.Vfx);
            }
        }

        /// <summary>
        /// Pushes a fixed-slot global pulse into DataVault for editor-triggered wave tests.
        /// </summary>
        public static bool TryTriggerEditorGlobalPulse(double3 originAUP, float waveSpeed, uint colorOverride)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!vault.TryGetBufferHandle(BufferID.BiolumSyncPulses, out VaultBufferHandle<SyncPulseDTO> pulseHandle) ||
                !vault.TryGetBufferHandle(BufferID.BiolumSyncPulseAges, out VaultBufferHandle<float> ageHandle))
            {
                return false;
            }

            bool lockedPulses = false;
            bool lockedAges = false;
            try
            {
                lockedPulses = vault.TryLockBuffer(BufferID.BiolumSyncPulses, SystemID.Vfx);
                if (!lockedPulses)
                    return false;

                lockedAges = vault.TryLockBuffer(BufferID.BiolumSyncPulseAges, SystemID.Vfx);
                if (!lockedAges)
                    return false;

                NativeArray<SyncPulseDTO> pulses = pulseHandle.Resolve(vault);
                NativeArray<float> ages = ageHandle.Resolve(vault);
                if (!pulses.IsCreated || !ages.IsCreated)
                    return false;

                int count = math.min(pulses.Length, ages.Length);
                if (count <= 0)
                    return false;

                int slot = 0;
                float oldestAge = -1f;
                for (int i = 0; i < count; i++)
                {
                    if (ages[i] > oldestAge)
                    {
                        oldestAge = ages[i];
                        slot = i;
                    }
                }

                pulses[slot] = new SyncPulseDTO
                {
                    OriginAUP = originAUP,
                    WaveSpeed = math.max(1f, waveSpeed),
                    ColorOverride = colorOverride
                };
                ages[slot] = 0f;
                return true;
            }
            finally
            {
                if (lockedAges)
                    vault.TryUnlockBuffer(BufferID.BiolumSyncPulseAges, SystemID.Vfx);
                if (lockedPulses)
                    vault.TryUnlockBuffer(BufferID.BiolumSyncPulses, SystemID.Vfx);
            }
        }

        /// <summary>
        /// Initializes a streamed glow range with an unmanaged template copy.
        /// </summary>
        public static unsafe bool TryMemCpyInitializeGlowRange(
            NativeArray<GlowStateDTO> states,
            int startIndex,
            int count,
            in GlowStateDTO template)
        {
            if (!states.IsCreated || startIndex < 0 || count <= 0 || startIndex >= states.Length)
                return false;

            int safeCount = math.min(count, states.Length - startIndex);
            if (safeCount <= 0)
                return false;

            GlowStateDTO localTemplate = template;
            byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(states) +
                                startIndex * UnsafeUtility.SizeOf<GlowStateDTO>();
            byte* source = (byte*)UnsafeUtility.AddressOf(ref localTemplate);
            int stride = UnsafeUtility.SizeOf<GlowStateDTO>();
            for (int i = 0; i < safeCount; i++)
                UnsafeUtility.MemCpy(destination + i * stride, source, stride);

            return true;
        }

#if UNITY_EDITOR
        [ContextMenu("Reload Biolum Profiles")]
        private void ReloadProfilesFromDiskEditor()
        {
            CompleteScheduledJob();
            RefreshCachedRegistryServices();
            EnsureVaultBuffers();
            _profilesLoaded = false;
            LoadProfilesFromDiskOrDefaults();
            EvaluateColdStartStates();
            UploadShaderGlobals(forceStateArray: true);
            _forceSchedule = true;
        }
#endif

        private void TryRegisterUpdate()
        {
            if (_registeredUpdate)
                return;
            if (_tickDispatcher == null)
                return;

            _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame)
                return;
            if (_tickDispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterUpdate()
        {
            if (!_registeredUpdate)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registeredUpdate = false;
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrame = false;
        }

        private void TryRegisterScalabilityEvents()
        {
            if (_registeredScalability)
                return;

            ScalabilityEvents.Register(this);
            _registeredScalability = true;
        }

        private void TryRegisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);

            RefreshCachedRegistryServices();
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        void IGlobalRegistryHotSwapRefListener.OnGlobalRegistryServiceRebound(
            GlobalRegistryServiceSlot serviceSlot,
            ref object currentService)
        {
            ApplyRegistryServiceRebind(serviceSlot, currentService);
        }

        void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            ApplyRegistryServiceRebind(serviceSlot, currentService);
        }

        private void RefreshCachedRegistryServices()
        {
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.Dispatcher, GlobalRegistry.TickDispatcher);
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.DataVault, GlobalRegistry.DataVault);
        }

        private void ApplyRegistryServiceRebind(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    ITickDispatcher tickDispatcher = currentService as ITickDispatcher;
                    if (!ReferenceEquals(_tickDispatcher, tickDispatcher))
                    {
                        TryUnregisterUpdate();
                        TryUnregisterLateFrame();
                        _tickDispatcher = tickDispatcher;
                    }

                    if (_tickDispatcher != null)
                    {
                        TryRegisterUpdate();
                        TryRegisterLateFrame();
                    }
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    BindDataVault(currentService as IDataVault);
                    break;
            }
        }

        private void RefreshQualityTier(HectonQualityTier tier)
        {
            _qualityTier = tier;
            _activeStateCount = ResolveStateCount(tier);
        }

        private void BindDataVault(IDataVault currentVault)
        {
            if (!ReferenceEquals(_dataVault, currentVault))
            {
                _dataVault = currentVault;
                ReleaseVaultHandlesOnly(invalidateProfiles: true);
            }
        }

        private bool EnsureVaultBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (_vaultGenerationId != 0u && _vaultGenerationId != vault.VaultGenerationID)
                ReleaseVaultHandlesOnly(invalidateProfiles: false);

            if (!_profileFloatsHandle.IsCreated ||
                _profileFloatsHandle.BufferId != BufferID.BiolumProfileFloats ||
                _profileFloatsHandle.Length < ProfileFloatCount)
            {
                if (vault.TryGetBufferHandle(BufferID.BiolumProfileFloats, out VaultBufferHandle<float> existingProfileHandle) &&
                    existingProfileHandle.Length >= ProfileFloatCount)
                {
                    _profileFloatsHandle = existingProfileHandle;
                }
                else
                {
                    _profilesLoaded = false;
                    _profileFloatsHandle = vault.GetBufferHandle<float>(
                        BufferID.BiolumProfileFloats,
                        ProfileFloatCount,
                        SystemID.Vfx,
                        NativeArrayOptions.ClearMemory);
                }
            }

            if (!_jobStatesHandle.IsCreated ||
                _jobStatesHandle.BufferId != BufferID.BiolumGlobalStates ||
                _jobStatesHandle.Length < MaxGlobalBiolumStates)
            {
                _jobStatesHandle = vault.GetBufferHandle<float4>(
                    BufferID.BiolumGlobalStates,
                    MaxGlobalBiolumStates,
                    SystemID.Vfx,
                    NativeArrayOptions.ClearMemory);
            }

            if (!_blackBoxHandle.IsCreated ||
                _blackBoxHandle.BufferId != BufferID.BiolumBlackBox ||
                _blackBoxHandle.Length < BlackBoxFrameCount)
            {
                _blackBoxHandle = vault.GetBufferHandle<BiolumPulseTelemetryEntry>(
                    BufferID.BiolumBlackBox,
                    BlackBoxFrameCount,
                    SystemID.Vfx,
                        NativeArrayOptions.ClearMemory);
            }

            if (!_glowStatesHandle.IsCreated ||
                _glowStatesHandle.BufferId != BufferID.BiolumGlowStates ||
                _glowStatesHandle.Length < MaxGlowInstances)
            {
                _glowStatesHandle = vault.GetBufferHandle<GlowStateDTO>(
                    BufferID.BiolumGlowStates,
                    MaxGlowInstances,
                    SystemID.Vfx,
                    NativeArrayOptions.UninitializedMemory);
                _mockGlowsInitialized = false;
            }

            if (!_gpuColorFrontHandle.IsCreated ||
                _gpuColorFrontHandle.BufferId != BufferID.BiolumGlowGpuColorFront ||
                _gpuColorFrontHandle.Length < MaxGlowInstances)
            {
                _gpuColorFrontHandle = vault.GetBufferHandle<uint>(
                    BufferID.BiolumGlowGpuColorFront,
                    MaxGlowInstances,
                    SystemID.Vfx,
                    NativeArrayOptions.UninitializedMemory);
                _gpuColorBufferUploaded = false;
                _publishedGpuColorCount = 0;
            }

            if (!_gpuColorBackHandle.IsCreated ||
                _gpuColorBackHandle.BufferId != BufferID.BiolumGlowGpuColorBack ||
                _gpuColorBackHandle.Length < MaxGlowInstances)
            {
                _gpuColorBackHandle = vault.GetBufferHandle<uint>(
                    BufferID.BiolumGlowGpuColorBack,
                    MaxGlowInstances,
                    SystemID.Vfx,
                    NativeArrayOptions.UninitializedMemory);
            }

            if (!_glowAupOriginsHandle.IsCreated ||
                _glowAupOriginsHandle.BufferId != BufferID.BiolumGlowAupOrigins ||
                _glowAupOriginsHandle.Length < MaxGlowInstances)
            {
                _glowAupOriginsHandle = vault.GetBufferHandle<double3>(
                    BufferID.BiolumGlowAupOrigins,
                    MaxGlowInstances,
                    SystemID.Vfx,
                    NativeArrayOptions.UninitializedMemory);
                _mockGlowsInitialized = false;
            }

            if (!_syncPulsesHandle.IsCreated ||
                _syncPulsesHandle.BufferId != BufferID.BiolumSyncPulses ||
                _syncPulsesHandle.Length < SyncPulseCapacity)
            {
                _syncPulsesHandle = vault.GetBufferHandle<SyncPulseDTO>(
                    BufferID.BiolumSyncPulses,
                    SyncPulseCapacity,
                    SystemID.Vfx,
                    NativeArrayOptions.ClearMemory);
            }

            if (!_syncPulseAgesHandle.IsCreated ||
                _syncPulseAgesHandle.BufferId != BufferID.BiolumSyncPulseAges ||
                _syncPulseAgesHandle.Length < SyncPulseCapacity)
            {
                _syncPulseAgesHandle = vault.GetBufferHandle<float>(
                    BufferID.BiolumSyncPulseAges,
                    SyncPulseCapacity,
                    SystemID.Vfx,
                    NativeArrayOptions.ClearMemory);
            }

            if (!_mockWeatherSignalHandle.IsCreated ||
                _mockWeatherSignalHandle.BufferId != BufferID.BiolumMockWeatherSignal ||
                _mockWeatherSignalHandle.Length < 1)
            {
                _mockWeatherSignalHandle = vault.GetBufferHandle<MockWeatherSignal>(
                    BufferID.BiolumMockWeatherSignal,
                    1,
                    SystemID.Vfx,
                    NativeArrayOptions.ClearMemory);
            }

            if (!_mockPredatorSignalHandle.IsCreated ||
                _mockPredatorSignalHandle.BufferId != BufferID.BiolumMockPredatorSignal ||
                _mockPredatorSignalHandle.Length < 1)
            {
                _mockPredatorSignalHandle = vault.GetBufferHandle<MockPredatorProximitySignal>(
                    BufferID.BiolumMockPredatorSignal,
                    1,
                    SystemID.Vfx,
                    NativeArrayOptions.ClearMemory);
            }

            if (!_mockDamageSignalHandle.IsCreated ||
                _mockDamageSignalHandle.BufferId != BufferID.BiolumMockDamageSignal ||
                _mockDamageSignalHandle.Length < 1)
            {
                _mockDamageSignalHandle = vault.GetBufferHandle<MockCombatDamageSignal>(
                    BufferID.BiolumMockDamageSignal,
                    1,
                    SystemID.Vfx,
                    NativeArrayOptions.ClearMemory);
            }

            if (!_speciesTuningHandle.IsCreated ||
                _speciesTuningHandle.BufferId != BufferID.BiolumSpeciesTuning ||
                _speciesTuningHandle.Length < MaxSpeciesTuningCount)
            {
                _speciesTuningHandle = vault.GetBufferHandle<BiolumSpeciesTuningDTO>(
                    BufferID.BiolumSpeciesTuning,
                    MaxSpeciesTuningCount,
                    SystemID.Vfx,
                    NativeArrayOptions.UninitializedMemory);
                _mockGlowsInitialized = false;
            }

            if (!_csvScratchHandle.IsCreated ||
                _csvScratchHandle.BufferId != BufferID.BiolumCsvScratch ||
                _csvScratchHandle.Length < CsvScratchByteCount)
            {
                _csvScratchHandle = vault.GetBufferHandle<byte>(
                    BufferID.BiolumCsvScratch,
                    CsvScratchByteCount,
                    SystemID.Vfx,
                    NativeArrayOptions.ClearMemory);
            }

            bool ready = _profileFloatsHandle.IsCreated &&
                         _jobStatesHandle.IsCreated &&
                         _blackBoxHandle.IsCreated &&
                         _glowStatesHandle.IsCreated &&
                         _gpuColorFrontHandle.IsCreated &&
                         _gpuColorBackHandle.IsCreated &&
                         _glowAupOriginsHandle.IsCreated &&
                         _syncPulsesHandle.IsCreated &&
                         _syncPulseAgesHandle.IsCreated &&
                         _mockWeatherSignalHandle.IsCreated &&
                         _mockPredatorSignalHandle.IsCreated &&
                         _mockDamageSignalHandle.IsCreated &&
                         _speciesTuningHandle.IsCreated &&
                         _csvScratchHandle.IsCreated;
            if (ready)
                _vaultGenerationId = vault.VaultGenerationID;

            return ready;
        }

        private bool HasVaultBuffers()
        {
            IDataVault vault = _dataVault;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   _vaultGenerationId != 0u &&
                   _vaultGenerationId == vault.VaultGenerationID &&
                   _profileFloatsHandle.IsCreated &&
                   _profileFloatsHandle.BufferId == BufferID.BiolumProfileFloats &&
                   _profileFloatsHandle.Length >= ProfileFloatCount &&
                   _jobStatesHandle.IsCreated &&
                   _jobStatesHandle.BufferId == BufferID.BiolumGlobalStates &&
                   _jobStatesHandle.Length >= MaxGlobalBiolumStates &&
                   _blackBoxHandle.IsCreated &&
                   _blackBoxHandle.BufferId == BufferID.BiolumBlackBox &&
                   _blackBoxHandle.Length >= BlackBoxFrameCount &&
                   _glowStatesHandle.IsCreated &&
                   _glowStatesHandle.BufferId == BufferID.BiolumGlowStates &&
                   _glowStatesHandle.Length >= MaxGlowInstances &&
                   _gpuColorFrontHandle.IsCreated &&
                   _gpuColorFrontHandle.BufferId == BufferID.BiolumGlowGpuColorFront &&
                   _gpuColorFrontHandle.Length >= MaxGlowInstances &&
                   _gpuColorBackHandle.IsCreated &&
                   _gpuColorBackHandle.BufferId == BufferID.BiolumGlowGpuColorBack &&
                   _gpuColorBackHandle.Length >= MaxGlowInstances &&
                   _glowAupOriginsHandle.IsCreated &&
                   _glowAupOriginsHandle.BufferId == BufferID.BiolumGlowAupOrigins &&
                   _glowAupOriginsHandle.Length >= MaxGlowInstances &&
                   _syncPulsesHandle.IsCreated &&
                   _syncPulsesHandle.BufferId == BufferID.BiolumSyncPulses &&
                   _syncPulsesHandle.Length >= SyncPulseCapacity &&
                   _syncPulseAgesHandle.IsCreated &&
                   _syncPulseAgesHandle.BufferId == BufferID.BiolumSyncPulseAges &&
                   _syncPulseAgesHandle.Length >= SyncPulseCapacity &&
                   _mockWeatherSignalHandle.IsCreated &&
                   _mockWeatherSignalHandle.BufferId == BufferID.BiolumMockWeatherSignal &&
                   _mockWeatherSignalHandle.Length >= 1 &&
                   _mockPredatorSignalHandle.IsCreated &&
                   _mockPredatorSignalHandle.BufferId == BufferID.BiolumMockPredatorSignal &&
                   _mockPredatorSignalHandle.Length >= 1 &&
                   _mockDamageSignalHandle.IsCreated &&
                   _mockDamageSignalHandle.BufferId == BufferID.BiolumMockDamageSignal &&
                   _mockDamageSignalHandle.Length >= 1 &&
                   _speciesTuningHandle.IsCreated &&
                   _speciesTuningHandle.BufferId == BufferID.BiolumSpeciesTuning &&
                   _speciesTuningHandle.Length >= MaxSpeciesTuningCount &&
                   _csvScratchHandle.IsCreated &&
                   _csvScratchHandle.BufferId == BufferID.BiolumCsvScratch &&
                   _csvScratchHandle.Length >= CsvScratchByteCount;
        }

        private bool TryRefreshExistingVaultHandlesNoAllocate()
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!vault.TryGetBufferHandle(BufferID.BiolumProfileFloats, out VaultBufferHandle<float> profileHandle) ||
                profileHandle.Length < ProfileFloatCount)
            {
                return false;
            }

            if (!vault.TryGetBufferHandle(BufferID.BiolumGlobalStates, out VaultBufferHandle<float4> statesHandle) ||
                statesHandle.Length < MaxGlobalBiolumStates)
            {
                return false;
            }

            if (!vault.TryGetBufferHandle(BufferID.BiolumBlackBox, out VaultBufferHandle<BiolumPulseTelemetryEntry> blackBoxHandle) ||
                blackBoxHandle.Length < BlackBoxFrameCount)
            {
                return false;
            }

            if (!vault.TryGetBufferHandle(BufferID.BiolumGlowStates, out VaultBufferHandle<GlowStateDTO> glowStatesHandle) ||
                glowStatesHandle.Length < MaxGlowInstances)
            {
                return false;
            }

            if (!vault.TryGetBufferHandle(BufferID.BiolumGlowGpuColorFront, out VaultBufferHandle<uint> gpuColorFrontHandle) ||
                gpuColorFrontHandle.Length < MaxGlowInstances)
            {
                return false;
            }

            if (!vault.TryGetBufferHandle(BufferID.BiolumGlowGpuColorBack, out VaultBufferHandle<uint> gpuColorBackHandle) ||
                gpuColorBackHandle.Length < MaxGlowInstances)
            {
                return false;
            }

            if (!vault.TryGetBufferHandle(BufferID.BiolumGlowAupOrigins, out VaultBufferHandle<double3> glowAupOriginsHandle) ||
                glowAupOriginsHandle.Length < MaxGlowInstances)
            {
                return false;
            }

            if (!vault.TryGetBufferHandle(BufferID.BiolumSyncPulses, out VaultBufferHandle<SyncPulseDTO> syncPulsesHandle) ||
                syncPulsesHandle.Length < SyncPulseCapacity)
            {
                return false;
            }

            if (!vault.TryGetBufferHandle(BufferID.BiolumSyncPulseAges, out VaultBufferHandle<float> syncPulseAgesHandle) ||
                syncPulseAgesHandle.Length < SyncPulseCapacity)
            {
                return false;
            }

            if (!vault.TryGetBufferHandle(BufferID.BiolumMockWeatherSignal, out VaultBufferHandle<MockWeatherSignal> mockWeatherSignalHandle) ||
                mockWeatherSignalHandle.Length < 1)
            {
                return false;
            }

            if (!vault.TryGetBufferHandle(BufferID.BiolumMockPredatorSignal, out VaultBufferHandle<MockPredatorProximitySignal> mockPredatorSignalHandle) ||
                mockPredatorSignalHandle.Length < 1)
            {
                return false;
            }

            if (!vault.TryGetBufferHandle(BufferID.BiolumMockDamageSignal, out VaultBufferHandle<MockCombatDamageSignal> mockDamageSignalHandle) ||
                mockDamageSignalHandle.Length < 1)
            {
                return false;
            }

            if (!vault.TryGetBufferHandle(BufferID.BiolumSpeciesTuning, out VaultBufferHandle<BiolumSpeciesTuningDTO> speciesTuningHandle) ||
                speciesTuningHandle.Length < MaxSpeciesTuningCount)
            {
                return false;
            }

            if (!vault.TryGetBufferHandle(BufferID.BiolumCsvScratch, out VaultBufferHandle<byte> csvScratchHandle) ||
                csvScratchHandle.Length < CsvScratchByteCount)
            {
                return false;
            }

            _profileFloatsHandle = profileHandle;
            _jobStatesHandle = statesHandle;
            _blackBoxHandle = blackBoxHandle;
            _glowStatesHandle = glowStatesHandle;
            _gpuColorFrontHandle = gpuColorFrontHandle;
            _gpuColorBackHandle = gpuColorBackHandle;
            _glowAupOriginsHandle = glowAupOriginsHandle;
            _syncPulsesHandle = syncPulsesHandle;
            _syncPulseAgesHandle = syncPulseAgesHandle;
            _mockWeatherSignalHandle = mockWeatherSignalHandle;
            _mockPredatorSignalHandle = mockPredatorSignalHandle;
            _mockDamageSignalHandle = mockDamageSignalHandle;
            _speciesTuningHandle = speciesTuningHandle;
            _csvScratchHandle = csvScratchHandle;
            _vaultGenerationId = vault.VaultGenerationID;
            return true;
        }

        private void ReleaseVaultHandlesOnly(bool invalidateProfiles)
        {
            _profileFloatsHandle = default;
            _jobStatesHandle = default;
            _blackBoxHandle = default;
            _glowStatesHandle = default;
            _gpuColorFrontHandle = default;
            _gpuColorBackHandle = default;
            _glowAupOriginsHandle = default;
            _syncPulsesHandle = default;
            _syncPulseAgesHandle = default;
            _mockWeatherSignalHandle = default;
            _mockPredatorSignalHandle = default;
            _mockDamageSignalHandle = default;
            _speciesTuningHandle = default;
            _csvScratchHandle = default;
            _vaultGenerationId = 0u;
            _mockGlowsInitialized = false;
            _gpuColorBufferUploaded = false;
            _publishedGpuColorCount = 0;
            if (invalidateProfiles)
                _profilesLoaded = false;
        }

        private static bool AreSyncLayoutsValid()
        {
            if (UnsafeUtility.SizeOf<GlowStateDTO>() != 16)
                return ReportInvalidSyncLayout("GlowStateDTO must remain 16 bytes.");

            if (UnsafeUtility.SizeOf<SyncPulseDTO>() != 32)
                return ReportInvalidSyncLayout("SyncPulseDTO must remain 32 bytes.");

            if (UnsafeUtility.SizeOf<MockWeatherSignal>() != 16)
                return ReportInvalidSyncLayout("MockWeatherSignal must remain 16 bytes.");

            if (UnsafeUtility.SizeOf<BiolumSpeciesTuningDTO>() != 24)
                return ReportInvalidSyncLayout("BiolumSpeciesTuningDTO must remain 24 bytes.");

            if (UnsafeUtility.SizeOf<MockPredatorProximitySignal>() != 40)
                return ReportInvalidSyncLayout("MockPredatorProximitySignal must remain 40 bytes.");

            if (UnsafeUtility.SizeOf<MockCombatDamageSignal>() != 40)
                return ReportInvalidSyncLayout("MockCombatDamageSignal must remain 40 bytes.");

            if (UnsafeUtility.SizeOf<BiolumPulseTelemetryEntry>() != 32)
                return ReportInvalidSyncLayout("BiolumPulseTelemetryEntry must remain 32 bytes.");

            if (UnsafeUtility.SizeOf<BiolumPulseDumpHeader>() != 16)
                return ReportInvalidSyncLayout("BiolumPulseDumpHeader must remain 16 bytes.");

            return true;
        }

        private static bool ReportInvalidSyncLayout(string message)
        {
            Debug.LogError(message);
            return false;
        }

        private bool EnsureGpuColorBuffer()
        {
            if (IsGpuColorBufferValid(_gpuColorBufferA) && IsGpuColorBufferValid(_gpuColorBufferB))
            {
                Shader.SetGlobalBuffer(_BiolumGpuColorBufferId, ResolveGpuReadBuffer());
                return true;
            }

            ReleaseGpuColorBuffer();
            _gpuColorBufferA = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                MaxGlowInstances,
                UnsafeUtility.SizeOf<uint>());
            _gpuColorBufferB = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                MaxGlowInstances,
                UnsafeUtility.SizeOf<uint>());
            _gpuColorFrontIndex = 0;
            Shader.SetGlobalBuffer(_BiolumGpuColorBufferId, _gpuColorBufferA);
            _gpuColorBufferUploaded = false;
            _publishedGpuColorCount = 0;
            return true;
        }

        private void ReleaseGpuColorBuffer()
        {
            if (_gpuColorBufferA != null)
            {
                _gpuColorBufferA.Release();
                _gpuColorBufferA = null;
            }

            if (_gpuColorBufferB != null)
            {
                _gpuColorBufferB.Release();
                _gpuColorBufferB = null;
            }

            _gpuColorFrontIndex = 0;
            _gpuColorBufferUploaded = false;
            _publishedGpuColorCount = 0;
        }

        private static bool IsGpuColorBufferValid(GraphicsBuffer buffer)
        {
            return buffer != null &&
                   buffer.count >= MaxGlowInstances &&
                   buffer.stride == UnsafeUtility.SizeOf<uint>();
        }

        private GraphicsBuffer ResolveGpuReadBuffer()
        {
            return _gpuColorFrontIndex == 0 ? _gpuColorBufferA : _gpuColorBufferB;
        }

        private GraphicsBuffer ResolveGpuWriteBuffer()
        {
            return _gpuColorFrontIndex == 0 ? _gpuColorBufferB : _gpuColorBufferA;
        }

        private unsafe void GenerateEmergencyMockGlows()
        {
            IDataVault vault = _dataVault;
            if (vault == null || !HasVaultBuffers())
                return;

            bool lockedGlowStates = false;
            bool lockedGpuFront = false;
            bool lockedGpuBack = false;
            bool lockedAup = false;
            bool lockedSpecies = false;
            bool lockedWeather = false;
            bool lockedPredator = false;
            bool lockedDamage = false;
            bool lockedPulses = false;
            bool lockedPulseAges = false;

            try
            {
                lockedGlowStates = vault.TryLockBuffer(BufferID.BiolumGlowStates, SystemID.Vfx);
                if (!lockedGlowStates)
                    return;

                lockedGpuFront = vault.TryLockBuffer(BufferID.BiolumGlowGpuColorFront, SystemID.Vfx);
                if (!lockedGpuFront)
                    return;

                lockedGpuBack = vault.TryLockBuffer(BufferID.BiolumGlowGpuColorBack, SystemID.Vfx);
                if (!lockedGpuBack)
                    return;

                lockedAup = vault.TryLockBuffer(BufferID.BiolumGlowAupOrigins, SystemID.Vfx);
                if (!lockedAup)
                    return;

                lockedSpecies = vault.TryLockBuffer(BufferID.BiolumSpeciesTuning, SystemID.Vfx);
                if (!lockedSpecies)
                    return;

                lockedWeather = vault.TryLockBuffer(BufferID.BiolumMockWeatherSignal, SystemID.Vfx);
                if (!lockedWeather)
                    return;

                lockedPredator = vault.TryLockBuffer(BufferID.BiolumMockPredatorSignal, SystemID.Vfx);
                if (!lockedPredator)
                    return;

                lockedDamage = vault.TryLockBuffer(BufferID.BiolumMockDamageSignal, SystemID.Vfx);
                if (!lockedDamage)
                    return;

                lockedPulses = vault.TryLockBuffer(BufferID.BiolumSyncPulses, SystemID.Vfx);
                if (!lockedPulses)
                    return;

                lockedPulseAges = vault.TryLockBuffer(BufferID.BiolumSyncPulseAges, SystemID.Vfx);
                if (!lockedPulseAges)
                    return;

                NativeArray<GlowStateDTO> glowStates = _glowStatesHandle.Resolve(vault);
                NativeArray<uint> gpuFront = _gpuColorFrontHandle.Resolve(vault);
                NativeArray<uint> gpuBack = _gpuColorBackHandle.Resolve(vault);
                NativeArray<double3> aupOrigins = _glowAupOriginsHandle.Resolve(vault);
                NativeArray<BiolumSpeciesTuningDTO> speciesTuning = _speciesTuningHandle.Resolve(vault);
                NativeArray<MockWeatherSignal> weatherSignal = _mockWeatherSignalHandle.Resolve(vault);
                NativeArray<MockPredatorProximitySignal> predatorSignal = _mockPredatorSignalHandle.Resolve(vault);
                NativeArray<MockCombatDamageSignal> damageSignal = _mockDamageSignalHandle.Resolve(vault);
                NativeArray<SyncPulseDTO> pulses = _syncPulsesHandle.Resolve(vault);
                NativeArray<float> pulseAges = _syncPulseAgesHandle.Resolve(vault);

                if (!glowStates.IsCreated ||
                    !gpuFront.IsCreated ||
                    !gpuBack.IsCreated ||
                    !aupOrigins.IsCreated ||
                    !speciesTuning.IsCreated ||
                    !weatherSignal.IsCreated ||
                    !predatorSignal.IsCreated ||
                    !damageSignal.IsCreated ||
                    !pulses.IsCreated ||
                    !pulseAges.IsCreated)
                {
                    return;
                }

                SeedSpeciesTuning(speciesTuning);

                weatherSignal[0] = new MockWeatherSignal
                {
                    AmbientLightLevel = 0.08f,
                    O2Level01 = 1f,
                    SystemHealthIndex01 = 0.25f,
                    CurrentBiomeHash = HashAscii32("ABYSS_NEON")
                };
                predatorSignal[0] = default;
                damageSignal[0] = default;
                for (int i = 0; i < SyncPulseCapacity; i++)
                {
                    pulses[i] = default;
                    pulseAges[i] = 99f;
                }

                int speciesCount = math.min(MaxSpeciesTuningCount, speciesTuning.Length);
                int activeCount = math.min(MaxGlowInstances, glowStates.Length);
                for (int i = 0; i < activeCount; i++)
                {
                    int speciesIndex = i % speciesCount;
                    BiolumSpeciesTuningDTO species = speciesTuning[speciesIndex];
                    float phase = math.frac((i * 0.754877666f) + (speciesIndex * 0.037f));
                    ref GlowStateDTO state = ref GetGlowStateRef(glowStates, i);
                    state.PackedColor = species.PackedColor;
                    state.Phase = phase;
                    state.Frequency = species.Frequency;
                    state.SpeciesHash = species.SpeciesHash;
                    gpuFront[i] = state.PackedColor;
                    gpuBack[i] = state.PackedColor;

                    int x = i % 250;
                    int z = i / 250;
                    double jitterX = ((int)(DeterministicHash((uint)i) & 1023u) - 512) * 0.00625;
                    double jitterZ = ((int)((DeterministicHash((uint)i ^ 0xA341316Cu) >> 10) & 1023u) - 512) * 0.00625;
                    aupOrigins[i] = new double3((x - 125) * 1.35 + jitterX, -220.0 - (speciesIndex & 7) * 0.75, (z - 100) * 1.35 + jitterZ);
                }

                _activeGlowInstanceCount = activeCount;
                _activeSyncPulseCount = 0;
                _mockGlowsInitialized = true;
                _gpuColorBufferUploaded = false;
                _publishedGpuColorCount = 0;
            }
            finally
            {
                if (lockedPulseAges)
                    vault.TryUnlockBuffer(BufferID.BiolumSyncPulseAges, SystemID.Vfx);
                if (lockedPulses)
                    vault.TryUnlockBuffer(BufferID.BiolumSyncPulses, SystemID.Vfx);
                if (lockedDamage)
                    vault.TryUnlockBuffer(BufferID.BiolumMockDamageSignal, SystemID.Vfx);
                if (lockedPredator)
                    vault.TryUnlockBuffer(BufferID.BiolumMockPredatorSignal, SystemID.Vfx);
                if (lockedWeather)
                    vault.TryUnlockBuffer(BufferID.BiolumMockWeatherSignal, SystemID.Vfx);
                if (lockedSpecies)
                    vault.TryUnlockBuffer(BufferID.BiolumSpeciesTuning, SystemID.Vfx);
                if (lockedAup)
                    vault.TryUnlockBuffer(BufferID.BiolumGlowAupOrigins, SystemID.Vfx);
                if (lockedGpuBack)
                    vault.TryUnlockBuffer(BufferID.BiolumGlowGpuColorBack, SystemID.Vfx);
                if (lockedGpuFront)
                    vault.TryUnlockBuffer(BufferID.BiolumGlowGpuColorFront, SystemID.Vfx);
                if (lockedGlowStates)
                    vault.TryUnlockBuffer(BufferID.BiolumGlowStates, SystemID.Vfx);
            }
        }

        private static unsafe ref GlowStateDTO GetGlowStateRef(NativeArray<GlowStateDTO> states, int index)
        {
            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(states);
            return ref UnsafeUtility.AsRef<GlowStateDTO>(basePtr + index * UnsafeUtility.SizeOf<GlowStateDTO>());
        }

        private static void ApplySpeciesTuningToGlowStates(NativeArray<GlowStateDTO> glowStates, in BiolumSpeciesTuningDTO tuning)
        {
            if (!glowStates.IsCreated)
                return;

            for (int i = 0; i < glowStates.Length; i++)
            {
                GlowStateDTO glow = glowStates[i];
                if (glow.SpeciesHash != tuning.SpeciesHash)
                    continue;

                glow.PackedColor = tuning.PackedColor;
                glow.Frequency = tuning.Frequency;
                glowStates[i] = glow;
            }
        }

        private static void SeedSpeciesTuning(NativeArray<BiolumSpeciesTuningDTO> speciesTuning)
        {
            int count = math.min(MaxSpeciesTuningCount, speciesTuning.Length);
            uint cyan = BiolumPackedColorUtility.PackRgb10A2(new float3(0.05f, 0.72f, 1f), 1f);
            uint green = BiolumPackedColorUtility.PackRgb10A2(new float3(0.10f, 1f, 0.62f), 1f);
            uint violet = BiolumPackedColorUtility.PackRgb10A2(new float3(0.72f, 0.28f, 1f), 1f);
            uint amber = BiolumPackedColorUtility.PackRgb10A2(new float3(1f, 0.66f, 0.18f), 1f);

            for (int i = 0; i < count; i++)
            {
                uint groupColor = (i & 3) == 0 ? cyan : (i & 3) == 1 ? green : (i & 3) == 2 ? violet : amber;
                uint shifted = BiolumPackedColorUtility.LerpPackedColor(groupColor, cyan, math.frac(i * 0.091f) * 0.2f);
                speciesTuning[i] = new BiolumSpeciesTuningDTO
                {
                    SpeciesHash = HashAscii32("CORAL_SYNC") ^ (uint)(0x9E3779B9u * (i + 1)),
                    PackedColor = shifted,
                    Frequency = 0.28f + math.frac(i * 0.173f) * 0.82f,
                    WaveSpeed = 24f + math.frac(i * 0.211f) * 72f,
                    BiomeBlend01 = math.frac(i * 0.137f)
                };
            }
        }

        private static uint HashAscii32(string text)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619u;
            }

            return hash;
        }

        private static uint DeterministicHash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }

        private static Unity.Mathematics.Random CreateDeterministicRandom(uint sectorHash, uint frameCounter, uint salt)
        {
            uint seed = DeterministicHash(sectorHash ^ (frameCounter * 0x9E3779B9u) ^ salt);
            if (seed == 0u)
                seed = 1u;

            Unity.Mathematics.Random rng = default;
            rng.InitState(seed);
            return rng;
        }

        private void LoadProfilesFromDiskOrDefaults()
        {
            if (!TryLockProfileBuffer(out IDataVault vault, out NativeArray<float> profileFloats, allowEnsure: true))
                return;

            try
            {
                SeedDefaultProfiles(profileFloats);
                _profileSourceHash = ProfileFallbackHash;

                string path = ResolveProfilePath();
                if (string.IsNullOrEmpty(path))
                {
                    _profilesLoaded = true;
                    return;
                }

                try
                {
                    Span<byte> profileBytes = stackalloc byte[ProfileByteCount];
                    int totalBytesRead = 0;
                    using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, ProfileByteCount, FileOptions.SequentialScan))
                    {
                        while (totalBytesRead < ProfileByteCount)
                        {
                            int bytesRead = stream.Read(profileBytes.Slice(totalBytesRead));
                            if (bytesRead <= 0)
                                break;

                            totalBytesRead += bytesRead;
                        }
                    }

                    int readableFloats = math.min(ProfileFloatCount, totalBytesRead >> 2);
                    for (int i = 0; i < readableFloats; i++)
                        profileFloats[i] = SanitizeProfileFloat(ReadFloatLittleEndian(profileBytes, i << 2), i);

                    _profileSourceHash = ProfileBinaryHash;
                }
                catch (Exception)
                {
                    SeedDefaultProfiles(profileFloats);
                    _profileSourceHash = ProfileFallbackHash;
                }

                _profilesLoaded = true;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.BiolumProfileFloats, SystemID.Vfx);
            }
        }

        private static float ReadFloatLittleEndian(ReadOnlySpan<byte> bytes, int offset)
        {
            uint raw =
                bytes[offset] |
                ((uint)bytes[offset + 1] << 8) |
                ((uint)bytes[offset + 2] << 16) |
                ((uint)bytes[offset + 3] << 24);
            return math.asfloat(raw);
        }

        private static string ResolveProfilePath()
        {
            if (TryResolveProfilePath(Application.streamingAssetsPath, out string profilePath))
                return profilePath;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (TryResolveProfilePath(Path.Combine(projectRoot, "Data", "Visuals"), out profilePath))
                return profilePath;

            if (TryResolveProfilePath(Path.Combine(projectRoot, "Docs", "Generated"), out profilePath))
                return profilePath;

            return TryResolveProfilePath(Path.Combine(projectRoot, "Docs"), out profilePath) ? profilePath : null;
#else
            return null;
#endif
        }

        private static bool TryResolveProfilePath(string directory, out string path)
        {
            path = Path.Combine(directory, ProfileFileName);
            if (File.Exists(path))
                return true;

            path = Path.Combine(directory, ProfileH8BinFileName);
            if (File.Exists(path))
                return true;

            path = Path.Combine(directory, LegacyPaletteArchiveName);
            if (File.Exists(path))
                return true;

            path = Path.Combine(directory, LegacyPulseArchiveName);
            if (File.Exists(path))
                return true;

            path = null;
            return false;
        }

        private bool TryLockProfileBuffer(out IDataVault vault, out NativeArray<float> profileFloats, bool allowEnsure = false)
        {
            profileFloats = default;
            vault = _dataVault;
            bool ready = allowEnsure ? EnsureVaultBuffers() : HasVaultBuffers();
            if (vault == null || !ready)
                return false;

            if (!vault.TryLockBuffer(BufferID.BiolumProfileFloats, SystemID.Vfx))
                return false;

            profileFloats = _profileFloatsHandle.Resolve(vault);
            if (profileFloats.IsCreated && profileFloats.Length >= ProfileFloatCount)
                return true;

            vault.TryUnlockBuffer(BufferID.BiolumProfileFloats, SystemID.Vfx);
            profileFloats = default;
            return false;
        }

        private bool TryLockBlackBoxBuffer(out IDataVault vault, out NativeArray<BiolumPulseTelemetryEntry> blackBox)
        {
            blackBox = default;
            vault = _dataVault;
            if (vault == null || !HasVaultBuffers())
                return false;

            if (!vault.TryLockBuffer(BufferID.BiolumBlackBox, SystemID.Vfx))
                return false;

            blackBox = _blackBoxHandle.Resolve(vault);
            if (blackBox.IsCreated && blackBox.Length >= BlackBoxFrameCount)
                return true;

            vault.TryUnlockBuffer(BufferID.BiolumBlackBox, SystemID.Vfx);
            blackBox = default;
            return false;
        }

        private void SeedDefaultProfiles(NativeArray<float> profileFloats)
        {
            for (int i = 0; i < MaxGlobalBiolumStates; i++)
            {
                int offset = i * ProfileFloatStride;
                float lane = i;
                profileFloats[offset] = math.frac(lane * 0.61803398875f);
                profileFloats[offset + 1] = 0.045f + lane * 0.0045f;
                profileFloats[offset + 2] = 0.42f + math.frac(lane * 0.37f) * 0.48f;
                profileFloats[offset + 3] = 0.18f + math.frac(lane * 0.23f) * 0.48f;
                profileFloats[offset + 4] = 0.12f + math.frac(lane * 0.19f) * 0.22f;
                profileFloats[offset + 5] = 0.22f + math.frac(lane * 0.31f) * 0.18f;
                profileFloats[offset + 6] = 0.76f + math.frac(lane * 0.17f) * 0.24f;
                profileFloats[offset + 7] = 0.86f + math.frac(lane * 0.11f) * 0.14f;
            }
        }

        private static float SanitizeProfileFloat(float value, int profileFloatIndex)
        {
            if (!math.isfinite(value))
                return 0f;

            int lane = profileFloatIndex % ProfileFloatStride;
            switch (lane)
            {
                case 1:
                    return math.clamp(value, 0.0025f, 1.5f);
                case 2:
                case 4:
                    return math.clamp(value, 0f, MaxHdrIntensity);
                case 3:
                case 5:
                case 6:
                case 7:
                    return math.saturate(value);
                default:
                    return math.frac(value);
            }
        }

        private void AdvanceTime(float dt)
        {
            ITickDispatcher dispatcher = _tickDispatcher;
            if (dispatcher != null)
            {
                H8TimeSnapshot snapshot = dispatcher.TimeSnapshot;
                if (snapshot.Time >= 0d && !double.IsNaN(snapshot.Time) && !double.IsInfinity(snapshot.Time))
                {
                    _localTimeSeconds = snapshot.Time;
                    return;
                }
            }

            _localTimeSeconds += dt;
        }

        private void ConsumeAupShiftSignals()
        {
            ReadOnlySpan<AupShiftSignal> shifts = SignalBus<AupShiftSignal>.GetFrameSnapshot();
            for (int i = 0; i < shifts.Length; i++)
            {
                float3 delta = shifts[i].ShiftMeters;
                if (math.all(math.isfinite(delta)))
                    _aupOriginOffset += delta;
            }

            if (!math.all(math.isfinite(_aupOriginOffset)))
            {
                _aupOriginOffset = float3.zero;
                _pendingTelemetryFlags |= TelemetryFlagAupInvalid;
                DumpBlackBox(TelemetryFlagAupInvalid);
            }
        }

        private void ConsumeGlobalSignalMirrors()
        {
            float ambientLight01 = -1f;
            float oxygen01 = -1f;
            bool weatherDirty = false;

            if (GlobalSignals.TryGetLatestLightLevelSignal(out LightLevelSignal light, out int lightSequence) &&
                lightSequence != _lastGlobalLightLevelSignalSequence)
            {
                _lastGlobalLightLevelSignalSequence = lightSequence;
                ambientLight01 = math.saturate(light.LightLevel01);
                weatherDirty = true;
            }

            if (GlobalSignals.TryGetLatestSurvivalDeathSignal(out SurvivalVitalsChangedSignal vitals, out int vitalsSequence) &&
                vitalsSequence != _lastGlobalSurvivalVitalsSequence)
            {
                _lastGlobalSurvivalVitalsSequence = vitalsSequence;
                oxygen01 = math.saturate(vitals.Oxygen01);
                weatherDirty = true;
            }

            if (weatherDirty)
                MirrorGlobalWeatherSignalsToVault(ambientLight01, oxygen01);

            if (GlobalSignals.TryGetLatestDamageSignal(out CombatDamageSignal damage, out int damageSequence) &&
                damageSequence != _lastGlobalDamageSignalSequence)
            {
                _lastGlobalDamageSignalSequence = damageSequence;
                MirrorGlobalDamageSignalToVault(in damage);
            }
        }

        private void MirrorGlobalWeatherSignalsToVault(float ambientLight01, float oxygen01)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !HasVaultBuffers())
                return;

            if (!vault.TryLockBuffer(BufferID.BiolumMockWeatherSignal, SystemID.Vfx))
                return;

            try
            {
                NativeArray<MockWeatherSignal> weatherSignal = _mockWeatherSignalHandle.Resolve(vault);
                if (!weatherSignal.IsCreated || weatherSignal.Length <= 0)
                    return;

                MockWeatherSignal weather = weatherSignal[0];
                if (ambientLight01 >= 0f)
                    weather.AmbientLightLevel = ambientLight01;
                if (oxygen01 >= 0f)
                    weather.O2Level01 = oxygen01;

                weatherSignal[0] = weather;
                _forceSchedule = true;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.BiolumMockWeatherSignal, SystemID.Vfx);
            }
        }

        private void MirrorGlobalDamageSignalToVault(in CombatDamageSignal signal)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !HasVaultBuffers())
                return;

            if (!vault.TryLockBuffer(BufferID.BiolumMockDamageSignal, SystemID.Vfx))
                return;

            try
            {
                NativeArray<MockCombatDamageSignal> damageSignal = _mockDamageSignalHandle.Resolve(vault);
                if (!damageSignal.IsCreated || damageSignal.Length <= 0)
                    return;

                float magnitude = math.max(signal.Magnitude, 0f);
                float radius = math.clamp(4f + math.sqrt(math.max(magnitude, 0.0001f)) * 2.75f, 4f, 48f);
                damageSignal[0] = new MockCombatDamageSignal
                {
                    OriginAUP = signal.ImpactAup,
                    RadiusMeters = radius,
                    AgeSeconds = 0f,
                    PackedDamageColor = BiolumPackedColorUtility.PackRgb10A2(new float3(1f, 0.075f, 0.025f), 1f),
                    FrameStamp = signal.Frame
                };

                _forceSchedule = true;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.BiolumMockDamageSignal, SystemID.Vfx);
            }
        }

        private void ConsumeFrameTimeSignals(float dt)
        {
            ReadOnlySpan<FrameTimeSignal> frameSignals = SignalBus<FrameTimeSignal>.GetFrameSnapshot();
            for (int i = 0; i < frameSignals.Length; i++)
            {
                FrameTimeSignal signal = frameSignals[i];
                float targetMs = math.max(signal.TargetFrameTimeMs, 0.001f);
                float currentMs = math.max(signal.CurrentFrameTimeMs, signal.FrameTimeEwmaMs);
                bool overloaded = signal.PressureLevel >= 2 || currentMs > targetMs * 1.25f;
                if (overloaded && math.isfinite(currentMs))
                    _overloadHoldSeconds = 0.45f;
            }

            if (_overloadHoldSeconds > 0f)
                _overloadHoldSeconds = math.max(0f, _overloadHoldSeconds - dt);
        }

        private void ConsumeAcousticPingSignals()
        {
            ReadOnlySpan<AcousticPingSignal> pings = SignalBus<AcousticPingSignal>.GetFrameSnapshot();
            float strongestPing01 = 0f;
            uint strongestSource = 0u;

            for (int i = 0; i < pings.Length; i++)
            {
                AcousticPingSignal signal = pings[i];
                float intensity = math.saturate(signal.Intensity01);
                float radius = math.isfinite(signal.RadiusMeters) ? math.max(signal.RadiusMeters, 0f) : DefaultPingRadiusMeters;
                if (intensity <= 0.0001f)
                    continue;

                float radiusBoost = math.saturate(radius / 180f);
                float contribution = math.saturate(intensity * (0.75f + radiusBoost * 0.25f));
                if (contribution > strongestPing01)
                {
                    strongestPing01 = contribution;
                    strongestSource = signal.SourceId;
                }
            }

            if (strongestPing01 <= 0f)
                return;

            _strobeTimerSeconds = StrobeDurationSeconds;
            _strobePeak01 = math.max(_strobePeak01, strongestPing01);
            _activeBiolumProfileId = (int)(strongestSource & (MaxGlobalBiolumStates - 1));
            _forceSchedule = true;
        }

        private void AdvanceStrobe(float dt)
        {
            if (_strobeTimerSeconds > 0f)
            {
                _strobeTimerSeconds = math.max(0f, _strobeTimerSeconds - dt);
                return;
            }

            if (_strobePeak01 <= 0f)
                return;

            float fadeStep = StrobeFadeSeconds > 0f ? dt / StrobeFadeSeconds : 1f;
            _strobePeak01 = math.max(0f, _strobePeak01 - fadeStep);
        }

        private float ResolveStrobe01()
        {
            if (_strobeTimerSeconds > 0f)
                return math.saturate(_strobePeak01);

            return math.saturate(_strobePeak01);
        }

        private void RefreshGlobalQualityWeight()
        {
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            _globalQualityWeight = math.saturate(math.isfinite(qualityWeight) ? qualityWeight : 1f);
        }

        private void UpdateBiomeBlendState(float dt)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !HasVaultBuffers())
                return;

            if (!vault.TryLockBuffer(BufferID.BiolumMockWeatherSignal, SystemID.Vfx))
                return;

            try
            {
                NativeArray<MockWeatherSignal> weather = _mockWeatherSignalHandle.Resolve(vault);
                if (!weather.IsCreated || weather.Length <= 0)
                    return;

                uint biomeHash = weather[0].CurrentBiomeHash;
                _individualGlowWeight01 = ResolveIndividualGlowWeight(_globalQualityWeight, weather[0].SystemHealthIndex01);
                _dearLieBlend01 = 1f - _individualGlowWeight01;
                _scheduledGpuColorCount = ResolveScheduledGlowCount(_individualGlowWeight01);
                if (_lastBiomeHash == 0u)
                    _lastBiomeHash = biomeHash;

                if (biomeHash != _lastBiomeHash)
                {
                    _lastBiomeHash = biomeHash;
                    _biomeBlend01 = 0f;
                }
                else
                {
                    _biomeBlend01 = math.saturate(_biomeBlend01 + dt * 0.1f);
                }
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.BiolumMockWeatherSignal, SystemID.Vfx);
            }
        }

        private static float ResolveIndividualGlowWeight(float globalQualityWeight, float systemHealthIndex01)
        {
            float quality = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            float stress = math.saturate(math.isfinite(systemHealthIndex01) ? systemHealthIndex01 : 0f);
            float healthWeight = 1f - SmoothStepRange01(0.65f, 0.95f, stress);
            return SmoothStepRange01(0.18f, 0.72f, math.min(quality, healthWeight));
        }

        private static int ResolveScheduledGlowCount(float individualGlowWeight01)
        {
            float weight = math.saturate(math.isfinite(individualGlowWeight01) ? individualGlowWeight01 : 1f);
            float activeWeight = SmoothStep01(weight) * math.step(0.0001f, weight);
            int count = (int)math.round(math.lerp((float)SyncGroupCount, (float)MaxGlowInstances, activeWeight));
            return math.clamp(count, SyncGroupCount, MaxGlowInstances);
        }

        private static float ResolveUpdateCadenceSeconds(float globalQualityWeight, float overloadHoldSeconds)
        {
            float quality = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            float qualityCurve = SmoothStepRange01(0.18f, 0.72f, quality);
            float qualityCadence = math.lerp(LowQualityUpdateIntervalSeconds, NormalUpdateIntervalSeconds, qualityCurve);
            float overload01 = math.saturate(math.isfinite(overloadHoldSeconds) ? overloadHoldSeconds / 0.45f : 0f);
            float overloadCadence = math.lerp(NormalUpdateIntervalSeconds, OverloadUpdateIntervalSeconds, overload01);
            return math.max(qualityCadence, overloadCadence);
        }

        private static float SmoothStepRange01(float edge0, float edge1, float value)
        {
            float denominator = math.max(0.0001f, edge1 - edge0);
            float t = math.saturate((value - edge0) / denominator);
            return t * t * (3f - 2f * t);
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        private void ConsumeMockPredatorSignalToPulse()
        {
            IDataVault vault = _dataVault;
            if (vault == null || !HasVaultBuffers())
                return;

            MockPredatorProximitySignal predator = default;
            if (!vault.TryLockBuffer(BufferID.BiolumMockPredatorSignal, SystemID.Vfx))
                return;

            try
            {
                NativeArray<MockPredatorProximitySignal> predatorSignal = _mockPredatorSignalHandle.Resolve(vault);
                if (!predatorSignal.IsCreated || predatorSignal.Length <= 0)
                    return;

                predator = predatorSignal[0];
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.BiolumMockPredatorSignal, SystemID.Vfx);
            }

            if (predator.Strength01 <= 0.01f || predator.FrameStamp == _lastPredatorSignalFrame)
                return;

            bool lockedPulses = false;
            bool lockedAges = false;
            try
            {
                lockedPulses = vault.TryLockBuffer(BufferID.BiolumSyncPulses, SystemID.Vfx);
                if (!lockedPulses)
                    return;

                lockedAges = vault.TryLockBuffer(BufferID.BiolumSyncPulseAges, SystemID.Vfx);
                if (!lockedAges)
                    return;

                NativeArray<SyncPulseDTO> pulses = _syncPulsesHandle.Resolve(vault);
                NativeArray<float> ages = _syncPulseAgesHandle.Resolve(vault);
                if (!pulses.IsCreated || !ages.IsCreated)
                    return;

                float waveSpeed = ResolveMockPredatorWaveSpeed(in predator);
                int slot = 0;
                float oldestAge = -1f;
                int count = math.min(SyncPulseCapacity, math.min(pulses.Length, ages.Length));
                for (int i = 0; i < count; i++)
                {
                    float age = ages[i];
                    if (age > oldestAge)
                    {
                        oldestAge = age;
                        slot = i;
                    }
                }

                pulses[slot] = new SyncPulseDTO
                {
                    OriginAUP = predator.OriginAUP,
                    WaveSpeed = waveSpeed,
                    ColorOverride = BiolumPackedColorUtility.PackRgb10A2(new float3(1f, 0.92f, 0.62f), 1f)
                };
                ages[slot] = 0f;
                _lastPredatorSignalFrame = predator.FrameStamp;
                _lastPulseOriginAUP = predator.OriginAUP;
                _activeSyncPulseCount = math.min(_activeSyncPulseCount + 1, count);
            }
            finally
            {
                if (lockedAges)
                    vault.TryUnlockBuffer(BufferID.BiolumSyncPulseAges, SystemID.Vfx);
                if (lockedPulses)
                    vault.TryUnlockBuffer(BufferID.BiolumSyncPulses, SystemID.Vfx);
            }
        }

        private void AdvanceMockPredatorSignal(float dt)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !HasVaultBuffers())
                return;

            if (!vault.TryLockBuffer(BufferID.BiolumMockPredatorSignal, SystemID.Vfx))
                return;

            try
            {
                NativeArray<MockPredatorProximitySignal> predatorSignal = _mockPredatorSignalHandle.Resolve(vault);
                if (!predatorSignal.IsCreated || predatorSignal.Length <= 0)
                    return;

                MockPredatorProximitySignal signal = predatorSignal[0];
                signal.Strength01 = math.max(0f, signal.Strength01 - dt * 0.45f);

                uint sectorHash = _lastBiomeHash != 0u ? _lastBiomeHash : _profileSourceHash;
                Unity.Mathematics.Random rng = CreateDeterministicRandom(sectorHash, _frameCounter, 0x6D2B79F5u);
                uint roll = rng.NextUInt();
                if ((roll & 255u) == 17u)
                {
                    float angleX = rng.NextFloat(0f, math.PI * 2f);
                    float angleZ = rng.NextFloat(0f, math.PI * 2f);
                    float radiusX = rng.NextFloat(64f, 96f);
                    float radiusZ = rng.NextFloat(60f, 92f);
                    signal.OriginAUP = new double3(
                        math.sin(angleX) * radiusX,
                        -218.0,
                        math.cos(angleZ) * radiusZ);
                    signal.RadiusMeters = rng.NextFloat(92f, 124f);
                    signal.Strength01 = 1f;
                    signal.SpeciesMask = 0xFFFFFFFFu;
                    signal.FrameStamp = _frameCounter;
                }

                predatorSignal[0] = signal;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.BiolumMockPredatorSignal, SystemID.Vfx);
            }
        }

        private float ResolveMockPredatorWaveSpeed(in MockPredatorProximitySignal predator)
        {
            float fallback = math.max(8f, predator.RadiusMeters * 0.65f);
            IDataVault vault = _dataVault;
            if (vault == null || !HasVaultBuffers())
                return fallback;

            if (!vault.TryLockBuffer(BufferID.BiolumSpeciesTuning, SystemID.Vfx))
                return fallback;

            try
            {
                NativeArray<BiolumSpeciesTuningDTO> species = _speciesTuningHandle.Resolve(vault);
                if (!species.IsCreated)
                    return fallback;

                uint mask = predator.SpeciesMask;
                float sum = 0f;
                int samples = 0;
                int count = math.min(MaxSpeciesTuningCount, species.Length);
                for (int i = 0; i < count && samples < SyncGroupCount; i++)
                {
                    uint bit = 1u << (i & 31);
                    if (mask != 0u && (mask & bit) == 0u)
                        continue;

                    float speed = species[i].WaveSpeed;
                    if (!math.isfinite(speed) || speed <= 0f)
                        continue;

                    sum += math.clamp(speed, 1f, 180f);
                    samples++;
                }

                return samples > 0 ? sum / math.max(1, samples) : fallback;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.BiolumSpeciesTuning, SystemID.Vfx);
            }
        }

        private void AdvanceSyncPulseAges(float dt)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !HasVaultBuffers())
                return;

            if (!vault.TryLockBuffer(BufferID.BiolumSyncPulseAges, SystemID.Vfx))
                return;

            try
            {
                NativeArray<float> ages = _syncPulseAgesHandle.Resolve(vault);
                if (!ages.IsCreated)
                    return;

                int active = 0;
                int count = math.min(SyncPulseCapacity, ages.Length);
                for (int i = 0; i < count; i++)
                {
                    float age = ages[i];
                    if (age < 8f)
                    {
                        age += dt;
                        ages[i] = age;
                        if (age < 8f)
                            active++;
                    }
                }

                _activeSyncPulseCount = active;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.BiolumSyncPulseAges, SystemID.Vfx);
            }
        }

        private void AdvanceMockDamageAge(float dt)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !HasVaultBuffers())
                return;

            if (!vault.TryLockBuffer(BufferID.BiolumMockDamageSignal, SystemID.Vfx))
                return;

            try
            {
                NativeArray<MockCombatDamageSignal> damage = _mockDamageSignalHandle.Resolve(vault);
                if (!damage.IsCreated || damage.Length <= 0)
                    return;

                MockCombatDamageSignal signal = damage[0];
                if (signal.AgeSeconds < 9f)
                {
                    signal.AgeSeconds += dt;
                    damage[0] = signal;
                }
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.BiolumMockDamageSignal, SystemID.Vfx);
            }
        }

        private void EnsureCsvBackgroundWatcher()
        {
            if (_csvWatcher != null)
                return;

            string path = ResolveCsvOverridePath();
            if (string.IsNullOrEmpty(path))
                return;

            Volatile.Write(ref _csvWorkerState, CsvWorkerIdle);

            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
                return;

            try
            {
                Directory.CreateDirectory(directory);
                _csvWatcher = new FileSystemWatcher(directory, CsvOverrideFileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    EnableRaisingEvents = false
                };
                _csvWatcher.Changed += OnCsvFileChanged;
                _csvWatcher.Created += OnCsvFileChanged;
                _csvWatcher.Renamed += OnCsvFileRenamed;
                _csvWatcher.EnableRaisingEvents = true;
            }
            catch (Exception)
            {
                _csvWatcher = null;
            }

            RequestCsvReload();
        }

        private void StopCsvBackgroundWatcher()
        {
            FileSystemWatcher watcher = _csvWatcher;
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Changed -= OnCsvFileChanged;
                watcher.Created -= OnCsvFileChanged;
                watcher.Renamed -= OnCsvFileRenamed;
                watcher.Dispose();
                _csvWatcher = null;
            }
        }

        private void OnCsvFileChanged(object sender, FileSystemEventArgs args)
        {
            RequestCsvReload();
        }

        private void OnCsvFileRenamed(object sender, RenamedEventArgs args)
        {
            RequestCsvReload();
        }

        private void RequestCsvReload()
        {
            int state = Volatile.Read(ref _csvWorkerState);
            if (state == CsvWorkerApplying)
                return;

            Interlocked.Exchange(ref _csvWorkerState, CsvWorkerRequested);
        }

        private unsafe void ApplyCsvOverridesIfReady()
        {
            if (Interlocked.CompareExchange(ref _csvWorkerState, CsvWorkerApplying, CsvWorkerRequested) != CsvWorkerRequested)
                return;

            IDataVault vault = _dataVault;
            if (vault == null || !HasVaultBuffers())
            {
                Interlocked.CompareExchange(ref _csvWorkerState, CsvWorkerIdle, CsvWorkerApplying);
                return;
            }

            bool lockedScratch = false;
            bool lockedGlowStates = false;
            bool lockedSpecies = false;
            bool retryWhenVaultUnlocks = false;
            try
            {
                lockedScratch = vault.TryLockBuffer(BufferID.BiolumCsvScratch, SystemID.Vfx);
                if (!lockedScratch)
                {
                    retryWhenVaultUnlocks = true;
                    return;
                }

                lockedGlowStates = vault.TryLockBuffer(BufferID.BiolumGlowStates, SystemID.Vfx);
                lockedSpecies = vault.TryLockBuffer(BufferID.BiolumSpeciesTuning, SystemID.Vfx);
                if (!lockedSpecies)
                {
                    retryWhenVaultUnlocks = true;
                    return;
                }

                NativeArray<byte> scratch = _csvScratchHandle.Resolve(vault);
                NativeArray<GlowStateDTO> glowStates = lockedGlowStates ? _glowStatesHandle.Resolve(vault) : default;
                NativeArray<BiolumSpeciesTuningDTO> species = _speciesTuningHandle.Resolve(vault);
                int bytesRead = TryReadCsvOverrideIntoScratch(scratch, out long writeTicks);
                if (!scratch.IsCreated || !species.IsCreated || bytesRead <= 0)
                    return;

                ParseCsvOverrides(scratch, bytesRead, species, glowStates);
                Volatile.Write(ref _csvLastWriteTicks, writeTicks);
                _forceSchedule = true;
            }
            finally
            {
                if (lockedSpecies)
                    vault.TryUnlockBuffer(BufferID.BiolumSpeciesTuning, SystemID.Vfx);
                if (lockedGlowStates)
                    vault.TryUnlockBuffer(BufferID.BiolumGlowStates, SystemID.Vfx);
                if (lockedScratch)
                    vault.TryUnlockBuffer(BufferID.BiolumCsvScratch, SystemID.Vfx);
                Interlocked.CompareExchange(
                    ref _csvWorkerState,
                    retryWhenVaultUnlocks ? CsvWorkerRequested : CsvWorkerIdle,
                    CsvWorkerApplying);
            }
        }

        private unsafe int TryReadCsvOverrideIntoScratch(NativeArray<byte> scratch, out long writeTicks)
        {
            writeTicks = 0L;
            if (!scratch.IsCreated)
                return 0;

            string path = _csvOverridePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return 0;

            try
            {
                writeTicks = File.GetLastWriteTimeUtc(path).Ticks;
                if (writeTicks == Volatile.Read(ref _csvLastWriteTicks))
                    return 0;

                int capacity = math.min(CsvScratchByteCount, scratch.Length);
                if (capacity <= 0)
                    return 0;

                void* scratchPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scratch);
                Span<byte> destination = new Span<byte>(scratchPtr, capacity);
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, capacity, FileOptions.SequentialScan))
                    return stream.Read(destination);
            }
            catch (Exception)
            {
                writeTicks = 0L;
                return 0;
            }
        }

        private string ResolveCsvOverridePath()
        {
            if (!string.IsNullOrEmpty(_csvOverridePath))
                return _csvOverridePath;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _csvOverridePath = Path.Combine(projectRoot, CsvOverrideFileName);
            return _csvOverridePath;
        }

        private static void ParseCsvOverrides(
            NativeArray<byte> scratch,
            int byteCount,
            NativeArray<BiolumSpeciesTuningDTO> species,
            NativeArray<GlowStateDTO> glowStates)
        {
            int cursor = 0;
            while (cursor < byteCount)
            {
                int lineStart = cursor;
                while (cursor < byteCount && scratch[cursor] != (byte)'\n' && scratch[cursor] != (byte)'\r')
                    cursor++;

                ParseCsvLine(scratch, lineStart, cursor, species, glowStates);

                while (cursor < byteCount && (scratch[cursor] == (byte)'\n' || scratch[cursor] == (byte)'\r'))
                    cursor++;
            }
        }

        private static void ParseCsvLine(
            NativeArray<byte> bytes,
            int start,
            int end,
            NativeArray<BiolumSpeciesTuningDTO> species,
            NativeArray<GlowStateDTO> glowStates)
        {
            start = SkipCsvWhitespace(bytes, start, end);
            if (start >= end || bytes[start] == (byte)'#')
                return;

            int tokenStart = start;
            int tokenEnd = FindCsvTokenEnd(bytes, tokenStart, end);
            if (tokenEnd <= tokenStart)
                return;

            uint speciesHash = TryParseUIntToken(bytes, tokenStart, tokenEnd, out uint parsedHash)
                ? parsedHash
                : HashToken(bytes, tokenStart, tokenEnd);

            int cursor = tokenEnd + 1;
            if (!TryReadCsvFloat(bytes, ref cursor, end, out float r) ||
                !TryReadCsvFloat(bytes, ref cursor, end, out float g) ||
                !TryReadCsvFloat(bytes, ref cursor, end, out float b) ||
                !TryReadCsvFloat(bytes, ref cursor, end, out float frequency))
            {
                return;
            }

            float waveSpeed = 0f;
            bool hasWaveSpeed = TryReadCsvFloat(bytes, ref cursor, end, out waveSpeed);

            int count = math.min(MaxSpeciesTuningCount, species.Length);
            if (count <= 0)
                return;

            int slot = (int)(speciesHash % (uint)count);
            for (int i = 0; i < count; i++)
            {
                if (species[i].SpeciesHash == speciesHash)
                {
                    slot = i;
                    break;
                }
            }

            BiolumSpeciesTuningDTO tuning = species[slot];
            tuning.SpeciesHash = speciesHash;
            tuning.PackedColor = BiolumPackedColorUtility.PackRgb10A2(new float3(r, g, b), 1f);
            tuning.Frequency = math.clamp(frequency, 0.0025f, 8f);
            tuning.WaveSpeed = hasWaveSpeed
                ? math.clamp(waveSpeed, 1f, 180f)
                : math.clamp(tuning.WaveSpeed <= 0f ? 48f : tuning.WaveSpeed, 1f, 180f);
            species[slot] = tuning;
            ApplySpeciesTuningToGlowStates(glowStates, tuning);
        }

        private static int SkipCsvWhitespace(NativeArray<byte> bytes, int cursor, int end)
        {
            while (cursor < end)
            {
                byte c = bytes[cursor];
                if (c != (byte)' ' && c != (byte)'\t' && c != (byte)',')
                    break;
                cursor++;
            }

            return cursor;
        }

        private static int FindCsvTokenEnd(NativeArray<byte> bytes, int cursor, int end)
        {
            while (cursor < end)
            {
                byte c = bytes[cursor];
                if (c == (byte)',' || c == (byte)' ' || c == (byte)'\t')
                    break;
                cursor++;
            }

            return cursor;
        }

        private static bool TryReadCsvFloat(NativeArray<byte> bytes, ref int cursor, int end, out float value)
        {
            value = 0f;
            cursor = SkipCsvWhitespace(bytes, cursor, end);
            int tokenEnd = FindCsvTokenEnd(bytes, cursor, end);
            if (tokenEnd <= cursor)
                return false;

            bool parsed = TryParseFloatToken(bytes, cursor, tokenEnd, out value);
            cursor = tokenEnd + 1;
            return parsed;
        }

        private static bool TryParseUIntToken(NativeArray<byte> bytes, int start, int end, out uint value)
        {
            value = 0u;
            int cursor = start;
            if (end - start > 2 && bytes[start] == (byte)'0' && (bytes[start + 1] == (byte)'x' || bytes[start + 1] == (byte)'X'))
            {
                cursor = start + 2;
                for (; cursor < end; cursor++)
                {
                    byte c = bytes[cursor];
                    uint digit;
                    if (c >= (byte)'0' && c <= (byte)'9')
                        digit = (uint)(c - (byte)'0');
                    else if (c >= (byte)'a' && c <= (byte)'f')
                        digit = (uint)(c - (byte)'a' + 10);
                    else if (c >= (byte)'A' && c <= (byte)'F')
                        digit = (uint)(c - (byte)'A' + 10);
                    else
                        return false;

                    value = (value << 4) | digit;
                }

                return cursor > start + 2;
            }

            for (; cursor < end; cursor++)
            {
                byte c = bytes[cursor];
                if (c < (byte)'0' || c > (byte)'9')
                    return false;

                value = value * 10u + (uint)(c - (byte)'0');
            }

            return cursor > start;
        }

        private static bool TryParseFloatToken(NativeArray<byte> bytes, int start, int end, out float value)
        {
            value = 0f;
            int cursor = start;
            float sign = 1f;
            if (cursor < end && bytes[cursor] == (byte)'-')
            {
                sign = -1f;
                cursor++;
            }

            bool any = false;
            float integer = 0f;
            while (cursor < end)
            {
                byte c = bytes[cursor];
                if (c < (byte)'0' || c > (byte)'9')
                    break;
                any = true;
                integer = integer * 10f + (c - (byte)'0');
                cursor++;
            }

            float fraction = 0f;
            float scale = 0.1f;
            if (cursor < end && bytes[cursor] == (byte)'.')
            {
                cursor++;
                while (cursor < end)
                {
                    byte c = bytes[cursor];
                    if (c < (byte)'0' || c > (byte)'9')
                        break;
                    any = true;
                    fraction += (c - (byte)'0') * scale;
                    scale *= 0.1f;
                    cursor++;
                }
            }

            if (!any || cursor != end)
                return false;

            value = (integer + fraction) * sign;
            return math.isfinite(value);
        }

        private static uint HashToken(NativeArray<byte> bytes, int start, int end)
        {
            uint hash = 2166136261u;
            for (int i = start; i < end; i++)
            {
                byte c = bytes[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                hash ^= c;
                hash *= 16777619u;
            }

            return hash;
        }

        private void ScheduleStateJob(float cadenceSeconds, float deltaTime)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !HasVaultBuffers())
                return;

            bool lockedStates = false;
            bool lockedGlowStates = false;
            bool lockedGpuBack = false;
            bool lockedAup = false;
            bool lockedPulses = false;
            bool lockedAges = false;
            bool lockedWeather = false;
            bool lockedDamage = false;
            bool lockedSpecies = false;
            try
            {
                lockedStates = vault.TryLockBuffer(BufferID.BiolumGlobalStates, SystemID.Vfx);
                if (!lockedStates)
                    return;

                lockedGlowStates = vault.TryLockBuffer(BufferID.BiolumGlowStates, SystemID.Vfx);
                if (!lockedGlowStates)
                    return;

                lockedGpuBack = vault.TryLockBuffer(BufferID.BiolumGlowGpuColorBack, SystemID.Vfx);
                if (!lockedGpuBack)
                    return;

                lockedAup = vault.TryLockBuffer(BufferID.BiolumGlowAupOrigins, SystemID.Vfx);
                if (!lockedAup)
                    return;

                lockedPulses = vault.TryLockBuffer(BufferID.BiolumSyncPulses, SystemID.Vfx);
                if (!lockedPulses)
                    return;

                lockedAges = vault.TryLockBuffer(BufferID.BiolumSyncPulseAges, SystemID.Vfx);
                if (!lockedAges)
                    return;

                lockedWeather = vault.TryLockBuffer(BufferID.BiolumMockWeatherSignal, SystemID.Vfx);
                if (!lockedWeather)
                    return;

                lockedDamage = vault.TryLockBuffer(BufferID.BiolumMockDamageSignal, SystemID.Vfx);
                if (!lockedDamage)
                    return;

                lockedSpecies = vault.TryLockBuffer(BufferID.BiolumSpeciesTuning, SystemID.Vfx);
                if (!lockedSpecies)
                    return;

                NativeArray<float4> jobStates = _jobStatesHandle.Resolve(vault);
                NativeArray<GlowStateDTO> glowStates = _glowStatesHandle.Resolve(vault);
                NativeArray<uint> gpuColors = _gpuColorBackHandle.Resolve(vault);
                NativeArray<double3> aupOrigins = _glowAupOriginsHandle.Resolve(vault);
                NativeArray<SyncPulseDTO> pulses = _syncPulsesHandle.Resolve(vault);
                NativeArray<float> pulseAges = _syncPulseAgesHandle.Resolve(vault);
                NativeArray<MockWeatherSignal> weather = _mockWeatherSignalHandle.Resolve(vault);
                NativeArray<MockCombatDamageSignal> damage = _mockDamageSignalHandle.Resolve(vault);
                NativeArray<BiolumSpeciesTuningDTO> speciesTuning = _speciesTuningHandle.Resolve(vault);
                if (!jobStates.IsCreated ||
                    !glowStates.IsCreated ||
                    !gpuColors.IsCreated ||
                    !aupOrigins.IsCreated ||
                    !pulses.IsCreated ||
                    !pulseAges.IsCreated ||
                    !weather.IsCreated ||
                    !damage.IsCreated ||
                    !speciesTuning.IsCreated)
                {
                    return;
                }

                BiolumVisualSyncJob job = new BiolumVisualSyncJob
                {
                    GlowStates = glowStates,
                    GpuColors = gpuColors,
                    AupOrigins = aupOrigins,
                    Pulses = pulses,
                    PulseAges = pulseAges,
                    WeatherSignal = weather,
                    DamageSignal = damage,
                    SpeciesTuning = speciesTuning,
                    States = jobStates,
                    TimeSeconds = _localTimeSeconds,
                    DeltaTime = deltaTime,
                    ActiveGlowCount = math.clamp(_activeGlowInstanceCount, 0, MaxGlowInstances),
                    ActivePulseCount = math.clamp(_activeSyncPulseCount, 0, SyncPulseCapacity),
                    ActiveIndividualCount = math.clamp(_scheduledGpuColorCount, SyncGroupCount, MaxGlowInstances),
                    BiomeBlend01 = _biomeBlend01,
                    GlobalQualityWeight = _globalQualityWeight,
                    IndividualGlowWeight01 = _individualGlowWeight01,
                    DearLieBlend01 = _dearLieBlend01,
                    Strobe01 = ResolveStrobe01(),
                    QualityTier = (byte)_qualityTier,
                    CadenceSeconds = cadenceSeconds
                };

                _stateJobScheduleTimestamp = Stopwatch.GetTimestamp();
                int scheduleCount = math.clamp(_scheduledGpuColorCount, SyncGroupCount, MaxGlowInstances);
                _scheduledGpuColorCount = scheduleCount;
                _stateJobHandle = job.Schedule(scheduleCount, BiolumJobInnerLoopBatchCount);
                H8Memory.RegisterActiveJob(SystemID.Vfx, _stateJobHandle);
                _stateJobScheduled = true;
                _jobLocksHeld = true;
            }
            finally
            {
                if (!_jobLocksHeld)
                {
                    if (lockedSpecies)
                        vault.TryUnlockBuffer(BufferID.BiolumSpeciesTuning, SystemID.Vfx);
                    if (lockedDamage)
                        vault.TryUnlockBuffer(BufferID.BiolumMockDamageSignal, SystemID.Vfx);
                    if (lockedWeather)
                        vault.TryUnlockBuffer(BufferID.BiolumMockWeatherSignal, SystemID.Vfx);
                    if (lockedAges)
                        vault.TryUnlockBuffer(BufferID.BiolumSyncPulseAges, SystemID.Vfx);
                    if (lockedPulses)
                        vault.TryUnlockBuffer(BufferID.BiolumSyncPulses, SystemID.Vfx);
                    if (lockedAup)
                        vault.TryUnlockBuffer(BufferID.BiolumGlowAupOrigins, SystemID.Vfx);
                    if (lockedGpuBack)
                        vault.TryUnlockBuffer(BufferID.BiolumGlowGpuColorBack, SystemID.Vfx);
                    if (lockedGlowStates)
                        vault.TryUnlockBuffer(BufferID.BiolumGlowStates, SystemID.Vfx);
                    if (lockedStates)
                        vault.TryUnlockBuffer(BufferID.BiolumGlobalStates, SystemID.Vfx);
                }
            }
        }

        private void CompleteScheduledJobAndPublish()
        {
            if (!_stateJobScheduled)
                return;

            if (!_stateJobHandle.IsCompleted)
            {
                _jobOverrunFrames = math.min(_jobOverrunFrames + 1, JobOverrunDumpFrameThreshold);
                _pendingTelemetryFlags |= TelemetryFlagJobOverrun;
                if (_jobOverrunFrames >= JobOverrunDumpFrameThreshold)
                    DumpBlackBox(TelemetryFlagJobOverrun);

                return;
            }

            // VISUAL_SYNC_FINALIZE: IsCompleted is true here; this releases Unity safety handles before owner-side publish.
            _stateJobHandle.Complete();
            long completeTimestamp = Stopwatch.GetTimestamp();
            _lastOscillatorComputeTimeMs = _stateJobScheduleTimestamp > 0L
                ? (float)((completeTimestamp - _stateJobScheduleTimestamp) * 1000.0 / Stopwatch.Frequency)
                : 0f;
            _stateJobScheduled = false;
            _jobOverrunFrames = 0;
            bool finite = CopyJobStatesToManagedBuffer();
            if (_scheduledGpuColorCount > SyncGroupCount)
                TryUploadGpuColorBufferFromLockedVault();
            UnlockJobBuffers();
            if (!finite)
            {
                _pendingTelemetryFlags |= TelemetryFlagNonFinite;
                RecordTelemetry(_pendingTelemetryFlags);
                _pendingTelemetryFlags = 0;
                DumpBlackBox(TelemetryFlagNonFinite);
                EvaluateColdStartStates();
            }

            UploadShaderGlobals(forceStateArray: true);
            if (_lastOscillatorComputeTimeMs > 0.1f)
            {
                _pendingTelemetryFlags |= TelemetryFlagJobOverrun;
                DumpBlackBox(TelemetryFlagJobOverrun);
            }
            _pendingTelemetryFlags |= finite ? (byte)0 : TelemetryFlagNonFinite;
        }

        private void CompleteScheduledJob()
        {
            if (_stateJobScheduled)
            {
                // BLOCKING_SYNC_POINT_TEARDOWN: shutdown drains owned visual job before releasing vault/GPU handles.
                _stateJobHandle.Complete();
                _stateJobScheduled = false;
            }

            UnlockJobBuffers();
        }

        private void UnlockJobBuffers()
        {
            if (!_jobLocksHeld)
                return;

            IDataVault vault = _dataVault;
            if (vault != null)
            {
                vault.TryUnlockBuffer(BufferID.BiolumSpeciesTuning, SystemID.Vfx);
                vault.TryUnlockBuffer(BufferID.BiolumMockDamageSignal, SystemID.Vfx);
                vault.TryUnlockBuffer(BufferID.BiolumMockWeatherSignal, SystemID.Vfx);
                vault.TryUnlockBuffer(BufferID.BiolumSyncPulseAges, SystemID.Vfx);
                vault.TryUnlockBuffer(BufferID.BiolumSyncPulses, SystemID.Vfx);
                vault.TryUnlockBuffer(BufferID.BiolumGlowAupOrigins, SystemID.Vfx);
                vault.TryUnlockBuffer(BufferID.BiolumGlowGpuColorBack, SystemID.Vfx);
                vault.TryUnlockBuffer(BufferID.BiolumGlowStates, SystemID.Vfx);
                vault.TryUnlockBuffer(BufferID.BiolumGlobalStates, SystemID.Vfx);
            }

            _jobLocksHeld = false;
        }

        private bool CopyJobStatesToManagedBuffer()
        {
            bool finite = true;
            int activeCount = SyncGroupCount;
            _publishedGlobalStateCount = activeCount;
            _dearLieGroupMatrix = Matrix4x4.zero;
            float strongest = 0f;
            int strongestProfile = 0;
            IDataVault vault = _dataVault;
            NativeArray<float4> jobStates = vault != null ? _jobStatesHandle.Resolve(vault) : default;
            if (!jobStates.IsCreated)
                return false;

            for (int i = 0; i < SyncGroupCount; i++)
            {
                if (i >= activeCount)
                    continue;

                float4 state = jobStates[i];
                if (!math.all(math.isfinite(state)))
                {
                    finite = false;
                    state = float4.zero;
                }

                state.xyz = math.clamp(state.xyz, float3.zero, new float3(MaxHdrIntensity));
                state.w = math.clamp(state.w, 0f, MaxHdrIntensity);
                _dearLieGroupMatrix.SetRow(i, new Vector4(state.x, state.y, state.z, state.w));

                if (i < activeCount && state.w > strongest)
                {
                    strongest = state.w;
                    strongestProfile = i;
                }
            }

            _activeBiolumProfileId = strongestProfile;
            return finite;
        }

        private unsafe void TryUploadGpuColorBufferFromLockedVault()
        {
            if (!EnsureGpuColorBuffer())
                return;

            IDataVault vault = _dataVault;
            NativeArray<uint> colors = vault != null ? _gpuColorBackHandle.Resolve(vault) : default;
            if (!colors.IsCreated)
                return;

            int scheduledCount = math.clamp(_scheduledGpuColorCount, 0, MaxGlowInstances);
            int count = math.min(math.min(math.clamp(_activeGlowInstanceCount, 0, MaxGlowInstances), scheduledCount), colors.Length);
            if (count <= 0)
                return;

            GraphicsBuffer writeBuffer = ResolveGpuWriteBuffer();
            if (writeBuffer == null)
                return;

            NativeArray<uint> mapped = writeBuffer.LockBufferForWrite<uint>(0, count);
            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(colors);
            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
            UnsafeUtility.MemCpy(destinationPtr, sourcePtr, (long)UnsafeUtility.SizeOf<uint>() * count);
            writeBuffer.UnlockBufferAfterWrite<uint>(count);
            _gpuColorFrontIndex = 1 - _gpuColorFrontIndex;
            Shader.SetGlobalBuffer(_BiolumGpuColorBufferId, ResolveGpuReadBuffer());
            _publishedGpuColorCount = count;
            _gpuColorBufferUploaded = true;
        }

        private void EvaluateColdStartStates()
        {
            _dearLieGroupMatrix = Matrix4x4.zero;
            if (!TryLockProfileBuffer(out IDataVault vault, out NativeArray<float> profileFloats))
            {
                _publishedGlobalStateCount = 0;
                return;
            }

            int activeCount = math.min(math.clamp(_activeStateCount, 1, MaxGlobalBiolumStates), SyncGroupCount);
            _publishedGlobalStateCount = activeCount;
            try
            {
                for (int i = 0; i < SyncGroupCount; i++)
                {
                    if (i >= activeCount)
                        continue;

                    int offset = i * ProfileFloatStride;
                    float intensity = math.clamp(profileFloats[offset + 4], 0f, MaxHdrIntensity);
                    _dearLieGroupMatrix.SetRow(i, new Vector4(
                        math.saturate(profileFloats[offset + 5]),
                        math.saturate(profileFloats[offset + 6]),
                        math.saturate(profileFloats[offset + 7]),
                        intensity));
                }
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.BiolumProfileFloats, SystemID.Vfx);
            }
        }

        private void UploadShaderGlobals(bool forceStateArray)
        {
            if (forceStateArray)
            {
                PublishDearLieGroups();
            }

            UploadShaderScalars();
        }

        private void PublishDearLieGroups()
        {
            Shader.SetGlobalMatrix(_GlobalBiolumDearLieGroupsId, _dearLieGroupMatrix);
        }

        private void UploadShaderScalars()
        {
            float strobe01 = ResolveStrobe01();
            float overloadFlag = math.saturate(math.isfinite(_overloadHoldSeconds) ? _overloadHoldSeconds / 0.45f : 0f);
            float cadence = ResolveUpdateCadenceSeconds(_globalQualityWeight, _overloadHoldSeconds);
            float timeFloat = (float)(_localTimeSeconds % 65536d);
            float masterPhase = math.frac(timeFloat * 0.045f);
            int globalStateCount = math.clamp(_publishedGlobalStateCount, 0, SyncGroupCount);
            int publishedGpuColorCount = _gpuColorBufferUploaded
                ? math.clamp(_publishedGpuColorCount, 0, MaxGlowInstances)
                : 0;
            float shaderIndividualGlowWeight = publishedGpuColorCount > SyncGroupCount
                ? math.saturate(_individualGlowWeight01)
                : 0f;

            Shader.SetGlobalVector(_GlobalBiolumParamsId, new Vector4(globalStateCount, (float)_qualityTier, strobe01, publishedGpuColorCount));
            Shader.SetGlobalVector(_GlobalBiolumClockId, new Vector4(timeFloat, cadence, _frameCounter, shaderIndividualGlowWeight));
            Shader.SetGlobalVector(_GlobalBiolumAupOffsetId, new Vector4(_aupOriginOffset.x, _aupOriginOffset.y, _aupOriginOffset.z, _profileSourceHash));
            Shader.SetGlobalVector(_BiolumIntensityId, new Vector4(ResolveLegacyBiolumIntensity(strobe01), strobe01, globalStateCount, overloadFlag));
            HectonShaderGlobalDataVaultBridge.PublishBiolumMasterPhase(new Vector4(masterPhase, ResolveTrianglePulse01(masterPhase), strobe01, _dearLieBlend01));
        }

        private float ResolveLegacyBiolumIntensity(float strobe01)
        {
            int activeCount = SyncGroupCount;
            float resolved = math.clamp(strobe01 * MaxHdrIntensity, 0f, MaxHdrIntensity);
            for (int i = 0; i < activeCount; i++)
            {
                float intensity = _dearLieGroupMatrix.GetRow(i).w;
                if (math.isfinite(intensity))
                    resolved = math.max(resolved, math.clamp(intensity, 0f, MaxHdrIntensity));
            }

            return math.clamp(resolved, 0f, MaxHdrIntensity);
        }

        private void ClearShaderGlobals()
        {
            _dearLieGroupMatrix = Matrix4x4.zero;
            _publishedGlobalStateCount = 0;
            Shader.SetGlobalVector(_GlobalBiolumParamsId, Vector4.zero);
            Shader.SetGlobalVector(_GlobalBiolumClockId, Vector4.zero);
            Shader.SetGlobalVector(_GlobalBiolumAupOffsetId, Vector4.zero);
            Shader.SetGlobalVector(_BiolumIntensityId, Vector4.zero);
            Shader.SetGlobalMatrix(_GlobalBiolumDearLieGroupsId, Matrix4x4.zero);
            HectonShaderGlobalDataVaultBridge.PublishBiolumMasterPhase(new Vector4(0f, 0.5f, 0f, 0f));
        }

        private void RecordTelemetry(byte flags)
        {
            if (!TryLockBlackBoxBuffer(out IDataVault vault, out NativeArray<BiolumPulseTelemetryEntry> blackBox))
                return;

            try
            {
                Vector4 primaryState = _dearLieGroupMatrix.GetRow(0);
                int telemetryGlowCount = _gpuColorBufferUploaded && _publishedGpuColorCount > SyncGroupCount
                    ? _publishedGpuColorCount
                    : SyncGroupCount;

                blackBox[_blackBoxCursor] = new BiolumPulseTelemetryEntry
                {
                    Frame = _frameCounter,
                    ActiveGlowingInstances = (uint)math.clamp(telemetryGlowCount, SyncGroupCount, MaxGlowInstances),
                    WavePulsesActive = (ushort)math.clamp(_activeSyncPulseCount, 0, SyncPulseCapacity),
                    QualityTier = (byte)_qualityTier,
                    Flags = flags,
                    OscillatorComputeTimeMs = math.max(0f, _lastOscillatorComputeTimeMs),
                    PrimaryIntensityHdr = math.clamp(primaryState.w, 0f, MaxHdrIntensity),
                    TimeSeconds = (float)(_localTimeSeconds % 65536d),
                    AupOffsetX = _aupOriginOffset.x,
                    AupOffsetZ = _aupOriginOffset.z
                };

                _blackBoxCursor = (_blackBoxCursor + 1) % blackBox.Length;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.BiolumBlackBox, SystemID.Vfx);
            }
        }

        private void DumpBlackBox(byte reason)
        {
            if (_dumpedFault || !TryLockBlackBoxBuffer(out IDataVault vault, out NativeArray<BiolumPulseTelemetryEntry> blackBox))
                return;

            _dumpedFault = true;
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                WriteBlackBoxDump(Path.Combine(projectRoot, DumpRelativePath), reason, blackBox);
                WriteBlackBoxDump(Path.Combine(projectRoot, DumpMirrorRelativePath), reason, blackBox);
            }
            catch (Exception)
            {
                _dumpedFault = false;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.BiolumBlackBox, SystemID.Vfx);
            }
        }

        private void WriteBlackBoxDump(string dumpPath, byte reason, NativeArray<BiolumPulseTelemetryEntry> blackBox)
        {
            string directory = Path.GetDirectoryName(dumpPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                BiolumPulseDumpHeader header = new BiolumPulseDumpHeader
                {
                    Magic = BlackBoxMagic,
                    Reason = reason,
                    EntrySizeBytes = BlackBoxEntrySizeBytes,
                    WriteCursor = _blackBoxCursor,
                    EntryCount = blackBox.Length
                };

                WriteUnmanaged(stream, ref header);
                for (int i = 0; i < blackBox.Length; i++)
                {
                    BiolumPulseTelemetryEntry entry = blackBox[(_blackBoxCursor + i) % blackBox.Length];
                    WriteUnmanaged(stream, ref entry);
                }
            }
        }

        private static int ResolveStateCount(HectonQualityTier tier)
        {
            switch (tier)
            {
                case HectonQualityTier.Ultra:
                    return 16;
                case HectonQualityTier.High:
                    return 16;
                case HectonQualityTier.Mid:
                    return 4;
                default:
                    return 1;
            }
        }

        private static float SanitizeDelta(float deltaTime)
        {
            if (!math.isfinite(deltaTime) || deltaTime <= 0f)
                return 0f;

            return math.min(deltaTime, 0.25f);
        }

        private void AdvanceSimulationFrameCounter()
        {
            _frameCounter = _frameCounter == uint.MaxValue ? 1u : _frameCounter + 1u;
        }

        private static float ResolveTrianglePulse01(float phase01)
        {
            return 1f - math.abs(math.frac(phase01) * 2f - 1f);
        }

        private static void WriteUnmanaged<T>(FileStream stream, ref T value) where T : unmanaged
        {
            ReadOnlySpan<T> valueSpan = MemoryMarshal.CreateReadOnlySpan(ref value, 1);
            stream.Write(MemoryMarshal.AsBytes(valueSpan));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct BiolumVisualSyncJob : IJobParallelFor
        {
            [NoAlias]
            public NativeArray<GlowStateDTO> GlowStates;
            [NoAlias]
            public NativeArray<uint> GpuColors;
            [ReadOnly, NoAlias]
            public NativeArray<double3> AupOrigins;
            [ReadOnly, NoAlias]
            public NativeArray<SyncPulseDTO> Pulses;
            [ReadOnly, NoAlias]
            public NativeArray<float> PulseAges;
            [ReadOnly, NoAlias]
            public NativeArray<MockWeatherSignal> WeatherSignal;
            [ReadOnly, NoAlias]
            public NativeArray<MockCombatDamageSignal> DamageSignal;
            [ReadOnly, NoAlias]
            public NativeArray<BiolumSpeciesTuningDTO> SpeciesTuning;
            [NoAlias]
            public NativeArray<float4> States;
            public double TimeSeconds;
            public float DeltaTime;
            public int ActiveGlowCount;
            public int ActivePulseCount;
            public int ActiveIndividualCount;
            public float BiomeBlend01;
            public float GlobalQualityWeight;
            public float IndividualGlowWeight01;
            public float DearLieBlend01;
            public float Strobe01;
            public byte QualityTier;
            public float CadenceSeconds;

            public void Execute(int index)
            {
                MockWeatherSignal weather = WeatherSignal.Length > 0 ? WeatherSignal[0] : default;

                if (index < SyncGroupCount)
                    States[index] = ResolveDearLieState(index, weather);

                if (index >= ActiveIndividualCount)
                    return;

                if (index >= ActiveGlowCount || index >= GlowStates.Length || index >= GpuColors.Length || index >= AupOrigins.Length)
                    return;

                float individualWeight = math.saturate(IndividualGlowWeight01);
                if (individualWeight <= 0.0001f)
                    return;

                ref GlowStateDTO glow = ref GetGlowRef(index);
                float frequency = math.max(glow.Frequency, 0.0025f);
                glow.Phase = math.frac(glow.Phase + frequency * DeltaTime);

                float phase = glow.Phase;
                uint basePacked = ResolveBiomePackedColor(glow.PackedColor, weather.CurrentBiomeHash, BiomeBlend01, (uint)index);

                float shaped = ResolveSmoothedTrianglePulse01(phase);
                float ambientMultiplier = math.saturate(1f - weather.AmbientLightLevel);
                float qualityCurve = SmoothStep01(math.saturate(GlobalQualityWeight));
                float tierGain = math.lerp(0.94f, QualityTier >= (byte)HectonQualityTier.High ? 1.08f : 1f, qualityCurve);
                float cadenceBoost = math.saturate(CadenceSeconds * 15f) * 0.04f;
                float intensity = math.saturate((0.18f + shaped * 0.82f) * tierGain + cadenceBoost) * ambientMultiplier * math.lerp(0.82f, 1f, individualWeight);

                float3 color = BiolumPackedColorUtility.UnpackRgb10A2(basePacked);
                float pulseStrength = 0f;
                uint pulsePacked = basePacked;
                if (individualWeight > 0.001f)
                    ResolveSpatialPulse(index, ref pulseStrength, ref pulsePacked, ref phase);
                pulseStrength *= individualWeight;

                if (pulseStrength > 0f)
                {
                    float3 pulseColor = BiolumPackedColorUtility.UnpackRgb10A2(pulsePacked);
                    color = math.lerp(color, pulseColor, pulseStrength);
                    intensity = math.saturate(math.max(intensity, pulseStrength));
                    glow.Phase = phase;
                }

                ApplyDamageFlicker(index, ref color, ref intensity, ref phase);
                ApplyOxygenWarning(weather, ref color, ref intensity, ref frequency);
                glow.Frequency = frequency;
                glow.Phase = phase;

                float strobe = math.saturate(Strobe01);
                color = math.lerp(color, new float3(1f, 1f, 1f), strobe);
                intensity = math.saturate(math.max(intensity, strobe));
                color *= intensity;

                float4 finiteCheck = new float4(color, intensity);
                GpuColors[index] = math.all(math.isfinite(finiteCheck))
                    ? BiolumPackedColorUtility.PackRgb10A2(color, intensity)
                    : 0u;
            }

            private ref GlowStateDTO GetGlowRef(int index)
            {
                // Current IJobParallelFor index exclusively owns the matching GlowStateDTO slot.
                byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(GlowStates);
                return ref UnsafeUtility.AsRef<GlowStateDTO>(basePtr + index * UnsafeUtility.SizeOf<GlowStateDTO>());
            }

            private float4 ResolveDearLieState(int groupIndex, MockWeatherSignal weather)
            {
                if (SpeciesTuning.Length <= 0)
                    return float4.zero;

                int speciesIndex = (groupIndex * 37) % SpeciesTuning.Length;
                BiolumSpeciesTuningDTO species = SpeciesTuning[speciesIndex];
                float time = (float)(TimeSeconds % 4096d);
                float phase = math.frac(time * math.max(species.Frequency, 0.0025f) + groupIndex * 0.25f);
                float shaped = ResolveSmoothedTrianglePulse01(phase);
                uint packed = ResolveBiomePackedColor(species.PackedColor, weather.CurrentBiomeHash, BiomeBlend01, (uint)groupIndex);
                float ambientMultiplier = math.saturate(1f - weather.AmbientLightLevel);
                float dearLieGain = math.lerp(1f, 1.08f, math.saturate(DearLieBlend01));
                float qualityGain = math.lerp(0.9f, 1.08f, SmoothStep01(math.saturate(GlobalQualityWeight)));
                float intensity = math.saturate((0.22f + shaped * 0.78f) * dearLieGain * qualityGain) * ambientMultiplier;
                float3 color = BiolumPackedColorUtility.UnpackRgb10A2(packed) * intensity;
                float4 state = new float4(color, intensity);
                return math.all(math.isfinite(state)) ? state : float4.zero;
            }

            private void ResolveSpatialPulse(int index, ref float strongest, ref uint packedColor, ref float phase)
            {
                int pulseCount = math.min(ActivePulseCount, math.min(Pulses.Length, PulseAges.Length));
                double3 plantAup = AupOrigins[index];
                for (int i = 0; i < pulseCount; i++)
                {
                    float age = PulseAges[i];
                    if (age < 0f || age > 8f)
                        continue;

                    SyncPulseDTO pulse = Pulses[i];
                    double3 deltaAup = plantAup - pulse.OriginAUP;
                    float3 localDelta = (float3)deltaAup;
                    if (!math.all(math.isfinite(localDelta)))
                        continue;

                    float speed = math.isfinite(pulse.WaveSpeed) ? math.max(0f, pulse.WaveSpeed) : 0f;
                    float radius = speed * age;
                    float width = math.max(2f, speed * 0.09f);
                    float distanceSq = math.lengthsq(localDelta);
                    float radiusSq = radius * radius;
                    float shellWidthSq = math.max(width * math.max(radius + radius + width, 0.0001f), 0.0001f);
                    float waveFront = 1f - math.saturate(math.abs(distanceSq - radiusSq) / shellWidthSq);
                    if (waveFront > strongest)
                    {
                        strongest = waveFront;
                        packedColor = pulse.ColorOverride;
                        phase = math.frac(0.03f + waveFront * 0.12f);
                    }
                }
            }

            private void ApplyDamageFlicker(int index, ref float3 color, ref float intensity, ref float phase)
            {
                if (DamageSignal.Length <= 0)
                    return;

                MockCombatDamageSignal damage = DamageSignal[0];
                if (damage.AgeSeconds < 0f || damage.AgeSeconds > 2f || damage.RadiusMeters <= 0.01f)
                    return;

                double3 deltaAup = AupOrigins[index] - damage.OriginAUP;
                float3 localDelta = (float3)deltaAup;
                if (!math.all(math.isfinite(localDelta)))
                    return;

                float radius = math.max(damage.RadiusMeters, 0.0001f);
                float falloff = SmoothStep01(1f - math.saturate(math.lengthsq(localDelta) / math.max(radius * radius, 0.0001f)));
                if (falloff <= 0f)
                    return;

                uint timeBucket = (uint)math.floor(math.max(0f, (float)(TimeSeconds % 65536d)) * 30f);
                float chaos = Hash01((uint)index + 1u, damage.FrameStamp, timeBucket);
                float flicker = math.saturate((chaos > 0.42f ? chaos : 0.08f) * falloff);
                float3 damageColor = BiolumPackedColorUtility.UnpackRgb10A2(damage.PackedDamageColor);
                color = math.lerp(color, damageColor, math.saturate(falloff * 0.78f));
                intensity = math.saturate(math.max(intensity, flicker));
                phase = math.frac(chaos + damage.AgeSeconds * 3.7f);
            }

            private void ApplyOxygenWarning(MockWeatherSignal weather, ref float3 color, ref float intensity, ref float frequency)
            {
                if (weather.O2Level01 >= 0.1f)
                    return;

                float warning01 = math.saturate(1f - weather.O2Level01 * 10f);
                float heartbeatPhase = math.frac((float)(TimeSeconds % 4096d) * 1.35f);
                float pulse = ResolveSmoothedTrianglePulse01(heartbeatPhase) * warning01;
                color = math.lerp(color, new float3(1f, 0.04f, 0.025f), math.saturate(pulse * 0.72f));
                intensity = math.saturate(math.max(intensity, 0.34f + pulse * 0.66f));
                frequency = math.lerp(frequency, 1.35f + warning01 * 0.45f, warning01 * 0.12f);
            }

            private static float ResolveSmoothedTrianglePulse01(float phase01)
            {
                float triangle = 1f - math.abs(math.frac(phase01) * 2f - 1f);
                return triangle * triangle * (3f - 2f * triangle);
            }

            private static float Hash01(uint a, uint b, uint c)
            {
                uint hash = a * 0x9E3779B9u;
                hash ^= b * 0x85EBCA6Bu;
                hash ^= c * 0xC2B2AE35u;
                hash ^= hash >> 16;
                hash *= 0x7FEB352Du;
                hash ^= hash >> 15;
                return (hash & 0x00FFFFFFu) * (1f / 16777215f);
            }

            private static uint ResolveBiomePackedColor(uint basePacked, uint biomeHash, float blend01, uint salt)
            {
                float smoothBlend = SmoothStep01(math.saturate(blend01));
                uint hash = DeterministicHash(biomeHash ^ (salt * 0x9E3779B9u));
                float3 biomeColor = new float3(
                    0.12f + ((hash & 255u) * (1f / 255f)) * 0.48f,
                    0.48f + (((hash >> 8) & 255u) * (1f / 255f)) * 0.42f,
                    0.68f + (((hash >> 16) & 255u) * (1f / 255f)) * 0.32f);
                uint targetPacked = BiolumPackedColorUtility.PackRgb10A2(math.saturate(biomeColor), 1f);
                return BiolumPackedColorUtility.LerpPackedColor(basePacked, targetPacked, smoothBlend);
            }

            private static float SmoothStep01(float t)
            {
                return t * t * (3f - 2f * t);
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct BiolumPulseTelemetryEntry
        {
            [FieldOffset(0)]
            public uint Frame;
            [FieldOffset(4)]
            public uint ActiveGlowingInstances;
            [FieldOffset(8)]
            public ushort WavePulsesActive;
            [FieldOffset(10)]
            public byte QualityTier;
            [FieldOffset(11)]
            public byte Flags;
            [FieldOffset(12)]
            public float OscillatorComputeTimeMs;
            [FieldOffset(16)]
            public float PrimaryIntensityHdr;
            [FieldOffset(20)]
            public float TimeSeconds;
            [FieldOffset(24)]
            public float AupOffsetX;
            [FieldOffset(28)]
            public float AupOffsetZ;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct BiolumPulseDumpHeader
        {
            [FieldOffset(0)]
            public uint Magic;
            [FieldOffset(4)]
            public byte Reason;
            [FieldOffset(5)]
            public byte Reserved;
            [FieldOffset(6)]
            public ushort EntrySizeBytes;
            [FieldOffset(8)]
            public int WriteCursor;
            [FieldOffset(12)]
            public int EntryCount;
        }
    }
}
