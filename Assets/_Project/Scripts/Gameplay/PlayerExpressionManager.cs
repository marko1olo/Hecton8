using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Unity.Collections;
using UnityEngine;
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
#endif

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Player-expression event discriminator for <see cref="PlayerExpressionEventPayload"/>.
    /// </summary>
    public enum PlayerExpressionEventType : byte
    {
        ProfileChanged = 0
    }

    /// <summary>
    /// Unmanaged player-expression payload carried by the native event queue.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct PlayerExpressionEventPayload
    {
        [FieldOffset(0)] public int ReferenceSlot;
        [FieldOffset(4)] public ushort EventType;
        [FieldOffset(6)] public ushort Reserved;
    }

    /// <summary>
    /// Listener contract for player-expression events drained from <see cref="SystemDispatcher"/>.
    /// </summary>
    public interface IPlayerExpressionEventListener
    {
        /// <summary>
        /// Consumes one queue-drained player-expression event.
        /// </summary>
        /// <param name="payload">Unmanaged event payload.</param>
        void OnPlayerExpressionEvent(in PlayerExpressionEventPayload payload);
    }

    /// <summary>
    /// Queue-backed bus for player expression changes.
    /// </summary>
    public static class PlayerExpressionEvents
    {
        private const int ListenerCapacity = 8;
        private const int PendingEventCapacity = 8;
        private const int ReferenceSlotCapacity = 8;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;

        private struct PlayerExpressionReferenceSlot
        {
            public PlayerExpressionProfile Profile;

            public void Clear()
            {
                Profile = null;
            }
        }

        private struct ListenerSlot
        {
            public IPlayerExpressionEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct PlayerExpressionListenerRegistry
        {
            private readonly ListenerSlot[] _slots;
            private int _count;

            public PlayerExpressionListenerRegistry(int capacity)
            {
                _slots = new ListenerSlot[capacity];
                _count = 0;
            }

            public int Count => _count;

            public void Clear()
            {
                for (int i = 0; i < _count; i++)
                    _slots[i].Clear();

                _count = 0;
            }

            public bool Contains(IPlayerExpressionEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_slots[i].Listener, listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(IPlayerExpressionEventListener listener)
            {
                if (listener == null || _count >= _slots.Length)
                    return false;

                _slots[_count++].Listener = listener;
                return true;
            }

            public void Unregister(IPlayerExpressionEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (!ReferenceEquals(_slots[i].Listener, listener))
                        continue;

                    _count--;
                    _slots[i] = _slots[_count];
                    _slots[_count].Clear();
                    return;
                }
            }

            public IPlayerExpressionEventListener GetAt(int index)
            {
                return (uint)index < (uint)_count ? _slots[index].Listener : null;
            }
        }

        // COLD ALLOC: ListenerSlot[8] - player-expression listeners drained by SystemDispatcher LateUpdate - owner: PlayerExpressionEvents
        private static PlayerExpressionListenerRegistry _listeners = new PlayerExpressionListenerRegistry(ListenerCapacity);
        // COLD ALLOC: PlayerExpressionReferenceSlot[8] - managed profile sidecar for unmanaged payloads - owner: PlayerExpressionEvents
        private static readonly PlayerExpressionReferenceSlot[] _referenceSlots = new PlayerExpressionReferenceSlot[ReferenceSlotCapacity];
        // COLD ALLOC: bool[8] - reference slot occupancy map prevents overwrite before deferred flush - owner: PlayerExpressionEvents
        private static readonly bool[] _referenceSlotOccupied = new bool[ReferenceSlotCapacity];
        private static NativeQueue<PlayerExpressionEventPayload> _pendingEvents;
        private static NativeQueue<PlayerExpressionEventPayload> _nextFrameEvents;
        private static int _referenceWriteIndex;
        private static int _referencePendingCount;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _droppedEventCount;
        private static int _droppedReferenceSlotCount;
        private static bool _isDispatching;

        /// <summary>
        /// Number of queued player-expression events awaiting LateUpdate dispatch.
        /// </summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;
        public static int DroppedEventCount => _droppedEventCount;
        public static int DroppedReferenceSlotCount => _droppedReferenceSlotCount;

        /// <summary>
        /// Registers a listener for deferred player-expression events.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Register(IPlayerExpressionEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (!_listeners.Contains(listener))
                _listeners.TryRegister(listener);
        }

        /// <summary>
        /// Unregisters a listener from deferred player-expression events.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Unregister(IPlayerExpressionEventListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        /// <summary>
        /// Flushes queued player-expression events to registered listeners.
        /// Called by <see cref="SystemDispatcher"/> from LateUpdate.
        /// </summary>
        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated || _listeners.Count <= 0)
            {
                DrainWithoutDispatch();
                return;
            }

            PromoteNextFrameEventsIfFrontEmpty();
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out PlayerExpressionEventPayload payload))
                {
                    _pendingEventCount = 0;
                    break;
                }

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IPlayerExpressionEventListener listener = _listeners.GetAt(i);
                        if (listener != null)
                            listener.OnPlayerExpressionEvent(in payload);
                    }
                }
                finally
                {
                    _isDispatching = false;
                }

                ReleaseReferenceSlot(payload.ReferenceSlot);
            }

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        /// <summary>
        /// Resolves the profile attached to an expression event payload.
        /// Valid only during listener dispatch.
        /// </summary>
        /// <param name="payload">Event payload.</param>
        /// <param name="profile">Resolved profile.</param>
        /// <returns>True when a profile reference is available.</returns>
        public static bool TryResolveProfile(in PlayerExpressionEventPayload payload, out PlayerExpressionProfile profile)
        {
            profile = null;
            if (!IsValidReferenceSlot(payload.ReferenceSlot))
                return false;

            profile = _referenceSlots[payload.ReferenceSlot].Profile;
            return profile != null;
        }

        [Obsolete("Use TryRaiseProfileChanged(PlayerExpressionProfile) so bounded enqueue refusal is visible.", true)]
        internal static void RaiseProfileChanged(PlayerExpressionProfile profile)
        {
            TryRaiseProfileChanged(profile);
        }

        internal static bool TryRaiseProfileChanged(PlayerExpressionProfile profile)
        {
            if (_listeners.Count <= 0)
                return false;

            if (!TryReserveReferenceSlot(out int referenceSlot))
            {
                _droppedReferenceSlotCount++;
                return false;
            }

            _referenceSlots[referenceSlot].Profile = profile;
            return Enqueue(new PlayerExpressionEventPayload
            {
                ReferenceSlot = referenceSlot,
                EventType = (ushort)PlayerExpressionEventType.ProfileChanged,
                Reserved = 0
            });
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(PlayerExpressionEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(PlayerExpressionEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            ClearReferenceSlots();
            _referenceWriteIndex = 0;
            _referencePendingCount = 0;
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _droppedEventCount = 0;
            _droppedReferenceSlotCount = 0;
            _isDispatching = false;
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<PlayerExpressionEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<PlayerExpressionEventPayload>[8] - deferred player-expression lane flushed by SystemDispatcher LateUpdate - owner: PlayerExpressionEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(PlayerExpressionEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<PlayerExpressionEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<PlayerExpressionEventPayload>[8] - next-frame player-expression lane prevents same-frame reentrant dispatch - owner: PlayerExpressionEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(PlayerExpressionEvents),
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

        private static bool Enqueue(in PlayerExpressionEventPayload payload)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                _droppedEventCount++;
                ReleaseReferenceSlot(payload.ReferenceSlot);
                return false;
            }

            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(payload);
                _nextFrameEventCount++;
                return true;
            }

            _pendingEvents.Enqueue(payload);
            _pendingEventCount++;
            return true;
        }

        private static bool TryReserveReferenceSlot(out int referenceSlot)
        {
            referenceSlot = -1;
            if (_referencePendingCount >= ReferenceSlotCapacity)
                return false;

            for (int probe = 0; probe < ReferenceSlotCapacity; probe++)
            {
                int candidateSlot = _referenceWriteIndex;
                _referenceWriteIndex++;
                if (_referenceWriteIndex >= ReferenceSlotCapacity)
                    _referenceWriteIndex = 0;

                if (_referenceSlotOccupied[candidateSlot])
                    continue;

                referenceSlot = candidateSlot;
                _referenceSlotOccupied[referenceSlot] = true;
                _referencePendingCount++;
                return true;
            }

            return false;
        }

        private static void ReleaseReferenceSlot(int referenceSlot)
        {
            if (!IsValidReferenceSlot(referenceSlot))
                return;

            if (!_referenceSlotOccupied[referenceSlot])
                return;

            _referenceSlots[referenceSlot].Clear();
            _referenceSlotOccupied[referenceSlot] = false;
            if (_referencePendingCount > 0)
                _referencePendingCount--;
        }

        private static bool IsValidReferenceSlot(int referenceSlot)
        {
            return (uint)referenceSlot < ReferenceSlotCapacity;
        }

        private static void DrainWithoutDispatch()
        {
            if (!DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
                return;

            if (_pendingEventCount <= 0)
            {
                PromoteNextFrameEventsIfFrontEmpty();
                if (!DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
                    return;
            }

            if (_nextFrameEvents.IsCreated)
                DrainQueueWithoutDispatch(ref _nextFrameEvents, ref _nextFrameEventCount);
        }

        private static bool DrainQueueWithoutDispatch(
            ref NativeQueue<PlayerExpressionEventPayload> queue,
            ref int pendingCount)
        {
            if (!queue.IsCreated)
                return true;

            int scanBudget = pendingCount > 0 ? pendingCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !queue.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!queue.TryDequeue(out PlayerExpressionEventPayload payload))
                {
                    pendingCount = 0;
                    break;
                }

                if (pendingCount > 0)
                    pendingCount--;

                ReleaseReferenceSlot(payload.ReferenceSlot);
            }

            if (queue.IsEmpty())
                pendingCount = 0;

            return true;
        }

        private static void PromoteNextFrameEventsIfFrontEmpty()
        {
            if (!_pendingEvents.IsCreated ||
                !_nextFrameEvents.IsCreated ||
                _pendingEventCount > 0 ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            NativeQueue<PlayerExpressionEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }

        private static void ClearReferenceSlots()
        {
            for (int i = 0; i < ReferenceSlotCapacity; i++)
            {
                _referenceSlots[i].Clear();
                _referenceSlotOccupied[i] = false;
            }
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-105)]
    [AddComponentMenu("Hecton8/Gameplay/Player Expression Manager")]
    public sealed class PlayerExpressionManager : MonoBehaviour, ISaveable, IGlobalRegistryHotSwapListener, IPlayerExpressionReadModel
    {
        private const string ProfileFolder = "Assets/_Project/Data/Customization/PlayerExpression";
        private const string DefaultIdentityName = "STANDARD";
        private const string DefaultIdentitySummary = "No authored player-expression profile is active.";

        private static PlayerExpressionProfile _activeProfile;
        private static SuitHUDProfile _activeHudProfileOverride;
        private static string _activeSuitLabelOverride;

        [Header("── References ─────────────────────────────────")]
        [Tooltip("Live tool manager used when syncing an identity to a recommended quick-slot kit.")]
        [SerializeField] private PlayerToolManager toolManager;

        [Tooltip("Live player movement owner used when syncing an identity to a recommended suit shell.")]
        [SerializeField] private HectonPlayerMovement playerMovement;

        [Tooltip("HUD notifier for user-facing profile switching messages.")]
        [SerializeField] private HUDNotification hudNotification;

        [Header("── Catalog ────────────────────────────────────")]
        [Tooltip("Default profile ID used for first boot or missing save data.")]
        [SerializeField] private string defaultProfileId = "expression.expedition.standard";

        [Tooltip("Authored player-expression catalog.")]
        [SerializeField] private PlayerExpressionProfile[] authoredProfiles = new PlayerExpressionProfile[0];

        [Header("── Behavior ───────────────────────────────────")]
        [Tooltip("Automatically apply the profile's recommended loadout when the identity changes.")]
        [SerializeField] private bool autoApplyRecommendedLoadoutOnSelection;

        [Tooltip("Development logging for profile apply/load behavior.")]
        [SerializeField] private bool verboseLogging;

        [Header("── Diagnostics ────────────────────────────────")]
        [SerializeField] private string _debugActiveProfileId;
        [SerializeField] private string _debugActiveProfileName;
        [SerializeField] private string _debugRecommendedSuitName;
        [SerializeField] private string _debugLiveSuitName;
        [SerializeField] private int _debugProfileCount;

        private bool _runtimeBindingsReady;
        private bool _pendingRecommendedSuitApply;
        private bool _serviceRegistered;
        private bool _hotSwapRegistered;
        private bool _saveRegistered;
        private ISaveService _saveService;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private FixedCharBuffer _notificationBuffer = new FixedCharBuffer(160); // COLD ALLOC: char[160] - identity HUD notification staging buffer - owner: PlayerExpressionManager

        /// <summary>Registry-owned instance for the active scene/runtime.</summary>
        private static PlayerExpressionManager s_activeRuntimeInstance;

        /// <summary>The currently active expression profile.</summary>
        public static PlayerExpressionProfile ActiveProfile => _activeProfile;

        /// <summary>HUD profile override consumed by live HUD systems.</summary>
        public static SuitHUDProfile ActiveHudProfileOverride => _activeHudProfileOverride;

        /// <summary>HUD suit-label override consumed by live HUD systems.</summary>
        public static string ActiveSuitLabelOverride => _activeSuitLabelOverride;

        /// <summary>Catalog size available for PDA/UI readback.</summary>
        public int ProfileCount => authoredProfiles != null ? authoredProfiles.Length : 0;

        /// <summary>Save priority for the expression profile state.</summary>
        public int SavePriority => 60;

        /// <summary>Load priority for the expression profile state.</summary>
        public int LoadPriority => 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _activeProfile = null;
            _activeHudProfileOverride = null;
            _activeSuitLabelOverride = null;
            s_activeRuntimeInstance = null;
        }

        private void Awake()
        {
            PlayerExpressionManager registered = s_activeRuntimeInstance ?? GlobalRegistry.PlayerExpression;
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return;
            }

            AutoResolveReferences();

#if UNITY_EDITOR
            AutoResolveCatalog();
#endif

            if (_activeProfile == null)
                ApplyProfileInternal(ResolveInitialProfileIndex(), false, false);

            SyncDiagnostics();
        }

        private void OnEnable()
        {
            CachePlayerRuntimeContext(GlobalRegistry.Player);
            AutoResolveReferences();
            TryRegisterService();
            TryRegisterHotSwapListener();
            RefreshColdRegistryDependencies();
            TryRegisterSaveOwner();
        }

        private void Start()
        {
            AutoResolveReferences();
            _runtimeBindingsReady = true;
            ApplyPendingRuntimeBindings();
            SyncDiagnostics();
        }

        private void OnDisable()
        {
            TryUnregisterSaveOwner();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            _playerRuntimeContext = null;
        }

        private void OnDestroy()
        {
            bool wasRegisteredOwner = ReferenceEquals(GlobalRegistry.PlayerExpression, this);
            TryUnregisterSaveOwner();
            TryUnregisterHotSwapListener();
            TryUnregisterService();

            if (wasRegisteredOwner)
            {
                _activeProfile = null;
                _activeHudProfileOverride = null;
                _activeSuitLabelOverride = null;
            }
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            PlayerExpressionManager registered = s_activeRuntimeInstance ?? GlobalRegistry.PlayerExpression;
            if (registered != null && registered != this)
                return;

            GlobalRegistry.RegisterPlayerExpressionRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.PlayerExpression, this);
            if (_serviceRegistered)
                s_activeRuntimeInstance = this;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterPlayerExpressionRuntime(this);
            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;
            _serviceRegistered = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            AutoResolveReferences();
            AutoResolveCatalog();
            SyncDiagnostics();
        }

        private void AutoResolveCatalog()
        {
            if (authoredProfiles != null && authoredProfiles.Length > 0)
            {
                bool hasNull = false;
                for (int i = 0; i < authoredProfiles.Length; i++)
                {
                    if (authoredProfiles[i] == null)
                    {
                        hasNull = true;
                        break;
                    }
                }

                if (!hasNull)
                    return;
            }

            string[] guids = AssetDatabase.FindAssets("t:PlayerExpressionProfile", new[] { ProfileFolder });
            if (guids == null || guids.Length == 0)
                return;

            List<PlayerExpressionProfile> profiles = new List<PlayerExpressionProfile>(guids.Length); // COLD ALLOC: List<PlayerExpressionProfile>[guids.Length] — editor catalog sync — owner: PlayerExpressionManager
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                PlayerExpressionProfile profile = AssetDatabase.LoadAssetAtPath<PlayerExpressionProfile>(path);
                if (profile != null)
                    profiles.Add(profile);
            }

            if (profiles.Count == 0)
                return;

            profiles.Sort(CompareProfiles);
            if (authoredProfiles == null || authoredProfiles.Length != profiles.Count)
                authoredProfiles = new PlayerExpressionProfile[profiles.Count];

            profiles.CopyTo(authoredProfiles);
            EditorUtility.SetDirty(this);
        }

        private static int CompareProfiles(PlayerExpressionProfile left, PlayerExpressionProfile right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;

            return string.CompareOrdinal(left.ProfileId, right.ProfileId);
        }
