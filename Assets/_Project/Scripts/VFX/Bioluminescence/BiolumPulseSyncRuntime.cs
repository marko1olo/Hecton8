using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.VFX.Bioluminescence
{
    /// <summary>
    /// Per-instance coral glow state. Four records fit exactly in one 64-byte cache line.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct GlowStateDTO
    {
        [FieldOffset(0)] public uint PackedColor;
        [FieldOffset(4)] public float Phase;
        [FieldOffset(8)] public float Frequency;
        [FieldOffset(12)] public uint SpeciesHash;
    }

    /// <summary>
    /// Global bioluminescence wave trigger using double precision AUP before local float math.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SyncPulseDTO
    {
        [FieldOffset(0)] public double3 OriginAUP;
        [FieldOffset(24)] public float WaveSpeed;
        [FieldOffset(28)] public uint ColorOverride;
    }

    /// <summary>
    /// Local weather and survival mock input for the bioluminescence domain.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct MockWeatherSignal
    {
        [FieldOffset(0)] public float AmbientLightLevel;
        [FieldOffset(4)] public float O2Level01;
        [FieldOffset(8)] public float SystemHealthIndex01;
        [FieldOffset(12)] public uint CurrentBiomeHash;
    }

    /// <summary>
    /// One global pulse matrix mirrored directly to _GlobalBiolumDearLieGroups.
    /// Rows are Phase, Frequency, Amplitude, SpatialOffset.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BiolumPulseStateDTO
    {
        [FieldOffset(0)]
        public float4 Group1_Params;
        [FieldOffset(16)]
        public float4 Group2_Params;
        [FieldOffset(32)]
        public float4 Group3_Params;
        [FieldOffset(48)]
        public float4 Group4_Params;
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
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct BiolumMockPredatorProximitySignal
    {
        [FieldOffset(0)] public double3 OriginAUP;
        [FieldOffset(24)] public float RadiusMeters;
        [FieldOffset(28)] public float Strength01;
        [FieldOffset(32)] public uint SpeciesMask;
        [FieldOffset(36)] public uint FrameStamp;
        [FieldOffset(40)] private ulong _pad0;
        [FieldOffset(48)] private ulong _pad1;
        [FieldOffset(56)] private ulong _pad2;
    }

    /// <summary>
    /// Local combat damage mock for visual flicker without combat-domain coupling.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct BiolumMockCombatDamageSignal
    {
        [FieldOffset(0)] public double3 OriginAUP;
        [FieldOffset(24)] public float RadiusMeters;
        [FieldOffset(28)] public float AgeSeconds;
        [FieldOffset(32)] public uint PackedDamageColor;
        [FieldOffset(36)] public uint FrameStamp;
        [FieldOffset(40)] private ulong _pad0;
        [FieldOffset(48)] private ulong _pad1;
        [FieldOffset(56)] private ulong _pad2;
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
        IColdTickable,
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
        private const int BlackBoxDumpStateIdle = 0;
        private const int BlackBoxDumpStateQueued = 1;
        private const int BlackBoxDumpStateWriting = 2;
        private const int BlackBoxDumpStateWritten = 3;
        private const int BlackBoxDumpStateFailed = 4;
        private const int BlackBoxDumpWorkerJoinMilliseconds = 1000;
        private const float StrobeDurationSeconds = 0.1f;
        private const float StrobeFadeSeconds = 0.16f;
        private const float OverloadUpdateIntervalSeconds = 1f / 15f;
        private const float LowQualityUpdateIntervalSeconds = 1f / 5f;
        private const float NormalUpdateIntervalSeconds = 0f;
        private const float MaxHdrIntensity = 10f;
        private const float DefaultPingRadiusMeters = 80f;
        private const float TwoPi = 6.283185307179586f;
        private const float DefaultDarknessActivationThreshold = 0.42f;
        private const float DefaultDepthDarknessStartMeters = 18f;
        private const float DefaultDepthDarknessFullMeters = 95f;
        private const float DefaultPredatorPanicSpeed = 2.35f;
        private const int JobOverrunDumpFrameThreshold = BlackBoxFrameCount;
        private const byte TelemetryFlagNonFinite = 1;
        private const byte TelemetryFlagJobOverrun = 2;
        private const byte TelemetryFlagAupInvalid = 4;
        private const ushort BlackBoxEntrySizeBytes = 64;
        private const uint BlackBoxMagic = 0x42505359u; // BPSY
        private const int BlackBoxDumpHeaderSizeBytes = 16;
        private const int BlackBoxDumpByteCount = BlackBoxDumpHeaderSizeBytes + (BlackBoxFrameCount * BlackBoxEntrySizeBytes);
        private const uint ProfileFallbackHash = 0x424C4642u; // BLFB
        private const uint ProfileBinaryHash = 0x424C554Du; // BLUM
        private const uint EmergencyNeonBluePacked = 0xFBBE1000u;
        private const BufferID BiolumPulseStateBufferId = (BufferID)70311;
        private const BufferID BiolumBlackBoxDumpScratchBufferId = (BufferID)70312;
        private const string ProfileFileName = "Biolum_Profiles.bin";
        private const string ProfileH8BinFileName = "Biolum_Profiles.h8bin";
        private const string LegacyPaletteArchiveName = "biolum_color_palettes.h8bin";
        private const string LegacyPulseArchiveName = "flora_pulse_rates.bin";
        private const string CsvOverrideFileName = "biolum_pulse_profiles.csv";
        private const string LegacyCsvOverrideFileName = "biolum_profiles.csv";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_238.bin";
        private const string DumpMirrorRelativePath = "Docs/AgentLogs/Dump_SHINOBU_238.h8dump";
        private const double ShaderClockWrapSeconds = 65536d;
        private const uint StateJobPinPulseState = 1u << 0;
        private const uint StateJobPinProfileFloats = 1u << 1;
        private const uint StateJobPinMockWeather = 1u << 2;
        private const uint StateJobPinMockPredator = 1u << 3;
        private const uint StateJobPinSyncPulses = 1u << 4;
        private const uint StateJobPinSyncPulseAges = 1u << 5;

        private static readonly ProfilerMarker _tickMarker = new ProfilerMarker("H8.VFX.BiolumPulseSync.Tick");
        private static readonly ProfilerMarker _lateFrameMarker = new ProfilerMarker("H8.VFX.BiolumPulseSync.LateFrame");
        private static readonly int _GlobalBiolumDearLieGroupsId = Shader.PropertyToID("_GlobalBiolumDearLieGroups");
        private static readonly int _GlobalBiolumParamsId = Shader.PropertyToID("_GlobalBiolumParams");
        private static readonly int _GlobalBiolumClockId = Shader.PropertyToID("_GlobalBiolumClock");
        private static readonly int _GlobalBioTimeId = Shader.PropertyToID("_GlobalBioTime");
        private static readonly int _GlobalBiolumAupOffsetId = Shader.PropertyToID("_GlobalBiolumAupOffset");
        private static readonly int _BiolumIntensityId = Shader.PropertyToID("_BiolumIntensity");
        private const uint EmergencyAbyssNeonHash = 0x1A205600u;
        private const uint EmergencyCoralSyncHash = 0xD9552170u;
        private const uint CsvGroupHash = 0x5FB91E8Cu;
        private const uint CsvPulseHash = 0x550BFE9Eu;
        private const uint CsvRowHash = 0x440E1D7Bu;
        private static readonly uint _EmergencyCyanPacked = BiolumPackedColorUtility.PackRgb10A2(new float3(0.05f, 0.72f, 1f), 1f);
        private static readonly uint _EmergencyGreenPacked = BiolumPackedColorUtility.PackRgb10A2(new float3(0.10f, 1f, 0.62f), 1f);
        private static readonly uint _EmergencyVioletPacked = BiolumPackedColorUtility.PackRgb10A2(new float3(0.72f, 0.28f, 1f), 1f);
        private static readonly uint _EmergencyAmberPacked = BiolumPackedColorUtility.PackRgb10A2(new float3(1f, 0.66f, 0.18f), 1f);
        private static int s_runtimeClaimed;
        private static BiolumPulseSyncRuntime s_activeRuntime;
#if UNITY_EDITOR
        private static bool s_editorReloadHooked;
#endif

        private static readonly ulong ProfileFloatsGuardMask =
            BiolumMutationGuardBit(BufferID.BiolumProfileFloats);

        private static readonly ulong BlackBoxGuardMask =
            BiolumMutationGuardBit(BufferID.BiolumBlackBox);

        private static readonly ulong GlowStatesGuardMask =
            BiolumMutationGuardBit(BufferID.BiolumGlowStates);

        private static readonly ulong GlowAupOriginsGuardMask =
            BiolumMutationGuardBit(BufferID.BiolumGlowAupOrigins);

        private static readonly ulong MockWeatherGuardMask =
            BiolumMutationGuardBit(BufferID.BiolumMockWeatherSignal);

        private static readonly ulong MockDamageGuardMask =
            BiolumMutationGuardBit(BufferID.BiolumMockDamageSignal);

        private static readonly ulong MockPredatorGuardMask =
            BiolumMutationGuardBit(BufferID.BiolumMockPredatorSignal);

        private static readonly ulong SpeciesTuningGuardMask =
            BiolumMutationGuardBit(BufferID.BiolumSpeciesTuning);

        private static readonly ulong PulseStateGuardMask =
            BiolumMutationGuardBit(BiolumPulseStateBufferId);

        private static readonly ulong SyncPulseGuardMask =
            BiolumMutationGuardBit(BufferID.BiolumSyncPulses) |
            BiolumMutationGuardBit(BufferID.BiolumSyncPulseAges);

        private static readonly ulong BlackBoxDumpScratchGuardMask =
            BiolumMutationGuardBit(BiolumBlackBoxDumpScratchBufferId);

        // SOURCE DECISION BIOLUM_BLACKBOX_OWNER_LOCAL_20260605: ACCEPT_OWNER_LOCAL_PENDING_PROOF.
        // BlackBoxDumpSnapshotOwner.Entries and _blackBoxDumpWriteBytes stay owner-local diagnostic scratch.
        // Lifetime: H8Memory/SystemID.Vfx scene owner. Disposal: owner Dispose()/DisposeBlackBoxDumpSnapshot()
        // and DisposeBlackBoxDumpWriteBytes() before lifecycle release. No gameplay authority, no cross-domain
        // snapshot contract, and no blind DataVault migration; these buffers only decouple crash dump file IO
        // from DataVault write guards while the DataVault black-box ring remains runtime telemetry authority.
        private struct BlackBoxDumpSnapshotOwner : IDisposable
        {
            public NativeArray<BiolumPulseTelemetryEntry> Entries;

            public bool IsReady(int requiredLength)
            {
                return requiredLength > 0 &&
                       Entries.IsCreated &&
                       Entries.Length >= requiredLength;
            }

            public void Allocate(int requiredLength)
            {
                Dispose();

                // COLD NATIVE ALLOC: BiolumPulseTelemetryEntry[300] - black-box dump snapshot, flattens DataVault write locks - owner: BIOLUM_PULSE_SYNC
                Entries = H8Memory.Allocate<BiolumPulseTelemetryEntry>(
                    requiredLength,
                    SystemID.Vfx,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
                if (!Entries.IsCreated)
                    throw new InvalidOperationException("H8Memory allocation failed for biolum black-box dump snapshot.");
            }

            public void Dispose()
            {
                if (!Entries.IsCreated)
                {
                    Entries = default;
                    return;
                }

                H8Memory.Release(ref Entries, SystemID.Vfx);
            }
        }

        private IDataVault _dataVault;
        private VaultGenerationHandle<float> _profileFloatsHandle;
        private VaultGenerationHandle<BiolumPulseStateDTO> _pulseStateHandle;
        private VaultGenerationHandle<BiolumPulseTelemetryEntry> _blackBoxHandle;
        private VaultGenerationHandle<GlowStateDTO> _glowStatesHandle;
        private VaultGenerationHandle<double3> _glowAupOriginsHandle;
        private VaultGenerationHandle<SyncPulseDTO> _syncPulsesHandle;
        private VaultGenerationHandle<float> _syncPulseAgesHandle;
        private VaultGenerationHandle<MockWeatherSignal> _mockWeatherSignalHandle;
        private VaultGenerationHandle<BiolumMockPredatorProximitySignal> _mockPredatorSignalHandle;
        private VaultGenerationHandle<BiolumMockCombatDamageSignal> _mockDamageSignalHandle;
        private VaultGenerationHandle<BiolumSpeciesTuningDTO> _speciesTuningHandle;
        private VaultGenerationHandle<byte> _blackBoxDumpScratchHandle;
        private BlackBoxDumpSnapshotOwner _blackBoxDumpSnapshot;
        private NativeArray<byte> _blackBoxDumpWriteBytes;
        private ITickDispatcher _tickDispatcher;
        private AutoResetEvent _blackBoxDumpSignal;
        private Thread _blackBoxDumpThread;
        private string _blackBoxDumpPath;
        private string _blackBoxDumpMirrorPath;
#if UNITY_EDITOR
        private string _csvOverridePath;
        private FileSystemWatcher _csvWatcher;
        private NativeArray<byte> _csvOverrideReadBytes;
#endif
        private JobHandle _stateJobHandle;
        private IDataVault _stateJobPinVault;
        private uint _stateJobPinMask;
        private bool _stateJobPinsHeld;
        private float3 _aupOriginOffset;
        private Matrix4x4 _dearLieGroupMatrix = Matrix4x4.zero;
        private double _localTimeSeconds;
        private double3 _lastPulseOriginAUP;
        private float _updateAccumulatorSeconds;
        private float _overloadHoldSeconds;
        private float _strobeTimerSeconds;
        private float _strobePeak01;
        private float _lastOscillatorComputeTimeMs;
        private float _globalQualityWeight = 1f;
        private float _dearLieBlend01 = 1f;
        private uint _frameCounter;
        private uint _profileSourceHash = ProfileFallbackHash;
        private uint _lastBiomeHash;
        private uint _lastPredatorSignalFrame;
        private int _publishedGlobalStateCount = SyncGroupCount;
        private int _activeSyncPulseCount;
        private int _activeGlowingInstanceCount;
        private int _activeBiolumProfileId;
        private int _blackBoxCursor;
        private int _blackBoxDumpSnapshotCursor;
        private int _blackBoxDumpSnapshotCount;
        private int _blackBoxDumpState;
        private int _blackBoxDumpByteCount;
        private int _blackBoxDumpStopRequested;
        private int _jobOverrunFrames;
        private long _stateJobScheduleTimestamp;
#if UNITY_EDITOR
        private long _csvLastWriteTicks;
        private int _csvWorkerState;
#endif
        private int _lastGlobalDamageSignalSequence;
        private int _lastGlobalLightLevelSignalSequence;
        private int _lastGlobalSurvivalVitalsSequence;
        private byte _pendingTelemetryFlags;
        private bool _registeredUpdate;
        private bool _registeredLateFrame;
        private bool _registeredColdTick;
        private bool _registeredHotSwap;
        private bool _stateJobScheduled;
        private bool _mockGlowsInitialized;
        private bool _disposed;
        private bool _dumpedFault;
        private bool _forceSchedule = true;
        private bool _profilesLoaded;
        private bool _runtimeClaimHeld;
        private bool _vaultBuffersReady;
        private bool _vaultRepairRequested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeClaim()
        {
            DisposeActiveRuntimeForLifecycleTransition();
        }

        private static void DisposeActiveRuntimeForLifecycleTransition()
        {
            BiolumPulseSyncRuntime runtime = s_activeRuntime;
            if (runtime != null)
                runtime.Dispose();

            s_activeRuntime = null;
            Volatile.Write(ref s_runtimeClaimed, 0);
#if UNITY_EDITOR
            s_editorReloadHooked = false;
#endif
        }

#if UNITY_EDITOR
        private static void EnsureEditorReloadHook()
        {
            if (s_editorReloadHooked)
                return;

            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
            EditorApplication.playModeStateChanged -= HandleEditorPlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandleEditorPlayModeStateChanged;
            s_editorReloadHooked = true;
        }

        private static void HandleBeforeAssemblyReload()
        {
            DisposeActiveRuntimeForLifecycleTransition();
        }

        private static void HandleEditorPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode ||
                change == PlayModeStateChange.EnteredEditMode)
            {
                DisposeActiveRuntimeForLifecycleTransition();
            }
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureSceneRuntime()
        {
            if (!Application.isPlaying)
                return;

            if (Volatile.Read(ref s_runtimeClaimed) != 0)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // COLD ALLOC: GameObject[1] - scene-local visual director host when authoring has not placed the component - owner: BIOLUM_PULSE_SYNC
            GameObject host = new GameObject("H8_BiolumPulseSyncRuntime");
            host.hideFlags = HideFlags.DontSave;
            host.AddComponent<BiolumPulseSyncRuntime>();
#endif
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
            if (!Application.isPlaying)
                return;

#if UNITY_EDITOR
            EnsureEditorReloadHook();
#endif
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
            s_activeRuntime = this;
            EnsureVaultBuffers();
            EnsureBlackBoxDumpSnapshot();
            EnsureBlackBoxDumpWorker();
#if UNITY_EDITOR
            EnsureCsvBackgroundWatcher();
#endif
            if (!_profilesLoaded)
                LoadProfilesFromDiskOrDefaults();
            if (!_mockGlowsInitialized)
                GenerateEmergencyMockGlows();
            GenerateMockLightingState();
            TryRegisterColdTick();
            TryRegisterUpdate();
            TryRegisterLateFrame();
            EvaluateColdStartStates();
            UploadShaderGlobals(forceStateArray: true);
            _forceSchedule = true;
        }

        private void OnDisable()
        {
            TryUnregisterLateFrame();
            TryUnregisterUpdate();
            TryUnregisterColdTick();

            TryUnregisterHotSwapListener();
            CompleteScheduledJobForTeardown();
            ClearShaderGlobals();
            bool dumpWorkerStopped = StopBlackBoxDumpWorker();
#if UNITY_EDITOR
            StopCsvBackgroundWatcher();
#endif
            if (dumpWorkerStopped)
                ReleaseVaultHandlesOnly(_dataVault, invalidateProfiles: false);
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
            AdvanceSimulationFrame(deltaTime);
        }

        private void AdvanceSimulationFrame(float deltaTime)
        {
            using (_tickMarker.Auto())
            {
                if (!HasVaultBuffersOrRequestColdRepair())
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
#if UNITY_EDITOR
                ApplyCsvOverridesIfReady();
#endif
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
                if (!HasVaultBuffersOrRequestColdRepair())
                    return;

                if (!CompleteScheduledJobAndPublish())
                    UploadShaderGlobals(forceStateArray: false);
            }
        }

        public void ColdTick()
        {
            if (!_runtimeClaimHeld || _disposed || _dataVault == null)
                return;

            if (!_vaultRepairRequested && HasVaultBuffers())
                return;

            if (_stateJobScheduled)
                return;

            RepairVaultBuffersCold();
        }

        public void Dispose()
        {
            TryUnregisterLateFrame();
            TryUnregisterUpdate();
            TryUnregisterColdTick();
            CompleteScheduledJobForTeardown();
            TryUnregisterHotSwapListener();
            bool dumpWorkerStopped = StopBlackBoxDumpWorker();
            DisposeBlackBoxDumpSnapshot();
            DisposeBlackBoxDumpWriteBytes();
#if UNITY_EDITOR
            StopCsvBackgroundWatcher();
            DisposeCsvOverrideReadBytes();
#endif
            if (dumpWorkerStopped)
                ReleaseVaultHandlesOnly(_dataVault, invalidateProfiles: true);
            if (dumpWorkerStopped)
                _dataVault = null;
            _tickDispatcher = null;
            ReleaseRuntimeOwnerClaim();
            _disposed = dumpWorkerStopped;
        }

        private void ReleaseRuntimeOwnerClaim()
        {
            if (!_runtimeClaimHeld)
                return;

            if (ReferenceEquals(s_activeRuntime, this))
                s_activeRuntime = null;
            _runtimeClaimHeld = false;
            Volatile.Write(ref s_runtimeClaimed, 0);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Copies one live species tuning row from DataVault for editor tooling.
        /// </summary>
        public static bool CopyEditorSpeciesTuning(int index, out BiolumSpeciesTuningDTO tuning)
        {
            tuning = default;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsCompactionFenceActive || index < 0)
                return false;

            if (!TryAcquireBiolumGuard(vault, SpeciesTuningGuardMask))
                return false;

            try
            {
                if (!vault.TryGetGenerationHandle(BufferID.BiolumSpeciesTuning, out VaultGenerationHandle<BiolumSpeciesTuningDTO> handle) ||
                    !TryReadBiolumVaultBuffer(vault, in handle, BufferID.BiolumSpeciesTuning, MaxSpeciesTuningCount, out NativeArray<BiolumSpeciesTuningDTO> species) ||
                    index >= species.Length)
                {
                    return false;
                }

                tuning = species[index];
                return true;
            }
            finally
            {
                ReleaseBiolumGuard(vault, SpeciesTuningGuardMask);
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

            if (!TryAcquireBiolumGuard(vault, SpeciesTuningGuardMask))
                return false;

            try
            {
                if (!vault.TryGetGenerationHandle(BufferID.BiolumSpeciesTuning, out VaultGenerationHandle<BiolumSpeciesTuningDTO> speciesHandle) ||
                    !IsBiolumVaultHandle(in speciesHandle, BufferID.BiolumSpeciesTuning))
                {
                    return false;
                }

                if (!TryResolveBiolumVaultBuffer(
                        vault,
                        in speciesHandle,
                        BufferID.BiolumSpeciesTuning,
                        MaxSpeciesTuningCount,
                        out NativeArray<BiolumSpeciesTuningDTO> species) ||
                    index >= species.Length)
                {
                    return false;
                }

                species[index] = tuning;
            }
            finally
            {
                ReleaseBiolumGuard(vault, SpeciesTuningGuardMask);
            }

            if (!TryAcquireBiolumGuard(vault, GlowStatesGuardMask))
                return true;

            try
            {
                if (vault.TryGetGenerationHandle(BufferID.BiolumGlowStates, out VaultGenerationHandle<GlowStateDTO> glowHandle) &&
                    TryResolveBiolumVaultBuffer(vault, in glowHandle, BufferID.BiolumGlowStates, MaxGlowInstances, out NativeArray<GlowStateDTO> glowStates))
                {
                    ApplySpeciesTuningToGlowStates(glowStates, tuning);
                }
            }
            finally
            {
                ReleaseBiolumGuard(vault, GlowStatesGuardMask);
            }

            return true;
        }

        /// <summary>
        /// Copies the live mock weather row used by the bioluminescence oscillator.
        /// </summary>
        public static bool CopyEditorMockWeather(out MockWeatherSignal signal)
        {
            signal = default;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!TryAcquireBiolumGuard(vault, MockWeatherGuardMask))
                return false;

            try
            {
                if (!vault.TryGetGenerationHandle(BufferID.BiolumMockWeatherSignal, out VaultGenerationHandle<MockWeatherSignal> handle) ||
                    !TryReadBiolumVaultBuffer(vault, in handle, BufferID.BiolumMockWeatherSignal, 1, out NativeArray<MockWeatherSignal> weather))
                {
                    return false;
                }

                signal = weather[0];
                return true;
            }
            finally
            {
                ReleaseBiolumGuard(vault, MockWeatherGuardMask);
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

            if (!TryAcquireBiolumGuard(vault, MockWeatherGuardMask))
                return false;

            VaultGenerationHandle<MockWeatherSignal> handle = default;
            bool writeLocked = false;
            try
            {
                if (!vault.TryGetGenerationHandle(BufferID.BiolumMockWeatherSignal, out handle) ||
                    !IsBiolumVaultHandle(in handle, BufferID.BiolumMockWeatherSignal) ||
                    !vault.TryAcquireWriteLock(in handle, SystemID.Vfx, out NativeArray<MockWeatherSignal> weather))
                {
                    return false;
                }

                writeLocked = true;
                if (!weather.IsCreated || weather.Length <= 0)
                    return false;

                weather[0] = signal;
                return true;
            }
            finally
            {
                if (writeLocked)
                    vault.ReleaseWriteLock(in handle, SystemID.Vfx);
                ReleaseBiolumGuard(vault, MockWeatherGuardMask);
            }
        }

        /// <summary>
        /// Copies the live global pulse matrix row set used by flora shaders.
        /// </summary>
        public static bool CopyEditorPulseState(out BiolumPulseStateDTO pulseState)
        {
            pulseState = default;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!TryAcquireBiolumGuard(vault, PulseStateGuardMask))
                return false;

            try
            {
                if (!vault.TryGetGenerationHandle(BiolumPulseStateBufferId, out VaultGenerationHandle<BiolumPulseStateDTO> handle) ||
                    !TryReadBiolumVaultBuffer(vault, in handle, BiolumPulseStateBufferId, 1, out NativeArray<BiolumPulseStateDTO> pulse))
                {
                    return false;
                }

                pulseState = pulse[0];
                return true;
            }
            finally
            {
                ReleaseBiolumGuard(vault, PulseStateGuardMask);
            }
        }

        /// <summary>
        /// Copies newest black-box telemetry entries for editor tooling. Element 0 is the newest completed sample.
        /// </summary>
        public static int CopyEditorTelemetryEntries(Span<BiolumPulseTelemetryEntry> destination)
        {
            if (destination.Length <= 0)
                return 0;

            IDataVault vault = GlobalRegistry.DataVault;
            BiolumPulseSyncRuntime runtime = s_activeRuntime;
            if (vault == null || runtime == null || vault.IsCompactionFenceActive)
                return 0;

            if (!TryAcquireBiolumGuard(vault, BlackBoxGuardMask))
                return 0;

            try
            {
                if (!vault.TryGetGenerationHandle(BufferID.BiolumBlackBox, out VaultGenerationHandle<BiolumPulseTelemetryEntry> handle) ||
                    !TryReadBiolumVaultBuffer(vault, in handle, BufferID.BiolumBlackBox, BlackBoxFrameCount, out NativeArray<BiolumPulseTelemetryEntry> blackBox))
                {
                    return 0;
                }

                int sourceCount = blackBox.Length;
                int ringCount = math.min(sourceCount, BlackBoxFrameCount);
                int copyCount = math.min(destination.Length, ringCount);
                int cursor = math.clamp(runtime._blackBoxCursor, 0, sourceCount - 1);
                for (int i = 0; i < copyCount; i++)
                {
                    int index = cursor - i - 1;
                    if (index < 0)
                        index += sourceCount;

                    destination[i] = blackBox[index];
                }

                return copyCount;
            }
            finally
            {
                ReleaseBiolumGuard(vault, BlackBoxGuardMask);
            }
        }

        /// <summary>
        /// Writes the live global pulse matrix row set used by flora shaders.
        /// </summary>
        public static bool TryWriteEditorPulseState(in BiolumPulseStateDTO pulseState)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!TryAcquireBiolumGuard(vault, PulseStateGuardMask))
                return false;

            VaultGenerationHandle<BiolumPulseStateDTO> handle = default;
            bool writeLocked = false;
            try
            {
                if (!vault.TryGetGenerationHandle(BiolumPulseStateBufferId, out handle) ||
                    !IsBiolumVaultHandle(in handle, BiolumPulseStateBufferId) ||
                    !vault.TryAcquireWriteLock(in handle, SystemID.Vfx, out NativeArray<BiolumPulseStateDTO> pulse))
                {
                    return false;
                }

                writeLocked = true;
                if (!pulse.IsCreated || pulse.Length <= 0)
                    return false;

                pulse[0] = pulseState;
                return true;
            }
            finally
            {
                if (writeLocked)
                    vault.ReleaseWriteLock(in handle, SystemID.Vfx);
                ReleaseBiolumGuard(vault, PulseStateGuardMask);
            }
        }

        /// <summary>
        /// Copies cold/editable global oscillator controls from Vault profile memory.
        /// </summary>
        public static bool CopyEditorPulseControls(
            out float baseFrequency,
            out float spatialOffsetMultiplier,
            out float darknessActivationThreshold,
            out float predatorPanicSpeed)
        {
            baseFrequency = 0.45f;
            spatialOffsetMultiplier = 1f;
            darknessActivationThreshold = DefaultDarknessActivationThreshold;
            predatorPanicSpeed = DefaultPredatorPanicSpeed;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!TryAcquireBiolumGuard(vault, ProfileFloatsGuardMask))
                return false;

            try
            {
                if (!vault.TryGetGenerationHandle(BufferID.BiolumProfileFloats, out VaultGenerationHandle<float> handle) ||
                    !TryReadBiolumVaultBuffer(vault, in handle, BufferID.BiolumProfileFloats, ProfileFloatCount, out NativeArray<float> profileFloats))
                {
                    return false;
                }

                baseFrequency = math.clamp(profileFloats[1], 0.0025f, 8f);
                float baseOffset = math.max(0.0001f, 0.18f);
                spatialOffsetMultiplier = math.clamp(profileFloats[3] / baseOffset, 0f, 8f);
                darknessActivationThreshold = ResolveDarknessActivationThreshold(profileFloats);
                predatorPanicSpeed = ResolvePredatorPanicSpeed(profileFloats);
                return true;
            }
            finally
            {
                ReleaseBiolumGuard(vault, ProfileFloatsGuardMask);
            }
        }

        /// <summary>
        /// Writes cold/editable global oscillator controls into Vault profile and pulse memory.
        /// </summary>
        public static bool TryWriteEditorPulseControls(
            float baseFrequency,
            float spatialOffsetMultiplier,
            float darknessActivationThreshold,
            float predatorPanicSpeed)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            float frequency = math.clamp(baseFrequency, 0.0025f, 8f);
            float offsetMultiplier = math.clamp(spatialOffsetMultiplier, 0f, 8f);
            float threshold = math.saturate(darknessActivationThreshold);
            float panicEncoded = math.saturate((math.clamp(predatorPanicSpeed, 1f, 4f) - 1f) / 3f);

            if (!TryAcquireBiolumGuard(vault, ProfileFloatsGuardMask))
                return false;

            try
            {
                if (!vault.TryGetGenerationHandle(BufferID.BiolumProfileFloats, out VaultGenerationHandle<float> profileHandle) ||
                    !IsBiolumVaultHandle(in profileHandle, BufferID.BiolumProfileFloats))
                {
                    return false;
                }

                if (!TryResolveBiolumVaultBuffer(vault, in profileHandle, BufferID.BiolumProfileFloats, ProfileFloatCount, out NativeArray<float> profileFloats))
                {
                    return false;
                }

                for (int i = 0; i < SyncGroupCount; i++)
                {
                    int offset = i * ProfileFloatStride;
                    float groupFrequency = math.clamp(frequency * (1f + i * 0.13f), 0.0025f, 8f);
                    float groupSpatialOffset = math.clamp((0.18f + i * 0.07f) * offsetMultiplier, 0f, 4f);
                    profileFloats[offset + 1] = groupFrequency;
                    profileFloats[offset + 3] = groupSpatialOffset;
                    profileFloats[offset + 4] = threshold;
                    profileFloats[offset + 5] = panicEncoded;
                }
            }
            finally
            {
                ReleaseBiolumGuard(vault, ProfileFloatsGuardMask);
            }

            if (!TryAcquireBiolumGuard(vault, PulseStateGuardMask))
                return false;

            try
            {
                if (!vault.TryGetGenerationHandle(BiolumPulseStateBufferId, out VaultGenerationHandle<BiolumPulseStateDTO> pulseHandle) ||
                    !IsBiolumVaultHandle(in pulseHandle, BiolumPulseStateBufferId) ||
                    !TryResolveBiolumVaultBuffer(vault, in pulseHandle, BiolumPulseStateBufferId, 1, out NativeArray<BiolumPulseStateDTO> pulseState))
                {
                    return false;
                }

                BiolumPulseStateDTO state = pulseState[0];
                for (int i = 0; i < SyncGroupCount; i++)
                {
                    float groupFrequency = math.clamp(frequency * (1f + i * 0.13f), 0.0025f, 8f);
                    float groupSpatialOffset = math.clamp((0.18f + i * 0.07f) * offsetMultiplier, 0f, 4f);
                    float4 row = GetPulseGroup(in state, i);
                    row.y = groupFrequency;
                    row.w = groupSpatialOffset;
                    SetPulseGroup(ref state, i, row);
                }

                pulseState[0] = state;
                return true;
            }
            finally
            {
                ReleaseBiolumGuard(vault, PulseStateGuardMask);
            }
        }

        /// <summary>
        /// Pushes an editor-triggered pulse directly into one matrix row.
        /// </summary>
        public static bool TryTriggerEditorGlobalPulse(double3 originAUP, float waveSpeed, uint colorOverride)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            BiolumPulseSyncRuntime runtime = s_activeRuntime;
            if (runtime == null)
                return false;

            double3 aupReference = new double3(runtime._aupOriginOffset.x, runtime._aupOriginOffset.y, runtime._aupOriginOffset.z);
            double3 localAup = AupPrecisionMath.LocalDeltaDouble(originAUP, aupReference);
            float3 local = AupPrecisionMath.DowncastLocalDelta(localAup, float3.zero);
            float x = math.isfinite(local.x) ? local.x : 0f;
            float y = math.isfinite(local.y) ? local.y : 0f;
            float z = math.isfinite(local.z) ? local.z : 0f;
            uint hash = DeterministicHash(
                math.asuint(x) ^
                (math.asuint(y) * 0x9E3779B9u) ^
                (math.asuint(z) * 0x85EBCA6Bu));
            int group = (int)(hash & (SyncGroupCount - 1));
            int offset = group * ProfileFloatStride;
            float speed = math.clamp(math.isfinite(waveSpeed) ? waveSpeed : DefaultPingRadiusMeters, 1f, 180f);
            float alpha = ((colorOverride >> 30) & 3u) * (1f / 3f);
            float frequency = math.clamp(speed * 0.0125f, 0.0025f, 8f);
            float amplitude = math.clamp(0.85f + alpha * 0.5f, 0f, MaxHdrIntensity);
            float spatialOffset = math.clamp(math.length(new float3(x, y, z)) * 0.0007f + speed * 0.006f, 0.05f, 4f);

            if (!TryAcquireBiolumGuard(vault, PulseStateGuardMask))
                return false;

            try
            {
                if (!vault.TryGetGenerationHandle(BiolumPulseStateBufferId, out VaultGenerationHandle<BiolumPulseStateDTO> pulseHandle) ||
                    !IsBiolumVaultHandle(in pulseHandle, BiolumPulseStateBufferId))
                {
                    return false;
                }

                if (!TryResolveBiolumVaultBuffer(vault, in pulseHandle, BiolumPulseStateBufferId, 1, out NativeArray<BiolumPulseStateDTO> pulseState))
                {
                    return false;
                }

                BiolumPulseStateDTO state = pulseState[0];
                float4 row = GetPulseGroup(in state, group);
                row.x = RepeatRadians(row.x + math.PI * 0.5f + ((hash >> 8) & 255u) * (math.PI / 255f));
                row.y = frequency;
                row.z = math.max(math.clamp(row.z, 0f, MaxHdrIntensity), amplitude);
                row.w = spatialOffset;
                SetPulseGroup(ref state, group, row);
                pulseState[0] = state;
            }
            finally
            {
                ReleaseBiolumGuard(vault, PulseStateGuardMask);
            }

            if (!TryAcquireBiolumGuard(vault, ProfileFloatsGuardMask))
                return false;

            try
            {
                if (!vault.TryGetGenerationHandle(BufferID.BiolumProfileFloats, out VaultGenerationHandle<float> profileHandle) ||
                    !IsBiolumVaultHandle(in profileHandle, BufferID.BiolumProfileFloats) ||
                    !TryResolveBiolumVaultBuffer(vault, in profileHandle, BufferID.BiolumProfileFloats, ProfileFloatCount, out NativeArray<float> profileFloats))
                {
                    return false;
                }

                profileFloats[offset + 1] = frequency;
                profileFloats[offset + 2] = math.max(math.clamp(profileFloats[offset + 2], 0f, MaxHdrIntensity), amplitude);
                profileFloats[offset + 3] = spatialOffset;
            }
            finally
            {
                ReleaseBiolumGuard(vault, ProfileFloatsGuardMask);
            }

            runtime._activeBiolumProfileId = group;
            runtime._strobeTimerSeconds = StrobeDurationSeconds;
            runtime._strobePeak01 = math.max(runtime._strobePeak01, math.saturate(amplitude));
            runtime._forceSchedule = true;

            return true;
        }
