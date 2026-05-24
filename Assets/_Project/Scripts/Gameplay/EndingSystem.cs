// ============================================================================
// HECTON-8 — EndingSystem.cs
// Sistema kontsovok igry.
//
// LOR (lor1 — Final):
//   Igrok dobralsya do yadra Atlas-6 na -5000m.
//   Tri vybora — ni odin ne "pravilnyy". Eto nuar.
//
//   VYKLYuChIT ATLAS-6:
//     Signal prekraschaetsya. Korporatsiya pridet.
//     Terraformirovanie prodolzhitsya. Zhizn unichtozhena.
//     Igrok uletaet. Ekonomicheski logichno — moralno net.
//
//   OSTAVIT ATLAS-6:
//     Signal prodolzhaetsya. Korporatsiya ne pridet poka signal aktiven.
//     Zhizn zaschischena — vremenno. Signal kogda-nibud naydut i zaglushat.
//
//   USILIT SIGNAL:
//     Signal publichnyy — ves sektor slyshit.
//     Korporatsiyu ne ostanovit — no teper vse znayut.
//     Atlas-6 vyklyuchaetsya sam — zadacha vypolnena.
//     Igrok stanovitsya tem, kto raskryl taynu.
//
// ARHITEKTURA:
//   • Otslezhivaet usloviya aktivatsii (glubina + rasshifrovka signala).
//   • Publikuet sobytiya dlya vseh sistem pri vybore kontsovki.
//   • ISaveable: sohranyaet vybrannuyu kontsovku.
//   • Integriruetsya s Atlas6DirectiveSystem, QuestManager, NarrativeEvents.
//
// ZERO GC:
//   • Static events, enum state.
//   • Nikakih new/LINQ v hot path.
// ============================================================================

