#nullable disable
using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for EquipmentHydrodynamicDragCalculator.
    /// Extracted from HectonPlayerMovement.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class EquipmentHydrodynamicDragCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="activeEquipmentMask">Parameter representing the activeEquipmentMask (ulong).</param>
        /// <param name="baseDragTable">Parameter representing the baseDragTable (float[]).</param>
        /// <returns>Returns Cumulative drag scalar of type float.</returns>
        public static float Compute(ulong activeEquipmentMask, float[] baseDragTable)
        {
            float totalDrag = 1.0f; // Base drag is 1.0

            if (baseDragTable == null || baseDragTable.Length == 0 || activeEquipmentMask == 0)
            {
                return totalDrag;
            }

            int count = Math.Min(baseDragTable.Length, 64);
            for (int i = 0; i < count; i++)
            {
                if ((activeEquipmentMask & (1UL << i)) != 0)
                {
                    float dragVal = baseDragTable[i];
                    if (!float.IsNaN(dragVal) && !float.IsInfinity(dragVal) && dragVal > 0f)
                    {
                        totalDrag += dragVal;
                    }
                }
            }

            if (totalDrag < 0f)
            {
                totalDrag = 0f;
            }

            if (float.IsInfinity(totalDrag) || float.IsNaN(totalDrag))
            {
                return 1.0f;
            }

            return totalDrag;
        }
    }
}
