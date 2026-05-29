using System;
using System.Runtime.InteropServices;
using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Environment
{
    public interface IBiomeMatrixEventListener
    {
        void OnMatrixBiomeChanged(HectonBiomeMatrixProfile profile);
        void OnDepthTierChanged(int depthTier, float depthMeters);
    }

    public static class BiomeMatrixEvents
    {
        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct BiomeMatrixEventPayload
        {
            [FieldOffset(0)] public byte EventType;
            [FieldOffset(1)] private byte _pad0;
            [FieldOffset(2)] private ushort _pad1;
            [FieldOffset(4)] public int ProfileSlot;
            [FieldOffset(8)] public int DepthTier;
            [FieldOffset(12)] public float DepthMeters;
        }

        private const byte MatrixBiomeChangedEventType = 1;
        private const byte DepthTierChangedEventType = 2;
        private const int ListenerCapacity = 16;
        private const int PendingEventCapacity = 32;
        private const int MatrixProfileCacheCapacity = 128;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
        private const uint ListenerRejectedWarningHash = 0x424D524Au; // BMRJ
        private const uint ListenerExceptionWarningHash = 0x424D4558u; // BMEX
        private const uint ListenerContextHash = 0x424D4C53u; // BMLS
        private const uint QueueOverflowWarningHash = 0x424D4551u; // BMEQ
        private const uint QueueContextHash = 0x424D4550u; // BMEP
        private const uint ProfileSlotOverflowWarningHash = 0x424D5053u; // BMPS
        private const uint ProfileSlotContextHash = 0x424D5043u; // BMPC

        private struct ListenerSlot
        {
            public IBiomeMatrixEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        // COLD ALLOC: ListenerSlot[16] - live biome matrix listeners drained by SystemDispatcher - owner: BiomeMatrixEvents
        private static readonly ListenerSlot[] _listeners = new ListenerSlot[ListenerCapacity];
        // COLD ALLOC: ListenerSlot[16] - listener additions deferred while dispatching biome matrix events - owner: BiomeMatrixEvents
        private static readonly ListenerSlot[] _deferredRegisterListeners = new ListenerSlot[ListenerCapacity];
        // COLD ALLOC: ListenerSlot[16] - listener removals deferred while dispatching biome matrix events - owner: BiomeMatrixEvents
        private static readonly ListenerSlot[] _deferredUnregisterListeners = new ListenerSlot[ListenerCapacity];
        private static readonly HectonBiomeMatrixProfile[] _profilesBySlot = new HectonBiomeMatrixProfile[MatrixProfileCacheCapacity]; // COLD ALLOC: HectonBiomeMatrixProfile[128] - stable profile lookup for deferred biome matrix payloads - owner: BiomeMatrixEvents
        private static NativeQueue<BiomeMatrixEventPayload> _pendingEvents;
        private static NativeQueue<BiomeMatrixEventPayload> _nextFrameEvents;
        private static int _listenerCount;
        private static int _profileSlotCount;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _droppedEventCount;
        private static int _droppedProfileSlotCount;
        private static int _droppedListenerRegistrationCount;
        private static int _listenerExceptionCount;
        private static int _lastQueueOverflowTelemetryFrame = -1;
        private static int _lastProfileSlotOverflowTelemetryFrame = -1;
        private static int _lastListenerRejectedTelemetryFrame = -1;
        private static int _lastListenerExceptionTelemetryFrame = -1;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;
        public static int DroppedEventCount => _droppedEventCount;
        public static int DroppedProfileSlotCount => _droppedProfileSlotCount;
        public static int DroppedListenerRegistrationCount => _droppedListenerRegistrationCount;
        public static int ListenerExceptionCount => _listenerExceptionCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(BiomeMatrixEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(BiomeMatrixEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            for (int i = 0; i < _listenerCount; i++)
                _listeners[i].Clear();
            for (int i = 0; i < _deferredRegisterCount; i++)
                _deferredRegisterListeners[i].Clear();
            for (int i = 0; i < _deferredUnregisterCount; i++)
                _deferredUnregisterListeners[i].Clear();
            Array.Clear(_profilesBySlot, 0, _profileSlotCount);
            _listenerCount = 0;
            _profileSlotCount = 0;
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _deferredRegisterCount = 0;
            _deferredUnregisterCount = 0;
            _droppedEventCount = 0;
            _droppedProfileSlotCount = 0;
            _droppedListenerRegistrationCount = 0;
            _listenerExceptionCount = 0;
            _lastQueueOverflowTelemetryFrame = -1;
            _lastProfileSlotOverflowTelemetryFrame = -1;
            _lastListenerRejectedTelemetryFrame = -1;
            _lastListenerExceptionTelemetryFrame = -1;
            _isDispatching = false;
        }

        public static void Register(IBiomeMatrixEventListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatching)
            {
                QueueDeferredRegister(listener);
                return;
            }

            RegisterImmediate(listener);
        }

        public static void Unregister(IBiomeMatrixEventListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatching)
            {
                QueueDeferredUnregister(listener);
                return;
            }

            TryUnregisterImmediate(listener);
        }

        public static bool TryRaiseMatrixBiomeChanged(HectonBiomeMatrixProfile profile)
        {
            if (_listenerCount <= 0)
                return false;

            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportQueueOverflow(MatrixBiomeChangedEventType);
                return false;
            }

            int profileSlot = ResolveProfileSlot(profile);
            if (profile != null && profileSlot < 0)
            {
                ReportProfileSlotOverflow();
                return false;
            }

            Enqueue(new BiomeMatrixEventPayload
            {
                EventType = MatrixBiomeChangedEventType,
                ProfileSlot = profileSlot
            });
            return true;
        }

        [Obsolete("Biome matrix producers must use TryRaiseMatrixBiomeChanged and handle bounded enqueue failure.", true)]
        public static void RaiseMatrixBiomeChanged(HectonBiomeMatrixProfile profile)
        {
            TryRaiseMatrixBiomeChanged(profile);
        }

        public static bool TryRaiseDepthTierChanged(int depthTier, float depthMeters)
        {
            if (_listenerCount <= 0)
                return false;

            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportQueueOverflow(DepthTierChangedEventType);
                return false;
            }

            Enqueue(new BiomeMatrixEventPayload
            {
                EventType = DepthTierChangedEventType,
                DepthTier = depthTier,
                DepthMeters = depthMeters
            });
            return true;
        }

        [Obsolete("Biome matrix producers must use TryRaiseDepthTierChanged and handle bounded enqueue failure.", true)]
        public static void RaiseDepthTierChanged(int depthTier, float depthMeters)
        {
            TryRaiseDepthTierChanged(depthTier, depthMeters);
        }

        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated)
                return;

            PromoteNextFrameEventsIfFrontEmpty();
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out BiomeMatrixEventPayload payload))
                {
                    _pendingEventCount = 0;
                    break;
                }

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                _isDispatching = true;
                try
                {
                    Dispatch(in payload);
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

        private static void Enqueue(in BiomeMatrixEventPayload payload)
        {
            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(payload);
                _nextFrameEventCount++;
                return;
            }

            _pendingEvents.Enqueue(payload);
            _pendingEventCount++;
        }

        private static void Dispatch(in BiomeMatrixEventPayload payload)
        {
            int listenerCount = _listenerCount;
            if (payload.EventType == MatrixBiomeChangedEventType)
            {
                HectonBiomeMatrixProfile profile = null;
                if ((uint)payload.ProfileSlot < (uint)_profileSlotCount)
                    profile = _profilesBySlot[payload.ProfileSlot];

                for (int i = listenerCount - 1; i >= 0; i--)
                {
                    IBiomeMatrixEventListener listener = _listeners[i].Listener;
                    if (listener == null || IsDeferredUnregisterPending(listener))
                        continue;

                    DispatchMatrixBiomeChanged(listener, profile);
                }

                return;
            }

            if (payload.EventType == DepthTierChangedEventType)
            {
                for (int i = listenerCount - 1; i >= 0; i--)
                {
                    IBiomeMatrixEventListener listener = _listeners[i].Listener;
                    if (listener == null || IsDeferredUnregisterPending(listener))
                        continue;

                    DispatchDepthTierChanged(listener, payload.DepthTier, payload.DepthMeters);
                }
            }
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<BiomeMatrixEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<BiomeMatrixEventPayload>[32] - deferred biome matrix event lane flushed by SystemDispatcher - owner: BiomeMatrixEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(BiomeMatrixEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<BiomeMatrixEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<BiomeMatrixEventPayload>[32] - next-frame biome matrix event lane prevents same-frame reentrant dispatch - owner: BiomeMatrixEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(BiomeMatrixEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
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

        private static void PromoteNextFrameEventsIfFrontEmpty()
        {
            if (!_pendingEvents.IsCreated ||
                !_nextFrameEvents.IsCreated ||
                !_pendingEvents.IsEmpty() ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            NativeQueue<BiomeMatrixEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }

        private static int ResolveProfileSlot(HectonBiomeMatrixProfile profile)
        {
            if (profile == null)
                return -1;

            for (int i = 0; i < _profileSlotCount; i++)
            {
                if (ReferenceEquals(_profilesBySlot[i], profile))
                    return i;
            }

            if (_profileSlotCount >= _profilesBySlot.Length)
                return -1;

            int slot = _profileSlotCount++;
            _profilesBySlot[slot] = profile;
            return slot;
        }

        private static void DispatchMatrixBiomeChanged(IBiomeMatrixEventListener listener, HectonBiomeMatrixProfile profile)
        {
            try
            {
                listener.OnMatrixBiomeChanged(profile);
            }
            catch (Exception exception)
            {
                ReportListenerDispatchException();
                LogListenerDispatchException(exception);
            }
        }

        private static void DispatchDepthTierChanged(IBiomeMatrixEventListener listener, int depthTier, float depthMeters)
        {
            try
            {
                listener.OnDepthTierChanged(depthTier, depthMeters);
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

        private static void QueueDeferredRegister(IBiomeMatrixEventListener listener)
        {
            if (ContainsImmediate(listener))
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

        private static void QueueDeferredUnregister(IBiomeMatrixEventListener listener)
        {
            if (CancelDeferredRegister(listener))
                return;

            if (!ContainsImmediate(listener) || IsDeferredUnregisterPending(listener))
                return;

            if (_deferredUnregisterCount >= ListenerCapacity)
            {
                ReportListenerRegistrationRejected();
                return;
            }

            _deferredUnregisterListeners[_deferredUnregisterCount++].Listener = listener;
        }

        private static bool CancelDeferredRegister(IBiomeMatrixEventListener listener)
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

        private static void CancelDeferredUnregister(IBiomeMatrixEventListener listener)
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

        private static bool IsDeferredRegisterPending(IBiomeMatrixEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(IBiomeMatrixEventListener listener)
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
                IBiomeMatrixEventListener listener = _deferredUnregisterListeners[i].Listener;
                _deferredUnregisterListeners[i].Clear();
                if (listener != null)
                    TryUnregisterImmediate(listener);
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                IBiomeMatrixEventListener listener = _deferredRegisterListeners[i].Listener;
                _deferredRegisterListeners[i].Clear();
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterCount = 0;
        }

        private static void RegisterImmediate(IBiomeMatrixEventListener listener)
        {
            if (ContainsImmediate(listener))
                return;

            if (_listenerCount >= ListenerCapacity)
            {
                ReportListenerRegistrationRejected();
                return;
            }

            _listeners[_listenerCount++].Listener = listener;
        }

        private static bool TryUnregisterImmediate(IBiomeMatrixEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (!ReferenceEquals(_listeners[i].Listener, listener))
                    continue;

                int lastIndex = --_listenerCount;
                if (i != lastIndex)
                    _listeners[i].Listener = _listeners[lastIndex].Listener;

                _listeners[lastIndex].Clear();
                return true;
            }

            return false;
        }

        private static bool ContainsImmediate(IBiomeMatrixEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static void ReportQueueOverflow(byte eventType)
        {
            _droppedEventCount++;
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastQueueOverflowTelemetryFrame == frame)
                return;

            _lastQueueOverflowTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                QueueOverflowWarningHash,
                QueueContextHash ^ ((uint)eventType << 24),
                Mathf.Max(1, _droppedEventCount));
        }

        private static void ReportProfileSlotOverflow()
        {
            _droppedProfileSlotCount++;
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastProfileSlotOverflowTelemetryFrame == frame)
                return;

            _lastProfileSlotOverflowTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                ProfileSlotOverflowWarningHash,
                ProfileSlotContextHash,
                Mathf.Max(1, _droppedProfileSlotCount));
        }

        private static void ReportListenerRegistrationRejected()
        {
            _droppedListenerRegistrationCount++;
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
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
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastListenerExceptionTelemetryFrame == frame)
                return;

            _lastListenerExceptionTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                ListenerExceptionWarningHash,
                ListenerContextHash,
                Mathf.Max(1, _listenerExceptionCount));
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4035)]
    [ExecuteAlways]
    public sealed class BiomeMatrixDirector : MonoBehaviour, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        private const string MissingProfileLabel = "No biome profile";
        private const string NorthCardinalRegionLabel = "North";
        private const string SouthCardinalRegionLabel = "South";
        private const string EastCardinalRegionLabel = "East";
        private const string WestCardinalRegionLabel = "West";
        private const string NoneClusterFocusLabel = "None";
        private const string FertileGrowthClusterFocusLabel = "FertileGrowth";
        private const string BiologicalNestClusterFocusLabel = "BiologicalNest";
        private const string ResourcePocketClusterFocusLabel = "ResourcePocket";
        private const string ShelterPocketClusterFocusLabel = "ShelterPocket";
        private const string HazardPocketClusterFocusLabel = "HazardPocket";
        private const string DebrisFieldClusterFocusLabel = "DebrisField";
        private const string RockCoverClusterFocusLabel = "RockCover";
        private const string NoneStructureFocusLabel = "None";
        private const string NaturalLandmarkStructureFocusLabel = "NaturalLandmark";
        private const string TechFragmentStructureFocusLabel = "TechFragment";
        private const string CaveReadStructureFocusLabel = "CaveRead";
        private const string BiologicalSilhouetteStructureFocusLabel = "BiologicalSilhouette";
        private const string NoneFaunaMoodLabel = "None";
        private const string CalmFaunaMoodLabel = "Calm";
        private const string LivelyFaunaMoodLabel = "Lively";
        private const string MixedFaunaMoodLabel = "Mixed";
        private const string HostileFaunaMoodLabel = "Hostile";
        private const int TectonicDustBiomeIdA = 7;
        private const int TectonicDustBiomeIdB = 9;
        private const int TectonicDustBiomeIdC = 11;
        private const float BiomeMatrixClockMaxSeconds = 16777215f;

        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private HectonBiomeMatrixCatalog matrixCatalog;

        [Header("World Framing")]
        [SerializeField] private float surfaceOffsetMeters = 0f;
        [SerializeField] private Vector3 worldOrigin = Vector3.zero;
        [SerializeField] private float regionDeadZone = 24f;

        [Header("Transition VFX")]
        [SerializeField, Range(1f, 30f)] private float seismicDustCooldownSeconds = 8f;

        [Header("Transition Hysteresis")]
        [SerializeField, Min(0f)] private float biomeTransitionHysteresisMeters = 15f;

        [Header("Diagnostics")]
        [SerializeField] private int _debugTier = 1;
        [SerializeField] private string _debugRegion = "North";
        [SerializeField] private string _debugBiomeName = "None";
        [SerializeField] private int _debugMatrixIndex = -1;
        [SerializeField] private bool _debugPlaceholder;
        [SerializeField] private float _debugSurfaceLevelY;
