using System;
using System.Collections.Generic;
using Hecton8.Core;
using NUnit.Framework;
using Unity.Collections;

namespace Hecton8.Tests.Editor
{
    public sealed class NativeRingBufferEditTests
    {
        [Test]
        public void CopyRange_UsesChronologicalOrderAfterWrap()
        {
            NativeRingBuffer<int> ring = new NativeRingBuffer<int>(8, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeArray<int> destination = new NativeArray<int>(8, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            try
            {
                for (int i = 0; i < 12; i++)
                    ring.Write(i + 1);

                ring.CopyRange(ring.TotalWrites - 8, 8, destination);

                for (int i = 0; i < 8; i++)
                    Assert.AreEqual(i + 5, destination[i]);
            }
            finally
            {
                if (destination.IsCreated)
                    destination.Dispose();
                ring.Dispose();
            }
        }

        [Test]
        public void CopyRange_WritesIntoDestinationOffset()
        {
            NativeRingBuffer<int> ring = new NativeRingBuffer<int>(8, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeArray<int> destination = new NativeArray<int>(10, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            try
            {
                for (int i = 0; i < 10; i++)
                    ring.Write((i + 1) * 10);

                ring.CopyRange(4, 4, destination, 3);

                Assert.AreEqual(0, destination[2]);
                Assert.AreEqual(50, destination[3]);
                Assert.AreEqual(60, destination[4]);
                Assert.AreEqual(70, destination[5]);
                Assert.AreEqual(80, destination[6]);
                Assert.AreEqual(0, destination[7]);
            }
            finally
            {
                if (destination.IsCreated)
                    destination.Dispose();
                ring.Dispose();
            }
        }

        [Test]
        public void RegisterBackingArray_WhenSentinelFails_DisposesBufferAndThrows()
        {
            NativeRingBuffer<int> ring = new NativeRingBuffer<int>(8, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            // Exhaust NativeMemorySentinel to force failure
            List<NativeArray<int>> garbage = new List<NativeArray<int>>();
            List<int> sentinels = new List<int>();

            try
            {
                // Fill up the sentinel capacity (1024)
                for (int i = 0; i < 1050; i++)
                {
                    NativeArray<int> arr = new NativeArray<int>(1, Allocator.Persistent);
                    garbage.Add(arr);
                    int id = NativeMemorySentinel.RegisterNativeArray(arr, "TestOwner", $"TestLabel_{i}", NativeAllocationLifetime.TempJob);
                    if (id > 0)
                        sentinels.Add(id);
                }

                Assert.IsTrue(ring.IsCreated, "Ring buffer should be created initially.");

                // This should fail and throw InvalidOperationException
                Assert.Throws<InvalidOperationException>(() =>
                    ring.RegisterBackingArray("TestOwner", "RingLabel", NativeAllocationLifetime.TempJob));

                // Buffer should be disposed
                Assert.IsFalse(ring.IsCreated, "Ring buffer should be disposed after failed registration.");
            }
            finally
            {
                if (ring.IsCreated)
                    ring.Dispose();

                foreach (int id in sentinels)
                    NativeMemorySentinel.Unregister(id);

                foreach (var arr in garbage)
                    arr.Dispose();
            }
        }
    }
}
