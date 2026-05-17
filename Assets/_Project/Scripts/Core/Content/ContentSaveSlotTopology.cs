using System;

namespace Hecton8.Core.Content
{
    /// <summary>
    /// Explicit save/data topology for content pipeline validation. Runtime save data remains delta-only.
    /// </summary>
    public static class ContentSaveSlotTopology
    {
        public const int MinSlotIndex = 0;
        public const int MaxSlotIndex = 2;
        public const int SaveSlotDirectoryChars = 12;
        public const int PlayerDeltaFileChars = 10;
        public const int PlayerDeltaBackupFileChars = 10;
        public const int PlayerDeltaTempFileChars = 10;
        public const int MacroDatabaseSectorFileChars = 30;
        public const int MaxSavePathChars = MacroDatabaseSectorFileChars;
        public const string MacroDatabaseDirectory = "H8_MacroDB";
        public const string SeedDerivedMarker = "WORLD_SEED_DERIVED";
        public const string SaveSlotDirectoryPrefix = "Saves/slot_";
        public const string SlotFilePrefix = "slot_";
        public const string PlayerDeltaExtension = ".sav";
        public const string PlayerDeltaBackupExtension = ".bak";
        public const string PlayerDeltaTempExtension = ".tmp";
        public const string MacroDatabaseSectorFilePrefix = "sector_";
        public const string MacroDatabaseSectorFileSuffix = ".h8page";

        public const byte SaveContainsPlayerDelta = 1;
        public const byte MacroDbContainsWorldState = 2;
        public const byte SeedDerivedContainsProceduralState = 3;

        private const string HexDigits = "0123456789ABCDEF";

        /// <summary>
        /// Returns true when the slot maps to the explicit HECTON-8 slot_0..slot_2 save contract.
        /// </summary>
        public static bool IsValidSlotIndex(int slotIndex)
        {
            return (uint)(slotIndex - MinSlotIndex) <= (uint)(MaxSlotIndex - MinSlotIndex);
        }

        /// <summary>
        /// Writes `Saves/slot_N` into a caller-owned span without using string formatting.
        /// </summary>
        public static bool TryWriteSaveSlotDirectory(int slotIndex, Span<char> destination, out int charsWritten)
        {
            return TryWriteSlotPath(SaveSlotDirectoryPrefix, slotIndex, string.Empty, destination, out charsWritten);
        }

        /// <summary>
        /// Writes `slot_N.sav` into a caller-owned span without using string formatting.
        /// </summary>
        public static bool TryWritePlayerDeltaFile(int slotIndex, Span<char> destination, out int charsWritten)
        {
            return TryWriteSlotPath(SlotFilePrefix, slotIndex, PlayerDeltaExtension, destination, out charsWritten);
        }

        /// <summary>
        /// Writes `slot_N.bak` into a caller-owned span without using string formatting.
        /// </summary>
        public static bool TryWritePlayerDeltaBackupFile(int slotIndex, Span<char> destination, out int charsWritten)
        {
            return TryWriteSlotPath(SlotFilePrefix, slotIndex, PlayerDeltaBackupExtension, destination, out charsWritten);
        }

        /// <summary>
        /// Writes `slot_N.tmp` into a caller-owned span without using string formatting.
        /// </summary>
        public static bool TryWritePlayerDeltaTempFile(int slotIndex, Span<char> destination, out int charsWritten)
        {
            return TryWriteSlotPath(SlotFilePrefix, slotIndex, PlayerDeltaTempExtension, destination, out charsWritten);
        }

        /// <summary>
        /// Writes `sector_XXXXXXXXXXXXXXXX.h8page` into a caller-owned span without heap formatting.
        /// </summary>
        public static bool TryWriteMacroDatabaseSectorFile(ulong sectorKey, Span<char> destination, out int charsWritten)
        {
            charsWritten = 0;
            int cursor = 0;
            if (!WriteLiteral(MacroDatabaseSectorFilePrefix, destination, ref cursor))
                return false;

            for (int shift = 60; shift >= 0; shift -= 4)
            {
                if (cursor >= destination.Length)
                    return false;

                int nibble = (int)((sectorKey >> shift) & 0xFUL);
                destination[cursor] = HexDigits[nibble];
                cursor++;
            }

            if (!WriteLiteral(MacroDatabaseSectorFileSuffix, destination, ref cursor))
                return false;

            charsWritten = cursor;
            return true;
        }

        public static bool IsPlayerDeltaPayload(byte topologyKind)
        {
            return topologyKind == SaveContainsPlayerDelta;
        }

        public static bool IsMacroDatabasePayload(byte topologyKind)
        {
            return topologyKind == MacroDbContainsWorldState;
        }

        public static bool IsSeedDerivedPayload(byte topologyKind)
        {
            return topologyKind == SeedDerivedContainsProceduralState;
        }

        private static bool TryWriteSlotPath(
            string prefix,
            int slotIndex,
            string suffix,
            Span<char> destination,
            out int charsWritten)
        {
            charsWritten = 0;
            if (!IsValidSlotIndex(slotIndex))
                return false;

            int cursor = 0;
            if (!WriteLiteral(prefix, destination, ref cursor))
                return false;
            if (!WriteSlotDigit(slotIndex, destination, ref cursor))
                return false;
            if (!WriteLiteral(suffix, destination, ref cursor))
                return false;

            charsWritten = cursor;
            return true;
        }

        private static bool WriteSlotDigit(int slotIndex, Span<char> destination, ref int cursor)
        {
            if (cursor >= destination.Length)
                return false;

            destination[cursor] = (char)('0' + slotIndex);
            cursor++;
            return true;
        }

        private static bool WriteLiteral(string literal, Span<char> destination, ref int cursor)
        {
            int length = literal != null ? literal.Length : 0;
            if (cursor < 0 || destination.Length - cursor < length)
                return false;

            for (int i = 0; i < length; i++)
                destination[cursor + i] = literal[i];

            cursor += length;
            return true;
        }
    }
}
