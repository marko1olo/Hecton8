namespace Hecton8.Gameplay
{
    /// <summary>
    /// Declares which near-camera hand is primarily owned by a held tool.
    /// </summary>
    public enum PlayerToolSwimHandedness : byte
    {
        /// <summary>Tool is primarily right-hand owned.</summary>
        Right = 0,

        /// <summary>Tool is primarily left-hand owned.</summary>
        Left = 1
    }
}
