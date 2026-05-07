// ============================================================================
// HECTON-8 — AudioLogSystem.cs
// Singleton-менеджер аудиодневников колонии.
//
// РОЛЬ:
//   • Хранит реестр всех обнаруженных логов (ISaveable).
//   • Управляет воспроизведением через SpatialAudioManager.
//   • Публикует события для PDA-архива и HUD-субтитров.
//   • Интегрируется с NarrativeEvents (discovery → log unlock).
//
// ZERO GC:
//   • HashSet<string> для O(1) проверки обнаруженных логов.
//   • ISlowTickable для проверки завершения воспроизведения.
//   • Никаких new/LINQ/string concat в hot path.
//
// СОХРАНЕНИЕ:
//   • LoadPriority 6 — после NarrativeDirector (5).
//   • Сохраняет список обнаруженных logId в SaveData.
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
        [Tooltip("Громкость воспроизведения аудиодневников.")]
        [SerializeField, Range(0f, 1f)] private float playbackVolume = 0.85f;

        [Tooltip("Максимальное количество сохраняемых logId.")]
        [SerializeField] private int maxSavedLogs = 256;

        [Tooltip("Authored audio log catalog used by narrative systems that unlock logs without a pickup object.")]
        [SerializeField] private AudioLogData[] allLogs = Array.Empty<AudioLogData>();

        // ══════════════════════════════════════════════════════════
        //  SERVICE AUTHORITY
        // ══════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        // COLD ALLOC: 256 entries — max discovered logs per save
        private const string NativeMemoryOwner = nameof(AudioLogSystem);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;
        private readonly HashSet<uint> _discoveredLogHashes = new HashSet<uint>(256);
        // COLD ALLOC: Dictionary<string,AudioLogData>[32] - authored log lookup by stable logId - owner: AudioLogSystem
        private readonly Dictionary<uint, AudioLogData> _logLookupByHash = new Dictionary<uint, AudioLogData>(32);
        private readonly Dictionary<AudioLogData, uint> _hashByLog = new Dictionary<AudioLogData, uint>(32);
        // COLD ALLOC: Dictionary<uint,uint>[32] - encrypted audio-log fragment bit masks by log hash - owner: AudioLogSystem
        private readonly Dictionary<uint, uint> _recoveredEncryptedLogBits = new Dictionary<uint, uint>(32);
        private const int PlaybackQueueCapacity = 16;
        private const uint EncryptedLogCompleteMask = 0xFu;
        private readonly HashSet<uint> _queuedLogHashSet = new HashSet<uint>(PlaybackQueueCapacity);
        private const string AudioLogFolder = "Assets/_Project/Data/Lore/AudioLogs";
        private NativeQueue<uint> _queuedLogHashes;
        private static readonly uint _QueueFullWarningHash = unchecked((uint)LocHash.Compute("AudioLogSystem.QueueFull"));
        private static readonly uint _LookupMissWarningHash = unchecked((uint)LocHash.Compute("AudioLogSystem.LookupMiss"));
        private static readonly uint _NarrativeQueueContextHash = unchecked((uint)LocHash.Compute("NarrativeQueue"));

        private AudioLogData _currentLog;
        private uint _currentLogHash;
        private float _playbackTimer;
        private int _queueCount;
        private float _atmosphericWarningTimer;
        private bool _isPlaying;
        private bool _atmosphericWarningActive;
        private bool _registered;
        private bool _serviceRegistered;
        private bool _queueRegistered;
        private bool _currentPlaybackBitCrushed;

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
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable — проверка завершения воспроизведения
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            TickAtmosphericWarningBlocker();

            if (!_isPlaying || _currentLog == null)
                return;

            _playbackTimer -= 0.5f; // SlowTick ~0.5s

            if (_playbackTimer > 0f)
                return;

            // Воспроизведение завершено
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
        /// Обнаружить лог (без воспроизведения). Вызывается из AudioLogPickup.
        /// </summary>
        public void DiscoverLog(AudioLogData data)
        {
            if (data == null || !TryResolveLogHash(data, out uint discoveredHash))
                return;

            if (data.IsFragmentedEncrypted && !IsEncryptedLogFullyRecovered(discoveredHash))
                return;

            if (_discoveredLogHashes.Contains(discoveredHash))
                return;

            string displayTitle = data.DisplayTitleOrFallback;
            _discoveredLogHashes.Add(discoveredHash);
            if (discoveredHash != 0u)
                AudioLogEvents.RaiseLogDiscovered(discoveredHash, data);
            LocalizationManager localization = Hecton8.Core.GlobalRegistry.Localization;
            NotificationEvents.PushInfo(localization != null
                ? localization.GetFormatted(LocalizationKeys.AUDIOLOG_DISCOVERED, displayTitle)
                : "LOG DISCOVERED: " + displayTitle);

            // Также регистрируем в NarrativeDirector
            NarrativeEvents.RaiseDiscoveryMade(data.logId);
            NarrativeEvents.RaiseAudioLogFound(data.logId);

            LogDiscovered(discoveredHash, displayTitle);
        }

        /// <summary>
        /// Воспроизвести аудиодневник. Если уже играет — останавливает предыдущий.
        /// </summary>
        public void PlayLog(AudioLogData data)
        {
            if (data == null || !TryResolveLogHash(data, out uint logHash))
                return;

            // Обнаруживаем если ещё не обнаружен
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

            // Воспроизводим через SpatialAudioManager
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

            return logHash != 0u && _recoveredEncryptedLogBits.TryGetValue(logHash, out uint recoveredBits)
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

            _recoveredEncryptedLogBits[logHash] = recoveredBits;

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
            _playbackTimer = data.Duration;
            _isPlaying = true;
            _currentPlaybackBitCrushed = false;

            AudioLogEvents.RaisePlaybackStarted(_currentLogHash, _playbackTimer, data);

            LogPlaying(logHash, data.Duration);
        }

        private bool IsEncryptedLogFullyRecovered(uint logHash)
        {
            return (GetRecoveredEncryptedBits(logHash) & EncryptedLogCompleteMask) == EncryptedLogCompleteMask;
        }

        private void PlayEncryptedPartialPreview(uint logHash, AudioLogData data)
        {
            if (data == null || _isPlaying || _atmosphericWarningActive)
                return;

            AudioClip playbackClip = data.ResolvedAudioClip;
            if (playbackClip == null)
                return;

            if (Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audioManager)
                audioManager.PlayStatic2D(playbackClip, playbackVolume);

            _currentLog = data;
            _currentLogHash = logHash;
            _playbackTimer = data.Duration;
            _isPlaying = true;
            _currentPlaybackBitCrushed = true;

            AudioLogEvents.RaisePlaybackStarted(_currentLogHash, _playbackTimer, data);
        }

        public void NotifyAtmosphericWarningStarted(float durationSeconds)
        {
            _atmosphericWarningActive = true;
            _atmosphericWarningTimer = Mathf.Max(_atmosphericWarningTimer, Mathf.Max(0.5f, durationSeconds));
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
        /// Остановить текущее воспроизведение.
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
        /// Проверить, обнаружен ли лог.
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
        /// Получить все обнаруженные logId (для PDA архива).
        /// Возвращает enumerator без аллокации.
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
            if (allLogs == null)
                return;

            for (int i = 0; i < allLogs.Length; i++)
            {
                AudioLogData data = allLogs[i];
                uint logHash = ComputeAudioLogHash(data != null ? data.logId : null);
                if (data == null || logHash == 0u)
                    continue;

                if (!_logLookupByHash.ContainsKey(logHash))
                    _logLookupByHash.Add(logHash, data);

                if (!_hashByLog.ContainsKey(data))
                    _hashByLog.Add(data, logHash);
            }
        }

        private bool TryResolveLogHash(AudioLogData data, out uint logHash)
        {
            logHash = 0u;
            if (data == null)
                return false;

            if (_hashByLog.TryGetValue(data, out logHash) && logHash != 0u)
                return true;

            logHash = ComputeAudioLogHash(data.logId);
            if (logHash == 0u)
                return false;

            _hashByLog[data] = logHash;
            if (!_logLookupByHash.ContainsKey(logHash))
                _logLookupByHash.Add(logHash, data);

            return true;
        }

        private static uint ComputeAudioLogHash(string logId)
        {
            return QuestFlagHashKernel.ComputeStableHash(logId);
        }

        private void EnqueuePlayback(uint logHash)
        {
            if (logHash == 0u || _currentLogHash == logHash || IsPlaybackQueued(logHash))
                return;

            if (_queueCount >= PlaybackQueueCapacity)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(_QueueFullWarningHash, _NarrativeQueueContextHash, _queueCount);
                return;
            }

            EnsurePlaybackQueue();
            _queuedLogHashSet.Add(logHash);
            _queuedLogHashes.Enqueue(logHash);
            _queueCount++;
        }

        private bool IsPlaybackQueued(uint logHash)
        {
            return logHash != 0u && _queuedLogHashSet.Contains(logHash);
        }

        private void TryStartNextQueuedLog()
        {
            if (_isPlaying || _atmosphericWarningActive || _queueCount <= 0)
                return;

            EnsurePlaybackQueue();
            if (_queuedLogHashes.TryDequeue(out uint nextHash))
            {
                _queueCount--;
                _queuedLogHashSet.Remove(nextHash);
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
            _queuedLogHashSet.Clear();
        }

        private void EnsurePlaybackQueue()
        {
            if (!_queuedLogHashes.IsCreated)
            {
                _queuedLogHashes = new NativeQueue<uint>(Allocator.Persistent); // COLD ALLOC: NativeQueue<uint>[16] - hash-only narrative playback queue - owner: AudioLogSystem
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
            }
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
        }

#if UNITY_EDITOR
        private void TryAutoPopulateAudioLogCatalog()
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioLogData", new[] { AudioLogFolder });
            if (guids == null || guids.Length == 0)
                return;

            List<AudioLogData> loadedLogs = new List<AudioLogData>(guids.Length); // COLD ALLOC: List<AudioLogData>[guids.Length] - editor-time log catalog bootstrap - owner: AudioLogSystem
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
        private static void LogDiscovered(uint logHash, string displayTitle)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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

            data.audioLogDiscoveredIds.Clear();
            int count = 0;

            if (allLogs == null)
                return;

            for (int i = 0; i < allLogs.Length; i++)
            {
                if (count >= maxSavedLogs)
                    break;

                AudioLogData logData = allLogs[i];
                if (logData == null || !TryResolveLogHash(logData, out uint logHash) || !_discoveredLogHashes.Contains(logHash))
                    continue;

                data.audioLogDiscoveredIds.Add(logData.logId);
                count++;
            }
        }

        public void LoadFromSaveData(SaveData data)
        {
            _discoveredLogHashes.Clear();

            if (data?.audioLogDiscoveredIds == null)
                return;

            for (int i = 0; i < data.audioLogDiscoveredIds.Count; i++)
            {
                uint logHash = ComputeAudioLogHash(data.audioLogDiscoveredIds[i]);
                if (logHash != 0u)
                    _discoveredLogHashes.Add(logHash);
            }

            LogLoadedCount(_discoveredLogHashes.Count);
        }
    }
}

