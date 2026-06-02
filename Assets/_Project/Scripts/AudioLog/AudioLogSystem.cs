// ============================================================================
// HECTON-8 - AudioLogSystem.cs
// Runtime service for colony audio logs.
//
// ROLE:
//   - Stores discovered log state through ISaveable.
//   - Routes playback through SpatialAudioManager.
//   - Publishes events for PDA archive and HUD subtitles.
//   - Integrates NarrativeEvents discovery into log unlock state.
//
// ZERO GC:
//   - HashSet<uint> gives O(1) discovered-log checks.
//   - ISlowTickable polls playback completion outside hot tick.
//   - No new, LINQ, or string concatenation in hot paths.
//
// SAVE:
//   - LoadPriority 6 after NarrativeDirector (5).
//   - Persists discovered logs as a fixed 1024-bit mask.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Conditional = System.Diagnostics.ConditionalAttribute;
using Hecton.Localization;
using Hecton8.AtlasSignal;
using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.Modding;
using Hecton8.Quest;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Narrative
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AudioLogVaultTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint FallbackFlags;
        [FieldOffset(8)] public uint LastBufferId;
        [FieldOffset(12)] public uint ExpectedGeneration;
        [FieldOffset(16)] public uint ActualGeneration;
        [FieldOffset(20)] public int QueueCount;
        [FieldOffset(24)] public int EncryptedFragmentCount;
        [FieldOffset(28)] public int SuccessfulVaultResolutions;
        [FieldOffset(32)] public int StaleHandleFailures;
        [FieldOffset(36)] public int EstimatedMicroseconds;
        [FieldOffset(40)] private ulong _pad0;
        [FieldOffset(48)] private ulong _pad1;
        [FieldOffset(56)] private ulong _pad2;
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-140)]
    public sealed class AudioLogSystem : MonoBehaviour, ISaveable, ISlowTickable, ILateFrameTickable, IAudioLogRuntime, IGlobalRegistryHotSwapListener
    {
        //  INSPECTOR

        [Header("Settings")]
        [Tooltip("Audio log playback volume.")]
        [SerializeField, Range(0f, 1f)] private float playbackVolume = 0.85f;

        [Tooltip("Maximum number of saved audio log IDs.")]
        [SerializeField] private int maxSavedLogs = 256;

        [Tooltip("Authored audio log catalog used by narrative systems that unlock logs without a pickup object.")]
        [SerializeField] private AudioLogData[] allLogs = Array.Empty<AudioLogData>();

        //  SERVICE AUTHORITY

        //  PRIVATE STATE

        private const int PlaybackQueueCapacity = 16;
        private const int EncryptedFragmentStateCapacity = 32;
        private const int AudioLogTelemetryCapacity = 300;
        private const int ResolvedLogHashCapacity = AudioLogDiscoveryBitMask.MaxLogCount;
        private const uint EncryptedLogCompleteMask = 0xFu;
        private const SystemID OwnerSystemId = SystemID.Audio;
        private const uint VaultFallbackMissingVault = 1u;
        private const uint VaultFallbackPlaybackQueue = 2u;
        private const uint VaultFallbackEncryptedHashes = 4u;
        private const uint VaultFallbackEncryptedBits = 8u;
        private const uint VaultFallbackTelemetry = 16u;
        private const uint VaultFallbackEncryptedState = VaultFallbackEncryptedHashes | VaultFallbackEncryptedBits;
        private static readonly ulong PlaybackQueueMutationGuardMask = AudioLogMutationGuardBit(BufferID.AudioLogPlaybackQueue);
        private static readonly ulong EncryptedFragmentStateMutationGuardMask =
            AudioLogMutationGuardBit(BufferID.AudioLogEncryptedFragmentHashes) |
            AudioLogMutationGuardBit(BufferID.AudioLogEncryptedFragmentRecoveredBits);
        private static readonly ulong TelemetryMutationGuardMask =
            AudioLogMutationGuardBit(BufferID.AudioLogTelemetryRing) |
            AudioLogMutationGuardBit(BufferID.AudioLogTelemetryCursor);
        // COLD ALLOC: HashSet<uint>[1024] — discovered audio-log hashes per save — owner: AudioLogSystem
        private readonly HashSet<uint> _discoveredLogHashes = new HashSet<uint>(ResolvedLogHashCapacity);
        // COLD ALLOC: Dictionary<uint,AudioLogData>[1024] — resolved log lookup by stable log hash — owner: AudioLogSystem
        private readonly Dictionary<uint, AudioLogData> _logLookupByHash = new Dictionary<uint, AudioLogData>(ResolvedLogHashCapacity);
        // COLD ALLOC: Dictionary<AudioLogData,uint>[1024] — resolved log reverse lookup by asset reference — owner: AudioLogSystem
        private readonly Dictionary<AudioLogData, uint> _hashByLog = new Dictionary<AudioLogData, uint>(ResolvedLogHashCapacity);
        // COLD ALLOC: Dictionary<uint,uint>[1024] — audio log discovery notification hash lookup — owner: AudioLogSystem
        private readonly Dictionary<uint, uint> _discoveryNotificationHashByLogHash = new Dictionary<uint, uint>(ResolvedLogHashCapacity);
        // COLD ALLOC: uint[16] — fixed narrative queue dedupe slots — owner: AudioLogSystem
        private readonly uint[] _queuedLogHashDedup = new uint[PlaybackQueueCapacity];
        // COLD ALLOC: uint[1024] — flat resolved audio-log catalog for deterministic save iteration — owner: AudioLogSystem
        private readonly uint[] _resolvedLogHashes = new uint[ResolvedLogHashCapacity];
        private const string AudioLogFolder = "Assets/_Project/Data/Lore/AudioLogs";
        private IDataVault _dataVault;
        private IDataVault _playbackQueueGuardVault;
        private IDataVault _encryptedFragmentStateGuardVault;
        private VaultGenerationHandle<uint> _queuedLogHashesHandle;
        private VaultGenerationHandle<uint> _encryptedFragmentLogHashesHandle;
        private VaultGenerationHandle<uint> _encryptedFragmentRecoveredBitsHandle;
        private VaultGenerationHandle<AudioLogVaultTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private int _playbackQueueReadIndex;
        private int _playbackQueueWriteIndex;
        private int _encryptedFragmentStateCount;
        private int _vaultResolutionSuccessCount;
        private int _vaultResolutionFailureCount;
        private int _resolvedLogHashCount;
        private static readonly uint _QueueFullWarningHash = unchecked((uint)LocHash.Compute("AudioLogSystem.QueueFull"));
        private static readonly uint _LookupMissWarningHash = unchecked((uint)LocHash.Compute("AudioLogSystem.LookupMiss"));
        private static readonly uint _ResolvedLogCatalogFullWarningHash = unchecked((uint)LocHash.Compute("AudioLogSystem.ResolvedLogCatalogFull"));
        private static readonly uint _EncryptedFragmentStateFullWarningHash = unchecked((uint)LocHash.Compute("AudioLogSystem.EncryptedFragmentStateFull"));
        private static readonly uint _EncryptedVoiceRouteMissingWarningHash = unchecked((uint)LocHash.Compute("AudioLogSystem.EncryptedVoiceRouteMissing"));
        private static readonly uint _NarrativeQueueContextHash = unchecked((uint)LocHash.Compute("NarrativeQueue"));
        private const float NarrativeRadioDeepStartDepthMeters = 450f;
        private const float NarrativeRadioDeepFullDepthMeters = 1800f;
        private uint _fallbackDiscoveryNotificationHash;

        private AudioLogData _currentLog;
        private uint _currentLogHash;
        private float _playbackTimer;
        private int _queueCount;
        private int _queuedLogHashDedupCount;
        private float _atmosphericWarningTimer;
        private bool _isPlaying;
        private bool _atmosphericWarningActive;
        private bool _registered;
        private bool _lateFrameRegistered;
        private bool _serviceRegistered;
        private bool _registeredHotSwapListener;
        private bool _currentPlaybackBitCrushed;
        private bool _pendingPlaybackDirty;
        private bool _pendingPlaybackBitCrushed;
        private float _pendingPlaybackVolume;
        private float _pendingNarrativeInterference01;
        private AudioGlitchParametersDTO _pendingPlaybackGlitch;
        private AudioClip _pendingPlaybackClip;
        private bool _resolvedLogCatalogFullTelemetryArmed = true;
        private bool _encryptedVoiceRouteMissingTelemetryArmed = true;
        private IAudioService _cachedAudioService;
        private ISpatialAudioNarrativeRadioSink _cachedNarrativeAudioSink;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private ISaveService _cachedSaveService;
        private bool _saveRegistered;

        //  ISaveable

        public int SavePriority => 6;
        public int LoadPriority => 6;

        //  PUBLIC PROPERTIES

        public bool IsPlaying => _isPlaying;
        public bool IsNarrativeQueueBlocked => _isPlaying || _atmosphericWarningActive;
        public AudioLogData CurrentLog => _currentLog;
        public int DiscoveredCount => _discoveredLogHashes.Count;
        public int DiscoveredAudioLogCount => _discoveredLogHashes.Count;
        public bool CurrentPlaybackBitCrushed => _currentPlaybackBitCrushed;

        //  LIFECYCLE

        private void Awake()
        {
            BuildLogLookup();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            TryAutoPopulateAudioLogCatalog();
            BuildLogLookup();
        }
#endif

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            EnsureVaultBuffersCold();
            TryRegisterHotSwapListener();
            TryRegisterService();
            TryRegister();

            TryRegisterSaveParticipant();
        }

        private void OnDisable()
        {
            TryUnregisterSaveParticipant();
            TryUnregister();
            TryUnregisterHotSwapListener();
            TryUnregisterService();

            if (_isPlaying)
            {
                StopPlayback();
            }

            ClearPlaybackQueue();
            ClearPendingPlaybackSync();
            TryUnregisterLateFrame();
            ClearAtmosphericWarningBlocker();
        }

        private void OnDestroy()
        {
            TryUnregisterSaveParticipant();
            TryUnregister();
            ClearPendingPlaybackSync();
            TryUnregisterLateFrame();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            ReleaseVaultBuffers(_dataVault);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioService(currentService as IAudioService);
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.Save:
                    TryUnregisterSaveParticipant();
                    _cachedSaveService = currentService as ISaveService;
                    TryRegisterSaveParticipant();
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    RebindDataVaultCold(currentService is IDataVault currentVault ? currentVault : null, ensureBuffers: true);
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    _registered = false;
                    _lateFrameRegistered = false;
                    if (currentService != null && isActiveAndEnabled)
                    {
                        TryRegister();
                        if (_isPlaying)
                            TryRegisterLateFrame();
                    }
                    break;
            }
        }

        //  ISlowTickable playback completion polling.

        public void SlowTick()
        {
            TickAtmosphericWarningBlocker();

            if (!_isPlaying || _currentLog == null)
                return;

            _playbackTimer -= 0.5f; // SlowTick ~0.5s

            if (_playbackTimer > 0f)
                return;

            // Playback completed.
            AudioLogData completedLog = _currentLog;
            uint completedHash = _currentLogHash;
            _isPlaying = false;
            _currentLog = null;
            _currentLogHash = 0u;
            _playbackTimer = 0f;

            _currentPlaybackBitCrushed = false;
            AudioLogEvents.TryRaisePlaybackCompleted(completedHash, completedLog);

            LogPlaybackCompleted(completedHash);
            TryStartNextQueuedLog();
        }

        public void LateFrameTick()
        {
            FlushPendingPlaybackVisualSync();

            if (!_pendingPlaybackDirty)
                TryUnregisterLateFrame();
        }

        //  PUBLIC API

        /// <summary>
        /// Marks a log as discovered without starting playback.
        /// </summary>
        public void DiscoverLog(AudioLogData data)
        {
            if (data == null || !TryResolveLogHash(data, out uint discoveredHash))
                return;

            if (data.IsFragmentedEncrypted && !IsEncryptedLogFullyRecovered(discoveredHash))
                return;

            if (_discoveredLogHashes.Contains(discoveredHash))
                return;

            _discoveredLogHashes.Add(discoveredHash);
            if (discoveredHash != 0u)
                AudioLogEvents.TryRaiseLogDiscovered(discoveredHash, data);
            uint notificationHash = ResolveDiscoveryNotificationHash(discoveredHash);
            if (notificationHash != 0u)
                NotificationEvents.TryPushRegisteredInfo(notificationHash);

            // Also register the discovery with narrative systems.
            NarrativeEvents.TryRaiseDiscoveryMade(discoveredHash);
            NarrativeEvents.TryRaiseAudioLogFound(discoveredHash);

            LogDiscovered(discoveredHash, data);
        }

        /// <summary>
        /// Plays an audio log or queues it when another log is already active.
        /// </summary>
        public void PlayLog(AudioLogData data)
        {
            if (data == null || !TryResolveLogHash(data, out uint logHash))
                return;

            // Mark undiscovered logs before playback.
            if (data.IsFragmentedEncrypted && !IsEncryptedLogFullyRecovered(logHash))
            {
                if (GetRecoveredEncryptedBits(logHash) != 0u)
                    PlayEncryptedPartialPreview(logHash, data);
                return;
            }

            DiscoverLog(data);

            if (_isPlaying || _atmosphericWarningActive)
            {
                EnqueuePlayback(logHash);
                return;
            }

            // Route playback through SpatialAudioManager.
            PlayLogByHash(logHash, data);
        }

        public bool TryPlayLogByHash(uint logHash)
        {
            if (logHash == 0u)
                return false;

            if (_logLookupByHash.Count == 0 && allLogs != null && allLogs.Length > 0)
                BuildLogLookup();

            if (!_logLookupByHash.TryGetValue(logHash, out AudioLogData data) || data == null)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(_LookupMissWarningHash, _NarrativeQueueContextHash, 1f);
                return false;
            }

            if (data.IsFragmentedEncrypted && !IsEncryptedLogFullyRecovered(logHash))
            {
                if (GetRecoveredEncryptedBits(logHash) != 0u)
                    PlayEncryptedPartialPreview(logHash, data);
                return false;
            }

            PlayLog(data);
            return true;
        }

        public bool TryPlayAudioLogByHash(uint logHash)
        {
            return TryPlayLogByHash(logHash);
        }

        public bool TryPlayAudioLog(string logId)
        {
            return TryPlayLogById(logId);
        }

        public bool TryPlayLogById(string logId)
        {
            return TryPlayLogByHash(ComputeAudioLogHash(logId));
        }

        public uint GetRecoveredEncryptedBits(uint logHash)
        {
            if (logHash == 0u)
                return 0u;

            if (_discoveredLogHashes.Contains(logHash))
                return EncryptedLogCompleteMask;

            return TryGetEncryptedFragmentBits(logHash, out uint recoveredBits)
                ? recoveredBits & EncryptedLogCompleteMask
                : 0u;
        }

        public uint GetRecoveredEncryptedAudioLogBits(uint logHash)
        {
            return GetRecoveredEncryptedBits(logHash);
        }

        public bool RecoverEncryptedFragment(uint logHash, uint fragmentHash)
        {
            if (logHash == 0u || fragmentHash == 0u)
                return false;

            if (_discoveredLogHashes.Contains(logHash))
                return true;

            if (_logLookupByHash.Count == 0 && allLogs != null && allLogs.Length > 0)
                BuildLogLookup();

            if (!_logLookupByHash.TryGetValue(logHash, out AudioLogData data) ||
                data == null ||
                !data.TryResolveEncryptedFragmentMask(fragmentHash, out uint fragmentBitMask))
            {
                return false;
            }

            uint previousBits = GetRecoveredEncryptedBits(logHash);
            uint recoveredBits = SignalBeaconMath.MergeRecoveredBits(previousBits, fragmentBitMask);
            if (recoveredBits == previousBits)
                return true;

            bool storedRecoveredBits = SetEncryptedFragmentBits(logHash, recoveredBits);
            if (!storedRecoveredBits)
                return false;

            if ((recoveredBits & EncryptedLogCompleteMask) == EncryptedLogCompleteMask)
            {
                DiscoverLog(data);
                if (_isPlaying || _atmosphericWarningActive)
                    EnqueuePlayback(logHash);
                else
                    PlayLogByHash(logHash, data);
                return true;
            }

            PlayEncryptedPartialPreview(logHash, data);
            return true;
        }

        private void PlayLogByHash(uint logHash, AudioLogData data)
        {
            if (data == null || logHash == 0u)
                return;

            TrackResolvedLogHash(logHash);
            float playbackDuration = ResolvePlaybackDuration(data.Duration);
            AudioClip playbackClip = data.ResolvedAudioClip;
            AudioGlitchParametersDTO glitch = ResolveAudioGlitchParameters(logHash, encryptedPreview: false);
            bool bitCrushRouteActive = false;
            if (playbackClip != null)
            {
                bitCrushRouteActive = QueuePlaybackVisualSync(
                    playbackClip,
                    playbackVolume,
                    ShouldPreferBitCrush(in glitch),
                    in glitch);
            }

            _currentLog = data;
            _currentLogHash = logHash;
            _playbackTimer = playbackDuration;
            _isPlaying = true;
            _currentPlaybackBitCrushed = bitCrushRouteActive;

            AudioLogEvents.TryRaisePlaybackStarted(_currentLogHash, _playbackTimer, in glitch, data);

            LogPlaying(logHash, playbackDuration);
        }

        private bool IsEncryptedLogFullyRecovered(uint logHash)
        {
            return (GetRecoveredEncryptedBits(logHash) & EncryptedLogCompleteMask) == EncryptedLogCompleteMask;
        }

        private void PlayEncryptedPartialPreview(uint logHash, AudioLogData data)
        {
            if (data == null || logHash == 0u)
                return;

            TrackResolvedLogHash(logHash);

            if (_isPlaying || _atmosphericWarningActive)
                return;

            AudioClip playbackClip = data.ResolvedAudioClip;
            if (playbackClip == null)
                return;

            AudioGlitchParametersDTO glitch = ResolveAudioGlitchParameters(logHash, encryptedPreview: true);
            bool bitCrushRouteActive = QueuePlaybackVisualSync(
                playbackClip,
                playbackVolume,
                ShouldPreferBitCrush(in glitch),
                in glitch);

            if (!bitCrushRouteActive && _encryptedVoiceRouteMissingTelemetryArmed)
            {
                _encryptedVoiceRouteMissingTelemetryArmed = false;
                Hecton8.Core.GlobalTelemetryBus.PublishPerformanceWarning(
                    _EncryptedVoiceRouteMissingWarningHash,
                    _NarrativeQueueContextHash,
                    1f);
            }

            _currentLog = data;
            _currentLogHash = logHash;
            float playbackDuration = ResolvePlaybackDuration(data.Duration);
            _playbackTimer = playbackDuration;
            _isPlaying = true;
            _currentPlaybackBitCrushed = bitCrushRouteActive;

            AudioLogEvents.TryRaisePlaybackStarted(_currentLogHash, _playbackTimer, in glitch, data);
        }

        private bool QueuePlaybackVisualSync(
            AudioClip clip,
            float volume,
            bool preferBitCrush,
            in AudioGlitchParametersDTO glitch)
        {
            if (clip == null)
                return false;

            bool bitCrushRouteAvailable = preferBitCrush && _cachedNarrativeAudioSink != null;
            AudioGlitchParametersDTO safeGlitch = AudioGlitchParametersDTO.Sanitize(in glitch);
            _pendingPlaybackClip = clip;
            _pendingPlaybackVolume = Sanitize01(volume);
            _pendingPlaybackBitCrushed = bitCrushRouteAvailable;
            _pendingPlaybackGlitch = safeGlitch;
            _pendingNarrativeInterference01 = math.max(
                ResolveNarrativeRadioInterference01(),
                GlitchPermilleTo01(safeGlitch.CorruptionPermille));
            _pendingPlaybackDirty = true;
            TryRegisterLateFrame();
            return bitCrushRouteAvailable;
        }

        private void FlushPendingPlaybackVisualSync()
        {
            if (!_pendingPlaybackDirty)
                return;

            AudioClip playbackClip = _pendingPlaybackClip;
            float volume = _pendingPlaybackVolume;
            bool useBitCrush = _pendingPlaybackBitCrushed;
            float interference01 = _pendingNarrativeInterference01;
            AudioGlitchParametersDTO glitch = _pendingPlaybackGlitch;
            ClearPendingPlaybackSync();

            if (playbackClip == null)
                return;

            ISpatialAudioNarrativeRadioSink narrativeAudioSink = _cachedNarrativeAudioSink;
            if (narrativeAudioSink != null)
            {
                narrativeAudioSink.SetNarrativeRadioInterference(math.max(interference01, GlitchBandPassTo01(glitch.BandPassByte)));
                if (useBitCrush && narrativeAudioSink.TryPlayStatic2DBitCrushed(playbackClip, volume))
                    return;
            }

            IAudioService audioManager = _cachedAudioService;
            if (audioManager != null)
                audioManager.PlayStatic2D(playbackClip, volume);
        }

        private void ClearPendingPlaybackSync()
        {
            _pendingPlaybackDirty = false;
            _pendingPlaybackBitCrushed = false;
            _pendingPlaybackVolume = 0f;
            _pendingNarrativeInterference01 = 0f;
            _pendingPlaybackGlitch = default;
            _pendingPlaybackClip = null;
        }

        private AudioGlitchParametersDTO ResolveAudioGlitchParameters(uint logHash, bool encryptedPreview)
        {
            float corruption01 = Sanitize01(ResolveNarrativeRadioInterference01());
            if (encryptedPreview)
                corruption01 = math.max(corruption01, 0.78f);

            uint ageBucket = (logHash >> 24) & 0xFFu;
            float age01 = ageBucket * (1f / 255f);
            corruption01 = math.saturate(math.max(corruption01, age01 * 0.35f));
            float quality = Sanitize01(HomeostasisBrain.GlobalQualityWeight);
            float lowTierTaming = math.lerp(0.72f, 1f, quality);
            float highTierOverdrive = math.lerp(1f, 1.18f, quality);

            ushort corruptionPermille = Unit01ToPermille(corruption01);
            ushort bitCrushPermille = Unit01ToPermille(
                (encryptedPreview ? math.max(corruption01, 0.65f) : corruption01 * 0.45f) * lowTierTaming);
            short pitchShiftCents = (short)math.round(math.lerp(-420f, -80f, 1f - corruption01) * highTierOverdrive);
            byte bandPassByte = (byte)math.round(math.saturate(corruption01 * highTierOverdrive) * 255f);
            byte flags = AudioGlitchParametersDTO.FlagDepthDerived | AudioGlitchParametersDTO.FlagBandPass;
            if (bitCrushPermille > 0)
                flags |= AudioGlitchParametersDTO.FlagBitCrush;
            if (pitchShiftCents != 0)
                flags |= AudioGlitchParametersDTO.FlagPitchShift;
            if (encryptedPreview)
                flags |= AudioGlitchParametersDTO.FlagEncryptedPreview;

            AudioGlitchParametersDTO glitch = new AudioGlitchParametersDTO
            {
                CorruptionPermille = corruptionPermille,
                BitCrushPermille = bitCrushPermille,
                PitchShiftCents = pitchShiftCents,
                BandPassByte = bandPassByte,
                Flags = flags
            };
            return AudioGlitchParametersDTO.Sanitize(in glitch);
        }

        private static bool ShouldPreferBitCrush(in AudioGlitchParametersDTO glitch)
        {
            return (glitch.Flags & AudioGlitchParametersDTO.FlagBitCrush) != 0 &&
                   glitch.BitCrushPermille >= 320;
        }

        private static ushort Unit01ToPermille(float value)
        {
            if (!math.isfinite(value))
                return 0;

            return (ushort)math.clamp((int)math.round(math.saturate(value) * 1000f), 0, 1000);
        }

        private static float GlitchPermilleTo01(ushort permille)
        {
            return math.saturate(permille * 0.001f);
        }

        private static float GlitchBandPassTo01(byte bandPassByte)
        {
            return math.saturate(bandPassByte * (1f / 255f));
        }

        private float ResolveNarrativeRadioInterference01()
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            HectonSurvivalSystem survivalSystem = playerContext != null ? playerContext.SurvivalSystem : null;
            float rawDepthMeters = survivalSystem != null ? survivalSystem.Depth : 0f;
            float depthMeters = math.isfinite(rawDepthMeters) ? math.max(0f, rawDepthMeters) : 0f;
            float depth01 = math.saturate(
                (depthMeters - NarrativeRadioDeepStartDepthMeters) /
                math.max(1f, NarrativeRadioDeepFullDepthMeters - NarrativeRadioDeepStartDepthMeters));

            TraumaDispatcher traumaDispatcher = playerContext != null ? playerContext.TraumaDispatcher : null;
            float radiation01 = traumaDispatcher != null ? Sanitize01(traumaDispatcher.HazardRadiationSignal01) : 0f;
            return math.max(depth01, radiation01);
        }

        private static float Sanitize01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float ResolvePlaybackDuration(float durationSeconds)
        {
            return math.isfinite(durationSeconds) ? math.max(0.5f, durationSeconds) : 0.5f;
        }

        public void NotifyAtmosphericWarningStarted(float durationSeconds)
        {
            _atmosphericWarningActive = true;
            _atmosphericWarningTimer = math.max(_atmosphericWarningTimer, ResolvePlaybackDuration(durationSeconds));
        }

        public void NotifyAtmosphericWarningCompleted()
        {
            if (!_atmosphericWarningActive)
                return;

            _atmosphericWarningActive = false;
            _atmosphericWarningTimer = 0f;
            TryStartNextQueuedLog();
        }

        /// <summary>
        /// Ostanovit tekuschee vosproizvedenie.
        /// </summary>
        public void StopPlayback()
        {
            if (!_isPlaying || _currentLog == null)
                return;

            AudioLogData stoppedLog = _currentLog;
            uint stoppedHash = _currentLogHash;
            _isPlaying = false;
            _currentLog = null;
            _currentLogHash = 0u;
            _playbackTimer = 0f;
            _currentPlaybackBitCrushed = false;
            ClearPendingPlaybackSync();
            TryUnregisterLateFrame();
            AudioLogEvents.TryRaisePlaybackStopped(stoppedHash, stoppedLog);
        }

        /// <summary>
        /// Checks whether a log has been discovered.
        /// </summary>
        public bool IsDiscovered(string logId)
        {
            return IsDiscovered(ComputeAudioLogHash(logId));
        }

        public bool IsDiscovered(uint logHash)
        {
            return logHash != 0u && _discoveredLogHashes.Contains(logHash);
        }

        public bool IsAudioLogDiscovered(uint logHash)
        {
            return IsDiscovered(logHash);
        }

        public bool IsAudioLogDiscovered(string logId)
        {
            return IsDiscovered(logId);
        }

        //  PRIVATE

        private void BuildLogLookup()
        {
            _logLookupByHash.Clear();
            _hashByLog.Clear();
            _discoveryNotificationHashByLogHash.Clear();
            ClearResolvedLogHashes();
            ResolveFallbackDiscoveryNotificationHash();
            int logCount = allLogs != null ? allLogs.Length : 0;
            for (int i = 0; i < logCount; i++)
            {
                AudioLogData data = allLogs[i];
                uint logHash = ComputeAudioLogHash(data != null ? data.logId : null);
                if (data == null || logHash == 0u)
                    continue;

                TryBindResolvedLogHash(logHash, data);
            }
        }

        private bool TryResolveLogHash(AudioLogData data, out uint logHash)
        {
            logHash = 0u;
            if (data == null)
                return false;

            if (_hashByLog.TryGetValue(data, out logHash) && logHash != 0u)
                return TryBindResolvedLogHash(logHash, data);

            logHash = ComputeAudioLogHash(data.logId);
            if (logHash == 0u)
                return false;

            return TryBindResolvedLogHash(logHash, data);
        }

        private bool TryBindResolvedLogHash(uint logHash, AudioLogData data)
        {
            if (logHash == 0u || data == null)
                return false;

            if (_logLookupByHash.TryGetValue(logHash, out AudioLogData existingData) &&
                existingData != null &&
                !ReferenceEquals(existingData, data))
            {
                return false;
            }

            if (!_logLookupByHash.ContainsKey(logHash))
                _logLookupByHash.Add(logHash, data);

            if (!_hashByLog.ContainsKey(data))
                _hashByLog.Add(data, logHash);

            TrackResolvedLogHash(logHash);
            CacheDiscoveryNotificationHash(logHash, data);
            return true;
        }

        private void CacheDiscoveryNotificationHash(uint logHash, AudioLogData data)
        {
            if (logHash == 0u || data == null)
                return;

            if (_discoveryNotificationHashByLogHash.TryGetValue(logHash, out uint existingNotificationHash))
            {
                if (existingNotificationHash != 0u &&
                    NotificationEvents.TryResolveMessage(existingNotificationHash, out _))
                {
                    return;
                }

                _discoveryNotificationHashByLogHash.Remove(logHash);
            }

            uint notificationHash = ResolveFallbackDiscoveryNotificationHash();
            if (notificationHash != 0u)
                _discoveryNotificationHashByLogHash.Add(logHash, notificationHash);
        }

        private uint ResolveFallbackDiscoveryNotificationHash()
        {
            if (_fallbackDiscoveryNotificationHash == 0u ||
                !NotificationEvents.TryResolveMessage(_fallbackDiscoveryNotificationHash, out _))
            {
                _fallbackDiscoveryNotificationHash = NotificationEvents.RegisterMessage("LOG DISCOVERED".AsSpan());
            }

            return _fallbackDiscoveryNotificationHash;
        }

        private uint ResolveDiscoveryNotificationHash(uint logHash)
        {
            if (logHash == 0u)
                return 0u;

            if (_discoveryNotificationHashByLogHash.TryGetValue(logHash, out uint notificationHash) &&
                notificationHash != 0u &&
                NotificationEvents.TryResolveMessage(notificationHash, out _))
            {
                return notificationHash;
            }

            if (_logLookupByHash.TryGetValue(logHash, out AudioLogData data) && data != null)
            {
                CacheDiscoveryNotificationHash(logHash, data);
                if (_discoveryNotificationHashByLogHash.TryGetValue(logHash, out notificationHash) &&
                    notificationHash != 0u &&
                    NotificationEvents.TryResolveMessage(notificationHash, out _))
                {
                    return notificationHash;
                }
            }

            return ResolveFallbackDiscoveryNotificationHash();
        }

        private static uint ComputeAudioLogHash(string logId)
        {
            return QuestFlagHashKernel.ComputeStableHash(logId);
        }

        private void TrackResolvedLogHash(uint logHash)
        {
            if (logHash == 0u)
                return;

            for (int i = 0; i < _resolvedLogHashCount; i++)
            {
                if (_resolvedLogHashes[i] == logHash)
                    return;
            }

            if (_resolvedLogHashCount >= ResolvedLogHashCapacity)
            {
                if (_resolvedLogCatalogFullTelemetryArmed)
                {
                    _resolvedLogCatalogFullTelemetryArmed = false;
                    GlobalTelemetryBus.PublishPerformanceWarning(
                        _ResolvedLogCatalogFullWarningHash,
                        _NarrativeQueueContextHash,
                        _resolvedLogHashCount);
                }

                return;
            }

            _resolvedLogHashes[_resolvedLogHashCount++] = logHash;
        }

        private void ClearResolvedLogHashes()
        {
            for (int i = 0; i < _resolvedLogHashCount; i++)
            {
                _resolvedLogHashes[i] = 0u;
            }

            _resolvedLogHashCount = 0;
            _resolvedLogCatalogFullTelemetryArmed = true;
        }

        private void EnqueuePlayback(uint logHash)
        {
            if (logHash == 0u ||
                (_currentLogHash == logHash && !_currentPlaybackBitCrushed) ||
                IsPlaybackQueued(logHash))
            {
                return;
            }

            if (_queueCount >= PlaybackQueueCapacity)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(_QueueFullWarningHash, _NarrativeQueueContextHash, _queueCount);
                return;
            }

            if (!TryAcquireVaultMutation(in _queuedLogHashesHandle, BufferID.AudioLogPlaybackQueue, PlaybackQueueCapacity, PlaybackQueueMutationGuardMask, VaultFallbackPlaybackQueue, out NativeArray<uint> queue))
            {
                GlobalTelemetryBus.PublishPerformanceWarning(_QueueFullWarningHash, _NarrativeQueueContextHash, _queueCount);
                return;
            }

            try
            {
                AddQueuedLogHash(logHash);
                queue[_playbackQueueWriteIndex] = logHash;
                _playbackQueueWriteIndex = (_playbackQueueWriteIndex + 1) % PlaybackQueueCapacity;
                _queueCount++;
            }
            finally
            {
                ReleaseVaultMutation(PlaybackQueueMutationGuardMask);
            }
        }

        private bool IsPlaybackQueued(uint logHash)
        {
            if (logHash == 0u)
                return false;

            for (int i = 0; i < _queuedLogHashDedupCount; i++)
            {
                if (_queuedLogHashDedup[i] == logHash)
                    return true;
            }

            return false;
        }

        private void AddQueuedLogHash(uint logHash)
        {
            if (logHash == 0u || _queuedLogHashDedupCount >= PlaybackQueueCapacity)
                return;

            _queuedLogHashDedup[_queuedLogHashDedupCount++] = logHash;
        }

        private void RemoveQueuedLogHash(uint logHash)
        {
            if (logHash == 0u)
                return;

            for (int i = 0; i < _queuedLogHashDedupCount; i++)
            {
                if (_queuedLogHashDedup[i] != logHash)
                    continue;

                int lastIndex = _queuedLogHashDedupCount - 1;
                _queuedLogHashDedup[i] = _queuedLogHashDedup[lastIndex];
                _queuedLogHashDedup[lastIndex] = 0u;
                _queuedLogHashDedupCount = lastIndex;
                return;
            }
        }

        private void ClearQueuedLogHashes()
        {
            for (int i = 0; i < _queuedLogHashDedupCount; i++)
            {
                _queuedLogHashDedup[i] = 0u;
            }

            _queuedLogHashDedupCount = 0;
        }

        private void TryStartNextQueuedLog()
        {
            if (_isPlaying || _atmosphericWarningActive || _queueCount <= 0)
                return;

            uint nextHash = 0u;
            if (TryAcquireVaultMutation(in _queuedLogHashesHandle, BufferID.AudioLogPlaybackQueue, PlaybackQueueCapacity, PlaybackQueueMutationGuardMask, VaultFallbackPlaybackQueue, out NativeArray<uint> queue))
            {
                try
                {
                    nextHash = queue[_playbackQueueReadIndex];
                    queue[_playbackQueueReadIndex] = 0u;
                    _playbackQueueReadIndex = (_playbackQueueReadIndex + 1) % PlaybackQueueCapacity;
                    _queueCount--;
                    RemoveQueuedLogHash(nextHash);
                }
                finally
                {
                    ReleaseVaultMutation(PlaybackQueueMutationGuardMask);
                }
            }

            if (nextHash != 0u && _logLookupByHash.TryGetValue(nextHash, out AudioLogData next) && next != null)
                PlayLogByHash(nextHash, next);
        }

        private void TickAtmosphericWarningBlocker()
        {
            if (!_atmosphericWarningActive)
                return;

            _atmosphericWarningTimer -= 0.5f; // SlowTick ~0.5s
            if (_atmosphericWarningTimer > 0f)
                return;

            NotifyAtmosphericWarningCompleted();
        }

        private void ClearAtmosphericWarningBlocker()
        {
            _atmosphericWarningActive = false;
            _atmosphericWarningTimer = 0f;
        }

        private void ClearPlaybackQueue()
        {
            if (TryAcquireVaultMutation(in _queuedLogHashesHandle, BufferID.AudioLogPlaybackQueue, PlaybackQueueCapacity, PlaybackQueueMutationGuardMask, VaultFallbackPlaybackQueue, out NativeArray<uint> queue))
            {
                try
                {
                    for (int i = 0; i < PlaybackQueueCapacity; i++)
                    {
                        queue[i] = 0u;
                    }
                }
                finally
                {
                    ReleaseVaultMutation(PlaybackQueueMutationGuardMask);
                }
            }

            _queueCount = 0;
            _playbackQueueReadIndex = 0;
            _playbackQueueWriteIndex = 0;
            ClearQueuedLogHashes();
        }

        private void EnsureVaultBuffersCold()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            EnsureVaultHandle(vault, ref _queuedLogHashesHandle, BufferID.AudioLogPlaybackQueue, PlaybackQueueCapacity, NativeArrayOptions.ClearMemory);
            EnsureVaultHandle(vault, ref _encryptedFragmentLogHashesHandle, BufferID.AudioLogEncryptedFragmentHashes, EncryptedFragmentStateCapacity, NativeArrayOptions.ClearMemory);
            EnsureVaultHandle(vault, ref _encryptedFragmentRecoveredBitsHandle, BufferID.AudioLogEncryptedFragmentRecoveredBits, EncryptedFragmentStateCapacity, NativeArrayOptions.ClearMemory);
            EnsureVaultHandle(vault, ref _telemetryRingHandle, BufferID.AudioLogTelemetryRing, AudioLogTelemetryCapacity, NativeArrayOptions.ClearMemory);
            EnsureVaultHandle(vault, ref _telemetryCursorHandle, BufferID.AudioLogTelemetryCursor, 1, NativeArrayOptions.ClearMemory);
        }

        private static void EnsureVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : struct
        {
            if (vault == null)
                return;

            if (IsAudioLogVaultHandle(in handle, bufferId) &&
                vault.TryResolveHandle(in handle, out NativeArray<T> buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return;
            }

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, OwnerSystemId, options);
        }

        private void ReleaseVaultBuffers(IDataVault vault)
        {
            ReleaseStoredVaultMutations();
            ReleaseVaultHandle(vault, ref _queuedLogHashesHandle, BufferID.AudioLogPlaybackQueue);
            ReleaseVaultHandle(vault, ref _encryptedFragmentLogHashesHandle, BufferID.AudioLogEncryptedFragmentHashes);
            ReleaseVaultHandle(vault, ref _encryptedFragmentRecoveredBitsHandle, BufferID.AudioLogEncryptedFragmentRecoveredBits);
            ReleaseVaultHandle(vault, ref _telemetryRingHandle, BufferID.AudioLogTelemetryRing);
            ReleaseVaultHandle(vault, ref _telemetryCursorHandle, BufferID.AudioLogTelemetryCursor);
            _queueCount = 0;
            _playbackQueueReadIndex = 0;
            _playbackQueueWriteIndex = 0;
            _encryptedFragmentStateCount = 0;
            ClearQueuedLogHashes();
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            if (vault != null && IsAudioLogVaultHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsAudioLogVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)OwnerSystemId &&
                   handle.Generation != 0u;
        }

        private bool TryReadVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null || !IsAudioLogVaultHandle(in handle, bufferId))
                return false;

            if (!vault.TryReadOnlyHandle(in handle, out buffer))
                return false;

            return buffer.IsCreated && buffer.Length >= requiredLength;
        }

        private bool TryAcquireVaultMutation<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            ulong mutationGuardMask,
            uint fallbackFlag,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null || mutationGuardMask == 0UL || !IsAudioLogVaultHandle(in handle, bufferId))
            {
                _vaultResolutionFailureCount++;
                RecordVaultTelemetry(fallbackFlag | VaultFallbackMissingVault, bufferId);
                return false;
            }

            if (vault.IsCompactionFenceActive || !vault.TryAcquireMutationGuard(mutationGuardMask))
            {
                _vaultResolutionFailureCount++;
                RecordVaultTelemetry(fallbackFlag, bufferId);
                return false;
            }

            bool success = false;
            bool validationFailed = false;
            try
            {
                if (vault.IsCompactionFenceActive ||
                    !vault.TryResolveHandle(in handle, out buffer) ||
                    vault.IsCompactionFenceActive ||
                    !buffer.IsCreated ||
                    buffer.Length < requiredLength)
                {
                    buffer = default;
                    validationFailed = true;
                    return false;
                }

                _vaultResolutionSuccessCount++;
                StoreVaultMutation(mutationGuardMask, vault);
                success = true;
                return true;
            }
            finally
            {
                if (!success)
                {
                    vault.ReleaseMutationGuard(mutationGuardMask);
                    if (validationFailed)
                    {
                        _vaultResolutionFailureCount++;
                        RecordVaultTelemetry(fallbackFlag, bufferId);
                    }
                }
            }
        }

        private void ReleaseVaultMutation(ulong mutationGuardMask)
        {
            IDataVault vault = TakeVaultMutation(mutationGuardMask);
            if (vault != null && mutationGuardMask != 0UL)
                vault.ReleaseMutationGuard(mutationGuardMask);
        }

        private bool TryReadEncryptedFragmentState(
            out NativeArray<uint>.ReadOnly hashes,
            out NativeArray<uint>.ReadOnly recoveredBits)
        {
            bool hasHashes = TryReadVaultBuffer(in _encryptedFragmentLogHashesHandle, BufferID.AudioLogEncryptedFragmentHashes, EncryptedFragmentStateCapacity, out hashes);
            bool hasBits = TryReadVaultBuffer(in _encryptedFragmentRecoveredBitsHandle, BufferID.AudioLogEncryptedFragmentRecoveredBits, EncryptedFragmentStateCapacity, out recoveredBits);
            return hasHashes && hasBits;
        }

        private void ClearEncryptedFragmentState()
        {
            int count = _encryptedFragmentStateCount;
            if (count > EncryptedFragmentStateCapacity)
                count = EncryptedFragmentStateCapacity;

            if (count > 0 &&
                TryAcquireEncryptedFragmentMutation(out NativeArray<uint> hashes, out NativeArray<uint> recoveredBits))
            {
                try
                {
                    for (int i = 0; i < count; i++)
                    {
                        hashes[i] = 0u;
                        recoveredBits[i] = 0u;
                    }
                }
                finally
                {
                    ReleaseVaultMutation(EncryptedFragmentStateMutationGuardMask);
                }
            }

            _encryptedFragmentStateCount = 0;
        }

        private bool TryGetEncryptedFragmentBits(uint logHash, out uint recoveredBits)
        {
            recoveredBits = 0u;
            if (logHash == 0u ||
                !TryReadEncryptedFragmentState(out NativeArray<uint>.ReadOnly hashes, out NativeArray<uint>.ReadOnly recoveredBitBuffer))
            {
                return false;
            }

            int count = _encryptedFragmentStateCount;
            if (count < 0)
                return false;
            if (count > EncryptedFragmentStateCapacity)
                count = EncryptedFragmentStateCapacity;

            for (int i = 0; i < count; i++)
            {
                if (hashes[i] != logHash)
                    continue;

                recoveredBits = recoveredBitBuffer[i] & EncryptedLogCompleteMask;
                return true;
            }

            return false;
        }

        private bool SetEncryptedFragmentBits(uint logHash, uint recoveredBits)
        {
            if (logHash == 0u)
                return false;

            if (!TryAcquireEncryptedFragmentMutation(out NativeArray<uint> writableHashes, out NativeArray<uint> recoveredBitBuffer))
                return false;

            bool stateFull = false;
            int stateFullCount = 0;
            try
            {
                int activeCount = _encryptedFragmentStateCount;
                if (activeCount < 0)
                    activeCount = 0;
                if (activeCount > EncryptedFragmentStateCapacity)
                    activeCount = EncryptedFragmentStateCapacity;

                int slot = -1;
                for (int i = 0; i < activeCount; i++)
                {
                    if (writableHashes[i] != logHash)
                        continue;

                    slot = i;
                    break;
                }

                bool newSlot = slot < 0;
                if (newSlot)
                {
                    if (activeCount >= EncryptedFragmentStateCapacity)
                    {
                        stateFull = true;
                        stateFullCount = activeCount;
                        return false;
                    }

                    slot = activeCount;
                    writableHashes[slot] = logHash;
                }

                recoveredBitBuffer[slot] = recoveredBits & EncryptedLogCompleteMask;

                if (newSlot)
                    _encryptedFragmentStateCount = activeCount + 1;

                return true;
            }
            finally
            {
                ReleaseVaultMutation(EncryptedFragmentStateMutationGuardMask);
                if (stateFull)
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(
                        _EncryptedFragmentStateFullWarningHash,
                        _NarrativeQueueContextHash,
                        stateFullCount);
                }
            }
        }

        private bool TryAcquireEncryptedFragmentMutation(
            out NativeArray<uint> hashes,
            out NativeArray<uint> recoveredBits)
        {
            hashes = default;
            recoveredBits = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                EncryptedFragmentStateMutationGuardMask == 0UL ||
                !IsAudioLogVaultHandle(in _encryptedFragmentLogHashesHandle, BufferID.AudioLogEncryptedFragmentHashes) ||
                !IsAudioLogVaultHandle(in _encryptedFragmentRecoveredBitsHandle, BufferID.AudioLogEncryptedFragmentRecoveredBits))
            {
                _vaultResolutionFailureCount++;
                RecordVaultTelemetry(VaultFallbackEncryptedState | VaultFallbackMissingVault, BufferID.AudioLogEncryptedFragmentHashes);
                return false;
            }

            if (vault.IsCompactionFenceActive || !vault.TryAcquireMutationGuard(EncryptedFragmentStateMutationGuardMask))
            {
                _vaultResolutionFailureCount++;
                RecordVaultTelemetry(VaultFallbackEncryptedState, BufferID.AudioLogEncryptedFragmentHashes);
                return false;
            }

            bool success = false;
            bool validationFailed = false;
            try
            {
                if (vault.IsCompactionFenceActive ||
                    !vault.TryResolveHandle(in _encryptedFragmentLogHashesHandle, out hashes) ||
                    !vault.TryResolveHandle(in _encryptedFragmentRecoveredBitsHandle, out recoveredBits) ||
                    vault.IsCompactionFenceActive ||
                    !hashes.IsCreated ||
                    !recoveredBits.IsCreated ||
                    hashes.Length < EncryptedFragmentStateCapacity ||
                    recoveredBits.Length < EncryptedFragmentStateCapacity)
                {
                    hashes = default;
                    recoveredBits = default;
                    validationFailed = true;
                    return false;
                }

                _vaultResolutionSuccessCount += 2;
                StoreVaultMutation(EncryptedFragmentStateMutationGuardMask, vault);
                success = true;
                return true;
            }
            finally
            {
                if (!success)
                {
                    vault.ReleaseMutationGuard(EncryptedFragmentStateMutationGuardMask);
                    if (validationFailed)
                    {
                        _vaultResolutionFailureCount++;
                        RecordVaultTelemetry(VaultFallbackEncryptedState, BufferID.AudioLogEncryptedFragmentHashes);
                    }
                }
            }
        }

        private void RecordVaultTelemetry(uint fallbackFlags, BufferID bufferId)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                TelemetryMutationGuardMask == 0UL ||
                !IsAudioLogVaultHandle(in _telemetryRingHandle, BufferID.AudioLogTelemetryRing) ||
                !IsAudioLogVaultHandle(in _telemetryCursorHandle, BufferID.AudioLogTelemetryCursor))
            {
                return;
            }

            if (!vault.TryAcquireMutationGuard(TelemetryMutationGuardMask))
                return;

            try
            {
                if (vault.IsCompactionFenceActive ||
                    !vault.TryResolveHandle(in _telemetryCursorHandle, out NativeArray<int> cursor) ||
                    !vault.TryResolveHandle(in _telemetryRingHandle, out NativeArray<AudioLogVaultTelemetryEntry> telemetry) ||
                    vault.IsCompactionFenceActive ||
                    !cursor.IsCreated ||
                    cursor.Length <= 0 ||
                    !telemetry.IsCreated ||
                    telemetry.Length < AudioLogTelemetryCapacity)
                {
                    return;
                }

                int index = math.clamp(cursor[0], 0, AudioLogTelemetryCapacity - 1);
                int nextIndex = index + 1;
                if (nextIndex >= AudioLogTelemetryCapacity)
                    nextIndex = 0;

                cursor[0] = nextIndex;
                telemetry[index] = new AudioLogVaultTelemetryEntry
                {
                    FrameIndex = (uint)SystemDispatcher.CurrentFrameIndex,
                    FallbackFlags = fallbackFlags,
                    LastBufferId = unchecked((uint)(int)bufferId),
                    ExpectedGeneration = ResolveExpectedGeneration(bufferId),
                    ActualGeneration = ResolveActualGeneration(vault, bufferId),
                    QueueCount = _queueCount,
                    EncryptedFragmentCount = _encryptedFragmentStateCount,
                    SuccessfulVaultResolutions = _vaultResolutionSuccessCount,
                    StaleHandleFailures = _vaultResolutionFailureCount,
                    EstimatedMicroseconds = 0
                };
            }
            finally
            {
                vault.ReleaseMutationGuard(TelemetryMutationGuardMask);
            }
        }

        private static ulong AudioLogMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private void StoreVaultMutation(ulong mutationGuardMask, IDataVault vault)
        {
            if (mutationGuardMask == PlaybackQueueMutationGuardMask)
                _playbackQueueGuardVault = vault;
            else if (mutationGuardMask == EncryptedFragmentStateMutationGuardMask)
                _encryptedFragmentStateGuardVault = vault;
        }

        private IDataVault TakeVaultMutation(ulong mutationGuardMask)
        {
            if (mutationGuardMask == PlaybackQueueMutationGuardMask)
            {
                IDataVault vault = _playbackQueueGuardVault;
                _playbackQueueGuardVault = null;
                return vault;
            }

            if (mutationGuardMask == EncryptedFragmentStateMutationGuardMask)
            {
                IDataVault vault = _encryptedFragmentStateGuardVault;
                _encryptedFragmentStateGuardVault = null;
                return vault;
            }

            return null;
        }

        private void ReleaseStoredVaultMutations()
        {
            IDataVault playbackVault = TakeVaultMutation(PlaybackQueueMutationGuardMask);
            if (playbackVault != null)
                playbackVault.ReleaseMutationGuard(PlaybackQueueMutationGuardMask);

            IDataVault encryptedStateVault = TakeVaultMutation(EncryptedFragmentStateMutationGuardMask);
            if (encryptedStateVault != null)
                encryptedStateVault.ReleaseMutationGuard(EncryptedFragmentStateMutationGuardMask);
        }

        private uint ResolveExpectedGeneration(BufferID bufferId)
        {
            if (bufferId == BufferID.AudioLogPlaybackQueue)
                return _queuedLogHashesHandle.Generation;
            if (bufferId == BufferID.AudioLogEncryptedFragmentHashes)
                return _encryptedFragmentLogHashesHandle.Generation;
            if (bufferId == BufferID.AudioLogEncryptedFragmentRecoveredBits)
                return _encryptedFragmentRecoveredBitsHandle.Generation;
            if (bufferId == BufferID.AudioLogTelemetryRing)
                return _telemetryRingHandle.Generation;
            if (bufferId == BufferID.AudioLogTelemetryCursor)
                return _telemetryCursorHandle.Generation;

            return 0u;
        }

        private static uint ResolveActualGeneration(IDataVault vault, BufferID bufferId)
        {
            return vault != null && vault.TryGetBufferGeneration(bufferId, out uint generation) ? generation : 0u;
        }

