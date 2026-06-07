using Unity.Mathematics;

namespace Hecton8.SaveSystem
{
    internal static class SaveDataInventorySanitizer
    {
        internal static InventoryShadowDTO BuildInventoryShadow(
            in InventoryDTO value,
            int shadowPayloadLength,
            uint shadowPayloadHash,
            bool hasShadowPayload)
        {
            InventoryDTO sanitized = default;
            sanitized.cellCount = math.clamp(value.cellCount, 0, ResolveExistingCellBound(in value));
            sanitized.gridColumns = math.clamp(value.gridColumns, 0, InventoryDTO.MaxCells);
            sanitized.gridRows = math.clamp(value.gridRows, 0, InventoryDTO.MaxCells);
            sanitized.totalWeight = math.isfinite(value.totalWeight) ? math.max(0f, value.totalWeight) : 0f;
            int safePayloadLength = ClampInventoryShadowPayloadLength(shadowPayloadLength);
            bool safeHasPayload = hasShadowPayload &&
                                  safePayloadLength > 0 &&
                                  safePayloadLength == shadowPayloadLength;
            return InventoryShadowDTO.FromInventory(
                in sanitized,
                safePayloadLength,
                shadowPayloadHash,
                safeHasPayload);
        }

        internal static int ResolveInventoryShadowPayloadLength(SaveData data)
        {
            if (data == null ||
                !data.hasInventoryShadowPayload ||
                data.inventoryShadowPayload == null ||
                data.inventoryShadowPayloadLength <= 0)
            {
                return 0;
            }

            if (data.inventoryShadowPayloadLength > data.inventoryShadowPayload.Length ||
                data.inventoryShadowPayloadLength > SaveData.InventoryShadowPayloadMaxBytes)
            {
                return 0;
            }

            uint payloadHash = ComputeInventoryShadowPayloadHash(data.inventoryShadowPayload, data.inventoryShadowPayloadLength);
            if (payloadHash != data.inventoryShadowPayloadHash)
                return 0;

            int expectedPayloadLength = ComputeInventoryShadowPayloadLength(in data.inventory);
            if (expectedPayloadLength != data.inventoryShadowPayloadLength)
                return 0;

            if (!InventoryShadowPayloadMatchesInventory(
                    data.inventoryShadowPayload,
                    data.inventoryShadowPayloadLength,
                    expectedPayloadLength,
                    in data.inventory))
            {
                return 0;
            }

            return data.inventoryShadowPayloadLength;
        }

        internal static bool ResolveInventoryShadowPayloadMetadata(
            SaveData data,
            out int payloadLength,
            out uint payloadHash)
        {
            payloadLength = ResolveInventoryShadowPayloadLength(data);
            if (payloadLength > 0)
            {
                payloadHash = data.inventoryShadowPayloadHash;
                return true;
            }

            if (data == null)
            {
                payloadHash = 0u;
                return false;
            }

            payloadLength = ResolveInventoryShadowPayloadLength(in data.inventoryShadow, in data.inventory);
            payloadHash = payloadLength > 0 ? data.inventoryShadow.payloadHash : 0u;
            return payloadLength > 0;
        }

        internal static int ResolveInventoryShadowPayloadLength(
            in InventoryShadowDTO shadow,
            in InventoryDTO inventory)
        {
            if (shadow.payloadLength <= 0 ||
                shadow.payloadLength > SaveData.InventoryShadowPayloadMaxBytes ||
                shadow.schemaVersion != InventoryShadowDTO.SchemaVersion ||
                shadow.reserved0 != 0 ||
                (shadow.flags & ~InventoryShadowDTO.FlagHasPayload) != 0 ||
                (shadow.flags & InventoryShadowDTO.FlagHasPayload) == 0)
            {
                return 0;
            }

            int expectedPayloadLength = ComputeInventoryShadowPayloadLength(in inventory);
            if (shadow.payloadLength != expectedPayloadLength)
                return 0;

            uint expectedPayloadHash = ComputeInventoryShadowPayloadHash(in inventory);
            if (shadow.payloadHash != expectedPayloadHash)
                return 0;

            return shadow.payloadLength;
        }

