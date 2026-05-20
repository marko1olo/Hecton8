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

            if (!TryResolveRange(ghostIndex, out int2 range))
            {
                best.Flags = ConstructionSocketFlags.CapacityExceeded;
                Results[ghostIndex] = best;
                return;
            }

            if (!SocketCsrTargetIndices.IsCreated)
            {
                best.Flags = ConstructionSocketFlags.CapacityExceeded;
                Results[ghostIndex] = best;
                return;
            }

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
                evaluated++;
                if ((uint)csrIndex >= (uint)SocketCsrTargetIndices.Length)
                {
                    best.Flags |= ConstructionSocketFlags.CapacityExceeded;
                    continue;
                }

                int targetIndex = SocketCsrTargetIndices[csrIndex];
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

        private bool TryResolveRange(int ghostIndex, out int2 range)
        {
            range = default;
            int rangeIndex = SocketCsrRangeOffset + ghostIndex;
            if (!SocketCsrRanges.IsCreated || (uint)rangeIndex >= (uint)SocketCsrRanges.Length)
                return false;

            range = SocketCsrRanges[rangeIndex];
            return true;
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
    public struct BuildBuilderGhostStateJob : IJob
    {
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<BuilderGhostStateDTO> States;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<BuilderGhostVisualDTO> Visuals;
        public double3 TargetAup;
        public double3 RuntimeOriginAup;
        public quaternion Rotation;
        public float3 BoundsScale;
        public double GridSizeMeters;
        public uint PrefabHashID;
        public uint ValidationFlags;
        public float AnimationPhase;
        public float GlobalQualityWeight;
        public float DearLieDampen;
        public float DearLieWiggleSpeed;
        public float4 ValidColor;
        public float4 InvalidColor;
        public uint Frame;
        public int StateIndex;

        public void Execute()
        {
            if (!States.IsCreated || States.Length <= 0)
                return;

            int index = math.clamp(StateIndex, 0, States.Length - 1);
            double3 snappedAup = SnapAup(TargetAup, GridSizeMeters);
            double3 runtimeDouble = snappedAup - RuntimeOriginAup;
            float3 runtimePosition = new float3(
                (float)runtimeDouble.x,
                (float)runtimeDouble.y,
                (float)runtimeDouble.z);
            float3 safeScale = math.max(BoundsScale, new float3(0.001f));
            uint flags = ValidationFlags |
                         BuilderGhostValidationFlags.Active |
                         BuilderGhostValidationFlags.PresentationOnly |
                         BuilderGhostValidationFlags.RollbackExcluded;

            if (!math.all(math.isfinite(snappedAup)) ||
                !math.all(math.isfinite(runtimeDouble)) ||
                !math.all(math.isfinite(runtimePosition)) ||
                !math.all(math.isfinite(Rotation.value)) ||
                !math.all(math.isfinite(safeScale)) ||
                math.any(math.abs(runtimeDouble) > (double)float.MaxValue))
            {
                flags &= ~BuilderGhostValidationFlags.Valid;
                flags |= BuilderGhostValidationFlags.NonFinite;
                runtimePosition = float3.zero;
                Rotation = quaternion.identity;
                safeScale = new float3(0.001f);
            }

            BuilderGhostStateDTO state;
            state.LocalToWorld = float4x4.TRS(runtimePosition, Rotation, safeScale);
            state.AUP_TargetPosition = snappedAup;
            state.PrefabHashID = PrefabHashID;
            state.ValidationFlags = flags;
            state.AnimationPhase = math.isfinite(AnimationPhase) ? AnimationPhase : 0f;
            state.ValidationStateHash = MakeBuilderGhostHash(PrefabHashID, flags, state.AnimationPhase, Frame);
            state._pad0 = 0u;
            state._pad1 = 0u;
            state._pad2 = 0u;
            state._pad3 = 0u;
            state._pad4 = 0u;
            state._pad5 = 0u;
            States[index] = state;

            if (Visuals.IsCreated && (uint)index < (uint)Visuals.Length)
            {
                BuilderGhostVisualDTO visual;
                visual.GlobalQualityWeight = ShinobuSocketConstructionRuntime.SanitizeQuality(GlobalQualityWeight);
                visual.DearLieDampen = math.clamp(math.isfinite(DearLieDampen) ? DearLieDampen : 0f, 0f, 1f);
                visual.DearLieWiggleSpeed = math.isfinite(DearLieWiggleSpeed) && DearLieWiggleSpeed > 0f ? DearLieWiggleSpeed : 18f;
                visual.Alpha = 1f;
                visual.ValidColor = ValidColor;
                visual.InvalidColor = InvalidColor;
                visual.Flags = flags;
                visual.Frame = Frame;
                visual._pad0 = 0u;
                visual._pad1 = 0u;
                Visuals[index] = visual;
            }
        }

        private static double3 SnapAup(double3 aup, double gridSize)
        {
            if (!math.isfinite(gridSize) || gridSize <= 0.0001d)
                return aup;

            double inv = 1.0d / gridSize;
            return math.round(aup * inv) * gridSize;
        }

        private static uint MakeBuilderGhostHash(uint prefabHash, uint flags, float phase, uint frame)
        {
            uint hash = ShinobuSocketConstructionRuntime.FoldHash(2166136261u, prefabHash);
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, flags);
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(phase));
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, frame);
            return hash;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ValidateBuilderGhostPlacementJob : IJob
    {
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<BuilderGhostStateDTO> States;
        [ReadOnly, NoAlias] public NativeArray<SocketModuleBoundsDTO> ExistingBounds;
        [ReadOnly, NoAlias] public NativeArray<byte> VoxelSdfSamples;
        public float3 BoundsExtents;
        public int ExistingCount;
        public float SolidSdfThreshold;
        public int StateIndex;

        public void Execute()
        {
            if (!States.IsCreated || States.Length <= 0)
                return;

            int index = math.clamp(StateIndex, 0, States.Length - 1);
            BuilderGhostStateDTO state = States[index];
            uint flags = state.ValidationFlags;
            flags &= ~(BuilderGhostValidationFlags.Valid | BuilderGhostValidationFlags.SdfBlocked | BuilderGhostValidationFlags.BoundsBlocked);

            float minSdf = float.MaxValue;
            uint cornerChecks = 0u;
            bool finite = IsFiniteState(in state) && math.all(math.isfinite(BoundsExtents)) && math.all(BoundsExtents >= 0f);
            if (!finite)
            {
                flags |= BuilderGhostValidationFlags.NonFinite;
                state.ValidationFlags = flags;
                state.ValidationStateHash = MakeValidationHash(state.PrefabHashID, flags, 0u, 0u);
                States[index] = state;
                return;
            }

            float3 center = state.LocalToWorld.c3.xyz;
            float3 axisX = state.LocalToWorld.c0.xyz * 0.5f;
            float3 axisY = state.LocalToWorld.c1.xyz * 0.5f;
            float3 axisZ = state.LocalToWorld.c2.xyz * 0.5f;
            for (int corner = 0; corner < 8; corner++)
            {
                float sx = (corner & 1) == 0 ? -1f : 1f;
                float sy = (corner & 2) == 0 ? -1f : 1f;
                float sz = (corner & 4) == 0 ? -1f : 1f;
                float3 runtimeCorner = center + axisX * sx + axisY * sy + axisZ * sz;
                if (!math.all(math.isfinite(runtimeCorner)))
                {
                    flags |= BuilderGhostValidationFlags.NonFinite;
                    continue;
                }

                cornerChecks++;
                if (VoxelSdfSamples.IsCreated && corner < VoxelSdfSamples.Length)
                {
                    float sdf = (sbyte)VoxelSdfSamples[corner] * (1f / 127f);
                    minSdf = math.min(minSdf, sdf);
                    if (sdf <= SolidSdfThreshold)
                        flags |= BuilderGhostValidationFlags.SdfBlocked;
                }
            }

            if (ExistingBounds.IsCreated && ExistingCount > 0)
            {
                int count = math.min(ExistingCount, ExistingBounds.Length);
                for (int i = 0; i < count; i++)
                {
                    SocketModuleBoundsDTO existing = ExistingBounds[i];
                    if ((existing.Flags & ConstructionSocketFlags.CollisionBlocked) != 0u ||
                        !math.all(math.isfinite(existing.CenterAup)) ||
                        !math.all(math.isfinite(existing.Extents)))
                    {
                        continue;
                    }

                    double3 delta = existing.CenterAup - state.AUP_TargetPosition;
                    float3 absDelta = new float3(
                        (float)math.abs(delta.x),
                        (float)math.abs(delta.y),
                        (float)math.abs(delta.z));
                    if (math.all(absDelta <= existing.Extents + BoundsExtents))
                    {
                        flags |= BuilderGhostValidationFlags.BoundsBlocked;
                        break;
                    }
                }
            }

            if ((flags & (BuilderGhostValidationFlags.NonFinite | BuilderGhostValidationFlags.SdfBlocked | BuilderGhostValidationFlags.BoundsBlocked)) == 0u)
                flags |= BuilderGhostValidationFlags.Valid;

            state.ValidationFlags = flags;
            state.ValidationStateHash = MakeValidationHash(state.PrefabHashID, flags, cornerChecks, math.asuint(minSdf == float.MaxValue ? 0f : minSdf));
            States[index] = state;
        }

        private static bool IsFiniteState(in BuilderGhostStateDTO state)
        {
            return math.all(math.isfinite(state.AUP_TargetPosition)) &&
                   math.all(math.isfinite(state.LocalToWorld.c0)) &&
                   math.all(math.isfinite(state.LocalToWorld.c1)) &&
                   math.all(math.isfinite(state.LocalToWorld.c2)) &&
                   math.all(math.isfinite(state.LocalToWorld.c3));
        }

        private static uint MakeValidationHash(uint prefabHash, uint flags, uint cornerChecks, uint sdfBits)
        {
            uint hash = ShinobuSocketConstructionRuntime.FoldHash(2166136261u, prefabHash);
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, flags);
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, cornerChecks);
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, sdfBits);
            return hash;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockBuilderGhostValidationJob : IJobParallelFor
    {
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<BuilderGhostStateDTO> States;
        public double3 RuntimeOriginAup;
        public float GridSizeMeters;
        public uint BasePrefabHash;
        public uint Frame;

        public void Execute(int index)
        {
            if (!States.IsCreated || (uint)index >= (uint)States.Length)
                return;

            int x = index % 100;
            int z = index / 100;
            double3 aup = new double3(x * GridSizeMeters, -40.0d, z * GridSizeMeters);
            double3 runtimeDouble = aup - RuntimeOriginAup;
            float3 runtime = new float3((float)runtimeDouble.x, (float)runtimeDouble.y, (float)runtimeDouble.z);
            float terrainFake = math.sin((x * 0.173f) + (z * 0.097f));
            uint flags = BuilderGhostValidationFlags.Active |
                         BuilderGhostValidationFlags.PresentationOnly |
                         BuilderGhostValidationFlags.RollbackExcluded |
                         BuilderGhostValidationFlags.GridSnapped;
            if (terrainFake < -0.72f)
                flags |= BuilderGhostValidationFlags.SdfBlocked;
            else
                flags |= BuilderGhostValidationFlags.Valid;

            BuilderGhostStateDTO state;
            state.LocalToWorld = float4x4.TRS(runtime, quaternion.identity, new float3(4f, 3f, 4f));
            state.AUP_TargetPosition = aup;
            state.PrefabHashID = BasePrefabHash + (uint)index;
            state.ValidationFlags = flags;
            state.AnimationPhase = math.frac((Frame * 0.013f) + (index * 0.001f));
            state.ValidationStateHash = ShinobuSocketConstructionRuntime.MakeResultHash(state.PrefabHashID, flags, (uint)index, Frame);
            state._pad0 = 0u;
            state._pad1 = 0u;
            state._pad2 = 0u;
            state._pad3 = 0u;
            state._pad4 = 0u;
            state._pad5 = 0u;
            States[index] = state;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct BuildBuilderGhostIndirectArgsJob : IJob
    {
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<BuilderGhostIndirectArgsDTO> Args;
        public uint InstanceCount;

        public void Execute()
        {
            if (!Args.IsCreated || Args.Length <= 0)
                return;

            BuilderGhostIndirectArgsDTO args;
            args.VertexCountPerInstance = ShinobuSocketConstructionRuntime.BuilderGhostProceduralVertexCount;
            args.InstanceCount = InstanceCount;
            args.StartVertex = 0u;
            args.StartInstance = 0u;
            Args[0] = args;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct RecordHolographyTelemetryJob : IJob
    {
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<HolographyTelemetryEntry> TelemetryRing;
        [ReadOnly, NoAlias] public NativeArray<BuilderGhostStateDTO> States;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> DumpRequest;
        public uint Frame;
        public uint SdfCornerChecks;
        public float SolverMicroseconds;
        public float MinSdfDistance;
        public float GlobalQualityWeight;
        public int StateIndex;

        public void Execute()
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0 || !States.IsCreated || States.Length <= 0)
                return;

            int stateIndex = math.clamp(StateIndex, 0, States.Length - 1);
            BuilderGhostStateDTO state = States[stateIndex];
            int ringIndex = (int)(Frame % (uint)math.min(TelemetryRing.Length, ShinobuSocketConstructionRuntime.TelemetryCapacity));
            HolographyTelemetryEntry entry;
            entry.AUP_TargetPosition = state.AUP_TargetPosition;
            entry.Frame = Frame;
            entry.PrefabHashID = state.PrefabHashID;
            entry.SdfCornerChecks = SdfCornerChecks;
            entry.ValidationFlags = state.ValidationFlags;
            entry.SolverMicroseconds = math.isfinite(SolverMicroseconds) ? SolverMicroseconds : -1f;
            entry.MinSdfDistance = math.isfinite(MinSdfDistance) ? MinSdfDistance : -9999f;
            entry.ValidationStateHash = state.ValidationStateHash;
            entry.GlobalQualityWeight = ShinobuSocketConstructionRuntime.SanitizeQuality(GlobalQualityWeight);
            entry._pad0 = 0u;
            entry._pad1 = 0u;
            TelemetryRing[ringIndex] = entry;

            if (DumpRequest.IsCreated &&
                DumpRequest.Length > 0 &&
                (entry.SolverMicroseconds > 500f ||
                 entry.SolverMicroseconds < 0f ||
                 (state.ValidationFlags & BuilderGhostValidationFlags.NonFinite) != 0u))
            {
                DumpRequest[0] = 1;
            }
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
