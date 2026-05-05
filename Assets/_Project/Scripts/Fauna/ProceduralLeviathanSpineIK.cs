using System;
using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace Hecton8.AI
{
    /// <summary>
    /// Procedural presentation owner for leviathan-class fauna.
    /// Disables authored animator playback and drives a vertebra chain from a Catmull-Rom trailing spline.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FaunaBrain))]
    internal sealed class ProceduralLeviathanSpineIK : MonoBehaviour, IUpdatable, ILateFrameTickable, IOriginShiftListener
    {
        private const string NativeMemoryOwner = nameof(ProceduralLeviathanSpineIK);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct SolveSpineJob : IJobParallelForTransform
        {
            [ReadOnly] public NativeArray<float> NormalizedBoneT;
            [ReadOnly] public NativeArray<quaternion> BindWorldRotations;
            [WriteOnly] public NativeArray<quaternion> SolvedWorldRotations;
            [NativeDisableParallelForRestriction] public NativeArray<quaternion> SolvedHeadWorldRotations;
            [NativeDisableParallelForRestriction] public NativeArray<float> JawOpenRadians;
            public float3 HistoryTail;
            public float3 HistoryMidA;
            public float3 HistoryMidB;
            public float3 HistoryHead;
            public float3 HeadForward;
            public float3 WorldUp;
            public float3 HeadLookTargetPosition;
            public float3 StrikeTargetPosition;
            public float PhaseTime;
            public float SpeedNormalized;
            public float BlendWeight;
            public float AmplitudeRadians;
            public float VerticalAmplitudeScale;
            public float Frequency;
            public float HeadLookBlend;
            public float HeadLookClampRadians;
            public float StrikeBlend;
            public float StrikeDistanceNormalized;
            public float StrikeLeadWeight;
            public float JawOpenRadiansMax;
            public float JawOscillationFrequency;
            public float TelegraphBlend;
            public float TelegraphPitchRadians;
            public float TelegraphJawOpenRadians;
            public int LastBoneIndex;

            private static float3 ClampDirectionToCone(float3 baseDirection, float3 desiredDirection, float maxRadians, float3 fallbackAxis)
            {
                float3 safeBase = ContextualPhysicalIkMath.SafeNormalize(baseDirection, new float3(0f, 0f, 1f));
                float3 safeDesired = ContextualPhysicalIkMath.SafeNormalize(desiredDirection, safeBase);
                float clampedDot = math.clamp(math.dot(safeBase, safeDesired), -1f, 1f);
                float angle = math.acos(clampedDot);
                if (angle <= maxRadians || angle <= 0.0001f)
                    return safeDesired;

                float3 axis = ContextualPhysicalIkMath.SafeNormalize(math.cross(safeBase, safeDesired), fallbackAxis);
                if (math.lengthsq(axis) <= 0.0001f)
                    return safeBase;

                quaternion clampRotation = quaternion.AxisAngle(axis, maxRadians);
                return ContextualPhysicalIkMath.SafeNormalize(math.rotate(clampRotation, safeBase), safeBase);
            }

            public void Execute(int index, TransformAccess transform)
            {
                if (!transform.isValid)
                    return;

                float normalizedT = math.saturate(NormalizedBoneT[index]);
                float3 fallbackForward = ContextualPhysicalIkMath.SafeNormalize(HeadForward, new float3(0f, 0f, 1f));
                float inverseLastBone = math.rcp((float)math.max(1, LastBoneIndex));
                float nextT = math.saturate(normalizedT + inverseLastBone);
                float3 currentSplinePosition = ContextualPhysicalIkMath.CatmullRom(HistoryTail, HistoryMidA, HistoryMidB, HistoryHead, normalizedT);
                float3 nextSplinePosition = ContextualPhysicalIkMath.CatmullRom(HistoryTail, HistoryMidA, HistoryMidB, HistoryHead, nextT);
                float3 curveTangent = ContextualPhysicalIkMath.SafeNormalize(
                    nextSplinePosition - currentSplinePosition,
                    ContextualPhysicalIkMath.CatmullRomTangent(HistoryTail, HistoryMidA, HistoryMidB, HistoryHead, normalizedT, fallbackForward));

                float3 safeTangent = ContextualPhysicalIkMath.SafeNormalize(curveTangent, fallbackForward);
                float3 side = ContextualPhysicalIkMath.SafeNormalize(math.cross(WorldUp, safeTangent), new float3(1f, 0f, 0f));
                if (math.lengthsq(side) <= 0.0001f)
                    side = ContextualPhysicalIkMath.SafeNormalize(math.cross(new float3(1f, 0f, 0f), safeTangent), new float3(0f, 1f, 0f));

                float3 up = ContextualPhysicalIkMath.SafeNormalize(math.cross(safeTangent, side), WorldUp);
                float amplitude = AmplitudeRadians * SpeedNormalized * math.saturate(normalizedT);
                float phase = (PhaseTime * Frequency) + normalizedT * 8.5f;
                quaternion yawOffset = quaternion.AxisAngle(up, math.sin(phase) * amplitude);
                quaternion pitchOffset = quaternion.AxisAngle(side, math.cos(phase * 0.63f) * (amplitude * VerticalAmplitudeScale));
                float3 deformedForward = math.rotate(math.mul(yawOffset, pitchOffset), safeTangent);
                quaternion targetRotation = quaternion.LookRotationSafe(
                    ContextualPhysicalIkMath.SafeNormalize(deformedForward, safeTangent),
                    up);

                if (index == LastBoneIndex)
                {
                    float strikeBlend = math.saturate(StrikeBlend);
                    float headLookBlend = math.saturate(HeadLookBlend) * (1f - strikeBlend);
                    if (headLookBlend > 0f)
                    {
                        float3 unclampedHeadLookDirection = ContextualPhysicalIkMath.SafeNormalize(
                            HeadLookTargetPosition - (float3)transform.position,
                            safeTangent);
                        float3 clampedHeadLookDirection = ClampDirectionToCone(
                            safeTangent,
                            unclampedHeadLookDirection,
                            math.max(0f, HeadLookClampRadians),
                            up);
                        quaternion headLookRotation = quaternion.LookRotationSafe(clampedHeadLookDirection, up);
                        targetRotation = math.slerp(targetRotation, headLookRotation, headLookBlend);
                    }

                    float3 strikeDirection = ContextualPhysicalIkMath.SafeNormalize(
                        StrikeTargetPosition - (float3)transform.position,
                        safeTangent);
                    float3 headAimDirection = ContextualPhysicalIkMath.SafeNormalize(
                        math.lerp(safeTangent, strikeDirection, math.saturate(StrikeLeadWeight) * strikeBlend),
                        safeTangent);
                    quaternion strikeRotation = quaternion.LookRotationSafe(headAimDirection, up);
                    targetRotation = math.slerp(targetRotation, strikeRotation, strikeBlend);

                    float telegraphBlend = math.saturate(TelegraphBlend);
                    if (telegraphBlend > 0.001f)
                    {
                        float3 pullbackDirection = ContextualPhysicalIkMath.SafeNormalize(
                            (float3)transform.position - StrikeTargetPosition,
                            -safeTangent);
                        quaternion pitchBack = quaternion.AxisAngle(side, -math.max(0f, TelegraphPitchRadians));
                        float3 telegraphForward = ContextualPhysicalIkMath.SafeNormalize(
                            math.rotate(pitchBack, ContextualPhysicalIkMath.SafeNormalize(math.lerp(safeTangent, pullbackDirection, 0.55f), safeTangent)),
                            safeTangent);
                        quaternion telegraphRotation = quaternion.LookRotationSafe(telegraphForward, up);
                        targetRotation = math.slerp(targetRotation, telegraphRotation, telegraphBlend);
                    }

                    float jawWave = math.saturate((math.sin((PhaseTime * math.max(0f, JawOscillationFrequency)) + (StrikeDistanceNormalized * math.PI)) * 0.5f) + 0.5f);
                    float strikeJawRadians = jawWave * JawOpenRadiansMax * StrikeDistanceNormalized * strikeBlend;
                    float telegraphJawRadians = math.max(0f, TelegraphJawOpenRadians) * math.saturate(TelegraphBlend);
                    JawOpenRadians[0] = math.max(strikeJawRadians, telegraphJawRadians);
                    SolvedHeadWorldRotations[0] = targetRotation;
                }

                quaternion bindRotation = BindWorldRotations[index];
                SolvedWorldRotations[index] = math.slerp(bindRotation, targetRotation, math.saturate(BlendWeight));
            }
        }

        [Header("Spine Binding")]
        [Tooltip("Animator disabled while the procedural spine driver is active.")]
        [SerializeField] private Animator animator;
        [Tooltip("Optional explicit skeletal root. If omitted, the first SkinnedMeshRenderer root bone is used.")]
        [SerializeField] private Transform skeletalRoot;
        [Tooltip("Optional explicit head bone. If omitted, the deepest descendant containing 'head' is preferred.")]
        [SerializeField] private Transform headBone;
        [Tooltip("Optional explicit jaw bone. Auto-resolved from jaw naming when omitted.")]
        [SerializeField] private Transform jawBone;
        [Tooltip("Optional authored vertebra chain from tail/root toward head. Auto-resolved when empty.")]
        [SerializeField] private Transform[] vertebrae = Array.Empty<Transform>();

        [Header("Spline Response")]
        [SerializeField, Min(0.1f)] private float controlPointSpacing = 2.5f;
        [SerializeField, Min(0.1f)] private float velocityLookAheadSeconds = 0.18f;
        [SerializeField, Min(0.1f)] private float splineResponseSharpness = 10f;
        [SerializeField, Min(0.1f)] private float springFrequencyHz = 2.15f;
        [SerializeField, Range(0.1f, 2f)] private float springDampingRatio = 0.74f;
        [SerializeField, Min(1f)] private float springMaxVelocity = 90f;
        [SerializeField, Min(0f)] private float undulationAmplitudeDegrees = 12f;
        [SerializeField, Min(0f)] private float undulationFrequency = 2.2f;
        [SerializeField, Range(0f, 1f)] private float verticalAmplitudeScale = 0.35f;
        [SerializeField, Range(0f, 1f)] private float idleBlendWeight = 0.2f;
        [SerializeField, Min(0.1f)] private float turnDampingSharpness = 4.5f;
        [SerializeField, Range(0.1f, 1f)] private float reverseTurnAmplitudeScale = 0.42f;
        [SerializeField] private Vector3 worldUpAxis = Vector3.up;

        [Header("Strike Kinematics")]
        [SerializeField, Min(0f)] private float jawOpenDegrees = 52f;
        [SerializeField, Min(0f)] private float jawOscillationFrequency = 6.5f;
        [SerializeField, Min(0f)] private float strikeLeadSeconds = 0.3f;
        [SerializeField, Min(0.1f)] private float strikeResponseSharpness = 11f;
        [SerializeField, Min(0.1f)] private float strikeRecoverySeconds = 1.5f;
        [SerializeField, Min(0.1f)] private float headLookResponseSharpness = 8f;
        [SerializeField, Range(0f, 1f)] private float strikeHeadBlend = 0.85f;
        [SerializeField, Range(0f, 89f)] private float headLookClampDegrees = 60f;
        [SerializeField] private Vector3 jawLocalOpenAxis = Vector3.right;
        [SerializeField, Min(0f)] private float telegraphPullbackMeters = 2.4f;
        [SerializeField, Range(0f, 89f)] private float telegraphHeadPitchDegrees = 18f;
        [SerializeField, Range(0f, 89f)] private float telegraphJawOpenDegrees = 34f;

        private FaunaBrain _faunaBrain;
        private Rigidbody _rigidbody;
        private Rigidbody _strikeTargetRigidbody;
        private bool _registered;
        private bool _registeredOriginShiftListener;
        private bool _jobScheduled;
        private bool _animatorSuppressed;
        private bool _jawBindLocalRotationResolved;
        private float3 _tailPoint;
        private float3 _midPointA;
        private float3 _midPointB;
        private float3 _headPoint;
        private float3 _tailVelocity;
        private float3 _midPointAVelocity;
        private float3 _midPointBVelocity;
        private float3 _headPointVelocity;
        private float3 _lastResolvedHeadPosition;
        private float3 _headLookTargetWorldPosition;
        private float3 _strikeTargetWorldPosition;
        private float3 _strikeRecoveryTargetWorldPosition;
        private float3 _smoothedTravelDirection;
        private float _phaseTime;
        private float _headLookBlend;
        private float _strikeBlend;
        private float _strikeTelegraphBlend;
        private float _strikeTelegraphTargetBlend;
        private float _strikeRange = 1f;
        private float _strikeRecoveryTimeRemaining;
        private float _strikeRecoveryDistanceNormalized;
        private JobHandle _pendingSpineHandle;
        private TransformAccessArray _vertebraAccessArray;
        private Transform[] _runtimeChain = Array.Empty<Transform>();
        private NativeArray<float> _normalizedBoneT;
        private NativeArray<quaternion> _bindWorldRotations;
        private NativeArray<quaternion> _solvedWorldRotations;
        private NativeArray<quaternion> _solvedHeadWorldRotations;
        private NativeArray<float> _jawOpenRadians;
        private quaternion _jawBindLocalRotation;
        private Transform _strikeTarget;
        private bool _headLookTargetActive;
        private bool _wasStrikeActiveLastTick;
        // COLD ALLOC: List<SkinnedMeshRenderer>[8] â€“ skeletal root discovery scratch buffer for leviathan presentation binding â€“ owner: ProceduralLeviathanSpineIK
        private readonly List<SkinnedMeshRenderer> _rendererScratch = new List<SkinnedMeshRenderer>(8);
        // COLD ALLOC: List<Transform>[64] â€” temporary transform scan buffer for leviathan vertebra auto-resolution â€” owner: ProceduralLeviathanSpineIK
        private readonly List<Transform> _transformScratch = new List<Transform>(64);
        // COLD ALLOC: List<Transform>[64] â€” parent-chain assembly buffer used to build the runtime vertebra array â€” owner: ProceduralLeviathanSpineIK
        private readonly List<Transform> _chainScratch = new List<Transform>(64);

        private void Awake()
        {
            _faunaBrain = GetComponent<FaunaBrain>();
            if (_rigidbody == null)
                TryGetComponent(out _rigidbody);

            if (animator == null)
                TryGetComponent(out animator);

            TryResolveVertebraChain();
            RebuildRuntimeBuffers();
            ResetSplineState();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (!TryResolveVertebraChain())
                return;

            RebuildRuntimeBuffers();
            ResetSplineState();
            SuppressAnimatorPlayback(true);
            TryRegister();
            TryRegisterOriginShiftListener();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            TryUnregister();
            TryUnregisterOriginShiftListener();
            CompletePendingJob();
            SuppressAnimatorPlayback(false);
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterOriginShiftListener();
            CompletePendingJob();
            DisposeRuntimeBuffers();
        }

        internal void BindFromFauna(FaunaBrain faunaBrain, Rigidbody body, Animator runtimeAnimator)
        {
            _faunaBrain = faunaBrain;
            _rigidbody = body;
            if (runtimeAnimator != null)
                animator = runtimeAnimator;

            TryResolveVertebraChain();
            RebuildRuntimeBuffers();
            ResetSplineState();
            SuppressAnimatorPlayback(isActiveAndEnabled);
        }

        internal void SetStrikeIntent(Transform target, float strikeRange, bool strikeActive)
        {
            _strikeRange = math.max(1f, strikeRange);
            if (!strikeActive || target == null)
            {
                _strikeTarget = null;
                _strikeTargetRigidbody = null;
                return;
            }

            if (_strikeTarget != target)
            {
                _strikeTarget = target;
                _strikeTargetRigidbody = null;
                target.TryGetComponent(out _strikeTargetRigidbody);
            }
        }

        internal void SetAttackTelegraph(float blend01)
        {
            _strikeTelegraphTargetBlend = math.saturate(blend01);
        }

        internal void SetHeadLookTarget(Vector3 worldPosition, bool active)
        {
            _headLookTargetWorldPosition = worldPosition;
            _headLookTargetActive = active;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || _faunaBrain == null || _vertebraAccessArray.length <= 0)
                return;

            if (_jobScheduled)
                return;

            if (!TryResolveHeadPose(out float3 headPosition, out float3 headForward, out float speedNormalized))
                return;

            float safeSpacing = math.max(0.1f, controlPointSpacing);
            float safeLookAhead = math.max(0.01f, velocityLookAheadSeconds);
            float3 velocity = _rigidbody != null
                ? (float3)_rigidbody.linearVelocity
                : (headPosition - _lastResolvedHeadPosition) / math.max(deltaTime, 0.0001f);
            float3 velocityDirection = ContextualPhysicalIkMath.SafeNormalize(velocity, headForward);
            float3 previousTravelDirection = ContextualPhysicalIkMath.SafeNormalize(_smoothedTravelDirection, velocityDirection);
            float reversal01 = math.saturate((-math.dot(previousTravelDirection, velocityDirection) + 1f) * 0.5f);
            float dampingSharpness = math.max(0.1f, turnDampingSharpness) * math.lerp(1f, 0.2f, reversal01);
            float dampingAlpha = ContextualPhysicalIkMath.SmoothAlpha(dampingSharpness, deltaTime);
            _smoothedTravelDirection = ContextualPhysicalIkMath.SafeNormalize(
                math.lerp(previousTravelDirection, velocityDirection, dampingAlpha),
                velocityDirection);
            float3 headLead = headPosition + _smoothedTravelDirection * (math.length(velocity) * safeLookAhead);
            _lastResolvedHeadPosition = headPosition;
            _phaseTime += deltaTime;
            float amplitudeDamping = math.lerp(1f, math.saturate(reverseTurnAmplitudeScale), reversal01);
            float headLookBlendTarget = _headLookTargetActive ? 1f : 0f;
            float headLookBlendAlpha = ContextualPhysicalIkMath.SmoothAlpha(headLookResponseSharpness, deltaTime);
            _headLookBlend = math.lerp(_headLookBlend, headLookBlendTarget, headLookBlendAlpha);
            float strikeBlendTarget = _strikeTarget != null ? 1f : 0f;
            float strikeBlendAlpha = ContextualPhysicalIkMath.SmoothAlpha(strikeResponseSharpness, deltaTime);
            _strikeBlend = math.lerp(_strikeBlend, strikeBlendTarget, strikeBlendAlpha);
            _strikeTelegraphBlend = math.lerp(_strikeTelegraphBlend, _strikeTelegraphTargetBlend, strikeBlendAlpha);
            float3 resolvedHeadLookTarget = _headLookTargetActive ? _headLookTargetWorldPosition : headLead;
            float3 resolvedStrikeTargetPosition = headLead;
            float strikeDistanceNormalized = 0f;
            float effectiveStrikeBlend = _strikeBlend;
            float safeRecoverySeconds = math.max(0.1f, strikeRecoverySeconds);
            bool strikeHasLiveTarget = _strikeTarget != null;
            if (strikeHasLiveTarget)
            {
                float3 strikeTargetVelocity = _strikeTargetRigidbody != null ? (float3)_strikeTargetRigidbody.linearVelocity : float3.zero;
                resolvedStrikeTargetPosition = (float3)_strikeTarget.position + (strikeTargetVelocity * math.max(0f, strikeLeadSeconds));
                float strikeDistance = math.distance(headPosition, resolvedStrikeTargetPosition);
                strikeDistanceNormalized = math.saturate(1f - (strikeDistance / math.max(1f, _strikeRange)));
                _strikeRecoveryTimeRemaining = safeRecoverySeconds;
                _strikeRecoveryDistanceNormalized = strikeDistanceNormalized;
                _strikeRecoveryTargetWorldPosition = resolvedStrikeTargetPosition;
                _wasStrikeActiveLastTick = true;
            }
            else
            {
                if (_wasStrikeActiveLastTick)
                {
                    _strikeRecoveryTimeRemaining = safeRecoverySeconds;
                    _wasStrikeActiveLastTick = false;
                }

                if (_strikeRecoveryTimeRemaining > 0f)
                {
                    _strikeRecoveryTimeRemaining = math.max(0f, _strikeRecoveryTimeRemaining - deltaTime);
                    float recoveryBlend = math.saturate(_strikeRecoveryTimeRemaining / safeRecoverySeconds);
                    resolvedStrikeTargetPosition = math.lerp(headLead, _strikeRecoveryTargetWorldPosition, recoveryBlend);
                    strikeDistanceNormalized = math.lerp(0f, _strikeRecoveryDistanceNormalized, recoveryBlend);
                    effectiveStrikeBlend = recoveryBlend;
                }
                else
                {
                    effectiveStrikeBlend = 0f;
                }
            }

            float3 headSpringTarget = headPosition;
            if (_strikeTelegraphBlend > 0.001f)
            {
                float3 pullbackDirection = ContextualPhysicalIkMath.SafeNormalize(
                    headPosition - resolvedStrikeTargetPosition,
                    -_smoothedTravelDirection);
                headSpringTarget += pullbackDirection * (math.max(0f, telegraphPullbackMeters) * _strikeTelegraphBlend);
            }

            float responseScale = math.sqrt(math.max(0.1f, splineResponseSharpness) / 10f);
            float springOmega = math.max(0.1f, springFrequencyHz) * responseScale * (math.PI * 2f);
            float springStiffness = springOmega * springOmega;
            float springDamping = 2f * math.max(0.1f, springDampingRatio) * springOmega;
            IntegrateControlPointSpring(headSpringTarget, springStiffness, springDamping, deltaTime, ref _headPoint, ref _headPointVelocity);
            IntegrateControlPointSpring(_headPoint - _smoothedTravelDirection * safeSpacing, springStiffness, springDamping, deltaTime, ref _midPointB, ref _midPointBVelocity);
            IntegrateControlPointSpring(_midPointB - _smoothedTravelDirection * safeSpacing, springStiffness, springDamping, deltaTime, ref _midPointA, ref _midPointAVelocity);
            IntegrateControlPointSpring(_midPointA - _smoothedTravelDirection * safeSpacing, springStiffness, springDamping, deltaTime, ref _tailPoint, ref _tailVelocity);

            _strikeTargetWorldPosition = math.lerp(_strikeTargetWorldPosition, resolvedStrikeTargetPosition, strikeBlendAlpha);
            _headLookTargetWorldPosition = math.lerp(_headLookTargetWorldPosition, resolvedHeadLookTarget, headLookBlendAlpha);

            SolveSpineJob job = new SolveSpineJob
            {
                NormalizedBoneT = _normalizedBoneT,
                BindWorldRotations = _bindWorldRotations,
                SolvedWorldRotations = _solvedWorldRotations,
                SolvedHeadWorldRotations = _solvedHeadWorldRotations,
                JawOpenRadians = _jawOpenRadians,
                HistoryTail = _tailPoint,
                HistoryMidA = _midPointA,
                HistoryMidB = _midPointB,
                HistoryHead = _headPoint,
                HeadForward = headForward,
                WorldUp = ContextualPhysicalIkMath.SafeNormalize((float3)worldUpAxis, new float3(0f, 1f, 0f)),
                HeadLookTargetPosition = _headLookTargetWorldPosition,
                StrikeTargetPosition = _strikeTargetWorldPosition,
                PhaseTime = _phaseTime,
                SpeedNormalized = speedNormalized,
                BlendWeight = math.lerp(idleBlendWeight, 1f, speedNormalized),
                AmplitudeRadians = math.radians(math.max(0f, undulationAmplitudeDegrees)) * amplitudeDamping,
                VerticalAmplitudeScale = math.saturate(verticalAmplitudeScale),
                Frequency = math.max(0f, undulationFrequency),
                HeadLookBlend = _headLookBlend,
                HeadLookClampRadians = math.radians(math.clamp(headLookClampDegrees, 0f, 89f)),
                StrikeBlend = effectiveStrikeBlend,
                StrikeDistanceNormalized = strikeDistanceNormalized,
                StrikeLeadWeight = math.saturate(strikeHeadBlend),
                JawOpenRadiansMax = math.radians(math.max(0f, jawOpenDegrees)),
                JawOscillationFrequency = math.max(0f, jawOscillationFrequency),
                LastBoneIndex = _normalizedBoneT.Length - 1,
                TelegraphBlend = _strikeTelegraphBlend,
                TelegraphPitchRadians = math.radians(math.clamp(telegraphHeadPitchDegrees, 0f, 89f)),
                TelegraphJawOpenRadians = math.radians(math.clamp(telegraphJawOpenDegrees, 0f, 89f))
            };

            _pendingSpineHandle = IJobParallelForTransformExtensions.ScheduleByRef(ref job, _vertebraAccessArray, default);
            _jobScheduled = true;
        }

        public void LateFrameTick()
        {
            CompletePendingJob(force: false);
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            CompletePendingJob();
            float3 shiftOffset = shiftData.ShiftOffset;
            _tailPoint += shiftOffset;
            _midPointA += shiftOffset;
            _midPointB += shiftOffset;
            _headPoint += shiftOffset;
            _lastResolvedHeadPosition += shiftOffset;
            _headLookTargetWorldPosition += shiftOffset;
            _strikeTargetWorldPosition += shiftOffset;
            _strikeRecoveryTargetWorldPosition += shiftOffset;
        }

        private void TryRegister()
        {
            if (_registered)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Environment);
            _registered = GlobalRegistry.Updatables.Contains(this) ||
                          SystemDispatcher.GetLateFrameLane(PriorityLayer.Environment).Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            if (SystemDispatcher.GetLateFrameLane(PriorityLayer.Environment).Contains(this))
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);

            if (GlobalRegistry.Updatables.Contains(this))
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);

            _registered = false;
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

        private bool CompletePendingJob(bool force = true)
        {
            if (!_jobScheduled)
                return true;

            if (!DispatcherJobSwap.TryComplete(ref _pendingSpineHandle, force))
                return false;

            _jobScheduled = false;
            ApplySolvedRotations();
            return true;
        }

        private void SuppressAnimatorPlayback(bool suppress)
        {
            if (animator == null)
                return;

            if (suppress)
            {
                if (!_animatorSuppressed)
                {
                    animator.enabled = false;
                    _animatorSuppressed = true;
                }
            }
            else if (_animatorSuppressed)
            {
                animator.enabled = true;
                _animatorSuppressed = false;
            }
        }

        private bool TryResolveHeadPose(out float3 headPosition, out float3 headForward, out float speedNormalized)
        {
            Transform resolvedHead = headBone != null ? headBone : transform;
            headPosition = resolvedHead.position;
            headForward = resolvedHead.forward;

            float speed = _rigidbody != null ? _rigidbody.linearVelocity.magnitude : 0f;
            float maxSpeed = 1f;
            if (_faunaBrain != null && _faunaBrain.SpeciesProfile != null)
                maxSpeed = math.max(1f, _faunaBrain.SpeciesProfile.aggressiveSpeedMultiplier * 6f);

            speedNormalized = math.saturate(speed / maxSpeed);
            return true;
        }

        private bool TryResolveVertebraChain()
        {
            if (vertebrae != null && vertebrae.Length >= 2)
            {
                if (headBone == null)
                    headBone = vertebrae[vertebrae.Length - 1];

                if (jawBone == null)
                {
                    _transformScratch.Clear();
                    GetComponentsInChildren(true, _transformScratch);
                    float3 jawAnchor = headBone != null ? (float3)headBone.position : (float3)transform.position;
                    float bestJawDistanceSq = float.MaxValue;
                    for (int i = 0; i < _transformScratch.Count; i++)
                    {
                        Transform candidate = _transformScratch[i];
                        if (candidate == null || candidate == transform)
                            continue;

                        string lowerName = candidate.name.ToLowerInvariant();
                        if (!lowerName.Contains("jaw") && !lowerName.Contains("mandible") && !lowerName.Contains("mouth"))
                            continue;

                        float distanceSq = math.lengthsq((float3)candidate.position - jawAnchor);
                        if (distanceSq >= bestJawDistanceSq)
                            continue;

                        bestJawDistanceSq = distanceSq;
                        jawBone = candidate;
                    }
                }

                return true;
            }

            if (animator == null)
                TryGetComponent(out animator);

            Transform resolvedRoot = skeletalRoot;
            if (resolvedRoot == null)
            {
                _rendererScratch.Clear();
                GetComponentsInChildren(true, _rendererScratch);
                for (int i = 0; i < _rendererScratch.Count; i++)
                {
                    SkinnedMeshRenderer renderer = _rendererScratch[i];
                    if (renderer != null && renderer.rootBone != null)
                    {
                        resolvedRoot = renderer.rootBone;
                        break;
                    }
                }
            }

            _transformScratch.Clear();
            GetComponentsInChildren(true, _transformScratch);
            Transform resolvedHead = headBone;
            if (resolvedHead == null)
            {
                float farthestDistanceSq = -1f;
                for (int i = 0; i < _transformScratch.Count; i++)
                {
                    Transform candidate = _transformScratch[i];
                    if (candidate == null || candidate == transform)
                        continue;

                    string lowerName = candidate.name.ToLowerInvariant();
                    if (!lowerName.Contains("head") && !lowerName.Contains("neck"))
                        continue;

                    float distanceSq = (candidate.position - transform.position).sqrMagnitude;
                    if (distanceSq <= farthestDistanceSq)
                        continue;

                    farthestDistanceSq = distanceSq;
                    resolvedHead = candidate;
                }
            }

            Transform resolvedJaw = jawBone;
            if (resolvedJaw == null)
            {
                float3 jawAnchor = resolvedHead != null ? (float3)resolvedHead.position : (float3)transform.position;
                float bestJawDistanceSq = float.MaxValue;
                for (int i = 0; i < _transformScratch.Count; i++)
                {
                    Transform candidate = _transformScratch[i];
                    if (candidate == null || candidate == transform)
                        continue;

                    string lowerName = candidate.name.ToLowerInvariant();
                    if (!lowerName.Contains("jaw") && !lowerName.Contains("mandible") && !lowerName.Contains("mouth"))
                        continue;

                    float distanceSq = math.lengthsq((float3)candidate.position - jawAnchor);
                    if (distanceSq >= bestJawDistanceSq)
                        continue;

                    bestJawDistanceSq = distanceSq;
                    resolvedJaw = candidate;
                }
            }

            if (resolvedRoot == null || resolvedHead == null)
                return false;

            _chainScratch.Clear();
            Transform current = resolvedHead;
            while (current != null)
            {
                _chainScratch.Add(current);
                if (current == resolvedRoot)
                    break;

                current = current.parent;
            }

            int chainCount = _chainScratch.Count;
            if (chainCount < 2)
                return false;

            if (_chainScratch[chainCount - 1] != resolvedRoot)
                _chainScratch.Add(resolvedRoot);

            int resolvedCount = _chainScratch.Count;
            vertebrae = new Transform[resolvedCount];
            for (int i = 0; i < resolvedCount; i++)
                vertebrae[i] = _chainScratch[resolvedCount - 1 - i];

            skeletalRoot = resolvedRoot;
            headBone = resolvedHead;
            jawBone = resolvedJaw;
            return true;
        }

        private void RebuildRuntimeBuffers()
        {
            CompletePendingJob();
            DisposeRuntimeBuffers();

            if (vertebrae == null || vertebrae.Length == 0)
                return;

            int validCount = 0;
            for (int i = 0; i < vertebrae.Length; i++)
            {
                if (vertebrae[i] != null)
                    validCount++;
            }

            if (validCount <= 0)
                return;

            // COLD ALLOC: Transform[validCount] â€“ cached vertebra chain used for post-job writeback â€“ owner: ProceduralLeviathanSpineIK
            _runtimeChain = new Transform[validCount];
            int writeIndex = 0;
            for (int i = 0; i < vertebrae.Length; i++)
            {
                if (vertebrae[i] == null)
                    continue;

                _runtimeChain[writeIndex] = vertebrae[i];
                writeIndex++;
            }

            TransformAccessArray.Allocate(validCount, -1, out _vertebraAccessArray);
            _vertebraAccessArray.SetTransforms(_runtimeChain);
            // COLD ALLOC: NativeArray<float>[validCount] â€“ normalized vertebra spline coordinates for leviathan presentation job â€“ owner: ProceduralLeviathanSpineIK
            _normalizedBoneT = new NativeArray<float>(validCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            // COLD ALLOC: NativeArray<quaternion>[validCount] â€“ bind-space world rotations used as the procedural leviathan presentation baseline â€“ owner: ProceduralLeviathanSpineIK
            _bindWorldRotations = new NativeArray<quaternion>(validCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            // COLD ALLOC: NativeArray<quaternion>[validCount] â€“ solved Catmull-Rom world rotations produced by the Burst spine job â€“ owner: ProceduralLeviathanSpineIK
            _solvedWorldRotations = new NativeArray<quaternion>(validCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            // COLD ALLOC: NativeArray<quaternion>[1] â€” strike head world rotation written by the procedural leviathan Burst solve â€” owner: ProceduralLeviathanSpineIK
            _solvedHeadWorldRotations = new NativeArray<quaternion>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            // COLD ALLOC: NativeArray<float>[1] â€” jaw-open radians written by the procedural leviathan Burst solve â€” owner: ProceduralLeviathanSpineIK
            _jawOpenRadians = new NativeArray<float>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            RegisterRuntimeBuffers();

            float denominator = math.max(1, validCount - 1);
            for (int i = 0; i < validCount; i++)
            {
                _normalizedBoneT[i] = i / denominator;
                _bindWorldRotations[i] = _runtimeChain[i] != null
                    ? (quaternion)_runtimeChain[i].rotation
                    : quaternion.identity;
                _solvedWorldRotations[i] = _bindWorldRotations[i];
            }

            _solvedHeadWorldRotations[0] = headBone != null ? (quaternion)headBone.rotation : quaternion.identity;
            _jawOpenRadians[0] = 0f;
            CaptureJawBindPose();
        }

        private void DisposeRuntimeBuffers()
        {
            CompletePendingJob();

            if (_vertebraAccessArray.isCreated)
                _vertebraAccessArray.Dispose();

            if (_normalizedBoneT.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_normalizedBoneT);
                _normalizedBoneT.Dispose();
                _normalizedBoneT = default;
            }

            if (_bindWorldRotations.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_bindWorldRotations);
                _bindWorldRotations.Dispose();
                _bindWorldRotations = default;
            }

            if (_solvedWorldRotations.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_solvedWorldRotations);
                _solvedWorldRotations.Dispose();
                _solvedWorldRotations = default;
            }

            if (_solvedHeadWorldRotations.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_solvedHeadWorldRotations);
                _solvedHeadWorldRotations.Dispose();
                _solvedHeadWorldRotations = default;
            }

            if (_jawOpenRadians.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_jawOpenRadians);
                _jawOpenRadians.Dispose();
                _jawOpenRadians = default;
            }

            _runtimeChain = Array.Empty<Transform>();
        }

        private void RegisterRuntimeBuffers()
        {
            NativeMemorySentinel.RegisterNativeArray(_normalizedBoneT, NativeMemoryOwner, nameof(_normalizedBoneT), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_bindWorldRotations, NativeMemoryOwner, nameof(_bindWorldRotations), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_solvedWorldRotations, NativeMemoryOwner, nameof(_solvedWorldRotations), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_solvedHeadWorldRotations, NativeMemoryOwner, nameof(_solvedHeadWorldRotations), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_jawOpenRadians, NativeMemoryOwner, nameof(_jawOpenRadians), NativeMemoryLifetime);
        }

        private void ResetSplineState()
        {
            Transform resolvedHead = headBone != null ? headBone : transform;
            float3 headPosition = resolvedHead != null ? (float3)resolvedHead.position : float3.zero;
            float3 headForward = resolvedHead != null ? (float3)resolvedHead.forward : new float3(0f, 0f, 1f);
            float safeSpacing = math.max(0.1f, controlPointSpacing);
            _headPoint = headPosition;
            _midPointB = headPosition - headForward * safeSpacing;
            _midPointA = headPosition - headForward * safeSpacing * 2f;
            _tailPoint = headPosition - headForward * safeSpacing * 3f;
            _headPointVelocity = float3.zero;
            _midPointBVelocity = float3.zero;
            _midPointAVelocity = float3.zero;
            _tailVelocity = float3.zero;
            _lastResolvedHeadPosition = headPosition;
            _headLookTargetWorldPosition = headPosition + (headForward * safeSpacing);
            _strikeTargetWorldPosition = headPosition + (headForward * safeSpacing);
            _strikeRecoveryTargetWorldPosition = _strikeTargetWorldPosition;
            _smoothedTravelDirection = ContextualPhysicalIkMath.SafeNormalize(headForward, new float3(0f, 0f, 1f));
            _phaseTime = 0f;
            _headLookBlend = 0f;
            _strikeBlend = 0f;
            _strikeTelegraphBlend = 0f;
            _strikeTelegraphTargetBlend = 0f;
            _strikeRecoveryTimeRemaining = 0f;
            _strikeRecoveryDistanceNormalized = 0f;
            _headLookTargetActive = false;
            _wasStrikeActiveLastTick = false;
            if (_jawOpenRadians.IsCreated)
                _jawOpenRadians[0] = 0f;

            CaptureJawBindPose();
            ApplyJawRotation(0f);
        }

        private void IntegrateControlPointSpring(
            float3 target,
            float stiffness,
            float damping,
            float deltaTime,
            ref float3 position,
            ref float3 velocity)
        {
            ContextualPhysicalIkMath.IntegrateSpringDamper(target, stiffness, damping, deltaTime, ref position, ref velocity);
            float maxVelocity = math.max(1f, springMaxVelocity);
            float velocityLengthSq = math.lengthsq(velocity);
            float maxVelocitySq = maxVelocity * maxVelocity;
            if (velocityLengthSq > maxVelocitySq)
                velocity = ContextualPhysicalIkMath.SafeNormalize(velocity, float3.zero) * maxVelocity;
        }

        private void ApplySolvedRotations()
        {
            if (_runtimeChain == null || !_solvedWorldRotations.IsCreated)
                return;

            int count = math.min(_runtimeChain.Length, _solvedWorldRotations.Length);
            for (int i = 0; i < count; i++)
            {
                Transform vertebra = _runtimeChain[i];
                if (vertebra == null)
                    continue;

                vertebra.rotation = _solvedWorldRotations[i];
            }

            if (headBone != null && _solvedHeadWorldRotations.IsCreated && _solvedHeadWorldRotations.Length > 0)
                headBone.rotation = _solvedHeadWorldRotations[0];

            if (_jawOpenRadians.IsCreated && _jawOpenRadians.Length > 0)
                ApplyJawRotation(_jawOpenRadians[0]);
        }

        private void CaptureJawBindPose()
        {
            if (jawBone == null)
                return;

            _jawBindLocalRotation = jawBone.localRotation;
            _jawBindLocalRotationResolved = true;
        }

        private void ApplyJawRotation(float jawOpenRadians)
        {
            if (jawBone == null)
                return;

            if (!_jawBindLocalRotationResolved)
                CaptureJawBindPose();

            float3 localAxis = ContextualPhysicalIkMath.SafeNormalize((float3)jawLocalOpenAxis, new float3(1f, 0f, 0f));
            quaternion jawOffset = quaternion.AxisAngle(localAxis, jawOpenRadians);
            jawBone.localRotation = math.mul(_jawBindLocalRotation, jawOffset);
        }
    }
}
