using NUnit.Framework;
using Hecton8.Core.Memory;
using Hecton8.Power;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

public sealed class PowerGridJacobiContractsEditTests
{
    [Test]
    public void PowerNodeDto_IsExactArm64Layout()
    {
        Assert.IsTrue(PowerGridLayoutAudit.ValidatePowerNodeDtoLayout());
        Assert.AreEqual(32, UnsafeUtility.SizeOf<PowerNodeDTO>());
        Assert.AreEqual(0, OffsetOf<PowerNodeDTO>(nameof(PowerNodeDTO.NodeHash)));
        Assert.AreEqual(4, OffsetOf<PowerNodeDTO>(nameof(PowerNodeDTO.Potential)));
        Assert.AreEqual(8, OffsetOf<PowerNodeDTO>(nameof(PowerNodeDTO.MaxCapacity)));
        Assert.AreEqual(12, OffsetOf<PowerNodeDTO>(nameof(PowerNodeDTO.CurrentStorage)));
        Assert.AreEqual(16, OffsetOf<PowerNodeDTO>(nameof(PowerNodeDTO.Flags)));
        Assert.AreEqual(20, OffsetOf<PowerNodeDTO>(nameof(PowerNodeDTO.InternalResistance)));
        Assert.AreEqual(24, OffsetOf<PowerNodeDTO>("_pad0"));
        Assert.AreEqual(31, OffsetOf<PowerNodeDTO>("_pad7"));
    }

    [Test]
    public void AllPowerDtos_HaveExactAuditedOffsets()
    {
        Assert.IsTrue(PowerGridLayoutAudit.ValidateAllPowerLayouts());
        Assert.AreEqual(0, OffsetOf<PowerGridEdgeDTO>(nameof(PowerGridEdgeDTO.SourceNodeHash)));
        Assert.AreEqual(28, OffsetOf<PowerGridEdgeDTO>(nameof(PowerGridEdgeDTO.DestinationNodeIndex)));
        Assert.AreEqual(0, OffsetOf<PowerProfileDTO>(nameof(PowerProfileDTO.ProfileHash)));
        Assert.AreEqual(28, OffsetOf<PowerProfileDTO>(nameof(PowerProfileDTO.Reserved1)));
        Assert.AreEqual(0, OffsetOf<PowerTelemetryEntry>(nameof(PowerTelemetryEntry.FrameIndex)));
        Assert.AreEqual(36, OffsetOf<PowerTelemetryEntry>(nameof(PowerTelemetryEntry.TotalLoad)));
        Assert.AreEqual(44, OffsetOf<PowerTelemetryEntry>(nameof(PowerTelemetryEntry.AveragePotential)));
        Assert.AreEqual(60, OffsetOf<PowerTelemetryEntry>(nameof(PowerTelemetryEntry.SolverMicroseconds)));
    }

    [Test]
    public void PowerVaultIds_UseOwnerLocalGenerationDescriptors()
    {
        Assert.AreEqual((BufferID)70850, PowerGridBufferIds.Nodes);
        Assert.AreEqual((BufferID)70864, PowerGridBufferIds.CsvScratch);
        Assert.AreEqual(16, UnsafeUtility.SizeOf<VaultGenerationHandle<PowerNodeDTO>>());
        Assert.AreEqual(16, UnsafeUtility.SizeOf<PowerEquipmentLoadRequest>());
        Assert.AreEqual(64, UnsafeUtility.SizeOf<PowerGridCounter64>());
        Assert.AreEqual(0, OffsetOf<PowerGridCounter64>(nameof(PowerGridCounter64.Value)));
        Assert.AreEqual(56, OffsetOf<PowerGridCounter64>(nameof(PowerGridCounter64.Reserved6)));
    }

    [Test]
    public void JacobiIterations_AreContinuousOneToEight()
    {
        Assert.AreEqual(1, PowerSolverConvergenceMath.ResolvePropagationIterations(0f));
        Assert.AreEqual(4, PowerSolverConvergenceMath.ResolvePropagationIterations(0.5f));
        Assert.AreEqual(8, PowerSolverConvergenceMath.ResolvePropagationIterations(1f));
    }