        internal static int ComputeInventoryShadowPayloadLength(in InventoryDTO value)
        {
            int count = math.clamp(value.cellCount, 0, ResolveExistingCellBound(in value));
            int durabilityRleLength = ResolveDurabilityRleLength(in value);
            return sizeof(int) +
                   EncodedStructArrayBytes<int>(count) +
                   EncodedStructArrayBytes<uint>(count) +
                   EncodedStructArrayBytes<ushort>(count) +
                   EncodedStructArrayBytes<ushort>(count) +
                   EncodedStructArrayBytes<byte>(count) +
                   EncodedStructArrayBytes<ushort>(count) +
                   EncodedStructArrayBytes<uint>(count) +
                   EncodedStructArrayBytes<byte>(durabilityRleLength) +
                   sizeof(float) +
                   sizeof(int) +
                   sizeof(int);
        }

        internal static uint ComputeInventoryShadowPayloadHash(in InventoryDTO value)
        {
            uint hash = SaveData.InventoryShadowPayloadHashSeed;
            int count = math.clamp(value.cellCount, 0, ResolveExistingCellBound(in value));
            HashInt(ref hash, count);
            HashIntArray(ref hash, value.itemHashIds, count);
            HashUIntArray(ref hash, value.packedCellCoordinates, count);
            HashUShortArray(ref hash, value.stackCounts, count, normalizeStacks: true);
            HashUShortArray(ref hash, value.itemStateFlags, count, normalizeStacks: false);
            HashByteArray(ref hash, value.itemGeneticsWords, count, maskGenetics: true);
            HashQualityArray(ref hash, value.qualityMilli, count);
            HashUIntArray(ref hash, value.lastUpdateUnixSeconds, count);

            int durabilityRleLength = ResolveDurabilityRleLength(in value);
            HashByteArray(ref hash, value.itemDurabilityRle, durabilityRleLength, maskGenetics: false);
            HashUInt(ref hash, math.asuint(math.isfinite(value.totalWeight) ? math.max(0f, value.totalWeight) : 0f));
            HashInt(ref hash, math.clamp(value.gridColumns, 0, InventoryDTO.MaxCells));
            HashInt(ref hash, math.clamp(value.gridRows, 0, InventoryDTO.MaxCells));
            return hash;
        }

        internal static uint ComputeInventoryShadowPayloadHash(byte[] payload, int payloadLength)
        {
            if (payload == null || payloadLength <= 0 || payloadLength > payload.Length)
                return 0u;

            uint hash = SaveData.InventoryShadowPayloadHashSeed;
            for (int i = 0; i < payloadLength; i++)
            {
                hash ^= payload[i];
                hash *= SaveData.InventoryShadowPayloadHashPrime;
            }

            return hash;
        }

        internal static bool SanitizeInventoryShadow(
            ref InventoryShadowDTO shadow,
            in InventoryDTO inventory,
            int shadowPayloadLength,
            uint shadowPayloadHash,
            bool hasShadowPayload)
        {
            InventoryShadowDTO safeShadow = BuildInventoryShadow(
                in inventory,
                shadowPayloadLength,
                shadowPayloadHash,
                hasShadowPayload);

            bool changed = shadow.cellCount != safeShadow.cellCount ||
                           shadow.payloadLength != safeShadow.payloadLength ||
                           shadow.payloadHash != safeShadow.payloadHash ||
                           shadow.gridColumns != safeShadow.gridColumns ||
                           shadow.gridRows != safeShadow.gridRows ||
                           !Approximately(shadow.totalWeight, safeShadow.totalWeight) ||
                           shadow.flags != safeShadow.flags ||
                           shadow.schemaVersion != safeShadow.schemaVersion ||
                           shadow.reserved0 != safeShadow.reserved0;

            if (changed)
                shadow = safeShadow;

            return changed;
        }

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

        private static int ResolveDurabilityRleLength(in InventoryDTO value)
        {
            int durabilityCapacity = value.itemDurabilityRle != null
                ? math.min(value.itemDurabilityRle.Length, InventoryDTO.MaxDurabilityRleBytes)
                : 0;

            return math.clamp(
                value.itemDurabilityRleLength,
                0,
                durabilityCapacity);
        }

