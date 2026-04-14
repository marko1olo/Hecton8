using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Hecton8.Core
{
    /// <summary>
    /// Shared waterline hysteresis contract for systems that must not thrash
    /// when the player hovers around the surface boundary.
    /// </summary>
    internal static class SurfaceStateUtility
    {
        internal const float EnterUnderwaterDepth = 0.12f;
        internal const float ExitUnderwaterDepth = 0.03f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool ResolveUnderwaterFromDepth(float depth, bool wasUnderwater)
            => ResolveUnderwaterFromDepth(depth, wasUnderwater, EnterUnderwaterDepth, ExitUnderwaterDepth);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool ResolveUnderwaterFromDepth(
            float depth,
            bool wasUnderwater,
            float enterUnderwaterDepth,
            float exitUnderwaterDepth)
        {
            float clampedDepth = math.max(0f, depth);
            float clampedEnterDepth = math.max(0f, enterUnderwaterDepth);
            float clampedExitDepth = math.max(0f, math.min(exitUnderwaterDepth, clampedEnterDepth));

            if (wasUnderwater)
                return clampedDepth > clampedExitDepth;

            return clampedDepth >= clampedEnterDepth;
        }

    }
}
