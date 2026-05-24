// ============================================================================
// HECTON-8 — CorporateOrderSystem.cs
// Sistema protivorechivyh korporativnyh prikazov.
//
// LOR (lor3 Blok A):
//   Igrok poluchaet protivorechivye prikazy cherez zaderzhku svyazi (8-12 chasov).
//   Fraktsiya «Etiki» vs «Pragmatiki».
//   Eto ne dialog — eto narrativ cherez interfeys.
//
// MEHANIKA:
//   • Prikazy prihodyat s zaderzhkoy (igrovoe vremya).
//   • Pri poluchenii konfliktuyuschego prikaza — HUD uvedomlenie.
//   • Igrok vidit oba prikaza v PDA (Data Log).
//   • Vybor — cherez deystviya v mire, ne cherez dialog.
//
// ZERO GC:
//   • ISlowTickable — proverka taymerov.
//   • Pre-allocated massiv sostoyaniy prikazov.
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
    public sealed class CorporateOrderSystem : MonoBehaviour, ISaveable, ISlowTickable, IServiceHeartbeat, IServiceShutdown
    {
        private const string IncomingOrderWarningMessage = "INCOMING CORPORATE ORDER - CHECK PDA LOG";
        private const uint ConflictHashSalt = 0xC0A5_EE11u;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Data ─────────────────────────────────────")]
        [SerializeField] private DeepReachCorporationData corporationData;

        [Header("── Timing ───────────────────────────────────")]
        [Tooltip("Igrovyh sekund v odnom igrovom chase (dlya zaderzhki prikazov).")]
        [SerializeField] private float gameSecondsPerHour = 120f; // 2 min realnogo vremeni = 1 igrovoy chas

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        // COLD ALLOC: max 16 orders
        private readonly HashSet<string> _receivedOrders  = new HashSet<string>(16);
        private readonly HashSet<uint> _activeConflicts = new HashSet<uint>(8);

        // Taymery ozhidaniya prikazov (orderId → remaining seconds)
        private readonly Dictionary<string, float> _pendingTimers =
            new Dictionary<string, float>(16);

        private bool _runtimeRegistered;
        private bool _registered;
        private bool _saveRegistered;
        private bool _ordersScheduled;

        // COLD ALLOC: bufer dlya dostavki prikazov v SlowTick
        private readonly List<string> _deliveryBuffer = new List<string>(16);

        // COLD ALLOC: bufer klyuchey dlya iteratsii Dictionary bez foreach-allokatsii
        private readonly List<string> _pendingKeyBuffer = new List<string>(16);

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        public int SavePriority => 12;
        public int LoadPriority => 12;
        public ServiceHeartbeatState HeartbeatState => _runtimeRegistered ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;
        public bool IsServiceReady => _runtimeRegistered;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            CorporateOrderSystem registered = GlobalRegistry.CorporateOrders;
            if (registered != null && registered != this) { Destroy(gameObject); return; }
        }

        private void OnEnable()
        {
            if (!TryRegisterRuntime())
                return;

            TryRegister();
            TryRegisterSaveParticipant();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterSaveParticipant();
            TryUnregisterRuntime();
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterSaveParticipant();
            TryUnregisterRuntime();
        }

        public void OnServiceShutdown()
        {
            TryUnregister();
            TryUnregisterSaveParticipant();
            TryUnregisterRuntime();
            _receivedOrders.Clear();
            _activeConflicts.Clear();
            _pendingTimers.Clear();
            _deliveryBuffer.Clear();
            _pendingKeyBuffer.Clear();
            _ordersScheduled = false;
        }

        private void Start()
        {
            if (!_ordersScheduled)
                ScheduleAllOrders();
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Player);
            _registered = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);

            _registered = false;
        }

        private bool TryRegisterRuntime()
        {
            if (_runtimeRegistered)
                return true;

            if (!Application.isPlaying)
                return false;

            CorporateOrderSystem registered = GlobalRegistry.CorporateOrders;
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return false;
            }

            GlobalRegistry.RegisterCorporateOrderRuntime(this);
            _runtimeRegistered = ReferenceEquals(GlobalRegistry.CorporateOrders, this);
            return _runtimeRegistered;
        }

        private void TryUnregisterRuntime()
        {
            if (!_runtimeRegistered)
                return;

            GlobalRegistry.UnregisterCorporateOrderRuntime(this);
            _runtimeRegistered = false;
        }

        private void TryRegisterSaveParticipant()
        {
            if (_saveRegistered)
                return;

            ISaveService saveService = Hecton8.SaveSystem.SaveManager.ActiveRuntimeInstance;
            if (saveService == null)
                return;

            saveService.Register(this);
            _saveRegistered = true;
        }

        private void TryUnregisterSaveParticipant()
        {
            if (!_saveRegistered)
                return;

            ISaveService saveService = Hecton8.SaveSystem.SaveManager.ActiveRuntimeInstance;
            if (saveService != null)
                saveService.Unregister(this);

            _saveRegistered = false;
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
        public bool HasActiveConflict(string conflictId) => _activeConflicts.Contains(ComputeStableHash(conflictId));

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

            // Use authored order id hash directly; PDA cold data holds the full text.
            NarrativeEvents.RaiseDiscoveryMade(ComputeStableHash(orderId));

            // Cinematic fake: static HUD warning avoids runtime preview string assembly.
            NotificationEvents.PushWarning(IncomingOrderWarningMessage);

            // Proveryaem konflikt
            if (!string.IsNullOrEmpty(order.conflictsWithOrderId) &&
                _receivedOrders.Contains(order.conflictsWithOrderId))
            {
                uint conflictHash = ComputeConflictHash(orderId, order.conflictsWithOrderId);
                if (conflictHash != 0u && _activeConflicts.Add(conflictHash))
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

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback) : fallback;
        }

        private static uint ComputeStableHash(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0u;

            const uint fnvOffset = 2166136261u;
            const uint fnvPrime = 16777619u;
            unchecked
            {
                uint hash = fnvOffset;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= fnvPrime;
                }

                return hash != 0u ? hash : 1u;
            }
        }

        private static uint ComputeConflictHash(string firstOrderId, string secondOrderId)
        {
            uint firstHash = ComputeStableHash(firstOrderId);
            uint secondHash = ComputeStableHash(secondOrderId);
            if (firstHash == 0u || secondHash == 0u)
                return 0u;

            uint low = firstHash < secondHash ? firstHash : secondHash;
            uint high = firstHash < secondHash ? secondHash : firstHash;
            unchecked
            {
                uint hash = ConflictHashSalt;
                hash ^= low + 0x9E37_79B9u + (hash << 6) + (hash >> 2);
                hash ^= high + 0x85EB_CA6Bu + (hash << 6) + (hash >> 2);
                return hash != 0u ? hash : 1u;
            }
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

            // Sohranyaem taymery ozhidaniya
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

            // Esli net sohranennyh taymerov — planiruem zanovo
            if (_pendingTimers.Count == 0 && _receivedOrders.Count == 0)
                ScheduleAllOrders();
        }
    }
}
