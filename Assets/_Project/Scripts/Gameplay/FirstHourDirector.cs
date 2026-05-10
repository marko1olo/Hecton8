// ============================================================================
// HECTON-8 — FirstHourDirector.cs
// Rezhissura pervogo chasa igry.
//
// LOR (lor1 — Psihologicheskiy arc pervyh dvuh chasov):
//   Minuta 0-5:    Dezorientatsiya → Orientatsiya
//   Minuta 5-15:   Lyubopytstvo bez straha (melkovode bezopasno)
//   Minuta 15-25:  Pervaya trevoga (ruka iz-pod oblomka, gul snizu)
//   Minuta 25-40:  Kompetentnost (pervyy kraft)
//   Minuta 40-50:  Udar po uverennosti (TEN — bolshaya, bystraya, sleva)
//   Minuta 50-70:  Ostorozhnost (igrok dvigaetsya inache)
//   Minuta 70-90:  Malenkaya pobeda (nashel modul)
//   Minuta 90-120: Predvkushenie (gul priblizhaetsya)
//
// MEHANIKA:
//   • Otslezhivaet vremya sessii i progress.
//   • Publikuet sobytiya dlya Director AI i narrativnyh sistem.
//   • Odnorazovye sobytiya (ne povtoryayutsya posle pervogo raza).
//   • ISaveable: sohranyaet progress pervogo chasa.
//
// ZERO GC:
//   • Bitovaya maska dlya otslezhivaniya vypolnennyh sobytiy.
//   • ISlowTickable.
// ============================================================================

