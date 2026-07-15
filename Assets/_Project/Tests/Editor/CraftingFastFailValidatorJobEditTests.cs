using Hecton8.Crafting;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;

namespace Hecton8.Tests.Editor
{
    public sealed class CraftingFastFailValidatorJobEditTests
    {
        private const int CopperHash = 0x10101;
        private const int SealHash = 0x20202;
        private const int RelayHash = 0x30303;

        [Test]
        public void CraftingRecipeValidatorJob_AllRequirementsAvailable_ReturnsSuccess()
        {
            RunSingleRecipe(
                new[] { CopperHash, SealHash },
                new[] { 5, 3 },
                new[] { CopperHash, SealHash, 0, 0 },
                new[] { 3, 2, 0, 0 },
                out int craftable,
                out CraftingFastFailStatus status);

            Assert.AreEqual(1, craftable);
            Assert.AreEqual(CraftingFastFailStatus.Success, status);
        }

        [Test]
        public void CraftingRecipeValidatorJob_MissingIngredientHash_ReturnsMaskMissing()
        {
            RunSingleRecipe(
                new[] { CopperHash },
                new[] { 5 },
                new[] { CopperHash, RelayHash, 0, 0 },
                new[] { 3, 1, 0, 0 },
                out int craftable,
                out CraftingFastFailStatus status);

            Assert.AreEqual(0, craftable);
            Assert.AreEqual(CraftingFastFailStatus.MaskMissing, status);
        }

        [Test]
        public void CraftingRecipeValidatorJob_InsufficientQuantity_ReturnsMissingQuantity()
        {
            RunSingleRecipe(
                new[] { CopperHash, SealHash },
                new[] { 1, 3 },
                new[] { CopperHash, SealHash, 0, 0 },
                new[] { 3, 2, 0, 0 },
                out int craftable,
                out CraftingFastFailStatus status);

            Assert.AreEqual(0, craftable);
            Assert.AreEqual(CraftingFastFailStatus.MissingQuantity, status);
        }

        private static void RunSingleRecipe(
            int[] playerHashesSource,
            int[] playerCountsSource,
            int[] requirementHashesSource,
            int[] requirementCountsSource,
            out int craftable,
            out CraftingFastFailStatus status)
        {
            NativeArray<int> playerHashes = default;
            NativeArray<int> playerCounts = default;
            NativeArray<int> requirementHashes = default;
            NativeArray<int> requirementCounts = default;
            NativeArray<int> craftableResults = default;
            NativeArray<int> statusResults = default;

            try
            {
                playerHashes = new NativeArray<int>(playerHashesSource.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                playerCounts = new NativeArray<int>(playerCountsSource.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                requirementHashes = new NativeArray<int>(CraftingFastFailValidator.MaxIngredientsPerRecipe, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                requirementCounts = new NativeArray<int>(CraftingFastFailValidator.MaxIngredientsPerRecipe, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                craftableResults = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                statusResults = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                for (int i = 0; i < playerHashesSource.Length; i++)
                    playerHashes[i] = playerHashesSource[i];
                for (int i = 0; i < playerCountsSource.Length; i++)
                    playerCounts[i] = playerCountsSource[i];
                for (int i = 0; i < requirementHashesSource.Length && i < CraftingFastFailValidator.MaxIngredientsPerRecipe; i++)
                    requirementHashes[i] = requirementHashesSource[i];
                for (int i = 0; i < requirementCountsSource.Length && i < CraftingFastFailValidator.MaxIngredientsPerRecipe; i++)
                    requirementCounts[i] = requirementCountsSource[i];

                CraftingRecipeValidatorJob job = new CraftingRecipeValidatorJob
                {
                    PlayerItemHashes = playerHashes,
                    PlayerItemCounts = playerCounts,
                    RequiredRecipeHashes = requirementHashes,
                    RequiredRecipeCounts = requirementCounts,
                    CraftableResults = craftableResults,
                    StatusResults = statusResults,
                    PlayerItemCount = playerHashesSource.Length,
                    RecipeCount = 1,
                    MaxRequirementsPerRecipe = CraftingFastFailValidator.MaxIngredientsPerRecipe
                };

                job.Schedule(1, 1).Complete();

                craftable = craftableResults[0];
                status = (CraftingFastFailStatus)statusResults[0];
            }
            finally
            {
                if (statusResults.IsCreated)
                    statusResults.Dispose();
                if (craftableResults.IsCreated)
                    craftableResults.Dispose();
                if (requirementCounts.IsCreated)
                    requirementCounts.Dispose();
                if (requirementHashes.IsCreated)
                    requirementHashes.Dispose();
                if (playerCounts.IsCreated)
                    playerCounts.Dispose();
                if (playerHashes.IsCreated)
                    playerHashes.Dispose();
            }
        }
    }
}
