using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Animation.Locomotion
{
    internal static class LadderClimbIkConstants
    {
        public const int MaxActiveLadders = 32;
        public const int BlackBoxFrameCapacity = 300;
        public const float DefaultRungSpacingMeters = 0.3f;
        public const float DefaultUpperArmMeters = 0.34f;
        public const float DefaultLowerArmMeters = 0.36f;
        public const float MinLengthSq = 0.000001f;
        public const uint SourceHash = 0x4C43494Bu; // LCIK

        public const byte FlagActive = 1 << 0;
        public const byte FlagLowTier = 1 << 1;
        public const byte FlagVrGrip = 1 << 2;
        public const byte FlagSlip = 1 << 3;
        public const byte FlagInvalidInput = 1 << 4;
        public const byte FlagLeftLocked = 1 << 5;
        public const byte FlagRightLocked = 1 << 6;
        public const byte FlagUnreachable = 1 << 7;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct LadderClimbIkInput
    {
        public float3 PlayerRoot;
        public float3 LadderUp;
        public float3 LadderForward;
        public float3 LeftShoulder;
        public float3 RightShoulder;
        public float3 LeftPole;
        public float3 RightPole;
        public float ProgressMeters;
        public float LadderHeightMeters;
        public float RungSpacingMeters;
        public float UpperArmMeters;
        public float LowerArmMeters;
        public float Stamina01;
        public int LadderIndex;
        public int Frame;
        public byte Flags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct LadderClimbIkOutput
    {
        public float3 LeftHandTarget;
        public float3 RightHandTarget;
        public float3 LeftElbowTarget;
        public float3 RightElbowTarget;
        public float3 LadderBaseRuntime;
        public float Progress01;
        public float Stamina01;
        public int LeftRungIndex;
        public int RightRungIndex;
        public byte Flags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct LadderClimbTelemetryEntry
    {
        public float3 PlayerRoot;
        public float3 LeftHandTarget;
        public float3 RightHandTarget;
        public float3 LeftElbowTarget;
        public float3 RightElbowTarget;
        public float ProgressMeters;
        public float Stamina01;
        public int LeftRungIndex;
        public int RightRungIndex;
        public int Frame;
        public uint Hash;
        public byte Flags;
    }

    [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct LadderClimbIkSolveJob : IJob
    {
        [ReadOnly] public NativeArray<LadderClimbIkInput> Inputs;
        [ReadOnly] public NativeArray<AbsoluteUniversePosition> LadderAups;
        public NativeArray<LadderClimbIkOutput> Outputs;
        public NativeArray<LadderClimbTelemetryEntry> TelemetryRing;
        public NativeArray<int> TelemetryCursor;
        public double3 CommittedOriginOffset;

        public void Execute()
        {
            if (Inputs.Length <= 0 || Outputs.Length <= 0)
                return;

            LadderClimbIkInput input = Inputs[0];
            LadderClimbIkOutput output = default;
            byte flags = input.Flags;

            float spacing = SanitizePositive(input.RungSpacingMeters, LadderClimbIkConstants.DefaultRungSpacingMeters);
            float height = SanitizePositive(input.LadderHeightMeters, spacing);
            float upperArm = SanitizePositive(input.UpperArmMeters, LadderClimbIkConstants.DefaultUpperArmMeters);
            float lowerArm = SanitizePositive(input.LowerArmMeters, LadderClimbIkConstants.DefaultLowerArmMeters);
            float progress = math.clamp(SanitizeFinite(input.ProgressMeters, 0f), 0f, height);
            float stamina = math.clamp(SanitizeFinite(input.Stamina01, 1f), 0f, 1f);
            float3 ladderUp = NormalizeSafe(input.LadderUp, new float3(0f, 1f, 0f));
            float3 ladderForward = NormalizeSafe(ProjectOnPlane(input.LadderForward, ladderUp), ResolvePerpendicular(ladderUp));
            float handOffset = spacing * 0.5f;

            int ladderIndex = math.max(0, input.LadderIndex);
            if (LadderAups.Length <= ladderIndex)
            {
                flags |= LadderClimbIkConstants.FlagInvalidInput;
                output.Flags = flags;
                Outputs[0] = output;
                WriteTelemetry(in input, in output);
                return;
            }

            AbsoluteUniversePosition ladderAup = LadderAups[ladderIndex];
            float3 ladderBase = ToRuntimeFloat3(in ladderAup, CommittedOriginOffset);
            ladderBase = SanitizeFinite(ladderBase, input.PlayerRoot);

            int leftRung = ResolveRungIndex(progress + handOffset, spacing);
            int rightRung = ResolveRungIndex(progress, spacing);
            float3 leftHand = ResolveRungPosition(ladderBase, ladderUp, leftRung, spacing, height);
            float3 rightHand = ResolveRungPosition(ladderBase, ladderUp, rightRung, spacing, height);

            float3 leftShoulder = SanitizeFinite(input.LeftShoulder, input.PlayerRoot);
            float3 rightShoulder = SanitizeFinite(input.RightShoulder, input.PlayerRoot);
            float3 leftPole = SanitizeFinite(input.LeftPole, leftShoulder + ladderForward);
            float3 rightPole = SanitizeFinite(input.RightPole, rightShoulder + ladderForward);

            float3 leftElbow = SolveElbow(
                leftShoulder,
                leftHand,
                leftPole,
                upperArm,
                lowerArm,
                (flags & LadderClimbIkConstants.FlagLowTier) != 0,
                ref flags);

            float3 rightElbow = SolveElbow(
                rightShoulder,
                rightHand,
                rightPole,
                upperArm,
                lowerArm,
                (flags & LadderClimbIkConstants.FlagLowTier) != 0,
                ref flags);

            output.LeftHandTarget = leftHand;
            output.RightHandTarget = rightHand;
            output.LeftElbowTarget = leftElbow;
            output.RightElbowTarget = rightElbow;
            output.LadderBaseRuntime = ladderBase;
            output.Progress01 = math.saturate(progress * math.rcp(math.max(height, 0.0001f)));
            output.Stamina01 = stamina;
            output.LeftRungIndex = leftRung;
            output.RightRungIndex = rightRung;
            output.Flags = (byte)(flags | LadderClimbIkConstants.FlagLeftLocked | LadderClimbIkConstants.FlagRightLocked);
            Outputs[0] = output;
            WriteTelemetry(in input, in output);
        }

        private static int ResolveRungIndex(float progressMeters, float spacing)
        {
            float rungFloat = SanitizeFinite(progressMeters, 0f) * math.rcp(math.max(spacing, 0.0001f));
            return math.max(0, (int)math.round(rungFloat));
        }

        private static float3 ResolveRungPosition(float3 ladderBase, float3 ladderUp, int rungIndex, float spacing, float height)
        {
            float rungMeters = math.min(height, math.max(0, rungIndex) * spacing);
            return ladderBase + ladderUp * rungMeters;
        }

        private static float3 SolveElbow(
            float3 shoulder,
            float3 handTarget,
            float3 pole,
            float upperArm,
            float lowerArm,
            bool lowTier,
            ref byte flags)
        {
            float3 shoulderToHand = handTarget - shoulder;
            float distanceSq = math.lengthsq(shoulderToHand);
            float distance = SafeSqrt(distanceSq);
            float minReach = math.max(0.01f, math.abs(upperArm - lowerArm) + 0.001f);
            float maxReach = math.max(minReach + 0.001f, upperArm + lowerArm - 0.001f);
            float clampedDistance = math.clamp(distance, minReach, maxReach);
            if (math.abs(clampedDistance - distance) > 0.001f)
                flags |= LadderClimbIkConstants.FlagUnreachable;

            float3 handDirection = NormalizeSafe(shoulderToHand, new float3(0f, 1f, 0f));
            float3 poleDirection = NormalizeSafe(pole - shoulder, ResolvePerpendicular(handDirection));
            float3 bendDirection = poleDirection - handDirection * math.dot(poleDirection, handDirection);
            bendDirection = NormalizeSafe(bendDirection, ResolvePerpendicular(handDirection));

            if (lowTier)
            {
                float3 fakeElbow = math.lerp(shoulder, handTarget, 0.5f) + bendDirection * 0.08f;
                return SanitizeFinite(fakeElbow, shoulder + handDirection * (upperArm * 0.5f));
            }

            float numerator = (upperArm * upperArm) + (clampedDistance * clampedDistance) - (lowerArm * lowerArm);
            float denominator = math.max(0.0001f, 2f * upperArm * clampedDistance);
            float acosInput = math.clamp(numerator * math.rcp(denominator), -1f, 1f);
            float shoulderAngle = math.acos(acosInput);
            float3 upperDirection = (handDirection * math.cos(shoulderAngle)) + (bendDirection * math.sin(shoulderAngle));
            upperDirection = NormalizeSafe(upperDirection, handDirection);
            return shoulder + upperDirection * upperArm;
        }

        private void WriteTelemetry(in LadderClimbIkInput input, in LadderClimbIkOutput output)
        {
            if (TelemetryRing.Length <= 0)
                return;

            int cursor = 0;
            if (TelemetryCursor.Length > 0)
            {
                cursor = TelemetryCursor[0];
                TelemetryCursor[0] = PositiveModulo(cursor + 1, LadderClimbIkConstants.BlackBoxFrameCapacity);
            }

            int index = PositiveModulo(cursor, TelemetryRing.Length);
            TelemetryRing[index] = new LadderClimbTelemetryEntry
            {
                PlayerRoot = SanitizeFinite(input.PlayerRoot, float3.zero),
                LeftHandTarget = SanitizeFinite(output.LeftHandTarget, float3.zero),
                RightHandTarget = SanitizeFinite(output.RightHandTarget, float3.zero),
                LeftElbowTarget = SanitizeFinite(output.LeftElbowTarget, float3.zero),
                RightElbowTarget = SanitizeFinite(output.RightElbowTarget, float3.zero),
                ProgressMeters = SanitizeFinite(input.ProgressMeters, 0f),
                Stamina01 = math.clamp(SanitizeFinite(input.Stamina01, 0f), 0f, 1f),
                LeftRungIndex = output.LeftRungIndex,
                RightRungIndex = output.RightRungIndex,
                Frame = input.Frame,
                Hash = ComposeTelemetryHash(in output),
                Flags = output.Flags
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ComposeTelemetryHash(in LadderClimbIkOutput output)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, (uint)output.LeftRungIndex);
            hash = Mix(hash, (uint)output.RightRungIndex);
            hash = Mix(hash, math.asuint(output.Progress01));
            hash = Mix(hash, math.asuint(output.Stamina01));
            hash = Mix(hash, output.Flags);
            return hash != 0u ? hash : 1u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint hash, uint value)
        {
            return (hash ^ value) * 16777619u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PositiveModulo(int value, int length)
        {
            int safeLength = math.max(1, length);
            int result = value % safeLength;
            return result < 0 ? result + safeLength : result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SafeSqrt(float value)
        {
            return value > LadderClimbIkConstants.MinLengthSq && math.isfinite(value)
                ? value * math.rsqrt(value)
                : 0.001f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ToRuntimeFloat3(in AbsoluteUniversePosition position, double3 committedOriginOffset)
        {
            double3 absolute = position.ToAbsoluteDouble3();
            double3 runtime = absolute - committedOriginOffset;
            return new float3((float)runtime.x, (float)runtime.y, (float)runtime.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ProjectOnPlane(float3 value, float3 normal)
        {
            return value - normal * math.dot(value, normal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolvePerpendicular(float3 direction)
        {
            float3 axis = math.abs(direction.y) < 0.9f ? new float3(0f, 1f, 0f) : new float3(1f, 0f, 0f);
            return NormalizeSafe(math.cross(direction, axis), new float3(1f, 0f, 0f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (lengthSq <= LadderClimbIkConstants.MinLengthSq || !math.isfinite(lengthSq))
                return SanitizeFinite(fallback, new float3(0f, 1f, 0f));

            return value * math.rsqrt(lengthSq);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizePositive(float value, float fallback)
        {
            return value > 0.0001f && math.isfinite(value) ? value : fallback;
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
    }
}
