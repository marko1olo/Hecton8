using Hecton8.Core;
using Hecton8.World;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Editor.Tests
{
    /// <summary>
    /// EditMode regression coverage for spatial hash handle generations and AUP-scale queries.
    /// </summary>
    public sealed class HectonSpatialHashEditorSelfTests
    {
        private const string NativeMemoryOwner = nameof(HectonSpatialHashEditorSelfTests);
        private const string RecycledHandleResultsLabel = "recycledHandleResults";
        private const string MoveResultsLabel = "moveResults";
        private const string LargeAupResultsLabel = "largeAupResults";
        private const int ResourceKind = 1;
        private const ulong ResourceFlag = 1UL;

        [Test]
        public void RecycledHandle_AdvancesGeneration_AndRejectsStaleHandle()
        {
            HectonSpatialHash spatialHash = new HectonSpatialHash(8, 64, 8d);
            NativeList<int> results = AllocateTrackedResults(8, RecycledHandleResultsLabel);

            try
            {
                AbsoluteUniversePosition origin = AbsoluteUniversePosition.FromAbsolutePosition(double3.zero);
                int firstHandle = spatialHash.Register(origin, new float3(0.5f), ResourceKind, ResourceFlag, 101);

                Assert.That(firstHandle, Is.GreaterThan(0));
                Assert.That(spatialHash.IsCurrentHandle(firstHandle), Is.True);

                spatialHash.Unregister(firstHandle);
                spatialHash.Unregister(firstHandle);
                spatialHash.ReleaseHandle(firstHandle);

                Assert.That(spatialHash.IsCurrentHandle(firstHandle), Is.False);
                Assert.That(spatialHash.TryGetEntry(firstHandle, out _), Is.False);
                Assert.That(spatialHash.EntryCount, Is.EqualTo(0));

                AbsoluteUniversePosition recycledPosition =
                    AbsoluteUniversePosition.FromAbsolutePosition(new double3(32d, 0d, 0d));
                int recycledHandle = spatialHash.Register(recycledPosition, new float3(0.5f), ResourceKind, ResourceFlag, 202);

                Assert.That(recycledHandle, Is.GreaterThan(0));
                Assert.That(recycledHandle, Is.Not.EqualTo(firstHandle));
                Assert.That(spatialHash.IsCurrentHandle(recycledHandle), Is.True);
                Assert.That(spatialHash.IsCurrentHandle(firstHandle), Is.False);

                spatialHash.UpdateEntry(
                    firstHandle,
                    AbsoluteUniversePosition.FromAbsolutePosition(new double3(96d, 0d, 0d)),
                    new float3(0.5f),
                    ResourceKind,
                    ResourceFlag,
                    999);

                int staleQueryCount = spatialHash.CollectSphere(
                    AbsoluteUniversePosition.FromAbsolutePosition(new double3(96d, 0d, 0d)),
                    2f,
                    ResourceKind,
                    ResourceFlag,
                    results);
                Assert.That(staleQueryCount, Is.EqualTo(0));

                int liveQueryCount = spatialHash.CollectSphere(recycledPosition, 2f, ResourceKind, ResourceFlag, results);
                Assert.That(liveQueryCount, Is.EqualTo(1));
                Assert.That(results[0], Is.EqualTo(recycledHandle));
            }
            finally
            {
                DisposeTrackedResults(ref results, RecycledHandleResultsLabel);

                spatialHash.Dispose();
            }
        }

        [Test]
        public void UpdateEntry_MovesBetweenCells_WithoutLeavingGhostOccupancy()
        {
            HectonSpatialHash spatialHash = new HectonSpatialHash(4, 32, 8d);
            NativeList<int> results = AllocateTrackedResults(4, MoveResultsLabel);

            try
            {
                AbsoluteUniversePosition origin = AbsoluteUniversePosition.FromAbsolutePosition(double3.zero);
                AbsoluteUniversePosition moved = AbsoluteUniversePosition.FromAbsolutePosition(new double3(128d, 0d, 0d));
                int handle = spatialHash.Register(origin, new float3(0.25f), ResourceKind, ResourceFlag, 303);

                Assert.That(spatialHash.CollectSphere(origin, 2f, ResourceKind, ResourceFlag, results), Is.EqualTo(1));
                Assert.That(results[0], Is.EqualTo(handle));

                spatialHash.UpdateEntry(handle, moved, new float3(0.25f), ResourceKind, ResourceFlag, 404);

                Assert.That(spatialHash.CollectSphere(origin, 2f, ResourceKind, ResourceFlag, results), Is.EqualTo(0));
                Assert.That(spatialHash.CollectSphere(moved, 2f, ResourceKind, ResourceFlag, results), Is.EqualTo(1));
                Assert.That(results[0], Is.EqualTo(handle));
            }
            finally
            {
                DisposeTrackedResults(ref results, MoveResultsLabel);

                spatialHash.Dispose();
            }
        }

        [Test]
        public void LargeAupCoordinates_QueryWithoutRuntimeFloatDrift()
        {
            HectonSpatialHash spatialHash = new HectonSpatialHash(4, 32, 16d);
            NativeList<int> results = AllocateTrackedResults(4, LargeAupResultsLabel);

            try
            {
                double3 absolute = new double3(128000d, -64000d, 96000d);
                AbsoluteUniversePosition position = AbsoluteUniversePosition.FromAbsolutePosition(absolute);
                int handle = spatialHash.Register(position, new float3(1f, 2f, 1f), ResourceKind, ResourceFlag, 505);

                Assert.That(handle, Is.GreaterThan(0));

                int hitCount = spatialHash.CollectSphere(position, 3f, ResourceKind, ResourceFlag, results);
                Assert.That(hitCount, Is.EqualTo(1));
                Assert.That(results[0], Is.EqualTo(handle));
            }
            finally
            {
                DisposeTrackedResults(ref results, LargeAupResultsLabel);

                spatialHash.Dispose();
            }
        }

        private static NativeList<int> AllocateTrackedResults(int capacity, string label)
        {
            NativeList<int> results = new NativeList<int>(capacity, Allocator.Temp);
            if (!results.IsCreated)
                throw new System.InvalidOperationException("[HectonSpatialHashEditorSelfTests] NativeList allocation failed for " + label + ".");

            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeList(results, NativeMemoryOwner, label, NativeAllocationLifetime.Temp);
                if (sentinelId <= 0)
                    throw new System.InvalidOperationException("[HectonSpatialHashEditorSelfTests] NativeMemorySentinel rejected NativeList registration for " + label + ".");
            }
            catch
            {
                results.Dispose();
                throw;
            }

            return results;
        }

        private static void DisposeTrackedResults(ref NativeList<int> results, string label)
        {
            if (!results.IsCreated)
                return;

            try
            {
                NativeMemorySentinel.UnregisterNativeList(NativeMemoryOwner, label);
            }
            finally
            {
                results.Dispose();
                results = default;
            }
        }
    }
}
