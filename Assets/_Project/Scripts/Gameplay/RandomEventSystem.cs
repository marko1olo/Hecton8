// ============================================================================
// HECTON-8 — RandomEventSystem.cs
// Система случайных событий мира.
//
// ЛОР (лор3 Блок 16 — Random Event Table):
//   • Биолюминесцентный шторм: глубина > 1000м, видимость +30%, привлечение фауны
//   • Термальный выброс: рифтовая зона, урон оборудованию, редкие минералы
//   • Миграция стаи: любой биом, изменение поведения фауны
//   • Сбой Hecton-OS: радиация/глубина, глитчи интерфейса
//   • Обрушение пещеры: воксельная зона, блокировка пути, новый лут
//
// АРХИТЕКТУРА:
//   • ISlowTickable — проверка условий раз в 0.5с.
//   • Каждое событие: условия, частота, эффект.
//   • Публикует события через RandomEventEvents.
//   • Интегрируется с HectonDirectorAI (tension modifier).
//
// ZERO GC:
//   • Pre-allocated массив состояний событий.
//   • Никаких new/LINQ в SlowTick.
// ============================================================================

using Hecton.Localization;
using Hecton8.Atmosphere;
using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Physics;
using Hecton8.UI;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    public readonly struct SeismicShockwaveEvent
    {
        public readonly Vector3 EpicenterWS;
        public readonly float ImpulseRadiusMeters;
        public readonly float ImpulseMagnitude;
        public readonly int AppliedStampCount;
        public readonly Vector3 AupStart;
        public readonly Vector3 AupEnd;
        private readonly byte _hasAupLineSegment;

        public bool HasAupLineSegment => _hasAupLineSegment != 0;

        public SeismicShockwaveEvent(
            Vector3 epicenterWS,
            float impulseRadiusMeters,
            float impulseMagnitude,
            int appliedStampCount)
            : this(
                epicenterWS,
                impulseRadiusMeters,
                impulseMagnitude,
                appliedStampCount,
                Vector3.zero,
                Vector3.zero,
                false)
        {
        }

        public SeismicShockwaveEvent(
            Vector3 epicenterWS,
            float impulseRadiusMeters,
            float impulseMagnitude,
            int appliedStampCount,
            Vector3 aupStart,
            Vector3 aupEnd)
            : this(
                epicenterWS,
                impulseRadiusMeters,
                impulseMagnitude,
                appliedStampCount,
                aupStart,
                aupEnd,
                true)
        {
        }

        private SeismicShockwaveEvent(
            Vector3 epicenterWS,
            float impulseRadiusMeters,
            float impulseMagnitude,
            int appliedStampCount,
            Vector3 aupStart,
            Vector3 aupEnd,
            bool hasAupLineSegment)
        {
            EpicenterWS = epicenterWS;
            ImpulseRadiusMeters = impulseRadiusMeters;
            ImpulseMagnitude = impulseMagnitude;
            AppliedStampCount = appliedStampCount;
            AupStart = aupStart;
            AupEnd = aupEnd;
            _hasAupLineSegment = hasAupLineSegment ? (byte)1 : (byte)0;
        }
    }

    public enum RandomEventType
    {
        BiolumStorm     = 0,   // Биолюминесцентный шторм
        ThermalEruption = 1,   // Термальный выброс
        FaunaMigration  = 2,   // Миграция стаи
        HectonOSGlitch  = 3,   // Сбой Hecton-OS
        CaveCollapse    = 4,   // Обрушение пещеры
        MeteorShower    = 5,   // Meteor shower
        SolarFlare      = 6    // Solar EMP flare
    }

    /// <summary>
    /// Deferred payload for random-event activation.
    /// </summary>
    public struct RandomEventStartedPayload
    {
        /// <summary>Activated random-event type.</summary>
        public RandomEventType Type;

        /// <summary>Normalized authored event intensity.</summary>
        public float Intensity;
    }

    /// <summary>
    /// Listener contract for queue-backed random world events.
    /// </summary>
    public interface IRandomEventListener
    {
        /// <summary>Called when a random event starts.</summary>
        /// <param name="type">Activated event type.</param>
        /// <param name="intensity">Normalized event intensity.</param>
        void OnRandomEventStarted(RandomEventType type, float intensity);

        /// <summary>Called when a random event ends.</summary>
        /// <param name="type">Ended event type.</param>
        void OnRandomEventEnded(RandomEventType type);

        /// <summary>Called after a seismic shockwave has been queued and flushed.</summary>
        /// <param name="payload">Seismic payload.</param>
        void OnSeismicShockwave(in SeismicShockwaveEvent payload);
    }

    public static class RandomEventEvents
    {
        private const int ListenerCapacity = 16;
        private const int PendingStartedCapacity = 16;
        private const int PendingEndedCapacity = 16;
        private const int PendingSeismicShockwaveCapacity = 8;

        // COLD ALLOC: RegistryBucket<IRandomEventListener>[16] - deferred random event listeners - owner: RandomEventEvents
        private static readonly RegistryBucket<IRandomEventListener> _listeners = new RegistryBucket<IRandomEventListener>(ListenerCapacity);
        private static NativeQueue<RandomEventStartedPayload> _pendingStarted;
        private static NativeQueue<RandomEventStartedPayload> _nextFrameStarted;
        private static NativeQueue<RandomEventType> _pendingEnded;
        private static NativeQueue<RandomEventType> _nextFrameEnded;
        private static NativeQueue<SeismicShockwaveEvent> _pendingSeismicShockwaves;
        private static NativeQueue<SeismicShockwaveEvent> _nextFrameSeismicShockwaves;
        private static int _pendingStartedCount;
        private static int _nextFrameStartedCount;
        private static int _pendingEndedCount;
        private static int _nextFrameEndedCount;
        private static int _pendingSeismicShockwaveCount;
        private static int _nextFrameSeismicShockwaveCount;
        private static bool _isDispatching;

        public static int PendingCount
        {
            get
            {
                return _pendingStartedCount
                    + _nextFrameStartedCount
                    + _pendingEndedCount
                    + _nextFrameEndedCount
                    + _pendingSeismicShockwaveCount
                    + _nextFrameSeismicShockwaveCount;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingStarted.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(RandomEventEvents), nameof(_pendingStarted));
                _pendingStarted.Dispose();
                _pendingStarted = default;
            }

            if (_nextFrameStarted.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(RandomEventEvents), nameof(_nextFrameStarted));
                _nextFrameStarted.Dispose();
                _nextFrameStarted = default;
            }

            if (_pendingEnded.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(RandomEventEvents), nameof(_pendingEnded));
                _pendingEnded.Dispose();
                _pendingEnded = default;
            }

            if (_nextFrameEnded.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(RandomEventEvents), nameof(_nextFrameEnded));
                _nextFrameEnded.Dispose();
                _nextFrameEnded = default;
            }

            if (_pendingSeismicShockwaves.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(RandomEventEvents), nameof(_pendingSeismicShockwaves));
                _pendingSeismicShockwaves.Dispose();
                _pendingSeismicShockwaves = default;
            }

            if (_nextFrameSeismicShockwaves.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(RandomEventEvents), nameof(_nextFrameSeismicShockwaves));
                _nextFrameSeismicShockwaves.Dispose();
                _nextFrameSeismicShockwaves = default;
            }

            _pendingStartedCount = 0;
            _nextFrameStartedCount = 0;
            _pendingEndedCount = 0;
            _nextFrameEndedCount = 0;
            _pendingSeismicShockwaveCount = 0;
            _nextFrameSeismicShockwaveCount = 0;
            _isDispatching = false;
            _listeners.Clear();
        }

        public static void Register(IRandomEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        public static void Unregister(IRandomEventListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        public static void FlushPending()
        {
            bool completed = false;
            _isDispatching = true;
            try
            {
                if (_listeners.Count <= 0)
                {
                    completed = DrainWithoutDispatch();
                }
                else
                {
                    completed = FlushStarted();
                    if (completed)
                        completed = FlushEnded();
                    if (completed)
                        completed = FlushSeismicShockwaves();
                }
            }
            finally
            {
                _isDispatching = false;
            }

            if (!completed || HasPendingFrontEvents())
                return;

            PromoteNextFrameEvents();
        }

        public static void RaiseStarted(RandomEventType type, float intensity)
        {
            EnsureInitialized();
            if (_pendingStartedCount + _nextFrameStartedCount >= PendingStartedCapacity)
                return;

            RandomEventStartedPayload payload = new RandomEventStartedPayload
            {
                Type = type,
                Intensity = intensity
            };

            if (_isDispatching)
            {
                _nextFrameStarted.Enqueue(payload);
                _nextFrameStartedCount++;
            }
            else
            {
                _pendingStarted.Enqueue(payload);
                _pendingStartedCount++;
            }
        }

        public static void RaiseEnded(RandomEventType type)
        {
            EnsureInitialized();
            if (_pendingEndedCount + _nextFrameEndedCount >= PendingEndedCapacity)
                return;

            if (_isDispatching)
            {
                _nextFrameEnded.Enqueue(type);
                _nextFrameEndedCount++;
            }
            else
            {
                _pendingEnded.Enqueue(type);
                _pendingEndedCount++;
            }
        }

        public static void RaiseSeismicShockwave(in SeismicShockwaveEvent payload)
        {
            PhysicsEventBus.NotifyAcousticPing(new AcousticPingEvent(
                payload.EpicenterWS,
                Mathf.Max(payload.ImpulseRadiusMeters, payload.ImpulseRadiusMeters * 4f),
                Mathf.Clamp01(payload.ImpulseMagnitude / 48f),
                8f,
                FieldTargetRole.HazardProbe,
                0));
            EnsureInitialized();
            if (_pendingSeismicShockwaveCount + _nextFrameSeismicShockwaveCount >= PendingSeismicShockwaveCapacity)
                return;

            if (_isDispatching)
            {
                _nextFrameSeismicShockwaves.Enqueue(payload);
                _nextFrameSeismicShockwaveCount++;
            }
            else
            {
                _pendingSeismicShockwaves.Enqueue(payload);
                _pendingSeismicShockwaveCount++;
            }
        }

        private static void EnsureInitialized()
        {
            if (!_pendingStarted.IsCreated)
            {
                _pendingStarted = new NativeQueue<RandomEventStartedPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<RandomEventStartedPayload>[16] - deferred random-event starts - owner: RandomEventEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingStarted,
                    PendingStartedCapacity,
                    nameof(RandomEventEvents),
                    nameof(_pendingStarted),
                    NativeAllocationLifetime.Session);
            }
            if (!_nextFrameStarted.IsCreated)
            {
                _nextFrameStarted = new NativeQueue<RandomEventStartedPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<RandomEventStartedPayload>[16] - next-frame random-event starts - owner: RandomEventEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameStarted,
                    PendingStartedCapacity,
                    nameof(RandomEventEvents),
                    nameof(_nextFrameStarted),
                    NativeAllocationLifetime.Session);
            }
            if (!_pendingEnded.IsCreated)
            {
                _pendingEnded = new NativeQueue<RandomEventType>(Allocator.Persistent); // COLD ALLOC: NativeQueue<RandomEventType>[16] - deferred random-event ends - owner: RandomEventEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEnded,
                    PendingEndedCapacity,
                    nameof(RandomEventEvents),
                    nameof(_pendingEnded),
                    NativeAllocationLifetime.Session);
            }
            if (!_nextFrameEnded.IsCreated)
            {
                _nextFrameEnded = new NativeQueue<RandomEventType>(Allocator.Persistent); // COLD ALLOC: NativeQueue<RandomEventType>[16] - next-frame random-event ends - owner: RandomEventEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEnded,
                    PendingEndedCapacity,
                    nameof(RandomEventEvents),
                    nameof(_nextFrameEnded),
                    NativeAllocationLifetime.Session);
            }
            if (!_pendingSeismicShockwaves.IsCreated)
            {
                _pendingSeismicShockwaves = new NativeQueue<SeismicShockwaveEvent>(Allocator.Persistent); // COLD ALLOC: NativeQueue<SeismicShockwaveEvent>[8] - deferred seismic shockwaves - owner: RandomEventEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingSeismicShockwaves,
                    PendingSeismicShockwaveCapacity,
                    nameof(RandomEventEvents),
                    nameof(_pendingSeismicShockwaves),
                    NativeAllocationLifetime.Session);
            }
            if (!_nextFrameSeismicShockwaves.IsCreated)
            {
                _nextFrameSeismicShockwaves = new NativeQueue<SeismicShockwaveEvent>(Allocator.Persistent); // COLD ALLOC: NativeQueue<SeismicShockwaveEvent>[8] - next-frame seismic shockwaves - owner: RandomEventEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameSeismicShockwaves,
                    PendingSeismicShockwaveCapacity,
                    nameof(RandomEventEvents),
                    nameof(_nextFrameSeismicShockwaves),
                    NativeAllocationLifetime.Session);
            }
        }

        private static bool FlushStarted()
        {
            if (!_pendingStarted.IsCreated)
                return true;

            int scanBudget = _pendingStartedCount > 0 ? _pendingStartedCount : PendingStartedCapacity;
            while (scanBudget > 0 && !_pendingStarted.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingStarted.TryDequeue(out RandomEventStartedPayload payload))
                    return true;

                _pendingStartedCount--;
                scanBudget--;
                IRandomEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                {
                    IRandomEventListener listener = rawArray[i];
                    if (listener == null)
                        continue;

                    listener.OnRandomEventStarted(payload.Type, payload.Intensity);
                }
            }

            if (_pendingStarted.IsEmpty())
                _pendingStartedCount = 0;

            return true;
        }

        private static bool FlushEnded()
        {
            if (!_pendingEnded.IsCreated)
                return true;

            int scanBudget = _pendingEndedCount > 0 ? _pendingEndedCount : PendingEndedCapacity;
            while (scanBudget > 0 && !_pendingEnded.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingEnded.TryDequeue(out RandomEventType type))
                    return true;

                _pendingEndedCount--;
                scanBudget--;
                IRandomEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                {
                    IRandomEventListener listener = rawArray[i];
                    if (listener == null)
                        continue;

                    listener.OnRandomEventEnded(type);
                }
            }

            if (_pendingEnded.IsEmpty())
                _pendingEndedCount = 0;

            return true;
        }

        private static bool FlushSeismicShockwaves()
        {
            if (!_pendingSeismicShockwaves.IsCreated)
                return true;

            int scanBudget = _pendingSeismicShockwaveCount > 0 ? _pendingSeismicShockwaveCount : PendingSeismicShockwaveCapacity;
            while (scanBudget > 0 && !_pendingSeismicShockwaves.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingSeismicShockwaves.TryDequeue(out SeismicShockwaveEvent payload))
                    return true;

                _pendingSeismicShockwaveCount--;
                scanBudget--;
                IRandomEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                {
                    IRandomEventListener listener = rawArray[i];
                    if (listener == null)
                        continue;

                    listener.OnSeismicShockwave(in payload);
                }
            }

            if (_pendingSeismicShockwaves.IsEmpty())
                _pendingSeismicShockwaveCount = 0;

            return true;
        }

        private static bool DrainWithoutDispatch()
        {
            if (_pendingStarted.IsCreated)
            {
                int scanBudget = _pendingStartedCount > 0 ? _pendingStartedCount : PendingStartedCapacity;
                while (scanBudget > 0 && !_pendingStarted.IsEmpty())
                {
                    if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                        return false;

                    if (!_pendingStarted.TryDequeue(out _))
                        return true;

                    _pendingStartedCount--;
                    scanBudget--;
                }

                if (_pendingStarted.IsEmpty())
                    _pendingStartedCount = 0;
            }

            if (_pendingEnded.IsCreated)
            {
                int scanBudget = _pendingEndedCount > 0 ? _pendingEndedCount : PendingEndedCapacity;
                while (scanBudget > 0 && !_pendingEnded.IsEmpty())
                {
                    if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                        return false;

                    if (!_pendingEnded.TryDequeue(out _))
                        return true;

                    _pendingEndedCount--;
                    scanBudget--;
                }

                if (_pendingEnded.IsEmpty())
                    _pendingEndedCount = 0;
            }

            if (_pendingSeismicShockwaves.IsCreated)
            {
                int scanBudget = _pendingSeismicShockwaveCount > 0 ? _pendingSeismicShockwaveCount : PendingSeismicShockwaveCapacity;
                while (scanBudget > 0 && !_pendingSeismicShockwaves.IsEmpty())
                {
                    if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                        return false;

                    if (!_pendingSeismicShockwaves.TryDequeue(out _))
                        return true;

                    _pendingSeismicShockwaveCount--;
                    scanBudget--;
                }

                if (_pendingSeismicShockwaves.IsEmpty())
                    _pendingSeismicShockwaveCount = 0;
            }

            return true;
        }

        private static bool HasPendingFrontEvents()
        {
            return (_pendingStarted.IsCreated && !_pendingStarted.IsEmpty())
                || (_pendingEnded.IsCreated && !_pendingEnded.IsEmpty())
                || (_pendingSeismicShockwaves.IsCreated && !_pendingSeismicShockwaves.IsEmpty());
        }

        private static void PromoteNextFrameEvents()
        {
            if (_nextFrameStarted.IsCreated)
            {
                while (_nextFrameStartedCount > 0 && _nextFrameStarted.TryDequeue(out RandomEventStartedPayload payload))
                {
                    _nextFrameStartedCount--;
                    _pendingStarted.Enqueue(payload);
                    _pendingStartedCount++;
                }
            }

            if (_nextFrameEnded.IsCreated)
            {
                while (_nextFrameEndedCount > 0 && _nextFrameEnded.TryDequeue(out RandomEventType type))
                {
                    _nextFrameEndedCount--;
                    _pendingEnded.Enqueue(type);
                    _pendingEndedCount++;
                }
            }

            if (_nextFrameSeismicShockwaves.IsCreated)
            {
                while (_nextFrameSeismicShockwaveCount > 0 && _nextFrameSeismicShockwaves.TryDequeue(out SeismicShockwaveEvent payload))
                {
                    _nextFrameSeismicShockwaveCount--;
                    _pendingSeismicShockwaves.Enqueue(payload);
                    _pendingSeismicShockwaveCount++;
                }
            }
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-70)]
    public sealed class RandomEventSystem : MonoBehaviour, ISlowTickable
    {
        public const int EventTypeCount = 7;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── References ──────────────────────────────")]
        [SerializeField] private HectonSurvivalSystem survivalSystem;
        [SerializeField] private HectonVoxelEngine voxelEngine;
        [SerializeField] private TectonicActivityProfile tectonicActivityProfile;

        [Header("── Event Probabilities (per SlowTick) ──────")]
        [SerializeField, Range(0f, 0.01f)] private float biolumStormChance    = 0.001f;
        [SerializeField, Range(0f, 0.01f)] private float thermalEruptionChance = 0.0005f;
        [SerializeField, Range(0f, 0.02f)] private float faunaMigrationChance  = 0.002f;
        [SerializeField, Range(0f, 0.01f)] private float glitchChance          = 0.0008f;
        [SerializeField, Range(0f, 0.005f)] private float caveCollapseChance   = 0.0003f;
        [SerializeField, Range(0f, 0.001f)] private float meteorShowerChance   = 0.00012f;
        [SerializeField, Range(0f, 0.001f)] private float solarFlareChance      = 0.00008f;

        [Header("── Event Durations (seconds) ───────────────")]
        [SerializeField] private float biolumStormDuration    = 120f;
        [SerializeField] private float thermalEruptionDuration = 30f;
        [SerializeField] private float faunaMigrationDuration  = 180f;
        [SerializeField] private float glitchDuration          = 15f;
        [SerializeField] private float caveCollapseDuration    = 5f;
        [SerializeField] private float meteorShowerDuration    = 45f;
        [SerializeField] private float solarFlareDuration      = 30f;

        [Header("── Seismic Collapse ───────────────────────")]
        [SerializeField, Min(4f)] private float seismicTargetRadius = 72f;
        [SerializeField, Range(16, 64)] private int seismicOverlapCapacity = 64;
        [SerializeField, Range(16, 128)] private int seismicUniqueBodyCapacity = 48;

        [Header("── Meteor Shower ─────────────────────────")]
        [SerializeField, Range(0f, 1f)] private float meteorShowerIntensity = 0.82f;
        [SerializeField, Range(0.5f, 8f)] private float meteorShowerFlashRate = 2.1f;
        [SerializeField, Range(0.5f, 8f)] private float meteorShowerFadeSeconds = 3f;
        [SerializeField] private Vector2 meteorShowerSkyDirection = new Vector2(-0.82f, -0.38f);
        [SerializeField, Range(0.02f, 0.45f)] private float meteorShowerStreakLength = 0.18f;
        [SerializeField, Range(0.0005f, 0.02f)] private float meteorShowerStreakWidth = 0.0035f;
        [SerializeField, Range(0f, 1f)] private float meteorBoomFlashThreshold = 0.62f;
        [SerializeField, Range(0f, 1f)] private float meteorBoomIntensity = 0.74f;
        [SerializeField, Range(80f, 800f)] private float meteorBoomLowPassCutoffHz = 260f;
        [SerializeField, Range(4f, 36f)] private float meteorBoomVerticalOffsetMeters = 18f;
        [SerializeField, Range(0f, 32f)] private float meteorBoomHorizontalOffsetMeters = 14f;
        [SerializeField, Range(4f, 96f)] private float meteorWaterImpactRadiusMeters = 42f;
        [SerializeField, Range(0.5f, 12f)] private float meteorWaterImpactDurationSeconds = 5.5f;
        [SerializeField, Range(0f, 1f)] private float meteorWaterImpactEnvelopeThreshold = 0.18f;

        [Header("Solar EMP Flare")]
        [SerializeField, Range(0f, 1f)] private float solarFlareIntensity = 1f;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        // Таймеры активных событий (0 = неактивно)
        // COLD ALLOC: float[EventTypeCount] - active random-event timers - owner: RandomEventSystem
        private readonly float[] _eventTimers = new float[EventTypeCount];
        // COLD ALLOC: Collider[64] - reusable shockwave overlap buffer capped for SlowTick impulse routing - owner: RandomEventSystem
        private readonly Collider[] _seismicOverlapBuffer = new Collider[64];
        // COLD ALLOC: Rigidbody[48] - reusable unique rigidbody buffer for cave-collapse impulse routing - owner: RandomEventSystem
        private readonly Rigidbody[] _seismicBodyBuffer = new Rigidbody[48];
        private bool _registered;
        private bool _registeredRuntime;
        private float _meteorSeed = 99173f;
        private int _meteorLastBoomIndex = -1;
        [SerializeField] private float _debugMeteorFlash;

        // Shader IDs
        private static readonly int _ShaderBiolumStorm  = Shader.PropertyToID("_BiolumStormActive");
        private static readonly int _ShaderGlitchActive = Shader.PropertyToID("_HUDGlitchActive");
        private static readonly int _ShaderMeteorShowerParams = Shader.PropertyToID("_MeteorShowerParams");
        private static readonly int _ShaderMeteorShowerDirection = Shader.PropertyToID("_MeteorShowerDirection");
        private static readonly int _ShaderMeteorWaterImpactPosition = Shader.PropertyToID("_MeteorWaterImpactPosition");
        private static readonly int _ShaderMeteorWaterImpactParams = Shader.PropertyToID("_MeteorWaterImpactParams");

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            TryRegisterRuntime();
            TryRegister();

            ResolveSurvivalSystem();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterRuntime();

            // Сбрасываем все активные события
            for (int i = 0; i < _eventTimers.Length; i++)
            {
                if (_eventTimers[i] > 0f)
                {
                    _eventTimers[i] = 0f;
                    RandomEventEvents.RaiseEnded((RandomEventType)i);
                }
            }

            Shader.SetGlobalFloat(_ShaderBiolumStorm, 0f);
            Shader.SetGlobalFloat(_ShaderGlitchActive, 0f);
            PublishMeteorShowerGlobals(0f, 0f, 0f);
            PublishMeteorWaterImpactGlobals(Vector3.zero, 0f, 0f, 0f);
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterRuntime();
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            if (survivalSystem == null && !ResolveSurvivalSystem())
                return;

            const float dt = 0.5f;
            float depth = survivalSystem != null ? survivalSystem.Depth : 0f;

            // Обновляем таймеры активных событий
            for (int i = 0; i < _eventTimers.Length; i++)
            {
                if (_eventTimers[i] <= 0f) continue;

                _eventTimers[i] -= dt;
                if (_eventTimers[i] <= 0f)
                {
                    _eventTimers[i] = 0f;
                    OnEventEnd((RandomEventType)i);
                }
            }

            // Проверяем условия для новых событий
            if (IsEventActive(RandomEventType.MeteorShower))
                TickMeteorShowerEvent(dt);

            TryTriggerBiolumStorm(depth);
            TryTriggerThermalEruption(depth);
            TryTriggerFaunaMigration();
            TryTriggerGlitch(depth);
            TryTriggerCaveCollapse(depth);
            TryTriggerMeteorShower();
            TryTriggerSolarFlare();
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

        private void TryRegisterRuntime()
        {
            if (_registeredRuntime)
                return;
            if (!Application.isPlaying)
                return;

            GlobalRegistry.RegisterRandomEventRuntime(this);
            _registeredRuntime = GlobalRegistry.RandomEvents == this;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private void TryUnregisterRuntime()
        {
            if (!_registeredRuntime)
                return;

            GlobalRegistry.UnregisterRandomEventRuntime(this);
            _registeredRuntime = false;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        public bool IsEventActive(RandomEventType type)
            => _eventTimers[(int)type] > 0f;

        public float GetEventTimeRemaining(RandomEventType type)
            => Mathf.Max(0f, _eventTimers[(int)type]);

        public static float EvaluateMeteorFlashForSmoke(float eventAgeSeconds, float seed, float flashRate)
        {
            return RandomEventMeteorMath.EvaluateMeteorFlash(eventAgeSeconds, seed, flashRate);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — EVENT TRIGGERS
        // ══════════════════════════════════════════════════════════

        private void TryTriggerBiolumStorm(float depth)
        {
            if (IsEventActive(RandomEventType.BiolumStorm)) return;
            if (depth < 1000f) return;
            if (UnityEngine.Random.value > biolumStormChance) return;

            StartEvent(RandomEventType.BiolumStorm, biolumStormDuration, 0.8f);
            Shader.SetGlobalFloat(_ShaderBiolumStorm, 1f);
            NotificationEvents.PushInfo(ResolveLocalized(
                LocalizationKeys.RANDOM_EVENT_BIOLUM_STORM,
                "BIOLUMINESCENT STORM - VISIBILITY +30%. FAUNA AGITATED."));
        }

        private void TryTriggerThermalEruption(float depth)
        {
            if (IsEventActive(RandomEventType.ThermalEruption)) return;
            if (depth < 3000f) return; // Только в рифтовых зонах
            if (UnityEngine.Random.value > thermalEruptionChance) return;

            StartEvent(RandomEventType.ThermalEruption, thermalEruptionDuration, 1f);
            NotificationEvents.PushWarning(ResolveLocalized(
                LocalizationKeys.RANDOM_EVENT_THERMAL_ERUPTION,
                "THERMAL ERUPTION - BURN HAZARD. RARE MINERALS EXPOSED."));

            // Урон оборудованию
            if (survivalSystem != null)
                survivalSystem.TakeDamage(5f);
        }

        private void TryTriggerFaunaMigration()
        {
            if (IsEventActive(RandomEventType.FaunaMigration)) return;
            if (UnityEngine.Random.value > faunaMigrationChance) return;

            StartEvent(RandomEventType.FaunaMigration, faunaMigrationDuration, 0.5f);
            NotificationEvents.PushInfo(ResolveLocalized(
                LocalizationKeys.RANDOM_EVENT_FAUNA_MIGRATION,
                "PACK MIGRATION - FAUNA BEHAVIOR SHIFT DETECTED."));
        }

        private void TryTriggerGlitch(float depth)
        {
            if (IsEventActive(RandomEventType.HectonOSGlitch)) return;
            if (depth < 500f) return;
            if (UnityEngine.Random.value > glitchChance) return;

            StartEvent(RandomEventType.HectonOSGlitch, glitchDuration, 0.6f);
            Shader.SetGlobalFloat(_ShaderGlitchActive, 1f);
            NotificationEvents.PushWarning(ResolveLocalized(
                LocalizationKeys.RANDOM_EVENT_HECTON_OS_GLITCH,
                "HECTON-OS GLITCH - RADIATION INTERFERENCE. READINGS MAY BE INACCURATE."));
        }

        private void TryTriggerCaveCollapse(float depth)
        {
            if (IsEventActive(RandomEventType.CaveCollapse)) return;
            if (depth < 200f) return;
            if (!TryResolveSeismicContext(
                    out Vector3 playerPosition,
                    out HectonVoxelVolume targetVolume,
                    out TectonicActivityProfile.SeismicEventSettings settings))
            {
                return;
            }

            float resolvedChance = caveCollapseChance * settings.collapseChanceMultiplier;
            if (UnityEngine.Random.value > Mathf.Clamp(resolvedChance, 0f, 1f)) return;
            if (!TryExecuteSeismicShockwave(playerPosition, targetVolume, settings, out SeismicShockwaveEvent seismicEvent))
                return;

            StartEvent(RandomEventType.CaveCollapse, caveCollapseDuration, 1f);
            RandomEventEvents.RaiseSeismicShockwave(in seismicEvent);
            NotificationEvents.PushWarning(ResolveLocalized(
                LocalizationKeys.RANDOM_EVENT_CAVE_COLLAPSE,
                "CAVE COLLAPSE - ROUTE BLOCKED. POSSIBLE NEW OPENING."));
        }

        private void TryTriggerMeteorShower()
        {
            if (IsEventActive(RandomEventType.MeteorShower)) return;
            if (UnityEngine.Random.value > meteorShowerChance) return;

            BeginMeteorShower();
            StartEvent(RandomEventType.MeteorShower, meteorShowerDuration, meteorShowerIntensity);
            NotificationEvents.PushInfo(ResolveLocalized(
                LocalizationKeys.RANDOM_EVENT_METEOR_SHOWER,
                "METEOR SHOWER - SKY FLASHES DETECTED. LOW-FREQUENCY ACOUSTIC BOOMS EXPECTED."));
        }

        private void TryTriggerSolarFlare()
        {
            if (IsEventActive(RandomEventType.SolarFlare)) return;
            if (UnityEngine.Random.value > solarFlareChance) return;

            StartEvent(RandomEventType.SolarFlare, solarFlareDuration, solarFlareIntensity);
            NotificationEvents.PushWarning("SOLAR FLARE - ELECTROMAGNETIC PULSE DETECTED. BASE POWER COLLAPSE EXPECTED.");
        }

        private void StartEvent(RandomEventType type, float duration, float intensity)
        {
            _eventTimers[(int)type] = duration;
            RandomEventEvents.RaiseStarted(type, intensity);

            LogEventStarted(type, duration, intensity);
        }

        private void OnEventEnd(RandomEventType type)
        {
            RandomEventEvents.RaiseEnded(type);

            // Сбрасываем шейдерные эффекты
            switch (type)
            {
                case RandomEventType.BiolumStorm:
                    Shader.SetGlobalFloat(_ShaderBiolumStorm, 0f);
                    break;
                case RandomEventType.HectonOSGlitch:
                    Shader.SetGlobalFloat(_ShaderGlitchActive, 0f);
                    break;
                case RandomEventType.MeteorShower:
                    PublishMeteorShowerGlobals(0f, 0f, 0f);
                    _meteorLastBoomIndex = -1;
                    break;
            }

            LogEventEnded(type);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogEventStarted(RandomEventType type, float duration, float intensity)
        {
            Debug.Log("[RandomEvent] Started");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogEventEnded(RandomEventType type)
        {
            Debug.Log("[RandomEvent] Ended");
        }

        private bool ResolveSurvivalSystem()
        {
            if (survivalSystem != null)
                return true;

            if (!SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) ||
                playerTransform == null)
            {
                return false;
            }

            return playerTransform.TryGetComponent(out survivalSystem);
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }

        private void BeginMeteorShower()
        {
            _meteorSeed = UnityEngine.Random.Range(1, 16777215);
            _meteorLastBoomIndex = -1;
            PublishMeteorShowerGlobals(0f, Mathf.Clamp01(meteorShowerIntensity), 1f);
        }

        private void TickMeteorShowerEvent(float dt)
        {
            float remaining = GetEventTimeRemaining(RandomEventType.MeteorShower);
            float safeDuration = Mathf.Max(0.01f, meteorShowerDuration);
            float eventAge = Mathf.Max(0f, safeDuration - remaining);
            float fadeWindow = Mathf.Max(0.01f, meteorShowerFadeSeconds);
            float fadeIn = Mathf.Clamp01(eventAge / fadeWindow);
            float fadeOut = Mathf.Clamp01(remaining / fadeWindow);
            float envelope = Mathf.Clamp01(meteorShowerIntensity) * Mathf.Min(fadeIn, fadeOut);
            float flash = EvaluateMeteorFlashForSmoke(eventAge, _meteorSeed, meteorShowerFlashRate);
            _debugMeteorFlash = flash;
            PublishMeteorShowerGlobals(eventAge, envelope, flash);
            TryPublishMeteorBoom(eventAge, flash, envelope);
        }

        private void PublishMeteorShowerGlobals(float eventAge, float intensity, float flash)
        {
            Vector2 skyDirection = ResolveMeteorSkyDirection();
            Shader.SetGlobalVector(
                _ShaderMeteorShowerParams,
                new Vector4(
                    Mathf.Clamp01(intensity),
                    _meteorSeed,
                    Mathf.Clamp01(flash),
                    Mathf.Max(0f, eventAge)));
            Shader.SetGlobalVector(
                _ShaderMeteorShowerDirection,
                new Vector4(
                    skyDirection.x,
                    skyDirection.y,
                    Mathf.Max(0.02f, meteorShowerStreakLength),
                    Mathf.Max(0.0005f, meteorShowerStreakWidth)));
        }

        private Vector2 ResolveMeteorSkyDirection()
        {
            Vector2 direction = meteorShowerSkyDirection;
            float magnitudeSqr = direction.sqrMagnitude;
            if (magnitudeSqr < 0.0001f)
                direction = new Vector2(-0.82f, -0.38f);
            else
                direction /= Mathf.Sqrt(magnitudeSqr);

            return direction;
        }

        private void TryPublishMeteorBoom(float eventAge, float flash, float envelope)
        {
            if (flash < meteorBoomFlashThreshold || envelope <= 0.001f)
                return;

            int boomIndex = Mathf.FloorToInt(eventAge * Mathf.Max(0.1f, meteorShowerFlashRate));
            if (boomIndex == _meteorLastBoomIndex)
                return;

            _meteorLastBoomIndex = boomIndex;
            if (!(GlobalRegistry.Audio is SpatialAudioManager spatialAudioManager))
                return;

            if (!SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) || playerTransform == null)
                return;

            Vector3 sourcePosition = ResolveMeteorBoomPosition(playerTransform.position, boomIndex);
            spatialAudioManager.PlayMeteorShowerBoom(
                sourcePosition,
                Mathf.Clamp01(flash * envelope * meteorBoomIntensity),
                meteorBoomLowPassCutoffHz);
            TryPublishMeteorWaterImpact(sourcePosition, flash, envelope);
        }

        private Vector3 ResolveMeteorBoomPosition(Vector3 playerPosition, int boomIndex)
        {
            float angle = RandomEventMeteorMath.Hash01(unchecked((uint)boomIndex), unchecked((uint)Mathf.RoundToInt(_meteorSeed))) * Mathf.PI * 2f;
            Vector3 horizontal = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            return playerPosition
                 + horizontal * Mathf.Max(0f, meteorBoomHorizontalOffsetMeters)
                 + Vector3.up * Mathf.Max(4f, meteorBoomVerticalOffsetMeters);
        }

        private void TryPublishMeteorWaterImpact(Vector3 meteorSourcePosition, float flash, float envelope)
        {
            float impactEnvelope = Mathf.Clamp01(flash * envelope);
            if (impactEnvelope < meteorWaterImpactEnvelopeThreshold)
                return;

            float seaLevelY = ResolveCurrentSeaLevelY();
            if (meteorSourcePosition.y < seaLevelY)
                return;

            Vector3 impactPosition = new Vector3(meteorSourcePosition.x, seaLevelY, meteorSourcePosition.z);
            float radius = Mathf.Max(4f, meteorWaterImpactRadiusMeters);
            float duration = Mathf.Max(0.5f, meteorWaterImpactDurationSeconds);
            PublishMeteorWaterImpactGlobals(impactPosition, radius, duration, impactEnvelope);
            PublishMeteorSplashFeedback(impactPosition, radius, impactEnvelope);

            SargassumGlobalDragManager sargassumDrag = GlobalRegistry.SargassumDrag;
            if (sargassumDrag != null)
                sargassumDrag.RegisterMassiveDisplacement(impactPosition, radius, duration);
        }

        private static void PublishMeteorSplashFeedback(Vector3 impactPosition, float radius, float intensity)
        {
            Vector3 absoluteUniversePosition = HectonFloatingOrigin.ToAbsoluteUniversePosition(impactPosition);
            float clampedIntensity = Mathf.Clamp01(intensity);
            SplashEvent splashEvent = new SplashEvent
            {
                RuntimePosition = new float3(impactPosition.x, impactPosition.y, impactPosition.z),
                AbsoluteUniversePosition = new float3(
                    absoluteUniversePosition.x,
                    absoluteUniversePosition.y,
                    absoluteUniversePosition.z),
                SurfaceNormal = new float3(0f, 1f, 0f),
                ImpactSpeedMetersPerSecond = Mathf.Lerp(18f, 54f, clampedIntensity),
                KineticEnergyJoules = radius * radius * Mathf.Lerp(480f, 3200f, clampedIntensity),
                SubmersionFactor = 1f,
                SampleIndex = -1
            };
            FluidFeedbackEvents.PublishSplashQueued(in splashEvent);
        }

        private static float ResolveCurrentSeaLevelY()
        {
            HectonAtmosphereManager atmosphere = GlobalRegistry.Atmosphere;
            return atmosphere != null ? atmosphere.SeaLevelY : 0f;
        }

        private static void PublishMeteorWaterImpactGlobals(Vector3 impactPosition, float radius, float duration, float intensity)
        {
            Shader.SetGlobalVector(
                _ShaderMeteorWaterImpactPosition,
                new Vector4(impactPosition.x, impactPosition.y, impactPosition.z, Mathf.Clamp01(intensity)));
            Shader.SetGlobalVector(
                _ShaderMeteorWaterImpactParams,
                new Vector4(Mathf.Max(0f, radius), Mathf.Max(0f, duration), Time.time, Mathf.Clamp01(intensity)));
        }

        private bool TryResolveSeismicContext(
            out Vector3 playerPosition,
            out HectonVoxelVolume targetVolume,
            out TectonicActivityProfile.SeismicEventSettings settings)
        {
            playerPosition = default;
            targetVolume = null;
            settings = tectonicActivityProfile != null
                ? tectonicActivityProfile.ResolveSeismicSettings(null, null)
                : default;

            if (!SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) || playerTransform == null)
                return false;

            playerPosition = playerTransform.position;
            if (voxelEngine == null)
                voxelEngine = HectonVoxelEngine.ActiveRuntimeInstance;

            if (voxelEngine == null || !voxelEngine.TryGetNearestActiveVolume(playerPosition, out targetVolume) || targetVolume == null)
                return false;

            float maxTargetRadius = Mathf.Max(4f, seismicTargetRadius);
            if (IsAupDistanceGreater(targetVolume.generationPosition, playerPosition, maxTargetRadius))
                return false;

            string familyId = null;
            string geologyProfileId = null;
            if (targetVolume.TryGetComponent(out WorldGenerativeGeologyVoxelRuntime runtime))
            {
                familyId = runtime.FamilyId;
                geologyProfileId = runtime.GeologyProfileId;
            }

            settings = tectonicActivityProfile != null
                ? tectonicActivityProfile.ResolveSeismicSettings(familyId, geologyProfileId)
                : new TectonicActivityProfile.SeismicEventSettings
                {
                    collapseChanceMultiplier = 1f,
                    stampCountMin = 2,
                    stampCountMax = 4,
                    stampScatterRadius = 18f,
                    ceilingSearchDepth = 18f,
                    craterRadiusMin = 2.5f,
                    craterRadiusMax = 6f,
                    impulseRadius = 100f,
                    impulseMagnitude = 14f
                }.Sanitize();
            return true;
        }

        private bool TryExecuteSeismicShockwave(
            Vector3 playerPosition,
            HectonVoxelVolume targetVolume,
            TectonicActivityProfile.SeismicEventSettings settings,
            out SeismicShockwaveEvent seismicEvent)
        {
            seismicEvent = default;
            if (targetVolume == null)
                return false;

            int stampCount = UnityEngine.Random.Range(settings.stampCountMin, settings.stampCountMax + 1);
            uint stableSeed = unchecked(((uint)Time.frameCount * 2654435761u) ^ (uint)targetVolume.RuntimeStamp);
            if (!targetVolume.TryApplySeismicShockwave(
                    playerPosition,
                    stampCount,
                    settings.stampScatterRadius,
                    settings.ceilingSearchDepth,
                    settings.craterRadiusMin,
                    settings.craterRadiusMax,
                    stableSeed,
                    out int appliedStampCount))
            {
                return false;
            }

            ApplySeismicImpulse(playerPosition, settings.impulseRadius, settings.impulseMagnitude);
            Vector3 epicenterAup = HectonFloatingOrigin.ToAbsoluteUniversePosition(playerPosition);
            Vector3 trenchDirection = ResolveSeismicEventLineDirection(epicenterAup, stableSeed);
            float halfTrenchLength = Mathf.Max(2f, settings.impulseRadius * 0.5f);
            seismicEvent = new SeismicShockwaveEvent(
                playerPosition,
                settings.impulseRadius,
                settings.impulseMagnitude,
                appliedStampCount,
                epicenterAup - trenchDirection * halfTrenchLength,
                epicenterAup + trenchDirection * halfTrenchLength);
            return true;
        }

        private static Vector3 ResolveSeismicEventLineDirection(Vector3 absoluteEpicenter, uint stableSeed)
        {
            uint seedA = unchecked((uint)Mathf.RoundToInt(absoluteEpicenter.x * 0.25f));
            uint seedB = unchecked((uint)Mathf.RoundToInt(absoluteEpicenter.z * 0.25f));
            uint state = seedA * 747796405u + seedB * 2891336453u + stableSeed;
            state ^= state >> 16;
            state *= 2246822519u;
            state ^= state >> 13;
            state *= 3266489917u;
            state ^= state >> 16;

            float angle = (state & 0x00FFFFFFu) * (Mathf.PI * 2f / 16777215f);
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        }

        private void ApplySeismicImpulse(Vector3 epicenter, float radius, float impulseMagnitude)
        {
            int overlapCapacity = Mathf.Clamp(seismicOverlapCapacity, 16, _seismicOverlapBuffer.Length);
            int hitCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                epicenter,
                Mathf.Max(1f, radius),
                _seismicOverlapBuffer,
                HectonLayerMasks.DefaultRaycastLayerMask,
                QueryTriggerInteraction.Ignore);
            if (hitCount <= 0)
                return;

            int uniqueCapacity = Mathf.Clamp(seismicUniqueBodyCapacity, 16, _seismicBodyBuffer.Length);
            int uniqueBodyCount = 0;
            for (int hitIndex = 0; hitIndex < hitCount && hitIndex < overlapCapacity; hitIndex++)
            {
                Collider collider = _seismicOverlapBuffer[hitIndex];
                _seismicOverlapBuffer[hitIndex] = null;
                if (collider == null)
                    continue;

                Rigidbody body = collider.attachedRigidbody;
                if (body == null || body.isKinematic)
                    continue;

                bool duplicate = false;
                for (int bodyIndex = 0; bodyIndex < uniqueBodyCount; bodyIndex++)
                {
                    if (_seismicBodyBuffer[bodyIndex] != body)
                        continue;

                    duplicate = true;
                    break;
                }

                if (duplicate)
                    continue;

                _seismicBodyBuffer[uniqueBodyCount++] = body;
                if (uniqueBodyCount >= uniqueCapacity)
                    break;
            }

            float safeRadius = Mathf.Max(1f, radius);
            for (int bodyIndex = 0; bodyIndex < uniqueBodyCount; bodyIndex++)
            {
                Rigidbody body = _seismicBodyBuffer[bodyIndex];
                _seismicBodyBuffer[bodyIndex] = null;
                if (body == null)
                    continue;

                ResolveAupDirectionAndDistance(epicenter, body.worldCenterOfMass, out Vector3 direction, out float distance);
                if (distance > safeRadius)
                    continue;

                if (distance <= 0.0001f)
                    direction = Vector3.up;
                direction.y = Mathf.Max(direction.y, 0.25f);
                direction.Normalize();

                float distance01 = 1f - Mathf.Clamp01(distance / safeRadius);
                float resolvedImpulse = impulseMagnitude * Mathf.Pow(distance01, 0.65f);
                PhysicsForceRouter.QueueForce(body, direction * resolvedImpulse, ForceMode.Impulse);
            }
        }

        private static bool IsAupDistanceGreater(Vector3 runtimeA, Vector3 runtimeB, float thresholdMeters)
        {
            float safeThreshold = Mathf.Max(0f, thresholdMeters);
            AbsoluteUniversePosition aupA = AbsoluteUniversePosition.FromRuntimePosition(runtimeA);
            AbsoluteUniversePosition aupB = AbsoluteUniversePosition.FromRuntimePosition(runtimeB);
            return AbsoluteUniversePosition.DistanceSq(in aupA, in aupB) > (double)safeThreshold * safeThreshold;
        }

        private static void ResolveAupDirectionAndDistance(
            Vector3 fromRuntime,
            Vector3 toRuntime,
            out Vector3 direction,
            out float distance)
        {
            AbsoluteUniversePosition fromAup = AbsoluteUniversePosition.FromRuntimePosition(fromRuntime);
            AbsoluteUniversePosition toAup = AbsoluteUniversePosition.FromRuntimePosition(toRuntime);
            double3 delta = toAup.ToAbsoluteDouble3() - fromAup.ToAbsoluteDouble3();
            double distanceSq = math.dot(delta, delta);
            double resolvedDistance = math.sqrt(math.max(0d, distanceSq));
            distance = resolvedDistance > float.MaxValue ? float.MaxValue : (float)resolvedDistance;
            direction = resolvedDistance > 0.0001d
                ? new Vector3(
                    (float)(delta.x / resolvedDistance),
                    (float)(delta.y / resolvedDistance),
                    (float)(delta.z / resolvedDistance))
                : Vector3.up;
        }
    }
}
