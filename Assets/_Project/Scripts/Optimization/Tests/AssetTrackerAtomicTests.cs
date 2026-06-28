using NUnit.Framework;
using Unity.Collections;
using Hecton8.Optimization;

namespace Hecton8.Optimization.Tests
{
    [TestFixture]
    public class AssetTrackerAtomicTests
    {
        [Test]
        public void Increment_ValidSlot_IncreasesRefCountAndReturnsNewValue()
        {
            var trackers = new NativeArray<AssetTrackerDTO>(2, Allocator.Temp);
            try
            {
                trackers[1] = new AssetTrackerDTO { ReferenceCount = 5 };
                int result = AssetTrackerAtomic.Increment(trackers, 1);
                Assert.That(result, Is.EqualTo(6));
                Assert.That(trackers[1].ReferenceCount, Is.EqualTo(6));
            }
            finally
            {
                trackers.Dispose();
            }
        }

        [Test]
        public void Decrement_ValidSlot_DecreasesRefCountAndReturnsNewValue()
        {
            var trackers = new NativeArray<AssetTrackerDTO>(2, Allocator.Temp);
            try
            {
                trackers[1] = new AssetTrackerDTO { ReferenceCount = 5 };
                int result = AssetTrackerAtomic.Decrement(trackers, 1);
                Assert.That(result, Is.EqualTo(4));
                Assert.That(trackers[1].ReferenceCount, Is.EqualTo(4));
            }
            finally
            {
                trackers.Dispose();
            }
        }

        [Test]
        public void Increment_UncreatedArray_ReturnsZero()
        {
            var trackers = new NativeArray<AssetTrackerDTO>();
            int result = AssetTrackerAtomic.Increment(trackers, 0);
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public void Decrement_UncreatedArray_ReturnsZero()
        {
            var trackers = new NativeArray<AssetTrackerDTO>();
            int result = AssetTrackerAtomic.Decrement(trackers, 0);
            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public void Increment_OutOfBoundsSlot_ReturnsZero()
        {
            var trackers = new NativeArray<AssetTrackerDTO>(1, Allocator.Temp);
            try
            {
                int result = AssetTrackerAtomic.Increment(trackers, 1);
                Assert.That(result, Is.EqualTo(0));

                result = AssetTrackerAtomic.Increment(trackers, -1);
                Assert.That(result, Is.EqualTo(0));
            }
            finally
            {
                trackers.Dispose();
            }
        }

        [Test]
        public void Decrement_OutOfBoundsSlot_ReturnsZero()
        {
            var trackers = new NativeArray<AssetTrackerDTO>(1, Allocator.Temp);
            try
            {
                int result = AssetTrackerAtomic.Decrement(trackers, 1);
                Assert.That(result, Is.EqualTo(0));

                result = AssetTrackerAtomic.Decrement(trackers, -1);
                Assert.That(result, Is.EqualTo(0));
            }
            finally
            {
                trackers.Dispose();
            }
        }

        [Test]
        public void IsRefCountZero_ZeroCount_ReturnsTrue()
        {
            var trackers = new NativeArray<AssetTrackerDTO>(1, Allocator.Temp);
            try
            {
                trackers[0] = new AssetTrackerDTO { ReferenceCount = 0 };
                bool result = AssetTrackerAtomic.IsRefCountZero(trackers, 0);
                Assert.That(result, Is.True);
            }
            finally
            {
                trackers.Dispose();
            }
        }

        [Test]
        public void IsRefCountZero_NonZeroCount_ReturnsFalse()
        {
            var trackers = new NativeArray<AssetTrackerDTO>(1, Allocator.Temp);
            try
            {
                trackers[0] = new AssetTrackerDTO { ReferenceCount = 1 };
                bool result = AssetTrackerAtomic.IsRefCountZero(trackers, 0);
                Assert.That(result, Is.False);
            }
            finally
            {
                trackers.Dispose();
            }
        }

        [Test]
        public void IsRefCountZero_UncreatedArray_ReturnsFalse()
        {
            var trackers = new NativeArray<AssetTrackerDTO>();
            bool result = AssetTrackerAtomic.IsRefCountZero(trackers, 0);
            Assert.That(result, Is.False);
        }

        [Test]
        public void IsRefCountZero_OutOfBoundsSlot_ReturnsFalse()
        {
            var trackers = new NativeArray<AssetTrackerDTO>(1, Allocator.Temp);
            try
            {
                bool result = AssetTrackerAtomic.IsRefCountZero(trackers, 1);
                Assert.That(result, Is.False);

                result = AssetTrackerAtomic.IsRefCountZero(trackers, -1);
                Assert.That(result, Is.False);
            }
            finally
            {
                trackers.Dispose();
            }
        }
    }
}
