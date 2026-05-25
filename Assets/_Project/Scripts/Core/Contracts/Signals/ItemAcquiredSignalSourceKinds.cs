namespace Hecton8.Core.Contracts.Signals
{
    /// <summary>
    /// Canonical source-kind IDs for ItemAcquiredSignal producers.
    /// </summary>
    public static class ItemAcquiredSignalSourceKinds
    {
        public const byte ScavengingLootOracle = 13;
        public const byte HarvestableOutcrop = 14;
        public const byte DroneMining = 15;
    }
}
