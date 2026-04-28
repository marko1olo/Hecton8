using System.Runtime.InteropServices;
using Hecton8.AI;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Crafting;
using Hecton8.Gameplay;
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
    /// Static high-pressure warning bus for submarine bulkhead events.
    /// </summary>
    public static class HighPressureEvents
    {
        /// <summary>Delegate used by high-pressure subscribers.</summary>
        public delegate void HighPressureEventHandler(in HighPressureEvent pressureEvent);

        /// <summary>Fired when a sealed bulkhead opens into unequal room pressures.</summary>
        public static event HighPressureEventHandler OnHighPressure;

        /// <summary>Emits a high-pressure warning payload.</summary>
        public static void Notify(in HighPressureEvent pressureEvent)
        {
            OnHighPressure?.Invoke(pressureEvent);
        }
    }

    /// <summary>
    /// Fixed-step pressurized interior simulation for submarines.
    /// Tracks O2 / CO2 / inert-gas redistribution across the compartment graph and couples pressure to flood displacement.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SubmarineFluidDynamics))]
    [AddComponentMenu("Hecton/Atmosphere/Submarine Atmosphere System")]
    public sealed class SubmarineAtmosphereSystem : MonoBehaviour, IFixedTickable
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
        private const int PressureImpulseOverlapCapacity = 32;
        private const int HeatEmitterCapacity = 24;
        private const float DefaultReferenceTemperatureCelsius = 20f;
        private const float DefaultFloodWaterTemperatureCelsius = 4f;
        private const float DefaultMinimumTemperatureCelsius = -5f;
        private const float DefaultMaximumTemperatureCelsius = 90f;
        private const float DefaultAirDensityKilogramsPerCubicMeter = 1.225f;
        private const float DefaultAirSpecificHeatJoulesPerKilogramKelvin = 1005f;
        private const float DefaultWaterDensityKilogramsPerCubicMeter = 1027f;
        private const float DefaultWaterSpecificHeatJoulesPerKilogramKelvin = 3990f;
        private const float DefaultMinimumThermalCapacityJoulesPerKelvin = 400f;
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
        private const int BoilingFaunaContactCapacity = 16;
        private const float Epsilon = 0.0001f;

        [System.Serializable]
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
        }

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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
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
            [ReadOnly] public NativeArray<float> TemperatureFront;
            [ReadOnly] public NativeArray<float> RoomHeatWatts;
            [ReadOnly] public NativeArray<int2> DoorPairs;
            [ReadOnly] public NativeArray<byte> DoorSealed;

            public NativeArray<float> O2Back;
            public NativeArray<float> CO2Back;
            public NativeArray<float> InertBack;
            public NativeArray<float> PressureBack;
            public NativeArray<float> GasVolumeBack;
            public NativeArray<float> TemperatureBack;

            public int RoomCount;
            public int DoorCount;
            public float DeltaTime;
            public float ReferencePressureKPa;
            public float DoorConductance;
            public float MaxTransferUnitsPerSecond;
            public float MinimumGasVolumeCubicMeters;
            public float MaximumPressureKPa;
            public float ReferenceTemperatureCelsius;
            public float FloodWaterTemperatureCelsius;
            public float MinimumTemperatureCelsius;
            public float MaximumTemperatureCelsius;
            public float AirDensityKilogramsPerCubicMeter;
            public float AirSpecificHeatJoulesPerKilogramKelvin;
            public float WaterDensityKilogramsPerCubicMeter;
            public float WaterSpecificHeatJoulesPerKilogramKelvin;
            public float MinimumThermalCapacityJoulesPerKelvin;

            public void Execute()
            {
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
                        continue;
                    }

                    float roomVolume = math.max(RoomVolumes[roomIndex], MinimumGasVolumeCubicMeters);
                    float floodVolume = math.clamp(FloodVolumes[roomIndex], 0f, roomVolume - Epsilon);
                    float gasVolume = math.max(MinimumGasVolumeCubicMeters, roomVolume - floodVolume);
                    float previousPressure = math.max(0f, PressureFront[roomIndex]);
                    float previousGasVolume = math.max(MinimumGasVolumeCubicMeters, GasVolumeFront[roomIndex]);
                    float previousTotalGas = math.max(0f, O2Front[roomIndex] + CO2Front[roomIndex] + InertFront[roomIndex]);

                    float oxygen = math.max(0f, O2Front[roomIndex] - (O2ConsumptionRates[roomIndex] * DeltaTime));
                    float carbonDioxide = math.max(0f, CO2Front[roomIndex] + (CO2GenerationRates[roomIndex] * DeltaTime));
                    float inert = math.max(0f, InertFront[roomIndex]);
                    float currentTotalGas = oxygen + carbonDioxide + inert;

                    O2Back[roomIndex] = oxygen;
                    CO2Back[roomIndex] = carbonDioxide;
                    InertBack[roomIndex] = inert;
                    GasVolumeBack[roomIndex] = gasVolume;
                    PressureBack[roomIndex] = ResolveFloodCompressedPressure(
                        previousPressure,
                        previousGasVolume,
                        gasVolume,
                        previousTotalGas,
                        currentTotalGas);

                    float previousTemperature = math.clamp(
                        TemperatureFront[roomIndex],
                        MinimumTemperatureCelsius,
                        MaximumTemperatureCelsius);
                    float airMassKilograms = math.max(0f, gasVolume * math.max(0.1f, AirDensityKilogramsPerCubicMeter));
                    float waterMassKilograms = math.max(0f, floodVolume * math.max(1f, WaterDensityKilogramsPerCubicMeter));
                    float airCapacity = airMassKilograms * math.max(1f, AirSpecificHeatJoulesPerKilogramKelvin);
                    float waterCapacity = waterMassKilograms * math.max(1f, WaterSpecificHeatJoulesPerKilogramKelvin);
                    float totalCapacity = math.max(MinimumThermalCapacityJoulesPerKelvin, airCapacity + waterCapacity);
                    float mixedTemperature = previousTemperature;
                    if (waterCapacity > Epsilon)
                    {
                        float totalMixedCapacity = math.max(Epsilon, airCapacity + waterCapacity);
                        mixedTemperature = ((previousTemperature * airCapacity) + (FloodWaterTemperatureCelsius * waterCapacity)) / totalMixedCapacity;
                    }

                    float temperatureDelta = (RoomHeatWatts[roomIndex] * DeltaTime) / totalCapacity;
                    TemperatureBack[roomIndex] = math.clamp(
                        mixedTemperature + temperatureDelta,
                        MinimumTemperatureCelsius,
                        MaximumTemperatureCelsius);
                }

                float maxTransferUnits = math.max(0f, MaxTransferUnitsPerSecond) * DeltaTime;
                for (int doorIndex = 0; doorIndex < DoorCapacity; doorIndex++)
                {
                    if (doorIndex >= DoorCount || DoorSealed[doorIndex] != 0)
                        continue;

                    int2 pair = DoorPairs[doorIndex];
                    int roomA = pair.x;
                    int roomB = pair.y;
                    if (roomA < 0 || roomA >= RoomCount || roomB < 0 || roomB >= RoomCount)
                        continue;

                    float pressureA = PressureBack[roomA];
                    float pressureB = PressureBack[roomB];
                    float pressureDelta = pressureA - pressureB;
                    if (math.abs(pressureDelta) <= Epsilon)
                        continue;

                    int sourceIndex = pressureDelta > 0f ? roomA : roomB;
                    int targetIndex = pressureDelta > 0f ? roomB : roomA;

                    float sourceOxygen = O2Back[sourceIndex];
                    float sourceCarbonDioxide = CO2Back[sourceIndex];
                    float sourceInert = InertBack[sourceIndex];
                    float sourceTotal = sourceOxygen + sourceCarbonDioxide + sourceInert;
                    if (sourceTotal <= Epsilon)
                        continue;

                    float targetGasVolume = GasVolumeBack[targetIndex];
                    float targetTotal = O2Back[targetIndex] + CO2Back[targetIndex] + InertBack[targetIndex];
                    float targetPressureCapacity = math.max(0f, ((MaximumPressureKPa / math.max(ReferencePressureKPa, Epsilon)) * targetGasVolume) - targetTotal);
                    if (targetPressureCapacity <= Epsilon)
                        continue;

                    float transferUnits = math.abs(pressureDelta) * math.max(0f, DoorConductance) * DeltaTime;
                    transferUnits = math.min(transferUnits, maxTransferUnits);
                    transferUnits = math.min(transferUnits, sourceTotal);
                    transferUnits = math.min(transferUnits, targetPressureCapacity);
                    if (transferUnits <= Epsilon)
                        continue;

                    float oxygenShare = sourceOxygen / sourceTotal;
                    float carbonDioxideShare = sourceCarbonDioxide / sourceTotal;
                    float inertShare = sourceInert / sourceTotal;

                    float oxygenDelta = transferUnits * oxygenShare;
                    float carbonDioxideDelta = transferUnits * carbonDioxideShare;
                    float inertDelta = transferUnits * inertShare;

                    O2Back[sourceIndex] = math.max(0f, sourceOxygen - oxygenDelta);
                    CO2Back[sourceIndex] = math.max(0f, sourceCarbonDioxide - carbonDioxideDelta);
                    InertBack[sourceIndex] = math.max(0f, sourceInert - inertDelta);

                    O2Back[targetIndex] += oxygenDelta;
                    CO2Back[targetIndex] += carbonDioxideDelta;
                    InertBack[targetIndex] += inertDelta;

                    PressureBack[sourceIndex] = ResolvePressure(
                        O2Back[sourceIndex] + CO2Back[sourceIndex] + InertBack[sourceIndex],
                        GasVolumeBack[sourceIndex]);
                    PressureBack[targetIndex] = ResolvePressure(
                        O2Back[targetIndex] + CO2Back[targetIndex] + InertBack[targetIndex],
                        GasVolumeBack[targetIndex]);
                }
            }

            private float ResolveFloodCompressedPressure(
                float previousPressure,
                float previousGasVolume,
                float currentGasVolume,
                float previousTotalGasUnits,
                float currentTotalGasUnits)
            {
                float safePreviousPressure = math.max(ReferencePressureKPa, previousPressure);
                float safePreviousVolume = math.max(MinimumGasVolumeCubicMeters, previousGasVolume);
                float safeCurrentVolume = math.max(MinimumGasVolumeCubicMeters, currentGasVolume);

                // Boyle's Law compression step: P_new = P_old * (V_old / V_new)
                float compressedPressure = safePreviousPressure * (safePreviousVolume / safeCurrentVolume);
                if (!math.isfinite(compressedPressure))
                    return ResolvePressure(currentTotalGasUnits, currentGasVolume);

                if (previousTotalGasUnits > Epsilon)
                    compressedPressure *= currentTotalGasUnits / previousTotalGasUnits;

                return math.clamp(compressedPressure, 0f, MaximumPressureKPa);
            }

            private float ResolvePressure(float totalGasUnits, float gasVolume)
            {
                float safeGasVolume = math.max(MinimumGasVolumeCubicMeters, gasVolume);
                float pressure = ReferencePressureKPa * math.max(totalGasUnits, 0f) / safeGasVolume;
                if (!math.isfinite(pressure))
                    return ReferencePressureKPa;

                return math.clamp(pressure, 0f, MaximumPressureKPa);
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

        [Tooltip("How strongly open doors equalize pressure. Units: reference-gas-volume transfer per second per kPa.")]
        [SerializeField, Min(0f)] private float doorConductance = DefaultDoorConductance;

        [Tooltip("Hard cap on transferred gas units per second across a single door.")]
        [SerializeField, Min(0f)] private float maxTransferUnitsPerSecond = DefaultMaxTransferUnitsPerSecond;

        [Tooltip("Gas volume floor used to prevent divide-by-zero when a room is almost fully flooded.")]
        [SerializeField, Min(0.001f)] private float minimumGasVolumeCubicMeters = DefaultMinimumGasVolumeCubicMeters;

        [Tooltip("Maximum simulated room pressure in kPa.")]
        [SerializeField, Min(10f)] private float maximumPressureKPa = DefaultMaximumPressureKPa;

        [Tooltip("Absolute room pressure threshold required before a high-pressure event is emitted.")]
        [SerializeField, Min(0f)] private float highPressureEventThresholdKPa = DefaultHighPressureEventThresholdKPa;

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

        [Tooltip("Waste-heat multiplier applied to fabricator electrical draw.")]
        [SerializeField, Min(0f)] private float fabricatorHeatWattsScale = DefaultFabricatorHeatWattsScale;

        [Tooltip("Waste-heat multiplier applied to deep-drill electrical draw.")]
        [SerializeField, Min(0f)] private float drillHeatWattsScale = DefaultDrillHeatWattsScale;

        [Tooltip("Waste-heat multiplier applied to reactor electrical output.")]
        [SerializeField, Min(0f)] private float reactorHeatWattsScale = DefaultReactorHeatWattsScale;

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
        [SerializeField] private LayerMask pressureImpulseLayers = ~0;

        [Header("── Diagnostics ──────────────────")]
        [SerializeField] private int _debugRoomCount;
        [SerializeField] private int _debugDoorCount;
        [SerializeField] private float _debugAveragePressureKPa;
        [SerializeField] private float _debugMaxPressureKPa;
        [SerializeField] private float _debugAverageOxygenFraction;
        [SerializeField] private float _debugAverageCarbonDioxideFraction;
        [SerializeField] private float _debugAverageTemperatureCelsius;
        [SerializeField] private float _debugMaxTemperatureCelsius;

        private Transform _cachedTransform;
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
        private NativeArray<float> _temperatureFront;
        private NativeArray<float> _temperatureBack;
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
        private int _fabricatorHeatEmitterCount;
        private int _drillHeatEmitterCount;
        private int _reactorHeatEmitterCount;

        public int RoomCount => fluidDynamics != null ? fluidDynamics.CompartmentCount : 0;

        public float GetRoomPressureKPa(int roomIndex)
        {
            if (!_pressureFront.IsCreated || roomIndex < 0 || roomIndex >= RoomCount)
                return referencePressureKPa;

            return _pressureFront[roomIndex];
        }

        public float GetRoomOxygenFraction(int roomIndex)
        {
            if (!_o2Front.IsCreated || !_co2Front.IsCreated || !_inertFront.IsCreated || roomIndex < 0 || roomIndex >= RoomCount)
                return DefaultInitialOxygenFraction;

            float totalGas = _o2Front[roomIndex] + _co2Front[roomIndex] + _inertFront[roomIndex];
            return totalGas > Epsilon ? math.saturate(_o2Front[roomIndex] / totalGas) : 0f;
        }

        public float GetRoomCarbonDioxideFraction(int roomIndex)
        {
            if (!_o2Front.IsCreated || !_co2Front.IsCreated || !_inertFront.IsCreated || roomIndex < 0 || roomIndex >= RoomCount)
                return DefaultInitialCarbonDioxideFraction;

            float totalGas = _o2Front[roomIndex] + _co2Front[roomIndex] + _inertFront[roomIndex];
            return totalGas > Epsilon ? math.saturate(_co2Front[roomIndex] / totalGas) : 0f;
        }

        public float GetRoomTemperatureCelsius(int roomIndex)
        {
            if (!_temperatureFront.IsCreated || roomIndex < 0 || roomIndex >= RoomCount)
                return referenceTemperatureCelsius;

            return _temperatureFront[roomIndex];
        }

        public void InjectOxygenUnits(int roomIndex, float oxygenUnits)
        {
            if (oxygenUnits <= 0f || !_o2Front.IsCreated || roomIndex < 0 || roomIndex >= RoomCount)
                return;

            CompleteAtmosphereJobForAuthoritativeWrite();
            _o2Front[roomIndex] += oxygenUnits;
            _pressureFront[roomIndex] = ResolveInstantPressure(
                _o2Front[roomIndex] + _co2Front[roomIndex] + _inertFront[roomIndex],
                _gasVolumeFront[roomIndex]);
        }

        private void Awake()
        {
            CacheReferences();
            SeedBoilingHazardIds();
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
            ConsumeCompletedJob();
            SyncFluidSnapshot();
            SeedTopologyIfNeeded();
            SeedThermalEmittersIfNeeded();
            AccumulateRoomHeatSources();
            EvaluateReactorMeltdowns();
            UpdateBoilingFloodHazards(fixedDeltaTime);
            PublishDoorOpeningPressureEvents();
            ScheduleAtmosphereJob(fixedDeltaTime);
            RefreshDebugState();
        }

        private void CacheReferences()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            if (fluidDynamics == null)
                TryGetComponent(out fluidDynamics);

            if (_submarineBody == null && fluidDynamics != null)
                fluidDynamics.TryGetComponent(out _submarineBody);
        }

        private void SeedBoilingHazardIds()
        {
            int instanceId = GetInstanceID();
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
                _boilingHazardIds[roomIndex] = (instanceId * 97) ^ (0x61A0 + roomIndex);
        }

        private void TryRegister()
        {
            if (_registered)
                return;

            GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Environment);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

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
            _temperatureFront = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _temperatureBack = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _roomHeatWatts = new NativeArray<float>(RoomCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<int2>[7] — door graph edges aligned to submarine bulkheads — owner: SubmarineAtmosphereSystem
            _doorPairs = new NativeArray<int2>(DoorCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<byte>[7] — sealed-door state copied from submarine bulkheads — owner: SubmarineAtmosphereSystem
            _doorSealed = new NativeArray<byte>(DoorCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<byte>[7] â€” previous sealed-door state used for door-opening pressure warnings â€” owner: SubmarineAtmosphereSystem
            _doorSealedPrevious = new NativeArray<byte>(DoorCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
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

                float inertFraction = math.max(0f, 1f - oxygenFraction - carbonDioxideFraction);
                _roomVolumes[roomIndex] = roomVolume;
                _gasVolumeFront[roomIndex] = roomVolume;
                _o2Front[roomIndex] = roomVolume * oxygenFraction;
                _co2Front[roomIndex] = roomVolume * carbonDioxideFraction;
                _inertFront[roomIndex] = roomVolume * inertFraction;
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
                _o2ConsumptionRates[roomIndex] = math.max(0f, definition.oxygenConsumptionUnitsPerSecond);
                _co2GenerationRates[roomIndex] = math.max(0f, definition.carbonDioxideGenerationUnitsPerSecond);
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

            float impulseMagnitude = pressureImpulseEvent.ImpulseVectorNewtonSeconds.magnitude;
            float falloffExponent = math.max(0.25f, pressureImpulseFalloffExponent);
            float forwardBiasMeters = math.max(0.25f, radius * 0.35f);
            for (int bodyIndex = 0; bodyIndex < uniqueBodyCount; bodyIndex++)
            {
                Rigidbody body = _pressureImpulseBodyBuffer[bodyIndex];
                _pressureImpulseBodyBuffer[bodyIndex] = null;
                if (body == null)
                    continue;

                Vector3 toDoor = pressureImpulseEvent.RuntimePosition - body.worldCenterOfMass;
                float distance = toDoor.magnitude;
                float normalizedDistance = math.saturate(1f - (distance / radius));
                if (normalizedDistance <= 0f)
                    continue;

                float falloff = math.pow(normalizedDistance, falloffExponent);
                Vector3 direction = pressureImpulseEvent.Direction;
                float signedSide = Vector3.Dot(body.worldCenterOfMass - pressureImpulseEvent.RuntimePosition, pressureImpulseEvent.Direction);
                if (signedSide > 0f)
                    direction = SafeNormalize(toDoor + pressureImpulseEvent.Direction * forwardBiasMeters, pressureImpulseEvent.Direction);

                Vector3 impulse = direction * (impulseMagnitude * falloff);
                PhysicsForceRouter.QueueForce(body, impulse, ForceMode.Impulse);
            }
        }

        private static Vector3 SafeNormalize(Vector3 value, Vector3 fallback)
        {
            float lengthSq = value.sqrMagnitude;
            if (lengthSq <= 0.000001f)
                return fallback;

            return value / math.sqrt(lengthSq);
        }

        private void ScheduleAtmosphereJob(float fixedDeltaTime)
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
                TemperatureFront = _temperatureFront,
                RoomHeatWatts = _roomHeatWatts,
                DoorPairs = _doorPairs,
                DoorSealed = _doorSealed,
                O2Back = _o2Back,
                CO2Back = _co2Back,
                InertBack = _inertBack,
                PressureBack = _pressureBack,
                GasVolumeBack = _gasVolumeBack,
                TemperatureBack = _temperatureBack,
                RoomCount = fluidDynamics.CompartmentCount,
                DoorCount = fluidDynamics.ConfiguredBulkheadCount,
                DeltaTime = fixedDeltaTime,
                ReferencePressureKPa = math.max(1f, referencePressureKPa),
                DoorConductance = math.max(0f, doorConductance),
                MaxTransferUnitsPerSecond = math.max(0f, maxTransferUnitsPerSecond),
                MinimumGasVolumeCubicMeters = math.max(0.001f, minimumGasVolumeCubicMeters),
                MaximumPressureKPa = math.max(referencePressureKPa, maximumPressureKPa),
                ReferenceTemperatureCelsius = referenceTemperatureCelsius,
                FloodWaterTemperatureCelsius = floodWaterTemperatureCelsius,
                MinimumTemperatureCelsius = math.min(minimumTemperatureCelsius, maximumTemperatureCelsius),
                MaximumTemperatureCelsius = math.max(minimumTemperatureCelsius, maximumTemperatureCelsius),
                AirDensityKilogramsPerCubicMeter = math.max(0.1f, airDensityKilogramsPerCubicMeter),
                AirSpecificHeatJoulesPerKilogramKelvin = math.max(1f, airSpecificHeatJoulesPerKilogramKelvin),
                WaterDensityKilogramsPerCubicMeter = math.max(1f, waterDensityKilogramsPerCubicMeter),
                WaterSpecificHeatJoulesPerKilogramKelvin = math.max(1f, waterSpecificHeatJoulesPerKilogramKelvin),
                MinimumThermalCapacityJoulesPerKelvin = math.max(1f, minimumThermalCapacityJoulesPerKelvin)
            };

            _atmosphereJobHandle = job.Schedule();
            _atmosphereJobRunning = true;
        }

        private void ConsumeCompletedJob()
        {
            if (!_atmosphereJobRunning || !_atmosphereJobHandle.IsCompleted)
                return;

            _atmosphereJobHandle.Complete();
            _atmosphereJobRunning = false;

            SwapBuffers(ref _o2Front, ref _o2Back);
            SwapBuffers(ref _co2Front, ref _co2Back);
            SwapBuffers(ref _inertFront, ref _inertBack);
            SwapBuffers(ref _pressureFront, ref _pressureBack);
            SwapBuffers(ref _gasVolumeFront, ref _gasVolumeBack);
            SwapBuffers(ref _temperatureFront, ref _temperatureBack);
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
                return;
            }

            float pressureSum = 0f;
            float maxPressure = 0f;
            float oxygenFractionSum = 0f;
            float carbonDioxideFractionSum = 0f;
            float temperatureSum = 0f;
            float maxTemperature = minimumTemperatureCelsius;
            for (int roomIndex = 0; roomIndex < roomCount; roomIndex++)
            {
                float pressure = _pressureFront[roomIndex];
                pressureSum += pressure;
                maxPressure = math.max(maxPressure, pressure);
                float temperature = _temperatureFront.IsCreated ? _temperatureFront[roomIndex] : referenceTemperatureCelsius;
                temperatureSum += temperature;
                maxTemperature = math.max(maxTemperature, temperature);

                float totalGas = _o2Front[roomIndex] + _co2Front[roomIndex] + _inertFront[roomIndex];
                if (totalGas > Epsilon)
                {
                    oxygenFractionSum += _o2Front[roomIndex] / totalGas;
                    carbonDioxideFractionSum += _co2Front[roomIndex] / totalGas;
                }
            }

            float inverseRoomCount = 1f / math.max(roomCount, 1);
            _debugAveragePressureKPa = pressureSum * inverseRoomCount;
            _debugMaxPressureKPa = maxPressure;
            _debugAverageOxygenFraction = oxygenFractionSum * inverseRoomCount;
            _debugAverageCarbonDioxideFraction = carbonDioxideFractionSum * inverseRoomCount;
            _debugAverageTemperatureCelsius = temperatureSum * inverseRoomCount;
            _debugMaxTemperatureCelsius = maxTemperature;
        }

        private void DisposeNativeStateDeferred()
        {
            ClearBoilingFloodHazards();
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
            DisposeDeferred(ref _temperatureFront, dependency);
            DisposeDeferred(ref _temperatureBack, dependency);
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

            _atmosphereJobHandle.Complete();
            _atmosphereJobRunning = false;
            SwapBuffers(ref _o2Front, ref _o2Back);
            SwapBuffers(ref _co2Front, ref _co2Back);
            SwapBuffers(ref _inertFront, ref _inertBack);
            SwapBuffers(ref _pressureFront, ref _pressureBack);
            SwapBuffers(ref _gasVolumeFront, ref _gasVolumeBack);
            SwapBuffers(ref _temperatureFront, ref _temperatureBack);
        }

        private float ResolveInstantPressure(float totalGasUnits, float gasVolumeCubicMeters)
        {
            float safeGasVolume = math.max(0.001f, gasVolumeCubicMeters);
            return math.clamp(
                math.max(1f, referencePressureKPa) * math.max(totalGasUnits, 0f) / safeGasVolume,
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

        private bool TryResolveBoilingHazardBounds(int roomIndex, float roomVolume, out Vector3 worldCenter, out float radius)
        {
            worldCenter = Vector3.zero;
            radius = 0f;
            if (fluidDynamics == null || _cachedTransform == null)
                return false;

            Vector3 localCentroid = fluidDynamics.GetCompartmentCentroid(roomIndex);
            worldCenter = _cachedTransform.TransformPoint(localCentroid);

            float compartmentRadius = math.pow(math.max(roomVolume, minimumGasVolumeCubicMeters) / 4.1887903f, 0.33333334f);
            radius = math.max(0.5f, compartmentRadius + math.max(0f, boilingHazardRadiusPaddingMeters));
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

            int roomCount = fluidDynamics.CompartmentCount;
            if (roomCount <= 0)
                return -1;

            Vector3 localPosition = _cachedTransform.InverseTransformPoint(worldPosition);
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
