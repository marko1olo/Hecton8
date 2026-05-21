using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World.ProceduralWreckage
{
    internal static class ProceduralWreckageMath
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

        public static ushort BuildRuleMask(int activeRuleCount)
        {
            int safeCount = math.clamp(activeRuleCount, 1, ProceduralWreckageConstants.MaxModuleRules);
            return safeCount >= 16 ? ushort.MaxValue : (ushort)((1u << safeCount) - 1u);
        }

        public static int PopCount(ushort value)
        {
            return math.countbits((uint)value);
        }

        public static byte SelectNthSetBit(ushort mask, uint ordinal)
        {
            int count = PopCount(mask);
            if (count <= 0)
                return 0;

            int target = (int)(ordinal % (uint)count);
            for (int bit = 0; bit < 16; bit++)
            {
                if (((mask >> bit) & 1) == 0)
                    continue;

                if (target == 0)
                    return (byte)bit;

                target--;
            }

            return 0;
        }

        public static int ToIndex(int3 coord, int3 dims)
        {
            return coord.x + (coord.z * dims.x) + (coord.y * dims.x * dims.z);
        }

        public static int3 ToCoord(int index, int3 dims)
        {
            int layer = dims.x * dims.z;
            int y = index / math.max(layer, 1);
            int rem = index - (y * layer);
            int z = rem / math.max(dims.x, 1);
            int x = rem - (z * dims.x);
            return new int3(x, y, z);
        }

        public static int3 DirectionOffset(int direction)
        {
            switch (direction)
            {
                case WreckageDirections.North:
                    return new int3(0, 0, 1);
                case WreckageDirections.East:
                    return new int3(1, 0, 0);
                case WreckageDirections.South:
                    return new int3(0, 0, -1);
                case WreckageDirections.West:
                    return new int3(-1, 0, 0);
                case WreckageDirections.Top:
                    return new int3(0, 1, 0);
                default:
                    return new int3(0, -1, 0);
            }
        }

        public static int OppositeDirection(int direction)
        {
            switch (direction)
            {
                case WreckageDirections.North:
                    return WreckageDirections.South;
                case WreckageDirections.East:
                    return WreckageDirections.West;
                case WreckageDirections.South:
                    return WreckageDirections.North;
                case WreckageDirections.West:
                    return WreckageDirections.East;
                case WreckageDirections.Top:
                    return WreckageDirections.Bottom;
                default:
                    return WreckageDirections.Top;
            }
        }

        public static ushort SocketAt(in WreckageRuleDTO rule, int direction)
        {
            switch (direction)
            {
                case WreckageDirections.North:
                    return rule.SocketNorth;
                case WreckageDirections.East:
                    return rule.SocketEast;
                case WreckageDirections.South:
                    return rule.SocketSouth;
                case WreckageDirections.West:
                    return rule.SocketWest;
                case WreckageDirections.Top:
                    return rule.SocketTop;
                default:
                    return rule.SocketBottom;
            }
        }

        public static bool IsInsideHull(int3 coord, int3 dims, float quality, uint seed)
        {
            float3 center = ((float3)dims - 1f) * 0.5f;
            float3 p = ((float3)coord - center) / math.max(center, new float3(ProceduralWreckageConstants.Epsilon));
            float q = Smooth01(quality);
            float width = math.lerp(0.34f, 0.54f, q);
            float height = math.lerp(0.42f, 0.78f, q);
            float spine = (p.x * p.x) / math.max(width * width, ProceduralWreckageConstants.Epsilon) +
                          (p.y * p.y) / math.max(height * height, ProceduralWreckageConstants.Epsilon);
            float nose = math.abs(p.z);
            float chaos = (HashToUnit(seed ^ (uint)(coord.x * 73856093) ^ (uint)(coord.y * 19349663) ^ (uint)(coord.z * 83492791)) - 0.5f) * math.lerp(0.05f, 0.18f, q);
            return spine + (nose * 0.38f) + chaos <= 1f;
        }

        public static float3 LocalCenterFromCoord(int3 coord, int3 dims, float cellSize)
        {
            float3 center = ((float3)dims - 1f) * 0.5f;
            return ((float3)coord - center) * math.max(cellSize, ProceduralWreckageConstants.Epsilon);
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
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct MockSectorTriggerJob : IJob
    {
        [WriteOnly]
        [NoAlias]
        public NativeArray<WreckageSectorTriggerDTO> SectorTriggers;

        [ReadOnly]
        [NoAlias]
        public NativeArray<WreckageTuningDTO> Tuning;

        [NoAlias]
        public NativeArray<WreckagePaddedCounterDTO> Counters;

        public double3 MockRootAUP;
        public uint WorldSeed;
        public uint SimulationFrameCounter;

        public void Execute()
        {
            if (!SectorTriggers.IsCreated || SectorTriggers.Length <= 0)
                return;

            WreckageTuningDTO tuning = Tuning.IsCreated && Tuning.Length > 0
                ? SanitizeTuning(Tuning[0])
                : BuildDefaultTuning();

            double3 root = ProceduralWreckageMath.IsFinite(MockRootAUP) ? MockRootAUP : double3.zero;
            uint sectorHash = ProceduralWreckageMath.HashDouble3(root);
            uint seed = ProceduralWreckageMath.Hash(sectorHash ^ WorldSeed ^ tuning.SeedSalt ^ SimulationFrameCounter);
            if (seed == 0u)
                seed = 1u;

            WreckageSectorTriggerDTO trigger = default;
            trigger.RootAUP = root;
            trigger.SectorHash = sectorHash;
            trigger.Seed = seed;
            trigger.GridDims = new int3(
                ProceduralWreckageConstants.GridResolutionX,
                ProceduralWreckageConstants.GridResolutionY,
                ProceduralWreckageConstants.GridResolutionZ);
            trigger.CellSize = math.max(tuning.CellSize, ProceduralWreckageConstants.Epsilon);
            trigger.GlobalQualityWeight = math.saturate(tuning.GlobalQualityWeight);
            trigger.SimulationFrame = SimulationFrameCounter;
            trigger.Flags = 1u;
            trigger.BacktrackLimit = math.max(1u, tuning.BacktrackLimit);
            SectorTriggers[0] = trigger;

            if (Counters.IsCreated && Counters.Length > 0)
            {
                WreckagePaddedCounterDTO counter = Counters[0];
                counter.StateHash = seed ^ sectorHash;
                counter.FaultFlags = 0;
                Counters[0] = counter;
            }
        }

        private static WreckageTuningDTO BuildDefaultTuning()
        {
            WreckageTuningDTO tuning = default;
            tuning.GlobalQualityWeight = 0.5f;
            tuning.ShearSeverity = 0.45f;
            tuning.DebrisScatterRadius = 80f;
            tuning.VisibilityDistanceMin = 100f;
            tuning.VisibilityDistanceMax = 500f;
            tuning.BacktrackLimit = 256u;
            tuning.MaxNodes = 192;
            tuning.MaxDebris = 512;
            tuning.CellSize = 8f;
            tuning.MaxGenerationMs = 2f;
            tuning.Version = 1u;
            tuning.SeedSalt = 0x121121u;
            return tuning;
        }

        private static WreckageTuningDTO SanitizeTuning(in WreckageTuningDTO tuning)
        {
            WreckageTuningDTO safe = tuning;
            safe.GlobalQualityWeight = math.saturate(tuning.GlobalQualityWeight);
            safe.ShearSeverity = math.saturate(tuning.ShearSeverity);
            safe.DebrisScatterRadius = math.max(tuning.DebrisScatterRadius, 1f);
            safe.VisibilityDistanceMin = math.max(tuning.VisibilityDistanceMin, 8f);
            safe.VisibilityDistanceMax = math.max(tuning.VisibilityDistanceMax, safe.VisibilityDistanceMin);
            safe.BacktrackLimit = math.max(1u, tuning.BacktrackLimit);
            safe.MaxNodes = math.clamp(tuning.MaxNodes, 1, ProceduralWreckageConstants.MaxWreckNodes);
            safe.MaxDebris = math.clamp(tuning.MaxDebris, 0, ProceduralWreckageConstants.MaxDebrisNodes);
            safe.CellSize = math.max(tuning.CellSize, ProceduralWreckageConstants.Epsilon);
            safe.MaxGenerationMs = math.max(tuning.MaxGenerationMs, 0.25f);
            safe.Version = tuning.Version == 0u ? 1u : tuning.Version;
            safe.SeedSalt = tuning.SeedSalt == 0u ? 0x121121u : tuning.SeedSalt;
            return safe;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct WreckageCollapseJob : IJob
    {
        [NoAlias]
        public NativeArray<WreckageGridCellDTO> Grid;

        [ReadOnly]
        [NoAlias]
        public NativeArray<WreckageRuleDTO> Rules;

        [ReadOnly]
        [NoAlias]
        public NativeArray<WreckageSectorTriggerDTO> SectorTriggers;

        [ReadOnly]
        [NoAlias]
        public NativeArray<WreckageTuningDTO> Tuning;

        [WriteOnly]
        [NoAlias]
        public NativeArray<WreckageNodeDTO> Nodes;

        [NoAlias]
        public NativeArray<WreckageDebugCellDTO> DebugCells;

        [NoAlias]
        public NativeArray<WreckagePaddedCounterDTO> Counters;

        [WriteOnly]
        [NoAlias]
        public NativeArray<WreckageGenerationTelemetryEntry> TelemetryRing;

        [NoAlias]
        public NativeArray<int> TelemetryCursor;

        public uint Frame;

        public void Execute()
        {
            if (!Grid.IsCreated || !Rules.IsCreated || !SectorTriggers.IsCreated || !Nodes.IsCreated || SectorTriggers.Length <= 0)
                return;

            WreckageSectorTriggerDTO trigger = SectorTriggers[0];
            WreckageTuningDTO tuning = ResolveTuning();
            float quality = math.saturate(trigger.GlobalQualityWeight > 0f ? trigger.GlobalQualityWeight : tuning.GlobalQualityWeight);
            float qualityCurve = ProceduralWreckageMath.Smooth01(quality);
            int activeRuleCount = ResolveActiveRuleCount();
            uint faultFlags = activeRuleCount <= 1 ? ProceduralWreckageConstants.FaultNoRules : 0u;
            ushort structuralMask = (ushort)(ProceduralWreckageMath.BuildRuleMask(activeRuleCount) & 0xFFFEu);
            if (structuralMask == 0)
                structuralMask = 0x0002;

            int3 dims = new int3(
                math.clamp(trigger.GridDims.x, 1, ProceduralWreckageConstants.GridResolutionX),
                math.clamp(trigger.GridDims.y, 1, ProceduralWreckageConstants.GridResolutionY),
                math.clamp(trigger.GridDims.z, 1, ProceduralWreckageConstants.GridResolutionZ));
            int cellCount = math.min(Grid.Length, dims.x * dims.y * dims.z);
            int targetNodes = math.clamp((int)math.round(math.lerp(32f, tuning.MaxNodes, qualityCurve)), 1, math.min(Nodes.Length, tuning.MaxNodes));
            uint seed = trigger.Seed == 0u ? 1u : trigger.Seed;
            float cellSize = math.max(trigger.CellSize > 0f ? trigger.CellSize : tuning.CellSize, ProceduralWreckageConstants.Epsilon);

            InitializeGrid(cellCount, dims, structuralMask, quality, seed, trigger.RootAUP, cellSize);
            ClearNodes();

            WreckagePaddedCounterDTO counter = default;
            counter.ActiveRuleCount = (uint)activeRuleCount;
            counter.StateHash = seed ^ trigger.SectorHash;
            counter.TelemetryCursor = Counters.IsCreated && Counters.Length > 0 ? Counters[0].TelemetryCursor : 0u;

            uint maxIterations = math.max(1u, trigger.BacktrackLimit);
            int iterations = 0;
            while (counter.NodeCount < targetNodes && iterations < cellCount && counter.BacktrackCount < maxIterations)
            {
                int selected = SelectNextCell(cellCount, seed ^ (uint)iterations);
                if (selected < 0)
                    break;

                WreckageGridCellDTO cell = Grid[selected];
                byte moduleId = SelectModule(cell.PossibleModuleMask, seed ^ (uint)selected ^ (uint)(iterations * 977));
                cell.CollapsedModuleId = moduleId;
                cell.PossibleModuleMask = (ushort)(1u << moduleId);
                cell.Entropy = 0f;
                Grid[selected] = cell;

                ConstrainNeighbors(selected, moduleId, dims, cellCount, ref counter);
                if (moduleId != 0)
                    EmitNode(selected, moduleId, dims, cellSize, trigger, ref counter);

                iterations++;
            }

            if (counter.BacktrackCount >= maxIterations)
                faultFlags |= ProceduralWreckageConstants.FaultContradiction;

            counter.FaultFlags |= (int)faultFlags;
            counter.StateHash ^= (uint)counter.NodeCount * 16777619u;
            if (Counters.IsCreated && Counters.Length > 0)
                Counters[0] = counter;

            WriteTelemetry(trigger.RootAUP, trigger.SectorHash, quality, counter, faultFlags, iterations);
        }

        private WreckageTuningDTO ResolveTuning()
        {
            if (!Tuning.IsCreated || Tuning.Length <= 0)
            {
                WreckageTuningDTO fallback = default;
                fallback.GlobalQualityWeight = 0.5f;
                fallback.ShearSeverity = 0.45f;
                fallback.DebrisScatterRadius = 80f;
                fallback.VisibilityDistanceMin = 100f;
                fallback.VisibilityDistanceMax = 500f;
                fallback.BacktrackLimit = 256u;
                fallback.MaxNodes = 192;
                fallback.MaxDebris = 512;
                fallback.CellSize = 8f;
                fallback.MaxGenerationMs = 2f;
                fallback.Version = 1u;
                fallback.SeedSalt = 0x121121u;
                return fallback;
            }

            WreckageTuningDTO tuning = Tuning[0];
            tuning.GlobalQualityWeight = math.saturate(tuning.GlobalQualityWeight);
            tuning.MaxNodes = math.clamp(tuning.MaxNodes, 1, ProceduralWreckageConstants.MaxWreckNodes);
            tuning.CellSize = math.max(tuning.CellSize, ProceduralWreckageConstants.Epsilon);
            return tuning;
        }

        private int ResolveActiveRuleCount()
        {
            int max = math.min(Rules.Length, ProceduralWreckageConstants.MaxModuleRules);
            int count = 0;
            for (int i = 0; i < max; i++)
            {
                WreckageRuleDTO rule = Rules[i];
                if (rule.ModuleHash == 0u && i > 0)
                    break;

                count = i + 1;
            }

            return math.max(count, 1);
        }

        private void InitializeGrid(int cellCount, int3 dims, ushort structuralMask, float quality, uint seed, double3 rootAup, float cellSize)
        {
            for (int i = 0; i < cellCount; i++)
            {
                int3 coord = ProceduralWreckageMath.ToCoord(i, dims);
                bool inside = ProceduralWreckageMath.IsInsideHull(coord, dims, quality, seed);
                WreckageGridCellDTO cell = default;
                cell.PossibleModuleMask = inside ? structuralMask : (ushort)1;
                cell.CollapsedModuleId = inside ? (byte)255 : (byte)0;
                cell.SocketConstraints = 0;
                cell.Entropy = inside ? math.max(1f, math.log2(math.max(ProceduralWreckageMath.PopCount(structuralMask), 1))) : 0f;
                cell.ParentIndex = uint.MaxValue;
                cell.Flags = inside ? 1u : 0u;
                Grid[i] = cell;

                if (DebugCells.IsCreated && i < DebugCells.Length)
                {
                    float3 local = ProceduralWreckageMath.LocalCenterFromCoord(coord, dims, cellSize);
                    WreckageDebugCellDTO debug = default;
                    debug.CenterAUP = rootAup + new double3(local.x, local.y, local.z);
                    debug.Extents = new float3(cellSize * 0.5f);
                    debug.SectorHash = ProceduralWreckageMath.HashDouble3(rootAup);
                    debug.CellIndex = (uint)i;
                    debug.State = inside ? (byte)0 : (byte)1;
                    debug.ModuleId = cell.CollapsedModuleId;
                    DebugCells[i] = debug;
                }
            }
        }

        private void ClearNodes()
        {
            int nodeClear = math.min(Nodes.Length, ProceduralWreckageConstants.MaxWreckNodes);
            for (int i = 0; i < nodeClear; i++)
                Nodes[i] = default;
        }

        private int SelectNextCell(int cellCount, uint salt)
        {
            int selected = -1;
            float bestEntropy = 3.40282347e+38f;
            uint bestTie = uint.MaxValue;
            for (int i = 0; i < cellCount; i++)
            {
                WreckageGridCellDTO cell = Grid[i];
                if (cell.CollapsedModuleId != 255 || cell.PossibleModuleMask == 0)
                    continue;

                uint tie = ProceduralWreckageMath.Hash(salt ^ (uint)i);
                float entropy = cell.Entropy + ((tie & 1023u) * 0.0000001f);
                if (entropy < bestEntropy || (math.abs(entropy - bestEntropy) <= 0.000001f && tie < bestTie))
                {
                    selected = i;
                    bestEntropy = entropy;
                    bestTie = tie;
                }
            }

            return selected;
        }

        private byte SelectModule(ushort mask, uint salt)
        {
            ushort safeMask = mask == 0 ? (ushort)0x0002 : mask;
            byte selected = ProceduralWreckageMath.SelectNthSetBit(safeMask, ProceduralWreckageMath.Hash(salt));
            return selected == 0 && (safeMask & 0xFFFEu) != 0
                ? ProceduralWreckageMath.SelectNthSetBit((ushort)(safeMask & 0xFFFEu), ProceduralWreckageMath.Hash(salt ^ 0xBADC0DEu))
                : selected;
        }

        private void ConstrainNeighbors(int selected, byte moduleId, int3 dims, int cellCount, ref WreckagePaddedCounterDTO counter)
        {
            WreckageRuleDTO currentRule = Rules[math.min(moduleId, Rules.Length - 1)];
            int3 coord = ProceduralWreckageMath.ToCoord(selected, dims);
            for (int direction = 0; direction < 6; direction++)
            {
                int3 neighborCoord = coord + ProceduralWreckageMath.DirectionOffset(direction);
                if (math.any(neighborCoord < 0) || neighborCoord.x >= dims.x || neighborCoord.y >= dims.y || neighborCoord.z >= dims.z)
                    continue;

                int neighborIndex = ProceduralWreckageMath.ToIndex(neighborCoord, dims);
                if ((uint)neighborIndex >= (uint)cellCount)
                    continue;

                WreckageGridCellDTO neighbor = Grid[neighborIndex];
                if (neighbor.CollapsedModuleId != 255)
                    continue;

                ushort compatible = BuildCompatibleMask(currentRule, direction, neighbor.PossibleModuleMask);
                ushort nextMask = (ushort)(neighbor.PossibleModuleMask & compatible);
                if (nextMask == 0)
                {
                    nextMask = 0x0001;
                    counter.BacktrackCount++;
                }

                if (nextMask != neighbor.PossibleModuleMask)
                {
                    neighbor.PossibleModuleMask = nextMask;
                    neighbor.Entropy = math.max(0f, math.log2(math.max(ProceduralWreckageMath.PopCount(nextMask), 1)));
                    neighbor.SocketConstraints = (byte)(neighbor.SocketConstraints | (1 << ProceduralWreckageMath.OppositeDirection(direction)));
                    Grid[neighborIndex] = neighbor;
                }
            }
        }

        private ushort BuildCompatibleMask(in WreckageRuleDTO currentRule, int direction, ushort neighborMask)
        {
            ushort mask = 0;
            ushort currentSocket = ProceduralWreckageMath.SocketAt(currentRule, direction);
            int opposite = ProceduralWreckageMath.OppositeDirection(direction);
            int maxRule = math.min(Rules.Length, ProceduralWreckageConstants.MaxModuleRules);
            for (int module = 0; module < maxRule; module++)
            {
                if (((neighborMask >> module) & 1) == 0)
                    continue;

                WreckageRuleDTO candidate = Rules[module];
                ushort candidateSocket = ProceduralWreckageMath.SocketAt(candidate, opposite);
                if ((currentSocket & candidateSocket) != 0)
                    mask |= (ushort)(1 << module);
            }

            return mask;
        }

        private void EmitNode(int cellIndex, byte moduleId, int3 dims, float cellSize, in WreckageSectorTriggerDTO trigger, ref WreckagePaddedCounterDTO counter)
        {
            if (counter.NodeCount >= Nodes.Length)
            {
                counter.FaultFlags |= (int)ProceduralWreckageConstants.FaultCapacity;
                return;
            }

            WreckageRuleDTO rule = Rules[math.min(moduleId, Rules.Length - 1)];
            if ((rule.Flags & WreckageRuleFlags.Structural) == 0)
                return;

            int3 coord = ProceduralWreckageMath.ToCoord(cellIndex, dims);
            float3 local = ProceduralWreckageMath.LocalCenterFromCoord(coord, dims, cellSize);
            double3 aup = trigger.RootAUP + new double3(local.x, local.y, local.z);
            float3 extents = math.max(rule.BoundsExtents, new float3(cellSize * 0.25f));
            float radiusSq = math.max(math.dot(extents, extents), ProceduralWreckageConstants.Epsilon);
            uint stableId = ProceduralWreckageMath.Hash(trigger.SectorHash ^ (uint)cellIndex ^ ((uint)moduleId << 24));
            uint degree = CountSocketDegree(rule);

            WreckageNodeDTO node = default;
            node.LocalMatrix = float4x4.TRS(local, quaternion.identity, new float3(1f));
            node.PrefabHash = rule.PrefabHash;
            node.StateFlags = WreckageNodeFlags.Alive | WreckageNodeFlags.Structural;
            if ((rule.Flags & WreckageRuleFlags.TerminusEligible) != 0 && degree <= 2u)
                node.StateFlags |= WreckageNodeFlags.Terminus;

            node.SectorAUP = aup;
            node.BoundsExtents = extents;
            node.BoundsRadius = radiusSq * math.rsqrt(radiusSq);
            node.SectorHash = trigger.SectorHash;
            node.ModuleId = moduleId;
            node.GraphDegree = degree;
            node.StableId = stableId;

            if (!ProceduralWreckageMath.IsFinite(node.LocalMatrix) || !ProceduralWreckageMath.IsFinite(node.SectorAUP))
            {
                node.LocalMatrix = float4x4.identity;
                node.SectorAUP = trigger.RootAUP;
                node.StateFlags |= WreckageNodeFlags.NonFiniteFallback;
                counter.FaultFlags |= (int)ProceduralWreckageConstants.FaultNonFinite;
            }

            Nodes[counter.NodeCount] = node;
            counter.StateHash = (counter.StateHash ^ stableId) * 16777619u;
            counter.NodeCount++;

            if (DebugCells.IsCreated && cellIndex < DebugCells.Length)
            {
                WreckageDebugCellDTO debug = DebugCells[cellIndex];
                debug.State = 1;
                debug.ModuleId = moduleId;
                DebugCells[cellIndex] = debug;
            }
        }

        private static uint CountSocketDegree(in WreckageRuleDTO rule)
        {
            uint degree = 0u;
            degree += rule.SocketNorth != 0 ? 1u : 0u;
            degree += rule.SocketEast != 0 ? 1u : 0u;
            degree += rule.SocketSouth != 0 ? 1u : 0u;
            degree += rule.SocketWest != 0 ? 1u : 0u;
            degree += rule.SocketTop != 0 ? 1u : 0u;
            degree += rule.SocketBottom != 0 ? 1u : 0u;
            return degree;
        }

        private void WriteTelemetry(double3 rootAup, uint sectorHash, float quality, in WreckagePaddedCounterDTO counter, uint faultFlags, int iterations)
        {
            if (!TelemetryRing.IsCreated || !TelemetryCursor.IsCreated || TelemetryRing.Length <= 0 || TelemetryCursor.Length <= 0)
                return;

            int cursor = TelemetryCursor[0];
            if ((uint)cursor >= (uint)TelemetryRing.Length)
                cursor = 0;

            WreckageGenerationTelemetryEntry entry = default;
            entry.RootAUP = rootAup;
            entry.Frame = Frame;
            entry.SectorHash = sectorHash;
            entry.CollapsedModules = counter.NodeCount;
            entry.BacktrackIterations = counter.BacktrackCount;
            entry.EstimatedComputeMs = iterations * 0.0002f;
            entry.GlobalQualityWeight = quality;
            entry.StateHash = counter.StateHash;
            entry.FaultFlags = faultFlags | (uint)counter.FaultFlags;
            entry.RenderedModules = (uint)counter.RenderMatrixCount;
            entry.DebrisCount = (uint)counter.DebrisCount;
            TelemetryRing[cursor] = entry;

            cursor++;
            if (cursor >= TelemetryRing.Length)
                cursor = 0;

            TelemetryCursor[0] = cursor;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ApplyStructuralShearJob : IJobParallelFor
    {
        [NoAlias]
        public NativeArray<WreckageNodeDTO> Nodes;

        [ReadOnly]
        [NoAlias]
        public NativeArray<WreckageTuningDTO> Tuning;

        public uint Frame;

        public void Execute(int index)
        {
            if (!Nodes.IsCreated || (uint)index >= (uint)Nodes.Length)
                return;

            WreckageNodeDTO node = Nodes[index];
            if ((node.StateFlags & WreckageNodeFlags.Alive) == 0 || (node.StateFlags & WreckageNodeFlags.Structural) == 0)
                return;

            WreckageTuningDTO tuning = Tuning.IsCreated && Tuning.Length > 0 ? Tuning[0] : default;
            float quality = math.saturate(tuning.GlobalQualityWeight);
            float severity = math.saturate(tuning.ShearSeverity);
            uint seed = ProceduralWreckageMath.Hash(node.StableId ^ node.SectorHash ^ Frame);
            if (seed == 0u)
                seed = 1u;

            Unity.Mathematics.Random random = new Unity.Mathematics.Random(seed);
            float deleteChance = severity * math.lerp(0.04f, 0.11f, ProceduralWreckageMath.Smooth01(quality));
            if (random.NextFloat() < deleteChance && (node.StateFlags & WreckageNodeFlags.Terminus) == 0)
            {
                node.StateFlags &= ~WreckageNodeFlags.Alive;
                Nodes[index] = node;
                return;
            }

            float3 axis = random.NextFloat3(new float3(-1f), new float3(1f));
            float axisSq = math.dot(axis, axis);
            axis = axis * math.rsqrt(math.max(axisSq, ProceduralWreckageConstants.Epsilon));
            float angle = (random.NextFloat(-1f, 1f) * severity) * math.lerp(0.08f, 0.36f, ProceduralWreckageMath.Smooth01(quality));
            quaternion shear = quaternion.AxisAngle(axis, angle);
            float3 translation = node.LocalMatrix.c3.xyz;
            float4x4 rotated = math.mul(float4x4.Rotate(shear), node.LocalMatrix);
            rotated.c3 = new float4(translation, 1f);
            node.LocalMatrix = ProceduralWreckageMath.IsFinite(rotated) ? rotated : node.LocalMatrix;
            node.StateFlags |= WreckageNodeFlags.Sheared;
            Nodes[index] = node;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateDebrisFieldJob : IJob
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<WreckageSectorTriggerDTO> SectorTriggers;

        [ReadOnly]
        [NoAlias]
        public NativeArray<WreckageTuningDTO> Tuning;

        [WriteOnly]
        [NoAlias]
        public NativeArray<WreckageNodeDTO> DebrisNodes;

        [NoAlias]
        public NativeArray<WreckagePaddedCounterDTO> Counters;

        public uint Frame;

        public void Execute()
        {
            if (!DebrisNodes.IsCreated || !SectorTriggers.IsCreated || SectorTriggers.Length <= 0)
                return;

            WreckageSectorTriggerDTO trigger = SectorTriggers[0];
            WreckageTuningDTO tuning = Tuning.IsCreated && Tuning.Length > 0 ? Tuning[0] : default;
            float quality = math.saturate(tuning.GlobalQualityWeight);
            float curve = ProceduralWreckageMath.Smooth01(quality);
            int maxDebris = math.clamp(tuning.MaxDebris <= 0 ? 512 : tuning.MaxDebris, 0, DebrisNodes.Length);
            int debrisCount = math.clamp((int)math.round(math.lerp(64f, maxDebris, curve)), 0, DebrisNodes.Length);
            float radius = math.max(tuning.DebrisScatterRadius, 1f);
            uint seed = trigger.Seed == 0u ? 1u : trigger.Seed;
            uint faultFlags = 0u;

            for (int i = 0; i < DebrisNodes.Length; i++)
                DebrisNodes[i] = default;

            for (int i = 0; i < debrisCount; i++)
            {
                uint h = ProceduralWreckageMath.Hash(seed ^ (uint)(i * 747796405) ^ Frame);
                float u = ProceduralWreckageMath.HashToUnit(h);
                float v = ProceduralWreckageMath.HashToUnit(h ^ 0x9E3779B9u);
                float angle = u * 6.28318530718f;
                float ring = math.lerp(0.2f, 1f, v);
                float2 baseDir = new float2(math.cos(angle), math.sin(angle));
                float2 curl = CurlNoise(baseDir * ring, seed);
                float2 offset2 = (baseDir + curl * math.lerp(0.12f, 0.38f, curve)) * (radius * ring);
                float3 local = new float3(offset2.x, math.lerp(-2f, 2f, ProceduralWreckageMath.HashToUnit(h ^ 0x51515151u)), offset2.y);
                double3 aup = trigger.RootAUP + new double3(local.x, local.y, local.z);
                float scale = math.lerp(0.35f, 1.4f, ProceduralWreckageMath.HashToUnit(h ^ 0xA5A5A5A5u)) * math.lerp(0.75f, 1.35f, curve);
                quaternion rotation = quaternion.EulerXYZ(
                    ProceduralWreckageMath.HashToUnit(h ^ 0x11u) * 6.28318530718f,
                    ProceduralWreckageMath.HashToUnit(h ^ 0x22u) * 6.28318530718f,
                    ProceduralWreckageMath.HashToUnit(h ^ 0x33u) * 6.28318530718f);

                WreckageNodeDTO node = default;
                node.LocalMatrix = float4x4.TRS(local, rotation, new float3(scale));
                node.PrefabHash = ProceduralWreckageMath.Hash(0x53435250u ^ h);
                node.StateFlags = WreckageNodeFlags.Alive | WreckageNodeFlags.Debris;
                node.SectorAUP = aup;
                node.BoundsExtents = new float3(scale);
                node.BoundsRadius = math.max(scale, ProceduralWreckageConstants.Epsilon);
                node.SectorHash = trigger.SectorHash;
                node.ModuleId = 0;
                node.GraphDegree = 0;
                node.StableId = h;
                if (!ProceduralWreckageMath.IsFinite(node.LocalMatrix) || !ProceduralWreckageMath.IsFinite(node.SectorAUP))
                {
                    node.LocalMatrix = float4x4.TRS(float3.zero, quaternion.identity, new float3(0.5f));
                    node.SectorAUP = trigger.RootAUP;
                    node.BoundsExtents = new float3(0.5f);
                    node.BoundsRadius = 0.5f;
                    node.StateFlags |= WreckageNodeFlags.NonFiniteFallback;
                    faultFlags |= ProceduralWreckageConstants.FaultNonFinite;
                }

                DebrisNodes[i] = node;
            }

            if (Counters.IsCreated && Counters.Length > 0)
            {
                WreckagePaddedCounterDTO counter = Counters[0];
                counter.DebrisCount = debrisCount;
                counter.FaultFlags |= (int)faultFlags;
                counter.StateHash ^= (uint)debrisCount * 16777619u;
                Counters[0] = counter;
            }
        }

        private static float2 CurlNoise(float2 p, uint seed)
        {
            const float e = 0.125f;
            float n1 = Noise(p + new float2(0f, e), seed);
            float n2 = Noise(p - new float2(0f, e), seed);
            float n3 = Noise(p + new float2(e, 0f), seed);
            float n4 = Noise(p - new float2(e, 0f), seed);
            float inv = 1f / (2f * e);
            return new float2((n1 - n2) * inv, (n4 - n3) * inv);
        }

        private static float Noise(float2 p, uint seed)
        {
            int2 ip = (int2)math.floor(p * 8f);
            uint h = seed ^ (uint)(ip.x * 73856093) ^ (uint)(ip.y * 19349663);
            return ProceduralWreckageMath.HashToUnit(h) * 2f - 1f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ExtractRenderMatricesJob : IJob
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<WreckageNodeDTO> Nodes;

        [ReadOnly]
        [NoAlias]
        public NativeArray<WreckageNodeDTO> DebrisNodes;

        [ReadOnly]
        [NoAlias]
        public NativeArray<WreckageTuningDTO> Tuning;

        [ReadOnly]
        [NoAlias]
        public NativeArray<WreckageHzbTileDTO> HzbTiles;

        [WriteOnly]
        [NoAlias]
        public NativeArray<float4x4> RenderMatrices;

        [WriteOnly]
        [NoAlias]
        public NativeArray<WreckageIndirectArgsDTO> IndirectArgs;

        [WriteOnly]
        [NoAlias]
        public NativeArray<WreckageGpuScalarDTO> GpuScalars;

        [NoAlias]
        public NativeArray<WreckagePaddedCounterDTO> Counters;

        public double3 CameraAUP;
        public float4x4 CameraRelativeViewProjection;
        public int HzbWidth;
        public int HzbHeight;
        public uint VertexCountPerInstance;
        public uint Frame;

        public void Execute()
        {
            if (!RenderMatrices.IsCreated || !IndirectArgs.IsCreated || IndirectArgs.Length <= 0)
                return;

            WreckageTuningDTO tuning = Tuning.IsCreated && Tuning.Length > 0 ? Tuning[0] : default;
            float quality = math.saturate(tuning.GlobalQualityWeight);
            float curve = ProceduralWreckageMath.Smooth01(quality);
            float maxDistance = math.lerp(math.max(tuning.VisibilityDistanceMin, 8f), math.max(tuning.VisibilityDistanceMax, tuning.VisibilityDistanceMin + 1f), curve);
            float maxDistanceSq = maxDistance * maxDistance;
            int write = 0;
            uint stateHash = 2166136261u;
            write = ExtractNodeSet(Nodes, maxDistanceSq, 1f, quality, ref stateHash, write);
            write = ExtractNodeSet(DebrisNodes, maxDistanceSq * math.lerp(0.12f, 0.58f, curve), math.lerp(0.25f, 1f, curve), quality, ref stateHash, write);

            WreckageIndirectArgsDTO args = default;
            args.VertexCountPerInstance = math.max(1u, VertexCountPerInstance);
            args.InstanceCount = (uint)write;
            args.StartVertex = 0u;
            args.StartInstance = 0u;
            IndirectArgs[0] = args;

            if (GpuScalars.IsCreated && GpuScalars.Length > 0)
            {
                WreckageGpuScalarDTO scalar = default;
                scalar.CausticRustSiltQuality = new float4(
                    math.lerp(0.35f, 1.25f, curve),
                    math.lerp(0.2f, 1.0f, curve),
                    math.lerp(0.25f, 0.85f, curve),
                    quality);
                scalar.BoundsAndDensity = new float4(maxDistance, maxDistanceSq, write, RenderMatrices.Length);
                scalar.FaultAndFrame = new float4(0f, Frame, HzbWidth, HzbHeight);
                scalar.StateHash = stateHash;
                GpuScalars[0] = scalar;
            }

            if (Counters.IsCreated && Counters.Length > 0)
            {
                WreckagePaddedCounterDTO counter = Counters[0];
                counter.RenderMatrixCount = write;
                counter.StateHash ^= stateHash;
                Counters[0] = counter;
            }
        }

        private int ExtractNodeSet(NativeArray<WreckageNodeDTO> source, float maxDistanceSq, float densityGate, float quality, ref uint stateHash, int write)
        {
            if (!source.IsCreated)
                return write;

            for (int i = 0; i < source.Length && write < RenderMatrices.Length; i++)
            {
                WreckageNodeDTO node = source[i];
                if ((node.StateFlags & WreckageNodeFlags.Alive) == 0)
                    continue;

                float stochastic = ProceduralWreckageMath.HashToUnit(node.StableId ^ (uint)i);
                if (stochastic > math.saturate(math.lerp(0.18f, 1f, ProceduralWreckageMath.Smooth01(quality)) * densityGate))
                    continue;

                double3 deltaD = node.SectorAUP - CameraAUP;
                if (!ProceduralWreckageMath.IsFinite(deltaD))
                    continue;

                float3 delta = new float3((float)deltaD.x, (float)deltaD.y, (float)deltaD.z);
                float distSq = math.dot(delta, delta);
                if (!math.isfinite(distSq) || distSq > maxDistanceSq)
                    continue;

                if (IsOccluded(delta, node.BoundsRadius))
                    continue;

                float4x4 matrix = node.LocalMatrix;
                matrix.c3 = new float4(delta, 1f);
                if (!ProceduralWreckageMath.IsFinite(matrix))
                    continue;

                RenderMatrices[write] = matrix;
                stateHash = (stateHash ^ node.StableId) * 16777619u;
                write++;
            }

            return write;
        }

        private bool IsOccluded(float3 local, float radius)
        {
            if (!HzbTiles.IsCreated || HzbWidth <= 0 || HzbHeight <= 0)
                return false;

            float4 clip = math.mul(CameraRelativeViewProjection, new float4(local, 1f));
            if (clip.w <= ProceduralWreckageConstants.Epsilon)
                return false;

            float invW = math.rcp(math.max(math.abs(clip.w), ProceduralWreckageConstants.Epsilon));
            float2 uv = (clip.xy * invW * 0.5f) + 0.5f;
            if (math.any(uv < 0f) || math.any(uv > 1f))
                return false;

            int x = math.clamp((int)math.floor(uv.x * HzbWidth), 0, HzbWidth - 1);
            int y = math.clamp((int)math.floor(uv.y * HzbHeight), 0, HzbHeight - 1);
            int index = math.clamp(x + (y * HzbWidth), 0, HzbTiles.Length - 1);
            WreckageHzbTileDTO tile = HzbTiles[index];
            if (tile.Depth01 <= 0f)
                return false;

            float depth01 = math.saturate(clip.z * invW);
            float radiusBias = math.saturate(radius * 0.001f);
            return depth01 - radiusBias > tile.Depth01 + 0.002f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct InjectLootRequestsJob : IJob
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<WreckageNodeDTO> Nodes;

        [WriteOnly]
        [NoAlias]
        public NativeArray<LootSpawnRequestDTO> LootRequests;

        [NoAlias]
        public NativeArray<WreckagePaddedCounterDTO> Counters;

        public uint LootTableHash;

        public void Execute()
        {
            if (!Nodes.IsCreated || !LootRequests.IsCreated)
                return;

            int write = 0;
            for (int i = 0; i < LootRequests.Length; i++)
                LootRequests[i] = default;

            for (int i = 0; i < Nodes.Length && write < LootRequests.Length; i++)
            {
                WreckageNodeDTO node = Nodes[i];
                if ((node.StateFlags & WreckageNodeFlags.Alive) == 0 || (node.StateFlags & WreckageNodeFlags.Terminus) == 0)
                    continue;

                LootSpawnRequestDTO request = default;
                request.AUP = node.SectorAUP;
                request.SectorHash = node.SectorHash;
                request.LootTableHash = LootTableHash == 0u ? ProceduralWreckageMath.Hash(0x4C4F4F54u ^ node.StableId) : LootTableHash;
                request.NodeIndex = (uint)i;
                request.Quantity = 1u + (ProceduralWreckageMath.Hash(node.StableId) & 3u);
                request.Flags = 1u;
                request.StableId = node.StableId;
                LootRequests[write++] = request;
            }

            if (Counters.IsCreated && Counters.Length > 0)
            {
                WreckagePaddedCounterDTO counter = Counters[0];
                counter.LootCount = write;
                Counters[0] = counter;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct StageCollisionProxiesJob : IJob
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<WreckageNodeDTO> Nodes;

        [WriteOnly]
        [NoAlias]
        public NativeArray<WreckageBoxColliderDTO> CollisionProxies;

        [NoAlias]
        public NativeArray<WreckagePaddedCounterDTO> Counters;

        public void Execute()
        {
            if (!Nodes.IsCreated || !CollisionProxies.IsCreated)
                return;

            int write = 0;
            for (int i = 0; i < CollisionProxies.Length; i++)
                CollisionProxies[i] = default;

            for (int i = 0; i < Nodes.Length && write < CollisionProxies.Length; i++)
            {
                WreckageNodeDTO node = Nodes[i];
                if ((node.StateFlags & WreckageNodeFlags.Alive) == 0 || (node.StateFlags & WreckageNodeFlags.Structural) == 0)
                    continue;

                WreckageBoxColliderDTO proxy = default;
                proxy.CenterAUP = node.SectorAUP;
                proxy.Extents = math.max(node.BoundsExtents, new float3(0.25f));
                proxy.ModuleIndex = (uint)i;
                proxy.Rotation = new float4(0f, 0f, 0f, 1f);
                proxy.Flags = 1u;
                proxy.SectorHash = node.SectorHash;
                CollisionProxies[write++] = proxy;
            }

            if (Counters.IsCreated && Counters.Length > 0)
            {
                WreckagePaddedCounterDTO counter = Counters[0];
                counter.CollisionProxyCount = write;
                Counters[0] = counter;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct WreckageSelfAuditJob : IJob
    {
        [ReadOnly]
        [NoAlias]
        public NativeArray<WreckageNodeDTO> Nodes;

        [ReadOnly]
        [NoAlias]
        public NativeArray<WreckagePaddedCounterDTO> Counters;

        [WriteOnly]
        [NoAlias]
        public NativeArray<WreckageSelfAuditResultDTO> Results;

        public uint Frame;

        public void Execute()
        {
            if (!Nodes.IsCreated || !Results.IsCreated || Results.Length <= 0)
                return;

            int live = 0;
            int openHull = 0;
            uint sectorHash = 0u;
            uint stateHash = 2166136261u;
            int nodeCount = Counters.IsCreated && Counters.Length > 0 ? math.clamp(Counters[0].NodeCount, 0, Nodes.Length) : Nodes.Length;
            int probeLimit = math.min(nodeCount, 256);
            int overlapPairs = 0;
            float maxOverlap = 0f;
            uint faultFlags = 0u;

            for (int i = 0; i < nodeCount; i++)
            {
                WreckageNodeDTO node = Nodes[i];
                if ((node.StateFlags & WreckageNodeFlags.Alive) == 0 || (node.StateFlags & WreckageNodeFlags.Structural) == 0)
                    continue;

                live++;
                sectorHash = node.SectorHash;
                stateHash = (stateHash ^ node.StableId) * 16777619u;
                if (node.GraphDegree < 2u)
                    openHull++;
            }

            for (int a = 0; a < probeLimit; a++)
            {
                WreckageNodeDTO nodeA = Nodes[a];
                if ((nodeA.StateFlags & WreckageNodeFlags.Alive) == 0)
                    continue;

                for (int b = a + 1; b < probeLimit; b++)
                {
                    WreckageNodeDTO nodeB = Nodes[b];
                    if ((nodeB.StateFlags & WreckageNodeFlags.Alive) == 0)
                        continue;

                    double3 deltaD = nodeA.SectorAUP - nodeB.SectorAUP;
                    if (!ProceduralWreckageMath.IsFinite(deltaD))
                    {
                        faultFlags |= ProceduralWreckageConstants.FaultNonFinite;
                        continue;
                    }

                    float3 delta = new float3((float)deltaD.x, (float)deltaD.y, (float)deltaD.z);
                    float distanceSq = math.max(math.dot(delta, delta), ProceduralWreckageConstants.Epsilon);
                    float distance = distanceSq * math.rsqrt(distanceSq);
                    float overlap = (nodeA.BoundsRadius + nodeB.BoundsRadius) - distance;
                    if (overlap > 0.01f)
                    {
                        overlapPairs++;
                        maxOverlap = math.max(maxOverlap, overlap);
                    }
                }
            }

            WreckageSelfAuditResultDTO result = default;
            result.Frame = Frame;
            result.SectorHash = sectorHash;
            result.OpenHullNodeCount = (uint)openHull;
            result.OverlapPairCount = (uint)overlapPairs;
            result.LiveNodeCount = (uint)live;
            result.RenderMatrixCount = Counters.IsCreated && Counters.Length > 0 ? (uint)math.max(0, Counters[0].RenderMatrixCount) : 0u;
            result.StateHash = stateHash;
            result.MaxOverlapDepth = maxOverlap;
            result.ClosedHullRatio = live > 0 ? 1f - ((float)openHull * math.rcp(math.max(live, 1))) : 0f;
            result.Flags |= faultFlags;
            if (openHull > math.max(2, live / 3))
                result.Flags |= ProceduralWreckageConstants.FaultOpenHull;
            if (overlapPairs > 0)
                result.Flags |= ProceduralWreckageConstants.FaultContradiction;

            Results[0] = result;
        }
    }
}
