using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts;
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
        private const uint PDAIntrusionListenerOverflowWarningHash = 0x5049564Cu; // PIVL
        private const uint PDAIntrusionListenerContextHash = 0x50495652u; // PIVR
        private const uint PDAIntrusionListenerExceptionWarningHash = 0x50495645u; // PIVE
        private const uint PDAIntrusionListenerExceptionContextHash = 0x50495658u; // PIVX
        private const uint PDAIntrusionEventOverflowWarningHash = 0x50495651u; // PIVQ
        private const uint PDAIntrusionEventContextHash = 0x50495650u; // PIVP
        private static readonly uint _RebootCompletedEventHash = unchecked((uint)LocHash.Compute("PDAIntrusion.RebootCompleted"));

        private struct PDAIntrusionListenerRegistry
        {
            private System.Collections.Generic.HashSet<IPDAIntrusionEventListener> _set;
            private System.Collections.Generic.HashSet<IPDAIntrusionEventListener> Set
            {
                get
                {
                    if (_set == null)
                        _set = new System.Collections.Generic.HashSet<IPDAIntrusionEventListener>();
                    return _set;
                }
            }

            public int Count => _set?.Count ?? 0;

            public void Clear()
            {
                _set?.Clear();
            }

            public bool Contains(IPDAIntrusionEventListener listener)
            {
                return _set != null && _set.Contains(listener);
            }

            public bool TryRegister(IPDAIntrusionEventListener listener)
            {
                if (listener == null || Set.Count >= ListenerCapacity)
                    return false;

                return Set.Add(listener);
            }

            public bool TryUnregister(IPDAIntrusionEventListener listener)
            {
                if (listener == null || _set == null)
                    return false;

                return _set.Remove(listener);
            }

            public System.Collections.Generic.HashSet<IPDAIntrusionEventListener>.Enumerator GetEnumerator()
            {
                return Set.GetEnumerator();
            }
        }

        private static PDAIntrusionListenerRegistry _listeners;
        private static PDAIntrusionListenerRegistry _deferredRegisterListeners;
        private static PDAIntrusionListenerRegistry _deferredUnregisterListeners;
        // Fixed inline slots: PDAIntrusionEventPayload[4] - deferred lane flushed by SystemDispatcher LateUpdate - owner: PDAIntrusionEvents
        private static FixedUiEventQueue<PDAIntrusionEventPayload> _pendingEvents;
        // Fixed inline slots: PDAIntrusionEventPayload[4] - next-frame lane prevents same-frame reentrant dispatch - owner: PDAIntrusionEvents
        private static FixedUiEventQueue<PDAIntrusionEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _droppedEventCount;
        private static int _droppedListenerRegistrationCount;
        private static int _listenerExceptionCount;
        private static int _lastEventOverflowTelemetryFrame = -1;
        private static int _lastListenerOverflowTelemetryFrame = -1;
        private static int _lastListenerExceptionTelemetryFrame = -1;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        public static int DroppedEventCount => _droppedEventCount;

        public static int DroppedListenerRegistrationCount => _droppedListenerRegistrationCount;

        public static int ListenerExceptionCount => _listenerExceptionCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _pendingEvents.Clear();
            _nextFrameEvents.Clear();
            _listeners.Clear();
            _deferredRegisterListeners.Clear();
            _deferredUnregisterListeners.Clear();
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _droppedEventCount = 0;
            _droppedListenerRegistrationCount = 0;
            _listenerExceptionCount = 0;
            _lastEventOverflowTelemetryFrame = -1;
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
            Hecton8.Core.H8Debug.LogError("[PDAIntrusionEvents] Listener destroyed while still registered.");
