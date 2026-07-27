using NUnit.Framework;
using Unity.Collections;

namespace Hecton8.Construction.Tests
{
    /// <summary>
    /// Locks the slot-row contract of <see cref="ExtractorSlotLanes"/>, which
    /// <see cref="AutonomousExtractorSystem"/> uses to keep accumulated extraction state attached to the
    /// module that earned it.
    ///
    /// Regression guarded: the extractor lanes (cycle timer, buffered item hash, buffered unit count,
    /// completed cycle count) are keyed by registry slot index, but the registry compaction pass moved a
    /// module to a lower slot and only updated the managed reference plus <c>SetRuntimeIndex</c>. The row
    /// stayed behind. A player who removed one extractor silently destroyed a second extractor's buffered
    /// yield - the surviving module began reading the freshly zeroed row of the slot it inherited - and the
    /// next extractor placed into the recycled tail slot adopted the abandoned tally and deposited units
    /// that were never mined into base storage.
    /// </summary>
    [TestFixture]
    public sealed class ExtractorSlotLanesTests
    {
        private const int RowCapacity = 4;

        private NativeArray<float> _cycleTimers;
        private NativeArray<int> _bufferedItemHashIds;
        private NativeArray<int> _bufferedUnitCounts;
        private NativeArray<int> _completedCycleCounts;

