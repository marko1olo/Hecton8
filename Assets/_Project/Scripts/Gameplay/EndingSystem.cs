// ============================================================================
// HECTON-8 — EndingSystem.cs
// Система концовок игры.
//
// ЛОР (лор1 — Финал):
//   Игрок добрался до ядра Атлас-6 на -5000м.
//   Три выбора — ни один не "правильный". Это нуар.
//
//   ВЫКЛЮЧИТЬ АТЛАС-6:
//     Сигнал прекращается. Корпорация придёт.
//     Терраформирование продолжится. Жизнь уничтожена.
//     Игрок улетает. Экономически логично — морально нет.
//
//   ОСТАВИТЬ АТЛАС-6:
//     Сигнал продолжается. Корпорация не придёт пока сигнал активен.
//     Жизнь защищена — временно. Сигнал когда-нибудь найдут и заглушат.
//
//   УСИЛИТЬ СИГНАЛ:
//     Сигнал публичный — весь сектор слышит.
//     Корпорацию не остановить — но теперь все знают.
//     Атлас-6 выключается сам — задача выполнена.
//     Игрок становится тем, кто раскрыл тайну.
//
// АРХИТЕКТУРА:
//   • Отслеживает условия активации (глубина + расшифровка сигнала).
//   • Публикует события для всех систем при выборе концовки.
//   • ISaveable: сохраняет выбранную концовку.
//   • Интегрируется с Atlas6DirectiveSystem, QuestManager, NarrativeEvents.
//
// ZERO GC:
//   • Static events, enum state.
//   • Никаких new/LINQ в hot path.
// ============================================================================