    [Test]
    public void BuildCsrPowerGraph_ZeroesDamagedConductanceWithoutDroppingEdge()
    {
        NativeArray<PowerNodeDTO> nodes = new NativeArray<PowerNodeDTO>(2, Allocator.TempJob);
        NativeArray<PowerGridEdgeDTO> edges = new NativeArray<PowerGridEdgeDTO>(1, Allocator.TempJob);
        NativeArray<int> offsets = new NativeArray<int>(3, Allocator.TempJob);
        NativeArray<int> cursors = new NativeArray<int>(2, Allocator.TempJob);
        NativeArray<int> destinations = new NativeArray<int>(2, Allocator.TempJob);
        NativeArray<float> conductance = new NativeArray<float>(2, Allocator.TempJob);
        NativeArray<float> flow = new NativeArray<float>(2, Allocator.TempJob);
        try
        {
            PowerNodeDTO sourceNode = default;
            sourceNode.NodeHash = 1u;
            sourceNode.Flags = PowerGridJacobiConstants.NodeFlagActive;
            nodes[0] = sourceNode;

            PowerNodeDTO damagedNode = default;
            damagedNode.NodeHash = 2u;
            damagedNode.Flags = PowerGridJacobiConstants.NodeFlagActive | PowerGridJacobiConstants.NodeFlagDamaged;
            nodes[1] = damagedNode;

            PowerGridEdgeDTO edge = default;
            edge.SourceNodeHash = 1u;
            edge.DestinationNodeHash = 2u;
            edge.SourceNodeIndex = 0;
            edge.DestinationNodeIndex = 1;
            edge.Conductance = 0.75f;
            edge.Capacity = 100f;
            edges[0] = edge;

            new BuildCsrPowerGraphJob
            {
                Nodes = nodes,
                FlatEdges = edges,
                NodeEdgeOffsets = offsets,
                EdgeWriteCursor = cursors,
                EdgeDestinations = destinations,
                EdgeConductance = conductance,
                EdgeCurrentFlow = flow,
                NodeCount = 2,
                EdgeCount = 1
            }.Execute();

            Assert.AreEqual(2, offsets[2]);
            Assert.AreEqual(0f, conductance[0]);
            Assert.AreEqual(0f, conductance[1]);
        }
        finally
        {
            if (nodes.IsCreated) nodes.Dispose();
            if (edges.IsCreated) edges.Dispose();
            if (offsets.IsCreated) offsets.Dispose();
            if (cursors.IsCreated) cursors.Dispose();
            if (destinations.IsCreated) destinations.Dispose();
            if (conductance.IsCreated) conductance.Dispose();
            if (flow.IsCreated) flow.Dispose();
        }
    }

    [Test]
    public void BuildCsrPowerGraph_SparkingContactLeaksMinimumConductance()
    {
        NativeArray<PowerNodeDTO> nodes = new NativeArray<PowerNodeDTO>(2, Allocator.TempJob);
        NativeArray<PowerGridEdgeDTO> edges = new NativeArray<PowerGridEdgeDTO>(1, Allocator.TempJob);
        NativeArray<int> offsets = new NativeArray<int>(3, Allocator.TempJob);
        NativeArray<int> cursors = new NativeArray<int>(2, Allocator.TempJob);
        NativeArray<int> destinations = new NativeArray<int>(2, Allocator.TempJob);
        NativeArray<float> conductance = new NativeArray<float>(2, Allocator.TempJob);
        NativeArray<float> flow = new NativeArray<float>(2, Allocator.TempJob);
        try
        {
            PowerNodeDTO sourceNode = default;
            sourceNode.NodeHash = 1u;
            sourceNode.Flags = PowerGridJacobiConstants.NodeFlagActive;
            nodes[0] = sourceNode;

            PowerNodeDTO destinationNode = default;
            destinationNode.NodeHash = 2u;
            destinationNode.Flags = PowerGridJacobiConstants.NodeFlagActive;
            nodes[1] = destinationNode;

            PowerGridEdgeDTO edge = default;
            edge.SourceNodeHash = 1u;
            edge.DestinationNodeHash = 2u;
            edge.SourceNodeIndex = 0;
            edge.DestinationNodeIndex = 1;
            edge.Conductance = 0.75f;
            edge.Capacity = 100f;
            edge.Flags = PowerGridJacobiConstants.EdgeFlagDamaged | PowerGridJacobiConstants.EdgeFlagSparking;
            edges[0] = edge;

            new BuildCsrPowerGraphJob
            {
                Nodes = nodes,
                FlatEdges = edges,
                NodeEdgeOffsets = offsets,
                EdgeWriteCursor = cursors,
                EdgeDestinations = destinations,
                EdgeConductance = conductance,
                EdgeCurrentFlow = flow,
                NodeCount = 2,
                EdgeCount = 1
            }.Execute();

            Assert.AreEqual(2, offsets[2]);
            Assert.AreEqual(PowerGridJacobiConstants.SparkLeakConductance, conductance[0], 0.0000001f);
            Assert.AreEqual(PowerGridJacobiConstants.SparkLeakConductance, conductance[1], 0.0000001f);
        }
        finally
        {
            if (nodes.IsCreated) nodes.Dispose();
            if (edges.IsCreated) edges.Dispose();
            if (offsets.IsCreated) offsets.Dispose();
            if (cursors.IsCreated) cursors.Dispose();
            if (destinations.IsCreated) destinations.Dispose();
            if (conductance.IsCreated) conductance.Dispose();
            if (flow.IsCreated) flow.Dispose();
        }
    }

