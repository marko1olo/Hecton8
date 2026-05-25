using Hecton.Localization;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Systems.AI;
using Hecton8.World;
using System;
using System.Runtime.InteropServices;
using TMPro;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hecton8.UI
{
    /// <summary>
    /// PDA intrusion event identifiers queued by <see cref="PDAIntrusionEvents"/>.
    /// </summary>
    public enum PDAIntrusionEventType : byte
    {
        RebootCompleted = 0
    }

    /// <summary>
    /// Blittable PDA intrusion payload flushed during dispatcher LateUpdate.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct PDAIntrusionEventPayload
    {
        [FieldOffset(0)] public uint SourceID;
        [FieldOffset(4)] public uint EventHashID;
        [FieldOffset(8)] public ushort EventType;
        [FieldOffset(10)] public ushort Reserved;
        [FieldOffset(12)] private uint _pad0;
    }

    /// <summary>
    /// Listener contract for PDA intrusion lifecycle events.
    /// </summary>
    public interface IPDAIntrusionEventListener
    {
        void OnPDAIntrusionEvent(in PDAIntrusionEventPayload payload);
    }

    /// <summary>
    /// Queue-backed PDA intrusion event lane flushed from <see cref="SystemDispatcher.LateUpdate"/>.
    /// </summary>
    public static class PDAIntrusionEvents
    {
        private const int ListenerCapacity = 8;
        private const int PendingEventCapacity = 4;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
        private const uint PDAIntrusionListenerOverflowWarningHash = 0x5049564Cu; // PIVL
        private const uint PDAIntrusionListenerContextHash = 0x50495652u; // PIVR
        private const uint PDAIntrusionListenerExceptionWarningHash = 0x50495645u; // PIVE
        private const uint PDAIntrusionListenerExceptionContextHash = 0x50495658u; // PIVX
        private static readonly uint _RebootCompletedEventHash = unchecked((uint)LocHash.Compute("PDAIntrusion.RebootCompleted"));

        private struct ListenerSlot
        {
            public IPDAIntrusionEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct PDAIntrusionListenerRegistry
        {
            private readonly ListenerSlot[] _slots;
            private int _count;

            public PDAIntrusionListenerRegistry(int capacity)
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

            public bool Contains(IPDAIntrusionEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_slots[i].Listener, listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(IPDAIntrusionEventListener listener)
            {
                if (listener == null || _count >= _slots.Length)
                    return false;

                _slots[_count++].Listener = listener;
                return true;
            }

            public bool TryUnregister(IPDAIntrusionEventListener listener)
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

            public IPDAIntrusionEventListener GetAt(int index)
            {
                return (uint)index < (uint)_count ? _slots[index].Listener : null;
            }
        }

        // COLD ALLOC: ListenerSlot[8] - PDA intrusion listeners drained by SystemDispatcher LateUpdate - owner: PDAIntrusionEvents
        private static PDAIntrusionListenerRegistry _listeners = new PDAIntrusionListenerRegistry(ListenerCapacity);
        // COLD ALLOC: ListenerSlot[8] - listener additions deferred while dispatching PDA intrusion events - owner: PDAIntrusionEvents
        private static readonly ListenerSlot[] _deferredRegisterListeners = new ListenerSlot[ListenerCapacity];
        // COLD ALLOC: ListenerSlot[8] - listener removals deferred while dispatching PDA intrusion events - owner: PDAIntrusionEvents
        private static readonly ListenerSlot[] _deferredUnregisterListeners = new ListenerSlot[ListenerCapacity];
        private static NativeQueue<PDAIntrusionEventPayload> _pendingEvents;
        private static NativeQueue<PDAIntrusionEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _droppedListenerRegistrationCount;
        private static int _listenerExceptionCount;
        private static int _lastListenerOverflowTelemetryFrame = -1;
        private static int _lastListenerExceptionTelemetryFrame = -1;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        public static int DroppedListenerRegistrationCount => _droppedListenerRegistrationCount;

        public static int ListenerExceptionCount => _listenerExceptionCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(PDAIntrusionEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(PDAIntrusionEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            Array.Clear(_deferredRegisterListeners, 0, _deferredRegisterCount);
            Array.Clear(_deferredUnregisterListeners, 0, _deferredUnregisterCount);
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _deferredRegisterCount = 0;
            _deferredUnregisterCount = 0;
            _droppedListenerRegistrationCount = 0;
            _listenerExceptionCount = 0;
            _lastListenerOverflowTelemetryFrame = -1;
            _lastListenerExceptionTelemetryFrame = -1;
            _isDispatching = false;
        }

        public static void Register(IPDAIntrusionEventListener listener)
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

        public static void Unregister(IPDAIntrusionEventListener listener)
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

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void AssertUnregistered(IPDAIntrusionEventListener listener, string ownerName)
        {
            if (listener == null || !_listeners.Contains(listener))
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.LogError("[PDAIntrusionEvents] Listener destroyed while still registered.");
#endif
        }

        internal static void RaiseRebootCompleted(uint sourceId)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return;

            PDAIntrusionEventPayload payload = new PDAIntrusionEventPayload
            {
                SourceID = sourceId,
                EventHashID = _RebootCompletedEventHash,
                EventType = (ushort)PDAIntrusionEventType.RebootCompleted,
                Reserved = 0
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

                if (!_pendingEvents.TryDequeue(out PDAIntrusionEventPayload payload))
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
                        IPDAIntrusionEventListener listener = _listeners.GetAt(i);
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

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<PDAIntrusionEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<PDAIntrusionEventPayload>[4] - deferred PDA intrusion lane flushed by SystemDispatcher LateUpdate - owner: PDAIntrusionEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(PDAIntrusionEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<PDAIntrusionEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<PDAIntrusionEventPayload>[4] - next-frame PDA intrusion lane prevents same-frame reentrant dispatch - owner: PDAIntrusionEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(PDAIntrusionEvents),
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

        private static void DrainWithoutDispatch()
        {
            if (!_pendingEvents.IsCreated)
                return;

            if (!DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
                return;

            if (_pendingEvents.IsEmpty())
                PromoteNextFrameEventsIfFrontEmpty();

            if (_pendingEventCount > 0 &&
                !DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
            {
                return;
            }

            if (_nextFrameEvents.IsCreated)
                DrainQueueWithoutDispatch(ref _nextFrameEvents, ref _nextFrameEventCount);
        }

        private static bool DrainQueueWithoutDispatch(
            ref NativeQueue<PDAIntrusionEventPayload> queue,
            ref int pendingCount)
        {
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
                !_pendingEvents.IsEmpty() ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            NativeQueue<PDAIntrusionEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }

        private static void DispatchToListener(
            IPDAIntrusionEventListener listener,
            in PDAIntrusionEventPayload payload)
        {
            try
            {
                listener.OnPDAIntrusionEvent(in payload);
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
            UnityEngine.Debug.LogException(exception);
#endif
        }

        private static void QueueDeferredRegister(IPDAIntrusionEventListener listener)
        {
            if (_listeners.Contains(listener))
            {
                CancelDeferredUnregister(listener);
                return;
            }

            if (IsDeferredRegisterPending(listener))
                return;

            if (_deferredRegisterCount >= _deferredRegisterListeners.Length)
            {
                ReportListenerRegistrationOverflow();
                return;
            }

            _deferredRegisterListeners[_deferredRegisterCount++].Listener = listener;
        }

        private static void QueueDeferredUnregister(IPDAIntrusionEventListener listener)
        {
            if (CancelDeferredRegister(listener))
                return;

            if (!_listeners.Contains(listener))
                return;

            if (IsDeferredUnregisterPending(listener))
                return;

            if (_deferredUnregisterCount >= _deferredUnregisterListeners.Length)
            {
                ReportListenerRegistrationOverflow();
                return;
            }

            _deferredUnregisterListeners[_deferredUnregisterCount++].Listener = listener;
        }

        private static bool CancelDeferredRegister(IPDAIntrusionEventListener listener)
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

        private static void CancelDeferredUnregister(IPDAIntrusionEventListener listener)
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

        private static bool IsDeferredRegisterPending(IPDAIntrusionEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(IPDAIntrusionEventListener listener)
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
                IPDAIntrusionEventListener listener = _deferredUnregisterListeners[i].Listener;
                _deferredUnregisterListeners[i].Clear();
                if (listener != null)
                    _listeners.TryUnregister(listener);
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                IPDAIntrusionEventListener listener = _deferredRegisterListeners[i].Listener;
                _deferredRegisterListeners[i].Clear();
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterCount = 0;
        }

        private static void RegisterImmediate(IPDAIntrusionEventListener listener)
        {
            if (_listeners.Contains(listener))
                return;

            if (!_listeners.TryRegister(listener))
                ReportListenerRegistrationOverflow();
        }

        private static void ReportListenerRegistrationOverflow()
        {
            _droppedListenerRegistrationCount++;
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastListenerOverflowTelemetryFrame == frame)
                return;

            _lastListenerOverflowTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                PDAIntrusionListenerOverflowWarningHash,
                PDAIntrusionListenerContextHash,
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
                PDAIntrusionListenerExceptionWarningHash,
                PDAIntrusionListenerExceptionContextHash,
                Mathf.Max(1, _listenerExceptionCount));
        }
    }

    /// <summary>
    /// Player-owned runtime owner for diegetic PDA intrusion, language hijack cadence, and manual reboot recovery.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Intrusion Manager")]
    public sealed class PDAIntrusionManager : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable, IDirectorAIEventListener, IGlobalRegistryHotSwapListener
    {
        private enum IntrusionVisualPhase : byte
        {
            English = 0,
            Arabic = 1,
            Chinese = 2,
            Glyphs = 3
        }

        private const float HullStressHackThreshold = 0.85f;
        private const float EquipmentGlitchHackThreshold = 0.75f;
        private const float LeviathanCheckInterval = 0.25f;
        private const float LeviathanHackRadius = 54f;
        private const float VisualPhaseDuration = 2f;
        private const float RebootHoldDuration = 3f;
        private const float TextDriftRescanInterval = 0.35f;
        private const float TextDriftAmplitudeMin = 1.5f;
        private const float TextDriftAmplitudeMax = 7.5f;
        private const float TextDriftFrequencyMin = 1.1f;
        private const float TextDriftFrequencyMax = 2.7f;
        private const float TextDriftInvTwoPi = 1f / (math.PI * 2f);
        private const float HiddenProgressCutoff = 0.0001f;
        private const int MaxBioformContacts = 24;
        private const int MaxDriftTargets = 96;

        [Header("── Intrusion Thresholds ──────────────────")]
        [Tooltip("Minimum director glitch intensity required before the intrusion owner treats the event as a hostile EMI strike.")]
        [SerializeField, Range(0f, 1f)] private float equipmentGlitchThreshold = EquipmentGlitchHackThreshold;

        [Tooltip("How often to scan the spatial grid for leviathan proximity while the PDA remains nominal.")]
        [SerializeField, Min(0.05f)] private float leviathanScanInterval = LeviathanCheckInterval;

        [Tooltip("Leviathan proximity radius that forces the PDA into hacked state.")]
        [SerializeField, Min(8f)] private float leviathanHackRadius = LeviathanHackRadius;

        [Tooltip("Cadence between visual language swaps during intrusion.")]
        [SerializeField, Min(0.1f)] private float visualPhaseDuration = VisualPhaseDuration;

        [Tooltip("How long the player must hold the reboot action while the PDA is open.")]
        [SerializeField, Min(0.5f)] private float rebootHoldDuration = RebootHoldDuration;

        // COLD ALLOC: SpatialQueryHit[24] — cached bioform proximity buffer for intrusion scans — owner: PDAIntrusionManager
        private readonly SpatialQueryHit[] _bioformContacts = new SpatialQueryHit[MaxBioformContacts];
        // COLD ALLOC: List<TextMeshProUGUI>[96] — reusable hacked-text scan buffer for PDA drift — owner: PDAIntrusionManager
        private readonly System.Collections.Generic.List<TextMeshProUGUI> _driftScanBuffer = new System.Collections.Generic.List<TextMeshProUGUI>(MaxDriftTargets);
        // COLD ALLOC: TextMeshProUGUI[96] — cached PDA text targets for hacked-line drift — owner: PDAIntrusionManager
        private readonly TextMeshProUGUI[] _driftTargets = new TextMeshProUGUI[MaxDriftTargets];
        // COLD ALLOC: RectTransform[96] — cached rect owners for hacked-line drift — owner: PDAIntrusionManager
        private readonly RectTransform[] _driftRects = new RectTransform[MaxDriftTargets];
        // COLD ALLOC: Vector2[96] — cached pre-hack anchored positions for text drift restore — owner: PDAIntrusionManager
        private readonly Vector2[] _driftBaseAnchoredPositions = new Vector2[MaxDriftTargets];
        // COLD ALLOC: float[96] — deterministic phase offsets for hacked-line drift — owner: PDAIntrusionManager
        private readonly float[] _driftPhaseOffsets = new float[MaxDriftTargets];

        private PlayerPDA _playerPda;
        private HectonPlayerMovement _playerMovement;
        private INativeInputManagerRuntime _inputManager;
        private InputAction _submitAction;
        private HectonMapMagicVegetationBridge _vegetationBridge;
        private GameObject _driftPanelRoot;
        private bool _serviceRegistered;
        private bool _registeredToTick;
        private bool _registeredLateFrame;
        private bool _hotSwapListenerRegistered;
        private bool _isHacked;
        private bool _visualPhaseDirty;
        private bool _restoreTextDriftRequested;
        private bool _clearVisualOverrideRequested;
        private float _leviathanScanTimer;
        private float _visualPhaseTimer;
        private float _rebootHoldTimer;
        private float _textDriftRescanTimer;
        private float _textDriftWaveTime;
        private int _driftTargetCount;
        private IntrusionVisualPhase _visualPhase;

        /// <summary>
        /// Active runtime intrusion owner attached to the current player.
        /// </summary>
        public static PDAIntrusionManager ActiveRuntimeInstance => GlobalRegistry.PDAIntrusion;

        /// <summary>
        /// True when the PDA is currently hijacked and the player must manually reboot it.
        /// </summary>
        public bool IsHacked => _isHacked;

        /// <summary>
        /// Hold progress for the manual reboot action in normalized [0..1] range.
        /// </summary>
        public float RebootProgressNormalized =>
            rebootHoldDuration > 0.001f
                ? math.saturate(_rebootHoldTimer / rebootHoldDuration)
                : 0f;

        private void Awake()
        {
            PDAIntrusionManager activeRuntime = GlobalRegistry.PDAIntrusion;
            if (activeRuntime != null && activeRuntime != this)
            {
                Destroy(this);
                return;
            }

            ResolveRuntimeOwners();
            ResolveInputActionOwner();
        }

        private void OnEnable()
        {
            TryRegisterService();
            ResolveRuntimeOwners();
            ResolveInputActionOwner();
            TryRegisterHotSwapListener();
            RegisterToTickManager();
            DirectorAIEvents.Register(this);
        }

        private void Start()
        {
            ResolveRuntimeOwners();
            ResolveInputActionOwner();
            TryRegisterHotSwapListener();
            RegisterToTickManager();
        }

        private void OnDisable()
        {
            DirectorAIEvents.Unregister(this);
            UnregisterFromTickManager();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            ClearInputActionOwner();
            ClearVisualOverride();
            RestoreTextDriftPositions();
            ResetTransientState();
        }

        private void OnDestroy()
        {
            DirectorAIEvents.Unregister(this);
            UnregisterFromTickManager();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            ClearInputActionOwner();
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            PDAIntrusionManager activeRuntime = GlobalRegistry.PDAIntrusion;
            if (activeRuntime != null && activeRuntime != this)
            {
                enabled = false;
                Destroy(this);
                return;
            }

            GlobalRegistry.RegisterPDAIntrusionRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.PDAIntrusion, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterPDAIntrusionRuntime(this);
            _serviceRegistered = false;
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            ResolveRuntimeOwners();

            if (!_isHacked)
            {
                _restoreTextDriftRequested = true;
                TickAmbientIntrusionThreat(dt);
                return;
            }

            TickVisualCadence(dt);
            TickRebootHold(dt);
        }

        public void LateFrameTick()
        {
            float dt = Time.unscaledDeltaTime;
            if (_restoreTextDriftRequested)
            {
                _restoreTextDriftRequested = false;
                RestoreTextDriftPositions();
            }

            if (_clearVisualOverrideRequested)
            {
                _clearVisualOverrideRequested = false;
                ClearVisualOverride();
            }

            if (_visualPhaseDirty)
            {
                _visualPhaseDirty = false;
                ApplyVisualPhase();
            }

            if (_isHacked)
                TickTextDrift(dt);
        }

        private void HandleEquipmentGlitchRequested(float intensity)
        {
            if (intensity < equipmentGlitchThreshold)
                return;

            TriggerHack();
        }

        void IDirectorAIEventListener.OnDirectorSpawnHordeRequested(Vector3 position)
        {
        }

        void IDirectorAIEventListener.OnDirectorEquipmentGlitchRequested(float intensity)
        {
            HandleEquipmentGlitchRequested(intensity);
        }

        void IDirectorAIEventListener.OnDirectorRareDiscoveryRequested(Vector3 position)
        {
        }

        void IDirectorAIEventListener.OnDirectorWeatherShiftRequested(float intensity)
        {
        }

        void IDirectorAIEventListener.OnDirectorMissionTriggerRequested(Vector3 position)
        {
        }

        void IDirectorAIEventListener.OnDirectorPredatorPressureChanged(bool pressureEnabled)
        {
        }

        void IDirectorAIEventListener.OnDirectorThreatSpike(Vector3 position, float intensity)
        {
            HandleEquipmentGlitchRequested(intensity);
        }

        private void TickAmbientIntrusionThreat(float dt)
        {
            _leviathanScanTimer -= dt;
            if (_leviathanScanTimer > 0f)
                return;

            _leviathanScanTimer = math.max(0.05f, leviathanScanInterval);

            AbsoluteUniversePosition originAup = ResolveOwnerAup();
            Vector3 origin = originAup.ToRuntimeFloat3();
            if (ShouldTriggerAbyssalHack(origin))
            {
                TriggerHack();
                return;
            }

            int contactCount = WorldSpatialHashGrid.CollectContactsNonAlloc(
                origin,
                math.max(8f, leviathanHackRadius),
                SpatialTargetKind.Bioform,
                _bioformContacts);

            for (int i = 0; i < contactCount; i++)
            {
                Component owner = _bioformContacts[i].Owner;
                FaunaBrain brain = owner as FaunaBrain;
                if (brain == null || brain.IsDead)
                    continue;

                FaunaSpeciesProfile speciesProfile = brain.SpeciesProfile;
                if (speciesProfile == null || !speciesProfile.isLeviathan)
                    continue;

                TriggerHack();
                return;
            }
        }

        private bool ShouldTriggerAbyssalHack(Vector3 origin)
        {
            if (_playerMovement != null && _playerMovement.CurrentHullStress01 > HullStressHackThreshold)
                return true;

            return IsInsideDeadZone(origin);
        }

        private bool IsInsideDeadZone(Vector3 origin)
        {
            HectonMapMagicVegetationBridge bridge = _vegetationBridge;
            if (bridge == null)
                return false;

            HectonMapMagicVegetationBridge.VegetationDensitySample densitySample = bridge.GetVegetationDensity(origin);
            return densitySample.BiomeLayer == HectonMapMagicVegetationBridge.VegetationBiomeLayer.DeadZone;
        }

        private AbsoluteUniversePosition ResolveOwnerAup()
        {
            HectonPlayerMovement playerMovement = _playerMovement;
            return playerMovement != null ? playerMovement.CurrentAup : default;
        }

        private void TickVisualCadence(float dt)
        {
            _visualPhaseTimer -= dt;
            if (_visualPhaseTimer > 0f)
                return;

            _visualPhaseTimer = math.max(0.1f, visualPhaseDuration);
            _visualPhase = NextVisualPhase(_visualPhase);
            _visualPhaseDirty = true;
        }

        private void TickRebootHold(float dt)
        {
            if (!CanAcceptRebootHold())
            {
                if (_rebootHoldTimer > HiddenProgressCutoff)
                    _rebootHoldTimer = 0f;
                return;
            }

            if (!IsRebootActionHeld())
            {
                if (_rebootHoldTimer > HiddenProgressCutoff)
                    _rebootHoldTimer = 0f;
                return;
            }

            _rebootHoldTimer += dt;
            if (_rebootHoldTimer < rebootHoldDuration)
                return;

            CompleteReboot();
        }

        private void TickTextDrift(float dt)
        {
            if (_playerPda == null || !PlayerPDA.IsOpen)
            {
                RestoreTextDriftPositions();
                return;
            }

            GameObject panelRoot = _playerPda.PanelRoot;
            if (panelRoot == null || !panelRoot.activeInHierarchy)
            {
                RestoreTextDriftPositions();
                return;
            }

            _textDriftRescanTimer -= dt;
            if (!ReferenceEquals(_driftPanelRoot, panelRoot) || _driftTargetCount == 0 || _textDriftRescanTimer <= 0f)
                RebuildTextDriftTargets(panelRoot);

            if (_driftTargetCount == 0)
                return;

            _textDriftWaveTime += dt;
            float glyphScale = _visualPhase == IntrusionVisualPhase.Glyphs ? 1.22f : 1f;
            for (int i = 0; i < _driftTargetCount; i++)
            {
                RectTransform rect = _driftRects[i];
                if (rect == null)
                    continue;

                float normalizedIndex = _driftTargetCount > 1
                    ? (float)i / (_driftTargetCount - 1)
                    : 0f;
                float amplitude = math.lerp(TextDriftAmplitudeMin, TextDriftAmplitudeMax, normalizedIndex) * glyphScale;
                float frequency = math.lerp(TextDriftFrequencyMin, TextDriftFrequencyMax, 1f - normalizedIndex);
                float offsetX = EvaluateCheapDriftWaveSigned((_textDriftWaveTime * frequency) + _driftPhaseOffsets[i]) * amplitude;
                Vector2 basePosition = _driftBaseAnchoredPositions[i];
                rect.anchoredPosition = new Vector2(basePosition.x + offsetX, basePosition.y);
            }
        }

        private static float EvaluateCheapDriftWaveSigned(float phaseRadians)
        {
            float phase01 = math.frac((phaseRadians * TextDriftInvTwoPi) + 0.25f);
            float triangle = 1f - math.abs(phase01 * 2f - 1f);
            return (triangle * 2f) - 1f;
        }

        private void RebuildTextDriftTargets(GameObject panelRoot)
        {
            RestoreTextDriftPositions();
            _driftPanelRoot = panelRoot;
            _driftTargetCount = 0;
            _textDriftRescanTimer = math.max(0.1f, TextDriftRescanInterval);
            _driftScanBuffer.Clear();
            panelRoot.GetComponentsInChildren(true, _driftScanBuffer);

            int candidateCount = _driftScanBuffer.Count;
            for (int i = 0; i < candidateCount && _driftTargetCount < MaxDriftTargets; i++)
            {
                TextMeshProUGUI text = _driftScanBuffer[i];
                if (text == null || !text.enabled)
                    continue;

                RectTransform rect = text.rectTransform;
                if (rect == null)
                    continue;

                int slot = _driftTargetCount;
                _driftTargets[slot] = text;
                _driftRects[slot] = rect;
                _driftBaseAnchoredPositions[slot] = rect.anchoredPosition;
                _driftPhaseOffsets[slot] = (slot * 0.73f) + (text.fontSize * 0.013f);
                _driftTargetCount++;
            }
        }

        private void RestoreTextDriftPositions()
        {
            if (_driftTargetCount <= 0)
                return;

            for (int i = 0; i < _driftTargetCount; i++)
            {
                RectTransform rect = _driftRects[i];
                if (rect != null)
                    rect.anchoredPosition = _driftBaseAnchoredPositions[i];

                _driftTargets[i] = null;
                _driftRects[i] = null;
                _driftPhaseOffsets[i] = 0f;
            }

            _driftTargetCount = 0;
            _driftPanelRoot = null;
        }

        private void TriggerHack()
        {
            if (_isHacked)
                return;

            _isHacked = true;
            _rebootHoldTimer = 0f;
            _visualPhase = IntrusionVisualPhase.English;
            _visualPhaseTimer = math.max(0.1f, visualPhaseDuration);
            _visualPhaseDirty = true;
        }

        private void CompleteReboot()
        {
            _restoreTextDriftRequested = true;
            _clearVisualOverrideRequested = true;
            ResetTransientState();
            PDAIntrusionEvents.RaiseRebootCompleted(unchecked((uint)EntityId.ToULong(GetEntityId())));
        }

        private void ApplyVisualPhase()
        {
            ILocalizationTransientOverrideSink overrideSink = Hecton8.Core.GlobalRegistry.LocalizationTransientOverrideSink;
            if (overrideSink == null)
                return;

            switch (_visualPhase)
            {
                case IntrusionVisualPhase.Arabic:
                    overrideSink.SetTransientLanguageOverride((ushort)GameLanguage.Arabic);
                    break;

                case IntrusionVisualPhase.Chinese:
                    overrideSink.SetTransientLanguageOverride((ushort)GameLanguage.ChineseSimplified);
                    break;

                case IntrusionVisualPhase.Glyphs:
                    overrideSink.SetTransientLanguageOverride((ushort)GameLanguage.ChineseSimplified, enableGlyphMode: true);
                    break;

                default:
                    overrideSink.SetTransientLanguageOverride((ushort)GameLanguage.English);
                    break;
            }
        }

        private void ClearVisualOverride()
        {
            Hecton8.Core.GlobalRegistry.LocalizationTransientOverrideSink?.ClearTransientLanguageOverride();
        }

        private void ResetTransientState()
        {
            _isHacked = false;
            _leviathanScanTimer = 0f;
            _visualPhaseTimer = 0f;
            _rebootHoldTimer = 0f;
            _textDriftRescanTimer = 0f;
            _textDriftWaveTime = 0f;
            _visualPhase = IntrusionVisualPhase.English;
            _visualPhaseDirty = false;
        }

        private bool CanAcceptRebootHold()
        {
            return _isHacked &&
                   _playerPda != null &&
                   PlayerPDA.IsOpen &&
                   _submitAction != null;
        }

        private bool IsRebootActionHeld()
        {
            return _submitAction != null && _submitAction.IsPressed();
        }

        private void ResolveRuntimeOwners()
        {
            if (_playerPda == null)
            {
                if (!TryGetComponent(out _playerPda))
                    _playerPda = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<PlayerPDA>(transform);
            }

            if (_playerMovement == null)
                TryGetComponent(out _playerMovement);

            if (_vegetationBridge == null)
                _vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
        }

        private void ResolveInputActionOwner()
        {
            ResolveInputActionOwner(ResolveNativeInputManager());
        }

        private static INativeInputManagerRuntime ResolveNativeInputManager()
        {
            return GlobalRegistry.NativeInputRuntime;
        }

        private void ResolveInputActionOwner(INativeInputManagerRuntime inputManager)
        {
            if (ReferenceEquals(_inputManager, inputManager) &&
                (_inputManager == null || _submitAction != null))
                return;

            _inputManager = inputManager;
            _submitAction = _inputManager != null
                ? _inputManager.GetAction("Submit", "UI")
                : null;
        }

        private void ClearInputActionOwner()
        {
            _inputManager = null;
            _submitAction = null;
            _rebootHoldTimer = 0f;
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Input)
                return;

            ClearInputActionOwner();

            if (!isActiveAndEnabled)
                return;

            ResolveInputActionOwner(ResolveNativeInputManager());
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void RegisterToTickManager()
        {
            if ((_registeredToTick && _registeredLateFrame) || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredToTick)
                _registeredToTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void UnregisterFromTickManager()
        {
            if (_registeredToTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _registeredToTick = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }
        }

        private static IntrusionVisualPhase NextVisualPhase(IntrusionVisualPhase current)
        {
            switch (current)
            {
                case IntrusionVisualPhase.English:
                    return IntrusionVisualPhase.Arabic;

                case IntrusionVisualPhase.Arabic:
                    return IntrusionVisualPhase.Chinese;

                case IntrusionVisualPhase.Chinese:
                    return IntrusionVisualPhase.Glyphs;

                default:
                    return IntrusionVisualPhase.English;
            }
        }
    }
}
