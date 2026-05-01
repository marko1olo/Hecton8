using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Hecton8.Atmosphere;
using Hecton8.Bootstrap;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    /// <summary>
    /// Deferred exterior water-entry payload emitted by sampled hull buoyancy points.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SplashEvent
    {
        /// <summary>Camera-relative world position of the splash contact point.</summary>
        public float3 RuntimePosition;
        /// <summary>Absolute universe position (AUP) of the splash for persistent VFX anchoring.</summary>
        public float3 AbsoluteUniversePosition;
        /// <summary>Water surface normal at the splash point.</summary>
        public float3 SurfaceNormal;
        /// <summary>Vertical impact speed at the moment of water entry.</summary>
        public float ImpactSpeedMetersPerSecond;
        /// <summary>Kinetic energy of the impact in joules, used to scale splash VFX intensity.</summary>
        public float KineticEnergyJoules;
        /// <summary>0â€“1 ratio of the sample point submerged below the waterline at impact.</summary>
        public float SubmersionFactor;
        /// <summary>Index of the exterior buoyancy sample point that detected the splash.</summary>
        public int SampleIndex;
    }

    /// <summary>
    /// Fixed-step flooded-interior model for submarine rigidbodies.
    /// Tracks compartment fill, bulkhead isolation, flood-mass coupling, center-of-mass shifting,
    /// inertia blending, and delayed slosh torque.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Hecton/Physics/Submarine Fluid Dynamics")]
    public sealed class SubmarineFluidDynamics : MonoBehaviour, IFixedTickable, IPostFixedTickable, IOriginShiftListener
    {
        private const int CompartmentCapacity = 8;
        private const int BulkheadCapacity = 7;
        private const int RingBufferLength = 8;
        private const int RingBufferMask = RingBufferLength - 1;
        private const int SloshDelayFrames = 3;
        private const float WaterDensityKgPerCubicMeter = 1025f;
        private const float GravityMetersPerSecondSquared = 9.81f;
        private const float DefaultFixedStepSeconds = 0.02f;
        private const float DefaultDischargeCoefficient = 0.62f;
        private const float DefaultBulkheadFlowCoefficient = 0.4f;
        private const float DefaultBulkheadDoorAreaSquareMeters = 1.6f;
        private const float DefaultMaxTransferPerTick = 0.1f;
        private const float DefaultNearZeroHeadDampingMeters = 0.15f;
        private const float DefaultExternalReferencePressureKPa = 101.325f;
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
        private const float DefaultHydraulicLeakRateCubicMetersPerSecond = 0.006f;
        private const float DefaultMaximumHydraulicViscosity = 1f;
        private const float DefaultViscositySloshDampingScale = 0.85f;
        private const float DefaultSludgePlayerDragMultiplier = 3.2f;
        private const float DefaultFloraDragAddedMassAtFullDensityKilograms = 4000f;
        private const float DefaultFloraCenterOfMassDownshiftMeters = 0.35f;
        private const float DefaultExteriorBuoyancyForceClampScale = 1.15f;
        private const float DefaultExteriorBuoyancyTorqueClampScale = 1.25f;
        private const int ExteriorBuoyancySampleCount = 8;
        private const int MaxQueuedSplashEvents = 32;
        private const int ExteriorThermalAnomalyCapacity = 8;
        private const int ExteriorThermalContactCapacity = 16;
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

        // Inspector-authored DTO. Unity serialization populates these fields outside constructor flow.
#pragma warning disable CS0649
        [System.Serializable]
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

        [StructLayout(LayoutKind.Sequential)]
        private struct CompartmentState
        {
            public float currentVolume;
            public float maxVolume;
            public float3 localCentroid;
            public uint stateFlags;
        }
#pragma warning restore CS0649

        [Header("â”€â”€ Compartments â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Authored compartment capacities, breach openings, and local centroids. Maximum supported count is eight.")]
        [SerializeField] private CompartmentDefinition[] compartments = new CompartmentDefinition[CompartmentCapacity];

        [Tooltip("Adjacency map for water transfer. If empty, a linear bow-to-stern chain is generated.")]
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
        [SerializeField] private Vector3 _debugLastThermalAnomalyCenter;
        [SerializeField] private float _debugLastThermalAnomalyTemperature;
        [SerializeField] private float _debugLastThermalAnomalyDepth;

        private Rigidbody _rigidbody;
        private Transform _cachedTransform;
        private Transform _cachedPlayerTransform;
        private HectonPlayerMovement _cachedPlayerMovement;
        private Rigidbody _cachedPlayerRigidbody;
        private bool _registered;
        private bool _registeredOriginShiftListener;
        private bool _fluidJobRunning;
        private bool _skipHydrodynamicsForCurrentFixedTick;
        private int _configuredCompartmentCount;
        private int _configuredBulkheadCount;
        private int _ringHead;
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
        private Vector3 _reportedFloodCenterOfMassLocal;
        private Vector3 _appliedCenterOfMassLocal;
        private Vector3 _currentFloodCenterOfMassLocal;
        private Vector3 _resolvedDryInertiaTensor;
        private Vector3 _resolvedFloodedInertiaTensor;
        private Vector3 _lastAppliedInertiaTensor;
        private Vector3 _lastSloshTorqueLocal;
        private Vector3 _lastExternalBuoyancyForce;
        private Vector3 _lastExternalBuoyancyTorque;
        private float _currentHydrodynamicLinearInertiaScale = 1f;
        private float _currentHydrodynamicAngularInertiaScale = 1f;
        private JobHandle _disposeHandle;
        private JobHandle _fluidJobHandle;
        private JobHandle _massPropertiesJobHandle;
        private bool _baselineDampingCached;
        private bool _baselineMassCached;
        private bool _massPropertiesJobRunning;
        private bool _hullImplosionActive;
        private int _queuedSplashEventCount;
        private CompartmentState[] _compartmentStates;
        private SubmarineAtmosphereSystem _atmosphereSystem;
        private ISubmarineHullBreachReadModel _structuralBreachReadModel;
        private IHectonOceanKinematics _oceanKinematics;
        private readonly List<MonoBehaviour> _componentSearchBuffer = new List<MonoBehaviour>(4); // COLD ALLOC: List<MonoBehaviour>(4) â€” local component search scratch for interface-only structural breach wiring â€” owner: SubmarineFluidDynamics
        private readonly List<LogisticsPipeNode> _pipeBindingBuffer = new List<LogisticsPipeNode>(16); // COLD ALLOC: List<LogisticsPipeNode>(16) — rare cold-path pipe rupture propagation cache — owner: SubmarineFluidDynamics
        // COLD ALLOC: Vector3[8] â€” cached local buoyancy sample points for exterior waterline force distribution â€” owner: SubmarineFluidDynamics
        private readonly Vector3[] _exteriorBuoyancySampleLocalPoints = new Vector3[ExteriorBuoyancySampleCount];
        // COLD ALLOC: SpatialQueryHit[16] â€” breach depressurization loose-body query scratch â€” owner: SubmarineFluidDynamics
        private readonly SpatialQueryHit[] _depressurizationContacts = new SpatialQueryHit[DepressurizationContactCapacity];
        // COLD ALLOC: Rigidbody[16] â€” unique rigidbody scratch for depressurization routing â€” owner: SubmarineFluidDynamics
        private readonly Rigidbody[] _depressurizationBodies = new Rigidbody[DepressurizationContactCapacity];
        // COLD ALLOC: Vector3[8] â€” runtime centers of localized exterior boil cells quantized to 8m volumes â€” owner: SubmarineFluidDynamics
        private readonly Vector3[] _exteriorThermalAnomalyCenters = new Vector3[ExteriorThermalAnomalyCapacity];
        // COLD ALLOC: float[8] â€” per-cell exterior water temperatures in Celsius for temporary plasma boil anomalies â€” owner: SubmarineFluidDynamics
        private readonly float[] _exteriorThermalAnomalyTemperatures = new float[ExteriorThermalAnomalyCapacity];
        // COLD ALLOC: float[8] â€” remaining lifetime of each exterior boil anomaly in seconds â€” owner: SubmarineFluidDynamics
        private readonly float[] _exteriorThermalAnomalyLifetimes = new float[ExteriorThermalAnomalyCapacity];
        // COLD ALLOC: int[8] â€” hazard source ids mapped one-to-one with exterior boil anomaly slots â€” owner: SubmarineFluidDynamics
        private readonly int[] _exteriorThermalHazardIds = new int[ExteriorThermalAnomalyCapacity];
        // COLD ALLOC: Collider[16] â€” bounded boiling-water rigidbody query scratch â€” owner: SubmarineFluidDynamics
        private readonly Collider[] _exteriorThermalContacts = new Collider[ExteriorThermalContactCapacity];

        private NativeArray<float> _compartmentFloodVolumes;
        private NativeArray<float> _compartmentViscosity01;
        private NativeArray<float> _compartmentBaseMaxVolumes;
        private NativeArray<float> _compartmentMaxVolumes;
        private NativeArray<float> _compartmentBreachAreas;
        private NativeArray<float3> _compartmentLocalCentroids;
        private NativeArray<uint> _compartmentFlags;
        private NativeArray<int2> _bulkheadPairs;
        private NativeArray<byte> _bulkheadSealed;
        private NativeArray<float> _bulkheadDoorAreas;
        private NativeArray<float3> _comAccumulatorFront;
        private NativeArray<float3> _comAccumulatorBack;
        private NativeArray<FloodMassPropertiesResult> _massPropertiesFront;
        private NativeArray<FloodMassPropertiesResult> _massPropertiesBack;
        private NativeArray<float3> _angularVelocityHistoryLocal;
        private NativeArray<float> _previousExteriorSampleSubmersionFactors;
        private NativeArray<float> _jobFloodVolumes;
        private NativeArray<uint> _jobCompartmentFlags;
        private NativeArray<float> _bulkheadTransferDeltas;
        private NativeQueue<SplashEvent> _splashEventQueue;
        private FluidMathCore _fluidMathCore;
        private bool _fluidSimulationRegistered;

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
        [StructLayout(LayoutKind.Sequential)]
        private struct FloodMassPropertiesResult
        {
            public float FloodMassKilograms;
            public float FloodMassRatio;
            public float3 FloodCenterLocal;
            public float3 TargetCenterLocal;
            public float3 InertiaTensor;
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
                    floodCenter = weightedSum / totalFloodMass;

                float maxFloodMass = totalCapacity * WaterDensityKgPerCubicMeter;
                float floodMassRatio = maxFloodMass > Epsilon
                    ? math.saturate(totalFloodMass / maxFloodMass)
                    : 0f;

                float3 targetCenter = math.lerp(DryCenterLocal, floodCenter, floodMassRatio);
                float3 inertiaTensor = math.lerp(DryInertiaTensor, FloodedInertiaTensor, floodMassRatio);

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
        public int PendingSplashEventCount => _queuedSplashEventCount;

        /// <summary>Reported local-space flood centroid for telemetry, audio, or VFX queries.</summary>
        public Vector3 ReportedFloodCenterOfMassLocal => _reportedFloodCenterOfMassLocal;

        /// <summary>Resolved external water depth used by the current ingress step.</summary>
        public float ExternalDepthMeters => _externalDepthMeters;

        internal int ConfiguredBulkheadCount => _configuredBulkheadCount;

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
            RebuildExteriorBuoyancySampleLocalPoints();
            EnsureNativeState();
            RefreshResolvedInertiaTensors();
            SeedNativeStateFromAuthoring();
            RefreshDerivedConstants(DefaultFixedStepSeconds);
            if (HasActiveHydrodynamicsConfiguration())
            {
                TryRegister();
                TryRegisterOriginShiftListener();
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
            TryUnregisterOriginShiftListener();
            TryUnregister();
            ClearExteriorThermalAnomalies();
            RestoreRigidbodyDynamics();
            DisposeNativeStateDeferred();
        }

        private void OnDestroy()
        {
            TryUnregisterFluidSimulationService();
            TryUnregisterOriginShiftListener();
            TryUnregister();
            ClearExteriorThermalAnomalies();
            RestoreRigidbodyDynamics();
            DisposeNativeStateDeferred();
        }

        /// <summary>
        /// Fixed-step fluid ingress, inter-compartment transfer, inertia interpolation, and delayed slosh torque.
        /// </summary>
        /// <param name="fixedDeltaTime">Discrete physics step accumulated through the dispatcher cadence.</param>
        public void FixedTick(float fixedDeltaTime)
        {
            if (!_compartmentFloodVolumes.IsCreated || _rigidbody == null || fixedDeltaTime <= 0f)
                return;

            if (!HasActiveHydrodynamicsConfiguration())
                return;

            _skipHydrodynamicsForCurrentFixedTick = false;
            _currentFixedDeltaTime = fixedDeltaTime;
            float depthMeters = ResolveExternalDepthMeters();
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

            ScheduleFloodMassPropertiesJob();
            ScheduleFluidTransferJob(depthMeters, fixedDeltaTime);
            RefreshDebugState();
        }

        /// <inheritdoc />
        public void PostFixedTick(float fixedDeltaTime)
        {
            CompleteFluidTransferInPostFixedSwapWindow();
            CompleteFloodMassPropertiesInPostFixedSwapWindow();
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
            float compartmentRadius = math.pow(roomVolume / 4.1887903f, 0.33333334f);
            float influenceRadius = math.max(0.5f, compartmentRadius + math.max(0f, depressurizationRoomRadiusPaddingMeters));
            float rawForceNewtons = pressureDeltaKPa * 1000f * math.max(Epsilon, breachAreaSquareMeters);
            float baseAcceleration = rawForceNewtons / math.max(1f, depressurizationReferenceMassKilograms);
            float maximumAcceleration = math.max(0f, maximumDepressurizationAccelerationMetersPerSecondSquared);
            if (!math.isfinite(baseAcceleration) || baseAcceleration <= Epsilon || maximumAcceleration <= Epsilon)
                return;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
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
        }

        /// <summary>
        /// Dequeues one sampled exterior water-entry payload for downstream VFX systems.
        /// </summary>
        /// <param name="splashEvent">Resolved splash payload when available.</param>
        /// <returns>True when a splash payload was dequeued.</returns>
        public bool TryDequeueSplashEvent(out SplashEvent splashEvent)
        {
            if (!_splashEventQueue.IsCreated || _queuedSplashEventCount <= 0)
            {
                splashEvent = default;
                return false;
            }

            if (!_splashEventQueue.TryDequeue(out splashEvent))
            {
                splashEvent = default;
                _queuedSplashEventCount = 0;
                return false;
            }

            _queuedSplashEventCount--;
            return true;
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
            if (_cachedTransform == null || heatEnergyJoules <= 0f)
                return;

            float surfaceY = ResolveSurfaceHeightAtSample(runtimePoint, runtimePoint.y);
            float depthMeters = math.max(0f, surfaceY - runtimePoint.y);
            if (depthMeters <= 0.01f)
                return;

            Vector3 quantizedCenter = QuantizeExteriorThermalCell(runtimePoint);
            int slotIndex = ResolveExteriorThermalSlot(quantizedCenter);
            if (slotIndex < 0)
                return;

            if (_exteriorThermalAnomalyLifetimes[slotIndex] > 0f &&
                (_exteriorThermalAnomalyCenters[slotIndex] - quantizedCenter).sqrMagnitude > 0.01f &&
                _exteriorThermalHazardIds[slotIndex] != 0)
            {
                HectonHazardManager.Unregister(_exteriorThermalHazardIds[slotIndex]);
                _exteriorThermalHazardIds[slotIndex] = 0;
            }

            float cellVolume = ExteriorThermalCellSizeMeters * ExteriorThermalCellSizeMeters * ExteriorThermalCellSizeMeters;
            float cellMass = cellVolume * WaterDensityKgPerCubicMeter;
            float deltaTemperature = heatEnergyJoules / math.max(1f, cellMass * ExteriorWaterSpecificHeatCapacityJoulesPerKilogramCelsius);
            if (!math.isfinite(deltaTemperature) || deltaTemperature <= 0f)
                return;

            float currentTemperature = _exteriorThermalAnomalyLifetimes[slotIndex] > 0f
                ? _exteriorThermalAnomalyTemperatures[slotIndex]
                : ExteriorWaterReferenceTemperatureCelsius;
            _exteriorThermalAnomalyCenters[slotIndex] = quantizedCenter;
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
                _baseLinearDamping = math.max(0f, _rigidbody.linearDamping);
                _baseAngularDamping = math.max(0f, _rigidbody.angularDamping);
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

        private void EnsureNativeState()
        {
            if (_compartmentFloodVolumes.IsCreated)
                return;

            if (_compartmentStates == null)
            {
                // COLD ALLOC: CompartmentState[8] â€” compartment flood snapshots for CoM and telemetry â€” owner: SubmarineFluidDynamics
                _compartmentStates = new CompartmentState[CompartmentCapacity];
            }

            // COLD ALLOC: NativeArray<float>[8] â€” compartment flood volume storage â€” owner: SubmarineFluidDynamics
            _compartmentFloodVolumes = new NativeArray<float>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] — per-compartment normalized sludge viscosity state — owner: SubmarineFluidDynamics
            _compartmentViscosity01 = new NativeArray<float>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] — authored compartment capacities preserved for dynamic crush compression — owner: SubmarineFluidDynamics
            _compartmentBaseMaxVolumes = new NativeArray<float>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] â€” compartment capacity storage â€” owner: SubmarineFluidDynamics
            _compartmentMaxVolumes = new NativeArray<float>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] â€” active breach area storage â€” owner: SubmarineFluidDynamics
            _compartmentBreachAreas = new NativeArray<float>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float3>[8] â€” local compartment centroids â€” owner: SubmarineFluidDynamics
            _compartmentLocalCentroids = new NativeArray<float3>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<uint>[8] â€” compartment state flags â€” owner: SubmarineFluidDynamics
            _compartmentFlags = new NativeArray<uint>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<int2>[7] â€” bulkhead adjacency pairs â€” owner: SubmarineFluidDynamics
            _bulkheadPairs = new NativeArray<int2>(BulkheadCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<byte>[7] â€” bulkhead seal state â€” owner: SubmarineFluidDynamics
            _bulkheadSealed = new NativeArray<byte>(BulkheadCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[7] â€” authored bulkhead doorway areas for pressure blowout math â€” owner: SubmarineFluidDynamics
            _bulkheadDoorAreas = new NativeArray<float>(BulkheadCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float3>[8] â€” ping-pong flood centroid accumulator front buffer â€” owner: SubmarineFluidDynamics
            _comAccumulatorFront = new NativeArray<float3>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float3>[8] â€” ping-pong flood centroid accumulator back buffer â€” owner: SubmarineFluidDynamics
            _comAccumulatorBack = new NativeArray<float3>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<FloodMassPropertiesResult>[1] â€” front flood mass-properties result buffer â€” owner: SubmarineFluidDynamics
            _massPropertiesFront = new NativeArray<FloodMassPropertiesResult>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<FloodMassPropertiesResult>[1] â€” back flood mass-properties result buffer â€” owner: SubmarineFluidDynamics
            _massPropertiesBack = new NativeArray<FloodMassPropertiesResult>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float3>[16] â€” local angular-velocity slosh history supporting 50â€“150 ms delayed counter-torque taps â€” owner: SubmarineFluidDynamics
            _angularVelocityHistoryLocal = new NativeArray<float3>(RingBufferLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] â€” previous sampled exterior submersion factors for splash transition detection â€” owner: SubmarineFluidDynamics
            _previousExteriorSampleSubmersionFactors = new NativeArray<float>(ExteriorBuoyancySampleCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] Ã¢â‚¬â€ Burst fluid-transfer output volumes Ã¢â‚¬â€ owner: SubmarineFluidDynamics
            _jobFloodVolumes = new NativeArray<float>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<uint>[8] Ã¢â‚¬â€ Burst fluid-transfer output flags Ã¢â‚¬â€ owner: SubmarineFluidDynamics
            _jobCompartmentFlags = new NativeArray<uint>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[7] Ã¢â‚¬â€ per-bulkhead transfer delta scratch Ã¢â‚¬â€ owner: SubmarineFluidDynamics
            _bulkheadTransferDeltas = new NativeArray<float>(BulkheadCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeQueue<SplashEvent>(Persistent) â€” deferred exterior splash payload queue for VFX consumers â€” owner: SubmarineFluidDynamics
            _splashEventQueue = new NativeQueue<SplashEvent>(Allocator.Persistent);
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
            Vector3 safeDryCenter = SanitizeCenterOfMass(dryCenterOfMassLocal, Vector3.zero);
            _reportedFloodCenterOfMassLocal = safeDryCenter;
            _appliedCenterOfMassLocal = safeDryCenter;
            _currentFloodCenterOfMassLocal = _appliedCenterOfMassLocal;
            _lastAppliedInertiaTensor = _resolvedDryInertiaTensor;
            _externalSubmergedVolumeCubicMeters = 0f;
            _submersionFactor = 0f;
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

            DisposeDeferred(ref _compartmentFloodVolumes);
            DisposeDeferred(ref _compartmentViscosity01);
            DisposeDeferred(ref _compartmentBaseMaxVolumes);
            DisposeDeferred(ref _compartmentMaxVolumes);
            DisposeDeferred(ref _compartmentBreachAreas);
            DisposeDeferred(ref _compartmentLocalCentroids);
            DisposeDeferred(ref _compartmentFlags);
            DisposeDeferred(ref _bulkheadPairs);
            DisposeDeferred(ref _bulkheadSealed);
            DisposeDeferred(ref _bulkheadDoorAreas);
            DisposeDeferred(ref _comAccumulatorFront);
            DisposeDeferred(ref _comAccumulatorBack);
            DisposeDeferred(ref _massPropertiesFront);
            DisposeDeferred(ref _massPropertiesBack);
            DisposeDeferred(ref _angularVelocityHistoryLocal);
            DisposeDeferred(ref _previousExteriorSampleSubmersionFactors);
            DisposeDeferred(ref _jobFloodVolumes);
            DisposeDeferred(ref _jobCompartmentFlags);
            DisposeDeferred(ref _bulkheadTransferDeltas);
            _disposeHandle.Complete();
            _disposeHandle = default;

            if (_splashEventQueue.IsCreated)
            {
                _splashEventQueue.Dispose();
                _splashEventQueue = default;
            }

            _queuedSplashEventCount = 0;
        }

        private void RestoreRigidbodyDynamics()
        {
            if (_rigidbody == null)
                return;

            Vector3 safeDryCenter = SanitizeCenterOfMass(dryCenterOfMassLocal, Vector3.zero);
            Vector3 safeDryTensor = SanitizeTensor(_resolvedDryInertiaTensor);
            float safeLinearDamping = math.isfinite(_baseLinearDamping) ? math.max(0f, _baseLinearDamping) : 0f;
            float safeAngularDamping = math.isfinite(_baseAngularDamping) ? math.max(0f, _baseAngularDamping) : 0f;
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
            _lastExternalBuoyancyForce = Vector3.zero;
            _lastExternalBuoyancyTorque = Vector3.zero;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Environment);
            GlobalRegistry.RegisterPostFixedTickable(this, PriorityLayer.Environment);
            _registered = true;
        }

        private void TryRegisterFluidSimulationService()
        {
            if (_fluidSimulationRegistered || _fluidMathCore == null || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterFluidSimulationService(_fluidMathCore);
            _fluidSimulationRegistered = true;
        }

        private void TryRegisterOriginShiftListener()
        {
            if (_registeredOriginShiftListener || !Application.isPlaying)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _registeredOriginShiftListener = true;
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

        private void TryUnregisterOriginShiftListener()
        {
            if (!_registeredOriginShiftListener)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _registeredOriginShiftListener = false;
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

            if (clearQueuedEvents && _splashEventQueue.IsCreated)
                _splashEventQueue.Clear();

            if (clearQueuedEvents)
                _queuedSplashEventCount = 0;
        }

        private void CompletePendingFluidTransferForAuthoritativeWrite()
        {
            if (!_fluidJobRunning)
                return;

            // COLD SYNC JOB: authoritative state writes must not race a pending fluid transfer.
            _fluidJobHandle.Complete();
            ApplyCompletedFluidTransfer();
        }

        private void CompletePendingFloodMassPropertiesForAuthoritativeWrite()
        {
            if (!_massPropertiesJobRunning)
                return;

            // COLD SYNC JOB: authoritative compartment writes must not race a pending flood mass-properties job.
            _massPropertiesJobHandle.Complete();
            ApplyCompletedFloodMassProperties();
        }

        private void CompleteFluidTransferInPostFixedSwapWindow()
        {
            if (!_fluidJobRunning || !_fluidJobHandle.IsCompleted || !_jobFloodVolumes.IsCreated || !_jobCompartmentFlags.IsCreated)
                return;

            _fluidJobHandle.Complete();
            ApplyCompletedFluidTransfer();
        }

        private void ApplyCompletedFluidTransfer()
        {
            if (!_jobFloodVolumes.IsCreated || !_jobCompartmentFlags.IsCreated)
                return;

            _fluidJobHandle = default;
            _fluidJobRunning = false;

            NativeArray<float> floodVolumeFrontBuffer = _compartmentFloodVolumes;
            _compartmentFloodVolumes = _jobFloodVolumes;
            _jobFloodVolumes = floodVolumeFrontBuffer;

            NativeArray<uint> flagFrontBuffer = _compartmentFlags;
            _compartmentFlags = _jobCompartmentFlags;
            _jobCompartmentFlags = flagFrontBuffer;
        }

        private void CompleteFloodMassPropertiesInPostFixedSwapWindow()
        {
            if (!_massPropertiesJobRunning || !_massPropertiesJobHandle.IsCompleted || !_massPropertiesBack.IsCreated)
                return;

            _massPropertiesJobHandle.Complete();
            ApplyCompletedFloodMassProperties();
        }

        private void ApplyCompletedFloodMassProperties()
        {
            if (!_massPropertiesBack.IsCreated)
                return;

            _massPropertiesJobHandle = default;
            _massPropertiesJobRunning = false;

            NativeArray<FloodMassPropertiesResult> resultFrontBuffer = _massPropertiesFront;
            _massPropertiesFront = _massPropertiesBack;
            _massPropertiesBack = resultFrontBuffer;

            NativeArray<float3> accumulatorFrontBuffer = _comAccumulatorFront;
            _comAccumulatorFront = _comAccumulatorBack;
            _comAccumulatorBack = accumulatorFrontBuffer;
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
                    ? compartmentCapacity / totalCapacity
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
                        _compartmentFloodVolumes[compartmentIndex] = math.max(0f, currentVolume / IceExpansionVolumeScale);
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

                _compartmentFloodVolumes[compartmentIndex] = math.max(0f, _compartmentFloodVolumes[compartmentIndex] / IceExpansionVolumeScale);
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
            float fillRatio = math.saturate(_compartmentFloodVolumes[compartmentIndex] / maxVolume);
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

        private static float ResolveAggregatePowerSupplyRatio()
        {
            IPowerGridService powerGridService = GlobalRegistry.PowerGrid;
            if (powerGridService == null)
                return 1f;

            float totalConsumption = math.max(0f, powerGridService.TotalConsumption);
            if (totalConsumption <= Epsilon)
                return 1f;

            float totalGeneration = math.max(0f, powerGridService.TotalGeneration);
            return math.saturate(totalGeneration / totalConsumption);
        }

        private int ResolveNearestCompartmentIndex(Vector3 localPosition)
        {
            if (!_compartmentLocalCentroids.IsCreated || _configuredCompartmentCount <= 0)
                return -1;

            int bestIndex = -1;
            float bestDistanceSq = float.MaxValue;
            for (int compartmentIndex = 0; compartmentIndex < _configuredCompartmentCount; compartmentIndex++)
            {
                float3 centroid = _compartmentLocalCentroids[compartmentIndex];
                float distanceSq = math.lengthsq(new float3(localPosition.x, localPosition.y, localPosition.z) - centroid);
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
            float ingressVelocity = math.sqrt(2f * GravityMetersPerSecondSquared * safeDepth);
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
                    EmergencyResetHydrodynamics("SimulateBulkheadTransfer.FillRatio");
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

                float velocityMetersPerSecond = math.sqrt(math.max(0f, 2f * GravityMetersPerSecondSquared * absHeadDifferenceMeters));
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

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
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
                float baseAcceleration = rawForceNewtons / referenceMassKilograms;
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

            float distanceToRoomCenter = math.distance(roomCenter, playerPosition);
            if (distanceToRoomCenter > influenceRadius)
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

            float distanceToRoomCenter = math.distance(roomCenter, bodyPosition);
            if (distanceToRoomCenter > influenceRadius)
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
            float distanceMeters = toBreach.magnitude;
            if (distanceMeters <= Epsilon)
                return Vector3.zero;

            float safeDistance = math.max(depressurizationDistanceFloorMeters, distanceMeters);
            float accelerationMagnitude = math.min(maximumAcceleration, baseAcceleration / safeDistance);
            Vector3 direction = toBreach / distanceMeters;
            return direction * accelerationMagnitude;
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
            float compartmentRadius = math.pow(roomVolume / 4.1887903f, 0.33333334f);
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

                if (_compartmentStates != null)
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
                    if (_compartmentStates != null)
                        _compartmentStates[i] = default;
                    continue;
                }

                float sourceVolume = _compartmentFloodVolumes[i];
                if (!math.isfinite(sourceVolume))
                {
                    EmergencyResetHydrodynamics("FinalizeCompartmentState.CurrentVolume");
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
                    EmergencyResetHydrodynamics("FinalizeCompartmentState.FillRatio");
                    fillRatio = 0f;
                }

                if (fillRatio >= CriticalFillThreshold)
                    flags |= FlagCritical;
                else
                    flags &= ~FlagCritical;

                _compartmentFloodVolumes[i] = currentVolume;
                _compartmentFlags[i] = flags;
                if (_compartmentStates != null)
                {
                    _compartmentStates[i] = new CompartmentState
                    {
                        currentVolume = currentVolume,
                        maxVolume = maxVolume,
                        localCentroid = _compartmentLocalCentroids[i],
                        stateFlags = flags
                    };
                }

                totalFloodVolume += currentVolume;
            }

            if (!math.isfinite(totalFloodVolume))
            {
                EmergencyResetHydrodynamics("FinalizeCompartmentState.TotalFloodVolume");
                _totalFloodVolumeCubicMeters = 0f;
                _floodFillRatio = 0f;
                return;
            }

            _totalFloodVolumeCubicMeters = totalFloodVolume;
            float totalCapacity = ResolveTotalCapacityCubicMeters();
            if (!math.isfinite(totalCapacity) || totalCapacity <= Epsilon)
            {
                EmergencyResetHydrodynamics("FinalizeCompartmentState.TotalCapacity");
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
            float maxFloraMass = math.max(0f, floraDragAddedMassAtFullDensityKilograms);
            float targetMass = math.clamp(dryMass + floodMass + _currentFloraAddedMassKilograms, dryMass, dryMass + maxFloodMass + maxFloraMass);
            _debugFloraAddedMassKilograms = _currentFloraAddedMassKilograms;
            if (!math.isfinite(targetMass))
            {
                EmergencyResetHydrodynamics("ApplyFloodMassPropertiesToRigidbody.TargetMass");
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
                EmergencyResetHydrodynamics("ApplyCenterOfMassShift.BlendedCenter");
                return;
            }

            Vector3 newCenter = HectonPlayerMotor.SafeVelocity(new Vector3(blendedCenter.x, blendedCenter.y, blendedCenter.z), _appliedCenterOfMassLocal);
            if ((_appliedCenterOfMassLocal - newCenter).sqrMagnitude <= 0.000001f)
                return;

            _rigidbody.centerOfMass = newCenter;
            _appliedCenterOfMassLocal = newCenter;
        }

        private void ApplyStartupMassProperties(float3 targetCenter)
        {
            Vector3 safeCenter = SanitizeCenterOfMass(
                new Vector3(targetCenter.x, targetCenter.y, targetCenter.z),
                _appliedCenterOfMassLocal);
            Vector3 safeTensor = SanitizeTensor(Vector3.Lerp(_resolvedDryInertiaTensor, _resolvedFloodedInertiaTensor, _floodFillRatio));
            float safeLinearDamping = math.isfinite(_lastAppliedLinearDamping) ? math.max(0f, _lastAppliedLinearDamping) : 0f;
            float safeAngularDamping = math.isfinite(_lastAppliedAngularDamping) ? math.max(0f, _lastAppliedAngularDamping) : 0f;

            _appliedCenterOfMassLocal = safeCenter;
            _currentFloodCenterOfMassLocal = safeCenter;
            _lastAppliedInertiaTensor = safeTensor;
            _lastAppliedLinearDamping = safeLinearDamping;
            _lastAppliedAngularDamping = safeAngularDamping;

            if (_rigidbody == null)
                return;

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
            float3 blendedCenter = math.lerp(currentCenter, targetCenter, _reportedCenterBlendAlpha);
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
                CompartmentState state = _compartmentStates != null ? _compartmentStates[i] : default;
                if ((state.stateFlags & FlagFrozen) != 0 || state.maxVolume <= Epsilon)
                {
                    if (_comAccumulatorBack.IsCreated && i < _comAccumulatorBack.Length)
                        _comAccumulatorBack[i] = float3.zero;

                    continue;
                }

                if (!math.isfinite(state.currentVolume))
                {
                    EmergencyResetHydrodynamics("ResolveFloodTargetCenterOfMassLocal.CurrentVolume");
                    return dryCenter;
                }

                float mass = math.max(0f, state.currentVolume) * WaterDensityKgPerCubicMeter;
                if (!math.isfinite(mass))
                {
                    EmergencyResetHydrodynamics("ResolveFloodTargetCenterOfMassLocal.Mass");
                    return dryCenter;
                }

                float3 weightedCentroid = state.localCentroid * mass;
                if (!math.all(math.isfinite(weightedCentroid)))
                {
                    EmergencyResetHydrodynamics("ResolveFloodTargetCenterOfMassLocal.WeightedCentroid");
                    return dryCenter;
                }

                if (_comAccumulatorBack.IsCreated && i < _comAccumulatorBack.Length)
                    _comAccumulatorBack[i] = weightedCentroid;

                weightedSum += weightedCentroid;
                if (!math.all(math.isfinite(weightedSum)))
                {
                    EmergencyResetHydrodynamics("ResolveFloodTargetCenterOfMassLocal.WeightedSum");
                    return dryCenter;
                }

                totalFloodMass += mass;
                if (!math.isfinite(totalFloodMass))
                {
                    EmergencyResetHydrodynamics("ResolveFloodTargetCenterOfMassLocal.TotalFloodMass");
                    return dryCenter;
                }
            }

            float maxFloodMass = ResolveTotalCapacityCubicMeters() * WaterDensityKgPerCubicMeter;
            float3 floodCenter = dryCenter;
            if (totalFloodMass > Epsilon)
            {
                if (!TryResolveSafeVectorDivision(weightedSum, totalFloodMass, out floodCenter))
                {
                    EmergencyResetHydrodynamics("ResolveFloodTargetCenterOfMassLocal.WeightedCenter");
                    floodCenter = dryCenter;
                }
            }

            float floodMassRatio = 0f;
            if (maxFloodMass > Epsilon &&
                !TryResolveSafeNormalizedRatio(totalFloodMass, maxFloodMass, out floodMassRatio))
            {
                EmergencyResetHydrodynamics("ResolveFloodTargetCenterOfMassLocal.FloodMassRatio");
                floodMassRatio = 0f;
            }

            float3 targetCenter = math.lerp(dryCenter, floodCenter, floodMassRatio);
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
                EmergencyResetHydrodynamics("RecordAndSampleDelayedSloshAngularVelocityLocal.Current");
                currentLocalAngularVelocity = float3.zero;
            }

            _angularVelocityHistoryLocal[_ringHead] = currentLocalAngularVelocity;
            _ringHead = (_ringHead + 1) & RingBufferMask;

            int delayIndex = (_ringHead - SloshDelayFrames) & RingBufferMask;
            float3 delayedAngularVelocity = _angularVelocityHistoryLocal[delayIndex];
            if (math.any(math.isnan(delayedAngularVelocity)) || !math.all(math.isfinite(delayedAngularVelocity)))
            {
                EmergencyResetHydrodynamics("RecordAndSampleDelayedSloshAngularVelocityLocal.Delayed");
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
                    : Vector3.Lerp(_resolvedDryInertiaTensor, _resolvedFloodedInertiaTensor, _floodFillRatio);
            }
            else
            {
                targetTensor = Vector3.Lerp(_resolvedDryInertiaTensor, _resolvedFloodedInertiaTensor, _floodFillRatio);
            }

            if (!IsFiniteVector(targetTensor))
            {
                EmergencyResetHydrodynamics("ApplyInterpolatedInertiaTensor.Lerp");
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
            Vector3 centerOfMassWorld = _rigidbody.worldCenterOfMass;
            float3 centerOfMassWorldFloat = new float3(centerOfMassWorld.x, centerOfMassWorld.y, centerOfMassWorld.z);
            if (math.any(math.isnan(centerOfMassWorldFloat)) || !math.all(math.isfinite(centerOfMassWorldFloat)))
            {
                EmergencyResetHydrodynamics("ApplySampledExteriorBuoyancy.CenterOfMass");
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
                EmergencyResetHydrodynamics("ApplySampledExteriorBuoyancy.SampleVolumeDivision");
                _externalSubmergedVolumeCubicMeters = 0f;
                _submersionFactor = 0f;
                _lastExternalBuoyancyForce = Vector3.zero;
                _lastExternalBuoyancyTorque = Vector3.zero;
                return;
            }

            float perSampleForceMagnitude = WaterDensityKgPerCubicMeter * sampleVolume * GravityMetersPerSecondSquared;
            float rigidbodyMass = math.isfinite(_rigidbody.mass) ? math.max(_rigidbody.mass, Epsilon) : Epsilon;
            if (!TryResolveSafeQuotient(rigidbodyMass, ExteriorBuoyancySampleCount, out float sampleHullMass))
            {
                EmergencyResetHydrodynamics("ApplySampledExteriorBuoyancy.SampleMassDivision");
                _externalSubmergedVolumeCubicMeters = 0f;
                _submersionFactor = 0f;
                _lastExternalBuoyancyForce = Vector3.zero;
                _lastExternalBuoyancyTorque = Vector3.zero;
                return;
            }

            if (!float.IsFinite(sampleVolume) || !float.IsFinite(perSampleForceMagnitude))
            {
                EmergencyResetHydrodynamics("ApplySampledExteriorBuoyancy.SampleVolume");
                _externalSubmergedVolumeCubicMeters = 0f;
                _submersionFactor = 0f;
                _lastExternalBuoyancyForce = Vector3.zero;
                _lastExternalBuoyancyTorque = Vector3.zero;
                return;
            }

            Vector3 totalEquivalentForce = Vector3.zero;
            Vector3 totalEquivalentTorque = Vector3.zero;
            float submergedVolume = 0f;

            for (int i = 0; i < ExteriorBuoyancySampleCount; i++)
            {
                Vector3 worldPoint = _cachedTransform.TransformPoint(_exteriorBuoyancySampleLocalPoints[i]);
                float3 worldPointFloat = new float3(worldPoint.x, worldPoint.y, worldPoint.z);
                if (math.any(math.isnan(worldPointFloat)) || !math.all(math.isfinite(worldPointFloat)))
                {
                    EmergencyResetHydrodynamics("ApplySampledExteriorBuoyancy.SamplePoint");
                    _externalSubmergedVolumeCubicMeters = 0f;
                    _submersionFactor = 0f;
                    _lastExternalBuoyancyForce = Vector3.zero;
                    _lastExternalBuoyancyTorque = Vector3.zero;
                    return;
                }

                if (!IsFiniteVector(worldPoint))
                    continue;

                float sampleSurfaceY = ResolveSurfaceHeightAtSample(worldPoint, fallbackSurfaceY);
                float submersionFactor = ResolveSurfaceSubmersionFactor(worldPoint.y - sampleSurfaceY);
                QueueExteriorSplashEventIfNeeded(i, worldPoint, submersionFactor, sampleHullMass);
                if (submersionFactor <= Epsilon)
                    continue;

                float submergedSampleVolume = sampleVolume * submersionFactor;
                if (!float.IsFinite(submergedSampleVolume))
                {
                    EmergencyResetHydrodynamics("ApplySampledExteriorBuoyancy.SubmergedSampleVolume");
                    _externalSubmergedVolumeCubicMeters = 0f;
                    _submersionFactor = 0f;
                    _lastExternalBuoyancyForce = Vector3.zero;
                    _lastExternalBuoyancyTorque = Vector3.zero;
                    return;
                }

                submergedVolume += submergedSampleVolume;

                Vector3 sampleAcceleration = Vector3.up * ((perSampleForceMagnitude * submersionFactor) / rigidbodyMass);
                if (!IsFiniteVector(sampleAcceleration))
                {
                    EmergencyResetHydrodynamics("ApplySampledExteriorBuoyancy.SampleAcceleration");
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
                EmergencyResetHydrodynamics("ApplySampledExteriorBuoyancy.SubmergedVolume");
                _externalSubmergedVolumeCubicMeters = 0f;
                _submersionFactor = 0f;
                _lastExternalBuoyancyForce = Vector3.zero;
                _lastExternalBuoyancyTorque = Vector3.zero;
                return;
            }

            _externalSubmergedVolumeCubicMeters = math.clamp(submergedVolume, 0f, displacementVolume);
            if (!TryResolveSafeNormalizedRatio(_externalSubmergedVolumeCubicMeters, displacementVolume, out _submersionFactor))
            {
                EmergencyResetHydrodynamics("ApplySampledExteriorBuoyancy.SubmersionRatio");
                _externalSubmergedVolumeCubicMeters = 0f;
                _submersionFactor = 0f;
                _lastExternalBuoyancyForce = Vector3.zero;
                _lastExternalBuoyancyTorque = Vector3.zero;
                return;
            }

            float maxForceMagnitude = WaterDensityKgPerCubicMeter *
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
                EmergencyResetHydrodynamics("ApplySampledExteriorBuoyancy.Result");
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
                math.lerp(1f, math.max(1f, floraDragLinearMultiplier), floraDensity01));
            float floraAngularMultiplier = math.max(
                MinimumAnalyticalDragModifier,
                math.lerp(1f, math.max(1f, floraDragAngularMultiplier), floraDensity01));
            _debugFloraDragDensity = floraDensity01;
            if (!float.IsFinite(criticalFloodRatio) ||
                !float.IsFinite(dampingSubmersion) ||
                !float.IsFinite(linearScale) ||
                !float.IsFinite(angularScale) ||
                !float.IsFinite(floraLinearMultiplier) ||
                !float.IsFinite(floraAngularMultiplier))
            {
                EmergencyResetHydrodynamics("ApplyAddedMassDamping.Scale");
                return;
            }

            float targetLinearDamping = _baseLinearDamping * (1f + (linearScale * dampingSubmersion)) * floraLinearMultiplier;
            float targetAngularDamping = _baseAngularDamping * (1f + (angularScale * dampingSubmersion)) * floraAngularMultiplier;
            if (_hullImplosionActive)
                targetLinearDamping += math.max(0f, implosionDragBonus);

            if (!float.IsFinite(targetLinearDamping) || !float.IsFinite(targetAngularDamping))
            {
                EmergencyResetHydrodynamics("ApplyAddedMassDamping.Result");
                return;
            }

            _currentHydrodynamicLinearInertiaScale = math.max(1f, (1f + (linearScale * dampingSubmersion)) * floraLinearMultiplier);
            _currentHydrodynamicAngularInertiaScale = math.max(1f, (1f + (angularScale * dampingSubmersion)) * floraAngularMultiplier);

            if (math.abs(_lastAppliedLinearDamping - targetLinearDamping) > 0.0005f)
            {
                _rigidbody.linearDamping = targetLinearDamping;
                _lastAppliedLinearDamping = targetLinearDamping;
            }

            if (math.abs(_lastAppliedAngularDamping - targetAngularDamping) > 0.0005f)
            {
                _rigidbody.angularDamping = targetAngularDamping;
                _lastAppliedAngularDamping = targetAngularDamping;
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

            float normalizedDensity = densitySum / densityCount;
            float radiusScale = math.saturate(sampleRadius / math.max(floraDragMinimumSampleRadiusMeters, 0.01f));
            return math.saturate(normalizedDensity * math.lerp(0.85f, 1.15f, math.saturate(radiusScale - 1f)));
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
            if (fixedDeltaTime <= 0f)
                return;

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

                Vector3 cellCenter = _exteriorThermalAnomalyCenters[slotIndex];
                float surfaceY = ResolveSurfaceHeightAtSample(cellCenter, cellCenter.y);
                float depthMeters = math.max(0f, surfaceY - cellCenter.y);
                float boilingPointCelsius = ResolveBoilingPointCelsius(depthMeters);

                if (currentTemperature > boilingPointCelsius)
                {
                    float intensity = math.saturate((currentTemperature - boilingPointCelsius) / 35f);
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

                _exteriorThermalAnomalyCenters[slotIndex] = Vector3.zero;
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
                float distance = Vector3.Distance(samplePoint, cellCenter);
                float distanceT = 1f - math.saturate(distance / math.max(0.01f, influenceRadius));
                if (distanceT <= 0f)
                    continue;

                float accelerationMagnitude = ExteriorBoilingAccelerationMetersPerSecondSquared * intensity * distanceT;
                PhysicsForceRouter.QueueForce(body, Vector3.up * accelerationMagnitude, ForceMode.Acceleration);
            }

            if (_cachedPlayerTransform == null || _cachedPlayerMovement == null)
                return;

            float playerDistance = Vector3.Distance(_cachedPlayerTransform.position, cellCenter);
            float playerDistanceT = 1f - math.saturate(playerDistance / math.max(0.01f, influenceRadius));
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
            for (int slotIndex = 0; slotIndex < ExteriorThermalAnomalyCapacity; slotIndex++)
            {
                if (_exteriorThermalHazardIds[slotIndex] != 0)
                    HectonHazardManager.Unregister(_exteriorThermalHazardIds[slotIndex]);

                _exteriorThermalHazardIds[slotIndex] = 0;
                _exteriorThermalAnomalyCenters[slotIndex] = Vector3.zero;
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
            float invCellSize = 1f / ExteriorThermalCellSizeMeters;
            return new Vector3(
                (math.floor(runtimePoint.x * invCellSize) + 0.5f) * ExteriorThermalCellSizeMeters,
                (math.floor(runtimePoint.y * invCellSize) + 0.5f) * ExteriorThermalCellSizeMeters,
                (math.floor(runtimePoint.z * invCellSize) + 0.5f) * ExteriorThermalCellSizeMeters);
        }

        private int ResolveExteriorThermalSlot(Vector3 quantizedCenter)
        {
            int expiredSlot = -1;
            float lowestLifetime = float.MaxValue;
            int oldestSlot = 0;

            for (int slotIndex = 0; slotIndex < ExteriorThermalAnomalyCapacity; slotIndex++)
            {
                if (_exteriorThermalAnomalyLifetimes[slotIndex] > 0f)
                {
                    if ((_exteriorThermalAnomalyCenters[slotIndex] - quantizedCenter).sqrMagnitude <= 0.01f)
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

        private int ResolveExteriorThermalHazardId(int slotIndex)
        {
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
                EmergencyResetHydrodynamics("ApplyDelayedSloshTorque.DelayedVelocity");
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
                    EmergencyResetHydrodynamics("ApplyDelayedSloshTorque.FillRatio");
                    _lastSloshTorqueLocal = Vector3.zero;
                    return;
                }

                float freesurf = 1f - fillRatio;
                freesurf *= freesurf;
                float sloshMass = currentVolume * WaterDensityKgPerCubicMeter;
                if (!float.IsFinite(sloshMass))
                {
                    EmergencyResetHydrodynamics("ApplyDelayedSloshTorque.SloshMass");
                    _lastSloshTorqueLocal = Vector3.zero;
                    return;
                }

                float viscosity01 = _compartmentViscosity01.IsCreated ? math.saturate(_compartmentViscosity01[i]) : 0f;
                float viscosityDamping = 1f / (1f + (viscosity01 * math.max(0f, viscositySloshDampingScale)));
                totalSloshTorque += -delayedAngularVelocity * (fillRatio * torqueScale * sloshMass * freesurf * viscosityDamping);
                if (math.any(math.isnan(totalSloshTorque)) || !math.all(math.isfinite(totalSloshTorque)))
                {
                    EmergencyResetHydrodynamics("ApplyDelayedSloshTorque.Accumulate");
                    _lastSloshTorqueLocal = Vector3.zero;
                    return;
                }
            }

            float maxTorqueMagnitude = math.max(0f, maxSloshTorque);
            if (maxTorqueMagnitude > Epsilon)
            {
                float torqueMagnitude = math.length(totalSloshTorque);
                if (torqueMagnitude > maxTorqueMagnitude && torqueMagnitude > Epsilon)
                {
                    if (!TryResolveSafeQuotient(maxTorqueMagnitude, torqueMagnitude, out float torqueClampScale))
                    {
                        EmergencyResetHydrodynamics("ApplyDelayedSloshTorque.Clamp");
                        _lastSloshTorqueLocal = Vector3.zero;
                        return;
                    }

                    totalSloshTorque *= torqueClampScale;
                }
            }

            if (math.any(math.isnan(totalSloshTorque)) || !math.all(math.isfinite(totalSloshTorque)))
            {
                EmergencyResetHydrodynamics("ApplyDelayedSloshTorque.Result");
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

        private void EmergencyResetHydrodynamics(string context)
        {
            _skipHydrodynamicsForCurrentFixedTick = true;

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }

            _externalSubmergedVolumeCubicMeters = 0f;
            _submersionFactor = 0f;
            _currentFloraDragDensity01 = 0f;
            _currentFloraAddedMassKilograms = 0f;
            _lastSloshTorqueLocal = Vector3.zero;
            _lastExternalBuoyancyForce = Vector3.zero;
            _lastExternalBuoyancyTorque = Vector3.zero;
            _debugDelayedSloshAngularVelocityLocal = Vector3.zero;
            _debugLastSloshTorqueLocal = Vector3.zero;
            _debugExternalSubmergedVolumeCubicMeters = 0f;
            _debugSubmersionFactor = 0f;
            _currentHydrodynamicLinearInertiaScale = 1f;
            _currentHydrodynamicAngularInertiaScale = 1f;
            ResetSplashDetectionState(clearQueuedEvents: true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[SubmarineFluidDynamics] NaN/Inf detected in {context}. Rigidbody velocities reset.");
#endif
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
            return scale > 1f ? (force / scale) : force;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector3 ApplyHydrodynamicAngularInertiaScale(Vector3 torque)
        {
            float scale = math.max(1f, _currentHydrodynamicAngularInertiaScale);
            return scale > 1f ? (torque / scale) : torque;
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

            if (previousSubmersionFactor > Epsilon || currentSubmersionFactor <= SplashSubmersionThreshold)
                return;

            if (!_splashEventQueue.IsCreated || _queuedSplashEventCount >= MaxQueuedSplashEvents || _rigidbody == null)
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

            Vector3 absoluteUniversePosition = HectonFloatingOrigin.ToAbsoluteUniversePosition(worldPoint);
            if (!IsFiniteVector(absoluteUniversePosition))
                return;

            SplashEvent splashEvent = new SplashEvent
            {
                RuntimePosition = new float3(worldPoint.x, worldPoint.y, worldPoint.z),
                AbsoluteUniversePosition = new float3(absoluteUniversePosition.x, absoluteUniversePosition.y, absoluteUniversePosition.z),
                SurfaceNormal = new float3(0f, 1f, 0f),
                ImpactSpeedMetersPerSecond = impactSpeedMetersPerSecond,
                KineticEnergyJoules = kineticEnergyJoules,
                SubmersionFactor = currentSubmersionFactor,
                SampleIndex = sampleIndex
            };

            _splashEventQueue.Enqueue(splashEvent);
            _queuedSplashEventCount++;
            FluidFeedbackEvents.PublishSplashQueued(in splashEvent);
        }

        private void RebuildExteriorBuoyancySampleLocalPoints()
        {
            if (_cachedTransform == null)
                return;

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
                float fallbackHalfExtent = math.max(0.5f, math.pow(fallbackVolume, 1f / 3f) * 0.5f);
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
                        _exteriorBuoyancySampleLocalPoints[sampleIndex++] = centerLocal + new Vector3(
                            extentsLocal.x * xSign,
                            extentsLocal.y * ySign,
                            extentsLocal.z * zSign);
                    }
                }
            }

            _exteriorBuoyancyMaxLeverArm = 0.1f;
            for (int i = 0; i < ExteriorBuoyancySampleCount; i++)
            {
                float leverArm = Vector3.Distance(centerLocal, _exteriorBuoyancySampleLocalPoints[i]);
                if (leverArm > _exteriorBuoyancyMaxLeverArm)
                    _exteriorBuoyancyMaxLeverArm = leverArm;
            }
        }

        private float ResolveSurfaceHeightAtSample(Vector3 worldPoint, float fallbackSurfaceY)
        {
            IHectonOceanKinematics oceanKinematics = _oceanKinematics;
            if (oceanKinematics == null || !oceanKinematics.IsAvailable)
            {
                IHectonOceanKinematicsService oceanKinematicsService = GlobalRegistry.OceanKinematics;
                oceanKinematics = oceanKinematicsService != null ? oceanKinematicsService.ActiveProvider : null;
                _oceanKinematics = oceanKinematics;
            }

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

            float candidate = numerator / denominator;
            if (math.isnan(candidate) || !math.isfinite(candidate))
                return false;

            ratio = math.saturate(candidate);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SafeCubeRoot(float value)
        {
            return math.pow(math.max(0f, value), 0.33333334f);
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

            float candidate = numerator / denominator;
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
                HectonAtmosphereManager atmosphereManager = HectonAtmosphereManager.Instance;
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

            if (!TryResolveSafeQuotient(-deltaTime, tauSeconds, out float exponent))
                return 0f;

            float candidate = 1f - math.exp(exponent);
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
        }

        private void SeedFloodMassPropertiesBuffers(float3 targetFloodCenter, float floodMassRatio)
        {
            if (!_massPropertiesFront.IsCreated || !_massPropertiesBack.IsCreated)
                return;

            Vector3 targetTensor = Vector3.Lerp(_resolvedDryInertiaTensor, _resolvedFloodedInertiaTensor, floodMassRatio);
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
            if (value.sqrMagnitude <= maxMagnitudeSq || value.sqrMagnitude <= Epsilon)
                return value;

            return value.normalized * maxMagnitude;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 SafeNormalize(Vector3 value, Vector3 fallback)
        {
            float magnitudeSq = value.sqrMagnitude;
            if (magnitudeSq <= Epsilon)
                return fallback.sqrMagnitude > Epsilon ? fallback.normalized : Vector3.up;

            return value * math.rsqrt(magnitudeSq);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFiniteVector(Vector3 value)
        {
            return !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) ||
                     float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z));
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

        private void DisposeDeferred<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            _disposeHandle = array.Dispose(_disposeHandle);
            array = default;
        }
    }
}
