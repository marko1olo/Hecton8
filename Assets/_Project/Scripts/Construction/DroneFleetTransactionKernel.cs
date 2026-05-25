using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Construction
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct DroneTransactionIntegrityDTO
    {
        [FieldOffset(0)] public uint TargetEntityHash;
        [FieldOffset(4)] public int CurrentIntegrityMilli;
        [FieldOffset(8)] public int MaxRecoverableIntegrityMilli;
        [FieldOffset(12)] public int RepairBudgetMilli;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public int CommandIndex;
        [FieldOffset(24)] public int Slot;
        [FieldOffset(28)] private int _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct DroneTransactionResultDTO
    {
        public const uint FlagRepairApplied = 1u << 0;
        public const uint FlagInventoryAdded = 1u << 1;
        public const uint FlagCompleted = 1u << 2;
        public const uint FlagInvalidInput = 1u << 3;
        public const uint FlagAtomicConflict = 1u << 4;
        public const uint FlagVfxSpark = 1u << 5;
        public const uint FlagNoop = 1u << 6;
        public const uint FlagNaNFault = 1u << 31;

        [FieldOffset(0)] public int Slot;
        [FieldOffset(4)] public int DroneId;
        [FieldOffset(8)] public uint TargetEntityHash;
        [FieldOffset(12)] public uint TaskTypeHash;
        [FieldOffset(16)] public int PreviousIntegrityMilli;
        [FieldOffset(20)] public int NextIntegrityMilli;
        [FieldOffset(24)] public int RepairAppliedMilli;
        [FieldOffset(28)] public int InventorySlot;
        [FieldOffset(32)] public uint InventoryHash;
        [FieldOffset(36)] public int InventoryQuantityAdded;
        [FieldOffset(40)] public float Progress01;
        [FieldOffset(44)] public float VfxIntensity01;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint ActiveInventorySlots;
        [FieldOffset(56)] public uint AtomicConflicts;
        [FieldOffset(60)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct DroneTransactionTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint StateHash;
        [FieldOffset(8)] public int TransactionCount;
        [FieldOffset(12)] public int RepairCount;
        [FieldOffset(16)] public int MiningCount;
        [FieldOffset(20)] public int InventoryAdds;
        [FieldOffset(24)] public int AtomicConflicts;
        [FieldOffset(28)] public int VfxSignals;
        [FieldOffset(32)] public float GlobalQualityWeight;
        [FieldOffset(36)] public float EstimatedMicroseconds;
        [FieldOffset(40)] public uint FaultFlags;
        [FieldOffset(44)] public uint LastTargetHash;
        [FieldOffset(48)] public int ActiveInventorySlots;
        [FieldOffset(52)] public int CommandCount;
        [FieldOffset(56)] public uint LayoutHash;
        [FieldOffset(60)] private uint _pad0;
    }

    internal enum DroneTransactionCounterSlot : int
    {
        ActiveTasks = 0,
        RepairCount = 1,
        MiningCount = 2,
        InventoryAdds = 3,
        AtomicConflicts = 4,
        VfxSignals = 5,
        Faults = 6,
        Count = 7
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct DroneTransactionCommandDTO
    {
        public const uint FlagValid = 1u << 0;

        [FieldOffset(0)] public int Slot;
        [FieldOffset(4)] public int DroneId;
        [FieldOffset(8)] public int CommandIndex;
        [FieldOffset(12)] public float DeltaTime;
        [FieldOffset(16)] public uint TaskTypeHash;
        [FieldOffset(20)] public uint TargetEntityHash;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Frame;
        [FieldOffset(32)] public float3 Position;
        [FieldOffset(44)] public float3 TargetPosition;
        [FieldOffset(56)] public uint StateHash;
        [FieldOffset(60)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct DroneTransactionAupSnapshotDTO
    {
        public const uint FlagValid = 1u << 0;

        [FieldOffset(0)] public double3 CurrentAUP;
        [FieldOffset(24)] public double3 TargetAUP;
        [FieldOffset(48)] public float Radius;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint TargetEntityHash;
        [FieldOffset(60)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct DroneTransactionCounterDTO
    {
        [FieldOffset(0)] public int Value;
        [FieldOffset(4)] private int _pad0;
        [FieldOffset(8)] private ulong _pad1;
        [FieldOffset(16)] private ulong _pad2;
        [FieldOffset(24)] private ulong _pad3;
        [FieldOffset(32)] private ulong _pad4;
        [FieldOffset(40)] private ulong _pad5;
        [FieldOffset(48)] private ulong _pad6;
        [FieldOffset(56)] private ulong _pad7;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateMockDroneTransactionsJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<DroneTaskDTO> Tasks;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<DroneTransactionIntegrityDTO> Integrity;
        public int RequestedCount;
        public uint RepairTaskHash;
        public uint MiningTaskHash;
        public uint InventoryPayloadHash;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)RequestedCount || (uint)index >= (uint)Tasks.Length)
                return;

            bool repair = (index & 1) == 0;
            float quality = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 0f);
            Tasks[index] = new DroneTaskDTO
            {
                TargetEntityHash = HashIndex(index),
                TaskTypeHash = repair ? RepairTaskHash : MiningTaskHash,
                TaskProgress01 = repair ? 0f : math.saturate((index & 3) * 0.2f),
                TaskEfficiencyScalar = repair ? math.lerp(0.002f, 0.012f, quality) : math.lerp(0.25f, 1f, quality),
                InventoryPayloadHash = repair ? 0u : InventoryPayloadHash
            };

            if (Integrity.IsCreated && (uint)index < (uint)Integrity.Length)
            {
                Integrity[index] = new DroneTransactionIntegrityDTO
                {
                    TargetEntityHash = HashIndex(index),
                    CurrentIntegrityMilli = repair ? 550 : 1000,
                    MaxRecoverableIntegrityMilli = 1000,
                    RepairBudgetMilli = repair ? math.max(1, (int)math.round(math.lerp(2f, 12f, quality))) : 0,
                    Flags = 1u,
                    CommandIndex = index,
                    Slot = index
                };
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HashIndex(int index)
        {
            uint value = (uint)index + 0x9E3779B9u;
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value == 0u ? 1u : value;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct EvaluateDroneTransactionsJob : IJobParallelFor
    {
        private const int InventoryQuantityPerMiningCompletion = 1;
        private const int AtomicRetryLimit = 16;

        [ReadOnly, NoAlias] public NativeArray<DroneTransactionCommandDTO> Commands;
        [ReadOnly, NoAlias] public NativeArray<DroneTransactionAupSnapshotDTO> AupSnapshots;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<DroneTaskDTO> Tasks;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<DroneTransactionIntegrityDTO> Integrity;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<DroneTransactionResultDTO> Results;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<DroneTransactionCounterDTO> Counters;

        public int TransactionCount;
        public uint RepairTaskHash;
        public uint MiningTaskHash;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)TransactionCount ||
                (uint)index >= (uint)Tasks.Length ||
                (uint)index >= (uint)Results.Length)
            {
                return;
            }

            DroneTaskDTO task = Tasks[index];
            DroneTransactionResultDTO result = default;
            result.TaskTypeHash = task.TaskTypeHash;
            result.TargetEntityHash = task.TargetEntityHash;
            result.InventorySlot = -1;
            result.Progress01 = math.saturate(math.isfinite(task.TaskProgress01) ? task.TaskProgress01 : 0f);

            DroneTransactionCommandDTO command = default;
            if (Commands.IsCreated && (uint)index < (uint)Commands.Length)
                command = Commands[index];

            result.Slot = command.Slot;
            result.DroneId = command.DroneId;

            if (task.TaskTypeHash == 0u || (command.Flags & DroneTransactionCommandDTO.FlagValid) == 0u)
            {
                Results[index] = result;
                return;
            }

            AddCounter(DroneTransactionCounterSlot.ActiveTasks, 1);
            if (!IsDroneAtTarget(index, result.TargetEntityHash))
            {
                result.Flags |= DroneTransactionResultDTO.FlagNoop;
                Results[index] = result;
                return;
            }

            if (!math.isfinite(task.TaskProgress01) || !math.isfinite(task.TaskEfficiencyScalar))
            {
                result.Flags |= DroneTransactionResultDTO.FlagNaNFault | DroneTransactionResultDTO.FlagInvalidInput;
                AddCounter(DroneTransactionCounterSlot.Faults, 1);
                Results[index] = result;
                return;
            }

            if (task.TaskTypeHash == RepairTaskHash)
            {
                uint repairVfxSeed = command.StateHash ^
                                     command.TargetEntityHash ^
                                     command.TaskTypeHash ^
                                     unchecked((uint)math.max(0, command.DroneId));
                EvaluateRepair(index, repairVfxSeed, ref result);
            }
            else if (task.TaskTypeHash == MiningTaskHash)
            {
                EvaluateMining(index, command.DeltaTime, ref task, ref result);
                Tasks[index] = task;
            }
            else
            {
                result.Flags |= DroneTransactionResultDTO.FlagInvalidInput;
                AddCounter(DroneTransactionCounterSlot.Faults, 1);
            }

            Results[index] = result;
        }

        private void EvaluateRepair(int index, uint repairVfxSeed, ref DroneTransactionResultDTO result)
        {
            if (!Integrity.IsCreated || (uint)index >= (uint)Integrity.Length)
            {
                result.Flags |= DroneTransactionResultDTO.FlagInvalidInput;
                AddCounter(DroneTransactionCounterSlot.Faults, 1);
                return;
            }

            DroneTransactionIntegrityDTO* integrityPtr = (DroneTransactionIntegrityDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Integrity);
            ref DroneTransactionIntegrityDTO integrity = ref UnsafeUtility.AsRef<DroneTransactionIntegrityDTO>(integrityPtr + index);
            ref int currentRef = ref integrity.CurrentIntegrityMilli;
            int cap = math.max(0, integrity.MaxRecoverableIntegrityMilli);
            int budget = math.max(0, integrity.RepairBudgetMilli);
            if (cap <= 0 || integrity.TargetEntityHash == 0u)
            {
                result.Flags |= DroneTransactionResultDTO.FlagInvalidInput;
                AddCounter(DroneTransactionCounterSlot.Faults, 1);
                return;
            }

            if (budget <= 0)
            {
                int observed = math.clamp(Interlocked.CompareExchange(ref currentRef, 0, 0), 0, cap);
                result.PreviousIntegrityMilli = observed;
                result.NextIntegrityMilli = observed;
                result.Progress01 = cap > 0 ? math.saturate(observed * math.rcp(cap)) : 1f;
                if (observed >= cap)
                {
                    result.Flags |= DroneTransactionResultDTO.FlagNoop | DroneTransactionResultDTO.FlagCompleted;
                }
                else
                {
                    result.Flags |= DroneTransactionResultDTO.FlagInvalidInput;
                    AddCounter(DroneTransactionCounterSlot.Faults, 1);
                }

                return;
            }

            for (int attempt = 0; attempt < AtomicRetryLimit; attempt++)
            {
                int observed = math.clamp(Interlocked.CompareExchange(ref currentRef, 0, 0), 0, cap);
                if (observed >= cap)
                {
                    result.PreviousIntegrityMilli = observed;
                    result.NextIntegrityMilli = observed;
                    result.Flags |= DroneTransactionResultDTO.FlagNoop | DroneTransactionResultDTO.FlagCompleted;
                    return;
                }

                int next = math.min(cap, observed + budget);
                if (Interlocked.CompareExchange(ref currentRef, next, observed) != observed)
                    continue;

                int repaired = next - observed;
                result.PreviousIntegrityMilli = observed;
                result.NextIntegrityMilli = next;
                result.RepairAppliedMilli = repaired;
                result.Progress01 = cap > 0 ? math.saturate(next * math.rcp(cap)) : 1f;
                result.VfxIntensity01 = ResolveSparkIntensity(repaired, cap, repairVfxSeed);
                result.Flags |= DroneTransactionResultDTO.FlagRepairApplied;
                if (result.VfxIntensity01 > 0f)
                    result.Flags |= DroneTransactionResultDTO.FlagVfxSpark;
                if (next >= cap)
                    result.Flags |= DroneTransactionResultDTO.FlagCompleted;

                AddCounter(DroneTransactionCounterSlot.RepairCount, 1);
                if ((result.Flags & DroneTransactionResultDTO.FlagVfxSpark) != 0u)
                    AddCounter(DroneTransactionCounterSlot.VfxSignals, 1);
                return;
            }

            result.Flags |= DroneTransactionResultDTO.FlagAtomicConflict;
            result.AtomicConflicts = 1u;
            AddCounter(DroneTransactionCounterSlot.AtomicConflicts, 1);
        }

        private void EvaluateMining(int index, float deltaTime, ref DroneTaskDTO task, ref DroneTransactionResultDTO result)
        {
            float dt = math.max(0f, math.isfinite(deltaTime) ? deltaTime : 0f);
            float progress = math.saturate(task.TaskProgress01 + dt * math.max(0f, task.TaskEfficiencyScalar));
            task.TaskProgress01 = progress;
            result.Progress01 = progress;
            AddCounter(DroneTransactionCounterSlot.MiningCount, 1);

            if (progress < 1f)
                return;

            if (task.InventoryPayloadHash == 0u)
            {
                result.Flags |= DroneTransactionResultDTO.FlagInvalidInput;
                AddCounter(DroneTransactionCounterSlot.Faults, 1);
                return;
            }

            task.TaskProgress01 = 0f;
            result.InventorySlot = -1;
            result.InventoryHash = task.InventoryPayloadHash;
            result.InventoryQuantityAdded = InventoryQuantityPerMiningCompletion;
            result.ActiveInventorySlots = 0u;
            result.Flags |= DroneTransactionResultDTO.FlagInventoryAdded | DroneTransactionResultDTO.FlagCompleted;
            AddCounter(DroneTransactionCounterSlot.InventoryAdds, 1);
        }

        private float ResolveSparkIntensity(int repairedMilli, int capMilli, uint repairVfxSeed)
        {
            float quality = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 0f);
            float repair01 = capMilli > 0 ? math.saturate(repairedMilli * math.rcp(capMilli)) : 0f;
            float cadence = math.lerp(0.08f, 1f, quality * quality);
            uint hash = Hash(repairVfxSeed ^ ResultSeed(repairedMilli, capMilli));
            float sample = (hash & 0x00FFFFFFu) * (1f / 16777215f);
            return sample <= cadence ? math.saturate(0.2f + repair01 * 8f + quality * 0.5f) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ResultSeed(int a, int b)
        {
            return unchecked((uint)a * 0x9E3779B9u ^ (uint)b * 0x85EBCA6Bu);
        }

        private bool IsDroneAtTarget(int index, uint expectedTargetHash)
        {
            if (!AupSnapshots.IsCreated || (uint)index >= (uint)AupSnapshots.Length)
                return false;

            DroneTransactionAupSnapshotDTO snapshot = AupSnapshots[index];
            if ((snapshot.Flags & DroneTransactionAupSnapshotDTO.FlagValid) == 0u ||
                snapshot.TargetEntityHash != expectedTargetHash)
            {
                return false;
            }

            double3 delta = snapshot.TargetAUP - snapshot.CurrentAUP;
            if (!math.all(math.isfinite(delta)))
                return false;

            float3 localDelta = new float3((float)delta.x, (float)delta.y, (float)delta.z);
            float radius = math.max(0.1f, snapshot.Radius);
            return math.lengthsq(localDelta) <= radius * radius;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }

        private void AddCounter(DroneTransactionCounterSlot slot, int amount)
        {
            if (!Counters.IsCreated || (uint)slot >= (uint)Counters.Length || amount == 0)
                return;

            DroneTransactionCounterDTO* ptr = (DroneTransactionCounterDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Counters);
            ref DroneTransactionCounterDTO counter = ref UnsafeUtility.AsRef<DroneTransactionCounterDTO>(ptr + (int)slot);
            Interlocked.Add(ref counter.Value, amount);
        }
    }
}