    [Test]
    public void BuildCsrPowerGraph_TruncatedAdjacencyDoesNotOverwriteAcceptedSlots()
    {
        NativeArray<PowerNodeDTO> nodes = new NativeArray<PowerNodeDTO>(3, Allocator.TempJob);
        NativeArray<PowerGridEdgeDTO> edges = new NativeArray<PowerGridEdgeDTO>(2, Allocator.TempJob);
        NativeArray<int> offsets = new NativeArray<int>(4, Allocator.TempJob);
        NativeArray<int> cursors = new NativeArray<int>(3, Allocator.TempJob);
        NativeArray<int> destinations = new NativeArray<int>(2, Allocator.TempJob);
        NativeArray<float> conductance = new NativeArray<float>(2, Allocator.TempJob);
        NativeArray<float> flow = new NativeArray<float>(2, Allocator.TempJob);
        try
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                PowerNodeDTO node = default;
                node.NodeHash = (uint)(i + 1);
                node.Flags = PowerGridJacobiConstants.NodeFlagActive;
                nodes[i] = node;
            }

            PowerGridEdgeDTO firstEdge = default;
            firstEdge.SourceNodeHash = 1u;
            firstEdge.DestinationNodeHash = 2u;
            firstEdge.SourceNodeIndex = 0;
            firstEdge.DestinationNodeIndex = 1;
            firstEdge.Conductance = 0.5f;
            edges[0] = firstEdge;

            PowerGridEdgeDTO overflowingEdge = default;
            overflowingEdge.SourceNodeHash = 3u;
            overflowingEdge.DestinationNodeHash = 1u;
            overflowingEdge.SourceNodeIndex = 2;
            overflowingEdge.DestinationNodeIndex = 0;
            overflowingEdge.Conductance = 0.25f;
            edges[1] = overflowingEdge;

            new BuildCsrPowerGraphJob
            {
                Nodes = nodes,
                FlatEdges = edges,
                NodeEdgeOffsets = offsets,
                EdgeWriteCursor = cursors,
                EdgeDestinations = destinations,
                EdgeConductance = conductance,
                EdgeCurrentFlow = flow,
                NodeCount = 3,
                EdgeCount = 2
            }.Execute();

