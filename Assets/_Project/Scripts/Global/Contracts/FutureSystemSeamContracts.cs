using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Hecton8.Global.Contracts
{
    internal static class FutureSystemSeamContractLayout
    {
        public const int SeamRecordStrideBytes = 64;
        public const int CommandEnvelopeStrideBytes = 64;
        public const int KernelBlackboxEntryStrideBytes = 64;
        public const int FloatBitsStrideBytes = 4;
    }

    public enum FutureSystemSlot : ushort
    {
        Unknown = 0,
        PhysiologyAndDecompression = 21,
        CompileTimeAndAsmdef = 31,
        HardwareScalability = 32,
        TelemetryAndCrashForensics = 33,
        SaveGameMerkleTree = 34,
        ChunkResidencyAndStreaming = 35,
        InputDeterminismAndHaptics = 36,
        PhysicsCullingAndLod = 37,
        QaWatchdogEndurance = 38,
        ZeroGcLocalizationAndSubtitles = 39,
        MasterIntegratorAndDispatcher = 40
    }

    public enum FutureRuntimeSurface : ushort
    {
        None = 0,
        SurvivalOverride = 1,
        HapticPulse = 2,
        SubtitleCue = 3,
        TelemetryMarker = 4,
        QaScenarioMarker = 5,
        ChunkInterestHint = 6,
        SaveHashProbe = 7
    }

    public enum FutureSurfaceOwnerState : byte
    {
        Unknown = 0,
        Absent = 1,
        PartialRuntimeExists = 2,
        ProcessOnly = 3
    }

    [System.Flags]
    public enum FutureSurfaceFlags : ushort
    {
        None = 0,
        ContractOnly = 1 << 0,
        RuntimeUnavailable = 1 << 1,
        RequiresOwnerKernel = 1 << 2,
        RequiresBlackbox = 1 << 3,
        ModFacing = 1 << 4,
        PresentationOnly = 1 << 5,
        ReadOnlyQuery = 1 << 6,
        EditorOrQaOnly = 1 << 7
    }

    [System.Flags]
    public enum FutureSeamValidationError : uint
    {
        None = 0,
        MissingOwnerSlot = 1u << 0,
        MissingSurface = 1u << 1,
        OwnerSlotMismatch = 1u << 2,
        InvalidPayloadSize = 1u << 3,
        InvalidBlackboxCapacity = 1u << 4,
        MissingContractOnlyFlag = 1u << 5,
        MissingRuntimeUnavailableFlag = 1u << 6,
        PublicApiLeak = 1u << 7,
        TtlOutOfRange = 1u << 8,
        ReservedBitsNonZero = 1u << 9,
        NonFiniteScalar = 1u << 10,
        MissingSourceAbsenceProof = 1u << 11,
        CsvParseError = 1u << 12,
        BinaryBufferTooSmall = 1u << 13,
        RecordValidationFailed = 1u << 14
    }

    [StructLayout(LayoutKind.Explicit, Size = FutureSystemSeamContractLayout.SeamRecordStrideBytes)]
    public struct FutureSystemSeamRecord64
    {
        [FieldOffset(0)] public ulong ContractHash;
        [FieldOffset(8)] public ulong EvidenceHash;
        [FieldOffset(16)] public uint OwnerHash;
        [FieldOffset(20)] public uint RuntimeSurfaceHash;
        [FieldOffset(24)] public uint ProofMask;
        [FieldOffset(28)] public uint PayloadSizeBytes;
        [FieldOffset(32)] public ushort Slot;
        [FieldOffset(34)] public ushort Surface;
        [FieldOffset(36)] public ushort Flags;
        [FieldOffset(38)] public byte OwnerState;
        [FieldOffset(39)] public byte SchemaVersion;
        [FieldOffset(40)] public uint BlackboxCapacity;
        [FieldOffset(44)] public uint Reserved0;
        [FieldOffset(48)] public ulong Reserved1;
        [FieldOffset(56)] public ulong Reserved2;
    }

    [StructLayout(LayoutKind.Explicit, Size = FutureSystemSeamContractLayout.CommandEnvelopeStrideBytes)]
    public struct FutureCommandEnvelope64
    {
        [FieldOffset(0)] public ushort ReservedOpcode;
        [FieldOffset(2)] public ushort ReservedTarget;
        [FieldOffset(4)] public ushort Flags;
        [FieldOffset(6)] public ushort ApiVersion;
        [FieldOffset(8)] public ulong Payload0;
        [FieldOffset(16)] public ulong Payload1;
        [FieldOffset(24)] public ulong Payload2;
        [FieldOffset(32)] public ulong Payload3;
        [FieldOffset(40)] public ulong Payload4;
        [FieldOffset(48)] public ulong Payload5;
        [FieldOffset(56)] public ulong Payload6;
    }

    [StructLayout(LayoutKind.Explicit, Size = FutureSystemSeamContractLayout.KernelBlackboxEntryStrideBytes)]
    public struct FutureKernelBlackboxEntry64
    {
        [FieldOffset(0)] public ulong SimTick;
        [FieldOffset(8)] public ulong PayloadHash;
        [FieldOffset(16)] public uint FrameIndex;
        [FieldOffset(20)] public uint SurfaceHash;
        [FieldOffset(24)] public uint ModHash;
        [FieldOffset(28)] public uint RequestId;
        [FieldOffset(32)] public uint RejectReason;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public float Scalar0;
        [FieldOffset(44)] public float Scalar1;
        [FieldOffset(48)] public float Scalar2;
        [FieldOffset(52)] public uint Reserved0;
        [FieldOffset(56)] public ulong Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = FutureSystemSeamContractLayout.FloatBitsStrideBytes)]
    internal struct FutureFloatBits
    {
        [FieldOffset(0)] public float FloatValue;
        [FieldOffset(0)] public uint UIntValue;
    }

    public static class FutureSystemSeamContracts
    {
        public const byte SchemaVersion = 1;
        public const int RecordSizeBytes = 64;
        public const int CommandEnvelopeSizeBytes = 64;
        public const int BlackboxEntrySizeBytes = 64;
        public const int RequiredBlackboxFrames = 300;
        public const int CurrentPublicModCommandCount = 8;
        public const int CurrentPublicModTargetCount = 7;
        public const int CurrentModApiVersion = 2;
        public const ushort ReservedOpcodeNone = 0;
        public const ushort ReservedTargetNone = 0;
        public const ushort FutureKernelReservedOpcodeMin = 0x7800;
        public const ushort FutureKernelReservedOpcodeMax = 0x78FF;
        public const ushort FutureKernelReservedTargetMin = 0x7800;
        public const ushort FutureKernelReservedTargetMax = 0x78FF;
        public const int FutureKernelReservedOpcodeCount = FutureKernelReservedOpcodeMax - FutureKernelReservedOpcodeMin + 1;
        public const ushort SurvivalOverrideMaxTtlMs = 3000;
        public const ushort SurvivalOverrideAllowedFlags = 0x0003;

        public const uint ProofSourceEnumAbsent = 1u << 0;
        public const uint ProofOwnerKernelAbsent = 1u << 1;
        public const uint ProofDocsLinked = 1u << 2;
        public const uint ProofValidatorRequired = 1u << 3;

        public const uint SurvivalOverrideHash = 0x53564F32u;
        public const uint HapticPulseHash = 0x48505443u;
        public const uint SubtitleCueHash = 0x53554243u;
        public const uint TelemetryMarkerHash = 0x544C4D4Bu;
        public const uint QaScenarioMarkerHash = 0x51415343u;
        public const uint ChunkInterestHintHash = 0x43494E54u;
        public const uint SaveHashProbeHash = 0x53485042u;

        private const ulong ContractHashSeed = 14695981039346656037UL;
        private const ulong ContractHashPrime = 1099511628211UL;
        private const uint RequiredProofMask =
            ProofSourceEnumAbsent |
            ProofOwnerKernelAbsent |
            ProofDocsLinked |
            ProofValidatorRequired;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetSurfaceHash(FutureRuntimeSurface surface)
        {
            switch (surface)
            {
                case FutureRuntimeSurface.SurvivalOverride:
                    return SurvivalOverrideHash;
                case FutureRuntimeSurface.HapticPulse:
                    return HapticPulseHash;
                case FutureRuntimeSurface.SubtitleCue:
                    return SubtitleCueHash;
                case FutureRuntimeSurface.TelemetryMarker:
                    return TelemetryMarkerHash;
                case FutureRuntimeSurface.QaScenarioMarker:
                    return QaScenarioMarkerHash;
                case FutureRuntimeSurface.ChunkInterestHint:
                    return ChunkInterestHintHash;
                case FutureRuntimeSurface.SaveHashProbe:
                    return SaveHashProbeHash;
                default:
                    return 0u;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FutureSystemSlot GetOwnerSlot(FutureRuntimeSurface surface)
        {
            switch (surface)
            {
                case FutureRuntimeSurface.SurvivalOverride:
                    return FutureSystemSlot.PhysiologyAndDecompression;
                case FutureRuntimeSurface.HapticPulse:
                    return FutureSystemSlot.InputDeterminismAndHaptics;
                case FutureRuntimeSurface.SubtitleCue:
                    return FutureSystemSlot.ZeroGcLocalizationAndSubtitles;
                case FutureRuntimeSurface.TelemetryMarker:
                    return FutureSystemSlot.TelemetryAndCrashForensics;
                case FutureRuntimeSurface.QaScenarioMarker:
                    return FutureSystemSlot.QaWatchdogEndurance;
                case FutureRuntimeSurface.ChunkInterestHint:
                    return FutureSystemSlot.ChunkResidencyAndStreaming;
                case FutureRuntimeSurface.SaveHashProbe:
                    return FutureSystemSlot.SaveGameMerkleTree;
                default:
                    return FutureSystemSlot.Unknown;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FutureSurfaceOwnerState GetOwnerState(FutureSystemSlot slot)
        {
            return slot == FutureSystemSlot.MasterIntegratorAndDispatcher
                ? FutureSurfaceOwnerState.ProcessOnly
                : slot == FutureSystemSlot.Unknown
                    ? FutureSurfaceOwnerState.Unknown
                    : FutureSurfaceOwnerState.PartialRuntimeExists;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFutureKernelReservedOpcode(ushort opcode)
        {
            return opcode >= FutureKernelReservedOpcodeMin && opcode <= FutureKernelReservedOpcodeMax;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFutureKernelReservedTarget(ushort target)
        {
            return target >= FutureKernelReservedTargetMin && target <= FutureKernelReservedTargetMax;
        }

        public static FutureSeamValidationError ValidatePublicModCommandSurface(ushort opcode, ushort target)
        {
            return IsFutureKernelReservedOpcode(opcode) || IsFutureKernelReservedTarget(target)
                ? FutureSeamValidationError.PublicApiLeak
                : FutureSeamValidationError.None;
        }

        public static bool TryBuildReservation(FutureRuntimeSurface surface, out FutureSystemSeamRecord64 record)
        {
            FutureSystemSlot slot = GetOwnerSlot(surface);
            if (slot == FutureSystemSlot.Unknown)
            {
                record = default;
                return false;
            }

            FutureSurfaceFlags flags = FutureSurfaceFlags.ContractOnly |
                                       FutureSurfaceFlags.RuntimeUnavailable |
                                       FutureSurfaceFlags.RequiresOwnerKernel |
                                       FutureSurfaceFlags.RequiresBlackbox |
                                       FutureSurfaceFlags.ModFacing;

            if (surface == FutureRuntimeSurface.HapticPulse ||
                surface == FutureRuntimeSurface.SubtitleCue)
            {
                flags |= FutureSurfaceFlags.PresentationOnly;
            }

            if (surface == FutureRuntimeSurface.SaveHashProbe)
            {
                flags |= FutureSurfaceFlags.ReadOnlyQuery;
            }

            if (surface == FutureRuntimeSurface.QaScenarioMarker)
            {
                flags |= FutureSurfaceFlags.EditorOrQaOnly;
            }

            record = new FutureSystemSeamRecord64
            {
                ContractHash = BuildContractHash(slot, surface),
                EvidenceHash = BuildEvidenceHash(slot, surface),
                OwnerHash = (uint)slot,
                RuntimeSurfaceHash = GetSurfaceHash(surface),
                ProofMask = RequiredProofMask,
                PayloadSizeBytes = CommandEnvelopeSizeBytes,
                Slot = (ushort)slot,
                Surface = (ushort)surface,
                Flags = (ushort)flags,
                OwnerState = (byte)GetOwnerState(slot),
                SchemaVersion = SchemaVersion,
                BlackboxCapacity = RequiredBlackboxFrames
            };
            return true;
        }

        public static FutureSeamValidationError ValidateReservation(in FutureSystemSeamRecord64 record)
        {
            FutureSeamValidationError errors = FutureSeamValidationError.None;
            FutureSystemSlot slot = (FutureSystemSlot)record.Slot;
            FutureRuntimeSurface surface = (FutureRuntimeSurface)record.Surface;
            FutureSurfaceFlags flags = (FutureSurfaceFlags)record.Flags;

            if (slot == FutureSystemSlot.Unknown)
                errors |= FutureSeamValidationError.MissingOwnerSlot;

            if (surface == FutureRuntimeSurface.None)
                errors |= FutureSeamValidationError.MissingSurface;

            if (surface != FutureRuntimeSurface.None && GetOwnerSlot(surface) != slot)
                errors |= FutureSeamValidationError.OwnerSlotMismatch;

            if (record.PayloadSizeBytes != CommandEnvelopeSizeBytes)
                errors |= FutureSeamValidationError.InvalidPayloadSize;

            if (record.BlackboxCapacity != RequiredBlackboxFrames)
                errors |= FutureSeamValidationError.InvalidBlackboxCapacity;

            if ((flags & FutureSurfaceFlags.ContractOnly) == 0)
                errors |= FutureSeamValidationError.MissingContractOnlyFlag;

            if ((flags & FutureSurfaceFlags.RuntimeUnavailable) == 0)
                errors |= FutureSeamValidationError.MissingRuntimeUnavailableFlag;

            if ((record.ProofMask & RequiredProofMask) != RequiredProofMask)
                errors |= FutureSeamValidationError.MissingSourceAbsenceProof;

            return errors;
        }

        public static FutureCommandEnvelope64 BuildSurvivalOverrideEnvelope(
            uint modHash,
            uint requestId,
            uint targetPlayerHash,
            ushort ttlMilliseconds,
            ushort overrideFlags,
            float oxygenFloor01)
        {
            FutureCommandEnvelope64 command = default;
            command.ReservedOpcode = ReservedOpcodeNone;
            command.ReservedTarget = ReservedTargetNone;
            command.ApiVersion = CurrentModApiVersion;
            command.Payload0 = ((ulong)requestId << 32) | modHash;
            command.Payload1 = targetPlayerHash |
                               ((ulong)ClampTtl(ttlMilliseconds) << 32) |
                               ((ulong)(overrideFlags & SurvivalOverrideAllowedFlags) << 48);
            command.Payload2 = ClampUnitFloatBits(oxygenFloor01);
            return command;
        }

        public static FutureSeamValidationError ValidateSurvivalOverrideEnvelope(in FutureCommandEnvelope64 command)
        {
            FutureSeamValidationError errors = FutureSeamValidationError.None;

            if (command.ReservedOpcode != ReservedOpcodeNone || command.ReservedTarget != ReservedTargetNone)
                errors |= FutureSeamValidationError.PublicApiLeak;

            ushort ttlMilliseconds = unchecked((ushort)((command.Payload1 >> 32) & 0xFFFFUL));
            ushort flags = unchecked((ushort)((command.Payload1 >> 48) & 0xFFFFUL));

            if (ttlMilliseconds > SurvivalOverrideMaxTtlMs)
                errors |= FutureSeamValidationError.TtlOutOfRange;

            if ((flags & ~SurvivalOverrideAllowedFlags) != 0 ||
                (command.Payload2 & 0xFFFFFFFF00000000UL) != 0UL ||
                command.Payload3 != 0UL ||
                command.Payload4 != 0UL ||
                command.Payload5 != 0UL ||
                command.Payload6 != 0UL)
            {
                errors |= FutureSeamValidationError.ReservedBitsNonZero;
            }

            float oxygenFloor01 = UIntToFloat(unchecked((uint)(command.Payload2 & 0xFFFFFFFFUL)));
            if (!(oxygenFloor01 >= 0f && oxygenFloor01 <= 1f))
                errors |= FutureSeamValidationError.NonFiniteScalar;

            return errors;
        }

        public static FutureKernelBlackboxEntry64 BuildBlackboxEntry(
            uint frameIndex,
            ulong simTick,
            FutureRuntimeSurface surface,
            in FutureCommandEnvelope64 command,
            FutureSeamValidationError rejectReason,
            uint flags)
        {
            return new FutureKernelBlackboxEntry64
            {
                SimTick = simTick,
                PayloadHash = HashCommandEnvelope(command),
                FrameIndex = frameIndex,
                SurfaceHash = GetSurfaceHash(surface),
                ModHash = GetModHash(command),
                RequestId = GetRequestId(command),
                RejectReason = (uint)rejectReason,
                Flags = flags
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AdvanceBlackboxCursor(int currentCursor)
        {
            int next = currentCursor + 1;
            return next >= RequiredBlackboxFrames ? 0 : next;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetModHash(in FutureCommandEnvelope64 command)
        {
            return unchecked((uint)(command.Payload0 & 0xFFFFFFFFUL));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetRequestId(in FutureCommandEnvelope64 command)
        {
            return unchecked((uint)(command.Payload0 >> 32));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort ClampTtl(ushort ttlMilliseconds)
        {
            return ttlMilliseconds > SurvivalOverrideMaxTtlMs ? SurvivalOverrideMaxTtlMs : ttlMilliseconds;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ClampUnitFloatBits(float value)
        {
            if (!(value >= 0f))
                value = 0f;
            else if (value > 1f)
                value = 1f;

            return FloatToUInt(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint FloatToUInt(float value)
        {
            FutureFloatBits bits = default;
            bits.FloatValue = value;
            return bits.UIntValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float UIntToFloat(uint value)
        {
            FutureFloatBits bits = default;
            bits.UIntValue = value;
            return bits.FloatValue;
        }

        private static ulong BuildContractHash(FutureSystemSlot slot, FutureRuntimeSurface surface)
        {
            ulong hash = ContractHashSeed;
            hash = Mix(hash, (ulong)slot);
            hash = Mix(hash, (ulong)surface);
            hash = Mix(hash, SchemaVersion);
            return Mix(hash, CommandEnvelopeSizeBytes);
        }

        private static ulong BuildEvidenceHash(FutureSystemSlot slot, FutureRuntimeSurface surface)
        {
            ulong hash = ContractHashSeed;
            hash = Mix(hash, (ulong)slot << 32);
            hash = Mix(hash, GetSurfaceHash(surface));
            return Mix(hash, RequiredProofMask);
        }

        private static ulong HashCommandEnvelope(in FutureCommandEnvelope64 command)
        {
            ulong hash = ContractHashSeed;
            hash = Mix(hash, command.ReservedOpcode);
            hash = Mix(hash, command.ReservedTarget);
            hash = Mix(hash, command.Flags);
            hash = Mix(hash, command.ApiVersion);
            hash = Mix(hash, command.Payload0);
            hash = Mix(hash, command.Payload1);
            hash = Mix(hash, command.Payload2);
            hash = Mix(hash, command.Payload3);
            hash = Mix(hash, command.Payload4);
            hash = Mix(hash, command.Payload5);
            return Mix(Mix(hash, command.Payload6), SchemaVersion);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Mix(ulong hash, ulong word)
        {
            hash ^= word;
            return hash * ContractHashPrime;
        }
    }
}
