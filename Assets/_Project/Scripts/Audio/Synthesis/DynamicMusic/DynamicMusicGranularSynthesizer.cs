using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Audio.Synthesis
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SynthVoiceDTO
    {
        [FieldOffset(0)] public float CurrentPhase;
        [FieldOffset(4)] public float PhaseIncrement;
        [FieldOffset(8)] public float EnvelopeState;
        [FieldOffset(12)] public uint SoundHash;
        [FieldOffset(16)] public float TargetPitch;
        [FieldOffset(20)] public float TargetVolume;
        [FieldOffset(24)] private uint _pad0;
        [FieldOffset(28)] private uint _pad1;
        [FieldOffset(32)] private uint _pad2;
        [FieldOffset(36)] private uint _pad3;
        [FieldOffset(40)] private uint _pad4;
        [FieldOffset(44)] private uint _pad5;
        [FieldOffset(48)] private uint _pad6;
        [FieldOffset(52)] private uint _pad7;
        [FieldOffset(56)] private uint _pad8;
        [FieldOffset(60)] private uint _pad9;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DynamicMusicSynthScalarDTO
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public float TensionIndex;
        [FieldOffset(12)] public float DepthMeters;
        [FieldOffset(16)] public float Depth01;
        [FieldOffset(20)] public float GlobalQualityWeight;
        [FieldOffset(24)] public float DamageImpulse01;
        [FieldOffset(28)] public float StingerImpulse;
        [FieldOffset(32)] public float BaseDensity;
        [FieldOffset(36)] public float TargetPitch;
        [FieldOffset(40)] public float TargetVolume;
        [FieldOffset(44)] public float LfoFrequency;
        [FieldOffset(48)] public float LpfCutoffHz;
        [FieldOffset(52)] public int ActiveVoices;
        [FieldOffset(56)] public float OutputPeak;
        [FieldOffset(60)] public float OutputRms;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct DynamicMusicSynthTuningDTO
    {
        [FieldOffset(0)] public float BasePitchHz;
        [FieldOffset(4)] public float BaseGrainDensity;
        [FieldOffset(8)] public float TensionMultiplier;
        [FieldOffset(12)] public float LfoFrequency;
        [FieldOffset(16)] public float BaseVolume;
        [FieldOffset(20)] public float GrainSizeSeconds;
        [FieldOffset(24)] public float QualityMin;
        [FieldOffset(28)] public float QualityMax;
        [FieldOffset(32)] public float DepthMaxMeters;
        [FieldOffset(36)] public float LpfMinHz;
        [FieldOffset(40)] public float LpfDepthHzPerMeter;
        [FieldOffset(44)] public float StereoWidth;
        [FieldOffset(48)] public float DensityTensionScale;
        [FieldOffset(52)] public float DetuneCentsMax;
        [FieldOffset(56)] public float StingerDecaySeconds;
        [FieldOffset(60)] public float NoiseFoldback;
        [FieldOffset(64)] public uint SeedBase;
        [FieldOffset(68)] public uint WaveformHash;
        [FieldOffset(72)] public uint PresetHash;
        [FieldOffset(76)] public uint Flags;
        [FieldOffset(80)] private uint _pad0;
        [FieldOffset(84)] private uint _pad1;
        [FieldOffset(88)] private uint _pad2;
        [FieldOffset(92)] private uint _pad3;
        [FieldOffset(96)] private uint _pad4;
        [FieldOffset(100)] private uint _pad5;
        [FieldOffset(104)] private uint _pad6;
        [FieldOffset(108)] private uint _pad7;
        [FieldOffset(112)] private uint _pad8;
        [FieldOffset(116)] private uint _pad9;
        [FieldOffset(120)] private uint _pad10;
        [FieldOffset(124)] private uint _pad11;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DynamicMusicBiquadStateDTO
    {
        [FieldOffset(0)] public float Z1Left;
        [FieldOffset(4)] public float Z2Left;
        [FieldOffset(8)] public float Z1Right;
        [FieldOffset(12)] public float Z2Right;
        [FieldOffset(16)] public float LastCutoffHz;
        [FieldOffset(20)] public float A0;
        [FieldOffset(24)] public float A1;
        [FieldOffset(28)] public float A2;
        [FieldOffset(32)] public float B1;
        [FieldOffset(36)] public float B2;
        [FieldOffset(40)] public float LastSampleRate;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] private uint _pad0;
        [FieldOffset(52)] private uint _pad1;
        [FieldOffset(56)] private uint _pad2;
        [FieldOffset(60)] private uint _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DynamicMusicPresetRuleDTO
    {
        [FieldOffset(0)] public uint PresetHash;
        [FieldOffset(4)] public uint BiomeHash;
        [FieldOffset(8)] public uint NarrativeHash;
        [FieldOffset(12)] public uint WaveformHash;
        [FieldOffset(16)] public float BasePitchHz;
        [FieldOffset(20)] public float GrainSizeSeconds;
        [FieldOffset(24)] public float BaseDensity;
        [FieldOffset(28)] public float TensionMultiplier;
        [FieldOffset(32)] public float LfoFrequency;
        [FieldOffset(36)] public float BaseVolume;
        [FieldOffset(40)] public float QualityMin;
        [FieldOffset(44)] public float QualityMax;
        [FieldOffset(48)] public float DepthMaxMeters;
        [FieldOffset(52)] public float LpfMinHz;
        [FieldOffset(56)] public float LpfDepthHzPerMeter;
        [FieldOffset(60)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DynamicMusicSharedStateDTO
    {
        [FieldOffset(0)] public int ReadyBufferIndex;
        [FieldOffset(4)] public int ReadySampleCount;
        [FieldOffset(8)] public int PendingBufferIndex;
        [FieldOffset(12)] public int AudioCopyBufferIndex;
        [FieldOffset(16)] public uint PublishedFrame;
        [FieldOffset(20)] public int Channels;
        [FieldOffset(24)] public int AudioUnderrunCount;
        [FieldOffset(28)] public int AudioOverflowCount;
        [FieldOffset(32)] public float LastDspMicroseconds;
        [FieldOffset(36)] public int LastActiveVoices;
        [FieldOffset(40)] public float LastTensionIndex;
        [FieldOffset(44)] public float LastDepthMeters;
        [FieldOffset(48)] public float LastCutoffHz;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] private uint _pad0;
        [FieldOffset(60)] private uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AudioDSPTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public int ActiveVoices;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public int ReadyBufferIndex;
        [FieldOffset(16)] public float TensionIndex;
        [FieldOffset(20)] public float DepthMeters;
        [FieldOffset(24)] public float LpfCutoffHz;
        [FieldOffset(28)] public float DspJobMicroseconds;
        [FieldOffset(32)] public float QualityWeight;
        [FieldOffset(36)] public float GrainDensity;
        [FieldOffset(40)] public float TargetPitch;
        [FieldOffset(44)] public float StingerImpulse;
        [FieldOffset(48)] public float OutputPeak;
        [FieldOffset(52)] public float OutputRms;
        [FieldOffset(56)] public int AudioUnderrunCount;
        [FieldOffset(60)] public uint OutputSampleCount;
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-3880)]
    [RequireComponent(typeof(AudioSource))]
    [AddComponentMenu("Hecton8/Audio/Dynamic Music Granular Synthesizer")]
    public sealed unsafe class DynamicMusicGranularSynthesizer : MonoBehaviour, IUpdatable, ILateFrameTickable, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        public const int VoiceCapacity = 128;
        public const int TelemetryCapacity = 300;
        public const int PresetRuleCapacity = 32;
        public const int GrainBankSampleCapacity = 2048;
        public const int OutputSampleCapacity = 8192;

        private const int DefaultAudioChannels = 2;
        private const int DefaultScheduleSamples = 2048;
        private const float DefaultBasePitchHz = 73.416f;
        private const float DefaultBaseGrainDensity = 32f;
        private const float DefaultTensionMultiplier = 1.35f;
        private const float DefaultLfoFrequency = 1.35f;
        private const float DefaultBaseVolume = 0.22f;
        private const float DefaultGrainSizeSeconds = 0.075f;
        private const float DefaultDepthMaxMeters = 1800f;
        private const float DefaultLpfMinHz = 400f;
        private const float DefaultLpfDepthHzPerMeter = 10f;
        private const float DefaultStereoWidth = 0.82f;
        private const float DefaultDetuneCentsMax = 31f;
        private const float DefaultStingerDecaySeconds = 0.7f;
        private const float DspDumpThresholdMicroseconds = 1500f;
        private const float MinimumDeltaSeconds = 0.0001f;
        private const float MaximumDeltaSeconds = 0.25f;
        private const uint DefaultSynthSeed = 0x51350B75u;
        private const uint DefaultWaveformHash = 0xC31105E5u;
        private const uint FlagNonFinite = 1u << 0;
        private const uint FlagUsingMockTension = 1u << 1;
        private const uint FlagAudioUnderrun = 1u << 2;
        private const uint FlagCsvApplied = 1u << 3;
        private const uint FlagProceduralOnly = 1u << 4;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_1308_Synthesis.bin";
        private const SystemID VaultOwner = SystemID.AudioDynamicSynth;
        private const int LockVoices = 1 << 0;
        private const int LockScalar = 1 << 1;
        private const int LockTuning = 1 << 2;
        private const int LockOutputA = 1 << 3;
        private const int LockOutputB = 1 << 4;
        private const int LockBiquad = 1 << 5;
        private const int LockTelemetryRing = 1 << 6;
        private const int LockTelemetryCursor = 1 << 7;
        private const int LockPresetRules = 1 << 8;
        private const int LockGrainBank = 1 << 9;
        private const int LockSharedState = 1 << 10;
#if UNITY_EDITOR
        private const int LockCsvScratch = 1 << 11;
        private const int CsvScratchBytes = 8192;
        private const string CsvDefaultRelativePath = "Docs/Audio/synth_presets.csv";
        private const int CsvPollSlowTickInterval = 2;

        private const uint CsvBasePitchHash = 0x3D2A4071u;
        private const uint CsvGrainSizeHash = 0x1EC2CD18u;
        private const uint CsvWaveformHash = 0xFE78C747u;
        private const uint CsvBaseDensityHash = 0x31457F5Bu;
        private const uint CsvTensionMultiplierHash = 0x98F16469u;
        private const uint CsvLfoFrequencyHash = 0x8558D531u;
        private const uint CsvBaseVolumeHash = 0xF90B5C4Fu;
        private const uint CsvQualityMinHash = 0x1F25AA6Du;
        private const uint CsvQualityMaxHash = 0x09106D13u;
        private const uint CsvDepthMaxMetersHash = 0x5003D7DEu;
        private const uint CsvLpfMinHzHash = 0xA00FDE5Du;
        private const uint CsvLpfDepthHzPerMeterHash = 0xF27ED764u;
        private const uint CsvStereoWidthHash = 0xFB212D22u;
        private const uint CsvPresetHash = 0xE7B52233u;
        private const uint CsvBiomeHash = 0xB709D014u;
        private const uint CsvNarrativeHash = 0xC05354D2u;
#endif

        private static DynamicMusicGranularSynthesizer _activeInstance;

#if UNITY_EDITOR
        [Header("Cold Tuning")]
        [SerializeField] private string _csvRelativePath = CsvDefaultRelativePath;
#endif
        [SerializeField, Range(0f, 1f)] private float _mockTensionBias01;
        [SerializeField, Min(0f)] private float _mockDepthMeters = 900f;
        [SerializeField, Range(0f, 1f)] private float _mockQualityBias01 = 1f;
        [SerializeField] private AudioClip _driverClip;
        [SerializeField] private bool _autoCreateRuntimeInstance = true;

        private ref struct DynamicMusicVaultViews
        {
            public NativeArray<SynthVoiceDTO> Voices;
            public NativeArray<DynamicMusicSynthScalarDTO> Scalar;
            public NativeArray<DynamicMusicSynthTuningDTO> Tuning;
            public NativeArray<float> OutputA;
            public NativeArray<float> OutputB;
            public NativeArray<DynamicMusicBiquadStateDTO> Biquad;
            public NativeArray<AudioDSPTelemetryEntry> TelemetryRing;
            public NativeArray<int> TelemetryCursor;
#if UNITY_EDITOR
            public NativeArray<byte> CsvScratch;
#endif
            public NativeArray<DynamicMusicPresetRuleDTO> PresetRules;
            public NativeArray<float> GrainBank;
            public NativeArray<DynamicMusicSharedStateDTO> SharedState;
        }

        private VaultGenerationHandle<SynthVoiceDTO> _voicesHandle;
        private VaultGenerationHandle<DynamicMusicSynthScalarDTO> _scalarHandle;
        private VaultGenerationHandle<DynamicMusicSynthTuningDTO> _tuningHandle;
        private VaultGenerationHandle<float> _outputAHandle;
        private VaultGenerationHandle<float> _outputBHandle;
        private VaultGenerationHandle<DynamicMusicBiquadStateDTO> _biquadHandle;
        private VaultGenerationHandle<AudioDSPTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
#if UNITY_EDITOR
        private VaultGenerationHandle<byte> _csvScratchHandle;
#endif
        private VaultGenerationHandle<DynamicMusicPresetRuleDTO> _presetRulesHandle;
        private VaultGenerationHandle<float> _grainBankHandle;
        private VaultGenerationHandle<DynamicMusicSharedStateDTO> _sharedStateHandle;
        private VaultGenerationHandle<ScalabilityStateDTO> _scalabilityStateHandle;

        private IDataVault _dataVault;
        private AudioSource _hostSource;
#if UNITY_EDITOR
        private string _resolvedCsvPath;
        private string _lastResolvedCsvRelativePath;
        private DateTime _lastCsvWriteUtc;
#endif
        private float _cachedGlobalQualityWeight = 1f;
        private float _externalTension01;
        private float _externalDepthMeters;
        private float _externalQualityWeight = 1f;
        private float _externalDamageImpulse01;
        private int _externalScalarPublished;
        private float _pendingDamageImpulse;
        private float _pendingStingerImpulse;
        private int _nativeAllocated;
        private int _registeredUpdate;
        private int _registeredLateFrame;
        private int _registeredSlowTick;
        private int _registeredHotSwap;
        private int _audioHostConfigDirty;
        private int _synthJobPending;
        private int _jobBufferIndex = -1;
        private int _jobSampleCount;
        private int _jobChannels = DefaultAudioChannels;
        private int _readyBufferIndex = -1;
        private int _readySampleCount;
        private int _audioCopyBufferIndex = -1;
        private int _lastAudioRequestSamples = DefaultScheduleSamples;
        private int _lastAudioChannels = DefaultAudioChannels;
        private int _audioUnderrunCount;
        private int _audioOverflowCount;
#if UNITY_EDITOR
        private int _csvPollCountdown;
#endif
        private int _telemetryDumped;
        private uint _simulationFrameCounter;
        private long _synthJobStartTicks;
        private JobHandle _synthJobHandle;
        private int _synthJobLockMask;
        private IDataVault _synthJobLockedVault;

        public static bool TryGetActive(out DynamicMusicGranularSynthesizer synth)
        {
            synth = _activeInstance;
            return synth != null && Volatile.Read(ref synth._nativeAllocated) != 0;
        }

        public static void EnsureRuntimeInstanceForScene(Scene scene)
        {
            if (!Application.isPlaying)
                return;

            if (_activeInstance != null)
                return;

            AudioListener listener = ResolvePlayerAudioListenerCold();
            if (listener == null)
                return;

            GameObject host = listener.gameObject;
            if (!host.TryGetComponent(out DynamicMusicGranularSynthesizer synth))
                return;
            _activeInstance = synth;
            _ = scene;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstanceAfterSceneLoad()
        {
            if (!Application.isPlaying)
                return;

            EnsureDynamicMusicSignalLaneCold();
            EnsureRuntimeInstanceForScene(SceneManager.GetActiveScene());
        }

        private static AudioListener ResolvePlayerAudioListenerCold()
        {
            Camera playerCamera = null;
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null)
                playerCamera = playerContext.PlayerCamera;

            if (playerCamera == null)
            {
                IPlayerSensoryService playerSensory = GlobalRegistry.PlayerSensory;
                if (playerSensory != null)
                    playerCamera = playerSensory.PlayerCamera;
            }

            if (playerCamera == null)
                return null;

            return playerCamera.TryGetComponent(out AudioListener listener) ? listener : null;
        }

        public bool TryGetEditorTuning(out DynamicMusicSynthTuningDTO tuning)
        {
            tuning = default;
            IDataVault vault = _dataVault;
            if (Volatile.Read(ref _nativeAllocated) == 0 ||
                vault == null ||
                !IsDynamicMusicVaultHandle(in _tuningHandle, BufferID.AudioDynamicSynthTuning) ||
                !vault.TryReadOnlyHandle(in _tuningHandle, out NativeArray<DynamicMusicSynthTuningDTO>.ReadOnly tuningView) ||
                tuningView.Length <= 0)
            {
                return false;
            }

            tuning = tuningView[0];
            return true;
        }

        public bool TryWriteEditorTuning(in DynamicMusicSynthTuningDTO tuning)
        {
            IDataVault lockedVault = _dataVault;
            if (Volatile.Read(ref _nativeAllocated) == 0 ||
                lockedVault == null ||
                !AreDynamicMusicVaultHandlesExact() ||
                Volatile.Read(ref _synthJobPending) != 0)
                return false;

            int lockMask = 0;
            try
            {
                if (!TryAcquireLockedView(lockedVault, in _tuningHandle, LockTuning, ref lockMask, out NativeArray<DynamicMusicSynthTuningDTO> tuningView) ||
                    tuningView.Length <= 0)
                    return false;

                tuningView[0] = SanitizeTuning(tuning);
                return true;
            }
            finally
            {
                ReleaseDynamicMusicWriteLocks(lockedVault, lockMask);
            }
        }

        public bool TryGetEditorTelemetry(int offsetFromNewest, out AudioDSPTelemetryEntry entry)
        {
            entry = default;
            IDataVault vault = _dataVault;
            if (Volatile.Read(ref _nativeAllocated) == 0 ||
                vault == null ||
                !IsDynamicMusicVaultHandle(in _telemetryRingHandle, BufferID.AudioDynamicSynthTelemetry) ||
                !IsDynamicMusicVaultHandle(in _telemetryCursorHandle, BufferID.AudioDynamicSynthTelemetryCursor) ||
                !vault.TryReadOnlyHandle(in _telemetryRingHandle, out NativeArray<AudioDSPTelemetryEntry>.ReadOnly telemetryRing) ||
                !vault.TryReadOnlyHandle(in _telemetryCursorHandle, out NativeArray<int>.ReadOnly telemetryCursor) ||
                telemetryRing.Length <= 0 ||
                telemetryCursor.Length <= 0)
                return false;

            int cursor = math.max(0, telemetryCursor[0]);
            int capacity = math.min(TelemetryCapacity, telemetryRing.Length);
            int offset = math.clamp(offsetFromNewest, 0, capacity - 1);
            int index = cursor - 1 - offset;
            while (index < 0)
                index += capacity;

            entry = telemetryRing[index % capacity];
            return true;
        }

        public bool TryGetEditorOutputSample(int sampleIndex, out float sample)
        {
            sample = 0f;
            if (Volatile.Read(ref _nativeAllocated) == 0)
                return false;

            int readyIndex = Volatile.Read(ref _readyBufferIndex);
            int readySamples = Volatile.Read(ref _readySampleCount);
            int safeIndex = math.clamp(sampleIndex, 0, math.max(0, readySamples - 1));
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (readyIndex == 0 &&
                IsDynamicMusicVaultHandle(in _outputAHandle, BufferID.AudioDynamicSynthOutputA) &&
                vault.TryReadOnlyHandle(in _outputAHandle, out NativeArray<float>.ReadOnly outputA) &&
                safeIndex < outputA.Length)
            {
                sample = outputA[safeIndex];
                return true;
            }

            if (readyIndex == 1 &&
                IsDynamicMusicVaultHandle(in _outputBHandle, BufferID.AudioDynamicSynthOutputB) &&
                vault.TryReadOnlyHandle(in _outputBHandle, out NativeArray<float>.ReadOnly outputB) &&
                safeIndex < outputB.Length)
            {
                sample = outputB[safeIndex];
                return true;
            }

            return false;
        }

#if UNITY_EDITOR
        public bool ReloadSynthPresetCsvCold()
        {
            if (Volatile.Read(ref _nativeAllocated) == 0)
                return false;

            string path = ResolveCachedCsvPathCold();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            DynamicMusicVaultViews views = default;
            int lockMask = 0;
            IDataVault lockedVault = _dataVault;
            if (lockedVault == null || !AreDynamicMusicVaultHandlesExact())
                return false;

            try
            {
                if (!TryAcquireLockedView(lockedVault, in _csvScratchHandle, LockCsvScratch, ref lockMask, out views.CsvScratch) ||
                    !TryAcquireLockedView(lockedVault, in _tuningHandle, LockTuning, ref lockMask, out views.Tuning) ||
                    !TryAcquireLockedView(lockedVault, in _presetRulesHandle, LockPresetRules, ref lockMask, out views.PresetRules) ||
                    views.CsvScratch.Length <= 0 ||
                    views.Tuning.Length <= 0)
                    return false;

                int bytesRead;
                NativeArray<byte> csvScratch = views.CsvScratch;
                byte* scratch = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(csvScratch);
                using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int safeLength = (int)math.min(stream.Length, math.min(CsvScratchBytes, csvScratch.Length));
                    bytesRead = 0;
                    while (bytesRead < safeLength)
                    {
                        ref byte scratchStart = ref UnsafeUtility.AsRef<byte>(scratch + bytesRead);
                        Span<byte> scratchSpan = MemoryMarshal.CreateSpan(ref scratchStart, safeLength - bytesRead);
                        int read = stream.Read(scratchSpan);
                        if (read <= 0)
                            break;

                        bytesRead += read;
                    }
                }

                ParseSynthPresetCsv(bytesRead, ref views);
                _lastCsvWriteUtc = File.GetLastWriteTimeUtc(path);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                ReleaseDynamicMusicWriteLocks(lockedVault, lockMask);
            }
        }

#endif

        public void InjectStingerImpulse(float impulse01, float pitchKick01)
        {
            float impulse = math.saturate(FiniteOrFallback(impulse01, 0f));
            float pitchKick = math.saturate(FiniteOrFallback(pitchKick01, 0f));
            _pendingStingerImpulse = math.saturate(math.max(_pendingStingerImpulse, impulse));
            _pendingDamageImpulse = math.saturate(math.max(_pendingDamageImpulse, pitchKick));
        }

        public void PublishPresentationScalars(float tension01, float depthMeters, float globalQualityWeight, float damageImpulse01)
        {
            _externalTension01 = math.saturate(FiniteOrFallback(tension01, 0f));
            _externalDepthMeters = math.max(0f, FiniteOrFallback(depthMeters, 0f));
            _externalQualityWeight = math.saturate(FiniteOrFallback(globalQualityWeight, 1f));
            _externalDamageImpulse01 = math.saturate(FiniteOrFallback(damageImpulse01, 0f));
            _externalScalarPublished = 1;
        }

        private void Awake()
        {
            EnsureDynamicMusicSignalLaneCold();
            CacheDataVaultCold();
            EnsureVaultStorage();
#if UNITY_EDITOR
            RefreshCsvPathCold();
#endif
            ConfigureAudioHostCold();
            GenerateDefaultGrainBankCold();
            GenerateEmergencyMockAudioProfiles();
#if UNITY_EDITOR
            ReloadSynthPresetCsvCold();
#endif
        }

        private void OnEnable()
        {
            EnsureDynamicMusicSignalLaneCold();
            CacheDataVaultCold();
            EnsureVaultStorage();
#if UNITY_EDITOR
            RefreshCsvPathCold();
#endif
            ConfigureAudioHostCold();
            if (_autoCreateRuntimeInstance || _activeInstance == null)
                _activeInstance = this;

            if (GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment))
                _registeredUpdate = 1;
            if (GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment))
                _registeredLateFrame = 1;
            if (GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment))
                _registeredSlowTick = 1;
            if (GlobalRegistry.TryRegisterHotSwapListener(this))
                _registeredHotSwap = 1;
        }

        private void OnDisable()
        {
            UnregisterRuntime();
        }

        private void OnDestroy()
        {
            UnregisterRuntime();
            DisposeVaultStorage();
        }

        public void Tick(float deltaTime)
        {
            if (Volatile.Read(ref _nativeAllocated) == 0)
            {
                EnsureVaultStorage();
                if (Volatile.Read(ref _nativeAllocated) == 0)
                    return;
            }

            TryFlushCompletedSynthJob();
            if (Volatile.Read(ref _synthJobPending) != 0)
                return;

            unchecked
            {
                _simulationFrameCounter++;
                if (_simulationFrameCounter == 0u)
                    _simulationFrameCounter = 1u;
            }

            DrainSignalInputs();
            ScheduleSynthJobs(math.clamp(deltaTime, MinimumDeltaSeconds, MaximumDeltaSeconds));
        }

        public void LateFrameTick()
        {
            if (Volatile.Read(ref _nativeAllocated) == 0)
                return;

            TryFlushCompletedSynthJob();
            if (Interlocked.Exchange(ref _audioHostConfigDirty, 0) != 0)
                ConfigureAudioHostCold();
        }

        public void SlowTick()
        {
            if (Volatile.Read(ref _nativeAllocated) == 0)
                return;

            TryRefreshScalabilityStateHandleCold();
            RefreshGlobalQualitySnapshotCold();
            PollCsvRulesCold();
            Volatile.Write(ref _audioHostConfigDirty, 1);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            IDataVault nextVault = currentService is IDataVault vault ? vault : null;
            RebindDataVaultCold(nextVault);
            EnsureVaultStorage();
            TryRefreshScalabilityStateHandleCold();
            RefreshGlobalQualitySnapshotCold();
            _ = previousService;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            int safeChannels = math.clamp(channels, 1, 2);
            Volatile.Write(ref _lastAudioChannels, safeChannels);
            Volatile.Write(ref _lastAudioRequestSamples, math.min(data != null ? data.Length : 0, OutputSampleCapacity));

            if (data == null || data.Length == 0)
                return;

            int readyIndex = Volatile.Read(ref _readyBufferIndex);
            int readySamples = Volatile.Read(ref _readySampleCount);
            if (readyIndex < 0 || readySamples <= 0)
            {
                Interlocked.Increment(ref _audioUnderrunCount);
                ZeroManagedAudioBuffer(data, 0, data.Length);
                return;
            }

            int lockMask = 0;
            IDataVault lockedVault = null;
            try
            {
                if (!TryAcquireAudioCopyBuffer(readyIndex, out NativeArray<float> sourceBuffer, out lockMask, out lockedVault) ||
                    !sourceBuffer.IsCreated)
                {
                    Interlocked.Increment(ref _audioUnderrunCount);
                    ZeroManagedAudioBuffer(data, 0, data.Length);
                    return;
                }

                void* source = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(sourceBuffer);

                Interlocked.Exchange(ref _audioCopyBufferIndex, readyIndex);
                int copySamples = math.min(math.min(data.Length, readySamples), sourceBuffer.Length);
                fixed (float* destination = data)
                {
                    UnsafeUtility.MemCpy(destination, source, (long)copySamples * sizeof(float));
                }

                if (copySamples < data.Length)
                {
                    Interlocked.Increment(ref _audioUnderrunCount);
                    ZeroManagedAudioBuffer(data, copySamples, data.Length - copySamples);
                }
            }
            finally
            {
                Volatile.Write(ref _audioCopyBufferIndex, -1);
                ReleaseDynamicMusicWriteLocks(lockedVault, lockMask);
            }
        }

        private void UnregisterRuntime()
        {
            if (ReferenceEquals(_activeInstance, this))
                _activeInstance = null;
            if (Interlocked.Exchange(ref _registeredHotSwap, 0) != 0)
                GlobalRegistry.UnregisterHotSwapListener(this);
            if (Interlocked.Exchange(ref _registeredSlowTick, 0) != 0)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            if (Interlocked.Exchange(ref _registeredLateFrame, 0) != 0)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            if (Interlocked.Exchange(ref _registeredUpdate, 0) != 0)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            ForceFlushSynthJobForShutdown();
        }

        private void CacheDataVaultCold()
        {
            RebindDataVaultCold(GlobalRegistry.DataVault);
        }

        private void RebindDataVaultCold(IDataVault nextVault)
        {
            if (ReferenceEquals(_dataVault, nextVault))
                return;

            ForceFlushSynthJobForShutdown();
            if (_dataVault != null)
                DisposeVaultStorage();

            _dataVault = nextVault;
        }

        private void EnsureVaultStorage()
        {
            IDataVault vault = _dataVault;
            if (Volatile.Read(ref _nativeAllocated) != 0)
            {
                if (AreVaultViewsResolvable())
                    return;

                DisposeVaultStorage();
                _dataVault = vault;
            }

            vault = _dataVault;
            if (vault == null)
                return;

            _dataVault = vault;
            _voicesHandle = vault.EnsureGenerationHandle<SynthVoiceDTO>(BufferID.AudioDynamicSynthVoices, VoiceCapacity, VaultOwner, NativeArrayOptions.UninitializedMemory);
            _scalarHandle = vault.EnsureGenerationHandle<DynamicMusicSynthScalarDTO>(BufferID.AudioDynamicSynthScalar, 1, VaultOwner, NativeArrayOptions.UninitializedMemory);
            _tuningHandle = vault.EnsureGenerationHandle<DynamicMusicSynthTuningDTO>(BufferID.AudioDynamicSynthTuning, 1, VaultOwner, NativeArrayOptions.UninitializedMemory);
            _outputAHandle = vault.EnsureGenerationHandle<float>(BufferID.AudioDynamicSynthOutputA, OutputSampleCapacity, VaultOwner, NativeArrayOptions.UninitializedMemory);
            _outputBHandle = vault.EnsureGenerationHandle<float>(BufferID.AudioDynamicSynthOutputB, OutputSampleCapacity, VaultOwner, NativeArrayOptions.UninitializedMemory);
            _biquadHandle = vault.EnsureGenerationHandle<DynamicMusicBiquadStateDTO>(BufferID.AudioDynamicSynthBiquad, 1, VaultOwner, NativeArrayOptions.UninitializedMemory);
            _telemetryRingHandle = vault.EnsureGenerationHandle<AudioDSPTelemetryEntry>(BufferID.AudioDynamicSynthTelemetry, TelemetryCapacity, VaultOwner, NativeArrayOptions.UninitializedMemory);
            _telemetryCursorHandle = vault.EnsureGenerationHandle<int>(BufferID.AudioDynamicSynthTelemetryCursor, 1, VaultOwner, NativeArrayOptions.UninitializedMemory);
#if UNITY_EDITOR
            _csvScratchHandle = vault.EnsureGenerationHandle<byte>(BufferID.AudioDynamicSynthCsvScratch, CsvScratchBytes, VaultOwner, NativeArrayOptions.UninitializedMemory);
#endif
            _presetRulesHandle = vault.EnsureGenerationHandle<DynamicMusicPresetRuleDTO>(BufferID.AudioDynamicSynthPresetRules, PresetRuleCapacity, VaultOwner, NativeArrayOptions.UninitializedMemory);
            _grainBankHandle = vault.EnsureGenerationHandle<float>(BufferID.AudioDynamicSynthGrainBank, GrainBankSampleCapacity, VaultOwner, NativeArrayOptions.UninitializedMemory);
            _sharedStateHandle = vault.EnsureGenerationHandle<DynamicMusicSharedStateDTO>(BufferID.AudioDynamicSynthSharedState, 1, VaultOwner, NativeArrayOptions.UninitializedMemory);

            if (!TryAcquireColdInitViews(out DynamicMusicVaultViews views, out int lockMask, out IDataVault lockedVault))
            {
                DisposeVaultStorage();
                return;
            }

            try
            {
                MemClearArray(views.Voices);
                MemClearArray(views.Scalar);
                MemClearArray(views.Tuning);
                MemClearArray(views.OutputA);
                MemClearArray(views.OutputB);
                MemClearArray(views.Biquad);
                MemClearArray(views.TelemetryRing);
                MemClearArray(views.TelemetryCursor);
#if UNITY_EDITOR
                MemClearArray(views.CsvScratch);
#endif
                MemClearArray(views.PresetRules);
                MemClearArray(views.SharedState);
            }
            finally
            {
                ReleaseDynamicMusicWriteLocks(lockedVault, lockMask);
            }

            Volatile.Write(ref _readyBufferIndex, -1);
            Volatile.Write(ref _readySampleCount, 0);
            Volatile.Write(ref _audioCopyBufferIndex, -1);
            TryRefreshScalabilityStateHandleCold();
            RefreshGlobalQualitySnapshotCold();
            Volatile.Write(ref _nativeAllocated, 1);
        }

        private bool AreVaultViewsResolvable()
        {
            return AreDynamicMusicVaultHandlesExact() &&
                   IsReadOnlyHandleResolvable(in _voicesHandle, VoiceCapacity) &&
                   IsReadOnlyHandleResolvable(in _scalarHandle, 1) &&
                   IsReadOnlyHandleResolvable(in _tuningHandle, 1) &&
                   IsReadOnlyHandleResolvable(in _outputAHandle, OutputSampleCapacity) &&
                   IsReadOnlyHandleResolvable(in _outputBHandle, OutputSampleCapacity) &&
                   IsReadOnlyHandleResolvable(in _biquadHandle, 1) &&
                   IsReadOnlyHandleResolvable(in _telemetryRingHandle, TelemetryCapacity) &&
                   IsReadOnlyHandleResolvable(in _telemetryCursorHandle, 1) &&
#if UNITY_EDITOR
                   IsReadOnlyHandleResolvable(in _csvScratchHandle, CsvScratchBytes) &&
#endif
                   IsReadOnlyHandleResolvable(in _presetRulesHandle, PresetRuleCapacity) &&
                   IsReadOnlyHandleResolvable(in _grainBankHandle, GrainBankSampleCapacity) &&
                   IsReadOnlyHandleResolvable(in _sharedStateHandle, 1);
        }

        private void DisposeVaultStorage()
        {
            IDataVault vault = _dataVault;
            ReleaseVaultBuffer(vault, ref _voicesHandle, BufferID.AudioDynamicSynthVoices);
            ReleaseVaultBuffer(vault, ref _scalarHandle, BufferID.AudioDynamicSynthScalar);
            ReleaseVaultBuffer(vault, ref _tuningHandle, BufferID.AudioDynamicSynthTuning);
            ReleaseVaultBuffer(vault, ref _outputAHandle, BufferID.AudioDynamicSynthOutputA);
            ReleaseVaultBuffer(vault, ref _outputBHandle, BufferID.AudioDynamicSynthOutputB);
            ReleaseVaultBuffer(vault, ref _biquadHandle, BufferID.AudioDynamicSynthBiquad);
            ReleaseVaultBuffer(vault, ref _telemetryRingHandle, BufferID.AudioDynamicSynthTelemetry);
            ReleaseVaultBuffer(vault, ref _telemetryCursorHandle, BufferID.AudioDynamicSynthTelemetryCursor);
#if UNITY_EDITOR
            ReleaseVaultBuffer(vault, ref _csvScratchHandle, BufferID.AudioDynamicSynthCsvScratch);
#endif
            ReleaseVaultBuffer(vault, ref _presetRulesHandle, BufferID.AudioDynamicSynthPresetRules);
            ReleaseVaultBuffer(vault, ref _grainBankHandle, BufferID.AudioDynamicSynthGrainBank);
            ReleaseVaultBuffer(vault, ref _sharedStateHandle, BufferID.AudioDynamicSynthSharedState);
            _scalabilityStateHandle = default;
            _dataVault = null;
#if UNITY_EDITOR
            _resolvedCsvPath = null;
            _lastResolvedCsvRelativePath = null;
#endif
            Volatile.Write(ref _nativeAllocated, 0);
        }

        private static void ReleaseVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID expectedBufferId)
            where T : struct
        {
            if (vault != null && IsDynamicMusicVaultHandle(in handle, expectedBufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsDynamicMusicVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId)
            where T : struct
        {
            return handle.BufferID == (uint)expectedBufferId &&
                   handle.SystemID == (uint)VaultOwner &&
                   handle.Generation != 0u;
        }

        private bool AreDynamicMusicVaultHandlesExact()
        {
            return IsDynamicMusicVaultHandle(in _voicesHandle, BufferID.AudioDynamicSynthVoices) &&
                   IsDynamicMusicVaultHandle(in _scalarHandle, BufferID.AudioDynamicSynthScalar) &&
                   IsDynamicMusicVaultHandle(in _tuningHandle, BufferID.AudioDynamicSynthTuning) &&
                   IsDynamicMusicVaultHandle(in _outputAHandle, BufferID.AudioDynamicSynthOutputA) &&
                   IsDynamicMusicVaultHandle(in _outputBHandle, BufferID.AudioDynamicSynthOutputB) &&
                   IsDynamicMusicVaultHandle(in _biquadHandle, BufferID.AudioDynamicSynthBiquad) &&
                   IsDynamicMusicVaultHandle(in _telemetryRingHandle, BufferID.AudioDynamicSynthTelemetry) &&
                   IsDynamicMusicVaultHandle(in _telemetryCursorHandle, BufferID.AudioDynamicSynthTelemetryCursor) &&
                   IsDynamicMusicVaultHandle(in _presetRulesHandle, BufferID.AudioDynamicSynthPresetRules) &&
                   IsDynamicMusicVaultHandle(in _grainBankHandle, BufferID.AudioDynamicSynthGrainBank) &&
                   IsDynamicMusicVaultHandle(in _sharedStateHandle, BufferID.AudioDynamicSynthSharedState)
#if UNITY_EDITOR
                   && IsDynamicMusicVaultHandle(in _csvScratchHandle, BufferID.AudioDynamicSynthCsvScratch)
#endif
                   ;
        }

        private bool IsReadOnlyHandleResolvable<T>(in VaultGenerationHandle<T> handle, int minimumLength)
            where T : struct
        {
            IDataVault vault = _dataVault;
            return vault != null &&
                   handle.Generation != 0u &&
                   vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly view) &&
                   view.Length >= minimumLength;
        }

        private bool TryAcquireSynthJobViews(int targetBuffer, out DynamicMusicVaultViews views, out int lockMask, out IDataVault lockedVault)
        {
            views = default;
            lockMask = 0;
            lockedVault = _dataVault;
            if (lockedVault == null || !AreDynamicMusicVaultHandlesExact())
                return false;

            if (!TryAcquireLockedView(lockedVault, in _voicesHandle, LockVoices, ref lockMask, out views.Voices) ||
                !TryAcquireLockedView(lockedVault, in _scalarHandle, LockScalar, ref lockMask, out views.Scalar) ||
                !TryAcquireLockedView(lockedVault, in _tuningHandle, LockTuning, ref lockMask, out views.Tuning) ||
                !TryAcquireLockedView(lockedVault, in _biquadHandle, LockBiquad, ref lockMask, out views.Biquad) ||
                !TryAcquireLockedView(lockedVault, in _grainBankHandle, LockGrainBank, ref lockMask, out views.GrainBank) ||
                !TryAcquireLockedView(lockedVault, in _telemetryRingHandle, LockTelemetryRing, ref lockMask, out views.TelemetryRing) ||
                !TryAcquireLockedView(lockedVault, in _telemetryCursorHandle, LockTelemetryCursor, ref lockMask, out views.TelemetryCursor) ||
                !TryAcquireLockedView(lockedVault, in _sharedStateHandle, LockSharedState, ref lockMask, out views.SharedState) ||
                (targetBuffer == 0 && !TryAcquireLockedView(lockedVault, in _outputAHandle, LockOutputA, ref lockMask, out views.OutputA)) ||
                (targetBuffer != 0 && !TryAcquireLockedView(lockedVault, in _outputBHandle, LockOutputB, ref lockMask, out views.OutputB)))
            {
                ReleaseDynamicMusicWriteLocks(lockedVault, lockMask);
                views = default;
                lockMask = 0;
                lockedVault = null;
                return false;
            }

            return true;
        }

        private bool TryAcquireColdInitViews(out DynamicMusicVaultViews views, out int lockMask, out IDataVault lockedVault)
        {
            views = default;
            lockMask = 0;
            lockedVault = _dataVault;
            if (lockedVault == null || !AreDynamicMusicVaultHandlesExact())
                return false;

            if (!TryAcquireLockedView(lockedVault, in _voicesHandle, LockVoices, ref lockMask, out views.Voices) ||
                !TryAcquireLockedView(lockedVault, in _scalarHandle, LockScalar, ref lockMask, out views.Scalar) ||
                !TryAcquireLockedView(lockedVault, in _tuningHandle, LockTuning, ref lockMask, out views.Tuning) ||
                !TryAcquireLockedView(lockedVault, in _outputAHandle, LockOutputA, ref lockMask, out views.OutputA) ||
                !TryAcquireLockedView(lockedVault, in _outputBHandle, LockOutputB, ref lockMask, out views.OutputB) ||
                !TryAcquireLockedView(lockedVault, in _biquadHandle, LockBiquad, ref lockMask, out views.Biquad) ||
                !TryAcquireLockedView(lockedVault, in _telemetryRingHandle, LockTelemetryRing, ref lockMask, out views.TelemetryRing) ||
                !TryAcquireLockedView(lockedVault, in _telemetryCursorHandle, LockTelemetryCursor, ref lockMask, out views.TelemetryCursor) ||
#if UNITY_EDITOR
                !TryAcquireLockedView(lockedVault, in _csvScratchHandle, LockCsvScratch, ref lockMask, out views.CsvScratch) ||
#endif
                !TryAcquireLockedView(lockedVault, in _presetRulesHandle, LockPresetRules, ref lockMask, out views.PresetRules) ||
                !TryAcquireLockedView(lockedVault, in _grainBankHandle, LockGrainBank, ref lockMask, out views.GrainBank) ||
                !TryAcquireLockedView(lockedVault, in _sharedStateHandle, LockSharedState, ref lockMask, out views.SharedState))
            {
                ReleaseDynamicMusicWriteLocks(lockedVault, lockMask);
                views = default;
                lockMask = 0;
                lockedVault = null;
                return false;
            }

            return true;
        }

        private bool TryResolveSynthPublishViews(IDataVault lockedVault, int publishedBuffer, out DynamicMusicVaultViews views)
        {
            views = default;
            if (lockedVault == null || !AreDynamicMusicVaultHandlesExact())
                return false;

            bool outputResolved = publishedBuffer == 0
                ? lockedVault.TryResolveHandle(in _outputAHandle, out views.OutputA)
                : lockedVault.TryResolveHandle(in _outputBHandle, out views.OutputB);

            if (!outputResolved ||
                !lockedVault.TryResolveHandle(in _scalarHandle, out views.Scalar) ||
                !lockedVault.TryResolveHandle(in _tuningHandle, out views.Tuning) ||
                !lockedVault.TryResolveHandle(in _telemetryRingHandle, out views.TelemetryRing) ||
                !lockedVault.TryResolveHandle(in _telemetryCursorHandle, out views.TelemetryCursor) ||
                !lockedVault.TryResolveHandle(in _sharedStateHandle, out views.SharedState) ||
                !views.Scalar.IsCreated ||
                !views.Tuning.IsCreated ||
                !views.TelemetryRing.IsCreated ||
                !views.TelemetryCursor.IsCreated ||
                !views.SharedState.IsCreated ||
                (publishedBuffer == 0 && !views.OutputA.IsCreated) ||
                (publishedBuffer != 0 && !views.OutputB.IsCreated))
            {
                views = default;
                return false;
            }

            return true;
        }

        private static bool HasRequiredPublishLocks(int lockMask, int publishedBuffer)
        {
            int required = LockScalar |
                LockTuning |
                LockTelemetryRing |
                LockTelemetryCursor |
                LockSharedState |
                (publishedBuffer == 0 ? LockOutputA : LockOutputB);
            return (lockMask & required) == required;
        }

        private bool TryAcquireAudioCopyBuffer(int readyIndex, out NativeArray<float> buffer, out int lockMask, out IDataVault lockedVault)
        {
            buffer = default;
            lockMask = 0;
            lockedVault = _dataVault;
            if (lockedVault == null || !AreDynamicMusicVaultHandlesExact())
                return false;

            if (readyIndex == 0)
                return TryAcquireLockedView(lockedVault, in _outputAHandle, LockOutputA, ref lockMask, out buffer);

            if (readyIndex == 1)
                return TryAcquireLockedView(lockedVault, in _outputBHandle, LockOutputB, ref lockMask, out buffer);

            lockedVault = null;
            return false;
        }

        private bool TryAcquireLockedView<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int lockBit,
            ref int lockMask,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null ||
                handle.Generation == 0u ||
                !vault.TryAcquireWriteLock(in handle, VaultOwner, out buffer))
                return false;

            if (!buffer.IsCreated)
            {
                vault.ReleaseWriteLock(in handle, VaultOwner);
                buffer = default;
                return false;
            }

            lockMask |= lockBit;
            return true;
        }

        private void ReleaseOutstandingSynthJobLocks()
        {
            int lockMask = Interlocked.Exchange(ref _synthJobLockMask, 0);
            IDataVault lockedVault = _synthJobLockedVault;
            _synthJobLockedVault = null;
            ReleaseDynamicMusicWriteLocks(lockedVault, lockMask);
        }

        private void ReleaseDynamicMusicWriteLocks(IDataVault vault, int lockMask)
        {
            if (vault == null || lockMask == 0)
                return;

            if ((lockMask & LockVoices) != 0)
                vault.ReleaseWriteLock(in _voicesHandle, VaultOwner);
            if ((lockMask & LockScalar) != 0)
                vault.ReleaseWriteLock(in _scalarHandle, VaultOwner);
            if ((lockMask & LockTuning) != 0)
                vault.ReleaseWriteLock(in _tuningHandle, VaultOwner);
            if ((lockMask & LockOutputA) != 0)
                vault.ReleaseWriteLock(in _outputAHandle, VaultOwner);
            if ((lockMask & LockOutputB) != 0)
                vault.ReleaseWriteLock(in _outputBHandle, VaultOwner);
            if ((lockMask & LockBiquad) != 0)
                vault.ReleaseWriteLock(in _biquadHandle, VaultOwner);
            if ((lockMask & LockTelemetryRing) != 0)
                vault.ReleaseWriteLock(in _telemetryRingHandle, VaultOwner);
            if ((lockMask & LockTelemetryCursor) != 0)
                vault.ReleaseWriteLock(in _telemetryCursorHandle, VaultOwner);
            if ((lockMask & LockPresetRules) != 0)
                vault.ReleaseWriteLock(in _presetRulesHandle, VaultOwner);
            if ((lockMask & LockGrainBank) != 0)
                vault.ReleaseWriteLock(in _grainBankHandle, VaultOwner);
            if ((lockMask & LockSharedState) != 0)
                vault.ReleaseWriteLock(in _sharedStateHandle, VaultOwner);
#if UNITY_EDITOR
            if ((lockMask & LockCsvScratch) != 0)
                vault.ReleaseWriteLock(in _csvScratchHandle, VaultOwner);
#endif
        }

        private void ConfigureAudioHostCold()
        {
            if (_hostSource == null && !TryGetComponent(out _hostSource))
                return;

            int sampleRate = math.max(8000, AudioSettings.outputSampleRate);
            _hostSource.playOnAwake = true;
            _hostSource.loop = true;
            _hostSource.spatialBlend = 0f;
            _hostSource.dopplerLevel = 0f;
            _hostSource.volume = 1f;

            if (_driverClip != null && _hostSource.clip != _driverClip)
                _hostSource.clip = _driverClip;

            if (_hostSource.clip == null)
            {
                Volatile.Write(ref _audioHostConfigDirty, 0);
                return;
            }

            if (!_hostSource.isPlaying && Application.isPlaying)
                _hostSource.Play();
        }

        private void TryRefreshScalabilityStateHandleCold()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
            {
                _scalabilityStateHandle = default;
                return;
            }

            if (!vault.TryGetGenerationHandle<ScalabilityStateDTO>(BufferID.ShinobuScalabilityState, out _scalabilityStateHandle))
            {
                _scalabilityStateHandle = default;
            }
        }

        private void RefreshGlobalQualitySnapshotCold()
        {
            if (TryResolveScalabilityState(out NativeArray<ScalabilityStateDTO>.ReadOnly scalabilityState) &&
                scalabilityState.Length > 0)
            {
                ScalabilityStateDTO state = scalabilityState[0];
                if (math.isfinite(state.GlobalQualityWeight))
                    _cachedGlobalQualityWeight = math.saturate(state.GlobalQualityWeight);
            }
            else
            {
                _cachedGlobalQualityWeight = math.saturate(HomeostasisBrain.GlobalQualityWeight);
            }
        }

        private void GenerateEmergencyMockAudioProfiles()
        {
            DynamicMusicVaultViews views = default;
            int lockMask = 0;
            IDataVault lockedVault = _dataVault;
            if (lockedVault == null || !AreDynamicMusicVaultHandlesExact())
                return;

            try
            {
                if (!TryAcquireLockedView(lockedVault, in _tuningHandle, LockTuning, ref lockMask, out views.Tuning) ||
                    !TryAcquireLockedView(lockedVault, in _scalarHandle, LockScalar, ref lockMask, out views.Scalar) ||
                    !TryAcquireLockedView(lockedVault, in _voicesHandle, LockVoices, ref lockMask, out views.Voices) ||
                    views.Tuning.Length <= 0 ||
                    views.Scalar.Length <= 0)
                    return;

                DynamicMusicSynthTuningDTO tuning = default;
                tuning.BasePitchHz = DefaultBasePitchHz;
                tuning.BaseGrainDensity = DefaultBaseGrainDensity;
                tuning.TensionMultiplier = DefaultTensionMultiplier;
                tuning.LfoFrequency = DefaultLfoFrequency;
                tuning.BaseVolume = DefaultBaseVolume;
                tuning.GrainSizeSeconds = DefaultGrainSizeSeconds;
                tuning.QualityMin = 0f;
                tuning.QualityMax = 1f;
                tuning.DepthMaxMeters = DefaultDepthMaxMeters;
                tuning.LpfMinHz = DefaultLpfMinHz;
                tuning.LpfDepthHzPerMeter = DefaultLpfDepthHzPerMeter;
                tuning.StereoWidth = DefaultStereoWidth;
                tuning.DensityTensionScale = 0.85f;
                tuning.DetuneCentsMax = DefaultDetuneCentsMax;
                tuning.StingerDecaySeconds = DefaultStingerDecaySeconds;
                tuning.NoiseFoldback = 0.23f;
                tuning.SeedBase = DefaultSynthSeed;
                tuning.WaveformHash = DefaultWaveformHash;
                tuning.PresetHash = 0xA8A55335u;
                tuning.Flags = FlagProceduralOnly;
                views.Tuning[0] = SanitizeTuning(tuning);

                DynamicMusicSynthScalarDTO scalar = default;
                scalar.GlobalQualityWeight = ResolveGlobalQualityWeightFromSnapshot();
                scalar.DepthMeters = math.max(0f, _mockDepthMeters);
                scalar.Depth01 = math.saturate(scalar.DepthMeters / math.max(0.0001f, tuning.DepthMaxMeters));
                scalar.TensionIndex = math.saturate(_mockTensionBias01);
                scalar.LpfCutoffHz = math.max(tuning.LpfMinHz, 22000f - scalar.DepthMeters * tuning.LpfDepthHzPerMeter);
                scalar.ActiveVoices = 16;
                views.Scalar[0] = scalar;

                NativeArray<SynthVoiceDTO> voices = views.Voices;
                for (int i = 0; i < voices.Length; i++)
                {
                    uint hash = Hash32((uint)i ^ tuning.SeedBase);
                    SynthVoiceDTO voice = default;
                    voice.CurrentPhase = HashToUnit(hash);
                    voice.PhaseIncrement = tuning.BasePitchHz / math.max(8000f, AudioSettings.outputSampleRate);
                    voice.EnvelopeState = 1f;
                    voice.SoundHash = hash == 0u ? 1u : hash;
                    voice.TargetPitch = tuning.BasePitchHz;
                    voice.TargetVolume = 0f;
                    voices[i] = voice;
                }
            }
            finally
            {
                ReleaseDynamicMusicWriteLocks(lockedVault, lockMask);
            }
        }

        private void GenerateDefaultGrainBankCold()
        {
            DynamicMusicVaultViews views = default;
            int lockMask = 0;
            IDataVault lockedVault = _dataVault;
            if (lockedVault == null || !AreDynamicMusicVaultHandlesExact())
                return;

            try
            {
                if (!TryAcquireLockedView(lockedVault, in _grainBankHandle, LockGrainBank, ref lockMask, out views.GrainBank))
                    return;

                NativeArray<float> grainBank = views.GrainBank;
                if (!grainBank.IsCreated)
                    return;

                for (int i = 0; i < grainBank.Length; i++)
                {
                    float phase = i / math.max(1f, (float)grainBank.Length);
                    float scrape = MathLodApproximation.ApproxSinBhaskara(phase * math.PI * 2f) * 0.55f;
                    scrape += MathLodApproximation.ApproxSinBhaskara(phase * math.PI * 9.7f + 0.8f) * 0.22f;
                    scrape += MathLodApproximation.ApproxSinBhaskara(phase * math.PI * 37.1f + 1.7f) * 0.08f;
                    float bow = (HashToUnit(Hash32((uint)i * 747796405u + DefaultSynthSeed)) - 0.5f) * 0.16f;
                    float envelope = MathLodApproximation.ApproxSinBhaskara(phase * math.PI);
                    grainBank[i] = math.clamp((scrape + bow) * envelope, -1f, 1f);
                }
            }
            finally
            {
                ReleaseDynamicMusicWriteLocks(lockedVault, lockMask);
            }
        }

        private void DrainSignalInputs()
        {
            float damageImpulse = math.saturate(_pendingDamageImpulse);
            float stingerImpulse = math.saturate(_pendingStingerImpulse);
            _pendingDamageImpulse = 0f;

            ReadOnlySpan<DynamicMusicScalarSignal> musicSignals = SignalBus<DynamicMusicScalarSignal>.GetFrameSnapshot();
            for (int i = 0; i < musicSignals.Length; i++)
            {
                DynamicMusicScalarSignal signal = musicSignals[i];
                if ((signal.Flags & DynamicMusicScalarSignal.FlagExternalScalars) != 0u)
                {
                    _externalTension01 = math.saturate(FiniteOrFallback(signal.Tension01, 0f));
                    _externalDepthMeters = math.max(0f, FiniteOrFallback(signal.DepthMeters, 0f));
                    _externalQualityWeight = math.saturate(FiniteOrFallback(signal.GlobalQualityWeight, 1f));
                    _externalDamageImpulse01 = math.saturate(FiniteOrFallback(signal.DamageImpulse01, 0f));
                    _externalScalarPublished = 1;
                }

                damageImpulse = math.max(damageImpulse, math.saturate(FiniteOrFallback(signal.DamageImpulse01, 0f)));
                damageImpulse = math.max(damageImpulse, math.saturate(FiniteOrFallback(signal.PitchKick01, 0f)));
                stingerImpulse = math.max(stingerImpulse, math.saturate(FiniteOrFallback(signal.StingerImpulse01, 0f)));
            }

            ReadOnlySpan<CombatDamageSignal> damageSignals = SignalBus<CombatDamageSignal>.GetFrameSnapshot();
            for (int i = 0; i < damageSignals.Length; i++)
            {
                CombatDamageSignal signal = damageSignals[i];
                float magnitude01 = math.saturate(signal.Magnitude * 0.01f);
                damageImpulse = math.max(damageImpulse, magnitude01);
                stingerImpulse = math.max(stingerImpulse, math.saturate(magnitude01 * 1.25f));
            }

            ReadOnlySpan<HullDeformedSignal> hullSignals = SignalBus<HullDeformedSignal>.GetFrameSnapshot();
            for (int i = 0; i < hullSignals.Length; i++)
            {
                HullDeformedSignal signal = hullSignals[i];
                float hullImpulse = math.saturate(math.max(signal.Intensity01, math.abs(signal.Depth) * 0.2f));
                damageImpulse = math.max(damageImpulse, hullImpulse * 0.65f);
                stingerImpulse = math.max(stingerImpulse, hullImpulse * 1.15f);
            }

            ReadOnlySpan<WaterlineBreachSignal> breachSignals = SignalBus<WaterlineBreachSignal>.GetFrameSnapshot();
            for (int i = 0; i < breachSignals.Length; i++)
            {
                WaterlineBreachSignal signal = breachSignals[i];
                float breachImpulse = math.saturate(signal.Intensity01);
                damageImpulse = math.max(damageImpulse, breachImpulse * 0.45f);
                stingerImpulse = math.max(stingerImpulse, breachImpulse * 1.35f);
            }

            _pendingStingerImpulse = math.saturate(stingerImpulse);
            _pendingDamageImpulse = damageImpulse;
        }

        private static void EnsureDynamicMusicSignalLaneCold()
        {
            SignalBus<DynamicMusicScalarSignal>.Configure(
                expectedCapacity: DynamicMusicScalarSignal.ExpectedCapacity,
                maxFrameSignals: DynamicMusicScalarSignal.MaxFrameSignals,
                lowTierFrameSignals: DynamicMusicScalarSignal.LowTierFrameSignals,
                laneHash: DynamicMusicScalarSignal.LaneHash);
            SignalBus<DynamicMusicScalarSignal>.EnsureInitialized();
        }

        private void ScheduleSynthJobs(float deltaSeconds)
        {
            int readyIndex = Volatile.Read(ref _readyBufferIndex);
            int copyIndex = Volatile.Read(ref _audioCopyBufferIndex);
            int targetBuffer = readyIndex == 0 ? 1 : 0;
            if (targetBuffer == copyIndex)
                return;

            if (Volatile.Read(ref _synthJobLockMask) != 0)
                ReleaseOutstandingSynthJobLocks();

            if (!TryAcquireSynthJobViews(targetBuffer, out DynamicMusicVaultViews views, out int lockMask, out IDataVault lockedVault))
            {
                Interlocked.Increment(ref _audioOverflowCount);
                return;
            }

            bool scheduled = false;
            try
            {
                if (!views.Voices.IsCreated ||
                    !views.Scalar.IsCreated ||
                    !views.Tuning.IsCreated ||
                    !views.GrainBank.IsCreated)
                    return;

                int requestedSamples = Volatile.Read(ref _lastAudioRequestSamples);
                int requestedChannels = math.clamp(Volatile.Read(ref _lastAudioChannels), 1, 2);
                int sampleCount = math.clamp(requestedSamples > 0 ? requestedSamples : DefaultScheduleSamples, requestedChannels, OutputSampleCapacity);
                sampleCount -= sampleCount % requestedChannels;
                if (sampleCount <= 0)
                    return;

                NativeArray<float> outputBuffer = targetBuffer == 0 ? views.OutputA : views.OutputB;
                if (!outputBuffer.IsCreated || outputBuffer.Length < sampleCount)
                    return;

                float* output = targetBuffer == 0
                    ? (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.OutputA)
                    : (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.OutputB);

                DynamicMusicSynthTuningDTO tuning = views.Tuning[0];
                float globalQuality = ResolveGlobalQualityWeightFromSnapshot();
                bool hasExternalScalars = _externalScalarPublished != 0;
                float externalTension = math.saturate(_externalTension01);
                float externalDepthMeters = math.max(0f, _externalDepthMeters);
                float externalQuality = hasExternalScalars ? math.saturate(_externalQualityWeight) : 1f;
                float damageImpulse = math.saturate(_pendingDamageImpulse);
                damageImpulse = math.saturate(math.max(damageImpulse, _externalDamageImpulse01));
                float stingerImpulse = math.saturate(_pendingStingerImpulse);
                _pendingDamageImpulse = 0f;
                _externalDamageImpulse01 = 0f;

                GenerateMockTensionJob mockJob = default;
                mockJob.Scalar = (DynamicMusicSynthScalarDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.Scalar);
                mockJob.Tuning = (DynamicMusicSynthTuningDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.Tuning);
                mockJob.FrameIndex = _simulationFrameCounter;
                mockJob.DeltaSeconds = deltaSeconds;
                mockJob.HasExternalScalars = hasExternalScalars ? 1 : 0;
                mockJob.ExternalTension01 = math.saturate(math.max(math.max(_mockTensionBias01, hasExternalScalars ? externalTension : 0f), damageImpulse));
                mockJob.ExternalDepthMeters = hasExternalScalars ? externalDepthMeters : math.max(0f, _mockDepthMeters);
                mockJob.DamageImpulse01 = damageImpulse;
                mockJob.StingerImpulse01 = math.max(stingerImpulse, damageImpulse);
                mockJob.GlobalQualityWeight = math.saturate(globalQuality * math.saturate(_mockQualityBias01) * externalQuality);
                JobHandle mockHandle = mockJob.Schedule();

                ModulateSynthParametersJob modulateJob = default;
                modulateJob.Voices = (SynthVoiceDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.Voices);
                modulateJob.Scalar = (DynamicMusicSynthScalarDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.Scalar);
                modulateJob.Tuning = (DynamicMusicSynthTuningDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.Tuning);
                modulateJob.VoiceCapacityValue = VoiceCapacity;
                modulateJob.SampleRate = math.max(8000, AudioSettings.outputSampleRate);
                JobHandle modulateHandle = modulateJob.Schedule(mockHandle);

                GranularSynthesisJob synthJob = default;
                synthJob.Voices = (SynthVoiceDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.Voices);
                synthJob.Scalar = (DynamicMusicSynthScalarDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.Scalar);
                synthJob.Tuning = (DynamicMusicSynthTuningDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.Tuning);
                synthJob.Biquad = (DynamicMusicBiquadStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.Biquad);
                synthJob.GrainBank = (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(views.GrainBank);
                synthJob.Output = output;
                synthJob.GrainBankLength = views.GrainBank.Length;
                synthJob.OutputSampleCount = sampleCount;
                synthJob.Channels = requestedChannels;
                synthJob.SampleRate = math.max(8000, AudioSettings.outputSampleRate);
                synthJob.FrameIndex = _simulationFrameCounter;

                _jobBufferIndex = targetBuffer;
                _jobSampleCount = sampleCount;
                _jobChannels = requestedChannels;
                _synthJobStartTicks = Stopwatch.GetTimestamp();
                _synthJobHandle = synthJob.Schedule(modulateHandle);
                _synthJobLockedVault = lockedVault;
                Volatile.Write(ref _synthJobLockMask, lockMask);
                Volatile.Write(ref _synthJobPending, 1);
                scheduled = true;
                lockedVault = null;
                lockMask = 0;
                _ = tuning;
            }
            finally
            {
                if (!scheduled)
                    ReleaseDynamicMusicWriteLocks(lockedVault, lockMask);
            }
        }

        private bool TryFlushCompletedSynthJob()
        {
            if (Volatile.Read(ref _synthJobPending) == 0)
            {
                if (Volatile.Read(ref _synthJobLockMask) != 0)
                    ReleaseOutstandingSynthJobLocks();
                return true;
            }

            if (!_synthJobHandle.IsCompleted)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _synthJobHandle))
                return false;

            Volatile.Write(ref _synthJobPending, 0);
            try
            {
                float elapsedMicroseconds = ResolveElapsedMicroseconds(_synthJobStartTicks);
                PublishReadyBuffer(elapsedMicroseconds);
            }
            finally
            {
                ReleaseOutstandingSynthJobLocks();
            }

            return true;
        }

        private void ForceFlushSynthJobForShutdown()
        {
            if (Volatile.Read(ref _synthJobPending) == 0)
            {
                if (Volatile.Read(ref _synthJobLockMask) != 0)
                    ReleaseOutstandingSynthJobLocks();
                return;
            }

            DispatcherJobFence.TryComplete(ref _synthJobHandle, forceComplete: true);
            Volatile.Write(ref _synthJobPending, 0);
            try
            {
                PublishReadyBuffer(ResolveElapsedMicroseconds(_synthJobStartTicks));
            }
            finally
            {
                ReleaseOutstandingSynthJobLocks();
            }
        }

        private void PublishReadyBuffer(float elapsedMicroseconds)
        {
            int publishedBuffer = _jobBufferIndex;
            if (publishedBuffer < 0)
                return;

            IDataVault lockedVault = _synthJobLockedVault;
            int lockMask = Volatile.Read(ref _synthJobLockMask);
            if (!HasRequiredPublishLocks(lockMask, publishedBuffer) ||
                !TryResolveSynthPublishViews(lockedVault, publishedBuffer, out DynamicMusicVaultViews views))
                return;

            int sampleCount = math.clamp(_jobSampleCount, 0, OutputSampleCapacity);
            int channels = math.clamp(_jobChannels, 1, 2);
            sampleCount -= sampleCount % channels;
            DynamicMusicSynthScalarDTO scalar = views.Scalar.IsCreated && views.Scalar.Length > 0 ? views.Scalar[0] : default;
            uint flags = scalar.Flags;
            if (HasNonFiniteScalar(in scalar))
                flags |= FlagNonFinite;

            Volatile.Write(ref _readySampleCount, sampleCount);
            Volatile.Write(ref _readyBufferIndex, publishedBuffer);
            WriteSharedState(ref views, publishedBuffer, sampleCount, channels, elapsedMicroseconds, flags);
            WriteTelemetry(ref views, elapsedMicroseconds, publishedBuffer, sampleCount, flags);
            if (elapsedMicroseconds > DspDumpThresholdMicroseconds || (flags & FlagNonFinite) != 0u)
                DumpTelemetryOnce(ref views);

            float decaySeconds = views.Tuning.IsCreated && views.Tuning.Length > 0
                ? math.max(0.0001f, views.Tuning[0].StingerDecaySeconds)
                : DefaultStingerDecaySeconds;
            _pendingStingerImpulse = math.saturate(_pendingStingerImpulse * MathLodApproximation.ApproxExpNegPade33Wide40(MaximumDeltaSeconds / decaySeconds));
        }

        private void WriteSharedState(ref DynamicMusicVaultViews views, int readyBuffer, int sampleCount, int channels, float elapsedMicroseconds, uint flags)
        {
            if (!views.SharedState.IsCreated || views.SharedState.Length <= 0 || !views.Scalar.IsCreated || views.Scalar.Length <= 0)
                return;

            DynamicMusicSynthScalarDTO scalar = views.Scalar[0];
            DynamicMusicSharedStateDTO state = default;
            state.ReadyBufferIndex = readyBuffer;
            state.ReadySampleCount = sampleCount;
            state.PendingBufferIndex = Volatile.Read(ref _synthJobPending) != 0 ? _jobBufferIndex : -1;
            state.AudioCopyBufferIndex = Volatile.Read(ref _audioCopyBufferIndex);
            state.PublishedFrame = scalar.Frame;
            state.Channels = channels;
            state.AudioUnderrunCount = Volatile.Read(ref _audioUnderrunCount);
            state.AudioOverflowCount = Volatile.Read(ref _audioOverflowCount);
            state.LastDspMicroseconds = elapsedMicroseconds;
            state.LastActiveVoices = scalar.ActiveVoices;
            state.LastTensionIndex = scalar.TensionIndex;
            state.LastDepthMeters = scalar.DepthMeters;
            state.LastCutoffHz = scalar.LpfCutoffHz;
            state.Flags = flags;
            views.SharedState[0] = state;
        }

        private void WriteTelemetry(ref DynamicMusicVaultViews views, float elapsedMicroseconds, int readyBuffer, int sampleCount, uint flags)
        {
            if (!views.TelemetryRing.IsCreated ||
                !views.TelemetryCursor.IsCreated ||
                views.TelemetryRing.Length <= 0 ||
                views.TelemetryCursor.Length <= 0 ||
                !views.Scalar.IsCreated ||
                views.Scalar.Length <= 0)
                return;

            DynamicMusicSynthScalarDTO scalar = views.Scalar[0];
            int cursor = views.TelemetryCursor[0];
            int capacity = math.min(TelemetryCapacity, views.TelemetryRing.Length);
            int index = cursor % capacity;
            AudioDSPTelemetryEntry entry = default;
            entry.Frame = scalar.Frame;
            entry.ActiveVoices = scalar.ActiveVoices;
            entry.Flags = flags;
            entry.ReadyBufferIndex = readyBuffer;
            entry.TensionIndex = scalar.TensionIndex;
            entry.DepthMeters = scalar.DepthMeters;
            entry.LpfCutoffHz = scalar.LpfCutoffHz;
            entry.DspJobMicroseconds = elapsedMicroseconds;
            entry.QualityWeight = scalar.GlobalQualityWeight;
            entry.GrainDensity = scalar.BaseDensity;
            entry.TargetPitch = scalar.TargetPitch;
            entry.StingerImpulse = scalar.StingerImpulse;
            entry.OutputPeak = scalar.OutputPeak;
            entry.OutputRms = scalar.OutputRms;
            entry.AudioUnderrunCount = Volatile.Read(ref _audioUnderrunCount);
            entry.OutputSampleCount = (uint)sampleCount;
            views.TelemetryRing[index] = entry;
            views.TelemetryCursor[0] = (cursor + 1) % capacity;
        }

        private bool HasNonFiniteScalar(in DynamicMusicSynthScalarDTO scalar)
        {
            return !math.isfinite(scalar.TensionIndex) ||
                   !math.isfinite(scalar.DepthMeters) ||
                   !math.isfinite(scalar.GlobalQualityWeight) ||
                   !math.isfinite(scalar.LpfCutoffHz) ||
                   !math.isfinite(scalar.OutputPeak) ||
                   !math.isfinite(scalar.OutputRms);
        }

        private void DumpTelemetryOnce(ref DynamicMusicVaultViews views)
        {
            if (Interlocked.Exchange(ref _telemetryDumped, 1) != 0)
                return;

            try
            {
                if (!views.TelemetryRing.IsCreated || views.TelemetryRing.Length <= 0)
                    return;

                string repoRoot = ResolveRepoRootPath();
                string dumpPath = Path.Combine(repoRoot, DumpRelativePath);
                string directory = Path.GetDirectoryName(dumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                void* source = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.TelemetryRing);
                int byteCount = math.min(TelemetryCapacity, views.TelemetryRing.Length) * UnsafeUtility.SizeOf<AudioDSPTelemetryEntry>();
                using (FileStream stream = File.Open(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    ref byte payloadStart = ref UnsafeUtility.AsRef<byte>(source);
                    ReadOnlySpan<byte> payload = MemoryMarshal.CreateReadOnlySpan(ref payloadStart, byteCount);
                    stream.Write(payload);
                }
            }
            catch (Exception)
            {
            }
        }

        private void PollCsvRulesCold()
        {
#if UNITY_EDITOR
            if (Volatile.Read(ref _nativeAllocated) == 0)
                return;

            if (_csvPollCountdown > 0)
            {
                _csvPollCountdown--;
                return;
            }

            _csvPollCountdown = CsvPollSlowTickInterval;
            string path = ResolveCachedCsvPathCold();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            DateTime writeTime = File.GetLastWriteTimeUtc(path);
            if (writeTime == _lastCsvWriteUtc)
                return;

            ReloadSynthPresetCsvCold();
#endif
        }

#if UNITY_EDITOR
        private void ParseSynthPresetCsv(int byteCount, ref DynamicMusicVaultViews views)
        {
            if (byteCount <= 0 || !views.Tuning.IsCreated || views.Tuning.Length <= 0 || !views.CsvScratch.IsCreated)
                return;

            NativeArray<byte> csvScratch = views.CsvScratch;
            int safeByteCount = math.min(byteCount, csvScratch.Length);
            DynamicMusicSynthTuningDTO tuning = views.Tuning[0];
            DynamicMusicPresetRuleDTO rule = default;
            int ruleIndex = 0;
            int index = 0;
            while (index < safeByteCount)
            {
                while (index < safeByteCount && IsLineBreakOrSpace(csvScratch[index]))
                    index++;
                if (index >= safeByteCount)
                    break;

                if (csvScratch[index] == (byte)'#')
                {
                    while (index < safeByteCount && !IsLineBreak(csvScratch[index]))
                        index++;
                    continue;
                }

                int keyStart = index;
                while (index < safeByteCount && csvScratch[index] != (byte)',' && !IsLineBreak(csvScratch[index]))
                    index++;

                int keyEnd = index;
                if (index >= safeByteCount || csvScratch[index] != (byte)',')
                {
                    while (index < safeByteCount && !IsLineBreak(csvScratch[index]))
                        index++;
                    continue;
                }

                index++;
                int valueStart = index;
                while (index < safeByteCount && !IsLineBreak(csvScratch[index]))
                    index++;

                uint keyHash = HashCsvKey(csvScratch, keyStart, keyEnd);
                if (TryParseFloat(csvScratch, valueStart, index, out float value))
                {
                    ApplyCsvTuning(ref tuning, ref rule, keyHash, value);
                }
                else
                {
                    uint valueHash = HashCsvKey(csvScratch, valueStart, index);
                    ApplyCsvHash(ref tuning, ref rule, keyHash, valueHash);
                }

                while (index < safeByteCount && IsLineBreak(csvScratch[index]))
                    index++;
            }

            if (views.PresetRules.IsCreated && ruleIndex < views.PresetRules.Length && rule.PresetHash != 0u)
            {
                views.PresetRules[ruleIndex] = SanitizePresetRule(rule);
                ruleIndex++;
            }

            tuning.Flags |= FlagCsvApplied | FlagProceduralOnly;
            views.Tuning[0] = SanitizeTuning(tuning);
        }

        private static void ApplyCsvTuning(ref DynamicMusicSynthTuningDTO tuning, ref DynamicMusicPresetRuleDTO rule, uint hash, float value)
        {
            if (hash == CsvBasePitchHash)
            {
                tuning.BasePitchHz = value;
                rule.BasePitchHz = value;
            }
            else if (hash == CsvGrainSizeHash)
            {
                tuning.GrainSizeSeconds = value;
                rule.GrainSizeSeconds = value;
            }
            else if (hash == CsvBaseDensityHash)
            {
                tuning.BaseGrainDensity = value;
                rule.BaseDensity = value;
            }
            else if (hash == CsvTensionMultiplierHash)
            {
                tuning.TensionMultiplier = value;
                rule.TensionMultiplier = value;
            }
            else if (hash == CsvLfoFrequencyHash)
            {
                tuning.LfoFrequency = value;
                rule.LfoFrequency = value;
            }
            else if (hash == CsvBaseVolumeHash)
            {
                tuning.BaseVolume = value;
                rule.BaseVolume = value;
            }
            else if (hash == CsvQualityMinHash)
            {
                tuning.QualityMin = value;
                rule.QualityMin = value;
            }
            else if (hash == CsvQualityMaxHash)
            {
                tuning.QualityMax = value;
                rule.QualityMax = value;
            }
            else if (hash == CsvDepthMaxMetersHash)
            {
                tuning.DepthMaxMeters = value;
                rule.DepthMaxMeters = value;
            }
            else if (hash == CsvLpfMinHzHash)
            {
                tuning.LpfMinHz = value;
                rule.LpfMinHz = value;
            }
            else if (hash == CsvLpfDepthHzPerMeterHash)
            {
                tuning.LpfDepthHzPerMeter = value;
                rule.LpfDepthHzPerMeter = value;
            }
            else if (hash == CsvStereoWidthHash)
            {
                tuning.StereoWidth = value;
            }
        }

        private static void ApplyCsvHash(ref DynamicMusicSynthTuningDTO tuning, ref DynamicMusicPresetRuleDTO rule, uint keyHash, uint valueHash)
        {
            if (keyHash == CsvWaveformHash)
            {
                tuning.WaveformHash = valueHash;
                rule.WaveformHash = valueHash;
            }
            else if (keyHash == CsvPresetHash)
            {
                tuning.PresetHash = valueHash;
                rule.PresetHash = valueHash;
            }
            else if (keyHash == CsvBiomeHash)
            {
                rule.BiomeHash = valueHash;
            }
            else if (keyHash == CsvNarrativeHash)
            {
                rule.NarrativeHash = valueHash;
            }
        }

        private void RefreshCsvPathCold()
        {
            _resolvedCsvPath = null;
            _lastResolvedCsvRelativePath = null;
            _lastCsvWriteUtc = default;
            _csvPollCountdown = 0;
            ResolveCachedCsvPathCold();
        }

        private string ResolveCachedCsvPathCold()
        {
            string relative = string.IsNullOrEmpty(_csvRelativePath) ? CsvDefaultRelativePath : _csvRelativePath;
            if (!string.IsNullOrEmpty(_resolvedCsvPath) &&
                string.Equals(_lastResolvedCsvRelativePath, relative, StringComparison.Ordinal))
            {
                return _resolvedCsvPath;
            }

            _lastCsvWriteUtc = default;
            _lastResolvedCsvRelativePath = relative;
            _resolvedCsvPath = Path.IsPathRooted(relative)
                ? relative
                : Path.Combine(ResolveRepoRootPath(), relative);
            return _resolvedCsvPath;
        }

#endif

        private float ResolveGlobalQualityWeightFromSnapshot()
        {
            if (TryResolveScalabilityState(out NativeArray<ScalabilityStateDTO>.ReadOnly scalabilityState) &&
                scalabilityState.Length > 0)
            {
                ScalabilityStateDTO state = scalabilityState[0];
                if (math.isfinite(state.GlobalQualityWeight))
                {
                    _cachedGlobalQualityWeight = math.saturate(state.GlobalQualityWeight);
                    return _cachedGlobalQualityWeight;
                }
            }

            return math.saturate(_cachedGlobalQualityWeight);
        }

        private bool TryResolveScalabilityState(out NativeArray<ScalabilityStateDTO>.ReadOnly scalabilityState)
        {
            scalabilityState = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   _scalabilityStateHandle.BufferID != 0u &&
                   vault.TryReadOnlyHandle(in _scalabilityStateHandle, out scalabilityState) &&
                   scalabilityState.Length > 0;
        }

        private static DynamicMusicSynthTuningDTO SanitizeTuning(DynamicMusicSynthTuningDTO tuning)
        {
            tuning.BasePitchHz = math.clamp(FiniteOrFallback(tuning.BasePitchHz, DefaultBasePitchHz), 8f, 880f);
            tuning.BaseGrainDensity = math.clamp(FiniteOrFallback(tuning.BaseGrainDensity, DefaultBaseGrainDensity), 1f, 128f);
            tuning.TensionMultiplier = math.clamp(FiniteOrFallback(tuning.TensionMultiplier, DefaultTensionMultiplier), 0f, 4f);
            tuning.LfoFrequency = math.clamp(FiniteOrFallback(tuning.LfoFrequency, DefaultLfoFrequency), 0.01f, 12f);
            tuning.BaseVolume = math.clamp(FiniteOrFallback(tuning.BaseVolume, DefaultBaseVolume), 0f, 1f);
            tuning.GrainSizeSeconds = math.clamp(FiniteOrFallback(tuning.GrainSizeSeconds, DefaultGrainSizeSeconds), 0.01f, 0.5f);
            tuning.QualityMin = math.saturate(FiniteOrFallback(tuning.QualityMin, 0f));
            tuning.QualityMax = math.max(tuning.QualityMin + 0.0001f, math.saturate(FiniteOrFallback(tuning.QualityMax, 1f)));
            tuning.DepthMaxMeters = math.max(1f, FiniteOrFallback(tuning.DepthMaxMeters, DefaultDepthMaxMeters));
            tuning.LpfMinHz = math.clamp(FiniteOrFallback(tuning.LpfMinHz, DefaultLpfMinHz), 20f, 22000f);
            tuning.LpfDepthHzPerMeter = math.clamp(FiniteOrFallback(tuning.LpfDepthHzPerMeter, DefaultLpfDepthHzPerMeter), 0.01f, 40f);
            tuning.StereoWidth = math.saturate(FiniteOrFallback(tuning.StereoWidth, DefaultStereoWidth));
            tuning.DensityTensionScale = math.clamp(FiniteOrFallback(tuning.DensityTensionScale, 0.85f), 0f, 3f);
            tuning.DetuneCentsMax = math.clamp(FiniteOrFallback(tuning.DetuneCentsMax, DefaultDetuneCentsMax), 0f, 120f);
            tuning.StingerDecaySeconds = math.clamp(FiniteOrFallback(tuning.StingerDecaySeconds, DefaultStingerDecaySeconds), 0.01f, 8f);
            tuning.NoiseFoldback = math.saturate(FiniteOrFallback(tuning.NoiseFoldback, 0.23f));
            if (tuning.SeedBase == 0u)
                tuning.SeedBase = DefaultSynthSeed;
            if (tuning.WaveformHash == 0u)
                tuning.WaveformHash = DefaultWaveformHash;
            return tuning;
        }

        private static DynamicMusicPresetRuleDTO SanitizePresetRule(DynamicMusicPresetRuleDTO rule)
        {
            rule.BasePitchHz = math.clamp(FiniteOrFallback(rule.BasePitchHz, DefaultBasePitchHz), 20f, 1000f);
            rule.GrainSizeSeconds = math.clamp(FiniteOrFallback(rule.GrainSizeSeconds, DefaultGrainSizeSeconds), 0.01f, 0.4f);
            rule.BaseDensity = math.clamp(FiniteOrFallback(rule.BaseDensity, DefaultBaseGrainDensity), 1f, 256f);
            rule.TensionMultiplier = math.clamp(FiniteOrFallback(rule.TensionMultiplier, DefaultTensionMultiplier), 0f, 4f);
            rule.LfoFrequency = math.clamp(FiniteOrFallback(rule.LfoFrequency, DefaultLfoFrequency), 0f, 16f);
            rule.BaseVolume = math.clamp(FiniteOrFallback(rule.BaseVolume, DefaultBaseVolume), 0f, 1f);
            rule.QualityMin = math.saturate(FiniteOrFallback(rule.QualityMin, 0f));
            rule.QualityMax = math.saturate(FiniteOrFallback(rule.QualityMax, 1f));
            rule.DepthMaxMeters = math.max(1f, FiniteOrFallback(rule.DepthMaxMeters, DefaultDepthMaxMeters));
            rule.LpfMinHz = math.clamp(FiniteOrFallback(rule.LpfMinHz, DefaultLpfMinHz), 20f, 22000f);
            rule.LpfDepthHzPerMeter = math.clamp(FiniteOrFallback(rule.LpfDepthHzPerMeter, DefaultLpfDepthHzPerMeter), 0.01f, 40f);
            rule.Flags |= FlagCsvApplied | FlagProceduralOnly;
            return rule;
        }

#if UNITY_EDITOR
        private static bool TryParseFloat(NativeArray<byte> bytes, int start, int end, out float value)
        {
            value = 0f;
            int index = start;
            while (index < end && IsHorizontalSpace(bytes[index]))
                index++;

            float sign = 1f;
            if (index < end && bytes[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }
            else if (index < end && bytes[index] == (byte)'+')
            {
                index++;
            }

            float integer = 0f;
            bool hasDigit = false;
            while (index < end)
            {
                byte b = bytes[index];
                if (b < (byte)'0' || b > (byte)'9')
                    break;

                integer = integer * 10f + (b - (byte)'0');
                hasDigit = true;
                index++;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (index < end && bytes[index] == (byte)'.')
            {
                index++;
                while (index < end)
                {
                    byte b = bytes[index];
                    if (b < (byte)'0' || b > (byte)'9')
                        break;

                    fraction = fraction * 10f + (b - (byte)'0');
                    divisor *= 10f;
                    hasDigit = true;
                    index++;
                }
            }

            if (!hasDigit)
                return false;

            value = sign * (integer + fraction / math.max(1f, divisor));
            return math.isfinite(value);
        }

        private static uint HashCsvKey(NativeArray<byte> bytes, int start, int end)
        {
            uint hash = 2166136261u;
            for (int i = start; i < end; i++)
            {
                byte b = bytes[i];
                if (b == (byte)' ' || b == (byte)'\t')
                    continue;

                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);

                hash ^= b;
                hash *= 16777619u;
            }

            return hash;
        }

        private static bool IsLineBreak(byte value)
        {
            return value == (byte)'\r' || value == (byte)'\n';
        }

        private static bool IsHorizontalSpace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t';
        }

        private static bool IsLineBreakOrSpace(byte value)
        {
            return IsLineBreak(value) || IsHorizontalSpace(value);
        }
#endif

        private static void ZeroManagedAudioBuffer(float[] data, int start, int count)
        {
            int end = math.min(data.Length, start + count);
            for (int i = math.max(0, start); i < end; i++)
                data[i] = 0f;
        }

        private static float ResolveElapsedMicroseconds(long startTicks)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            return elapsedTicks * (1000000f / math.max(1f, (float)Stopwatch.Frequency));
        }

        private static string ResolveRepoRootPath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static float FiniteOrFallback(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - 2f * x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash32(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value == 0u ? 1u : value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float HashToUnit(uint hash)
        {
            return (hash & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private static void MemClearArray<T>(NativeArray<T> array)
            where T : unmanaged
        {
            if (!array.IsCreated || array.Length == 0)
                return;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(array);
            UnsafeUtility.MemClear(ptr, (long)UnsafeUtility.SizeOf<T>() * array.Length);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct GenerateMockTensionJob : IJob
        {
            [NoAlias] [NativeDisableUnsafePtrRestriction] public DynamicMusicSynthScalarDTO* Scalar;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public DynamicMusicSynthTuningDTO* Tuning;
            public uint FrameIndex;
            public int HasExternalScalars;
            public float DeltaSeconds;
            public float ExternalTension01;
            public float ExternalDepthMeters;
            public float DamageImpulse01;
            public float StingerImpulse01;
            public float GlobalQualityWeight;

            public void Execute()
            {
                ref DynamicMusicSynthScalarDTO scalar = ref UnsafeUtility.AsRef<DynamicMusicSynthScalarDTO>(Scalar);
                ref readonly DynamicMusicSynthTuningDTO tuning = ref UnsafeUtility.AsRef<DynamicMusicSynthTuningDTO>(Tuning);

                float frame = FrameIndex;
                float slowPhase = frame * 0.00137f;
                float dangerInner = MathLodApproximation.ApproxSinBhaskara(frame * 0.0031f);
                float dangerWave = 0.5f + 0.5f * MathLodApproximation.ApproxSinBhaskara(frame * 0.021f + dangerInner);
                float depthWave = 0.5f + 0.5f * MathLodApproximation.ApproxSinBhaskara(slowPhase);
                float fallbackDepthMeters = math.max(ExternalDepthMeters, depthWave * tuning.DepthMaxMeters);
                float depthMeters = HasExternalScalars != 0 ? ExternalDepthMeters : fallbackDepthMeters;
                float fallbackTension = dangerWave * 0.35f + DamageImpulse01 * 0.85f;
                float tension = HasExternalScalars != 0
                    ? math.saturate(math.max(ExternalTension01, DamageImpulse01 * 0.85f))
                    : math.saturate(math.max(ExternalTension01, fallbackTension));
                float depth01 = math.saturate(depthMeters / math.max(0.0001f, tuning.DepthMaxMeters));
                float cutoff = math.max(tuning.LpfMinHz, 22000f - depthMeters * tuning.LpfDepthHzPerMeter);

                scalar.Frame = FrameIndex;
                scalar.Flags = (HasExternalScalars != 0 ? 0u : FlagUsingMockTension) | tuning.Flags;
                scalar.TensionIndex = math.saturate(FiniteOrFallback(tension, 0f));
                scalar.DepthMeters = FiniteOrFallback(depthMeters, 0f);
                scalar.Depth01 = depth01;
                scalar.GlobalQualityWeight = math.saturate(FiniteOrFallback(GlobalQualityWeight, 1f));
                scalar.DamageImpulse01 = math.saturate(FiniteOrFallback(DamageImpulse01, 0f));
                scalar.StingerImpulse = math.saturate(math.max(FiniteOrFallback(StingerImpulse01, 0f), scalar.StingerImpulse));
                scalar.LfoFrequency = tuning.LfoFrequency;
                scalar.LpfCutoffHz = math.clamp(FiniteOrFallback(cutoff, 22000f), tuning.LpfMinHz, 22000f);
                _ = DeltaSeconds;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct ModulateSynthParametersJob : IJob
        {
            [NoAlias] [NativeDisableUnsafePtrRestriction] public SynthVoiceDTO* Voices;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public DynamicMusicSynthScalarDTO* Scalar;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public DynamicMusicSynthTuningDTO* Tuning;
            public int VoiceCapacityValue;
            public int SampleRate;

            public void Execute()
            {
                ref DynamicMusicSynthScalarDTO scalar = ref UnsafeUtility.AsRef<DynamicMusicSynthScalarDTO>(Scalar);
                ref readonly DynamicMusicSynthTuningDTO tuning = ref UnsafeUtility.AsRef<DynamicMusicSynthTuningDTO>(Tuning);
                float qualityDenominator = math.max(0.0001f, tuning.QualityMax - tuning.QualityMin);
                float q = math.saturate((scalar.GlobalQualityWeight - tuning.QualityMin) / qualityDenominator);
                float qSmooth = Smooth01(q);
                int activeVoices = math.clamp((int)math.lerp(16f, 128f, qSmooth), 1, math.max(1, VoiceCapacityValue));
                float tension = math.saturate(scalar.TensionIndex * tuning.TensionMultiplier);
                float density = tuning.BaseGrainDensity * math.lerp(0.55f, 1.85f, tension) * math.lerp(0.5f, 1.15f, qSmooth);
                float lfoPhase = scalar.Frame * tuning.LfoFrequency * 0.016666668f;
                float heartbeat = 0.55f + 0.45f * MathLodApproximation.ApproxSinBhaskara(lfoPhase * math.PI * 2f);
                float pitchBend = 1f + tension * 0.18f + scalar.DamageImpulse01 * 0.32f;
                float baseVolume = tuning.BaseVolume * math.lerp(0.72f, 1.15f, tension) * math.lerp(0.7f, 1f, qSmooth);
                float normalization = math.rsqrt(math.max(1f, activeVoices));
                float safeSampleRate = math.max(8000f, SampleRate);

                scalar.BaseDensity = density;
                scalar.TargetPitch = tuning.BasePitchHz * pitchBend;
                scalar.TargetVolume = baseVolume * heartbeat;
                scalar.ActiveVoices = activeVoices;

                for (int i = 0; i < VoiceCapacityValue; i++)
                {
                    ref SynthVoiceDTO voice = ref UnsafeUtility.AsRef<SynthVoiceDTO>(Voices + i);
                    uint hash = voice.SoundHash != 0u ? voice.SoundHash : Hash32((uint)i ^ tuning.SeedBase);
                    float signedUnit = HashToUnit(hash) * 2f - 1f;
                    float cents = signedUnit * tuning.DetuneCentsMax * tension;
                    float detuneExponent = cents * 0.00057762265f;
                    float detuneUp = MathLodApproximation.ApproxExpPositivePade33Reduced(detuneExponent);
                    float detuneDown = MathLodApproximation.ApproxExpNegPade33Reduced(-detuneExponent);
                    float detune = math.select(detuneUp, detuneDown, detuneExponent < 0f);
                    float densityPush = math.lerp(0.75f, 1.5f, math.saturate(density / 128f));
                    float targetPitch = tuning.BasePitchHz * pitchBend * detune * densityPush;
                    float activeMask = i < activeVoices ? 1f : 0f;
                    voice.PhaseIncrement = math.clamp(targetPitch / safeSampleRate, 0.000001f, 0.25f);
                    voice.TargetPitch = targetPitch;
                    voice.TargetVolume = activeMask * baseVolume * normalization * (0.82f + HashToUnit(Hash32(hash ^ 0xA53A9D1Du)) * 0.36f);
                    voice.EnvelopeState = math.saturate(math.max(voice.EnvelopeState, activeMask));
                    voice.SoundHash = hash;
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct GranularSynthesisJob : IJob
        {
            [NoAlias] [NativeDisableUnsafePtrRestriction] public SynthVoiceDTO* Voices;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public DynamicMusicSynthScalarDTO* Scalar;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public DynamicMusicSynthTuningDTO* Tuning;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public DynamicMusicBiquadStateDTO* Biquad;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public float* GrainBank;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public float* Output;
            public int GrainBankLength;
            public int OutputSampleCount;
            public int Channels;
            public int SampleRate;
            public uint FrameIndex;

            public void Execute()
            {
                ref DynamicMusicSynthScalarDTO scalar = ref UnsafeUtility.AsRef<DynamicMusicSynthScalarDTO>(Scalar);
                ref readonly DynamicMusicSynthTuningDTO tuning = ref UnsafeUtility.AsRef<DynamicMusicSynthTuningDTO>(Tuning);
                ref DynamicMusicBiquadStateDTO biquad = ref UnsafeUtility.AsRef<DynamicMusicBiquadStateDTO>(Biquad);

                int safeChannels = math.clamp(Channels, 1, 2);
                int frameCount = OutputSampleCount / safeChannels;
                int safeBankLength = math.max(1, GrainBankLength);
                int safeSampleRate = math.max(8000, SampleRate);
                int activeVoices = math.clamp(scalar.ActiveVoices, 1, VoiceCapacity);
                float lpfCutoff = math.clamp(scalar.LpfCutoffHz, tuning.LpfMinHz, safeSampleRate * 0.45f);
                float stinger = math.saturate(scalar.StingerImpulse);
                float stereoWidth = math.saturate(tuning.StereoWidth);
                float qualityWeight = math.saturate(scalar.GlobalQualityWeight);
                float interpolationCurve = Smooth01(qualityWeight);
                float totalPeak = 0f;
                float totalEnergy = 0f;
                ComputeLowPassCoefficients(ref biquad, lpfCutoff, safeSampleRate);

                for (int frame = 0; frame < frameCount; frame++)
                {
                    float left = 0f;
                    float right = 0f;
                    for (int voiceIndex = 0; voiceIndex < activeVoices; voiceIndex++)
                    {
                        ref SynthVoiceDTO voice = ref UnsafeUtility.AsRef<SynthVoiceDTO>(Voices + voiceIndex);
                        uint hash = voice.SoundHash;
                        float phase = math.frac(voice.CurrentPhase);
                        float grainWindow = MathLodApproximation.ApproxSinBhaskara(phase * math.PI);
                        float offset = HashToUnit(Hash32(hash ^ tuning.WaveformHash));
                        float samplePhase = math.frac(phase + offset);
                        float position = samplePhase * (safeBankLength - 1);
                        int baseIndex = (int)position;
                        int nextIndex = math.min(baseIndex + 1, safeBankLength - 1);
                        float frac = (position - baseIndex) * interpolationCurve;
                        float grainSample = math.lerp(GrainBank[baseIndex], GrainBank[nextIndex], frac);
                        float fold = MathLodApproximation.ApproxSinBhaskara((grainSample + (HashToUnit(Hash32(hash ^ (uint)frame)) - 0.5f) * tuning.NoiseFoldback * scalar.TensionIndex) * math.PI);
                        float sample = math.lerp(grainSample, fold, math.saturate(scalar.TensionIndex * 0.35f + stinger * 0.55f));
                        sample *= grainWindow * voice.TargetVolume * (1f + stinger * 1.8f);

                        float pan = (HashToUnit(Hash32(hash ^ 0x9E3779B9u)) - 0.5f) * stereoWidth;
                        left += sample * (1f - math.max(0f, pan));
                        right += sample * (1f + math.min(0f, pan));

                        voice.CurrentPhase = math.frac(phase + voice.PhaseIncrement * math.lerp(0.65f, 1.45f, scalar.TensionIndex));
                    }

                    left = ApplyLowPass(ref biquad.Z1Left, ref biquad.Z2Left, biquad.A0, biquad.A1, biquad.A2, biquad.B1, biquad.B2, left);
                    right = ApplyLowPass(ref biquad.Z1Right, ref biquad.Z2Right, biquad.A0, biquad.A1, biquad.A2, biquad.B1, biquad.B2, right);
                    left = math.clamp(FiniteOrFallback(left, 0f), -1f, 1f);
                    right = math.clamp(FiniteOrFallback(right, 0f), -1f, 1f);
                    if (safeChannels == 1)
                    {
                        float mono = (left + right) * 0.5f;
                        Output[frame] = mono;
                        totalPeak = math.max(totalPeak, math.abs(mono));
                        totalEnergy += mono * mono;
                    }
                    else
                    {
                        int outIndex = frame << 1;
                        Output[outIndex] = left;
                        Output[outIndex + 1] = right;
                        totalPeak = math.max(totalPeak, math.max(math.abs(left), math.abs(right)));
                        totalEnergy += left * left + right * right;
                    }
                }

                scalar.Frame = FrameIndex;
                scalar.OutputPeak = totalPeak;
                scalar.OutputRms = math.sqrt(totalEnergy / math.max(1f, (float)OutputSampleCount));
                scalar.StingerImpulse = math.saturate(stinger * MathLodApproximation.ApproxExpNegPade33Wide40(frameCount / math.max(1f, tuning.StingerDecaySeconds * safeSampleRate)));
                if (!math.isfinite(scalar.OutputPeak) || !math.isfinite(scalar.OutputRms))
                    scalar.Flags |= FlagNonFinite;
            }

            private static void ComputeLowPassCoefficients(ref DynamicMusicBiquadStateDTO biquad, float cutoffHz, int sampleRate)
            {
                float safeRate = math.max(8000f, sampleRate);
                float normalized = math.clamp(cutoffHz / math.max(0.0001f, safeRate), 0.0001f, 0.45f);
                float k = MathLodApproximation.ApproxTanClamped(math.PI * normalized, 16f);
                float q = 0.70710678f;
                float norm = 1f / math.max(0.0001f, 1f + k / q + k * k);
                float a0 = k * k * norm;
                biquad.A0 = a0;
                biquad.A1 = 2f * a0;
                biquad.A2 = a0;
                biquad.B1 = 2f * (k * k - 1f) * norm;
                biquad.B2 = (1f - k / q + k * k) * norm;
                biquad.LastCutoffHz = cutoffHz;
                biquad.LastSampleRate = safeRate;
            }

            private static float ApplyLowPass(
                ref float z1,
                ref float z2,
                float a0,
                float a1,
                float a2,
                float b1,
                float b2,
                float input)
            {
                input = FiniteOrFallback(input, 0f);
                z1 = FiniteOrFallback(z1, 0f);
                z2 = FiniteOrFallback(z2, 0f);
                float output = a0 * input + z1;
                if (!math.isfinite(output))
                {
                    z1 = 0f;
                    z2 = 0f;
                    return 0f;
                }

                z1 = FiniteOrFallback(a1 * input - b1 * output + z2, 0f);
                z2 = FiniteOrFallback(a2 * input - b2 * output, 0f);
                return output;
            }
        }
    }
}
