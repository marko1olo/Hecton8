using System;
using System.Numerics;
namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for SignalPrioritySortCalculator.
    /// Extracted from SignalBusRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class SignalPrioritySortCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="priorityA">Parameter representing the priorityA (int).</param>
        /// <param name="timestampA">Parameter representing the timestampA (long).</param>
        /// <param name="priorityB">Parameter representing the priorityB (int).</param>
        /// <param name="timestampB">Parameter representing the timestampB (long).</param>
        /// <returns>Returns comparison result -1, 0, 1 of type int.</returns>
        public static int Compute(int priorityA, long timestampA, int priorityB, long timestampB)
        {
            // Higher priority always wins. Same priority: earlier timestamp wins. Identical: stable (0).
            if (priorityA > priorityB) return -1; // A comes first (higher priority)
            if (priorityA < priorityB) return 1;  // B comes first
            if (timestampA < timestampB) return -1; // A comes first (earlier timestamp)
            if (timestampA > timestampB) return 1;  // B comes first
            return 0; // Identical
        }
    }
}
