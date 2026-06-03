using System.Runtime.InteropServices;

namespace Hecton8.Core.Contracts.Physiology
{
    /// <summary>
    /// Shared physiology state contract consumed by Core/KCC without referencing the Physiology runtime assembly.
    /// </summary>
    public static class ShinobuMetabolismVaultContract
    {
        public const int MetabolismStatesBufferId = 70238;
        public const int MetabolicStateSizeBytes = 48;
        public const float HypoxiaAgonyDurationSeconds = 4f;
        public const ulong MetabolismStateMutationGuardMask = 1UL << 48;

        public const uint FlagStarving = 1u << 0;
        public const uint FlagDehydrated = 1u << 1;
        public const uint FlagHypothermia = 1u << 2;
        public const uint FlagToxic = 1u << 3;
        public const uint FlagInvalidMath = 1u << 4;
        public const uint FlagMockEntity = 1u << 5;
        public const uint FlagFatigue = 1u << 9;
        public const uint FlagHypoxia = 1u << 10;
    }

    [StructLayout(LayoutKind.Explicit, Size = ShinobuMetabolismVaultContract.MetabolicStateSizeBytes)]
    public struct MetabolicStateDTO
    {
        [FieldOffset(0)] public float Calories;
        [FieldOffset(4)] public float Hydration;
        [FieldOffset(8)] public float CoreTemperature;
        [FieldOffset(12)] public float Toxicity;
        [FieldOffset(16)] public uint EntityHashID;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public float Fatigue01;
        [FieldOffset(28)] public float RealO2;
        [FieldOffset(32)] public float AgonyTimeRemaining;
        [FieldOffset(36)] public byte IsInHypoxia;
        [FieldOffset(37)] private byte _pad0;
        [FieldOffset(38)] private ushort _pad1;
        [FieldOffset(40)] private uint _pad2;
        [FieldOffset(44)] private uint _pad3;
    }

    public static class ShinobuSuitIntegrityVaultContract
    {
        public const int StateBufferId = 72510;
        public const int SuitIntegrityStateSizeBytes = 32;
    }

    [StructLayout(LayoutKind.Explicit, Size = ShinobuSuitIntegrityVaultContract.SuitIntegrityStateSizeBytes)]
    public struct SuitIntegrityDTO
    {
        [FieldOffset(0)] public float CurrentIntegrity01;
        [FieldOffset(4)] public float AppliedPressureATM;
        [FieldOffset(8)] public float MicroFractureAccumulation;
        [FieldOffset(12)] public uint EquippedSuitHash;
        [FieldOffset(16)] public uint IntegrityFlags;
        [FieldOffset(20)] private uint _pad0;
        [FieldOffset(24)] private uint _pad1;
        [FieldOffset(28)] private uint _pad2;
    }
}
