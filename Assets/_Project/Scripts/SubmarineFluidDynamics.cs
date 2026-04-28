using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Gameplay;
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
        /// <summary>0–1 ratio of the sample point submerged below the waterline at impact.</summary>
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
    public sealed class SubmarineFluidDynamics : MonoBehaviour, IFixedTickable, IOriginShiftListener
    {
        private const int CompartmentCapacity = 8;
        private const int BulkheadCapacity = 7;
        private const int RingBufferLength = 16;
        private const int RingBufferMask = RingBufferLength - 1;
        private const float MinimumSloshDelaySeconds = 0.05f;
        private const float MaximumSloshDelaySeconds = 0.15f;
        private const float WaterDensityKgPerCubicMeter = 1025f;
        private const float GravityMetersPerSecondSquared = 9.81f;
        private const float DefaultFixedStepSeconds = 0.02f;
        private const float DefaultDischargeCoefficient = 0.62f;
        private const float DefaultBulkheadFlowCoefficient = 0.4f;
        private const float DefaultBulkheadDoorAreaSquareMeters = 1.6f;
        private const float DefaultMaxTransferPerTick = 0.1f;
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
        private const float CriticalFloodSloshResistanceBoost = 6f;
        private const float FloodedHullRotationalResistanceScale = 0.08f;
        private const float DefaultExteriorBuoyancyForceClampScale = 1.15f;
        private const float DefaultExteriorBuoyancyTorqueClampScale = 1.25f;
        private const int ExteriorBuoyancySampleCount = 8;
        private const int MaxQueuedSplashEvents = 32;
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
        private const uint PersistentFlagsMask = FlagBreached | FlagPurging | FlagFrozen;

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

        [Header("── Compartments ──────────────────")]
        [Tooltip("Authored compartment capacities, breach openings, and local centroids. Maximum supported count is eight.")]
        [SerializeField] private CompartmentDefinition[] compartments = new CompartmentDefinition[CompartmentCapacity];

        [Tooltip("Adjacency map for water transfer. If empty, a linear bow-to-stern chain is generated.")]
        [SerializeField] private BulkheadDefinition[] bulkheads = new BulkheadDefinition[0];

        [Header("── Inertia Blend ─────────────────")]
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

        [Header("── Flood Math ────────────────────")]
        [Tooltip("Sharp-edge discharge coefficient used in Torricelli ingress.")]
        [SerializeField, Range(0.05f, 1f)] private float dischargeCoefficient = DefaultDischargeCoefficient;

        [Tooltip("Bulkhead transfer coefficient in cubic meters per second per unit fill differential.")]
        [SerializeField, Min(0f)] private float bulkheadFlowCoefficient = DefaultBulkheadFlowCoefficient;

        [Tooltip("Maximum cross-bulkhead transfer per fixed step, in cubic meters.")]
        [SerializeField, Min(0.01f)] private float maxTransferPerTick = DefaultMaxTransferPerTick;

        [Tooltip("Safety limiter for ingress. 0.25 means at most 25% of a compartment volume can enter per second.")]
        [SerializeField, Range(0.01f, 1f)] private float maximumIngressPerSecondNormalized = DefaultMaximumIngressPerSecondNormalized;

        [Header("── Slosh Response ────────────────")]
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

        [Header("── Depth Source ──────────────────")]
        [Tooltip("When true, depth is sampled from the atmosphere sea level relative to the hull position.")]
        [SerializeField] private bool sampleDepthFromAtmosphere = true;

        [Tooltip("Fallback or manual external depth when atmospheric sea level sampling is disabled.")]
        [SerializeField, Min(0f)] private float manualExternalDepthMeters;

        [Header("── Exterior Buoyancy ──────────────────")]
        [Tooltip("Optional explicit collider used to derive exterior buoyancy sample points. Falls back to the first owned collider.")]
        [SerializeField] private Collider exteriorHullCollider;

        [Tooltip("Exterior displaced volume used by sampled buoyancy. Zero derives from total compartment capacity or the hull bounds.")]
        [SerializeField, Min(0f)] private float exteriorDisplacementVolumeCubicMeters;

        [Tooltip("Safety clamp applied against the theoretical Archimedes force for the full displacement volume.")]
        [SerializeField, Range(1f, 2f)] private float exteriorBuoyancyForceClampScale = DefaultExteriorBuoyancyForceClampScale;

        [Tooltip("Safety clamp multiplier applied against the theoretical buoyancy torque from the furthest sample lever arm.")]
        [SerializeField, Range(1f, 3f)] private float exteriorBuoyancyTorqueClampScale = DefaultExteriorBuoyancyTorqueClampScale;

        [Header("── Diagnostics ───────────────────")]
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

        private Rigidbody _rigidbody;
        private Transform _cachedTransform;
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
        private int _queuedSplashEventCount;
        private CompartmentState[] _compartmentStates;
        private ISubmarineHullBreachReadModel _structuralBreachReadModel;
        private readonly List<MonoBehaviour> _componentSearchBuffer = new List<MonoBehaviour>(4); // COLD ALLOC: List<MonoBehaviour>(4) — local component search scratch for interface-only structural breach wiring — owner: SubmarineFluidDynamics
        // COLD ALLOC: Vector3[8] — cached local buoyancy sample points for exterior waterline force distribution — owner: SubmarineFluidDynamics
        private readonly Vector3[] _exteriorBuoyancySampleLocalPoints = new Vector3[ExteriorBuoyancySampleCount];

        private NativeArray<float> _compartmentFloodVolumes;
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

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
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
                float safeDepth = math.max(0f, DepthMeters);
                float ingressVelocity = math.sqrt(2f * GravityMetersPerSecondSquared * safeDepth);
                if (!math.isfinite(ingressVelocity))
                    ingressVelocity = 0f;

                float maxIngressScale = math.max(0.01f, MaximumIngressPerSecondNormalized) * FixedDeltaTime;
                float cd = math.clamp(DischargeCoefficient, 0.05f, 1f);

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
                            float deltaVolume = ingressVelocity * breachArea * cd * FixedDeltaTime;
                            if (!math.isfinite(deltaVolume))
                                deltaVolume = 0f;

                            float maxIngressThisStep = maxVolume * maxIngressScale;
                            deltaVolume = math.clamp(deltaVolume, 0f, math.min(remainingCapacity, maxIngressThisStep));
                            currentVolume += deltaVolume;
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

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
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

                if (!TryResolveSafeNormalizedRatio(FloodVolumes[compartmentA], maxVolumeA, out float fillA) ||
                    !TryResolveSafeNormalizedRatio(FloodVolumes[compartmentB], maxVolumeB, out float fillB))
                {
                    return;
                }

                float transferCoefficient = math.max(0f, BulkheadFlowCoefficient);
                float perTickTransferCap = math.max(0.01f, MaxTransferPerTick);
                float doorAreaSquareMeters = math.max(Epsilon, BulkheadDoorAreas[index]);
                float characteristicHeightA = math.max(0.1f, math.cbrt(maxVolumeA));
                float characteristicHeightB = math.max(0.1f, math.cbrt(maxVolumeB));
                float headHeightA = fillA * characteristicHeightA;
                float headHeightB = fillB * characteristicHeightB;
                float headDifferenceMeters = headHeightA - headHeightB;
                float velocityMetersPerSecond = math.sqrt(math.max(0f, 2f * GravityMetersPerSecondSquared * math.abs(headDifferenceMeters)));
                float signedDeltaVolume =
                    math.sign(headDifferenceMeters) *
                    doorAreaSquareMeters *
                    math.max(0f, DischargeCoefficient) *
                    velocityMetersPerSecond *
                    transferCoefficient *
                    FixedDeltaTime;
                float deltaVolume = math.clamp(signedDeltaVolume, -perTickTransferCap, perTickTransferCap);

                if (deltaVolume > 0f)
                {
                    deltaVolume = math.min(
                        deltaVolume,
                        math.min(FloodVolumes[compartmentA], maxVolumeB - FloodVolumes[compartmentB]));
                }
                else if (deltaVolume < 0f)
                {
                    float transferMagnitude = math.min(
                        -deltaVolume,
                        math.min(FloodVolumes[compartmentB], maxVolumeA - FloodVolumes[compartmentA]));
                    deltaVolume = -transferMagnitude;
                }

                if (math.abs(deltaVolume) <= Epsilon || !math.isfinite(deltaVolume))
                    deltaVolume = 0f;

                TransferDeltas[index] = deltaVolume;
            }
        }

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
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

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        [StructLayout(LayoutKind.Sequential)]
        private struct FloodMassPropertiesResult
        {
            public float FloodMassKilograms;
            public float FloodMassRatio;
            public float3 FloodCenterLocal;
            public float3 TargetCenterLocal;
            public float3 InertiaTensor;
        }

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
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
            CacheReferences();
            RefreshResolvedInertiaTensors();
            RefreshDerivedConstants(DefaultFixedStepSeconds);
            RefreshDebugState();
        }

        private void OnEnable()
        {
            CacheReferences();
            RebuildExteriorBuoyancySampleLocalPoints();
            EnsureNativeState();
            RefreshResolvedInertiaTensors();
            SeedNativeStateFromAuthoring();
            RefreshDerivedConstants(DefaultFixedStepSeconds);
            TryRegister();
            TryRegisterOriginShiftListener();
            RefreshDebugState();
        }

        private void OnDisable()
        {
            TryUnregisterOriginShiftListener();
            TryUnregister();
            RestoreRigidbodyDynamics();
            DisposeNativeStateDeferred();
        }

        private void OnDestroy()
        {
            TryUnregisterOriginShiftListener();
            TryUnregister();
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

            _skipHydrodynamicsForCurrentFixedTick = false;
            _currentFixedDeltaTime = fixedDeltaTime;
            float depthMeters = ResolveExternalDepthMeters();
            ConsumeCompletedFluidTransfer();
            ConsumeCompletedFloodMassProperties();
            if (ShouldAbortHydrodynamicsFixedTick())
                return;

            RefreshDerivedConstants(fixedDeltaTime);
            SyncBulkheadSealedFlags();
            SyncStructuralBreachIngress();
            FinalizeCompartmentState();
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

            ApplyDelayedSloshTorque();
            if (ShouldAbortHydrodynamicsFixedTick())
                return;

            ScheduleFloodMassPropertiesJob();
            ScheduleFluidTransferJob(depthMeters, fixedDeltaTime);
            RefreshDebugState();
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

            if (exteriorHullCollider == null)
                TryGetComponent(out exteriorHullCollider);

            if (_structuralBreachReadModel == null)
                _structuralBreachReadModel = GlobalRegistry.SubmarineHullBreach;

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
                // COLD ALLOC: CompartmentState[8] — compartment flood snapshots for CoM and telemetry — owner: SubmarineFluidDynamics
                _compartmentStates = new CompartmentState[CompartmentCapacity];
            }

            // COLD ALLOC: NativeArray<float>[8] — compartment flood volume storage — owner: SubmarineFluidDynamics
            _compartmentFloodVolumes = new NativeArray<float>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] — compartment capacity storage — owner: SubmarineFluidDynamics
            _compartmentMaxVolumes = new NativeArray<float>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] — active breach area storage — owner: SubmarineFluidDynamics
            _compartmentBreachAreas = new NativeArray<float>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float3>[8] — local compartment centroids — owner: SubmarineFluidDynamics
            _compartmentLocalCentroids = new NativeArray<float3>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<uint>[8] — compartment state flags — owner: SubmarineFluidDynamics
            _compartmentFlags = new NativeArray<uint>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<int2>[7] — bulkhead adjacency pairs — owner: SubmarineFluidDynamics
            _bulkheadPairs = new NativeArray<int2>(BulkheadCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<byte>[7] — bulkhead seal state — owner: SubmarineFluidDynamics
            _bulkheadSealed = new NativeArray<byte>(BulkheadCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[7] — authored bulkhead doorway areas for pressure blowout math — owner: SubmarineFluidDynamics
            _bulkheadDoorAreas = new NativeArray<float>(BulkheadCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float3>[8] — ping-pong flood centroid accumulator front buffer — owner: SubmarineFluidDynamics
            _comAccumulatorFront = new NativeArray<float3>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float3>[8] — ping-pong flood centroid accumulator back buffer — owner: SubmarineFluidDynamics
            _comAccumulatorBack = new NativeArray<float3>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<FloodMassPropertiesResult>[1] — front flood mass-properties result buffer — owner: SubmarineFluidDynamics
            _massPropertiesFront = new NativeArray<FloodMassPropertiesResult>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<FloodMassPropertiesResult>[1] — back flood mass-properties result buffer — owner: SubmarineFluidDynamics
            _massPropertiesBack = new NativeArray<FloodMassPropertiesResult>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float3>[16] — local angular-velocity slosh history supporting 50–150 ms delayed counter-torque taps — owner: SubmarineFluidDynamics
            _angularVelocityHistoryLocal = new NativeArray<float3>(RingBufferLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] — previous sampled exterior submersion factors for splash transition detection — owner: SubmarineFluidDynamics
            _previousExteriorSampleSubmersionFactors = new NativeArray<float>(ExteriorBuoyancySampleCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[8] â€” Burst fluid-transfer output volumes â€” owner: SubmarineFluidDynamics
            _jobFloodVolumes = new NativeArray<float>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<uint>[8] â€” Burst fluid-transfer output flags â€” owner: SubmarineFluidDynamics
            _jobCompartmentFlags = new NativeArray<uint>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[7] â€” per-bulkhead transfer delta scratch â€” owner: SubmarineFluidDynamics
            _bulkheadTransferDeltas = new NativeArray<float>(BulkheadCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeQueue<SplashEvent>(Persistent) — deferred exterior splash payload queue for VFX consumers — owner: SubmarineFluidDynamics
            _splashEventQueue = new NativeQueue<SplashEvent>(Allocator.Persistent);
        }

        private void SeedNativeStateFromAuthoring()
        {
            if (!_compartmentFloodVolumes.IsCreated)
                return;

            _configuredCompartmentCount = math.clamp(compartments != null ? compartments.Length : 0, 0, CompartmentCapacity);
            for (int i = 0; i < CompartmentCapacity; i++)
            {
                if (i < _configuredCompartmentCount)
                {
                    CompartmentDefinition definition = compartments[i];
                    float maxVolume = math.max(0f, definition.maxFloodVolumeCubicMeters);
                    float fillVolume = math.saturate(definition.initialFillNormalized) * maxVolume;
                    float breachArea = math.max(0f, definition.breachAreaSquareMeters);

                    _compartmentMaxVolumes[i] = maxVolume;
                    _compartmentFloodVolumes[i] = fillVolume;
                    _compartmentBreachAreas[i] = breachArea;
                    _compartmentLocalCentroids[i] = new float3(
                        definition.localCentroid.x,
                        definition.localCentroid.y,
                        definition.localCentroid.z);
                    _compartmentFlags[i] = breachArea > Epsilon ? FlagBreached : 0u;
                }
                else
                {
                    _compartmentMaxVolumes[i] = 0f;
                    _compartmentFloodVolumes[i] = 0f;
                    _compartmentBreachAreas[i] = 0f;
                    _compartmentLocalCentroids[i] = float3.zero;
                    _compartmentFlags[i] = 0u;
                }

                _comAccumulatorFront[i] = float3.zero;
                _comAccumulatorBack[i] = float3.zero;
            }

            if (_massPropertiesFront.IsCreated)
                _massPropertiesFront[0] = default;

            if (_massPropertiesBack.IsCreated)
                _massPropertiesBack[0] = default;

            if (bulkheads != null && bulkheads.Length > 0)
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
            _lastExternalBuoyancyForce = Vector3.zero;
            _lastExternalBuoyancyTorque = Vector3.zero;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            SystemDispatcher.EnsureRuntimeInstance();
            GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Environment);
            _registered = true;
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
            _registered = false;
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
            ConsumeCompletedFluidTransfer();
        }

        private void CompletePendingFloodMassPropertiesForAuthoritativeWrite()
        {
            if (!_massPropertiesJobRunning)
                return;

            // COLD SYNC JOB: authoritative compartment writes must not race a pending flood mass-properties job.
            _massPropertiesJobHandle.Complete();
            ConsumeCompletedFloodMassProperties();
        }

        private void ConsumeCompletedFluidTransfer()
        {
            if (!_fluidJobRunning || !_fluidJobHandle.IsCompleted || !_jobFloodVolumes.IsCreated || !_jobCompartmentFlags.IsCreated)
                return;

            _fluidJobHandle.Complete();
            _fluidJobHandle = default;
            _fluidJobRunning = false;

            NativeArray<float> floodVolumeFrontBuffer = _compartmentFloodVolumes;
            _compartmentFloodVolumes = _jobFloodVolumes;
            _jobFloodVolumes = floodVolumeFrontBuffer;

            NativeArray<uint> flagFrontBuffer = _compartmentFlags;
            _compartmentFlags = _jobCompartmentFlags;
            _jobCompartmentFlags = flagFrontBuffer;
        }

        private void ConsumeCompletedFloodMassProperties()
        {
            if (!_massPropertiesJobRunning || !_massPropertiesJobHandle.IsCompleted || !_massPropertiesBack.IsCreated)
                return;

            _massPropertiesJobHandle.Complete();
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
                DischargeCoefficient = dischargeCoefficient
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
                float characteristicHeightA = math.max(0.1f, math.cbrt(maxVolumeA));
                float characteristicHeightB = math.max(0.1f, math.cbrt(maxVolumeB));
                float headHeightA = fillA * characteristicHeightA;
                float headHeightB = fillB * characteristicHeightB;
                float headDifferenceMeters = headHeightA - headHeightB;
                float velocityMetersPerSecond = math.sqrt(math.max(0f, 2f * GravityMetersPerSecondSquared * math.abs(headDifferenceMeters)));
                float signedDeltaVolume =
                    math.sign(headDifferenceMeters) *
                    doorAreaSquareMeters *
                    safeDischargeCoefficient *
                    velocityMetersPerSecond *
                    transferCoefficient *
                    fixedDeltaTime;
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

        private void FinalizeCompartmentState()
        {
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
            float targetMass = dryMass + floodMass;
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
            float3 blendedCenter = math.lerp(currentCenter, targetCenter, _centerOfMassBlendAlpha);
            if (!math.all(math.isfinite(blendedCenter)))
            {
                EmergencyResetHydrodynamics("ApplyCenterOfMassShift.BlendedCenter");
                return;
            }

            float3 delta = blendedCenter - currentCenter;
            float maxCenterDelta = math.max(0.001f, maxCenterOfMassDeltaPerTickMeters);
            float deltaMagnitude = math.length(delta);
            if (deltaMagnitude > maxCenterDelta)
            {
                if (!TryResolveSafeQuotient(maxCenterDelta, deltaMagnitude, out float centerClampScale))
                {
                    EmergencyResetHydrodynamics("ApplyCenterOfMassShift.Clamp");
                    return;
                }
                else
                {
                    blendedCenter = currentCenter + (delta * centerClampScale);
                }
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

            _currentFloodCenterOfMassLocal = new Vector3(targetCenter.x, targetCenter.y, targetCenter.z);
            return targetCenter;
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

            float delaySeconds = ResolveSloshDelaySeconds(internalFloodRatio);
            float safeFixedStep = math.max(_currentFixedDeltaTime, DefaultFixedStepSeconds);
            int delayFrames = 1;
            if (!TryResolveSafeQuotient(delaySeconds, safeFixedStep, out float delayFrameFloat))
            {
                EmergencyResetHydrodynamics("RecordAndSampleDelayedSloshAngularVelocityLocal.DelayFrames");
            }
            else
            {
                delayFrames = math.clamp(
                    (int)math.round(delayFrameFloat),
                    1,
                    RingBufferLength - 1);
            }

            int delayIndex = (_ringHead - delayFrames - 1) & RingBufferMask;
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
            float surfaceY = _cachedTransform.position.y + safeDepthMeters;
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

            Vector3 totalForce = Vector3.zero;
            Vector3 totalTorque = Vector3.zero;
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

                float submersionFactor = ResolveSurfaceSubmersionFactor(worldPoint.y - surfaceY);
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

                Vector3 sampleForce = Vector3.up * (perSampleForceMagnitude * submersionFactor);
                if (!IsFiniteVector(sampleForce))
                {
                    EmergencyResetHydrodynamics("ApplySampledExteriorBuoyancy.SampleForce");
                    _externalSubmergedVolumeCubicMeters = 0f;
                    _submersionFactor = 0f;
                    _lastExternalBuoyancyForce = Vector3.zero;
                    _lastExternalBuoyancyTorque = Vector3.zero;
                    return;
                }

                totalForce += sampleForce;
                totalTorque += Vector3.Cross(worldPoint - centerOfMassWorld, sampleForce);
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

            totalForce = ClampMagnitude(totalForce, maxForceMagnitude);
            totalTorque = ClampMagnitude(totalTorque, maxTorqueMagnitude);
            float3 totalForceFloat = new float3(totalForce.x, totalForce.y, totalForce.z);
            float3 totalTorqueFloat = new float3(totalTorque.x, totalTorque.y, totalTorque.z);
            if (math.any(math.isnan(totalForceFloat)) || math.any(math.isnan(totalTorqueFloat)) ||
                !math.all(math.isfinite(totalForceFloat)) || !math.all(math.isfinite(totalTorqueFloat)))
            {
                EmergencyResetHydrodynamics("ApplySampledExteriorBuoyancy.Result");
                totalForce = Vector3.zero;
                totalTorque = Vector3.zero;
            }

            if (!IsFiniteVector(totalForce))
                totalForce = Vector3.zero;
            if (!IsFiniteVector(totalTorque))
                totalTorque = Vector3.zero;

            totalForce = ApplyHydrodynamicLinearInertiaScale(totalForce);
            totalTorque = ApplyHydrodynamicAngularInertiaScale(totalTorque);
            _lastExternalBuoyancyForce = totalForce;
            _lastExternalBuoyancyTorque = totalTorque;

            if (totalForce.sqrMagnitude > Epsilon)
                PhysicsForceRouter.QueueForce(_rigidbody, totalForce, ForceMode.Force);

            if (totalTorque.sqrMagnitude > Epsilon)
                PhysicsForceRouter.QueueTorque(_rigidbody, totalTorque, ForceMode.Force);
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
            if (!float.IsFinite(criticalFloodRatio) ||
                !float.IsFinite(dampingSubmersion) ||
                !float.IsFinite(linearScale) ||
                !float.IsFinite(angularScale))
            {
                EmergencyResetHydrodynamics("ApplyAddedMassDamping.Scale");
                return;
            }

            float targetLinearDamping = _baseLinearDamping * (1f + (linearScale * dampingSubmersion));
            float targetAngularDamping = _baseAngularDamping * (1f + (angularScale * dampingSubmersion));
            if (!float.IsFinite(targetLinearDamping) || !float.IsFinite(targetAngularDamping))
            {
                EmergencyResetHydrodynamics("ApplyAddedMassDamping.Result");
                return;
            }

            _currentHydrodynamicLinearInertiaScale = math.max(1f, 1f + (linearScale * dampingSubmersion));
            _currentHydrodynamicAngularInertiaScale = math.max(1f, 1f + (angularScale * dampingSubmersion));

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
            float criticalFloodRatio = math.smoothstep(CriticalFillThreshold, 1f, internalFloodRatio);
            float delayedResistanceScale = math.max(0f, sloshFactor) *
                internalFloodRatio *
                (1f + (criticalFloodRatio * CriticalFloodSloshResistanceBoost));

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
                float sloshMass = currentVolume * WaterDensityKgPerCubicMeter * math.lerp(1f, 3f, criticalFloodRatio);
                if (!float.IsFinite(sloshMass))
                {
                    EmergencyResetHydrodynamics("ApplyDelayedSloshTorque.SloshMass");
                    _lastSloshTorqueLocal = Vector3.zero;
                    return;
                }

                totalSloshTorque += -delayedAngularVelocity * (fillRatio * sloshMass * freesurf);
                if (math.any(math.isnan(totalSloshTorque)) || !math.all(math.isfinite(totalSloshTorque)))
                {
                    EmergencyResetHydrodynamics("ApplyDelayedSloshTorque.Accumulate");
                    _lastSloshTorqueLocal = Vector3.zero;
                    return;
                }
            }

            float floodedHullMass = math.max(0f, _totalFloodVolumeCubicMeters) * WaterDensityKgPerCubicMeter;
            float resistanceLeverArm = math.max(0.1f, _exteriorBuoyancyMaxLeverArm);
            float3 rotationalResistanceTorque = -delayedAngularVelocity *
                (floodedHullMass * resistanceLeverArm * FloodedHullRotationalResistanceScale * criticalFloodRatio);
            if (math.any(math.isnan(rotationalResistanceTorque)) || !math.all(math.isfinite(rotationalResistanceTorque)))
            {
                EmergencyResetHydrodynamics("ApplyDelayedSloshTorque.RotationalResistance");
                _lastSloshTorqueLocal = Vector3.zero;
                return;
            }

            totalSloshTorque += rotationalResistanceTorque;
            totalSloshTorque *= delayedResistanceScale;

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

            PhysicsForceRouter.QueueTorque(_rigidbody, worldTorque, ForceMode.Force);
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

            _splashEventQueue.Enqueue(new SplashEvent
            {
                RuntimePosition = new float3(worldPoint.x, worldPoint.y, worldPoint.z),
                AbsoluteUniversePosition = new float3(absoluteUniversePosition.x, absoluteUniversePosition.y, absoluteUniversePosition.z),
                SurfaceNormal = new float3(0f, 1f, 0f),
                ImpactSpeedMetersPerSecond = impactSpeedMetersPerSecond,
                KineticEnergyJoules = kineticEnergyJoules,
                SubmersionFactor = currentSubmersionFactor,
                SampleIndex = sampleIndex
            });
            _queuedSplashEventCount++;
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
        private static float ResolveSloshDelaySeconds(float internalFloodRatio)
        {
            float safeFloodRatio = math.saturate(math.select(0f, internalFloodRatio, math.isfinite(internalFloodRatio)));
            return math.lerp(MinimumSloshDelaySeconds, MaximumSloshDelaySeconds, safeFloodRatio);
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
                totalCapacity += _compartmentMaxVolumes[i];

            return totalCapacity;
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
