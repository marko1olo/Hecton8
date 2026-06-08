using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Determinism;
using Hecton8.World;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Construction
{
    public static class BulkheadContainmentMath
    {
        public static double3 ToAbsoluteDouble3(in LockstepPlayerKinematicState state)
        {
            const double cell = HectonPhysicsContract.AupSectorSizeMetersDouble;
            return new double3(
                state.SectorX * cell + state.LocalPosition.x,
                state.SectorY * cell + state.LocalPosition.y,
                state.SectorZ * cell + state.LocalPosition.z);
        }

        public static double3 ToAbsoluteDouble3(in AbsoluteUniversePosition position)
        {
            const double cell = HectonPhysicsContract.AupSectorSizeMetersDouble;
            return new double3(
                position.GridX * cell + position.LocalX,
                position.GridY * cell + position.LocalY,
                position.GridZ * cell + position.LocalZ);
        }

        public static float Sanitize01(float value, float fallback)
        {
            return math.isfinite(value) ? math.saturate(value) : math.saturate(fallback);
        }

        public static float SanitizePositive(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }

        public static float SanitizeNonNegative(float value, float fallback)
        {
            return math.isfinite(value) && value >= 0f ? value : fallback;
        }

        public static float3 SafeNormal(float3 value, float3 fallback)
        {
            if (!math.all(math.isfinite(value)))
                return fallback;

            float lenSq = math.lengthsq(value);
            return math.isfinite(lenSq) && lenSq > 1e-6f ? value * math.rsqrt(lenSq) : fallback;
        }

        public static bool CanCastLocalDeltaToFloat3(double3 value)
        {
            const double maxFloatMagnitude = 3.4028234663852886e38;
            return math.all(math.isfinite(value)) && math.all(math.abs(value) <= maxFloatMagnitude);
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
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public unsafe struct UpdateBulkheadClosureJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public BulkheadStateDTO* States;
        public int Count;
        public float DeltaSeconds;
        public float CloseSpeedPerSecond;
        public float OpenSpeedPerSecond;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count ||
                States == null)
            {
                return;
            }

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
                state.ClosureProgress = previous < 0.73f ? 0.73f : previous;
                state.Flags &= ~BulkheadStateFlags.Sealed;
                return;
            }

            float dt = math.min(BulkheadContainmentMath.SanitizeNonNegative(DeltaSeconds, 0f), 0.1f);
            const float cadenceScale = 1.2f;
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
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public unsafe struct ApplyBulkheadLockJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public BulkheadStateDTO* States;
        [NativeDisableUnsafePtrRestriction, NoAlias] public BulkheadCsrEdgeDTO* CsrEdges;
        [NativeDisableUnsafePtrRestriction, NoAlias] public float* EdgeConductivity;
        [NativeDisableUnsafePtrRestriction, NoAlias] public float* EdgeFluidFlow;
        public int Count;
        public int ConductivityCount;
        public int FluidFlowCount;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count ||
                States == null ||
                CsrEdges == null ||
                EdgeConductivity == null ||
                EdgeFluidFlow == null ||
                ConductivityCount <= 0 ||
                FluidFlowCount <= 0)
            {
                return;
            }

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

            float openConductivity = BulkheadContainmentMath.SanitizeNonNegative(edge.OpenConductivity, 1f);
            float openFluidFlow = BulkheadContainmentMath.SanitizeNonNegative(edge.OpenFluidFlow, 1f);
            if ((uint)edge.ConductivityIndex < (uint)ConductivityCount)
                EdgeConductivity[edge.ConductivityIndex] = sealedEdge ? 0f : openConductivity;
            if ((uint)edge.FluidFlowIndex < (uint)FluidFlowCount)
                EdgeFluidFlow[edge.FluidFlowIndex] = sealedEdge ? 0f : openFluidFlow;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public unsafe struct ProcessDoorOverrideJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<InteractionUiSignal>.ReadOnly Signals;
        [NativeDisableUnsafePtrRestriction, NoAlias] public BulkheadStateDTO* States;
        [NativeDisableUnsafePtrRestriction, NoAlias] public double3* Aups;
        public int SignalCount;
        public int StateCount;
        public double3 PlayerAup;
        public float OverrideDistanceMeters;

        public void Execute()
        {
            if (!Signals.IsCreated ||
                SignalCount <= 0 ||
                StateCount <= 0 ||
                States == null ||
                Aups == null)
            {
                return;
            }

            if (!math.all(math.isfinite(PlayerAup)))
                return;

            float maxDistance = BulkheadContainmentMath.SanitizePositive(OverrideDistanceMeters, 3.0f);
            double maxDistanceSq = (double)maxDistance * maxDistance;
            int signalCount = math.min(SignalCount, Signals.Length);

            for (int signalIndex = 0; signalIndex < signalCount; signalIndex++)
            {
                InteractionUiSignal signal = Signals[signalIndex];
                if (signal.State == 0 ||
                    signal.ToolHash != BulkheadContainmentConstants.OverrideToolHash)
                {
                    continue;
                }

                double3 signalAup = default;
                bool signalAupResolved = false;
                bool signalAupFinite = false;
                for (int stateIndex = 0; stateIndex < StateCount; stateIndex++)
                {
                    ref BulkheadStateDTO state = ref UnsafeUtility.AsRef<BulkheadStateDTO>(States + stateIndex);
                    if ((state.Flags & BulkheadStateFlags.Active) == 0u)
                        continue;

                    bool hashMatch = signal.TargetHash != 0u && signal.TargetHash == state.EdgeHashID;
                    bool distanceMatch = false;
                    if (!hashMatch)
                    {
                        if (!signalAupResolved)
                        {
                            signalAup = BulkheadContainmentMath.ToAbsoluteDouble3(in signal.TargetAup);
                            signalAupFinite = math.all(math.isfinite(signalAup));
                            signalAupResolved = true;
                        }

                        if (!signalAupFinite)
                            continue;

                        double3 center = Aups[stateIndex];
                        if (!math.all(math.isfinite(center)))
                            continue;

                        double3 playerDelta = PlayerAup - center;
                        double3 signalDelta = signalAup - center;
                        distanceMatch = math.dot(playerDelta, playerDelta) <= maxDistanceSq &&
                                        math.dot(signalDelta, signalDelta) <= maxDistanceSq;
                    }

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
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
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
            best.Frame = Frame;
            if (Result == null)
                return;

            if (Count <= 0 ||
                States == null ||
                Planes == null)
            {
                Result[0] = best;
                return;
            }

            if (!math.all(math.isfinite(PlayerStartAup)) ||
                !math.all(math.isfinite(PlayerEndAup)))
            {
                best.Flags = BulkheadCollisionFlags.NonFinite;
                Result[0] = best;
                return;
            }

            float bestDepth = 0f;
            float radius = BulkheadContainmentMath.SanitizePositive(PlayerRadiusMeters, 0.05f);
            const float crossEpsilon = BulkheadContainmentConstants.PlaneCrossEpsilonMeters;

            for (int index = 0; index < Count; index++)
            {
                BulkheadStateDTO state = States[index];
                float closure = state.ClosureProgress;
                if ((state.Flags & BulkheadStateFlags.Active) == 0u ||
                    (state.Flags & BulkheadStateFlags.Destroyed) != 0u ||
                    state.EdgeHashID == 0u)
                {
                    continue;
                }

                if (!math.isfinite(closure))
                {
                    best.Flags |= BulkheadCollisionFlags.NonFinite;
                    continue;
                }

                if (closure <= 0.5f)
                    continue;

                BulkheadPlaneDTO plane = Planes[index];
                if (!math.all(math.isfinite(plane.CenterAup)) ||
                    !math.all(math.isfinite(plane.Normal)) ||
                    !math.isfinite(plane.WidthMeters) ||
                    !math.isfinite(plane.HeightMeters) ||
                    !math.isfinite(plane.HalfThicknessMeters))
                {
                    best.Flags |= BulkheadCollisionFlags.NonFinite;
                    continue;
                }

                float3 normal = BulkheadContainmentMath.SafeNormal(plane.Normal, new float3(0f, 0f, 1f));
                double3 startDeltaD = PlayerStartAup - plane.CenterAup;
                double3 endDeltaD = PlayerEndAup - plane.CenterAup;
                if (!BulkheadContainmentMath.CanCastLocalDeltaToFloat3(startDeltaD) ||
                    !BulkheadContainmentMath.CanCastLocalDeltaToFloat3(endDeltaD))
                {
                    best.Flags |= BulkheadCollisionFlags.NonFinite;
                    continue;
                }

                float3 startDelta = (float3)startDeltaD;
                float3 endDelta = (float3)endDeltaD;
                if (!math.all(math.isfinite(startDelta)) ||
                    !math.all(math.isfinite(endDelta)))
                {
                    best.Flags |= BulkheadCollisionFlags.NonFinite;
                    continue;
                }

                float signedStart = math.dot(startDelta, normal);
                float signedEnd = math.dot(endDelta, normal);
                float halfThickness = math.max(0.05f, BulkheadContainmentMath.SanitizePositive(plane.HalfThicknessMeters, 0.05f));
                float depth = halfThickness + radius - math.abs(signedEnd);
                bool crosses = math.abs(signedStart) <= crossEpsilon ||
                               math.abs(signedEnd) <= crossEpsilon ||
                               math.sign(signedStart) != math.sign(signedEnd);
                bool insideSlab = depth > 0f || crosses;
                if (!insideSlab)
                    continue;

                float3 axisSeed = math.abs(normal.y) < 0.8f ? new float3(0f, 1f, 0f) : new float3(1f, 0f, 0f);
                float3 tangent = BulkheadContainmentMath.SafeNormal(math.cross(axisSeed, normal), new float3(1f, 0f, 0f));
                float3 bitangent = BulkheadContainmentMath.SafeNormal(math.cross(normal, tangent), new float3(0f, 1f, 0f));
                float halfWidth = math.max(0.1f, BulkheadContainmentMath.SanitizePositive(plane.WidthMeters, 0.2f) * 0.5f) + radius;
                float halfHeight = math.max(0.1f, BulkheadContainmentMath.SanitizePositive(plane.HeightMeters, 0.2f) * 0.5f) + radius;
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
                uint diagnosticFlags = best.Flags & BulkheadCollisionFlags.NonFinite;
                uint blockFlags = (state.Flags & BulkheadStateFlags.Jammed) != 0u
                    ? (BulkheadCollisionFlags.Blocked | BulkheadCollisionFlags.Jammed)
                    : BulkheadCollisionFlags.Blocked;
                best.Flags = diagnosticFlags | blockFlags;
                best.ClosureProgress = BulkheadContainmentMath.Sanitize01(closure, 0f);
                best.Frame = Frame;
            }

            Result[0] = best;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
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
            if ((uint)index >= (uint)Count ||
                States == null ||
                CsrEdges == null ||
                ParentModuleIntegrity01 == null ||
                IntegrityCount <= 0)
            {
                return;
            }

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
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
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
        public ulong IntentCounters0;
        public ulong IntentCounters1;

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

                float closure01 = BulkheadContainmentMath.Sanitize01(closure, 0f);
                sumClosure += closure01;
                sealedCount += (state.Flags & BulkheadStateFlags.Sealed) != 0u ? 1u : 0u;
                jammed += (state.Flags & BulkheadStateFlags.Jammed) != 0u ? 1u : 0u;
                stateHash = BulkheadContainmentMath.Hash(stateHash, state.EdgeHashID ^ math.asuint(closure01));
            }

            int cursor = (int)(Cursor[0] % (uint)TelemetryCount);
            BulkheadCollisionResultDTO collision = CollisionResult[0];
            if ((collision.Flags & BulkheadCollisionFlags.NonFinite) != 0u ||
                !math.isfinite(collision.DepthMeters) ||
                !math.all(math.isfinite(collision.Normal)))
            {
                flags |= BulkheadTelemetryFlags.NonFinite | BulkheadTelemetryFlags.DumpRequested;
            }

            float collisionDepth = BulkheadContainmentMath.SanitizePositive(collision.DepthMeters, 0f);
            Telemetry[cursor] = new BulkheadTelemetryEntry
            {
                Frame = Frame,
                ActiveCount = active,
                SealedCount = sealedCount,
                JammedCount = jammed,
                AverageClosure = active > 0u ? sumClosure / active : 0f,
                AuthorityCadenceHz = BulkheadContainmentMath.SanitizePositive(AuthorityCadenceHz, 5f),
                GlobalQualityWeight = BulkheadContainmentMath.Sanitize01(GlobalQualityWeight, 0f),
                LastScheduleMicroseconds = BulkheadContainmentMath.SanitizePositive(LastScheduleMicroseconds, 0f),
                StateHash = stateHash,
                CollisionEdgeHash = collision.EdgeHashID,
                CollisionDepthMeters = collisionDepth,
                Flags = flags,
                Reserved0 = IntentCounters0,
                Reserved1 = IntentCounters1
            };
            Cursor[0] = unchecked(Cursor[0] + 1u);
        }
    }
}
