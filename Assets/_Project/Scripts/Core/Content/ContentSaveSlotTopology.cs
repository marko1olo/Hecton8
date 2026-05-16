namespace Hecton8.Core.Content
{
    /// <summary>
    /// Explicit save/data topology for content pipeline validation. Runtime save data remains delta-only.
    /// </summary>
    public static class ContentSaveSlotTopology
    {
        public const string SaveSlotDirectory = "Saves/slot_{0}";
        public const string PlayerDeltaFile = "slot_{0}.sav";
        public const string PlayerDeltaBackupFile = "slot_{0}.bak";
        public const string PlayerDeltaTempFile = "slot_{0}.tmp";
        public const string MacroDatabaseDirectory = "H8_MacroDB";
        public const string MacroDatabaseSectorFile = "sector_{0:X16}.h8page";
        public const string SeedDerivedMarker = "WORLD_SEED_DERIVED";

        public const byte SaveContainsPlayerDelta = 1;
        public const byte MacroDbContainsWorldState = 2;
        public const byte SeedDerivedContainsProceduralState = 3;

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
    }
}
