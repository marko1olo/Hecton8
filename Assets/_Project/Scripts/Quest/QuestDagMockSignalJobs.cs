using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Quest
{
    /// <summary>
    /// Blind-dependency mock producer for position, inventory, and story events.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct MockQuestSignalPushJob : IJob
    {
        public NativeQueue<MockPlayerPositionSignal>.ParallelWriter PlayerPositionWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> PlayerPositionWriterBudget;
        public NativeQueue<QuestDagMockItemAcquiredSignal>.ParallelWriter ItemAcquiredWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> ItemAcquiredWriterBudget;
        public NativeQueue<MockStoryEventSignal>.ParallelWriter StoryEventWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> StoryEventWriterBudget;
        public uint Frame;
        public uint Seed;
        public ulong Timestamp;

        public void Execute()
        {
            uint state = Seed != 0u ? Seed : 0xA341316Cu;
            state = Next(state);
            double x = (state & 0xFFFFu) * 0.01d;
            state = Next(state);
            double z = (state & 0xFFFFu) * 0.01d;

            SignalBus<MockPlayerPositionSignal>.TryEnqueueBounded(PlayerPositionWriter, PlayerPositionWriterBudget, new MockPlayerPositionSignal
            {
                AUP = new double3(x, 0d, z),
                Frame = Frame,
                Seed = state,
                Flags = 0u,
                _pad0 = 0u
            });

            state = Next(state);
            SignalBus<QuestDagMockItemAcquiredSignal>.TryEnqueueBounded(ItemAcquiredWriter, ItemAcquiredWriterBudget, new QuestDagMockItemAcquiredSignal
            {
                Timestamp = Timestamp,
                ItemHash = unchecked(0x49000000u + (state & 31u)),
                Quantity = 1 + (int)(state & 3u),
                Frame = Frame,
                Flags = 0u,
                _pad0 = 0u,
                _pad1 = 0u
            });

            state = Next(state);
            SignalBus<MockStoryEventSignal>.TryEnqueueBounded(StoryEventWriter, StoryEventWriterBudget, new MockStoryEventSignal
            {
                Timestamp = Timestamp,
                EventHash = unchecked(0x54000000u + (state & 1023u)),
                NodeHash = unchecked(0x51000000u + (state & 63u)),
                Frame = Frame,
                Flags = 0u,
                _pad0 = 0u,
                _pad1 = 0u
            });
        }

        private static uint Next(uint value)
        {
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            return value;
        }
    }

    /// <summary>
    /// Cold helper for creating mock signal queues without relying on upstream systems.
    /// </summary>
    public static class QuestDagMockSignals
    {
        public static MockQuestSignalPushJob CreateJob(uint frame, uint seed, ulong timestamp)
        {
            SignalBus<MockPlayerPositionSignal>.EnsureInitialized();
            SignalBus<QuestDagMockItemAcquiredSignal>.EnsureInitialized();
            SignalBus<MockStoryEventSignal>.EnsureInitialized();
            return new MockQuestSignalPushJob
            {
                PlayerPositionWriter = SignalBus<MockPlayerPositionSignal>.ParallelWriter,
                PlayerPositionWriterBudget = SignalBus<MockPlayerPositionSignal>.ParallelWriterBudget,
                ItemAcquiredWriter = SignalBus<QuestDagMockItemAcquiredSignal>.ParallelWriter,
                ItemAcquiredWriterBudget = SignalBus<QuestDagMockItemAcquiredSignal>.ParallelWriterBudget,
                StoryEventWriter = SignalBus<MockStoryEventSignal>.ParallelWriter,
                StoryEventWriterBudget = SignalBus<MockStoryEventSignal>.ParallelWriterBudget,
                Frame = frame,
                Seed = seed,
                Timestamp = timestamp
            };
        }
    }
}
