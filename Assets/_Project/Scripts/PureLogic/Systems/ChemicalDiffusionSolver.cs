using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for ChemicalDiffusionSolver.
    /// Extracted from ChemicalInfluenceGrid.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class ChemicalDiffusionSolver
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="concentrationGrid">Parameter representing the concentrationGrid (float[,]).</param>
        /// <param name="diffusionRate">Parameter representing the diffusionRate (float).</param>
        /// <param name="deltaTime">Parameter representing the deltaTime (float).</param>
        /// <param name="epsilon">Safety parameter to prevent division by zero or NaN propagation.</param>
        /// <param name="laplacianNeighborCount">The number of orthogonal neighbors used in the Laplacian operator.</param>
        /// <param name="implicitSolverBaseDenominator">The base constant added to the denominator in the implicit Jacobi solver step.</param>
        /// <returns>Returns updated concentration grid of type float[,].</returns>
        public static float[,] Solve(
            float[,] concentrationGrid,
            float diffusionRate,
            float deltaTime,
            float epsilon = 0.0001f,
            float laplacianNeighborCount = 4f,
            float implicitSolverBaseDenominator = 1f)
        {
            if (concentrationGrid == null)
            {
                throw new ArgumentNullException(nameof(concentrationGrid));
            }

            int width = concentrationGrid.GetLength(0);
            int height = concentrationGrid.GetLength(1);
            float[,] resultGrid = new float[width, height];

            if (width == 0 || height == 0)
            {
                 return resultGrid;
            }

            if (float.IsNaN(diffusionRate) || float.IsInfinity(diffusionRate) || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                throw new ArgumentException("Invalid diffusionRate or deltaTime");
            }

            float safeDiffusionRate = Math.Max(0f, diffusionRate);
            float safeDeltaTime = Math.Max(0f, deltaTime);

            float rate = safeDiffusionRate * safeDeltaTime;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float center = concentrationGrid[x, y];

                    if (float.IsNaN(center) || float.IsInfinity(center))
                    {
                       center = 0f;
                    }

                    float n0 = x > 0 ? concentrationGrid[x - 1, y] : center;
                    if (float.IsNaN(n0) || float.IsInfinity(n0)) n0 = 0f;

                    float n1 = x < width - 1 ? concentrationGrid[x + 1, y] : center;
                    if (float.IsNaN(n1) || float.IsInfinity(n1)) n1 = 0f;

                    float n2 = y > 0 ? concentrationGrid[x, y - 1] : center;
                    if (float.IsNaN(n2) || float.IsInfinity(n2)) n2 = 0f;

                    float n3 = y < height - 1 ? concentrationGrid[x, y + 1] : center;
                    if (float.IsNaN(n3) || float.IsInfinity(n3)) n3 = 0f;

                    float neighborSum = n0 + n1 + n2 + n3;

                    float sumRate = laplacianNeighborCount * rate;
                    float jacobi = (neighborSum * rate + center) / Math.Max(epsilon, sumRate + implicitSolverBaseDenominator);

                    if (float.IsNaN(jacobi) || float.IsInfinity(jacobi))
                    {
                         jacobi = 0f;
                    }

                    resultGrid[x, y] = Math.Max(0f, jacobi);
                }
            }

            return resultGrid;
        }
    }
}
