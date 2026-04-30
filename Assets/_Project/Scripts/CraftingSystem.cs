using Hecton.Localization;
using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Inventory;
using Hecton8.Power;
using Hecton8.SaveSystem;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Crafting
{
    /// <summary>
    /// Zero-GC fabrication helper that flattens authored recipe costs into contiguous native buffers.
    /// </summary>
    internal static class CraftingSystem
    {
        public const int MaxRecipeIngredientCount = 32;
        public const int MaxDeconstructionOutputCount = MaxRecipeIngredientCount;
        public const int MaxRecursiveDeconstructionNodeCount = 64;

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct EvaluateRecipeAvailabilityJob : IJob
        {
            [ReadOnly] public NativeArray<int2> RecipeCosts;
            [ReadOnly] public NativeParallelHashMap<int, int> AvailableItemCounts;
            public NativeArray<byte> Result;
            public int RecipeCostCount;

            public void Execute()
            {
                byte canCraft = 1;
                for (int index = 0; index < RecipeCostCount; index++)
                {
                    int2 cost = RecipeCosts[index];
                    if (cost.x == 0 || cost.y <= 0)
                        continue;

                    if (!AvailableItemCounts.TryGetValue(cost.x, out int availableCount) ||
                        availableCount < cost.y)
                    {
                        canCraft = 0;
                        break;
                    }
                }

                Result[0] = canCraft;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BuildDeconstructionYieldJob : IJob
        {
            [ReadOnly] public NativeArray<int2> RecipeCosts;
            public NativeArray<int2> OutputYields;
            public NativeArray<int> OutputCount;
            public int RecipeCostCount;
            public int ResultQuantity;
            public int ReclaimPercent;
            public int ScrapItemHashId;
            public byte ForceScrapYield;

            public void Execute()
            {
                int safeResultQuantity = math.max(1, ResultQuantity);
                int resolvedCount = 0;
                int scrapYield = 0;

                for (int index = 0; index < OutputYields.Length; index++)
                    OutputYields[index] = int2.zero;

                for (int index = 0; index < RecipeCostCount; index++)
                {
                    int2 cost = RecipeCosts[index];
                    if (cost.x == 0 || cost.y <= 0)
                        continue;

                    int scaledYield = (cost.y * ReclaimPercent) / (safeResultQuantity * 100);
                    if (scaledYield <= 0 && ReclaimPercent > 0)
                        scaledYield = 1;

                    if (ForceScrapYield != 0)
                    {
                        scrapYield += math.max(0, scaledYield);
                        continue;
                    }

                    if (scaledYield <= 0 || resolvedCount >= OutputYields.Length)
                        continue;

                    OutputYields[resolvedCount++] = new int2(cost.x, scaledYield);
                }

                if (ForceScrapYield != 0 && ScrapItemHashId != 0 && scrapYield > 0 && OutputYields.Length > 0)
                {
                    OutputYields[0] = new int2(ScrapItemHashId, math.max(1, scrapYield));
                    resolvedCount = 1;
                }

                OutputCount[0] = resolvedCount;
            }
        }

        public static bool CanCraft(
            RecipeData recipe,
            Fabricator fabricator,
            PlayerInventory inventory,
            NativeParallelHashMap<int, int> availableItemCounts,
            NativeArray<int2> recipeCosts,
            NativeArray<byte> result)
        {
            if (recipe == null ||
                fabricator == null ||
                inventory == null ||
                !availableItemCounts.IsCreated ||
                !recipeCosts.IsCreated ||
                recipeCosts.Length < MaxRecipeIngredientCount ||
                !result.IsCreated ||
                result.Length == 0 ||
                !inventory.TryCopyAvailableItemCountsNonAlloc(availableItemCounts, out _))
            {
                return false;
            }

            if (!TryBuildRecipeCostBuffer(recipe, fabricator, recipeCosts, out int recipeCostCount))
                return false;

            MergeAccessibleNetworkCounts(fabricator, availableItemCounts, recipeCosts, recipeCostCount);

            result[0] = 0;
            new EvaluateRecipeAvailabilityJob
            {
                RecipeCosts = recipeCosts,
                AvailableItemCounts = availableItemCounts,
                Result = result,
                RecipeCostCount = recipeCostCount
            }.Run();

            return result[0] != 0;
        }

        public static bool TryBuildRecipeCostBuffer(
            RecipeData recipe,
            Fabricator fabricator,
            NativeArray<int2> recipeCosts,
            out int recipeCostCount)
        {
            recipeCostCount = 0;
            if (recipe == null || fabricator == null || !recipeCosts.IsCreated)
                return false;

            for (int index = 0; index < recipeCosts.Length; index++)
                recipeCosts[index] = int2.zero;

            if (recipe.ingredients == null || recipe.ingredients.Count == 0)
                return false;

            for (int ingredientIndex = 0; ingredientIndex < recipe.ingredients.Count; ingredientIndex++)
            {
                InventoryCost cost = recipe.ingredients[ingredientIndex];
                if (cost == null || cost.item == null || cost.amount <= 0)
                    continue;

                int itemHashId = LocHash.Compute(cost.item.PersistentId);
                int adjustedAmount = fabricator.GetAdjustedIngredientAmount(cost);
                if (itemHashId == 0 || adjustedAmount <= 0)
                    continue;

                int existingIndex = FindCostIndex(recipeCosts, recipeCostCount, itemHashId);
                if (existingIndex >= 0)
                {
                    int2 existing = recipeCosts[existingIndex];
                    existing.y += adjustedAmount;
                    recipeCosts[existingIndex] = existing;
                    continue;
                }

                if (recipeCostCount >= recipeCosts.Length)
                    return false;

                recipeCosts[recipeCostCount++] = new int2(itemHashId, adjustedAmount);
            }

            return recipeCostCount > 0;
        }

        public static bool TryBuildDeconstructionYieldBuffer(
            RecipeData recipe,
            Fabricator fabricator,
            ItemCatalog itemCatalog,
            bool forceScrapYield,
            int reclaimPercent,
            NativeArray<int2> recipeCosts,
            NativeArray<int2> flattenedCosts,
            NativeArray<int2> outputYields,
            NativeArray<int> outputCount,
            int scrapItemHashId)
        {
            if (recipe == null ||
                fabricator == null ||
                !recipeCosts.IsCreated ||
                !flattenedCosts.IsCreated ||
                !outputYields.IsCreated ||
                !outputCount.IsCreated ||
                outputCount.Length == 0 ||
                flattenedCosts.Length < MaxDeconstructionOutputCount ||
                outputYields.Length < MaxDeconstructionOutputCount)
            {
                return false;
            }

            if (!TryBuildRecipeCostBuffer(recipe, fabricator, recipeCosts, out int recipeCostCount))
                return false;

            NativeArray<int2> sourceCosts = recipeCosts;
            int sourceCostCount = recipeCostCount;
            if (TryFlattenDeconstructionCosts(itemCatalog, fabricator, recipeCosts, recipeCostCount, flattenedCosts, out int flattenedCostCount))
            {
                sourceCosts = flattenedCosts;
                sourceCostCount = flattenedCostCount;
            }

            outputCount[0] = 0;
            new BuildDeconstructionYieldJob
            {
                RecipeCosts = sourceCosts,
                OutputYields = outputYields,
                OutputCount = outputCount,
                RecipeCostCount = sourceCostCount,
                ResultQuantity = 1,
                ReclaimPercent = math.clamp(reclaimPercent, 0, 100),
                ScrapItemHashId = scrapItemHashId,
                ForceScrapYield = forceScrapYield ? (byte)1 : (byte)0
            }.Run();

            return outputCount[0] > 0;
        }

        private static bool TryFlattenDeconstructionCosts(
            ItemCatalog itemCatalog,
            Fabricator fabricator,
            NativeArray<int2> recipeCosts,
            int recipeCostCount,
            NativeArray<int2> flattenedCosts,
            out int flattenedCostCount)
        {
            flattenedCostCount = 0;
            if (itemCatalog == null || fabricator == null || !recipeCosts.IsCreated || !flattenedCosts.IsCreated)
                return false;

            for (int index = 0; index < flattenedCosts.Length; index++)
                flattenedCosts[index] = int2.zero;

            int visitedNodeCount = 0;
            for (int index = 0; index < recipeCostCount; index++)
            {
                int2 cost = recipeCosts[index];
                if (cost.x == 0 || cost.y <= 0)
                    continue;

                if (!TryAddFlattenedCostRecursive(
                        itemCatalog,
                        fabricator,
                        cost.x,
                        cost.y,
                        flattenedCosts,
                        ref flattenedCostCount,
                        ref visitedNodeCount))
                {
                    flattenedCostCount = 0;
                    return false;
                }
            }

            return flattenedCostCount > 0;
        }

        private static bool TryAddFlattenedCostRecursive(
            ItemCatalog itemCatalog,
            Fabricator fabricator,
            int itemHashId,
            int quantity,
            NativeArray<int2> flattenedCosts,
            ref int flattenedCostCount,
            ref int visitedNodeCount)
        {
            if (itemHashId == 0 || quantity <= 0)
                return true;

            if (visitedNodeCount++ >= MaxRecursiveDeconstructionNodeCount)
                return TryAddMergedCost(flattenedCosts, ref flattenedCostCount, itemHashId, quantity);

            if (!Fabricator.TryResolveRecipeForResultHash(itemCatalog, itemHashId, out RecipeData subRecipe) ||
                subRecipe == null ||
                subRecipe.ingredients == null ||
                subRecipe.ingredients.Count == 0)
            {
                return TryAddMergedCost(flattenedCosts, ref flattenedCostCount, itemHashId, quantity);
            }

            int safeResultQuantity = math.max(1, subRecipe.resultQuantity);
            for (int ingredientIndex = 0; ingredientIndex < subRecipe.ingredients.Count; ingredientIndex++)
            {
                InventoryCost cost = subRecipe.ingredients[ingredientIndex];
                if (cost == null || cost.item == null || cost.amount <= 0)
                    continue;

                int ingredientHashId = LocHash.Compute(cost.item.PersistentId);
                int adjustedAmount = fabricator.GetAdjustedIngredientAmount(cost);
                if (ingredientHashId == 0 || adjustedAmount <= 0)
                    continue;

                long scaledLong = ((long)adjustedAmount * quantity + safeResultQuantity - 1L) / safeResultQuantity;
                int scaledQuantity = scaledLong > int.MaxValue ? int.MaxValue : (int)scaledLong;
                if (!TryAddFlattenedCostRecursive(
                        itemCatalog,
                        fabricator,
                        ingredientHashId,
                        scaledQuantity,
                        flattenedCosts,
                        ref flattenedCostCount,
                        ref visitedNodeCount))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryAddMergedCost(NativeArray<int2> costs, ref int costCount, int itemHashId, int quantity)
        {
            if (itemHashId == 0 || quantity <= 0)
                return true;

            int existingIndex = FindCostIndex(costs, costCount, itemHashId);
            if (existingIndex >= 0)
            {
                int2 existing = costs[existingIndex];
                existing.y = existing.y > int.MaxValue - quantity ? int.MaxValue : existing.y + quantity;
                costs[existingIndex] = existing;
                return true;
            }

            if (costCount >= costs.Length)
                return false;

            costs[costCount++] = new int2(itemHashId, quantity);
            return true;
        }

        private static void MergeAccessibleNetworkCounts(
            Fabricator fabricator,
            NativeParallelHashMap<int, int> availableItemCounts,
            NativeArray<int2> recipeCosts,
            int recipeCostCount)
        {
            PowerGrid grid = fabricator.CurrentPowerGrid;
            if (grid == null)
                return;

            for (int index = 0; index < recipeCostCount; index++)
            {
                int2 cost = recipeCosts[index];
                if (cost.x == 0 || cost.y <= 0)
                    continue;

                int networkCount = BaseLogisticsNetwork.CountAccessibleItem(grid, cost.x);
                if (networkCount <= 0)
                    continue;

                if (availableItemCounts.TryGetValue(cost.x, out int localCount))
                    availableItemCounts[cost.x] = localCount + networkCount;
                else
                    availableItemCounts.TryAdd(cost.x, networkCount);
            }
        }

        private static int FindCostIndex(NativeArray<int2> recipeCosts, int recipeCostCount, int itemHashId)
        {
            for (int index = 0; index < recipeCostCount; index++)
            {
                if (recipeCosts[index].x == itemHashId)
                    return index;
            }

            return -1;
        }
    }
}
