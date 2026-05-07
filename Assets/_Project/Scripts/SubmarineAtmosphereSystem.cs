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
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Atmosphere
{
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
            DoorIndex = doorIndex;
            RoomA = roomA;
            RoomB = roomB;
            PressureAKPa = pressureAKPa;
            PressureBKPa = pressureBKPa;
            PressureDeltaKPa = math.abs(pressureAKPa - pressureBKPa);
            RuntimePosition = runtimePosition;
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
    [StructLayout(LayoutKind.Sequential)]
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
        }

        /// <summary>Unregisters one high-pressure warning listener.</summary>
        public static void Unregister(IHighPressureEventListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
                _listeners.Unregister(listener);
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
            }
        }

        private static bool Enqueue(in HighPressureEventPayload payload)
        {
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
            TemperatureCelsius = temperatureCelsius;
            RuntimePosition = runtimePosition;
        }

        public uint NodeId { get; }
        public int RoomIndex { get; }
        public float TemperatureCelsius { get; }
        public Vector3 RuntimePosition { get; }
    }

    /// <summary>
    /// Unmanaged fatal pressure implosion payload carried by the deferred event lane.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
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
        }

        /// <summary>Unregisters one fatal pressure implosion listener.</summary>
        public static void Unregister(IFatalPressureImplosionEventListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
                _listeners.Unregister(listener);
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
            }
        }

        private static bool Enqueue(in FatalPressureImplosionEventPayload payload)
        {
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
        private const float DefaultPlayerOxygenConsumptionPercentPerSecond = 0.5f;
        private const float DefaultAtmosphereSlowTickSeconds = 0.1f;
        private const float DefaultHeatWattsToCelsiusPerSecond = 0.001f;
        private const float DefaultOverheatBrownoutTemperatureCelsius = 80f;
        private const float DefaultOverheatMinimumVoltage = 0.18f;
        private const float DefaultToxicRoomHazardIntensity = 1f;
        private const float DefaultFireSmokeHazardIntensity = 0.85f;
        private const float DefaultRoomHazardRadiusPaddingMeters = 1.25f;
        private const float DefaultFireSmokeVisorGlitchBias = 1.15f;
        private const float DefaultLowOxygenAudioCooldownSeconds = 45f;
        private const float FakeHazardRadiusBaseMeters = 0.85f;
        private const float FakeHazardRadiusVolumeScale = 0.08f;
        private const float FakeHazardRadiusMaxMeters = 6f;
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
        private const float ThermalConductionCadenceSeconds = 0.5f;
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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [StructLayout(LayoutKind.Sequential)]
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

            public int RoomCount;
            public int DoorCount;
            public float DeltaTime;
            public float ReferencePressureKPa;
            public float MinimumGasVolumeCubicMeters;
            public float MaximumPressureKPa;
            public float DoorConductance;
            public float MaxTransferUnitsPerSecond;
            public float ReferenceTemperatureCelsius;
            public float FloodWaterTemperatureCelsius;
            public float MinimumTemperatureCelsius;
            public float MaximumTemperatureCelsius;
            public float AirDensityKilogramsPerCubicMeter;
            public float AirSpecificHeatJoulesPerKilogramKelvin;
            public float WaterDensityKilogramsPerCubicMeter;
            public float WaterSpecificHeatJoulesPerKilogramKelvin;
            public float MinimumThermalCapacityJoulesPerKelvin;
            public float ThermalConductionDeltaTime;
            public float BulkheadThermalConductivityWattsPerKelvin;
            public float SealedBulkheadThermalCoupling;
            public float OpenBulkheadThermalCoupling;
            public float ReferenceTemperatureKelvin;
            public float OxygenTankCapacity;
            public float HeatWattsToCelsiusPerSecond;

            public void Execute()
            {
                float tankCapacity = math.max(1f, OxygenTankCapacity);
                for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
                {
                    if (roomIndex >= RoomCount)
                    {
                        O2Back[roomIndex] = 0f;
                        CO2Back[roomIndex] = 0f;
                        InertBack[roomIndex] = 0f;
                        PressureBack[roomIndex] = ReferencePressureKPa;
                        GasVolumeBack[roomIndex] = MinimumGasVolumeCubicMeters;
                        TemperatureBack[roomIndex] = ReferenceTemperatureCelsius;
                        SteamBack[roomIndex] = 0f;
                        continue;
                    }

                    float roomVolume = math.max(RoomVolumes[roomIndex], MinimumGasVolumeCubicMeters);
                    float floodVolume = math.clamp(FloodVolumes[roomIndex], 0f, roomVolume - Epsilon);
                    float gasVolume = math.max(MinimumGasVolumeCubicMeters, roomVolume - floodVolume);
                    int playerCount = math.max(0, RoomPlayerCounts[roomIndex]);

                    float oxygenDrain = math.max(0f, O2ConsumptionRates[roomIndex]) * playerCount * DeltaTime;
                    float oxygen = math.clamp(O2Front[roomIndex] - oxygenDrain, 0f, tankCapacity);
                    float carbonDioxide = math.clamp(
                        CO2Front[roomIndex] + (math.max(0f, CO2GenerationRates[roomIndex]) * playerCount * DeltaTime),
                        0f,
                        tankCapacity);
                    float inert = 0f;
                    float steam = math.max(0f, SteamFront[roomIndex]);

                    O2Back[roomIndex] = oxygen;
                    CO2Back[roomIndex] = carbonDioxide;
                    InertBack[roomIndex] = inert;
                    SteamBack[roomIndex] = steam;
                    GasVolumeBack[roomIndex] = gasVolume;

                    float previousTemperature = math.clamp(
                        TemperatureFront[roomIndex],
                        MinimumTemperatureCelsius,
                        MaximumTemperatureCelsius);
                    float floodFill01 = math.saturate(floodVolume / math.max(roomVolume, Epsilon));
                    float floodBlend = math.saturate(floodFill01 * DeltaTime * 0.1f);
                    float mixedTemperature = math.lerp(previousTemperature, FloodWaterTemperatureCelsius, floodBlend);
                    float temperatureDelta = RoomHeatWatts[roomIndex] * math.max(0f, HeatWattsToCelsiusPerSecond) * DeltaTime;
                    float roomTemperature = math.clamp(
                        mixedTemperature + temperatureDelta,
                        MinimumTemperatureCelsius,
                        MaximumTemperatureCelsius);
                    TemperatureBack[roomIndex] = roomTemperature;
                    PressureBack[roomIndex] = ResolveFakePressure(roomVolume, floodVolume, steam, roomTemperature);
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

                    float transferLimit01 = math.saturate(MaxTransferUnitsPerSecond / tankCapacity);
                    float doorBlend = math.min(0.5f, math.saturate(DoorConductance * DeltaTime) + transferLimit01);
                    if (doorBlend <= Epsilon)
                        continue;

                    float oxygenDelta = (O2Back[roomB] - O2Back[roomA]) * doorBlend;
                    float toxicityDelta = (CO2Back[roomB] - CO2Back[roomA]) * doorBlend;
                    float heatDelta = (TemperatureBack[roomB] - TemperatureBack[roomA]) * doorBlend;
                    float pressureDelta = (PressureBack[roomB] - PressureBack[roomA]) * doorBlend;

                    O2Back[roomA] += oxygenDelta;
                    O2Back[roomB] -= oxygenDelta;
                    CO2Back[roomA] += toxicityDelta;
                    CO2Back[roomB] -= toxicityDelta;
                    TemperatureBack[roomA] += heatDelta;
                    TemperatureBack[roomB] -= heatDelta;
                    PressureBack[roomA] += pressureDelta;
                    PressureBack[roomB] -= pressureDelta;
                }
            }

            private float ResolveFakePressure(float roomVolume, float floodVolume, float steamVolume, float temperatureCelsius)
            {
                float flood01 = math.saturate(floodVolume / math.max(roomVolume, Epsilon));
                float steam01 = math.saturate(steamVolume / math.max(roomVolume, Epsilon));
                float heat01 = math.saturate((temperatureCelsius - ReferenceTemperatureCelsius) / math.max(1f, MaximumTemperatureCelsius - ReferenceTemperatureCelsius));
                float pressure01 = math.saturate((flood01 * 0.65f) + (steam01 * 0.35f) + (heat01 * 0.2f));
                return math.clamp(
                    math.lerp(ReferencePressureKPa, MaximumPressureKPa, pressure01),
                    0f,
                    MaximumPressureKPa);
            }

            private void ApplyBulkheadThermalConduction()
            {
                float conductionDeltaTime = math.max(0f, ThermalConductionDeltaTime);
                if (conductionDeltaTime <= Epsilon)
                    return;

                float conductivity = math.max(0f, BulkheadThermalConductivityWattsPerKelvin);
                if (conductivity <= Epsilon)
                    return;

                float sealedCoupling = math.saturate(SealedBulkheadThermalCoupling);
                float openCoupling = math.max(sealedCoupling, OpenBulkheadThermalCoupling);
                for (int doorIndex = 0; doorIndex < DoorCount; doorIndex++)
                {
                    int2 pair = DoorPairs[doorIndex];
                    int roomA = pair.x;
                    int roomB = pair.y;
                    if (roomA < 0 || roomA >= RoomCount || roomB < 0 || roomB >= RoomCount)
                        continue;

                    float temperatureA = TemperatureBack[roomA];
                    float temperatureB = TemperatureBack[roomB];
                    float temperatureDelta = temperatureA - temperatureB;
                    if (math.abs(temperatureDelta) <= Epsilon)
                        continue;

                    float capacityA = ResolveRoomThermalCapacity(roomA);
                    float capacityB = ResolveRoomThermalCapacity(roomB);
                    float totalCapacity = capacityA + capacityB;
                    if (totalCapacity <= Epsilon)
                        continue;

                    float equilibriumTemperature = ((temperatureA * capacityA) + (temperatureB * capacityB)) / totalCapacity;
                    float maxTransferEnergy = math.abs(temperatureA - equilibriumTemperature) * capacityA;
                    if (maxTransferEnergy <= Epsilon)
                        continue;

                    float coupling = DoorSealed[doorIndex] != 0 ? sealedCoupling : openCoupling;
                    if (coupling <= Epsilon)
                        continue;

                    float transferEnergy = conductivity * coupling * temperatureDelta * conductionDeltaTime;
                    float transferMagnitude = math.min(math.abs(transferEnergy), maxTransferEnergy);
                    if (transferMagnitude <= Epsilon)
                        continue;

                    float signedEnergy = math.sign(transferEnergy) * transferMagnitude;
                    TemperatureBack[roomA] = math.clamp(
                        temperatureA - (signedEnergy / capacityA),
                        MinimumTemperatureCelsius,
                        MaximumTemperatureCelsius);
                    TemperatureBack[roomB] = math.clamp(
                        temperatureB + (signedEnergy / capacityB),
                        MinimumTemperatureCelsius,
                        MaximumTemperatureCelsius);
                }
            }

            private float ResolveRoomThermalCapacity(int roomIndex)
            {
                float roomVolume = math.max(RoomVolumes[roomIndex], MinimumGasVolumeCubicMeters);
                float floodVolume = math.clamp(FloodVolumes[roomIndex], 0f, roomVolume - Epsilon);
                float gasVolume = math.max(MinimumGasVolumeCubicMeters, roomVolume - floodVolume);
                float airMassKilograms = math.max(0f, gasVolume * math.max(0.1f, AirDensityKilogramsPerCubicMeter));
                float waterMassKilograms = math.max(0f, floodVolume * math.max(1f, WaterDensityKilogramsPerCubicMeter));
                float airCapacity = airMassKilograms * math.max(1f, AirSpecificHeatJoulesPerKilogramKelvin);
                float waterCapacity = waterMassKilograms * math.max(1f, WaterSpecificHeatJoulesPerKilogramKelvin);
                return math.max(MinimumThermalCapacityJoulesPerKelvin, airCapacity + waterCapacity);
            }
        }

        [Header("── References ──────────────────")]
        [Tooltip("Flood-compartment owner that provides room capacities, flood displacement, and sealed-door topology.")]
        [SerializeField] private SubmarineFluidDynamics fluidDynamics;

        [Header("── Atmosphere Rooms ──────────────────")]
        [Tooltip("Per-room initial fractions and metabolic sources. Entries map 1:1 to the submarine fluid compartments.")]
        [SerializeField] private RoomDefinition[] rooms = new RoomDefinition[RoomCapacity];

        [Header("── Gas Solver ──────────────────")]
        [Tooltip("Reference pressure used when a room is dry and filled with its authored gas volume.")]
        [SerializeField, Min(1f)] private float referencePressureKPa = DefaultReferencePressureKPa;

        [Tooltip("Legacy pressure setting retained for authored data compatibility. Cheap solver uses one-pass hatch averaging.")]
        [SerializeField, Min(0f)] private float doorConductance = DefaultDoorConductance;

        [Tooltip("Legacy transfer cap retained for authored data compatibility. Cheap solver does not iterate gas transfer.")]
        [SerializeField, Min(0f)] private float maxTransferUnitsPerSecond = DefaultMaxTransferUnitsPerSecond;

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

        [Tooltip("Atmosphere job cadence. 0.1 seconds is 10 Hz.")]
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

        [Tooltip("Audio log played once per cooldown when the occupied room falls below low-O2 threshold.")]
        [SerializeField] private AudioLogData lowOxygenGaspingAudioLog;

        [Tooltip("Minimum seconds between low-O2 gasping audio log triggers.")]
        [SerializeField, Min(0f)] private float lowOxygenAudioCooldownSeconds = DefaultLowOxygenAudioCooldownSeconds;

        [Header("── Thermodynamics ──────────────────")]
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
        [SerializeField, Min(0f)] private float bulkheadThermalConductivityWattsPerKelvin = DefaultBulkheadThermalConductivityWattsPerKelvin;

        [Tooltip("Fraction of bulkhead conductivity applied while the connecting door is sealed.")]
        [SerializeField, Range(0f, 1f)] private float sealedBulkheadThermalCoupling = DefaultSealedBulkheadThermalCoupling;

        [Tooltip("Multiplier applied to bulkhead conductivity while the connecting door is open.")]
        [SerializeField, Min(0f)] private float openBulkheadThermalCoupling = DefaultOpenBulkheadThermalCoupling;
        [Header("Steam Phase Change")]
        [Tooltip("Legacy temperature reference retained for authored data compatibility.")]
        [SerializeField, Min(1f)] private float referenceTemperatureKelvin = DefaultReferenceTemperatureKelvin;
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

        [Header("── Abyssal Freeze ──────────────────")]
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

        [Header("── Boiling Flood Hazard ──────────────────")]
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

        [Header("── Reactor Meltdown ──────────────────")]
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
        [Header("â”€â”€ Thermal Material Stress â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Room temperature threshold in Celsius where material-specific structural-fatigue scaling begins.")]
        [SerializeField] private float thermalFatigueThresholdCelsius = DefaultThermalFatigueThresholdCelsius;
        [Tooltip("Structural fatigue multiplier applied to glass rooms above the thermal threshold.")]
        [SerializeField, Min(0f)] private float glassThermalFatigueMultiplier = DefaultGlassThermalFatigueMultiplier;
        [Tooltip("Structural fatigue multiplier applied to titanium rooms above the thermal threshold.")]
        [SerializeField, Min(0f)] private float titaniumThermalFatigueMultiplier = DefaultTitaniumThermalFatigueMultiplier;

        [Header("── Explosive Electrolysis ──────────────────")]
        [Tooltip("Per-second decay applied to explosive electrolysis gas pockets.")]
        [SerializeField, Min(0f)] private float explosivePocketDecayPerSecond = DefaultExplosivePocketDecayPerSecond;

        [Tooltip("Minimum combined hydrogen/oxygen pocket intensity required before a spark detonates the compartment.")]
        [SerializeField, Range(0f, 1f)] private float explosivePocketThreshold = DefaultExplosionPocketThreshold;

        [Tooltip("Impulse scale applied to the submarine rigidbody when an explosive pocket detonates.")]
        [SerializeField, Min(0f)] private float explosionImpulsePerPocketUnit = DefaultExplosionImpulsePerPocketUnit;

        [Tooltip("Safety cap on one electrolysis explosion impulse.")]
        [SerializeField, Min(1f)] private float explosionMaximumImpulseNewtonSeconds = DefaultExplosionMaximumImpulseNewtonSeconds;

        [Tooltip("Pressure spike injected into the room when a flooded overloaded node electrolyzes violently.")]
        [SerializeField, Min(0f)] private float electrolysisPressureSpikeKPa = DefaultExplosionPressureSpikeKPa;

        [Header("── Pressure Blowout ──────────────────")]
        [Tooltip("Radius around an opened bulkhead that receives the pressure blowout impulse.")]
        [SerializeField, Min(0.25f)] private float pressureImpulseRadiusMeters = DefaultPressureImpulseRadiusMeters;

        [Tooltip("Impulse duration used to convert raw pressure force into a one-shot rigidbody impulse.")]
        [SerializeField, Min(0.001f)] private float pressureImpulseDurationSeconds = DefaultPressureImpulseDurationSeconds;

        [Tooltip("Distance falloff exponent applied to bodies near the bulkhead opening.")]
        [SerializeField, Min(0.25f)] private float pressureImpulseFalloffExponent = DefaultPressureImpulseFalloffExponent;

        [Tooltip("Safety cap on one blowout impulse magnitude in newton-seconds.")]
        [SerializeField, Min(1f)] private float maximumPressureImpulseNewtonSeconds = DefaultMaximumPressureImpulseNewtonSeconds;

        [Tooltip("Rigidbodies on these layers receive the blowout impulse.")]
        [SerializeField] private LayerMask pressureImpulseLayers = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        [Header("── Diagnostics ──────────────────")]
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
        private Rigidbody _submarineBody;
        private bool _registered;
        private bool _topologySeeded;
        private bool _thermalEmittersSeeded;
        private JobHandle _atmosphereJobHandle;
        private JobHandle _disposeHandle;
        private bool _atmosphereJobRunning;

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
        private NativeArray<int2> _doorPairs;
        private NativeArray<byte> _doorSealed;
        private NativeArray<byte> _doorSealedPrevious;
        // COLD ALLOC: Collider[32] — one-shot non-alloc bulkhead blowout overlap buffer — owner: SubmarineAtmosphereSystem
        private readonly Collider[] _pressureImpulseOverlapBuffer = new Collider[PressureImpulseOverlapCapacity];
        // COLD ALLOC: Rigidbody[32] — unique-body scratch for pressure blowout dispatch — owner: SubmarineAtmosphereSystem
        private readonly Rigidbody[] _pressureImpulseBodyBuffer = new Rigidbody[PressureImpulseOverlapCapacity];
        // COLD ALLOC: int[8] â€” per-room boiling hazard source IDs â€” owner: SubmarineAtmosphereSystem
        private readonly int[] _boilingHazardIds = new int[RoomCapacity];
        // COLD ALLOC: int[8] - per-room low-O2 toxicity hazard source IDs - owner: SubmarineAtmosphereSystem
        private readonly int[] _toxicRoomHazardIds = new int[RoomCapacity];
        // COLD ALLOC: int[8] - per-room fake smoke hazard source IDs - owner: SubmarineAtmosphereSystem
        private readonly int[] _fireSmokeHazardIds = new int[RoomCapacity];
        // COLD ALLOC: BaseModule[8] — cached room-to-base brownout links — owner: SubmarineAtmosphereSystem
        private readonly BaseModule[] _brownoutRoomModules = new BaseModule[RoomCapacity];
        // COLD ALLOC: BaseModule[8] - cached room-to-base module links for visual atmosphere fakes - owner: SubmarineAtmosphereSystem
        private readonly BaseModule[] _atmosphereRoomModules = new BaseModule[RoomCapacity];
        private uint _overheatVisualActiveMask;
        // COLD ALLOC: SpatialQueryHit[16] â€” fauna spillover query scratch for boiling rooms â€” owner: SubmarineAtmosphereSystem
        private readonly SpatialQueryHit[] _boilingFaunaContacts = new SpatialQueryHit[BoilingFaunaContactCapacity];
        // COLD ALLOC: FabricatorHeatEmitter[24] — cached fabricator heat sources mapped to rooms — owner: SubmarineAtmosphereSystem
        private readonly FabricatorHeatEmitter[] _fabricatorHeatEmitters = new FabricatorHeatEmitter[HeatEmitterCapacity];
        // COLD ALLOC: DrillHeatEmitter[24] — cached drill heat sources mapped to rooms — owner: SubmarineAtmosphereSystem
        private readonly DrillHeatEmitter[] _drillHeatEmitters = new DrillHeatEmitter[HeatEmitterCapacity];
        // COLD ALLOC: ReactorHeatEmitter[24] — cached reactor heat sources mapped to rooms — owner: SubmarineAtmosphereSystem
        private readonly ReactorHeatEmitter[] _reactorHeatEmitters = new ReactorHeatEmitter[HeatEmitterCapacity];
        // COLD ALLOC: bool[24] — one-shot reactor meltdown guards keyed to cached emitter slots — owner: SubmarineAtmosphereSystem
        private readonly bool[] _reactorMeltdownTriggered = new bool[HeatEmitterCapacity];
        // COLD ALLOC: List<Fabricator>[8] — cold-path fabricator scan scratch for thermal emitter cache — owner: SubmarineAtmosphereSystem
        private readonly System.Collections.Generic.List<Fabricator> _fabricatorScanBuffer = new System.Collections.Generic.List<Fabricator>(8);
        // COLD ALLOC: List<DeepDrillModule>[8] — cold-path drill scan scratch for thermal emitter cache — owner: SubmarineAtmosphereSystem
        private readonly System.Collections.Generic.List<DeepDrillModule> _drillScanBuffer = new System.Collections.Generic.List<DeepDrillModule>(8);
        // COLD ALLOC: List<BioReactor>[8] — cold-path reactor scan scratch for thermal emitter cache — owner: SubmarineAtmosphereSystem
        private readonly System.Collections.Generic.List<BioReactor> _reactorScanBuffer = new System.Collections.Generic.List<BioReactor>(8);
        private readonly System.Collections.Generic.List<LogisticsPipeNode> _ventPipeScanBuffer = new System.Collections.Generic.List<LogisticsPipeNode>(16);
        private int _fabricatorHeatEmitterCount;
        private int _drillHeatEmitterCount;
        private int _reactorHeatEmitterCount;
        private float _thermalConductionAccumulator;
        private float _atmosphereStepAccumulator;
        private float _lowOxygenAudioCooldownRemaining;

        public int RoomCount => fluidDynamics != null ? fluidDynamics.CompartmentCount : 0;

        public float GetRoomPressureKPa(int roomIndex)
        {
            if (!_pressureFront.IsCreated || roomIndex < 0 || roomIndex >= RoomCount)
                return referencePressureKPa;

            return _pressureFront[roomIndex];
        }

        public float GetRoomOxygenFraction(int roomIndex)
        {
            if (!_o2Front.IsCreated || roomIndex < 0 || roomIndex >= RoomCount)
                return DefaultInitialOxygenFraction;

            return math.saturate(_o2Front[roomIndex] / math.max(1f, oxygenTankCapacity));
        }

        public float GetRoomCarbonDioxideFraction(int roomIndex)
        {
            if (!_co2Front.IsCreated || roomIndex < 0 || roomIndex >= RoomCount)
                return DefaultInitialCarbonDioxideFraction;

            return math.saturate(_co2Front[roomIndex] / math.max(1f, oxygenTankCapacity));
        }

        public float GetRoomTemperatureCelsius(int roomIndex)
        {
            if (!_temperatureFront.IsCreated || roomIndex < 0 || roomIndex >= RoomCount)
                return referenceTemperatureCelsius;

            return _temperatureFront[roomIndex];
        }

        public float GetRoomFloodFillRatio(int roomIndex)
        {
            if (!_floodVolumes.IsCreated || !_roomVolumes.IsCreated || roomIndex < 0 || roomIndex >= RoomCount)
                return 0f;

            float roomVolume = math.max(Epsilon, _roomVolumes[roomIndex]);
            return math.saturate(_floodVolumes[roomIndex] / roomVolume);
        }

        public void InjectOxygenUnits(int roomIndex, float oxygenUnits)
        {
            if (oxygenUnits <= 0f ||
                !_o2Front.IsCreated ||
                !_co2Front.IsCreated ||
                !_inertFront.IsCreated ||
                !_pressureFront.IsCreated ||
                !_gasVolumeFront.IsCreated ||
                roomIndex < 0 ||
                roomIndex >= RoomCount)
            {
                return;
            }

            CompleteAtmosphereJobForAuthoritativeWrite();
            float currentOxygenUnits = math.max(0f, _o2Front[roomIndex]);
            float maximumOxygenUnits = ResolveRoomMaxOxygenCapacityUnits(roomIndex);
            float clampedOxygenDelta = math.min(oxygenUnits, math.max(0f, maximumOxygenUnits - currentOxygenUnits));
            if (clampedOxygenDelta <= 0f)
                return;

            _o2Front[roomIndex] = currentOxygenUnits + clampedOxygenDelta;
            RefreshRoomPressureImmediate(roomIndex);
        }

        internal float TransferOxygenFromStorage(int roomIndex, float requestedOxygenUnits, ref float storageOxygenUnits)
        {
            if (requestedOxygenUnits <= 0f ||
                storageOxygenUnits <= 0f ||
                !_o2Front.IsCreated ||
                roomIndex < 0 ||
                roomIndex >= RoomCount)
            {
                return 0f;
            }

            CompleteAtmosphereJobForAuthoritativeWrite();
            float currentOxygenUnits = math.max(0f, _o2Front[roomIndex]);
            float capacity = ResolveRoomMaxOxygenCapacityUnits(roomIndex);
            float transfer = math.min(
                math.min(requestedOxygenUnits, storageOxygenUnits),
                math.max(0f, capacity - currentOxygenUnits));
            if (transfer <= Epsilon)
                return 0f;

            _o2Front[roomIndex] = currentOxygenUnits + transfer;
            storageOxygenUnits = math.max(0f, storageOxygenUnits - transfer);
            RefreshRoomPressureImmediate(roomIndex);
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

            InjectOxygenUnits(roomIndex, oxygenUnits);
            return oxygenUnits;
        }

        public void InjectRoomTemperatureDeltaCelsius(int roomIndex, float deltaCelsius)
        {
            if (deltaCelsius == 0f || !_temperatureFront.IsCreated || roomIndex < 0 || roomIndex >= RoomCount)
                return;

            CompleteAtmosphereJobForAuthoritativeWrite();
            _temperatureFront[roomIndex] = math.clamp(
                _temperatureFront[roomIndex] + deltaCelsius,
                minimumTemperatureCelsius,
                maximumTemperatureCelsius);
        }

        public void InjectRoomHeatEnergyJoules(int roomIndex, float heatEnergyJoules)
        {
            if (heatEnergyJoules <= 0f || !_temperatureFront.IsCreated || roomIndex < 0 || roomIndex >= RoomCount)
                return;

            CompleteAtmosphereJobForAuthoritativeWrite();
            float thermalCapacity = ResolveInstantThermalCapacity(roomIndex);
            if (thermalCapacity <= Epsilon)
                return;

            float deltaCelsius = heatEnergyJoules / thermalCapacity;
            if (!math.isfinite(deltaCelsius) || deltaCelsius <= 0f)
                return;

            _temperatureFront[roomIndex] = math.clamp(
                _temperatureFront[roomIndex] + deltaCelsius,
                minimumTemperatureCelsius,
                maximumTemperatureCelsius);
        }

        public void TransferRoomHeatEnergyJoules(int sourceRoomIndex, int destinationRoomIndex, float heatEnergyJoules)
        {
            if (heatEnergyJoules <= 0f ||
                !_temperatureFront.IsCreated ||
                sourceRoomIndex < 0 || sourceRoomIndex >= RoomCount ||
                destinationRoomIndex < 0 || destinationRoomIndex >= RoomCount ||
                sourceRoomIndex == destinationRoomIndex)
            {
                return;
            }

            CompleteAtmosphereJobForAuthoritativeWrite();

            float sourceCapacity = ResolveInstantThermalCapacity(sourceRoomIndex);
            float destinationCapacity = ResolveInstantThermalCapacity(destinationRoomIndex);
            if (sourceCapacity <= Epsilon || destinationCapacity <= Epsilon)
                return;

            float sourceDelta = heatEnergyJoules / sourceCapacity;
            float destinationDelta = heatEnergyJoules / destinationCapacity;
            if (!math.isfinite(sourceDelta) || !math.isfinite(destinationDelta) || sourceDelta <= 0f || destinationDelta <= 0f)
                return;

            _temperatureFront[sourceRoomIndex] = math.clamp(
                _temperatureFront[sourceRoomIndex] - sourceDelta,
                minimumTemperatureCelsius,
                maximumTemperatureCelsius);
            _temperatureFront[destinationRoomIndex] = math.clamp(
                _temperatureFront[destinationRoomIndex] + destinationDelta,
                minimumTemperatureCelsius,
                maximumTemperatureCelsius);
        }

        public void InjectElectrolysisGasPocket(int roomIndex, float hydrogenUnits, float oxygenUnits, float pressureSpikeKPa)
        {
            if (roomIndex < 0 || roomIndex >= RoomCount || !_hydrogenPocketFront.IsCreated || !_oxygenPocketFront.IsCreated)
                return;

            CompleteAtmosphereJobForAuthoritativeWrite();
            _hydrogenPocketFront[roomIndex] = math.max(0f, _hydrogenPocketFront[roomIndex] + hydrogenUnits);
            _oxygenPocketFront[roomIndex] = math.max(0f, _oxygenPocketFront[roomIndex] + oxygenUnits);
            if (_pressureFront.IsCreated && pressureSpikeKPa > 0f)
            {
                _pressureFront[roomIndex] = math.clamp(
                    _pressureFront[roomIndex] + pressureSpikeKPa,
                    0f,
                    maximumPressureKPa);
            }
        }

        public float GetRoomSteamVolumeCubicMeters(int roomIndex)
        {
            if (!_steamFront.IsCreated || roomIndex < 0 || roomIndex >= RoomCount)
                return 0f;

            return math.max(0f, _steamFront[roomIndex]);
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

            float thresholdTemperature = math.max(referenceTemperatureCelsius, thermalFatigueThresholdCelsius);
            if (_temperatureFront[roomIndex] < thresholdTemperature)
                return 1f;

            RoomStructuralMaterial structuralMaterial = roomIndex < rooms.Length
                ? rooms[roomIndex].primaryStructuralMaterial
                : RoomStructuralMaterial.Titanium;

            return structuralMaterial == RoomStructuralMaterial.Glass
                ? math.max(0f, glassThermalFatigueMultiplier)
                : math.max(0f, titaniumThermalFatigueMultiplier);
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
                return _cachedTransform != null ? _cachedTransform.position : Vector3.zero;

            Vector3 localCentroid = fluidDynamics.GetCompartmentCentroid(roomIndex);
            return _cachedTransform != null ? _cachedTransform.TransformPoint(localCentroid) : localCentroid;
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
            return fluidDynamics != null ? math.max(0f, fluidDynamics.ExternalDepthMeters) : 0f;
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

            float pocketIntensity = math.min(_hydrogenPocketFront[roomIndex], _oxygenPocketFront[roomIndex]);
            if (pocketIntensity < math.saturate(explosivePocketThreshold))
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
            if (fluidDynamics == null)
                return;

            EnsureNativeState();
            SyncFluidSnapshot();
            SeedTopologyIfNeeded();
            SeedThermalEmittersIfNeeded();
            AccumulateRoomHeatSources();
            PublishDoorOpeningPressureEvents();
            _thermalConductionAccumulator += fixedDeltaTime;
            float thermalConductionDeltaTime = 0f;
            if (_thermalConductionAccumulator + Epsilon >= ThermalConductionCadenceSeconds)
            {
                thermalConductionDeltaTime = _thermalConductionAccumulator;
                _thermalConductionAccumulator = 0f;
            }

            _atmosphereStepAccumulator += fixedDeltaTime;
            float slowTickSeconds = math.max(0.02f, atmosphereSlowTickSeconds);
            if (_atmosphereStepAccumulator + Epsilon < slowTickSeconds)
            {
                RefreshDebugState();
                return;
            }

            float atmosphereDeltaTime = _atmosphereStepAccumulator;
            _atmosphereStepAccumulator = 0f;
            ScheduleAtmosphereJob(atmosphereDeltaTime, thermalConductionDeltaTime);
            RefreshDebugState();
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            ConsumeCompletedJob(fixedDeltaTime);
        }

        private void ApplyAbyssalBlackoutFreeze(float fixedDeltaTime)
        {
            if (fluidDynamics == null || !_temperatureFront.IsCreated || !_floodVolumes.IsCreated || !_roomVolumes.IsCreated)
                return;

            float depthMeters = math.max(0f, fluidDynamics.ExternalDepthMeters);
            if (depthMeters < math.max(0f, deepFreezeDepthThresholdMeters))
                return;

            IPowerGridService powerGridService = GlobalRegistry.PowerGrid;
            float totalConsumption = powerGridService != null ? math.max(0f, powerGridService.TotalConsumption) : 0f;
            float supplyRatio = totalConsumption > Epsilon
                ? math.saturate(math.max(0f, powerGridService.TotalGeneration) / totalConsumption)
                : 1f;
            if (supplyRatio >= math.saturate(deepFreezeSupplyRatioThreshold))
                return;

            float targetTemperature = math.clamp(
                deepFreezeTargetTemperatureCelsius,
                minimumTemperatureCelsius,
                maximumTemperatureCelsius);
            float alpha = ResolveBlendFactor(math.max(0.1f, deepFreezeTauSeconds), fixedDeltaTime);
            for (int roomIndex = 0; roomIndex < RoomCount; roomIndex++)
            {
                float roomVolume = math.max(Epsilon, _roomVolumes[roomIndex]);
                float floodFillRatio = math.saturate(_floodVolumes[roomIndex] / roomVolume);
                if (floodFillRatio <= Epsilon)
                    continue;

                float currentTemperature = _temperatureFront[roomIndex];
                _temperatureFront[roomIndex] = math.clamp(
                    math.lerp(currentTemperature, targetTemperature, alpha),
                    minimumTemperatureCelsius,
                    maximumTemperatureCelsius);
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

            float safeSteamExpansionRatio = math.max(1f, steamExpansionRatio);
            float vaporizationRate = math.max(0f, steamGenerationRateCubicMetersPerSecondPerCelsius);
            float condensationRate = math.max(0f, steamCondensationCoefficient);
            float hullShellTemperature = ResolveHullShellTemperatureCelsius();

            for (int roomIndex = 0; roomIndex < RoomCount; roomIndex++)
            {
                float roomTemperature = _temperatureFront[roomIndex];
                float floodVolume = math.max(0f, _floodVolumes[roomIndex]);
                float steamVolume = math.max(0f, _steamFront[roomIndex]);
                float boilingPoint = ResolveRoomBoilingPointCelsius(roomIndex);

                if (floodVolume > Epsilon && roomTemperature > boilingPoint)
                {
                    float overshootCelsius = roomTemperature - boilingPoint;
                    float liquidVaporized = math.min(
                        floodVolume,
                        overshootCelsius * vaporizationRate * math.max(0f, fixedDeltaTime));
                    if (liquidVaporized > Epsilon)
                    {
                        fluidDynamics.AddCompartmentFloodVolumeDelta(roomIndex, -liquidVaporized);
                        _floodVolumes[roomIndex] = math.max(0f, _floodVolumes[roomIndex] - liquidVaporized);
                        steamVolume += liquidVaporized * safeSteamExpansionRatio;
                    }
                }

                if (steamVolume > Epsilon && roomTemperature > hullShellTemperature)
                {
                    float condensedSteamVolume = math.min(
                        steamVolume,
                        (roomTemperature - hullShellTemperature) * condensationRate * math.max(0f, fixedDeltaTime));
                    if (condensedSteamVolume > Epsilon)
                    {
                        steamVolume -= condensedSteamVolume;
                        float returnedLiquidVolume = condensedSteamVolume / safeSteamExpansionRatio;
                        fluidDynamics.AddCompartmentFloodVolumeDelta(roomIndex, returnedLiquidVolume);
                        _floodVolumes[roomIndex] += returnedLiquidVolume;
                    }
                }

                if (_gasVolumeFront.IsCreated)
                {
                    float roomVolume = math.max(minimumGasVolumeCubicMeters, _roomVolumes[roomIndex]);
                    _gasVolumeFront[roomIndex] = math.max(minimumGasVolumeCubicMeters, roomVolume - _floodVolumes[roomIndex]);
                }

                _steamFront[roomIndex] = math.max(0f, steamVolume);
                RecomputeInstantRoomPressure(roomIndex);
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
                float roomPressure = _pressureFront[roomIndex];
                if (roomPressure <= ventThresholdPressure)
                    continue;

                if (!TryResolveEmergencyVentPipe(roomIndex, out LogisticsPipeNode ventPipe))
                {
                    if (roomPressure >= math.max(ventThresholdPressure, maximumPressureKPa * 0.98f))
                        TriggerSteamOverpressureFailure(roomIndex, roomPressure);
                    continue;
                }

                float releaseFraction = math.saturate(steamVentReleaseFraction);
                if (releaseFraction <= Epsilon)
                    continue;

                _steamFront[roomIndex] = math.max(0f, _steamFront[roomIndex] * (1f - releaseFraction));
                _hydrogenPocketFront[roomIndex] = math.max(0f, _hydrogenPocketFront[roomIndex] * (1f - releaseFraction));
                _oxygenPocketFront[roomIndex] = math.max(0f, _oxygenPocketFront[roomIndex] * (1f - releaseFraction));
                _o2Front[roomIndex] = math.max(0f, _o2Front[roomIndex] * (1f - (releaseFraction * 0.5f)));
                _co2Front[roomIndex] = math.max(0f, _co2Front[roomIndex] * (1f - (releaseFraction * 0.5f)));
                _inertFront[roomIndex] = math.max(0f, _inertFront[roomIndex] * (1f - (releaseFraction * 0.5f)));
                RecomputeInstantRoomPressure(roomIndex);

                Vector3 ventPosition = ventPipe.ResolveVentRuntimePosition();
                Vector3 ventDirection = ventPipe.ResolveVentDirection(_submarineBody.worldCenterOfMass);
                float overshootKPa = math.max(0f, roomPressure - ventThresholdPressure);
                float impulseMagnitude = overshootKPa *
                                         math.max(0f, steamVentImpulsePerKilopascal) *
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
            float overshoot01 = math.saturate((roomPressure - referencePressureKPa) / math.max(1f, maximumPressureKPa - referencePressureKPa));
            float explosionPocket = math.max(
                overshoot01,
                math.min(_hydrogenPocketFront[roomIndex], _oxygenPocketFront[roomIndex]));
            TriggerExplosivePocketDetonation(roomIndex, roomPosition, math.max(explosivePocketThreshold, explosionPocket));
            fluidDynamics.TriggerBreach(roomIndex, math.max(0.25f, overshoot01));
        }

        private static float ResolveBlendFactor(float tauSeconds, float deltaTime)
        {
            float safeTau = math.max(0.0001f, tauSeconds);
            float safeDeltaTime = math.max(0f, deltaTime);
            return math.saturate(1f - math.exp(-safeDeltaTime / safeTau));
        }

        private void DecayExplosivePockets(float fixedDeltaTime)
        {
            if (!_hydrogenPocketFront.IsCreated || !_oxygenPocketFront.IsCreated || fixedDeltaTime <= 0f)
                return;

            float decay = math.max(0f, explosivePocketDecayPerSecond) * fixedDeltaTime;
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                _hydrogenPocketFront[roomIndex] = math.max(0f, _hydrogenPocketFront[roomIndex] - decay);
                _oxygenPocketFront[roomIndex] = math.max(0f, _oxygenPocketFront[roomIndex] - decay);
            }
        }

        private void TriggerExplosivePocketDetonation(int roomIndex, Vector3 runtimeHitPoint, float pocketIntensity)
        {
            if (_submarineBody == null || roomIndex < 0 || roomIndex >= RoomCount)
                return;

            Vector3 roomPosition = ResolveRoomRuntimePosition(roomIndex);
            Vector3 centerDirection = roomPosition - _submarineBody.worldCenterOfMass;
            Vector3 forceDirection = SafeNormalize(Vector3.Lerp(centerDirection, Vector3.up, 0.35f), Vector3.up);
            float impulseMagnitude = math.min(
                math.max(0f, pocketIntensity) * math.max(0f, explosionImpulsePerPocketUnit),
                math.max(1f, explosionMaximumImpulseNewtonSeconds));

            PhysicsForceRouter.QueueForceAtPosition(
                _submarineBody,
                forceDirection * impulseMagnitude,
                runtimeHitPoint,
                ForceMode.Impulse);

            _hydrogenPocketFront[roomIndex] = 0f;
            _oxygenPocketFront[roomIndex] = 0f;
            if (_pressureFront.IsCreated)
            {
                _pressureFront[roomIndex] = math.min(
                    maximumPressureKPa,
                _pressureFront[roomIndex] + math.max(0f, electrolysisPressureSpikeKPa));
            }
        }

        private float ResolveHullShellTemperatureCelsius()
        {
            if (fluidDynamics == null)
                return floodWaterTemperatureCelsius;

            float depthMeters = math.max(0f, fluidDynamics.ExternalDepthMeters);
            float abyssalCooling = math.saturate(depthMeters / 4000f);
            return math.lerp(referenceTemperatureCelsius, floodWaterTemperatureCelsius, abyssalCooling);
        }

        private float ResolveRoomBoilingPointCelsius(int roomIndex)
        {
            float externalDepthMeters = fluidDynamics != null ? math.max(0f, fluidDynamics.ExternalDepthMeters) : 0f;
            float pressureKPa = roomIndex >= 0 && roomIndex < RoomCount && _pressureFront.IsCreated
                ? math.max(referencePressureKPa, _pressureFront[roomIndex])
                : referencePressureKPa;
            float pressureDepthEquivalent = math.max(0f, pressureKPa - referencePressureKPa);
            return 100f + ((externalDepthMeters + (pressureDepthEquivalent * 0.1f)) * 0.02f);
        }

        private void RecomputeInstantRoomPressure(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCount || !_pressureFront.IsCreated || !_gasVolumeFront.IsCreated)
                return;

            float temperatureCelsius = _temperatureFront.IsCreated ? _temperatureFront[roomIndex] : referenceTemperatureCelsius;
            _pressureFront[roomIndex] = ResolveInstantFakePressure(roomIndex, temperatureCelsius);
        }

        private void RefreshRoomPressureImmediate(int roomIndex)
        {
            RecomputeInstantRoomPressure(roomIndex);
        }

        private float ResolveInstantPressureWithTemperature(float totalGasUnits, float gasVolumeCubicMeters, float temperatureCelsius)
        {
            return ResolveSimplePressureKPa(gasVolumeCubicMeters, 0f, 0f, temperatureCelsius);
        }

        private float ResolveRoomMaxOxygenCapacityUnits(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCount)
                return 0f;

            return math.max(1f, oxygenTankCapacity);
        }

        private float ResolveEmergencyVentThresholdPressureKPa()
        {
            float pressureCap = math.max(referencePressureKPa, maximumPressureKPa);
            float ratioThreshold = math.saturate(steamVentMinimumPressureRatio);
            float hullThreshold = fluidDynamics != null ? fluidDynamics.HullPressureRatingKPa : pressureCap;
            return math.min(hullThreshold, pressureCap * math.max(0.1f, ratioThreshold));
        }

        private bool TryResolveEmergencyVentPipe(int roomIndex, out LogisticsPipeNode ventPipe)
        {
            ventPipe = null;
            _ventPipeScanBuffer.Clear();
            GetComponentsInChildren(true, _ventPipeScanBuffer);
            int pipeCount = _ventPipeScanBuffer.Count;
            for (int pipeIndex = 0; pipeIndex < pipeCount; pipeIndex++)
            {
                LogisticsPipeNode pipe = _ventPipeScanBuffer[pipeIndex];
                if (pipe == null || !pipe.CanEmergencyVent)
                    continue;

                if (pipe.ResolveAmbientRoomIndex() != roomIndex)
                    continue;

                ventPipe = pipe;
                return true;
            }

            return false;
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
                    _playerTransform = playerContext.PlayerTransform;
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
            if (fixedDeltaTime > 0f)
                _lowOxygenAudioCooldownRemaining = math.max(0f, _lowOxygenAudioCooldownRemaining - fixedDeltaTime);

            if (fluidDynamics == null || !_o2Front.IsCreated || !_temperatureFront.IsCreated)
            {
                ClearAtmosphereFakes();
                return;
            }

            int roomCount = RoomCount;
            float lowOxygenValue = math.saturate(lowOxygenThreshold01) * math.max(1f, oxygenTankCapacity);
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                if (roomIndex >= roomCount)
                {
                    HectonHazardManager.Unregister(_toxicRoomHazardIds[roomIndex]);
                    HectonHazardManager.Unregister(_fireSmokeHazardIds[roomIndex]);
                    ResetOverheatVisual(roomIndex);
                    continue;
                }

                float oxygenValue = _o2Front[roomIndex];
                bool lowOxygen = oxygenValue < lowOxygenValue;
                bool playerOccupied = _roomPlayerCounts.IsCreated && _roomPlayerCounts[roomIndex] > 0;
                if (lowOxygen && playerOccupied)
                    TryPlayLowOxygenGaspingAudioLog();

                BaseModule roomModule = ResolveModuleForRoom(roomIndex);
                bool fireSmoke = roomModule != null && roomModule.CurrentFailureMode == BaseModuleFailureMode.Fire;
                bool hasBounds = TryResolveRoomHazardBounds(roomIndex, out Vector3 worldCenter, out float radius);

                if (lowOxygen && hasBounds)
                {
                    float oxygenDanger01 = math.saturate((lowOxygenValue - oxygenValue) / math.max(1f, lowOxygenValue));
                    HectonHazardManager.Register(
                        _toxicRoomHazardIds[roomIndex],
                        worldCenter,
                        oxygenDanger01 * math.max(0f, toxicRoomHazardIntensity),
                        radius,
                        HazardType.Toxicity,
                        1f);
                }
                else
                {
                    HectonHazardManager.Unregister(_toxicRoomHazardIds[roomIndex]);
                }

                if (fireSmoke && hasBounds)
                {
                    HectonHazardManager.Register(
                        _fireSmokeHazardIds[roomIndex],
                        worldCenter,
                        math.max(0f, fireSmokeHazardIntensity),
                        radius,
                        HazardType.Toxicity,
                        math.max(1f, fireSmokeVisorGlitchBias));
                }
                else
                {
                    HectonHazardManager.Unregister(_fireSmokeHazardIds[roomIndex]);
                }

                ApplyOverheatVoltageFake(roomIndex, roomModule);
            }
        }

        private void ClearAtmosphereFakes()
        {
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                HectonHazardManager.Unregister(_toxicRoomHazardIds[roomIndex]);
                HectonHazardManager.Unregister(_fireSmokeHazardIds[roomIndex]);
                ResetOverheatVisual(roomIndex);
            }
        }

        private void TryPlayLowOxygenGaspingAudioLog()
        {
            if (_lowOxygenAudioCooldownRemaining > 0f || lowOxygenGaspingAudioLog == null)
                return;

            AudioLogSystem audioLogs = GlobalRegistry.AudioLogs;
            if (audioLogs == null)
                return;

            audioLogs.PlayLog(lowOxygenGaspingAudioLog);
            _lowOxygenAudioCooldownRemaining = math.max(0f, lowOxygenAudioCooldownSeconds);
        }

        private void ApplyOverheatVoltageFake(int roomIndex, BaseModule roomModule)
        {
            if (roomIndex < 0 || roomIndex >= RoomCapacity || !_temperatureFront.IsCreated)
                return;

            float threshold = overheatBrownoutTemperatureCelsius;
            float temperature = _temperatureFront[roomIndex];
            if (roomModule == null || temperature <= threshold)
            {
                ResetOverheatVisual(roomIndex);
                return;
            }

            float heat01 = math.saturate((temperature - threshold) / math.max(1f, maximumTemperatureCelsius - threshold));
            float voltage = math.lerp(1f, math.saturate(overheatMinimumVoltage), math.saturate(heat01));
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
                bool powerBrownout = module.CachedPowerSupplyRatio < math.saturate(brownoutOxygenSupplyRatioThreshold);
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
            if (_roomVolumes.IsCreated)
                return;

            // COLD ALLOC: NativeArray<float>[8] — room gas-capacity snapshot aligned to submarine compartments — owner: SubmarineAtmosphereSystem
            _roomVolumes = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] — flood-volume snapshot consumed by the atmosphere solver — owner: SubmarineAtmosphereSystem
            _floodVolumes = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] — front O2 double buffer in reference-gas-volume units — owner: SubmarineAtmosphereSystem
            _o2Front = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] — back O2 double buffer in reference-gas-volume units — owner: SubmarineAtmosphereSystem
            _o2Back = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] — front CO2 double buffer in reference-gas-volume units — owner: SubmarineAtmosphereSystem
            _co2Front = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] — back CO2 double buffer in reference-gas-volume units — owner: SubmarineAtmosphereSystem
            _co2Back = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] — front inert-gas double buffer — owner: SubmarineAtmosphereSystem
            _inertFront = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] — back inert-gas double buffer — owner: SubmarineAtmosphereSystem
            _inertBack = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] — front room-pressure snapshot — owner: SubmarineAtmosphereSystem
            _pressureFront = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] — back room-pressure snapshot — owner: SubmarineAtmosphereSystem
            _pressureBack = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] — front available gas volume snapshot — owner: SubmarineAtmosphereSystem
            _gasVolumeFront = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] — back available gas volume snapshot — owner: SubmarineAtmosphereSystem
            _gasVolumeBack = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] — room O2 metabolic sink rates — owner: SubmarineAtmosphereSystem
            _o2ConsumptionRates = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] — room CO2 metabolic source rates — owner: SubmarineAtmosphereSystem
            _co2GenerationRates = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<int>[8] - local player occupancy counts consumed by the cheap atmosphere job - owner: SubmarineAtmosphereSystem
            _roomPlayerCounts = new NativeArray<int>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _temperatureFront = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _temperatureBack = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _steamFront = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _steamBack = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] — hydrogen-pocket accumulator for submerged overload electrolysis — owner: SubmarineAtmosphereSystem
            _hydrogenPocketFront = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] — oxygen-pocket accumulator for submerged overload electrolysis — owner: SubmarineAtmosphereSystem
            _oxygenPocketFront = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _roomHeatWatts = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<int2>[7] — door graph edges aligned to submarine bulkheads — owner: SubmarineAtmosphereSystem
            _doorPairs = new NativeArray<int2>(DoorCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<byte>[7] — sealed-door state copied from submarine bulkheads — owner: SubmarineAtmosphereSystem
            _doorSealed = new NativeArray<byte>(DoorCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<byte>[7] â€” previous sealed-door state used for door-opening pressure warnings â€” owner: SubmarineAtmosphereSystem
            _doorSealedPrevious = new NativeArray<byte>(DoorCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            RegisterNativeState();
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

            int roomCount = fluidDynamics.CompartmentCount;
            if (roomCount <= 0)
                return;

            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                if (roomIndex >= roomCount)
                {
                    _roomVolumes[roomIndex] = minimumGasVolumeCubicMeters;
                    _gasVolumeFront[roomIndex] = minimumGasVolumeCubicMeters;
                    _pressureFront[roomIndex] = referencePressureKPa;
                    _o2Front[roomIndex] = 0f;
                    _co2Front[roomIndex] = 0f;
                    _inertFront[roomIndex] = 0f;
                    _temperatureFront[roomIndex] = referenceTemperatureCelsius;
                    continue;
                }

                RoomDefinition definition = roomIndex < rooms.Length ? rooms[roomIndex] : default;
                float roomVolume = definition.gasCapacityOverrideCubicMeters > Epsilon
                    ? definition.gasCapacityOverrideCubicMeters
                    : fluidDynamics.GetCompartmentMaxFloodVolumeCubicMeters(roomIndex);
                roomVolume = math.max(roomVolume, minimumGasVolumeCubicMeters);

                float oxygenFraction = math.saturate(definition.initialOxygenFraction > Epsilon ? definition.initialOxygenFraction : DefaultInitialOxygenFraction);
                float carbonDioxideFraction = math.saturate(definition.initialCarbonDioxideFraction > 0f ? definition.initialCarbonDioxideFraction : DefaultInitialCarbonDioxideFraction);
                if (oxygenFraction + carbonDioxideFraction > 0.95f)
                {
                    float scale = 0.95f / math.max(oxygenFraction + carbonDioxideFraction, Epsilon);
                    oxygenFraction *= scale;
                    carbonDioxideFraction *= scale;
                }

                _roomVolumes[roomIndex] = roomVolume;
                _gasVolumeFront[roomIndex] = roomVolume;
                _o2Front[roomIndex] = math.saturate(oxygenFraction / math.max(DefaultInitialOxygenFraction, Epsilon)) * math.max(1f, oxygenTankCapacity);
                _co2Front[roomIndex] = math.saturate(carbonDioxideFraction) * math.max(1f, oxygenTankCapacity);
                _inertFront[roomIndex] = 0f;
                _pressureFront[roomIndex] = referencePressureKPa;
                _temperatureFront[roomIndex] = math.clamp(
                    definition.initialTemperatureCelsius != 0f ? definition.initialTemperatureCelsius : referenceTemperatureCelsius,
                    minimumTemperatureCelsius,
                    maximumTemperatureCelsius);
                _o2ConsumptionRates[roomIndex] = math.max(0f, definition.oxygenConsumptionUnitsPerSecond);
                _co2GenerationRates[roomIndex] = math.max(0f, definition.carbonDioxideGenerationUnitsPerSecond);
            }

            for (int doorIndex = 0; doorIndex < DoorCapacity; doorIndex++)
            {
                if (fluidDynamics.TryGetBulkheadDefinition(doorIndex, out int compartmentA, out int compartmentB, out bool isSealed))
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

            _topologySeeded = true;
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
                    RoomIndex = ResolveNearestRoomIndex(fabricator.transform.position)
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
                    RoomIndex = ResolveNearestRoomIndex(drill.transform.position)
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
                    RoomIndex = ResolveNearestRoomIndex(reactor.transform.position)
                };
            }

            for (int i = _reactorHeatEmitterCount; i < HeatEmitterCapacity; i++)
                _reactorMeltdownTriggered[i] = false;

            _thermalEmittersSeeded = true;
        }

        private void SyncFluidSnapshot()
        {
            if (fluidDynamics == null)
                return;

            int roomCount = fluidDynamics.CompartmentCount;
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                if (_roomPlayerCounts.IsCreated)
                    _roomPlayerCounts[roomIndex] = 0;

                if (roomIndex >= roomCount)
                {
                    _floodVolumes[roomIndex] = 0f;
                    continue;
                }

                RoomDefinition definition = roomIndex < rooms.Length ? rooms[roomIndex] : default;
                float roomVolume = definition.gasCapacityOverrideCubicMeters > Epsilon
                    ? definition.gasCapacityOverrideCubicMeters
                    : fluidDynamics.GetCompartmentMaxFloodVolumeCubicMeters(roomIndex);
                _roomVolumes[roomIndex] = math.max(roomVolume, minimumGasVolumeCubicMeters);
                _floodVolumes[roomIndex] = math.clamp(fluidDynamics.GetCompartmentFloodVolumeCubicMeters(roomIndex), 0f, _roomVolumes[roomIndex] - Epsilon);
                float oxygenConsumptionRate = math.max(
                    math.max(0f, definition.oxygenConsumptionUnitsPerSecond),
                    math.max(0f, playerOxygenConsumptionPercentPerSecond));
                ApplyBrownoutOccupiedRoomOxygenDrain(roomIndex, ref oxygenConsumptionRate);
                _o2ConsumptionRates[roomIndex] = oxygenConsumptionRate;
                _co2GenerationRates[roomIndex] = math.max(0f, definition.carbonDioxideGenerationUnitsPerSecond);
            }

            if (_roomPlayerCounts.IsCreated && TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
            {
                int occupiedRoomIndex = ResolveNearestRoomIndex(in playerAup);
                if (occupiedRoomIndex >= 0 && occupiedRoomIndex < roomCount)
                    _roomPlayerCounts[occupiedRoomIndex] = 1;
            }

            int doorCount = fluidDynamics.ConfiguredBulkheadCount;
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
            return playerInsideModule && supplyRatio < math.saturate(threshold);
        }

        internal static float ResolveBrownoutOxygenConsumptionRate(float currentConsumptionRate, float brownoutDrainRate)
        {
            return math.max(math.max(0f, currentConsumptionRate), math.max(0f, brownoutDrainRate));
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
            return Mathf.Abs(delta.x) <= halfExtents.x &&
                   Mathf.Abs(delta.y) <= halfExtents.y &&
                   Mathf.Abs(delta.z) <= halfExtents.z;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext == null || playerContext.PlayerMovement == null)
                return false;

            _playerTransform = playerContext.PlayerTransform;
            playerAup = playerContext.PlayerMovement.CurrentAup;
            return true;
        }

        private void AccumulateRoomHeatSources()
        {
            if (!_roomHeatWatts.IsCreated || fluidDynamics == null)
                return;

            int roomCount = fluidDynamics.CompartmentCount;
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                if (roomIndex >= roomCount)
                {
                    _roomHeatWatts[roomIndex] = 0f;
                    continue;
                }

                RoomDefinition definition = roomIndex < rooms.Length ? rooms[roomIndex] : default;
                _roomHeatWatts[roomIndex] = math.max(0f, definition.passiveHeatWatts);
            }

            for (int i = 0; i < _fabricatorHeatEmitterCount; i++)
            {
                FabricatorHeatEmitter emitter = _fabricatorHeatEmitters[i];
                if (emitter.Fabricator == null || emitter.RoomIndex < 0 || emitter.RoomIndex >= roomCount)
                    continue;

                if (emitter.Fabricator.IsCrafting)
                    _roomHeatWatts[emitter.RoomIndex] += math.abs(emitter.Fabricator.PowerRating) * math.max(0f, fabricatorHeatWattsScale);
            }

            for (int i = 0; i < _drillHeatEmitterCount; i++)
            {
                DrillHeatEmitter emitter = _drillHeatEmitters[i];
                if (emitter.Drill == null || emitter.RoomIndex < 0 || emitter.RoomIndex >= roomCount)
                    continue;

                _roomHeatWatts[emitter.RoomIndex] += math.abs(emitter.Drill.PowerRating) * math.max(0f, drillHeatWattsScale);
            }

            for (int i = 0; i < _reactorHeatEmitterCount; i++)
            {
                ReactorHeatEmitter emitter = _reactorHeatEmitters[i];
                if (emitter.Reactor == null || emitter.RoomIndex < 0 || emitter.RoomIndex >= roomCount)
                    continue;

                _roomHeatWatts[emitter.RoomIndex] += math.max(0f, emitter.Reactor.PowerRating) * math.max(0f, reactorHeatWattsScale);
            }
        }

        private void EvaluateReactorMeltdowns()
        {
            if (_submarineBody == null || fluidDynamics == null || !_temperatureFront.IsCreated || !_floodVolumes.IsCreated || !_roomVolumes.IsCreated)
                return;

            float thresholdTemperature = math.max(DefaultReactorMeltdownTemperatureCelsius, reactorMeltdownTemperatureCelsius);
            float minimumImpulse = math.max(1f, reactorMeltdownMinimumImpulseNewtonSeconds);
            float maximumImpulse = math.max(minimumImpulse, reactorMeltdownMaximumImpulseNewtonSeconds);
            float upwardBias = math.saturate(reactorMeltdownUpwardBias);
            float impulseDuration = math.max(0.001f, reactorMeltdownImpulseDurationSeconds);
            float impulsePerWattSecond = math.max(0f, reactorMeltdownImpulsePerWattSecond);
            float floodAmplification = math.max(1f, reactorMeltdownFloodAmplification);

            for (int emitterIndex = 0; emitterIndex < _reactorHeatEmitterCount; emitterIndex++)
            {
                ReactorHeatEmitter emitter = _reactorHeatEmitters[emitterIndex];
                if (emitter.Reactor == null || emitter.RoomIndex < 0 || emitter.RoomIndex >= RoomCount)
                    continue;

                if (_reactorMeltdownTriggered[emitterIndex])
                    continue;

                float roomTemperature = _temperatureFront[emitter.RoomIndex];
                if (roomTemperature < thresholdTemperature)
                    continue;

                Vector3 reactorWorldPosition = emitter.Reactor.transform.position;
                Vector3 centerDirection = _submarineBody.worldCenterOfMass - reactorWorldPosition;
                Vector3 forceDirection = SafeNormalize(Vector3.Lerp(centerDirection, Vector3.up, upwardBias), Vector3.up);

                float roomVolume = math.max(minimumGasVolumeCubicMeters, _roomVolumes[emitter.RoomIndex]);
                float floodRatio = math.saturate(_floodVolumes[emitter.RoomIndex] / roomVolume);
                float floodMultiplier = math.lerp(1f, floodAmplification, floodRatio);
                float temperatureOvershoot = math.max(0f, roomTemperature - thresholdTemperature);
                float thermalScale = 1f + math.saturate(temperatureOvershoot / math.max(1f, thresholdTemperature));
                float baseImpulseMagnitude = math.max(
                    minimumImpulse,
                    math.max(0f, emitter.Reactor.PowerRating) * impulsePerWattSecond * impulseDuration);
                float impulseMagnitude = math.clamp(
                    baseImpulseMagnitude * floodMultiplier * thermalScale,
                    minimumImpulse,
                    maximumImpulse);

                PhysicsForceRouter.QueueForceAtPosition(
                    _submarineBody,
                    forceDirection * impulseMagnitude,
                    reactorWorldPosition,
                    ForceMode.Impulse);
                _reactorMeltdownTriggered[emitterIndex] = true;
            }
        }

        private void PublishDoorOpeningPressureEvents()
        {
            if (!_topologySeeded || !_pressureFront.IsCreated || !_doorSealedPrevious.IsCreated || fluidDynamics == null)
                return;

            int roomCount = fluidDynamics.CompartmentCount;
            int doorCount = math.min(fluidDynamics.ConfiguredBulkheadCount, DoorCapacity);
            float thresholdKPa = math.max(0f, highPressureEventThresholdKPa);
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

                float pressureA = _pressureFront[pair.x];
                float pressureB = _pressureFront[pair.y];
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
                return _cachedTransform != null ? _cachedTransform.position : Vector3.zero;

            Vector3 centroidA = fluidDynamics.GetCompartmentCentroid(roomA);
            Vector3 centroidB = fluidDynamics.GetCompartmentCentroid(roomB);
            Vector3 localMidpoint = (centroidA + centroidB) * 0.5f;
            return _cachedTransform != null ? _cachedTransform.TransformPoint(localMidpoint) : localMidpoint;
        }

        private void EmitPressureBlowout(int doorIndex, int roomA, int roomB, float pressureA, float pressureB, Vector3 runtimePosition)
        {
            if (fluidDynamics == null)
                return;

            float highPressureKPa = math.max(pressureA, pressureB);
            float lowPressureKPa = math.min(pressureA, pressureB);
            float pressureDeltaKPa = highPressureKPa - lowPressureKPa;
            if (pressureDeltaKPa <= Epsilon)
                return;

            Vector3 direction = ResolveDoorFlowDirection(roomA, roomB, pressureA, pressureB);
            if (direction.sqrMagnitude <= Epsilon)
                return;

            float doorAreaSquareMeters = math.max(Epsilon, fluidDynamics.GetBulkheadDoorAreaSquareMeters(doorIndex));
            float forceMagnitudeNewtons = pressureDeltaKPa * 1000f * doorAreaSquareMeters;
            float impulseMagnitude = math.min(
                forceMagnitudeNewtons * math.max(0.001f, pressureImpulseDurationSeconds),
                math.max(1f, maximumPressureImpulseNewtonSeconds));

            PressureImpulseEvent pressureImpulseEvent = new PressureImpulseEvent(
                doorIndex,
                runtimePosition,
                direction,
                doorAreaSquareMeters,
                highPressureKPa,
                lowPressureKPa,
                direction * forceMagnitudeNewtons,
                direction * impulseMagnitude,
                math.max(0.25f, pressureImpulseRadiusMeters));
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
            return SafeNormalize(worldDirection, _cachedTransform != null ? _cachedTransform.forward : Vector3.forward);
        }

        private void ApplyPressureBlowoutImpulse(in PressureImpulseEvent pressureImpulseEvent)
        {
            float radius = math.max(0.25f, pressureImpulseEvent.InfluenceRadiusMeters);
            int hitCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                pressureImpulseEvent.RuntimePosition,
                radius,
                _pressureImpulseOverlapBuffer,
                pressureImpulseLayers,
                QueryTriggerInteraction.Ignore);
            if (hitCount <= 0)
                return;

            float radiusSq = math.max(Epsilon, radius * radius);
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

                bool duplicate = false;
                for (int uniqueIndex = 0; uniqueIndex < uniqueBodyCount; uniqueIndex++)
                {
                    if (_pressureImpulseBodyBuffer[uniqueIndex] != body)
                        continue;

                    duplicate = true;
                    break;
                }

                if (duplicate)
                    continue;

                _pressureImpulseBodyBuffer[uniqueBodyCount++] = body;
                if (uniqueBodyCount >= PressureImpulseOverlapCapacity)
                    break;
            }

            float impulseMagnitude = math.min(
                pressureImpulseEvent.PressureDeltaKPa * 1000f * math.max(Epsilon, pressureImpulseEvent.DoorAreaSquareMeters) * math.max(0.001f, pressureImpulseDurationSeconds),
                math.max(1f, maximumPressureImpulseNewtonSeconds));
            float falloffBias = math.saturate(pressureImpulseFalloffExponent - 1f) * 0.1f;
            for (int bodyIndex = 0; bodyIndex < uniqueBodyCount; bodyIndex++)
            {
                Rigidbody body = _pressureImpulseBodyBuffer[bodyIndex];
                _pressureImpulseBodyBuffer[bodyIndex] = null;
                if (body == null)
                    continue;

                Vector3 toDoor = pressureImpulseEvent.RuntimePosition - body.worldCenterOfMass;
                float normalizedDistance = math.saturate(1f - (toDoor.sqrMagnitude / radiusSq));
                if (normalizedDistance <= 0f)
                    continue;

                float falloff = math.saturate(normalizedDistance - falloffBias);
                if (falloff <= Epsilon)
                    continue;

                Vector3 direction = pressureImpulseEvent.Direction;
                Vector3 impulse = direction * (impulseMagnitude * falloff);
                PhysicsForceRouter.QueueForce(body, impulse, ForceMode.Impulse);
            }
        }

        private static Vector3 SafeNormalize(Vector3 value, Vector3 fallback)
        {
            float lengthSq = value.sqrMagnitude;
            if (lengthSq <= 0.000001f)
                return fallback;

            return value * math.rsqrt(lengthSq);
        }

        private void ScheduleAtmosphereJob(float fixedDeltaTime, float thermalConductionDeltaTime)
        {
            if (_atmosphereJobRunning || fluidDynamics == null || !_o2Front.IsCreated)
                return;

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
                RoomCount = fluidDynamics.CompartmentCount,
                DoorCount = fluidDynamics.ConfiguredBulkheadCount,
                DeltaTime = fixedDeltaTime,
                ReferencePressureKPa = math.max(1f, referencePressureKPa),
                MinimumGasVolumeCubicMeters = math.max(0.001f, minimumGasVolumeCubicMeters),
                MaximumPressureKPa = math.max(referencePressureKPa, maximumPressureKPa),
                DoorConductance = math.max(0f, doorConductance),
                MaxTransferUnitsPerSecond = math.max(0f, maxTransferUnitsPerSecond),
                ReferenceTemperatureCelsius = referenceTemperatureCelsius,
                FloodWaterTemperatureCelsius = floodWaterTemperatureCelsius,
                MinimumTemperatureCelsius = math.min(minimumTemperatureCelsius, maximumTemperatureCelsius),
                MaximumTemperatureCelsius = math.max(minimumTemperatureCelsius, maximumTemperatureCelsius),
                AirDensityKilogramsPerCubicMeter = math.max(0.1f, airDensityKilogramsPerCubicMeter),
                AirSpecificHeatJoulesPerKilogramKelvin = math.max(1f, airSpecificHeatJoulesPerKilogramKelvin),
                WaterDensityKilogramsPerCubicMeter = math.max(1f, waterDensityKilogramsPerCubicMeter),
                WaterSpecificHeatJoulesPerKilogramKelvin = math.max(1f, waterSpecificHeatJoulesPerKilogramKelvin),
                MinimumThermalCapacityJoulesPerKelvin = math.max(1f, minimumThermalCapacityJoulesPerKelvin),
                ThermalConductionDeltaTime = math.max(0f, thermalConductionDeltaTime),
                BulkheadThermalConductivityWattsPerKelvin = math.max(0f, bulkheadThermalConductivityWattsPerKelvin),
                SealedBulkheadThermalCoupling = math.saturate(sealedBulkheadThermalCoupling),
                OpenBulkheadThermalCoupling = math.max(0f, openBulkheadThermalCoupling),
                ReferenceTemperatureKelvin = math.max(1f, referenceTemperatureKelvin),
                OxygenTankCapacity = math.max(1f, oxygenTankCapacity),
                HeatWattsToCelsiusPerSecond = math.max(0f, heatWattsToCelsiusPerSecond)
            };

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

            SwapBuffers(ref _o2Front, ref _o2Back);
            SwapBuffers(ref _co2Front, ref _co2Back);
            SwapBuffers(ref _inertFront, ref _inertBack);
            SwapBuffers(ref _pressureFront, ref _pressureBack);
            SwapBuffers(ref _gasVolumeFront, ref _gasVolumeBack);
            SwapBuffers(ref _temperatureFront, ref _temperatureBack);
            SwapBuffers(ref _steamFront, ref _steamBack);
            PublishAtmosphereFakes(fixedDeltaTime);
        }

        private void RefreshDebugState()
        {
            int roomCount = fluidDynamics != null ? fluidDynamics.CompartmentCount : 0;
            int doorCount = fluidDynamics != null ? fluidDynamics.ConfiguredBulkheadCount : 0;
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
            for (int roomIndex = 0; roomIndex < roomCount; roomIndex++)
            {
                float pressure = _pressureFront[roomIndex];
                pressureSum += pressure;
                maxPressure = math.max(maxPressure, pressure);
                float temperature = _temperatureFront.IsCreated ? _temperatureFront[roomIndex] : referenceTemperatureCelsius;
                temperatureSum += temperature;
                maxTemperature = math.max(maxTemperature, temperature);
                float steamVolume = _steamFront.IsCreated ? _steamFront[roomIndex] : 0f;
                steamSum += steamVolume;
                maxSteam = math.max(maxSteam, steamVolume);

                float tankCapacity = math.max(1f, oxygenTankCapacity);
                oxygenFractionSum += math.saturate(_o2Front[roomIndex] / tankCapacity);
                carbonDioxideFractionSum += math.saturate(_co2Front[roomIndex] / tankCapacity);
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
            DisposeDeferred(ref _doorPairs, dependency);
            DisposeDeferred(ref _doorSealed, dependency);
            DisposeDeferred(ref _doorSealedPrevious, dependency);
            _topologySeeded = false;
            _thermalEmittersSeeded = false;
        }

        private void DisposeDeferred<T>(ref NativeArray<T> array, JobHandle dependency) where T : struct
        {
            if (!array.IsCreated)
                return;

            _disposeHandle = JobHandle.CombineDependencies(_disposeHandle, array.Dispose(dependency));
            array = default;
        }

        private void CompleteAtmosphereJobForAuthoritativeWrite()
        {
            if (!_atmosphereJobRunning)
                return;

            DispatcherJobSwap.TryComplete(ref _atmosphereJobHandle, true);
            _atmosphereJobRunning = false;
            SwapBuffers(ref _o2Front, ref _o2Back);
            SwapBuffers(ref _co2Front, ref _co2Back);
            SwapBuffers(ref _inertFront, ref _inertBack);
            SwapBuffers(ref _pressureFront, ref _pressureBack);
            SwapBuffers(ref _gasVolumeFront, ref _gasVolumeBack);
            SwapBuffers(ref _temperatureFront, ref _temperatureBack);
            SwapBuffers(ref _steamFront, ref _steamBack);
        }

        private float ResolveInstantPressure(float totalGasUnits, float gasVolumeCubicMeters)
        {
            return ResolveInstantPressureWithTemperature(totalGasUnits, gasVolumeCubicMeters, referenceTemperatureCelsius);
        }

        private float ResolveInstantFakePressure(int roomIndex, float temperatureCelsius)
        {
            if (roomIndex < 0 || roomIndex >= RoomCount)
                return referencePressureKPa;

            float roomVolume = _roomVolumes.IsCreated
                ? math.max(_roomVolumes[roomIndex], minimumGasVolumeCubicMeters)
                : minimumGasVolumeCubicMeters;
            float floodVolume = _floodVolumes.IsCreated
                ? math.clamp(_floodVolumes[roomIndex], 0f, roomVolume)
                : 0f;
            float steamVolume = _steamFront.IsCreated
                ? math.max(0f, _steamFront[roomIndex])
                : 0f;
            return ResolveSimplePressureKPa(roomVolume, floodVolume, steamVolume, temperatureCelsius);
        }

        private float ResolveSimplePressureKPa(float roomVolume, float floodVolume, float steamVolume, float temperatureCelsius)
        {
            float safeRoomVolume = math.max(minimumGasVolumeCubicMeters, roomVolume);
            float flood01 = math.saturate(floodVolume / safeRoomVolume);
            float steam01 = math.saturate(steamVolume / safeRoomVolume);
            float heat01 = math.saturate((temperatureCelsius - referenceTemperatureCelsius) / math.max(1f, maximumTemperatureCelsius - referenceTemperatureCelsius));
            float pressure01 = math.saturate((flood01 * 0.65f) + (steam01 * 0.35f) + (heat01 * 0.2f));
            return math.clamp(
                math.lerp(referencePressureKPa, maximumPressureKPa, pressure01),
                0f,
                math.max(referencePressureKPa, maximumPressureKPa));
        }

        private void UpdateBoilingFloodHazards(float fixedDeltaTime)
        {
            if (fluidDynamics == null || !_temperatureFront.IsCreated || !_floodVolumes.IsCreated || !_roomVolumes.IsCreated)
            {
                ClearBoilingFloodHazards();
                return;
            }

            int roomCount = fluidDynamics.CompartmentCount;
            float thresholdTemperature = boilingFloodTemperatureCelsius;
            float minimumFillRatio = math.saturate(boilingFloodMinimumFillRatio);
            float hazardBaseIntensity = math.max(0f, boilingHazardIntensity);
            float faunaDamagePerStep = math.max(0f, boilingFaunaDamagePerSecond) * math.max(0f, fixedDeltaTime);
            float maxTemperature = math.max(thresholdTemperature + 1f, maximumTemperatureCelsius);

            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                int hazardId = _boilingHazardIds[roomIndex];
                if (roomIndex >= roomCount)
                {
                    HectonHazardManager.Unregister(hazardId);
                    continue;
                }

                float roomVolume = math.max(minimumGasVolumeCubicMeters, _roomVolumes[roomIndex]);
                float floodVolume = math.clamp(_floodVolumes[roomIndex], 0f, roomVolume);
                float fillRatio = math.saturate(floodVolume / roomVolume);
                float temperature = _temperatureFront[roomIndex];
                if (temperature < thresholdTemperature || fillRatio < minimumFillRatio)
                {
                    HectonHazardManager.Unregister(hazardId);
                    continue;
                }

                if (!TryResolveBoilingHazardBounds(roomIndex, roomVolume, out Vector3 worldCenter, out float radius))
                {
                    HectonHazardManager.Unregister(hazardId);
                    continue;
                }

                float temperature01 = math.saturate((temperature - thresholdTemperature) / math.max(1f, maxTemperature - thresholdTemperature));
                float fill01 = math.saturate((fillRatio - minimumFillRatio) / math.max(0.01f, 1f - minimumFillRatio));
                float intensity = hazardBaseIntensity * math.max(0.1f, math.max(temperature01, fill01));

                HectonHazardManager.Register(hazardId, worldCenter, intensity, radius, HazardType.Heat);
                ApplyBoilingFaunaDamage(worldCenter, radius, intensity * faunaDamagePerStep);
            }
        }

        private void ClearBoilingFloodHazards()
        {
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
                HectonHazardManager.Unregister(_boilingHazardIds[roomIndex]);
        }

        private float ResolveInstantThermalCapacity(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCount || !_roomVolumes.IsCreated || !_floodVolumes.IsCreated || !_gasVolumeFront.IsCreated)
                return math.max(Epsilon, minimumThermalCapacityJoulesPerKelvin);

            float gasVolume = math.max(minimumGasVolumeCubicMeters, _gasVolumeFront[roomIndex]);
            float floodVolume = math.max(0f, _floodVolumes[roomIndex]);
            float airMass = gasVolume * math.max(Epsilon, airDensityKilogramsPerCubicMeter);
            float waterMass = floodVolume * math.max(Epsilon, waterDensityKilogramsPerCubicMeter);
            float airCapacity = airMass * math.max(Epsilon, airSpecificHeatJoulesPerKilogramKelvin);
            float waterCapacity = waterMass * math.max(Epsilon, waterSpecificHeatJoulesPerKilogramKelvin);
            return math.max(minimumThermalCapacityJoulesPerKelvin, airCapacity + waterCapacity);
        }

        private static float ResolveFakeHazardRadius(float roomVolume, float paddingMeters)
        {
            float safePadding = math.max(0f, paddingMeters);
            float volumeRadius = FakeHazardRadiusBaseMeters + (math.max(0f, roomVolume) * FakeHazardRadiusVolumeScale);
            return math.clamp(volumeRadius + safePadding, 0.5f, FakeHazardRadiusMaxMeters + safePadding);
        }

        private bool TryResolveBoilingHazardBounds(int roomIndex, float roomVolume, out Vector3 worldCenter, out float radius)
        {
            worldCenter = Vector3.zero;
            radius = 0f;
            if (fluidDynamics == null || _cachedTransform == null)
                return false;

            Vector3 localCentroid = fluidDynamics.GetCompartmentCentroid(roomIndex);
            worldCenter = _cachedTransform.TransformPoint(localCentroid);

            radius = ResolveFakeHazardRadius(roomVolume, boilingHazardRadiusPaddingMeters);
            return radius > 0f;
        }

        private bool TryResolveRoomHazardBounds(int roomIndex, out Vector3 worldCenter, out float radius)
        {
            worldCenter = Vector3.zero;
            radius = 0f;
            if (fluidDynamics == null || _cachedTransform == null || roomIndex < 0 || roomIndex >= RoomCount)
                return false;

            float roomVolume = _roomVolumes.IsCreated
                ? math.max(_roomVolumes[roomIndex], minimumGasVolumeCubicMeters)
                : math.max(fluidDynamics.GetCompartmentMaxFloodVolumeCubicMeters(roomIndex), minimumGasVolumeCubicMeters);
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
            int roomCount = fluidDynamics.CompartmentCount;
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
