using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Construction
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct EvaluateSocketSnappingJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<SocketStateDTO> TargetSockets;
        [ReadOnly, NoAlias] public NativeArray<double3> TargetSocketAups;
        [ReadOnly, NoAlias] public NativeArray<SocketStateDTO> GhostSockets;
        [ReadOnly, NoAlias] public NativeArray<double3> GhostSocketAups;
        [ReadOnly, NoAlias] public NativeArray<int2> SocketCsrRanges;
        [ReadOnly, NoAlias] public NativeArray<int> SocketCsrTargetIndices;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<SocketSnappingResultDTO> Results;
        public ConstructionSocketTuningDTO Tuning;
        public double3 GhostRootAup;
        public double3 RuntimeOriginAup;
        public quaternion GhostRootRotation;
        public uint GhostModuleHash;
        public int TargetCount;
        public int GhostCount;
        public int SocketCsrRangeOffset;

        public void Execute(int ghostIndex)
        {
            if (!Results.IsCreated || (uint)ghostIndex >= (uint)Results.Length)
                return;

            SocketSnappingResultDTO best = default;
            best.DistanceSq = float.MaxValue;
            best.AlignmentDot = -1f;
            best.TargetSocketIndex = -1;
            best.GhostSocketIndex = ghostIndex;
            best.SnappingMatrix = float4x4.identity;
            best.SnappedRootAup = GhostRootAup;
            best.GhostModuleHash = GhostModuleHash;

            if ((uint)ghostIndex >= (uint)GhostCount ||
                !TargetSockets.IsCreated ||
                !TargetSocketAups.IsCreated ||
                !GhostSockets.IsCreated ||
                !GhostSocketAups.IsCreated ||
                (uint)ghostIndex >= (uint)GhostSockets.Length ||
                (uint)ghostIndex >= (uint)GhostSocketAups.Length)
            {
                best.Flags = ConstructionSocketFlags.NonFinite;
                Results[ghostIndex] = best;
                return;
            }

            SocketStateDTO ghostSocket = GhostSockets[ghostIndex];
            double3 ghostSocketAup = GhostSocketAups[ghostIndex];
            uint ghostFault = ghostSocket.ConnectionStatus & (ConstructionSocketFlags.NonFinite | ConstructionSocketFlags.CollisionBlocked);
            bool ghostDirectionValid = ShinobuSocketConstructionRuntime.HasValidDirection(ghostSocket);
            if (ghostFault != 0u || !ghostDirectionValid)
            {
                best.Flags = ghostDirectionValid ? ghostFault : ghostFault | ConstructionSocketFlags.NonFinite;
                Results[ghostIndex] = best;
                return;
            }

            float3 ghostNormal = ResolveSafeNormal(ghostSocket);
            if (!math.all(math.isfinite(ghostSocketAup)) || !math.all(math.isfinite(ghostNormal)))
            {
                best.Flags = ConstructionSocketFlags.NonFinite;
                Results[ghostIndex] = best;
                return;
            }

            int2 range = ResolveRange(ghostIndex);
            int safeStart = math.clamp(range.x, 0, math.max(0, TargetCount));
            int safeCount = math.clamp(range.y, 0, math.max(0, TargetCount - safeStart));
            int budget = math.min(
                safeCount,
                ShinobuSocketConstructionRuntime.ResolveCandidateBudget(
                    Tuning.GlobalQualityWeight,
                    Tuning.MinCandidateBudget,
                    Tuning.MaxCandidateBudget));
            double radius = ShinobuSocketConstructionRuntime.ResolveSearchRadius(
                Tuning.GlobalQualityWeight,
                Tuning.SearchRadiusLowMeters,
                Tuning.SearchRadiusUltraMeters);
            double radiusSq = radius * radius;
            float alignmentThreshold = math.clamp(Tuning.AlignmentDotThreshold, -1f, 1f);
            uint evaluated = 0u;

            for (int offset = 0; offset < safeCount && evaluated < (uint)budget; offset++)
            {
                int csrIndex = safeStart + offset;
                int targetIndex = SocketCsrTargetIndices.IsCreated && (uint)csrIndex < (uint)SocketCsrTargetIndices.Length
                    ? SocketCsrTargetIndices[csrIndex]
                    : csrIndex;
                evaluated++;
                if ((uint)targetIndex >= (uint)TargetSockets.Length ||
                    (uint)targetIndex >= (uint)TargetSocketAups.Length)
                {
                    best.Flags |= ConstructionSocketFlags.NonFinite;
                    continue;
                }

                SocketStateDTO targetSocket = TargetSockets[targetIndex];
                if ((targetSocket.ConnectionStatus & (ConstructionSocketFlags.Connected | ConstructionSocketFlags.CollisionBlocked | ConstructionSocketFlags.NonFinite)) != 0u)
                    continue;

                double3 targetSocketAup = TargetSocketAups[targetIndex];
                double3 deltaAup = targetSocketAup - ghostSocketAup;
                if (!ShinobuSocketConstructionRuntime.HasValidDirection(targetSocket) ||
                    !math.all(math.isfinite(targetSocketAup)) ||
                    !math.all(math.isfinite(deltaAup)))
                {
                    best.Flags |= ConstructionSocketFlags.NonFinite;
                    continue;
                }

                double distanceSqDouble = math.lengthsq(deltaAup);
                if (distanceSqDouble > radiusSq)
                    continue;

                if (!ShinobuSocketConstructionRuntime.AreCompatible(targetSocket, ghostSocket))
                    continue;

                float3 targetNormal = ResolveSafeNormal(targetSocket);
                float alignmentDot = math.dot(targetNormal, -ghostNormal);
                if (!math.isfinite(alignmentDot) || alignmentDot < alignmentThreshold)
                    continue;

                quaternion alignedRotation = FromToRotation(ghostNormal, -targetNormal);
                alignedRotation = math.mul(alignedRotation, GhostRootRotation);
                double3 ghostLocalOffset = ghostSocket.LocalOffset;
                if (!math.all(math.isfinite(ghostLocalOffset)) || !math.all(math.isfinite(alignedRotation.value)))
                {
                    best.Flags |= ConstructionSocketFlags.NonFinite;
                    continue;
                }

                float3 rotatedGhostOffset = math.rotate(
                    alignedRotation,
                    new float3((float)ghostLocalOffset.x, (float)ghostLocalOffset.y, (float)ghostLocalOffset.z));
                if (!math.all(math.isfinite(rotatedGhostOffset)))
                {
                    best.Flags |= ConstructionSocketFlags.NonFinite;
                    continue;
                }

                float distanceSq = (float)math.min(distanceSqDouble, (double)float.MaxValue);
                if (distanceSq >= best.DistanceSq)
                    continue;

                double3 snappedRootAup = targetSocketAup - new double3(rotatedGhostOffset.x, rotatedGhostOffset.y, rotatedGhostOffset.z);
                double3 runtimePositionDouble = snappedRootAup - RuntimeOriginAup;
                if (!math.all(math.isfinite(snappedRootAup)) ||
                    !math.all(math.isfinite(runtimePositionDouble)) ||
                    math.any(math.abs(runtimePositionDouble) > (double)float.MaxValue))
                {
                    best.Flags |= ConstructionSocketFlags.NonFinite;
                    continue;
                }

                float3 runtimePosition = new float3(
                    (float)runtimePositionDouble.x,
                    (float)runtimePositionDouble.y,
                    (float)runtimePositionDouble.z);

                best.SnappingMatrix = float4x4.TRS(runtimePosition, alignedRotation, new float3(1f));
                best.SnappedRootAup = snappedRootAup;
                best.TargetSocketIndex = targetIndex;
                best.GhostSocketIndex = ghostIndex;
                best.DistanceSq = distanceSq;
                best.AlignmentDot = alignmentDot;
                best.Flags = ConstructionSocketFlags.ValidSnap | ConstructionSocketFlags.DearLieActive;
                best.TargetModuleHash = targetSocket.ParentModuleHash;
                best.GhostModuleHash = GhostModuleHash;
                best.DearLieDampen = ShinobuSocketConstructionRuntime.ResolveDearLieDampen(
                    distanceSq,
                    Tuning.SnappingRadius,
                    Tuning.GlobalQualityWeight,
                    Tuning.DearLieShrinkMeters);
                best.ResultHash = ShinobuSocketConstructionRuntime.MakeResultHash(
                    (uint)targetIndex,
                    (uint)ghostIndex,
                    targetSocket.ParentModuleHash,
                    GhostModuleHash);
            }

            best.EvaluatedCandidates = evaluated;
            Results[ghostIndex] = best;
        }

        private int2 ResolveRange(int ghostIndex)
        {
            int rangeIndex = SocketCsrRangeOffset + ghostIndex;
            if (SocketCsrRanges.IsCreated && (uint)rangeIndex < (uint)SocketCsrRanges.Length)
                return SocketCsrRanges[rangeIndex];

            return new int2(0, TargetCount);
        }

        private static float3 ResolveSafeNormal(SocketStateDTO socket)
        {
            float3 normal = socket.NormalDirection;
            float lengthSq = math.lengthsq(normal);
            if (!math.all(math.isfinite(normal)) || lengthSq <= 0.000001f)
                normal = ShinobuSocketConstructionRuntime.DirectionToNormal(ShinobuSocketConstructionRuntime.ExtractDirection(socket));
            else
                normal *= math.rsqrt(lengthSq);

            return normal;
        }

        private static quaternion FromToRotation(float3 from, float3 to)
        {
            float3 f = math.normalizesafe(from, new float3(0f, 0f, 1f));
            float3 t = math.normalizesafe(to, new float3(0f, 0f, 1f));
            float dot = math.clamp(math.dot(f, t), -1f, 1f);
            if (dot > 0.9999f)
                return quaternion.identity;

            if (dot < -0.9999f)
            {
                float3 basis = math.abs(f.x) < 0.9f ? new float3(1f, 0f, 0f) : new float3(0f, 1f, 0f);
                float3 axis = math.normalizesafe(math.cross(f, basis), new float3(0f, 1f, 0f));
                return quaternion.AxisAngle(axis, math.PI);
            }

            float3 cross = math.cross(f, t);
            float invS = math.rsqrt(math.max(0.000001f, (1f + dot) * 2f));
            quaternion rotation = new quaternion(cross.x * invS, cross.y * invS, cross.z * invS, 0.5f / invS);
            return math.normalize(rotation);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct SelectBestSocketSnapJob : IJob
    {
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<SocketSnappingResultDTO> Results;
        public int ResultCount;
        public int ResultSinkIndex;

        public void Execute()
        {
            if (!Results.IsCreated || (uint)ResultSinkIndex >= (uint)Results.Length)
                return;

            SocketSnappingResultDTO best = default;
            best.TargetSocketIndex = -1;
            best.GhostSocketIndex = -1;
            best.DistanceSq = float.MaxValue;
            best.SnappingMatrix = float4x4.identity;

            uint evaluated = 0u;
            uint faultFlags = 0u;
            int count = ResultCount > 0 ? math.min(ResultCount, Results.Length) : Results.Length;
            count = math.min(count, ResultSinkIndex);
            for (int i = 0; i < count; i++)
            {
                SocketSnappingResultDTO candidate = Results[i];
                evaluated = AddSaturating(evaluated, candidate.EvaluatedCandidates);
                faultFlags |= candidate.Flags & (ConstructionSocketFlags.NonFinite | ConstructionSocketFlags.CollisionBlocked | ConstructionSocketFlags.CapacityExceeded);
                if ((candidate.Flags & ConstructionSocketFlags.ValidSnap) == 0u)
                    continue;

                if (!IsFiniteResult(candidate))
                {
                    faultFlags |= ConstructionSocketFlags.NonFinite;
                    continue;
                }

                if (candidate.DistanceSq >= best.DistanceSq)
                    continue;

                best = candidate;
            }

            best.EvaluatedCandidates = evaluated;
            best.Flags |= faultFlags;
            Results[ResultSinkIndex] = best;
        }

        private static uint AddSaturating(uint lhs, uint rhs)
        {
            uint sum = lhs + rhs;
            return sum < lhs ? uint.MaxValue : sum;
        }

        private static bool IsFiniteResult(SocketSnappingResultDTO result)
        {
            return math.isfinite(result.DistanceSq) &&
                   math.isfinite(result.AlignmentDot) &&
                   math.all(math.isfinite(result.SnappedRootAup)) &&
                   math.all(math.isfinite(result.SnappingMatrix.c0)) &&
                   math.all(math.isfinite(result.SnappingMatrix.c1)) &&
                   math.all(math.isfinite(result.SnappingMatrix.c2)) &&
                   math.all(math.isfinite(result.SnappingMatrix.c3));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct AdaptConnectedSocketsJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<SocketConnectionPairDTO> Connections;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<SocketStateDTO> TargetSockets;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<SocketStateDTO> GhostSockets;
        public int ConnectionCount;

        public void Execute()
        {
            if (!Connections.IsCreated)
                return;

            int count = ConnectionCount > 0 ? math.min(ConnectionCount, Connections.Length) : Connections.Length;
            for (int index = 0; index < count; index++)
            {
                SocketConnectionPairDTO connection = Connections[index];
                if ((connection.Flags & ConstructionSocketFlags.ValidSnap) == 0u)
                    continue;

                uint flags = ConstructionSocketFlags.Connected | (connection.ConnectionKind & (ConstructionSocketFlags.CorridorRoom | ConstructionSocketFlags.Hatch));
                if (TargetSockets.IsCreated && (uint)connection.TargetSocketIndex < (uint)TargetSockets.Length)
                {
                    SocketStateDTO target = TargetSockets[connection.TargetSocketIndex];
                    target.ConnectionStatus |= flags;
                    TargetSockets[connection.TargetSocketIndex] = target;
                }

                if (GhostSockets.IsCreated && (uint)connection.GhostSocketIndex < (uint)GhostSockets.Length)
                {
                    SocketStateDTO ghost = GhostSockets[connection.GhostSocketIndex];
                    ghost.ConnectionStatus |= flags;
                    GhostSockets[connection.GhostSocketIndex] = ghost;
                }
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct VerifyModuleBoundsJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<SocketModuleBoundsDTO> ProposedBounds;
        [ReadOnly, NoAlias] public NativeArray<SocketModuleBoundsDTO> ExistingBounds;
        [ReadOnly, NoAlias] public NativeArray<byte> VoxelSdfSamples;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<SocketBoundsResultDTO> Results;
        public ConstructionSocketTuningDTO Tuning;
        public int ExistingCount;

        public void Execute(int index)
        {
            if (!Results.IsCreated || !ProposedBounds.IsCreated || (uint)index >= (uint)Results.Length || (uint)index >= (uint)ProposedBounds.Length)
                return;

            SocketModuleBoundsDTO proposed = ProposedBounds[index];
            SocketBoundsResultDTO result = default;
            result.HitModuleIndex = -1;
            result.SdfHitIndex = -1;
            result.MinSeparationMeters = float.MaxValue;

            if (!math.all(math.isfinite(proposed.CenterAup)) ||
                !math.all(math.isfinite(proposed.Extents)) ||
                math.any(proposed.Extents < 0f))
            {
                result.FailureFlags |= ConstructionSocketFlags.NonFinite;
                Results[index] = result;
                return;
            }

            int budget = math.min(
                math.max(0, ExistingCount),
                ShinobuSocketConstructionRuntime.ResolveCandidateBudget(
                    Tuning.GlobalQualityWeight,
                    Tuning.MinCandidateBudget,
                    Tuning.MaxCandidateBudget));

            for (int i = 0; i < budget && i < ExistingBounds.Length; i++)
            {
                SocketModuleBoundsDTO existing = ExistingBounds[i];
                if ((existing.Flags & ConstructionSocketFlags.CollisionBlocked) != 0u)
                    continue;

                double3 centerDelta = existing.CenterAup - proposed.CenterAup;
                float3 absDelta = new float3(
                    (float)math.abs(centerDelta.x),
                    (float)math.abs(centerDelta.y),
                    (float)math.abs(centerDelta.z));
                float3 separation = absDelta - (existing.Extents + proposed.Extents);
                float maxAxisSeparation = math.cmax(separation);
                result.MinSeparationMeters = math.min(result.MinSeparationMeters, maxAxisSeparation);
                result.EvaluatedBounds++;

                if (math.all(separation <= 0f))
                {
                    result.FailureFlags |= ConstructionSocketFlags.CollisionBlocked;
                    result.HitModuleIndex = i;
                    break;
                }
            }

            if (VoxelSdfSamples.IsCreated && proposed.SdfSampleCount > 0)
            {
                int start = math.max(0, proposed.SdfSampleStart);
                int end = math.min(VoxelSdfSamples.Length, start + proposed.SdfSampleCount);
                for (int s = start; s < end; s++)
                {
                    if ((sbyte)VoxelSdfSamples[s] >= 0)
                        continue;

                    result.FailureFlags |= ConstructionSocketFlags.CollisionBlocked;
                    result.SdfHitIndex = s;
                    break;
                }
            }

            result.ResultHash = ShinobuSocketConstructionRuntime.MakeResultHash(
                proposed.ModuleHash,
                (uint)index,
                result.FailureFlags,
                (uint)math.max(0, result.HitModuleIndex));
            Results[index] = result;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct CommitPlacedModuleJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<ConstructionSocketModuleDTO> PendingModules;
        [ReadOnly, NoAlias] public NativeArray<SocketStateDTO> PendingSockets;
        [ReadOnly, NoAlias] public NativeArray<double3> PendingSocketAups;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<ConstructionSocketModuleDTO> Modules;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<SocketStateDTO> Sockets;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<double3> SocketAups;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> Counters;
        public uint Frame;

        public void Execute()
        {
            if (!PendingModules.IsCreated || PendingModules.Length <= 0 ||
                !Modules.IsCreated ||
                !Sockets.IsCreated ||
                !SocketAups.IsCreated ||
                !Counters.IsCreated ||
                Counters.Length < 4)
            {
                return;
            }

            ConstructionSocketModuleDTO pending = PendingModules[0];
            int moduleCount = math.max(0, Counters[0]);
            int socketCount = math.max(0, Counters[1]);
            int pendingSocketCount = math.clamp(pending.SocketCount, 0, PendingSockets.IsCreated ? PendingSockets.Length : 0);
            if (!IsFiniteModule(pending) || !AreFinitePendingSockets(PendingSockets, PendingSocketAups, pendingSocketCount))
            {
                Counters[3] = (int)ConstructionSocketFlags.NonFinite;
                return;
            }

            if (moduleCount >= Modules.Length || socketCount + pendingSocketCount > math.min(Sockets.Length, SocketAups.Length))
            {
                Counters[3] = (int)ConstructionSocketFlags.CapacityExceeded;
                return;
            }

            pending.SocketStart = socketCount;
            pending.Flags |= ConstructionSocketFlags.PendingCommit | ConstructionSocketFlags.TopologyDirty | ConstructionSocketFlags.RollbackFence;
            pending.TopologyVersion = unchecked((uint)(Counters[2] + 1));
            Modules[moduleCount] = pending;

            for (int i = 0; i < pendingSocketCount; i++)
            {
                SocketStateDTO socket = PendingSockets[i];
                socket.ParentModuleHash = pending.ModuleHash;
                Sockets[socketCount + i] = socket;
                SocketAups[socketCount + i] = PendingSocketAups.IsCreated && i < PendingSocketAups.Length
                    ? PendingSocketAups[i]
                    : pending.RootAup + socket.LocalOffset;
            }

            Counters[0] = moduleCount + 1;
            Counters[1] = socketCount + pendingSocketCount;
            Counters[2] = unchecked(Counters[2] + 1);
            Counters[3] = (int)(ConstructionSocketFlags.TopologyDirty | ConstructionSocketFlags.RollbackFence);
        }

        private static bool IsFiniteModule(ConstructionSocketModuleDTO module)
        {
            return math.all(math.isfinite(module.RootAup)) &&
                   math.all(math.isfinite(module.Rotation.value)) &&
                   math.all(math.isfinite(module.BoundsCenter)) &&
                   math.all(math.isfinite(module.BoundsExtents)) &&
                   math.all(module.BoundsExtents >= 0f) &&
                   module.SocketCount >= 0;
        }

        private static bool AreFinitePendingSockets(
            NativeArray<SocketStateDTO> pendingSockets,
            NativeArray<double3> pendingSocketAups,
            int pendingSocketCount)
        {
            if (pendingSocketCount <= 0)
                return true;

            if (!pendingSockets.IsCreated || pendingSocketCount > pendingSockets.Length)
                return false;

            for (int i = 0; i < pendingSocketCount; i++)
            {
                SocketStateDTO socket = pendingSockets[i];
                if (!math.all(math.isfinite(socket.LocalOffset)) ||
                    !math.all(math.isfinite(socket.NormalDirection)))
                {
                    return false;
                }

                if (pendingSocketAups.IsCreated &&
                    i < pendingSocketAups.Length &&
                    !math.all(math.isfinite(pendingSocketAups[i])))
                {
                    return false;
                }
            }

            return true;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct RecordConstructionSocketTelemetryJob : IJob
    {
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<ConstructionSocketTelemetryEntry> TelemetryRing;
        [ReadOnly, NoAlias] public NativeArray<SocketSnappingResultDTO> BestResult;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> DumpRequest;
        public double3 PreviewAup;
        public uint Frame;
        public uint ActiveSocketCount;
        public uint TopologyVersion;
        public float SolverMicroseconds;
        public float GlobalQualityWeight;
        public int BestResultIndex;

        public void Execute()
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0)
                return;

            SocketSnappingResultDTO best = default;
            if (BestResult.IsCreated && BestResult.Length > 0)
            {
                int safeBestIndex = math.clamp(BestResultIndex, 0, BestResult.Length - 1);
                best = BestResult[safeBestIndex];
            }

            int index = (int)(Frame % (uint)math.min(TelemetryRing.Length, ShinobuSocketConstructionRuntime.TelemetryCapacity));
            ConstructionSocketTelemetryEntry entry;
            entry.PreviewAup = PreviewAup;
            entry.Frame = Frame;
            entry.ActiveSocketCount = ActiveSocketCount;
            entry.EvaluatedCandidateCount = best.EvaluatedCandidates;
            entry.AcceptedSnapCount = (best.Flags & ConstructionSocketFlags.ValidSnap) != 0u ? 1u : 0u;
            entry.SolverMicroseconds = math.isfinite(SolverMicroseconds) ? SolverMicroseconds : -1f;
            entry.BestDistanceSq = math.isfinite(best.DistanceSq) ? best.DistanceSq : float.MaxValue;
            entry.Flags = best.Flags;
            entry.ResultHash = best.ResultHash;
            entry.GlobalQualityWeight = ShinobuSocketConstructionRuntime.SanitizeQuality(GlobalQualityWeight);
            entry.TopologyVersion = TopologyVersion;
            TelemetryRing[index] = entry;

            if ((!math.all(math.isfinite(PreviewAup)) ||
                 !math.isfinite(entry.BestDistanceSq) ||
                 (entry.Flags & ConstructionSocketFlags.NonFinite) != 0u) &&
                DumpRequest.IsCreated &&
                DumpRequest.Length > 0)
            {
                DumpRequest[0] = 1;
            }
        }
    }
}
