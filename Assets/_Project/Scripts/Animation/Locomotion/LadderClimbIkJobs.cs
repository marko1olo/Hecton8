using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.World;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Animation.Locomotion
{
    internal static class LadderClimbIkConstants
    {
        public const int MaxActiveLadders = 32;
        public const int BlackBoxFrameCapacity = 300;
        public const int TelemetryCursorElementCount = 2;
        public const int TelemetryCursorNextWriteIndex = 0;
        public const int TelemetryCursorRetainedCountIndex = 1;
        public const float DefaultRungSpacingMeters = 0.3f;
        public const float DefaultUpperArmMeters = 0.34f;
        public const float DefaultLowerArmMeters = 0.36f;
        public const float MinLengthSq = 0.000001f;
        public const uint SourceHash = 0x4C43494Bu; // LCIK

        public const uint FlagActive = 1u << 0;
        public const uint FlagLowTier = 1u << 1;
        public const uint FlagVrGrip = 1u << 2;
        public const uint FlagSlip = 1u << 3;
        public const uint FlagInvalidInput = 1u << 4;
        public const uint FlagLeftLocked = 1u << 5;
        public const uint FlagRightLocked = 1u << 6;
        public const uint FlagUnreachable = 1u << 7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct LadderClimbIkInput
    {
        [FieldOffset(0)] public float3 PlayerRoot;
        [FieldOffset(12)] public float3 LadderUp;
        [FieldOffset(24)] public float3 LadderForward;
        [FieldOffset(36)] public float3 LeftShoulder;
        [FieldOffset(48)] public float3 RightShoulder;
        [FieldOffset(60)] public float3 LeftPole;
        [FieldOffset(72)] public float3 RightPole;
        [FieldOffset(84)] public float ProgressMeters;
        [FieldOffset(88)] public float LadderHeightMeters;
        [FieldOffset(92)] public float RungSpacingMeters;
        [FieldOffset(96)] public float UpperArmMeters;
        [FieldOffset(100)] public float LowerArmMeters;
        [FieldOffset(104)] public float Stamina01;
        [FieldOffset(108)] public int LadderIndex;
        [FieldOffset(112)] public int Frame;
        [FieldOffset(116)] public uint Flags;
        [FieldOffset(120)] private ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct LadderClimbIkOutput
    {
        [FieldOffset(0)] public float3 LeftHandTarget;
        [FieldOffset(12)] public float3 RightHandTarget;
        [FieldOffset(24)] public float3 LeftElbowTarget;
        [FieldOffset(36)] public float3 RightElbowTarget;
        [FieldOffset(48)] public float3 LadderBaseRuntime;
        [FieldOffset(60)] public float Progress01;
        [FieldOffset(64)] public float Stamina01;
        [FieldOffset(68)] public int LeftRungIndex;
        [FieldOffset(72)] public int RightRungIndex;
        [FieldOffset(76)] public uint Flags;
        [FieldOffset(80)] private ulong _pad0;
        [FieldOffset(88)] private ulong _pad1;
        [FieldOffset(96)] private ulong _pad2;
        [FieldOffset(104)] private ulong _pad3;
        [FieldOffset(112)] private ulong _pad4;
        [FieldOffset(120)] private ulong _pad5;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct LadderClimbTelemetryEntry
    {
        [FieldOffset(0)] public float3 PlayerRoot;
        [FieldOffset(12)] public float3 LeftHandTarget;
        [FieldOffset(24)] public float3 RightHandTarget;
        [FieldOffset(36)] public float3 LeftElbowTarget;
        [FieldOffset(48)] public float3 RightElbowTarget;
        [FieldOffset(60)] public float ProgressMeters;
        [FieldOffset(64)] public float Stamina01;
        [FieldOffset(68)] public int LeftRungIndex;
        [FieldOffset(72)] public int RightRungIndex;
        [FieldOffset(76)] public int Frame;
        [FieldOffset(80)] public uint Hash;
        [FieldOffset(84)] public uint Flags;
        [FieldOffset(88)] private ulong _pad0;
        [FieldOffset(96)] private ulong _pad1;
        [FieldOffset(104)] private ulong _pad2;
        [FieldOffset(112)] private ulong _pad3;
        [FieldOffset(120)] private ulong _pad4;
    }

    internal struct LadderClimbIkVaultViews
    {
        public NativeArray<LadderClimbIkInput> Inputs;
        public NativeArray<LadderClimbIkOutput> Outputs;
        public NativeArray<AbsoluteUniversePosition> LadderAups;
        public NativeArray<LadderClimbTelemetryEntry> TelemetryRing;
        public NativeArray<int> TelemetryCursor;

        public bool HasSolveCapacity =>
            Inputs.IsCreated &&
            Outputs.IsCreated &&
            LadderAups.IsCreated &&
            TelemetryRing.IsCreated &&
            TelemetryCursor.IsCreated &&
            Inputs.Length >= 1 &&
            Outputs.Length >= 1 &&
            LadderAups.Length >= 1 &&
            TelemetryRing.Length >= LadderClimbIkConstants.BlackBoxFrameCapacity &&
            TelemetryCursor.Length >= LadderClimbIkConstants.TelemetryCursorElementCount;

        public bool HasOutput => Outputs.IsCreated && Outputs.Length >= 1;
        public bool HasLadderAup => LadderAups.IsCreated && LadderAups.Length >= 1;
        public bool HasTelemetry => TelemetryRing.IsCreated && TelemetryRing.Length > 0;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct LadderClimbIkSolveJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<LadderClimbIkInput> Inputs;
        [ReadOnly, NoAlias] public NativeArray<AbsoluteUniversePosition> LadderAups;
        [NoAlias] public NativeArray<LadderClimbIkOutput> Outputs;
        [NoAlias] public NativeArray<LadderClimbTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        public double3 CommittedOriginOffset;

        public void Execute()
        {
            if (Inputs.Length <= 0 || Outputs.Length <= 0)
                return;

            LadderClimbIkInput input = Inputs[0];
            LadderClimbIkOutput output = default;
            uint flags = input.Flags;

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
            output.Flags = flags | LadderClimbIkConstants.FlagLeftLocked | LadderClimbIkConstants.FlagRightLocked;
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
            ref uint flags)
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

            int capacity = math.min(TelemetryRing.Length, LadderClimbIkConstants.BlackBoxFrameCapacity);
            int cursor = 0;
            int retainedCount = 0;
            if (TelemetryCursor.Length >= LadderClimbIkConstants.TelemetryCursorElementCount)
            {
                cursor = PositiveModulo(TelemetryCursor[LadderClimbIkConstants.TelemetryCursorNextWriteIndex], capacity);
                retainedCount = math.clamp(TelemetryCursor[LadderClimbIkConstants.TelemetryCursorRetainedCountIndex], 0, capacity);
                TelemetryCursor[LadderClimbIkConstants.TelemetryCursorNextWriteIndex] = PositiveModulo(cursor + 1, capacity);
                TelemetryCursor[LadderClimbIkConstants.TelemetryCursorRetainedCountIndex] = math.min(retainedCount + 1, capacity);
            }

            int index = PositiveModulo(cursor, capacity);
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
