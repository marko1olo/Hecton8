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

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        public static AudioLogSystem Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Instance = null;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        // COLD ALLOC: 256 entries — max discovered logs per save
        private readonly HashSet<string> _discoveredLogs = new HashSet<string>(256);

        private AudioLogData _currentLog;
        private uint _currentLogHash;
        private float _playbackTimer;
        private bool _isPlaying;
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
        public AudioLogData CurrentLog => _currentLog;
        public int DiscoveredCount => _discoveredLogs.Count;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

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
                StopPlayback();
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterService();

            if (Instance == this)
                Instance = null;
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable — проверка завершения воспроизведения
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
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
            LoreAcquiredEvent loreAcquiredEvent = new LoreAcquiredEvent(discoveredHash);
            HectonEventBus.Publish(in loreAcquiredEvent);

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

            // Останавливаем текущее воспроизведение
            if (_isPlaying)
                StopPlayback();

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

