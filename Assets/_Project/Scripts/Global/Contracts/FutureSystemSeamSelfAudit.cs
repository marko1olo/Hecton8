using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Hecton8.Global.Contracts
{
    internal static class FutureSystemSeamSelfAuditLayout
    {
        internal const int ReportStrideBytes = 64;
    }

    [System.Flags]
    public enum FutureSeamAuditFlags : uint
    {
        None = 0,
        RecordsValid = 1u << 0,
        AllCurrentReservationsPresent = 1u << 1,
        BinaryWriteValid = 1u << 2,
        BlackboxRingValid = 1u << 3,
        SurvivalEnvelopeValid = 1u << 4,
        PublicApiStillClosed = 1u << 5
    }

    /// <summary>
    /// Fixed self-audit report for dormant future-seam contracts. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = FutureSystemSeamSelfAuditLayout.ReportStrideBytes)]
    public struct FutureSystemSeamAuditReport64
    {
        [FieldOffset(0)] public ulong ContractHash;
        [FieldOffset(8)] public ulong EvidenceHash;
        [FieldOffset(16)] public uint CheckedSurfaceMask;
        [FieldOffset(20)] public uint ErrorMask;
        [FieldOffset(24)] public uint RecordCount;
        [FieldOffset(28)] public uint BinaryBytes;
        [FieldOffset(32)] public uint BlackboxFrames;
        [FieldOffset(36)] public uint PublicApiOpcodeCount;
        [FieldOffset(40)] public uint PublicApiTargetCount;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public ulong Reserved1;
        [FieldOffset(56)] public ulong Reserved2;
    }

    /// <summary>
    /// Stateless self-audit helpers for future seam reservations.
    /// </summary>
    public static class FutureSystemSeamSelfAudit
    {
        public const int ReportSizeBytes = FutureSystemSeamSelfAuditLayout.ReportStrideBytes;
        public const int RequiredReservationCount = 7;
        public const uint RequiredSurfaceMask = 0x000000FEu;

        private const ulong HashSeed = 14695981039346656037UL;
        private const ulong HashPrime = 1099511628211UL;
        /// <summary>Builds the seven current dormant reservations into caller-owned storage.</summary>
        public static int BuildDefaultReservations(
            Span<FutureSystemSeamRecord64> records,
            out FutureSeamValidationError errors)
        {
            errors = FutureSeamValidationError.None;
            if (records.Length < RequiredReservationCount)
            {
                errors = FutureSeamValidationError.BinaryBufferTooSmall;
                return 0;
            }

            int count = 0;
            AppendReservation(FutureRuntimeSurface.SurvivalOverride, records, ref count, ref errors);
            AppendReservation(FutureRuntimeSurface.HapticPulse, records, ref count, ref errors);
            AppendReservation(FutureRuntimeSurface.SubtitleCue, records, ref count, ref errors);
            AppendReservation(FutureRuntimeSurface.TelemetryMarker, records, ref count, ref errors);
            AppendReservation(FutureRuntimeSurface.QaScenarioMarker, records, ref count, ref errors);
            AppendReservation(FutureRuntimeSurface.ChunkInterestHint, records, ref count, ref errors);
            AppendReservation(FutureRuntimeSurface.SaveHashProbe, records, ref count, ref errors);
            return count;
        }

        /// <summary>
        /// Runs a deterministic audit using only caller-provided scratch buffers.
        /// </summary>
        public static bool Run(
            ReadOnlySpan<FutureSystemSeamRecord64> records,
            Span<byte> binaryScratch,
            Span<FutureKernelBlackboxEntry64> blackboxScratch,
            out FutureSystemSeamAuditReport64 report)
        {
            uint flags = 0u;
            int bytesWritten = 0;
            uint surfaceMask = BuildSurfaceMask(records);
            FutureSeamValidationError errors = FutureSystemSeamPacking.ValidateRecords(records);

            if (errors == FutureSeamValidationError.None && records.Length > 0)
                flags |= (uint)FutureSeamAuditFlags.RecordsValid;

            if (records.Length == RequiredReservationCount && (surfaceMask & RequiredSurfaceMask) == RequiredSurfaceMask)
                flags |= (uint)FutureSeamAuditFlags.AllCurrentReservationsPresent;
            else
                errors |= FutureSeamValidationError.MissingSurface;

            if (FutureSystemSeamPacking.TryWriteBinary(records, binaryScratch, out bytesWritten, out FutureSeamValidationError writeErrors))
                flags |= (uint)FutureSeamAuditFlags.BinaryWriteValid;
            else
                errors |= writeErrors;

            FutureCommandEnvelope64 survivalEnvelope = FutureSystemSeamContracts.BuildSurvivalOverrideEnvelope(
                0x48385352u,
                1u,
                0u,
                FutureSystemSeamContracts.SurvivalOverrideMaxTtlMs,
                FutureSystemSeamContracts.SurvivalOverrideAllowedFlags,
                1f);

            FutureSeamValidationError survivalErrors =
                FutureSystemSeamContracts.ValidateSurvivalOverrideEnvelope(in survivalEnvelope);
            if (survivalErrors == FutureSeamValidationError.None)
                flags |= (uint)FutureSeamAuditFlags.SurvivalEnvelopeValid;
            else
                errors |= survivalErrors;

            if (RunBlackboxProbe(blackboxScratch, in survivalEnvelope))
                flags |= (uint)FutureSeamAuditFlags.BlackboxRingValid;
            else
                errors |= FutureSeamValidationError.InvalidBlackboxCapacity;

            if (FutureSystemSeamContracts.CurrentPublicModCommandCount == GetExpectedPublicModCommandCount() &&
                FutureSystemSeamContracts.CurrentPublicModTargetCount == GetExpectedPublicModTargetCount())
            {
                flags |= (uint)FutureSeamAuditFlags.PublicApiStillClosed;
            }
            else
            {
                errors |= FutureSeamValidationError.PublicApiLeak;
            }

            uint binaryBytes = bytesWritten > 0
                ? unchecked((uint)bytesWritten)
                : unchecked((uint)FutureSystemSeamPacking.ComputeBinarySize(records.Length));

            report = new FutureSystemSeamAuditReport64
            {
                ContractHash = BuildContractHash(records, surfaceMask, flags),
                EvidenceHash = BuildEvidenceHash(surfaceMask, errors, flags, binaryBytes),
                CheckedSurfaceMask = surfaceMask,
                ErrorMask = (uint)errors,
                RecordCount = unchecked((uint)records.Length),
                BinaryBytes = binaryBytes,
                BlackboxFrames = FutureSystemSeamContracts.RequiredBlackboxFrames,
                PublicApiOpcodeCount = FutureSystemSeamContracts.CurrentPublicModCommandCount,
                PublicApiTargetCount = FutureSystemSeamContracts.CurrentPublicModTargetCount,
                Flags = flags
            };

            return errors == FutureSeamValidationError.None;
        }

        private static void AppendReservation(
            FutureRuntimeSurface surface,
            Span<FutureSystemSeamRecord64> records,
            ref int count,
            ref FutureSeamValidationError errors)
        {
            if (!FutureSystemSeamContracts.TryBuildReservation(surface, out FutureSystemSeamRecord64 record))
            {
                errors |= FutureSeamValidationError.MissingSurface;
                return;
            }

            FutureSeamValidationError recordErrors = FutureSystemSeamContracts.ValidateReservation(in record);
            if (recordErrors != FutureSeamValidationError.None)
            {
                errors |= recordErrors;
                return;
            }

            records[count++] = record;
        }

        private static bool RunBlackboxProbe(
            Span<FutureKernelBlackboxEntry64> blackboxScratch,
            in FutureCommandEnvelope64 command)
        {
            FutureKernelBlackboxRingState64 state = FutureKernelBlackboxRing.CreateState();
            FutureKernelBlackboxEntry64 entry = FutureSystemSeamContracts.BuildBlackboxEntry(
                1u,
                1UL,
                FutureRuntimeSurface.SurvivalOverride,
                in command,
                FutureSeamValidationError.None,
                0u);

            if (!FutureKernelBlackboxRing.TryAppend(blackboxScratch, ref state, in entry))
                return false;

            if (!FutureKernelBlackboxRing.TryReadLatest(blackboxScratch, in state, out FutureKernelBlackboxEntry64 latest))
                return false;

            return latest.PayloadHash == entry.PayloadHash &&
                   latest.SurfaceHash == entry.SurfaceHash &&
                   state.Capacity == FutureSystemSeamContracts.RequiredBlackboxFrames;
        }

        private static uint BuildSurfaceMask(ReadOnlySpan<FutureSystemSeamRecord64> records)
        {
            uint mask = 0u;
            for (int i = 0; i < records.Length; i++)
            {
                ushort surface = records[i].Surface;
                if (surface < 32)
                    mask |= 1u << surface;
            }

            return mask;
        }

        private static ulong BuildContractHash(
            ReadOnlySpan<FutureSystemSeamRecord64> records,
            uint surfaceMask,
            uint flags)
        {
            ulong hash = HashSeed;
            hash = Mix(hash, unchecked((ulong)records.Length));
            hash = Mix(hash, surfaceMask);
            hash = Mix(hash, flags);
            for (int i = 0; i < records.Length; i++)
            {
                hash = Mix(hash, records[i].ContractHash);
                hash = Mix(hash, records[i].RuntimeSurfaceHash);
            }

            return hash;
        }

        private static ulong BuildEvidenceHash(
            uint surfaceMask,
            FutureSeamValidationError errors,
            uint flags,
            uint binaryBytes)
        {
            ulong hash = HashSeed;
            hash = Mix(hash, surfaceMask);
            hash = Mix(hash, (uint)errors);
            hash = Mix(hash, flags);
            hash = Mix(hash, binaryBytes);
            hash = Mix(hash, FutureSystemSeamContracts.RequiredBlackboxFrames);
            hash = Mix(hash, FutureSystemSeamContracts.CurrentPublicModCommandCount);
            return Mix(hash, FutureSystemSeamContracts.CurrentPublicModTargetCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint GetExpectedPublicModCommandCount()
        {
            return 8u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint GetExpectedPublicModTargetCount()
        {
            return 7u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Mix(ulong hash, ulong word)
        {
            hash ^= word;
            return hash * HashPrime;
        }
    }
}
