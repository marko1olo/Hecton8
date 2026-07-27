using NUnit.Framework;
using Unity.Collections;

namespace Hecton8.Inventory.Tests
{
    /// <summary>
    /// Locks the slot-window contract of <see cref="SoaInventoryQueryEngine.ResolveScanWindow"/> and
    /// the empty-inventory insert path that depends on it.
    ///
    /// Regression guarded: the hash scan used to read <c>requestedSlotCount &gt; 0 ? ... : laneLength</c>,
    /// so a requested count of zero - which every call site produces from an ACTIVE SLOT COUNT and which
    /// therefore means "the inventory is empty" - opened the window over the whole SOA buffer. The lanes
    /// are acquired with <c>NativeArrayOptions.UninitializedMemory</c>, so the tail past the active
    /// region carries stale vault bytes. A stale hash match there made the first pickup of the route
    /// take the "already present" branch: no slot was reserved, ActiveSlotCount stayed 0, and the
    /// quantity landed in a slot no consumer scans. The resource vanished with no error.
    /// </summary>
    [TestFixture]
    public sealed class SoaInventoryScanWindowTests
    {
        private const int LaneLength = 8;
        private const uint StaleHash = 0xC0FFEE01u;

        [Test]
        public void EmptyActiveRegion_ResolvesEmptyWindow()
        {
            SoaInventoryQueryEngine.ResolveScanWindow(
                SoaInventoryQueryEngine.DefaultSlotCapacity,
                SoaInventoryQueryEngine.DefaultSlotCapacity,
                slotStart: 0,
                requestedSlotCount: 0,
                out int scanStart,
                out int scanEnd);

            Assert.AreEqual(0, scanStart);
            Assert.AreEqual(0, scanEnd, "An active count of zero must scan zero slots, not the whole buffer.");
        }

        [Test]
        public void ActiveRegion_StopsAtActiveCount()
        {
            SoaInventoryQueryEngine.ResolveScanWindow(
                SoaInventoryQueryEngine.DefaultSlotCapacity,
                SoaInventoryQueryEngine.DefaultSlotCapacity,
                slotStart: 0,
                requestedSlotCount: 3,
                out int scanStart,
                out int scanEnd);

            Assert.AreEqual(0, scanStart);
            Assert.AreEqual(3, scanEnd);
        }

        [Test]
        public void SlotStart_ShiftsWindowAndKeepsCount()
        {
            SoaInventoryQueryEngine.ResolveScanWindow(
                SoaInventoryQueryEngine.DefaultSlotCapacity,
                SoaInventoryQueryEngine.DefaultSlotCapacity,
                slotStart: 4,
                requestedSlotCount: 3,
                out int scanStart,
                out int scanEnd);

            Assert.AreEqual(4, scanStart);
            Assert.AreEqual(7, scanEnd);
        }

        [Test]
        public void ShorterQuantityLane_ClipsWindow()
        {
            SoaInventoryQueryEngine.ResolveScanWindow(
                hashLaneLength: 512,
                quantityLaneLength: 8,
                slotStart: 4,
                requestedSlotCount: 512,
                out int scanStart,
                out int scanEnd);

            Assert.AreEqual(4, scanStart);
            Assert.AreEqual(8, scanEnd, "The window may never run past the shortest bound lane.");
        }

        [Test]
        public void SlotStartPastLane_ResolvesEmptyWindow()
        {
            SoaInventoryQueryEngine.ResolveScanWindow(
                hashLaneLength: 8,
                quantityLaneLength: 8,
                slotStart: 600,
                requestedSlotCount: 4,
                out int scanStart,
                out int scanEnd);

            Assert.AreEqual(8, scanStart);
            Assert.AreEqual(8, scanEnd);
        }

