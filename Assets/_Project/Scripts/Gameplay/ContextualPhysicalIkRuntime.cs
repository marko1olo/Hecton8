using Hecton8.Core;
using Hecton8.World;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ContextualPhysicalIkContactTarget
    {
        public float3 WorldPosition;
        public float3 WorldNormal;
        public float Blend;
        public float DeltaHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ContextualPhysicalIkTargetFrame
    {
        public ContextualPhysicalIkContactTarget LeftFoot;
        public ContextualPhysicalIkContactTarget RightFoot;
        public ContextualPhysicalIkContactTarget LeftHand;
        public ContextualPhysicalIkContactTarget RightHand;
        public float3 ComOffsetLocal;
        public float2 ComLeanRadians;
        public float DeltaTime;
        public float ViewerDistanceSq;
        public float TunnelBlend;
        public uint UpdateBitfield;
        public byte ContextMask;
        public byte ThrottleTier;
        public byte ShouldComputeThisFrame;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ContextualPhysicalIkEntityState
    {
        public int IsActive;
        public int EnableFootPlacement;
        public int EnableHandBracing;
        public float DeltaTime;
        public quaternion RootRotation;
        public float3 RootPosition;
        public float3 PelvisPosition;
        public float3 LeftFootProbeOrigin;
        public float3 RightFootProbeOrigin;
        public float3 LeftHandProbeOrigin;
        public float3 RightHandProbeOrigin;
        public float3 PredictiveLeftHandPosition;
        public float3 PredictiveRightHandPosition;
        public float3 PredictiveLeftHandNormal;
        public float3 PredictiveRightHandNormal;
        public float LeftLegReach;
        public float RightLegReach;
        public float LeftArmReach;
        public float RightArmReach;
        public float PredictiveLeftHandBlend;
        public float PredictiveRightHandBlend;
        public float FootContactOffset;
        public float HandContactOffset;
        public float FootProbeDistanceScale;
        public float HandProbeDistanceScale;
        public int GroundLayerMask;
        public int WallLayerMask;
        public float TunnelClearanceDistance;
        public float HandBraceFadeDistance;
        public float TargetPositionSharpness;
        public float TargetNormalSharpness;
        public float BlendFadeSharpness;
        public float MaxDeltaHeight;
        public float ComShiftLateralFactor;
        public float ComShiftForwardFactor;
        public float ComShiftVerticalFactor;
        public float ComResponseSharpness;
        public float ComLeanPitchRadians;
        public float ComLeanRollRadians;
        public float MaxComLateral;
        public float MaxComForward;
        public float MaxComVertical;
        public int UpdateThisFrame;
        public float ViewerDistanceSq;
        public uint UpdateBitfield;
        public byte ThrottleTier;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    internal struct ContextualPhysicalIkGroundDetectionJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<ContextualPhysicalIkEntityState> Entities;
        public NativeArray<RaycastCommand> Commands;

        public void Execute(int index)
        {
            int baseCommandIndex = index * ContextualPhysicalIkRuntime.RaysPerEntity;
            ContextualPhysicalIkEntityState entity = Entities[index];

            if (entity.IsActive == 0 || entity.UpdateThisFrame == 0)
            {
                WriteDisabledCommand(baseCommandIndex + 0);
                WriteDisabledCommand(baseCommandIndex + 1);
                WriteDisabledCommand(baseCommandIndex + 2);
                WriteDisabledCommand(baseCommandIndex + 3);
                return;
            }

            QueryParameters groundQuery = new QueryParameters(entity.GroundLayerMask, false, QueryTriggerInteraction.Ignore, false);
            QueryParameters wallQuery = new QueryParameters(entity.WallLayerMask, false, QueryTriggerInteraction.Ignore, false);

            float leftFootDistance = entity.EnableFootPlacement != 0 ? math.max(0.0f, entity.LeftLegReach * entity.FootProbeDistanceScale) : 0.0f;
            float rightFootDistance = entity.EnableFootPlacement != 0 ? math.max(0.0f, entity.RightLegReach * entity.FootProbeDistanceScale) : 0.0f;
            bool leftHandUsesPredictiveLatch = entity.PredictiveLeftHandBlend > 0.0001f;
            bool rightHandUsesPredictiveLatch = entity.PredictiveRightHandBlend > 0.0001f;
            float leftHandDistance = entity.EnableHandBracing != 0 && !leftHandUsesPredictiveLatch ? math.max(0.0f, entity.LeftArmReach * entity.HandProbeDistanceScale) : 0.0f;
            float rightHandDistance = entity.EnableHandBracing != 0 && !rightHandUsesPredictiveLatch ? math.max(0.0f, entity.RightArmReach * entity.HandProbeDistanceScale) : 0.0f;

            float3 leftBraceDirection = ContextualPhysicalIkMath.SafeNormalize(
                math.mul(entity.RootRotation, new float3(-0.7f, -0.7f, 0.0f)),
                new float3(-0.70710677f, -0.70710677f, 0.0f));
            float3 rightBraceDirection = ContextualPhysicalIkMath.SafeNormalize(
                math.mul(entity.RootRotation, new float3(0.7f, -0.7f, 0.0f)),
                new float3(0.70710677f, -0.70710677f, 0.0f));

            Commands[baseCommandIndex + 0] = new RaycastCommand(
                ContextualPhysicalIkMath.ToUnityVector3(entity.LeftFootProbeOrigin),
                Vector3.down,
                groundQuery,
                leftFootDistance);

            Commands[baseCommandIndex + 1] = new RaycastCommand(
                ContextualPhysicalIkMath.ToUnityVector3(entity.RightFootProbeOrigin),
                Vector3.down,
                groundQuery,
                rightFootDistance);

            if (leftHandUsesPredictiveLatch)
            {
                WriteDisabledCommand(baseCommandIndex + 2);
            }
            else
            {
                Commands[baseCommandIndex + 2] = new RaycastCommand(
                    ContextualPhysicalIkMath.ToUnityVector3(entity.LeftHandProbeOrigin),
                    ContextualPhysicalIkMath.ToUnityVector3(leftBraceDirection),
                    wallQuery,
                    leftHandDistance);
            }

            if (rightHandUsesPredictiveLatch)
            {
                WriteDisabledCommand(baseCommandIndex + 3);
            }
            else
            {
                Commands[baseCommandIndex + 3] = new RaycastCommand(
                    ContextualPhysicalIkMath.ToUnityVector3(entity.RightHandProbeOrigin),
                    ContextualPhysicalIkMath.ToUnityVector3(rightBraceDirection),
                    wallQuery,
                    rightHandDistance);
            }
        }

        private void WriteDisabledCommand(int commandIndex)
        {
            Commands[commandIndex] = new RaycastCommand(
                Vector3.zero,
                Vector3.down,
                new QueryParameters(HectonLayerMasks.NoLayers, false, QueryTriggerInteraction.Ignore, false),
                0.0f);
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    internal struct ContextualPhysicalIkGroundResponseJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<ContextualPhysicalIkEntityState> Entities;
        [ReadOnly] public NativeArray<RaycastHit> Hits;
        [ReadOnly] public NativeArray<ContextualPhysicalIkTargetFrame> PreviousTargets;
        public NativeArray<ContextualPhysicalIkTargetFrame> NextTargets;

        public void Execute(int index)
        {
            ContextualPhysicalIkEntityState entity = Entities[index];
            if (entity.IsActive == 0)
            {
                NextTargets[index] = default;
                return;
            }

            ContextualPhysicalIkTargetFrame previous = PreviousTargets[index];
            ContextualPhysicalIkTargetFrame next = previous;
            next.DeltaTime = entity.DeltaTime;
            next.ViewerDistanceSq = entity.ViewerDistanceSq;
            next.UpdateBitfield = entity.UpdateBitfield;
            next.ThrottleTier = entity.ThrottleTier;
            next.ShouldComputeThisFrame = entity.UpdateThisFrame != 0 ? (byte)1 : (byte)0;
            int baseHitIndex = index * ContextualPhysicalIkRuntime.RaysPerEntity;
            RaycastHit leftFootHit = Hits[baseHitIndex + 0];
            RaycastHit rightFootHit = Hits[baseHitIndex + 1];
            RaycastHit leftHandHit = Hits[baseHitIndex + 2];
            RaycastHit rightHandHit = Hits[baseHitIndex + 3];

            if (entity.UpdateThisFrame == 0)
            {
                NextTargets[index] = next;
                return;
            }

            if (entity.EnableFootPlacement != 0)
            {
                ResolveContactTarget(
                    ref next.LeftFoot,
                    in previous.LeftFoot,
                    in leftFootHit,
                    entity.LeftFootProbeOrigin,
                    entity.FootContactOffset,
                    1.0f,
                    entity.TargetPositionSharpness,
                    entity.TargetNormalSharpness,
                    entity.BlendFadeSharpness,
                    entity.MaxDeltaHeight,
                    entity.DeltaTime);

                ResolveContactTarget(
                    ref next.RightFoot,
                    in previous.RightFoot,
                    in rightFootHit,
                    entity.RightFootProbeOrigin,
                    entity.FootContactOffset,
                    1.0f,
                    entity.TargetPositionSharpness,
                    entity.TargetNormalSharpness,
                    entity.BlendFadeSharpness,
                    entity.MaxDeltaHeight,
                    entity.DeltaTime);
            }
            else
            {
                FadeOutTarget(ref next.LeftFoot, in previous.LeftFoot, entity.BlendFadeSharpness, entity.DeltaTime);
                FadeOutTarget(ref next.RightFoot, in previous.RightFoot, entity.BlendFadeSharpness, entity.DeltaTime);
            }

            float tunnelTargetBlend = entity.EnableHandBracing != 0
                ? ResolveBraceProxyTunnelBlend(in leftHandHit, in rightHandHit, in entity)
                : 0.0f;
            next.TunnelBlend = ContextualPhysicalIkMath.SmoothScalar(previous.TunnelBlend, tunnelTargetBlend, entity.BlendFadeSharpness, entity.DeltaTime);
            next.ContextMask = next.TunnelBlend > 0.05f ? (byte)0x01 : (byte)0x00;

            if (entity.EnableHandBracing != 0)
            {
                ResolveContactTarget(
                    ref next.LeftHand,
                    in previous.LeftHand,
                    in leftHandHit,
                    entity.LeftHandProbeOrigin,
                    entity.HandContactOffset,
                    next.TunnelBlend,
                    entity.TargetPositionSharpness,
                    entity.TargetNormalSharpness,
                    entity.BlendFadeSharpness,
                    entity.MaxDeltaHeight,
                    entity.DeltaTime);

                ResolveContactTarget(
                    ref next.RightHand,
                    in previous.RightHand,
                    in rightHandHit,
                    entity.RightHandProbeOrigin,
                    entity.HandContactOffset,
                    next.TunnelBlend,
                    entity.TargetPositionSharpness,
                    entity.TargetNormalSharpness,
                    entity.BlendFadeSharpness,
                    entity.MaxDeltaHeight,
                    entity.DeltaTime);

                ApplyPredictiveLatch(
                    ref next.LeftHand,
                    in previous.LeftHand,
                    entity.PredictiveLeftHandPosition,
                    entity.PredictiveLeftHandNormal,
                    entity.PredictiveLeftHandBlend,
                    entity.TargetPositionSharpness,
                    entity.TargetNormalSharpness,
                    entity.BlendFadeSharpness,
                    entity.DeltaTime);

                ApplyPredictiveLatch(
                    ref next.RightHand,
                    in previous.RightHand,
                    entity.PredictiveRightHandPosition,
                    entity.PredictiveRightHandNormal,
                    entity.PredictiveRightHandBlend,
                    entity.TargetPositionSharpness,
                    entity.TargetNormalSharpness,
                    entity.BlendFadeSharpness,
                    entity.DeltaTime);
            }
            else
            {
                FadeOutTarget(ref next.LeftHand, in previous.LeftHand, entity.BlendFadeSharpness, entity.DeltaTime);
                FadeOutTarget(ref next.RightHand, in previous.RightHand, entity.BlendFadeSharpness, entity.DeltaTime);
            }

            float leftDelta = next.LeftFoot.DeltaHeight * next.LeftFoot.Blend;
            float rightDelta = next.RightFoot.DeltaHeight * next.RightFoot.Blend;
            float deltaDifference = leftDelta - rightDelta;
            float dominantDelta = math.max(math.abs(leftDelta), math.abs(rightDelta));
            float lateralDirection = deltaDifference >= 0.0f ? -1.0f : 1.0f;

            float targetLateral = math.clamp(math.abs(deltaDifference) * entity.ComShiftLateralFactor * lateralDirection, -entity.MaxComLateral, entity.MaxComLateral);
            float targetForward = math.clamp(dominantDelta * entity.ComShiftForwardFactor, 0.0f, entity.MaxComForward);
            float targetVertical = math.clamp(-dominantDelta * entity.ComShiftVerticalFactor, -entity.MaxComVertical, 0.0f);
            float pitch = math.clamp(dominantDelta * entity.ComLeanPitchRadians, 0.0f, entity.ComLeanPitchRadians);
            float roll = math.clamp(-deltaDifference * entity.ComLeanRollRadians, -entity.ComLeanRollRadians, entity.ComLeanRollRadians);

            next.ComOffsetLocal = ContextualPhysicalIkMath.SmoothVector(
                previous.ComOffsetLocal,
                new float3(targetLateral, targetVertical, targetForward),
                entity.ComResponseSharpness,
                entity.DeltaTime);

            next.ComLeanRadians = new float2(
                ContextualPhysicalIkMath.SmoothScalar(previous.ComLeanRadians.x, pitch, entity.ComResponseSharpness, entity.DeltaTime),
                ContextualPhysicalIkMath.SmoothScalar(previous.ComLeanRadians.y, roll, entity.ComResponseSharpness, entity.DeltaTime));

            NextTargets[index] = next;
        }

        private static void FadeOutTarget(
            ref ContextualPhysicalIkContactTarget target,
            in ContextualPhysicalIkContactTarget previous,
            float fadeSharpness,
            float deltaTime)
        {
            target = previous;
            target.Blend = ContextualPhysicalIkMath.SmoothScalar(previous.Blend, 0.0f, fadeSharpness, deltaTime);
            target.DeltaHeight = 0.0f;
        }

        private static void ApplyPredictiveLatch(
            ref ContextualPhysicalIkContactTarget target,
            in ContextualPhysicalIkContactTarget previous,
            float3 predictivePosition,
            float3 predictiveNormal,
            float predictiveBlend,
            float positionSharpness,
            float normalSharpness,
            float fadeSharpness,
            float deltaTime)
        {
            float targetBlend = math.saturate(predictiveBlend);
            if (targetBlend <= 0.0001f || !math.all(math.isfinite(predictivePosition)))
                return;

            float3 normal = ContextualPhysicalIkMath.SafeNormalize(predictiveNormal, new float3(0.0f, 1.0f, 0.0f));
            float3 currentPosition = target.Blend > 0.0001f ? target.WorldPosition : previous.WorldPosition;
            float3 currentNormal = target.Blend > 0.0001f ? target.WorldNormal : previous.WorldNormal;
            target.WorldPosition = ContextualPhysicalIkMath.SmoothVector(currentPosition, predictivePosition, positionSharpness, deltaTime);
            target.WorldNormal = ContextualPhysicalIkMath.SafeNormalize(
                ContextualPhysicalIkMath.SmoothVector(currentNormal, normal, normalSharpness, deltaTime),
                normal);
            target.Blend = math.max(target.Blend, ContextualPhysicalIkMath.SmoothScalar(previous.Blend, targetBlend, fadeSharpness, deltaTime));
            target.DeltaHeight = 0.0f;
        }

        private static void ResolveContactTarget(
            ref ContextualPhysicalIkContactTarget target,
            in ContextualPhysicalIkContactTarget previous,
            in RaycastHit hit,
            float3 probeOrigin,
            float contactOffset,
            float targetBlend,
            float positionSharpness,
            float normalSharpness,
            float fadeSharpness,
            float maxDeltaHeight,
            float deltaTime)
        {
            if (!HasHit(in hit))
            {
                FadeOutTarget(ref target, in previous, fadeSharpness, deltaTime);
                return;
            }

            float3 normal = ContextualPhysicalIkMath.SafeNormalize(ContextualPhysicalIkMath.ToFloat3(hit.normal), new float3(0.0f, 1.0f, 0.0f));
            float3 point = ContextualPhysicalIkMath.ToFloat3(hit.point) + (normal * contactOffset);

            target.WorldPosition = ContextualPhysicalIkMath.SmoothVector(previous.WorldPosition, point, positionSharpness, deltaTime);
            target.WorldNormal = ContextualPhysicalIkMath.SafeNormalize(
                ContextualPhysicalIkMath.SmoothVector(previous.WorldNormal, normal, normalSharpness, deltaTime),
                normal);
            target.Blend = ContextualPhysicalIkMath.SmoothScalar(previous.Blend, targetBlend, fadeSharpness, deltaTime);
            target.DeltaHeight = math.clamp(point.y - probeOrigin.y, -maxDeltaHeight, maxDeltaHeight);
        }

        private static bool HasHit(in RaycastHit hit)
        {
            return hit.distance > 0.0f || math.lengthsq(ContextualPhysicalIkMath.ToFloat3(hit.normal)) > 0.0001f;
        }

        private static float ResolveBraceProxyTunnelBlend(
            in RaycastHit leftHandHit,
            in RaycastHit rightHandHit,
            in ContextualPhysicalIkEntityState entity)
        {
            float leftBlend = ResolveBraceHitProxyBlend(
                in leftHandHit,
                entity.LeftArmReach,
                entity.HandProbeDistanceScale,
                entity.TunnelClearanceDistance,
                entity.HandBraceFadeDistance);
            float rightBlend = ResolveBraceHitProxyBlend(
                in rightHandHit,
                entity.RightArmReach,
                entity.HandProbeDistanceScale,
                entity.TunnelClearanceDistance,
                entity.HandBraceFadeDistance);
            return math.max(leftBlend, rightBlend);
        }

        private static float ResolveBraceHitProxyBlend(
            in RaycastHit hit,
            float armReach,
            float distanceScale,
            float clearanceDistance,
            float fadeDistance)
        {
            if (!HasHit(in hit))
                return 0.0f;

            float scaledReach = math.max(0.0001f, armReach * math.max(0.0001f, distanceScale));
            float proxyDistance = math.max(0.0001f, math.min(scaledReach, math.max(0.0001f, clearanceDistance)));
            float safeFadeDistance = math.max(0.0001f, fadeDistance);
            return math.saturate((proxyDistance - math.max(0.0f, hit.distance)) / safeFadeDistance);
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9920)]
    internal sealed class ContextualPhysicalIkRuntime : MonoBehaviour, IUpdatable, ILateFrameTickable, IOriginShiftListener
    {
        private const int MaxEntities = 128;
        internal const int RaysPerEntity = 4;
        private const int MinCommandsPerJob = 32;
        private const float CameraResolveRetryInterval = 1.0f;
        private const string NativeMemoryOwner = nameof(ContextualPhysicalIkRuntime);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;

        // COLD ALLOC: ContextualPhysicalIkRig[128] — stable slot owner registry for contextual IK entities — owner: ContextualPhysicalIkRuntime
        private readonly ContextualPhysicalIkRig[] _registeredRigs = new ContextualPhysicalIkRig[MaxEntities];
        // COLD ALLOC: bool[128] — active slot bitset for contextual IK entities — owner: ContextualPhysicalIkRuntime
        private readonly bool[] _slotActive = new bool[MaxEntities];
        // COLD ALLOC: int[128] — free-slot stack for contextual IK stable indexing — owner: ContextualPhysicalIkRuntime
        private readonly int[] _freeSlots = new int[MaxEntities];

        private NativeArray<ContextualPhysicalIkEntityState> _scheduledEntityStates;
        private NativeArray<RaycastCommand> _scheduledCommands;
        private NativeArray<RaycastHit> _scheduledHits;
        private NativeArray<ContextualPhysicalIkTargetFrame> _frontTargetFrames;
        private NativeArray<ContextualPhysicalIkTargetFrame> _backTargetFrames;

        private JobHandle _pendingGroundResponseHandle;
        private JobHandle _disposeHandle;
        private Transform _cameraTransform;
        private bool _groundResponseScheduled;
        private bool _registered;
        private bool _registeredLateFrame;
        private bool _registeredOriginShiftListener;
        private int _freeSlotCount;
        private float _cameraResolveRetryTimer;
        private uint _frameIndex;

        internal NativeArray<ContextualPhysicalIkTargetFrame> CurrentTargetFrames => _frontTargetFrames;

        internal static ContextualPhysicalIkRuntime EnsureRuntimeInstance()
        {
            ContextualPhysicalIkRuntime runtime = GlobalRegistry.ContextualPhysicalIkRuntime;
            if (runtime != null)
                return runtime;

            GameObject runtimeRoot = new GameObject("[ContextualPhysicalIkRuntime]"); // COLD ALLOC: GameObject[1] — persistent contextual IK runtime owner — owner: ContextualPhysicalIkRuntime
            runtime = runtimeRoot.AddComponent<ContextualPhysicalIkRuntime>();
            GlobalRegistry.RegisterContextualPhysicalIkRuntime(runtime);
            return runtime;
        }

        private void Awake()
        {
            ContextualPhysicalIkRuntime runtime = GlobalRegistry.ContextualPhysicalIkRuntime;
            if (runtime != null && !ReferenceEquals(runtime, this))
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterContextualPhysicalIkRuntime(this);
            InitializeFreeSlots();
            EnsurePersistentBuffers();
        }

        private void OnEnable()
        {
            EnsurePersistentBuffers();
            TryRegister();
            TryRegisterOriginShiftListener();
        }

        private void OnDisable()
        {
            TryUnregisterOriginShiftListener();
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregisterOriginShiftListener();
            TryUnregister();
            JobHandle dependency = _groundResponseScheduled ? _pendingGroundResponseHandle : default;
            DisposeBuffers(dependency);
            GlobalRegistry.ClearContextualPhysicalIkRuntime(this);
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            if (shiftOffset.sqrMagnitude <= 0.000001f)
                return;

            CompletePendingGroundResponseForOriginShift();

            float3 offset = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z);
            RebaseScheduledEntityStates(offset);
            RebaseTargetFrames(_frontTargetFrames, offset);
            RebaseTargetFrames(_backTargetFrames, offset);
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            uint frameIndex = _frameIndex;
            _frameIndex++;
            bool hasViewerPosition = TryResolveViewerPosition(deltaTime, out float3 viewerPosition);

            if (_groundResponseScheduled)
                return;

            if (!CaptureEntityStates(deltaTime, frameIndex, viewerPosition, hasViewerPosition))
                return;

            ScheduleGroundPipeline();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (!_groundResponseScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _pendingGroundResponseHandle, forceComplete: false))
                return;

            SwapTargetBuffers();
            PublishFrontTargetBuffer();
            _groundResponseScheduled = false;
        }

        internal bool RegisterRig(ContextualPhysicalIkRig rig, out int slotIndex)
        {
            slotIndex = -1;
            if (rig == null || _freeSlotCount <= 0)
                return false;

            int freeStackIndex = _freeSlotCount - 1;
            slotIndex = _freeSlots[freeStackIndex];
            _freeSlotCount = freeStackIndex;

            _registeredRigs[slotIndex] = rig;
            _slotActive[slotIndex] = true;
            ResetTargetSlot(slotIndex);
            rig.AssignEntitySlot(slotIndex, _frontTargetFrames);
            return true;
        }

        internal void UnregisterRig(ContextualPhysicalIkRig rig, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxEntities)
                return;

            if (!ReferenceEquals(_registeredRigs[slotIndex], rig))
                return;

            _registeredRigs[slotIndex] = null;
            _slotActive[slotIndex] = false;
            ResetTargetSlot(slotIndex);
            _freeSlots[_freeSlotCount] = slotIndex;
            _freeSlotCount++;
        }

        private void InitializeFreeSlots()
        {
            _freeSlotCount = MaxEntities;
            for (int i = 0; i < MaxEntities; i++)
                _freeSlots[i] = i;
        }

        private void EnsurePersistentBuffers()
        {
            if (!_scheduledEntityStates.IsCreated)
            {
                _scheduledEntityStates = new NativeArray<ContextualPhysicalIkEntityState>(
                    MaxEntities,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ContextualPhysicalIkEntityState>[128] — scheduled IK entity snapshots — owner: ContextualPhysicalIkRuntime
                NativeMemorySentinel.RegisterNativeArray(_scheduledEntityStates, NativeMemoryOwner, nameof(_scheduledEntityStates), NativeMemoryLifetime);
            }

            if (!_scheduledCommands.IsCreated)
            {
                _scheduledCommands = new NativeArray<RaycastCommand>(
                    MaxEntities * RaysPerEntity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastCommand>[512] — contextual IK ground/hand probes — owner: ContextualPhysicalIkRuntime
                NativeMemorySentinel.RegisterNativeArray(_scheduledCommands, NativeMemoryOwner, nameof(_scheduledCommands), NativeMemoryLifetime);
            }

            if (!_scheduledHits.IsCreated)
            {
                _scheduledHits = new NativeArray<RaycastHit>(
                    MaxEntities * RaysPerEntity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastHit>[512] — contextual IK raycast results — owner: ContextualPhysicalIkRuntime
                NativeMemorySentinel.RegisterNativeArray(_scheduledHits, NativeMemoryOwner, nameof(_scheduledHits), NativeMemoryLifetime);
            }

            if (!_frontTargetFrames.IsCreated)
            {
                _frontTargetFrames = new NativeArray<ContextualPhysicalIkTargetFrame>(
                    MaxEntities,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ContextualPhysicalIkTargetFrame>[128] — read-side IK target frames — owner: ContextualPhysicalIkRuntime
                NativeMemorySentinel.RegisterNativeArray(_frontTargetFrames, NativeMemoryOwner, nameof(_frontTargetFrames), NativeMemoryLifetime);
            }

            if (!_backTargetFrames.IsCreated)
            {
                _backTargetFrames = new NativeArray<ContextualPhysicalIkTargetFrame>(
                    MaxEntities,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ContextualPhysicalIkTargetFrame>[128] — write-side IK target frames — owner: ContextualPhysicalIkRuntime
                NativeMemorySentinel.RegisterNativeArray(_backTargetFrames, NativeMemoryOwner, nameof(_backTargetFrames), NativeMemoryLifetime);
            }
        }

        private void DisposeBuffers(JobHandle dependency)
        {
            DisposeNativeArray(ref _scheduledEntityStates, dependency);
            DisposeNativeArray(ref _scheduledCommands, dependency);
            DisposeNativeArray(ref _scheduledHits, dependency);
            DisposeNativeArray(ref _frontTargetFrames, dependency);
            DisposeNativeArray(ref _backTargetFrames, dependency);
            DispatcherJobSwap.TryComplete(ref _disposeHandle, forceComplete: true);
            _groundResponseScheduled = false;
            _pendingGroundResponseHandle = default;
        }

        private void DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            _disposeHandle = JobHandle.CombineDependencies(_disposeHandle, array.Dispose(dependency));
            array = default;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.UI);
            _registered = GlobalRegistry.Updatables.Contains(this) ||
                          SystemDispatcher.GetLateFrameLane(PriorityLayer.UI).Contains(this);
            _registeredLateFrame = SystemDispatcher.GetLateFrameLane(PriorityLayer.UI).Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            if (GlobalRegistry.Updatables.Contains(this))
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);

            if (_registeredLateFrame && SystemDispatcher.GetLateFrameLane(PriorityLayer.UI).Contains(this))
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);

            _registered = false;
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

        private void CompletePendingGroundResponseForOriginShift()
        {
            if (!_groundResponseScheduled)
                return;

            // COLD SYNC JOB: floating-origin rebasing must not race pending IK target writes.
            DispatcherJobSwap.TryComplete(ref _pendingGroundResponseHandle, forceComplete: true);
            SwapTargetBuffers();
            PublishFrontTargetBuffer();
            _groundResponseScheduled = false;
        }

        private void RebaseScheduledEntityStates(float3 shiftOffset)
        {
            if (!_scheduledEntityStates.IsCreated)
                return;

            for (int slotIndex = 0; slotIndex < MaxEntities; slotIndex++)
            {
                if (!_slotActive[slotIndex])
                    continue;

                ContextualPhysicalIkEntityState state = _scheduledEntityStates[slotIndex];
                state.RootPosition -= shiftOffset;
                state.PelvisPosition -= shiftOffset;
                state.LeftFootProbeOrigin -= shiftOffset;
                state.RightFootProbeOrigin -= shiftOffset;
                state.LeftHandProbeOrigin -= shiftOffset;
                state.RightHandProbeOrigin -= shiftOffset;
                state.PredictiveLeftHandPosition -= shiftOffset;
                state.PredictiveRightHandPosition -= shiftOffset;
                _scheduledEntityStates[slotIndex] = state;
            }
        }

        private void RebaseTargetFrames(NativeArray<ContextualPhysicalIkTargetFrame> targetFrames, float3 shiftOffset)
        {
            if (!targetFrames.IsCreated)
                return;

            for (int slotIndex = 0; slotIndex < MaxEntities; slotIndex++)
            {
                if (!_slotActive[slotIndex])
                    continue;

                ContextualPhysicalIkTargetFrame frame = targetFrames[slotIndex];
                RebaseContactTarget(ref frame.LeftFoot, shiftOffset);
                RebaseContactTarget(ref frame.RightFoot, shiftOffset);
                RebaseContactTarget(ref frame.LeftHand, shiftOffset);
                RebaseContactTarget(ref frame.RightHand, shiftOffset);
                targetFrames[slotIndex] = frame;
            }
        }

        private static void RebaseContactTarget(ref ContextualPhysicalIkContactTarget target, float3 shiftOffset)
        {
            if (target.Blend <= 0.0001f &&
                math.lengthsq(target.WorldPosition) <= 0.000001f &&
                target.DeltaHeight == 0.0f)
            {
                return;
            }

            target.WorldPosition -= shiftOffset;
        }

        private bool CaptureEntityStates(float deltaTime, uint frameIndex, float3 viewerPosition, bool hasViewerPosition)
        {
            bool hasActiveEntity = false;
            for (int slotIndex = 0; slotIndex < MaxEntities; slotIndex++)
            {
                ContextualPhysicalIkEntityState entityState = default;

                if (_slotActive[slotIndex])
                {
                    ContextualPhysicalIkRig rig = _registeredRigs[slotIndex];
                    if (rig != null && rig.CaptureScheduledState(deltaTime, frameIndex, viewerPosition, hasViewerPosition, ref entityState))
                    {
                        hasActiveEntity = true;
                    }
                }

                _scheduledEntityStates[slotIndex] = entityState;
            }

            return hasActiveEntity;
        }

        private bool TryResolveViewerPosition(float deltaTime, out float3 viewerPosition)
        {
            viewerPosition = float3.zero;
            if (_cameraTransform != null)
            {
                viewerPosition = ContextualPhysicalIkMath.ToFloat3(_cameraTransform.position);
                return true;
            }

            _cameraResolveRetryTimer -= deltaTime;
            if (_cameraResolveRetryTimer > 0.0f)
                return false;

            _cameraResolveRetryTimer = CameraResolveRetryInterval;
            Camera playerCamera = GlobalRegistry.Player != null ? GlobalRegistry.Player.PlayerCamera : null;
            if (playerCamera == null)
                return false;

            _cameraTransform = playerCamera.transform;
            viewerPosition = ContextualPhysicalIkMath.ToFloat3(_cameraTransform.position);
            return true;
        }

        private void ScheduleGroundPipeline()
        {
            ContextualPhysicalIkGroundDetectionJob groundDetectionJob = new ContextualPhysicalIkGroundDetectionJob
            {
                Entities = _scheduledEntityStates,
                Commands = _scheduledCommands,
            };

            JobHandle commandBuildHandle = groundDetectionJob.Schedule(MaxEntities, 32);
            JobHandle raycastHandle = RaycastCommand.ScheduleBatch(
                _scheduledCommands,
                _scheduledHits,
                MinCommandsPerJob,
                commandBuildHandle);
            JobHandle groundDetectionHandle = JobHandle.CombineDependencies(commandBuildHandle, raycastHandle);

            ContextualPhysicalIkGroundResponseJob responseJob = new ContextualPhysicalIkGroundResponseJob
            {
                Entities = _scheduledEntityStates,
                Hits = _scheduledHits,
                PreviousTargets = _frontTargetFrames,
                NextTargets = _backTargetFrames,
            };

            JobHandle responseHandle = responseJob.Schedule(MaxEntities, 32, groundDetectionHandle);
            _pendingGroundResponseHandle = JobHandle.CombineDependencies(groundDetectionHandle, responseHandle);
            _groundResponseScheduled = true;
        }

        private void SwapTargetBuffers()
        {
            NativeArray<ContextualPhysicalIkTargetFrame> swapBuffer = _frontTargetFrames;
            _frontTargetFrames = _backTargetFrames;
            _backTargetFrames = swapBuffer;
        }

        private void PublishFrontTargetBuffer()
        {
            for (int slotIndex = 0; slotIndex < MaxEntities; slotIndex++)
            {
                if (!_slotActive[slotIndex])
                    continue;

                ContextualPhysicalIkRig rig = _registeredRigs[slotIndex];
                if (rig == null)
                    continue;

                rig.OnTargetBufferSwapped(_frontTargetFrames);
            }
        }

        private void ResetTargetSlot(int slotIndex)
        {
            if (_frontTargetFrames.IsCreated)
                _frontTargetFrames[slotIndex] = default;

            if (_backTargetFrames.IsCreated)
                _backTargetFrames[slotIndex] = default;
        }
    }
}
