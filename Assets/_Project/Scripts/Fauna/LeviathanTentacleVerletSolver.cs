using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.AI
{
    /// <summary>
    /// Data-oriented Verlet tentacle runtime for leviathan-class fauna.
    /// </summary>
    [DisallowMultipleComponent]
    // Runs early enough to register with the dispatcher before fauna presentation consumers sample matrices.
    [DefaultExecutionOrder(-9910)]
    internal sealed class LeviathanTentacleVerletSolver : MonoBehaviour, IUpdatable, ILateFrameTickable, IOriginShiftListener, IDisposable
    {
        private const int MaxTentacles = 8;
        private const int SegmentsPerTentacle = 20;
        private const int SegmentLastIndex = SegmentsPerTentacle - 1;
        private const int TotalSegments = MaxTentacles * SegmentsPerTentacle;
        private const int TelemetryCapacity = 300;
        private const uint TentacleStateActive = 1u << 0;
        private const uint TentacleStateGrabbing = 1u << 1;
        private const string NativeMemoryOwner = nameof(LeviathanTentacleVerletSolver);
        private const string TelemetryDumpRelativePath = "Docs/AgentLogs/Dump_LEVIATHAN_TENTACLE_IK.bin";
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;
        private const ulong TelemetryDumpMagic = 0x484543544F4E3800UL;
        private const int TelemetryEntryPayloadBytes = 64;
        private const float FlowGridIntegerEpsilon = 0.01f;
        private const float FlowGridMinSpacing = 0.001f;
        private const float ConstraintIterationHysteresisSeconds = 2.5f;
        private const int AbyssalFlowVectorStrideBytes = 16;
        private const int MaxSupportedAbyssalFlowAxis = 4096;

        private static readonly int _MatrixBufferId = Shader.PropertyToID("_H8LeviathanTentacleMatrices");
        private static readonly int _RadiusBufferId = Shader.PropertyToID("_H8LeviathanTentacleRadius");
        private static readonly int _AbyssalFlowFieldId = Shader.PropertyToID("_H8AbyssalFlowField");
        private static readonly int _AbyssalFlowResolutionId = Shader.PropertyToID("_H8AbyssalFlowResolution");
        private static readonly int _AbyssalFlowCenterId = Shader.PropertyToID("_H8AbyssalFlowCenter");
        private static readonly int _AbyssalFlowSpacingId = Shader.PropertyToID("_H8AbyssalFlowSpacing");
        private static readonly int _AbyssalFlowActiveId = Shader.PropertyToID("_H8AbyssalFlowActive");
        private static readonly int _BaseRadiusReferenceId = Shader.PropertyToID("_BaseRadiusReference");
        private static readonly int _TipRadiusReferenceId = Shader.PropertyToID("_TipRadiusReference");

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 64)]
        private struct LeviathanTentacleTelemetryEntry
        {
            public int FrameIndex;
            public int ActiveTentacleCount;
            public uint Flags;
            public uint StateHash;
            public float3 Root0;
            public float3 Tip0;
            public float3 FlowVector;
            public float MaxStretchFraction;
            public float Padding0;
            public float Padding1;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
        private struct VerletSolveJob : IJob
        {
            private const float MinDistanceSq = 0.000001f;

            public NativeArray<float3> Positions;
            public NativeArray<float3> PreviousPositions;
            public NativeArray<float> Radius;
            public NativeArray<float4x4> SegmentMatrices;
            public NativeArray<float> StretchFractions;
            public NativeArray<float3> ConstraintCorrections;
            public NativeArray<int> ConstraintCorrectionCounts;
            [ReadOnly] public NativeArray<float3> RootPositions;
            [ReadOnly] public NativeArray<float3> TargetPositions;
            [ReadOnly] public NativeArray<uint> TentacleStates;
            public float DeltaTime;
            public float Damping;
            public float RestLength;
            public float MaxStretchLength;
            public float BaseRadius;
            public float TipRadius;
            public float FlowStrength;
            public float FlowNoiseStrength;
            public float SuctionPulseStrength;
            public float TimeSeconds;
            public float3 Gravity;
            public float3 FlowVector;
            public int TentacleCount;
            public int ConstraintIterations;

            public void Execute()
            {
                float safeDeltaTime = math.select(0f, math.min(DeltaTime, 0.05f), math.isfinite(DeltaTime) && DeltaTime > 0f);
                float dtSq = safeDeltaTime * safeDeltaTime;
                float safeDamping = math.clamp(Damping, 0f, 1f);
                float safeRestLength = math.max(0.001f, RestLength);
                float safeMaxStretchLength = math.max(safeRestLength * SegmentLastIndex, MaxStretchLength);
                float safeBaseRadius = math.max(0.001f, BaseRadius);
                float safeTipRadius = math.max(0.001f, TipRadius);
                float safeFlowStrength = math.max(0f, FlowStrength);
                float safeFlowNoiseStrength = math.max(0f, FlowNoiseStrength);
                float safePulseStrength = math.max(0f, SuctionPulseStrength);
                float safeTime = math.select(0f, TimeSeconds, math.isfinite(TimeSeconds));
                float3 safeGravity = SanitizeFinite(Gravity, float3.zero);
                float3 safeFlow = SanitizeFinite(FlowVector, float3.zero) * safeFlowStrength;
                int safeTentacleCount = math.clamp(TentacleCount, 0, MaxTentacles);
                int safeIterations = math.clamp(ConstraintIterations, 1, 3);
                float invSegmentLast = math.rcp(SegmentLastIndex);

                for (int tentacleIndex = 0; tentacleIndex < MaxTentacles; tentacleIndex++)
                    StretchFractions[tentacleIndex] = 0f;

                for (int tentacleIndex = 0; tentacleIndex < safeTentacleCount; tentacleIndex++)
                {
                    uint state = TentacleStates[tentacleIndex];
                    if ((state & TentacleStateActive) == 0u)
                        continue;

                    int baseIndex = FlatIndex(tentacleIndex, 0);
                    float3 rootPosition = SanitizeFinite(RootPositions[tentacleIndex], float3.zero);
                    float3 targetPosition = SanitizeFinite(TargetPositions[tentacleIndex], rootPosition);
                    bool grabbing = (state & TentacleStateGrabbing) != 0u;
                    targetPosition = ResolveClampedTarget(rootPosition, targetPosition, grabbing, safeMaxStretchLength, out float stretchFraction);
                    StretchFractions[tentacleIndex] = grabbing ? stretchFraction : 0f;

                    Positions[baseIndex] = rootPosition;
                    PreviousPositions[baseIndex] = rootPosition;

                    for (int segmentIndex = 1; segmentIndex < SegmentsPerTentacle; segmentIndex++)
                    {
                        int nodeIndex = FlatIndex(tentacleIndex, segmentIndex);
                        float t = segmentIndex * invSegmentLast;
                        float middleMask = math.saturate(1f - math.abs((t * 2f) - 1f));
                        float phase = safeTime * (0.61f + tentacleIndex * 0.071f) + t * 3.713f;
                        float waveA = CheapTriangleWave(phase) * 2f - 1f;
                        float waveB = CheapTriangleWave(phase * 0.73f + 0.19f) * 2f - 1f;
                        float3 organicNoise = new float3(waveA, waveB * 0.35f, -waveA * 0.52f) *
                            (safeFlowNoiseStrength * middleMask);

                        float3 current = SanitizeFinite(Positions[nodeIndex], rootPosition);
                        float3 previous = SanitizeFinite(PreviousPositions[nodeIndex], current);
                        float3 velocity = (current - previous) * safeDamping;
                        float3 next = current + velocity + ((safeGravity + safeFlow + organicNoise) * dtSq);
                        PreviousPositions[nodeIndex] = current;
                        Positions[nodeIndex] = SanitizeFinite(next, current);
                    }

                    if (grabbing)
                        Positions[FlatIndex(tentacleIndex, SegmentLastIndex)] = targetPosition;

                    for (int iteration = 0; iteration < safeIterations; iteration++)
                        SolveDistanceConstraintsJacobi(tentacleIndex, rootPosition, targetPosition, grabbing, safeRestLength);

                    if (grabbing)
                        Positions[FlatIndex(tentacleIndex, SegmentLastIndex)] = targetPosition;

                    WriteRadiusAndMatrices(tentacleIndex, rootPosition, grabbing, safeBaseRadius, safeTipRadius, safePulseStrength, safeTime, invSegmentLast);
                }
            }

            private void SolveDistanceConstraintsJacobi(
                int tentacleIndex,
                float3 rootPosition,
                float3 targetPosition,
                bool grabbing,
                float safeRestLength)
            {
                int baseIndex = FlatIndex(tentacleIndex, 0);
                Positions[baseIndex] = rootPosition;
                if (grabbing)
                    Positions[FlatIndex(tentacleIndex, SegmentLastIndex)] = targetPosition;

                for (int segmentIndex = 0; segmentIndex < SegmentsPerTentacle; segmentIndex++)
                {
                    int nodeIndex = baseIndex + segmentIndex;
                    ConstraintCorrections[nodeIndex] = float3.zero;
                    ConstraintCorrectionCounts[nodeIndex] = 0;
                }

                for (int segmentIndex = 1; segmentIndex < SegmentsPerTentacle; segmentIndex++)
                {
                    int aIndex = FlatIndex(tentacleIndex, segmentIndex - 1);
                    int bIndex = FlatIndex(tentacleIndex, segmentIndex);
                    float3 a = SanitizeFinite(Positions[aIndex], rootPosition);
                    float3 b = SanitizeFinite(Positions[bIndex], rootPosition);
                    float3 delta = b - a;
                    float distanceSq = math.lengthsq(delta);
                    float invDistance = math.rsqrt(math.max(distanceSq, MinDistanceSq));
                    float distance = distanceSq * invDistance;
                    float3 direction = delta * invDistance;
                    float3 correction = direction * (distance - safeRestLength);
                    bool aPinned = segmentIndex == 1;
                    bool bPinned = grabbing && segmentIndex == SegmentLastIndex;

                    if (aPinned && !bPinned)
                    {
                        ConstraintCorrections[bIndex] -= correction;
                        ConstraintCorrectionCounts[bIndex] += 1;
                    }
                    else if (!aPinned && bPinned)
                    {
                        ConstraintCorrections[aIndex] += correction;
                        ConstraintCorrectionCounts[aIndex] += 1;
                    }
                    else if (!aPinned)
                    {
                        ConstraintCorrections[aIndex] += correction * 0.5f;
                        ConstraintCorrectionCounts[aIndex] += 1;
                        ConstraintCorrections[bIndex] -= correction * 0.5f;
                        ConstraintCorrectionCounts[bIndex] += 1;
                    }
                }

                for (int segmentIndex = 1; segmentIndex < SegmentsPerTentacle; segmentIndex++)
                {
                    int nodeIndex = baseIndex + segmentIndex;
                    if (grabbing && segmentIndex == SegmentLastIndex)
                    {
                        Positions[nodeIndex] = targetPosition;
                        continue;
                    }

                    int correctionCount = ConstraintCorrectionCounts[nodeIndex];
                    if (correctionCount <= 0)
                        continue;

                    float invCount = math.rcp((float)correctionCount);
                    Positions[nodeIndex] = SanitizeFinite(
                        Positions[nodeIndex] + ConstraintCorrections[nodeIndex] * invCount,
                        rootPosition);
                }
            }

            private void WriteRadiusAndMatrices(
                int tentacleIndex,
                float3 rootPosition,
                bool grabbing,
                float safeBaseRadius,
                float safeTipRadius,
                float safePulseStrength,
                float safeTime,
                float invSegmentLast)
            {
                for (int segmentIndex = 0; segmentIndex < SegmentsPerTentacle; segmentIndex++)
                {
                    int nodeIndex = FlatIndex(tentacleIndex, segmentIndex);
                    float t = segmentIndex * invSegmentLast;
                    float baseTaper = math.lerp(safeBaseRadius, safeTipRadius, t);
                    float pulseMask = grabbing ? math.saturate(1f - math.abs((t * 2f) - 1f)) : 0f;
                    float pulse = CheapTriangleWave(safeTime * 2.25f + tentacleIndex * 0.13f + t * 5.1f);
                    float solvedRadius = math.max(0.001f, baseTaper * (1f + pulseMask * pulse * safePulseStrength));
                    Radius[nodeIndex] = solvedRadius;

                    float3 current = SanitizeFinite(Positions[nodeIndex], rootPosition);
                    float3 previous = segmentIndex == 0
                        ? current
                        : SanitizeFinite(Positions[nodeIndex - 1], rootPosition);
                    float3 next = segmentIndex == SegmentLastIndex
                        ? current
                        : SanitizeFinite(Positions[nodeIndex + 1], current);
                    bool tipCap = segmentIndex == SegmentLastIndex;
                    float3 axis = tipCap ? current - previous : next - current;
                    float axisSq = math.lengthsq(axis);
                    float invAxis = math.rsqrt(math.max(axisSq, MinDistanceSq));
                    float axisLength = axisSq * invAxis;
                    float3 direction = axis * invAxis;
                    quaternion rotation = quaternion.LookRotationSafe(direction, new float3(0f, 1f, 0f));
                    float visualLength = tipCap ? solvedRadius * 2f : math.max(0.001f, axisLength);
                    float3 center = tipCap ? current : current + axis * 0.5f;
                    SegmentMatrices[nodeIndex] = float4x4.TRS(
                        SanitizeFinite(center, rootPosition),
                        rotation,
                        new float3(solvedRadius, solvedRadius, visualLength));
                }
            }

            private static float3 ResolveClampedTarget(float3 root, float3 target, bool grabbing, float maxLength, out float stretchFraction)
            {
                stretchFraction = 0f;
                if (!grabbing)
                    return target;

                float3 delta = target - root;
                float distanceSq = math.lengthsq(delta);
                float invDistance = math.rsqrt(math.max(distanceSq, MinDistanceSq));
                float distance = distanceSq * invDistance;
                float safeMaxLength = math.max(0.001f, maxLength);
                stretchFraction = distance * math.rcp(safeMaxLength);
                if (distance <= safeMaxLength)
                    return target;

                return root + delta * invDistance * safeMaxLength;
            }

            private static int FlatIndex(int tentacleIndex, int segmentIndex)
            {
                return tentacleIndex * SegmentsPerTentacle + segmentIndex;
            }

            private static float CheapTriangleWave(float phase)
            {
                float wrapped = phase - math.floor(phase);
                return 1f - math.abs(wrapped * 2f - 1f);
            }

            private static float3 SanitizeFinite(float3 value, float3 fallback)
            {
                return math.all(math.isfinite(value)) ? value : fallback;
            }
        }

        [Header("Tentacle Binding")]
        [Tooltip("Active tentacles. Clamped to the fixed 8 tentacle S.O.A. budget.")]
        [SerializeField, Range(1, MaxTentacles)] private int activeTentacleCount = MaxTentacles;

        [Tooltip("Root socket transforms for each tentacle. Missing sockets use deterministic owner-local fallback anchors.")]
        [SerializeField] private Transform[] rootSockets = Array.Empty<Transform>();

        [Tooltip("Optional default target used while grabbing if no runtime target was assigned.")]
        [SerializeField] private Transform defaultGrabTarget;

        [Header("Verlet Settings")]
        [Tooltip("Meters between tentacle solver nodes.")]
        [SerializeField, Min(0.01f)] private float restLength = 1.15f;

        [Tooltip("Maximum root-to-tip reach before the target point is clamped.")]
        [SerializeField, Min(0.01f)] private float maxStretchLength = 23f;

        [Tooltip("Velocity retention applied to Verlet displacement.")]
        [SerializeField, Range(0f, 1f)] private float damping = 0.985f;

        [Tooltip("Presentation gravity/current bias applied to nodes.")]
        [SerializeField] private Vector3 gravity = new Vector3(0f, -0.18f, 0f);

        [Tooltip("High-tier constraint iteration count. Low/MX350 clamps to one iteration.")]
        [SerializeField, Range(1, 3)] private int highTierConstraintIterations = 3;

        [Header("Flow and Visual Shape")]
        [Tooltip("Root node render radius.")]
        [SerializeField, Min(0.001f)] private float baseRadius = 0.22f;

        [Tooltip("Tip node render radius.")]
        [SerializeField, Min(0.001f)] private float tipRadius = 0.055f;

        [Tooltip("Multiplier applied to the sampled abyssal flow vector.")]
        [SerializeField, Min(0f)] private float flowStrength = 1f;

        [Tooltip("Cheap middle-segment triangle-wave turbulence amplitude.")]
        [SerializeField, Min(0f)] private float flowNoiseStrength = 0.28f;

        [Tooltip("Triangle-wave suction cup pulse radius while grabbing.")]
        [SerializeField, Range(0f, 0.5f)] private float suctionPulseStrength = 0.16f;

        [Header("Rendering")]
        [Tooltip("Segment mesh rendered once per solver segment via RenderMeshIndirect.")]
        [SerializeField] private Mesh tentacleSegmentMesh;

        [Tooltip("Material using Hecton8/Fauna/LeviathanTentacleIndirect and reading matrix/radius buffers.")]
        [SerializeField] private Material tentacleMaterial;

        [Tooltip("Enables indirect rendering. Disable only for authoring diagnostics.")]
        [SerializeField] private bool renderIndirect = true;

        [Tooltip("Local-space conservative render bounds around the leviathan.")]
        [SerializeField] private Bounds renderBounds = new Bounds(Vector3.zero, new Vector3(96f, 96f, 96f));

        [Tooltip("Shadow mode for tentacle indirect draw.")]
        [SerializeField] private ShadowCastingMode shadowCastingMode = ShadowCastingMode.On;

        [Tooltip("Whether indirect tentacles receive shadows.")]
        [SerializeField] private bool receiveShadows = true;

        [Header("Grab Damage")]
        [Tooltip("Combat damage amount queued once per second while a target is grabbed.")]
        [SerializeField, Min(0f)] private float grabDamageAmount = 12f;

        [Tooltip("Impulse magnitude passed to CombatDamageRuntime while grabbing.")]
        [SerializeField, Min(0f)] private float grabDamageImpulse = 35f;

        private Transform _cachedTransform;
        private Transform _grabTarget;
        private int _grabTargetDamageId;
        private bool _registeredUpdate;
        private bool _registeredLateFrame;
        private bool _registeredOriginShiftListener;
        private bool _solverScheduled;
        private bool _pendingOriginShiftRebase;
        private bool _telemetryDumped;
        private bool _disposed;
        private bool _invalidInputDetected;
        private int _matrixUploadBufferIndex;
        private int _argsUploadInstanceCount = -1;
        private int _resolvedConstraintIterations;
        private int _pendingConstraintIterations;
        private int _telemetryCursor;
        private int _frameIndex;
        private float _grabDamageTimer;
        private float _solverTimeSeconds;
        private float _constraintIterationSwitchTimer;
        private float3 _pendingOriginShiftOffset;
        private float3 _lastFlowVector;
        private Vector4 _lastFlowGridResolution;
        private Vector4 _lastFlowCenter;
        private Vector4 _lastFlowSpacing;
        private GraphicsBuffer _gpuAbyssalFlowFieldBuffer;
        private GraphicsBuffer _matrixGraphicsBufferA;
        private GraphicsBuffer _matrixGraphicsBufferB;
        private GraphicsBuffer _radiusGraphicsBufferA;
        private GraphicsBuffer _radiusGraphicsBufferB;
        private GraphicsBuffer _indirectArgsBuffer;
        private Mesh _argsUploadMesh;
        private JobHandle _pendingSolverHandle;
        private JobHandle _disposeHandle;

        // COLD ALLOC: Vector3[8] - deterministic missing-socket local anchors - owner: LeviathanTentacleVerletSolver
        private readonly Vector3[] _fallbackRootOffsets = new Vector3[MaxTentacles];

        private NativeArray<float3> _positions;
        private NativeArray<float3> _previousPositions;
        private NativeArray<float> _radius;
        private NativeArray<float4x4> _segmentMatrices;
        private NativeArray<float> _stretchFractions;
        private NativeArray<float3> _constraintCorrections;
        private NativeArray<int> _constraintCorrectionCounts;
        private NativeArray<float3> _rootPositions;
        private NativeArray<float3> _targetPositions;
        private NativeArray<AbsoluteUniversePosition> _rootAups;
        private NativeArray<AbsoluteUniversePosition> _targetAups;
        private NativeArray<uint> _tentacleStates;
        private NativeArray<LeviathanTentacleTelemetryEntry> _telemetryRing;

        private void Awake()
        {
            _cachedTransform = transform;
            BuildFallbackRootOffsets();
            EnsurePersistentBuffers();
            SeedAllTentaclesFromSockets();
        }

        private void OnEnable()
        {
            if (_disposed || !Application.isPlaying)
                return;

            CompletePendingJob(force: true);
            EnsurePersistentBuffers();
            SeedAllTentaclesFromSockets();
            ResetConstraintIterationHysteresis();
            TryRegister();
            TryRegisterOriginShiftListener();
        }

        private void OnDisable()
        {
            if (_disposed || !Application.isPlaying)
                return;

            TryUnregisterOriginShiftListener();
            TryUnregister();
            DispatcherJobSwap.TryFinalizeCompleted(ref _pendingSolverHandle);
            _solverScheduled = !_pendingSolverHandle.IsCompleted;
        }

        private void OnDestroy()
        {
            Dispose();
        }

        /// <summary>
        /// Releases owned native and graphics resources. Safe to call repeatedly from teardown.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            TryUnregisterOriginShiftListener();
            TryUnregister();
            JobHandle dependency = _solverScheduled ? _pendingSolverHandle : default;
            DisposePersistentBuffers(dependency);
            ReleaseGraphicsBuffers();
        }

        /// <summary>
        /// Rebases all owned solver nodes and cached AUP/runtime positions after a floating-origin shift.
        /// </summary>
        /// <param name="shiftData">Committed origin-shift payload from the floating-origin system.</param>
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (_disposed)
                return;

            Vector3 shiftOffset = shiftData.ShiftOffset;
            if (shiftOffset.sqrMagnitude <= 0.000001f || !_positions.IsCreated)
                return;

            float3 offset = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z);
            if (!math.all(math.isfinite(offset)))
            {
                DumpTelemetryBlackBoxOnce();
                return;
            }

            if (_solverScheduled)
            {
                if (!DispatcherJobSwap.TryFinalizeCompleted(ref _pendingSolverHandle))
                {
                    QueueOriginShiftRebase(offset);
                    return;
                }

                _solverScheduled = false;
            }

            ApplyOriginShiftRebase(offset);
        }

        /// <summary>
        /// Assigns a shared grab target for active tentacles.
        /// </summary>
        /// <param name="target">Target transform to pin tips to, or null to clear.</param>
        /// <param name="damageTargetId">Registered combat damage target id for hull damage emission.</param>
        public void SetGrabTarget(Transform target, int damageTargetId)
        {
            if (_disposed)
                return;

            if (_grabTarget != target || _grabTargetDamageId != damageTargetId)
                _grabDamageTimer = 0f;

            _grabTarget = target;
            _grabTargetDamageId = damageTargetId;
        }

        /// <summary>
        /// Clears the shared grab target and returns tentacles to free Verlet drift.
        /// </summary>
        public void ClearGrabTarget()
        {
            if (_disposed)
                return;

            _grabTarget = null;
            _grabTargetDamageId = 0;
            _grabDamageTimer = 0f;
        }

        /// <summary>
        /// Captures root/target/flow inputs and schedules the Burst Verlet solve for this dispatcher tick.
        /// </summary>
        /// <param name="deltaTime">Scaled dispatcher delta time in seconds.</param>
        public void Tick(float deltaTime)
        {
            if (_disposed || !_positions.IsCreated || _solverScheduled || deltaTime <= 0f)
                return;

            ApplyPendingOriginShiftRebase();
            CaptureTentacleInputs();
            ResolveFlowInput();
            TryQueueGrabDamage(deltaTime);
            float safeDeltaTime = math.isfinite(deltaTime) ? math.min(math.max(0f, deltaTime), 0.05f) : 0f;
            int constraintIterations = ResolveConstraintIterationsWithHysteresis(safeDeltaTime);
            _solverTimeSeconds += safeDeltaTime;
            if (_solverTimeSeconds > 4096f)
                _solverTimeSeconds -= 4096f;

            VerletSolveJob job = new VerletSolveJob
            {
                Positions = _positions,
                PreviousPositions = _previousPositions,
                Radius = _radius,
                SegmentMatrices = _segmentMatrices,
                StretchFractions = _stretchFractions,
                ConstraintCorrections = _constraintCorrections,
                ConstraintCorrectionCounts = _constraintCorrectionCounts,
                RootPositions = _rootPositions,
                TargetPositions = _targetPositions,
                TentacleStates = _tentacleStates,
                DeltaTime = safeDeltaTime,
                Damping = damping,
                RestLength = restLength,
                MaxStretchLength = maxStretchLength,
                BaseRadius = baseRadius,
                TipRadius = tipRadius,
                FlowStrength = flowStrength,
                FlowNoiseStrength = flowNoiseStrength,
                SuctionPulseStrength = suctionPulseStrength,
                TimeSeconds = _solverTimeSeconds,
                Gravity = new float3(gravity.x, gravity.y, gravity.z),
                FlowVector = _lastFlowVector,
                TentacleCount = math.clamp(activeTentacleCount, 0, MaxTentacles),
                ConstraintIterations = constraintIterations
            };

            _pendingSolverHandle = job.Schedule();
            _solverScheduled = true;
        }

        /// <summary>
        /// Completes the scheduled solver job in the dispatcher end-of-frame window and submits indirect rendering.
        /// </summary>
        public void LateFrameTick()
        {
            if (_disposed)
                return;

            if (!_solverScheduled)
            {
                ApplyPendingOriginShiftRebase();
                return;
            }

            if (!DispatcherJobSwap.TryComplete(ref _pendingSolverHandle, forceComplete: false))
                return;

            _solverScheduled = false;
            if (ApplyPendingOriginShiftRebase())
                return;

            WriteTelemetryFrame();
            UploadAndRenderIndirect();
        }

        private void TryRegister()
        {
            if (_registeredUpdate && _registeredLateFrame)
                return;

            if (_registeredUpdate || _registeredLateFrame)
            {
                if (_registeredUpdate)
                    GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                if (_registeredLateFrame)
                    GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);

                _registeredUpdate = false;
                _registeredLateFrame = false;
            }

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            if (_registeredUpdate && _registeredLateFrame)
                return;

            if (_registeredUpdate)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            if (_registeredLateFrame)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);

            _registeredUpdate = false;
            _registeredLateFrame = false;
        }

        private void TryUnregister()
        {
            if (_registeredUpdate)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            if (_registeredLateFrame)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);

            _registeredUpdate = false;
            _registeredLateFrame = false;
        }

        private void TryRegisterOriginShiftListener()
        {
            if (_registeredOriginShiftListener)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        private void TryUnregisterOriginShiftListener()
        {
            if (!_registeredOriginShiftListener)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _registeredOriginShiftListener = false;
        }

        private bool CompletePendingJob(bool force)
        {
            if (!_solverScheduled)
                return true;

            if (!DispatcherJobSwap.TryComplete(ref _pendingSolverHandle, force))
                return false;

            _solverScheduled = false;
            return true;
        }

        private void EnsurePersistentBuffers()
        {
            if (_disposed)
                return;

            if (!_positions.IsCreated)
            {
                _positions = new NativeArray<float3>(TotalSegments, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float3>[160] - tentacle Verlet positions - owner: LeviathanTentacleVerletSolver
                NativeMemorySentinel.RegisterNativeArray(_positions, NativeMemoryOwner, nameof(_positions), NativeMemoryLifetime);
            }

            if (!_previousPositions.IsCreated)
            {
                _previousPositions = new NativeArray<float3>(TotalSegments, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float3>[160] - tentacle Verlet previous positions - owner: LeviathanTentacleVerletSolver
                NativeMemorySentinel.RegisterNativeArray(_previousPositions, NativeMemoryOwner, nameof(_previousPositions), NativeMemoryLifetime);
            }

            if (!_radius.IsCreated)
            {
                _radius = new NativeArray<float>(TotalSegments, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[160] - tentacle per-node radius lane - owner: LeviathanTentacleVerletSolver
                NativeMemorySentinel.RegisterNativeArray(_radius, NativeMemoryOwner, nameof(_radius), NativeMemoryLifetime);
            }

            if (!_segmentMatrices.IsCreated)
            {
                _segmentMatrices = new NativeArray<float4x4>(TotalSegments, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float4x4>[160] - tentacle GPU matrix upload lane - owner: LeviathanTentacleVerletSolver
                NativeMemorySentinel.RegisterNativeArray(_segmentMatrices, NativeMemoryOwner, nameof(_segmentMatrices), NativeMemoryLifetime);
            }

            if (!_stretchFractions.IsCreated)
            {
                _stretchFractions = new NativeArray<float>(MaxTentacles, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[8] - max stretch clamp telemetry - owner: LeviathanTentacleVerletSolver
                NativeMemorySentinel.RegisterNativeArray(_stretchFractions, NativeMemoryOwner, nameof(_stretchFractions), NativeMemoryLifetime);
            }

            if (!_constraintCorrections.IsCreated)
            {
                _constraintCorrections = new NativeArray<float3>(TotalSegments, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float3>[160] - Jacobi constraint correction lane - owner: LeviathanTentacleVerletSolver
                NativeMemorySentinel.RegisterNativeArray(_constraintCorrections, NativeMemoryOwner, nameof(_constraintCorrections), NativeMemoryLifetime);
            }

            if (!_constraintCorrectionCounts.IsCreated)
            {
                _constraintCorrectionCounts = new NativeArray<int>(TotalSegments, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[160] - Jacobi constraint correction counts - owner: LeviathanTentacleVerletSolver
                NativeMemorySentinel.RegisterNativeArray(_constraintCorrectionCounts, NativeMemoryOwner, nameof(_constraintCorrectionCounts), NativeMemoryLifetime);
            }

            if (!_rootPositions.IsCreated)
            {
                _rootPositions = new NativeArray<float3>(MaxTentacles, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float3>[8] - root socket runtime positions - owner: LeviathanTentacleVerletSolver
                NativeMemorySentinel.RegisterNativeArray(_rootPositions, NativeMemoryOwner, nameof(_rootPositions), NativeMemoryLifetime);
            }

            if (!_targetPositions.IsCreated)
            {
                _targetPositions = new NativeArray<float3>(MaxTentacles, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float3>[8] - tentacle target runtime positions - owner: LeviathanTentacleVerletSolver
                NativeMemorySentinel.RegisterNativeArray(_targetPositions, NativeMemoryOwner, nameof(_targetPositions), NativeMemoryLifetime);
            }

            if (!_rootAups.IsCreated)
            {
                _rootAups = new NativeArray<AbsoluteUniversePosition>(MaxTentacles, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<AbsoluteUniversePosition>[8] - root socket AUP cache - owner: LeviathanTentacleVerletSolver
                NativeMemorySentinel.RegisterNativeArray(_rootAups, NativeMemoryOwner, nameof(_rootAups), NativeMemoryLifetime);
            }

            if (!_targetAups.IsCreated)
            {
                _targetAups = new NativeArray<AbsoluteUniversePosition>(MaxTentacles, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<AbsoluteUniversePosition>[8] - target AUP cache - owner: LeviathanTentacleVerletSolver
                NativeMemorySentinel.RegisterNativeArray(_targetAups, NativeMemoryOwner, nameof(_targetAups), NativeMemoryLifetime);
            }

            if (!_tentacleStates.IsCreated)
            {
                _tentacleStates = new NativeArray<uint>(MaxTentacles, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<uint>[8] - tentacle active/grab state bits - owner: LeviathanTentacleVerletSolver
                NativeMemorySentinel.RegisterNativeArray(_tentacleStates, NativeMemoryOwner, nameof(_tentacleStates), NativeMemoryLifetime);
            }

            if (!_telemetryRing.IsCreated)
            {
                _telemetryRing = new NativeArray<LeviathanTentacleTelemetryEntry>(TelemetryCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<LeviathanTentacleTelemetryEntry>[300] - tentacle black box ring - owner: LeviathanTentacleVerletSolver
                NativeMemorySentinel.RegisterNativeArray(_telemetryRing, NativeMemoryOwner, nameof(_telemetryRing), NativeMemoryLifetime);
            }
        }

        private void DisposePersistentBuffers(JobHandle dependency)
        {
            DisposeNativeArray(ref _positions, dependency);
            DisposeNativeArray(ref _previousPositions, dependency);
            DisposeNativeArray(ref _radius, dependency);
            DisposeNativeArray(ref _segmentMatrices, dependency);
            DisposeNativeArray(ref _stretchFractions, dependency);
            DisposeNativeArray(ref _constraintCorrections, dependency);
            DisposeNativeArray(ref _constraintCorrectionCounts, dependency);
            DisposeNativeArray(ref _rootPositions, dependency);
            DisposeNativeArray(ref _targetPositions, dependency);
            DisposeNativeArray(ref _rootAups, dependency);
            DisposeNativeArray(ref _targetAups, dependency);
            DisposeNativeArray(ref _tentacleStates, dependency);
            DisposeNativeArray(ref _telemetryRing, dependency);
            DispatcherJobSwap.TryFinalizeCompleted(ref _disposeHandle);
            _pendingSolverHandle = default;
            _solverScheduled = false;
        }

        private void DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            _disposeHandle = JobHandle.CombineDependencies(_disposeHandle, array.Dispose(dependency));
            array = default;
        }

        private void BuildFallbackRootOffsets()
        {
            for (int i = 0; i < MaxTentacles; i++)
            {
                float angle = (math.PI * 2f) * (i * math.rcp(MaxTentacles));
                _fallbackRootOffsets[i] = new Vector3(math.cos(angle) * 0.85f, 0f, math.sin(angle) * 0.85f);
            }
        }

        private void SeedAllTentaclesFromSockets()
        {
            if (!_positions.IsCreated || _cachedTransform == null)
                return;

            float3 ownerFallback = ResolveOwnerRuntimePosition();
            float3 back = SanitizeFiniteInputFloat3(-(float3)_cachedTransform.forward, new float3(0f, 0f, -1f));
            float safeRestLength = math.max(0.01f, restLength);
            float safeBaseRadius = math.max(0.001f, baseRadius);
            float safeTipRadius = math.max(0.001f, tipRadius);
            int safeTentacleCount = math.clamp(activeTentacleCount, 0, MaxTentacles);
            for (int tentacleIndex = 0; tentacleIndex < MaxTentacles; tentacleIndex++)
            {
                float3 root = SanitizeFiniteFloat3(ResolveRootRuntimePosition(tentacleIndex), ownerFallback);
                uint state = tentacleIndex < safeTentacleCount ? TentacleStateActive : 0u;
                _rootPositions[tentacleIndex] = root;
                _targetPositions[tentacleIndex] = root;
                _rootAups[tentacleIndex] = ToAbsoluteUniversePosition(root);
                _targetAups[tentacleIndex] = _rootAups[tentacleIndex];
                _tentacleStates[tentacleIndex] = state;
                _stretchFractions[tentacleIndex] = 0f;

                int baseIndex = FlatIndex(tentacleIndex, 0);
                for (int segmentIndex = 0; segmentIndex < SegmentsPerTentacle; segmentIndex++)
                {
                    int nodeIndex = baseIndex + segmentIndex;
                    float3 position = SanitizeFiniteFloat3(root + (back * safeRestLength * segmentIndex), root);
                    _positions[nodeIndex] = position;
                    _previousPositions[nodeIndex] = position;
                    float t = segmentIndex * math.rcp(SegmentLastIndex);
                    float solvedRadius = math.max(0.001f, math.lerp(safeBaseRadius, safeTipRadius, t));
                    _radius[nodeIndex] = solvedRadius;
                    _segmentMatrices[nodeIndex] = float4x4.TRS(position, quaternion.identity, new float3(solvedRadius, solvedRadius, safeRestLength));
                }
            }
        }

        private void CaptureTentacleInputs()
        {
            int safeTentacleCount = math.clamp(activeTentacleCount, 0, MaxTentacles);
            Transform target = _grabTarget != null ? _grabTarget : defaultGrabTarget;
            bool grabbing = target != null;
            float3 ownerFallback = ResolveOwnerRuntimePosition();
            float3 targetPosition = grabbing
                ? SanitizeFiniteInputFloat3((float3)target.position, ownerFallback)
                : ownerFallback;

            for (int tentacleIndex = 0; tentacleIndex < MaxTentacles; tentacleIndex++)
            {
                float3 root = SanitizeFiniteFloat3(ResolveRootRuntimePosition(tentacleIndex), ownerFallback);
                float3 resolvedTarget = SanitizeFiniteFloat3(grabbing ? targetPosition : root, root);
                _rootPositions[tentacleIndex] = root;
                _targetPositions[tentacleIndex] = resolvedTarget;
                _rootAups[tentacleIndex] = ToAbsoluteUniversePosition(root);
                _targetAups[tentacleIndex] = ToAbsoluteUniversePosition(resolvedTarget);

                uint state = tentacleIndex < safeTentacleCount ? TentacleStateActive : 0u;
                if (grabbing)
                    state |= TentacleStateGrabbing;
                _tentacleStates[tentacleIndex] = state;
            }
        }

        private void ResolveFlowInput()
        {
            _lastFlowVector = float3.zero;
            _gpuAbyssalFlowFieldBuffer = null;
            _lastFlowGridResolution = Vector4.zero;
            _lastFlowCenter = Vector4.zero;
            _lastFlowSpacing = Vector4.zero;

            var fluid = GlobalRegistry.Fluid;
            if (fluid == null)
                return;

            float3 ownerPositionFloat3 = ResolveOwnerRuntimePosition();
            Vector3 ownerPosition = new Vector3(ownerPositionFloat3.x, ownerPositionFloat3.y, ownerPositionFloat3.z);
            if (fluid.TrySampleModAbyssalFlow(ownerPosition, out float3 flowVector))
                _lastFlowVector = SanitizeFiniteInputFloat3(flowVector, float3.zero);

            if (fluid.TryGetGpuAbyssalFlowFieldBuffer(
                    out GraphicsBuffer flowFieldBuffer,
                    out Vector4 gridResolution,
                    out Vector4 flowCenter,
                    out Vector4 flowSpacing))
            {
                if (TryPrepareAbyssalFlowPayload(
                        flowFieldBuffer,
                        gridResolution,
                        flowCenter,
                        flowSpacing,
                        out Vector4 safeGridResolution,
                        out Vector4 safeFlowCenter,
                        out Vector4 safeFlowSpacing))
                {
                    _gpuAbyssalFlowFieldBuffer = flowFieldBuffer;
                    _lastFlowGridResolution = safeGridResolution;
                    _lastFlowCenter = safeFlowCenter;
                    _lastFlowSpacing = safeFlowSpacing;
                }
            }
        }

        private bool TryQueueGrabDamage(float deltaTime)
        {
            Transform target = _grabTarget != null ? _grabTarget : defaultGrabTarget;
            if (target == null || grabDamageAmount <= 0f)
            {
                _grabDamageTimer = 0f;
                return false;
            }

            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;
            _grabDamageTimer += safeDeltaTime;
            if (_grabDamageTimer < 1f)
                return false;

            _grabDamageTimer = math.max(0f, _grabDamageTimer - 1f);
            if (_grabDamageTimer > 1f)
                _grabDamageTimer = 0f;

            int targetId = _grabTargetDamageId != 0
                ? _grabTargetDamageId
                : CombatDamageRuntime.ResolveTargetId(target.gameObject);
            if (targetId == 0 || !CombatDamageRuntime.IsTargetRegistered(targetId))
                return false;

            float3 root = _rootPositions.IsCreated ? SanitizeFiniteInputFloat3(_rootPositions[0], float3.zero) : float3.zero;
            float3 tip = _targetPositions.IsCreated ? SanitizeFiniteInputFloat3(_targetPositions[0], root) : root;
            float3 directionDelta = tip - root;
            float directionSq = math.lengthsq(directionDelta);
            float3 direction = directionSq > 0.000001f
                ? directionDelta * math.rsqrt(directionSq)
                : new float3(0f, 0f, 1f);
            Vector3 localPointVector = target.InverseTransformPoint(new Vector3(tip.x, tip.y, tip.z));
            float3 localPoint = SanitizeFiniteInputFloat3(new float3(localPointVector.x, localPointVector.y, localPointVector.z), float3.zero);

            CombatDamageRequest signal = new CombatDamageRequest
            {
                TargetId = targetId,
                SourceId = DamageSourceIds.FaunaLeviathanBite,
                Amount = grabDamageAmount,
                ImpulseMagnitude = grabDamageImpulse,
                Direction = direction,
                PackedMeta = CombatDamageRuntime.PackSignalMeta(
                    CombatDamageTypes.Impact,
                    CombatStatusBits.Crushed,
                    CombatWeakspotTier.None)
            };

            CombatDamageSignalDetail detail = new CombatDamageSignalDetail
            {
                LocalPoint = localPoint,
                ArmorNormal = -direction,
                LocalTemperatureCelsius = 20f,
                StatusDurationSeconds = 1f
            };

            return CombatDamageRuntime.TryQueueDamage(in signal, in detail);
        }

        private void UploadAndRenderIndirect()
        {
            int instanceCount = math.clamp(activeTentacleCount, 0, MaxTentacles) * SegmentsPerTentacle;
            if (!renderIndirect || instanceCount <= 0 || tentacleSegmentMesh == null || tentacleMaterial == null)
                return;

            if (!_segmentMatrices.IsCreated || !_radius.IsCreated)
                return;

            EnsureGraphicsBuffers();
            GraphicsBuffer matrixBuffer = _matrixUploadBufferIndex == 0 ? _matrixGraphicsBufferA : _matrixGraphicsBufferB;
            GraphicsBuffer radiusBuffer = _matrixUploadBufferIndex == 0 ? _radiusGraphicsBufferA : _radiusGraphicsBufferB;
            if (!HasValidGraphicsBuffer(matrixBuffer, instanceCount) ||
                !HasValidGraphicsBuffer(radiusBuffer, instanceCount) ||
                !HasValidGraphicsBuffer(_indirectArgsBuffer, 1))
            {
                return;
            }

            GraphicsBufferUploadUtility.UploadNativeArray(matrixBuffer, _segmentMatrices, instanceCount);
            GraphicsBufferUploadUtility.UploadNativeArray(radiusBuffer, _radius, instanceCount);
            tentacleMaterial.SetBuffer(_MatrixBufferId, matrixBuffer);
            tentacleMaterial.SetBuffer(_RadiusBufferId, radiusBuffer);
            BindRadiusReferenceToMaterial();
            BindFlowBufferToMaterial();
            UploadIndirectArgs(instanceCount);

            Bounds worldBounds = renderBounds;
            if (_cachedTransform != null)
            {
                float3 ownerPosition = ResolveOwnerRuntimePosition();
                worldBounds.center += new Vector3(ownerPosition.x, ownerPosition.y, ownerPosition.z);
            }

            RenderParams renderParams = new RenderParams(tentacleMaterial)
            {
                worldBounds = worldBounds,
                layer = gameObject.layer,
                shadowCastingMode = shadowCastingMode,
                receiveShadows = receiveShadows,
                motionVectorMode = MotionVectorGenerationMode.Camera
            };
            Graphics.RenderMeshIndirect(renderParams, tentacleSegmentMesh, _indirectArgsBuffer, 1, 0);
            _matrixUploadBufferIndex ^= 1;
        }

        private void BindRadiusReferenceToMaterial()
        {
            float safeBaseRadius = math.max(0.001f, baseRadius);
            float safeTipRadius = math.max(0.001f, tipRadius);
            tentacleMaterial.SetFloat(_BaseRadiusReferenceId, safeBaseRadius);
            tentacleMaterial.SetFloat(_TipRadiusReferenceId, safeTipRadius);
        }

        private void BindFlowBufferToMaterial()
        {
            bool hasFlowBuffer = TryPrepareAbyssalFlowPayload(
                _gpuAbyssalFlowFieldBuffer,
                _lastFlowGridResolution,
                _lastFlowCenter,
                _lastFlowSpacing,
                out Vector4 safeGridResolution,
                out Vector4 safeFlowCenter,
                out Vector4 safeFlowSpacing);
            tentacleMaterial.SetFloat(_AbyssalFlowActiveId, hasFlowBuffer ? 1f : 0f);
            if (!hasFlowBuffer)
                return;

            tentacleMaterial.SetBuffer(_AbyssalFlowFieldId, _gpuAbyssalFlowFieldBuffer);
            tentacleMaterial.SetVector(_AbyssalFlowResolutionId, safeGridResolution);
            tentacleMaterial.SetVector(_AbyssalFlowCenterId, safeFlowCenter);
            tentacleMaterial.SetVector(_AbyssalFlowSpacingId, safeFlowSpacing);
        }

        private void EnsureGraphicsBuffers()
        {
            if (!HasValidGraphicsBuffer(_matrixGraphicsBufferA, TotalSegments))
            {
                ReleaseGraphicsBuffer(ref _matrixGraphicsBufferA);
                _matrixGraphicsBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4x4>(TotalSegments); // COLD ALLOC: GraphicsBuffer[160 float4x4] - indirect tentacle matrix upload A - owner: LeviathanTentacleVerletSolver
            }

            if (!HasValidGraphicsBuffer(_matrixGraphicsBufferB, TotalSegments))
            {
                ReleaseGraphicsBuffer(ref _matrixGraphicsBufferB);
                _matrixGraphicsBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4x4>(TotalSegments); // COLD ALLOC: GraphicsBuffer[160 float4x4] - indirect tentacle matrix upload B - owner: LeviathanTentacleVerletSolver
            }

            if (!HasValidGraphicsBuffer(_radiusGraphicsBufferA, TotalSegments))
            {
                ReleaseGraphicsBuffer(ref _radiusGraphicsBufferA);
                _radiusGraphicsBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float>(TotalSegments); // COLD ALLOC: GraphicsBuffer[160 float] - indirect tentacle radius upload A - owner: LeviathanTentacleVerletSolver
            }

            if (!HasValidGraphicsBuffer(_radiusGraphicsBufferB, TotalSegments))
            {
                ReleaseGraphicsBuffer(ref _radiusGraphicsBufferB);
                _radiusGraphicsBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float>(TotalSegments); // COLD ALLOC: GraphicsBuffer[160 float] - indirect tentacle radius upload B - owner: LeviathanTentacleVerletSolver
            }

            if (!HasValidGraphicsBuffer(_indirectArgsBuffer, 1))
            {
                ReleaseGraphicsBuffer(ref _indirectArgsBuffer);
                _indirectArgsBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - indirect tentacle draw args - owner: LeviathanTentacleVerletSolver
            }
        }

        private void UploadIndirectArgs(int instanceCount)
        {
            if (_indirectArgsBuffer == null || tentacleSegmentMesh == null || instanceCount <= 0)
                return;

            if (_argsUploadMesh == tentacleSegmentMesh && _argsUploadInstanceCount == instanceCount)
                return;

            NativeArray<GraphicsBuffer.IndirectDrawIndexedArgs> argsWrite =
                _indirectArgsBuffer.LockBufferForWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(0, 1);
            argsWrite[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = tentacleSegmentMesh.GetIndexCount(0),
                instanceCount = (uint)instanceCount,
                startIndex = tentacleSegmentMesh.GetIndexStart(0),
                baseVertexIndex = (uint)Mathf.Max(0, tentacleSegmentMesh.GetBaseVertex(0)),
                startInstance = 0u
            };
            _indirectArgsBuffer.UnlockBufferAfterWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(1);
            _argsUploadMesh = tentacleSegmentMesh;
            _argsUploadInstanceCount = instanceCount;
        }

        private void ReleaseGraphicsBuffers()
        {
            ReleaseGraphicsBuffer(ref _matrixGraphicsBufferA);
            ReleaseGraphicsBuffer(ref _matrixGraphicsBufferB);
            ReleaseGraphicsBuffer(ref _radiusGraphicsBufferA);
            ReleaseGraphicsBuffer(ref _radiusGraphicsBufferB);
            ReleaseGraphicsBuffer(ref _indirectArgsBuffer);
            _gpuAbyssalFlowFieldBuffer = null;
            _argsUploadMesh = null;
            _argsUploadInstanceCount = -1;
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            if (buffer.IsValid())
                buffer.Release();
            buffer = null;
        }

        private void QueueOriginShiftRebase(float3 offset)
        {
            _pendingOriginShiftOffset += offset;
            _pendingOriginShiftRebase = true;
        }

        private bool ApplyPendingOriginShiftRebase()
        {
            if (!_pendingOriginShiftRebase)
                return false;

            float3 offset = _pendingOriginShiftOffset;
            _pendingOriginShiftOffset = float3.zero;
            _pendingOriginShiftRebase = false;
            ApplyOriginShiftRebase(offset);
            return true;
        }

        private void ApplyOriginShiftRebase(float3 offset)
        {
            if (!_positions.IsCreated || !_previousPositions.IsCreated || !_rootPositions.IsCreated || !_targetPositions.IsCreated)
                return;

            if (!math.all(math.isfinite(offset)))
            {
                DumpTelemetryBlackBoxOnce();
                return;
            }

            for (int i = 0; i < TotalSegments; i++)
            {
                _positions[i] = SanitizeFiniteInputFloat3(_positions[i] - offset, float3.zero);
                _previousPositions[i] = SanitizeFiniteInputFloat3(_previousPositions[i] - offset, _positions[i]);
                if (!_segmentMatrices.IsCreated)
                    continue;

                float4x4 matrix = _segmentMatrices[i];
                float4 c3 = matrix.c3;
                float3 matrixPosition = SanitizeFiniteInputFloat3(new float3(c3.x - offset.x, c3.y - offset.y, c3.z - offset.z), _positions[i]);
                matrix.c3 = new float4(matrixPosition.x, matrixPosition.y, matrixPosition.z, c3.w);
                _segmentMatrices[i] = matrix;
            }

            for (int tentacleIndex = 0; tentacleIndex < MaxTentacles; tentacleIndex++)
            {
                _rootPositions[tentacleIndex] = SanitizeFiniteInputFloat3(_rootPositions[tentacleIndex] - offset, float3.zero);
                _targetPositions[tentacleIndex] = SanitizeFiniteInputFloat3(_targetPositions[tentacleIndex] - offset, _rootPositions[tentacleIndex]);
                if (_rootAups.IsCreated)
                    _rootAups[tentacleIndex] = ToAbsoluteUniversePosition(_rootPositions[tentacleIndex]);
                if (_targetAups.IsCreated)
                    _targetAups[tentacleIndex] = ToAbsoluteUniversePosition(_targetPositions[tentacleIndex]);
            }
        }

        private void WriteTelemetryFrame()
        {
            if (!_telemetryRing.IsCreated || !_positions.IsCreated)
                return;

            int safeTentacleCount = math.clamp(activeTentacleCount, 0, MaxTentacles);
            int firstTipIndex = SegmentLastIndex;
            float3 root = _positions[0];
            float3 tip = _positions[firstTipIndex];
            bool invalid = _invalidInputDetected ||
                !math.all(math.isfinite(root)) ||
                !math.all(math.isfinite(tip)) ||
                !math.all(math.isfinite(_lastFlowVector));
            uint flags = safeTentacleCount > 0 ? 0x01u : 0u;
            if ((_tentacleStates.IsCreated && (_tentacleStates[0] & TentacleStateGrabbing) != 0u))
                flags |= 0x02u;
            if (_pendingOriginShiftRebase)
                flags |= 0x04u;
            if (invalid)
                flags |= 0x8000u;

            float3 safeRoot = SanitizeFiniteFloat3(root, float3.zero);
            float3 safeTip = SanitizeFiniteFloat3(tip, safeRoot);
            float3 safeFlow = SanitizeFiniteFloat3(_lastFlowVector, float3.zero);
            LeviathanTentacleTelemetryEntry entry = new LeviathanTentacleTelemetryEntry
            {
                FrameIndex = _frameIndex,
                ActiveTentacleCount = safeTentacleCount,
                Flags = flags,
                StateHash = ComputeTelemetryHash(safeRoot, safeTip, safeFlow, safeTentacleCount),
                Root0 = safeRoot,
                Tip0 = safeTip,
                FlowVector = safeFlow,
                MaxStretchFraction = ResolveMaxStretchFraction()
            };

            int telemetryWriteIndex = _telemetryCursor % TelemetryCapacity;
            if (telemetryWriteIndex < 0)
                telemetryWriteIndex += TelemetryCapacity;
            _telemetryRing[telemetryWriteIndex] = entry;
            _telemetryCursor = _telemetryCursor == int.MaxValue ? 0 : _telemetryCursor + 1;
            _frameIndex = _frameIndex == int.MaxValue ? 0 : _frameIndex + 1;
            if (invalid)
                DumpTelemetryBlackBoxOnce();
            _invalidInputDetected = false;
        }

        private float ResolveMaxStretchFraction()
        {
            if (!_stretchFractions.IsCreated)
                return 0f;

            float maxStretch = 0f;
            for (int i = 0; i < MaxTentacles; i++)
                maxStretch = math.max(maxStretch, _stretchFractions[i]);

            return maxStretch;
        }

        private void DumpTelemetryBlackBox()
        {
            if (!_telemetryRing.IsCreated)
                return;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string dumpPath = Path.Combine(projectRoot, TelemetryDumpRelativePath);
            string directory = Path.GetDirectoryName(dumpPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(TelemetryDumpMagic);
            writer.Write(TelemetryCapacity);
            writer.Write(_telemetryCursor);
            writer.Write(TelemetryEntryPayloadBytes);
            for (int i = 0; i < TelemetryCapacity; i++)
            {
                LeviathanTentacleTelemetryEntry entry = _telemetryRing[i];
                writer.Write(entry.FrameIndex);
                writer.Write(entry.ActiveTentacleCount);
                writer.Write(entry.Flags);
                writer.Write(entry.StateHash);
                writer.Write(entry.Root0.x);
                writer.Write(entry.Root0.y);
                writer.Write(entry.Root0.z);
                writer.Write(entry.Tip0.x);
                writer.Write(entry.Tip0.y);
                writer.Write(entry.Tip0.z);
                writer.Write(entry.FlowVector.x);
                writer.Write(entry.FlowVector.y);
                writer.Write(entry.FlowVector.z);
                writer.Write(entry.MaxStretchFraction);
                writer.Write(entry.Padding0);
                writer.Write(entry.Padding1);
            }
        }

        private void DumpTelemetryBlackBoxOnce()
        {
            if (_telemetryDumped || !_telemetryRing.IsCreated)
                return;

            DumpTelemetryBlackBox();
            _telemetryDumped = true;
        }

        private float3 ResolveRootRuntimePosition(int tentacleIndex)
        {
            Transform socket = tentacleIndex >= 0 && rootSockets != null && tentacleIndex < rootSockets.Length
                ? rootSockets[tentacleIndex]
                : null;
            float3 fallback = ResolveOwnerRuntimePosition();
            if (socket != null)
                return SanitizeFiniteInputFloat3((float3)socket.position, fallback);

            if (_cachedTransform == null)
                return fallback;

            return SanitizeFiniteInputFloat3(
                (float3)_cachedTransform.TransformPoint(_fallbackRootOffsets[math.clamp(tentacleIndex, 0, MaxTentacles - 1)]),
                fallback);
        }

        private float3 ResolveOwnerRuntimePosition()
        {
            return _cachedTransform != null
                ? SanitizeFiniteInputFloat3((float3)_cachedTransform.position, float3.zero)
                : float3.zero;
        }

        private static int ResolveConstraintIterations(HectonQualityTier tier, int highTierIterations)
        {
            return tier == HectonQualityTier.Low || tier == HectonQualityTier.Mx350 || tier == HectonQualityTier.Unknown
                ? 1
                : math.clamp(highTierIterations, 1, 3);
        }

        private void ResetConstraintIterationHysteresis()
        {
            int iterations = ResolveConstraintIterations(GlobalRegistry.ScalabilityTier, highTierConstraintIterations);
            _resolvedConstraintIterations = iterations;
            _pendingConstraintIterations = iterations;
            _constraintIterationSwitchTimer = 0f;
        }

        private int ResolveConstraintIterationsWithHysteresis(float deltaTime)
        {
            int requestedIterations = ResolveConstraintIterations(GlobalRegistry.ScalabilityTier, highTierConstraintIterations);
            if (_resolvedConstraintIterations < 1)
            {
                _resolvedConstraintIterations = requestedIterations;
                _pendingConstraintIterations = requestedIterations;
                _constraintIterationSwitchTimer = 0f;
                return requestedIterations;
            }

            if (requestedIterations == _resolvedConstraintIterations)
            {
                _pendingConstraintIterations = requestedIterations;
                _constraintIterationSwitchTimer = 0f;
                return _resolvedConstraintIterations;
            }

            if (requestedIterations != _pendingConstraintIterations)
            {
                _pendingConstraintIterations = requestedIterations;
                _constraintIterationSwitchTimer = 0f;
                return _resolvedConstraintIterations;
            }

            _constraintIterationSwitchTimer += math.max(0f, deltaTime);
            if (_constraintIterationSwitchTimer >= ConstraintIterationHysteresisSeconds)
            {
                _resolvedConstraintIterations = requestedIterations;
                _constraintIterationSwitchTimer = 0f;
            }

            return _resolvedConstraintIterations;
        }

        private static int FlatIndex(int tentacleIndex, int segmentIndex)
        {
            return tentacleIndex * SegmentsPerTentacle + segmentIndex;
        }

        private static uint ComputeTelemetryHash(float3 rootPosition, float3 tipPosition, float3 flowVector, int activeTentacleCount)
        {
            uint rootHash = math.hash(rootPosition);
            uint tipHash = math.hash(tipPosition);
            uint flowHash = math.hash(flowVector);
            return rootHash ^ (tipHash * 16777619u) ^ (flowHash * 2166136261u) ^ (uint)activeTentacleCount;
        }

        private static float3 SanitizeFiniteFloat3(float3 value, float3 fallback)
        {
            return math.all(math.isfinite(value)) ? value : fallback;
        }

        private float3 SanitizeFiniteInputFloat3(float3 value, float3 fallback)
        {
            if (math.all(math.isfinite(value)))
                return value;

            _invalidInputDetected = true;
            return fallback;
        }

        private static AbsoluteUniversePosition ToAbsoluteUniversePosition(float3 runtimePosition)
        {
            float3 safeRuntimePosition = SanitizeFiniteFloat3(runtimePosition, float3.zero);
            return AbsoluteUniversePosition.FromRuntimePosition(new Vector3(
                safeRuntimePosition.x,
                safeRuntimePosition.y,
                safeRuntimePosition.z));
        }

        private static bool TryPrepareAbyssalFlowPayload(
            GraphicsBuffer flowFieldBuffer,
            Vector4 gridResolution,
            Vector4 flowCenter,
            Vector4 flowSpacing,
            out Vector4 safeGridResolution,
            out Vector4 safeFlowCenter,
            out Vector4 safeFlowSpacing)
        {
            safeGridResolution = Vector4.zero;
            safeFlowCenter = Vector4.zero;
            safeFlowSpacing = Vector4.zero;
            if (!HasValidAbyssalFlowBuffer(flowFieldBuffer, 1) ||
                !IsFiniteVector4(gridResolution) ||
                !IsFiniteVector4(flowCenter) ||
                !IsFiniteVector4(flowSpacing))
            {
                return false;
            }

            if (!TryResolvePositiveInteger(gridResolution.x, MaxSupportedAbyssalFlowAxis, out int resolutionX) ||
                !TryResolvePositiveInteger(gridResolution.y, MaxSupportedAbyssalFlowAxis, out int resolutionY) ||
                !TryResolvePositiveInteger(gridResolution.z, MaxSupportedAbyssalFlowAxis, out int resolutionZ))
            {
                return false;
            }

            long cellCountLong = (long)resolutionX * resolutionY * resolutionZ;
            if (cellCountLong <= 0L || cellCountLong > flowFieldBuffer.count || cellCountLong > int.MaxValue)
                return false;

            int cellCount = (int)cellCountLong;
            if (!TryResolvePositiveInteger(gridResolution.w, flowFieldBuffer.count, out int publishedCellCount) ||
                publishedCellCount != cellCount)
            {
                return false;
            }

            float horizontalSpacing = math.abs(flowSpacing.x);
            float verticalSpacing = math.abs(flowSpacing.y);
            if (horizontalSpacing < FlowGridMinSpacing || verticalSpacing < FlowGridMinSpacing)
                return false;

            safeGridResolution = new Vector4(resolutionX, resolutionY, resolutionZ, cellCount);
            safeFlowCenter = flowCenter;
            safeFlowSpacing = new Vector4(
                horizontalSpacing,
                verticalSpacing,
                math.max(math.abs(flowSpacing.z), FlowGridMinSpacing),
                math.max(0f, flowSpacing.w));
            return true;
        }

        private static bool TryResolvePositiveInteger(float value, int maxValue, out int resolvedValue)
        {
            resolvedValue = 0;
            if (!math.isfinite(value) || maxValue < 1 || value < 1f || value > maxValue)
                return false;

            float roundedValue = math.round(value);
            if (math.abs(value - roundedValue) > FlowGridIntegerEpsilon)
                return false;

            resolvedValue = (int)roundedValue;
            return resolvedValue >= 1 && resolvedValue <= maxValue;
        }

        private static bool IsFiniteVector4(Vector4 value)
        {
            return math.all(math.isfinite(new float4(value.x, value.y, value.z, value.w)));
        }

        private static bool HasValidGraphicsBuffer(GraphicsBuffer buffer, int requiredCount)
        {
            return buffer != null && buffer.IsValid() && buffer.count >= requiredCount;
        }

        private static bool HasValidAbyssalFlowBuffer(GraphicsBuffer buffer, int requiredCount)
        {
            return HasValidGraphicsBuffer(buffer, requiredCount) &&
                buffer.stride == AbyssalFlowVectorStrideBytes;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            activeTentacleCount = Mathf.Clamp(activeTentacleCount, 1, MaxTentacles);
            restLength = Mathf.Max(0.01f, restLength);
            maxStretchLength = Mathf.Max(maxStretchLength, restLength * SegmentLastIndex);
            damping = Mathf.Clamp01(damping);
            highTierConstraintIterations = Mathf.Clamp(highTierConstraintIterations, 1, 3);
            baseRadius = Mathf.Max(0.001f, baseRadius);
            tipRadius = Mathf.Max(0.001f, tipRadius);
            flowStrength = Mathf.Max(0f, flowStrength);
            flowNoiseStrength = Mathf.Max(0f, flowNoiseStrength);
            suctionPulseStrength = Mathf.Clamp(suctionPulseStrength, 0f, 0.5f);
            grabDamageAmount = Mathf.Max(0f, grabDamageAmount);
            grabDamageImpulse = Mathf.Max(0f, grabDamageImpulse);
        }
#endif
    }
}
