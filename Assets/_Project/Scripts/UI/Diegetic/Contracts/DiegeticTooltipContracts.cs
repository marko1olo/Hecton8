namespace Hecton8.UI.Diegetic.Contracts
{
    /// <summary>
    /// Stable input-scheme hashes mirrored from the deterministic input dispatcher.
    /// </summary>
    public static class DiegeticTooltipInputSchemeHashes
    {
        public const uint KeyboardMouse = 0x4B424D21u;
        public const uint Gamepad = 0x47504144u;
        public const uint SteamDeck = 0x5354444Bu;
        public const uint XRTouch = 0x58525443u;
    }

    /// <summary>
    /// TMP sprite table indices used by the diegetic interact prompt resolver.
    /// </summary>
    public static class DiegeticTooltipGlyphIndices
    {
        public const int KeyboardInteract = 1;
        public const int GamepadInteract = 12;
        public const int SteamDeckInteract = 14;
        public const int XRInteract = 18;
    }
}
