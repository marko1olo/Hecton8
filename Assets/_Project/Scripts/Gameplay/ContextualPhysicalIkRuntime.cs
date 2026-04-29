using Hecton8.Core;
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
        public float ViewerDistance;
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
        public float3 ClearanceProbeOrigin;
        public float LeftLegReach;
        public float RightLegReach;
        public float LeftArmReach;
        public float RightArmReach;
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
        public float ViewerDistance;
        public uint UpdateBitfield;
        public byte ThrottleTier;
    }

    [BurstCompile(FloatPrecision = FloatPrecision.Standard, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
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
                WriteDisabledCommand(baseCommandIndex + 4);
                return;
            }

            QueryParameters groundQuery = new QueryParameters(entity.GroundLayerMask, false, QueryTriggerInteraction.Ignore, false);
            QueryParameters wallQuery = new QueryParameters(entity.WallLayerMask, false, QueryTriggerInteraction.Ignore, false);

            float leftFootDistance = entity.EnableFootPlacement != 0 ? math.max(0.0f, entity.LeftLegReach * entity.FootProbeDistanceScale) : 0.0f;
            float rightFootDistance = entity.EnableFootPlacement != 0 ? math.max(0.0f, entity.RightLegReach * entity.FootProbeDistanceScale) : 0.0f;
            float leftHandDistance = entity.EnableHandBracing != 0 ? math.max(0.0f, entity.LeftArmReach * entity.HandProbeDistanceScale) : 0.0f;
            float rightHandDistance = entity.EnableHandBracing != 0 ? math.max(0.0f, entity.RightArmReach * entity.HandProbeDistanceScale) : 0.0f;
            float clearanceDistance = entity.EnableHandBracing != 0 ? math.max(0.0f, entity.TunnelClearanceDistance) : 0.0f;

            float3 forward = math.mul(entity.RootRotation, new float3(0.0f, 0.0f, 1.0f));
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

            Commands[baseCommandIndex + 2] = new RaycastCommand(
                ContextualPhysicalIkMath.ToUnityVector3(entity.LeftHandProbeOrigin),
                ContextualPhysicalIkMath.ToUnityVector3(leftBraceDirection),
                wallQuery,
                leftHandDistance);

            Commands[baseCommandIndex + 3] = new RaycastCommand(
                ContextualPhysicalIkMath.ToUnityVector3(entity.RightHandProbeOrigin),
                ContextualPhysicalIkMath.ToUnityVector3(rightBraceDirection),
                wallQuery,
                rightHandDistance);

            Commands[baseCommandIndex + 4] = new RaycastCommand(
                ContextualPhysicalIkMath.ToUnityVector3(entity.ClearanceProbeOrigin),
                ContextualPhysicalIkMath.ToUnityVector3(ContextualPhysicalIkMath.SafeNormalize(forward, new float3(0.0f, 0.0f, 1.0f))),
                wallQuery,
                clearanceDistance);
        }

        private void WriteDisabledCommand(int commandIndex)
        {
            Commands[commandIndex] = new RaycastCommand(
                Vector3.zero,
                Vector3.down,
                new QueryParameters(0, false, QueryTriggerInteraction.Ignore, false),
                0.0f);
        }
    }

    [BurstCompile(FloatPrecision = FloatPrecision.Standard, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
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
            next.ViewerDistance = entity.ViewerDistance;
            next.UpdateBitfield = entity.UpdateBitfield;
            next.ThrottleTier = entity.ThrottleTier;
            next.ShouldComputeThisFrame = entity.UpdateThisFrame != 0 ? (byte)1 : (byte)0;
            int baseHitIndex = index * ContextualPhysicalIkRuntime.RaysPerEntity;
            RaycastHit leftFootHit = Hits[baseHitIndex + 0];
            RaycastHit rightFootHit = Hits[baseHitIndex + 1];
            RaycastHit leftHandHit = Hits[baseHitIndex + 2];
            RaycastHit rightHandHit = Hits[baseHitIndex + 3];
            RaycastHit clearanceHit = Hits[baseHitIndex + 4];

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

            bool tunnelDetected = entity.EnableHandBracing != 0 &&
                                  HasHit(in clearanceHit) &&
                                  clearanceHit.distance <= entity.TunnelClearanceDistance;

            float tunnelTargetBlend = 0.0f;
            if (tunnelDetected)
            {
                float safeFadeDistance = math.max(0.0001f, entity.HandBraceFadeDistance);
                tunnelTargetBlend = math.saturate((entity.TunnelClearanceDistance - math.max(0.0f, clearanceHit.distance)) / safeFadeDistance);
            }

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
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9920)]
    internal sealed class ContextualPhysicalIkRuntime : MonoBehaviour, IUpdatable, IOriginShiftListener
    {
        private const int MaxEntities = 128;
        internal const int RaysPerEntity = 5;
        private const int MinCommandsPerJob = 32;
        private const float CameraResolveRetryInterval = 1.0f;

        private static ContextualPhysicalIkRuntime _instance;

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
        private Transform _cameraTransform;
        private bool _groundResponseScheduled;
        private bool _registered;
        private bool _registeredOriginShiftListener;
        private int _freeSlotCount;
        private float _cameraResolveRetryTimer;
        private uint _frameIndex;

        internal static ContextualPhysicalIkRuntime Instance => _instance;

        internal NativeArray<ContextualPhysicalIkTargetFrame> CurrentTargetFrames => _frontTargetFrames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        internal static ContextualPhysicalIkRuntime EnsureRuntimeInstance()
        {
            if (_instance != null)
                return _instance;

            GameObject runtimeRoot = new GameObject("[ContextualPhysicalIkRuntime]"); // COLD ALLOC: GameObject[1] — persistent contextual IK runtime owner — owner: ContextualPhysicalIkRuntime
            return runtimeRoot.AddComponent<ContextualPhysicalIkRuntime>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (Application.isPlaying)
            {
                if (transform.parent != null)
                    transform.SetParent(null, true);

                DontDestroyOnLoad(gameObject);
            }

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

            if (_instance == this)
                _instance = null;
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

            if (_groundResponseScheduled && _pendingGroundResponseHandle.IsCompleted)
            {
                SwapTargetBuffers();
                PublishFrontTargetBuffer();
                _groundResponseScheduled = false;
            }

            if (_groundResponseScheduled)
                return;

            if (!CaptureEntityStates(deltaTime, frameIndex, viewerPosition, hasViewerPosition))
                return;

            ScheduleGroundPipeline();
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
            }

            if (!_scheduledCommands.IsCreated)
            {
                _scheduledCommands = new NativeArray<RaycastCommand>(
                    MaxEntities * RaysPerEntity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastCommand>[640] — contextual IK ground/tunnel probes — owner: ContextualPhysicalIkRuntime
            }

            if (!_scheduledHits.IsCreated)
            {
                _scheduledHits = new NativeArray<RaycastHit>(
                    MaxEntities * RaysPerEntity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastHit>[640] — contextual IK raycast results — owner: ContextualPhysicalIkRuntime
            }

            if (!_frontTargetFrames.IsCreated)
            {
                _frontTargetFrames = new NativeArray<ContextualPhysicalIkTargetFrame>(
                    MaxEntities,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ContextualPhysicalIkTargetFrame>[128] — read-side IK target frames — owner: ContextualPhysicalIkRuntime
            }

            if (!_backTargetFrames.IsCreated)
            {
                _backTargetFrames = new NativeArray<ContextualPhysicalIkTargetFrame>(
                    MaxEntities,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ContextualPhysicalIkTargetFrame>[128] — write-side IK target frames — owner: ContextualPhysicalIkRuntime
            }
        }

        private void DisposeBuffers(JobHandle dependency)
        {
            DisposeNativeArray(ref _scheduledEntityStates, dependency);
            DisposeNativeArray(ref _scheduledCommands, dependency);
            DisposeNativeArray(ref _scheduledHits, dependency);
            DisposeNativeArray(ref _frontTargetFrames, dependency);
            DisposeNativeArray(ref _backTargetFrames, dependency);
            _groundResponseScheduled = false;
            _pendingGroundResponseHandle = default;
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency) where T : struct
        {
            if (!array.IsCreated)
                return;

            array.Dispose(dependency);
            array = default;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
        }

        private void TryRegisterOriginShiftListener()
        {
            if (_registeredOriginShiftListener)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _registeredOriginShiftListener = true;
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
            _pendingGroundResponseHandle.Complete();
            SwapTargetBuffers();
            PublishFrontTargetBuffer();
            _groundResponseScheduled = false;
            _pendingGroundResponseHandle = default;
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
                state.ClearanceProbeOrigin -= shiftOffset;
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
