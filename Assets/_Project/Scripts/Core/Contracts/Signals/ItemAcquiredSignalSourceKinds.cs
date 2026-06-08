namespace Hecton8.Core.Contracts.Signals
{
    /// <summary>
    /// Canonical source-kind IDs for ItemAcquiredSignal producers.
    /// </summary>
    public static class ItemAcquiredSignalSourceKinds
    {
        public const byte Unknown = 0;
        public const byte ResourceNode = 1;
        public const byte ProceduralOreSpawner = 2;
        public const byte Fabricator = 4;
        public const byte DeconstructionRefund = 4;
        public const byte DeployableSdfDrill = 7;
        public const byte LootMagnet = 8;
        public const byte ManualPickup = 9;
        public const byte VoxelCarve = 12;
        public const byte ScavengingLootOracle = 13;
        public const byte HarvestableOutcrop = 14;
        public const byte DroneMining = 15;
    }
}
