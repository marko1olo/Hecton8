using Hecton8.Construction;
using Hecton8.Inventory;
using Unity.Collections;

namespace Hecton8.Crafting
{
    public sealed partial class Fabricator
    {
        private bool HasIngredientsFastFailOrLegacy(RecipeData recipe, int multiplier)
        {
            if (TryHasIngredientsFastFail(recipe, multiplier, out bool craftable, out bool authoritativeLocalOnly) &&
                (craftable || authoritativeLocalOnly))
            {
                return craftable;
            }

            return HasIngredients(recipe, multiplier);
        }

        private bool TryHasIngredientsFastFail(RecipeData recipe, int multiplier, out bool craftable, out bool authoritativeLocalOnly)
        {
            craftable = false;
            authoritativeLocalOnly = CurrentPowerGrid == null;
            if (recipe == null ||
                _playerInventory == null ||
                !TryBuildAdjustedFastFailRequirement(recipe, multiplier, out RecipeRequirementDTO requirement))
            {
                return false;
            }

            if (!_playerInventory.TryReadFastFailInventorySoA(
                    out NativeArray<uint>.ReadOnly itemHashIds,
                    out NativeArray<uint>.ReadOnly quantities,
                    out int activeSlotCount,
                    out ulong currentInventoryMask))
            {
                return false;
            }

            return TryHasIngredientsFastFailFromSoA(
                in requirement,
                itemHashIds,
                quantities,
                activeSlotCount,
                currentInventoryMask,
                out craftable);
        }

        internal bool TryCanCraftFastFailPresentation(
            RecipeData recipe,
            int multiplier,
            NativeArray<uint>.ReadOnly itemHashIds,
            NativeArray<uint>.ReadOnly quantities,
            int activeSlotCount,
            ulong currentInventoryMask,
            out bool craftable)
        {
            craftable = false;
            if (recipe == null ||
                _isCrafting ||
                !HasOperationalPower ||
                _playerInventory == null ||
                _playerInventory.Grid == null ||
                recipe.resultItem == null ||
                recipe.resultQuantity <= 0 ||
                !IsRecipeUnlocked(recipe) ||
                !PassesBiomeLock(recipe))
            {
                return true;
            }

            int safeMultiplier = multiplier < 1 ? 1 : multiplier;
            if (!TryBuildAdjustedFastFailRequirement(recipe, safeMultiplier, out RecipeRequirementDTO requirement))
                return false;

            return TryCanCraftFastFailPresentation(
                recipe,
                safeMultiplier,
                in requirement,
                itemHashIds,
                quantities,
                activeSlotCount,
                currentInventoryMask,
                out craftable);
        }

        internal bool TryCanCraftFastFailPresentation(
            RecipeData recipe,
            int multiplier,
            in RecipeRequirementDTO requirement,
            NativeArray<uint>.ReadOnly itemHashIds,
            NativeArray<uint>.ReadOnly quantities,
            int activeSlotCount,
            ulong currentInventoryMask,
            out bool craftable)
        {
            craftable = false;
            if (recipe == null ||
                _isCrafting ||
                !HasOperationalPower ||
                _playerInventory == null ||
                _playerInventory.Grid == null ||
                recipe.resultItem == null ||
                recipe.resultQuantity <= 0 ||
                !IsRecipeUnlocked(recipe) ||
                !PassesBiomeLock(recipe))
            {
                return true;
            }

            int safeMultiplier = multiplier < 1 ? 1 : multiplier;
            if (!TryHasIngredientsFastFailFromSoA(
                    in requirement,
                    itemHashIds,
                    quantities,
                    activeSlotCount,
                    currentInventoryMask,
                    out bool hasIngredients))
            {
                return false;
            }

            if (!hasIngredients)
                return CurrentPowerGrid == null;

            craftable = !IsOutputStorageCapacityExceededFastOrExact(recipe, safeMultiplier);
            return true;
        }

