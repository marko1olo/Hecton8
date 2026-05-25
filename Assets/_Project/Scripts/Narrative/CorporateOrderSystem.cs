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

using System;
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
    public sealed class CorporateOrderSystem : MonoBehaviour, ISaveable, ISlowTickable, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private const string IncomingOrderWarningMessage = "INCOMING CORPORATE ORDER - CHECK PDA LOG";
        private const uint ConflictHashSalt = 0xC0A5_EE11u;
        private const int OrderCapacity = SaveData.MaxCorporateOrderIds;

        // ----------------------------------------------------------
        //  INSPECTOR
        // ----------------------------------------------------------

        [Header("-- Data -------------------------------------")]
        [SerializeField] private DeepReachCorporationData corporationData;

        [Header("-- Timing -----------------------------------")]
        [Tooltip("Igrovyh sekund v odnom igrovom chase (dlya zaderzhki prikazov).")]
        [SerializeField] private float gameSecondsPerHour = 120f; // 2 min realnogo vremeni = 1 igrovoy chas

        // ----------------------------------------------------------
        //  SINGLETON
        // ----------------------------------------------------------

        // ----------------------------------------------------------
        //  PRIVATE STATE
        // ----------------------------------------------------------

        // COLD ALLOC: max 16 orders
        private readonly HashSet<string> _receivedOrders  = new HashSet<string>(OrderCapacity);
        private readonly HashSet<uint> _activeConflicts = new HashSet<uint>(OrderCapacity);

        // Taymery ozhidaniya prikazov (orderId ? remaining seconds)
        private readonly Dictionary<string, float> _pendingTimers =
            new Dictionary<string, float>(OrderCapacity);

        private bool _runtimeRegistered;
        private bool _registered;
        private bool _saveRegistered;
        private bool _registeredHotSwapListener;
        private bool _ordersScheduled;
        private ISaveService _saveService;

        // ----------------------------------------------------------
        //  ISaveable
        // ----------------------------------------------------------

        public int SavePriority => 12;
        public int LoadPriority => 12;
        public ServiceHeartbeatState HeartbeatState => _runtimeRegistered ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;
        public bool IsServiceReady => _runtimeRegistered;

        // ----------------------------------------------------------
        //  LIFECYCLE
        // ----------------------------------------------------------

        private void Awake()
        {
            CorporateOrderSystem registered = GlobalRegistry.CorporateOrders;
            if (registered != null && registered != this) { Destroy(gameObject); return; }
        }

        private void OnEnable()
        {
            if (!TryRegisterRuntime())
                return;

            _saveService = Hecton8.SaveSystem.SaveManager.ActiveRuntimeInstance;
            TryRegisterHotSwapListener();
            TryRegister();
            TryRegisterSaveParticipant();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();
            TryUnregisterRuntime();
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();
            TryUnregisterRuntime();
        }

        public void OnServiceShutdown()
        {
            TryUnregister();
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();
            TryUnregisterRuntime();
            _receivedOrders.Clear();
            _activeConflicts.Clear();
            _pendingTimers.Clear();
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

            _registered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player);
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Save)
                return;

            TryUnregisterSaveParticipant();
            _saveService = currentService as ISaveService;
            TryRegisterSaveParticipant();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
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

        private void TryRegisterSaveParticipant()
        {
            if (_saveRegistered || !Application.isPlaying || !isActiveAndEnabled)
                return;

            if (_saveService == null)
                _saveService = Hecton8.SaveSystem.SaveManager.ActiveRuntimeInstance;

            if (_saveService == null)
                return;

            _saveService.Register(this);
            _saveRegistered = true;
        }

        private void TryUnregisterSaveParticipant()
        {
            if (!_saveRegistered)
                return;

            ISaveService saveService = _saveService;
            if (saveService != null)
                saveService.Unregister(this);

            _saveRegistered = false;
        }

        // ----------------------------------------------------------
        //  ISlowTickable
        // ----------------------------------------------------------

        public void SlowTick()
        {
            if (corporationData == null || _pendingTimers.Count == 0)
                return;

            const float dt = 0.5f;

            CorporateOrder[] orders = corporationData.orders;
            if (orders == null || orders.Length == 0)
                return;

            for (int i = 0; i < orders.Length; i++)
            {
                string key = orders[i].orderId;
                if (string.IsNullOrEmpty(key) || !_pendingTimers.TryGetValue(key, out float remaining))
                    continue;

                remaining -= dt;
                if (remaining <= 0f)
                {
                    _pendingTimers.Remove(key);
                    DeliverOrder(key);
                    continue;
                }

                _pendingTimers[key] = remaining;
            }
        }

        // ----------------------------------------------------------
        //  PUBLIC API
        // ----------------------------------------------------------

        public bool HasReceivedOrder(string orderId) => _receivedOrders.Contains(orderId);
        public bool HasActiveConflict(string conflictId) => _activeConflicts.Contains(ComputeStableHash(conflictId));

        // ----------------------------------------------------------
        //  PRIVATE
        // ----------------------------------------------------------

        private void ScheduleAllOrders()
        {
            _ordersScheduled = true;
            if (corporationData == null) return;

            for (int i = 0; i < corporationData.orders.Length; i++)
            {
                CorporateOrder order = corporationData.orders[i];
                if (string.IsNullOrEmpty(order.orderId)) continue;
                if (_receivedOrders.Contains(order.orderId)) continue;
                if (_pendingTimers.Count + _receivedOrders.Count >= OrderCapacity) break;

                float delaySeconds = order.transmissionDelayHours * gameSecondsPerHour;
                _pendingTimers[order.orderId] = delaySeconds;
            }
        }

        private void DeliverOrder(string orderId)
        {
            if (corporationData == null) return;
            if (!corporationData.TryGetOrder(orderId, out CorporateOrder order)) return;
            if (!_receivedOrders.Contains(orderId) && _receivedOrders.Count >= OrderCapacity) return;

            _receivedOrders.Add(orderId);

            // Use authored order id hash directly; PDA cold data holds the full text.
            NarrativeEvents.TryRaiseDiscoveryMade(ComputeStableHash(orderId));

            // Cinematic fake: static HUD warning avoids runtime preview string assembly.
            NotificationEvents.TryPushWarning(IncomingOrderWarningMessage.AsSpan());

            // Proveryaem konflikt
            if (!string.IsNullOrEmpty(order.conflictsWithOrderId) &&
                _receivedOrders.Contains(order.conflictsWithOrderId))
            {
                uint conflictHash = ComputeConflictHash(orderId, order.conflictsWithOrderId);
                if (conflictHash != 0u &&
                    _activeConflicts.Count < OrderCapacity &&
                    _activeConflicts.Add(conflictHash))
                {
                    NotificationEvents.TryPushWarning(ResolveLocalizedSpan(
                        LocalizationKeys.CORP_ORDER_CONFLICT,
                        "ORDER CONFLICT - CORPORATE FACTIONS ARE DIRECTLY CONTRADICTING EACH OTHER."));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    H8Debug.Log("[CorporateOrders] Conflict.");
#endif
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.Log("[CorporateOrders] Delivered.");
#endif
        }

        private static ReadOnlySpan<char> ResolveLocalizedSpan(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null
                ? manager.GetRawSpanOrFallback(LocHash.Compute(key), fallback.AsSpan())
                : fallback.AsSpan();
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

        // ----------------------------------------------------------
        //  ISaveable
        // ----------------------------------------------------------

        public void PopulateSaveData(SaveData data)
        {
            if (data == null) return;

            data.corporateReceivedOrderIds.Clear();
            foreach (string id in _receivedOrders)
            {
                if (data.corporateReceivedOrderIds.Count >= OrderCapacity)
                    break;

                data.corporateReceivedOrderIds.Add(id);
            }

            // Sohranyaem taymery ozhidaniya
            data.corporatePendingOrderIds.Clear();
            data.corporatePendingOrderTimers.Clear();
            foreach (var kvp in _pendingTimers)
            {
                if (data.corporatePendingOrderIds.Count >= OrderCapacity)
                    break;

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
                {
                    if (_receivedOrders.Count >= OrderCapacity)
                        break;

                    if (!string.IsNullOrEmpty(id)) _receivedOrders.Add(id);
                }

            if (data.corporatePendingOrderIds != null && data.corporatePendingOrderTimers != null)
            {
                int count = Mathf.Min(
                    Mathf.Min(data.corporatePendingOrderIds.Count, data.corporatePendingOrderTimers.Count),
                    OrderCapacity);
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
