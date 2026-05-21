// ============================================================================
// HECTON-8 — DepthZoneDirector.cs
// Opredelyaet tekuschuyu zonu igroka po glubine i publikuet sobytiya.
//
// ROL:
//   • Otslezhivaet glubinu igroka cherez HectonSurvivalSystem.
//   • Pri smene zony: publikuet sobytie, registriruet discovery,
//     obnovlyaet QuestManager, uvedomlyaet HUD.
//   • Proveryaet trebovaniya k tiru korpusa — preduprezhdaet esli
//     igrok nyryaet glubzhe dopustimogo.
//
// ZERO GC:
//   • ISlowTickable — proverka zony raz v 0.5s.
//   • Nikakih new/LINQ v SlowTick.
// ============================================================================

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Quest;
using Hecton8.UI;
using Unity.Collections;
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
        private const Allocator DataVaultExemptDepthZoneEventLaneAllocator = Allocator.Persistent;

        private struct ListenerSlot
        {
            public IDepthZoneEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private static readonly ListenerSlot[] _listeners = new ListenerSlot[ListenerCapacity];
        private static readonly System.Collections.Generic.Dictionary<uint, DepthZoneProfile> _profilesByHash = new System.Collections.Generic.Dictionary<uint, DepthZoneProfile>(32);
        private static NativeQueue<DepthZoneEventPayload> _pendingEvents;
        private static NativeQueue<DepthZoneEventPayload> _nextFrameEvents;
        private static int _listenerCount;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(DepthZoneEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(DepthZoneEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
            for (int i = 0; i < ListenerCapacity; i++)
                _listeners[i].Clear();

            _listenerCount = 0;
            _profilesByHash.Clear();
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

        public static void RaiseZoneEntered(DepthZoneProfile zone) => Enqueue(zone, EnteredEventType);
        public static void RaiseZoneExited(DepthZoneProfile zone)  => Enqueue(zone, ExitedEventType);

        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated)
                return;

            PromoteNextFrameEventsIfFrontEmpty();
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out DepthZoneEventPayload payload))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                if (!_profilesByHash.TryGetValue(payload.ZoneHash, out DepthZoneProfile profile) || profile == null)
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

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        private static void Enqueue(DepthZoneProfile zone, byte eventType)
        {
            if (zone == null || _listenerCount <= 0 || _pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return;

            EnsureInitialized();
            uint zoneHash = zone.ZoneHash != 0u ? zone.ZoneHash : unchecked((uint)EntityId.ToULong(zone.GetEntityId()));
            _profilesByHash[zoneHash] = zone;
            DepthZoneEventPayload payload = new DepthZoneEventPayload
            {
                ZoneHash = zoneHash,
                EventType = eventType
            };

            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(payload);
                _nextFrameEventCount++;
                return;
            }

            _pendingEvents.Enqueue(payload);
            _pendingEventCount++;
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<DepthZoneEventPayload>(DataVaultExemptDepthZoneEventLaneAllocator); // COLD ALLOC: NativeQueue<DepthZoneEventPayload>[16] - depth-zone event lane flushed by SystemDispatcher - owner: DepthZoneEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(DepthZoneEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<DepthZoneEventPayload>(DataVaultExemptDepthZoneEventLaneAllocator); // COLD ALLOC: NativeQueue<DepthZoneEventPayload>[16] - next-frame depth-zone event lane prevents same-frame reentrant dispatch - owner: DepthZoneEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(DepthZoneEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
            }
        }

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)
            where T : unmanaged
        {
            if (!queue.IsCreated || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
            }
        }

        private static void PromoteNextFrameEventsIfFrontEmpty()
        {
            if (!_pendingEvents.IsCreated ||
                !_nextFrameEvents.IsCreated ||
                !_pendingEvents.IsEmpty() ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            NativeQueue<DepthZoneEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-105)]
    public sealed class DepthZoneDirector : MonoBehaviour, ISlowTickable, ILocalizationLanguageChangedListener
    {
        private const string DepthZoneDataRoot = "Assets/_Project/Data/Lore/DepthZones";

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Zones ───────────────────────────────────")]
        [Tooltip("Vse zony glubiny. Poryadok ne vazhen — sortiruyutsya po minDepth.")]
        [SerializeField] private DepthZoneProfile[] zones = new DepthZoneProfile[0];

        [Header("── References ──────────────────────────────")]
        [Tooltip("Sistema vyzhivaniya dlya chteniya glubiny.")]
        [SerializeField] private HectonSurvivalSystem survivalSystem;

        [Header("â”€â”€ Notification Cadence â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Minimum delay between depth-zone enter HUD messages. Prevents boundary spam and early-route noise.")]
        [SerializeField, Min(0f)] private float zoneNotificationCooldown = 18f;

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        public static DepthZoneDirector Instance => GlobalRegistry.DepthZone;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private DepthZoneProfile _currentZone;
        private bool _registered;
        private bool _serviceRegistered;
        private bool _hullWarningShown;
        private float _nextZoneNotificationTime;
        // COLD ALLOC: small per-zone message caches avoid string formatting in SlowTick transition path.
        private readonly DepthZoneProfile[] _cachedMessageZones = new DepthZoneProfile[32];
        private readonly string[] _cachedZoneEnterMessages = new string[32];
        private readonly string[] _cachedHullWarningMessages = new string[32];
        private readonly string[] _cachedZoneRouteCueMessages = new string[32];
        private int _cachedMessageCount;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public DepthZoneProfile CurrentZone => _currentZone;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            DepthZoneDirector registered = GlobalRegistry.DepthZone;
            if (Application.isPlaying && registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return;
            }

            RebuildZoneMessageCache();
        }

        private void OnEnable()
        {
            TryRegisterService();
            TryRegister();

            LocalizationEvents.RegisterLanguageListener(this);
            ResolveSurvivalSystem();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterService();

            LocalizationEvents.UnregisterLanguageListener(this);
            _nextZoneNotificationTime = 0f;
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterService();
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            if (survivalSystem == null && !ResolveSurvivalSystem())
                return;

            if (survivalSystem == null || zones == null || zones.Length == 0)
                return;

            float depth = survivalSystem.Depth;

            // Nahodim tekuschuyu zonu
            DepthZoneProfile newZone = FindZoneForDepth(depth);

            // Obnovlyaem QuestManager posle razresheniya tekuschey authored zone context.
            QuestManager questManager = GlobalRegistry.Quest;
            if (questManager != null)
            {
                questManager.UpdateDepthContext(
                    depth,
                    newZone != null ? newZone.ZoneHash : 0u,
                    newZone != null && newZone.isThermal);
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
                DepthZoneEvents.RaiseZoneExited(oldZone);

            if (newZone != null)
            {
                DepthZoneEvents.RaiseZoneEntered(newZone);

                // Registriruem discovery
                if (!string.IsNullOrEmpty(newZone.discoveryId))
                    NarrativeEvents.RaiseDiscoveryMade(newZone.discoveryId);

                // HUD uvedomlenie
                if (ShouldPublishZoneEnterNotification())
                {
                    NotificationEvents.PushInfo(GetZoneEnterMessage(newZone));
                    _nextZoneNotificationTime = Time.unscaledTime + Mathf.Max(0f, zoneNotificationCooldown);
                }

                LogZoneEntered(newZone.DisplayNameOrFallback, depth);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

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

        private void CheckHullWarning(DepthZoneProfile zone)
        {
            if (zone == null || _hullWarningShown) return;

            SuitUpgradeManager upgradeManager = Hecton8.Core.GlobalRegistry.SuitUpgrades;
            if (upgradeManager == null) return;

            if (upgradeManager.CurrentHullTier < zone.requiredHullTier)
            {
                _hullWarningShown = true;
                NotificationEvents.PushWarning(GetHullWarningMessage(zone));
            }
        }

        private bool ShouldPublishZoneEnterNotification()
        {
            if (Time.unscaledTime < _nextZoneNotificationTime)
                return false;

            FirstHourDirector firstHourDirector = Hecton8.Core.GlobalRegistry.FirstHour;
            if (firstHourDirector == null)
                return true;

            return firstHourDirector.IsMilestoneComplete(FirstHourMilestone.Orientation);
        }

        private bool ResolveSurvivalSystem()
        {
            if (survivalSystem != null)
                return true;

            if (!BootstrapState.TryGetCurrentPlayerTransform(out Transform playerTransform) ||
                playerTransform == null)
            {
                return false;
            }

            return playerTransform.TryGetComponent(out survivalSystem);
        }

        private void RebuildZoneMessageCache()
        {
            _cachedMessageCount = 0;

            if (zones == null || zones.Length == 0)
                return;

            int maxCacheCount = Mathf.Min(zones.Length, _cachedMessageZones.Length);
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            for (int i = 0; i < maxCacheCount; i++)
            {
                DepthZoneProfile zone = zones[i];
                if (zone == null)
                    continue;

                zone.RebuildCache();
                string fallbackUnknown = manager != null
                    ? manager.GetOrFallback(manager.CurrentLanguage, LocalizationKeys.DEPTH_ZONE_UNKNOWN, "UNKNOWN ZONE")
                    : "UNKNOWN ZONE";
                string resolvedDisplayName = string.IsNullOrWhiteSpace(zone.DisplayNameOrFallback)
                    ? fallbackUnknown
                    : zone.DisplayNameOrFallback;
                string uppercaseZoneLabel = resolvedDisplayName.ToUpperInvariant();
                string zoneEnterLabel = manager != null
                    ? manager.GetFormatted(LocalizationKeys.DEPTH_ZONE_ENTER, uppercaseZoneLabel)
                    : "ZONE: " + uppercaseZoneLabel;
                string zoneRouteCue = ResolveZoneRouteCue(zone);
                _cachedMessageZones[_cachedMessageCount] = zone;
                _cachedZoneEnterMessages[_cachedMessageCount] = string.IsNullOrWhiteSpace(zone.cachedHudLabel)
                    ? zoneEnterLabel
                    : zone.cachedHudLabel;
                _cachedZoneRouteCueMessages[_cachedMessageCount] = string.IsNullOrWhiteSpace(zoneRouteCue)
                    ? _cachedZoneEnterMessages[_cachedMessageCount]
                    : _cachedZoneEnterMessages[_cachedMessageCount] + " — " + zoneRouteCue;
                _cachedHullWarningMessages[_cachedMessageCount] = manager != null
                    ? manager.GetFormatted(LocalizationKeys.DEPTH_ZONE_HULL_WARNING, zone.requiredHullTier)
                    : "WARNING: SUIT HULL IS NOT RATED FOR THIS DEPTH. TIER " + zone.requiredHullTier + ".";
                _cachedMessageCount++;
            }
        }

        private string GetZoneEnterMessage(DepthZoneProfile zone)
        {
            for (int i = 0; i < _cachedMessageCount; i++)
            {
                if (_cachedMessageZones[i] == zone)
                    return _cachedZoneRouteCueMessages[i];
            }

            return ResolveZoneEnterFallback(ResolveUnknownZoneLabel());
        }

        private string GetHullWarningMessage(DepthZoneProfile zone)
        {
            for (int i = 0; i < _cachedMessageCount; i++)
            {
                if (_cachedMessageZones[i] == zone)
                    return _cachedHullWarningMessages[i];
            }

            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null
                ? manager.GetFormatted(LocalizationKeys.DEPTH_ZONE_HULL_WARNING, zone != null ? zone.requiredHullTier : 0)
                : "WARNING: SUIT HULL IS NOT RATED FOR THIS DEPTH.";
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogZoneEntered(string zoneDisplayName, float depth)
        {
            UnityEngine.Debug.Log($"[DepthZone] Entered: {zoneDisplayName} (depth: {depth:F0}m)");
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildZoneMessageCache();
        }

        private static string ResolveUnknownZoneLabel()
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, LocalizationKeys.DEPTH_ZONE_UNKNOWN, "UNKNOWN ZONE")
                : "UNKNOWN ZONE";
        }

        private static string ResolveZoneEnterFallback(string zoneLabel)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null
                ? manager.GetFormatted(LocalizationKeys.DEPTH_ZONE_ENTER, zoneLabel)
                : "ZONE: " + zoneLabel;
        }

        private static string ResolveZoneRouteCue(DepthZoneProfile zone)
        {
            if (zone == null)
                return null;

            if (!string.IsNullOrWhiteSpace(zone.DescriptionOrFallback))
                return zone.DescriptionOrFallback.ToUpperInvariant();

            if (zone.isThermal)
                return "THERMAL WATER DISTORTS COLOR AND RANGE. TRUST YOUR RETURN LINE, NOT THE GLOW.";

            if (zone.hasCaves)
                return "CAVES CUT READABILITY. HOLD A CLEAN EXIT VECTOR BEFORE YOU COMMIT.";

            if (zone.dangerLevel >= 0.75f)
                return "HIGH-PRESSURE WATER. ROUTE MEMORY MATTERS MORE THAN GREED HERE.";

            if (zone.dangerLevel >= 0.45f)
                return "VISIBILITY FALLS FAST. KEEP THE SAFER SILHOUETTE IN MEMORY.";

            return "READ THE SHELVES, NOT THE NOISE. SAFE WATER IS FOR RESET, NOT FORWARD PROGRESS.";
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registered = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            _registered = false;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            DepthZoneDirector registered = GlobalRegistry.DepthZone;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterDepthZoneRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.DepthZone, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            if (ReferenceEquals(GlobalRegistry.DepthZone, this))
                GlobalRegistry.UnregisterDepthZoneRuntime(this);

            _serviceRegistered = false;
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
