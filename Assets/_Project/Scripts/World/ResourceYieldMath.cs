using Unity.Burst;
using Unity.Mathematics;

namespace Hecton8.World
{
    /// <summary>
    /// Deterministic mineral yield math invoked by runtime resource nodes.
    ///
    /// L19j root cause: static field initializers called
    /// BurstCompiler.CompileFunctionPointer without [MonoPInvokeCallback] and without
    /// a lazy/fallback gate. Under -batchmode/-nographics the first TakeDamage path
    /// requested Burst JIT compilation of Evaluate*Burst and hard-crashed the editor
    /// (Crash!!! stack: ResourceNode.TakeDamage → TryEmitIncrementalYield → Invoke).
    ///
    /// The formula is a single multiply/rcp; FunctionPointer overhead is unjustified.
    /// Keep the math in plain Unity.Mathematics so headless probes never touch Burst JIT
    /// on the damage pulse hot path. Burst-compiled mirrors remain available for jobs
    /// that already run inside the Burst compiler domain.
    /// </summary>
    internal static class ResourceYieldMath
    {
        internal const int GramsPerKilogram = 1000;
        internal const float KilogramsPerGram = 0.001f;

        public static float EvaluateYieldUnits(
            float toolPower,
            float nodeHardness,
            float elapsedSeconds,
            float unitItemMassKg)
        {
            float extractedMassKg = EvaluateExtractedMassKg(toolPower, nodeHardness, elapsedSeconds);
            return extractedMassKg / math.max(0.01f, unitItemMassKg);
        }

        public static float EvaluateExtractedMassKg(
            float toolPower,
            float nodeHardness,
            float elapsedSeconds)
        {
            return EvaluateExtractedMassKgCore(toolPower, nodeHardness, elapsedSeconds);
        }

        internal static int KilogramsToWholeGrams(float kilograms)
        {
            if (!math.isfinite(kilograms) || kilograms <= 0f)
                return 0;

            float clampedGrams = math.min(int.MaxValue, math.round(kilograms * GramsPerKilogram));
            return (int)clampedGrams;
        }

        /// <summary>
        /// Burst-callable core shared with any future job that already owns a Burst context.
        /// Do not CompileFunctionPointer this from managed static initializers.
        /// </summary>
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal static float EvaluateExtractedMassKgCore(
            float toolPower,
            float nodeHardness,
            float elapsedSeconds)
        {
            return math.max(0f, toolPower) * math.rcp(math.max(0.01f, nodeHardness)) * math.max(0f, elapsedSeconds);
        }
    }
}
