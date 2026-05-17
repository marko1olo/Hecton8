// AUTO-GENERATED. DO NOT EDIT.
// Source: Tools/QuestCompiler.py --graph Data/Narrative/First_Hour_Quests.json
namespace Hecton8.Core.Generated
{
    public static class H8QuestMasks
    {
        public const int NodeCount = 4;
        public const int MaxNodes = 32;
        public const int BitsPerQuest = 2;
        public const ulong StateBitsMask = 0x3UL;
        public const ulong InactiveState = 0x0UL;
        public const ulong ActiveState = 0x1UL;
        public const ulong DoneState = 0x2UL;
        public const uint GraphHash32 = 0x83B9BEE8u;
        public const uint BinaryMagic = 0x47513848u;
        public const int BinaryVersion = 1;
        public const int BinaryAlignmentBytes = 16;
        public const int BinaryHeaderBytes = 64;
        public const int BinaryNodeRecordBytes = 32;
        public const int BinaryTriggerRecordBytes = 16;
        public const int BinaryEdgeRecordBytes = 16;
        public const int BinaryScalabilityTierRecordBytes = 48;
        public const int BinaryScalabilityTierOffset = 304;
        public const int BinaryScalabilityTierCount = 4;
        public const int BinaryBlobBytes = 496;
        public const uint TierFlagHashOnly = 1u;
        public const uint TierFlagLorePayload = 2u;
        public const uint TierFlagTriggerPayload = 4u;
        public const uint TierFlagVfxPayload = 8u;
        public const uint TierFlagHighResGradient = 16u;
        public const uint TierFlagComplexHarmonicNoise = 32u;
        public const ulong AllQuestStateMask = 0x00000000000000FFUL;
        public const ulong AllDoneMask = 0x00000000000000AAUL;

        public static class LowTier
        {
            public const int Index = 0;
            public const uint TierHash32 = 0x4F516A81u;
            public const uint TargetHash32 = 0x0C2C8AEAu;
            public const uint MarkerPayloadHash32 = 0x1D9B854Cu;
            public const uint ScannerVisualProfileHash32 = 0x00000000u;
            public const uint RadioNoiseProfileHash32 = 0x00000000u;
            public const uint ScannerGradientProfileHash32 = 0x00000000u;
            public const uint RadioHarmonicProfileHash32 = 0x00000000u;
            public const uint PayloadFlags = 1u;
            public const int StateWordCount = 1;
            public const int MaxEvaluatedNodesPerSignal = 4;
            public const int HarmonicBands = 0;
        }

        public static class MiddleTier
        {
            public const int Index = 1;
            public const uint TierHash32 = 0xC982A718u;
            public const uint TargetHash32 = 0x59EB9DA3u;
            public const uint MarkerPayloadHash32 = 0xE778DFEBu;
            public const uint ScannerVisualProfileHash32 = 0x00000000u;
            public const uint RadioNoiseProfileHash32 = 0x00000000u;
            public const uint ScannerGradientProfileHash32 = 0x00000000u;
            public const uint RadioHarmonicProfileHash32 = 0x00000000u;
            public const uint PayloadFlags = 2u;
            public const int StateWordCount = 1;
            public const int MaxEvaluatedNodesPerSignal = 8;
            public const int HarmonicBands = 0;
        }

        public static class HighTier
        {
            public const int Index = 2;
            public const uint TierHash32 = 0x3DDDB9FDu;
            public const uint TargetHash32 = 0x5567DBDCu;
            public const uint MarkerPayloadHash32 = 0x2C48FF07u;
            public const uint ScannerVisualProfileHash32 = 0xE0C19AB9u;
            public const uint RadioNoiseProfileHash32 = 0x132C57E9u;
            public const uint ScannerGradientProfileHash32 = 0x6FE4A44Bu;
            public const uint RadioHarmonicProfileHash32 = 0x3179ECF2u;
            public const uint PayloadFlags = 62u;
            public const int StateWordCount = 1;
            public const int MaxEvaluatedNodesPerSignal = 16;
            public const int HarmonicBands = 4;
        }

