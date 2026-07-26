// ============================================================================
// HECTON-8 - DepthZoneDirector.cs
// Opredelyaet tekuschuyu zonu igroka po glubine i publikuet sobytiya.
//
// ROL:
//   - Otslezhivaet glubinu igroka cherez HectonSurvivalSystem.
//   - Pri smene zony: publikuet sobytie, registriruet discovery,
//     obnovlyaet quest runtime, uvedomlyaet HUD.
//   - Proveryaet trebovaniya k tiru korpusa - preduprezhdaet esli
//     igrok nyryaet glubzhe dopustimogo.
//
// ZERO GC:
//   - ISlowTickable - proverka zony raz v 0.5s.
//   - Nikakih new/LINQ v SlowTick.
// ============================================================================

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.UI;
using Unity.Mathematics;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.World
{
    /// <summary>
    /// Listener contract for queue-backed depth-zone notifications.
    /// </summary>
    public interface IDepthZoneEventListener
    {
        /// <summary>Called when the player enters a new depth zone.</summary>
        /// <param name="zone">Entered zone profile.</param>
        void OnDepthZoneEntered(DepthZoneProfile zone);

        /// <summary>Called when the player exits a depth zone.</summary>
        /// <param name="zone">Exited zone profile.</param>
        void OnDepthZoneExited(DepthZoneProfile zone);
    }

    public static class DepthZoneEvents
    {
        [StructLayout(LayoutKind.Explicit, Size = 8)]
        private struct DepthZoneEventPayload
        {
            [FieldOffset(0)] public uint ZoneHash;
            [FieldOffset(4)] public byte EventType;
            [FieldOffset(5)] private byte _pad0;
            [FieldOffset(6)] private ushort _pad1;
        }

        private const byte EnteredEventType = 1;
        private const byte ExitedEventType = 2;
        private const int PendingEventCapacity = 16;
        private const int ListenerCapacity = 16;
        private const int ProfileSidecarCapacity = 32;

        private struct ListenerSlot
        {
            public IDepthZoneEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct ProfileSlot
        {
            public uint ZoneHash;
            public DepthZoneProfile Profile;

            public void Clear()
            {
                ZoneHash = 0u;
                Profile = null;
            }
        }

        private static readonly ListenerSlot[] _listeners = new ListenerSlot[ListenerCapacity];
        private static readonly ProfileSlot[] _profileSlots = new ProfileSlot[ProfileSidecarCapacity];
        // COLD ALLOC: DepthZoneEventPayload[16] - bounded depth-zone event ring flushed by SystemDispatcher - owner: DepthZoneEvents
        private static DepthZoneEventPayload[] _pendingEvents = new DepthZoneEventPayload[PendingEventCapacity];
        // COLD ALLOC: DepthZoneEventPayload[16] - bounded next-frame ring for reentrant depth-zone dispatch - owner: DepthZoneEvents
        private static DepthZoneEventPayload[] _nextFrameEvents = new DepthZoneEventPayload[PendingEventCapacity];
        private static int _listenerCount;
        private static int _profileSlotCount;
        private static int _pendingEventHead;
        private static int _pendingEventTail;
        private static int _pendingEventCount;
        private static int _nextFrameEventHead;
        private static int _nextFrameEventTail;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _pendingEventHead = 0;
            _pendingEventTail = 0;
            _pendingEventCount = 0;
            _nextFrameEventHead = 0;
            _nextFrameEventTail = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
            for (int i = 0; i < ListenerCapacity; i++)
                _listeners[i].Clear();

            _listenerCount = 0;
            for (int i = 0; i < _profileSlotCount; i++)
                _profileSlots[i].Clear();

            _profileSlotCount = 0;
        }

        /// <summary>Vhod v novuyu zonu. DepthZoneProfile: novaya zona.</summary>

        /// <summary>Vyhod iz zony. DepthZoneProfile: pokinutaya zona.</summary>

        public static void Register(IDepthZoneEventListener listener)
        {
            if (listener == null)
                return;

            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i].Listener, listener))
                    return;
            }

            if (_listenerCount >= ListenerCapacity)
                return;

            _listeners[_listenerCount++].Listener = listener;
        }

        public static void Unregister(IDepthZoneEventListener listener)
        {
            if (listener == null)
                return;

            for (int i = 0; i < _listenerCount; i++)
            {
                if (!ReferenceEquals(_listeners[i].Listener, listener))
                    continue;

                int lastIndex = --_listenerCount;
                if (i != lastIndex)
                    _listeners[i].Listener = _listeners[lastIndex].Listener;

                _listeners[lastIndex].Clear();
                return;
            }
        }

        public static bool TryRaiseZoneEntered(DepthZoneProfile zone) => Enqueue(zone, EnteredEventType);
        public static bool TryRaiseZoneExited(DepthZoneProfile zone)  => Enqueue(zone, ExitedEventType);

        [Obsolete("Use TryRaiseZoneEntered so bounded queue refusal is visible at the producer.", true)]
        public static void RaiseZoneEntered(DepthZoneProfile zone) => TryRaiseZoneEntered(zone);

        [Obsolete("Use TryRaiseZoneExited so bounded queue refusal is visible at the producer.", true)]
        public static void RaiseZoneExited(DepthZoneProfile zone) => TryRaiseZoneExited(zone);

        public static void FlushPending()
        {
            PromoteNextFrameEventsIfFrontEmpty();
            if (_pendingEventCount <= 0)
                return;
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && _pendingEventCount > 0)
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!TryDequeuePending(out DepthZoneEventPayload payload))
                {
                    _pendingEventCount = 0;
                    break;
                }


                if (!TryResolveProfile(payload.ZoneHash, out DepthZoneProfile profile) || profile == null)
                    continue;

                int count = _listenerCount;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IDepthZoneEventListener listener = _listeners[i].Listener;
                        if (listener == null)
                            continue;

                        if (payload.EventType == EnteredEventType)
                            listener.OnDepthZoneEntered(profile);
                        else
                            listener.OnDepthZoneExited(profile);
                    }
                }
                finally
                {
                    _isDispatching = false;
                }
            }

            if (_pendingEventCount <= 0)
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        private static bool Enqueue(DepthZoneProfile zone, byte eventType)
        {
            if (zone == null || _listenerCount <= 0 || _pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return false;

            uint zoneHash = zone.ZoneHash != 0u ? zone.ZoneHash : unchecked((uint)EntityId.ToULong(zone.GetEntityId()));
            if (!TryStoreProfile(zoneHash, zone))
                return false;

            DepthZoneEventPayload payload = new DepthZoneEventPayload
            {
                ZoneHash = zoneHash,
                EventType = eventType
            };

            if (_isDispatching)
                return TryEnqueueNextFrame(payload);

            return TryEnqueuePending(payload);
        }

        private static bool TryStoreProfile(uint zoneHash, DepthZoneProfile profile)
        {
            for (int i = 0; i < _profileSlotCount; i++)
            {
                if (_profileSlots[i].ZoneHash != zoneHash)
                    continue;

                _profileSlots[i].Profile = profile;
                return true;
            }

            if (_profileSlotCount >= ProfileSidecarCapacity)
                return false;

            _profileSlots[_profileSlotCount++] = new ProfileSlot
            {
                ZoneHash = zoneHash,
                Profile = profile
            };
            return true;
        }

        private static bool TryResolveProfile(uint zoneHash, out DepthZoneProfile profile)
        {
            for (int i = 0; i < _profileSlotCount; i++)
            {
                if (_profileSlots[i].ZoneHash != zoneHash)
                    continue;

                profile = _profileSlots[i].Profile;
                return profile != null;
            }

            profile = null;
            return false;
        }

        private static bool TryEnqueuePending(DepthZoneEventPayload payload)
        {
            if (_pendingEventCount >= PendingEventCapacity)
                return false;

            _pendingEvents[_pendingEventTail] = payload;
            _pendingEventTail = (_pendingEventTail + 1) % PendingEventCapacity;
            _pendingEventCount++;
            return true;
        }

        private static bool TryEnqueueNextFrame(DepthZoneEventPayload payload)
        {
            if (_nextFrameEventCount >= PendingEventCapacity)
                return false;

            _nextFrameEvents[_nextFrameEventTail] = payload;
            _nextFrameEventTail = (_nextFrameEventTail + 1) % PendingEventCapacity;
            _nextFrameEventCount++;
            return true;
        }

        private static bool TryDequeuePending(out DepthZoneEventPayload payload)
        {
            if (_pendingEventCount <= 0)
            {
                payload = default;
                return false;
            }

            payload = _pendingEvents[_pendingEventHead];
            _pendingEvents[_pendingEventHead] = default;
            _pendingEventHead = (_pendingEventHead + 1) % PendingEventCapacity;
            _pendingEventCount--;
            return true;
        }

        private static void PromoteNextFrameEventsIfFrontEmpty()
        {
            if (_pendingEventCount > 0 || _nextFrameEventCount <= 0)
                return;

            DepthZoneEventPayload[] swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventHead = _nextFrameEventHead;
            _pendingEventTail = _nextFrameEventTail;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventHead = 0;
            _nextFrameEventTail = 0;
            _nextFrameEventCount = 0;
        }
    }
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-105)]
    public sealed class DepthZoneDirector : MonoBehaviour, ISlowTickable, ILateFrameTickable, ILocalizationLanguageChangedListener, IDepthZoneReadModel, IGlobalRegistryHotSwapListener
    {
        private const string DepthZoneDataRoot = "Assets/_Project/Data/Lore/DepthZones";
        private const int CachedDepthZoneCapacity = 32;
        private const int DepthZoneNotificationCharCapacity = 256;
        private const int DepthZoneAuxCharCapacity = 192;
        private const int DepthZoneNotificationRetryFrameLimit = 3;
        private const string UnknownZoneFallback = "UNKNOWN ZONE";
        private const string ZoneEnterFallbackTemplate = "ZONE: {0}";
        private const string HullWarningFallbackTemplate = "WARNING: SUIT HULL IS NOT RATED FOR THIS DEPTH. TIER {0}.";
        private static readonly uint _DepthZoneEventDropWarningHash = unchecked((uint)LocHash.Compute("DepthZoneDirector.EventDrop"));
        private static readonly uint _DepthZoneNotificationMissWarningHash = unchecked((uint)LocHash.Compute("DepthZoneDirector.NotificationMiss"));
        private static readonly uint _DepthZoneRuntimeContextHash = unchecked((uint)LocHash.Compute("DepthZoneDirector.Runtime"));
        private static readonly uint _DepthZoneEnteredContextHash = unchecked((uint)LocHash.Compute("DepthZoneDirector.Entered"));
        private static readonly uint _DepthZoneExitedContextHash = unchecked((uint)LocHash.Compute("DepthZoneDirector.Exited"));
        private static readonly uint _DepthZoneEnterNotificationContextHash = unchecked((uint)LocHash.Compute("DepthZoneDirector.EnterNotification"));
        private static readonly uint _DepthZoneHullWarningNotificationContextHash = unchecked((uint)LocHash.Compute("DepthZoneDirector.HullWarningNotification"));

        // ----------------------------------------------------------
        //  INSPECTOR
        // ----------------------------------------------------------

        [Header("-- Zones -----------------------------------")]
        [Tooltip("Vse zony glubiny. Poryadok ne vazhen - sortiruyutsya po minDepth.")]
        [SerializeField] private DepthZoneProfile[] zones = new DepthZoneProfile[0];

        [Header("-- References ------------------------------")]
        [Tooltip("Sistema vyzhivaniya dlya chteniya glubiny.")]
        [SerializeField] private HectonSurvivalSystem survivalSystem;

        [Header("-- Notification Cadence ----------------")]
        [Tooltip("Minimum delay between depth-zone enter HUD messages. Prevents boundary spam and early-route noise.")]
        [SerializeField, Min(0f)] private float zoneNotificationCooldown = 18f;

        // ----------------------------------------------------------
        //  SINGLETON
        // ----------------------------------------------------------

        private static DepthZoneDirector s_activeRuntimeInstance;

        public static DepthZoneDirector Instance => s_activeRuntimeInstance;

        // ----------------------------------------------------------
        //  PRIVATE STATE
        // ----------------------------------------------------------

        private DepthZoneProfile _currentZone;
        private bool _registered;
        private bool _registeredLateFrame;
        private bool _serviceRegistered;
        private bool _hotSwapRegistered;
        private bool _hullWarningShown;
        private float _nextZoneNotificationTime;
        private DepthZoneProfile _pendingZoneEntered;
        private DepthZoneProfile _pendingZoneExited;
        private DepthZoneProfile _pendingZoneNotification;
        private DepthZoneProfile _pendingHullWarningNotification;
        private uint _pendingDiscoveryHash;
        private DepthZoneProfile _pendingLogZone;
        private float _pendingLogDepth;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IQuestSystem _questSystem;
        private SuitUpgradeManager _suitUpgradeManager;
        private IFirstHourReadModel _firstHourDirector;
        private ILocalizationTextReadModel _localizationText;
        private int _depthZoneEventDropCount;
        private int _depthZoneNotificationMissCount;
        private int _pendingZoneNotificationRetryCount;
        private int _pendingHullWarningNotificationRetryCount;
        // COLD ALLOC: small per-zone identity cache avoids dynamic collection work in SlowTick transition path.
        private readonly DepthZoneProfile[] _cachedMessageZones = new DepthZoneProfile[CachedDepthZoneCapacity];
        private char[] _zoneNotificationBuffer;
        private char[] _zoneAuxBuffer;
        private int _cachedMessageCount;

        // ----------------------------------------------------------
        //  PUBLIC PROPERTIES
        // ----------------------------------------------------------

        public DepthZoneProfile CurrentZone => _currentZone;
        public int DepthZoneEventDropCount => _depthZoneEventDropCount;
        public int DepthZoneNotificationMissCount => _depthZoneNotificationMissCount;

        // ----------------------------------------------------------
        //  LIFECYCLE
        // ----------------------------------------------------------

        private void Awake()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            EnsureMessageBuffers();
            RebuildZoneMessageCache();
        }

        private void OnEnable()
        {
            if (!TryRegisterService())
                return;

            CacheRegistryServicesCold();
            TryRegister();
            TryRegisterHotSwapListener();
            RebuildZoneMessageCache();

            LocalizationEvents.RegisterLanguageListener(this);
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            TryUnregister();
            TryUnregisterService();

            LocalizationEvents.UnregisterLanguageListener(this);
            _nextZoneNotificationTime = 0f;
            ClearPendingPresentationEvents();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            TryUnregister();
            TryUnregisterService();
            LocalizationEvents.UnregisterLanguageListener(this);
            ClearPendingPresentationEvents();
        }

        // ----------------------------------------------------------
        //  ISlowTickable
        // ----------------------------------------------------------

        public void SlowTick()
        {
            if (!TryResolveCurrentDepthMeters(out float depth))
                return;

            if (zones == null || zones.Length == 0)
                return;

            // Nahodim tekuschuyu zonu
            DepthZoneProfile newZone = FindZoneForDepth(depth);

            // Obnovlyaem quest runtime posle razresheniya tekuschey authored zone context.
            IQuestSystem questSystem = _questSystem;
            if (questSystem != null)
            {
                questSystem.UpdateDepthContext(
                    depth,
                    newZone != null ? newZone.ZoneHash : 0u,
                    newZone != null && newZone.isThermal);
            }

            // Hysteresis Band: prevent rapid 2Hz zone-switching event storms at zone boundary
            if (_currentZone != null && newZone != _currentZone)
            {
                float boundaryDist = Mathf.Abs(depth - _currentZone.minDepth);
                if (boundaryDist < 2.5f)
                {
                    newZone = _currentZone;
                }
            }

            if (newZone == _currentZone)
            {
                // Proveryaem preduprezhdenie o korpuse
                CheckHullWarning(newZone);
                return;
            }

            // Smena zony
            DepthZoneProfile oldZone = _currentZone;
            _currentZone = newZone;
            _hullWarningShown = false;

            if (oldZone != null)
                _pendingZoneExited = oldZone;

            if (newZone != null)
            {
                _pendingZoneEntered = newZone;

                // Registriruem discovery
                if (newZone.DiscoveryHash != 0u)
                    _pendingDiscoveryHash = newZone.DiscoveryHash;

                // HUD uvedomlenie
                if (ShouldPublishZoneEnterNotification())
                {
                    _pendingZoneNotification = newZone;
                    _pendingZoneNotificationRetryCount = 0;
                    _nextZoneNotificationTime = (float)SystemDispatcher.CurrentUnscaledTimeSeconds + Mathf.Max(0f, zoneNotificationCooldown);
                }

                _pendingLogZone = newZone;
                _pendingLogDepth = depth;
            }
        }

        public void LateFrameTick()
        {
            if (_pendingZoneExited != null)
            {
                TryRaiseDepthZoneEvent(_pendingZoneExited, entered: false);
                _pendingZoneExited = null;
            }

            if (_pendingZoneEntered != null)
            {
                TryRaiseDepthZoneEvent(_pendingZoneEntered, entered: true);
                _pendingZoneEntered = null;
            }

            if (_pendingDiscoveryHash != 0u)
            {
                NarrativeEvents.TryRaiseDiscoveryMade(_pendingDiscoveryHash);
                _pendingDiscoveryHash = 0u;
            }

            if (_pendingZoneNotification != null)
            {
                if (TryPushDepthZoneNotification(
                    GetZoneEnterMessageSpan(_pendingZoneNotification),
                    _pendingZoneNotification,
                    warning: false) ||
                    ShouldDropDepthZoneNotificationAfterMiss(ref _pendingZoneNotificationRetryCount))
                {
                    _pendingZoneNotification = null;
                    _pendingZoneNotificationRetryCount = 0;
                }
            }

            if (_pendingHullWarningNotification != null)
            {
                if (TryPushDepthZoneNotification(
                    GetHullWarningMessageSpan(_pendingHullWarningNotification),
                    _pendingHullWarningNotification,
                    warning: true) ||
                    ShouldDropDepthZoneNotificationAfterMiss(ref _pendingHullWarningNotificationRetryCount))
                {
                    _pendingHullWarningNotification = null;
                    _pendingHullWarningNotificationRetryCount = 0;
                }
            }

            if (_pendingLogZone != null)
            {
                LogZoneEntered();
                _pendingLogZone = null;
                _pendingLogDepth = 0f;
            }
        }

        // ----------------------------------------------------------
        //  PRIVATE
        // ----------------------------------------------------------

        private DepthZoneProfile FindZoneForDepth(float depth)
        {
            DepthZoneProfile best = null;
            float bestMin = -1f;

            for (int i = 0; i < zones.Length; i++)
            {
                DepthZoneProfile z = zones[i];
                if (z == null) continue;
                if (!z.ContainsDepth(depth)) continue;

                // Berem zonu s naibolshim minDepth (naibolee spetsifichnuyu)
                if (z.minDepth > bestMin)
                {
                    bestMin = z.minDepth;
                    best = z;
                }
            }

            return best;
        }

        private void ClearPendingPresentationEvents()
        {
            _pendingZoneEntered = null;
            _pendingZoneExited = null;
            _pendingZoneNotification = null;
            _pendingHullWarningNotification = null;
            _pendingDiscoveryHash = 0u;
            _pendingLogZone = null;
            _pendingLogDepth = 0f;
            _depthZoneEventDropCount = 0;
            _depthZoneNotificationMissCount = 0;
            _pendingZoneNotificationRetryCount = 0;
            _pendingHullWarningNotificationRetryCount = 0;
        }

        private void TryRaiseDepthZoneEvent(DepthZoneProfile zone, bool entered)
        {
            if (zone == null)
                return;

            bool raised = entered
                ? DepthZoneEvents.TryRaiseZoneEntered(zone)
                : DepthZoneEvents.TryRaiseZoneExited(zone);
            if (raised)
                return;

            ReportDepthZoneEventDrop(zone, entered ? _DepthZoneEnteredContextHash : _DepthZoneExitedContextHash);
        }

        private bool TryPushDepthZoneNotification(ReadOnlySpan<char> message, DepthZoneProfile zone, bool warning)
        {
            bool pushed = warning
                ? NotificationEvents.TryPushWarning(message)
                : NotificationEvents.TryPushInfo(message);
            if (pushed)
                return true;

            ReportDepthZoneNotificationMiss(
                zone,
                warning ? _DepthZoneHullWarningNotificationContextHash : _DepthZoneEnterNotificationContextHash);
            return false;
        }

        private static bool ShouldDropDepthZoneNotificationAfterMiss(ref int retryCount)
        {
            retryCount++;
            return retryCount >= DepthZoneNotificationRetryFrameLimit;
        }

        private void ReportDepthZoneEventDrop(DepthZoneProfile zone, uint contextHash)
        {
            _depthZoneEventDropCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _DepthZoneEventDropWarningHash,
                ResolveDepthZoneTelemetryContext(zone, contextHash),
                math.max(1, _depthZoneEventDropCount));
        }

        private void ReportDepthZoneNotificationMiss(DepthZoneProfile zone, uint contextHash)
        {
            _depthZoneNotificationMissCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _DepthZoneNotificationMissWarningHash,
                ResolveDepthZoneTelemetryContext(zone, contextHash),
                math.max(1, _depthZoneNotificationMissCount));
        }

        private static uint ResolveDepthZoneTelemetryContext(DepthZoneProfile zone, uint contextHash)
        {
            uint zoneHash = 0u;
            if (zone != null)
            {
                zoneHash = zone.ZoneHash != 0u
                    ? zone.ZoneHash
                    : unchecked((uint)EntityId.ToULong(zone.GetEntityId()));
            }

            return _DepthZoneRuntimeContextHash ^ contextHash ^ zoneHash;
        }

        private void CheckHullWarning(DepthZoneProfile zone)
        {
            if (zone == null || _hullWarningShown) return;

            SuitUpgradeManager upgradeManager = _suitUpgradeManager;
            if (upgradeManager == null) return;

            if (upgradeManager.CurrentHullTier < zone.requiredHullTier)
            {
                _hullWarningShown = true;
                _pendingHullWarningNotification = zone;
                _pendingHullWarningNotificationRetryCount = 0;
            }
        }

        private bool ShouldPublishZoneEnterNotification()
        {
            if ((float)SystemDispatcher.CurrentUnscaledTimeSeconds < _nextZoneNotificationTime)
                return false;

            IFirstHourReadModel firstHourDirector = _firstHourDirector;
            if (firstHourDirector == null)
                return true;

            return firstHourDirector.IsFirstHourMilestoneComplete((int)FirstHourMilestone.Orientation);
        }

        private bool ResolveSurvivalSystemCold()
        {
            if (survivalSystem != null)
                return true;

            return TryCacheSurvivalSystemFromPlayerContext(_playerRuntimeContext);
        }

        private bool TryResolveCurrentDepthMeters(out float depth)
        {
            depth = 0f;
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null &&
                playerContext.IsInitialized &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                math.isfinite(movementState.DepthMeters))
            {
                depth = math.max(0f, movementState.DepthMeters);
                return true;
            }

            if (playerContext != null)
                return false;

            HectonSurvivalSystem survival = survivalSystem;
            if (survival != null && math.isfinite(survival.Depth))
            {
                depth = math.max(0f, survival.Depth);
                return true;
            }

            if (ResolveSurvivalSystemCold() && survivalSystem != null && math.isfinite(survivalSystem.Depth))
            {
                depth = math.max(0f, survivalSystem.Depth);
                return true;
            }

            return false;
        }

        private bool TryCacheSurvivalSystemFromPlayerContext(IPlayerRuntimeContext playerContext)
        {
            if (playerContext == null)
                return survivalSystem != null;

            HectonSurvivalSystem contextSurvivalSystem = playerContext.SurvivalSystem;
            if (contextSurvivalSystem == null)
                return survivalSystem != null;

            survivalSystem = contextSurvivalSystem;
            return true;
        }

        private void RebuildZoneMessageCache()
        {
            _cachedMessageCount = 0;

            if (zones == null || zones.Length == 0)
                return;

            int maxCacheCount = Mathf.Min(zones.Length, _cachedMessageZones.Length);
            for (int i = 0; i < maxCacheCount; i++)
            {
                DepthZoneProfile zone = zones[i];
                if (zone == null)
                    continue;

                zone.RebuildCache();
                _cachedMessageZones[_cachedMessageCount++] = zone;
            }
        }

        private ReadOnlySpan<char> GetZoneEnterMessageSpan(DepthZoneProfile zone)
        {
            EnsureMessageBuffers();
            ReadOnlySpan<char> zoneLabel = ResolveZoneDisplayNameUpperSpan(zone);
            ReadOnlySpan<char> template = ResolveLocalizedSpan(LocalizationKeys.DEPTH_ZONE_ENTER, ZoneEnterFallbackTemplate);
            if (!TryWriteSingleSpanTemplate(template, zoneLabel, _zoneNotificationBuffer, out int length))
                length = CopySpan(ZoneEnterFallbackTemplate.AsSpan(), _zoneNotificationBuffer);

            ReadOnlySpan<char> routeCue = ResolveZoneRouteCueSpan(zone);
            if (!IsWhiteSpace(routeCue))
            {
                AppendSpan(_zoneNotificationBuffer, ref length, " - ".AsSpan());
                AppendSpan(_zoneNotificationBuffer, ref length, routeCue);
            }

            return _zoneNotificationBuffer.AsSpan(0, length);
        }

        private ReadOnlySpan<char> GetHullWarningMessageSpan(DepthZoneProfile zone)
        {
            EnsureMessageBuffers();
            ReadOnlySpan<char> template = ResolveLocalizedSpan(LocalizationKeys.DEPTH_ZONE_HULL_WARNING, HullWarningFallbackTemplate);
            int hullTier = zone != null ? zone.requiredHullTier : 0;
            if (LocNumericBuffer.TryWrite(template, _zoneNotificationBuffer.AsSpan(), LocNumericArg.Int(hullTier), out int length))
                return _zoneNotificationBuffer.AsSpan(0, length);

            length = 0;
            AppendSpan(_zoneNotificationBuffer, ref length, "WARNING: SUIT HULL IS NOT RATED FOR THIS DEPTH. TIER ".AsSpan());
            ZeroGCFormatter.FastIntToChars(hullTier, _zoneNotificationBuffer.AsSpan(), ref length);
            AppendSpan(_zoneNotificationBuffer, ref length, ".".AsSpan());
            return _zoneNotificationBuffer.AsSpan(0, length);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogZoneEntered()
        {
            H8Debug.Log("[DepthZone] Entered.");
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildZoneMessageCache();
        }

        private ReadOnlySpan<char> ResolveUnknownZoneLabelSpan()
        {
            return ResolveLocalizedSpan(LocalizationKeys.DEPTH_ZONE_UNKNOWN, UnknownZoneFallback);
        }

        private ReadOnlySpan<char> ResolveLocalizedSpan(string key, string fallback)
        {
            ILocalizationTextReadModel manager = _localizationText;
            return manager != null
                ? manager.GetRawSpanOrFallback(LocHash.Compute(key.AsSpan()), fallback.AsSpan())
                : fallback.AsSpan();
        }

        private ReadOnlySpan<char> ResolveZoneDisplayNameUpperSpan(DepthZoneProfile zone)
        {
            ReadOnlySpan<char> source = zone != null
                ? zone.ResolveDisplayNameSpan(_localizationText)
                : ReadOnlySpan<char>.Empty;
            if (IsWhiteSpace(source))
                source = ResolveUnknownZoneLabelSpan();

            int length = CopyUpperAscii(source, _zoneAuxBuffer);
            return _zoneAuxBuffer.AsSpan(0, length);
        }

        private ReadOnlySpan<char> ResolveZoneRouteCueSpan(DepthZoneProfile zone)
        {
            if (zone == null)
                return ReadOnlySpan<char>.Empty;

            ReadOnlySpan<char> description = zone.ResolveDescriptionSpan(_localizationText);
            if (!IsWhiteSpace(description))
            {
                int length = CopyUpperAscii(description, _zoneAuxBuffer);
                return _zoneAuxBuffer.AsSpan(0, length);
            }

            if (zone.isThermal)
                return "THERMAL WATER DISTORTS COLOR AND RANGE. TRUST YOUR RETURN LINE, NOT THE GLOW.".AsSpan();

            if (zone.hasCaves)
                return "CAVES CUT READABILITY. HOLD A CLEAN EXIT VECTOR BEFORE YOU COMMIT.".AsSpan();

            if (zone.dangerLevel >= 0.75f)
                return "HIGH-PRESSURE WATER. ROUTE MEMORY MATTERS MORE THAN GREED HERE.".AsSpan();

            if (zone.dangerLevel >= 0.45f)
                return "VISIBILITY FALLS FAST. KEEP THE SAFER SILHOUETTE IN MEMORY.".AsSpan();

            return "READ THE SHELVES, NOT THE NOISE. SAFE WATER IS FOR RESET, NOT FORWARD PROGRESS.".AsSpan();
        }

        private void EnsureMessageBuffers()
        {
            if (_zoneNotificationBuffer == null || _zoneNotificationBuffer.Length < DepthZoneNotificationCharCapacity)
                _zoneNotificationBuffer = new char[DepthZoneNotificationCharCapacity];
            if (_zoneAuxBuffer == null || _zoneAuxBuffer.Length < DepthZoneAuxCharCapacity)
                _zoneAuxBuffer = new char[DepthZoneAuxCharCapacity];
        }

        private static bool TryWriteSingleSpanTemplate(ReadOnlySpan<char> template, ReadOnlySpan<char> arg0, char[] destination, out int length)
        {
            length = 0;
            if (destination == null)
                return false;

            for (int i = 0; i < template.Length; i++)
            {
                char c = template[i];
                if (c == '{' && i + 2 < template.Length && template[i + 1] == '0' && template[i + 2] == '}')
                {
                    if (!AppendSpan(destination, ref length, arg0))
                        return false;
                    i += 2;
                    continue;
                }

                if (length >= destination.Length)
                    return false;

                destination[length++] = c;
            }

            return true;
        }

        private static int CopySpan(ReadOnlySpan<char> source, char[] destination)
        {
            if (destination == null)
                return 0;

            int length = Mathf.Min(source.Length, destination.Length);
            source.Slice(0, length).CopyTo(destination.AsSpan(0, length));
            return length;
        }

        private static int CopyUpperAscii(ReadOnlySpan<char> source, char[] destination)
        {
            if (destination == null)
                return 0;

            int length = Mathf.Min(source.Length, destination.Length);
            for (int i = 0; i < length; i++)
            {
                char c = source[i];
                destination[i] = c >= 'a' && c <= 'z'
                    ? (char)(c - 32)
                    : c;
            }

            return length;
        }

        private static bool AppendSpan(char[] destination, ref int length, ReadOnlySpan<char> source)
        {
            if (destination == null || length < 0 || length > destination.Length)
                return false;

            int available = destination.Length - length;
            if (source.Length > available)
            {
                if (available <= 0)
                    return false;

                source.Slice(0, available).CopyTo(destination.AsSpan(length, available));
                length = destination.Length;
                return false;
            }

            source.CopyTo(destination.AsSpan(length, source.Length));
            length += source.Length;
            return true;
        }

        private static bool IsWhiteSpace(ReadOnlySpan<char> value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i]))
                    return false;
            }

            return true;
        }

        private void TryRegister()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registered)
                _registered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (_registered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registered = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }
        }

        private bool TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return true;

            if (TryAbortForUsableExistingRuntime())
                return false;

            GlobalRegistry.RegisterDepthZoneRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.DepthZone, this);
            if (_serviceRegistered)
                s_activeRuntimeInstance = this;
            return _serviceRegistered;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            if (ReferenceEquals(GlobalRegistry.DepthZone, this))
                GlobalRegistry.UnregisterDepthZoneRuntime(this);

            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;

            _serviceRegistered = false;
        }
        private bool TryAbortForUsableExistingRuntime()
        {
            if (!Application.isPlaying)
                return false;

            DepthZoneDirector registered = GlobalRegistry.DepthZone;
            if (!ReferenceEquals(registered, null) && !ReferenceEquals(registered, this))
            {
                if (IsDepthZoneRuntimeUsable(registered))
                {
                    s_activeRuntimeInstance = registered;
                    Destroy(gameObject);
                    return true;
                }

                if (ReferenceEquals(s_activeRuntimeInstance, registered))
                    s_activeRuntimeInstance = null;

                GlobalRegistry.UnregisterDepthZoneRuntime(registered);
            }

            DepthZoneDirector active = s_activeRuntimeInstance;
            if (ReferenceEquals(active, null) || ReferenceEquals(active, this))
                return false;

            if (IsDepthZoneRuntimeUsable(active))
            {
                GlobalRegistry.RegisterDepthZoneRuntime(active);
                Destroy(gameObject);
                return true;
            }

            s_activeRuntimeInstance = null;
            if (ReferenceEquals(GlobalRegistry.DepthZone, active))
                GlobalRegistry.UnregisterDepthZoneRuntime(active);
            return false;
        }

        private static bool IsDepthZoneRuntimeUsable(DepthZoneDirector director)
        {
            return !ReferenceEquals(director, null) &&
                   director != null &&
                   director._serviceRegistered &&
                   director.isActiveAndEnabled;
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.QuestRuntime:
                    _questSystem = currentService as IQuestSystem;
                    break;
                case GlobalRegistryServiceSlot.SuitUpgradeRuntime:
                    _suitUpgradeManager = currentService as SuitUpgradeManager;
                    break;
                case GlobalRegistryServiceSlot.FirstHourRuntime:
                    _firstHourDirector = currentService as IFirstHourReadModel;
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localizationText = currentService as ILocalizationTextReadModel;
                    RebuildZoneMessageCache();
                    break;
                case GlobalRegistryServiceSlot.Player:
                    IPlayerRuntimeContext previousPlayerContext = previousService as IPlayerRuntimeContext;
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    if (_playerRuntimeContext != null)
                    {
                        survivalSystem = _playerRuntimeContext.SurvivalSystem;
                    }
                    else if (previousPlayerContext != null && ReferenceEquals(survivalSystem, previousPlayerContext.SurvivalSystem))
                    {
                        survivalSystem = null;
                    }

                    break;
            }
        }

        private void CacheRegistryServicesCold()
        {
            _playerRuntimeContext = GlobalRegistry.Player;
            ResolveSurvivalSystemCold();
            _questSystem = GlobalRegistry.QuestSystem;
            _suitUpgradeManager = GlobalRegistry.SuitUpgrades;
            _firstHourDirector = GlobalRegistry.FirstHourReadModel;
            _localizationText = GlobalRegistry.LocalizationText;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            TryAutoPopulateZones();
            RebuildZoneMessageCache();
        }

        private void TryAutoPopulateZones()
        {
            if (zones != null && zones.Length > 0)
                return;

            string[] guids = AssetDatabase.FindAssets("t:DepthZoneProfile", new[] { DepthZoneDataRoot });
            if (guids == null || guids.Length <= 0)
                return;

            DepthZoneProfile[] loadedZones = new DepthZoneProfile[guids.Length];
            int loadedCount = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                DepthZoneProfile zone = AssetDatabase.LoadAssetAtPath<DepthZoneProfile>(path);
                if (zone == null)
                    continue;

                loadedZones[loadedCount] = zone;
                loadedCount++;
            }

            if (loadedCount <= 0)
                return;

            if (loadedCount != loadedZones.Length)
            {
                DepthZoneProfile[] compactZones = new DepthZoneProfile[loadedCount];
                System.Array.Copy(loadedZones, compactZones, loadedCount);
                loadedZones = compactZones;
            }

            SortZonesByMinDepth(loadedZones);
            zones = loadedZones;
            EditorUtility.SetDirty(this);
        }

        private static void SortZonesByMinDepth(DepthZoneProfile[] authoredZones)
        {
            if (authoredZones == null || authoredZones.Length <= 1)
                return;

            for (int i = 0; i < authoredZones.Length - 1; i++)
            {
                int bestIndex = i;
                float bestDepth = authoredZones[i] != null ? authoredZones[i].minDepth : float.MaxValue;
                for (int j = i + 1; j < authoredZones.Length; j++)
                {
                    float candidateDepth = authoredZones[j] != null ? authoredZones[j].minDepth : float.MaxValue;
                    if (candidateDepth < bestDepth)
                    {
                        bestIndex = j;
                        bestDepth = candidateDepth;
                    }
                }

                if (bestIndex == i)
                    continue;

                DepthZoneProfile swap = authoredZones[i];
                authoredZones[i] = authoredZones[bestIndex];
                authoredZones[bestIndex] = swap;
            }
        }
#endif
    }
}
