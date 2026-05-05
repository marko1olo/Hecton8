using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Construction
{
    internal struct AutonomousExtractorJobInput
    {
        public float CycleTimerSeconds;
        public float CycleSeconds;
        public int BufferedUnitCount;
        public int BufferedUnitCapacity;
        public int ItemHashId;
        public byte IsActive;
    }

    internal struct AutonomousExtractorJobResult
    {
        public float NextCycleTimerSeconds;
        public int NextBufferedUnitCount;
        public int BufferedItemHashId;
        public int CompletedCycleDelta;
        public byte IsOperating;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct AutonomousExtractorAdvanceJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<AutonomousExtractorJobInput> Inputs;
        public NativeArray<AutonomousExtractorJobResult> Results;
        public float SlowTickDeltaSeconds;

        public void Execute(int index)
        {
            AutonomousExtractorJobInput input = Inputs[index];
            AutonomousExtractorJobResult result = new AutonomousExtractorJobResult
            {
                NextCycleTimerSeconds = math.max(0f, input.CycleTimerSeconds),
                NextBufferedUnitCount = math.max(0, input.BufferedUnitCount),
                BufferedItemHashId = input.ItemHashId,
                CompletedCycleDelta = 0,
                IsOperating = 0
            };

            bool canOperate = input.IsActive != 0 &&
                              input.ItemHashId != 0 &&
                              input.BufferedUnitCapacity > 0 &&
                              input.BufferedUnitCount < input.BufferedUnitCapacity &&
                              input.CycleSeconds > 0f;
            if (!canOperate)
            {
                Results[index] = result;
                return;
            }

            result.IsOperating = 1;
            float accumulatedTime = input.CycleTimerSeconds + math.max(0f, SlowTickDeltaSeconds);
            float cycleSeconds = math.max(0.001f, input.CycleSeconds);
            int completedCycles = (int)math.floor(accumulatedTime / cycleSeconds);
            int availableCapacity = math.max(0, input.BufferedUnitCapacity - input.BufferedUnitCount);
            int producedUnits = math.min(math.max(0, completedCycles), availableCapacity);

            result.NextBufferedUnitCount = input.BufferedUnitCount + producedUnits;
            result.CompletedCycleDelta = producedUnits;
            result.NextCycleTimerSeconds = accumulatedTime - (producedUnits * cycleSeconds);

            if (completedCycles > producedUnits)
                result.NextCycleTimerSeconds = cycleSeconds;

            if (result.NextBufferedUnitCount >= input.BufferedUnitCapacity)
                result.IsOperating = 0;

            Results[index] = result;
        }
    }
}
