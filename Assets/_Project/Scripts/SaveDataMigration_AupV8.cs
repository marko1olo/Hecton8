using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory.Layout;
using Hecton8.World;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.SaveSystem
{
    internal static class SaveDataMigrationAupV8Layout
    {
        public const int AbsoluteUniversePositionV7StrideBytes = 36;
        public const int PayloadPrefixV7StrideBytes = 60;
        public const int PayloadPrefixV8StrideBytes = 72;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = SaveDataMigrationAupV8Layout.AbsoluteUniversePositionV7StrideBytes)]
    internal struct AbsoluteUniversePositionV7
    {
        [FieldOffset(0)]
        public long GridX;
        [FieldOffset(8)]
        public long GridY;
        [FieldOffset(16)]
        public long GridZ;
        [FieldOffset(24)]
        public float LocalX;
        [FieldOffset(28)]
        public float LocalY;
        [FieldOffset(32)]
        public float LocalZ;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = SaveDataMigrationAupV8Layout.PayloadPrefixV7StrideBytes)]
    internal struct PayloadPrefixV7
    {
        [FieldOffset(0)]
        public ulong TimestampUnixMs;
        [FieldOffset(8)]
        public float PlayTimeSeconds;
        [FieldOffset(12)]
        public AbsoluteUniversePositionV7 PlayerPosition;
        [FieldOffset(48)]
        public int SaveDataVersion;
        [FieldOffset(52)]
        public uint SaveDataByteLength;
        [FieldOffset(56)]
        public ushort SceneNameByteLength;
        [FieldOffset(58)]
        public ushort GameVersionByteLength;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = SaveDataMigrationAupV8Layout.PayloadPrefixV8StrideBytes)]
    internal struct PayloadPrefixV8
    {
        [FieldOffset(0)]
        public ulong TimestampUnixMs;
        [FieldOffset(8)]
        public float PlayTimeSeconds;
        [FieldOffset(12)]
        public AbsoluteUniversePosition PlayerPosition;
        [FieldOffset(60)]
        public int SaveDataVersion;
        [FieldOffset(64)]
        public uint SaveDataByteLength;
        [FieldOffset(68)]
        public ushort SceneNameByteLength;
        [FieldOffset(70)]
        public ushort GameVersionByteLength;
    }

    internal struct PayloadPrefixInfo
    {
        public ulong TimestampUnixMs;
        public float PlayTimeSeconds;
        public AbsoluteUniversePosition PlayerPosition;
        public int SaveDataVersion;
        public uint SaveDataByteLength;
        public ushort SceneNameByteLength;
        public ushort GameVersionByteLength;
        public int PrefixSizeBytes;
    }

    internal static unsafe class SaveDataMigration_AupV8
    {
        internal const ushort AupV8Version = 0x0008;
        internal const int LegacyAupSizeBytes = 36;
        internal const int CurrentAupSizeBytes = 48;
        internal const int LegacyPayloadPrefixSizeBytes = 60;
        internal const int CurrentPayloadPrefixSizeBytes = 72;
        internal const int PayloadPrefixByteShift = CurrentPayloadPrefixSizeBytes - LegacyPayloadPrefixSizeBytes;

        internal static bool TryReadPayloadPrefix(
            byte* rawPtr,
            int rawLength,
            ushort headerVersion,
            out PayloadPrefixInfo prefix,
            out string error)
        {
            prefix = default;
            error = string.Empty;

            if (rawPtr == null)
            {
                error = "Payload prefix read requested a null source buffer.";
                return false;
            }

            if (headerVersion >= AupV8Version)
                return TryReadPayloadPrefixV8(rawPtr, rawLength, out prefix, out error);

            return TryReadPayloadPrefixV7(rawPtr, rawLength, out prefix, out error);
        }

        private static bool TryReadPayloadPrefixV7(
            byte* rawPtr,
            int rawLength,
            out PayloadPrefixInfo prefix,
            out string error)
        {
            prefix = default;
            error = string.Empty;
            if (rawLength < LegacyPayloadPrefixSizeBytes)
            {
                error = "Legacy v7 payload prefix is truncated.";
                return false;
            }

            PayloadPrefixV7 legacyPrefix = default;
            if (!UnsafeMemoryCopyGuard.SafeCopy(
                    UnsafeUtility.AddressOf(ref legacyPrefix),
                    UnsafeUtility.SizeOf<PayloadPrefixV7>(),
                    rawPtr,
                    LegacyPayloadPrefixSizeBytes))
            {
                error = "Legacy v7 payload prefix copy failed bounds validation.";
                return false;
            }

            prefix = new PayloadPrefixInfo
            {
                TimestampUnixMs = legacyPrefix.TimestampUnixMs,
                PlayTimeSeconds = legacyPrefix.PlayTimeSeconds,
                PlayerPosition = new AbsoluteUniversePosition
                {
                    GridX = legacyPrefix.PlayerPosition.GridX,
                    GridY = legacyPrefix.PlayerPosition.GridY,
                    GridZ = legacyPrefix.PlayerPosition.GridZ,
                    LocalX = legacyPrefix.PlayerPosition.LocalX,
                    LocalY = legacyPrefix.PlayerPosition.LocalY,
                    LocalZ = legacyPrefix.PlayerPosition.LocalZ
                },
                SaveDataVersion = legacyPrefix.SaveDataVersion,
                SaveDataByteLength = legacyPrefix.SaveDataByteLength,
                SceneNameByteLength = legacyPrefix.SceneNameByteLength,
                GameVersionByteLength = legacyPrefix.GameVersionByteLength,
                PrefixSizeBytes = LegacyPayloadPrefixSizeBytes
            };
            return true;
        }

        private static bool TryReadPayloadPrefixV8(
            byte* rawPtr,
            int rawLength,
            out PayloadPrefixInfo prefix,
            out string error)
        {
            prefix = default;
            error = string.Empty;
            if (rawLength < CurrentPayloadPrefixSizeBytes)
            {
                error = "AUP v8 payload prefix is truncated.";
                return false;
            }

            PayloadPrefixV8 currentPrefix = default;
            if (!UnsafeMemoryCopyGuard.SafeCopy(
                    UnsafeUtility.AddressOf(ref currentPrefix),
                    UnsafeUtility.SizeOf<PayloadPrefixV8>(),
                    rawPtr,
                    CurrentPayloadPrefixSizeBytes))
            {
                error = "AUP v8 payload prefix copy failed bounds validation.";
                return false;
            }

            prefix = new PayloadPrefixInfo
            {
                TimestampUnixMs = currentPrefix.TimestampUnixMs,
                PlayTimeSeconds = currentPrefix.PlayTimeSeconds,
                PlayerPosition = currentPrefix.PlayerPosition,
                SaveDataVersion = currentPrefix.SaveDataVersion,
                SaveDataByteLength = currentPrefix.SaveDataByteLength,
                SceneNameByteLength = currentPrefix.SceneNameByteLength,
                GameVersionByteLength = currentPrefix.GameVersionByteLength,
                PrefixSizeBytes = CurrentPayloadPrefixSizeBytes
            };
            return true;
        }

        internal static bool TryMigratePayloadToV8(
            byte* rawPtr,
            int rawLength,
            int destinationCapacity,
            out PayloadPrefixInfo prefix,
            out int migratedLength,
            out int payloadByteShift,
            out string error)
        {
            prefix = default;
            migratedLength = rawLength;
            payloadByteShift = 0;
            error = string.Empty;

            if (rawPtr == null)
            {
                error = "AUP v8 migration requested a null payload buffer.";
                return false;
            }

            if (rawLength < LegacyPayloadPrefixSizeBytes)
            {
                error = "AUP v8 migration source payload is truncated.";
                return false;
            }

            if (destinationCapacity < rawLength + PayloadPrefixByteShift)
            {
                error = "AUP v8 migration destination capacity is smaller than the expanded payload prefix.";
                return false;
            }

            PayloadPrefixV7 legacyPrefix = default;
            if (!UnsafeMemoryCopyGuard.SafeCopy(
                    UnsafeUtility.AddressOf(ref legacyPrefix),
                    UnsafeUtility.SizeOf<PayloadPrefixV7>(),
                    rawPtr,
                    LegacyPayloadPrefixSizeBytes))
            {
                error = "AUP v8 migration prefix copy failed bounds validation.";
                return false;
            }

            int trailingByteCount = rawLength - LegacyPayloadPrefixSizeBytes;
            if (trailingByteCount > 0)
            {
                UnsafeUtility.MemMove(
                    rawPtr + CurrentPayloadPrefixSizeBytes,
                    rawPtr + LegacyPayloadPrefixSizeBytes,
                    trailingByteCount);
            }

            UnsafeUtility.MemClear(rawPtr, CurrentPayloadPrefixSizeBytes);

            PayloadPrefixV8 migratedPrefix = new PayloadPrefixV8
            {
                TimestampUnixMs = legacyPrefix.TimestampUnixMs,
                PlayTimeSeconds = legacyPrefix.PlayTimeSeconds,
                PlayerPosition = new AbsoluteUniversePosition
                {
                    GridX = legacyPrefix.PlayerPosition.GridX,
                    GridY = legacyPrefix.PlayerPosition.GridY,
                    GridZ = legacyPrefix.PlayerPosition.GridZ,
                    LocalX = legacyPrefix.PlayerPosition.LocalX,
                    LocalY = legacyPrefix.PlayerPosition.LocalY,
                    LocalZ = legacyPrefix.PlayerPosition.LocalZ
                },
                SaveDataVersion = legacyPrefix.SaveDataVersion,
                SaveDataByteLength = legacyPrefix.SaveDataByteLength,
                SceneNameByteLength = legacyPrefix.SceneNameByteLength,
                GameVersionByteLength = legacyPrefix.GameVersionByteLength
            };

            UnsafeUtility.CopyStructureToPtr(ref migratedPrefix, rawPtr);

            migratedLength = rawLength + PayloadPrefixByteShift;
            payloadByteShift = PayloadPrefixByteShift;
            prefix = new PayloadPrefixInfo
            {
                TimestampUnixMs = migratedPrefix.TimestampUnixMs,
                PlayTimeSeconds = migratedPrefix.PlayTimeSeconds,
                PlayerPosition = migratedPrefix.PlayerPosition,
                SaveDataVersion = migratedPrefix.SaveDataVersion,
                SaveDataByteLength = migratedPrefix.SaveDataByteLength,
                SceneNameByteLength = migratedPrefix.SceneNameByteLength,
                GameVersionByteLength = migratedPrefix.GameVersionByteLength,
                PrefixSizeBytes = CurrentPayloadPrefixSizeBytes
            };

            return true;
        }
    }
}
