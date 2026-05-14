using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Gameplay.Loot.Contracts
{
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct LootMagnetPullJob : IJobParallelFor
    {
        public AbsoluteUniversePosition PlayerAup;
        public float DeltaTimeSeconds;
        public float PullRadiusSq;
        public float PullStrength;
        public float MaxVelocityMetersPerSecond;
        public uint Frame;
        public byte LowTierSnap;

        public NativeArray<AbsoluteUniversePosition> EntityAups;
        public NativeArray<uint> EntityFlags;
        public NativeArray<float3> EntityVelocities;
        [ReadOnly] public NativeArray<uint> EntityItemHashes;
        [ReadOnly] public NativeArray<ushort> EntityQuantities;
        public NativeArray<LootMagnetSignalEvent> SignalEvents;

        public void Execute(int index)
        {
            SignalEvents[index] = default;
            uint flags = EntityFlags[index];
            const uint requiredFlags = LootEntityFlags.Active | LootEntityFlags.IsLoot | LootEntityFlags.PullEnabled;
            if ((flags & requiredFlags) != requiredFlags)
            {
                return;
            }

            AbsoluteUniversePosition lootAup = EntityAups[index];
            float3 toPlayer = ResolveDeltaToPlayer(in lootAup, in PlayerAup);
            float distSq = math.lengthsq(toPlayer);
            if (!math.isfinite(distSq))
            {
                EntityFlags[index] = flags | LootEntityFlags.NonFinite;
                return;
            }

            if (distSq > PullRadiusSq)
            {
                EntityFlags[index] = flags & ~(LootEntityFlags.Pulling | LootEntityFlags.LowTierSnap);
                return;
            }

            if (LowTierSnap != 0 || distSq <= LootMagnetConstants.AcquireDistanceSq)
            {
                EntityVelocities[index] = float3.zero;
                EntityFlags[index] = (flags & ~LootEntityFlags.Active) |
                                     LootEntityFlags.Acquired |
                                     LootEntityFlags.Pulling |
                                     (LowTierSnap != 0 ? LootEntityFlags.LowTierSnap : 0u);
                EntityAups[index] = LowTierSnap != 0 ? PlayerAup : lootAup;
                WriteSignalEvent(
                    index,
                    LowTierSnap != 0 ? PlayerAup : lootAup,
                    float3.zero,
                    distSq,
                    LootMagnetEventFlags.Acquired | LootMagnetEventFlags.Acoustic | LootMagnetEventFlags.Wake);
                return;
            }

            float3 velocity = EntityVelocities[index];
            float safeDistSq = math.max(distSq, LootMagnetConstants.MinDistanceSq);
            float3 dir = toPlayer * math.rsqrt(safeDistSq);
            velocity += dir * PullStrength * DeltaTimeSeconds * math.rcp(safeDistSq);
            float speedSq = math.lengthsq(velocity);
            float maxSpeedSq = MaxVelocityMetersPerSecond * MaxVelocityMetersPerSecond;
            if (speedSq > maxSpeedSq)
                velocity *= math.rsqrt(speedSq) * MaxVelocityMetersPerSecond;

            if (!math.all(math.isfinite(velocity)))
            {
                EntityVelocities[index] = float3.zero;
                EntityFlags[index] = flags | LootEntityFlags.NonFinite;
                return;
            }

            AbsoluteUniversePosition nextAup = OffsetAup(in lootAup, velocity * DeltaTimeSeconds);

            EntityVelocities[index] = velocity;
            EntityAups[index] = nextAup;
            EntityFlags[index] = flags | LootEntityFlags.Pulling |
                                 (LowTierSnap != 0 ? LootEntityFlags.LowTierSnap : 0u);
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

        private static AbsoluteUniversePosition OffsetAup(in AbsoluteUniversePosition aup, float3 offsetMeters)
        {
            double cellSize = LootMagnetConstants.AupCellSizeMeters;
            double3 absolute = new double3(
                ((double)aup.GridX * cellSize) + aup.LocalX + offsetMeters.x,
                ((double)aup.GridY * cellSize) + aup.LocalY + offsetMeters.y,
                ((double)aup.GridZ * cellSize) + aup.LocalZ + offsetMeters.z);
            return AbsoluteUniversePosition.FromAbsolutePosition(absolute);
        }
    }
}
