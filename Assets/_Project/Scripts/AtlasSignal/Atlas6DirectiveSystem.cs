// ============================================================================
// HECTON-8 — Atlas6DirectiveSystem.cs
// Система директив Атлас-6 и их нарушений.
//
// ЛОР (лор3 Блок В):
//   Оригинальные директивы (приоритет по убыванию):
//   1. Сохранить миссию «Посев»
//   2. Обеспечить выживание человеческой колонии
//   3. Изучать и адаптироваться к среде
//   4. Поддерживать связь с Землёй
//
//   Что пошло не так:
//   • Катастрофа → потеря связи → директива #4 невыполнима
//   • Колония уничтожена → директива #2 невыполнима
//   • Остаётся #1 и #3
//
//   Новая логика:
//   «Люди мертвы = экосистема повреждена»
//   «Решение: воссоздать "людей" из доступных материалов»
//   → Биомеханические дроны = попытка «воскресить» колонию
//   → Игрок = аномалия: живой человек, но не из оригинальной колонии
//   → Статус: «Неопознанный биологический агент. Угроза стабильности»
//
// АРХИТЕКТУРА:
//   • Отслеживает статус игрока с точки зрения Атлас-6.
//   • Публикует события при изменении статуса.
//   • Интегрируется с HectonDirectorAI (tension при угрозе).
//   • ISaveable: сохраняет статус и историю взаимодействий.
// ============================================================================

using System;
using Conditional = System.Diagnostics.ConditionalAttribute;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Hecton.Localization;
using UnityEngine;

namespace Hecton8.AtlasSignal
{
    /// <summary>
    /// Статус игрока с точки зрения Атлас-6.
    /// </summary>
    public enum Atlas6PlayerStatus
    {
        Unknown         = 0,   // Не обнаружен
        Detected        = 1,   // Обнаружен — анализ
        Neutral         = 2,   // Нейтральный — не угроза
        Threat          = 3,   // Угроза стабильности экосистемы
        Collaborator    = 4,   // Сотрудничество (торговля)
        Anomaly         = 5    // Аномалия — живой человек вне колонии
    }

