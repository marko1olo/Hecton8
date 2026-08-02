using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.UI;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;
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
        [FieldOffset(56)] public float MusicActivity01;
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
    public sealed unsafe class DynamicMusicGranularSynthesizer : MonoBehaviour, IColdTickable, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        public const int VoiceCapacity = 128;
        public const int TelemetryCapacity = 300;
        public const int PresetRuleCapacity = 32;
        public const int GrainBankSampleCapacity = 2048;
        public const int OutputSampleCapacity = 8192;

        private const int DefaultAudioChannels = 2;
        private const int DefaultScheduleSamples = 2048;
        private const int RuntimeDriverClipSampleCount = 256;
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
        private const float MinimumDeltaSeconds = 0.0001f;
        private const float MaximumDeltaSeconds = 0.25f;
        private const int MusicDirectorScalarGraceTicks = 12;
        private const uint DefaultSynthSeed = 0x51350B75u;
        private const uint DefaultWaveformHash = 0xC31105E5u;
        private const uint FlagNonFinite = 1u << 0;
        private const uint FlagUsingMockTension = 1u << 1;
        private const uint FlagAudioUnderrun = 1u << 2;
        private const uint FlagCsvApplied = 1u << 3;
        private const uint FlagProceduralOnly = 1u << 4;
        private const float InvHash24Max = 0.0000000596046483281042f;
        private const float InvStopwatchMicrosScale = 1000000f;
        private const float InvSqrtEpsilon = 0.000000000001f;
        private const SystemID VaultOwner = SystemID.AudioDynamicSynth;
        private const ulong GuardVoices = 1UL << ((int)BufferID.AudioDynamicSynthVoices & 31);
        private const ulong GuardScalar = 1UL << ((int)BufferID.AudioDynamicSynthScalar & 31);
        private const ulong GuardTuning = 1UL << ((int)BufferID.AudioDynamicSynthTuning & 31);
        private const ulong GuardOutputA = 1UL << ((int)BufferID.AudioDynamicSynthOutputA & 31);
        private const ulong GuardOutputB = 1UL << ((int)BufferID.AudioDynamicSynthOutputB & 31);
        private const ulong GuardBiquad = 1UL << ((int)BufferID.AudioDynamicSynthBiquad & 31);
        private const ulong GuardTelemetryRing = 1UL << ((int)BufferID.AudioDynamicSynthTelemetry & 31);
        private const ulong GuardTelemetryCursor = 1UL << ((int)BufferID.AudioDynamicSynthTelemetryCursor & 31);
        private const ulong GuardGrainBank = 1UL << ((int)BufferID.AudioDynamicSynthGrainBank & 31);
        private const ulong GuardSharedState = 1UL << ((int)BufferID.AudioDynamicSynthSharedState & 31);
        private const ulong GuardPresetRules = 1UL << ((int)BufferID.AudioDynamicSynthPresetRules & 31);
        private const int SynthJobLockVoices = 1 << 0;
        private const int SynthJobLockScalar = 1 << 1;
        private const int SynthJobLockTuning = 1 << 2;
        private const int SynthJobLockBiquad = 1 << 3;
        private const int SynthJobLockGrainBank = 1 << 4;
        private const int SynthJobLockOutput = 1 << 5;
#if UNITY_EDITOR
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
        [SerializeField] private bool _autoCreateRuntimeInstance = true;
        [SerializeField] private bool _allowMockPlaybackWithoutDirector;

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
        private VaultGenerationHandle<DynamicMusicPresetRuleDTO> _presetRulesHandle;
        private VaultGenerationHandle<float> _grainBankHandle;
        private VaultGenerationHandle<DynamicMusicSharedStateDTO> _sharedStateHandle;
        private VaultGenerationHandle<ScalabilityStateDTO> _scalabilityStateHandle;

        private IDataVault _dataVault;
        private IAudioService _cachedAudioService;
        private HectonMusicDirector _cachedMusicDirector;
        private SettingsManager _cachedSettingsManager;
        private AudioSource _hostSource;
        private AudioClip _runtimeDriverClip;
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
        private float _externalMusicActivity01;
        private int _externalScalarPublished;
        private int _musicDirectorScalarMissedTicks;
        private int _suppressReactiveMusicImpulses;
        private float _pendingDamageImpulse;
        private float _pendingStingerImpulse;
        private int _nativeAllocated;
        private int _registeredUpdate;
        private int _registeredLateFrame;
        private int _registeredColdTick;
        private int _registeredHotSwap;
        private int _audioHostConfigDirty;
        private int _synthJobPending;
        private int _jobBufferIndex = -1;
        private int _jobSampleCount;
        private int _jobChannels = DefaultAudioChannels;
        private int _readyBufferIndex = -1;
        private int _readySampleCount;
        private int _readyPublishSequence;
        private int _audioCopyBufferIndex = -1;
        private int _audioThreadPublishedBufferIndex = -1;
        private int _audioThreadCopiedPublishSequence;
        private int _audioThreadCopyASampleCount;
        private int _audioThreadCopyBSampleCount;
        private int _lastAudioRequestSamples = DefaultScheduleSamples;
        private int _lastAudioChannels = DefaultAudioChannels;
        private int _audioUnderrunCount;
        private int _audioOverflowCount;
        private int _invalidAudioFilterHost;
#if UNITY_EDITOR
        private int _csvPollCountdown;