        [Test]
        public void ScanToBufferEndSentinel_OpensFullWindow()
        {
            SoaInventoryQueryEngine.ResolveScanWindow(
                hashLaneLength: 8,
                quantityLaneLength: 8,
                slotStart: 2,
                requestedSlotCount: SoaInventoryQueryEngine.ScanToBufferEnd,
                out int scanStart,
                out int scanEnd);

            Assert.AreEqual(2, scanStart);
            Assert.AreEqual(8, scanEnd);
        }

        [Test]
        public void FirstPickup_IntoEmptyInventoryWithStaleTail_ReservesSlotZero()
        {
            NativeArray<uint> itemHashIds = new NativeArray<uint>(LaneLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeArray<uint> quantities = new NativeArray<uint>(LaneLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeArray<float> durabilities = new NativeArray<float>(LaneLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeArray<int> activeSlotCount = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            try
            {
                // Uninitialized vault tail: slot 5 still carries a hash from an earlier arena tenant,
                // while the authoritative active region is empty.
                itemHashIds[5] = StaleHash;
                quantities[5] = 7u;
                activeSlotCount[0] = 0;

                bool accepted = SoaInventoryQueryEngine.TryApplyMutationOwnerPhase(
                    itemHashIds,
                    quantities,
                    durabilities,
                    activeSlotCount,
                    targetHashId: StaleHash,
                    quantityDelta: 1,
                    insertWhenMissing: 1u,
                    removeWhenZero: 1u,
                    initialDurability01: 1f,
                    out InventorySoaMutationResultDTO mutation);

                Assert.IsTrue(accepted, "A first pickup into an empty inventory must be accepted.");
                Assert.AreEqual(0, mutation.SlotIndex, "The pickup must reserve the first active slot, not the stale tail slot.");
                Assert.AreNotEqual(0u, mutation.Flags & SoaInventoryQueryEngine.ResultInserted, "The pickup must be recorded as an insert.");
                Assert.AreEqual(1, mutation.ActiveAfter, "ActiveSlotCount must advance, otherwise no consumer can see the item.");
                Assert.AreEqual(1u, mutation.NewQuantity);
                Assert.AreEqual(StaleHash, itemHashIds[0]);
                Assert.AreEqual(1u, quantities[0]);
                Assert.AreEqual(7u, quantities[5], "The stale tail slot must not absorb the pickup.");
            }
            finally
            {
                itemHashIds.Dispose();
                quantities.Dispose();
                durabilities.Dispose();
                activeSlotCount.Dispose();
            }
        }

        [Test]
        public void SecondPickup_OfSameItem_StacksInsideActiveRegion()
        {
            NativeArray<uint> itemHashIds = new NativeArray<uint>(LaneLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeArray<uint> quantities = new NativeArray<uint>(LaneLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeArray<float> durabilities = new NativeArray<float>(LaneLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeArray<int> activeSlotCount = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            try
            {
                itemHashIds[0] = StaleHash;
                quantities[0] = 1u;
                activeSlotCount[0] = 1;

                bool accepted = SoaInventoryQueryEngine.TryApplyMutationOwnerPhase(
                    itemHashIds,
                    quantities,
                    durabilities,
                    activeSlotCount,
                    targetHashId: StaleHash,
                    quantityDelta: 2,
                    insertWhenMissing: 1u,
                    removeWhenZero: 1u,
                    initialDurability01: 1f,
                    out InventorySoaMutationResultDTO mutation);

                Assert.IsTrue(accepted);
                Assert.AreEqual(0, mutation.SlotIndex);
                Assert.AreEqual(0u, mutation.Flags & SoaInventoryQueryEngine.ResultInserted, "An occupied active slot must stack, not reserve a second slot.");
                Assert.AreEqual(3u, mutation.NewQuantity);
                Assert.AreEqual(1, mutation.ActiveAfter);
            }
            finally
            {
                itemHashIds.Dispose();
                quantities.Dispose();
                durabilities.Dispose();
                activeSlotCount.Dispose();
            }
        }
    }
}