#pragma warning disable 0414
        [SerializeField] private string _debugDepthSource = "SurfaceOffset";
#pragma warning restore 0414
#pragma warning disable 0414
        [SerializeField] private string _debugEvaluationSource = "Player";
#pragma warning restore 0414
        [SerializeField] private string _debugFamilyId = "None";
        [SerializeField] private string _debugFamilyLabel = "None";
        [SerializeField] private string _debugResolutionMode = "Exact";
        [SerializeField] private string _debugAtmosphereMood = "None";
        [SerializeField] private string _debugPrimaryResourceTheme = "None";
        [SerializeField] private string _debugNavigationStyle = "None";
        [SerializeField] private string _debugAtmosphereProfile = "None";
        [SerializeField] private string _debugFaunaFamily = "None";
        [SerializeField] private string _debugThreatStyle = "None";
        [SerializeField] private string _debugRecommendedLoadout = "None";
        [SerializeField] private string _debugResourcePlan = "None";
        [SerializeField] private string _debugResourceChannels = "None";
        [SerializeField] private string _debugEarlyFarmReason = "None";
        [SerializeField] private string _debugLateReturnReason = "None";
        [SerializeField] private string _debugExtractionStyle = "None";
        [SerializeField] private string _debugPocketResource = "None";
        [SerializeField] private string _debugNodeResource = "None";
        [SerializeField] private string _debugSafePocketResource = "None";
        [SerializeField] private string _debugRareObjectiveResource = "None";
        [SerializeField] private int _debugLoosePickupWeight;
        [SerializeField] private int _debugNodeExtractionWeight;
        [SerializeField] private int _debugSalvageRecoveryWeight;
        [SerializeField] private int _debugCommonResourcePull;
        [SerializeField] private int _debugUncommonResourcePull;
        [SerializeField] private int _debugRareResourcePull;
        [SerializeField] private string _debugLandmarkPlan = "None";
        [SerializeField] private string _debugDominantLandmarkRole = "None";
        [SerializeField] private string _debugRouteUse = "None";
        [SerializeField] private string _debugEmotionalRead = "None";
        [SerializeField] private string _debugSpatialPattern = "None";
        [SerializeField] private string _debugResourcePocketPattern = "None";
        [SerializeField] private string _debugNodeClusterPattern = "None";
        [SerializeField] private string _debugSafePocketPattern = "None";
        [SerializeField] private string _debugRouteAnchorPattern = "None";
        [SerializeField] private string _debugRareObjectivePattern = "None";
        [SerializeField] private string _debugExplorationLoop = "None";
        [SerializeField] private string _debugWhyPlayerComesHere = "None";
        [SerializeField] private int _debugRouteClarity;
        [SerializeField] private int _debugSafePocketFrequency;
        [SerializeField] private int _debugRareRewardPull;
        [SerializeField] private int _debugEncounterPressure;
        [SerializeField] private int _debugHazardPressure;
        [SerializeField] private string _debugVisitPurpose = "None";
        [SerializeField] private string _debugCommonRewardHook = "None";
        [SerializeField] private string _debugRareRewardHook = "None";
        [SerializeField] private string _debugLandmarkIdentity = "None";
        [SerializeField] private string _debugSafePocketIdentity = "None";
        [SerializeField] private string _debugRiskSummary = "None";
        [SerializeField] private string _debugExtractionFocus = "None";
        [SerializeField] private string _debugLandmarkGuidance = "None";
        [SerializeField] private int _debugLoosePickupBias;
        [SerializeField] private int _debugNodeExtractionBias;
        [SerializeField] private int _debugSalvageBias;
        [SerializeField] private int _debugCommonResourceBias;
        [SerializeField] private int _debugUncommonResourceBias;
        [SerializeField] private int _debugRareResourceBias;
        [SerializeField] private int _debugRoutePressure;
        [SerializeField] private int _debugLandmarkStrengthValue;
        [SerializeField] private int _debugRewardPullValue;
        [SerializeField] private int _debugSurvivalPressure;
        [SerializeField] private string _debugPrimaryClusterFocus = "None";
        [SerializeField] private string _debugSecondaryClusterFocus = "None";
        [SerializeField] private string _debugPrimaryStructureFocus = "None";
        [SerializeField] private string _debugSecondaryStructureFocus = "None";
        [SerializeField] private string _debugFaunaMoodValue = "None";
        [SerializeField] private int _debugLastSeismicDustBiomeId = -1;

        private bool _registeredToTickManager;
        private bool _hotSwapRegistered;
        private HectonBiomeMatrixProfile _currentProfile;
        private int _currentDepthTier = 1;
        private float _currentDepthMeters;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private HectonPlayerMovement _playerMovement;
        private IFluidDecalPresentationSink _resolvedFluidDecals;
        private IFluidSurfaceCurrentReadModel _resolvedFluidEngine;
        private ITerrainProvider _resolvedTerrainProvider;
        private IAtmosphereReadModel _resolvedAtmosphereReadModel;
        private bool _editorPreviewDirty = true;
        private Transform _editorLastEvaluationTransform;
        private Vector3 _editorLastEvaluationPosition;
        private float _editorLastSurfaceLevelY = float.NaN;
        private float _lastSeismicDustTime = -999f;
        private HectonBiomeMatrixProfile _pendingHysteresisProfile;
        private AbsoluteUniversePosition _pendingHysteresisAup;
        private bool _hasPendingHysteresisProfile;

        internal static BiomeMatrixDirector ActiveRuntimeInstance { get; private set; }

        public HectonBiomeMatrixProfile CurrentProfile => _currentProfile;
        public HectonBiomeFamilyProfile CurrentFamilyProfile => _currentProfile != null ? _currentProfile.familyProfile : null;
        public HectonBiomeMatrixCatalog MatrixCatalog => matrixCatalog;
        public bool HasCatalog => matrixCatalog != null && matrixCatalog.Count > 0;
        public int CurrentDepthTier => _currentDepthTier;
        public float CurrentDepthMeters => _currentDepthMeters;

        private void Awake()
        {
            ActiveRuntimeInstance = this;
            if (Application.isPlaying)
                GlobalRegistry.RegisterBiomeMatrixRuntime(this);
            CacheRuntimeDependencies();
            TryRegisterHotSwapListener();
            ResolveReferences();
            EvaluateMatrix(forcePublish: true);
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (EditorApplication.isCompiling || !Application.isPlaying)
                return;
#endif

            CacheRuntimeDependencies();
            TryRegister();
            TryRegisterHotSwapListener();
#if UNITY_EDITOR
            _editorPreviewDirty = true;
            EditorApplication.update -= EditorUpdate;
            EditorApplication.update += EditorUpdate;
#endif
        }

        private void Start()
        {
            CacheRuntimeDependencies();
            ResolveReferences();
            TryRegister();

            EvaluateMatrix(forcePublish: true);
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            ClearRuntimeDependencies();
#if UNITY_EDITOR
            EditorApplication.update -= EditorUpdate;
#endif
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            ClearRuntimeDependencies();
#if UNITY_EDITOR
            EditorApplication.update -= EditorUpdate;
#endif

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
            if (GlobalRegistry.BiomeMatrix == this)
                GlobalRegistry.UnregisterBiomeMatrixRuntime(this);
        }

