using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Crafting;
using Hecton8.Interaction;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.Modding
{
    internal interface IHectonEventChannel
    {
        void Unsubscribe(int subscriptionId);
    }

    internal interface IResettableEventChannel
    {
        void Reset();
    }

    internal interface ISubscriberIsolatableEventChannel
    {
        void DisableSubscriber(string subscriberId);
    }

    /// <summary>
    /// Base type for all typed modding events dispatched through <see cref="HectonEventBus"/>.
    /// </summary>
    internal abstract class HectonEvent
    {
    }

    /// <summary>
    /// Base type for events that may be cancelled by one or more subscribers before the game owner applies the action.
    /// </summary>
    internal abstract class HectonCancellableEvent : HectonEvent
    {
        /// <summary>
        /// True after any subscriber cancels the event.
        /// </summary>
        public bool IsCancelled { get; private set; }

        /// <summary>
        /// Optional diagnostic text supplied by the first subscriber that cancelled the event.
        /// </summary>
        public string CancelReason { get; private set; }

        /// <summary>
        /// Cancels the current event. Cancellation is monotonic and cannot be reverted by later subscribers.
        /// </summary>
        /// <param name="reason">Optional diagnostic reason for development-time logs and debugging.</param>
        public void Cancel(string reason = null)
        {
            if (IsCancelled)
                return;

            IsCancelled = true;
            CancelReason = reason ?? string.Empty;
        }
    }

    /// <summary>
    /// Disposable subscription token returned by <see cref="HectonEventBus.Subscribe{TEvent}(Action{TEvent},string)"/>.
    /// Dispose this token from <c>IHectonMod.OnUnload()</c> to remove the handler safely.
    /// </summary>
    public sealed class HectonEventSubscription : IDisposable
    {
        private IHectonEventChannel _channel;

        internal HectonEventSubscription(IHectonEventChannel channel, int subscriptionId, string subscriberId)
        {
            _channel = channel;
            SubscriptionId = subscriptionId;
            SubscriberId = subscriberId ?? string.Empty;
        }

        /// <summary>
        /// Human-readable subscriber ID used in diagnostics when a mod handler throws.
        /// </summary>
        public string SubscriberId { get; }

        internal int SubscriptionId { get; }

        /// <summary>
        /// True while this token is still attached to an active event channel.
        /// </summary>
        public bool IsActive => _channel != null;

        /// <summary>
        /// Removes the subscription from the owning event channel. Repeated calls are ignored.
        /// </summary>
        public void Dispose()
        {
            if (_channel == null)
                return;

            _channel.Unsubscribe(SubscriptionId);
            _channel = null;
        }
    }

    /// <summary>
    /// Global typed event bus for moddable runtime systems.
    /// First-party gameplay queues such as Save/Quest/Scan are owned separately by their NativeQueue-backed static buses.
    /// Unlike raw C# events, every handler invocation is isolated behind try/catch so one broken mod cannot break the chain.
    /// </summary>
    public static class HectonEventBus
    {
        private const int MaxEventDispatchDepth = 5;
        private const uint ManagedEventCascadeBreakerSubjectHash = 0x45564450u; // EVDP
        private const uint ManagedEventCascadeBreakerFallbackHash = 0x43415343u; // CASC
        private const string RecursiveCascadeCriticalMessage = "[HectonEventBus] RECURSIVE_CASCADE_CRITICAL: dispatch recursion depth exceeded; payload dropped.";
        private const string ModStallWarningMessage = "[HectonEventBus] STALL_WARNING: mod callback exceeded 2.0ms.";
        private const string ModStallDisableReason = "Event callback exceeded 2.0ms watchdog for 3 consecutive frames.";
        private const string ModCallbackExceptionDisableReason = "Event callback exception.";
        private static readonly long _modCallbackWatchdogTicks = Math.Max(1L, (long)(Stopwatch.Frequency * 0.002d));
        // COLD ALLOC: List<IResettableEventChannel>[32] — typed event channel registry for play-session resets — owner: HectonEventBus
        private static readonly List<IResettableEventChannel> _channels = new List<IResettableEventChannel>(32);
        // COLD ALLOC: NativeQueueBridge[1] - read-only first-party queue listener for mod event projection - owner: HectonEventBus
        private static readonly NativeQueueBridge _nativeQueueBridge = new NativeQueueBridge();
        // COLD ALLOC: NativePayloadChannel[1] - immutable byte-span bridge for native payload copies - owner: HectonEventBus
        private static readonly NativePayloadChannel _nativePayloadChannel = new NativePayloadChannel();
        private static int _eventDepthCounter;
        private static int _lastCascadeWarningFrame;
        private static int _lastCascadeTelemetryFrame;
        private static bool _eventCascadeDropActive;
        private static bool _nativeQueueBindingsInstalled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            for (int i = 0; i < _channels.Count; i++)
                _channels[i].Reset();

            _nativePayloadChannel.Reset();
            _eventDepthCounter = 0;
            _lastCascadeWarningFrame = 0;
            _lastCascadeTelemetryFrame = 0;
            _eventCascadeDropActive = false;
            _nativeQueueBindingsInstalled = false;
        }

        /// <summary>
        /// Subscribes a handler to a typed event stream.
        /// Use a stable <paramref name="subscriberId"/> such as a mod ID so exception logs identify the offending mod immediately.
        /// </summary>
        /// <typeparam name="TEvent">Concrete event type to listen for.</typeparam>
        /// <param name="handler">Method invoked whenever the event is published.</param>
        /// <param name="subscriberId">Optional diagnostic owner ID. When omitted, the current mod execution scope is used if available.</param>
        /// <returns>A disposable subscription token, or null when the handler argument is invalid.</returns>
        internal static HectonEventSubscription Subscribe<TEvent>(Action<TEvent> handler, string subscriberId = null)
            where TEvent : HectonEvent
        {
            if (ModExecutionScope.HasActiveMod)
                throw new IllegalContractException("Managed HectonEvent subscriptions are forbidden for mods. Use unmanaged payload subscriptions or SubscribeNative.");

            if (handler == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[HectonEventBus] Cannot subscribe a null handler.");
#endif
                return null;
            }

            string resolvedSubscriberId = string.IsNullOrWhiteSpace(subscriberId)
                ? ModExecutionScope.CurrentModId
                : subscriberId;

            return EventChannelCache<TEvent>.Instance.Subscribe(handler, resolvedSubscriberId);
        }

        /// <summary>
        /// Subscribes a mod-facing handler to an unmanaged payload event stream.
        /// </summary>
        /// <typeparam name="TPayload">Unmanaged event payload type.</typeparam>
        /// <param name="handler">Method invoked on dispatch.</param>
        /// <param name="subscriberId">Stable mod identifier used for isolation.</param>
        /// <returns>A disposable subscription token.</returns>
        public static HectonEventSubscription Subscribe<TPayload>(
            HectonUnmanagedEventHandler<TPayload> handler,
            string subscriberId = null)
            where TPayload : unmanaged
        {
            if (handler == null)
                throw new IllegalContractException("Cannot subscribe a null unmanaged payload handler.");

            string resolvedSubscriberId = string.IsNullOrWhiteSpace(subscriberId)
                ? ModExecutionScope.CurrentModId
                : subscriberId;

            return UnmanagedEventChannelCache<TPayload>.Instance.Subscribe(handler, resolvedSubscriberId);
        }

        /// <summary>
        /// Subscribes to immutable native queue payload copies. Mods receive bytes, never NativeArray/NativeQueue handles.
        /// </summary>
        /// <param name="handler">Callback invoked during the managed bridge flush.</param>
        /// <param name="subscriberId">Stable mod identifier used for automatic isolation on callback failure.</param>
        /// <returns>Subscription token, or null when the handler is invalid.</returns>
        public static HectonEventSubscription SubscribeNative(HectonNativeEventHandler handler, string subscriberId = null)
        {
            if (handler == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[HectonEventBus] Cannot subscribe a null native payload handler.");
#endif
                return null;
            }

            string resolvedSubscriberId = string.IsNullOrWhiteSpace(subscriberId)
                ? ModExecutionScope.CurrentModId
                : subscriberId;

            return _nativePayloadChannel.Subscribe(handler, resolvedSubscriberId);
        }

        /// <summary>
        /// Publishes a typed event to every active subscriber in subscription order.
        /// Exceptions thrown by individual handlers are logged and suppressed so the remaining chain still executes.
        /// </summary>
        /// <typeparam name="TEvent">Concrete event type being dispatched.</typeparam>
        /// <param name="evt">Event payload instance. The same instance is passed to every subscriber.</param>
        /// <returns>The same event instance so caller code can inspect mutations or cancellation state after dispatch.</returns>
        internal static TEvent Publish<TEvent>(TEvent evt)
            where TEvent : HectonEvent
        {
            if (ModExecutionScope.HasActiveMod)
                throw new IllegalContractException("Managed HectonEvent publishing is forbidden for mods. Use ModCommandDispatcher.Request.");

            if (evt == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[HectonEventBus] Cannot publish a null event instance.");
#endif
                return null;
            }

            EventChannelCache<TEvent>.Instance.Publish(evt);
            return evt;
        }

        /// <summary>
        /// Publishes an unmanaged payload to the mod-facing event stream.
        /// </summary>
        /// <typeparam name="TPayload">Unmanaged event payload type.</typeparam>
        /// <param name="payload">Blittable payload.</param>
        public static void Publish<TPayload>(in TPayload payload)
            where TPayload : unmanaged
        {
            UnmanagedEventChannelCache<TPayload>.Instance.Publish(in payload);
        }

        /// <summary>
        /// Installs read-only bridges from first-party NativeQueue event lanes into the managed mod event bus.
        /// </summary>
        internal static void InstallNativeQueueBindings()
        {
            if (_nativeQueueBindingsInstalled)
                return;

            InteractionEvents.Register(_nativeQueueBridge);
            CraftingEvents.Register(_nativeQueueBridge);
            _nativeQueueBindingsInstalled = true;
        }

        /// <summary>
        /// Removes read-only bridges from first-party NativeQueue event lanes.
        /// </summary>
        internal static void UninstallNativeQueueBindings()
        {
            if (!_nativeQueueBindingsInstalled)
                return;

            InteractionEvents.Unregister(_nativeQueueBridge);
            CraftingEvents.Unregister(_nativeQueueBridge);
            _nativeQueueBindingsInstalled = false;
        }

        internal static void RegisterChannel(IResettableEventChannel channel)
        {
            if (channel == null)
                return;

            for (int i = 0; i < _channels.Count; i++)
            {
                if (ReferenceEquals(_channels[i], channel))
                    return;
            }

            _channels.Add(channel);
        }

        internal static void DisableSubscriber(string subscriberId)
        {
            if (string.IsNullOrWhiteSpace(subscriberId))
                return;

            for (int i = 0; i < _channels.Count; i++)
            {
                if (_channels[i] is ISubscriberIsolatableEventChannel isolatableChannel)
                    isolatableChannel.DisableSubscriber(subscriberId);
            }

            _nativePayloadChannel.DisableSubscriber(subscriberId);
        }

        private static bool TryEnterDispatch(uint eventHash)
        {
            if (_eventCascadeDropActive || _eventDepthCounter >= MaxEventDispatchDepth)
            {
                _eventCascadeDropActive = true;
                if (ModExecutionScope.HasActiveMod)
                {
                    string currentModId = ModExecutionScope.CurrentModId;
                    ModCommandDispatcher.QuarantineMod(currentModId);
                    ModLoader.DisableManagedMod(currentModId, "Dispatch recursion depth exceeded.");
                }

                ReportRecursiveCascadeCritical(eventHash);
                return false;
            }

            _eventDepthCounter++;
            return true;
        }

        private static void ReportRecursiveCascadeCritical(uint eventHash)
        {
            CrashTelemetryBuffer.ReportRecursiveCascadeCritical();
            int frame = Time.frameCount;
            if (_lastCascadeTelemetryFrame != frame)
            {
                _lastCascadeTelemetryFrame = frame;
                GlobalTelemetryBus.PublishCatastrophicCascadePrevented(
                    ManagedEventCascadeBreakerSubjectHash,
                    eventHash != 0u ? eventHash : ManagedEventCascadeBreakerFallbackHash,
                    math.max(1, _eventDepthCounter));
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_lastCascadeWarningFrame == frame)
                return;

            _lastCascadeWarningFrame = frame;
            Debug.LogError(RecursiveCascadeCriticalMessage);
#endif
        }

        private static void ExitDispatch()
        {
            if (_eventDepthCounter > 0)
                _eventDepthCounter--;

            if (_eventDepthCounter == 0)
                _eventCascadeDropActive = false;
        }

        private static bool IsSequentialNativePayload<TPayload>()
            where TPayload : unmanaged
        {
            Type payloadType = typeof(TPayload);
            return payloadType == typeof(InteractionEventPayload) ||
                   payloadType == typeof(CraftingEventPayload);
        }

        private static bool HandleCallbackWatchdog(
            string subscriberId,
            uint subscriberHash,
            uint eventHash,
            long elapsedTicks,
            ref int consecutiveStallFrames)
        {
            if (elapsedTicks <= _modCallbackWatchdogTicks)
            {
                consecutiveStallFrames = 0;
                return false;
            }

            consecutiveStallFrames++;
            uint modHash = subscriberHash != 0u ? subscriberHash : ModCommandDispatcher.ComputeModHash(subscriberId);
            float elapsedMilliseconds = elapsedTicks * 1000f / Stopwatch.Frequency;
            GlobalTelemetryBus.PublishModStallWarning(modHash, eventHash, elapsedMilliseconds);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(ModStallWarningMessage);
#endif
            if (consecutiveStallFrames < 3)
                return false;

            ModLoader.DisableManagedMod(subscriberId, ModStallDisableReason);
            return true;
        }

        private static class EventChannelCache<TEvent>
            where TEvent : HectonEvent
        {
            internal static readonly EventChannel<TEvent> Instance = new EventChannel<TEvent>();
        }

        private static class UnmanagedEventChannelCache<TPayload>
            where TPayload : unmanaged
        {
            internal static readonly UnmanagedEventChannel<TPayload> Instance = new UnmanagedEventChannel<TPayload>();
        }

        private sealed class NativeQueueBridge : IInteractionEventListener, ICraftingEventListener
        {
            public void OnInteractionEvent(in InteractionEventPayload payload)
            {
                PublishNativePayload(HectonNativeEventKind.Interaction, in payload);
            }

            public void OnCraftingEvent(in CraftingEventPayload payload)
            {
                PublishNativePayload(HectonNativeEventKind.Crafting, in payload);
            }
        }

        private static void PublishNativePayload<TPayload>(HectonNativeEventKind eventKind, in TPayload payload)
            where TPayload : unmanaged
        {
            if (!IsSequentialNativePayload<TPayload>())
                return;

            TPayload payloadCopy = payload;
            ReadOnlySpan<byte> payloadBytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref payloadCopy, 1));
            _nativePayloadChannel.Publish(eventKind, payloadBytes);
        }

        private sealed class UnmanagedEventChannel<TPayload> : IHectonEventChannel, IResettableEventChannel, ISubscriberIsolatableEventChannel
            where TPayload : unmanaged
        {
            // COLD ALLOC: List<SubscriptionEntry>[8] - unmanaged payload handlers for mod isolation - owner: UnmanagedEventChannel<TPayload>
            private readonly List<SubscriptionEntry> _subscriptions = new List<SubscriptionEntry>(8);
            private readonly uint _eventHash;
            private int _nextSubscriptionId = 1;
            private int _dispatchDepth;
            private bool _needsCompaction;

            internal UnmanagedEventChannel()
            {
                _eventHash = unchecked((uint)Hecton.Localization.LocHash.Compute(typeof(TPayload).FullName ?? typeof(TPayload).Name));
                RegisterChannel(this);
            }

            internal HectonEventSubscription Subscribe(HectonUnmanagedEventHandler<TPayload> handler, string subscriberId)
            {
                string resolvedSubscriberId = string.IsNullOrWhiteSpace(subscriberId) ? "anonymous" : subscriberId;
                SubscriptionEntry entry = new SubscriptionEntry
                {
                    Id = _nextSubscriptionId++,
                    Handler = handler,
                    SubscriberId = resolvedSubscriberId,
                    SubscriberHash = ModCommandDispatcher.ComputeModHash(resolvedSubscriberId),
                    IsActive = true,
                    ConsecutiveStallFrames = 0
                };

                _subscriptions.Add(entry);
                return new HectonEventSubscription(this, entry.Id, entry.SubscriberId);
            }

            internal void Publish(in TPayload payload)
            {
                if (!HectonEventBus.TryEnterDispatch(_eventHash))
                    return;

                _dispatchDepth++;
                try
                {
                    for (int i = 0; i < _subscriptions.Count; i++)
                    {
                        SubscriptionEntry entry = _subscriptions[i];
                        if (!entry.IsActive || entry.Handler == null)
                            continue;

                        long callbackStartTimestamp = Stopwatch.GetTimestamp();
                        long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
                        try
                        {
                            if (ModCommandDispatcher.IsRegisteredMod(entry.SubscriberHash))
                            {
                                using (ModExecutionScope.Enter(entry.SubscriberId, entry.SubscriberHash))
                                {
                                    entry.Handler(in payload);
                                }
                            }
                            else
                            {
                                entry.Handler(in payload);
                            }

                            long callbackElapsedTicks = Stopwatch.GetTimestamp() - callbackStartTimestamp;
                            long allocationDelta = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
                            ModCommandDispatcher.ReportModManagedAllocation(entry.SubscriberHash, allocationDelta);
                            if (HectonEventBus.HandleCallbackWatchdog(
                                    entry.SubscriberId,
                                    entry.SubscriberHash,
                                    _eventHash,
                                    callbackElapsedTicks,
                                    ref entry.ConsecutiveStallFrames))
                            {
                                entry.IsActive = false;
                                entry.Handler = null;
                                _needsCompaction = true;
                            }

                            _subscriptions[i] = entry;
                        }
                        catch (Exception)
                        {
                            long allocationDelta = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
                            ModCommandDispatcher.ReportModManagedAllocation(entry.SubscriberHash, allocationDelta);
                            entry.IsActive = false;
                            entry.Handler = null;
                            _subscriptions[i] = entry;
                            _needsCompaction = true;
                            ModLoader.DisableManagedMod(entry.SubscriberId, ModCallbackExceptionDisableReason);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.LogError("[HectonEventBus] Unmanaged subscriber threw during payload dispatch.");
#endif
                        }
                    }
                }
                finally
                {
                    _dispatchDepth--;
                    HectonEventBus.ExitDispatch();
                    if (_dispatchDepth == 0 && _needsCompaction)
                        CompactInactiveSubscriptions();
                }
            }

            public void Unsubscribe(int subscriptionId)
            {
                for (int i = 0; i < _subscriptions.Count; i++)
                {
                    SubscriptionEntry entry = _subscriptions[i];
                    if (entry.Id != subscriptionId)
                        continue;

                    entry.IsActive = false;
                    entry.Handler = null;
                    _subscriptions[i] = entry;
                    _needsCompaction = true;

                    if (_dispatchDepth == 0)
                        CompactInactiveSubscriptions();
                    return;
                }
            }

            public void Reset()
            {
                _subscriptions.Clear();
                _nextSubscriptionId = 1;
                _dispatchDepth = 0;
                _needsCompaction = false;
            }

            public void DisableSubscriber(string subscriberId)
            {
                for (int i = 0; i < _subscriptions.Count; i++)
                {
                    SubscriptionEntry entry = _subscriptions[i];
                    if (!entry.IsActive || entry.SubscriberId != subscriberId)
                        continue;

                    entry.IsActive = false;
                    entry.Handler = null;
                    _subscriptions[i] = entry;
                    _needsCompaction = true;
                }

                if (_dispatchDepth == 0 && _needsCompaction)
                    CompactInactiveSubscriptions();
            }

            private void CompactInactiveSubscriptions()
            {
                for (int i = _subscriptions.Count - 1; i >= 0; i--)
                {
                    if (!_subscriptions[i].IsActive || _subscriptions[i].Handler == null)
                        _subscriptions.RemoveAt(i);
                }

                _needsCompaction = false;
            }

            private struct SubscriptionEntry
            {
                public int Id;
                public bool IsActive;
                public HectonUnmanagedEventHandler<TPayload> Handler;
                public string SubscriberId;
                public uint SubscriberHash;
                public int ConsecutiveStallFrames;
            }
        }

        private sealed class NativePayloadChannel : IHectonEventChannel, IResettableEventChannel, ISubscriberIsolatableEventChannel
        {
            // COLD ALLOC: List<SubscriptionEntry>[8] - native byte-span handlers for mod isolation - owner: NativePayloadChannel
            private readonly List<SubscriptionEntry> _subscriptions = new List<SubscriptionEntry>(8);
            private int _nextSubscriptionId = 1;
            private int _dispatchDepth;
            private bool _needsCompaction;

            internal HectonEventSubscription Subscribe(HectonNativeEventHandler handler, string subscriberId)
            {
                string resolvedSubscriberId = string.IsNullOrWhiteSpace(subscriberId) ? "anonymous" : subscriberId;
                SubscriptionEntry entry = new SubscriptionEntry
                {
                    Id = _nextSubscriptionId++,
                    Handler = handler,
                    SubscriberId = resolvedSubscriberId,
                    SubscriberHash = ModCommandDispatcher.ComputeModHash(resolvedSubscriberId),
                    IsActive = true,
                    ConsecutiveStallFrames = 0
                };

                _subscriptions.Add(entry);
                return new HectonEventSubscription(this, entry.Id, entry.SubscriberId);
            }

            internal void Publish(HectonNativeEventKind eventKind, ReadOnlySpan<byte> payload)
            {
                if (!HectonEventBus.TryEnterDispatch((uint)eventKind))
                    return;

                _dispatchDepth++;
                try
                {
                    for (int i = 0; i < _subscriptions.Count; i++)
                    {
                        SubscriptionEntry entry = _subscriptions[i];
                        if (!entry.IsActive || entry.Handler == null)
                            continue;

                        long callbackStartTimestamp = Stopwatch.GetTimestamp();
                        long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
                        try
                        {
                            if (ModCommandDispatcher.IsRegisteredMod(entry.SubscriberHash))
                            {
                                using (ModExecutionScope.Enter(entry.SubscriberId, entry.SubscriberHash))
                                {
                                    entry.Handler(eventKind, payload);
                                }
                            }
                            else
                            {
                                entry.Handler(eventKind, payload);
                            }

                            long callbackElapsedTicks = Stopwatch.GetTimestamp() - callbackStartTimestamp;
                            long allocationDelta = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
                            ModCommandDispatcher.ReportModManagedAllocation(entry.SubscriberHash, allocationDelta);
                            if (HectonEventBus.HandleCallbackWatchdog(
                                    entry.SubscriberId,
                                    entry.SubscriberHash,
                                    (uint)eventKind,
                                    callbackElapsedTicks,
                                    ref entry.ConsecutiveStallFrames))
                            {
                                entry.IsActive = false;
                                entry.Handler = null;
                                _needsCompaction = true;
                            }

                            _subscriptions[i] = entry;
                        }
                        catch (Exception)
                        {
                            long allocationDelta = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
                            ModCommandDispatcher.ReportModManagedAllocation(entry.SubscriberHash, allocationDelta);
                            entry.IsActive = false;
                            entry.Handler = null;
                            _subscriptions[i] = entry;
                            _needsCompaction = true;
                            ModLoader.DisableManagedMod(entry.SubscriberId, ModCallbackExceptionDisableReason);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.LogError("[HectonEventBus] Native subscriber threw during payload dispatch.");
#endif
                        }
                    }
                }
                finally
                {
                    _dispatchDepth--;
                    HectonEventBus.ExitDispatch();
                    if (_dispatchDepth == 0 && _needsCompaction)
                        CompactInactiveSubscriptions();
                }
            }

            public void Unsubscribe(int subscriptionId)
            {
                for (int i = 0; i < _subscriptions.Count; i++)
                {
                    SubscriptionEntry entry = _subscriptions[i];
                    if (entry.Id != subscriptionId)
                        continue;

                    entry.IsActive = false;
                    entry.Handler = null;
                    _subscriptions[i] = entry;
                    _needsCompaction = true;

                    if (_dispatchDepth == 0)
                        CompactInactiveSubscriptions();
                    return;
                }
            }

            public void Reset()
            {
                _subscriptions.Clear();
                _nextSubscriptionId = 1;
                _dispatchDepth = 0;
                _needsCompaction = false;
            }

            public void DisableSubscriber(string subscriberId)
            {
                for (int i = 0; i < _subscriptions.Count; i++)
                {
                    SubscriptionEntry entry = _subscriptions[i];
                    if (!entry.IsActive || entry.SubscriberId != subscriberId)
                        continue;

                    entry.IsActive = false;
                    entry.Handler = null;
                    _subscriptions[i] = entry;
                    _needsCompaction = true;
                }

                if (_dispatchDepth == 0 && _needsCompaction)
                    CompactInactiveSubscriptions();
            }

            private void CompactInactiveSubscriptions()
            {
                for (int i = _subscriptions.Count - 1; i >= 0; i--)
                {
                    if (!_subscriptions[i].IsActive || _subscriptions[i].Handler == null)
                        _subscriptions.RemoveAt(i);
                }

                _needsCompaction = false;
            }

            private struct SubscriptionEntry
            {
                public int Id;
                public bool IsActive;
                public HectonNativeEventHandler Handler;
                public string SubscriberId;
                public uint SubscriberHash;
                public int ConsecutiveStallFrames;
            }
        }

        private sealed class EventChannel<TEvent> : IHectonEventChannel, IResettableEventChannel, ISubscriberIsolatableEventChannel
            where TEvent : HectonEvent
        {
            // COLD ALLOC: List<SubscriptionEntry>[8] — handler list for one typed mod event stream — owner: EventChannel<TEvent>
            private readonly List<SubscriptionEntry> _subscriptions = new List<SubscriptionEntry>(8);
            private readonly uint _eventHash;
            private int _nextSubscriptionId = 1;
            private int _dispatchDepth;
            private bool _needsCompaction;

            internal EventChannel()
            {
                _eventHash = unchecked((uint)Hecton.Localization.LocHash.Compute(typeof(TEvent).FullName ?? typeof(TEvent).Name));
                RegisterChannel(this);
            }

            internal HectonEventSubscription Subscribe(Action<TEvent> handler, string subscriberId)
            {
                string resolvedSubscriberId = string.IsNullOrWhiteSpace(subscriberId) ? "anonymous" : subscriberId;
                SubscriptionEntry entry = new SubscriptionEntry
                {
                    Id = _nextSubscriptionId++,
                    Handler = handler,
                    SubscriberId = resolvedSubscriberId,
                    SubscriberHash = ModCommandDispatcher.ComputeModHash(resolvedSubscriberId),
                    IsActive = true,
                    ConsecutiveStallFrames = 0
                };

                _subscriptions.Add(entry);
                return new HectonEventSubscription(this, entry.Id, entry.SubscriberId);
            }

            internal void Publish(TEvent evt)
            {
                if (!HectonEventBus.TryEnterDispatch(_eventHash))
                    return;

                _dispatchDepth++;

                try
                {
                    for (int i = 0; i < _subscriptions.Count; i++)
                    {
                        SubscriptionEntry entry = _subscriptions[i];
                        if (!entry.IsActive || entry.Handler == null)
                            continue;

                        long callbackStartTimestamp = Stopwatch.GetTimestamp();
                        long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
                        try
                        {
                            if (ModCommandDispatcher.IsRegisteredMod(entry.SubscriberHash))
                            {
                                using (ModExecutionScope.Enter(entry.SubscriberId, entry.SubscriberHash))
                                {
                                    entry.Handler(evt);
                                }
                            }
                            else
                            {
                                entry.Handler(evt);
                            }

                            long callbackElapsedTicks = Stopwatch.GetTimestamp() - callbackStartTimestamp;
                            long allocationDelta = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
                            ModCommandDispatcher.ReportModManagedAllocation(entry.SubscriberHash, allocationDelta);
                            if (HectonEventBus.HandleCallbackWatchdog(
                                    entry.SubscriberId,
                                    entry.SubscriberHash,
                                    _eventHash,
                                    callbackElapsedTicks,
                                    ref entry.ConsecutiveStallFrames))
                            {
                                entry.IsActive = false;
                                entry.Handler = null;
                                _needsCompaction = true;
                            }

                            _subscriptions[i] = entry;
                        }
                        catch (Exception)
                        {
                            long allocationDelta = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
                            ModCommandDispatcher.ReportModManagedAllocation(entry.SubscriberHash, allocationDelta);
                            entry.IsActive = false;
                            entry.Handler = null;
                            _subscriptions[i] = entry;
                            _needsCompaction = true;
                            ModLoader.DisableManagedMod(entry.SubscriberId, ModCallbackExceptionDisableReason);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.LogError("[HectonEventBus] Subscriber threw during managed payload dispatch.");
#endif
                        }
                    }
                }
                finally
                {
                    _dispatchDepth--;
                    HectonEventBus.ExitDispatch();
                    if (_dispatchDepth == 0 && _needsCompaction)
                        CompactInactiveSubscriptions();
                }
            }

            public void Unsubscribe(int subscriptionId)
            {
                for (int i = 0; i < _subscriptions.Count; i++)
                {
                    SubscriptionEntry entry = _subscriptions[i];
                    if (entry.Id != subscriptionId)
                        continue;

                    entry.IsActive = false;
                    entry.Handler = null;
                    _subscriptions[i] = entry;
                    _needsCompaction = true;

                    if (_dispatchDepth == 0)
                        CompactInactiveSubscriptions();
                    return;
                }
            }

            public void Reset()
            {
                _subscriptions.Clear();
                _nextSubscriptionId = 1;
                _dispatchDepth = 0;
                _needsCompaction = false;
            }

            public void DisableSubscriber(string subscriberId)
            {
                for (int i = 0; i < _subscriptions.Count; i++)
                {
                    SubscriptionEntry entry = _subscriptions[i];
                    if (!entry.IsActive || entry.SubscriberId != subscriberId)
                        continue;

                    entry.IsActive = false;
                    entry.Handler = null;
                    _subscriptions[i] = entry;
                    _needsCompaction = true;
                }

                if (_dispatchDepth == 0 && _needsCompaction)
                    CompactInactiveSubscriptions();
            }

            private void CompactInactiveSubscriptions()
            {
                for (int i = _subscriptions.Count - 1; i >= 0; i--)
                {
                    if (!_subscriptions[i].IsActive || _subscriptions[i].Handler == null)
                        _subscriptions.RemoveAt(i);
                }

                _needsCompaction = false;
            }

            private struct SubscriptionEntry
            {
                public int Id;
                public bool IsActive;
                public Action<TEvent> Handler;
                public string SubscriberId;
                public uint SubscriberHash;
                public int ConsecutiveStallFrames;
            }
        }
    }
}
