using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Hecton8.SaveSystem;
using Hecton8.World;

namespace Hecton8.SaveSystem.Tests
{
    [TestFixture]
    public class RadixSortSectorEntityStateEntriesJobEditTests
    {
        private NativeArray<SaveBinaryStorage.SectorEntityStateSortEntry> _entries;
        private NativeArray<SaveBinaryStorage.SectorEntityStateSortEntry> _scratch;
        private NativeArray<int> _counts;
        private NativeArray<int> _offsets;

        [TearDown]
        public void Teardown()
        {
            if (_entries.IsCreated) _entries.Dispose();
            if (_scratch.IsCreated) _scratch.Dispose();
            if (_counts.IsCreated) _counts.Dispose();
            if (_offsets.IsCreated) _offsets.Dispose();
        }

        private void InitializeArrays(int length)
        {
            _entries = new NativeArray<SaveBinaryStorage.SectorEntityStateSortEntry>(length, Allocator.TempJob);
            _scratch = new NativeArray<SaveBinaryStorage.SectorEntityStateSortEntry>(length, Allocator.TempJob);
            _counts = new NativeArray<int>(65536, Allocator.TempJob);
            _offsets = new NativeArray<int>(65536, Allocator.TempJob);
        }

        [Test]
        public void Execute_WithUnsortedEntries_SortsCorrectlyBySortKey()
        {
            InitializeArrays(5);

            _entries[0] = new SaveBinaryStorage.SectorEntityStateSortEntry { SortKey = 500 };
            _entries[1] = new SaveBinaryStorage.SectorEntityStateSortEntry { SortKey = 100 };
            _entries[2] = new SaveBinaryStorage.SectorEntityStateSortEntry { SortKey = 9999999999999999 };
            _entries[3] = new SaveBinaryStorage.SectorEntityStateSortEntry { SortKey = 0 };
            _entries[4] = new SaveBinaryStorage.SectorEntityStateSortEntry { SortKey = 250 };

            var job = new SaveBinaryStorage.RadixSortSectorEntityStateEntriesJob
            {
                Entries = _entries,
                Scratch = _scratch,
                Counts = _counts,
                Offsets = _offsets
            };

            job.Run();

            Assert.AreEqual(0, _entries[0].SortKey);
            Assert.AreEqual(100, _entries[1].SortKey);
            Assert.AreEqual(250, _entries[2].SortKey);
            Assert.AreEqual(500, _entries[3].SortKey);
            Assert.AreEqual(9999999999999999, _entries[4].SortKey);
        }

        [Test]
        public void Execute_WithEmptyEntries_DoesNotThrow()
        {
            InitializeArrays(0);

            var job = new SaveBinaryStorage.RadixSortSectorEntityStateEntriesJob
            {
                Entries = _entries,
                Scratch = _scratch,
                Counts = _counts,
                Offsets = _offsets
            };

            Assert.DoesNotThrow(() => job.Run());
        }

        [Test]
        public void Execute_WithSingleEntry_DoesNotChangeArray()
        {
            InitializeArrays(1);

            _entries[0] = new SaveBinaryStorage.SectorEntityStateSortEntry { SortKey = 42 };

            var job = new SaveBinaryStorage.RadixSortSectorEntityStateEntriesJob
            {
                Entries = _entries,
                Scratch = _scratch,
                Counts = _counts,
                Offsets = _offsets
            };

            job.Run();

            Assert.AreEqual(42, _entries[0].SortKey);
        }

        [Test]
        public void Execute_WithDuplicateKeys_MaintainsStableSort()
        {
            InitializeArrays(4);

            _entries[0] = new SaveBinaryStorage.SectorEntityStateSortEntry { SortKey = 100, Record = new EntityDataRecord { InstanceUid = 1 } };
            _entries[1] = new SaveBinaryStorage.SectorEntityStateSortEntry { SortKey = 50, Record = new EntityDataRecord { InstanceUid = 2 } };
            _entries[2] = new SaveBinaryStorage.SectorEntityStateSortEntry { SortKey = 100, Record = new EntityDataRecord { InstanceUid = 3 } };
            _entries[3] = new SaveBinaryStorage.SectorEntityStateSortEntry { SortKey = 10, Record = new EntityDataRecord { InstanceUid = 4 } };

            var job = new SaveBinaryStorage.RadixSortSectorEntityStateEntriesJob
            {
                Entries = _entries,
                Scratch = _scratch,
                Counts = _counts,
                Offsets = _offsets
            };

            job.Run();

            Assert.AreEqual(10, _entries[0].SortKey);
            Assert.AreEqual(50, _entries[1].SortKey);
            Assert.AreEqual(100, _entries[2].SortKey);
            Assert.AreEqual(1, _entries[2].Record.InstanceUid);
            Assert.AreEqual(100, _entries[3].SortKey);
            Assert.AreEqual(3, _entries[3].Record.InstanceUid);
        }
    }
}