        public static class UltraTier
        {
            public const int Index = 3;
            public const uint TierHash32 = 0x18B071F5u;
            public const uint TargetHash32 = 0x1EB9666Au;
            public const uint MarkerPayloadHash32 = 0x727977CCu;
            public const uint ScannerVisualProfileHash32 = 0xE0C19AB9u;
            public const uint RadioNoiseProfileHash32 = 0x132C57E9u;
            public const uint ScannerGradientProfileHash32 = 0x6FE4A44Bu;
            public const uint RadioHarmonicProfileHash32 = 0x3179ECF2u;
            public const uint PayloadFlags = 62u;
            public const int StateWordCount = 1;
            public const int MaxEvaluatedNodesPerSignal = 32;
            public const int HarmonicBands = 8;
        }

        public static class QuestFirstHourWakeUp
        {
            public const int Slot = 0;
            public const int Shift = 0;
            public const uint NodeHash32 = 0x315FA14Cu;
            public const uint LoreHash32 = 0xAEC57EACu;
            public const int PrerequisiteCount = 0;
            public const int TriggerCount = 1;
            public const ulong StateMask = 0x0000000000000003UL;
            public const ulong ActiveMask = 0x0000000000000001UL;
            public const ulong DoneMask = 0x0000000000000002UL;
            public const ulong PrerequisiteDoneMask = 0x0000000000000000UL;
            public const uint Trigger0Hash32 = 0xE6EB9B11u;
            public const uint Trigger0TypeHash32 = 0xA7EA84A8u;
        }

        public static class QuestFirstHourFindScanner
        {
            public const int Slot = 1;
            public const int Shift = 2;
            public const uint NodeHash32 = 0x5C2D7896u;
            public const uint LoreHash32 = 0xAEC57EACu;
            public const int PrerequisiteCount = 1;
            public const int TriggerCount = 1;
            public const ulong StateMask = 0x000000000000000CUL;
            public const ulong ActiveMask = 0x0000000000000004UL;
            public const ulong DoneMask = 0x0000000000000008UL;
            public const ulong PrerequisiteDoneMask = 0x0000000000000002UL;
            public const uint Trigger0Hash32 = 0x28A840B4u;
            public const uint Trigger0TypeHash32 = 0x1D88D039u;
        }

        public static class QuestFirstHourScanLeviathanTrace
        {
            public const int Slot = 2;
            public const int Shift = 4;
            public const uint NodeHash32 = 0x29384196u;
            public const uint LoreHash32 = 0xBC52DB39u;
            public const int PrerequisiteCount = 1;
            public const int TriggerCount = 1;
            public const ulong StateMask = 0x0000000000000030UL;
            public const ulong ActiveMask = 0x0000000000000010UL;
            public const ulong DoneMask = 0x0000000000000020UL;
            public const ulong PrerequisiteDoneMask = 0x0000000000000008UL;
            public const uint Trigger0Hash32 = 0x2FCC4837u;
            public const uint Trigger0TypeHash32 = 0xC57C3DF1u;
        }

        public static class QuestFirstHourFixRadio
        {
            public const int Slot = 3;
            public const int Shift = 6;
            public const uint NodeHash32 = 0xA48A5B3Bu;
            public const uint LoreHash32 = 0xBC52DB39u;
            public const int PrerequisiteCount = 1;
            public const int TriggerCount = 1;
            public const ulong StateMask = 0x00000000000000C0UL;
            public const ulong ActiveMask = 0x0000000000000040UL;
            public const ulong DoneMask = 0x0000000000000080UL;
            public const ulong PrerequisiteDoneMask = 0x0000000000000020UL;
            public const uint Trigger0Hash32 = 0x485BB212u;
            public const uint Trigger0TypeHash32 = 0xFCCB8134u;
        }

    }
}
