namespace Hecton8.EditorTools
{
    internal static class WorldProceduralFloraFinalBudgetCatalog
    {
        internal static Budget Resolve(string familyId)
        {
            switch (familyId)
            {
                case "family.kelp.tall":
                    return new Budget(12, 6, 8000, 4500);
                case "family.kelp.patch.dense":
                    return new Budget(18, 8, 12000, 6500);
                case "family.kelp.canopy":
                    return new Budget(14, 6, 10000, 5500);
                case "family.coral.low":
                    return new Budget(10, 4, 7000, 3500);
                case "family.coral.branching":
                    return new Budget(16, 6, 12000, 6500);
                case "family.coral.massive":
                    return new Budget(12, 5, 9000, 5000);
                case "family.coral.plate":
                    return new Budget(12, 5, 8500, 4500);
                default:
                    return new Budget(12, 4, 8000, 4000);
            }
        }

        internal readonly struct Budget
        {
            internal Budget(int maxRenderers, int maxMaterialSlots, int maxTriangles, int lodRecommendedTriangleThreshold)
            {
                MaxRenderers = maxRenderers;
                MaxMaterialSlots = maxMaterialSlots;
                MaxTriangles = maxTriangles;
                LodRecommendedTriangleThreshold = lodRecommendedTriangleThreshold;
            }

            internal int MaxRenderers { get; }
            internal int MaxMaterialSlots { get; }
            internal int MaxTriangles { get; }
            internal int LodRecommendedTriangleThreshold { get; }
        }
    }
}
