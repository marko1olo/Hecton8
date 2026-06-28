using System;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for SymbiosisBenefitMatrixCalculator.
    /// Extracted from ShinobuFloraFaunaSymbiosisSolver.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class SymbiosisBenefitMatrixCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="speciesPopulations">Parameter representing the speciesPopulations (float[]).</param>
        /// <param name="interactionMatrix">Parameter representing the interactionMatrix (float[,]).</param>
        /// <returns>Returns netBenefitPerSpecies of type float[].</returns>
        public static float[] Compute(float[] speciesPopulations, float[,] interactionMatrix)
        {
            if (speciesPopulations == null) throw new ArgumentNullException(nameof(speciesPopulations));
            if (interactionMatrix == null) throw new ArgumentNullException(nameof(interactionMatrix));

            int rows = interactionMatrix.GetLength(0);
            int cols = interactionMatrix.GetLength(1);

            if (speciesPopulations.Length != cols || rows != cols)
                throw new ArgumentException("Interaction matrix must be a square matrix matching the population count.");

            int n = rows;
            float[] netBenefitPerSpecies = new float[n];

            for (int i = 0; i < n; i++)
            {
                float popI = speciesPopulations[i];
                if (float.IsNaN(popI) || float.IsInfinity(popI) || popI < 0f)
                    popI = 0f;

                if (popI <= 0f)
                {
                    netBenefitPerSpecies[i] = 0f;
                    continue;
                }

                double netBenefit = 0.0;

                for (int j = 0; j < n; j++)
                {
                    float popJ = speciesPopulations[j];
                    if (float.IsNaN(popJ) || float.IsInfinity(popJ) || popJ < 0f)
                        popJ = 0f;

                    float interaction = interactionMatrix[i, j];
                    if (float.IsNaN(interaction) || float.IsInfinity(interaction))
                        interaction = 0f;

                    // Matrix multiply: interaction value scaled by both populations
                    // representing total population-level benefit or harm
                    netBenefit += (double)interaction * (double)popJ * (double)popI;
                }

                if (netBenefit > float.MaxValue) netBenefit = float.MaxValue;
                if (netBenefit < -float.MaxValue) netBenefit = -float.MaxValue;
                if (double.IsNaN(netBenefit)) netBenefit = 0.0;

                netBenefitPerSpecies[i] = (float)netBenefit;
            }

            return netBenefitPerSpecies;
        }
    }
}