            Assert.AreEqual(2, offsets[3]);
            Assert.AreEqual(1, destinations[0]);
            Assert.AreEqual(0, destinations[1]);
        }
        finally
        {
            if (nodes.IsCreated) nodes.Dispose();
            if (edges.IsCreated) edges.Dispose();
            if (offsets.IsCreated) offsets.Dispose();
            if (cursors.IsCreated) cursors.Dispose();
            if (destinations.IsCreated) destinations.Dispose();
            if (conductance.IsCreated) conductance.Dispose();
            if (flow.IsCreated) flow.Dispose();
        }
    }

    [Test]
    public void GenerateMockPowerNetwork_RejectsMissingAupLaneWithoutWrites()
    {
        NativeArray<PowerNodeDTO> nodes = new NativeArray<PowerNodeDTO>(4, Allocator.TempJob);
        NativeArray<PowerGridEdgeDTO> edges = new NativeArray<PowerGridEdgeDTO>(4, Allocator.TempJob);
        NativeArray<int> counts = new NativeArray<int>(2, Allocator.TempJob);
        try
        {
            new GenerateMockPowerNetworkJob
            {
                Nodes = nodes,
                Edges = edges,
                NodeAup = default,
                Counts = counts,
                BaseOriginAup = default,
                RequestedNodeCount = 4,
                RequestedEdgeCount = 4
            }.Execute();

            Assert.AreEqual(0, counts[0]);
            Assert.AreEqual(0, counts[1]);
            Assert.AreEqual(0u, nodes[0].NodeHash);
        }
        finally
        {
            if (nodes.IsCreated) nodes.Dispose();
            if (edges.IsCreated) edges.Dispose();
            if (counts.IsCreated) counts.Dispose();
        }
    }

    [Test]
    public unsafe void PowerVoltageSolver_CascadeShedMarksBrownoutAndRecovers()
    {
        NativeArray<PowerNodeDTO> nodes = new NativeArray<PowerNodeDTO>(2, Allocator.TempJob);
        NativeArray<int> offsets = new NativeArray<int>(3, Allocator.TempJob);
        NativeArray<int> destinations = new NativeArray<int>(2, Allocator.TempJob);
        NativeArray<float> conductance = new NativeArray<float>(2, Allocator.TempJob);
        NativeArray<float> front = new NativeArray<float>(2, Allocator.TempJob);
        NativeArray<float> demand = new NativeArray<float>(2, Allocator.TempJob);
        NativeArray<float> back = new NativeArray<float>(2, Allocator.TempJob);
        try
        {
            PowerNodeDTO source = default;
            source.NodeHash = 1u;
            source.Flags = PowerGridJacobiConstants.NodeFlagActive | PowerGridJacobiConstants.NodeFlagSource;
            source.Potential = 1f;
            nodes[0] = source;

            PowerNodeDTO consumer = default;
            consumer.NodeHash = 2u;
            consumer.Flags = PowerGridJacobiConstants.NodeFlagActive;
            consumer.Potential = 0.05f;
            nodes[1] = consumer;

            offsets[0] = 0;
            offsets[1] = 1;
            offsets[2] = 2;
            destinations[0] = 1;
            destinations[1] = 0;
            front[0] = 1f;
            front[1] = 0.05f;
            demand[1] = 1f;

            PowerVoltageSolverJob shedJob = new PowerVoltageSolverJob
            {
                NodesPtr = (PowerNodeDTO*)nodes.GetUnsafePtr(),
                NodeEdgeOffsets = offsets,
                EdgeDestinations = destinations,
                EdgeConductance = conductance,
                FrontPotential = front,
                DemandRate = demand,
                BackPotential = back,
                NodeCount = 2,
                GlobalQualityWeight = 1f,
                SmoothingFactor = 1f
            };
            shedJob.Execute(1);

            PowerNodeDTO shedNode = nodes[1];
            Assert.AreEqual(0f, shedNode.Potential);
            Assert.AreNotEqual(0u, shedNode.Flags & PowerGridJacobiConstants.NodeFlagBrownout);
            Assert.AreNotEqual(0u, shedNode.Flags & PowerGridJacobiConstants.NodeFlagCascadeShed);
            Assert.AreEqual(0f, back[1]);

            front[1] = 0.5f;
            conductance[1] = 10f;
            demand[1] = 0f;
            shedJob.Execute(1);

            PowerNodeDTO recoveredNode = nodes[1];
            Assert.Greater(recoveredNode.Potential, PowerGridJacobiConstants.BrownoutThreshold01);
            Assert.AreEqual(0u, recoveredNode.Flags & PowerGridJacobiConstants.NodeFlagCascadeShed);
            Assert.AreEqual(0u, recoveredNode.Flags & PowerGridJacobiConstants.NodeFlagBrownout);
        }
        finally
        {
            if (nodes.IsCreated) nodes.Dispose();
            if (offsets.IsCreated) offsets.Dispose();
            if (destinations.IsCreated) destinations.Dispose();
            if (conductance.IsCreated) conductance.Dispose();
            if (front.IsCreated) front.Dispose();
            if (demand.IsCreated) demand.Dispose();
            if (back.IsCreated) back.Dispose();
        }
    }

    [Test]
    public void RecordPowerTelemetryJob_WritesGenerationLoadPotentialAndCursor()
    {
        NativeArray<PowerNodeDTO> nodes = new NativeArray<PowerNodeDTO>(2, Allocator.TempJob);
        NativeArray<float> demand = new NativeArray<float>(2, Allocator.TempJob);
        NativeArray<PowerTelemetryEntry> telemetry = new NativeArray<PowerTelemetryEntry>(PowerGridJacobiConstants.TelemetryFrameCount, Allocator.TempJob);
        NativeArray<PowerGridCounter64> cursor = new NativeArray<PowerGridCounter64>(1, Allocator.TempJob);
        try
        {
            PowerNodeDTO source = default;
            source.NodeHash = 11u;
            source.Flags = PowerGridJacobiConstants.NodeFlagActive | PowerGridJacobiConstants.NodeFlagSource;
            source.Potential = 0.75f;
            source.MaxCapacity = 100f;
            nodes[0] = source;

            PowerNodeDTO consumer = default;
            consumer.NodeHash = 22u;
            consumer.Flags = PowerGridJacobiConstants.NodeFlagActive;
            consumer.Potential = 0.10f;
            consumer.MaxCapacity = 0f;
            nodes[1] = consumer;

            demand[0] = 0.25f;
            demand[1] = 0.50f;

            PowerGridCounter64 initialCursor = default;
            initialCursor.Value = 0;
            cursor[0] = initialCursor;

            new RecordPowerTelemetryJob
            {
                Nodes = nodes,
                DemandRate = demand,
                TelemetryRing = telemetry,
                TelemetryCursor = cursor,
                FrameIndex = 42u,
                ReasonFlags = 0u,
                NodeCount = 2,
                EdgeCount = 4,
                RuntimeEdgeCount = 3,
                SolveStartNode = 0,
                SolveNodeCount = 2,
                SolverMicroseconds = 77
            }.Execute();

            PowerTelemetryEntry entry = telemetry[0];
            Assert.AreEqual(42u, entry.FrameIndex);
            Assert.AreEqual(2, entry.NodeCount);
            Assert.AreEqual(4, entry.EdgeCount);
            Assert.AreEqual(3, entry.RuntimeEdgeCount);
            Assert.AreEqual(75f, entry.TotalGeneration, 0.0001f);
            Assert.AreEqual(0.75f, entry.TotalLoad, 0.0001f);
            Assert.AreEqual(1f, entry.SupplyRatio, 0.0001f);
            Assert.AreEqual(0.425f, entry.AveragePotential, 0.0001f);
            Assert.AreEqual(0.10f, entry.MinPotential, 0.0001f);
            Assert.AreEqual(0.75f, entry.MaxPotential, 0.0001f);
            Assert.AreEqual(1, entry.BrownoutCount);
            Assert.AreEqual(77, entry.SolverMicroseconds);
            Assert.AreNotEqual(0u, entry.ReasonFlags & PowerGridJacobiConstants.TelemetryReasonBrownout);
            Assert.AreEqual(1, cursor[0].Value);
            Assert.AreNotEqual(0u, cursor[0].Flags & PowerGridJacobiConstants.TelemetryReasonBrownout);
        }
        finally
        {
            if (nodes.IsCreated) nodes.Dispose();
            if (demand.IsCreated) demand.Dispose();
            if (telemetry.IsCreated) telemetry.Dispose();
            if (cursor.IsCreated) cursor.Dispose();
        }
    }

    [Test]
    public void AupMath_SubtractsBaseOriginBeforeFloatCast()
    {
        double3 baseOrigin = new double3(100000.0, -2000.0, 75000.0);
        double3 nodeAup = new double3(100012.5, -1998.0, 74996.25);

        float3 local = PowerGridAupMath.ToBaseLocalFloat3(nodeAup, baseOrigin);

        Assert.AreEqual(12.5f, local.x);
        Assert.AreEqual(2f, local.y);
        Assert.AreEqual(-3.75f, local.z);
    }


    [Test]
    public void AupMath_DistanceMeters_CalculatesCorrectDistance()
    {
        double3 baseOrigin = new double3(100000.0, -2000.0, 75000.0);
        double3 aupA = new double3(100010.0, -2000.0, 75000.0);
        double3 aupB = new double3(100000.0, -2000.0, 75010.0);

        float distance = PowerGridAupMath.DistanceMeters(aupA, aupB, baseOrigin);

        Assert.AreEqual(14.1421356f, distance, 0.001f);
    }

    [Test]
    public void AupMath_DistanceMeters_ReturnsZeroForMicroscopicDistances()
    {
        double3 baseOrigin = new double3(100000.0, -2000.0, 75000.0);
        double3 aupA = new double3(100010.0, -2000.0, 75000.0);
        double3 aupB = new double3(100010.0001, -2000.0, 75000.0);

        float distance = PowerGridAupMath.DistanceMeters(aupA, aupB, baseOrigin);

        Assert.AreEqual(0f, distance);
    }

    private static int OffsetOf<T>(string fieldName) where T : struct
    {
        var field = typeof(T).GetField(fieldName);
        return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
    }
}