        private bool IsOutputStorageCapacityExceededFastOrExact(RecipeData recipe, int multiplier)
        {
            if (recipe == null || recipe.resultItem == null || _playerInventory == null || _playerInventory.Grid == null)
                return false;

            int safeMultiplier = multiplier < 1 ? 1 : multiplier;
            long neededCells =
                (long)UnityEngine.Mathf.Max(1, recipe.resultItem.CellArea) *
                UnityEngine.Mathf.Max(1, recipe.resultQuantity) *
                safeMultiplier;
            if (neededCells <= _playerInventory.Grid.FreeCells)
                return false;

            return IsOutputStorageCapacityExceeded(recipe, safeMultiplier);
        }

        private static bool TryHasIngredientsFastFailFromSoA(
            in RecipeRequirementDTO requirement,
            NativeArray<uint>.ReadOnly itemHashIds,
            NativeArray<uint>.ReadOnly quantities,
            int activeSlotCount,
            ulong currentInventoryMask,
            out bool craftable)
        {
            craftable = false;
            ulong unlockedMask = CraftingFastFailValidator.NormalizePlayerUnlockMask(requirement.BlueprintUnlockMask);
            craftable = CraftingFastFailValidator.TryEvaluateRecipeAvailability(
                in requirement,
                itemHashIds,
                quantities,
                activeSlotCount,
                currentInventoryMask,
                unlockedMask,
                out CraftingFastFailStatus status,
                out _);

            return status != CraftingFastFailStatus.InvalidInput;
        }

        private bool TryReserveDirectFastFailRecipeCosts(RecipeData recipe, int multiplier)
        {
            if (_playerInventory == null ||
                !TryBuildAdjustedFastFailRequirement(recipe, multiplier, out RecipeRequirementDTO requirement))
            {
                return false;
            }

            uint hashA = requirement.IngredientHashA;
            uint hashB = requirement.IngredientHashB;
            uint hashC = requirement.IngredientHashC;
            uint hashD = requirement.IngredientHashD;
            CraftingFastFailValidator.UnpackQuantities(
                requirement.QuantitiesPacked,
                out uint quantityA,
                out uint quantityB,
                out uint quantityC,
                out uint quantityD);

            NormalizeFastFailReservationCosts(ref hashA, ref quantityA, ref hashB, ref quantityB, ref hashC, ref quantityC, ref hashD, ref quantityD);
            return TryReserveDirectFastFailCost(hashA, quantityA) &&
                   TryReserveDirectFastFailCost(hashB, quantityB) &&
                   TryReserveDirectFastFailCost(hashC, quantityC) &&
                   TryReserveDirectFastFailCost(hashD, quantityD) &&
                   TryCommitFastFailNetworkReservation();
        }

        private bool TryReserveDirectFastFailCost(uint itemHash, uint requiredQuantity)
        {
            if (itemHash == 0u || requiredQuantity == 0u)
                return true;

            PlayerInventory reservationOwner = _craftReservationOwner != null ? _craftReservationOwner : _playerInventory;
            if (reservationOwner == null)
                return false;

            int itemHashId = unchecked((int)itemHash);
            int remaining = requiredQuantity > int.MaxValue ? int.MaxValue : (int)requiredQuantity;
            if (!reservationOwner.TryReserveAvailableQuantityForCraft(
                    itemHashId,
                    remaining,
                    _localCraftReservations,
                    ref _localCraftReservationCount,
                    out int localTake))
            {
                return false;
            }

            remaining -= localTake;
            return remaining <= 0 || TryAccumulateNetworkCost(itemHashId, remaining);
        }

        private bool TryCommitFastFailNetworkReservation()
        {
            if (_networkCostCount <= 0)
                return true;

            return BaseLogisticsNetwork.TryReserveResources(
                CurrentPowerGrid,
                _networkCostItemHashes,
                _networkCostAmounts,
                _networkCostCount,
                out _networkReservation);
        }