using System;
using System.Runtime.InteropServices;
using Hecton8.AtlasSignal;
using Hecton8.Bootstrap;
using Hecton8.Crafting;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.Interaction;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton.Localization;
using Hecton8.Narrative;
using Hecton8.Quest;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    public enum FirstHourMilestone
    {
        Orientation     = 0,   // Min 0-5: orientatsiya
        FirstAnxiety    = 1,   // Min 15-25: pervaya trevoga (gul)
        FirstCraft      = 2,   // Min 25-40: pervyy kraft
        TheShadow       = 3,   // Min 40-50: TEN
        FirstModule     = 4,   // Min 70-90: pervyy modul kolonii
        HumCloser       = 5    // Min 90-120: gul priblizhaetsya
    }

    public static class FirstHourEvents
    {
        private const int ListenerCapacity = 8;
        private const int PendingEventCapacity = 16;
        private const uint EventOverflowWarningHash = 0x46484F46u;
        private const uint ListenerRejectedWarningHash = 0x4648524Au;
        private const uint ListenerExceptionWarningHash = 0x46484558u;
        private const uint EventContextHash = 0x46484D53u;
        private const uint ListenerContextHash = 0x46484C53u;

        // COLD ALLOC: RegistryBucket<IFirstHourEventListener>[8] - first-hour milestone listeners drained by SystemDispatcher LateUpdate - owner: FirstHourEvents
        private static readonly RegistryBucket<IFirstHourEventListener> _listeners = new RegistryBucket<IFirstHourEventListener>(ListenerCapacity);
        private static readonly IFirstHourEventListener[] _deferredRegisterListeners = new IFirstHourEventListener[ListenerCapacity];
        private static readonly IFirstHourEventListener[] _deferredUnregisterListeners = new IFirstHourEventListener[ListenerCapacity];
        private static NativeQueue<FirstHourEventPayload> _pendingEvents;
        private static NativeQueue<FirstHourEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _droppedEventCount;
        private static int _droppedListenerRegistrationCount;
        private static int _listenerExceptionCount;
        private static int _lastEventOverflowTelemetryFrame = -1;
        private static int _lastListenerRejectedTelemetryFrame = -1;
        private static int _lastListenerExceptionTelemetryFrame = -1;
        private static bool _isDispatching;

        /// <summary>
        /// Number of queued first-hour milestone events awaiting LateUpdate dispatch.
        /// </summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(FirstHourEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(FirstHourEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            Array.Clear(_deferredRegisterListeners, 0, _deferredRegisterCount);
            Array.Clear(_deferredUnregisterListeners, 0, _deferredUnregisterCount);
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _deferredRegisterCount = 0;
            _deferredUnregisterCount = 0;
            _droppedEventCount = 0;
            _droppedListenerRegistrationCount = 0;
            _listenerExceptionCount = 0;
            _lastEventOverflowTelemetryFrame = -1;
            _lastListenerRejectedTelemetryFrame = -1;
            _lastListenerExceptionTelemetryFrame = -1;
            _isDispatching = false;
        }

        /// <summary>
        /// Registers a listener for deferred first-hour milestone events.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Register(IFirstHourEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (_isDispatching)
            {
                QueueDeferredRegister(listener);
                return;
            }

            RegisterImmediate(listener);
        }

        /// <summary>
        /// Unregisters a listener from deferred first-hour milestone events.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Unregister(IFirstHourEventListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatching)
            {
                QueueDeferredUnregister(listener);
                return;
            }

            if (!_listeners.TryUnregister(listener))
                return;

            if (_listeners.Count <= 0)
                DropQueuedEvents();
        }

        /// <summary>
        /// Enqueues a first-hour milestone event.
        /// </summary>
        /// <param name="milestone">Reached milestone.</param>
        public static void RaiseMilestone(FirstHourMilestone milestone)
        {
            if (_listeners.Count <= 0)
                return;

            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportEventOverflow();
                return;
            }

            FirstHourEventPayload payload = new FirstHourEventPayload
            {
                Milestone = (byte)milestone,
                Reserved0 = 0,
                Reserved1 = 0,
                Reserved2 = 0
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

        /// <summary>
        /// Flushes queued first-hour milestone events to registered listeners.
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

                if (!_pendingEvents.TryDequeue(out FirstHourEventPayload payload))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                IFirstHourEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IFirstHourEventListener listener = rawArray[i];
                        if (listener == null || IsDeferredUnregisterPending(listener))
                            continue;

                        DispatchToListener(listener, in payload);
                    }
                }
                finally
                {
                    _isDispatching = false;
                    ApplyDeferredListenerMutations();
                }
            }

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<FirstHourEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<FirstHourEventPayload>[16] — deferred first-hour milestone lane flushed by SystemDispatcher LateUpdate — owner: FirstHourEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(FirstHourEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<FirstHourEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<FirstHourEventPayload>[16] — next-frame first-hour milestone lane prevents same-frame reentrant dispatch — owner: FirstHourEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(FirstHourEvents),
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

        private static void DispatchToListener(IFirstHourEventListener listener, in FirstHourEventPayload payload)
        {
            try
            {
                listener.OnFirstHourMilestoneReached(in payload);
            }
            catch (Exception exception)
            {
                ReportListenerDispatchException();
                LogListenerDispatchException(exception);
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogListenerDispatchException(Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogException(exception);
#endif
        }

        private static void QueueDeferredRegister(IFirstHourEventListener listener)
        {
            if (_listeners.Contains(listener))
            {
                CancelDeferredUnregister(listener);
                return;
            }

            if (IsDeferredRegisterPending(listener))
                return;

            if (_deferredRegisterCount >= ListenerCapacity)
            {
                ReportListenerRegistrationRejected();
                return;
            }

            _deferredRegisterListeners[_deferredRegisterCount++] = listener;
        }

        private static void QueueDeferredUnregister(IFirstHourEventListener listener)
        {
            if (CancelDeferredRegister(listener))
                return;

            if (!_listeners.Contains(listener) || IsDeferredUnregisterPending(listener))
                return;

            if (_deferredUnregisterCount >= ListenerCapacity)
            {
                ReportListenerRegistrationRejected();
                return;
            }

            _deferredUnregisterListeners[_deferredUnregisterCount++] = listener;
        }

        private static bool CancelDeferredRegister(IFirstHourEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (!ReferenceEquals(_deferredRegisterListeners[i], listener))
                    continue;

                _deferredRegisterCount--;
                _deferredRegisterListeners[i] = _deferredRegisterListeners[_deferredRegisterCount];
                _deferredRegisterListeners[_deferredRegisterCount] = null;
                return true;
            }

            return false;
        }

        private static void CancelDeferredUnregister(IFirstHourEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (!ReferenceEquals(_deferredUnregisterListeners[i], listener))
                    continue;

                _deferredUnregisterCount--;
                _deferredUnregisterListeners[i] = _deferredUnregisterListeners[_deferredUnregisterCount];
                _deferredUnregisterListeners[_deferredUnregisterCount] = null;
                return;
            }
        }

        private static bool IsDeferredRegisterPending(IFirstHourEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i], listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(IFirstHourEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (ReferenceEquals(_deferredUnregisterListeners[i], listener))
                    return true;
            }

            return false;
        }

        private static void ApplyDeferredListenerMutations()
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                IFirstHourEventListener listener = _deferredUnregisterListeners[i];
                _deferredUnregisterListeners[i] = null;
                if (listener != null)
                    _listeners.TryUnregister(listener);
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                IFirstHourEventListener listener = _deferredRegisterListeners[i];
                _deferredRegisterListeners[i] = null;
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterCount = 0;

            if (_listeners.Count <= 0)
                DropQueuedEvents();
        }

        private static void RegisterImmediate(IFirstHourEventListener listener)
        {
            if (_listeners.Contains(listener))
                return;

            if (!_listeners.TryRegister(listener))
                ReportListenerRegistrationRejected();
        }

        private static void ReportEventOverflow()
        {
            _droppedEventCount++;
            int frame = Time.frameCount;
            if (_lastEventOverflowTelemetryFrame == frame)
                return;

            _lastEventOverflowTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                EventOverflowWarningHash,
                EventContextHash,
                Mathf.Max(1, _droppedEventCount));
        }

        private static void ReportListenerRegistrationRejected()
        {
            _droppedListenerRegistrationCount++;
            int frame = Time.frameCount;
            if (_lastListenerRejectedTelemetryFrame == frame)
                return;

            _lastListenerRejectedTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                ListenerRejectedWarningHash,
                ListenerContextHash,
                Mathf.Max(1, _droppedListenerRegistrationCount));
        }

        private static void ReportListenerDispatchException()
        {
            _listenerExceptionCount++;
            int frame = Time.frameCount;
            if (_lastListenerExceptionTelemetryFrame == frame)
                return;

            _lastListenerExceptionTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                ListenerExceptionWarningHash,
                ListenerContextHash,
                Mathf.Max(1, _listenerExceptionCount));
        }

        private static void DropQueuedEvents()
        {
            if (_pendingEvents.IsCreated)
            {
                while (_pendingEvents.TryDequeue(out _))
                {
                }
            }

            if (_nextFrameEvents.IsCreated)
            {
                while (_nextFrameEvents.TryDequeue(out _))
                {
                }
            }

            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
        }

        private static void DrainWithoutDispatch()
        {
            if (!_pendingEvents.IsCreated)
                return;

            if (!DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
                return;

            if (_pendingEvents.IsEmpty())
                PromoteNextFrameEventsIfFrontEmpty();

            if (_pendingEventCount > 0 &&
                !DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
            {
                return;
            }

            if (_nextFrameEvents.IsCreated)
                DrainQueueWithoutDispatch(ref _nextFrameEvents, ref _nextFrameEventCount);
        }

        private static bool DrainQueueWithoutDispatch(
            ref NativeQueue<FirstHourEventPayload> queue,
            ref int pendingCount)
        {
            int scanBudget = pendingCount > 0 ? pendingCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !queue.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!queue.TryDequeue(out _))
                    break;

                if (pendingCount > 0)
                    pendingCount--;
            }

            if (queue.IsEmpty())
                pendingCount = 0;

            return true;
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

            NativeQueue<FirstHourEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }
    }

    /// <summary>
    /// Unmanaged first-hour milestone event payload.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct FirstHourEventPayload
    {
        public byte Milestone;
        public byte Reserved0;
        public byte Reserved1;
        public byte Reserved2;
    }

    /// <summary>
    /// Listener contract for first-hour milestone events.
    /// </summary>
    public interface IFirstHourEventListener
    {
        /// <summary>
        /// Consumes one queue-drained first-hour milestone event.
        /// </summary>
        /// <param name="payload">Unmanaged milestone payload.</param>
        void OnFirstHourMilestoneReached(in FirstHourEventPayload payload);
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-65)]
    public sealed class FirstHourDirector : MonoBehaviour, ISaveable, ISlowTickable, IQuestEventListener, IAudioLogEventListener, INarrativeEventListener, IScanEventListener, ICraftingEventListener, IInteractionEventListener
    {
        [Flags]
        private enum GuidanceStateFlags
        {
            FirstModuleHintIssued = 1 << 0,
            FirstResourceReminderIssued = 1 << 1,
            FirstDepthReminderIssued = 1 << 2,
            FirstModuleReminderIssued = 1 << 3,
            StarterResourcesZoneHintIssued = 1 << 4,
            StarterFabricationFallbackHintIssued = 1 << 5,
            StarterBackslideGuidanceIssued = 1 << 6,
            FirstReturnLoreHintIssued = 1 << 7,
            DeeperRouteZoneHintIssued = 1 << 8,
            ModuleRouteHintIssued = 1 << 9,
            HasLoreRouteContact = 1 << 10
        }

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Timing (seconds) ────────────────────────")]
        [SerializeField] private float orientationTime   = 300f;   // 5 min
        [SerializeField] private float shadowTime        = 2400f;  // 40 min
        [SerializeField] private float firstModuleTime   = 4200f;  // 70 min

        [Header("── Shadow Trigger ──────────────────────────")]
        [Tooltip("Minimalnaya glubina dlya poyavleniya teni (metry).")]
        [SerializeField] private float shadowMinDepth = 30f;

        [Header("── Early Goal Hooks ─────────────────────────")]
        [Tooltip("Quest that represents successful arrival/orientation.")]
        [SerializeField] private string arrivalQuestId = "quest_arrival";

        [Tooltip("Quest that should become the next clear early-game material goal.")]
        [SerializeField] private string firstResourceQuestId = "quest_copper_sample";

        [Tooltip("Item ID that proves the first resource goal was already solved in an older save.")]
        [SerializeField] private string firstResourceItemId = "Data_Copper";

        [Tooltip("Quest that should take over once the player secures the first core material.")]
        [SerializeField] private string firstDepthQuestId = "quest_first_breath";

        [Tooltip("Narrative discovery that counts as a real ruined-colony/module contact.")]
        [SerializeField] private string firstModuleZoneDiscoveryId = "zone_drowned_factories";

        [Header("── Retention Nudges ─────────────────────────")]
        [Tooltip("When to remind the player about the first core resource if they are still drifting.")]
        [SerializeField] private float firstResourceReminderTime = 480f;

        [Tooltip("When to remind the player that the next real step is to go deeper.")]
        [SerializeField] private float firstDepthReminderTime = 1080f;

        [Tooltip("When to remind the player that shallow safety is no longer the real progression route and the next meaningful contact is a module or ruin.")]
        [SerializeField] private float firstModuleReminderTime = 2100f;

        [Header("── Soft Guidance ───────────────────────────")]
        [Tooltip("Minimum delay between contextual onboarding nudges.")]
        [SerializeField] private float contextualGuidanceCooldown = 24f;

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private float _sessionTime;
        private int   _completedMilestones; // bitovaya maska
        private bool  _registered;
        private bool  _serviceRegistered;
        private bool  _firstModuleHintIssued;
        private bool  _firstResourceReminderIssued;
        private bool  _firstDepthReminderIssued;
        private bool  _firstModuleReminderIssued;
        private bool  _starterResourcesZoneHintIssued;
        private bool  _starterFabricationFallbackHintIssued;
        private bool  _starterBackslideGuidanceIssued;
        private bool  _firstReturnLoreHintIssued;
        private bool  _deeperRouteZoneHintIssued;
        private bool  _moduleRouteHintIssued;
        private bool  _hasLoreRouteContact;
        private float _nextContextualGuidanceTime;
        private WorldZoneDirector _worldZoneDirector;
        private BiomeMatrixDirector _biomeMatrixDirector;
        private WorldZoneAnchor _lastObservedZone;
        private bool _lastContextResourceCompleted;
        private bool _lastContextDepthCompleted;
        private bool _lastContextLoreContact;
        private HectonSurvivalSystem _survivalSystem;
        private uint _firstModuleZoneDiscoveryHash;
        private uint _arrivalQuestHash;
        private uint _firstResourceQuestHash;
        private uint _firstDepthQuestHash;
        private int _firstResourceItemHash;
        private bool _firstResourceIsCopper;

        private const float MinEarnedOrientationTime = 75f;
        private const string DataCopperItemId = "Data_Copper";
        private const string ShadowEventDiscoveryId = "first_hour_shadow_event";
        private const string FirstColonyModuleDiscoveryId = "first_colony_module_spotted";
        private static readonly int _dataCopperItemHash = LocHash.Compute(DataCopperItemId);
        private static readonly uint _shadowEventDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(ShadowEventDiscoveryId);
        private static readonly uint _firstColonyModuleDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(FirstColonyModuleDiscoveryId);
        private const string MsgResourceShelfRead =
            "HOLD THE READABLE EDGE. THE FIRST COPPER NORMALLY SITS LOWER AND OFF TO THE SIDE OF THE SAFEST SHELF.";
        private const string MsgFabricationFallback =
            "THE NODE BUYS BREATHING ROOM, NOT ANSWERS. MOVE OUT AGAIN - THE STRONG EARLY EXIT SITS A LITTLE DEEPER.";
        private const string MsgReturnLoreRelay =
            "SERVICE NODES MAY STILL HOLD LOGS AND MARKERS. CHECK TERMINALS AND SIDE RACKS, NOT JUST RESOURCES.";
        private const string MsgDeeperRouteRead =
            "LOWER DOWN, ROUTE MATTERS MORE THAN GREED. KEEP THE EXIT SILHOUETTE AND A CALM AIR POCKET IN MIND.";
        private const string MsgModuleRouteRead =
            "NOW LOOK FOR A TRACE, NOT LOOSE SCRAP. RUINS, MODULES, AND SERVICE HALTS WILL GIVE YOU A REAL VECTOR.";
        private const string MsgStarterBackslideRead =
            "THE SHALLOWS ONLY BUY BREATHING ROOM NOW, NOT PROGRESS. REGROUP AND RETURN TO THE DEEPER ROUTE.";

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        public int SavePriority => 13;
        public int LoadPriority => 13;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public float SessionTime => _sessionTime;
        public bool IsFirstHourComplete => IsMilestoneComplete(FirstHourMilestone.HumCloser);

        public bool IsMilestoneComplete(FirstHourMilestone m)
            => (_completedMilestones & (1 << (int)m)) != 0;

        /// <summary>
        /// Registers a confirmed service-relay route contact for first-hour pacing.
        /// </summary>
        public void RegisterServiceRelayRouteContact()
        {
            _hasLoreRouteContact = true;
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            RefreshCachedHashes();
        }

        private void OnEnable()
        {
            if (!TryRegisterService())
                return;

            TryRegister();

            if (Hecton8.Core.GlobalRegistry.SaveRuntime != null)
                Hecton8.Core.GlobalRegistry.SaveRuntime.Register(this);

            ResolveSurvivalSystem();
            ResolveWorldContext(force: true);
            SynchronizeContextFromRuntimeSystems();

            CraftingEvents.Register(this);
            NarrativeEvents.Register(this);
            QuestEvents.Register(this);
            ScanEvents.Register(this);
            InteractionEvents.Register(this);
            AudioLogEvents.Register(this);
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterService();

            if (Hecton8.Core.GlobalRegistry.SaveRuntime != null)
                Hecton8.Core.GlobalRegistry.SaveRuntime.Unregister(this);

            CraftingEvents.Unregister(this);
            NarrativeEvents.Unregister(this);
            QuestEvents.Unregister(this);
            ScanEvents.Unregister(this);
            InteractionEvents.Unregister(this);
            AudioLogEvents.Unregister(this);

            _lastObservedZone = null;
            _nextContextualGuidanceTime = 0f;
            _lastContextResourceCompleted = false;
            _lastContextDepthCompleted = false;
            _lastContextLoreContact = false;
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterService();
        }

        private void Start()
        {
            if (!TryRegisterService())
                return;

            TryRegister();
            Hecton8.Core.GlobalRegistry.SaveRuntime?.Register(this);
            RefreshCachedHashes();
            ResolveSurvivalSystem();
            ResolveWorldContext(force: true);
            SynchronizeContextFromRuntimeSystems();
        }

        private void TryRegister()
        {
            if (_registered)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
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

        private bool TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return true;

            FirstHourDirector registeredRuntime = GlobalRegistry.FirstHour;
            if (registeredRuntime != null && !ReferenceEquals(registeredRuntime, this))
            {
                Destroy(gameObject);
                return false;
            }

            GlobalRegistry.RegisterFirstHourRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.FirstHour, this);
            return _serviceRegistered;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            if (ReferenceEquals(GlobalRegistry.FirstHour, this))
                GlobalRegistry.UnregisterFirstHourRuntime(this);

            _serviceRegistered = false;
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            if (IsFirstHourComplete) return;

            _sessionTime += 0.5f;
            ResolveSurvivalSystem();
            ResolveWorldContext();

            float depth = _survivalSystem != null ? _survivalSystem.Depth : 0f;
            WorldZoneAnchor currentZone = _worldZoneDirector != null ? _worldZoneDirector.CurrentZone : null;
            int currentDepthTier = _biomeMatrixDirector != null ? _biomeMatrixDirector.CurrentDepthTier : 1;
            int atlasRevealStage = GetCurrentAtlasRevealStage();

            CheckMilestone(FirstHourMilestone.Orientation,
                _sessionTime >= orientationTime || IsOrientationEarned(currentZone));
            CheckMilestone(
                FirstHourMilestone.FirstAnxiety,
                ShouldTriggerFirstAnxiety(atlasRevealStage));

            // Ten — tolko esli igrok pod vodoy na nuzhnoy glubine
            CheckMilestone(FirstHourMilestone.TheShadow,
                _sessionTime >= shadowTime && depth >= shadowMinDepth);

            if (!_firstModuleHintIssued &&
                !_moduleRouteHintIssued &&
                !_firstModuleReminderIssued &&
                !IsMilestoneComplete(FirstHourMilestone.FirstModule) &&
                _sessionTime >= firstModuleTime &&
                currentDepthTier > 1 &&
                GlobalRegistry.Quest != null &&
                GlobalRegistry.Quest.IsCompleted(_firstDepthQuestHash))
            {
                _firstModuleHintIssued = true;
                _firstModuleReminderIssued = true;
                NotificationEvents.PushInfo(ResolveLocalized(
                    LocalizationKeys.FIRST_HOUR_MODULE_SCAN_HINT,
                    "SCAN RUINS AND MODULES. SOMETHING INTACT IS STILL DOWN HERE."));
            }

            TryIssueRetentionNudges();
            TryIssueContextualGuidance();

            CheckMilestone(
                FirstHourMilestone.HumCloser,
                ShouldTriggerHumCloser(atlasRevealStage));
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private void CheckMilestone(FirstHourMilestone milestone, bool condition)
        {
            if (!condition) return;
            if (IsMilestoneComplete(milestone)) return;

            _completedMilestones |= (1 << (int)milestone);
            TriggerMilestone(milestone);
        }

        private void TriggerMilestone(FirstHourMilestone milestone)
        {
            FirstHourEvents.RaiseMilestone(milestone);

            switch (milestone)
            {
                case FirstHourMilestone.Orientation:
                    CompleteQuest(_arrivalQuestHash);
                    ActivateQuest(_firstResourceQuestHash);
                    TryAdvanceFirstResourceGoalFromRuntimeInventory();
                    break;

                case FirstHourMilestone.TheShadow:
                    // TEN — bolshaya, bystraya, sleva
                    // Director AI poluchaet narrative bonus (snizhenie tension posle straha)
                    NarrativeEvents.RaiseDiscoveryMade(_shadowEventDiscoveryHash);
                    break;

                case FirstHourMilestone.FirstModule:
                    NarrativeEvents.RaiseDiscoveryMade(_firstColonyModuleDiscoveryHash);
                    break;

            }

            LogMilestoneTriggered(milestone, _sessionTime);
        }

        private void HandleCraftCompleted(ItemData resultItem)
        {
            if (resultItem == null)
                return;

            CheckMilestone(FirstHourMilestone.FirstCraft,
                !IsMilestoneComplete(FirstHourMilestone.FirstCraft));
        }

        public void OnNarrativeEvent(in NarrativeEventPayload payload)
        {
            if ((NarrativeEventType)payload.EventType != NarrativeEventType.DiscoveryMade)
                return;

            if (!IsMilestoneComplete(FirstHourMilestone.FirstModule) &&
                payload.DiscoveryHash != 0u &&
                payload.DiscoveryHash == _firstModuleZoneDiscoveryHash)
            {
                CheckMilestone(FirstHourMilestone.FirstModule, true);
            }

            EmergencyServiceRelayDirector relayDirector = Hecton8.Core.GlobalRegistry.EmergencyRelay;
            if (relayDirector != null && relayDirector.IsRelayDiscoveryHash(payload.DiscoveryHash))
                _hasLoreRouteContact = true;
        }

        public void OnScanEvent(in ScanEventPayload payload)
        {
            if ((ScanEventType)payload.EventType != ScanEventType.EntryDiscovered ||
                IsMilestoneComplete(FirstHourMilestone.FirstModule))
            {
                return;
            }

            if ((ScanEntryKind)payload.EntryKind == ScanEntryKind.Module)
                CheckMilestone(FirstHourMilestone.FirstModule, true);
        }

        public void OnQuestEvent(in QuestEventPayload payload)
        {
            if ((QuestEventType)payload.EventType != QuestEventType.Completed)
                return;

            if (payload.QuestHashID == _firstDepthQuestHash)
            {
                _firstDepthReminderIssued = true;
                return;
            }

            if (payload.QuestHashID != _firstResourceQuestHash)
                return;

            _firstResourceReminderIssued = true;
            ActivateQuest(_firstDepthQuestHash);
        }

        public void OnAudioLogEvent(in AudioLogEventPayload payload)
        {
            if (payload.Type == AudioLogEventType.Discovered && payload.LogHash != 0u)
                _hasLoreRouteContact = true;
        }

        public void OnCraftingEvent(in CraftingEventPayload payload)
        {
            if ((CraftingEventType)payload.EventType != CraftingEventType.CraftCompleted)
                return;

            if (!CraftingEvents.TryResolveItem(in payload, out ItemData resultItem))
                return;

            HandleCraftCompleted(resultItem);
        }

        public void OnInteractionEvent(in InteractionEventPayload payload)
        {
            if ((InteractionEventType)payload.EventType != InteractionEventType.ItemCollected)
                return;

            if (!InteractionEvents.TryResolveItem(in payload, out ItemData item))
                return;

            InteractionEvents.TryResolveInteractor(in payload, out Transform interactor);
            HandleItemCollected(item, payload.Quantity, interactor);
        }

        private void HandleItemCollected(ItemData item, int quantity, Transform interactor)
        {
            if (!IsMilestoneComplete(FirstHourMilestone.Orientation) ||
                item == null ||
                !item.MatchesPersistentHash(_firstResourceItemHash))
            {
                return;
            }

            CompleteQuest(_firstResourceQuestHash);
            _firstResourceReminderIssued = true;
            ActivateQuest(_firstDepthQuestHash);
            _firstDepthReminderIssued = false;
        }

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        private bool ResolveSurvivalSystem()
        {
            if (_survivalSystem != null)
                return true;

            if (!GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) ||
                playerTransform == null)
            {
                return false;
            }

            return playerTransform.TryGetComponent(out _survivalSystem);
        }

        private void ResolveWorldContext(bool force = false)
        {
            if (force || _worldZoneDirector == null)
                _worldZoneDirector = WorldZoneDirector.ActiveRuntimeInstance;

            if (force || _biomeMatrixDirector == null)
                _biomeMatrixDirector = BiomeMatrixDirector.ActiveRuntimeInstance;
        }

        private void SynchronizeContextFromRuntimeSystems()
        {
            AudioLogSystem audioLogSystem = Hecton8.Core.GlobalRegistry.AudioLogs;
            if (audioLogSystem != null && audioLogSystem.DiscoveredCount > 0)
                _hasLoreRouteContact = true;

            EmergencyServiceRelayDirector relayDirector = Hecton8.Core.GlobalRegistry.EmergencyRelay;
            if (relayDirector != null && relayDirector.HasDiscoveredRelayInDrivenChain())
                _hasLoreRouteContact = true;
        }

        private bool IsOrientationEarned(WorldZoneAnchor currentZone)
        {
            if (_sessionTime < MinEarnedOrientationTime || currentZone == null)
                return false;

            if (currentZone.RouteCritical)
                return true;

            switch (currentZone.Kind)
            {
                case WorldZoneAnchor.ZoneKind.Resources:
                case WorldZoneAnchor.ZoneKind.Fabrication:
                case WorldZoneAnchor.ZoneKind.Navigation:
                case WorldZoneAnchor.ZoneKind.Progression:
                case WorldZoneAnchor.ZoneKind.Service:
                    return true;
                default:
                    return false;
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMilestoneTriggered(FirstHourMilestone milestone, float sessionTime)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[FirstHour] Milestone: {milestone} (t={sessionTime:F0}s)");
#endif
        }

        public void PopulateSaveData(SaveData data)
        {
            if (data == null) return;
            data.firstHourSessionTime = _sessionTime;
            data.firstHourMilestones  = _completedMilestones;
            data.firstHourGuidanceFlags = BuildGuidanceStateFlags();
        }

        public void LoadFromSaveData(SaveData data)
        {
            if (data == null) return;
            _sessionTime          = data.firstHourSessionTime;
            _completedMilestones  = data.firstHourMilestones;
            ApplyGuidanceStateFlags(data.firstHourGuidanceFlags);
            _firstModuleHintIssued |= _sessionTime >= firstModuleTime ||
                                      IsMilestoneComplete(FirstHourMilestone.FirstModule);
            _nextContextualGuidanceTime = 0f;
            _lastObservedZone = null;
            _lastContextResourceCompleted = false;
            _lastContextDepthCompleted = false;
            _lastContextLoreContact = false;
            SynchronizeContextFromRuntimeSystems();
            SynchronizeAtlasMilestonesFromRuntime();
            SynchronizeEarlyQuestState();
            SynchronizeFirstResourceQuestFromSaveData(data);
        }

        private int BuildGuidanceStateFlags()
        {
            GuidanceStateFlags flags = 0;
            if (_firstModuleHintIssued)
                flags |= GuidanceStateFlags.FirstModuleHintIssued;
            if (_firstResourceReminderIssued)
                flags |= GuidanceStateFlags.FirstResourceReminderIssued;
            if (_firstDepthReminderIssued)
                flags |= GuidanceStateFlags.FirstDepthReminderIssued;
            if (_firstModuleReminderIssued)
                flags |= GuidanceStateFlags.FirstModuleReminderIssued;
            if (_starterResourcesZoneHintIssued)
                flags |= GuidanceStateFlags.StarterResourcesZoneHintIssued;
            if (_starterFabricationFallbackHintIssued)
                flags |= GuidanceStateFlags.StarterFabricationFallbackHintIssued;
            if (_starterBackslideGuidanceIssued)
                flags |= GuidanceStateFlags.StarterBackslideGuidanceIssued;
            if (_firstReturnLoreHintIssued)
                flags |= GuidanceStateFlags.FirstReturnLoreHintIssued;
            if (_deeperRouteZoneHintIssued)
                flags |= GuidanceStateFlags.DeeperRouteZoneHintIssued;
            if (_moduleRouteHintIssued)
                flags |= GuidanceStateFlags.ModuleRouteHintIssued;
            if (_hasLoreRouteContact)
                flags |= GuidanceStateFlags.HasLoreRouteContact;

            return (int)flags;
        }

        private void ApplyGuidanceStateFlags(int persistedFlags)
        {
            GuidanceStateFlags flags = (GuidanceStateFlags)persistedFlags;
            _firstModuleHintIssued = (flags & GuidanceStateFlags.FirstModuleHintIssued) != 0;
            _firstResourceReminderIssued = (flags & GuidanceStateFlags.FirstResourceReminderIssued) != 0;
            _firstDepthReminderIssued = (flags & GuidanceStateFlags.FirstDepthReminderIssued) != 0;
            _firstModuleReminderIssued = (flags & GuidanceStateFlags.FirstModuleReminderIssued) != 0;
            _starterResourcesZoneHintIssued = (flags & GuidanceStateFlags.StarterResourcesZoneHintIssued) != 0;
            _starterFabricationFallbackHintIssued = (flags & GuidanceStateFlags.StarterFabricationFallbackHintIssued) != 0;
            _starterBackslideGuidanceIssued = (flags & GuidanceStateFlags.StarterBackslideGuidanceIssued) != 0;
            _firstReturnLoreHintIssued = (flags & GuidanceStateFlags.FirstReturnLoreHintIssued) != 0;
            _deeperRouteZoneHintIssued = (flags & GuidanceStateFlags.DeeperRouteZoneHintIssued) != 0;
            _moduleRouteHintIssued = (flags & GuidanceStateFlags.ModuleRouteHintIssued) != 0;
            _hasLoreRouteContact = (flags & GuidanceStateFlags.HasLoreRouteContact) != 0;
        }

        private void ActivateQuest(uint questHash)
        {
            if (questHash == 0u)
                return;

            QuestManager questManager = GlobalRegistry.Quest;
            if (questManager == null)
                return;

            if (!questManager.IsActive(questHash) && !questManager.IsCompleted(questHash))
                questManager.ActivateQuest(questHash);
        }

        private void CompleteQuest(uint questHash)
        {
            if (questHash == 0u)
                return;

            QuestManager questManager = GlobalRegistry.Quest;
            if (questManager == null)
                return;

            if (!questManager.IsActive(questHash) && !questManager.IsCompleted(questHash))
                questManager.ActivateQuest(questHash);

            if (questManager.IsActive(questHash))
                questManager.CompleteQuest(questHash);
        }

        private void SynchronizeEarlyQuestState()
        {
            if (!IsMilestoneComplete(FirstHourMilestone.Orientation))
                return;

            CompleteQuest(_arrivalQuestHash);
            ActivateQuest(_firstResourceQuestHash);
            TryAdvanceFirstResourceGoalFromRuntimeInventory();

            QuestManager questManager = GlobalRegistry.Quest;
            if (questManager != null && questManager.IsCompleted(_firstResourceQuestHash))
            {
                _firstResourceReminderIssued = true;
                ActivateQuest(_firstDepthQuestHash);
            }

            if (questManager != null && questManager.IsCompleted(_firstDepthQuestHash))
                _firstDepthReminderIssued = true;

            if (_hasLoreRouteContact)
                _firstReturnLoreHintIssued = true;

            if (IsMilestoneComplete(FirstHourMilestone.FirstModule))
                _firstModuleReminderIssued = true;
        }

        private void SynchronizeAtlasMilestonesFromRuntime()
        {
            int atlasRevealStage = GetCurrentAtlasRevealStage();
            if (atlasRevealStage >= 1)
                _completedMilestones |= 1 << (int)FirstHourMilestone.FirstAnxiety;

            if (atlasRevealStage >= 2)
                _completedMilestones |= 1 << (int)FirstHourMilestone.HumCloser;
        }

        private int GetCurrentAtlasRevealStage()
        {
            AtlasSignalSystem atlasSignalSystem = Hecton8.Core.GlobalRegistry.AtlasSignal;
            return atlasSignalSystem != null ? atlasSignalSystem.CurrentRevealStage : 0;
        }

        private static bool ShouldTriggerFirstAnxiety(int atlasRevealStage)
        {
            return atlasRevealStage >= 1;
        }

        private static bool ShouldTriggerHumCloser(int atlasRevealStage)
        {
            return atlasRevealStage >= 2;
        }

        private void SynchronizeFirstResourceQuestFromSaveData(SaveData data)
        {
            if (data == null ||
                !IsMilestoneComplete(FirstHourMilestone.Orientation) ||
                !SaveInventoryContainsItem(data.inventory, _firstResourceItemHash))
            {
                return;
            }

            CompleteQuest(_firstResourceQuestHash);
            _firstResourceReminderIssued = true;
            ActivateQuest(_firstDepthQuestHash);
            _firstDepthReminderIssued = false;
        }

        private static bool SaveInventoryContainsItem(InventoryDTO inventory, int itemHashId)
        {
            if (itemHashId == 0 ||
                inventory.itemHashIds == null ||
                inventory.cellCount <= 0)
            {
                return false;
            }

            int cellCount = math.min(inventory.cellCount, inventory.itemHashIds.Length);
            for (int i = 0; i < cellCount; i++)
            {
                if (inventory.itemHashIds[i] == itemHashId)
                    return true;
            }

            return false;
        }

        private void TryAdvanceFirstResourceGoalFromRuntimeInventory()
        {
            if (_firstResourceItemHash == 0 ||
                !TryGetRuntimeInventory(out PlayerInventory inventory) ||
                inventory == null)
            {
                return;
            }

            InventoryGrid grid = inventory.Grid;
            if (grid == null)
                return;

            int columns = grid.Columns;
            int rows = grid.Rows;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int itemHashId = inventory.GetItemHashAt(x, y);
                    if (itemHashId != _firstResourceItemHash)
                        continue;

                    CompleteQuest(_firstResourceQuestHash);
                    _firstResourceReminderIssued = true;
                    ActivateQuest(_firstDepthQuestHash);
                    _firstDepthReminderIssued = false;
                    return;
                }
            }
        }

        private void TryIssueRetentionNudges()
        {
            if (!IsMilestoneComplete(FirstHourMilestone.Orientation))
                return;

            QuestManager questManager = GlobalRegistry.Quest;
            if (questManager == null)
                return;

            if (!_firstResourceReminderIssued &&
                !questManager.IsCompleted(_firstResourceQuestHash) &&
                _sessionTime >= firstResourceReminderTime)
            {
                _firstResourceReminderIssued = true;
                string reminderMessage = _firstResourceIsCopper
                    ? ResolveLocalized(
                        LocalizationKeys.FIRST_HOUR_RESOURCE_REMINDER_COPPER,
                        "LOOK FOR COPPER IN THE DEBRIS AND AROUND THE ROCKS. WITHOUT IT, YOU DO NOT MOVE THE CHAIN FORWARD.")
                    : ResolveLocalized(
                        LocalizationKeys.FIRST_HOUR_RESOURCE_REMINDER_GENERIC,
                        "LOOK FOR THE FIRST CORE MATERIAL IN THE DEBRIS AND ALONG READABLE ROCK FACES. WITHOUT IT, THE CHAIN STOPS HERE.");
                NotificationEvents.PushInfo(reminderMessage);
            }

            if (!_firstDepthReminderIssued &&
                questManager.IsCompleted(_firstResourceQuestHash) &&
                questManager.IsActive(_firstDepthQuestHash) &&
                !questManager.IsCompleted(_firstDepthQuestHash) &&
                _sessionTime >= firstDepthReminderTime)
            {
                _firstDepthReminderIssued = true;
                NotificationEvents.PushInfo(ResolveLocalized(
                    LocalizationKeys.FIRST_HOUR_DEPTH_REMINDER,
                    "THE FIRST REAL FIND IS LOWER. GO DEEPER, BUT DO NOT LOSE THE WAY OUT."));
            }

            if (!_firstModuleReminderIssued &&
                questManager.IsCompleted(_firstDepthQuestHash) &&
                !IsMilestoneComplete(FirstHourMilestone.FirstModule) &&
                _sessionTime >= firstModuleReminderTime)
            {
                _firstModuleReminderIssued = true;

                WorldZoneAnchor currentZone = _worldZoneDirector != null ? _worldZoneDirector.CurrentZone : null;
                HectonBiomeMatrixProfile currentBiome = ResolveCurrentBiomeProfile(currentZone);
                NotificationEvents.PushInfo(ResolveModuleRouteGuidanceMessage(currentZone, currentBiome));
            }
        }

        private void TryIssueContextualGuidance()
        {
            if (!IsMilestoneComplete(FirstHourMilestone.Orientation))
                return;

            if (Time.unscaledTime < _nextContextualGuidanceTime)
                return;

            QuestManager questManager = GlobalRegistry.Quest;
            if (questManager == null)
                return;

            SynchronizeContextFromRuntimeSystems();

            if (TryIssueServiceRelayGuidance())
                return;

            WorldZoneAnchor currentZone = _worldZoneDirector != null ? _worldZoneDirector.CurrentZone : null;
            if (currentZone == null)
                return;

            int currentDepthTier = _biomeMatrixDirector != null ? _biomeMatrixDirector.CurrentDepthTier : 1;
            HectonBiomeMatrixProfile currentBiome = ResolveCurrentBiomeProfile(currentZone);
            bool resourceCompleted = questManager.IsCompleted(_firstResourceQuestHash);
            bool depthCompleted = questManager.IsCompleted(_firstDepthQuestHash);
            bool loreRouteContact = _hasLoreRouteContact;

            bool zoneChanged = !ReferenceEquals(currentZone, _lastObservedZone);
            bool stageChanged =
                resourceCompleted != _lastContextResourceCompleted ||
                depthCompleted != _lastContextDepthCompleted ||
                loreRouteContact != _lastContextLoreContact;

            _lastObservedZone = currentZone;
            _lastContextResourceCompleted = resourceCompleted;
            _lastContextDepthCompleted = depthCompleted;
            _lastContextLoreContact = loreRouteContact;

            if (!zoneChanged && !stageChanged)
                return;

            if (TryIssueEarlyResourceZoneGuidance(questManager, currentZone, currentBiome))
                return;

            if (TryIssueFabricationReturnGuidance(questManager, currentZone, currentBiome))
                return;

            if (TryIssueStarterBackslideGuidance(questManager, currentZone, currentBiome, currentDepthTier))
                return;

            if (TryIssueDeeperRouteGuidance(questManager, currentZone, currentBiome, currentDepthTier))
                return;

            TryIssueModuleRouteGuidance(questManager, currentZone, currentBiome, currentDepthTier);
        }

        private bool TryIssueServiceRelayGuidance()
        {
            EmergencyServiceRelayDirector relayDirector = Hecton8.Core.GlobalRegistry.EmergencyRelay;
            if (relayDirector == null ||
                !relayDirector.TryBuildContextualGuidanceMessage(out string relayMessage))
            {
                return false;
            }

            PublishContextualInfo(relayMessage);
            return true;
        }

        private bool TryIssueEarlyResourceZoneGuidance(
            QuestManager questManager,
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome)
        {
            if (_starterResourcesZoneHintIssued ||
                questManager.IsCompleted(_firstResourceQuestHash) ||
                currentZone.Kind != WorldZoneAnchor.ZoneKind.Resources)
            {
                return false;
            }

            _starterResourcesZoneHintIssued = true;
            PublishContextualInfo(ResolveResourceZoneGuidanceMessage(currentZone, currentBiome));
            return true;
        }

        private bool TryIssueFabricationReturnGuidance(
            QuestManager questManager,
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome)
        {
            if (currentZone.Kind != WorldZoneAnchor.ZoneKind.Fabrication)
                return false;

            if (!_starterFabricationFallbackHintIssued &&
                !questManager.IsCompleted(_firstResourceQuestHash))
            {
                _starterFabricationFallbackHintIssued = true;
                PublishContextualInfo(ResolveFabricationFallbackMessage(currentZone, currentBiome));
                return true;
            }

            if (!_firstReturnLoreHintIssued &&
                questManager.IsCompleted(_firstResourceQuestHash) &&
                !questManager.IsCompleted(_firstDepthQuestHash) &&
                !_hasLoreRouteContact)
            {
                _firstReturnLoreHintIssued = true;
                PublishContextualInfo(ResolveReturnLoreGuidanceMessage(currentZone, currentBiome));
                return true;
            }

            return false;
        }

        private bool TryIssueDeeperRouteGuidance(
            QuestManager questManager,
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome,
            int currentDepthTier)
        {
            if (_deeperRouteZoneHintIssued ||
                !questManager.IsCompleted(_firstResourceQuestHash) ||
                questManager.IsCompleted(_firstDepthQuestHash) ||
                currentDepthTier > 1)
            {
                return false;
            }

            if (currentZone.Kind != WorldZoneAnchor.ZoneKind.Navigation &&
                currentZone.Kind != WorldZoneAnchor.ZoneKind.Progression &&
                currentZone.Kind != WorldZoneAnchor.ZoneKind.Service)
            {
                return false;
            }

            _deeperRouteZoneHintIssued = true;
            PublishContextualInfo(ResolveDeeperRouteGuidanceMessage(currentZone, currentBiome));
            return true;
        }

        private bool TryIssueStarterBackslideGuidance(
            QuestManager questManager,
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome,
            int currentDepthTier)
        {
            if (_starterBackslideGuidanceIssued ||
                !questManager.IsCompleted(_firstDepthQuestHash) ||
                IsMilestoneComplete(FirstHourMilestone.FirstModule) ||
                currentZone == null)
            {
                return false;
            }

            bool inStarterSafetyPocket =
                currentZone.Tier == WorldZoneAnchor.ZoneTier.Starter &&
                (currentZone.Kind == WorldZoneAnchor.ZoneKind.Resources ||
                 currentZone.Kind == WorldZoneAnchor.ZoneKind.Fabrication ||
                 currentZone.Kind == WorldZoneAnchor.ZoneKind.Service);

            if (!inStarterSafetyPocket && currentDepthTier > 1)
                return false;

            _starterBackslideGuidanceIssued = true;
            PublishContextualInfo(ResolveStarterBackslideMessage(currentZone, currentBiome));
            return true;
        }

        private bool TryIssueModuleRouteGuidance(
            QuestManager questManager,
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome,
            int currentDepthTier)
        {
            if (_moduleRouteHintIssued ||
                !questManager.IsCompleted(_firstDepthQuestHash) ||
                IsMilestoneComplete(FirstHourMilestone.FirstModule) ||
                currentDepthTier <= 1)
            {
                return false;
            }

            if (currentZone.Kind != WorldZoneAnchor.ZoneKind.Navigation &&
                currentZone.Kind != WorldZoneAnchor.ZoneKind.Service &&
                currentZone.Kind != WorldZoneAnchor.ZoneKind.Progression &&
                currentZone.Kind != WorldZoneAnchor.ZoneKind.Combat)
            {
                return false;
            }

            _moduleRouteHintIssued = true;
            _firstModuleHintIssued = true;
            PublishContextualInfo(ResolveModuleRouteGuidanceMessage(currentZone, currentBiome));
            return true;
        }

        private void PublishContextualInfo(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            NotificationEvents.PushInfo(message);
            _nextContextualGuidanceTime = Time.unscaledTime + math.max(0f, contextualGuidanceCooldown);
        }

        private HectonBiomeMatrixProfile ResolveCurrentBiomeProfile(WorldZoneAnchor currentZone)
        {
            if (currentZone != null && currentZone.DominantMatrixBiome != null)
                return currentZone.DominantMatrixBiome;

            return _biomeMatrixDirector != null ? _biomeMatrixDirector.CurrentProfile : null;
        }

        private string ResolveResourceZoneGuidanceMessage(
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome)
        {
            WorldZoneProfile zoneProfile = currentZone != null ? currentZone.Profile : null;
            WorldMotivationProfile motivation = zoneProfile != null ? zoneProfile.motivationProfile : null;
            WorldSandboxAttractionProfile sandbox = zoneProfile != null ? zoneProfile.sandboxAttractionProfile : null;
            WorldExpeditionLoopProfile expedition = zoneProfile != null ? zoneProfile.expeditionLoopProfile : null;

            return SelectFirstNonEmpty(
                motivation != null ? motivation.resourceNeed : null,
                sandbox != null ? sandbox.ambientValue : null,
                expedition != null ? expedition.softProgressionPull : null,
                currentBiome != null ? currentBiome.commonRewardHook : null,
                currentBiome != null ? currentBiome.landmarkGuidance : null,
                ResolveLocalized(LocalizationKeys.FIRST_HOUR_RESOURCE_SHELF_READ, MsgResourceShelfRead));
        }

        private string ResolveFabricationFallbackMessage(
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome)
        {
            WorldZoneProfile zoneProfile = currentZone != null ? currentZone.Profile : null;
            WorldSandboxAttractionProfile sandbox = zoneProfile != null ? zoneProfile.sandboxAttractionProfile : null;
            WorldExpeditionLoopProfile expedition = zoneProfile != null ? zoneProfile.expeditionLoopProfile : null;

            return SelectFirstNonEmpty(
                expedition != null ? expedition.reliefBeat : null,
                expedition != null ? expedition.playerPromise : null,
                sandbox != null ? sandbox.shelterRead : null,
                currentBiome != null ? currentBiome.safePocketIdentity : null,
                null,
                ResolveLocalized(LocalizationKeys.FIRST_HOUR_FABRICATION_FALLBACK, MsgFabricationFallback));
        }

        private string ResolveReturnLoreGuidanceMessage(
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome)
        {
            WorldZoneProfile zoneProfile = currentZone != null ? currentZone.Profile : null;
            WorldMotivationProfile motivation = zoneProfile != null ? zoneProfile.motivationProfile : null;
            WorldSandboxAttractionProfile sandbox = zoneProfile != null ? zoneProfile.sandboxAttractionProfile : null;

            return SelectFirstNonEmpty(
                motivation != null ? motivation.storyPull : null,
                sandbox != null ? sandbox.storyLure : null,
                currentBiome != null ? currentBiome.rareRewardHook : null,
                null,
                null,
                ResolveLocalized(LocalizationKeys.FIRST_HOUR_RETURN_LORE_RELAY, MsgReturnLoreRelay));
        }

        private string ResolveDeeperRouteGuidanceMessage(
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome)
        {
            WorldZoneProfile zoneProfile = currentZone != null ? currentZone.Profile : null;
            WorldSandboxAttractionProfile sandbox = zoneProfile != null ? zoneProfile.sandboxAttractionProfile : null;
            WorldExpeditionLoopProfile expedition = zoneProfile != null ? zoneProfile.expeditionLoopProfile : null;

            return SelectFirstNonEmpty(
                sandbox != null ? sandbox.deepLure : null,
                expedition != null ? expedition.softProgressionPull : null,
                currentBiome != null ? currentBiome.landmarkGuidance : null,
                currentBiome != null ? currentBiome.rareRewardHook : null,
                null,
                ResolveLocalized(LocalizationKeys.FIRST_HOUR_DEEPER_ROUTE_READ, MsgDeeperRouteRead));
        }

        private string ResolveModuleRouteGuidanceMessage(
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome)
        {
            WorldZoneProfile zoneProfile = currentZone != null ? currentZone.Profile : null;
            WorldMotivationProfile motivation = zoneProfile != null ? zoneProfile.motivationProfile : null;
            WorldSandboxAttractionProfile sandbox = zoneProfile != null ? zoneProfile.sandboxAttractionProfile : null;

            return SelectFirstNonEmpty(
                motivation != null ? motivation.storyPull : null,
                motivation != null ? motivation.curiosityPull : null,
                sandbox != null ? sandbox.storyLure : null,
                currentBiome != null ? currentBiome.landmarkGuidance : null,
                null,
                ResolveLocalized(LocalizationKeys.FIRST_HOUR_MODULE_ROUTE_READ, MsgModuleRouteRead));
        }

        private string ResolveStarterBackslideMessage(
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome)
        {
            WorldZoneProfile zoneProfile = currentZone != null ? currentZone.Profile : null;
            WorldExpeditionLoopProfile expedition = zoneProfile != null ? zoneProfile.expeditionLoopProfile : null;
            WorldMotivationProfile motivation = zoneProfile != null ? zoneProfile.motivationProfile : null;
            WorldSandboxAttractionProfile sandbox = zoneProfile != null ? zoneProfile.sandboxAttractionProfile : null;

            return SelectFirstNonEmpty(
                expedition != null ? expedition.playerPromise : null,
                expedition != null ? expedition.softProgressionPull : null,
                motivation != null ? motivation.storyPull : null,
                sandbox != null ? sandbox.deepLure : null,
                currentBiome != null ? currentBiome.landmarkGuidance : null,
                ResolveLocalized(LocalizationKeys.FIRST_HOUR_STARTER_BACKSLIDE_READ, MsgStarterBackslideRead));
        }

        private static string SelectFirstNonEmpty(
            string optionA,
            string optionB,
            string optionC,
            string optionD,
            string optionE,
            string fallback)
        {
            if (!string.IsNullOrWhiteSpace(optionA))
                return optionA;

            if (!string.IsNullOrWhiteSpace(optionB))
                return optionB;

            if (!string.IsNullOrWhiteSpace(optionC))
                return optionC;

            if (!string.IsNullOrWhiteSpace(optionD))
                return optionD;

            if (!string.IsNullOrWhiteSpace(optionE))
                return optionE;

            return fallback;
        }

        private static bool TryGetRuntimeInventory(out PlayerInventory inventory)
        {
            inventory = null;

            if (!GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) ||
                playerTransform == null)
            {
                return false;
            }

            inventory = playerTransform.GetComponent<PlayerInventory>();
            if (inventory != null)
                return true;

            inventory = ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.Inventory != null) ? Hecton8.Core.GlobalRegistry.Player.Inventory : playerTransform.GetComponent<PlayerInventory>());
            return inventory != null;
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager localization = Hecton8.Core.GlobalRegistry.Localization;
            return localization != null ? localization.GetOrFallback(localization.CurrentLanguage, key, fallback) : fallback;
        }

        private void RefreshCachedHashes()
        {
            _firstModuleZoneDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(firstModuleZoneDiscoveryId);
            _arrivalQuestHash = QuestFlagHashKernel.ComputeStableHash(arrivalQuestId);
            _firstResourceQuestHash = QuestFlagHashKernel.ComputeStableHash(firstResourceQuestId);
            _firstDepthQuestHash = QuestFlagHashKernel.ComputeStableHash(firstDepthQuestId);
            _firstResourceItemHash = string.IsNullOrWhiteSpace(firstResourceItemId)
                ? 0
                : LocHash.Compute(firstResourceItemId);
            _firstResourceIsCopper = _firstResourceItemHash != 0 && _firstResourceItemHash == _dataCopperItemHash;
        }
    }
}
