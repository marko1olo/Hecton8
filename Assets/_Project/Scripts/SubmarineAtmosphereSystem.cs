using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.AI;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Crafting;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Narrative;
using Hecton8.Physics;
using Hecton8.Visor;
using Hecton8.World;
using NASAPunk.Visor;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Hecton8.Atmosphere
{
    internal static class DeferredAtmosphereNativeQueueWarmup
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Prewarm<T>(ref NativeQueue<T> queue, int capacity)
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
    }

    internal static class AtmosphereEventPayloadSanitizer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float FiniteNonNegativeOrZero(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float FiniteOrZero(float value)
        {
            return math.isfinite(value) ? value : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector3 RuntimePositionOrZero(Vector3 runtimePosition)
        {
            float3 value = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            return math.all(math.isfinite(value)) ? runtimePosition : Vector3.zero;
        }
    }

    /// <summary>
    /// Pressure discontinuity emitted when a sealed bulkhead opens into unequal room pressures.
    /// </summary>
    public readonly struct HighPressureEvent
    {
        /// <summary>
        /// Creates a high-pressure door-opening payload.
        /// </summary>
        public HighPressureEvent(int doorIndex, int roomA, int roomB, float pressureAKPa, float pressureBKPa, Vector3 runtimePosition)
        {
            float safePressureA = AtmosphereEventPayloadSanitizer.FiniteNonNegativeOrZero(pressureAKPa);
            float safePressureB = AtmosphereEventPayloadSanitizer.FiniteNonNegativeOrZero(pressureBKPa);
            DoorIndex = doorIndex;
            RoomA = roomA;
            RoomB = roomB;
            PressureAKPa = safePressureA;
            PressureBKPa = safePressureB;
            PressureDeltaKPa = math.abs(safePressureA - safePressureB);
            RuntimePosition = AtmosphereEventPayloadSanitizer.RuntimePositionOrZero(runtimePosition);
        }

        /// <summary>Bulkhead edge index inside the compartment graph.</summary>
        public int DoorIndex { get; }

        /// <summary>First room linked by the opened bulkhead.</summary>
        public int RoomA { get; }

        /// <summary>Second room linked by the opened bulkhead.</summary>
        public int RoomB { get; }

        /// <summary>Pressure in room A at the moment of opening.</summary>
        public float PressureAKPa { get; }

        /// <summary>Pressure in room B at the moment of opening.</summary>
        public float PressureBKPa { get; }

        /// <summary>Absolute pressure difference across the opened bulkhead.</summary>
        public float PressureDeltaKPa { get; }

        /// <summary>Runtime-space midpoint for downstream VFX or alarm placement.</summary>
        public Vector3 RuntimePosition { get; }
    }

    /// <summary>
    /// Unmanaged high-pressure warning payload carried by the deferred event lane.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct HighPressureEventPayload
    {
        public Vector3 RuntimePosition;
        public float PressureAKPa;
        public float PressureBKPa;
        public int DoorIndex;
        public int RoomA;
        public int RoomB;
    }

    /// <summary>
    /// Listener for deferred high-pressure warnings.
    /// </summary>
    public interface IHighPressureEventListener
    {
        void OnHighPressure(in HighPressureEvent pressureEvent);
    }

    /// <summary>
    /// NativeQueue-backed high-pressure warning bus for submarine bulkhead events.
    /// </summary>
    public static class HighPressureEvents
    {
        private const int ListenerCapacity = 16;
        private const int PendingEventCapacity = 32;
        private static readonly uint _overflowWarningHash = unchecked((uint)LocHash.Compute("HighPressureEvents.Overflow"));
        private static readonly uint _queueHash = unchecked((uint)LocHash.Compute("HighPressureEvents"));

        // COLD ALLOC: RegistryBucket<IHighPressureEventListener>[16] - high-pressure listeners drained by SystemDispatcher LateUpdate - owner: HighPressureEvents
        private static readonly RegistryBucket<IHighPressureEventListener> _listeners = new RegistryBucket<IHighPressureEventListener>(ListenerCapacity);
        private static NativeQueue<HighPressureEventPayload> _pendingEvents;
        private static NativeQueue<HighPressureEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;
        private static int _lastOverflowWarningFrame = -1;

        /// <summary>Number of high-pressure payloads waiting for late-frame dispatch.</summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(HighPressureEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(HighPressureEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
            _lastOverflowWarningFrame = -1;
        }

        /// <summary>Registers one high-pressure warning listener.</summary>
        public static void Register(IHighPressureEventListener listener)
        {
            if (listener == null)
                return;

            if (!_listeners.Contains(listener))
                _listeners.Register(listener);

            EnsureInitialized();
        }

        /// <summary>Unregisters one high-pressure warning listener.</summary>
        public static void Unregister(IHighPressureEventListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        internal static void Shutdown()
        {
            ResetStaticState();
        }

        /// <summary>Flushes queued high-pressure warnings.</summary>
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

                if (!_pendingEvents.TryDequeue(out HighPressureEventPayload payload))
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

        /// <summary>Emits a high-pressure warning payload.</summary>
        public static void Notify(in HighPressureEvent pressureEvent)
        {
            if (_listeners.Count <= 0)
                return;

            Enqueue(new HighPressureEventPayload
            {
                RuntimePosition = pressureEvent.RuntimePosition,
                PressureAKPa = pressureEvent.PressureAKPa,
                PressureBKPa = pressureEvent.PressureBKPa,
                DoorIndex = pressureEvent.DoorIndex,
                RoomA = pressureEvent.RoomA,
                RoomB = pressureEvent.RoomB
            });
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<HighPressureEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<HighPressureEventPayload>[32] - deferred submarine high-pressure warning lane flushed by SystemDispatcher LateUpdate - owner: HighPressureEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(HighPressureEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                DeferredAtmosphereNativeQueueWarmup.Prewarm(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<HighPressureEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<HighPressureEventPayload>[32] - next-frame high-pressure warning lane prevents same-frame reentrant dispatch - owner: HighPressureEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(HighPressureEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
                DeferredAtmosphereNativeQueueWarmup.Prewarm(ref _nextFrameEvents, PendingEventCapacity);
            }
        }

        private static bool Enqueue(in HighPressureEventPayload payload)
        {
            if (_listeners.Count <= 0)
                return false;

            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportOverflowOncePerFrame();
                return false;
            }

            EnsureInitialized();
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

        private static void Dispatch(in HighPressureEventPayload payload)
        {
            int count = _listeners.Count;
            if (count <= 0)
                return;

            HighPressureEvent pressureEvent = new HighPressureEvent(
                payload.DoorIndex,
                payload.RoomA,
                payload.RoomB,
                payload.PressureAKPa,
                payload.PressureBKPa,
                payload.RuntimePosition);

            IHighPressureEventListener[] rawArray = _listeners.RawArray;
            for (int i = count - 1; i >= 0; i--)
            {
                IHighPressureEventListener listener = rawArray[i];
                if (listener != null)
                    listener.OnHighPressure(in pressureEvent);
            }
        }

        private static void ReportOverflowOncePerFrame()
        {
            int frame = Time.frameCount;
            if (_lastOverflowWarningFrame == frame)
                return;

            _lastOverflowWarningFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(_overflowWarningHash, _queueHash, PendingEventCapacity);
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

            NativeQueue<HighPressureEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }
    }

    /// <summary>
    /// Fatal overload-driven implosion payload emitted when a superheated powered room catastrophically fails.
    /// </summary>
    public readonly struct FatalPressureImplosionEvent
    {
        public FatalPressureImplosionEvent(uint nodeId, int roomIndex, float temperatureCelsius, Vector3 runtimePosition)
        {
            NodeId = nodeId;
            RoomIndex = roomIndex;
            TemperatureCelsius = AtmosphereEventPayloadSanitizer.FiniteOrZero(temperatureCelsius);
            RuntimePosition = AtmosphereEventPayloadSanitizer.RuntimePositionOrZero(runtimePosition);
        }

        public uint NodeId { get; }
        public int RoomIndex { get; }
        public float TemperatureCelsius { get; }
        public Vector3 RuntimePosition { get; }
    }

    /// <summary>
    /// Unmanaged fatal pressure implosion payload carried by the deferred event lane.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct FatalPressureImplosionEventPayload
    {
        public Vector3 RuntimePosition;
        public float TemperatureCelsius;
        public uint NodeId;
        public int RoomIndex;
    }

    /// <summary>
    /// Listener for deferred fatal pressure implosion events.
    /// </summary>
    public interface IFatalPressureImplosionEventListener
    {
        void OnFatalPressureImplosion(in FatalPressureImplosionEvent implosionEvent);
    }

    /// <summary>
    /// NativeQueue-backed fatal-implosion bus for catastrophic overload failures.
    /// </summary>
    public static class FatalPressureImplosionEvents
    {
        private const int ListenerCapacity = 16;
        private const int PendingEventCapacity = 8;
        private static readonly uint _overflowWarningHash = unchecked((uint)LocHash.Compute("FatalPressureImplosionEvents.Overflow"));
        private static readonly uint _queueHash = unchecked((uint)LocHash.Compute("FatalPressureImplosionEvents"));

        // COLD ALLOC: RegistryBucket<IFatalPressureImplosionEventListener>[16] - fatal implosion listeners drained by SystemDispatcher LateUpdate - owner: FatalPressureImplosionEvents
        private static readonly RegistryBucket<IFatalPressureImplosionEventListener> _listeners = new RegistryBucket<IFatalPressureImplosionEventListener>(ListenerCapacity);
        private static NativeQueue<FatalPressureImplosionEventPayload> _pendingEvents;
        private static NativeQueue<FatalPressureImplosionEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;
        private static int _lastOverflowWarningFrame = -1;

        /// <summary>Number of fatal implosion payloads waiting for late-frame dispatch.</summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(FatalPressureImplosionEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(FatalPressureImplosionEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
            _lastOverflowWarningFrame = -1;
        }

        /// <summary>Registers one fatal pressure implosion listener.</summary>
        public static void Register(IFatalPressureImplosionEventListener listener)
        {
            if (listener == null)
                return;

            if (!_listeners.Contains(listener))
                _listeners.Register(listener);

            EnsureInitialized();
        }

        /// <summary>Unregisters one fatal pressure implosion listener.</summary>
        public static void Unregister(IFatalPressureImplosionEventListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        internal static void Shutdown()
        {
            ResetStaticState();
        }

        /// <summary>Flushes queued fatal pressure implosion payloads.</summary>
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

                if (!_pendingEvents.TryDequeue(out FatalPressureImplosionEventPayload payload))
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

        public static void Notify(in FatalPressureImplosionEvent implosionEvent)
        {
            if (_listeners.Count <= 0)
                return;

            Enqueue(new FatalPressureImplosionEventPayload
            {
                RuntimePosition = implosionEvent.RuntimePosition,
                TemperatureCelsius = implosionEvent.TemperatureCelsius,
                NodeId = implosionEvent.NodeId,
                RoomIndex = implosionEvent.RoomIndex
            });
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<FatalPressureImplosionEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<FatalPressureImplosionEventPayload>[8] - deferred fatal pressure implosion lane flushed by SystemDispatcher LateUpdate - owner: FatalPressureImplosionEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(FatalPressureImplosionEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                DeferredAtmosphereNativeQueueWarmup.Prewarm(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<FatalPressureImplosionEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<FatalPressureImplosionEventPayload>[8] - next-frame fatal pressure implosion lane prevents same-frame reentrant dispatch - owner: FatalPressureImplosionEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(FatalPressureImplosionEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
                DeferredAtmosphereNativeQueueWarmup.Prewarm(ref _nextFrameEvents, PendingEventCapacity);
            }
        }

        private static bool Enqueue(in FatalPressureImplosionEventPayload payload)
        {
            if (_listeners.Count <= 0)
                return false;

            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportOverflowOncePerFrame();
                return false;
            }

            EnsureInitialized();
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

        private static void Dispatch(in FatalPressureImplosionEventPayload payload)
        {
            int count = _listeners.Count;
            if (count <= 0)
                return;

            FatalPressureImplosionEvent implosionEvent = new FatalPressureImplosionEvent(
                payload.NodeId,
                payload.RoomIndex,
                payload.TemperatureCelsius,
                payload.RuntimePosition);

            IFatalPressureImplosionEventListener[] rawArray = _listeners.RawArray;
            for (int i = count - 1; i >= 0; i--)
            {
                IFatalPressureImplosionEventListener listener = rawArray[i];
                if (listener != null)
                    listener.OnFatalPressureImplosion(in implosionEvent);
            }
        }

        private static void ReportOverflowOncePerFrame()
        {
            int frame = Time.frameCount;
            if (_lastOverflowWarningFrame == frame)
                return;

            _lastOverflowWarningFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(_overflowWarningHash, _queueHash, PendingEventCapacity);
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

            NativeQueue<FatalPressureImplosionEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }
    }

    /// <summary>
    /// Fixed-step pressurized interior simulation for submarines.
    /// Tracks cheap room atmosphere state across the compartment graph. O2 is a 0..100 tank, not chemistry.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SubmarineFluidDynamics))]
    [AddComponentMenu("Hecton/Atmosphere/Submarine Atmosphere System")]
    public sealed class SubmarineAtmosphereSystem : MonoBehaviour, IFixedTickable, IPostFixedTickable, IInteractionSignalConsumer
    {
        private const int RoomCapacity = 8;
        private const int DoorCapacity = 7;
        private const float DefaultHighPressureEventThresholdKPa = 150f;
        private const float DefaultReferencePressureKPa = 101.325f;
        private const float DefaultDoorConductance = 0.045f;
        private const float DefaultMaxTransferUnitsPerSecond = 1.5f;
        private const float DefaultMinimumGasVolumeCubicMeters = 0.05f;
        private const float DefaultMaximumPressureKPa = 400f;
        private const float DefaultPressureImpulseRadiusMeters = 2.5f;
        private const float DefaultPressureImpulseDurationSeconds = 0.12f;
        private const float DefaultPressureImpulseFalloffExponent = 1.5f;
        private const float DefaultMaximumPressureImpulseNewtonSeconds = 18000f;
        private const float DefaultInitialOxygenFraction = 0.2095f;
        private const float DefaultInitialCarbonDioxideFraction = 0.0004f;
        private const float DefaultInertFraction = 1f - DefaultInitialOxygenFraction - DefaultInitialCarbonDioxideFraction;
        private const float DefaultOxygenTankCapacity = 100f;
        private const float DefaultLowOxygenThreshold01 = 0.2f;
        private const float DefaultCarbonDioxideToxicityFraction = 0.05f;
        private const float DefaultInvCarbonDioxideToxicitySpan = 20f;
        private const float DefaultPlayerOxygenConsumptionPercentPerSecond = 0.5f;
        private const float DefaultAtmosphereSlowTickSeconds = 1f;
        private const int MaxAtmosphereCatchupTicks = 4;
        private const int MaxFakeAtmospherePlayerCountPerRoom = 4;
        private const float DefaultHeatWattsToCelsiusPerSecond = 0.001f;
        private const float DefaultOverheatBrownoutTemperatureCelsius = 80f;
        private const float DefaultOverheatMinimumVoltage = 0.18f;
        private const float DefaultToxicRoomHazardIntensity = 1f;
        private const float DefaultFireSmokeHazardIntensity = 0.85f;
        private const float DefaultRoomHazardRadiusPaddingMeters = 1.25f;
        private const float DefaultFireSmokeVisorGlitchBias = 1.15f;
        private const float DefaultLowOxygenAudioCooldownSeconds = 45f;
        private const float DefaultFreezingRoomTemperatureCelsius = 0f;
        private const float DefaultPressureScreechCooldownSeconds = 2.75f;
        private const float DefaultPressureScreechVolume = 0.82f;
        private const float DefaultPressureScreechPitchMin = 0.86f;
        private const float DefaultPressureScreechPitchMax = 1.08f;
        private const float DefaultToxicRoomVisorPulseCooldownSeconds = 0.35f;
        private const float DefaultToxicRoomVisorGlitchDurationSeconds = 0.08f;
        private const float DefaultToxicRoomVisorDistortionHoldSeconds = 0.06f;
        private const float DefaultToxicRoomVisorDistortionRecovery = 6f;
        private const float DefaultFireSmokeSootScreenRadiusScale = 0.08f;
        private const float DefaultFireSmokeSootDitherStrength = 0.58f;
        private const float DefaultFireSmokeSootDarkenStrength = 0.42f;
        private const float FakeHazardRadiusBaseMeters = 0.85f;
        private const float FakeHazardRadiusVolumeScale = 0.08f;
        private const float FakeHazardRadiusMaxMeters = 6f;
        private const float BlendFactorPadeInstantThreshold = 8f;
        private const string NativeMemoryOwner = nameof(SubmarineAtmosphereSystem);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;
        private const int PressureImpulseOverlapCapacity = 32;
        private const int HeatEmitterCapacity = 24;
        private const float DefaultReferenceTemperatureCelsius = 20f;
        private const float DefaultFloodWaterTemperatureCelsius = 4f;
        private const float DefaultMinimumTemperatureCelsius = -5f;
        private const float DefaultMaximumTemperatureCelsius = 90f;
        private const float DefaultDeepFreezeDepthThresholdMeters = 3000f;
        private const float DefaultDeepFreezeSupplyRatioThreshold = 0.1f;
        private const float DefaultDeepFreezeTauSeconds = 8f;
        private const float DefaultDeepFreezeTargetTemperatureCelsius = -3f;
        private const float DefaultBrownoutOxygenSupplyRatioThreshold = 0.40f;
        private const float DefaultBrownoutOccupiedRoomOxygenConsumptionUnitsPerSecond = 0.0008f;
        private const float DefaultAirDensityKilogramsPerCubicMeter = 1.225f;
        private const float DefaultAirSpecificHeatJoulesPerKilogramKelvin = 1005f;
        private const float DefaultWaterDensityKilogramsPerCubicMeter = 1027f;
        private const float DefaultWaterSpecificHeatJoulesPerKilogramKelvin = 3990f;
        private const float DefaultMinimumThermalCapacityJoulesPerKelvin = 400f;
        private const float DefaultBulkheadThermalConductivityWattsPerKelvin = 185f;
        private const float DefaultSealedBulkheadThermalCoupling = 0.35f;
        private const float DefaultOpenBulkheadThermalCoupling = 1f;
        private const float DefaultFabricatorHeatWattsScale = 0.92f;
        private const float DefaultDrillHeatWattsScale = 0.97f;
        private const float DefaultReactorHeatWattsScale = 1.15f;
        private const float DefaultBoilingFloodTemperatureCelsius = 80f;
        private const float DefaultBoilingFloodMinimumFillRatio = 0.15f;
        private const float DefaultBoilingHazardIntensity = 1.1f;
        private const float DefaultBoilingHazardRadiusPaddingMeters = 1.25f;
        private const float DefaultBoilingFaunaDamagePerSecond = 14f;
        private const float DefaultReactorMeltdownTemperatureCelsius = 150f;
        private const float DefaultReactorMeltdownImpulseDurationSeconds = 0.18f;
        private const float DefaultReactorMeltdownImpulsePerWattSecond = 42f;
        private const float DefaultReactorMeltdownMinimumImpulseNewtonSeconds = 3200f;
        private const float DefaultReactorMeltdownMaximumImpulseNewtonSeconds = 28000f;
        private const float DefaultReactorMeltdownUpwardBias = 0.55f;
        private const float DefaultReactorMeltdownFloodAmplification = 1.35f;
        private const float DefaultThermalFatigueThresholdCelsius = 120f;
        private const float DefaultGlassThermalFatigueMultiplier = 5f;
        private const float DefaultTitaniumThermalFatigueMultiplier = 0.1f;
        private const float CelsiusToKelvinOffset = 273.15f;
        private const float DefaultReferenceTemperatureKelvin = 293.15f;
        private const float DefaultSteamExpansionRatio = 1600f;
        private const float DefaultSteamGenerationRateCubicMetersPerSecondPerCelsius = 0.00045f;
        private const float DefaultSteamCondensationCoefficient = 0.012f;
        private const float DefaultSteamVentReleaseFraction = 0.35f;
        private const float DefaultSteamVentImpulsePerKilopascal = 55f;
        private const float DefaultSteamVentMinimumPressureRatio = 0.92f;
        private const float DefaultExplosivePocketDecayPerSecond = 0.35f;
        private const float DefaultExplosionPocketThreshold = 0.15f;
        private const float DefaultExplosionImpulsePerPocketUnit = 24000f;
        private const float DefaultExplosionMaximumImpulseNewtonSeconds = 120000f;
        private const float DefaultExplosionPressureSpikeKPa = 55f;
        private const int BoilingFaunaContactCapacity = 16;
        private const float Epsilon = 0.0001f;
        private const int RoomStatusToxicShift = 0;
        private const int RoomStatusFreezingShift = 8;
        private const int RoomStatusPressureShift = 16;
        private const int RoomStatusFireShift = 24;
        private const uint PressureScreechRngSeed = 0xA511E9B3u;

        private static readonly Vector4 AtmosphereSootDefaultCenter = new Vector4(0.5f, 0.5f, 0f, 0f);

        private enum RoomStructuralMaterial : byte
        {
            Titanium = 0,
            Glass = 1
        }

        [System.Serializable]
#pragma warning disable 0649 // Unity serializes room definitions from submarine authoring data.
        private struct RoomDefinition
        {
            [Tooltip("Override for gas capacity in cubic meters. Zero uses the linked flood-compartment capacity.")]
            [Min(0f)]
            public float gasCapacityOverrideCubicMeters;

            [Tooltip("Initial O2 fraction inside this room. 0.2095 matches dry sea-level air.")]
            [Range(0f, 1f)]
            public float initialOxygenFraction;

            [Tooltip("Initial CO2 fraction inside this room.")]
            [Range(0f, 1f)]
            public float initialCarbonDioxideFraction;

            [Tooltip("Continuous O2 consumption in reference-gas-volume units per second.")]
            [Min(0f)]
            public float oxygenConsumptionUnitsPerSecond;

            [Tooltip("Continuous CO2 generation in reference-gas-volume units per second.")]
            [Min(0f)]
            public float carbonDioxideGenerationUnitsPerSecond;

            [Tooltip("Passive room heat injected every second in watts.")]
            [Min(0f)]
            public float passiveHeatWatts;

            [Tooltip("Initial dry-room temperature in Celsius.")]
            public float initialTemperatureCelsius;

            [Tooltip("Primary structural material used to scale thermal fatigue once the room overheats.")]
            public RoomStructuralMaterial primaryStructuralMaterial;
        }
#pragma warning restore 0649

        private struct FabricatorHeatEmitter
        {
            public Fabricator Fabricator;
            public int RoomIndex;
        }

        private struct DrillHeatEmitter
        {
            public DeepDrillModule Drill;
            public int RoomIndex;
        }

        private struct ReactorHeatEmitter
        {
            public BioReactor Reactor;
            public int RoomIndex;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct PendingAtmosphereMutation
        {
            public float OxygenUnits;
            public float TemperatureDeltaCelsius;
            public float HydrogenPocketUnits;
            public float OxygenPocketUnits;
            public float PressureSpikeKPa;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float ResolveDaltonPressureKPa(
            float oxygenUnits,
            float carbonDioxideUnits,
            float nitrogenUnits,
            float waterVaporVolumeCubicMeters,
            float roomVolumeCubicMeters,
            float gasVolumeCubicMeters,
            float temperatureCelsius,
            float referencePressureKPa,
            float maximumPressureKPa,
            float referenceTemperatureCelsius,
            float oxygenTankCapacity,
            out float oxygenPartialPressureKPa,
            out float carbonDioxidePartialPressureKPa,
            out float nitrogenPartialPressureKPa)
        {
            float tankCapacity = math.max(1f, SanitizeFiniteStatic(oxygenTankCapacity, DefaultOxygenTankCapacity));
            float referencePressure = math.max(1f, SanitizeFiniteStatic(referencePressureKPa, DefaultReferencePressureKPa));
            float maximumPressure = math.max(referencePressure, SanitizeFiniteStatic(maximumPressureKPa, DefaultMaximumPressureKPa));
            float roomVolume = math.max(0.001f, SanitizeFiniteStatic(roomVolumeCubicMeters, 0.001f));
            float gasVolume = math.max(0.001f, SanitizeFiniteStatic(gasVolumeCubicMeters, roomVolume));
            float invGasVolume = math.rcp(gasVolume);
            float invTankCapacity = math.rcp(tankCapacity);
            float compressionScale = math.max(1f, roomVolume * invGasVolume);
            float referenceKelvin = math.max(1f, SanitizeFiniteStatic(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius) + CelsiusToKelvinOffset);
            float temperatureKelvin = math.max(1f, SanitizeFiniteStatic(temperatureCelsius, referenceTemperatureCelsius) + CelsiusToKelvinOffset);
            float pressureScale = referencePressure * compressionScale * (temperatureKelvin * math.rcp(referenceKelvin));

            float oxygenFraction = math.saturate(SanitizeNonNegativeStatic(oxygenUnits) * invTankCapacity) * DefaultInitialOxygenFraction;
            float carbonDioxideFraction = math.saturate(SanitizeNonNegativeStatic(carbonDioxideUnits) * invTankCapacity);
            float nitrogenFraction = math.saturate(SanitizeNonNegativeStatic(nitrogenUnits) * invTankCapacity);
            float waterVaporFraction = math.saturate(SanitizeNonNegativeStatic(waterVaporVolumeCubicMeters) * invGasVolume);

            oxygenPartialPressureKPa = pressureScale * oxygenFraction;
            carbonDioxidePartialPressureKPa = pressureScale * carbonDioxideFraction;
            nitrogenPartialPressureKPa = pressureScale * nitrogenFraction;
            float waterVaporPartialPressureKPa = pressureScale * waterVaporFraction;
            float rawPressure = oxygenPartialPressureKPa + carbonDioxidePartialPressureKPa + nitrogenPartialPressureKPa + waterVaporPartialPressureKPa;
            float pressure = math.min(maximumPressure, rawPressure);
            float capScale = rawPressure > Epsilon ? pressure * math.rcp(rawPressure) : 0f;
            oxygenPartialPressureKPa *= capScale;
            carbonDioxidePartialPressureKPa *= capScale;
            nitrogenPartialPressureKPa *= capScale;
            return pressure;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeFiniteStatic(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeNonNegativeStatic(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveCarbonDioxideToxicity01(float carbonDioxidePressureFraction)
        {
            return math.saturate(
                (SanitizeNonNegativeStatic(carbonDioxidePressureFraction) - DefaultCarbonDioxideToxicityFraction) *
                DefaultInvCarbonDioxideToxicitySpan);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [StructLayout(LayoutKind.Sequential, Pack = 16)]
        private struct AtmosphereStepJob : IJob
        {
            [ReadOnly] public NativeArray<float> O2Front;
            [ReadOnly] public NativeArray<float> CO2Front;
            [ReadOnly] public NativeArray<float> InertFront;
            [ReadOnly] public NativeArray<float> FloodVolumes;
            [ReadOnly] public NativeArray<float> RoomVolumes;
            [ReadOnly] public NativeArray<float> PressureFront;
            [ReadOnly] public NativeArray<float> GasVolumeFront;
            [ReadOnly] public NativeArray<float> O2ConsumptionRates;
            [ReadOnly] public NativeArray<float> CO2GenerationRates;
            [ReadOnly] public NativeArray<int> RoomPlayerCounts;
            [ReadOnly] public NativeArray<float> TemperatureFront;
            [ReadOnly] public NativeArray<float> RoomHeatWatts;
            [ReadOnly] public NativeArray<float> SteamFront;
            [ReadOnly] public NativeArray<int2> DoorPairs;
            [ReadOnly] public NativeArray<byte> DoorSealed;

            public NativeArray<float> O2Back;
            public NativeArray<float> CO2Back;
            public NativeArray<float> InertBack;
            public NativeArray<float> PressureBack;
            public NativeArray<float> GasVolumeBack;
            public NativeArray<float> TemperatureBack;
            public NativeArray<float> SteamBack;
            [WriteOnly] public NativeArray<float> O2PartialPressureBack;
            [WriteOnly] public NativeArray<float> CO2PartialPressureBack;
            [WriteOnly] public NativeArray<float> N2PartialPressureBack;
            [WriteOnly] public NativeArray<uint> RoomStatusMaskBack;

            public int RoomCount;
            public int DoorCount;
            public float DeltaTime;
            public float ReferencePressureKPa;
            public float MinimumGasVolumeCubicMeters;
            public float MaximumPressureKPa;
            public float ReferenceTemperatureCelsius;
            public float FloodWaterTemperatureCelsius;
            public float MinimumTemperatureCelsius;
            public float MaximumTemperatureCelsius;
            public float OxygenTankCapacity;
            public float HeatWattsToCelsiusPerSecond;
            public float LowOxygenThresholdUnits;
            public float CarbonDioxideToxicityFraction;
            public float FreezingTemperatureCelsius;
            public float HighPressureStatusKPa;

            public void Execute()
            {
                float deltaTime = SanitizeNonNegative(DeltaTime);
                float tankCapacity = math.max(1f, SanitizeFinite(OxygenTankCapacity, DefaultOxygenTankCapacity));
                float referencePressure = math.max(1f, SanitizeFinite(ReferencePressureKPa, DefaultReferencePressureKPa));
                float maximumPressure = math.max(referencePressure, SanitizeFinite(MaximumPressureKPa, DefaultMaximumPressureKPa));
                float minimumGasVolume = math.max(0.001f, SanitizeFinite(MinimumGasVolumeCubicMeters, DefaultMinimumGasVolumeCubicMeters));
                float minimumTemperature = math.min(
                    SanitizeFinite(MinimumTemperatureCelsius, DefaultMinimumTemperatureCelsius),
                    SanitizeFinite(MaximumTemperatureCelsius, DefaultMaximumTemperatureCelsius));
                float maximumTemperature = math.max(
                    SanitizeFinite(MinimumTemperatureCelsius, DefaultMinimumTemperatureCelsius),
                    SanitizeFinite(MaximumTemperatureCelsius, DefaultMaximumTemperatureCelsius));
                float referenceTemperature = SanitizeRange(
                    ReferenceTemperatureCelsius,
                    DefaultReferenceTemperatureCelsius,
                    minimumTemperature,
                    maximumTemperature);
                float floodWaterTemperature = SanitizeRange(
                    FloodWaterTemperatureCelsius,
                    DefaultFloodWaterTemperatureCelsius,
                    minimumTemperature,
                    maximumTemperature);
                for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
                {
                    if (roomIndex >= RoomCount)
                    {
                        O2Back[roomIndex] = 0f;
                        CO2Back[roomIndex] = 0f;
                        InertBack[roomIndex] = 0f;
                        PressureBack[roomIndex] = referencePressure;
                        GasVolumeBack[roomIndex] = minimumGasVolume;
                        TemperatureBack[roomIndex] = referenceTemperature;
                        SteamBack[roomIndex] = 0f;
                        O2PartialPressureBack[roomIndex] = 0f;
                        CO2PartialPressureBack[roomIndex] = 0f;
                        N2PartialPressureBack[roomIndex] = 0f;
                        continue;
                    }

                    float roomVolume = math.max(SanitizeFinite(RoomVolumes[roomIndex], minimumGasVolume), minimumGasVolume);
                    float floodVolume = math.clamp(SanitizeNonNegative(FloodVolumes[roomIndex]), 0f, roomVolume - Epsilon);
                    float gasVolume = math.max(minimumGasVolume, roomVolume - floodVolume);
                    int playerCount = math.clamp(RoomPlayerCounts[roomIndex], 0, MaxFakeAtmospherePlayerCountPerRoom);

                    float oxygenDrain = SanitizeNonNegative(O2ConsumptionRates[roomIndex]) * playerCount * deltaTime;
                    float oxygen = math.clamp(SanitizeFinite(O2Front[roomIndex], tankCapacity) - oxygenDrain, 0f, tankCapacity);
                    float carbonDioxide = math.clamp(
                        SanitizeNonNegative(CO2Front[roomIndex]) + (SanitizeNonNegative(CO2GenerationRates[roomIndex]) * playerCount * deltaTime),
                        0f,
                        tankCapacity);
                    float inert = math.clamp(SanitizeNonNegative(InertFront[roomIndex]), 0f, tankCapacity);
                    float steam = SanitizeNonNegative(SteamFront[roomIndex]);

                    O2Back[roomIndex] = oxygen;
                    CO2Back[roomIndex] = carbonDioxide;
                    InertBack[roomIndex] = inert;
                    SteamBack[roomIndex] = steam;
                    GasVolumeBack[roomIndex] = gasVolume;

                    float previousTemperature = SanitizeRange(TemperatureFront[roomIndex], referenceTemperature, minimumTemperature, maximumTemperature);
                    float floodFill01 = math.saturate(floodVolume / math.max(roomVolume, Epsilon));
                    float floodBlend = math.saturate(floodFill01 * deltaTime * 0.1f);
                    float mixedTemperature = math.lerp(previousTemperature, floodWaterTemperature, floodBlend);
                    float temperatureDelta = SanitizeFinite(RoomHeatWatts[roomIndex], 0f) * SanitizeNonNegative(HeatWattsToCelsiusPerSecond) * deltaTime;
                    float roomTemperature = math.clamp(
                        mixedTemperature + temperatureDelta,
                        minimumTemperature,
                        maximumTemperature);
                    TemperatureBack[roomIndex] = roomTemperature;
                    PressureBack[roomIndex] = referencePressure;
                }

                for (int doorIndex = 0; doorIndex < DoorCapacity; doorIndex++)
                {
                    if (doorIndex >= DoorCount || DoorSealed[doorIndex] != 0)
                        continue;

                    int2 pair = DoorPairs[doorIndex];
                    int roomA = pair.x;
                    int roomB = pair.y;
                    if (roomA < 0 || roomA >= RoomCount || roomB < 0 || roomB >= RoomCount)
                        continue;

                    float averageOxygen = (O2Back[roomA] + O2Back[roomB]) * 0.5f;
                    float averageToxicity = (CO2Back[roomA] + CO2Back[roomB]) * 0.5f;
                    float averageInert = (InertBack[roomA] + InertBack[roomB]) * 0.5f;
                    float averageSteam = (SteamBack[roomA] + SteamBack[roomB]) * 0.5f;
                    float averageHeat = (TemperatureBack[roomA] + TemperatureBack[roomB]) * 0.5f;

                    O2Back[roomA] = averageOxygen;
                    O2Back[roomB] = averageOxygen;
                    CO2Back[roomA] = averageToxicity;
                    CO2Back[roomB] = averageToxicity;
                    InertBack[roomA] = averageInert;
                    InertBack[roomB] = averageInert;
                    SteamBack[roomA] = averageSteam;
                    SteamBack[roomB] = averageSteam;
                    TemperatureBack[roomA] = averageHeat;
                    TemperatureBack[roomB] = averageHeat;
                }

                uint statusMask = 0u;
                float lowOxygenThreshold = SanitizeNonNegative(LowOxygenThresholdUnits);
                float carbonDioxideToxicityFraction = math.max(
                    0.0001f,
                    SanitizeFinite(CarbonDioxideToxicityFraction, DefaultCarbonDioxideToxicityFraction));
                float pressureThreshold = math.max(referencePressure, SanitizeFinite(HighPressureStatusKPa, referencePressure));
                for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
                {
                    if (roomIndex >= RoomCount)
                        continue;

                    float roomVolume = math.max(SanitizeFinite(RoomVolumes[roomIndex], minimumGasVolume), minimumGasVolume);
                    float gasVolume = math.max(minimumGasVolume, SanitizeFinite(GasVolumeBack[roomIndex], minimumGasVolume));
                    float pressure = ResolveDaltonPressureKPa(
                        O2Back[roomIndex],
                        CO2Back[roomIndex],
                        InertBack[roomIndex],
                        SteamBack[roomIndex],
                        roomVolume,
                        gasVolume,
                        TemperatureBack[roomIndex],
                        referencePressure,
                        maximumPressure,
                        referenceTemperature,
                        tankCapacity,
                        out float oxygenPartialPressureKPa,
                        out float carbonDioxidePartialPressureKPa,
                        out float nitrogenPartialPressureKPa);
                    PressureBack[roomIndex] = pressure;
                    O2PartialPressureBack[roomIndex] = oxygenPartialPressureKPa;
                    CO2PartialPressureBack[roomIndex] = carbonDioxidePartialPressureKPa;
                    N2PartialPressureBack[roomIndex] = nitrogenPartialPressureKPa;

                    uint roomBit = 1u << roomIndex;
                    float carbonDioxidePressureFraction = pressure > Epsilon
                        ? carbonDioxidePartialPressureKPa * math.rcp(pressure)
                        : 0f;
                    if (O2Back[roomIndex] < lowOxygenThreshold || carbonDioxidePressureFraction >= carbonDioxideToxicityFraction)
                        statusMask |= roomBit << RoomStatusToxicShift;

                    if (TemperatureBack[roomIndex] <= FreezingTemperatureCelsius)
                        statusMask |= roomBit << RoomStatusFreezingShift;

                    if (PressureBack[roomIndex] >= pressureThreshold)
                        statusMask |= roomBit << RoomStatusPressureShift;
                }

                RoomStatusMaskBack[0] = statusMask;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float SanitizeFinite(float value, float fallback)
            {
                return math.isfinite(value) ? value : fallback;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float SanitizeNonNegative(float value)
            {
                return math.isfinite(value) ? math.max(0f, value) : 0f;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float SanitizeRange(float value, float fallback, float minimum, float maximum)
            {
                return math.clamp(math.isfinite(value) ? value : fallback, minimum, maximum);
            }
        }

        [Header("-- References ------------------")]
        [Tooltip("Flood-compartment owner that provides room capacities, flood displacement, and sealed-door topology.")]
        [SerializeField] private SubmarineFluidDynamics fluidDynamics;

        [Header("-- Atmosphere Rooms ------------------")]
        [Tooltip("Per-room initial fractions and metabolic sources. Entries map 1:1 to the submarine fluid compartments.")]
        [SerializeField] private RoomDefinition[] rooms = new RoomDefinition[RoomCapacity];

        [Header("-- Gas Solver ------------------")]
        [Tooltip("Reference pressure used when a room is dry and filled with its authored gas volume.")]
        [SerializeField, Min(1f)] private float referencePressureKPa = DefaultReferencePressureKPa;

        [Tooltip("Legacy pressure setting retained for authored data compatibility. Cheap solver uses one-pass hatch averaging.")]
#pragma warning disable CS0414
        [SerializeField, Min(0f)] private float doorConductance = DefaultDoorConductance;
#pragma warning restore CS0414

        [Tooltip("Legacy transfer cap retained for authored data compatibility. Cheap solver does not iterate gas transfer.")]
#pragma warning disable CS0414
        [SerializeField, Min(0f)] private float maxTransferUnitsPerSecond = DefaultMaxTransferUnitsPerSecond;
#pragma warning restore CS0414

        [Tooltip("Gas volume floor used to prevent divide-by-zero when a room is almost fully flooded.")]
        [SerializeField, Min(0.001f)] private float minimumGasVolumeCubicMeters = DefaultMinimumGasVolumeCubicMeters;

        [Tooltip("Maximum simulated room pressure in kPa.")]
        [SerializeField, Min(10f)] private float maximumPressureKPa = DefaultMaximumPressureKPa;

        [Tooltip("Absolute room pressure threshold required before a high-pressure event is emitted.")]
        [SerializeField, Min(0f)] private float highPressureEventThresholdKPa = DefaultHighPressureEventThresholdKPa;

        [Header("Cheap Atmosphere Fakes")]
        [Tooltip("O2 tank capacity per room. Release contract is 0..100.")]
        [SerializeField, Min(1f)] private float oxygenTankCapacity = DefaultOxygenTankCapacity;

        [Tooltip("Room O2 threshold below which low-O2 audio and toxicity hazard signals arm.")]
        [SerializeField, Range(0f, 1f)] private float lowOxygenThreshold01 = DefaultLowOxygenThreshold01;

        [Tooltip("Fallback room O2 drain in percent per second per local player.")]
        [SerializeField, Min(0f)] private float playerOxygenConsumptionPercentPerSecond = DefaultPlayerOxygenConsumptionPercentPerSecond;

        [Tooltip("Atmosphere job cadence. 1.0 seconds is the 1 Hz ColdTick.")]
        [SerializeField, Min(0.02f)] private float atmosphereSlowTickSeconds = DefaultAtmosphereSlowTickSeconds;

        [Tooltip("Fake heat gain in Celsius per second per watt.")]
        [SerializeField, Min(0f)] private float heatWattsToCelsiusPerSecond = DefaultHeatWattsToCelsiusPerSecond;

        [Tooltip("Room temperature where ambient module lights begin overheat brownout flicker.")]
        [SerializeField] private float overheatBrownoutTemperatureCelsius = DefaultOverheatBrownoutTemperatureCelsius;

        [Tooltip("Minimum voltage ratio pushed to module shaders at full overheat.")]
        [SerializeField, Range(0f, 1f)] private float overheatMinimumVoltage = DefaultOverheatMinimumVoltage;

        [Tooltip("Toxicity hazard intensity published for low-O2 rooms.")]
        [SerializeField, Min(0f)] private float toxicRoomHazardIntensity = DefaultToxicRoomHazardIntensity;

        [Tooltip("Toxicity hazard intensity published for rooms with module fire smoke.")]
        [SerializeField, Min(0f)] private float fireSmokeHazardIntensity = DefaultFireSmokeHazardIntensity;

        [Tooltip("Extra room radius for localized fake atmosphere hazard volumes.")]
        [SerializeField, Min(0f)] private float roomHazardRadiusPaddingMeters = DefaultRoomHazardRadiusPaddingMeters;

        [Tooltip("Extra visor glitch bias while standing inside fake smoke.")]
        [SerializeField, Min(0f)] private float fireSmokeVisorGlitchBias = DefaultFireSmokeVisorGlitchBias;

        [Tooltip("Screen-space soot radius scale applied by the fake smoke fullscreen pass.")]
        [SerializeField, Range(0f, 0.25f)] private float fireSmokeSootScreenRadiusScale = DefaultFireSmokeSootScreenRadiusScale;

        [Tooltip("Screen-space soot dither strength applied by the fake smoke fullscreen pass.")]
        [SerializeField, Range(0f, 1f)] private float fireSmokeSootDitherStrength = DefaultFireSmokeSootDitherStrength;

        [Tooltip("Screen-space darkening strength applied by the fake smoke fullscreen pass.")]
        [SerializeField, Range(0f, 1f)] private float fireSmokeSootDarkenStrength = DefaultFireSmokeSootDarkenStrength;

        [Tooltip("Audio log played once per cooldown when the occupied room falls below low-O2 threshold.")]
        [SerializeField] private AudioLogData lowOxygenGaspingAudioLog;

        [Tooltip("Minimum seconds between low-O2 gasping audio log triggers.")]
        [SerializeField, Min(0f)] private float lowOxygenAudioCooldownSeconds = DefaultLowOxygenAudioCooldownSeconds;

        [Tooltip("Temperature where the room status bit becomes Freezing.")]
        [SerializeField] private float freezingRoomTemperatureCelsius = DefaultFreezingRoomTemperatureCelsius;

        [Tooltip("Random metal screech clips used when the room Pressure bit is active.")]
        [SerializeField] private AudioClip[] pressureScreechClips;

        [Tooltip("Minimum seconds between high-pressure hull creak fakes.")]
        [SerializeField, Min(0f)] private float pressureScreechCooldownSeconds = DefaultPressureScreechCooldownSeconds;

        [Tooltip("World-space hull screech volume.")]
        [SerializeField, Range(0f, 1f)] private float pressureScreechVolume = DefaultPressureScreechVolume;

        [Tooltip("Minimum random pitch for high-pressure hull screech fakes.")]
        [SerializeField, Range(0.25f, 2f)] private float pressureScreechPitchMin = DefaultPressureScreechPitchMin;

        [Tooltip("Maximum random pitch for high-pressure hull screech fakes.")]
        [SerializeField, Range(0.25f, 2f)] private float pressureScreechPitchMax = DefaultPressureScreechPitchMax;

        [Tooltip("Minimum seconds between Toxic-room visor chromatic pulses.")]
        [SerializeField, Min(0f)] private float toxicRoomVisorPulseCooldownSeconds = DefaultToxicRoomVisorPulseCooldownSeconds;

        [Header("-- Thermodynamics ------------------")]
        [Tooltip("Reference dry-room temperature in Celsius used when room state is reset.")]
        [SerializeField] private float referenceTemperatureCelsius = DefaultReferenceTemperatureCelsius;

        [Tooltip("Incoming flood-water temperature in Celsius. Flooded rooms blend toward this sink.")]
        [SerializeField] private float floodWaterTemperatureCelsius = DefaultFloodWaterTemperatureCelsius;

        [Tooltip("Minimum simulated room temperature in Celsius.")]
        [SerializeField] private float minimumTemperatureCelsius = DefaultMinimumTemperatureCelsius;

        [Tooltip("Maximum simulated room temperature in Celsius.")]
        [SerializeField] private float maximumTemperatureCelsius = DefaultMaximumTemperatureCelsius;

        [Tooltip("Air density used when converting gas volume into thermal mass.")]
        [SerializeField, Min(0.1f)] private float airDensityKilogramsPerCubicMeter = DefaultAirDensityKilogramsPerCubicMeter;

        [Tooltip("Specific heat of air in J/(kg*K).")]
        [SerializeField, Min(1f)] private float airSpecificHeatJoulesPerKilogramKelvin = DefaultAirSpecificHeatJoulesPerKilogramKelvin;

        [Tooltip("Flood-water density used by the room heat sink.")]
        [SerializeField, Min(1f)] private float waterDensityKilogramsPerCubicMeter = DefaultWaterDensityKilogramsPerCubicMeter;

        [Tooltip("Specific heat of seawater in J/(kg*K).")]
        [SerializeField, Min(1f)] private float waterSpecificHeatJoulesPerKilogramKelvin = DefaultWaterSpecificHeatJoulesPerKilogramKelvin;

        [Tooltip("Thermal-capacity floor used to stabilize nearly empty rooms.")]
        [SerializeField, Min(1f)] private float minimumThermalCapacityJoulesPerKelvin = DefaultMinimumThermalCapacityJoulesPerKelvin;

        [Tooltip("Bulkhead thermal conductivity used by the room-to-room Fourier conduction pass in W/K.")]
#pragma warning disable CS0414
        [SerializeField, Min(0f)] private float bulkheadThermalConductivityWattsPerKelvin = DefaultBulkheadThermalConductivityWattsPerKelvin;
#pragma warning restore CS0414

        [Tooltip("Fraction of bulkhead conductivity applied while the connecting door is sealed.")]
#pragma warning disable CS0414
        [SerializeField, Range(0f, 1f)] private float sealedBulkheadThermalCoupling = DefaultSealedBulkheadThermalCoupling;
#pragma warning restore CS0414

        [Tooltip("Multiplier applied to bulkhead conductivity while the connecting door is open.")]
#pragma warning disable CS0414
        [SerializeField, Min(0f)] private float openBulkheadThermalCoupling = DefaultOpenBulkheadThermalCoupling;
#pragma warning restore CS0414

        [Header("Steam Phase Change")]
        [Tooltip("Legacy temperature reference retained for authored data compatibility.")]
#pragma warning disable CS0414
        [SerializeField, Min(1f)] private float referenceTemperatureKelvin = DefaultReferenceTemperatureKelvin;
#pragma warning restore CS0414

        [Tooltip("Expansion ratio applied when liquid seawater flashes into steam inside a flooded overheated room.")]
        [SerializeField, Min(1f)] private float steamExpansionRatio = DefaultSteamExpansionRatio;
        [Tooltip("Liquid-water vaporization rate in cubic meters per second for each Celsius above the boiling threshold.")]
        [SerializeField, Min(0f)] private float steamGenerationRateCubicMetersPerSecondPerCelsius = DefaultSteamGenerationRateCubicMetersPerSecondPerCelsius;
        [Tooltip("Condensation coefficient used when steam meets a colder hull boundary. Units: equivalent steam volume per second per Celsius.")]
        [SerializeField, Min(0f)] private float steamCondensationCoefficient = DefaultSteamCondensationCoefficient;
        [Tooltip("Fraction of room gas and steam dumped during one emergency vent burst.")]
        [SerializeField, Range(0.05f, 1f)] private float steamVentReleaseFraction = DefaultSteamVentReleaseFraction;
        [Tooltip("Pressure-ratio threshold relative to the configured pressure cap that arms emergency venting.")]
        [SerializeField, Range(0.1f, 1f)] private float steamVentMinimumPressureRatio = DefaultSteamVentMinimumPressureRatio;
        [Tooltip("Impulse scale applied to the submarine when an overpressured room vents to the abyss.")]
        [SerializeField, Min(0f)] private float steamVentImpulsePerKilopascal = DefaultSteamVentImpulsePerKilopascal;

        [Tooltip("Waste-heat multiplier applied to fabricator electrical draw.")]
        [SerializeField, Min(0f)] private float fabricatorHeatWattsScale = DefaultFabricatorHeatWattsScale;

        [Tooltip("Waste-heat multiplier applied to deep-drill electrical draw.")]
        [SerializeField, Min(0f)] private float drillHeatWattsScale = DefaultDrillHeatWattsScale;

        [Tooltip("Waste-heat multiplier applied to reactor electrical output.")]
        [SerializeField, Min(0f)] private float reactorHeatWattsScale = DefaultReactorHeatWattsScale;

        [Header("-- Abyssal Freeze ------------------")]
        [Tooltip("Depth threshold where catastrophic blackout cooling starts forcing flooded rooms toward freezing.")]
        [SerializeField, Min(0f)] private float deepFreezeDepthThresholdMeters = DefaultDeepFreezeDepthThresholdMeters;

        [Tooltip("Below this supply ratio, flooded rooms begin exponential blackout cooling.")]
        [SerializeField, Range(0f, 1f)] private float deepFreezeSupplyRatioThreshold = DefaultDeepFreezeSupplyRatioThreshold;

        [Tooltip("Time constant used by the blackout cooling curve.")]
        [SerializeField, Min(0.1f)] private float deepFreezeTauSeconds = DefaultDeepFreezeTauSeconds;

        [Tooltip("Target flooded-room temperature reached under abyssal blackout conditions.")]
        [SerializeField] private float deepFreezeTargetTemperatureCelsius = DefaultDeepFreezeTargetTemperatureCelsius;

        [Header("Brownout Life Support")]
        [Tooltip("Below this module supply ratio, occupied base rooms stop generation and become O2 sinks.")]
        [SerializeField, Range(0f, 1f)] private float brownoutOxygenSupplyRatioThreshold = DefaultBrownoutOxygenSupplyRatioThreshold;

        [Tooltip("Slow occupied-room O2 drain applied while the connected base module is browned out.")]
        [SerializeField, Min(0f)] private float brownoutOccupiedRoomOxygenConsumptionUnitsPerSecond = DefaultBrownoutOccupiedRoomOxygenConsumptionUnitsPerSecond;

        [Header("-- Boiling Flood Hazard ------------------")]
        [Tooltip("Flooded rooms at or above this temperature register a heat hazard in the surrounding water.")]
        [SerializeField] private float boilingFloodTemperatureCelsius = DefaultBoilingFloodTemperatureCelsius;

        [Tooltip("Minimum flooded fill ratio required before boiling-water hazards become active.")]
        [SerializeField, Range(0f, 1f)] private float boilingFloodMinimumFillRatio = DefaultBoilingFloodMinimumFillRatio;

        [Tooltip("Base heat-hazard intensity registered for boiling flooded rooms.")]
        [SerializeField, Min(0f)] private float boilingHazardIntensity = DefaultBoilingHazardIntensity;

        [Tooltip("Extra radius added to the compartment-derived boiling hazard bounds.")]
        [SerializeField, Min(0f)] private float boilingHazardRadiusPaddingMeters = DefaultBoilingHazardRadiusPaddingMeters;

        [Tooltip("Per-second thermal damage applied to nearby fauna caught in boiling flooded rooms.")]
        [SerializeField, Min(0f)] private float boilingFaunaDamagePerSecond = DefaultBoilingFaunaDamagePerSecond;

        [Header("-- Reactor Meltdown ------------------")]
        [Tooltip("Room temperature threshold in Celsius that triggers a reactor meltdown impulse.")]
        [SerializeField] private float reactorMeltdownTemperatureCelsius = DefaultReactorMeltdownTemperatureCelsius;

        [Tooltip("Seconds used to convert reactor thermal force into a one-shot impulse.")]
        [SerializeField, Min(0.001f)] private float reactorMeltdownImpulseDurationSeconds = DefaultReactorMeltdownImpulseDurationSeconds;

        [Tooltip("Impulse scale in newton-seconds per watt of reactor output.")]
        [SerializeField, Min(0f)] private float reactorMeltdownImpulsePerWattSecond = DefaultReactorMeltdownImpulsePerWattSecond;

        [Tooltip("Minimum reactor meltdown impulse in newton-seconds.")]
        [SerializeField, Min(1f)] private float reactorMeltdownMinimumImpulseNewtonSeconds = DefaultReactorMeltdownMinimumImpulseNewtonSeconds;

        [Tooltip("Maximum reactor meltdown impulse in newton-seconds.")]
        [SerializeField, Min(1f)] private float reactorMeltdownMaximumImpulseNewtonSeconds = DefaultReactorMeltdownMaximumImpulseNewtonSeconds;

        [Tooltip("How much world-up is mixed into the reactor blowout direction.")]
        [SerializeField, Range(0f, 1f)] private float reactorMeltdownUpwardBias = DefaultReactorMeltdownUpwardBias;

        [Tooltip("Extra impulse multiplier applied when the reactor room is flooded.")]
        [SerializeField, Min(1f)] private float reactorMeltdownFloodAmplification = DefaultReactorMeltdownFloodAmplification;
        [Header("Thermal Material Stress")]
        [Tooltip("Room temperature threshold in Celsius where material-specific structural-fatigue scaling begins.")]
        [SerializeField] private float thermalFatigueThresholdCelsius = DefaultThermalFatigueThresholdCelsius;
        [Tooltip("Structural fatigue multiplier applied to glass rooms above the thermal threshold.")]
        [SerializeField, Min(0f)] private float glassThermalFatigueMultiplier = DefaultGlassThermalFatigueMultiplier;
        [Tooltip("Structural fatigue multiplier applied to titanium rooms above the thermal threshold.")]
        [SerializeField, Min(0f)] private float titaniumThermalFatigueMultiplier = DefaultTitaniumThermalFatigueMultiplier;

        [Header("-- Explosive Electrolysis ------------------")]
        [Tooltip("Per-second decay applied to explosive electrolysis gas pockets.")]
        [SerializeField, Min(0f)] private float explosivePocketDecayPerSecond = DefaultExplosivePocketDecayPerSecond;

        [Tooltip("Minimum combined hydrogen/oxygen pocket intensity required before a spark detonates the compartment.")]
        [SerializeField, Range(0f, 1f)] private float explosivePocketThreshold = DefaultExplosionPocketThreshold;

        [Tooltip("Impulse scale applied to the submarine rigidbody when an explosive pocket detonates.")]
        [SerializeField, Min(0f)] private float explosionImpulsePerPocketUnit = DefaultExplosionImpulsePerPocketUnit;

        [Tooltip("Safety cap on one electrolysis explosion impulse.")]
        [SerializeField, Min(1f)] private float explosionMaximumImpulseNewtonSeconds = DefaultExplosionMaximumImpulseNewtonSeconds;

        [Tooltip("Pressure spike injected into the room when a flooded overloaded node detonates violently.")]
        [FormerlySerializedAs("electrolysisPressureSpikeKPa")]
        [SerializeField, Min(0f)] private float explosionPressureSpikeKPa = DefaultExplosionPressureSpikeKPa;

        [Header("-- Pressure Blowout ------------------")]
        [Tooltip("Radius around an opened bulkhead that receives the pressure blowout impulse.")]
        [SerializeField, Min(0.25f)] private float pressureImpulseRadiusMeters = DefaultPressureImpulseRadiusMeters;

        [Tooltip("Impulse duration used to convert raw pressure force into a one-shot rigidbody impulse.")]
        [SerializeField, Min(0.001f)] private float pressureImpulseDurationSeconds = DefaultPressureImpulseDurationSeconds;

        [Tooltip("Cheap squared-distance falloff bias applied to bodies near the bulkhead opening. Kept serialized under the legacy field name.")]
        [SerializeField, Min(0.25f)] private float pressureImpulseFalloffExponent = DefaultPressureImpulseFalloffExponent;

        [Tooltip("Safety cap on one blowout impulse magnitude in newton-seconds.")]
        [SerializeField, Min(1f)] private float maximumPressureImpulseNewtonSeconds = DefaultMaximumPressureImpulseNewtonSeconds;

        [Tooltip("Rigidbodies on these layers receive the blowout impulse.")]
        [SerializeField] private LayerMask pressureImpulseLayers = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        [Header("-- Diagnostics ------------------")]
        [SerializeField] private int _debugRoomCount;
        [SerializeField] private int _debugDoorCount;
        [SerializeField] private float _debugAveragePressureKPa;
        [SerializeField] private float _debugMaxPressureKPa;
        [SerializeField] private float _debugAverageOxygenFraction;
        [SerializeField] private float _debugAverageCarbonDioxideFraction;
        [SerializeField] private float _debugAverageTemperatureCelsius;
        [SerializeField] private float _debugMaxTemperatureCelsius;
        [SerializeField] private float _debugAverageSteamVolumeCubicMeters;
        [SerializeField] private float _debugMaxSteamVolumeCubicMeters;

        private Transform _cachedTransform;
        private Transform _playerTransform;
        private Camera _playerCamera;
        private Rigidbody _submarineBody;
        private bool _registered;
        private bool _topologySeeded;
        private bool _thermalEmittersSeeded;
        private bool _emergencyVentPipesSeeded;
        private int _topologyRoomCount = -1;
        private int _topologyDoorCount = -1;
        private JobHandle _atmosphereJobHandle;
        private JobHandle _disposeHandle;
        private bool _atmosphereJobRunning;
        private float _scheduledAtmosphereDeltaTime;

        private NativeArray<float> _roomVolumes;
        private NativeArray<float> _floodVolumes;
        private NativeArray<float> _o2Front;
        private NativeArray<float> _o2Back;
        private NativeArray<float> _co2Front;
        private NativeArray<float> _co2Back;
        private NativeArray<float> _inertFront;
        private NativeArray<float> _inertBack;
        private NativeArray<float> _pressureFront;
        private NativeArray<float> _pressureBack;
        private NativeArray<float> _o2PartialPressureFront;
        private NativeArray<float> _o2PartialPressureBack;
        private NativeArray<float> _co2PartialPressureFront;
        private NativeArray<float> _co2PartialPressureBack;
        private NativeArray<float> _n2PartialPressureFront;
        private NativeArray<float> _n2PartialPressureBack;
        private NativeArray<float> _gasVolumeFront;
        private NativeArray<float> _gasVolumeBack;
        private NativeArray<float> _o2ConsumptionRates;
        private NativeArray<float> _co2GenerationRates;
        private NativeArray<int> _roomPlayerCounts;
        private NativeArray<float> _temperatureFront;
        private NativeArray<float> _temperatureBack;
        private NativeArray<float> _steamFront;
        private NativeArray<float> _steamBack;
        private NativeArray<float> _hydrogenPocketFront;
        private NativeArray<float> _oxygenPocketFront;
        private NativeArray<float> _roomHeatWatts;
        private NativeArray<uint> _roomStatusMaskFront;
        private NativeArray<uint> _roomStatusMaskBack;
        private NativeArray<int2> _doorPairs;
        private NativeArray<byte> _doorSealed;
        private NativeArray<byte> _doorSealedPrevious;
        // COLD ALLOC: Collider[32] - one-shot non-alloc bulkhead blowout overlap buffer - owner: SubmarineAtmosphereSystem
        private readonly Collider[] _pressureImpulseOverlapBuffer = new Collider[PressureImpulseOverlapCapacity];
        // COLD ALLOC: Rigidbody[32] - unique-body scratch for pressure blowout dispatch - owner: SubmarineAtmosphereSystem
        private readonly Rigidbody[] _pressureImpulseBodyBuffer = new Rigidbody[PressureImpulseOverlapCapacity];
        // COLD ALLOC: float[32] - precomputed pressure blowout falloff per unique body - owner: SubmarineAtmosphereSystem
        private readonly float[] _pressureImpulseFalloffBuffer = new float[PressureImpulseOverlapCapacity];
        // COLD ALLOC: int[8] - per-room boiling hazard source IDs - owner: SubmarineAtmosphereSystem
        private readonly int[] _boilingHazardIds = new int[RoomCapacity];
        private uint _boilingHazardActiveMask;
        // COLD ALLOC: int[8] - per-room low-O2 toxicity hazard source IDs - owner: SubmarineAtmosphereSystem
        private readonly int[] _toxicRoomHazardIds = new int[RoomCapacity];
        // COLD ALLOC: int[8] - per-room fake smoke hazard source IDs - owner: SubmarineAtmosphereSystem
        private readonly int[] _fireSmokeHazardIds = new int[RoomCapacity];
        private uint _toxicRoomHazardActiveMask;
        private uint _fireSmokeHazardActiveMask;
        // COLD ALLOC: BaseModule[8] - cached room-to-base brownout links - owner: SubmarineAtmosphereSystem
        private readonly BaseModule[] _brownoutRoomModules = new BaseModule[RoomCapacity];
        // COLD ALLOC: BaseModule[8] - cached room-to-base module links for visual atmosphere fakes - owner: SubmarineAtmosphereSystem
        private readonly BaseModule[] _atmosphereRoomModules = new BaseModule[RoomCapacity];
        private uint _overheatVisualActiveMask;
        // COLD ALLOC: SpatialQueryHit[16] - fauna spillover query scratch for boiling rooms - owner: SubmarineAtmosphereSystem
        private readonly SpatialQueryHit[] _boilingFaunaContacts = new SpatialQueryHit[BoilingFaunaContactCapacity];
        // COLD ALLOC: FabricatorHeatEmitter[24] - cached fabricator heat sources mapped to rooms - owner: SubmarineAtmosphereSystem
        private readonly FabricatorHeatEmitter[] _fabricatorHeatEmitters = new FabricatorHeatEmitter[HeatEmitterCapacity];
        // COLD ALLOC: DrillHeatEmitter[24] - cached drill heat sources mapped to rooms - owner: SubmarineAtmosphereSystem
        private readonly DrillHeatEmitter[] _drillHeatEmitters = new DrillHeatEmitter[HeatEmitterCapacity];
        // COLD ALLOC: ReactorHeatEmitter[24] - cached reactor heat sources mapped to rooms - owner: SubmarineAtmosphereSystem
        private readonly ReactorHeatEmitter[] _reactorHeatEmitters = new ReactorHeatEmitter[HeatEmitterCapacity];
        private uint _reactorMeltdownTriggeredMask;
        // COLD ALLOC: List<Fabricator>[8] - cold-path fabricator scan scratch for thermal emitter cache - owner: SubmarineAtmosphereSystem
        private readonly System.Collections.Generic.List<Fabricator> _fabricatorScanBuffer = new System.Collections.Generic.List<Fabricator>(8);
        // COLD ALLOC: List<DeepDrillModule>[8] - cold-path drill scan scratch for thermal emitter cache - owner: SubmarineAtmosphereSystem
        private readonly System.Collections.Generic.List<DeepDrillModule> _drillScanBuffer = new System.Collections.Generic.List<DeepDrillModule>(8);
        // COLD ALLOC: List<BioReactor>[8] - cold-path reactor scan scratch for thermal emitter cache - owner: SubmarineAtmosphereSystem
        private readonly System.Collections.Generic.List<BioReactor> _reactorScanBuffer = new System.Collections.Generic.List<BioReactor>(8);
        // COLD ALLOC: PendingAtmosphereMutation[8] - deferred authoritative room writes while Burst atmosphere job owns BackBuffer - owner: SubmarineAtmosphereSystem
        private readonly PendingAtmosphereMutation[] _pendingAtmosphereMutations = new PendingAtmosphereMutation[RoomCapacity];
        // COLD ALLOC: LogisticsPipeNode[8] - room-indexed emergency vent cache, avoids component scans in pressure path - owner: SubmarineAtmosphereSystem
        private readonly LogisticsPipeNode[] _emergencyVentPipesByRoom = new LogisticsPipeNode[RoomCapacity];
        // COLD ALLOC: List<LogisticsPipeNode>[16] - cold scan scratch for emergency vent cache seeding - owner: SubmarineAtmosphereSystem
        private readonly System.Collections.Generic.List<LogisticsPipeNode> _ventPipeScanBuffer = new System.Collections.Generic.List<LogisticsPipeNode>(16);
        private uint _emergencyVentRoomMask;
        private int _fabricatorHeatEmitterCount;
        private int _drillHeatEmitterCount;
        private int _reactorHeatEmitterCount;
        private float _atmosphereStepAccumulator;
        private float _lowOxygenAudioCooldownRemaining;
        private float _pressureScreechCooldownRemaining;
        private float _toxicRoomVisorPulseCooldownRemaining;
        private uint _pressureScreechRngState = PressureScreechRngSeed;
        private uint _runtimeRoomStatusMask;
        private uint _pendingAtmosphereMutationMask;
        private bool _smokeOverlayRuntimeActive;
        private bool _smokeOverlayRuntimeDirty = true;
        private Vector4 _lastSmokeOverlayParams;
        private Vector4 _lastSmokeOverlayCenter = AtmosphereSootDefaultCenter;

        public int RoomCount => fluidDynamics != null ? math.clamp(fluidDynamics.CompartmentCount, 0, RoomCapacity) : 0;

        internal uint RuntimeRoomStatusMask => _runtimeRoomStatusMask;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FiniteOr(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FiniteNonNegativeOrZero(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FiniteClampedOr(float value, float fallback, float minimum, float maximum)
        {
            return math.clamp(math.isfinite(value) ? value : fallback, minimum, maximum);
        }

        private void ResolveSafeTemperatureBounds(out float minimumTemperature, out float maximumTemperature)
        {
            float rawMinimum = FiniteOr(minimumTemperatureCelsius, DefaultMinimumTemperatureCelsius);
            float rawMaximum = FiniteOr(maximumTemperatureCelsius, DefaultMaximumTemperatureCelsius);
            minimumTemperature = math.min(rawMinimum, rawMaximum);
            maximumTemperature = math.max(rawMinimum, rawMaximum);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ResolveSafeReferencePressureKPa()
        {
            return math.max(1f, FiniteOr(referencePressureKPa, DefaultReferencePressureKPa));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ResolveSafeMaximumPressureKPa()
        {
            return math.max(ResolveSafeReferencePressureKPa(), FiniteOr(maximumPressureKPa, DefaultMaximumPressureKPa));
        }

        public float GetRoomPressureKPa(int roomIndex)
        {
            if (!_pressureFront.IsCreated || roomIndex < 0 || roomIndex >= RoomCount)
                return ResolveSafeReferencePressureKPa();

            return FiniteClampedOr(_pressureFront[roomIndex], ResolveSafeReferencePressureKPa(), 0f, ResolveSafeMaximumPressureKPa());
        }

        public float GetRoomOxygenFraction(int roomIndex)
        {
            if (!_o2Front.IsCreated || roomIndex < 0 || roomIndex >= RoomCount)
                return DefaultInitialOxygenFraction;

            return math.saturate(FiniteNonNegativeOrZero(_o2Front[roomIndex]) / math.max(1f, FiniteOr(oxygenTankCapacity, DefaultOxygenTankCapacity)));
        }

        public float GetRoomCarbonDioxideFraction(int roomIndex)
        {
            if (!_co2Front.IsCreated || roomIndex < 0 || roomIndex >= RoomCount)
                return DefaultInitialCarbonDioxideFraction;

            return math.saturate(FiniteNonNegativeOrZero(_co2Front[roomIndex]) / math.max(1f, FiniteOr(oxygenTankCapacity, DefaultOxygenTankCapacity)));
        }

        public float GetRoomOxygenPartialPressureKPa(int roomIndex)
        {
            if (!_o2PartialPressureFront.IsCreated || roomIndex < 0 || roomIndex >= RoomCount)
                return DefaultInitialOxygenFraction * ResolveSafeReferencePressureKPa();

            return FiniteClampedOr(_o2PartialPressureFront[roomIndex], DefaultInitialOxygenFraction * ResolveSafeReferencePressureKPa(), 0f, ResolveSafeMaximumPressureKPa());
        }

        public float GetRoomCarbonDioxidePartialPressureKPa(int roomIndex)
        {
            if (!_co2PartialPressureFront.IsCreated || roomIndex < 0 || roomIndex >= RoomCount)
                return DefaultInitialCarbonDioxideFraction * ResolveSafeReferencePressureKPa();

            return FiniteClampedOr(_co2PartialPressureFront[roomIndex], DefaultInitialCarbonDioxideFraction * ResolveSafeReferencePressureKPa(), 0f, ResolveSafeMaximumPressureKPa());
        }

        public float GetRoomNitrogenPartialPressureKPa(int roomIndex)
        {
            if (!_n2PartialPressureFront.IsCreated || roomIndex < 0 || roomIndex >= RoomCount)
                return DefaultInertFraction * ResolveSafeReferencePressureKPa();

            return FiniteClampedOr(_n2PartialPressureFront[roomIndex], DefaultInertFraction * ResolveSafeReferencePressureKPa(), 0f, ResolveSafeMaximumPressureKPa());
        }

        public float GetRoomCarbonDioxidePressureFraction(int roomIndex)
        {
            return ResolveRoomCarbonDioxidePressureFraction(roomIndex);
        }

        public float GetRoomTemperatureCelsius(int roomIndex)
        {
            if (!_temperatureFront.IsCreated || roomIndex < 0 || roomIndex >= RoomCount)
                return FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius);

            ResolveSafeTemperatureBounds(out float minimumTemperature, out float maximumTemperature);
            return FiniteClampedOr(_temperatureFront[roomIndex], FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius), minimumTemperature, maximumTemperature);
        }

        public float GetRoomFloodFillRatio(int roomIndex)
        {
            if (!_floodVolumes.IsCreated || !_roomVolumes.IsCreated || roomIndex < 0 || roomIndex >= RoomCount)
                return 0f;

            float roomVolume = math.max(Epsilon, FiniteOr(_roomVolumes[roomIndex], Epsilon));
            return math.saturate(FiniteNonNegativeOrZero(_floodVolumes[roomIndex]) / roomVolume);
        }

        public void InjectOxygenUnits(int roomIndex, float oxygenUnits)
        {
            InjectOxygenUnitsInternal(roomIndex, oxygenUnits);
        }

        private float InjectOxygenUnitsInternal(int roomIndex, float oxygenUnits)
        {
            if (oxygenUnits <= 0f ||
                !math.isfinite(oxygenUnits) ||
                !_o2Front.IsCreated ||
                !_co2Front.IsCreated ||
                !_inertFront.IsCreated ||
                !_pressureFront.IsCreated ||
                !_gasVolumeFront.IsCreated ||
                roomIndex < 0 ||
                roomIndex >= RoomCount)
            {
                return 0f;
            }

            if (!TryPrepareAtmosphereFrontForWrite())
                return QueuePendingOxygenUnits(roomIndex, oxygenUnits);

            return ApplyOxygenUnitsImmediate(roomIndex, oxygenUnits);
        }

        internal float TransferOxygenFromStorage(int roomIndex, float requestedOxygenUnits, ref float storageOxygenUnits)
        {
            if (requestedOxygenUnits <= 0f ||
                !math.isfinite(requestedOxygenUnits) ||
                storageOxygenUnits <= 0f ||
                !math.isfinite(storageOxygenUnits) ||
                !_o2Front.IsCreated ||
                roomIndex < 0 ||
                roomIndex >= RoomCount)
            {
                return 0f;
            }

            if (!TryPrepareAtmosphereFrontForWrite())
            {
                float queuedTransfer = QueuePendingOxygenUnits(roomIndex, math.min(requestedOxygenUnits, storageOxygenUnits));
                if (queuedTransfer <= 0f)
                    return 0f;

                storageOxygenUnits = math.max(0f, storageOxygenUnits - queuedTransfer);
                return queuedTransfer;
            }

            float transfer = ApplyOxygenUnitsImmediate(roomIndex, math.min(requestedOxygenUnits, storageOxygenUnits));
            if (transfer <= 0f)
                return 0f;

            storageOxygenUnits = math.max(0f, storageOxygenUnits - transfer);
            return transfer;
        }

        /// <summary>
        /// Injects oxygen from mature cultivation slots carrying genetics Bit1 into a room atmosphere.
        /// </summary>
        internal float InjectCultivationOxygenFromSlots(
            int roomIndex,
            NativeArray<CultivationManager.CultivationSlotState>.ReadOnly slots,
            float oxygenUnitsPerMaturePlant)
        {
            if (oxygenUnitsPerMaturePlant <= 0f ||
                !math.isfinite(oxygenUnitsPerMaturePlant) ||
                !slots.IsCreated ||
                roomIndex < 0 ||
                roomIndex >= RoomCount)
            {
                return 0f;
            }

            ulong oxygenGeneMask = (ulong)GeneticTraitProfile.GeneticTraitMask.OxygenProducing;
            float oxygenUnits = 0f;
            for (int i = 0; i < slots.Length; i++)
            {
                CultivationManager.CultivationSlotState slot = slots[i];
                if (slot.SeedItemHashId == 0 ||
                    slot.Growth01 < 0.999f ||
                    slot.Quality01 <= 0f ||
                    (slot.GeneticsMask & oxygenGeneMask) == 0UL)
                {
                    continue;
                }

                oxygenUnits += oxygenUnitsPerMaturePlant;
            }

            if (oxygenUnits <= 0f)
                return 0f;

            return InjectOxygenUnitsInternal(roomIndex, oxygenUnits);
        }

        public void InjectRoomTemperatureDeltaCelsius(int roomIndex, float deltaCelsius)
        {
            if (deltaCelsius == 0f ||
                !math.isfinite(deltaCelsius) ||
                !_temperatureFront.IsCreated ||
                roomIndex < 0 ||
                roomIndex >= RoomCount)
            {
                return;
            }

            if (!TryPrepareAtmosphereFrontForWrite())
            {
                QueuePendingTemperatureDelta(roomIndex, deltaCelsius);
                return;
            }

            ResolveSafeTemperatureBounds(out float minTemperature, out float maxTemperature);
            float currentTemperature = FiniteClampedOr(_temperatureFront[roomIndex], FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius), minTemperature, maxTemperature);
            _temperatureFront[roomIndex] = math.clamp(
                currentTemperature + deltaCelsius,
                minTemperature,
                maxTemperature);
            RefreshRoomPressureImmediate(roomIndex);
        }

        public void InjectRoomHeatEnergyJoules(int roomIndex, float heatEnergyJoules)
        {
            if (heatEnergyJoules <= 0f ||
                !math.isfinite(heatEnergyJoules) ||
                !_temperatureFront.IsCreated ||
                roomIndex < 0 ||
                roomIndex >= RoomCount)
            {
                return;
            }

            if (!TryPrepareAtmosphereFrontForWrite())
            {
                QueuePendingHeatEnergy(roomIndex, heatEnergyJoules);
                return;
            }

            float thermalCapacity = ResolveInstantThermalCapacity(roomIndex);
            if (thermalCapacity <= Epsilon)
                return;

            float deltaCelsius = heatEnergyJoules / thermalCapacity;
            if (!math.isfinite(deltaCelsius) || deltaCelsius <= 0f)
                return;

            ResolveSafeTemperatureBounds(out float minTemperature, out float maxTemperature);
            float currentTemperature = FiniteClampedOr(_temperatureFront[roomIndex], FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius), minTemperature, maxTemperature);
            _temperatureFront[roomIndex] = math.clamp(
                currentTemperature + deltaCelsius,
                minTemperature,
                maxTemperature);
            RefreshRoomPressureImmediate(roomIndex);
        }

        public void TransferRoomHeatEnergyJoules(int sourceRoomIndex, int destinationRoomIndex, float heatEnergyJoules)
        {
            if (heatEnergyJoules <= 0f ||
                !math.isfinite(heatEnergyJoules) ||
                !_temperatureFront.IsCreated ||
                sourceRoomIndex < 0 || sourceRoomIndex >= RoomCount ||
                destinationRoomIndex < 0 || destinationRoomIndex >= RoomCount ||
                sourceRoomIndex == destinationRoomIndex)
            {
                return;
            }

            if (!TryPrepareAtmosphereFrontForWrite())
            {
                QueuePendingHeatEnergy(sourceRoomIndex, -heatEnergyJoules);
                QueuePendingHeatEnergy(destinationRoomIndex, heatEnergyJoules);
                return;
            }

            float sourceCapacity = ResolveInstantThermalCapacity(sourceRoomIndex);
            float destinationCapacity = ResolveInstantThermalCapacity(destinationRoomIndex);
            if (sourceCapacity <= Epsilon || destinationCapacity <= Epsilon)
                return;

            float sourceDelta = heatEnergyJoules / sourceCapacity;
            float destinationDelta = heatEnergyJoules / destinationCapacity;
            if (!math.isfinite(sourceDelta) || !math.isfinite(destinationDelta) || sourceDelta <= 0f || destinationDelta <= 0f)
                return;

            ResolveSafeTemperatureBounds(out float minTemperature, out float maxTemperature);
            float sourceTemperature = FiniteClampedOr(_temperatureFront[sourceRoomIndex], FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius), minTemperature, maxTemperature);
            float destinationTemperature = FiniteClampedOr(_temperatureFront[destinationRoomIndex], FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius), minTemperature, maxTemperature);
            _temperatureFront[sourceRoomIndex] = math.clamp(
                sourceTemperature - sourceDelta,
                minTemperature,
                maxTemperature);
            _temperatureFront[destinationRoomIndex] = math.clamp(
                destinationTemperature + destinationDelta,
                minTemperature,
                maxTemperature);
            RefreshRoomPressureImmediate(sourceRoomIndex);
            RefreshRoomPressureImmediate(destinationRoomIndex);
        }

        public void InjectElectrolysisGasPocket(int roomIndex, float hydrogenUnits, float oxygenUnits, float pressureSpikeKPa)
        {
            if (roomIndex < 0 ||
                roomIndex >= RoomCount ||
                !_hydrogenPocketFront.IsCreated ||
                !_oxygenPocketFront.IsCreated ||
                (!IsPositiveFinite(hydrogenUnits) && !IsPositiveFinite(oxygenUnits) && !IsPositiveFinite(pressureSpikeKPa)))
            {
                return;
            }

            if (!TryPrepareAtmosphereFrontForWrite())
            {
                QueuePendingElectrolysisPocket(roomIndex, hydrogenUnits, oxygenUnits, pressureSpikeKPa);
                return;
            }

            if (IsPositiveFinite(hydrogenUnits))
                _hydrogenPocketFront[roomIndex] = FiniteNonNegativeOrZero(_hydrogenPocketFront[roomIndex]) + hydrogenUnits;

            if (IsPositiveFinite(oxygenUnits))
                _oxygenPocketFront[roomIndex] = FiniteNonNegativeOrZero(_oxygenPocketFront[roomIndex]) + oxygenUnits;

            if (_pressureFront.IsCreated && IsPositiveFinite(pressureSpikeKPa))
            {
                float currentPressure = FiniteClampedOr(_pressureFront[roomIndex], ResolveSafeReferencePressureKPa(), 0f, ResolveSafeMaximumPressureKPa());
                _pressureFront[roomIndex] = math.clamp(
                    currentPressure + pressureSpikeKPa,
                    0f,
                    ResolveSafeMaximumPressureKPa());
                RefreshRoomStatusBitsImmediate(roomIndex);
            }
        }

        private bool TryPrepareAtmosphereFrontForWrite()
        {
            if (_atmosphereJobRunning)
                return false;

            ApplyPendingAtmosphereMutations();
            return true;
        }

        private float ApplyOxygenUnitsImmediate(int roomIndex, float oxygenUnits)
        {
            if (oxygenUnits <= 0f || !math.isfinite(oxygenUnits) || !_o2Front.IsCreated || roomIndex < 0 || roomIndex >= RoomCount)
                return 0f;

            float currentOxygenUnits = FiniteNonNegativeOrZero(_o2Front[roomIndex]);
            float maximumOxygenUnits = ResolveRoomMaxOxygenCapacityUnits(roomIndex);
            float clampedOxygenDelta = math.min(oxygenUnits, math.max(0f, maximumOxygenUnits - currentOxygenUnits));
            if (clampedOxygenDelta <= Epsilon)
                return 0f;

            _o2Front[roomIndex] = currentOxygenUnits + clampedOxygenDelta;
            RefreshRoomPressureImmediate(roomIndex);
            return clampedOxygenDelta;
        }

        private float QueuePendingOxygenUnits(int roomIndex, float oxygenUnits)
        {
            if (oxygenUnits <= 0f || !math.isfinite(oxygenUnits) || roomIndex < 0 || roomIndex >= RoomCount || !_o2Front.IsCreated)
                return 0f;

            ref PendingAtmosphereMutation mutation = ref _pendingAtmosphereMutations[roomIndex];
            float currentOxygenUnits = FiniteNonNegativeOrZero(_o2Front[roomIndex]);
            float pendingOxygenUnits = math.isfinite(mutation.OxygenUnits) ? math.max(0f, mutation.OxygenUnits) : 0f;
            float maximumOxygenUnits = ResolveRoomMaxOxygenCapacityUnits(roomIndex);
            float queuedOxygenUnits = math.min(
                oxygenUnits,
                math.max(0f, maximumOxygenUnits - currentOxygenUnits - pendingOxygenUnits));
            if (queuedOxygenUnits <= Epsilon)
                return 0f;

            mutation.OxygenUnits = pendingOxygenUnits + queuedOxygenUnits;
            _pendingAtmosphereMutationMask |= 1u << roomIndex;
            return queuedOxygenUnits;
        }

        private void QueuePendingTemperatureDelta(int roomIndex, float deltaCelsius)
        {
            if (deltaCelsius == 0f || !math.isfinite(deltaCelsius) || roomIndex < 0 || roomIndex >= RoomCount)
                return;

            ResolveSafeTemperatureBounds(out float minTemperature, out float maxTemperature);
            float currentTemperature = _temperatureFront.IsCreated
                ? FiniteClampedOr(_temperatureFront[roomIndex], FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius), minTemperature, maxTemperature)
                : FiniteClampedOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius, minTemperature, maxTemperature);
            ref PendingAtmosphereMutation mutation = ref _pendingAtmosphereMutations[roomIndex];
            float pendingTemperatureDelta = math.isfinite(mutation.TemperatureDeltaCelsius)
                ? mutation.TemperatureDeltaCelsius
                : 0f;
            float minDelta = minTemperature - currentTemperature;
            float maxDelta = maxTemperature - currentTemperature;
            float clampedPendingDelta = math.clamp(pendingTemperatureDelta + deltaCelsius, minDelta, maxDelta);
            if (math.abs(clampedPendingDelta) <= Epsilon)
            {
                mutation.TemperatureDeltaCelsius = 0f;
                return;
            }

            mutation.TemperatureDeltaCelsius = clampedPendingDelta;
            _pendingAtmosphereMutationMask |= 1u << roomIndex;
        }

        private void QueuePendingHeatEnergy(int roomIndex, float heatEnergyJoules)
        {
            if (heatEnergyJoules == 0f || !math.isfinite(heatEnergyJoules) || roomIndex < 0 || roomIndex >= RoomCount)
                return;

            float thermalCapacity = ResolveInstantThermalCapacity(roomIndex);
            if (thermalCapacity <= Epsilon)
                return;

            QueuePendingTemperatureDelta(roomIndex, heatEnergyJoules / thermalCapacity);
        }

        private void QueuePendingElectrolysisPocket(int roomIndex, float hydrogenUnits, float oxygenUnits, float pressureSpikeKPa)
        {
            if (roomIndex < 0 ||
                roomIndex >= RoomCount ||
                (!IsPositiveFinite(hydrogenUnits) && !IsPositiveFinite(oxygenUnits) && !IsPositiveFinite(pressureSpikeKPa)))
            {
                return;
            }

            ref PendingAtmosphereMutation mutation = ref _pendingAtmosphereMutations[roomIndex];
            bool hasMutation = false;
            if (IsPositiveFinite(hydrogenUnits))
            {
                float pendingHydrogenUnits = math.isfinite(mutation.HydrogenPocketUnits)
                    ? math.max(0f, mutation.HydrogenPocketUnits)
                    : 0f;
                mutation.HydrogenPocketUnits = pendingHydrogenUnits + hydrogenUnits;
                hasMutation = true;
            }

            if (IsPositiveFinite(oxygenUnits))
            {
                float pendingOxygenPocketUnits = math.isfinite(mutation.OxygenPocketUnits)
                    ? math.max(0f, mutation.OxygenPocketUnits)
                    : 0f;
                mutation.OxygenPocketUnits = pendingOxygenPocketUnits + oxygenUnits;
                hasMutation = true;
            }

            if (IsPositiveFinite(pressureSpikeKPa))
            {
                float maximumPressure = ResolveSafeMaximumPressureKPa();
                float currentPressure = _pressureFront.IsCreated
                    ? FiniteClampedOr(_pressureFront[roomIndex], ResolveSafeReferencePressureKPa(), 0f, maximumPressure)
                    : ResolveSafeReferencePressureKPa();
                float pendingPressureSpike = math.isfinite(mutation.PressureSpikeKPa)
                    ? math.max(0f, mutation.PressureSpikeKPa)
                    : 0f;
                float clampedPressureSpike = math.min(
                    pressureSpikeKPa,
                    math.max(0f, maximumPressure - currentPressure - pendingPressureSpike));
                if (clampedPressureSpike > Epsilon)
                {
                    mutation.PressureSpikeKPa = pendingPressureSpike + clampedPressureSpike;
                    hasMutation = true;
                }
            }

            if (hasMutation)
                _pendingAtmosphereMutationMask |= 1u << roomIndex;
        }

        private void ApplyPendingAtmosphereMutations()
        {
            uint mutationMask = _pendingAtmosphereMutationMask;
            if (mutationMask == 0u)
                return;

            _pendingAtmosphereMutationMask = 0u;
            int roomCount = RoomCount;
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                uint roomBit = 1u << roomIndex;
                if ((mutationMask & roomBit) == 0u)
                    continue;

                PendingAtmosphereMutation mutation = _pendingAtmosphereMutations[roomIndex];
                _pendingAtmosphereMutations[roomIndex] = default;
                if (roomIndex >= roomCount)
                    continue;

                if (IsPositiveFinite(mutation.OxygenUnits))
                    ApplyOxygenUnitsImmediate(roomIndex, mutation.OxygenUnits);

                bool pressureDirty = false;
                if (_temperatureFront.IsCreated &&
                    mutation.TemperatureDeltaCelsius != 0f &&
                    math.isfinite(mutation.TemperatureDeltaCelsius))
                {
                    ResolveSafeTemperatureBounds(out float minTemperature, out float maxTemperature);
                    float currentTemperature = FiniteClampedOr(_temperatureFront[roomIndex], FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius), minTemperature, maxTemperature);
                    _temperatureFront[roomIndex] = math.clamp(
                        currentTemperature + mutation.TemperatureDeltaCelsius,
                        minTemperature,
                        maxTemperature);
                    pressureDirty = true;
                }

                if (pressureDirty)
                    RefreshRoomPressureImmediate(roomIndex);

                if (_hydrogenPocketFront.IsCreated && IsPositiveFinite(mutation.HydrogenPocketUnits))
                    _hydrogenPocketFront[roomIndex] = FiniteNonNegativeOrZero(_hydrogenPocketFront[roomIndex]) + mutation.HydrogenPocketUnits;

                if (_oxygenPocketFront.IsCreated && IsPositiveFinite(mutation.OxygenPocketUnits))
                    _oxygenPocketFront[roomIndex] = FiniteNonNegativeOrZero(_oxygenPocketFront[roomIndex]) + mutation.OxygenPocketUnits;

                if (_pressureFront.IsCreated && IsPositiveFinite(mutation.PressureSpikeKPa))
                {
                    float currentPressure = FiniteClampedOr(_pressureFront[roomIndex], ResolveSafeReferencePressureKPa(), 0f, ResolveSafeMaximumPressureKPa());
                    _pressureFront[roomIndex] = math.clamp(
                        currentPressure + mutation.PressureSpikeKPa,
                        0f,
                        ResolveSafeMaximumPressureKPa());
                    RefreshRoomStatusBitsImmediate(roomIndex);
                }
            }
        }

        public float GetRoomSteamVolumeCubicMeters(int roomIndex)
        {
            if (!_steamFront.IsCreated || roomIndex < 0 || roomIndex >= RoomCount)
                return 0f;

            return FiniteNonNegativeOrZero(_steamFront[roomIndex]);
        }

        internal void HandleExternalModuleBreach(Vector3 breachWorldPosition, float breachAreaSquareMeters)
        {
            if (fluidDynamics == null)
                return;

            int roomIndex = ResolveNearestRoomIndexForWorldPosition(breachWorldPosition);
            if (roomIndex < 0 || roomIndex >= RoomCount)
                return;

            float sanitizedArea = math.max(0.05f, breachAreaSquareMeters);
            fluidDynamics.TriggerImmediateBreachDepressurization(roomIndex, breachWorldPosition, sanitizedArea);
            SealAdjacentBulkheads(roomIndex);
        }

        internal float ResolveThermalFatigueMultiplier(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCount || !_temperatureFront.IsCreated)
                return 1f;

            float thresholdTemperature = math.max(FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius), FiniteOr(thermalFatigueThresholdCelsius, DefaultThermalFatigueThresholdCelsius));
            if (GetRoomTemperatureCelsius(roomIndex) < thresholdTemperature)
                return 1f;

            RoomStructuralMaterial structuralMaterial = roomIndex < rooms.Length
                ? rooms[roomIndex].primaryStructuralMaterial
                : RoomStructuralMaterial.Titanium;

            return structuralMaterial == RoomStructuralMaterial.Glass
                ? FiniteNonNegativeOrZero(glassThermalFatigueMultiplier)
                : FiniteNonNegativeOrZero(titaniumThermalFatigueMultiplier);
        }

        private void SealAdjacentBulkheads(int breachedRoomIndex)
        {
            if (fluidDynamics == null || breachedRoomIndex < 0)
                return;

            int doorCount = math.min(fluidDynamics.ConfiguredBulkheadCount, DoorCapacity);
            for (int doorIndex = 0; doorIndex < doorCount; doorIndex++)
            {
                if (!fluidDynamics.TryGetBulkheadDefinition(doorIndex, out int compartmentA, out int compartmentB, out bool isSealed))
                    continue;

                if (isSealed || (compartmentA != breachedRoomIndex && compartmentB != breachedRoomIndex))
                    continue;

                fluidDynamics.SetBulkheadSealed(compartmentA, compartmentB, true);
                if (_doorSealed.IsCreated && doorIndex < _doorSealed.Length)
                    _doorSealed[doorIndex] = 1;
                if (_doorSealedPrevious.IsCreated && doorIndex < _doorSealedPrevious.Length)
                    _doorSealedPrevious[doorIndex] = 1;
            }
        }

        internal int ResolveNearestRoomIndexForWorldPosition(Vector3 worldPosition)
        {
            return ResolveNearestRoomIndex(worldPosition);
        }

        internal Vector3 ResolveRoomRuntimePosition(int roomIndex)
        {
            if (fluidDynamics == null || roomIndex < 0 || roomIndex >= RoomCount)
                return ResolveSubmarineFallbackRuntimePosition();

            Vector3 localCentroid = fluidDynamics.GetCompartmentCentroid(roomIndex);
            return _cachedTransform != null ? _cachedTransform.TransformPoint(localCentroid) : localCentroid;
        }

        private Vector3 ResolveSubmarineFallbackRuntimePosition()
        {
            return _submarineBody != null ? _submarineBody.worldCenterOfMass : Vector3.zero;
        }

        internal float ResolveRoomFloodFillNormalized(int roomIndex)
        {
            if (fluidDynamics == null || roomIndex < 0 || roomIndex >= RoomCount)
                return 0f;

            return math.saturate(fluidDynamics.GetCompartmentFillRatio(roomIndex));
        }

        internal bool TryResolveRoomFloodFillNormalized(Vector3 worldPosition, out int roomIndex, out float floodFillNormalized)
        {
            roomIndex = ResolveNearestRoomIndex(worldPosition);
            if (roomIndex < 0 || roomIndex >= RoomCount)
            {
                floodFillNormalized = 0f;
                return false;
            }

            floodFillNormalized = ResolveRoomFloodFillNormalized(roomIndex);
            return true;
        }

        internal float ResolveExternalDepthMeters()
        {
            return fluidDynamics != null ? FiniteNonNegativeOrZero(fluidDynamics.ExternalDepthMeters) : 0f;
        }

        public void ApplyInteractionSignal(in Hecton8.Interaction.InteractionSignal signal, Vector3 runtimeHitPoint)
        {
            InteractionEffectType effectType = (InteractionEffectType)signal.EffectType;
            if (effectType != InteractionEffectType.PlasmaCut &&
                effectType != InteractionEffectType.Weld &&
                effectType != InteractionEffectType.Torch)
            {
                return;
            }

            int roomIndex = ResolveNearestRoomIndex(runtimeHitPoint);
            if (roomIndex < 0 || roomIndex >= RoomCount || !_hydrogenPocketFront.IsCreated || !_oxygenPocketFront.IsCreated)
                return;

            float pocketIntensity = math.min(FiniteNonNegativeOrZero(_hydrogenPocketFront[roomIndex]), FiniteNonNegativeOrZero(_oxygenPocketFront[roomIndex]));
            if (pocketIntensity < math.saturate(FiniteOr(explosivePocketThreshold, DefaultExplosionPocketThreshold)))
                return;

            TriggerExplosivePocketDetonation(roomIndex, runtimeHitPoint, pocketIntensity);
        }

        private void Awake()
        {
            CacheReferences();
            SeedBoilingHazardIds();
            SeedAtmosphereHazardIds();
            RefreshDebugState();
        }

        private void OnEnable()
        {
            CacheReferences();
            EnsureNativeState();
            if (_roomVolumes.IsCreated)
                PrewarmAtmosphereAuthoringCaches();

            TryRegister();
            RefreshDebugState();
        }

        private void OnDisable()
        {
            TryUnregister();
            DisposeNativeStateDeferred();
        }

        private void OnDestroy()
        {
            TryUnregister();
            DisposeNativeStateDeferred();
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (fixedDeltaTime <= 0f)
                return;

            CacheReferences();
            TryFinalizeDeferredNativeDisposal();
            if (fluidDynamics == null)
            {
                _atmosphereStepAccumulator = 0f;
                ClearBoilingFloodHazards();
                ClearAtmosphereFakes();
                RefreshDebugState();
                return;
            }

            EnsureNativeState();
            if (!_roomVolumes.IsCreated)
            {
                RefreshDebugState();
                return;
            }

            if (_atmosphereJobRunning)
            {
                AccumulateAtmosphereStepTime(fixedDeltaTime);
                RefreshDebugState();
                return;
            }

            InvalidateTopologyIfShapeChanged();
            SyncFluidSnapshot();
            SeedTopologyIfNeeded();
            SeedThermalEmittersIfNeeded();
            SeedEmergencyVentPipesIfNeeded();
            AccumulateRoomHeatSources();
            PublishDoorOpeningPressureEvents();

            AccumulateAtmosphereStepTime(fixedDeltaTime);
            float slowTickSeconds = math.max(0.02f, FiniteOr(atmosphereSlowTickSeconds, DefaultAtmosphereSlowTickSeconds));
            if (_atmosphereStepAccumulator + Epsilon < slowTickSeconds)
            {
                RefreshDebugState();
                return;
            }

            float atmosphereDeltaTime = _atmosphereStepAccumulator;
            _atmosphereStepAccumulator = 0f;
            ScheduleAtmosphereJob(atmosphereDeltaTime);
            RefreshDebugState();
        }

        private void PrewarmAtmosphereAuthoringCaches()
        {
            SeedTopologyIfNeeded();
            SeedThermalEmittersIfNeeded();
            SeedEmergencyVentPipesIfNeeded();
        }

        private void AccumulateAtmosphereStepTime(float fixedDeltaTime)
        {
            float slowTickSeconds = math.max(0.02f, FiniteOr(atmosphereSlowTickSeconds, DefaultAtmosphereSlowTickSeconds));
            float maxAccumulatedSeconds = slowTickSeconds * MaxAtmosphereCatchupTicks;
            _atmosphereStepAccumulator = math.min(
                FiniteNonNegativeOrZero(_atmosphereStepAccumulator) + FiniteNonNegativeOrZero(fixedDeltaTime),
                maxAccumulatedSeconds);
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            ConsumeCompletedJob(fixedDeltaTime);
        }

        private void ApplyAbyssalBlackoutFreeze(float fixedDeltaTime)
        {
            if (fluidDynamics == null || !_temperatureFront.IsCreated || !_floodVolumes.IsCreated || !_roomVolumes.IsCreated)
                return;

            float depthMeters = FiniteNonNegativeOrZero(fluidDynamics.ExternalDepthMeters);
            if (depthMeters < FiniteNonNegativeOrZero(deepFreezeDepthThresholdMeters))
                return;

            IPowerGridService powerGridService = GlobalRegistry.PowerGrid;
            float totalConsumption = powerGridService != null ? FiniteNonNegativeOrZero(powerGridService.TotalConsumption) : 0f;
            float supplyRatio = totalConsumption > Epsilon
                ? math.saturate(FiniteNonNegativeOrZero(powerGridService.TotalGeneration) / totalConsumption)
                : 1f;
            if (supplyRatio >= math.saturate(FiniteOr(deepFreezeSupplyRatioThreshold, DefaultDeepFreezeSupplyRatioThreshold)))
                return;

            ResolveSafeTemperatureBounds(out float minTemperature, out float maxTemperature);
            float targetTemperature = FiniteClampedOr(deepFreezeTargetTemperatureCelsius, DefaultDeepFreezeTargetTemperatureCelsius, minTemperature, maxTemperature);
            float alpha = ResolveBlendFactor(math.max(0.1f, FiniteOr(deepFreezeTauSeconds, DefaultDeepFreezeTauSeconds)), fixedDeltaTime);
            for (int roomIndex = 0; roomIndex < RoomCount; roomIndex++)
            {
                float roomVolume = math.max(Epsilon, FiniteOr(_roomVolumes[roomIndex], Epsilon));
                float floodFillRatio = math.saturate(FiniteNonNegativeOrZero(_floodVolumes[roomIndex]) / roomVolume);
                if (floodFillRatio <= Epsilon)
                    continue;

                float currentTemperature = FiniteClampedOr(_temperatureFront[roomIndex], FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius), minTemperature, maxTemperature);
                _temperatureFront[roomIndex] = math.clamp(
                    math.lerp(currentTemperature, targetTemperature, alpha),
                    minTemperature,
                    maxTemperature);
            }
        }

        private void ProcessSteamPhaseCycle(float fixedDeltaTime)
        {
            if (fluidDynamics == null ||
                !_steamFront.IsCreated ||
                !_temperatureFront.IsCreated ||
                !_floodVolumes.IsCreated ||
                !_roomVolumes.IsCreated)
            {
                return;
            }

            float safeDeltaTime = FiniteNonNegativeOrZero(fixedDeltaTime);
            float safeSteamExpansionRatio = math.max(1f, FiniteOr(steamExpansionRatio, DefaultSteamExpansionRatio));
            float vaporizationRate = FiniteNonNegativeOrZero(steamGenerationRateCubicMetersPerSecondPerCelsius);
            float condensationRate = FiniteNonNegativeOrZero(steamCondensationCoefficient);
            float hullShellTemperature = ResolveHullShellTemperatureCelsius();
            float minimumGasVolume = math.max(0.001f, FiniteOr(minimumGasVolumeCubicMeters, DefaultMinimumGasVolumeCubicMeters));

            for (int roomIndex = 0; roomIndex < RoomCount; roomIndex++)
            {
                float roomVolume = math.max(minimumGasVolume, FiniteOr(_roomVolumes[roomIndex], minimumGasVolume));
                float roomTemperature = GetRoomTemperatureCelsius(roomIndex);
                float floodVolume = math.clamp(FiniteNonNegativeOrZero(_floodVolumes[roomIndex]), 0f, roomVolume);
                float steamVolume = FiniteNonNegativeOrZero(_steamFront[roomIndex]);
                float boilingPoint = ResolveRoomBoilingPointCelsius(roomIndex);

                if (floodVolume > Epsilon && roomTemperature > boilingPoint)
                {
                    float overshootCelsius = roomTemperature - boilingPoint;
                    float liquidVaporized = math.min(
                        floodVolume,
                        overshootCelsius * vaporizationRate * safeDeltaTime);
                    if (liquidVaporized > Epsilon)
                    {
                        fluidDynamics.AddCompartmentFloodVolumeDelta(roomIndex, -liquidVaporized);
                        _floodVolumes[roomIndex] = math.max(0f, floodVolume - liquidVaporized);
                        floodVolume = _floodVolumes[roomIndex];
                        steamVolume += liquidVaporized * safeSteamExpansionRatio;
                    }
                }

                if (steamVolume > Epsilon && roomTemperature > hullShellTemperature)
                {
                    float condensedSteamVolume = math.min(
                        steamVolume,
                        (roomTemperature - hullShellTemperature) * condensationRate * safeDeltaTime);
                    if (condensedSteamVolume > Epsilon)
                    {
                        steamVolume -= condensedSteamVolume;
                        float returnedLiquidVolume = condensedSteamVolume / safeSteamExpansionRatio;
                        fluidDynamics.AddCompartmentFloodVolumeDelta(roomIndex, returnedLiquidVolume);
                        floodVolume = math.clamp(floodVolume + returnedLiquidVolume, 0f, roomVolume);
                        _floodVolumes[roomIndex] = floodVolume;
                    }
                }

                if (_gasVolumeFront.IsCreated)
                    _gasVolumeFront[roomIndex] = math.max(minimumGasVolume, roomVolume - floodVolume);

                _steamFront[roomIndex] = FiniteNonNegativeOrZero(steamVolume);
                RecomputeInstantRoomPressure(roomIndex);
                RefreshRoomStatusBitsOnly(roomIndex);
            }
        }

        private void TryEmergencyAtmosphericVenting(float fixedDeltaTime)
        {
            if (_submarineBody == null || fluidDynamics == null || !_pressureFront.IsCreated || !_steamFront.IsCreated)
                return;

            float ventThresholdPressure = ResolveEmergencyVentThresholdPressureKPa();
            if (ventThresholdPressure <= Epsilon)
                return;

            for (int roomIndex = 0; roomIndex < RoomCount; roomIndex++)
            {
                float roomPressure = FiniteClampedOr(_pressureFront[roomIndex], ResolveSafeReferencePressureKPa(), 0f, ResolveSafeMaximumPressureKPa());
                if (roomPressure <= ventThresholdPressure)
                    continue;

                if (!TryResolveEmergencyVentPipe(roomIndex, out LogisticsPipeNode ventPipe))
                {
                    if (roomPressure >= math.max(ventThresholdPressure, ResolveSafeMaximumPressureKPa() * 0.98f))
                        TriggerSteamOverpressureFailure(roomIndex, roomPressure);
                    continue;
                }

                float releaseFraction = math.saturate(FiniteOr(steamVentReleaseFraction, DefaultSteamVentReleaseFraction));
                if (releaseFraction <= Epsilon)
                    continue;

                _steamFront[roomIndex] = FiniteNonNegativeOrZero(_steamFront[roomIndex]) * (1f - releaseFraction);
                _hydrogenPocketFront[roomIndex] = FiniteNonNegativeOrZero(_hydrogenPocketFront[roomIndex]) * (1f - releaseFraction);
                _oxygenPocketFront[roomIndex] = FiniteNonNegativeOrZero(_oxygenPocketFront[roomIndex]) * (1f - releaseFraction);
                _o2Front[roomIndex] = FiniteNonNegativeOrZero(_o2Front[roomIndex]) * (1f - (releaseFraction * 0.5f));
                _co2Front[roomIndex] = FiniteNonNegativeOrZero(_co2Front[roomIndex]) * (1f - (releaseFraction * 0.5f));
                _inertFront[roomIndex] = FiniteNonNegativeOrZero(_inertFront[roomIndex]) * (1f - (releaseFraction * 0.5f));
                RecomputeInstantRoomPressure(roomIndex);
                RefreshRoomStatusBitsOnly(roomIndex);

                Vector3 ventPosition = ventPipe.ResolveVentRuntimePosition();
                Vector3 ventDirection = ventPipe.ResolveVentDirection(_submarineBody.worldCenterOfMass);
                float overshootKPa = FiniteNonNegativeOrZero(roomPressure - ventThresholdPressure);
                float impulseMagnitude = overshootKPa *
                                         FiniteNonNegativeOrZero(steamVentImpulsePerKilopascal) *
                                         math.max(0.05f, releaseFraction);
                if (impulseMagnitude > Epsilon)
                {
                    PhysicsForceRouter.QueueForceAtPosition(
                        _submarineBody,
                        -ventDirection * impulseMagnitude,
                        ventPosition,
                        ForceMode.Impulse);
                }

                ventPipe.RegisterEmergencyVentVisual(math.saturate(overshootKPa / math.max(1f, ventThresholdPressure)));
            }
        }

        private void TriggerSteamOverpressureFailure(int roomIndex, float roomPressure)
        {
            if (_submarineBody == null || fluidDynamics == null || roomIndex < 0 || roomIndex >= RoomCount)
                return;

            Vector3 roomPosition = ResolveRoomRuntimePosition(roomIndex);
            float referencePressure = ResolveSafeReferencePressureKPa();
            float maximumPressure = ResolveSafeMaximumPressureKPa();
            float overshoot01 = math.saturate((FiniteOr(roomPressure, referencePressure) - referencePressure) / math.max(1f, maximumPressure - referencePressure));
            float explosionPocket = math.max(
                overshoot01,
                math.min(FiniteNonNegativeOrZero(_hydrogenPocketFront[roomIndex]), FiniteNonNegativeOrZero(_oxygenPocketFront[roomIndex])));
            TriggerExplosivePocketDetonation(roomIndex, roomPosition, math.max(math.saturate(FiniteOr(explosivePocketThreshold, DefaultExplosionPocketThreshold)), explosionPocket));
            fluidDynamics.TriggerBreach(roomIndex, math.max(0.25f, FiniteNonNegativeOrZero(overshoot01)));
        }

        private static float ResolveBlendFactor(float tauSeconds, float deltaTime)
        {
            float safeTau = math.max(0.0001f, FiniteOr(tauSeconds, 0.0001f));
            float safeDeltaTime = FiniteNonNegativeOrZero(deltaTime);
            float normalizedStep = math.min(safeDeltaTime / safeTau, BlendFactorPadeInstantThreshold);
            return ResolveOneMinusExpPade(normalizedStep);
        }

        private static float ResolveOneMinusExpPade(float normalizedStep)
        {
            float x = FiniteNonNegativeOrZero(normalizedStep);
            float numerator = x * (6f + x);
            float denominator = 6f + (4f * x) + (x * x);
            return math.saturate(numerator / math.max(denominator, 0.0001f));
        }

        private void DecayExplosivePockets(float fixedDeltaTime)
        {
            float safeDeltaTime = FiniteNonNegativeOrZero(fixedDeltaTime);
            if (!_hydrogenPocketFront.IsCreated || !_oxygenPocketFront.IsCreated || safeDeltaTime <= 0f)
                return;

            float decay = FiniteNonNegativeOrZero(explosivePocketDecayPerSecond) * safeDeltaTime;
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                _hydrogenPocketFront[roomIndex] = math.max(0f, FiniteNonNegativeOrZero(_hydrogenPocketFront[roomIndex]) - decay);
                _oxygenPocketFront[roomIndex] = math.max(0f, FiniteNonNegativeOrZero(_oxygenPocketFront[roomIndex]) - decay);
            }
        }

        private void TriggerExplosivePocketDetonation(int roomIndex, Vector3 runtimeHitPoint, float pocketIntensity)
        {
            if (_submarineBody == null || roomIndex < 0 || roomIndex >= RoomCount)
                return;

            Vector3 roomPosition = ResolveRoomRuntimePosition(roomIndex);
            Vector3 centerDirection = roomPosition - _submarineBody.worldCenterOfMass;
            Vector3 forceDirection = ResolveFakeBlastDirection(centerDirection, 0.35f);
            float impulseMagnitude = math.min(
                FiniteNonNegativeOrZero(pocketIntensity) * FiniteNonNegativeOrZero(explosionImpulsePerPocketUnit),
                math.max(1f, FiniteOr(explosionMaximumImpulseNewtonSeconds, DefaultExplosionMaximumImpulseNewtonSeconds)));

            PhysicsForceRouter.QueueForceAtPosition(
                _submarineBody,
                forceDirection * impulseMagnitude,
                runtimeHitPoint,
                ForceMode.Impulse);

            _hydrogenPocketFront[roomIndex] = 0f;
            _oxygenPocketFront[roomIndex] = 0f;
            if (_pressureFront.IsCreated)
            {
                float currentPressure = FiniteClampedOr(_pressureFront[roomIndex], ResolveSafeReferencePressureKPa(), 0f, ResolveSafeMaximumPressureKPa());
                _pressureFront[roomIndex] = math.min(
                    ResolveSafeMaximumPressureKPa(),
                    currentPressure + FiniteNonNegativeOrZero(explosionPressureSpikeKPa));
                RefreshRoomStatusBitsImmediate(roomIndex);
            }
        }

        private float ResolveHullShellTemperatureCelsius()
        {
            if (fluidDynamics == null)
                return FiniteOr(floodWaterTemperatureCelsius, DefaultFloodWaterTemperatureCelsius);

            float depthMeters = FiniteNonNegativeOrZero(fluidDynamics.ExternalDepthMeters);
            float abyssalCooling = math.saturate(depthMeters / 4000f);
            ResolveSafeTemperatureBounds(out float minTemperature, out float maxTemperature);
            float referenceTemperature = FiniteClampedOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius, minTemperature, maxTemperature);
            float floodTemperature = FiniteClampedOr(floodWaterTemperatureCelsius, DefaultFloodWaterTemperatureCelsius, minTemperature, maxTemperature);
            return math.lerp(referenceTemperature, floodTemperature, abyssalCooling);
        }

        private float ResolveRoomBoilingPointCelsius(int roomIndex)
        {
            float externalDepthMeters = fluidDynamics != null ? FiniteNonNegativeOrZero(fluidDynamics.ExternalDepthMeters) : 0f;
            float pressureKPa = roomIndex >= 0 && roomIndex < RoomCount && _pressureFront.IsCreated
                ? math.max(ResolveSafeReferencePressureKPa(), FiniteClampedOr(_pressureFront[roomIndex], ResolveSafeReferencePressureKPa(), 0f, ResolveSafeMaximumPressureKPa()))
                : ResolveSafeReferencePressureKPa();
            float pressureDepthEquivalent = math.max(0f, pressureKPa - ResolveSafeReferencePressureKPa());
            return 100f + ((externalDepthMeters + (pressureDepthEquivalent * 0.1f)) * 0.02f);
        }

        private void RecomputeInstantRoomPressure(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCount || !_pressureFront.IsCreated || !_gasVolumeFront.IsCreated)
                return;

            float temperatureCelsius = _temperatureFront.IsCreated ? GetRoomTemperatureCelsius(roomIndex) : FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius);
            _pressureFront[roomIndex] = ResolveInstantFakePressure(roomIndex, temperatureCelsius);
        }

        private float ResolveRoomCarbonDioxidePressureFraction(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCount)
                return DefaultInitialCarbonDioxideFraction;

            float pressure = _pressureFront.IsCreated
                ? FiniteClampedOr(_pressureFront[roomIndex], ResolveSafeReferencePressureKPa(), 0f, ResolveSafeMaximumPressureKPa())
                : ResolveSafeReferencePressureKPa();
            float carbonDioxidePartialPressure = _co2PartialPressureFront.IsCreated
                ? FiniteClampedOr(_co2PartialPressureFront[roomIndex], DefaultInitialCarbonDioxideFraction * ResolveSafeReferencePressureKPa(), 0f, ResolveSafeMaximumPressureKPa())
                : DefaultInitialCarbonDioxideFraction * ResolveSafeReferencePressureKPa();
            if (pressure <= Epsilon)
                return 0f;

            float inversePressure = math.rcp(math.max(pressure, Epsilon));
            return math.saturate(carbonDioxidePartialPressure * inversePressure);
        }

        private void RefreshRoomPressureImmediate(int roomIndex)
        {
            RecomputeInstantRoomPressure(roomIndex);
            RefreshRoomStatusBitsImmediate(roomIndex);
        }

        private void RefreshRoomStatusBitsOnly(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCount || !_roomStatusMaskFront.IsCreated)
                return;

            uint roomBit = 1u << roomIndex;
            uint roomStatusBits = ResolveRoomStatusBits(roomBit);
            uint statusMask = ResolveRoomStatusMask(roomIndex, _roomStatusMaskFront[0]);
            _roomStatusMaskFront[0] = statusMask;
            _runtimeRoomStatusMask = (_runtimeRoomStatusMask & ~roomStatusBits) | (statusMask & roomStatusBits);
        }

        private void RefreshRoomStatusBitsImmediate(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCount || !_roomStatusMaskFront.IsCreated)
                return;

            uint roomBit = 1u << roomIndex;
            uint roomStatusBits = ResolveRoomStatusBits(roomBit);
            uint statusMask = ResolveRoomStatusMask(roomIndex, _roomStatusMaskFront[0]);
            _roomStatusMaskFront[0] = statusMask;
            _runtimeRoomStatusMask = (_runtimeRoomStatusMask & ~roomStatusBits) | (statusMask & roomStatusBits);

            float tankCapacity = math.max(1f, FiniteOr(oxygenTankCapacity, DefaultOxygenTankCapacity));
            float lowOxygenThreshold = math.saturate(FiniteOr(lowOxygenThreshold01, DefaultLowOxygenThreshold01)) * tankCapacity;
            bool hasOxygen = _o2Front.IsCreated;
            float oxygenValue = hasOxygen ? FiniteNonNegativeOrZero(_o2Front[roomIndex]) : tankCapacity;
            bool toxicRoom = (statusMask & (roomBit << RoomStatusToxicShift)) != 0u;
            bool lowOxygen = hasOxygen && oxygenValue < lowOxygenThreshold;
            bool pressureStatus = (statusMask & (roomBit << RoomStatusPressureShift)) != 0u;
            float carbonDioxideDanger01 = ResolveCarbonDioxideToxicity01(ResolveRoomCarbonDioxidePressureFraction(roomIndex));

            if (hasOxygen && _roomPlayerCounts.IsCreated && _roomPlayerCounts[roomIndex] > 0)
            {
                float roomOxygen01 = oxygenValue / tankCapacity;
                if (math.isfinite(roomOxygen01))
                    UIStateStore.WriteValue(UIValueSlotId.RoomOxygen01, math.saturate(roomOxygen01), Time.unscaledTime);
                else
                    UIStateStore.ClearValue(UIValueSlotId.RoomOxygen01);
            }

            Vector3 worldCenter = default;
            float radius = 0f;
            bool needsBounds = toxicRoom || pressureStatus;
            bool hasBounds = needsBounds && TryResolveRoomHazardBounds(roomIndex, out worldCenter, out radius);
            if (toxicRoom && hasBounds)
            {
                float oxygenDanger01 = math.saturate((lowOxygenThreshold - oxygenValue) / math.max(1f, lowOxygenThreshold));
                float toxicityDanger01 = math.max(0.1f, math.max(oxygenDanger01, carbonDioxideDanger01));
                RegisterToxicRoomHazard(roomIndex, worldCenter, toxicityDanger01 * FiniteNonNegativeOrZero(toxicRoomHazardIntensity), radius);
            }
            else
            {
                UnregisterToxicRoomHazard(roomIndex);
            }

            if (pressureStatus)
                TryPlayPressureScreech(roomIndex, hasBounds ? worldCenter : ResolveSubmarineFallbackRuntimePosition());

            float roomTemperature = _temperatureFront.IsCreated ? GetRoomTemperatureCelsius(roomIndex) : FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius);
            if (_temperatureFront.IsCreated &&
                (roomTemperature > FiniteOr(overheatBrownoutTemperatureCelsius, DefaultOverheatBrownoutTemperatureCelsius) ||
                 (_overheatVisualActiveMask & roomBit) != 0u))
            {
                ApplyOverheatVoltageFake(roomIndex, ResolveModuleForRoom(roomIndex));
            }
        }

        private uint ResolveRoomStatusMask(int roomIndex, uint sourceStatusMask)
        {
            uint roomBit = 1u << roomIndex;
            uint roomStatusBits = ResolveRoomStatusBits(roomBit);
            uint statusMask = sourceStatusMask & ~roomStatusBits;

            float tankCapacity = math.max(1f, FiniteOr(oxygenTankCapacity, DefaultOxygenTankCapacity));
            float lowOxygenThreshold = math.saturate(FiniteOr(lowOxygenThreshold01, DefaultLowOxygenThreshold01)) * tankCapacity;
            bool hasOxygen = _o2Front.IsCreated;
            float oxygenValue = hasOxygen ? FiniteNonNegativeOrZero(_o2Front[roomIndex]) : tankCapacity;
            bool carbonDioxideToxic = ResolveRoomCarbonDioxidePressureFraction(roomIndex) >= DefaultCarbonDioxideToxicityFraction;
            if ((hasOxygen && oxygenValue < lowOxygenThreshold) || carbonDioxideToxic)
                statusMask |= roomBit << RoomStatusToxicShift;

            float roomTemperature = _temperatureFront.IsCreated ? GetRoomTemperatureCelsius(roomIndex) : FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius);
            ResolveSafeTemperatureBounds(out float minTemperature, out float maxTemperature);
            if (_temperatureFront.IsCreated && roomTemperature <= FiniteClampedOr(freezingRoomTemperatureCelsius, DefaultFreezingRoomTemperatureCelsius, minTemperature, maxTemperature))
                statusMask |= roomBit << RoomStatusFreezingShift;

            float referencePressure = ResolveSafeReferencePressureKPa();
            float pressureThreshold = math.max(referencePressure, FiniteOr(highPressureEventThresholdKPa, referencePressure));
            if (_pressureFront.IsCreated &&
                FiniteClampedOr(_pressureFront[roomIndex], referencePressure, 0f, ResolveSafeMaximumPressureKPa()) >= pressureThreshold)
            {
                statusMask |= roomBit << RoomStatusPressureShift;
            }

            return statusMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ResolveRoomStatusBits(uint roomBit)
        {
            return (roomBit << RoomStatusToxicShift) |
                   (roomBit << RoomStatusFreezingShift) |
                   (roomBit << RoomStatusPressureShift);
        }

        private float ResolveInstantPressureWithTemperature(float totalGasUnits, float gasVolumeCubicMeters, float temperatureCelsius)
        {
            return ResolveSimplePressureKPa(gasVolumeCubicMeters, 0f, 0f, temperatureCelsius);
        }

        private float ResolveRoomMaxOxygenCapacityUnits(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCount)
                return 0f;

            return math.max(1f, FiniteOr(oxygenTankCapacity, DefaultOxygenTankCapacity));
        }

        private float ResolveEmergencyVentThresholdPressureKPa()
        {
            float pressureCap = ResolveSafeMaximumPressureKPa();
            float ratioThreshold = math.saturate(FiniteOr(steamVentMinimumPressureRatio, DefaultSteamVentMinimumPressureRatio));
            float hullThreshold = fluidDynamics != null ? fluidDynamics.HullPressureRatingKPa : pressureCap;
            return math.min(FiniteOr(hullThreshold, pressureCap), pressureCap * math.max(0.1f, ratioThreshold));
        }

        private bool TryResolveEmergencyVentPipe(int roomIndex, out LogisticsPipeNode ventPipe)
        {
            ventPipe = null;
            if (roomIndex < 0 || roomIndex >= RoomCapacity)
                return false;

            SeedEmergencyVentPipesIfNeeded();

            uint roomBit = 1u << roomIndex;
            if ((_emergencyVentRoomMask & roomBit) == 0u)
                return false;

            LogisticsPipeNode cachedPipe = _emergencyVentPipesByRoom[roomIndex];
            if (cachedPipe == null || !cachedPipe.CanEmergencyVent)
            {
                _emergencyVentRoomMask &= ~roomBit;
                _emergencyVentPipesByRoom[roomIndex] = null;
                return false;
            }

            ventPipe = cachedPipe;
            return true;
        }

        private void SeedEmergencyVentPipesIfNeeded()
        {
            if (_emergencyVentPipesSeeded || fluidDynamics == null || !_topologySeeded)
                return;

            _emergencyVentRoomMask = 0u;
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
                _emergencyVentPipesByRoom[roomIndex] = null;

            _ventPipeScanBuffer.Clear();
            GetComponentsInChildren(true, _ventPipeScanBuffer);
            int pipeCount = _ventPipeScanBuffer.Count;
            for (int pipeIndex = 0; pipeIndex < pipeCount; pipeIndex++)
            {
                LogisticsPipeNode pipe = _ventPipeScanBuffer[pipeIndex];
                if (pipe == null || !pipe.CanEmergencyVent)
                    continue;

                int roomIndex = pipe.ResolveAmbientRoomIndex();
                if (roomIndex < 0 || roomIndex >= RoomCapacity)
                    continue;

                uint roomBit = 1u << roomIndex;
                if ((_emergencyVentRoomMask & roomBit) != 0u)
                    continue;

                _emergencyVentPipesByRoom[roomIndex] = pipe;
                _emergencyVentRoomMask |= roomBit;
            }

            _ventPipeScanBuffer.Clear();
            _emergencyVentPipesSeeded = true;
        }

        private void CacheReferences()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            if (fluidDynamics == null)
                TryGetComponent(out fluidDynamics);

            if (_submarineBody == null && fluidDynamics != null)
                fluidDynamics.TryGetComponent(out _submarineBody);

            if (_playerTransform == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                if (playerContext != null)
                {
                    _playerTransform = playerContext.PlayerTransform;
                    _playerCamera = playerContext.PlayerCamera;
                }
            }
            else if (_playerCamera == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                if (playerContext != null)
                    _playerCamera = playerContext.PlayerCamera;
            }
        }

        private void SeedBoilingHazardIds()
        {
            int instanceId = unchecked((int)EntityId.ToULong(GetEntityId()));
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
                _boilingHazardIds[roomIndex] = (instanceId * 97) ^ (0x61A0 + roomIndex);
        }

        private void SeedAtmosphereHazardIds()
        {
            int instanceId = unchecked((int)EntityId.ToULong(GetEntityId()));
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                _toxicRoomHazardIds[roomIndex] = (instanceId * 131) ^ (0xA70C + roomIndex);
                _fireSmokeHazardIds[roomIndex] = (instanceId * 149) ^ (0x5A10 + roomIndex);
            }
        }

        private void PublishAtmosphereFakes(float fixedDeltaTime)
        {
            float safeDeltaTime = FiniteNonNegativeOrZero(fixedDeltaTime);
            if (safeDeltaTime > 0f)
            {
                _lowOxygenAudioCooldownRemaining = math.max(0f, FiniteNonNegativeOrZero(_lowOxygenAudioCooldownRemaining) - safeDeltaTime);
                _pressureScreechCooldownRemaining = math.max(0f, FiniteNonNegativeOrZero(_pressureScreechCooldownRemaining) - safeDeltaTime);
                _toxicRoomVisorPulseCooldownRemaining = math.max(0f, FiniteNonNegativeOrZero(_toxicRoomVisorPulseCooldownRemaining) - safeDeltaTime);
            }

            if (fluidDynamics == null || !_o2Front.IsCreated || !_temperatureFront.IsCreated || !_roomStatusMaskFront.IsCreated)
            {
                ClearAtmosphereFakes();
                return;
            }

            int roomCount = RoomCount;
            float tankCapacity = math.max(1f, FiniteOr(oxygenTankCapacity, DefaultOxygenTankCapacity));
            float lowOxygenValue = math.saturate(FiniteOr(lowOxygenThreshold01, DefaultLowOxygenThreshold01)) * tankCapacity;
            uint runtimeStatusMask = _roomStatusMaskFront[0] & ResolveActiveRoomStatusMask(roomCount);
            bool smokeOverlayActive = false;
            Vector4 smokeOverlayParams = Vector4.zero;
            Vector4 smokeOverlayCenter = AtmosphereSootDefaultCenter;
            bool hasOccupiedRoomOxygen = false;
            float occupiedRoomOxygen01 = 1f;
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                if (roomIndex >= roomCount)
                {
                    UnregisterToxicRoomHazard(roomIndex);
                    UnregisterFireSmokeHazard(roomIndex);
                    ResetOverheatVisual(roomIndex);
                    continue;
                }

                float oxygenValue = FiniteNonNegativeOrZero(_o2Front[roomIndex]);

                uint roomBit = 1u << roomIndex;
                bool toxicRoom = (runtimeStatusMask & (roomBit << RoomStatusToxicShift)) != 0u;
                bool lowOxygen = oxygenValue < lowOxygenValue;
                bool pressureStatus = (runtimeStatusMask & (roomBit << RoomStatusPressureShift)) != 0u;
                float carbonDioxideDanger01 = ResolveCarbonDioxideToxicity01(ResolveRoomCarbonDioxidePressureFraction(roomIndex));

                bool playerOccupied = _roomPlayerCounts.IsCreated && _roomPlayerCounts[roomIndex] > 0;
                if (playerOccupied)
                {
                    hasOccupiedRoomOxygen = true;
                    occupiedRoomOxygen01 = math.saturate(oxygenValue / tankCapacity);
                }

                float oxygenDanger01 = lowOxygen
                    ? math.saturate((lowOxygenValue - oxygenValue) / math.max(1f, lowOxygenValue))
                    : 0f;
                float toxicityDanger01 = toxicRoom
                    ? math.max(0.1f, math.max(oxygenDanger01, carbonDioxideDanger01))
                    : 0f;
                if (toxicRoom && playerOccupied)
                {
                    if (lowOxygen)
                        TryPlayLowOxygenGaspingAudioLog();
                    TryPulseToxicRoomVisor(toxicityDanger01);
                }

                BaseModule roomModule = ResolveModuleForRoom(roomIndex);
                bool fireSmoke = roomModule != null && roomModule.CurrentFailureMode == BaseModuleFailureMode.Fire;
                if (fireSmoke)
                    runtimeStatusMask |= roomBit << RoomStatusFireShift;

                bool hasBounds = TryResolveRoomHazardBounds(roomIndex, roomModule, out Vector3 worldCenter, out float radius);

                if (toxicRoom && hasBounds)
                {
                    RegisterToxicRoomHazard(roomIndex, worldCenter, toxicityDanger01 * FiniteNonNegativeOrZero(toxicRoomHazardIntensity), radius);
                }
                else
                {
                    UnregisterToxicRoomHazard(roomIndex);
                }

                if (fireSmoke && hasBounds)
                {
                    RegisterFireSmokeHazard(
                        roomIndex,
                        worldCenter,
                        FiniteNonNegativeOrZero(fireSmokeHazardIntensity),
                        radius,
                        math.max(1f, FiniteOr(fireSmokeVisorGlitchBias, DefaultFireSmokeVisorGlitchBias)));

                    if (playerOccupied)
                        AccumulateSmokeOverlayFake(worldCenter, radius, ref smokeOverlayActive, ref smokeOverlayParams, ref smokeOverlayCenter);
                }
                else
                {
                    UnregisterFireSmokeHazard(roomIndex);
                }

                if (pressureStatus)
                    TryPlayPressureScreech(roomIndex, hasBounds ? worldCenter : ResolveRoomRuntimePosition(roomIndex));

                ApplyOverheatVoltageFake(roomIndex, roomModule);
            }

            PublishOccupiedRoomOxygenHud(hasOccupiedRoomOxygen, occupiedRoomOxygen01);
            _runtimeRoomStatusMask = runtimeStatusMask;
            PublishSmokeOverlayRuntimeState(smokeOverlayActive, in smokeOverlayParams, in smokeOverlayCenter);
        }

        private static void PublishOccupiedRoomOxygenHud(bool hasOccupiedRoomOxygen, float oxygen01)
        {
            if (!hasOccupiedRoomOxygen)
            {
                UIStateStore.ClearValue(UIValueSlotId.RoomOxygen01);
                return;
            }

            if (!math.isfinite(oxygen01))
            {
                UIStateStore.ClearValue(UIValueSlotId.RoomOxygen01);
                return;
            }

            UIStateStore.WriteValue(UIValueSlotId.RoomOxygen01, math.saturate(oxygen01), Time.unscaledTime);
        }

        private void ClearAtmosphereFakes()
        {
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                UnregisterToxicRoomHazard(roomIndex);
                UnregisterFireSmokeHazard(roomIndex);
                ResetOverheatVisual(roomIndex);
            }

            _runtimeRoomStatusMask = 0u;
            ClearRoomStatusMasks();
            _lowOxygenAudioCooldownRemaining = 0f;
            _pressureScreechCooldownRemaining = 0f;
            _toxicRoomVisorPulseCooldownRemaining = 0f;
            UIStateStore.ClearValue(UIValueSlotId.RoomOxygen01);
            PublishSmokeOverlayRuntimeState(false, default, default);
            ClearRoomModuleCache();
            ClearBrownoutRoomModuleCache();
        }

        private void RegisterToxicRoomHazard(int roomIndex, Vector3 worldCenter, float intensity, float radius)
        {
            if (roomIndex < 0 || roomIndex >= RoomCapacity)
                return;

            uint roomBit = 1u << roomIndex;
            if (HectonHazardManager.Register(
                    _toxicRoomHazardIds[roomIndex],
                    worldCenter,
                    FiniteNonNegativeOrZero(intensity),
                    FiniteNonNegativeOrZero(radius),
                    HazardType.Toxicity,
                    1f))
            {
                _toxicRoomHazardActiveMask |= roomBit;
                return;
            }

            UnregisterToxicRoomHazard(roomIndex);
        }

        private void UnregisterToxicRoomHazard(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCapacity)
                return;

            uint roomBit = 1u << roomIndex;
            if ((_toxicRoomHazardActiveMask & roomBit) == 0u)
                return;

            HectonHazardManager.Unregister(_toxicRoomHazardIds[roomIndex]);
            _toxicRoomHazardActiveMask &= ~roomBit;
        }

        private void RegisterFireSmokeHazard(int roomIndex, Vector3 worldCenter, float intensity, float radius, float visorGlitchBias)
        {
            if (roomIndex < 0 || roomIndex >= RoomCapacity)
                return;

            uint roomBit = 1u << roomIndex;
            if (HectonHazardManager.Register(
                    _fireSmokeHazardIds[roomIndex],
                    worldCenter,
                    FiniteNonNegativeOrZero(intensity),
                    FiniteNonNegativeOrZero(radius),
                    HazardType.Toxicity,
                    math.max(1f, FiniteOr(visorGlitchBias, DefaultFireSmokeVisorGlitchBias))))
            {
                _fireSmokeHazardActiveMask |= roomBit;
                return;
            }

            UnregisterFireSmokeHazard(roomIndex);
        }

        private void UnregisterFireSmokeHazard(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCapacity)
                return;

            uint roomBit = 1u << roomIndex;
            if ((_fireSmokeHazardActiveMask & roomBit) == 0u)
                return;

            HectonHazardManager.Unregister(_fireSmokeHazardIds[roomIndex]);
            _fireSmokeHazardActiveMask &= ~roomBit;
        }

        private void TryPlayLowOxygenGaspingAudioLog()
        {
            if (_lowOxygenAudioCooldownRemaining > 0f)
                return;

            if (lowOxygenGaspingAudioLog == null)
            {
                _lowOxygenAudioCooldownRemaining = FiniteNonNegativeOrZero(lowOxygenAudioCooldownSeconds);
                return;
            }

            AudioLogSystem audioLogs = GlobalRegistry.AudioLogs;
            if (audioLogs == null)
            {
                _lowOxygenAudioCooldownRemaining = FiniteNonNegativeOrZero(lowOxygenAudioCooldownSeconds);
                return;
            }

            audioLogs.PlayLog(lowOxygenGaspingAudioLog);
            _lowOxygenAudioCooldownRemaining = FiniteNonNegativeOrZero(lowOxygenAudioCooldownSeconds);
        }

        private void TryPulseToxicRoomVisor(float oxygenDanger01)
        {
            if (_toxicRoomVisorPulseCooldownRemaining > 0f)
                return;

            IPlayerSensoryService sensoryService = GlobalRegistry.PlayerSensory;
            VisorHUDController visorController = sensoryService != null ? sensoryService.VisorController : null;
            if (visorController == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                visorController = playerContext != null ? playerContext.VisorController : null;
            }

            if (visorController == null)
            {
                _toxicRoomVisorPulseCooldownRemaining = FiniteNonNegativeOrZero(toxicRoomVisorPulseCooldownSeconds);
                return;
            }

            float intensity = math.saturate(oxygenDanger01);
            visorController.GlitchPulse(DefaultToxicRoomVisorGlitchDurationSeconds + (intensity * 0.08f));
            visorController.TriggerEnvironmentalDistortion(
                math.saturate(0.24f + (intensity * 0.48f)),
                DefaultToxicRoomVisorDistortionHoldSeconds,
                DefaultToxicRoomVisorDistortionRecovery);
            _toxicRoomVisorPulseCooldownRemaining = FiniteNonNegativeOrZero(toxicRoomVisorPulseCooldownSeconds);
        }

        private void TryPlayPressureScreech(int roomIndex, Vector3 worldCenter)
        {
            if (_pressureScreechCooldownRemaining > 0f)
                return;

            AudioClip[] clips = pressureScreechClips;
            int clipCount = clips != null ? clips.Length : 0;
            if (clipCount <= 0)
            {
                _pressureScreechCooldownRemaining = FiniteNonNegativeOrZero(pressureScreechCooldownSeconds);
                return;
            }

            IAudioService audioService = GlobalRegistry.Audio;
            if (audioService == null)
            {
                _pressureScreechCooldownRemaining = FiniteNonNegativeOrZero(pressureScreechCooldownSeconds);
                return;
            }

            uint random = NextPressureScreechRandom();
            int clipIndex = (int)(random % (uint)clipCount);
            AudioClip clip = null;
            for (int i = 0; i < clipCount; i++)
            {
                clip = clips[(clipIndex + i) % clipCount];
                if (clip != null)
                    break;
            }

            if (clip == null)
            {
                _pressureScreechCooldownRemaining = FiniteNonNegativeOrZero(pressureScreechCooldownSeconds);
                return;
            }

            float referencePressure = ResolveSafeReferencePressureKPa();
            float maximumPressure = ResolveSafeMaximumPressureKPa();
            float pressure = _pressureFront.IsCreated && roomIndex >= 0 && roomIndex < RoomCapacity
                ? FiniteClampedOr(_pressureFront[roomIndex], referencePressure, 0f, maximumPressure)
                : math.max(referencePressure, FiniteOr(highPressureEventThresholdKPa, referencePressure));
            float pressureThreshold = math.max(referencePressure, FiniteOr(highPressureEventThresholdKPa, referencePressure));
            float pressureRange = math.max(1f, maximumPressure - pressureThreshold);
            float pressure01 = math.saturate((pressure - pressureThreshold) / pressureRange);
            float resolvedVolume = math.saturate(FiniteOr(pressureScreechVolume, DefaultPressureScreechVolume)) * math.lerp(0.55f, 1f, pressure01);
            float minPitch = math.min(FiniteOr(pressureScreechPitchMin, DefaultPressureScreechPitchMin), FiniteOr(pressureScreechPitchMax, DefaultPressureScreechPitchMax));
            float maxPitch = math.max(FiniteOr(pressureScreechPitchMin, DefaultPressureScreechPitchMin), FiniteOr(pressureScreechPitchMax, DefaultPressureScreechPitchMax));
            float pitchT = (NextPressureScreechRandom() & 0x00FFFFFFu) * (1f / 16777215f);
            audioService.PlayAtPoint(clip, worldCenter, resolvedVolume, math.lerp(minPitch, maxPitch, pitchT));
            _pressureScreechCooldownRemaining = FiniteNonNegativeOrZero(pressureScreechCooldownSeconds);
        }

        private void AccumulateSmokeOverlayFake(
            Vector3 worldCenter,
            float radius,
            ref bool smokeOverlayActive,
            ref Vector4 smokeOverlayParams,
            ref Vector4 smokeOverlayCenter)
        {
            float intensity = math.saturate(FiniteOr(fireSmokeHazardIntensity, DefaultFireSmokeHazardIntensity));
            if (intensity <= 0.001f)
                return;

            float screenRadius = math.saturate(math.max(0.01f, FiniteNonNegativeOrZero(radius) * FiniteNonNegativeOrZero(fireSmokeSootScreenRadiusScale)));
            if (!smokeOverlayActive || intensity > smokeOverlayParams.x)
            {
                smokeOverlayActive = true;
                smokeOverlayParams = new Vector4(
                    intensity,
                    screenRadius,
                    math.saturate(FiniteOr(fireSmokeSootDitherStrength, DefaultFireSmokeSootDitherStrength)),
                    math.saturate(FiniteOr(fireSmokeSootDarkenStrength, DefaultFireSmokeSootDarkenStrength)));
                smokeOverlayCenter = TryResolveSmokeOverlayCenter(worldCenter, out Vector4 viewportCenter)
                    ? viewportCenter
                    : AtmosphereSootDefaultCenter;
            }
        }

        private bool TryResolveSmokeOverlayCenter(Vector3 worldCenter, out Vector4 viewportCenter)
        {
            viewportCenter = default;
            Camera playerCamera = _playerCamera;
            if (playerCamera == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                if (playerContext != null)
                {
                    playerCamera = playerContext.PlayerCamera;
                    _playerCamera = playerCamera;
                }
            }

            if (playerCamera == null)
                return false;

            Vector3 viewportPoint = playerCamera.WorldToViewportPoint(worldCenter);
            if (!math.isfinite(viewportPoint.x) || !math.isfinite(viewportPoint.y) || viewportPoint.z <= 0f)
                return false;

            viewportCenter = new Vector4(
                math.saturate(viewportPoint.x),
                math.saturate(viewportPoint.y),
                0f,
                0f);
            return true;
        }

        private void PublishSmokeOverlayRuntimeState(bool active, in Vector4 smokeOverlayParams, in Vector4 smokeOverlayCenter)
        {
            if (!active)
            {
                if (!_smokeOverlayRuntimeActive && !_smokeOverlayRuntimeDirty)
                    return;

                HectonAtmosphereSootFeature.PublishRuntimeState(false, default, default);
                _lastSmokeOverlayParams = Vector4.zero;
                _lastSmokeOverlayCenter = AtmosphereSootDefaultCenter;
                _smokeOverlayRuntimeActive = false;
                _smokeOverlayRuntimeDirty = false;
                return;
            }

            if (_smokeOverlayRuntimeActive &&
                !_smokeOverlayRuntimeDirty &&
                Vector4Equals(_lastSmokeOverlayParams, smokeOverlayParams) &&
                Vector4Equals(_lastSmokeOverlayCenter, smokeOverlayCenter))
            {
                return;
            }

            HectonAtmosphereSootFeature.PublishRuntimeState(true, in smokeOverlayParams, in smokeOverlayCenter);
            _lastSmokeOverlayParams = smokeOverlayParams;
            _lastSmokeOverlayCenter = smokeOverlayCenter;
            _smokeOverlayRuntimeActive = true;
            _smokeOverlayRuntimeDirty = false;
        }

        private uint NextPressureScreechRandom()
        {
            uint state = _pressureScreechRngState != 0u ? _pressureScreechRngState : PressureScreechRngSeed;
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            _pressureScreechRngState = state != 0u ? state : PressureScreechRngSeed;
            return _pressureScreechRngState;
        }

        private static bool Vector4Equals(Vector4 left, Vector4 right)
        {
            return left.x == right.x &&
                   left.y == right.y &&
                   left.z == right.z &&
                   left.w == right.w;
        }

        private static uint ResolveActiveRoomStatusMask(int roomCount)
        {
            int safeRoomCount = math.clamp(roomCount, 0, RoomCapacity);
            uint roomMask = safeRoomCount > 0 ? ((1u << safeRoomCount) - 1u) : 0u;
            return (roomMask << RoomStatusToxicShift) |
                   (roomMask << RoomStatusFreezingShift) |
                   (roomMask << RoomStatusPressureShift) |
                   (roomMask << RoomStatusFireShift);
        }

        private void ClearRoomStatusMasks()
        {
            if (_roomStatusMaskFront.IsCreated)
                _roomStatusMaskFront[0] = 0u;

            if (_roomStatusMaskBack.IsCreated)
                _roomStatusMaskBack[0] = 0u;
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && math.isfinite(value);
        }

        private void ApplyOverheatVoltageFake(int roomIndex, BaseModule roomModule)
        {
            if (roomIndex < 0 || roomIndex >= RoomCapacity || !_temperatureFront.IsCreated)
                return;

            ResolveSafeTemperatureBounds(out _, out float maximumTemperature);
            float threshold = FiniteOr(overheatBrownoutTemperatureCelsius, DefaultOverheatBrownoutTemperatureCelsius);
            float temperature = GetRoomTemperatureCelsius(roomIndex);
            if (roomModule == null || temperature <= threshold)
            {
                ResetOverheatVisual(roomIndex);
                return;
            }

            float heat01 = math.saturate((temperature - threshold) / math.max(1f, maximumTemperature - threshold));
            float voltage = math.lerp(1f, math.saturate(FiniteOr(overheatMinimumVoltage, DefaultOverheatMinimumVoltage)), math.saturate(heat01));
            roomModule.SetAmbientPowerVisualState(true, voltage);
            _overheatVisualActiveMask |= 1u << roomIndex;
        }

        private void ResetOverheatVisual(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCapacity)
                return;

            uint roomBit = 1u << roomIndex;
            if ((_overheatVisualActiveMask & roomBit) == 0u)
                return;

            BaseModule module = _atmosphereRoomModules[roomIndex];
            if (module != null)
            {
                bool powerBrownout = module.CachedPowerSupplyRatio < math.saturate(FiniteOr(brownoutOxygenSupplyRatioThreshold, DefaultBrownoutOxygenSupplyRatioThreshold));
                module.SetAmbientPowerVisualState(powerBrownout, powerBrownout ? module.CachedPowerSupplyRatio : 1f);
            }

            _overheatVisualActiveMask &= ~roomBit;
        }

        private BaseModule ResolveModuleForRoom(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCapacity)
                return null;

            BaseModule cachedModule = _atmosphereRoomModules[roomIndex];
            if (IsModuleMappedToRoom(cachedModule, roomIndex))
                return cachedModule;

            int moduleCount = BaseModule.ActiveModuleCount;
            for (int moduleIndex = 0; moduleIndex < moduleCount; moduleIndex++)
            {
                BaseModule candidate = BaseModule.GetActiveModuleAt(moduleIndex);
                if (!IsModuleMappedToRoom(candidate, roomIndex))
                    continue;

                _atmosphereRoomModules[roomIndex] = candidate;
                return candidate;
            }

            _atmosphereRoomModules[roomIndex] = null;
            return null;
        }

        private bool IsModuleMappedToRoom(BaseModule module, int roomIndex)
        {
            if (module == null || !module.TryGetInteriorAabbBounds(out Vector3 worldCenter, out _))
                return false;

            AbsoluteUniversePosition moduleAup = AbsoluteUniversePosition.FromRuntimePosition(worldCenter);
            return ResolveNearestRoomIndex(in moduleAup) == roomIndex;
        }

        private void TryRegister()
        {
            if (_registered)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Environment);
            GlobalRegistry.RegisterPostFixedTickable(this, PriorityLayer.Environment);
            _registered = SystemDispatcher.GetPostFixedLane(PriorityLayer.Environment).Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private void EnsureNativeState()
        {
            if (!TryFinalizeDeferredNativeDisposal())
                return;

            if (_roomVolumes.IsCreated)
                return;

            // COLD ALLOC: NativeArray<float>[8] - room gas-capacity snapshot aligned to submarine compartments - owner: SubmarineAtmosphereSystem
            _roomVolumes = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - flood-volume snapshot consumed by the atmosphere solver - owner: SubmarineAtmosphereSystem
            _floodVolumes = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - front O2 double buffer in reference-gas-volume units - owner: SubmarineAtmosphereSystem
            _o2Front = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - back O2 double buffer in reference-gas-volume units - owner: SubmarineAtmosphereSystem
            _o2Back = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - front CO2 double buffer in reference-gas-volume units - owner: SubmarineAtmosphereSystem
            _co2Front = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - back CO2 double buffer in reference-gas-volume units - owner: SubmarineAtmosphereSystem
            _co2Back = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - front inert-gas double buffer - owner: SubmarineAtmosphereSystem
            _inertFront = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - back inert-gas double buffer - owner: SubmarineAtmosphereSystem
            _inertBack = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - front room-pressure snapshot - owner: SubmarineAtmosphereSystem
            _pressureFront = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - back room-pressure snapshot - owner: SubmarineAtmosphereSystem
            _pressureBack = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - front O2 partial-pressure snapshot in kPa - owner: SubmarineAtmosphereSystem
            _o2PartialPressureFront = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - back O2 partial-pressure snapshot in kPa - owner: SubmarineAtmosphereSystem
            _o2PartialPressureBack = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - front CO2 partial-pressure snapshot in kPa - owner: SubmarineAtmosphereSystem
            _co2PartialPressureFront = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - back CO2 partial-pressure snapshot in kPa - owner: SubmarineAtmosphereSystem
            _co2PartialPressureBack = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - front N2 partial-pressure snapshot in kPa - owner: SubmarineAtmosphereSystem
            _n2PartialPressureFront = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - back N2 partial-pressure snapshot in kPa - owner: SubmarineAtmosphereSystem
            _n2PartialPressureBack = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - front available gas volume snapshot - owner: SubmarineAtmosphereSystem
            _gasVolumeFront = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - back available gas volume snapshot - owner: SubmarineAtmosphereSystem
            _gasVolumeBack = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - room O2 metabolic sink rates - owner: SubmarineAtmosphereSystem
            _o2ConsumptionRates = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - room CO2 metabolic source rates - owner: SubmarineAtmosphereSystem
            _co2GenerationRates = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<int>[8] - local player occupancy counts consumed by the cheap atmosphere job - owner: SubmarineAtmosphereSystem
            _roomPlayerCounts = new NativeArray<int>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - front room heat snapshot for cheap atmosphere solve - owner: SubmarineAtmosphereSystem
            _temperatureFront = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - back room heat snapshot for cheap atmosphere solve - owner: SubmarineAtmosphereSystem
            _temperatureBack = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - front steam phase accumulator for room VFX state - owner: SubmarineAtmosphereSystem
            _steamFront = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - back steam phase accumulator for room VFX state - owner: SubmarineAtmosphereSystem
            _steamBack = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - hydrogen-pocket accumulator for submerged overload electrolysis - owner: SubmarineAtmosphereSystem
            _hydrogenPocketFront = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - oxygen-pocket accumulator for submerged overload electrolysis - owner: SubmarineAtmosphereSystem
            _oxygenPocketFront = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] - room heat watt source cache consumed by Burst atmosphere job - owner: SubmarineAtmosphereSystem
            _roomHeatWatts = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<uint>[1] - front packed Safe/Toxic/Freezing/Pressure/Fire room status bitmask - owner: SubmarineAtmosphereSystem
            _roomStatusMaskFront = new NativeArray<uint>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<uint>[1] - back packed Safe/Toxic/Freezing/Pressure/Fire room status bitmask - owner: SubmarineAtmosphereSystem
            _roomStatusMaskBack = new NativeArray<uint>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<int2>[7] - door graph edges aligned to submarine bulkheads - owner: SubmarineAtmosphereSystem
            _doorPairs = new NativeArray<int2>(DoorCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<byte>[7] - sealed-door state copied from submarine bulkheads - owner: SubmarineAtmosphereSystem
            _doorSealed = new NativeArray<byte>(DoorCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<byte>[7] - previous sealed-door state used for door-opening pressure warnings - owner: SubmarineAtmosphereSystem
            _doorSealedPrevious = new NativeArray<byte>(DoorCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            RegisterNativeState();
        }

        private bool TryFinalizeDeferredNativeDisposal()
        {
            return DispatcherJobSwap.TryFinalizeCompleted(ref _disposeHandle);
        }

        private void RegisterNativeState()
        {
            RegisterNativeArray(_roomVolumes, nameof(_roomVolumes));
            RegisterNativeArray(_floodVolumes, nameof(_floodVolumes));
            RegisterNativeArray(_o2Front, nameof(_o2Front));
            RegisterNativeArray(_o2Back, nameof(_o2Back));
            RegisterNativeArray(_co2Front, nameof(_co2Front));
            RegisterNativeArray(_co2Back, nameof(_co2Back));
            RegisterNativeArray(_inertFront, nameof(_inertFront));
            RegisterNativeArray(_inertBack, nameof(_inertBack));
            RegisterNativeArray(_pressureFront, nameof(_pressureFront));
            RegisterNativeArray(_pressureBack, nameof(_pressureBack));
            RegisterNativeArray(_o2PartialPressureFront, nameof(_o2PartialPressureFront));
            RegisterNativeArray(_o2PartialPressureBack, nameof(_o2PartialPressureBack));
            RegisterNativeArray(_co2PartialPressureFront, nameof(_co2PartialPressureFront));
            RegisterNativeArray(_co2PartialPressureBack, nameof(_co2PartialPressureBack));
            RegisterNativeArray(_n2PartialPressureFront, nameof(_n2PartialPressureFront));
            RegisterNativeArray(_n2PartialPressureBack, nameof(_n2PartialPressureBack));
            RegisterNativeArray(_gasVolumeFront, nameof(_gasVolumeFront));
            RegisterNativeArray(_gasVolumeBack, nameof(_gasVolumeBack));
            RegisterNativeArray(_o2ConsumptionRates, nameof(_o2ConsumptionRates));
            RegisterNativeArray(_co2GenerationRates, nameof(_co2GenerationRates));
            RegisterNativeArray(_roomPlayerCounts, nameof(_roomPlayerCounts));
            RegisterNativeArray(_temperatureFront, nameof(_temperatureFront));
            RegisterNativeArray(_temperatureBack, nameof(_temperatureBack));
            RegisterNativeArray(_steamFront, nameof(_steamFront));
            RegisterNativeArray(_steamBack, nameof(_steamBack));
            RegisterNativeArray(_hydrogenPocketFront, nameof(_hydrogenPocketFront));
            RegisterNativeArray(_oxygenPocketFront, nameof(_oxygenPocketFront));
            RegisterNativeArray(_roomHeatWatts, nameof(_roomHeatWatts));
            RegisterNativeArray(_roomStatusMaskFront, nameof(_roomStatusMaskFront));
            RegisterNativeArray(_roomStatusMaskBack, nameof(_roomStatusMaskBack));
            RegisterNativeArray(_doorPairs, nameof(_doorPairs));
            RegisterNativeArray(_doorSealed, nameof(_doorSealed));
            RegisterNativeArray(_doorSealedPrevious, nameof(_doorSealedPrevious));
        }

        private void UnregisterNativeState()
        {
            UnregisterNativeArray(_roomVolumes);
            UnregisterNativeArray(_floodVolumes);
            UnregisterNativeArray(_o2Front);
            UnregisterNativeArray(_o2Back);
            UnregisterNativeArray(_co2Front);
            UnregisterNativeArray(_co2Back);
            UnregisterNativeArray(_inertFront);
            UnregisterNativeArray(_inertBack);
            UnregisterNativeArray(_pressureFront);
            UnregisterNativeArray(_pressureBack);
            UnregisterNativeArray(_o2PartialPressureFront);
            UnregisterNativeArray(_o2PartialPressureBack);
            UnregisterNativeArray(_co2PartialPressureFront);
            UnregisterNativeArray(_co2PartialPressureBack);
            UnregisterNativeArray(_n2PartialPressureFront);
            UnregisterNativeArray(_n2PartialPressureBack);
            UnregisterNativeArray(_gasVolumeFront);
            UnregisterNativeArray(_gasVolumeBack);
            UnregisterNativeArray(_o2ConsumptionRates);
            UnregisterNativeArray(_co2GenerationRates);
            UnregisterNativeArray(_roomPlayerCounts);
            UnregisterNativeArray(_temperatureFront);
            UnregisterNativeArray(_temperatureBack);
            UnregisterNativeArray(_steamFront);
            UnregisterNativeArray(_steamBack);
            UnregisterNativeArray(_hydrogenPocketFront);
            UnregisterNativeArray(_oxygenPocketFront);
            UnregisterNativeArray(_roomHeatWatts);
            UnregisterNativeArray(_roomStatusMaskFront);
            UnregisterNativeArray(_roomStatusMaskBack);
            UnregisterNativeArray(_doorPairs);
            UnregisterNativeArray(_doorSealed);
            UnregisterNativeArray(_doorSealedPrevious);
        }

        private static void RegisterNativeArray<T>(NativeArray<T> array, string label) where T : struct
        {
            NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime);
        }

        private static void UnregisterNativeArray<T>(NativeArray<T> array) where T : struct
        {
            NativeMemorySentinel.UnregisterNativeArray(array);
        }

        private void SeedTopologyIfNeeded()
        {
            if (_topologySeeded || fluidDynamics == null)
                return;

            int roomCount = RoomCount;
            if (roomCount <= 0)
                return;
            int doorCount = math.min(fluidDynamics.ConfiguredBulkheadCount, DoorCapacity);

            ResolveSafeTemperatureBounds(out float minTemperature, out float maxTemperature);
            float minimumGasVolume = math.max(0.001f, FiniteOr(minimumGasVolumeCubicMeters, DefaultMinimumGasVolumeCubicMeters));
            float tankCapacity = math.max(1f, FiniteOr(oxygenTankCapacity, DefaultOxygenTankCapacity));
            float seedReferenceTemperature = FiniteClampedOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius, minTemperature, maxTemperature);
            float seedReferencePressure = ResolveSafeReferencePressureKPa();
            float seedMaximumPressure = ResolveSafeMaximumPressureKPa();
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                if (roomIndex >= roomCount)
                {
                    _roomVolumes[roomIndex] = minimumGasVolume;
                    _gasVolumeFront[roomIndex] = minimumGasVolume;
                    _pressureFront[roomIndex] = seedReferencePressure;
                    _o2Front[roomIndex] = 0f;
                    _co2Front[roomIndex] = 0f;
                    _inertFront[roomIndex] = 0f;
                    _o2PartialPressureFront[roomIndex] = 0f;
                    _co2PartialPressureFront[roomIndex] = 0f;
                    _n2PartialPressureFront[roomIndex] = 0f;
                    _temperatureFront[roomIndex] = seedReferenceTemperature;
                    continue;
                }

                RoomDefinition definition = roomIndex < rooms.Length ? rooms[roomIndex] : default;
                float roomVolume = definition.gasCapacityOverrideCubicMeters > Epsilon && math.isfinite(definition.gasCapacityOverrideCubicMeters)
                    ? definition.gasCapacityOverrideCubicMeters
                    : fluidDynamics.GetCompartmentMaxFloodVolumeCubicMeters(roomIndex);
                roomVolume = math.max(FiniteOr(roomVolume, minimumGasVolume), minimumGasVolume);

                float oxygenFraction = math.saturate(definition.initialOxygenFraction > Epsilon && math.isfinite(definition.initialOxygenFraction) ? definition.initialOxygenFraction : DefaultInitialOxygenFraction);
                float carbonDioxideFraction = math.saturate(definition.initialCarbonDioxideFraction > 0f && math.isfinite(definition.initialCarbonDioxideFraction) ? definition.initialCarbonDioxideFraction : DefaultInitialCarbonDioxideFraction);
                if (oxygenFraction + carbonDioxideFraction > 0.95f)
                {
                    float scale = 0.95f / math.max(oxygenFraction + carbonDioxideFraction, Epsilon);
                    oxygenFraction *= scale;
                    carbonDioxideFraction *= scale;
                }

                _roomVolumes[roomIndex] = roomVolume;
                _gasVolumeFront[roomIndex] = roomVolume;
                float oxygenUnits = math.saturate(oxygenFraction / math.max(DefaultInitialOxygenFraction, Epsilon)) * tankCapacity;
                float carbonDioxideUnits = math.saturate(carbonDioxideFraction) * tankCapacity;
                float nitrogenUnits = math.saturate(1f - oxygenFraction - carbonDioxideFraction) * tankCapacity;
                _o2Front[roomIndex] = oxygenUnits;
                _co2Front[roomIndex] = carbonDioxideUnits;
                _inertFront[roomIndex] = nitrogenUnits;
                float initialTemperature = definition.initialTemperatureCelsius != 0f && math.isfinite(definition.initialTemperatureCelsius)
                    ? definition.initialTemperatureCelsius
                    : seedReferenceTemperature;
                initialTemperature = math.clamp(initialTemperature, minTemperature, maxTemperature);
                _temperatureFront[roomIndex] = initialTemperature;
                _pressureFront[roomIndex] = ResolveDaltonPressureKPa(
                    oxygenUnits,
                    carbonDioxideUnits,
                    nitrogenUnits,
                    0f,
                    roomVolume,
                    roomVolume,
                    initialTemperature,
                    seedReferencePressure,
                    seedMaximumPressure,
                    seedReferenceTemperature,
                    tankCapacity,
                    out float oxygenPartialPressureKPa,
                    out float carbonDioxidePartialPressureKPa,
                    out float nitrogenPartialPressureKPa);
                _o2PartialPressureFront[roomIndex] = oxygenPartialPressureKPa;
                _co2PartialPressureFront[roomIndex] = carbonDioxidePartialPressureKPa;
                _n2PartialPressureFront[roomIndex] = nitrogenPartialPressureKPa;
                _o2ConsumptionRates[roomIndex] = FiniteNonNegativeOrZero(definition.oxygenConsumptionUnitsPerSecond);
                _co2GenerationRates[roomIndex] = FiniteNonNegativeOrZero(definition.carbonDioxideGenerationUnitsPerSecond);
            }

            for (int doorIndex = 0; doorIndex < DoorCapacity; doorIndex++)
            {
                if (doorIndex < doorCount && fluidDynamics.TryGetBulkheadDefinition(doorIndex, out int compartmentA, out int compartmentB, out bool isSealed))
                {
                    _doorPairs[doorIndex] = new int2(compartmentA, compartmentB);
                    _doorSealed[doorIndex] = isSealed ? (byte)1 : (byte)0;
                    _doorSealedPrevious[doorIndex] = _doorSealed[doorIndex];
                    continue;
                }

                _doorPairs[doorIndex] = new int2(-1, -1);
                _doorSealed[doorIndex] = 1;
                _doorSealedPrevious[doorIndex] = 1;
            }

            _topologyRoomCount = roomCount;
            _topologyDoorCount = doorCount;
            _topologySeeded = true;
        }

        private void InvalidateTopologyIfShapeChanged()
        {
            if (!_topologySeeded || fluidDynamics == null)
                return;

            int roomCount = RoomCount;
            int doorCount = math.min(fluidDynamics.ConfiguredBulkheadCount, DoorCapacity);
            if (roomCount == _topologyRoomCount && doorCount == _topologyDoorCount)
                return;

            _topologySeeded = false;
            _thermalEmittersSeeded = false;
            _emergencyVentPipesSeeded = false;
            _emergencyVentRoomMask = 0u;
            _fabricatorHeatEmitterCount = 0;
            _drillHeatEmitterCount = 0;
            _reactorHeatEmitterCount = 0;
            _reactorMeltdownTriggeredMask = 0u;
            _topologyRoomCount = -1;
            _topologyDoorCount = -1;
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
                _emergencyVentPipesByRoom[roomIndex] = null;
            ClearRoomModuleCache();
            ClearBrownoutRoomModuleCache();
        }

        private void SeedThermalEmittersIfNeeded()
        {
            if (_thermalEmittersSeeded || fluidDynamics == null || !_topologySeeded)
                return;

            _fabricatorHeatEmitterCount = 0;
            _drillHeatEmitterCount = 0;
            _reactorHeatEmitterCount = 0;

            _fabricatorScanBuffer.Clear();
            GetComponentsInChildren(true, _fabricatorScanBuffer);
            for (int i = 0; i < _fabricatorScanBuffer.Count && _fabricatorHeatEmitterCount < HeatEmitterCapacity; i++)
            {
                Fabricator fabricator = _fabricatorScanBuffer[i];
                if (fabricator == null)
                    continue;

                _fabricatorHeatEmitters[_fabricatorHeatEmitterCount++] = new FabricatorHeatEmitter
                {
                    Fabricator = fabricator,
                    RoomIndex = ResolveThermalEmitterRoomIndex(fabricator)
                };
            }

            _drillScanBuffer.Clear();
            GetComponentsInChildren(true, _drillScanBuffer);
            for (int i = 0; i < _drillScanBuffer.Count && _drillHeatEmitterCount < HeatEmitterCapacity; i++)
            {
                DeepDrillModule drill = _drillScanBuffer[i];
                if (drill == null)
                    continue;

                _drillHeatEmitters[_drillHeatEmitterCount++] = new DrillHeatEmitter
                {
                    Drill = drill,
                    RoomIndex = ResolveThermalEmitterRoomIndex(drill)
                };
            }

            _reactorScanBuffer.Clear();
            GetComponentsInChildren(true, _reactorScanBuffer);
            for (int i = 0; i < _reactorScanBuffer.Count && _reactorHeatEmitterCount < HeatEmitterCapacity; i++)
            {
                BioReactor reactor = _reactorScanBuffer[i];
                if (reactor == null)
                    continue;

                _reactorHeatEmitters[_reactorHeatEmitterCount++] = new ReactorHeatEmitter
                {
                    Reactor = reactor,
                    RoomIndex = ResolveThermalEmitterRoomIndex(reactor)
                };
            }

            for (int i = _reactorHeatEmitterCount; i < HeatEmitterCapacity; i++)
                _reactorMeltdownTriggeredMask &= ~(1u << i);

            _thermalEmittersSeeded = true;
        }

        private int ResolveThermalEmitterRoomIndex(Component emitter)
        {
            if (emitter == null)
                return -1;

            BaseModule hostModule = emitter.GetComponentInParent<BaseModule>();
            if (hostModule != null)
            {
                int roomCount = math.min(RoomCount, RoomCapacity);
                for (int roomIndex = 0; roomIndex < roomCount; roomIndex++)
                {
                    if (ReferenceEquals(ResolveModuleForRoom(roomIndex), hostModule))
                        return roomIndex;
                }

                if (hostModule.TryGetInteriorAabbBounds(out Vector3 hostCenter, out _))
                {
                    AbsoluteUniversePosition hostAup = AbsoluteUniversePosition.FromRuntimePosition(hostCenter);
                    return ResolveNearestRoomIndex(in hostAup);
                }
            }

            return ResolveSubmarineCenterRoomIndex();
        }

        private int ResolveSubmarineCenterRoomIndex()
        {
            if (_submarineBody == null)
                return -1;

            AbsoluteUniversePosition submarineCenterAup = AbsoluteUniversePosition.FromRuntimePosition(_submarineBody.worldCenterOfMass);
            return ResolveNearestRoomIndex(in submarineCenterAup);
        }

        private void SyncFluidSnapshot()
        {
            if (fluidDynamics == null)
                return;

            int roomCount = RoomCount;
            float minimumGasVolume = math.max(0.001f, FiniteOr(minimumGasVolumeCubicMeters, DefaultMinimumGasVolumeCubicMeters));
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                if (_roomPlayerCounts.IsCreated)
                    _roomPlayerCounts[roomIndex] = 0;

                if (roomIndex >= roomCount)
                {
                    _roomVolumes[roomIndex] = minimumGasVolume;
                    _floodVolumes[roomIndex] = 0f;
                    _gasVolumeFront[roomIndex] = minimumGasVolume;
                    _o2ConsumptionRates[roomIndex] = 0f;
                    _co2GenerationRates[roomIndex] = 0f;
                    _roomHeatWatts[roomIndex] = 0f;
                    continue;
                }

                RoomDefinition definition = roomIndex < rooms.Length ? rooms[roomIndex] : default;
                float roomVolume = definition.gasCapacityOverrideCubicMeters > Epsilon && math.isfinite(definition.gasCapacityOverrideCubicMeters)
                    ? definition.gasCapacityOverrideCubicMeters
                    : fluidDynamics.GetCompartmentMaxFloodVolumeCubicMeters(roomIndex);
                _roomVolumes[roomIndex] = math.max(FiniteOr(roomVolume, minimumGasVolume), minimumGasVolume);
                _floodVolumes[roomIndex] = math.clamp(
                    FiniteNonNegativeOrZero(fluidDynamics.GetCompartmentFloodVolumeCubicMeters(roomIndex)),
                    0f,
                    _roomVolumes[roomIndex] - Epsilon);
                if (_gasVolumeFront.IsCreated)
                    _gasVolumeFront[roomIndex] = math.max(minimumGasVolume, _roomVolumes[roomIndex] - _floodVolumes[roomIndex]);
                float oxygenConsumptionRate = math.max(
                    FiniteNonNegativeOrZero(definition.oxygenConsumptionUnitsPerSecond),
                    FiniteNonNegativeOrZero(playerOxygenConsumptionPercentPerSecond));
                ApplyBrownoutOccupiedRoomOxygenDrain(roomIndex, ref oxygenConsumptionRate);
                _o2ConsumptionRates[roomIndex] = oxygenConsumptionRate;
                _co2GenerationRates[roomIndex] = FiniteNonNegativeOrZero(definition.carbonDioxideGenerationUnitsPerSecond);
            }

            if (_roomPlayerCounts.IsCreated && TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
            {
                int occupiedRoomIndex = ResolveNearestRoomIndex(in playerAup);
                if (occupiedRoomIndex >= 0 && occupiedRoomIndex < roomCount)
                    _roomPlayerCounts[occupiedRoomIndex] = 1;
            }

            int doorCount = math.min(fluidDynamics.ConfiguredBulkheadCount, DoorCapacity);
            for (int doorIndex = 0; doorIndex < DoorCapacity; doorIndex++)
            {
                if (doorIndex < doorCount && fluidDynamics.TryGetBulkheadDefinition(doorIndex, out int compartmentA, out int compartmentB, out bool isSealed))
                {
                    _doorPairs[doorIndex] = new int2(compartmentA, compartmentB);
                    _doorSealed[doorIndex] = isSealed ? (byte)1 : (byte)0;
                    continue;
                }

                _doorPairs[doorIndex] = new int2(-1, -1);
                _doorSealed[doorIndex] = 1;
            }
        }

        private void ApplyBrownoutOccupiedRoomOxygenDrain(int roomIndex, ref float oxygenConsumptionRate)
        {
            if (roomIndex < 0 || roomIndex >= RoomCapacity)
                return;

            if (!TryResolveBrownoutOccupiedModuleForRoom(roomIndex, out _))
                return;

            oxygenConsumptionRate = ResolveBrownoutOxygenConsumptionRate(
                oxygenConsumptionRate,
                brownoutOccupiedRoomOxygenConsumptionUnitsPerSecond);
        }

        private bool TryResolveBrownoutOccupiedModuleForRoom(int roomIndex, out BaseModule module)
        {
            module = null;
            if (roomIndex < 0 || roomIndex >= RoomCapacity)
                return false;

            BaseModule cachedModule = _brownoutRoomModules[roomIndex];
            if (IsBrownoutOccupiedModuleCandidate(cachedModule, roomIndex))
            {
                module = cachedModule;
                return true;
            }

            int moduleCount = BaseModule.ActiveModuleCount;
            for (int moduleIndex = 0; moduleIndex < moduleCount; moduleIndex++)
            {
                BaseModule candidate = BaseModule.GetActiveModuleAt(moduleIndex);
                if (!IsBrownoutOccupiedModuleCandidate(candidate, roomIndex))
                    continue;

                _brownoutRoomModules[roomIndex] = candidate;
                module = candidate;
                return true;
            }

            _brownoutRoomModules[roomIndex] = null;
            return false;
        }

        private bool IsBrownoutOccupiedModuleCandidate(BaseModule module, int roomIndex)
        {
            if (module == null)
                return false;

            if (!IsModuleMappedToRoom(module, roomIndex))
                return false;

            return ShouldSiphonOxygenDuringBrownout(
                module.CachedPowerSupplyRatio,
                brownoutOxygenSupplyRatioThreshold,
                IsPlayerInsideModuleAabb(module));
        }

        internal static bool ShouldSiphonOxygenDuringBrownout(float supplyRatio, float threshold, bool playerInsideModule)
        {
            return playerInsideModule && math.saturate(FiniteOr(supplyRatio, 1f)) < math.saturate(FiniteOr(threshold, DefaultBrownoutOxygenSupplyRatioThreshold));
        }

        internal static float ResolveBrownoutOxygenConsumptionRate(float currentConsumptionRate, float brownoutDrainRate)
        {
            return math.max(FiniteNonNegativeOrZero(currentConsumptionRate), FiniteNonNegativeOrZero(brownoutDrainRate));
        }

        private bool IsPlayerInsideModuleAabb(BaseModule module)
        {
            if (module == null)
                return false;

            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup) ||
                !module.TryGetInteriorAabbBounds(out Vector3 worldCenter, out Vector3 halfExtents))
                return false;

            float3 playerRuntime = playerAup.ToRuntimeFloat3();
            Vector3 delta = new Vector3(playerRuntime.x, playerRuntime.y, playerRuntime.z) - worldCenter;
            return math.abs(delta.x) <= FiniteNonNegativeOrZero(halfExtents.x) &&
                   math.abs(delta.y) <= FiniteNonNegativeOrZero(halfExtents.y) &&
                   math.abs(delta.z) <= FiniteNonNegativeOrZero(halfExtents.z);
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext == null || playerContext.PlayerMovement == null)
                return false;

            _playerTransform = playerContext.PlayerTransform;
            _playerCamera = playerContext.PlayerCamera;
            playerAup = playerContext.PlayerMovement.CurrentAup;
            return true;
        }

        private void AccumulateRoomHeatSources()
        {
            if (!_roomHeatWatts.IsCreated || fluidDynamics == null)
                return;

            int roomCount = RoomCount;
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                if (roomIndex >= roomCount)
                {
                    _roomHeatWatts[roomIndex] = 0f;
                    continue;
                }

                RoomDefinition definition = roomIndex < rooms.Length ? rooms[roomIndex] : default;
                _roomHeatWatts[roomIndex] = FiniteNonNegativeOrZero(definition.passiveHeatWatts);
            }

            for (int i = 0; i < _fabricatorHeatEmitterCount; i++)
            {
                FabricatorHeatEmitter emitter = _fabricatorHeatEmitters[i];
                if (emitter.Fabricator == null || emitter.RoomIndex < 0 || emitter.RoomIndex >= roomCount)
                    continue;

                if (emitter.Fabricator.IsCrafting)
                    _roomHeatWatts[emitter.RoomIndex] += math.abs(FiniteOr(emitter.Fabricator.PowerRating, 0f)) * FiniteNonNegativeOrZero(fabricatorHeatWattsScale);
            }

            for (int i = 0; i < _drillHeatEmitterCount; i++)
            {
                DrillHeatEmitter emitter = _drillHeatEmitters[i];
                if (emitter.Drill == null || emitter.RoomIndex < 0 || emitter.RoomIndex >= roomCount)
                    continue;

                _roomHeatWatts[emitter.RoomIndex] += math.abs(FiniteOr(emitter.Drill.PowerRating, 0f)) * FiniteNonNegativeOrZero(drillHeatWattsScale);
            }

            for (int i = 0; i < _reactorHeatEmitterCount; i++)
            {
                ReactorHeatEmitter emitter = _reactorHeatEmitters[i];
                if (emitter.Reactor == null || emitter.RoomIndex < 0 || emitter.RoomIndex >= roomCount)
                    continue;

                _roomHeatWatts[emitter.RoomIndex] += FiniteNonNegativeOrZero(emitter.Reactor.PowerRating) * FiniteNonNegativeOrZero(reactorHeatWattsScale);
            }
        }

        private void EvaluateReactorMeltdowns()
        {
            if (_submarineBody == null || fluidDynamics == null || !_temperatureFront.IsCreated || !_floodVolumes.IsCreated || !_roomVolumes.IsCreated)
                return;

            ResolveSafeTemperatureBounds(out _, out float maximumTemperature);
            float thresholdTemperature = math.min(maximumTemperature, math.max(DefaultReactorMeltdownTemperatureCelsius, FiniteOr(reactorMeltdownTemperatureCelsius, DefaultReactorMeltdownTemperatureCelsius)));
            float minimumImpulse = math.max(1f, FiniteOr(reactorMeltdownMinimumImpulseNewtonSeconds, DefaultReactorMeltdownMinimumImpulseNewtonSeconds));
            float maximumImpulse = math.max(minimumImpulse, FiniteOr(reactorMeltdownMaximumImpulseNewtonSeconds, DefaultReactorMeltdownMaximumImpulseNewtonSeconds));
            float upwardBias = math.saturate(FiniteOr(reactorMeltdownUpwardBias, DefaultReactorMeltdownUpwardBias));
            float impulseDuration = math.max(0.001f, FiniteOr(reactorMeltdownImpulseDurationSeconds, DefaultReactorMeltdownImpulseDurationSeconds));
            float impulsePerWattSecond = FiniteNonNegativeOrZero(reactorMeltdownImpulsePerWattSecond);
            float floodAmplification = math.max(1f, FiniteOr(reactorMeltdownFloodAmplification, DefaultReactorMeltdownFloodAmplification));

            for (int emitterIndex = 0; emitterIndex < _reactorHeatEmitterCount; emitterIndex++)
            {
                ReactorHeatEmitter emitter = _reactorHeatEmitters[emitterIndex];
                if (emitter.Reactor == null || emitter.RoomIndex < 0 || emitter.RoomIndex >= RoomCount)
                    continue;

                uint emitterBit = 1u << emitterIndex;
                if ((_reactorMeltdownTriggeredMask & emitterBit) != 0u)
                    continue;

                float roomTemperature = GetRoomTemperatureCelsius(emitter.RoomIndex);
                if (roomTemperature < thresholdTemperature)
                    continue;

                BaseModule roomModule = ResolveModuleForRoom(emitter.RoomIndex);
                Vector3 reactorWorldPosition = TryResolveRoomHazardBounds(emitter.RoomIndex, roomModule, out Vector3 roomWorldCenter, out _)
                    ? roomWorldCenter
                    : _submarineBody.worldCenterOfMass;
                Vector3 centerDirection = _submarineBody.worldCenterOfMass - reactorWorldPosition;
                Vector3 forceDirection = ResolveFakeBlastDirection(centerDirection, upwardBias);

                float minimumGasVolume = math.max(0.001f, FiniteOr(minimumGasVolumeCubicMeters, DefaultMinimumGasVolumeCubicMeters));
                float roomVolume = math.max(minimumGasVolume, FiniteOr(_roomVolumes[emitter.RoomIndex], minimumGasVolume));
                float floodRatio = math.saturate(FiniteNonNegativeOrZero(_floodVolumes[emitter.RoomIndex]) / roomVolume);
                float floodMultiplier = math.lerp(1f, floodAmplification, floodRatio);
                float temperatureOvershoot = math.max(0f, roomTemperature - thresholdTemperature);
                float thermalScale = 1f + math.saturate(temperatureOvershoot / math.max(1f, thresholdTemperature));
                float baseImpulseMagnitude = math.max(
                    minimumImpulse,
                    FiniteNonNegativeOrZero(emitter.Reactor.PowerRating) * impulsePerWattSecond * impulseDuration);
                float impulseMagnitude = math.clamp(
                    baseImpulseMagnitude * floodMultiplier * thermalScale,
                    minimumImpulse,
                    maximumImpulse);

                PhysicsForceRouter.QueueForceAtPosition(
                    _submarineBody,
                    forceDirection * impulseMagnitude,
                    reactorWorldPosition,
                    ForceMode.Impulse);
                _reactorMeltdownTriggeredMask |= emitterBit;
            }
        }

        private void PublishDoorOpeningPressureEvents()
        {
            if (!_topologySeeded || !_pressureFront.IsCreated || !_doorSealedPrevious.IsCreated || fluidDynamics == null)
                return;

            int roomCount = RoomCount;
            int doorCount = math.min(fluidDynamics.ConfiguredBulkheadCount, DoorCapacity);
            float referencePressure = ResolveSafeReferencePressureKPa();
            float maximumPressure = ResolveSafeMaximumPressureKPa();
            float thresholdKPa = math.max(referencePressure, FiniteOr(highPressureEventThresholdKPa, referencePressure));
            for (int doorIndex = 0; doorIndex < DoorCapacity; doorIndex++)
            {
                byte currentState = doorIndex < doorCount ? _doorSealed[doorIndex] : (byte)1;
                byte previousState = _doorSealedPrevious[doorIndex];
                _doorSealedPrevious[doorIndex] = currentState;

                if (doorIndex >= doorCount || previousState == 0 || currentState != 0)
                    continue;

                int2 pair = _doorPairs[doorIndex];
                if (pair.x < 0 || pair.x >= roomCount || pair.y < 0 || pair.y >= roomCount)
                    continue;

                float pressureA = FiniteClampedOr(_pressureFront[pair.x], referencePressure, 0f, maximumPressure);
                float pressureB = FiniteClampedOr(_pressureFront[pair.y], referencePressure, 0f, maximumPressure);
                if (math.abs(pressureA - pressureB) <= Epsilon)
                    continue;

                if (math.max(pressureA, pressureB) < thresholdKPa)
                    continue;

                HighPressureEvent pressureEvent = new HighPressureEvent(
                    doorIndex,
                    pair.x,
                    pair.y,
                    pressureA,
                    pressureB,
                    ResolveDoorRuntimePosition(pair.x, pair.y));
                HighPressureEvents.Notify(in pressureEvent);
                EmitPressureBlowout(doorIndex, pair.x, pair.y, pressureA, pressureB, pressureEvent.RuntimePosition);
            }
        }

        private Vector3 ResolveDoorRuntimePosition(int roomA, int roomB)
        {
            if (fluidDynamics == null)
                return ResolveSubmarineFallbackRuntimePosition();

            Vector3 centroidA = fluidDynamics.GetCompartmentCentroid(roomA);
            Vector3 centroidB = fluidDynamics.GetCompartmentCentroid(roomB);
            Vector3 localMidpoint = (centroidA + centroidB) * 0.5f;
            return _cachedTransform != null ? _cachedTransform.TransformPoint(localMidpoint) : localMidpoint;
        }

        private void EmitPressureBlowout(int doorIndex, int roomA, int roomB, float pressureA, float pressureB, Vector3 runtimePosition)
        {
            if (fluidDynamics == null)
                return;

            float referencePressure = ResolveSafeReferencePressureKPa();
            float maximumPressure = ResolveSafeMaximumPressureKPa();
            float highPressureKPa = math.max(
                FiniteClampedOr(pressureA, referencePressure, 0f, maximumPressure),
                FiniteClampedOr(pressureB, referencePressure, 0f, maximumPressure));
            float lowPressureKPa = math.min(
                FiniteClampedOr(pressureA, referencePressure, 0f, maximumPressure),
                FiniteClampedOr(pressureB, referencePressure, 0f, maximumPressure));
            float pressureDeltaKPa = highPressureKPa - lowPressureKPa;
            if (pressureDeltaKPa <= Epsilon)
                return;

            Vector3 direction = ResolveDoorFlowDirection(roomA, roomB, pressureA, pressureB);
            if (direction.sqrMagnitude <= Epsilon)
                return;

            float doorAreaSquareMeters = math.max(Epsilon, FiniteOr(fluidDynamics.GetBulkheadDoorAreaSquareMeters(doorIndex), Epsilon));
            float forceMagnitudeNewtons = pressureDeltaKPa * 1000f * doorAreaSquareMeters;
            float impulseMagnitude = math.min(
                forceMagnitudeNewtons * math.max(0.001f, FiniteOr(pressureImpulseDurationSeconds, DefaultPressureImpulseDurationSeconds)),
                math.max(1f, FiniteOr(maximumPressureImpulseNewtonSeconds, DefaultMaximumPressureImpulseNewtonSeconds)));

            PressureImpulseEvent pressureImpulseEvent = new PressureImpulseEvent(
                doorIndex,
                runtimePosition,
                direction,
                doorAreaSquareMeters,
                highPressureKPa,
                lowPressureKPa,
                direction * forceMagnitudeNewtons,
                direction * impulseMagnitude,
                math.max(0.25f, FiniteOr(pressureImpulseRadiusMeters, DefaultPressureImpulseRadiusMeters)));
            PhysicsEventBus.NotifyPressureImpulse(in pressureImpulseEvent);
            ApplyPressureBlowoutImpulse(in pressureImpulseEvent);
        }

        private Vector3 ResolveDoorFlowDirection(int roomA, int roomB, float pressureA, float pressureB)
        {
            if (fluidDynamics == null)
                return _cachedTransform != null ? _cachedTransform.forward : Vector3.forward;

            Vector3 centroidA = fluidDynamics.GetCompartmentCentroid(roomA);
            Vector3 centroidB = fluidDynamics.GetCompartmentCentroid(roomB);
            Vector3 localDirection = pressureA >= pressureB ? (centroidB - centroidA) : (centroidA - centroidB);
            Vector3 worldDirection = _cachedTransform != null ? _cachedTransform.TransformDirection(localDirection) : localDirection;
            return ResolveFakeAxisDirection(worldDirection, _cachedTransform != null ? _cachedTransform.forward : Vector3.forward);
        }

        private void ApplyPressureBlowoutImpulse(in PressureImpulseEvent pressureImpulseEvent)
        {
            float radius = math.max(0.25f, FiniteOr(pressureImpulseEvent.InfluenceRadiusMeters, DefaultPressureImpulseRadiusMeters));
            int hitCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                pressureImpulseEvent.RuntimePosition,
                radius,
                _pressureImpulseOverlapBuffer,
                pressureImpulseLayers,
                QueryTriggerInteraction.Ignore);
            if (hitCount <= 0)
                return;

            float radiusSq = math.max(Epsilon, radius * radius);
            float falloffBias = math.saturate(FiniteOr(pressureImpulseFalloffExponent, DefaultPressureImpulseFalloffExponent) - 1f) * 0.1f;
            int uniqueBodyCount = 0;
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider collider = _pressureImpulseOverlapBuffer[hitIndex];
                _pressureImpulseOverlapBuffer[hitIndex] = null;
                if (collider == null)
                    continue;

                Rigidbody body = collider.attachedRigidbody;
                if (body == null || body.isKinematic || body == _submarineBody)
                    continue;

                Vector3 toDoor = pressureImpulseEvent.RuntimePosition - body.worldCenterOfMass;
                float normalizedDistance = math.saturate(1f - (toDoor.sqrMagnitude / radiusSq));
                if (normalizedDistance <= 0f)
                    continue;

                float falloff = math.saturate(normalizedDistance - falloffBias);
                if (falloff <= Epsilon)
                    continue;

                bool duplicate = false;
                for (int uniqueIndex = 0; uniqueIndex < uniqueBodyCount; uniqueIndex++)
                {
                    if (_pressureImpulseBodyBuffer[uniqueIndex] != body)
                        continue;

                    _pressureImpulseFalloffBuffer[uniqueIndex] = math.max(_pressureImpulseFalloffBuffer[uniqueIndex], falloff);
                    duplicate = true;
                    break;
                }

                if (duplicate)
                    continue;

                _pressureImpulseBodyBuffer[uniqueBodyCount] = body;
                _pressureImpulseFalloffBuffer[uniqueBodyCount] = falloff;
                uniqueBodyCount++;
                if (uniqueBodyCount >= PressureImpulseOverlapCapacity)
                    break;
            }

            float impulseMagnitude = math.min(
                FiniteNonNegativeOrZero(pressureImpulseEvent.PressureDeltaKPa) * 1000f * math.max(Epsilon, FiniteOr(pressureImpulseEvent.DoorAreaSquareMeters, Epsilon)) * math.max(0.001f, FiniteOr(pressureImpulseDurationSeconds, DefaultPressureImpulseDurationSeconds)),
                math.max(1f, FiniteOr(maximumPressureImpulseNewtonSeconds, DefaultMaximumPressureImpulseNewtonSeconds)));
            for (int bodyIndex = 0; bodyIndex < uniqueBodyCount; bodyIndex++)
            {
                Rigidbody body = _pressureImpulseBodyBuffer[bodyIndex];
                _pressureImpulseBodyBuffer[bodyIndex] = null;
                float falloff = _pressureImpulseFalloffBuffer[bodyIndex];
                _pressureImpulseFalloffBuffer[bodyIndex] = 0f;
                if (body == null)
                    continue;

                if (falloff <= Epsilon)
                    continue;

                Vector3 direction = pressureImpulseEvent.Direction;
                Vector3 impulse = direction * (impulseMagnitude * falloff);
                PhysicsForceRouter.QueueForce(body, impulse, ForceMode.Impulse);
            }
        }

        private static Vector3 ResolveFakeBlastDirection(Vector3 centerDirection, float upwardBias)
        {
            float x = FiniteOr(centerDirection.x, 0f);
            float z = FiniteOr(centerDirection.z, 0f);
            float lateralXSq = x * x;
            float lateralZSq = z * z;
            if (lateralXSq + lateralZSq <= 0.000001f)
                return Vector3.up;

            bool highArc = FiniteOr(upwardBias, 0f) >= 0.5f;
            float lateral = highArc ? 0.6f : 0.94f;
            float vertical = highArc ? 0.8f : 0.35f;
            if (lateralXSq >= lateralZSq)
                return new Vector3(x >= 0f ? lateral : -lateral, vertical, 0f);

            return new Vector3(0f, vertical, z >= 0f ? lateral : -lateral);
        }

        private static Vector3 ResolveFakeAxisDirection(Vector3 value, Vector3 fallback)
        {
            Vector3 safeValue = new Vector3(FiniteOr(value.x, 0f), FiniteOr(value.y, 0f), FiniteOr(value.z, 0f));
            float lengthSq = safeValue.sqrMagnitude;
            if (lengthSq <= 0.000001f)
                return fallback;

            float absX = math.abs(safeValue.x);
            float absY = math.abs(safeValue.y);
            float absZ = math.abs(safeValue.z);
            if (absX >= absY && absX >= absZ)
                return safeValue.x >= 0f ? Vector3.right : Vector3.left;

            if (absY >= absZ)
                return safeValue.y >= 0f ? Vector3.up : Vector3.down;

            return safeValue.z >= 0f ? Vector3.forward : Vector3.back;
        }

        private void ScheduleAtmosphereJob(float fixedDeltaTime)
        {
            if (_atmosphereJobRunning || fluidDynamics == null || !_o2Front.IsCreated)
                return;

            int roomCount = RoomCount;
            int doorCount = math.min(fluidDynamics.ConfiguredBulkheadCount, DoorCapacity);
            ResolveSafeTemperatureBounds(out float minimumTemperature, out float maximumTemperature);
            float referencePressure = ResolveSafeReferencePressureKPa();
            float maximumPressure = ResolveSafeMaximumPressureKPa();
            float tankCapacity = math.max(1f, FiniteOr(oxygenTankCapacity, DefaultOxygenTankCapacity));
            AtmosphereStepJob job = new AtmosphereStepJob
            {
                O2Front = _o2Front,
                CO2Front = _co2Front,
                InertFront = _inertFront,
                FloodVolumes = _floodVolumes,
                RoomVolumes = _roomVolumes,
                PressureFront = _pressureFront,
                GasVolumeFront = _gasVolumeFront,
                O2ConsumptionRates = _o2ConsumptionRates,
                CO2GenerationRates = _co2GenerationRates,
                RoomPlayerCounts = _roomPlayerCounts,
                TemperatureFront = _temperatureFront,
                RoomHeatWatts = _roomHeatWatts,
                SteamFront = _steamFront,
                DoorPairs = _doorPairs,
                DoorSealed = _doorSealed,
                O2Back = _o2Back,
                CO2Back = _co2Back,
                InertBack = _inertBack,
                PressureBack = _pressureBack,
                GasVolumeBack = _gasVolumeBack,
                TemperatureBack = _temperatureBack,
                SteamBack = _steamBack,
                O2PartialPressureBack = _o2PartialPressureBack,
                CO2PartialPressureBack = _co2PartialPressureBack,
                N2PartialPressureBack = _n2PartialPressureBack,
                RoomStatusMaskBack = _roomStatusMaskBack,
                RoomCount = roomCount,
                DoorCount = doorCount,
                DeltaTime = FiniteNonNegativeOrZero(fixedDeltaTime),
                ReferencePressureKPa = referencePressure,
                MinimumGasVolumeCubicMeters = math.max(0.001f, FiniteOr(minimumGasVolumeCubicMeters, DefaultMinimumGasVolumeCubicMeters)),
                MaximumPressureKPa = maximumPressure,
                ReferenceTemperatureCelsius = FiniteClampedOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius, minimumTemperature, maximumTemperature),
                FloodWaterTemperatureCelsius = FiniteClampedOr(floodWaterTemperatureCelsius, DefaultFloodWaterTemperatureCelsius, minimumTemperature, maximumTemperature),
                MinimumTemperatureCelsius = minimumTemperature,
                MaximumTemperatureCelsius = maximumTemperature,
                OxygenTankCapacity = tankCapacity,
                HeatWattsToCelsiusPerSecond = FiniteNonNegativeOrZero(heatWattsToCelsiusPerSecond),
                LowOxygenThresholdUnits = math.saturate(FiniteOr(lowOxygenThreshold01, DefaultLowOxygenThreshold01)) * tankCapacity,
                CarbonDioxideToxicityFraction = DefaultCarbonDioxideToxicityFraction,
                FreezingTemperatureCelsius = FiniteClampedOr(freezingRoomTemperatureCelsius, DefaultFreezingRoomTemperatureCelsius, minimumTemperature, maximumTemperature),
                HighPressureStatusKPa = math.max(referencePressure, FiniteOr(highPressureEventThresholdKPa, referencePressure))
            };

            _scheduledAtmosphereDeltaTime = FiniteNonNegativeOrZero(fixedDeltaTime);
            _atmosphereJobHandle = job.Schedule();
            _atmosphereJobRunning = true;
        }

        private void ConsumeCompletedJob(float fixedDeltaTime)
        {
            if (!_atmosphereJobRunning)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _atmosphereJobHandle, false))
                return;

            _atmosphereJobRunning = false;
            SwapAtmosphereBuffers();
            ApplyPendingAtmosphereMutations();
            ApplyCompletedAtmosphereStepSideEffects(ResolveCompletedAtmosphereDeltaTime(fixedDeltaTime));
        }

        private float ResolveCompletedAtmosphereDeltaTime(float fallbackDeltaTime)
        {
            float deltaTime = _scheduledAtmosphereDeltaTime > 0f
                ? _scheduledAtmosphereDeltaTime
                : FiniteNonNegativeOrZero(fallbackDeltaTime);
            _scheduledAtmosphereDeltaTime = 0f;
            return deltaTime;
        }

        private void ApplyCompletedAtmosphereStepSideEffects(float atmosphereDeltaTime)
        {
            ApplyAbyssalBlackoutFreeze(atmosphereDeltaTime);
            ProcessSteamPhaseCycle(atmosphereDeltaTime);
            TryEmergencyAtmosphericVenting(atmosphereDeltaTime);
            DecayExplosivePockets(atmosphereDeltaTime);
            EvaluateReactorMeltdowns();
            UpdateBoilingFloodHazards(atmosphereDeltaTime);
            PublishCompartmentPartialPressureSnapshot();
            PublishAtmosphereFakes(atmosphereDeltaTime);
        }

        private void PublishCompartmentPartialPressureSnapshot()
        {
            if (fluidDynamics == null ||
                !_o2PartialPressureFront.IsCreated ||
                !_co2PartialPressureFront.IsCreated ||
                !_n2PartialPressureFront.IsCreated)
            {
                return;
            }

            int roomCount = RoomCount;
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                if (roomIndex >= roomCount)
                    continue;

                fluidDynamics.SetCompartmentGasPartialPressuresKPa(
                    roomIndex,
                    _o2PartialPressureFront[roomIndex],
                    _co2PartialPressureFront[roomIndex],
                    _n2PartialPressureFront[roomIndex]);
            }
        }

        private void RefreshDebugState()
        {
            int roomCount = RoomCount;
            int doorCount = fluidDynamics != null ? math.min(fluidDynamics.ConfiguredBulkheadCount, DoorCapacity) : 0;
            _debugRoomCount = roomCount;
            _debugDoorCount = doorCount;

            if (!_pressureFront.IsCreated || roomCount <= 0)
            {
                _debugAveragePressureKPa = 0f;
                _debugMaxPressureKPa = 0f;
                _debugAverageOxygenFraction = 0f;
                _debugAverageCarbonDioxideFraction = 0f;
                _debugAverageSteamVolumeCubicMeters = 0f;
                _debugMaxSteamVolumeCubicMeters = 0f;
                return;
            }

            float pressureSum = 0f;
            float maxPressure = 0f;
            float oxygenFractionSum = 0f;
            float carbonDioxideFractionSum = 0f;
            float temperatureSum = 0f;
            float maxTemperature = minimumTemperatureCelsius;
            float steamSum = 0f;
            float maxSteam = 0f;
            float tankCapacity = math.max(1f, FiniteOr(oxygenTankCapacity, DefaultOxygenTankCapacity));
            for (int roomIndex = 0; roomIndex < roomCount; roomIndex++)
            {
                float pressure = FiniteClampedOr(_pressureFront[roomIndex], ResolveSafeReferencePressureKPa(), 0f, ResolveSafeMaximumPressureKPa());
                pressureSum += pressure;
                maxPressure = math.max(maxPressure, pressure);
                float temperature = _temperatureFront.IsCreated ? GetRoomTemperatureCelsius(roomIndex) : FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius);
                temperatureSum += temperature;
                maxTemperature = math.max(maxTemperature, temperature);
                float steamVolume = _steamFront.IsCreated ? FiniteNonNegativeOrZero(_steamFront[roomIndex]) : 0f;
                steamSum += steamVolume;
                maxSteam = math.max(maxSteam, steamVolume);

                oxygenFractionSum += math.saturate(FiniteNonNegativeOrZero(_o2Front[roomIndex]) / tankCapacity);
                carbonDioxideFractionSum += math.saturate(FiniteNonNegativeOrZero(_co2Front[roomIndex]) / tankCapacity);
            }

            float inverseRoomCount = 1f / math.max(roomCount, 1);
            _debugAveragePressureKPa = pressureSum * inverseRoomCount;
            _debugMaxPressureKPa = maxPressure;
            _debugAverageOxygenFraction = oxygenFractionSum * inverseRoomCount;
            _debugAverageCarbonDioxideFraction = carbonDioxideFractionSum * inverseRoomCount;
            _debugAverageTemperatureCelsius = temperatureSum * inverseRoomCount;
            _debugMaxTemperatureCelsius = maxTemperature;
            _debugAverageSteamVolumeCubicMeters = steamSum * inverseRoomCount;
            _debugMaxSteamVolumeCubicMeters = maxSteam;
        }

        private void DisposeNativeStateDeferred()
        {
            ClearBoilingFloodHazards();
            ClearAtmosphereFakes();
            UnregisterNativeState();
            JobHandle dependency = _atmosphereJobRunning ? _atmosphereJobHandle : default;
            _atmosphereJobRunning = false;
            _scheduledAtmosphereDeltaTime = 0f;
            DisposeDeferred(ref _roomVolumes, dependency);
            DisposeDeferred(ref _floodVolumes, dependency);
            DisposeDeferred(ref _o2Front, dependency);
            DisposeDeferred(ref _o2Back, dependency);
            DisposeDeferred(ref _co2Front, dependency);
            DisposeDeferred(ref _co2Back, dependency);
            DisposeDeferred(ref _inertFront, dependency);
            DisposeDeferred(ref _inertBack, dependency);
            DisposeDeferred(ref _pressureFront, dependency);
            DisposeDeferred(ref _pressureBack, dependency);
            DisposeDeferred(ref _o2PartialPressureFront, dependency);
            DisposeDeferred(ref _o2PartialPressureBack, dependency);
            DisposeDeferred(ref _co2PartialPressureFront, dependency);
            DisposeDeferred(ref _co2PartialPressureBack, dependency);
            DisposeDeferred(ref _n2PartialPressureFront, dependency);
            DisposeDeferred(ref _n2PartialPressureBack, dependency);
            DisposeDeferred(ref _gasVolumeFront, dependency);
            DisposeDeferred(ref _gasVolumeBack, dependency);
            DisposeDeferred(ref _o2ConsumptionRates, dependency);
            DisposeDeferred(ref _co2GenerationRates, dependency);
            DisposeDeferred(ref _roomPlayerCounts, dependency);
            DisposeDeferred(ref _temperatureFront, dependency);
            DisposeDeferred(ref _temperatureBack, dependency);
            DisposeDeferred(ref _steamFront, dependency);
            DisposeDeferred(ref _steamBack, dependency);
            DisposeDeferred(ref _hydrogenPocketFront, dependency);
            DisposeDeferred(ref _oxygenPocketFront, dependency);
            DisposeDeferred(ref _roomHeatWatts, dependency);
            DisposeDeferred(ref _roomStatusMaskFront, dependency);
            DisposeDeferred(ref _roomStatusMaskBack, dependency);
            DisposeDeferred(ref _doorPairs, dependency);
            DisposeDeferred(ref _doorSealed, dependency);
            DisposeDeferred(ref _doorSealedPrevious, dependency);
            _topologySeeded = false;
            _topologyRoomCount = -1;
            _topologyDoorCount = -1;
            _thermalEmittersSeeded = false;
            _emergencyVentPipesSeeded = false;
            _emergencyVentRoomMask = 0u;
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
                _emergencyVentPipesByRoom[roomIndex] = null;
            ClearPendingAtmosphereMutations();
        }

        private void DisposeDeferred<T>(ref NativeArray<T> array, JobHandle dependency) where T : struct
        {
            if (!array.IsCreated)
                return;

            _disposeHandle = JobHandle.CombineDependencies(_disposeHandle, array.Dispose(dependency));
            array = default;
        }

        private void SwapAtmosphereBuffers()
        {
            SwapBuffers(ref _o2Front, ref _o2Back);
            SwapBuffers(ref _co2Front, ref _co2Back);
            SwapBuffers(ref _inertFront, ref _inertBack);
            SwapBuffers(ref _pressureFront, ref _pressureBack);
            SwapBuffers(ref _o2PartialPressureFront, ref _o2PartialPressureBack);
            SwapBuffers(ref _co2PartialPressureFront, ref _co2PartialPressureBack);
            SwapBuffers(ref _n2PartialPressureFront, ref _n2PartialPressureBack);
            SwapBuffers(ref _gasVolumeFront, ref _gasVolumeBack);
            SwapBuffers(ref _temperatureFront, ref _temperatureBack);
            SwapBuffers(ref _steamFront, ref _steamBack);
            SwapBuffers(ref _roomStatusMaskFront, ref _roomStatusMaskBack);
        }

        private void ClearPendingAtmosphereMutations()
        {
            _pendingAtmosphereMutationMask = 0u;
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
                _pendingAtmosphereMutations[roomIndex] = default;
        }

        private void ClearRoomModuleCache()
        {
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
                _atmosphereRoomModules[roomIndex] = null;
        }

        private void ClearBrownoutRoomModuleCache()
        {
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
                _brownoutRoomModules[roomIndex] = null;
        }

        private float ResolveInstantPressure(float totalGasUnits, float gasVolumeCubicMeters)
        {
            return ResolveInstantPressureWithTemperature(totalGasUnits, gasVolumeCubicMeters, FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius));
        }

        private float ResolveInstantFakePressure(int roomIndex, float temperatureCelsius)
        {
            if (roomIndex < 0 || roomIndex >= RoomCount)
                return ResolveSafeReferencePressureKPa();

            return ResolveInstantDaltonPressure(roomIndex, temperatureCelsius);
        }

        private float ResolveInstantDaltonPressure(int roomIndex, float temperatureCelsius)
        {
            if (roomIndex < 0 || roomIndex >= RoomCount)
                return ResolveSafeReferencePressureKPa();

            float minimumGasVolume = math.max(0.001f, FiniteOr(minimumGasVolumeCubicMeters, DefaultMinimumGasVolumeCubicMeters));
            float roomVolume = _roomVolumes.IsCreated
                ? math.max(FiniteOr(_roomVolumes[roomIndex], minimumGasVolume), minimumGasVolume)
                : minimumGasVolume;
            float floodVolume = _floodVolumes.IsCreated
                ? math.clamp(FiniteNonNegativeOrZero(_floodVolumes[roomIndex]), 0f, roomVolume - Epsilon)
                : 0f;
            float gasVolume = math.max(minimumGasVolume, roomVolume - floodVolume);
            float steamVolume = _steamFront.IsCreated
                ? FiniteNonNegativeOrZero(_steamFront[roomIndex])
                : 0f;
            float tankCapacity = math.max(1f, FiniteOr(oxygenTankCapacity, DefaultOxygenTankCapacity));
            ResolveSafeTemperatureBounds(out float minimumTemperature, out float maximumTemperature);
            float referenceTemperature = FiniteClampedOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius, minimumTemperature, maximumTemperature);
            float pressure = ResolveDaltonPressureKPa(
                _o2Front.IsCreated ? _o2Front[roomIndex] : tankCapacity,
                _co2Front.IsCreated ? _co2Front[roomIndex] : DefaultInitialCarbonDioxideFraction * tankCapacity,
                _inertFront.IsCreated ? _inertFront[roomIndex] : DefaultInertFraction * tankCapacity,
                steamVolume,
                roomVolume,
                gasVolume,
                temperatureCelsius,
                ResolveSafeReferencePressureKPa(),
                ResolveSafeMaximumPressureKPa(),
                referenceTemperature,
                tankCapacity,
                out float oxygenPartialPressureKPa,
                out float carbonDioxidePartialPressureKPa,
                out float nitrogenPartialPressureKPa);

            if (_gasVolumeFront.IsCreated)
                _gasVolumeFront[roomIndex] = gasVolume;
            if (_o2PartialPressureFront.IsCreated)
                _o2PartialPressureFront[roomIndex] = oxygenPartialPressureKPa;
            if (_co2PartialPressureFront.IsCreated)
                _co2PartialPressureFront[roomIndex] = carbonDioxidePartialPressureKPa;
            if (_n2PartialPressureFront.IsCreated)
                _n2PartialPressureFront[roomIndex] = nitrogenPartialPressureKPa;

            return pressure;
        }

        private float ResolveSimplePressureKPa(float roomVolume, float floodVolume, float steamVolume, float temperatureCelsius)
        {
            ResolveSafeTemperatureBounds(out float minimumTemperature, out float maximumTemperature);
            float referenceTemperature = FiniteClampedOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius, minimumTemperature, maximumTemperature);
            float safeRoomVolume = math.max(math.max(0.001f, FiniteOr(minimumGasVolumeCubicMeters, DefaultMinimumGasVolumeCubicMeters)), FiniteOr(roomVolume, DefaultMinimumGasVolumeCubicMeters));
            float gasVolume = math.max(math.max(0.001f, FiniteOr(minimumGasVolumeCubicMeters, DefaultMinimumGasVolumeCubicMeters)), safeRoomVolume - math.clamp(FiniteNonNegativeOrZero(floodVolume), 0f, safeRoomVolume - Epsilon));
            float safeTemperature = FiniteClampedOr(temperatureCelsius, referenceTemperature, minimumTemperature, maximumTemperature);
            float tankCapacity = math.max(1f, FiniteOr(oxygenTankCapacity, DefaultOxygenTankCapacity));
            return ResolveDaltonPressureKPa(
                tankCapacity,
                DefaultInitialCarbonDioxideFraction * tankCapacity,
                DefaultInertFraction * tankCapacity,
                steamVolume,
                safeRoomVolume,
                gasVolume,
                safeTemperature,
                ResolveSafeReferencePressureKPa(),
                ResolveSafeMaximumPressureKPa(),
                referenceTemperature,
                tankCapacity,
                out _,
                out _,
                out _);
        }

        private void UpdateBoilingFloodHazards(float fixedDeltaTime)
        {
            if (fluidDynamics == null || !_temperatureFront.IsCreated || !_floodVolumes.IsCreated || !_roomVolumes.IsCreated)
            {
                ClearBoilingFloodHazards();
                return;
            }

            int roomCount = RoomCount;
            ResolveSafeTemperatureBounds(out float safeMinimumTemperature, out float safeMaximumTemperature);
            float thresholdTemperature = FiniteClampedOr(boilingFloodTemperatureCelsius, DefaultBoilingFloodTemperatureCelsius, safeMinimumTemperature, safeMaximumTemperature);
            float minimumFillRatio = math.saturate(FiniteOr(boilingFloodMinimumFillRatio, DefaultBoilingFloodMinimumFillRatio));
            float hazardBaseIntensity = FiniteNonNegativeOrZero(boilingHazardIntensity);
            float faunaDamagePerStep = FiniteNonNegativeOrZero(boilingFaunaDamagePerSecond) * FiniteNonNegativeOrZero(fixedDeltaTime);
            float maxTemperature = math.max(thresholdTemperature + 1f, safeMaximumTemperature);
            float minimumGasVolume = math.max(0.001f, FiniteOr(minimumGasVolumeCubicMeters, DefaultMinimumGasVolumeCubicMeters));

            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                if (roomIndex >= roomCount)
                {
                    UnregisterBoilingHazard(roomIndex);
                    continue;
                }

                float roomVolume = math.max(minimumGasVolume, FiniteOr(_roomVolumes[roomIndex], minimumGasVolume));
                float floodVolume = math.clamp(FiniteNonNegativeOrZero(_floodVolumes[roomIndex]), 0f, roomVolume);
                float invRoomVolume = math.rcp(roomVolume);
                float fillRatio = math.saturate(floodVolume * invRoomVolume);
                float temperature = GetRoomTemperatureCelsius(roomIndex);
                if (temperature < thresholdTemperature || fillRatio < minimumFillRatio)
                {
                    UnregisterBoilingHazard(roomIndex);
                    continue;
                }

                if (!TryResolveBoilingHazardBounds(roomIndex, roomVolume, out Vector3 worldCenter, out float radius))
                {
                    UnregisterBoilingHazard(roomIndex);
                    continue;
                }

                float temperature01 = math.saturate((temperature - thresholdTemperature) * math.rcp(math.max(1f, maxTemperature - thresholdTemperature)));
                float fill01 = math.saturate((fillRatio - minimumFillRatio) * math.rcp(math.max(0.01f, 1f - minimumFillRatio)));
                float intensity = hazardBaseIntensity * math.max(0.1f, math.max(temperature01, fill01));

                RegisterBoilingHazard(roomIndex, worldCenter, intensity, radius);
                ApplyBoilingFaunaDamage(worldCenter, radius, intensity * faunaDamagePerStep);
            }
        }

        private void ClearBoilingFloodHazards()
        {
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
                UnregisterBoilingHazard(roomIndex);
        }

        private void RegisterBoilingHazard(int roomIndex, Vector3 worldCenter, float intensity, float radius)
        {
            if (roomIndex < 0 || roomIndex >= RoomCapacity)
                return;

            uint roomBit = 1u << roomIndex;
            if (HectonHazardManager.Register(_boilingHazardIds[roomIndex], worldCenter, intensity, radius, HazardType.Heat))
            {
                _boilingHazardActiveMask |= roomBit;
                return;
            }

            UnregisterBoilingHazard(roomIndex);
        }

        private void UnregisterBoilingHazard(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCapacity)
                return;

            uint roomBit = 1u << roomIndex;
            if ((_boilingHazardActiveMask & roomBit) == 0u)
                return;

            HectonHazardManager.Unregister(_boilingHazardIds[roomIndex]);
            _boilingHazardActiveMask &= ~roomBit;
        }

        private float ResolveInstantThermalCapacity(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCount || !_roomVolumes.IsCreated || !_floodVolumes.IsCreated || !_gasVolumeFront.IsCreated)
                return math.max(Epsilon, FiniteOr(minimumThermalCapacityJoulesPerKelvin, DefaultMinimumThermalCapacityJoulesPerKelvin));

            float minimumGasVolume = math.max(0.001f, FiniteOr(minimumGasVolumeCubicMeters, DefaultMinimumGasVolumeCubicMeters));
            float gasVolume = math.max(minimumGasVolume, FiniteOr(_gasVolumeFront[roomIndex], minimumGasVolume));
            float floodVolume = FiniteNonNegativeOrZero(_floodVolumes[roomIndex]);
            float airMass = gasVolume * math.max(Epsilon, FiniteOr(airDensityKilogramsPerCubicMeter, DefaultAirDensityKilogramsPerCubicMeter));
            float waterMass = floodVolume * math.max(Epsilon, FiniteOr(waterDensityKilogramsPerCubicMeter, DefaultWaterDensityKilogramsPerCubicMeter));
            float airCapacity = airMass * math.max(Epsilon, FiniteOr(airSpecificHeatJoulesPerKilogramKelvin, DefaultAirSpecificHeatJoulesPerKilogramKelvin));
            float waterCapacity = waterMass * math.max(Epsilon, FiniteOr(waterSpecificHeatJoulesPerKilogramKelvin, DefaultWaterSpecificHeatJoulesPerKilogramKelvin));
            return math.max(math.max(Epsilon, FiniteOr(minimumThermalCapacityJoulesPerKelvin, DefaultMinimumThermalCapacityJoulesPerKelvin)), airCapacity + waterCapacity);
        }

        private static float ResolveFakeHazardRadius(float roomVolume, float paddingMeters)
        {
            float safePadding = FiniteNonNegativeOrZero(paddingMeters);
            float volumeRadius = FakeHazardRadiusBaseMeters + (FiniteNonNegativeOrZero(roomVolume) * FakeHazardRadiusVolumeScale);
            return math.clamp(volumeRadius + safePadding, 0.5f, FakeHazardRadiusMaxMeters + safePadding);
        }

        private static bool TryResolveModuleInteriorHazardBounds(BaseModule module, float paddingMeters, out Vector3 worldCenter, out float radius)
        {
            worldCenter = Vector3.zero;
            radius = 0f;
            if (module == null || !module.TryGetInteriorAabbBounds(out worldCenter, out Vector3 halfExtents))
                return false;

            float safePadding = FiniteNonNegativeOrZero(paddingMeters);
            float maxExtent = math.max(
                FiniteNonNegativeOrZero(halfExtents.x),
                math.max(FiniteNonNegativeOrZero(halfExtents.y), FiniteNonNegativeOrZero(halfExtents.z)));
            radius = math.clamp((maxExtent * 1.75f) + safePadding, 0.5f, FakeHazardRadiusMaxMeters + safePadding);
            return radius > 0f;
        }

        private bool TryResolveBoilingHazardBounds(int roomIndex, float roomVolume, out Vector3 worldCenter, out float radius)
        {
            worldCenter = Vector3.zero;
            radius = 0f;
            if (fluidDynamics == null || _cachedTransform == null)
                return false;

            if (TryResolveModuleInteriorHazardBounds(ResolveModuleForRoom(roomIndex), boilingHazardRadiusPaddingMeters, out worldCenter, out radius))
                return true;

            Vector3 localCentroid = fluidDynamics.GetCompartmentCentroid(roomIndex);
            worldCenter = _cachedTransform.TransformPoint(localCentroid);

            radius = ResolveFakeHazardRadius(roomVolume, boilingHazardRadiusPaddingMeters);
            return radius > 0f;
        }

        private bool TryResolveRoomHazardBounds(int roomIndex, out Vector3 worldCenter, out float radius)
        {
            return TryResolveRoomHazardBounds(roomIndex, ResolveModuleForRoom(roomIndex), out worldCenter, out radius);
        }

        private bool TryResolveRoomHazardBounds(int roomIndex, BaseModule roomModule, out Vector3 worldCenter, out float radius)
        {
            worldCenter = Vector3.zero;
            radius = 0f;
            if (fluidDynamics == null || _cachedTransform == null || roomIndex < 0 || roomIndex >= RoomCount)
                return false;

            if (TryResolveModuleInteriorHazardBounds(roomModule, roomHazardRadiusPaddingMeters, out worldCenter, out radius))
                return true;

            float minimumGasVolume = math.max(0.001f, FiniteOr(minimumGasVolumeCubicMeters, DefaultMinimumGasVolumeCubicMeters));
            float roomVolume = _roomVolumes.IsCreated
                ? math.max(FiniteOr(_roomVolumes[roomIndex], minimumGasVolume), minimumGasVolume)
                : math.max(FiniteOr(fluidDynamics.GetCompartmentMaxFloodVolumeCubicMeters(roomIndex), minimumGasVolume), minimumGasVolume);
            Vector3 localCentroid = fluidDynamics.GetCompartmentCentroid(roomIndex);
            worldCenter = _cachedTransform.TransformPoint(localCentroid);
            radius = ResolveFakeHazardRadius(roomVolume, roomHazardRadiusPaddingMeters);
            return radius > 0f;
        }

        private void ApplyBoilingFaunaDamage(Vector3 worldCenter, float radius, float damageAmount)
        {
            if (damageAmount <= 0f || radius <= 0f)
                return;

            int hitCount = WorldSpatialHashGrid.CollectContactsNonAlloc(
                worldCenter,
                radius,
                SpatialTargetKind.Bioform,
                _boilingFaunaContacts);
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                SpatialQueryHit hit = _boilingFaunaContacts[hitIndex];
                if (hit.Owner is FaunaBrain faunaBrain)
                    faunaBrain.TakeDamage(damageAmount);
            }
        }

        private int ResolveNearestRoomIndex(Vector3 worldPosition)
        {
            if (fluidDynamics == null || _cachedTransform == null)
                return -1;

            return ResolveNearestRoomIndexLocal(_cachedTransform.InverseTransformPoint(worldPosition));
        }

        private int ResolveNearestRoomIndex(in AbsoluteUniversePosition worldAup)
        {
            if (fluidDynamics == null || _cachedTransform == null)
                return -1;

            float3 runtimePosition = worldAup.ToRuntimeFloat3();
            return ResolveNearestRoomIndex(new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
        }

        private int ResolveNearestRoomIndexLocal(Vector3 localPosition)
        {
            int roomCount = RoomCount;
            if (roomCount <= 0)
                return -1;

            int bestRoomIndex = 0;
            float bestDistanceSq = float.MaxValue;
            for (int roomIndex = 0; roomIndex < roomCount; roomIndex++)
            {
                Vector3 centroid = fluidDynamics.GetCompartmentCentroid(roomIndex);
                float distanceSq = (centroid - localPosition).sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                bestRoomIndex = roomIndex;
            }

            return bestRoomIndex;
        }

        private static void SwapBuffers<T>(ref NativeArray<T> front, ref NativeArray<T> back) where T : struct
        {
            NativeArray<T> swap = front;
            front = back;
            back = swap;
        }
    }
}
