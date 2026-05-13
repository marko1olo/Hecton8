// ============================================================================
// HECTON-8 - SurvivalStatusMasks.cs
// Fixed bit layout for player physiology conditions.
// ============================================================================

namespace Hecton8.Gameplay
{
    public static class SurvivalStatusMasks
    {
        public const uint Bends = 1u << 0;
        public const uint Freezing = 1u << 1;
        public const uint Starving = 1u << 2;
        public const uint Dehydrated = 1u << 3;
        public const uint Narcosis = 1u << 4;
        public const uint Toxicity = 1u << 5;
        public const uint CrushWarning = 1u << 6;
        public const uint RadiationPenalty = 1u << 7;
    }
}