using Conditional = System.Diagnostics.ConditionalAttribute;
using Hecton8.AtlasSignal;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Quest;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Hecton.Localization;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Gameplay
{
    public enum EndingChoice
    {
        None        = 0,
        ShutDown    = 1,   // Выключить Атлас-6
        Leave       = 2,   // Оставить Атлас-6
        Amplify     = 3    // Усилить сигнал
    }

    /// <summary>
    /// Ending event discriminator for <see cref="EndingEventPayload"/>.
    /// </summary>
    public enum EndingEventType : byte
    {
        ConditionMet = 0,
        Chosen = 1,
        SequenceComplete = 2
    }

    /// <summary>
    /// Unmanaged ending event payload.
    /// </summary>
    public struct EndingEventPayload
    {
        public byte EventType;
        public byte Choice;
        public ushort Reserved;
    }

    /// <summary>
    /// Listener contract for ending events drained from <see cref="SystemDispatcher"/>.
    /// </summary>
    public interface IEndingEventListener
    {
        /// <summary>
        /// Consumes one queue-drained ending event.
        /// </summary>
        /// <param name="payload">Unmanaged ending payload.</param>
        void OnEndingEvent(in EndingEventPayload payload);
    }

    public static class EndingEvents
    {
        private const int ListenerCapacity = 8;
        private const int PendingEventCapacity = 8;

        // COLD ALLOC: RegistryBucket<IEndingEventListener>[8] - ending listeners drained by SystemDispatcher LateUpdate - owner: EndingEvents
        private static readonly RegistryBucket<IEndingEventListener> _listeners = new RegistryBucket<IEndingEventListener>(ListenerCapacity);
        private static NativeQueue<EndingEventPayload> _pendingEvents;
        private static NativeQueue<EndingEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;

        /// <summary>
        /// Number of queued ending events awaiting LateUpdate dispatch.
        /// </summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(EndingEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(EndingEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
        }

        /// <summary>
        /// Registers a listener for deferred ending events.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Register(IEndingEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        /// <summary>
        /// Unregisters a listener from deferred ending events.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Unregister(IEndingEventListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        public static void RaiseConditionMet()
        {
            Enqueue(EndingEventType.ConditionMet, EndingChoice.None);
        }

        public static void RaiseChosen(EndingChoice choice)
        {
            Enqueue(EndingEventType.Chosen, choice);
        }

        public static void RaiseSequenceComplete(EndingChoice choice)
        {
            Enqueue(EndingEventType.SequenceComplete, choice);
        }

        /// <summary>
        /// Flushes queued ending events to registered listeners.
        /// Called by <see cref="SystemDispatcher"/> from LateUpdate.
        /// </summary>
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

                if (!_pendingEvents.TryDequeue(out EndingEventPayload payload))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                IEndingEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IEndingEventListener listener = rawArray[i];
                        if (listener != null)
                            listener.OnEndingEvent(in payload);
                    }
                }
                finally
                {
                    _isDispatching = false;
                }
            }

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        private static void Enqueue(EndingEventType type, EndingChoice choice)
        {
            if (_listeners.Count <= 0)
                return;

            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return;

            EndingEventPayload payload = new EndingEventPayload
            {
                EventType = (byte)type,
                Choice = (byte)choice,
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

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<EndingEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<EndingEventPayload>[8] - deferred ending lane flushed by SystemDispatcher LateUpdate - owner: EndingEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(EndingEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<EndingEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<EndingEventPayload>[8] - next-frame ending lane prevents same-frame reentrant dispatch - owner: EndingEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(EndingEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
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
            ref NativeQueue<EndingEventPayload> queue,
            ref int pendingCount)
        {
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
                !_pendingEvents.IsEmpty() ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            NativeQueue<EndingEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class EndingSystem : MonoBehaviour, ISaveable, ISlowTickable, IAtlasSignalEventListener
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Activation Conditions ───────────────────")]
        [Tooltip("Минимальная глубина для активации концовки (метры).")]
        [SerializeField] private float requiredDepth = 4800f;

        [Tooltip("Минимальная сила сигнала для активации (расшифровка).")]
        [SerializeField, Range(0f, 1f)] private float requiredSignalStrength = 0.90f;

        [Header("── Quest IDs ───────────────────────────────")]
        [SerializeField] private string endingQuestId = "quest_atlas_core_reached";

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        public static EndingSystem Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Instance = null;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private EndingChoice _chosenEnding = EndingChoice.None;
        private bool _conditionMet;
        private bool _endingComplete;
        private bool _registered;
        private bool _serviceRegistered;
        private HectonSurvivalSystem _survivalSystem;

        // ══════════════════════════════════════════════════════════
        //  ISaveable
        // ══════════════════════════════════════════════════════════

        public int SavePriority => 14;
        public int LoadPriority => 14;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public EndingChoice ChosenEnding    => _chosenEnding;
        public bool IsConditionMet          => _conditionMet;
        public bool IsEndingComplete        => _endingComplete;
        public bool CanChooseEnding         => _conditionMet && !_endingComplete;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            TryRegister();
            TryRegisterService();

            if (Hecton8.Core.GlobalRegistry.SaveRuntime != null)
                Hecton8.Core.GlobalRegistry.SaveRuntime.Register(this);

            AtlasSignalEvents.Register(this);

            ResolveSurvivalSystem();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterService();

            if (Hecton8.Core.GlobalRegistry.SaveRuntime != null)
                Hecton8.Core.GlobalRegistry.SaveRuntime.Unregister(this);

            AtlasSignalEvents.Unregister(this);
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterService();

            if (Instance == this)
                Instance = null;
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            if (_conditionMet || _endingComplete) return;

            float depth = _survivalSystem != null ? _survivalSystem.Depth : 0f;
            if (depth < requiredDepth) return;

            AtlasSignalSystem signal = Hecton8.Core.GlobalRegistry.AtlasSignal;
            if (signal == null) return;
            if (signal.CurrentStrength < requiredSignalStrength) return;

            // Условия выполнены
            _conditionMet = true;
            EndingEvents.RaiseConditionMet();

            // Активируем квест
            QuestManager qm = GlobalRegistry.Quest;
            if (qm != null && !string.IsNullOrEmpty(endingQuestId))
                qm.ActivateQuest(endingQuestId);

            NarrativeEvents.RaiseDiscoveryMade("atlas6_core_reached");

            NotificationEvents.PushWarning(ResolveLocalized(
                LocalizationKeys.ENDING_CORE_REACHED,
                "ATLAS-6 CORE DETECTED. TERMINAL ACTIVE. SELECT AN ACTION."));

            LogEndingConditionMet();
        }

        private void TryRegister()
        {
            if (_registered)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registered = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying || Instance != this)
                return;

            Hecton8.Core.GlobalRegistry.RegisterEndingRuntime(this);
            _serviceRegistered = ReferenceEquals(Hecton8.Core.GlobalRegistry.Ending, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            Hecton8.Core.GlobalRegistry.UnregisterEndingRuntime(this);
            _serviceRegistered = false;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — ВЫБОР КОНЦОВКИ
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Игрок выбрал концовку. Вызывается из UI терминала ядра.
        /// </summary>
        public void ForceConditionMetFromQuestDAG()
        {
            if (_conditionMet || _endingComplete)
                return;

            _conditionMet = true;
            EndingEvents.RaiseConditionMet();
            NarrativeEvents.RaiseDiscoveryMade("atlas6_core_data_accessed");
        }

        public void ChooseEnding(EndingChoice choice)
        {
            if (!CanChooseEnding)
            {
                LogInvalidEndingChoice(_conditionMet, _endingComplete);
                return;
            }

            if (choice == EndingChoice.None) return;

            _chosenEnding = choice;
            EndingEvents.RaiseChosen(choice);

            ExecuteEnding(choice);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — КОНЦОВКИ
        // ══════════════════════════════════════════════════════════

        private void ExecuteEnding(EndingChoice choice)
        {
            switch (choice)
            {
                case EndingChoice.ShutDown:
                    ExecuteShutDown();
                    break;

                case EndingChoice.Leave:
                    ExecuteLeave();
                    break;

                case EndingChoice.Amplify:
                    ExecuteAmplify();
                    break;
            }

            // Завершаем квест
            QuestManager qm = GlobalRegistry.Quest;
            if (qm != null && !string.IsNullOrEmpty(endingQuestId))
                qm.CompleteQuest(endingQuestId);

            _endingComplete = true;
            EndingEvents.RaiseSequenceComplete(choice);

            LogEndingChoiceExecuted(choice);
        }

        private void ExecuteShutDown()
        {
            // Атлас-6 выключен. Сигнал прекращается.
            AtlasSignalSystem signal = Hecton8.Core.GlobalRegistry.AtlasSignal;
            if (signal != null)
                signal.DecodeSignal("atlas6_shutdown");

            Atlas6DirectiveSystem directive = Hecton8.Core.GlobalRegistry.Atlas6Directive;
            if (directive != null)
                directive.RegisterBarterTransaction(); // Корпорация получила что хотела

            NarrativeEvents.RaiseDiscoveryMade("ending_shutdown");

            NotificationEvents.PushWarning(ResolveLocalized(
                LocalizationKeys.ENDING_SHUTDOWN_COMPLETE,
                "ATLAS-6 SHUT DOWN. SIGNAL TERMINATED. THE CORPORATION WILL GET THE DATA. TERRAFORMING CONTINUES."));
        }

        private void ExecuteLeave()
        {
            // Атлас-6 продолжает работу. Сигнал активен.
            NarrativeEvents.RaiseDiscoveryMade("ending_leave");

            NotificationEvents.PushInfo(ResolveLocalized(
                LocalizationKeys.ENDING_LEAVE_COMPLETE,
                "ATLAS-6 REMAINS ACTIVE. SIGNAL LIVE. LIFE IS PROTECTED - UNTIL THE SIGNAL IS FOUND."));
        }

        private void ExecuteAmplify()
        {
            // Сигнал усилен — публичный. Атлас-6 выключается сам.
            AtlasSignalSystem signal = Hecton8.Core.GlobalRegistry.AtlasSignal;
            if (signal != null)
                signal.DecodeSignal("atlas6_amplified_public");

            NarrativeEvents.RaiseDiscoveryMade("ending_amplify");

            // Публикуем в шейдер — максимальная интенсивность сигнала
            Shader.SetGlobalFloat(
                Shader.PropertyToID("_AtlasSignalStrength"), 1f);

            NotificationEvents.PushWarning(ResolveLocalized(
                LocalizationKeys.ENDING_AMPLIFY_COMPLETE,
                "SIGNAL AMPLIFIED. THE WHOLE SECTOR CAN HEAR IT. ATLAS-6 IS ENDING THE PROGRAM. THE TRUTH IS OUT. CONSEQUENCES UNPREDICTABLE."));
        }

        private bool ResolveSurvivalSystem()
        {
            if (_survivalSystem != null)
                return true;

            if (!SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) ||
                playerTransform == null)
            {
                return false;
            }

            return playerTransform.TryGetComponent(out _survivalSystem);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogInvalidEndingChoice(bool conditionMet, bool endingComplete)
        {
            Debug.LogWarning($"[Ending] Cannot choose ending: conditionMet={conditionMet}, complete={endingComplete}");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogEndingChoiceExecuted(EndingChoice choice)
        {
            Debug.Log($"[Ending] Choice executed: {choice}");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogEndingConditionMet()
        {
            Debug.Log("[Ending] Condition met — player at Atlas-6 core.");
        }

        public void OnAtlasSignalEvent(in AtlasSignalEventPayload payload)
        {
            if ((AtlasSignalEventType)payload.EventType == AtlasSignalEventType.Decoded)
                HandleSignalDecoded(string.Empty);
        }

        private void HandleSignalDecoded(string messageId)
        {
            // Полная расшифровка — условие может быть выполнено
            // SlowTick проверит глубину на следующем тике
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
            data.endingChoice   = (int)_chosenEnding;
            data.endingComplete = _endingComplete;
            data.endingConditionMet = _conditionMet;
        }

        public void LoadFromSaveData(SaveData data)
        {
            if (data == null) return;
            _chosenEnding   = (EndingChoice)data.endingChoice;
            _endingComplete = data.endingComplete;
            _conditionMet   = data.endingConditionMet;
        }
    }
}
