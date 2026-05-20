using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.World;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Construction
{
    public static class BulkheadContainmentMath
    {
        public static double3 ToAbsoluteDouble3(in AbsoluteUniversePosition aup)
        {
            double cell = AbsoluteUniversePosition.CellSizeMeters;
            return new double3(
                aup.GridX * cell + aup.LocalX,
                aup.GridY * cell + aup.LocalY,
                aup.GridZ * cell + aup.LocalZ);
        }

        public static double3 ToAbsoluteDouble3(in LockstepPlayerKinematicState state)
        {
            double cell = AbsoluteUniversePosition.CellSizeMeters;
            return new double3(
                state.SectorX * cell + state.LocalPosition.x,
                state.SectorY * cell + state.LocalPosition.y,
                state.SectorZ * cell + state.LocalPosition.z);
        }

        public static float Sanitize01(float value, float fallback)
        {
            return math.isfinite(value) ? math.saturate(value) : math.saturate(fallback);
        }

        public static float SanitizePositive(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }

        public static float3 SafeNormal(float3 value, float3 fallback)
        {
            if (!math.all(math.isfinite(value)))
                return fallback;

            float lenSq = math.lengthsq(value);
            return lenSq > 1e-6f ? value * math.rsqrt(lenSq) : fallback;
        }

        public static uint Hash(uint seed, uint value)
        {
            uint hash = seed ^ value;
            hash *= 16777619u;
            hash ^= hash >> 13;
            hash *= 3266489917u;
            return hash;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockBulkheadsJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public BulkheadStateDTO* States;
        [NativeDisableUnsafePtrRestriction, NoAlias] public double3* Aups;
        [NativeDisableUnsafePtrRestriction, NoAlias] public BulkheadPlaneDTO* Planes;
        [NativeDisableUnsafePtrRestriction, NoAlias] public BulkheadCsrEdgeDTO* CsrEdges;
        public int Count;
        public double3 OriginAup;
        public uint Seed;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count)
                return;

            uint edgeHash = BulkheadContainmentMath.Hash(Seed, (uint)index + 1u);
            double3 center = OriginAup + new double3((index & 7) * 6.0, 0.0, (index >> 3) * 7.0);
            ref BulkheadStateDTO state = ref UnsafeUtility.AsRef<BulkheadStateDTO>(States + index);
            state.EdgeHashID = edgeHash;
            state.ClosureProgress = 0f;
            state.AssociatedLock = 0u;
            state.SiblingNodeHash = BulkheadContainmentMath.Hash(edgeHash, 0xBADC0DEu);
            state.Flags = BulkheadStateFlags.Active;

            Aups[index] = center;
            Planes[index] = new BulkheadPlaneDTO
            {
                CenterAup = center,
                Normal = new float3(0f, 0f, 1f),
                WidthMeters = 2.6f,
                HeightMeters = 3.2f,
                HalfThicknessMeters = 0.18f,
                EdgeHashID = edgeHash,
                Flags = BulkheadStateFlags.Active,
                IntegrityIndex = (uint)index
            };
            CsrEdges[index] = new BulkheadCsrEdgeDTO
            {
                EdgeHashID = edgeHash,
                ConductivityIndex = index,
                FluidFlowIndex = index,
                OpenConductivity = 1f,
                OpenFluidFlow = 1f,
                IntegrityIndex = index,
                Flags = BulkheadStateFlags.Active
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct UpdateBulkheadClosureJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public BulkheadStateDTO* States;
        public int Count;
        public float DeltaSeconds;
        public float CloseSpeedPerSecond;
        public float OpenSpeedPerSecond;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count)
                return;

            ref BulkheadStateDTO state = ref UnsafeUtility.AsRef<BulkheadStateDTO>(States + index);
            if ((state.Flags & BulkheadStateFlags.Active) == 0u)
                return;

            float previous = BulkheadContainmentMath.Sanitize01(state.ClosureProgress, 0f);
            if ((state.Flags & BulkheadStateFlags.Destroyed) != 0u)
            {
                state.ClosureProgress = 0.73f;
                state.Flags &= ~BulkheadStateFlags.Sealed;
                return;
            }

            if ((state.Flags & BulkheadStateFlags.Jammed) != 0u)
            {
                state.ClosureProgress = math.saturate(math.max(previous, 0.73f));
                state.Flags &= ~BulkheadStateFlags.Sealed;
                return;
            }

            float dt = math.clamp(DeltaSeconds, 0f, 0.1f);
            float q = BulkheadContainmentMath.Sanitize01(GlobalQualityWeight, 0f);
            float cadenceScale = math.lerp(0.75f, 1.2f, q);
            float target = (state.AssociatedLock & 1u) != 0u ? 1f : 0f;
            float speed = target > previous
                ? BulkheadContainmentMath.SanitizePositive(CloseSpeedPerSecond, 2f)
                : BulkheadContainmentMath.SanitizePositive(OpenSpeedPerSecond, 2.5f);
            float next = math.clamp(previous + math.sign(target - previous) * speed * cadenceScale * dt, 0f, 1f);
            if (math.abs(target - previous) <= speed * cadenceScale * dt)
                next = target;

            state.ClosureProgress = next;
            state.Flags = next >= 0.5f ? (state.Flags | BulkheadStateFlags.Closing) : (state.Flags & ~BulkheadStateFlags.Closing);
            state.Flags = next >= 0.95f ? (state.Flags | BulkheadStateFlags.Sealed) : (state.Flags & ~BulkheadStateFlags.Sealed);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ApplyBulkheadLockJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public BulkheadStateDTO* States;
        [NativeDisableUnsafePtrRestriction, NoAlias] public BulkheadCsrEdgeDTO* CsrEdges;
        [NativeDisableUnsafePtrRestriction, NoAlias] public float* EdgeConductivity;
        [NativeDisableUnsafePtrRestriction, NoAlias] public float* EdgeFluidFlow;
        public int Count;
        public int EdgeScalarCount;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count)
                return;

            ref BulkheadStateDTO state = ref UnsafeUtility.AsRef<BulkheadStateDTO>(States + index);
            ref BulkheadCsrEdgeDTO edge = ref UnsafeUtility.AsRef<BulkheadCsrEdgeDTO>(CsrEdges + index);
            if (state.EdgeHashID == 0u || edge.EdgeHashID != state.EdgeHashID)
                return;

            bool sealedEdge = state.ClosureProgress >= 0.95f &&
                              (state.Flags & BulkheadStateFlags.Destroyed) == 0u &&
                              (state.Flags & BulkheadStateFlags.Jammed) == 0u;
            if (sealedEdge)
                state.Flags |= BulkheadStateFlags.Sealed;
            else
                state.Flags &= ~BulkheadStateFlags.Sealed;

            if ((uint)edge.ConductivityIndex < (uint)EdgeScalarCount)
                EdgeConductivity[edge.ConductivityIndex] = sealedEdge ? 0f : math.max(edge.OpenConductivity, 0f);
            if ((uint)edge.FluidFlowIndex < (uint)EdgeScalarCount)
                EdgeFluidFlow[edge.FluidFlowIndex] = sealedEdge ? 0f : math.max(edge.OpenFluidFlow, 0f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ProcessDoorOverrideJob : IJob
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public InteractionUiSignal* Signals;
        [NativeDisableUnsafePtrRestriction, NoAlias] public BulkheadStateDTO* States;
        [NativeDisableUnsafePtrRestriction, NoAlias] public double3* Aups;
        public int SignalCount;
        public int StateCount;
        public double3 PlayerAup;
        public float OverrideDistanceMeters;

        public void Execute()
        {
            float maxDistance = BulkheadContainmentMath.SanitizePositive(OverrideDistanceMeters, 3.0f);
            double maxDistanceSq = (double)maxDistance * maxDistance;

            for (int signalIndex = 0; signalIndex < SignalCount; signalIndex++)
            {
                InteractionUiSignal signal = Signals[signalIndex];
                if (signal.State == 0)
                    continue;

                double3 signalAup = BulkheadContainmentMath.ToAbsoluteDouble3(in signal.TargetAup);
                for (int stateIndex = 0; stateIndex < StateCount; stateIndex++)
                {
                    ref BulkheadStateDTO state = ref UnsafeUtility.AsRef<BulkheadStateDTO>(States + stateIndex);
                    if ((state.Flags & BulkheadStateFlags.Active) == 0u)
                        continue;

                    bool hashMatch = signal.TargetHash != 0u && signal.TargetHash == state.EdgeHashID;
                    double3 center = Aups[stateIndex];
                    double3 playerDelta = PlayerAup - center;
                    double3 signalDelta = signalAup - center;
                    bool distanceMatch = math.dot(playerDelta, playerDelta) <= maxDistanceSq &&
                                         math.dot(signalDelta, signalDelta) <= maxDistanceSq;
                    if (!hashMatch && !distanceMatch)
                        continue;

                    state.AssociatedLock ^= 1u;
                    state.Flags |= BulkheadStateFlags.ManualOverride;
                    break;
                }
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvaluateDoorCollisionsJob : IJob
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public BulkheadStateDTO* States;
        [NativeDisableUnsafePtrRestriction, NoAlias] public BulkheadPlaneDTO* Planes;
        [NativeDisableUnsafePtrRestriction, NoAlias] public BulkheadCollisionResultDTO* Result;
        public int Count;
        public double3 PlayerStartAup;
        public double3 PlayerEndAup;
        public float PlayerRadiusMeters;
        public uint Frame;

        public void Execute()
        {
            BulkheadCollisionResultDTO best = default;
            float bestDepth = 0f;
            float radius = math.max(0.05f, PlayerRadiusMeters);

            for (int index = 0; index < Count; index++)
            {
                BulkheadStateDTO state = States[index];
                if ((state.Flags & BulkheadStateFlags.Active) == 0u ||
                    (state.Flags & BulkheadStateFlags.Destroyed) != 0u ||
                    state.ClosureProgress <= 0.5f)
                {
                    continue;
                }

                BulkheadPlaneDTO plane = Planes[index];
                float3 normal = BulkheadContainmentMath.SafeNormal(plane.Normal, new float3(0f, 0f, 1f));
                double3 startDeltaD = PlayerStartAup - plane.CenterAup;
                double3 endDeltaD = PlayerEndAup - plane.CenterAup;
                float3 startDelta = (float3)startDeltaD;
                float3 endDelta = (float3)endDeltaD;
                float signedStart = math.dot(startDelta, normal);
                float signedEnd = math.dot(endDelta, normal);
                float halfThickness = math.max(0.05f, plane.HalfThicknessMeters);
                float depth = halfThickness + radius - math.abs(signedEnd);
                bool crosses = signedStart == 0f || signedEnd == 0f || math.sign(signedStart) != math.sign(signedEnd);
                bool insideSlab = depth > 0f || crosses;
                if (!insideSlab)
                    continue;

                float3 axisSeed = math.abs(normal.y) < 0.8f ? new float3(0f, 1f, 0f) : new float3(1f, 0f, 0f);
                float3 tangent = BulkheadContainmentMath.SafeNormal(math.cross(axisSeed, normal), new float3(1f, 0f, 0f));
                float3 bitangent = BulkheadContainmentMath.SafeNormal(math.cross(normal, tangent), new float3(0f, 1f, 0f));
                float halfWidth = math.max(0.1f, plane.WidthMeters * 0.5f) + radius;
                float halfHeight = math.max(0.1f, plane.HeightMeters * 0.5f) + radius;
                if (math.abs(math.dot(endDelta, tangent)) > halfWidth ||
                    math.abs(math.dot(endDelta, bitangent)) > halfHeight)
                {
                    continue;
                }

                float resolvedDepth = math.max(depth, halfThickness + radius);
                if (resolvedDepth <= bestDepth)
                    continue;

                bestDepth = resolvedDepth;
                best.Normal = signedEnd >= 0f ? normal : -normal;
                best.DepthMeters = resolvedDepth;
                best.EdgeHashID = state.EdgeHashID;
                best.Flags = ((state.Flags & BulkheadStateFlags.Jammed) != 0u)
                    ? (BulkheadCollisionFlags.Blocked | BulkheadCollisionFlags.Jammed)
                    : BulkheadCollisionFlags.Blocked;
                best.ClosureProgress = state.ClosureProgress;
                best.Frame = Frame;
            }

            Result[0] = best;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ApplyCatastrophicDoorDamageJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public BulkheadStateDTO* States;
        [NativeDisableUnsafePtrRestriction, NoAlias] public BulkheadCsrEdgeDTO* CsrEdges;
        [NativeDisableUnsafePtrRestriction, NoAlias] public float* ParentModuleIntegrity01;
        public int Count;
        public int IntegrityCount;
        public float CatastrophicIntegrity01;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count)
                return;

            ref BulkheadStateDTO state = ref UnsafeUtility.AsRef<BulkheadStateDTO>(States + index);
            ref BulkheadCsrEdgeDTO edge = ref UnsafeUtility.AsRef<BulkheadCsrEdgeDTO>(CsrEdges + index);
            if ((state.Flags & BulkheadStateFlags.Active) == 0u ||
                (uint)edge.IntegrityIndex >= (uint)IntegrityCount)
            {
                return;
            }

            float integrity01 = BulkheadContainmentMath.Sanitize01(ParentModuleIntegrity01[edge.IntegrityIndex], 1f);
            float threshold = BulkheadContainmentMath.Sanitize01(CatastrophicIntegrity01, 0.18f);
            if (integrity01 > threshold)
                return;

            state.ClosureProgress = 0.73f;
            state.AssociatedLock = 1u;
            state.SiblingNodeHash = 0u;
            state.Flags |= BulkheadStateFlags.Jammed | BulkheadStateFlags.Destroyed | BulkheadStateFlags.CatastrophicDamage;
            state.Flags &= ~BulkheadStateFlags.Sealed;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct RecordBulkheadTelemetryJob : IJob
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public BulkheadStateDTO* States;
        [NativeDisableUnsafePtrRestriction, NoAlias] public BulkheadCollisionResultDTO* CollisionResult;
        [NativeDisableUnsafePtrRestriction, NoAlias] public BulkheadTelemetryEntry* Telemetry;
        [NativeDisableUnsafePtrRestriction, NoAlias] public uint* Cursor;
        public int Count;
        public int TelemetryCount;
        public uint Frame;
        public float GlobalQualityWeight;
        public float AuthorityCadenceHz;
        public float LastScheduleMicroseconds;
        public uint Flags;

        public void Execute()
        {
            if (TelemetryCount <= 0 ||
                Telemetry == null ||
                Cursor == null ||
                CollisionResult == null ||
                (Count > 0 && States == null))
            {
                return;
            }

            uint active = 0u;
            uint sealedCount = 0u;
            uint jammed = 0u;
            float sumClosure = 0f;
            uint stateHash = 2166136261u;
            uint flags = Flags;

            for (int index = 0; index < Count; index++)
            {
                BulkheadStateDTO state = States[index];
                if ((state.Flags & BulkheadStateFlags.Active) == 0u)
                    continue;

                active++;
                float closure = state.ClosureProgress;
                if (!math.isfinite(closure))
                {
                    closure = 0f;
                    flags |= BulkheadTelemetryFlags.NonFinite | BulkheadTelemetryFlags.DumpRequested;
                }

                sumClosure += math.saturate(closure);
                sealedCount += (state.Flags & BulkheadStateFlags.Sealed) != 0u ? 1u : 0u;
                jammed += (state.Flags & BulkheadStateFlags.Jammed) != 0u ? 1u : 0u;
                stateHash = BulkheadContainmentMath.Hash(stateHash, state.EdgeHashID ^ math.asuint(math.saturate(closure)));
            }

            int cursor = (int)(Cursor[0] % (uint)TelemetryCount);
            BulkheadCollisionResultDTO collision = CollisionResult[0];
            Telemetry[cursor] = new BulkheadTelemetryEntry
            {
                Frame = Frame,
                ActiveCount = active,
                SealedCount = sealedCount,
                JammedCount = jammed,
                AverageClosure = active > 0u ? sumClosure / active : 0f,
                AuthorityCadenceHz = AuthorityCadenceHz,
                GlobalQualityWeight = BulkheadContainmentMath.Sanitize01(GlobalQualityWeight, 0f),
                LastScheduleMicroseconds = math.max(0f, LastScheduleMicroseconds),
                StateHash = stateHash,
                CollisionEdgeHash = collision.EdgeHashID,
                CollisionDepthMeters = collision.DepthMeters,
                Flags = flags
            };
            Cursor[0] = unchecked(Cursor[0] + 1u);
        }
    }
}
