using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory.Layout;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.SaveSystem
{
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal readonly struct SaveMasterHashV10Result
    {
        [FieldOffset(0)] public readonly ulong PlainLo;
        [FieldOffset(8)] public readonly ulong PlainHi;
        [FieldOffset(16)] public readonly ulong StoredLo;
        [FieldOffset(24)] public readonly ulong StoredHi;

        public SaveMasterHashV10Result(ulong plainLo, ulong plainHi, ulong storedLo, ulong storedHi)
        {
            PlainLo = plainLo;
            PlainHi = plainHi;
            StoredLo = storedLo;
            StoredHi = storedHi;
        }
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = SaveMasterHashV10.HeaderSizeBytes)]
    internal struct SaveFileHeaderV10
    {
        [FieldOffset(0)] public uint MagicValue;
        [FieldOffset(4)] public ushort Version;
        [FieldOffset(6)] public byte CompatMask;
        [FieldOffset(7)] public byte Flags;
        [FieldOffset(8)] public ulong TimestampUnixMs;
        [FieldOffset(16)] public uint Checksum;
        [FieldOffset(20)] public uint DeltaCount;
        [FieldOffset(24)] public uint EntityCount;
        [FieldOffset(28)] public uint PlayerOffset;
        [FieldOffset(32)] public uint DeltaOffset;
        [FieldOffset(36)] public uint EntityOffset;
        [FieldOffset(40)] public ulong HashPayload64;
        [FieldOffset(48)] public ulong HashHeader64;
        [FieldOffset(56)] public ulong MasterStateHashLo;
        [FieldOffset(64)] public ulong MasterStateHashHi;
    }

    internal static unsafe class SaveMasterHashV10
    {
        internal const ushort HeaderVersion = 0x000A;
        internal const int HeaderSizeBytes = 72;
        internal const int MasterStateHashLoOffset = 56;
        internal const int MasterStateHashHiOffset = 64;

        private const int MasterPreimageBytes = 96;
        private const int MasterLoHashBytes = MasterPreimageBytes + 3;
        private const int MasterHiHashBytes = MasterPreimageBytes + 11;
        private const int ShuffleMaskLoBytes = 36;
        private const int ShuffleMaskHiBytes = 44;

        internal static SaveMasterHashV10Result Compute(
            uint magicValue,
            ushort version,
            byte compatMask,
            byte flags,
            ulong timestampUnixMs,
            uint checksum,
            uint deltaCount,
            uint entityCount,
            uint playerOffset,
            uint deltaOffset,
            uint entityOffset,
            ulong hashPayload64,
            long worldSeed,
            long sectorHash)
        {
            byte* preimage = stackalloc byte[MasterHiHashBytes];
            BuildMasterPreimage(
                preimage,
                magicValue,
                version,
                compatMask,
                flags,
                timestampUnixMs,
                checksum,
                deltaCount,
                entityCount,
                playerOffset,
                deltaOffset,
                entityOffset,
                hashPayload64,
                worldSeed,
                sectorHash);

            WriteMasterLoSuffix(preimage + MasterPreimageBytes);
            ulong plainLo = Hash64(preimage, MasterLoHashBytes);
            WriteMasterHiSuffix(preimage + MasterPreimageBytes, plainLo);
            ulong plainHi = Hash64(preimage, MasterHiHashBytes);
            ShuffleHash128(plainLo, plainHi, worldSeed, sectorHash, out ulong storedLo, out ulong storedHi);

            return new SaveMasterHashV10Result(plainLo, plainHi, storedLo, storedHi);
        }

        internal static SaveMasterHashV10Result Compute(in SaveFileHeaderV10 header, long worldSeed, long sectorHash)
        {
            return Compute(
                header.MagicValue,
                header.Version,
                header.CompatMask,
                header.Flags,
                header.TimestampUnixMs,
                header.Checksum,
                header.DeltaCount,
                header.EntityCount,
                header.PlayerOffset,
                header.DeltaOffset,
                header.EntityOffset,
                header.HashPayload64,
                worldSeed,
                sectorHash);
        }

        internal static SaveFileHeaderV10 WithComputedMasterHash(
            in SaveFileHeaderV10 header,
            long worldSeed,
            long sectorHash)
        {
            SaveFileHeaderV10 copy = header;
            SaveMasterHashV10Result hash = Compute(in copy, worldSeed, sectorHash);
            copy.MasterStateHashLo = hash.StoredLo;
            copy.MasterStateHashHi = hash.StoredHi;
            return copy;
        }

        internal static bool MatchesStoredMasterHash(
            in SaveFileHeaderV10 header,
            long worldSeed,
            long sectorHash)
        {
            SaveMasterHashV10Result hash = Compute(in header, worldSeed, sectorHash);
            return hash.StoredLo == header.MasterStateHashLo &&
                   hash.StoredHi == header.MasterStateHashHi;
        }

        internal static void DeriveShuffleMask(long worldSeed, long sectorHash, out ulong maskLo, out ulong maskHi)
        {
            byte* buffer = stackalloc byte[ShuffleMaskHiBytes];
            int cursor = WriteShuffleDomainLo(buffer);
            WriteU64(buffer + cursor, unchecked((ulong)worldSeed));
            cursor += 8;
            WriteU64(buffer + cursor, unchecked((ulong)sectorHash));
            maskLo = Hash64(buffer, ShuffleMaskLoBytes);

            cursor = WriteShuffleDomainHi(buffer);
            WriteU64(buffer + cursor, unchecked((ulong)sectorHash));
            cursor += 8;
            WriteU64(buffer + cursor, unchecked((ulong)worldSeed));
            cursor += 8;
            WriteU64(buffer + cursor, maskLo);
            maskHi = Hash64(buffer, ShuffleMaskHiBytes);
        }

        internal static void ShuffleHash128(
            ulong plainLo,
            ulong plainHi,
            long worldSeed,
            long sectorHash,
            out ulong storedLo,
            out ulong storedHi)
        {
            DeriveShuffleMask(worldSeed, sectorHash, out ulong maskLo, out ulong maskHi);
            int rotation = (int)((maskLo ^ (maskHi >> 1)) & 127UL);
            Rotl128(plainLo ^ maskLo, plainHi ^ maskHi, rotation, out storedLo, out storedHi);
        }

        internal static void UnshuffleHash128(
            ulong storedLo,
            ulong storedHi,
            long worldSeed,
            long sectorHash,
            out ulong plainLo,
            out ulong plainHi)
        {
            DeriveShuffleMask(worldSeed, sectorHash, out ulong maskLo, out ulong maskHi);
            int rotation = (int)((maskLo ^ (maskHi >> 1)) & 127UL);
            Rotr128(storedLo, storedHi, rotation, out ulong unrotatedLo, out ulong unrotatedHi);
            plainLo = unrotatedLo ^ maskLo;
            plainHi = unrotatedHi ^ maskHi;
        }

        private static void BuildMasterPreimage(
            byte* target,
            uint magicValue,
            ushort version,
            byte compatMask,
            byte flags,
            ulong timestampUnixMs,
            uint checksum,
            uint deltaCount,
            uint entityCount,
            uint playerOffset,
            uint deltaOffset,
            uint entityOffset,
            ulong hashPayload64,
            long worldSeed,
            long sectorHash)
        {
            int cursor = WriteMasterDomain(target);
            WriteU32(target + cursor, magicValue);
            cursor += 4;
            WriteU16(target + cursor, version);
            cursor += 2;
            target[cursor++] = compatMask;
            target[cursor++] = flags;
            WriteU64(target + cursor, timestampUnixMs);
            cursor += 8;
            WriteU32(target + cursor, checksum);
            cursor += 4;
            WriteU32(target + cursor, deltaCount);
            cursor += 4;
            WriteU32(target + cursor, entityCount);
            cursor += 4;
            WriteU32(target + cursor, playerOffset);
            cursor += 4;
            WriteU32(target + cursor, deltaOffset);
            cursor += 4;
            WriteU32(target + cursor, entityOffset);
            cursor += 4;
            WriteU64(target + cursor, hashPayload64);
            cursor += 8;
            WriteU64(target + cursor, unchecked((ulong)worldSeed));
            cursor += 8;
            WriteU64(target + cursor, unchecked((ulong)sectorHash));
            cursor += 8;
            WriteU64(target + cursor, HectonContractVersion.HashLo);
            cursor += 8;
            WriteU64(target + cursor, HectonContractVersion.HashHi);
        }

        private static ulong Hash64(void* ptr, int length)
        {
            uint2 hash = xxHash3.Hash64(ptr, (long)length);
            return ((ulong)hash.y << 32) | hash.x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Rotl128(ulong lo, ulong hi, int shift, out ulong outLo, out ulong outHi)
        {
            shift &= 127;
            if (shift == 0)
            {
                outLo = lo;
                outHi = hi;
                return;
            }

            if (shift == 64)
            {
                outLo = hi;
                outHi = lo;
                return;
            }

            if (shift < 64)
            {
                int inverse = 64 - shift;
                outLo = (lo << shift) | (hi >> inverse);
                outHi = (hi << shift) | (lo >> inverse);
                return;
            }

            int laneShift = shift - 64;
            int laneInverse = 64 - laneShift;
            outLo = (hi << laneShift) | (lo >> laneInverse);
            outHi = (lo << laneShift) | (hi >> laneInverse);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Rotr128(ulong lo, ulong hi, int shift, out ulong outLo, out ulong outHi)
        {
            shift &= 127;
            if (shift == 0)
            {
                outLo = lo;
                outHi = hi;
                return;
            }

            if (shift == 64)
            {
                outLo = hi;
                outHi = lo;
                return;
            }

            if (shift < 64)
            {
                int inverse = 64 - shift;
                outLo = (lo >> shift) | (hi << inverse);
                outHi = (hi >> shift) | (lo << inverse);
                return;
            }

            int laneShift = shift - 64;
            int laneInverse = 64 - laneShift;
            outLo = (hi >> laneShift) | (lo << laneInverse);
            outHi = (lo >> laneShift) | (hi << laneInverse);
        }

        private static int WriteMasterDomain(byte* target)
        {
            target[0] = (byte)'H';
            target[1] = (byte)'8';
            target[2] = (byte)'S';
            target[3] = (byte)'A';
            target[4] = (byte)'V';
            target[5] = (byte)'E';
            target[6] = (byte)'_';
            target[7] = (byte)'M';
            target[8] = (byte)'A';
            target[9] = (byte)'S';
            target[10] = (byte)'T';
            target[11] = (byte)'E';
            target[12] = (byte)'R';
            target[13] = (byte)'_';
            target[14] = (byte)'V';
            target[15] = (byte)'1';
            return 16;
        }

        private static int WriteShuffleDomainLo(byte* target)
        {
            WriteShuffleDomainPrefix(target);
            target[15] = (byte)'L';
            target[16] = (byte)'O';
            target[17] = (byte)'_';
            target[18] = (byte)'V';
            target[19] = (byte)'1';
            return 20;
        }

        private static int WriteShuffleDomainHi(byte* target)
        {
            WriteShuffleDomainPrefix(target);
            target[15] = (byte)'H';
            target[16] = (byte)'I';
            target[17] = (byte)'_';
            target[18] = (byte)'V';
            target[19] = (byte)'1';
            return 20;
        }

        private static void WriteShuffleDomainPrefix(byte* target)
        {
            target[0] = (byte)'H';
            target[1] = (byte)'8';
            target[2] = (byte)'S';
            target[3] = (byte)'A';
            target[4] = (byte)'V';
            target[5] = (byte)'E';
            target[6] = (byte)'_';
            target[7] = (byte)'S';
            target[8] = (byte)'H';
            target[9] = (byte)'U';
            target[10] = (byte)'F';
            target[11] = (byte)'F';
            target[12] = (byte)'L';
            target[13] = (byte)'E';
            target[14] = (byte)'_';
        }

        private static void WriteMasterLoSuffix(byte* target)
        {
            target[0] = (byte)'_';
            target[1] = (byte)'L';
            target[2] = (byte)'O';
        }

        private static void WriteMasterHiSuffix(byte* target, ulong plainLo)
        {
            target[0] = (byte)'_';
            target[1] = (byte)'H';
            target[2] = (byte)'I';
            WriteU64(target + 3, plainLo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteU16(byte* target, ushort value)
        {
            target[0] = (byte)value;
            target[1] = (byte)(value >> 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteU32(byte* target, uint value)
        {
            target[0] = (byte)value;
            target[1] = (byte)(value >> 8);
            target[2] = (byte)(value >> 16);
            target[3] = (byte)(value >> 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteU64(byte* target, ulong value)
        {
            target[0] = (byte)value;
            target[1] = (byte)(value >> 8);
            target[2] = (byte)(value >> 16);
            target[3] = (byte)(value >> 24);
            target[4] = (byte)(value >> 32);
            target[5] = (byte)(value >> 40);
            target[6] = (byte)(value >> 48);
            target[7] = (byte)(value >> 56);
        }
    }
}
