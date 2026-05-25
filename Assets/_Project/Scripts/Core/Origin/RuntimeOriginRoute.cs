using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;
using AbsoluteUniversePosition = Hecton8.World.AbsoluteUniversePosition;

namespace Hecton8.Core
{
    /// <summary>
    /// Pure runtime-origin and source-id route. No queue ownership, no scene lookup, no allocation.
    /// </summary>
    public static class RuntimeOriginRoute
    {
        public static AbsoluteUniversePosition CurrentRuntimeOriginAup()
        {
            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            return math.all(math.isfinite(origin)) ? AbsoluteUniversePosition.FromAbsolutePosition(origin) : default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryRuntimePositionToAup(Vector3 runtimePosition, ref AbsoluteUniversePosition aup)
        {
            return TryRuntimePositionToAup(new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z), ref aup);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryRuntimePositionToAup(float3 runtimePosition, ref AbsoluteUniversePosition aup)
        {
            if (!math.all(math.isfinite(runtimePosition)))
                return false;

            AbsoluteUniversePosition originAup = CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            aup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return aup.IsFinite();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint FoldEntityIdToSourceId(ulong entityId)
        {
            uint hash = unchecked((uint)entityId ^ (uint)(entityId >> 32));
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return hash == 0u ? 1u : hash;
        }
    }
}
