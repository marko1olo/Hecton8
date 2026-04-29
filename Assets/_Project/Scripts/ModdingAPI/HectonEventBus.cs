using System;
using System.Collections.Generic;
using UnityEngine;

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

    /// <summary>
    /// Base type for all typed modding events dispatched through <see cref="HectonEventBus"/>.
    /// </summary>
    public abstract class HectonEvent
    {
    }

    /// <summary>
    /// Base type for events that may be cancelled by one or more subscribers before the game owner applies the action.
    /// </summary>
    public abstract class HectonCancellableEvent : HectonEvent
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
        // COLD ALLOC: List<IResettableEventChannel>[32] — typed event channel registry for play-session resets — owner: HectonEventBus
        private static readonly List<IResettableEventChannel> _channels = new List<IResettableEventChannel>(32);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            for (int i = 0; i < _channels.Count; i++)
                _channels[i].Reset();
        }

        /// <summary>
        /// Subscribes a handler to a typed event stream.
        /// Use a stable <paramref name="subscriberId"/> such as a mod ID so exception logs identify the offending mod immediately.
        /// </summary>
        /// <typeparam name="TEvent">Concrete event type to listen for.</typeparam>
        /// <param name="handler">Method invoked whenever the event is published.</param>
        /// <param name="subscriberId">Optional diagnostic owner ID. When omitted, the current mod execution scope is used if available.</param>
        /// <returns>A disposable subscription token, or null when the handler argument is invalid.</returns>
        public static HectonEventSubscription Subscribe<TEvent>(Action<TEvent> handler, string subscriberId = null)
            where TEvent : HectonEvent
        {
            if (handler == null)
            {
                Debug.LogError("[HectonEventBus] Cannot subscribe a null handler.");
                return null;
            }

            string resolvedSubscriberId = string.IsNullOrWhiteSpace(subscriberId)
                ? ModExecutionScope.CurrentModId
                : subscriberId;

            return EventChannelCache<TEvent>.Instance.Subscribe(handler, resolvedSubscriberId);
        }

        /// <summary>
        /// Publishes a typed event to every active subscriber in subscription order.
        /// Exceptions thrown by individual handlers are logged and suppressed so the remaining chain still executes.
        /// </summary>
        /// <typeparam name="TEvent">Concrete event type being dispatched.</typeparam>
        /// <param name="evt">Event payload instance. The same instance is passed to every subscriber.</param>
        /// <returns>The same event instance so caller code can inspect mutations or cancellation state after dispatch.</returns>
        public static TEvent Publish<TEvent>(TEvent evt)
            where TEvent : HectonEvent
        {
            if (evt == null)
            {
                Debug.LogError("[HectonEventBus] Cannot publish a null event instance.");
                return null;
            }

            EventChannelCache<TEvent>.Instance.Publish(evt);
            return evt;
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

        private static class EventChannelCache<TEvent>
            where TEvent : HectonEvent
        {
            internal static readonly EventChannel<TEvent> Instance = new EventChannel<TEvent>();
        }

        private sealed class EventChannel<TEvent> : IHectonEventChannel, IResettableEventChannel
            where TEvent : HectonEvent
        {
            // COLD ALLOC: List<SubscriptionEntry>[8] — handler list for one typed mod event stream — owner: EventChannel<TEvent>
            private readonly List<SubscriptionEntry> _subscriptions = new List<SubscriptionEntry>(8);
            private int _nextSubscriptionId = 1;
            private int _dispatchDepth;
            private bool _needsCompaction;

            internal EventChannel()
            {
                RegisterChannel(this);
            }

            internal HectonEventSubscription Subscribe(Action<TEvent> handler, string subscriberId)
            {
                SubscriptionEntry entry = new SubscriptionEntry
                {
                    Id = _nextSubscriptionId++,
                    Handler = handler,
                    SubscriberId = string.IsNullOrWhiteSpace(subscriberId) ? "anonymous" : subscriberId,
                    IsActive = true
                };

                _subscriptions.Add(entry);
                return new HectonEventSubscription(this, entry.Id, entry.SubscriberId);
            }

            internal void Publish(TEvent evt)
            {
                _dispatchDepth++;

                try
                {
                    for (int i = 0; i < _subscriptions.Count; i++)
                    {
                        SubscriptionEntry entry = _subscriptions[i];
                        if (!entry.IsActive || entry.Handler == null)
                            continue;

                        try
                        {
                            entry.Handler(evt);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError(
                                $"[HectonEventBus] Subscriber '{entry.SubscriberId}' threw while handling '{typeof(TEvent).Name}': {ex}");
                        }
                    }
                }
                finally
                {
                    _dispatchDepth--;
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
            }
        }
    }
}
