using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;

namespace Hecton8.Core.Contracts
{
    /// <summary>
    /// Contract-only ABI for the SHINOBU_116 macro ecosystem Vault snapshot.
    /// </summary>
    public static class MacroEcosystemVaultContract
    {
        public const int SectorCapacity = 10000;
        public const int IndexCapacity = 32768;
        public const float SectorSizeMeters = 1000f;
        public const float DefaultCarryingCapacityPrey = 60000f;
        public const float DefaultCarryingCapacityPredator = 12000f;
        public const uint TuningFlagSnapshotWriteInFlight = 1u << 31;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ComputeSectorHash(long sectorX, long sectorY, long sectorZ)
        {
            ulong hash = 14695981039346656037UL;
            hash = MixLong(hash, sectorX);
            hash = MixLong(hash, sectorY);
            hash = MixLong(hash, sectorZ);
            return hash == 0UL ? 1UL : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveOpenAddressSlot(ulong hash, int capacity)
        {
            if (capacity <= 0)
                return 0;

            uint mixed = (uint)(hash ^ (hash >> 32));
            mixed ^= mixed >> 16;
            mixed *= 0x7FEB352Du;
            mixed ^= mixed >> 15;
            mixed *= 0x846CA68Bu;
            mixed ^= mixed >> 16;
            return (capacity & (capacity - 1)) == 0
                ? (int)(mixed & (uint)(capacity - 1))
                : (int)(mixed % (uint)capacity);
        }

        public static bool TryResolveSectorIndex(
            NativeArray<MacroEcosystemSectorIndexRecord> entries,
            ulong sectorHash,
            out int sectorIndex)
        {
            sectorIndex = -1;
            if (!entries.IsCreated || entries.Length <= 0 || sectorHash == 0UL)
                return false;

            int slot = ResolveOpenAddressSlot(sectorHash, entries.Length);
            for (int probe = 0; probe < entries.Length; probe++)
            {
                MacroEcosystemSectorIndexRecord entry = entries[slot];
                if (entry.Occupied == 0u)
                    return false;

                if (entry.SectorHash == sectorHash)
                {
                    sectorIndex = entry.Slot;
                    return sectorIndex >= 0 && sectorIndex < SectorCapacity;
                }

                slot++;
                if (slot == entries.Length)
                    slot = 0;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong MixLong(ulong hash, long value)
        {
            ulong v = unchecked((ulong)value);
            for (int shift = 0; shift < 64; shift += 8)
                hash = (hash ^ ((v >> shift) & 0xFFUL)) * 1099511628211UL;
            return hash;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MacroEcosystemSectorVaultRecord
    {
        [FieldOffset(0)] public ulong SectorHash;
        [FieldOffset(8)] public uint PreyBiomass;
        [FieldOffset(12)] public uint PredatorBiomass;
        [FieldOffset(16)] public float LocalTemperature;
        [FieldOffset(20)] public float ToxinLevel;
        [FieldOffset(24)] public byte _pad0;
        [FieldOffset(25)] public byte _pad1;
        [FieldOffset(26)] public byte _pad2;
        [FieldOffset(27)] public byte _pad3;
        [FieldOffset(28)] public byte _pad4;
        [FieldOffset(29)] public byte _pad5;
        [FieldOffset(30)] public byte _pad6;
        [FieldOffset(31)] public byte _pad7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct MacroEcosystemSectorIndexRecord
    {
        [FieldOffset(0)] public ulong SectorHash;
        [FieldOffset(8)] public int Slot;
        [FieldOffset(12)] public uint Occupied;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MacroEcosystemTuningVaultRecord
    {
        [FieldOffset(0)] public float BaseBirthRate;
        [FieldOffset(4)] public float PredationRate;
        [FieldOffset(8)] public float PredatorConversionRate;
        [FieldOffset(12)] public float PredatorStarvationRate;
        [FieldOffset(16)] public float CarryingCapacityPrey;
        [FieldOffset(20)] public float CarryingCapacityPredator;
        [FieldOffset(24)] public float MigrationRate;
        [FieldOffset(28)] public float GlobalQualityWeight;
        [FieldOffset(32)] public float FrostDeltaSeconds;
        [FieldOffset(36)] public float TemperatureOptimum;
        [FieldOffset(40)] public float TemperatureHalfRange;
        [FieldOffset(44)] public float ToxicityBirthSuppression;
        [FieldOffset(48)] public float ToxicityDeathBoost;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint StateHash;
        [FieldOffset(60)] public uint Reserved;
    }
}
