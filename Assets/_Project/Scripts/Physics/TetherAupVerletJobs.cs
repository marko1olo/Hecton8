using System.IO;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    internal static class TetherAupRuntimeConstants
    {
        public const int MockTetherCount = 5;
        public const int MockNodesPerTether = 30;
        public const int MockNodeCapacity = MockTetherCount * MockNodesPerTether;
        public const int MockConstraintCapacity = MockTetherCount * (MockNodesPerTether - 1);
        public const int MockForcePacketCapacity = MockConstraintCapacity * 2;
        public const int MockSplineVertexCapacity = MockTetherCount * MockNodesPerTether;
        public const int SolverStatsCapacity = 4;
        public const int MaterialCapacity = 16;
        public const int TelemetryCapacity = 300;
        public const int BootstrapMagic = 0x53483135;
        public const float SafeLocalAupSpanMeters = 32768f;
    }

    internal static class TetherNodeRuntimeFlags
    {
        public const uint Pinned = 1u << 0;
        public const uint NonFiniteRecovered = 1u << 1;
        public const uint ConstraintFault = 1u << 2;
    }

    internal static class TetherForcePacketFlags
    {
        public const uint EndpointAnchor = 1u << 0;
        public const uint EndpointPayload = 1u << 1;
    }

    public static class TetherAupSolverScheduler
    {
        public static int ResolveIterationCount(float globalQualityWeight)
        {
            float q = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            int iterations = (int)math.lerp(2f, 15f, q);
            return math.clamp(iterations, 2, 15);
        }

        public static JobHandle Schedule(
            NativeArray<TetherNodeDTO> nodes,
            NativeArray<TetherConstraintDTO> constraints,
            NativeArray<float> segmentTensions,
            NativeArray<float> solverStats,
            NativeArray<TetherForcePacketDTO> forcePackets,
            NativeArray<TetherAupTelemetryEntry> telemetryRing,
            NativeArray<int> telemetryHead,
            NativeArray<double3> pinnedAups,
            NativeArray<byte> pinnedMask,
            double3 anchorAup,
            int activeNodeCount,
            int activeConstraintCount,
            uint frameIndex,
            float simulationTickDelta,
            float3 gravityAcceleration,
            float3 externalAcceleration,
            float3 abyssalCurrentAcceleration,
            float velocityDamping,
            float maxStepMeters,
            float tensionScale,
            float cpuMicroseconds,
            float globalQualityWeight,
            JobHandle inputDependency)
        {
            if (!nodes.IsCreated || nodes.Length <= 0)
                return inputDependency;

            int nodeCount = math.clamp(activeNodeCount, 0, nodes.Length);
            if (nodeCount <= 0)
                return inputDependency;

            int iterations = ResolveIterationCount(globalQualityWeight);
            JobHandle integrateHandle = new IntegrateTetherNodesJob
            {
                Nodes = nodes,
                PinnedAUPs = pinnedAups,
                PinnedMask = pinnedMask,
                GravityAcceleration = gravityAcceleration,
                ExternalAcceleration = externalAcceleration,
                AbyssalCurrentAcceleration = abyssalCurrentAcceleration,
                SimulationTickDelta = simulationTickDelta,
                VelocityDamping = velocityDamping,
                MaxStepMeters = maxStepMeters,
                GlobalQualityWeight = globalQualityWeight
            }.Schedule(nodeCount, 32, inputDependency);

            JobHandle solveHandle = new SolveTetherConstraintsJob
            {
                Nodes = nodes,
                Constraints = constraints,
                SegmentTensions = segmentTensions,
                SolverStats = solverStats,
                ForcePackets = forcePackets,
                ActiveConstraintCount = activeConstraintCount,
                IterationCount = iterations,
                TensionScale = tensionScale,
                FrameIndex = frameIndex
            }.Schedule(integrateHandle);

            if (!telemetryRing.IsCreated || !telemetryHead.IsCreated)
                return solveHandle;

            return new RecordTetherAupTelemetryJob
            {
                Nodes = nodes,
                SolverStats = solverStats,
                TelemetryRing = telemetryRing,
                TelemetryHead = telemetryHead,
                AnchorAUP = anchorAup,
                NodeOffset = 0,
                NodeCount = nodeCount,
                IterationCount = iterations,
                FrameIndex = frameIndex,
                Flags = 0u,
                CpuMicroseconds = cpuMicroseconds,
                GlobalQualityWeight = globalQualityWeight
            }.Schedule(solveHandle);
        }

        public static JobHandle ScheduleMock(
            NativeArray<TetherNodeDTO> nodes,
            NativeArray<TetherConstraintDTO> constraints,
            NativeArray<TetherEndpointAupDTO> endpoints,
            NativeArray<float> segmentTensions,
            NativeArray<float> solverStats,
            NativeArray<TetherForcePacketDTO> forcePackets,
            NativeArray<TetherAupTelemetryEntry> telemetryRing,
            NativeArray<int> telemetryHead,
            NativeArray<double3> pinnedAups,
            NativeArray<byte> pinnedMask,
            uint frameIndex,
            uint sectorHash,
            float simulationTickDelta,
            float3 gravityAcceleration,
            float3 externalAcceleration,
            float velocityDamping,
            float maxStepMeters,
            float tensionScale,
            float cpuMicroseconds,
            float globalQualityWeight,
            JobHandle inputDependency)
        {
            JobHandle endpointsHandle = new AdvanceMockTetherEndpointsJob
            {
                Nodes = nodes,
                Endpoints = endpoints,
                PinnedAUPs = pinnedAups,
                PinnedMask = pinnedMask,
                FrameIndex = frameIndex,
                SectorHash = sectorHash,
                GlobalQualityWeight = globalQualityWeight
            }.Schedule(inputDependency);

            float q = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            float phase = frameIndex * math.lerp(0.017f, 0.047f, q);
            float3 currentAcceleration = new float3(
                math.sin(phase) * 0.06f,
                -0.015f,
                math.cos(phase * 0.73f) * 0.04f) * math.lerp(0.3f, 1f, q);
            return Schedule(
                nodes,
                constraints,
                segmentTensions,
                solverStats,
                forcePackets,
                telemetryRing,
                telemetryHead,
                pinnedAups,
                pinnedMask,
                double3.zero,
                TetherAupRuntimeConstants.MockNodeCapacity,
                TetherAupRuntimeConstants.MockConstraintCapacity,
                frameIndex,
                simulationTickDelta,
                gravityAcceleration,
                externalAcceleration,
                currentAcceleration,
                velocityDamping,
                maxStepMeters,
                tensionScale,
                cpuMicroseconds,
                globalQualityWeight,
                endpointsHandle);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct InitializeMockTetherAupJob : IJob
    {
        [NoAlias] public NativeArray<TetherNodeDTO> Nodes;
        [NoAlias] public NativeArray<TetherConstraintDTO> Constraints;
        [NoAlias] public NativeArray<TetherEndpointAupDTO> Endpoints;
        [NoAlias] public NativeArray<CableMaterialDTO> Materials;
        [NoAlias] public NativeArray<int> BootstrapState;
        [NoAlias] public NativeArray<double3> PinnedAUPs;
        [NoAlias] public NativeArray<byte> PinnedMask;

        public uint FrameIndex;
        public uint SectorHash;
        public float GlobalQualityWeight;

        public void Execute()
        {
            int tetherCount = math.min(TetherAupRuntimeConstants.MockTetherCount, Endpoints.IsCreated ? Endpoints.Length : 0);
            int nodesPerTether = TetherAupRuntimeConstants.MockNodesPerTether;
            int constraintsPerTether = nodesPerTether - 1;
            float q = Sanitize01(GlobalQualityWeight);

            for (int cable = 0; cable < tetherCount; cable++)
            {
                double3 anchor = new double3(cable * 3.0, -22.0, cable * 1.5);
                double3 payload = anchor + new double3(10.0 + cable * 0.75, -1.25, 5.5);
                float3 current = new float3(
                    0.08f * (cable + 1),
                    -0.015f * (1f + cable),
                    0.04f * (1f + q));

                Endpoints[cable] = new TetherEndpointAupDTO
                {
                    AnchorAUP = anchor,
                    PayloadAUP = payload,
                    AbyssalCurrentAcceleration = current,
                    GlobalQualityWeight = q
                };

                int nodeOffset = cable * nodesPerTether;
                for (int node = 0; node < nodesPerTether; node++)
                {
                    float t = node * math.rcp(math.max(1, nodesPerTether - 1));
                    float wave = math.sin((cable + 1) * 1.713f + t * 6.2831855f) * math.lerp(0.05f, 0.35f, q);
                    float sag = -math.sin(t * 3.1415927f) * math.lerp(0.1f, 0.7f, q);
                    double3 aup = math.lerp(anchor, payload, (double)t) + new double3(0.0, sag, wave);
                    Nodes[nodeOffset + node] = new TetherNodeDTO
                    {
                        CurrentAUP = aup,
                        PreviousAUP = aup - new double3(
                            current.x * 0.0004d,
                            current.y * 0.0004d,
                            current.z * 0.0004d),
                        InverseMass = node == 0 || node == nodesPerTether - 1 ? 0f : 1f,
                        Flags = node == 0 || node == nodesPerTether - 1 ? TetherNodeRuntimeFlags.Pinned : 0u
                    };

                    if (PinnedAUPs.IsCreated && nodeOffset + node < PinnedAUPs.Length)
                        PinnedAUPs[nodeOffset + node] = aup;
                    if (PinnedMask.IsCreated && nodeOffset + node < PinnedMask.Length)
                        PinnedMask[nodeOffset + node] = (byte)(node == 0 || node == nodesPerTether - 1 ? 1 : 0);
                }

                int constraintOffset = cable * constraintsPerTether;
                float restLength = (float)math.length(payload - anchor) * math.rcp(math.max(1, constraintsPerTether));
                for (int segment = 0; segment < constraintsPerTether; segment++)
                {
                    Constraints[constraintOffset + segment] = new TetherConstraintDTO
                    {
                        NodeA = nodeOffset + segment,
                        NodeB = nodeOffset + segment + 1,
                        RestLength = math.max(VerletCableLayout.MinConstraintLength, restLength),
                        Stiffness = math.lerp(0.55f, 0.98f, q),
                        Flags = 0u,
                        CableId = (uint)cable
                    };
                }
            }

            if (Materials.IsCreated)
                CableMaterialDTO.GenerateEmergencyMockCables(Materials);

            if (BootstrapState.IsCreated && BootstrapState.Length > 0)
                BootstrapState[0] = TetherAupRuntimeConstants.BootstrapMagic;
        }

        private static float Sanitize01(float value)
        {
            return math.saturate(math.isfinite(value) ? value : 1f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct AdvanceMockTetherEndpointsJob : IJob
    {
        [NoAlias] public NativeArray<TetherNodeDTO> Nodes;
        [NoAlias] public NativeArray<TetherEndpointAupDTO> Endpoints;
        [NoAlias] public NativeArray<double3> PinnedAUPs;
        [NoAlias] public NativeArray<byte> PinnedMask;

        public uint FrameIndex;
        public uint SectorHash;
        public float GlobalQualityWeight;

        public void Execute()
        {
            int tetherCount = math.min(TetherAupRuntimeConstants.MockTetherCount, Endpoints.IsCreated ? Endpoints.Length : 0);
            int nodesPerTether = TetherAupRuntimeConstants.MockNodesPerTether;
            float q = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 1f);
            float frame = FrameIndex * math.lerp(0.021f, 0.061f, q);
            uint seed = math.max(1u, (SectorHash ^ (FrameIndex * 747796405u)) | 1u);
            Unity.Mathematics.Random rng = new Unity.Mathematics.Random(seed);

            for (int cable = 0; cable < tetherCount; cable++)
            {
                float randomPhase = rng.NextFloat(0.0f, 6.2831855f);
                float phase = frame + cable * 1.719f + randomPhase * 0.03125f;
                double3 baseAnchor = new double3(cable * 3.0, -22.0, cable * 1.5);
                double3 anchor = baseAnchor + new double3(
                    math.sin(phase) * 0.35f,
                    math.sin(phase * 0.7f) * 0.08f,
                    math.cos(phase * 0.83f) * 0.22f);
                double3 payload = baseAnchor + new double3(
                    10.0 + cable * 0.75 + math.sin(phase * 0.47f) * 0.8f,
                    -1.25 + math.cos(phase * 0.53f) * 0.18f,
                    5.5 + math.sin(phase * 0.41f) * 0.55f);
                float3 current = new float3(
                    math.sin(phase * 0.37f) * 0.08f,
                    -0.015f * (1f + cable),
                    math.cos(phase * 0.43f) * 0.06f) * math.lerp(0.3f, 1f, q);

                Endpoints[cable] = new TetherEndpointAupDTO
                {
                    AnchorAUP = anchor,
                    PayloadAUP = payload,
                    AbyssalCurrentAcceleration = current,
                    GlobalQualityWeight = q
                };

                int nodeOffset = cable * nodesPerTether;
                PinNode(nodeOffset, anchor);
                PinNode(nodeOffset + nodesPerTether - 1, payload);
            }
        }

        private void PinNode(int nodeIndex, double3 aup)
        {
            if (PinnedAUPs.IsCreated && (uint)nodeIndex < (uint)PinnedAUPs.Length)
                PinnedAUPs[nodeIndex] = aup;
            if (PinnedMask.IsCreated && (uint)nodeIndex < (uint)PinnedMask.Length)
                PinnedMask[nodeIndex] = 1;
            if (Nodes.IsCreated && (uint)nodeIndex < (uint)Nodes.Length)
            {
                TetherNodeDTO node = Nodes[nodeIndex];
                node.CurrentAUP = aup;
                node.PreviousAUP = aup;
                node.InverseMass = 0f;
                node.Flags |= TetherNodeRuntimeFlags.Pinned;
                Nodes[nodeIndex] = node;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct IntegrateTetherNodesJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<TetherNodeDTO> Nodes;

        [ReadOnly, NoAlias] public NativeArray<double3> PinnedAUPs;
        [ReadOnly, NoAlias] public NativeArray<byte> PinnedMask;

        public float3 GravityAcceleration;
        public float3 ExternalAcceleration;
        public float3 AbyssalCurrentAcceleration;
        public float SimulationTickDelta;
        public float VelocityDamping;
        public float MaxStepMeters;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Nodes.Length)
                return;

            TetherNodeDTO node = Nodes[index];
            bool pinned = node.InverseMass <= 0f ||
                          (node.Flags & TetherNodeRuntimeFlags.Pinned) != 0u ||
                          (PinnedMask.IsCreated && index < PinnedMask.Length && PinnedMask[index] != 0);
            if (pinned)
            {
                double3 pinnedAup = PinnedAUPs.IsCreated && index < PinnedAUPs.Length
                    ? SanitizeAup(PinnedAUPs[index], node.CurrentAUP)
                    : SanitizeAup(node.CurrentAUP, double3.zero);
                node.CurrentAUP = pinnedAup;
                node.PreviousAUP = pinnedAup;
                node.InverseMass = 0f;
                node.Flags |= TetherNodeRuntimeFlags.Pinned;
                Nodes[index] = node;
                return;
            }

            double3 current = SanitizeAup(node.CurrentAUP, node.PreviousAUP);
            double3 previous = SanitizeAup(node.PreviousAUP, current);
            uint flags = node.Flags & ~TetherNodeRuntimeFlags.NonFiniteRecovered;
            if (!IsFinite(node.CurrentAUP) || !IsFinite(node.PreviousAUP))
                flags |= TetherNodeRuntimeFlags.NonFiniteRecovered;

            float3 velocity = LocalDeltaToFloat3(current - previous, ref flags) * SanitizeNonNegative(VelocityDamping, 0.985f);
            float maxStep = SanitizeNonNegative(MaxStepMeters, 0f);
            float velocitySq = math.lengthsq(velocity);
            if (maxStep > 0f && math.isfinite(velocitySq) && velocitySq > maxStep * maxStep)
                velocity *= maxStep * math.rsqrt(math.max(velocitySq, 0.000001f));

            float q = Sanitize01(GlobalQualityWeight);
            float currentWeight = math.lerp(0.15f, 1f, q * q);
            float3 acceleration = SanitizeFloat3(GravityAcceleration, float3.zero) +
                                  SanitizeFloat3(ExternalAcceleration, float3.zero) +
                                  SanitizeFloat3(AbyssalCurrentAcceleration, float3.zero) * currentWeight;
            float dt = SanitizeNonNegative(SimulationTickDelta, 0f);
            double3 next = current + new double3(velocity + acceleration * (dt * dt));
            if (!IsFinite(next))
            {
                next = current;
                flags |= TetherNodeRuntimeFlags.NonFiniteRecovered;
            }

            node.PreviousAUP = current;
            node.CurrentAUP = next;
            node.InverseMass = math.isfinite(node.InverseMass) ? math.max(0f, node.InverseMass) : 0f;
            node.Flags = flags;
            Nodes[index] = node;
        }

        private static float3 LocalDeltaToFloat3(double3 delta, ref uint flags)
        {
            if (!IsFinite(delta))
            {
                flags |= TetherNodeRuntimeFlags.NonFiniteRecovered;
                return float3.zero;
            }

            double span = TetherAupRuntimeConstants.SafeLocalAupSpanMeters;
            double3 clamped = math.clamp(delta, new double3(-span), new double3(span));
            float3 local = new float3((float)clamped.x, (float)clamped.y, (float)clamped.z);
            if (math.all(math.isfinite(local)))
                return local;

            flags |= TetherNodeRuntimeFlags.NonFiniteRecovered;
            return float3.zero;
        }

        private static double3 SanitizeAup(double3 value, double3 fallback)
        {
            return IsFinite(value) ? value : (IsFinite(fallback) ? fallback : double3.zero);
        }

        private static bool IsFinite(double3 value)
        {
            return math.all(math.isfinite(value));
        }

        private static float Sanitize01(float value)
        {
            return math.saturate(math.isfinite(value) ? value : 1f);
        }

        private static float SanitizeNonNegative(float value, float fallback)
        {
            return math.isfinite(value) ? math.max(0f, value) : fallback;
        }

        private static float3 SanitizeFloat3(float3 value, float3 fallback)
        {
            return math.all(math.isfinite(value)) ? value : fallback;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct SolveTetherConstraintsJob : IJob
    {
        [NoAlias] public NativeArray<TetherNodeDTO> Nodes;
        [NoAlias] public NativeArray<TetherConstraintDTO> Constraints;
        [NoAlias] public NativeArray<float> SegmentTensions;
        [NoAlias] public NativeArray<float> SolverStats;
        [NoAlias] public NativeArray<TetherForcePacketDTO> ForcePackets;

        public int ActiveConstraintCount;
        public int IterationCount;
        public float TensionScale;
        public uint FrameIndex;

        public void Execute()
        {
            int constraintCount = math.clamp(ActiveConstraintCount, 0, Constraints.IsCreated ? Constraints.Length : 0);
            int iterations = math.clamp(IterationCount, 1, 15);
            float peakTension = 0f;
            float maxError = 0f;
            int faultFlags = 0;

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                for (int i = 0; i < constraintCount; i++)
                {
                    TetherConstraintDTO constraint = Constraints[i];
                    if (constraint.Stiffness <= 0f ||
                        (uint)constraint.NodeA >= (uint)Nodes.Length ||
                        (uint)constraint.NodeB >= (uint)Nodes.Length)
                    {
                        WriteTension(i, 0f);
                        ClearEndpointForces(i);
                        continue;
                    }

                    TetherNodeDTO a = Nodes[constraint.NodeA];
                    TetherNodeDTO b = Nodes[constraint.NodeB];
                    uint nodeFlags = 0u;
                    float3 delta = LocalDeltaToFloat3(b.CurrentAUP - a.CurrentAUP, ref nodeFlags);
                    float lenSq = math.lengthsq(delta);
                    if (!math.isfinite(lenSq) || lenSq <= VerletCableLayout.MinConstraintLengthSq)
                    {
                        faultFlags |= (int)TetherNodeRuntimeFlags.ConstraintFault;
                        WriteTension(i, 0f);
                        ClearEndpointForces(i);
                        continue;
                    }

                    float invLen = math.rsqrt(math.max(lenSq, VerletCableLayout.MinConstraintLengthSq));
                    float distance = lenSq * invLen;
                    float restLength = math.max(VerletCableLayout.MinConstraintLength, constraint.RestLength);
                    float error = distance - restLength;
                    float stretch = math.max(0f, error);
                    float stiffness = math.saturate(constraint.Stiffness);
                    float tension = stretch * stiffness * math.max(0f, TensionScale);
                    peakTension = math.max(peakTension, tension);
                    maxError = math.max(maxError, math.abs(error));
                    WriteTension(i, tension);

                    float3 direction = delta * invLen;
                    WriteEndpointForces(i, constraint, a.CurrentAUP, b.CurrentAUP, direction, tension);
                    if (math.abs(error) <= VerletCableLayout.MinConstraintLength)
                        continue;

                    float invMassA = math.max(0f, a.InverseMass);
                    float invMassB = math.max(0f, b.InverseMass);
                    float invMassSum = invMassA + invMassB;
                    if (invMassSum <= 0.000001f)
                        continue;

                    float3 correction = direction * (error * stiffness);
                    if (invMassA > 0f)
                    {
                        float3 weightedCorrection = correction * (invMassA * math.rcp(math.max(invMassSum, 0.000001f)));
                        a.CurrentAUP += new double3(weightedCorrection.x, weightedCorrection.y, weightedCorrection.z);
                        a.Flags |= nodeFlags;
                        Nodes[constraint.NodeA] = a;
                    }

                    if (invMassB > 0f)
                    {
                        float3 weightedCorrection = correction * (invMassB * math.rcp(math.max(invMassSum, 0.000001f)));
                        b.CurrentAUP -= new double3(weightedCorrection.x, weightedCorrection.y, weightedCorrection.z);
                        b.Flags |= nodeFlags;
                        Nodes[constraint.NodeB] = b;
                    }
                }
            }

            if (SolverStats.IsCreated)
            {
                if (SolverStats.Length > 0)
                    SolverStats[0] = math.isfinite(peakTension) ? math.max(0f, peakTension) : 0f;
                if (SolverStats.Length > 1)
                    SolverStats[1] = math.isfinite(maxError) ? math.max(0f, maxError) : 0f;
                if (SolverStats.Length > 2)
                    SolverStats[2] = faultFlags;
            }
        }

        private void WriteTension(int index, float tension)
        {
            if (SegmentTensions.IsCreated && index >= 0 && index < SegmentTensions.Length)
                SegmentTensions[index] = math.isfinite(tension) ? math.max(0f, tension) : 0f;
        }

        private void WriteEndpointForces(
            int constraintIndex,
            TetherConstraintDTO constraint,
            double3 aupA,
            double3 aupB,
            float3 direction,
            float tension)
        {
            if (!ForcePackets.IsCreated)
                return;

            int first = constraintIndex * 2;
            int second = first + 1;
            if (tension <= 0f || !math.isfinite(tension) || !math.all(math.isfinite(direction)))
            {
                if (first < ForcePackets.Length)
                    ForcePackets[first] = default;
                if (second < ForcePackets.Length)
                    ForcePackets[second] = default;
                return;
            }

            if (first < ForcePackets.Length)
            {
                ForcePackets[first] = new TetherForcePacketDTO
                {
                    ApplicationAUP = aupA,
                    Force = direction * tension,
                    Tension = tension,
                    CableId = unchecked((int)constraint.CableId),
                    BodySlot = 0,
                    Flags = TetherForcePacketFlags.EndpointAnchor,
                    FrameIndex = FrameIndex
                };
            }

            if (second < ForcePackets.Length)
            {
                ForcePackets[second] = new TetherForcePacketDTO
                {
                    ApplicationAUP = aupB,
                    Force = -direction * tension,
                    Tension = tension,
                    CableId = unchecked((int)constraint.CableId),
                    BodySlot = 1,
                    Flags = TetherForcePacketFlags.EndpointPayload,
                    FrameIndex = FrameIndex
                };
            }
        }

        private void ClearEndpointForces(int constraintIndex)
        {
            if (!ForcePackets.IsCreated)
                return;

            int first = constraintIndex * 2;
            if (first < ForcePackets.Length)
                ForcePackets[first] = default;
            int second = first + 1;
            if (second < ForcePackets.Length)
                ForcePackets[second] = default;
        }

        private static float3 LocalDeltaToFloat3(double3 delta, ref uint flags)
        {
            if (!math.all(math.isfinite(delta)))
            {
                flags |= TetherNodeRuntimeFlags.ConstraintFault;
                return float3.zero;
            }

            double span = TetherAupRuntimeConstants.SafeLocalAupSpanMeters;
            double3 clamped = math.clamp(delta, new double3(-span), new double3(span));
            float3 local = new float3((float)clamped.x, (float)clamped.y, (float)clamped.z);
            if (math.all(math.isfinite(local)))
                return local;

            flags |= TetherNodeRuntimeFlags.ConstraintFault;
            return float3.zero;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateTetherSplineVerticesJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<TetherNodeDTO> Nodes;
        [ReadOnly, NoAlias] public NativeArray<float> SegmentTensions;
        [NoAlias] public NativeArray<TetherSplineVertexDTO> Vertices;

        public double3 CameraAUP;
        public int NodeOffset;
        public int NodeCount;
        public int VertexOffset;
        public int VertexCount;
        public float InvSnapTension;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)VertexCount)
                return;

            int writeIndex = VertexOffset + index;
            if ((uint)writeIndex >= (uint)Vertices.Length || NodeCount < 2)
                return;

            float q = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 1f);
            float scaled = index * math.rcp(math.max(1, VertexCount - 1)) * (NodeCount - 1);
            int segment = math.clamp((int)math.floor(scaled), 0, NodeCount - 2);
            float t = math.saturate(scaled - segment);
            float3 p1 = ToCameraLocal(NodeOffset + segment);
            float3 p2 = ToCameraLocal(NodeOffset + segment + 1);
            float3 linear = math.lerp(p1, p2, t);
            float catmullWeight = math.step(0.3f, q) * Smooth01(q);
            float3 position = linear;
            if (catmullWeight > 0f)
            {
                float3 p0 = ToCameraLocal(NodeOffset + math.max(0, segment - 1));
                float3 p3 = ToCameraLocal(NodeOffset + math.min(NodeCount - 1, segment + 2));
                float3 catmull = CatmullRom(p0, p1, p2, p3, t);
                position = math.lerp(linear, catmull, catmullWeight);
            }

            float3 tangent = p2 - p1;
            float tangentSq = math.lengthsq(tangent);
            tangent = math.isfinite(tangentSq) && tangentSq > 0.000001f
                ? tangent * math.rsqrt(tangentSq)
                : new float3(0f, 0f, 1f);

            float tension = SegmentTensions.IsCreated && segment < SegmentTensions.Length
                ? SegmentTensions[segment]
                : 0f;
            Vertices[writeIndex] = new TetherSplineVertexDTO
            {
                Position = SanitizeFloat3(position, float3.zero),
                U = index * math.rcp(math.max(1, VertexCount - 1)),
                Tangent = tangent,
                Tension01 = math.saturate(tension * math.max(0f, InvSnapTension))
            };
        }

        private float3 ToCameraLocal(int nodeIndex)
        {
            if ((uint)nodeIndex >= (uint)Nodes.Length)
                return float3.zero;

            double3 delta = Nodes[nodeIndex].CurrentAUP - CameraAUP;
            if (!math.all(math.isfinite(delta)))
                return float3.zero;

            double span = TetherAupRuntimeConstants.SafeLocalAupSpanMeters;
            double3 clamped = math.clamp(delta, new double3(-span), new double3(span));
            return SanitizeFloat3(new float3((float)clamped.x, (float)clamped.y, (float)clamped.z), float3.zero);
        }

        private static float3 CatmullRom(float3 p0, float3 p1, float3 p2, float3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1) +
                           (-p0 + p2) * t +
                           (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                           (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - 2f * x);
        }

        private static float3 SanitizeFloat3(float3 value, float3 fallback)
        {
            return math.all(math.isfinite(value)) ? value : fallback;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct TetherSplineGpuMemcpyJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<TetherSplineVertexDTO> Source;
        [NativeDisableUnsafePtrRestriction] public void* Destination;
        public int Count;
        public long DestinationBytes;

        public void Execute()
        {
            if (!Source.IsCreated || Destination == null)
                return;

            int safeCount = math.clamp(Count, 0, Source.Length);
            long bytes = (long)UnsafeUtility.SizeOf<TetherSplineVertexDTO>() * safeCount;
            if (bytes <= 0L || bytes > DestinationBytes)
                return;

            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Source);
            UnsafeUtility.MemCpy(Destination, sourcePtr, bytes);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct RecordTetherAupTelemetryJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<TetherNodeDTO> Nodes;
        [ReadOnly, NoAlias] public NativeArray<float> SolverStats;
        [NoAlias] public NativeArray<TetherAupTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<int> TelemetryHead;

        public double3 AnchorAUP;
        public int NodeOffset;
        public int NodeCount;
        public int IterationCount;
        public uint FrameIndex;
        public uint Flags;
        public float CpuMicroseconds;
        public float GlobalQualityWeight;

        public void Execute()
        {
            if (!TelemetryRing.IsCreated || !TelemetryHead.IsCreated || TelemetryRing.Length == 0 || TelemetryHead.Length == 0)
                return;

            int capacity = math.min(TetherAupRuntimeConstants.TelemetryCapacity, TelemetryRing.Length);
            int head = TelemetryHead[0];
            if ((uint)head >= (uint)capacity)
                head = 0;

            int availableNodes = Nodes.IsCreated ? math.max(0, Nodes.Length - math.max(0, NodeOffset)) : 0;
            int activeNodes = math.clamp(NodeCount, 0, availableNodes);
            uint hash = 2166136261u;
            uint flags = Flags;
            for (int i = 0; i < activeNodes; i++)
            {
                TetherNodeDTO node = Nodes[NodeOffset + i];
                if (!math.all(math.isfinite(node.CurrentAUP)))
                    flags |= TetherNodeRuntimeFlags.NonFiniteRecovered;

                double3 delta = node.CurrentAUP - AnchorAUP;
                double span = TetherAupRuntimeConstants.SafeLocalAupSpanMeters;
                double3 clamped = math.clamp(delta, new double3(-span), new double3(span));
                float3 local = new float3((float)clamped.x, (float)clamped.y, (float)clamped.z);
                hash = (hash ^ math.asuint(local.x)) * 16777619u;
                hash = (hash ^ math.asuint(local.y)) * 16777619u;
                hash = (hash ^ math.asuint(local.z)) * 16777619u;
            }

            float peakTension = SolverStats.IsCreated && SolverStats.Length > 0 ? SolverStats[0] : 0f;
            if (SolverStats.IsCreated && SolverStats.Length > 2 && SolverStats[2] > 0f)
                flags |= (uint)math.max(0, (int)SolverStats[2]);

            TelemetryRing[head] = new TetherAupTelemetryEntry
            {
                FrameIndex = FrameIndex,
                NodeCount = activeNodes,
                IterationCount = IterationCount,
                MaxTension = math.isfinite(peakTension) ? math.max(0f, peakTension) : 0f,
                AnchorAUP = math.all(math.isfinite(AnchorAUP)) ? AnchorAUP : double3.zero,
                StateHash = hash,
                Flags = flags,
                CpuMicroseconds = math.isfinite(CpuMicroseconds) ? math.max(0f, CpuMicroseconds) : 0f,
                GlobalQualityWeight = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 1f)
            };
            TelemetryHead[0] = (head + 1) % capacity;
        }
    }

    internal static class TetherGpuSplineUploadBridge
    {
        public static unsafe void UploadSplineVertices(GraphicsBuffer destination, NativeArray<TetherSplineVertexDTO> source, int count)
        {
            if (destination == null || !source.IsCreated)
                return;

            int safeCount = math.min(math.max(0, count), math.min(source.Length, destination.count));
            if (safeCount <= 0)
                return;

            NativeArray<TetherSplineVertexDTO> mapped = destination.LockBufferForWrite<TetherSplineVertexDTO>(0, safeCount);
            var job = new TetherSplineGpuMemcpyJob
            {
                Source = source,
                Destination = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped),
                Count = safeCount,
                DestinationBytes = (long)UnsafeUtility.SizeOf<TetherSplineVertexDTO>() * mapped.Length
            };
            JobHandle handle = job.Schedule();
            DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
            destination.UnlockBufferAfterWrite<TetherSplineVertexDTO>(safeCount);
        }
    }

    public static class TetherAupForcePacketBridge
    {
        public static int FlushPacketPair(
            in TetherForcePacketDTO anchorPacket,
            in TetherForcePacketDTO payloadPacket,
            Rigidbody anchorBody,
            Rigidbody payloadBody,
            double3 localOriginAUP,
            float maxForceNewton)
        {
            int accepted = 0;
            if (TryFlushOne(in anchorPacket, anchorBody, localOriginAUP, maxForceNewton))
                accepted++;
            if (TryFlushOne(in payloadPacket, payloadBody, localOriginAUP, maxForceNewton))
                accepted++;
            return accepted;
        }

        public static int FlushToPhysics(
            NativeArray<TetherForcePacketDTO> packets,
            int packetCount,
            Rigidbody anchorBody,
            Rigidbody payloadBody,
            double3 localOriginAUP,
            float maxForceNewton,
            uint frameIndex)
        {
            if (!packets.IsCreated || packetCount <= 0)
                return 0;

            int count = math.min(packetCount, packets.Length);
            float maxForce = math.isfinite(maxForceNewton) && maxForceNewton > 0f ? maxForceNewton : float.MaxValue;
            int accepted = 0;
            for (int i = 0; i < count; i++)
            {
                TetherForcePacketDTO packet = packets[i];
                if (frameIndex != 0u && packet.FrameIndex != frameIndex)
                    continue;
                Rigidbody body = packet.BodySlot == 0 ? anchorBody : payloadBody;
                if (TryFlushOne(in packet, body, localOriginAUP, maxForce))
                    accepted++;
            }

            return accepted;
        }

        private static bool TryFlushOne(
            in TetherForcePacketDTO packet,
            Rigidbody body,
            double3 localOriginAUP,
            float maxForceNewton)
        {
            if (body == null || packet.Tension <= 0f || !math.isfinite(packet.Tension))
                return false;

            float3 force3 = packet.Force;
            float forceSq = math.lengthsq(force3);
            if (!math.isfinite(forceSq) || forceSq <= 0.000001f)
                return false;

            float maxForce = math.isfinite(maxForceNewton) && maxForceNewton > 0f ? maxForceNewton : float.MaxValue;
            if (forceSq > maxForce * maxForce)
                force3 *= maxForce * math.rsqrt(math.max(forceSq, 0.000001f));

            float3 localPoint = AupToLocalFloat3(packet.ApplicationAUP - localOriginAUP);
            Vector3 force = new Vector3(force3.x, force3.y, force3.z);
            Vector3 worldPoint = new Vector3(localPoint.x, localPoint.y, localPoint.z);
            return PhysicsForceRouter.QueueForceAtPosition(body, force, worldPoint, ForceMode.Acceleration);
        }

        private static float3 AupToLocalFloat3(double3 delta)
        {
            if (!math.all(math.isfinite(delta)))
                return float3.zero;

            double span = TetherAupRuntimeConstants.SafeLocalAupSpanMeters;
            double3 clamped = math.clamp(delta, new double3(-span), new double3(span));
            float3 local = new float3((float)clamped.x, (float)clamped.y, (float)clamped.z);
            return math.all(math.isfinite(local)) ? local : float3.zero;
        }
    }

    public static class TetherAupRuntimeIntrospection
    {
        public static bool TrySampleLatestTelemetry(out TetherAupTelemetryEntry telemetry)
        {
            telemetry = default;
            return TrySampleLatestTelemetry(GlobalRegistry.DataVault, out telemetry);
        }

        public static bool TrySampleLatestTelemetry(IDataVault vault, out TetherAupTelemetryEntry telemetry)
        {
            telemetry = default;
            if (vault == null ||
                !vault.TryGetBufferHandle(BufferID.Shinobu143TetherTelemetryRing, out VaultBufferHandle<TetherAupTelemetryEntry> ringHandle) ||
                !vault.TryGetBufferHandle(BufferID.Shinobu143TetherTelemetryHead, out VaultBufferHandle<int> headHandle))
            {
                return false;
            }

            NativeArray<TetherAupTelemetryEntry> ring = ringHandle.Resolve(vault);
            NativeArray<int> head = headHandle.Resolve(vault);
            if (!ring.IsCreated || ring.Length == 0 || !head.IsCreated || head.Length == 0)
                return false;

            int capacity = math.min(TetherAupRuntimeConstants.TelemetryCapacity, ring.Length);
            int index = head[0] - 1;
            if (index < 0)
                index = capacity - 1;
            if ((uint)index >= (uint)capacity)
                index = 0;

            telemetry = ring[index];
            return telemetry.NodeCount > 0 || telemetry.FrameIndex != 0u;
        }

        public static bool TryDumpCableSurgeon(uint reasonFlags)
        {
            return TryDumpCableSurgeon(GlobalRegistry.DataVault, reasonFlags);
        }

        public static bool TryDumpCableSurgeon(IDataVault vault, uint reasonFlags)
        {
            DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
            return projectRoot != null && TetherAupBlackBoxDumper.TryDumpLatestVault(vault, projectRoot.FullName, reasonFlags);
        }
    }

    public static class TetherAupBlackBoxDumper
    {
        private const ulong DumpMagic = 0x3134335F55424F53ul; // SOBU_431
        private const string LegacyDumpRelativePath = "Docs/AgentLogs/Dump_CABLE_SURGEON.bin";
        private const string H8DumpRelativePath = "Docs/AgentLogs/Dump_CABLE_SURGEON.h8dump";

        public static bool TryDumpLatestVault(IDataVault vault, string projectRoot, uint reasonFlags)
        {
            if (vault == null || string.IsNullOrEmpty(projectRoot))
                return false;
            if (!vault.TryGetBufferHandle(BufferID.Shinobu143TetherTelemetryRing, out VaultBufferHandle<TetherAupTelemetryEntry> ringHandle) ||
                !vault.TryGetBufferHandle(BufferID.Shinobu143TetherTelemetryHead, out VaultBufferHandle<int> headHandle))
            {
                return false;
            }

            NativeArray<TetherAupTelemetryEntry> ring = ringHandle.Resolve(vault);
            NativeArray<int> head = headHandle.Resolve(vault);
            if (!ring.IsCreated || ring.Length == 0 || !head.IsCreated || head.Length == 0)
                return false;

            int capacity = math.min(TetherAupRuntimeConstants.TelemetryCapacity, ring.Length);
            int normalizedHead = head[0];
            if ((uint)normalizedHead >= (uint)capacity)
                normalizedHead = 0;

            string legacyPath = Path.Combine(projectRoot, LegacyDumpRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string h8Path = Path.Combine(projectRoot, H8DumpRelativePath.Replace('/', Path.DirectorySeparatorChar));
            NativeArray<TetherAupTelemetryEntry> slice = ring.GetSubArray(0, capacity);
            TetherBlackBoxDumpWriter.WritePrimaryAndLegacy(
                h8Path,
                legacyPath,
                DumpMagic,
                slice,
                normalizedHead,
                reasonFlags);
            return true;
        }
    }

    internal static class TetherAupVaultBootstrap
    {
        public static void EnsureMockBuffers(IDataVault vault, float globalQualityWeight, uint frameIndex)
        {
            if (vault == null)
                return;

            VaultBufferHandle<int> stateHandle = vault.GetBufferHandle<int>(
                BufferID.Shinobu143TetherBootstrapState,
                1,
                SystemID.Physics,
                NativeArrayOptions.ClearMemory);
            NativeArray<int> state = stateHandle.Resolve(vault);
            if (state.IsCreated && state.Length > 0 && state[0] == TetherAupRuntimeConstants.BootstrapMagic)
                return;

            NativeArray<TetherNodeDTO> nodes = vault.GetBufferHandle<TetherNodeDTO>(
                BufferID.Shinobu143TetherAupNodes,
                TetherAupRuntimeConstants.MockNodeCapacity,
                SystemID.Physics,
                NativeArrayOptions.UninitializedMemory).Resolve(vault);
            NativeArray<TetherConstraintDTO> constraints = vault.GetBufferHandle<TetherConstraintDTO>(
                BufferID.Shinobu143TetherConstraints,
                TetherAupRuntimeConstants.MockConstraintCapacity,
                SystemID.Physics,
                NativeArrayOptions.UninitializedMemory).Resolve(vault);
            NativeArray<TetherEndpointAupDTO> endpoints = vault.GetBufferHandle<TetherEndpointAupDTO>(
                BufferID.Shinobu143TetherEndpoints,
                TetherAupRuntimeConstants.MockTetherCount,
                SystemID.Physics,
                NativeArrayOptions.UninitializedMemory).Resolve(vault);
            vault.GetBufferHandle<TetherSplineVertexDTO>(
                BufferID.Shinobu143TetherSplineVertices,
                TetherAupRuntimeConstants.MockSplineVertexCapacity,
                SystemID.Physics,
                NativeArrayOptions.UninitializedMemory).Resolve(vault);
            vault.GetBufferHandle<TetherForcePacketDTO>(
                BufferID.Shinobu143TetherForcePackets,
                TetherAupRuntimeConstants.MockForcePacketCapacity,
                SystemID.Physics,
                NativeArrayOptions.UninitializedMemory).Resolve(vault);
            NativeArray<float> segmentTensions = vault.GetBufferHandle<float>(
                BufferID.Shinobu143TetherSegmentTensions,
                TetherAupRuntimeConstants.MockConstraintCapacity,
                SystemID.Physics,
                NativeArrayOptions.UninitializedMemory).Resolve(vault);
            NativeArray<float> solverStats = vault.GetBufferHandle<float>(
                BufferID.Shinobu143TetherSolverStats,
                TetherAupRuntimeConstants.SolverStatsCapacity,
                SystemID.Physics,
                NativeArrayOptions.ClearMemory).Resolve(vault);
            NativeArray<double3> pinnedAups = vault.GetBufferHandle<double3>(
                BufferID.Shinobu143TetherPinnedAups,
                TetherAupRuntimeConstants.MockNodeCapacity,
                SystemID.Physics,
                NativeArrayOptions.UninitializedMemory).Resolve(vault);
            NativeArray<byte> pinnedMask = vault.GetBufferHandle<byte>(
                BufferID.Shinobu143TetherPinnedMask,
                TetherAupRuntimeConstants.MockNodeCapacity,
                SystemID.Physics,
                NativeArrayOptions.UninitializedMemory).Resolve(vault);
            vault.GetBufferHandle<TetherAupTelemetryEntry>(
                BufferID.Shinobu143TetherTelemetryRing,
                TetherAupRuntimeConstants.TelemetryCapacity,
                SystemID.Physics,
                NativeArrayOptions.UninitializedMemory).Resolve(vault);
            vault.GetBufferHandle<int>(
                BufferID.Shinobu143TetherTelemetryHead,
                1,
                SystemID.Physics,
                NativeArrayOptions.ClearMemory).Resolve(vault);
            NativeArray<CableMaterialDTO> materials = vault.GetBufferHandle<CableMaterialDTO>(
                BufferID.Shinobu143CableMaterials,
                TetherAupRuntimeConstants.MaterialCapacity,
                SystemID.Physics,
                NativeArrayOptions.UninitializedMemory).Resolve(vault);
            vault.GetBufferHandle<byte>(
                BufferID.Shinobu143CableMaterialCsvScratch,
                16 * 1024,
                SystemID.Physics,
                NativeArrayOptions.UninitializedMemory).Resolve(vault);

            if (!nodes.IsCreated ||
                !constraints.IsCreated ||
                !endpoints.IsCreated ||
                !materials.IsCreated ||
                !state.IsCreated ||
                !segmentTensions.IsCreated ||
                !solverStats.IsCreated ||
                !pinnedAups.IsCreated ||
                !pinnedMask.IsCreated)
            {
                return;
            }

            var job = new InitializeMockTetherAupJob
            {
                Nodes = nodes,
                Constraints = constraints,
                Endpoints = endpoints,
                Materials = materials,
                BootstrapState = state,
                PinnedAUPs = pinnedAups,
                PinnedMask = pinnedMask,
                FrameIndex = frameIndex,
                SectorHash = 0x5348494Eu,
                GlobalQualityWeight = globalQualityWeight
            };
            JobHandle handle = job.Schedule();
            DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
        }
    }
}
