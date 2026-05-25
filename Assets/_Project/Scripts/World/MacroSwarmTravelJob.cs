using Hecton8.Core.Contracts;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    /// <summary>
    /// Burst-compiled low-frequency macro travel integrator. Removal uses swap-with-last inside the native array.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct MacroSwarmTravelJob : IJob
    {
        [NoAlias] public NativeArray<MacroSwarm> Swarms;
        [NoAlias] public NativeArray<MacroSwarmArrival> Arrivals;
        [NoAlias] public NativeArray<int> Counters;
        public float DeltaSeconds;

        public void Execute()
        {
            if (!Swarms.IsCreated || !Counters.IsCreated || Counters.Length <= 0)
                return;

            int count = math.clamp(Counters[0], 0, Swarms.Length);
            int arrivalCount = 0;
            float dt = math.select(0f, math.max(0f, DeltaSeconds), math.isfinite(DeltaSeconds));
            int i = 0;
            while (i < count)
            {
                MacroSwarm swarm = Swarms[i];
                if (!IsValid(in swarm))
                {
                    count = RemoveAtSwapBack(i, count);
                    continue;
                }

                float2 target = new float2(swarm.TargetSectorAup.x, swarm.TargetSectorAup.y);
                float2 delta = target - swarm.CurrentSectorAup;
                float distanceSq = math.lengthsq(delta);
                float step = math.max(0f, swarm.Speed) * dt;
                if (distanceSq <= 0.0001f || step * step >= distanceSq)
                {
                    if (Arrivals.IsCreated && arrivalCount < Arrivals.Length)
                    {
                        Arrivals[arrivalCount++] = new MacroSwarmArrival
                        {
                            TargetSectorAup = swarm.TargetSectorAup,
                            BiomassValue = math.saturate(swarm.BiomassValue),
                            HashId = swarm.HashId
                        };
                    }

                    count = RemoveAtSwapBack(i, count);
                    continue;
                }

                float invDistance = math.rsqrt(math.max(distanceSq, 0.0001f));
                float2 next = swarm.CurrentSectorAup + delta * invDistance * step;
                if (!math.all(math.isfinite(next)))
                {
                    count = RemoveAtSwapBack(i, count);
                    continue;
                }

                swarm.CurrentSectorAup = next;
                swarm.SectorAup = new int2((int)math.floor(next.x), (int)math.floor(next.y));
                Swarms[i] = swarm;
                i++;
            }

            Counters[0] = count;
            if (Counters.Length > 1)
                Counters[1] = arrivalCount;
        }

        private int RemoveAtSwapBack(int index, int count)
        {
            count--;
            Swarms[index] = index < count ? Swarms[count] : default;
            Swarms[count] = default;
            return count;
        }

        private static bool IsValid(in MacroSwarm swarm)
        {
            return swarm.HashId != 0u &&
                   math.isfinite(swarm.BiomassValue) &&
                   math.isfinite(swarm.Speed) &&
                   math.all(math.isfinite(swarm.CurrentSectorAup)) &&
                   swarm.BiomassValue > 0.0001f &&
                   swarm.Speed > 0f;
        }
    }
}