        private static int EncodedStructArrayBytes<T>(int count) where T : unmanaged
        {
            return sizeof(int) + math.max(0, count) * Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<T>();
        }

        private static bool InventoryShadowPayloadMatchesInventory(
            byte[] payload,
            int payloadLength,
            int expectedPayloadLength,
            in InventoryDTO value)
        {
            if (payload == null ||
                payloadLength <= 0 ||
                payloadLength > payload.Length ||
                payloadLength != expectedPayloadLength)
            {
                return false;
            }

            int count = math.clamp(value.cellCount, 0, ResolveExistingCellBound(in value));
            int offset = 0;
            if (!MatchInt(payload, payloadLength, ref offset, count) ||
                !MatchIntArray(payload, payloadLength, ref offset, value.itemHashIds, count) ||
                !MatchUIntArray(payload, payloadLength, ref offset, value.packedCellCoordinates, count) ||
                !MatchUShortArray(payload, payloadLength, ref offset, value.stackCounts, count, normalizeStacks: true) ||
                !MatchUShortArray(payload, payloadLength, ref offset, value.itemStateFlags, count, normalizeStacks: false) ||
                !MatchByteArray(payload, payloadLength, ref offset, value.itemGeneticsWords, count, maskGenetics: true) ||
                !MatchQualityArray(payload, payloadLength, ref offset, value.qualityMilli, count) ||
                !MatchUIntArray(payload, payloadLength, ref offset, value.lastUpdateUnixSeconds, count))
            {
                return false;
            }

            int durabilityRleLength = ResolveDurabilityRleLength(in value);
            return MatchByteArray(payload, payloadLength, ref offset, value.itemDurabilityRle, durabilityRleLength, maskGenetics: false) &&
                   MatchUInt(payload, payloadLength, ref offset, math.asuint(math.isfinite(value.totalWeight) ? math.max(0f, value.totalWeight) : 0f)) &&
                   MatchInt(payload, payloadLength, ref offset, math.clamp(value.gridColumns, 0, InventoryDTO.MaxCells)) &&
                   MatchInt(payload, payloadLength, ref offset, math.clamp(value.gridRows, 0, InventoryDTO.MaxCells)) &&
                   offset == payloadLength;
        }

        private static void HashIntArray(ref uint hash, int[] values, int count)
        {
            HashInt(ref hash, count);
            for (int i = 0; i < count; i++)
                HashInt(ref hash, values[i]);
        }

        private static void HashUIntArray(ref uint hash, uint[] values, int count)
        {
            HashInt(ref hash, count);
            for (int i = 0; i < count; i++)
                HashUInt(ref hash, values[i]);
        }

        private static void HashUShortArray(ref uint hash, ushort[] values, int count, bool normalizeStacks)
        {
            HashInt(ref hash, count);
            for (int i = 0; i < count; i++)
            {
                ushort value = normalizeStacks && values[i] <= 0 ? (ushort)1 : values[i];
                HashUShort(ref hash, value);
            }
        }

        private static void HashQualityArray(ref uint hash, ushort[] values, int count)
        {
            HashInt(ref hash, count);
            for (int i = 0; i < count; i++)
                HashUShort(ref hash, NormalizeQualityMilli(values[i]));
        }

        private static void HashByteArray(ref uint hash, byte[] values, int count, bool maskGenetics)
        {
            HashInt(ref hash, count);
            for (int i = 0; i < count; i++)
            {
                byte value = maskGenetics
                    ? (byte)(values[i] & SaveData.InventoryItemGeneticsSupportedFlagsMask)
                    : values[i];
                HashByte(ref hash, value);
            }
        }

        private static void HashInt(ref uint hash, int value)
        {
            HashUInt(ref hash, unchecked((uint)value));
        }

        private static void HashUInt(ref uint hash, uint value)
        {
            HashByte(ref hash, (byte)value);
            HashByte(ref hash, (byte)(value >> 8));
            HashByte(ref hash, (byte)(value >> 16));
            HashByte(ref hash, (byte)(value >> 24));
        }

