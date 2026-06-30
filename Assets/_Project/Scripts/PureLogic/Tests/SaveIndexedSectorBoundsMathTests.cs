#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using Hecton8.SaveSystem;
using Unity.Collections;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class ValidateIndexedSectorBoundsProbeJobTests
    {
        [Test]
        public void Execute_ValidBlock_ExpectedValid_ReturnsOne()
        {
            var probes = new NativeArray<IndexedSectorBoundsProbe>(1, Allocator.Temp);
            probes[0] = new IndexedSectorBoundsProbe
            {
                ByteOffset = 100L,
                CompressedSize = 50,
                MinimumByteOffset = 0L,
                FileLength = 200L,
                ExpectedValid = 1
            };
            var results = new NativeArray<byte>(1, Allocator.Temp);
            var job = new ValidateIndexedSectorBoundsProbeJob { Probes = probes, Results = results };

            job.Execute(0);

            Assert.AreEqual((byte)1, results[0], "Valid block matching ExpectedValid should return 1.");

            probes.Dispose();
            results.Dispose();
        }

        [Test]
        public void Execute_InvalidBlock_ExpectedInvalid_ReturnsOne()
        {
            var probes = new NativeArray<IndexedSectorBoundsProbe>(1, Allocator.Temp);
            probes[0] = new IndexedSectorBoundsProbe
            {
                ByteOffset = 100L,
                CompressedSize = -10, // Invalid block
                MinimumByteOffset = 0L,
                FileLength = 200L,
                ExpectedValid = 0
            };
            var results = new NativeArray<byte>(1, Allocator.Temp);
            var job = new ValidateIndexedSectorBoundsProbeJob { Probes = probes, Results = results };

            job.Execute(0);

            Assert.AreEqual((byte)1, results[0], "Invalid block matching ExpectedValid == 0 should return 1.");

            probes.Dispose();
            results.Dispose();
        }

        [Test]
        public void Execute_ValidBlock_ExpectedInvalid_ReturnsZero()
        {
            var probes = new NativeArray<IndexedSectorBoundsProbe>(1, Allocator.Temp);
            probes[0] = new IndexedSectorBoundsProbe
            {
                ByteOffset = 100L,
                CompressedSize = 50, // Valid block
                MinimumByteOffset = 0L,
                FileLength = 200L,
                ExpectedValid = 0
            };
            var results = new NativeArray<byte>(1, Allocator.Temp);
            var job = new ValidateIndexedSectorBoundsProbeJob { Probes = probes, Results = results };

            job.Execute(0);

            Assert.AreEqual((byte)0, results[0], "Valid block but ExpectedValid == 0 should return 0.");

            probes.Dispose();
            results.Dispose();
        }

        [Test]
        public void Execute_InvalidBlock_ExpectedValid_ReturnsZero()
        {
            var probes = new NativeArray<IndexedSectorBoundsProbe>(1, Allocator.Temp);
            probes[0] = new IndexedSectorBoundsProbe
            {
                ByteOffset = 100L,
                CompressedSize = -10, // Invalid block
                MinimumByteOffset = 0L,
                FileLength = 200L,
                ExpectedValid = 1
            };
            var results = new NativeArray<byte>(1, Allocator.Temp);
            var job = new ValidateIndexedSectorBoundsProbeJob { Probes = probes, Results = results };

            job.Execute(0);

            Assert.AreEqual((byte)0, results[0], "Invalid block but ExpectedValid == 1 should return 0.");

            probes.Dispose();
            results.Dispose();
        }
    }
}
#endif
