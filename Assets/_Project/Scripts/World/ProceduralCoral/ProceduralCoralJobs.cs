using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World.ProceduralCoral
{
    internal static class ProceduralCoralMath
    {
        public static float Smooth01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - (2f * t));
        }

        public static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }

        public static uint HashDouble3(double3 value)
        {
            long x = (long)math.floor(value.x * 0.01d);
            long y = (long)math.floor(value.y * 0.01d);
            long z = (long)math.floor(value.z * 0.01d);
            uint h = 2166136261u;
            h = (h ^ (uint)x) * 16777619u;
            h = (h ^ (uint)(x >> 32)) * 16777619u;
            h = (h ^ (uint)y) * 16777619u;
            h = (h ^ (uint)(y >> 32)) * 16777619u;
            h = (h ^ (uint)z) * 16777619u;
            h = (h ^ (uint)(z >> 32)) * 16777619u;
            return Hash(h);
        }

        public static float HashToUnit(uint value)
        {
            return (Hash(value) & 0x00FFFFFFu) * (1f / 16777215f);
        }

        public static bool IsFinite(double3 value)
        {
            return math.all(math.isfinite(value));
        }

        public static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        public static bool IsFinite(float4x4 value)
        {
            return math.all(math.isfinite(value.c0)) &&
                   math.all(math.isfinite(value.c1)) &&
                   math.all(math.isfinite(value.c2)) &&
                   math.all(math.isfinite(value.c3));
        }

        public static uint HashAsciiLower(byte value, uint hash)
        {
            byte c = value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
            if (c == (byte)' ' || c == (byte)'\t')
                return hash;

            hash ^= c;
            hash *= 16777619u;
            return hash;
        }

        public static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float lengthSq = math.dot(value, value);
            if (!math.isfinite(lengthSq) || lengthSq <= ProceduralCoralConstants.Epsilon)
                return fallback;

            return value * math.rsqrt(lengthSq);
        }

        public static float3 Forward(quaternion rotation)
        {
            return math.mul(rotation, new float3(0f, 1f, 0f));
        }

        public static float3 Right(quaternion rotation)
        {
            return math.mul(rotation, new float3(1f, 0f, 0f));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct MockSectorTriggerJob : IJob
    {
        [NoAlias]
        public NativeArray<CoralSectorTriggerDTO> SectorTriggers;

        [ReadOnly]
        [NoAlias]
        public NativeArray<CoralTuningDTO> Tuning;

        [NoAlias]
        public NativeArray<CoralPaddedCounterDTO> Counters;

        public double3 MockRootAUP;
        public uint WorldSeed;
        public uint SimulationFrameCounter;

        public void Execute()
        {
            if (!SectorTriggers.IsCreated || SectorTriggers.Length <= 0)
                return;

            CoralTuningDTO tuning = Tuning.IsCreated && Tuning.Length > 0 ? SanitizeTuning(Tuning[0]) : BuildDefaultTuning();
            double3 root = ProceduralCoralMath.IsFinite(MockRootAUP) ? MockRootAUP : double3.zero;
            uint sectorHash = ProceduralCoralMath.HashDouble3(root);
            uint seed = ProceduralCoralMath.Hash(sectorHash ^ WorldSeed ^ tuning.SeedSalt ^ SimulationFrameCounter);
            if (seed == 0u)
                seed = 1u;

            CoralSectorTriggerDTO trigger = default;
            trigger.RootAUP = root;
            trigger.SectorHash = sectorHash;
            trigger.Seed = seed;
            trigger.GlobalQualityWeight = math.saturate(tuning.GlobalQualityWeight);
            trigger.SimulationFrame = SimulationFrameCounter;
            trigger.BaseStepMeters = math.max(tuning.BaseStepMeters, ProceduralCoralConstants.Epsilon);
            trigger.BaseRadiusMeters = math.max(tuning.BaseRadiusMeters, ProceduralCoralConstants.Epsilon);
            trigger.MaxDepth = math.clamp(tuning.MaxDepth, 1, 12);
            trigger.SectorRadiusMeters = math.lerp(32f, 220f, ProceduralCoralMath.Smooth01(tuning.GlobalQualityWeight));
            trigger.Flags = 1u;
            trigger.SeedSalt = tuning.SeedSalt;
            SectorTriggers[0] = trigger;

            if (Counters.IsCreated && Counters.Length > 0)
            {
                CoralPaddedCounterDTO counter = Counters[0];
                counter.StateHash = seed ^ sectorHash;
                counter.FaultFlags = 0;
                Counters[0] = counter;
            }
        }

        private static CoralTuningDTO BuildDefaultTuning()
        {
            CoralTuningDTO tuning = default;
            tuning.GlobalQualityWeight = 0.5f;
            tuning.BranchAngleRadians = 0.52f;
            tuning.AngleVarianceRadians = 0.18f;
            tuning.BaseStepMeters = 1.6f;
            tuning.BaseRadiusMeters = 0.32f;
            tuning.RadiusDecay = 0.82f;
            tuning.SdfAvoidanceWeight = 0.55f;
            tuning.MaxDepth = 7;
            tuning.MaxBranches = 768;
            tuning.MaxInstructions = 2048;
            tuning.VisibilityDistanceMin = 48f;
            tuning.VisibilityDistanceMax = 360f;
            tuning.CurrentSwayAmplitude = 0.32f;
            tuning.Version = 1u;
            tuning.SeedSalt = 0xC0A17u;
            return tuning;
        }

        private static CoralTuningDTO SanitizeTuning(in CoralTuningDTO tuning)
        {
            CoralTuningDTO safe = tuning;
            safe.GlobalQualityWeight = math.saturate(tuning.GlobalQualityWeight);
            safe.BranchAngleRadians = math.clamp(tuning.BranchAngleRadians, 0.05f, 1.35f);
            safe.AngleVarianceRadians = math.saturate(tuning.AngleVarianceRadians);
            safe.BaseStepMeters = math.max(tuning.BaseStepMeters, ProceduralCoralConstants.Epsilon);
            safe.BaseRadiusMeters = math.max(tuning.BaseRadiusMeters, ProceduralCoralConstants.Epsilon);
            safe.RadiusDecay = math.clamp(tuning.RadiusDecay, 0.35f, 0.98f);
            safe.SdfAvoidanceWeight = math.saturate(tuning.SdfAvoidanceWeight);
            safe.MaxDepth = math.clamp(tuning.MaxDepth, 1, 12);
            safe.MaxBranches = math.clamp(tuning.MaxBranches, 1, ProceduralCoralConstants.MaxBranches);
            safe.MaxInstructions = math.clamp(tuning.MaxInstructions, 1, ProceduralCoralConstants.MaxInstructions);
            safe.VisibilityDistanceMin = math.max(tuning.VisibilityDistanceMin, 8f);
            safe.VisibilityDistanceMax = math.max(tuning.VisibilityDistanceMax, safe.VisibilityDistanceMin);
            safe.CurrentSwayAmplitude = math.saturate(tuning.CurrentSwayAmplitude);
            safe.Version = tuning.Version == 0u ? 1u : tuning.Version;
            safe.SeedSalt = tuning.SeedSalt == 0u ? 0xC0A17u : tuning.SeedSalt;
            return safe;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct EvaluateCoralLSystemJob : IJob
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<CoralLSystemRuleDTO> Rules;

        [ReadOnly]
        [NoAlias]
        public NativeArray<CoralSectorTriggerDTO> SectorTriggers;

        [ReadOnly]
        [NoAlias]
        public NativeArray<CoralTuningDTO> Tuning;

        [NoAlias]
        public NativeArray<uint> InstructionScratchA;

        [NoAlias]
        public NativeArray<uint> InstructionScratchB;

        [NoAlias]
        public NativeArray<CoralBranchDTO> Branches;

        [NoAlias]
        public NativeArray<CoralTurtleStateDTO> TurtleStack;

        [NoAlias]
        public NativeArray<CoralDebugSegmentDTO> DebugSegments;

        [NoAlias]
        public NativeArray<CoralPaddedCounterDTO> Counters;

        [NoAlias]
        public NativeArray<CoralGenerationTelemetryEntry> TelemetryRing;

        [NoAlias]
        public NativeArray<int> TelemetryCursor;

        public uint Frame;

        public void Execute()
        {
            if (!Rules.IsCreated ||
                !SectorTriggers.IsCreated ||
                !Branches.IsCreated ||
                !InstructionScratchA.IsCreated ||
                !InstructionScratchB.IsCreated ||
                SectorTriggers.Length <= 0)
            {
                return;
            }

            CoralSectorTriggerDTO trigger = SectorTriggers[0];
            CoralTuningDTO tuning = ResolveTuning();
            float quality = math.saturate(trigger.GlobalQualityWeight > 0f ? trigger.GlobalQualityWeight : tuning.GlobalQualityWeight);
            float qualityCurve = ProceduralCoralMath.Smooth01(quality);
            int activeRuleCount = ResolveActiveRuleCount();
            uint faultFlags = activeRuleCount <= 0 ? ProceduralCoralConstants.FaultNoRules : 0u;
            int maxDepth = math.clamp((int)math.round(math.lerp(2f, tuning.MaxDepth, qualityCurve)), 1, 12);
            int instructionLimit = math.clamp(
                (int)math.round(math.lerp(64f, tuning.MaxInstructions, qualityCurve)),
                1,
                math.min(InstructionScratchA.Length, InstructionScratchB.Length));
            int branchLimit = math.clamp(
                (int)math.round(math.lerp(48f, tuning.MaxBranches, qualityCurve)),
                1,
                math.min(Branches.Length, tuning.MaxBranches));

            ClearBranches();
            ClearDebug();

            InstructionScratchA[0] = ProceduralCoralConstants.OpGrow;
            int currentLength = 1;
            bool sourceA = true;
            int depthReached = 0;
            for (int depth = 0; depth < maxDepth; depth++)
            {
                NativeArray<uint> source = sourceA ? InstructionScratchA : InstructionScratchB;
                NativeArray<uint> target = sourceA ? InstructionScratchB : InstructionScratchA;
                int write = 0;
                for (int i = 0; i < currentLength && write < instructionLimit; i++)
                {
                    uint opcode = source[i];
                    if (TryFindRule(opcode, activeRuleCount, out CoralLSystemRuleDTO rule) && rule.ReplacementCount > 0)
                    {
                        int replacementCount = math.min(rule.ReplacementCount, (byte)8);
                        for (int r = 0; r < replacementCount && write < instructionLimit; r++)
                            target[write++] = GetReplacement(rule, r);
                    }
                    else
                    {
                        target[write++] = opcode;
                    }
                }

                if (write >= instructionLimit)
                    faultFlags |= ProceduralCoralConstants.FaultCapacity;

                currentLength = write;
                sourceA = !sourceA;
                depthReached = depth + 1;
                if (currentLength <= 0 || (faultFlags & ProceduralCoralConstants.FaultCapacity) != 0u)
                    break;
            }

            NativeArray<uint> finalStream = sourceA ? InstructionScratchA : InstructionScratchB;
            CoralPaddedCounterDTO counter = default;
            counter.ActiveRuleCount = (uint)activeRuleCount;
            counter.InstructionCount = currentLength;
            counter.DepthReached = depthReached;
            counter.StateHash = trigger.Seed ^ trigger.SectorHash;
            counter.TelemetryCursor = Counters.IsCreated && Counters.Length > 0 ? Counters[0].TelemetryCursor : 0u;
            counter.FaultFlags = (int)faultFlags;

            InterpretStream(finalStream, currentLength, branchLimit, trigger, tuning, quality, ref counter);
            if (Counters.IsCreated && Counters.Length > 0)
                Counters[0] = counter;

            WriteTelemetry(trigger.RootAUP, trigger.SectorHash, quality, counter, (uint)counter.FaultFlags);
        }

        private CoralTuningDTO ResolveTuning()
        {
            if (!Tuning.IsCreated || Tuning.Length <= 0)
            {
                CoralTuningDTO fallback = default;
                fallback.GlobalQualityWeight = 0.5f;
                fallback.BranchAngleRadians = 0.52f;
                fallback.AngleVarianceRadians = 0.18f;
                fallback.BaseStepMeters = 1.6f;
                fallback.BaseRadiusMeters = 0.32f;
                fallback.RadiusDecay = 0.82f;
                fallback.SdfAvoidanceWeight = 0.55f;
                fallback.MaxDepth = 7;
                fallback.MaxBranches = 768;
                fallback.MaxInstructions = 2048;
                fallback.VisibilityDistanceMin = 48f;
                fallback.VisibilityDistanceMax = 360f;
                fallback.CurrentSwayAmplitude = 0.32f;
                fallback.Version = 1u;
                fallback.SeedSalt = 0xC0A17u;
                return fallback;
            }

            CoralTuningDTO tuning = Tuning[0];
            tuning.GlobalQualityWeight = math.saturate(tuning.GlobalQualityWeight);
            tuning.BranchAngleRadians = math.clamp(tuning.BranchAngleRadians, 0.05f, 1.35f);
            tuning.AngleVarianceRadians = math.saturate(tuning.AngleVarianceRadians);
            tuning.BaseStepMeters = math.max(tuning.BaseStepMeters, ProceduralCoralConstants.Epsilon);
            tuning.BaseRadiusMeters = math.max(tuning.BaseRadiusMeters, ProceduralCoralConstants.Epsilon);
            tuning.RadiusDecay = math.clamp(tuning.RadiusDecay, 0.35f, 0.98f);
            tuning.MaxDepth = math.clamp(tuning.MaxDepth, 1, 12);
            tuning.MaxBranches = math.clamp(tuning.MaxBranches, 1, ProceduralCoralConstants.MaxBranches);
            tuning.MaxInstructions = math.clamp(tuning.MaxInstructions, 1, ProceduralCoralConstants.MaxInstructions);
            tuning.VisibilityDistanceMin = math.max(tuning.VisibilityDistanceMin, 8f);
            tuning.VisibilityDistanceMax = math.max(tuning.VisibilityDistanceMax, tuning.VisibilityDistanceMin);
            tuning.CurrentSwayAmplitude = math.saturate(tuning.CurrentSwayAmplitude);
            return tuning;
        }

        private int ResolveActiveRuleCount()
        {
            int max = math.min(Rules.Length, ProceduralCoralConstants.MaxRules);
            int count = 0;
            for (int i = 0; i < max; i++)
            {
                CoralLSystemRuleDTO rule = Rules[i];
                if (rule.SourceOpcode == 0u)
                    break;

                count = i + 1;
            }

            return count;
        }

        private bool TryFindRule(uint opcode, int activeRuleCount, out CoralLSystemRuleDTO rule)
        {
            int limit = math.min(activeRuleCount, Rules.Length);
            for (int i = 0; i < limit; i++)
            {
                CoralLSystemRuleDTO candidate = Rules[i];
                if (candidate.SourceOpcode == opcode)
                {
                    rule = candidate;
                    return true;
                }
            }

            rule = default;
            return false;
        }

        private static uint GetReplacement(in CoralLSystemRuleDTO rule, int index)
        {
            switch (index)
            {
                case 0:
                    return rule.Replacement0;
                case 1:
                    return rule.Replacement1;
                case 2:
                    return rule.Replacement2;
                case 3:
                    return rule.Replacement3;
                case 4:
                    return rule.Replacement4;
                case 5:
                    return rule.Replacement5;
                case 6:
                    return rule.Replacement6;
                default:
                    return rule.Replacement7;
            }
        }

        private void InterpretStream(
            NativeArray<uint> stream,
            int streamLength,
            int branchLimit,
            in CoralSectorTriggerDTO trigger,
            in CoralTuningDTO tuning,
            float quality,
            ref CoralPaddedCounterDTO counter)
        {
            CoralTurtleStateDTO turtle = default;
            turtle.LocalPosition = float3.zero;
            turtle.Radius = math.max(trigger.BaseRadiusMeters > 0f ? trigger.BaseRadiusMeters : tuning.BaseRadiusMeters, ProceduralCoralConstants.Epsilon);
            turtle.Rotation = quaternion.identity;
            turtle.ParentIndex = uint.MaxValue;
            turtle.Depth = 0u;
            turtle.StableId = ProceduralCoralMath.Hash(trigger.Seed ^ trigger.SectorHash ^ 0xC010u);
            turtle.RuleHash = 0u;
            turtle.StepMeters = math.max(trigger.BaseStepMeters > 0f ? trigger.BaseStepMeters : tuning.BaseStepMeters, ProceduralCoralConstants.Epsilon);
            turtle.Stiffness = 1f;

            int stackCount = 0;
            int branchCount = 0;
            int tipCount = 0;
            uint faultFlags = (uint)counter.FaultFlags;
            float baseAngle = tuning.BranchAngleRadians;
            float qualityCurve = ProceduralCoralMath.Smooth01(quality);
            float stepScale = math.lerp(0.68f, 1.22f, qualityCurve);
            float varianceScale = tuning.AngleVarianceRadians * math.lerp(0.35f, 1f, qualityCurve);

            for (int i = 0; i < streamLength && branchCount < branchLimit; i++)
            {
                uint opcode = stream[i];
                uint salt = ProceduralCoralMath.Hash(trigger.Seed ^ (uint)i ^ turtle.StableId);
                float variance = (ProceduralCoralMath.HashToUnit(salt) - 0.5f) * varianceScale;

                if (opcode == ProceduralCoralConstants.OpGrow || opcode == ProceduralCoralConstants.OpFork)
                {
                    EmitBranch(trigger, tuning, opcode, salt, stepScale, ref turtle, ref counter, ref branchCount, ref tipCount, ref faultFlags);
                    continue;
                }

                if (opcode == ProceduralCoralConstants.OpTurnLeft)
                {
                    turtle.Rotation = math.mul(turtle.Rotation, quaternion.AxisAngle(new float3(0f, 0f, 1f), baseAngle + variance));
                    continue;
                }

                if (opcode == ProceduralCoralConstants.OpTurnRight)
                {
                    turtle.Rotation = math.mul(turtle.Rotation, quaternion.AxisAngle(new float3(0f, 0f, 1f), -baseAngle + variance));
                    continue;
                }

                if (opcode == ProceduralCoralConstants.OpPitchUp)
                {
                    turtle.Rotation = math.mul(turtle.Rotation, quaternion.AxisAngle(new float3(1f, 0f, 0f), baseAngle * 0.72f + variance));
                    continue;
                }

                if (opcode == ProceduralCoralConstants.OpPitchDown)
                {
                    turtle.Rotation = math.mul(turtle.Rotation, quaternion.AxisAngle(new float3(1f, 0f, 0f), -baseAngle * 0.55f + variance));
                    continue;
                }

                if (opcode == ProceduralCoralConstants.OpRoll)
                {
                    turtle.Rotation = math.mul(turtle.Rotation, quaternion.AxisAngle(new float3(0f, 1f, 0f), (baseAngle * 0.5f) + variance));
                    continue;
                }

                if (opcode == ProceduralCoralConstants.OpThin)
                {
                    turtle.Radius = math.max(turtle.Radius * tuning.RadiusDecay, 0.018f);
                    turtle.StepMeters = math.max(turtle.StepMeters * math.lerp(0.82f, 0.94f, qualityCurve), 0.12f);
                    turtle.Stiffness = math.saturate(turtle.Stiffness * 0.86f);
                    continue;
                }

                if (opcode == ProceduralCoralConstants.OpPush)
                {
                    if (TurtleStack.IsCreated && stackCount < TurtleStack.Length)
                    {
                        TurtleStack[stackCount++] = turtle;
                        turtle.Depth++;
                        turtle.Radius = math.max(turtle.Radius * tuning.RadiusDecay, 0.018f);
                        turtle.Stiffness = math.saturate(turtle.Stiffness * 0.82f);
                    }
                    else
                    {
                        faultFlags |= ProceduralCoralConstants.FaultStackOverflow;
                    }

                    continue;
                }

                if (opcode == ProceduralCoralConstants.OpPop)
                {
                    if (TurtleStack.IsCreated && stackCount > 0)
                        turtle = TurtleStack[--stackCount];

                    continue;
                }

                if (opcode == ProceduralCoralConstants.OpTip)
                {
                    if (turtle.ParentIndex != uint.MaxValue && turtle.ParentIndex < Branches.Length)
                    {
                        int parentIndex = (int)turtle.ParentIndex;
                        CoralBranchDTO branch = Branches[parentIndex];
                        branch.StateFlags |= CoralBranchFlags.Tip | CoralBranchFlags.Bioluminescent;
                        Branches[parentIndex] = branch;
                        if (DebugSegments.IsCreated && parentIndex < DebugSegments.Length)
                        {
                            CoralDebugSegmentDTO segment = DebugSegments[parentIndex];
                            segment.StateFlags = branch.StateFlags;
                            DebugSegments[parentIndex] = segment;
                        }

                        tipCount++;
                    }
                }
            }

            if (branchCount >= branchLimit && streamLength > 0)
                faultFlags |= ProceduralCoralConstants.FaultCapacity;

            counter.BranchCount = branchCount;
            counter.TipCount = (uint)tipCount;
            counter.FaultFlags = (int)faultFlags;
            counter.StateHash ^= (uint)branchCount * 16777619u;
        }

        private void EmitBranch(
            in CoralSectorTriggerDTO trigger,
            in CoralTuningDTO tuning,
            uint opcode,
            uint salt,
            float stepScale,
            ref CoralTurtleStateDTO turtle,
            ref CoralPaddedCounterDTO counter,
            ref int branchCount,
            ref int tipCount,
            ref uint faultFlags)
        {
            float3 start = turtle.LocalPosition;
            float3 direction = ProceduralCoralMath.SafeNormalize(ProceduralCoralMath.Forward(turtle.Rotation), new float3(0f, 1f, 0f));
            float step = math.max(turtle.StepMeters * stepScale, 0.08f);
            if (opcode == ProceduralCoralConstants.OpFork)
                step *= 0.72f;

            float3 end = start + direction * step;
            float3 mid = (start + end) * 0.5f;
            double3 aup = trigger.RootAUP + new double3(mid.x, mid.y, mid.z);
            uint stableId = ProceduralCoralMath.Hash(turtle.StableId ^ salt ^ (uint)branchCount);
            float radius = math.max(turtle.Radius, 0.012f);
            float3 scale = new float3(radius * 2f, step, radius * 2f);
            float4x4 matrix = float4x4.TRS(mid, turtle.Rotation, scale);
            uint flags = CoralBranchFlags.Alive;
            if (branchCount == 0)
                flags |= CoralBranchFlags.Root;

            if (!ProceduralCoralMath.IsFinite(matrix) || !ProceduralCoralMath.IsFinite(aup))
            {
                matrix = float4x4.TRS(float3.zero, quaternion.identity, new float3(0.25f, 0.5f, 0.25f));
                aup = trigger.RootAUP;
                flags |= CoralBranchFlags.NonFiniteFallback;
                faultFlags |= ProceduralCoralConstants.FaultNonFinite;
            }

            CoralBranchDTO branch = default;
            branch.LocalMatrix = matrix;
            branch.PrefabHash = SelectPrefabHash(salt, turtle.Depth);
            branch.GenerationDepth = turtle.Depth;
            branch.SectorAUP = aup;
            branch.Stiffness = math.saturate(turtle.Stiffness);
            branch.Radius = radius;
            branch.StateFlags = flags;
            branch.ParentIndex = turtle.ParentIndex;
            branch.StableId = stableId;
            branch.SectorHash = trigger.SectorHash;
            Branches[branchCount] = branch;

            if (DebugSegments.IsCreated && branchCount < DebugSegments.Length)
            {
                CoralDebugSegmentDTO segment = default;
                segment.StartAUP = trigger.RootAUP + new double3(start.x, start.y, start.z);
                segment.EndAUP = trigger.RootAUP + new double3(end.x, end.y, end.z);
                segment.BranchIndex = (uint)branchCount;
                segment.StateFlags = flags;
                segment.SectorHash = trigger.SectorHash;
                segment.GenerationDepth = turtle.Depth;
                DebugSegments[branchCount] = segment;
            }

            turtle.LocalPosition = end;
            turtle.ParentIndex = (uint)branchCount;
            turtle.StableId = stableId;
            turtle.RuleHash = opcode;
            turtle.Radius = math.max(radius * tuning.RadiusDecay, 0.018f);
            turtle.StepMeters = math.max(turtle.StepMeters * 0.96f, 0.12f);
            branchCount++;
            counter.StateHash = (counter.StateHash ^ stableId) * 16777619u;

            if (turtle.Depth >= (uint)math.max(1, tuning.MaxDepth - 1))
            {
                CoralBranchDTO tip = Branches[branchCount - 1];
                tip.StateFlags |= CoralBranchFlags.Tip | CoralBranchFlags.Bioluminescent;
                Branches[branchCount - 1] = tip;
                if (DebugSegments.IsCreated && branchCount - 1 < DebugSegments.Length)
                {
                    CoralDebugSegmentDTO segment = DebugSegments[branchCount - 1];
                    segment.StateFlags = tip.StateFlags;
                    DebugSegments[branchCount - 1] = segment;
                }

                tipCount++;
            }
        }

        private uint SelectPrefabHash(uint salt, uint depth)
        {
            int limit = math.min(Rules.Length, ProceduralCoralConstants.MaxRules);
            if (limit <= 0)
                return ProceduralCoralMath.Hash(0xC012A100u ^ salt);

            int index = (int)(ProceduralCoralMath.Hash(salt ^ depth) % (uint)limit);
            CoralLSystemRuleDTO rule = Rules[index];
            if (rule.PrefabHash != 0u)
                return rule.PrefabHash;

            return ProceduralCoralMath.Hash(0xC012A100u ^ salt ^ depth);
        }

        private void ClearBranches()
        {
            for (int i = 0; i < Branches.Length; i++)
                Branches[i] = default;
        }

        private void ClearDebug()
        {
            if (!DebugSegments.IsCreated)
                return;

            for (int i = 0; i < DebugSegments.Length; i++)
                DebugSegments[i] = default;
        }

        private void WriteTelemetry(double3 rootAup, uint sectorHash, float quality, in CoralPaddedCounterDTO counter, uint faultFlags)
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0)
                return;

            int cursor = 0;
            if (TelemetryCursor.IsCreated && TelemetryCursor.Length > 0)
            {
                cursor = TelemetryCursor[0];
                TelemetryCursor[0] = (cursor + 1) % TelemetryRing.Length;
            }

            CoralGenerationTelemetryEntry entry = default;
            entry.RootAUP = rootAup;
            entry.Frame = Frame;
            entry.SectorHash = sectorHash;
            entry.BranchCount = counter.BranchCount;
            entry.DepthReached = counter.DepthReached;
            entry.EstimatedComputeUs = EstimateComputeUs(counter.BranchCount, counter.InstructionCount, quality);
            entry.GlobalQualityWeight = quality;
            entry.StateHash = counter.StateHash;
            entry.FaultFlags = faultFlags;
            entry.TipCount = counter.TipCount;
            entry.MatrixCount = (uint)math.max(0, counter.RenderMatrixCount);
            TelemetryRing[math.clamp(cursor, 0, TelemetryRing.Length - 1)] = entry;
        }

        private static float EstimateComputeUs(int branchCount, int instructionCount, float quality)
        {
            return (branchCount * math.lerp(0.018f, 0.034f, quality)) + (instructionCount * 0.0065f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ConstrainCoralGrowthJob : IJob
    {
        [NoAlias]
        public NativeArray<CoralBranchDTO> Branches;

        [NoAlias]
        public NativeArray<CoralSpatialCellDTO> SpatialCells;

        [ReadOnly]
        [NoAlias]
        public NativeArray<CoralSectorTriggerDTO> SectorTriggers;

        [ReadOnly]
        [NoAlias]
        public NativeArray<CoralTuningDTO> Tuning;

        [NoAlias]
        public NativeArray<CoralPaddedCounterDTO> Counters;

        [NoAlias]
        public NativeArray<CoralGenerationTelemetryEntry> TelemetryRing;

        [ReadOnly]
        [NoAlias]
        public NativeArray<int> TelemetryCursor;

        public void Execute()
        {
            if (!Branches.IsCreated || !Counters.IsCreated || Counters.Length <= 0)
                return;

            CoralPaddedCounterDTO counter = Counters[0];
            CoralSectorTriggerDTO trigger = SectorTriggers.IsCreated && SectorTriggers.Length > 0 ? SectorTriggers[0] : default;
            CoralTuningDTO tuning = Tuning.IsCreated && Tuning.Length > 0 ? Tuning[0] : default;
            float quality = math.saturate(tuning.GlobalQualityWeight);
            int branchCount = math.clamp(counter.BranchCount, 0, Branches.Length);
            uint faultFlags = (uint)counter.FaultFlags;
            int pruned = 0;

            if (SpatialCells.IsCreated)
            {
                for (int i = 0; i < SpatialCells.Length; i++)
                    SpatialCells[i] = default;
            }

            for (int i = 0; i < branchCount; i++)
            {
                CoralBranchDTO branch = Branches[i];
                if ((branch.StateFlags & CoralBranchFlags.Alive) == 0)
                    continue;

                float3 local = branch.LocalMatrix.c3.xyz;
                if (!ProceduralCoralMath.IsFinite(branch.LocalMatrix) || !ProceduralCoralMath.IsFinite(branch.SectorAUP))
                {
                    branch.LocalMatrix = float4x4.TRS(float3.zero, quaternion.identity, new float3(0.25f, 0.5f, 0.25f));
                    branch.SectorAUP = trigger.RootAUP;
                    branch.StateFlags |= CoralBranchFlags.NonFiniteFallback;
                    faultFlags |= ProceduralCoralConstants.FaultNonFinite;
                }

                float seabedClearance = math.lerp(0.02f, 0.34f, quality);
                if (local.y < seabedClearance)
                {
                    local.y = math.lerp(local.y, seabedClearance, math.saturate(tuning.SdfAvoidanceWeight));
                    branch.LocalMatrix.c3 = new float4(local, 1f);
                    branch.SectorAUP = trigger.RootAUP + new double3(local.x, local.y, local.z);
                    branch.StateFlags |= CoralBranchFlags.CollisionAdjusted;
                }

                int probeLimit = math.min(i, (int)math.round(math.lerp(16f, 96f, ProceduralCoralMath.Smooth01(quality))));
                for (int j = math.max(0, i - probeLimit); j < i; j++)
                {
                    CoralBranchDTO other = Branches[j];
                    if ((other.StateFlags & CoralBranchFlags.Alive) == 0)
                        continue;

                    float3 delta = local - other.LocalMatrix.c3.xyz;
                    float distanceSq = math.max(math.dot(delta, delta), ProceduralCoralConstants.Epsilon);
                    float minDistance = (branch.Radius + other.Radius) * math.lerp(1.25f, 0.78f, quality);
                    if (distanceSq >= minDistance * minDistance)
                        continue;

                    float3 normal = ProceduralCoralMath.SafeNormalize(delta, ProceduralCoralMath.Right(quaternion.identity));
                    float overlap = minDistance - math.sqrt(distanceSq);
                    float pruneThreshold = math.lerp(0.55f, 1.85f, quality) * math.max(branch.Radius, ProceduralCoralConstants.Epsilon);
                    if (overlap > pruneThreshold)
                    {
                        branch.StateFlags &= ~CoralBranchFlags.Alive;
                        branch.StateFlags |= CoralBranchFlags.CollisionPruned;
                        faultFlags |= ProceduralCoralConstants.FaultCollisionPruned;
                        pruned++;
                        break;
                    }

                    local += normal * overlap * math.lerp(0.45f, 0.9f, quality);
                    branch.LocalMatrix.c3 = new float4(local, 1f);
                    branch.SectorAUP = trigger.RootAUP + new double3(local.x, local.y, local.z);
                    branch.StateFlags |= CoralBranchFlags.CollisionAdjusted;
                }

                Branches[i] = branch;
                if (SpatialCells.IsCreated && i < SpatialCells.Length)
                {
                    CoralSpatialCellDTO cell = default;
                    cell.LocalPosition = branch.LocalMatrix.c3.xyz;
                    cell.Radius = branch.Radius;
                    cell.BranchIndex = (uint)i;
                    cell.SectorHash = branch.SectorHash;
                    cell.OccupancyHash = ProceduralCoralMath.Hash(branch.StableId ^ branch.SectorHash);
                    cell.Flags = branch.StateFlags;
                    SpatialCells[i] = cell;
                }
            }

            counter.PrunedCount = (uint)pruned;
            counter.FaultFlags = (int)faultFlags;
            Counters[0] = counter;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ExtractCoralRenderMatricesJob : IJob
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<CoralBranchDTO> Branches;

        [ReadOnly]
        [NoAlias]
        public NativeArray<CoralTuningDTO> Tuning;

        [NoAlias]
        public NativeArray<float4x4> RenderMatrices;

        [NoAlias]
        public NativeArray<CoralIndirectArgsDTO> IndirectArgs;

        [NoAlias]
        public NativeArray<CoralGpuSwayDTO> GpuSway;

        [NoAlias]
        public NativeArray<CoralPaddedCounterDTO> Counters;

        public double3 CameraAUP;
        public uint VertexCountPerInstance;
        public uint Frame;

        public void Execute()
        {
            if (!Branches.IsCreated || !RenderMatrices.IsCreated || !IndirectArgs.IsCreated || IndirectArgs.Length <= 0)
                return;

            CoralTuningDTO tuning = Tuning.IsCreated && Tuning.Length > 0 ? Tuning[0] : default;
            float quality = math.saturate(tuning.GlobalQualityWeight);
            float curve = ProceduralCoralMath.Smooth01(quality);
            float maxDistance = math.lerp(math.max(tuning.VisibilityDistanceMin, 8f), math.max(tuning.VisibilityDistanceMax, tuning.VisibilityDistanceMin + 1f), curve);
            float maxDistanceSq = maxDistance * maxDistance;
            int branchCount = Counters.IsCreated && Counters.Length > 0 ? math.clamp(Counters[0].BranchCount, 0, Branches.Length) : Branches.Length;
            int write = 0;
            uint stateHash = 2166136261u;
            float density = math.lerp(0.22f, 1f, curve);
            float4x4* dst = (float4x4*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(RenderMatrices);

            for (int i = 0; i < branchCount && write < RenderMatrices.Length; i++)
            {
                CoralBranchDTO branch = Branches[i];
                if ((branch.StateFlags & CoralBranchFlags.Alive) == 0)
                    continue;

                float stochastic = ProceduralCoralMath.HashToUnit(branch.StableId ^ (uint)i);
                if (stochastic > density)
                    continue;

                double3 deltaD = branch.SectorAUP - CameraAUP;
                if (!ProceduralCoralMath.IsFinite(deltaD))
                    continue;

                float3 delta = new float3((float)deltaD.x, (float)deltaD.y, (float)deltaD.z);
                float distSq = math.dot(delta, delta);
                if (!math.isfinite(distSq) || distSq > maxDistanceSq)
                    continue;

                float4x4 matrix = branch.LocalMatrix;
                matrix.c3 = new float4(delta, 1f);
                if (!ProceduralCoralMath.IsFinite(matrix))
                    continue;

                UnsafeUtility.MemCpy(dst + write, &matrix, UnsafeUtility.SizeOf<float4x4>());
                stateHash = (stateHash ^ branch.StableId) * 16777619u;
                write++;
            }

            CoralIndirectArgsDTO args = default;
            args.VertexCountPerInstance = math.max(1u, VertexCountPerInstance);
            args.InstanceCount = (uint)write;
            args.StartVertex = 0u;
            args.StartInstance = 0u;
            IndirectArgs[0] = args;

            if (GpuSway.IsCreated && GpuSway.Length > 0)
            {
                CoralGpuSwayDTO gpu = default;
                gpu.FlowAndAmplitude = new float4(
                    math.lerp(0.04f, 0.18f, curve),
                    math.lerp(0.12f, 0.55f, curve) * math.saturate(tuning.CurrentSwayAmplitude),
                    math.lerp(0.65f, 1.85f, curve),
                    quality);
                gpu.BoundsAndDensity = new float4(maxDistance, maxDistanceSq, write, RenderMatrices.Length);
                gpu.FaultAndFrame = new float4(Counters.IsCreated && Counters.Length > 0 ? Counters[0].FaultFlags : 0, Frame, 0f, 0f);
                gpu.StateHash = stateHash;
                GpuSway[0] = gpu;
            }

            if (Counters.IsCreated && Counters.Length > 0)
            {
                CoralPaddedCounterDTO counter = Counters[0];
                counter.RenderMatrixCount = write;
                counter.StateHash ^= stateHash;
                Counters[0] = counter;
            }

            if (TelemetryRing.IsCreated && TelemetryCursor.IsCreated && TelemetryRing.Length > 0 && TelemetryCursor.Length > 0)
            {
                int cursor = TelemetryCursor[0] - 1;
                if (cursor < 0)
                    cursor = TelemetryRing.Length - 1;

                cursor = math.clamp(cursor, 0, TelemetryRing.Length - 1);
                CoralGenerationTelemetryEntry entry = TelemetryRing[cursor];
                entry.MatrixCount = (uint)write;
                entry.StateHash ^= stateHash;
                TelemetryRing[cursor] = entry;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct InjectBioluminescenceNodesJob : IJob
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<CoralBranchDTO> Branches;

        [ReadOnly]
        [NoAlias]
        public NativeArray<CoralTuningDTO> Tuning;

        [NoAlias]
        public NativeArray<SyncPulseDTO> SyncPulses;

        [NoAlias]
        public NativeArray<CoralPaddedCounterDTO> Counters;

        public void Execute()
        {
            if (!Branches.IsCreated || !SyncPulses.IsCreated)
                return;

            for (int i = 0; i < SyncPulses.Length; i++)
                SyncPulses[i] = default;

            CoralTuningDTO tuning = Tuning.IsCreated && Tuning.Length > 0 ? Tuning[0] : default;
            float quality = math.saturate(tuning.GlobalQualityWeight);
            float density = math.lerp(0.12f, 0.92f, ProceduralCoralMath.Smooth01(quality));
            int branchCount = Counters.IsCreated && Counters.Length > 0 ? math.clamp(Counters[0].BranchCount, 0, Branches.Length) : Branches.Length;
            int write = 0;
            for (int i = 0; i < branchCount && write < SyncPulses.Length; i++)
            {
                CoralBranchDTO branch = Branches[i];
                if ((branch.StateFlags & CoralBranchFlags.Alive) == 0 ||
                    (branch.StateFlags & CoralBranchFlags.Bioluminescent) == 0)
                {
                    continue;
                }

                if (ProceduralCoralMath.HashToUnit(branch.StableId ^ 0xB10u) > density)
                    continue;

                SyncPulseDTO pulse = default;
                pulse.OriginAUP = branch.SectorAUP;
                pulse.WaveSpeed = math.lerp(0.35f, 1.8f, quality) * math.lerp(0.8f, 1.25f, ProceduralCoralMath.HashToUnit(branch.StableId));
                pulse.ColorOverride = 0x5AD6FFFFu ^ (branch.StableId & 0x0000FFFFu);
                SyncPulses[write++] = pulse;
            }

            if (Counters.IsCreated && Counters.Length > 0)
            {
                CoralPaddedCounterDTO counter = Counters[0];
                counter.SyncPulseCount = write;
                Counters[0] = counter;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct StageCollisionProxiesJob : IJob
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<CoralBranchDTO> Branches;

        [ReadOnly]
        [NoAlias]
        public NativeArray<CoralTuningDTO> Tuning;

        [NoAlias]
        public NativeArray<CapsuleColliderDTO> CollisionProxies;

        [NoAlias]
        public NativeArray<CoralPaddedCounterDTO> Counters;

        public void Execute()
        {
            if (!Branches.IsCreated || !CollisionProxies.IsCreated)
                return;

            for (int i = 0; i < CollisionProxies.Length; i++)
                CollisionProxies[i] = default;

            CoralTuningDTO tuning = Tuning.IsCreated && Tuning.Length > 0 ? Tuning[0] : default;
            float quality = math.saturate(tuning.GlobalQualityWeight);
            uint maxProxyDepth = (uint)math.clamp((int)math.round(math.lerp(1f, 4f, quality)), 1, 4);
            int branchCount = Counters.IsCreated && Counters.Length > 0 ? math.clamp(Counters[0].BranchCount, 0, Branches.Length) : Branches.Length;
            int write = 0;
            for (int i = 0; i < branchCount && write < CollisionProxies.Length; i++)
            {
                CoralBranchDTO branch = Branches[i];
                if ((branch.StateFlags & CoralBranchFlags.Alive) == 0 || branch.GenerationDepth > maxProxyDepth)
                    continue;

                float3 axis = ProceduralCoralMath.SafeNormalize(branch.LocalMatrix.c1.xyz, new float3(0f, 1f, 0f));
                float height = math.max(math.length(branch.LocalMatrix.c1.xyz), branch.Radius * 2f);
                CapsuleColliderDTO proxy = default;
                proxy.CenterAUP = branch.SectorAUP;
                proxy.Axis = axis;
                proxy.Radius = math.max(branch.Radius, 0.05f);
                proxy.Height = height;
                proxy.BranchIndex = (uint)i;
                proxy.Flags = 1u;
                proxy.SectorHash = branch.SectorHash;
                CollisionProxies[write++] = proxy;
            }

            if (Counters.IsCreated && Counters.Length > 0)
            {
                CoralPaddedCounterDTO counter = Counters[0];
                counter.CollisionProxyCount = write;
                Counters[0] = counter;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct CoralSelfAuditJob : IJob
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<CoralBranchDTO> Branches;

        [ReadOnly]
        [NoAlias]
        public NativeArray<CoralPaddedCounterDTO> Counters;

        [NoAlias]
        public NativeArray<CoralSelfAuditResultDTO> Results;

        public uint Frame;

        public void Execute()
        {
            if (!Branches.IsCreated || !Results.IsCreated || Results.Length <= 0)
                return;

            int branchCount = Counters.IsCreated && Counters.Length > 0 ? math.clamp(Counters[0].BranchCount, 0, Branches.Length) : Branches.Length;
            int probeLimit = math.min(branchCount, 256);
            int live = 0;
            int tips = 0;
            int overlapPairs = 0;
            float maxOverlap = 0f;
            uint sectorHash = 0u;
            uint stateHash = 2166136261u;
            uint faultFlags = 0u;

            for (int i = 0; i < branchCount; i++)
            {
                CoralBranchDTO branch = Branches[i];
                if ((branch.StateFlags & CoralBranchFlags.Alive) == 0)
                    continue;

                live++;
                sectorHash = branch.SectorHash;
                stateHash = (stateHash ^ branch.StableId) * 16777619u;
                if ((branch.StateFlags & CoralBranchFlags.Tip) != 0)
                    tips++;

                if (!ProceduralCoralMath.IsFinite(branch.LocalMatrix) || !ProceduralCoralMath.IsFinite(branch.SectorAUP))
                    faultFlags |= ProceduralCoralConstants.FaultNonFinite;
            }

            for (int a = 0; a < probeLimit; a++)
            {
                CoralBranchDTO branchA = Branches[a];
                if ((branchA.StateFlags & CoralBranchFlags.Alive) == 0)
                    continue;

                for (int b = a + 1; b < probeLimit; b++)
                {
                    CoralBranchDTO branchB = Branches[b];
                    if ((branchB.StateFlags & CoralBranchFlags.Alive) == 0)
                        continue;

                    float3 delta = branchA.LocalMatrix.c3.xyz - branchB.LocalMatrix.c3.xyz;
                    float distanceSq = math.max(math.dot(delta, delta), ProceduralCoralConstants.Epsilon);
                    float distance = math.sqrt(distanceSq);
                    float overlap = ((branchA.Radius + branchB.Radius) * 0.7f) - distance;
                    if (overlap > 0.01f)
                    {
                        overlapPairs++;
                        maxOverlap = math.max(maxOverlap, overlap);
                    }
                }
            }

            CoralSelfAuditResultDTO result = default;
            result.Frame = Frame;
            result.SectorHash = sectorHash;
            result.Flags = faultFlags;
            result.LiveBranchCount = (uint)live;
            result.TipCount = (uint)tips;
            result.OverlapPairCount = (uint)overlapPairs;
            result.RenderMatrixCount = Counters.IsCreated && Counters.Length > 0 ? (uint)math.max(0, Counters[0].RenderMatrixCount) : 0u;
            result.StateHash = stateHash;
            result.MaxOverlapDepth = maxOverlap;
            result.BranchUtilization = branchCount > 0 ? (float)live / math.max(branchCount, 1) : 0f;
            if (overlapPairs > 0)
                result.Flags |= ProceduralCoralConstants.FaultCollisionPruned;

            Results[0] = result;
        }
    }
}
