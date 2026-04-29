using System;
using Hecton.Localization;
using Hecton8.AtlasSignal;
using Hecton8.Celestial;
using Hecton8.Core;
using Hecton8.Modding;
using Hecton8.Narrative;
using UnityEngine;

namespace Hecton8.Quest
{
    internal sealed class QuestGraphEvaluator : IDisposable
    {
        private const string EventSubscriberId = "quest.graph.evaluator";
        private const int MaxSignalDepth = 4;
        private const float DepthTierTwoMeters = 100f;
        private const float DepthTierThreeMeters = 300f;
        private const float DepthTierFourMeters = 1000f;

        private readonly QuestStateManager _stateManager;
        private readonly Action _onResultsAvailable;

        private HectonEventSubscription _itemCollectedSubscription;
        private HectonEventSubscription _itemDiscardedSubscription;
        private HectonEventSubscription _biomeDiscoveredSubscription;
        private HectonEventSubscription _loreAcquiredSubscription;
        private bool _isBound;
        private int _signalDepth;

        public QuestGraphEvaluator(QuestStateManager stateManager, Action onResultsAvailable)
        {
            _stateManager = stateManager;
            _onResultsAvailable = onResultsAvailable;
        }

        public void Dispose()
        {
            Unbind();
        }

        public void Bind()
        {
            if (_isBound)
                return;

            _itemCollectedSubscription = HectonEventBus.Subscribe<ItemCollectedEvent>(HandleItemCollected, EventSubscriberId);
            _itemDiscardedSubscription = HectonEventBus.Subscribe<ItemDiscardedEvent>(HandleItemDiscarded, EventSubscriberId);
            _biomeDiscoveredSubscription = HectonEventBus.Subscribe<BiomeDiscoveredEvent>(HandleBiomeDiscovered, EventSubscriberId);
            _loreAcquiredSubscription = HectonEventBus.Subscribe<Hecton8.Narrative.LoreAcquiredEvent>(HandleLoreAcquired, EventSubscriberId);
            NarrativeEvents.OnDepthTierReached += HandleDepthTierReached;
            HectonCelestialEngine.OnEclipseStart += HandleEclipseStart;
            AtlasSignalEvents.OnSignalDecoded += HandleSignalDecoded;
            _isBound = true;
        }

        public void Unbind()
        {
            if (!_isBound)
                return;

            _itemCollectedSubscription?.Dispose();
            _itemCollectedSubscription = null;
            _itemDiscardedSubscription?.Dispose();
            _itemDiscardedSubscription = null;
            _biomeDiscoveredSubscription?.Dispose();
            _biomeDiscoveredSubscription = null;
            _loreAcquiredSubscription?.Dispose();
            _loreAcquiredSubscription = null;
            NarrativeEvents.OnDepthTierReached -= HandleDepthTierReached;
            HectonCelestialEngine.OnEclipseStart -= HandleEclipseStart;
            AtlasSignalEvents.OnSignalDecoded -= HandleSignalDecoded;
            _isBound = false;
        }

        public void UpdateDepth(float depthMeters)
        {
            Evaluate(new QuestEventPayload
            {
                EventType = (ushort)QuestSignalKind.DepthReached,
                Timestamp = Time.timeAsDouble,
                NumericValue = depthMeters
            });
        }

        public void Evaluate(in QuestEventPayload payload)
        {
            if (_stateManager == null || _signalDepth >= MaxSignalDepth)
                return;

            _signalDepth++;
            try
            {
                _stateManager.EvaluateSignal(payload);
                _onResultsAvailable?.Invoke();
            }
            finally
            {
                _signalDepth--;
            }
        }

        private void HandleItemCollected(ItemCollectedEvent evt)
        {
            if (evt == null)
                return;

            uint itemHash = evt.ItemHashId != 0
                ? unchecked((uint)evt.ItemHashId)
                : evt.Item != null
                    ? QuestFlagHashKernel.ComputeStableHash(evt.Item.PersistentId)
                    : 0u;

            Evaluate(new QuestEventPayload
            {
                EntityHash = itemHash,
                EventType = (ushort)QuestSignalKind.ItemCollected,
                ItemId = itemHash,
                Timestamp = Time.timeAsDouble,
                NumericValue = evt.Quantity
            });
        }

        private void HandleItemDiscarded(ItemDiscardedEvent evt)
        {
            if (evt == null || _stateManager == null)
                return;

            uint itemHash = evt.Item != null
                ? QuestFlagHashKernel.ComputeStableHash(evt.Item.PersistentId)
                : 0u;
            if (itemHash == 0u)
                return;

            if (_stateManager.TryRevertCriticalItem(
                    itemHash,
                    Time.timeAsDouble,
                    out QuestRevertRequest revertRequest))
            {
                QuestEvents.RaiseRevertRequested(in revertRequest);
                _onResultsAvailable?.Invoke();
            }
        }

        private void HandleBiomeDiscovered(BiomeDiscoveredEvent evt)
        {
            if (evt == null)
                return;

            Evaluate(new QuestEventPayload
            {
                EventType = (ushort)QuestSignalKind.BiomeEntered,
                Timestamp = Time.timeAsDouble,
                NumericValue = evt.BiomeId
            });
        }

        private void HandleLoreAcquired(Hecton8.Narrative.LoreAcquiredEvent evt)
        {
            if (evt == null)
                return;

            double timestamp = Time.timeAsDouble;
            QuestEventPayload discoveryPayload = new QuestEventPayload
            {
                EntityHash = evt.LoreHash,
                EventType = (ushort)QuestSignalKind.DiscoveryMade,
                Timestamp = timestamp
            };
            Evaluate(discoveryPayload);

            QuestEventPayload audioLogPayload = new QuestEventPayload
            {
                EntityHash = evt.LoreHash,
                EventType = (ushort)QuestSignalKind.AudioLogFound,
                Timestamp = timestamp
            };
            Evaluate(audioLogPayload);
        }

        private void HandleDepthTierReached(int tier)
        {
            UpdateDepth(MapDepthTierToMeters(tier));
        }

        private void HandleEclipseStart()
        {
            Evaluate(new QuestEventPayload
            {
                EventType = (ushort)QuestSignalKind.EclipseStarted,
                Timestamp = Time.timeAsDouble
            });
        }

        private void HandleSignalDecoded(string messageId)
        {
            uint payloadHash = string.IsNullOrWhiteSpace(messageId)
                ? 0u
                : QuestFlagHashKernel.ComputeStableHash(messageId);
            Evaluate(new QuestEventPayload
            {
                EntityHash = payloadHash,
                EventType = (ushort)QuestSignalKind.SignalDecoded,
                Timestamp = Time.timeAsDouble
            });
        }

        private static float MapDepthTierToMeters(int tier)
        {
            switch (tier)
            {
                case 2:
                    return DepthTierTwoMeters;
                case 3:
                    return DepthTierThreeMeters;
                case 4:
                    return DepthTierFourMeters;
                default:
                    return 0f;
            }
        }
    }
}
