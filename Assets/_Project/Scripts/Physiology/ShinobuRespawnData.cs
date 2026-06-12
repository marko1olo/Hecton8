using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Physiology
{
    public static class ShinobuRespawnConstants
    {
        public const int TelemetryFrameCount = 300;
        public const int MockMedicalBayCapacity = 8;
        public const int PenaltyRuleCapacity = 64;
        public const int CsvScratchBytes = 32768;
        public const int RespawnStateSizeBytes = 32;
        public const int RespawnRequestSizeBytes = 64;
        public const int MedicalBaySizeBytes = 32;
        public const int RespawnFadeSizeBytes = 32;
        public const int RespawnTuningSizeBytes = 64;
        public const int RespawnPenaltyRuleSizeBytes = 16;
        public const int InventoryCommandSignalSizeBytes = 32;
        public const int InventoryRespawnDeathAupSignalSizeBytes = 64;
        public const int InventoryDeathLootCacheSignalSizeBytes = 128;
        public const int InventoryRespawnPenaltyResultSignalSizeBytes = 32;
        public const int PlayerRespawnSignalSizeBytes = 128;
        public const int RespawnTelemetryEntrySizeBytes = 64;
        public const int RespawnTelemetryCursorSizeBytes = 64;
        public const int TelemetryDroppedItemShift = 16;
        public const uint TelemetryDroppedItemMask = 0x00FF0000u;
        public const int MedicalBayPriorityShift = 16;
        public const uint MedicalBayPriorityMask = 0x00FF0000u;
        public const uint SourceHash = 0x53333239u; // S329

        public const BufferID RespawnStateBuffer = BufferID.ShinobuRespawnData_RespawnStateBuffer;
        public const BufferID MedicalBayRespawnPointsBuffer = BufferID.ShinobuRespawnData_MedicalBayRespawnPointsBuffer;
        public const BufferID RespawnFadeBuffer = BufferID.ShinobuRespawnData_RespawnFadeBuffer;
        public const BufferID RespawnTelemetryRingBuffer = BufferID.ShinobuRespawnData_RespawnTelemetryRingBuffer;
        public const BufferID RespawnTelemetryCursorBuffer = BufferID.ShinobuRespawnData_RespawnTelemetryCursorBuffer;
        public const BufferID RespawnTuningBuffer = BufferID.ShinobuRespawnData_RespawnTuningBuffer;
        public const BufferID RespawnPenaltyRulesBuffer = BufferID.ShinobuRespawnData_RespawnPenaltyRulesBuffer;
        public const BufferID RespawnPenaltyRuleCountBuffer = BufferID.ShinobuRespawnData_RespawnPenaltyRuleCountBuffer;
        public const BufferID RespawnRequestBuffer = BufferID.ShinobuRespawnData_RespawnRequestBuffer;
    }

    public static class ShinobuRespawnFlags
    {
        public const uint RespawnActive = 1u << 0;
        public const uint PendingRequest = 1u << 1;
        public const uint PenaltyApplied = 1u << 2;
        public const uint MockMedicalBay = 1u << 3;
        public const uint FallbackLifepod = 1u << 4;
        public const uint InvalidTargetAup = 1u << 5;
        public const uint Committed = 1u << 6;
        public const uint ManualTuning = 1u << 7;
        public const uint MedicalBayActive = 1u << 8;
        public const uint MedicalBayPowered = 1u << 9;
        public const uint DeathSequenceBlackoutPrimed = 1u << 10;
        public const uint CanceledByLoad = 1u << 11;
        public const uint NanDetected = 1u << 31;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct RespawnStateDTO
    {
        [FieldOffset(0)] public double3 TargetAUP;
        [FieldOffset(24)] public uint MedicalBayHashID;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct RespawnRequestDTO
    {
        [FieldOffset(0)] public double3 DeathAUP;
        [FieldOffset(24)] public uint PlayerHash;
        [FieldOffset(28)] public uint DamageHash;
        [FieldOffset(32)] public uint Frame;
        [FieldOffset(36)] public uint Sequence;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public uint MedicalBayHashID;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MedicalBayDTO
    {
        [FieldOffset(0)] public double3 BayAUP;
        [FieldOffset(24)] public uint AssociatedBaseHash;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct RespawnFadeDTO
    {
        [FieldOffset(0)] public float DeathFadeIntensity;
        [FieldOffset(4)] public float FadeRate;
        [FieldOffset(8)] public float ChromaticAberration01;
        [FieldOffset(12)] public float FilmGrain01;
        [FieldOffset(16)] public float GlobalQualityWeight;
        [FieldOffset(20)] public uint Frame;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct RespawnTuningDTO
    {
        [FieldOffset(0)] public double3 FallbackLifepodAUP;
        [FieldOffset(24)] public float HighQualityFadeRate;
        [FieldOffset(28)] public float LowQualityFadeRate;
        [FieldOffset(32)] public float PenaltyMultiplier;
        [FieldOffset(36)] public float ValidationClearanceMeters;
        [FieldOffset(40)] public float RespawnInvulnerabilitySeconds;
        [FieldOffset(44)] public float MedicalBaySearchRadiusMeters;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint Version;
        [FieldOffset(56)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct RespawnTelemetryEntry
    {
        [FieldOffset(0)] public double3 DeathAUP;
        [FieldOffset(24)] public double3 RespawnAUP;
        [FieldOffset(48)] public uint CauseHash;
        [FieldOffset(52)] public uint Frame;
        [FieldOffset(56)] public float ReconcileMicroseconds;
        [FieldOffset(60)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct RespawnTelemetryCursor64
    {
        [FieldOffset(0)] public int Cursor;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public ulong _pad0;
        [FieldOffset(16)] public ulong _pad1;
        [FieldOffset(24)] public ulong _pad2;
        [FieldOffset(32)] public ulong _pad3;
        [FieldOffset(40)] public ulong _pad4;
        [FieldOffset(48)] public ulong _pad5;
        [FieldOffset(56)] public ulong _pad6;
    }

    public static class ShinobuRespawnLayoutGuards
    {
        public static bool ValidateRespawnLayouts()
        {
            return ValidateRespawnStateLayout() &&
                   ValidateRespawnRequestLayout() &&
                   ValidateMedicalBayLayout() &&
                   ValidateRespawnFadeLayout() &&
                   ValidateRespawnTuningLayout() &&
                   ValidatePenaltyRuleLayout() &&
                   ValidateInventoryCommandSignalLayout() &&
                   ValidateInventoryRespawnDeathAupSignalLayout() &&
                   ValidateInventoryDeathLootCacheSignalLayout() &&
                   ValidateInventoryRespawnPenaltyResultSignalLayout() &&
                   ValidatePlayerRespawnSignalLayout() &&
                   ValidatePlayerRespawnSignalPhase() &&
                   ValidatePlayerRespawnSignalFlags() &&
                   ValidateTelemetryEntryLayout() &&
                   ValidateTelemetryCursorLayout();
        }

        private static bool ValidateRespawnStateLayout()
        {
            return UnsafeUtility.SizeOf<RespawnStateDTO>() == ShinobuRespawnConstants.RespawnStateSizeBytes &&
                   OffsetOf<RespawnStateDTO>(nameof(RespawnStateDTO.TargetAUP)) == 0 &&
                   OffsetOf<RespawnStateDTO>(nameof(RespawnStateDTO.MedicalBayHashID)) == 24 &&
                   OffsetOf<RespawnStateDTO>(nameof(RespawnStateDTO.Flags)) == 28;
        }

        private static bool ValidateRespawnRequestLayout()
        {
            return UnsafeUtility.SizeOf<RespawnRequestDTO>() == ShinobuRespawnConstants.RespawnRequestSizeBytes &&
                   OffsetOf<RespawnRequestDTO>(nameof(RespawnRequestDTO.DeathAUP)) == 0 &&
                   OffsetOf<RespawnRequestDTO>(nameof(RespawnRequestDTO.PlayerHash)) == 24 &&
                   OffsetOf<RespawnRequestDTO>(nameof(RespawnRequestDTO.DamageHash)) == 28 &&
                   OffsetOf<RespawnRequestDTO>(nameof(RespawnRequestDTO.Frame)) == 32 &&
                   OffsetOf<RespawnRequestDTO>(nameof(RespawnRequestDTO.Sequence)) == 36 &&
                   OffsetOf<RespawnRequestDTO>(nameof(RespawnRequestDTO.Flags)) == 40 &&
                   OffsetOf<RespawnRequestDTO>(nameof(RespawnRequestDTO.MedicalBayHashID)) == 44 &&
                   OffsetOf<RespawnRequestDTO>(nameof(RespawnRequestDTO._pad0)) == 48 &&
                   OffsetOf<RespawnRequestDTO>(nameof(RespawnRequestDTO._pad1)) == 56;
        }

        private static bool ValidateMedicalBayLayout()
        {
            return UnsafeUtility.SizeOf<MedicalBayDTO>() == ShinobuRespawnConstants.MedicalBaySizeBytes &&
                   OffsetOf<MedicalBayDTO>(nameof(MedicalBayDTO.BayAUP)) == 0 &&
                   OffsetOf<MedicalBayDTO>(nameof(MedicalBayDTO.AssociatedBaseHash)) == 24 &&
                   OffsetOf<MedicalBayDTO>(nameof(MedicalBayDTO.Flags)) == 28;
        }

        private static bool ValidateRespawnFadeLayout()
        {
            return UnsafeUtility.SizeOf<RespawnFadeDTO>() == ShinobuRespawnConstants.RespawnFadeSizeBytes &&
                   OffsetOf<RespawnFadeDTO>(nameof(RespawnFadeDTO.DeathFadeIntensity)) == 0 &&
                   OffsetOf<RespawnFadeDTO>(nameof(RespawnFadeDTO.FadeRate)) == 4 &&
                   OffsetOf<RespawnFadeDTO>(nameof(RespawnFadeDTO.ChromaticAberration01)) == 8 &&
                   OffsetOf<RespawnFadeDTO>(nameof(RespawnFadeDTO.FilmGrain01)) == 12 &&
                   OffsetOf<RespawnFadeDTO>(nameof(RespawnFadeDTO.GlobalQualityWeight)) == 16 &&
                   OffsetOf<RespawnFadeDTO>(nameof(RespawnFadeDTO.Frame)) == 20 &&
                   OffsetOf<RespawnFadeDTO>(nameof(RespawnFadeDTO.Flags)) == 24 &&
                   OffsetOf<RespawnFadeDTO>(nameof(RespawnFadeDTO._pad0)) == 28;
        }

        private static bool ValidateRespawnTuningLayout()
        {
            return UnsafeUtility.SizeOf<RespawnTuningDTO>() == ShinobuRespawnConstants.RespawnTuningSizeBytes &&
                   OffsetOf<RespawnTuningDTO>(nameof(RespawnTuningDTO.FallbackLifepodAUP)) == 0 &&
                   OffsetOf<RespawnTuningDTO>(nameof(RespawnTuningDTO.HighQualityFadeRate)) == 24 &&
                   OffsetOf<RespawnTuningDTO>(nameof(RespawnTuningDTO.LowQualityFadeRate)) == 28 &&
                   OffsetOf<RespawnTuningDTO>(nameof(RespawnTuningDTO.PenaltyMultiplier)) == 32 &&
                   OffsetOf<RespawnTuningDTO>(nameof(RespawnTuningDTO.ValidationClearanceMeters)) == 36 &&
                   OffsetOf<RespawnTuningDTO>(nameof(RespawnTuningDTO.RespawnInvulnerabilitySeconds)) == 40 &&
                   OffsetOf<RespawnTuningDTO>(nameof(RespawnTuningDTO.MedicalBaySearchRadiusMeters)) == 44 &&
                   OffsetOf<RespawnTuningDTO>(nameof(RespawnTuningDTO.Flags)) == 48 &&
                   OffsetOf<RespawnTuningDTO>(nameof(RespawnTuningDTO.Version)) == 52 &&
                   OffsetOf<RespawnTuningDTO>(nameof(RespawnTuningDTO._pad0)) == 56;
        }

        private static bool ValidatePenaltyRuleLayout()
        {
            return UnsafeUtility.SizeOf<InventoryDeathPenaltyRuleDTO>() == ShinobuRespawnConstants.RespawnPenaltyRuleSizeBytes &&
                   OffsetOf<InventoryDeathPenaltyRuleDTO>(nameof(InventoryDeathPenaltyRuleDTO.ItemHash)) == 0 &&
                   OffsetOf<InventoryDeathPenaltyRuleDTO>(nameof(InventoryDeathPenaltyRuleDTO.DropOnDeath)) == 4 &&
                   OffsetOf<InventoryDeathPenaltyRuleDTO>(nameof(InventoryDeathPenaltyRuleDTO.RetainIfEquipped)) == 5 &&
                   OffsetOf<InventoryDeathPenaltyRuleDTO>(nameof(InventoryDeathPenaltyRuleDTO.Reserved0)) == 6 &&
                   OffsetOf<InventoryDeathPenaltyRuleDTO>(nameof(InventoryDeathPenaltyRuleDTO.Flags)) == 8 &&
                   OffsetOf<InventoryDeathPenaltyRuleDTO>(nameof(InventoryDeathPenaltyRuleDTO._pad0)) == 12;
        }

        private static bool ValidateInventoryCommandSignalLayout()
        {
            return UnsafeUtility.SizeOf<InventoryCommandSignal>() == ShinobuRespawnConstants.InventoryCommandSignalSizeBytes &&
                   OffsetOf<InventoryCommandSignal>(nameof(InventoryCommandSignal.InventoryHash)) == 0 &&
                   OffsetOf<InventoryCommandSignal>(nameof(InventoryCommandSignal.Frame)) == 4 &&
                   OffsetOf<InventoryCommandSignal>(nameof(InventoryCommandSignal.Sequence)) == 8 &&
                   OffsetOf<InventoryCommandSignal>(nameof(InventoryCommandSignal.Command)) == 12 &&
                   OffsetOf<InventoryCommandSignal>(nameof(InventoryCommandSignal.Flags)) == 13 &&
                   OffsetOf<InventoryCommandSignal>(nameof(InventoryCommandSignal.PayloadFlags)) == 14 &&
                   OffsetOf<InventoryCommandSignal>(nameof(InventoryCommandSignal.Payload0)) == 16 &&
                   OffsetOf<InventoryCommandSignal>(nameof(InventoryCommandSignal.Payload1)) == 20 &&
                   OffsetOf<InventoryCommandSignal>(nameof(InventoryCommandSignal.Payload2)) == 24 &&
                   OffsetOf<InventoryCommandSignal>(nameof(InventoryCommandSignal.Payload3)) == 28;
        }

        private static bool ValidateInventoryRespawnPenaltyResultSignalLayout()
        {
            return UnsafeUtility.SizeOf<InventoryRespawnPenaltyResultSignal>() == ShinobuRespawnConstants.InventoryRespawnPenaltyResultSignalSizeBytes &&
                   OffsetOf<InventoryRespawnPenaltyResultSignal>(nameof(InventoryRespawnPenaltyResultSignal.InventoryHash)) == 0 &&
                   OffsetOf<InventoryRespawnPenaltyResultSignal>(nameof(InventoryRespawnPenaltyResultSignal.Frame)) == 4 &&
                   OffsetOf<InventoryRespawnPenaltyResultSignal>(nameof(InventoryRespawnPenaltyResultSignal.Sequence)) == 8 &&
                   OffsetOf<InventoryRespawnPenaltyResultSignal>(nameof(InventoryRespawnPenaltyResultSignal.DroppedCount)) == 12 &&
                   OffsetOf<InventoryRespawnPenaltyResultSignal>(nameof(InventoryRespawnPenaltyResultSignal.Flags)) == 16;
        }

        private static bool ValidateInventoryRespawnDeathAupSignalLayout()
        {
            return UnsafeUtility.SizeOf<InventoryRespawnDeathAupSignal>() == ShinobuRespawnConstants.InventoryRespawnDeathAupSignalSizeBytes &&
                   OffsetOf<InventoryRespawnDeathAupSignal>(nameof(InventoryRespawnDeathAupSignal.DeathAUP)) == 0 &&
                   OffsetOf<InventoryRespawnDeathAupSignal>(nameof(InventoryRespawnDeathAupSignal.InventoryHash)) == 24 &&
                   OffsetOf<InventoryRespawnDeathAupSignal>(nameof(InventoryRespawnDeathAupSignal.Frame)) == 28 &&
                   OffsetOf<InventoryRespawnDeathAupSignal>(nameof(InventoryRespawnDeathAupSignal.Sequence)) == 32 &&
                   OffsetOf<InventoryRespawnDeathAupSignal>(nameof(InventoryRespawnDeathAupSignal.Flags)) == 36 &&
                   OffsetOf<InventoryRespawnDeathAupSignal>(nameof(InventoryRespawnDeathAupSignal.SourceHash)) == 40;
        }

        private static bool ValidateInventoryDeathLootCacheSignalLayout()
        {
            return UnsafeUtility.SizeOf<InventoryDeathLootCacheSignal>() == ShinobuRespawnConstants.InventoryDeathLootCacheSignalSizeBytes &&
                   OffsetOf<InventoryDeathLootCacheSignal>(nameof(InventoryDeathLootCacheSignal.PositionAup)) == 0 &&
                   OffsetOf<InventoryDeathLootCacheSignal>(nameof(InventoryDeathLootCacheSignal.GeneticsMask)) == 48 &&
                   OffsetOf<InventoryDeathLootCacheSignal>(nameof(InventoryDeathLootCacheSignal.InventoryHash)) == 56 &&
                   OffsetOf<InventoryDeathLootCacheSignal>(nameof(InventoryDeathLootCacheSignal.ItemHash)) == 60 &&
                   OffsetOf<InventoryDeathLootCacheSignal>(nameof(InventoryDeathLootCacheSignal.Sequence)) == 64 &&
                   OffsetOf<InventoryDeathLootCacheSignal>(nameof(InventoryDeathLootCacheSignal.Frame)) == 68 &&
                   OffsetOf<InventoryDeathLootCacheSignal>(nameof(InventoryDeathLootCacheSignal.Quantity)) == 72 &&
                   OffsetOf<InventoryDeathLootCacheSignal>(nameof(InventoryDeathLootCacheSignal.QualityMilli)) == 74 &&
                   OffsetOf<InventoryDeathLootCacheSignal>(nameof(InventoryDeathLootCacheSignal.Flags)) == 76 &&
                   OffsetOf<InventoryDeathLootCacheSignal>(nameof(InventoryDeathLootCacheSignal.StateFlags)) == 80;
        }

        private static bool ValidatePlayerRespawnSignalLayout()
        {
            return UnsafeUtility.SizeOf<PlayerRespawnSignal>() == ShinobuRespawnConstants.PlayerRespawnSignalSizeBytes &&
                   OffsetOf<PlayerRespawnSignal>(nameof(PlayerRespawnSignal.DeathAUP)) == 0 &&
                   OffsetOf<PlayerRespawnSignal>(nameof(PlayerRespawnSignal.RespawnAUP)) == 24 &&
                   OffsetOf<PlayerRespawnSignal>(nameof(PlayerRespawnSignal.PlayerHash)) == 48 &&
                   OffsetOf<PlayerRespawnSignal>(nameof(PlayerRespawnSignal.MedicalBayHashID)) == 52 &&
                   OffsetOf<PlayerRespawnSignal>(nameof(PlayerRespawnSignal.DamageHash)) == 56 &&
                   OffsetOf<PlayerRespawnSignal>(nameof(PlayerRespawnSignal.Frame)) == 60 &&
                   OffsetOf<PlayerRespawnSignal>(nameof(PlayerRespawnSignal.Sequence)) == 64 &&
                   OffsetOf<PlayerRespawnSignal>(nameof(PlayerRespawnSignal.Flags)) == 68 &&
                   OffsetOf<PlayerRespawnSignal>(nameof(PlayerRespawnSignal.Phase)) == 72 &&
                   OffsetOf<PlayerRespawnSignal>(nameof(PlayerRespawnSignal.SuspendCollisionFrames)) == 73 &&
                   OffsetOf<PlayerRespawnSignal>(nameof(PlayerRespawnSignal.Reserved0)) == 74 &&
                   OffsetOf<PlayerRespawnSignal>(nameof(PlayerRespawnSignal.Reserved1)) == 76 &&
                   OffsetOf<PlayerRespawnSignal>(nameof(PlayerRespawnSignal.Reserved2)) == 80 &&
                   OffsetOf<PlayerRespawnSignal>(nameof(PlayerRespawnSignal.Reserved3)) == 88 &&
                   OffsetOf<PlayerRespawnSignal>(nameof(PlayerRespawnSignal.Reserved4)) == 96 &&
                   OffsetOf<PlayerRespawnSignal>(nameof(PlayerRespawnSignal.Reserved5)) == 104 &&
                   OffsetOf<PlayerRespawnSignal>(nameof(PlayerRespawnSignal.Reserved6)) == 112 &&
                   OffsetOf<PlayerRespawnSignal>(nameof(PlayerRespawnSignal.Reserved7)) == 120;
        }

        private static bool ValidatePlayerRespawnSignalFlags()
        {
            const uint expectedMask = PlayerRespawnSignalFlags.Requested |
                                      PlayerRespawnSignalFlags.Committed |
                                      PlayerRespawnSignalFlags.SuspendCollision |
                                      PlayerRespawnSignalFlags.MockMedicalBay |
                                      PlayerRespawnSignalFlags.FallbackLifepod |
                                      PlayerRespawnSignalFlags.InvalidTargetAup |
                                      PlayerRespawnSignalFlags.PenaltyApplied |
                                      PlayerRespawnSignalFlags.InvalidDeathAup;

            return PlayerRespawnSignalFlags.Requested == (1u << 0) &&
                   PlayerRespawnSignalFlags.Committed == (1u << 1) &&
                   PlayerRespawnSignalFlags.SuspendCollision == (1u << 2) &&
                   PlayerRespawnSignalFlags.MockMedicalBay == (1u << 3) &&
                   PlayerRespawnSignalFlags.FallbackLifepod == (1u << 4) &&
                   PlayerRespawnSignalFlags.InvalidTargetAup == (1u << 5) &&
                   PlayerRespawnSignalFlags.PenaltyApplied == (1u << 6) &&
                   PlayerRespawnSignalFlags.InvalidDeathAup == (1u << 7) &&
                   expectedMask == 0xFFu;
        }

        private static bool ValidatePlayerRespawnSignalPhase()
        {
            return PlayerRespawnSignalPhase.Request == 1 &&
                   PlayerRespawnSignalPhase.Committed == 2;
        }

        private static bool ValidateTelemetryEntryLayout()
        {
            return UnsafeUtility.SizeOf<RespawnTelemetryEntry>() == ShinobuRespawnConstants.RespawnTelemetryEntrySizeBytes &&
                   OffsetOf<RespawnTelemetryEntry>(nameof(RespawnTelemetryEntry.DeathAUP)) == 0 &&
                   OffsetOf<RespawnTelemetryEntry>(nameof(RespawnTelemetryEntry.RespawnAUP)) == 24 &&
                   OffsetOf<RespawnTelemetryEntry>(nameof(RespawnTelemetryEntry.CauseHash)) == 48 &&
                   OffsetOf<RespawnTelemetryEntry>(nameof(RespawnTelemetryEntry.Frame)) == 52 &&
                   OffsetOf<RespawnTelemetryEntry>(nameof(RespawnTelemetryEntry.ReconcileMicroseconds)) == 56 &&
                   OffsetOf<RespawnTelemetryEntry>(nameof(RespawnTelemetryEntry.Flags)) == 60;
        }

        private static bool ValidateTelemetryCursorLayout()
        {
            return UnsafeUtility.SizeOf<RespawnTelemetryCursor64>() == ShinobuRespawnConstants.RespawnTelemetryCursorSizeBytes &&
                   OffsetOf<RespawnTelemetryCursor64>(nameof(RespawnTelemetryCursor64.Cursor)) == 0 &&
                   OffsetOf<RespawnTelemetryCursor64>(nameof(RespawnTelemetryCursor64.Flags)) == 4 &&
                   OffsetOf<RespawnTelemetryCursor64>(nameof(RespawnTelemetryCursor64._pad0)) == 8 &&
                   OffsetOf<RespawnTelemetryCursor64>(nameof(RespawnTelemetryCursor64._pad1)) == 16 &&
                   OffsetOf<RespawnTelemetryCursor64>(nameof(RespawnTelemetryCursor64._pad2)) == 24 &&
                   OffsetOf<RespawnTelemetryCursor64>(nameof(RespawnTelemetryCursor64._pad3)) == 32 &&
                   OffsetOf<RespawnTelemetryCursor64>(nameof(RespawnTelemetryCursor64._pad4)) == 40 &&
                   OffsetOf<RespawnTelemetryCursor64>(nameof(RespawnTelemetryCursor64._pad5)) == 48 &&
                   OffsetOf<RespawnTelemetryCursor64>(nameof(RespawnTelemetryCursor64._pad6)) == 56;
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            var field = typeof(T).GetField(fieldName);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }
    }
}