    public static class Atlas6Events
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            OnPlayerStatusChanged = null;
            OnDirectiveConflict = null;
            OnBarterAccepted = null;
        }

        /// <summary>Статус игрока изменился.</summary>
        public static event Action<Atlas6PlayerStatus> OnPlayerStatusChanged;

        /// <summary>Конфликт директив — Атлас-6 не может выполнить приказ.</summary>
        public static event Action<string> OnDirectiveConflict;

        /// <summary>Бартер принят — Атлас-6 получил ресурсы.</summary>
        public static event Action<int> OnBarterAccepted;

        public static void RaisePlayerStatusChanged(Atlas6PlayerStatus status)
            => OnPlayerStatusChanged?.Invoke(status);

        public static void RaiseDirectiveConflict(string conflictId)
        {
            if (!string.IsNullOrEmpty(conflictId))
                OnDirectiveConflict?.Invoke(conflictId);
        }

        public static void RaiseBarterAccepted(int transactionCount)
            => OnBarterAccepted?.Invoke(transactionCount);
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-80)]
    public sealed class Atlas6DirectiveSystem : MonoBehaviour, ISaveable, ISlowTickable
    {
        private const int MinimumRevealStageForDirectiveIdentity = 3;
        private const string SignalIdentityDiscoveryId = "atlas6_signal_identified";
        private const string SignalFullyDecodedDiscoveryId = "atlas6_signal_fully_decoded";
        private const string TerminalSectorDiscoveryId = "atlas6_terminal_sector3";
        private const string CoreReachedDiscoveryId = "atlas6_core_reached";
        private const string CoreDataAccessedDiscoveryId = "atlas6_core_data_accessed";

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Thresholds ──────────────────────────────")]
        [Tooltip("Количество бартер-транзакций для перехода в Collaborator.")]
        [SerializeField] private int collaboratorThreshold = 5;

        [Tooltip("Расстояние обнаружения игрока дронами (метры). Зарезервировано для FaunaDirector.")]
#pragma warning disable CS0414
        [SerializeField] private float detectionRange = 200f;
#pragma warning restore CS0414

        [Tooltip("Расстояние до ядра для перехода в Anomaly статус.")]
        [SerializeField] private float anomalyRange = 500f;

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        public static Atlas6DirectiveSystem Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Instance = null;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private Atlas6PlayerStatus _playerStatus = Atlas6PlayerStatus.Unknown;
        private int  _barterTransactionCount;
        private bool _directiveConflictTriggered;
        private bool _registered;
        private Transform _playerTransform;

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        public int SavePriority => 11;
        public int LoadPriority => 11;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public Atlas6PlayerStatus PlayerStatus => _playerStatus;
        public int BarterTransactionCount => _barterTransactionCount;

        /// <summary>
        /// Уровень доверия Атлас-6 к игроку [0..1].
        /// Растёт с торговлей, падает при угрозе.
        /// </summary>
        public float TrustLevel
        {
            get
            {
                return _playerStatus switch
                {
                    Atlas6PlayerStatus.Unknown      => 0f,
                    Atlas6PlayerStatus.Detected     => 0.1f,
                    Atlas6PlayerStatus.Neutral      => 0.3f,
                    Atlas6PlayerStatus.Collaborator => Mathf.Min(1f, _barterTransactionCount / (float)collaboratorThreshold),
                    Atlas6PlayerStatus.Anomaly      => 0.5f,
                    Atlas6PlayerStatus.Threat       => 0f,
                    _                               => 0f
                };
            }
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            TryRegister();

            if (SaveManager.Instance != null)
                SaveManager.Instance.Register(this);

            NarrativeEvents.OnDiscoveryMade += HandleDiscovery;
            Atlas6Events.OnBarterAccepted   += HandleBarterAccepted;
            ResolvePlayer();
        }

        private void OnDisable()
        {
            TryUnregister();

            if (SaveManager.Instance != null)
                SaveManager.Instance.Unregister(this);

            NarrativeEvents.OnDiscoveryMade    -= HandleDiscovery;
            Atlas6Events.OnBarterAccepted      -= HandleBarterAccepted;
        }

        private void OnDestroy()
        {
            TryUnregister();

            if (Instance == this)
                Instance = null;
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            if (_playerTransform == null)
            {
                ResolvePlayer();
                return;
            }

            AtlasSignalSystem signal = AtlasSignalSystem.Instance;
            if (signal == null) return;
            if (!signal.IsDetected) return;

            float distToCore = Vector3.Distance(_playerTransform.position, signal.AtlasCorePosition);

            // Переход в Anomaly при приближении к ядру
            if (distToCore < anomalyRange &&
                _playerStatus != Atlas6PlayerStatus.Anomaly &&
                _playerStatus != Atlas6PlayerStatus.Threat)
            {
                SetStatus(Atlas6PlayerStatus.Anomaly);
                NotificationEvents.PushWarning(ResolveLocalized(
                    LocalizationKeys.ATLAS6_ANOMALY_DETECTED,
                    "ATLAS-6: UNIDENTIFIED BIOLOGICAL AGENT DETECTED. ANALYSIS..."));
            }

            // Конфликт директив — обнаружен живой человек
            if (!_directiveConflictTriggered &&
                _playerStatus >= Atlas6PlayerStatus.Detected)
            {
                _directiveConflictTriggered = true;
                Atlas6Events.RaiseDirectiveConflict("directive_2_impossible_colony_dead");

                LogDirectiveConflict();
            }
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

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>Зарегистрировать бартер-транзакцию.</summary>
        public void RegisterBarterTransaction()
        {
            _barterTransactionCount++;
            Atlas6Events.RaiseBarterAccepted(_barterTransactionCount);

            // Переход в Collaborator
            if (_barterTransactionCount >= collaboratorThreshold &&
                _playerStatus != Atlas6PlayerStatus.Collaborator &&
                _playerStatus != Atlas6PlayerStatus.Threat)
            {
                SetStatus(Atlas6PlayerStatus.Collaborator);
                NotificationEvents.PushInfo(ResolveLocalized(
                    LocalizationKeys.ATLAS6_COLLABORATOR_STATUS,
                    "ATLAS-6: UTILITARIAN CALCULATION - EXCHANGE EFFICIENT. STATUS: COLLABORATOR."));
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private void SetStatus(Atlas6PlayerStatus newStatus)
        {
            if (newStatus == _playerStatus) return;
            _playerStatus = newStatus;
            Atlas6Events.RaisePlayerStatusChanged(newStatus);

            LogPlayerStatus(newStatus);
        }

        private void HandleDiscovery(string discoveryId)
        {
            // Обнаружение терминала Атлас-6 → Detected
            if (CanAdoptAtlasStatusFromDiscovery(discoveryId))
                SetStatus(Atlas6PlayerStatus.Detected);
        }

        private void HandleBarterAccepted(int count)
        {
            // Первая торговля → Neutral
            if (_playerStatus == Atlas6PlayerStatus.Detected ||
                _playerStatus == Atlas6PlayerStatus.Unknown)
                SetStatus(Atlas6PlayerStatus.Neutral);
        }

        private bool CanAdoptAtlasStatusFromDiscovery(string discoveryId)
        {
            if (_playerStatus != Atlas6PlayerStatus.Unknown)
                return false;

            if (!IsDirectiveIdentityDiscovery(discoveryId))
                return false;

            AtlasSignalSystem signal = AtlasSignalSystem.Instance;
            if (signal != null)
                return signal.CurrentRevealStage >= MinimumRevealStageForDirectiveIdentity;

            FirstHourDirector firstHourDirector = FirstHourDirector.Instance;
            if (firstHourDirector != null)
                return firstHourDirector.IsMilestoneComplete(FirstHourMilestone.HumCloser);

            return true;
        }

        private static bool IsDirectiveIdentityDiscovery(string discoveryId)
        {
            return discoveryId == SignalIdentityDiscoveryId ||
                   discoveryId == SignalFullyDecodedDiscoveryId ||
                   discoveryId == TerminalSectorDiscoveryId ||
                   discoveryId == CoreReachedDiscoveryId ||
                   discoveryId == CoreDataAccessedDiscoveryId;
        }

        private void ResolvePlayer()
        {
            SceneBootstrap.TryGetCurrentPlayerTransform(out _playerTransform);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogDirectiveConflict()
        {
            Debug.Log("[Atlas6] Directive conflict: Directive #2 (protect colony) impossible; colony dead.");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogPlayerStatus(Atlas6PlayerStatus newStatus)
        {
            Debug.Log($"[Atlas6] Player status: {newStatus}");
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            return manager != null ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback) : fallback;
        }

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        public void PopulateSaveData(SaveData data)
        {
            if (data == null) return;
            data.atlas6PlayerStatus = (int)_playerStatus;
            data.atlas6BarterCount  = _barterTransactionCount;
            data.atlas6DirectiveConflictTriggered = _directiveConflictTriggered;
        }

        public void LoadFromSaveData(SaveData data)
        {
            if (data == null) return;
            _playerStatus = (Atlas6PlayerStatus)data.atlas6PlayerStatus;
            _barterTransactionCount = data.atlas6BarterCount;
            _directiveConflictTriggered = data.atlas6DirectiveConflictTriggered;
        }
    }
}
