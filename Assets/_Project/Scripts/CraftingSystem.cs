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
        public const int MaxComplexRecipeDepth = 5;
        public const int MaxComplexRecipeNodeCount = 64;
        public const int MaxComplexRecipeEdgeCount = 128;
        private const int MaxDeconstructionRecursionDepth = 64;

        [BurstCompile]
        internal struct EvaluateRecipeAvailabilityJob : IJob
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

        [BurstCompile]
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

        [BurstCompile]
        internal struct KahnTotalRawCostJob : IJob
        {
            [ReadOnly] public NativeArray<int2> GraphNodes;
            [ReadOnly] public NativeArray<int2> GraphEdges;
            public NativeArray<int> InDegrees;
            public NativeArray<int> Queue;
            public NativeArray<int2> RawCosts;
            public NativeArray<int> RawCostCount;
            public NativeArray<byte> Status;
            public int NodeCount;
            public int EdgeCount;

            public void Execute()
            {
                if (Status.IsCreated && Status.Length > 0)
                    Status[0] = 0;

                if (!RawCostCount.IsCreated || RawCostCount.Length == 0)
                    return;

                RawCostCount[0] = 0;
                for (int index = 0; index < RawCosts.Length; index++)
                    RawCosts[index] = int2.zero;

                if (NodeCount <= 0 ||
                    NodeCount > GraphNodes.Length ||
                    NodeCount > InDegrees.Length ||
                    NodeCount > Queue.Length ||
                    EdgeCount < 0 ||
                    EdgeCount > GraphEdges.Length)
                {
                    return;
                }

                int head = 0;
                int tail = 0;
                for (int nodeIndex = 0; nodeIndex < NodeCount; nodeIndex++)
                {
                    if (InDegrees[nodeIndex] == 0)
                        Queue[tail++] = nodeIndex;
                }

                int processed = 0;
                while (head < tail)
                {
                    int nodeIndex = Queue[head++];
                    processed++;

                    bool hasOutgoingEdge = false;
                    for (int edgeIndex = 0; edgeIndex < EdgeCount; edgeIndex++)
                    {
                        int2 edge = GraphEdges[edgeIndex];
                        if (edge.x != nodeIndex)
                            continue;

                        hasOutgoingEdge = true;
                        int childIndex = edge.y;
                        if ((uint)childIndex >= (uint)NodeCount)
                            return;

                        int remainingInDegree = InDegrees[childIndex] - 1;
                        InDegrees[childIndex] = remainingInDegree;
                        if (remainingInDegree == 0)
                        {
                            if (tail >= Queue.Length)
                                return;

                            Queue[tail++] = childIndex;
                        }
                    }

                    if (!hasOutgoingEdge && !TryMergeRawCost(GraphNodes[nodeIndex]))
                        return;
                }

                if (processed != NodeCount)
                    return;

                if (Status.IsCreated && Status.Length > 0)
                    Status[0] = 1;
            }

            private bool TryMergeRawCost(int2 cost)
            {
                if (cost.x == 0 || cost.y <= 0)
                    return true;

                int count = RawCostCount[0];
                for (int index = 0; index < count; index++)
                {
                    int2 existing = RawCosts[index];
                    if (existing.x != cost.x)
                        continue;

                    existing.y = existing.y > int.MaxValue - cost.y ? int.MaxValue : existing.y + cost.y;
                    RawCosts[index] = existing;
                    return true;
                }

                if (count >= RawCosts.Length)
                    return false;

                RawCosts[count] = cost;
                RawCostCount[0] = count + 1;
                return true;
            }
        }

        public static bool CanCraft(
            RecipeData recipe,
            Fabricator fabricator,
            PlayerInventory inventory,
            NativeParallelHashMap<int, int> availableItemCounts,
            NativeArray<int2> recipeCosts,
            NativeArray<byte> result,
            NativeArray<int2> complexGraphNodes,
            NativeArray<int2> complexGraphEdges,
            NativeArray<int> complexGraphInDegrees,
            NativeArray<int> complexGraphQueue,
            NativeArray<int2> complexRawCosts,
            NativeArray<int> complexRawCostCount,
            NativeArray<byte> complexGraphStatus,
            int recipeMultiplier = 1)
        {
            if (recipe == null ||
                fabricator == null ||
                inventory == null ||
                !availableItemCounts.IsCreated ||
                !recipeCosts.IsCreated ||
                recipeCosts.Length < MaxRecipeIngredientCount ||
                !result.IsCreated ||
                result.Length == 0 ||
                !complexGraphNodes.IsCreated ||
                complexGraphNodes.Length < MaxComplexRecipeNodeCount ||
                !complexGraphEdges.IsCreated ||
                complexGraphEdges.Length < MaxComplexRecipeEdgeCount ||
                !complexGraphInDegrees.IsCreated ||
                complexGraphInDegrees.Length < MaxComplexRecipeNodeCount ||
                !complexGraphQueue.IsCreated ||
                complexGraphQueue.Length < MaxComplexRecipeNodeCount ||
                !complexRawCosts.IsCreated ||
                complexRawCosts.Length < MaxRecipeIngredientCount ||
                !complexRawCostCount.IsCreated ||
                complexRawCostCount.Length == 0 ||
                !complexGraphStatus.IsCreated ||
                complexGraphStatus.Length == 0 ||
                !inventory.TryCopyAvailableItemCountsNonAlloc(availableItemCounts, out _))
            {
                return false;
            }

            int safeMultiplier = math.max(1, recipeMultiplier);
            if (!TryBuildRecipeCostBuffer(recipe, fabricator, recipeCosts, out int recipeCostCount, safeMultiplier))
                return false;

            MergeAccessibleNetworkCounts(fabricator, availableItemCounts, recipeCosts, recipeCostCount);

            result[0] = 0;
            new EvaluateRecipeAvailabilityJob
            {
                RecipeCosts = recipeCosts,
                AvailableItemCounts = availableItemCounts,
                Result = result,
                RecipeCostCount = recipeCostCount
            }.Execute();

            if (result[0] != 0)
                return true;

            if (!TryBuildTotalRawCostBuffer(
                    recipe,
                    fabricator,
                    inventory.ItemCatalog,
                    complexGraphNodes,
                    complexGraphEdges,
                    complexGraphInDegrees,
                    complexGraphQueue,
                    complexRawCosts,
                    complexRawCostCount,
                    complexGraphStatus,
                    safeMultiplier))
            {
                return false;
            }

            int rawCostCount = complexRawCostCount[0];
            MergeAccessibleNetworkCounts(fabricator, availableItemCounts, complexRawCosts, rawCostCount);

            result[0] = 0;
            new EvaluateRecipeAvailabilityJob
            {
                RecipeCosts = complexRawCosts,
                AvailableItemCounts = availableItemCounts,
                Result = result,
                RecipeCostCount = rawCostCount
            }.Execute();

            return result[0] != 0;
        }

        public static bool TryBuildTotalRawCostBuffer(
            RecipeData recipe,
            Fabricator fabricator,
            ItemCatalog itemCatalog,
            NativeArray<int2> graphNodes,
            NativeArray<int2> graphEdges,
            NativeArray<int> graphInDegrees,
            NativeArray<int> graphQueue,
            NativeArray<int2> rawCosts,
            NativeArray<int> rawCostCount,
            NativeArray<byte> graphStatus,
            int recipeMultiplier = 1)
        {
            if (recipe == null ||
                fabricator == null ||
                itemCatalog == null ||
                !graphNodes.IsCreated ||
                graphNodes.Length < MaxComplexRecipeNodeCount ||
                !graphEdges.IsCreated ||
                graphEdges.Length < MaxComplexRecipeEdgeCount ||
                !graphInDegrees.IsCreated ||
                graphInDegrees.Length < MaxComplexRecipeNodeCount ||
                !graphQueue.IsCreated ||
                graphQueue.Length < MaxComplexRecipeNodeCount ||
                !rawCosts.IsCreated ||
                rawCosts.Length < MaxRecipeIngredientCount ||
                !rawCostCount.IsCreated ||
                rawCostCount.Length == 0 ||
                !graphStatus.IsCreated ||
                graphStatus.Length == 0 ||
                recipe.ingredients == null ||
                recipe.ingredients.Count == 0)
            {
                return false;
            }

            for (int index = 0; index < graphNodes.Length; index++)
                graphNodes[index] = int2.zero;
            for (int index = 0; index < graphEdges.Length; index++)
                graphEdges[index] = int2.zero;
            for (int index = 0; index < graphInDegrees.Length; index++)
                graphInDegrees[index] = 0;
            for (int index = 0; index < graphQueue.Length; index++)
                graphQueue[index] = 0;
            for (int index = 0; index < rawCosts.Length; index++)
                rawCosts[index] = int2.zero;

            rawCostCount[0] = 0;
            graphStatus[0] = 0;

            int nodeCount = 0;
            int edgeCount = 0;
            bool expandedAnySubcomponent = false;
            int safeMultiplier = math.max(1, recipeMultiplier);

            for (int ingredientIndex = 0; ingredientIndex < recipe.ingredients.Count; ingredientIndex++)
            {
                InventoryCost cost = recipe.ingredients[ingredientIndex];
                if (cost == null || cost.item == null || cost.amount <= 0)
                    continue;

                int itemHashId = LocHash.Compute(cost.item.PersistentId);
                int adjustedAmount = fabricator.GetAdjustedIngredientAmount(cost);
                if (itemHashId == 0 || adjustedAmount <= 0)
                    continue;

                int scaledAmount = ScaleCostAmount(adjustedAmount, safeMultiplier);
                int nodeIndex = AppendComplexRecipeNode(graphNodes, ref nodeCount, itemHashId, scaledAmount);
                if (nodeIndex < 0)
                    return false;

                if (!TryAppendComplexRecipeChildren(
                        itemCatalog,
                        fabricator,
                        graphNodes,
                        graphEdges,
                        graphInDegrees,
                        ref nodeCount,
                        ref edgeCount,
                        nodeIndex,
                        itemHashId,
                        scaledAmount,
                        0,
                        itemHashId,
                        ref expandedAnySubcomponent))
                {
                    return false;
                }
            }

            if (nodeCount <= 0 || !expandedAnySubcomponent)
                return false;

            new KahnTotalRawCostJob
            {
                GraphNodes = graphNodes,
                GraphEdges = graphEdges,
                InDegrees = graphInDegrees,
                Queue = graphQueue,
                RawCosts = rawCosts,
                RawCostCount = rawCostCount,
                Status = graphStatus,
                NodeCount = nodeCount,
                EdgeCount = edgeCount
            }.Execute();

            return graphStatus[0] != 0 && rawCostCount[0] > 0;
        }

        private static int AppendComplexRecipeNode(
            NativeArray<int2> graphNodes,
            ref int nodeCount,
            int itemHashId,
            int quantity)
        {
            if (itemHashId == 0 || quantity <= 0 || nodeCount >= graphNodes.Length)
                return -1;

            int nodeIndex = nodeCount;
            graphNodes[nodeIndex] = new int2(itemHashId, quantity);
            nodeCount++;
            return nodeIndex;
        }

        private static bool TryAppendComplexRecipeChildren(
            ItemCatalog itemCatalog,
            Fabricator fabricator,
            NativeArray<int2> graphNodes,
            NativeArray<int2> graphEdges,
            NativeArray<int> graphInDegrees,
            ref int nodeCount,
            ref int edgeCount,
            int parentNodeIndex,
            int parentItemHashId,
            int parentQuantity,
            int depth,
            int rootHashId,
            ref bool expandedAnySubcomponent)
        {
            if (depth >= MaxComplexRecipeDepth)
                return true;

            if (!Fabricator.TryResolveRecipeForResultHash(itemCatalog, parentItemHashId, out RecipeData subRecipe) ||
                subRecipe == null ||
                subRecipe.ingredients == null ||
                subRecipe.ingredients.Count == 0)
            {
                return true;
            }

            int safeResultQuantity = math.max(1, subRecipe.resultQuantity);
            bool appendedChild = false;
            for (int ingredientIndex = 0; ingredientIndex < subRecipe.ingredients.Count; ingredientIndex++)
            {
                InventoryCost cost = subRecipe.ingredients[ingredientIndex];
                if (cost == null || cost.item == null || cost.amount <= 0)
                    continue;

                int childHashId = LocHash.Compute(cost.item.PersistentId);
                int adjustedAmount = fabricator.GetAdjustedIngredientAmount(cost);
                if (childHashId == 0 || adjustedAmount <= 0)
                    continue;

                if (childHashId == parentItemHashId || childHashId == rootHashId)
                    return false;

                long scaledLong = ((long)adjustedAmount * parentQuantity + safeResultQuantity - 1L) / safeResultQuantity;
                int childQuantity = scaledLong > int.MaxValue ? int.MaxValue : (int)scaledLong;
                int childNodeIndex = AppendComplexRecipeNode(graphNodes, ref nodeCount, childHashId, childQuantity);
                if (childNodeIndex < 0 || edgeCount >= graphEdges.Length)
                    return false;

                graphEdges[edgeCount++] = new int2(parentNodeIndex, childNodeIndex);
                graphInDegrees[childNodeIndex] = graphInDegrees[childNodeIndex] + 1;
                appendedChild = true;

                if (!TryAppendComplexRecipeChildren(
                        itemCatalog,
                        fabricator,
                        graphNodes,
                        graphEdges,
                        graphInDegrees,
                        ref nodeCount,
                        ref edgeCount,
                        childNodeIndex,
                        childHashId,
                        childQuantity,
                        depth + 1,
                        rootHashId,
                        ref expandedAnySubcomponent))
                {
                    return false;
                }
            }

            if (appendedChild)
                expandedAnySubcomponent = true;

            return true;
        }

        public static bool TryBuildRecipeCostBuffer(
            RecipeData recipe,
            Fabricator fabricator,
            NativeArray<int2> recipeCosts,
            out int recipeCostCount,
            int recipeMultiplier = 1)
        {
            recipeCostCount = 0;
            if (recipe == null || fabricator == null || !recipeCosts.IsCreated)
                return false;

            for (int index = 0; index < recipeCosts.Length; index++)
                recipeCosts[index] = int2.zero;

            if (recipe.ingredients == null || recipe.ingredients.Count == 0)
                return false;

            int safeMultiplier = math.max(1, recipeMultiplier);
            for (int ingredientIndex = 0; ingredientIndex < recipe.ingredients.Count; ingredientIndex++)
            {
                InventoryCost cost = recipe.ingredients[ingredientIndex];
                if (cost == null || cost.item == null || cost.amount <= 0)
                    continue;

                int itemHashId = LocHash.Compute(cost.item.PersistentId);
                int adjustedAmount = fabricator.GetAdjustedIngredientAmount(cost);
                if (itemHashId == 0 || adjustedAmount <= 0)
                    continue;

                adjustedAmount = ScaleCostAmount(adjustedAmount, safeMultiplier);
                int existingIndex = FindCostIndex(recipeCosts, recipeCostCount, itemHashId);
                if (existingIndex >= 0)
                {
                    int2 existing = recipeCosts[existingIndex];
                    existing.y = ScaleCostAmount(existing.y, 1, adjustedAmount);
                    recipeCosts[existingIndex] = existing;
                    continue;
                }

                if (recipeCostCount >= recipeCosts.Length)
                    return false;

                recipeCosts[recipeCostCount++] = new int2(itemHashId, adjustedAmount);
            }

            return recipeCostCount > 0;
        }

        private static int ScaleCostAmount(int baseAmount, int multiplier, int additive = 0)
        {
            if (baseAmount <= 0)
                return math.max(0, additive);

            long scaled = (long)baseAmount * math.max(1, multiplier) + math.max(0, additive);
            return scaled > int.MaxValue ? int.MaxValue : (int)scaled;
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
            int scrapItemHashId,
            int recipeMultiplier = 1)
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

            int safeMultiplier = math.max(1, recipeMultiplier);
            if (!TryBuildRecipeCostBuffer(recipe, fabricator, recipeCosts, out int recipeCostCount, safeMultiplier))
                return false;

            NativeArray<int2> sourceCosts = recipeCosts;
            int sourceCostCount = recipeCostCount;
            bool resolvedForceScrapYield = forceScrapYield;
            if (TryFlattenDeconstructionCosts(
                    itemCatalog,
                    fabricator,
                    recipeCosts,
                    recipeCostCount,
                    flattenedCosts,
                    out int flattenedCostCount,
                    out bool recursionGuardTripped))
            {
                sourceCosts = flattenedCosts;
                sourceCostCount = flattenedCostCount;
            }
            else if (recursionGuardTripped)
            {
                resolvedForceScrapYield = true;
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
                ForceScrapYield = resolvedForceScrapYield ? (byte)1 : (byte)0
            }.Execute();

            return outputCount[0] > 0;
        }

        private static bool TryFlattenDeconstructionCosts(
            ItemCatalog itemCatalog,
            Fabricator fabricator,
            NativeArray<int2> recipeCosts,
            int recipeCostCount,
            NativeArray<int2> flattenedCosts,
            out int flattenedCostCount,
            out bool recursionGuardTripped)
        {
            flattenedCostCount = 0;
            recursionGuardTripped = false;
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
                        ref visitedNodeCount,
                        0,
                        cost.x,
                        ref recursionGuardTripped))
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
            ref int visitedNodeCount,
            int recursionDepth,
            int rootHashId,
            ref bool recursionGuardTripped)
        {
            if (itemHashId == 0 || quantity <= 0)
                return true;

            if (recursionDepth >= MaxDeconstructionRecursionDepth ||
                visitedNodeCount++ >= MaxRecursiveDeconstructionNodeCount)
            {
                recursionGuardTripped = true;
                return false;
            }

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

                if (ingredientHashId == itemHashId || ingredientHashId == rootHashId)
                {
                    recursionGuardTripped = true;
                    return false;
                }

                long scaledLong = ((long)adjustedAmount * quantity + safeResultQuantity - 1L) / safeResultQuantity;
                int scaledQuantity = scaledLong > int.MaxValue ? int.MaxValue : (int)scaledLong;
                if (!TryAddFlattenedCostRecursive(
                        itemCatalog,
                        fabricator,
                        ingredientHashId,
                        scaledQuantity,
                        flattenedCosts,
                        ref flattenedCostCount,
                        ref visitedNodeCount,
                        recursionDepth + 1,
                        rootHashId,
                        ref recursionGuardTripped))
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