#endif
        }

        internal static void RaiseRebootCompleted(uint sourceId)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportEventQueueOverflow();
                return;
            }

            PDAIntrusionEventPayload payload = default;
            payload.SourceID = sourceId;
            payload.EventHashID = _RebootCompletedEventHash;
            payload.EventType = (ushort)PDAIntrusionEventType.RebootCompleted;
            payload.Reserved = 0;

            if (_isDispatching)
            {
                if (!_nextFrameEvents.Enqueue(in payload))
                {
                    ReportEventQueueOverflow();
                    return;
                }

                _nextFrameEventCount++;
                return;
            }

            if (!_pendingEvents.Enqueue(in payload))
            {
                ReportEventQueueOverflow();
                return;
            }

            _pendingEventCount++;
        }

        public static void FlushPending()
        {
            if (_listeners.Count <= 0)
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

                _isDispatching = true;
                try
                {
                    foreach (var listener in _listeners)
                    {
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
                _pendingEvents.Configure(PendingEventCapacity);

            if (!_nextFrameEvents.IsCreated)
                _nextFrameEvents.Configure(PendingEventCapacity);
        }

        private static void DrainWithoutDispatch()
        {
            if (!DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
                return;

            if (_pendingEvents.IsEmpty())
                PromoteNextFrameEventsIfFrontEmpty();

            if (_pendingEventCount > 0 &&
                !DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
            {
                return;
            }

            DrainQueueWithoutDispatch(ref _nextFrameEvents, ref _nextFrameEventCount);
        }

        private static bool DrainQueueWithoutDispatch(
            ref FixedUiEventQueue<PDAIntrusionEventPayload> queue,
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
            if (!_pendingEvents.IsEmpty() ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            FixedUiEventQueue<PDAIntrusionEventPayload> swap = _pendingEvents;
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
            catch (ObjectDisposedException exception)
            {
                ReportListenerDispatchException();
                LogListenerDispatchException(exception);
            }
            catch (InvalidOperationException exception)
            {
                ReportListenerDispatchException();
                LogListenerDispatchException(exception);
            }
            catch (ArgumentException exception)
            {
                ReportListenerDispatchException();
                LogListenerDispatchException(exception);
            }
            catch (NotSupportedException exception)
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

        private static void QueueDeferredRegister(IPDAIntrusionEventListener listener)
        {
            if (_listeners.Contains(listener))
            {
                CancelDeferredUnregister(listener);
                return;
            }

            if (IsDeferredRegisterPending(listener))
                return;

            if (!_deferredRegisterListeners.TryRegister(listener))
            {
                ReportListenerRegistrationOverflow();
                return;
            }
        }

        private static void QueueDeferredUnregister(IPDAIntrusionEventListener listener)
        {
            if (CancelDeferredRegister(listener))
                return;

            if (!_listeners.Contains(listener))
                return;

            if (IsDeferredUnregisterPending(listener))
                return;

            if (!_deferredUnregisterListeners.TryRegister(listener))
            {
                ReportListenerRegistrationOverflow();
            }
        }

        private static bool CancelDeferredRegister(IPDAIntrusionEventListener listener)
        {
            return _deferredRegisterListeners.TryUnregister(listener);
        }

        private static void CancelDeferredUnregister(IPDAIntrusionEventListener listener)
        {
            _deferredUnregisterListeners.TryUnregister(listener);
        }

        private static bool IsDeferredRegisterPending(IPDAIntrusionEventListener listener)
        {
            return _deferredRegisterListeners.Contains(listener);
        }

        private static bool IsDeferredUnregisterPending(IPDAIntrusionEventListener listener)
        {
            return _deferredUnregisterListeners.Contains(listener);
        }

        private static void ApplyDeferredListenerMutations()
        {
            foreach (var listener in _deferredUnregisterListeners)
            {
                if (listener != null)
                    _listeners.TryUnregister(listener);
            }

            _deferredUnregisterListeners.Clear();

            foreach (var listener in _deferredRegisterListeners)
            {
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterListeners.Clear();
        }

        private static void RegisterImmediate(IPDAIntrusionEventListener listener)
        {
            if (_listeners.Contains(listener))
                return;

            if (!_listeners.TryRegister(listener))
                ReportListenerRegistrationOverflow();
        }

        private static void ReportEventQueueOverflow()
        {
            _droppedEventCount++;
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastEventOverflowTelemetryFrame == frame)
                return;

            _lastEventOverflowTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                PDAIntrusionEventOverflowWarningHash,
                PDAIntrusionEventContextHash,
                Mathf.Max(1, _droppedEventCount));
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
    public sealed class PDAIntrusionManager : MonoBehaviour, ILateFrameTickable, IDirectorAIEventListener, IGlobalRegistryHotSwapListener
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
        private const float RuntimeOwnerRetryIntervalSeconds = 0.5f;
        private const int MaxBioformContacts = 24;
        private const int MaxDriftTargets = 96;

        [Header("-- Intrusion Thresholds ------------------")]
        [Tooltip("Minimum director glitch intensity required before the intrusion owner treats the event as a hostile EMI strike.")]
        [SerializeField, Range(0f, 1f)] private float equipmentGlitchThreshold = EquipmentGlitchHackThreshold;

        [Tooltip("Hull stress threshold above which the PDA is treated as compromised.")]
        [SerializeField, Range(0f, 1f)] private float hullStressThreshold = HullStressHackThreshold;

        [Tooltip("How often to scan the spatial grid for leviathan proximity while the PDA remains nominal.")]
        [SerializeField, Min(0.05f)] private float leviathanScanInterval = LeviathanCheckInterval;

        [Tooltip("Leviathan proximity radius that forces the PDA into hacked state.")]
        [SerializeField, Min(8f)] private float leviathanHackRadius = LeviathanHackRadius;

        [Tooltip("Cadence between visual language swaps during intrusion.")]
        [SerializeField, Min(0.1f)] private float visualPhaseDuration = VisualPhaseDuration;

        [Tooltip("How long the player must hold the reboot action while the PDA is open.")]
        [SerializeField, Min(0.5f)] private float rebootHoldDuration = RebootHoldDuration;

        // COLD ALLOC: SpatialQueryHit[24] - cached bioform proximity buffer for intrusion scans - owner: PDAIntrusionManager
        private readonly SpatialQueryHit[] _bioformContacts = new SpatialQueryHit[MaxBioformContacts];
                private struct TextDriftState
        {
            public TextMeshProUGUI Target;
            public RectTransform Rect;
            public Vector3 BaseAnchoredPosition;
            public float PhaseOffset;
            public float AppliedOffset;
        }

        // COLD ALLOC: TextDriftState[96] - unified text drift state - owner: PDAIntrusionManager
        private readonly TextDriftState[] _driftStates = new TextDriftState[MaxDriftTargets];
        // COLD ALLOC: RectTransform[96] - stack for UI traversal - owner: PDAIntrusionManager
        private readonly RectTransform[] _driftTraversalStack = new RectTransform[MaxDriftTargets];

        private PlayerPDA _playerPda;
        private HectonPlayerMovement _playerMovement;
        private INativeInputManagerRuntime _inputManager;
        private ILocalizationTransientOverrideSink _transientOverrideSink;
        private InputAction _submitAction;
        private HectonMapMagicVegetationBridge _vegetationBridge;
        private GameObject _driftPanelRoot;
        private bool _serviceRegistered;
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
        private float _runtimeOwnerResolveRetryTimer;
        private int _driftTargetCount;
        private IntrusionVisualPhase _visualPhase;

        /// <summary>
        /// Active runtime intrusion owner attached to the current player.
        /// </summary>
        private static PDAIntrusionManager s_activeRuntimeInstance;

        public static PDAIntrusionManager ActiveRuntimeInstance => s_activeRuntimeInstance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeOwnerState()
        {
            s_activeRuntimeInstance = null;
        }

        /// <summary>
        /// True when the PDA is currently hijacked and the player must manually reboot it.
        /// </summary>
        public bool IsHacked => _isHacked;

        /// <summary>
        /// Hold progress for the manual reboot action in normalized [0..1] range.
        /// </summary>
        public float RebootProgressNormalized
        {
            get
            {
                float safeDuration = ResolveRebootHoldDurationSeconds(rebootHoldDuration);
                return safeDuration > 0.001f
                    ? math.saturate(SanitizeNonNegativeSeconds(_rebootHoldTimer) / safeDuration)
                    : 0f;
            }
        }

        private void Awake()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            ResolveRuntimeOwners();
            BindInputActionOwnerCold();
            BindLocalizationOverrideSinkCold();
            RebuildTextDriftTargetsCold();
        }

        private void OnEnable()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            TryRegisterService();
            ResolveRuntimeOwners();
            BindInputActionOwnerCold();
            BindLocalizationOverrideSinkCold();
            RebuildTextDriftTargetsCold();
            TryRegisterHotSwapListener();
            RegisterToTickManager();
            DirectorAIEvents.Register(this);
        }

        private void Start()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            ResolveRuntimeOwners();
            BindInputActionOwnerCold();
            BindLocalizationOverrideSinkCold();
            RebuildTextDriftTargetsCold();
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

            if (TryAbortForUsableExistingRuntime())
                return;

            GlobalRegistry.RegisterPDAIntrusionRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.PDAIntrusion, this);
            if (_serviceRegistered)
                s_activeRuntimeInstance = this;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterPDAIntrusionRuntime(this);
            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;
            _serviceRegistered = false;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            PDAIntrusionManager registered = GlobalRegistry.PDAIntrusion;
            if (!ReferenceEquals(registered, null) && !ReferenceEquals(registered, this))
            {
                if (IsPDAIntrusionRuntimeUsable(registered))
                {
                    s_activeRuntimeInstance = registered;
                    enabled = false;
                    Destroy(this);
                    return true;
                }

                GlobalRegistry.UnregisterPDAIntrusionRuntime(registered);
                if (ReferenceEquals(s_activeRuntimeInstance, registered))
                    s_activeRuntimeInstance = null;
            }

            PDAIntrusionManager active = s_activeRuntimeInstance;
            if (ReferenceEquals(active, null) || ReferenceEquals(active, this))
                return false;

            if (IsPDAIntrusionRuntimeUsable(active))
            {
                GlobalRegistry.RegisterPDAIntrusionRuntime(active);
                s_activeRuntimeInstance = active;
                enabled = false;
                Destroy(this);
                return true;
            }

            GlobalRegistry.UnregisterPDAIntrusionRuntime(active);
            if (ReferenceEquals(s_activeRuntimeInstance, active))
                s_activeRuntimeInstance = null;

            return false;
        }

        private static bool IsPDAIntrusionRuntimeUsable(PDAIntrusionManager manager)
        {
            return manager != null && manager._serviceRegistered && manager.isActiveAndEnabled;
        }

        /// <inheritdoc />
        private void AdvanceIntrusionPresentationState(float dt)
        {
            float safeDeltaTime = SanitizeDeltaTime(dt);
            if (!_isHacked)
            {
                _restoreTextDriftRequested = true;
                TickAmbientIntrusionThreat(safeDeltaTime);
                return;
            }

            TickVisualCadence(safeDeltaTime);
            TickRebootHold(safeDeltaTime);
        }

        public void LateFrameTick()
        {
            float dt = SanitizeDeltaTime(SystemDispatcher.CurrentFrameUnscaledDeltaTime);
            AdvanceIntrusionPresentationState(SystemDispatcher.CurrentFrameDeltaTime);

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
            if (!math.isfinite(intensity) ||
                math.saturate(intensity) < ResolveEquipmentGlitchThreshold01(equipmentGlitchThreshold))
            {
                return;
            }

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
            _leviathanScanTimer = SanitizeNonNegativeSeconds(_leviathanScanTimer) - SanitizeDeltaTime(dt);
            if (_leviathanScanTimer > 0f)
                return;

            _leviathanScanTimer = ResolveLeviathanScanIntervalSeconds(leviathanScanInterval);

            if (!TryResolveIntrusionOriginAup(out AbsoluteUniversePosition originAup))
                return;

            if (!TryResolveRuntimePosition(in originAup, out Vector3 origin))
                return;

            if (ShouldTriggerHack(origin))
            {
                TriggerHack();
                return;
            }
        }

        private bool ShouldTriggerHack(Vector3 origin)
        {
            if (_playerMovement != null &&
                math.isfinite(_playerMovement.CurrentHullStress01) &&
                _playerMovement.CurrentHullStress01 > hullStressThreshold)
            {
                return true;
            }

            if (IsFinite(origin) && IsInsideDeadZone(origin))
                return true;

            int contactCount = WorldSpatialHashGrid.CollectContactsNonAlloc(
                origin,
                ResolveLeviathanHackRadiusMeters(leviathanHackRadius),
                SpatialTargetKind.Bioform,
                _bioformContacts);

            for (int i = 0; i < contactCount; i++)
            {
                IFaunaSpatialContact faunaContact = _bioformContacts[i].Owner as IFaunaSpatialContact;
                if (faunaContact != null && !faunaContact.IsDead && faunaContact.IsLeviathanContact)
                    return true;
            }

            return false;
        }

        private bool TryResolveIntrusionOriginAup(out AbsoluteUniversePosition originAup)
        {
            originAup = default;
            IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;
            if (runtimeContext != null)
            {
                if (runtimeContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                    (snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                    snapshot.Aup.IsFinite())
                {
                    originAup = snapshot.Aup;
                    return true;
                }

                if (runtimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                    (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                    movementState.PredictedAup.IsFinite())
                {
                    originAup = movementState.PredictedAup;
                    return true;
                }

                return false;
            }

            HectonPlayerMovement playerMovement = _playerMovement;
            if (playerMovement == null)
                return false;

            originAup = playerMovement.CurrentAup;
            return originAup.IsFinite();
        }

        private bool IsInsideDeadZone(Vector3 origin)
        {
            HectonMapMagicVegetationBridge bridge = _vegetationBridge;
            if (bridge == null)
                return false;

            HectonMapMagicVegetationBridge.VegetationDensitySample densitySample = bridge.GetVegetationDensity(origin);
            return densitySample.BiomeLayer == HectonMapMagicVegetationBridge.VegetationBiomeLayer.DeadZone;
        }

        private static bool TryResolveRuntimePosition(in AbsoluteUniversePosition positionAup, out Vector3 runtimePosition)
        {
            runtimePosition = default;
            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!positionAup.IsFinite() || !originAup.IsFinite())
                return false;

            double3 localDelta = AbsoluteUniversePosition.DeltaMetersClamped(in positionAup, in originAup);
            if (!math.all(math.isfinite(localDelta)))
                return false;

            runtimePosition.x = (float)localDelta.x;
            runtimePosition.y = (float)localDelta.y;
            runtimePosition.z = (float)localDelta.z;
            return math.isfinite(runtimePosition.x) &&
                   math.isfinite(runtimePosition.y) &&
                   math.isfinite(runtimePosition.z);
        }

        private void TickVisualCadence(float dt)
        {
            _visualPhaseTimer = SanitizeNonNegativeSeconds(_visualPhaseTimer) - SanitizeDeltaTime(dt);
            if (_visualPhaseTimer > 0f)
                return;

            _visualPhaseTimer = ResolveVisualPhaseDurationSeconds(visualPhaseDuration);
            _visualPhase = NextVisualPhase(_visualPhase);
            _visualPhaseDirty = true;
        }

        private void TickRebootHold(float dt)
        {
            float safeDeltaTime = SanitizeDeltaTime(dt);
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

            _rebootHoldTimer = SanitizeNonNegativeSeconds(_rebootHoldTimer) + safeDeltaTime;
            if (_rebootHoldTimer < ResolveRebootHoldDurationSeconds(rebootHoldDuration))
                return;

            CompleteReboot();
        }

        private void TickTextDrift(float dt)
        {
            float safeDeltaTime = SanitizeDeltaTime(dt);
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

            _textDriftRescanTimer = math.isfinite(_textDriftRescanTimer)
                ? _textDriftRescanTimer - safeDeltaTime
                : 0f;
            if (!ReferenceEquals(_driftPanelRoot, panelRoot))
            {
                RestoreTextDriftPositions();
                _textDriftRescanTimer = ResolveTextDriftRescanIntervalSeconds(TextDriftRescanInterval);
                return;
            }

            if (_driftTargetCount == 0)
                return;

            if (_textDriftRescanTimer <= 0f)
                _textDriftRescanTimer = ResolveTextDriftRescanIntervalSeconds(TextDriftRescanInterval);

            if (_driftTargetCount == 0)
                return;

            _textDriftWaveTime = math.isfinite(_textDriftWaveTime)
                ? _textDriftWaveTime + safeDeltaTime
                : 0f;
            float glyphScale = _visualPhase == IntrusionVisualPhase.Glyphs ? 1.22f : 1f;
            for (int i = 0; i < _driftTargetCount; i++)
            {
                ref TextDriftState state = ref _driftStates[i];
                if (state.Rect == null)
                    continue;

                float normalizedIndex = _driftTargetCount > 1
                    ? (float)i / (_driftTargetCount - 1)
                    : 0f;
                float amplitude = math.lerp(TextDriftAmplitudeMin, TextDriftAmplitudeMax, normalizedIndex) * glyphScale;
                float frequency = math.lerp(TextDriftFrequencyMin, TextDriftFrequencyMax, 1f - normalizedIndex);
                float offsetX = EvaluateCheapDriftWaveSigned((_textDriftWaveTime * frequency) + state.PhaseOffset) * amplitude;
                Vector3 currentPos = state.Rect.anchoredPosition3D;
                Vector3 expectedPos = state.BaseAnchoredPosition;
                expectedPos.x += state.AppliedOffset;

                if (math.abs(currentPos.x - expectedPos.x) > 0.01f ||
                    math.abs(currentPos.y - expectedPos.y) > 0.01f ||
                    math.abs(currentPos.z - expectedPos.z) > 0.01f)
                {
                    state.BaseAnchoredPosition = currentPos;
                }

                Vector3 basePosition = state.BaseAnchoredPosition;
                basePosition.x += offsetX;
                state.Rect.anchoredPosition3D = basePosition;
                state.AppliedOffset = offsetX;
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
            RectTransform root = panelRoot.transform as RectTransform;
            int stackCount = 0;
            if (root != null)
                _driftTraversalStack[MaxDriftTargets - 1 - stackCount++] = root;

            while (stackCount > 0 && _driftTargetCount < MaxDriftTargets)
            {
                int stackSlot = MaxDriftTargets - stackCount;
                RectTransform current = _driftTraversalStack[stackSlot];
                _driftTraversalStack[stackSlot] = null;
                stackCount--;
                if (current == null)
                    continue;

                if (current.TryGetComponent(out TextMeshProUGUI text) && text != null && text.enabled)
                {
                    RectTransform rect = text.rectTransform;
                    if (rect != null)
                    {
                        int slot = _driftTargetCount;
                        _driftStates[slot] = new TextDriftState
                        {
                            Target = text,
                            Rect = rect,
                            BaseAnchoredPosition = rect.anchoredPosition3D,
                            AppliedOffset = 0f,
                            PhaseOffset = (slot * 0.73f) + (text.fontSize * 0.013f)
                        };
                        _driftTargetCount++;
                    }
                }

                int childCount = current.childCount;
                for (int i = childCount - 1; i >= 0 && _driftTargetCount + stackCount < MaxDriftTargets; i--)
                {
                    if (current.GetChild(i) is RectTransform child)
                        _driftTraversalStack[MaxDriftTargets - 1 - stackCount++] = child;
                }
            }

            for (int i = 0; i < stackCount; i++)
                _driftTraversalStack[MaxDriftTargets - 1 - i] = null;
        }

        private void RebuildTextDriftTargetsCold()
        {
            PlayerPDA pda = _playerPda;
            GameObject panelRoot = pda != null ? pda.PanelRoot : null;
            if (panelRoot == null)
                return;

            RebuildTextDriftTargets(panelRoot);
        }

        private void RestoreTextDriftPositions()
        {
            if (_driftTargetCount <= 0)
                return;

            for (int i = 0; i < _driftTargetCount; i++)
            {
                ref TextDriftState state = ref _driftStates[i];
                if (state.Rect != null)
                {
                    Vector3 currentPos = state.Rect.anchoredPosition3D;
                    Vector3 expectedPos = state.BaseAnchoredPosition;
                    expectedPos.x += state.AppliedOffset;

                    if (math.abs(currentPos.x - expectedPos.x) <= 0.01f &&
                        math.abs(currentPos.y - expectedPos.y) <= 0.01f &&
                        math.abs(currentPos.z - expectedPos.z) <= 0.01f)
                    {
                        state.Rect.anchoredPosition3D = state.BaseAnchoredPosition;
                    }
                }

                state = default;
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
            _visualPhaseTimer = ResolveVisualPhaseDurationSeconds(visualPhaseDuration);
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
            ILocalizationTransientOverrideSink overrideSink = _transientOverrideSink;
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
            _transientOverrideSink?.ClearTransientLanguageOverride();
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
            _runtimeOwnerResolveRetryTimer = 0f;
            ResolveRuntimeOwners(RuntimeOwnerRetryIntervalSeconds);
        }

        private void ResolveRuntimeOwners(float dt)
        {
            if (_playerPda != null && _playerMovement != null && _vegetationBridge != null)
                return;

            _runtimeOwnerResolveRetryTimer = SanitizeNonNegativeSeconds(_runtimeOwnerResolveRetryTimer) -
                                             SanitizeDeltaTime(dt);
            if (_runtimeOwnerResolveRetryTimer > 0f)
                return;

            _runtimeOwnerResolveRetryTimer = RuntimeOwnerRetryIntervalSeconds;

            if (_playerPda == null)
            {
                if (!TryGetComponent(out _playerPda))
                    _playerPda = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<PlayerPDA>(transform);
            }

            if (_playerMovement == null)
                TryGetComponent(out _playerMovement);

            if (_vegetationBridge == null || !_vegetationBridge.isActiveAndEnabled)
                WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref _vegetationBridge);
        }

        private void BindInputActionOwnerCold()
        {
            ResolveInputActionOwner(CaptureNativeInputRuntimeCold());
        }

        private static INativeInputManagerRuntime CaptureNativeInputRuntimeCold()
        {
            return GlobalRegistry.NativeInputRuntime;
        }

        private void BindLocalizationOverrideSinkCold()
        {
            _transientOverrideSink = GlobalRegistry.LocalizationTransientOverrideSink;
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
            if (serviceSlot == GlobalRegistryServiceSlot.Input ||
                serviceSlot == GlobalRegistryServiceSlot.NativeInputManagerRuntime)
            {
                ClearInputActionOwner();

                if (!isActiveAndEnabled)
                    return;

                INativeInputManagerRuntime inputManager = currentService as INativeInputManagerRuntime;
                ResolveInputActionOwner(inputManager ?? CaptureNativeInputRuntimeCold());
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.LocalizationRuntime)
            {
                _transientOverrideSink = currentService as ILocalizationTransientOverrideSink;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _playerPda = null;
                _playerMovement = null;
                ResolveRuntimeOwners();
                RebuildTextDriftTargetsCold();
            }
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
            if (_registeredLateFrame || !Application.isPlaying)
                return;

            if (!_registeredLateFrame)
                _registeredLateFrame = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        private void UnregisterFromTickManager()
        {
            if (_registeredLateFrame)
            {
                SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            equipmentGlitchThreshold = ResolveEquipmentGlitchThreshold01(equipmentGlitchThreshold);
            hullStressThreshold = ResolveHullStressThreshold01(hullStressThreshold);
            leviathanScanInterval = ResolveLeviathanScanIntervalSeconds(leviathanScanInterval);
            leviathanHackRadius = ResolveLeviathanHackRadiusMeters(leviathanHackRadius);
            visualPhaseDuration = ResolveVisualPhaseDurationSeconds(visualPhaseDuration);
            rebootHoldDuration = ResolveRebootHoldDurationSeconds(rebootHoldDuration);
        }
#endif

        private static float SanitizeDeltaTime(float seconds)
        {
            return math.isfinite(seconds) ? math.max(0f, seconds) : 0f;
        }

        private static float SanitizeNonNegativeSeconds(float seconds)
        {
            return math.isfinite(seconds) ? math.max(0f, seconds) : 0f;
        }

        private static float ResolveHullStressThreshold01(float threshold)
        {
            return math.isfinite(threshold) ? math.saturate(threshold) : HullStressHackThreshold;
        }

        private static float ResolveEquipmentGlitchThreshold01(float threshold)
        {
            return math.isfinite(threshold) ? math.saturate(threshold) : EquipmentGlitchHackThreshold;
        }

        private static float ResolveLeviathanScanIntervalSeconds(float intervalSeconds)
        {
            return math.isfinite(intervalSeconds) ? math.max(0.05f, intervalSeconds) : LeviathanCheckInterval;
        }

        private static float ResolveLeviathanHackRadiusMeters(float radiusMeters)
        {
            return math.isfinite(radiusMeters) ? math.max(8f, radiusMeters) : LeviathanHackRadius;
        }

        private static float ResolveVisualPhaseDurationSeconds(float durationSeconds)
        {
            return math.isfinite(durationSeconds) ? math.max(0.1f, durationSeconds) : VisualPhaseDuration;
        }

        private static float ResolveRebootHoldDurationSeconds(float durationSeconds)
        {
            return math.isfinite(durationSeconds) ? math.max(0.5f, durationSeconds) : RebootHoldDuration;
        }

        private static float ResolveTextDriftRescanIntervalSeconds(float intervalSeconds)
        {
            return math.isfinite(intervalSeconds) ? math.max(0.1f, intervalSeconds) : TextDriftRescanInterval;
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
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