#if UNITY_EDITOR
        private void EditorUpdate()
        {
            if (EditorApplication.isCompiling || !Application.isPlaying)
            {
                EditorApplication.update -= EditorUpdate;
                return;
            }

            if (Application.isPlaying)
                return;

            if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive)
                return;

            if (!ShouldEvaluateEditorPreview())
                return;

            EvaluateMatrix(forcePublish: false);
            CacheEditorPreviewState();
            _editorPreviewDirty = false;
        }

        private bool ShouldEvaluateEditorPreview()
        {
            if (_editorPreviewDirty)
                return true;

            Transform evaluationTransform = ResolveEvaluationTransform();
            if (!ReferenceEquals(_editorLastEvaluationTransform, evaluationTransform))
                return true;

            if (evaluationTransform == null || !HasCatalog)
                return false;

            Vector3 evaluationPosition = evaluationTransform.position;
            if ((evaluationPosition - _editorLastEvaluationPosition).sqrMagnitude > 0.0001f)
                return true;

            float surfaceLevelY = ResolveSurfaceLevelY();
            return !Mathf.Approximately(surfaceLevelY, _editorLastSurfaceLevelY);
        }

        private void CacheEditorPreviewState()
        {
            Transform evaluationTransform = ResolveEvaluationTransform();
            _editorLastEvaluationTransform = evaluationTransform;
            _editorLastEvaluationPosition = evaluationTransform != null
                ? evaluationTransform.position
                : Vector3.zero;
            _editorLastSurfaceLevelY = evaluationTransform != null && HasCatalog
                ? ResolveSurfaceLevelY()
                : float.NaN;
        }
