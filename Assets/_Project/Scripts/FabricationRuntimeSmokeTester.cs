using System.Collections;
using Hecton8.Building;
using Hecton8.Crafting;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.Debugging
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Debug/Fabrication Runtime Smoke Tester")]
    public sealed class FabricationRuntimeSmokeTester : MonoBehaviour
    {
        [SerializeField] private bool runOnStart;
        [SerializeField] private bool verboseLogging;
        [SerializeField] private string targetFabricatorName = "Forward_Fabricator";
        [SerializeField] private string targetRecipeName = "Dive Flashlight";
        [SerializeField] private float startupDelay = 0.75f;
        [SerializeField] private float completionPadding = 0.5f;

        private PlayerInventory _inventory;
        private ScanLogSystem _scanLogSystem;

        private void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
            _scanLogSystem = GetComponent<ScanLogSystem>();
        }

        private void Start()
        {
            if (!runOnStart)
                return;

            StartCoroutine(RunSmoke());
        }

        private IEnumerator RunSmoke()
        {
            yield return new WaitForSeconds(startupDelay);

            if (_inventory == null || _scanLogSystem == null)
            {
                Debug.LogError("[FabricationSmoke] Missing PlayerInventory or ScanLogSystem on Player.");
                yield break;
            }

            Fabricator fabricator = FindTargetFabricator();
            if (fabricator == null)
            {
                Debug.LogError($"[FabricationSmoke] Fabricator '{targetFabricatorName}' not found.");
                yield break;
            }

            RecipeData recipe = FindTargetRecipe(fabricator);
            if (recipe == null)
            {
                Debug.LogError($"[FabricationSmoke] Recipe '{targetRecipeName}' not found on '{fabricator.name}'.");
                yield break;
            }

            UnlockRecipe(recipe);
            SeedIngredients(recipe);

            int beforeCount = recipe.resultItem != null ? _inventory.CountTotal(Hecton.Localization.LocHash.Compute(recipe.resultItem.PersistentId)) : 0;

            fabricator.Interact(transform);
            yield return null;

            bool menuOpened = HectonFabricatorUI.IsMenuOpen;
            bool craftStarted = fabricator.StartCraft(recipe);

            if (!craftStarted)
            {
                Debug.LogError($"[FabricationSmoke] Failed to start craft '{recipe.recipeName}'.");
                yield break;
            }

            float waitTime = Mathf.Max(0.1f, recipe.craftTime + completionPadding);
            yield return new WaitForSeconds(waitTime);

            int afterCount = recipe.resultItem != null ? _inventory.CountTotal(Hecton.Localization.LocHash.Compute(recipe.resultItem.PersistentId)) : 0;
            bool crafted = afterCount > beforeCount;

            if (!crafted)
            {
                Debug.LogError(
                    $"[FabricationSmoke] FAIL recipe='{recipe.recipeName}' menuOpened={menuOpened} before={beforeCount} after={afterCount}");
                yield break;
            }

            if (verboseLogging)
            {
                Debug.Log(
                    $"[FabricationSmoke] PASS recipe='{recipe.recipeName}' menuOpened={menuOpened} before={beforeCount} after={afterCount}");
            }
        }

        private Fabricator FindTargetFabricator()
        {
            Fabricator[] all = FindObjectsByType<Fabricator>();
            for (int i = 0; i < all.Length; i++)
            {
                Fabricator fabricator = all[i];
                if (fabricator == null)
                    continue;

                if (string.IsNullOrWhiteSpace(targetFabricatorName) || fabricator.name == targetFabricatorName)
                    return fabricator;
            }

            return null;
        }

        private RecipeData FindTargetRecipe(Fabricator fabricator)
        {
            if (fabricator == null)
                return null;

            var recipes = fabricator.AvailableRecipes;
            for (int i = 0; i < recipes.Count; i++)
            {
                RecipeData recipe = recipes[i];
                if (recipe == null)
                    continue;

                if (string.IsNullOrWhiteSpace(targetRecipeName) || recipe.recipeName == targetRecipeName)
                    return recipe;
            }

            for (int i = 0; i < fabricator.AvailableRecipes.Count; i++)
            {
                RecipeData recipe = fabricator.AvailableRecipes[i];
                if (recipe != null)
                    return recipe;
            }

            return null;
        }

        private void UnlockRecipe(RecipeData recipe)
        {
            if (recipe == null || !recipe.RequiresScanUnlock || _scanLogSystem == null)
                return;

            if (_scanLogSystem.ContainsEntry(recipe.RequiredScanEntryId))
                return;

            _scanLogSystem.ArchiveEntry(
                recipe.RequiredScanEntryId,
                recipe.recipeName.ToUpperInvariant(),
                "Blueprint",
                "Smoke unlock for fabrication validation.",
                markRecent: false);
        }

        private void SeedIngredients(RecipeData recipe)
        {
            if (recipe == null || recipe.ingredients == null || _inventory == null)
                return;

            for (int i = 0; i < recipe.ingredients.Count; i++)
            {
                InventoryCost cost = recipe.ingredients[i];
                if (cost == null || cost.item == null || cost.amount <= 0)
                    continue;

                int missing = Mathf.Max(0, cost.amount - _inventory.CountTotal(Hecton.Localization.LocHash.Compute(cost.item.PersistentId)));
                if (missing <= 0)
                    continue;

                _inventory.TryAddItem(Hecton.Localization.LocHash.Compute(cost.item.PersistentId), missing);
            }
        }
    }
}
