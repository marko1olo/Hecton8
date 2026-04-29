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
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Conditional = System.Diagnostics.ConditionalAttribute;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Hecton.Localization;
using Unity.Collections;
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

    public enum Atlas6EventType : byte
    {
        PlayerStatusChanged = 0,
        DirectiveConflict = 1,
        BarterAccepted = 2,
        ScarcityDirectiveIssued = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Atlas6EventPayload
    {
        public int TransactionCount;
        public uint ConflictHash;
        public uint DirectiveQuestHash;
        public uint ResourceHash;
        public ushort EventType;
        public ushort StatusValue;
    }

    public interface IAtlas6EventListener
    {
        void OnAtlas6Event(in Atlas6EventPayload payload);
    }

    public static class Atlas6Events
    {
        // COLD ALLOC: RegistryBucket<IAtlas6EventListener>[4] - Atlas-6 directive listeners drained on dispatcher LateUpdate - owner: Atlas6Events
        private static readonly RegistryBucket<IAtlas6EventListener> _listeners = new RegistryBucket<IAtlas6EventListener>(4);
        // COLD ALLOC: Dictionary<uint,string>[8] - hashed directive conflict IDs for cold-path resolution - owner: Atlas6Events
        private static readonly Dictionary<uint, string> _conflictIdsByHash = new Dictionary<uint, string>(8);
        private static NativeQueue<Atlas6EventPayload> _pendingEvents;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            _listeners.Clear();
            _conflictIdsByHash.Clear();
        }

        /// <summary>Статус игрока изменился.</summary>
        public static void Register(IAtlas6EventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            _listeners.Register(listener);
        }

        /// <summary>Конфликт директив — Атлас-6 не может выполнить приказ.</summary>
        public static void Unregister(IAtlas6EventListener listener)
        {
            if (listener == null)
                return;

            _listeners.Unregister(listener);
        }

        /// <summary>Бартер принят — Атлас-6 получил ресурсы.</summary>
        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated || _listeners.Count <= 0)
            {
                DrainWithoutDispatch();
                return;
            }

            while (_pendingEvents.TryDequeue(out Atlas6EventPayload payload))
            {
                IAtlas6EventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                    rawArray[i].OnAtlas6Event(in payload);
            }
        }

        public static bool TryResolveDirectiveConflict(uint conflictHash, out string conflictId)
        {
            return _conflictIdsByHash.TryGetValue(conflictHash, out conflictId);
        }

        public static void RaisePlayerStatusChanged(Atlas6PlayerStatus status)
        {
            Enqueue(new Atlas6EventPayload
            {
                TransactionCount = 0,
                ConflictHash = 0u,
                DirectiveQuestHash = 0u,
                ResourceHash = 0u,
                EventType = (ushort)Atlas6EventType.PlayerStatusChanged,
                StatusValue = (ushort)status
            });
        }

        public static void RaiseDirectiveConflict(string conflictId)
        {
            uint conflictHash = string.IsNullOrWhiteSpace(conflictId)
                ? 0u
                : unchecked((uint)LocHash.Compute(conflictId));
            if (conflictHash == 0u)
                return;

            if (!_conflictIdsByHash.ContainsKey(conflictHash))
                _conflictIdsByHash.Add(conflictHash, conflictId);

            Enqueue(new Atlas6EventPayload
            {
                TransactionCount = 0,
                ConflictHash = conflictHash,
                DirectiveQuestHash = 0u,
                ResourceHash = 0u,
                EventType = (ushort)Atlas6EventType.DirectiveConflict,
                StatusValue = 0
            });
        }

        public static void RaiseBarterAccepted(int transactionCount)
        {
            Enqueue(new Atlas6EventPayload
            {
                TransactionCount = transactionCount,
                ConflictHash = 0u,
                DirectiveQuestHash = 0u,
                ResourceHash = 0u,
                EventType = (ushort)Atlas6EventType.BarterAccepted,
                StatusValue = 0
            });
        }

        public static void RaiseScarcityDirective(uint questHash, uint resourceHash)
        {
            if (questHash == 0u || resourceHash == 0u)
                return;

            Enqueue(new Atlas6EventPayload
            {
                TransactionCount = 0,
                ConflictHash = 0u,
                DirectiveQuestHash = questHash,
                ResourceHash = resourceHash,
                EventType = (ushort)Atlas6EventType.ScarcityDirectiveIssued,
                StatusValue = 0
            });
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<Atlas6EventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<Atlas6EventPayload>[4] - deferred Atlas-6 directive lane flushed by SystemDispatcher LateUpdate - owner: Atlas6Events
            }
        }

        private static void Enqueue(in Atlas6EventPayload payload)
        {
            EnsureInitialized();
            _pendingEvents.Enqueue(payload);
        }

        private static void DrainWithoutDispatch()
        {
            if (!_pendingEvents.IsCreated)
                return;

            while (_pendingEvents.TryDequeue(out _))
            {
            }
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-80)]
    public sealed class Atlas6DirectiveSystem : MonoBehaviour, ISaveable, ISlowTickable, INarrativeEventListener, IAtlas6EventListener
    {
        private const int MinimumRevealStageForDirectiveIdentity = 3;
        private const string SignalIdentityDiscoveryId = "atlas6_signal_identified";
        private const string SignalFullyDecodedDiscoveryId = "atlas6_signal_fully_decoded";
        private const string TerminalSectorDiscoveryId = "atlas6_terminal_sector3";
        private const string CoreReachedDiscoveryId = "atlas6_core_reached";
        private const string CoreDataAccessedDiscoveryId = "atlas6_core_data_accessed";
        private static readonly uint _signalIdentityDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(SignalIdentityDiscoveryId);
        private static readonly uint _signalFullyDecodedDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(SignalFullyDecodedDiscoveryId);
        private static readonly uint _terminalSectorDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(TerminalSectorDiscoveryId);
        private static readonly uint _coreReachedDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(CoreReachedDiscoveryId);
        private static readonly uint _coreDataAccessedDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(CoreDataAccessedDiscoveryId);

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
        private uint _latestScarcityDirectiveQuestHash;
        private uint _latestScarcityDirectiveResourceHash;

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

            NarrativeEvents.Register(this);
            Atlas6Events.Register(this);
            ResolvePlayer();
        }

        private void OnDisable()
        {
            TryUnregister();

            if (SaveManager.Instance != null)
                SaveManager.Instance.Unregister(this);

            NarrativeEvents.Unregister(this);
            Atlas6Events.Unregister(this);
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
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Core);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
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

        public void OnNarrativeEvent(in NarrativeEventPayload payload)
        {
            if ((NarrativeEventType)payload.EventType != NarrativeEventType.DiscoveryMade)
                return;

            if (CanAdoptAtlasStatusFromDiscovery(payload.DiscoveryHash))
                SetStatus(Atlas6PlayerStatus.Detected);
        }

        public void OnAtlas6Event(in Atlas6EventPayload payload)
        {
            Atlas6EventType eventType = (Atlas6EventType)payload.EventType;
            if (eventType == Atlas6EventType.BarterAccepted)
            {
                HandleBarterAccepted(payload.TransactionCount);
                return;
            }

            if (eventType == Atlas6EventType.ScarcityDirectiveIssued)
                HandleScarcityDirective(payload.DirectiveQuestHash, payload.ResourceHash);
        }

        private void HandleBarterAccepted(int count)
        {
            // Первая торговля → Neutral
            if (_playerStatus == Atlas6PlayerStatus.Detected ||
                _playerStatus == Atlas6PlayerStatus.Unknown)
                SetStatus(Atlas6PlayerStatus.Neutral);
        }

        private void HandleScarcityDirective(uint directiveQuestHash, uint resourceHash)
        {
            _latestScarcityDirectiveQuestHash = directiveQuestHash;
            _latestScarcityDirectiveResourceHash = resourceHash;

            Quest.QuestManager questManager = Quest.QuestManager.Instance;
            if (questManager != null &&
                directiveQuestHash != 0u &&
                questManager.TryGetQuestPresentation(
                    directiveQuestHash,
                    out string title,
                    out _,
                    out _,
                    out _,
                    out _)
                && !string.IsNullOrWhiteSpace(title))
            {
                NotificationEvents.PushWarning(title);
                return;
            }

            string resourceName = "ESSENTIAL RESOURCE";
            IPlayerInventoryService inventoryService = GlobalRegistry.PlayerInventory;
            PlayerInventory inventory = inventoryService != null && inventoryService.IsInitialized
                ? inventoryService.Inventory
                : null;
            ItemCatalog catalog = inventory != null ? inventory.ItemCatalog : null;
            ItemData item = catalog != null ? catalog.FindByHash(unchecked((int)resourceHash)) : null;
            if (item != null)
                resourceName = item.itemName.ToUpperInvariant();

            NotificationEvents.PushWarning($"ATLAS-6 DIRECTIVE: RESTOCK {resourceName}.");
        }

        private bool CanAdoptAtlasStatusFromDiscovery(uint discoveryHash)
        {
            if (_playerStatus != Atlas6PlayerStatus.Unknown)
                return false;

            if (!IsDirectiveIdentityDiscovery(discoveryHash))
                return false;

            AtlasSignalSystem signal = AtlasSignalSystem.Instance;
            if (signal != null)
                return signal.CurrentRevealStage >= MinimumRevealStageForDirectiveIdentity;

            FirstHourDirector firstHourDirector = FirstHourDirector.Instance;
            if (firstHourDirector != null)
                return firstHourDirector.IsMilestoneComplete(FirstHourMilestone.HumCloser);

            return true;
        }

        private static bool IsDirectiveIdentityDiscovery(uint discoveryHash)
        {
            return discoveryHash == _signalIdentityDiscoveryHash ||
                   discoveryHash == _signalFullyDecodedDiscoveryHash ||
                   discoveryHash == _terminalSectorDiscoveryHash ||
                   discoveryHash == _coreReachedDiscoveryHash ||
                   discoveryHash == _coreDataAccessedDiscoveryHash;
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
            _latestScarcityDirectiveQuestHash = 0u;
            _latestScarcityDirectiveResourceHash = 0u;
        }
    }
}