using System;
using Conditional = System.Diagnostics.ConditionalAttribute;
using System.Runtime.InteropServices;
using Hecton8.AtlasSignal;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Quest;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Hecton.Localization;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Gameplay
{
    public enum EndingChoice
    {
        None        = 0,
        ShutDown    = 1,   // Vyklyuchit Atlas-6
        Leave       = 2,   // Ostavit Atlas-6
        Amplify     = 3    // Usilit signal
    }

    /// <summary>
    /// Ending event discriminator for <see cref="EndingEventPayload"/>.
    /// </summary>
    public enum EndingEventType : byte
    {
        ConditionMet = 0,
        Chosen = 1,
        SequenceComplete = 2
    }

    /// <summary>
    /// Unmanaged ending event payload.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct EndingEventPayload
    {
        [FieldOffset(0)] public byte EventType;
        [FieldOffset(1)] public byte Choice;
        [FieldOffset(2)] public ushort Reserved;
        [FieldOffset(4)] private uint _pad0;
        [FieldOffset(8)] private ulong _pad1;
    }

    /// <summary>
    /// Listener contract for ending events drained from <see cref="SystemDispatcher"/>.
    /// </summary>
    public interface IEndingEventListener
    {
        /// <summary>
        /// Consumes one queue-drained ending event.
        /// </summary>
        /// <param name="payload">Unmanaged ending payload.</param>
        void OnEndingEvent(in EndingEventPayload payload);
    }

    public static class EndingEvents
    {
        private const int ListenerCapacity = 8;
        private const int PendingEventCapacity = 8;
        private const uint EventOverflowWarningHash = 0x454E4F46u;
        private const uint ListenerRejectedWarningHash = 0x454E524Au;
        private const uint ListenerExceptionWarningHash = 0x454E4558u;
        private const uint EventContextHash = 0x454E4556u;
        private const uint ListenerContextHash = 0x454E4C53u;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;

        private struct ListenerSlot
        {
            public IEndingEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct EndingListenerRegistry
        {
            private readonly ListenerSlot[] _slots;
            private int _count;

            public EndingListenerRegistry(int capacity)
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

            public bool Contains(IEndingEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_slots[i].Listener, listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(IEndingEventListener listener)
            {
                if (listener == null || _count >= _slots.Length)
                    return false;

                _slots[_count++].Listener = listener;
                return true;
            }

            public bool TryUnregister(IEndingEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (!ReferenceEquals(_slots[i].Listener, listener))
                        continue;

                    _count--;
                    _slots[i] = _slots[_count];
                    _slots[_count].Clear();
                    return true;
                }

                return false;
            }

            public IEndingEventListener GetAt(int index)
            {
                return (uint)index < (uint)_count ? _slots[index].Listener : null;
            }
        }

        // COLD ALLOC: ListenerSlot[8] - ending listeners drained by SystemDispatcher LateUpdate - owner: EndingEvents
        private static EndingListenerRegistry _listeners = new EndingListenerRegistry(ListenerCapacity);
        private static readonly ListenerSlot[] _deferredRegisterListeners = new ListenerSlot[ListenerCapacity];
        private static readonly ListenerSlot[] _deferredUnregisterListeners = new ListenerSlot[ListenerCapacity];
        private static NativeQueue<EndingEventPayload> _pendingEvents;
        private static NativeQueue<EndingEventPayload> _nextFrameEvents;
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
        /// Number of queued ending events awaiting LateUpdate dispatch.
        /// </summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(EndingEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(EndingEvents), nameof(_nextFrameEvents));
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
        /// Registers a listener for deferred ending events.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Register(IEndingEventListener listener)
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
        /// Unregisters a listener from deferred ending events.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Unregister(IEndingEventListener listener)
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

        public static void RaiseConditionMet()
        {
            Enqueue(EndingEventType.ConditionMet, EndingChoice.None);
        }

        public static void RaiseChosen(EndingChoice choice)
        {
            Enqueue(EndingEventType.Chosen, choice);
        }

        public static void RaiseSequenceComplete(EndingChoice choice)
        {
            Enqueue(EndingEventType.SequenceComplete, choice);
        }

        /// <summary>
        /// Flushes queued ending events to registered listeners.
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

                if (!_pendingEvents.TryDequeue(out EndingEventPayload payload))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IEndingEventListener listener = _listeners.GetAt(i);
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

        private static void Enqueue(EndingEventType type, EndingChoice choice)
        {
            if (_listeners.Count <= 0)
                return;

            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportEventOverflow();
                return;
            }

            EndingEventPayload payload = new EndingEventPayload
            {
                EventType = (byte)type,
                Choice = (byte)choice,
                Reserved = 0
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
                _pendingEvents = new NativeQueue<EndingEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<EndingEventPayload>[8] — deferred ending lane flushed by SystemDispatcher LateUpdate — owner: EndingEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(EndingEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<EndingEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<EndingEventPayload>[8] — next-frame ending lane prevents same-frame reentrant dispatch — owner: EndingEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(EndingEvents),
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

        private static void DispatchToListener(IEndingEventListener listener, in EndingEventPayload payload)
        {
            try
            {
                listener.OnEndingEvent(in payload);
            }
            catch (Exception exception)
            {
                ReportListenerDispatchException();
                LogListenerDispatchException(exception);
            }
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        private static void LogListenerDispatchException(Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogException(exception);
#endif
        }

        private static void QueueDeferredRegister(IEndingEventListener listener)
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

            _deferredRegisterListeners[_deferredRegisterCount++].Listener = listener;
        }

        private static void QueueDeferredUnregister(IEndingEventListener listener)
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

            _deferredUnregisterListeners[_deferredUnregisterCount++].Listener = listener;
        }

        private static bool CancelDeferredRegister(IEndingEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (!ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    continue;

                _deferredRegisterCount--;
                _deferredRegisterListeners[i] = _deferredRegisterListeners[_deferredRegisterCount];
                _deferredRegisterListeners[_deferredRegisterCount].Clear();
                return true;
            }

            return false;
        }

        private static void CancelDeferredUnregister(IEndingEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (!ReferenceEquals(_deferredUnregisterListeners[i].Listener, listener))
                    continue;

                _deferredUnregisterCount--;
                _deferredUnregisterListeners[i] = _deferredUnregisterListeners[_deferredUnregisterCount];
                _deferredUnregisterListeners[_deferredUnregisterCount].Clear();
                return;
            }
        }

        private static bool IsDeferredRegisterPending(IEndingEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(IEndingEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (ReferenceEquals(_deferredUnregisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static void ApplyDeferredListenerMutations()
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                IEndingEventListener listener = _deferredUnregisterListeners[i].Listener;
                _deferredUnregisterListeners[i].Clear();
                if (listener != null)
                    _listeners.TryUnregister(listener);
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                IEndingEventListener listener = _deferredRegisterListeners[i].Listener;
                _deferredRegisterListeners[i].Clear();
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterCount = 0;

            if (_listeners.Count <= 0)
                DropQueuedEvents();
        }

        private static void RegisterImmediate(IEndingEventListener listener)
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
            ref NativeQueue<EndingEventPayload> queue,
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

            NativeQueue<EndingEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class EndingSystem : MonoBehaviour, ISaveable, ISlowTickable, IAtlasSignalEventListener, IGlobalRegistryHotSwapListener
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Activation Conditions ───────────────────")]
        [Tooltip("Minimalnaya glubina dlya aktivatsii kontsovki (metry).")]
        [SerializeField] private float requiredDepth = 4800f;

        [Tooltip("Minimalnaya sila signala dlya aktivatsii (rasshifrovka).")]
        [SerializeField, Range(0f, 1f)] private float requiredSignalStrength = 0.90f;

        [Header("── Quest IDs ───────────────────────────────")]
        [SerializeField] private string endingQuestId = "quest_atlas_core_reached";

        private const string AtlasShutdownMessageId = "atlas6_shutdown";
        private const string AtlasAmplifiedPublicMessageId = "atlas6_amplified_public";
        private const string AtlasCoreReachedDiscoveryId = "atlas6_core_reached";
        private const string EndingShutdownDiscoveryId = "ending_shutdown";
        private const string EndingLeaveDiscoveryId = "ending_leave";
        private const string EndingAmplifyDiscoveryId = "ending_amplify";
        private static readonly uint _atlasShutdownMessageHash = AtlasSignalEvents.ComputeMessageHash(AtlasShutdownMessageId);
        private static readonly uint _atlasAmplifiedPublicMessageHash = AtlasSignalEvents.ComputeMessageHash(AtlasAmplifiedPublicMessageId);
        private static readonly uint _atlasCoreReachedDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(AtlasCoreReachedDiscoveryId);
        private static readonly uint _endingShutdownDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(EndingShutdownDiscoveryId);
        private static readonly uint _endingLeaveDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(EndingLeaveDiscoveryId);
        private static readonly uint _endingAmplifyDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(EndingAmplifyDiscoveryId);

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private EndingChoice _chosenEnding = EndingChoice.None;
        private bool _conditionMet;
        private bool _endingComplete;
        private bool _registered;
        private bool _serviceRegistered;
        private bool _hotSwapRegistered;
        private HectonSurvivalSystem _survivalSystem;
        private AtlasSignalSystem _atlasSignal;
        private Atlas6DirectiveSystem _atlas6Directive;
        private QuestManager _questRuntime;
        private SaveManager _saveRuntime;
        private LocalizationManager _localization;
        private uint _endingQuestHash;

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        public int SavePriority => 14;
        public int LoadPriority => 14;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public EndingChoice ChosenEnding    => _chosenEnding;
        public bool IsConditionMet          => _conditionMet;
        public bool IsEndingComplete        => _endingComplete;
        public bool CanChooseEnding         => _conditionMet && !_endingComplete;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            CacheQuestHash();
            if (!TryRegisterService())
                return;

            TryRegister();
            CacheRuntimeDependencies();
            TryRegisterSaveParticipant();

            AtlasSignalEvents.Register(this);

            ResolveSurvivalSystem();
            TryRegisterHotSwapListener();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            TryUnregister();
            TryUnregisterService();
            TryUnregisterSaveParticipant();

            AtlasSignalEvents.Unregister(this);
            ClearRuntimeDependencies();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            TryUnregister();
            TryUnregisterService();
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            if (_conditionMet || _endingComplete) return;

            float depth = _survivalSystem != null ? _survivalSystem.Depth : 0f;
            if (depth < requiredDepth) return;

            AtlasSignalSystem signal = _atlasSignal;
            if (signal == null) return;
            if (signal.CurrentStrength < requiredSignalStrength) return;

            // Usloviya vypolneny
            _conditionMet = true;
            EndingEvents.RaiseConditionMet();

            // Aktiviruem kvest
            QuestManager qm = _questRuntime;
            if (qm != null && _endingQuestHash != 0u)
                qm.ActivateQuest(_endingQuestHash);

            NarrativeEvents.RaiseDiscoveryMade(_atlasCoreReachedDiscoveryHash);

            NotificationEvents.PushWarning(ResolveLocalized(
                LocalizationKeys.ENDING_CORE_REACHED,
                "ATLAS-6 CORE DETECTED. TERMINAL ACTIVE. SELECT AN ACTION."));

            LogEndingConditionMet();
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

            EndingSystem registeredRuntime = Hecton8.Core.GlobalRegistry.Ending;
            if (registeredRuntime != null && !ReferenceEquals(registeredRuntime, this))
            {
                Destroy(gameObject);
                return false;
            }

            Hecton8.Core.GlobalRegistry.RegisterEndingRuntime(this);
            _serviceRegistered = ReferenceEquals(Hecton8.Core.GlobalRegistry.Ending, this);
            return _serviceRegistered;
        }

        private void CacheRuntimeDependencies()
        {
            _atlasSignal = Hecton8.Core.GlobalRegistry.AtlasSignal;
            _atlas6Directive = Hecton8.Core.GlobalRegistry.Atlas6Directive;
            _questRuntime = GlobalRegistry.Quest;
            _saveRuntime = Hecton8.SaveSystem.SaveManager.ActiveRuntimeInstance;
            _localization = Hecton.Localization.LocalizationManager.ActiveRuntimeInstance;
        }

        private void ClearRuntimeDependencies()
        {
            _atlasSignal = null;
            _atlas6Directive = null;
            _questRuntime = null;
            _saveRuntime = null;
            _localization = null;
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.AtlasSignalRuntime:
                    _atlasSignal = currentService as AtlasSignalSystem;
                    break;
                case GlobalRegistryServiceSlot.Atlas6DirectiveRuntime:
                    _atlas6Directive = currentService as Atlas6DirectiveSystem;
                    break;
                case GlobalRegistryServiceSlot.QuestRuntime:
                    _questRuntime = currentService as QuestManager;
                    break;
                case GlobalRegistryServiceSlot.Save:
                    if (previousService is SaveManager previousSave)
                        previousSave.Unregister(this);

                    _saveRuntime = currentService as SaveManager;
                    TryRegisterSaveParticipant();
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localization = currentService as LocalizationManager;
                    break;
            }
        }

        private void TryRegisterSaveParticipant()
        {
            SaveManager saveRuntime = _saveRuntime;
            if (saveRuntime != null)
                saveRuntime.Register(this);
        }

        private void TryUnregisterSaveParticipant()
        {
            SaveManager saveRuntime = _saveRuntime;
            if (saveRuntime != null)
                saveRuntime.Unregister(this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            Hecton8.Core.GlobalRegistry.UnregisterEndingRuntime(this);
            _serviceRegistered = false;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — VYBOR KONTsOVKI
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Igrok vybral kontsovku. Vyzyvaetsya iz UI terminala yadra.
        /// </summary>
        public void ForceConditionMetFromQuestDAG()
        {
            if (_conditionMet || _endingComplete)
                return;

            _conditionMet = true;
            EndingEvents.RaiseConditionMet();
            NarrativeEvents.RaiseDiscoveryMade("atlas6_core_data_accessed");
        }

        public void ChooseEnding(EndingChoice choice)
        {
            if (!CanChooseEnding)
            {
                LogInvalidEndingChoice(_conditionMet, _endingComplete);
                return;
            }

            if (choice == EndingChoice.None) return;

            _chosenEnding = choice;
            EndingEvents.RaiseChosen(choice);

            ExecuteEnding(choice);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — KONTsOVKI
        // ══════════════════════════════════════════════════════════

        private void ExecuteEnding(EndingChoice choice)
        {
            switch (choice)
            {
                case EndingChoice.ShutDown:
                    ExecuteShutDown();
                    break;

                case EndingChoice.Leave:
                    ExecuteLeave();
                    break;

                case EndingChoice.Amplify:
                    ExecuteAmplify();
                    break;
            }

            // Zavershaem kvest
            QuestManager qm = _questRuntime;
            if (qm != null && _endingQuestHash != 0u)
                qm.CompleteQuest(_endingQuestHash);

            _endingComplete = true;
            EndingEvents.RaiseSequenceComplete(choice);

            LogEndingChoiceExecuted(choice);
        }

        private void ExecuteShutDown()
        {
            // Atlas-6 vyklyuchen. Signal prekraschaetsya.
            AtlasSignalSystem signal = _atlasSignal;
            if (signal != null)
                signal.DecodeSignal(_atlasShutdownMessageHash);

            Atlas6DirectiveSystem directive = _atlas6Directive;
            if (directive != null)
                directive.RegisterBarterTransaction(); // Korporatsiya poluchila chto hotela

            NarrativeEvents.RaiseDiscoveryMade(_endingShutdownDiscoveryHash);

            NotificationEvents.PushWarning(ResolveLocalized(
                LocalizationKeys.ENDING_SHUTDOWN_COMPLETE,
                "ATLAS-6 SHUT DOWN. SIGNAL TERMINATED. THE CORPORATION WILL GET THE DATA. TERRAFORMING CONTINUES."));
        }

        private void ExecuteLeave()
        {
            // Atlas-6 prodolzhaet rabotu. Signal aktiven.
            NarrativeEvents.RaiseDiscoveryMade(_endingLeaveDiscoveryHash);

            NotificationEvents.PushInfo(ResolveLocalized(
                LocalizationKeys.ENDING_LEAVE_COMPLETE,
                "ATLAS-6 REMAINS ACTIVE. SIGNAL LIVE. LIFE IS PROTECTED - UNTIL THE SIGNAL IS FOUND."));
        }

        private void ExecuteAmplify()
        {
            // Signal usilen — publichnyy. Atlas-6 vyklyuchaetsya sam.
            AtlasSignalSystem signal = _atlasSignal;
            if (signal != null)
                signal.DecodeSignal(_atlasAmplifiedPublicMessageHash);

            NarrativeEvents.RaiseDiscoveryMade(_endingAmplifyDiscoveryHash);

            // Publikuem v sheyder — maksimalnaya intensivnost signala
            Shader.SetGlobalFloat(
                Shader.PropertyToID("_AtlasSignalStrength"), 1f);

            NotificationEvents.PushWarning(ResolveLocalized(
                LocalizationKeys.ENDING_AMPLIFY_COMPLETE,
                "SIGNAL AMPLIFIED. THE WHOLE SECTOR CAN HEAR IT. ATLAS-6 IS ENDING THE PROGRAM. THE TRUTH IS OUT. CONSEQUENCES UNPREDICTABLE."));
        }

        private void CacheQuestHash()
        {
            _endingQuestHash = QuestFlagHashKernel.ComputeStableHash(endingQuestId);
        }

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

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogInvalidEndingChoice(bool conditionMet, bool endingComplete)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[Ending] Cannot choose ending: conditionMet={conditionMet}, complete={endingComplete}");
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogEndingChoiceExecuted(EndingChoice choice)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Ending] Choice executed: {choice}");
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogEndingConditionMet()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[Ending] Condition met — player at Atlas-6 core.");
#endif
        }

        public void OnAtlasSignalEvent(in AtlasSignalEventPayload payload)
        {
            if ((AtlasSignalEventType)payload.EventType == AtlasSignalEventType.Decoded)
                HandleSignalDecoded();
        }

        private void HandleSignalDecoded()
        {
            // Polnaya rasshifrovka — uslovie mozhet byt vypolneno
            // SlowTick proverit glubinu na sleduyuschem tike
        }

        private string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = _localization;
            return manager != null ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback) : fallback;
        }

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        public void PopulateSaveData(SaveData data)
        {
            if (data == null) return;
            data.endingChoice   = (int)_chosenEnding;
            data.endingComplete = _endingComplete;
            data.endingConditionMet = _conditionMet;
        }

        public void LoadFromSaveData(SaveData data)
        {
            if (data == null) return;
            _chosenEnding   = (EndingChoice)data.endingChoice;
            _endingComplete = data.endingComplete;
            _conditionMet   = data.endingConditionMet;
        }
    }
}
