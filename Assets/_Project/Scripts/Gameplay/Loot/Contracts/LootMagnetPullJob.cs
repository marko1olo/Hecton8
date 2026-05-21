using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Gameplay.Loot.Contracts
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct LootMagnetJob : IJobParallelFor
    {
        public AbsoluteUniversePosition PlayerAup;
        public float DeltaTimeSeconds;
        public float PullRadiusSq;
        public float PullStrength;
        public float MaxVelocityMetersPerSecond;
        public uint Frame;

        [NoAlias] public NativeArray<AbsoluteUniversePosition> EntityAups;
        [NoAlias] public NativeArray<uint> EntityFlags;
        [NoAlias] public NativeArray<float3> EntityVelocities;
        [ReadOnly, NoAlias] public NativeArray<uint> EntityItemHashes;
        [ReadOnly, NoAlias] public NativeArray<ushort> EntityQuantities;
        [WriteOnly, NoAlias] public NativeArray<LootMagnetSignalEvent> SignalEvents;

        public void Execute(int index)
        {
            SignalEvents[index] = default;
            uint flags = EntityFlags[index];
            const uint requiredFlags = LootEntityFlags.Active | LootEntityFlags.IsLoot | LootEntityFlags.Bit_IsMagnetic;
            if ((flags & requiredFlags) != requiredFlags)
            {
                return;
            }

            AbsoluteUniversePosition lootAup = EntityAups[index];
            if (!IsFiniteAup(in lootAup) || !IsFiniteAup(in PlayerAup))
            {
                EntityVelocities[index] = float3.zero;
                EntityFlags[index] = flags | LootEntityFlags.NonFinite;
                return;
            }

            if (!TryResolveKernelParameters(
                    out float safeDeltaTime,
                    out float pullRadiusSq,
                    out float pullStrength,
                    out float maxVelocityMetersPerSecond))
            {
                EntityVelocities[index] = float3.zero;
                EntityFlags[index] = (flags & ~(LootEntityFlags.Pulling | LootEntityFlags.LowTierLerp)) |
                                     LootEntityFlags.NonFinite;
                return;
            }

            if (pullRadiusSq <= LootMagnetConstants.AupCellSizeSq &&
                IsOutsideAdjacentAupCells(in lootAup, in PlayerAup))
            {
                EntityFlags[index] = flags & ~(LootEntityFlags.Pulling | LootEntityFlags.LowTierLerp);
                return;
            }

            float3 toPlayer = ResolveDeltaToPlayer(in lootAup, in PlayerAup);
            float distSq = math.lengthsq(toPlayer);
            if (!math.isfinite(distSq))
            {
                EntityFlags[index] = flags | LootEntityFlags.NonFinite;
                return;
            }

            if (distSq > pullRadiusSq)
            {
                EntityFlags[index] = flags & ~(LootEntityFlags.Pulling | LootEntityFlags.LowTierLerp);
                return;
            }

            if (distSq <= LootMagnetConstants.AcquireDistanceSq)
            {
                EntityVelocities[index] = float3.zero;
                EntityFlags[index] = (flags & ~LootEntityFlags.Active) |
                                     LootEntityFlags.Flag_Acquired |
                                     LootEntityFlags.Pulling;
                WriteSignalEvent(
                    index,
                    lootAup,
                    float3.zero,
                    distSq,
                    LootMagnetEventFlags.Acquired | LootMagnetEventFlags.Acoustic | LootMagnetEventFlags.Wake);
                return;
            }

            float3 velocity = EntityVelocities[index];
            float safeRsqrtDistSq = math.max(distSq, LootMagnetConstants.MinRsqrtDistanceSq);
            float safeForceDistSq = math.max(distSq, LootMagnetConstants.MinForceDistanceSq);
            float3 dir = toPlayer * math.rsqrt(safeRsqrtDistSq);
            velocity += dir * (pullStrength * math.rcp(safeForceDistSq)) * safeDeltaTime;
            float speedSq = math.lengthsq(velocity);
            float maxSpeedSq = maxVelocityMetersPerSecond * maxVelocityMetersPerSecond;
            if (speedSq > maxSpeedSq)
                velocity *= math.rsqrt(math.max(speedSq, LootMagnetConstants.MinRsqrtDistanceSq)) * maxVelocityMetersPerSecond;

            if (!math.all(math.isfinite(velocity)))
            {
                EntityVelocities[index] = float3.zero;
                EntityFlags[index] = flags | LootEntityFlags.NonFinite;
                return;
            }

            AbsoluteUniversePosition nextAup = OffsetAup(in lootAup, velocity * safeDeltaTime);
            if (!IsFiniteAup(in nextAup))
            {
                EntityVelocities[index] = float3.zero;
                EntityFlags[index] = flags | LootEntityFlags.NonFinite;
                return;
            }

            EntityVelocities[index] = velocity;
            EntityAups[index] = nextAup;
            EntityFlags[index] = (flags | LootEntityFlags.Pulling) & ~LootEntityFlags.LowTierLerp;
            if ((index & (LootMagnetConstants.PresentationSignalStride - 1)) == 0)
            {
                WriteSignalEvent(
                    index,
                    nextAup,
                    velocity,
                    distSq,
                    LootMagnetEventFlags.Acoustic | LootMagnetEventFlags.Wake);
            }
        }

        private bool TryResolveKernelParameters(
            out float safeDeltaTime,
            out float pullRadiusSq,
            out float pullStrength,
            out float maxVelocityMetersPerSecond)
        {
            safeDeltaTime = DeltaTimeSeconds;
            pullRadiusSq = PullRadiusSq;
            pullStrength = PullStrength;
            maxVelocityMetersPerSecond = MaxVelocityMetersPerSecond;

            if (!math.isfinite(DeltaTimeSeconds) ||
                !math.isfinite(PullRadiusSq) ||
                !math.isfinite(PullStrength) ||
                !math.isfinite(MaxVelocityMetersPerSecond) ||
                DeltaTimeSeconds <= 0f ||
                PullRadiusSq < LootMagnetConstants.AcquireDistanceSq ||
                PullStrength < 0f ||
                MaxVelocityMetersPerSecond <= 0f)
            {
                return false;
            }

            safeDeltaTime = math.clamp(
                DeltaTimeSeconds,
                0.0001f,
                LootMagnetConstants.MaxIntegrationDeltaTimeSeconds);
            pullRadiusSq = math.clamp(
                PullRadiusSq,
                LootMagnetConstants.AcquireDistanceSq,
                LootMagnetConstants.MaxStablePullRadiusMeters * LootMagnetConstants.MaxStablePullRadiusMeters);
            pullStrength = math.clamp(PullStrength, 0f, LootMagnetConstants.MaxStablePullStrength);
            maxVelocityMetersPerSecond = math.clamp(
                MaxVelocityMetersPerSecond,
                0.01f,
                LootMagnetConstants.MaxStableVelocityMetersPerSecond);
            return true;
        }

        private void WriteSignalEvent(
            int index,
            AbsoluteUniversePosition positionAup,
            float3 velocity,
            float distSq,
            uint eventFlags)
        {
            SignalEvents[index] = new LootMagnetSignalEvent
            {
                PositionAup = positionAup,
                Velocity = velocity,
                ItemHash = EntityItemHashes[index],
                Quantity = EntityQuantities[index],
                DistanceSq = distSq,
                Frame = Frame,
                Flags = eventFlags
            };
        }

        private static float3 ResolveDeltaToPlayer(
            in AbsoluteUniversePosition lootAup,
            in AbsoluteUniversePosition playerAup)
        {
            double cellSize = LootMagnetConstants.AupCellSizeMeters;
            double3 delta = new double3(
                (((double)playerAup.GridX - lootAup.GridX) * cellSize) + playerAup.LocalX - lootAup.LocalX,
                (((double)playerAup.GridY - lootAup.GridY) * cellSize) + playerAup.LocalY - lootAup.LocalY,
                (((double)playerAup.GridZ - lootAup.GridZ) * cellSize) + playerAup.LocalZ - lootAup.LocalZ);
            return new float3((float)delta.x, (float)delta.y, (float)delta.z);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition aup)
        {
            return math.isfinite(aup.LocalX) &&
                   math.isfinite(aup.LocalY) &&
                   math.isfinite(aup.LocalZ);
        }

        private static bool IsOutsideAdjacentAupCells(
            in AbsoluteUniversePosition lootAup,
            in AbsoluteUniversePosition playerAup)
        {
            return IsAxisOutsideAdjacent(playerAup.GridX, lootAup.GridX) ||
                   IsAxisOutsideAdjacent(playerAup.GridY, lootAup.GridY) ||
                   IsAxisOutsideAdjacent(playerAup.GridZ, lootAup.GridZ);
        }

        private static bool IsAxisOutsideAdjacent(long playerGrid, long lootGrid)
        {
            if (playerGrid == lootGrid)
                return false;

            return playerGrid > lootGrid
                ? playerGrid - 1L > lootGrid
                : lootGrid - 1L > playerGrid;
        }

        private static AbsoluteUniversePosition OffsetAup(in AbsoluteUniversePosition aup, float3 offsetMeters)
        {
            if (!math.all(math.isfinite(offsetMeters)))
                return new AbsoluteUniversePosition { LocalX = float.NaN };

            double cellSize = LootMagnetConstants.AupCellSizeMeters;
            double3 absolute = new double3(
                ((double)aup.GridX * cellSize) + aup.LocalX + offsetMeters.x,
                ((double)aup.GridY * cellSize) + aup.LocalY + offsetMeters.y,
                ((double)aup.GridZ * cellSize) + aup.LocalZ + offsetMeters.z);
            return BuildAupFromAbsolute(absolute);
        }

        private static AbsoluteUniversePosition BuildAupFromAbsolute(double3 absolutePosition)
        {
            if (!math.all(math.isfinite(absolutePosition)))
                return new AbsoluteUniversePosition { LocalX = float.NaN };

            double cellSize = LootMagnetConstants.AupCellSizeMeters;
            long gridX = (long)math.floor(absolutePosition.x / cellSize);
            long gridY = (long)math.floor(absolutePosition.y / cellSize);
            long gridZ = (long)math.floor(absolutePosition.z / cellSize);
            double originX = gridX * cellSize;
            double originY = gridY * cellSize;
            double originZ = gridZ * cellSize;
            return new AbsoluteUniversePosition
            {
                GridX = gridX,
                GridY = gridY,
                GridZ = gridZ,
                LocalX = (float)(absolutePosition.x - originX),
                LocalY = (float)(absolutePosition.y - originY),
                LocalZ = (float)(absolutePosition.z - originZ)
            };
        }
    }
}
