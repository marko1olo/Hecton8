namespace Hecton8.EditorTools
{
    internal static class WorldProceduralFloraFinalBudgetCatalog
    {
        internal static Budget Resolve(string familyId)
        {
            switch (familyId)
            {
                case "family.kelp.tall":
                    return new Budget(12, 6, 8000, 4500, 360);
                case "family.kelp.patch.dense":
                    return new Budget(18, 8, 12000, 6500, 320);
                case "family.kelp.canopy":
                    return new Budget(14, 6, 10000, 5500, 460);
                case "family.kelp.abyssal":
                    return new Budget(14, 6, 9000, 5200, 380);
                case "family.coral.low":
                    return new Budget(10, 4, 7000, 3500, 900);
                case "family.coral.branching":
                    return new Budget(16, 6, 12000, 6500, 800);
                case "family.coral.massive":
                    return new Budget(12, 5, 9000, 5000, 1100);
                case "family.coral.plate":
                    return new Budget(12, 5, 8500, 4500, 220);
                case "family.coral.brittle":
                    return new Budget(14, 6, 9500, 5200, 720);
                default:
                    return new Budget(12, 4, 8000, 4000, 300);
            }
        }

        internal readonly struct Budget
        {
            internal Budget(int maxRenderers, int maxMaterialSlots, int maxTriangles, int lodRecommendedTriangleThreshold, int minRecommendedTriangles)
            {
                MaxRenderers = maxRenderers;
                MaxMaterialSlots = maxMaterialSlots;
                MaxTriangles = maxTriangles;
                LodRecommendedTriangleThreshold = lodRecommendedTriangleThreshold;
                MinRecommendedTriangles = minRecommendedTriangles;
            }

            internal int MaxRenderers { get; }
            internal int MaxMaterialSlots { get; }
            internal int MaxTriangles { get; }
            internal int LodRecommendedTriangleThreshold { get; }
            internal int MinRecommendedTriangles { get; }
        }
    }
}
