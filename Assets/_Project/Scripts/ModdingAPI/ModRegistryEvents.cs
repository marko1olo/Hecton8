using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Modding
{
    internal static class ModRegistryEventLayout
    {
        public const int PayloadStrideBytes = 16;
    }

    /// <summary>
    /// Mod registry event discriminator for <see cref="ModRegistryEventPayload"/>.
    /// </summary>
    internal enum ModRegistryEventType : ushort
    {
        RuntimeRegistryChanged = 1,
        SettingsRegistryChanged = 2,
        RecipeRegistryChanged = 3,
        BuildableRegistryChanged = 4,
        RecycleRegistryChanged = 5
    }

    /// <summary>
    /// Deferred unmanaged payload for mod registry invalidation events.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = ModRegistryEventLayout.PayloadStrideBytes)]
    internal struct ModRegistryEventPayload
    {
        [FieldOffset(0)]
        public uint Frame;

        [FieldOffset(4)]
        public uint ModHash;

        [FieldOffset(8)]
        public uint SubjectHash;

        [FieldOffset(12)]
        public ushort EventType;

        [FieldOffset(14)]
        public ushort StatusBits;
    }

    /// <summary>
    /// Listener contract for deferred mod registry events.
    /// </summary>
    internal interface IModRegistryEventListener
    {
        /// <summary>
        /// Called during the dispatcher late-frame event flush.
        /// </summary>
        /// <param name="payload">Unmanaged registry invalidation payload.</param>
        void OnModRegistryEvent(in ModRegistryEventPayload payload);
    }

    /// <summary>
    /// NativeQueue-backed mod registry invalidation lane.
    /// </summary>
    internal static class ModRegistryEvents
    {
        private const int ListenerCapacity = 32;
        private const int PendingEventCapacity = 5;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
        private const uint RegistryEventQueueOverflowWarningHash = 0x4D524F46u; // MROF
        private const uint RegistryEventQueueContextHash = 0x4D524551u; // MREQ
        private const uint RegistryEventListenerOverflowWarningHash = 0x4D524C46u; // MRLF
        private const uint RegistryEventListenerContextHash = 0x4D524C51u; // MRLQ
        private const uint RegistryEventListenerExceptionWarningHash = 0x4D524558u; // MREX
        private const uint RegistryEventListenerExceptionContextHash = 0x4D524543u; // MREC

        private struct ListenerSlot
        {
            public IModRegistryEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        // COLD ALLOC: ListenerSlot[32] - mod registry invalidation listeners drained by SystemDispatcher without interface array dispatch - owner: ModRegistryEvents
        private static readonly ListenerSlot[] _listeners = new ListenerSlot[ListenerCapacity];
        // COLD ALLOC: IModRegistryEventListener[32] - stable dispatch snapshot prevents listener mutations from duplicating callbacks - owner: ModRegistryEvents
        private static readonly IModRegistryEventListener[] _dispatchListeners = new IModRegistryEventListener[ListenerCapacity];
        private static NativeQueue<ModRegistryEventPayload> _pendingEvents;
        private static NativeQueue<ModRegistryEventPayload> _nextFrameEvents;
        private static int _pendingEventsSentinelId;
        private static int _nextFrameEventsSentinelId;
        private static int _listenerCount;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _droppedEventCount;
        private static int _droppedListenerRegistrationCount;
        private static int _listenerExceptionCount;
        private static int _lastQueueOverflowTelemetryFrame = -1;
        private static int _lastListenerOverflowTelemetryFrame = -1;
        private static int _lastListenerExceptionTelemetryFrame = -1;
        private static bool _isDispatching;
        private static bool _runtimeRegistryChangeQueued;
        private static bool _settingsRegistryChangeQueued;
        private static bool _recipeRegistryChangeQueued;
        private static bool _buildableRegistryChangeQueued;
        private static bool _recycleRegistryChangeQueued;
        private static bool _runtimeRegistryChangeOverflowed;
        private static bool _settingsRegistryChangeOverflowed;
        private static bool _recipeRegistryChangeOverflowed;
        private static bool _buildableRegistryChangeOverflowed;
        private static bool _recycleRegistryChangeOverflowed;

        /// <summary>
        /// Pending payload count in the mod registry event lane.
        /// </summary>
        internal static int PendingCount => _pendingEventCount + _nextFrameEventCount;
        internal static int DroppedEventCount => _droppedEventCount;
        internal static int DroppedListenerRegistrationCount => _droppedListenerRegistrationCount;
        internal static int ListenerExceptionCount => _listenerExceptionCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStaticState()
        {
            ReleaseNativeQueues();

            for (int i = 0; i < _listenerCount; i++)
                _listeners[i].Clear();

            ClearDispatchSnapshot(ListenerCapacity);
            _listenerCount = 0;
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _droppedEventCount = 0;
            _droppedListenerRegistrationCount = 0;
            _listenerExceptionCount = 0;
            _lastQueueOverflowTelemetryFrame = -1;
            _lastListenerOverflowTelemetryFrame = -1;
            _lastListenerExceptionTelemetryFrame = -1;
            _isDispatching = false;
            _runtimeRegistryChangeQueued = false;
            _settingsRegistryChangeQueued = false;
            _recipeRegistryChangeQueued = false;
            _buildableRegistryChangeQueued = false;
            _recycleRegistryChangeQueued = false;
            ClearOverflowedFlags();
        }

        /// <summary>
        /// Registers a mod registry event listener.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        internal static bool Register(IModRegistryEventListener listener)
        {
            if (listener == null)
                return false;

            EnsureInitialized();
            return RegisterImmediate(listener);
        }

        /// <summary>
        /// Unregisters a mod registry event listener.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        internal static void Unregister(IModRegistryEventListener listener)
        {
            if (listener == null)
                return;

            TryUnregisterImmediate(listener);
        }

        /// <summary>
        /// Enqueues a runtime registry invalidation event.
        /// </summary>
        /// <param name="modHash">Stable mod hash, or zero when the event is global.</param>
        internal static void NotifyRuntimeRegistryChanged(uint modHash)
        {
            Enqueue(ModRegistryEventType.RuntimeRegistryChanged, modHash, 0u, 0);
        }

        /// <summary>
        /// Enqueues a settings registry invalidation event.
        /// </summary>
        /// <param name="modHash">Stable owning mod hash.</param>
        /// <param name="settingHash">Stable setting hash.</param>
        internal static void NotifySettingsRegistryChanged(uint modHash, uint settingHash)
        {
            Enqueue(ModRegistryEventType.SettingsRegistryChanged, modHash, settingHash, 0);
        }

        /// <summary>
        /// Enqueues a recipe registry invalidation event.
        /// </summary>
        internal static void NotifyRecipeRegistryChanged()
        {
            Enqueue(ModRegistryEventType.RecipeRegistryChanged, 0u, 0u, 0);
        }

        /// <summary>
        /// Enqueues a buildable registry invalidation event.
        /// </summary>
        internal static void NotifyBuildableRegistryChanged()
        {
            Enqueue(ModRegistryEventType.BuildableRegistryChanged, 0u, 0u, 0);
        }

        /// <summary>
        /// Enqueues a global recycle-yield registry invalidation event.
        /// </summary>
        internal static void NotifyRecycleRegistryChanged()
        {
            Enqueue(ModRegistryEventType.RecycleRegistryChanged, 0u, 0u, 0);
        }

        /// <summary>
        /// Flushes pending mod registry events under the dispatcher late-frame budget.
        /// </summary>
        internal static void FlushPending()
        {
            if (!_pendingEvents.IsCreated)
                return;

            if (_listenerCount <= 0)
            {
                DrainPendingEventsWithoutDispatch();
                return;
            }

            PromoteNextFrameEventsIfFrontEmpty();
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out ModRegistryEventPayload payload))
                    return;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                ClearQueuedFlag(payload.EventType);

                int count = _listenerCount;
                CaptureDispatchSnapshot(count);
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IModRegistryEventListener listener = _dispatchListeners[i];
                        if (listener != null)
                        {
                            try
                            {
                                listener.OnModRegistryEvent(in payload);
                            }
                            catch (Exception exception)
                            {
                                ReportListenerDispatchException(payload.EventType, exception);
                            }
                        }
                    }
                }
                finally
                {
                    ClearDispatchSnapshot(count);
                    _isDispatching = false;
                }

                ReplayOverflowedEvents();
            }

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
                ReplayOverflowedEvents();
            }
        }

        private static void Enqueue(ModRegistryEventType eventType, uint modHash, uint subjectHash, ushort statusBits)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                MarkOverflowedIfNotAlreadyQueued(eventType);
                ReportQueueOverflow(eventType);
                return;
            }

            if (!TryMarkQueued(eventType))
                return;

            ModRegistryEventPayload payload = new ModRegistryEventPayload
            {
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                ModHash = modHash,
                SubjectHash = subjectHash,
                EventType = (ushort)eventType,
                StatusBits = statusBits
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

        private static void ReplayOverflowedEvents()
        {
            if (!_pendingEvents.IsCreated)
                return;

            TryReplayOverflowedEvent(ModRegistryEventType.RuntimeRegistryChanged, ref _runtimeRegistryChangeOverflowed);
            TryReplayOverflowedEvent(ModRegistryEventType.SettingsRegistryChanged, ref _settingsRegistryChangeOverflowed);
            TryReplayOverflowedEvent(ModRegistryEventType.RecipeRegistryChanged, ref _recipeRegistryChangeOverflowed);
            TryReplayOverflowedEvent(ModRegistryEventType.BuildableRegistryChanged, ref _buildableRegistryChangeOverflowed);
            TryReplayOverflowedEvent(ModRegistryEventType.RecycleRegistryChanged, ref _recycleRegistryChangeOverflowed);
        }

        private static void TryReplayOverflowedEvent(ModRegistryEventType eventType, ref bool overflowed)
        {
            if (!overflowed)
                return;

            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return;

            if (IsQueued(eventType))
            {
                overflowed = false;
                return;
            }

            overflowed = false;
            Enqueue(eventType, 0u, 0u, 1);
        }

        private static void MarkOverflowedIfNotAlreadyQueued(ModRegistryEventType eventType)
        {
            if (IsQueued(eventType))
                return;

            switch (eventType)
            {
                case ModRegistryEventType.RuntimeRegistryChanged:
                    _runtimeRegistryChangeOverflowed = true;
                    break;

                case ModRegistryEventType.SettingsRegistryChanged:
                    _settingsRegistryChangeOverflowed = true;
                    break;

                case ModRegistryEventType.RecipeRegistryChanged:
                    _recipeRegistryChangeOverflowed = true;
                    break;

                case ModRegistryEventType.BuildableRegistryChanged:
                    _buildableRegistryChangeOverflowed = true;
                    break;

                case ModRegistryEventType.RecycleRegistryChanged:
                    _recycleRegistryChangeOverflowed = true;
                    break;
            }
        }

        private static bool IsQueued(ModRegistryEventType eventType)
        {
            switch (eventType)
            {
                case ModRegistryEventType.RuntimeRegistryChanged:
                    return _runtimeRegistryChangeQueued;

                case ModRegistryEventType.SettingsRegistryChanged:
                    return _settingsRegistryChangeQueued;

                case ModRegistryEventType.RecipeRegistryChanged:
                    return _recipeRegistryChangeQueued;

                case ModRegistryEventType.BuildableRegistryChanged:
                    return _buildableRegistryChangeQueued;

                case ModRegistryEventType.RecycleRegistryChanged:
                    return _recycleRegistryChangeQueued;

                default:
                    return false;
            }
        }

        private static bool TryMarkQueued(ModRegistryEventType eventType)
        {
            switch (eventType)
            {
                case ModRegistryEventType.RuntimeRegistryChanged:
                    if (_runtimeRegistryChangeQueued)
                        return false;

                    _runtimeRegistryChangeQueued = true;
                    return true;

                case ModRegistryEventType.SettingsRegistryChanged:
                    if (_settingsRegistryChangeQueued)
                        return false;

                    _settingsRegistryChangeQueued = true;
                    return true;

                case ModRegistryEventType.RecipeRegistryChanged:
                    if (_recipeRegistryChangeQueued)
                        return false;

                    _recipeRegistryChangeQueued = true;
                    return true;

                case ModRegistryEventType.BuildableRegistryChanged:
                    if (_buildableRegistryChangeQueued)
                        return false;

                    _buildableRegistryChangeQueued = true;
                    return true;

                case ModRegistryEventType.RecycleRegistryChanged:
                    if (_recycleRegistryChangeQueued)
                        return false;

                    _recycleRegistryChangeQueued = true;
                    return true;

                default:
                    return true;
            }
        }

        private static void ClearQueuedFlag(ushort eventType)
        {
            switch ((ModRegistryEventType)eventType)
            {
                case ModRegistryEventType.RuntimeRegistryChanged:
                    _runtimeRegistryChangeQueued = false;
                    break;

                case ModRegistryEventType.SettingsRegistryChanged:
                    _settingsRegistryChangeQueued = false;
                    break;

                case ModRegistryEventType.RecipeRegistryChanged:
                    _recipeRegistryChangeQueued = false;
                    break;

                case ModRegistryEventType.BuildableRegistryChanged:
                    _buildableRegistryChangeQueued = false;
                    break;

                case ModRegistryEventType.RecycleRegistryChanged:
                    _recycleRegistryChangeQueued = false;
                    break;
            }
        }

        private static bool RegisterImmediate(IModRegistryEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i].Listener, listener))
                    return true;
            }

            if (_listenerCount >= ListenerCapacity)
            {
                ReportListenerRegistrationOverflow();
                return false;
            }

            _listeners[_listenerCount++].Listener = listener;
            return true;
        }

        private static bool TryUnregisterImmediate(IModRegistryEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (!ReferenceEquals(_listeners[i].Listener, listener))
                    continue;

                _listenerCount--;
                _listeners[i] = _listeners[_listenerCount];
                _listeners[_listenerCount].Clear();
                return true;
            }

            return false;
        }

        private static void CaptureDispatchSnapshot(int count)
        {
            int safeCount = Mathf.Clamp(count, 0, ListenerCapacity);
            for (int i = 0; i < safeCount; i++)
                _dispatchListeners[i] = _listeners[i].Listener;
        }

        private static void ClearDispatchSnapshot(int count)
        {
            int safeCount = Mathf.Clamp(count, 0, ListenerCapacity);
            for (int i = 0; i < safeCount; i++)
                _dispatchListeners[i] = null;
        }

        private static void EnsureInitialized()
        {
            try
            {
                if (!_pendingEvents.IsCreated)
                {
                    _pendingEvents = new NativeQueue<ModRegistryEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<ModRegistryEventPayload>[5] — deferred coalesced mod registry event lane — owner: ModRegistryEvents
                    RegisterNativeQueue(ref _pendingEvents, PendingEventCapacity, nameof(_pendingEvents), out _pendingEventsSentinelId);
                    PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
                }

                if (!_nextFrameEvents.IsCreated)
                {
                    _nextFrameEvents = new NativeQueue<ModRegistryEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<ModRegistryEventPayload>[5] — next-frame mod registry lane prevents same-frame reentrant dispatch — owner: ModRegistryEvents
                    RegisterNativeQueue(ref _nextFrameEvents, PendingEventCapacity, nameof(_nextFrameEvents), out _nextFrameEventsSentinelId);
                    PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
                }
            }
            catch
            {
                ReleaseNativeQueues();
                _pendingEventCount = 0;
                _nextFrameEventCount = 0;
                _runtimeRegistryChangeQueued = false;
                _settingsRegistryChangeQueued = false;
                _recipeRegistryChangeQueued = false;
                _buildableRegistryChangeQueued = false;
                _recycleRegistryChangeQueued = false;
                ClearOverflowedFlags();
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
                nameof(ModRegistryEvents),
                label,
                NativeAllocationLifetime.Session);
            if (sentinelId > 0)
                return;

            queue.Dispose();
            queue = default;
            throw new System.InvalidOperationException($"Native memory sentinel registration failed for {label}.");
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

        private static void DrainPendingEventsWithoutDispatch()
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

            ClearOverflowedFlags();
        }

        private static bool DrainQueueWithoutDispatch(
            ref NativeQueue<ModRegistryEventPayload> queue,
            ref int pendingCount)
        {
            if (!queue.IsCreated)
                return true;

            int scanBudget = pendingCount > 0 ? pendingCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !queue.IsEmpty())
            {
                if (!queue.TryDequeue(out ModRegistryEventPayload payload))
                    break;

                if (pendingCount > 0)
                    pendingCount--;

                ClearQueuedFlag(payload.EventType);
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

            NativeQueue<ModRegistryEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            int sentinelIdSwap = _pendingEventsSentinelId;
            _pendingEventsSentinelId = _nextFrameEventsSentinelId;
            _nextFrameEventsSentinelId = sentinelIdSwap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }

        private static void ClearOverflowedFlags()
        {
            _runtimeRegistryChangeOverflowed = false;
            _settingsRegistryChangeOverflowed = false;
            _recipeRegistryChangeOverflowed = false;
            _buildableRegistryChangeOverflowed = false;
            _recycleRegistryChangeOverflowed = false;
        }

        private static void ReportQueueOverflow(ModRegistryEventType eventType)
        {
            _droppedEventCount++;
            int frame = ResolveCurrentFrameIndexSafe();
            if (_lastQueueOverflowTelemetryFrame == frame)
                return;

            _lastQueueOverflowTelemetryFrame = frame;
            PublishPerformanceWarningBestEffort(
                RegistryEventQueueOverflowWarningHash,
                RegistryEventQueueContextHash ^ ((uint)eventType << 24),
                _droppedEventCount);
        }

        private static void ReportListenerRegistrationOverflow()
        {
            _droppedListenerRegistrationCount++;
            int frame = ResolveCurrentFrameIndexSafe();
            if (_lastListenerOverflowTelemetryFrame == frame)
                return;

            _lastListenerOverflowTelemetryFrame = frame;
            PublishPerformanceWarningBestEffort(
                RegistryEventListenerOverflowWarningHash,
                RegistryEventListenerContextHash,
                _droppedListenerRegistrationCount);
        }

        private static void ReportListenerDispatchException(ushort eventType, Exception exception)
        {
            _listenerExceptionCount++;
            int frame = ResolveCurrentFrameIndexSafe();
            if (_lastListenerExceptionTelemetryFrame != frame)
            {
                _lastListenerExceptionTelemetryFrame = frame;
                PublishPerformanceWarningBestEffort(
                    RegistryEventListenerExceptionWarningHash,
                    RegistryEventListenerExceptionContextHash ^ ((uint)eventType << 24),
                    _listenerExceptionCount);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.LogWarning("[ModRegistryEvents] listener failed: " + exception.Message);
#endif
        }

        private static int ResolveCurrentFrameIndexSafe()
        {
            try
            {
                return SystemDispatcher.CurrentFrameIndex;
            }
            catch
            {
                return -1;
            }
        }

        private static void PublishPerformanceWarningBestEffort(uint warningHash, uint contextHash, float value)
        {
            try
            {
                GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, value);
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.LogWarning("[ModRegistryEvents] telemetry failed: " + exception.Message);
#endif
            }
        }
    }
}
