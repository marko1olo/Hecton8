using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Modding
{
    /// <summary>
    /// Mod registry event discriminator for <see cref="ModRegistryEventPayload"/>.
    /// </summary>
    public enum ModRegistryEventType : ushort
    {
        RuntimeRegistryChanged = 1,
        SettingsRegistryChanged = 2,
        RecipeRegistryChanged = 3,
        BuildableRegistryChanged = 4
    }

    /// <summary>
    /// Deferred unmanaged payload for mod registry invalidation events.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ModRegistryEventPayload
    {
        public uint Frame;
        public uint ModHash;
        public uint SubjectHash;
        public ushort EventType;
        public ushort StatusBits;
    }

    /// <summary>
    /// Listener contract for deferred mod registry events.
    /// </summary>
    public interface IModRegistryEventListener
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

        // COLD ALLOC: RegistryBucket<IModRegistryEventListener>[32] - mod registry invalidation listeners drained by SystemDispatcher - owner: ModRegistryEvents
        private static readonly RegistryBucket<IModRegistryEventListener> _listeners = new RegistryBucket<IModRegistryEventListener>(ListenerCapacity);
        private static NativeQueue<ModRegistryEventPayload> _pendingEvents;
        private static NativeQueue<ModRegistryEventPayload> _nextFrameEvents;
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
        private static void ResetStaticState()
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

            _listeners.Clear();
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
            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        /// <summary>
        /// Unregisters a mod registry event listener.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        internal static void Unregister(IModRegistryEventListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
                _listeners.Unregister(listener);
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

            if (_listeners.Count <= 0)
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

                IModRegistryEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IModRegistryEventListener listener = rawArray[i];
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
                Frame = unchecked((uint)Mathf.Max(0, Time.frameCount)),
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

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<ModRegistryEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<ModRegistryEventPayload>[4] - deferred coalesced mod registry event lane - owner: ModRegistryEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(ModRegistryEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<ModRegistryEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<ModRegistryEventPayload>[4] - next-frame mod registry lane prevents same-frame reentrant dispatch - owner: ModRegistryEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(ModRegistryEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
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
