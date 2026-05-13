namespace Hecton8.Inventory.Corrosion.Contracts
{
    public static class InventoryCorrosionConstants
    {
        public const int InventoryMaskBitCount = 64;

        public const int ResultChangedCount = 0;
        public const int ResultBrokenCount = 1;
        public const int ResultAverageDurabilityMilli = 2;
        public const int ResultEquippedCount = 3;
        public const int ResultScannedCount = 4;
        public const int ResultRequiredLength = 5;
    }

    public static class ItemCorrosionMath
    {
        public static int ResolveInventoryMaskBitIndex(uint itemHash)
        {
            return (int)(itemHash & (InventoryCorrosionConstants.InventoryMaskBitCount - 1));
        }

        public static ulong ResolveInventoryMaterialBit(uint itemHash)
        {
            return itemHash == 0u ? 0UL : 1UL << ResolveInventoryMaskBitIndex(itemHash);
        }
    }
}
