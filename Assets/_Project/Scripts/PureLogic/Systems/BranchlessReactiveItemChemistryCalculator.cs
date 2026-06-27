using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for BranchlessReactiveItemChemistryCalculator.
    /// Extracted from PlayerInventory.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class BranchlessReactiveItemChemistryCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="itemAFlags">Parameter representing the itemAFlags (uint).</param>
        /// <param name="itemBFlags">Parameter representing the itemBFlags (uint).</param>
        /// <param name="reactionMatrix">Parameter representing the reactionMatrix (uint).</param>
        /// <returns>Returns Reaction code mask, 0 if inert of type uint.</returns>
        public static uint Compute(uint itemAFlags, uint itemBFlags, uint reactionMatrix)
        {
            uint aMasked = itemAFlags & reactionMatrix;
            uint bMasked = itemBFlags & reactionMatrix;

            // Branchlessly evaluate if x != 0.
            // (x | (~x + 1)) >> 31 gives 1 if x != 0, 0 if x == 0.
            uint aNZ = (aMasked | (~aMasked + 1)) >> 31;
            uint bNZ = (bMasked | (~bMasked + 1)) >> 31;

            uint aXorB = aMasked ^ bMasked;
            uint aMulti = aMasked & (aMasked - 1);

            uint diffOrMulti = aXorB | aMulti;
            uint diffOrMultiNZ = (diffOrMulti | (~diffOrMulti + 1)) >> 31;

            uint isReactive = aNZ & bNZ & diffOrMultiNZ;

            // Return combined flags if they react, otherwise 0
            return (aMasked | bMasked) * isReactive;
        }
    }
}
