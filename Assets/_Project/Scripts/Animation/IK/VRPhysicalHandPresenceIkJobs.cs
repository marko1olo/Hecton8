using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Animation.IK
{
    /// <summary>
    /// Fixed constants for the VR physical hand presence lanes.
    /// </summary>
    public static class VRPhysicalHandPresenceConstants
    {
        /// <summary>Left and right hand lanes are solved as a fixed two-lane packet.</summary>
        public const int HandCount = 2;

        /// <summary>Left-hand SOA lane.</summary>
        public const int LeftHandIndex = 0;

        /// <summary>Right-hand SOA lane.</summary>
        public const int RightHandIndex = 1;

        /// <summary>Black-box frame history capacity.</summary>
        public const int TelemetryCapacity = 300;

        public const uint RuntimeFlagVrActive = 1u << 0;
        public const uint RuntimeFlagGrip = 1u << 1;
        public const uint RuntimeFlagSurfacePlane = 1u << 2;
        public const uint RuntimeFlagSdfProjection = 1u << 3;
        public const uint RuntimeFlagLowTier = 1u << 4;
        public const uint RuntimeFlagHighTier = 1u << 5;
        public const uint RuntimeFlagInteractableAupValid = 1u << 6;
        public const uint RuntimeFlagLeftHand = 1u << 7;

        public const byte AupFlagValid = 1 << 0;
        public const byte AupFlagShiftRebased = 1 << 1;
        public const byte AupFlagInteractable = 1 << 2;

        public const byte GrabStateFree = 0;
        public const byte GrabStateLocked = 1;
        public const byte GrabStateSliding = 2;
        public const byte GrabStateGhosted = 3;

        public const uint OutputFlagScreenSpaceFallback = 1u << 0;
        public const uint OutputFlagLocked = 1u << 1;
        public const uint OutputFlagSurfaceSlide = 1u << 2;
        public const uint OutputFlagSdfSlide = 1u << 3;
        public const uint OutputFlagHapticScrape = 1u << 4;
        public const uint OutputFlagGhostHand = 1u << 5;
        public const uint OutputFlagNanFallback = 1u << 6;
        public const uint OutputFlagJointLimited = 1u << 7;
        public const uint OutputFlagGripInput = 1u << 8;

        public const uint TelemetryMarkerIKLockState = 0x494B4C53u;
    }

    /// <summary>
    /// Compact AUP hand pose used by DataVault hand target and actual lanes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VRHandAupPose
    {
        public long GridX;
        public long GridY;
        public long GridZ;
        public float3 LocalMeters;
        public uint ShiftFrameId;
        public uint SourceHash;
        public byte Flags;
        public byte HandIndex;
        public ushort Reserved;
    }

    /// <summary>
    /// Persistent per-hand grab state stored in the global vault.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VRHandGrabState
    {
        public float3 TargetPosition;
        public float3 ActualPosition;
        public float3 ControllerPosition;
        public float3 SurfaceNormal;
        public float Grip01;
        public float LockBlend01;
        public float SlidingSpeed;
        public byte State;
        public byte Flags;
        public ushort InteractableId;
        public uint FrameIndex;
        public uint StateHash;
    }

    /// <summary>
    /// Blittable bridge payload for UniversalInputStateSignal grip and interactable AUP data.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VRHandPresenceInput
    {
        public float3 ShoulderPosition;
        public float3 CurrentElbowPosition;
        public float3 ControllerPosition;
        public float3 PreviousActualPosition;
        public float3 PolePosition;
        public float3 ControllerForward;
        public float3 ControllerUp;
        public float3 SurfacePoint;
        public float3 SurfaceNormal;
        public VRHandAupPose InteractableAUP;
        public quaternion CurrentUpperRotation;
        public quaternion CurrentLowerRotation;
        public quaternion CurrentHandRotation;
        public quaternion ControllerRotation;
        public float UpperArmLength;
        public float LowerArmLength;
        public float Grip01;
        public float TargetBlend01;
        public float HandClearance;
        public float ScrapeVelocityThreshold;
        public uint RuntimeFlags;
        public uint UniversalInputFlags;
        public uint GripInputMask;
        public ushort InteractableId;
        public byte HandIndex;
        public byte Reserved;
    }

    /// <summary>
    /// Solved hand presence output consumed by animation rig binding code.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VRHandPresenceOutput
    {
        public float3 ActualHandPosition;
        public float3 GhostHandPosition;
        public float3 ElbowPosition;
        public float3 SurfaceNormal;
        public quaternion UpperArmRotation;
        public quaternion LowerArmRotation;
        public quaternion HandRotation;
        public float LockBlend01;
        public float HapticIntensity;
        public float SlidingSpeed;
        public uint Flags;
        public uint StateHash;
    }

    /// <summary>
    /// Fixed-size black-box record for hand IK lock state, hashes, and guard flags.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VRHandIkTelemetryEntry
    {
        public uint FrameIndex;
        public uint StateHash;
        public uint Flags;
        public ushort InteractableId;
        public byte HandIndex;
        public byte GrabState;
        public byte IKLockState;
        public ushort Reserved;
        public float3 TargetPosition;
        public float3 ActualPosition;
        public float3 ControllerPosition;
        public float3 SurfaceNormal;
        public float LockBlend01;
        public float SlidingSpeed;
        public float ControllerSeparation;
    }

    /// <summary>
    /// Cold-path DataVault resolver for the fixed hand presence lanes.
    /// </summary>
    public static class VRPhysicalHandPresenceVault
    {
        /// <summary>
        /// Resolves all NativeArray lanes needed by <see cref="VRPhysicalHandPresenceJob"/>.
        /// </summary>
        public static bool TryResolveBuffers(
            IDataVault vault,
            out NativeArray<VRHandPresenceInput> inputs,
            out NativeArray<VRHandPresenceOutput> outputs,
            out NativeArray<VRHandAupPose> handTargetAup,
            out NativeArray<VRHandAupPose> handActualAup,
            out NativeArray<VRHandGrabState> grabStates,
            out NativeArray<VRHandIkTelemetryEntry> telemetryRing,
            out NativeArray<int> telemetryCursor)
        {
            inputs = default;
            outputs = default;
            handTargetAup = default;
            handActualAup = default;
            grabStates = default;
            telemetryRing = default;
            telemetryCursor = default;

            if (vault == null)
                return false;

            inputs = vault.GetBuffer<VRHandPresenceInput>(
                BufferID.HandPresenceInput,
                VRPhysicalHandPresenceConstants.HandCount,
                SystemID.GameplayPlayer);
            outputs = vault.GetBuffer<VRHandPresenceOutput>(
                BufferID.HandPresenceOutput,
                VRPhysicalHandPresenceConstants.HandCount,
                SystemID.GameplayPlayer);
            handTargetAup = vault.GetBuffer<VRHandAupPose>(
                BufferID.HandTargetAUP,
                VRPhysicalHandPresenceConstants.HandCount,
                SystemID.GameplayPlayer);
            handActualAup = vault.GetBuffer<VRHandAupPose>(
                BufferID.HandActualAUP,
                VRPhysicalHandPresenceConstants.HandCount,
                SystemID.GameplayPlayer);
            grabStates = vault.GetBuffer<VRHandGrabState>(
                BufferID.HandGrabState,
                VRPhysicalHandPresenceConstants.HandCount,
                SystemID.GameplayPlayer);
            telemetryRing = vault.GetBuffer<VRHandIkTelemetryEntry>(
                BufferID.HandIkTelemetryRing,
                VRPhysicalHandPresenceConstants.TelemetryCapacity,
                SystemID.GameplayPlayer);
            telemetryCursor = vault.GetBuffer<int>(
                BufferID.HandIkTelemetryCursor,
                1,
                SystemID.GameplayPlayer);

            return inputs.IsCreated &&
                   outputs.IsCreated &&
                   handTargetAup.IsCreated &&
                   handActualAup.IsCreated &&
                   grabStates.IsCreated &&
                   telemetryRing.IsCreated &&
                   telemetryCursor.IsCreated &&
                   inputs.Length >= VRPhysicalHandPresenceConstants.HandCount &&
                   outputs.Length >= VRPhysicalHandPresenceConstants.HandCount &&
                   handTargetAup.Length >= VRPhysicalHandPresenceConstants.HandCount &&
                   handActualAup.Length >= VRPhysicalHandPresenceConstants.HandCount &&
                   grabStates.Length >= VRPhysicalHandPresenceConstants.HandCount &&
                   telemetryRing.Length >= VRPhysicalHandPresenceConstants.TelemetryCapacity &&
                   telemetryCursor.Length >= 1;
        }
    }

    /// <summary>
    /// Cold-path serializer for the 300-frame hand IK black-box ring.
    /// </summary>
    public static class VRPhysicalHandPresenceBlackBox
    {
        /// <summary>Default crash dump path required by the batch protocol.</summary>
        public const string DefaultDumpPath = "Docs/AgentLogs/Dump_GRAB_IK_PROJECTION.bin";

        /// <summary>
        /// Writes hand IK telemetry to disk after a NaN/crash signal. Never call from a Burst job or hot frame path.
        /// </summary>
        public static bool TryDumpTelemetry(
            string path,
            NativeArray<VRHandIkTelemetryEntry> telemetryRing,
            NativeArray<int> telemetryCursor)
        {
            if (string.IsNullOrEmpty(path) || !telemetryRing.IsCreated || telemetryRing.Length <= 0)
                return false;

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(0x4752494Bu);
                    writer.Write(VRPhysicalHandPresenceConstants.TelemetryMarkerIKLockState);
                    writer.Write(telemetryRing.Length);
                    writer.Write(telemetryCursor.IsCreated && telemetryCursor.Length > 0 ? telemetryCursor[0] : 0);
                    for (int i = 0; i < telemetryRing.Length; i++)
                        WriteEntry(writer, telemetryRing[i]);
                }

                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static void WriteEntry(BinaryWriter writer, VRHandIkTelemetryEntry entry)
        {
            writer.Write(entry.FrameIndex);
            writer.Write(entry.StateHash);
            writer.Write(entry.Flags);
            writer.Write(entry.InteractableId);
            writer.Write(entry.HandIndex);
            writer.Write(entry.GrabState);
            writer.Write(entry.IKLockState);
            writer.Write(entry.Reserved);
            WriteFloat3(writer, entry.TargetPosition);
            WriteFloat3(writer, entry.ActualPosition);
            WriteFloat3(writer, entry.ControllerPosition);
            WriteFloat3(writer, entry.SurfaceNormal);
            writer.Write(entry.LockBlend01);
            writer.Write(entry.SlidingSpeed);
            writer.Write(entry.ControllerSeparation);
        }

        private static void WriteFloat3(BinaryWriter writer, float3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }
    }

    /// <summary>
    /// Burst hand presence job: input controller pose in, projected physical hand and two-bone arm pose out.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    public struct VRPhysicalHandPresenceJob : IJob
    {
        private const float MinLengthSq = 0.000001f;
        private const float DefaultUpperArmLength = 0.34f;
        private const float DefaultLowerArmLength = 0.36f;
        private const float DefaultClearance = 0.015f;
        private const float DefaultGhostDistance = 0.3f;
        private const float DefaultScrapeVelocityThreshold = 0.08f;
        private const float DefaultLockBlendSharpness = 18f;
        private const float InvEncodedByteMax = 0.0039215686274509803f;

        [ReadOnly] public NativeArray<VRHandPresenceInput> Inputs;
        [ReadOnly] public NativeArray<byte> EncodedSdf;
        public NativeArray<VRHandPresenceOutput> Outputs;
        public NativeArray<VRHandAupPose> HandTargetAUP;
        public NativeArray<VRHandAupPose> HandActualAUP;
        public NativeArray<VRHandGrabState> GrabStates;
        public NativeArray<VRHandIkTelemetryEntry> TelemetryRing;
        public NativeArray<int> TelemetryCursor;
        public int3 SdfDimensions;
        public float3 SdfOrigin;
        public float3 SdfCellSize;
        public float SdfRange;
        public float3 AupShiftMeters;
        public float DeltaTime;
        public float SurfaceClearance;
        public float GhostDistance;
        public float ScrapeVelocityThreshold;
        public float LockBlendSharpness;
        public uint ShiftFrameId;
        public uint FrameIndex;

        /// <inheritdoc />
        public void Execute()
        {
            if (!Inputs.IsCreated ||
                !Outputs.IsCreated ||
                !HandTargetAUP.IsCreated ||
                !HandActualAUP.IsCreated ||
                !GrabStates.IsCreated)
            {
                return;
            }

            int laneCount = math.min(
                VRPhysicalHandPresenceConstants.HandCount,
                math.min(Inputs.Length, math.min(Outputs.Length, math.min(HandTargetAUP.Length, math.min(HandActualAUP.Length, GrabStates.Length)))));
            if (laneCount <= 0)
                return;

            RebaseAupLaneRange(laneCount);

            float dt = SanitizeFiniteClamp(DeltaTime, 1f / 90f, 0.001f, 0.05f);
            float clearance = SanitizePositiveFinite(SurfaceClearance, DefaultClearance, 0f);
            float ghostDistance = SanitizePositiveFinite(GhostDistance, DefaultGhostDistance, 0.05f);
            float scrapeThreshold = SanitizePositiveFinite(ScrapeVelocityThreshold, DefaultScrapeVelocityThreshold, 0.001f);
            float blendSharpness = SanitizePositiveFinite(LockBlendSharpness, DefaultLockBlendSharpness, 0.001f);

            bool canUseSdf =
                EncodedSdf.IsCreated &&
                TryResolveSdfVoxelCount(SdfDimensions, out int expectedSdfLength) &&
                EncodedSdf.Length >= expectedSdfLength &&
                math.all(math.isfinite(SdfOrigin)) &&
                math.all(math.isfinite(SdfCellSize)) &&
                math.all(SdfCellSize > new float3(0.0001f)) &&
                SdfRange > 0.0001f;
            float3 invCellSize = canUseSdf ? math.rcp(SdfCellSize) : float3.zero;
            float3 gradientStep = canUseSdf ? math.max(SdfCellSize, new float3(0.025f)) : new float3(0.025f);

            for (int hand = 0; hand < laneCount; hand++)
                ProcessHand(hand, dt, clearance, ghostDistance, scrapeThreshold, blendSharpness, canUseSdf, invCellSize, gradientStep);
        }

        private void ProcessHand(
            int hand,
            float dt,
            float clearance,
            float ghostDistance,
            float scrapeThreshold,
            float blendSharpness,
            bool canUseSdf,
            float3 invCellSize,
            float3 gradientStep)
        {
            VRHandPresenceInput input = Inputs[hand];
            VRHandGrabState previousState = GrabStates[hand];
            uint flags = 0u;

            bool vrActive = (input.RuntimeFlags & VRPhysicalHandPresenceConstants.RuntimeFlagVrActive) != 0u;
            bool lowTier = (input.RuntimeFlags & VRPhysicalHandPresenceConstants.RuntimeFlagLowTier) != 0u;
            bool gripInput = input.Grip01 > 0.5f ||
                             (input.RuntimeFlags & VRPhysicalHandPresenceConstants.RuntimeFlagGrip) != 0u ||
                             (input.GripInputMask != 0u && (input.UniversalInputFlags & input.GripInputMask) != 0u);
            if (gripInput)
                flags |= VRPhysicalHandPresenceConstants.OutputFlagGripInput;

            float3 controller = SanitizeFinite(input.ControllerPosition, previousState.ControllerPosition);
            float3 previousActual = SanitizeFinite(previousState.ActualPosition, input.PreviousActualPosition);
            if (!math.all(math.isfinite(previousActual)))
                previousActual = controller;

            if (!vrActive || lowTier)
            {
                flags |= VRPhysicalHandPresenceConstants.OutputFlagScreenSpaceFallback;
                float3 ghostPosition = controller;
                float3 shoulder = SanitizeFinite(input.ShoulderPosition, controller + new float3(0f, -0.25f, -0.2f));
                float3 pole = SanitizeFinite(input.PolePosition, shoulder + ResolvePerpendicular(controller - shoulder));
                float3 elbow = math.lerp(shoulder, controller, 0.5f) + NormalizeSafe(pole - shoulder, new float3(0f, 0f, 1f)) * 0.08f;
                quaternion handRotation = SanitizeQuaternion(input.ControllerRotation, SanitizeQuaternion(input.CurrentHandRotation, quaternion.identity));

                VRHandPresenceOutput fallbackOutput = new VRHandPresenceOutput
                {
                    ActualHandPosition = controller,
                    GhostHandPosition = ghostPosition,
                    ElbowPosition = SanitizeFinite(elbow, controller),
                    SurfaceNormal = new float3(0f, 1f, 0f),
                    UpperArmRotation = SanitizeQuaternion(input.CurrentUpperRotation, quaternion.identity),
                    LowerArmRotation = SanitizeQuaternion(input.CurrentLowerRotation, quaternion.identity),
                    HandRotation = handRotation,
                    LockBlend01 = 0f,
                    HapticIntensity = 0f,
                    SlidingSpeed = 0f,
                    Flags = flags,
                    StateHash = ComposeStateHash(controller, controller, ghostPosition, flags, hand)
                };
                Outputs[hand] = fallbackOutput;
                UpdatePersistentState(hand, in input, in fallbackOutput, controller, controller, new float3(0f, 1f, 0f), 0f, VRPhysicalHandPresenceConstants.GrabStateFree);
                WriteTelemetry(hand, in input, in fallbackOutput, VRPhysicalHandPresenceConstants.GrabStateFree);
                return;
            }

            float3 shoulderPosition = SanitizeFinite(input.ShoulderPosition, controller + new float3(0f, -0.25f, -0.2f));
            float3 polePosition = SanitizeFinite(input.PolePosition, input.CurrentElbowPosition);
            float3 surfaceNormal = NormalizeSafe(input.SurfaceNormal, ResolvePerpendicular(controller - shoulderPosition));
            float3 projectedTarget = controller;
            bool locked = false;
            bool usedSdf = false;
            bool usedPlane = false;

            float localClearance = SanitizePositiveFinite(input.HandClearance, clearance, 0f);
            bool allowSdfProjection = (input.RuntimeFlags & (VRPhysicalHandPresenceConstants.RuntimeFlagSdfProjection | VRPhysicalHandPresenceConstants.RuntimeFlagHighTier)) != 0u;
            if (gripInput && canUseSdf &&
                allowSdfProjection &&
                TrySampleSdfTrilinear(controller, invCellSize, SdfRange, out float density) &&
                density > -localClearance &&
                TryResolveSdfGradient(controller, invCellSize, SdfRange, gradientStep, out float3 sdfNormal))
            {
                surfaceNormal = sdfNormal;
                projectedTarget = SanitizeFinite(controller + surfaceNormal * (density + localClearance), controller);
                locked = true;
                usedSdf = true;
                flags |= VRPhysicalHandPresenceConstants.OutputFlagLocked | VRPhysicalHandPresenceConstants.OutputFlagSdfSlide;
            }
            else if (gripInput &&
                     (input.RuntimeFlags & VRPhysicalHandPresenceConstants.RuntimeFlagSurfacePlane) != 0u &&
                     math.all(math.isfinite(input.SurfacePoint)))
            {
                float signedDistance = math.dot(controller - input.SurfacePoint, surfaceNormal);
                if (signedDistance < localClearance)
                {
                    projectedTarget = SanitizeFinite(controller + surfaceNormal * (localClearance - signedDistance), controller);
                    locked = true;
                    usedPlane = true;
                    flags |= VRPhysicalHandPresenceConstants.OutputFlagLocked | VRPhysicalHandPresenceConstants.OutputFlagSurfaceSlide;
                }
            }

            float3 tangentDelta = ProjectOnPlane(projectedTarget - previousActual, surfaceNormal);
            float slidingSpeed = locked ? ResolveLength(tangentDelta) / dt : 0f;
            float inputScrapeThreshold = SanitizePositiveFinite(input.ScrapeVelocityThreshold, scrapeThreshold, 0.001f);
            float hapticIntensity = 0f;
            if ((usedSdf || usedPlane) && slidingSpeed > inputScrapeThreshold)
            {
                hapticIntensity = math.saturate((slidingSpeed - inputScrapeThreshold) * math.rcp(math.max(inputScrapeThreshold * 4f, 0.001f)));
                flags |= VRPhysicalHandPresenceConstants.OutputFlagHapticScrape;
            }

            float alpha = FastBlendAlpha(blendSharpness, dt);
            float previousBlend = math.saturate(previousState.LockBlend01);
            float targetBlend = locked ? 1f : 0f;
            float lockBlend = math.lerp(previousBlend, targetBlend, alpha);
            float bridgeBlend = math.saturate(SanitizeFinite(input.TargetBlend01, 1f));
            lockBlend *= bridgeBlend;

            float3 desiredPhysical = math.lerp(controller, projectedTarget, lockBlend);
            float3 actual = SanitizeFinite(math.lerp(previousActual, desiredPhysical, alpha), controller);
            float separation = ResolveLength(controller - actual);
            float3 ghostPosition = controller;
            byte grabState = locked ? VRPhysicalHandPresenceConstants.GrabStateLocked : VRPhysicalHandPresenceConstants.GrabStateFree;
            if (locked && separation > ghostDistance)
            {
                flags |= VRPhysicalHandPresenceConstants.OutputFlagGhostHand;
                grabState = VRPhysicalHandPresenceConstants.GrabStateGhosted;
            }
            else if (usedSdf || usedPlane)
            {
                grabState = VRPhysicalHandPresenceConstants.GrabStateSliding;
            }

            float upperArm = SanitizePositiveFinite(input.UpperArmLength, DefaultUpperArmLength, 0.05f);
            float lowerArm = SanitizePositiveFinite(input.LowerArmLength, DefaultLowerArmLength, 0.05f);
            SolveTwoBone(
                shoulderPosition,
                actual,
                polePosition,
                upperArm,
                lowerArm,
                out float3 elbowPosition,
                out bool jointLimited);
            if (jointLimited)
                flags |= VRPhysicalHandPresenceConstants.OutputFlagJointLimited;

            quaternion desiredUpper = BuildLimbRotation(shoulderPosition, elbowPosition, polePosition, input.CurrentUpperRotation);
            quaternion desiredLower = BuildLimbRotation(elbowPosition, actual, polePosition, input.CurrentLowerRotation);
            quaternion desiredHand = BuildHandRotation(in input, surfaceNormal, locked);

            quaternion upperRotation = FastNlerp(input.CurrentUpperRotation, desiredUpper, alpha);
            quaternion lowerRotation = FastNlerp(input.CurrentLowerRotation, desiredLower, alpha);
            quaternion handRotation = FastNlerp(input.CurrentHandRotation, desiredHand, alpha);

            VRHandPresenceOutput output = new VRHandPresenceOutput
            {
                ActualHandPosition = actual,
                GhostHandPosition = ghostPosition,
                ElbowPosition = elbowPosition,
                SurfaceNormal = surfaceNormal,
                UpperArmRotation = upperRotation,
                LowerArmRotation = lowerRotation,
                HandRotation = handRotation,
                LockBlend01 = lockBlend,
                HapticIntensity = hapticIntensity,
                SlidingSpeed = slidingSpeed,
                Flags = flags,
                StateHash = ComposeStateHash(projectedTarget, actual, ghostPosition, flags, hand)
            };

            if (!IsValidOutput(in output))
            {
                flags |= VRPhysicalHandPresenceConstants.OutputFlagNanFallback;
                output = BuildNanFallback(in input, previousState, hand, flags);
                projectedTarget = output.ActualHandPosition;
                controller = output.GhostHandPosition;
                surfaceNormal = output.SurfaceNormal;
                slidingSpeed = 0f;
                grabState = VRPhysicalHandPresenceConstants.GrabStateFree;
            }

            Outputs[hand] = output;
            UpdatePersistentState(hand, in input, in output, projectedTarget, controller, surfaceNormal, slidingSpeed, grabState);
            WriteTelemetry(hand, in input, in output, grabState);
        }

        private void RebaseAupLaneRange(int laneCount)
        {
            if (ShiftFrameId == 0u || !math.all(math.isfinite(AupShiftMeters)))
                return;

            for (int hand = 0; hand < laneCount; hand++)
            {
                VRHandAupPose target = HandTargetAUP[hand];
                VRHandAupPose actual = HandActualAUP[hand];
                RebasePose(ref target);
                RebasePose(ref actual);
                HandTargetAUP[hand] = target;
                HandActualAUP[hand] = actual;
            }
        }

        private void RebasePose(ref VRHandAupPose pose)
        {
            if ((pose.Flags & VRPhysicalHandPresenceConstants.AupFlagValid) == 0 ||
                pose.ShiftFrameId == ShiftFrameId)
            {
                return;
            }

            pose.LocalMeters = SanitizeFinite(pose.LocalMeters - AupShiftMeters, pose.LocalMeters);
            pose.ShiftFrameId = ShiftFrameId;
            pose.Flags |= VRPhysicalHandPresenceConstants.AupFlagShiftRebased;
        }

        private void UpdatePersistentState(
            int hand,
            in VRHandPresenceInput input,
            in VRHandPresenceOutput output,
            float3 projectedTarget,
            float3 controller,
            float3 surfaceNormal,
            float slidingSpeed,
            byte grabState)
        {
            bool hasInteractableAup = (input.RuntimeFlags & VRPhysicalHandPresenceConstants.RuntimeFlagInteractableAupValid) != 0u;
            VRHandAupPose targetAup = hasInteractableAup ? input.InteractableAUP : HandTargetAUP[hand];
            targetAup.LocalMeters = SanitizeFinite(hasInteractableAup ? targetAup.LocalMeters : projectedTarget, projectedTarget);
            targetAup.ShiftFrameId = ShiftFrameId;
            targetAup.SourceHash = ComposeStateHash(projectedTarget, output.ActualHandPosition, controller, output.Flags, hand);
            targetAup.Flags = (byte)(VRPhysicalHandPresenceConstants.AupFlagValid | (hasInteractableAup ? VRPhysicalHandPresenceConstants.AupFlagInteractable : 0));
            targetAup.HandIndex = (byte)hand;

            VRHandAupPose actualAup = HandActualAUP[hand];
            actualAup.LocalMeters = SanitizeFinite(output.ActualHandPosition, projectedTarget);
            actualAup.ShiftFrameId = ShiftFrameId;
            actualAup.SourceHash = output.StateHash;
            actualAup.Flags = VRPhysicalHandPresenceConstants.AupFlagValid;
            actualAup.HandIndex = (byte)hand;

            HandTargetAUP[hand] = targetAup;
            HandActualAUP[hand] = actualAup;
            GrabStates[hand] = new VRHandGrabState
            {
                TargetPosition = SanitizeFinite(projectedTarget, controller),
                ActualPosition = SanitizeFinite(output.ActualHandPosition, controller),
                ControllerPosition = controller,
                SurfaceNormal = NormalizeSafe(surfaceNormal, new float3(0f, 1f, 0f)),
                Grip01 = math.saturate(SanitizeFinite(input.Grip01, 0f)),
                LockBlend01 = math.saturate(output.LockBlend01),
                SlidingSpeed = SanitizeFinite(slidingSpeed, 0f),
                State = grabState,
                Flags = (byte)math.min(output.Flags, 255u),
                InteractableId = input.InteractableId,
                FrameIndex = FrameIndex,
                StateHash = output.StateHash
            };
        }

        private void WriteTelemetry(
            int hand,
            in VRHandPresenceInput input,
            in VRHandPresenceOutput output,
            byte grabState)
        {
            if (!TelemetryRing.IsCreated || !TelemetryCursor.IsCreated || TelemetryRing.Length <= 0 || TelemetryCursor.Length <= 0)
                return;

            int cursor = TelemetryCursor[0];
            int index = PositiveModulo(cursor, TelemetryRing.Length);
            float separation = ResolveLength(output.GhostHandPosition - output.ActualHandPosition);
            TelemetryRing[index] = new VRHandIkTelemetryEntry
            {
                FrameIndex = FrameIndex,
                StateHash = output.StateHash,
                Flags = output.Flags,
                InteractableId = input.InteractableId,
                HandIndex = (byte)hand,
                GrabState = grabState,
                IKLockState = grabState,
                Reserved = 0,
                TargetPosition = GrabStates[hand].TargetPosition,
                ActualPosition = output.ActualHandPosition,
                ControllerPosition = output.GhostHandPosition,
                SurfaceNormal = output.SurfaceNormal,
                LockBlend01 = output.LockBlend01,
                SlidingSpeed = output.SlidingSpeed,
                ControllerSeparation = separation
            };

            TelemetryCursor[0] = cursor == int.MaxValue
                ? PositiveModulo(index + 1, TelemetryRing.Length)
                : cursor + 1;
        }

        private VRHandPresenceOutput BuildNanFallback(in VRHandPresenceInput input, VRHandGrabState previousState, int hand, uint flags)
        {
            float3 controller = SanitizeFinite(input.ControllerPosition, previousState.ControllerPosition);
            float3 actual = SanitizeFinite(previousState.ActualPosition, controller);
            float3 shoulder = SanitizeFinite(input.ShoulderPosition, actual + new float3(0f, -0.25f, -0.2f));
            float3 fallbackElbow = SanitizeFinite(math.lerp(shoulder, actual, 0.5f), actual);
            quaternion safeRotation = SanitizeQuaternion(input.ControllerRotation, quaternion.identity);
            return new VRHandPresenceOutput
            {
                ActualHandPosition = actual,
                GhostHandPosition = controller,
                ElbowPosition = SanitizeFinite(input.CurrentElbowPosition, fallbackElbow),
                SurfaceNormal = new float3(0f, 1f, 0f),
                UpperArmRotation = SanitizeQuaternion(input.CurrentUpperRotation, quaternion.identity),
                LowerArmRotation = SanitizeQuaternion(input.CurrentLowerRotation, quaternion.identity),
                HandRotation = safeRotation,
                LockBlend01 = 0f,
                HapticIntensity = 0f,
                SlidingSpeed = 0f,
                Flags = flags,
                StateHash = ComposeStateHash(actual, actual, controller, flags, hand)
            };
        }

        private static bool IsValidOutput(in VRHandPresenceOutput output)
        {
            return math.all(math.isfinite(output.ActualHandPosition)) &&
                   math.all(math.isfinite(output.GhostHandPosition)) &&
                   math.all(math.isfinite(output.ElbowPosition)) &&
                   math.all(math.isfinite(output.SurfaceNormal)) &&
                   math.all(math.isfinite(output.UpperArmRotation.value)) &&
                   math.all(math.isfinite(output.LowerArmRotation.value)) &&
                   math.all(math.isfinite(output.HandRotation.value)) &&
                   math.isfinite(output.LockBlend01) &&
                   math.isfinite(output.HapticIntensity) &&
                   math.isfinite(output.SlidingSpeed);
        }

        private static void SolveTwoBone(
            float3 shoulder,
            float3 handTarget,
            float3 pole,
            float upperArm,
            float lowerArm,
            out float3 elbowPosition,
            out bool jointLimited)
        {
            jointLimited = false;
            float3 shoulderToHand = handTarget - shoulder;
            float distanceSq = math.lengthsq(shoulderToHand);
            float distance = FastSqrt(math.max(distanceSq, MinLengthSq));
            float minReach = math.max(0.01f, math.abs(upperArm - lowerArm) + 0.001f);
            float maxReach = math.max(minReach + 0.001f, upperArm + lowerArm - 0.001f);
            float clampedDistance = math.clamp(distance, minReach, maxReach);
            if (math.abs(clampedDistance - distance) > 0.001f)
                jointLimited = true;

            float3 handDirection = NormalizeSafe(shoulderToHand, new float3(0f, 0f, 1f));
            float3 poleDirection = NormalizeSafe(pole - shoulder, ResolvePerpendicular(handDirection));
            float3 bendDirection = ProjectOnPlane(poleDirection, handDirection);
            bendDirection = NormalizeSafe(bendDirection, ResolvePerpendicular(handDirection));

            float numerator = upperArm * upperArm + clampedDistance * clampedDistance - lowerArm * lowerArm;
            float denominator = math.max(0.0001f, 2f * upperArm * clampedDistance);
            float cosShoulderAngle = numerator / denominator;
            float clampedCos = math.clamp(cosShoulderAngle, -0.98f, 0.995f);
            if (math.abs(clampedCos - cosShoulderAngle) > 0.0001f)
                jointLimited = true;

            float shoulderAngle = FastAcos(clampedCos);
            float sinAngle = math.sin(shoulderAngle);
            float cosAngle = math.cos(shoulderAngle);
            float3 upperDirection = NormalizeSafe(handDirection * cosAngle + bendDirection * sinAngle, handDirection);

            if (math.dot(upperDirection, bendDirection) < -0.001f)
            {
                upperDirection = NormalizeSafe(handDirection * cosAngle + bendDirection * math.abs(sinAngle), handDirection);
                jointLimited = true;
            }

            elbowPosition = SanitizeFinite(shoulder + upperDirection * upperArm, math.lerp(shoulder, handTarget, 0.5f));
        }

        private static quaternion BuildLimbRotation(float3 start, float3 end, float3 pole, quaternion fallback)
        {
            float3 forward = NormalizeSafe(end - start, new float3(0f, 0f, 1f));
            float3 up = NormalizeSafe(ProjectOnPlane(pole - start, forward), ResolvePerpendicular(forward));
            return SanitizeQuaternion(quaternion.LookRotationSafe(forward, up), fallback);
        }

        private static quaternion BuildHandRotation(in VRHandPresenceInput input, float3 surfaceNormal, bool locked)
        {
            quaternion fallback = SanitizeQuaternion(input.CurrentHandRotation, quaternion.identity);
            quaternion controllerRotation = SanitizeQuaternion(input.ControllerRotation, fallback);
            if (!locked)
                return controllerRotation;

            float3 forward = NormalizeSafe(ProjectOnPlane(input.ControllerForward, surfaceNormal), ResolvePerpendicular(surfaceNormal));
            float3 up = NormalizeSafe(surfaceNormal, new float3(0f, 1f, 0f));
            return SanitizeQuaternion(quaternion.LookRotationSafe(forward, up), controllerRotation);
        }

        private bool TrySampleSdfTrilinear(float3 worldPosition, float3 invCellSize, float sdfRange, out float density)
        {
            density = 0f;
            if (!EncodedSdf.IsCreated ||
                SdfDimensions.x <= 1 ||
                SdfDimensions.y <= 1 ||
                SdfDimensions.z <= 1 ||
                sdfRange <= 0.0001f)
            {
                return false;
            }

            float3 sample = (worldPosition - SdfOrigin) * invCellSize;
            if (sample.x < 0f || sample.y < 0f || sample.z < 0f ||
                sample.x > SdfDimensions.x - 1f ||
                sample.y > SdfDimensions.y - 1f ||
                sample.z > SdfDimensions.z - 1f)
            {
                return false;
            }

            sample = math.clamp(sample, float3.zero, new float3(SdfDimensions.x - 1.001f, SdfDimensions.y - 1.001f, SdfDimensions.z - 1.001f));
            int x0 = (int)math.floor(sample.x);
            int y0 = (int)math.floor(sample.y);
            int z0 = (int)math.floor(sample.z);
            int x1 = math.min(x0 + 1, SdfDimensions.x - 1);
            int y1 = math.min(y0 + 1, SdfDimensions.y - 1);
            int z1 = math.min(z0 + 1, SdfDimensions.z - 1);
            float3 f = sample - new float3(x0, y0, z0);

            float c000 = DecodeSdf(SdfIndex(x0, y0, z0), sdfRange);
            float c100 = DecodeSdf(SdfIndex(x1, y0, z0), sdfRange);
            float c010 = DecodeSdf(SdfIndex(x0, y1, z0), sdfRange);
            float c110 = DecodeSdf(SdfIndex(x1, y1, z0), sdfRange);
            float c001 = DecodeSdf(SdfIndex(x0, y0, z1), sdfRange);
            float c101 = DecodeSdf(SdfIndex(x1, y0, z1), sdfRange);
            float c011 = DecodeSdf(SdfIndex(x0, y1, z1), sdfRange);
            float c111 = DecodeSdf(SdfIndex(x1, y1, z1), sdfRange);
            float c00 = math.lerp(c000, c100, f.x);
            float c10 = math.lerp(c010, c110, f.x);
            float c01 = math.lerp(c001, c101, f.x);
            float c11 = math.lerp(c011, c111, f.x);
            float c0 = math.lerp(c00, c10, f.y);
            float c1 = math.lerp(c01, c11, f.y);
            density = math.lerp(c0, c1, f.z);
            return math.isfinite(density);
        }

        private bool TryResolveSdfGradient(float3 worldPosition, float3 invCellSize, float sdfRange, float3 step, out float3 normal)
        {
            normal = new float3(0f, 1f, 0f);
            float3 sx = new float3(step.x, 0f, 0f);
            float3 sy = new float3(0f, step.y, 0f);
            float3 sz = new float3(0f, 0f, step.z);
            if (!TrySampleSdfTrilinear(worldPosition + sx, invCellSize, sdfRange, out float px) ||
                !TrySampleSdfTrilinear(worldPosition - sx, invCellSize, sdfRange, out float nx) ||
                !TrySampleSdfTrilinear(worldPosition + sy, invCellSize, sdfRange, out float py) ||
                !TrySampleSdfTrilinear(worldPosition - sy, invCellSize, sdfRange, out float ny) ||
                !TrySampleSdfTrilinear(worldPosition + sz, invCellSize, sdfRange, out float pz) ||
                !TrySampleSdfTrilinear(worldPosition - sz, invCellSize, sdfRange, out float nz))
            {
                return false;
            }

            normal = NormalizeSafe(new float3(px - nx, py - ny, pz - nz), normal);
            return math.all(math.isfinite(normal));
        }

        private float DecodeSdf(int index, float sdfRange)
        {
            if ((uint)index >= (uint)EncodedSdf.Length)
                return -sdfRange;

            return ((EncodedSdf[index] * InvEncodedByteMax) * 2f - 1f) * sdfRange;
        }

        private int SdfIndex(int x, int y, int z)
        {
            return (z * SdfDimensions.y + y) * SdfDimensions.x + x;
        }

        /// <summary>
        /// Validates encoded SDF dimensions without allocating.
        /// </summary>
        public static bool TryResolveSdfVoxelCount(int3 dimensions, out int voxelCount)
        {
            voxelCount = 0;
            if (dimensions.x <= 1 || dimensions.y <= 1 || dimensions.z <= 1)
                return false;

            long count = (long)dimensions.x * dimensions.y * dimensions.z;
            if (count <= 0L || count > int.MaxValue)
                return false;

            voxelCount = (int)count;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FastBlendAlpha(float sharpness, float dt)
        {
            float x = math.max(0f, sharpness * dt);
            return math.saturate(1f - math.rcp(1f + x + 0.5f * x * x));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FastAcos(float value)
        {
            float x = math.clamp(value, -1f, 1f);
            float negate = x < 0f ? 1f : 0f;
            float ax = math.abs(x);
            float ret = -0.0187293f;
            ret = ret * ax + 0.0742610f;
            ret = ret * ax - 0.2121144f;
            ret = ret * ax + 1.5707288f;
            ret *= FastSqrt(1f - ax);
            return ret - 2f * negate * ret + negate * math.PI;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FastSqrt(float value)
        {
            return value > MinLengthSq && math.isfinite(value) ? value * math.rsqrt(value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static quaternion FastNlerp(quaternion from, quaternion to, float t)
        {
            quaternion a = SanitizeQuaternion(from, quaternion.identity);
            quaternion b = SanitizeQuaternion(to, a);
            float4 av = a.value;
            float4 bv = b.value;
            bv = math.dot(av, bv) < 0f ? -bv : bv;
            float4 v = math.lerp(av, bv, math.saturate(t));
            float lengthSq = math.lengthsq(v);
            return lengthSq > MinLengthSq && math.isfinite(lengthSq)
                ? new quaternion(v * math.rsqrt(lengthSq))
                : a;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static quaternion SanitizeQuaternion(quaternion value, quaternion fallback)
        {
            float lengthSq = math.lengthsq(value.value);
            return math.all(math.isfinite(value.value)) && lengthSq > MinLengthSq
                ? new quaternion(value.value * math.rsqrt(lengthSq))
                : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ProjectOnPlane(float3 value, float3 normal)
        {
            return value - normal * math.dot(value, normal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolvePerpendicular(float3 direction)
        {
            float3 safeDirection = NormalizeSafe(direction, new float3(0f, 0f, 1f));
            float3 axis = math.abs(safeDirection.y) < 0.9f ? new float3(0f, 1f, 0f) : new float3(1f, 0f, 0f);
            return NormalizeSafe(math.cross(safeDirection, axis), new float3(1f, 0f, 0f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveLength(float3 value)
        {
            float lengthSq = math.lengthsq(value);
            return lengthSq > MinLengthSq && math.isfinite(lengthSq) ? lengthSq * math.rsqrt(lengthSq) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= MinLengthSq)
                return SanitizeFinite(fallback, new float3(0f, 1f, 0f));

            return value * math.rsqrt(lengthSq);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizePositiveFinite(float value, float fallback, float minValue)
        {
            return math.isfinite(value) && value > minValue ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeFiniteClamp(float value, float fallback, float minValue, float maxValue)
        {
            return math.isfinite(value) ? math.clamp(value, minValue, maxValue) : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SanitizeFinite(float3 value, float3 fallback)
        {
            return math.all(math.isfinite(value)) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PositiveModulo(int value, int length)
        {
            int safeLength = math.max(1, length);
            int result = value % safeLength;
            return result < 0 ? result + safeLength : result;
        }

        private static uint ComposeStateHash(float3 target, float3 actual, float3 controller, uint flags, int hand)
        {
            uint hash = 2166136261u;
            hash = HashFloat3(hash, target);
            hash = HashFloat3(hash, actual);
            hash = HashFloat3(hash, controller);
            hash = Mix(hash, flags);
            hash = Mix(hash, (uint)hand);
            return hash != 0u ? hash : 1u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HashFloat3(uint hash, float3 value)
        {
            hash = Mix(hash, math.asuint(value.x));
            hash = Mix(hash, math.asuint(value.y));
            hash = Mix(hash, math.asuint(value.z));
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint hash, uint value)
        {
            return (hash ^ value) * 16777619u;
        }
    }
}
