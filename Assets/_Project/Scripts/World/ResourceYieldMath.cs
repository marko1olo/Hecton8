using Unity.Burst;
using Unity.Mathematics;

namespace Hecton8.World
{
    /// <summary>
    /// Burst-compiled deterministic mineral yield math invoked by runtime resource nodes.
    /// </summary>
    internal static class ResourceYieldMath
    {
        public delegate float EvaluateYieldUnitsDelegate(
            float toolPower,
            float nodeHardness,
            float elapsedSeconds,
            float harvestDurationSeconds);

        private static readonly FunctionPointer<EvaluateYieldUnitsDelegate> _evaluateYieldUnits =
            BurstCompiler.CompileFunctionPointer<EvaluateYieldUnitsDelegate>(EvaluateYieldUnitsBurst);

        public static float EvaluateYieldUnits(
            float toolPower,
            float nodeHardness,
            float elapsedSeconds,
            float harvestDurationSeconds)
        {
            return _evaluateYieldUnits.Invoke(toolPower, nodeHardness, elapsedSeconds, harvestDurationSeconds);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float EvaluateYieldUnitsBurst(
            float toolPower,
            float nodeHardness,
            float elapsedSeconds,
            float harvestDurationSeconds)
        {
            float resolvedHardness = math.max(0.01f, nodeHardness);
            float resolvedDuration = math.max(0.05f, harvestDurationSeconds);
            float resolvedToolPower = math.max(0f, toolPower);
            float resolvedTime = math.max(0f, elapsedSeconds);
            return (resolvedToolPower * resolvedTime) / (resolvedHardness * resolvedDuration);
        }
    }
}
