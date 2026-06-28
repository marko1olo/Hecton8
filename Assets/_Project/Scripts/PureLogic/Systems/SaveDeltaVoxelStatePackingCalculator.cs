using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for SaveDeltaVoxelStatePackingCalculator.
    /// Extracted from SaveBinaryStorage.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class SaveDeltaVoxelStatePackingCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="originalBlocks">Parameter representing the originalBlocks (byte[]).</param>
        /// <param name="modifiedBlocks">Parameter representing the modifiedBlocks (byte[]).</param>
        /// <returns>Returns Delta package containing index offsets and new values of type byte[].</returns>
        public static byte[] Compute(byte[] originalBlocks, byte[] modifiedBlocks)
        {
            if (originalBlocks == null)
                throw new ArgumentNullException(nameof(originalBlocks));

            if (modifiedBlocks == null)
                throw new ArgumentNullException(nameof(modifiedBlocks));

            if (originalBlocks.Length != modifiedBlocks.Length)
                throw new ArgumentException("Arrays must be of the same length.");

            int diffCount = 0;
            int length = originalBlocks.Length;
            for (int i = 0; i < length; i++)
            {
                if (originalBlocks[i] != modifiedBlocks[i])
                {
                    diffCount++;
                }
            }

            if (diffCount == 0)
            {
                return Array.Empty<byte>();
            }

            const int bytesPerChange = 5; // 4 bytes for index + 1 byte for value
            byte[] deltaPackage = new byte[diffCount * bytesPerChange];

            int offset = 0;
            for (int i = 0; i < length; i++)
            {
                byte modVal = modifiedBlocks[i];
                if (originalBlocks[i] != modVal)
                {
                    deltaPackage[offset] = (byte)(i & 0xFF);
                    deltaPackage[offset + 1] = (byte)((i >> 8) & 0xFF);
                    deltaPackage[offset + 2] = (byte)((i >> 16) & 0xFF);
                    deltaPackage[offset + 3] = (byte)((i >> 24) & 0xFF);
                    deltaPackage[offset + 4] = modVal;

                    offset += bytesPerChange;
                }
            }

            return deltaPackage;
        }
    }
}
