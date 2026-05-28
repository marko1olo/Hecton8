using Hecton8.Construction;
using Hecton8.Logistics;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Tests.Editor
{
    public sealed unsafe class FluidLogisticsCsr1421EditTests
    {
        const int StressNodeCount = 10000;
        const int StressEdgeCount = 30000;

        [Test]
        public void DrainageDtos_AreExplicitArm64Aligned()
        {
            Assert.IsTrue(SumpPumpPipeGridValidation.ValidateDrainageNodeLayout());
            Assert.IsTrue(SumpPumpPipeGridValidation.ValidatePipeEdgeLayout());
            Assert.IsTrue(SumpPumpPipeGridValidation.ValidateDrainageTuningLayout());
            Assert.IsTrue(SumpPumpPipeGridValidation.ValidateDrainageTelemetryLayout());
            Assert.IsTrue(SumpPumpPipeGridValidation.ValidateDrainageDumpHeaderLayout());
            Assert.IsTrue(SumpPumpPipeGridValidation.ValidateRoomDrainLockLayout());
            Assert.AreEqual(32, UnsafeUtility.SizeOf<DrainageNodeDTO>());
            Assert.AreEqual(32, UnsafeUtility.SizeOf<PipeEdgeDTO>());
            Assert.AreEqual(80, UnsafeUtility.SizeOf<DrainageTuningDTO>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<DrainageTelemetryEntry>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<DrainageDumpHeader>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<DrainageRoomDrainLock64>());
        }

        [Test]
        public void FluidPipeCadence_ConsumesContinuousQualityWeight()
        {
            float lowCadence = FluidPipeGraphConstants.ResolveCadenceSeconds(0f);
            float middleCadence = FluidPipeGraphConstants.ResolveCadenceSeconds(0.5f);
            float ultraCadence = FluidPipeGraphConstants.ResolveCadenceSeconds(1f);

            Assert.Greater(lowCadence, middleCadence);
            Assert.Greater(middleCadence, ultraCadence);
            Assert.AreEqual(FluidPipeGraphConstants.LowCadenceSeconds, lowCadence);
            Assert.AreEqual(FluidPipeGraphConstants.UltraCadenceSeconds, ultraCadence);
        }

        [Test]
        public void BuildFluidPipeCsrJob_CompactsConnectionsIntoContiguousOffsets()
        {
            NativeArray<int> sources = new NativeArray<int>(6, Allocator.TempJob);
            NativeArray<int> destinations = new NativeArray<int>(6, Allocator.TempJob);
            NativeArray<int> offsets = new NativeArray<int>(5, Allocator.TempJob);
            NativeArray<int> csrDestinations = new NativeArray<int>(6, Allocator.TempJob);
            NativeArray<int> writeCursor = new NativeArray<int>(4, Allocator.TempJob);

            try
            {
                sources[0] = 0;
                destinations[0] = 1;
                sources[1] = 0;
                destinations[1] = 2;
                sources[2] = 1;
                destinations[2] = 2;
                sources[3] = 2;
                destinations[3] = 0;
                sources[4] = 2;
                destinations[4] = 3;
                sources[5] = 3;
                destinations[5] = 1;

                BuildFluidPipeCsrJob job = new BuildFluidPipeCsrJob
                {
                    NodeCount = 4,
                    ConnectionCount = 6,
                    ConnectionSources = sources,
                    ConnectionDestinations = destinations,
                    ConnectionOffsets = offsets,
                    ConnectionCsrDestinations = csrDestinations,
                    ConnectionWriteCursor = writeCursor
                };

                job.Schedule().Complete();

                Assert.AreEqual(0, offsets[0]);
                Assert.AreEqual(2, offsets[1]);
                Assert.AreEqual(3, offsets[2]);
                Assert.AreEqual(5, offsets[3]);
                Assert.AreEqual(6, offsets[4]);
                Assert.AreEqual(1, csrDestinations[0]);
                Assert.AreEqual(2, csrDestinations[1]);
                Assert.AreEqual(2, csrDestinations[2]);
                Assert.AreEqual(0, csrDestinations[3]);
                Assert.AreEqual(3, csrDestinations[4]);
                Assert.AreEqual(1, csrDestinations[5]);
            }
            finally
            {
                writeCursor.Dispose();
                csrDestinations.Dispose();
                offsets.Dispose();
                destinations.Dispose();
                sources.Dispose();
            }
        }

        [Test]
        public void BuildCsrPipeGraphJob_Stress10000Nodes30000Edges_ProducesContiguousOffsets()
        {
            NativeArray<DrainageNodeDTO> nodes = new NativeArray<DrainageNodeDTO>(StressNodeCount, Allocator.TempJob);
            NativeArray<PipeEdgeDTO> edges = new NativeArray<PipeEdgeDTO>(StressEdgeCount, Allocator.TempJob);
            NativeArray<double3> nodeAup = new NativeArray<double3>(StressNodeCount, Allocator.TempJob);
            NativeArray<int> offsets = new NativeArray<int>(StressNodeCount + 1, Allocator.TempJob);
            NativeArray<int> destinations = new NativeArray<int>(StressEdgeCount, Allocator.TempJob);
            NativeArray<float> conductance = new NativeArray<float>(StressEdgeCount, Allocator.TempJob);
            NativeArray<float> flow = new NativeArray<float>(StressEdgeCount, Allocator.TempJob);
            NativeArray<int> flatEdgeIndex = new NativeArray<int>(StressEdgeCount, Allocator.TempJob);
            NativeArray<int> writeCursor = new NativeArray<int>(StressNodeCount + 1, Allocator.TempJob);
            NativeArray<int> counters = new NativeArray<int>(SumpPumpPipeGridConstants.CounterCount, Allocator.TempJob);
            NativeArray<float> pressureA = new NativeArray<float>(StressNodeCount, Allocator.TempJob);
            NativeArray<float> pressureB = new NativeArray<float>(StressNodeCount, Allocator.TempJob);
            NativeArray<float> power = new NativeArray<float>(StressNodeCount, Allocator.TempJob);

            try
            {
                SeedStressGraph(nodes, edges, nodeAup, pressureA, power);

                BuildCsrPipeGraphJob buildJob = new BuildCsrPipeGraphJob
                {
                    PipeEdges = edges,
                    NodeAup = nodeAup,
                    NodeEdgeOffsets = offsets,
                    EdgeDestinations = destinations,
                    EdgeConductance = conductance,
                    EdgeCurrentFlow = flow,
                    CsrFlatEdgeIndex = flatEdgeIndex,
                    EdgeWriteCursor = writeCursor,
                    Counters = counters,
                    NodeCount = StressNodeCount,
                    EdgeCount = StressEdgeCount,
                    BasePipeConductance = SumpPumpPipeGridConstants.DefaultBasePipeConductance
                };
                buildJob.Schedule().Complete();

                Assert.AreEqual(0, offsets[0]);
                Assert.AreEqual(StressEdgeCount, offsets[StressNodeCount]);
                Assert.AreEqual(StressEdgeCount, counters[SumpPumpPipeGridConstants.CounterValidCsrEdges]);

                for (int nodeIndex = 0; nodeIndex < StressNodeCount; nodeIndex++)
                {
                    Assert.LessOrEqual(offsets[nodeIndex], offsets[nodeIndex + 1]);
                    for (int edgeIndex = offsets[nodeIndex]; edgeIndex < offsets[nodeIndex + 1]; edgeIndex++)
                    {
                        Assert.AreEqual(nodeIndex, edges[flatEdgeIndex[edgeIndex]].SourceNodeIndex);
                        Assert.GreaterOrEqual(destinations[edgeIndex], 0);
                        Assert.Less(destinations[edgeIndex], StressNodeCount);
                        Assert.Greater(conductance[edgeIndex], 0f);
                    }
                }

                EvaluatePipePressureDeltaPassJob pressureJob = new EvaluatePipePressureDeltaPassJob
                {
                    PumpNodes = (DrainageNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(nodes),
                    NodeEdgeOffsets = offsets,
                    EdgeDestinations = destinations,
                    EdgeConductance = conductance,
                    NodeAup = nodeAup,
                    PressureFront = pressureA,
                    PressureBack = pressureB,
                    PowerPotential = power,
                    NodeCount = StressNodeCount,
                    DeltaSmoothingFactor = SumpPumpPipeGridConstants.DefaultDeltaSmoothingFactor,
                    GravityAssistScalar = SumpPumpPipeGridConstants.DefaultGravityAssistScalar,
                    GravityResistanceScalar = SumpPumpPipeGridConstants.DefaultGravityResistanceScalar
                };

                pressureJob.Schedule(StressNodeCount, 128).Complete();

                AssertFinitePressure(pressureB);
            }
            finally
            {
                power.Dispose();
                pressureB.Dispose();
                pressureA.Dispose();
                counters.Dispose();
                writeCursor.Dispose();
                flatEdgeIndex.Dispose();
                flow.Dispose();
                conductance.Dispose();
                destinations.Dispose();
                offsets.Dispose();
                nodeAup.Dispose();
                edges.Dispose();
                nodes.Dispose();
            }
        }

        static void SeedStressGraph(
            NativeArray<DrainageNodeDTO> nodes,
            NativeArray<PipeEdgeDTO> edges,
            NativeArray<double3> nodeAup,
            NativeArray<float> pressure,
            NativeArray<float> power)
        {
            for (int nodeIndex = 0; nodeIndex < StressNodeCount; nodeIndex++)
            {
                nodes[nodeIndex] = new DrainageNodeDTO
                {
                    NodeHashID = 0x8E210000u + (uint)nodeIndex,
                    HydraulicPressure = (nodeIndex & 63) * (1f / 63f),
                    MaxPumpRate = (nodeIndex % 19) == 0 ? 0.32f : 0f,
                    CurrentFlow = 0f,
                    Flags = SumpPumpNodeFlags.Active | ((nodeIndex % 19) == 0 ? SumpPumpNodeFlags.Pump : 0u)
                };
                nodeAup[nodeIndex] = new double3(nodeIndex & 127, -((nodeIndex * 7) % 23) * 0.25, nodeIndex >> 7);
                pressure[nodeIndex] = nodes[nodeIndex].HydraulicPressure;
                power[nodeIndex] = 0.75f;
            }

            for (int edgeIndex = 0; edgeIndex < StressEdgeCount; edgeIndex++)
            {
                int source = edgeIndex % StressNodeCount;
                int stride = 1 + ((edgeIndex * 17) % (StressNodeCount - 1));
                int destination = (source + stride) % StressNodeCount;
                edges[edgeIndex] = new PipeEdgeDTO
                {
                    SourceNodeIndex = source,
                    DestinationNodeIndex = destination,
                    Conductance = 0.04f + ((edgeIndex & 15) * 0.0025f),
                    CurrentFlow = 0f,
                    Flags = SumpPipeEdgeFlags.Active,
                    EdgeHash = 0x8E420000u + (uint)edgeIndex,
                    SourceNodeHash = 0x8E210000u + (uint)source,
                    DestinationNodeHash = 0x8E210000u + (uint)destination
                };
            }
        }

        static void AssertFinitePressure(NativeArray<float> pressure)
        {
            float total = 0f;
            for (int nodeIndex = 0; nodeIndex < pressure.Length; nodeIndex++)
            {
                Assert.IsTrue(math.isfinite(pressure[nodeIndex]));
                total += pressure[nodeIndex];
            }

            Assert.Greater(total, 0f);
        }
    }
}
