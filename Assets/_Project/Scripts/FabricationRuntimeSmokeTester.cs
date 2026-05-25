using System;
using System.Threading;
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
            TryGetComponent(out _inventory);
            TryGetComponent(out _scanLogSystem);
        }

        private void Start()
        {
            if (!runOnStart)
                return;

            _ = RunSmokeAsync(destroyCancellationToken);
        }

        private async Awaitable RunSmokeAsync(CancellationToken cancellationToken)
        {
            try
            {
                await DelayRealtimeAsync(startupDelay, cancellationToken);

                if (cancellationToken.IsCancellationRequested || this == null)
                    return;

                if (_inventory == null || _scanLogSystem == null)
                {
                    Hecton8.Core.H8Debug.LogError("[FabricationSmoke] Missing PlayerInventory or ScanLogSystem on Player.");
                    return;
                }

                Fabricator fabricator = FindTargetFabricator();
                if (fabricator == null)
                {
                    Hecton8.Core.H8Debug.LogError($"[FabricationSmoke] Fabricator '{targetFabricatorName}' not found.");
                    return;
                }

                RecipeData recipe = FindTargetRecipe(fabricator);
                if (recipe == null)
                {
                    Hecton8.Core.H8Debug.LogError($"[FabricationSmoke] Recipe '{targetRecipeName}' not found on '{fabricator.name}'.");
                    return;
                }

                UnlockRecipe(recipe);
                SeedIngredients(recipe);

                int beforeCount = recipe.resultItem != null ? _inventory.CountTotal(recipe.resultItem.PersistentHashId) : 0;

                fabricator.Interact(transform);
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: cancellationToken);

                if (cancellationToken.IsCancellationRequested || this == null)
                    return;

                bool menuOpened = HectonFabricatorUI.IsMenuOpen;
                bool craftStarted = fabricator.StartCraft(recipe);

                if (!craftStarted)
                {
                    Hecton8.Core.H8Debug.LogError($"[FabricationSmoke] Failed to start craft '{recipe.recipeName}'.");
                    return;
                }

                float waitTime = Mathf.Max(0.1f, recipe.craftTime + completionPadding);
                await DelayRealtimeAsync(waitTime, cancellationToken);

                if (cancellationToken.IsCancellationRequested || this == null)
                    return;

                int afterCount = recipe.resultItem != null ? _inventory.CountTotal(recipe.resultItem.PersistentHashId) : 0;
                bool crafted = afterCount > beforeCount;

                if (!crafted)
                {
                    Hecton8.Core.H8Debug.LogError(
                        $"[FabricationSmoke] FAIL recipe='{recipe.recipeName}' menuOpened={menuOpened} before={beforeCount} after={afterCount}");
                    return;
                }

                if (verboseLogging)
                {
                    Hecton8.Core.H8Debug.Log(
                        $"[FabricationSmoke] PASS recipe='{recipe.recipeName}' menuOpened={menuOpened} before={beforeCount} after={afterCount}");
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Hecton8.Core.H8Debug.LogException(exception);
            }
        }

        private static async Awaitable DelayRealtimeAsync(float seconds, CancellationToken cancellationToken)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0f, seconds);
            while (!cancellationToken.IsCancellationRequested && Time.realtimeSinceStartup < deadline)
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: cancellationToken);
        }

        private Fabricator FindTargetFabricator()
        {
            return Fabricator.TryGetActiveFabricator(targetFabricatorName, out Fabricator fabricator)
                ? fabricator
                : null;
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

            if (_scanLogSystem.ContainsEntry(recipe.RequiredScanEntryHash))
                return;

            _scanLogSystem.ArchiveEntry(
                recipe.RequiredScanEntryId,
                recipe.recipeName,
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

                int itemHash = cost.item.PersistentHashId;
                int missing = Mathf.Max(0, cost.amount - _inventory.CountTotal(itemHash));
                if (missing <= 0)
                    continue;

                _inventory.TryAddItem(itemHash, missing);
            }
        }
    }
}
