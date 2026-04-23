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
}
