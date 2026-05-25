using System;
using System.Runtime.CompilerServices;
using Hecton8.AtlasSignal;
using Hecton8.Celestial;
using Hecton8.Core;
using Hecton8.Crafting;
using Hecton8.Environment;
using Hecton8.Interaction;
using Hecton8.Items;
using Hecton8.Narrative;
using Hecton.Localization;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Quest
{
    internal sealed class QuestGraphEvaluator : IDisposable, INarrativeEventListener, IAtlasSignalEventListener, ICelestialEventListener, ICraftingEventListener, IInteractionEventListener, IBiomeMatrixEventListener
    {
        private const float DepthTierTwoMeters = 100f;
        private const float DepthTierThreeMeters = 300f;
        private const float DepthTierFourMeters = 1000f;
        private const int PendingSignalCapacity = 16;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
        private static readonly uint _deepAbyssZoneHash = QuestFlagHashKernel.ComputeStableHash("zone_deep_abyss");
        private static readonly uint _PendingSignalOverflowWarningHash = unchecked((uint)LocHash.Compute("QuestGraphEvaluator.PendingSignalOverflow"));
        private static readonly uint _ActiveEvaluatorRejectedWarningHash = unchecked((uint)LocHash.Compute("QuestGraphEvaluator.ActiveEvaluatorRejected"));
        private static readonly uint _PendingSignalContextHash = unchecked((uint)LocHash.Compute("QuestGraphEvaluator.PendingSignals"));
        private static readonly uint _ActiveEvaluatorContextHash = unchecked((uint)LocHash.Compute("QuestGraphEvaluator.ActiveEvaluators"));
        private static readonly RegistryBucket<QuestGraphEvaluator> _activeEvaluators = new RegistryBucket<QuestGraphEvaluator>(4);
        private static int _activeEvaluatorRejectCount;
        private static int _lastActiveEvaluatorRejectedTelemetryFrame = -1;

        private readonly QuestStateManager _stateManager;
        private readonly Action _onResultsAvailable;
        private readonly string _pendingSignalsSentinelLabel;

        private NativeQueue<QuestSignalPayload> _pendingSignals;
        private int _pendingSignalCount;
        private int _droppedSignalCount;
        private int _lastPendingSignalOverflowTelemetryFrame = -1;
        private bool _isBound;
        private bool _isDrainingSignals;

        internal int DroppedSignalCount => _droppedSignalCount;
        internal static int ActiveEvaluatorRejectCount => _activeEvaluatorRejectCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _activeEvaluators.Clear();
            _activeEvaluatorRejectCount = 0;
            _lastActiveEvaluatorRejectedTelemetryFrame = -1;
        }

        public QuestGraphEvaluator(QuestStateManager stateManager, Action onResultsAvailable)
        {
            _stateManager = stateManager;
            _onResultsAvailable = onResultsAvailable;
            _pendingSignalsSentinelLabel = nameof(_pendingSignals) + RuntimeHelpers.GetHashCode(this);
            _pendingSignals = new NativeQueue<QuestSignalPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<QuestSignalPayload>[16] — quest signal ingress lane drained on event receipt — owner: QuestGraphEvaluator
            NativeMemorySentinel.RegisterNativeQueue(
                _pendingSignals,
                PendingSignalCapacity,
                nameof(QuestGraphEvaluator),
                _pendingSignalsSentinelLabel,
                NativeAllocationLifetime.Session);
            PrewarmQueue(ref _pendingSignals, PendingSignalCapacity);
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

        public void Dispose()
        {
            Unbind();

            if (_pendingSignals.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(QuestGraphEvaluator), _pendingSignalsSentinelLabel);
                _pendingSignals.Dispose();
            }

            _pendingSignalCount = 0;
            _droppedSignalCount = 0;
            _lastPendingSignalOverflowTelemetryFrame = -1;
        }

        public void Bind()
        {
            if (_isBound)
                return;

            if (!_activeEvaluators.TryRegister(this))
            {
                ReportActiveEvaluatorRejected();
                return;
            }

            NarrativeEvents.Register(this);
            CraftingEvents.Register(this);
            InteractionEvents.Register(this);
            BiomeMatrixEvents.Register(this);
            CelestialEvents.Register(this);
            AtlasSignalEvents.Register(this);
            _isBound = true;
        }

        public void Unbind()
        {
            if (!_isBound)
                return;

            NarrativeEvents.Unregister(this);
            CraftingEvents.Unregister(this);
            InteractionEvents.Unregister(this);
            BiomeMatrixEvents.Unregister(this);
            CelestialEvents.Unregister(this);
            AtlasSignalEvents.Unregister(this);
            _activeEvaluators.Unregister(this);
            _isBound = false;

            while (_pendingSignals.IsCreated && _pendingSignals.TryDequeue(out _))
            {
            }

            _pendingSignalCount = 0;
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

        public void OnInteractionEvent(in InteractionEventPayload payload)
        {
            InteractionEventType eventType = (InteractionEventType)payload.EventType;
            if (eventType != InteractionEventType.ItemCollected &&
                eventType != InteractionEventType.ItemLost)
            {
                return;
            }

            uint itemHash = payload.ItemHashId;
            if (itemHash == 0u)
                return;

            if (eventType == InteractionEventType.ItemLost)
            {
                TryRevertCriticalItem(itemHash);
                return;
            }

            EnqueueSignal(new QuestSignalPayload
            {
                EntityHash = itemHash,
                EventType = (ushort)QuestSignalKind.ItemCollected,
                ItemId = itemHash,
                Timestamp = Time.timeAsDouble,
                NumericValue = math.max(1, payload.Quantity)
            });
        }

        private void TryRevertCriticalItem(uint itemHash)
        {
            if (_stateManager == null || itemHash == 0u)
                return;

            if (_stateManager.TryRevertCriticalItem(
                    itemHash,
                    Time.timeAsDouble,
                    out QuestRevertRequest revertRequest))
            {
                QuestEvents.TryRaiseRevertRequested(in revertRequest);
                _onResultsAvailable?.Invoke();
            }
        }

        public void OnMatrixBiomeChanged(HectonBiomeMatrixProfile profile)
        {
            if (profile == null)
                return;

            EnqueueSignal(new QuestSignalPayload
            {
                EventType = (ushort)QuestSignalKind.BiomeEntered,
                Timestamp = Time.timeAsDouble,
                NumericValue = profile.matrixIndex
            });
        }

        public void OnDepthTierChanged(int depthTier, float depthMeters)
        {
            UpdateDepth(depthMeters > 0f ? depthMeters : MapDepthTierToMeters(depthTier));
        }

        public void OnNarrativeEvent(in NarrativeEventPayload payload)
        {
            switch ((NarrativeEventType)payload.EventType)
            {
                case NarrativeEventType.DiscoveryMade:
                    EnqueueSignal(new QuestSignalPayload
                    {
                        EntityHash = payload.DiscoveryHash,
                        EventType = (ushort)QuestSignalKind.DiscoveryMade,
                        Timestamp = Time.timeAsDouble
                    });
                    return;

                case NarrativeEventType.AudioLogFound:
                    EnqueueSignal(new QuestSignalPayload
                    {
                        EntityHash = payload.DiscoveryHash,
                        EventType = (ushort)QuestSignalKind.AudioLogFound,
                        Timestamp = Time.timeAsDouble
                    });
                    return;

                case NarrativeEventType.DepthTierReached:
                    UpdateDepth(MapDepthTierToMeters(payload.DepthTier));
                    return;
            }
        }

        public void OnCraftingEvent(in CraftingEventPayload payload)
        {
            if ((CraftingEventType)payload.EventType != CraftingEventType.CraftCompleted)
                return;

            uint itemHash = payload.ResultItemHashId;
            if (itemHash == 0u)
                return;

            double timestamp = Time.timeAsDouble;
            EnqueueSignal(new QuestSignalPayload
            {
                EntityHash = itemHash,
                EventType = (ushort)QuestSignalKind.CraftCompleted,
                ItemId = itemHash,
                Timestamp = timestamp,
                NumericValue = 1f
            });

            EnqueueSignal(new QuestSignalPayload
            {
                EntityHash = itemHash,
                EventType = (ushort)QuestSignalKind.ItemCollected,
                ItemId = itemHash,
                Timestamp = timestamp,
                NumericValue = 1f
            });
        }

        private void HandleEclipseStart()
        {
            EnqueueSignal(new QuestSignalPayload
            {
                EventType = (ushort)QuestSignalKind.EclipseStarted,
                Timestamp = Time.timeAsDouble
            });
        }

        void ICelestialEventListener.OnCelestialEclipseStarted()
        {
            HandleEclipseStart();
        }

        void ICelestialEventListener.OnCelestialEclipseEnded()
        {
        }

        void ICelestialEventListener.OnCelestialSunAngleChanged(float angleDegrees)
        {
        }

        void ICelestialEventListener.OnCelestialPlanetPhaseChanged(float phase)
        {
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

        private void EnqueueSignal(in QuestSignalPayload payload)
        {
            if (!_pendingSignals.IsCreated)
                return;

            if (_pendingSignalCount >= PendingSignalCapacity)
            {
                ReportPendingSignalOverflow(payload.EventType);
                return;
            }

            _pendingSignals.Enqueue(payload);
            _pendingSignalCount++;
        }

        private bool DrainPendingSignals()
        {
            if (_stateManager == null || !_pendingSignals.IsCreated || _isDrainingSignals)
                return true;

            _isDrainingSignals = true;
            try
            {
                int scanBudget = _pendingSignalCount > 0 ? _pendingSignalCount : PendingSignalCapacity;
                while (scanBudget > 0 && !_pendingSignals.IsEmpty())
                {
                    if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                        return false;

                    if (!_pendingSignals.TryDequeue(out QuestSignalPayload payload))
                    {
                        _pendingSignalCount = 0;
                        break;
                    }

                    if (_pendingSignalCount > 0)
                        _pendingSignalCount--;
                    scanBudget--;
                    _stateManager.EvaluateSignal(payload);
                    _onResultsAvailable?.Invoke();
                }

                if (_pendingSignals.IsEmpty())
                    _pendingSignalCount = 0;
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

        private void ReportPendingSignalOverflow(ushort eventType)
        {
            _droppedSignalCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastPendingSignalOverflowTelemetryFrame == frame)
                return;

            _lastPendingSignalOverflowTelemetryFrame = frame;
            uint contextHash = _PendingSignalContextHash ^ ((uint)eventType << 24);
            GlobalTelemetryBus.PublishPerformanceWarning(
                _PendingSignalOverflowWarningHash,
                contextHash,
                _droppedSignalCount);
        }

        private static void ReportActiveEvaluatorRejected()
        {
            _activeEvaluatorRejectCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastActiveEvaluatorRejectedTelemetryFrame == frame)
                return;

            _lastActiveEvaluatorRejectedTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _ActiveEvaluatorRejectedWarningHash,
                _ActiveEvaluatorContextHash,
                _activeEvaluatorRejectCount);
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
