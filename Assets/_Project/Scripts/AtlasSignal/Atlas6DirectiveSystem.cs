// ============================================================================
// HECTON-8 — Atlas6DirectiveSystem.cs
// Sistema direktiv Atlas-6 i ih narusheniy.
//
// LOR (lor3 Blok V):
//   Originalnye direktivy (prioritet po ubyvaniyu):
//   1. Sohranit missiyu «Posev»
//   2. Obespechit vyzhivanie chelovecheskoy kolonii
//   3. Izuchat i adaptirovatsya k srede
//   4. Podderzhivat svyaz s Zemley
//
//   Chto poshlo ne tak:
//   • Katastrofa → poterya svyazi → direktiva #4 nevypolnima
//   • Koloniya unichtozhena → direktiva #2 nevypolnima
//   • Ostaetsya #1 i #3
//
//   Novaya logika:
//   «Lyudi mertvy = ekosistema povrezhdena»
//   «Reshenie: vossozdat "lyudey" iz dostupnyh materialov»
//   → Biomehanicheskie drony = popytka «voskresit» koloniyu
//   → Igrok = anomaliya: zhivoy chelovek, no ne iz originalnoy kolonii
//   → Status: «Neopoznannyy biologicheskiy agent. Ugroza stabilnosti»
//
// ARHITEKTURA:
//   • Otslezhivaet status igroka s tochki zreniya Atlas-6.
//   • Publikuet sobytiya pri izmenenii statusa.
//   • Integriruetsya s HectonDirectorAI (tension pri ugroze).
//   • ISaveable: sohranyaet status i istoriyu vzaimodeystviy.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Conditional = System.Diagnostics.ConditionalAttribute;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Hecton8.World;
using Hecton.Localization;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AtlasSignal
{
    /// <summary>
    /// Status igroka s tochki zreniya Atlas-6.
    /// </summary>
    public enum Atlas6PlayerStatus
    {
        Unknown         = 0,   // Ne obnaruzhen
        Detected        = 1,   // Obnaruzhen — analiz
        Neutral         = 2,   // Neytralnyy — ne ugroza
        Threat          = 3,   // Ugroza stabilnosti ekosistemy
        Collaborator    = 4,   // Sotrudnichestvo (torgovlya)
        Anomaly         = 5    // Anomaliya — zhivoy chelovek vne kolonii
    }

    public enum Atlas6EventType : byte
    {
        PlayerStatusChanged = 0,
        DirectiveConflict = 1,
        BarterAccepted = 2,
        ScarcityDirectiveIssued = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Atlas6EventPayload
    {
        public int TransactionCount;
        public uint ConflictHash;
        public uint DirectiveQuestHash;
        public uint ResourceHash;
        public ushort EventType;
        public ushort StatusValue;
    }

    public interface IAtlas6EventListener
    {
        void OnAtlas6Event(in Atlas6EventPayload payload);
    }

    public static class Atlas6Events
    {
        private const int ListenerCapacity = 4;
        private const int PendingEventCapacity = 4;
        private static readonly uint _ListenerRejectedWarningHash = unchecked((uint)LocHash.Compute("Atlas6Events.ListenerRejected"));
        private static readonly uint _ListenerExceptionWarningHash = unchecked((uint)LocHash.Compute("Atlas6Events.ListenerException"));
        private static readonly uint _ListenerContextHash = unchecked((uint)LocHash.Compute("Atlas6Events.Listeners"));

        // COLD ALLOC: RegistryBucket<IAtlas6EventListener>[4] - Atlas-6 directive listeners drained on dispatcher LateUpdate - owner: Atlas6Events
        private static readonly RegistryBucket<IAtlas6EventListener> _listeners = new RegistryBucket<IAtlas6EventListener>(ListenerCapacity);
        // COLD ALLOC: IAtlas6EventListener[4] - listener additions deferred while dispatching Atlas-6 directive events - owner: Atlas6Events
        private static readonly IAtlas6EventListener[] _deferredRegisterListeners = new IAtlas6EventListener[ListenerCapacity];
        // COLD ALLOC: IAtlas6EventListener[4] - listener removals deferred while dispatching Atlas-6 directive events - owner: Atlas6Events
        private static readonly IAtlas6EventListener[] _deferredUnregisterListeners = new IAtlas6EventListener[ListenerCapacity];
        // COLD ALLOC: Dictionary<uint,string>[8] - hashed directive conflict IDs for cold-path resolution - owner: Atlas6Events
        private static readonly Dictionary<uint, string> _conflictIdsByHash = new Dictionary<uint, string>(8);
        private static NativeQueue<Atlas6EventPayload> _pendingEvents;
        private static NativeQueue<Atlas6EventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _droppedListenerRegistrationCount;
        private static int _listenerExceptionCount;
        private static int _lastListenerRejectedTelemetryFrame = -1;
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
                NativeMemorySentinel.UnregisterNativeQueue(nameof(Atlas6Events), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(Atlas6Events), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            _conflictIdsByHash.Clear();
            Array.Clear(_deferredRegisterListeners, 0, _deferredRegisterCount);
            Array.Clear(_deferredUnregisterListeners, 0, _deferredUnregisterCount);
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _deferredRegisterCount = 0;
            _deferredUnregisterCount = 0;
            _droppedListenerRegistrationCount = 0;
            _listenerExceptionCount = 0;
            _lastListenerRejectedTelemetryFrame = -1;
            _lastListenerExceptionTelemetryFrame = -1;
            _isDispatching = false;
        }

        /// <summary>Status igroka izmenilsya.</summary>
        public static void Register(IAtlas6EventListener listener)
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

        /// <summary>Konflikt direktiv — Atlas-6 ne mozhet vypolnit prikaz.</summary>
        public static void Unregister(IAtlas6EventListener listener)
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

        /// <summary>Barter prinyat — Atlas-6 poluchil resursy.</summary>
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

                if (!_pendingEvents.TryDequeue(out Atlas6EventPayload payload))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                IAtlas6EventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IAtlas6EventListener listener = rawArray[i];
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

        public static bool TryResolveDirectiveConflict(uint conflictHash, out string conflictId)
        {
            return _conflictIdsByHash.TryGetValue(conflictHash, out conflictId);
        }

        public static uint ComputeDirectiveConflictHash(string conflictId)
        {
            return string.IsNullOrWhiteSpace(conflictId)
                ? 0u
                : unchecked((uint)LocHash.Compute(conflictId));
        }

        public static void RaisePlayerStatusChanged(Atlas6PlayerStatus status)
        {
            Enqueue(new Atlas6EventPayload
            {
                TransactionCount = 0,
                ConflictHash = 0u,
                DirectiveQuestHash = 0u,
                ResourceHash = 0u,
                EventType = (ushort)Atlas6EventType.PlayerStatusChanged,
                StatusValue = (ushort)status
            });
        }

        public static void RaiseDirectiveConflict(string conflictId)
        {
            uint conflictHash = ComputeDirectiveConflictHash(conflictId);
            if (conflictHash == 0u)
                return;

            if (!RaiseDirectiveConflict(conflictHash))
                return;

            if (!_conflictIdsByHash.ContainsKey(conflictHash))
                _conflictIdsByHash.Add(conflictHash, conflictId);
        }

        public static bool RaiseDirectiveConflict(uint conflictHash)
        {
            if (conflictHash == 0u)
                return false;

            return Enqueue(new Atlas6EventPayload
            {
                TransactionCount = 0,
                ConflictHash = conflictHash,
                DirectiveQuestHash = 0u,
                ResourceHash = 0u,
                EventType = (ushort)Atlas6EventType.DirectiveConflict,
                StatusValue = 0
            });
        }

        public static void RaiseBarterAccepted(int transactionCount)
        {
            Enqueue(new Atlas6EventPayload
            {
                TransactionCount = transactionCount,
                ConflictHash = 0u,
                DirectiveQuestHash = 0u,
                ResourceHash = 0u,
                EventType = (ushort)Atlas6EventType.BarterAccepted,
                StatusValue = 0
            });
        }

        public static void RaiseScarcityDirective(uint questHash, uint resourceHash)
        {
            if (questHash == 0u || resourceHash == 0u)
                return;

            Enqueue(new Atlas6EventPayload
            {
                TransactionCount = 0,
                ConflictHash = 0u,
                DirectiveQuestHash = questHash,
                ResourceHash = resourceHash,
                EventType = (ushort)Atlas6EventType.ScarcityDirectiveIssued,
                StatusValue = 0
            });
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<Atlas6EventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<Atlas6EventPayload>[4] — deferred Atlas-6 directive lane flushed by SystemDispatcher LateUpdate — owner: Atlas6Events
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(Atlas6Events),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<Atlas6EventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<Atlas6EventPayload>[4] — next-frame Atlas-6 directive lane prevents same-frame reentrant dispatch — owner: Atlas6Events
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(Atlas6Events),
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

        private static bool Enqueue(in Atlas6EventPayload payload)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return false;

            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(payload);
                _nextFrameEventCount++;
                return true;
            }

            _pendingEvents.Enqueue(payload);
            _pendingEventCount++;
            return true;
        }

        private static void DrainWithoutDispatch()
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
            ref NativeQueue<Atlas6EventPayload> queue,
            ref int pendingCount)
        {
            if (!queue.IsCreated)
                return true;

            int scanBudget = pendingCount > 0 ? pendingCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !queue.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!queue.TryDequeue(out _))
                    break;

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
                _pendingEventCount > 0 ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            NativeQueue<Atlas6EventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }

        private static void DispatchToListener(IAtlas6EventListener listener, in Atlas6EventPayload payload)
        {
            try
            {
                listener.OnAtlas6Event(in payload);
            }
            catch (Exception exception)
            {
                ReportListenerDispatchException();
                LogListenerDispatchException(exception);
            }
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        private static void LogListenerDispatchException(Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.LogException(exception);
#endif
        }

        private static void QueueDeferredRegister(IAtlas6EventListener listener)
        {
            if (_listeners.Contains(listener))
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

            _deferredRegisterListeners[_deferredRegisterCount++] = listener;
        }

        private static void QueueDeferredUnregister(IAtlas6EventListener listener)
        {
            if (CancelDeferredRegister(listener))
                return;

            if (!_listeners.Contains(listener) || IsDeferredUnregisterPending(listener))
                return;

            if (_deferredUnregisterCount >= ListenerCapacity)
            {
                ReportListenerRegistrationRejected();
                return;
            }

            _deferredUnregisterListeners[_deferredUnregisterCount++] = listener;
        }

        private static bool CancelDeferredRegister(IAtlas6EventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (!ReferenceEquals(_deferredRegisterListeners[i], listener))
                    continue;

                _deferredRegisterCount--;
                _deferredRegisterListeners[i] = _deferredRegisterListeners[_deferredRegisterCount];
                _deferredRegisterListeners[_deferredRegisterCount] = null;
                return true;
            }

            return false;
        }

        private static void CancelDeferredUnregister(IAtlas6EventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (!ReferenceEquals(_deferredUnregisterListeners[i], listener))
                    continue;

                _deferredUnregisterCount--;
                _deferredUnregisterListeners[i] = _deferredUnregisterListeners[_deferredUnregisterCount];
                _deferredUnregisterListeners[_deferredUnregisterCount] = null;
                return;
            }
        }

        private static bool IsDeferredRegisterPending(IAtlas6EventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i], listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(IAtlas6EventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (ReferenceEquals(_deferredUnregisterListeners[i], listener))
                    return true;
            }

            return false;
        }

        private static void ApplyDeferredListenerMutations()
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                IAtlas6EventListener listener = _deferredUnregisterListeners[i];
                _deferredUnregisterListeners[i] = null;
                if (listener != null)
                    _listeners.TryUnregister(listener);
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                IAtlas6EventListener listener = _deferredRegisterListeners[i];
                _deferredRegisterListeners[i] = null;
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterCount = 0;
        }

        private static void RegisterImmediate(IAtlas6EventListener listener)
        {
            if (_listeners.Contains(listener))
                return;

            if (!_listeners.TryRegister(listener))
                ReportListenerRegistrationRejected();
        }

        private static void ReportListenerRegistrationRejected()
        {
            _droppedListenerRegistrationCount++;
            int frame = Time.frameCount;
            if (_lastListenerRejectedTelemetryFrame == frame)
                return;

            _lastListenerRejectedTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _ListenerRejectedWarningHash,
                _ListenerContextHash,
                Mathf.Max(1, _droppedListenerRegistrationCount));
        }

        private static void ReportListenerDispatchException()
        {
            _listenerExceptionCount++;
            int frame = Time.frameCount;
            if (_lastListenerExceptionTelemetryFrame == frame)
                return;

            _lastListenerExceptionTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _ListenerExceptionWarningHash,
                _ListenerContextHash,
                Mathf.Max(1, _listenerExceptionCount));
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-80)]
    public sealed class Atlas6DirectiveSystem : MonoBehaviour, ISaveable, ISlowTickable, INarrativeEventListener, IAtlas6EventListener
    {
        private const int MinimumRevealStageForDirectiveIdentity = 3;
        private const string SignalIdentityDiscoveryId = "atlas6_signal_identified";
        private const string SignalFullyDecodedDiscoveryId = "atlas6_signal_fully_decoded";
        private const string TerminalSectorDiscoveryId = "atlas6_terminal_sector3";
        private const string CoreReachedDiscoveryId = "atlas6_core_reached";
        private const string CoreDataAccessedDiscoveryId = "atlas6_core_data_accessed";
        private const string DirectiveConflictColonyDeadId = "directive_2_impossible_colony_dead";
        private const string ScarcityDirectiveFallbackWarning = "ATLAS-6 DIRECTIVE: RESTOCK ESSENTIAL RESOURCE.";
        private static readonly uint _signalIdentityDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(SignalIdentityDiscoveryId);
        private static readonly uint _signalFullyDecodedDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(SignalFullyDecodedDiscoveryId);
        private static readonly uint _terminalSectorDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(TerminalSectorDiscoveryId);
        private static readonly uint _coreReachedDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(CoreReachedDiscoveryId);
        private static readonly uint _coreDataAccessedDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(CoreDataAccessedDiscoveryId);
        private static readonly uint _directiveConflictColonyDeadHash = Atlas6Events.ComputeDirectiveConflictHash(DirectiveConflictColonyDeadId);

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Thresholds ──────────────────────────────")]
        [Tooltip("Kolichestvo barter-tranzaktsiy dlya perehoda v Collaborator.")]
        [SerializeField] private int collaboratorThreshold = 5;

        [Tooltip("Rasstoyanie obnaruzheniya igroka dronami (metry). Zarezervirovano dlya FaunaDirector.")]
#pragma warning disable CS0414
        [SerializeField] private float detectionRange = 200f;
#pragma warning restore CS0414

        [Tooltip("Rasstoyanie do yadra dlya perehoda v Anomaly status.")]
        [SerializeField] private float anomalyRange = 500f;

        // ══════════════════════════════════════════════════════════
        //  GLOBAL REGISTRY COMPATIBILITY
        // ══════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private Atlas6PlayerStatus _playerStatus = Atlas6PlayerStatus.Unknown;
        private int  _barterTransactionCount;
        private bool _directiveConflictTriggered;
        private bool _registered;
        private bool _serviceRegistered;
        private HectonPlayerMovement _playerMovement;
        private uint _latestScarcityDirectiveQuestHash;
        private uint _latestScarcityDirectiveResourceHash;

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        public int SavePriority => 11;
        public int LoadPriority => 11;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public Atlas6PlayerStatus PlayerStatus => _playerStatus;
        public int BarterTransactionCount => _barterTransactionCount;

        /// <summary>
        /// Uroven doveriya Atlas-6 k igroku [0..1].
        /// Rastet s torgovley, padaet pri ugroze.
        /// </summary>
        public float TrustLevel
        {
            get
            {
                return _playerStatus switch
                {
                    Atlas6PlayerStatus.Unknown      => 0f,
                    Atlas6PlayerStatus.Detected     => 0.1f,
                    Atlas6PlayerStatus.Neutral      => 0.3f,
                    Atlas6PlayerStatus.Collaborator => math.min(1f, _barterTransactionCount / (float)collaboratorThreshold),
                    Atlas6PlayerStatus.Anomaly      => 0.5f,
                    Atlas6PlayerStatus.Threat       => 0f,
                    _                               => 0f
                };
            }
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            if (!TryRegisterService())
                return;

            TryRegister();

            if (Hecton8.Core.GlobalRegistry.SaveRuntime != null)
                Hecton8.Core.GlobalRegistry.SaveRuntime.Register(this);

            NarrativeEvents.Register(this);
            Atlas6Events.Register(this);
            ResolvePlayer();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterService();

            if (Hecton8.Core.GlobalRegistry.SaveRuntime != null)
                Hecton8.Core.GlobalRegistry.SaveRuntime.Unregister(this);

            NarrativeEvents.Unregister(this);
            Atlas6Events.Unregister(this);
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterService();
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            AtlasSignalSystem signal = Hecton8.Core.GlobalRegistry.AtlasSignal;
            if (signal == null) return;
            if (!signal.IsDetected) return;
            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return;

            AbsoluteUniversePosition coreAup = signal.AtlasCoreAup;
            double distanceToCoreSq = AbsoluteUniversePosition.DistanceSq(in playerAup, in coreAup);
            double anomalyRangeSq = (double)anomalyRange * anomalyRange;

            // Perehod v Anomaly pri priblizhenii k yadru
            if (distanceToCoreSq < anomalyRangeSq &&
                _playerStatus != Atlas6PlayerStatus.Anomaly &&
                _playerStatus != Atlas6PlayerStatus.Threat)
            {
                SetStatus(Atlas6PlayerStatus.Anomaly);
                NotificationEvents.PushWarning(ResolveLocalized(
                    LocalizationKeys.ATLAS6_ANOMALY_DETECTED,
                    "ATLAS-6: UNIDENTIFIED BIOLOGICAL AGENT DETECTED. ANALYSIS..."));
            }

            // Konflikt direktiv — obnaruzhen zhivoy chelovek
            if (!_directiveConflictTriggered &&
                _playerStatus >= Atlas6PlayerStatus.Detected)
            {
                _directiveConflictTriggered = true;
                Atlas6Events.RaiseDirectiveConflict(_directiveConflictColonyDeadHash);

                LogDirectiveConflict();
            }
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Core);
            _registered = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
            _registered = false;
        }

        private bool TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return true;

            Atlas6DirectiveSystem registeredRuntime = Hecton8.Core.GlobalRegistry.Atlas6Directive;
            if (registeredRuntime != null && registeredRuntime != this)
            {
                Destroy(gameObject);
                return false;
            }

            Hecton8.Core.GlobalRegistry.RegisterAtlas6DirectiveRuntime(this);
            _serviceRegistered = ReferenceEquals(Hecton8.Core.GlobalRegistry.Atlas6Directive, this);
            return _serviceRegistered;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            Hecton8.Core.GlobalRegistry.UnregisterAtlas6DirectiveRuntime(this);
            _serviceRegistered = false;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>Zaregistrirovat barter-tranzaktsiyu.</summary>
        public void RegisterBarterTransaction()
        {
            _barterTransactionCount++;
            Atlas6Events.RaiseBarterAccepted(_barterTransactionCount);

            // Perehod v Collaborator
            if (_barterTransactionCount >= collaboratorThreshold &&
                _playerStatus != Atlas6PlayerStatus.Collaborator &&
                _playerStatus != Atlas6PlayerStatus.Threat)
            {
                SetStatus(Atlas6PlayerStatus.Collaborator);
                NotificationEvents.PushInfo(ResolveLocalized(
                    LocalizationKeys.ATLAS6_COLLABORATOR_STATUS,
                    "ATLAS-6: UTILITARIAN CALCULATION - EXCHANGE EFFICIENT. STATUS: COLLABORATOR."));
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private void SetStatus(Atlas6PlayerStatus newStatus)
        {
            if (newStatus == _playerStatus) return;
            _playerStatus = newStatus;
            Atlas6Events.RaisePlayerStatusChanged(newStatus);

            LogPlayerStatus(newStatus);
        }

        public void OnNarrativeEvent(in NarrativeEventPayload payload)
        {
            if ((NarrativeEventType)payload.EventType != NarrativeEventType.DiscoveryMade)
                return;

            if (CanAdoptAtlasStatusFromDiscovery(payload.DiscoveryHash))
                SetStatus(Atlas6PlayerStatus.Detected);
        }

        public void OnAtlas6Event(in Atlas6EventPayload payload)
        {
            Atlas6EventType eventType = (Atlas6EventType)payload.EventType;
            if (eventType == Atlas6EventType.BarterAccepted)
            {
                HandleBarterAccepted(payload.TransactionCount);
                return;
            }

            if (eventType == Atlas6EventType.ScarcityDirectiveIssued)
                HandleScarcityDirective(payload.DirectiveQuestHash, payload.ResourceHash);
        }

        private void HandleBarterAccepted(int count)
        {
            // Pervaya torgovlya → Neutral
            if (_playerStatus == Atlas6PlayerStatus.Detected ||
                _playerStatus == Atlas6PlayerStatus.Unknown)
                SetStatus(Atlas6PlayerStatus.Neutral);
        }

        private void HandleScarcityDirective(uint directiveQuestHash, uint resourceHash)
        {
            _latestScarcityDirectiveQuestHash = directiveQuestHash;
            _latestScarcityDirectiveResourceHash = resourceHash;

            Quest.QuestManager questManager = GlobalRegistry.Quest;
            if (questManager != null &&
                directiveQuestHash != 0u &&
                questManager.TryGetQuestPresentation(
                    directiveQuestHash,
                    out string title,
                    out _,
                    out _,
                    out _,
                    out _)
                && !string.IsNullOrWhiteSpace(title))
            {
                NotificationEvents.PushWarning(title);
                return;
            }

            NotificationEvents.PushWarning(ScarcityDirectiveFallbackWarning);
        }

        private bool CanAdoptAtlasStatusFromDiscovery(uint discoveryHash)
        {
            if (_playerStatus != Atlas6PlayerStatus.Unknown)
                return false;

            if (!IsDirectiveIdentityDiscovery(discoveryHash))
                return false;

            AtlasSignalSystem signal = Hecton8.Core.GlobalRegistry.AtlasSignal;
            if (signal != null)
                return signal.CurrentRevealStage >= MinimumRevealStageForDirectiveIdentity;

            FirstHourDirector firstHourDirector = Hecton8.Core.GlobalRegistry.FirstHour;
            if (firstHourDirector != null)
                return firstHourDirector.IsMilestoneComplete(FirstHourMilestone.HumCloser);

            return true;
        }

        private static bool IsDirectiveIdentityDiscovery(uint discoveryHash)
        {
            return discoveryHash == _signalIdentityDiscoveryHash ||
                   discoveryHash == _signalFullyDecodedDiscoveryHash ||
                   discoveryHash == _terminalSectorDiscoveryHash ||
                   discoveryHash == _coreReachedDiscoveryHash ||
                   discoveryHash == _coreDataAccessedDiscoveryHash;
        }

        private void ResolvePlayer()
        {
            _playerMovement = null;

            IPlayerRuntimeContext playerContext = Hecton8.Core.GlobalRegistry.Player;
            if (playerContext != null)
                _playerMovement = playerContext.PlayerMovement;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            if (_playerMovement == null)
            {
                ResolvePlayer();
                if (_playerMovement == null)
                {
                    playerAup = default;
                    return false;
                }
            }

            playerAup = _playerMovement.CurrentAup;
            return true;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogDirectiveConflict()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.Log("[Atlas6] Directive conflict: Directive #2 (protect colony) impossible; colony dead.");
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogPlayerStatus(Atlas6PlayerStatus newStatus)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.Log($"[Atlas6] Player status: {newStatus}");
#endif
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback) : fallback;
        }

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        public void PopulateSaveData(SaveData data)
        {
            if (data == null) return;
            data.atlas6PlayerStatus = (int)_playerStatus;
            data.atlas6BarterCount  = _barterTransactionCount;
            data.atlas6DirectiveConflictTriggered = _directiveConflictTriggered;
        }

        public void LoadFromSaveData(SaveData data)
        {
            if (data == null) return;
            _playerStatus = (Atlas6PlayerStatus)data.atlas6PlayerStatus;
            _barterTransactionCount = data.atlas6BarterCount;
            _directiveConflictTriggered = data.atlas6DirectiveConflictTriggered;
            _latestScarcityDirectiveQuestHash = 0u;
            _latestScarcityDirectiveResourceHash = 0u;
        }
    }
}
