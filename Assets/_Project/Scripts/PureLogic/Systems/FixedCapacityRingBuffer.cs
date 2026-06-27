using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for FixedCapacityRingBuffer.
    /// Extracted from SignalBusRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class FixedCapacityRingBuffer
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="head">Parameter representing the head (int).</param>
        /// <param name="tail">Parameter representing the tail (int).</param>
        /// <param name="capacity">Parameter representing the capacity (int).</param>
        /// <param name="isPush">Parameter representing the isPush (bool).</param>
        /// <returns>Returns newHead or newTail, bool (success) of type int.</returns>
        public static int Calculate(int head, int tail, int capacity, bool isPush)
        {
            if (capacity <= 0)
                return -1; // Indicate failure

            // Clamp out-of-bounds bounds to 0
            if (head < 0 || head >= capacity)
                head = 0;
            if (tail < 0 || tail >= capacity)
                tail = 0;

            if (isPush)
            {
                int nextHead = (head + 1) % capacity;
                if (nextHead == tail)
                    return -1; // Failure: Buffer full
                return nextHead;
            }
            else
            {
                if (head == tail)
                    return -1; // Failure: Buffer empty
                return (tail + 1) % capacity;
            }
        }
    }
}
