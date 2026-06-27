using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for FabricationRecipeYieldRoll.
    /// Extracted from FabricationAssemblerRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class FabricationRecipeYieldRoll
    {
        [StructLayout(LayoutKind.Explicit)]
        private struct FloatIntUnion
        {
            [FieldOffset(0)] public float f;
            [FieldOffset(0)] public uint i;
        }

        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="playerSkill01">Parameter representing the playerSkill01 (float).</param>
        /// <param name="baseYield">Parameter representing the baseYield (float).</param>
        /// <param name="maxBonusYield">Parameter representing the maxBonusYield (float).</param>
        /// <param name="randomSeed">Parameter representing the randomSeed (float).</param>
        /// <returns>Returns actualYield of type float.</returns>
        public static float Calculate(float playerSkill01, float baseYield, float maxBonusYield, float randomSeed)
        {
            if (float.IsNaN(playerSkill01) || float.IsNaN(baseYield) || float.IsNaN(maxBonusYield) || float.IsNaN(randomSeed) ||
                float.IsInfinity(playerSkill01) || float.IsInfinity(baseYield) || float.IsInfinity(maxBonusYield) || float.IsInfinity(randomSeed))
            {
                return 0f;
            }

            float skill = Math.Clamp(playerSkill01, 0f, 1f);
            float bYield = Math.Max(0f, baseYield);
            float mBonusYield = Math.Max(0f, maxBonusYield);

            FloatIntUnion u = new FloatIntUnion { f = randomSeed };
            uint seed = u.i;

            unchecked
            {
                // Hash seed
                seed = (seed ^ 61) ^ (seed >> 16);
                seed *= 9;
                seed = seed ^ (seed >> 4);
                seed *= 0x27d4eb2d;
                seed = seed ^ (seed >> 15);
            }

            float randomVal = (seed & 0xFFFFFF) / (float)0xFFFFFF;

            return bYield + mBonusYield * skill * randomVal;
        }
    }
}
