using Hecton8.Audio.Propagation;
using Hecton8.Core.Contracts;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Tests.Editor
{
    public sealed class AcousticPortalPropagationTests
    {
        [Test]
        public void AcousticPathJob_RoutesThroughCornerPortal()
        {
            RunThreeNodeRoute(AcousticPortalFlags.None, out AcousticPathResult result);

            Assert.AreEqual(AcousticPathStatus.PathFound, result.Status);
            Assert.AreEqual(1, result.UsedPortalPath);
            Assert.AreEqual(1, result.CornerCount);
            Assert.AreEqual(20f / AcousticPortalConstants.SoundSpeedWaterMetersPerSecond, result.DelaySeconds, 0.0001f);
            Assert.LessOrEqual(result.LowPassCutoffHz, AcousticPortalConstants.CornerLowPassHertz);
            Assert.AreEqual(AcousticPortalConstants.CornerGain, result.Transmission01, 0.0001f);
        }

        [Test]
        public void AcousticPathJob_SealedBulkheadAddsMuffleAndDelay()
        {
            RunThreeNodeRoute(AcousticPortalFlags.SealedBulkhead, out AcousticPathResult result);

            Assert.AreEqual(AcousticPathStatus.PathFound, result.Status);
            Assert.AreEqual(1, result.UsedSealedBulkhead);
            Assert.AreEqual(AcousticPortalConstants.SealedBulkheadLowPassHertz, result.LowPassCutoffHz, 0.001f);
            Assert.AreEqual(
                (20f / AcousticPortalConstants.SoundSpeedWaterMetersPerSecond) + AcousticPortalConstants.SealedBulkheadDelaySeconds,
                result.DelaySeconds,
                0.0001f);
        }

        private static void RunThreeNodeRoute(AcousticPortalFlags edgeFlags, out AcousticPathResult result)
        {
            NativeArray<AcousticPortalNode> nodes = new NativeArray<AcousticPortalNode>(3, Allocator.TempJob);
            NativeArray<AcousticPortalEdge> edges = new NativeArray<AcousticPortalEdge>(4, Allocator.TempJob);
            NativeArray<AcousticPathResult> results = new NativeArray<AcousticPathResult>(1, Allocator.TempJob);
            NativeArray<float> costs = new NativeArray<float>(3, Allocator.TempJob);
            NativeArray<int> cameFrom = new NativeArray<int>(3, Allocator.TempJob);
            NativeArray<byte> states = new NativeArray<byte>(3, Allocator.TempJob);
            NativeList<int> openSet = new NativeList<int>(3, Allocator.TempJob);
            NativeList<int> closedSet = new NativeList<int>(3, Allocator.TempJob);

            try
            {
                AcousticAup source = new AcousticAup(0, 0, 0, new float3(0f, 0f, 0f));
                AcousticAup portal = new AcousticAup(0, 0, 0, new float3(10f, 0f, 0f));
                AcousticAup listener = new AcousticAup(0, 0, 0, new float3(20f, 0f, 0f));

                nodes[0] = new AcousticPortalNode { Position = source, FirstEdge = 0, EdgeCount = 1, Flags = AcousticPortalFlags.Voxel };
                nodes[1] = new AcousticPortalNode { Position = portal, FirstEdge = 1, EdgeCount = 2, RoomVolumeCubicMeters = 100f, Flags = AcousticPortalFlags.Voxel };
                nodes[2] = new AcousticPortalNode { Position = listener, FirstEdge = 3, EdgeCount = 1, RoomVolumeCubicMeters = 100f, Flags = AcousticPortalFlags.Voxel };

                edges[0] = new AcousticPortalEdge { ToNode = 1, DistanceMeters = 10f, Flags = edgeFlags };
                edges[1] = new AcousticPortalEdge { ToNode = 0, DistanceMeters = 10f, Flags = edgeFlags };
                edges[2] = new AcousticPortalEdge { ToNode = 2, DistanceMeters = 10f, Flags = edgeFlags };
                edges[3] = new AcousticPortalEdge { ToNode = 1, DistanceMeters = 10f, Flags = edgeFlags };

                new AcousticPathJob
                {
                    Nodes = nodes,
                    Edges = edges,
                    OpenSet = openSet,
                    ClosedSet = closedSet,
                    Costs = costs,
                    CameFrom = cameFrom,
                    States = states,
                    Result = results,
                    Query = new AcousticPathQuery
                    {
                        SourceAup = source,
                        ListenerAup = listener,
                        ListenerRight = new float3(1f, 0f, 0f),
                        NodeCount = 3,
                        EdgeCount = 4,
                        MaxNodeExpansions = AcousticPortalConstants.MaxPathNodes,
                        GlobalQualityWeight = 1f,
                        DisablePortalPath = 0
                    }
                }.Run();

                result = results[0];
            }
            finally
            {
                if (closedSet.IsCreated)
                    closedSet.Dispose();
                if (openSet.IsCreated)
                    openSet.Dispose();
                if (states.IsCreated)
                    states.Dispose();
                if (cameFrom.IsCreated)
                    cameFrom.Dispose();
                if (costs.IsCreated)
                    costs.Dispose();
                if (results.IsCreated)
                    results.Dispose();
                if (edges.IsCreated)
                    edges.Dispose();
                if (nodes.IsCreated)
                    nodes.Dispose();
            }
        }
    }
}
