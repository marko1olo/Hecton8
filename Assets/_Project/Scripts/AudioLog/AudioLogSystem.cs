// ============================================================================
// HECTON-8 — AudioLogSystem.cs
// Singleton-menedzher audiodnevnikov kolonii.
//
// ROL:
//   • Hranit reestr vseh obnaruzhennyh logov (ISaveable).
//   • Upravlyaet vosproizvedeniem cherez SpatialAudioManager.
//   • Publikuet sobytiya dlya PDA-arhiva i HUD-subtitrov.
//   • Integriruetsya s NarrativeEvents (discovery → log unlock).
//
// ZERO GC:
//   • HashSet<uint> dlya O(1) proverki obnaruzhennyh logov.
//   • ISlowTickable dlya proverki zaversheniya vosproizvedeniya.
//   • Nikakih new/LINQ/string concat v hot path.
//
// SOHRANENIE:
//   • LoadPriority 6 — posle NarrativeDirector (5).
//   • Sohranyaet spisok obnaruzhennyh logId v SaveData.
// ============================================================================

using System;
using System.Collections.Generic;
using Conditional = System.Diagnostics.ConditionalAttribute;
using Hecton.Localization;
using Hecton8.AtlasSignal;
using Hecton8.Audio;
using Hecton8.Core;
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
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-140)]
    public sealed class AudioLogSystem : MonoBehaviour, ISaveable, ISlowTickable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Settings ────────────────────────────────")]
        [Tooltip("Gromkost vosproizvedeniya audiodnevnikov.")]
        [SerializeField, Range(0f, 1f)] private float playbackVolume = 0.85f;

        [Tooltip("Maksimalnoe kolichestvo sohranyaemyh logId.")]
        [SerializeField] private int maxSavedLogs = 256;

        [Tooltip("Authored audio log catalog used by narrative systems that unlock logs without a pickup object.")]
        [SerializeField] private AudioLogData[] allLogs = Array.Empty<AudioLogData>();

        // ══════════════════════════════════════════════════════════
        //  SERVICE AUTHORITY
        // ══════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private const int PlaybackQueueCapacity = 16;
        private const int EncryptedFragmentStateCapacity = 32;
        private const int ResolvedLogHashCapacity = 512;
        private const uint EncryptedLogCompleteMask = 0xFu;
        // COLD ALLOC: HashSet<uint>[512] — discovered audio-log hashes per save — owner: AudioLogSystem
        private const string NativeMemoryOwner = nameof(AudioLogSystem);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;
        private readonly HashSet<uint> _discoveredLogHashes = new HashSet<uint>(ResolvedLogHashCapacity);
        // COLD ALLOC: Dictionary<uint,AudioLogData>[512] — resolved log lookup by stable log hash — owner: AudioLogSystem
        private readonly Dictionary<uint, AudioLogData> _logLookupByHash = new Dictionary<uint, AudioLogData>(ResolvedLogHashCapacity);
        // COLD ALLOC: Dictionary<AudioLogData,uint>[512] — resolved log reverse lookup by asset reference — owner: AudioLogSystem
        private readonly Dictionary<AudioLogData, uint> _hashByLog = new Dictionary<AudioLogData, uint>(ResolvedLogHashCapacity);
        // COLD ALLOC: Dictionary<uint,uint>[512] — audio log discovery notification hash lookup — owner: AudioLogSystem
        private readonly Dictionary<uint, uint> _discoveryNotificationHashByLogHash = new Dictionary<uint, uint>(ResolvedLogHashCapacity);
        // COLD ALLOC: uint[16] — fixed narrative queue dedupe slots — owner: AudioLogSystem
        private readonly uint[] _queuedLogHashDedup = new uint[PlaybackQueueCapacity];
        // COLD ALLOC: uint[512] — flat resolved audio-log catalog for deterministic save iteration — owner: AudioLogSystem
        private readonly uint[] _resolvedLogHashes = new uint[ResolvedLogHashCapacity];
        private const string AudioLogFolder = "Assets/_Project/Data/Lore/AudioLogs";
        private NativeQueue<uint> _queuedLogHashes;
        private NativeArray<uint> _encryptedFragmentLogHashes;
        private NativeArray<uint> _encryptedFragmentRecoveredBits;
        private int _encryptedFragmentStateCount;
        private int _resolvedLogHashCount;
        private static readonly uint _QueueFullWarningHash = unchecked((uint)LocHash.Compute("AudioLogSystem.QueueFull"));
        private static readonly uint _LookupMissWarningHash = unchecked((uint)LocHash.Compute("AudioLogSystem.LookupMiss"));
        private static readonly uint _ResolvedLogCatalogFullWarningHash = unchecked((uint)LocHash.Compute("AudioLogSystem.ResolvedLogCatalogFull"));
        private static readonly uint _EncryptedFragmentStateFullWarningHash = unchecked((uint)LocHash.Compute("AudioLogSystem.EncryptedFragmentStateFull"));
        private static readonly uint _EncryptedVoiceRouteMissingWarningHash = unchecked((uint)LocHash.Compute("AudioLogSystem.EncryptedVoiceRouteMissing"));
        private static readonly uint _NarrativeQueueContextHash = unchecked((uint)LocHash.Compute("NarrativeQueue"));
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
        private bool _serviceRegistered;
        private bool _queueRegistered;
        private bool _currentPlaybackBitCrushed;
        private bool _resolvedLogCatalogFullTelemetryArmed = true;
        private bool _encryptedVoiceRouteMissingTelemetryArmed = true;

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        public int SavePriority => 6;
        public int LoadPriority => 6;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public bool IsPlaying => _isPlaying;
        public bool IsNarrativeQueueBlocked => _isPlaying || _atmosphericWarningActive;
        public AudioLogData CurrentLog => _currentLog;
        public int DiscoveredCount => _discoveredLogHashes.Count;
        public bool CurrentPlaybackBitCrushed => _currentPlaybackBitCrushed;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            EnsurePlaybackQueue();
            EnsureEncryptedFragmentState();
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
            TryRegisterService();
            TryRegister();

            if (Hecton8.Core.GlobalRegistry.SaveRuntime != null)
                Hecton8.Core.GlobalRegistry.SaveRuntime.Register(this);
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterService();

            if (Hecton8.Core.GlobalRegistry.SaveRuntime != null)
                Hecton8.Core.GlobalRegistry.SaveRuntime.Unregister(this);

            if (_isPlaying)
            {
                StopPlayback();
            }

            ClearPlaybackQueue();
            ClearAtmosphericWarningBlocker();
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterService();
            DisposePlaybackQueue();
            DisposeEncryptedFragmentState();
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable — proverka zaversheniya vosproizvedeniya
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            TickAtmosphericWarningBlocker();

            if (!_isPlaying || _currentLog == null)
                return;

            _playbackTimer -= 0.5f; // SlowTick ~0.5s

            if (_playbackTimer > 0f)
                return;

            // Vosproizvedenie zaversheno
            AudioLogData completedLog = _currentLog;
            uint completedHash = _currentLogHash;
            _isPlaying = false;
            _currentLog = null;
            _currentLogHash = 0u;
            _playbackTimer = 0f;

            _currentPlaybackBitCrushed = false;
            AudioLogEvents.RaisePlaybackCompleted(completedHash, completedLog);

            LogPlaybackCompleted(completedHash);
            TryStartNextQueuedLog();
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Obnaruzhit log (bez vosproizvedeniya). Vyzyvaetsya iz AudioLogPickup.
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
                AudioLogEvents.RaiseLogDiscovered(discoveredHash, data);
            uint notificationHash = ResolveDiscoveryNotificationHash(discoveredHash);
            if (notificationHash != 0u)
                NotificationEvents.PushRegisteredInfo(notificationHash);

            // Takzhe registriruem v NarrativeDirector
            NarrativeEvents.RaiseDiscoveryMade(discoveredHash);
            NarrativeEvents.RaiseAudioLogFound(discoveredHash);

            LogDiscovered(discoveredHash, data);
        }

        /// <summary>
        /// Vosproizvesti audiodnevnik. Esli uzhe igraet — ostanavlivaet predyduschiy.
        /// </summary>
        public void PlayLog(AudioLogData data)
        {
            if (data == null || !TryResolveLogHash(data, out uint logHash))
                return;

            // Obnaruzhivaem esli esche ne obnaruzhen
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

            // Vosproizvodim cherez SpatialAudioManager
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
            float playbackDuration = math.max(0.5f, data.Duration);
            AudioClip playbackClip = data.ResolvedAudioClip;
            if (playbackClip != null)
            {
                if (Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audioManager)
                {
                    audioManager.PlayStatic2D(playbackClip, playbackVolume);
                }
            }

            _currentLog = data;
            _currentLogHash = logHash;
            _playbackTimer = playbackDuration;
            _isPlaying = true;
            _currentPlaybackBitCrushed = false;

            AudioLogEvents.RaisePlaybackStarted(_currentLogHash, _playbackTimer, data);

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

            bool bitCrushRouteActive = false;
            if (Hecton8.Core.GlobalRegistry.Audio is SpatialAudioManager spatialAudioManager)
            {
                bitCrushRouteActive = spatialAudioManager.TryPlayStatic2DBitCrushed(playbackClip, playbackVolume);
            }
            else if (Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audioManager)
            {
                audioManager.PlayStatic2D(playbackClip, playbackVolume);
            }

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
            float playbackDuration = math.max(0.5f, data.Duration);
            _playbackTimer = playbackDuration;
            _isPlaying = true;
            _currentPlaybackBitCrushed = bitCrushRouteActive;

            AudioLogEvents.RaisePlaybackStarted(_currentLogHash, _playbackTimer, data);
        }

        public void NotifyAtmosphericWarningStarted(float durationSeconds)
        {
            _atmosphericWarningActive = true;
            _atmosphericWarningTimer = math.max(_atmosphericWarningTimer, math.max(0.5f, durationSeconds));
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
            AudioLogEvents.RaisePlaybackStopped(stoppedHash, stoppedLog);
        }

        /// <summary>
        /// Proverit, obnaruzhen li log.
        /// </summary>
        public bool IsDiscovered(string logId)
        {
            return IsDiscovered(ComputeAudioLogHash(logId));
        }

        public bool IsDiscovered(uint logHash)
        {
            return logHash != 0u && _discoveredLogHashes.Contains(logHash);
        }

        /// <summary>
        /// Poluchit vse obnaruzhennye logId (dlya PDA arhiva).
        /// Vozvraschaet enumerator bez allokatsii.
        /// </summary>
        public HashSet<uint>.Enumerator GetDiscoveredHashEnumerator()
        {
            return _discoveredLogHashes.GetEnumerator();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

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

            string displayTitle = data.DisplayTitleOrFallback;
            uint notificationHash = string.IsNullOrWhiteSpace(displayTitle)
                ? ResolveFallbackDiscoveryNotificationHash()
                : NotificationEvents.RegisterMessage("LOG DISCOVERED: " + displayTitle);
            if (notificationHash != 0u)
                _discoveryNotificationHashByLogHash.Add(logHash, notificationHash);
        }

        private uint ResolveFallbackDiscoveryNotificationHash()
        {
            if (_fallbackDiscoveryNotificationHash == 0u ||
                !NotificationEvents.TryResolveMessage(_fallbackDiscoveryNotificationHash, out _))
            {
                _fallbackDiscoveryNotificationHash = NotificationEvents.RegisterMessage("LOG DISCOVERED");
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

            EnsurePlaybackQueue();
            AddQueuedLogHash(logHash);
            _queuedLogHashes.Enqueue(logHash);
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

            EnsurePlaybackQueue();
            if (_queuedLogHashes.TryDequeue(out uint nextHash))
            {
                _queueCount--;
                RemoveQueuedLogHash(nextHash);
                if (_logLookupByHash.TryGetValue(nextHash, out AudioLogData next) && next != null)
                {
                    PlayLogByHash(nextHash, next);
                }
            }
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
            if (_queuedLogHashes.IsCreated)
            {
                while (_queuedLogHashes.TryDequeue(out _))
                {
                }
            }

            _queueCount = 0;
            ClearQueuedLogHashes();
        }

        private void EnsurePlaybackQueue()
        {
            bool createdQueue = false;
            if (!_queuedLogHashes.IsCreated)
            {
                createdQueue = true;
                _queuedLogHashes = new NativeQueue<uint>(Allocator.Persistent); // COLD ALLOC: NativeQueue<uint>[16] — hash-only narrative playback queue — owner: AudioLogSystem
            }

            if (!_queueRegistered)
            {
                NativeMemorySentinel.RegisterNativeQueue(
                    _queuedLogHashes,
                    PlaybackQueueCapacity,
                    NativeMemoryOwner,
                    nameof(_queuedLogHashes),
                NativeMemoryLifetime);
                _queueRegistered = true;
                if (createdQueue)
                    PrewarmPlaybackQueue();
            }
        }

        private void PrewarmPlaybackQueue()
        {
            if (!_queuedLogHashes.IsCreated)
                return;

            for (int i = 0; i < PlaybackQueueCapacity; i++)
                _queuedLogHashes.Enqueue(default);

            while (_queuedLogHashes.TryDequeue(out _))
            {
            }

            _queueCount = 0;
            ClearQueuedLogHashes();
        }

        private void DisposePlaybackQueue()
        {
            if (_queueRegistered)
            {
                NativeMemorySentinel.UnregisterNativeQueue(NativeMemoryOwner, nameof(_queuedLogHashes));
                _queueRegistered = false;
            }

            if (_queuedLogHashes.IsCreated)
                _queuedLogHashes.Dispose();

            _queuedLogHashes = default;
            _queueCount = 0;
            ClearQueuedLogHashes();
        }

        private void EnsureEncryptedFragmentState()
        {
            if (!_encryptedFragmentLogHashes.IsCreated)
            {
                _encryptedFragmentLogHashes = new NativeArray<uint>(
                    EncryptedFragmentStateCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<uint>[32] — encrypted audio-log hash slots — owner: AudioLogSystem
                NativeMemorySentinel.RegisterNativeArray(
                    _encryptedFragmentLogHashes,
                    NativeMemoryOwner,
                    nameof(_encryptedFragmentLogHashes),
                    NativeMemoryLifetime);
            }

            if (!_encryptedFragmentRecoveredBits.IsCreated)
            {
                _encryptedFragmentRecoveredBits = new NativeArray<uint>(
                    EncryptedFragmentStateCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<uint>[32] — encrypted audio-log recovered 4-bit masks — owner: AudioLogSystem
                NativeMemorySentinel.RegisterNativeArray(
                    _encryptedFragmentRecoveredBits,
                    NativeMemoryOwner,
                    nameof(_encryptedFragmentRecoveredBits),
                    NativeMemoryLifetime);
            }
        }

        private void DisposeEncryptedFragmentState()
        {
            if (_encryptedFragmentLogHashes.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_encryptedFragmentLogHashes);
                _encryptedFragmentLogHashes.Dispose();
            }

            if (_encryptedFragmentRecoveredBits.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_encryptedFragmentRecoveredBits);
                _encryptedFragmentRecoveredBits.Dispose();
            }

            _encryptedFragmentLogHashes = default;
            _encryptedFragmentRecoveredBits = default;
            _encryptedFragmentStateCount = 0;
        }

        private void ClearEncryptedFragmentState()
        {
            EnsureEncryptedFragmentState();
            for (int i = 0; i < _encryptedFragmentStateCount; i++)
            {
                _encryptedFragmentLogHashes[i] = 0u;
                _encryptedFragmentRecoveredBits[i] = 0u;
            }

            _encryptedFragmentStateCount = 0;
        }

        private bool TryGetEncryptedFragmentBits(uint logHash, out uint recoveredBits)
        {
            recoveredBits = 0u;
            if (logHash == 0u || !_encryptedFragmentLogHashes.IsCreated || !_encryptedFragmentRecoveredBits.IsCreated)
                return false;

            for (int i = 0; i < _encryptedFragmentStateCount; i++)
            {
                if (_encryptedFragmentLogHashes[i] != logHash)
                    continue;

                recoveredBits = _encryptedFragmentRecoveredBits[i] & EncryptedLogCompleteMask;
                return true;
            }

            return false;
        }

        private bool SetEncryptedFragmentBits(uint logHash, uint recoveredBits)
        {
            if (logHash == 0u)
                return false;

            EnsureEncryptedFragmentState();
            for (int i = 0; i < _encryptedFragmentStateCount; i++)
            {
                if (_encryptedFragmentLogHashes[i] != logHash)
                    continue;

                _encryptedFragmentRecoveredBits[i] = recoveredBits & EncryptedLogCompleteMask;
                return true;
            }

            if (_encryptedFragmentStateCount >= EncryptedFragmentStateCapacity)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _EncryptedFragmentStateFullWarningHash,
                    _NarrativeQueueContextHash,
                    _encryptedFragmentStateCount);
                return false;
            }

            int slot = _encryptedFragmentStateCount++;
            _encryptedFragmentLogHashes[slot] = logHash;
            _encryptedFragmentRecoveredBits[slot] = recoveredBits & EncryptedLogCompleteMask;
            return true;
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

            allLogs = loadedLogs.ToArray();
            EditorUtility.SetDirty(this);
        }