        [SetUp]
        public void SetUp()
        {
            _cycleTimers = new NativeArray<float>(RowCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _bufferedItemHashIds = new NativeArray<int>(RowCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _bufferedUnitCounts = new NativeArray<int>(RowCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _completedCycleCounts = new NativeArray<int>(RowCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        [TearDown]
        public void TearDown()
        {
            DisposeLane(ref _cycleTimers);
            DisposeIntLane(ref _bufferedItemHashIds);
            DisposeIntLane(ref _bufferedUnitCounts);
            DisposeIntLane(ref _completedCycleCounts);
        }

        [Test]
        public void MoveRow_CarriesBufferedYieldToTheNewSlot()
        {
            WriteRow(index: 3, cycleTimerSeconds: 4.25f, itemHashId: 0x5EED1234, bufferedUnits: 7, completedCycles: 19);

            bool moved = ExtractorSlotLanes.TryMoveRow(
                _cycleTimers,
                _bufferedItemHashIds,
                _bufferedUnitCounts,
                _completedCycleCounts,
                sourceIndex: 3,
                destinationIndex: 0);

            Assert.That(moved, Is.True);
            Assert.That(_cycleTimers[0], Is.EqualTo(4.25f));
            Assert.That(_bufferedItemHashIds[0], Is.EqualTo(0x5EED1234));
            Assert.That(_bufferedUnitCounts[0], Is.EqualTo(7));
            Assert.That(_completedCycleCounts[0], Is.EqualTo(19));
        }

        [Test]
        public void MoveRow_ZeroesTheVacatedSlotSoNothingInheritsIt()
        {
            WriteRow(index: 2, cycleTimerSeconds: 1.5f, itemHashId: 99, bufferedUnits: 5, completedCycles: 11);

            ExtractorSlotLanes.TryMoveRow(
                _cycleTimers,
                _bufferedItemHashIds,
                _bufferedUnitCounts,
                _completedCycleCounts,
                sourceIndex: 2,
                destinationIndex: 1);

            Assert.That(_cycleTimers[2], Is.EqualTo(0f));
            Assert.That(_bufferedItemHashIds[2], Is.EqualTo(0));
            Assert.That(_bufferedUnitCounts[2], Is.EqualTo(0));
            Assert.That(_completedCycleCounts[2], Is.EqualTo(0));
        }

        [Test]
        public void MoveRow_OverwritesTheDestinationRatherThanAccumulating()
        {
            WriteRow(index: 0, cycleTimerSeconds: 9f, itemHashId: 111, bufferedUnits: 40, completedCycles: 400);
            WriteRow(index: 1, cycleTimerSeconds: 2f, itemHashId: 222, bufferedUnits: 3, completedCycles: 6);

            ExtractorSlotLanes.TryMoveRow(
                _cycleTimers,
                _bufferedItemHashIds,
                _bufferedUnitCounts,
                _completedCycleCounts,
                sourceIndex: 1,
                destinationIndex: 0);

            Assert.That(_cycleTimers[0], Is.EqualTo(2f));
            Assert.That(_bufferedItemHashIds[0], Is.EqualTo(222));
            Assert.That(_bufferedUnitCounts[0], Is.EqualTo(3));
            Assert.That(_completedCycleCounts[0], Is.EqualTo(6));
        }

        [Test]
        public void MoveRow_SanitizesCorruptedAccumulatorsWhileCarryingThem()
        {
            WriteRow(index: 1, cycleTimerSeconds: float.NaN, itemHashId: 77, bufferedUnits: -4, completedCycles: -1);

            ExtractorSlotLanes.TryMoveRow(
                _cycleTimers,
                _bufferedItemHashIds,
                _bufferedUnitCounts,
                _completedCycleCounts,
                sourceIndex: 1,
                destinationIndex: 0);

            Assert.That(_cycleTimers[0], Is.EqualTo(0f));
            Assert.That(_bufferedUnitCounts[0], Is.EqualTo(0));
            Assert.That(_completedCycleCounts[0], Is.EqualTo(0));
            Assert.That(_bufferedItemHashIds[0], Is.EqualTo(77));
        }

        [Test]
        public void MoveRow_RejectsOutOfRangeSlotsAndLeavesLanesIntact()
        {
            WriteRow(index: 0, cycleTimerSeconds: 3f, itemHashId: 5, bufferedUnits: 2, completedCycles: 8);

            Assert.That(
                ExtractorSlotLanes.TryMoveRow(
                    _cycleTimers,
                    _bufferedItemHashIds,
                    _bufferedUnitCounts,
                    _completedCycleCounts,
                    sourceIndex: 0,
                    destinationIndex: RowCapacity),
                Is.False);
            Assert.That(
                ExtractorSlotLanes.TryMoveRow(
                    _cycleTimers,
                    _bufferedItemHashIds,
                    _bufferedUnitCounts,
                    _completedCycleCounts,
                    sourceIndex: -1,
                    destinationIndex: 0),
                Is.False);

            Assert.That(_cycleTimers[0], Is.EqualTo(3f));
            Assert.That(_bufferedUnitCounts[0], Is.EqualTo(2));
            Assert.That(_completedCycleCounts[0], Is.EqualTo(8));
        }

        [Test]
        public void MoveRow_ToItselfIsANoOpAndKeepsTheRow()
        {
            WriteRow(index: 2, cycleTimerSeconds: 6f, itemHashId: 42, bufferedUnits: 9, completedCycles: 13);

            Assert.That(
                ExtractorSlotLanes.TryMoveRow(
                    _cycleTimers,
                    _bufferedItemHashIds,
                    _bufferedUnitCounts,
                    _completedCycleCounts,
                    sourceIndex: 2,
                    destinationIndex: 2),
                Is.True);

            Assert.That(_cycleTimers[2], Is.EqualTo(6f));
            Assert.That(_bufferedItemHashIds[2], Is.EqualTo(42));
            Assert.That(_bufferedUnitCounts[2], Is.EqualTo(9));
            Assert.That(_completedCycleCounts[2], Is.EqualTo(13));
        }

        [Test]
        public void ClearRow_ZeroesTheClaimedSlotSoNoPhantomYieldIsDeposited()
        {
            WriteRow(index: 1, cycleTimerSeconds: 8f, itemHashId: 321, bufferedUnits: 12, completedCycles: 30);

            Assert.That(
                ExtractorSlotLanes.TryClearRow(
                    _cycleTimers,
                    _bufferedItemHashIds,
                    _bufferedUnitCounts,
                    _completedCycleCounts,
                    index: 1),
                Is.True);

            Assert.That(_cycleTimers[1], Is.EqualTo(0f));
            Assert.That(_bufferedItemHashIds[1], Is.EqualTo(0));
            Assert.That(_bufferedUnitCounts[1], Is.EqualTo(0));
            Assert.That(_completedCycleCounts[1], Is.EqualTo(0));
        }

        [Test]
        public void ClearRow_RejectsOutOfRangeSlots()
        {
            Assert.That(
                ExtractorSlotLanes.TryClearRow(
                    _cycleTimers,
                    _bufferedItemHashIds,
                    _bufferedUnitCounts,
                    _completedCycleCounts,
                    index: RowCapacity),
                Is.False);
        }

        [Test]
        public void ResolveUsableRowCount_TakesTheShortestLaneAndRejectsMissingLanes()
        {
            Assert.That(
                ExtractorSlotLanes.ResolveUsableRowCount(
                    _cycleTimers,
                    _bufferedItemHashIds,
                    _bufferedUnitCounts,
                    _completedCycleCounts),
                Is.EqualTo(RowCapacity));

            var shortLane = new NativeArray<int>(2, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            try
            {
                Assert.That(
                    ExtractorSlotLanes.ResolveUsableRowCount(
                        _cycleTimers,
                        shortLane,
                        _bufferedUnitCounts,
                        _completedCycleCounts),
                    Is.EqualTo(2));
            }
            finally
            {
                shortLane.Dispose();
            }

            Assert.That(
                ExtractorSlotLanes.ResolveUsableRowCount(
                    _cycleTimers,
                    default,
                    _bufferedUnitCounts,
                    _completedCycleCounts),
                Is.EqualTo(0));
        }

        /// <summary>
        /// The sanitize rule is owned by <c>Hecton8.Core.MathGuard.SanitizeNonNegative</c> and
        /// <c>math.max</c>, not by a private copy inside the lanes, so it is locked here through the public
        /// row API that actually applies it. Each case is carried across a real slot move.
        /// </summary>
        [TestCase(float.NaN, 0f)]
        [TestCase(float.PositiveInfinity, 0f)]
        [TestCase(float.NegativeInfinity, 0f)]
        [TestCase(-0.5f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(2.5f, 2.5f)]
        public void MoveRow_ClampsTheCarriedCycleTimerToAFiniteNonNegativeSchedule(
            float storedSeconds,
            float expectedSeconds)
        {
            WriteRow(index: 1, cycleTimerSeconds: storedSeconds, itemHashId: 4242, bufferedUnits: 3, completedCycles: 5);

            Assert.That(
                ExtractorSlotLanes.TryMoveRow(
                    _cycleTimers,
                    _bufferedItemHashIds,
                    _bufferedUnitCounts,
                    _completedCycleCounts,
                    sourceIndex: 1,
                    destinationIndex: 0),
                Is.True);

            Assert.That(_cycleTimers[0], Is.EqualTo(expectedSeconds));
            Assert.That(_bufferedItemHashIds[0], Is.EqualTo(4242));
        }

        [TestCase(-1000, 0)]
        [TestCase(-1, 0)]
        [TestCase(0, 0)]
        [TestCase(6, 6)]
        public void MoveRow_ClampsCarriedUnitTalliesToNonNegativeCounts(int storedCount, int expectedCount)
        {
            WriteRow(
                index: 2,
                cycleTimerSeconds: 1.25f,
                itemHashId: 808,
                bufferedUnits: storedCount,
                completedCycles: storedCount);

            Assert.That(
                ExtractorSlotLanes.TryMoveRow(
                    _cycleTimers,
                    _bufferedItemHashIds,
                    _bufferedUnitCounts,
                    _completedCycleCounts,
                    sourceIndex: 2,
                    destinationIndex: 0),
                Is.True);

            Assert.That(_bufferedUnitCounts[0], Is.EqualTo(expectedCount));
            Assert.That(_completedCycleCounts[0], Is.EqualTo(expectedCount));
            Assert.That(_cycleTimers[0], Is.EqualTo(1.25f));
        }

        [Test]
        public void CompactionSequence_KeepsEachExtractorAttachedToItsOwnYield()
        {
            // Slot 0 = extractor A, slot 1 = extractor B mid-cycle with seven buffered units.
            WriteRow(index: 0, cycleTimerSeconds: 0.4f, itemHashId: 1001, bufferedUnits: 2, completedCycles: 4);
            WriteRow(index: 1, cycleTimerSeconds: 3.75f, itemHashId: 2002, bufferedUnits: 7, completedCycles: 21);

            // The player removes extractor A. UnregisterModule zeroes the vacated row 0.
            ExtractorSlotLanes.TryClearRow(
                _cycleTimers,
                _bufferedItemHashIds,
                _bufferedUnitCounts,
                _completedCycleCounts,
                index: 0);

            // Compaction relocates extractor B from slot 1 to slot 0 and must carry its row.
            ExtractorSlotLanes.TryMoveRow(
                _cycleTimers,
                _bufferedItemHashIds,
                _bufferedUnitCounts,
                _completedCycleCounts,
                sourceIndex: 1,
                destinationIndex: 0);

            Assert.That(_bufferedUnitCounts[0], Is.EqualTo(7));
            Assert.That(_bufferedItemHashIds[0], Is.EqualTo(2002));
            Assert.That(_completedCycleCounts[0], Is.EqualTo(21));
            Assert.That(_cycleTimers[0], Is.EqualTo(3.75f));

            // A new extractor C claims the freed tail slot 1 and must start empty.
            ExtractorSlotLanes.TryClearRow(
                _cycleTimers,
                _bufferedItemHashIds,
                _bufferedUnitCounts,
                _completedCycleCounts,
                index: 1);

            Assert.That(_bufferedUnitCounts[1], Is.EqualTo(0));
            Assert.That(_bufferedItemHashIds[1], Is.EqualTo(0));
            Assert.That(_completedCycleCounts[1], Is.EqualTo(0));
            Assert.That(_cycleTimers[1], Is.EqualTo(0f));
        }

        private void WriteRow(int index, float cycleTimerSeconds, int itemHashId, int bufferedUnits, int completedCycles)
        {
            _cycleTimers[index] = cycleTimerSeconds;
            _bufferedItemHashIds[index] = itemHashId;
            _bufferedUnitCounts[index] = bufferedUnits;
            _completedCycleCounts[index] = completedCycles;
        }

        private static void DisposeLane(ref NativeArray<float> lane)
        {
            if (lane.IsCreated)
                lane.Dispose();

            lane = default;
        }

        private static void DisposeIntLane(ref NativeArray<int> lane)
        {
            if (lane.IsCreated)
                lane.Dispose();

            lane = default;
        }
    }
}
