// ============================================================================
// HECTON-8 — EclipseGameplaySystem.cs
// Геймплейные последствия Великого Затмения.
//
// ЛОР (лор1):
//   • Температура падает на 8°C за минуту
//   • Ночные хищники поднимаются из глубины
//   • Биолюминесценция усиливается
//   • Бездонник поднимается до 200-300м
//   • Planet-shine — единственное освещение
//
// АРХИТЕКТУРА:
//   • Слушает CelestialEvents eclipse start/end lane.
//   • Публикует события для HUD, атмосферы, фауны.
//   • ISlowTickable — температурный дрейф во время затмения.
//   • Интегрируется с HectonAtmosphereManager через событие.
//
// ZERO GC:
//   • Никаких new/LINQ в SlowTick.
//   • Static events для decoupled уведомлений.
// ============================================================================

using System;
using Conditional = System.Diagnostics.ConditionalAttribute;
using Hecton.Localization;
using Hecton8.Celestial;
using Hecton8.Core;
using Hecton8.UI;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Main-thread listener for deferred eclipse gameplay events.
    /// </summary>
    public interface IEclipseGameplayEventListener
    {
        /// <summary>Called when eclipse gameplay phase changes.</summary>
        void OnEclipseGameplayPhaseChanged(bool active);

        /// <summary>Called when night predator rise pressure changes.</summary>
        void OnNightPredatorsRising(float intensity);

        /// <summary>Called when eclipse temperature delta changes.</summary>
        void OnEclipseTemperatureDelta(float delta);
    }

    /// <summary>
    /// Queue-backed eclipse gameplay event lane.
    /// </summary>
    public static class EclipseGameplayEvents
    {
        private struct EclipseGameplayEventPayload
        {
            public byte EventType;
            public byte BoolValue;
            public float Value;
        }

        private const byte PhaseChangedEventType = 1;
        private const byte NightPredatorsRisingEventType = 2;
        private const byte TemperatureDeltaEventType = 3;
        private const int ExpectedPendingEventCapacity = 8;
        private const int ListenerCapacity = 8;

        private static readonly RegistryBucket<IEclipseGameplayEventListener> _listeners = new RegistryBucket<IEclipseGameplayEventListener>(ListenerCapacity);
        private static NativeQueue<EclipseGameplayEventPayload> _pendingEvents;
        private static NativeQueue<EclipseGameplayEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;

        /// <summary>
        /// Number of queued eclipse gameplay payloads awaiting dispatch.
        /// </summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(EclipseGameplayEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(EclipseGameplayEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
        }

        /// <summary>
        /// Registers a main-thread eclipse gameplay listener.
        /// </summary>
        public static void Register(IEclipseGameplayEventListener listener)
        {
            if (listener != null && !_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        /// <summary>
        /// Unregisters a main-thread eclipse gameplay listener.
        /// </summary>
        public static void Unregister(IEclipseGameplayEventListener listener)
        {
            if (listener != null && _listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        /// <summary>Queues an eclipse phase change.</summary>
        public static void RaisePhaseChanged(bool active)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= ExpectedPendingEventCapacity)
                return;

            Enqueue(new EclipseGameplayEventPayload
            {
                EventType = PhaseChangedEventType,
                BoolValue = active ? (byte)1 : (byte)0
            });
        }

        /// <summary>Queues night predator rise pressure.</summary>
        public static void RaiseNightPredatorsRising(float intensity)
        {
            EnqueueValue(NightPredatorsRisingEventType, intensity);
        }

        /// <summary>Queues eclipse temperature delta.</summary>
        public static void RaiseTemperatureDelta(float delta)
        {
            EnqueueValue(TemperatureDeltaEventType, delta);
        }

        /// <summary>
        /// Flushes queued eclipse gameplay events on the main thread.
        /// </summary>
        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated)
                return;

            PromoteNextFrameEventsIfFrontEmpty();
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : ExpectedPendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out EclipseGameplayEventPayload payload))
                    break;

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
                }
            }

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        private static void EnqueueValue(byte eventType, float value)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= ExpectedPendingEventCapacity)
                return;

            Enqueue(new EclipseGameplayEventPayload
            {
                EventType = eventType,
                Value = value
            });
        }

        private static void Enqueue(in EclipseGameplayEventPayload payload)
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

        private static void Dispatch(in EclipseGameplayEventPayload payload)
        {
            IEclipseGameplayEventListener[] rawListeners = _listeners.RawArray;
            int listenerCount = _listeners.Count;
            for (int i = listenerCount - 1; i >= 0; i--)
            {
                IEclipseGameplayEventListener listener = rawListeners[i];
                if (listener == null)
                    continue;

                switch (payload.EventType)
                {
                    case PhaseChangedEventType:
                        listener.OnEclipseGameplayPhaseChanged(payload.BoolValue != 0);
                        break;
                    case NightPredatorsRisingEventType:
                        listener.OnNightPredatorsRising(payload.Value);
                        break;
                    case TemperatureDeltaEventType:
                        listener.OnEclipseTemperatureDelta(payload.Value);
                        break;
                }
            }
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<EclipseGameplayEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<EclipseGameplayEventPayload>[8] - deferred eclipse gameplay lane flushed by SystemDispatcher - owner: EclipseGameplayEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    ExpectedPendingEventCapacity,
                    nameof(EclipseGameplayEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<EclipseGameplayEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<EclipseGameplayEventPayload>[8] - next-frame eclipse gameplay lane prevents same-frame reentrant dispatch - owner: EclipseGameplayEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    ExpectedPendingEventCapacity,
                    nameof(EclipseGameplayEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
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

            NativeQueue<EclipseGameplayEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class EclipseGameplaySystem : MonoBehaviour, ISlowTickable, ICelestialEventListener
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Temperature ─────────────────────────────")]
        [Tooltip("Скорость падения температуры во время затмения (°C/сек).")]
        [SerializeField] private float temperatureCoolRate = 0.133f; // 8°C/мин = 0.133°C/сек

        [Tooltip("Максимальное падение температуры за одно затмение (°C).")]
        [SerializeField] private float maxTemperatureDrop = 8f;

        [Tooltip("Скорость восстановления температуры после затмения (°C/сек).")]
        [SerializeField] private float temperatureRecoveryRate = 0.05f;

        [Header("── Night Predators ──────────────────────────")]
        [Tooltip("Задержка перед подъёмом ночных хищников (сек после начала затмения).")]
        [SerializeField] private float predatorRiseDelay = 60f;

        [Tooltip("Интенсивность подъёма хищников [0..1].")]
        [SerializeField, Range(0f, 1f)] private float predatorRiseIntensity = 0.7f;

        [Header("── Bioluminescence ────────────────────────")]
        [Tooltip("Множитель биолюминесценции во время затмения.")]
        [SerializeField] private float biolumMultiplier = 1.5f;

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        public static EclipseGameplaySystem Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Instance = null;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private bool  _eclipseActive;
        private float _eclipseTimer;
        private float _currentTempDrop;
        private bool  _predatorsRisen;
        private bool  _registered;

        private static readonly int _ShaderBiolumMultiplier =
            Shader.PropertyToID("_EclipseBiolumMultiplier");

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public bool IsEclipseActive => _eclipseActive;
        public float CurrentTempDrop => _currentTempDrop;
        public float EclipseProgress => _eclipseActive && maxTemperatureDrop > 0f
            ? _currentTempDrop / maxTemperatureDrop
            : 0f;

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
            CelestialEvents.Register(this);
        }

        private void OnDisable()
        {
            TryUnregister();
            CelestialEvents.Unregister(this);
        }

        private void OnDestroy()
        {
            TryUnregister();
            CelestialEvents.Unregister(this);

            if (Instance == this)
                Instance = null;
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            const float dt = 0.5f; // SlowTick interval

            if (_eclipseActive)
            {
                _eclipseTimer += dt;

                // Температурный дрейф
                if (_currentTempDrop < maxTemperatureDrop)
                {
                    float newDrop = Mathf.Min(maxTemperatureDrop,
                        _currentTempDrop + temperatureCoolRate * dt);

                    if (newDrop > _currentTempDrop)
                    {
                        _currentTempDrop = newDrop;
                        EclipseGameplayEvents.RaiseTemperatureDelta(-_currentTempDrop);
                    }
                }

                // Ночные хищники поднимаются после задержки
                if (!_predatorsRisen && _eclipseTimer >= predatorRiseDelay)
                {
                    _predatorsRisen = true;
                    EclipseGameplayEvents.RaiseNightPredatorsRising(predatorRiseIntensity);

                    LogNightPredatorsRising(predatorRiseIntensity);
                }
            }
            else
            {
                // Восстановление температуры
                if (_currentTempDrop > 0f)
                {
                    _currentTempDrop = Mathf.Max(0f,
                        _currentTempDrop - temperatureRecoveryRate * dt);
                    EclipseGameplayEvents.RaiseTemperatureDelta(-_currentTempDrop);
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  EVENT HANDLERS
        // ══════════════════════════════════════════════════════════

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogNightPredatorsRising(float intensity)
        {
            Debug.Log($"[Eclipse] Night predators rising! Intensity: {intensity:F2}");
        }

        private void HandleEclipseStart()
        {
            _eclipseActive = true;
            _eclipseTimer  = 0f;
            _predatorsRisen = false;

            EclipseGameplayEvents.RaisePhaseChanged(true);
            NotificationEvents.PushWarning(ResolveLocalized(
                LocalizationKeys.ECLIPSE_EVENT_STARTED,
                "GREAT ECLIPSE - TEMPERATURE FALLING. NIGHT PREDATORS ASCENDING."));

            // Биолюминесценция усиливается
            Shader.SetGlobalFloat(_ShaderBiolumMultiplier, biolumMultiplier);

            LogEclipseStarted();
        }

        private void HandleEclipseEnd()
        {
            _eclipseActive = false;

            EclipseGameplayEvents.RaisePhaseChanged(false);
            NotificationEvents.PushInfo(ResolveLocalized(
                LocalizationKeys.ECLIPSE_EVENT_ENDED,
                "ECLIPSE ENDED - TEMPERATURE RECOVERING."));

            // Биолюминесценция возвращается к норме
            Shader.SetGlobalFloat(_ShaderBiolumMultiplier, 1f);

            LogEclipseEnded();
        }

        void ICelestialEventListener.OnCelestialEclipseStarted()
        {
            HandleEclipseStart();
        }

        void ICelestialEventListener.OnCelestialEclipseEnded()
        {
            HandleEclipseEnd();
        }

        void ICelestialEventListener.OnCelestialSunAngleChanged(float angleDegrees)
        {
        }

        void ICelestialEventListener.OnCelestialPlanetPhaseChanged(float phase)
        {
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogEclipseStarted()
        {
            Debug.Log("[Eclipse] Eclipse started — gameplay consequences active.");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogEclipseEnded()
        {
            Debug.Log("[Eclipse] Eclipse ended — temperature recovering.");
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

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }
    }
}
