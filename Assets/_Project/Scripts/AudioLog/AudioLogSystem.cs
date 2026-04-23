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
        private float _playbackTimer;
        private bool _isPlaying;
        private bool _registered;

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
            TryRegister();

            if (SaveManager.Instance != null)
                SaveManager.Instance.Register(this);

            NarrativeEvents.OnDiscoveryMade += HandleNarrativeDiscovery;
        }

        private void OnDisable()
        {
            TryUnregister();

            if (SaveManager.Instance != null)
                SaveManager.Instance.Unregister(this);

            NarrativeEvents.OnDiscoveryMade -= HandleNarrativeDiscovery;

            if (_isPlaying)
                StopPlayback();
        }

        private void OnDestroy()
        {
            TryUnregister();

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
            string completedId = _currentLog.logId;
            _isPlaying = false;
            _currentLog = null;
            _playbackTimer = 0f;

            AudioLogEvents.RaisePlaybackCompleted(completedId);

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
            _discoveredLogs.Add(data.logId);
            AudioLogEvents.RaiseLogDiscovered(data.logId);
            LocalizationManager localization = LocalizationManager.Instance;
            NotificationEvents.PushInfo(localization != null
                ? localization.GetFormatted(LocalizationKeys.AUDIOLOG_DISCOVERED, displayTitle)
                : "LOG DISCOVERED: " + displayTitle);

            // Также регистрируем в NarrativeDirector
            NarrativeEvents.RaiseDiscoveryMade(data.logId);
            HectonEventBus.Publish(new LoreAcquiredEvent(LoreDatabaseManager.ComputeLoreHash(data.logId)));

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
                if (SpatialAudioManager.TryGetInstance(out SpatialAudioManager audioManager))
                {
                    audioManager.PlayStatic2D(playbackClip, playbackVolume);
                }
            }

            _currentLog = data;
            _playbackTimer = data.Duration;
            _isPlaying = true;

            AudioLogEvents.RaisePlaybackStarted(data);

            LogPlaying(data.logId, data.Duration);
        }

        /// <summary>
        /// Остановить текущее воспроизведение.
        /// </summary>
        public void StopPlayback()
        {
            if (!_isPlaying || _currentLog == null)
                return;

            string stoppedId = _currentLog.logId;
            _isPlaying = false;
            _currentLog = null;
            _playbackTimer = 0f;

            AudioLogEvents.RaisePlaybackStopped(stoppedId);
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

        private void HandleNarrativeDiscovery(string discoveryId)
        {
            // NarrativeDirector уже обработал — ничего дополнительного не нужно
        }

        private void TryRegister()
        {
            if (_registered)
                return;

            GameTickManager gameTickManager = GameTickManager.Instance;
            if (gameTickManager == null)
                return;

            gameTickManager.Register(this);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GameTickManager gameTickManager = GameTickManager.Instance;
            if (gameTickManager != null)
                gameTickManager.Unregister(this);

            _registered = false;
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
