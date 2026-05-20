using System.Runtime.CompilerServices;
using Hecton8.Core.Contracts;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Animation.KineticCharacter
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct MockCharacterKinematicsJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<KineticCharacterFrameInputDTO> Inputs;
        public uint Frame;
        public float DeltaTime;
        public float SimulationTime;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            if (!Inputs.IsCreated || (uint)index >= (uint)Inputs.Length)
                return;

            uint seed = ((uint)(index + 1) * 0x9E3779B9u) ^ (Frame * 0x85EBCA6Bu) ^ 0x53484E42u;
            float phase = SimulationTime * (0.35f + (seed & 15u) * 0.0075f);
            float sway = KineticCharacterMath.FastSin(phase) * 0.18f;
            float bob = KineticCharacterMath.FastSin(phase * 1.73f + 0.4f) * 0.06f;

            KineticCharacterFrameInputDTO input = default;
            input.RootLocalPosition = KineticCharacterMath.Float3(sway, 0f, bob);
            input.GlobalQualityWeight = math.saturate(math.select(1f, GlobalQualityWeight, math.isfinite(GlobalQualityWeight)));
            input.RootRotation = quaternion.identity;
            input.VelocityLocal = KineticCharacterMath.Float3(
                KineticCharacterMath.FastSin(phase * 1.21f) * 0.55f,
                KineticCharacterMath.FastSin(phase * 0.7f) * 0.08f,
                1.4f + input.GlobalQualityWeight * 0.8f);
            input.Visible01 = 1f;
            input.CameraLocalPosition = input.RootLocalPosition + KineticCharacterMath.Float3(0f, 1.58f, -0.08f);
            input.CameraForwardLocal = KineticCharacterMath.Float3(0f, 0f, 1f);
            input.OxygenLevel01 = 0.82f;
            input.ToolPoseMatrix = float4x4.identity;
            input.SimulationTickDelta = math.clamp(DeltaTime, KineticCharacterAnimatorConstants.MinDeltaTime, KineticCharacterAnimatorConstants.MaxDeltaTime);
            input.SimulationTime = SimulationTime;
            input.SwimWaveForward = KineticCharacterMath.FastSin(phase * 0.83f) * 0.2f;
            input.SwimWaveLateral = KineticCharacterMath.FastSin(phase * 0.61f + 1.1f) * 0.18f;
            input.SwimLeanWeight = 0.35f + input.GlobalQualityWeight * 0.25f;
            input.BreathingPhase = KineticCharacterMath.FastSin(phase * 0.55f);
            input.Frame = Frame;
            input.Flags = KineticCharacterAnimatorConstants.InputFlagVisible | KineticCharacterAnimatorConstants.InputFlagMock;
            Inputs[index] = input;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct EvaluateWallProximityJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<KineticCharacterFrameInputDTO> Inputs;
        [NoAlias] public NativeArray<ProceduralIKTargetDTO> Targets;
        [ReadOnly, NoAlias] public NativeArray<byte> VoxelSdfTexture3D;
        public int3 SdfDimensions;
        public float3 SdfOrigin;
        public float3 SdfCellSize;
        public float SdfRangeMeters;
        public float WallBraceDistanceMeters;
        public double AupSectorSizeMeters;

        public void Execute(int index)
        {
            if (!Inputs.IsCreated || !Targets.IsCreated || (uint)index >= (uint)Inputs.Length)
                return;

            int targetStart = index * KineticCharacterAnimatorConstants.IkTargetCount;
            if (targetStart < 0 || targetStart + 3 >= Targets.Length)
                return;

            KineticCharacterFrameInputDTO input = Inputs[index];
            float3 rootRelative = KineticCharacterMath.AupToObserverRelative(
                input.RootSectorX,
                input.RootSectorY,
                input.RootSectorZ,
                input.RootLocalPosition,
                input.CameraSectorX,
                input.CameraSectorY,
                input.CameraSectorZ,
                input.CameraLocalPosition,
                AupSectorSizeMeters);
            float3 forward = KineticCharacterMath.NormalizeSafe(input.CameraForwardLocal, KineticCharacterMath.Float3(0f, 0f, 1f));
            quaternion rootRotation = KineticCharacterMath.SanitizeRotation(input.RootRotation);
            float3 right = math.mul(rootRotation, KineticCharacterMath.Float3(1f, 0f, 0f));
            float3 up = math.mul(rootRotation, KineticCharacterMath.Float3(0f, 1f, 0f));

            ProceduralIKTargetDTO leftHand = BuildDefaultTarget(rootRelative, forward, right, up, -1f, KineticCharacterAnimatorConstants.IkTargetFlagLeftHand);
            ProceduralIKTargetDTO rightHand = BuildDefaultTarget(rootRelative, forward, right, up, 1f, KineticCharacterAnimatorConstants.IkTargetFlagRightHand);
            bool usedSdf = TryApplySdfBrace(input, rootRelative, forward, right, up, -1f, ref leftHand);
            usedSdf |= TryApplySdfBrace(input, rootRelative, forward, right, up, 1f, ref rightHand);

            if (usedSdf)
            {
                leftHand.Flags |= (leftHand.Weight01 > 0f ? KineticCharacterAnimatorConstants.IkTargetFlagSdfBrace : 0u);
                rightHand.Flags |= (rightHand.Weight01 > 0f ? KineticCharacterAnimatorConstants.IkTargetFlagSdfBrace : 0u);
            }

            Targets[targetStart] = leftHand;
            Targets[targetStart + 1] = rightHand;
            Targets[targetStart + 2] = default;
            Targets[targetStart + 3] = default;
        }

        private bool TryApplySdfBrace(
            KineticCharacterFrameInputDTO input,
            float3 rootRelative,
            float3 forward,
            float3 right,
            float3 up,
            float sideSign,
            ref ProceduralIKTargetDTO target)
        {
            if (!VoxelSdfTexture3D.IsCreated ||
                SdfDimensions.x <= 1 ||
                SdfDimensions.y <= 1 ||
                SdfDimensions.z <= 1 ||
                SdfCellSize.x <= 0f ||
                SdfCellSize.y <= 0f ||
                SdfCellSize.z <= 0f)
            {
                return false;
            }

            float3 probeLocal = input.RootLocalPosition +
                                forward * 0.62f +
                                right * (sideSign * 0.28f) +
                                up * 0.98f;
            float quality = math.saturate(math.select(0f, input.GlobalQualityWeight, math.isfinite(input.GlobalQualityWeight)));
            if (!TrySampleSdf(probeLocal, quality, out float distance, out float3 normal))
                return false;

            float braceDistance = math.max(0.05f, WallBraceDistanceMeters);
            float weight = math.saturate((braceDistance - distance) * math.rcp(math.max(braceDistance, 0.0001f)));
            if (weight <= 0.0001f)
                return false;

            float3 contactLocal = probeLocal + normal * math.max(0.025f, distance + 0.025f);
            float3 contactRelative = KineticCharacterMath.AupToObserverRelative(
                input.RootSectorX,
                input.RootSectorY,
                input.RootSectorZ,
                contactLocal,
                input.CameraSectorX,
                input.CameraSectorY,
                input.CameraSectorZ,
                input.CameraLocalPosition,
                AupSectorSizeMeters);
            target.LocalPosition = math.lerp(target.LocalPosition, contactRelative, weight);
            target.PoleOrNormal = normal;
            target.Weight01 = math.max(target.Weight01, weight);
            target.Flags |= KineticCharacterAnimatorConstants.IkTargetFlagSdfBrace;
            return true;
        }

        private bool TrySampleSdf(float3 sampleLocal, float quality, out float distance, out float3 normal)
        {
            distance = 0f;
            normal = KineticCharacterMath.Float3(0f, 1f, 0f);
            float3 safeCellSize = math.max(math.abs(SdfCellSize), KineticCharacterMath.Float3(0.0001f, 0.0001f, 0.0001f));
            float3 grid = (sampleLocal - SdfOrigin) * math.rcp(safeCellSize);
            int x = (int)math.round(grid.x);
            int y = (int)math.round(grid.y);
            int z = (int)math.round(grid.z);
            if (x <= 0 || y <= 0 || z <= 0 || x >= SdfDimensions.x - 1 || y >= SdfDimensions.y - 1 || z >= SdfDimensions.z - 1)
                return false;

            distance = DecodeSdfAt(x, y, z);
            float gradientGate = math.step(0.24f, quality);
            if (gradientGate > 0f)
            {
                float dx = DecodeSdfAt(x + 1, y, z) - DecodeSdfAt(x - 1, y, z);
                float dy = DecodeSdfAt(x, y + 1, z) - DecodeSdfAt(x, y - 1, z);
                float dz = DecodeSdfAt(x, y, z + 1) - DecodeSdfAt(x, y, z - 1);
                float3 gradientNormal = KineticCharacterMath.NormalizeSafe(new float3(dx, dy, dz), normal);
                normal = KineticCharacterMath.NormalizeSafe(math.lerp(normal, gradientNormal, KineticCharacterMath.SmoothRange01(quality, 0.24f, 1f)), normal);
            }

            return math.isfinite(distance) && math.all(math.isfinite(normal));
        }

        private float DecodeSdfAt(int x, int y, int z)
        {
            int index = x + SdfDimensions.x * (y + SdfDimensions.y * z);
            if ((uint)index >= (uint)VoxelSdfTexture3D.Length)
                return SdfRangeMeters;

            return ((VoxelSdfTexture3D[index] * 0.0039215686274509803f) * 2f - 1f) * SdfRangeMeters;
        }

        private static ProceduralIKTargetDTO BuildDefaultTarget(float3 rootRelative, float3 forward, float3 right, float3 up, float sideSign, uint flags)
        {
            ProceduralIKTargetDTO target = default;
            target.LocalPosition = rootRelative + up * 1.08f + forward * 0.5f + right * sideSign * 0.28f;
            target.PoleOrNormal = up;
            target.Weight01 = 0f;
            target.Flags = flags;
            return target;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct ProceduralLocomotionPhaseJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<KineticCharacterRigDTO> Rigs;
        [ReadOnly, NoAlias] public NativeArray<KineticCharacterFrameInputDTO> Inputs;
        [ReadOnly, NoAlias] public NativeArray<int> ParentIndices;
        [ReadOnly, NoAlias] public NativeArray<float4x4> BindPoses;
        [NoAlias] public NativeArray<ProceduralBoneDTO> BoneOutputs;
        [NoAlias] public NativeArray<KineticCharacterFrameStatsDTO> Stats;
        [ReadOnly, NoAlias] public NativeArray<ProceduralIKTargetDTO> IkTargets;
        [ReadOnly, NoAlias] public NativeArray<KineticCharacterTuningDTO> Tuning;
        public float GlobalQualityWeight;
        public float DeltaTime;
        public float SimulationTime;
        public uint Frame;
        public double AupSectorSizeMeters;

        public void Execute(int index)
        {
            if (!Rigs.IsCreated ||
                !Inputs.IsCreated ||
                !BoneOutputs.IsCreated ||
                !Stats.IsCreated ||
                (uint)index >= (uint)Rigs.Length ||
                (uint)index >= (uint)Inputs.Length ||
                (uint)index >= (uint)Stats.Length)
            {
                return;
            }

            KineticCharacterRigDTO rig = Rigs[index];
            KineticCharacterFrameInputDTO input = Inputs[index];
            KineticCharacterTuningDTO tuning = ResolveTuning();
            KineticCharacterFrameStatsDTO stats = default;
            stats.Frame = Frame;
            stats.StateHash = rig.SkeletonHash;

            int boneStart = math.max(0, rig.BoneStart);
            int boneCount = math.min(math.max(0, rig.BoneCount), BoneOutputs.Length - boneStart);
            if (boneCount <= 0 || boneStart < 0)
            {
                stats.InvalidMathCount = 1;
                stats.Flags = KineticCharacterAnimatorConstants.TelemetryFlagInvalid;
                Stats[index] = stats;
                return;
            }

            float visible = math.saturate(math.select(0f, input.Visible01, math.isfinite(input.Visible01)));
            if ((rig.Flags & KineticCharacterAnimatorConstants.RigFlagVisible) == 0u ||
                (input.Flags & KineticCharacterAnimatorConstants.InputFlagVisible) == 0u ||
                visible <= 0.0001f)
            {
                Stats[index] = stats;
                return;
            }

            float inputQuality = math.isfinite(input.GlobalQualityWeight) ? input.GlobalQualityWeight : GlobalQualityWeight;
            float quality = math.saturate(math.min(math.saturate(inputQuality), math.min(tuning.GlobalQualityWeight, GlobalQualityWeight)));
            float qualityCurve = KineticCharacterMath.Smooth01(quality);
            int ikIterations = ResolveIkIterations(quality, tuning);
            int activeBoneCount = ResolveActiveBoneCount(quality, boneCount, tuning);
            float dt = math.clamp(
                math.select(DeltaTime, input.SimulationTickDelta, math.isfinite(input.SimulationTickDelta) && input.SimulationTickDelta > 0f),
                KineticCharacterAnimatorConstants.MinDeltaTime,
                KineticCharacterAnimatorConstants.MaxDeltaTime);
            float localTime = math.select(SimulationTime, input.SimulationTime, math.isfinite(input.SimulationTime) && input.SimulationTime > 0f);

            float3 rootRelative = KineticCharacterMath.AupToObserverRelative(
                input.RootSectorX,
                input.RootSectorY,
                input.RootSectorZ,
                input.RootLocalPosition,
                input.CameraSectorX,
                input.CameraSectorY,
                input.CameraSectorZ,
                input.CameraLocalPosition,
                AupSectorSizeMeters);
            quaternion rootRotation = KineticCharacterMath.SanitizeRotation(input.RootRotation);
            float3 forward = KineticCharacterMath.NormalizeSafe(math.mul(rootRotation, KineticCharacterMath.Float3(0f, 0f, 1f)), KineticCharacterMath.Float3(0f, 0f, 1f));
            float3 right = KineticCharacterMath.NormalizeSafe(math.mul(rootRotation, KineticCharacterMath.Float3(1f, 0f, 0f)), KineticCharacterMath.Float3(1f, 0f, 0f));
            float3 up = KineticCharacterMath.NormalizeSafe(math.mul(rootRotation, KineticCharacterMath.Float3(0f, 1f, 0f)), KineticCharacterMath.Float3(0f, 1f, 0f));
            float speedSq = math.max(0f, math.lengthsq(KineticCharacterMath.SanitizeFinite(input.VelocityLocal, float3.zero)));
            float speed = speedSq * math.rsqrt(math.max(speedSq, 0.000001f));
            float frequency = tuning.LocomotionFrequencyHz * (1f + speed * 0.055f);
            rig.Phase += dt * frequency * KineticCharacterAnimatorConstants.TwoPi;
            rig.Phase -= math.floor(rig.Phase * (1f / KineticCharacterAnimatorConstants.TwoPi)) * KineticCharacterAnimatorConstants.TwoPi;

            if ((input.Flags & KineticCharacterAnimatorConstants.InputFlagDamageImpulse) != 0u)
                rig.DamageSeconds = math.max(rig.DamageSeconds, tuning.DamageFlinchSeconds * math.saturate(input.DamageImpulse01));
            rig.DamageSeconds = math.max(0f, rig.DamageSeconds - dt * math.max(0.05f, rig.DamageDecayHz));

            float breathPhase = input.BreathingPhase;
            float authoredBreath = math.isfinite(breathPhase) ? breathPhase : KineticCharacterMath.FastSin(localTime * tuning.BreathingFrequencyHz * KineticCharacterAnimatorConstants.TwoPi);
            float lowTierTriangle = KineticCharacterMath.TriangleWaveSigned(localTime * tuning.BreathingFrequencyHz);
            float breath = math.lerp(lowTierTriangle, authoredBreath, qualityCurve);
            float oxygenStress = 1f - math.saturate(input.OxygenLevel01);
            float breathAmp = (tuning.BreathingAmplitudeMeters + rig.BreathAmplitudeMeters) * math.lerp(0.7f, 1.55f, oxygenStress);
            float swimLean = math.saturate(input.SwimLeanWeight);
            float swimBob = tuning.SwimBobMeters * (0.35f + swimLean) * KineticCharacterMath.FastSin(rig.Phase + input.SwimWaveForward);
            float damage01 = math.saturate(rig.DamageSeconds * math.rcp(math.max(0.01f, tuning.DamageFlinchSeconds)));
            float damageAngle = damage01 * tuning.DamageFlinchRadians * KineticCharacterMath.FastSin(localTime * 23f + rig.StableSeed * 0.00021f);
            float3 rootOffset = up * (breath * breathAmp + swimBob) + right * (input.SwimWaveLateral * tuning.SpineLeanRadians * 0.08f);
            rootOffset += KineticCharacterMath.SanitizeFinite(input.DamageImpulseLocal, float3.zero) * (damage01 * 0.055f);

            float3 root = rootRelative + rootOffset;
            float3 pelvis = root + up * 0.92f;
            float3 chest = root + up * (0.92f + rig.SpineLength) + forward * (input.SwimWaveForward * tuning.SpineLeanRadians * 0.12f);
            float3 neck = chest + up * rig.NeckLength + forward * 0.03f;
            float3 head = neck + up * 0.14f + forward * 0.02f;
            quaternion spineDamage = quaternion.AxisAngle(right, damageAngle);
            float4x4 rootMatrix = float4x4.TRS(root, rootRotation, KineticCharacterMath.Float3(1f, 1f, 1f));

            int invalidCount = 0;
            int matricesComputed = 0;
            WriteBone(boneStart, rig.RootIndex, rootMatrix, activeBoneCount, ref invalidCount, ref matricesComputed);
            WriteBone(boneStart, rig.SpineIndex, KineticCharacterMath.BoneMatrix(pelvis, chest - pelvis, up), activeBoneCount, ref invalidCount, ref matricesComputed);
            WriteBone(boneStart, rig.ChestIndex, float4x4.TRS(chest, math.mul(rootRotation, spineDamage), KineticCharacterMath.Float3(1f, 1f, 1f)), activeBoneCount, ref invalidCount, ref matricesComputed);
            WriteBone(boneStart, rig.NeckIndex, KineticCharacterMath.BoneMatrix(neck, head - neck, up), activeBoneCount, ref invalidCount, ref matricesComputed);
            WriteBone(boneStart, rig.HeadIndex, KineticCharacterMath.BoneMatrix(head, forward + up * 0.1f, up), activeBoneCount, ref invalidCount, ref matricesComputed);

            float3 leftShoulder = chest - right * (rig.ShoulderWidth * 0.5f);
            float3 rightShoulder = chest + right * (rig.ShoulderWidth * 0.5f);
            float3 leftHip = pelvis - right * (rig.HipWidth * 0.5f);
            float3 rightHip = pelvis + right * (rig.HipWidth * 0.5f);

            float phaseLeft = rig.Phase;
            float phaseRight = rig.Phase + KineticCharacterAnimatorConstants.Pi;
            float armReach = tuning.ArmReachScale;
            float legReach = tuning.LegReachScale;
            float leftStroke = KineticCharacterMath.FastSin(phaseLeft);
            float rightStroke = KineticCharacterMath.FastSin(phaseRight);
            float toolWeight = math.saturate(input.ActiveToolWeight01 * tuning.ToolAlignmentWeight);
            uint flags = KineticCharacterAnimatorConstants.TelemetryFlagVisible;
            if ((input.Flags & KineticCharacterAnimatorConstants.InputFlagMock) != 0u)
                flags |= KineticCharacterAnimatorConstants.TelemetryFlagMock;
            if (damage01 > 0.0001f)
                flags |= KineticCharacterAnimatorConstants.TelemetryFlagDamageFlinch;
            if (activeBoneCount < boneCount)
                flags |= KineticCharacterAnimatorConstants.TelemetryFlagQualityCollapsed;

            ProceduralIKTargetDTO leftHandTarget = ResolveTarget(index, 0);
            ProceduralIKTargetDTO rightHandTarget = ResolveTarget(index, 1);
            if ((leftHandTarget.Flags & KineticCharacterAnimatorConstants.IkTargetFlagSdfBrace) != 0u ||
                (rightHandTarget.Flags & KineticCharacterAnimatorConstants.IkTargetFlagSdfBrace) != 0u)
            {
                flags |= KineticCharacterAnimatorConstants.TelemetryFlagSdfBrace;
            }

            if ((leftHandTarget.Flags & KineticCharacterAnimatorConstants.IkTargetFlagPlayerKinematics) != 0u ||
                (rightHandTarget.Flags & KineticCharacterAnimatorConstants.IkTargetFlagPlayerKinematics) != 0u)
            {
                flags |= KineticCharacterAnimatorConstants.TelemetryFlagPlayerKinematicsTargets;
            }

            float3 leftDefaultHand = leftShoulder + forward * (0.46f + leftStroke * tuning.LocomotionAmplitudeMeters) - right * 0.08f - up * (0.22f - leftStroke * 0.04f);
            float3 rightDefaultHand = rightShoulder + forward * (0.46f + rightStroke * tuning.LocomotionAmplitudeMeters) + right * 0.08f - up * (0.22f - rightStroke * 0.04f);
            leftDefaultHand += right * (input.SwimWaveLateral * 0.045f);
            rightDefaultHand += right * (input.SwimWaveLateral * 0.045f);

            bool hasToolPose = (input.Flags & KineticCharacterAnimatorConstants.InputFlagToolActive) != 0u &&
                               toolWeight > 0.0001f &&
                               KineticCharacterMath.IsFinite(input.ToolPoseMatrix) &&
                               math.lengthsq(input.ToolPoseMatrix.c3.xyz) > 0.0001f;
            if (hasToolPose)
            {
                float3 toolPosition = input.ToolPoseMatrix.c3.xyz;
                uint toolHash = ((input.Flags & KineticCharacterAnimatorConstants.InputFlagToolHashValid) != 0u) ? input.ActiveToolHash : 0u;
                float hash01 = (toolHash & 255u) * (1f / 255f);
                float side = math.select(-1f, 1f, (toolHash & 1u) != 0u);
                float supportWeight = math.saturate(toolWeight * math.lerp(0.35f, 0.85f, quality) * math.select(0f, 1f, toolHash != 0u));
                float supportReach = math.lerp(0.18f, 0.34f, hash01);
                float3 supportGrip = toolPosition - forward * supportReach - right * (0.12f * side) - up * 0.03f;
                leftDefaultHand = math.lerp(leftDefaultHand, supportGrip, supportWeight);
                rightDefaultHand = math.lerp(rightDefaultHand, toolPosition, toolWeight);
                rightDefaultHand = math.lerp(rightDefaultHand, rightShoulder + forward * 0.34f + right * 0.22f - up * 0.16f, tuning.ToolHandSuppression01 * toolWeight);
                flags |= KineticCharacterAnimatorConstants.TelemetryFlagToolAligned;
            }

            float leftTargetWeight = math.saturate(leftHandTarget.Weight01 * tuning.WallBraceWeightScale);
            float rightTargetWeight = math.saturate(rightHandTarget.Weight01 * tuning.WallBraceWeightScale);
            float3 leftHand = math.lerp(leftDefaultHand, leftHandTarget.LocalPosition, leftTargetWeight);
            float3 rightHand = math.lerp(rightDefaultHand, rightHandTarget.LocalPosition, rightTargetWeight);
            float3 leftElbow = default;
            float3 rightElbow = default;
            SolveFabrikTwoBone(leftShoulder, leftHand, up - forward * 0.15f - right * 0.25f, rig.ArmUpperLength * armReach, rig.ArmLowerLength * armReach, ikIterations, tuning.IkToleranceMeters, out leftElbow, out leftHand);
            SolveFabrikTwoBone(rightShoulder, rightHand, up - forward * 0.15f + right * 0.25f, rig.ArmUpperLength * armReach, rig.ArmLowerLength * armReach, ikIterations, tuning.IkToleranceMeters, out rightElbow, out rightHand);

            WriteBone(boneStart, rig.LeftShoulderIndex, KineticCharacterMath.BoneMatrix(leftShoulder, leftElbow - leftShoulder, up), activeBoneCount, ref invalidCount, ref matricesComputed);
            WriteBone(boneStart, rig.LeftElbowIndex, KineticCharacterMath.BoneMatrix(leftElbow, leftHand - leftElbow, up), activeBoneCount, ref invalidCount, ref matricesComputed);
            WriteBone(boneStart, rig.LeftHandIndex, KineticCharacterMath.BoneMatrix(leftHand, forward - right * 0.15f, up), activeBoneCount, ref invalidCount, ref matricesComputed);
            WriteBone(boneStart, rig.RightShoulderIndex, KineticCharacterMath.BoneMatrix(rightShoulder, rightElbow - rightShoulder, up), activeBoneCount, ref invalidCount, ref matricesComputed);
            WriteBone(boneStart, rig.RightElbowIndex, KineticCharacterMath.BoneMatrix(rightElbow, rightHand - rightElbow, up), activeBoneCount, ref invalidCount, ref matricesComputed);
            WriteBone(boneStart, rig.RightHandIndex, KineticCharacterMath.BoneMatrix(rightHand, forward + right * 0.15f, up), activeBoneCount, ref invalidCount, ref matricesComputed);

            float legSwing = tuning.LocomotionAmplitudeMeters * 0.55f * math.saturate(speed * 0.35f);
            float3 leftFootTarget = leftHip - up * ((rig.LegUpperLength + rig.LegLowerLength) * 0.9f) + forward * (KineticCharacterMath.FastSin(phaseRight) * legSwing) - right * 0.05f;
            float3 rightFootTarget = rightHip - up * ((rig.LegUpperLength + rig.LegLowerLength) * 0.9f) + forward * (KineticCharacterMath.FastSin(phaseLeft) * legSwing) + right * 0.05f;
            float3 leftKnee = default;
            float3 rightKnee = default;
            SolveFabrikTwoBone(leftHip, leftFootTarget, forward - right * 0.15f, rig.LegUpperLength * legReach, rig.LegLowerLength * legReach, ikIterations, tuning.IkToleranceMeters, out leftKnee, out leftFootTarget);
            SolveFabrikTwoBone(rightHip, rightFootTarget, forward + right * 0.15f, rig.LegUpperLength * legReach, rig.LegLowerLength * legReach, ikIterations, tuning.IkToleranceMeters, out rightKnee, out rightFootTarget);

            WriteBone(boneStart, rig.LeftHipIndex, KineticCharacterMath.BoneMatrix(leftHip, leftKnee - leftHip, up), activeBoneCount, ref invalidCount, ref matricesComputed);
            WriteBone(boneStart, rig.LeftKneeIndex, KineticCharacterMath.BoneMatrix(leftKnee, leftFootTarget - leftKnee, up), activeBoneCount, ref invalidCount, ref matricesComputed);
            WriteBone(boneStart, rig.LeftFootIndex, KineticCharacterMath.BoneMatrix(leftFootTarget, forward, up), activeBoneCount, ref invalidCount, ref matricesComputed);
            WriteBone(boneStart, rig.RightHipIndex, KineticCharacterMath.BoneMatrix(rightHip, rightKnee - rightHip, up), activeBoneCount, ref invalidCount, ref matricesComputed);
            WriteBone(boneStart, rig.RightKneeIndex, KineticCharacterMath.BoneMatrix(rightKnee, rightFootTarget - rightKnee, up), activeBoneCount, ref invalidCount, ref matricesComputed);
            WriteBone(boneStart, rig.RightFootIndex, KineticCharacterMath.BoneMatrix(rightFootTarget, forward, up), activeBoneCount, ref invalidCount, ref matricesComputed);

            if (rig.ToolSocketIndex >= 0)
            {
                float4x4 toolMatrix = hasToolPose
                    ? input.ToolPoseMatrix
                    : KineticCharacterMath.BoneMatrix(rightHand, forward, up);
                WriteBone(boneStart, rig.ToolSocketIndex, toolMatrix, activeBoneCount, ref invalidCount, ref matricesComputed);
            }

            CollapseInactiveBones(boneStart, boneCount, activeBoneCount, rootMatrix);

            rig.ActiveBoneCount = activeBoneCount;
            rig.MaxIkIterations = ikIterations;
            rig.RuntimeFlags = flags;
            Rigs[index] = rig;

            uint hash = rig.SkeletonHash;
            hash = KineticCharacterMath.Hash(hash, (uint)matricesComputed);
            hash = KineticCharacterMath.Hash(hash, (uint)activeBoneCount);
            hash = KineticCharacterMath.Hash(hash, (uint)ikIterations);
            hash = KineticCharacterMath.Hash(hash, math.asuint(quality));
            hash = KineticCharacterMath.Hash(hash, math.asuint(root.x));
            hash = KineticCharacterMath.Hash(hash, math.asuint(root.y));
            hash = KineticCharacterMath.Hash(hash, math.asuint(root.z));
            hash = KineticCharacterMath.Hash(hash, input.ActiveToolHash);
            hash = KineticCharacterMath.Hash(hash, flags);

            stats.ActiveCharacters = 1;
            stats.MatricesComputed = matricesComputed;
            stats.InvalidMathCount = invalidCount;
            stats.MaxMatrixIndexPlusOne = boneStart + boneCount;
            stats.AverageIkIterations = ikIterations;
            stats.Quality = quality;
            stats.StateHash = hash;
            stats.Flags = invalidCount > 0 ? flags | KineticCharacterAnimatorConstants.TelemetryFlagInvalid : flags;
            stats.LastRootLocal = rootRelative;
            stats.CpuEstimateMicroseconds = EstimateMicroseconds(boneCount, activeBoneCount, ikIterations, quality);
            stats.ActiveIkTargets = CountActiveTargets(index);
            stats.BoneUploadCount = boneCount;
            Stats[index] = stats;
        }

        private KineticCharacterTuningDTO ResolveTuning()
        {
            if (Tuning.IsCreated && Tuning.Length > 0)
                return KineticCharacterSanitizer.SanitizeTuning(Tuning[0]);

            return KineticCharacterTuningDTO.Default();
        }

        private ProceduralIKTargetDTO ResolveTarget(int characterIndex, int offset)
        {
            int index = characterIndex * KineticCharacterAnimatorConstants.IkTargetCount + offset;
            return IkTargets.IsCreated && (uint)index < (uint)IkTargets.Length ? IkTargets[index] : default;
        }

        private int CountActiveTargets(int characterIndex)
        {
            int count = 0;
            int start = characterIndex * KineticCharacterAnimatorConstants.IkTargetCount;
            for (int i = 0; i < KineticCharacterAnimatorConstants.IkTargetCount; i++)
            {
                int index = start + i;
                if (IkTargets.IsCreated && (uint)index < (uint)IkTargets.Length && IkTargets[index].Weight01 > 0.0001f)
                    count++;
            }

            return count;
        }

        private static int ResolveIkIterations(float quality, KineticCharacterTuningDTO tuning)
        {
            float curved = KineticCharacterMath.Smooth01(quality);
            return math.clamp((int)math.round(math.lerp(tuning.MinimumIkIterations, tuning.UltraIkIterations, curved)), 1, 8);
        }

        private static int ResolveActiveBoneCount(float quality, int boneCount, KineticCharacterTuningDTO tuning)
        {
            float secondary = KineticCharacterMath.SmoothRange01(quality, tuning.SecondaryMotionStart01, 1f);
            int baseCount = math.min(10, boneCount);
            return math.clamp(baseCount + (int)math.round((boneCount - baseCount) * secondary), 1, boneCount);
        }

        private static float EstimateMicroseconds(int boneCount, int activeBoneCount, int ikIterations, float quality)
        {
            float baseCost = 2.4f + activeBoneCount * 0.22f + ikIterations * 0.82f;
            return baseCost + KineticCharacterMath.SmoothRange01(quality, 0.65f, 1f) * boneCount * 0.05f;
        }

        private void WriteBone(int boneStart, int relativeIndex, float4x4 matrix, int activeBoneCount, ref int invalidCount, ref int matricesComputed)
        {
            if (relativeIndex < 0)
                return;

            int absoluteIndex = boneStart + relativeIndex;
            if ((uint)absoluteIndex >= (uint)BoneOutputs.Length)
                return;

            if (!KineticCharacterMath.IsFinite(matrix))
            {
                matrix = float4x4.identity;
                invalidCount++;
            }

            BoneOutputs[absoluteIndex] = new ProceduralBoneDTO { LocalToWorld = matrix };
            if (relativeIndex < activeBoneCount)
                matricesComputed++;
        }

        private void CollapseInactiveBones(int boneStart, int boneCount, int activeBoneCount, float4x4 rootMatrix)
        {
            for (int relativeIndex = activeBoneCount; relativeIndex < boneCount; relativeIndex++)
            {
                int absolute = boneStart + relativeIndex;
                if ((uint)absolute >= (uint)BoneOutputs.Length)
                    continue;

                int parent = ParentIndices.IsCreated && (uint)absolute < (uint)ParentIndices.Length
                    ? ParentIndices[absolute]
                    : -1;
                float4x4 matrix = parent >= boneStart && parent < absolute && (uint)parent < (uint)BoneOutputs.Length
                    ? BoneOutputs[parent].LocalToWorld
                    : rootMatrix;
                BoneOutputs[absolute] = new ProceduralBoneDTO { LocalToWorld = matrix };
            }
        }

        private static void SolveFabrikTwoBone(
            float3 root,
            float3 target,
            float3 pole,
            float upperLength,
            float lowerLength,
            int iterations,
            float tolerance,
            out float3 joint,
            out float3 end)
        {
            float3 fallbackDirection = KineticCharacterMath.NormalizeSafe(target - root, KineticCharacterMath.Float3(0f, 0f, 1f));
            float maxReach = math.max(0.001f, upperLength + lowerLength - 0.0005f);
            float3 toTarget = target - root;
            float distanceSq = math.lengthsq(toTarget);
            bool hasTargetDistance = math.isfinite(distanceSq) && distanceSq > 0.000001f;
            float distance = hasTargetDistance ? distanceSq * math.rsqrt(math.max(distanceSq, 0.000001f)) : 0f;
            float clampedDistance = math.clamp(distance, math.abs(upperLength - lowerLength) + 0.0005f, maxReach);
            float3 direction = hasTargetDistance ? toTarget * math.rcp(math.max(distance, 0.000001f)) : fallbackDirection;
            float cosA = math.clamp((upperLength * upperLength + clampedDistance * clampedDistance - lowerLength * lowerLength) * math.rcp(math.max(0.0001f, 2f * upperLength * clampedDistance)), -1f, 1f);
            float sinSq = math.max(0f, 1f - cosA * cosA);
            float sinA = sinSq * math.rsqrt(math.max(sinSq, 0.000001f));
            float3 poleDirection = pole - direction * math.dot(pole, direction);
            poleDirection = KineticCharacterMath.NormalizeSafe(poleDirection, KineticCharacterMath.Float3(0f, 1f, 0f));
            joint = root + direction * (cosA * upperLength) + poleDirection * (sinA * upperLength);
            end = root + direction * clampedDistance;

            int count = math.clamp(iterations, 1, 8);
            for (int i = 0; i < count; i++)
            {
                end = target;
                joint = end + KineticCharacterMath.NormalizeSafe(joint - end, -direction) * lowerLength;
                float3 rootCandidate = joint + KineticCharacterMath.NormalizeSafe(root - joint, -direction) * upperLength;
                joint = root + KineticCharacterMath.NormalizeSafe(joint - rootCandidate, direction) * upperLength;
                end = joint + KineticCharacterMath.NormalizeSafe(end - joint, direction) * lowerLength;
                float errSq = math.lengthsq(end - target);
                if (errSq <= tolerance * tolerance)
                    break;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct ComputeFinalBoneMatricesJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<ProceduralBoneDTO> BoneOutputs;
        [NoAlias] public NativeArray<float4x4> Matrices;

        public void Execute(int index)
        {
            if (!BoneOutputs.IsCreated || !Matrices.IsCreated || (uint)index >= (uint)BoneOutputs.Length || (uint)index >= (uint)Matrices.Length)
                return;

            float4x4 matrix = BoneOutputs[index].LocalToWorld;
            Matrices[index] = KineticCharacterMath.IsFinite(matrix) ? matrix : float4x4.identity;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct KineticAnimationTelemetryJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<KineticCharacterFrameStatsDTO> Stats;
        [ReadOnly, NoAlias] public NativeArray<KineticCharacterFrameInputDTO> Inputs;
        [NoAlias] public NativeArray<KineticAnimationTelemetryEntry> Telemetry;
        [NoAlias] public NativeArray<int> Cursor;
        public uint Frame;

        public void Execute()
        {
            if (!Stats.IsCreated || !Inputs.IsCreated || !Telemetry.IsCreated || !Cursor.IsCreated || Cursor.Length <= 0 || Telemetry.Length <= 0)
                return;

            int active = 0;
            int bones = 0;
            int invalid = 0;
            float ik = 0f;
            float quality = 0f;
            float cpuUs = 0f;
            uint hash = 0x53484E42u;
            uint flags = 0u;
            float3 rootLocal = float3.zero;
            long sectorX = 0L;
            long sectorY = 0L;
            long sectorZ = 0L;
            for (int i = 0; i < Stats.Length; i++)
            {
                KineticCharacterFrameStatsDTO stat = Stats[i];
                if (stat.ActiveCharacters <= 0)
                    continue;

                KineticCharacterFrameInputDTO input = Inputs[math.min(i, Inputs.Length - 1)];
                active += stat.ActiveCharacters;
                bones = math.max(bones, stat.BoneUploadCount);
                invalid += stat.InvalidMathCount;
                ik += stat.AverageIkIterations;
                quality += stat.Quality;
                cpuUs += stat.CpuEstimateMicroseconds;
                hash = KineticCharacterMath.Hash(hash, stat.StateHash);
                flags |= stat.Flags;
                rootLocal = input.RootLocalPosition;
                sectorX = input.RootSectorX;
                sectorY = input.RootSectorY;
                sectorZ = input.RootSectorZ;
            }

            float invActive = active > 0 ? math.rcp(math.max(active, 1)) : 0f;
            KineticAnimationTelemetryEntry entry = default;
            entry.RootSectorX = sectorX;
            entry.RootSectorY = sectorY;
            entry.RootSectorZ = sectorZ;
            entry.RootLocal = rootLocal;
            entry.Frame = Frame;
            entry.BonesEvaluated = bones;
            entry.AverageIkIterations = ik * invActive;
            entry.CpuTimeMicroseconds = cpuUs;
            entry.StateHash = hash;
            entry.Flags = invalid > 0 ? flags | KineticCharacterAnimatorConstants.TelemetryFlagInvalid : flags;
            entry.GlobalQualityWeight = quality * invActive;

            int write = KineticCharacterMath.PositiveModulo(Cursor[0], Telemetry.Length);
            Telemetry[write] = entry;
            Cursor[0] = write + 1;
        }
    }
}
