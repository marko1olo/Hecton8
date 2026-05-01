using System;
using Hecton.Localization;
using Hecton8.AtlasSignal;
using Hecton8.Celestial;
using Hecton8.Core;
using Hecton8.Modding;
using Hecton8.Narrative;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Quest
{
    internal sealed class QuestGraphEvaluator : IDisposable, INarrativeEventListener, IAtlasSignalEventListener
    {
        private const string EventSubscriberId = "quest.graph.evaluator";
        private const float DepthTierTwoMeters = 100f;
        private const float DepthTierThreeMeters = 300f;
        private const float DepthTierFourMeters = 1000f;
        private static readonly uint _deepAbyssZoneHash = QuestFlagHashKernel.ComputeStableHash("zone_deep_abyss");
        private static readonly RegistryBucket<QuestGraphEvaluator> _activeEvaluators = new RegistryBucket<QuestGraphEvaluator>(4);

        private readonly QuestStateManager _stateManager;
        private readonly Action _onResultsAvailable;

        private HectonEventSubscription _itemCollectedSubscription;
        private HectonEventSubscription _itemDiscardedSubscription;
        private HectonEventSubscription _biomeDiscoveredSubscription;
        private HectonEventSubscription _loreAcquiredSubscription;
        private NativeQueue<QuestSignalPayload> _pendingSignals;
        private bool _isBound;
        private bool _isDrainingSignals;

        public QuestGraphEvaluator(QuestStateManager stateManager, Action onResultsAvailable)
        {
            _stateManager = stateManager;
            _onResultsAvailable = onResultsAvailable;
            _pendingSignals = new NativeQueue<QuestSignalPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<QuestSignalPayload>[16] - quest signal ingress lane drained on event receipt - owner: QuestGraphEvaluator
        }

        public void Dispose()
        {
            Unbind();

            if (_pendingSignals.IsCreated)
                _pendingSignals.Dispose();
        }

        public void Bind()
        {
            if (_isBound)
                return;

            _itemCollectedSubscription = HectonEventBus.Subscribe<ItemCollectedEvent>(HandleItemCollected, EventSubscriberId);
            _itemDiscardedSubscription = HectonEventBus.Subscribe<ItemDiscardedEvent>(HandleItemDiscarded, EventSubscriberId);
            _biomeDiscoveredSubscription = HectonEventBus.Subscribe<BiomeDiscoveredEvent>(HandleBiomeDiscovered, EventSubscriberId);
            _loreAcquiredSubscription = HectonEventBus.Subscribe<LoreAcquiredEvent>(HandleLoreAcquired, EventSubscriberId);
            NarrativeEvents.Register(this);
            HectonCelestialEngine.OnEclipseStart += HandleEclipseStart;
            AtlasSignalEvents.Register(this);
            _activeEvaluators.Register(this);
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
            NarrativeEvents.Unregister(this);
            HectonCelestialEngine.OnEclipseStart -= HandleEclipseStart;
            AtlasSignalEvents.Unregister(this);
            _activeEvaluators.Unregister(this);
            _isBound = false;

            while (_pendingSignals.IsCreated && _pendingSignals.TryDequeue(out _))
            {
            }
        }

        public void UpdateDepth(float depthMeters)
        {
            UpdateDepthContext(depthMeters, 0u, false);
        }

        public void UpdateDepthContext(float depthMeters, uint zoneHash, bool isThermalZone)
        {
            QuestSignalContextFlags flags = QuestSignalContextFlags.None;
            if (isThermalZone)
                flags |= QuestSignalContextFlags.ThermalPhase;
            if (zoneHash == _deepAbyssZoneHash)
                flags |= QuestSignalContextFlags.AbyssalPhase;

            EnqueueSignal(new QuestSignalPayload
            {
                EventType = (ushort)QuestSignalKind.DepthReached,
                Timestamp = Time.timeAsDouble,
                NumericValue = depthMeters,
                Flags = (uint)flags
            });
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

            EnqueueSignal(new QuestSignalPayload
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

            EnqueueSignal(new QuestSignalPayload
            {
                EventType = (ushort)QuestSignalKind.BiomeEntered,
                Timestamp = Time.timeAsDouble,
                NumericValue = evt.BiomeId
            });
        }

        private void HandleLoreAcquired(LoreAcquiredEvent evt)
        {
            if (evt == null)
                return;

            double timestamp = Time.timeAsDouble;
            EnqueueSignal(new QuestSignalPayload
            {
                EntityHash = evt.LoreHash,
                EventType = (ushort)QuestSignalKind.DiscoveryMade,
                Timestamp = timestamp
            });
            EnqueueSignal(new QuestSignalPayload
            {
                EntityHash = evt.LoreHash,
                EventType = (ushort)QuestSignalKind.AudioLogFound,
                Timestamp = timestamp
            });
        }

        public void OnNarrativeEvent(in NarrativeEventPayload payload)
        {
            if ((NarrativeEventType)payload.EventType != NarrativeEventType.DepthTierReached)
                return;

            UpdateDepth(MapDepthTierToMeters(payload.DepthTier));
        }

        private void HandleEclipseStart()
        {
            EnqueueSignal(new QuestSignalPayload
            {
                EventType = (ushort)QuestSignalKind.EclipseStarted,
                Timestamp = Time.timeAsDouble
            });
        }

        public void OnAtlasSignalEvent(in AtlasSignalEventPayload payload)
        {
            if ((AtlasSignalEventType)payload.EventType != AtlasSignalEventType.Decoded)
                return;

            EnqueueSignal(new QuestSignalPayload
            {
                EntityHash = payload.MessageHash,
                EventType = (ushort)QuestSignalKind.SignalDecoded,
                Timestamp = Time.timeAsDouble
            });
        }

        private void HandleSignalDecoded(string messageId)
        {
            uint payloadHash = string.IsNullOrWhiteSpace(messageId)
                ? 0u
                : QuestFlagHashKernel.ComputeStableHash(messageId);
            EnqueueSignal(new QuestSignalPayload
            {
                EntityHash = payloadHash,
                EventType = (ushort)QuestSignalKind.SignalDecoded,
                Timestamp = Time.timeAsDouble
            });
        }

        private void EnqueueSignal(in QuestSignalPayload payload)
        {
            if (!_pendingSignals.IsCreated)
                return;

            _pendingSignals.Enqueue(payload);
        }

        private bool DrainPendingSignals()
        {
            if (_stateManager == null || !_pendingSignals.IsCreated || _isDrainingSignals)
                return true;

            _isDrainingSignals = true;
            try
            {
                while (!_pendingSignals.IsEmpty())
                {
                    if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                        return false;

                    if (!_pendingSignals.TryDequeue(out QuestSignalPayload payload))
                        break;

                    _stateManager.EvaluateSignal(payload);
                    _onResultsAvailable?.Invoke();
                }
            }
            finally
            {
                _isDrainingSignals = false;
            }

            return true;
        }

        internal static void FlushPendingSignals()
        {
            QuestGraphEvaluator[] rawArray = _activeEvaluators.RawArray;
            int count = _activeEvaluators.Count;
            for (int i = count - 1; i >= 0; i--)
            {
                if (!rawArray[i].DrainPendingSignals())
                    return;
            }
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