        private static void HashUShort(ref uint hash, ushort value)
        {
            HashByte(ref hash, (byte)value);
            HashByte(ref hash, (byte)(value >> 8));
        }

        private static void HashByte(ref uint hash, byte value)
        {
            hash ^= value;
            hash *= SaveData.InventoryShadowPayloadHashPrime;
        }

        private static bool MatchIntArray(
            byte[] payload,
            int payloadLength,
            ref int offset,
            int[] values,
            int count)
        {
            if (!MatchInt(payload, payloadLength, ref offset, count))
                return false;

            for (int i = 0; i < count; i++)
            {
                if (!MatchInt(payload, payloadLength, ref offset, values[i]))
                    return false;
            }

            return true;
        }

        private static bool MatchUIntArray(
            byte[] payload,
            int payloadLength,
            ref int offset,
            uint[] values,
            int count)
        {
            if (!MatchInt(payload, payloadLength, ref offset, count))
                return false;

            for (int i = 0; i < count; i++)
            {
                if (!MatchUInt(payload, payloadLength, ref offset, values[i]))
                    return false;
            }

            return true;
        }

        private static bool MatchUShortArray(
            byte[] payload,
            int payloadLength,
            ref int offset,
            ushort[] values,
            int count,
            bool normalizeStacks)
        {
            if (!MatchInt(payload, payloadLength, ref offset, count))
                return false;

            for (int i = 0; i < count; i++)
            {
                ushort value = normalizeStacks && values[i] <= 0 ? (ushort)1 : values[i];
                if (!MatchUShort(payload, payloadLength, ref offset, value))
                    return false;
            }

            return true;
        }

        private static bool MatchQualityArray(
            byte[] payload,
            int payloadLength,
            ref int offset,
            ushort[] values,
            int count)
        {
            if (!MatchInt(payload, payloadLength, ref offset, count))
                return false;

            for (int i = 0; i < count; i++)
            {
                if (!MatchUShort(payload, payloadLength, ref offset, NormalizeQualityMilli(values[i])))
                    return false;
            }

            return true;
        }

        private static bool MatchByteArray(
            byte[] payload,
            int payloadLength,
            ref int offset,
            byte[] values,
            int count,
            bool maskGenetics)
        {
            if (!MatchInt(payload, payloadLength, ref offset, count))
                return false;

            for (int i = 0; i < count; i++)
            {
                byte value = maskGenetics
                    ? (byte)(values[i] & SaveData.InventoryItemGeneticsSupportedFlagsMask)
                    : values[i];
                if (!MatchByte(payload, payloadLength, ref offset, value))
                    return false;
            }

            return true;
        }

        private static bool MatchInt(byte[] payload, int payloadLength, ref int offset, int value)
        {
            return MatchUInt(payload, payloadLength, ref offset, unchecked((uint)value));
        }

        private static bool MatchUInt(byte[] payload, int payloadLength, ref int offset, uint value)
        {
            return MatchByte(payload, payloadLength, ref offset, (byte)value) &&
                   MatchByte(payload, payloadLength, ref offset, (byte)(value >> 8)) &&
                   MatchByte(payload, payloadLength, ref offset, (byte)(value >> 16)) &&
                   MatchByte(payload, payloadLength, ref offset, (byte)(value >> 24));
        }

        private static bool MatchUShort(byte[] payload, int payloadLength, ref int offset, ushort value)
        {
            return MatchByte(payload, payloadLength, ref offset, (byte)value) &&
                   MatchByte(payload, payloadLength, ref offset, (byte)(value >> 8));
        }

        private static bool MatchByte(byte[] payload, int payloadLength, ref int offset, byte value)
        {
            if ((uint)offset >= (uint)payloadLength || payload[offset] != value)
                return false;

            offset++;
            return true;
        }

        private static int ClampInventoryShadowPayloadLength(int value)
        {
            return math.clamp(value, 0, SaveData.InventoryShadowPayloadMaxBytes);
        }

        private static bool Approximately(float a, float b)
        {
            return math.abs(a - b) <= 0.000001f;
        }
    }
}
