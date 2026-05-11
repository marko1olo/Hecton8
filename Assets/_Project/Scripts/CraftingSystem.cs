using Hecton.Localization;
using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Inventory;
using Hecton8.Items;
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
        public const int MaxComplexRecipeDepth = 5;
        public const int MaxComplexRecipeNodeCount = 64;
        public const int MaxComplexRecipeEdgeCount = 128;

        [BurstCompile]
        internal struct EvaluateRecipeAvailabilityJob : IJob
        {
            [ReadOnly] public NativeArray<int2> RecipeCosts;
            [ReadOnly] public NativeParallelHashMap<int, int> AvailableItemCounts;
            public NativeArray<byte> Result;
            public int RecipeCostCount;
            public ulong AvailableResourceMask;
            public ulong RecipeResourceMask;

            public void Execute()
            {
                if ((AvailableResourceMask & RecipeResourceMask) != RecipeResourceMask)
                {
                    Result[0] = 0;
                    return;
                }

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
                !inventory.TryCopyAvailableItemCountsNonAlloc(availableItemCounts, out _, out ulong localAvailableResourceMask))
            {
                return false;
            }

            int safeMultiplier = math.max(1, recipeMultiplier);
            if (!TryBuildRecipeCostBuffer(recipe, fabricator, recipeCosts, out int recipeCostCount, safeMultiplier))
                return false;

            ulong availableResourceMask = localAvailableResourceMask;
            MergeAccessibleNetworkCounts(fabricator, availableItemCounts, recipeCosts, recipeCostCount, ref availableResourceMask);
            ulong recipeResourceMask = BuildRecipeResourceMask(recipeCosts, recipeCostCount);

            if ((availableResourceMask & recipeResourceMask) != recipeResourceMask)
            {
                if (safeMultiplier <= 1)
                    return false;
            }
            else
            {
                result[0] = 0;
                new EvaluateRecipeAvailabilityJob
                {
                    RecipeCosts = recipeCosts,
                    AvailableItemCounts = availableItemCounts,
                    Result = result,
                    RecipeCostCount = recipeCostCount,
                    AvailableResourceMask = availableResourceMask,
                    RecipeResourceMask = recipeResourceMask
                }.Execute();

                if (result[0] != 0)
                    return true;
            }

            if (safeMultiplier <= 1)
                return false;

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
            ulong rawAvailableResourceMask = availableResourceMask;
            MergeAccessibleNetworkCounts(fabricator, availableItemCounts, complexRawCosts, rawCostCount, ref rawAvailableResourceMask);
            ulong rawRecipeResourceMask = BuildRecipeResourceMask(complexRawCosts, rawCostCount);

            if ((rawAvailableResourceMask & rawRecipeResourceMask) != rawRecipeResourceMask)
                return false;

            result[0] = 0;
            new EvaluateRecipeAvailabilityJob
            {
                RecipeCosts = complexRawCosts,
                AvailableItemCounts = availableItemCounts,
                Result = result,
                RecipeCostCount = rawCostCount,
                AvailableResourceMask = rawAvailableResourceMask,
                RecipeResourceMask = rawRecipeResourceMask
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

                int itemHashId = cost.item.PersistentHashId;
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

                int childHashId = cost.item.PersistentHashId;
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

                int itemHashId = cost.item.PersistentHashId;
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
            ItemData sourceItem,
            NativeArray<int2> outputYields,
            NativeArray<int> outputCount)
        {
            if (sourceItem == null ||
                !outputYields.IsCreated ||
                !outputCount.IsCreated ||
                outputCount.Length == 0 ||
                outputYields.Length < MaxDeconstructionOutputCount)
            {
                return false;
            }

            for (int index = 0; index < outputYields.Length; index++)
                outputYields[index] = int2.zero;

            int resolvedCount = 0;
            int yieldCount = sourceItem.DeconstructYieldCount;
            for (int index = 0; index < yieldCount; index++)
            {
                if (!sourceItem.TryGetDeconstructYield(index, out DeconstructYieldEntry entry))
                    continue;

                int amount = entry.ResolveDeterministicAmount();
                if (amount <= 0 || entry.Item == null)
                    continue;

                int itemHashId = entry.Item.PersistentHashId;
                if (itemHashId == 0)
                    continue;

                if (!TryAddMergedCost(outputYields, ref resolvedCount, itemHashId, amount))
                    return false;
            }

            outputCount[0] = resolvedCount;
            return resolvedCount > 0;
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

        private static ulong BuildRecipeResourceMask(
            NativeArray<int2> recipeCosts,
            int recipeCostCount)
        {
            ulong recipeMask = 0UL;
            int safeCount = math.min(recipeCostCount, recipeCosts.IsCreated ? recipeCosts.Length : 0);
            for (int index = 0; index < safeCount; index++)
            {
                int itemHashId = recipeCosts[index].x;
                int quantity = recipeCosts[index].y;
                if (itemHashId == 0 || quantity <= 0)
                    continue;

                ulong resourceBit = 1UL << ResolveResourceMaskBit(itemHashId);
                recipeMask |= resourceBit;
            }

            return recipeMask;
        }

        private static int ResolveResourceMaskBit(int itemHashId)
        {
            return itemHashId & 63;
        }

        private static void MergeAccessibleNetworkCounts(
            Fabricator fabricator,
            NativeParallelHashMap<int, int> availableItemCounts,
            NativeArray<int2> recipeCosts,
            int recipeCostCount,
            ref ulong availableResourceMask)
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

                availableResourceMask |= 1UL << ResolveResourceMaskBit(cost.x);

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