#endif

        /// <summary>Returns the currently active profile name.</summary>
        public string GetActiveProfileName()
        {
            return _activeProfile != null && !string.IsNullOrWhiteSpace(_activeProfile.DisplayName)
                ? _activeProfile.DisplayName
                : DefaultIdentityName;
        }

        /// <summary>Returns the currently active profile summary.</summary>
        public string GetActiveProfileSummary()
        {
            return _activeProfile != null && !string.IsNullOrWhiteSpace(_activeProfile.Summary)
                ? _activeProfile.Summary
                : DefaultIdentitySummary;
        }

        /// <summary>Returns the active profile's recommended loadout name, if any.</summary>
        public string GetActiveRecommendedLoadoutName()
        {
            return _activeProfile != null && _activeProfile.RecommendedLoadout != null
                ? _activeProfile.RecommendedLoadout.presetName
                : string.Empty;
        }

        /// <summary>Returns the active profile's recommended suit name, if any.</summary>
        public string GetActiveRecommendedSuitName()
        {
            if (_activeProfile == null || _activeProfile.RecommendedSuit == null)
                return string.Empty;

            return _activeProfile.RecommendedSuit.name.Replace('_', ' ');
        }

        /// <summary>Returns the currently applied live suit name, if any.</summary>
        public string GetLiveSuitName()
        {
            AutoResolveReferences();
            return playerMovement != null && playerMovement.CurrentSuit != null
                ? playerMovement.CurrentSuit.name.Replace('_', ' ')
                : string.Empty;
        }

        /// <summary>Returns true when the active profile's recommended suit is live on the player.</summary>
        public bool IsActiveRecommendedSuitApplied()
        {
            if (_activeProfile == null || _activeProfile.RecommendedSuit == null)
                return false;

            AutoResolveReferences();
            return playerMovement != null && ReferenceEquals(playerMovement.CurrentSuit, _activeProfile.RecommendedSuit);
        }

        /// <summary>Returns the index of the active profile in the authored catalog.</summary>
        public int GetActiveProfileIndex()
        {
            if (_activeProfile == null || authoredProfiles == null)
                return -1;

            for (int i = 0; i < authoredProfiles.Length; i++)
            {
                if (ReferenceEquals(authoredProfiles[i], _activeProfile))
                    return i;
            }

            return -1;
        }

        /// <summary>Returns the next valid profile index for PDA cycling.</summary>
        public int GetNextProfileIndex()
        {
            if (authoredProfiles == null || authoredProfiles.Length == 0)
                return -1;

            int activeIndex = GetActiveProfileIndex();
            if (activeIndex < 0)
                return FindFirstValidProfileIndex();

            for (int step = 1; step <= authoredProfiles.Length; step++)
            {
                int index = (activeIndex + step) % authoredProfiles.Length;
                if (authoredProfiles[index] != null)
                    return index;
            }

            return -1;
        }

        /// <summary>Returns the authored profile at the requested index.</summary>
        public PlayerExpressionProfile GetProfile(int index)
        {
            if (authoredProfiles == null || index < 0 || index >= authoredProfiles.Length)
                return null;

            return authoredProfiles[index];
        }

        public bool TryGetNextProfileDisplayName(out string displayName)
        {
            displayName = null;
            PlayerExpressionProfile profile = GetProfile(GetNextProfileIndex());
            if (profile == null)
                return false;

            displayName = profile.DisplayName;
            return !string.IsNullOrWhiteSpace(displayName);
        }

        /// <summary>Cycles to the next authored profile.</summary>
        public bool CycleNextProfile(bool applyRecommendedLoadout = false)
        {
            int nextIndex = GetNextProfileIndex();
            return ApplyProfileInternal(nextIndex, applyRecommendedLoadout, true);
        }

        /// <summary>Applies the requested profile from the authored catalog.</summary>
        public bool ApplyProfileAt(int index, bool applyRecommendedLoadout = false)
        {
            return ApplyProfileInternal(index, applyRecommendedLoadout, true);
        }

        /// <summary>Applies the active profile's recommended loadout, if authored.</summary>
        public bool ApplyRecommendedLoadoutForActiveProfile()
        {
            if (_activeProfile == null || _activeProfile.RecommendedLoadout == null || toolManager == null)
                return false;

            bool applied = toolManager.ApplyLoadoutPreset(_activeProfile.RecommendedLoadout, holsterFirst: true);
            if (!applied)
                return false;

            NotifyInfo("IDENTITY KIT APPLIED - ".AsSpan(), _activeProfile.RecommendedLoadout.presetName);
            return true;
        }

        /// <summary>Applies the active profile's recommended suit shell, if authored.</summary>
        public bool ApplyRecommendedSuitForActiveProfile()
        {
            if (_activeProfile == null)
                return false;

            bool applied = TryApplyRecommendedSuit(_activeProfile);
            if (!applied)
                return false;

            string suitName = GetActiveRecommendedSuitName();
            if (!string.IsNullOrWhiteSpace(suitName))
                NotifyInfo("SUIT SHELL APPLIED - ".AsSpan(), suitName);

            return true;
        }

        /// <summary>Writes the active identity selection into save data.</summary>
        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            data.playerExpressionProfileId = _activeProfile != null
                ? _activeProfile.ProfileId
                : string.Empty;
        }

        /// <summary>Restores the active identity selection from save data.</summary>
        public void LoadFromSaveData(SaveData data)
        {
            string requestedId = data != null ? data.playerExpressionProfileId : string.Empty;
            int profileIndex = ResolveProfileIndexForLoad(requestedId);
            ApplyProfileInternal(profileIndex, false, false);
        }

        private void AutoResolveReferences()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (toolManager == null && playerContext != null)
                toolManager = playerContext.ToolManager;

            if (playerMovement == null && playerContext != null)
                playerMovement = playerContext.PlayerMovement;

            if (hudNotification == null)
                HUDNotification.TryGetActive(out hudNotification);
        }

        private void CachePlayerRuntimeContext(IPlayerRuntimeContext playerContext)
        {
            _playerRuntimeContext = playerContext;
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

        private void RefreshColdRegistryDependencies()
        {
            _saveService = GlobalRegistry.Save;
        }

        private void TryRegisterSaveOwner()
        {
            if (_saveRegistered)
                return;

            ISaveService saveService = _saveService;
            if (saveService == null)
                return;

            saveService.Register(this);
            _saveRegistered = true;
        }

        private void TryUnregisterSaveOwner()
        {
            if (!_saveRegistered)
                return;

            _saveService?.Unregister(this);
            _saveRegistered = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    CachePlayerRuntimeContext(currentService as IPlayerRuntimeContext);
                    AutoResolveReferences();
                    ApplyPendingRuntimeBindings();
                    SyncDiagnostics();
                    break;
                case GlobalRegistryServiceSlot.Save:
                    if (_saveRegistered && previousService is ISaveService previousSave)
                        previousSave.Unregister(this);

                    _saveRegistered = false;
                    _saveService = currentService as ISaveService;

                    if (Application.isPlaying && isActiveAndEnabled)
                        TryRegisterSaveOwner();
                    break;
            }
        }

        private int ResolveInitialProfileIndex()
        {
            int configuredIndex = FindProfileIndexById(defaultProfileId);
            if (configuredIndex >= 0)
                return configuredIndex;

            return FindFirstValidProfileIndex();
        }

        private int ResolveProfileIndexForLoad(string requestedId)
        {
            int requestedIndex = FindProfileIndexById(requestedId);
            if (requestedIndex >= 0)
                return requestedIndex;

            return ResolveInitialProfileIndex();
        }

        private int FindFirstValidProfileIndex()
        {
            if (authoredProfiles == null)
                return -1;

            for (int i = 0; i < authoredProfiles.Length; i++)
            {
                if (authoredProfiles[i] != null)
                    return i;
            }

            return -1;
        }

        private int FindProfileIndexById(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId) || authoredProfiles == null)
                return -1;

            for (int i = 0; i < authoredProfiles.Length; i++)
            {
                PlayerExpressionProfile profile = authoredProfiles[i];
                if (profile == null || string.IsNullOrWhiteSpace(profile.ProfileId))
                    continue;

                if (string.Equals(profile.ProfileId, profileId, System.StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private bool ApplyProfileInternal(int index, bool applyRecommendedLoadout, bool userFacingNotification)
        {
            if (index < 0 || authoredProfiles == null || index >= authoredProfiles.Length)
                return false;

            PlayerExpressionProfile profile = authoredProfiles[index];
            if (profile == null)
                return false;

            _activeProfile = profile;
            _activeHudProfileOverride = profile.HudProfile;
            _activeSuitLabelOverride = string.IsNullOrWhiteSpace(profile.HudLabelOverride)
                ? string.Empty
                : profile.HudLabelOverride;

            if ((applyRecommendedLoadout || autoApplyRecommendedLoadoutOnSelection) &&
                profile.RecommendedLoadout != null &&
                toolManager != null)
            {
                toolManager.ApplyLoadoutPreset(profile.RecommendedLoadout, holsterFirst: true);
            }

            if (_runtimeBindingsReady)
                _pendingRecommendedSuitApply = !TryApplyRecommendedSuit(profile);
            else
                _pendingRecommendedSuitApply = profile.RecommendedSuit != null;

            SyncDiagnostics();
            PlayerExpressionEvents.TryRaiseProfileChanged(profile);

            if (userFacingNotification)
                NotifyInfo("SUIT IDENTITY ACTIVE - ".AsSpan(), profile.DisplayName);

            LogProfileApplied(profile.ProfileId, profile.DisplayName);
            return true;
        }

        private void SyncDiagnostics()
        {
            _debugActiveProfileId = _activeProfile != null ? _activeProfile.ProfileId : string.Empty;
            _debugActiveProfileName = _activeProfile != null ? _activeProfile.DisplayName : string.Empty;
            _debugRecommendedSuitName = GetActiveRecommendedSuitName();
            _debugLiveSuitName = GetLiveSuitName();
            _debugProfileCount = ProfileCount;
        }

        private void ApplyPendingRuntimeBindings()
        {
            if (!_runtimeBindingsReady || _activeProfile == null)
                return;

            if (_activeProfile.RecommendedSuit == null)
            {
                _pendingRecommendedSuitApply = false;
                SyncDiagnostics();
                return;
            }

            _pendingRecommendedSuitApply = !TryApplyRecommendedSuit(_activeProfile);
            SyncDiagnostics();
        }

        private bool TryApplyRecommendedSuit(PlayerExpressionProfile profile)
        {
            if (profile == null || profile.RecommendedSuit == null)
            {
                _pendingRecommendedSuitApply = false;
                return false;
            }

            AutoResolveReferences();
            if (playerMovement == null)
            {
                _pendingRecommendedSuitApply = true;
                return false;
            }

            if (!ReferenceEquals(playerMovement.CurrentSuit, profile.RecommendedSuit))
            {
                playerMovement.SetSuit(profile.RecommendedSuit);
                LogSuitApplied(profile.ProfileId, profile.RecommendedSuit.name);
            }

            _pendingRecommendedSuitApply = false;
            return true;
        }

        private void NotifyInfo(ReadOnlySpan<char> prefix, string value)
        {
            if (hudNotification == null)
                HUDNotification.TryGetActive(out hudNotification);

            _notificationBuffer.Clear();
            if (hudNotification != null &&
                _notificationBuffer.Append(prefix) &&
                AppendUpperOrDefault(ref _notificationBuffer, value))
            {
                hudNotification.ShowInfo(in _notificationBuffer);
            }
        }

        private static bool AppendUpperOrDefault(ref FixedCharBuffer buffer, string value)
        {
            ReadOnlySpan<char> source = string.IsNullOrWhiteSpace(value)
                ? DefaultIdentityName.AsSpan()
                : value.AsSpan();

            Span<char> single = stackalloc char[1];
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                if (c >= 'a' && c <= 'z')
                    c = (char)(c - 32);

                single[0] = c;
                if (!buffer.Append(single))
                    return false;
            }

            return true;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void LogProfileApplied(string profileId, string displayName)
        {
            if (!verboseLogging)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.Log($"[PlayerExpression] Active profile: {profileId} ({displayName})");
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void LogSuitApplied(string profileId, string suitName)
        {
            if (!verboseLogging)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.Log($"[PlayerExpression] Suit applied: {profileId} -> {suitName}");
#endif
        }
    }
}
