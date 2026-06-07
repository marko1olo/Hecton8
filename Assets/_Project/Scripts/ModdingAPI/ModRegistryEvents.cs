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
        BuildableRegistryChanged = 4
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
        private const int PendingEventCapacity = 4;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;

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
        private static NativeQueue<ModRegistryEventPayload> _pendingEvents;
        private static NativeQueue<ModRegistryEventPayload> _nextFrameEvents;
        private static int _listenerCount;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;
        private static bool _runtimeRegistryChangeQueued;
        private static bool _settingsRegistryChangeQueued;
        private static bool _recipeRegistryChangeQueued;
        private static bool _buildableRegistryChangeQueued;

        /// <summary>
        /// Pending payload count in the mod registry event lane.
        /// </summary>
        internal static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStaticState()
        {
            ReleaseNativeQueues();

            for (int i = 0; i < _listenerCount; i++)
                _listeners[i].Clear();

            _listenerCount = 0;
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
            _runtimeRegistryChangeQueued = false;
            _settingsRegistryChangeQueued = false;
            _recipeRegistryChangeQueued = false;
            _buildableRegistryChangeQueued = false;
        }

        /// <summary>
        /// Registers a mod registry event listener.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        internal static void Register(IModRegistryEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            RegisterImmediate(listener);
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
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IModRegistryEventListener listener = _listeners[i].Listener;
                        if (listener != null)
                            listener.OnModRegistryEvent(in payload);
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

        private static void Enqueue(ModRegistryEventType eventType, uint modHash, uint subjectHash, ushort statusBits)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return;

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
            }
        }

        private static void RegisterImmediate(IModRegistryEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i].Listener, listener))
                    return;
            }

            if (_listenerCount >= ListenerCapacity)
                return;

            _listeners[_listenerCount++].Listener = listener;
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

        private static void EnsureInitialized()
        {
            try
            {
                if (!_pendingEvents.IsCreated)
                {
                    _pendingEvents = new NativeQueue<ModRegistryEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<ModRegistryEventPayload>[4] — deferred coalesced mod registry event lane — owner: ModRegistryEvents
                    RegisterNativeQueue(ref _pendingEvents, PendingEventCapacity, nameof(_pendingEvents));
                    PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
                }

                if (!_nextFrameEvents.IsCreated)
                {
                    _nextFrameEvents = new NativeQueue<ModRegistryEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<ModRegistryEventPayload>[4] — next-frame mod registry lane prevents same-frame reentrant dispatch — owner: ModRegistryEvents
                    RegisterNativeQueue(ref _nextFrameEvents, PendingEventCapacity, nameof(_nextFrameEvents));
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
                throw;
            }
        }

        private static void RegisterNativeQueue<T>(
            ref NativeQueue<T> queue,
            int capacity,
            string label)
            where T : unmanaged
        {
            int sentinelId = NativeMemorySentinel.RegisterNativeQueue(
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
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(ModRegistryEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(ModRegistryEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
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
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }
    }
}