#if UNITY_EDITOR
        private void TryAutoPopulateAudioLogCatalog()
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioLogData", new[] { AudioLogFolder });
            if (guids == null || guids.Length == 0)
                return;

            List<AudioLogData> loadedLogs = new List<AudioLogData>(guids.Length); // COLD ALLOC: List<AudioLogData>[guids.Length] — editor-time log catalog bootstrap — owner: AudioLogSystem
            if (allLogs != null)
            {
                for (int i = 0; i < allLogs.Length; i++)
                {
                    AudioLogData existing = allLogs[i];
                    if (existing != null && !loadedLogs.Contains(existing))
                        loadedLogs.Add(existing);
                }
            }

            int previousCount = loadedLogs.Count;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                AudioLogData data = AssetDatabase.LoadAssetAtPath<AudioLogData>(path);
                if (data != null && !loadedLogs.Contains(data))
                    loadedLogs.Add(data);
            }

            if (loadedLogs.Count <= 0 || loadedLogs.Count == previousCount)
                return;

            if (allLogs == null || allLogs.Length != loadedLogs.Count)
                allLogs = new AudioLogData[loadedLogs.Count];

            loadedLogs.CopyTo(allLogs);
            EditorUtility.SetDirty(this);
        }
#endif

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
            _registered = false;
        }

        private void TryRegisterLateFrame()
        {
            if (_lateFrameRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_lateFrameRegistered)
                return;

            SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.Core);
            _lateFrameRegistered = false;
        }

        private void CacheRegistryServicesCold()
        {
            CacheAudioService(GlobalRegistry.Audio);
            _cachedPlayerContext = Hecton8.Core.GlobalRegistry.Player;
            _cachedSaveService = GlobalRegistry.Save;
            RebindDataVaultCold(GlobalRegistry.DataVault, ensureBuffers: false);
        }

        private void CacheAudioService(IAudioService audioService)
        {
            _cachedAudioService = audioService;
            _cachedNarrativeAudioSink = audioService as ISpatialAudioNarrativeRadioSink;
        }

        private void TryRegisterSaveParticipant()
        {
            if (_saveRegistered || !Application.isPlaying || !isActiveAndEnabled)
                return;

            if (_cachedSaveService == null)
                _cachedSaveService = GlobalRegistry.Save;

            if (_cachedSaveService == null)
                return;

            _cachedSaveService.Register(this);
            _saveRegistered = true;
        }

        private void TryUnregisterSaveParticipant()
        {
            if (!_saveRegistered)
                return;

            ISaveService saveService = _cachedSaveService;
            if (saveService != null)
                saveService.Unregister(this);

            _saveRegistered = false;
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

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            if (GlobalRegistry.AudioLogs != null && !ReferenceEquals(GlobalRegistry.AudioLogs, this))
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterAudioLogRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.AudioLogs, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            if (ReferenceEquals(GlobalRegistry.AudioLogs, this))
                GlobalRegistry.UnregisterAudioLogRuntime(this);

            _serviceRegistered = false;
        }

        private void RebindDataVaultCold(IDataVault nextVault, bool ensureBuffers)
        {
            if (!ReferenceEquals(_dataVault, nextVault))
            {
                ReleaseVaultBuffers(_dataVault);
                _dataVault = nextVault;
            }

            if (ensureBuffers)
                EnsureVaultBuffersCold();
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogPlaybackCompleted(uint completedHash)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.Log("[AudioLog] Playback completed.");
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogDiscovered(uint logHash, AudioLogData data)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.Log("[AudioLog] Discovered.");
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogPlaying(uint logHash, float duration)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.Log("[AudioLog] Playing.");
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogLoadedCount(int discoveredCount)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.Log("[AudioLog] Loaded discovered logs.");
#endif
        }

        //  ISaveable

        public void PopulateSaveData(SaveData data)
        {
            if (data == null) return;

            if (data.audioLogDiscoveredIds == null)
                data.audioLogDiscoveredIds = new List<string>(math.max(0, maxSavedLogs)); // COLD ALLOC: List<string>[maxSavedLogs] — fallback discovered audio-log save list — owner: AudioLogSystem
            else
                data.audioLogDiscoveredIds.Clear();
            AudioLogDiscoveryBitMask.EnsureCapacity(ref data.audioLogDiscoveryBitWords);
            AudioLogDiscoveryBitMask.Clear(data.audioLogDiscoveryBitWords);
            EnsureSaveEncryptedFragmentArrays(data);
            data.audioLogEncryptedFragmentCount = 0;

            for (int i = 0; i < _resolvedLogHashCount; i++)
            {
                uint logHash = _resolvedLogHashes[i];
                if (!_discoveredLogHashes.Contains(logHash))
                {
                    continue;
                }

                AudioLogDiscoveryBitMask.Set(data.audioLogDiscoveryBitWords, i);
            }

            int partialCount = 0;
            bool hasEncryptedState = TryReadEncryptedFragmentState(
                out NativeArray<uint>.ReadOnly encryptedHashes,
                out NativeArray<uint>.ReadOnly encryptedRecoveredBits);
            for (int i = 0; hasEncryptedState && i < _encryptedFragmentStateCount && partialCount < SaveData.MaxEncryptedAudioLogFragments; i++)
            {
                uint logHash = encryptedHashes[i];
                uint recoveredBits = encryptedRecoveredBits[i] & EncryptedLogCompleteMask;
                if (logHash == 0u ||
                    recoveredBits == 0u ||
                    recoveredBits == EncryptedLogCompleteMask ||
                    _discoveredLogHashes.Contains(logHash))
                {
                    continue;
                }

                data.audioLogEncryptedFragmentHashes[partialCount] = logHash;
                data.audioLogEncryptedFragmentBits[partialCount] = recoveredBits;
                partialCount++;
            }

            data.audioLogEncryptedFragmentCount = partialCount;
        }

        public void LoadFromSaveData(SaveData data)
        {
            _discoveredLogHashes.Clear();
            ClearEncryptedFragmentState();
            if (_logLookupByHash.Count == 0 && allLogs != null && allLogs.Length > 0)
                BuildLogLookup();

            bool loadedPackedDiscoveryBits = LoadDiscoveredLogsFromPackedBits(data);
            int discoveredCount = !loadedPackedDiscoveryBits && data != null && data.audioLogDiscoveredIds != null
                ? data.audioLogDiscoveredIds.Count
                : 0;
            for (int i = 0; i < discoveredCount; i++)
            {
                uint logHash = ComputeAudioLogHash(data.audioLogDiscoveredIds[i]);
                if (logHash == 0u)
                    continue;

                _discoveredLogHashes.Add(logHash);
                if (_logLookupByHash.TryGetValue(logHash, out AudioLogData logData) && logData != null)
                {
                    TrackResolvedLogHash(logHash);
                    CacheDiscoveryNotificationHash(logHash, logData);
                }
            }

            LoadEncryptedFragmentState(data);

            LogLoadedCount(_discoveredLogHashes.Count);
        }

        private bool LoadDiscoveredLogsFromPackedBits(SaveData data)
        {
            if (data == null ||
                !AudioLogDiscoveryBitMask.HasExpectedCapacity(data.audioLogDiscoveryBitWords) ||
                !AudioLogDiscoveryBitMask.HasAnySet(data.audioLogDiscoveryBitWords))
            {
                return false;
            }

            int count = math.min(_resolvedLogHashCount, AudioLogDiscoveryBitMask.MaxLogCount);
            int nextIndex = 0;
            while (AudioLogDiscoveryBitMask.TryGetNextSetIndex(data.audioLogDiscoveryBitWords, nextIndex, count, out int i))
            {
                uint logHash = _resolvedLogHashes[i];
                if (logHash == 0u)
                {
                    nextIndex = i + 1;
                    continue;
                }

                _discoveredLogHashes.Add(logHash);
                if (_logLookupByHash.TryGetValue(logHash, out AudioLogData logData) && logData != null)
                    CacheDiscoveryNotificationHash(logHash, logData);

                nextIndex = i + 1;
            }

            return true;
        }

        public bool RecoverEncryptedAudioLogFragment(uint logHash, uint fragmentHash)
        {
            return RecoverEncryptedFragment(logHash, fragmentHash);
        }

        private static void EnsureSaveEncryptedFragmentArrays(SaveData data)
        {
            if (data == null)
                return;

            if (data.audioLogEncryptedFragmentHashes == null ||
                data.audioLogEncryptedFragmentHashes.Length < SaveData.MaxEncryptedAudioLogFragments)
            {
                data.audioLogEncryptedFragmentHashes = new uint[SaveData.MaxEncryptedAudioLogFragments];
            }
            else
            {
                Array.Clear(data.audioLogEncryptedFragmentHashes, 0, data.audioLogEncryptedFragmentHashes.Length);
            }

            if (data.audioLogEncryptedFragmentBits == null ||
                data.audioLogEncryptedFragmentBits.Length < SaveData.MaxEncryptedAudioLogFragments)
            {
                data.audioLogEncryptedFragmentBits = new uint[SaveData.MaxEncryptedAudioLogFragments];
            }
            else
            {
                Array.Clear(data.audioLogEncryptedFragmentBits, 0, data.audioLogEncryptedFragmentBits.Length);
            }
        }

        private void LoadEncryptedFragmentState(SaveData data)
        {
            if (data == null ||
                data.audioLogEncryptedFragmentHashes == null ||
                data.audioLogEncryptedFragmentBits == null)
            {
                return;
            }

            int count = math.clamp(
                data.audioLogEncryptedFragmentCount,
                0,
                math.min(
                    SaveData.MaxEncryptedAudioLogFragments,
                    math.min(data.audioLogEncryptedFragmentHashes.Length, data.audioLogEncryptedFragmentBits.Length)));

            for (int i = 0; i < count; i++)
            {
                uint logHash = data.audioLogEncryptedFragmentHashes[i];
                uint recoveredBits = data.audioLogEncryptedFragmentBits[i] & EncryptedLogCompleteMask;
                if (logHash == 0u || recoveredBits == 0u || _discoveredLogHashes.Contains(logHash))
                    continue;

                SetEncryptedFragmentBits(logHash, recoveredBits);
            }
        }
    }
}

