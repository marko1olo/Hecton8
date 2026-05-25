// ============================================================================
// HECTON-8 — EclipseGameplaySystem.cs
// Geympleynye posledstviya Velikogo Zatmeniya.
//
// LOR (lor1):
//   • Temperatura padaet na 8°C za minutu
//   • Nochnye hischniki podnimayutsya iz glubiny
//   • Biolyuminestsentsiya usilivaetsya
//   • Bezdonnik podnimaetsya do 200-300m
//   • Planet-shine — edinstvennoe osveschenie
//
// ARHITEKTURA:
//   • Slushaet CelestialEvents eclipse start/end lane.
//   • Publikuet sobytiya dlya HUD, atmosfery, fauny.
//   • ISlowTickable — temperaturnyy dreyf vo vremya zatmeniya.
//   • Integriruetsya s HectonAtmosphereManager cherez sobytie.
//
// ZERO GC:
//   • Nikakih new/LINQ v SlowTick.
//   • Static events dlya decoupled uvedomleniy.
// ============================================================================

using System;
using System.Runtime.InteropServices;
using Conditional = System.Diagnostics.ConditionalAttribute;
using Hecton.Localization;
using Hecton8.Audio;
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

        /// <summary>Called when eclipse bioluminescence multiplier changes.</summary>
        void OnEclipseBiolumMultiplierChanged(float multiplier);
    }

    /// <summary>
    /// Queue-backed eclipse gameplay event lane.
    /// </summary>
    public static class EclipseGameplayEvents
    {
        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct EclipseGameplayEventPayload
        {
            [FieldOffset(0)] public byte EventType;
            [FieldOffset(1)] public byte BoolValue;
            [FieldOffset(2)] private ushort _pad0;
            [FieldOffset(4)] public float Value;
            [FieldOffset(8)] private ulong _pad1;
        }

        private const byte PhaseChangedEventType = 1;
        private const byte NightPredatorsRisingEventType = 2;
        private const byte TemperatureDeltaEventType = 3;
        private const byte BiolumMultiplierEventType = 4;
        private const int ExpectedPendingEventCapacity = 16;
        private const int ListenerCapacity = 8;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;

        private struct ListenerSlot
        {
            public IEclipseGameplayEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        // COLD ALLOC: ListenerSlot[8] - eclipse gameplay listeners drained without interface array dispatch - owner: EclipseGameplayEvents
        private static readonly ListenerSlot[] _listeners = new ListenerSlot[ListenerCapacity];
        private static NativeQueue<EclipseGameplayEventPayload> _pendingEvents;
        private static NativeQueue<EclipseGameplayEventPayload> _nextFrameEvents;
        private static int _listenerCount;
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

            for (int i = 0; i < _listenerCount; i++)
                _listeners[i].Clear();

            _listenerCount = 0;
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
        }

        /// <summary>
        /// Registers a main-thread eclipse gameplay listener.
        /// </summary>
        public static void Register(IEclipseGameplayEventListener listener)
        {
            if (listener != null)
                RegisterImmediate(listener);
        }

        /// <summary>
        /// Unregisters a main-thread eclipse gameplay listener.
        /// </summary>
        public static void Unregister(IEclipseGameplayEventListener listener)
        {
            if (listener != null)
                TryUnregisterImmediate(listener);
        }

        /// <summary>Queues an eclipse phase change.</summary>
        public static bool TryRaisePhaseChanged(bool active)
        {
            if (_listenerCount <= 0)
                return false;

            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= ExpectedPendingEventCapacity)
                return false;

            return Enqueue(new EclipseGameplayEventPayload
            {
                EventType = PhaseChangedEventType,
                BoolValue = active ? (byte)1 : (byte)0
            });
        }

        [Obsolete("Use TryRaisePhaseChanged so bounded queue refusal is visible at the producer.", true)]
        public static void RaisePhaseChanged(bool active) => TryRaisePhaseChanged(active);

        /// <summary>Queues night predator rise pressure.</summary>
        public static bool TryRaiseNightPredatorsRising(float intensity)
        {
            return EnqueueValue(NightPredatorsRisingEventType, intensity);
        }

        [Obsolete("Use TryRaiseNightPredatorsRising so bounded queue refusal is visible at the producer.", true)]
        public static void RaiseNightPredatorsRising(float intensity) => TryRaiseNightPredatorsRising(intensity);

        /// <summary>Queues eclipse temperature delta.</summary>
        public static bool TryRaiseTemperatureDelta(float delta)
        {
            return EnqueueValue(TemperatureDeltaEventType, delta);
        }

        [Obsolete("Use TryRaiseTemperatureDelta so bounded queue refusal is visible at the producer.", true)]
        public static void RaiseTemperatureDelta(float delta) => TryRaiseTemperatureDelta(delta);

        /// <summary>Queues eclipse bioluminescence multiplier.</summary>
        public static bool TryRaiseBiolumMultiplierChanged(float multiplier)
        {
            return EnqueueValue(BiolumMultiplierEventType, Mathf.Max(0f, multiplier));
        }

        [Obsolete("Use TryRaiseBiolumMultiplierChanged so bounded queue refusal is visible at the producer.", true)]
        public static void RaiseBiolumMultiplierChanged(float multiplier) => TryRaiseBiolumMultiplierChanged(multiplier);

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
                }
            }

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        private static bool EnqueueValue(byte eventType, float value)
        {
            if (_listenerCount <= 0)
                return false;

            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= ExpectedPendingEventCapacity)
                return false;

            return Enqueue(new EclipseGameplayEventPayload
            {
                EventType = eventType,
                Value = value
            });
        }

        private static bool Enqueue(in EclipseGameplayEventPayload payload)
        {
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

        private static void Dispatch(in EclipseGameplayEventPayload payload)
        {
            int listenerCount = _listenerCount;
            for (int i = listenerCount - 1; i >= 0; i--)
            {
                IEclipseGameplayEventListener listener = _listeners[i].Listener;
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
                    case BiolumMultiplierEventType:
                        listener.OnEclipseBiolumMultiplierChanged(payload.Value);
                        break;
                }
            }
        }

        private static void RegisterImmediate(IEclipseGameplayEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i].Listener, listener))
                    return;
            }

            if (_listenerCount >= ListenerCapacity)
                return;

            _listeners[_listenerCount++].Listener = listener;
        }

        private static bool TryUnregisterImmediate(IEclipseGameplayEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (!ReferenceEquals(_listeners[i].Listener, listener))
                    continue;

                _listenerCount--;
                _listeners[i] = _listeners[_listenerCount];
                _listeners[_listenerCount].Clear();
                return true;
            }

            return false;
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<EclipseGameplayEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<EclipseGameplayEventPayload>[16] — deferred eclipse gameplay lane flushed by SystemDispatcher — owner: EclipseGameplayEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    ExpectedPendingEventCapacity,
                    nameof(EclipseGameplayEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, ExpectedPendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<EclipseGameplayEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<EclipseGameplayEventPayload>[16] — next-frame eclipse gameplay lane prevents same-frame reentrant dispatch — owner: EclipseGameplayEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    ExpectedPendingEventCapacity,
                    nameof(EclipseGameplayEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameEvents, ExpectedPendingEventCapacity);
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

            NativeQueue<EclipseGameplayEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class EclipseGameplaySystem : MonoBehaviour, ISlowTickable, ILateFrameTickable, ICelestialEventListener, IGlobalRegistryHotSwapListener
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Temperature ─────────────────────────────")]
        [Tooltip("Skorost padeniya temperatury vo vremya zatmeniya (°C/sek).")]
        [SerializeField] private float temperatureCoolRate = 0.133f; // 8°C/min = 0.133°C/sek

        [Tooltip("Maksimalnoe padenie temperatury za odno zatmenie (°C).")]
        [SerializeField] private float maxTemperatureDrop = 8f;

        [Tooltip("Skorost vosstanovleniya temperatury posle zatmeniya (°C/sek).")]
        [SerializeField] private float temperatureRecoveryRate = 0.05f;

        [Header("── Night Predators ──────────────────────────")]
        [Tooltip("Zaderzhka pered podemom nochnyh hischnikov (sek posle nachala zatmeniya).")]
        [SerializeField] private float predatorRiseDelay = 60f;

        [Tooltip("Intensivnost podema hischnikov [0..1].")]
        [SerializeField, Range(0f, 1f)] private float predatorRiseIntensity = 0.7f;
        [SerializeField, Min(0f)] private float predatorRiseHoldSeconds = 180f;

        [Header("── Bioluminescence ────────────────────────")]
        [Tooltip("Mnozhitel biolyuminestsentsii vo vremya zatmeniya.")]
        [SerializeField] private float biolumMultiplier = 2f;

        [Header("Eclipse Audio")]
        [SerializeField, Range(-300f, 0f)] private float totalEclipseAcousticPitchShiftCents = -150f;
        [SerializeField, Range(0f, 1f)] private float acousticPitchShiftStartOcclusion = 0.85f;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private bool  _eclipseActive;
        private float _eclipseTimer;
        private float _currentTempDrop;
        private bool  _predatorsRisen;
        private bool  _registered;
        private bool  _registeredLateFrame;
        private bool  _registeredRuntime;
        private bool _hotSwapRegistered;
        private bool  _reportedMissingEcosystemDirector;
        private float _currentBiolumMultiplier = 1f;
        private float _currentAcousticPitchShiftCents;
        private float _pendingBiolumMultiplier = 1f;
        private bool _biolumMultiplierShaderDirty;

        private static readonly uint _EclipseGameplayContextHash =
            unchecked((uint)LocHash.Compute("EclipseGameplaySystem"));
        private static readonly uint _EclipseNoEcosystemDirectorWarningHash =
            unchecked((uint)LocHash.Compute("EclipseGameplay.NoEcosystemDirector"));

        private static readonly int _ShaderBiolumMultiplier =
            Shader.PropertyToID("_EclipseBiolumMultiplier");
        private const float EclipseBiolumBoostOverlapThreshold = 0.8f;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public bool IsEclipseActive => _eclipseActive;
        public float CurrentTempDrop => _currentTempDrop;
        public float CurrentAcousticPitchShiftCents => _currentAcousticPitchShiftCents;
        public float EclipseProgress => _eclipseActive && maxTemperatureDrop > 0f
            ? _currentTempDrop / maxTemperatureDrop
            : 0f;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            TryRegisterRuntime();
            TryRegisterHotSwapListener();
            TryRegister();
            CelestialEvents.Register(this);
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterRuntime();
            TryUnregisterHotSwapListener();
            CelestialEvents.Unregister(this);
            ApplyPredatorShallowMigration(0f, 0f);
            _currentBiolumMultiplier = 1f;
            Shader.SetGlobalFloat(_ShaderBiolumMultiplier, 1f);
            PublishEclipseAcousticPitchShift(0f);
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterRuntime();
            TryUnregisterHotSwapListener();
            CelestialEvents.Unregister(this);
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

                // Temperaturnyy dreyf
                if (_currentTempDrop < maxTemperatureDrop)
                {
                    float newDrop = Mathf.Min(maxTemperatureDrop,
                        _currentTempDrop + temperatureCoolRate * dt);

                    if (newDrop > _currentTempDrop)
                    {
                        _currentTempDrop = newDrop;
                        EclipseGameplayEvents.TryRaiseTemperatureDelta(-_currentTempDrop);
                    }
                }

                // Nochnye hischniki podnimayutsya posle zaderzhki
                if (!_predatorsRisen && _eclipseTimer >= predatorRiseDelay)
                {
                    _predatorsRisen = true;
                    EclipseGameplayEvents.TryRaiseNightPredatorsRising(predatorRiseIntensity);
                    ApplyPredatorShallowMigration(predatorRiseIntensity, predatorRiseHoldSeconds);

                    LogNightPredatorsRising();
                }

                PublishBiolumMultiplier(ResolveTargetBiolumMultiplier());
                PublishEclipseAcousticPitchShift(ResolveTargetAcousticPitchShiftCents());
            }
            else
            {
                // Vosstanovlenie temperatury
                if (_currentTempDrop > 0f)
                {
                    _currentTempDrop = Mathf.Max(0f,
                        _currentTempDrop - temperatureRecoveryRate * dt);
                    EclipseGameplayEvents.TryRaiseTemperatureDelta(-_currentTempDrop);
                }
            }
        }

        private void TryRegisterRuntime()
        {
            if (_registeredRuntime)
                return;
            if (!Application.isPlaying)
                return;

            GlobalRegistry.RegisterEclipseGameplayRuntime(this);
            _registeredRuntime = GlobalRegistry.EclipseGameplay == this;
        }

        private void TryUnregisterRuntime()
        {
            if (!_registeredRuntime)
                return;

            GlobalRegistry.UnregisterEclipseGameplayRuntime(this);
            _registeredRuntime = false;
        }

        // ══════════════════════════════════════════════════════════
        //  EVENT HANDLERS
        // ══════════════════════════════════════════════════════════

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogNightPredatorsRising()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.Log("[Eclipse] Night predators rising.");
#endif
        }

        public void LateFrameTick()
        {
            FlushQueuedBiolumMultiplier();
        }

        private void HandleEclipseStart()
        {
            _eclipseActive = true;
            _eclipseTimer  = 0f;
            _predatorsRisen = false;

            EclipseGameplayEvents.TryRaisePhaseChanged(true);
            NotificationEvents.TryPushWarning(ResolveLocalizedSpan(
                LocalizationKeys.ECLIPSE_EVENT_STARTED,
                "GREAT ECLIPSE - TEMPERATURE FALLING. NIGHT PREDATORS ASCENDING."));

            // Biolyuminestsentsiya usilivaetsya
            PublishBiolumMultiplier(ResolveTargetBiolumMultiplier());
            PublishEclipseAcousticPitchShift(ResolveTargetAcousticPitchShiftCents());

            LogEclipseStarted();
        }

        private void HandleEclipseEnd()
        {
            _eclipseActive = false;

            EclipseGameplayEvents.TryRaisePhaseChanged(false);
            ApplyPredatorShallowMigration(0f, 0f);
            NotificationEvents.TryPushInfo(ResolveLocalizedSpan(
                LocalizationKeys.ECLIPSE_EVENT_ENDED,
                "ECLIPSE ENDED - TEMPERATURE RECOVERING."));

            // Biolyuminestsentsiya vozvraschaetsya k norme
            PublishBiolumMultiplier(1f);
            PublishEclipseAcousticPitchShift(0f);

            LogEclipseEnded();
        }

        private void ApplyPredatorShallowMigration(float intensity01, float holdSeconds)
        {
            IEcosystemDirectorService ecosystemDirector = GlobalRegistry.EcosystemDirector;
            if (ecosystemDirector == null)
            {
                if (intensity01 > 0f)
                    PublishOnce(ref _reportedMissingEcosystemDirector, _EclipseNoEcosystemDirectorWarningHash, intensity01);
                return;
            }

            _reportedMissingEcosystemDirector = false;
            ecosystemDirector.ApplyEclipsePredatorShallowMigration(
                Mathf.Clamp01(intensity01),
                Mathf.Max(0f, holdSeconds));
        }

        private float ResolveTargetBiolumMultiplier()
        {
            if (!_eclipseActive)
                return 1f;

            float occlusion01 = ResolveEclipseOcclusion01();
            float resonanceMultiplier = 1f;
            HectonCelestialEngine celestialEngine = GlobalRegistry.CelestialEngine;
            if (celestialEngine != null && celestialEngine.IsLunarResonanceActive)
                resonanceMultiplier = Mathf.Max(1f, celestialEngine.LunarResonanceBiolumMultiplier);

            if (occlusion01 < EclipseBiolumBoostOverlapThreshold)
                return 1f;

            return Mathf.Max(1f, biolumMultiplier) * resonanceMultiplier;
        }

        private float ResolveTargetAcousticPitchShiftCents()
        {
            if (!_eclipseActive)
                return 0f;

            float occlusion01 = ResolveEclipseOcclusion01();
            float start = Mathf.Clamp(acousticPitchShiftStartOcclusion, 0f, 0.99f);
            float totality01 = Mathf.Clamp01((occlusion01 - start) / Mathf.Max(0.0001f, 1f - start));
            totality01 = totality01 * totality01 * (3f - 2f * totality01);
            return Mathf.Min(0f, totalEclipseAcousticPitchShiftCents) * totality01;
        }

        private static float ResolveEclipseOcclusion01()
        {
            float occlusion01 = 1f;
            HectonCelestialEngine celestialEngine = GlobalRegistry.CelestialEngine;
            if (celestialEngine != null)
            {
                occlusion01 = Mathf.Clamp01(Mathf.Max(
                    celestialEngine.PenumbraFactor,
                    celestialEngine.SunOcclusionFactor));
            }

            return occlusion01;
        }

        private void PublishBiolumMultiplier(float multiplier)
        {
            float clampedMultiplier = Mathf.Max(0f, multiplier);
            if (Mathf.Abs(clampedMultiplier - _currentBiolumMultiplier) <= 0.001f)
                return;

            _currentBiolumMultiplier = clampedMultiplier;
            _pendingBiolumMultiplier = clampedMultiplier;
            _biolumMultiplierShaderDirty = true;
            EclipseGameplayEvents.TryRaiseBiolumMultiplierChanged(clampedMultiplier);
        }

        private void FlushQueuedBiolumMultiplier()
        {
            if (!_biolumMultiplierShaderDirty)
                return;

            _biolumMultiplierShaderDirty = false;
            Shader.SetGlobalFloat(_ShaderBiolumMultiplier, _pendingBiolumMultiplier);
        }

        private void PublishEclipseAcousticPitchShift(float shiftCents)
        {
            float clampedCents = Mathf.Clamp(shiftCents, -300f, 0f);
            if (Mathf.Abs(clampedCents - _currentAcousticPitchShiftCents) <= 0.01f)
                return;

            _currentAcousticPitchShiftCents = clampedCents;
            if (GlobalRegistry.Audio is ISpatialAudioEnvironmentModulationSink spatialAudio)
                spatialAudio.SetEclipseAcousticPitchShiftCents(clampedCents);
        }

        private static void PublishOnce(ref bool latch, uint warningHash, float scalarValue)
        {
            if (latch)
                return;

            latch = true;
            GlobalTelemetryBus.PublishPerformanceWarning(warningHash, _EclipseGameplayContextHash, scalarValue);
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.Log("[Eclipse] Eclipse started — gameplay consequences active.");
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogEclipseEnded()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.Log("[Eclipse] Eclipse ended — temperature recovering.");
#endif
        }

        private void TryRegister()
        {
            if (_registered)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            if (_registered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registered = false;
            }
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher || currentService == null || !isActiveAndEnabled)
                return;

            TryUnregister();
            TryRegister();
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

        private static ReadOnlySpan<char> ResolveLocalizedSpan(string key, string fallback)
        {
            ILocalizationTextReadModel manager = Hecton8.Core.GlobalRegistry.LocalizationText;
            return manager != null
                ? manager.GetRawSpanOrFallback(LocHash.Compute(key), fallback.AsSpan())
                : fallback.AsSpan();
        }
    }
}