#endif

        private void TryRegister()
        {
            if (!Application.isPlaying || _registeredToTickManager)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredToTickManager = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToTickManager = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    RebindPlayerRuntimeContext(previousService, currentService);
                    break;
                case GlobalRegistryServiceSlot.AbyssalFluidDecalRuntime:
                    _resolvedFluidDecals = currentService as IFluidDecalPresentationSink;
                    break;
                case GlobalRegistryServiceSlot.FluidRuntime:
                    _resolvedFluidEngine = currentService as IFluidSurfaceCurrentReadModel;
                    break;
                case GlobalRegistryServiceSlot.TerrainProviderRuntime:
                    _resolvedTerrainProvider = currentService as ITerrainProvider;
                    break;
                case GlobalRegistryServiceSlot.AtmosphereRuntime:
                    _resolvedAtmosphereReadModel = currentService as IAtmosphereReadModel;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    if (currentService == null)
                    {
                        _registeredToTickManager = false;
                        break;
                    }

                    if (isActiveAndEnabled)
                    {
                        TryUnregister();
                        TryRegister();
                    }
                    break;
            }
        }

        private void CacheRuntimeDependencies()
        {
            _playerRuntimeContext ??= Hecton8.Core.GlobalRegistry.Player;
            _resolvedFluidDecals ??= GlobalRegistry.FluidDecalPresentation;
            _resolvedFluidEngine ??= GlobalRegistry.FluidSurfaceCurrent;
            if (_resolvedTerrainProvider == null)
            {
                MapMagicBridge mapMagicBridge = null;
                if (WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge))
                    _resolvedTerrainProvider = mapMagicBridge;
            }

            _resolvedAtmosphereReadModel ??= Hecton8.Core.GlobalRegistry.AtmosphereReadModel;
            ApplyPlayerRuntimeContext();
        }

        private void ClearRuntimeDependencies()
        {
            _playerRuntimeContext = null;
            _playerMovement = null;
            _resolvedFluidDecals = null;
            _resolvedFluidEngine = null;
            _resolvedTerrainProvider = null;
            _resolvedAtmosphereReadModel = null;
        }

        private void RebindPlayerRuntimeContext(object previousService, object currentService)
        {
            IPlayerRuntimeContext previousContext = previousService as IPlayerRuntimeContext;
            if (previousContext != null &&
                previousContext.PlayerTransform != null &&
                ReferenceEquals(playerTransform, previousContext.PlayerTransform))
            {
                playerTransform = null;
            }

            _playerRuntimeContext = currentService as IPlayerRuntimeContext;
            ApplyPlayerRuntimeContext();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
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

        public void SlowTick()
        {
            EvaluateMatrix(forcePublish: false);
        }

        /// <summary>
        /// Forces immediate biome matrix evaluation for the current player position.
        /// </summary>
        public void ForceRefresh()
        {
            ResolveReferences();
            EvaluateMatrix(forcePublish: true);
        }

        public void SetMatrixCatalog(HectonBiomeMatrixCatalog catalog)
        {
            matrixCatalog = catalog;
#if UNITY_EDITOR
            _editorPreviewDirty = true;
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (EditorApplication.isCompiling || !Application.isPlaying)
                return;

            _editorPreviewDirty = true;
        }
#endif

        private void EvaluateMatrix(bool forcePublish)
        {
            Transform evaluationTransform = ResolveEvaluationTransform();

            if (evaluationTransform == null || !HasCatalog)
            {
                bool hadProfile = _currentProfile != null;
                _currentProfile = null;
                _currentDepthMeters = 0f;
                _currentDepthTier = 1;
                ClearPendingBiomeHysteresis();
                _debugResolutionMode = evaluationTransform == null ? "Missing evaluation transform" : "Missing catalog";
                if (hadProfile && Application.isPlaying)
                    BiomeMatrixEvents.TryRaiseMatrixBiomeChanged(null);
                UpdateDiagnostics(null, 1, HectonBiomeMatrixProfile.CardinalRegion.North);
                return;
            }

            float surfaceLevelY = ResolveSurfaceLevelY();
            float depth = Mathf.Max(0f, surfaceLevelY - evaluationTransform.position.y);
            int tier = ResolveDepthTier(depth);
            HectonBiomeMatrixProfile.CardinalRegion region = ResolveRegion(evaluationTransform.position);
            bool usedFallback;
            HectonBiomeMatrixProfile next = ResolveMatrixProfile(tier, region, out usedFallback);
            bool depthTierChanged = forcePublish || tier != _currentDepthTier;

            _currentDepthMeters = depth;
            _currentDepthTier = tier;
            _debugSurfaceLevelY = surfaceLevelY;
            _debugResolutionMode = next == null ? MissingProfileLabel : usedFallback ? "Fallback" : "Exact";

            if (depthTierChanged && Application.isPlaying)
                BiomeMatrixEvents.TryRaiseDepthTierChanged(_currentDepthTier, _currentDepthMeters);

            if (!ShouldCommitBiomeProfile(next, evaluationTransform.position, forcePublish))
            {
                _debugResolutionMode = next == null ? MissingProfileLabel : "HysteresisPending";
                UpdateDiagnostics(_currentProfile, tier, region);
                return;
            }

            if (forcePublish || next != _currentProfile)
            {
                bool changedProfile = next != _currentProfile;
                _currentProfile = next;
                if (Application.isPlaying)
                {
                    if (changedProfile)
                        TryEmitSeismicDustForBiome(next, evaluationTransform.position);

                    uint biomeTelemetryHash = next != null
                        ? GlobalTelemetryBus.ComputeContextHash(next.biomeName)
                        : 0u;
                    GlobalTelemetryBus.PublishBiomeVisited(biomeTelemetryHash, tier, depth);
                    BiomeMatrixEvents.TryRaiseMatrixBiomeChanged(_currentProfile);
                }
            }

            UpdateDiagnostics(_currentProfile, tier, region);
        }

        private bool ShouldCommitBiomeProfile(HectonBiomeMatrixProfile next, Vector3 evaluationPosition, bool forcePublish)
        {
            if (forcePublish ||
                next == _currentProfile ||
                _currentProfile == null ||
                biomeTransitionHysteresisMeters <= 0f)
            {
                ClearPendingBiomeHysteresis();
                return true;
            }

            if (!TryResolveAupFromRuntimeOrigin(evaluationPosition, out AbsoluteUniversePosition currentAup))
                return false;

            if (!_hasPendingHysteresisProfile || _pendingHysteresisProfile != next)
            {
                _pendingHysteresisProfile = next;
                _pendingHysteresisAup = currentAup;
                _hasPendingHysteresisProfile = true;
                return false;
            }

            double requiredDistanceSq = (double)biomeTransitionHysteresisMeters * biomeTransitionHysteresisMeters;
            if (AbsoluteUniversePosition.DistanceSq(in currentAup, in _pendingHysteresisAup) < requiredDistanceSq)
                return false;

            ClearPendingBiomeHysteresis();
            return true;
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            float3 localRuntime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(localRuntime)))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(localRuntime.x, localRuntime.y, localRuntime.z));
            return positionAup.IsFinite();
        }

        private void ClearPendingBiomeHysteresis()
        {
            _pendingHysteresisProfile = null;
            _pendingHysteresisAup = default;
            _hasPendingHysteresisProfile = false;
        }

        private void TryEmitSeismicDustForBiome(HectonBiomeMatrixProfile profile, Vector3 evaluationPosition)
        {
            if (profile == null || !ShouldEmitSeismicDust(profile))
                return;

            float now = ResolveBiomeMatrixClockSeconds();
            if (now - _lastSeismicDustTime < Mathf.Max(1f, seismicDustCooldownSeconds))
                return;

            IFluidDecalPresentationSink fluidDecals = _resolvedFluidDecals;
            if (fluidDecals == null)
                return;

            if (_resolvedTerrainProvider == null ||
                !_resolvedTerrainProvider.TryGetHeight(evaluationPosition.x, evaluationPosition.z, out float seafloorHeight))
            {
                return;
            }

            Vector3 dustPosition = new Vector3(
                evaluationPosition.x,
                seafloorHeight + Mathf.Max(0f, profile.seismicDustSeafloorOffsetMeters),
                evaluationPosition.z);
            fluidDecals.RegisterSeismicDust(dustPosition, profile.seismicDustRadiusScale);
            _lastSeismicDustTime = now;
            _debugLastSeismicDustBiomeId = profile.matrixIndex;
        }

        private static float ResolveBiomeMatrixClockSeconds()
        {
            SystemDispatcher dispatcher = SystemDispatcher.ActiveRuntimeInstance;
            if (dispatcher == null)
                return 0f;

            double timeSeconds = dispatcher.DilatedTimeSeconds;
            if (!math.isfinite(timeSeconds) || timeSeconds <= 0d)
                return 0f;

            return (float)math.min(BiomeMatrixClockMaxSeconds, timeSeconds);
        }

        private static bool ShouldEmitSeismicDust(HectonBiomeMatrixProfile profile)
        {
            if (profile == null)
                return false;

            return profile.emitsSeismicDustOnEntry ||
                   profile.matrixIndex == TectonicDustBiomeIdA ||
                   profile.matrixIndex == TectonicDustBiomeIdB ||
                   profile.matrixIndex == TectonicDustBiomeIdC;
        }

        private void ResolveReferences()
        {
            if (playerTransform == null)
            {
                ApplyPlayerRuntimeContext();
#if UNITY_EDITOR
                if (playerTransform == null && !Application.isPlaying)
                    WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);
#endif
            }

            if (playerTransform != null && _playerMovement == null && !Application.isPlaying)
                playerTransform.TryGetComponent(out _playerMovement);
        }

        private void ApplyPlayerRuntimeContext()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext == null)
                return;

            if (playerContext.PlayerTransform != null)
                playerTransform = playerContext.PlayerTransform;

            if (playerContext.PlayerMovement != null)
                _playerMovement = playerContext.PlayerMovement;
        }

        private Transform ResolveEvaluationTransform()
        {
            if (Application.isPlaying)
            {
                _debugEvaluationSource = "Player";
                return playerTransform;
            }

#if UNITY_EDITOR
            SceneView sceneView = SceneView.lastActiveSceneView;
            Camera sceneViewCamera = sceneView != null ? sceneView.camera : null;
            if (sceneViewCamera != null)
            {
                _debugEvaluationSource = "SceneView";
                return sceneViewCamera.transform;
            }
#endif

            _debugEvaluationSource = "Player";
            return playerTransform;
        }

        private HectonBiomeMatrixProfile ResolveMatrixProfile(int tier, HectonBiomeMatrixProfile.CardinalRegion region, out bool usedFallback)
        {
            usedFallback = false;
            if (matrixCatalog == null)
                return null;

            HectonBiomeMatrixProfile exact = matrixCatalog.Resolve(tier, region);
            if (exact != null)
                return exact;

            HectonBiomeMatrixProfile[] profiles = matrixCatalog.Profiles;
            if (profiles == null || profiles.Length == 0)
                return null;

            HectonBiomeMatrixProfile bestProfile = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < profiles.Length; i++)
            {
                HectonBiomeMatrixProfile profile = profiles[i];
                if (profile == null)
                    continue;

                int tierDelta = Mathf.Abs(profile.depthTier - tier);
                int score = 0;
                score -= tierDelta * 20;

                if (profile.depthTier == tier)
                    score += 1200;
                else if (tierDelta <= 1)
                    score += 200;

                if (profile.region == region)
                    score += 150;

                if (!profile.isPlaceholder)
                    score += 100;

                if (!string.IsNullOrWhiteSpace(profile.biomeName))
                    score += 10;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestProfile = profile;
                }
            }

            usedFallback = bestProfile != null;
            return bestProfile;
        }

        private float ResolveSurfaceLevelY()
        {
            if (_playerMovement != null)
            {
                _debugDepthSource = "PlayerMovement";
                return _playerMovement.CurrentWaterSurfaceY;
            }

            if (_resolvedFluidEngine != null)
            {
                _debugDepthSource = "FluidEngine";
                return _resolvedFluidEngine.WaterLevel;
            }

            if (_resolvedTerrainProvider != null)
            {
                _debugDepthSource = "TerrainProvider";
                return _resolvedTerrainProvider.WaterSurfaceLevel;
            }

            if (_resolvedAtmosphereReadModel != null)
            {
                _debugDepthSource = "AtmosphereReadModel";
                return _resolvedAtmosphereReadModel.SeaLevelY;
            }

            _debugDepthSource = "SurfaceOffset";
            return surfaceOffsetMeters;
        }

        private int ResolveDepthTier(float depth)
        {
            if (depth <= 0f)
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

            float clamped = Mathf.Clamp(depth, 3500f, 14000f);
            float normalized = (clamped - 3500f) / 10500f;
            int tier = 10 + Mathf.FloorToInt(normalized * 17f);
            return Mathf.Clamp(tier, 10, 26);
        }

        private HectonBiomeMatrixProfile.CardinalRegion ResolveRegion(Vector3 position)
        {
            Vector3 delta = position - worldOrigin;
            delta.y = 0f;

            if (Mathf.Abs(delta.x) <= regionDeadZone && Mathf.Abs(delta.z) <= regionDeadZone)
                return HectonBiomeMatrixProfile.CardinalRegion.North;

            if (Mathf.Abs(delta.z) >= Mathf.Abs(delta.x))
                return delta.z >= 0f ? HectonBiomeMatrixProfile.CardinalRegion.North : HectonBiomeMatrixProfile.CardinalRegion.South;

            return delta.x >= 0f ? HectonBiomeMatrixProfile.CardinalRegion.East : HectonBiomeMatrixProfile.CardinalRegion.West;
        }

        private void UpdateDiagnostics(
            HectonBiomeMatrixProfile profile,
            int tier,
            HectonBiomeMatrixProfile.CardinalRegion region)
        {
            _debugTier = tier;
            _debugRegion = ResolveCardinalRegionLabel(region);
            _debugBiomeName = profile != null ? profile.biomeName : "None";
            _debugMatrixIndex = profile != null ? profile.matrixIndex : -1;
            _debugPlaceholder = profile != null && profile.isPlaceholder;
            _debugFamilyId = profile != null ? profile.familyId : "None";
            _debugFamilyLabel = profile != null && profile.familyProfile != null ? profile.familyProfile.RuntimeFamilyLabel : "None";
            _debugAtmosphereMood = profile != null && profile.familyProfile != null ? profile.familyProfile.RuntimeAtmosphereMood : "None";
            _debugPrimaryResourceTheme = profile != null && profile.familyProfile != null ? profile.familyProfile.RuntimePrimaryResourceTheme : "None";
            _debugNavigationStyle = profile != null && profile.familyProfile != null ? profile.familyProfile.RuntimeNavigationStyle : "None";
            _debugAtmosphereProfile = profile != null && profile.familyProfile != null && profile.familyProfile.atmosphereProfile != null ? profile.familyProfile.atmosphereProfile.name : "None";
            _debugFaunaFamily = profile != null && profile.familyProfile != null && profile.familyProfile.faunaFamilyProfile != null ? profile.familyProfile.faunaFamilyProfile.familyLabel : "None";
            _debugThreatStyle = profile != null && profile.familyProfile != null && profile.familyProfile.faunaFamilyProfile != null ? profile.familyProfile.faunaFamilyProfile.threatStyle : "None";
            _debugRecommendedLoadout = profile != null && profile.familyProfile != null && profile.familyProfile.recommendedLoadoutPreset != null ? profile.familyProfile.recommendedLoadoutPreset.presetName : "None";
            _debugResourcePlan = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.RuntimeProfileLabel : "None";
            _debugResourceChannels = profile != null && profile.familyProfile != null && profile.familyProfile.resourceChannelProfile != null ? profile.familyProfile.resourceChannelProfile.profileLabel : "None";
            _debugEarlyFarmReason = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.RuntimeEarlyReasonToFarm : "None";
            _debugLateReturnReason = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.RuntimeLateReasonToReturn : "None";
            _debugExtractionStyle = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.RuntimeExtractionStyle : "None";
            _debugPocketResource = GetItemLabel(profile != null && profile.familyProfile != null && profile.familyProfile.resourceChannelProfile != null ? profile.familyProfile.resourceChannelProfile.resourcePocketItem : null);
            _debugNodeResource = GetItemLabel(profile != null && profile.familyProfile != null && profile.familyProfile.resourceChannelProfile != null ? profile.familyProfile.resourceChannelProfile.nodeClusterItem : null);
            _debugSafePocketResource = GetItemLabel(profile != null && profile.familyProfile != null && profile.familyProfile.resourceChannelProfile != null ? profile.familyProfile.resourceChannelProfile.safePocketItem : null);
            _debugRareObjectiveResource = GetItemLabel(profile != null && profile.familyProfile != null && profile.familyProfile.resourceChannelProfile != null ? profile.familyProfile.resourceChannelProfile.rareObjectiveRewardItem : null);
            _debugLoosePickupWeight = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.RuntimeLoosePickupWeight : 0;
            _debugNodeExtractionWeight = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.RuntimeNodeExtractionWeight : 0;
            _debugSalvageRecoveryWeight = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.RuntimeSalvageRecoveryWeight : 0;
            _debugCommonResourcePull = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.RuntimeCommonResourcePull : 0;
            _debugUncommonResourcePull = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.RuntimeUncommonResourcePull : 0;
            _debugRareResourcePull = profile != null && profile.familyProfile != null && profile.familyProfile.resourcePlanProfile != null ? profile.familyProfile.resourcePlanProfile.RuntimeRareResourcePull : 0;
            _debugLandmarkPlan = profile != null && profile.familyProfile != null && profile.familyProfile.landmarkPlanProfile != null ? profile.familyProfile.landmarkPlanProfile.profileLabel : "None";
            _debugDominantLandmarkRole = profile != null && profile.familyProfile != null && profile.familyProfile.landmarkPlanProfile != null ? profile.familyProfile.landmarkPlanProfile.dominantLandmarkRole : "None";
            _debugRouteUse = profile != null && profile.familyProfile != null && profile.familyProfile.landmarkPlanProfile != null ? profile.familyProfile.landmarkPlanProfile.routeUse : "None";
            _debugEmotionalRead = profile != null && profile.familyProfile != null && profile.familyProfile.landmarkPlanProfile != null ? profile.familyProfile.landmarkPlanProfile.emotionalRead : "None";
            _debugSpatialPattern = profile != null && profile.familyProfile != null && profile.familyProfile.spatialPatternProfile != null ? profile.familyProfile.spatialPatternProfile.profileLabel : "None";
            _debugResourcePocketPattern = profile != null && profile.familyProfile != null && profile.familyProfile.spatialPatternProfile != null ? profile.familyProfile.spatialPatternProfile.resourcePocketPattern : "None";
            _debugNodeClusterPattern = profile != null && profile.familyProfile != null && profile.familyProfile.spatialPatternProfile != null ? profile.familyProfile.spatialPatternProfile.nodeClusterPattern : "None";
            _debugSafePocketPattern = profile != null && profile.familyProfile != null && profile.familyProfile.spatialPatternProfile != null ? profile.familyProfile.spatialPatternProfile.safePocketPattern : "None";
            _debugRouteAnchorPattern = profile != null && profile.familyProfile != null && profile.familyProfile.spatialPatternProfile != null ? profile.familyProfile.spatialPatternProfile.routeAnchorPattern : "None";
            _debugRareObjectivePattern = profile != null && profile.familyProfile != null && profile.familyProfile.spatialPatternProfile != null ? profile.familyProfile.spatialPatternProfile.rareObjectivePattern : "None";
            _debugExplorationLoop = profile != null && profile.familyProfile != null && profile.familyProfile.spatialPatternProfile != null ? profile.familyProfile.spatialPatternProfile.explorationLoop : "None";
            _debugWhyPlayerComesHere = profile != null && profile.familyProfile != null && profile.familyProfile.playProfile != null ? profile.familyProfile.playProfile.RuntimeWhyPlayerComesHere : "None";
            _debugRouteClarity = profile != null && profile.familyProfile != null && profile.familyProfile.playProfile != null ? profile.familyProfile.playProfile.RuntimeRouteClarity : 0;
            _debugSafePocketFrequency = profile != null && profile.familyProfile != null && profile.familyProfile.playProfile != null ? profile.familyProfile.playProfile.RuntimeSafePocketFrequency : 0;
            _debugRareRewardPull = profile != null && profile.familyProfile != null && profile.familyProfile.playProfile != null ? profile.familyProfile.playProfile.RuntimeRareRewardPull : 0;
            _debugEncounterPressure = profile != null && profile.familyProfile != null && profile.familyProfile.playProfile != null ? profile.familyProfile.playProfile.RuntimeEncounterPressure : 0;
            _debugHazardPressure = profile != null && profile.familyProfile != null && profile.familyProfile.playProfile != null ? profile.familyProfile.playProfile.RuntimeHazardPressure : 0;
            _debugVisitPurpose = profile != null ? profile.visitPurpose : "None";
            _debugCommonRewardHook = profile != null ? profile.commonRewardHook : "None";
            _debugRareRewardHook = profile != null ? profile.rareRewardHook : "None";
            _debugLandmarkIdentity = profile != null ? profile.landmarkIdentity : "None";
            _debugSafePocketIdentity = profile != null ? profile.safePocketIdentity : "None";
            _debugRiskSummary = profile != null ? profile.riskSummary : "None";
            _debugExtractionFocus = profile != null ? profile.extractionFocus : "None";
            _debugLandmarkGuidance = profile != null ? profile.landmarkGuidance : "None";
            _debugLoosePickupBias = profile != null ? profile.loosePickupBias : 0;
            _debugNodeExtractionBias = profile != null ? profile.nodeExtractionBias : 0;
            _debugSalvageBias = profile != null ? profile.salvageBias : 0;
            _debugCommonResourceBias = profile != null ? profile.commonResourceBias : 0;
            _debugUncommonResourceBias = profile != null ? profile.uncommonResourceBias : 0;
            _debugRareResourceBias = profile != null ? profile.rareResourceBias : 0;
            _debugRoutePressure = profile != null ? profile.routePressure : 0;
            _debugLandmarkStrengthValue = profile != null ? profile.landmarkStrength : 0;
            _debugRewardPullValue = profile != null ? profile.rewardPull : 0;
            _debugSurvivalPressure = profile != null ? profile.survivalPressure : 0;
            _debugPrimaryClusterFocus = profile != null ? ResolveClusterFocusLabel(profile.primaryClusterFocus) : "None";
            _debugSecondaryClusterFocus = profile != null ? ResolveClusterFocusLabel(profile.secondaryClusterFocus) : "None";
            _debugPrimaryStructureFocus = profile != null ? ResolveStructureFocusLabel(profile.primaryStructureFocus) : "None";
            _debugSecondaryStructureFocus = profile != null ? ResolveStructureFocusLabel(profile.secondaryStructureFocus) : "None";
            _debugFaunaMoodValue = profile != null ? ResolveFaunaMoodLabel(profile.faunaMood) : "None";
        }

        private static string GetItemLabel(Hecton8.Items.ItemData item)
        {
            if (item == null)
                return "None";

            return string.IsNullOrWhiteSpace(item.itemName) ? item.name : item.itemName;
        }

        private static string ResolveCardinalRegionLabel(HectonBiomeMatrixProfile.CardinalRegion region)
        {
            switch (region)
            {
                case HectonBiomeMatrixProfile.CardinalRegion.South:
                    return SouthCardinalRegionLabel;
                case HectonBiomeMatrixProfile.CardinalRegion.East:
                    return EastCardinalRegionLabel;
                case HectonBiomeMatrixProfile.CardinalRegion.West:
                    return WestCardinalRegionLabel;
                default:
                    return NorthCardinalRegionLabel;
            }
        }

        private static string ResolveClusterFocusLabel(WorldProceduralClusterFocus focus)
        {
            switch (focus)
            {
                case WorldProceduralClusterFocus.FertileGrowth:
                    return FertileGrowthClusterFocusLabel;
                case WorldProceduralClusterFocus.BiologicalNest:
                    return BiologicalNestClusterFocusLabel;
                case WorldProceduralClusterFocus.ResourcePocket:
                    return ResourcePocketClusterFocusLabel;
                case WorldProceduralClusterFocus.ShelterPocket:
                    return ShelterPocketClusterFocusLabel;
                case WorldProceduralClusterFocus.HazardPocket:
                    return HazardPocketClusterFocusLabel;
                case WorldProceduralClusterFocus.DebrisField:
                    return DebrisFieldClusterFocusLabel;
                case WorldProceduralClusterFocus.RockCover:
                    return RockCoverClusterFocusLabel;
                default:
                    return NoneClusterFocusLabel;
            }
        }

        private static string ResolveStructureFocusLabel(WorldProceduralStructureFocus focus)
        {
            switch (focus)
            {
                case WorldProceduralStructureFocus.NaturalLandmark:
                    return NaturalLandmarkStructureFocusLabel;
                case WorldProceduralStructureFocus.TechFragment:
                    return TechFragmentStructureFocusLabel;
                case WorldProceduralStructureFocus.CaveRead:
                    return CaveReadStructureFocusLabel;
                case WorldProceduralStructureFocus.BiologicalSilhouette:
                    return BiologicalSilhouetteStructureFocusLabel;
                default:
                    return NoneStructureFocusLabel;
            }
        }

        private static string ResolveFaunaMoodLabel(WorldProceduralFaunaMood mood)
        {
            switch (mood)
            {
                case WorldProceduralFaunaMood.Calm:
                    return CalmFaunaMoodLabel;
                case WorldProceduralFaunaMood.Lively:
                    return LivelyFaunaMoodLabel;
                case WorldProceduralFaunaMood.Mixed:
                    return MixedFaunaMoodLabel;
                case WorldProceduralFaunaMood.Hostile:
                    return HostileFaunaMoodLabel;
                default:
                    return NoneFaunaMoodLabel;
            }
        }
    }
}
