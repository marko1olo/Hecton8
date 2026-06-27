using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for HuffmanRleSaveDataCompressorCalculator.
    /// Extracted from SaveBinaryPayloadCodec.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class HuffmanRleSaveDataCompressorCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="uncompressedData">Parameter representing the uncompressedData (byte[]).</param>
        /// <returns>Returns Compressed output payload of type byte[].</returns>
        public static byte[] Compute(byte[] uncompressedData)
        {
            if (uncompressedData == null || uncompressedData.Length == 0)
            {
                return Array.Empty<byte>();
            }

            int inputLength = uncompressedData.Length;
            int requiredOutputBytes = 0;
            int readPass1 = 0;

            // First pass: calculate required output buffer size
            while (readPass1 < inputLength)
            {
                byte value = uncompressedData[readPass1];
                int run = 1;
                while (readPass1 + run < inputLength && run < ushort.MaxValue && uncompressedData[readPass1 + run] == value)
                {
                    run++;
                }

                requiredOutputBytes += 3;
                readPass1 += run;
            }

            byte[] compressedData = new byte[requiredOutputBytes];
            int readPass2 = 0;
            int writeIndex = 0;

            // Second pass: perform the actual run-length encoding
            while (readPass2 < inputLength)
            {
                byte value = uncompressedData[readPass2];
                int run = 1;
                while (readPass2 + run < inputLength && run < ushort.MaxValue && uncompressedData[readPass2 + run] == value)
                {
                    run++;
                }

                compressedData[writeIndex++] = value;
                ushort run16 = (ushort)run;
                compressedData[writeIndex++] = unchecked((byte)run16);
                compressedData[writeIndex++] = unchecked((byte)(run16 >> 8));

                readPass2 += run;
            }

            return compressedData;
        }
    }
}
