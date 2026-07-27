// ============================================================================
// HECTON-8 — FirstHourDirector.cs
// Rezhissura pervogo chasa igry.
//
// LOR (lor1 — Psihologicheskiy arc pervyh dvuh chasov):
//   Minuta 0-5:    Dezorientatsiya ? Orientatsiya
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
using Hecton8.Core.Contracts.Signals;
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
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;

        private struct ListenerSlot
        {
            public IFirstHourEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct FirstHourListenerRegistry
        {
            private readonly ListenerSlot[] _slots;
            private int _count;

            public FirstHourListenerRegistry(int capacity)
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

            public bool Contains(IFirstHourEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_slots[i].Listener, listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(IFirstHourEventListener listener)
            {
                if (listener == null || _count >= _slots.Length)
                    return false;

                _slots[_count++].Listener = listener;
                return true;
            }

            public bool TryUnregister(IFirstHourEventListener listener)
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

            public IFirstHourEventListener GetAt(int index)
            {
                return (uint)index < (uint)_count ? _slots[index].Listener : null;
            }
        }

        // COLD ALLOC: ListenerSlot[8] - first-hour milestone listeners drained by SystemDispatcher LateUpdate - owner: FirstHourEvents
        private static FirstHourListenerRegistry _listeners = new FirstHourListenerRegistry(ListenerCapacity);
        private static readonly ListenerSlot[] _deferredRegisterListeners = new ListenerSlot[ListenerCapacity];
        private static readonly ListenerSlot[] _deferredUnregisterListeners = new ListenerSlot[ListenerCapacity];
        private static NativeQueue<FirstHourEventPayload> _pendingEvents;
        private static NativeQueue<FirstHourEventPayload> _nextFrameEvents;
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
        /// Number of queued first-hour milestone events awaiting LateUpdate dispatch.
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
        public static bool TryRaiseMilestone(FirstHourMilestone milestone)
        {
            if (_listeners.Count <= 0)
                return false;

            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportEventOverflow();
                return false;
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
                return true;
            }

            _pendingEvents.Enqueue(payload);
            _pendingEventCount++;
            return true;
        }

        [Obsolete("Use TryRaiseMilestone so bounded queue refusal is visible at the producer.", true)]
        public static void RaiseMilestone(FirstHourMilestone milestone) => TryRaiseMilestone(milestone);

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
                        IFirstHourEventListener listener = _listeners.GetAt(i);
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
            try
            {
                if (!_pendingEvents.IsCreated)
                {
                    _pendingEvents = new NativeQueue<FirstHourEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<FirstHourEventPayload>[16] — deferred first-hour milestone lane flushed by SystemDispatcher LateUpdate — owner: FirstHourEvents
                    RegisterNativeQueue(ref _pendingEvents, PendingEventCapacity, nameof(_pendingEvents), out _pendingEventsSentinelId);
                    PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
                }

                if (!_nextFrameEvents.IsCreated)
                {
                    _nextFrameEvents = new NativeQueue<FirstHourEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<FirstHourEventPayload>[16] — next-frame first-hour milestone lane prevents same-frame reentrant dispatch — owner: FirstHourEvents
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
                nameof(FirstHourEvents),
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
            Hecton8.Core.H8Debug.LogException(exception);
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

            _deferredRegisterListeners[_deferredRegisterCount++].Listener = listener;
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

            _deferredUnregisterListeners[_deferredUnregisterCount++].Listener = listener;
        }

        private static bool CancelDeferredRegister(IFirstHourEventListener listener)
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

        private static void CancelDeferredUnregister(IFirstHourEventListener listener)
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

        private static bool IsDeferredRegisterPending(IFirstHourEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(IFirstHourEventListener listener)
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
                IFirstHourEventListener listener = _deferredUnregisterListeners[i].Listener;
                _deferredUnregisterListeners[i].Clear();
                if (listener != null)
                    _listeners.TryUnregister(listener);
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                IFirstHourEventListener listener = _deferredRegisterListeners[i].Listener;
                _deferredRegisterListeners[i].Clear();
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
            ref NativeQueue<FirstHourEventPayload> queue,
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

            NativeQueue<FirstHourEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            int sentinelIdSwap = _pendingEventsSentinelId;
            _pendingEventsSentinelId = _nextFrameEventsSentinelId;
            _nextFrameEventsSentinelId = sentinelIdSwap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }
    }

    /// <summary>
    /// Unmanaged first-hour milestone event payload.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct FirstHourEventPayload
    {
        [FieldOffset(0)] public byte Milestone;
        [FieldOffset(1)] public byte Reserved0;
        [FieldOffset(2)] public byte Reserved1;
        [FieldOffset(3)] public byte Reserved2;
        [FieldOffset(4)] private uint _pad0;
        [FieldOffset(8)] private ulong _pad1;
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
    public sealed class FirstHourDirector : MonoBehaviour, ISaveable, ISlowTickable, ILateFrameTickable, IQuestEventListener, IAudioLogEventListener, INarrativeEventListener, IScanEventListener, ICraftingEventListener, IInteractionEventListener, IFirstHourReadModel, IFirstHourRouteContactSink, IGlobalRegistryHotSwapListener
    {
        private const int PendingNotificationCapacity = 4;
        private const int PendingNotificationCharCapacity = 512;
        private static readonly uint _NotificationQueueDropWarningHash = unchecked((uint)LocHash.Compute("FirstHourDirector.NotificationQueueDrop"));
        private static readonly uint _NotificationPushMissWarningHash = unchecked((uint)LocHash.Compute("FirstHourDirector.NotificationPushMiss"));
        private static readonly uint _NotificationContextHash = unchecked((uint)LocHash.Compute("FirstHourDirector.Notification"));

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
            HasLoreRouteContact = 1 << 10,
            StarterToolReminderIssued = 1 << 11
        }

        // ----------------------------------------------------------
        //  INSPECTOR
        // ----------------------------------------------------------

        [Header("-- Timing (seconds) ------------------------")]
        [SerializeField] private float orientationTime   = 300f;   // 5 min
        [SerializeField] private float shadowTime        = 2400f;  // 40 min
        [SerializeField] private float firstModuleTime   = 4200f;  // 70 min

        [Header("-- Shadow Trigger --------------------------")]
        [Tooltip("Minimalnaya glubina dlya poyavleniya teni (metry).")]
        [SerializeField] private float shadowMinDepth = 30f;

        [Header("-- Early Goal Hooks -------------------------")]
        [Tooltip("Quest that represents successful arrival/orientation.")]
        [SerializeField] private string arrivalQuestId = "quest_arrival";

        [Tooltip("Quest that opens the starter drill route before copper becomes a valid first-hour target.")]
        [SerializeField] private string starterToolQuestId = "quest_starter_drill";

        [Tooltip("Item ID that proves the starter drill route was solved in an older save.")]
        [SerializeField] private string starterToolItemId = "Item_Tool_SeafloorDrill";

        [Tooltip("Quest that should become the next clear early-game material goal after the starter drill.")]
        [SerializeField] private string firstResourceQuestId = "quest_copper_sample";

        [Tooltip("Item ID that proves the first post-drill resource goal was already solved in an older save.")]
        [SerializeField] private string firstResourceItemId = "Data_Copper";

        [Tooltip("Quest that should take over once the player secures the first core material.")]
        [SerializeField] private string firstDepthQuestId = "quest_first_breath";

        [Tooltip("Narrative discovery that counts as a real ruined-colony/module contact.")]
        [SerializeField] private string firstModuleZoneDiscoveryId = "zone_drowned_factories";

        [Header("-- First Craft Gate -------------------------")]
        [Tooltip("First-hour craft milestone result: early conductive line.")]
        [SerializeField] private string firstCraftResultItemId0 = "Comp_CopperWire";

        [Tooltip("First-hour craft milestone result: emergency oxygen safety margin.")]
        [SerializeField] private string firstCraftResultItemId1 = "Data_EmergencyO2Canister";

        [Tooltip("First-hour craft milestone result: real navigation support.")]
        [SerializeField] private string firstCraftResultItemId2 = "Item_Tool_BeaconDeployer";

        [Tooltip("First-hour craft milestone result: repair progression tool.")]
        [SerializeField] private string firstCraftResultItemId3 = "Item_Tool_Repair";

        [Tooltip("First-hour craft milestone result: pressure route component.")]
        [SerializeField] private string firstCraftResultItemId4 = "Comp_PressureSeal";

        [SerializeField] private string firstCraftResultItemId5 = "Item_Tool_SeafloorDrill";

        [Header("-- Retention Nudges -------------------------")]
        [Tooltip("When to remind the player about the first core resource if they are still drifting.")]
        [SerializeField] private float firstResourceReminderTime = 480f;

        [Tooltip("When to remind the player that the next real step is to go deeper.")]
        [SerializeField] private float firstDepthReminderTime = 1080f;

        [Tooltip("When to remind the player that shallow safety is no longer the real progression route and the next meaningful contact is a module or ruin.")]
        [SerializeField] private float firstModuleReminderTime = 2100f;

        [Header("-- Soft Guidance ---------------------------")]
        [Tooltip("Minimum delay between contextual onboarding nudges.")]
        [SerializeField] private float contextualGuidanceCooldown = 24f;

        // ----------------------------------------------------------
        //  SINGLETON
        // ----------------------------------------------------------

        // ----------------------------------------------------------
        //  PRIVATE STATE
        // ----------------------------------------------------------

        private float _sessionTime;
        private double _sessionClockSampleSeconds = UnsampledSessionClock;
        private int   _completedMilestones; // bitovaya maska
        private bool  _registered;
        private bool  _lateFrameRegistered;
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
        private bool  _starterToolReminderIssued;
        private bool  _hasLoreRouteContact;
        private float _nextContextualGuidanceTime;
        private WorldZoneDirector _worldZoneDirector;
        private BiomeMatrixDirector _biomeMatrixDirector;
        private IQuestSystem _cachedQuestManager;
        private IAtlasSignalReadModel _cachedAtlasSignalSystem;
        private IEmergencyRelayRouteReadModel _cachedEmergencyRelayDirector;
        private IAudioLogRuntime _cachedAudioLogSystem;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private ILocalizationTextReadModel _cachedLocalization;
        private WorldZoneAnchor _lastObservedZone;
        private bool _lastContextStarterToolCompleted;
        private bool _lastContextResourceCompleted;
        private bool _lastContextDepthCompleted;
        private bool _lastContextLoreContact;
        private uint _lastServiceRelayGuidanceHash;
        private HectonSurvivalSystem _survivalSystem;
        private uint _firstModuleZoneDiscoveryHash;
        private uint _arrivalQuestHash;
        private uint _starterToolQuestHash;
        private uint _firstResourceQuestHash;
        private uint _firstDepthQuestHash;
        private int _starterToolItemHash;
        private int _firstResourceItemHash;
        private bool _firstResourceIsCopper;
        private int _firstCraftResultItemHash0;
        private int _firstCraftResultItemHash1;
        private int _firstCraftResultItemHash2;
        private int _firstCraftResultItemHash3;
        private int _firstCraftResultItemHash4;
        private int _firstCraftResultItemHash5;
        private bool _hotSwapRegistered;
        private bool _saveRegistered;
        private bool _craftingEventRegistered;
        private bool _narrativeEventRegistered;
        private bool _questEventRegistered;
        private bool _scanEventRegistered;
        private bool _interactionEventRegistered;
        private bool _audioLogEventRegistered;
        private bool _runtimeOwnerAborted;
        private ISaveService _saveService;
        private ISaveService _registeredSaveService;
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

        private const float MinEarnedOrientationTime = 75f;

        // The dispatcher slow lane is NOT a fixed 2 Hz lane. ITickable.cs documents it as the 10 Hz slow
        // cadence, and SystemDispatcher.ResolveSlowTickIntervalSeconds actually returns 0.1 s normally,
        // 0.2 s while thermal-critical, 1.0 s during a homeostasis emergency, and a GlobalQualityWeight
        // dependent lerp between 0.1 s and 0.2 s while the simulation bucketer idles. Counting ticks
        // therefore cannot measure the authored first-hour curve, and it would make the persisted
        // firstHourSessionTime a function of the player's hardware tier. Session time is sampled from the
        // monotonic dispatcher clock instead, and each tick's contribution is capped at the lane's own
        // worst-case interval: a larger gap means a pause, a scene load, or a hitch, and real seconds the
        // simulation never ran are not billed to the player's hour.
        private const float MaxSessionClockAdvanceSeconds = 1f;

        // Sentinel for "the session clock has not been sampled since enable/load"; the first tick after it
        // establishes the baseline and contributes no session time of its own.
        private const double UnsampledSessionClock = -1d;

        private const string DataCopperItemId = "Data_Copper";
        private const string ShadowEventDiscoveryId = "first_hour_shadow_event";
        private const string FirstColonyModuleDiscoveryId = "first_colony_module_spotted";
        private static readonly int _dataCopperItemHash = LocHash.Compute(DataCopperItemId);
        private static readonly uint _shadowEventDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(ShadowEventDiscoveryId);
        private static readonly uint _firstColonyModuleDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(FirstColonyModuleDiscoveryId);
        private const string MsgResourceShelfRead =
            "HOLD THE READABLE EDGE. COPPER VEINS SIT LOWER AND OFF TO THE SIDE OF THE SAFEST SHELF.";
        private const string MsgStarterResourceShelfRead =
            "HOLD THE READABLE EDGE. SHALLOW GLASS AND KELP BUILD THE DRILL; COPPER COMES AFTER THE VEIN OPENS.";
        private const string MsgStarterToolReminder =
            "SCAN A RESOURCE NODE, FABRICATE THE SEAFLOOR DRILL, THEN OPEN COPPER VEINS.";
        private const string MsgFabricationFallback =
            "THE FABRICATOR CAN BUILD THE DRILL FROM SHALLOW GLASS AND FIBER. COPPER IS THE NEXT STEP, NOT THE FIRST COST.";
        private const string MsgReturnLoreRelay =
            "SERVICE NODES MAY STILL HOLD LOGS AND MARKERS. CHECK TERMINALS AND SIDE RACKS, NOT JUST RESOURCES.";
        private const string MsgDeeperRouteRead =
            "LOWER DOWN, ROUTE MATTERS MORE THAN GREED. KEEP THE EXIT SILHOUETTE AND A CALM AIR POCKET IN MIND.";
        private const string MsgModuleRouteRead =
            "NOW LOOK FOR A TRACE, NOT LOOSE SCRAP. RUINS, MODULES, AND SERVICE HALTS WILL GIVE YOU A REAL VECTOR.";
        private const string MsgStarterBackslideRead =
            "THE SHALLOWS ONLY BUY BREATHING ROOM NOW, NOT PROGRESS. REGROUP AND RETURN TO THE DEEPER ROUTE.";

        // ----------------------------------------------------------
        //  ISaveable
        // ----------------------------------------------------------

        public int SavePriority => 13;
        public int LoadPriority => 13;

        // ----------------------------------------------------------
        //  PUBLIC PROPERTIES
        // ----------------------------------------------------------

        public float SessionTime => _sessionTime;
        public bool IsFirstHourComplete => IsMilestoneComplete(FirstHourMilestone.HumCloser);

        public int NotificationQueueDropCount => _notificationQueueDropCount;

        public int NotificationPushMissCount => _notificationPushMissCount;

        public bool IsMilestoneComplete(FirstHourMilestone m)
            => (_completedMilestones & (1 << (int)m)) != 0;

        public bool IsFirstHourMilestoneComplete(int milestoneCode)
        {
            if ((uint)milestoneCode > (uint)FirstHourMilestone.HumCloser)
                return false;

            return IsMilestoneComplete((FirstHourMilestone)milestoneCode);
        }

        /// <summary>
        /// Registers a confirmed service-relay route contact for first-hour pacing.
        /// </summary>
        public void RegisterServiceRelayRouteContact()
        {
            if (_runtimeOwnerAborted)
                return;

            _hasLoreRouteContact = true;
        }

        // ----------------------------------------------------------
        //  LIFECYCLE
        // ----------------------------------------------------------

        private void Awake()
        {
            RefreshCachedHashes();
        }

        private void OnEnable()
        {
            // Re-baseline the session clock: the wall-clock span this director spent disabled is not
            // first-hour playtime.
            _sessionClockSampleSeconds = UnsampledSessionClock;

            if (!TryRegisterService())
                return;

            TryRegister();
            TryRegisterLateFrameTick();
            CacheRuntimeServices();
            TryRegisterHotSwapListener();
            TryRegisterSaveParticipant();

            ResolveSurvivalSystem();
            ResolveWorldContext(force: true);
            SynchronizeContextFromRuntimeSystems();

            TryRegisterRuntimeEventListeners();
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregister();
            TryUnregisterLateFrameTick();
            TryUnregisterService();
            TryUnregisterSaveParticipant();

            TryUnregisterRuntimeEventListeners();
            TryUnregisterHotSwapListener();
            ClearCachedRuntimeServices();
            _lastObservedZone = null;
            _nextContextualGuidanceTime = 0f;
            _sessionClockSampleSeconds = UnsampledSessionClock;
            _lastContextResourceCompleted = false;
            _lastContextDepthCompleted = false;
            _lastContextLoreContact = false;
            _lastServiceRelayGuidanceHash = 0u;
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregister();
            TryUnregisterLateFrameTick();
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();
            TryUnregisterRuntimeEventListeners();
            TryUnregisterService();
            ClearCachedRuntimeServices();
        }

        private void Start()
        {
            if (_runtimeOwnerAborted || !TryRegisterService())
                return;

            TryRegister();
            CacheRuntimeServices();
            TryRegisterHotSwapListener();
            TryRegisterSaveParticipant();
            RefreshCachedHashes();
            ResolveSurvivalSystem();
            ResolveWorldContext(force: true);
            SynchronizeContextFromRuntimeSystems();
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

        public void LateFrameTick()
        {
            if (_runtimeOwnerAborted)
                return;

            ConsumeCraftingCompletedSignals();
            ConsumeItemAcquiredSignals();
            FlushQueuedNotifications();
        }

        private void TryRegisterLateFrameTick()
        {
            if (_runtimeOwnerAborted || _lateFrameRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void TryUnregisterLateFrameTick(bool clearQueuedNotifications = true)
        {
            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _lateFrameRegistered = false;
            }

            if (clearQueuedNotifications)
                ClearQueuedNotifications();
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

        private void ClearQueuedNotifications()
        {
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

            FirstHourDirector registeredRuntime = GlobalRegistry.FirstHour;
            if (IsFirstHourRuntimeUsable(registeredRuntime))
            {
                AbortDuplicateRuntimeOwner();
                return false;
            }

            if (!ReferenceEquals(registeredRuntime, null) && !ReferenceEquals(registeredRuntime, this))
                GlobalRegistry.UnregisterFirstHourRuntime(registeredRuntime);

            GlobalRegistry.RegisterFirstHourRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.FirstHour, this);
            if (!_serviceRegistered)
            {
                AbortDuplicateRuntimeOwner();
                return false;
            }

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
                    TryUnregisterLateFrameTick(clearQueuedNotifications: false);
                    if (currentService != null && isActiveAndEnabled)
                    {
                        TryRegister();
                        TryRegisterLateFrameTick();
                    }
                    break;
                case GlobalRegistryServiceSlot.QuestRuntime:
                    _cachedQuestManager = currentService as IQuestSystem;
                    break;
                case GlobalRegistryServiceSlot.AtlasSignalRuntime:
                    _cachedAtlasSignalSystem = currentService as IAtlasSignalReadModel;
                    break;
                case GlobalRegistryServiceSlot.EmergencyRelayRuntime:
                    _cachedEmergencyRelayDirector = currentService as IEmergencyRelayRouteReadModel;
                    break;
                case GlobalRegistryServiceSlot.AudioLogRuntime:
                    CacheAudioLogSystem(currentService as IAudioLogRuntime);
                    break;
                case GlobalRegistryServiceSlot.Player:
                    CachePlayerContext(
                        currentService as IPlayerRuntimeContext,
                        previousService as IPlayerRuntimeContext);
                    break;
                case GlobalRegistryServiceSlot.BiomeMatrixRuntime:
                    _biomeMatrixDirector = currentService as BiomeMatrixDirector;
                    if (_biomeMatrixDirector == null || !_biomeMatrixDirector.isActiveAndEnabled)
                    {
                        _biomeMatrixDirector = null;
                        WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref _biomeMatrixDirector);
                    }
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _cachedLocalization = currentService as ILocalizationTextReadModel;
                    break;
                case GlobalRegistryServiceSlot.Save:
                    TryUnregisterSaveParticipant();
                    _saveService = currentService as ISaveService;
                    TryRegisterSaveParticipant();
                    break;
            }
        }

        private void CacheRuntimeServices()
        {
            if (_runtimeOwnerAborted)
                return;

            _cachedQuestManager = GlobalRegistry.QuestSystem;
            _cachedAtlasSignalSystem = Hecton8.Core.GlobalRegistry.AtlasSignalReadModel;
            _cachedEmergencyRelayDirector = Hecton8.Core.GlobalRegistry.EmergencyRelayReadModel;
            CacheAudioLogSystem(Hecton8.Core.GlobalRegistry.AudioLogRuntime);
            CachePlayerContext(Hecton8.Core.GlobalRegistry.Player, null);
            _cachedLocalization = Hecton8.Core.GlobalRegistry.LocalizationText;
            _saveService = Hecton8.Core.GlobalRegistry.Save;
        }

        private void ClearCachedRuntimeServices()
        {
            _cachedQuestManager = null;
            _cachedAtlasSignalSystem = null;
            _cachedEmergencyRelayDirector = null;
            _cachedAudioLogSystem = null;
            _cachedPlayerContext = null;
            _survivalSystem = null;
            _cachedLocalization = null;
            _saveService = null;
        }

        private void CacheAudioLogSystem(IAudioLogRuntime audioLogSystem)
        {
            if (_runtimeOwnerAborted)
            {
                _cachedAudioLogSystem = null;
                return;
            }

            _cachedAudioLogSystem = IsAudioLogRuntimeUsable(audioLogSystem) ? audioLogSystem : null;
        }

        private IAudioLogRuntime ResolveAudioLogSystem()
        {
            IAudioLogRuntime audioLogSystem = _cachedAudioLogSystem;
            if (IsAudioLogRuntimeUsable(audioLogSystem))
                return audioLogSystem;

            _cachedAudioLogSystem = null;
            return null;
        }

        private static bool IsAudioLogRuntimeUsable(IAudioLogRuntime audioLogSystem)
        {
            if (audioLogSystem == null || !audioLogSystem.IsAudioLogRuntimeReady)
                return false;

            if (audioLogSystem is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private void CachePlayerContext(
            IPlayerRuntimeContext currentPlayerContext,
            IPlayerRuntimeContext previousPlayerContext)
        {
            if (_runtimeOwnerAborted)
                return;

            if (previousPlayerContext != null &&
                ReferenceEquals(_survivalSystem, previousPlayerContext.SurvivalSystem))
            {
                _survivalSystem = null;
            }

            _cachedPlayerContext = currentPlayerContext;
            HectonSurvivalSystem contextSurvival = currentPlayerContext != null
                ? currentPlayerContext.SurvivalSystem
                : null;

            if (contextSurvival != null)
                _survivalSystem = contextSurvival;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_runtimeOwnerAborted || _hotSwapRegistered)
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
            _saveRegistered = false;
        }

        private void TryRegisterRuntimeEventListeners()
        {
            if (_runtimeOwnerAborted)
                return;

            if (!_craftingEventRegistered)
            {
                CraftingEvents.Register(this);
                _craftingEventRegistered = true;
            }

            if (!_narrativeEventRegistered)
            {
                NarrativeEvents.Register(this);
                _narrativeEventRegistered = true;
            }

            if (!_questEventRegistered)
            {
                QuestEvents.Register(this);
                _questEventRegistered = true;
            }

            if (!_scanEventRegistered)
            {
                ScanEvents.Register(this);
                _scanEventRegistered = true;
            }

            if (!_interactionEventRegistered)
            {
                InteractionEvents.Register(this);
                _interactionEventRegistered = true;
            }

            if (!_audioLogEventRegistered)
            {
                AudioLogEvents.Register(this);
                _audioLogEventRegistered = true;
            }
        }

        private void TryUnregisterRuntimeEventListeners()
        {
            if (_craftingEventRegistered)
            {
                CraftingEvents.Unregister(this);
                _craftingEventRegistered = false;
            }

            if (_narrativeEventRegistered)
            {
                NarrativeEvents.Unregister(this);
                _narrativeEventRegistered = false;
            }

            if (_questEventRegistered)
            {
                QuestEvents.Unregister(this);
                _questEventRegistered = false;
            }

            if (_scanEventRegistered)
            {
                ScanEvents.Unregister(this);
                _scanEventRegistered = false;
            }

            if (_interactionEventRegistered)
            {
                InteractionEvents.Unregister(this);
                _interactionEventRegistered = false;
            }

            if (_audioLogEventRegistered)
            {
                AudioLogEvents.Unregister(this);
                _audioLogEventRegistered = false;
            }
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            FirstHourDirector registeredRuntime = GlobalRegistry.FirstHour;
            if (ReferenceEquals(registeredRuntime, this))
                return false;

            if (IsFirstHourRuntimeUsable(registeredRuntime))
            {
                AbortDuplicateRuntimeOwner();
                return true;
            }

            if (!ReferenceEquals(registeredRuntime, null))
                GlobalRegistry.UnregisterFirstHourRuntime(registeredRuntime);

            return false;
        }

        private void AbortDuplicateRuntimeOwner()
        {
            TryUnregister();
            TryUnregisterLateFrameTick();
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();
            TryUnregisterRuntimeEventListeners();
            ClearCachedRuntimeServices();
            ClearQueuedNotifications();
            _runtimeOwnerAborted = true;
            _registered = false;
            _lateFrameRegistered = false;
            _serviceRegistered = false;
            _hotSwapRegistered = false;
            _registeredSaveService = null;
            _saveRegistered = false;
            _craftingEventRegistered = false;
            _narrativeEventRegistered = false;
            _questEventRegistered = false;
            _scanEventRegistered = false;
            _interactionEventRegistered = false;
            _audioLogEventRegistered = false;
            enabled = false;
        }

        private static bool IsFirstHourRuntimeUsable(FirstHourDirector director)
        {
            return !ReferenceEquals(director, null) &&
                   director != null &&
                   director._serviceRegistered &&
                   director.isActiveAndEnabled &&
                   !director._runtimeOwnerAborted;
        }

        // ----------------------------------------------------------
        //  ISlowTickable
        // ----------------------------------------------------------

        /// <summary>
        /// Advances the first-hour session clock by the real seconds elapsed between two consecutive
        /// dispatcher slow ticks. Pure function: no Unity types, no state, no allocation.
        /// </summary>
        /// <param name="currentSessionSeconds">Session seconds accumulated so far.</param>
        /// <param name="previousSampleSeconds">Previous monotonic clock sample, or a negative sentinel when unsampled.</param>
        /// <param name="nowSeconds">Current monotonic clock reading.</param>
        /// <param name="maxAdvanceSeconds">Largest real gap that may be billed to one tick.</param>
        /// <returns>Updated session seconds.</returns>
        public static float AdvanceSessionClockSeconds(
            float currentSessionSeconds,
            double previousSampleSeconds,
            double nowSeconds,
            float maxAdvanceSeconds)
        {
            // Unsampled baseline: the caller records nowSeconds and this tick buys no session time.
            if (previousSampleSeconds < 0d)
                return currentSessionSeconds;

            double deltaSeconds = nowSeconds - previousSampleSeconds;

            // Written as a negated comparison so a NaN clock reading falls through unchanged rather than
            // poisoning the persisted session time. Several catch-up substeps inside one frame read the
            // same dispatcher time snapshot, so their delta is zero and the clock advances once per frame.
            if (!(deltaSeconds > 0d))
                return currentSessionSeconds;

            if (deltaSeconds > maxAdvanceSeconds)
                deltaSeconds = maxAdvanceSeconds;

            return currentSessionSeconds + (float)deltaSeconds;
        }

        public void SlowTick()
        {
            if (_runtimeOwnerAborted)
                return;

            if (IsFirstHourComplete) return;

            double nowSeconds = SystemDispatcher.CurrentUnscaledTimeSeconds;
            _sessionTime = AdvanceSessionClockSeconds(
                _sessionTime,
                _sessionClockSampleSeconds,
                nowSeconds,
                MaxSessionClockAdvanceSeconds);
            _sessionClockSampleSeconds = nowSeconds;

            ResolveSurvivalSystem();
            ResolveWorldContext();

            float depth = ResolveCurrentDepthMeters();
            WorldZoneAnchor currentZone = _worldZoneDirector != null ? _worldZoneDirector.CurrentZone : null;
            int currentDepthTier = ResolveCurrentDepthTier(depth);
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
                _cachedQuestManager != null &&
                _cachedQuestManager.IsCompleted(_firstDepthQuestHash))
            {
                _firstModuleHintIssued = true;
                _firstModuleReminderIssued = true;
                QueueNotification(
                    ResolveLocalizedSpan(
                        LocalizationKeys.FIRST_HOUR_MODULE_SCAN_HINT,
                        "SCAN RUINS AND MODULES. SOMETHING INTACT IS STILL DOWN HERE."),
                    NotificationEventSeverity.Info);
            }

            TryIssueRetentionNudges();
            TryIssueContextualGuidance();

            CheckMilestone(
                FirstHourMilestone.HumCloser,
                ShouldTriggerHumCloser(atlasRevealStage));
        }

        // ----------------------------------------------------------
        //  PRIVATE
        // ----------------------------------------------------------

        private void CheckMilestone(FirstHourMilestone milestone, bool condition)
        {
            if (!condition) return;
            if (IsMilestoneComplete(milestone)) return;

            _completedMilestones |= (1 << (int)milestone);
            TriggerMilestone(milestone);
        }

        private void TriggerMilestone(FirstHourMilestone milestone)
        {
            FirstHourEvents.TryRaiseMilestone(milestone);

            switch (milestone)
            {
                case FirstHourMilestone.Orientation:
                    CompleteQuest(_arrivalQuestHash);
                    ActivateQuest(_starterToolQuestHash);
                    TryAdvanceStarterToolGoalFromRuntimeInventory();
                    TryAdvanceFirstResourceGoalFromRuntimeInventory();
                    break;

                case FirstHourMilestone.TheShadow:
                    // TEN — bolshaya, bystraya, sleva
                    // Director AI poluchaet narrative bonus (snizhenie tension posle straha)
                    NarrativeEvents.TryRaiseDiscoveryMade(_shadowEventDiscoveryHash);
                    break;

                case FirstHourMilestone.FirstModule:
                    NarrativeEvents.TryRaiseDiscoveryMade(_firstColonyModuleDiscoveryHash);
                    break;

            }

            LogMilestoneTriggered(milestone, _sessionTime);
        }

        private void HandleCraftCompleted(ItemData resultItem)
        {
            if (resultItem == null)
                return;

            if (resultItem.MatchesPersistentHash(_starterToolItemHash))
            {
                CompleteQuest(_starterToolQuestHash);
                _starterToolReminderIssued = true;
                ActivateQuest(_firstResourceQuestHash);
            }

            if (!IsAcceptedFirstCraftResultItem(resultItem))
                return;

            CheckMilestone(FirstHourMilestone.FirstCraft,
                !IsMilestoneComplete(FirstHourMilestone.FirstCraft));
        }

        private void ConsumeCraftingCompletedSignals()
        {
            if (IsMilestoneComplete(FirstHourMilestone.FirstCraft))
                return;

            ReadOnlySpan<CraftingCompletedSignal> signals = SignalBus<CraftingCompletedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                if (!IsAcceptedFirstCraftResultHash(signals[i].ResultItemHash))
                    continue;

                CheckMilestone(FirstHourMilestone.FirstCraft, true);
                return;
            }
        }

        // Drill and manual-pickup acquisition publish ItemAcquiredSignal on the double-buffered
        // SignalBus, not the legacy InteractionEvents.ItemCollected lane that OnInteractionEvent
        // listens to. Without this drain, copper mined by the drill or picked up off the seafloor
        // never advances quest_copper_sample, so the first-hour drill-blueprint gate hangs. The
        // hash-based quest advances below are idempotent (ActivateQuest/CompleteQuest guard on
        // IsActive/IsCompleted), so an item that ever hits both lanes cannot double-count.
        private void ConsumeItemAcquiredSignals()
        {
            if (!IsMilestoneComplete(FirstHourMilestone.Orientation))
                return;

            ReadOnlySpan<ItemAcquiredSignal> signals = SignalBus<ItemAcquiredSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                uint itemHash = signals[i].ItemHash;
                if (itemHash == 0u)
                    continue;

                if (_starterToolItemHash != 0 && itemHash == unchecked((uint)_starterToolItemHash))
                {
                    CompleteQuest(_starterToolQuestHash);
                    _starterToolReminderIssued = true;
                    ActivateQuest(_firstResourceQuestHash);
                    continue;
                }

                if (_firstResourceItemHash != 0 && itemHash == unchecked((uint)_firstResourceItemHash))
                {
                    CompleteQuest(_firstResourceQuestHash);
                    _firstResourceReminderIssued = true;
                    ActivateQuest(_firstDepthQuestHash);
                    _firstDepthReminderIssued = false;
                }
            }
        }

        private bool IsAcceptedFirstCraftResultItem(ItemData item)
        {
            return item != null &&
                   (item.MatchesPersistentHash(_firstCraftResultItemHash0) ||
                    item.MatchesPersistentHash(_firstCraftResultItemHash1) ||
                    item.MatchesPersistentHash(_firstCraftResultItemHash2) ||
                    item.MatchesPersistentHash(_firstCraftResultItemHash3) ||
                    item.MatchesPersistentHash(_firstCraftResultItemHash4) ||
                    item.MatchesPersistentHash(_firstCraftResultItemHash5));
        }

        private bool IsAcceptedFirstCraftResultHash(uint itemHash)
        {
            return itemHash != 0u &&
                   (MatchesCachedHash(itemHash, _firstCraftResultItemHash0) ||
                    MatchesCachedHash(itemHash, _firstCraftResultItemHash1) ||
                    MatchesCachedHash(itemHash, _firstCraftResultItemHash2) ||
                    MatchesCachedHash(itemHash, _firstCraftResultItemHash3) ||
                    MatchesCachedHash(itemHash, _firstCraftResultItemHash4) ||
                    MatchesCachedHash(itemHash, _firstCraftResultItemHash5));
        }

        private static bool MatchesCachedHash(uint itemHash, int cachedHash)
        {
            return cachedHash != 0 && itemHash == unchecked((uint)cachedHash);
        }

        public void OnNarrativeEvent(in NarrativeEventPayload payload)
        {
            if (_runtimeOwnerAborted)
                return;

            if ((NarrativeEventType)payload.EventType != NarrativeEventType.DiscoveryMade)
                return;

            if (!IsMilestoneComplete(FirstHourMilestone.FirstModule) &&
                payload.DiscoveryHash != 0u &&
                payload.DiscoveryHash == _firstModuleZoneDiscoveryHash)
            {
                CheckMilestone(FirstHourMilestone.FirstModule, true);
            }

            IEmergencyRelayRouteReadModel relayDirector = _cachedEmergencyRelayDirector;
            if (relayDirector != null && relayDirector.IsRelayDiscoveryHash(payload.DiscoveryHash))
                _hasLoreRouteContact = true;
        }

        public void OnScanEvent(in ScanEventPayload payload)
        {
            if (_runtimeOwnerAborted)
                return;

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
            if (_runtimeOwnerAborted)
                return;

            if ((QuestEventType)payload.EventType != QuestEventType.Completed)
                return;

            if (payload.QuestHashID == _firstDepthQuestHash)
            {
                _firstDepthReminderIssued = true;
                return;
            }

            if (payload.QuestHashID == _starterToolQuestHash)
            {
                _starterToolReminderIssued = true;
                ActivateQuest(_firstResourceQuestHash);
                return;
            }

            if (payload.QuestHashID != _firstResourceQuestHash)
                return;

            _firstResourceReminderIssued = true;
            ActivateQuest(_firstDepthQuestHash);
        }

        public void OnAudioLogEvent(in AudioLogEventPayload payload)
        {
            if (_runtimeOwnerAborted)
                return;

            if (payload.Type == AudioLogEventType.Discovered && payload.LogHash != 0u)
                _hasLoreRouteContact = true;
        }

        public void OnCraftingEvent(in CraftingEventPayload payload)
        {
            if (_runtimeOwnerAborted)
                return;

            if ((CraftingEventType)payload.EventType != CraftingEventType.CraftCompleted)
                return;

            if (!CraftingEvents.TryResolveItem(in payload, out ItemData resultItem))
                return;

            HandleCraftCompleted(resultItem);
        }

        public void OnInteractionEvent(in InteractionEventPayload payload)
        {
            if (_runtimeOwnerAborted)
                return;

            if ((InteractionEventType)payload.EventType != InteractionEventType.ItemCollected)
                return;

            if (!InteractionEvents.TryResolveItem(in payload, out ItemData item))
                return;

            InteractionEvents.TryResolveInteractor(in payload, out Transform interactor);
            HandleItemCollected(item, payload.Quantity, interactor);
        }

        private void HandleItemCollected(ItemData item, int quantity, Transform interactor)
        {
            if (!IsMilestoneComplete(FirstHourMilestone.Orientation) || item == null)
            {
                return;
            }

            if (item.MatchesPersistentHash(_starterToolItemHash))
            {
                CompleteQuest(_starterToolQuestHash);
                _starterToolReminderIssued = true;
                ActivateQuest(_firstResourceQuestHash);
                return;
            }

            if (!item.MatchesPersistentHash(_firstResourceItemHash))
                return;

            CompleteQuest(_firstResourceQuestHash);
            _firstResourceReminderIssued = true;
            ActivateQuest(_firstDepthQuestHash);
            _firstDepthReminderIssued = false;
        }

        // ----------------------------------------------------------
        //  ISaveable
        // ----------------------------------------------------------

        private bool ResolveSurvivalSystem()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (_survivalSystem != null)
                return true;

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            CachePlayerContext(playerContext, null);
            return _survivalSystem != null;
        }

        private float ResolveCurrentDepthMeters()
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
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

        private void ResolveWorldContext(bool force = false)
        {
            if (_runtimeOwnerAborted)
                return;

            if (force || _worldZoneDirector == null || !_worldZoneDirector.isActiveAndEnabled)
            {
                _worldZoneDirector = null;
                WorldRuntimeReferenceUtility.TryResolveWorldZoneDirector(ref _worldZoneDirector);
            }

            if (force || _biomeMatrixDirector == null || !_biomeMatrixDirector.isActiveAndEnabled)
            {
                _biomeMatrixDirector = null;
                WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref _biomeMatrixDirector);
            }
        }

        private void SynchronizeContextFromRuntimeSystems()
        {
            if (_runtimeOwnerAborted)
                return;

            IAudioLogRuntime audioLogSystem = ResolveAudioLogSystem();
            if (audioLogSystem != null && audioLogSystem.DiscoveredAudioLogCount > 0)
                _hasLoreRouteContact = true;

            IEmergencyRelayRouteReadModel relayDirector = _cachedEmergencyRelayDirector;
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
            H8Debug.Log($"[FirstHour] Milestone: {milestone} (t={sessionTime:F0}s)");
#endif
        }

        public void PopulateSaveData(SaveData data)
        {
            if (_runtimeOwnerAborted || data == null) return;
            data.firstHourSessionTime = _sessionTime;
            data.firstHourMilestones  = _completedMilestones;
            data.firstHourGuidanceFlags = BuildGuidanceStateFlags();
        }

        public void LoadFromSaveData(SaveData data)
        {
            ClearQueuedNotifications();
            if (_runtimeOwnerAborted || data == null) return;
            _sessionTime          = data.firstHourSessionTime;
            _completedMilestones  = data.firstHourMilestones;
            ApplyGuidanceStateFlags(data.firstHourGuidanceFlags);
            _firstModuleHintIssued |= _sessionTime >= firstModuleTime ||
                                      IsMilestoneComplete(FirstHourMilestone.FirstModule);
            _nextContextualGuidanceTime = 0f;
            // The restored session time is the new baseline; the real seconds spent in the menu and in the
            // load itself belong to neither the old run nor the new one.
            _sessionClockSampleSeconds = UnsampledSessionClock;
            _lastObservedZone = null;
            _lastContextStarterToolCompleted = false;
            _lastContextResourceCompleted = false;
            _lastContextDepthCompleted = false;
            _lastContextLoreContact = false;
            _lastServiceRelayGuidanceHash = 0u;
            SynchronizeContextFromRuntimeSystems();
            SynchronizeAtlasMilestonesFromRuntime();
            SynchronizeEarlyQuestState();
            SynchronizeFirstHourRouteQuestStateFromSaveData(data);
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
            if (_starterToolReminderIssued)
                flags |= GuidanceStateFlags.StarterToolReminderIssued;

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
            _starterToolReminderIssued = (flags & GuidanceStateFlags.StarterToolReminderIssued) != 0;
        }

        private void ActivateQuest(uint questHash)
        {
            if (questHash == 0u)
                return;

            IQuestSystem questManager = _cachedQuestManager;
            if (questManager == null)
                return;

            if (!questManager.IsActive(questHash) && !questManager.IsCompleted(questHash))
                questManager.ActivateQuest(questHash);
        }

        private void CompleteQuest(uint questHash)
        {
            if (questHash == 0u)
                return;

            IQuestSystem questManager = _cachedQuestManager;
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
            ActivateQuest(_starterToolQuestHash);
            TryAdvanceStarterToolGoalFromRuntimeInventory();
            TryAdvanceFirstResourceGoalFromRuntimeInventory();

            IQuestSystem questManager = _cachedQuestManager;
            if (questManager != null && questManager.IsCompleted(_starterToolQuestHash))
            {
                _starterToolReminderIssued = true;
                ActivateQuest(_firstResourceQuestHash);
            }

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
            IAtlasSignalReadModel atlasSignalSystem = _cachedAtlasSignalSystem;
            return atlasSignalSystem != null ? atlasSignalSystem.CurrentAtlasSignalRevealStage : 0;
        }

        private static bool ShouldTriggerFirstAnxiety(int atlasRevealStage)
        {
            return atlasRevealStage >= 1;
        }

        private static bool ShouldTriggerHumCloser(int atlasRevealStage)
        {
            return atlasRevealStage >= 2;
        }

        private void SynchronizeFirstHourRouteQuestStateFromSaveData(SaveData data)
        {
            if (data == null || !IsMilestoneComplete(FirstHourMilestone.Orientation))
                return;

            if (SaveInventoryContainsItem(data.inventory, _starterToolItemHash))
            {
                CompleteQuest(_starterToolQuestHash);
                _starterToolReminderIssued = true;
                ActivateQuest(_firstResourceQuestHash);
            }

            if (SaveInventoryContainsItem(data.inventory, _firstResourceItemHash))
            {
                CompleteQuest(_firstResourceQuestHash);
                _firstResourceReminderIssued = true;
                ActivateQuest(_firstDepthQuestHash);
                _firstDepthReminderIssued = false;
            }
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

        private void TryAdvanceStarterToolGoalFromRuntimeInventory()
        {
            if (!RuntimeInventoryContainsItem(_starterToolItemHash))
                return;

            CompleteQuest(_starterToolQuestHash);
            _starterToolReminderIssued = true;
            ActivateQuest(_firstResourceQuestHash);
        }

        private void TryAdvanceFirstResourceGoalFromRuntimeInventory()
        {
            IQuestSystem questManager = _cachedQuestManager;
            if (questManager != null &&
                _starterToolQuestHash != 0u &&
                !questManager.IsCompleted(_starterToolQuestHash) &&
                !RuntimeInventoryContainsItem(_firstResourceItemHash))
            {
                return;
            }

            if (!RuntimeInventoryContainsItem(_firstResourceItemHash))
                return;

            CompleteQuest(_firstResourceQuestHash);
            _firstResourceReminderIssued = true;
            ActivateQuest(_firstDepthQuestHash);
            _firstDepthReminderIssued = false;
        }

        private bool RuntimeInventoryContainsItem(int itemHashId)
        {
            if (itemHashId == 0 ||
                !TryGetRuntimeInventory(out PlayerInventory inventory) ||
                inventory == null)
            {
                return false;
            }

            InventoryGrid grid = inventory.Grid;
            if (grid == null)
                return false;

            int columns = grid.Columns;
            int rows = grid.Rows;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int cellItemHashId = inventory.GetItemHashAt(x, y);
                    if (cellItemHashId != itemHashId)
                        continue;

                    return true;
                }
            }

            return false;
        }

        private void TryIssueRetentionNudges()
        {
            if (!IsMilestoneComplete(FirstHourMilestone.Orientation))
                return;

            IQuestSystem questManager = _cachedQuestManager;
            if (questManager == null)
                return;

            bool starterToolCompleted = questManager.IsCompleted(_starterToolQuestHash);
            if (!_starterToolReminderIssued &&
                !starterToolCompleted &&
                _sessionTime >= firstResourceReminderTime)
            {
                _starterToolReminderIssued = true;
                QueueNotification(MsgStarterToolReminder.AsSpan(), NotificationEventSeverity.Info);
                return;
            }

            if (!_firstResourceReminderIssued &&
                starterToolCompleted &&
                !questManager.IsCompleted(_firstResourceQuestHash) &&
                _sessionTime >= firstResourceReminderTime)
            {
                _firstResourceReminderIssued = true;
                ReadOnlySpan<char> reminderMessage = _firstResourceIsCopper
                    ? ResolveLocalizedSpan(
                        LocalizationKeys.FIRST_HOUR_RESOURCE_REMINDER_COPPER,
                        "LOOK FOR COPPER IN THE DEBRIS AND AROUND THE ROCKS. WITHOUT IT, YOU DO NOT MOVE THE CHAIN FORWARD.")
                    : ResolveLocalizedSpan(
                        LocalizationKeys.FIRST_HOUR_RESOURCE_REMINDER_GENERIC,
                        "LOOK FOR THE FIRST CORE MATERIAL IN THE DEBRIS AND ALONG READABLE ROCK FACES. WITHOUT IT, THE CHAIN STOPS HERE.");
                QueueNotification(reminderMessage, NotificationEventSeverity.Info);
            }

            if (!_firstDepthReminderIssued &&
                questManager.IsCompleted(_firstResourceQuestHash) &&
                questManager.IsActive(_firstDepthQuestHash) &&
                !questManager.IsCompleted(_firstDepthQuestHash) &&
                _sessionTime >= firstDepthReminderTime)
            {
                _firstDepthReminderIssued = true;
                QueueNotification(
                    ResolveLocalizedSpan(
                        LocalizationKeys.FIRST_HOUR_DEPTH_REMINDER,
                        "THE FIRST REAL FIND IS LOWER. GO DEEPER, BUT DO NOT LOSE THE WAY OUT."),
                    NotificationEventSeverity.Info);
            }

            if (!_firstModuleReminderIssued &&
                questManager.IsCompleted(_firstDepthQuestHash) &&
                !IsMilestoneComplete(FirstHourMilestone.FirstModule) &&
                _sessionTime >= firstModuleReminderTime)
            {
                _firstModuleReminderIssued = true;

                WorldZoneAnchor currentZone = _worldZoneDirector != null ? _worldZoneDirector.CurrentZone : null;
                HectonBiomeMatrixProfile currentBiome = ResolveCurrentBiomeProfile(currentZone);
                QueueNotification(
                    ResolveModuleRouteGuidanceMessage(currentZone, currentBiome),
                    NotificationEventSeverity.Info);
            }
        }

        private void TryIssueContextualGuidance()
        {
            if (!IsMilestoneComplete(FirstHourMilestone.Orientation))
                return;

            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (now < _nextContextualGuidanceTime)
                return;

            IQuestSystem questManager = _cachedQuestManager;
            if (questManager == null)
                return;

            SynchronizeContextFromRuntimeSystems();

            if (TryIssueServiceRelayGuidance())
                return;

            ResolveWorldContext();
            WorldZoneAnchor currentZone = _worldZoneDirector != null ? _worldZoneDirector.CurrentZone : null;
            if (currentZone == null)
                return;

            int currentDepthTier = ResolveCurrentDepthTier(ResolveCurrentDepthMeters());
            HectonBiomeMatrixProfile currentBiome = ResolveCurrentBiomeProfile(currentZone);
            bool starterToolCompleted = questManager.IsCompleted(_starterToolQuestHash);
            bool resourceCompleted = questManager.IsCompleted(_firstResourceQuestHash);
            bool depthCompleted = questManager.IsCompleted(_firstDepthQuestHash);
            bool loreRouteContact = _hasLoreRouteContact;

            bool zoneChanged = !ReferenceEquals(currentZone, _lastObservedZone);
            bool stageChanged =
                starterToolCompleted != _lastContextStarterToolCompleted ||
                resourceCompleted != _lastContextResourceCompleted ||
                depthCompleted != _lastContextDepthCompleted ||
                loreRouteContact != _lastContextLoreContact;

            _lastObservedZone = currentZone;
            _lastContextStarterToolCompleted = starterToolCompleted;
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
            IEmergencyRelayRouteReadModel relayDirector = _cachedEmergencyRelayDirector;
            if (relayDirector == null)
            {
                return false;
            }

            uint relayHash = 0u;
            if (relayDirector.TryReadActiveRouteTarget(out EmergencyRelayRouteTargetSnapshot routeTarget))
            {
                relayHash = routeTarget.RelayHash;
                if (relayHash != 0u && _lastServiceRelayGuidanceHash == relayHash)
                    return false;
            }

            if (!relayDirector.TryBuildContextualGuidanceMessageSpan(out ReadOnlySpan<char> relayMessage))
                return false;

            PublishContextualInfo(relayMessage);
            if (relayHash != 0u)
                _lastServiceRelayGuidanceHash = relayHash;
            return true;
        }

        private bool TryIssueEarlyResourceZoneGuidance(
            IQuestSystem questManager,
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome)
        {
            if (_starterResourcesZoneHintIssued ||
                (questManager.IsCompleted(_starterToolQuestHash) && questManager.IsCompleted(_firstResourceQuestHash)) ||
                currentZone.Kind != WorldZoneAnchor.ZoneKind.Resources)
            {
                return false;
            }

            _starterResourcesZoneHintIssued = true;
            PublishContextualInfo(
                questManager.IsCompleted(_starterToolQuestHash)
                    ? ResolveResourceZoneGuidanceMessage(currentZone, currentBiome)
                    : MsgStarterResourceShelfRead.AsSpan());
            return true;
        }

        private bool TryIssueFabricationReturnGuidance(
            IQuestSystem questManager,
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome)
        {
            if (currentZone.Kind != WorldZoneAnchor.ZoneKind.Fabrication)
                return false;

            if (!_starterFabricationFallbackHintIssued &&
                !questManager.IsCompleted(_starterToolQuestHash))
            {
                _starterFabricationFallbackHintIssued = true;
                PublishContextualInfo(MsgFabricationFallback.AsSpan());
                return true;
            }

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
            IQuestSystem questManager,
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
            IQuestSystem questManager,
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
            IQuestSystem questManager,
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

        private void PublishContextualInfo(ReadOnlySpan<char> message)
        {
            if (message.IsEmpty)
                return;

            QueueNotification(message, NotificationEventSeverity.Info);
            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            _nextContextualGuidanceTime = now + math.max(0f, contextualGuidanceCooldown);
        }

        private HectonBiomeMatrixProfile ResolveCurrentBiomeProfile(WorldZoneAnchor currentZone)
        {
            if (currentZone != null && currentZone.DominantMatrixBiome != null)
                return currentZone.DominantMatrixBiome;

            return TryResolveLiveBiomeMatrixDirector(out BiomeMatrixDirector matrixDirector)
                ? matrixDirector.CurrentProfile
                : null;
        }

        private int ResolveCurrentDepthTier(float depthMeters)
        {
            if (math.isfinite(depthMeters) && depthMeters >= 0f)
                return ResolveFallbackDepthTier(depthMeters);

            if (TryResolveLiveBiomeMatrixDirector(out BiomeMatrixDirector matrixDirector))
                return math.max(1, matrixDirector.CurrentDepthTier);

            return ResolveFallbackDepthTier(depthMeters);
        }

        private bool TryResolveLiveBiomeMatrixDirector(out BiomeMatrixDirector matrixDirector)
        {
            matrixDirector = _biomeMatrixDirector;
            if (matrixDirector == null || !matrixDirector.isActiveAndEnabled)
            {
                matrixDirector = null;
                WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref matrixDirector);
                _biomeMatrixDirector = matrixDirector;
            }

            return matrixDirector != null && matrixDirector.isActiveAndEnabled;
        }

        private static int ResolveFallbackDepthTier(float depth)
        {
            if (!math.isfinite(depth) || depth <= 0f)
                return 1;
            if (depth <= 300f)
                return 2;
            if (depth <= 600f)
                return 3;
            if (depth <= 1000f)
                return 4;
            if (depth <= 1500f)
                return 5;
            if (depth <= 2000f)
                return 6;
            if (depth <= 2500f)
                return 7;
            if (depth <= 3000f)
                return 8;
            if (depth <= 3500f)
                return 9;
            if (depth >= 14000f)
                return 27;

            float clamped = math.clamp(depth, 3500f, 14000f);
            float normalized = (clamped - 3500f) / 10500f;
            int tier = 10 + (int)math.floor(normalized * 17f);
            return math.clamp(tier, 10, 26);
        }

        private ReadOnlySpan<char> ResolveResourceZoneGuidanceMessage(
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome)
        {
            WorldZoneProfile zoneProfile = currentZone != null ? currentZone.Profile : null;
            WorldMotivationProfile motivation = zoneProfile != null ? zoneProfile.motivationProfile : null;
            WorldSandboxAttractionProfile sandbox = zoneProfile != null ? zoneProfile.sandboxAttractionProfile : null;
            WorldExpeditionLoopProfile expedition = zoneProfile != null ? zoneProfile.expeditionLoopProfile : null;

            return SelectFirstNonEmptySpan(
                motivation != null ? motivation.resourceNeed : null,
                sandbox != null ? sandbox.ambientValue : null,
                expedition != null ? expedition.softProgressionPull : null,
                currentBiome != null ? currentBiome.commonRewardHook : null,
                currentBiome != null ? currentBiome.landmarkGuidance : null,
                ResolveLocalizedSpan(LocalizationKeys.FIRST_HOUR_RESOURCE_SHELF_READ, MsgResourceShelfRead));
        }

        private ReadOnlySpan<char> ResolveFabricationFallbackMessage(
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome)
        {
            WorldZoneProfile zoneProfile = currentZone != null ? currentZone.Profile : null;
            WorldSandboxAttractionProfile sandbox = zoneProfile != null ? zoneProfile.sandboxAttractionProfile : null;
            WorldExpeditionLoopProfile expedition = zoneProfile != null ? zoneProfile.expeditionLoopProfile : null;

            return SelectFirstNonEmptySpan(
                expedition != null ? expedition.reliefBeat : null,
                expedition != null ? expedition.playerPromise : null,
                sandbox != null ? sandbox.shelterRead : null,
                currentBiome != null ? currentBiome.safePocketIdentity : null,
                null,
                ResolveLocalizedSpan(LocalizationKeys.FIRST_HOUR_FABRICATION_FALLBACK, MsgFabricationFallback));
        }

        private ReadOnlySpan<char> ResolveReturnLoreGuidanceMessage(
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome)
        {
            WorldZoneProfile zoneProfile = currentZone != null ? currentZone.Profile : null;
            WorldMotivationProfile motivation = zoneProfile != null ? zoneProfile.motivationProfile : null;
            WorldSandboxAttractionProfile sandbox = zoneProfile != null ? zoneProfile.sandboxAttractionProfile : null;

            return SelectFirstNonEmptySpan(
                motivation != null ? motivation.storyPull : null,
                sandbox != null ? sandbox.storyLure : null,
                currentBiome != null ? currentBiome.rareRewardHook : null,
                null,
                null,
                ResolveLocalizedSpan(LocalizationKeys.FIRST_HOUR_RETURN_LORE_RELAY, MsgReturnLoreRelay));
        }

        private ReadOnlySpan<char> ResolveDeeperRouteGuidanceMessage(
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome)
        {
            WorldZoneProfile zoneProfile = currentZone != null ? currentZone.Profile : null;
            WorldSandboxAttractionProfile sandbox = zoneProfile != null ? zoneProfile.sandboxAttractionProfile : null;
            WorldExpeditionLoopProfile expedition = zoneProfile != null ? zoneProfile.expeditionLoopProfile : null;

            return SelectFirstNonEmptySpan(
                sandbox != null ? sandbox.deepLure : null,
                expedition != null ? expedition.softProgressionPull : null,
                currentBiome != null ? currentBiome.landmarkGuidance : null,
                currentBiome != null ? currentBiome.rareRewardHook : null,
                null,
                ResolveLocalizedSpan(LocalizationKeys.FIRST_HOUR_DEEPER_ROUTE_READ, MsgDeeperRouteRead));
        }

        private ReadOnlySpan<char> ResolveModuleRouteGuidanceMessage(
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome)
        {
            WorldZoneProfile zoneProfile = currentZone != null ? currentZone.Profile : null;
            WorldMotivationProfile motivation = zoneProfile != null ? zoneProfile.motivationProfile : null;
            WorldSandboxAttractionProfile sandbox = zoneProfile != null ? zoneProfile.sandboxAttractionProfile : null;

            return SelectFirstNonEmptySpan(
                motivation != null ? motivation.storyPull : null,
                motivation != null ? motivation.curiosityPull : null,
                sandbox != null ? sandbox.storyLure : null,
                currentBiome != null ? currentBiome.landmarkGuidance : null,
                null,
                ResolveLocalizedSpan(LocalizationKeys.FIRST_HOUR_MODULE_ROUTE_READ, MsgModuleRouteRead));
        }

        private ReadOnlySpan<char> ResolveStarterBackslideMessage(
            WorldZoneAnchor currentZone,
            HectonBiomeMatrixProfile currentBiome)
        {
            WorldZoneProfile zoneProfile = currentZone != null ? currentZone.Profile : null;
            WorldExpeditionLoopProfile expedition = zoneProfile != null ? zoneProfile.expeditionLoopProfile : null;
            WorldMotivationProfile motivation = zoneProfile != null ? zoneProfile.motivationProfile : null;
            WorldSandboxAttractionProfile sandbox = zoneProfile != null ? zoneProfile.sandboxAttractionProfile : null;

            return SelectFirstNonEmptySpan(
                expedition != null ? expedition.playerPromise : null,
                expedition != null ? expedition.softProgressionPull : null,
                motivation != null ? motivation.storyPull : null,
                sandbox != null ? sandbox.deepLure : null,
                currentBiome != null ? currentBiome.landmarkGuidance : null,
                ResolveLocalizedSpan(LocalizationKeys.FIRST_HOUR_STARTER_BACKSLIDE_READ, MsgStarterBackslideRead));
        }

        private static ReadOnlySpan<char> SelectFirstNonEmptySpan(
            string optionA,
            string optionB,
            string optionC,
            string optionD,
            string optionE,
            ReadOnlySpan<char> fallback)
        {
            if (!string.IsNullOrWhiteSpace(optionA))
                return optionA.AsSpan();

            if (!string.IsNullOrWhiteSpace(optionB))
                return optionB.AsSpan();

            if (!string.IsNullOrWhiteSpace(optionC))
                return optionC.AsSpan();

            if (!string.IsNullOrWhiteSpace(optionD))
                return optionD.AsSpan();

            if (!string.IsNullOrWhiteSpace(optionE))
                return optionE.AsSpan();

            return fallback;
        }

        private bool TryGetRuntimeInventory(out PlayerInventory inventory)
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            inventory = playerContext != null ? playerContext.Inventory : null;
            return inventory != null;
        }

        private ReadOnlySpan<char> ResolveLocalizedSpan(string key, string fallback)
        {
            ILocalizationTextReadModel localization = _cachedLocalization;
            return localization != null
                ? localization.GetRawSpanOrFallback(LocHash.Compute(key), fallback.AsSpan())
                : fallback.AsSpan();
        }

        private void RefreshCachedHashes()
        {
            _firstModuleZoneDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(firstModuleZoneDiscoveryId);
            _arrivalQuestHash = QuestFlagHashKernel.ComputeStableHash(arrivalQuestId);
            _starterToolQuestHash = QuestFlagHashKernel.ComputeStableHash(starterToolQuestId);
            _firstResourceQuestHash = QuestFlagHashKernel.ComputeStableHash(firstResourceQuestId);
            _firstDepthQuestHash = QuestFlagHashKernel.ComputeStableHash(firstDepthQuestId);
            _starterToolItemHash = string.IsNullOrWhiteSpace(starterToolItemId)
                ? 0
                : LocHash.Compute(starterToolItemId);
            _firstResourceItemHash = string.IsNullOrWhiteSpace(firstResourceItemId)
                ? 0
                : LocHash.Compute(firstResourceItemId);
            _firstResourceIsCopper = _firstResourceItemHash != 0 && _firstResourceItemHash == _dataCopperItemHash;
            _firstCraftResultItemHash0 = ComputeOptionalHash(firstCraftResultItemId0);
            _firstCraftResultItemHash1 = ComputeOptionalHash(firstCraftResultItemId1);
            _firstCraftResultItemHash2 = ComputeOptionalHash(firstCraftResultItemId2);
            _firstCraftResultItemHash3 = ComputeOptionalHash(firstCraftResultItemId3);
            _firstCraftResultItemHash4 = ComputeOptionalHash(firstCraftResultItemId4);
            _firstCraftResultItemHash5 = ComputeOptionalHash(firstCraftResultItemId5);
        }

        private static int ComputeOptionalHash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? 0 : LocHash.Compute(value);
        }
    }
}
