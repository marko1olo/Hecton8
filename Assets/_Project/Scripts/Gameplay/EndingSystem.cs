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
using Unity.Mathematics;
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
        private static int _pendingEventsSentinelId;
        private static int _nextFrameEventsSentinelId;
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
            ReleaseNativeQueues();

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

        public static bool TryRaiseConditionMet()
        {
            return Enqueue(EndingEventType.ConditionMet, EndingChoice.None);
        }

        [Obsolete("Use TryRaiseConditionMet so bounded queue refusal is visible at the producer.", true)]
        public static void RaiseConditionMet() => TryRaiseConditionMet();

        public static bool TryRaiseChosen(EndingChoice choice)
        {
            if (!IsActionChoice(choice))
                return false;

            return Enqueue(EndingEventType.Chosen, choice);
        }

        [Obsolete("Use TryRaiseChosen so bounded queue refusal is visible at the producer.", true)]
        public static void RaiseChosen(EndingChoice choice) => TryRaiseChosen(choice);

        public static bool TryRaiseSequenceComplete(EndingChoice choice)
        {
            if (!IsActionChoice(choice))
                return false;

            return Enqueue(EndingEventType.SequenceComplete, choice);
        }

        [Obsolete("Use TryRaiseSequenceComplete so bounded queue refusal is visible at the producer.", true)]
        public static void RaiseSequenceComplete(EndingChoice choice) => TryRaiseSequenceComplete(choice);

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

        private static bool Enqueue(EndingEventType type, EndingChoice choice)
        {
            if (!TryBuildPayload(type, choice, out EndingEventPayload payload))
                return false;

            if (_listeners.Count <= 0)
                return false;

            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportEventOverflow();
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

        private static bool TryBuildPayload(
            EndingEventType type,
            EndingChoice choice,
            out EndingEventPayload payload)
        {
            payload = default;
            if (!IsKnownEventType(type))
                return false;

            EndingChoice safeChoice = choice;
            if (type == EndingEventType.ConditionMet)
            {
                safeChoice = EndingChoice.None;
            }
            else if (!IsActionChoice(choice))
            {
                return false;
            }

            payload = new EndingEventPayload
            {
                EventType = (byte)type,
                Choice = (byte)safeChoice,
                Reserved = 0
            };
            return true;
        }

        private static bool IsKnownEventType(EndingEventType type)
        {
            return type >= EndingEventType.ConditionMet &&
                   type <= EndingEventType.SequenceComplete;
        }

        private static bool IsActionChoice(EndingChoice choice)
        {
            return choice >= EndingChoice.ShutDown &&
                   choice <= EndingChoice.Amplify;
        }

        private static void EnsureInitialized()
        {
            try
            {
                if (!_pendingEvents.IsCreated)
                {
                    _pendingEvents = new NativeQueue<EndingEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<EndingEventPayload>[8] — deferred ending lane flushed by SystemDispatcher LateUpdate — owner: EndingEvents
                    RegisterNativeQueue(ref _pendingEvents, PendingEventCapacity, nameof(_pendingEvents), out _pendingEventsSentinelId);
                    PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
                }

                if (!_nextFrameEvents.IsCreated)
                {
                    _nextFrameEvents = new NativeQueue<EndingEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<EndingEventPayload>[8] — next-frame ending lane prevents same-frame reentrant dispatch — owner: EndingEvents
                    RegisterNativeQueue(ref _nextFrameEvents, PendingEventCapacity, nameof(_nextFrameEvents), out _nextFrameEventsSentinelId);
                    PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
                }
            }
            catch
            {
                ReleaseNativeQueues();
                _pendingEventCount = 0;
                _nextFrameEventCount = 0;
                throw;
            }
        }

        private static void RegisterNativeQueue<T>(
            ref NativeQueue<T> queue,
            int capacity,
            string label,
            out int sentinelId)
            where T : unmanaged
        {
            sentinelId = 0;
            sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(
                queue,
                capacity,
                nameof(EndingEvents),
                label,
                NativeAllocationLifetime.Session);
            if (sentinelId > 0)
                return;

            ReleaseNativeQueue(ref queue, ref sentinelId);
            throw new InvalidOperationException($"Native memory sentinel registration failed for {label}.");
        }

        private static void ReleaseNativeQueues()
        {
            ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);
            ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);
        }

        private static void ReleaseNativeQueue<T>(ref NativeQueue<T> queue, ref int sentinelId)
            where T : unmanaged
        {
            Exception firstException = null;

            if (sentinelId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(sentinelId);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
                finally
                {
                    sentinelId = 0;
                }
            }

            if (queue.IsCreated)
            {
                try
                {
                    queue.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
                finally
                {
                    queue = default;
                }
            }
            else
            {
                queue = default;
            }

            if (firstException != null)
                throw firstException;
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
            Hecton8.Core.H8Debug.LogException(exception);
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
            int frame = SystemDispatcher.CurrentFrameIndex;
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
            int frame = SystemDispatcher.CurrentFrameIndex;
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
            int frame = SystemDispatcher.CurrentFrameIndex;
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
                {
                    pendingCount = 0;
                    break;
                }

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
            int sentinelIdSwap = _pendingEventsSentinelId;
            _pendingEventsSentinelId = _nextFrameEventsSentinelId;
            _nextFrameEventsSentinelId = sentinelIdSwap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class EndingSystem : MonoBehaviour, ISaveable, ISlowTickable, ILateFrameTickable, IAtlasSignalEventListener, IGlobalRegistryHotSwapListener, IEndingRuntimeService
    {
        private const int PendingNotificationCapacity = 4;
        private const int PendingNotificationCharCapacity = 512;
        private static readonly uint _NotificationQueueDropWarningHash = unchecked((uint)LocHash.Compute("EndingSystem.NotificationQueueDrop"));
        private static readonly uint _NotificationPushMissWarningHash = unchecked((uint)LocHash.Compute("EndingSystem.NotificationPushMiss"));
        private static readonly uint _NotificationContextHash = unchecked((uint)LocHash.Compute("EndingSystem.Notification"));

        // ----------------------------------------------------------
        //  INSPECTOR
        // ----------------------------------------------------------

        [Header("-- Activation Conditions -------------------")]
        [Tooltip("Minimalnaya glubina dlya aktivatsii kontsovki (metry).")]
        [SerializeField] private float requiredDepth = 4800f;

        [Tooltip("Minimalnaya sila signala dlya aktivatsii (rasshifrovka).")]
        [SerializeField, Range(0f, 1f)] private float requiredSignalStrength = 0.90f;

        [Header("-- Quest IDs -------------------------------")]
        [SerializeField] private string endingQuestId = "quest_atlas_core_reached";

        private const string AtlasShutdownMessageId = "atlas6_shutdown";
        private const string AtlasAmplifiedPublicMessageId = "atlas6_amplified_public";
        private const string AtlasCoreReachedDiscoveryId = "atlas6_core_reached";
        private const string AtlasCoreDataAccessedDiscoveryId = "atlas6_core_data_accessed";
        private const string EndingShutdownDiscoveryId = "ending_shutdown";
        private const string EndingLeaveDiscoveryId = "ending_leave";
        private const string EndingAmplifyDiscoveryId = "ending_amplify";
        private static readonly uint _atlasShutdownMessageHash = AtlasSignalEvents.ComputeMessageHash(AtlasShutdownMessageId);
        private static readonly uint _atlasAmplifiedPublicMessageHash = AtlasSignalEvents.ComputeMessageHash(AtlasAmplifiedPublicMessageId);
        private static readonly uint _atlasCoreReachedDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(AtlasCoreReachedDiscoveryId);
        private static readonly uint _atlasCoreDataAccessedDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(AtlasCoreDataAccessedDiscoveryId);
        private static readonly uint _endingShutdownDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(EndingShutdownDiscoveryId);
        private static readonly uint _endingLeaveDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(EndingLeaveDiscoveryId);
        private static readonly uint _endingAmplifyDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(EndingAmplifyDiscoveryId);
        private static readonly int _atlasSignalStrengthShaderId = Shader.PropertyToID("_AtlasSignalStrength");

        // ----------------------------------------------------------
        //  SINGLETON
        // ----------------------------------------------------------

        // ----------------------------------------------------------
        //  PRIVATE STATE
        // ----------------------------------------------------------

        private EndingChoice _chosenEnding = EndingChoice.None;
        private bool _conditionMet;
        private bool _endingComplete;
        private bool _registered;
        private bool _lateFrameRegistered;
        private bool _serviceRegistered;
        private bool _hotSwapRegistered;
        private bool _saveRegistered;
        private bool _atlasSignalEventRegistered;
        private bool _runtimeOwnerAborted;
        private HectonSurvivalSystem _survivalSystem;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IAtlasSignalReadModel _atlasSignal;
        private IAtlasSignalDecodeSink _atlasSignalDecodeSink;
        private IAtlas6DirectiveCommandSink _atlas6Directive;
        private IQuestSystem _questRuntime;
        private ISaveService _saveService;
        private ISaveService _registeredSaveService;
        private ILocalizationTextReadModel _localization;
        private uint _endingQuestHash;
        private bool _pendingAtlasSignalStrengthDirty;
        private float _pendingAtlasSignalStrength;
        private PendingNotificationRequest _pendingNotification0;
        private PendingNotificationRequest _pendingNotification1;
        private PendingNotificationRequest _pendingNotification2;
        private PendingNotificationRequest _pendingNotification3;
        private byte _pendingNotificationCount;
        private int _notificationQueueDropCount;
        private int _notificationPushMissCount;

        private unsafe struct PendingNotificationRequest
        {
            public ushort Length;
            public byte Severity;
            public byte IsDirty;
            public fixed char Characters[PendingNotificationCharCapacity];

            public void Clear()
            {
                Length = 0;
                Severity = 0;
                IsDirty = 0;
            }
        }

        // ----------------------------------------------------------
        //  ISaveable
        // ----------------------------------------------------------

        public int SavePriority => 14;
        public int LoadPriority => 14;
        public int NotificationQueueDropCount => _notificationQueueDropCount;
        public int NotificationPushMissCount => _notificationPushMissCount;

        // ----------------------------------------------------------
        //  PUBLIC PROPERTIES
        // ----------------------------------------------------------

        public EndingChoice ChosenEnding    => _chosenEnding;
        public bool IsConditionMet          => _conditionMet;
        public bool IsEndingComplete        => _endingComplete;
        public bool CanChooseEnding         => _conditionMet && !_endingComplete;

        // ----------------------------------------------------------
        //  LIFECYCLE
        // ----------------------------------------------------------

        private void OnEnable()
        {
            CacheQuestHash();
            if (!TryRegisterService())
                return;

            TryRegister();
            CacheRuntimeDependencies();
            TryRegisterSaveParticipant();

            TryRegisterAtlasSignalEvents();

            ResolveSurvivalSystem();
            TryRegisterHotSwapListener();
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregisterHotSwapListener();
            TryUnregisterLateFrameTick();
            TryUnregister();
            TryUnregisterService();
            TryUnregisterSaveParticipant();
            TryUnregisterAtlasSignalEvents();
            ClearRuntimeDependencies();
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregisterHotSwapListener();
            TryUnregisterLateFrameTick();
            TryUnregister();
            TryUnregisterService();
            TryUnregisterSaveParticipant();
            TryUnregisterAtlasSignalEvents();
            ClearRuntimeDependencies();
        }

        // ----------------------------------------------------------
        //  ISlowTickable
        // ----------------------------------------------------------

        public void SlowTick()
        {
            if (_runtimeOwnerAborted)
                return;

            if (_conditionMet || _endingComplete) return;

            float depth = ResolveCurrentDepthMeters();
            if (depth < requiredDepth) return;

            IAtlasSignalReadModel signal = _atlasSignal;
            if (signal == null) return;
            if (signal.CurrentAtlasSignalStrength01 < requiredSignalStrength) return;

            // Usloviya vypolneny
            _conditionMet = true;
            EndingEvents.TryRaiseConditionMet();

            // Aktiviruem kvest
            IQuestSystem qm = _questRuntime;
            if (qm != null && _endingQuestHash != 0u)
                qm.ActivateQuest(_endingQuestHash);

            NarrativeEvents.TryRaiseDiscoveryMade(_atlasCoreReachedDiscoveryHash);

            QueueNotification(
                ResolveLocalizedSpan(
                    LocalizationKeys.ENDING_CORE_REACHED,
                    "ATLAS-6 CORE DETECTED. TERMINAL ACTIVE. SELECT AN ACTION."),
                NotificationEventSeverity.Warning);

            LogEndingConditionMet();
        }

        public void LateFrameTick()
        {
            if (_runtimeOwnerAborted)
                return;

            FlushQueuedAtlasSignalStrength();
            FlushQueuedNotifications();
            if (!_pendingAtlasSignalStrengthDirty && _pendingNotificationCount == 0)
                TryUnregisterLateFrameTick();
        }

        private void TryRegister()
        {
            if (_runtimeOwnerAborted || _registered)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private void TryRegisterLateFrameTick()
        {
            if (_runtimeOwnerAborted || _lateFrameRegistered || !Application.isPlaying)
                return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void TryUnregisterLateFrameTick()
        {
            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _lateFrameRegistered = false;
            }

            ClearQueuedPresentation();
        }

        private void QueueAtlasSignalStrength(float strength01)
        {
            if (_runtimeOwnerAborted)
                return;

            _pendingAtlasSignalStrength = Mathf.Clamp01(strength01);
            _pendingAtlasSignalStrengthDirty = true;
            TryRegisterLateFrameTick();
        }

        private void FlushQueuedAtlasSignalStrength()
        {
            if (_runtimeOwnerAborted)
                return;

            if (!_pendingAtlasSignalStrengthDirty)
                return;

            _pendingAtlasSignalStrengthDirty = false;
            Shader.SetGlobalFloat(_atlasSignalStrengthShaderId, _pendingAtlasSignalStrength);
        }

        private unsafe bool QueueNotification(ReadOnlySpan<char> message, NotificationEventSeverity severity)
        {
            if (_runtimeOwnerAborted)
                return false;

            if (message.IsEmpty || message.Length > PendingNotificationCharCapacity)
            {
                ReportNotificationQueueDrop((uint)severity);
                return false;
            }

            if (_pendingNotificationCount >= PendingNotificationCapacity)
            {
                ReportNotificationQueueDrop((uint)severity);
                return false;
            }

            ref PendingNotificationRequest request = ref GetPendingNotificationSlot(_pendingNotificationCount);
            fixed (char* destination = request.Characters)
            {
                for (int i = 0; i < message.Length; i++)
                    destination[i] = message[i];
            }

            request.Length = (ushort)message.Length;
            request.Severity = (byte)severity;
            request.IsDirty = 1;
            _pendingNotificationCount++;
            TryRegisterLateFrameTick();
            return true;
        }

        private unsafe void FlushQueuedNotifications()
        {
            if (_runtimeOwnerAborted)
                return;

            int count = _pendingNotificationCount;
            _pendingNotificationCount = 0;

            for (int i = 0; i < count; i++)
            {
                ref PendingNotificationRequest request = ref GetPendingNotificationSlot(i);
                if (request.IsDirty == 0 || request.Length == 0)
                {
                    request.Clear();
                    continue;
                }

                ushort length = request.Length;
                byte severity = request.Severity;
                request.Clear();

                fixed (char* characters = request.Characters)
                {
                    ReadOnlySpan<char> message =
                        System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref characters[0], length);
                    uint messageHash = NotificationEvents.RegisterMessage(message);
                    if (messageHash == 0u)
                        continue;

                    TryPushQueuedNotification(messageHash, severity);
                }
            }
        }

        private void TryPushQueuedNotification(uint messageHash, byte severity)
        {
            bool pushed;
            if (severity == (byte)NotificationEventSeverity.Warning)
            {
                pushed = NotificationEvents.TryPushRegisteredWarning(messageHash);
            }
            else if (severity == (byte)NotificationEventSeverity.Critical)
            {
                pushed = NotificationEvents.TryPushRegisteredCritical(messageHash);
            }
            else
            {
                pushed = NotificationEvents.TryPushRegisteredInfo(messageHash);
            }

            if (pushed)
                return;

            ReportNotificationPushMiss(messageHash);
        }

        private void ReportNotificationQueueDrop(uint contextHash)
        {
            _notificationQueueDropCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _NotificationQueueDropWarningHash,
                _NotificationContextHash ^ contextHash,
                math.max(1, _notificationQueueDropCount));
        }

        private void ReportNotificationPushMiss(uint messageHash)
        {
            _notificationPushMissCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _NotificationPushMissWarningHash,
                _NotificationContextHash ^ messageHash,
                math.max(1, _notificationPushMissCount));
        }

        private void ClearQueuedPresentation()
        {
            _pendingAtlasSignalStrengthDirty = false;
            _pendingAtlasSignalStrength = 0f;
            _pendingNotificationCount = 0;
            _pendingNotification0.Clear();
            _pendingNotification1.Clear();
            _pendingNotification2.Clear();
            _pendingNotification3.Clear();
            _notificationQueueDropCount = 0;
            _notificationPushMissCount = 0;
        }

        private ref PendingNotificationRequest GetPendingNotificationSlot(int index)
        {
            switch (index)
            {
                case 0:
                    return ref _pendingNotification0;
                case 1:
                    return ref _pendingNotification1;
                case 2:
                    return ref _pendingNotification2;
                default:
                    return ref _pendingNotification3;
            }
        }

        private bool TryRegisterService()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (_serviceRegistered || !Application.isPlaying)
                return true;

            if (TryAbortForUsableExistingRuntime())
                return false;

            EndingSystem registeredRuntime = Hecton8.Core.GlobalRegistry.Ending;
            if (IsEndingRuntimeUsable(registeredRuntime))
            {
                AbortDuplicateRuntimeOwner();
                return false;
            }

            if (!ReferenceEquals(registeredRuntime, null) && !ReferenceEquals(registeredRuntime, this))
                Hecton8.Core.GlobalRegistry.UnregisterEndingRuntime(registeredRuntime);

            Hecton8.Core.GlobalRegistry.RegisterEndingRuntime(this);
            _serviceRegistered = ReferenceEquals(Hecton8.Core.GlobalRegistry.Ending, this);
            if (!_serviceRegistered)
            {
                AbortDuplicateRuntimeOwner();
                return false;
            }

            return _serviceRegistered;
        }

        private void CacheRuntimeDependencies()
        {
            if (_runtimeOwnerAborted)
                return;

            _atlasSignal = Hecton8.Core.GlobalRegistry.AtlasSignalReadModel;
            _atlasSignalDecodeSink = Hecton8.Core.GlobalRegistry.AtlasSignalDecodeSink;
            _atlas6Directive = Hecton8.Core.GlobalRegistry.Atlas6DirectiveCommandSink;
            _questRuntime = GlobalRegistry.QuestSystem;
            _saveService = Hecton8.Core.GlobalRegistry.Save;
            _localization = Hecton8.Core.GlobalRegistry.LocalizationText;
            CachePlayerRuntimeContext(GlobalRegistry.Player, null);
        }

        private void ClearRuntimeDependencies()
        {
            _survivalSystem = null;
            _playerRuntimeContext = null;
            _atlasSignal = null;
            _atlasSignalDecodeSink = null;
            _atlas6Directive = null;
            _questRuntime = null;
            _saveService = null;
            _localization = null;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_runtimeOwnerAborted || _hotSwapRegistered || !Application.isPlaying)
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
            if (_runtimeOwnerAborted)
                return;

            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregister();
                    TryUnregisterLateFrameTick();
                    if (currentService != null && isActiveAndEnabled)
                    {
                        TryRegister();
                        if (_pendingAtlasSignalStrengthDirty || _pendingNotificationCount > 0)
                            TryRegisterLateFrameTick();
                    }
                    break;
                case GlobalRegistryServiceSlot.AtlasSignalRuntime:
                    _atlasSignal = currentService as IAtlasSignalReadModel;
                    _atlasSignalDecodeSink = currentService as IAtlasSignalDecodeSink;
                    break;
                case GlobalRegistryServiceSlot.Atlas6DirectiveRuntime:
                    _atlas6Directive = currentService as IAtlas6DirectiveCommandSink;
                    break;
                case GlobalRegistryServiceSlot.QuestRuntime:
                case GlobalRegistryServiceSlot.QuestSystem:
                    _questRuntime = currentService as IQuestSystem;
                    break;
                case GlobalRegistryServiceSlot.Save:
                    TryUnregisterSaveParticipant();
                    _saveService = currentService as ISaveService;
                    TryRegisterSaveParticipant();
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localization = currentService as ILocalizationTextReadModel;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    CachePlayerRuntimeContext(
                        currentService as IPlayerRuntimeContext,
                        previousService as IPlayerRuntimeContext);
                    break;
            }
        }

        private void TryRegisterSaveParticipant()
        {
            if (_runtimeOwnerAborted || _saveRegistered || !Application.isPlaying || !isActiveAndEnabled)
                return;

            ISaveService saveService = _saveService;
            if (!IsSaveServiceUsable(saveService))
            {
                saveService = Hecton8.Core.GlobalRegistry.Save;
                _saveService = saveService;
            }

            if (!IsSaveServiceUsable(saveService))
                return;

            saveService.Register(this);
            _registeredSaveService = saveService;
            _saveRegistered = true;
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
        }

        private void TryUnregisterSaveParticipant()
        {
            if (!_saveRegistered && _registeredSaveService == null)
                return;

            ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;
            if (saveService != null)
                saveService.Unregister(this);

            _registeredSaveService = null;
            _saveService = null;
            _saveRegistered = false;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            Hecton8.Core.GlobalRegistry.UnregisterEndingRuntime(this);
            _serviceRegistered = false;
        }

        private void TryRegisterAtlasSignalEvents()
        {
            if (_runtimeOwnerAborted || _atlasSignalEventRegistered)
                return;

            AtlasSignalEvents.Register(this);
            _atlasSignalEventRegistered = true;
        }

        private void TryUnregisterAtlasSignalEvents()
        {
            if (!_atlasSignalEventRegistered)
                return;

            AtlasSignalEvents.Unregister(this);
            _atlasSignalEventRegistered = false;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            EndingSystem registeredRuntime = Hecton8.Core.GlobalRegistry.Ending;
            if (ReferenceEquals(registeredRuntime, this))
                return false;

            if (IsEndingRuntimeUsable(registeredRuntime))
            {
                AbortDuplicateRuntimeOwner();
                return true;
            }

            if (!ReferenceEquals(registeredRuntime, null))
                Hecton8.Core.GlobalRegistry.UnregisterEndingRuntime(registeredRuntime);

            return false;
        }

        private void AbortDuplicateRuntimeOwner()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterLateFrameTick();
            TryUnregister();
            TryUnregisterSaveParticipant();
            TryUnregisterAtlasSignalEvents();
            ClearRuntimeDependencies();
            ClearQueuedPresentation();
            _runtimeOwnerAborted = true;
            _registered = false;
            _lateFrameRegistered = false;
            _serviceRegistered = false;
            _hotSwapRegistered = false;
            _saveRegistered = false;
            _atlasSignalEventRegistered = false;
            enabled = false;
            Destroy(gameObject);
        }

        private static bool IsEndingRuntimeUsable(EndingSystem system)
        {
            return !ReferenceEquals(system, null) &&
                   system != null &&
                   system._serviceRegistered &&
                   system.isActiveAndEnabled &&
                   !system._runtimeOwnerAborted;
        }

        // ----------------------------------------------------------
        //  PUBLIC API — VYBOR KONTsOVKI
        // ----------------------------------------------------------

        /// <summary>
        /// Igrok vybral kontsovku. Vyzyvaetsya iz UI terminala yadra.
        /// </summary>
        public void ForceConditionMetFromQuestDAG()
        {
            if (_runtimeOwnerAborted)
                return;

            if (_conditionMet || _endingComplete)
                return;

            _conditionMet = true;
            EndingEvents.TryRaiseConditionMet();
            NarrativeEvents.TryRaiseDiscoveryMade(_atlasCoreDataAccessedDiscoveryHash);
        }

        public void ChooseEnding(EndingChoice choice)
        {
            if (_runtimeOwnerAborted)
                return;

            if (!CanChooseEnding)
            {
                LogInvalidEndingChoice(_conditionMet, _endingComplete);
                return;
            }

            if (!IsActionEndingChoice(choice))
            {
                LogInvalidEndingChoice(_conditionMet, _endingComplete);
                return;
            }

            _chosenEnding = choice;
            EndingEvents.TryRaiseChosen(choice);

            ExecuteEnding(choice);
        }

        public void ChooseEnding(byte endingChoiceCode)
        {
            ChooseEnding(SanitizeEndingChoice(endingChoiceCode));
        }

        // ----------------------------------------------------------
        //  PRIVATE — KONTsOVKI
        // ----------------------------------------------------------

        private void ExecuteEnding(EndingChoice choice)
        {
            if (!IsActionEndingChoice(choice))
                return;

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
            IQuestSystem qm = _questRuntime;
            if (qm != null && _endingQuestHash != 0u)
                qm.CompleteQuest(_endingQuestHash);

            _endingComplete = true;
            EndingEvents.TryRaiseSequenceComplete(choice);

            LogEndingChoiceExecuted(choice);
        }

        private void ExecuteShutDown()
        {
            // Atlas-6 vyklyuchen. Signal prekraschaetsya.
            IAtlasSignalDecodeSink signal = _atlasSignalDecodeSink;
            if (signal != null)
                signal.DecodeSignal(_atlasShutdownMessageHash);

            IAtlas6DirectiveCommandSink directive = _atlas6Directive;
            if (directive != null)
                directive.RegisterBarterTransaction(); // Korporatsiya poluchila chto hotela

            NarrativeEvents.TryRaiseDiscoveryMade(_endingShutdownDiscoveryHash);

            QueueNotification(
                ResolveLocalizedSpan(
                    LocalizationKeys.ENDING_SHUTDOWN_COMPLETE,
                    "ATLAS-6 SHUT DOWN. SIGNAL TERMINATED. THE CORPORATION WILL GET THE DATA. TERRAFORMING CONTINUES."),
                NotificationEventSeverity.Warning);
        }

        private void ExecuteLeave()
        {
            // Atlas-6 prodolzhaet rabotu. Signal aktiven.
            NarrativeEvents.TryRaiseDiscoveryMade(_endingLeaveDiscoveryHash);

            QueueNotification(
                ResolveLocalizedSpan(
                    LocalizationKeys.ENDING_LEAVE_COMPLETE,
                    "ATLAS-6 REMAINS ACTIVE. SIGNAL LIVE. LIFE IS PROTECTED - UNTIL THE SIGNAL IS FOUND."),
                NotificationEventSeverity.Info);
        }

        private void ExecuteAmplify()
        {
            // Signal usilen — publichnyy. Atlas-6 vyklyuchaetsya sam.
            IAtlasSignalDecodeSink signal = _atlasSignalDecodeSink;
            if (signal != null)
                signal.DecodeSignal(_atlasAmplifiedPublicMessageHash);

            NarrativeEvents.TryRaiseDiscoveryMade(_endingAmplifyDiscoveryHash);

            QueueAtlasSignalStrength(1f);

            QueueNotification(
                ResolveLocalizedSpan(
                    LocalizationKeys.ENDING_AMPLIFY_COMPLETE,
                    "SIGNAL AMPLIFIED. THE WHOLE SECTOR CAN HEAR IT. ATLAS-6 IS ENDING THE PROGRAM. THE TRUTH IS OUT. CONSEQUENCES UNPREDICTABLE."),
                NotificationEventSeverity.Warning);
        }

        private void CacheQuestHash()
        {
            _endingQuestHash = QuestFlagHashKernel.ComputeStableHash(endingQuestId);
        }

        private bool ResolveSurvivalSystem()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (_survivalSystem != null)
                return true;

            CachePlayerRuntimeContext(_playerRuntimeContext != null ? _playerRuntimeContext : GlobalRegistry.Player, null);
            if (_survivalSystem != null)
                return true;

            if (!GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) ||
                playerTransform == null)
            {
                return false;
            }

            return playerTransform.TryGetComponent(out _survivalSystem);
        }

        private void CachePlayerRuntimeContext(
            IPlayerRuntimeContext currentPlayerContext,
            IPlayerRuntimeContext previousPlayerContext)
        {
            if (previousPlayerContext != null &&
                ReferenceEquals(_survivalSystem, previousPlayerContext.SurvivalSystem))
            {
                _survivalSystem = null;
            }

            _playerRuntimeContext = currentPlayerContext != null && currentPlayerContext.IsInitialized
                ? currentPlayerContext
                : null;

            HectonSurvivalSystem contextSurvival = currentPlayerContext != null
                ? currentPlayerContext.SurvivalSystem
                : null;

            if (contextSurvival != null)
                _survivalSystem = contextSurvival;
        }

        private float ResolveCurrentDepthMeters()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null &&
                playerContext.IsInitialized &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                math.isfinite(movementState.DepthMeters))
            {
                return math.max(0f, movementState.DepthMeters);
            }

            if (playerContext != null)
                return 0f;

            HectonSurvivalSystem survival = _survivalSystem;
            if (survival != null && math.isfinite(survival.Depth))
                return math.max(0f, survival.Depth);

            if (_survivalSystem == null)
                ResolveSurvivalSystem();

            survival = _survivalSystem;
            if (survival != null && math.isfinite(survival.Depth))
                return math.max(0f, survival.Depth);

            return 0f;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogInvalidEndingChoice(bool conditionMet, bool endingComplete)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogWarning($"[Ending] Cannot choose ending: conditionMet={conditionMet}, complete={endingComplete}");
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogEndingChoiceExecuted(EndingChoice choice)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.Log($"[Ending] Choice executed: {choice}");
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogEndingConditionMet()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.Log("[Ending] Condition met — player at Atlas-6 core.");
#endif
        }

        public void OnAtlasSignalEvent(in AtlasSignalEventPayload payload)
        {
            if (_runtimeOwnerAborted)
                return;

            if ((AtlasSignalEventType)payload.EventType == AtlasSignalEventType.Decoded)
                HandleSignalDecoded();
        }

        private void HandleSignalDecoded()
        {
            // Polnaya rasshifrovka — uslovie mozhet byt vypolneno
            // SlowTick proverit glubinu na sleduyuschem tike
        }

        private ReadOnlySpan<char> ResolveLocalizedSpan(string key, string fallback)
        {
            ILocalizationTextReadModel manager = _localization;
            return manager != null
                ? manager.GetRawSpanOrFallback(LocHash.Compute(key), fallback.AsSpan())
                : fallback.AsSpan();
        }

        // ----------------------------------------------------------
        //  ISaveable
        // ----------------------------------------------------------

        public void PopulateSaveData(SaveData data)
        {
            if (_runtimeOwnerAborted || data == null) return;
            EndingChoice safeChoice = SanitizeEndingChoice((int)_chosenEnding);
            bool safeComplete = _endingComplete && safeChoice != EndingChoice.None;
            if (!safeComplete)
                safeChoice = EndingChoice.None;
            data.endingChoice = (int)safeChoice;
            data.endingComplete = safeComplete;
            data.endingConditionMet = _conditionMet || safeComplete;
        }

        public void LoadFromSaveData(SaveData data)
        {
            ClearQueuedPresentation();
            if (_runtimeOwnerAborted || data == null) return;
            _chosenEnding = SanitizeEndingChoice(data.endingChoice);
            _endingComplete = data.endingComplete && _chosenEnding != EndingChoice.None;
            _conditionMet = data.endingConditionMet || _endingComplete;
            if (!_endingComplete)
                _chosenEnding = EndingChoice.None;
        }

        private static EndingChoice SanitizeEndingChoice(int endingChoice)
        {
            return endingChoice >= (int)EndingChoice.None &&
                   endingChoice <= (int)EndingChoice.Amplify
                ? (EndingChoice)endingChoice
                : EndingChoice.None;
        }

        private static bool IsActionEndingChoice(EndingChoice choice)
        {
            return choice >= EndingChoice.ShutDown &&
                   choice <= EndingChoice.Amplify;
        }
    }
}
