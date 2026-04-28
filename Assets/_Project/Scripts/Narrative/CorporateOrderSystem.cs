// ============================================================================
// HECTON-8 — CorporateOrderSystem.cs
// Система противоречивых корпоративных приказов.
//
// ЛОР (лор3 Блок А):
//   Игрок получает противоречивые приказы через задержку связи (8-12 часов).
//   Фракция «Этики» vs «Прагматики».
//   Это не диалог — это нарратив через интерфейс.
//
// МЕХАНИКА:
//   • Приказы приходят с задержкой (игровое время).
//   • При получении конфликтующего приказа — HUD уведомление.
//   • Игрок видит оба приказа в PDA (Data Log).
//   • Выбор — через действия в мире, не через диалог.
//
// ZERO GC:
//   • ISlowTickable — проверка таймеров.
//   • Pre-allocated массив состояний приказов.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Hecton.Localization;
using UnityEngine;

namespace Hecton8.Narrative
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-75)]
    public sealed class CorporateOrderSystem : MonoBehaviour, ISaveable, ISlowTickable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Data ─────────────────────────────────────")]
        [SerializeField] private DeepReachCorporationData corporationData;

        [Header("── Timing ───────────────────────────────────")]
        [Tooltip("Игровых секунд в одном игровом часе (для задержки приказов).")]
        [SerializeField] private float gameSecondsPerHour = 120f; // 2 мин реального времени = 1 игровой час

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        public static CorporateOrderSystem Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Instance = null;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        // COLD ALLOC: max 16 orders
        private readonly HashSet<string> _receivedOrders  = new HashSet<string>(16);
        private readonly HashSet<string> _activeConflicts = new HashSet<string>(8);

        // Таймеры ожидания приказов (orderId → remaining seconds)
        private readonly Dictionary<string, float> _pendingTimers =
            new Dictionary<string, float>(16);

        private bool _registered;
        private bool _ordersScheduled;

        // COLD ALLOC: буфер для доставки приказов в SlowTick
        private readonly List<string> _deliveryBuffer = new List<string>(4);

        // COLD ALLOC: буфер ключей для итерации Dictionary без foreach-аллокации
        private readonly List<string> _pendingKeyBuffer = new List<string>(16);

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        public int SavePriority => 12;
        public int LoadPriority => 12;

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
        }

        private void OnDisable()
        {
            TryUnregister();

            if (SaveManager.Instance != null)
                SaveManager.Instance.Unregister(this);
        }

        private void OnDestroy()
        {
            TryUnregister();

            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            if (!_ordersScheduled)
                ScheduleAllOrders();
        }

        private void TryRegister()
        {
            if (_registered)
                return;


            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Player);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);

            _registered = false;
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            if (corporationData == null || _pendingTimers.Count == 0)
                return;

            const float dt = 0.5f;

            _deliveryBuffer.Clear();

            // Populate key buffer — avoids Dictionary enumerator GC alloc
            _pendingKeyBuffer.Clear();
            Dictionary<string, float>.Enumerator pendingTimerEnumerator = _pendingTimers.GetEnumerator();
            while (pendingTimerEnumerator.MoveNext())
                _pendingKeyBuffer.Add(pendingTimerEnumerator.Current.Key);

            for (int i = 0; i < _pendingKeyBuffer.Count; i++)
            {
                string key = _pendingKeyBuffer[i];
                float remaining = _pendingTimers[key] - dt;
                if (remaining <= 0f)
                    _deliveryBuffer.Add(key);
                else
                    _pendingTimers[key] = remaining;
            }

            for (int i = 0; i < _deliveryBuffer.Count; i++)
            {
                string orderId = _deliveryBuffer[i];
                _pendingTimers.Remove(orderId);
                DeliverOrder(orderId);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        public bool HasReceivedOrder(string orderId) => _receivedOrders.Contains(orderId);
        public bool HasActiveConflict(string conflictId) => _activeConflicts.Contains(conflictId);

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private void ScheduleAllOrders()
        {
            _ordersScheduled = true;
            if (corporationData == null) return;

            for (int i = 0; i < corporationData.orders.Length; i++)
            {
                CorporateOrder order = corporationData.orders[i];
                if (string.IsNullOrEmpty(order.orderId)) continue;
                if (_receivedOrders.Contains(order.orderId)) continue;

                float delaySeconds = order.transmissionDelayHours * gameSecondsPerHour;
                _pendingTimers[order.orderId] = delaySeconds;
            }
        }

        private void DeliverOrder(string orderId)
        {
            if (corporationData == null) return;
            if (!corporationData.TryGetOrder(orderId, out CorporateOrder order)) return;

            _receivedOrders.Add(orderId);

            // Регистрируем как discovery для PDA
            NarrativeEvents.RaiseDiscoveryMade($"corporate_order_{orderId}");

            // HUD уведомление — берём первые 60 символов без аллокации через Substring
            string orderText = order.OrderTextOrFallback;
            string preview = orderText.Length > 60
                ? orderText.Substring(0, 60) + "..."
                : orderText;
            NotificationEvents.PushWarning(string.Format(
                ResolveLocalized(LocalizationKeys.CORP_ORDER_INCOMING, "INCOMING ORDER - {0}: {1}"),
                ResolveFactionLabel(order.sourceFactionId),
                preview));

            // Проверяем конфликт
            if (!string.IsNullOrEmpty(order.conflictsWithOrderId) &&
                _receivedOrders.Contains(order.conflictsWithOrderId))
            {
                string conflictKey = $"{orderId}_vs_{order.conflictsWithOrderId}";
                if (_activeConflicts.Add(conflictKey))
                {
                    NotificationEvents.PushWarning(ResolveLocalized(
                        LocalizationKeys.CORP_ORDER_CONFLICT,
                        "ORDER CONFLICT - CORPORATE FACTIONS ARE DIRECTLY CONTRADICTING EACH OTHER."));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[CorporateOrders] Conflict: {orderId} vs {order.conflictsWithOrderId}");
#endif
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[CorporateOrders] Delivered: {orderId} from {order.sourceFactionId}");
#endif
        }

        private string ResolveFactionLabel(string factionId)
        {
            if (corporationData != null && corporationData.factions != null)
            {
                for (int i = 0; i < corporationData.factions.Length; i++)
                {
                    if (string.Equals(corporationData.factions[i].factionId, factionId, System.StringComparison.Ordinal))
                        return corporationData.factions[i].DisplayNameOrFallback;
                }
            }

            return factionId;
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

            data.corporateReceivedOrderIds.Clear();
            foreach (string id in _receivedOrders)
                data.corporateReceivedOrderIds.Add(id);

            // Сохраняем таймеры ожидания
            data.corporatePendingOrderIds.Clear();
            data.corporatePendingOrderTimers.Clear();
            foreach (var kvp in _pendingTimers)
            {
                data.corporatePendingOrderIds.Add(kvp.Key);
                data.corporatePendingOrderTimers.Add(kvp.Value);
            }
        }

        public void LoadFromSaveData(SaveData data)
        {
            _receivedOrders.Clear();
            _pendingTimers.Clear();
            _ordersScheduled = true;

            if (data == null) return;

            if (data.corporateReceivedOrderIds != null)
                foreach (string id in data.corporateReceivedOrderIds)
                    if (!string.IsNullOrEmpty(id)) _receivedOrders.Add(id);

            if (data.corporatePendingOrderIds != null && data.corporatePendingOrderTimers != null)
            {
                int count = Mathf.Min(data.corporatePendingOrderIds.Count,
                                      data.corporatePendingOrderTimers.Count);
                for (int i = 0; i < count; i++)
                {
                    string id = data.corporatePendingOrderIds[i];
                    if (!string.IsNullOrEmpty(id))
                        _pendingTimers[id] = data.corporatePendingOrderTimers[i];
                }
            }

            // Если нет сохранённых таймеров — планируем заново
            if (_pendingTimers.Count == 0 && _receivedOrders.Count == 0)
                ScheduleAllOrders();
        }
    }
}
