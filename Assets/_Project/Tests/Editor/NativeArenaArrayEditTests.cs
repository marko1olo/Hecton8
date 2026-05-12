using System;
using Hecton8.Core;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Hecton8.Tests.Editor
{
    public sealed class NativeArenaArrayEditTests
    {
        [SetUp]
        public void SetUp()
        {
            HectonArenaAllocator.Shutdown();
            HectonArenaAllocator.Initialize(1024 * 1024);
        }

        [TearDown]
        public void TearDown()
        {
            HectonArenaAllocator.Shutdown();
        }

        [Test]
        public void AlignOffset16_RoundsToSixteenByteBoundary()
        {
            Assert.AreEqual(0, HectonArenaAllocator.AlignOffset16(0));
            Assert.AreEqual(16, HectonArenaAllocator.AlignOffset16(1));
            Assert.AreEqual(16, HectonArenaAllocator.AlignOffset16(16));
            Assert.AreEqual(32, HectonArenaAllocator.AlignOffset16(17));
        }

        [Test]
        public void NativeArenaArray_WritesInsideIJobParallelFor()
        {
            const int Count = 64;

            Assert.IsTrue(HectonArenaAllocator.TryAllocateNativeArenaArray<int>(
                Count,
                NativeArrayOptions.ClearMemory,
                out NativeArenaArray<int> values));
            Assert.AreEqual(Count * UnsafeUtility.SizeOf<int>(), values.ByteCount);

            WriteArenaValuesJob job = new WriteArenaValuesJob
            {
                Values = values
            };

            JobHandle handle = job.Schedule(Count, 16);
            handle.Complete();

            for (int i = 0; i < Count; i++)
                Assert.AreEqual((i * 3) + 7, values[i]);
        }

        [Test]
        public unsafe void ArenaAllocation_SpillsToFreeSlabBeforeOom()
        {
            if (HectonArenaAllocator.SlabCount < 2)
                Assert.Ignore("Spare slab fallback requires at least two slabs.");

            int firstSize = HectonArenaAllocator.SlabCapacityBytes - HectonArenaAllocator.CacheLineAlignment;
            int secondSize = HectonArenaAllocator.CacheLineAlignment * 2;

            Assert.IsTrue(HectonArenaAllocator.TryAllocateBytes(
                firstSize,
                HectonArenaAllocator.CacheLineAlignment,
                out byte* first));

            int oomBefore = HectonArenaAllocator.OomCount;
            Assert.IsTrue(HectonArenaAllocator.TryAllocateBytes(
                secondSize,
                HectonArenaAllocator.CacheLineAlignment,
                out byte* second));

            Assert.AreEqual(oomBefore, HectonArenaAllocator.OomCount);
            Assert.AreNotEqual((IntPtr)first, (IntPtr)second);
        }

        [Test]
        public unsafe void OwnerTelemetry_TracksAllocatedBytesNotSlabOffset()
        {
            const uint OwnerHash = 0x4F574E52u;
            const int FirstBytes = 64;
            const int SecondBytes = 128;

            Assert.IsTrue(HectonArenaAllocator.TryAllocateBytes(
                FirstBytes,
                HectonArenaAllocator.CacheLineAlignment,
                OwnerHash,
                out byte* first));
            Assert.IsTrue(HectonArenaAllocator.TryAllocateBytes(
                SecondBytes,
                HectonArenaAllocator.CacheLineAlignment,
                OwnerHash,
                out byte* second));
            Assert.AreNotEqual((IntPtr)first, (IntPtr)second);

            Assert.IsTrue(HectonArenaAllocator.TryGetOwnerHighWaterBytes(OwnerHash, out int highWaterBytes));
            Assert.AreEqual(FirstBytes + SecondBytes, highWaterBytes);

            HectonArenaAllocator.EndFrameSwap();

            Assert.IsTrue(HectonArenaAllocator.TryGetOwnerLastFrameBytes(OwnerHash, out int lastFrameBytes));
            Assert.AreEqual(FirstBytes + SecondBytes, lastFrameBytes);
        }

        [Test]
        public unsafe void Shutdown_ClearsArenaTelemetryState()
        {
            const uint OwnerHash = 0x5348444Eu;
            const int ByteCount = 64;

            Assert.IsTrue(HectonArenaAllocator.TryAllocateBytes(
                ByteCount,
                HectonArenaAllocator.CacheLineAlignment,
                OwnerHash,
                out byte* ptr));
            Assert.AreNotEqual(IntPtr.Zero, (IntPtr)ptr);

            HectonArenaAllocator.EndFrameSwap();
            Assert.IsTrue(HectonArenaAllocator.TryGetOwnerLastFrameBytes(OwnerHash, out int lastFrameBytes));
            Assert.AreEqual(ByteCount, lastFrameBytes);

            HectonArenaAllocator.Shutdown();

            Assert.IsFalse(HectonArenaAllocator.IsCreated);
            Assert.AreEqual(0, HectonArenaAllocator.CapacityBytes);
            Assert.AreEqual(0, HectonArenaAllocator.UsedBytes);
            Assert.AreEqual(0, HectonArenaAllocator.LastFrameHighWaterBytes);
            Assert.AreEqual(0, HectonArenaAllocator.OomCount);
            Assert.IsFalse(HectonArenaAllocator.TryGetOwnerLastFrameBytes(OwnerHash, out _));
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct WriteArenaValuesJob : IJobParallelFor
        {
            public NativeArenaArray<int> Values;

            public void Execute(int index)
            {
                Values[index] = (index * 3) + 7;
            }
        }
    }
}
