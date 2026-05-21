using System.Runtime.InteropServices;

namespace Hecton8.Core.Contracts.Physiology
{
    /// <summary>
    /// Shared physiology state contract consumed by Core/KCC without referencing the Physiology runtime assembly.
    /// </summary>
    public static class ShinobuMetabolismVaultContract
    {
        public const int MetabolismStatesBufferId = 70238;
        public const int MetabolicStateSizeBytes = 32;

        public const uint FlagStarving = 1u << 0;
        public const uint FlagDehydrated = 1u << 1;
        public const uint FlagHypothermia = 1u << 2;
        public const uint FlagToxic = 1u << 3;
        public const uint FlagInvalidMath = 1u << 4;
        public const uint FlagMockEntity = 1u << 5;
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
        [FieldOffset(24)] public uint _pad0;
        [FieldOffset(28)] public uint _pad1;
    }
}