#endif

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Core);
            _registered = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
            _registered = false;
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

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogPlaybackCompleted(uint completedHash)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[AudioLog] Playback completed: 0x{completedHash:X8}");
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogDiscovered(uint logHash, AudioLogData data)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string displayTitle = data != null ? data.DisplayTitleOrFallback : string.Empty;
            Debug.Log($"[AudioLog] Discovered: 0x{logHash:X8} ({displayTitle})");
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogPlaying(uint logHash, float duration)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[AudioLog] Playing: 0x{logHash:X8} ({duration:F1}s)");
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogLoadedCount(int discoveredCount)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[AudioLog] Loaded {discoveredCount} discovered logs.");
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        public void PopulateSaveData(SaveData data)
        {
            if (data == null) return;

            if (data.audioLogDiscoveredIds == null)
                data.audioLogDiscoveredIds = new List<string>(math.max(0, maxSavedLogs)); // COLD ALLOC: List<string>[maxSavedLogs] — fallback discovered audio-log save list — owner: AudioLogSystem
            else
                data.audioLogDiscoveredIds.Clear();
            EnsureSaveEncryptedFragmentArrays(data);
            data.audioLogEncryptedFragmentCount = 0;
            int count = 0;

            for (int i = 0; i < _resolvedLogHashCount; i++)
            {
                if (count >= maxSavedLogs)
                    break;

                uint logHash = _resolvedLogHashes[i];
                if (!_discoveredLogHashes.Contains(logHash) ||
                    !_logLookupByHash.TryGetValue(logHash, out AudioLogData logData) ||
                    logData == null ||
                    string.IsNullOrWhiteSpace(logData.logId))
                {
                    continue;
                }

                data.audioLogDiscoveredIds.Add(logData.logId);
                count++;
            }

            int partialCount = 0;
            for (int i = 0; i < _encryptedFragmentStateCount && partialCount < SaveData.MaxEncryptedAudioLogFragments; i++)
            {
                uint logHash = _encryptedFragmentLogHashes.IsCreated ? _encryptedFragmentLogHashes[i] : 0u;
                uint recoveredBits = _encryptedFragmentRecoveredBits.IsCreated
                    ? _encryptedFragmentRecoveredBits[i] & EncryptedLogCompleteMask
                    : 0u;
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

            int discoveredCount = data != null && data.audioLogDiscoveredIds != null ? data.audioLogDiscoveredIds.Count : 0;
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

