using System.Runtime.InteropServices;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Construction
{
    public static class HabitatDeconstructionTransactionKernel
    {
        public const int MaxCostPairs = 4;
        public const int MaxTeardownsLow = 5;
        public const int MaxTeardownsUltra = 50;
        public const int TelemetryCapacity = 300;
#if UNITY_EDITOR
        public const int CsvScratchBytes = 4096;
#endif
        public const int RefundProfileCapacity = 16;
        public const uint SystemHash = 0x53333336u; // S336
        public const uint FaultInvalidLayout = 1u << 0;
        public const uint FaultMissingCost = 1u << 1;
        public const uint FaultInvalidAup = 1u << 2;
        public const uint FaultNoGraph = 1u << 3;
        public const uint FaultRefundOverflow = 1u << 4;
        public const uint FaultNaN = 1u << 5;
        public const uint FaultBudgetExceeded = 1u << 6;

        public const byte RefundStatusPendingInventory = 0;
        public const byte RefundStatusOverflowLootCache = 1;
        public const byte RefundStatusInvalid = 2;

        public static int ResolveMaxTeardownsPerFrame(float globalQualityWeight)
        {
            float q = math.saturate(math.select(0f, globalQualityWeight, math.isfinite(globalQualityWeight)));
            return math.clamp((int)math.round(math.lerp(MaxTeardownsLow, MaxTeardownsUltra, q)), MaxTeardownsLow, MaxTeardownsUltra);
        }

        public static bool RuntimeLayoutValid()
        {
            return UnsafeUtility.SizeOf<DeconstructionTransactionDTO>() == 32 &&
                   UnsafeUtility.SizeOf<RefundCommandDTO>() == 32 &&
                   UnsafeUtility.SizeOf<LootCacheDTO>() == 64 &&
                   UnsafeUtility.SizeOf<TeardownTelemetryEntry>() == 64 &&
                   UnsafeUtility.SizeOf<RefundProfileDTO>() == 32 &&
                   Marshal.OffsetOf<DeconstructionTransactionDTO>(nameof(DeconstructionTransactionDTO.TargetModuleHash)).ToInt32() == 0 &&
                   Marshal.OffsetOf<DeconstructionTransactionDTO>(nameof(DeconstructionTransactionDTO.InitiatorEntityHash)).ToInt32() == 4 &&
                   Marshal.OffsetOf<DeconstructionTransactionDTO>(nameof(DeconstructionTransactionDTO.OriginalAUP)).ToInt32() == 8;
        }

        public static uint HashTransaction(in DeconstructionTransactionDTO transaction, int nodeIndex, uint frame)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ transaction.TargetModuleHash) * 16777619u;
                hash = (hash ^ transaction.InitiatorEntityHash) * 16777619u;
                hash = (hash ^ (uint)nodeIndex) * 16777619u;
                hash = (hash ^ frame) * 16777619u;
                hash = (hash ^ math.asuint((float)transaction.OriginalAUP.x)) * 16777619u;
                hash = (hash ^ math.asuint((float)transaction.OriginalAUP.y)) * 16777619u;
                hash = (hash ^ math.asuint((float)transaction.OriginalAUP.z)) * 16777619u;
                return hash;
            }
        }

        public static uint HashRefund(uint itemHash, int quantity, uint stateHash)
        {
            unchecked
            {
                stateHash = (stateHash ^ itemHash) * 16777619u;
                stateHash = (stateHash ^ (uint)quantity) * 16777619u;
                return stateHash;
            }
        }

        public static uint HashCache(in LootCacheDTO cache, uint stateHash)
        {
            unchecked
            {
                stateHash = (stateHash ^ cache.ItemHash) * 16777619u;
                stateHash = (stateHash ^ (uint)cache.Quantity) * 16777619u;
                stateHash = (stateHash ^ cache.Sequence) * 16777619u;
                stateHash = (stateHash ^ math.asuint(cache.LocalOffset.x)) * 16777619u;
                stateHash = (stateHash ^ math.asuint(cache.LocalOffset.y)) * 16777619u;
                stateHash = (stateHash ^ math.asuint(cache.LocalOffset.z)) * 16777619u;
                return stateHash;
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DeconstructionTransactionDTO
    {
        [FieldOffset(0)] public uint TargetModuleHash;
        [FieldOffset(4)] public uint InitiatorEntityHash;
        [FieldOffset(8)] public double3 OriginalAUP;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct RefundCommandDTO
    {
        [FieldOffset(0)] public uint ItemHash;
        [FieldOffset(4)] public int Quantity;
        [FieldOffset(8)] public uint TargetModuleHash;
        [FieldOffset(12)] public uint Sequence;
        [FieldOffset(16)] public byte Status;
        [FieldOffset(17)] public byte PairIndex;
        [FieldOffset(18)] public ushort Reserved0;
        [FieldOffset(20)] public uint StateHash;
        [FieldOffset(24)] private ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct LootCacheDTO
    {
        [FieldOffset(0)] public double3 PositionAup;
        [FieldOffset(24)] public float3 LocalOffset;
        [FieldOffset(36)] public uint ItemHash;
        [FieldOffset(40)] public int Quantity;
        [FieldOffset(44)] public uint SourceModuleHash;
        [FieldOffset(48)] public uint Sequence;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] private ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct TeardownTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint TargetModuleHash;
        [FieldOffset(8)] public uint InitiatorEntityHash;
        [FieldOffset(12)] public uint StateHash;
        [FieldOffset(16)] public int ModulesProcessed;
        [FieldOffset(20)] public int ResourcesRefunded;
        [FieldOffset(24)] public int OverflowLootCaches;
        [FieldOffset(28)] public int EdgesSevered;
        [FieldOffset(32)] public float BurstMicroseconds;
        [FieldOffset(36)] public float GlobalQualityWeight;
        [FieldOffset(40)] public uint FaultFlags;
        [FieldOffset(44)] public int TargetNodeIndex;
        [FieldOffset(48)] public double AupLocalMagnitude;
        [FieldOffset(56)] private ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct RefundProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public float RefundScalar01;
        [FieldOffset(8)] public float OverflowOffsetMeters;
        [FieldOffset(12)] public float GlobalQualityWeight;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public uint RowHash;
        [FieldOffset(24)] private ulong _pad0;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ExecuteModuleTeardownJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<DeconstructionTransactionDTO> Transactions;
        [ReadOnly, NoAlias] public NativeArray<ModuleCostDTO> ModuleCosts;
        [ReadOnly, NoAlias] public NativeArray<int> EdgeOffsets;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> EdgeDestinations;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<float> EdgeStrength;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<byte> EdgeFlags;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<RefundCommandDTO> RefundCommands;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> RefundCommandCount;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<LootCacheDTO> LootCaches;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> LootCacheCount;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<TeardownTelemetryEntry> TelemetryRing;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> TelemetryCursor;
        public int TransactionCount;
        public int ModuleCostCount;
        public int TargetNodeIndex;
        public int NodeCount;
        public int EdgeCount;
        public int MaxTeardownsPerFrame;
        public uint Frame;
        public uint SequenceBase;
        public uint LayoutValid;
        public float GlobalQualityWeight;

        public void Execute()
        {
            int maxTransactions = math.min(
                math.min(TransactionCount, MaxTeardownsPerFrame),
                Transactions.IsCreated ? Transactions.Length : 0);
            int refundWrite = 0;
            int lootWrite = LootCacheCount.IsCreated && LootCacheCount.Length > 0 ? math.max(0, LootCacheCount[0]) : 0;
            uint faultFlags = 0u;
            int totalRefunded = 0;
            int totalEdgesSevered = 0;
            uint stateHash = 2166136261u;
            uint targetHash = 0u;
            uint initiatorHash = 0u;
            double aupMagnitude = 0d;

            if (LayoutValid == 0u)
                faultFlags |= HabitatDeconstructionTransactionKernel.FaultInvalidLayout;

            for (int transactionIndex = 0; transactionIndex < maxTransactions; transactionIndex++)
            {
                DeconstructionTransactionDTO transaction = Transactions[transactionIndex];
                targetHash = transaction.TargetModuleHash;
                initiatorHash = transaction.InitiatorEntityHash;
                stateHash = HabitatDeconstructionTransactionKernel.HashTransaction(in transaction, TargetNodeIndex, Frame);

                if (!math.all(math.isfinite(transaction.OriginalAUP)))
                {
                    faultFlags |= HabitatDeconstructionTransactionKernel.FaultInvalidAup | HabitatDeconstructionTransactionKernel.FaultNaN;
                    continue;
                }

                aupMagnitude = math.length(transaction.OriginalAUP);
                if (!CanReadCsrEdges(TargetNodeIndex))
                    faultFlags |= HabitatDeconstructionTransactionKernel.FaultNoGraph;
                totalEdgesSevered += SeverCsrEdges(TargetNodeIndex);

                if (!TryGetModuleCost(targetHash, out ModuleCostDTO cost))
                {
                    faultFlags |= HabitatDeconstructionTransactionKernel.FaultMissingCost;
                    continue;
                }

                for (int pairIndex = 0; pairIndex < HabitatDeconstructionTransactionKernel.MaxCostPairs; pairIndex++)
                {
                    ReadCostPair(in cost, pairIndex, out uint itemHash, out int originalQuantity);
                    if (itemHash == 0u || originalQuantity <= 0)
                        continue;

                    int refundQuantity = originalQuantity >> 1;
                    if (refundQuantity <= 0)
                        continue;

                    if (refundWrite >= (RefundCommands.IsCreated ? RefundCommands.Length : 0))
                    {
                        faultFlags |= HabitatDeconstructionTransactionKernel.FaultRefundOverflow;
                        WriteLootCache(
                            ref lootWrite,
                            in transaction,
                            itemHash,
                            refundQuantity,
                            pairIndex,
                            ref stateHash,
                            HabitatDeconstructionTransactionKernel.FaultRefundOverflow);
                        continue;
                    }

                    uint sequence = SequenceBase + (uint)refundWrite;
                    stateHash = HabitatDeconstructionTransactionKernel.HashRefund(itemHash, refundQuantity, stateHash);
                    RefundCommands[refundWrite] = new RefundCommandDTO
                    {
                        ItemHash = itemHash,
                        Quantity = refundQuantity,
                        TargetModuleHash = targetHash,
                        Sequence = sequence,
                        Status = HabitatDeconstructionTransactionKernel.RefundStatusPendingInventory,
                        PairIndex = (byte)pairIndex,
                        Reserved0 = 0,
                        StateHash = stateHash
                    };
                    refundWrite++;
                    totalRefunded += refundQuantity;
                }
            }

            if (RefundCommandCount.IsCreated && RefundCommandCount.Length > 0)
                RefundCommandCount[0] = refundWrite;
            if (LootCacheCount.IsCreated && LootCacheCount.Length > 0)
                LootCacheCount[0] = lootWrite;

            WriteTelemetry(
                targetHash,
                initiatorHash,
                stateHash,
                maxTransactions,
                totalRefunded,
                lootWrite,
                totalEdgesSevered,
                faultFlags,
                aupMagnitude);
        }

        private int SeverCsrEdges(int nodeIndex)
        {
            if (!CanReadCsrEdges(nodeIndex))
            {
                return 0;
            }

            int safeEdgeCount = math.min(math.max(0, EdgeCount), math.min(EdgeDestinations.Length, EdgeStrength.Length));
            if (safeEdgeCount <= 0)
                return 0;

            int severed = 0;
            int edgeStart = math.clamp(EdgeOffsets[nodeIndex], 0, safeEdgeCount);
            int edgeEnd = math.clamp(EdgeOffsets[nodeIndex + 1], edgeStart, safeEdgeCount);
            for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                severed += ZeroEdge(edgeIndex);

            for (int edgeIndex = 0; edgeIndex < safeEdgeCount; edgeIndex++)
            {
                if (EdgeDestinations[edgeIndex] == nodeIndex)
                    severed += ZeroEdge(edgeIndex);
            }

            return severed;
        }

        private bool CanReadCsrEdges(int nodeIndex)
        {
            return EdgeOffsets.IsCreated &&
                   EdgeDestinations.IsCreated &&
                   EdgeStrength.IsCreated &&
                   nodeIndex >= 0 &&
                   NodeCount > 0 &&
                   nodeIndex < NodeCount &&
                   nodeIndex + 1 < EdgeOffsets.Length;
        }

        private int ZeroEdge(int edgeIndex)
        {
            if ((uint)edgeIndex >= (uint)EdgeStrength.Length)
                return 0;

            float previous = EdgeStrength[edgeIndex];
            EdgeStrength[edgeIndex] = 0f;
            if (EdgeFlags.IsCreated && edgeIndex < EdgeFlags.Length)
                EdgeFlags[edgeIndex] = (byte)(EdgeFlags[edgeIndex] | 2);

            return previous != 0f ? 1 : 0;
        }

        private bool TryGetModuleCost(uint prefabHash, out ModuleCostDTO cost)
        {
            cost = default;
            if (!ModuleCosts.IsCreated || prefabHash == 0u)
                return false;

            int count = math.min(math.max(0, ModuleCostCount), ModuleCosts.Length);
            int lo = 0;
            int hi = count - 1;
            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                ModuleCostDTO candidate = ModuleCosts[mid];
                if (candidate.PrefabHashID == prefabHash)
                {
                    cost = candidate;
                    return true;
                }

                if (candidate.PrefabHashID < prefabHash)
                    lo = mid + 1;
                else
                    hi = mid - 1;
            }

            return false;
        }

        private static void ReadCostPair(in ModuleCostDTO cost, int index, out uint itemHash, out int quantity)
        {
            switch (index)
            {
                case 0:
                    itemHash = cost.ItemHash0;
                    quantity = cost.Quantity0;
                    break;
                case 1:
                    itemHash = cost.ItemHash1;
                    quantity = cost.Quantity1;
                    break;
                case 2:
                    itemHash = cost.ItemHash2;
                    quantity = cost.Quantity2;
                    break;
                default:
                    itemHash = cost.ItemHash3;
                    quantity = cost.Quantity3;
                    break;
            }
        }

        private void WriteLootCache(
            ref int lootWrite,
            in DeconstructionTransactionDTO transaction,
            uint itemHash,
            int quantity,
            int pairIndex,
            ref uint stateHash,
            uint flags)
        {
            if (!LootCaches.IsCreated || lootWrite >= LootCaches.Length)
                return;

            uint sequence = SequenceBase + (uint)(1024 + lootWrite);
            float offsetMeters = math.lerp(0.35f, 0.95f, math.saturate(GlobalQualityWeight));
            float3 offset = ResolveDeterministicOffset(sequence, pairIndex, offsetMeters);
            LootCacheDTO cache = new LootCacheDTO
            {
                PositionAup = transaction.OriginalAUP + new double3(offset.x, offset.y, offset.z),
                LocalOffset = offset,
                ItemHash = itemHash,
                Quantity = quantity,
                SourceModuleHash = transaction.TargetModuleHash,
                Sequence = sequence,
                Flags = flags
            };
            stateHash = HabitatDeconstructionTransactionKernel.HashCache(in cache, stateHash);
            LootCaches[lootWrite++] = cache;
        }

        private static float3 ResolveDeterministicOffset(uint sequence, int pairIndex, float radius)
        {
            uint hash = sequence ^ ((uint)pairIndex * 0x9E3779B9u);
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            float angle = (hash & 1023u) * (6.28318530718f / 1024f);
            float y = 0.35f + ((hash >> 10) & 63u) * (0.3f / 63f);
            Hecton8.Core.MathLodApproximation.ApproxSinCosBhaskara(angle, out float sin, out float cos);
            return new float3(cos * radius, y, sin * radius);
        }

        private void WriteTelemetry(
            uint targetModuleHash,
            uint initiatorHash,
            uint stateHash,
            int modulesProcessed,
            int resourcesRefunded,
            int overflowLootCaches,
            int edgesSevered,
            uint faultFlags,
            double aupMagnitude)
        {
            if (!TelemetryRing.IsCreated || !TelemetryCursor.IsCreated || TelemetryRing.Length == 0 || TelemetryCursor.Length == 0)
                return;

            int cursor = TelemetryCursor[0];
            if (cursor < 0)
                cursor = 0;

            int slot = cursor % TelemetryRing.Length;
            TelemetryRing[slot] = new TeardownTelemetryEntry
            {
                Frame = Frame,
                TargetModuleHash = targetModuleHash,
                InitiatorEntityHash = initiatorHash,
                StateHash = stateHash,
                ModulesProcessed = modulesProcessed,
                ResourcesRefunded = resourcesRefunded,
                OverflowLootCaches = overflowLootCaches,
                EdgesSevered = edgesSevered,
                BurstMicroseconds = 0f,
                GlobalQualityWeight = math.saturate(GlobalQualityWeight),
                FaultFlags = faultFlags,
                TargetNodeIndex = TargetNodeIndex,
                AupLocalMagnitude = aupMagnitude
            };
            TelemetryCursor[0] = cursor == int.MaxValue ? 0 : cursor + 1;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockDeconstructionDataJob : IJob
    {
        [NoAlias] public NativeArray<DeconstructionTransactionDTO> Transactions;
        [NoAlias] public NativeArray<ModuleCostDTO> Costs;
        public uint ModuleHash;
        public uint InitiatorHash;
        public double3 Aup;
        public uint ItemHashA;
        public uint ItemHashB;

        public void Execute()
        {
            if (Transactions.IsCreated && Transactions.Length > 0)
            {
                Transactions[0] = new DeconstructionTransactionDTO
                {
                    TargetModuleHash = ModuleHash != 0u ? ModuleHash : 0x53333336u,
                    InitiatorEntityHash = InitiatorHash,
                    OriginalAUP = Aup
                };
            }

            if (Costs.IsCreated && Costs.Length > 0)
            {
                Costs[0] = new ModuleCostDTO
                {
                    PrefabHashID = ModuleHash != 0u ? ModuleHash : 0x53333336u,
                    CostCount = 2u,
                    ItemHash0 = ItemHashA != 0u ? ItemHashA : 0x5449544Eu,
                    Quantity0 = 4,
                    ItemHash1 = ItemHashB != 0u ? ItemHashB : 0x43555052u,
                    Quantity1 = 2
                };
            }
        }
    }
}
