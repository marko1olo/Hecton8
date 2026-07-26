using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for MarchingCubesLookupTable.
    /// Extracted from HectonVoxelEngine.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class MarchingCubesLookupTable
    {
        public static readonly int[] EdgeTable = new int[256]
        {
            0x000,0x109,0x203,0x30A,0x406,0x50F,0x605,0x70C,
            0x80C,0x905,0xA0F,0xB06,0xC0A,0xD03,0xE09,0xF00,
            0x190,0x099,0x393,0x29A,0x596,0x49F,0x795,0x69C,
            0x99C,0x895,0xB9F,0xA96,0xD9A,0xC93,0xF99,0xE90,
            0x230,0x339,0x033,0x13A,0x636,0x73F,0x435,0x53C,
            0xA3C,0xB35,0x83F,0x936,0xE3A,0xF33,0xC39,0xD30,
            0x3A0,0x2A9,0x1A3,0x0AA,0x7A6,0x6AF,0x5A5,0x4AC,
            0xBAC,0xAA5,0x9AF,0x8A6,0xFAA,0xEA3,0xDA9,0xCA0,
            0x460,0x569,0x663,0x76A,0x066,0x16F,0x265,0x36C,
            0xC6C,0xD65,0xE6F,0xF66,0x86A,0x963,0xA69,0xB60,
            0x5F0,0x4F9,0x7F3,0x6FA,0x1F6,0x0FF,0x3F5,0x2FC,
            0xDFC,0xCF5,0xFFF,0xEF6,0x9FA,0x8F3,0xBF9,0xAF0,
            0x650,0x759,0x453,0x55A,0x256,0x35F,0x055,0x15C,
            0xE5C,0xF55,0xC5F,0xD56,0xA5A,0xB53,0x859,0x950,
            0x7C0,0x6C9,0x5C3,0x4CA,0x3C6,0x2CF,0x1C5,0x0CC,
            0xFCC,0xEC5,0xDCF,0xCC6,0xBCA,0xAC3,0x9C9,0x8C0,
            0x8C0,0x9C9,0xAC3,0xBCA,0xCC6,0xDCF,0xEC5,0xFCC,
            0x0CC,0x1C5,0x2CF,0x3C6,0x4CA,0x5C3,0x6C9,0x7C0,
            0x950,0x859,0xB53,0xA5A,0xD56,0xC5F,0xF55,0xE5C,
            0x15C,0x055,0x35F,0x256,0x55A,0x453,0x759,0x650,
            0xAF0,0xBF9,0x8F3,0x9FA,0xEF6,0xFFF,0xCF5,0xDFC,
            0x2FC,0x3F5,0x0FF,0x1F6,0x6FA,0x7F3,0x4F9,0x5F0,
            0xB60,0xA69,0x963,0x86A,0xF66,0xE6F,0xD65,0xC6C,
            0x36C,0x265,0x16F,0x066,0x76A,0x663,0x569,0x460,
            0xCA0,0xDA9,0xEA3,0xFAA,0x8A6,0x9AF,0xAA5,0xBAC,
            0x4AC,0x5A5,0x6AF,0x7A6,0x0AA,0x1A3,0x2A9,0x3A0,
            0xD30,0xC39,0xF33,0xE3A,0x936,0x83F,0xB35,0xA3C,
            0x53C,0x435,0x73F,0x636,0x13A,0x033,0x339,0x230,
            0xE90,0xF99,0xC93,0xD9A,0xA96,0xB9F,0x895,0x99C,
            0x69C,0x795,0x49F,0x596,0x29A,0x393,0x099,0x190,
            0xF00,0xE09,0xD03,0xC0A,0xB06,0xA0F,0x905,0x80C,
            0x70C,0x605,0x50F,0x406,0x30A,0x203,0x109,0x000
        };

        /// <summary>
        /// Burst-safe edge-flag lookup. Never throws and never allocates, so it is legal
        /// inside a <c>[BurstCompile]</c> job — unlike the <c>Calculate</c> overloads below,
        /// which throw on invalid input and therefore abort the process when a throw is
        /// reached from Burst-compiled code in a player build.
        /// </summary>
        /// <param name="caseMask">Corner-sign mask; bit i is set when corner i is inside.</param>
        /// <param name="d0">Corner density 0.</param>
        /// <param name="d1">Corner density 1.</param>
        /// <param name="d2">Corner density 2.</param>
        /// <param name="d3">Corner density 3.</param>
        /// <param name="d4">Corner density 4.</param>
        /// <param name="d5">Corner density 5.</param>
        /// <param name="d6">Corner density 6.</param>
        /// <param name="d7">Corner density 7.</param>
        /// <param name="isoLevel">Iso level; must be finite.</param>
        /// <param name="edgeFlags">
        /// 12-bit mask of the cube edges the surface crosses. Set to <c>0</c> when any input
        /// is non-finite, which makes the caller emit no triangles for that cell.
        /// </param>
        /// <returns><c>true</c> when every input was finite and <paramref name="edgeFlags"/> is usable.</returns>
        public static bool TryCalculate(
            byte caseMask,
            float d0, float d1, float d2, float d3,
            float d4, float d5, float d6, float d7,
            float isoLevel,
            out int edgeFlags)
        {
            if (!float.IsFinite(isoLevel) ||
                !float.IsFinite(d0) || !float.IsFinite(d1) ||
                !float.IsFinite(d2) || !float.IsFinite(d3) ||
                !float.IsFinite(d4) || !float.IsFinite(d5) ||
                !float.IsFinite(d6) || !float.IsFinite(d7))
            {
                edgeFlags = 0;
                return false;
            }

            edgeFlags = EdgeTable[caseMask];
            return true;
        }

        /// <summary>
        /// Returns the 12-bit edge-crossing mask for <paramref name="caseMask"/>.
        /// </summary>
        /// <remarks>
        /// This returns edge flags only. It does not interpolate vertex positions — the
        /// caller owns that step, using the densities and iso level it already holds.
        /// The density and iso-level arguments here serve the finiteness contract, which is
        /// why they are validated but not otherwise read.
        /// Throws on invalid input; use <see cref="TryCalculate"/> from Burst-compiled code.
        /// </remarks>
        /// <param name='caseMask'>Corner-sign mask; bit i is set when corner i is inside.</param>
        /// <param name='cornerDensities'>The 8 corner densities; must be non-null, length >= 8, all finite.</param>
        /// <param name='isoLevel'>Iso level; must be finite.</param>
        /// <returns>Returns the 12-bit edge-crossing mask of type int.</returns>
        public static int Calculate(byte caseMask, float[] cornerDensities, float isoLevel)
        {
            if (cornerDensities == null)
            {
                throw new ArgumentNullException(nameof(cornerDensities));
            }
            if (cornerDensities.Length < 8)
            {
                throw new ArgumentException("cornerDensities must contain at least 8 elements.", nameof(cornerDensities));
            }
            if (float.IsNaN(isoLevel) || float.IsInfinity(isoLevel))
            {
                throw new ArgumentException("isoLevel must be a finite number.", nameof(isoLevel));
            }
            for (int i = 0; i < 8; i++)
            {
                 if (float.IsNaN(cornerDensities[i]) || float.IsInfinity(cornerDensities[i]))
                 {
                     throw new ArgumentException("cornerDensities must be finite numbers.", nameof(cornerDensities));
                 }
            }
            return EdgeTable[caseMask];
        }

        /// <summary>
        /// Returns the 12-bit edge-crossing mask for <paramref name="caseMask"/>.
        /// Throws on non-finite input; use <see cref="TryCalculate"/> from Burst-compiled code.
        /// </summary>
        public static int Calculate(byte caseMask, float d0, float d1, float d2, float d3, float d4, float d5, float d6, float d7, float isoLevel)
        {
            if (float.IsNaN(isoLevel) || float.IsInfinity(isoLevel) ||
                float.IsNaN(d0) || float.IsInfinity(d0) ||
                float.IsNaN(d1) || float.IsInfinity(d1) ||
                float.IsNaN(d2) || float.IsInfinity(d2) ||
                float.IsNaN(d3) || float.IsInfinity(d3) ||
                float.IsNaN(d4) || float.IsInfinity(d4) ||
                float.IsNaN(d5) || float.IsInfinity(d5) ||
                float.IsNaN(d6) || float.IsInfinity(d6) ||
                float.IsNaN(d7) || float.IsInfinity(d7))
            {
                throw new ArgumentException("Inputs must be finite numbers.");
            }
            return EdgeTable[caseMask];
        }
    }
}
