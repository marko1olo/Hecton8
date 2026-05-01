using Unity.Jobs;

namespace Hecton8.World
{
    /// <summary>
    /// Tracked backward-compatible vegetation job recovery facade.
    /// Real completion ownership is <see cref="VegetationLateFrameJobSwap"/>.
    /// </summary>
    internal static class VegetationJobRecovery
    {
        /// <summary>
        /// Completes a vegetation job during a caller-owned pause/teardown barrier.
        /// </summary>
        /// <param name="handle">Job handle to complete and clear.</param>
        public static void Recover(ref JobHandle handle)
        {
            VegetationLateFrameJobSwap.TryComplete(ref handle, forceComplete: true);
        }

        /// <summary>
        /// Completes a vegetation job only when already ready, unless forced by teardown.
        /// </summary>
        /// <param name="handle">Job handle to complete and clear.</param>
        /// <param name="forceComplete">True only inside teardown or explicit simulation pause barriers.</param>
        /// <returns>True when the handle was completed and cleared.</returns>
        public static bool TryComplete(ref JobHandle handle, bool forceComplete)
        {
            return VegetationLateFrameJobSwap.TryComplete(ref handle, forceComplete);
        }
    }
}
