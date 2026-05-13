using Hecton8.Core;
using Hecton8.Core.Signals;
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

        public NativeQueue<ItemAcquiredSignal>.ParallelWriter ItemAcquiredWriter;
        public NativeQueue<AcousticPingSignal>.ParallelWriter AcousticPingWriter;
        public NativeQueue<WakeGeneratedSignal>.ParallelWriter WakeGeneratedWriter;

        public void Execute(int index)
        {
            uint flags = EntityFlags[index];
            if ((flags & (LootEntityFlags.Active | LootEntityFlags.IsLoot)) !=
                (LootEntityFlags.Active | LootEntityFlags.IsLoot))
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
                EntityFlags[index] = flags & ~LootEntityFlags.Pulling;
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
                EmitAcquired(index, LowTierSnap != 0 ? PlayerAup : lootAup, distSq);
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
            EmitPresentationSignals(index, nextAup, velocity, distSq);
        }

        private void EmitAcquired(int index, AbsoluteUniversePosition positionAup, float distSq)
        {
            uint itemHash = EntityItemHashes[index];
            ushort quantity = EntityQuantities[index];
            ItemAcquiredWriter.Enqueue(new ItemAcquiredSignal
            {
                PositionAup = positionAup,
                ItemHash = itemHash,
                OreHash = itemHash,
                Quantity = quantity,
                SourceKind = LootMagnetConstants.ItemSourceLootMagnet,
                Flags = LootMagnetConstants.SignalFlagLootMagnet,
                Frame = Frame
            });

            AcousticPingWriter.Enqueue(new AcousticPingSignal
            {
                PositionAup = positionAup,
                RadiusMeters = math.sqrt(PullRadiusSq),
                Intensity01 = ResolveIntensity(distSq),
                SourceId = itemHash,
                Channel = AcousticPingSignal.ChannelLootZip,
                Flags = AcousticPingSignal.FlagLootZip
            });

            WakeGeneratedWriter.Enqueue(new WakeGeneratedSignal
            {
                PositionAup = positionAup,
                Velocity = float3.zero,
                SourceFlags = LootMagnetConstants.WakeSourceLootZip
            });
        }

        private void EmitPresentationSignals(int index, AbsoluteUniversePosition positionAup, float3 velocity, float distSq)
        {
            if ((index & (LootMagnetConstants.PresentationSignalStride - 1)) != 0)
                return;

            uint itemHash = EntityItemHashes[index];
            AcousticPingWriter.Enqueue(new AcousticPingSignal
            {
                PositionAup = positionAup,
                RadiusMeters = math.sqrt(PullRadiusSq),
                Intensity01 = ResolveIntensity(distSq),
                SourceId = itemHash,
                Channel = AcousticPingSignal.ChannelLootZip,
                Flags = AcousticPingSignal.FlagLootZip
            });

            WakeGeneratedWriter.Enqueue(new WakeGeneratedSignal
            {
                PositionAup = positionAup,
                Velocity = velocity,
                SourceFlags = LootMagnetConstants.WakeSourceLootZip
            });
        }

        private float ResolveIntensity(float distSq)
        {
            float radiusSq = math.max(PullRadiusSq, LootMagnetConstants.MinDistanceSq);
            return math.saturate(1f - (distSq * math.rcp(radiusSq)));
        }

        private static float3 ResolveDeltaToPlayer(
            in AbsoluteUniversePosition lootAup,
            in AbsoluteUniversePosition playerAup)
        {
            double3 lootAbsolute = lootAup.ToAbsoluteDouble3();
            double3 playerAbsolute = playerAup.ToAbsoluteDouble3();
            double3 delta = playerAbsolute - lootAbsolute;
            return new float3((float)delta.x, (float)delta.y, (float)delta.z);
        }

        private static AbsoluteUniversePosition OffsetAup(in AbsoluteUniversePosition aup, float3 offsetMeters)
        {
            double3 absolute = aup.ToAbsoluteDouble3() + new double3(offsetMeters.x, offsetMeters.y, offsetMeters.z);
            return AbsoluteUniversePosition.FromAbsolutePosition(absolute);
        }
    }
}
