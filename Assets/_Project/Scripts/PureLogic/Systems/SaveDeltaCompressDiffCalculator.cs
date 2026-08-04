using System;
using System.Collections.Generic;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for SaveDeltaCompressDiffCalculator.
    /// Extracted from SaveBinaryPayloadCodec.cs. Stateless; allocates patch
    /// buffers on the cold save path (not a per-frame route).
    /// </summary>
    public static class SaveDeltaCompressDiffCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="baseSnapshot">Parameter representing the baseSnapshot (byte[]).</param>
        /// <param name="newSnapshot">Parameter representing the newSnapshot (byte[]).</param>
        /// <returns>Returns Tuple return values of type (int offset, int length, byte[] patchData)[] patches.</returns>
        public static (int offset, int length, byte[] patchData)[] Compute(byte[] baseSnapshot, byte[] newSnapshot)
        {
            if (newSnapshot == null)
                return Array.Empty<(int, int, byte[])>();

            if (baseSnapshot == null)
            {
                if (newSnapshot.Length == 0)
                    return Array.Empty<(int, int, byte[])>();

                byte[] fullCopy = new byte[newSnapshot.Length];
                Array.Copy(newSnapshot, fullCopy, newSnapshot.Length);
                return new[] { (0, newSnapshot.Length, fullCopy) };
            }

            var patches = new List<(int offset, int length, byte[] patchData)>();

            int lengthToCompare = Math.Min(baseSnapshot.Length, newSnapshot.Length);
            int i = 0;

            while (i < lengthToCompare)
            {
                if (baseSnapshot[i] != newSnapshot[i])
                {
                    int startOffset = i;
                    while (i < lengthToCompare && baseSnapshot[i] != newSnapshot[i])
                    {
                        i++;
                    }

                    int diffLength = i - startOffset;
                    byte[] diffData = new byte[diffLength];
                    Array.Copy(newSnapshot, startOffset, diffData, 0, diffLength);
                    patches.Add((startOffset, diffLength, diffData));
                }
                else
                {
                    i++;
                }
            }

            if (newSnapshot.Length > baseSnapshot.Length)
            {
                int startOffset = baseSnapshot.Length;
                int diffLength = newSnapshot.Length - baseSnapshot.Length;
                byte[] diffData = new byte[diffLength];
                Array.Copy(newSnapshot, startOffset, diffData, 0, diffLength);
                patches.Add((startOffset, diffLength, diffData));
            }

            return patches.ToArray();
        }
    }
}
