using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Hecton8.Audio;
using Hecton8.Atmosphere;
using Hecton8.Bootstrap;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using ScalabilityChangedEvent = Hecton8.Core.Contracts.Signals.ScalabilityChangedEvent;
using Hecton8.Core.Contracts;
using Hecton8.Environment.Fluids;
using Hecton8.Gameplay;
using Hecton8.Tools;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using BrineLayerSample = Hecton8.Core.Contracts.BrineLayerSample;

namespace Hecton8.Physics
{
    /// <summary>
    /// Fixed-step flooded-interior model for submarine rigidbodies.
    /// Tracks compartment fill, bulkhead isolation, flood-mass coupling, center-of-mass shifting,
    /// inertia blending, and delayed slosh torque.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Physics/Submarine Fluid Dynamics")]
    public sealed class SubmarineFluidDynamics : MonoBehaviour,
        IFixedTickable,
        IPostFixedTickable,
        IOriginShiftListener,
        IScalabilityChangedEventListener,
        IGlobalRegistryHotSwapListener
    {
        private const int CompartmentCapacity = 8;
        private const int BulkheadCapacity = 7;
        private const int RingBufferLength = 8;
        private const int RingBufferMask = RingBufferLength - 1;
        private const int SloshDelayFrames = 3;
        private const float WaterDensityKgPerCubicMeter = HectonPhysicsContract.WaterDensityKgPerCubicMeterConst;
        private const float MinimumMassForReciprocal = 0.01f;
        private const float GravityMetersPerSecondSquared = HectonPhysicsContract.GravityMetersPerSecondSquaredConst;
        private const float DefaultFixedStepSeconds = HectonPhysicsContract.FixedDeltaTimeSeconds;
        private const float DefaultDischargeCoefficient = 0.62f;
        private const float DefaultBulkheadFlowCoefficient = 0.4f;
        private const float DefaultBulkheadDoorAreaSquareMeters = 1.6f;
        private const float DefaultMaxTransferPerTick = 0.1f;
        private const float DefaultNearZeroHeadDampingMeters = 0.15f;
        private const float DefaultExternalReferencePressureKPa = HectonSurvivalContract.KPaPerAtmosphere;
        private const float DefaultDepressurizationMinimumPressureDeltaKPa = 5f;
        private const float DefaultDepressurizationReferenceMassKilograms = 80f;
        private const float DefaultDepressurizationMaxAccelerationMetersPerSecondSquared = 45f;
        private const float DefaultDepressurizationRoomRadiusPaddingMeters = 1.25f;
        private const float DefaultDepressurizationDistanceFloorMeters = 0.5f;
        private const int DepressurizationContactCapacity = 16;
        private const float DefaultSloshFactor = 0.15f;
        private const float DefaultSloshMinimumVolume = 0.05f;
        private const float DefaultMaxSloshTorque = 50000f;
        private const float DefaultReportedCenterTauSeconds = 1.2f;
        private const float DefaultCenterOfMassTauSeconds = 1.2f;
        private const float DefaultMaxCenterOfMassDeltaPerTickMeters = 0.05f;
        private const float DefaultMaximumIngressPerSecondNormalized = 0.25f;
        private const float DefaultAddedMassLinearDampingScale = 0.3f;
        private const float DefaultAddedMassAngularDampingScale = 0.3f;
        private const float DefaultRigidbodyMassUpdateThresholdKg = 5f;
        private const float CriticalFloodAddedMassLinearBoost = 2f;
        private const float CriticalFloodAddedMassAngularBoost = 8f;
        private const float DefaultHullImplosionDepthThresholdMeters = 4000f;
        private const float DefaultHullPressureRatingKPa = 40000f;
        private const float DefaultHullImplosionBreachAreaNormalized = 0.6f;
        private const float DefaultImplosionDragBonus = 50f;
        private const float DefaultDeepFreezeDepthThresholdMeters = 3000f;
        private const float DefaultDeepFreezeSupplyRatioThreshold = 0.1f;
        private const float IceExpansionVolumeScale = 1.09f;
        private const float DefaultMaximumCompressionNormalized = 0.15f;
        private const int EngineCompartmentIndex = 3;
        private const uint HydrodynamicsResetWarningHash = 0x48445253u;
        private const uint SubmarineFluidDynamicsContextHash = 0x53464459u;
        private const int HydroBlackBoxCapacity = 300;
        private const uint HydroBlackBoxMagic = 0x4844524Fu;
        private const uint HydroBlackBoxFlagHullImplosion = 1u << 0;
        private const uint HydroBlackBoxFlagBallastBlow = 1u << 1;
        private const uint HydroBlackBoxFlagTowingTension = 1u << 2;
        private const uint HydroBlackBoxFlagCavitation = 1u << 3;
        private const uint HydroBlackBoxFlagInvalidOutput = 1u << 4;
        private const uint HydroBlackBoxFlagInvalidVelocity = 1u << 5;
        private const uint HydroBlackBoxFlagInvalidBuoyancy = 1u << 6;
        private const uint HydroBlackBoxFlagEmergencyReset = 1u << 7;
        private const uint HydroBlackBoxFlagBrineSubmerged = 1u << 8;
        private const string HydroBlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_SUBMARINE_BALLAST_PID_V2.bin";
        private const float DefaultHydraulicLeakRateCubicMetersPerSecond = 0.006f;
        private const float DefaultMaximumHydraulicViscosity = 1f;
        private const float DefaultViscositySloshDampingScale = 0.85f;
        private const float DefaultSludgePlayerDragMultiplier = 3.2f;
        private const float DefaultFloraDragAddedMassAtFullDensityKilograms = 4000f;
        private const float DefaultFloraCenterOfMassDownshiftMeters = 0.35f;
        private const float DefaultExteriorBuoyancyForceClampScale = 1.15f;
        private const float DefaultExteriorBuoyancyTorqueClampScale = 1.25f;
        private const float DefaultMaxCargoMassKilograms = 12000f;
        private const float DefaultDamageControlLeakMassKilogramsPerKpaSecond = 0.025f;
        private const float DefaultCargoDraftMetersPer1000Kg = 0.18f;
        private const float DefaultMaxCargoDraftOffsetMeters = 1.2f;
        private const float DefaultCrushDepthBuoyancyScale = 0.85f;
        private const float DefaultForwardHydroDragCoefficient = 0.04f;
        private const float DefaultLateralHydroDragMultiplier = 5f;
        private const float DefaultVerticalHydroDragCoefficient = 0.08f;
        private const float DefaultAngularHydroDragCoefficient = 14f;
        private const float DefaultRightingTorqueCoefficient = 18f;
        private const float DefaultHydroSolverMaxAcceleration = 45f;
        private const float DefaultHydroSolverMaxTorque = 85000f;
        private const float DefaultCompressedAirUnits = 6f;
        private const float DefaultBallastBlowAirCost = 1f;
        private const float DefaultBallastBlowDurationSeconds = 1.75f;
        private const float DefaultBallastBlowUpAcceleration = 2.8f;
        private const float DefaultTowingTensionHoldSeconds = 0.12f;
        private const float DefaultSurfacingBreachSpeedMetersPerSecond = 15f;
        private const float DefaultCavitationThrottleThreshold = 0.98f;
        private const float DefaultCavitationStallSpeedMetersPerSecond = 2f;
        private const float DefaultCavitationCooldownSeconds = 0.35f;
        private const int ExteriorBuoyancySampleCount = 8;
        private const int ExteriorThermalAnomalyCapacity = 8;
        private const int ExteriorThermalContactCapacity = 16;
        private const int VaultCompartmentFloodVolumesFlag = 1 << 0;
        private const int VaultCompartmentViscosityFlag = 1 << 1;
        private const int VaultCompartmentBaseMaxVolumesFlag = 1 << 2;
        private const int VaultCompartmentMaxVolumesFlag = 1 << 3;
        private const int VaultCompartmentBreachAreasFlag = 1 << 4;
        private const int VaultCompartmentLocalCentroidsFlag = 1 << 5;
        private const int VaultCompartmentFlagsFlag = 1 << 6;
        private const int VaultBulkheadPairsFlag = 1 << 7;
        private const int VaultBulkheadSealedFlag = 1 << 8;
        private const int VaultBulkheadDoorAreasFlag = 1 << 9;
        private const int VaultComAccumulatorFrontFlag = 1 << 10;
        private const int VaultComAccumulatorBackFlag = 1 << 11;
        private const int VaultMassPropertiesFrontFlag = 1 << 12;
        private const int VaultMassPropertiesBackFlag = 1 << 13;
        private const int VaultAngularVelocityHistoryFlag = 1 << 14;
        private const int VaultExteriorSubmersionHistoryFlag = 1 << 15;
        private const int VaultJobFloodVolumesFlag = 1 << 16;
        private const int VaultJobCompartmentFlagsFlag = 1 << 17;
        private const int VaultBulkheadTransferDeltasFlag = 1 << 18;
        private const int VaultHydroInputFlag = 1 << 19;
        private const int VaultHydroOutputFlag = 1 << 20;
        private const int VaultHydroBlackBoxFlag = 1 << 21;
        private const int VaultCompartmentStatesFlag = 1 << 22;
        private const int VaultExteriorThermalCentersFlag = 1 << 23;
        private const int VaultExteriorThermalTemperaturesFlag = 1 << 24;
        private const int VaultExteriorThermalLifetimesFlag = 1 << 25;
        private const int VaultExteriorThermalHazardIdsFlag = 1 << 26;
        private const int VaultExteriorBuoyancySamplesFlag = 1 << 27;
        private const float ExteriorThermalCellSizeMeters = 8f;
        private const float ExteriorWaterSpecificHeatCapacityJoulesPerKilogramCelsius = 3990f;
        private const float ExteriorWaterReferenceTemperatureCelsius = 6f;
        private const float ExteriorThermalDecayPerSecond = 8f;
        private const float ExteriorThermalLifetimeSeconds = 1.25f;
        private const float ExteriorBoilingDepthSlopeCelsiusPerMeter = 1.2f;
        private const float ExteriorBoilingImpulseRadiusMeters = 4f;
        private const float ExteriorBoilingAccelerationMetersPerSecondSquared = 18f;
        private static readonly int _ExteriorBoilingUpdraftLayerMask = HectonLayerMasks.MountedSweepLayerMask;
        private const float MinimumAnalyticalDragModifier = 0.1f;
        private const float SplashSubmersionThreshold = 0.5f;
        private const float CriticalFillThreshold = 0.8f;
        private const float Epsilon = 0.0001f;
        private const uint FlagBreached = 1u << 0;
        private const uint FlagSealed = 1u << 1;
        private const uint FlagCritical = 1u << 2;
        private const uint FlagPurging = 1u << 3;
        private const uint FlagFrozen = 1u << 4;
        private const uint FlagTransferSource = 1u << 5;
        private const uint FlagTransferDestination = 1u << 6;
        private const uint FlagOverflow = 1u << 7;
        private const uint FlagRuptured = 1u << 8;
        private const uint FlagIceExpanded = 1u << 9;
        private const uint PersistentFlagsMask = FlagBreached | FlagPurging | FlagFrozen | FlagRuptured | FlagIceExpanded;
        private const string NativeMemoryOwner = nameof(SubmarineFluidDynamics);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;

        // Inspector-authored DTO. Unity serialization populates these fields outside constructor flow.
#pragma warning disable CS0649
        [System.Serializable]
        [StructLayout(LayoutKind.Sequential)]
        private struct CompartmentDefinition
        {
            [Tooltip("Flood capacity for this compartment in cubic meters.")]
            [Min(0f)]
            public float maxFloodVolumeCubicMeters;

            [Tooltip("Initial normalized fill state. 0 = dry, 1 = fully flooded.")]
            [Range(0f, 1f)]
            public float initialFillNormalized;

            [Tooltip("Active breach opening in square meters. Zero disables ingress for this compartment.")]
            [Min(0f)]
            public float breachAreaSquareMeters;

            [Tooltip("Compartment centroid in rigidbody-local space.")]
            public Vector3 localCentroid;
        }

        [System.Serializable]
        [StructLayout(LayoutKind.Sequential)]
        private struct BulkheadDefinition
        {
            [Tooltip("Source compartment index for this transfer gate.")]
            [Range(0, CompartmentCapacity - 1)]
            public int compartmentA;

            [Tooltip("Destination compartment index for this transfer gate.")]
            [Range(0, CompartmentCapacity - 1)]
            public int compartmentB;

            [Tooltip("When true, fluid transfer across this bulkhead pair halts immediately.")]
            public bool isSealed;

            [Tooltip("Cross-sectional doorway area used for pressure blowout force in square meters.")]
            [Min(0f)]
            public float doorAreaSquareMeters;
        }

        // 64-byte stride keeps the gas mix and flood scalar on a 32-byte multiple without dropping partial pressures.
        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct CompartmentState
        {
            [FieldOffset(0)] public float currentVolume;
            [FieldOffset(4)] public float maxVolume;
            [FieldOffset(8)] public float3 localCentroid;
            [FieldOffset(20)] public uint stateFlags;
            [FieldOffset(24)] public float o2PartialPressureKPa;
            [FieldOffset(28)] public float co2PartialPressureKPa;
            [FieldOffset(32)] public float n2PartialPressureKPa;
        }
#pragma warning restore CS0649

        [Header("â”€â”€ Compartments â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Authored compartment capacities, breach openings, and local centroids. Maximum supported count is eight.")]
        // Inspector-authored DTO only. Runtime flood authority is mirrored into GlobalDataVault during enable.
        [SerializeField] private CompartmentDefinition[] compartments = new CompartmentDefinition[CompartmentCapacity];

        [Tooltip("Adjacency map for water transfer. If empty, a linear bow-to-stern chain is generated.")]
        // Inspector-authored DTO only. Runtime bulkhead state is mirrored into GlobalDataVault during enable.
        [SerializeField] private BulkheadDefinition[] bulkheads = new BulkheadDefinition[0];

        [Header("â”€â”€ Inertia Blend â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Authored inertia tensor for the dry hull state.")]
        [SerializeField] private Vector3 dryInertiaTensor = new Vector3(1200f, 1800f, 2300f);

        [Tooltip("Authored inertia tensor for the fully flooded state.")]
        [SerializeField] private Vector3 fullyFloodedInertiaTensor = new Vector3(1650f, 2550f, 3200f);

        [Tooltip("Dry-hull local center used as the baseline for flood-driven center-of-mass shifting.")]
        [SerializeField] private Vector3 dryCenterOfMassLocal = Vector3.zero;

        [Tooltip("Time constant used to smooth the reported flood centroid for downstream telemetry and VFX.")]
        [SerializeField, Min(0.1f)] private float reportedCenterTauSeconds = DefaultReportedCenterTauSeconds;

        [Tooltip("Time constant used to blend the live rigidbody center of mass toward the flooded centroid.")]
        [SerializeField, Min(0.1f)] private float centerOfMassTauSeconds = DefaultCenterOfMassTauSeconds;

        [Tooltip("Maximum local-space center-of-mass movement applied per fixed step.")]
        [SerializeField, Min(0.001f)] private float maxCenterOfMassDeltaPerTickMeters = DefaultMaxCenterOfMassDeltaPerTickMeters;

        [Tooltip("Minimum flood-mass delta required before Rigidbody.mass is updated.")]
        [SerializeField, Min(0.1f)] private float rigidbodyMassUpdateThresholdKg = DefaultRigidbodyMassUpdateThresholdKg;

        [Header("â”€â”€ Flood Math â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Sharp-edge discharge coefficient used in Torricelli ingress.")]
        [SerializeField, Range(0.05f, 1f)] private float dischargeCoefficient = DefaultDischargeCoefficient;

        [Tooltip("Bulkhead transfer coefficient in cubic meters per second per unit fill differential.")]
        [SerializeField, Min(0f)] private float bulkheadFlowCoefficient = DefaultBulkheadFlowCoefficient;

        [Tooltip("Maximum cross-bulkhead transfer per fixed step, in cubic meters.")]
        [SerializeField, Min(0.01f)] private float maxTransferPerTick = DefaultMaxTransferPerTick;

        [Tooltip("Head-delta band in meters where Torricelli bulkhead equalization is damped to prevent endless slosh near equilibrium.")]
        [SerializeField, Min(0.001f)] private float nearZeroHeadDampingMeters = DefaultNearZeroHeadDampingMeters;

        [Tooltip("Safety limiter for ingress. 0.25 means at most 25% of a compartment volume can enter per second.")]
        [SerializeField, Range(0.01f, 1f)] private float maximumIngressPerSecondNormalized = DefaultMaximumIngressPerSecondNormalized;

        [Header("── Phase Change ──────────────────")]
        [Tooltip("Below this global supply ratio, deep flooded compartments start freezing instead of merely cooling.")]
        [SerializeField, Range(0f, 1f)] private float deepFreezeSupplyRatioThreshold = DefaultDeepFreezeSupplyRatioThreshold;

        [Tooltip("Below this depth, power-loss freezing logic stays inactive.")]
        [SerializeField, Min(0f)] private float deepFreezeDepthThresholdMeters = DefaultDeepFreezeDepthThresholdMeters;

        [Tooltip("Maximum fraction of authored compartment capacity lost to abyssal crush compression.")]
        [SerializeField, Range(0f, 0.5f)] private float maximumCompressionNormalized = DefaultMaximumCompressionNormalized;
        [Header("Sludge Viscosity")]
        [Tooltip("Hydraulic-fluid leak rate in cubic meters per second once the engine room is damaged and flooded.")]
        [SerializeField, Min(0f)] private float hydraulicLeakRateCubicMetersPerSecond = DefaultHydraulicLeakRateCubicMetersPerSecond;
        [Tooltip("Maximum normalized sludge viscosity injected into a flooded damaged engine compartment.")]
        [SerializeField, Range(0f, 1f)] private float maximumHydraulicViscosity = DefaultMaximumHydraulicViscosity;
        [Tooltip("How strongly sludge viscosity damps delayed slosh torque. Higher values make oily water behave more like syrup.")]
        [SerializeField, Min(0f)] private float viscositySloshDampingScale = DefaultViscositySloshDampingScale;
        [Tooltip("Extra environmental drag multiplier applied to the player while walking through sludge-filled flooded rooms.")]
        [SerializeField, Min(0f)] private float sludgePlayerDragMultiplier = DefaultSludgePlayerDragMultiplier;
        [Header("Ã¢â€â‚¬Ã¢â€â‚¬ Depressurization Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬")]
        [Tooltip("Reference exterior pressure used when a breached room vents directly into the ocean or a low-pressure zone.")]
        [SerializeField, Min(1f)] private float externalReferencePressureKPa = DefaultExternalReferencePressureKPa;
        [Tooltip("Minimum room-to-exterior pressure delta in kPa required before breach suction applies.")]
        [SerializeField, Min(0f)] private float minimumDepressurizationPressureDeltaKPa = DefaultDepressurizationMinimumPressureDeltaKPa;
        [Tooltip("Reference target mass used to convert pressure force into acceleration for suction-zone bodies.")]
        [SerializeField, Min(1f)] private float depressurizationReferenceMassKilograms = DefaultDepressurizationReferenceMassKilograms;
        [Tooltip("Safety cap applied to breach suction acceleration.")]
        [SerializeField, Min(0f)] private float maximumDepressurizationAccelerationMetersPerSecondSquared = DefaultDepressurizationMaxAccelerationMetersPerSecondSquared;
        [Tooltip("Extra radius added to the compartment-derived suction zone bounds.")]
        [SerializeField, Min(0f)] private float depressurizationRoomRadiusPaddingMeters = DefaultDepressurizationRoomRadiusPaddingMeters;
        [Tooltip("Minimum denominator used by inverse-distance suction falloff to prevent singular accelerations.")]
        [SerializeField, Min(0.05f)] private float depressurizationDistanceFloorMeters = DefaultDepressurizationDistanceFloorMeters;

        [Header("â”€â”€ Slosh Response â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Scales delayed local-space slosh torque from partially flooded compartments.")]
        [SerializeField, Min(0f)] private float sloshFactor = DefaultSloshFactor;

        [Tooltip("Compartments below this flood volume do not contribute slosh torque.")]
        [SerializeField, Min(0f)] private float sloshMinimumVolumeCubicMeters = DefaultSloshMinimumVolume;

        [Tooltip("Absolute cap on total delayed slosh torque in local space.")]
        [SerializeField, Min(0f)] private float maxSloshTorque = DefaultMaxSloshTorque;

        [Tooltip("Extra angular damping multiplier applied when the hull is submerged. 0.3 = 30% heavier turns.")]
        [SerializeField, Range(0f, 1f)] private float addedMassAngularDampingScale = DefaultAddedMassAngularDampingScale;

        [Tooltip("Extra linear damping multiplier applied when the hull is submerged. 0.3 = 30% heavier translational drag.")]
        [SerializeField, Range(0f, 1f)] private float addedMassLinearDampingScale = DefaultAddedMassLinearDampingScale;

        [Header("â”€â”€ Depth Source â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("When true, depth is sampled from the atmosphere sea level relative to the hull position.")]
        [SerializeField] private bool sampleDepthFromAtmosphere = true;

        [Tooltip("Fallback or manual external depth when atmospheric sea level sampling is disabled.")]
        [SerializeField, Min(0f)] private float manualExternalDepthMeters;

        [Header("── Hull Implosion ──────────────────")]
        [Tooltip("Below this external depth, implosion escalation never triggers.")]
        [SerializeField, Min(0f)] private float hullImplosionDepthThresholdMeters = DefaultHullImplosionDepthThresholdMeters;

        [Tooltip("Hydrostatic pressure threshold in kilopascals above which the hull is considered structurally lost.")]
        [SerializeField, Min(1f)] private float hullPressureRatingKPa = DefaultHullPressureRatingKPa;

        [Tooltip("Maximum fraction of the estimated hull surface that can become open breach area during catastrophic implosion.")]
        [SerializeField, Range(0f, 1f)] private float hullImplosionBreachAreaNormalized = DefaultHullImplosionBreachAreaNormalized;

        [Tooltip("Extra linear damping applied once the hull enters catastrophic implosion drag.")]
        [SerializeField, Min(0f)] private float implosionDragBonus = DefaultImplosionDragBonus;

        [Header("Flora Drag")]
        [Tooltip("Minimum submarine speed required before kelp and sargassum drag queries can amplify hydrodynamic damping.")]
        [SerializeField, Min(0f)] private float floraDragMinimumSpeedMetersPerSecond = 1f;

        [Tooltip("Minimum world-space sample radius used when querying macro-flora density around the hull.")]
        [SerializeField, Min(0.25f)] private float floraDragMinimumSampleRadiusMeters = 2.5f;

        [Tooltip("Additional linear damping multiplier applied at full macro-flora density.")]
        [SerializeField, Range(1f, 3f)] private float floraDragLinearMultiplier = 1.45f;

        [Tooltip("Additional angular damping multiplier applied at full macro-flora density.")]
        [SerializeField, Range(1f, 3f)] private float floraDragAngularMultiplier = 1.3f;

        [Tooltip("Additional hull mass applied at full macro-flora density to mimic overgrowth dragging the hull down.")]
        [SerializeField, Min(0f)] private float floraDragAddedMassAtFullDensityKilograms = DefaultFloraDragAddedMassAtFullDensityKilograms;

        [Tooltip("Maximum downward local center-of-mass shift caused by dense exterior overgrowth.")]
        [SerializeField, Min(0f)] private float floraCenterOfMassDownshiftMeters = DefaultFloraCenterOfMassDownshiftMeters;

        [Header("â”€â”€ Exterior Buoyancy â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Optional explicit collider used to derive exterior buoyancy sample points. Falls back to the first owned collider.")]
        [SerializeField] private Collider exteriorHullCollider;

        [Tooltip("Exterior displaced volume used by sampled buoyancy. Zero derives from total compartment capacity or the hull bounds.")]
        [SerializeField, Min(0f)] private float exteriorDisplacementVolumeCubicMeters;

        [Tooltip("Safety clamp applied against the theoretical Archimedes force for the full displacement volume.")]
        [SerializeField, Range(1f, 2f)] private float exteriorBuoyancyForceClampScale = DefaultExteriorBuoyancyForceClampScale;

        [Tooltip("Safety clamp multiplier applied against the theoretical buoyancy torque from the furthest sample lever arm.")]
        [SerializeField, Range(1f, 3f)] private float exteriorBuoyancyTorqueClampScale = DefaultExteriorBuoyancyTorqueClampScale;

        [Header("-- Payload Buoyancy --")]
        [Tooltip("When true, consumes typed inventory change signals and mirrors the latest cargo mass scalar into submarine buoyancy.")]
        [SerializeField] private bool syncCargoMassFromInventorySignals = true;

        [Tooltip("Maximum cargo mass that can affect buoyancy and draft.")]
        [SerializeField, Min(0f)] private float maxCargoMassKilograms = DefaultMaxCargoMassKilograms;
        [Tooltip("Damage-control leak severity-pressure converted into added sinking mass per second.")]
        [SerializeField, Min(0f)] private float damageControlLeakMassKilogramsPerKpaSecond = DefaultDamageControlLeakMassKilogramsPerKpaSecond;

        [Tooltip("Visual draft offset per 1000 kg of cargo. Positive values make the hull settle lower before Archimedes lift catches it.")]
        [SerializeField, Min(0f)] private float cargoDraftMetersPer1000Kg = DefaultCargoDraftMetersPer1000Kg;

        [Tooltip("Upper clamp for the mass-driven draft offset applied to waterline samples.")]
        [SerializeField, Min(0f)] private float maxCargoDraftOffsetMeters = DefaultMaxCargoDraftOffsetMeters;

        [Tooltip("Certified operating depth used for the abyss mass penalty. Zero resolves from the active SubmarineCoreDirector.")]
        [SerializeField, Min(0f)] private float safeCrushDepthMeters;

        [Tooltip("Buoyancy multiplier below safe depth. 0.85 means the abyss removes 15 percent lift.")]
        [SerializeField, Range(0.5f, 1f)] private float crushDepthBuoyancyScale = DefaultCrushDepthBuoyancyScale;

        [Header("-- Directional Hydro Drag --")]
        [Tooltip("Forward quadratic drag coefficient. Lateral drag is derived independently from local X.")]
        [SerializeField, Min(0f)] private float forwardHydroDragCoefficient = DefaultForwardHydroDragCoefficient;

        [Tooltip("Multiplier applied to forward drag for local-X broadside movement.")]
        [SerializeField, Min(1f)] private float lateralHydroDragMultiplier = DefaultLateralHydroDragMultiplier;

        [Tooltip("Vertical quadratic drag coefficient for rise/sink control.")]
        [SerializeField, Min(0f)] private float verticalHydroDragCoefficient = DefaultVerticalHydroDragCoefficient;

        [Tooltip("Counter-torque coefficient. Burst solver applies -angularVelocity * coefficient * waterDensity.")]
        [SerializeField, Min(0f)] private float angularHydroDragCoefficient = DefaultAngularHydroDragCoefficient;

        [Tooltip("Righting torque coefficient that lets the hull recover pitch and roll without rigidbody angular damping.")]
        [SerializeField, Min(0f)] private float pitchRollRightingTorqueCoefficient = DefaultRightingTorqueCoefficient;

        [Tooltip("Safety clamp for custom hydrodynamic acceleration packets.")]
        [SerializeField, Min(0f)] private float hydroSolverMaxAcceleration = DefaultHydroSolverMaxAcceleration;

        [Tooltip("Safety clamp for custom hydrodynamic torque packets.")]
        [SerializeField, Min(0f)] private float hydroSolverMaxTorque = DefaultHydroSolverMaxTorque;

        [Header("-- Ballast And Breach Feedback --")]
        [Tooltip("Fallback compressed-air reserve until the logistics owner exposes a typed air tank contract.")]
        [SerializeField, Min(0f)] private float compressedAirUnits = DefaultCompressedAirUnits;

        [Tooltip("Compressed-air units consumed by one ballast blow command.")]
        [SerializeField, Min(0f)] private float ballastBlowAirCost = DefaultBallastBlowAirCost;

        [Tooltip("Duration of the positive buoyancy pulse after a blow ballast command.")]
        [SerializeField, Min(0.05f)] private float ballastBlowDurationSeconds = DefaultBallastBlowDurationSeconds;

        [Tooltip("Upward acceleration bias during ballast blow.")]
        [SerializeField, Min(0f)] private float ballastBlowUpAcceleration = DefaultBallastBlowUpAcceleration;

        [Tooltip("Upward velocity threshold for a surfacing breach impact signal.")]
        [SerializeField, Min(0f)] private float surfacingBreachSpeedMetersPerSecond = DefaultSurfacingBreachSpeedMetersPerSecond;

        [Tooltip("Throttle fraction considered full thrust for cavitation rumble.")]
        [SerializeField, Range(0f, 1f)] private float cavitationThrottleThreshold = DefaultCavitationThrottleThreshold;

        [Tooltip("Speed below which full thrust is considered stalled.")]
        [SerializeField, Min(0f)] private float cavitationStallSpeedMetersPerSecond = DefaultCavitationStallSpeedMetersPerSecond;

        [Tooltip("Minimum time between cavitation rumble packets.")]
        [SerializeField, Min(0f)] private float cavitationCooldownSeconds = DefaultCavitationCooldownSeconds;

        [Header("â”€â”€ Diagnostics â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField] private int _debugConfiguredCompartmentCount;
        [SerializeField] private int _debugConfiguredBulkheadCount;
        [SerializeField] private float _debugExternalDepthMeters;
        [SerializeField] private float _debugTotalFloodVolumeCubicMeters;
        [SerializeField] private float _debugFloodFillRatio;
        [SerializeField] private Vector3 _debugReportedFloodCenterOfMassLocal;
        [SerializeField] private Vector3 _debugAppliedCenterOfMassLocal;
        [SerializeField] private Vector3 _debugAppliedInertiaTensor;
        [SerializeField] private float _debugAppliedRigidbodyMass;
        [SerializeField] private float _debugFloodMassKilograms;
        [SerializeField] private Vector3 _debugDelayedSloshAngularVelocityLocal;
        [SerializeField] private Vector3 _debugLastSloshTorqueLocal;
        [SerializeField] private float _debugExternalSubmergedVolumeCubicMeters;
        [SerializeField] private Vector3 _debugLastExternalBuoyancyForce;
        [SerializeField] private Vector3 _debugLastExternalBuoyancyTorque;
        [SerializeField] private float _debugAppliedLinearDamping;
        [SerializeField] private float _debugAppliedAngularDamping;
        [SerializeField] private float _debugSubmersionFactor;
        [SerializeField] private bool _debugHullImplosionActive;
        [SerializeField] private float _debugExternalPressureKPa;
        [SerializeField] private float _debugCompressionScale = 1f;
        [SerializeField] private float _debugFloraDragDensity;
        [SerializeField] private float _debugFloraAddedMassKilograms;
        [SerializeField] private float _debugTotalCargoMassKilograms;
        [SerializeField] private float _debugBallastWaterMassKilograms;
        [SerializeField] private float _debugDockedExternalMassKilograms;
        [SerializeField] private float _debugDamageControlLeakAddedMassKilograms;
        [SerializeField] private float _debugCargoMassScalar;
        [SerializeField] private float _debugCargoDraftOffsetMeters;
        [SerializeField] private float _debugCrushBuoyancyScale = 1f;
        [SerializeField] private float _debugTargetBuoyancyBias01;
        [SerializeField] private float _debugCompressedAirUnits;
        [SerializeField] private float _debugHydroForwardSpeed;
        [SerializeField] private float _debugHydroLateralSpeed;
        [SerializeField] private float _debugHydroVerticalSpeed;
        [SerializeField] private Vector3 _debugHydroDragAcceleration;
        [SerializeField] private Vector3 _debugHydroTorque;
        [SerializeField] private Vector3 _debugTowingTensionVector;
        [SerializeField] private bool _debugCavitationActive;
        [SerializeField] private Vector3 _debugLastThermalAnomalyCenter;
        [SerializeField] private float _debugLastThermalAnomalyTemperature;
        [SerializeField] private float _debugLastThermalAnomalyDepth;

        private Rigidbody _rigidbody;
        private Transform _cachedTransform;
        private Transform _cachedPlayerTransform;
        private HectonPlayerMovement _cachedPlayerMovement;
        private IDataVault _dataVault;

        private unsafe struct VaultNativeBuffer<T> where T : struct
        {
            private IDataVault _vault;
            private VaultBufferHandle<T> _handle;

            public bool IsCreated => _handle.IsCreated && _vault != null;

            public int Length => _handle.Length;

            public T this[int index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return IsIndexValid(index) ? UnsafeUtility.ReadArrayElement<T>(_handle.ptr, index) : default;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set
                {
                    if (IsIndexValid(index))
                        UnsafeUtility.WriteArrayElement(_handle.ptr, index, value);
                }
            }

            public bool Ensure(IDataVault vault, BufferID bufferId, int requiredLength)
            {
                if (vault == null)
                {
                    Clear();
                    return false;
                }

                bool mustRebind =
                    !ReferenceEquals(_vault, vault) ||
                    !_handle.IsCreated ||
                    _handle.BufferId != bufferId ||
                    _handle.Length < requiredLength ||
                    !IsGenerationCurrent(vault, bufferId);

                _vault = vault;
                if (mustRebind)
                {
                    _handle = vault.GetBufferHandle<T>(
                        bufferId,
                        requiredLength,
                        SystemID.VehiclesPhysics,
                        NativeArrayOptions.ClearMemory);
                }

                NativeArray<T> view = Resolve();
                if (view.IsCreated && view.Length >= requiredLength)
                    return true;

                Clear();
                return false;
            }

            public void Clear()
            {
                _vault = null;
                _handle = default;
            }

            public bool Refresh(IDataVault vault)
            {
                if (vault == null || !_handle.IsCreated)
                {
                    Clear();
                    return false;
                }

                BufferID bufferId = _handle.BufferId;
                int requiredLength = _handle.Length;
                if (!vault.TryGetBufferHandle(bufferId, out VaultBufferHandle<T> refreshed) ||
                    refreshed.Length < requiredLength)
                {
                    Clear();
                    return false;
                }

                _vault = vault;
                _handle = refreshed;
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public NativeArray<T> Resolve()
            {
                return _vault != null && _handle.IsCreated ? _handle.Resolve(_vault) : default;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator NativeArray<T>(VaultNativeBuffer<T> buffer)
            {
                return buffer.Resolve();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool IsIndexValid(int index)
            {
                return _handle.ptr != null && (uint)index < (uint)_handle.Length;
            }

            private bool IsGenerationCurrent(IDataVault vault, BufferID bufferId)
            {
                if (!_handle.IsCreated)
                    return false;

                return vault.TryGetBufferGeneration(bufferId, out uint generation) &&
                    generation == _handle.generation;
            }
        }

        private Rigidbody _cachedPlayerRigidbody;
        private IPlayerRuntimeContext _playerRuntime;
        private ISubmarineRuntimeContext _submarineRuntime;
        private HectonFluidEngine _fluidRuntime;
        private IPowerGridService _powerGridService;
        private byte _cachedFloodStateMathLod;
        private bool _registered;
        private bool _registeredOriginShiftListener;
        private bool _registeredHotSwapListener;
        private bool _registeredScalabilityListener;
        private bool _vaultNativeRefreshRequested;
        private bool _fluidJobRunning;
        private bool _skipHydrodynamicsForCurrentFixedTick;
        private bool _externalCenterOfMassAuthority;
        private int _configuredCompartmentCount;
        private int _configuredBulkheadCount;
        private int _ringHead;
        private int _vaultNativeStateMask;
        private float _externalDepthMeters;
        private float _floodFillRatio;
        private float _totalFloodVolumeCubicMeters;
        private float _currentFixedDeltaTime = DefaultFixedStepSeconds;
        private float _reportedCenterBlendAlpha;
        private float _reportedCenterBlendFixedStep = -1f;
        private float _centerOfMassBlendAlpha;
        private float _centerOfMassBlendFixedStep = -1f;
        private float _baseLinearDamping;
        private float _baseAngularDamping;
        private float _dryRigidbodyMass;
        private float _lastAppliedRigidbodyMass;
        private float _currentFloraDragDensity01;
        private float _currentFloraAddedMassKilograms;
        private float _dynamicCompressionScale = 1f;
        private float _lastAppliedLinearDamping;
        private float _lastAppliedAngularDamping;
        private float _externalSubmergedVolumeCubicMeters;
        private float _exteriorBuoyancyMaxLeverArm = 1f;
        private float _submersionFactor;
        private float _totalCargoMassKilograms;
        private float _ballastWaterMassKilograms;
        private float _dockedExternalMassKilograms;
        private float _damageControlLeakAddedMassKilograms;
        private float _cargoMassScalar;
        private float3 _externalFloodLinearDragTensor = new float3(1f);
        private float3 _externalFloodAngularDragTensor = new float3(1f);
        private float _lastResolvedCargoMassKilograms = -1f;
        private float _lastResolvedCargoScalar = -1f;
        private uint _lastInventoryMassSignalRevision;
        private float _ballastBlowTimer;
        private float _targetBuoyancyBias01;
        private float _thrustInput01;
        private float _cavitationCooldownTimer;
        private float _towingTensionHoldTimer;
        private bool _hydroKinematicJobRunning;
        private bool _hydroKinematicOutputReady;
        private Vector3 _reportedFloodCenterOfMassLocal;
        private Vector3 _appliedCenterOfMassLocal;
        private Vector3 _currentFloodCenterOfMassLocal;
        private Vector3 _resolvedDryInertiaTensor;
        private Vector3 _resolvedFloodedInertiaTensor;
        private Vector3 _lastAppliedInertiaTensor;
        private Vector3 _lastSloshTorqueLocal;
        private Vector3 _lastExternalBuoyancyForce;
        private Vector3 _lastExternalBuoyancyTorque;
        private Vector3 _pendingTowingTensionVector;
        private float _currentHydrodynamicLinearInertiaScale = 1f;
        private float _currentHydrodynamicAngularInertiaScale = 1f;
        private JobHandle _disposeHandle;
        private JobHandle _fluidJobHandle;
        private JobHandle _massPropertiesJobHandle;
        private JobHandle _hydroKinematicJobHandle;
        private bool _baselineDampingCached;
        private bool _baselineMassCached;
        private bool _massPropertiesJobRunning;
        private bool _hullImplosionActive;
        private int _hydroBlackBoxCursor;
        private bool _hydroBlackBoxDumped;
        private SubmarineAtmosphereSystem _atmosphereSystem;
        private ISubmarineHullBreachReadModel _structuralBreachReadModel;
        private IHectonOceanKinematics _oceanKinematics;
        private ResourceDistributionDirector _resourceDistributionRuntime;
        private IVocalWarningSystem _vocalWarningSystem;
        private readonly List<MonoBehaviour> _componentSearchBuffer = new List<MonoBehaviour>(4); // COLD ALLOC: List<MonoBehaviour>[4] - Unity GetComponents scratch, not runtime authority - owner: SubmarineFluidDynamics
        private readonly List<LogisticsPipeNode> _pipeBindingBuffer = new List<LogisticsPipeNode>(16); // COLD ALLOC: List<LogisticsPipeNode>[16] - Unity GetComponentsInChildren scratch, not runtime authority - owner: SubmarineFluidDynamics
        // COLD ALLOC: SpatialQueryHit[16] - WorldSpatialHashGrid managed-array API scratch, not runtime authority - owner: SubmarineFluidDynamics
        private readonly SpatialQueryHit[] _depressurizationContacts = new SpatialQueryHit[DepressurizationContactCapacity];
        // COLD ALLOC: Rigidbody[16] â€” unique rigidbody scratch for depressurization routing â€” owner: SubmarineFluidDynamics
        // Unity/PhysX managed reference scratch only; authoritative force output routes immediately through PhysicsForceRouter.
        private readonly Rigidbody[] _depressurizationBodies = new Rigidbody[DepressurizationContactCapacity];
        // COLD ALLOC: Collider[16] â€” bounded boiling-water rigidbody query scratch â€” owner: SubmarineFluidDynamics
        // Unity OverlapSphereNonAlloc scratch only; exterior thermal state lives in GlobalDataVault.
        private readonly Collider[] _exteriorThermalContacts = new Collider[ExteriorThermalContactCapacity];

        private VaultNativeBuffer<float> _compartmentFloodVolumes;
        private VaultNativeBuffer<float> _compartmentViscosity01;
        private VaultNativeBuffer<float> _compartmentBaseMaxVolumes;
        private VaultNativeBuffer<float> _compartmentMaxVolumes;
        private VaultNativeBuffer<float> _compartmentBreachAreas;
        private VaultNativeBuffer<float3> _compartmentLocalCentroids;
        private VaultNativeBuffer<uint> _compartmentFlags;
        private VaultNativeBuffer<int2> _bulkheadPairs;
        private VaultNativeBuffer<byte> _bulkheadSealed;
        private VaultNativeBuffer<float> _bulkheadDoorAreas;
        private VaultNativeBuffer<float3> _comAccumulatorFront;
        private VaultNativeBuffer<float3> _comAccumulatorBack;
        private VaultNativeBuffer<FloodMassPropertiesResult> _massPropertiesFront;
        private VaultNativeBuffer<FloodMassPropertiesResult> _massPropertiesBack;
        private VaultNativeBuffer<float3> _angularVelocityHistoryLocal;
        private VaultNativeBuffer<float> _previousExteriorSampleSubmersionFactors;
        private VaultNativeBuffer<float3> _exteriorBuoyancySampleLocalPoints;
        private VaultNativeBuffer<CompartmentState> _compartmentStates;
        private VaultNativeBuffer<float> _jobFloodVolumes;
        private VaultNativeBuffer<uint> _jobCompartmentFlags;
        private VaultNativeBuffer<float> _bulkheadTransferDeltas;
        private VaultNativeBuffer<HydroKinematicJobInput> _hydroKinematicInput;
        private VaultNativeBuffer<HydroKinematicJobOutput> _hydroKinematicOutput;
        private VaultNativeBuffer<HydroBlackBoxEntry> _hydroBlackBox;
        private VaultNativeBuffer<float3> _exteriorThermalAnomalyCenters;
        private VaultNativeBuffer<float> _exteriorThermalAnomalyTemperatures;
        private VaultNativeBuffer<float> _exteriorThermalAnomalyLifetimes;
        private VaultNativeBuffer<int> _exteriorThermalHazardIds;
        private FluidMathCore _fluidMathCore;
        private bool _fluidSimulationRegistered;
        private bool _isBrineSubmerged;
        private bool _wasBrineSubmerged;
        private float _brineSubmersionTime;

        [StructLayout(LayoutKind.Explicit, Size = 160)]
        private struct HydroKinematicJobInput
        {
            [FieldOffset(0)] public float3 Velocity;
            [FieldOffset(12)] public float3 AngularVelocity;
            [FieldOffset(24)] public float3 Forward;
            [FieldOffset(36)] public float3 Right;
            [FieldOffset(48)] public float3 Up;
            [FieldOffset(60)] public float3 WorldUp;
            [FieldOffset(72)] public float3 TowingAcceleration;
            [FieldOffset(84)] public float3 FlowVelocityWS;
            [FieldOffset(96)] public float MassKilograms;
            [FieldOffset(100)] public float AddedMassKilograms;
            [FieldOffset(104)] public float WaterDensity;
            [FieldOffset(108)] public float SubmersionFactor;
            [FieldOffset(112)] public float ForwardDragCoefficient;
            [FieldOffset(116)] public float LateralDragCoefficient;
            [FieldOffset(120)] public float VerticalDragCoefficient;
            [FieldOffset(124)] public float AngularDragCoefficient;
            [FieldOffset(128)] public float3 AngularDragTensorMultiplier;
            [FieldOffset(140)] public float RightingTorqueCoefficient;
            [FieldOffset(144)] public float BallastUpAcceleration;
            [FieldOffset(148)] public float MaxAcceleration;
            [FieldOffset(152)] public float MaxTorque;
        }

        [StructLayout(LayoutKind.Explicit, Size = 48)]
        private struct HydroKinematicJobOutput
        {
            [FieldOffset(0)] public float3 DragAcceleration;
            [FieldOffset(12)] public float3 Torque;
            [FieldOffset(24)] public float ForwardSpeed;
            [FieldOffset(28)] public float LateralSpeed;
            [FieldOffset(32)] public float VerticalSpeed;
        }

        [StructLayout(LayoutKind.Explicit, Size = 128)]
        private struct HydroBlackBoxEntry
        {
            [FieldOffset(0)] public int Frame;
            [FieldOffset(4)] public float FixedTime;
            [FieldOffset(8)] public float3 Position;
            [FieldOffset(20)] public float3 Velocity;
            [FieldOffset(32)] public float3 AngularVelocity;
            [FieldOffset(44)] public float MassKilograms;
            [FieldOffset(48)] public float CargoMassKilograms;
            [FieldOffset(52)] public float CargoMassScalar;
            [FieldOffset(56)] public float SubmersionFactor;
            [FieldOffset(60)] public float DepthMeters;
            [FieldOffset(64)] public float FloodRatio;
            [FieldOffset(68)] public float BallastBias01;
            [FieldOffset(72)] public float3 HydroAcceleration;
            [FieldOffset(84)] public float3 HydroTorque;
            [FieldOffset(96)] public float3 TowingTension;
            [FieldOffset(108)] public float BrineSubmersionTime;
            [FieldOffset(112)] public uint Flags;
            [FieldOffset(116)] public uint StateHash;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [StructLayout(LayoutKind.Sequential)]
        private struct HydroKinematicDragJob : IJob
        {
            [ReadOnly] public NativeArray<HydroKinematicJobInput> Input;
            public NativeArray<HydroKinematicJobOutput> Output;

            public void Execute()
            {
                HydroKinematicJobInput input = Input[0];
                float3 velocity = SelectFinite(input.Velocity, float3.zero);
                float3 flowVelocity = SelectFinite(input.FlowVelocityWS, float3.zero);
                float3 relativeVelocity = velocity - flowVelocity;
                float3 angularVelocity = SelectFinite(input.AngularVelocity, float3.zero);
                float3 forward = SelectFinite(input.Forward, new float3(0f, 0f, 1f));
                float3 right = SelectFinite(input.Right, new float3(1f, 0f, 0f));
                float3 up = SelectFinite(input.Up, new float3(0f, 1f, 0f));
                float3 worldUp = SelectFinite(input.WorldUp, new float3(0f, 1f, 0f));

                float submersion = math.saturate(input.SubmersionFactor);
                float waterDensity = math.max(0f, input.WaterDensity);
                float mass = math.max(Epsilon, input.MassKilograms + math.max(0f, input.AddedMassKilograms));
                float forwardSpeed = math.dot(relativeVelocity, forward);
                float lateralSpeed = math.dot(relativeVelocity, right);
                float verticalSpeed = math.dot(relativeVelocity, up);

                float3 dragForce =
                    (-forward * forwardSpeed * math.abs(forwardSpeed) * math.max(0f, input.ForwardDragCoefficient)) +
                    (-right * lateralSpeed * math.abs(lateralSpeed) * math.max(0f, input.LateralDragCoefficient)) +
                    (-up * verticalSpeed * math.abs(verticalSpeed) * math.max(0f, input.VerticalDragCoefficient));
                dragForce *= waterDensity * submersion;

                float3 acceleration = dragForce * math.rcp(mass);
                acceleration += SelectFinite(input.TowingAcceleration, float3.zero);
                acceleration += worldUp * (math.max(0f, input.BallastUpAcceleration) * submersion);
                acceleration = ClampFiniteMagnitude(acceleration, math.max(0f, input.MaxAcceleration));

                float3 angularTensor = math.max(new float3(0.1f), SelectFinite(input.AngularDragTensorMultiplier, new float3(1f)));
                float angularDrag = math.max(0f, input.AngularDragCoefficient);
                float pitchSpeed = math.dot(angularVelocity, right);
                float yawSpeed = math.dot(angularVelocity, up);
                float rollSpeed = math.dot(angularVelocity, forward);
                float3 torque =
                    (-right * pitchSpeed * angularDrag * angularTensor.x) +
                    (-up * yawSpeed * angularDrag * angularTensor.y) +
                    (-forward * rollSpeed * angularDrag * angularTensor.z);
                torque *= waterDensity * submersion;
                float3 rightingAxis = math.cross(up, worldUp);
                float rightingAxisLengthSq = math.lengthsq(rightingAxis);
                if (rightingAxisLengthSq > 0.000001f)
                {
                    torque += rightingAxis * (math.max(0f, input.RightingTorqueCoefficient) * mass * submersion);
                }

                torque = ClampFiniteMagnitude(torque, math.max(0f, input.MaxTorque));
                Output[0] = new HydroKinematicJobOutput
                {
                    DragAcceleration = acceleration,
                    Torque = torque,
                    ForwardSpeed = forwardSpeed,
                    LateralSpeed = lateralSpeed,
                    VerticalSpeed = verticalSpeed
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float3 SelectFinite(float3 value, float3 fallback)
            {
                return math.all(math.isfinite(value)) && !math.any(math.isnan(value)) ? value : fallback;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float3 ClampFiniteMagnitude(float3 value, float maxMagnitude)
            {
                if (!math.all(math.isfinite(value)) || math.any(math.isnan(value)))
                    return float3.zero;

                if (maxMagnitude <= 0f)
                    return value;

                float lengthSq = math.lengthsq(value);
                float maxSq = maxMagnitude * maxMagnitude;
                if (!math.isfinite(lengthSq) || math.isnan(lengthSq))
                    return float3.zero;

                return lengthSq > maxSq ? value * (maxMagnitude * math.rsqrt(math.max(lengthSq, 0.000001f))) : value;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [StructLayout(LayoutKind.Sequential)]
        private struct FluidTransferJob : IJob
        {
            [ReadOnly] public NativeArray<float> InputFloodVolumes;
            [ReadOnly] public NativeArray<float> MaxVolumes;
            [ReadOnly] public NativeArray<float> BreachAreas;
            [ReadOnly] public NativeArray<uint> InputFlags;

            public NativeArray<float> OutputFloodVolumes;
            public NativeArray<uint> OutputFlags;

            public int CompartmentCount;
            public float DepthMeters;
            public float FixedDeltaTime;
            public float DischargeCoefficient;
            public float MaximumIngressPerSecondNormalized;

            public void Execute()
            {
                for (int i = 0; i < CompartmentCapacity; i++)
                {
                    if (i >= CompartmentCount)
                    {
                        OutputFloodVolumes[i] = 0f;
                        OutputFlags[i] = 0u;
                        continue;
                    }

                    float maxVolume = MaxVolumes[i];
                    float currentVolume = InputFloodVolumes[i];
                    uint flags = InputFlags[i] & PersistentFlagsMask;
                    if (maxVolume <= Epsilon)
                    {
                        OutputFloodVolumes[i] = 0f;
                        OutputFlags[i] = 0u;
                        continue;
                    }

                    float breachArea = BreachAreas[i];
                    if (breachArea > Epsilon)
                    {
                        flags |= FlagBreached;

                        float remainingCapacity = maxVolume - currentVolume;
                        if (remainingCapacity > Epsilon)
                        {
                            currentVolume = FluidMathCore.ResolveIngressVolume(
                                currentVolume,
                                maxVolume,
                                breachArea,
                                DepthMeters,
                                FixedDeltaTime,
                                DischargeCoefficient,
                                MaximumIngressPerSecondNormalized,
                                GravityMetersPerSecondSquared,
                                Epsilon);
                        }
                    }
                    else
                    {
                        flags &= ~FlagBreached;
                    }

                    OutputFloodVolumes[i] = currentVolume;
                    OutputFlags[i] = flags;
                }
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [StructLayout(LayoutKind.Sequential)]
        private struct BulkheadTransferDeltaJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> FloodVolumes;
            [ReadOnly] public NativeArray<float> MaxVolumes;
            [ReadOnly] public NativeArray<int2> BulkheadPairs;
            [ReadOnly] public NativeArray<byte> BulkheadSealed;
            [ReadOnly] public NativeArray<float> BulkheadDoorAreas;

            public NativeArray<float> TransferDeltas;

            public int CompartmentCount;
            public float FixedDeltaTime;
            public float BulkheadFlowCoefficient;
            public float MaxTransferPerTick;
            public float DischargeCoefficient;
            public float NearZeroHeadDampingMeters;

            public void Execute(int index)
            {
                TransferDeltas[index] = 0f;

                int2 pair = BulkheadPairs[index];
                int compartmentA = pair.x;
                int compartmentB = pair.y;
                if (compartmentA < 0 || compartmentA >= CompartmentCount || compartmentB < 0 || compartmentB >= CompartmentCount)
                    return;

                if (BulkheadSealed[index] != 0)
                    return;

                float maxVolumeA = MaxVolumes[compartmentA];
                float maxVolumeB = MaxVolumes[compartmentB];
                if (maxVolumeA <= Epsilon || maxVolumeB <= Epsilon)
                    return;

                TransferDeltas[index] = FluidMathCore.ResolveBulkheadTransferDelta(
                    FloodVolumes[compartmentA],
                    FloodVolumes[compartmentB],
                    maxVolumeA,
                    maxVolumeB,
                    BulkheadDoorAreas[index],
                    FixedDeltaTime,
                    BulkheadFlowCoefficient,
                    MaxTransferPerTick,
                    DischargeCoefficient,
                    NearZeroHeadDampingMeters,
                    GravityMetersPerSecondSquared,
                    Epsilon);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [StructLayout(LayoutKind.Sequential)]
        private struct ApplyBulkheadTransferJob : IJob
        {
            [ReadOnly] public NativeArray<int2> BulkheadPairs;
            [ReadOnly] public NativeArray<byte> BulkheadSealed;
            [ReadOnly] public NativeArray<float> MaxVolumes;
            [ReadOnly] public NativeArray<float> TransferDeltas;

            public NativeArray<float> FloodVolumes;
            public NativeArray<uint> Flags;

            public int BulkheadCount;
            public int CompartmentCount;

            public void Execute()
            {
                for (int i = 0; i < BulkheadCount; i++)
                {
                    int2 pair = BulkheadPairs[i];
                    int compartmentA = pair.x;
                    int compartmentB = pair.y;
                    if (compartmentA < 0 || compartmentA >= CompartmentCount || compartmentB < 0 || compartmentB >= CompartmentCount)
                        continue;

                    if (BulkheadSealed[i] != 0)
                    {
                        Flags[compartmentA] |= FlagSealed;
                        Flags[compartmentB] |= FlagSealed;
                        continue;
                    }

                    float deltaVolume = TransferDeltas[i];
                    if (deltaVolume > Epsilon)
                    {
                        FloodVolumes[compartmentA] = math.clamp(FloodVolumes[compartmentA] - deltaVolume, 0f, MaxVolumes[compartmentA]);
                        FloodVolumes[compartmentB] = math.clamp(FloodVolumes[compartmentB] + deltaVolume, 0f, MaxVolumes[compartmentB]);
                        Flags[compartmentA] |= FlagTransferSource;
                        Flags[compartmentB] |= FlagTransferDestination;
                    }
                    else if (deltaVolume < -Epsilon)
                    {
                        float transferMagnitude = -deltaVolume;
                        FloodVolumes[compartmentA] = math.clamp(FloodVolumes[compartmentA] + transferMagnitude, 0f, MaxVolumes[compartmentA]);
                        FloodVolumes[compartmentB] = math.clamp(FloodVolumes[compartmentB] - transferMagnitude, 0f, MaxVolumes[compartmentB]);
                        Flags[compartmentB] |= FlagTransferSource;
                        Flags[compartmentA] |= FlagTransferDestination;
                    }
                }
            }
        }

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [StructLayout(LayoutKind.Explicit, Size = 48)]
        private struct FloodMassPropertiesResult
        {
            [FieldOffset(0)] public float FloodMassKilograms;
            [FieldOffset(4)] public float FloodMassRatio;
            [FieldOffset(8)] public float3 FloodCenterLocal;
            [FieldOffset(20)] public float3 TargetCenterLocal;
            [FieldOffset(32)] public float3 InertiaTensor;
            [FieldOffset(44)] public uint Reserved0;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [StructLayout(LayoutKind.Sequential)]
        private struct FloodMassPropertiesJob : IJob
        {
            [ReadOnly] public NativeArray<float> FloodVolumes;
            [ReadOnly] public NativeArray<float> MaxVolumes;
            [ReadOnly] public NativeArray<float3> LocalCentroids;
            [ReadOnly] public NativeArray<uint> Flags;

            public NativeArray<float3> WeightedCentroids;
            public NativeArray<FloodMassPropertiesResult> Output;

            public int CompartmentCount;
            public float3 DryCenterLocal;
            public float3 DryInertiaTensor;
            public float3 FloodedInertiaTensor;

            public void Execute()
            {
                float totalFloodMass = 0f;
                float totalCapacity = 0f;
                float3 weightedSum = float3.zero;

                for (int i = 0; i < CompartmentCapacity; i++)
                {
                    if (i >= CompartmentCount)
                    {
                        WeightedCentroids[i] = float3.zero;
                        continue;
                    }

                    float maxVolume = math.max(0f, MaxVolumes[i]);
                    totalCapacity += maxVolume;
                    if (maxVolume <= Epsilon || (Flags[i] & FlagFrozen) != 0u)
                    {
                        WeightedCentroids[i] = float3.zero;
                        continue;
                    }

                    float currentVolume = math.clamp(FloodVolumes[i], 0f, maxVolume);
                    float mass = currentVolume * WaterDensityKgPerCubicMeter;
                    float3 weightedCentroid = LocalCentroids[i] * mass;
                    WeightedCentroids[i] = weightedCentroid;
                    weightedSum += weightedCentroid;
                    totalFloodMass += mass;
                }

                float3 floodCenter = DryCenterLocal;
                if (totalFloodMass > Epsilon)
                    floodCenter = weightedSum * math.rcp(math.max(MinimumMassForReciprocal, totalFloodMass));

                float maxFloodMass = totalCapacity * WaterDensityKgPerCubicMeter;
                float floodMassRatio = maxFloodMass > Epsilon
                    ? math.saturate(totalFloodMass * math.rcp(math.max(MinimumMassForReciprocal, maxFloodMass)))
                    : 0f;

                float3 targetCenter = LerpMad(DryCenterLocal, floodCenter, floodMassRatio);
                float3 inertiaTensor = LerpMad(DryInertiaTensor, FloodedInertiaTensor, floodMassRatio);

                Output[0] = new FloodMassPropertiesResult
                {
                    FloodMassKilograms = math.isfinite(totalFloodMass) ? math.max(0f, totalFloodMass) : 0f,
                    FloodMassRatio = math.isfinite(floodMassRatio) ? math.saturate(floodMassRatio) : 0f,
                    FloodCenterLocal = math.all(math.isfinite(floodCenter)) ? floodCenter : DryCenterLocal,
                    TargetCenterLocal = math.all(math.isfinite(targetCenter)) ? targetCenter : DryCenterLocal,
                    InertiaTensor = math.all(math.isfinite(inertiaTensor)) ? inertiaTensor : DryInertiaTensor
                };
            }
        }

        /// <summary>Configured compartment count authored for this submarine, clamped to the supported maximum.</summary>
        public int CompartmentCount => _configuredCompartmentCount;

        /// <summary>Total flood volume currently tracked across all compartments.</summary>
        public float TotalFloodVolumeCubicMeters => _totalFloodVolumeCubicMeters;

        /// <summary>Normalized total fill ratio across the authored compartment capacity.</summary>
        public float FloodFillRatio => _floodFillRatio;

        /// <summary>Number of deferred splash payloads available for downstream VFX polling.</summary>
        public int PendingSplashEventCount => 0;

        /// <summary>Reported local-space flood centroid for telemetry, audio, or VFX queries.</summary>
        public Vector3 ReportedFloodCenterOfMassLocal => _reportedFloodCenterOfMassLocal;

        /// <summary>Resolved external water depth used by the current ingress step.</summary>
        public float ExternalDepthMeters => _externalDepthMeters;

        internal int ConfiguredBulkheadCount => _configuredBulkheadCount;

        internal void SetExternalCenterOfMassAuthority(bool enabled)
        {
            _externalCenterOfMassAuthority = enabled;
            if (!enabled || _rigidbody == null)
                return;

            _appliedCenterOfMassLocal = SanitizeCenterOfMass(_rigidbody.centerOfMass, _appliedCenterOfMassLocal);
        }

        private void Awake()
        {
            // COLD ALLOC: FluidMathCore[1] - data-only submarine fluid math service registered through GlobalRegistry - owner: SubmarineFluidDynamics
            _fluidMathCore ??= new FluidMathCore();
            CacheReferences();
            RefreshResolvedInertiaTensors();
            RefreshDerivedConstants(DefaultFixedStepSeconds);
            RefreshDebugState();
        }

        private void OnEnable()
        {
            TryRegisterFluidSimulationService();
            CacheReferences();
            TryRegisterHotSwapListener();
            TryRegisterScalabilityListener();
            EnsureNativeState();
            RebuildExteriorBuoyancySampleLocalPoints();
            RefreshResolvedInertiaTensors();
            SeedNativeStateFromAuthoring();
            RefreshDerivedConstants(DefaultFixedStepSeconds);
            if (HasActiveHydrodynamicsConfiguration())
            {
                TryRegister();
                TryRegisterOriginShiftListener();
                SeedCargoMassFromRegistryCold();
            }
            else
            {
                TryUnregisterOriginShiftListener();
                TryUnregister();
            }

            RefreshDebugState();
        }

        private void OnDisable()
        {
            TryUnregisterFluidSimulationService();
            TryUnregisterHotSwapListener();
            TryUnregisterScalabilityListener();
            TryUnregisterOriginShiftListener();
            TryUnregister();
            ClearExteriorThermalAnomalies();
            ResetBrineHullState();
            RestoreRigidbodyDynamics();
            DisposeNativeStateDeferred();
            ClearRuntimeServiceCaches();
        }

        private void OnDestroy()
        {
            TryUnregisterFluidSimulationService();
            TryUnregisterHotSwapListener();
            TryUnregisterOriginShiftListener();
            TryUnregister();
            ClearExteriorThermalAnomalies();
            ResetBrineHullState();
            RestoreRigidbodyDynamics();
            DisposeNativeStateDeferred();
            ClearRuntimeServiceCaches();
        }

        /// <summary>
        /// Fixed-step fluid ingress, inter-compartment transfer, inertia interpolation, and delayed slosh torque.
        /// </summary>
        /// <param name="fixedDeltaTime">Discrete physics step accumulated through the dispatcher cadence.</param>
        public void FixedTick(float fixedDeltaTime)
        {
            RefreshVaultNativeStateAfterRelocation();
            if (!_compartmentFloodVolumes.IsCreated || _rigidbody == null || fixedDeltaTime <= 0f)
                return;

            if (!HasActiveHydrodynamicsConfiguration())
                return;

            _skipHydrodynamicsForCurrentFixedTick = false;
            _currentFixedDeltaTime = fixedDeltaTime;
            ConsumeInventoryMassSignals();
            UpdateHydroRuntimeState(fixedDeltaTime);
            ApplyCompletedHydroKinematicOutput();
            float depthMeters = ResolveExternalDepthMeters();
            WriteHydroBlackBoxSample(depthMeters, 0u);
            if (ShouldAbortHydrodynamicsFixedTick())
                return;

            RefreshDerivedConstants(fixedDeltaTime);
            SyncBulkheadSealedFlags();
            SyncStructuralBreachIngress();
            ApplyDynamicCompressionToCompartments();
            ApplyIceExpansionPhaseChange();
            ApplyHydraulicLeakViscosity(fixedDeltaTime);
            ApplyOverflowSpillover();
            EvaluateHullImplosion(depthMeters);
            FinalizeCompartmentState();
            if (ShouldAbortHydrodynamicsFixedTick())
                return;

            ApplyBreachDepressurizationSuction();
            if (ShouldAbortHydrodynamicsFixedTick())
                return;

            ApplyFloodMassPropertiesToRigidbody(force: false);
            if (ShouldAbortHydrodynamicsFixedTick())
                return;

            float3 targetFloodCenter = ResolveFloodTargetCenterOfMassFromBufferedJob();
            if (ShouldAbortHydrodynamicsFixedTick())
                return;

            ApplyCenterOfMassShift(targetFloodCenter);
            if (ShouldAbortHydrodynamicsFixedTick())
                return;

            UpdateReportedFloodCenter(targetFloodCenter);
            ApplyInterpolatedInertiaTensor();
            if (ShouldAbortHydrodynamicsFixedTick())
                return;

            ApplySampledExteriorBuoyancy(depthMeters);
            if (ShouldAbortHydrodynamicsFixedTick())
                return;

            ApplyAddedMassDamping();
            if (ShouldAbortHydrodynamicsFixedTick())
                return;

            UpdateExteriorThermalAnomalies(fixedDeltaTime);
            if (ShouldAbortHydrodynamicsFixedTick())
                return;

            ApplySludgePlayerDrag();
            if (ShouldAbortHydrodynamicsFixedTick())
                return;

            ApplyDelayedSloshTorque();
            if (ShouldAbortHydrodynamicsFixedTick())
                return;

            ScheduleHydroKinematicJob(fixedDeltaTime);
            ScheduleFloodMassPropertiesJob();
            ScheduleFluidTransferJob(depthMeters, fixedDeltaTime);
            RefreshDebugState();
        }

        /// <inheritdoc />
        public void PostFixedTick(float fixedDeltaTime)
        {
            CompleteHydroKinematicJobInPostFixedSwapWindow();
            CompleteFluidTransferInPostFixedSwapWindow();
            CompleteFloodMassPropertiesInPostFixedSwapWindow();
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.PowerGrid)
            {
                _powerGridService = currentService as IPowerGridService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _playerRuntime = currentService as IPlayerRuntimeContext;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Submarine)
            {
                _submarineRuntime = currentService as ISubmarineRuntimeContext;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.FluidRuntime)
            {
                _fluidRuntime = currentService as HectonFluidEngine;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.OceanKinematics)
            {
                IHectonOceanKinematicsService oceanKinematicsService = currentService as IHectonOceanKinematicsService;
                _oceanKinematics = oceanKinematicsService != null ? oceanKinematicsService.ActiveProvider : null;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.SubmarineHullBreach)
            {
                _structuralBreachReadModel = currentService as ISubmarineHullBreachReadModel;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.ResourceDistributionRuntime)
            {
                _resourceDistributionRuntime = currentService as ResourceDistributionDirector;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.VocalWarningRuntime)
            {
                _vocalWarningSystem = currentService as IVocalWarningSystem;
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            if (ReferenceEquals(_dataVault, currentService))
                return;

            DisposeNativeStateDeferred();
            _dataVault = currentService as IDataVault;
            _vaultNativeRefreshRequested = false;
            if (_dataVault == null || !isActiveAndEnabled)
                return;

            EnsureNativeState();
            SeedNativeStateFromAuthoring();
            RefreshDerivedConstants(DefaultFixedStepSeconds);
            RefreshDebugState();
        }

        public void OnScalabilityChanged(in Hecton8.Core.Contracts.Signals.ScalabilityChangedEvent payload)
        {
            _cachedFloodStateMathLod = payload.CurrentTier == ScalabilityTierProfiles.LowMx350 ? (byte)0 : (byte)1;
        }

        /// <summary>
        /// Overrides automatic depth sampling with a manual external water depth.
        /// </summary>
        public void SetExternalDepthMeters(float depthMeters)
        {
            sampleDepthFromAtmosphere = false;
            manualExternalDepthMeters = math.max(0f, depthMeters);
            _externalDepthMeters = manualExternalDepthMeters;
            RefreshDebugState();
        }

        /// <summary>
        /// Sets the current depth-driven compartment compression scale applied to authored flood capacities.
        /// </summary>
        public void SetCompartmentCompressionScale(float compressionScale)
        {
            _dynamicCompressionScale = math.clamp(compressionScale, 1f - math.saturate(maximumCompressionNormalized), 1f);
        }

        /// <summary>Total cargo mass currently coupled into hull mass and draft.</summary>
        public float TotalCargoMassKg => _totalCargoMassKilograms + _ballastWaterMassKilograms;

        /// <summary>Ballast water mass currently coupled into hull mass and draft.</summary>
        public float BallastWaterMassKg => _ballastWaterMassKilograms;

        /// <summary>Damage-control leak mass currently coupled into hull sinking weight.</summary>
        public float DamageControlLeakAddedMassKg => _damageControlLeakAddedMassKilograms;

        /// <summary>Cached 0-1 cargo mass scalar used by low-tier math LOD consumers.</summary>
        public float CargoMassScalar => _cargoMassScalar;

        /// <summary>
        /// Adds bounded sinking mass from packed submarine breach severity multiplied by ambient pressure.
        /// </summary>
        public void ApplyDamageControlLeakMass(float severityPressureKPa, float fixedDeltaTime)
        {
            if (!math.isfinite(severityPressureKPa) || !math.isfinite(fixedDeltaTime))
                return;

            float deltaKg = math.max(0f, severityPressureKPa) *
                            math.max(0f, damageControlLeakMassKilogramsPerKpaSecond) *
                            math.max(0f, fixedDeltaTime);
            if (deltaKg <= 0f)
                return;

            float maxLeakMass = math.max(0f, ResolveExteriorDisplacementVolumeCubicMeters()) * WaterDensityKgPerCubicMeter;
            if (maxLeakMass <= Epsilon)
                maxLeakMass = math.max(_dryRigidbodyMass, Epsilon);

            _damageControlLeakAddedMassKilograms = math.min(maxLeakMass, _damageControlLeakAddedMassKilograms + deltaKg);
            _debugDamageControlLeakAddedMassKilograms = _damageControlLeakAddedMassKilograms;
            ApplyFloodMassPropertiesToRigidbody(force: false);
        }

        /// <summary>
        /// External storage or logistics systems can publish total cargo mass here without linking concrete inventory code.
        /// </summary>
        public void SetCargoMassScalar(float totalCargoMassKg)
        {
            float safeMaxCargo = math.max(0f, maxCargoMassKilograms);
            float safeMass = math.isfinite(totalCargoMassKg) ? math.max(0f, totalCargoMassKg) : 0f;
            _totalCargoMassKilograms = safeMaxCargo > 0f ? math.min(safeMass, safeMaxCargo) : safeMass;
            _cargoMassScalar = safeMaxCargo > Epsilon
                ? math.saturate(_totalCargoMassKilograms * math.rcp(safeMaxCargo))
                : 0f;
        }

        /// <summary>Compatibility alias for submarine storage systems that speak in kilograms.</summary>
        public void SetSubmarineCargoMassKilograms(float totalCargoMassKg)
        {
            SetCargoMassScalar(totalCargoMassKg);
        }

        public void SetBallastWaterMassKilograms(float massKg)
        {
            _ballastWaterMassKilograms = math.isfinite(massKg) ? math.max(0f, massKg) : 0f;
            _debugBallastWaterMassKilograms = _ballastWaterMassKilograms;
        }

        public void SetExternalFloodDragTensor(float3 linearMultiplier, float3 angularMultiplier)
        {
            _externalFloodLinearDragTensor = math.all(math.isfinite(linearMultiplier))
                ? math.clamp(linearMultiplier, new float3(0.1f), new float3(4f))
                : new float3(1f);
            _externalFloodAngularDragTensor = math.all(math.isfinite(angularMultiplier))
                ? math.clamp(angularMultiplier, new float3(0.1f), new float3(4f))
                : new float3(1f);
        }

        public void SetDockedExternalMassKilograms(float massKg)
        {
            _dockedExternalMassKilograms = math.isfinite(massKg) ? math.max(0f, massKg) : 0f;
        }

        /// <summary>Updates a cached thrust input used to detect stalled full-power cavitation.</summary>
        public void SetThrustInput01(float thrustInput01)
        {
            _thrustInput01 = math.saturate(math.isfinite(thrustInput01) ? thrustInput01 : 0f);
        }

        /// <summary>Injects external tether tension into the next submarine velocity-solver packet.</summary>
        public void SetTowingTensionVector(Vector3 tensionVector)
        {
            if (!IsFiniteVector(tensionVector))
                return;

            _pendingTowingTensionVector = tensionVector;
            _towingTensionHoldTimer = DefaultTowingTensionHoldSeconds;
        }

        /// <summary>Authoritative logistics bridge for compressed-air reserves.</summary>
        public void SetCompressedAirUnits(float units)
        {
            compressedAirUnits = math.max(0f, math.isfinite(units) ? units : 0f);
        }

        /// <summary>Command entry point for controls/UI: burns compressed air and applies a temporary positive buoyancy bias.</summary>
        public bool BlowBallast()
        {
            return TryBlowBallast(ballastBlowDurationSeconds);
        }

        /// <summary>Command entry point with explicit duration for scripted emergency procedures.</summary>
        public bool TryBlowBallast(float durationSeconds)
        {
            float cost = math.max(0f, ballastBlowAirCost);
            if (compressedAirUnits < cost)
                return false;

            compressedAirUnits = math.max(0f, compressedAirUnits - cost);
            _ballastBlowTimer = math.max(0.05f, math.isfinite(durationSeconds) ? durationSeconds : ballastBlowDurationSeconds);
            _targetBuoyancyBias01 = 1f;

            if (_vocalWarningSystem != null)
            {
                uint sourceId = _rigidbody != null ? unchecked((uint)EntityId.ToULong(_rigidbody.GetEntityId())) : 0u;
                _vocalWarningSystem.TryQueueWarning(
                    (byte)VocalWarningId.CrushDepth,
                    0.85f,
                    0.6f,
                    VocalWarningSignalFlags.HabitatIntegrityCompromised,
                    sourceId);
            }

            return true;
        }

        internal float HullPressureRatingKPa => math.max(1f, hullPressureRatingKPa);

        /// <summary>
        /// Enables or resizes a compartment breach opening in square meters.
        /// </summary>
        public void TriggerBreach(int compartmentIndex, float breachAreaSquareMeters)
        {
            if (!IsCompartmentIndexValid(compartmentIndex) || !_compartmentBreachAreas.IsCreated)
                return;

            CompletePendingFluidTransferForAuthoritativeWrite();
            CompletePendingFloodMassPropertiesForAuthoritativeWrite();
            float sanitizedArea = math.max(0f, breachAreaSquareMeters);
            _compartmentBreachAreas[compartmentIndex] = sanitizedArea;

            uint flags = _compartmentFlags[compartmentIndex];
            if (sanitizedArea > Epsilon)
                flags |= FlagBreached;
            else
                flags &= ~FlagBreached;

            _compartmentFlags[compartmentIndex] = flags;
        }

        /// <summary>
        /// Clears active ingress for a compartment without draining existing flood volume.
        /// </summary>
        public void ClearBreach(int compartmentIndex)
        {
            TriggerBreach(compartmentIndex, 0f);
        }

        internal void TriggerImmediateBreachDepressurization(int compartmentIndex, Vector3 breachWorldPosition, float breachAreaSquareMeters)
        {
            if (_atmosphereSystem == null || _cachedTransform == null || !IsCompartmentIndexValid(compartmentIndex))
                return;

            TriggerBreach(compartmentIndex, breachAreaSquareMeters);

            float externalPressureKPa = math.max(1f, externalReferencePressureKPa);
            float pressureDeltaKPa = _atmosphereSystem.GetRoomPressureKPa(compartmentIndex) - externalPressureKPa;
            if (pressureDeltaKPa < math.max(0f, minimumDepressurizationPressureDeltaKPa))
                return;

            Vector3 roomCenter = _cachedTransform.TransformPoint(GetCompartmentCentroid(compartmentIndex));
            float roomVolume = compartmentIndex < _compartmentMaxVolumes.Length
                ? math.max(Epsilon, _compartmentMaxVolumes[compartmentIndex])
                : Epsilon;
            float compartmentRadius = SafeCubeRoot(roomVolume * math.rcp(4.1887903f));
            float influenceRadius = math.max(0.5f, compartmentRadius + math.max(0f, depressurizationRoomRadiusPaddingMeters));
            float rawForceNewtons = pressureDeltaKPa * 1000f * math.max(Epsilon, breachAreaSquareMeters);
            float baseAcceleration = rawForceNewtons * math.rcp(math.max(1f, depressurizationReferenceMassKilograms));
            float maximumAcceleration = math.max(0f, maximumDepressurizationAccelerationMetersPerSecondSquared);
            if (!math.isfinite(baseAcceleration) || baseAcceleration <= Epsilon || maximumAcceleration <= Epsilon)
                return;

            IPlayerRuntimeContext playerContext = ResolvePlayerRuntimeContext();
            Rigidbody playerBody = playerContext != null ? playerContext.PlayerRigidbody : null;
            Transform playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
            HectonPlayerMovement playerMovement = playerContext != null ? playerContext.PlayerMovement : null;

            if (playerTransform != null)
            {
                if (playerMovement != null)
                {
                    ApplyDepressurizationToPlayer(
                        playerMovement,
                        playerTransform.position,
                        breachWorldPosition,
                        roomCenter,
                        influenceRadius,
                        baseAcceleration,
                        maximumAcceleration);
                }
                else if (playerBody != null)
                {
                    ApplyDepressurizationToBody(
                        playerBody,
                        playerTransform.position,
                        breachWorldPosition,
                        roomCenter,
                        influenceRadius,
                        baseAcceleration,
                        maximumAcceleration);
                }
            }

            ApplyDepressurizationToLooseBodies(
                playerBody,
                roomCenter,
                breachWorldPosition,
                influenceRadius,
                baseAcceleration,
                maximumAcceleration);
        }

        /// <summary>
        /// Sets the sealed state for a bulkhead pair. Closed bulkheads halt transfer instantly.
        /// </summary>
        public void SetBulkheadSealed(int compartmentA, int compartmentB, bool isSealed)
        {
            if (!_bulkheadPairs.IsCreated || !_bulkheadSealed.IsCreated)
                return;

            CompletePendingFluidTransferForAuthoritativeWrite();
            CompletePendingFloodMassPropertiesForAuthoritativeWrite();
            int bulkheadIndex = FindBulkheadIndex(compartmentA, compartmentB);
            if (bulkheadIndex >= 0)
            {
                _bulkheadSealed[bulkheadIndex] = isSealed ? (byte)1 : (byte)0;
                SyncBulkheadSealedFlags();
                RefreshDebugState();
            }
        }

        /// <summary>
        /// Authoritatively writes a compartment fill ratio for save restore or scripted setup.
        /// </summary>
        public void SetCompartmentFillNormalized(int compartmentIndex, float fillNormalized)
        {
            if (!IsCompartmentIndexValid(compartmentIndex) || !_compartmentFloodVolumes.IsCreated)
                return;

            CompletePendingFluidTransferForAuthoritativeWrite();
            CompletePendingFloodMassPropertiesForAuthoritativeWrite();
            float maxVolume = _compartmentMaxVolumes[compartmentIndex];
            _compartmentFloodVolumes[compartmentIndex] = math.saturate(fillNormalized) * maxVolume;
            RefreshDerivedConstants(DefaultFixedStepSeconds);
            FinalizeCompartmentState();
            float3 targetFloodCenter = ResolveFloodTargetCenterOfMassLocal();
            SeedFloodMassPropertiesBuffers(targetFloodCenter, _floodFillRatio);
            ApplyCenterOfMassShift(targetFloodCenter);
            UpdateReportedFloodCenter(targetFloodCenter);
            ApplyInterpolatedInertiaTensor();
            ApplyAddedMassDamping();
            RefreshDebugState();
        }

        internal void AddCompartmentFloodVolumeDelta(int compartmentIndex, float deltaVolumeCubicMeters)
        {
            if (!IsCompartmentIndexValid(compartmentIndex) || !_compartmentFloodVolumes.IsCreated || deltaVolumeCubicMeters == 0f)
                return;

            CompletePendingFluidTransferForAuthoritativeWrite();
            CompletePendingFloodMassPropertiesForAuthoritativeWrite();
            float nextVolume = math.max(0f, _compartmentFloodVolumes[compartmentIndex] + deltaVolumeCubicMeters);
            _compartmentFloodVolumes[compartmentIndex] = nextVolume;

            float maxVolume = _compartmentMaxVolumes.IsCreated ? math.max(0f, _compartmentMaxVolumes[compartmentIndex]) : 0f;
            if (maxVolume > Epsilon && nextVolume > maxVolume + Epsilon)
                _compartmentFlags[compartmentIndex] |= FlagOverflow;
            else
                _compartmentFlags[compartmentIndex] &= ~FlagOverflow;
        }

        /// <summary>
        /// Returns normalized flood fill for a compartment. Invalid indices return zero.
        /// </summary>
        public float GetCompartmentFillRatio(int compartmentIndex)
        {
            if (!IsCompartmentIndexValid(compartmentIndex) || !_compartmentFloodVolumes.IsCreated)
                return 0f;

            float maxVolume = _compartmentMaxVolumes[compartmentIndex];
            if (maxVolume <= Epsilon)
                return 0f;

            return TryResolveSafeNormalizedRatio(_compartmentFloodVolumes[compartmentIndex], maxVolume, out float fillRatio)
                ? fillRatio
                : 0f;
        }

        internal float GetCompartmentFloodVolumeCubicMeters(int compartmentIndex)
        {
            if (!IsCompartmentIndexValid(compartmentIndex) || !_compartmentFloodVolumes.IsCreated)
                return 0f;

            return _compartmentFloodVolumes[compartmentIndex];
        }

        internal float GetCompartmentMaxFloodVolumeCubicMeters(int compartmentIndex)
        {
            if (!IsCompartmentIndexValid(compartmentIndex) || !_compartmentMaxVolumes.IsCreated)
                return 0f;

            return _compartmentMaxVolumes[compartmentIndex];
        }

        internal void SetCompartmentGasPartialPressuresKPa(
            int compartmentIndex,
            float oxygenPartialPressureKPa,
            float carbonDioxidePartialPressureKPa,
            float nitrogenPartialPressureKPa)
        {
            if (!IsCompartmentIndexValid(compartmentIndex) || !_compartmentStates.IsCreated)
                return;

            CompartmentState state = _compartmentStates[compartmentIndex];
            state.o2PartialPressureKPa = SanitizeNonNegativeFinite(oxygenPartialPressureKPa);
            state.co2PartialPressureKPa = SanitizeNonNegativeFinite(carbonDioxidePartialPressureKPa);
            state.n2PartialPressureKPa = SanitizeNonNegativeFinite(nitrogenPartialPressureKPa);
            _compartmentStates[compartmentIndex] = state;
        }

        internal float GetCompartmentViscosity01(int compartmentIndex)
        {
            if (!IsCompartmentIndexValid(compartmentIndex) || !_compartmentViscosity01.IsCreated)
                return 0f;

            return math.saturate(_compartmentViscosity01[compartmentIndex]);
        }

        internal bool TryGetBulkheadDefinition(int bulkheadIndex, out int compartmentA, out int compartmentB, out bool isSealed)
        {
            compartmentA = -1;
            compartmentB = -1;
            isSealed = false;

            if (bulkheadIndex < 0 || bulkheadIndex >= _configuredBulkheadCount || !_bulkheadPairs.IsCreated || !_bulkheadSealed.IsCreated)
                return false;

            int2 pair = _bulkheadPairs[bulkheadIndex];
            if (!IsCompartmentIndexValid(pair.x) || !IsCompartmentIndexValid(pair.y))
                return false;

            compartmentA = pair.x;
            compartmentB = pair.y;
            isSealed = _bulkheadSealed[bulkheadIndex] != 0;
            return true;
        }

        internal float GetBulkheadDoorAreaSquareMeters(int bulkheadIndex)
        {
            if (bulkheadIndex < 0 || bulkheadIndex >= _configuredBulkheadCount || !_bulkheadDoorAreas.IsCreated)
                return DefaultBulkheadDoorAreaSquareMeters;

            return math.max(Epsilon, _bulkheadDoorAreas[bulkheadIndex]);
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (shiftData.ShiftOffset.sqrMagnitude <= 0.000001f)
                return;

            ResetSloshHistoryForOriginShift();
            ResetSplashDetectionState(clearQueuedEvents: true);
            _pendingTowingTensionVector = Vector3.zero;
            _towingTensionHoldTimer = 0f;
        }

        /// <summary>
        /// Dequeues one sampled exterior water-entry payload for downstream VFX systems.
        /// </summary>
        /// <param name="splashEvent">Resolved splash payload when available.</param>
        /// <returns>True when a splash payload was dequeued.</returns>
        public bool TryDequeueSplashEvent(out SplashEvent splashEvent)
        {
            splashEvent = default;
            return false;
        }

        /// <summary>
        /// Injects localized exterior water heat from a high-energy tool impact into the quantized 8 m thermal anomaly field.
        /// </summary>
        /// <param name="runtimePoint">Runtime world-space sample point inside the water volume.</param>
        /// <param name="direction">Beam direction used for buoyancy impulse orientation.</param>
        /// <param name="cutStrength">Coupled cutter heat strength in joule-like gameplay units.</param>
        /// <param name="normalizedPower">Normalized beam power in the [0..1] range.</param>
        public void InjectLocalizedWaterHeat(Vector3 runtimePoint, Vector3 direction, float cutStrength, float normalizedPower)
        {
            if (_cachedTransform == null || cutStrength <= 0f || normalizedPower <= MinEffectiveBeamPowerForThermalAnomaly())
                return;

            float heatEnergyJoules = cutStrength * math.saturate(normalizedPower);
            if (heatEnergyJoules <= 0f)
                return;

            InjectLocalizedWaterHeatJoulesInternal(runtimePoint, heatEnergyJoules);
        }

        /// <summary>
        /// Injects localized heat directly into the exterior water anomaly field using submarine-local coordinates and explicit joules.
        /// </summary>
        /// <param name="localPos">Local-space sample point relative to the submarine transform.</param>
        /// <param name="joules">Absolute heat energy added to the quantized fluid cell.</param>
        public void InjectLocalizedWaterHeat(float3 localPos, float joules)
        {
            if (_cachedTransform == null || joules <= 0f)
                return;

            Vector3 runtimePoint = _cachedTransform.TransformPoint(new Vector3(localPos.x, localPos.y, localPos.z));
            InjectLocalizedWaterHeatJoulesInternal(runtimePoint, joules);
        }

        /// <summary>
        /// Injects localized heat directly into the exterior water anomaly field using a runtime-space sample point.
        /// </summary>
        public void InjectLocalizedWaterHeat(Vector3 runtimePoint, float joules)
        {
            InjectLocalizedWaterHeatJoulesInternal(runtimePoint, joules);
        }

        private void InjectLocalizedWaterHeatJoulesInternal(Vector3 runtimePoint, float heatEnergyJoules)
        {
            if (_cachedTransform == null ||
                heatEnergyJoules <= 0f ||
                !_exteriorThermalAnomalyCenters.IsCreated ||
                !_exteriorThermalAnomalyTemperatures.IsCreated ||
                !_exteriorThermalAnomalyLifetimes.IsCreated ||
                !_exteriorThermalHazardIds.IsCreated)
            {
                return;
            }

            float surfaceY = ResolveSurfaceHeightAtSample(runtimePoint, runtimePoint.y);
            float depthMeters = math.max(0f, surfaceY - runtimePoint.y);
            if (depthMeters <= 0.01f)
                return;

            Vector3 quantizedCenter = QuantizeExteriorThermalCell(runtimePoint);
            float3 quantizedCenterFloat = ToFloat3(quantizedCenter);
            int slotIndex = ResolveExteriorThermalSlot(quantizedCenter);
            if (slotIndex < 0)
                return;

            if (_exteriorThermalAnomalyLifetimes[slotIndex] > 0f &&
                math.lengthsq(ResolveExteriorThermalAnomalyCenter(slotIndex) - quantizedCenterFloat) > 0.01f &&
                _exteriorThermalHazardIds[slotIndex] != 0)
            {
                HectonHazardManager.Unregister(_exteriorThermalHazardIds[slotIndex]);
                _exteriorThermalHazardIds[slotIndex] = 0;
            }

            float cellVolume = ExteriorThermalCellSizeMeters * ExteriorThermalCellSizeMeters * ExteriorThermalCellSizeMeters;
            float cellMass = cellVolume * WaterDensityKgPerCubicMeter;
            float deltaTemperature = heatEnergyJoules * math.rcp(math.max(1f, cellMass * ExteriorWaterSpecificHeatCapacityJoulesPerKilogramCelsius));
            if (!math.isfinite(deltaTemperature) || deltaTemperature <= 0f)
                return;

            float currentTemperature = _exteriorThermalAnomalyLifetimes[slotIndex] > 0f
                ? _exteriorThermalAnomalyTemperatures[slotIndex]
                : ExteriorWaterReferenceTemperatureCelsius;
            _exteriorThermalAnomalyCenters[slotIndex] = quantizedCenterFloat;
            _exteriorThermalAnomalyTemperatures[slotIndex] = math.max(ExteriorWaterReferenceTemperatureCelsius, currentTemperature + deltaTemperature);
            _exteriorThermalAnomalyLifetimes[slotIndex] = math.max(_exteriorThermalAnomalyLifetimes[slotIndex], ExteriorThermalLifetimeSeconds);
            _debugLastThermalAnomalyCenter = quantizedCenter;
            _debugLastThermalAnomalyTemperature = _exteriorThermalAnomalyTemperatures[slotIndex];
            _debugLastThermalAnomalyDepth = depthMeters;
        }

        /// <summary>
        /// Returns the local centroid authored for a compartment. Invalid indices return the dry center.
        /// </summary>
        public Vector3 GetCompartmentCentroid(int compartmentIndex)
        {
            if (!IsCompartmentIndexValid(compartmentIndex) || !_compartmentLocalCentroids.IsCreated)
                return dryCenterOfMassLocal;

            float3 centroid = _compartmentLocalCentroids[compartmentIndex];
            return new Vector3(centroid.x, centroid.y, centroid.z);
        }

        /// <summary>
        /// Returns the current compartment state flags. Invalid indices return zero.
        /// </summary>
        public uint GetCompartmentFlags(int compartmentIndex)
        {
            if (!IsCompartmentIndexValid(compartmentIndex) || !_compartmentFlags.IsCreated)
                return 0u;

            return _compartmentFlags[compartmentIndex];
        }

        private void CacheReferences()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            if (_rigidbody == null)
                TryGetComponent(out _rigidbody);

            if (_atmosphereSystem == null)
                TryGetComponent(out _atmosphereSystem);

            if (exteriorHullCollider == null)
                TryGetComponent(out exteriorHullCollider);

            if (_structuralBreachReadModel == null)
                _structuralBreachReadModel = GlobalRegistry.SubmarineHullBreach;

            if (_oceanKinematics == null || !_oceanKinematics.IsAvailable)
            {
                IHectonOceanKinematicsService oceanKinematicsService = GlobalRegistry.OceanKinematics;
                _oceanKinematics = oceanKinematicsService != null ? oceanKinematicsService.ActiveProvider : null;
            }

            if (_resourceDistributionRuntime == null)
                _resourceDistributionRuntime = GlobalRegistry.ResourceDistribution;

            if (_vocalWarningSystem == null || IsUnityObjectInvalid(_vocalWarningSystem))
                _vocalWarningSystem = GlobalRegistry.VocalWarnings;

            IDataVault registryDataVault = GlobalRegistry.DataVault;
            if (!ReferenceEquals(_dataVault, registryDataVault))
            {
                _dataVault = registryDataVault;
                _vaultNativeRefreshRequested = true;
            }

            RefreshRuntimeActorContextsIfMissing();
            if (_powerGridService == null || IsUnityObjectInvalid(_powerGridService))
                _powerGridService = GlobalRegistry.PowerGrid;

            if (_structuralBreachReadModel == null)
            {
                _componentSearchBuffer.Clear();
                GetComponents(_componentSearchBuffer);
                for (int i = 0; i < _componentSearchBuffer.Count; i++)
                {
                    MonoBehaviour component = _componentSearchBuffer[i];
                    if (ReferenceEquals(component, this))
                        continue;

                    if (component is ISubmarineHullBreachReadModel readModel)
                    {
                        _structuralBreachReadModel = readModel;
                        break;
                    }
                }
            }

            if (_rigidbody != null && !_baselineDampingCached)
            {
                _baseLinearDamping = 0f;
                _baseAngularDamping = 0f;
                _rigidbody.linearDamping = 0f;
                _rigidbody.angularDamping = 0f;
                _lastAppliedLinearDamping = _baseLinearDamping;
                _lastAppliedAngularDamping = _baseAngularDamping;
                _baselineDampingCached = true;
            }

            if (_rigidbody != null && !_baselineMassCached)
            {
                _dryRigidbodyMass = math.isfinite(_rigidbody.mass) ? math.max(_rigidbody.mass, Epsilon) : 1f;
                _lastAppliedRigidbodyMass = _dryRigidbodyMass;
                _baselineMassCached = true;
            }
        }

        private void RefreshVaultNativeStateAfterRelocation()
        {
            ReadOnlySpan<MemoryAddressShiftSignal> shifts = SignalBus<MemoryAddressShiftSignal>.GetFrameSnapshot();
            for (int i = 0; i < shifts.Length; i++)
            {
                MemoryAddressShiftSignal shift = shifts[i];
                if (shift.SystemId == (byte)SystemID.VehiclesPhysics || IsSubmarineFluidVaultBuffer(shift.BufferId))
                {
                    _vaultNativeRefreshRequested = true;
                    break;
                }
            }

            if (!_vaultNativeRefreshRequested)
                return;

            RefreshNativeStateViewsFromVault();
            _vaultNativeRefreshRequested = false;
        }

        private static bool IsSubmarineFluidVaultBuffer(int bufferId)
        {
            switch ((BufferID)bufferId)
            {
                case BufferID.SubmarineFluidCompartmentFloodVolumes:
                case BufferID.SubmarineFluidCompartmentViscosity01:
                case BufferID.SubmarineFluidCompartmentBaseMaxVolumes:
                case BufferID.SubmarineFluidCompartmentMaxVolumes:
                case BufferID.SubmarineFluidCompartmentBreachAreas:
                case BufferID.SubmarineFluidCompartmentLocalCentroids:
                case BufferID.SubmarineFluidCompartmentFlags:
                case BufferID.SubmarineFluidBulkheadPairs:
                case BufferID.SubmarineFluidBulkheadSealed:
                case BufferID.SubmarineFluidBulkheadDoorAreas:
                case BufferID.SubmarineFluidComAccumulatorFront:
                case BufferID.SubmarineFluidComAccumulatorBack:
                case BufferID.SubmarineFluidMassPropertiesFront:
                case BufferID.SubmarineFluidMassPropertiesBack:
                case BufferID.SubmarineFluidAngularVelocityHistoryLocal:
                case BufferID.SubmarineFluidPreviousExteriorSampleSubmersionFactors:
                case BufferID.SubmarineFluidExteriorBuoyancySampleLocalPoints:
                case BufferID.SubmarineFluidCompartmentStates:
                case BufferID.SubmarineFluidJobFloodVolumes:
                case BufferID.SubmarineFluidJobCompartmentFlags:
                case BufferID.SubmarineFluidBulkheadTransferDeltas:
                case BufferID.SubmarineHydroKinematicInput:
                case BufferID.SubmarineHydroKinematicOutput:
                case BufferID.SubmarineHydroBlackBox:
                case BufferID.SubmarineFluidExteriorThermalCenters:
                case BufferID.SubmarineFluidExteriorThermalTemperatures:
                case BufferID.SubmarineFluidExteriorThermalLifetimes:
                case BufferID.SubmarineFluidExteriorThermalHazardIds:
                    return true;
                default:
                    return false;
            }
        }

        private void EnsureNativeState()
        {
            // COLD ALLOC: NativeArray<float>[8] â€” compartment flood volume storage â€” owner: SubmarineFluidDynamics
            EnsureNativeStateBuffer(ref _compartmentFloodVolumes, BufferID.SubmarineFluidCompartmentFloodVolumes, CompartmentCapacity, nameof(_compartmentFloodVolumes), VaultCompartmentFloodVolumesFlag);
            // COLD ALLOC: NativeArray<float>[8] — per-compartment normalized sludge viscosity state — owner: SubmarineFluidDynamics
            EnsureNativeStateBuffer(ref _compartmentViscosity01, BufferID.SubmarineFluidCompartmentViscosity01, CompartmentCapacity, nameof(_compartmentViscosity01), VaultCompartmentViscosityFlag);
            // COLD ALLOC: NativeArray<float>[8] — authored compartment capacities preserved for dynamic crush compression — owner: SubmarineFluidDynamics
            EnsureNativeStateBuffer(ref _compartmentBaseMaxVolumes, BufferID.SubmarineFluidCompartmentBaseMaxVolumes, CompartmentCapacity, nameof(_compartmentBaseMaxVolumes), VaultCompartmentBaseMaxVolumesFlag);
            // COLD ALLOC: NativeArray<float>[8] â€” compartment capacity storage â€” owner: SubmarineFluidDynamics
            EnsureNativeStateBuffer(ref _compartmentMaxVolumes, BufferID.SubmarineFluidCompartmentMaxVolumes, CompartmentCapacity, nameof(_compartmentMaxVolumes), VaultCompartmentMaxVolumesFlag);
            // COLD ALLOC: NativeArray<float>[8] â€” active breach area storage â€” owner: SubmarineFluidDynamics
            EnsureNativeStateBuffer(ref _compartmentBreachAreas, BufferID.SubmarineFluidCompartmentBreachAreas, CompartmentCapacity, nameof(_compartmentBreachAreas), VaultCompartmentBreachAreasFlag);
            // COLD ALLOC: NativeArray<float3>[8] â€” local compartment centroids â€” owner: SubmarineFluidDynamics
            EnsureNativeStateBuffer(ref _compartmentLocalCentroids, BufferID.SubmarineFluidCompartmentLocalCentroids, CompartmentCapacity, nameof(_compartmentLocalCentroids), VaultCompartmentLocalCentroidsFlag);
            // COLD ALLOC: NativeArray<uint>[8] â€” compartment state flags â€” owner: SubmarineFluidDynamics
            EnsureNativeStateBuffer(ref _compartmentFlags, BufferID.SubmarineFluidCompartmentFlags, CompartmentCapacity, nameof(_compartmentFlags), VaultCompartmentFlagsFlag);
            // COLD ALLOC: NativeArray<int2>[7] â€” bulkhead adjacency pairs â€” owner: SubmarineFluidDynamics
            EnsureNativeStateBuffer(ref _bulkheadPairs, BufferID.SubmarineFluidBulkheadPairs, BulkheadCapacity, nameof(_bulkheadPairs), VaultBulkheadPairsFlag);
            // COLD ALLOC: NativeArray<byte>[7] â€” bulkhead seal state â€” owner: SubmarineFluidDynamics
            EnsureNativeStateBuffer(ref _bulkheadSealed, BufferID.SubmarineFluidBulkheadSealed, BulkheadCapacity, nameof(_bulkheadSealed), VaultBulkheadSealedFlag);
            // COLD ALLOC: NativeArray<float>[7] â€” authored bulkhead doorway areas for pressure blowout math â€” owner: SubmarineFluidDynamics
            EnsureNativeStateBuffer(ref _bulkheadDoorAreas, BufferID.SubmarineFluidBulkheadDoorAreas, BulkheadCapacity, nameof(_bulkheadDoorAreas), VaultBulkheadDoorAreasFlag);
            // COLD ALLOC: NativeArray<float3>[8] â€” ping-pong flood centroid accumulator front buffer â€” owner: SubmarineFluidDynamics
            EnsureNativeStateBuffer(ref _comAccumulatorFront, BufferID.SubmarineFluidComAccumulatorFront, CompartmentCapacity, nameof(_comAccumulatorFront), VaultComAccumulatorFrontFlag);
            // COLD ALLOC: NativeArray<float3>[8] â€” ping-pong flood centroid accumulator back buffer â€” owner: SubmarineFluidDynamics
            EnsureNativeStateBuffer(ref _comAccumulatorBack, BufferID.SubmarineFluidComAccumulatorBack, CompartmentCapacity, nameof(_comAccumulatorBack), VaultComAccumulatorBackFlag);
            // COLD ALLOC: NativeArray<FloodMassPropertiesResult>[1] â€” front flood mass-properties result buffer â€” owner: SubmarineFluidDynamics
            EnsureNativeStateBuffer(ref _massPropertiesFront, BufferID.SubmarineFluidMassPropertiesFront, 1, nameof(_massPropertiesFront), VaultMassPropertiesFrontFlag);
            // COLD ALLOC: NativeArray<FloodMassPropertiesResult>[1] â€” back flood mass-properties result buffer â€” owner: SubmarineFluidDynamics
            EnsureNativeStateBuffer(ref _massPropertiesBack, BufferID.SubmarineFluidMassPropertiesBack, 1, nameof(_massPropertiesBack), VaultMassPropertiesBackFlag);
            // COLD ALLOC: NativeArray<float3>[16] â€” local angular-velocity slosh history supporting 50â€“150 ms delayed counter-torque taps â€” owner: SubmarineFluidDynamics
            EnsureNativeStateBuffer(ref _angularVelocityHistoryLocal, BufferID.SubmarineFluidAngularVelocityHistoryLocal, RingBufferLength, nameof(_angularVelocityHistoryLocal), VaultAngularVelocityHistoryFlag);
            // COLD ALLOC: NativeArray<float>[8] â€” previous sampled exterior submersion factors for splash transition detection â€” owner: SubmarineFluidDynamics
            EnsureNativeStateBuffer(ref _previousExteriorSampleSubmersionFactors, BufferID.SubmarineFluidPreviousExteriorSampleSubmersionFactors, ExteriorBuoyancySampleCount, nameof(_previousExteriorSampleSubmersionFactors), VaultExteriorSubmersionHistoryFlag);
            // COLD ALLOC: NativeArray<float3>[8] - exterior buoyancy local sample points - owner: GlobalDataVault/VehiclesPhysics
            EnsureNativeStateBuffer(ref _exteriorBuoyancySampleLocalPoints, BufferID.SubmarineFluidExteriorBuoyancySampleLocalPoints, ExteriorBuoyancySampleCount, nameof(_exteriorBuoyancySampleLocalPoints), VaultExteriorBuoyancySamplesFlag);
            // COLD ALLOC: NativeArray<CompartmentState>[8] - authoritative compartment flood snapshots for CoM and telemetry - owner: GlobalDataVault/VehiclesPhysics
            EnsureNativeStateBuffer(ref _compartmentStates, BufferID.SubmarineFluidCompartmentStates, CompartmentCapacity, nameof(_compartmentStates), VaultCompartmentStatesFlag);
            // COLD ALLOC: NativeArray<float>[8] Ã¢â‚¬â€ Burst fluid-transfer output volumes Ã¢â‚¬â€ owner: SubmarineFluidDynamics
            EnsureNativeStateBuffer(ref _jobFloodVolumes, BufferID.SubmarineFluidJobFloodVolumes, CompartmentCapacity, nameof(_jobFloodVolumes), VaultJobFloodVolumesFlag);
            // COLD ALLOC: NativeArray<uint>[8] Ã¢â‚¬â€ Burst fluid-transfer output flags Ã¢â‚¬â€ owner: SubmarineFluidDynamics
            EnsureNativeStateBuffer(ref _jobCompartmentFlags, BufferID.SubmarineFluidJobCompartmentFlags, CompartmentCapacity, nameof(_jobCompartmentFlags), VaultJobCompartmentFlagsFlag);
            // COLD ALLOC: NativeArray<float>[7] Ã¢â‚¬â€ per-bulkhead transfer delta scratch Ã¢â‚¬â€ owner: SubmarineFluidDynamics
            EnsureNativeStateBuffer(ref _bulkheadTransferDeltas, BufferID.SubmarineFluidBulkheadTransferDeltas, BulkheadCapacity, nameof(_bulkheadTransferDeltas), VaultBulkheadTransferDeltasFlag);
            // COLD ALLOC: NativeArray<HydroKinematicJobInput>[1] - submarine true-buoyancy and custom drag input packet - owner: SubmarineFluidDynamics
            EnsureNativeStateBuffer(ref _hydroKinematicInput, BufferID.SubmarineHydroKinematicInput, 1, nameof(_hydroKinematicInput), VaultHydroInputFlag);
            // COLD ALLOC: NativeArray<HydroKinematicJobOutput>[1] - one-frame-late custom drag force/torque packet - owner: SubmarineFluidDynamics
            EnsureNativeStateBuffer(ref _hydroKinematicOutput, BufferID.SubmarineHydroKinematicOutput, 1, nameof(_hydroKinematicOutput), VaultHydroOutputFlag);
            // COLD ALLOC: NativeArray<HydroBlackBoxEntry>[300] - fixed hydro crash telemetry ring - owner: SubmarineFluidDynamics
            EnsureNativeStateBuffer(ref _hydroBlackBox, BufferID.SubmarineHydroBlackBox, HydroBlackBoxCapacity, nameof(_hydroBlackBox), VaultHydroBlackBoxFlag);
            // COLD ALLOC: NativeArray<float3>[8] - exterior boil-cell centers - owner: GlobalDataVault/VehiclesPhysics
            EnsureNativeStateBuffer(ref _exteriorThermalAnomalyCenters, BufferID.SubmarineFluidExteriorThermalCenters, ExteriorThermalAnomalyCapacity, nameof(_exteriorThermalAnomalyCenters), VaultExteriorThermalCentersFlag);
            // COLD ALLOC: NativeArray<float>[8] - exterior boil-cell temperatures - owner: GlobalDataVault/VehiclesPhysics
            EnsureNativeStateBuffer(ref _exteriorThermalAnomalyTemperatures, BufferID.SubmarineFluidExteriorThermalTemperatures, ExteriorThermalAnomalyCapacity, nameof(_exteriorThermalAnomalyTemperatures), VaultExteriorThermalTemperaturesFlag);
            // COLD ALLOC: NativeArray<float>[8] - exterior boil-cell lifetimes - owner: GlobalDataVault/VehiclesPhysics
            EnsureNativeStateBuffer(ref _exteriorThermalAnomalyLifetimes, BufferID.SubmarineFluidExteriorThermalLifetimes, ExteriorThermalAnomalyCapacity, nameof(_exteriorThermalAnomalyLifetimes), VaultExteriorThermalLifetimesFlag);
            // COLD ALLOC: NativeArray<int>[8] - exterior boil-cell hazard ids - owner: GlobalDataVault/VehiclesPhysics
            EnsureNativeStateBuffer(ref _exteriorThermalHazardIds, BufferID.SubmarineFluidExteriorThermalHazardIds, ExteriorThermalAnomalyCapacity, nameof(_exteriorThermalHazardIds), VaultExteriorThermalHazardIdsFlag);
            _vaultNativeRefreshRequested = false;
        }

        private void SeedNativeStateFromAuthoring()
        {
            if (!_compartmentFloodVolumes.IsCreated)
                return;

            int highestConfiguredCompartmentIndex = -1;
            int authoredCompartmentCount = math.clamp(compartments != null ? compartments.Length : 0, 0, CompartmentCapacity);
            for (int i = 0; i < authoredCompartmentCount; i++)
            {
                CompartmentDefinition definition = compartments[i];
                if (math.isfinite(definition.maxFloodVolumeCubicMeters) && definition.maxFloodVolumeCubicMeters > Epsilon)
                    highestConfiguredCompartmentIndex = i;
            }

            _configuredCompartmentCount = highestConfiguredCompartmentIndex + 1;
            for (int i = 0; i < CompartmentCapacity; i++)
            {
                if (i < _configuredCompartmentCount)
                {
                    CompartmentDefinition definition = compartments[i];
                    float maxVolume = math.isfinite(definition.maxFloodVolumeCubicMeters)
                        ? math.max(0f, definition.maxFloodVolumeCubicMeters)
                        : 0f;
                    float initialFillNormalized = math.isfinite(definition.initialFillNormalized)
                        ? math.saturate(definition.initialFillNormalized)
                        : 0f;
                    float fillVolume = initialFillNormalized * maxVolume;
                    float breachArea = math.isfinite(definition.breachAreaSquareMeters)
                        ? math.max(0f, definition.breachAreaSquareMeters)
                        : 0f;

                    _compartmentBaseMaxVolumes[i] = maxVolume;
                    _compartmentMaxVolumes[i] = maxVolume;
                    _compartmentFloodVolumes[i] = fillVolume;
                    _compartmentBreachAreas[i] = breachArea;
                    _compartmentLocalCentroids[i] = new float3(
                        math.isfinite(definition.localCentroid.x) ? definition.localCentroid.x : 0f,
                        math.isfinite(definition.localCentroid.y) ? definition.localCentroid.y : 0f,
                        math.isfinite(definition.localCentroid.z) ? definition.localCentroid.z : 0f);
                    _compartmentFlags[i] = breachArea > Epsilon ? FlagBreached : 0u;
                }
                else
                {
                    _compartmentBaseMaxVolumes[i] = 0f;
                    _compartmentMaxVolumes[i] = 0f;
                    _compartmentFloodVolumes[i] = 0f;
                    _compartmentBreachAreas[i] = 0f;
                    _compartmentLocalCentroids[i] = float3.zero;
                    _compartmentFlags[i] = 0u;
                }

                _comAccumulatorFront[i] = float3.zero;
                _comAccumulatorBack[i] = float3.zero;
                if (_compartmentViscosity01.IsCreated)
                    _compartmentViscosity01[i] = 0f;
            }

            if (_massPropertiesFront.IsCreated)
                _massPropertiesFront[0] = default;

            if (_massPropertiesBack.IsCreated)
                _massPropertiesBack[0] = default;

            if (_configuredCompartmentCount <= 0)
            {
                _configuredBulkheadCount = 0;
            }
            else if (bulkheads != null && bulkheads.Length > 0)
            {
                _configuredBulkheadCount = math.clamp(bulkheads.Length, 0, BulkheadCapacity);
                for (int i = 0; i < _configuredBulkheadCount; i++)
                {
                    BulkheadDefinition bulkhead = bulkheads[i];
                    _bulkheadPairs[i] = new int2(
                        math.clamp(bulkhead.compartmentA, 0, CompartmentCapacity - 1),
                        math.clamp(bulkhead.compartmentB, 0, CompartmentCapacity - 1));
                    _bulkheadSealed[i] = bulkhead.isSealed ? (byte)1 : (byte)0;
                    _bulkheadDoorAreas[i] = math.max(Epsilon, bulkhead.doorAreaSquareMeters > Epsilon ? bulkhead.doorAreaSquareMeters : DefaultBulkheadDoorAreaSquareMeters);
                }
            }
            else
            {
                _configuredBulkheadCount = BulkheadCapacity;
                for (int i = 0; i < BulkheadCapacity; i++)
                {
                    _bulkheadPairs[i] = new int2(i, i + 1);
                    _bulkheadSealed[i] = 0;
                    _bulkheadDoorAreas[i] = DefaultBulkheadDoorAreaSquareMeters;
                }
            }

            for (int i = _configuredBulkheadCount; i < BulkheadCapacity; i++)
            {
                _bulkheadPairs[i] = new int2(-1, -1);
                _bulkheadSealed[i] = 0;
                _bulkheadDoorAreas[i] = DefaultBulkheadDoorAreaSquareMeters;
            }

            for (int i = 0; i < RingBufferLength; i++)
                _angularVelocityHistoryLocal[i] = float3.zero;

            ResetSplashDetectionState(clearQueuedEvents: true);

            for (int i = 0; i < CompartmentCapacity; i++)
            {
                if (_jobFloodVolumes.IsCreated)
                    _jobFloodVolumes[i] = _compartmentFloodVolumes[i];

                if (_jobCompartmentFlags.IsCreated)
                    _jobCompartmentFlags[i] = _compartmentFlags[i];
            }

            _ringHead = 0;
            _dynamicCompressionScale = 1f;
            _fluidJobHandle = default;
            _fluidJobRunning = false;
            _massPropertiesJobHandle = default;
            _massPropertiesJobRunning = false;
            _hydroKinematicJobHandle = default;
            _hydroKinematicJobRunning = false;
            _hydroKinematicOutputReady = false;
            _hydroBlackBoxCursor = 0;
            _hydroBlackBoxDumped = false;
            Vector3 safeDryCenter = SanitizeCenterOfMass(dryCenterOfMassLocal, Vector3.zero);
            _reportedFloodCenterOfMassLocal = safeDryCenter;
            _appliedCenterOfMassLocal = safeDryCenter;
            _currentFloodCenterOfMassLocal = _appliedCenterOfMassLocal;
            _lastAppliedInertiaTensor = _resolvedDryInertiaTensor;
            _externalSubmergedVolumeCubicMeters = 0f;
            _submersionFactor = 0f;
            _targetBuoyancyBias01 = 0f;
            _ballastBlowTimer = 0f;
            _towingTensionHoldTimer = 0f;
            _pendingTowingTensionVector = Vector3.zero;
            _lastExternalBuoyancyForce = Vector3.zero;
            _lastExternalBuoyancyTorque = Vector3.zero;

            FinalizeCompartmentState();
            float3 targetFloodCenter = ResolveFloodTargetCenterOfMassLocal();
            ApplyStartupMassProperties(targetFloodCenter);
            SeedFloodMassPropertiesBuffers(targetFloodCenter, _floodFillRatio);
            UpdateReportedFloodCenter(targetFloodCenter);
        }

        private void DisposeNativeStateDeferred()
        {
            if (_fluidJobRunning)
            {
                _disposeHandle = JobHandle.CombineDependencies(_disposeHandle, _fluidJobHandle);
                _fluidJobHandle = default;
                _fluidJobRunning = false;
            }

            if (_massPropertiesJobRunning)
            {
                _disposeHandle = JobHandle.CombineDependencies(_disposeHandle, _massPropertiesJobHandle);
                _massPropertiesJobHandle = default;
                _massPropertiesJobRunning = false;
            }

            if (_hydroKinematicJobRunning)
            {
                _disposeHandle = JobHandle.CombineDependencies(_disposeHandle, _hydroKinematicJobHandle);
                _hydroKinematicJobHandle = default;
                _hydroKinematicJobRunning = false;
                _hydroKinematicOutputReady = false;
            }

            DisposeNativeStateBuffer(ref _compartmentFloodVolumes, VaultCompartmentFloodVolumesFlag);
            DisposeNativeStateBuffer(ref _compartmentViscosity01, VaultCompartmentViscosityFlag);
            DisposeNativeStateBuffer(ref _compartmentBaseMaxVolumes, VaultCompartmentBaseMaxVolumesFlag);
            DisposeNativeStateBuffer(ref _compartmentMaxVolumes, VaultCompartmentMaxVolumesFlag);
            DisposeNativeStateBuffer(ref _compartmentBreachAreas, VaultCompartmentBreachAreasFlag);
            DisposeNativeStateBuffer(ref _compartmentLocalCentroids, VaultCompartmentLocalCentroidsFlag);
            DisposeNativeStateBuffer(ref _compartmentFlags, VaultCompartmentFlagsFlag);
            DisposeNativeStateBuffer(ref _bulkheadPairs, VaultBulkheadPairsFlag);
            DisposeNativeStateBuffer(ref _bulkheadSealed, VaultBulkheadSealedFlag);
            DisposeNativeStateBuffer(ref _bulkheadDoorAreas, VaultBulkheadDoorAreasFlag);
            DisposeNativeStateBuffer(ref _comAccumulatorFront, VaultComAccumulatorFrontFlag);
            DisposeNativeStateBuffer(ref _comAccumulatorBack, VaultComAccumulatorBackFlag);
            DisposeNativeStateBuffer(ref _massPropertiesFront, VaultMassPropertiesFrontFlag);
            DisposeNativeStateBuffer(ref _massPropertiesBack, VaultMassPropertiesBackFlag);
            DisposeNativeStateBuffer(ref _angularVelocityHistoryLocal, VaultAngularVelocityHistoryFlag);
            DisposeNativeStateBuffer(ref _previousExteriorSampleSubmersionFactors, VaultExteriorSubmersionHistoryFlag);
            DisposeNativeStateBuffer(ref _exteriorBuoyancySampleLocalPoints, VaultExteriorBuoyancySamplesFlag);
            DisposeNativeStateBuffer(ref _compartmentStates, VaultCompartmentStatesFlag);
            DisposeNativeStateBuffer(ref _jobFloodVolumes, VaultJobFloodVolumesFlag);
            DisposeNativeStateBuffer(ref _jobCompartmentFlags, VaultJobCompartmentFlagsFlag);
            DisposeNativeStateBuffer(ref _bulkheadTransferDeltas, VaultBulkheadTransferDeltasFlag);
            DisposeNativeStateBuffer(ref _hydroKinematicInput, VaultHydroInputFlag);
            DisposeNativeStateBuffer(ref _hydroKinematicOutput, VaultHydroOutputFlag);
            DisposeNativeStateBuffer(ref _hydroBlackBox, VaultHydroBlackBoxFlag);
            DisposeNativeStateBuffer(ref _exteriorThermalAnomalyCenters, VaultExteriorThermalCentersFlag);
            DisposeNativeStateBuffer(ref _exteriorThermalAnomalyTemperatures, VaultExteriorThermalTemperaturesFlag);
            DisposeNativeStateBuffer(ref _exteriorThermalAnomalyLifetimes, VaultExteriorThermalLifetimesFlag);
            DisposeNativeStateBuffer(ref _exteriorThermalHazardIds, VaultExteriorThermalHazardIdsFlag);
            ClearNativeStateViews();
            DispatcherJobSwap.TryComplete(ref _disposeHandle, true);

        }

        private void RestoreRigidbodyDynamics()
        {
            if (_rigidbody == null)
                return;

            Vector3 safeDryCenter = SanitizeCenterOfMass(dryCenterOfMassLocal, Vector3.zero);
            Vector3 safeDryTensor = SanitizeTensor(_resolvedDryInertiaTensor);
            float safeLinearDamping = 0f;
            float safeAngularDamping = 0f;
            _rigidbody.centerOfMass = safeDryCenter;
            _rigidbody.inertiaTensor = safeDryTensor;
            _rigidbody.mass = math.max(_dryRigidbodyMass, Epsilon);
            _lastAppliedRigidbodyMass = _rigidbody.mass;
            _rigidbody.linearDamping = safeLinearDamping;
            _rigidbody.angularDamping = safeAngularDamping;
            _lastAppliedLinearDamping = safeLinearDamping;
            _lastAppliedAngularDamping = safeAngularDamping;
            _externalSubmergedVolumeCubicMeters = 0f;
            _submersionFactor = 0f;
            _currentFloraDragDensity01 = 0f;
            _currentFloraAddedMassKilograms = 0f;
            _hullImplosionActive = false;
            _targetBuoyancyBias01 = 0f;
            _ballastBlowTimer = 0f;
            _pendingTowingTensionVector = Vector3.zero;
            _towingTensionHoldTimer = 0f;
            _lastExternalBuoyancyForce = Vector3.zero;
            _lastExternalBuoyancyTorque = Vector3.zero;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Environment);
            GlobalRegistry.RegisterPostFixedTickable(this, PriorityLayer.Environment);
            _registered = SystemDispatcher.GetPostFixedLane(PriorityLayer.Environment).Contains(this);
        }

        private void TryRegisterFluidSimulationService()
        {
            if (_fluidSimulationRegistered || _fluidMathCore == null || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterFluidSimulationService(_fluidMathCore);
            _fluidSimulationRegistered = true;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryRegisterOriginShiftListener()
        {
            if (_registeredOriginShiftListener || !Application.isPlaying)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        private void TryRegisterScalabilityListener()
        {
            if (_registeredScalabilityListener || !Application.isPlaying)
                return;

            _cachedFloodStateMathLod = DistanceMath.IsHighQualityTier(GlobalRegistry.ScalabilityTier) ? (byte)1 : (byte)0;
            ScalabilityEvents.Register(this);
            _registeredScalabilityListener = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private void TryUnregisterFluidSimulationService()
        {
            if (!_fluidSimulationRegistered || _fluidMathCore == null)
                return;

            GlobalRegistry.UnregisterFluidSimulationService(_fluidMathCore);
            _fluidSimulationRegistered = false;
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.UnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private void TryUnregisterOriginShiftListener()
        {
            if (!_registeredOriginShiftListener)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _registeredOriginShiftListener = false;
        }

        private void TryUnregisterScalabilityListener()
        {
            if (!_registeredScalabilityListener)
                return;

            ScalabilityEvents.Unregister(this);
            _registeredScalabilityListener = false;
        }

        private void ResetSloshHistoryForOriginShift()
        {
            if (_angularVelocityHistoryLocal.IsCreated)
            {
                for (int i = 0; i < RingBufferLength; i++)
                    _angularVelocityHistoryLocal[i] = float3.zero;
            }

            _ringHead = 0;
            _debugDelayedSloshAngularVelocityLocal = Vector3.zero;
            _lastSloshTorqueLocal = Vector3.zero;
            _debugLastSloshTorqueLocal = Vector3.zero;
        }

        private void ResetSplashDetectionState(bool clearQueuedEvents)
        {
            if (_previousExteriorSampleSubmersionFactors.IsCreated)
            {
                for (int i = 0; i < _previousExteriorSampleSubmersionFactors.Length; i++)
                    _previousExteriorSampleSubmersionFactors[i] = 0f;
            }

        }

        private void CompletePendingFluidTransferForAuthoritativeWrite()
        {
            if (!_fluidJobRunning)
                return;

            // COLD SYNC JOB: authoritative state writes must not race a pending fluid transfer.
            DispatcherJobSwap.TryComplete(ref _fluidJobHandle, true);
            ApplyCompletedFluidTransfer();
        }

        private void CompletePendingFloodMassPropertiesForAuthoritativeWrite()
        {
            if (!_massPropertiesJobRunning)
                return;

            // COLD SYNC JOB: authoritative compartment writes must not race a pending flood mass-properties job.
            DispatcherJobSwap.TryComplete(ref _massPropertiesJobHandle, true);
            ApplyCompletedFloodMassProperties();
        }

        private void CompleteFluidTransferInPostFixedSwapWindow()
        {
            if (!_fluidJobRunning || !_jobFloodVolumes.IsCreated || !_jobCompartmentFlags.IsCreated)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _fluidJobHandle, false))
                return;

            ApplyCompletedFluidTransfer();
        }

        private void ApplyCompletedFluidTransfer()
        {
            if (!_jobFloodVolumes.IsCreated || !_jobCompartmentFlags.IsCreated)
                return;

            _fluidJobHandle = default;
            _fluidJobRunning = false;

            SwapNativeStateBuffers(ref _compartmentFloodVolumes, ref _jobFloodVolumes);
            SwapNativeStateBuffers(ref _compartmentFlags, ref _jobCompartmentFlags);
        }

        private void CompleteFloodMassPropertiesInPostFixedSwapWindow()
        {
            if (!_massPropertiesJobRunning || !_massPropertiesBack.IsCreated)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _massPropertiesJobHandle, false))
                return;

            ApplyCompletedFloodMassProperties();
        }

        private void ApplyCompletedFloodMassProperties()
        {
            if (!_massPropertiesBack.IsCreated)
                return;

            _massPropertiesJobHandle = default;
            _massPropertiesJobRunning = false;

            SwapNativeStateBuffers(ref _massPropertiesFront, ref _massPropertiesBack);
            SwapNativeStateBuffers(ref _comAccumulatorFront, ref _comAccumulatorBack);

            PublishSubmarineRoomMassDataVault();
            PublishSubmarineFloodStateSignal();
        }

        private void PublishSubmarineRoomMassDataVault()
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                !_compartmentFloodVolumes.IsCreated ||
                !_compartmentMaxVolumes.IsCreated ||
                !_compartmentLocalCentroids.IsCreated)
            {
                return;
            }

            int activeCount = math.min(
                math.max(0, _configuredCompartmentCount),
                math.min(_compartmentFloodVolumes.Length, math.min(_compartmentMaxVolumes.Length, _compartmentLocalCentroids.Length)));

            const int sharedRoomCount = CompartmentCapacity;
            NativeArray<float> roomWaterLevels;
            NativeArray<float> roomVolumes;
            NativeArray<float3> roomLocalAups;
            bool hasRoomWaterLevels = vault.TryGetBuffer(BufferID.RoomWaterLevels, out roomWaterLevels);
            bool hasRoomVolumes = vault.TryGetBuffer(BufferID.RoomVolumes, out roomVolumes);
            bool hasRoomLocalAups = vault.TryGetBuffer(BufferID.RoomLocalAUPs, out roomLocalAups);
            bool hasPartialRoomSoa = hasRoomWaterLevels || hasRoomVolumes || hasRoomLocalAups;
            if (activeCount <= 0 && !hasPartialRoomSoa)
                return;

            if (vault.IsAllocationLocked || hasPartialRoomSoa)
            {
                if (!hasRoomWaterLevels || !hasRoomVolumes || !hasRoomLocalAups)
                    return;
            }
            else
            {
                roomWaterLevels = vault.GetBuffer<float>(
                    BufferID.RoomWaterLevels,
                    sharedRoomCount,
                    SystemID.VehiclesPhysics,
                    NativeArrayOptions.UninitializedMemory);
                roomVolumes = vault.GetBuffer<float>(
                    BufferID.RoomVolumes,
                    sharedRoomCount,
                    SystemID.VehiclesPhysics,
                    NativeArrayOptions.UninitializedMemory);
                roomLocalAups = vault.GetBuffer<float3>(
                    BufferID.RoomLocalAUPs,
                    sharedRoomCount,
                    SystemID.VehiclesPhysics,
                    NativeArrayOptions.UninitializedMemory);
            }

            if (!roomWaterLevels.IsCreated ||
                !roomVolumes.IsCreated ||
                !roomLocalAups.IsCreated ||
                roomWaterLevels.Length < sharedRoomCount ||
                roomVolumes.Length < sharedRoomCount ||
                roomLocalAups.Length < sharedRoomCount)
            {
                return;
            }

            for (int i = 0; i < sharedRoomCount; i++)
            {
                bool active = i < activeCount;
                float maxVolume = active ? math.max(0f, _compartmentMaxVolumes[i]) : 0f;
                float currentVolume = active ? math.clamp(_compartmentFloodVolumes[i], 0f, maxVolume) : 0f;
                float fill01 = maxVolume > Epsilon
                    ? math.saturate(currentVolume * math.rcp(maxVolume))
                    : 0f;

                roomWaterLevels[i] = math.isfinite(fill01) ? fill01 : 0f;
                roomVolumes[i] = math.isfinite(maxVolume) ? maxVolume : 0f;
                roomLocalAups[i] = active && math.all(math.isfinite(_compartmentLocalCentroids[i]))
                    ? _compartmentLocalCentroids[i]
                    : float3.zero;
            }
        }

        private void PublishSubmarineFloodStateSignal()
        {
            if (_rigidbody == null || !_massPropertiesFront.IsCreated || _massPropertiesFront.Length == 0)
                return;

            FloodMassPropertiesResult result = _massPropertiesFront[0];
            float3 dryCenter = new float3(dryCenterOfMassLocal.x, dryCenterOfMassLocal.y, dryCenterOfMassLocal.z);
            float3 appliedCenter = new float3(_appliedCenterOfMassLocal.x, _appliedCenterOfMassLocal.y, _appliedCenterOfMassLocal.z);
            float3 targetCenter = math.all(math.isfinite(result.TargetCenterLocal))
                ? result.TargetCenterLocal
                : dryCenter;
            float3 dynamicCenter = math.all(math.isfinite(appliedCenter))
                ? appliedCenter
                : targetCenter;
            float3 centerOffset = dynamicCenter - dryCenter;
            float baseMass = math.max(MinimumMassForReciprocal, math.isfinite(_dryRigidbodyMass) ? _dryRigidbodyMass : _rigidbody.mass);
            float waterMass = math.max(0f, result.FloodMassKilograms) + math.max(0f, _damageControlLeakAddedMassKilograms);
            float angularDragMultiplier = 1f + (waterMass * math.rcp(math.max(MinimumMassForReciprocal, baseMass)));
            byte flags = 0;

            if (waterMass > Epsilon)
                flags |= SubmarineFloodStateSignal.FlagHasFloodMass;

            if (waterMass > baseMass * 0.4f)
                flags |= SubmarineFloodStateSignal.FlagCriticalFlood;

            if (!math.all(math.isfinite(dynamicCenter)) ||
                !math.all(math.isfinite(centerOffset)) ||
                !math.isfinite(waterMass) ||
                !math.isfinite(baseMass) ||
                !math.isfinite(angularDragMultiplier))
            {
                flags |= SubmarineFloodStateSignal.FlagInvalid;
                dynamicCenter = dryCenter;
                centerOffset = float3.zero;
                waterMass = 0f;
                angularDragMultiplier = 1f;
            }

            SubmarineFloodStateSignal signal = default;
            signal.DynamicCenterOfMassLocal = dynamicCenter;
            signal.DynamicCenterOfMassOffsetLocal = centerOffset;
            signal.TotalWaterMassKg = waterMass;
            signal.BaseMassKg = baseMass;
            signal.FillRatio01 = math.saturate(math.isfinite(result.FloodMassRatio) ? result.FloodMassRatio : _floodFillRatio);
            signal.AngularDragMultiplier = math.max(1f, angularDragMultiplier);
            signal.SourceBodyId = _rigidbody != null ? unchecked((uint)EntityId.ToULong(_rigidbody.GetEntityId())) : 0u;
            signal.Frame = unchecked((uint)Time.frameCount);
            signal.RoomCount = (ushort)math.min(ushort.MaxValue, math.max(0, _configuredCompartmentCount));
            signal.MathLod = ResolveFloodStateMathLod();
            signal.Flags = flags;
            SignalBus<SubmarineFloodStateSignal>.Push(in signal);
        }

        private void CompleteHydroKinematicJobInPostFixedSwapWindow()
        {
            if (!_hydroKinematicJobRunning || !_hydroKinematicOutput.IsCreated)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _hydroKinematicJobHandle, false))
                return;

            _hydroKinematicJobHandle = default;
            _hydroKinematicJobRunning = false;
            _hydroKinematicOutputReady = true;
        }

        private void ApplyCompletedHydroKinematicOutput()
        {
            if (!_hydroKinematicOutputReady || !_hydroKinematicOutput.IsCreated || _rigidbody == null)
                return;

            HydroKinematicJobOutput output = _hydroKinematicOutput[0];
            Vector3 acceleration = new Vector3(output.DragAcceleration.x, output.DragAcceleration.y, output.DragAcceleration.z);
            Vector3 torque = new Vector3(output.Torque.x, output.Torque.y, output.Torque.z);

            bool invalidOutput = false;
            if (!IsFiniteVector(acceleration))
            {
                acceleration = Vector3.zero;
                invalidOutput = true;
            }

            if (!IsFiniteVector(torque))
            {
                torque = Vector3.zero;
                invalidOutput = true;
            }

            if (invalidOutput)
                DumpHydroBlackBoxOnce(HydroBlackBoxFlagInvalidOutput);

            if (acceleration.sqrMagnitude > Epsilon)
                PhysicsForceRouter.QueueAmbientForce(_rigidbody, acceleration, ForceMode.Acceleration);

            if (torque.sqrMagnitude > Epsilon)
                PhysicsForceRouter.QueueAmbientTorque(_rigidbody, torque, ForceMode.Force);

            _debugHydroDragAcceleration = acceleration;
            _debugHydroTorque = torque;
            _debugHydroForwardSpeed = output.ForwardSpeed;
            _debugHydroLateralSpeed = output.LateralSpeed;
            _debugHydroVerticalSpeed = output.VerticalSpeed;
            _hydroKinematicOutputReady = false;
        }

        private void ScheduleHydroKinematicJob(float fixedDeltaTime)
        {
            if (_hydroKinematicJobRunning ||
                !_hydroKinematicInput.IsCreated ||
                !_hydroKinematicOutput.IsCreated ||
                _rigidbody == null ||
                _cachedTransform == null)
            {
                return;
            }

            Vector3 velocity = _rigidbody.linearVelocity;
            Vector3 angularVelocity = _rigidbody.angularVelocity;
            if (!IsFiniteVector(velocity) || !IsFiniteVector(angularVelocity))
            {
                DumpHydroBlackBoxOnce(HydroBlackBoxFlagInvalidVelocity | HydroBlackBoxFlagEmergencyReset);
                EmergencyResetHydrodynamics();
                return;
            }

            float mass = math.isfinite(_rigidbody.mass) ? math.max(_rigidbody.mass, Epsilon) : Epsilon;
            float lateralCoefficient = math.max(0f, forwardHydroDragCoefficient) * math.max(1f, lateralHydroDragMultiplier);
            float3 linearDragTensor = math.all(math.isfinite(_externalFloodLinearDragTensor))
                ? math.max(new float3(0.1f), _externalFloodLinearDragTensor)
                : new float3(1f);
            float3 angularDragTensor = math.all(math.isfinite(_externalFloodAngularDragTensor))
                ? math.max(new float3(0.1f), _externalFloodAngularDragTensor)
                : new float3(1f);
            float3 velocityFloat = new float3(velocity.x, velocity.y, velocity.z);
            float3 localFlowVelocity = ResolveAnalyticalFlowVelocity(_rigidbody.worldCenterOfMass);
            _hydroKinematicInput[0] = new HydroKinematicJobInput
            {
                Velocity = velocityFloat,
                AngularVelocity = new float3(angularVelocity.x, angularVelocity.y, angularVelocity.z),
                Forward = new float3(_cachedTransform.forward.x, _cachedTransform.forward.y, _cachedTransform.forward.z),
                Right = new float3(_cachedTransform.right.x, _cachedTransform.right.y, _cachedTransform.right.z),
                Up = new float3(_cachedTransform.up.x, _cachedTransform.up.y, _cachedTransform.up.z),
                WorldUp = new float3(0f, 1f, 0f),
                TowingAcceleration = ResolveTowingAcceleration(mass),
                FlowVelocityWS = localFlowVelocity,
                MassKilograms = mass,
                AddedMassKilograms = 0f,
                WaterDensity = WaterDensityKgPerCubicMeter,
                SubmersionFactor = math.saturate(_submersionFactor),
                ForwardDragCoefficient = forwardHydroDragCoefficient * linearDragTensor.z,
                LateralDragCoefficient = lateralCoefficient * linearDragTensor.x,
                VerticalDragCoefficient = verticalHydroDragCoefficient * linearDragTensor.y,
                AngularDragCoefficient = angularHydroDragCoefficient,
                AngularDragTensorMultiplier = angularDragTensor,
                RightingTorqueCoefficient = pitchRollRightingTorqueCoefficient,
                BallastUpAcceleration = ResolveBallastUpAcceleration(),
                MaxAcceleration = hydroSolverMaxAcceleration,
                MaxTorque = hydroSolverMaxTorque
            };

            PublishCavitationRumbleIfNeeded(ApproximateMagnitude(velocityFloat), fixedDeltaTime);
            _hydroKinematicJobHandle = new HydroKinematicDragJob
            {
                Input = _hydroKinematicInput,
                Output = _hydroKinematicOutput
            }.Schedule();
            _hydroKinematicJobRunning = true;
        }

        private float3 ResolveAnalyticalFlowVelocity(Vector3 samplePosition)
        {
            if (!IsFiniteVector(samplePosition))
                return float3.zero;

            HectonFluidEngine fluidEngine = ResolveFluidRuntime();
            if (fluidEngine == null)
                return float3.zero;

            float3 flow = fluidEngine.GetFlowAtPosition(new float3(samplePosition.x, samplePosition.y, samplePosition.z));
            return math.all(math.isfinite(flow)) ? flow : float3.zero;
        }

        private void SeedCargoMassFromRegistryCold()
        {
            if (!syncCargoMassFromInventorySignals)
                return;

            float cachedMass = GlobalRegistry.PlayerInventoryMassKg;
            if (!math.isfinite(cachedMass))
                cachedMass = 0f;

            if (math.abs(cachedMass - _lastResolvedCargoMassKilograms) <= 0.05f &&
                math.abs(_cargoMassScalar - _lastResolvedCargoScalar) <= 0.0001f)
            {
                return;
            }

            CommitCargoMassScalar(cachedMass);
        }

        private void ConsumeInventoryMassSignals()
        {
            if (!syncCargoMassFromInventorySignals)
                return;

            ReadOnlySpan<InventoryChangedSignal> signals = SignalBus<InventoryChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                ref readonly InventoryChangedSignal signal = ref signals[i];
                if (signal.Revision != 0u &&
                    _lastInventoryMassSignalRevision != 0u &&
                    signal.Revision <= _lastInventoryMassSignalRevision)
                {
                    continue;
                }

                float massKg = math.isfinite(signal.TotalMassKg) ? math.max(0f, signal.TotalMassKg) : 0f;
                if (math.abs(massKg - _lastResolvedCargoMassKilograms) <= 0.05f &&
                    math.abs(_cargoMassScalar - _lastResolvedCargoScalar) <= 0.0001f)
                {
                    _lastInventoryMassSignalRevision = signal.Revision;
                    continue;
                }

                CommitCargoMassScalar(massKg);
                _lastInventoryMassSignalRevision = signal.Revision;
            }
        }

        private void CommitCargoMassScalar(float massKg)
        {
            SetCargoMassScalar(massKg);
            _lastResolvedCargoMassKilograms = _totalCargoMassKilograms;
            _lastResolvedCargoScalar = _cargoMassScalar;
        }

        private void UpdateHydroRuntimeState(float fixedDeltaTime)
        {
            float safeDeltaTime = math.max(0f, math.isfinite(fixedDeltaTime) ? fixedDeltaTime : 0f);
            if (_ballastBlowTimer > 0f)
            {
                _ballastBlowTimer = math.max(0f, _ballastBlowTimer - safeDeltaTime);
                float duration = math.max(0.05f, ballastBlowDurationSeconds);
                _targetBuoyancyBias01 = math.saturate(_ballastBlowTimer * math.rcp(duration));
            }
            else
            {
                _targetBuoyancyBias01 = 0f;
            }

            if (_cavitationCooldownTimer > 0f)
                _cavitationCooldownTimer = math.max(0f, _cavitationCooldownTimer - safeDeltaTime);

            if (_towingTensionHoldTimer > 0f)
            {
                _towingTensionHoldTimer = math.max(0f, _towingTensionHoldTimer - safeDeltaTime);
            }
            else
            {
                _pendingTowingTensionVector = Vector3.zero;
                _debugTowingTensionVector = Vector3.zero;
            }
        }

        private float3 ResolveTowingAcceleration(float mass)
        {
            if (!IsFiniteVector(_pendingTowingTensionVector) || _pendingTowingTensionVector.sqrMagnitude <= Epsilon)
                return float3.zero;

            float inverseMass = math.rcp(math.max(mass, Epsilon));
            Vector3 acceleration = _pendingTowingTensionVector * inverseMass;
            acceleration = ClampMagnitude(acceleration, math.max(0f, hydroSolverMaxAcceleration));
            _debugTowingTensionVector = _pendingTowingTensionVector;
            return new float3(acceleration.x, acceleration.y, acceleration.z);
        }

        private float ResolveBallastUpAcceleration()
        {
            return math.max(0f, ballastBlowUpAcceleration) * math.saturate(_targetBuoyancyBias01);
        }

        private void PublishCavitationRumbleIfNeeded(float speedMetersPerSecond, float fixedDeltaTime)
        {
            bool active = _thrustInput01 >= math.saturate(cavitationThrottleThreshold) &&
                          speedMetersPerSecond < math.max(0f, cavitationStallSpeedMetersPerSecond);
            _debugCavitationActive = active;
            if (!active || _cavitationCooldownTimer > 0f)
                return;

            float intensity = math.saturate(1f - (speedMetersPerSecond * math.rcp(math.max(0.01f, cavitationStallSpeedMetersPerSecond))));
            ToolHapticsRuntime.EnqueueSinusoidalCommand(
                intensity * 0.55f,
                intensity * 0.25f,
                0.18f,
                24f,
                ToolHapticsRuntime.PriorityCritical,
                0b0011);

            Vector3 source = _rigidbody != null ? _rigidbody.worldCenterOfMass : (_cachedTransform != null ? _cachedTransform.position : Vector3.zero);
            if (IsFiniteVector(source))
            {
                ProceduralAudioEvents.RaiseAudioPingTriggered(
                    source,
                    intensity,
                    0.11f,
                    1f,
                    3200f,
                    ProceduralAudioPingKind.MechanicalWhirr);
            }

            _cavitationCooldownTimer = math.max(cavitationCooldownSeconds, fixedDeltaTime);
        }

        private void ScheduleFluidTransferJob(float depthMeters, float fixedDeltaTime)
        {
            if (_fluidJobRunning || !_jobFloodVolumes.IsCreated || !_jobCompartmentFlags.IsCreated || !_bulkheadTransferDeltas.IsCreated)
                return;

            FluidTransferJob ingressJob = new FluidTransferJob
            {
                InputFloodVolumes = _compartmentFloodVolumes,
                MaxVolumes = _compartmentMaxVolumes,
                BreachAreas = _compartmentBreachAreas,
                InputFlags = _compartmentFlags,
                OutputFloodVolumes = _jobFloodVolumes,
                OutputFlags = _jobCompartmentFlags,
                CompartmentCount = _configuredCompartmentCount,
                DepthMeters = depthMeters,
                FixedDeltaTime = fixedDeltaTime,
                DischargeCoefficient = dischargeCoefficient,
                MaximumIngressPerSecondNormalized = maximumIngressPerSecondNormalized
            };

            BulkheadTransferDeltaJob deltaJob = new BulkheadTransferDeltaJob
            {
                FloodVolumes = _jobFloodVolumes,
                MaxVolumes = _compartmentMaxVolumes,
                BulkheadPairs = _bulkheadPairs,
                BulkheadSealed = _bulkheadSealed,
                BulkheadDoorAreas = _bulkheadDoorAreas,
                TransferDeltas = _bulkheadTransferDeltas,
                CompartmentCount = _configuredCompartmentCount,
                FixedDeltaTime = fixedDeltaTime,
                BulkheadFlowCoefficient = bulkheadFlowCoefficient,
                MaxTransferPerTick = maxTransferPerTick,
                DischargeCoefficient = dischargeCoefficient,
                NearZeroHeadDampingMeters = nearZeroHeadDampingMeters
            };

            ApplyBulkheadTransferJob applyJob = new ApplyBulkheadTransferJob
            {
                BulkheadPairs = _bulkheadPairs,
                BulkheadSealed = _bulkheadSealed,
                MaxVolumes = _compartmentMaxVolumes,
                TransferDeltas = _bulkheadTransferDeltas,
                FloodVolumes = _jobFloodVolumes,
                Flags = _jobCompartmentFlags,
                BulkheadCount = _configuredBulkheadCount,
                CompartmentCount = _configuredCompartmentCount
            };

            JobHandle ingressHandle = ingressJob.Schedule();
            JobHandle deltaHandle = deltaJob.Schedule(_configuredBulkheadCount, 1, ingressHandle);
            _fluidJobHandle = applyJob.Schedule(deltaHandle);
            _fluidJobRunning = true;
        }

        private void ScheduleFloodMassPropertiesJob()
        {
            if (_massPropertiesJobRunning || !_massPropertiesBack.IsCreated || !_comAccumulatorBack.IsCreated)
                return;

            Vector3 safeDryCenter = SanitizeCenterOfMass(dryCenterOfMassLocal, Vector3.zero);
            FloodMassPropertiesJob job = new FloodMassPropertiesJob
            {
                FloodVolumes = _compartmentFloodVolumes,
                MaxVolumes = _compartmentMaxVolumes,
                LocalCentroids = _compartmentLocalCentroids,
                Flags = _compartmentFlags,
                WeightedCentroids = _comAccumulatorBack,
                Output = _massPropertiesBack,
                CompartmentCount = _configuredCompartmentCount,
                DryCenterLocal = new float3(safeDryCenter.x, safeDryCenter.y, safeDryCenter.z),
                DryInertiaTensor = new float3(_resolvedDryInertiaTensor.x, _resolvedDryInertiaTensor.y, _resolvedDryInertiaTensor.z),
                FloodedInertiaTensor = new float3(_resolvedFloodedInertiaTensor.x, _resolvedFloodedInertiaTensor.y, _resolvedFloodedInertiaTensor.z)
            };

            _massPropertiesJobHandle = job.Schedule();
            _massPropertiesJobRunning = true;
        }

        private void ClearTransientFlags()
        {
            for (int i = 0; i < CompartmentCapacity; i++)
                _compartmentFlags[i] &= PersistentFlagsMask;
        }

        private void SyncBulkheadSealedFlags()
        {
            for (int i = 0; i < CompartmentCapacity; i++)
                _compartmentFlags[i] &= ~FlagSealed;

            for (int i = 0; i < _configuredBulkheadCount; i++)
            {
                int2 pair = _bulkheadPairs[i];
                if (!IsCompartmentIndexValid(pair.x) || !IsCompartmentIndexValid(pair.y))
                    continue;

                if (_bulkheadSealed[i] == 0)
                    continue;

                _compartmentFlags[pair.x] |= FlagSealed;
                _compartmentFlags[pair.y] |= FlagSealed;
            }
        }

        private void SyncStructuralBreachIngress()
        {
            if (_structuralBreachReadModel == null || !_structuralBreachReadModel.IsReady || !_compartmentBreachAreas.IsCreated)
                return;

            for (int i = 0; i < CompartmentCapacity; i++)
            {
                float breachArea = i < _configuredCompartmentCount
                    ? math.max(0f, _structuralBreachReadModel.GetCompartmentBreachAreaSquareMeters(i))
                    : 0f;

                _compartmentBreachAreas[i] = breachArea;
                if (breachArea > Epsilon)
                    _compartmentFlags[i] |= FlagBreached;
                else
                    _compartmentFlags[i] &= ~FlagBreached;
            }
        }

        private void EvaluateHullImplosion(float depthMeters)
        {
            if (!_compartmentBreachAreas.IsCreated || !_compartmentFlags.IsCreated || _configuredCompartmentCount <= 0)
                return;

            if (_hullImplosionActive)
            {
                ApplyCatastrophicImplosionBreaches();
                return;
            }

            float safeDepthMeters = math.max(0f, depthMeters);
            if (safeDepthMeters < math.max(0f, hullImplosionDepthThresholdMeters))
                return;

            float externalPressureKPa = ResolveExternalPressureKPa(safeDepthMeters);
            if (!math.isfinite(externalPressureKPa) || externalPressureKPa <= math.max(1f, hullPressureRatingKPa))
                return;

            _hullImplosionActive = true;
            ApplyCatastrophicImplosionBreaches();
        }

        private void ApplyCatastrophicImplosionBreaches()
        {
            float totalCapacity = ResolveTotalCapacityCubicMeters();
            if (!math.isfinite(totalCapacity) || totalCapacity <= Epsilon)
                return;

            float estimatedHullAreaSquareMeters = ResolveEstimatedHullSurfaceAreaSquareMeters();
            float clampedBreachAreaSquareMeters = math.max(0f, estimatedHullAreaSquareMeters) *
                math.saturate(hullImplosionBreachAreaNormalized);
            if (!math.isfinite(clampedBreachAreaSquareMeters) || clampedBreachAreaSquareMeters <= Epsilon)
                return;

            for (int i = 0; i < CompartmentCapacity; i++)
            {
                if (i >= _configuredCompartmentCount)
                {
                    if (_compartmentBreachAreas.IsCreated && i < _compartmentBreachAreas.Length)
                        _compartmentBreachAreas[i] = 0f;

                    if (_compartmentFlags.IsCreated && i < _compartmentFlags.Length)
                        _compartmentFlags[i] &= ~FlagBreached;

                    continue;
                }

                float compartmentCapacity = math.max(0f, _compartmentMaxVolumes[i]);
                float compartmentWeight = compartmentCapacity > Epsilon
                    ? compartmentCapacity * math.rcp(totalCapacity)
                    : 0f;
                float catastrophicBreachArea = clampedBreachAreaSquareMeters * compartmentWeight;
                _compartmentBreachAreas[i] = catastrophicBreachArea;
                _compartmentFlags[i] |= FlagBreached;
            }
        }

        private void ApplyDynamicCompressionToCompartments()
        {
            if (!_compartmentMaxVolumes.IsCreated || !_compartmentBaseMaxVolumes.IsCreated)
                return;

            float minimumCompressionScale = 1f - math.saturate(maximumCompressionNormalized);
            float appliedCompressionScale = math.clamp(_dynamicCompressionScale, minimumCompressionScale, 1f);
            _debugCompressionScale = appliedCompressionScale;

            for (int i = 0; i < CompartmentCapacity; i++)
            {
                float authoredCapacity = _compartmentBaseMaxVolumes[i];
                _compartmentMaxVolumes[i] = authoredCapacity > Epsilon
                    ? authoredCapacity * appliedCompressionScale
                    : 0f;
            }
        }

        private void ApplyIceExpansionPhaseChange()
        {
            if (_atmosphereSystem == null || !_compartmentFloodVolumes.IsCreated || !_compartmentMaxVolumes.IsCreated || _configuredCompartmentCount <= 0)
                return;

            float externalDepthMeters = math.max(0f, _externalDepthMeters);
            if (externalDepthMeters < math.max(0f, deepFreezeDepthThresholdMeters))
            {
                ClearIceExpansionFlagsIfWarmed();
                return;
            }

            float supplyRatio = ResolveAggregatePowerSupplyRatio();
            if (supplyRatio >= math.saturate(deepFreezeSupplyRatioThreshold))
            {
                ClearIceExpansionFlagsIfWarmed();
                return;
            }

            for (int compartmentIndex = 0; compartmentIndex < _configuredCompartmentCount; compartmentIndex++)
            {
                float currentVolume = _compartmentFloodVolumes[compartmentIndex];
                if (currentVolume <= Epsilon)
                {
                    _compartmentFlags[compartmentIndex] &= ~FlagIceExpanded;
                    continue;
                }

                float roomTemperature = _atmosphereSystem.GetRoomTemperatureCelsius(compartmentIndex);
                if (roomTemperature >= 0f)
                {
                    if ((_compartmentFlags[compartmentIndex] & FlagIceExpanded) != 0u)
                    {
                        _compartmentFloodVolumes[compartmentIndex] = math.max(0f, currentVolume * math.rcp(IceExpansionVolumeScale));
                        _compartmentFlags[compartmentIndex] &= ~FlagIceExpanded;
                    }

                    continue;
                }

                if ((_compartmentFlags[compartmentIndex] & FlagIceExpanded) != 0u)
                    continue;

                float expandedVolume = currentVolume * IceExpansionVolumeScale;
                if (!math.isfinite(expandedVolume))
                    expandedVolume = currentVolume;

                _compartmentFloodVolumes[compartmentIndex] = expandedVolume;
                _compartmentFlags[compartmentIndex] |= FlagIceExpanded;

                float compressedCapacity = math.max(0f, _compartmentMaxVolumes[compartmentIndex]);
                if (expandedVolume <= compressedCapacity + Epsilon)
                    continue;

                TriggerInternalIceRupture(compartmentIndex, expandedVolume - compressedCapacity);
            }
        }

        private void ClearIceExpansionFlagsIfWarmed()
        {
            if (_atmosphereSystem == null || !_compartmentFlags.IsCreated || !_compartmentFloodVolumes.IsCreated)
                return;

            for (int compartmentIndex = 0; compartmentIndex < _configuredCompartmentCount; compartmentIndex++)
            {
                if ((_compartmentFlags[compartmentIndex] & FlagIceExpanded) == 0u)
                    continue;

                if (_atmosphereSystem.GetRoomTemperatureCelsius(compartmentIndex) < 0f)
                    continue;

                _compartmentFloodVolumes[compartmentIndex] = math.max(0f, _compartmentFloodVolumes[compartmentIndex] * math.rcp(IceExpansionVolumeScale));
                _compartmentFlags[compartmentIndex] &= ~FlagIceExpanded;
            }
        }

        private void ApplyHydraulicLeakViscosity(float fixedDeltaTime)
        {
            if (!_compartmentViscosity01.IsCreated || !_compartmentFloodVolumes.IsCreated || _configuredCompartmentCount <= EngineCompartmentIndex)
                return;

            for (int compartmentIndex = 0; compartmentIndex < _configuredCompartmentCount; compartmentIndex++)
                _compartmentViscosity01[compartmentIndex] = compartmentIndex == EngineCompartmentIndex
                    ? _compartmentViscosity01[compartmentIndex]
                    : math.max(0f, _compartmentViscosity01[compartmentIndex] - (fixedDeltaTime * 0.02f));

            uint engineFlags = _compartmentFlags[EngineCompartmentIndex];
            float engineVolume = _compartmentFloodVolumes[EngineCompartmentIndex];
            bool leakingHydraulicFluid = (engineFlags & (FlagBreached | FlagRuptured)) != 0u && engineVolume > Epsilon;
            if (!leakingHydraulicFluid)
                return;

            float viscosityGain = math.max(0f, hydraulicLeakRateCubicMetersPerSecond) * math.max(0f, fixedDeltaTime);
            if (viscosityGain <= 0f)
                return;

            _compartmentViscosity01[EngineCompartmentIndex] = math.saturate(
                math.min(
                    math.max(0f, maximumHydraulicViscosity),
                    _compartmentViscosity01[EngineCompartmentIndex] + viscosityGain));
        }

        private void TriggerInternalIceRupture(int compartmentIndex, float overflowVolume)
        {
            if (!IsCompartmentIndexValid(compartmentIndex))
                return;

            float compressedCapacity = math.max(Epsilon, _compartmentMaxVolumes[compartmentIndex]);
            float authoredCapacity = _compartmentBaseMaxVolumes.IsCreated
                ? math.max(compressedCapacity, _compartmentBaseMaxVolumes[compartmentIndex])
                : compressedCapacity;
            float ruptureArea = math.max(
                _compartmentBreachAreas[compartmentIndex],
                math.max(Epsilon, authoredCapacity * 0.08f));

            _compartmentBreachAreas[compartmentIndex] = ruptureArea;
            _compartmentFlags[compartmentIndex] |= FlagBreached | FlagRuptured | FlagOverflow;
            PropagateRuptureToConnectedPipes(compartmentIndex);
        }

        private void ApplySludgePlayerDrag()
        {
            if (_cachedPlayerMovement == null || _cachedPlayerTransform == null || !_compartmentViscosity01.IsCreated || !_compartmentFloodVolumes.IsCreated)
                return;

            Vector3 playerLocalPosition = _cachedTransform != null
                ? _cachedTransform.InverseTransformPoint(_cachedPlayerTransform.position)
                : _cachedPlayerTransform.position;
            int compartmentIndex = ResolveNearestCompartmentIndex(playerLocalPosition);
            if (compartmentIndex < 0)
                return;

            float viscosity01 = math.saturate(_compartmentViscosity01[compartmentIndex]);
            if (viscosity01 <= Epsilon)
                return;

            float maxVolume = math.max(Epsilon, _compartmentMaxVolumes[compartmentIndex]);
            float fillRatio = math.saturate(_compartmentFloodVolumes[compartmentIndex] * math.rcp(maxVolume));
            if (fillRatio <= 0.1f)
                return;

            float dragMultiplier = 1f + (viscosity01 * math.max(0f, sludgePlayerDragMultiplier) * fillRatio);
            _cachedPlayerMovement.ApplyEnvironmentalDrag(dragMultiplier);
        }

        private void ApplyOverflowSpillover()
        {
            if (!_compartmentFloodVolumes.IsCreated || !_compartmentMaxVolumes.IsCreated || !_bulkheadPairs.IsCreated)
                return;

            for (int bulkheadIndex = 0; bulkheadIndex < _configuredBulkheadCount; bulkheadIndex++)
            {
                if (_bulkheadSealed[bulkheadIndex] != 0)
                    continue;

                int2 pair = _bulkheadPairs[bulkheadIndex];
                SpillOverflowIntoAdjacentCompartment(pair.x, pair.y);
                SpillOverflowIntoAdjacentCompartment(pair.y, pair.x);
            }
        }

        private void SpillOverflowIntoAdjacentCompartment(int sourceIndex, int destinationIndex)
        {
            if (!IsCompartmentIndexValid(sourceIndex) || !IsCompartmentIndexValid(destinationIndex))
                return;

            float sourceMaxVolume = math.max(0f, _compartmentMaxVolumes[sourceIndex]);
            float destinationMaxVolume = math.max(0f, _compartmentMaxVolumes[destinationIndex]);
            if (sourceMaxVolume <= Epsilon || destinationMaxVolume <= Epsilon)
                return;

            float overflowVolume = _compartmentFloodVolumes[sourceIndex] - sourceMaxVolume;
            if (overflowVolume <= Epsilon)
                return;

            float availableDestinationCapacity = destinationMaxVolume - _compartmentFloodVolumes[destinationIndex];
            if (availableDestinationCapacity <= Epsilon)
                return;

            float transferredVolume = math.min(overflowVolume, availableDestinationCapacity);
            _compartmentFloodVolumes[sourceIndex] -= transferredVolume;
            _compartmentFloodVolumes[destinationIndex] += transferredVolume;
            _compartmentFlags[sourceIndex] |= FlagTransferSource | FlagOverflow;
            _compartmentFlags[destinationIndex] |= FlagTransferDestination;
        }

        private void PropagateRuptureToConnectedPipes(int compartmentIndex)
        {
            if (_atmosphereSystem == null)
                return;

            RefreshPipeBindingsCold();
            int pipeCount = _pipeBindingBuffer.Count;
            for (int pipeIndex = 0; pipeIndex < pipeCount; pipeIndex++)
            {
                LogisticsPipeNode pipe = _pipeBindingBuffer[pipeIndex];
                if (pipe == null)
                    continue;

                if (pipe.ResolveAmbientRoomIndex() != compartmentIndex)
                    continue;

                pipe.TriggerExternalRupture();
            }
        }

        private void RefreshPipeBindingsCold()
        {
            _pipeBindingBuffer.Clear();
            GetComponentsInChildren(includeInactive: true, result: _pipeBindingBuffer);
        }

        private float ResolveAggregatePowerSupplyRatio()
        {
            IPowerGridService powerGridService = ResolvePowerGridService();
            if (powerGridService == null)
                return 1f;

            float totalConsumption = math.max(0f, powerGridService.TotalConsumption);
            if (totalConsumption <= Epsilon)
                return 1f;

            float totalGeneration = math.max(0f, powerGridService.TotalGeneration);
            return math.saturate(totalGeneration * math.rcp(totalConsumption));
        }

        private int ResolveNearestCompartmentIndex(Vector3 localPosition)
        {
            if (!_compartmentLocalCentroids.IsCreated || _configuredCompartmentCount <= 0)
                return -1;

            int bestIndex = -1;
            float bestDistanceSq = float.MaxValue;
            float3 localPosition3 = new float3(localPosition.x, localPosition.y, localPosition.z);
            for (int compartmentIndex = 0; compartmentIndex < _configuredCompartmentCount; compartmentIndex++)
            {
                float3 centroid = _compartmentLocalCentroids[compartmentIndex];
                float distanceSq = math.lengthsq(localPosition3 - centroid);
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                bestIndex = compartmentIndex;
            }

            return bestIndex;
        }

        private void SimulateIngress(float depthMeters, float fixedDeltaTime)
        {
            float safeDepth = math.max(0f, depthMeters);
            float ingressVelocity = ApproximateSqrtPositive(2f * GravityMetersPerSecondSquared * safeDepth);
            if (!math.isfinite(ingressVelocity))
                ingressVelocity = 0f;

            float maxIngressScale = math.max(0.01f, maximumIngressPerSecondNormalized) * fixedDeltaTime;
            float cd = math.clamp(dischargeCoefficient, 0.05f, 1f);
            for (int i = 0; i < _configuredCompartmentCount; i++)
            {
                float maxVolume = _compartmentMaxVolumes[i];
                if (maxVolume <= Epsilon)
                    continue;

                float breachArea = _compartmentBreachAreas[i];
                if (breachArea <= Epsilon)
                {
                    _compartmentFlags[i] &= ~FlagBreached;
                    continue;
                }

                _compartmentFlags[i] |= FlagBreached;

                float remainingCapacity = maxVolume - _compartmentFloodVolumes[i];
                if (remainingCapacity <= Epsilon)
                    continue;

                float deltaVolume = ingressVelocity * breachArea * cd * fixedDeltaTime;
                if (!math.isfinite(deltaVolume))
                    deltaVolume = 0f;

                float maxIngressThisStep = maxVolume * maxIngressScale;
                deltaVolume = math.clamp(deltaVolume, 0f, math.min(remainingCapacity, maxIngressThisStep));
                _compartmentFloodVolumes[i] += deltaVolume;
            }
        }

        private void SimulateBulkheadTransfer(float fixedDeltaTime)
        {
            float transferCoefficient = math.max(0f, bulkheadFlowCoefficient);
            float perTickTransferCap = math.max(0.01f, maxTransferPerTick);
            float safeDischargeCoefficient = math.max(0f, dischargeCoefficient);
            for (int i = 0; i < _configuredBulkheadCount; i++)
            {
                if (_bulkheadSealed[i] != 0)
                    continue;

                int2 pair = _bulkheadPairs[i];
                int compartmentA = pair.x;
                int compartmentB = pair.y;
                if (!IsCompartmentIndexValid(compartmentA) || !IsCompartmentIndexValid(compartmentB))
                    continue;

                float maxVolumeA = _compartmentMaxVolumes[compartmentA];
                float maxVolumeB = _compartmentMaxVolumes[compartmentB];
                if (maxVolumeA <= Epsilon || maxVolumeB <= Epsilon)
                    continue;

                if (!TryResolveSafeNormalizedRatio(_compartmentFloodVolumes[compartmentA], maxVolumeA, out float fillA) ||
                    !TryResolveSafeNormalizedRatio(_compartmentFloodVolumes[compartmentB], maxVolumeB, out float fillB))
                {
                    EmergencyResetHydrodynamics();
                    continue;
                }

                float doorAreaSquareMeters = math.max(Epsilon, _bulkheadDoorAreas[i]);
                float characteristicHeightA = math.max(0.1f, SafeCubeRoot(maxVolumeA));
                float characteristicHeightB = math.max(0.1f, SafeCubeRoot(maxVolumeB));
                float headHeightA = fillA * characteristicHeightA;
                float headHeightB = fillB * characteristicHeightB;
                float headDifferenceMeters = headHeightA - headHeightB;
                float absHeadDifferenceMeters = math.abs(headDifferenceMeters);
                float dampingHeadMeters = math.max(Epsilon, nearZeroHeadDampingMeters);
                float dampingFactor = math.smoothstep(0f, dampingHeadMeters, absHeadDifferenceMeters);
                if (dampingFactor <= Epsilon)
                    continue;

                float velocityMetersPerSecond = ApproximateSqrtPositive(2f * GravityMetersPerSecondSquared * absHeadDifferenceMeters);
                float signedDeltaVolume =
                    math.sign(headDifferenceMeters) *
                    doorAreaSquareMeters *
                    safeDischargeCoefficient *
                    velocityMetersPerSecond *
                    transferCoefficient *
                    fixedDeltaTime *
                    dampingFactor;
                float deltaVolume = math.clamp(signedDeltaVolume, -perTickTransferCap, perTickTransferCap);

                if (deltaVolume > 0f)
                {
                    deltaVolume = math.min(deltaVolume,
                        math.min(_compartmentFloodVolumes[compartmentA], maxVolumeB - _compartmentFloodVolumes[compartmentB]));
                    if (deltaVolume <= Epsilon)
                        continue;

                    _compartmentFloodVolumes[compartmentA] -= deltaVolume;
                    _compartmentFloodVolumes[compartmentB] += deltaVolume;
                    _compartmentFlags[compartmentA] |= FlagTransferSource;
                    _compartmentFlags[compartmentB] |= FlagTransferDestination;
                }
                else if (deltaVolume < 0f)
                {
                    float transferMagnitude = math.min(-deltaVolume,
                        math.min(_compartmentFloodVolumes[compartmentB], maxVolumeA - _compartmentFloodVolumes[compartmentA]));
                    if (transferMagnitude <= Epsilon)
                        continue;

                    _compartmentFloodVolumes[compartmentA] += transferMagnitude;
                    _compartmentFloodVolumes[compartmentB] -= transferMagnitude;
                    _compartmentFlags[compartmentB] |= FlagTransferSource;
                    _compartmentFlags[compartmentA] |= FlagTransferDestination;
                }
            }
        }

        private void ApplyBreachDepressurizationSuction()
        {
            if (_atmosphereSystem == null || _cachedTransform == null || _configuredCompartmentCount <= 0)
                return;

            float externalPressureKPa = math.max(1f, externalReferencePressureKPa);
            float minimumPressureDeltaKPa = math.max(0f, minimumDepressurizationPressureDeltaKPa);
            float referenceMassKilograms = math.max(1f, depressurizationReferenceMassKilograms);
            float maximumAcceleration = math.max(0f, maximumDepressurizationAccelerationMetersPerSecondSquared);
            if (maximumAcceleration <= Epsilon)
                return;

            IPlayerRuntimeContext playerContext = ResolvePlayerRuntimeContext();
            Rigidbody playerBody = playerContext != null ? playerContext.PlayerRigidbody : null;
            Transform playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
            HectonPlayerMovement playerMovement = playerContext != null ? playerContext.PlayerMovement : null;

            for (int compartmentIndex = 0; compartmentIndex < _configuredCompartmentCount; compartmentIndex++)
            {
                float breachAreaSquareMeters = _compartmentBreachAreas[compartmentIndex];
                if (breachAreaSquareMeters <= Epsilon)
                    continue;

                float compartmentPressureKPa = _atmosphereSystem.GetRoomPressureKPa(compartmentIndex);
                float pressureDeltaKPa = compartmentPressureKPa - externalPressureKPa;
                if (pressureDeltaKPa < minimumPressureDeltaKPa)
                    continue;

                if (!TryResolveDepressurizationBounds(compartmentIndex, out Vector3 roomCenter, out Vector3 breachPosition, out float influenceRadius))
                    continue;

                float rawForceNewtons = pressureDeltaKPa * 1000f * breachAreaSquareMeters;
                float baseAcceleration = rawForceNewtons * math.rcp(referenceMassKilograms);
                if (!math.isfinite(baseAcceleration) || baseAcceleration <= Epsilon)
                    continue;

                if (playerTransform != null)
                {
                    if (playerMovement != null)
                    {
                        ApplyDepressurizationToPlayer(playerMovement, playerTransform.position, breachPosition, roomCenter, influenceRadius, baseAcceleration, maximumAcceleration);
                    }
                    else if (playerBody != null)
                    {
                        ApplyDepressurizationToBody(playerBody, playerTransform.position, breachPosition, roomCenter, influenceRadius, baseAcceleration, maximumAcceleration);
                    }
                }

                ApplyDepressurizationToLooseBodies(playerBody, roomCenter, breachPosition, influenceRadius, baseAcceleration, maximumAcceleration);
            }
        }

        private void ApplyDepressurizationToPlayer(
            HectonPlayerMovement playerMovement,
            Vector3 playerPosition,
            Vector3 breachPosition,
            Vector3 roomCenter,
            float influenceRadius,
            float baseAcceleration,
            float maximumAcceleration)
        {
            if (playerMovement == null)
                return;

            if (!IsWithinRadiusSq(playerPosition, roomCenter, influenceRadius))
                return;

            Vector3 acceleration = ResolveDepressurizationAcceleration(playerPosition, breachPosition, baseAcceleration, maximumAcceleration);
            if (acceleration.sqrMagnitude <= 0.000001f)
                return;

            playerMovement.QueueSubsystemExternalAcceleration(acceleration);
        }

        private void ApplyDepressurizationToLooseBodies(
            Rigidbody playerBody,
            Vector3 roomCenter,
            Vector3 breachPosition,
            float influenceRadius,
            float baseAcceleration,
            float maximumAcceleration)
        {
            SpatialTargetKind kindMask = SpatialTargetKind.Pickup | SpatialTargetKind.Resource;
            int hitCount = WorldSpatialHashGrid.CollectContactsNonAlloc(roomCenter, influenceRadius, kindMask, _depressurizationContacts);
            int uniqueBodyCount = 0;

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                SpatialQueryHit hit = _depressurizationContacts[hitIndex];
                if (!TryResolveDynamicBody(hit.Owner, hit.Transform, out Rigidbody body) || body == null)
                    continue;

                if (ReferenceEquals(body, _rigidbody) || ReferenceEquals(body, playerBody))
                    continue;

                bool duplicateBody = false;
                for (int uniqueIndex = 0; uniqueIndex < uniqueBodyCount; uniqueIndex++)
                {
                    if (!ReferenceEquals(_depressurizationBodies[uniqueIndex], body))
                        continue;

                    duplicateBody = true;
                    break;
                }

                if (duplicateBody)
                    continue;

                _depressurizationBodies[uniqueBodyCount++] = body;
                if (uniqueBodyCount >= DepressurizationContactCapacity)
                    break;
            }

            for (int bodyIndex = 0; bodyIndex < uniqueBodyCount; bodyIndex++)
            {
                Rigidbody body = _depressurizationBodies[bodyIndex];
                _depressurizationBodies[bodyIndex] = null;
                if (body == null)
                    continue;

                ApplyDepressurizationToBody(body, body.worldCenterOfMass, breachPosition, roomCenter, influenceRadius, baseAcceleration, maximumAcceleration);
            }
        }

        private void ApplyDepressurizationToBody(
            Rigidbody body,
            Vector3 bodyPosition,
            Vector3 breachPosition,
            Vector3 roomCenter,
            float influenceRadius,
            float baseAcceleration,
            float maximumAcceleration)
        {
            if (body == null)
                return;

            if (!IsWithinRadiusSq(bodyPosition, roomCenter, influenceRadius))
                return;

            Vector3 acceleration = ResolveDepressurizationAcceleration(bodyPosition, breachPosition, baseAcceleration, maximumAcceleration);
            if (acceleration.sqrMagnitude <= 0.000001f)
                return;

            PhysicsForceRouter.QueueAmbientForce(body, acceleration, ForceMode.Acceleration);
        }

        private Vector3 ResolveDepressurizationAcceleration(
            Vector3 bodyPosition,
            Vector3 breachPosition,
            float baseAcceleration,
            float maximumAcceleration)
        {
            Vector3 toBreach = breachPosition - bodyPosition;
            if (!float.IsFinite(baseAcceleration) || !float.IsFinite(maximumAcceleration) || baseAcceleration <= 0f || maximumAcceleration <= 0f)
                return Vector3.zero;

            float distanceSq = toBreach.sqrMagnitude;
            if (!float.IsFinite(distanceSq) || distanceSq <= Epsilon * Epsilon)
                return Vector3.zero;

            float approximateDistance = ApproximateMagnitude(toBreach);
            float inverseDistance = math.rcp(math.max(approximateDistance, Epsilon));
            float floorMeters = math.max(depressurizationDistanceFloorMeters, Epsilon);
            float floorSq = floorMeters * floorMeters;
            float inverseSafeDistance = distanceSq <= floorSq
                ? math.rcp(floorMeters)
                : inverseDistance;
            float accelerationMagnitude = math.min(math.max(0f, maximumAcceleration), baseAcceleration * inverseSafeDistance);
            return DominantAxisOrDefault(toBreach, Vector3.zero) * accelerationMagnitude;
        }

        private bool TryResolveDepressurizationBounds(int compartmentIndex, out Vector3 roomCenter, out Vector3 breachPosition, out float influenceRadius)
        {
            roomCenter = Vector3.zero;
            breachPosition = Vector3.zero;
            influenceRadius = 0f;

            if (_cachedTransform == null || compartmentIndex < 0 || compartmentIndex >= _configuredCompartmentCount)
                return false;

            Vector3 localCentroid = GetCompartmentCentroid(compartmentIndex);
            roomCenter = _cachedTransform.TransformPoint(localCentroid);

            float roomVolume = compartmentIndex < _compartmentMaxVolumes.Length
                ? math.max(Epsilon, _compartmentMaxVolumes[compartmentIndex])
                : Epsilon;
            float compartmentRadius = SafeCubeRoot(roomVolume * math.rcp(4.1887903f));
            influenceRadius = math.max(0.5f, compartmentRadius + math.max(0f, depressurizationRoomRadiusPaddingMeters));

            Vector3 hullCenter = exteriorHullCollider != null ? exteriorHullCollider.bounds.center : _cachedTransform.position;
            Vector3 outwardDirection = SafeNormalize(roomCenter - hullCenter, _cachedTransform.up);
            Vector3 probePosition = roomCenter + (outwardDirection * influenceRadius);
            breachPosition = exteriorHullCollider != null
                ? exteriorHullCollider.ClosestPoint(probePosition)
                : probePosition;

            return influenceRadius > 0f;
        }

        private static bool TryResolveDynamicBody(Component owner, Transform runtimeTransform, out Rigidbody body)
        {
            body = null;
            if (owner != null)
            {
                if (owner.TryGetComponent(out body))
                    return true;
            }

            if (runtimeTransform == null)
                return false;

            return runtimeTransform.TryGetComponent(out body);
        }

        private void FinalizeCompartmentState()
        {
            if (_configuredCompartmentCount <= 0)
            {
                _totalFloodVolumeCubicMeters = 0f;
                _floodFillRatio = 0f;

                if (_compartmentStates.IsCreated)
                {
                    for (int i = 0; i < CompartmentCapacity; i++)
                        _compartmentStates[i] = default;
                }

                return;
            }

            float totalFloodVolume = 0f;
            for (int i = 0; i < CompartmentCapacity; i++)
            {
                float maxVolume = math.max(0f, _compartmentMaxVolumes[i]);
                if (maxVolume <= Epsilon)
                {
                    _compartmentFloodVolumes[i] = 0f;
                    _compartmentFlags[i] = 0u;
                    if (_compartmentStates.IsCreated)
                        _compartmentStates[i] = default;
                    continue;
                }

                float sourceVolume = _compartmentFloodVolumes[i];
                if (!math.isfinite(sourceVolume))
                {
                    EmergencyResetHydrodynamics();
                    sourceVolume = 0f;
                }

                float currentVolume = math.clamp(sourceVolume, 0f, maxVolume);
                uint flags = _compartmentFlags[i];
                if (currentVolume >= maxVolume - Epsilon)
                {
                    currentVolume = maxVolume;
                    flags |= FlagOverflow;
                }
                else
                {
                    flags &= ~FlagOverflow;
                }

                if (!TryResolveSafeNormalizedRatio(currentVolume, maxVolume, out float fillRatio))
                {
                    EmergencyResetHydrodynamics();
                    fillRatio = 0f;
                }

                if (fillRatio >= CriticalFillThreshold)
                    flags |= FlagCritical;
                else
                    flags &= ~FlagCritical;

                _compartmentFloodVolumes[i] = currentVolume;
                _compartmentFlags[i] = flags;
                if (_compartmentStates.IsCreated)
                {
                    CompartmentState previousState = _compartmentStates[i];
                    _compartmentStates[i] = new CompartmentState
                    {
                        currentVolume = currentVolume,
                        maxVolume = maxVolume,
                        localCentroid = _compartmentLocalCentroids[i],
                        stateFlags = flags,
                        o2PartialPressureKPa = previousState.o2PartialPressureKPa,
                        co2PartialPressureKPa = previousState.co2PartialPressureKPa,
                        n2PartialPressureKPa = previousState.n2PartialPressureKPa
                    };
                }

                totalFloodVolume += currentVolume;
            }

            if (!math.isfinite(totalFloodVolume))
            {
                EmergencyResetHydrodynamics();
                _totalFloodVolumeCubicMeters = 0f;
                _floodFillRatio = 0f;
                return;
            }

            _totalFloodVolumeCubicMeters = totalFloodVolume;
            float totalCapacity = ResolveTotalCapacityCubicMeters();
            if (!math.isfinite(totalCapacity) || totalCapacity <= Epsilon)
            {
                EmergencyResetHydrodynamics();
                _totalFloodVolumeCubicMeters = 0f;
                _floodFillRatio = 0f;
                return;
            }

            _floodFillRatio = TryResolveSafeNormalizedRatio(totalFloodVolume, totalCapacity, out float totalFillRatio)
                ? totalFillRatio
                : 0f;
        }

        private void ApplyFloodMassPropertiesToRigidbody(bool force)
        {
            if (_rigidbody == null)
                return;

            float dryMass = math.max(_dryRigidbodyMass, Epsilon);
            float floodMass = _massPropertiesFront.IsCreated && _massPropertiesFront.Length > 0
                ? math.max(0f, _massPropertiesFront[0].FloodMassKilograms)
                : math.max(0f, _totalFloodVolumeCubicMeters) * WaterDensityKgPerCubicMeter;
            _currentFloraDragDensity01 = SampleMacroFloraDragDensity();
            _currentFloraAddedMassKilograms = math.max(0f, floraDragAddedMassAtFullDensityKilograms) *
                                              math.saturate(_currentFloraDragDensity01);
            float maxFloodMass = math.max(0f, ResolveTotalCapacityCubicMeters()) * WaterDensityKgPerCubicMeter;
            float damageControlLeakMass = math.max(0f, _damageControlLeakAddedMassKilograms);
            floodMass = math.min(maxFloodMass, floodMass + damageControlLeakMass);
            float cargoMass = math.max(0f, _totalCargoMassKilograms);
            float ballastWaterMass = math.max(0f, _ballastWaterMassKilograms);
            float dockedExternalMass = math.max(0f, _dockedExternalMassKilograms);
            float maxFloraMass = math.max(0f, floraDragAddedMassAtFullDensityKilograms);
            float maxCargoMass = math.max(0f, maxCargoMassKilograms);
            float targetMass = math.clamp(
                dryMass + cargoMass + ballastWaterMass + dockedExternalMass + floodMass + _currentFloraAddedMassKilograms,
                dryMass,
                dryMass + maxCargoMass + ballastWaterMass + dockedExternalMass + maxFloodMass + maxFloraMass);
            _debugFloraAddedMassKilograms = _currentFloraAddedMassKilograms;
            _debugTotalCargoMassKilograms = cargoMass;
            _debugBallastWaterMassKilograms = ballastWaterMass;
            _debugDockedExternalMassKilograms = dockedExternalMass;
            _debugDamageControlLeakAddedMassKilograms = damageControlLeakMass;
            _debugCargoMassScalar = _cargoMassScalar;
            if (!math.isfinite(targetMass))
            {
                EmergencyResetHydrodynamics();
                return;
            }

            float currentMass = math.isfinite(_lastAppliedRigidbodyMass)
                ? math.max(_lastAppliedRigidbodyMass, Epsilon)
                : dryMass;
            float threshold = math.max(0.1f, rigidbodyMassUpdateThresholdKg);
            if (!force && math.abs(targetMass - currentMass) <= threshold)
                return;

            _rigidbody.mass = targetMass;
            _lastAppliedRigidbodyMass = targetMass;
        }

        private void ApplyCenterOfMassShift(float3 targetCenter)
        {
            if (_rigidbody == null)
                return;

            float3 currentCenter = new float3(_appliedCenterOfMassLocal.x, _appliedCenterOfMassLocal.y, _appliedCenterOfMassLocal.z);
            float3 blendedCenter = FluidMathCore.ResolveCenterOfMassStep(
                currentCenter,
                targetCenter,
                _centerOfMassBlendAlpha,
                maxCenterOfMassDeltaPerTickMeters,
                Epsilon,
                out byte centerStepValid);
            if (centerStepValid == 0)
            {
                EmergencyResetHydrodynamics();
                return;
            }

            Vector3 newCenter = HectonPlayerMotor.SafeVelocity(new Vector3(blendedCenter.x, blendedCenter.y, blendedCenter.z), _appliedCenterOfMassLocal);
            if ((_appliedCenterOfMassLocal - newCenter).sqrMagnitude <= 0.000001f)
                return;

            _appliedCenterOfMassLocal = newCenter;
            _currentFloodCenterOfMassLocal = newCenter;
            if (!_externalCenterOfMassAuthority)
                _rigidbody.centerOfMass = newCenter;
        }

        private void ApplyStartupMassProperties(float3 targetCenter)
        {
            Vector3 safeCenter = SanitizeCenterOfMass(
                new Vector3(targetCenter.x, targetCenter.y, targetCenter.z),
                _appliedCenterOfMassLocal);
            Vector3 safeTensor = SanitizeTensor(LerpVector3ClampedMath(_resolvedDryInertiaTensor, _resolvedFloodedInertiaTensor, _floodFillRatio));
            float safeLinearDamping = math.isfinite(_lastAppliedLinearDamping) ? math.max(0f, _lastAppliedLinearDamping) : 0f;
            float safeAngularDamping = math.isfinite(_lastAppliedAngularDamping) ? math.max(0f, _lastAppliedAngularDamping) : 0f;

            _appliedCenterOfMassLocal = safeCenter;
            _currentFloodCenterOfMassLocal = safeCenter;
            _lastAppliedInertiaTensor = safeTensor;
            _lastAppliedLinearDamping = safeLinearDamping;
            _lastAppliedAngularDamping = safeAngularDamping;

            if (_rigidbody == null)
                return;

            if (!_externalCenterOfMassAuthority)
                _rigidbody.centerOfMass = safeCenter;
            _rigidbody.inertiaTensor = safeTensor;
            ApplyFloodMassPropertiesToRigidbody(force: true);
            _rigidbody.linearDamping = safeLinearDamping;
            _rigidbody.angularDamping = safeAngularDamping;
        }

        private void UpdateReportedFloodCenter(float3 targetCenter)
        {
            float3 currentCenter = new float3(
                _reportedFloodCenterOfMassLocal.x,
                _reportedFloodCenterOfMassLocal.y,
                _reportedFloodCenterOfMassLocal.z);
            float3 blendedCenter = LerpMad(currentCenter, targetCenter, _reportedCenterBlendAlpha);
            if (!math.all(math.isfinite(blendedCenter)))
                blendedCenter = currentCenter;

            _reportedFloodCenterOfMassLocal = new Vector3(blendedCenter.x, blendedCenter.y, blendedCenter.z);
        }

        private float3 ResolveFloodTargetCenterOfMassLocal()
        {
            float totalFloodMass = 0f;
            float3 weightedSum = float3.zero;
            Vector3 safeDryCenter = SanitizeCenterOfMass(dryCenterOfMassLocal, Vector3.zero);
            float3 dryCenter = new float3(safeDryCenter.x, safeDryCenter.y, safeDryCenter.z);
            for (int i = 0; i < _configuredCompartmentCount; i++)
            {
                CompartmentState state = _compartmentStates.IsCreated ? _compartmentStates[i] : default;
                if ((state.stateFlags & FlagFrozen) != 0 || state.maxVolume <= Epsilon)
                {
                    if (_comAccumulatorBack.IsCreated && i < _comAccumulatorBack.Length)
                        _comAccumulatorBack[i] = float3.zero;

                    continue;
                }

                if (!math.isfinite(state.currentVolume))
                {
                    EmergencyResetHydrodynamics();
                    return dryCenter;
                }

                float mass = math.max(0f, state.currentVolume) * WaterDensityKgPerCubicMeter;
                if (!math.isfinite(mass))
                {
                    EmergencyResetHydrodynamics();
                    return dryCenter;
                }

                float3 weightedCentroid = state.localCentroid * mass;
                if (!math.all(math.isfinite(weightedCentroid)))
                {
                    EmergencyResetHydrodynamics();
                    return dryCenter;
                }

                if (_comAccumulatorBack.IsCreated && i < _comAccumulatorBack.Length)
                    _comAccumulatorBack[i] = weightedCentroid;

                weightedSum += weightedCentroid;
                if (!math.all(math.isfinite(weightedSum)))
                {
                    EmergencyResetHydrodynamics();
                    return dryCenter;
                }

                totalFloodMass += mass;
                if (!math.isfinite(totalFloodMass))
                {
                    EmergencyResetHydrodynamics();
                    return dryCenter;
                }
            }

            float maxFloodMass = ResolveTotalCapacityCubicMeters() * WaterDensityKgPerCubicMeter;
            float3 floodCenter = dryCenter;
            if (totalFloodMass > Epsilon)
            {
                if (!TryResolveSafeVectorDivision(weightedSum, totalFloodMass, out floodCenter))
                {
                    EmergencyResetHydrodynamics();
                    floodCenter = dryCenter;
                }
            }

            float floodMassRatio = 0f;
            if (maxFloodMass > Epsilon &&
                !TryResolveSafeNormalizedRatio(totalFloodMass, maxFloodMass, out floodMassRatio))
            {
                EmergencyResetHydrodynamics();
                floodMassRatio = 0f;
            }

            float3 targetCenter = LerpMad(dryCenter, floodCenter, floodMassRatio);
            targetCenter = ApplyFloraOvergrowthCenterOfMassBias(targetCenter);
            if (!math.all(math.isfinite(targetCenter)))
                targetCenter = dryCenter;

            _currentFloodCenterOfMassLocal = new Vector3(targetCenter.x, targetCenter.y, targetCenter.z);
            return targetCenter;
        }

        private float3 ResolveFloodTargetCenterOfMassFromBufferedJob()
        {
            if (!_massPropertiesFront.IsCreated || _massPropertiesFront.Length == 0)
                return ResolveFloodTargetCenterOfMassLocal();

            FloodMassPropertiesResult result = _massPropertiesFront[0];
            float3 dryCenter = new float3(_appliedCenterOfMassLocal.x, _appliedCenterOfMassLocal.y, _appliedCenterOfMassLocal.z);
            float3 targetCenter = math.all(math.isfinite(result.TargetCenterLocal))
                ? result.TargetCenterLocal
                : dryCenter;
            targetCenter = ApplyFloraOvergrowthCenterOfMassBias(targetCenter);

            _currentFloodCenterOfMassLocal = new Vector3(targetCenter.x, targetCenter.y, targetCenter.z);
            return targetCenter;
        }

        private float3 ApplyFloraOvergrowthCenterOfMassBias(float3 targetCenter)
        {
            float floraDensity01 = math.saturate(_currentFloraDragDensity01);
            if (floraDensity01 <= Epsilon)
                return targetCenter;

            float downshiftMeters = math.max(0f, floraCenterOfMassDownshiftMeters);
            if (!math.isfinite(downshiftMeters))
                return targetCenter;

            float3 shiftedCenter = targetCenter;
            shiftedCenter.y -= downshiftMeters * floraDensity01;
            return math.all(math.isfinite(shiftedCenter)) ? shiftedCenter : targetCenter;
        }

        private float3 RecordAndSampleDelayedSloshAngularVelocityLocal(float internalFloodRatio)
        {
            Vector3 worldAngularVelocity = IsFiniteVector(_rigidbody.angularVelocity) ? _rigidbody.angularVelocity : Vector3.zero;
            Vector3 localAngularVelocity = _cachedTransform != null
                ? _cachedTransform.InverseTransformDirection(worldAngularVelocity)
                : worldAngularVelocity;
            if (!IsFiniteVector(localAngularVelocity))
                localAngularVelocity = Vector3.zero;

            float3 currentLocalAngularVelocity = new float3(
                localAngularVelocity.x,
                localAngularVelocity.y,
                localAngularVelocity.z);
            if (math.any(math.isnan(currentLocalAngularVelocity)) || !math.all(math.isfinite(currentLocalAngularVelocity)))
            {
                EmergencyResetHydrodynamics();
                currentLocalAngularVelocity = float3.zero;
            }

            _angularVelocityHistoryLocal[_ringHead] = currentLocalAngularVelocity;
            _ringHead = (_ringHead + 1) & RingBufferMask;

            int delayIndex = (_ringHead - SloshDelayFrames) & RingBufferMask;
            float3 delayedAngularVelocity = _angularVelocityHistoryLocal[delayIndex];
            if (math.any(math.isnan(delayedAngularVelocity)) || !math.all(math.isfinite(delayedAngularVelocity)))
            {
                EmergencyResetHydrodynamics();
                _angularVelocityHistoryLocal[delayIndex] = float3.zero;
                return float3.zero;
            }

            return delayedAngularVelocity;
        }

        private void ApplyInterpolatedInertiaTensor()
        {
            if (_rigidbody == null)
                return;

            Vector3 targetTensor;
            if (_massPropertiesFront.IsCreated && _massPropertiesFront.Length > 0)
            {
                float3 jobTensor = _massPropertiesFront[0].InertiaTensor;
                targetTensor = math.all(math.isfinite(jobTensor))
                    ? new Vector3(jobTensor.x, jobTensor.y, jobTensor.z)
                    : LerpVector3ClampedMath(_resolvedDryInertiaTensor, _resolvedFloodedInertiaTensor, _floodFillRatio);
            }
            else
            {
                targetTensor = LerpVector3ClampedMath(_resolvedDryInertiaTensor, _resolvedFloodedInertiaTensor, _floodFillRatio);
            }

            if (!IsFiniteVector(targetTensor))
            {
                EmergencyResetHydrodynamics();
                return;
            }

            Vector3 tensor = SanitizeTensor(targetTensor);
            if ((_lastAppliedInertiaTensor - tensor).sqrMagnitude <= 0.000001f)
                return;

            _rigidbody.inertiaTensor = tensor;
            _lastAppliedInertiaTensor = tensor;
        }

        private void ApplySampledExteriorBuoyancy(float depthMeters)
        {
            if (_rigidbody == null || _cachedTransform == null)
            {
                _externalSubmergedVolumeCubicMeters = 0f;
                _submersionFactor = 0f;
                _lastExternalBuoyancyForce = Vector3.zero;
                _lastExternalBuoyancyTorque = Vector3.zero;
                ResetSplashDetectionState(clearQueuedEvents: false);
                return;
            }

            float displacementVolume = ResolveExteriorDisplacementVolumeCubicMeters();
            if (!float.IsFinite(displacementVolume) || displacementVolume <= Epsilon)
            {
                _externalSubmergedVolumeCubicMeters = 0f;
                _submersionFactor = 0f;
                _lastExternalBuoyancyForce = Vector3.zero;
                _lastExternalBuoyancyTorque = Vector3.zero;
                ResetSplashDetectionState(clearQueuedEvents: false);
                return;
            }

            float safeDepthMeters = float.IsFinite(depthMeters) ? math.max(0f, depthMeters) : 0f;
            float fallbackSurfaceY = _cachedTransform.position.y + safeDepthMeters;
            float cargoDraftOffsetMeters = ResolveCargoDraftOffsetMeters();
            float crushBuoyancyScale = ResolveCrushDepthBuoyancyScale(safeDepthMeters);
            Vector3 centerOfMassWorld = _rigidbody.worldCenterOfMass;
            float3 centerOfMassWorldFloat = new float3(centerOfMassWorld.x, centerOfMassWorld.y, centerOfMassWorld.z);
            if (math.any(math.isnan(centerOfMassWorldFloat)) || !math.all(math.isfinite(centerOfMassWorldFloat)))
            {
                EmergencyResetHydrodynamics();
                _externalSubmergedVolumeCubicMeters = 0f;
                _submersionFactor = 0f;
                _lastExternalBuoyancyForce = Vector3.zero;
                _lastExternalBuoyancyTorque = Vector3.zero;
                return;
            }

            if (!IsFiniteVector(centerOfMassWorld))
            {
                _externalSubmergedVolumeCubicMeters = 0f;
                _submersionFactor = 0f;
                _lastExternalBuoyancyForce = Vector3.zero;
                _lastExternalBuoyancyTorque = Vector3.zero;
                return;
            }

            if (!TryResolveSafeQuotient(displacementVolume, ExteriorBuoyancySampleCount, out float sampleVolume))
            {
                EmergencyResetHydrodynamics();
                _externalSubmergedVolumeCubicMeters = 0f;
                _submersionFactor = 0f;
                _lastExternalBuoyancyForce = Vector3.zero;
                _lastExternalBuoyancyTorque = Vector3.zero;
                return;
            }

            bool hullInsideBrine = TryResolveHullBrineLayer(centerOfMassWorld, out BrineLayerSample brineSample);
            float brineDensityMultiplier = hullInsideBrine
                ? math.max(1f, brineSample.DensityMultiplier)
                : 1f;
            UpdateBrineHullBreachState(hullInsideBrine, centerOfMassWorld);

            float perSampleForceMagnitude = WaterDensityKgPerCubicMeter *
                brineDensityMultiplier *
                sampleVolume *
                GravityMetersPerSecondSquared *
                crushBuoyancyScale;
            float rigidbodyMass = math.isfinite(_rigidbody.mass) ? math.max(_rigidbody.mass, Epsilon) : Epsilon;
            if (!TryResolveSafeQuotient(rigidbodyMass, ExteriorBuoyancySampleCount, out float sampleHullMass))
            {
                EmergencyResetHydrodynamics();
                _externalSubmergedVolumeCubicMeters = 0f;
                _submersionFactor = 0f;
                _lastExternalBuoyancyForce = Vector3.zero;
                _lastExternalBuoyancyTorque = Vector3.zero;
                return;
            }

            if (!float.IsFinite(sampleVolume) || !float.IsFinite(perSampleForceMagnitude))
            {
                EmergencyResetHydrodynamics();
                _externalSubmergedVolumeCubicMeters = 0f;
                _submersionFactor = 0f;
                _lastExternalBuoyancyForce = Vector3.zero;
                _lastExternalBuoyancyTorque = Vector3.zero;
                return;
            }

            Vector3 totalEquivalentForce = Vector3.zero;
            Vector3 totalEquivalentTorque = Vector3.zero;
            float submergedVolume = 0f;
            Matrix4x4 localToWorldMatrix = _cachedTransform.localToWorldMatrix;
            IHectonOceanKinematics oceanKinematics = ResolveOceanKinematicsProvider();
            if (!_exteriorBuoyancySampleLocalPoints.IsCreated ||
                _exteriorBuoyancySampleLocalPoints.Length < ExteriorBuoyancySampleCount)
            {
                EmergencyResetHydrodynamics();
                _externalSubmergedVolumeCubicMeters = 0f;
                _submersionFactor = 0f;
                _lastExternalBuoyancyForce = Vector3.zero;
                _lastExternalBuoyancyTorque = Vector3.zero;
                return;
            }

            for (int i = 0; i < ExteriorBuoyancySampleCount; i++)
            {
                Vector3 worldPoint = localToWorldMatrix.MultiplyPoint3x4(ToVector3(_exteriorBuoyancySampleLocalPoints[i]));
                float3 worldPointFloat = new float3(worldPoint.x, worldPoint.y, worldPoint.z);
                if (math.any(math.isnan(worldPointFloat)) || !math.all(math.isfinite(worldPointFloat)))
                {
                    EmergencyResetHydrodynamics();
                    _externalSubmergedVolumeCubicMeters = 0f;
                    _submersionFactor = 0f;
                    _lastExternalBuoyancyForce = Vector3.zero;
                    _lastExternalBuoyancyTorque = Vector3.zero;
                    return;
                }

                if (!IsFiniteVector(worldPoint))
                    continue;

                float sampleSurfaceY = ResolveSurfaceHeightAtSample(worldPoint, fallbackSurfaceY, oceanKinematics);
                float submersionFactor = ResolveSurfaceSubmersionFactor((worldPoint.y + cargoDraftOffsetMeters) - sampleSurfaceY);
                QueueExteriorSplashEventIfNeeded(i, worldPoint, submersionFactor, sampleHullMass);
                if (submersionFactor <= Epsilon)
                    continue;

                float submergedSampleVolume = sampleVolume * submersionFactor;
                if (!float.IsFinite(submergedSampleVolume))
                {
                    EmergencyResetHydrodynamics();
                    _externalSubmergedVolumeCubicMeters = 0f;
                    _submersionFactor = 0f;
                    _lastExternalBuoyancyForce = Vector3.zero;
                    _lastExternalBuoyancyTorque = Vector3.zero;
                    return;
                }

                submergedVolume += submergedSampleVolume;

                Vector3 sampleAcceleration = Vector3.up * ((perSampleForceMagnitude * submersionFactor) * math.rcp(rigidbodyMass));
                if (!IsFiniteVector(sampleAcceleration))
                {
                    EmergencyResetHydrodynamics();
                    _externalSubmergedVolumeCubicMeters = 0f;
                    _submersionFactor = 0f;
                    _lastExternalBuoyancyForce = Vector3.zero;
                    _lastExternalBuoyancyTorque = Vector3.zero;
                    return;
                }

                Vector3 scaledAcceleration = ApplyHydrodynamicLinearInertiaScale(sampleAcceleration);
                if (scaledAcceleration.sqrMagnitude > Epsilon)
                    PhysicsForceRouter.QueueAmbientForceAtPosition(_rigidbody, scaledAcceleration, worldPoint, ForceMode.Acceleration);

                Vector3 equivalentForce = scaledAcceleration * rigidbodyMass;
                totalEquivalentForce += equivalentForce;
                totalEquivalentTorque += Vector3.Cross(worldPoint - centerOfMassWorld, equivalentForce);
            }

            if (!float.IsFinite(submergedVolume))
            {
                EmergencyResetHydrodynamics();
                _externalSubmergedVolumeCubicMeters = 0f;
                _submersionFactor = 0f;
                _lastExternalBuoyancyForce = Vector3.zero;
                _lastExternalBuoyancyTorque = Vector3.zero;
                return;
            }

            _externalSubmergedVolumeCubicMeters = math.clamp(submergedVolume, 0f, displacementVolume);
            if (!TryResolveSafeNormalizedRatio(_externalSubmergedVolumeCubicMeters, displacementVolume, out _submersionFactor))
            {
                DumpHydroBlackBoxOnce(HydroBlackBoxFlagInvalidBuoyancy | HydroBlackBoxFlagEmergencyReset);
                EmergencyResetHydrodynamics();
                _externalSubmergedVolumeCubicMeters = 0f;
                _submersionFactor = 0f;
                _lastExternalBuoyancyForce = Vector3.zero;
                _lastExternalBuoyancyTorque = Vector3.zero;
                return;
            }

            float maxForceMagnitude = WaterDensityKgPerCubicMeter *
                brineDensityMultiplier *
                displacementVolume *
                GravityMetersPerSecondSquared *
                math.max(1f, exteriorBuoyancyForceClampScale);
            float maxTorqueMagnitude = maxForceMagnitude *
                math.max(0.1f, _exteriorBuoyancyMaxLeverArm) *
                math.max(1f, exteriorBuoyancyTorqueClampScale);

            totalEquivalentForce = ClampMagnitude(totalEquivalentForce, maxForceMagnitude);
            totalEquivalentTorque = ClampMagnitude(totalEquivalentTorque, maxTorqueMagnitude);
            float3 totalForceFloat = new float3(totalEquivalentForce.x, totalEquivalentForce.y, totalEquivalentForce.z);
            float3 totalTorqueFloat = new float3(totalEquivalentTorque.x, totalEquivalentTorque.y, totalEquivalentTorque.z);
            if (math.any(math.isnan(totalForceFloat)) || math.any(math.isnan(totalTorqueFloat)) ||
                !math.all(math.isfinite(totalForceFloat)) || !math.all(math.isfinite(totalTorqueFloat)))
            {
                DumpHydroBlackBoxOnce(HydroBlackBoxFlagInvalidBuoyancy | HydroBlackBoxFlagEmergencyReset);
                EmergencyResetHydrodynamics();
                totalEquivalentForce = Vector3.zero;
                totalEquivalentTorque = Vector3.zero;
            }

            if (!IsFiniteVector(totalEquivalentForce))
                totalEquivalentForce = Vector3.zero;
            if (!IsFiniteVector(totalEquivalentTorque))
                totalEquivalentTorque = Vector3.zero;

            _lastExternalBuoyancyForce = totalEquivalentForce;
            _lastExternalBuoyancyTorque = ApplyHydrodynamicAngularInertiaScale(totalEquivalentTorque);
        }

        private bool TryResolveHullBrineLayer(Vector3 runtimePosition, out BrineLayerSample sample)
        {
            sample = default;
            if (!IsFiniteVector(runtimePosition))
                return false;

            ResourceDistributionDirector director = _resourceDistributionRuntime;
            if (director == null || !director.TrySampleBrineLayer(runtimePosition, out sample))
                return false;

            double3 shiftOffset = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            return BrineLayerMath.IsRuntimeBelowAbsolutePlane(
                runtimePosition.y,
                sample.AbsoluteHeightY,
                (float)shiftOffset.y);
        }

        private void UpdateBrineHullBreachState(bool insideBrine, Vector3 runtimePosition)
        {
            _wasBrineSubmerged = _isBrineSubmerged;
            _isBrineSubmerged = insideBrine;
            if (_isBrineSubmerged)
            {
                _brineSubmersionTime += math.max(0f, Time.fixedDeltaTime);
            }
            else
            {
                _brineSubmersionTime = 0f;
            }

            if (_isBrineSubmerged == _wasBrineSubmerged || !IsFiniteVector(runtimePosition))
                return;

            AcousticPingSignal signal = default;
            signal.PositionAup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            signal.RadiusMeters = 28f;
            signal.Intensity01 = 0.85f;
            signal.SourceId = SubmarineFluidDynamicsContextHash;
            signal.Channel = BrineLayerConstants.AcousticThickFluidChannel;
            signal.Flags = _isBrineSubmerged ? BrineLayerConstants.EnteredFlag : BrineLayerConstants.ExitedFlag;
            SignalBus<AcousticPingSignal>.Push(in signal);
        }

        private void ResetBrineHullState()
        {
            _isBrineSubmerged = false;
            _wasBrineSubmerged = false;
            _brineSubmersionTime = 0f;
        }

        private void ApplyAddedMassDamping()
        {
            if (_rigidbody == null)
                return;

            if (!float.IsFinite(_submersionFactor))
                _submersionFactor = 0f;
            else
                _submersionFactor = math.saturate(_submersionFactor);

            float internalFloodRatio = float.IsFinite(_floodFillRatio)
                ? math.saturate(_floodFillRatio)
                : 0f;
            float criticalFloodRatio = math.smoothstep(CriticalFillThreshold, 1f, internalFloodRatio);
            float dampingSubmersion = math.max(_submersionFactor, internalFloodRatio);
            float linearScale = math.max(0f, addedMassLinearDampingScale) *
                (1f + internalFloodRatio + (criticalFloodRatio * CriticalFloodAddedMassLinearBoost));
            float angularScale = math.max(0f, addedMassAngularDampingScale) *
                (1f + (internalFloodRatio * 2f) + (criticalFloodRatio * CriticalFloodAddedMassAngularBoost));
            float floraDensity01 = math.saturate(_currentFloraDragDensity01);
            float floraLinearMultiplier = math.max(
                MinimumAnalyticalDragModifier,
                LerpMad(1f, math.max(1f, floraDragLinearMultiplier), floraDensity01));
            float floraAngularMultiplier = math.max(
                MinimumAnalyticalDragModifier,
                LerpMad(1f, math.max(1f, floraDragAngularMultiplier), floraDensity01));
            _debugFloraDragDensity = floraDensity01;
            if (!float.IsFinite(criticalFloodRatio) ||
                !float.IsFinite(dampingSubmersion) ||
                !float.IsFinite(linearScale) ||
                !float.IsFinite(angularScale) ||
                !float.IsFinite(floraLinearMultiplier) ||
                !float.IsFinite(floraAngularMultiplier))
            {
                EmergencyResetHydrodynamics();
                return;
            }

            _currentHydrodynamicLinearInertiaScale = math.max(1f, (1f + (linearScale * dampingSubmersion)) * floraLinearMultiplier);
            _currentHydrodynamicAngularInertiaScale = math.max(1f, (1f + (angularScale * dampingSubmersion)) * floraAngularMultiplier);

            if (_hullImplosionActive)
                _currentHydrodynamicLinearInertiaScale += math.max(0f, implosionDragBonus) * 0.02f;

            if (math.abs(_lastAppliedLinearDamping) > 0.0005f || _rigidbody.linearDamping != 0f)
            {
                _rigidbody.linearDamping = 0f;
                _lastAppliedLinearDamping = 0f;
            }

            if (math.abs(_lastAppliedAngularDamping) > 0.0005f || _rigidbody.angularDamping != 0f)
            {
                _rigidbody.angularDamping = 0f;
                _lastAppliedAngularDamping = 0f;
            }
        }

        private float SampleMacroFloraDragDensity()
        {
            if (_rigidbody == null || _cachedTransform == null)
                return 0f;

            Vector3 linearVelocity = _rigidbody.linearVelocity;
            if (linearVelocity.sqrMagnitude < floraDragMinimumSpeedMetersPerSecond * floraDragMinimumSpeedMetersPerSecond)
                return 0f;

            Bounds hullBounds = exteriorHullCollider != null
                ? exteriorHullCollider.bounds
                : new Bounds(_rigidbody.worldCenterOfMass, Vector3.one * 4f);
            Vector3 center = hullBounds.center;
            float sampleRadius = math.max(floraDragMinimumSampleRadiusMeters, math.max(hullBounds.extents.x, hullBounds.extents.z));
            FloraInteractionManager floraInteractionManager = FloraInteractionManager.ActiveRuntimeInstance;
            if (floraInteractionManager != null &&
                floraInteractionManager.TryResolveKelpPushback(center, sampleRadius, out float spatialHashDensity01, out float bendRadiusMeters))
            {
                floraInteractionManager.RegisterExternalInteraction(center, linearVelocity, math.max(sampleRadius, bendRadiusMeters));
                return math.saturate(spatialHashDensity01);
            }

            HectonMapMagicVegetationBridge vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (vegetationBridge == null)
                return 0f;

            Vector3 forwardOffset = _cachedTransform.forward * (hullBounds.extents.z * 0.6f);
            Vector3 rightOffset = _cachedTransform.right * (hullBounds.extents.x * 0.6f);

            float densitySum = 0f;
            int densityCount = 0;
            AccumulateFloraDensitySample(vegetationBridge, center, ref densitySum, ref densityCount);
            AccumulateFloraDensitySample(vegetationBridge, center + forwardOffset, ref densitySum, ref densityCount);
            AccumulateFloraDensitySample(vegetationBridge, center - forwardOffset, ref densitySum, ref densityCount);
            AccumulateFloraDensitySample(vegetationBridge, center + rightOffset, ref densitySum, ref densityCount);
            AccumulateFloraDensitySample(vegetationBridge, center - rightOffset, ref densitySum, ref densityCount);

            if (densityCount <= 0)
                return 0f;

            float normalizedDensity = densitySum * math.rcp((float)densityCount);
            float radiusScale = math.saturate(sampleRadius * math.rcp(math.max(floraDragMinimumSampleRadiusMeters, 0.01f)));
            return math.saturate(normalizedDensity * LerpMad(0.85f, 1.15f, math.saturate(radiusScale - 1f)));
        }

        private static void AccumulateFloraDensitySample(
            HectonMapMagicVegetationBridge vegetationBridge,
            Vector3 samplePosition,
            ref float densitySum,
            ref int densityCount)
        {
            HectonMapMagicVegetationBridge.VegetationDensitySample sample = vegetationBridge.GetVegetationDensity(samplePosition);
            if (!sample.HasVegetation)
                return;

            bool contributesDrag = sample.Type == HectonVegetationInstanceType.GiantKelp ||
                                   sample.Type == HectonVegetationInstanceType.Sargassum ||
                                   sample.SemanticType == HectonMapMagicVegetationBridge.VegetationSemanticType.OrganicKelp ||
                                   sample.SemanticType == HectonMapMagicVegetationBridge.VegetationSemanticType.FloatingSargassum;
            if (!contributesDrag)
                return;

            densitySum += math.saturate(sample.Density);
            densityCount++;
        }

        private void UpdateExteriorThermalAnomalies(float fixedDeltaTime)
        {
            if (fixedDeltaTime <= 0f ||
                !_exteriorThermalAnomalyCenters.IsCreated ||
                !_exteriorThermalAnomalyTemperatures.IsCreated ||
                !_exteriorThermalAnomalyLifetimes.IsCreated ||
                !_exteriorThermalHazardIds.IsCreated)
            {
                return;
            }

            EnsurePlayerBindings();

            for (int slotIndex = 0; slotIndex < ExteriorThermalAnomalyCapacity; slotIndex++)
            {
                float remainingLifetime = _exteriorThermalAnomalyLifetimes[slotIndex];
                if (remainingLifetime <= 0f)
                    continue;

                float nextLifetime = math.max(0f, remainingLifetime - fixedDeltaTime);
                float currentTemperature = math.max(ExteriorWaterReferenceTemperatureCelsius, _exteriorThermalAnomalyTemperatures[slotIndex]);
                currentTemperature = math.max(
                    ExteriorWaterReferenceTemperatureCelsius,
                    currentTemperature - (ExteriorThermalDecayPerSecond * fixedDeltaTime));

                Vector3 cellCenter = ToVector3(_exteriorThermalAnomalyCenters[slotIndex]);
                float surfaceY = ResolveSurfaceHeightAtSample(cellCenter, cellCenter.y);
                float depthMeters = math.max(0f, surfaceY - cellCenter.y);
                float boilingPointCelsius = ResolveBoilingPointCelsius(depthMeters);

                if (currentTemperature > boilingPointCelsius)
                {
                    float intensity = math.saturate((currentTemperature - boilingPointCelsius) * 0.028571428f);
                    int hazardId = ResolveExteriorThermalHazardId(slotIndex);
                    HectonHazardManager.Register(hazardId, cellCenter, intensity, ExteriorBoilingImpulseRadiusMeters, HazardType.Heat);
                    ApplyExteriorBoilingUpdraft(cellCenter, intensity, fixedDeltaTime);
                }
                else if (_exteriorThermalHazardIds[slotIndex] != 0)
                {
                    HectonHazardManager.Unregister(_exteriorThermalHazardIds[slotIndex]);
                    _exteriorThermalHazardIds[slotIndex] = 0;
                }

                _exteriorThermalAnomalyTemperatures[slotIndex] = currentTemperature;
                _exteriorThermalAnomalyLifetimes[slotIndex] = nextLifetime;
                if (nextLifetime > 0f || currentTemperature > ExteriorWaterReferenceTemperatureCelsius + 0.1f)
                    continue;

                if (_exteriorThermalHazardIds[slotIndex] != 0)
                {
                    HectonHazardManager.Unregister(_exteriorThermalHazardIds[slotIndex]);
                    _exteriorThermalHazardIds[slotIndex] = 0;
                }

                _exteriorThermalAnomalyCenters[slotIndex] = float3.zero;
                _exteriorThermalAnomalyTemperatures[slotIndex] = ExteriorWaterReferenceTemperatureCelsius;
                _exteriorThermalAnomalyLifetimes[slotIndex] = 0f;
            }
        }

        private void ApplyExteriorBoilingUpdraft(Vector3 cellCenter, float intensity, float fixedDeltaTime)
        {
            if (intensity <= 0f)
                return;

            float influenceRadius = ExteriorBoilingImpulseRadiusMeters;
            int contactCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                cellCenter,
                influenceRadius,
                _exteriorThermalContacts,
                _ExteriorBoilingUpdraftLayerMask,
                QueryTriggerInteraction.Ignore);

            for (int contactIndex = 0; contactIndex < contactCount; contactIndex++)
            {
                Collider contact = _exteriorThermalContacts[contactIndex];
                if (contact == null)
                    continue;

                Rigidbody body = contact.attachedRigidbody;
                if (body == null || body == _rigidbody)
                    continue;

                Vector3 samplePoint = body.worldCenterOfMass;
                float distanceT = ResolveDistanceSqFalloff01(samplePoint, cellCenter, influenceRadius);
                if (distanceT <= 0f)
                    continue;

                float accelerationMagnitude = ExteriorBoilingAccelerationMetersPerSecondSquared * intensity * distanceT;
                PhysicsForceRouter.QueueForce(body, Vector3.up * accelerationMagnitude, ForceMode.Acceleration);
            }

            if (_cachedPlayerTransform == null || _cachedPlayerMovement == null)
                return;

            float playerDistanceT = ResolveDistanceSqFalloff01(_cachedPlayerTransform.position, cellCenter, influenceRadius);
            if (playerDistanceT <= 0f)
                return;

            Vector3 updraftVelocity = Vector3.up * (ExteriorBoilingAccelerationMetersPerSecondSquared * 0.06f * intensity * playerDistanceT * fixedDeltaTime);
            _cachedPlayerMovement.ApplyExternalThermalUpdraft(updraftVelocity);
            if (_cachedPlayerRigidbody != null)
                PhysicsForceRouter.QueueAmbientForce(_cachedPlayerRigidbody, Vector3.up * (ExteriorBoilingAccelerationMetersPerSecondSquared * intensity * playerDistanceT), ForceMode.Acceleration);
        }

        private void EnsurePlayerBindings()
        {
            Transform playerTransform = BootstrapState.CurrentPlayerTransform;
            if (playerTransform == null)
            {
                _cachedPlayerTransform = null;
                _cachedPlayerMovement = null;
                _cachedPlayerRigidbody = null;
                return;
            }

            if (_cachedPlayerTransform != playerTransform)
            {
                _cachedPlayerTransform = playerTransform;
                _cachedPlayerMovement = null;
                _cachedPlayerRigidbody = null;
            }

            if (_cachedPlayerMovement == null)
                playerTransform.TryGetComponent(out _cachedPlayerMovement);

            if (_cachedPlayerRigidbody == null)
                playerTransform.TryGetComponent(out _cachedPlayerRigidbody);
        }

        private void ClearExteriorThermalAnomalies()
        {
            if (!_exteriorThermalAnomalyCenters.IsCreated ||
                !_exteriorThermalAnomalyTemperatures.IsCreated ||
                !_exteriorThermalAnomalyLifetimes.IsCreated ||
                !_exteriorThermalHazardIds.IsCreated)
            {
                return;
            }

            for (int slotIndex = 0; slotIndex < ExteriorThermalAnomalyCapacity; slotIndex++)
            {
                if (_exteriorThermalHazardIds[slotIndex] != 0)
                    HectonHazardManager.Unregister(_exteriorThermalHazardIds[slotIndex]);

                _exteriorThermalHazardIds[slotIndex] = 0;
                _exteriorThermalAnomalyCenters[slotIndex] = float3.zero;
                _exteriorThermalAnomalyTemperatures[slotIndex] = ExteriorWaterReferenceTemperatureCelsius;
                _exteriorThermalAnomalyLifetimes[slotIndex] = 0f;
            }
        }

        private static float MinEffectiveBeamPowerForThermalAnomaly()
        {
            return 0.02f;
        }

        private static Vector3 QuantizeExteriorThermalCell(Vector3 runtimePoint)
        {
            float invCellSize = math.rcp(ExteriorThermalCellSizeMeters);
            return new Vector3(
                (math.floor(runtimePoint.x * invCellSize) + 0.5f) * ExteriorThermalCellSizeMeters,
                (math.floor(runtimePoint.y * invCellSize) + 0.5f) * ExteriorThermalCellSizeMeters,
                (math.floor(runtimePoint.z * invCellSize) + 0.5f) * ExteriorThermalCellSizeMeters);
        }

        private int ResolveExteriorThermalSlot(Vector3 quantizedCenter)
        {
            if (!_exteriorThermalAnomalyCenters.IsCreated ||
                !_exteriorThermalAnomalyLifetimes.IsCreated)
            {
                return -1;
            }

            int expiredSlot = -1;
            float lowestLifetime = float.MaxValue;
            int oldestSlot = 0;
            float3 quantizedCenterFloat = ToFloat3(quantizedCenter);

            for (int slotIndex = 0; slotIndex < ExteriorThermalAnomalyCapacity; slotIndex++)
            {
                if (_exteriorThermalAnomalyLifetimes[slotIndex] > 0f)
                {
                    if (math.lengthsq(ResolveExteriorThermalAnomalyCenter(slotIndex) - quantizedCenterFloat) <= 0.01f)
                        return slotIndex;

                    if (_exteriorThermalAnomalyLifetimes[slotIndex] < lowestLifetime)
                    {
                        lowestLifetime = _exteriorThermalAnomalyLifetimes[slotIndex];
                        oldestSlot = slotIndex;
                    }

                    continue;
                }

                expiredSlot = slotIndex;
                break;
            }

            return expiredSlot >= 0 ? expiredSlot : oldestSlot;
        }

        private float3 ResolveExteriorThermalAnomalyCenter(int slotIndex)
        {
            if (!_exteriorThermalAnomalyCenters.IsCreated ||
                slotIndex < 0 ||
                slotIndex >= _exteriorThermalAnomalyCenters.Length)
            {
                return float3.zero;
            }

            float3 center = _exteriorThermalAnomalyCenters[slotIndex];
            return math.all(math.isfinite(center)) ? center : float3.zero;
        }

        private int ResolveExteriorThermalHazardId(int slotIndex)
        {
            if (!_exteriorThermalHazardIds.IsCreated || slotIndex < 0 || slotIndex >= _exteriorThermalHazardIds.Length)
                return 0;

            if (_exteriorThermalHazardIds[slotIndex] != 0)
                return _exteriorThermalHazardIds[slotIndex];

            int hullId = _cachedTransform != null
                ? unchecked((int)EntityId.ToULong(_cachedTransform.GetEntityId()))
                : unchecked((int)EntityId.ToULong(GetEntityId()));
            _exteriorThermalHazardIds[slotIndex] = (hullId * 31) ^ (slotIndex + 1);
            return _exteriorThermalHazardIds[slotIndex];
        }

        private static float ResolveBoilingPointCelsius(float depthMeters)
        {
            return 100f + (math.max(0f, depthMeters) * ExteriorBoilingDepthSlopeCelsiusPerMeter);
        }

        private void ApplyDelayedSloshTorque()
        {
            if (_rigidbody == null || !_angularVelocityHistoryLocal.IsCreated)
            {
                _debugDelayedSloshAngularVelocityLocal = Vector3.zero;
                return;
            }

            float internalFloodRatio = float.IsFinite(_floodFillRatio)
                ? math.saturate(_floodFillRatio)
                : 0f;
            if (internalFloodRatio <= Epsilon)
            {
                _debugDelayedSloshAngularVelocityLocal = Vector3.zero;
                _lastSloshTorqueLocal = Vector3.zero;
                return;
            }

            float3 delayedAngularVelocity = RecordAndSampleDelayedSloshAngularVelocityLocal(internalFloodRatio);
            if (math.any(math.isnan(delayedAngularVelocity)) || !math.all(math.isfinite(delayedAngularVelocity)))
            {
                EmergencyResetHydrodynamics();
                _debugDelayedSloshAngularVelocityLocal = Vector3.zero;
                _lastSloshTorqueLocal = Vector3.zero;
                return;
            }

            _debugDelayedSloshAngularVelocityLocal = new Vector3(
                delayedAngularVelocity.x,
                delayedAngularVelocity.y,
                delayedAngularVelocity.z);
            float3 totalSloshTorque = float3.zero;
            float torqueScale = math.max(0f, sloshFactor);

            for (int i = 0; i < _configuredCompartmentCount; i++)
            {
                float currentVolume = _compartmentFloodVolumes[i];
                if (currentVolume < sloshMinimumVolumeCubicMeters)
                    continue;

                float maxVolume = _compartmentMaxVolumes[i];
                if (maxVolume <= Epsilon)
                    continue;

                if (!TryResolveSafeNormalizedRatio(currentVolume, maxVolume, out float fillRatio))
                {
                    EmergencyResetHydrodynamics();
                    _lastSloshTorqueLocal = Vector3.zero;
                    return;
                }

                float freesurf = 1f - fillRatio;
                freesurf *= freesurf;
                float sloshMass = currentVolume * WaterDensityKgPerCubicMeter;
                if (!float.IsFinite(sloshMass))
                {
                    EmergencyResetHydrodynamics();
                    _lastSloshTorqueLocal = Vector3.zero;
                    return;
                }

                float viscosity01 = _compartmentViscosity01.IsCreated ? math.saturate(_compartmentViscosity01[i]) : 0f;
                float viscosityDamping = math.rcp(1f + (viscosity01 * math.max(0f, viscositySloshDampingScale)));
                totalSloshTorque += -delayedAngularVelocity * (fillRatio * torqueScale * sloshMass * freesurf * viscosityDamping);
                if (math.any(math.isnan(totalSloshTorque)) || !math.all(math.isfinite(totalSloshTorque)))
                {
                    EmergencyResetHydrodynamics();
                    _lastSloshTorqueLocal = Vector3.zero;
                    return;
                }
            }

            float maxTorqueMagnitude = math.max(0f, maxSloshTorque);
            if (maxTorqueMagnitude > Epsilon)
            {
                float torqueMagnitudeSq = math.lengthsq(totalSloshTorque);
                float maxTorqueMagnitudeSq = maxTorqueMagnitude * maxTorqueMagnitude;
                if (torqueMagnitudeSq > maxTorqueMagnitudeSq && torqueMagnitudeSq > Epsilon)
                {
                    float torqueMagnitude = ApproximateMagnitude(totalSloshTorque);
                    float torqueClampScale = maxTorqueMagnitude * math.rcp(math.max(torqueMagnitude, Epsilon));
                    if (!math.isfinite(torqueClampScale))
                    {
                        EmergencyResetHydrodynamics();
                        _lastSloshTorqueLocal = Vector3.zero;
                        return;
                    }

                    totalSloshTorque *= torqueClampScale;
                }
            }

            if (math.any(math.isnan(totalSloshTorque)) || !math.all(math.isfinite(totalSloshTorque)))
            {
                EmergencyResetHydrodynamics();
                _lastSloshTorqueLocal = Vector3.zero;
                return;
            }

            if (math.lengthsq(totalSloshTorque) <= Epsilon)
            {
                _lastSloshTorqueLocal = Vector3.zero;
                return;
            }

            Vector3 localTorque = HectonPlayerMotor.SafeVelocity(new Vector3(totalSloshTorque.x, totalSloshTorque.y, totalSloshTorque.z));
            if (localTorque.sqrMagnitude <= Epsilon)
            {
                _lastSloshTorqueLocal = Vector3.zero;
                return;
            }

            Vector3 worldTorque = _cachedTransform != null
                ? _cachedTransform.TransformDirection(localTorque)
                : localTorque;
            worldTorque = ApplyHydrodynamicAngularInertiaScale(worldTorque);
            if (!IsFiniteVector(worldTorque))
            {
                _lastSloshTorqueLocal = Vector3.zero;
                return;
            }

            PhysicsForceRouter.QueueAmbientTorque(_rigidbody, worldTorque, ForceMode.Force);
            _lastSloshTorqueLocal = localTorque;
        }

        private void EmergencyResetHydrodynamics()
        {
            _skipHydrodynamicsForCurrentFixedTick = true;

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
                _rigidbody.linearDamping = 0f;
                _rigidbody.angularDamping = 0f;
            }

            _externalSubmergedVolumeCubicMeters = 0f;
            _submersionFactor = 0f;
            _currentFloraDragDensity01 = 0f;
            _currentFloraAddedMassKilograms = 0f;
            _damageControlLeakAddedMassKilograms = 0f;
            _lastSloshTorqueLocal = Vector3.zero;
            _lastExternalBuoyancyForce = Vector3.zero;
            _lastExternalBuoyancyTorque = Vector3.zero;
            _pendingTowingTensionVector = Vector3.zero;
            _targetBuoyancyBias01 = 0f;
            _ballastBlowTimer = 0f;
            _debugDelayedSloshAngularVelocityLocal = Vector3.zero;
            _debugLastSloshTorqueLocal = Vector3.zero;
            _debugHydroDragAcceleration = Vector3.zero;
            _debugHydroTorque = Vector3.zero;
            _debugTowingTensionVector = Vector3.zero;
            _debugCavitationActive = false;
            _debugExternalSubmergedVolumeCubicMeters = 0f;
            _debugSubmersionFactor = 0f;
            _currentHydrodynamicLinearInertiaScale = 1f;
            _currentHydrodynamicAngularInertiaScale = 1f;
            ResetSplashDetectionState(clearQueuedEvents: true);

            GlobalTelemetryBus.PublishPerformanceWarning(
                HydrodynamicsResetWarningHash,
                SubmarineFluidDynamicsContextHash,
                1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldAbortHydrodynamicsFixedTick()
        {
            if (!_skipHydrodynamicsForCurrentFixedTick)
                return false;

            RefreshDebugState();
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector3 ApplyHydrodynamicLinearInertiaScale(Vector3 force)
        {
            float scale = math.max(1f, _currentHydrodynamicLinearInertiaScale);
            return scale > 1f ? force * math.rcp(scale) : force;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector3 ApplyHydrodynamicAngularInertiaScale(Vector3 torque)
        {
            float scale = math.max(1f, _currentHydrodynamicAngularInertiaScale);
            return scale > 1f ? torque * math.rcp(scale) : torque;
        }

        private void QueueExteriorSplashEventIfNeeded(int sampleIndex, Vector3 worldPoint, float currentSubmersionFactor, float sampleHullMass)
        {
            if (!_previousExteriorSampleSubmersionFactors.IsCreated ||
                sampleIndex < 0 ||
                sampleIndex >= _previousExteriorSampleSubmersionFactors.Length)
            {
                return;
            }

            float previousSubmersionFactor = _previousExteriorSampleSubmersionFactors[sampleIndex];
            if (!float.IsFinite(previousSubmersionFactor))
                previousSubmersionFactor = 0f;

            previousSubmersionFactor = math.saturate(previousSubmersionFactor);
            currentSubmersionFactor = math.saturate(currentSubmersionFactor);
            _previousExteriorSampleSubmersionFactors[sampleIndex] = currentSubmersionFactor;

            if (previousSubmersionFactor >= SplashSubmersionThreshold && currentSubmersionFactor <= Epsilon)
                QueueSurfacingBreachSignalIfNeeded(worldPoint, sampleHullMass);

            if (previousSubmersionFactor > Epsilon || currentSubmersionFactor <= SplashSubmersionThreshold)
                return;

            if (_rigidbody == null)
                return;

            Vector3 pointVelocity = _rigidbody.GetPointVelocity(worldPoint);
            if (!IsFiniteVector(pointVelocity))
                return;

            float impactSpeedMetersPerSecond = math.max(0f, -Vector3.Dot(pointVelocity, Vector3.up));
            if (!(impactSpeedMetersPerSecond > Epsilon) || !float.IsFinite(impactSpeedMetersPerSecond))
                return;

            float effectiveSampleMass = math.max(sampleHullMass, Epsilon);
            float kineticEnergyJoules = 0.5f * effectiveSampleMass * impactSpeedMetersPerSecond * impactSpeedMetersPerSecond;
            if (!(kineticEnergyJoules > Epsilon) || !float.IsFinite(kineticEnergyJoules))
                return;

            double3 absoluteUniversePosition = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(worldPoint);
            if (!math.all(math.isfinite(absoluteUniversePosition)))
                return;

            uint splashHash = ResolveSplashLcgHash(absoluteUniversePosition, sampleIndex);
            float deterministicGain = 0.9f + ((splashHash & 1023u) * (0.2f / 1023f));
            kineticEnergyJoules *= deterministicGain;

            SplashEvent splashEvent = new SplashEvent
            {
                RuntimePosition = new float3(worldPoint.x, worldPoint.y, worldPoint.z),
                AbsoluteUniversePosition = new float3(
                    (float)absoluteUniversePosition.x,
                    (float)absoluteUniversePosition.y,
                    (float)absoluteUniversePosition.z),
                SurfaceNormal = new float3(0f, 1f, 0f),
                ImpactSpeedMetersPerSecond = impactSpeedMetersPerSecond,
                KineticEnergyJoules = kineticEnergyJoules,
                SubmersionFactor = currentSubmersionFactor,
                SampleIndex = sampleIndex
            };

            PublishSplashFluidImpulse(in splashEvent, absoluteUniversePosition, pointVelocity);

            ImpactSignal impactSignal = new ImpactSignal
            {
                PointAup = AbsoluteUniversePosition.FromAbsolutePosition(absoluteUniversePosition),
                Force = impactSpeedMetersPerSecond * effectiveSampleMass,
                Intensity = math.saturate(kineticEnergyJoules * 0.0005f),
                PrimaryBodyId = _rigidbody != null ? unchecked((uint)EntityId.ToULong(_rigidbody.GetEntityId())) : 0u,
                WeightClass = ResolveSplashWeightClass(kineticEnergyJoules),
                PrimaryMaterialId = 0,
                SecondaryMaterialId = 0,
                Flags = 1
            };
            SignalBus<ImpactSignal>.Push(in impactSignal);
        }

        private static void PublishSplashFluidImpulse(
            in SplashEvent splashEvent,
            double3 absoluteUniversePosition,
            Vector3 pointVelocity)
        {
            float3 velocity = new float3(pointVelocity.x, pointVelocity.y, pointVelocity.z);
            if (!math.all(math.isfinite(velocity)) ||
                !math.all(math.isfinite(absoluteUniversePosition)) ||
                !math.isfinite(splashEvent.KineticEnergyJoules) ||
                !math.isfinite(splashEvent.ImpactSpeedMetersPerSecond))
            {
                return;
            }

            float impact01 = math.saturate(splashEvent.KineticEnergyJoules * 0.0002f);
            float3 impulseVector = new float3(
                velocity.x,
                math.max(splashEvent.ImpactSpeedMetersPerSecond, math.abs(velocity.y)),
                velocity.z);
            float vectorLengthSq = math.lengthsq(impulseVector);
            if (vectorLengthSq <= 0.000001f)
                impulseVector = new float3(0f, math.max(0.1f, splashEvent.ImpactSpeedMetersPerSecond), 0f);

            FluidImpulseSignal impulse = default;
            impulse.PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(absoluteUniversePosition);
            impulse.Vector = impulseVector * math.lerp(0.15f, 0.65f, impact01);
            impulse.Radius = math.lerp(0.75f, 4.5f, impact01);
            impulse.Lifetime = math.lerp(0.2f, 0.9f, impact01);
            impulse.Frame = unchecked((uint)Time.frameCount);
            impulse.SourceHash = 0x53504C48u; // SPLH
            impulse.Flags = 1u;
            SignalBus<FluidImpulseSignal>.Push(in impulse);
        }

        private void QueueSurfacingBreachSignalIfNeeded(Vector3 worldPoint, float sampleHullMass)
        {
            if (_rigidbody == null)
                return;

            Vector3 pointVelocity = _rigidbody.GetPointVelocity(worldPoint);
            if (!IsFiniteVector(pointVelocity))
                return;

            float upwardSpeedMetersPerSecond = math.max(0f, Vector3.Dot(pointVelocity, Vector3.up));
            if (upwardSpeedMetersPerSecond < math.max(0f, surfacingBreachSpeedMetersPerSecond))
                return;

            double3 absoluteUniversePosition = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(worldPoint);
            if (!math.all(math.isfinite(absoluteUniversePosition)))
                return;

            float effectiveSampleMass = math.max(sampleHullMass, Epsilon);
            float kineticEnergyJoules = 0.5f * effectiveSampleMass * upwardSpeedMetersPerSecond * upwardSpeedMetersPerSecond;
            if (!math.isfinite(effectiveSampleMass) || !math.isfinite(kineticEnergyJoules))
                return;

            ImpactSignal impactSignal = new ImpactSignal
            {
                PointAup = AbsoluteUniversePosition.FromAbsolutePosition(absoluteUniversePosition),
                Force = upwardSpeedMetersPerSecond * effectiveSampleMass,
                Intensity = math.saturate(kineticEnergyJoules * 0.00035f),
                PrimaryBodyId = unchecked((uint)EntityId.ToULong(_rigidbody.GetEntityId())),
                WeightClass = ResolveSplashWeightClass(kineticEnergyJoules),
                PrimaryMaterialId = 0,
                SecondaryMaterialId = 0,
                Flags = 2
            };
            SignalBus<ImpactSignal>.Push(in impactSignal);
        }

        private static uint ResolveSplashLcgHash(double3 absoluteUniversePosition, int sampleIndex)
        {
            unchecked
            {
                uint state = 2166136261u;
                state = MixSplashSeed(state, FastFloorToLong(absoluteUniversePosition.x * 16d));
                state = MixSplashSeed(state, FastFloorToLong(absoluteUniversePosition.y * 16d));
                state = MixSplashSeed(state, FastFloorToLong(absoluteUniversePosition.z * 16d));
                state = (state ^ (uint)sampleIndex) * 1664525u + 1013904223u;
                return state;
            }
        }

        private static uint MixSplashSeed(uint state, long value)
        {
            unchecked
            {
                state = (state ^ (uint)value) * 1664525u + 1013904223u;
                return (state ^ (uint)(value >> 32)) * 1664525u + 1013904223u;
            }
        }

        private static long FastFloorToLong(double value)
        {
            long truncated = (long)value;
            return value >= truncated ? truncated : truncated - 1L;
        }

        private static byte ResolveSplashWeightClass(float kineticEnergyJoules)
        {
            if (kineticEnergyJoules >= 2500f)
                return 2;
            if (kineticEnergyJoules >= 450f)
                return 1;
            return 0;
        }

        private void RebuildExteriorBuoyancySampleLocalPoints()
        {
            if (_cachedTransform == null ||
                !_exteriorBuoyancySampleLocalPoints.IsCreated ||
                _exteriorBuoyancySampleLocalPoints.Length < ExteriorBuoyancySampleCount)
            {
                _exteriorBuoyancyMaxLeverArm = 1f;
                return;
            }

            Vector3 centerLocal = SanitizeCenterOfMass(dryCenterOfMassLocal, Vector3.zero);
            Vector3 extentsLocal = Vector3.one;
            if (exteriorHullCollider != null)
            {
                Bounds hullBounds = exteriorHullCollider.bounds;
                if (IsFiniteVector(hullBounds.min) && IsFiniteVector(hullBounds.max))
                {
                    Vector3 localMin = _cachedTransform.InverseTransformPoint(hullBounds.min);
                    Vector3 localMax = _cachedTransform.InverseTransformPoint(hullBounds.max);
                    if (IsFiniteVector(localMin) && IsFiniteVector(localMax))
                    {
                        Vector3 minLocal = Vector3.Min(localMin, localMax);
                        Vector3 maxLocal = Vector3.Max(localMin, localMax);
                        centerLocal = SanitizeCenterOfMass((minLocal + maxLocal) * 0.5f, centerLocal);
                        extentsLocal = maxLocal - minLocal;
                        extentsLocal *= 0.5f;
                    }
                }
            }
            else
            {
                float fallbackVolume = math.max(Epsilon, ResolveExteriorDisplacementVolumeCubicMeters());
                float fallbackHalfExtent = math.max(0.5f, SafeCubeRoot(fallbackVolume) * 0.5f);
                extentsLocal = new Vector3(fallbackHalfExtent, fallbackHalfExtent * 0.6f, fallbackHalfExtent * 1.4f);
            }

            if (!IsFiniteVector(extentsLocal))
                extentsLocal = Vector3.one;

            extentsLocal.x = math.max(0.25f, math.abs(extentsLocal.x) * 0.75f);
            extentsLocal.y = math.max(0.25f, math.abs(extentsLocal.y) * 0.75f);
            extentsLocal.z = math.max(0.25f, math.abs(extentsLocal.z) * 0.75f);

            int sampleIndex = 0;
            for (int ySign = -1; ySign <= 1; ySign += 2)
            {
                for (int xSign = -1; xSign <= 1; xSign += 2)
                {
                    for (int zSign = -1; zSign <= 1; zSign += 2)
                    {
                        _exteriorBuoyancySampleLocalPoints[sampleIndex++] = ToFloat3(centerLocal + new Vector3(
                            extentsLocal.x * xSign,
                            extentsLocal.y * ySign,
                            extentsLocal.z * zSign));
                    }
                }
            }

            float maxLeverArmSq = 0.01f;
            for (int i = 0; i < ExteriorBuoyancySampleCount; i++)
            {
                float leverArmSq = (centerLocal - ToVector3(_exteriorBuoyancySampleLocalPoints[i])).sqrMagnitude;
                if (leverArmSq > maxLeverArmSq)
                    maxLeverArmSq = leverArmSq;
            }

            _exteriorBuoyancyMaxLeverArm = ApproximateSqrtPositive(maxLeverArmSq);
        }

        private float ResolveSurfaceHeightAtSample(Vector3 worldPoint, float fallbackSurfaceY)
        {
            return ResolveSurfaceHeightAtSample(worldPoint, fallbackSurfaceY, ResolveOceanKinematicsProvider());
        }

        private IHectonOceanKinematics ResolveOceanKinematicsProvider()
        {
            IHectonOceanKinematics oceanKinematics = _oceanKinematics;
            if (oceanKinematics != null && oceanKinematics.IsAvailable)
                return oceanKinematics;

            return null;
        }

        private static float ResolveSurfaceHeightAtSample(Vector3 worldPoint, float fallbackSurfaceY, IHectonOceanKinematics oceanKinematics)
        {
            if (oceanKinematics != null &&
                oceanKinematics.IsAvailable &&
                oceanKinematics.TrySampleWaveHeight(new float3(worldPoint.x, worldPoint.y, worldPoint.z), 1f, out float sampledHeight) &&
                float.IsFinite(sampledHeight))
            {
                return sampledHeight;
            }

            return fallbackSurfaceY;
        }

        private float ResolveExteriorDisplacementVolumeCubicMeters()
        {
            if (exteriorDisplacementVolumeCubicMeters > Epsilon)
                return exteriorDisplacementVolumeCubicMeters;

            float compartmentCapacity = ResolveTotalCapacityCubicMeters();
            if (compartmentCapacity > Epsilon)
                return compartmentCapacity;

            if (exteriorHullCollider != null)
            {
                Bounds hullBounds = exteriorHullCollider.bounds;
                Vector3 hullSize = hullBounds.size;
                if (IsFiniteVector(hullSize))
                    return math.max(Epsilon, hullSize.x * hullSize.y * hullSize.z);
            }

            return 0f;
        }

        private float ResolveCargoDraftOffsetMeters()
        {
            float perThousandKg = math.max(0f, cargoDraftMetersPer1000Kg);
            float massThousands = math.max(0f, _totalCargoMassKilograms + _ballastWaterMassKilograms) * 0.001f;
            float draft = massThousands * perThousandKg;
            float clampedDraft = math.clamp(draft, 0f, math.max(0f, maxCargoDraftOffsetMeters));
            _debugCargoDraftOffsetMeters = math.isfinite(clampedDraft) ? clampedDraft : 0f;
            return _debugCargoDraftOffsetMeters;
        }

        private float ResolveCrushDepthBuoyancyScale(float depthMeters)
        {
            float safeDepth = ResolveSafeCrushDepthMeters();
            float scale = depthMeters > safeDepth
                ? math.clamp(crushDepthBuoyancyScale, 0.5f, 1f)
                : 1f;
            _debugCrushBuoyancyScale = scale;
            return scale;
        }

        private float ResolveSafeCrushDepthMeters()
        {
            if (safeCrushDepthMeters > Epsilon)
                return safeCrushDepthMeters;

            ISubmarineRuntimeContext submarine = ResolveSubmarineRuntimeContext();
            if (submarine is SubmarineCoreDirector director)
                return math.max(Epsilon, director.MaxDepth);

            return math.max(Epsilon, hullImplosionDepthThresholdMeters);
        }

        private IPlayerRuntimeContext ResolvePlayerRuntimeContext()
        {
            if (IsUnityObjectInvalid(_playerRuntime))
            {
                _playerRuntime = null;
                return null;
            }

            return _playerRuntime;
        }

        private ISubmarineRuntimeContext ResolveSubmarineRuntimeContext()
        {
            if (IsUnityObjectInvalid(_submarineRuntime))
            {
                _submarineRuntime = null;
                return null;
            }

            return _submarineRuntime;
        }

        private HectonFluidEngine ResolveFluidRuntime()
        {
            if (IsUnityObjectInvalid(_fluidRuntime))
            {
                _fluidRuntime = null;
                return null;
            }

            return _fluidRuntime;
        }

        private IPowerGridService ResolvePowerGridService()
        {
            if (IsUnityObjectInvalid(_powerGridService))
            {
                _powerGridService = null;
                return null;
            }

            return _powerGridService;
        }

        private byte ResolveFloodStateMathLod()
        {
            return _cachedFloodStateMathLod;
        }

        private void RefreshRuntimeActorContextsIfMissing()
        {
            if (_playerRuntime == null || IsUnityObjectInvalid(_playerRuntime))
                _playerRuntime = GlobalRegistry.Player;

            if (_submarineRuntime == null || IsUnityObjectInvalid(_submarineRuntime))
                _submarineRuntime = GlobalRegistry.Submarine;

            if (_fluidRuntime == null || IsUnityObjectInvalid(_fluidRuntime))
                _fluidRuntime = GlobalRegistry.Fluid;
        }

        private void ClearRuntimeServiceCaches()
        {
            _playerRuntime = null;
            _submarineRuntime = null;
            _fluidRuntime = null;
            _powerGridService = null;
            _resourceDistributionRuntime = null;
            _vocalWarningSystem = null;
        }

        private static bool IsUnityObjectInvalid(object context)
        {
            return context is UnityEngine.Object unityObject && unityObject == null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveSurfaceSubmersionFactor(float signedDistanceToSurface)
        {
            return math.saturate(math.smoothstep(0.5f, -0.5f, signedDistanceToSurface));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryResolveSafeNormalizedRatio(float numerator, float denominator, out float ratio)
        {
            ratio = 0f;
            if (math.isnan(numerator) || math.isnan(denominator) ||
                !math.isfinite(numerator) || !math.isfinite(denominator) ||
                denominator <= Epsilon)
            {
                return false;
            }

            float candidate = numerator * math.rcp(denominator);
            if (math.isnan(candidate) || !math.isfinite(candidate))
                return false;

            ratio = math.saturate(candidate);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SafeCubeRoot(float value)
        {
            float safeValue = math.max(0f, value);
            if (safeValue <= 0f)
                return 0f;

            float estimate = math.asfloat((math.asint(safeValue) / 3) + 709921077);
            float estimateSq = math.max(estimate * estimate, 0.000001f);
            estimate = ((estimate + estimate) + safeValue * math.rcp(estimateSq)) * 0.33333334f;
            return math.isfinite(estimate) ? estimate : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryResolveSafeQuotient(float numerator, float denominator, out float quotient)
        {
            quotient = 0f;
            if (math.isnan(numerator) || math.isnan(denominator) ||
                !math.isfinite(numerator) || !math.isfinite(denominator) ||
                math.abs(denominator) <= Epsilon)
            {
                return false;
            }

            float candidate = numerator * math.rcp(denominator);
            if (math.isnan(candidate) || !math.isfinite(candidate))
                return false;

            quotient = candidate;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryResolveSafeVectorDivision(float3 numerator, float denominator, out float3 quotient)
        {
            quotient = float3.zero;
            if (!TryResolveSafeQuotient(1f, denominator, out float inverseDenominator))
                return false;

            float3 candidate = numerator * inverseDenominator;
            if (math.any(math.isnan(candidate)) || !math.all(math.isfinite(candidate)))
                return false;

            quotient = candidate;
            return true;
        }

        private float ResolveExternalDepthMeters()
        {
            if (sampleDepthFromAtmosphere)
            {
                HectonAtmosphereManager atmosphereManager = Hecton8.Core.GlobalRegistry.Atmosphere;
                if (atmosphereManager != null && _cachedTransform != null)
                {
                    float depthMeters = atmosphereManager.SeaLevelY - _cachedTransform.position.y;
                    _externalDepthMeters = math.isfinite(depthMeters) ? math.max(0f, depthMeters) : 0f;
                    return _externalDepthMeters;
                }
            }

            _externalDepthMeters = math.isfinite(manualExternalDepthMeters) ? math.max(0f, manualExternalDepthMeters) : 0f;
            return _externalDepthMeters;
        }

        private void RefreshDerivedConstants(float fixedDeltaTime)
        {
            float safeFixedStep = fixedDeltaTime > 0f ? fixedDeltaTime : DefaultFixedStepSeconds;
            if (math.abs(_reportedCenterBlendFixedStep - safeFixedStep) > 0.0001f)
            {
                float reportedTau = math.max(0.1f, reportedCenterTauSeconds);
                _reportedCenterBlendAlpha = ResolveBlendFactor(reportedTau, safeFixedStep);
                _reportedCenterBlendFixedStep = safeFixedStep;
            }

            if (math.abs(_centerOfMassBlendFixedStep - safeFixedStep) > 0.0001f)
            {
                float centerTau = math.max(0.1f, centerOfMassTauSeconds);
                _centerOfMassBlendAlpha = ResolveBlendFactor(centerTau, safeFixedStep);
                _centerOfMassBlendFixedStep = safeFixedStep;
            }
        }

        private static float ResolveBlendFactor(float tauSeconds, float deltaTime)
        {
            if (tauSeconds <= 0f || deltaTime <= 0f)
                return 0f;

            if (!TryResolveSafeQuotient(deltaTime, tauSeconds, out float normalizedStep))
                return 0f;

            float candidate = normalizedStep * math.rcp(1f + normalizedStep);
            return math.isfinite(candidate) ? math.saturate(candidate) : 0f;
        }

        private void RefreshResolvedInertiaTensors()
        {
            _resolvedDryInertiaTensor = SanitizeTensor(dryInertiaTensor);
            _resolvedFloodedInertiaTensor = SanitizeTensor(fullyFloodedInertiaTensor);
            dryInertiaTensor = _resolvedDryInertiaTensor;
            fullyFloodedInertiaTensor = _resolvedFloodedInertiaTensor;
        }

        private void RefreshDebugState()
        {
            _debugConfiguredCompartmentCount = _configuredCompartmentCount;
            _debugConfiguredBulkheadCount = _configuredBulkheadCount;
            _debugExternalDepthMeters = _externalDepthMeters;
            _debugTotalFloodVolumeCubicMeters = _totalFloodVolumeCubicMeters;
            _debugFloodFillRatio = _floodFillRatio;
            _debugReportedFloodCenterOfMassLocal = _reportedFloodCenterOfMassLocal;
            _debugAppliedCenterOfMassLocal = _appliedCenterOfMassLocal;
            _debugAppliedInertiaTensor = _lastAppliedInertiaTensor;
            _debugAppliedRigidbodyMass = _lastAppliedRigidbodyMass;
            _debugFloodMassKilograms = math.max(0f, _totalFloodVolumeCubicMeters) * WaterDensityKgPerCubicMeter;
            _debugLastSloshTorqueLocal = _lastSloshTorqueLocal;
            _debugExternalSubmergedVolumeCubicMeters = _externalSubmergedVolumeCubicMeters;
            _debugLastExternalBuoyancyForce = _lastExternalBuoyancyForce;
            _debugLastExternalBuoyancyTorque = _lastExternalBuoyancyTorque;
            _debugAppliedLinearDamping = _lastAppliedLinearDamping;
            _debugAppliedAngularDamping = _lastAppliedAngularDamping;
            _debugSubmersionFactor = _submersionFactor;
            _debugHullImplosionActive = _hullImplosionActive;
            _debugExternalPressureKPa = ResolveExternalPressureKPa(_externalDepthMeters);
            _debugFloraDragDensity = _currentFloraDragDensity01;
            _debugFloraAddedMassKilograms = _currentFloraAddedMassKilograms;
            _debugTotalCargoMassKilograms = _totalCargoMassKilograms;
            _debugBallastWaterMassKilograms = _ballastWaterMassKilograms;
            _debugDockedExternalMassKilograms = _dockedExternalMassKilograms;
            _debugDamageControlLeakAddedMassKilograms = _damageControlLeakAddedMassKilograms;
            _debugCargoMassScalar = _cargoMassScalar;
            _debugTargetBuoyancyBias01 = _targetBuoyancyBias01;
            _debugCompressedAirUnits = compressedAirUnits;
        }

        private void SeedFloodMassPropertiesBuffers(float3 targetFloodCenter, float floodMassRatio)
        {
            if (!_massPropertiesFront.IsCreated || !_massPropertiesBack.IsCreated)
                return;

            Vector3 targetTensor = LerpVector3ClampedMath(_resolvedDryInertiaTensor, _resolvedFloodedInertiaTensor, floodMassRatio);
            float floodMassKilograms = math.max(0f, _totalFloodVolumeCubicMeters) * WaterDensityKgPerCubicMeter;
            FloodMassPropertiesResult result = new FloodMassPropertiesResult
            {
                FloodMassKilograms = floodMassKilograms,
                FloodMassRatio = math.saturate(floodMassRatio),
                FloodCenterLocal = targetFloodCenter,
                TargetCenterLocal = targetFloodCenter,
                InertiaTensor = new float3(targetTensor.x, targetTensor.y, targetTensor.z)
            };

            _massPropertiesFront[0] = result;
            _massPropertiesBack[0] = result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ResolveTotalCapacityCubicMeters()
        {
            if (!_compartmentMaxVolumes.IsCreated || _configuredCompartmentCount <= 0)
                return 0f;

            float totalCapacity = 0f;
            for (int i = 0; i < _configuredCompartmentCount; i++)
            {
                float compartmentCapacity = _compartmentMaxVolumes[i];
                if (!math.isfinite(compartmentCapacity) || compartmentCapacity <= 0f)
                    continue;

                totalCapacity += compartmentCapacity;
            }

            return totalCapacity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasActiveHydrodynamicsConfiguration()
        {
            return _configuredCompartmentCount > 0 ||
                   exteriorHullCollider != null ||
                   (math.isfinite(exteriorDisplacementVolumeCubicMeters) && exteriorDisplacementVolumeCubicMeters > Epsilon);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ResolveExternalPressureKPa(float depthMeters)
        {
            float hydrostaticPressureKPa = math.max(0f, depthMeters) *
                WaterDensityKgPerCubicMeter *
                GravityMetersPerSecondSquared *
                0.001f;
            float pressureKPa = math.max(1f, externalReferencePressureKPa) + hydrostaticPressureKPa;
            return math.isfinite(pressureKPa) ? pressureKPa : math.max(1f, externalReferencePressureKPa);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ResolveEstimatedHullSurfaceAreaSquareMeters()
        {
            if (exteriorHullCollider != null)
            {
                Bounds hullBounds = exteriorHullCollider.bounds;
                Vector3 hullSize = hullBounds.size;
                if (IsFiniteVector(hullSize))
                {
                    float x = math.max(Epsilon, hullSize.x);
                    float y = math.max(Epsilon, hullSize.y);
                    float z = math.max(Epsilon, hullSize.z);
                    return 2f * ((x * y) + (x * z) + (y * z));
                }
            }

            float displacementVolume = ResolveExteriorDisplacementVolumeCubicMeters();
            if (!math.isfinite(displacementVolume) || displacementVolume <= Epsilon)
                displacementVolume = math.max(Epsilon, ResolveTotalCapacityCubicMeters());

            float characteristicLength = math.max(Epsilon, SafeCubeRoot(displacementVolume));
            return 6f * characteristicLength * characteristicLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindBulkheadIndex(int compartmentA, int compartmentB)
        {
            for (int i = 0; i < _configuredBulkheadCount; i++)
            {
                int2 pair = _bulkheadPairs[i];
                if ((pair.x == compartmentA && pair.y == compartmentB) ||
                    (pair.x == compartmentB && pair.y == compartmentA))
                {
                    return i;
                }
            }

            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 SanitizeTensor(Vector3 tensor)
        {
            tensor.x = float.IsFinite(tensor.x) ? math.max(0.001f, tensor.x) : 0.001f;
            tensor.y = float.IsFinite(tensor.y) ? math.max(0.001f, tensor.y) : 0.001f;
            tensor.z = float.IsFinite(tensor.z) ? math.max(0.001f, tensor.z) : 0.001f;
            return tensor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 SanitizeCenterOfMass(Vector3 value, Vector3 fallback)
        {
            if (IsFiniteVector(value))
                return value;

            return IsFiniteVector(fallback) ? fallback : Vector3.zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 ClampMagnitude(Vector3 value, float maxMagnitude)
        {
            float maxMagnitudeSq = maxMagnitude * maxMagnitude;
            float magnitudeSq = value.sqrMagnitude;
            if (magnitudeSq <= maxMagnitudeSq || magnitudeSq <= Epsilon)
                return value;

            float magnitude = ApproximateMagnitude(value);
            return value * (maxMagnitude * math.rcp(math.max(magnitude, Epsilon)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 SafeNormalize(Vector3 value, Vector3 fallback)
        {
            float magnitudeSq = value.sqrMagnitude;
            if (magnitudeSq <= Epsilon)
            {
                float fallbackMagnitudeSq = fallback.sqrMagnitude;
                return fallbackMagnitudeSq > Epsilon ? DominantAxisOrDefault(fallback, Vector3.up) : Vector3.up;
            }

            return DominantAxisOrDefault(value, fallback);
        }

        private static float ApproximateSqrtPositive(float value)
        {
            float safeValue = math.max(0f, value);
            if (safeValue <= 0f)
                return 0f;

            float magnitude = safeValue * math.rsqrt(math.max(safeValue, 0.000001f));
            return math.isfinite(magnitude) ? magnitude : 0f;
        }

        private static float ApproximateMagnitude(float3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float maxAxis = math.max(ax, math.max(ay, az));
            float minAxis = math.min(ax, math.min(ay, az));
            float midAxis = ax + ay + az - maxAxis - minAxis;
            return maxAxis + (midAxis * 0.375f) + (minAxis * 0.125f);
        }

        private static float ApproximateMagnitude(Vector3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float maxAxis = math.max(ax, math.max(ay, az));
            float minAxis = math.min(ax, math.min(ay, az));
            float midAxis = ax + ay + az - maxAxis - minAxis;
            return maxAxis + (midAxis * 0.375f) + (minAxis * 0.125f);
        }

        private static Vector3 DominantAxisOrDefault(Vector3 value, Vector3 fallback)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float maxComponent = math.max(ax, math.max(ay, az));
            if (maxComponent <= Epsilon)
                return fallback;

            if (ax >= ay && ax >= az)
                return new Vector3(value.x >= 0f ? 1f : -1f, 0f, 0f);

            if (ay >= az)
                return new Vector3(0f, value.y >= 0f ? 1f : -1f, 0f);

            return new Vector3(0f, 0f, value.z >= 0f ? 1f : -1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsWithinRadiusSq(Vector3 value, Vector3 center, float radius)
        {
            if (!float.IsFinite(radius) || radius <= 0f)
                return false;

            float radiusSq = radius * radius;
            float distanceSq = (value - center).sqrMagnitude;
            return float.IsFinite(distanceSq) && distanceSq <= radiusSq;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveDistanceSqFalloff01(Vector3 value, Vector3 center, float radius)
        {
            if (!float.IsFinite(radius) || radius <= 0f)
                return 0f;

            float radiusSq = math.max(radius * radius, Epsilon);
            float distanceSq = (value - center).sqrMagnitude;
            if (!float.IsFinite(distanceSq) || distanceSq >= radiusSq)
                return 0f;

            return 1f - math.saturate(distanceSq * math.rcp(radiusSq));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 LerpVector3ClampedMath(Vector3 from, Vector3 to, float t)
        {
            float3 from3 = new float3(from.x, from.y, from.z);
            float3 to3 = new float3(to.x, to.y, to.z);
            float safeT = math.isfinite(t) ? math.saturate(t) : 0f;
            float3 value = LerpMad(from3, to3, safeT);
            return new Vector3(value.x, value.y, value.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float LerpMad(float from, float to, float t)
        {
            return from + (to - from) * t;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 LerpMad(float3 from, float3 to, float t)
        {
            return from + (to - from) * t;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFiniteVector(Vector3 value)
        {
            return !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) ||
                     float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeNonNegativeFinite(float value)
        {
            return float.IsFinite(value) ? math.max(0f, value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsCompartmentIndexValid(int index)
        {
            return index >= 0 && index < _configuredCompartmentCount;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            exteriorDisplacementVolumeCubicMeters = math.max(0f, exteriorDisplacementVolumeCubicMeters);
            exteriorBuoyancyForceClampScale = math.clamp(exteriorBuoyancyForceClampScale, 1f, 2f);
            exteriorBuoyancyTorqueClampScale = math.clamp(exteriorBuoyancyTorqueClampScale, 1f, 3f);
            maxCargoMassKilograms = math.max(0f, maxCargoMassKilograms);
            damageControlLeakMassKilogramsPerKpaSecond = math.max(0f, damageControlLeakMassKilogramsPerKpaSecond);
            cargoDraftMetersPer1000Kg = math.max(0f, cargoDraftMetersPer1000Kg);
            maxCargoDraftOffsetMeters = math.max(0f, maxCargoDraftOffsetMeters);
            safeCrushDepthMeters = math.max(0f, safeCrushDepthMeters);
            crushDepthBuoyancyScale = math.clamp(crushDepthBuoyancyScale, 0.5f, 1f);
            forwardHydroDragCoefficient = math.max(0f, forwardHydroDragCoefficient);
            lateralHydroDragMultiplier = math.max(1f, lateralHydroDragMultiplier);
            verticalHydroDragCoefficient = math.max(0f, verticalHydroDragCoefficient);
            angularHydroDragCoefficient = math.max(0f, angularHydroDragCoefficient);
            pitchRollRightingTorqueCoefficient = math.max(0f, pitchRollRightingTorqueCoefficient);
            hydroSolverMaxAcceleration = math.max(0f, hydroSolverMaxAcceleration);
            hydroSolverMaxTorque = math.max(0f, hydroSolverMaxTorque);
            compressedAirUnits = math.max(0f, compressedAirUnits);
            ballastBlowAirCost = math.max(0f, ballastBlowAirCost);
            ballastBlowDurationSeconds = math.max(0.05f, ballastBlowDurationSeconds);
            ballastBlowUpAcceleration = math.max(0f, ballastBlowUpAcceleration);
            surfacingBreachSpeedMetersPerSecond = math.max(0f, surfacingBreachSpeedMetersPerSecond);
            cavitationThrottleThreshold = math.saturate(cavitationThrottleThreshold);
            cavitationStallSpeedMetersPerSecond = math.max(0f, cavitationStallSpeedMetersPerSecond);
            cavitationCooldownSeconds = math.max(0f, cavitationCooldownSeconds);
            hullImplosionDepthThresholdMeters = math.max(0f, hullImplosionDepthThresholdMeters);
            hullPressureRatingKPa = math.max(1f, hullPressureRatingKPa);
            hullImplosionBreachAreaNormalized = math.saturate(hullImplosionBreachAreaNormalized);
            implosionDragBonus = math.max(0f, implosionDragBonus);
            addedMassLinearDampingScale = math.saturate(addedMassLinearDampingScale);
            addedMassAngularDampingScale = math.saturate(addedMassAngularDampingScale);

            if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode &&
                !UnityEditor.EditorApplication.isCompiling &&
                !UnityEditor.EditorApplication.isUpdating)
            {
                CacheReferences();
                RebuildExteriorBuoyancySampleLocalPoints();
            }
        }
#endif

        private void WriteHydroBlackBoxSample(float depthMeters, uint reasonFlags)
        {
            if (!_hydroBlackBox.IsCreated)
                return;

            int index = _hydroBlackBoxCursor;
            if ((uint)index >= (uint)_hydroBlackBox.Length)
                index = 0;

            Vector3 position = _cachedTransform != null ? _cachedTransform.position : Vector3.zero;
            Vector3 velocity = _rigidbody != null && IsFiniteVector(_rigidbody.linearVelocity) ? _rigidbody.linearVelocity : Vector3.zero;
            Vector3 angularVelocity = _rigidbody != null && IsFiniteVector(_rigidbody.angularVelocity) ? _rigidbody.angularVelocity : Vector3.zero;
            float mass = _rigidbody != null && math.isfinite(_rigidbody.mass) ? math.max(Epsilon, _rigidbody.mass) : 0f;
            uint flags = ResolveHydroBlackBoxRuntimeFlags(reasonFlags);
            uint stateHash = BuildHydroBlackBoxHash(position, velocity, angularVelocity, mass, depthMeters, flags);

            _hydroBlackBox[index] = new HydroBlackBoxEntry
            {
                Frame = Time.frameCount,
                FixedTime = Time.fixedTime,
                Position = ToFloat3(position),
                Velocity = ToFloat3(velocity),
                AngularVelocity = ToFloat3(angularVelocity),
                MassKilograms = mass,
                CargoMassKilograms = _totalCargoMassKilograms + _ballastWaterMassKilograms,
                CargoMassScalar = _cargoMassScalar,
                SubmersionFactor = _submersionFactor,
                DepthMeters = math.isfinite(depthMeters) ? math.max(0f, depthMeters) : 0f,
                FloodRatio = _floodFillRatio,
                BallastBias01 = _targetBuoyancyBias01,
                HydroAcceleration = ToFloat3(_debugHydroDragAcceleration),
                HydroTorque = ToFloat3(_debugHydroTorque),
                TowingTension = ToFloat3(_pendingTowingTensionVector),
                BrineSubmersionTime = math.max(0f, _brineSubmersionTime),
                Flags = flags,
                StateHash = stateHash
            };

            _hydroBlackBoxCursor = (index + 1) % _hydroBlackBox.Length;
        }

        private uint ResolveHydroBlackBoxRuntimeFlags(uint reasonFlags)
        {
            uint flags = reasonFlags;
            if (_hullImplosionActive)
                flags |= HydroBlackBoxFlagHullImplosion;
            if (_ballastBlowTimer > Epsilon || _targetBuoyancyBias01 > Epsilon)
                flags |= HydroBlackBoxFlagBallastBlow;
            if (_towingTensionHoldTimer > Epsilon || (_pendingTowingTensionVector.sqrMagnitude > Epsilon && IsFiniteVector(_pendingTowingTensionVector)))
                flags |= HydroBlackBoxFlagTowingTension;
            if (_debugCavitationActive)
                flags |= HydroBlackBoxFlagCavitation;
            if (_isBrineSubmerged)
                flags |= HydroBlackBoxFlagBrineSubmerged;

            return flags;
        }

        private uint BuildHydroBlackBoxHash(Vector3 position, Vector3 velocity, Vector3 angularVelocity, float mass, float depthMeters, uint flags)
        {
            uint hash = 2166136261u;
            hash = HashHydroBlackBox(hash, (uint)Time.frameCount);
            hash = HashHydroBlackBox(hash, QuantizeHydroBlackBox(position.x));
            hash = HashHydroBlackBox(hash, QuantizeHydroBlackBox(position.y));
            hash = HashHydroBlackBox(hash, QuantizeHydroBlackBox(position.z));
            hash = HashHydroBlackBox(hash, QuantizeHydroBlackBox(velocity.x));
            hash = HashHydroBlackBox(hash, QuantizeHydroBlackBox(velocity.y));
            hash = HashHydroBlackBox(hash, QuantizeHydroBlackBox(velocity.z));
            hash = HashHydroBlackBox(hash, QuantizeHydroBlackBox(angularVelocity.x));
            hash = HashHydroBlackBox(hash, QuantizeHydroBlackBox(angularVelocity.y));
            hash = HashHydroBlackBox(hash, QuantizeHydroBlackBox(angularVelocity.z));
            hash = HashHydroBlackBox(hash, QuantizeHydroBlackBox(mass));
            hash = HashHydroBlackBox(hash, QuantizeHydroBlackBox(depthMeters));
            hash = HashHydroBlackBox(hash, QuantizeHydroBlackBox(_submersionFactor));
            hash = HashHydroBlackBox(hash, QuantizeHydroBlackBox(_floodFillRatio));
            hash = HashHydroBlackBox(hash, QuantizeHydroBlackBox(_brineSubmersionTime));
            hash = HashHydroBlackBox(hash, flags);
            return hash;
        }

        private void DumpHydroBlackBoxOnce(uint reasonFlags)
        {
            if (_hydroBlackBoxDumped || !_hydroBlackBox.IsCreated)
                return;

            _hydroBlackBoxDumped = true;
            WriteHydroBlackBoxSample(ResolveExternalDepthMeters(), reasonFlags);
            DumpHydroBlackBox(reasonFlags);
        }

        private void DumpHydroBlackBox(uint reasonFlags)
        {
            if (!_hydroBlackBox.IsCreated)
                return;

            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                WriteHydroBlackBoxDumpFile(projectRoot, HydroBlackBoxDumpRelativePath, reasonFlags);
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    HydrodynamicsResetWarningHash,
                    SubmarineFluidDynamicsContextHash,
                    1f);
            }
        }

        private void WriteHydroBlackBoxDumpFile(string projectRoot, string relativePath, uint reasonFlags)
        {
            string dumpPath = Path.Combine(projectRoot, relativePath);
            string directory = Path.GetDirectoryName(dumpPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(HydroBlackBoxMagic);
                writer.Write((uint)HydroBlackBoxCapacity);
                writer.Write((uint)_hydroBlackBoxCursor);
                writer.Write(reasonFlags);

                for (int i = 0; i < _hydroBlackBox.Length; i++)
                {
                    int index = (_hydroBlackBoxCursor + i) % _hydroBlackBox.Length;
                    WriteHydroBlackBoxEntry(writer, _hydroBlackBox[index]);
                }
            }
        }

        private static void WriteHydroBlackBoxEntry(BinaryWriter writer, HydroBlackBoxEntry entry)
        {
            writer.Write(entry.Frame);
            writer.Write(entry.FixedTime);
            WriteFloat3(writer, entry.Position);
            WriteFloat3(writer, entry.Velocity);
            WriteFloat3(writer, entry.AngularVelocity);
            writer.Write(entry.MassKilograms);
            writer.Write(entry.CargoMassKilograms);
            writer.Write(entry.CargoMassScalar);
            writer.Write(entry.SubmersionFactor);
            writer.Write(entry.DepthMeters);
            writer.Write(entry.FloodRatio);
            writer.Write(entry.BallastBias01);
            WriteFloat3(writer, entry.HydroAcceleration);
            WriteFloat3(writer, entry.HydroTorque);
            WriteFloat3(writer, entry.TowingTension);
            writer.Write(entry.BrineSubmersionTime);
            writer.Write(entry.Flags);
            writer.Write(entry.StateHash);
        }

        private static void WriteFloat3(BinaryWriter writer, float3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ToFloat3(Vector3 value)
        {
            return IsFiniteVector(value) ? new float3(value.x, value.y, value.z) : float3.zero;
        }

        private static Vector3 ToVector3(float3 value)
        {
            return math.all(math.isfinite(value)) ? new Vector3(value.x, value.y, value.z) : Vector3.zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint QuantizeHydroBlackBox(float value)
        {
            if (!math.isfinite(value) || math.isnan(value))
                return 0u;

            return unchecked((uint)(int)math.round(value * 1000f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HashHydroBlackBox(uint hash, uint value)
        {
            return unchecked((hash ^ value) * 16777619u);
        }

        private void EnsureNativeStateBuffer<T>(
            ref VaultNativeBuffer<T> buffer,
            BufferID bufferId,
            int requiredLength,
            string label,
            int vaultFlag) where T : struct
        {
            _ = label;
            if (buffer.Ensure(_dataVault, bufferId, requiredLength))
                _vaultNativeStateMask |= vaultFlag;
            else
                _vaultNativeStateMask &= ~vaultFlag;
        }

        private void RefreshNativeStateViewsFromVault()
        {
            RefreshNativeStateBuffer(ref _compartmentFloodVolumes, VaultCompartmentFloodVolumesFlag);
            RefreshNativeStateBuffer(ref _compartmentViscosity01, VaultCompartmentViscosityFlag);
            RefreshNativeStateBuffer(ref _compartmentBaseMaxVolumes, VaultCompartmentBaseMaxVolumesFlag);
            RefreshNativeStateBuffer(ref _compartmentMaxVolumes, VaultCompartmentMaxVolumesFlag);
            RefreshNativeStateBuffer(ref _compartmentBreachAreas, VaultCompartmentBreachAreasFlag);
            RefreshNativeStateBuffer(ref _compartmentLocalCentroids, VaultCompartmentLocalCentroidsFlag);
            RefreshNativeStateBuffer(ref _compartmentFlags, VaultCompartmentFlagsFlag);
            RefreshNativeStateBuffer(ref _bulkheadPairs, VaultBulkheadPairsFlag);
            RefreshNativeStateBuffer(ref _bulkheadSealed, VaultBulkheadSealedFlag);
            RefreshNativeStateBuffer(ref _bulkheadDoorAreas, VaultBulkheadDoorAreasFlag);
            RefreshNativeStateBuffer(ref _comAccumulatorFront, VaultComAccumulatorFrontFlag);
            RefreshNativeStateBuffer(ref _comAccumulatorBack, VaultComAccumulatorBackFlag);
            RefreshNativeStateBuffer(ref _massPropertiesFront, VaultMassPropertiesFrontFlag);
            RefreshNativeStateBuffer(ref _massPropertiesBack, VaultMassPropertiesBackFlag);
            RefreshNativeStateBuffer(ref _angularVelocityHistoryLocal, VaultAngularVelocityHistoryFlag);
            RefreshNativeStateBuffer(ref _previousExteriorSampleSubmersionFactors, VaultExteriorSubmersionHistoryFlag);
            RefreshNativeStateBuffer(ref _exteriorBuoyancySampleLocalPoints, VaultExteriorBuoyancySamplesFlag);
            RefreshNativeStateBuffer(ref _compartmentStates, VaultCompartmentStatesFlag);
            RefreshNativeStateBuffer(ref _jobFloodVolumes, VaultJobFloodVolumesFlag);
            RefreshNativeStateBuffer(ref _jobCompartmentFlags, VaultJobCompartmentFlagsFlag);
            RefreshNativeStateBuffer(ref _bulkheadTransferDeltas, VaultBulkheadTransferDeltasFlag);
            RefreshNativeStateBuffer(ref _hydroKinematicInput, VaultHydroInputFlag);
            RefreshNativeStateBuffer(ref _hydroKinematicOutput, VaultHydroOutputFlag);
            RefreshNativeStateBuffer(ref _hydroBlackBox, VaultHydroBlackBoxFlag);
            RefreshNativeStateBuffer(ref _exteriorThermalAnomalyCenters, VaultExteriorThermalCentersFlag);
            RefreshNativeStateBuffer(ref _exteriorThermalAnomalyTemperatures, VaultExteriorThermalTemperaturesFlag);
            RefreshNativeStateBuffer(ref _exteriorThermalAnomalyLifetimes, VaultExteriorThermalLifetimesFlag);
            RefreshNativeStateBuffer(ref _exteriorThermalHazardIds, VaultExteriorThermalHazardIdsFlag);
        }

        private void RefreshNativeStateBuffer<T>(ref VaultNativeBuffer<T> buffer, int vaultFlag) where T : struct
        {
            if (buffer.Refresh(_dataVault))
                _vaultNativeStateMask |= vaultFlag;
            else
                _vaultNativeStateMask &= ~vaultFlag;
        }

        private void ClearNativeStateViews()
        {
            _compartmentFloodVolumes = default;
            _compartmentViscosity01 = default;
            _compartmentBaseMaxVolumes = default;
            _compartmentMaxVolumes = default;
            _compartmentBreachAreas = default;
            _compartmentLocalCentroids = default;
            _compartmentFlags = default;
            _bulkheadPairs = default;
            _bulkheadSealed = default;
            _bulkheadDoorAreas = default;
            _comAccumulatorFront = default;
            _comAccumulatorBack = default;
            _massPropertiesFront = default;
            _massPropertiesBack = default;
            _angularVelocityHistoryLocal = default;
            _previousExteriorSampleSubmersionFactors = default;
            _exteriorBuoyancySampleLocalPoints = default;
            _compartmentStates = default;
            _jobFloodVolumes = default;
            _jobCompartmentFlags = default;
            _bulkheadTransferDeltas = default;
            _hydroKinematicInput = default;
            _hydroKinematicOutput = default;
            _hydroBlackBox = default;
            _exteriorThermalAnomalyCenters = default;
            _exteriorThermalAnomalyTemperatures = default;
            _exteriorThermalAnomalyLifetimes = default;
            _exteriorThermalHazardIds = default;
        }

        private void DisposeNativeStateBuffer<T>(ref VaultNativeBuffer<T> buffer, int vaultFlag) where T : struct
        {
            buffer.Clear();
            _vaultNativeStateMask &= ~vaultFlag;
        }

        private static void SwapNativeStateBuffers<T>(
            ref VaultNativeBuffer<T> first,
            ref VaultNativeBuffer<T> second) where T : struct
        {
            VaultNativeBuffer<T> temp = first;
            first = second;
            second = temp;
        }

    }
}
