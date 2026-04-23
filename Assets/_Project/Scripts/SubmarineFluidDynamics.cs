using System.Runtime.CompilerServices;
using Hecton8.Atmosphere;
using Hecton8.Core;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    /// <summary>
    /// Fixed-step flooded-interior model for submarine rigidbodies.
    /// Tracks compartment fill, bulkhead isolation, inertia blending, and delayed slosh torque
    /// without rewriting rigidbody mass or center of mass every physics step.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Hecton/Physics/Submarine Fluid Dynamics")]
    public sealed class SubmarineFluidDynamics : MonoBehaviour, IFixedTickable
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
        private const float DefaultMaxTransferPerTick = 0.1f;
        private const float DefaultSloshFactor = 0.15f;
        private const float DefaultSloshMinimumVolume = 0.05f;
        private const float DefaultMaxSloshTorque = 50000f;
        private const float DefaultReportedCenterTauSeconds = 1.2f;
        private const float DefaultMaximumIngressPerSecondNormalized = 0.25f;
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
        }

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

        [Tooltip("Dry-hull local center used only for telemetry. Rigidbody.centerOfMass is not rewritten at runtime.")]
        [SerializeField] private Vector3 dryCenterOfMassLocal = Vector3.zero;

        [Tooltip("Time constant used to smooth the reported flood centroid for downstream telemetry and VFX.")]
        [SerializeField, Min(0.1f)] private float reportedCenterTauSeconds = DefaultReportedCenterTauSeconds;

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

        [Header("── Depth Source ──────────────────")]
        [Tooltip("When true, depth is sampled from the atmosphere sea level relative to the hull position.")]
        [SerializeField] private bool sampleDepthFromAtmosphere = true;

        [Tooltip("Fallback or manual external depth when atmospheric sea level sampling is disabled.")]
        [SerializeField, Min(0f)] private float manualExternalDepthMeters;

        [Header("── Diagnostics ───────────────────")]
        [SerializeField] private int _debugConfiguredCompartmentCount;
        [SerializeField] private int _debugConfiguredBulkheadCount;
        [SerializeField] private float _debugExternalDepthMeters;
        [SerializeField] private float _debugTotalFloodVolumeCubicMeters;
        [SerializeField] private float _debugFloodFillRatio;
        [SerializeField] private Vector3 _debugReportedFloodCenterOfMassLocal;
        [SerializeField] private Vector3 _debugAppliedInertiaTensor;
        [SerializeField] private Vector3 _debugLastSloshTorqueLocal;

        private Rigidbody _rigidbody;
        private Transform _cachedTransform;
        private bool _registered;
        private int _configuredCompartmentCount;
        private int _configuredBulkheadCount;
        private int _ringHead;
        private float _externalDepthMeters;
        private float _floodFillRatio;
        private float _totalFloodVolumeCubicMeters;
        private float _reportedCenterBlendAlpha;
        private float _reportedCenterBlendFixedStep = -1f;
        private Vector3 _reportedFloodCenterOfMassLocal;
        private Vector3 _lastAppliedInertiaTensor;
        private Vector3 _lastSloshTorqueLocal;
        private JobHandle _disposeHandle;

        private NativeArray<float> _compartmentFloodVolumes;
        private NativeArray<float> _compartmentMaxVolumes;
        private NativeArray<float> _compartmentBreachAreas;
        private NativeArray<float3> _compartmentLocalCentroids;
        private NativeArray<uint> _compartmentFlags;
        private NativeArray<int2> _bulkheadPairs;
        private NativeArray<byte> _bulkheadSealed;
        private NativeArray<float3> _comAccumulatorFront;
        private NativeArray<float3> _comAccumulatorBack;
        private NativeArray<float3> _angularVelocityHistoryLocal;

        /// <summary>Configured compartment count authored for this submarine, clamped to the supported maximum.</summary>
        public int CompartmentCount => _configuredCompartmentCount;

        /// <summary>Total flood volume currently tracked across all compartments.</summary>
        public float TotalFloodVolumeCubicMeters => _totalFloodVolumeCubicMeters;

        /// <summary>Normalized total fill ratio across the authored compartment capacity.</summary>
        public float FloodFillRatio => _floodFillRatio;

        /// <summary>Reported local-space flood centroid for telemetry, audio, or VFX queries.</summary>
        public Vector3 ReportedFloodCenterOfMassLocal => _reportedFloodCenterOfMassLocal;

        /// <summary>Resolved external water depth used by the current ingress step.</summary>
        public float ExternalDepthMeters => _externalDepthMeters;

        private void Awake()
        {
            CacheReferences();
            EnsureNativeState();
            SanitizeAuthoredTensors();
            SeedNativeStateFromAuthoring();
            RefreshDerivedConstants(DefaultFixedStepSeconds);
            RefreshDebugState();
        }

        private void OnEnable()
        {
            CacheReferences();
            EnsureNativeState();
            SanitizeAuthoredTensors();
            SeedNativeStateFromAuthoring();
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

        /// <summary>
        /// Fixed-step fluid ingress, inter-compartment transfer, inertia interpolation, and delayed slosh torque.
        /// </summary>
        /// <param name="fixedDeltaTime">Discrete physics step provided by GameTickManager.</param>
        public void FixedTick(float fixedDeltaTime)
        {
            if (!_compartmentFloodVolumes.IsCreated || _rigidbody == null || fixedDeltaTime <= 0f)
                return;

            RefreshDerivedConstants(fixedDeltaTime);
            ClearTransientFlags();
            SyncBulkheadSealedFlags();

            float resolvedDepthMeters = ResolveExternalDepthMeters();
            SimulateIngress(resolvedDepthMeters, fixedDeltaTime);
            SimulateBulkheadTransfer(fixedDeltaTime);
            FinalizeCompartmentState();
            UpdateReportedFloodCenter();
            ApplyInterpolatedInertiaTensor();
            ApplyDelayedSloshTorque();
            RefreshDebugState();
        }

        /// <summary>
        /// Overrides automatic depth sampling with a manual external water depth.
        /// </summary>
        public void SetExternalDepthMeters(float depthMeters)
        {
            sampleDepthFromAtmosphere = false;
            manualExternalDepthMeters = Mathf.Max(0f, depthMeters);
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

            float sanitizedArea = Mathf.Max(0f, breachAreaSquareMeters);
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

            float maxVolume = _compartmentMaxVolumes[compartmentIndex];
            _compartmentFloodVolumes[compartmentIndex] = math.saturate(fillNormalized) * maxVolume;
            FinalizeCompartmentState();
            UpdateReportedFloodCenter();
            ApplyInterpolatedInertiaTensor();
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
            return maxVolume > Epsilon
                ? math.saturate(_compartmentFloodVolumes[compartmentIndex] / maxVolume)
                : 0f;
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
        }

        private void EnsureNativeState()
        {
            if (_compartmentFloodVolumes.IsCreated)
                return;

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
            // COLD ALLOC: NativeArray<float3>[8] — ping-pong flood centroid accumulator front buffer — owner: SubmarineFluidDynamics
            _comAccumulatorFront = new NativeArray<float3>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float3>[8] — ping-pong flood centroid accumulator back buffer — owner: SubmarineFluidDynamics
            _comAccumulatorBack = new NativeArray<float3>(CompartmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float3>[8] — local angular-velocity slosh history — owner: SubmarineFluidDynamics
            _angularVelocityHistoryLocal = new NativeArray<float3>(RingBufferLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private void SeedNativeStateFromAuthoring()
        {
            if (!_compartmentFloodVolumes.IsCreated)
                return;

            _configuredCompartmentCount = Mathf.Clamp(compartments != null ? compartments.Length : 0, 0, CompartmentCapacity);
            for (int i = 0; i < CompartmentCapacity; i++)
            {
                if (i < _configuredCompartmentCount)
                {
                    CompartmentDefinition definition = compartments[i];
                    float maxVolume = Mathf.Max(0f, definition.maxFloodVolumeCubicMeters);
                    float fillVolume = math.saturate(definition.initialFillNormalized) * maxVolume;
                    float breachArea = Mathf.Max(0f, definition.breachAreaSquareMeters);

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

            if (bulkheads != null && bulkheads.Length > 0)
            {
                _configuredBulkheadCount = Mathf.Clamp(bulkheads.Length, 0, BulkheadCapacity);
                for (int i = 0; i < _configuredBulkheadCount; i++)
                {
                    BulkheadDefinition bulkhead = bulkheads[i];
                    _bulkheadPairs[i] = new int2(
                        Mathf.Clamp(bulkhead.compartmentA, 0, CompartmentCapacity - 1),
                        Mathf.Clamp(bulkhead.compartmentB, 0, CompartmentCapacity - 1));
                    _bulkheadSealed[i] = bulkhead.isSealed ? (byte)1 : (byte)0;
                }
            }
            else
            {
                _configuredBulkheadCount = BulkheadCapacity;
                for (int i = 0; i < BulkheadCapacity; i++)
                {
                    _bulkheadPairs[i] = new int2(i, i + 1);
                    _bulkheadSealed[i] = 0;
                }
            }

            for (int i = _configuredBulkheadCount; i < BulkheadCapacity; i++)
            {
                _bulkheadPairs[i] = new int2(-1, -1);
                _bulkheadSealed[i] = 0;
            }

            for (int i = 0; i < RingBufferLength; i++)
                _angularVelocityHistoryLocal[i] = float3.zero;

            _ringHead = 0;
            _reportedFloodCenterOfMassLocal = dryCenterOfMassLocal;
            _lastAppliedInertiaTensor = SanitizeTensor(dryInertiaTensor);
            if (_rigidbody != null)
                _rigidbody.inertiaTensor = _lastAppliedInertiaTensor;

            FinalizeCompartmentState();
            UpdateReportedFloodCenter();
        }

        private void DisposeNativeStateDeferred()
        {
            DisposeDeferred(ref _compartmentFloodVolumes);
            DisposeDeferred(ref _compartmentMaxVolumes);
            DisposeDeferred(ref _compartmentBreachAreas);
            DisposeDeferred(ref _compartmentLocalCentroids);
            DisposeDeferred(ref _compartmentFlags);
            DisposeDeferred(ref _bulkheadPairs);
            DisposeDeferred(ref _bulkheadSealed);
            DisposeDeferred(ref _comAccumulatorFront);
            DisposeDeferred(ref _comAccumulatorBack);
            DisposeDeferred(ref _angularVelocityHistoryLocal);
        }

        private void TryRegister()
        {
            if (_registered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register((IFixedTickable)this);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister((IFixedTickable)this);

            _registered = false;
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

                float fillA = _compartmentFloodVolumes[compartmentA] / maxVolumeA;
                float fillB = _compartmentFloodVolumes[compartmentB] / maxVolumeB;
                float headDifference = fillA - fillB;
                float deltaVolume = math.clamp(headDifference * transferCoefficient * fixedDeltaTime, -perTickTransferCap, perTickTransferCap);

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
                    continue;
                }

                float currentVolume = math.clamp(_compartmentFloodVolumes[i], 0f, maxVolume);
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

                float fillRatio = currentVolume / maxVolume;
                if (fillRatio >= CriticalFillThreshold)
                    flags |= FlagCritical;
                else
                    flags &= ~FlagCritical;

                _compartmentFloodVolumes[i] = currentVolume;
                _compartmentFlags[i] = flags;
                totalFloodVolume += currentVolume;
            }

            _totalFloodVolumeCubicMeters = totalFloodVolume;
            float totalCapacity = ResolveTotalCapacityCubicMeters();
            _floodFillRatio = totalCapacity > Epsilon
                ? math.saturate(totalFloodVolume / totalCapacity)
                : 0f;
        }

        private void UpdateReportedFloodCenter()
        {
            float totalFloodMass = 0f;
            float3 weightedSum = float3.zero;
            for (int i = 0; i < CompartmentCapacity; i++)
            {
                float currentVolume = _compartmentFloodVolumes[i];
                float mass = currentVolume * WaterDensityKgPerCubicMeter;
                float3 weightedCentroid = _compartmentLocalCentroids[i] * mass;
                _comAccumulatorBack[i] = weightedCentroid;
                weightedSum += weightedCentroid;
                totalFloodMass += mass;
            }

            float3 dryCenter = new float3(dryCenterOfMassLocal.x, dryCenterOfMassLocal.y, dryCenterOfMassLocal.z);
            float3 floodCenter = totalFloodMass > Epsilon
                ? weightedSum / totalFloodMass
                : dryCenter;
            float3 targetCenter = math.lerp(dryCenter, floodCenter, _floodFillRatio);
            float3 currentCenter = new float3(
                _reportedFloodCenterOfMassLocal.x,
                _reportedFloodCenterOfMassLocal.y,
                _reportedFloodCenterOfMassLocal.z);
            float3 blendedCenter = math.lerp(currentCenter, targetCenter, _reportedCenterBlendAlpha);
            if (!math.all(math.isfinite(blendedCenter)))
                blendedCenter = dryCenter;

            _reportedFloodCenterOfMassLocal = new Vector3(blendedCenter.x, blendedCenter.y, blendedCenter.z);

            NativeArray<float3> swap = _comAccumulatorFront;
            _comAccumulatorFront = _comAccumulatorBack;
            _comAccumulatorBack = swap;
        }

        private void ApplyInterpolatedInertiaTensor()
        {
            if (_rigidbody == null)
                return;

            Vector3 tensor = Vector3.Lerp(dryInertiaTensor, fullyFloodedInertiaTensor, _floodFillRatio);
            tensor = SanitizeTensor(tensor);
            if ((_lastAppliedInertiaTensor - tensor).sqrMagnitude <= 0.000001f)
                return;

            _rigidbody.inertiaTensor = tensor;
            _lastAppliedInertiaTensor = tensor;
        }

        private void ApplyDelayedSloshTorque()
        {
            if (_rigidbody == null || !_angularVelocityHistoryLocal.IsCreated)
                return;

            Vector3 worldAngularVelocity = _rigidbody.angularVelocity;
            Vector3 localAngularVelocity = _cachedTransform != null
                ? _cachedTransform.InverseTransformDirection(worldAngularVelocity)
                : worldAngularVelocity;

            float3 currentLocalAngularVelocity = new float3(
                localAngularVelocity.x,
                localAngularVelocity.y,
                localAngularVelocity.z);
            _angularVelocityHistoryLocal[_ringHead] = currentLocalAngularVelocity;
            _ringHead = (_ringHead + 1) & RingBufferMask;

            int delayIndex = (_ringHead - SloshDelayFrames - 1) & RingBufferMask;
            float3 delayedAngularVelocity = _angularVelocityHistoryLocal[delayIndex];
            float3 totalSloshTorque = float3.zero;

            for (int i = 0; i < _configuredCompartmentCount; i++)
            {
                float currentVolume = _compartmentFloodVolumes[i];
                if (currentVolume < sloshMinimumVolumeCubicMeters)
                    continue;

                float maxVolume = _compartmentMaxVolumes[i];
                if (maxVolume <= Epsilon)
                    continue;

                float fillRatio = math.saturate(currentVolume / maxVolume);
                float freeSurfaceFactor = 1f - fillRatio;
                freeSurfaceFactor *= freeSurfaceFactor;
                float sloshMass = currentVolume * WaterDensityKgPerCubicMeter;
                totalSloshTorque += -delayedAngularVelocity * (fillRatio * math.max(0f, sloshFactor) * sloshMass * freeSurfaceFactor);
            }

            float maxTorqueMagnitude = math.max(0f, maxSloshTorque);
            if (maxTorqueMagnitude > Epsilon)
            {
                float torqueMagnitude = math.length(totalSloshTorque);
                if (torqueMagnitude > maxTorqueMagnitude && torqueMagnitude > Epsilon)
                    totalSloshTorque = (totalSloshTorque / torqueMagnitude) * maxTorqueMagnitude;
            }

            if (!math.all(math.isfinite(totalSloshTorque)) || math.lengthsq(totalSloshTorque) <= Epsilon)
            {
                _lastSloshTorqueLocal = Vector3.zero;
                return;
            }

            Vector3 localTorque = new Vector3(totalSloshTorque.x, totalSloshTorque.y, totalSloshTorque.z);
            _rigidbody.AddRelativeTorque(localTorque, ForceMode.Force);
            _lastSloshTorqueLocal = localTorque;
        }

        private float ResolveExternalDepthMeters()
        {
            if (sampleDepthFromAtmosphere)
            {
                HectonAtmosphereManager atmosphereManager = HectonAtmosphereManager.Instance;
                if (atmosphereManager != null && _cachedTransform != null)
                {
                    _externalDepthMeters = math.max(0f, atmosphereManager.SeaLevelY - _cachedTransform.position.y);
                    return _externalDepthMeters;
                }
            }

            _externalDepthMeters = math.max(0f, manualExternalDepthMeters);
            return _externalDepthMeters;
        }

        private void RefreshDerivedConstants(float fixedDeltaTime)
        {
            float safeFixedStep = fixedDeltaTime > 0f ? fixedDeltaTime : DefaultFixedStepSeconds;
            if (math.abs(_reportedCenterBlendFixedStep - safeFixedStep) <= 0.0001f)
                return;

            float tau = math.max(0.1f, reportedCenterTauSeconds);
            _reportedCenterBlendAlpha = 1f - math.exp(-safeFixedStep / tau);
            _reportedCenterBlendFixedStep = safeFixedStep;
        }

        private void SanitizeAuthoredTensors()
        {
            dryInertiaTensor = SanitizeTensor(dryInertiaTensor);
            fullyFloodedInertiaTensor = SanitizeTensor(fullyFloodedInertiaTensor);
        }

        private void RefreshDebugState()
        {
            _debugConfiguredCompartmentCount = _configuredCompartmentCount;
            _debugConfiguredBulkheadCount = _configuredBulkheadCount;
            _debugExternalDepthMeters = _externalDepthMeters;
            _debugTotalFloodVolumeCubicMeters = _totalFloodVolumeCubicMeters;
            _debugFloodFillRatio = _floodFillRatio;
            _debugReportedFloodCenterOfMassLocal = _reportedFloodCenterOfMassLocal;
            _debugAppliedInertiaTensor = _lastAppliedInertiaTensor;
            _debugLastSloshTorqueLocal = _lastSloshTorqueLocal;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ResolveTotalCapacityCubicMeters()
        {
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
            tensor.x = Mathf.Max(0.001f, tensor.x);
            tensor.y = Mathf.Max(0.001f, tensor.y);
            tensor.z = Mathf.Max(0.001f, tensor.z);
            return tensor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsCompartmentIndexValid(int index)
        {
            return index >= 0 && index < _configuredCompartmentCount;
        }

        private void DisposeDeferred<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            _disposeHandle = array.Dispose(_disposeHandle);
            array = default;
        }
    }
}
