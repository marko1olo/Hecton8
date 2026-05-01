using Unity.Jobs;

namespace Hecton8.World
{
    /// <summary>
    /// Central job-fence helper for vegetation systems. Callers must use this only from dispatcher
    /// swap windows, teardown, or after an explicit <see cref="JobHandle.IsCompleted"/> gate.
    /// </summary>
    internal static class VegetationJobRecovery
    {
        public static bool TryComplete(ref JobHandle handle, bool forceComplete)
        {
            if (!forceComplete && !handle.IsCompleted)
                return false;

            handle.Complete();
            return true;
        }

        public static void Recover(ref JobHandle handle)
        {
            handle.Complete();
        }
    }
}
