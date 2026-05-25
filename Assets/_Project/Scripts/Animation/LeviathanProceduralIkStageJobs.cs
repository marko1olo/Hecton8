using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Animation.IK
{
    public static class ProceduralIkMatrixConstants
    {
        public const int IkStateBytes = 32;
        public const int ProceduralBoneBytes = 64;
        public const int IkConfigBytes = 32;
        public const uint StateFlagActive = 1u << 0;
        public const uint StateFlagMockTarget = 1u << 1;
        public const uint StateFlagAupLocalized = 1u << 2;
    }

    public static class ProceduralIkMatrixLayout
    {
        public const int IkStateCurrentPosOffset = 0;
        public const int IkStateTargetPosOffset = 12;
        public const int IkStateBoneLengthOffset = 24;
        public const int IkStateFlagsOffset = 28;
        public const int ProceduralBoneMatrixOffset = 0;
        public const int IkConfigSpeciesHashOffset = 0;
        public const int IkConfigMaxIterationsOffset = 20;

        public static bool Validate()
        {
            return UnsafeUtility.SizeOf<IkStateDTO>() == ProceduralIkMatrixConstants.IkStateBytes &&
                   IkStateCurrentPosOffset == 0 &&
                   IkStateTargetPosOffset == 12 &&
                   IkStateBoneLengthOffset == 24 &&
                   IkStateFlagsOffset == 28 &&
                   UnsafeUtility.SizeOf<ProceduralBoneDTO>() == ProceduralIkMatrixConstants.ProceduralBoneBytes &&
                   ProceduralBoneMatrixOffset == 0 &&
                   UnsafeUtility.SizeOf<IkConfigDTO>() == ProceduralIkMatrixConstants.IkConfigBytes &&
                   IkConfigSpeciesHashOffset == 0 &&
                   IkConfigMaxIterationsOffset == 20;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ProceduralBoneDTO
    {
        [FieldOffset(0)] public float4x4 LocalToWorldMatrix;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct IkStateDTO
    {
        [FieldOffset(0)] public float3 CurrentPos;
        [FieldOffset(12)] public float3 TargetPos;
        [FieldOffset(24)] public float BoneLengthMeters;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct IkConfigDTO
    {
        [FieldOffset(0)] public uint SpeciesHash;
        [FieldOffset(4)] public float SineWaveAmplitudeMeters;
        [FieldOffset(8)] public float SineWaveSpeed;
        [FieldOffset(12)] public float MaxBendRadians;
        [FieldOffset(16)] public float BoneLengthMeters;
        [FieldOffset(20)] public int MaxFabrikIterations;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public float Reserved0;
    }

    #if UNITY_EDITOR
    public static class FaunaIkProfileCsvParser
    {
        private const uint FnvaOffsetBasis = 2166136261u;
        private const uint FnvaPrime = 16777619u;

        public static int Parse(ReadOnlySpan<byte> bytes, NativeArray<IkConfigDTO> output)
        {
            if (!output.IsCreated || output.Length <= 0 || bytes.Length <= 0)
                return 0;

            int rowStart = 0;
            int written = 0;
            bool headerSkipped = false;
            while (rowStart < bytes.Length && written < output.Length)
            {
                int rowEnd = rowStart;
                while (rowEnd < bytes.Length && bytes[rowEnd] != (byte)'\n' && bytes[rowEnd] != (byte)'\r')
                    rowEnd++;

                ReadOnlySpan<byte> row = Trim(bytes.Slice(rowStart, rowEnd - rowStart));
                if (row.Length > 0)
                {
                    if (!headerSkipped && LooksLikeHeader(row))
                    {
                        headerSkipped = true;
                    }
                    else if (TryParseRow(row, out IkConfigDTO config))
                    {
                        output[written++] = config;
                    }
                }

                rowStart = rowEnd + 1;
                while (rowStart < bytes.Length && (bytes[rowStart] == (byte)'\n' || bytes[rowStart] == (byte)'\r'))
                    rowStart++;
            }

            return written;
        }

        private static bool TryParseRow(ReadOnlySpan<byte> row, out IkConfigDTO config)
        {
            config = default;
            int cursor = 0;
            if (!TryReadField(row, ref cursor, out ReadOnlySpan<byte> species) || species.Length <= 0)
                return false;

            config.SpeciesHash = HashFnv1a(species);
            if (!TryReadFloat(row, ref cursor, out float amplitude))
                amplitude = 1f;
            if (!TryReadFloat(row, ref cursor, out float speed))
                speed = 0.55f;
            if (!TryReadFloat(row, ref cursor, out float maxBend))
                maxBend = 1.2217305f;
            if (!TryReadFloat(row, ref cursor, out float boneLength))
                boneLength = LeviathanTerrainIkConstants.DefaultSegmentLength;
            if (!TryReadInt(row, ref cursor, out int maxIterations))
                maxIterations = 8;

            config.SineWaveAmplitudeMeters = math.max(0f, LeviathanProceduralMath.SanitizeFinite(amplitude, 1f));
            config.SineWaveSpeed = math.max(0.01f, LeviathanProceduralMath.SanitizeFinite(speed, 0.55f));
            config.MaxBendRadians = math.clamp(LeviathanProceduralMath.SanitizeFinite(maxBend, 1.2217305f), 0.001f, math.PI);
            config.BoneLengthMeters = math.max(LeviathanTerrainIkConstants.MinSegmentLength, LeviathanProceduralMath.SanitizeFinite(boneLength, LeviathanTerrainIkConstants.DefaultSegmentLength));
            config.MaxFabrikIterations = math.clamp(maxIterations, 1, 8);
            config.Flags = 1u;
            return config.SpeciesHash != 0u;
        }

        private static bool TryReadFloat(ReadOnlySpan<byte> row, ref int cursor, out float value)
        {
            value = 0f;
            if (!TryReadField(row, ref cursor, out ReadOnlySpan<byte> field) || field.Length <= 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (field[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }
            else if (field[index] == (byte)'+')
            {
                index++;
            }

            float result = 0f;
            bool anyDigit = false;
            while (index < field.Length && IsDigit(field[index]))
            {
                result = result * 10f + (field[index] - (byte)'0');
                index++;
                anyDigit = true;
            }

            if (index < field.Length && field[index] == (byte)'.')
            {
                index++;
                float divisor = 10f;
                while (index < field.Length && IsDigit(field[index]))
                {
                    result += (field[index] - (byte)'0') / divisor;
                    divisor *= 10f;
                    index++;
                    anyDigit = true;
                }
            }

            if (!anyDigit)
                return false;

            value = sign * result;
            return math.isfinite(value);
        }

        private static bool TryReadInt(ReadOnlySpan<byte> row, ref int cursor, out int value)
        {
            value = 0;
            if (!TryReadField(row, ref cursor, out ReadOnlySpan<byte> field) || field.Length <= 0)
                return false;

            int index = 0;
            int sign = 1;
            if (field[index] == (byte)'-')
            {
                sign = -1;
                index++;
            }

            int result = 0;
            bool anyDigit = false;
            while (index < field.Length && IsDigit(field[index]))
            {
                result = result * 10 + (field[index] - (byte)'0');
                index++;
                anyDigit = true;
            }

            if (!anyDigit)
                return false;

            value = result * sign;
            return true;
        }

        private static bool TryReadField(ReadOnlySpan<byte> row, ref int cursor, out ReadOnlySpan<byte> field)
        {
            field = default;
            if (cursor > row.Length)
                return false;

            int start = cursor;
            while (cursor < row.Length && row[cursor] != (byte)',')
                cursor++;

            field = Trim(row.Slice(start, cursor - start));
            if (cursor < row.Length && row[cursor] == (byte)',')
                cursor++;

            return true;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && IsWhitespace(value[start]))
                start++;
            while (end >= start && IsWhitespace(value[end]))
                end--;

            return start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool LooksLikeHeader(ReadOnlySpan<byte> row)
        {
            ReadOnlySpan<byte> trimmed = Trim(row);
            return trimmed.Length >= 7 &&
                   ToLowerAscii(trimmed[0]) == (byte)'s' &&
                   ToLowerAscii(trimmed[1]) == (byte)'p' &&
                   ToLowerAscii(trimmed[2]) == (byte)'e' &&
                   ToLowerAscii(trimmed[3]) == (byte)'c' &&
                   ToLowerAscii(trimmed[4]) == (byte)'i' &&
                   ToLowerAscii(trimmed[5]) == (byte)'e' &&
                   ToLowerAscii(trimmed[6]) == (byte)'s';
        }

        private static uint HashFnv1a(ReadOnlySpan<byte> value)
        {
            uint hash = FnvaOffsetBasis;
            for (int i = 0; i < value.Length; i++)
            {
                byte c = ToLowerAscii(value[i]);
                if (c == (byte)' ' || c == (byte)'\t')
                    c = (byte)'_';
                hash = (hash ^ c) * FnvaPrime;
            }

            return hash;
        }

        private static bool IsDigit(byte value)
        {
            return value >= (byte)'0' && value <= (byte)'9';
        }

        private static bool IsWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t';
        }

        private static byte ToLowerAscii(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }
    }
    #endif

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct LeviathanMockTargetDTO
    {
        [FieldOffset(0)] public double3 TargetAup;
        [FieldOffset(24)] public uint SectorHash;
        [FieldOffset(28)] public int FrameIndex;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    public struct MockLeviathanTargetJob : IJob
    {
        [NoAlias] public NativeArray<LeviathanMockTargetDTO> TargetOutput;
        public double3 RootAup;
        public uint SectorHash;
        public int SimulationFrame;
        public double SimulationTickDelta;
        public double OrbitRadiusMeters;
        public double VerticalAmplitudeMeters;

        public void Execute()
        {
            if (!TargetOutput.IsCreated || TargetOutput.Length <= 0)
                return;

            double safeTickDelta = math.select(0.016666666666666666d, math.min(math.max(SimulationTickDelta, 0.001d), 0.05d), math.isfinite(SimulationTickDelta) && SimulationTickDelta > 0d);
            double radius = math.select(18d, math.max(1d, OrbitRadiusMeters), math.isfinite(OrbitRadiusMeters) && OrbitRadiusMeters > 0d);
            double vertical = math.select(4d, math.max(0d, VerticalAmplitudeMeters), math.isfinite(VerticalAmplitudeMeters) && VerticalAmplitudeMeters >= 0d);
            uint seed = (SectorHash ^ (uint)math.max(0, SimulationFrame) * 747796405u) | 1u;
            double seedPhase = (seed & 1023u) * 0.006135923151542565d;
            double phase = SimulationFrame * safeTickDelta * 0.47d + seedPhase;
            LeviathanProceduralMath.ApproxSinCosRadians((float)phase, out float phaseSin, out float phaseCos);
            float verticalSin = LeviathanProceduralMath.ApproxSinRadians((float)(phase * 0.37d));
            double3 target = RootAup + new double3(
                phaseCos * radius,
                verticalSin * vertical,
                phaseSin * radius);

            LeviathanMockTargetDTO dto = default;
            dto.TargetAup = math.all(math.isfinite(target)) ? target : RootAup;
            dto.SectorHash = SectorHash;
            dto.FrameIndex = SimulationFrame;
            TargetOutput[0] = dto;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    public struct GenerateMockIkTargetsJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<IkStateDTO> States;
        public double3 RootAup;
        public double3 TargetAup;
        public float SimulationTimeSeconds;
        public float GlobalQualityWeight;
        public float FigureEightRadiusMeters;
        public float VerticalAmplitudeMeters;
        public float DefaultBoneLengthMeters;
        public int ChainLength;
        public uint Flags;

        public void Execute(int index)
        {
            if (!States.IsCreated || (uint)index >= (uint)States.Length)
                return;

            float quality = LeviathanProceduralMath.Smooth01(GlobalQualityWeight);
            int chainLength = math.max(1, ChainLength);
            int boneIndex = index % chainLength;
            float normalizedBone = boneIndex * math.rcp(math.max(1, chainLength - 1));
            float baseLength = math.max(LeviathanTerrainIkConstants.MinSegmentLength, LeviathanProceduralMath.SanitizeFinite(DefaultBoneLengthMeters, LeviathanTerrainIkConstants.DefaultSegmentLength));
            double3 localTargetDouble = TargetAup - RootAup;
            bool finiteAup = math.all(math.isfinite(RootAup)) &&
                             math.all(math.isfinite(TargetAup)) &&
                             math.all(math.isfinite(localTargetDouble)) &&
                             math.all(math.abs(localTargetDouble) < new double3(262144d));
            float3 localTarget = finiteAup
                ? new float3((float)localTargetDouble.x, (float)localTargetDouble.y, (float)localTargetDouble.z)
                : new float3(0f, 0f, baseLength * chainLength);
            float radius = math.max(0f, LeviathanProceduralMath.SanitizeFinite(FigureEightRadiusMeters, 2f)) * math.lerp(0.15f, 1f, quality);
            float vertical = math.max(0f, LeviathanProceduralMath.SanitizeFinite(VerticalAmplitudeMeters, 0.5f)) * math.lerp(0.2f, 1f, quality);
            float phase = SimulationTimeSeconds * math.lerp(0.35f, 1.45f, quality) + boneIndex * 0.173f;
            float sinA = LeviathanProceduralMath.CheapSinSigned(phase);
            float sinB = LeviathanProceduralMath.CheapSinSigned(phase * 2.0f + 0.25f);
            float taper = normalizedBone * normalizedBone;
            float3 dearLieOffset = new float3(
                sinA * radius * taper,
                sinB * vertical * taper,
                sinA * sinB * radius * 0.35f * taper);

            IkStateDTO state = States[index];
            state.TargetPos = LeviathanProceduralMath.SanitizeFinite(localTarget + dearLieOffset, localTarget);
            state.BoneLengthMeters = math.max(LeviathanTerrainIkConstants.MinSegmentLength, LeviathanProceduralMath.SanitizeFinite(state.BoneLengthMeters, baseLength));
            state.Flags = Flags | ProceduralIkMatrixConstants.StateFlagActive | ProceduralIkMatrixConstants.StateFlagMockTarget | ProceduralIkMatrixConstants.StateFlagAupLocalized;
            States[index] = state;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    public unsafe struct EvaluateProceduralIkJob : IJobParallelFor
    {
        /*
         * SAFETY CONTRACT:
         * Each Execute(index) owns one contiguous chain: [index * max(ChainStride, ChainLength), +ChainLength).
         * The NativeArray safety system cannot express this disjoint strided ownership, so parallel-for write
         * restriction is disabled. Callers must keep ChainStride >= ChainLength; this job enforces that clamp
         * before address calculation and never writes outside States.Length.
         *
         * WRITE ROUTE:
         * Only CurrentPos is mutated during FABRIK. TargetPos/BoneLengthMeters/Flags are read as immutable
         * inputs for the chain solve. No global state, scene graph, managed object, or allocation route exists
         * in this job; all memory is caller-owned NativeArray storage.
         *
         * FAILURE MODE:
         * Invalid lengths, NaN positions, or unreachable targets collapse to a straight chain from the
         * sanitized root toward the sanitized target. That keeps matrices finite and leaves crash evidence to
         * the owner's black-box telemetry instead of producing undefined GPU payload.
         */
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<IkStateDTO> States;
        public float3 RootLocalPosition;
        public float GlobalQualityWeight;
        public float DefaultBoneLengthMeters;
        public float ToleranceMeters;
        public int ChainLength;
        public int ChainStride;
        public int MaxIterations;

        public void Execute(int chainIndex)
        {
            if (!States.IsCreated || States.Length <= 1 || chainIndex < 0)
                return;

            int chainLength = math.max(2, ChainLength);
            int stride = math.max(chainLength, ChainStride);
            int start = chainIndex * stride;
            if ((uint)start >= (uint)States.Length)
                return;

            int count = math.min(chainLength, States.Length - start);
            if (count < 2)
                return;

            int end = start + count - 1;
            float quality = LeviathanProceduralMath.Smooth01(GlobalQualityWeight);
            int authoredMax = math.clamp(MaxIterations <= 0 ? 8 : MaxIterations, 1, 8);
            int iterations = math.clamp((int)math.round(math.lerp(1f, authoredMax, quality)), 1, 8);
            float defaultLength = math.max(LeviathanTerrainIkConstants.MinSegmentLength, LeviathanProceduralMath.SanitizeFinite(DefaultBoneLengthMeters, LeviathanTerrainIkConstants.DefaultSegmentLength));
            float3 root = LeviathanProceduralMath.SanitizeFinite(RootLocalPosition, ElementAt(start).CurrentPos);
            float3 target = LeviathanProceduralMath.SanitizeFinite(ElementAt(end).TargetPos, ElementAt(end).CurrentPos);
            float totalLength = ResolveTotalLength(start + 1, end, defaultLength);
            float3 rootToTarget = target - root;
            float targetDistanceSq = math.lengthsq(rootToTarget);

            if (!math.isfinite(targetDistanceSq) || targetDistanceSq <= 0.000001f)
            {
                target = root + new float3(0f, 0f, defaultLength);
                rootToTarget = target - root;
                targetDistanceSq = math.lengthsq(rootToTarget);
            }

            if (targetDistanceSq >= totalLength * totalLength)
            {
                float3 direction = LeviathanProceduralMath.NormalizeSafe(rootToTarget, new float3(0f, 0f, 1f));
                float cursor = 0f;
                ElementAt(start).CurrentPos = root;
                for (int i = start + 1; i <= end; i++)
                {
                    cursor += ResolveBoneLength(i, defaultLength);
                    ElementAt(i).CurrentPos = root + direction * cursor;
                }

                return;
            }

            float toleranceSq = math.max(0.000001f, LeviathanProceduralMath.SanitizeFinite(ToleranceMeters, 0.025f) * LeviathanProceduralMath.SanitizeFinite(ToleranceMeters, 0.025f));
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                ElementAt(end).CurrentPos = target;
                for (int i = end - 1; i >= start; i--)
                {
                    float3 child = LeviathanProceduralMath.SanitizeFinite(ElementAt(i + 1).CurrentPos, target);
                    float3 current = LeviathanProceduralMath.SanitizeFinite(ElementAt(i).CurrentPos, child);
                    float length = ResolveBoneLength(i + 1, defaultLength);
                    ElementAt(i).CurrentPos = child + LeviathanProceduralMath.NormalizeSafe(current - child, new float3(0f, 0f, -1f)) * length;
                }

                ElementAt(start).CurrentPos = root;
                for (int i = start + 1; i <= end; i++)
                {
                    float3 parent = LeviathanProceduralMath.SanitizeFinite(ElementAt(i - 1).CurrentPos, root);
                    float3 current = LeviathanProceduralMath.SanitizeFinite(ElementAt(i).CurrentPos, parent);
                    float length = ResolveBoneLength(i, defaultLength);
                    ElementAt(i).CurrentPos = parent + LeviathanProceduralMath.NormalizeSafe(current - parent, new float3(0f, 0f, 1f)) * length;
                }

                float errorSq = math.lengthsq(ElementAt(end).CurrentPos - target);
                if (math.isfinite(errorSq) && errorSq <= toleranceSq)
                    break;
            }
        }

        private unsafe ref IkStateDTO ElementAt(int index)
        {
            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(States);
            return ref UnsafeUtility.AsRef<IkStateDTO>(ptr + UnsafeUtility.SizeOf<IkStateDTO>() * index);
        }

        private float ResolveBoneLength(int index, float fallback)
        {
            float length = ElementAt(index).BoneLengthMeters;
            return math.max(LeviathanTerrainIkConstants.MinSegmentLength, LeviathanProceduralMath.SanitizeFinite(length, fallback));
        }

        private float ResolveTotalLength(int start, int end, float fallback)
        {
            float total = 0f;
            for (int i = start; i <= end; i++)
                total += ResolveBoneLength(i, fallback);

            return math.max(fallback, total);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    public struct CalculateBoneMatricesJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<IkStateDTO> States;
        [NoAlias] public NativeArray<ProceduralBoneDTO> OutputMatrices;
        public float3 Up;
        public float RadiusMeters;
        public float DefaultBoneLengthMeters;
        public int ChainLength;
        public int ActiveBoneCount;

        public void Execute(int index)
        {
            if (!States.IsCreated ||
                !OutputMatrices.IsCreated ||
                (uint)index >= (uint)States.Length ||
                (uint)index >= (uint)OutputMatrices.Length)
            {
                return;
            }

            int activeCount = ActiveBoneCount > 0
                ? math.min(ActiveBoneCount, math.min(States.Length, OutputMatrices.Length))
                : math.min(States.Length, OutputMatrices.Length);
            if (index >= activeCount)
                return;

            int chainLength = math.max(2, ChainLength);
            int chainStart = (index / chainLength) * chainLength;
            int chainEnd = math.min(activeCount - 1, chainStart + chainLength - 1);
            float3 position = LeviathanProceduralMath.SanitizeFinite(States[index].CurrentPos, float3.zero);
            float3 fallbackForward = new float3(0f, 0f, 1f);
            float3 next = index < chainEnd
                ? LeviathanProceduralMath.SanitizeFinite(States[index + 1].CurrentPos, position + fallbackForward)
                : position + LeviathanProceduralMath.NormalizeSafe(position - LeviathanProceduralMath.SanitizeFinite(States[math.max(chainStart, index - 1)].CurrentPos, position - fallbackForward), fallbackForward);
            float3 forward = LeviathanProceduralMath.NormalizeSafe(next - position, fallbackForward);
            float3 requestedUp = LeviathanProceduralMath.NormalizeSafe(Up, new float3(0f, 1f, 0f));
            float3 projectedUp = requestedUp - forward * math.dot(requestedUp, forward);
            projectedUp = LeviathanProceduralMath.NormalizeSafe(projectedUp, math.abs(forward.y) < 0.9f ? new float3(0f, 1f, 0f) : new float3(1f, 0f, 0f));
            float length = ResolveMatrixLength(index, next, position);
            float radius = math.max(0.001f, LeviathanProceduralMath.SanitizeFinite(RadiusMeters, 0.1f));
            ProceduralBoneDTO dto = default;
            dto.LocalToWorldMatrix = float4x4.TRS(position, quaternion.LookRotationSafe(forward, projectedUp), new float3(radius, radius, length));
            OutputMatrices[index] = dto;
        }

        private float ResolveMatrixLength(int index, float3 next, float3 position)
        {
            float fallback = math.max(LeviathanTerrainIkConstants.MinSegmentLength, LeviathanProceduralMath.SanitizeFinite(DefaultBoneLengthMeters, LeviathanTerrainIkConstants.DefaultSegmentLength));
            float authored = States[index].BoneLengthMeters;
            if (math.isfinite(authored) && authored >= LeviathanTerrainIkConstants.MinSegmentLength)
                return authored;

            float measuredSq = math.lengthsq(next - position);
            return math.isfinite(measuredSq) && measuredSq > 0.000001f
                ? measuredSq * math.rsqrt(measuredSq)
                : fallback;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    public struct ProceduralSpineMotionJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<float3> SegmentPositions;
        [ReadOnly, NoAlias] public NativeArray<LeviathanBoneConstraintsDTO> BoneConstraints;
        public float SimulationTimeSeconds;
        public float ForwardVelocityMetersPerSecond;
        public float GlobalQualityWeight;
        public float BaseAmplitudeMeters;
        public float WaveFrequencyHz;
        public float PhaseOffset;
        public float3 SideAxis;
        public int ActiveSegmentCount;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)SegmentPositions.Length || index >= ActiveSegmentCount)
                return;

            float quality = LeviathanProceduralMath.Smooth01(GlobalQualityWeight);
            float speed = math.max(0f, LeviathanProceduralMath.SanitizeFinite(ForwardVelocityMetersPerSecond, 0f));
            float amplitude = math.max(0f, LeviathanProceduralMath.SanitizeFinite(BaseAmplitudeMeters, 0.25f)) *
                math.lerp(0.2f, 1f, quality) *
                math.saturate(speed * 0.2f + 0.15f);
            float frequency = math.max(0.01f, LeviathanProceduralMath.SanitizeFinite(WaveFrequencyHz, 0.6f));
            float phaseOffset = math.max(0f, LeviathanProceduralMath.SanitizeFinite(PhaseOffset, 0.45f));
            float phase = SimulationTimeSeconds * frequency - index * phaseOffset;
            float wave = LeviathanProceduralMath.CheapSinSigned(phase);
            float taper = index * math.rcp(math.max(1, ActiveSegmentCount - 1));
            float3 side = LeviathanProceduralMath.NormalizeSafe(SideAxis, new float3(1f, 0f, 0f));
            float3 position = LeviathanProceduralMath.SanitizeFinite(SegmentPositions[index], float3.zero);
            SegmentPositions[index] = position + side * (wave * amplitude * taper);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    public struct InverseKinematicsFABRIKJob : IJob
    {
        [NoAlias] public NativeArray<float3> ChainPositions;
        [ReadOnly, NoAlias] public NativeArray<LeviathanBoneConstraintsDTO> BoneConstraints;
        public float3 RootPosition;
        public float3 TargetPosition;
        public float GlobalQualityWeight;
        public float DefaultSegmentLength;
        public float ToleranceMeters;
        public int ChainStartIndex;
        public int ChainCount;

        public void Execute()
        {
            if (!ChainPositions.IsCreated || ChainPositions.Length <= 1)
                return;

            int start = math.clamp(ChainStartIndex, 0, ChainPositions.Length - 1);
            int count = math.clamp(ChainCount, 2, ChainPositions.Length - start);
            int end = start + count - 1;
            int iterations = math.clamp((int)math.round(math.lerp(1f, 10f, LeviathanProceduralMath.Smooth01(GlobalQualityWeight))), 1, 10);
            float toleranceSq = math.max(0.000001f, ToleranceMeters * ToleranceMeters);
            float3 root = LeviathanProceduralMath.SanitizeFinite(RootPosition, ChainPositions[start]);
            float3 target = LeviathanProceduralMath.SanitizeFinite(TargetPosition, ChainPositions[end]);
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                ChainPositions[end] = target;
                for (int i = end - 1; i >= start; i--)
                {
                    float3 child = LeviathanProceduralMath.SanitizeFinite(ChainPositions[i + 1], target);
                    float3 current = LeviathanProceduralMath.SanitizeFinite(ChainPositions[i], child);
                    float length = ResolveLength(i + 1);
                    ChainPositions[i] = child + LeviathanProceduralMath.NormalizeSafe(current - child, new float3(0f, 0f, -1f)) * length;
                }

                ChainPositions[start] = root;
                for (int i = start + 1; i <= end; i++)
                {
                    float3 parent = LeviathanProceduralMath.SanitizeFinite(ChainPositions[i - 1], root);
                    float3 current = LeviathanProceduralMath.SanitizeFinite(ChainPositions[i], parent);
                    float length = ResolveLength(i);
                    ChainPositions[i] = parent + LeviathanProceduralMath.NormalizeSafe(current - parent, new float3(0f, 0f, 1f)) * length;
                }

                float errorSq = math.lengthsq(ChainPositions[end] - target);
                if (math.isfinite(errorSq) && errorSq <= toleranceSq)
                    break;
            }
        }

        private float ResolveLength(int index)
        {
            if (BoneConstraints.IsCreated && (uint)index < (uint)BoneConstraints.Length)
                return math.max(LeviathanTerrainIkConstants.MinSegmentLength, BoneConstraints[index].SegmentLengthMeters);

            return math.max(LeviathanTerrainIkConstants.MinSegmentLength, DefaultSegmentLength);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    public struct SecondaryMotionSpringJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<float3> BonePositions;
        [NoAlias] public NativeArray<float3> BoneVelocities;
        [ReadOnly, NoAlias] public NativeArray<LeviathanBoneConstraintsDTO> BoneConstraints;
        public float DeltaTime;
        public float GlobalQualityWeight;
        public float SpringStrength;
        public float Damping;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)BonePositions.Length || index == 0)
                return;

            float dt = math.select(0f, math.min(DeltaTime, 0.05f), math.isfinite(DeltaTime) && DeltaTime > 0f);
            float spring = math.max(0f, LeviathanProceduralMath.SanitizeFinite(SpringStrength, 18f)) *
                math.lerp(0.25f, 1f, LeviathanProceduralMath.Smooth01(GlobalQualityWeight));
            float damping = math.saturate(LeviathanProceduralMath.SanitizeFinite(Damping, 0.82f));
            float3 parent = LeviathanProceduralMath.SanitizeFinite(BonePositions[index - 1], float3.zero);
            float3 current = LeviathanProceduralMath.SanitizeFinite(BonePositions[index], parent);
            float length = BoneConstraints.IsCreated && (uint)index < (uint)BoneConstraints.Length
                ? math.max(LeviathanTerrainIkConstants.MinSegmentLength, BoneConstraints[index].SegmentLengthMeters)
                : LeviathanTerrainIkConstants.DefaultSegmentLength;
            float3 target = parent + LeviathanProceduralMath.NormalizeSafe(current - parent, new float3(0f, 0f, -1f)) * length;
            float3 velocity = LeviathanProceduralMath.SanitizeFinite(BoneVelocities[index], float3.zero);
            velocity = (velocity + (target - current) * spring * dt) * damping;
            BoneVelocities[index] = velocity;
            BonePositions[index] = LeviathanProceduralMath.SanitizeFinite(current + velocity * dt, target);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    public struct ComputeFinalBoneMatricesJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<float3> BonePositions;
        [ReadOnly, NoAlias] public NativeArray<LeviathanBoneConstraintsDTO> BoneConstraints;
        [NoAlias] public NativeArray<LeviathanBoneDTO> BoneMatrices;
        public float3 Up;
        public float BodyRadius;
        public float DefaultSegmentLength;
        public int ActiveBoneCount;

        public void Execute()
        {
            if (!BonePositions.IsCreated || !BoneMatrices.IsCreated)
                return;

            int count = math.clamp(ActiveBoneCount, 1, math.min(BonePositions.Length, BoneMatrices.Length));
            float3 up = LeviathanProceduralMath.NormalizeSafe(Up, new float3(0f, 1f, 0f));
            float3 lastForward = new float3(0f, 0f, 1f);
            for (int i = 0; i < count; i++)
            {
                float3 position = LeviathanProceduralMath.SanitizeFinite(BonePositions[i], float3.zero);
                float3 next = i + 1 < count ? LeviathanProceduralMath.SanitizeFinite(BonePositions[i + 1], position + lastForward) : position + lastForward;
                float3 forward = LeviathanProceduralMath.NormalizeSafe(next - position, lastForward);
                lastForward = forward;
                float length = BoneConstraints.IsCreated && (uint)i < (uint)BoneConstraints.Length
                    ? math.max(LeviathanTerrainIkConstants.MinSegmentLength, BoneConstraints[i].SegmentLengthMeters)
                    : math.max(LeviathanTerrainIkConstants.MinSegmentLength, DefaultSegmentLength);
                float radius = math.max(0.01f, BodyRadius);
                LeviathanBoneDTO dto = default;
                dto.LocalToWorld = float4x4.TRS(position, quaternion.LookRotationSafe(forward, up), new float3(radius, radius, length));
                BoneMatrices[i] = dto;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    public struct StageCreatureCollidersJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<LeviathanBoneDTO> BoneMatrices;
        [NoAlias] public NativeArray<LeviathanCapsuleColliderDTO> ColliderProxies;
        public float BodyRadius;
        public uint OwnerHash;
        public int FrameIndex;
        public int ActiveBoneCount;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)BoneMatrices.Length ||
                (uint)index >= (uint)ColliderProxies.Length ||
                index >= ActiveBoneCount)
            {
                return;
            }

            float4x4 matrix = BoneMatrices[index].LocalToWorld;
            float3 center = new float3(matrix.c3.x, matrix.c3.y, matrix.c3.z);
            float3 axisRaw = new float3(matrix.c2.x, matrix.c2.y, matrix.c2.z);
            float3 axis = LeviathanProceduralMath.NormalizeSafe(axisRaw, new float3(0f, 0f, 1f));
            float radius = math.max(0.01f, BodyRadius);
            float halfHeight = math.max(radius, LeviathanProceduralMath.ResolveLength(axisRaw) * 0.5f);
            LeviathanCapsuleColliderDTO collider = default;
            collider.Center = LeviathanProceduralMath.SanitizeFinite(center, float3.zero);
            collider.Radius = radius;
            collider.Axis = axis;
            collider.HalfHeight = halfHeight;
            collider.OwnerHash = OwnerHash;
            collider.Flags = 1u;
            collider.BoneIndex = index;
            collider.FrameIndex = FrameIndex;
            collider.AabbExtents = math.abs(axis) * halfHeight + new float3(radius);
            ColliderProxies[index] = collider;
        }
    }

    internal static class LeviathanProceduralMath
    {
        public static float Smooth01(float value)
        {
            float t = math.saturate(math.select(1f, value, math.isfinite(value)));
            return t * t * (3f - 2f * t);
        }

        public static float CheapSinSigned(float phase)
        {
            float wrapped = phase - math.floor(phase);
            float tri = 1f - math.abs(wrapped * 2f - 1f);
            return (tri * 2f - 1f) * (1f - 0.225f * tri * tri);
        }

        public static float ApproxSinRadians(float radians)
        {
            const float epsilon = 0.000001f;
            float angle = math.select(0f, radians, math.isfinite(radians));
            float cycle = angle * 0.15915494309189535f;
            float wrapped = cycle - math.floor(cycle);
            float x = wrapped * (2f * math.PI);
            float mirrored = math.select(x, (2f * math.PI) - x, x > math.PI);
            float sign = math.select(1f, -1f, x > math.PI);
            float shape = mirrored * (math.PI - mirrored);
            float denominator = math.max(epsilon, (5f * math.PI * math.PI) - (4f * shape));
            float sine = sign * (16f * shape) * math.rcp(denominator);
            return math.clamp(math.select(0f, sine, math.isfinite(sine)), -1f, 1f);
        }

        public static float ApproxCosRadians(float radians)
        {
            return ApproxSinRadians(radians + (0.5f * math.PI));
        }

        public static void ApproxSinCosRadians(float radians, out float sine, out float cosine)
        {
            sine = ApproxSinRadians(radians);
            cosine = ApproxCosRadians(radians);
        }

        public static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        public static float3 SanitizeFinite(float3 value, float3 fallback)
        {
            return math.all(math.isfinite(value)) ? value : fallback;
        }

        public static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return math.isfinite(lengthSq) && lengthSq > 0.000001f
                ? value * math.rsqrt(lengthSq)
                : fallback;
        }

        public static float ResolveLength(float3 value)
        {
            float lengthSq = math.lengthsq(value);
            return math.isfinite(lengthSq) && lengthSq > 0.000001f
                ? lengthSq * math.rsqrt(lengthSq)
                : LeviathanTerrainIkConstants.DefaultSegmentLength;
        }
    }
}
