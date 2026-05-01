using Unity.Burst;
using Unity.Mathematics;

namespace Hecton8.World
{
    /// <summary>
    /// Burst-compiled deterministic mineral yield math invoked by runtime resource nodes.
    /// </summary>
    internal static class ResourceYieldMath
    {
        internal const int GramsPerKilogram = 1000;
        internal const float KilogramsPerGram = 0.001f;

        public delegate float EvaluateYieldUnitsDelegate(
            float toolPower,
            float nodeHardness,
            float elapsedSeconds,
            float unitItemMassKg);

        private static readonly FunctionPointer<EvaluateYieldUnitsDelegate> _evaluateYieldUnits =
            BurstCompiler.CompileFunctionPointer<EvaluateYieldUnitsDelegate>(EvaluateYieldUnitsBurst);

        public delegate float EvaluateExtractedMassKgDelegate(
            float toolPower,
            float nodeHardness,
            float elapsedSeconds);

        private static readonly FunctionPointer<EvaluateExtractedMassKgDelegate> _evaluateExtractedMassKg =
            BurstCompiler.CompileFunctionPointer<EvaluateExtractedMassKgDelegate>(EvaluateExtractedMassKgBurst);

        public static float EvaluateYieldUnits(
            float toolPower,
            float nodeHardness,
            float elapsedSeconds,
            float unitItemMassKg)
        {
            return _evaluateYieldUnits.Invoke(toolPower, nodeHardness, elapsedSeconds, unitItemMassKg);
        }

        public static float EvaluateExtractedMassKg(
            float toolPower,
            float nodeHardness,
            float elapsedSeconds)
        {
            return _evaluateExtractedMassKg.Invoke(toolPower, nodeHardness, elapsedSeconds);
        }

        internal static int KilogramsToWholeGrams(float kilograms)
        {
            if (!math.isfinite(kilograms) || kilograms <= 0f)
                return 0;

            float clampedGrams = math.min(int.MaxValue, math.round(kilograms * GramsPerKilogram));
            return (int)clampedGrams;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float EvaluateYieldUnitsBurst(
            float toolPower,
            float nodeHardness,
            float elapsedSeconds,
            float unitItemMassKg)
        {
            float extractedMassKg = EvaluateExtractedMassKgBurst(toolPower, nodeHardness, elapsedSeconds);
            return extractedMassKg / math.max(0.01f, unitItemMassKg);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float EvaluateExtractedMassKgBurst(
            float toolPower,
            float nodeHardness,
            float elapsedSeconds)
        {
            return math.max(0f, toolPower) * math.max(0.01f, nodeHardness) * math.max(0f, elapsedSeconds);
        }
    }
}
