#if UNITY_EDITOR
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Editor.HydraulicErosionForge
{
    internal static class HydraulicErosionChunkTransferBridge
    {
        public static double3 ResolveNeighborSectorAup(double3 sourceSectorAup, int directionX, int directionZ)
        {
            double step = HydraulicErosionForgeConstants.DefaultSectorSizeMeters;
            double3 neighbor = sourceSectorAup + new double3(directionX * step, 0.0, directionZ * step);
            return ErosionDeterminismHash.QuantizeAupToMillimeters(neighbor);
        }

        public static int ConsumeIncomingQueue(
            NativeQueue<ErosionDropletDTO> incoming,
            NativeArray<ErosionDropletDTO> droplets,
            int startIndex,
            int maxCount)
        {
            if (!incoming.IsCreated || !droplets.IsCreated)
                return 0;

            int cursor = math.clamp(startIndex, 0, droplets.Length);
            int limit = math.min(droplets.Length, cursor + math.max(0, maxCount));
            int consumed = 0;
            while (cursor < limit && incoming.TryDequeue(out ErosionDropletDTO droplet))
            {
                droplets[cursor] = droplet;
                cursor++;
                consumed++;
            }

            return consumed;
        }
    }
}
#endif
