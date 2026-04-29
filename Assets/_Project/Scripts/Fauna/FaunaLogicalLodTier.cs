namespace Hecton8.AI
{
    /// <summary>
    /// Runtime fauna simulation tier resolved from player proximity.
    /// </summary>
    internal enum FaunaLogicalLodTier : byte
    {
        FullSim = 0,
        DataOnly = 1,
        Hibernating = 2
    }
}