        private static void NormalizeFastFailReservationCosts(
            ref uint hashA,
            ref uint quantityA,
            ref uint hashB,
            ref uint quantityB,
            ref uint hashC,
            ref uint quantityC,
            ref uint hashD,
            ref uint quantityD)
        {
            AccumulateDuplicateCost(ref hashA, ref quantityA, ref hashB, ref quantityB);
            AccumulateDuplicateCost(ref hashA, ref quantityA, ref hashC, ref quantityC);
            AccumulateDuplicateCost(ref hashA, ref quantityA, ref hashD, ref quantityD);
            AccumulateDuplicateCost(ref hashB, ref quantityB, ref hashC, ref quantityC);
            AccumulateDuplicateCost(ref hashB, ref quantityB, ref hashD, ref quantityD);
            AccumulateDuplicateCost(ref hashC, ref quantityC, ref hashD, ref quantityD);
        }

        private static void AccumulateDuplicateCost(ref uint firstHash, ref uint firstQuantity, ref uint secondHash, ref uint secondQuantity)
        {
            if (firstHash == 0u || firstHash != secondHash)
                return;

            firstQuantity += secondQuantity;
            secondHash = 0u;
            secondQuantity = 0u;
        }

        internal bool TryBuildAdjustedFastFailRequirement(
            RecipeData recipe,
            int multiplier,
            out RecipeRequirementDTO requirement)
        {
            return TryBuildAdjustedFastFailRequirement(recipe, multiplier, out requirement, out _);
        }

        internal bool TryBuildAdjustedFastFailRequirement(
            RecipeData recipe,
            int multiplier,
            out RecipeRequirementDTO requirement,
            out float inflationMultiplier)
        {
            requirement = default;
            inflationMultiplier = 1f;
            if (recipe == null ||
                recipe.resultItem == null ||
                recipe.ingredients == null ||
                recipe.ingredients.Count == 0)
            {
                return false;
            }

            uint resultHash = unchecked((uint)recipe.resultItem.PersistentHashId);
            if (resultHash == 0u)
                return false;

            uint hashA = 0u;
            uint hashB = 0u;
            uint hashC = 0u;
            uint hashD = 0u;
            uint quantityA = 0u;
            uint quantityB = 0u;
            uint quantityC = 0u;
            uint quantityD = 0u;
            int safeMultiplier = multiplier < 1 ? 1 : multiplier;
            int emitted = 0;

            for (int i = 0; i < recipe.ingredients.Count; i++)
            {
                Hecton8.Building.InventoryCost cost = recipe.ingredients[i];
                if (cost == null || cost.item == null || cost.amount <= 0)
                    continue;

                uint hash = unchecked((uint)cost.item.PersistentHashId);
                int adjustedAmount = CalculateAdjustedIngredientAmount(cost);
                if (adjustedAmount <= 0)
                    continue;

                if (adjustedAmount > cost.amount)
                {
                    float candidateMultiplier = adjustedAmount / (float)cost.amount;
                    if (candidateMultiplier > inflationMultiplier)
                        inflationMultiplier = candidateMultiplier;
                }

                long scaledAmount = (long)adjustedAmount * safeMultiplier;
                if (scaledAmount > 255L)
                    return false;

                uint quantity = (uint)scaledAmount;
                if (hash == 0u || quantity == 0u)
                    continue;

                if (emitted >= CraftingFastFailValidator.MaxIngredientsPerRecipe)
                    return false;

                if (emitted == 0)
                {
                    hashA = hash;
                    quantityA = quantity;
                }
                else if (emitted == 1)
                {
                    hashB = hash;
                    quantityB = quantity;
                }
                else if (emitted == 2)
                {
                    hashC = hash;
                    quantityC = quantity;
                }
                else
                {
                    hashD = hash;
                    quantityD = quantity;
                }

                emitted++;
            }

            if (emitted == 0)
                return false;

            requirement = CraftingFastFailValidator.BuildRequirement(
                resultHash,
                hashA,
                hashB,
                hashC,
                hashD,
                quantityA,
                quantityB,
                quantityC,
                quantityD,
                CraftingFastFailValidator.ResolveBlueprintUnlockMask(recipe.RequiredScanEntryHash));
            return true;
        }
    }
}
