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
using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Modding;
using Hecton8.SaveSystem;
using Hecton8.UI;
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
        private readonly HashSet<string> _discoveredLogs = new HashSet<string>(256);
        // COLD ALLOC: Dictionary<string,AudioLogData>[32] - authored log lookup by stable logId - owner: AudioLogSystem
        private readonly Dictionary<string, AudioLogData> _logLookup = new Dictionary<string, AudioLogData>(32);
        private const int PlaybackQueueCapacity = 16;
        private const string AudioLogFolder = "Assets/_Project/Data/Lore/AudioLogs";
        private readonly AudioLogData[] _queuedLogs = new AudioLogData[PlaybackQueueCapacity]; // COLD ALLOC: AudioLogData[16] - non-overlap narrative playback queue - owner: AudioLogSystem
        private static readonly uint _QueueFullWarningHash = unchecked((uint)LocHash.Compute("AudioLogSystem.QueueFull"));
        private static readonly uint _LookupMissWarningHash = unchecked((uint)LocHash.Compute("AudioLogSystem.LookupMiss"));
        private static readonly uint _NarrativeQueueContextHash = unchecked((uint)LocHash.Compute("NarrativeQueue"));

        private AudioLogData _currentLog;
        private uint _currentLogHash;
        private float _playbackTimer;
        private int _queueHead;
        private int _queueTail;
        private int _queueCount;
        private float _atmosphericWarningTimer;
        private bool _isPlaying;
        private bool _atmosphericWarningActive;
        private bool _registered;
        private bool _serviceRegistered;

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
        public int DiscoveredCount => _discoveredLogs.Count;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

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
            string completedId = completedLog.logId;
            uint completedHash = _currentLogHash;
            _isPlaying = false;
            _currentLog = null;
            _currentLogHash = 0u;
            _playbackTimer = 0f;

            AudioLogEvents.RaisePlaybackCompleted(completedHash, completedLog);

            LogPlaybackCompleted(completedId);
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
            if (data == null || string.IsNullOrEmpty(data.logId))
                return;

            if (_discoveredLogs.Contains(data.logId))
                return;

            string displayTitle = data.DisplayTitleOrFallback;
            uint discoveredHash = LoreDatabaseManager.ComputeLoreHash(data.SafeLogId);
            _discoveredLogs.Add(data.logId);
            if (discoveredHash != 0u)
                AudioLogEvents.RaiseLogDiscovered(discoveredHash, data);
            LocalizationManager localization = Hecton8.Core.GlobalRegistry.Localization;
            NotificationEvents.PushInfo(localization != null
                ? localization.GetFormatted(LocalizationKeys.AUDIOLOG_DISCOVERED, displayTitle)
                : "LOG DISCOVERED: " + displayTitle);

            // Также регистрируем в NarrativeDirector
            NarrativeEvents.RaiseDiscoveryMade(data.logId);
            NarrativeEvents.RaiseAudioLogFound(data.logId);

            LogDiscovered(data.logId, displayTitle);
        }

        /// <summary>
        /// Воспроизвести аудиодневник. Если уже играет — останавливает предыдущий.
        /// </summary>
        public void PlayLog(AudioLogData data)
        {
            if (data == null)
                return;

            // Обнаруживаем если ещё не обнаружен
            DiscoverLog(data);

            if (_isPlaying || _atmosphericWarningActive)
            {
                EnqueuePlayback(data);
                return;
            }

            // Воспроизводим через SpatialAudioManager
            AudioClip playbackClip = data.ResolvedAudioClip;
            if (playbackClip != null)
            {
                if (Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audioManager)
                {
                    audioManager.PlayStatic2D(playbackClip, playbackVolume);
                }
            }

            _currentLog = data;
            _currentLogHash = LoreDatabaseManager.ComputeLoreHash(data.SafeLogId);
            _playbackTimer = data.Duration;
            _isPlaying = true;

            AudioLogEvents.RaisePlaybackStarted(_currentLogHash, _playbackTimer, data);

            LogPlaying(data.logId, data.Duration);
        }

        public bool TryPlayLogById(string logId)
        {
            if (string.IsNullOrWhiteSpace(logId))
                return false;

            if (_logLookup.Count == 0 && allLogs != null && allLogs.Length > 0)
                BuildLogLookup();

            if (!_logLookup.TryGetValue(logId, out AudioLogData data) || data == null)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(_LookupMissWarningHash, _NarrativeQueueContextHash, 1f);
                return false;
            }

            PlayLog(data);
            return true;
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

            AudioLogEvents.RaisePlaybackStopped(stoppedHash, stoppedLog);
        }

        /// <summary>
        /// Проверить, обнаружен ли лог.
        /// </summary>
        public bool IsDiscovered(string logId)
        {
            return !string.IsNullOrEmpty(logId) && _discoveredLogs.Contains(logId);
        }

        /// <summary>
        /// Получить все обнаруженные logId (для PDA архива).
        /// Возвращает enumerator без аллокации.
        /// </summary>
        public HashSet<string>.Enumerator GetDiscoveredEnumerator()
        {
            return _discoveredLogs.GetEnumerator();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private void BuildLogLookup()
        {
            _logLookup.Clear();
            if (allLogs == null)
                return;

            for (int i = 0; i < allLogs.Length; i++)
            {
                AudioLogData data = allLogs[i];
                if (data == null || string.IsNullOrWhiteSpace(data.logId))
                    continue;

                if (!_logLookup.ContainsKey(data.logId))
                    _logLookup.Add(data.logId, data);
            }
        }

        private void EnqueuePlayback(AudioLogData data)
        {
            if (data == null)
                return;

            if (ReferenceEquals(_currentLog, data) || IsPlaybackQueued(data))
                return;

            if (_queueCount >= PlaybackQueueCapacity)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(_QueueFullWarningHash, _NarrativeQueueContextHash, _queueCount);
                return;
            }

            _queuedLogs[_queueTail] = data;
            _queueTail = (_queueTail + 1) % PlaybackQueueCapacity;
            _queueCount++;
        }

        private bool IsPlaybackQueued(AudioLogData data)
        {
            int index = _queueHead;
            for (int i = 0; i < _queueCount; i++)
            {
                if (ReferenceEquals(_queuedLogs[index], data))
                    return true;

                index = (index + 1) % PlaybackQueueCapacity;
            }

            return false;
        }

        private void TryStartNextQueuedLog()
        {
            if (_isPlaying || _atmosphericWarningActive || _queueCount <= 0)
                return;

            AudioLogData next = _queuedLogs[_queueHead];
            _queuedLogs[_queueHead] = null;
            _queueHead = (_queueHead + 1) % PlaybackQueueCapacity;
            _queueCount--;

            if (next != null)
                PlayLog(next);
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
            for (int i = 0; i < _queuedLogs.Length; i++)
                _queuedLogs[i] = null;

            _queueHead = 0;
            _queueTail = 0;
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
        private static void LogPlaybackCompleted(string completedId)
        {
            Debug.Log($"[AudioLog] Playback completed: {completedId}");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogDiscovered(string logId, string displayTitle)
        {
            Debug.Log($"[AudioLog] Discovered: {logId} ({displayTitle})");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogPlaying(string logId, float duration)
        {
            Debug.Log($"[AudioLog] Playing: {logId} ({duration:F1}s)");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogLoadedCount(int discoveredCount)
        {
            Debug.Log($"[AudioLog] Loaded {discoveredCount} discovered logs.");
        }

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        public void PopulateSaveData(SaveData data)
        {
            if (data == null) return;

            data.audioLogDiscoveredIds.Clear();
            int count = 0;

            foreach (string logId in _discoveredLogs)
            {
                if (count >= maxSavedLogs) break;
                data.audioLogDiscoveredIds.Add(logId);
                count++;
            }
        }

        public void LoadFromSaveData(SaveData data)
        {
            _discoveredLogs.Clear();

            if (data?.audioLogDiscoveredIds == null)
                return;

            foreach (string logId in data.audioLogDiscoveredIds)
            {
                if (!string.IsNullOrEmpty(logId))
                    _discoveredLogs.Add(logId);
            }

            LogLoadedCount(_discoveredLogs.Count);
        }
    }
}

