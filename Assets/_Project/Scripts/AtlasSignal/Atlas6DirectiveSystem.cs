// ============================================================================
// HECTON-8 - Atlas6DirectiveSystem.cs
// Atlas-6 directive state and violation tracking.
//
// LORE BLOCK V:
//   Original directives in descending priority:
//   1. Preserve the Seeding mission.
//   2. Ensure survival of the human colony.
//   3. Study and adapt to the environment.
//   4. Maintain contact with Earth.
//
//   Failure state:
//   - Catastrophe cut contact, making directive #4 impossible.
//   - The colony was destroyed, making directive #2 impossible.
//   - Directives #1 and #3 remain.
//
//   New logic:
//   - Dead humans mean the ecosystem is damaged.
//   - Rebuild "humans" from available material.
//   - Biomechanical drones are an attempt to resurrect the colony.
//   - The player is a living human anomaly outside the original colony.
//   - Status: unidentified biological agent; ecosystem stability threat.
//
// ARCHITECTURE:
//   - Tracks player status from the Atlas-6 point of view.
//   - Publishes events on status changes.
//   - Integrates with HectonDirectorAI tension.
//   - ISaveable: persists status and interaction history.
// ============================================================================

using System;
using System.Runtime.InteropServices;
using Conditional = System.Diagnostics.ConditionalAttribute;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Hecton8.World;
using Hecton.Localization;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AtlasSignal
{
    /// <summary>
    /// Player status from the Atlas-6 point of view.
    /// </summary>
    public enum Atlas6PlayerStatus
    {
        Unknown         = 0,   // Not detected
        Detected        = 1,   // Detected and under analysis
        Neutral         = 2,   // Not a threat
        Threat          = 3,   // Ecosystem stability threat
        Collaborator    = 4,   // Trade collaborator
        Anomaly         = 5    // Living human outside the original colony
    }

    public enum Atlas6EventType : byte
    {
        PlayerStatusChanged = 0,
        DirectiveConflict = 1,
        BarterAccepted = 2,
        ScarcityDirectiveIssued = 3
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct Atlas6EventPayload
    {
        [FieldOffset(0)] public int TransactionCount;
        [FieldOffset(4)] public uint ConflictHash;
        [FieldOffset(8)] public uint DirectiveQuestHash;
        [FieldOffset(12)] public uint ResourceHash;
        [FieldOffset(16)] public ushort EventType;
        [FieldOffset(18)] public ushort StatusValue;
        [FieldOffset(20)] private uint _pad0;
        [FieldOffset(24)] private ulong _pad1;
    }

    public interface IAtlas6EventListener
    {
        void OnAtlas6Event(in Atlas6EventPayload payload);
    }

    public static class Atlas6Events
    {
        public const string ActuarialLiabilityThreatConflictId = "atlas6_actuarial_liability_threat";
        public const string SatoRenSilenceSeveranceConflictId = "atlas6_sato_ren_silence_severance";

        private const int ListenerCapacity = 4;
        private const int PendingEventCapacity = 4;
        private const int ConflictIdCapacity = 8;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
        public static readonly uint ActuarialLiabilityThreatConflictHash =
            ComputeDirectiveConflictHash(ActuarialLiabilityThreatConflictId);
        public static readonly uint SatoRenSilenceSeveranceConflictHash =
            ComputeDirectiveConflictHash(SatoRenSilenceSeveranceConflictId);
        private static readonly uint _ListenerRejectedWarningHash = unchecked((uint)LocHash.Compute("Atlas6Events.ListenerRejected"));
        private static readonly uint _ListenerExceptionWarningHash = unchecked((uint)LocHash.Compute("Atlas6Events.ListenerException"));
        private static readonly uint _ListenerContextHash = unchecked((uint)LocHash.Compute("Atlas6Events.Listeners"));

        private struct ListenerSlot
        {
            public IAtlas6EventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct ConflictIdSlot
        {
            public uint ConflictHash;
            public string ConflictId;
            public byte IsValid;

            public void Clear()
            {
                ConflictHash = 0u;
                ConflictId = null;
                IsValid = 0;
            }
        }

        private struct Atlas6ListenerRegistry
        {
            private readonly ListenerSlot[] _slots;
            private int _count;

            public Atlas6ListenerRegistry(int capacity)
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

            public bool Contains(IAtlas6EventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_slots[i].Listener, listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(IAtlas6EventListener listener)
            {
                if (listener == null || _count >= _slots.Length)
                    return false;

                _slots[_count++].Listener = listener;
                return true;
            }

            public bool TryUnregister(IAtlas6EventListener listener)
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

            public IAtlas6EventListener GetAt(int index)
            {
                return (uint)index < (uint)_count ? _slots[index].Listener : null;
            }
        }

        // COLD ALLOC: ListenerSlot[4] - Atlas-6 directive listeners drained on dispatcher LateUpdate - owner: Atlas6Events
        private static Atlas6ListenerRegistry _listeners = new Atlas6ListenerRegistry(ListenerCapacity);
        // COLD ALLOC: ListenerSlot[4] - listener additions deferred while dispatching Atlas-6 directive events - owner: Atlas6Events
        private static readonly ListenerSlot[] _deferredRegisterListeners = new ListenerSlot[ListenerCapacity];
        // COLD ALLOC: ListenerSlot[4] - listener removals deferred while dispatching Atlas-6 directive events - owner: Atlas6Events
        private static readonly ListenerSlot[] _deferredUnregisterListeners = new ListenerSlot[ListenerCapacity];
        // COLD ALLOC: ConflictIdSlot[8] - fixed hashed directive conflict IDs for cold-path resolution - owner: Atlas6Events
        private static readonly ConflictIdSlot[] _conflictIdsByHash = new ConflictIdSlot[ConflictIdCapacity];
        private static NativeQueue<Atlas6EventPayload> _pendingEvents;
        private static NativeQueue<Atlas6EventPayload> _nextFrameEvents;
        private static int _pendingEventsSentinelId;
        private static int _nextFrameEventsSentinelId;
        private static int _conflictIdCount;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _droppedListenerRegistrationCount;
        private static int _listenerExceptionCount;
        private static int _lastListenerRejectedTelemetryFrame = -1;
        private static int _lastListenerExceptionTelemetryFrame = -1;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;
        public static int DroppedListenerRegistrationCount => _droppedListenerRegistrationCount;
        public static int ListenerExceptionCount => _listenerExceptionCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ReleaseNativeQueues();

            _listeners.Clear();
            ClearConflictIds();
            Array.Clear(_deferredRegisterListeners, 0, _deferredRegisterCount);
            Array.Clear(_deferredUnregisterListeners, 0, _deferredUnregisterCount);
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _deferredRegisterCount = 0;
            _deferredUnregisterCount = 0;
            _droppedListenerRegistrationCount = 0;
            _listenerExceptionCount = 0;
            _lastListenerRejectedTelemetryFrame = -1;
            _lastListenerExceptionTelemetryFrame = -1;
            _isDispatching = false;
        }

        /// <summary>Status igroka izmenilsya.</summary>
        public static void Register(IAtlas6EventListener listener)
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

        /// <summary>Directive conflict: Atlas-6 cannot execute the order.</summary>
        public static void Unregister(IAtlas6EventListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatching)
            {
                QueueDeferredUnregister(listener);
                return;
            }

            _listeners.TryUnregister(listener);
        }

        /// <summary>Barter accepted: Atlas-6 received resources.</summary>
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

                if (!_pendingEvents.TryDequeue(out Atlas6EventPayload payload))
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
                        IAtlas6EventListener listener = _listeners.GetAt(i);
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

        public static bool TryResolveDirectiveConflict(uint conflictHash, out string conflictId)
        {
            if (conflictHash == ActuarialLiabilityThreatConflictHash)
            {
                conflictId = ActuarialLiabilityThreatConflictId;
                return true;
            }

            if (conflictHash == SatoRenSilenceSeveranceConflictHash)
            {
                conflictId = SatoRenSilenceSeveranceConflictId;
                return true;
            }

            return TryResolveConflictId(conflictHash, out conflictId);
        }

        public static uint ComputeDirectiveConflictHash(string conflictId)
        {
            return string.IsNullOrWhiteSpace(conflictId)
                ? 0u
                : unchecked((uint)LocHash.Compute(conflictId));
        }

        [Obsolete("Use TryRaisePlayerStatusChanged(Atlas6PlayerStatus) so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaisePlayerStatusChanged(Atlas6PlayerStatus status)
        {
            TryRaisePlayerStatusChanged(status);
        }

        public static bool TryRaisePlayerStatusChanged(Atlas6PlayerStatus status)
        {
            if (!IsKnownPlayerStatus(status))
                return false;

            return Enqueue(new Atlas6EventPayload
            {
                TransactionCount = 0,
                ConflictHash = 0u,
                DirectiveQuestHash = 0u,
                ResourceHash = 0u,
                EventType = (ushort)Atlas6EventType.PlayerStatusChanged,
                StatusValue = (ushort)status
            });
        }

        [Obsolete("Use TryRaiseDirectiveConflict(uint conflictHash). String ingress is not allowed on first-party event lanes.", true)]
        public static void RaiseDirectiveConflict(string conflictId)
        {
            TryRaiseDirectiveConflictFromString(conflictId);
        }

        private static bool TryRaiseDirectiveConflictFromString(string conflictId)
        {
            uint conflictHash = ComputeDirectiveConflictHash(conflictId);
            if (conflictHash == 0u)
                return false;

            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return false;

            if (!TryRegisterConflictId(conflictHash, conflictId))
                return false;

            return TryRaiseDirectiveConflict(conflictHash);
        }

        [Obsolete("Use TryRaiseDirectiveConflict(uint conflictHash) so overflow/drop semantics stay visible at the producer.", true)]
        public static bool RaiseDirectiveConflict(uint conflictHash)
        {
            return TryRaiseDirectiveConflict(conflictHash);
        }

        public static bool TryRaiseDirectiveConflict(uint conflictHash)
        {
            if (conflictHash == 0u)
                return false;

            return Enqueue(new Atlas6EventPayload
            {
                TransactionCount = 0,
                ConflictHash = conflictHash,
                DirectiveQuestHash = 0u,
                ResourceHash = 0u,
                EventType = (ushort)Atlas6EventType.DirectiveConflict,
                StatusValue = 0
            });
        }

        [Obsolete("Use TryRaiseBarterAccepted(int) so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaiseBarterAccepted(int transactionCount)
        {
            TryRaiseBarterAccepted(transactionCount);
        }

        public static bool TryRaiseBarterAccepted(int transactionCount)
        {
            if (transactionCount <= 0)
                return false;

            return Enqueue(new Atlas6EventPayload
            {
                TransactionCount = transactionCount,
                ConflictHash = 0u,
                DirectiveQuestHash = 0u,
                ResourceHash = 0u,
                EventType = (ushort)Atlas6EventType.BarterAccepted,
                StatusValue = 0
            });
        }

        [Obsolete("Use TryRaiseScarcityDirective(uint,uint) so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaiseScarcityDirective(uint questHash, uint resourceHash)
        {
            TryRaiseScarcityDirective(questHash, resourceHash);
        }

        public static bool TryRaiseScarcityDirective(uint questHash, uint resourceHash)
        {
            if (questHash == 0u || resourceHash == 0u)
                return false;

            return Enqueue(new Atlas6EventPayload
            {
                TransactionCount = 0,
                ConflictHash = 0u,
                DirectiveQuestHash = questHash,
                ResourceHash = resourceHash,
                EventType = (ushort)Atlas6EventType.ScarcityDirectiveIssued,
                StatusValue = 0
            });
        }

        private static bool TryRegisterConflictId(uint conflictHash, string conflictId)
        {
            if (conflictHash == 0u)
                return false;

            if (TryFindConflictId(conflictHash, out _))
                return true;

            if (_conflictIdCount >= _conflictIdsByHash.Length)
                return false;

            _conflictIdsByHash[_conflictIdCount++] = new ConflictIdSlot
            {
                ConflictHash = conflictHash,
                ConflictId = conflictId,
                IsValid = 1
            };
            return true;
        }

        private static bool IsKnownPlayerStatus(Atlas6PlayerStatus status)
        {
            return status >= Atlas6PlayerStatus.Unknown &&
                   status <= Atlas6PlayerStatus.Anomaly;
        }

        private static bool TryResolveConflictId(uint conflictHash, out string conflictId)
        {
            if (TryFindConflictId(conflictHash, out int index))
            {
                conflictId = _conflictIdsByHash[index].ConflictId ?? string.Empty;
                return true;
            }

            conflictId = string.Empty;
            return false;
        }

        private static bool TryFindConflictId(uint conflictHash, out int index)
        {
            for (int i = 0; i < _conflictIdCount; i++)
            {
                ConflictIdSlot slot = _conflictIdsByHash[i];
                if (slot.IsValid != 0 && slot.ConflictHash == conflictHash)
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        private static void ClearConflictIds()
        {
            for (int i = 0; i < _conflictIdCount; i++)
                _conflictIdsByHash[i].Clear();

            _conflictIdCount = 0;
        }

        private static void EnsureInitialized()
        {
            try
            {
                if (!_pendingEvents.IsCreated)
                {
                    _pendingEvents = new NativeQueue<Atlas6EventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<Atlas6EventPayload>[4] - deferred Atlas-6 directive lane flushed by SystemDispatcher LateUpdate - owner: Atlas6Events
                    RegisterNativeQueue(ref _pendingEvents, PendingEventCapacity, nameof(_pendingEvents), out _pendingEventsSentinelId);
                    PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
                }

                if (!_nextFrameEvents.IsCreated)
                {
                    _nextFrameEvents = new NativeQueue<Atlas6EventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<Atlas6EventPayload>[4] - next-frame Atlas-6 directive lane prevents same-frame reentrant dispatch - owner: Atlas6Events
                    RegisterNativeQueue(ref _nextFrameEvents, PendingEventCapacity, nameof(_nextFrameEvents), out _nextFrameEventsSentinelId);
                    PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
                }
            }
            catch
            {
                ReleaseNativeQueues();
                ClearConflictIds();
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
                nameof(Atlas6Events),
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

        private static bool Enqueue(in Atlas6EventPayload payload)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return false;

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
            ref NativeQueue<Atlas6EventPayload> queue,
            ref int pendingCount)
        {
            if (!queue.IsCreated)
                return true;

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
                _pendingEventCount > 0 ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            NativeQueue<Atlas6EventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            int sentinelIdSwap = _pendingEventsSentinelId;
            _pendingEventsSentinelId = _nextFrameEventsSentinelId;
            _nextFrameEventsSentinelId = sentinelIdSwap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }

        private static void DispatchToListener(IAtlas6EventListener listener, in Atlas6EventPayload payload)
        {
            try
            {
                listener.OnAtlas6Event(in payload);
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

        private static void QueueDeferredRegister(IAtlas6EventListener listener)
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

        private static void QueueDeferredUnregister(IAtlas6EventListener listener)
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

        private static bool CancelDeferredRegister(IAtlas6EventListener listener)
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

        private static void CancelDeferredUnregister(IAtlas6EventListener listener)
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

        private static bool IsDeferredRegisterPending(IAtlas6EventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(IAtlas6EventListener listener)
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
                IAtlas6EventListener listener = _deferredUnregisterListeners[i].Listener;
                _deferredUnregisterListeners[i].Clear();
                if (listener != null)
                    _listeners.TryUnregister(listener);
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                IAtlas6EventListener listener = _deferredRegisterListeners[i].Listener;
                _deferredRegisterListeners[i].Clear();
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterCount = 0;
        }

        private static void RegisterImmediate(IAtlas6EventListener listener)
        {
            if (_listeners.Contains(listener))
                return;

            if (!_listeners.TryRegister(listener))
                ReportListenerRegistrationRejected();
        }

        private static void ReportListenerRegistrationRejected()
        {
            _droppedListenerRegistrationCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastListenerRejectedTelemetryFrame == frame)
                return;

            _lastListenerRejectedTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _ListenerRejectedWarningHash,
                _ListenerContextHash,
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
                _ListenerExceptionWarningHash,
                _ListenerContextHash,
                Mathf.Max(1, _listenerExceptionCount));
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-80)]
    public sealed class Atlas6DirectiveSystem : MonoBehaviour, ISaveable, ISlowTickable, ILateFrameTickable, INarrativeEventListener, IAtlas6EventListener, IAtlas6DirectiveCommandSink, IGlobalRegistryHotSwapListener
    {
        private const int MinimumRevealStageForDirectiveIdentity = 3;
        private const int PendingNotificationCapacity = 4;
        private const int PendingNotificationCharCapacity = 512;
        private static readonly uint _NotificationQueueDropWarningHash = unchecked((uint)LocHash.Compute("Atlas6DirectiveSystem.NotificationQueueDrop"));
        private static readonly uint _NotificationPushMissWarningHash = unchecked((uint)LocHash.Compute("Atlas6DirectiveSystem.NotificationPushMiss"));
        private static readonly uint _NotificationContextHash = unchecked((uint)LocHash.Compute("Atlas6DirectiveSystem.Notification"));
        private const string SignalIdentityDiscoveryId = "atlas6_signal_identified";
        private const string SignalFullyDecodedDiscoveryId = "atlas6_signal_fully_decoded";
        private const string TerminalSectorDiscoveryId = "atlas6_terminal_sector3";
        private const string CoreReachedDiscoveryId = "atlas6_core_reached";
        private const string CoreDataAccessedDiscoveryId = "atlas6_core_data_accessed";
        private const string DirectiveConflictColonyDeadId = "directive_2_impossible_colony_dead";
        private const string ScarcityDirectiveFallbackWarning = "ATLAS-6 DIRECTIVE: RESTOCK ESSENTIAL RESOURCE.";
        private const int ScarcityDirectiveTitleCapacity = 160;
        private static readonly uint _signalIdentityDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(SignalIdentityDiscoveryId);
        private static readonly uint _signalFullyDecodedDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(SignalFullyDecodedDiscoveryId);
        private static readonly uint _terminalSectorDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(TerminalSectorDiscoveryId);
        private static readonly uint _coreReachedDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(CoreReachedDiscoveryId);
        private static readonly uint _coreDataAccessedDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(CoreDataAccessedDiscoveryId);
        private static readonly uint _directiveConflictColonyDeadHash = Atlas6Events.ComputeDirectiveConflictHash(DirectiveConflictColonyDeadId);

        // ----------------------------------------------------------
        //  INSPECTOR
        // ----------------------------------------------------------

        [Header("-- Thresholds ------------------------------")]
        [Tooltip("Kolichestvo barter-tranzaktsiy dlya perehoda v Collaborator.")]
        [SerializeField] private int collaboratorThreshold = 5;

        [Tooltip("Rasstoyanie obnaruzheniya igroka dronami (metry). Zarezervirovano dlya FaunaDirector.")]
#pragma warning disable CS0414
        [SerializeField] private float detectionRange = 200f;
#pragma warning restore CS0414

        [Tooltip("Rasstoyanie do yadra dlya perehoda v Anomaly status.")]
        [SerializeField] private float anomalyRange = 500f;

        // ----------------------------------------------------------
        //  GLOBAL REGISTRY COMPATIBILITY
        // ----------------------------------------------------------

        // ----------------------------------------------------------
        //  PRIVATE STATE
        // ----------------------------------------------------------

        private Atlas6PlayerStatus _playerStatus = Atlas6PlayerStatus.Unknown;
        private int  _barterTransactionCount;
        private bool _directiveConflictTriggered;
        private bool _registered;
        private bool _lateFrameRegistered;
        private bool _serviceRegistered;
        private bool _hotSwapRegistered;
        private bool _saveRegistered;
        private bool _narrativeEventRegistered;
        private bool _atlas6EventRegistered;
        private bool _runtimeOwnerAborted;
        private HectonPlayerMovement _playerMovement;
        private IAtlasSignalReadModel _atlasSignal;
        private IFirstHourReadModel _firstHourDirector;
        private IQuestSystem _questManager;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private ILocalizationTextReadModel _localization;
        private ISaveService _saveService;
        private ISaveService _registeredSaveService;
        private uint _latestScarcityDirectiveQuestHash;
        private uint _latestScarcityDirectiveResourceHash;
        private readonly char[] _scarcityDirectiveTitleBuffer = new char[ScarcityDirectiveTitleCapacity];
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

        public int SavePriority => 11;
        public int LoadPriority => 11;
        public int NotificationQueueDropCount => _notificationQueueDropCount;
        public int NotificationPushMissCount => _notificationPushMissCount;

        // ----------------------------------------------------------
        //  PUBLIC PROPERTIES
        // ----------------------------------------------------------

        public Atlas6PlayerStatus PlayerStatus => _playerStatus;
        public int BarterTransactionCount => math.max(0, _barterTransactionCount);

        /// <summary>
        /// Uroven doveriya Atlas-6 k igroku [0..1].
        /// Rastet s torgovley, padaet pri ugroze.
        /// </summary>
        public float TrustLevel
        {
            get
            {
                return _playerStatus switch
                {
                    Atlas6PlayerStatus.Unknown      => 0f,
                    Atlas6PlayerStatus.Detected     => 0.1f,
                    Atlas6PlayerStatus.Neutral      => 0.3f,
                    Atlas6PlayerStatus.Collaborator => math.saturate(BarterTransactionCount / (float)ResolveCollaboratorThreshold()),
                    Atlas6PlayerStatus.Anomaly      => 0.5f,
                    Atlas6PlayerStatus.Threat       => 0f,
                    _                               => 0f
                };
            }
        }

        // ----------------------------------------------------------
        //  LIFECYCLE
        // ----------------------------------------------------------

        private void OnEnable()
        {
            if (!TryRegisterService())
                return;

            CacheAtlasDependenciesCold();
            TryRegisterHotSwapListener();
            TryRegister();
            TryRegisterSaveParticipant();

            TryRegisterNarrativeEvents();
            TryRegisterAtlas6Events();
            ResolvePlayer();
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregister();
            TryUnregisterLateFrameTick();
            TryUnregisterService();
            TryUnregisterHotSwapListener();
            ClearAtlasDependencies();
            TryUnregisterSaveParticipant();
            TryUnregisterNarrativeEvents();
            TryUnregisterAtlas6Events();
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregister();
            TryUnregisterLateFrameTick();
            TryUnregisterService();
            TryUnregisterHotSwapListener();
            TryUnregisterSaveParticipant();
            TryUnregisterNarrativeEvents();
            TryUnregisterAtlas6Events();
            ClearAtlasDependencies();
        }

        // ----------------------------------------------------------
        //  ISlowTickable
        // ----------------------------------------------------------

        public void SlowTick()
        {
            if (_runtimeOwnerAborted)
                return;

            IAtlasSignalReadModel signal = _atlasSignal;
            if (signal == null) return;
            if (!signal.IsAtlasSignalDetected) return;
            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return;
            if (!signal.TryReadAtlasSignalCoreAup(out AbsoluteUniversePosition coreAup))
                return;

            double distanceToCoreSq = AbsoluteUniversePosition.DistanceSq(in playerAup, in coreAup);
            double anomalyRangeSq = (double)anomalyRange * anomalyRange;

            // Perehod v Anomaly pri priblizhenii k yadru
            if (distanceToCoreSq < anomalyRangeSq &&
                _playerStatus != Atlas6PlayerStatus.Anomaly &&
                _playerStatus != Atlas6PlayerStatus.Threat)
            {
                SetStatus(Atlas6PlayerStatus.Anomaly);
                QueueNotification(
                    ResolveLocalizedSpan(
                        LocalizationKeys.ATLAS6_ANOMALY_DETECTED,
                        "ATLAS-6: UNIDENTIFIED BIOLOGICAL AGENT DETECTED. ANALYSIS..."),
                    NotificationEventSeverity.Warning);
            }

            // Directive conflict: living human detected.
            if (!_directiveConflictTriggered &&
                _playerStatus >= Atlas6PlayerStatus.Detected)
            {
                _directiveConflictTriggered = true;
                Atlas6Events.TryRaiseDirectiveConflict(_directiveConflictColonyDeadHash);

                LogDirectiveConflict();
            }
        }

        public void LateFrameTick()
        {
            if (_runtimeOwnerAborted)
                return;

            FlushQueuedNotifications();
            if (_pendingNotificationCount == 0)
                TryUnregisterLateFrameTick();
        }

        private void TryRegister()
        {
            if (_runtimeOwnerAborted || _registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
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
            TryUnregisterLateFrameTick(clearQueuedNotifications: true);
        }

        private void TryUnregisterLateFrameTick(bool clearQueuedNotifications)
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

            Atlas6DirectiveSystem registeredRuntime = Hecton8.Core.GlobalRegistry.Atlas6Directive;
            if (IsAtlas6DirectiveRuntimeUsable(registeredRuntime))
            {
                AbortDuplicateRuntimeOwner();
                return false;
            }

            if (!ReferenceEquals(registeredRuntime, null) && !ReferenceEquals(registeredRuntime, this))
                Hecton8.Core.GlobalRegistry.UnregisterAtlas6DirectiveRuntime(registeredRuntime);

            Hecton8.Core.GlobalRegistry.RegisterAtlas6DirectiveRuntime(this);
            _serviceRegistered = ReferenceEquals(Hecton8.Core.GlobalRegistry.Atlas6Directive, this);
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

            Hecton8.Core.GlobalRegistry.UnregisterAtlas6DirectiveRuntime(this);
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
                case GlobalRegistryServiceSlot.AtlasSignalRuntime:
                    _atlasSignal = currentService as IAtlasSignalReadModel;
                    break;
                case GlobalRegistryServiceSlot.FirstHourRuntime:
                    _firstHourDirector = currentService as IFirstHourReadModel;
                    break;
                case GlobalRegistryServiceSlot.QuestRuntime:
                    _questManager = currentService as IQuestSystem;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    ResolvePlayer();
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localization = currentService as ILocalizationTextReadModel;
                    break;
                case GlobalRegistryServiceSlot.Save:
                    TryUnregisterSaveParticipant();
                    _saveService = currentService as ISaveService;
                    TryRegisterSaveParticipant();
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregister();
                    TryUnregisterLateFrameTick(clearQueuedNotifications: false);
                    if (currentService != null && isActiveAndEnabled)
                    {
                        TryRegister();
                        if (_pendingNotificationCount > 0)
                            TryRegisterLateFrameTick();
                    }
                    break;
            }
        }

        private void CacheAtlasDependenciesCold()
        {
            if (_runtimeOwnerAborted)
                return;

            if (_atlasSignal == null)
                _atlasSignal = Hecton8.Core.GlobalRegistry.AtlasSignalReadModel;

            if (_firstHourDirector == null)
                _firstHourDirector = Hecton8.Core.GlobalRegistry.FirstHourReadModel;

            if (_questManager == null)
                _questManager = GlobalRegistry.QuestSystem;

            if (_playerRuntimeContext == null)
                _playerRuntimeContext = Hecton8.Core.GlobalRegistry.Player;

            if (_localization == null)
                _localization = Hecton8.Core.GlobalRegistry.LocalizationText;
        }

        private void TryRegisterSaveParticipant()
        {
            if (_runtimeOwnerAborted || _saveRegistered || !Application.isPlaying || !isActiveAndEnabled)
                return;

            ISaveService saveService = _saveService;
            if (!IsSaveServiceUsable(saveService))
            {
                saveService = GlobalRegistry.Save;
                _saveService = saveService;
            }

            if (!IsSaveServiceUsable(saveService))
                return;

            saveService.Register(this);
            _registeredSaveService = saveService;
            _saveRegistered = true;
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

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
        }

        private void ClearAtlasDependencies()
        {
            _atlasSignal = null;
            _firstHourDirector = null;
            _questManager = null;
            _playerRuntimeContext = null;
            _playerMovement = null;
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

        private void TryRegisterNarrativeEvents()
        {
            if (_runtimeOwnerAborted || _narrativeEventRegistered)
                return;

            NarrativeEvents.Register(this);
            _narrativeEventRegistered = true;
        }

        private void TryUnregisterNarrativeEvents()
        {
            if (!_narrativeEventRegistered)
                return;

            NarrativeEvents.Unregister(this);
            _narrativeEventRegistered = false;
        }

        private void TryRegisterAtlas6Events()
        {
            if (_runtimeOwnerAborted || _atlas6EventRegistered)
                return;

            Atlas6Events.Register(this);
            _atlas6EventRegistered = true;
        }

        private void TryUnregisterAtlas6Events()
        {
            if (!_atlas6EventRegistered)
                return;

            Atlas6Events.Unregister(this);
            _atlas6EventRegistered = false;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            Atlas6DirectiveSystem registeredRuntime = Hecton8.Core.GlobalRegistry.Atlas6Directive;
            if (ReferenceEquals(registeredRuntime, this))
                return false;

            if (IsAtlas6DirectiveRuntimeUsable(registeredRuntime))
            {
                AbortDuplicateRuntimeOwner();
                return true;
            }

            if (!ReferenceEquals(registeredRuntime, null))
                Hecton8.Core.GlobalRegistry.UnregisterAtlas6DirectiveRuntime(registeredRuntime);

            return false;
        }

        private void AbortDuplicateRuntimeOwner()
        {
            TryUnregister();
            TryUnregisterLateFrameTick();
            TryUnregisterHotSwapListener();
            TryUnregisterSaveParticipant();
            TryUnregisterNarrativeEvents();
            TryUnregisterAtlas6Events();
            ClearAtlasDependencies();
            ClearQueuedNotifications();
            _runtimeOwnerAborted = true;
            _registered = false;
            _lateFrameRegistered = false;
            _serviceRegistered = false;
            _hotSwapRegistered = false;
            _saveRegistered = false;
            _narrativeEventRegistered = false;
            _atlas6EventRegistered = false;
            enabled = false;
            Destroy(gameObject);
        }

        private static bool IsAtlas6DirectiveRuntimeUsable(Atlas6DirectiveSystem system)
        {
            return !ReferenceEquals(system, null) &&
                   system != null &&
                   system._serviceRegistered &&
                   system.isActiveAndEnabled &&
                   !system._runtimeOwnerAborted;
        }

        // ----------------------------------------------------------
        //  PUBLIC API
        // ----------------------------------------------------------

        /// <summary>Zaregistrirovat barter-tranzaktsiyu.</summary>
        public void RegisterBarterTransaction()
        {
            if (_runtimeOwnerAborted)
                return;

            int safeCount = BarterTransactionCount;
            _barterTransactionCount = safeCount < int.MaxValue
                ? safeCount + 1
                : int.MaxValue;
            Atlas6Events.TryRaiseBarterAccepted(_barterTransactionCount);

            // Perehod v Collaborator
            if (_barterTransactionCount >= ResolveCollaboratorThreshold() &&
                _playerStatus != Atlas6PlayerStatus.Collaborator &&
                _playerStatus != Atlas6PlayerStatus.Threat)
            {
                SetStatus(Atlas6PlayerStatus.Collaborator);
                QueueNotification(
                    ResolveLocalizedSpan(
                        LocalizationKeys.ATLAS6_COLLABORATOR_STATUS,
                        "ATLAS-6: UTILITARIAN CALCULATION - EXCHANGE EFFICIENT. STATUS: COLLABORATOR."),
                    NotificationEventSeverity.Info);
            }
        }

        // ----------------------------------------------------------
        //  PRIVATE
        // ----------------------------------------------------------

        private void SetStatus(Atlas6PlayerStatus newStatus)
        {
            if (newStatus == _playerStatus) return;
            _playerStatus = newStatus;
            Atlas6Events.TryRaisePlayerStatusChanged(newStatus);

            LogPlayerStatusChanged();
        }

        private void AdoptExternalStatus(Atlas6PlayerStatus newStatus)
        {
            if (newStatus == _playerStatus) return;
            _playerStatus = newStatus;

            QueueExternalStatusNotification(newStatus);
            LogPlayerStatusChanged();
        }

        private int ResolveCollaboratorThreshold()
        {
            return math.max(1, collaboratorThreshold);
        }

        private void QueueExternalStatusNotification(Atlas6PlayerStatus status)
        {
            if (status == Atlas6PlayerStatus.Threat)
            {
                QueueNotification(
                    "ATLAS-6: ACTUARIAL THREAT CLASSIFICATION ACTIVE.".AsSpan(),
                    NotificationEventSeverity.Critical);
            }
        }

        public void OnNarrativeEvent(in NarrativeEventPayload payload)
        {
            if (_runtimeOwnerAborted)
                return;

            if ((NarrativeEventType)payload.EventType != NarrativeEventType.DiscoveryMade)
                return;

            if (CanAdoptAtlasStatusFromDiscovery(payload.DiscoveryHash))
                SetStatus(Atlas6PlayerStatus.Detected);
        }

        public void OnAtlas6Event(in Atlas6EventPayload payload)
        {
            if (_runtimeOwnerAborted)
                return;

            Atlas6EventType eventType = (Atlas6EventType)payload.EventType;
            if (eventType == Atlas6EventType.PlayerStatusChanged)
            {
                HandleExternalPlayerStatusChanged((Atlas6PlayerStatus)payload.StatusValue);
                return;
            }

            if (eventType == Atlas6EventType.BarterAccepted)
            {
                HandleBarterAccepted(payload.TransactionCount);
                return;
            }

            if (eventType == Atlas6EventType.DirectiveConflict)
            {
                HandleDirectiveConflict(payload.ConflictHash);
                return;
            }

            if (eventType == Atlas6EventType.ScarcityDirectiveIssued)
                HandleScarcityDirective(payload.DirectiveQuestHash, payload.ResourceHash);
        }

        private void HandleExternalPlayerStatusChanged(Atlas6PlayerStatus status)
        {
            if (status < Atlas6PlayerStatus.Unknown || status > Atlas6PlayerStatus.Anomaly)
                return;

            if (_playerStatus == Atlas6PlayerStatus.Threat && status != Atlas6PlayerStatus.Threat)
                return;

            AdoptExternalStatus(status);
        }

        private void HandleDirectiveConflict(uint conflictHash)
        {
            if (conflictHash == 0u)
                return;

            if (conflictHash == Atlas6Events.ActuarialLiabilityThreatConflictHash)
            {
                QueueNotification(
                    "ATLAS-6: ACTUARIAL LIABILITY CONFLICT. REPAIR ROUTES SUSPENDED.".AsSpan(),
                    NotificationEventSeverity.Critical);
                return;
            }

            if (conflictHash == Atlas6Events.SatoRenSilenceSeveranceConflictHash)
            {
                QueueNotification(
                    "ATLAS-6: SATO-REN SILENCE. ACOUSTIC TETHER SEVERED.".AsSpan(),
                    NotificationEventSeverity.Critical);
            }
        }

        private void HandleBarterAccepted(int count)
        {
            // First trade moves status to Neutral.
            if (_playerStatus == Atlas6PlayerStatus.Detected ||
                _playerStatus == Atlas6PlayerStatus.Unknown)
                SetStatus(Atlas6PlayerStatus.Neutral);
        }

        private void HandleScarcityDirective(uint directiveQuestHash, uint resourceHash)
        {
            _latestScarcityDirectiveQuestHash = directiveQuestHash;
            _latestScarcityDirectiveResourceHash = resourceHash;

            IQuestSystem questManager = _questManager;
            if (questManager != null &&
                directiveQuestHash != 0u &&
                questManager.TryCopyQuestPresentation(
                    directiveQuestHash,
                    _scarcityDirectiveTitleBuffer,
                    out int titleLength,
                    null,
                    out _,
                    out _,
                    out _,
                    out _)
                && titleLength > 0)
            {
                if (QueueNotification(
                        _scarcityDirectiveTitleBuffer.AsSpan(0, math.min(titleLength, _scarcityDirectiveTitleBuffer.Length)),
                        NotificationEventSeverity.Warning))
                    return;
            }

            QueueNotification(ScarcityDirectiveFallbackWarning.AsSpan(), NotificationEventSeverity.Warning);
        }

        private bool CanAdoptAtlasStatusFromDiscovery(uint discoveryHash)
        {
            if (_playerStatus != Atlas6PlayerStatus.Unknown)
                return false;

            if (!IsDirectiveIdentityDiscovery(discoveryHash))
                return false;

            IAtlasSignalReadModel signal = _atlasSignal;
            if (signal != null)
                return signal.CurrentAtlasSignalRevealStage >= MinimumRevealStageForDirectiveIdentity;

            IFirstHourReadModel firstHourDirector = _firstHourDirector;
            if (firstHourDirector != null)
                return firstHourDirector.IsFirstHourMilestoneComplete((int)FirstHourMilestone.HumCloser);

            return true;
        }

        private static bool IsDirectiveIdentityDiscovery(uint discoveryHash)
        {
            return discoveryHash == _signalIdentityDiscoveryHash ||
                   discoveryHash == _signalFullyDecodedDiscoveryHash ||
                   discoveryHash == _terminalSectorDiscoveryHash ||
                   discoveryHash == _coreReachedDiscoveryHash ||
                   discoveryHash == _coreDataAccessedDiscoveryHash;
        }

        private void ResolvePlayer()
        {
            if (_runtimeOwnerAborted)
                return;

            _playerMovement = null;

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null)
                _playerMovement = playerContext.PlayerMovement;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            if (_runtimeOwnerAborted)
            {
                playerAup = default;
                return false;
            }

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext == null)
                ResolvePlayer();

            playerContext = _playerRuntimeContext;
            if (playerContext == null ||
                !playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) ||
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u ||
                !movementState.PredictedAup.IsFinite())
            {
                playerAup = default;
                return false;
            }

            playerAup = movementState.PredictedAup;
            return true;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogDirectiveConflict()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.Log("[Atlas6] Directive conflict: Directive #2 (protect colony) impossible; colony dead.");
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogPlayerStatusChanged()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.Log("[Atlas6] Player status changed.");
#endif
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
            data.atlas6PlayerStatus = (int)_playerStatus;
            data.atlas6BarterCount  = BarterTransactionCount;
            data.atlas6DirectiveConflictTriggered = _directiveConflictTriggered;
        }

        public void LoadFromSaveData(SaveData data)
        {
            ClearQueuedNotifications();
            if (_runtimeOwnerAborted || data == null) return;
            _playerStatus = SanitizePlayerStatus(data.atlas6PlayerStatus);
            _barterTransactionCount = math.max(0, data.atlas6BarterCount);
            _directiveConflictTriggered = data.atlas6DirectiveConflictTriggered;
            _latestScarcityDirectiveQuestHash = 0u;
            _latestScarcityDirectiveResourceHash = 0u;
        }

        private static Atlas6PlayerStatus SanitizePlayerStatus(int playerStatus)
        {
            return playerStatus >= (int)Atlas6PlayerStatus.Unknown &&
                   playerStatus <= (int)Atlas6PlayerStatus.Anomaly
                ? (Atlas6PlayerStatus)playerStatus
                : Atlas6PlayerStatus.Unknown;
        }
    }
}
