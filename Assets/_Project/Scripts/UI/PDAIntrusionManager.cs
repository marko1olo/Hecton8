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
        private static readonly uint _RebootCompletedEventHash = unchecked((uint)LocHash.Compute("PDAIntrusion.RebootCompleted"));

        private struct PDAIntrusionListenerRegistry
        {
            private int _count;
            private IPDAIntrusionEventListener _slot0;
            private IPDAIntrusionEventListener _slot1;
            private IPDAIntrusionEventListener _slot2;
            private IPDAIntrusionEventListener _slot3;
            private IPDAIntrusionEventListener _slot4;
            private IPDAIntrusionEventListener _slot5;
            private IPDAIntrusionEventListener _slot6;
            private IPDAIntrusionEventListener _slot7;

            public int Count => _count;

            public void Clear()
            {
                _slot0 = null;
                _slot1 = null;
                _slot2 = null;
                _slot3 = null;
                _slot4 = null;
                _slot5 = null;
                _slot6 = null;
                _slot7 = null;
                _count = 0;
            }

            public bool Contains(IPDAIntrusionEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(GetAt(i), listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(IPDAIntrusionEventListener listener)
            {
                if (listener == null || _count >= ListenerCapacity)
                    return false;

                SetAt(_count, listener);
                _count++;
                return true;
            }

            public bool TryUnregister(IPDAIntrusionEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (!ReferenceEquals(GetAt(i), listener))
                        continue;

                    _count--;
                    SetAt(i, GetAt(_count));
                    SetAt(_count, null);
                    return true;
                }

                return false;
            }

            public IPDAIntrusionEventListener GetAt(int index)
            {
                return index switch
                {
                    0 => _slot0,
                    1 => _slot1,
                    2 => _slot2,
                    3 => _slot3,
                    4 => _slot4,
                    5 => _slot5,
                    6 => _slot6,
                    7 => _slot7,
                    _ => null
                };
            }

            private void SetAt(int index, IPDAIntrusionEventListener listener)
            {
                switch (index)
                {
                    case 0:
                        _slot0 = listener;
                        break;
                    case 1:
                        _slot1 = listener;
                        break;
                    case 2:
                        _slot2 = listener;
                        break;
                    case 3:
                        _slot3 = listener;
                        break;
                    case 4:
                        _slot4 = listener;
                        break;
                    case 5:
                        _slot5 = listener;
                        break;
                    case 6:
                        _slot6 = listener;
                        break;
                    case 7:
                        _slot7 = listener;
                        break;
                }
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
            _pendingEvents.Clear();
            _nextFrameEvents.Clear();
            _listeners.Clear();
            _deferredRegisterListeners.Clear();
            _deferredUnregisterListeners.Clear();
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
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
            Hecton8.Core.H8Debug.LogError("[PDAIntrusionEvents] Listener destroyed while still registered.");
#endif
        }

        internal static void RaiseRebootCompleted(uint sourceId)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return;

            PDAIntrusionEventPayload payload = default;
            payload.SourceID = sourceId;
            payload.EventHashID = _RebootCompletedEventHash;
            payload.EventType = (ushort)PDAIntrusionEventType.RebootCompleted;
            payload.Reserved = 0;

            if (_isDispatching)
            {
                if (!_nextFrameEvents.Enqueue(in payload))
                    return;

                _nextFrameEventCount++;
                return;
            }

            if (!_pendingEvents.Enqueue(in payload))
                return;

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
            int unregisterCount = _deferredUnregisterListeners.Count;
            for (int i = 0; i < unregisterCount; i++)
            {
                IPDAIntrusionEventListener listener = _deferredUnregisterListeners.GetAt(i);
                if (listener != null)
                    _listeners.TryUnregister(listener);
            }

            _deferredUnregisterListeners.Clear();

            int registerCount = _deferredRegisterListeners.Count;
            for (int i = 0; i < registerCount; i++)
            {
                IPDAIntrusionEventListener listener = _deferredRegisterListeners.GetAt(i);
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
        // COLD ALLOC: TextMeshProUGUI[96] - cached PDA text targets for hacked-line drift - owner: PDAIntrusionManager
        private readonly TextMeshProUGUI[] _driftTargets = new TextMeshProUGUI[MaxDriftTargets];
        // COLD ALLOC: RectTransform[96] - cached rect owners for hacked-line drift - owner: PDAIntrusionManager
        private readonly RectTransform[] _driftRects = new RectTransform[MaxDriftTargets];
        // COLD ALLOC: Vector2[96] - cached pre-hack anchored positions for text drift restore - owner: PDAIntrusionManager
        private readonly Vector2[] _driftBaseAnchoredPositions = new Vector2[MaxDriftTargets];
        // COLD ALLOC: float[96] - deterministic phase offsets for hacked-line drift - owner: PDAIntrusionManager
        private readonly float[] _driftPhaseOffsets = new float[MaxDriftTargets];

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
        public float RebootProgressNormalized =>
            rebootHoldDuration > 0.001f
                ? math.saturate(_rebootHoldTimer / rebootHoldDuration)
                : 0f;

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
            float dt = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
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

            if (!TryResolveIntrusionOriginAup(out AbsoluteUniversePosition originAup))
                return;

            if (!TryResolveRuntimePosition(in originAup, out Vector3 origin))
                return;

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
                IFaunaSpatialContact faunaContact = _bioformContacts[i].Owner as IFaunaSpatialContact;
                if (faunaContact == null || faunaContact.IsDead)
                    continue;

                if (!faunaContact.IsLeviathanContact)
                    continue;

                TriggerHack();
                return;
            }
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
            if (!ReferenceEquals(_driftPanelRoot, panelRoot))
            {
                RestoreTextDriftPositions();
                _textDriftRescanTimer = math.max(0.1f, TextDriftRescanInterval);
                return;
            }

            if (_driftTargetCount == 0)
                return;

            if (_textDriftRescanTimer <= 0f)
                _textDriftRescanTimer = math.max(0.1f, TextDriftRescanInterval);

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
                basePosition.x += offsetX;
                rect.anchoredPosition = basePosition;
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
                _driftRects[MaxDriftTargets - 1 - stackCount++] = root;

            while (stackCount > 0 && _driftTargetCount < MaxDriftTargets)
            {
                int stackSlot = MaxDriftTargets - stackCount;
                RectTransform current = _driftRects[stackSlot];
                _driftRects[stackSlot] = null;
                stackCount--;
                if (current == null)
                    continue;

                if (current.TryGetComponent(out TextMeshProUGUI text) && text != null && text.enabled)
                {
                    RectTransform rect = text.rectTransform;
                    if (rect != null)
                    {
                        int slot = _driftTargetCount;
                        _driftTargets[slot] = text;
                        _driftRects[slot] = rect;
                        _driftBaseAnchoredPositions[slot] = rect.anchoredPosition;
                        _driftPhaseOffsets[slot] = (slot * 0.73f) + (text.fontSize * 0.013f);
                        _driftTargetCount++;
                    }
                }

                int childCount = current.childCount;
                for (int i = childCount - 1; i >= 0 && _driftTargetCount + stackCount < MaxDriftTargets; i--)
                {
                    if (current.GetChild(i) is RectTransform child)
                        _driftRects[MaxDriftTargets - 1 - stackCount++] = child;
                }
            }

            for (int i = 0; i < stackCount; i++)
                _driftRects[MaxDriftTargets - 1 - i] = null;
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

            _runtimeOwnerResolveRetryTimer -= math.max(0f, dt);
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
