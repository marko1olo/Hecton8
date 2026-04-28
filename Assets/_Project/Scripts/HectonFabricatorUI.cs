using System;
using System.Collections.Generic;
using Hecton8.Bootstrap;
using Hecton8.Building;
using Hecton8.Core;
using Hecton8.Crafting;
using Hecton8.Input;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton.Localization;
using TMPro;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    public sealed class HectonFabricatorUI : MonoBehaviour, ITickable, IUpdatable, IUIService
    {
        private const string HologramShaderPath = "Assets/_Project/Art/Shaders/Hecton_FabricatorHologram.shader";
        private const int MaxVisibleHologramInstances = 16;
        private const int MaxVisibleRecipeEntries = 8;
        private const int RecipeLabelBufferCapacity = 128;
        private const float RecipePointerDistanceMeters = 6f;
        private const float HologramBaseDistanceMeters = 1f;
        private const float HologramSpinDegreesPerSecond = 36f;

        private struct RecipeListEntry
        {
            public Transform Root;
            public TextMeshPro Label;
            public BoxCollider Collider;
            public WorldSpaceTMPSharpnessController Sharpness;
            public int RecipeIndex;
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("References")]
        [SerializeField] private Camera hudCamera;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private Shader hologramShader;
        [SerializeField] private Mesh[] hologramProxyMeshes = Array.Empty<Mesh>();

        [Header("Runtime Compatibility")]
        [SerializeField] private bool useCullingMasks;

        [Header("Hologram Layout")]
        [SerializeField, Min(0.1f)] private float hologramHeight = 1.35f;
        [SerializeField, Min(0.01f)] private float hologramCellSize = 0.085f;
        [SerializeField, Min(0.01f)] private float hologramSpacing = 0.11f;
        [SerializeField, Min(0f)] private float hologramBobAmplitude = 0.035f;
        [SerializeField, Min(0f)] private float hologramBobFrequency = 1.4f;
        [SerializeField, Min(0f)] private float hologramYawBias = 14f;
        [SerializeField] private Color hologramColor = new Color(0.08f, 0.88f, 1f, 0.42f);

        [Header("Diegetic Recipe List")]
        [SerializeField, Min(0.1f)] private float recipeListHeight = 1.68f;
        [SerializeField, Min(0f)] private float recipeListForwardOffset = 0.42f;
        [SerializeField, Min(0.05f)] private float recipeEntrySpacing = 0.12f;
        [SerializeField, Min(0.001f)] private float recipeEntryScale = 0.0024f;
        [SerializeField] private Color recipeIdleColor = new Color(0.42f, 0.9f, 1f, 0.85f);
        [SerializeField] private Color recipeSelectedColor = new Color(1f, 0.97f, 0.72f, 1f);
        [SerializeField] private Color recipeUnavailableColor = new Color(1f, 0.52f, 0.32f, 0.92f);

        [Header("Diagnostics")]
        [SerializeField] private bool _debugIsOpen;
        [SerializeField] private bool _debugIsCrafting;
        [SerializeField] private int _debugSelectedIndex;
        [SerializeField] private int _debugVisibleInstanceCount;
        [SerializeField] private int _debugHoveredRecipeIndex = -1;

        // COLD ALLOC: List<RecipeData>[32] — filtered fabricator recipe cache — owner: HectonFabricatorUI
        private readonly List<RecipeData> _filteredRecipes = new List<RecipeData>(32);
        // COLD ALLOC: Matrix4x4[16] — instanced hologram draw buffer mirror — owner: HectonFabricatorUI
        private readonly Matrix4x4[] _hologramMatrixBuffer = new Matrix4x4[MaxVisibleHologramInstances];
        // COLD ALLOC: Matrix4x4[1] — selected recipe hologram draw buffer — owner: HectonFabricatorUI
        private readonly Matrix4x4[] _selectedRecipeHologramBuffer = new Matrix4x4[1];
        private readonly RecipeListEntry[] _recipeEntries = new RecipeListEntry[MaxVisibleRecipeEntries];
        private readonly char[] _recipeLabelBuffer = new char[RecipeLabelBufferCapacity];

        private NativeArray<Matrix4x4> _hologramMatrices;
        private NativeArray<RaycastCommand> _recipePointerCommands;
        private NativeArray<RaycastHit> _recipePointerHits;
        private Material _runtimeHologramMaterial;
        private Mesh _runtimeHologramMesh;
        private Fabricator _currentFabricator;
        private IReadOnlyList<RecipeData> _allRecipes;
        private IReadOnlyList<RecipeData> _recipes;
        private Transform _recipeListRoot;
        private FabricationGroup _selectedGroup = FabricationGroup.Unspecified;
        private int _selectedIndex;
        private int _hoveredRecipeIndex = -1;
        private int _lastRecipeVisualVersion = int.MinValue;
        private bool _isOpen;
        private bool _isCrafting;
        private float _craftProgress;
        private bool _tickRegistered;
        private bool _recipePointerScheduled;
        private bool _ownsGlobalUiSlot;
        private JobHandle _recipePointerHandle;

        public static bool IsMenuOpen { get; private set; }
        public bool IsInitialized => isActiveAndEnabled && _hologramMatrices.IsCreated && _recipeListRoot != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            IsMenuOpen = false;
        }

        private void Awake()
        {
            ResolveRuntimeReferences();

            EnsureHologramBuffers();
            EnsureHologramResources();
            EnsureRecipePointerBuffers();
            EnsureRecipeListPool();
        }

        private void OnEnable()
        {
            TryRegisterUiService();
            InputManager inputManager = InputManager.Instance;
            if (inputManager != null)
            {
                inputManager.OnNavigate += HandleNavigateInput;
                inputManager.OnSubmit += HandleSubmitInput;
                inputManager.OnCancel += HandleCancelInput;
            }

            CraftingEvents.OnFabricatorOpened += HandleFabricatorOpened;
            CraftingEvents.OnFabricatorClosed += HandleFabricatorClosed;
            CraftingEvents.OnCraftStarted += HandleCraftStarted;
            CraftingEvents.OnCraftProgressUpdated += HandleCraftProgress;
            CraftingEvents.OnCraftCompleted += HandleCraftCompleted;
            CraftingEvents.OnCraftCancelled += HandleCraftCancelled;
        }

        private void OnDisable()
        {
            UnregisterUiService();
            InputManager inputManager = InputManager.Instance;
            if (inputManager != null)
            {
                inputManager.OnNavigate -= HandleNavigateInput;
                inputManager.OnSubmit -= HandleSubmitInput;
                inputManager.OnCancel -= HandleCancelInput;
            }

            CraftingEvents.OnFabricatorOpened -= HandleFabricatorOpened;
            CraftingEvents.OnFabricatorClosed -= HandleFabricatorClosed;
            CraftingEvents.OnCraftStarted -= HandleCraftStarted;
            CraftingEvents.OnCraftProgressUpdated -= HandleCraftProgress;
            CraftingEvents.OnCraftCompleted -= HandleCraftCompleted;
            CraftingEvents.OnCraftCancelled -= HandleCraftCancelled;

            UnregisterTick();

            if (_isOpen)
                CloseMenu();
        }

        private void OnDestroy()
        {
            UnregisterUiService();
            if (_hologramMatrices.IsCreated)
            {
                _hologramMatrices.Dispose();
                _hologramMatrices = default;
            }

            if (_recipePointerScheduled)
            {
                _recipePointerHandle.Complete();
                _recipePointerScheduled = false;
            }

            if (_recipePointerCommands.IsCreated)
            {
                _recipePointerCommands.Dispose();
                _recipePointerCommands = default;
            }

            if (_recipePointerHits.IsCreated)
            {
                _recipePointerHits.Dispose();
                _recipePointerHits = default;
            }

            if (_runtimeHologramMaterial != null)
            {
                Destroy(_runtimeHologramMaterial);
                _runtimeHologramMaterial = null;
            }

            if (_runtimeHologramMesh != null)
            {
                Destroy(_runtimeHologramMesh);
                _runtimeHologramMesh = null;
            }

            if (_recipeListRoot != null)
            {
                Destroy(_recipeListRoot.gameObject);
                _recipeListRoot = null;
            }
        }

        private void TryRegisterUiService()
        {
            if (!Application.isPlaying || _ownsGlobalUiSlot)
                return;

            IUIService current = GlobalRegistry.UI;
            if (current != null && !ReferenceEquals(current, this))
                return;

            GlobalRegistry.RegisterUIService(this);
            _ownsGlobalUiSlot = true;
        }

        private void UnregisterUiService()
        {
            if (!_ownsGlobalUiSlot)
                return;

            GlobalRegistry.UnregisterUIService(this);
            _ownsGlobalUiSlot = false;
        }

        public void Tick(float deltaTime)
        {
            if (_isOpen && _currentFabricator == null)
            {
                CloseMenu();
                return;
            }

            ResolveRuntimeReferences();
            UpdateRecipePointerSelection();
            UpdateRecipeListPose();
            RefreshRecipeListIfDirty();
            RenderActiveRecipeHologram(deltaTime);
            ScheduleRecipePointerSelection();
            UpdateDiagnostics();
        }

        private void HandleFabricatorOpened(Fabricator fabricator)
        {
            if (fabricator == null || fabricator.AvailableRecipes == null)
                return;

            _currentFabricator = fabricator;
            _allRecipes = fabricator.AvailableRecipes;
            _selectedGroup = FabricationGroup.Unspecified;
            _selectedIndex = 0;
            _craftProgress = 0f;
            _isCrafting = fabricator.IsCrafting;
            _isOpen = true;
            IsMenuOpen = true;

            RebuildVisibleRecipes();
            EnsureRecipeListPool();
            SetRecipeListVisible(true);
            RegisterTick();

            InputManager inputManager = InputManager.Instance;
            if (inputManager != null)
                inputManager.SwitchToUIInput();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;
            UpdateDiagnostics();
        }

        private void HandleFabricatorClosed()
        {
            CloseMenu();
        }

        private void HandleCraftStarted(RecipeData recipe)
        {
            if (_currentFabricator == null || recipe == null)
                return;

            _isCrafting = true;
            _craftProgress = 0f;

            if (_recipes != null)
            {
                for (int i = 0; i < _recipes.Count; i++)
                {
                    if (ReferenceEquals(_recipes[i], recipe))
                    {
                        _selectedIndex = i;
                        break;
                    }
                }
            }
        }

        private void HandleCraftProgress(float progress)
        {
            _craftProgress = Mathf.Clamp01(progress);
        }

        private void HandleCraftCompleted(ItemData resultItem)
        {
            _isCrafting = false;
            _craftProgress = 0f;
        }

        private void HandleCraftCancelled()
        {
            _isCrafting = false;
            _craftProgress = 0f;
        }

        private void HandleNavigateInput(Vector2 direction)
        {
            if (!_isOpen || _isCrafting || _recipes == null || _recipes.Count == 0)
                return;

            if (Mathf.Abs(direction.x) >= 0.5f && Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                CycleGroup(direction.x > 0f ? 1 : -1);
                return;
            }

            if (Mathf.Abs(direction.y) < 0.5f)
                return;

            SetSelectedIndex(_selectedIndex + (direction.y > 0f ? -1 : 1));
        }

        private void HandleSubmitInput()
        {
            if (!_isOpen || _isCrafting || _currentFabricator == null || _recipes == null || _recipes.Count == 0)
                return;

            if (_selectedIndex < 0 || _selectedIndex >= _recipes.Count)
                return;

            RecipeData recipe = _recipes[_selectedIndex];
            if (recipe == null)
                return;

            _currentFabricator.StartCraft(recipe);
        }

        private void HandleCancelInput()
        {
            if (!_isOpen)
                return;

            if (_isCrafting)
            {
                if (_currentFabricator != null)
                    _currentFabricator.CancelCraft();
                else
                    CloseMenu();

                return;
            }

            CloseMenu();
        }

        private void CloseMenu()
        {
            if (_recipePointerScheduled)
            {
                _recipePointerHandle.Complete();
                _recipePointerScheduled = false;
            }

            _isOpen = false;
            IsMenuOpen = false;
            _isCrafting = false;
            _craftProgress = 0f;
            _currentFabricator = null;
            _allRecipes = null;
            _recipes = null;
            _filteredRecipes.Clear();
            _debugVisibleInstanceCount = 0;
            _hoveredRecipeIndex = -1;
            _lastRecipeVisualVersion = int.MinValue;
            SetRecipeListVisible(false);

            UnregisterTick();

            InputManager inputManager = InputManager.Instance;
            if (inputManager != null)
                inputManager.SwitchToPlayerInput();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            UpdateDiagnostics();
        }

        private void RegisterTick()
        {
            if (_tickRegistered)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _tickRegistered = true;
        }

        private void UnregisterTick()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _tickRegistered = false;
        }

        private void SetSelectedIndex(int nextIndex)
        {
            if (_recipes == null || _recipes.Count == 0)
                return;

            _selectedIndex = Mathf.Clamp(nextIndex, 0, _recipes.Count - 1);
            UpdateDiagnostics();
        }

        private void CycleGroup(int direction)
        {
            FabricationGroup[] groups =
            {
                FabricationGroup.Unspecified,
                FabricationGroup.Materials,
                FabricationGroup.Components,
                FabricationGroup.Tools,
                FabricationGroup.Suit,
                FabricationGroup.Construction,
                FabricationGroup.Power
            };

            int currentIndex = 0;
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] == _selectedGroup)
                {
                    currentIndex = i;
                    break;
                }
            }

            for (int step = 1; step <= groups.Length; step++)
            {
                int nextIndex = (currentIndex + (step * direction) + groups.Length) % groups.Length;
                FabricationGroup candidate = groups[nextIndex];
                if (!HasRecipesInGroup(candidate))
                    continue;

                _selectedGroup = candidate;
                _selectedIndex = 0;
                RebuildVisibleRecipes();
                return;
            }
        }

        private bool HasRecipesInGroup(FabricationGroup group)
        {
            if (_allRecipes == null)
                return false;

            for (int i = 0; i < _allRecipes.Count; i++)
            {
                RecipeData recipe = _allRecipes[i];
                if (recipe == null)
                    continue;

                if (group == FabricationGroup.Unspecified || recipe.GetResolvedFabricationGroup() == group)
                    return true;
            }

            return false;
        }

        private void RebuildVisibleRecipes()
        {
            _filteredRecipes.Clear();
            if (_allRecipes == null)
            {
                _recipes = null;
                return;
            }

            for (int i = 0; i < _allRecipes.Count; i++)
            {
                RecipeData recipe = _allRecipes[i];
                if (recipe == null)
                    continue;

                if (_selectedGroup != FabricationGroup.Unspecified &&
                    recipe.GetResolvedFabricationGroup() != _selectedGroup)
                {
                    continue;
                }

                _filteredRecipes.Add(recipe);
            }

            _recipes = _filteredRecipes;
            if (_recipes.Count == 0)
                _selectedIndex = 0;
            else
                _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _recipes.Count - 1);
        }

        private RecipeData ResolveDisplayRecipe()
        {
            if (_currentFabricator == null)
                return null;

            if (_currentFabricator.IsCrafting && _currentFabricator.ActiveRecipe != null)
                return _currentFabricator.ActiveRecipe;

            if (!_isOpen || _recipes == null || _recipes.Count == 0)
                return null;

            return _selectedIndex >= 0 && _selectedIndex < _recipes.Count
                ? _recipes[_selectedIndex]
                : null;
        }

        private void RenderActiveRecipeHologram(float deltaTime)
        {
            if ((_currentFabricator == null || !_isOpen) && !_isCrafting)
            {
                _debugVisibleInstanceCount = 0;
                return;
            }

            RecipeData recipe = ResolveDisplayRecipe();
            if (recipe == null || recipe.ingredients == null || recipe.ingredients.Count == 0)
            {
                _debugVisibleInstanceCount = 0;
                return;
            }

            EnsureHologramBuffers();
            EnsureHologramResources();

            if (!_hologramMatrices.IsCreated || _runtimeHologramMaterial == null || _runtimeHologramMesh == null)
            {
                _debugVisibleInstanceCount = 0;
                return;
            }

            int visibleCount = BuildHologramMatrices(recipe, deltaTime);
            _debugVisibleInstanceCount = visibleCount;
            if (visibleCount <= 0)
            {
                RenderSelectedRecipeHologram(recipe);
                return;
            }

            RenderSelectedRecipeHologram(recipe);

            Graphics.DrawMeshInstanced(
                _runtimeHologramMesh,
                0,
                _runtimeHologramMaterial,
                _hologramMatrixBuffer,
                visibleCount,
                null,
                ShadowCastingMode.Off,
                false,
                0,
                null,
                LightProbeUsage.Off,
                null);
        }

        private void RenderSelectedRecipeHologram(RecipeData recipe)
        {
            if (_currentFabricator == null || recipe == null)
                return;

            Mesh proxyMesh = ResolveProxyMesh(recipe.resultItem);
            if (proxyMesh == null || _runtimeHologramMaterial == null)
                return;

            Transform anchor = _currentFabricator.transform;
            if (anchor == null)
                return;

            Vector3 anchorPosition = anchor.position + anchor.up * (hologramHeight + 0.28f) + anchor.forward * 0.16f;
            Quaternion worldRotation = Quaternion.AngleAxis(Time.unscaledTime * HologramSpinDegreesPerSecond, Vector3.up);
            Vector3 scale = Vector3.one * (hologramCellSize * 2.8f);
            _selectedRecipeHologramBuffer[0] = Matrix4x4.TRS(anchorPosition, worldRotation, scale);

            Graphics.DrawMeshInstanced(
                proxyMesh,
                0,
                _runtimeHologramMaterial,
                _selectedRecipeHologramBuffer,
                1,
                null,
                ShadowCastingMode.Off,
                false,
                0,
                null,
                LightProbeUsage.Off,
                null);
        }

        private Mesh ResolveProxyMesh(ItemData item)
        {
            if (item != null)
            {
                int itemHashId = ComputeItemHash(item);
                if (itemHashId != 0 &&
                    ItemTemplateRegistry.TryGetTemplate(itemHashId, out ItemTemplate template))
                {
                    int proxyMeshIndex = template.ProxyMeshIndex;
                    if ((uint)proxyMeshIndex < (uint)hologramProxyMeshes.Length)
                    {
                        Mesh mesh = hologramProxyMeshes[proxyMeshIndex];
                        if (mesh != null)
                            return mesh;
                    }
                }
            }

            return _runtimeHologramMesh;
        }

        private int BuildHologramMatrices(RecipeData recipe, float deltaTime)
        {
            int instanceCount = 0;
            Transform anchor = _currentFabricator != null ? _currentFabricator.transform : null;
            if (anchor == null || recipe.ingredients == null)
                return 0;

            Vector3 anchorPosition = anchor.position + anchor.up * hologramHeight;
            Quaternion anchorRotation = Quaternion.LookRotation(anchor.forward, Vector3.up);
            float bobOffset = Mathf.Sin(Time.unscaledTime * hologramBobFrequency) * hologramBobAmplitude;
            int ingredientCount = recipe.ingredients.Count;

            for (int ingredientIndex = 0; ingredientIndex < ingredientCount && instanceCount < MaxVisibleHologramInstances; ingredientIndex++)
            {
                InventoryCost ingredient = recipe.ingredients[ingredientIndex];
                if (ingredient == null || ingredient.item == null || ingredient.amount <= 0)
                    continue;

                int unitCount = Mathf.Clamp(ingredient.amount, 1, MaxVisibleHologramInstances - instanceCount);
                for (int unitIndex = 0; unitIndex < unitCount && instanceCount < MaxVisibleHologramInstances; unitIndex++)
                {
                    int gridColumn = instanceCount % 4;
                    int gridRow = instanceCount / 4;
                    float lateral = (gridColumn - 1.5f) * hologramSpacing;
                    float vertical = gridRow * hologramSpacing * 0.72f;
                    Vector3 localOffset = new Vector3(lateral, vertical + bobOffset, 0.24f + gridRow * 0.02f);
                    Vector3 worldPosition = anchorPosition +
                                            anchorRotation * localOffset +
                                            anchor.right * Mathf.Sin((Time.unscaledTime + instanceCount) * 0.37f) * 0.01f;
                    Quaternion worldRotation = Quaternion.AngleAxis(
                        hologramYawBias + Time.unscaledTime * HologramSpinDegreesPerSecond + ingredientIndex * 11f + unitIndex * 7f,
                        Vector3.up);
                    Vector3 scale = Vector3.one * hologramCellSize;

                    Matrix4x4 matrix = Matrix4x4.TRS(worldPosition, worldRotation, scale);
                    _hologramMatrices[instanceCount] = matrix;
                    _hologramMatrixBuffer[instanceCount] = matrix;
                    instanceCount++;
                }
            }

            return instanceCount;
        }

        private void EnsureHologramBuffers()
        {
            if (_hologramMatrices.IsCreated)
                return;

            _hologramMatrices = new NativeArray<Matrix4x4>(MaxVisibleHologramInstances, Allocator.Persistent);
        }

        private void EnsureHologramResources()
        {
            if (_runtimeHologramMesh == null)
                _runtimeHologramMesh = CreateCubeMesh();

            if (_runtimeHologramMaterial == null)
            {
#if UNITY_EDITOR
                if (hologramShader == null)
                    hologramShader = AssetDatabase.LoadAssetAtPath<Shader>(HologramShaderPath);
#endif
                if (hologramShader != null)
                {
                    _runtimeHologramMaterial = new Material(hologramShader)
                    {
                        enableInstancing = true,
                        hideFlags = HideFlags.DontSave
                    };

                    if (_runtimeHologramMaterial.HasProperty(BaseColorId))
                        _runtimeHologramMaterial.SetColor(BaseColorId, hologramColor);
                    else if (_runtimeHologramMaterial.HasProperty(ColorId))
                        _runtimeHologramMaterial.SetColor(ColorId, hologramColor);
                }
            }
        }

        private void ResolveRuntimeReferences()
        {
            IPlayerInventoryService inventoryService = GlobalRegistry.PlayerInventory;
            if (inventoryService != null && inventoryService.Inventory != null)
                playerInventory = inventoryService.Inventory;

            if (hudCamera == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                if (playerContext != null)
                    hudCamera = playerContext.PlayerCamera;
            }

            if (playerInventory == null &&
                SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                playerInventory = playerTransform.GetComponent<PlayerInventory>();
            }
        }

        private void EnsureRecipePointerBuffers()
        {
            if (!_recipePointerCommands.IsCreated)
                _recipePointerCommands = new NativeArray<RaycastCommand>(1, Allocator.Persistent);

            if (!_recipePointerHits.IsCreated)
                _recipePointerHits = new NativeArray<RaycastHit>(1, Allocator.Persistent);
        }

        private void EnsureRecipeListPool()
        {
            if (_recipeListRoot != null)
                return;

            GameObject root = new GameObject("FabricatorRecipeList"); // COLD ALLOC: GameObject[1] — diegetic recipe list root — owner: HectonFabricatorUI
            root.hideFlags = HideFlags.DontSave;
            _recipeListRoot = root.transform;
            _recipeListRoot.localScale = Vector3.one * recipeEntryScale;

            for (int i = 0; i < MaxVisibleRecipeEntries; i++)
            {
                GameObject entryObject = new GameObject($"RecipeEntry_{i}"); // COLD ALLOC: GameObject[8] — diegetic recipe entry pool — owner: HectonFabricatorUI
                entryObject.hideFlags = HideFlags.DontSave;
                entryObject.transform.SetParent(_recipeListRoot, false);

                TextMeshPro label = entryObject.AddComponent<TextMeshPro>();
                label.fontSize = 4.2f;
                label.alignment = TextAlignmentOptions.Center;
                label.color = recipeIdleColor;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.text = string.Empty;

                BoxCollider collider = entryObject.AddComponent<BoxCollider>();
                collider.size = new Vector3(2.2f, 0.52f, 0.04f);
                collider.center = new Vector3(0f, 0f, 0.02f);

                WorldSpaceTMPSharpnessController sharpness = entryObject.AddComponent<WorldSpaceTMPSharpnessController>();
                sharpness.Bind(label, hudCamera);

                _recipeEntries[i] = new RecipeListEntry
                {
                    Root = entryObject.transform,
                    Label = label,
                    Collider = collider,
                    Sharpness = sharpness,
                    RecipeIndex = -1
                };
            }

            SetRecipeListVisible(false);
        }

        private void SetRecipeListVisible(bool visible)
        {
            if (_recipeListRoot == null)
                return;

            _recipeListRoot.gameObject.SetActive(visible);
        }

        private void UpdateRecipeListPose()
        {
            if (!_isOpen || _recipeListRoot == null || _currentFabricator == null)
                return;

            ResolveRuntimeReferences();
            Transform anchor = _currentFabricator.transform;
            Vector3 rootPosition = anchor.position + anchor.up * recipeListHeight + anchor.forward * recipeListForwardOffset;
            _recipeListRoot.position = rootPosition;
            _recipeListRoot.localScale = Vector3.one * recipeEntryScale;

            if (hudCamera != null)
            {
                Vector3 facing = rootPosition - hudCamera.transform.position;
                if (facing.sqrMagnitude > 0.0001f)
                    _recipeListRoot.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
            }
        }

        private void RefreshRecipeListIfDirty()
        {
            if (!_isOpen || _recipeListRoot == null)
                return;

            int visualVersion = ResolveRecipeVisualVersion();
            if (visualVersion == _lastRecipeVisualVersion)
                return;

            _lastRecipeVisualVersion = visualVersion;
            RebuildRecipeListEntries();
        }

        private int ResolveRecipeVisualVersion()
        {
            int recipeCount = _recipes != null ? _recipes.Count : 0;
            int inventoryVersion = playerInventory != null ? playerInventory.InventoryVersion : 0;
            return recipeCount ^ (_selectedIndex << 8) ^ (((int)_selectedGroup & 0xFF) << 16) ^ (inventoryVersion << 1) ^ ((_hoveredRecipeIndex + 1) << 24);
        }

        private void RebuildRecipeListEntries()
        {
            int visibleRecipeCount = _recipes != null ? Mathf.Min(_recipes.Count, MaxVisibleRecipeEntries) : 0;

            for (int i = 0; i < MaxVisibleRecipeEntries; i++)
            {
                RecipeListEntry entry = _recipeEntries[i];
                if (entry.Root == null || entry.Label == null)
                    continue;

                if (i < visibleRecipeCount)
                {
                    RecipeData recipe = _recipes[i];
                    bool selected = i == _selectedIndex;
                    bool craftable = CanCraftRecipe(recipe);
                    entry.RecipeIndex = i;
                    entry.Root.localPosition = new Vector3(0f, -i * recipeEntrySpacing, 0f);
                    entry.Root.localRotation = Quaternion.identity;
                    entry.Root.localScale = selected ? Vector3.one * 1.08f : Vector3.one;
                    entry.Root.gameObject.SetActive(true);
                    entry.Label.color = !craftable
                        ? recipeUnavailableColor
                        : selected
                            ? recipeSelectedColor
                            : recipeIdleColor;
                    if (entry.Sharpness != null)
                        entry.Sharpness.Bind(entry.Label, hudCamera);

                    int length = BuildRecipeLabel(entry.Label, recipe, selected, craftable);
                    entry.Label.SetCharArray(_recipeLabelBuffer, 0, length);
                    _recipeEntries[i] = entry;
                }
                else
                {
                    entry.RecipeIndex = -1;
                    entry.Root.gameObject.SetActive(false);
                    _recipeEntries[i] = entry;
                }
            }
        }

        private int BuildRecipeLabel(TMP_Text label, RecipeData recipe, bool selected, bool craftable)
        {
            int cursor = 0;
            cursor = AppendLiteral(selected ? '>' : ' ', _recipeLabelBuffer, cursor);
            cursor = AppendLiteral(' ', _recipeLabelBuffer, cursor);
            cursor = AppendLiteral(craftable ? '[' : '[', _recipeLabelBuffer, cursor);
            cursor = AppendString(craftable ? "OK" : "LOW", _recipeLabelBuffer, cursor);
            cursor = AppendLiteral(']', _recipeLabelBuffer, cursor);
            cursor = AppendLiteral(' ', _recipeLabelBuffer, cursor);

            string displayName = recipe != null ? recipe.DisplayNameOrFallback : string.Empty;
            cursor = AppendString(displayName, _recipeLabelBuffer, cursor);

            if (recipe != null && recipe.resultQuantity > 1)
            {
                cursor = AppendLiteral(' ', _recipeLabelBuffer, cursor);
                cursor = AppendLiteral('x', _recipeLabelBuffer, cursor);
                if (recipe.resultQuantity.TryFormat(_recipeLabelBuffer.AsSpan(cursor), out int written))
                    cursor += written;
            }

            TMP_TextRegistry.EnsureRegistered(label);
            return Mathf.Clamp(cursor, 0, _recipeLabelBuffer.Length);
        }

        private void ScheduleRecipePointerSelection()
        {
            if (!_isOpen || _recipeListRoot == null || hudCamera == null || !_recipePointerCommands.IsCreated || !_recipePointerHits.IsCreated)
                return;

            if (_recipePointerScheduled)
                return;

            QueryParameters query = new QueryParameters(~0, false, QueryTriggerInteraction.Ignore);
            _recipePointerCommands[0] = new RaycastCommand(
                hudCamera.transform.position,
                hudCamera.transform.forward,
                query,
                RecipePointerDistanceMeters);
            _recipePointerHandle = RaycastCommand.ScheduleBatch(_recipePointerCommands, _recipePointerHits, 1);
            _recipePointerScheduled = true;
        }

        private void UpdateRecipePointerSelection()
        {
            if (!_recipePointerScheduled)
                return;

            if (!_recipePointerHandle.IsCompleted)
                return;

            _recipePointerHandle.Complete();
            _recipePointerScheduled = false;
            _hoveredRecipeIndex = -1;

            Collider hitCollider = _recipePointerHits[0].collider;
            if (hitCollider == null)
                return;

            for (int i = 0; i < MaxVisibleRecipeEntries; i++)
            {
                RecipeListEntry entry = _recipeEntries[i];
                if (entry.Collider == null || !ReferenceEquals(entry.Collider, hitCollider) || entry.RecipeIndex < 0)
                    continue;

                _hoveredRecipeIndex = entry.RecipeIndex;
                if (_selectedIndex != entry.RecipeIndex)
                    _selectedIndex = entry.RecipeIndex;
                break;
            }
        }

        private bool CanCraftRecipe(RecipeData recipe)
        {
            if (recipe == null || recipe.ingredients == null || playerInventory == null)
                return false;

            InventoryGrid grid = playerInventory.Grid;
            NativeArray<int>.ReadOnly anchorHashIds = grid != null ? grid.AnchorHashIds : default;
            NativeArray<ushort>.ReadOnly stackCounts = playerInventory.GetStackCountsReadOnly();
            if (!anchorHashIds.IsCreated || !stackCounts.IsCreated)
                return false;

            for (int ingredientIndex = 0; ingredientIndex < recipe.ingredients.Count; ingredientIndex++)
            {
                InventoryCost ingredient = recipe.ingredients[ingredientIndex];
                if (ingredient == null || ingredient.item == null || ingredient.amount <= 0)
                    continue;

                int itemHashId = ComputeItemHash(ingredient.item);
                if (itemHashId == 0)
                    return false;

                int availableCount = 0;
                int anchorCount = Mathf.Min(anchorHashIds.Length, stackCounts.Length);
                for (int anchorIndex = 0; anchorIndex < anchorCount; anchorIndex++)
                {
                    if (anchorHashIds[anchorIndex] != itemHashId)
                        continue;

                    availableCount += stackCounts[anchorIndex];
                    if (availableCount >= ingredient.amount)
                        break;
                }

                if (availableCount < ingredient.amount)
                    return false;
            }

            return true;
        }

        private static int ComputeItemHash(ItemData item)
        {
            return item != null ? LocHash.Compute(item.PersistentId) : 0;
        }

        private static int AppendLiteral(char value, char[] buffer, int cursor)
        {
            if ((uint)cursor >= (uint)buffer.Length)
                return cursor;

            buffer[cursor] = value;
            return cursor + 1;
        }

        private static int AppendString(string value, char[] buffer, int cursor)
        {
            if (string.IsNullOrEmpty(value) || cursor >= buffer.Length)
                return cursor;

            ReadOnlySpan<char> span = value.AsSpan();
            int writable = Mathf.Min(span.Length, buffer.Length - cursor);
            span.Slice(0, writable).CopyTo(buffer.AsSpan(cursor, writable));
            return cursor + writable;
        }

        private static Mesh CreateCubeMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "FabricatorHologramCube"
            };

            Vector3[] vertices =
            {
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3( 0.5f, -0.5f, -0.5f),
                new Vector3( 0.5f,  0.5f, -0.5f),
                new Vector3(-0.5f,  0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f,  0.5f),
                new Vector3( 0.5f, -0.5f,  0.5f),
                new Vector3( 0.5f,  0.5f,  0.5f),
                new Vector3(-0.5f,  0.5f,  0.5f)
            };

            int[] triangles =
            {
                0, 2, 1, 0, 3, 2,
                1, 2, 6, 1, 6, 5,
                5, 6, 7, 5, 7, 4,
                4, 7, 3, 4, 3, 0,
                3, 7, 6, 3, 6, 2,
                4, 0, 1, 4, 1, 5
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return mesh;
        }

        private void UpdateDiagnostics()
        {
            _debugIsOpen = _isOpen;
            _debugIsCrafting = _isCrafting;
            _debugSelectedIndex = _selectedIndex;
            _debugHoveredRecipeIndex = _hoveredRecipeIndex;
        }
    }
}
