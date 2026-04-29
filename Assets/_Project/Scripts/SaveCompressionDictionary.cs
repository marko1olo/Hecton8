using System;

namespace Hecton8.SaveSystem
{
    internal static class SaveCompressionDictionary
    {
        private const int DictionaryLengthBytes = 64 * 1024;
        internal static readonly byte[] Bytes;

        static SaveCompressionDictionary()
        {
            Bytes = CreateDictionaryBytes(DictionaryLengthBytes);
        }

        private static byte[] CreateDictionaryBytes(int dictionaryLengthBytes)
        {
            byte[] dictionary = new byte[dictionaryLengthBytes];
            byte[] zeroPattern = new byte[64];
            byte[] qualityPattern = BuildRepeatedUShortPattern(1000u, 64);
            byte[] onePattern = BuildRepeatedUIntPattern(1u, 32);
            byte[] questWordPattern = BuildRepeatedUIntPattern(0u, 320);
            byte[] inventoryCoordinatePattern = BuildRepeatedUIntPattern(InventoryDTO.PackCellCoordinate(0, 0), 128);
            byte[] inventoryHashPattern = BuildRepeatedIntPattern(0, 128);
            byte[] inventoryStatePattern = BuildRepeatedUShortPattern(0u, 128);
            byte[] chunkWordPattern = BuildRepeatedLongPattern(0L, 64);
            byte[] commonUtf16Pattern = BuildUtf16Pattern(
                "02_HECTON_WORLD",
                "slot_0",
                "slot_1",
                "slot_2",
                "Unknown",
                "World_HeroProps",
                "Audio_Ambient",
                "Wrecks_Modules",
                string.Empty);

            byte[][] seeds =
            {
                zeroPattern,
                qualityPattern,
                onePattern,
                questWordPattern,
                inventoryCoordinatePattern,
                inventoryHashPattern,
                inventoryStatePattern,
                chunkWordPattern,
                commonUtf16Pattern
            };

            int cursor = 0;
            int seedIndex = 0;
            while (cursor < dictionary.Length)
            {
                byte[] seed = seeds[seedIndex];
                int bytesToCopy = Math.Min(seed.Length, dictionary.Length - cursor);
                Buffer.BlockCopy(seed, 0, dictionary, cursor, bytesToCopy);
                cursor += bytesToCopy;
                seedIndex++;
                if (seedIndex >= seeds.Length)
                    seedIndex = 0;
            }

            return dictionary;
        }

        private static byte[] BuildRepeatedUIntPattern(uint value, int repeatCount)
        {
            int stride = sizeof(uint);
            byte[] bytes = new byte[repeatCount * stride];
            for (int i = 0; i < repeatCount; i++)
            {
                int offset = i * stride;
                bytes[offset] = (byte)value;
                bytes[offset + 1] = (byte)(value >> 8);
                bytes[offset + 2] = (byte)(value >> 16);
                bytes[offset + 3] = (byte)(value >> 24);
            }

            return bytes;
        }

        private static byte[] BuildRepeatedIntPattern(int value, int repeatCount)
        {
            return BuildRepeatedUIntPattern(unchecked((uint)value), repeatCount);
        }

        private static byte[] BuildRepeatedUShortPattern(uint value, int repeatCount)
        {
            ushort ushortValue = unchecked((ushort)value);
            int stride = sizeof(ushort);
            byte[] bytes = new byte[repeatCount * stride];
            for (int i = 0; i < repeatCount; i++)
            {
                int offset = i * stride;
                bytes[offset] = (byte)ushortValue;
                bytes[offset + 1] = (byte)(ushortValue >> 8);
            }

            return bytes;
        }

        private static byte[] BuildRepeatedLongPattern(long value, int repeatCount)
        {
            ulong ulongValue = unchecked((ulong)value);
            int stride = sizeof(long);
            byte[] bytes = new byte[repeatCount * stride];
            for (int i = 0; i < repeatCount; i++)
            {
                int offset = i * stride;
                for (int byteIndex = 0; byteIndex < stride; byteIndex++)
                    bytes[offset + byteIndex] = (byte)(ulongValue >> (byteIndex * 8));
            }

            return bytes;
        }

        private static byte[] BuildUtf16Pattern(params string[] values)
        {
            int totalChars = 0;
            for (int i = 0; i < values.Length; i++)
            {
                totalChars += values[i] != null ? values[i].Length : 0;
                totalChars += 1;
            }

            byte[] bytes = new byte[totalChars * sizeof(char)];
            int cursor = 0;
            for (int i = 0; i < values.Length; i++)
            {
                string value = values[i] ?? string.Empty;
                for (int charIndex = 0; charIndex < value.Length; charIndex++)
                {
                    char character = value[charIndex];
                    bytes[cursor++] = (byte)character;
                    bytes[cursor++] = (byte)(character >> 8);
                }

                bytes[cursor++] = 0;
                bytes[cursor++] = 0;
            }

            return bytes;
        }
    }
}
