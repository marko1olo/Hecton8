using Unity.Mathematics;

namespace Hecton8.Core.Contracts.Fluids
{
    /// <summary>
    /// Shared brine-layer constants. Kept contract-side so physics, player, AI, audio, and rendering agree without concrete dependencies.
    /// </summary>
    public static class BrineLayerConstants
    {
        public const float CartographySectorSizeMeters = 50f;
        public const float DensityMultiplier = 3f;
        public const float SwimSpeedMultiplier = 0.6f;
        public const float CarbonDioxideEquivalentKPa = 10f;
        public const float DefaultToxicity01 = 0.92f;
        public const float DefaultBrineFogHardClip = 0f;
        public const byte SampleValidFlag = 1;
        public const byte SubmergedFlag = 1 << 1;
        public const byte EnteredFlag = 1 << 2;
        public const byte ExitedFlag = 1 << 3;
        public const byte FluidKindBrine = 2;
        public const byte AcousticThickFluidChannel = 7;

        public static readonly float4 DefaultBrineColor = new float4(0.66f, 0.77f, 0.21f, 0.92f);
    }
}