#endif

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
            if (!TryFinalizeScheduledJobForEditorReload())
                return;

            RefreshCachedRegistryServices();
            EnsureVaultBuffers();
            _profilesLoaded = false;
            LoadProfilesFromDiskOrDefaults();
            EvaluateColdStartStates();
            UploadShaderGlobals(forceStateArray: true);
            _forceSchedule = true;
        }

        private bool TryFinalizeScheduledJobForEditorReload()
        {
            if (!_stateJobScheduled)
                return true;

            if (!_stateJobHandle.IsCompleted)
            {
                _pendingTelemetryFlags |= TelemetryFlagJobOverrun;
                return false;
            }

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _stateJobHandle))
                return false;

            _stateJobScheduled = false;
            _jobOverrunFrames = 0;
            bool finite;
            try
            {
                finite = CopyPulseStateToManagedBuffer();
            }
            finally
            {
                ReleaseStateJobBufferPins();
            }

            if (finite)
                return true;

            _pendingTelemetryFlags |= TelemetryFlagNonFinite;
            DumpBlackBox(TelemetryFlagNonFinite);
            return true;
        }
#endif

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame)
                return;
            if (_tickDispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryRegisterUpdate()
        {
            if (_registeredUpdate)
                return;
            if (_tickDispatcher == null)
                return;

            _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryRegisterColdTick()
        {
            if (_registeredColdTick)
                return;
            if (_tickDispatcher == null)
                return;

            _registeredColdTick = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrame = false;
        }

        private void TryUnregisterUpdate()
        {
            if (!_registeredUpdate)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registeredUpdate = false;
        }

        private void TryUnregisterColdTick()
        {
            if (!_registeredColdTick)
                return;

            GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Environment);
            _registeredColdTick = false;
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
            ApplyBiolumRegistryServiceRebind(serviceSlot, currentService);
        }

        void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            ApplyBiolumRegistryServiceRebind(serviceSlot, currentService);
        }

        private void RefreshCachedRegistryServices()
        {
            ApplyBiolumRegistryServiceRebind(GlobalRegistryServiceSlot.Dispatcher, GlobalRegistry.TickDispatcher);
            ApplyBiolumRegistryServiceRebind(GlobalRegistryServiceSlot.DataVault, GlobalRegistry.DataVault);
        }

        private void ApplyBiolumRegistryServiceRebind(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    ITickDispatcher tickDispatcher = currentService as ITickDispatcher;
                    if (!ReferenceEquals(_tickDispatcher, tickDispatcher))
                    {
                        TryUnregisterLateFrame();
                        TryUnregisterUpdate();
                        TryUnregisterColdTick();
                        _tickDispatcher = tickDispatcher;
                    }

                    if (_tickDispatcher != null)
                    {
                        TryRegisterColdTick();
                        TryRegisterUpdate();
                        TryRegisterLateFrame();
                    }
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    BindDataVault(currentService as IDataVault);
                    break;
            }
        }

        private void BindDataVault(IDataVault currentVault)
        {
            if (!ReferenceEquals(_dataVault, currentVault))
            {
                FenceScheduledJobBeforeVaultHandleInvalidation();
                if (!StopBlackBoxDumpWorker())
                    return;

                IDataVault previousVault = _dataVault;
                ReleaseVaultHandlesOnly(previousVault, invalidateProfiles: true);
                _dataVault = currentVault;
                if (currentVault != null && _runtimeClaimHeld)
                {
                    EnsureVaultBuffers();
                    EnsureBlackBoxDumpWorker();
                }
            }
        }

        private void FenceScheduledJobBeforeVaultHandleInvalidation()
        {
            if (_stateJobScheduled)
                CompleteScheduledJobForTeardown();
        }

        private bool EnsureVaultBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
            {
                _vaultBuffersReady = false;
                return false;
            }

            VaultGenerationHandle<float> previousProfiles = _profileFloatsHandle;
            VaultGenerationHandle<GlowStateDTO> previousGlowStates = _glowStatesHandle;
            VaultGenerationHandle<double3> previousGlowAup = _glowAupOriginsHandle;
            VaultGenerationHandle<BiolumSpeciesTuningDTO> previousSpecies = _speciesTuningHandle;
            bool wasReady = _vaultBuffersReady;

            if (!EnsureBiolumVaultBuffer(vault, ref _profileFloatsHandle, BufferID.BiolumProfileFloats, ProfileFloatCount, NativeArrayOptions.ClearMemory) ||
                !EnsureBiolumVaultBuffer(vault, ref _pulseStateHandle, BiolumPulseStateBufferId, 1, NativeArrayOptions.UninitializedMemory) ||
                !EnsureBiolumVaultBuffer(vault, ref _blackBoxHandle, BufferID.BiolumBlackBox, BlackBoxFrameCount, NativeArrayOptions.ClearMemory) ||
                !EnsureBiolumVaultBuffer(vault, ref _glowStatesHandle, BufferID.BiolumGlowStates, MaxGlowInstances, NativeArrayOptions.UninitializedMemory) ||
                !EnsureBiolumVaultBuffer(vault, ref _glowAupOriginsHandle, BufferID.BiolumGlowAupOrigins, MaxGlowInstances, NativeArrayOptions.UninitializedMemory) ||
                !EnsureBiolumVaultBuffer(vault, ref _syncPulsesHandle, BufferID.BiolumSyncPulses, SyncPulseCapacity, NativeArrayOptions.ClearMemory) ||
                !EnsureBiolumVaultBuffer(vault, ref _syncPulseAgesHandle, BufferID.BiolumSyncPulseAges, SyncPulseCapacity, NativeArrayOptions.ClearMemory) ||
                !EnsureBiolumVaultBuffer(vault, ref _mockWeatherSignalHandle, BufferID.BiolumMockWeatherSignal, 1, NativeArrayOptions.ClearMemory) ||
                !EnsureBiolumVaultBuffer(vault, ref _mockPredatorSignalHandle, BufferID.BiolumMockPredatorSignal, 1, NativeArrayOptions.ClearMemory) ||
                !EnsureBiolumVaultBuffer(vault, ref _mockDamageSignalHandle, BufferID.BiolumMockDamageSignal, 1, NativeArrayOptions.ClearMemory) ||
                !EnsureBiolumVaultBuffer(vault, ref _speciesTuningHandle, BufferID.BiolumSpeciesTuning, MaxSpeciesTuningCount, NativeArrayOptions.UninitializedMemory) ||
                !EnsureBiolumVaultBuffer(vault, ref _blackBoxDumpScratchHandle, BiolumBlackBoxDumpScratchBufferId, BlackBoxDumpByteCount, NativeArrayOptions.UninitializedMemory))
            {
                _vaultBuffersReady = false;
                return false;
            }

            bool hadProfiles = wasReady && SameVaultHandle(in previousProfiles, in _profileFloatsHandle);
            bool hadGlowStates = wasReady && SameVaultHandle(in previousGlowStates, in _glowStatesHandle);
            bool hadGlowAup = wasReady && SameVaultHandle(in previousGlowAup, in _glowAupOriginsHandle);
            bool hadSpecies = wasReady && SameVaultHandle(in previousSpecies, in _speciesTuningHandle);

            if (!hadProfiles)
                _profilesLoaded = false;
            if (!hadGlowStates || !hadGlowAup || !hadSpecies)
                _mockGlowsInitialized = false;
            if (_runtimeClaimHeld)
                EnsureBlackBoxDumpWorker();

            _vaultBuffersReady = true;
            _vaultRepairRequested = false;
            return true;
        }

        private bool HasVaultBuffers()
        {
            IDataVault vault = _dataVault;
            return _vaultBuffersReady &&
                   vault != null &&
                   !vault.IsCompactionFenceActive;
        }

        private bool HasVaultBuffersOrRequestColdRepair()
        {
            if (HasVaultBuffers())
                return true;

            _vaultRepairRequested = true;
            return false;
        }

        private void RepairVaultBuffersCold()
        {
            if (!EnsureVaultBuffers())
            {
                _vaultRepairRequested = true;
                return;
            }

            EnsureBlackBoxDumpWorker();

            if (!_profilesLoaded)
                LoadProfilesFromDiskOrDefaults();

            if (!_mockGlowsInitialized)
                GenerateEmergencyMockGlows();

            GenerateMockLightingState();
            EvaluateColdStartStates();
            _forceSchedule = true;
            _vaultRepairRequested = false;
        }

        private void ReleaseVaultHandlesOnly(IDataVault vault, bool invalidateProfiles)
        {
            _vaultBuffersReady = false;
            ReleaseBiolumVaultHandle(vault, ref _profileFloatsHandle, BufferID.BiolumProfileFloats);
            ReleaseBiolumVaultHandle(vault, ref _pulseStateHandle, BiolumPulseStateBufferId);
            ReleaseBiolumVaultHandle(vault, ref _blackBoxHandle, BufferID.BiolumBlackBox);
            ReleaseBiolumVaultHandle(vault, ref _glowStatesHandle, BufferID.BiolumGlowStates);
            ReleaseBiolumVaultHandle(vault, ref _glowAupOriginsHandle, BufferID.BiolumGlowAupOrigins);
            ReleaseBiolumVaultHandle(vault, ref _syncPulsesHandle, BufferID.BiolumSyncPulses);
            ReleaseBiolumVaultHandle(vault, ref _syncPulseAgesHandle, BufferID.BiolumSyncPulseAges);
            ReleaseBiolumVaultHandle(vault, ref _mockWeatherSignalHandle, BufferID.BiolumMockWeatherSignal);
            ReleaseBiolumVaultHandle(vault, ref _mockPredatorSignalHandle, BufferID.BiolumMockPredatorSignal);
            ReleaseBiolumVaultHandle(vault, ref _mockDamageSignalHandle, BufferID.BiolumMockDamageSignal);
            ReleaseBiolumVaultHandle(vault, ref _speciesTuningHandle, BufferID.BiolumSpeciesTuning);
            ReleaseBiolumVaultHandle(vault, ref _blackBoxDumpScratchHandle, BiolumBlackBoxDumpScratchBufferId);
            _mockGlowsInitialized = false;
            _activeGlowingInstanceCount = 0;
            if (invalidateProfiles)
                _profilesLoaded = false;
        }

        private static bool EnsureBiolumVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : struct
        {
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (TryResolveBiolumVaultBufferGuarded(vault, in handle, bufferId, requiredLength))
                return true;

            if (vault.IsAllocationLocked)
                return false;

            VaultGenerationHandle<T> acquired = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, SystemID.Vfx, options);
            if (TryResolveBiolumVaultBufferGuarded(vault, in acquired, bufferId, requiredLength))
            {
                handle = acquired;
                return true;
            }

            handle = default;
            return false;
        }

        private static bool TryResolveBiolumVaultBufferGuarded<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            ulong guardMask = BiolumMutationGuardBit(bufferId);
            if (!TryAcquireBiolumGuard(vault, guardMask))
                return false;

            try
            {
                return TryResolveBiolumVaultBuffer(vault, in handle, bufferId, requiredLength, out _);
            }
            finally
            {
                ReleaseBiolumGuard(vault, guardMask);
            }
        }

        private static bool TryResolveBiolumVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   requiredLength > 0 &&
                   IsBiolumVaultHandle(in handle, bufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryReadBiolumVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   requiredLength > 0 &&
                   IsBiolumVaultHandle(in handle, bufferId) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool IsBiolumVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)SystemID.Vfx &&
                   handle.Generation != 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SameVaultHandle<T>(
            in VaultGenerationHandle<T> left,
            in VaultGenerationHandle<T> right) where T : struct
        {
            return left.BufferID == right.BufferID &&
                   left.SystemID == right.SystemID &&
                   left.Generation == right.Generation &&
                   left.Flags == right.Flags;
        }

        private static void ReleaseBiolumVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            if (vault != null && IsBiolumVaultHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static ulong BiolumMutationGuardBit(BufferID bufferId)
        {
            return 1UL << ((int)bufferId & 63);
        }

        private static bool TryAcquireBiolumGuard(IDataVault vault, ulong guardMask)
        {
            return vault != null &&
                   guardMask != 0UL &&
                   !vault.IsCompactionFenceActive &&
                   vault.TryAcquireMutationGuard(guardMask);
        }

        private static void ReleaseBiolumGuard(IDataVault vault, ulong guardMask)
        {
            if (vault != null && guardMask != 0UL)
                vault.ReleaseMutationGuard(guardMask);
        }

        private static bool AreSyncLayoutsValid()
        {
            if (UnsafeUtility.SizeOf<GlowStateDTO>() != 16)
                return ReportInvalidSyncLayout("GlowStateDTO must remain 16 bytes.");

            if (UnsafeUtility.SizeOf<SyncPulseDTO>() != 32)
                return ReportInvalidSyncLayout("SyncPulseDTO must remain 32 bytes.");

            if (UnsafeUtility.SizeOf<MockWeatherSignal>() != 16)
                return ReportInvalidSyncLayout("MockWeatherSignal must remain 16 bytes.");

            if (UnsafeUtility.SizeOf<BiolumPulseStateDTO>() != 64)
                return ReportInvalidSyncLayout("BiolumPulseStateDTO must remain 64 bytes.");

            if (GetFieldOffset<BiolumPulseStateDTO>(nameof(BiolumPulseStateDTO.Group1_Params)) != 0 ||
                GetFieldOffset<BiolumPulseStateDTO>(nameof(BiolumPulseStateDTO.Group2_Params)) != 16 ||
                GetFieldOffset<BiolumPulseStateDTO>(nameof(BiolumPulseStateDTO.Group3_Params)) != 32 ||
                GetFieldOffset<BiolumPulseStateDTO>(nameof(BiolumPulseStateDTO.Group4_Params)) != 48)
            {
                return ReportInvalidSyncLayout("BiolumPulseStateDTO float4 rows must remain at 0/16/32/48.");
            }

            if (UnsafeUtility.SizeOf<BiolumSpeciesTuningDTO>() != 24)
                return ReportInvalidSyncLayout("BiolumSpeciesTuningDTO must remain 24 bytes.");

            if (UnsafeUtility.SizeOf<BiolumMockPredatorProximitySignal>() != 64)
                return ReportInvalidSyncLayout("BiolumMockPredatorProximitySignal must remain 64 bytes.");

            if (UnsafeUtility.SizeOf<BiolumMockCombatDamageSignal>() != 64)
                return ReportInvalidSyncLayout("BiolumMockCombatDamageSignal must remain 64 bytes.");

            if (UnsafeUtility.SizeOf<BiolumPulseTelemetryEntry>() != 64)
                return ReportInvalidSyncLayout("BiolumPulseTelemetryEntry must remain 64 bytes.");

            if (UnsafeUtility.SizeOf<BiolumPulseDumpHeader>() != 16)
                return ReportInvalidSyncLayout("BiolumPulseDumpHeader must remain 16 bytes.");

            return true;
        }

        private static int GetFieldOffset<T>(string fieldName) where T : struct
        {
            System.Reflection.FieldInfo field = typeof(T).GetField(fieldName);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }

        private static bool ReportInvalidSyncLayout(string message)
        {
            Hecton8.Core.H8Debug.LogError(message);
            return false;
        }

        private void GenerateEmergencyMockGlows()
        {
            IDataVault vault = _dataVault;
            if (vault == null || !HasVaultBuffers())
                return;

            if (!SeedEmergencySpeciesTuning(vault) ||
                !SeedEmergencyMockWeather(vault) ||
                !WriteMockLightingPredatorReset(vault) ||
                !ResetEmergencyMockDamage(vault) ||
                !ResetEmergencySyncPulses(vault) ||
                !TryResolveEmergencyGlowCapacity(vault, out int activeCount) ||
                !SeedEmergencyGlowStates(vault, activeCount) ||
                !SeedEmergencyGlowAupOrigins(vault, activeCount))
            {
                return;
            }

            _activeSyncPulseCount = 0;
            _activeGlowingInstanceCount = activeCount;
            _mockGlowsInitialized = true;
        }

        private bool SeedEmergencySpeciesTuning(IDataVault vault)
        {
            if (!TryAcquireBiolumGuard(vault, SpeciesTuningGuardMask))
                return false;

            try
            {
                if (!TryResolveBiolumVaultBuffer(vault, in _speciesTuningHandle, BufferID.BiolumSpeciesTuning, MaxSpeciesTuningCount, out NativeArray<BiolumSpeciesTuningDTO> speciesTuning))
                    return false;

                SeedSpeciesTuning(speciesTuning);
                return true;
            }
            finally
            {
                ReleaseBiolumGuard(vault, SpeciesTuningGuardMask);
            }
        }

        private bool SeedEmergencyMockWeather(IDataVault vault)
        {
            if (!TryAcquireBiolumGuard(vault, MockWeatherGuardMask))
                return false;

            try
            {
                if (!TryResolveBiolumVaultBuffer(vault, in _mockWeatherSignalHandle, BufferID.BiolumMockWeatherSignal, 1, out NativeArray<MockWeatherSignal> weatherSignal))
                    return false;

                weatherSignal[0] = new MockWeatherSignal
                {
                    AmbientLightLevel = 0.08f,
                    O2Level01 = 1f,
                    SystemHealthIndex01 = 0.25f,
                    CurrentBiomeHash = EmergencyAbyssNeonHash
                };
                return true;
            }
            finally
            {
                ReleaseBiolumGuard(vault, MockWeatherGuardMask);
            }
        }

        private bool ResetEmergencyMockDamage(IDataVault vault)
        {
            if (!TryAcquireBiolumGuard(vault, MockDamageGuardMask))
                return false;

            try
            {
                if (!TryResolveBiolumVaultBuffer(vault, in _mockDamageSignalHandle, BufferID.BiolumMockDamageSignal, 1, out NativeArray<BiolumMockCombatDamageSignal> damageSignal))
                    return false;

                damageSignal[0] = default;
                return true;
            }
            finally
            {
                ReleaseBiolumGuard(vault, MockDamageGuardMask);
            }
        }

        private bool ResetEmergencySyncPulses(IDataVault vault)
        {
            if (!TryAcquireBiolumGuard(vault, SyncPulseGuardMask))
                return false;

            try
            {
                if (!TryResolveBiolumVaultBuffer(vault, in _syncPulsesHandle, BufferID.BiolumSyncPulses, SyncPulseCapacity, out NativeArray<SyncPulseDTO> pulses) ||
                    !TryResolveBiolumVaultBuffer(vault, in _syncPulseAgesHandle, BufferID.BiolumSyncPulseAges, SyncPulseCapacity, out NativeArray<float> pulseAges))
                {
                    return false;
                }

                int count = math.min(SyncPulseCapacity, math.min(pulses.Length, pulseAges.Length));
                for (int i = 0; i < count; i++)
                {
                    pulses[i] = default;
                    pulseAges[i] = 99f;
                }

                return true;
            }
            finally
            {
                ReleaseBiolumGuard(vault, SyncPulseGuardMask);
            }
        }

        private bool TryResolveEmergencyGlowCapacity(IDataVault vault, out int activeCount)
        {
            activeCount = 0;

            if (!TryAcquireBiolumGuard(vault, GlowStatesGuardMask))
                return false;

            try
            {
                if (!TryResolveBiolumVaultBuffer(vault, in _glowStatesHandle, BufferID.BiolumGlowStates, MaxGlowInstances, out NativeArray<GlowStateDTO> glowStates))
                    return false;

                activeCount = math.min(MaxGlowInstances, glowStates.Length);
            }
            finally
            {
                ReleaseBiolumGuard(vault, GlowStatesGuardMask);
            }

            if (activeCount <= 0)
                return false;

            if (!TryAcquireBiolumGuard(vault, GlowAupOriginsGuardMask))
            {
                activeCount = 0;
                return false;
            }

            try
            {
                if (!TryResolveBiolumVaultBuffer(vault, in _glowAupOriginsHandle, BufferID.BiolumGlowAupOrigins, MaxGlowInstances, out NativeArray<double3> aupOrigins))
                {
                    activeCount = 0;
                    return false;
                }

                activeCount = math.min(activeCount, aupOrigins.Length);
                return activeCount > 0;
            }
            finally
            {
                ReleaseBiolumGuard(vault, GlowAupOriginsGuardMask);
            }
        }

        private bool SeedEmergencyGlowStates(IDataVault vault, int activeCount)
        {
            if (activeCount <= 0 || !TryAcquireBiolumGuard(vault, GlowStatesGuardMask))
                return false;

            try
            {
                if (!TryResolveBiolumVaultBuffer(vault, in _glowStatesHandle, BufferID.BiolumGlowStates, MaxGlowInstances, out NativeArray<GlowStateDTO> glowStates))
                    return false;

                int count = math.min(activeCount, glowStates.Length);
                for (int i = 0; i < count; i++)
                {
                    int speciesIndex = i % MaxSpeciesTuningCount;
                    BiolumSpeciesTuningDTO species = BuildEmergencySpeciesTuning(speciesIndex);
                    float phase = math.frac((i * 0.754877666f) + (speciesIndex * 0.037f));
                    GlowStateDTO state = glowStates[i];
                    state.PackedColor = species.PackedColor;
                    state.Phase = phase;
                    state.Frequency = species.Frequency;
                    state.SpeciesHash = species.SpeciesHash;
                    glowStates[i] = state;
                }

                return count > 0;
            }
            finally
            {
                ReleaseBiolumGuard(vault, GlowStatesGuardMask);
            }
        }

        private bool SeedEmergencyGlowAupOrigins(IDataVault vault, int activeCount)
        {
            if (activeCount <= 0 || !TryAcquireBiolumGuard(vault, GlowAupOriginsGuardMask))
                return false;

            try
            {
                if (!TryResolveBiolumVaultBuffer(vault, in _glowAupOriginsHandle, BufferID.BiolumGlowAupOrigins, MaxGlowInstances, out NativeArray<double3> aupOrigins))
                    return false;

                int count = math.min(activeCount, aupOrigins.Length);
                for (int i = 0; i < count; i++)
                {
                    int speciesIndex = i % MaxSpeciesTuningCount;
                    int x = i % 250;
                    int z = i / 250;
                    double jitterX = ((int)(DeterministicHash((uint)i) & 1023u) - 512) * 0.00625;
                    double jitterZ = ((int)((DeterministicHash((uint)i ^ 0xA341316Cu) >> 10) & 1023u) - 512) * 0.00625;
                    aupOrigins[i] = new double3((x - 125) * 1.35 + jitterX, -220.0 - (speciesIndex & 7) * 0.75, (z - 100) * 1.35 + jitterZ);
                }

                return count > 0;
            }
            finally
            {
                ReleaseBiolumGuard(vault, GlowAupOriginsGuardMask);
            }
        }

        private void GenerateMockLightingState()
        {
            IDataVault vault = _dataVault;
            if (vault == null || !HasVaultBuffers())
                return;

            if (!WriteMockLightingWeatherSeed(vault, out MockWeatherSignal weather) ||
                !WriteMockLightingPredatorReset(vault) ||
                !TryReadMockLightingProfileDefaults(
                    vault,
                    out BiolumPulseStateDTO profileDefaults,
                    out float darknessActivationThreshold))
            {
                return;
            }

            BiolumPulseStateDTO generatedState = BuildMockLightingPulseState(
                in weather,
                in profileDefaults,
                darknessActivationThreshold);

            if (!TryWriteMockLightingPulseState(vault, in generatedState))
                return;

            CopyPulseStateToManagedBuffer(in generatedState);
        }

        private bool WriteMockLightingWeatherSeed(IDataVault vault, out MockWeatherSignal weather)
        {
            weather = default;
            if (!TryAcquireBiolumGuard(vault, MockWeatherGuardMask))
                return false;

            try
            {
                if (!TryResolveBiolumVaultBuffer(vault, in _mockWeatherSignalHandle, BufferID.BiolumMockWeatherSignal, 1, out NativeArray<MockWeatherSignal> weatherSignal))
                {
                    return false;
                }

                weather = weatherSignal[0];
                if (!math.isfinite(weather.O2Level01) || weather.O2Level01 <= 0f)
                    weather.O2Level01 = 1f;
                if (!math.isfinite(weather.SystemHealthIndex01) || weather.SystemHealthIndex01 <= 0f)
                    weather.SystemHealthIndex01 = 0.25f;
                if (!math.isfinite(weather.AmbientLightLevel))
                    weather.AmbientLightLevel = 0.08f;

                weatherSignal[0] = weather;
                return true;
            }
            finally
            {
                ReleaseBiolumGuard(vault, MockWeatherGuardMask);
            }
        }

        private bool WriteMockLightingPredatorReset(IDataVault vault)
        {
            if (!TryAcquireBiolumGuard(vault, MockPredatorGuardMask))
                return false;

            try
            {
                if (!TryResolveBiolumVaultBuffer(vault, in _mockPredatorSignalHandle, BufferID.BiolumMockPredatorSignal, 1, out NativeArray<BiolumMockPredatorProximitySignal> predator))
                    return false;

                predator[0] = default;
                return true;
            }
            finally
            {
                ReleaseBiolumGuard(vault, MockPredatorGuardMask);
            }
        }

        private bool TryReadMockLightingProfileDefaults(
            IDataVault vault,
            out BiolumPulseStateDTO profileDefaults,
            out float darknessActivationThreshold)
        {
            profileDefaults = default;
            darknessActivationThreshold = DefaultDarknessActivationThreshold;
            if (!TryAcquireBiolumGuard(vault, ProfileFloatsGuardMask))
                return false;

            try
            {
                if (!TryResolveBiolumVaultBuffer(vault, in _profileFloatsHandle, BufferID.BiolumProfileFloats, ProfileFloatCount, out NativeArray<float> profileFloats))
                    return false;

                darknessActivationThreshold = ResolveDarknessActivationThreshold(profileFloats);
                for (int i = 0; i < SyncGroupCount; i++)
                {
                    float phase = ResolveProfilePhase(ReadPulseProfileValue(profileFloats, i, 0, i * 0.25f));
                    float frequency = math.clamp(ReadPulseProfileValue(profileFloats, i, 1, 0.45f + i * 0.11f), 0.0025f, 8f);
                    float amplitude = ReadPulseProfileValue(profileFloats, i, 2, 0.58f + i * 0.08f);
                    float spatialOffset = ReadPulseProfileValue(profileFloats, i, 3, 0.18f + i * 0.07f);
                    SetPulseGroup(ref profileDefaults, i, new float4(phase, frequency, amplitude, spatialOffset));
                }

                return true;
            }
            finally
            {
                ReleaseBiolumGuard(vault, ProfileFloatsGuardMask);
            }
        }

        private BiolumPulseStateDTO BuildMockLightingPulseState(
            in MockWeatherSignal weather,
            in BiolumPulseStateDTO profileDefaults,
            float darknessActivationThreshold)
        {
            BiolumPulseStateDTO generatedState = default;
            double3 aupReference = new double3(_aupOriginOffset.x, _aupOriginOffset.y, _aupOriginOffset.z);
            float darkness = ResolveDarknessScalar(weather, darknessActivationThreshold, aupReference);
            float qualityGain = math.lerp(0.92f, 1.12f, math.saturate(_globalQualityWeight));

            for (int i = 0; i < SyncGroupCount; i++)
            {
                float4 defaults = GetPulseGroup(in profileDefaults, i);
                float amplitude = math.clamp(defaults.z * darkness * qualityGain, 0f, MaxHdrIntensity);
                float spatialOffset = math.clamp(defaults.w, 0f, 4f);
                SetPulseGroup(ref generatedState, i, new float4(defaults.x, defaults.y, amplitude, spatialOffset));
            }

            return generatedState;
        }

        private bool TryWriteMockLightingPulseState(IDataVault vault, in BiolumPulseStateDTO generatedState)
        {
            if (!TryAcquireBiolumGuard(vault, PulseStateGuardMask))
                return false;

            try
            {
                if (!TryResolveBiolumVaultBuffer(vault, in _pulseStateHandle, BiolumPulseStateBufferId, 1, out NativeArray<BiolumPulseStateDTO> pulseState))
                    return false;

                pulseState[0] = generatedState;
                return true;
            }
            finally
            {
                ReleaseBiolumGuard(vault, PulseStateGuardMask);
            }
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
            for (int i = 0; i < count; i++)
                speciesTuning[i] = BuildEmergencySpeciesTuning(i);
        }

        private static BiolumSpeciesTuningDTO BuildEmergencySpeciesTuning(int index)
        {
            uint groupColor = (index & 3) == 0 ? _EmergencyCyanPacked : (index & 3) == 1 ? _EmergencyGreenPacked : (index & 3) == 2 ? _EmergencyVioletPacked : _EmergencyAmberPacked;
            uint shifted = BiolumPackedColorUtility.LerpPackedColor(groupColor, _EmergencyCyanPacked, math.frac(index * 0.091f) * 0.2f);
            return new BiolumSpeciesTuningDTO
            {
                SpeciesHash = EmergencyCoralSyncHash ^ (uint)(0x9E3779B9u * (index + 1)),
                PackedColor = shifted,
                Frequency = 0.28f + math.frac(index * 0.173f) * 0.82f,
                WaveSpeed = 24f + math.frac(index * 0.211f) * 72f,
                BiomeBlend01 = math.frac(index * 0.137f)
            };
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
            Span<byte> profileBytes = stackalloc byte[ProfileByteCount];
            int totalBytesRead = TryReadColdProfileBytes(profileBytes);
            if (!TryAcquireProfileBufferCold(out IDataVault vault, out NativeArray<float> profileFloats))
                return;

            try
            {
                SeedDefaultProfiles(profileFloats);
                _profileSourceHash = ProfileFallbackHash;

                if (totalBytesRead > 0)
                {
                    int readableFloats = math.min(ProfileFloatCount, totalBytesRead >> 2);
                    for (int i = 0; i < readableFloats; i++)
                        profileFloats[i] = SanitizeProfileFloat(ReadFloatLittleEndian(profileBytes, i << 2), i);

                    _profileSourceHash = ProfileBinaryHash;
                }

                _profilesLoaded = true;
            }
            finally
            {
                ReleaseBiolumGuard(vault, ProfileFloatsGuardMask);
            }
        }

        private static int TryReadColdProfileBytes(Span<byte> profileBytes)
        {
            string path = BuildColdProfilePath();
            if (string.IsNullOrEmpty(path))
                return 0;

            try
            {
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

                return totalBytesRead;
            }
            catch (Exception)
            {
                return 0;
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

        private static string BuildColdProfilePath()
        {
            if (TryFindProfilePath(Application.streamingAssetsPath, out string profilePath))
                return profilePath;

#if UNITY_EDITOR
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (TryFindProfilePath(Path.Combine(projectRoot, "Data", "Visuals"), out profilePath))
                return profilePath;

            if (TryFindProfilePath(Path.Combine(projectRoot, "Docs", "Generated"), out profilePath))
                return profilePath;

            return TryFindProfilePath(Path.Combine(projectRoot, "Docs"), out profilePath) ? profilePath : null;
#else
            return null;
#endif
        }

        private static bool TryFindProfilePath(string directory, out string path)
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

        private bool TryAcquireProfileBufferCold(out IDataVault vault, out NativeArray<float> profileFloats)
        {
            profileFloats = default;
            vault = _dataVault;
            if (vault == null || !EnsureVaultBuffers())
                return false;

            if (!TryAcquireBiolumGuard(vault, ProfileFloatsGuardMask))
                return false;

            bool success = false;
            try
            {
                success = TryResolveBiolumVaultBuffer(vault, in _profileFloatsHandle, BufferID.BiolumProfileFloats, ProfileFloatCount, out profileFloats);
                return success;
            }
            finally
            {
                if (!success)
                {
                    profileFloats = default;
                    ReleaseBiolumGuard(vault, ProfileFloatsGuardMask);
                }
            }
        }

        private bool TryAcquireProfileBuffer(out IDataVault vault, out NativeArray<float> profileFloats)
        {
            profileFloats = default;
            vault = _dataVault;
            if (vault == null || !HasVaultBuffers())
                return false;

            if (!TryAcquireBiolumGuard(vault, ProfileFloatsGuardMask))
                return false;

            bool success = false;
            try
            {
                success = TryResolveBiolumVaultBuffer(vault, in _profileFloatsHandle, BufferID.BiolumProfileFloats, ProfileFloatCount, out profileFloats);
                return success;
            }
            finally
            {
                if (!success)
                {
                    profileFloats = default;
                    ReleaseBiolumGuard(vault, ProfileFloatsGuardMask);
                }
            }
        }

        private bool TryAcquireBlackBoxBuffer(out IDataVault vault, out NativeArray<BiolumPulseTelemetryEntry> blackBox)
        {
            blackBox = default;
            vault = _dataVault;
            if (vault == null || !HasVaultBuffers())
                return false;

            if (!TryAcquireBiolumGuard(vault, BlackBoxGuardMask))
                return false;

            bool success = false;
            try
            {
                success = TryResolveBiolumVaultBuffer(vault, in _blackBoxHandle, BufferID.BiolumBlackBox, BlackBoxFrameCount, out blackBox);
                return success;
            }
            finally
            {
                if (!success)
                {
                    blackBox = default;
                    ReleaseBiolumGuard(vault, BlackBoxGuardMask);
                }
            }
        }

        private void SeedDefaultProfiles(NativeArray<float> profileFloats)
        {
            for (int i = 0; i < MaxGlobalBiolumStates; i++)
            {
                int offset = i * ProfileFloatStride;
                float lane = i;
                profileFloats[offset] = math.frac(lane * 0.61803398875f);
                profileFloats[offset + 1] = 0.42f + math.frac(lane * 0.41f) * 0.78f;
                profileFloats[offset + 2] = 0.42f + math.frac(lane * 0.37f) * 0.48f;
                profileFloats[offset + 3] = 0.18f + math.frac(lane * 0.23f) * 0.48f;
                profileFloats[offset + 4] = DefaultDarknessActivationThreshold;
                profileFloats[offset + 5] = 0.45f + math.frac(lane * 0.31f) * 0.28f;
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
                    return math.clamp(value, 0.0025f, 8f);
                case 2:
                    return math.clamp(value, 0f, MaxHdrIntensity);
                case 3:
                    return math.clamp(value, 0f, 4f);
                case 4:
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

            if (SignalBus<LightLevelSignal>.TryGetLatest(out LightLevelSignal light, out int lightSequence) &&
                lightSequence != _lastGlobalLightLevelSignalSequence)
            {
                _lastGlobalLightLevelSignalSequence = lightSequence;
                ambientLight01 = math.saturate(light.LightLevel01);
                weatherDirty = true;
            }

            if (SurvivalSignalRoute.TryGetLatestDeath(out SurvivalVitalsChangedSignal vitals, out int vitalsSequence) &&
                vitalsSequence != _lastGlobalSurvivalVitalsSequence)
            {
                _lastGlobalSurvivalVitalsSequence = vitalsSequence;
                oxygen01 = math.saturate(vitals.Oxygen01);
                weatherDirty = true;
            }

            if (weatherDirty)
                MirrorGlobalWeatherSignalsToVault(ambientLight01, oxygen01);

            if (SignalBus<CombatDamageSignal>.TryGetLatest(out CombatDamageSignal damage, out int damageSequence) &&
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

            if (!TryAcquireBiolumGuard(vault, MockWeatherGuardMask))
                return;

            try
            {
                if (!TryResolveBiolumVaultBuffer(vault, in _mockWeatherSignalHandle, BufferID.BiolumMockWeatherSignal, 1, out NativeArray<MockWeatherSignal> weatherSignal))
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
                ReleaseBiolumGuard(vault, MockWeatherGuardMask);
            }
        }

        private void MirrorGlobalDamageSignalToVault(in CombatDamageSignal signal)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !HasVaultBuffers())
                return;

            if (!TryAcquireBiolumGuard(vault, MockDamageGuardMask))
                return;

            try
            {
                if (!TryResolveBiolumVaultBuffer(vault, in _mockDamageSignalHandle, BufferID.BiolumMockDamageSignal, 1, out NativeArray<BiolumMockCombatDamageSignal> damageSignal))
                    return;

                float magnitude = math.max(signal.Magnitude, 0f);
                float radius = math.clamp(4f + math.sqrt(math.max(magnitude, 0.0001f)) * 2.75f, 4f, 48f);
                damageSignal[0] = new BiolumMockCombatDamageSignal
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
                ReleaseBiolumGuard(vault, MockDamageGuardMask);
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

        private static byte EncodeQualityWeightByte(float qualityWeight01)
        {
            return (byte)math.clamp((int)math.round(math.saturate(qualityWeight01) * 255f), 0, 255);
        }

        private void UpdateBiomeBlendState(float dt)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !HasVaultBuffers())
                return;

            if (!TryAcquireBiolumGuard(vault, MockWeatherGuardMask))
                return;

            try
            {
                if (!TryResolveBiolumVaultBuffer(vault, in _mockWeatherSignalHandle, BufferID.BiolumMockWeatherSignal, 1, out NativeArray<MockWeatherSignal> weather))
                    return;

                uint biomeHash = weather[0].CurrentBiomeHash;
                _dearLieBlend01 = 1f;
                if (_lastBiomeHash == 0u)
                    _lastBiomeHash = biomeHash;

                if (biomeHash != _lastBiomeHash)
                    _lastBiomeHash = biomeHash;
            }
            finally
            {
                ReleaseBiolumGuard(vault, MockWeatherGuardMask);
            }
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

            if (!TryAcquireBiolumGuard(vault, MockPredatorGuardMask))
                return;

            BiolumMockPredatorProximitySignal predator;
            try
            {
                if (!TryResolveBiolumVaultBuffer(
                        vault,
                        in _mockPredatorSignalHandle,
                        BufferID.BiolumMockPredatorSignal,
                        1,
                        out NativeArray<BiolumMockPredatorProximitySignal> predatorSignal))
                {
                    return;
                }

                predator = predatorSignal[0];
            }
            finally
            {
                ReleaseBiolumGuard(vault, MockPredatorGuardMask);
            }

            if (predator.Strength01 <= 0.01f || predator.FrameStamp == _lastPredatorSignalFrame)
                return;

            float waveSpeed = ResolveMockPredatorWaveSpeed(in predator);

            if (!TryAcquireBiolumGuard(vault, SyncPulseGuardMask))
                return;

            try
            {
                if (!TryResolveBiolumVaultBuffer(vault, in _syncPulsesHandle, BufferID.BiolumSyncPulses, SyncPulseCapacity, out NativeArray<SyncPulseDTO> pulses) ||
                    !TryResolveBiolumVaultBuffer(vault, in _syncPulseAgesHandle, BufferID.BiolumSyncPulseAges, SyncPulseCapacity, out NativeArray<float> ages))
                {
                    return;
                }

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
                ReleaseBiolumGuard(vault, SyncPulseGuardMask);
            }
        }

        private void AdvanceMockPredatorSignal(float dt)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !HasVaultBuffers())
                return;

            if (!TryAcquireBiolumGuard(vault, MockPredatorGuardMask))
                return;

            try
            {
                if (!TryResolveBiolumVaultBuffer(vault, in _mockPredatorSignalHandle, BufferID.BiolumMockPredatorSignal, 1, out NativeArray<BiolumMockPredatorProximitySignal> predatorSignal))
                    return;

                BiolumMockPredatorProximitySignal signal = predatorSignal[0];
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
                        MathLodApproximation.ApproxSinBhaskara(angleX) * radiusX,
                        -218.0,
                        MathLodApproximation.ApproxCosBhaskara(angleZ) * radiusZ);
                    signal.RadiusMeters = rng.NextFloat(92f, 124f);
                    signal.Strength01 = 1f;
                    signal.SpeciesMask = 0xFFFFFFFFu;
                    signal.FrameStamp = _frameCounter;
                }

                predatorSignal[0] = signal;
            }
            finally
            {
                ReleaseBiolumGuard(vault, MockPredatorGuardMask);
            }
        }

        private float ResolveMockPredatorWaveSpeed(in BiolumMockPredatorProximitySignal predator)
        {
            float fallback = math.max(8f, predator.RadiusMeters * 0.65f);
            IDataVault vault = _dataVault;
            if (vault == null || !HasVaultBuffers())
                return fallback;

            if (!TryAcquireBiolumGuard(vault, SpeciesTuningGuardMask))
                return fallback;

            try
            {
                if (!TryResolveBiolumVaultBuffer(vault, in _speciesTuningHandle, BufferID.BiolumSpeciesTuning, MaxSpeciesTuningCount, out NativeArray<BiolumSpeciesTuningDTO> species))
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
                ReleaseBiolumGuard(vault, SpeciesTuningGuardMask);
            }
        }

        private void AdvanceSyncPulseAges(float dt)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !HasVaultBuffers())
                return;

            if (!TryAcquireBiolumGuard(vault, SyncPulseGuardMask))
                return;

            try
            {
                if (!TryResolveBiolumVaultBuffer(vault, in _syncPulsesHandle, BufferID.BiolumSyncPulses, SyncPulseCapacity, out NativeArray<SyncPulseDTO> pulses) ||
                    !TryResolveBiolumVaultBuffer(vault, in _syncPulseAgesHandle, BufferID.BiolumSyncPulseAges, SyncPulseCapacity, out NativeArray<float> ages))
                {
                    return;
                }

                int active = 0;
                int count = math.min(SyncPulseCapacity, math.min(pulses.Length, ages.Length));
                for (int i = 0; i < count; i++)
                {
                    SyncPulseDTO pulse = pulses[i];
                    float age = ages[i];
                    if (age < 8f && math.isfinite(pulse.WaveSpeed) && pulse.WaveSpeed > 0.0001f)
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
                ReleaseBiolumGuard(vault, SyncPulseGuardMask);
            }
        }

        private void AdvanceMockDamageAge(float dt)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !HasVaultBuffers())
                return;

            if (!TryAcquireBiolumGuard(vault, MockDamageGuardMask))
                return;

            try
            {
                if (!TryResolveBiolumVaultBuffer(vault, in _mockDamageSignalHandle, BufferID.BiolumMockDamageSignal, 1, out NativeArray<BiolumMockCombatDamageSignal> damage))
                    return;

                BiolumMockCombatDamageSignal signal = damage[0];
                if (signal.AgeSeconds < 9f)
                {
                    signal.AgeSeconds += dt;
                    damage[0] = signal;
                }
            }
            finally
            {
                ReleaseBiolumGuard(vault, MockDamageGuardMask);
            }
        }

