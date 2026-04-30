using System.Threading;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Receives an atomic floating-origin shift notification after the shift job
    /// has completed and the absolute-universe offset has been committed.
    /// </summary>
    public interface IOriginShiftListener
    {
        /// <summary>
        /// Reacts to a committed floating-origin shift.
        /// </summary>
        /// <param name="shiftData">Committed shift payload.</param>
        void OnOriginShift(in OriginShiftEventData shiftData);
    }

    /// <summary>
    /// Optional async extension for origin-shift listeners that must complete a job or buffer swap before physics resumes.
    /// </summary>
    public interface IAwaitableOriginShiftListener : IOriginShiftListener
    {
        /// <summary>
        /// Reacts to a committed floating-origin shift and returns only after listener-owned rebase state is stable.
        /// </summary>
        /// <param name="shiftData">Committed shift payload.</param>
        /// <param name="cancellationToken">Owner lifetime cancellation token.</param>
        Awaitable OnOriginShiftAsync(OriginShiftEventData shiftData, CancellationToken cancellationToken);
    }
}
