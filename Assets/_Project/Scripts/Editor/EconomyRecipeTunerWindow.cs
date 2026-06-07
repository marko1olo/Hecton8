#if UNITY_EDITOR
using System;
using System.IO;
using Hecton8.Building;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Crafting;
using Hecton8.Inventory;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public sealed class EconomyRecipeTunerWindow : EditorWindow
    {
        private const string NativeMemoryOwner = nameof(EconomyRecipeTunerWindow);
        private const string BinaryScratchLabel = "binaryScratch";
        private const string CsvConstantsLabel = "csvConstants";
        private const string RecipeRoot = "Assets/_Project/Data/Crafting/Recipes";
        private const string CsvDefaultPath = "Assets/_Project/Data/item_encyclopedia.csv";
        private const string H8CrDefaultPath = "Data/Economy/Crafting_Costs.h8bin";
        private const int MaxSoARows = 128;

        private Vector2 _recipeScroll;
        private Vector2 _soaScroll;
        private string _recipeSearch = string.Empty;
        private string _csvPath = CsvDefaultPath;
        private string _h8crPath = H8CrDefaultPath;
        private RecipeData _selectedRecipe;
        private bool _drawSceneGizmo;
        private bool _monitorCsv;
        private long _lastCsvWriteTicks;
        private double _nextCsvPollTime;
        private int _vaultRecipeIndex;

        [MenuItem("Hecton/Debug/Economy Recipe Tuner", priority = 260)]
        public static void Open()
        {
            GetWindow<EconomyRecipeTunerWindow>("Economy Tuner");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= DrawSceneGizmo;
            SceneView.duringSceneGui += DrawSceneGizmo;
            EditorApplication.update -= PollCsvMonitor;
            EditorApplication.update += PollCsvMonitor;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneGizmo;
            EditorApplication.update -= PollCsvMonitor;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("SHINOBU_19 SoA Economy", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                _recipeSearch = EditorGUILayout.TextField("Recipe Search", _recipeSearch);
                if (GUILayout.Button("Refresh", GUILayout.Width(90f)))
                    AssetDatabase.Refresh();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawRecipeList();
                DrawRecipeDetails();
            }

            EditorGUILayout.Space(6f);
            DrawBinaryRecipeImporter();
            EditorGUILayout.Space(6f);
            DrawCsvIngestor();
            EditorGUILayout.Space(6f);
            DrawRawSoA();
        }

        private void DrawRecipeList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(280f)))
            {
                EditorGUILayout.LabelField("Recipes", EditorStyles.boldLabel);
                _recipeScroll = EditorGUILayout.BeginScrollView(_recipeScroll, GUILayout.Height(260f));
                string[] guids = AssetDatabase.FindAssets("t:RecipeData", new[] { RecipeRoot });
                for (int index = 0; index < guids.Length; index++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                    RecipeData recipe = AssetDatabase.LoadAssetAtPath<RecipeData>(path);
                    if (recipe == null)
                        continue;

                    string label = recipe.resultItem != null ? recipe.resultItem.itemName : recipe.recipeName;
                    if (!MatchesSearch(label))
                        continue;

                    GUIStyle style = recipe == _selectedRecipe ? EditorStyles.toolbarButton : EditorStyles.miniButton;
                    if (GUILayout.Button(label, style))
                        _selectedRecipe = recipe;
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawRecipeDetails()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField("Runtime DTO", EditorStyles.boldLabel);
                if (_selectedRecipe == null)
                {
                    EditorGUILayout.HelpBox("Select a recipe asset.", MessageType.Info);
                    return;
                }

                EditorGUILayout.ObjectField("Recipe", _selectedRecipe, typeof(RecipeData), false);
                CraftingRecipeDTO dto = BuildDto(_selectedRecipe);
                CraftingRecipeMaskDTO mask = Shinobu19EconomyLedger.BuildRecipeMask(in dto, 0u);
                EditorGUILayout.LongField("Requirement Mask", unchecked((long)mask.RequirementMask));
                EditorGUILayout.IntField("DTO Bytes", Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<CraftingRecipeDTO>());

                EditorGUILayout.Space(4f);
                DrawIngredientEditor(_selectedRecipe);
                EditorGUILayout.Space(4f);
                DrawVaultRecipeDto(dto);
            }
        }

        private void DrawIngredientEditor(RecipeData recipe)
        {
            if (recipe.ingredients == null)
            {
                EditorGUILayout.HelpBox("Recipe has no ingredient list.", MessageType.Warning);
                return;
            }

            int count = Mathf.Min(recipe.ingredients.Count, 2);
            for (int index = 0; index < count; index++)
            {
                InventoryCost cost = recipe.ingredients[index];
                if (cost == null)
                    continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.ObjectField(cost.item, typeof(UnityEngine.Object), false);
                    int amount = EditorGUILayout.IntField(cost.amount, GUILayout.Width(80f));
                    if (amount != cost.amount)
                    {
                        Undo.RecordObject(recipe, "Tune Recipe Amount");
                        cost.amount = Mathf.Max(1, amount);
                        EditorUtility.SetDirty(recipe);
                    }
                }
            }

            if (GUILayout.Button("Save Recipe Asset", GUILayout.Width(140f)))
            {
                EditorUtility.SetDirty(recipe);
                AssetDatabase.SaveAssets();
            }
        }

        private void DrawCsvIngestor()
        {
            EditorGUILayout.LabelField("CSV Physical Overrides", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _csvPath = EditorGUILayout.TextField(_csvPath);
                if (GUILayout.Button("Apply", GUILayout.Width(80f)))
                    ApplyCsvCold();
            }

            _monitorCsv = EditorGUILayout.Toggle("Monitor CSV", _monitorCsv);
        }

        private void DrawBinaryRecipeImporter()
        {
            EditorGUILayout.LabelField("H8CR Binary Recipe Import", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _h8crPath = EditorGUILayout.TextField(_h8crPath);
                if (GUILayout.Button("Import", GUILayout.Width(80f)))
                    ImportH8CrCold();
            }
        }

        private void ImportH8CrCold()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
            {
                Debug.LogWarning("[SHINOBU_19] DataVault is not registered; H8CR import skipped.");
                return;
            }

            string path = _h8crPath;
            if (!Path.IsPathRooted(path))
                path = Path.Combine(Directory.GetCurrentDirectory(), path);

            if (!File.Exists(path))
            {
                Debug.LogWarning("[SHINOBU_19] H8CR not found: " + path);
                return;
            }

            byte[] managedBytes = File.ReadAllBytes(path);
            int recipeCapacity = managedBytes.Length >= 28
                ? Mathf.Max(256, ReadInt32LittleEndian(managedBytes, 16))
                : 256;
            int ingredientCapacity = managedBytes.Length >= 32
                ? Mathf.Max(1024, ReadInt32LittleEndian(managedBytes, 24))
                : 1024;

            if (!TryResolveRecipeBuffersForEditor(vault, recipeCapacity, out NativeArray<CraftingRecipeDTO> recipes, out NativeArray<CraftingRecipeMaskDTO> masks) ||
                !TryResolveRecipeIngredientBufferForEditor(vault, ingredientCapacity, out NativeArray<CraftingIngredientDTO> ingredients))
            {
                Debug.LogWarning("[SHINOBU_19] Failed to resolve Vault recipe buffers.");
                return;
            }

            NativeArray<byte> binary = default;
            try
            {
                binary = AllocateTrackedArray<byte>(managedBytes.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory, BinaryScratchLabel, NativeAllocationLifetime.Temp);
                binary.CopyFrom(managedBytes);
                ShinobuTransactionStatus status = Shinobu19EconomyLedger.HydrateCraftingRecipesFromH8Cr(
                    binary,
                    recipes,
                    masks,
                    ingredients,
                    out int recipeCount,
                    out int ingredientCount);
                Debug.Log("[SHINOBU_19] H8CR import status=" + status + " recipes=" + recipeCount + " ingredients=" + ingredientCount);
            }
            finally
            {
                DisposeTrackedArray(ref binary);
            }
        }

        private void ApplyCsvCold()
        {
            string path = _csvPath;
            if (!Path.IsPathRooted(path))
                path = Path.Combine(Directory.GetCurrentDirectory(), path);

            if (!File.Exists(path))
            {
                Debug.LogWarning("[SHINOBU_19] CSV not found: " + path);
                return;
            }

            string[] lines = File.ReadAllLines(path);
            int capacity = Mathf.Max(64, lines.Length);
            IDataVault vault = GlobalRegistry.DataVault;
            if (TryResolvePhysicalConstantsForEditor(vault, capacity, out NativeArray<ItemPhysicalConstantsDTO> vaultConstants))
            {
                ApplyCsvLines(lines, vaultConstants, out int accepted, out int rejected);
                Debug.Log("[SHINOBU_19] CSV applied to Vault accepted=" + accepted + " rejected=" + rejected);
                return;
            }

            NativeArray<ItemPhysicalConstantsDTO> constants = default;
            try
            {
                constants = AllocateTrackedArray<ItemPhysicalConstantsDTO>(capacity, Allocator.Temp, NativeArrayOptions.ClearMemory, CsvConstantsLabel, NativeAllocationLifetime.Temp);
                ApplyCsvLines(lines, constants, out int accepted, out int rejected);
                Debug.Log("[SHINOBU_19] CSV applied to temp fallback accepted=" + accepted + " rejected=" + rejected);
            }
            finally
            {
                DisposeTrackedArray(ref constants);
            }
        }

        private static NativeArray<T> AllocateTrackedArray<T>(int length, Allocator allocator, NativeArrayOptions options, string label, NativeAllocationLifetime lifetime) where T : struct
        {
            NativeArray<T> array = new NativeArray<T>(length, allocator, options);
            if (!array.IsCreated)
                throw new InvalidOperationException("[EconomyRecipeTunerWindow] NativeArray allocation failed for " + label + ".");

            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, lifetime);
                if (sentinelId <= 0)
                    throw new InvalidOperationException("[EconomyRecipeTunerWindow] NativeMemorySentinel rejected NativeArray registration for " + label + ".");
            }
            catch
            {
                array.Dispose();
                throw;
            }

            return array;
        }

        private static void DisposeTrackedArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            try
            {
                NativeMemorySentinel.UnregisterNativeArray(array);
            }
            finally
            {
                array.Dispose();
                array = default;
            }
        }

        private static void ApplyCsvLines(string[] lines, NativeArray<ItemPhysicalConstantsDTO> constants, out int accepted, out int rejected)
        {
            accepted = 0;
            rejected = 0;
            for (int index = 0; index < lines.Length; index++)
            {
                if (Shinobu19EconomyLedger.TryApplyCsvOverrideLine(lines[index].AsSpan(), constants))
                    accepted++;
                else
                    rejected++;
            }
        }

        private void PollCsvMonitor()
        {
            if (!_monitorCsv || EditorApplication.timeSinceStartup < _nextCsvPollTime)
                return;

            _nextCsvPollTime = EditorApplication.timeSinceStartup + 1.0d;
            string path = _csvPath;
            if (!Path.IsPathRooted(path))
                path = Path.Combine(Directory.GetCurrentDirectory(), path);

            if (!File.Exists(path))
                return;

            long ticks = File.GetLastWriteTimeUtc(path).Ticks;
            if (ticks == _lastCsvWriteTicks)
                return;

            _lastCsvWriteTicks = ticks;
            ApplyCsvCold();
            Repaint();
        }

        private void DrawVaultRecipeDto(CraftingRecipeDTO selectedDto)
        {
            EditorGUILayout.LabelField("Unmanaged Recipe DTO Buffer", EditorStyles.boldLabel);
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
            {
                EditorGUILayout.HelpBox("GlobalDataVault is not registered.", MessageType.Info);
                return;
            }

            _vaultRecipeIndex = EditorGUILayout.IntSlider("Vault Index", _vaultRecipeIndex, 0, 255);
            if (!TryReadExistingVaultView(vault, BufferID.ShinobuRecipeDtos, out NativeArray<CraftingRecipeDTO> recipes) ||
                !TryReadExistingVaultView(vault, BufferID.ShinobuRecipeMasks, out NativeArray<CraftingRecipeMaskDTO> masks) ||
                recipes.Length == 0 ||
                masks.Length == 0)
            {
                if (GUILayout.Button("Create 256 DTO Vault Buffers", GUILayout.Width(220f)))
                    TryResolveRecipeBuffersForEditor(vault, 256, out _, out _);
                return;
            }

            int index = Mathf.Clamp(_vaultRecipeIndex, 0, Mathf.Min(recipes.Length, masks.Length) - 1);
            CraftingRecipeDTO dto = recipes[index];
            EditorGUILayout.LabelField("Current", FormatRecipeDto(dto));

            bool hasIngredientRows = false;
            int ingredientCursor = 0;
            int ingredientCount = 0;
            if (TryReadExistingVaultView(vault, BufferID.ShinobuRecipeIngredients, out NativeArray<CraftingIngredientDTO> ingredients))
            {
                hasIngredientRows = TryResolveIngredientWindow(in dto, ingredients, out ingredientCursor, out ingredientCount);
            }

            if (hasIngredientRows)
            {
                DrawIngredientRows(vault, ingredients, ingredientCursor, ingredientCount, index, ref dto);
            }
            else
            {
                int quantityA = EditorGUILayout.IntField("Component A Qty", dto.QuantityA);
                int quantityB = EditorGUILayout.IntField("Component B Qty", dto.QuantityB);
                if (quantityA != dto.QuantityA || quantityB != dto.QuantityB)
                {
                    dto.QuantityA = dto.ComponentA != 0u ? Mathf.Max(1, quantityA) : 0;
                    dto.QuantityB = dto.ComponentB != 0u ? Mathf.Max(1, quantityB) : 0;
                    TryWriteRecipeAndMask(vault, index, in dto, Shinobu19EconomyLedger.BuildRecipeMask(in dto, (uint)index));
                }
            }

            if (GUILayout.Button("Write Selected Recipe To Vault", GUILayout.Width(220f)))
            {
                TryWriteRecipeAndMask(vault, index, in selectedDto, Shinobu19EconomyLedger.BuildRecipeMask(in selectedDto, (uint)index));
            }
        }

        private static bool DrawIngredientRows(
            IDataVault vault,
            NativeArray<CraftingIngredientDTO> ingredients,
            int ingredientCursor,
            int ingredientCount,
            int recipeIndex,
            ref CraftingRecipeDTO dto)
        {
            EditorGUILayout.LabelField("H8CR Ingredient Rows", EditorStyles.miniBoldLabel);
            bool dirty = false;
            for (int offset = 0; offset < ingredientCount; offset++)
            {
                int rowIndex = ingredientCursor + offset;
                CraftingIngredientDTO ingredient = ingredients[rowIndex];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        offset + " | 0x" + ingredient.ItemHash.ToString("X8"),
                        GUILayout.MinWidth(150f));
                    int quantity = EditorGUILayout.IntField(ingredient.Quantity, GUILayout.Width(90f));
                    if (quantity == ingredient.Quantity)
                        continue;

                    int clamped = Mathf.Clamp(quantity, 1, ushort.MaxValue);
                    ingredient.Quantity = unchecked((ushort)clamped);
                    ulong totalMass = (ulong)ingredient.UnitMassGrams * (uint)ingredient.Quantity;
                    ingredient.TotalMassGrams = totalMass > uint.MaxValue ? uint.MaxValue : (uint)totalMass;
                    CraftingRecipeDTO updatedDto = dto;
                    if (offset == 0)
                        updatedDto.QuantityA = ingredient.Quantity;
                    else if (offset == 1)
                        updatedDto.QuantityB = ingredient.Quantity;

                    if (TryWriteIngredientRecipeAndMask(vault, rowIndex, in ingredient, recipeIndex, in updatedDto))
                    {
                        dto = updatedDto;
                        dirty = true;
                    }
                }
            }

            return dirty;
        }

        private static bool TryReadExistingVaultView<T>(IDataVault vault, BufferID bufferId, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        private static bool TryResolveRecipeBuffersForEditor(
            IDataVault vault,
            int recipeCapacity,
            out NativeArray<CraftingRecipeDTO> recipes,
            out NativeArray<CraftingRecipeMaskDTO> masks)
        {
            recipes = default;
            masks = default;
            if (vault == null || recipeCapacity <= 0)
                return false;

            if (!OpenOrAcquireEconomyVaultBufferForEditor(
                    vault,
                    BufferID.ShinobuRecipeDtos,
                    recipeCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out recipes) ||
                !OpenOrAcquireEconomyVaultBufferForEditor(
                    vault,
                    BufferID.ShinobuRecipeMasks,
                    recipeCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out masks))
            {
                recipes = default;
                masks = default;
                return false;
            }

            return recipes.IsCreated && masks.IsCreated && recipes.Length >= recipeCapacity && masks.Length >= recipeCapacity;
        }

        private static bool TryResolveRecipeIngredientBufferForEditor(
            IDataVault vault,
            int ingredientCapacity,
            out NativeArray<CraftingIngredientDTO> ingredients)
        {
            return OpenOrAcquireEconomyVaultBufferForEditor(
                vault,
                BufferID.ShinobuRecipeIngredients,
                ingredientCapacity,
                NativeArrayOptions.UninitializedMemory,
                out ingredients);
        }

        private static bool TryResolvePhysicalConstantsForEditor(
            IDataVault vault,
            int itemCapacity,
            out NativeArray<ItemPhysicalConstantsDTO> constants)
        {
            return OpenOrAcquireEconomyVaultBufferForEditor(
                vault,
                BufferID.ShinobuPhysicalConstants,
                itemCapacity,
                NativeArrayOptions.UninitializedMemory,
                out constants);
        }

        private static bool OpenOrAcquireEconomyVaultBufferForEditor<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            VaultGenerationHandle<T> handle;
            if (vault.IsAllocationLocked)
            {
                if (!vault.TryGetGenerationHandle(bufferId, out handle))
                    return false;

                return TryOpenEconomyVaultBufferForEditor(vault, in handle, bufferId, requiredLength, out buffer);
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.GameplayPlayer,
                options);
            return TryOpenEconomyVaultBufferForEditor(vault, in handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryOpenEconomyVaultBufferForEditor<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                handle.BufferID != (uint)bufferId ||
                handle.SystemID != (uint)SystemID.GameplayPlayer ||
                handle.Generation == 0u ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool TryAcquireEditorWriteView<T>(
            IDataVault vault,
            BufferID bufferId,
            out VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer)
            where T : struct
        {
            handle = default;
            buffer = default;
            if (vault == null ||
                !vault.TryGetGenerationHandle(bufferId, out handle) ||
                !vault.TryAcquireWriteLock(in handle, SystemID.CoreDiagnostics, out buffer))
            {
                return false;
            }

            if (buffer.IsCreated)
                return true;

            vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            buffer = default;
            return false;
        }

        private static bool TryWriteRecipeAndMask(
            IDataVault vault,
            int recipeIndex,
            in CraftingRecipeDTO recipe,
            in CraftingRecipeMaskDTO mask)
        {
            if (!TryAcquireEditorWriteView(vault, BufferID.ShinobuRecipeDtos, out VaultGenerationHandle<CraftingRecipeDTO> recipeHandle, out NativeArray<CraftingRecipeDTO> recipes))
                return false;

            bool maskLocked = false;
            VaultGenerationHandle<CraftingRecipeMaskDTO> maskHandle = default;
            try
            {
                if ((uint)recipeIndex >= (uint)recipes.Length)
                    return false;

                if (!TryAcquireEditorWriteView(vault, BufferID.ShinobuRecipeMasks, out maskHandle, out NativeArray<CraftingRecipeMaskDTO> masks))
                    return false;

                maskLocked = true;
                if ((uint)recipeIndex >= (uint)masks.Length)
                    return false;

                recipes[recipeIndex] = recipe;
                masks[recipeIndex] = mask;
                return true;
            }
            finally
            {
                if (maskLocked)
                    vault.ReleaseWriteLock(in maskHandle, SystemID.CoreDiagnostics);
                vault.ReleaseWriteLock(in recipeHandle, SystemID.CoreDiagnostics);
            }
        }

        private static bool TryWriteIngredientRecipeAndMask(
            IDataVault vault,
            int ingredientIndex,
            in CraftingIngredientDTO ingredient,
            int recipeIndex,
            in CraftingRecipeDTO recipe)
        {
            if (!TryAcquireEditorWriteView(vault, BufferID.ShinobuRecipeIngredients, out VaultGenerationHandle<CraftingIngredientDTO> ingredientHandle, out NativeArray<CraftingIngredientDTO> ingredients))
                return false;

            bool recipeLocked = false;
            bool maskLocked = false;
            VaultGenerationHandle<CraftingRecipeDTO> recipeHandle = default;
            VaultGenerationHandle<CraftingRecipeMaskDTO> maskHandle = default;
            try
            {
                if ((uint)ingredientIndex >= (uint)ingredients.Length)
                    return false;

                if (!TryAcquireEditorWriteView(vault, BufferID.ShinobuRecipeDtos, out recipeHandle, out NativeArray<CraftingRecipeDTO> recipes))
                    return false;

                recipeLocked = true;
                if ((uint)recipeIndex >= (uint)recipes.Length)
                    return false;

                if (!TryAcquireEditorWriteView(vault, BufferID.ShinobuRecipeMasks, out maskHandle, out NativeArray<CraftingRecipeMaskDTO> masks))
                    return false;

                maskLocked = true;
                if ((uint)recipeIndex >= (uint)masks.Length)
                    return false;

                ingredients[ingredientIndex] = ingredient;
                recipes[recipeIndex] = recipe;
                masks[recipeIndex] = Shinobu19EconomyLedger.BuildRecipeMask(in recipe, (uint)recipeIndex, ingredients);
                return true;
            }
            finally
            {
                if (maskLocked)
                    vault.ReleaseWriteLock(in maskHandle, SystemID.CoreDiagnostics);
                if (recipeLocked)
                    vault.ReleaseWriteLock(in recipeHandle, SystemID.CoreDiagnostics);
                vault.ReleaseWriteLock(in ingredientHandle, SystemID.CoreDiagnostics);
            }
        }

        private static bool TryResolveIngredientWindow(
            in CraftingRecipeDTO dto,
            NativeArray<CraftingIngredientDTO> ingredients,
            out int ingredientCursor,
            out int ingredientCount)
        {
            ingredientCursor = unchecked((int)dto.Reserved1);
            ingredientCount = unchecked((int)dto.Reserved2);
            return ingredients.IsCreated &&
                   ingredientCursor >= 0 &&
                   ingredientCount > 0 &&
                   ingredientCursor <= ingredients.Length - ingredientCount;
        }

        private void DrawRawSoA()
        {
            EditorGUILayout.LabelField("Live Raw SoA", EditorStyles.boldLabel);
            _drawSceneGizmo = EditorGUILayout.Toggle("Scene Gizmo", _drawSceneGizmo);

            PlayerInventory inventory = ResolveInventory();
            if (inventory == null ||
                !inventory.TryGetInventorySoA(
                    out NativeArray<uint>.ReadOnly itemHashes,
                    out NativeArray<ushort>.ReadOnly itemCounts,
                    out NativeArray<float>.ReadOnly itemCondition,
                    out ulong currentInventoryMask))
            {
                EditorGUILayout.HelpBox("No live PlayerInventory SoA available.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Inventory Mask", "0x" + currentInventoryMask.ToString("X16"));
            int rowCount = Mathf.Min(MaxSoARows, Mathf.Min(itemHashes.Length, Mathf.Min(itemCounts.Length, itemCondition.Length)));
            _soaScroll = EditorGUILayout.BeginScrollView(_soaScroll, GUILayout.Height(230f));
            EditorGUILayout.LabelField("Index | Hash | Quantity | Durability01", EditorStyles.miniBoldLabel);
            for (int index = 0; index < rowCount; index++)
            {
                uint hash = itemHashes[index];
                ushort count = itemCounts[index];
                if (hash == 0u && count == 0)
                    continue;

                EditorGUILayout.LabelField(index + " | 0x" + hash.ToString("X8") + " | " + count + " | " + itemCondition[index].ToString("0.000"));
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSceneGizmo(SceneView sceneView)
        {
            if (!_drawSceneGizmo)
                return;

            PlayerInventory inventory = ResolveInventory();
            if (inventory == null ||
                !inventory.TryGetInventorySoA(
                    out NativeArray<uint>.ReadOnly hashes,
                    out NativeArray<ushort>.ReadOnly counts,
                    out _,
                    out ulong mask))
            {
                return;
            }

            int occupied = 0;
            int capacity = Mathf.Min(hashes.Length, counts.Length);
            for (int index = 0; index < capacity; index++)
            {
                if (hashes[index] != 0u && counts[index] > 0)
                    occupied++;
            }

            Handles.Label(
                inventory.transform.position + Vector3.up * 1.5f,
                "SHINOBU_19 SoA slots " + occupied + "/" + capacity + "\nmask 0x" + mask.ToString("X16"));
        }

        private bool MatchesSearch(string label)
        {
            return string.IsNullOrEmpty(_recipeSearch) ||
                   (!string.IsNullOrEmpty(label) && label.IndexOf(_recipeSearch, System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static CraftingRecipeDTO BuildDto(RecipeData recipe)
        {
            uint resultHash = recipe.resultItem != null ? unchecked((uint)recipe.resultItem.PersistentHashId) : 0u;
            uint componentA = 0u;
            int quantityA = 0;
            uint componentB = 0u;
            int quantityB = 0;

            if (recipe.ingredients != null)
            {
                int outputIndex = 0;
                for (int index = 0; index < recipe.ingredients.Count && outputIndex < 2; index++)
                {
                    InventoryCost cost = recipe.ingredients[index];
                    if (cost == null || cost.item == null || cost.amount <= 0)
                        continue;

                    if (outputIndex == 0)
                    {
                        componentA = unchecked((uint)cost.item.PersistentHashId);
                        quantityA = cost.amount;
                    }
                    else
                    {
                        componentB = unchecked((uint)cost.item.PersistentHashId);
                        quantityB = cost.amount;
                    }

                    outputIndex++;
                }
            }

            return Shinobu19EconomyLedger.BuildRecipe(resultHash, componentA, quantityA, componentB, quantityB);
        }

        private static PlayerInventory ResolveInventory()
        {
            return UnityEngine.Object.FindAnyObjectByType<PlayerInventory>();
        }

        private static string FormatRecipeDto(CraftingRecipeDTO dto)
        {
            return "Result 0x" + dto.ResultHash.ToString("X8") +
                   " | A 0x" + dto.ComponentA.ToString("X8") + " x" + dto.QuantityA +
                   " | B 0x" + dto.ComponentB.ToString("X8") + " x" + dto.QuantityB;
        }

        private static int ReadInt32LittleEndian(byte[] bytes, int offset)
        {
            if (bytes == null || offset < 0 || offset > bytes.Length - 4)
                return 0;

            return bytes[offset] |
                   (bytes[offset + 1] << 8) |
                   (bytes[offset + 2] << 16) |
                   (bytes[offset + 3] << 24);
        }
    }
}
#endif