#endif
        private uint _simulationFrameCounter;
        private long _synthJobStartTicks;
        private JobHandle _synthJobHandle;
        private NativeArray<float> _audioThreadCopyA;
        private NativeArray<float> _audioThreadCopyB;
        private int _synthJobLockedBufferMask;
        private int _synthJobLockedTargetBuffer = -1;

        public static bool TryGetActive(out DynamicMusicGranularSynthesizer synth)
        {
            synth = _activeInstance;
            return synth != null && Volatile.Read(ref synth._nativeAllocated) != 0;
        }

        /// <summary>
        /// Resolve-or-create the active granular synth owner for player builds.
        /// Player.prefab authors the component on a different GO than AudioListener; the old
        /// resolve-only path only TryGetComponent'd the listener host and silently no-op'd.
        /// MUST NOT parent onto the AudioListener GO - RejectInvalidAudioFilterHostCold disables
        /// any instance that shares a host with AudioListener (OnAudioFilterRead conflict).
        /// </summary>
        public static DynamicMusicGranularSynthesizer EnsureRuntimeInstance()
        {
            if (_activeInstance != null)
                return _activeInstance;

            if (!Application.isPlaying)
                return null;

            // Broad scene resolve first: prefab hosts synth away from the AudioListener GO.
            DynamicMusicGranularSynthesizer existing =
                Object.FindFirstObjectByType<DynamicMusicGranularSynthesizer>(FindObjectsInactive.Include);
            if (existing != null)
            {
                _activeInstance = existing;
                return existing;
            }

            // Walk player camera hierarchy (parent/children) without landing on the listener host.
            AudioListener listener = ResolvePlayerAudioListenerCold();
            if (listener != null)
            {
                Transform listenerTransform = listener.transform;
                existing = listenerTransform.GetComponentInParent<DynamicMusicGranularSynthesizer>(true);
                if (existing != null && !IsInvalidAudioFilterHost(existing))
                {
                    _activeInstance = existing;
                    return existing;
                }

                existing = listenerTransform.GetComponentInChildren<DynamicMusicGranularSynthesizer>(true);
                if (existing != null && !IsInvalidAudioFilterHost(existing))
                {
                    _activeInstance = existing;
                    return existing;
                }
            }

            // Player-build construction path: dedicated root (AudioSource via RequireComponent).
            // Never AddComponent on the AudioListener GO - that path self-disables in Awake/OnEnable.
            // Player-build construction path: no authored/bootstrap instance reachable.
            // Must construct in player builds when bootstrap reorders or skips registration.
            GameObject host = new GameObject("[DynamicMusicGranularSynthesizer]"); // COLD ALLOC
            DynamicMusicGranularSynthesizer created = host.AddComponent<DynamicMusicGranularSynthesizer>();
            _activeInstance = created;
            return created;
        }

        private static bool IsInvalidAudioFilterHost(DynamicMusicGranularSynthesizer synth)
        {
            return synth != null && synth.TryGetComponent<AudioListener>(out _);
        }


        public static void EnsureRuntimeInstanceForScene(Scene scene)
        {
            _ = scene;
            EnsureRuntimeInstance();
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

            if (playerCamera.TryGetComponent(out AudioListener listener))
                return listener;

            // Listener may live on a child of the player camera root.
            return playerCamera.GetComponentInChildren<AudioListener>(true);
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
            if (Volatile.Read(ref _nativeAllocated) == 0 ||
                Volatile.Read(ref _synthJobPending) != 0)
                return false;

            return TryWriteTuningSnapshot(in tuning);
        }

        private DynamicMusicSynthTuningDTO ReadTuningSnapshotOrDefault()
        {
            if (TryGetEditorTuning(out DynamicMusicSynthTuningDTO tuning))
                return tuning;

            return CreateDefaultSynthTuning();
        }

        private bool TryWriteTuningSnapshot(in DynamicMusicSynthTuningDTO tuning)
        {
            IDataVault lockedVault = _dataVault;
            if (lockedVault == null || !AreDynamicMusicVaultHandlesExact())
                return false;

            IDataVault guardVault = null;
            try
            {
                if (!TryAcquireDynamicMusicMutationView(lockedVault, in _tuningHandle, GuardTuning, out NativeArray<DynamicMusicSynthTuningDTO> tuningView, out guardVault) ||
                    tuningView.Length <= 0)
                    return false;

                tuningView[0] = SanitizeTuning(tuning);
                return true;
            }
            finally
            {
                ReleaseDynamicMusicMutationGuard(guardVault, GuardTuning);
            }
        }

        private bool TryWritePresetRuleSnapshot(in DynamicMusicPresetRuleDTO rule, int ruleIndex)
        {
            IDataVault lockedVault = _dataVault;
            if (lockedVault == null || !AreDynamicMusicVaultHandlesExact())
                return false;

            IDataVault guardVault = null;
            try
            {
                if (!TryAcquireDynamicMusicMutationView(lockedVault, in _presetRulesHandle, GuardPresetRules, out NativeArray<DynamicMusicPresetRuleDTO> presetRules, out guardVault) ||
                    ruleIndex < 0 ||
                    ruleIndex >= presetRules.Length)
                    return false;

                presetRules[ruleIndex] = SanitizePresetRule(rule);
                return true;
            }
            finally
            {
                ReleaseDynamicMusicMutationGuard(guardVault, GuardPresetRules);
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

        public bool TryGetEditorSharedState(out DynamicMusicSharedStateDTO state)
        {
            state = default;
            IDataVault vault = _dataVault;
            if (Volatile.Read(ref _nativeAllocated) == 0 ||
                vault == null ||
                !IsDynamicMusicVaultHandle(in _sharedStateHandle, BufferID.AudioDynamicSynthSharedState) ||
                !vault.TryReadOnlyHandle(in _sharedStateHandle, out NativeArray<DynamicMusicSharedStateDTO>.ReadOnly sharedState) ||
                sharedState.Length <= 0)
            {
                return false;
            }

            state = sharedState[0];
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

            DynamicMusicSynthTuningDTO tuning = ReadTuningSnapshotOrDefault();
            DynamicMusicPresetRuleDTO rule = default;
            if (!TryReadCsvIntoScratchAndParse(path, ref tuning, ref rule, out int ruleCount))
                return false;

            if (!TryWriteTuningSnapshot(in tuning))
                return false;

            if (ruleCount > 0 && !TryWritePresetRuleSnapshot(in rule, 0))
                return false;

            _lastCsvWriteUtc = File.GetLastWriteTimeUtc(path);
            return true;
        }

        private bool TryReadCsvIntoScratchAndParse(
            string path,
            ref DynamicMusicSynthTuningDTO tuning,
            ref DynamicMusicPresetRuleDTO rule,
            out int ruleCount)
        {
            ruleCount = 0;
            IDataVault lockedVault = _dataVault;
            if (lockedVault == null || !AreDynamicMusicVaultHandlesExact())
                return false;

            try
            {
                int bytesRead;
                int expectedBytes;
                Span<byte> csvScratch = stackalloc byte[CsvScratchBytes];
                using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long fileLength = stream.Length;
                    if (fileLength <= 0L || fileLength > CsvScratchBytes)
                        return false;

                    expectedBytes = (int)fileLength;
                    bytesRead = 0;
                    while (bytesRead < expectedBytes)
                    {
                        int read = stream.Read(csvScratch.Slice(bytesRead, expectedBytes - bytesRead));
                        if (read <= 0)
                            break;

                        bytesRead += read;
                    }
                }

                if (bytesRead != expectedBytes)
                    return false;

                ParseSynthPresetCsv(csvScratch.Slice(0, bytesRead), ref tuning, ref rule, out ruleCount);
                return true;
            }
            catch (Exception)
            {
                return false;
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
            if (RejectInvalidAudioFilterHostCold())
                return;

            EnsureDynamicMusicSignalLaneCold();
            CacheDataVaultCold();
            CacheAudioServiceCold();
            CacheMusicDirectorCold();
            CacheSettingsManagerCold();
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
            if (RejectInvalidAudioFilterHostCold())
                return;

            EnsureDynamicMusicSignalLaneCold();
            CacheDataVaultCold();
            CacheAudioServiceCold();
            CacheMusicDirectorCold();
            CacheSettingsManagerCold();
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
            if (GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Environment))
                _registeredColdTick = 1;
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
            DestroyRuntimeDriverClip();
        }

        private bool RejectInvalidAudioFilterHostCold()
        {
            if (!TryGetComponent<AudioListener>(out _))
            {
                Volatile.Write(ref _invalidAudioFilterHost, 0);
                return false;
            }

            if (_hostSource == null)
                TryGetComponent(out _hostSource);
            if (_hostSource != null)
            {
                _hostSource.playOnAwake = false;
                _hostSource.loop = false;
                if (_hostSource.isPlaying)
                    _hostSource.Stop();
            }

            Volatile.Write(ref _invalidAudioFilterHost, 1);
            if (ReferenceEquals(_activeInstance, this))
                _activeInstance = null;
            enabled = false;
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (Volatile.Read(ref _nativeAllocated) == 0)
                return;

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
            PublishAudioThreadCopyBufferLateFrame();
            if (Interlocked.Exchange(ref _audioHostConfigDirty, 0) != 0)
                ConfigureAudioHostCached();
        }

        public void ColdTick()
        {
            if (Volatile.Read(ref _nativeAllocated) == 0)
                EnsureVaultStorage();

            TryRefreshScalabilityStateHandleCold();
            RefreshGlobalQualitySnapshotCold();
            CacheAudioServiceCold();
            CacheMusicDirectorCold();
            CacheSettingsManagerCold();
            PollCsvRulesCold();
            Volatile.Write(ref _audioHostConfigDirty, 1);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Audio)
            {
                CacheAudioService(currentService as IAudioService);
                Volatile.Write(ref _audioHostConfigDirty, 1);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.SettingsRuntime)
            {
                CacheSettingsManager(currentService as SettingsManager);
                Volatile.Write(ref _audioHostConfigDirty, 1);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.MusicDirectorRuntime)
            {
                CacheMusicDirector(currentService as HectonMusicDirector);
                Volatile.Write(ref _audioHostConfigDirty, 1);
                return;
            }

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
            if (Volatile.Read(ref _invalidAudioFilterHost) != 0)
            {
                if (data != null && data.Length > 0)
                    ZeroManagedAudioBuffer(data, 0, data.Length);
                return;
            }

            int safeChannels = math.clamp(channels, 1, 2);
            Volatile.Write(ref _lastAudioChannels, safeChannels);
            Volatile.Write(ref _lastAudioRequestSamples, math.min(data != null ? data.Length : 0, OutputSampleCapacity));

            if (data == null || data.Length == 0)
                return;

            int readyIndex = Volatile.Read(ref _readyBufferIndex);
            if (readyIndex < 0)
            {
                Interlocked.Increment(ref _audioUnderrunCount);
                ZeroManagedAudioBuffer(data, 0, data.Length);
                return;
            }

            Interlocked.Exchange(ref _audioCopyBufferIndex, readyIndex);
            try
            {
                if (!TryResolvePublishedAudioThreadCopyBuffer(out NativeArray<float> sourceBuffer, out int readySamples) ||
                    readySamples <= 0)
                {
                    Interlocked.Increment(ref _audioUnderrunCount);
                    ZeroManagedAudioBuffer(data, 0, data.Length);
                    return;
                }

                void* source = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(sourceBuffer);
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
            }
        }

        private void UnregisterRuntime()
        {
            if (ReferenceEquals(_activeInstance, this))
                _activeInstance = null;
            ClearCachedRuntimeServices();
            if (Interlocked.Exchange(ref _registeredHotSwap, 0) != 0)
                GlobalRegistry.TryUnregisterHotSwapListener(this);
            if (Interlocked.Exchange(ref _registeredColdTick, 0) != 0)
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Environment);
            if (Interlocked.Exchange(ref _registeredLateFrame, 0) != 0)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            if (Interlocked.Exchange(ref _registeredUpdate, 0) != 0)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            ForceFlushSynthJobForShutdown();
        }

        private void ClearCachedRuntimeServices()
        {
            _cachedAudioService = null;
            _cachedMusicDirector = null;
            _cachedSettingsManager = null;
        }

        private void CacheDataVaultCold()
        {
            RebindDataVaultCold(GlobalRegistry.DataVault);
        }

        private void CacheAudioServiceCold()
        {
            CacheAudioService(GlobalRegistry.Audio);
        }

        private void CacheAudioService(IAudioService audioService)
        {
            _cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null;
        }

        private IAudioService ResolveAudioService()
        {
            IAudioService audioService = _cachedAudioService;
            if (IsAudioServiceUsable(audioService))
                return audioService;

            _cachedAudioService = null;
            return null;
        }

        private static bool IsAudioServiceUsable(IAudioService audioService)
        {
            if (audioService == null || !audioService.IsAudioRuntimeReady)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private void CacheMusicDirectorCold()
        {
            CacheMusicDirector(GlobalRegistry.MusicDirector);
        }

        private void CacheMusicDirector(HectonMusicDirector musicDirector)
        {
            _cachedMusicDirector = musicDirector != null && musicDirector.isActiveAndEnabled ? musicDirector : null;
        }

        private void CacheSettingsManagerCold()
        {
            CacheSettingsManager(GlobalRegistry.Settings);
        }

        private void CacheSettingsManager(SettingsManager settingsManager)
        {
            _cachedSettingsManager = settingsManager != null && settingsManager.isActiveAndEnabled ? settingsManager : null;
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
            _presetRulesHandle = vault.EnsureGenerationHandle<DynamicMusicPresetRuleDTO>(BufferID.AudioDynamicSynthPresetRules, PresetRuleCapacity, VaultOwner, NativeArrayOptions.UninitializedMemory);
            _grainBankHandle = vault.EnsureGenerationHandle<float>(BufferID.AudioDynamicSynthGrainBank, GrainBankSampleCapacity, VaultOwner, NativeArrayOptions.UninitializedMemory);
            _sharedStateHandle = vault.EnsureGenerationHandle<DynamicMusicSharedStateDTO>(BufferID.AudioDynamicSynthSharedState, 1, VaultOwner, NativeArrayOptions.UninitializedMemory);

            if (!ClearDynamicMusicVaultStorage())
            {
                DisposeVaultStorage();
                return;
            }

            if (!EnsureAudioThreadCopyBuffersCold())
            {
                DisposeVaultStorage();
                return;
            }

            Volatile.Write(ref _readyBufferIndex, -1);
            Volatile.Write(ref _readySampleCount, 0);
            Volatile.Write(ref _readyPublishSequence, 0);
            Volatile.Write(ref _audioCopyBufferIndex, -1);
            Volatile.Write(ref _audioThreadPublishedBufferIndex, -1);
            Volatile.Write(ref _audioThreadCopiedPublishSequence, 0);
            Volatile.Write(ref _audioThreadCopyASampleCount, 0);
            Volatile.Write(ref _audioThreadCopyBSampleCount, 0);
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
            ReleaseVaultBuffer(vault, ref _presetRulesHandle, BufferID.AudioDynamicSynthPresetRules);
            ReleaseVaultBuffer(vault, ref _grainBankHandle, BufferID.AudioDynamicSynthGrainBank);
            ReleaseVaultBuffer(vault, ref _sharedStateHandle, BufferID.AudioDynamicSynthSharedState);
            DisposeAudioThreadCopyBuffers();
            _scalabilityStateHandle = default;
            _dataVault = null;
#if UNITY_EDITOR
            _resolvedCsvPath = null;
            _lastResolvedCsvRelativePath = null;
#endif
            Volatile.Write(ref _nativeAllocated, 0);
        }

        private bool EnsureAudioThreadCopyBuffersCold()
        {
            if (_audioThreadCopyA.IsCreated &&
                _audioThreadCopyB.IsCreated &&
                _audioThreadCopyA.Length >= OutputSampleCapacity &&
                _audioThreadCopyB.Length >= OutputSampleCapacity)
            {
                return true;
            }

            DisposeAudioThreadCopyBuffers();
            _audioThreadCopyA = H8Memory.Allocate<float>(OutputSampleCapacity, VaultOwner, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _audioThreadCopyB = H8Memory.Allocate<float>(OutputSampleCapacity, VaultOwner, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            if (!_audioThreadCopyA.IsCreated || !_audioThreadCopyB.IsCreated)
            {
                DisposeAudioThreadCopyBuffers();
                return false;
            }

            Volatile.Write(ref _audioThreadPublishedBufferIndex, -1);
            Volatile.Write(ref _audioThreadCopiedPublishSequence, 0);
            return true;
        }

        private void DisposeAudioThreadCopyBuffers()
        {
            Volatile.Write(ref _audioThreadPublishedBufferIndex, -1);
            Volatile.Write(ref _audioThreadCopiedPublishSequence, 0);
            Volatile.Write(ref _audioThreadCopyASampleCount, 0);
            Volatile.Write(ref _audioThreadCopyBSampleCount, 0);
            if (_audioThreadCopyA.IsCreated)
                H8Memory.Release(ref _audioThreadCopyA, VaultOwner);
            if (_audioThreadCopyB.IsCreated)
                H8Memory.Release(ref _audioThreadCopyB, VaultOwner);
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

        private bool ClearDynamicMusicVaultStorage()
        {
            return TryClearMutationBuffer(in _voicesHandle, GuardVoices) &&
                   TryClearMutationBuffer(in _scalarHandle, GuardScalar) &&
                   TryClearMutationBuffer(in _tuningHandle, GuardTuning) &&
                   TryClearMutationBuffer(in _outputAHandle, GuardOutputA) &&
                   TryClearMutationBuffer(in _outputBHandle, GuardOutputB) &&
                   TryClearMutationBuffer(in _biquadHandle, GuardBiquad) &&
                   TryClearMutationBuffer(in _telemetryRingHandle, GuardTelemetryRing) &&
                   TryClearMutationBuffer(in _telemetryCursorHandle, GuardTelemetryCursor) &&
                   TryClearMutationBuffer(in _presetRulesHandle, GuardPresetRules) &&
                   TryClearMutationBuffer(in _grainBankHandle, GuardGrainBank) &&
                   TryClearMutationBuffer(in _sharedStateHandle, GuardSharedState);
        }

        private bool TryClearMutationBuffer<T>(in VaultGenerationHandle<T> handle, ulong mutationMask)
            where T : unmanaged
        {
            IDataVault guardVault = null;
            IDataVault lockedVault = _dataVault;
            if (lockedVault == null)
                return false;

            try
            {
                if (!TryAcquireDynamicMusicMutationView(lockedVault, in handle, mutationMask, out NativeArray<T> buffer, out guardVault))
                    return false;

                MemClearArray(buffer);
                return true;
            }
            finally
            {
                ReleaseDynamicMusicMutationGuard(guardVault, mutationMask);
            }
        }

        private bool TryResolveSynthJobViewsLocked(int targetBuffer, out DynamicMusicVaultViews views)
        {
            views = default;
            IDataVault vault = _dataVault;
            if (vault == null || !AreDynamicMusicVaultHandlesExact())
                return false;

            if (!vault.TryResolveHandle(in _voicesHandle, out views.Voices) ||
                !vault.TryResolveHandle(in _scalarHandle, out views.Scalar) ||
                !vault.TryResolveHandle(in _tuningHandle, out views.Tuning) ||
                !vault.TryResolveHandle(in _biquadHandle, out views.Biquad) ||
                !vault.TryResolveHandle(in _grainBankHandle, out views.GrainBank) ||
                (targetBuffer == 0 && !vault.TryResolveHandle(in _outputAHandle, out views.OutputA)) ||
                (targetBuffer != 0 && !vault.TryResolveHandle(in _outputBHandle, out views.OutputB)))
            {
                views = default;
                return false;
            }

            return views.Voices.IsCreated &&
                   views.Scalar.IsCreated &&
                   views.Tuning.IsCreated &&
                   views.Biquad.IsCreated &&
                   views.GrainBank.IsCreated &&
                   (targetBuffer == 0 ? views.OutputA.IsCreated : views.OutputB.IsCreated);
        }

        private static ulong ResolvePublishMutationMask(int publishedBuffer)
        {
            _ = publishedBuffer;
            return GuardScalar |
                   GuardTuning |
                   GuardTelemetryRing |
                   GuardTelemetryCursor |
                   GuardSharedState;
        }

        private static ulong ResolveOutputMutationMask(int bufferIndex)
        {
            if (bufferIndex == 0)
                return GuardOutputA;
            if (bufferIndex == 1)
                return GuardOutputB;

            return 0UL;
        }

        private static void ReleaseDynamicMusicMutationGuard(IDataVault vault, ulong mutationMask)
        {
            if (vault == null || mutationMask == 0UL)
                return;

            vault.ReleaseMutationGuard(mutationMask);
        }

        private bool TryResolveSynthPublishViews(IDataVault guardedVault, int publishedBuffer, out DynamicMusicVaultViews views)
        {
            views = default;
            _ = publishedBuffer;
            if (guardedVault == null || !AreDynamicMusicVaultHandlesExact())
                return false;

            if (!guardedVault.TryResolveHandle(in _scalarHandle, out views.Scalar) ||
                !guardedVault.TryResolveHandle(in _tuningHandle, out views.Tuning) ||
                !guardedVault.TryResolveHandle(in _telemetryRingHandle, out views.TelemetryRing) ||
                !guardedVault.TryResolveHandle(in _telemetryCursorHandle, out views.TelemetryCursor) ||
                !guardedVault.TryResolveHandle(in _sharedStateHandle, out views.SharedState) ||
                !views.Scalar.IsCreated ||
                !views.Tuning.IsCreated ||
                !views.TelemetryRing.IsCreated ||
                !views.TelemetryCursor.IsCreated ||
                !views.SharedState.IsCreated)
            {
                views = default;
                return false;
            }

            return true;
        }

        private bool TryAcquireReadyOutputBuffer(int readyIndex, out NativeArray<float> buffer, out ulong mutationMask, out IDataVault guardedVault)
        {
            buffer = default;
            mutationMask = ResolveOutputMutationMask(readyIndex);
            guardedVault = _dataVault;
            if (guardedVault == null || mutationMask == 0UL || !AreDynamicMusicVaultHandlesExact())
                return false;

            if (!guardedVault.TryAcquireMutationGuard(mutationMask))
                return false;

            bool resolved = false;
            try
            {
                resolved = readyIndex == 0
                    ? guardedVault.TryResolveHandle(in _outputAHandle, out buffer)
                    : guardedVault.TryResolveHandle(in _outputBHandle, out buffer);
                if (resolved && buffer.IsCreated)
                    return true;

                return false;
            }
            finally
            {
                if (!resolved || !buffer.IsCreated)
                {
                    ReleaseDynamicMusicMutationGuard(guardedVault, mutationMask);
                    buffer = default;
                    mutationMask = 0UL;
                    guardedVault = null;
                }
            }
        }

        private bool TryAcquireDynamicMusicMutationView<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            ulong mutationMask,
            out NativeArray<T> buffer,
            out IDataVault guardVault)
            where T : struct
        {
            buffer = default;
            guardVault = null;
            if (vault == null ||
                handle.Generation == 0u ||
                handle.SystemID != (uint)VaultOwner ||
                mutationMask == 0UL ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(mutationMask))
            {
                return false;
            }

            bool acquired = true;
            try
            {
                if (vault.IsCompactionFenceActive ||
                    !vault.TryResolveHandle(in handle, out buffer) ||
                    !buffer.IsCreated)
                {
                    return false;
                }

                guardVault = vault;
                acquired = false;
                return true;
            }
            finally
            {
                if (acquired)
                {
                    ReleaseDynamicMusicMutationGuard(vault, mutationMask);
                    buffer = default;
                }
            }
        }

        private bool TryLockSynthJobBuffers(int targetBuffer, out int lockedMask)
        {
            lockedMask = 0;
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!TryLockSynthJobBuffer(vault, BufferID.AudioDynamicSynthVoices, SynthJobLockVoices, ref lockedMask) ||
                !TryLockSynthJobBuffer(vault, BufferID.AudioDynamicSynthScalar, SynthJobLockScalar, ref lockedMask) ||
                !TryLockSynthJobBuffer(vault, BufferID.AudioDynamicSynthTuning, SynthJobLockTuning, ref lockedMask) ||
                !TryLockSynthJobBuffer(vault, BufferID.AudioDynamicSynthBiquad, SynthJobLockBiquad, ref lockedMask) ||
                !TryLockSynthJobBuffer(vault, BufferID.AudioDynamicSynthGrainBank, SynthJobLockGrainBank, ref lockedMask) ||
                !TryLockSynthJobBuffer(vault, targetBuffer == 0 ? BufferID.AudioDynamicSynthOutputA : BufferID.AudioDynamicSynthOutputB, SynthJobLockOutput, ref lockedMask))
            {
                ReleaseSynthJobBufferLocks(vault, lockedMask, targetBuffer);
                lockedMask = 0;
                return false;
            }

            return true;
        }

        private static bool TryLockSynthJobBuffer(IDataVault vault, BufferID bufferId, int lockBit, ref int lockedMask)
        {
            if (vault == null || !vault.TryLockBuffer(bufferId, VaultOwner))
                return false;

            lockedMask |= lockBit;
            return true;
        }

        private void ReleaseOutstandingSynthJobBufferLocks()
        {
            int lockedMask = Interlocked.Exchange(ref _synthJobLockedBufferMask, 0);
            int targetBuffer = Interlocked.Exchange(ref _synthJobLockedTargetBuffer, -1);
            ReleaseSynthJobBufferLocks(_dataVault, lockedMask, targetBuffer);
        }

        private static void ReleaseSynthJobBufferLocks(IDataVault vault, int lockedMask, int targetBuffer)
        {
            if (vault == null || lockedMask == 0)
                return;

            if ((lockedMask & SynthJobLockOutput) != 0 && targetBuffer >= 0)
                vault.TryUnlockBuffer(targetBuffer == 0 ? BufferID.AudioDynamicSynthOutputA : BufferID.AudioDynamicSynthOutputB, VaultOwner);
            if ((lockedMask & SynthJobLockGrainBank) != 0)
                vault.TryUnlockBuffer(BufferID.AudioDynamicSynthGrainBank, VaultOwner);
            if ((lockedMask & SynthJobLockBiquad) != 0)
                vault.TryUnlockBuffer(BufferID.AudioDynamicSynthBiquad, VaultOwner);
            if ((lockedMask & SynthJobLockTuning) != 0)
                vault.TryUnlockBuffer(BufferID.AudioDynamicSynthTuning, VaultOwner);
            if ((lockedMask & SynthJobLockScalar) != 0)
                vault.TryUnlockBuffer(BufferID.AudioDynamicSynthScalar, VaultOwner);
            if ((lockedMask & SynthJobLockVoices) != 0)
                vault.TryUnlockBuffer(BufferID.AudioDynamicSynthVoices, VaultOwner);
        }

        private void ConfigureAudioHostCold()
        {
            if (_hostSource == null && !TryGetComponent(out _hostSource))
                return;

            ConfigureAudioHostCached();
        }

        private void ConfigureAudioHostCached()
        {
            if (_hostSource == null)
                return;

            if (Volatile.Read(ref _invalidAudioFilterHost) != 0)
            {
                _hostSource.playOnAwake = false;
                _hostSource.loop = false;
                if (_hostSource.isPlaying)
                    _hostSource.Stop();
                Volatile.Write(ref _audioHostConfigDirty, 0);
                return;
            }

            int sampleRate = math.max(8000, AudioSettings.outputSampleRate);
            _hostSource.playOnAwake = true;
            _hostSource.loop = true;
            _hostSource.spatialBlend = 0f;
            _hostSource.dopplerLevel = 0f;
            _hostSource.volume = 1f;
            ApplyAudioHostMixerRoute();

            EnsureRuntimeDriverClipCold(sampleRate);
            if (_runtimeDriverClip != null && _hostSource.clip != _runtimeDriverClip)
                _hostSource.clip = _runtimeDriverClip;

            if (_hostSource.clip == null)
            {
                Volatile.Write(ref _audioHostConfigDirty, 0);
                return;
            }

            if (!_hostSource.isPlaying && Application.isPlaying)
                _hostSource.Play();
        }

        private void EnsureRuntimeDriverClipCold(int sampleRate)
        {
            int safeSampleRate = math.max(8000, sampleRate);
            if (_runtimeDriverClip != null && _runtimeDriverClip.frequency == safeSampleRate)
                return;

            DestroyRuntimeDriverClip();
            // COLD ALLOC: AudioClip[256 samples] - silent AudioSource driver for OnAudioFilterRead host - owner: DynamicMusicGranularSynthesizer
            _runtimeDriverClip = AudioClip.Create(
                "H8_DynamicMusic_FilterDriver",
                RuntimeDriverClipSampleCount,
                1,
                safeSampleRate,
                false);
        }

        private void DestroyRuntimeDriverClip()
        {
            if (_runtimeDriverClip == null)
                return;

            Destroy(_runtimeDriverClip);
            _runtimeDriverClip = null;
        }

        private void ApplyAudioHostMixerRoute()
        {
            HectonMusicDirector musicDirector = _cachedMusicDirector;
            AudioMixerGroup musicGroup = musicDirector != null && musicDirector.isActiveAndEnabled ? musicDirector.DedicatedMusicMixerGroup : null;
            if (musicGroup != null)
            {
                if (_hostSource.outputAudioMixerGroup != musicGroup)
                    _hostSource.outputAudioMixerGroup = musicGroup;
                _hostSource.volume = 1f;
                return;
            }

            _hostSource.volume = ResolveFallbackMusicHostVolume01();

            IAudioService audioService = ResolveAudioService();
            if (audioService == null || audioService.AmbientGroup == null)
                return;

            if (_hostSource.outputAudioMixerGroup != audioService.AmbientGroup)
                _hostSource.outputAudioMixerGroup = audioService.AmbientGroup;
        }

        private float ResolveFallbackMusicHostVolume01()
        {
            SettingsManager settings = _cachedSettingsManager;
            return settings != null ? math.saturate(settings.MusicVolume) : 1f;
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
            if (_dataVault == null || !AreDynamicMusicVaultHandlesExact())
                return;

            DynamicMusicSynthTuningDTO tuning = CreateDefaultSynthTuning();
            if (!TryWriteTuningSnapshot(in tuning))
                return;

            DynamicMusicSynthScalarDTO scalar = default;
            scalar.GlobalQualityWeight = ResolveGlobalQualityWeightFromSnapshot();
            scalar.DepthMeters = math.max(0f, _mockDepthMeters);
            scalar.Depth01 = math.saturate(scalar.DepthMeters * math.rcp(math.max(0.0001f, tuning.DepthMaxMeters)));
            scalar.TensionIndex = math.saturate(_mockTensionBias01);
            scalar.LpfCutoffHz = math.max(tuning.LpfMinHz, 22000f - scalar.DepthMeters * tuning.LpfDepthHzPerMeter);
            scalar.ActiveVoices = 16;
            if (!TryWriteScalarSnapshot(in scalar))
                return;

            TryWriteDefaultVoiceBank(in tuning);
        }

        private static DynamicMusicSynthTuningDTO CreateDefaultSynthTuning()
        {
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
            return SanitizeTuning(tuning);
        }

        private bool TryWriteScalarSnapshot(in DynamicMusicSynthScalarDTO scalar)
        {
            IDataVault guardVault = null;
            IDataVault lockedVault = _dataVault;
            if (lockedVault == null || !AreDynamicMusicVaultHandlesExact())
                return false;

            try
            {
                if (!TryAcquireDynamicMusicMutationView(lockedVault, in _scalarHandle, GuardScalar, out NativeArray<DynamicMusicSynthScalarDTO> scalarView, out guardVault) ||
                    scalarView.Length <= 0)
                    return false;

                scalarView[0] = scalar;
                return true;
            }
            finally
            {
                ReleaseDynamicMusicMutationGuard(guardVault, GuardScalar);
            }
        }

        private bool TryWriteDefaultVoiceBank(in DynamicMusicSynthTuningDTO tuning)
        {
            IDataVault guardVault = null;
            IDataVault lockedVault = _dataVault;
            if (lockedVault == null || !AreDynamicMusicVaultHandlesExact())
                return false;

            try
            {
                if (!TryAcquireDynamicMusicMutationView(lockedVault, in _voicesHandle, GuardVoices, out NativeArray<SynthVoiceDTO> voices, out guardVault))
                    return false;

                float sampleRate = math.max(8000f, AudioSettings.outputSampleRate);
                for (int i = 0; i < voices.Length; i++)
                {
                    uint hash = Hash32((uint)i ^ tuning.SeedBase);
                    SynthVoiceDTO voice = default;
                    voice.CurrentPhase = HashToUnit(hash);
                    voice.PhaseIncrement = tuning.BasePitchHz * math.rcp(sampleRate);
                    voice.EnvelopeState = 1f;
                    voice.SoundHash = hash == 0u ? 1u : hash;
                    voice.TargetPitch = tuning.BasePitchHz;
                    voice.TargetVolume = 0f;
                    voices[i] = voice;
                }

                return true;
            }
            finally
            {
                ReleaseDynamicMusicMutationGuard(guardVault, GuardVoices);
            }
        }

        private void GenerateDefaultGrainBankCold()
        {
            DynamicMusicVaultViews views = default;
            IDataVault guardVault = null;
            IDataVault lockedVault = _dataVault;
            if (lockedVault == null || !AreDynamicMusicVaultHandlesExact())
                return;

            try
            {
                if (!TryAcquireDynamicMusicMutationView(lockedVault, in _grainBankHandle, GuardGrainBank, out views.GrainBank, out guardVault))
                    return;

                NativeArray<float> grainBank = views.GrainBank;
                if (!grainBank.IsCreated)
                    return;

                for (int i = 0; i < grainBank.Length; i++)
                {
                    float phase = i * math.rcp(math.max(1f, (float)grainBank.Length));
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
                ReleaseDynamicMusicMutationGuard(guardVault, GuardGrainBank);
            }
        }

        private void DrainSignalInputs()
        {
            float damageImpulse = math.saturate(_pendingDamageImpulse);
            float stingerImpulse = math.saturate(_pendingStingerImpulse);
            _pendingDamageImpulse = 0f;
            bool receivedMusicDirectorScalar = false;
            bool suppressReactiveImpulses = false;

            ReadOnlySpan<DynamicMusicScalarSignal> musicSignals = SignalBus<DynamicMusicScalarSignal>.GetFrameSnapshot();
            for (int i = 0; i < musicSignals.Length; i++)
            {
                DynamicMusicScalarSignal signal = musicSignals[i];
                bool signalIsMusicDirectorScalar = signal.SourceHash == DynamicMusicScalarSignal.SourceMusicDirectorHash;
                if ((signal.Flags & DynamicMusicScalarSignal.FlagExternalScalars) != 0u)
                {
                    if (signalIsMusicDirectorScalar || !receivedMusicDirectorScalar)
                    {
                        _externalTension01 = math.saturate(FiniteOrFallback(signal.Tension01, 0f));
                        _externalDepthMeters = math.max(0f, FiniteOrFallback(signal.DepthMeters, 0f));
                        _externalQualityWeight = math.saturate(FiniteOrFallback(signal.GlobalQualityWeight, 1f));
                        _externalDamageImpulse01 = math.saturate(FiniteOrFallback(signal.DamageImpulse01, 0f));
                    }

                    if (signalIsMusicDirectorScalar)
                    {
                        _externalMusicActivity01 = math.saturate(FiniteOrFallback(signal.MusicActivity01, 0f));
                        receivedMusicDirectorScalar = true;
                        if ((signal.Flags & DynamicMusicScalarSignal.FlagSuppressReactiveImpulses) != 0u)
                            suppressReactiveImpulses = true;
                    }
                    else if (!receivedMusicDirectorScalar && signal.MusicActivity01 > 0f)
                    {
                        _externalMusicActivity01 = math.saturate(FiniteOrFallback(signal.MusicActivity01, 0f));
                    }

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

            if (suppressReactiveImpulses)
            {
                damageImpulse = 0f;
                stingerImpulse = 0f;
                _externalDamageImpulse01 = 0f;
                _externalMusicActivity01 = 0f;
            }

            if (receivedMusicDirectorScalar)
            {
                _musicDirectorScalarMissedTicks = 0;
            }
            else if (_musicDirectorScalarMissedTicks < MusicDirectorScalarGraceTicks)
            {
                _musicDirectorScalarMissedTicks++;
            }
            else
            {
                _externalMusicActivity01 = 0f;
            }

            if (!receivedMusicDirectorScalar &&
                _musicDirectorScalarMissedTicks >= MusicDirectorScalarGraceTicks &&
                !_allowMockPlaybackWithoutDirector)
            {
                damageImpulse = 0f;
                stingerImpulse = 0f;
                _externalDamageImpulse01 = 0f;
                suppressReactiveImpulses = true;
            }

            Volatile.Write(ref _suppressReactiveMusicImpulses, suppressReactiveImpulses ? 1 : 0);
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

            if (Volatile.Read(ref _synthJobLockedBufferMask) != 0)
                ReleaseOutstandingSynthJobBufferLocks();

            if (!TryLockSynthJobBuffers(targetBuffer, out int lockedMask))
            {
                Interlocked.Increment(ref _audioOverflowCount);
                return;
            }

            bool scheduled = false;
            try
            {
                if (!TryResolveSynthJobViewsLocked(targetBuffer, out DynamicMusicVaultViews views))
                    return;

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
                float externalActivity = hasExternalScalars
                    ? math.saturate(_externalMusicActivity01)
                    : (_allowMockPlaybackWithoutDirector ? 1f : 0f);
                bool suppressReactiveImpulses = Volatile.Read(ref _suppressReactiveMusicImpulses) != 0;
                if (suppressReactiveImpulses)
                    externalActivity = 0f;
                float damageImpulse = math.saturate(_pendingDamageImpulse);
                damageImpulse = math.saturate(math.max(damageImpulse, _externalDamageImpulse01));
                float stingerImpulse = math.saturate(_pendingStingerImpulse);
                if (suppressReactiveImpulses)
                {
                    damageImpulse = 0f;
                    stingerImpulse = 0f;
                }
                _pendingDamageImpulse = 0f;
                _pendingStingerImpulse = 0f;
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
                mockJob.MusicActivity01 = math.saturate(math.max(externalActivity, math.max(stingerImpulse, damageImpulse * 0.45f)));
                mockJob.SuppressReactiveImpulses = suppressReactiveImpulses ? 1 : 0;
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
                H8Memory.RegisterActiveJob(VaultOwner, _synthJobHandle);
                Interlocked.Exchange(ref _synthJobLockedTargetBuffer, targetBuffer);
                Interlocked.Exchange(ref _synthJobLockedBufferMask, lockedMask);
                Volatile.Write(ref _synthJobPending, 1);
                scheduled = true;
                lockedMask = 0;
                _ = tuning;
            }
            finally
            {
                if (!scheduled)
                    ReleaseSynthJobBufferLocks(_dataVault, lockedMask, targetBuffer);
            }
        }

        private bool TryFlushCompletedSynthJob()
        {
            if (Volatile.Read(ref _synthJobPending) == 0)
            {
                if (Volatile.Read(ref _synthJobLockedBufferMask) != 0)
                    ReleaseOutstandingSynthJobBufferLocks();
                return true;
            }

            if (!_synthJobHandle.IsCompleted)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _synthJobHandle))
                return false;

            Volatile.Write(ref _synthJobPending, 0);
            ReleaseOutstandingSynthJobBufferLocks();
            float elapsedMicroseconds = ResolveElapsedMicroseconds(_synthJobStartTicks);
            PublishReadyBuffer(elapsedMicroseconds);

            return true;
        }

        private void ForceFlushSynthJobForShutdown()
        {
            if (Volatile.Read(ref _synthJobPending) == 0)
            {
                if (Volatile.Read(ref _synthJobLockedBufferMask) != 0)
                    ReleaseOutstandingSynthJobBufferLocks();
                return;
            }

            DispatcherJobFence.BeginLateFrameSwapWindow();
            try
            {
                DispatcherJobFence.TryComplete(ref _synthJobHandle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndLateFrameSwapWindow();
            }

            Volatile.Write(ref _synthJobPending, 0);
            ReleaseOutstandingSynthJobBufferLocks();
            PublishReadyBuffer(ResolveElapsedMicroseconds(_synthJobStartTicks));
        }

        private void PublishReadyBuffer(float elapsedMicroseconds)
        {
            int publishedBuffer = _jobBufferIndex;
            if (publishedBuffer < 0)
                return;

            IDataVault guardedVault = _dataVault;
            ulong mutationMask = ResolvePublishMutationMask(publishedBuffer);
            if (guardedVault == null || !guardedVault.TryAcquireMutationGuard(mutationMask))
                return;

            try
            {
                if (!TryResolveSynthPublishViews(guardedVault, publishedBuffer, out DynamicMusicVaultViews views))
                    return;

                int sampleCount = math.clamp(_jobSampleCount, 0, OutputSampleCapacity);
                int channels = math.clamp(_jobChannels, 1, 2);
                sampleCount -= sampleCount % channels;
                DynamicMusicSynthScalarDTO scalar = views.Scalar.IsCreated && views.Scalar.Length > 0 ? views.Scalar[0] : default;
                uint flags = scalar.Flags;
                if (HasNonFiniteScalar(in scalar))
                    flags |= FlagNonFinite;

                WriteSharedState(ref views, publishedBuffer, sampleCount, channels, elapsedMicroseconds, flags);
                WriteTelemetry(ref views, elapsedMicroseconds, publishedBuffer, sampleCount, flags);

                Volatile.Write(ref _readySampleCount, sampleCount);
                Volatile.Write(ref _readyBufferIndex, publishedBuffer);
                int nextSequence = Volatile.Read(ref _readyPublishSequence) + 1;
                if (nextSequence <= 0)
                    nextSequence = 1;
                Volatile.Write(ref _readyPublishSequence, nextSequence);

                float decaySeconds = views.Tuning.IsCreated && views.Tuning.Length > 0
                    ? math.max(0.0001f, views.Tuning[0].StingerDecaySeconds)
                    : DefaultStingerDecaySeconds;
                _pendingStingerImpulse = math.saturate(_pendingStingerImpulse * MathLodApproximation.ApproxExpNegPade33Wide40(MaximumDeltaSeconds * math.rcp(decaySeconds)));
            }
            finally
            {
                ReleaseDynamicMusicMutationGuard(guardedVault, mutationMask);
            }
        }

        private void PublishAudioThreadCopyBufferLateFrame()
        {
            int readySequence = Volatile.Read(ref _readyPublishSequence);
            if (readySequence <= 0 || readySequence == Volatile.Read(ref _audioThreadCopiedPublishSequence))
                return;

            if (!_audioThreadCopyA.IsCreated || !_audioThreadCopyB.IsCreated)
                return;

            int readyIndex = Volatile.Read(ref _readyBufferIndex);
            int readySamples = Volatile.Read(ref _readySampleCount);
            if (readyIndex < 0 || readySamples <= 0)
                return;

            int publishedCopyIndex = Volatile.Read(ref _audioThreadPublishedBufferIndex);
            int writeCopyIndex = publishedCopyIndex == 0 ? 1 : 0;
            NativeArray<float> targetBuffer = writeCopyIndex == 0 ? _audioThreadCopyA : _audioThreadCopyB;
            if (!targetBuffer.IsCreated)
                return;

            ulong mutationMask = 0UL;
            IDataVault guardedVault = null;
            try
            {
                if (!TryAcquireReadyOutputBuffer(readyIndex, out NativeArray<float> sourceBuffer, out mutationMask, out guardedVault) ||
                    !sourceBuffer.IsCreated)
                {
                    return;
                }

                int copySamples = math.min(math.min(readySamples, sourceBuffer.Length), targetBuffer.Length);
                if (copySamples <= 0)
                    return;

                void* source = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(sourceBuffer);
                void* destination = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(targetBuffer);
                UnsafeUtility.MemCpy(destination, source, (long)copySamples * sizeof(float));

                if (writeCopyIndex == 0)
                {
                    Volatile.Write(ref _audioThreadCopyASampleCount, copySamples);
                }
                else
                {
                    Volatile.Write(ref _audioThreadCopyBSampleCount, copySamples);
                }

                Volatile.Write(ref _audioThreadCopiedPublishSequence, readySequence);
                Volatile.Write(ref _audioThreadPublishedBufferIndex, writeCopyIndex);
            }
            finally
            {
                ReleaseDynamicMusicMutationGuard(guardedVault, mutationMask);
            }
        }

        private bool TryResolvePublishedAudioThreadCopyBuffer(out NativeArray<float> buffer, out int sampleCount)
        {
            int publishedIndex = Volatile.Read(ref _audioThreadPublishedBufferIndex);
            if (publishedIndex == 0)
            {
                buffer = _audioThreadCopyA;
                sampleCount = Volatile.Read(ref _audioThreadCopyASampleCount);
                return buffer.IsCreated;
            }

            if (publishedIndex == 1)
            {
                buffer = _audioThreadCopyB;
                sampleCount = Volatile.Read(ref _audioThreadCopyBSampleCount);
                return buffer.IsCreated;
            }

            buffer = default;
            sampleCount = 0;
            return false;
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
            state.MusicActivity01 = math.saturate(scalar.TargetVolume);
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
        private void ParseSynthPresetCsv(
            ReadOnlySpan<byte> csvScratch,
            ref DynamicMusicSynthTuningDTO tuning,
            ref DynamicMusicPresetRuleDTO rule,
            out int ruleCount)
        {
            ruleCount = 0;
            if (csvScratch.Length <= 0)
                return;

            int safeByteCount = csvScratch.Length;
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

            if (rule.PresetHash != 0u)
            {
                rule = SanitizePresetRule(rule);
                ruleCount = 1;
            }

            tuning.Flags |= FlagCsvApplied | FlagProceduralOnly;
            tuning = SanitizeTuning(tuning);
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
        private static bool TryParseFloat(ReadOnlySpan<byte> bytes, int start, int end, out float value)
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

            value = sign * (integer + fraction * math.rcp(math.max(1f, divisor)));
            return math.isfinite(value);
        }

        private static uint HashCsvKey(ReadOnlySpan<byte> bytes, int start, int end)
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
            if (data == null || count <= 0)
                return;

            int safeStart = math.clamp(start, 0, data.Length);
            int safeCount = math.min(count, data.Length - safeStart);
            if (safeCount > 0)
                Array.Clear(data, safeStart, safeCount);
        }

        private static float ResolveElapsedMicroseconds(long startTicks)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            return elapsedTicks * InvStopwatchMicrosScale * math.rcp(math.max(1f, (float)Stopwatch.Frequency));
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
            return (hash & 0x00FFFFFFu) * InvHash24Max;
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
            public float MusicActivity01;
            public int SuppressReactiveImpulses;

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
                float damageImpulse = SuppressReactiveImpulses != 0 ? 0f : math.saturate(FiniteOrFallback(DamageImpulse01, 0f));
                float stingerImpulse = SuppressReactiveImpulses != 0 ? 0f : math.saturate(FiniteOrFallback(StingerImpulse01, 0f));
                float musicActivity = SuppressReactiveImpulses != 0 ? 0f : math.saturate(FiniteOrFallback(MusicActivity01, 0f));
                float fallbackTension = dangerWave * 0.35f + damageImpulse * 0.85f;
                float tension = HasExternalScalars != 0
                    ? math.saturate(math.max(ExternalTension01, damageImpulse * 0.85f))
                    : math.saturate(math.max(ExternalTension01, fallbackTension));
                float depth01 = math.saturate(depthMeters * math.rcp(math.max(0.0001f, tuning.DepthMaxMeters)));
                float cutoff = math.max(tuning.LpfMinHz, 22000f - depthMeters * tuning.LpfDepthHzPerMeter);

                scalar.Frame = FrameIndex;
                scalar.Flags = (HasExternalScalars != 0 ? 0u : FlagUsingMockTension) | tuning.Flags;
                scalar.TensionIndex = math.saturate(FiniteOrFallback(tension, 0f));
                scalar.DepthMeters = FiniteOrFallback(depthMeters, 0f);
                scalar.Depth01 = depth01;
                scalar.GlobalQualityWeight = math.saturate(FiniteOrFallback(GlobalQualityWeight, 1f));
                scalar.DamageImpulse01 = damageImpulse;
                scalar.StingerImpulse = SuppressReactiveImpulses != 0
                    ? 0f
                    : math.saturate(math.max(stingerImpulse, scalar.StingerImpulse));
                scalar.TargetVolume = musicActivity;
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
                float q = math.saturate((scalar.GlobalQualityWeight - tuning.QualityMin) * math.rcp(qualityDenominator));
                float qSmooth = Smooth01(q);
                float musicActivity = math.saturate(math.max(scalar.TargetVolume, math.max(scalar.StingerImpulse * 0.85f, scalar.DamageImpulse01 * 0.35f)));
                int activeVoices = math.clamp((int)math.lerp(16f, 128f, qSmooth), 1, math.max(1, VoiceCapacityValue));
                if (musicActivity <= 0.0001f)
                    activeVoices = 1;

                float tension = math.saturate(scalar.TensionIndex * tuning.TensionMultiplier);
                float density = tuning.BaseGrainDensity * math.lerp(0.55f, 1.85f, tension) * math.lerp(0.5f, 1.15f, qSmooth);
                float lfoPhase = scalar.Frame * tuning.LfoFrequency * 0.016666668f;
                float heartbeat = 0.55f + 0.45f * MathLodApproximation.ApproxSinBhaskara(lfoPhase * math.PI * 2f);
                float pitchBend = 1f + tension * 0.18f + scalar.DamageImpulse01 * 0.32f;
                float baseVolume = tuning.BaseVolume * math.lerp(0.72f, 1.15f, tension) * math.lerp(0.7f, 1f, qSmooth) * musicActivity;
                float normalization = math.rsqrt(math.max(1f, activeVoices));
                float safeSampleRate = math.max(8000f, SampleRate);

                scalar.BaseDensity = density * musicActivity;
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
                    float densityPush = math.lerp(0.75f, 1.5f, math.saturate(density * 0.0078125f));
                    float targetPitch = tuning.BasePitchHz * pitchBend * detune * densityPush;
                    float activeMask = i < activeVoices ? 1f : 0f;
                    voice.PhaseIncrement = math.clamp(targetPitch * math.rcp(safeSampleRate), 0.000001f, 0.25f);
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
                        float grainWindow = Hecton8.PureLogic.Systems.GrainEnvelopeCalculator.Compute(phase, 0.5f, 0.5f);
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
                scalar.OutputRms = FastSqrtNonNegative(totalEnergy * math.rcp(math.max(1f, (float)OutputSampleCount)));
                scalar.StingerImpulse = math.saturate(stinger * MathLodApproximation.ApproxExpNegPade33Wide40(frameCount * math.rcp(math.max(1f, tuning.StingerDecaySeconds * safeSampleRate))));
                if (!math.isfinite(scalar.OutputPeak) || !math.isfinite(scalar.OutputRms))
                    scalar.Flags |= FlagNonFinite;
            }

            private static void ComputeLowPassCoefficients(ref DynamicMusicBiquadStateDTO biquad, float cutoffHz, int sampleRate)
            {
                float safeRate = math.max(8000f, sampleRate);
                float normalized = math.clamp(cutoffHz * math.rcp(math.max(0.0001f, safeRate)), 0.0001f, 0.45f);
                float k = MathLodApproximation.ApproxTanClamped(math.PI * normalized, 16f);
                float invQ = 1.41421356237f;
                float norm = math.rcp(math.max(0.0001f, 1f + k * invQ + k * k));
                float a0 = k * k * norm;
                biquad.A0 = a0;
                biquad.A1 = 2f * a0;
                biquad.A2 = a0;
                biquad.B1 = 2f * (k * k - 1f) * norm;
                biquad.B2 = (1f - k * invQ + k * k) * norm;
                biquad.LastCutoffHz = cutoffHz;
                biquad.LastSampleRate = safeRate;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float FastSqrtNonNegative(float value)
            {
                if (!math.isfinite(value) || value <= 0f)
                    return 0f;

                return value * math.rsqrt(math.max(value, InvSqrtEpsilon));
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

        #region JulesLink_PitchShiftResampleCalculator
        private static void JulesLink_PitchShiftResampleCalculator() { _ = typeof(Hecton8.PureLogic.Systems.PitchShiftResampleCalculator); }
        #endregion
    }
}
