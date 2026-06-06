using Unity.Mathematics;

namespace Hecton8.SaveSystem
{
    internal static class SaveDataInventorySanitizer
    {
        internal static bool SanitizeInventory(ref InventoryDTO value)
        {
            bool changed = false;
            int cellBound = ResolveExistingCellBound(in value);
            int durabilityBound = value.itemDurabilityRle != null
                ? math.min(value.itemDurabilityRle.Length, InventoryDTO.MaxDurabilityRleBytes)
                : 0;

            if (value.itemHashIds == null ||
                value.itemHashIds.Length != InventoryDTO.MaxCells ||
                value.packedCellCoordinates == null ||
                value.packedCellCoordinates.Length != InventoryDTO.MaxCells ||
                value.stackCounts == null ||
                value.stackCounts.Length != InventoryDTO.MaxCells ||
                value.itemStateFlags == null ||
                value.itemStateFlags.Length != InventoryDTO.MaxCells ||
                value.itemGeneticsWords == null ||
                value.itemGeneticsWords.Length != InventoryDTO.MaxCells ||
                value.qualityMilli == null ||
                value.qualityMilli.Length != InventoryDTO.MaxCells ||
                value.lastUpdateUnixSeconds == null ||
                value.lastUpdateUnixSeconds.Length != InventoryDTO.MaxCells ||
                value.itemDurabilityRle == null ||
                value.itemDurabilityRle.Length != InventoryDTO.MaxDurabilityRleBytes)
            {
                value.EnsureCapacity();
                changed = true;
            }

            int safeCellCount = math.clamp(value.cellCount, 0, cellBound);
            if (safeCellCount != value.cellCount)
            {
                value.cellCount = safeCellCount;
                changed = true;
            }

            int safeDurabilityRleLength = math.clamp(value.itemDurabilityRleLength, 0, durabilityBound);
            if (safeDurabilityRleLength != value.itemDurabilityRleLength)
            {
                value.itemDurabilityRleLength = safeDurabilityRleLength;
                changed = true;
            }

            float safeTotalWeight = math.isfinite(value.totalWeight) ? math.max(0f, value.totalWeight) : 0f;
            if (!Approximately(value.totalWeight, safeTotalWeight))
            {
                value.totalWeight = safeTotalWeight;
                changed = true;
            }

            int safeGridColumns = math.clamp(value.gridColumns, 0, InventoryDTO.MaxCells);
            if (safeGridColumns != value.gridColumns)
            {
                value.gridColumns = safeGridColumns;
                changed = true;
            }

            int safeGridRows = math.clamp(value.gridRows, 0, InventoryDTO.MaxCells);
            if (safeGridRows != value.gridRows)
            {
                value.gridRows = safeGridRows;
                changed = true;
            }

            for (int i = 0; i < value.cellCount; i++)
            {
                if (value.stackCounts[i] <= 0)
                {
                    value.stackCounts[i] = 1;
                    changed = true;
                }

                ushort safeQuality = NormalizeQualityMilli(value.qualityMilli[i]);
                if (safeQuality != value.qualityMilli[i])
                {
                    value.qualityMilli[i] = safeQuality;
                    changed = true;
                }

                byte safeGenetics = (byte)(value.itemGeneticsWords[i] & SaveData.InventoryItemGeneticsSupportedFlagsMask);
                if (safeGenetics != value.itemGeneticsWords[i])
                {
                    value.itemGeneticsWords[i] = safeGenetics;
                    changed = true;
                }
            }

            return changed;
        }

        private static int ResolveExistingCellBound(in InventoryDTO value)
        {
            int bound = InventoryDTO.MaxCells;
            bound = math.min(bound, value.itemHashIds != null ? value.itemHashIds.Length : 0);
            bound = math.min(bound, value.packedCellCoordinates != null ? value.packedCellCoordinates.Length : 0);
            bound = math.min(bound, value.stackCounts != null ? value.stackCounts.Length : 0);
            bound = math.min(bound, value.itemStateFlags != null ? value.itemStateFlags.Length : 0);
            bound = math.min(bound, value.itemGeneticsWords != null ? value.itemGeneticsWords.Length : 0);
            bound = math.min(bound, value.qualityMilli != null ? value.qualityMilli.Length : 0);
            bound = math.min(bound, value.lastUpdateUnixSeconds != null ? value.lastUpdateUnixSeconds.Length : 0);
            return math.max(0, bound);
        }

        private static ushort NormalizeQualityMilli(ushort qualityMilli)
        {
            if (qualityMilli == 0)
                return SaveData.InventoryDefaultQualityMilli;

            return (ushort)math.min((int)qualityMilli, SaveData.InventoryDefaultQualityMilli);
        }

        private static bool Approximately(float a, float b)
        {
            return math.abs(a - b) <= 0.000001f;
        }
    }
}
