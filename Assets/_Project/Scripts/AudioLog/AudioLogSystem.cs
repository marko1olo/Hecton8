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
using Unity.Collections.LowLevel.Unsafe;
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
        private const int AudioLogTelemetryEntrySizeBytes = 64;
        private const int AudioGlitchParametersSizeBytes = 8;
        private const int NativeDtoAlignmentBytes = 8;
        private const uint EncryptedLogCompleteMask = 0xFu;
        private const SystemID OwnerSystemId = SystemID.Audio;
        private const uint VaultFallbackMissingVault = 1u;
        private const uint VaultFallbackPlaybackQueue = 2u;
        private const uint VaultFallbackEncryptedHashes = 4u;
        private const uint VaultFallbackEncryptedBits = 8u;
        private const uint VaultFallbackEncryptedState = VaultFallbackEncryptedHashes | VaultFallbackEncryptedBits;
        private const float NarrativeGlitchDeltaFallbackSeconds = 1f / 60f;
        private const float NarrativeGlitchFlutterFrequencyHz = 50f;
        private const float NarrativeGlitchFlutterMaxCents = 240f;
        private static readonly ulong PlaybackQueueMutationGuardMask = AudioLogMutationGuardBit(BufferID.AudioLogPlaybackQueue);
        private static readonly ulong EncryptedFragmentHashesMutationGuardMask = AudioLogMutationGuardBit(BufferID.AudioLogEncryptedFragmentHashes);
        private static readonly ulong EncryptedFragmentRecoveredBitsMutationGuardMask = AudioLogMutationGuardBit(BufferID.AudioLogEncryptedFragmentRecoveredBits);
        private static readonly ulong EncryptedFragmentStateMutationGuardMask = EncryptedFragmentHashesMutationGuardMask | EncryptedFragmentRecoveredBitsMutationGuardMask;
        private static readonly ulong TelemetryMutationGuardMask = AudioLogMutationGuardBit(BufferID.AudioLogTelemetryRing);
        private static readonly int _audioLogTelemetryEntryRuntimeSizeBytes = UnsafeUtility.SizeOf<AudioLogVaultTelemetryEntry>();
        private static readonly int _audioGlitchParametersRuntimeSizeBytes = UnsafeUtility.SizeOf<AudioGlitchParametersDTO>();
        private static readonly bool _audioLogTelemetryLayoutValid =
            _audioLogTelemetryEntryRuntimeSizeBytes == AudioLogTelemetryEntrySizeBytes &&
            (_audioLogTelemetryEntryRuntimeSizeBytes & (NativeDtoAlignmentBytes - 1)) == 0;
        private static readonly bool _audioGlitchParametersLayoutValid =
            _audioGlitchParametersRuntimeSizeBytes == AudioGlitchParametersSizeBytes &&
            (_audioGlitchParametersRuntimeSizeBytes & (NativeDtoAlignmentBytes - 1)) == 0;
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
        private VaultGenerationHandle<uint> _queuedLogHashesHandle;
        private VaultGenerationHandle<uint> _encryptedFragmentLogHashesHandle;
        private VaultGenerationHandle<uint> _encryptedFragmentRecoveredBitsHandle;
        private VaultGenerationHandle<AudioLogVaultTelemetryEntry> _telemetryRingHandle;
        private int _telemetryWriteCursor;
        private int _playbackQueueReadIndex;
        private int _playbackQueueWriteIndex;
        private int _encryptedFragmentStateCount;
        private int _vaultResolutionSuccessCount;
        private int _vaultResolutionFailureCount;
        private int _resolvedLogHashCount;
        private int _discoveryNotificationMissCount;
        private static readonly uint _QueueFullWarningHash = unchecked((uint)LocHash.Compute("AudioLogSystem.QueueFull"));
        private static readonly uint _LookupMissWarningHash = unchecked((uint)LocHash.Compute("AudioLogSystem.LookupMiss"));
        private static readonly uint _ResolvedLogCatalogFullWarningHash = unchecked((uint)LocHash.Compute("AudioLogSystem.ResolvedLogCatalogFull"));
        private static readonly uint _EncryptedFragmentStateFullWarningHash = unchecked((uint)LocHash.Compute("AudioLogSystem.EncryptedFragmentStateFull"));
        private static readonly uint _EncryptedVoiceRouteMissingWarningHash = unchecked((uint)LocHash.Compute("AudioLogSystem.EncryptedVoiceRouteMissing"));
        private static readonly uint _DiscoveryNotificationMissWarningHash = unchecked((uint)LocHash.Compute("AudioLogSystem.DiscoveryNotificationMiss"));
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
        private bool _runtimeOwnerAborted;
        private bool _registeredHotSwapListener;
        private bool _currentPlaybackBitCrushed;
        private bool _pendingPlaybackDirty;
        private bool _pendingPlaybackBitCrushed;
        private bool _pendingGlitchResetDirty;
        private float _pendingPlaybackVolume;
        private float _pendingNarrativeInterference01;
        private float _playbackGlitchElapsedSeconds;
        private uint _lastGlitchVisualSyncFrame;
        private AudioGlitchParametersDTO _currentPlaybackGlitch;
        private AudioGlitchParametersDTO _pendingPlaybackGlitch;
        private AudioClip _pendingPlaybackClip;
        private bool _resolvedLogCatalogFullTelemetryArmed = true;
        private bool _encryptedVoiceRouteMissingTelemetryArmed = true;
        private IAudioService _cachedAudioService;
        private ISpatialAudioNarrativeRadioSink _cachedNarrativeAudioSink;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private ISaveService _cachedSaveService;
        private ISaveService _registeredSaveService;
        private bool _saveRegistered;

        //  ISaveable

        public int SavePriority => 6;
        public int LoadPriority => 6;

        //  PUBLIC PROPERTIES

        public bool IsPlaying => !_runtimeOwnerAborted && _isPlaying;
        public bool IsNarrativeQueueBlocked => !_runtimeOwnerAborted && (_isPlaying || _atmosphericWarningActive || _queueCount > 0);
        public bool IsAudioLogRuntimeReady => !_runtimeOwnerAborted && _serviceRegistered && isActiveAndEnabled;
        public AudioLogData CurrentLog => _runtimeOwnerAborted ? null : _currentLog;
        public int DiscoveredCount => _runtimeOwnerAborted ? 0 : _discoveredLogHashes.Count;
        public int DiscoveredAudioLogCount => _runtimeOwnerAborted ? 0 : _discoveredLogHashes.Count;
        public int DiscoveryNotificationMissCount => _runtimeOwnerAborted ? 0 : _discoveryNotificationMissCount;
        public bool CurrentPlaybackBitCrushed => !_runtimeOwnerAborted && _currentPlaybackBitCrushed;

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


        /// <summary>
        /// Resolve-or-create the sole GlobalRegistry.AudioLogs owner.
        /// GUID ca4d93977437b664fbbf3dcd8b694d38 has ZERO live scene/prefab hits
        /// (only Assets/_Recovery leftovers). HectonLoreSystemsRoot.SetupAllSystems
        /// is editor ContextMenu-only and does not run in play mode.
        /// OnEnable only registers when already present; without this factory
        /// NarrativeDiscovery, AudioLogPickup, PDADataLogTab, FirstHourDirector,
        /// ProceduralLoreDirector, NarrativeProgressionBridge, AtlasSignalSystem,
        /// HectonPlayerHealth and EmergencyServiceRelay hit permanent null.
        /// </summary>
        public static AudioLogSystem EnsureRuntimeInstance()
        {
            AudioLogSystem registered = GlobalRegistry.AudioLogs;
            if (IsAudioLogSystemUsable(registered))
                return registered;

            if (!ReferenceEquals(registered, null))
            {
                GlobalRegistry.UnregisterAudioLogRuntime(registered);
                registered._serviceRegistered = false;
            }

            if (!Application.isPlaying)
                return null;

            // Player-build construction path: zero authored scene/prefab hits for this owner.
            GameObject runtimeRoot = new GameObject("[AudioLogSystem]"); // COLD ALLOC
            return runtimeRoot.AddComponent<AudioLogSystem>();
        }

        private void OnEnable()
        {
            if (!TryRegisterService())
                return;

            CacheRegistryServicesCold();
            EnsureVaultBuffersCold();
            TryRegisterHotSwapListener();
            TryRegister();

            TryRegisterSaveParticipant();
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
                return;

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
            FlushPendingNarrativeRadioGlitchReset();
            TryUnregisterLateFrame();
            ClearAtmosphericWarningBlocker();
            ClearDiscoveryNotificationDiagnostics();
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregisterSaveParticipant();
            TryUnregister();
            if (_isPlaying)
                StopPlayback();
            ClearPendingPlaybackSync();
            FlushPendingNarrativeRadioGlitchReset();
            TryUnregisterLateFrame();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            ReleaseVaultBuffers(_dataVault);
            ClearDiscoveryNotificationDiagnostics();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (_runtimeOwnerAborted)
                return;

            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Audio:
                    ResetPreviousNarrativeRadioSink(previousService as ISpatialAudioNarrativeRadioSink, currentService);
                    CacheAudioService(currentService as IAudioService);
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _cachedPlayerContext = ResolveInitializedPlayerContext(currentService as IPlayerRuntimeContext);
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
                    TryUnregister();
                    TryUnregisterLateFrame();
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
            if (_runtimeOwnerAborted)
                return;

            bool queuedPlaybackStarted = TickAtmosphericWarningBlocker();
            if (queuedPlaybackStarted)
                return;

            if (!_isPlaying && !_atmosphericWarningActive && _queueCount > 0)
            {
                TryStartNextQueuedLog();
                if (_isPlaying)
                    return;
            }

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
            QueueNarrativeRadioGlitchReset();
            AudioLogEvents.TryRaisePlaybackCompleted(completedHash, completedLog);

            LogPlaybackCompleted(completedHash);
            TryStartNextQueuedLog();
        }

        public void LateFrameTick()
        {
            if (_runtimeOwnerAborted)
                return;

            FlushPendingPlaybackVisualSync();
            RefreshActiveNarrativeRadioGlitchVisualSync();
            FlushPendingNarrativeRadioGlitchReset();

            if (!_pendingPlaybackDirty && !_isPlaying && !_pendingGlitchResetDirty)
                TryUnregisterLateFrame();
        }

        //  PUBLIC API

        /// <summary>
        /// Marks a log as discovered without starting playback.
        /// </summary>
        public void DiscoverLog(AudioLogData data)
        {
            if (_runtimeOwnerAborted)
                return;

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
            TryPushDiscoveryNotification(notificationHash, discoveredHash);

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
            if (_runtimeOwnerAborted)
                return;

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
            if (_runtimeOwnerAborted)
                return false;

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
            if (_runtimeOwnerAborted)
                return 0u;

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
            if (_runtimeOwnerAborted)
                return false;

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
            if (_runtimeOwnerAborted)
                return;

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
            BeginNarrativeRadioGlitch(in glitch);

            AudioLogEvents.TryRaisePlaybackStarted(_currentLogHash, _playbackTimer, in glitch, data);

            LogPlaying(logHash, playbackDuration);
        }

        private bool IsEncryptedLogFullyRecovered(uint logHash)
        {
            return (GetRecoveredEncryptedBits(logHash) & EncryptedLogCompleteMask) == EncryptedLogCompleteMask;
        }

        private void PlayEncryptedPartialPreview(uint logHash, AudioLogData data)
        {
            if (_runtimeOwnerAborted)
                return;

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
            BeginNarrativeRadioGlitch(in glitch);

            AudioLogEvents.TryRaisePlaybackStarted(_currentLogHash, _playbackTimer, in glitch, data);
        }

        private bool QueuePlaybackVisualSync(
            AudioClip clip,
            float volume,
            bool preferBitCrush,
            in AudioGlitchParametersDTO glitch)
        {
            if (_runtimeOwnerAborted)
                return false;

            if (clip == null)
                return false;

            ISpatialAudioNarrativeRadioSink narrativeAudioSink = ResolveNarrativeAudioSink();
            bool bitCrushRouteAvailable = preferBitCrush && narrativeAudioSink != null;
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
            if (_runtimeOwnerAborted)
                return;

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

            ISpatialAudioNarrativeRadioSink narrativeAudioSink = ResolveNarrativeAudioSink();
            if (narrativeAudioSink != null)
            {
                narrativeAudioSink.SetNarrativeRadioInterference(math.max(interference01, GlitchBandPassTo01(glitch.BandPassByte)));
                if (useBitCrush && narrativeAudioSink.TryPlayStatic2DBitCrushed(playbackClip, volume))
                    return;
            }

            IAudioService audioManager = ResolveAudioService();
            if (audioManager != null)
                audioManager.PlayStatic2D(playbackClip, volume);
        }

        private void BeginNarrativeRadioGlitch(in AudioGlitchParametersDTO glitch)
        {
            if (_runtimeOwnerAborted)
                return;

            if (!_audioGlitchParametersLayoutValid)
            {
                _currentPlaybackGlitch = default;
                return;
            }

            _currentPlaybackGlitch = AudioGlitchParametersDTO.Sanitize(in glitch);
            _playbackGlitchElapsedSeconds = 0f;
            _lastGlitchVisualSyncFrame = 0u;
            _pendingGlitchResetDirty = false;
            TryRegisterLateFrame();
        }

        private void RefreshActiveNarrativeRadioGlitchVisualSync()
        {
            if (_runtimeOwnerAborted)
                return;

            if (!_isPlaying)
                return;

            ISpatialAudioNarrativeRadioSink narrativeAudioSink = ResolveNarrativeAudioSink();
            if (narrativeAudioSink == null)
                return;

            uint frame = unchecked((uint)SystemDispatcher.CurrentFrameIndex);
            if (_lastGlitchVisualSyncFrame == frame)
                return;

            _lastGlitchVisualSyncFrame = frame;
            float deltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            if (!math.isfinite(deltaTime) || deltaTime <= 0f || deltaTime > 0.25f)
                deltaTime = NarrativeGlitchDeltaFallbackSeconds;

            _playbackGlitchElapsedSeconds = math.min(
                _playbackGlitchElapsedSeconds + deltaTime,
                86400f);

            AudioGlitchParametersDTO glitch = _currentPlaybackGlitch;
            float corruption01 = GlitchPermilleTo01(glitch.CorruptionPermille);
            float bitCrushMix01 = math.max(
                math.saturate((corruption01 - 0.2f) * 1.25f),
                GlitchPermilleTo01(glitch.BitCrushPermille));
            float pitchShiftCents = glitch.PitchShiftCents +
                                    (math.sin(_playbackGlitchElapsedSeconds * NarrativeGlitchFlutterFrequencyHz) *
                                     corruption01 *
                                     NarrativeGlitchFlutterMaxCents);
            float interference01 = math.max(ResolveNarrativeRadioInterference01(), GlitchBandPassTo01(glitch.BandPassByte));
            narrativeAudioSink.SetNarrativeRadioInterference(math.max(interference01, corruption01));
            narrativeAudioSink.SetNarrativeRadioGlitch(
                corruption01,
                bitCrushMix01,
                pitchShiftCents,
                Sanitize01(HomeostasisBrain.GlobalQualityWeight));
        }

        private void QueueNarrativeRadioGlitchReset()
        {
            if (_runtimeOwnerAborted)
                return;

            _currentPlaybackGlitch = default;
            _playbackGlitchElapsedSeconds = 0f;
            _lastGlitchVisualSyncFrame = 0u;
            _pendingGlitchResetDirty = true;
            TryRegisterLateFrame();
        }

        private void FlushPendingNarrativeRadioGlitchReset()
        {
            if (_runtimeOwnerAborted)
                return;

            if (!_pendingGlitchResetDirty)
                return;

            ISpatialAudioNarrativeRadioSink narrativeAudioSink = ResolveNarrativeAudioSink();
            if (narrativeAudioSink == null)
            {
                TryRegisterLateFrame();
                return;
            }

            ResetNarrativeRadioSink(narrativeAudioSink);
            _pendingGlitchResetDirty = false;
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
            if (!_audioGlitchParametersLayoutValid)
                return default;

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

            AudioGlitchParametersDTO glitch = default;
            glitch.CorruptionPermille = corruptionPermille;
            glitch.BitCrushPermille = bitCrushPermille;
            glitch.PitchShiftCents = pitchShiftCents;
            glitch.BandPassByte = bandPassByte;
            glitch.Flags = flags;
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
            float depthMeters = ResolvePlayerDepthMeters(playerContext);
            float depth01 = math.saturate(
                (depthMeters - NarrativeRadioDeepStartDepthMeters) /
                math.max(1f, NarrativeRadioDeepFullDepthMeters - NarrativeRadioDeepStartDepthMeters));

            TraumaDispatcher traumaDispatcher = playerContext != null ? playerContext.TraumaDispatcher : null;
            float radiation01 = traumaDispatcher != null ? Sanitize01(traumaDispatcher.HazardRadiationSignal01) : 0f;
            return math.max(depth01, radiation01);
        }

        private static float ResolvePlayerDepthMeters(IPlayerRuntimeContext playerContext)
        {
            if (playerContext == null || !playerContext.IsInitialized)
                return 0f;

            if (playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                math.isfinite(movementState.DepthMeters))
            {
                return math.max(0f, movementState.DepthMeters);
            }

            return 0f;
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
            if (_runtimeOwnerAborted)
                return;

            _atmosphericWarningActive = true;
            _atmosphericWarningTimer = math.max(_atmosphericWarningTimer, ResolvePlaybackDuration(durationSeconds));
        }

        public void NotifyAtmosphericWarningCompleted()
        {
            if (_runtimeOwnerAborted)
                return;

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
            if (_runtimeOwnerAborted)
                return;

            AudioLogData stoppedLog = _currentLog;
            uint stoppedHash = _currentLogHash;
            bool hadPlayback = _isPlaying || stoppedLog != null || _pendingPlaybackDirty || _currentPlaybackBitCrushed;
            _isPlaying = false;
            _currentLog = null;
            _currentLogHash = 0u;
            _playbackTimer = 0f;
            _currentPlaybackBitCrushed = false;
            ClearPendingPlaybackSync();
            ClearPlaybackQueue();
            if (!hadPlayback)
                return;

            QueueNarrativeRadioGlitchReset();
            if (stoppedLog != null)
                AudioLogEvents.TryRaisePlaybackStopped(stoppedHash, stoppedLog);
        }

        private void ClearTransientPlaybackState()
        {
            _isPlaying = false;
            _currentLog = null;
            _currentLogHash = 0u;
            _playbackTimer = 0f;
            _currentPlaybackBitCrushed = false;
            ClearPendingPlaybackSync();
            _pendingGlitchResetDirty = false;
            _currentPlaybackGlitch = default;
            _pendingPlaybackGlitch = default;
            ClearPlaybackQueue();
            ClearAtmosphericWarningBlocker();
        }

        /// <summary>
        /// Checks whether a log has been discovered.
        /// </summary>
        public bool IsDiscovered(string logId)
        {
            if (_runtimeOwnerAborted)
                return false;

            return IsDiscovered(ComputeAudioLogHash(logId));
        }

        public bool IsDiscovered(uint logHash)
        {
            if (_runtimeOwnerAborted)
                return false;

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

        private void TryPushDiscoveryNotification(uint notificationHash, uint logHash)
        {
            if (notificationHash == 0u)
            {
                ReportDiscoveryNotificationMiss(logHash);
                return;
            }

            if (NotificationEvents.TryPushRegisteredInfo(notificationHash))
                return;

            ReportDiscoveryNotificationMiss(logHash);
        }

        private void ReportDiscoveryNotificationMiss(uint logHash)
        {
            _discoveryNotificationMissCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _DiscoveryNotificationMissWarningHash,
                _NarrativeQueueContextHash ^ logHash,
                math.max(1, _discoveryNotificationMissCount));
        }

        private void ClearDiscoveryNotificationDiagnostics()
        {
            _discoveryNotificationMissCount = 0;
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

            int writeIndex = _playbackQueueWriteIndex;
            if (!TryAcquirePlaybackQueueMutationView(out NativeArray<uint> queue, out IDataVault guardVault))
            {
                GlobalTelemetryBus.PublishPerformanceWarning(_QueueFullWarningHash, _NarrativeQueueContextHash, _queueCount);
                return;
            }

            try
            {
                queue[writeIndex] = logHash;
            }
            finally
            {
                ReleaseVaultMutation(guardVault, PlaybackQueueMutationGuardMask);
            }

            AddQueuedLogHash(logHash);
            _playbackQueueWriteIndex = (writeIndex + 1) % PlaybackQueueCapacity;
            _queueCount++;
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
            int readIndex = _playbackQueueReadIndex;
            if (TryAcquirePlaybackQueueMutationView(out NativeArray<uint> queue, out IDataVault guardVault))
            {
                try
                {
                    nextHash = queue[readIndex];
                    queue[readIndex] = 0u;

                    _playbackQueueReadIndex = (readIndex + 1) % PlaybackQueueCapacity;
                    _queueCount = math.max(0, _queueCount - 1);
                    RebuildQueuedLogHashDedupFromQueue(queue, _playbackQueueReadIndex, _queueCount);
                }
                finally
                {
                    ReleaseVaultMutation(guardVault, PlaybackQueueMutationGuardMask);
                }
            }

            if (nextHash != 0u && _logLookupByHash.TryGetValue(nextHash, out AudioLogData next) && next != null)
                PlayLogByHash(nextHash, next);
        }

        private void RebuildQueuedLogHashDedupFromQueue(NativeArray<uint> queue, int readIndex, int queueCount)
        {
            ClearQueuedLogHashes();
            if (!queue.IsCreated || queue.Length < PlaybackQueueCapacity || queueCount <= 0)
                return;

            int count = math.min(queueCount, PlaybackQueueCapacity);
            int index = math.clamp(readIndex, 0, PlaybackQueueCapacity - 1);
            for (int i = 0; i < count; i++)
            {
                uint logHash = queue[index];
                if (logHash != 0u && !IsPlaybackQueued(logHash))
                    AddQueuedLogHash(logHash);

                index = (index + 1) % PlaybackQueueCapacity;
            }
        }

        private bool TickAtmosphericWarningBlocker()
        {
            if (!_atmosphericWarningActive)
                return false;

            _atmosphericWarningTimer -= 0.5f; // SlowTick ~0.5s
            if (_atmosphericWarningTimer > 0f)
                return false;

            bool wasPlaying = _isPlaying;
            NotifyAtmosphericWarningCompleted();
            return !wasPlaying && _isPlaying;
        }

        private void ClearAtmosphericWarningBlocker()
        {
            _atmosphericWarningActive = false;
            _atmosphericWarningTimer = 0f;
        }

        private unsafe void ClearPlaybackQueue()
        {
            if (TryAcquirePlaybackQueueMutationView(out NativeArray<uint> queue, out IDataVault guardVault))
            {
                try
                {
                    UnsafeUtility.MemClear(queue.GetUnsafePtr(), PlaybackQueueCapacity * UnsafeUtility.SizeOf<uint>());
                }
                finally
                {
                    ReleaseVaultMutation(guardVault, PlaybackQueueMutationGuardMask);
                }
            }

            _queueCount = 0;
            _playbackQueueReadIndex = 0;
            _playbackQueueWriteIndex = 0;
            ClearQueuedLogHashes();
        }

        private void EnsureVaultBuffersCold()
        {
            if (_runtimeOwnerAborted || !_serviceRegistered)
                return;

            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            EnsureVaultHandle(vault, ref _queuedLogHashesHandle, BufferID.AudioLogPlaybackQueue, PlaybackQueueCapacity, NativeArrayOptions.ClearMemory);
            EnsureVaultHandle(vault, ref _encryptedFragmentLogHashesHandle, BufferID.AudioLogEncryptedFragmentHashes, EncryptedFragmentStateCapacity, NativeArrayOptions.ClearMemory);
            EnsureVaultHandle(vault, ref _encryptedFragmentRecoveredBitsHandle, BufferID.AudioLogEncryptedFragmentRecoveredBits, EncryptedFragmentStateCapacity, NativeArrayOptions.ClearMemory);
            EnsureVaultHandle(vault, ref _telemetryRingHandle, BufferID.AudioLogTelemetryRing, AudioLogTelemetryCapacity, NativeArrayOptions.ClearMemory);
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
                vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return;
            }

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, OwnerSystemId, options);
        }

        private void ReleaseVaultBuffers(IDataVault vault)
        {
            ReleaseVaultHandle(vault, ref _queuedLogHashesHandle, BufferID.AudioLogPlaybackQueue);
            ReleaseVaultHandle(vault, ref _encryptedFragmentLogHashesHandle, BufferID.AudioLogEncryptedFragmentHashes);
            ReleaseVaultHandle(vault, ref _encryptedFragmentRecoveredBitsHandle, BufferID.AudioLogEncryptedFragmentRecoveredBits);
            ReleaseVaultHandle(vault, ref _telemetryRingHandle, BufferID.AudioLogTelemetryRing);
            _queueCount = 0;
            _playbackQueueReadIndex = 0;
            _playbackQueueWriteIndex = 0;
            _encryptedFragmentStateCount = 0;
            _telemetryWriteCursor = 0;
            ClearDiscoveryNotificationDiagnostics();
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
            if (_runtimeOwnerAborted)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null || !IsAudioLogVaultHandle(in handle, bufferId))
                return false;

            if (!vault.TryReadOnlyHandle(in handle, out buffer))
                return false;

            return buffer.IsCreated && buffer.Length >= requiredLength;
        }

        private bool TryAcquirePlaybackQueueMutationView(out NativeArray<uint> buffer, out IDataVault guardVault)
        {
            return TryAcquireVaultMutation(
                in _queuedLogHashesHandle,
                BufferID.AudioLogPlaybackQueue,
                PlaybackQueueCapacity,
                PlaybackQueueMutationGuardMask,
                VaultFallbackPlaybackQueue,
                out buffer,
                out guardVault);
        }

        private bool TryAcquireVaultMutation<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            ulong mutationGuardMask,
            uint fallbackFlag,
            out NativeArray<T> buffer,
            out IDataVault guardVault) where T : struct
        {
            buffer = default;
            guardVault = _dataVault;
            if (_runtimeOwnerAborted)
            {
                guardVault = null;
                return false;
            }

            if (guardVault == null || !IsAudioLogVaultHandle(in handle, bufferId))
            {
                _vaultResolutionFailureCount++;
                RecordVaultTelemetry(fallbackFlag | VaultFallbackMissingVault, bufferId);
                guardVault = null;
                return false;
            }

            if (mutationGuardMask == 0UL ||
                guardVault.IsCompactionFenceActive ||
                !guardVault.TryAcquireMutationGuard(mutationGuardMask))
            {
                _vaultResolutionFailureCount++;
                RecordVaultTelemetry(fallbackFlag, bufferId);
                guardVault = null;
                return false;
            }

            bool acquired = true;
            bool validationFailed = false;
            try
            {
                if (guardVault.IsCompactionFenceActive ||
                    !guardVault.TryResolveHandle(in handle, out buffer) ||
                    !buffer.IsCreated ||
                    buffer.Length < requiredLength)
                {
                    validationFailed = true;
                    return false;
                }

                _vaultResolutionSuccessCount++;
                acquired = false;
                return true;
            }
            finally
            {
                if (acquired)
                {
                    ReleaseVaultMutation(guardVault, mutationGuardMask);
                    if (validationFailed)
                    {
                        _vaultResolutionFailureCount++;
                        RecordVaultTelemetry(fallbackFlag, bufferId);
                    }

                    guardVault = null;
                    buffer = default;
                }
            }
        }

        private static void ReleaseVaultMutation(IDataVault guardVault, ulong mutationGuardMask)
        {
            guardVault?.ReleaseMutationGuard(mutationGuardMask);
        }

        private static ulong AudioLogMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private bool TryReadEncryptedFragmentState(
            out NativeArray<uint>.ReadOnly hashes,
            out NativeArray<uint>.ReadOnly recoveredBits)
        {
            if (_runtimeOwnerAborted)
            {
                hashes = default;
                recoveredBits = default;
                return false;
            }

            bool hasHashes = TryReadVaultBuffer(in _encryptedFragmentLogHashesHandle, BufferID.AudioLogEncryptedFragmentHashes, EncryptedFragmentStateCapacity, out hashes);
            bool hasBits = TryReadVaultBuffer(in _encryptedFragmentRecoveredBitsHandle, BufferID.AudioLogEncryptedFragmentRecoveredBits, EncryptedFragmentStateCapacity, out recoveredBits);
            return hasHashes && hasBits;
        }

        private void ClearEncryptedFragmentState()
        {
            if (_runtimeOwnerAborted)
                return;

            int count = _encryptedFragmentStateCount;
            if (count > EncryptedFragmentStateCapacity)
                count = EncryptedFragmentStateCapacity;

            _encryptedFragmentStateCount = 0;
            if (count <= 0)
                return;

            TryClearEncryptedFragmentBuffer(
                in _encryptedFragmentLogHashesHandle,
                BufferID.AudioLogEncryptedFragmentHashes,
                count,
                VaultFallbackEncryptedHashes);
            TryClearEncryptedFragmentBuffer(
                in _encryptedFragmentRecoveredBitsHandle,
                BufferID.AudioLogEncryptedFragmentRecoveredBits,
                count,
                VaultFallbackEncryptedBits);
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

            if (!TryReadEncryptedFragmentState(out NativeArray<uint>.ReadOnly hashes, out NativeArray<uint>.ReadOnly recoveredBitBuffer))
            {
                _vaultResolutionFailureCount++;
                RecordVaultTelemetry(VaultFallbackEncryptedState, BufferID.AudioLogEncryptedFragmentHashes);
                return false;
            }

            int activeCount = _encryptedFragmentStateCount;
            if (activeCount < 0)
                activeCount = 0;
            if (activeCount > EncryptedFragmentStateCapacity)
                activeCount = EncryptedFragmentStateCapacity;

            int slot = -1;
            for (int i = 0; i < activeCount; i++)
            {
                if (hashes[i] != logHash)
                    continue;

                slot = i;
                break;
            }

            uint clampedRecoveredBits = recoveredBits & EncryptedLogCompleteMask;
            if (slot >= 0)
            {
                return TryWriteEncryptedFragmentValue(
                    in _encryptedFragmentRecoveredBitsHandle,
                    BufferID.AudioLogEncryptedFragmentRecoveredBits,
                    slot,
                    clampedRecoveredBits,
                    VaultFallbackEncryptedBits);
            }

            if (activeCount >= EncryptedFragmentStateCapacity)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _EncryptedFragmentStateFullWarningHash,
                    _NarrativeQueueContextHash,
                    activeCount);
                return false;
            }

            slot = activeCount;
            if (!TryWriteEncryptedFragmentPair(slot, logHash, clampedRecoveredBits))
                return false;

            _encryptedFragmentStateCount = activeCount + 1;
            return true;
        }

        private bool TryWriteEncryptedFragmentPair(int slot, uint logHash, uint recoveredBits)
        {
            if ((uint)slot >= EncryptedFragmentStateCapacity || logHash == 0u)
                return false;

            if (!TryAcquireEncryptedFragmentMutationView(
                    out NativeArray<uint> hashes,
                    out NativeArray<uint> recoveredBitBuffer,
                    out IDataVault guardVault))
            {
                return false;
            }

            try
            {
                recoveredBitBuffer[slot] = recoveredBits & EncryptedLogCompleteMask;
                hashes[slot] = logHash;
                return true;
            }
            finally
            {
                ReleaseVaultMutation(guardVault, EncryptedFragmentStateMutationGuardMask);
            }
        }

        private bool TryAcquireEncryptedFragmentMutationView(
            out NativeArray<uint> hashes,
            out NativeArray<uint> recoveredBits,
            out IDataVault guardVault)
        {
            hashes = default;
            recoveredBits = default;
            guardVault = _dataVault;
            if (_runtimeOwnerAborted)
            {
                guardVault = null;
                return false;
            }

            if (guardVault == null ||
                !IsAudioLogVaultHandle(in _encryptedFragmentLogHashesHandle, BufferID.AudioLogEncryptedFragmentHashes) ||
                !IsAudioLogVaultHandle(in _encryptedFragmentRecoveredBitsHandle, BufferID.AudioLogEncryptedFragmentRecoveredBits))
            {
                _vaultResolutionFailureCount++;
                RecordVaultTelemetry(VaultFallbackEncryptedState | VaultFallbackMissingVault, BufferID.AudioLogEncryptedFragmentHashes);
                guardVault = null;
                return false;
            }

            if (guardVault.IsCompactionFenceActive ||
                !guardVault.TryAcquireMutationGuard(EncryptedFragmentStateMutationGuardMask))
            {
                _vaultResolutionFailureCount++;
                RecordVaultTelemetry(VaultFallbackEncryptedState, BufferID.AudioLogEncryptedFragmentHashes);
                guardVault = null;
                return false;
            }

            bool acquired = true;
            bool validationFailed = false;
            try
            {
                if (guardVault.IsCompactionFenceActive ||
                    !guardVault.TryResolveHandle(in _encryptedFragmentLogHashesHandle, out hashes) ||
                    !guardVault.TryResolveHandle(in _encryptedFragmentRecoveredBitsHandle, out recoveredBits) ||
                    !hashes.IsCreated ||
                    !recoveredBits.IsCreated ||
                    hashes.Length < EncryptedFragmentStateCapacity ||
                    recoveredBits.Length < EncryptedFragmentStateCapacity)
                {
                    validationFailed = true;
                    return false;
                }

                _vaultResolutionSuccessCount++;
                acquired = false;
                return true;
            }
            finally
            {
                if (acquired)
                {
                    ReleaseVaultMutation(guardVault, EncryptedFragmentStateMutationGuardMask);
                    if (validationFailed)
                    {
                        _vaultResolutionFailureCount++;
                        RecordVaultTelemetry(VaultFallbackEncryptedState, BufferID.AudioLogEncryptedFragmentHashes);
                    }

                    guardVault = null;
                    hashes = default;
                    recoveredBits = default;
                }
            }
        }

        private unsafe bool TryClearEncryptedFragmentBuffer(
            in VaultGenerationHandle<uint> handle,
            BufferID bufferId,
            int count,
            uint fallbackFlag)
        {
            if (count <= 0)
                return true;

            if (count > EncryptedFragmentStateCapacity)
                count = EncryptedFragmentStateCapacity;

            if (!TryAcquireVaultMutation(
                    in handle,
                    bufferId,
                    EncryptedFragmentStateCapacity,
                    EncryptedFragmentStateMutationGuardMask,
                    fallbackFlag,
                    out NativeArray<uint> buffer,
                    out IDataVault guardVault))
            {
                return false;
            }

            try
            {
                UnsafeUtility.MemClear(buffer.GetUnsafePtr(), count * UnsafeUtility.SizeOf<uint>());
                return true;
            }
            finally
            {
                ReleaseVaultMutation(guardVault, EncryptedFragmentStateMutationGuardMask);
            }
        }

        private bool TryWriteEncryptedFragmentValue(
            in VaultGenerationHandle<uint> handle,
            BufferID bufferId,
            int slot,
            uint value,
            uint fallbackFlag)
        {
            if ((uint)slot >= EncryptedFragmentStateCapacity)
                return false;

            if (!TryAcquireVaultMutation(
                    in handle,
                    bufferId,
                    EncryptedFragmentStateCapacity,
                    EncryptedFragmentStateMutationGuardMask,
                    fallbackFlag,
                    out NativeArray<uint> buffer,
                    out IDataVault guardVault))
            {
                return false;
            }

            try
            {
                buffer[slot] = value;
                return true;
            }
            finally
            {
                ReleaseVaultMutation(guardVault, EncryptedFragmentStateMutationGuardMask);
            }
        }

        private void RecordVaultTelemetry(uint fallbackFlags, BufferID bufferId)
        {
            if (_runtimeOwnerAborted)
                return;

            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !_audioLogTelemetryLayoutValid ||
                !IsAudioLogVaultHandle(in _telemetryRingHandle, BufferID.AudioLogTelemetryRing))
            {
                return;
            }

            int frameIndex = SystemDispatcher.CurrentFrameIndex;
            int index = math.clamp(_telemetryWriteCursor, 0, AudioLogTelemetryCapacity - 1);
            int nextIndex = index + 1;
            if (nextIndex >= AudioLogTelemetryCapacity)
                nextIndex = 0;

            AudioLogVaultTelemetryEntry entry = default;
            entry.FrameIndex = (uint)frameIndex;
            entry.FallbackFlags = fallbackFlags;
            entry.LastBufferId = unchecked((uint)(int)bufferId);
            entry.ExpectedGeneration = ResolveExpectedGeneration(bufferId);
            entry.ActualGeneration = ResolveActualGeneration(vault, bufferId);
            entry.QueueCount = _queueCount;
            entry.EncryptedFragmentCount = _encryptedFragmentStateCount;
            entry.SuccessfulVaultResolutions = _vaultResolutionSuccessCount;
            entry.StaleHandleFailures = _vaultResolutionFailureCount;
            entry.EstimatedMicroseconds = 0;

            if (!vault.TryAcquireMutationGuard(TelemetryMutationGuardMask))
                return;

            try
            {
                if (vault.IsCompactionFenceActive ||
                    !vault.TryResolveHandle(in _telemetryRingHandle, out NativeArray<AudioLogVaultTelemetryEntry> telemetry) ||
                    !telemetry.IsCreated ||
                    telemetry.Length < AudioLogTelemetryCapacity)
                {
                    return;
                }

                telemetry[index] = entry;
                _telemetryWriteCursor = nextIndex;
            }
            finally
            {
                ReleaseVaultMutation(vault, TelemetryMutationGuardMask);
            }
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
            if (_runtimeOwnerAborted || !_serviceRegistered || _registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
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
            if (_runtimeOwnerAborted || !_serviceRegistered || _lateFrameRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
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
            if (_runtimeOwnerAborted)
                return;

            CacheAudioService(GlobalRegistry.Audio);
            _cachedPlayerContext = ResolveInitializedPlayerContext(Hecton8.Core.GlobalRegistry.Player);
            _cachedSaveService = GlobalRegistry.Save;
            RebindDataVaultCold(GlobalRegistry.DataVault, ensureBuffers: false);
        }

        private static IPlayerRuntimeContext ResolveInitializedPlayerContext(IPlayerRuntimeContext playerContext)
        {
            return playerContext != null && playerContext.IsInitialized ? playerContext : null;
        }

        private void CacheAudioService(IAudioService audioService)
        {
            if (_runtimeOwnerAborted)
                return;

            if (!IsAudioServiceUsable(audioService))
            {
                _cachedAudioService = null;
                _cachedNarrativeAudioSink = null;
                return;
            }

            _cachedAudioService = audioService;
            ISpatialAudioNarrativeRadioSink narrativeAudioSink = audioService as ISpatialAudioNarrativeRadioSink;
            _cachedNarrativeAudioSink = IsNarrativeAudioSinkUsable(narrativeAudioSink) ? narrativeAudioSink : null;

            if (_cachedNarrativeAudioSink != null && (_isPlaying || _pendingGlitchResetDirty))
                TryRegisterLateFrame();
        }

        private IAudioService ResolveAudioService()
        {
            if (_runtimeOwnerAborted)
                return null;

            IAudioService audioService = _cachedAudioService;
            if (IsAudioServiceUsable(audioService))
                return audioService;

            _cachedAudioService = null;
            _cachedNarrativeAudioSink = null;
            return null;
        }

        private ISpatialAudioNarrativeRadioSink ResolveNarrativeAudioSink()
        {
            if (_runtimeOwnerAborted)
                return null;

            IAudioService audioService = ResolveAudioService();
            if (audioService == null)
                return null;

            ISpatialAudioNarrativeRadioSink narrativeAudioSink = _cachedNarrativeAudioSink;
            if (ReferenceEquals(narrativeAudioSink, audioService) && IsNarrativeAudioSinkUsable(narrativeAudioSink))
                return narrativeAudioSink;

            narrativeAudioSink = audioService as ISpatialAudioNarrativeRadioSink;
            _cachedNarrativeAudioSink = IsNarrativeAudioSinkUsable(narrativeAudioSink) ? narrativeAudioSink : null;
            return _cachedNarrativeAudioSink;
        }

        private void ResetPreviousNarrativeRadioSink(ISpatialAudioNarrativeRadioSink previousSink, object currentService)
        {
            if (_runtimeOwnerAborted)
                return;

            if (previousSink == null ||
                ReferenceEquals(previousSink, currentService) ||
                !IsNarrativeAudioSinkUsable(previousSink))
            {
                return;
            }

            ResetNarrativeRadioSink(previousSink);
        }

        private void ResetNarrativeRadioSink(ISpatialAudioNarrativeRadioSink narrativeAudioSink)
        {
            if (!IsNarrativeAudioSinkUsable(narrativeAudioSink))
                return;

            narrativeAudioSink.SetNarrativeRadioInterference(ResolveNarrativeRadioInterference01());
            narrativeAudioSink.SetNarrativeRadioGlitch(0f, 0f, 0f, Sanitize01(HomeostasisBrain.GlobalQualityWeight));
        }

        private static bool IsAudioServiceUsable(IAudioService audioService)
        {
            if (audioService == null || !audioService.IsAudioRuntimeReady)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private static bool IsNarrativeAudioSinkUsable(ISpatialAudioNarrativeRadioSink narrativeAudioSink)
        {
            if (narrativeAudioSink == null)
                return false;

            if (narrativeAudioSink is IAudioService audioService && !audioService.IsAudioRuntimeReady)
                return false;

            if (narrativeAudioSink is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
        }

        private void TryRegisterSaveParticipant()
        {
            if (_runtimeOwnerAborted || !_serviceRegistered || _saveRegistered || !Application.isPlaying || !isActiveAndEnabled)
                return;

            ISaveService saveService = _cachedSaveService;
            if (!IsSaveServiceUsable(saveService))
            {
                saveService = GlobalRegistry.Save;
                _cachedSaveService = saveService;
            }

            if (!IsSaveServiceUsable(saveService))
                return;

            saveService.Register(this);
            _registeredSaveService = saveService;
            _saveRegistered = true;
        }

        private void TryUnregisterSaveParticipant()
        {
            if (!_saveRegistered && _registeredSaveService == null)
                return;

            ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _cachedSaveService;
            if (saveService != null)
                saveService.Unregister(this);

            _registeredSaveService = null;
            _saveRegistered = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_runtimeOwnerAborted || !_serviceRegistered || _registeredHotSwapListener || !Application.isPlaying)
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

        private bool TryRegisterService()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (_serviceRegistered)
                return true;

            if (!Application.isPlaying)
                return true;

            if (TryAbortForUsableExistingRuntime())
                return false;

            AudioLogSystem registeredAudioLogs = GlobalRegistry.AudioLogs;
            if (!ReferenceEquals(registeredAudioLogs, null) && !ReferenceEquals(registeredAudioLogs, this))
            {
                if (IsAudioLogSystemUsable(registeredAudioLogs))
                {
                    AbortDuplicateRuntimeOwner();
                    return false;
                }

                GlobalRegistry.UnregisterAudioLogRuntime(registeredAudioLogs);
            }

            GlobalRegistry.RegisterAudioLogRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.AudioLogs, this);
            if (!_serviceRegistered)
            {
                AbortDuplicateRuntimeOwner();
                return false;
            }

            return true;
        }

        private static bool IsAudioLogSystemUsable(AudioLogSystem audioLogSystem)
        {
            return audioLogSystem != null && audioLogSystem.IsAudioLogRuntimeReady;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            if (_runtimeOwnerAborted)
                return true;

            if (!Application.isPlaying)
                return false;

            AudioLogSystem registeredAudioLogs = GlobalRegistry.AudioLogs;
            if (ReferenceEquals(registeredAudioLogs, this))
                return false;

            if (IsAudioLogSystemUsable(registeredAudioLogs))
            {
                AbortDuplicateRuntimeOwner();
                return true;
            }

            if (registeredAudioLogs != null)
                GlobalRegistry.UnregisterAudioLogRuntime(registeredAudioLogs);

            return false;
        }

        private void AbortDuplicateRuntimeOwner()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregisterSaveParticipant();
            TryUnregister();
            TryUnregisterLateFrame();
            TryUnregisterHotSwapListener();
            TryUnregisterService();

            ClearTransientPlaybackState();
            ReleaseVaultBuffers(_dataVault);
            _dataVault = null;
            _cachedAudioService = null;
            _cachedNarrativeAudioSink = null;
            _cachedPlayerContext = null;
            _cachedSaveService = null;
            _serviceRegistered = false;
            _registered = false;
            _lateFrameRegistered = false;
            _registeredHotSwapListener = false;
            _saveRegistered = false;
            _runtimeOwnerAborted = true;
            enabled = false;
            Destroy(gameObject);
        }

        private void TryUnregisterService()
        {
            if (_runtimeOwnerAborted || !_serviceRegistered)
                return;

            if (ReferenceEquals(GlobalRegistry.AudioLogs, this))
                GlobalRegistry.UnregisterAudioLogRuntime(this);

            _serviceRegistered = false;
        }

        private void RebindDataVaultCold(IDataVault nextVault, bool ensureBuffers)
        {
            if (_runtimeOwnerAborted)
                return;

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
            if (_runtimeOwnerAborted || (Application.isPlaying && !_serviceRegistered))
                return;

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
            if (_runtimeOwnerAborted || (Application.isPlaying && !_serviceRegistered))
                return;

            ClearDiscoveryNotificationDiagnostics();
            ClearTransientPlaybackState();
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
            if (_runtimeOwnerAborted)
                return false;

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
            if (_runtimeOwnerAborted)
                return;

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