#if UNITY_EDITOR
        private void EnsureCsvBackgroundWatcher()
        {
            if (_csvWatcher != null)
                return;

            string path = BuildEditorCsvOverridePath();
            if (string.IsNullOrEmpty(path))
                return;

            Volatile.Write(ref _csvWorkerState, CsvWorkerIdle);

            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
                return;

            _csvWatcher = TryCreateCsvBackgroundWatcher(directory);

            RequestCsvReload();
        }

        private void StopCsvBackgroundWatcher()
        {
            FileSystemWatcher watcher = _csvWatcher;
            if (watcher != null)
            {
                _csvWatcher = null;
                StopCsvBackgroundWatcherNoThrow(watcher);
            }
        }

        private FileSystemWatcher TryCreateCsvBackgroundWatcher(string directory)
        {
            try
            {
                Directory.CreateDirectory(directory);
                FileSystemWatcher watcher = new FileSystemWatcher(directory, CsvOverrideFileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    EnableRaisingEvents = false
                };
                watcher.Changed += OnCsvFileChanged;
                watcher.Created += OnCsvFileChanged;
                watcher.Renamed += OnCsvFileRenamed;
                watcher.EnableRaisingEvents = true;
                return watcher;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void StopCsvBackgroundWatcherNoThrow(FileSystemWatcher watcher)
        {
            try
            {
                watcher.EnableRaisingEvents = false;
            }
            catch (Exception)
            {
            }

            try
            {
                watcher.Changed -= OnCsvFileChanged;
                watcher.Created -= OnCsvFileChanged;
                watcher.Renamed -= OnCsvFileRenamed;
                watcher.Dispose();
            }
            catch (Exception)
            {
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

            if (!EnsureCsvOverrideReadBytes())
            {
                Interlocked.CompareExchange(ref _csvWorkerState, CsvWorkerIdle, CsvWorkerApplying);
                return;
            }

            int bytesRead = TryReadCsvOverrideIntoBuffer(_csvOverrideReadBytes, out long writeTicks);
            if (bytesRead <= 0)
            {
                Interlocked.CompareExchange(ref _csvWorkerState, CsvWorkerIdle, CsvWorkerApplying);
                return;
            }

            IDataVault vault = _dataVault;
            if (vault == null || !HasVaultBuffers())
            {
                Interlocked.CompareExchange(ref _csvWorkerState, CsvWorkerRequested, CsvWorkerApplying);
                return;
            }

            if (!TryApplyCsvOverridesToVault(vault, bytesRead))
            {
                Interlocked.CompareExchange(ref _csvWorkerState, CsvWorkerRequested, CsvWorkerApplying);
                return;
            }

            Volatile.Write(ref _csvLastWriteTicks, writeTicks);
            _forceSchedule = true;
            Interlocked.CompareExchange(ref _csvWorkerState, CsvWorkerIdle, CsvWorkerApplying);
        }

        private bool TryApplyCsvOverridesToVault(IDataVault vault, int bytesRead)
        {
            if (vault == null || bytesRead <= 0 || !_csvOverrideReadBytes.IsCreated)
                return false;

            int cursor = 0;
            while (cursor < bytesRead)
            {
                int lineStart = cursor;
                while (cursor < bytesRead && _csvOverrideReadBytes[cursor] != (byte)'\n' && _csvOverrideReadBytes[cursor] != (byte)'\r')
                    cursor++;

                bool parsed = TryParseCsvLine(
                    _csvOverrideReadBytes,
                    lineStart,
                    cursor,
                    out bool isPulseRow,
                    out int pulseGroup,
                    out uint speciesHash,
                    out float value0,
                    out float value1,
                    out float value2,
                    out float value3,
                    out float waveSpeed,
                    out bool hasWaveSpeed);

                while (cursor < bytesRead && (_csvOverrideReadBytes[cursor] == (byte)'\n' || _csvOverrideReadBytes[cursor] == (byte)'\r'))
                    cursor++;

                if (!parsed)
                    continue;

                if (isPulseRow)
                {
                    float4 pulseRow = ResolveCsvPulseRow(value0, value1, value2, value3);
                    if (!TryApplyPulseProfileCsvOverrideToVault(vault, pulseGroup, in pulseRow) ||
                        !TryApplyPulseStateCsvOverrideToVault(vault, pulseGroup, in pulseRow))
                    {
                        return false;
                    }

                    continue;
                }

                if (!TryApplySpeciesCsvOverrideToVault(vault, speciesHash, value0, value1, value2, value3, waveSpeed, hasWaveSpeed, out BiolumSpeciesTuningDTO tuning) ||
                    !TryApplyGlowStateCsvOverrideToVault(vault, in tuning))
                {
                    return false;
                }
            }

            return true;
        }

        private bool EnsureCsvOverrideReadBytes()
        {
            if (_csvOverrideReadBytes.IsCreated &&
                _csvOverrideReadBytes.Length >= CsvScratchByteCount)
            {
                return true;
            }

            DisposeCsvOverrideReadBytes();

            // COLD NATIVE ALLOC: byte[CsvScratchByteCount] - editor CSV staging outside DataVault guards - owner: BIOLUM_PULSE_SYNC
            _csvOverrideReadBytes = H8Memory.Allocate<byte>(
                CsvScratchByteCount,
                SystemID.Vfx,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            return _csvOverrideReadBytes.IsCreated;
        }

        private void DisposeCsvOverrideReadBytes()
        {
            if (!_csvOverrideReadBytes.IsCreated)
            {
                _csvOverrideReadBytes = default;
                return;
            }

            H8Memory.Release(ref _csvOverrideReadBytes, SystemID.Vfx);
        }

        private unsafe int TryReadCsvOverrideIntoBuffer(NativeArray<byte> readBuffer, out long writeTicks)
        {
            writeTicks = 0L;
            if (!readBuffer.IsCreated)
                return 0;

            string path = _csvOverridePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return 0;

            try
            {
                writeTicks = File.GetLastWriteTimeUtc(path).Ticks;
                if (writeTicks == Volatile.Read(ref _csvLastWriteTicks))
                    return 0;

                int capacity = math.min(CsvScratchByteCount, readBuffer.Length);
                if (capacity <= 0)
                    return 0;

                void* readBufferPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(readBuffer);
                Span<byte> destination = new Span<byte>(readBufferPtr, capacity);
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, capacity, FileOptions.SequentialScan))
                    return stream.Read(destination);
            }
            catch (Exception)
            {
                writeTicks = 0L;
                return 0;
            }
        }

        private string BuildEditorCsvOverridePath()
        {
            if (!string.IsNullOrEmpty(_csvOverridePath))
                return _csvOverridePath;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _csvOverridePath = Path.Combine(projectRoot, CsvOverrideFileName);
            if (!File.Exists(_csvOverridePath))
            {
                string legacyPath = Path.Combine(projectRoot, LegacyCsvOverrideFileName);
                if (File.Exists(legacyPath))
                    _csvOverridePath = legacyPath;
            }
            return _csvOverridePath;
        }
#endif

#if UNITY_EDITOR
        private bool TryApplyPulseProfileCsvOverrideToVault(
            IDataVault vault,
            int pulseGroup,
            in float4 pulseRow)
        {
            if (!TryAcquireBiolumGuard(vault, ProfileFloatsGuardMask))
                return false;

            try
            {
                if (!TryResolveBiolumVaultBuffer(vault, in _profileFloatsHandle, BufferID.BiolumProfileFloats, ProfileFloatCount, out NativeArray<float> profileFloats))
                    return false;

                WritePulseGroupCsvOverride(profileFloats, default, pulseGroup, in pulseRow);
                return true;
            }
            finally
            {
                ReleaseBiolumGuard(vault, ProfileFloatsGuardMask);
            }
        }

        private bool TryApplyPulseStateCsvOverrideToVault(
            IDataVault vault,
            int pulseGroup,
            in float4 pulseRow)
        {
            if (!TryAcquireBiolumGuard(vault, PulseStateGuardMask))
                return false;

            try
            {
                if (!TryResolveBiolumVaultBuffer(vault, in _pulseStateHandle, BiolumPulseStateBufferId, 1, out NativeArray<BiolumPulseStateDTO> pulseState))
                    return false;

                WritePulseGroupCsvOverride(default, pulseState, pulseGroup, in pulseRow);
                return true;
            }
            finally
            {
                ReleaseBiolumGuard(vault, PulseStateGuardMask);
            }
        }

        private bool TryApplySpeciesCsvOverrideToVault(
            IDataVault vault,
            uint speciesHash,
            float r,
            float g,
            float b,
            float frequency,
            float waveSpeed,
            bool hasWaveSpeed,
            out BiolumSpeciesTuningDTO tuning)
        {
            tuning = default;
            uint packedColor = BiolumPackedColorUtility.PackRgb10A2(new float3(r, g, b), 1f);
            float clampedFrequency = math.clamp(frequency, 0.0025f, 8f);
            float clampedWaveSpeed = hasWaveSpeed ? math.clamp(waveSpeed, 1f, 180f) : -1f;

            if (!TryAcquireBiolumGuard(vault, SpeciesTuningGuardMask))
                return false;

            try
            {
                if (!TryResolveBiolumVaultBuffer(vault, in _speciesTuningHandle, BufferID.BiolumSpeciesTuning, MaxSpeciesTuningCount, out NativeArray<BiolumSpeciesTuningDTO> species))
                    return false;

                int count = math.min(MaxSpeciesTuningCount, species.Length);
                if (count <= 0)
                    return false;

                int slot = (int)(speciesHash % (uint)count);
                for (int i = 0; i < count; i++)
                {
                    if (species[i].SpeciesHash == speciesHash)
                    {
                        slot = i;
                        break;
                    }
                }

                tuning = species[slot];
                tuning.SpeciesHash = speciesHash;
                tuning.PackedColor = packedColor;
                tuning.Frequency = clampedFrequency;
                tuning.WaveSpeed = hasWaveSpeed
                    ? clampedWaveSpeed
                    : math.clamp(tuning.WaveSpeed <= 0f ? 48f : tuning.WaveSpeed, 1f, 180f);
                species[slot] = tuning;
                return true;
            }
            finally
            {
                ReleaseBiolumGuard(vault, SpeciesTuningGuardMask);
            }
        }

        private bool TryApplyGlowStateCsvOverrideToVault(IDataVault vault, in BiolumSpeciesTuningDTO tuning)
        {
            if (!TryAcquireBiolumGuard(vault, GlowStatesGuardMask))
                return false;

            try
            {
                if (!TryResolveBiolumVaultBuffer(vault, in _glowStatesHandle, BufferID.BiolumGlowStates, MaxGlowInstances, out NativeArray<GlowStateDTO> glowStates))
                    return false;

                ApplySpeciesTuningToGlowStates(glowStates, tuning);
                return true;
            }
            finally
            {
                ReleaseBiolumGuard(vault, GlowStatesGuardMask);
            }
        }

        private static bool TryParseCsvLine(
            NativeArray<byte> bytes,
            int start,
            int end,
            out bool isPulseRow,
            out int pulseGroup,
            out uint speciesHash,
            out float value0,
            out float value1,
            out float value2,
            out float value3,
            out float waveSpeed,
            out bool hasWaveSpeed)
        {
            isPulseRow = false;
            pulseGroup = -1;
            speciesHash = 0u;
            value0 = 0f;
            value1 = 0f;
            value2 = 0f;
            value3 = 0f;
            waveSpeed = 0f;
            hasWaveSpeed = false;

            start = SkipCsvWhitespace(bytes, start, end);
            if (start >= end || bytes[start] == (byte)'#')
                return false;

            int tokenStart = start;
            int tokenEnd = FindCsvTokenEnd(bytes, tokenStart, end);
            if (tokenEnd <= tokenStart)
                return false;

            speciesHash = TryParseUIntToken(bytes, tokenStart, tokenEnd, out uint parsedHash)
                ? parsedHash
                : HashToken(bytes, tokenStart, tokenEnd);

            int cursor = tokenEnd + 1;
            if (TryParsePulseGroupToken(bytes, tokenStart, tokenEnd, speciesHash, out int parsedPulseGroup))
            {
                if (TryReadCsvFloat(bytes, ref cursor, end, out float phase) &&
                    TryReadCsvFloat(bytes, ref cursor, end, out float groupFrequency) &&
                    TryReadCsvFloat(bytes, ref cursor, end, out float amplitude) &&
                    TryReadCsvFloat(bytes, ref cursor, end, out float spatialOffset))
                {
                    isPulseRow = true;
                    pulseGroup = parsedPulseGroup;
                    value0 = phase;
                    value1 = groupFrequency;
                    value2 = amplitude;
                    value3 = spatialOffset;
                    return true;
                }

                return false;
            }

            if (!TryReadCsvFloat(bytes, ref cursor, end, out float r) ||
                !TryReadCsvFloat(bytes, ref cursor, end, out float g) ||
                !TryReadCsvFloat(bytes, ref cursor, end, out float b) ||
                !TryReadCsvFloat(bytes, ref cursor, end, out float frequency))
            {
                return false;
            }

            value0 = r;
            value1 = g;
            value2 = b;
            value3 = frequency;
            hasWaveSpeed = TryReadCsvFloat(bytes, ref cursor, end, out waveSpeed);
            return true;
        }

        private static bool TryParsePulseGroupToken(NativeArray<byte> bytes, int start, int end, uint parsedHash, out int groupIndex)
        {
            groupIndex = -1;
            if (TryParseUIntToken(bytes, start, end, out uint numeric) && numeric < SyncGroupCount)
            {
                groupIndex = (int)numeric;
                return true;
            }

            int digit = -1;
            for (int i = start; i < end; i++)
            {
                byte c = bytes[i];
                if (c >= (byte)'0' && c <= (byte)'3')
                    digit = c - (byte)'0';
            }

            if (digit < 0 || digit >= SyncGroupCount)
                return false;

            uint tokenPrefixHash = HashToken(bytes, start, math.max(start, end - 1));
            if (parsedHash == CsvGroupHash || parsedHash == CsvPulseHash || parsedHash == CsvRowHash || tokenPrefixHash == CsvGroupHash || tokenPrefixHash == CsvPulseHash || tokenPrefixHash == CsvRowHash)
            {
                groupIndex = digit;
                return true;
            }

            return false;
        }

        private static float4 ResolveCsvPulseRow(float phase, float frequency, float amplitude, float spatialOffset)
        {
            return new float4(
                math.fmod(math.max(0f, math.isfinite(phase) ? phase : 0f), TwoPi),
                math.clamp(math.isfinite(frequency) ? frequency : 0.45f, 0.0025f, 8f),
                math.clamp(math.isfinite(amplitude) ? amplitude : 0.65f, 0f, MaxHdrIntensity),
                math.clamp(math.isfinite(spatialOffset) ? spatialOffset : 0.25f, 0f, 4f));
        }

        private static void WritePulseGroupCsvOverride(
            NativeArray<float> profileFloats,
            NativeArray<BiolumPulseStateDTO> pulseState,
            int groupIndex,
            in float4 row)
        {
            if (groupIndex < 0 || groupIndex >= SyncGroupCount)
                return;

            if (profileFloats.IsCreated && profileFloats.Length >= ProfileFloatCount)
            {
                int offset = groupIndex * ProfileFloatStride;
                profileFloats[offset] = row.x;
                profileFloats[offset + 1] = row.y;
                profileFloats[offset + 2] = row.z;
                profileFloats[offset + 3] = row.w;
            }

            if (!pulseState.IsCreated || pulseState.Length <= 0)
                return;

            BiolumPulseStateDTO state = pulseState[0];
            SetPulseGroup(ref state, groupIndex, row);
            pulseState[0] = state;
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

#endif

        private void ScheduleStateJob(float cadenceSeconds, float deltaTime)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !HasVaultBuffers())
                return;

            if (!TryPinStateJobBuffers(vault))
                return;

            bool scheduled = false;
            try
            {
                if (!TryResolveBiolumVaultBuffer(vault, in _pulseStateHandle, BiolumPulseStateBufferId, 1, out NativeArray<BiolumPulseStateDTO> pulseState) ||
                    !TryResolveBiolumVaultBuffer(vault, in _profileFloatsHandle, BufferID.BiolumProfileFloats, ProfileFloatCount, out NativeArray<float> profileFloats) ||
                    !TryResolveBiolumVaultBuffer(vault, in _mockWeatherSignalHandle, BufferID.BiolumMockWeatherSignal, 1, out NativeArray<MockWeatherSignal> weather) ||
                    !TryResolveBiolumVaultBuffer(vault, in _mockPredatorSignalHandle, BufferID.BiolumMockPredatorSignal, 1, out NativeArray<BiolumMockPredatorProximitySignal> predator) ||
                    !TryResolveBiolumVaultBuffer(vault, in _syncPulsesHandle, BufferID.BiolumSyncPulses, SyncPulseCapacity, out NativeArray<SyncPulseDTO> syncPulses) ||
                    !TryResolveBiolumVaultBuffer(vault, in _syncPulseAgesHandle, BufferID.BiolumSyncPulseAges, SyncPulseCapacity, out NativeArray<float> syncPulseAges))
                {
                    return;
                }

                AdvanceBiolumPhasesJob phaseJob = new AdvanceBiolumPhasesJob
                {
                    PulseState = pulseState,
                    ProfileFloats = profileFloats,
                    WeatherSignal = weather,
                    PredatorSignal = predator,
                    SyncPulses = syncPulses,
                    SyncPulseAges = syncPulseAges,
                    DeltaTime = deltaTime,
                    GlobalQualityWeight = _globalQualityWeight,
                    AupReference = new double3(_aupOriginOffset.x, _aupOriginOffset.y, _aupOriginOffset.z),
                    DarknessActivationThreshold = ResolveDarknessActivationThreshold(profileFloats),
                    PredatorPanicSpeed = ResolvePredatorPanicSpeed(profileFloats)
                };

                _stateJobScheduleTimestamp = Stopwatch.GetTimestamp();
                _stateJobHandle = phaseJob.Schedule();
                H8Memory.RegisterActiveJob(SystemID.Vfx, _stateJobHandle);
                _stateJobScheduled = true;
                scheduled = true;
            }
            finally
            {
                if (!scheduled)
                    ReleaseStateJobBufferPins();
            }
        }

        private bool CompleteScheduledJobAndPublish()
        {
            if (!_stateJobScheduled)
                return false;

            if (!_stateJobHandle.IsCompleted)
            {
                _jobOverrunFrames = math.min(_jobOverrunFrames + 1, JobOverrunDumpFrameThreshold);
                _pendingTelemetryFlags |= TelemetryFlagJobOverrun;
                if (_jobOverrunFrames >= JobOverrunDumpFrameThreshold)
                    DumpBlackBox(TelemetryFlagJobOverrun);

                return false;
            }

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _stateJobHandle))
                return false;

            long completeTimestamp = Stopwatch.GetTimestamp();
            _lastOscillatorComputeTimeMs = _stateJobScheduleTimestamp > 0L
                ? (float)((completeTimestamp - _stateJobScheduleTimestamp) * 1000.0 / Stopwatch.Frequency)
                : 0f;
            _stateJobScheduled = false;
            _jobOverrunFrames = 0;
            bool finite;
            try
            {
                finite = CopyPulseStateToManagedBuffer();
            }
            finally
            {
                ReleaseStateJobBufferPins();
            }

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
            return true;
        }

        private void CompleteScheduledJobForTeardown()
        {
            if (_stateJobScheduled)
            {
                DispatcherJobFence.TryComplete(ref _stateJobHandle, forceComplete: true);
                _stateJobScheduled = false;
            }

            ReleaseStateJobBufferPins();
        }

        private bool TryPinStateJobBuffers(IDataVault vault)
        {
            if (_stateJobPinsHeld)
                return false;

            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            bool success = false;
            try
            {
                _stateJobPinVault = vault;
                if (!TryLockStateJobBuffer(vault, BiolumPulseStateBufferId, StateJobPinPulseState) ||
                    !TryLockStateJobBuffer(vault, BufferID.BiolumProfileFloats, StateJobPinProfileFloats) ||
                    !TryLockStateJobBuffer(vault, BufferID.BiolumMockWeatherSignal, StateJobPinMockWeather) ||
                    !TryLockStateJobBuffer(vault, BufferID.BiolumMockPredatorSignal, StateJobPinMockPredator) ||
                    !TryLockStateJobBuffer(vault, BufferID.BiolumSyncPulses, StateJobPinSyncPulses) ||
                    !TryLockStateJobBuffer(vault, BufferID.BiolumSyncPulseAges, StateJobPinSyncPulseAges))
                {
                    return false;
                }

                _stateJobPinsHeld = true;
                success = true;
                return true;
            }
            finally
            {
                if (!success)
                    ReleaseStateJobBufferPins();
            }
        }

        private bool TryLockStateJobBuffer(IDataVault vault, BufferID bufferId, uint pinBit)
        {
            if ((_stateJobPinMask & pinBit) != 0u)
                return true;

            if (vault == null ||
                (_stateJobPinVault != null && !ReferenceEquals(_stateJobPinVault, vault)) ||
                !vault.TryLockBuffer(bufferId, SystemID.Vfx))
            {
                return false;
            }

            _stateJobPinVault = vault;
            _stateJobPinMask |= pinBit;
            return true;
        }

        private void ReleaseStateJobBufferPins()
        {
            IDataVault vault = _stateJobPinVault;
            uint pinMask = _stateJobPinMask;
            _stateJobPinVault = null;
            _stateJobPinMask = 0u;
            _stateJobPinsHeld = false;

            if (vault == null || pinMask == 0u)
                return;

            TryUnlockStateJobBuffer(vault, pinMask, StateJobPinSyncPulseAges, BufferID.BiolumSyncPulseAges);
            TryUnlockStateJobBuffer(vault, pinMask, StateJobPinSyncPulses, BufferID.BiolumSyncPulses);
            TryUnlockStateJobBuffer(vault, pinMask, StateJobPinMockPredator, BufferID.BiolumMockPredatorSignal);
            TryUnlockStateJobBuffer(vault, pinMask, StateJobPinMockWeather, BufferID.BiolumMockWeatherSignal);
            TryUnlockStateJobBuffer(vault, pinMask, StateJobPinProfileFloats, BufferID.BiolumProfileFloats);
            TryUnlockStateJobBuffer(vault, pinMask, StateJobPinPulseState, BiolumPulseStateBufferId);
        }

        private static void TryUnlockStateJobBuffer(IDataVault vault, uint pinMask, uint pinBit, BufferID bufferId)
        {
            if ((pinMask & pinBit) != 0u)
                vault.TryUnlockBuffer(bufferId, SystemID.Vfx);
        }

        private bool CopyPulseStateToManagedBuffer()
        {
            IDataVault vault = _dataVault;
            if (!TryResolveBiolumVaultBuffer(vault, in _pulseStateHandle, BiolumPulseStateBufferId, 1, out NativeArray<BiolumPulseStateDTO> pulseState))
                return false;

            BiolumPulseStateDTO stateDto = pulseState[0];
            return CopyPulseStateToManagedBuffer(in stateDto);
        }

        private bool CopyPulseStateToManagedBuffer(in BiolumPulseStateDTO stateDto)
        {
            bool finite = true;
            int activeCount = SyncGroupCount;
            _publishedGlobalStateCount = activeCount;
            _dearLieGroupMatrix = Matrix4x4.zero;
            float strongest = -1f;
            int strongestProfile = 0;
            for (int i = 0; i < SyncGroupCount; i++)
            {
                if (i >= activeCount)
                    continue;

                float4 state = GetPulseGroup(in stateDto, i);
                if (!math.all(math.isfinite(state)))
                {
                    finite = false;
                    state = new float4(0f, 0.45f, 0f, 0.2f);
                }

                state.x = RepeatRadians(state.x);
                state.y = math.clamp(state.y, 0.0025f, 8f);
                state.z = math.clamp(state.z, 0f, MaxHdrIntensity);
                state.w = math.clamp(state.w, 0f, 4f);
                _dearLieGroupMatrix.SetRow(i, new Vector4(state.x, state.y, state.z, state.w));

                if (i < activeCount && state.z > strongest)
                {
                    strongest = state.z;
                    strongestProfile = i;
                }
            }

            _activeBiolumProfileId = strongestProfile;
            return finite;
        }

        private void EvaluateColdStartStates()
        {
            _dearLieGroupMatrix = Matrix4x4.zero;
            if (!TryAcquireProfileBuffer(out IDataVault vault, out NativeArray<float> profileFloats))
            {
                _publishedGlobalStateCount = 0;
                return;
            }

            _publishedGlobalStateCount = SyncGroupCount;
            try
            {
                for (int i = 0; i < SyncGroupCount; i++)
                {
                    float phase = ResolveProfilePhase(ReadPulseProfileValue(profileFloats, i, 0, i * 0.25f));
                    float frequency = math.clamp(ReadPulseProfileValue(profileFloats, i, 1, 0.45f + i * 0.11f), 0.0025f, 8f);
                    float amplitude = math.clamp(ReadPulseProfileValue(profileFloats, i, 2, 0.58f + i * 0.08f), 0f, MaxHdrIntensity);
                    float spatialOffset = math.clamp(ReadPulseProfileValue(profileFloats, i, 3, 0.18f + i * 0.07f), 0f, 4f);
                    _dearLieGroupMatrix.SetRow(i, new Vector4(
                        phase,
                        frequency,
                        amplitude,
                        spatialOffset));
                }
            }
            finally
            {
                ReleaseBiolumGuard(vault, ProfileFloatsGuardMask);
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
            float timeFloat = RepeatPositiveSeconds(_localTimeSeconds, ShaderClockWrapSeconds);
            float masterPhase = math.frac(timeFloat * 0.045f);
            int globalStateCount = math.clamp(_publishedGlobalStateCount, 0, SyncGroupCount);

            Shader.SetGlobalVector(_GlobalBiolumParamsId, new Vector4(globalStateCount, math.saturate(_globalQualityWeight), strobe01, 0f));
            Shader.SetGlobalVector(_GlobalBiolumClockId, new Vector4(timeFloat, cadence, _frameCounter, 0f));
            Shader.SetGlobalFloat(_GlobalBioTimeId, timeFloat);
            Shader.SetGlobalVector(_GlobalBiolumAupOffsetId, new Vector4(_aupOriginOffset.x, _aupOriginOffset.y, _aupOriginOffset.z, _profileSourceHash));
            Shader.SetGlobalVector(_BiolumIntensityId, new Vector4(ResolveMatrixDerivedBiolumIntensity(strobe01), strobe01, globalStateCount, overloadFlag));
            HectonShaderGlobalDataVaultBridge.PublishBiolumMasterPhase(new Vector4(masterPhase, ResolveTrianglePulse01(masterPhase), strobe01, _dearLieBlend01));
        }

        private float ResolveMatrixDerivedBiolumIntensity(float strobe01)
        {
            int activeCount = SyncGroupCount;
            float resolved = math.clamp(strobe01 * MaxHdrIntensity, 0f, MaxHdrIntensity);
            for (int i = 0; i < activeCount; i++)
            {
                float intensity = _dearLieGroupMatrix.GetRow(i).z;
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
            Shader.SetGlobalFloat(_GlobalBioTimeId, 0f);
            Shader.SetGlobalVector(_GlobalBiolumAupOffsetId, Vector4.zero);
            Shader.SetGlobalVector(_BiolumIntensityId, Vector4.zero);
            Shader.SetGlobalMatrix(_GlobalBiolumDearLieGroupsId, Matrix4x4.zero);
            HectonShaderGlobalDataVaultBridge.PublishBiolumMasterPhase(new Vector4(0f, 0.5f, 0f, 0f));
        }

        private void RecordTelemetry(byte flags)
        {
            if (!TryAcquireBlackBoxBuffer(out IDataVault vault, out NativeArray<BiolumPulseTelemetryEntry> blackBox))
                return;

            try
            {
                Vector4 primaryState = _dearLieGroupMatrix.GetRow(0);

                blackBox[_blackBoxCursor] = new BiolumPulseTelemetryEntry
                {
                    Frame = _frameCounter,
                    ActiveGlowingInstances = (uint)math.clamp(_activeGlowingInstanceCount, 0, MaxGlowInstances),
                    WavePulsesActive = (ushort)math.clamp(_activeSyncPulseCount, 0, SyncPulseCapacity),
                    QualityTier = EncodeQualityWeightByte(_globalQualityWeight),
                    Flags = flags,
                    OscillatorComputeTimeMs = math.max(0f, _lastOscillatorComputeTimeMs),
                    GlobalDarknessScalar = ResolveDarknessScalarFromMatrix(),
                    Group0Phase = RepeatRadians(primaryState.x),
                    FrequencyMultiplier = math.clamp(primaryState.y, 0f, 8f),
                    PrimaryAmplitudeHdr = math.clamp(primaryState.z, 0f, MaxHdrIntensity)
                };

                _blackBoxCursor = (_blackBoxCursor + 1) % blackBox.Length;
            }
            finally
            {
                ReleaseBiolumGuard(vault, BlackBoxGuardMask);
            }
        }

        private void DumpBlackBox(byte reason)
        {
            if (_dumpedFault)
                return;

            try
            {
                if (!CopyBlackBoxDumpSnapshot())
                {
                    Volatile.Write(ref _blackBoxDumpState, BlackBoxDumpStateFailed);
                    return;
                }

                if (!WriteBlackBoxDumpSnapshotToScratch(reason))
                {
                    Volatile.Write(ref _blackBoxDumpState, BlackBoxDumpStateFailed);
                    return;
                }

                if (QueueBlackBoxDumpWrite())
                    _dumpedFault = true;
            }
            catch (Exception)
            {
                Volatile.Write(ref _blackBoxDumpState, BlackBoxDumpStateFailed);
            }
        }

        private bool CopyBlackBoxDumpSnapshot()
        {
            if (!_blackBoxDumpSnapshot.IsReady(BlackBoxFrameCount) ||
                !TryAcquireBlackBoxBuffer(out IDataVault vault, out NativeArray<BiolumPulseTelemetryEntry> blackBox))
                return false;

            try
            {
                NativeArray<BiolumPulseTelemetryEntry> snapshot = _blackBoxDumpSnapshot.Entries;
                int sourceCount = blackBox.Length;
                int dumpCount = math.min(BlackBoxFrameCount, sourceCount);
                int cursor = math.clamp(_blackBoxCursor, 0, sourceCount - 1);
                int startIndex = cursor - dumpCount;
                if (startIndex < 0)
                    startIndex += sourceCount;

                _blackBoxDumpSnapshotCursor = cursor;
                _blackBoxDumpSnapshotCount = dumpCount;
                for (int i = 0; i < dumpCount; i++)
                    snapshot[i] = blackBox[(startIndex + i) % sourceCount];

                return true;
            }
            finally
            {
                ReleaseBiolumGuard(vault, BlackBoxGuardMask);
            }
        }

        private bool WriteBlackBoxDumpSnapshotToScratch(byte reason)
        {
            IDataVault vault = _dataVault;
            int dumpCount = math.clamp(_blackBoxDumpSnapshotCount, 0, BlackBoxFrameCount);
            if (vault == null ||
                dumpCount <= 0 ||
                !_blackBoxDumpSnapshot.IsReady(BlackBoxFrameCount))
            {
                return false;
            }

            if (!TryAcquireBiolumGuard(vault, BlackBoxDumpScratchGuardMask))
                return false;

            try
            {
                if (!TryResolveBiolumVaultBuffer(vault, in _blackBoxDumpScratchHandle, BiolumBlackBoxDumpScratchBufferId, BlackBoxDumpByteCount, out NativeArray<byte> bytes))
                    return false;

                BiolumPulseDumpHeader header = new BiolumPulseDumpHeader
                {
                    Magic = BlackBoxMagic,
                    Reason = reason,
                    EntrySizeBytes = BlackBoxEntrySizeBytes,
                    WriteCursor = _blackBoxDumpSnapshotCursor,
                    EntryCount = dumpCount
                };

                WriteUnmanagedToBytes(bytes, 0, ref header);
                int offset = BlackBoxDumpHeaderSizeBytes;
                for (int i = 0; i < dumpCount; i++)
                {
                    BiolumPulseTelemetryEntry entry = _blackBoxDumpSnapshot.Entries[i];
                    WriteUnmanagedToBytes(bytes, offset, ref entry);
                    offset += BlackBoxEntrySizeBytes;
                }

                Volatile.Write(ref _blackBoxDumpByteCount, offset);
                return true;
            }
            finally
            {
                ReleaseBiolumGuard(vault, BlackBoxDumpScratchGuardMask);
            }
        }

        private bool EnsureBlackBoxDumpSnapshot()
        {
            if (_blackBoxDumpSnapshot.IsReady(BlackBoxFrameCount))
                return true;

            try
            {
                _blackBoxDumpSnapshot.Allocate(BlackBoxFrameCount);
                _blackBoxDumpSnapshotCursor = 0;
                _blackBoxDumpSnapshotCount = 0;
                return _blackBoxDumpSnapshot.IsReady(BlackBoxFrameCount);
            }
            catch (Exception)
            {
                _blackBoxDumpSnapshot.Dispose();
                _blackBoxDumpSnapshotCursor = 0;
                _blackBoxDumpSnapshotCount = 0;
                return false;
            }
        }

        private bool EnsureBlackBoxDumpWriteBytes()
        {
            if (_blackBoxDumpWriteBytes.IsCreated &&
                _blackBoxDumpWriteBytes.Length >= BlackBoxDumpByteCount)
            {
                return true;
            }

            DisposeBlackBoxDumpWriteBytes();

            // COLD NATIVE ALLOC: byte[BlackBoxDumpByteCount] - private dump write mirror, keeps file IO outside DataVault guard - owner: BIOLUM_PULSE_SYNC
            _blackBoxDumpWriteBytes = H8Memory.Allocate<byte>(
                BlackBoxDumpByteCount,
                SystemID.Vfx,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            return _blackBoxDumpWriteBytes.IsCreated;
        }

        private void DisposeBlackBoxDumpSnapshot()
        {
            _blackBoxDumpSnapshot.Dispose();
            _blackBoxDumpSnapshotCursor = 0;
            _blackBoxDumpSnapshotCount = 0;
        }

        private void DisposeBlackBoxDumpWriteBytes()
        {
            if (!_blackBoxDumpWriteBytes.IsCreated)
            {
                _blackBoxDumpWriteBytes = default;
                return;
            }

            H8Memory.Release(ref _blackBoxDumpWriteBytes, SystemID.Vfx);
        }

        private bool QueueBlackBoxDumpWrite()
        {
            AutoResetEvent signal = _blackBoxDumpSignal;
            Thread thread = _blackBoxDumpThread;
            if (signal == null || thread == null || !thread.IsAlive)
            {
                Volatile.Write(ref _blackBoxDumpState, BlackBoxDumpStateFailed);
                return false;
            }

            Volatile.Write(ref _blackBoxDumpState, BlackBoxDumpStateQueued);
            return SignalBlackBoxDumpWorkerNoThrow(signal);
        }

        private void EnsureBlackBoxDumpWorker()
        {
            Thread existingThread = _blackBoxDumpThread;
            if (existingThread != null)
            {
                if (existingThread.IsAlive)
                    return;

                _blackBoxDumpThread = null;
                AutoResetEvent staleSignal = _blackBoxDumpSignal;
                if (staleSignal != null)
                {
                    DisposeBlackBoxDumpSignalNoThrow(staleSignal);
                    _blackBoxDumpSignal = null;
                }
            }

            try
            {
                if (!EnsureBlackBoxDumpSnapshot() ||
                    !EnsureBlackBoxDumpWriteBytes())
                {
                    Volatile.Write(ref _blackBoxDumpState, BlackBoxDumpStateFailed);
                    return;
                }

                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                _blackBoxDumpPath = Path.Combine(projectRoot, DumpRelativePath);
                _blackBoxDumpMirrorPath = Path.Combine(projectRoot, DumpMirrorRelativePath);
                _blackBoxDumpSignal = new AutoResetEvent(false);
                Volatile.Write(ref _blackBoxDumpStopRequested, 0);
                Volatile.Write(ref _blackBoxDumpState, BlackBoxDumpStateIdle);
                _blackBoxDumpThread = new Thread(BlackBoxDumpWorkerLoop)
                {
                    IsBackground = true,
                    Name = "H8_SHINOBU_238_BlackBoxDump"
                };
                _blackBoxDumpThread.Start();
            }
            catch (Exception)
            {
                _blackBoxDumpThread = null;
                if (_blackBoxDumpSignal != null)
                {
                    DisposeBlackBoxDumpSignalNoThrow(_blackBoxDumpSignal);
                    _blackBoxDumpSignal = null;
                }

                _blackBoxDumpPath = null;
                _blackBoxDumpMirrorPath = null;
                Volatile.Write(ref _blackBoxDumpState, BlackBoxDumpStateFailed);
            }
        }

        private bool StopBlackBoxDumpWorker()
        {
            Thread thread = _blackBoxDumpThread;
            AutoResetEvent signal = _blackBoxDumpSignal;
            if (thread == null)
                return true;

            Volatile.Write(ref _blackBoxDumpStopRequested, 1);
            SignalBlackBoxDumpWorkerNoThrow(signal);
            bool joined = TryJoinBlackBoxDumpWorkerNoThrow(thread);

            if (!joined)
            {
                Volatile.Write(ref _blackBoxDumpState, BlackBoxDumpStateFailed);
                return false;
            }

            _blackBoxDumpThread = null;
            if (signal != null)
            {
                DisposeBlackBoxDumpSignalNoThrow(signal);
                _blackBoxDumpSignal = null;
            }

            return true;
        }

        private static bool SignalBlackBoxDumpWorkerNoThrow(AutoResetEvent signal)
        {
            if (signal == null)
                return false;

            try
            {
                signal.Set();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryJoinBlackBoxDumpWorkerNoThrow(Thread thread)
        {
            if (thread == null || !thread.IsAlive)
                return true;
            if (ReferenceEquals(Thread.CurrentThread, thread))
                return false;

            try
            {
                thread.Join(BlackBoxDumpWorkerJoinMilliseconds);
                return !thread.IsAlive;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void DisposeBlackBoxDumpSignalNoThrow(AutoResetEvent signal)
        {
            if (signal == null)
                return;

            try
            {
                signal.Dispose();
            }
            catch (Exception)
            {
            }
        }

        private void BlackBoxDumpWorkerLoop()
        {
            AutoResetEvent signal = _blackBoxDumpSignal;
            while (signal != null)
            {
                signal.WaitOne();
                int state = Volatile.Read(ref _blackBoxDumpState);
                if (state == BlackBoxDumpStateQueued &&
                    Interlocked.CompareExchange(ref _blackBoxDumpState, BlackBoxDumpStateWriting, BlackBoxDumpStateQueued) == BlackBoxDumpStateQueued)
                {
                    try
                    {
                        Volatile.Write(
                            ref _blackBoxDumpState,
                            WriteQueuedBlackBoxDump() ? BlackBoxDumpStateWritten : BlackBoxDumpStateFailed);
                    }
                    catch (Exception)
                    {
                        Volatile.Write(ref _blackBoxDumpState, BlackBoxDumpStateFailed);
                    }
                }

                if (Volatile.Read(ref _blackBoxDumpStopRequested) != 0)
                    return;
            }
        }

        private bool WriteQueuedBlackBoxDump()
        {
            int count = Volatile.Read(ref _blackBoxDumpByteCount);
            IDataVault vault = _dataVault;
            if (vault == null ||
                count <= 0 ||
                !EnsureBlackBoxDumpWriteBytes() ||
                count > _blackBoxDumpWriteBytes.Length)
            {
                return false;
            }

            if (!TryAcquireBiolumGuard(vault, BlackBoxDumpScratchGuardMask))
                return false;

            try
            {
                if (!TryResolveBiolumVaultBuffer(vault, in _blackBoxDumpScratchHandle, BiolumBlackBoxDumpScratchBufferId, BlackBoxDumpByteCount, out NativeArray<byte> bytes) ||
                    count > bytes.Length)
                    return false;

                NativeArray<byte>.Copy(bytes, 0, _blackBoxDumpWriteBytes, 0, count);
            }
            finally
            {
                ReleaseBiolumGuard(vault, BlackBoxDumpScratchGuardMask);
            }

            bool wrotePrimary = WriteBlackBoxDumpBytes(_blackBoxDumpPath, _blackBoxDumpWriteBytes, count);
            bool wroteMirror = WriteBlackBoxDumpBytes(_blackBoxDumpMirrorPath, _blackBoxDumpWriteBytes, count);
            return wrotePrimary && wroteMirror;
        }

        private static bool WriteBlackBoxDumpBytes(string dumpPath, NativeArray<byte> bytes, int count)
        {
            if (string.IsNullOrEmpty(dumpPath) || !bytes.IsCreated || count <= 0 || count > bytes.Length)
                return false;

            return NativeFaultDumpWriter.TryWriteAll(dumpPath, bytes, count);
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

        private static float ResolveProfilePhase(float profilePhase)
        {
            if (!math.isfinite(profilePhase))
                return 0f;

            float positive = profilePhase < 0f ? 0f : profilePhase;
            return positive <= 1f ? positive * TwoPi : RepeatRadians(positive);
        }

        private static float RepeatRadians(float radians)
        {
            if (!math.isfinite(radians))
                return 0f;

            float wrapped = math.fmod(radians, TwoPi);
            return wrapped < 0f ? wrapped + TwoPi : wrapped;
        }

        private static float RepeatPositiveSeconds(double seconds, double period)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds))
                return 0f;

            double safePeriod = period > 0d && !double.IsNaN(period) && !double.IsInfinity(period) ? period : 1d;
            double wrapped = seconds % safePeriod;
            if (wrapped < 0d)
                wrapped += safePeriod;
            return (float)wrapped;
        }

        private static float ResolveDarknessActivationThreshold(NativeArray<float> profileFloats)
        {
            if (!profileFloats.IsCreated || profileFloats.Length <= 4)
                return DefaultDarknessActivationThreshold;

            float threshold = profileFloats[4];
            return math.isfinite(threshold) ? math.saturate(threshold) : DefaultDarknessActivationThreshold;
        }

        private static float ResolvePredatorPanicSpeed(NativeArray<float> profileFloats)
        {
            if (!profileFloats.IsCreated || profileFloats.Length <= 5)
                return DefaultPredatorPanicSpeed;

            float encoded = profileFloats[5];
            if (!math.isfinite(encoded))
                return DefaultPredatorPanicSpeed;

            return math.lerp(1f, 4f, math.saturate(encoded));
        }

        private float ResolveDarknessScalarFromMatrix()
        {
            float strongest = 0f;
            for (int i = 0; i < SyncGroupCount; i++)
            {
                float amp = _dearLieGroupMatrix.GetRow(i).z;
                if (math.isfinite(amp))
                    strongest = math.max(strongest, math.clamp(amp, 0f, MaxHdrIntensity));
            }

            return math.saturate(strongest);
        }

        private static float4 GetPulseGroup(in BiolumPulseStateDTO pulseState, int groupIndex)
        {
            switch (groupIndex)
            {
                case 0:
                    return pulseState.Group1_Params;
                case 1:
                    return pulseState.Group2_Params;
                case 2:
                    return pulseState.Group3_Params;
                default:
                    return pulseState.Group4_Params;
            }
        }

        private static void SetPulseGroup(ref BiolumPulseStateDTO pulseState, int groupIndex, float4 row)
        {
            switch (groupIndex)
            {
                case 0:
                    pulseState.Group1_Params = row;
                    break;
                case 1:
                    pulseState.Group2_Params = row;
                    break;
                case 2:
                    pulseState.Group3_Params = row;
                    break;
                default:
                    pulseState.Group4_Params = row;
                    break;
            }
        }

        private static unsafe void WriteUnmanagedToBytes<T>(NativeArray<byte> target, int offset, ref T value) where T : unmanaged
        {
            if (!target.IsCreated)
                return;

            int size = UnsafeUtility.SizeOf<T>();
            if (offset < 0 || offset + size > target.Length)
                return;

            void* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(target) + offset;
            void* source = UnsafeUtility.AddressOf(ref value);
            UnsafeUtility.MemCpy(destination, source, size);
        }

        private static float ReadPulseProfileValue(NativeArray<float> profileFloats, int groupIndex, int lane, float fallback)
        {
            int offset = groupIndex * ProfileFloatStride + lane;
            if (!profileFloats.IsCreated || offset < 0 || offset >= profileFloats.Length)
                return fallback;

            float value = profileFloats[offset];
            return math.isfinite(value) ? value : fallback;
        }

        private static float ResolveGlobalDarknessScalar(MockWeatherSignal weather, float activationThreshold)
        {
            float threshold = math.max(0.0001f, math.saturate(activationThreshold));
            float ambient = math.saturate(math.isfinite(weather.AmbientLightLevel) ? weather.AmbientLightLevel : 1f);
            float eclipse = math.saturate((threshold - ambient) / threshold);
            float health = math.saturate(math.isfinite(weather.SystemHealthIndex01) ? weather.SystemHealthIndex01 : 1f);
            return math.saturate(eclipse * math.lerp(0.82f, 1.12f, health));
        }

        private static float ResolveAupDepthDarknessScalar(double3 aupReference)
        {
            float yMeters = math.isfinite((float)aupReference.y) ? (float)aupReference.y : 0f;
            float depthMeters = math.max(0f, -yMeters);
            return SmoothStepRange01(DefaultDepthDarknessStartMeters, DefaultDepthDarknessFullMeters, depthMeters);
        }

        private static float ResolveDarknessScalar(MockWeatherSignal weather, float activationThreshold, double3 aupReference)
        {
            return math.max(
                ResolveGlobalDarknessScalar(weather, activationThreshold),
                ResolveAupDepthDarknessScalar(aupReference));
        }

        private static unsafe ref BiolumPulseStateDTO GetPulseStateRef(NativeArray<BiolumPulseStateDTO> states)
        {
            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(states);
            return ref UnsafeUtility.AsRef<BiolumPulseStateDTO>(basePtr);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct AdvanceBiolumPhasesJob : IJob
        {
            [NoAlias]
            public NativeArray<BiolumPulseStateDTO> PulseState;
            [ReadOnly, NoAlias]
            public NativeArray<float> ProfileFloats;
            [ReadOnly, NoAlias]
            public NativeArray<MockWeatherSignal> WeatherSignal;
            [ReadOnly, NoAlias]
            public NativeArray<BiolumMockPredatorProximitySignal> PredatorSignal;
            [ReadOnly, NoAlias]
            public NativeArray<SyncPulseDTO> SyncPulses;
            [ReadOnly, NoAlias]
            public NativeArray<float> SyncPulseAges;
            public float DeltaTime;
            public float GlobalQualityWeight;
            public double3 AupReference;
            public float DarknessActivationThreshold;
            public float PredatorPanicSpeed;

            public void Execute()
            {
                if (!PulseState.IsCreated || PulseState.Length <= 0)
                    return;

                ref BiolumPulseStateDTO pulse = ref GetPulseStateRef(PulseState);
                MockWeatherSignal weather = WeatherSignal.IsCreated && WeatherSignal.Length > 0 ? WeatherSignal[0] : default;
                BiolumMockPredatorProximitySignal predator = PredatorSignal.IsCreated && PredatorSignal.Length > 0 ? PredatorSignal[0] : default;
                float dt = math.clamp(math.isfinite(DeltaTime) ? DeltaTime : 0f, 0f, 0.25f);
                float darkness = ResolveDarknessScalar(weather, DarknessActivationThreshold, AupReference);
                float threat01 = math.saturate(math.isfinite(predator.Strength01) ? predator.Strength01 : 0f);
                float panicSpeed = math.lerp(1f, math.max(1f, PredatorPanicSpeed), threat01);
                float amplitudePanicGain = math.lerp(1f, 1.38f, threat01);
                float qualityGain = math.lerp(0.92f, 1.12f, math.saturate(GlobalQualityWeight));

                for (int i = 0; i < SyncGroupCount; i++)
                {
                    float4 current = GetPulseGroup(in pulse, i);
                    float phase = math.isfinite(current.x) ? current.x : ResolveProfilePhase(ReadPulseProfileValue(ProfileFloats, i, 0, i * 0.25f));
                    float profileFrequency = ReadPulseProfileValue(ProfileFloats, i, 1, 0.45f + i * 0.11f);
                    float frequency = math.clamp(math.isfinite(current.y) && current.y > 0.0001f ? current.y : profileFrequency, 0.0025f, 8f);
                    float baseAmplitude = math.clamp(ReadPulseProfileValue(ProfileFloats, i, 2, 0.58f + i * 0.08f), 0f, MaxHdrIntensity);
                    float profileOffset = ReadPulseProfileValue(ProfileFloats, i, 3, 0.18f + i * 0.07f);
                    float spatialOffset = math.clamp(math.isfinite(current.w) && current.w > 0.0001f ? current.w : profileOffset, 0f, 4f);
                    phase = RepeatRadians(phase + frequency * dt * panicSpeed);
                    float amplitude = math.clamp(baseAmplitude * darkness * amplitudePanicGain * qualityGain, 0f, MaxHdrIntensity);
                    ApplyFixedSlotPulse(i, ref phase, ref amplitude, ref spatialOffset);
                    float4 row = new float4(phase, frequency, amplitude, spatialOffset);
                    if (!math.all(math.isfinite(row)))
                        row = new float4(0f, math.max(0.0025f, profileFrequency), 0f, math.max(0.01f, profileOffset));
                    SetPulseGroup(ref pulse, i, row);
                }
            }

            private void ApplyFixedSlotPulse(int groupIndex, ref float phase, ref float amplitude, ref float spatialOffset)
            {
                if (!SyncPulses.IsCreated || !SyncPulseAges.IsCreated)
                    return;

                int count = math.min(SyncPulseCapacity, math.min(SyncPulses.Length, SyncPulseAges.Length));
                for (int i = 0; i < count; i++)
                {
                    if ((i & (SyncGroupCount - 1)) != groupIndex)
                        continue;

                    float age = SyncPulseAges[i];
                    if (!math.isfinite(age) || age < 0f || age > 8f)
                        continue;

                    SyncPulseDTO pulse = SyncPulses[i];
                    if (!math.isfinite(pulse.WaveSpeed) || pulse.WaveSpeed <= 0.0001f)
                        continue;

                    double3 localAup = AupPrecisionMath.LocalDeltaDouble(pulse.OriginAUP, AupReference);
                    float3 local = AupPrecisionMath.DowncastLocalDelta(localAup, float3.zero);
                    if (!math.all(math.isfinite(local)))
                        continue;

                    float speed = math.clamp(pulse.WaveSpeed, 0.0001f, 180f);
                    float envelope = SmoothStep01(1f - math.saturate(age * 0.125f));
                    float projected = math.dot(local, new float3(0.0071f, 0.0023f, 0.0059f));
                    phase = RepeatRadians(phase + projected + age * speed * 0.017f);
                    amplitude = math.max(amplitude, envelope * math.lerp(0.45f, MaxHdrIntensity, math.saturate(speed * 0.006f)));
                    spatialOffset = math.max(spatialOffset, math.clamp(speed * 0.006f + math.length(local) * 0.00045f, 0.05f, 4f));
                }
            }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
        public struct BiolumPulseTelemetryEntry
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public uint Frame;
            [System.Runtime.InteropServices.FieldOffset(4)]
            public uint ActiveGlowingInstances;
            [System.Runtime.InteropServices.FieldOffset(8)]
            public float OscillatorComputeTimeMs;
            [System.Runtime.InteropServices.FieldOffset(12)]
            public float GlobalDarknessScalar;
            [System.Runtime.InteropServices.FieldOffset(16)]
            public float Group0Phase;
            [System.Runtime.InteropServices.FieldOffset(20)]
            public float FrequencyMultiplier;
            [System.Runtime.InteropServices.FieldOffset(24)]
            public float PrimaryAmplitudeHdr;
            [System.Runtime.InteropServices.FieldOffset(28)]
            public ushort WavePulsesActive;
            [System.Runtime.InteropServices.FieldOffset(30)]
            public byte QualityTier;
            [System.Runtime.InteropServices.FieldOffset(31)]
            public byte Flags;
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
