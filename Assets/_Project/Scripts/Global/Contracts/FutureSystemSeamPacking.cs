using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Hecton8.Global.Contracts
{
    /// <summary>
    /// Fixed binary header for future-seam reservation blobs. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FutureSystemSeamBinaryHeader64
    {
        [FieldOffset(0)] public ulong Magic;
        [FieldOffset(8)] public ulong ContentHash;
        [FieldOffset(16)] public uint SchemaVersion;
        [FieldOffset(20)] public uint HeaderSizeBytes;
        [FieldOffset(24)] public uint RecordSizeBytes;
        [FieldOffset(28)] public uint RecordCount;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public uint Reserved0;
        [FieldOffset(40)] public ulong Reserved1;
        [FieldOffset(48)] public ulong Reserved2;
        [FieldOffset(56)] public ulong Reserved3;
    }

    /// <summary>
    /// Allocation-free parser and binary emitter for dormant future-seam reservations.
    /// </summary>
    public static class FutureSystemSeamPacking
    {
        public const ulong BinaryMagic = 0x314D414553463848UL; // H8FSEAM1
        public const uint BinaryFormatVersion = 1u;
        public const int HeaderSizeBytes = 64;
        public const int MaxAuthoringReservationRows = 64;

        private const ulong HashSeed = 14695981039346656037UL;
        private const ulong HashPrime = 1099511628211UL;

        /// <summary>Returns the byte count required for one binary blob.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ComputeBinarySize(int recordCount)
        {
            return recordCount <= 0
                ? HeaderSizeBytes
                : HeaderSizeBytes + (recordCount * FutureSystemSeamContracts.RecordSizeBytes);
        }

        /// <summary>Builds a deterministic 64-byte header for a caller-owned record span.</summary>
        public static FutureSystemSeamBinaryHeader64 BuildHeader(
            ReadOnlySpan<FutureSystemSeamRecord64> records,
            uint flags)
        {
            return new FutureSystemSeamBinaryHeader64
            {
                Magic = BinaryMagic,
                ContentHash = HashRecords(records),
                SchemaVersion = BinaryFormatVersion,
                HeaderSizeBytes = HeaderSizeBytes,
                RecordSizeBytes = FutureSystemSeamContracts.RecordSizeBytes,
                RecordCount = unchecked((uint)records.Length),
                Flags = flags
            };
        }

        /// <summary>
        /// Writes a little-endian binary reservation blob into a caller-provided byte span.
        /// </summary>
        public static bool TryWriteBinary(
            ReadOnlySpan<FutureSystemSeamRecord64> records,
            Span<byte> destination,
            out int bytesWritten,
            out FutureSeamValidationError errors)
        {
            bytesWritten = 0;
            errors = ValidateRecords(records);

            int requiredBytes = ComputeBinarySize(records.Length);
            if (requiredBytes > destination.Length)
            {
                errors |= FutureSeamValidationError.BinaryBufferTooSmall;
                return false;
            }

            if (errors != FutureSeamValidationError.None)
            {
                errors |= FutureSeamValidationError.RecordValidationFailed;
                return false;
            }

            FutureSystemSeamBinaryHeader64 header = BuildHeader(records, 0u);
            WriteHeader(destination.Slice(0, HeaderSizeBytes), in header);

            int offset = HeaderSizeBytes;
            for (int i = 0; i < records.Length; i++)
            {
                WriteRecord(destination.Slice(offset, FutureSystemSeamContracts.RecordSizeBytes), in records[i]);
                offset += FutureSystemSeamContracts.RecordSizeBytes;
            }

            bytesWritten = requiredBytes;
            return true;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Parses CSV rows as surface,payloadBytes,blackboxFrames,flagsMask,proofMask.
        /// </summary>
        public static int ParseCsvReservations(
            ReadOnlySpan<char> csv,
            Span<FutureSystemSeamRecord64> records,
            out FutureSeamValidationError errors)
        {
            errors = FutureSeamValidationError.None;
            int count = 0;
            int cursor = 0;

            while (TryReadLine(csv, ref cursor, out ReadOnlySpan<char> line))
            {
                line = Trim(line);
                if (line.Length == 0 || line[0] == '#')
                    continue;

                if (IsHeaderRow(line))
                    continue;

                if (count >= records.Length)
                {
                    errors |= FutureSeamValidationError.BinaryBufferTooSmall;
                    break;
                }

                if (!TryParseReservationRow(line, out FutureSystemSeamRecord64 record, out FutureSeamValidationError rowErrors))
                {
                    errors |= rowErrors == FutureSeamValidationError.None
                        ? FutureSeamValidationError.CsvParseError
                        : rowErrors | FutureSeamValidationError.CsvParseError;
                    continue;
                }

                rowErrors = FutureSystemSeamContracts.ValidateReservation(in record);
                if (rowErrors != FutureSeamValidationError.None)
                {
                    errors |= rowErrors | FutureSeamValidationError.RecordValidationFailed;
                    continue;
                }

                records[count++] = record;
            }

            return count;
        }

        /// <summary>Validates a record span without allocating or touching owner runtime systems.</summary>
        public static FutureSeamValidationError ValidateRecords(ReadOnlySpan<FutureSystemSeamRecord64> records)
        {
            FutureSeamValidationError errors = FutureSeamValidationError.None;
            for (int i = 0; i < records.Length; i++)
                errors |= FutureSystemSeamContracts.ValidateReservation(in records[i]);
            return errors;
        }

        private static bool TryParseReservationRow(
            ReadOnlySpan<char> line,
            out FutureSystemSeamRecord64 record,
            out FutureSeamValidationError errors)
        {
            record = default;
            errors = FutureSeamValidationError.None;
            int cursor = 0;

            if (!TryReadColumn(line, ref cursor, out ReadOnlySpan<char> surfaceToken) ||
                !TryResolveSurface(surfaceToken, out FutureRuntimeSurface surface))
            {
                errors = FutureSeamValidationError.MissingSurface;
                return false;
            }

            if (!FutureSystemSeamContracts.TryBuildReservation(surface, out record))
            {
                errors = FutureSeamValidationError.MissingOwnerSlot;
                return false;
            }

            if (TryReadColumn(line, ref cursor, out ReadOnlySpan<char> payloadToken) &&
                payloadToken.Length > 0)
            {
                if (!TryParseUInt(payloadToken, out uint payloadBytes))
                {
                    errors |= FutureSeamValidationError.CsvParseError;
                    return false;
                }

                record.PayloadSizeBytes = payloadBytes;
            }

            if (TryReadColumn(line, ref cursor, out ReadOnlySpan<char> blackboxToken) &&
                blackboxToken.Length > 0)
            {
                if (!TryParseUInt(blackboxToken, out uint blackboxFrames))
                {
                    errors |= FutureSeamValidationError.CsvParseError;
                    return false;
                }

                record.BlackboxCapacity = blackboxFrames;
            }

            if (TryReadColumn(line, ref cursor, out ReadOnlySpan<char> flagsToken) &&
                flagsToken.Length > 0)
            {
                if (!TryParseUInt(flagsToken, out uint flagsMask))
                {
                    errors |= FutureSeamValidationError.CsvParseError;
                    return false;
                }

                record.Flags = unchecked((ushort)(flagsMask & 0xFFFFu));
            }

            if (TryReadColumn(line, ref cursor, out ReadOnlySpan<char> proofToken) &&
                proofToken.Length > 0)
            {
                if (!TryParseUInt(proofToken, out uint proofMask))
                {
                    errors |= FutureSeamValidationError.CsvParseError;
                    return false;
                }

                record.ProofMask = proofMask;
            }

            return true;
        }

        private static bool IsHeaderRow(ReadOnlySpan<char> line)
        {
            int cursor = 0;
            return TryReadColumn(line, ref cursor, out ReadOnlySpan<char> token) &&
                   EqualsAsciiIgnoreCase(token, "surface".AsSpan());
        }

        private static bool TryResolveSurface(ReadOnlySpan<char> token, out FutureRuntimeSurface surface)
        {
            token = Trim(token);
            if (TryParseUInt(token, out uint numericSurface))
            {
                surface = (FutureRuntimeSurface)numericSurface;
                return FutureSystemSeamContracts.GetSurfaceHash(surface) != 0u;
            }

            if (EqualsAsciiIgnoreCase(token, "SurvivalOverride".AsSpan()))
            {
                surface = FutureRuntimeSurface.SurvivalOverride;
                return true;
            }

            if (EqualsAsciiIgnoreCase(token, "HapticPulse".AsSpan()))
            {
                surface = FutureRuntimeSurface.HapticPulse;
                return true;
            }

            if (EqualsAsciiIgnoreCase(token, "SubtitleCue".AsSpan()))
            {
                surface = FutureRuntimeSurface.SubtitleCue;
                return true;
            }

            if (EqualsAsciiIgnoreCase(token, "TelemetryMarker".AsSpan()))
            {
                surface = FutureRuntimeSurface.TelemetryMarker;
                return true;
            }

            if (EqualsAsciiIgnoreCase(token, "QaScenarioMarker".AsSpan()))
            {
                surface = FutureRuntimeSurface.QaScenarioMarker;
                return true;
            }

            if (EqualsAsciiIgnoreCase(token, "ChunkInterestHint".AsSpan()))
            {
                surface = FutureRuntimeSurface.ChunkInterestHint;
                return true;
            }

            if (EqualsAsciiIgnoreCase(token, "SaveHashProbe".AsSpan()))
            {
                surface = FutureRuntimeSurface.SaveHashProbe;
                return true;
            }

            surface = FutureRuntimeSurface.None;
            return false;
        }

        private static bool TryReadLine(ReadOnlySpan<char> text, ref int cursor, out ReadOnlySpan<char> line)
        {
            line = default;
            if (cursor >= text.Length)
                return false;

            int start = cursor;
            while (cursor < text.Length && text[cursor] != '\n' && text[cursor] != '\r')
                cursor++;

            int end = cursor;
            while (cursor < text.Length && (text[cursor] == '\n' || text[cursor] == '\r'))
                cursor++;

            line = text.Slice(start, end - start);
            return true;
        }

        private static bool TryReadColumn(
            ReadOnlySpan<char> line,
            ref int cursor,
            out ReadOnlySpan<char> column)
        {
            column = default;
            if (cursor > line.Length)
                return false;

            int start = cursor;
            while (cursor < line.Length && line[cursor] != ',' && line[cursor] != ';')
                cursor++;

            int end = cursor;
            if (cursor < line.Length)
                cursor++;
            else
                cursor = line.Length + 1;

            column = Trim(line.Slice(start, end - start));
            return true;
        }

        private static ReadOnlySpan<char> Trim(ReadOnlySpan<char> text)
        {
            int start = 0;
            int end = text.Length - 1;
            while (start <= end && char.IsWhiteSpace(text[start]))
                start++;
            while (end >= start && char.IsWhiteSpace(text[end]))
                end--;
            return start <= end ? text.Slice(start, end - start + 1) : ReadOnlySpan<char>.Empty;
        }

        private static bool TryParseUInt(ReadOnlySpan<char> text, out uint value)
        {
            value = 0u;
            text = Trim(text);
            if (text.Length == 0)
                return false;

            int index = 0;
            if (text.Length > 2 && text[0] == '0' && (text[1] == 'x' || text[1] == 'X'))
            {
                index = 2;
                for (; index < text.Length; index++)
                {
                    int digit = HexDigit(text[index]);
                    if (digit < 0)
                        return false;
                    value = (value << 4) | unchecked((uint)digit);
                }

                return true;
            }

            for (; index < text.Length; index++)
            {
                char c = text[index];
                if (c < '0' || c > '9')
                    return false;
                value = (value * 10u) + unchecked((uint)(c - '0'));
            }

            return true;
        }

        private static int HexDigit(char c)
        {
            if (c >= '0' && c <= '9')
                return c - '0';
            if (c >= 'a' && c <= 'f')
                return c - 'a' + 10;
            if (c >= 'A' && c <= 'F')
                return c - 'A' + 10;
            return -1;
        }

        private static bool EqualsAsciiIgnoreCase(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
        {
            if (left.Length != right.Length)
                return false;

            for (int i = 0; i < left.Length; i++)
            {
                char a = left[i];
                char b = right[i];
                if (a >= 'A' && a <= 'Z')
                    a = (char)(a + 32);
                if (b >= 'A' && b <= 'Z')
                    b = (char)(b + 32);
                if (a != b)
                    return false;
            }

            return true;
        }
#endif

        private static ulong HashRecords(ReadOnlySpan<FutureSystemSeamRecord64> records)
        {
            ulong hash = HashSeed;
            hash = Mix(hash, unchecked((ulong)records.Length));
            for (int i = 0; i < records.Length; i++)
            {
                FutureSystemSeamRecord64 record = records[i];
                hash = Mix(hash, record.ContractHash);
                hash = Mix(hash, record.EvidenceHash);
                hash = Mix(hash, record.RuntimeSurfaceHash);
                hash = Mix(hash, record.ProofMask);
                hash = Mix(hash, record.PayloadSizeBytes);
                hash = Mix(hash, record.BlackboxCapacity);
                hash = Mix(hash, record.Slot);
                hash = Mix(hash, record.Surface);
                hash = Mix(hash, record.Flags);
                hash = Mix(hash, record.OwnerState);
                hash = Mix(hash, record.SchemaVersion);
            }

            return hash;
        }

        private static void WriteHeader(Span<byte> destination, in FutureSystemSeamBinaryHeader64 header)
        {
            WriteUInt64(destination, 0, header.Magic);
            WriteUInt64(destination, 8, header.ContentHash);
            WriteUInt32(destination, 16, header.SchemaVersion);
            WriteUInt32(destination, 20, header.HeaderSizeBytes);
            WriteUInt32(destination, 24, header.RecordSizeBytes);
            WriteUInt32(destination, 28, header.RecordCount);
            WriteUInt32(destination, 32, header.Flags);
            WriteUInt32(destination, 36, header.Reserved0);
            WriteUInt64(destination, 40, header.Reserved1);
            WriteUInt64(destination, 48, header.Reserved2);
            WriteUInt64(destination, 56, header.Reserved3);
        }

        private static void WriteRecord(Span<byte> destination, in FutureSystemSeamRecord64 record)
        {
            WriteUInt64(destination, 0, record.ContractHash);
            WriteUInt64(destination, 8, record.EvidenceHash);
            WriteUInt32(destination, 16, record.OwnerHash);
            WriteUInt32(destination, 20, record.RuntimeSurfaceHash);
            WriteUInt32(destination, 24, record.ProofMask);
            WriteUInt32(destination, 28, record.PayloadSizeBytes);
            WriteUInt16(destination, 32, record.Slot);
            WriteUInt16(destination, 34, record.Surface);
            WriteUInt16(destination, 36, record.Flags);
            destination[38] = record.OwnerState;
            destination[39] = record.SchemaVersion;
            WriteUInt32(destination, 40, record.BlackboxCapacity);
            WriteUInt32(destination, 44, record.Reserved0);
            WriteUInt64(destination, 48, record.Reserved1);
            WriteUInt64(destination, 56, record.Reserved2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUInt16(Span<byte> destination, int offset, ushort value)
        {
            destination[offset] = unchecked((byte)value);
            destination[offset + 1] = unchecked((byte)(value >> 8));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUInt32(Span<byte> destination, int offset, uint value)
        {
            destination[offset] = unchecked((byte)value);
            destination[offset + 1] = unchecked((byte)(value >> 8));
            destination[offset + 2] = unchecked((byte)(value >> 16));
            destination[offset + 3] = unchecked((byte)(value >> 24));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUInt64(Span<byte> destination, int offset, ulong value)
        {
            WriteUInt32(destination, offset, unchecked((uint)value));
            WriteUInt32(destination, offset + 4, unchecked((uint)(value >> 32)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Mix(ulong hash, ulong word)
        {
            hash ^= word;
            return hash * HashPrime;
        }
    }
}
