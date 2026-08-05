using System;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using Hecton8.Bootstrap;
using Hecton8.Building;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Crafting;
using Hecton8.Economy;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton.Localization;
using TMPro;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    public sealed class HectonFabricatorUI : MonoBehaviour, ILateFrameTickable, ICraftingEventListener, IGlobalRegistryHotSwapListener, IOriginShiftListener, ILocalizationLanguageChangedListener
    {
        private const int MaxVisibleHologramInstances = 16;
        private const int MaxVisibleRecipeEntries = 8;
        private const int RecipeLabelBufferCapacity = 128;
        private const int FallbackBufferCapacity = 64;
        private const float RecipePointerDistanceMeters = 6f;
        private const float RecipePointerHalfWidth = 1.1f;
        private const float RecipePointerHalfHeight = 0.26f;
        private const float RecipePointerPlaneEpsilon = 0.0001f;
        private const int FabricatorUiPerformanceWarningCooldownFrames = 30;
        private const float HologramBaseDistanceMeters = 1f;
        private const float SelectedHologramScaleMultiplier = 2.8f;
        private const string FabricatorStaticCanvasRootName = "Fabricator_StaticCanvasRoot";
        private const string FabricatorDynamicCanvasRootName = "Fabricator_DynamicCanvasRoot";
        private const string RuntimeRootName = "[HectonFabricatorUI]";
        private const int FabricatorStaticCanvasSortingOrder = 10;
        private const int FabricatorDynamicCanvasSortingOrder = 30;
        private const float InverseTwoPi = 0.15915494f;
        private static readonly uint _FabricatorUiSolveBudgetWarningHash =
            unchecked((uint)LocHash.Compute("HectonFabricatorUI.SolveBudgetExceeded"));
        private static readonly uint _FabricatorUiContextHash =
            unchecked((uint)LocHash.Compute(nameof(HectonFabricatorUI)));
        private static readonly long _FabricatorUiSolveBudgetTicks = Math.Max(1L, Stopwatch.Frequency / 10000L);

        private struct RecipeListEntry
        {
            public Transform Root;
            public TextMeshPro Label;
            public TextMeshPro InflationLabel;
            public WorldSpaceTMPSharpnessController Sharpness;
            public WorldSpaceTMPSharpnessController InflationSharpness;
            public int RecipeIndex;
            public RecipeData FastFailRecipe;
            public RecipeRequirementDTO FastFailRequirement;
            public int FastFailRequirementMultiplier;
            public int FastFailScarcityVersion;
            public float FastFailInflationMultiplier;
            public bool HasFastFailRequirement;
            public RecipeData LabelRecipe;
            public char[] CachedDisplayNameBuffer;
            public int CachedDisplayNameLength;
            public int CachedDisplayNameVersion;
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int CraftProgressId = Shader.PropertyToID("_CraftProgress");
        private static readonly int ScanProgressId = Shader.PropertyToID("_ScanProgress");
        private static readonly int GlitchAmountId = Shader.PropertyToID("_GlitchAmount");
        private static readonly int HologramBobAmplitudeId = Shader.PropertyToID("_HologramBobAmplitude");
        private static readonly int HologramBobFrequencyId = Shader.PropertyToID("_HologramBobFrequency");
        private static readonly int HologramSwayAmplitudeId = Shader.PropertyToID("_HologramSwayAmplitude");
        private static readonly int HologramSwayFrequencyId = Shader.PropertyToID("_HologramSwayFrequency");
        private static readonly int HologramPulseAmplitudeId = Shader.PropertyToID("_HologramPulseAmplitude");
        private static readonly int HologramPulseFrequencyId = Shader.PropertyToID("_HologramPulseFrequency");
        // COLD ALLOC: FabricationGroup[7] - stable recipe group cycle order - owner: HectonFabricatorUI
        private static readonly FabricationGroup[] s_fabricationGroupCycle =
        {
            FabricationGroup.Unspecified,
            FabricationGroup.Materials,
            FabricationGroup.Components,
            FabricationGroup.Tools,
            FabricationGroup.Suit,
            FabricationGroup.Construction,
            FabricationGroup.Power
        };

        [Header("References")]
        [SerializeField] private Camera hudCamera;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField, Tooltip("Required authored billboard/preview mesh. Runtime mesh generation is forbidden.")]
        private Mesh hologramMesh;
        [SerializeField, Tooltip("Required authored instanced hologram material. Runtime material generation is forbidden.")]
        private Material hologramMaterial;

        [Header("Runtime Compatibility")]
        [SerializeField] private bool useCullingMasks;

        [Header("Canvas Split")]
        [SerializeField] private Transform staticCanvasRoot;
        [SerializeField] private Transform dynamicCanvasRoot;
        [SerializeField] private Canvas staticCanvas;
        [SerializeField] private Canvas dynamicCanvas;

        [Header("Hologram Layout")]
        [SerializeField, Min(0.1f)] private float hologramHeight = 1.35f;
        [SerializeField, Min(0.01f)] private float hologramCellSize = 0.085f;
        [SerializeField, Min(0.01f)] private float hologramSpacing = 0.11f;
        [SerializeField, Min(0f)] private float hologramBobAmplitude = 0.035f;
        [SerializeField, Min(0f)] private float hologramBobFrequency = 1.4f;
        [SerializeField, Min(0f)] private float hologramYawBias = 14f;
        [SerializeField] private Color hologramColor = new Color(0.08f, 0.88f, 1f, 0.42f);
        [SerializeField, Min(0f)] private float hologramPulseAmplitude = 0.16f;
        [SerializeField, Min(0f)] private float hologramPulseFrequency = 1.1f;
        [SerializeField, Min(0f)] private float hologramOrbitAmplitude = 0.035f;
        [SerializeField, Min(0f)] private float hologramOrbitFrequency = 0.9f;

        [Header("Diegetic Recipe List")]
        [SerializeField, Min(0.1f)] private float recipeListHeight = 1.68f;
        [SerializeField, Min(0f)] private float recipeListForwardOffset = 0.42f;
        [SerializeField, Min(0.05f)] private float recipeEntrySpacing = 0.12f;
        [SerializeField, Min(0.001f)] private float recipeEntryScale = 0.0024f;
        [SerializeField] private Color recipeIdleColor = new Color(0.42f, 0.9f, 1f, 0.85f);
        [SerializeField] private Color recipeSelectedColor = new Color(1f, 0.97f, 0.72f, 1f);
        [SerializeField] private Color recipeUnavailableColor = new Color(1f, 0.52f, 0.32f, 0.92f);
        [SerializeField] private Color inflationColor = new Color(1f, 0.28f, 0.22f, 1f);

        [Header("Batch Crafting")]
        [SerializeField, Min(1)] private int craftBatchMultiplier = 1;

        [Header("Diegetic Failure Feedback")]
        [SerializeField, Min(0f)] private float failurePanelShakeDurationSeconds = 0.22f;
        [SerializeField, Min(0f)] private float failurePanelShakeAmplitudeMeters = 0.018f;
        [SerializeField, Min(0f)] private float failurePanelShakeFrequencyHz = 32f;

        [Header("Diagnostics")]
        [SerializeField] private bool _debugIsOpen;
        [SerializeField] private bool _debugIsCrafting;
        [SerializeField] private int _debugSelectedIndex;
        [SerializeField] private int _debugVisibleInstanceCount;
        [SerializeField] private int _debugHoveredRecipeIndex = -1;

        // COLD ALLOC: List<RecipeData>[Fabricator.MaxRecipeCacheEntries] — filtered fabricator recipe cache — owner: HectonFabricatorUI
        private readonly List<RecipeData> _filteredRecipes = new List<RecipeData>(Fabricator.MaxRecipeCacheEntries);
        // COLD ALLOC: Matrix4x4[16] — instanced hologram draw buffer mirror — owner: HectonFabricatorUI
        private readonly Matrix4x4[] _hologramMatrixBuffer = new Matrix4x4[MaxVisibleHologramInstances];
        // COLD ALLOC: RecipeListEntry[12] — fixed diegetic recipe row cache — owner: HectonFabricatorUI
        private readonly RecipeListEntry[] _recipeEntries = new RecipeListEntry[MaxVisibleRecipeEntries];
        // COLD ALLOC: char[96] — reusable diegetic recipe label buffer — owner: HectonFabricatorUI
        private readonly char[] _recipeLabelBuffer = new char[RecipeLabelBufferCapacity];
        // COLD ALLOC: char[64] — CharBufferPool failure fallback for scarcity inflation labels — owner: HectonFabricatorUI
        private readonly char[] _fallbackBuffer = new char[FallbackBufferCapacity];

        // COLD ALLOC: MaterialPropertyBlock - shared hologram draw properties - owner: HectonFabricatorUI
        private MaterialPropertyBlock _hologramProperties;
        private Material _resolvedHologramMaterial;
        private Mesh _resolvedHologramMesh;
        private bool _missingHologramMeshAnnounced;
        private bool _missingHologramMaterialAnnounced;
        private bool _hologramHasBaseColor;
        private bool _hologramHasColor;
        private bool _hologramHasCraftProgress;
        private bool _hologramHasScanProgress;
        private bool _hologramHasGlitchAmount;
        private bool _hologramHasBobAmplitude;
        private bool _hologramHasBobFrequency;
        private bool _hologramHasSwayAmplitude;
        private bool _hologramHasSwayFrequency;
        private bool _hologramHasPulseAmplitude;
        private bool _hologramHasPulseFrequency;
        private float _lastHologramMaterialProgress = -1f;
        private float _lastHologramMaterialGlitch = -1f;
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
        private bool _lateFrameTickRegistered;
        private float _pendingFabricatorVisualDeltaTime;
        private bool _hasPendingFabricatorVisualTick;
        private bool _pendingRecipeListVisibility;
        private bool _hasPendingRecipeListVisibility;
        private bool _hotSwapListenerRegistered;
        private bool _originShiftListenerRegistered;
        private bool _localizationListenerRegistered;
        private ILocalizationTextReadModel _localizationManager;
        private IInputService _inputService;
        private IPlayerInventoryService _playerInventoryService;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private INativeInputManagerRuntime _nativeInputManager;
        private IResourceScarcityReadModel _resourceScarcityRuntime;
        private INativeInputManagerRuntime _subscribedInputManager;
        private uint _lastPlayerInputSignalSequence;
        private const uint PlayerInputSignalSourceHash = 0x504C494Eu;
        private int _selectedHologramRecipeHash;
        private bool _selectedHologramMatrixInitialized;
        private float4x4 _selectedHologramBaseMatrix = float4x4.identity;
        private float _selectedHologramYawRadians;
        private float _selectedHologramCachedSize;
        private Transform _selectedHologramAnchor;
        private int _hologramMatrixRecipeHash;
        private int _hologramMatrixLayoutVersion = int.MinValue;
        private Transform _hologramMatrixAnchor;
        private int _hologramMatrixVisibleCount;
        private bool _hologramMatrixCacheDirty = true;
        private CanvasGroup _recipeListCanvasGroup;
        private bool _recipeListVisible;
        private bool _recipeListPoseValid;
        private Vector3 _recipeListRuntimePosition;
        private Vector3 _recipeListRight;
        private Vector3 _recipeListUp;
        private Vector3 _recipeListForward;
        private Vector3 _recipeListInverseScale = Vector3.one;
        private Quaternion _recipeListAppliedRotation = Quaternion.identity;
        private float _recipeListAppliedScale = -1f;
        private float _failurePanelShakeRemainingSeconds;
        private float _failurePanelShakeElapsedSeconds;
        private int _nextPerformanceWarningFrame;
        private int _recipeLabelTextVersion;

        public static bool IsMenuOpen { get; private set; }

        /// <summary>
        /// Resolve-or-create the sole fabricator crafting UI owner.
        /// Script GUID 4bc926527a4a0ca4f82df88084310ef1 has ZERO live scene/prefab hits, so no authored
        /// instance exists in any scene. HectonLoreSystemsRoot.SetupAllSystems is editor ContextMenu-only
        /// and does not run in play mode. This type registers itself with CraftingEvents in OnEnable, which
        /// only runs on an instance that already exists, so without this construction site
        /// Fabricator.cs:687 raises CraftingEvents.FabricatorOpened into an empty listener list and the
        /// player can never open the crafting menu.
        /// Idempotent: an authored or already-constructed instance wins and no duplicate is built.
        /// </summary>
        public static HectonFabricatorUI EnsureRuntimeInstance()
        {
            HectonFabricatorUI existing = FindFirstObjectByType<HectonFabricatorUI>(FindObjectsInactive.Include);
            if (existing != null)
            {
                // Re-activate a buried instance rather than stacking a second one. This is not cosmetic:
                // CraftingEvents.Register runs in OnEnable, and OnEnable never fires while the GameObject
                // is inactive in hierarchy, so a found-but-disabled owner leaves the FabricatorOpened
                // payload unheard exactly as an absent one would.
                if (!existing.gameObject.activeSelf)
                    existing.gameObject.SetActive(true);
                if (!existing.enabled)
                    existing.enabled = true;
                return existing;
            }

            if (!Application.isPlaying)
                return null;

            // Player-build construction path: no authored instance is reachable in any scene.
            // Every serialized reference this type needs is resolved cold in Awake - playerInventory and
            // hudCamera from GlobalRegistry (RebindPlayerInventoryService / RebindPlayerRuntimeContext),
            // the canvas split from EnsureCanvasSplit - so a runtime-created owner drives the full craft
            // flow. The authored hologramMesh/hologramMaterial pair stays unassigned on this path and the
            // existing one-shot diagnostics report it; the preview hologram is the only degraded surface.
            GameObject runtimeRoot = new GameObject(RuntimeRootName); // COLD ALLOC: GameObject[1] - bootstrap-owned fabricator UI root - owner: HectonFabricatorUI
            return runtimeRoot.AddComponent<HectonFabricatorUI>();
        }

        public int CraftBatchMultiplier
        {
            get => Mathf.Max(1, craftBatchMultiplier);
            set
            {
                int nextMultiplier = Mathf.Max(1, value);
                if (nextMultiplier == craftBatchMultiplier)
                    return;

                craftBatchMultiplier = nextMultiplier;
                _lastRecipeVisualVersion = int.MinValue;
                InvalidateHologramMatrixCache();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            IsMenuOpen = false;
        }

        private void Awake()
        {
            GlobalTelemetryBus.Initialize();
            CharBufferPool.Prewarm();
            CacheRegistryServicesCold();
            BindRuntimeReferencesCold(allowFallbackLookup: true);
            EnsureCanvasSplit();

            EnsureHologramResources();
            EnsureRecipeListPool();
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            TryRegisterOriginShiftListener();
            SubscribeInputManagerIfAvailable();
            TryRegisterLocalizationListener();
            RegisterLateFrameTick();

            CraftingEvents.Register(this);
        }

        private void Start()
        {
            CacheRegistryServicesCold();
            BindRuntimeReferencesCold(allowFallbackLookup: true);
            TryRegisterHotSwapListener();
            SubscribeInputManagerIfAvailable();
        }

        private void OnDisable()
        {
            UnsubscribeInputManager();
            TryUnregisterHotSwapListener();
            TryUnregisterOriginShiftListener();
            TryUnregisterLocalizationListener();

            CraftingEvents.Unregister(this);

            UnregisterLateFrameTick();

            if (_isOpen)
                CloseMenu();
        }

        private void OnDestroy()
        {
            UnsubscribeInputManager();
            TryUnregisterHotSwapListener();
            TryUnregisterOriginShiftListener();
            TryUnregisterLocalizationListener();
            UnregisterLateFrameTick();

            if (_hologramProperties != null)
                _hologramProperties.Clear();
            _resolvedHologramMaterial = null;
            _resolvedHologramMesh = null;

            if (_recipeListRoot != null)
            {
                Destroy(_recipeListRoot.gameObject);
                _recipeListRoot = null;
            }
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!math.all(math.isfinite(new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z))) ||
                !math.isfinite(shiftSqrMagnitude) ||
                shiftSqrMagnitude <= 0.000001f)
            {
                return;
            }

            if (_recipeListRoot != null)
                _recipeListRoot.hasChanged = true;

            _selectedHologramMatrixInitialized = false;
            InvalidateHologramMatrixCache();
        }

        private void TryRegisterOriginShiftListener()
        {
            if (_originShiftListenerRegistered || !Application.isPlaying)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _originShiftListenerRegistered = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        private void TryUnregisterOriginShiftListener()
        {
            if (!_originShiftListenerRegistered)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _originShiftListenerRegistered = false;
        }

        private void RunFabricatorUiFrame(float deltaTime)
        {
            long solveStartTimestamp = Stopwatch.GetTimestamp();
            ConsumePlayerInputSignals();
            if (!_isOpen)
            {
                PublishSolveWarningIfNeeded(solveStartTimestamp);
                return;
            }

            if (_isOpen && _currentFabricator == null)
            {
                CloseMenu();
                PublishSolveWarningIfNeeded(solveStartTimestamp);
                return;
            }

            if (hudCamera == null || playerInventory == null)
                RefreshRuntimeReferencesHot();

            AdvanceFailurePanelShake(deltaTime);
            UpdateRecipePointerSelection();
            UpdateDiagnostics();
            _pendingFabricatorVisualDeltaTime += math.max(0f, deltaTime);
            _hasPendingFabricatorVisualTick = true;
            PublishSolveWarningIfNeeded(solveStartTimestamp);
        }

        public void LateFrameTick()
        {
            RunFabricatorLateFrame();
        }

        void ILateFrameTickable.LateFrameTick()
        {
            RunFabricatorLateFrame();
        }

        private void RunFabricatorLateFrame()
        {
            if (!_isOpen && !_isCrafting && !_hasPendingFabricatorVisualTick && !_hasPendingRecipeListVisibility)
                return;

            RunFabricatorUiFrame(SystemDispatcher.CurrentFrameDeltaTime);
            FlushFabricatorVisualTick();
        }

        private void FlushFabricatorVisualTick()
        {
            FlushPendingRecipeListVisibility();

            if (!_hasPendingFabricatorVisualTick)
                return;

            float visualDeltaTime = _pendingFabricatorVisualDeltaTime;
            _pendingFabricatorVisualDeltaTime = 0f;
            _hasPendingFabricatorVisualTick = false;

            if (!_isOpen && !_isCrafting)
            {
                _debugVisibleInstanceCount = 0;
                return;
            }

            if (hudCamera == null || playerInventory == null)
                RefreshRuntimeReferencesHot();

            UpdateRecipeListPose();
            RefreshRecipeListIfDirty();
            RenderActiveRecipeHologram(visualDeltaTime);
            UpdateDiagnostics();
        }

        private void PublishSolveWarningIfNeeded(long startTimestamp)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
            int currentFrame = SystemDispatcher.CurrentFrameIndex;
            if (elapsedTicks <= _FabricatorUiSolveBudgetTicks || currentFrame < _nextPerformanceWarningFrame)
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(
                _FabricatorUiSolveBudgetWarningHash,
                _FabricatorUiContextHash,
                (elapsedTicks * 1000f) / Stopwatch.Frequency);
            _nextPerformanceWarningFrame = currentFrame + FabricatorUiPerformanceWarningCooldownFrames;
        }

        public void OnCraftingEvent(in CraftingEventPayload payload)
        {
            switch ((CraftingEventType)payload.EventType)
            {
                case CraftingEventType.FabricatorOpened:
                    if (CraftingEvents.TryResolveFabricator(in payload, out Fabricator fabricator))
                        HandleFabricatorOpened(fabricator);
                    break;
                case CraftingEventType.FabricatorClosed:
                    HandleFabricatorClosed();
                    break;
                case CraftingEventType.CraftStarted:
                    if (CraftingEvents.TryResolveRecipe(in payload, out RecipeData recipe))
                        HandleCraftStarted(recipe);
                    break;
                case CraftingEventType.CraftProgressUpdated:
                    HandleCraftProgress(payload.Progress01);
                    break;
                case CraftingEventType.CraftCompleted:
                    CraftingEvents.TryResolveItem(in payload, out ItemData resultItem);
                    HandleCraftCompleted(resultItem);
                    break;
                case CraftingEventType.CraftCancelled:
                    HandleCraftCancelled();
                    break;
                case CraftingEventType.CraftFailed:
                    HandleCraftFailed(in payload);
                    break;
            }
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)
        {
            unchecked
            {
                _recipeLabelTextVersion++;
            }

            _lastRecipeVisualVersion = int.MinValue;
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
            BaselinePlayerInputSignalSequence();
            RegisterLateFrameTick();

            IInputService inputService = _inputService;
            if (inputService != null)
                inputService.SwitchToUIInput();

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
            InvalidateHologramMatrixCache();

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
            InvalidateHologramMatrixCache();
        }

        private void HandleCraftCancelled()
        {
            _isCrafting = false;
            _craftProgress = 0f;
            InvalidateHologramMatrixCache();
        }

        private void HandleCraftFailed(in CraftingEventPayload payload)
        {
            if (!_isOpen)
                return;

            if (CraftingEvents.TryResolveFabricator(in payload, out Fabricator fabricator) &&
                _currentFabricator != null &&
                !ReferenceEquals(fabricator, _currentFabricator))
            {
                return;
            }

            _failurePanelShakeRemainingSeconds = Mathf.Max(_failurePanelShakeRemainingSeconds, failurePanelShakeDurationSeconds);
            _failurePanelShakeElapsedSeconds = 0f;
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

            _currentFabricator.StartCraft(recipe, Mathf.Max(1, craftBatchMultiplier));
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

        /// <summary>
        /// Public entry for systems that must restore locomotion input when the fabricator
        /// menu is still open (e.g. headless probe settle). Routes through the real close path
        /// so IsMenuOpen, UI maps, and cursor state stay coherent — not a harness mock.
        /// </summary>
        public void ForceCloseMenu()
        {
            if (!_isOpen && !IsMenuOpen)
                return;

            CloseMenu();
        }

        private void CloseMenu()
        {
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
            _failurePanelShakeRemainingSeconds = 0f;
            _failurePanelShakeElapsedSeconds = 0f;
            _recipeListPoseValid = false;
            _lastRecipeVisualVersion = int.MinValue;
            InvalidateHologramMatrixCache();
            QueueRecipeListVisibility(false);

            UnregisterLateFrameTick();

            IInputService inputService = _inputService;
            if (inputService != null)
                inputService.SwitchToPlayerInput();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            UpdateDiagnostics();
        }

        private void RegisterLateFrameTick()
        {
            if (_lateFrameTickRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _lateFrameTickRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void UnregisterLateFrameTick(bool clearPendingWork = true)
        {
            if (_lateFrameTickRegistered)
            {
                SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
                _lateFrameTickRegistered = false;
            }

            if (!clearPendingWork)
                return;

            _pendingFabricatorVisualDeltaTime = 0f;
            _hasPendingFabricatorVisualTick = false;
            _hasPendingRecipeListVisibility = false;
        }

        private void SubscribeInputManagerIfAvailable()
        {
            if (_subscribedInputManager != null)
                return;

            INativeInputManagerRuntime inputManager = _nativeInputManager;
            if (inputManager == null)
                return;

            _subscribedInputManager = inputManager;
            _subscribedInputManager.OnNavigate += HandleNavigateInput;
            _subscribedInputManager.OnSubmit += HandleSubmitInput;
        }

        private void UnsubscribeInputManager()
        {
            if (_subscribedInputManager == null)
                return;

            _subscribedInputManager.OnNavigate -= HandleNavigateInput;
            _subscribedInputManager.OnSubmit -= HandleSubmitInput;
            _subscribedInputManager = null;
        }

        private void ConsumePlayerInputSignals()
        {
            ReadOnlySpan<PlayerInputSignal> signals = SignalBus<PlayerInputSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PlayerInputSignal signal = signals[i];
                if (signal.SourceHash != PlayerInputSignalSourceHash ||
                    !IsNewerInputSequence(signal.Sequence, _lastPlayerInputSignalSequence))
                    continue;

                _lastPlayerInputSignalSequence = signal.Sequence;
                switch (signal.Command)
                {
                    case PlayerInputSignalCommands.Cancel:
                        HandleCancelInput();
                        break;
                    case PlayerInputSignalCommands.TabNext:
                        HandleBatchNextInput();
                        break;
                    case PlayerInputSignalCommands.TabPrevious:
                        HandleBatchPreviousInput();
                        break;
                }
            }
        }

        private void BaselinePlayerInputSignalSequence()
        {
            ReadOnlySpan<PlayerInputSignal> signals = SignalBus<PlayerInputSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PlayerInputSignal signal = signals[i];
                if (signal.SourceHash == PlayerInputSignalSourceHash &&
                    IsNewerInputSequence(signal.Sequence, _lastPlayerInputSignalSequence))
                    _lastPlayerInputSignalSequence = signal.Sequence;
            }
        }

        private static bool IsNewerInputSequence(uint candidate, uint current)
        {
            return candidate != 0u && candidate != current && unchecked(candidate - current) < 0x80000000u;
        }

        private void HandleBatchNextInput()
        {
            CycleCraftBatchMultiplier(1);
        }

        private void HandleBatchPreviousInput()
        {
            CycleCraftBatchMultiplier(-1);
        }

        /// <summary>
        /// Cycles the diegetic batch count used by the next fabrication request.
        /// </summary>
        public void CycleCraftBatchMultiplier(int direction)
        {
            int nextMultiplier = ResolveNextBatchMultiplier(craftBatchMultiplier, direction);
            if (nextMultiplier == craftBatchMultiplier)
                return;

            craftBatchMultiplier = nextMultiplier;
            _lastRecipeVisualVersion = int.MinValue;
            InvalidateHologramMatrixCache();
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    bool needsLateFrameTick = _lateFrameTickRegistered ||
                                             _isOpen ||
                                             _isCrafting ||
                                             _hasPendingFabricatorVisualTick ||
                                             _hasPendingRecipeListVisibility;
                    UnregisterLateFrameTick(clearPendingWork: false);
                    if (needsLateFrameTick && currentService != null && isActiveAndEnabled)
                        RegisterLateFrameTick();
                    return;
                case GlobalRegistryServiceSlot.Input:
                    _inputService = currentService as IInputService;
                    return;
                case GlobalRegistryServiceSlot.NativeInputManagerRuntime:
                    RebindNativeInputManager(currentService as INativeInputManagerRuntime);
                    return;
                case GlobalRegistryServiceSlot.PlayerInventory:
                    RebindPlayerInventoryService(currentService as IPlayerInventoryService);
                    return;
                case GlobalRegistryServiceSlot.Player:
                    RebindPlayerRuntimeContext(currentService as IPlayerRuntimeContext);
                    return;
                case GlobalRegistryServiceSlot.ResourceScarcityRuntime:
                    _resourceScarcityRuntime = currentService as IResourceScarcityReadModel;
                    _lastRecipeVisualVersion = int.MinValue;
                    InvalidateHologramMatrixCache();
                    return;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localizationManager = currentService as ILocalizationTextReadModel;
                    unchecked
                    {
                        _recipeLabelTextVersion++;
                    }
                    _lastRecipeVisualVersion = int.MinValue;
                    return;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void TryRegisterLocalizationListener()
        {
            if (_localizationListenerRegistered || !Application.isPlaying)
                return;

            LocalizationEvents.RegisterLanguageListener(this);
            _localizationListenerRegistered = true;
        }

        private void TryUnregisterLocalizationListener()
        {
            if (!_localizationListenerRegistered)
                return;

            LocalizationEvents.UnregisterLanguageListener(this);
            _localizationListenerRegistered = false;
        }

        private void SetSelectedIndex(int nextIndex)
        {
            if (_recipes == null || _recipes.Count == 0)
                return;

            int clampedIndex = Mathf.Clamp(nextIndex, 0, _recipes.Count - 1);
            if (clampedIndex == _selectedIndex)
                return;

            _selectedIndex = clampedIndex;
            InvalidateHologramMatrixCache();
            UpdateDiagnostics();
        }

        private void CycleGroup(int direction)
        {
            int currentIndex = 0;
            for (int i = 0; i < s_fabricationGroupCycle.Length; i++)
            {
                if (s_fabricationGroupCycle[i] == _selectedGroup)
                {
                    currentIndex = i;
                    break;
                }
            }

            for (int step = 1; step <= s_fabricationGroupCycle.Length; step++)
            {
                int nextIndex = (currentIndex + (step * direction) + s_fabricationGroupCycle.Length) % s_fabricationGroupCycle.Length;
                FabricationGroup candidate = s_fabricationGroupCycle[nextIndex];
                if (!HasRecipesInGroup(candidate))
                    continue;

                _selectedGroup = candidate;
                _selectedIndex = 0;
                RebuildVisibleRecipes();
                InvalidateHologramMatrixCache();
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

            InvalidateHologramMatrixCache();
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
            if (recipe == null)
            {
                _debugVisibleInstanceCount = 0;
                return;
            }

            if (_resolvedHologramMaterial == null || _resolvedHologramMesh == null)
            {
                _debugVisibleInstanceCount = 0;
                return;
            }

            int visibleCount = recipe.ingredients != null && recipe.ingredients.Count > 0
                ? BuildHologramMatrices(recipe)
                : 0;
            _debugVisibleInstanceCount = visibleCount;
            if (visibleCount <= 0)
            {
                RenderSelectedRecipeHologram(recipe, deltaTime);
                return;
            }

            RenderSelectedRecipeHologram(recipe, deltaTime);

            UnityEngine.Graphics.DrawMeshInstanced(
                _resolvedHologramMesh,
                0,
                _resolvedHologramMaterial,
                _hologramMatrixBuffer,
                visibleCount,
                _hologramProperties,
                ShadowCastingMode.Off,
                false,
                0,
                null,
                LightProbeUsage.Off);
        }

        private void RenderSelectedRecipeHologram(RecipeData recipe, float deltaTime)
        {
            if (_currentFabricator == null || recipe == null)
                return;

            if (_resolvedHologramMesh == null || _resolvedHologramMaterial == null)
                return;

            Transform anchor = _currentFabricator.transform;
            if (anchor == null)
                return;

            int recipeHash = ComputeItemHash(recipe.resultItem);
            float selectedSize = hologramCellSize * SelectedHologramScaleMultiplier;
            EnsureSelectedHologramBaseMatrix(anchor, recipeHash, selectedSize);
            float yawRadiansPerSecond = math.radians(math.max(1f, hologramYawBias));
            _selectedHologramYawRadians += math.max(0f, deltaTime) * yawRadiansPerSecond;
            if (_selectedHologramYawRadians > math.PI * 2f)
                _selectedHologramYawRadians -= math.PI * 2f;

            float4x4 previewMatrix = math.mul(_selectedHologramBaseMatrix, BuildYRotationMatrix(_selectedHologramYawRadians));
            Matrix4x4 previewUnityMatrix = ToMatrix4x4(in previewMatrix);
            UpdateHologramMaterialState(recipe);

            UnityEngine.Graphics.DrawMesh(
                _resolvedHologramMesh,
                previewUnityMatrix,
                _resolvedHologramMaterial,
                0,
                null,
                0,
                _hologramProperties,
                ShadowCastingMode.Off,
                false,
                null,
                LightProbeUsage.Off);
        }

        private void EnsureSelectedHologramBaseMatrix(Transform anchor, int recipeHash, float selectedSize)
        {
            if (_selectedHologramMatrixInitialized &&
                _selectedHologramRecipeHash == recipeHash &&
                ReferenceEquals(_selectedHologramAnchor, anchor) &&
                math.abs(_selectedHologramCachedSize - selectedSize) <= 0.0001f)
            {
                return;
            }

            float3 anchorPosition = ResolveSelectedHologramAnchorRuntimePosition(anchor);
            float3 anchorUp = (float3)(anchor.up);
            float3 anchorForward = (float3)(anchor.forward);
            float3 position = anchorPosition + (anchorUp * (hologramHeight + 0.28f)) + (anchorForward * 0.16f);
            quaternion rotation = quaternion.LookRotationSafe(anchorForward, anchorUp);
            _selectedHologramBaseMatrix = float4x4.TRS(position, rotation, new float3(selectedSize, selectedSize, 1f));
            _selectedHologramRecipeHash = recipeHash;
            _selectedHologramAnchor = anchor;
            _selectedHologramCachedSize = selectedSize;
            _selectedHologramYawRadians = 0f;
            _selectedHologramMatrixInitialized = true;
        }

        private float3 ResolveSelectedHologramAnchorRuntimePosition(Transform anchor)
        {
            Vector3 visualPosition = anchor.position;
            return new float3(visualPosition.x, visualPosition.y, visualPosition.z);
        }

        private static float4x4 BuildYRotationMatrix(float radians)
        {
            MathLodApproximation.ApproxSinCosBhaskara(radians, out float sinYaw, out float cosYaw);
            return new float4x4(
                new float4(cosYaw, 0f, -sinYaw, 0f),
                new float4(0f, 1f, 0f, 0f),
                new float4(sinYaw, 0f, cosYaw, 0f),
                new float4(0f, 0f, 0f, 1f));
        }

        private void UpdateHologramMaterialState(RecipeData recipe)
        {
            if (_resolvedHologramMaterial == null)
                return;

            float progress = ResolveHologramRevealProgress(recipe);
            if (math.abs(progress - _lastHologramMaterialProgress) > 0.0005f)
            {
                if (_hologramHasCraftProgress)
                    _hologramProperties.SetFloat(CraftProgressId, progress);

                if (_hologramHasScanProgress)
                    _hologramProperties.SetFloat(ScanProgressId, progress);

                _lastHologramMaterialProgress = progress;
            }

            float glitch = _currentFabricator != null && _currentFabricator.IsPausedNoPower
                ? 0.82f
                : math.lerp(0.05f, 0.28f, 1f - math.abs((progress * 2f) - 1f));
            if (_hologramHasGlitchAmount && math.abs(glitch - _lastHologramMaterialGlitch) > 0.0005f)
            {
                _hologramProperties.SetFloat(GlitchAmountId, glitch);
                _lastHologramMaterialGlitch = glitch;
            }

        }

        private float ResolveHologramRevealProgress(RecipeData recipe)
        {
            if (_currentFabricator == null || recipe == null)
                return 0f;

            if (_currentFabricator.IsCrafting && ReferenceEquals(_currentFabricator.ActiveRecipe, recipe))
                return Mathf.Clamp01(_currentFabricator.CraftingProgress01);

            return 1f;
        }

        private int BuildHologramMatrices(RecipeData recipe)
        {
            Transform anchor = _currentFabricator != null ? _currentFabricator.transform : null;
            if (anchor == null || recipe.ingredients == null)
                return 0;

            int recipeHash = ComputeItemHash(recipe.resultItem);
            int layoutVersion = ResolveHologramMatrixLayoutVersion(recipeHash);
            if (!_hologramMatrixCacheDirty &&
                _hologramMatrixRecipeHash == recipeHash &&
                _hologramMatrixLayoutVersion == layoutVersion &&
                ReferenceEquals(_hologramMatrixAnchor, anchor))
            {
                return _hologramMatrixVisibleCount;
            }

            int instanceCount = 0;
            float3 anchorRuntimePosition = ResolveSelectedHologramAnchorRuntimePosition(anchor);
            float3 anchorUp = (float3)(anchor.up);
            float3 anchorForward = (float3)(anchor.forward);
            float3 anchorPosition = anchorRuntimePosition + (anchorUp * hologramHeight);
            quaternion anchorRotation = quaternion.LookRotationSafe(anchorForward, math.up());
            int ingredientCount = recipe.ingredients.Count;

            for (int ingredientIndex = 0; ingredientIndex < ingredientCount && instanceCount < MaxVisibleHologramInstances; ingredientIndex++)
            {
                InventoryCost ingredient = recipe.ingredients[ingredientIndex];
                if (ingredient == null || ingredient.item == null || ingredient.amount <= 0)
                    continue;

                int adjustedAmount = _currentFabricator != null
                    ? _currentFabricator.CalculateAdjustedIngredientAmount(ingredient)
                    : ingredient.amount;
                int unitCount = Mathf.Clamp(adjustedAmount, 1, MaxVisibleHologramInstances - instanceCount);
                for (int unitIndex = 0; unitIndex < unitCount && instanceCount < MaxVisibleHologramInstances; unitIndex++)
                {
                    int gridColumn = instanceCount % 4;
                    int gridRow = instanceCount / 4;
                    float lateral = (gridColumn - 1.5f) * hologramSpacing;
                    float vertical = gridRow * hologramSpacing * 0.72f;
                    float3 localOffset = new float3(lateral, vertical, 0.24f + gridRow * 0.02f);
                    float3 worldPosition = anchorPosition + math.mul(anchorRotation, localOffset);
                    float4x4 matrix = float4x4.TRS(
                        worldPosition,
                        anchorRotation,
                        new float3(hologramCellSize, hologramCellSize, 1f));
                    WriteMatrix(_hologramMatrixBuffer, instanceCount, in matrix);
                    instanceCount++;
                }
            }

            _hologramMatrixRecipeHash = recipeHash;
            _hologramMatrixLayoutVersion = layoutVersion;
            _hologramMatrixAnchor = anchor;
            _hologramMatrixVisibleCount = instanceCount;
            _hologramMatrixCacheDirty = false;
            return instanceCount;
        }

        private void InvalidateHologramMatrixCache()
        {
            _hologramMatrixCacheDirty = true;
            _hologramMatrixRecipeHash = 0;
            _hologramMatrixLayoutVersion = int.MinValue;
            _hologramMatrixAnchor = null;
            _hologramMatrixVisibleCount = 0;
        }

        private int ResolveHologramMatrixLayoutVersion(int recipeHash)
        {
            unchecked
            {
                int version = recipeHash;
                version = (version * 397) ^ math.clamp(craftBatchMultiplier, 1, 99);
                version = (version * 397) ^ _lastRecipeVisualVersion;
                version = (version * 397) ^ (_currentFabricator != null && _currentFabricator.IsCrafting ? 1 : 0);

                return version;
            }
        }

        private static void WriteMatrix(Matrix4x4[] matrices, int index, in float4x4 matrix)
        {
            if (matrices == null || (uint)index >= (uint)matrices.Length)
                return;

            matrices[index] = ToMatrix4x4(in matrix);
        }

        private static Matrix4x4 ToMatrix4x4(in float4x4 matrix)
        {
            Matrix4x4 result = default;
            result.m00 = matrix.c0.x;
            result.m10 = matrix.c0.y;
            result.m20 = matrix.c0.z;
            result.m30 = matrix.c0.w;
            result.m01 = matrix.c1.x;
            result.m11 = matrix.c1.y;
            result.m21 = matrix.c1.z;
            result.m31 = matrix.c1.w;
            result.m02 = matrix.c2.x;
            result.m12 = matrix.c2.y;
            result.m22 = matrix.c2.z;
            result.m32 = matrix.c2.w;
            result.m03 = matrix.c3.x;
            result.m13 = matrix.c3.y;
            result.m23 = matrix.c3.z;
            result.m33 = matrix.c3.w;
            return result;
        }

        /// <summary>
        /// Resolves the authored hologram mesh/material pair, or reports the gap once without throwing.
        /// </summary>
        /// <remarks>
        /// The three <c>UnityEngine.Assertions.Assert</c> calls removed from this method THREW - nothing under
        /// Assets sets <c>Assert.raiseExceptions = false</c> - and each one sat directly above the graceful path
        /// that was written to handle the very same condition, making that path dead code: the
        /// <c>if (_resolvedHologramMaterial == null) return;</c> below the material assert and the
        /// <c>_resolvedHologramMaterial = null; return;</c> below the instancing assert could never be reached.
        ///
        /// The only caller is <see cref="Awake"/> (:257), which reaches this before <c>EnsureRecipeListPool()</c>
        /// (:258). An unassigned inspector slot therefore threw out of Awake and left the fabricator with no
        /// recipe list pool at all, so the crafting menu had no rows to show - a failure that looks nothing like
        /// "the hologram is missing" and sent debugging in the wrong direction.
        ///
        /// The asserts guarded nothing beyond that dead code: all three draw/update sites already null-check the
        /// resolved pair (:970, :1007, :1081).
        /// </remarks>
        private void EnsureHologramResources()
        {
            EnsureHologramPropertyBlockCold();

            if (_resolvedHologramMesh == null)
            {
                _resolvedHologramMesh = hologramMesh;
                if (_resolvedHologramMesh == null && !_missingHologramMeshAnnounced)
                {
                    _missingHologramMeshAnnounced = true;
                    LogMissingFabricatorHologramMesh();
                }
            }

            if (_resolvedHologramMaterial == null)
            {
                _resolvedHologramMaterial = hologramMaterial;
                if (_resolvedHologramMaterial == null)
                {
                    if (!_missingHologramMaterialAnnounced)
                    {
                        _missingHologramMaterialAnnounced = true;
                        LogMissingFabricatorHologramMaterial();
                    }

                    return;
                }

                bool authoredMaterialValid = _resolvedHologramMaterial.enableInstancing;
                if (!authoredMaterialValid)
                {
                    _resolvedHologramMaterial = null;
                    if (!_missingHologramMaterialAnnounced)
                    {
                        _missingHologramMaterialAnnounced = true;
                        LogFabricatorHologramMaterialNotInstanced();
                    }

                    return;
                }

                _lastHologramMaterialProgress = -1f;
                _lastHologramMaterialGlitch = -1f;
                _hologramProperties.Clear();

                CacheHologramMaterialProperties();

                if (_hologramHasBaseColor)
                    _hologramProperties.SetColor(BaseColorId, hologramColor);
                else if (_hologramHasColor)
                    _hologramProperties.SetColor(ColorId, hologramColor);

                ApplyHologramMaterialStaticState();
            }
        }

        /// <summary>
        /// One-shot report of the unassigned authored fabricator hologram mesh. Latched by the caller, and no
        /// arguments means no string work or allocation on any tick cadence.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMissingFabricatorHologramMesh()
        {
            Hecton8.Core.H8Debug.LogError("HectonFabricatorUI: serialized field 'hologramMesh' is unassigned, so the fabricator preview hologram never renders this session - all three draw sites null-guard the resolved pair and skip. The crafting menu, recipe list, input and telemetry are unaffected. Runtime mesh generation is forbidden: assign the authored hologram mesh in the inspector.");
        }

        /// <summary>
        /// One-shot report of the unassigned authored fabricator hologram material. Latched by the caller, and no
        /// arguments means no string work or allocation on any tick cadence.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMissingFabricatorHologramMaterial()
        {
            Hecton8.Core.H8Debug.LogError("HectonFabricatorUI: serialized field 'hologramMaterial' is unassigned, so the fabricator preview hologram never renders this session - all three draw sites null-guard the resolved pair and skip. The crafting menu, recipe list, input and telemetry are unaffected. Runtime material generation is forbidden: assign the authored hologram material in the inspector.");
        }

        /// <summary>
        /// One-shot report of an authored fabricator hologram material that has GPU instancing disabled. Latched by
        /// the caller, and no arguments means no string work or allocation on any tick cadence.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogFabricatorHologramMaterialNotInstanced()
        {
            Hecton8.Core.H8Debug.LogError("HectonFabricatorUI: the material assigned to 'hologramMaterial' has Enable GPU Instancing OFF, which the instanced hologram draw requires, so the resolved material is dropped and the fabricator preview hologram never renders this session. The crafting menu and recipe list are unaffected. Tick 'Enable GPU Instancing' on that material asset.");
        }

        private void EnsureHologramPropertyBlockCold()
        {
            if (_hologramProperties != null)
                return;

            _hologramProperties = new MaterialPropertyBlock();
            _lastHologramMaterialProgress = -1f;
            _lastHologramMaterialGlitch = -1f;
        }

        private void CacheHologramMaterialProperties()
        {
            if (_resolvedHologramMaterial == null)
                return;

            _hologramHasBaseColor = _resolvedHologramMaterial.HasProperty(BaseColorId);
            _hologramHasColor = _resolvedHologramMaterial.HasProperty(ColorId);
            _hologramHasCraftProgress = _resolvedHologramMaterial.HasProperty(CraftProgressId);
            _hologramHasScanProgress = _resolvedHologramMaterial.HasProperty(ScanProgressId);
            _hologramHasGlitchAmount = _resolvedHologramMaterial.HasProperty(GlitchAmountId);
            _hologramHasBobAmplitude = _resolvedHologramMaterial.HasProperty(HologramBobAmplitudeId);
            _hologramHasBobFrequency = _resolvedHologramMaterial.HasProperty(HologramBobFrequencyId);
            _hologramHasSwayAmplitude = _resolvedHologramMaterial.HasProperty(HologramSwayAmplitudeId);
            _hologramHasSwayFrequency = _resolvedHologramMaterial.HasProperty(HologramSwayFrequencyId);
            _hologramHasPulseAmplitude = _resolvedHologramMaterial.HasProperty(HologramPulseAmplitudeId);
            _hologramHasPulseFrequency = _resolvedHologramMaterial.HasProperty(HologramPulseFrequencyId);
        }

        private void ApplyHologramMaterialStaticState()
        {
            if (_resolvedHologramMaterial == null)
                return;

            if (_hologramHasBobAmplitude)
                _hologramProperties.SetFloat(HologramBobAmplitudeId, math.max(0f, hologramBobAmplitude));
            if (_hologramHasBobFrequency)
                _hologramProperties.SetFloat(HologramBobFrequencyId, math.max(0f, hologramBobFrequency));
            if (_hologramHasSwayAmplitude)
                _hologramProperties.SetFloat(HologramSwayAmplitudeId, math.max(0f, hologramOrbitAmplitude));
            if (_hologramHasSwayFrequency)
                _hologramProperties.SetFloat(HologramSwayFrequencyId, math.max(0f, hologramOrbitFrequency));
            if (_hologramHasPulseAmplitude)
                _hologramProperties.SetFloat(HologramPulseAmplitudeId, math.max(0f, hologramPulseAmplitude));
            if (_hologramHasPulseFrequency)
                _hologramProperties.SetFloat(HologramPulseFrequencyId, math.max(0f, hologramPulseFrequency));
        }

        private void BindRuntimeReferencesCold(bool allowFallbackLookup)
        {
            RefreshRuntimeReferencesHot();

            if (allowFallbackLookup &&
                playerInventory == null &&
                GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                playerTransform.TryGetComponent(out playerInventory);
            }
        }

        private void RefreshRuntimeReferencesHot()
        {
            RebindPlayerInventoryService(_playerInventoryService);
            RebindPlayerRuntimeContext(_playerRuntimeContext);
        }

        private void CacheRegistryServicesCold()
        {
            _inputService = GlobalRegistry.Input;
            _nativeInputManager = GlobalRegistry.NativeInputRuntime;
            _resourceScarcityRuntime = GlobalRegistry.ResourceScarcityReadModel;
            _localizationManager = GlobalRegistry.LocalizationText;
            RebindPlayerInventoryService(GlobalRegistry.PlayerInventory);
            RebindPlayerRuntimeContext(GlobalRegistry.Player);
        }

        private void RebindNativeInputManager(INativeInputManagerRuntime inputManager)
        {
            if (ReferenceEquals(_nativeInputManager, inputManager))
                return;

            UnsubscribeInputManager();
            _nativeInputManager = inputManager;

            if (isActiveAndEnabled)
                SubscribeInputManagerIfAvailable();
        }

        private void RebindPlayerInventoryService(IPlayerInventoryService inventoryService)
        {
            _playerInventoryService = inventoryService;
            if (inventoryService != null && inventoryService.Inventory != null)
                playerInventory = inventoryService.Inventory;
        }

        private void RebindPlayerRuntimeContext(IPlayerRuntimeContext playerContext)
        {
            _playerRuntimeContext = playerContext;
            if (hudCamera == null && playerContext != null)
                hudCamera = playerContext.PlayerCamera;
        }

        private void EnsureCanvasSplit()
        {
            staticCanvasRoot = EnsureSplitRoot(staticCanvasRoot, FabricatorStaticCanvasRootName);
            dynamicCanvasRoot = EnsureSplitRoot(dynamicCanvasRoot, FabricatorDynamicCanvasRootName);
            staticCanvas = EnsureSplitCanvas(staticCanvasRoot, staticCanvas, FabricatorStaticCanvasSortingOrder);
            dynamicCanvas = EnsureSplitCanvas(dynamicCanvasRoot, dynamicCanvas, FabricatorDynamicCanvasSortingOrder);
        }

        private Transform EnsureSplitRoot(Transform existingRoot, string rootName)
        {
            if (existingRoot != null)
                return existingRoot;

            Transform parent = transform;
            for (int childIndex = 0; childIndex < parent.childCount; childIndex++)
            {
                Transform child = parent.GetChild(childIndex);
                if (child != null && child.name == rootName)
                    return child;
            }

            GameObject rootObject = new GameObject(rootName, typeof(RectTransform)); // COLD ALLOC: GameObject[1] - fabricator canvas split root - owner: HectonFabricatorUI
            rootObject.layer = gameObject.layer;
            Transform root = rootObject.transform;
            root.SetParent(parent, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
            return root;
        }

        private static Canvas EnsureSplitCanvas(Transform root, Canvas existingCanvas, int sortingOrder)
        {
            Canvas canvas = existingCanvas;
            if (canvas == null)
            {
                if (root == null)
                    return null;

                if (!root.TryGetComponent(out canvas))
                    canvas = root.gameObject.AddComponent<Canvas>(); // COLD ALLOC: Canvas[1] - prefab authoring split root - owner: HectonFabricatorUI
            }

            canvas.renderMode = RenderMode.WorldSpace;
            canvas.pixelPerfect = false;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;

            if (canvas.TryGetComponent(out GraphicRaycaster raycaster))
                raycaster.enabled = false;

            return canvas;
        }

        private void EnsureRecipeListPool()
        {
            if (_recipeListRoot != null)
                return;

            GameObject root = new GameObject("FabricatorRecipeList"); // COLD ALLOC: GameObject[1] — diegetic recipe list root — owner: HectonFabricatorUI
            root.hideFlags = HideFlags.DontSave;
            _recipeListRoot = root.transform;
            if (dynamicCanvasRoot != null)
                _recipeListRoot.SetParent(dynamicCanvasRoot, false);

            _recipeListRoot.localScale = Vector3.one * recipeEntryScale;
            _recipeListCanvasGroup = root.AddComponent<CanvasGroup>(); // COLD ALLOC: CanvasGroup[1] - fabricator dynamic recipe visibility gate - owner: HectonFabricatorUI
            _recipeListCanvasGroup.alpha = 0f;
            _recipeListCanvasGroup.interactable = false;
            _recipeListCanvasGroup.blocksRaycasts = false;

            for (int i = 0; i < MaxVisibleRecipeEntries; i++)
            {
                GameObject entryObject = new GameObject("RecipeEntry"); // COLD ALLOC: GameObject[8] — diegetic recipe entry pool — owner: HectonFabricatorUI
                entryObject.hideFlags = HideFlags.DontSave;
                entryObject.transform.SetParent(_recipeListRoot, false);

                TextMeshPro label = entryObject.AddComponent<TextMeshPro>();
                label.fontSize = 4.2f;
                label.alignment = TextAlignmentOptions.Center;
                label.color = recipeIdleColor;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                PreallocateTextElement(label, _recipeLabelBuffer, RecipeLabelBufferCapacity);

                GameObject inflationObject = new GameObject("RecipeInflation"); // COLD ALLOC: GameObject[8] — diegetic inflation label pool — owner: HectonFabricatorUI
                inflationObject.hideFlags = HideFlags.DontSave;
                inflationObject.transform.SetParent(entryObject.transform, false);
                inflationObject.transform.localPosition = new Vector3(1.38f, 0f, 0f);

                TextMeshPro inflationLabel = inflationObject.AddComponent<TextMeshPro>();
                inflationLabel.fontSize = 3.3f;
                inflationLabel.alignment = TextAlignmentOptions.Right;
                inflationLabel.color = inflationColor;
                inflationLabel.textWrappingMode = TextWrappingModes.NoWrap;
                PreallocateTextElement(inflationLabel, _fallbackBuffer, FallbackBufferCapacity);

                WorldSpaceTMPSharpnessController sharpness = entryObject.AddComponent<WorldSpaceTMPSharpnessController>();
                sharpness.Bind(label, hudCamera);
                WorldSpaceTMPSharpnessController inflationSharpness = inflationObject.AddComponent<WorldSpaceTMPSharpnessController>();
                inflationSharpness.Bind(inflationLabel, hudCamera);

                _recipeEntries[i] = new RecipeListEntry
                {
                    Root = entryObject.transform,
                    Label = label,
                    InflationLabel = inflationLabel,
                    Sharpness = sharpness,
                    InflationSharpness = inflationSharpness,
                    CachedDisplayNameBuffer = new char[RecipeLabelBufferCapacity],
                    RecipeIndex = -1
                };
            }

            SetRecipeListVisible(false);
        }

        private static void PreallocateTextElement(TMP_Text text, char[] stagingBuffer, int maximumCharacterCount)
        {
            if (text == null || stagingBuffer == null || maximumCharacterCount <= 0)
                return;

            int length = Mathf.Min(maximumCharacterCount, stagingBuffer.Length);
            for (int i = 0; i < length; i++)
                stagingBuffer[i] = ' ';

            text.maxVisibleCharacters = length;
            text.SetCharArray(stagingBuffer, 0, length);
            text.SetCharArray(stagingBuffer, 0, 0);
        }

        private void SetRecipeListVisible(bool visible)
        {
            if (_recipeListRoot == null)
                return;

            _recipeListVisible = visible;
            if (_recipeListCanvasGroup == null)
                return;

            _recipeListCanvasGroup.alpha = visible ? 1f : 0f;
            _recipeListCanvasGroup.interactable = false;
            _recipeListCanvasGroup.blocksRaycasts = false;

            if (!visible)
            {
                _recipeListPoseValid = false;
                _recipeListAppliedScale = -1f;
                for (int i = 0; i < MaxVisibleRecipeEntries; i++)
                    SetRecipeEntryVisible(in _recipeEntries[i], false);
            }
        }

        private void UpdateRecipeListPose()
        {
            if (!_isOpen || _recipeListRoot == null || _currentFabricator == null)
                return;

            if (hudCamera == null || playerInventory == null)
                RefreshRuntimeReferencesHot();

            Transform anchor = _currentFabricator.transform;
            float3 anchorRuntimePosition = ResolveSelectedHologramAnchorRuntimePosition(anchor);
            Vector3 rootPosition = new Vector3(anchorRuntimePosition.x, anchorRuntimePosition.y, anchorRuntimePosition.z) +
                                   anchor.up * recipeListHeight +
                                   anchor.forward * recipeListForwardOffset;
            rootPosition += ResolveFailurePanelShakeOffset(anchor);
            bool poseWasValid = _recipeListPoseValid;
            if (!poseWasValid || (_recipeListRuntimePosition - rootPosition).sqrMagnitude > 0.0000001f)
                _recipeListRoot.position = rootPosition;

            if (!poseWasValid || math.abs(_recipeListAppliedScale - recipeEntryScale) > 0.000001f)
            {
                _recipeListRoot.localScale = Vector3.one * recipeEntryScale;
                _recipeListAppliedScale = recipeEntryScale;
            }

            _recipeListRuntimePosition = rootPosition;

            if (hudCamera != null)
            {
                Vector3 facing = rootPosition - hudCamera.transform.position;
                float facingSqrMagnitude = facing.sqrMagnitude;
                if (facingSqrMagnitude > 0.0001f)
                {
                    facing *= math.rsqrt(facingSqrMagnitude);
                    Quaternion targetRotation = Quaternion.LookRotation(facing, Vector3.up);
                    if (!poseWasValid || math.abs(Quaternion.Dot(_recipeListAppliedRotation, targetRotation)) < 0.999999f)
                    {
                        _recipeListRoot.rotation = targetRotation;
                        _recipeListAppliedRotation = targetRotation;
                    }
                }
            }

            _recipeListRight = _recipeListRoot.right;
            _recipeListUp = _recipeListRoot.up;
            _recipeListForward = _recipeListRoot.forward;
            Vector3 rootScale = _recipeListRoot.lossyScale;
            _recipeListInverseScale = new Vector3(
                1f / math.max(0.000001f, math.abs(rootScale.x)),
                1f / math.max(0.000001f, math.abs(rootScale.y)),
                1f / math.max(0.000001f, math.abs(rootScale.z)));
            _recipeListPoseValid = true;
        }

        private void RefreshRecipeListIfDirty()
        {
            if (!_isOpen || _recipeListRoot == null)
                return;

            int visualVersion = ResolveRecipeVisualVersion();
            if (visualVersion == _lastRecipeVisualVersion)
                return;

            _lastRecipeVisualVersion = visualVersion;
            InvalidateHologramMatrixCache();
            RebuildRecipeListEntries();
        }

        private int ResolveRecipeVisualVersion()
        {
            int recipeCount = _recipes != null ? _recipes.Count : 0;
            int inventoryVersion = playerInventory != null ? playerInventory.InventoryVersion : 0;
            int batchMultiplier = Mathf.Clamp(craftBatchMultiplier, 1, 99);
            IResourceScarcityReadModel scarcity = _resourceScarcityRuntime;
            int scarcityVersion = scarcity != null ? scarcity.RuntimeVersion : 0;
            return recipeCount ^
                   (_selectedIndex << 8) ^
                   (((int)_selectedGroup & 0xFF) << 16) ^
                   (inventoryVersion << 1) ^
                   ((_hoveredRecipeIndex + 1) << 24) ^
                   (batchMultiplier << 4) ^
                   (scarcityVersion << 5) ^
                   (_recipeLabelTextVersion << 6);
        }

        private void RebuildRecipeListEntries()
        {
            int visibleRecipeCount = _recipes != null ? Mathf.Min(_recipes.Count, MaxVisibleRecipeEntries) : 0;
            bool hasFastFailInventory = TryReadRecipeListFastFailInventory(
                out NativeArray<uint>.ReadOnly fastFailHashes,
                out NativeArray<uint>.ReadOnly fastFailQuantities,
                out int fastFailActiveSlotCount,
                out ulong fastFailInventoryMask);
            int fastFailScarcityVersion = ResolveFastFailScarcityVersion();

            for (int i = 0; i < MaxVisibleRecipeEntries; i++)
            {
                RecipeListEntry entry = _recipeEntries[i];
                if (entry.Root == null || entry.Label == null)
                    continue;

                if (i < visibleRecipeCount)
                {
                    RecipeData recipe = _recipes[i];
                    bool selected = i == _selectedIndex;
                    int safeBatchMultiplier = Mathf.Max(1, craftBatchMultiplier);
                    bool hasFastFailRequirement = TryGetRecipeListFastFailRequirement(
                        ref entry,
                        recipe,
                        safeBatchMultiplier,
                        fastFailScarcityVersion,
                        out RecipeRequirementDTO fastFailRequirement,
                        out float inflationMultiplier);
                    bool craftable = CanCraftRecipe(
                        recipe,
                        safeBatchMultiplier,
                        hasFastFailRequirement,
                        in fastFailRequirement,
                        hasFastFailInventory,
                        fastFailHashes,
                        fastFailQuantities,
                        fastFailActiveSlotCount,
                        fastFailInventoryMask);
                    bool inflated = inflationMultiplier > 1.001f;
                    entry.RecipeIndex = i;
                    entry.Root.localPosition = new Vector3(0f, -i * recipeEntrySpacing, 0f);
                    entry.Root.localRotation = Quaternion.identity;
                    entry.Root.localScale = selected ? Vector3.one * 1.08f : Vector3.one;
                    SetRecipeEntryVisible(in entry, _recipeListVisible);
                    entry.Label.color = !craftable
                        ? recipeUnavailableColor
                        : selected
                            ? recipeSelectedColor
                            : recipeIdleColor;
                    if (entry.Sharpness != null)
                        entry.Sharpness.Bind(entry.Label, hudCamera);
                    if (entry.InflationSharpness != null)
                        entry.InflationSharpness.Bind(entry.InflationLabel, hudCamera);

                    int displayNameLength = ResolveRecipeListDisplayName(ref entry, recipe);
                    ReadOnlySpan<char> displayName = entry.CachedDisplayNameBuffer != null
                        ? entry.CachedDisplayNameBuffer.AsSpan(0, displayNameLength)
                        : ReadOnlySpan<char>.Empty;
                    int length = BuildRecipeLabel(
                        entry.Label,
                        recipe,
                        displayName,
                        selected,
                        craftable);
                    entry.Label.SetCharArray(_recipeLabelBuffer, 0, length);
                    ApplyInflationLabel(entry, inflated, inflationMultiplier);
                    _recipeEntries[i] = entry;
                }
                else
                {
                    entry.RecipeIndex = -1;
                    entry.FastFailRecipe = null;
                    entry.HasFastFailRequirement = false;
                    entry.FastFailRequirementMultiplier = 0;
                    entry.FastFailScarcityVersion = 0;
                    entry.FastFailInflationMultiplier = 1f;
                    entry.FastFailRequirement = default;
                    entry.LabelRecipe = null;
                    entry.CachedDisplayNameLength = 0;
                    entry.CachedDisplayNameVersion = 0;
                    SetRecipeEntryVisible(in entry, false);
                    _recipeEntries[i] = entry;
                }
            }
        }

        private void QueueRecipeListVisibility(bool visible)
        {
            _pendingRecipeListVisibility = visible;
            _hasPendingRecipeListVisibility = true;
        }

        private void FlushPendingRecipeListVisibility()
        {
            if (!_hasPendingRecipeListVisibility)
                return;

            bool visible = _pendingRecipeListVisibility;
            _hasPendingRecipeListVisibility = false;
            SetRecipeListVisible(visible);
        }

        private int ResolveFastFailScarcityVersion()
        {
            IResourceScarcityReadModel scarcity = _resourceScarcityRuntime;
            return scarcity != null ? scarcity.RuntimeVersion : 0;
        }

        private bool TryGetRecipeListFastFailRequirement(
            ref RecipeListEntry entry,
            RecipeData recipe,
            int batchMultiplier,
            int scarcityVersion,
            out RecipeRequirementDTO requirement,
            out float inflationMultiplier)
        {
            requirement = default;
            inflationMultiplier = 1f;
            if (recipe == null)
            {
                entry.FastFailRecipe = null;
                entry.HasFastFailRequirement = false;
                entry.FastFailRequirementMultiplier = 0;
                entry.FastFailScarcityVersion = 0;
                entry.FastFailInflationMultiplier = 1f;
                entry.FastFailRequirement = default;
                return false;
            }

            int safeBatchMultiplier = batchMultiplier < 1 ? 1 : batchMultiplier;
            if (entry.HasFastFailRequirement &&
                ReferenceEquals(entry.FastFailRecipe, recipe) &&
                entry.FastFailRequirementMultiplier == safeBatchMultiplier &&
                entry.FastFailScarcityVersion == scarcityVersion)
            {
                requirement = entry.FastFailRequirement;
                inflationMultiplier = entry.FastFailInflationMultiplier;
                return true;
            }

            bool baked = _currentFabricator != null
                ? _currentFabricator.TryBuildAdjustedFastFailRequirement(recipe, safeBatchMultiplier, out requirement, out inflationMultiplier)
                : CraftingFastFailValidator.TryBuildRequirementFromRecipeData(recipe, safeBatchMultiplier, out requirement);
            if (!baked)
            {
                entry.FastFailRecipe = recipe;
                entry.FastFailRequirementMultiplier = safeBatchMultiplier;
                entry.FastFailScarcityVersion = scarcityVersion;
                entry.FastFailInflationMultiplier = 1f;
                entry.FastFailRequirement = default;
                entry.HasFastFailRequirement = false;
                inflationMultiplier = 1f;
                return false;
            }

            entry.FastFailRecipe = recipe;
            entry.FastFailRequirementMultiplier = safeBatchMultiplier;
            entry.FastFailScarcityVersion = scarcityVersion;
            entry.FastFailInflationMultiplier = math.max(1f, inflationMultiplier);
            entry.FastFailRequirement = requirement;
            entry.HasFastFailRequirement = true;
            inflationMultiplier = entry.FastFailInflationMultiplier;
            return true;
        }

        private static void SetRecipeEntryVisible(in RecipeListEntry entry, bool visible)
        {
            if (entry.Label != null && entry.Label.enabled != visible)
                entry.Label.enabled = visible;

            if (entry.InflationLabel != null && entry.InflationLabel.enabled != visible)
                entry.InflationLabel.enabled = visible;
        }

        private void ApplyInflationLabel(RecipeListEntry entry, bool inflated, float multiplier)
        {
            if (entry.InflationLabel == null)
                return;

            if (!inflated)
            {
                entry.InflationLabel.SetCharArray(_fallbackBuffer, 0, 0);
                return;
            }

            bool rented = CharBufferPool.TryAcquire(out CharBufferPool.Lease lease);
            char[] buffer = rented && lease.Buffer != null
                ? lease.Buffer
                : _fallbackBuffer;

            try
            {
                int cursor = 0;
                cursor = AppendLiteral('x', buffer, cursor);
                if (multiplier.TryFormat(buffer.AsSpan(cursor), out int written, "0.00"))
                    cursor += written;

                entry.InflationLabel.color = inflationColor;
                entry.InflationLabel.SetCharArray(buffer, 0, math.clamp(cursor, 0, buffer.Length));
            }
            finally
            {
                if (rented)
                    CharBufferPool.Release(in lease);
            }
        }

        private int ResolveRecipeListDisplayName(ref RecipeListEntry entry, RecipeData recipe)
        {
            if (recipe == null)
            {
                entry.LabelRecipe = null;
                entry.CachedDisplayNameLength = 0;
                entry.CachedDisplayNameVersion = _recipeLabelTextVersion;
                return 0;
            }

            if (entry.CachedDisplayNameBuffer == null || entry.CachedDisplayNameBuffer.Length != RecipeLabelBufferCapacity)
            {
                entry.CachedDisplayNameLength = 0;
                entry.CachedDisplayNameVersion = _recipeLabelTextVersion;
                return 0;
            }

            if (!ReferenceEquals(entry.LabelRecipe, recipe) ||
                entry.CachedDisplayNameVersion != _recipeLabelTextVersion ||
                entry.CachedDisplayNameLength <= 0)
            {
                entry.LabelRecipe = recipe;
                entry.CachedDisplayNameVersion = _recipeLabelTextVersion;
                if (!recipe.TryWriteDisplayNameOrFallback(_localizationManager, entry.CachedDisplayNameBuffer, out int nameLength))
                    nameLength = CopySpanToBuffer((recipe.recipeName ?? string.Empty).AsSpan(), entry.CachedDisplayNameBuffer);
                entry.CachedDisplayNameLength = math.clamp(nameLength, 0, entry.CachedDisplayNameBuffer.Length);
            }

            return entry.CachedDisplayNameLength;
        }

        private int BuildRecipeLabel(TMP_Text label, RecipeData recipe, ReadOnlySpan<char> displayName, bool selected, bool craftable)
        {
            int cursor = 0;
            cursor = AppendLiteral(selected ? '>' : ' ', _recipeLabelBuffer, cursor);
            cursor = AppendLiteral(' ', _recipeLabelBuffer, cursor);
            cursor = AppendLiteral(craftable ? '[' : '[', _recipeLabelBuffer, cursor);
            cursor = AppendSpan(craftable ? "OK".AsSpan() : "LOW".AsSpan(), _recipeLabelBuffer, cursor);
            cursor = AppendLiteral(']', _recipeLabelBuffer, cursor);
            cursor = AppendLiteral(' ', _recipeLabelBuffer, cursor);

            cursor = AppendSpan(displayName, _recipeLabelBuffer, cursor);

            int displayOutputQuantity = ResolveDisplayedOutputQuantity(recipe, craftBatchMultiplier);
            if (displayOutputQuantity > 1)
            {
                cursor = AppendLiteral(' ', _recipeLabelBuffer, cursor);
                cursor = AppendLiteral('x', _recipeLabelBuffer, cursor);
                if (displayOutputQuantity.TryFormat(_recipeLabelBuffer.AsSpan(cursor), out int written))
                    cursor += written;
            }

            int safeBatchMultiplier = Mathf.Max(1, craftBatchMultiplier);
            if (safeBatchMultiplier > 1)
            {
                cursor = AppendLiteral(' ', _recipeLabelBuffer, cursor);
                cursor = AppendLiteral('[', _recipeLabelBuffer, cursor);
                cursor = AppendLiteral('B', _recipeLabelBuffer, cursor);
                cursor = AppendLiteral('x', _recipeLabelBuffer, cursor);
                if (safeBatchMultiplier.TryFormat(_recipeLabelBuffer.AsSpan(cursor), out int written))
                    cursor += written;
                cursor = AppendLiteral(']', _recipeLabelBuffer, cursor);
            }

            TMP_TextRegistry.EnsureRegistered(label);
            return Mathf.Clamp(cursor, 0, _recipeLabelBuffer.Length);
        }

        private void UpdateRecipePointerSelection()
        {
            int nextHoveredRecipeIndex = -1;
            bool selectionChanged = false;
            if (_isOpen && _recipeListRoot != null && _recipeListPoseValid && hudCamera != null && _recipes != null && _recipes.Count > 0)
            {
                Transform cameraTransform = hudCamera.transform;
                Vector3 rayOrigin = cameraTransform.position;
                Vector3 rayDirection = cameraTransform.forward;
                Vector3 planeNormal = _recipeListForward;
                float denom = Vector3.Dot(planeNormal, rayDirection);
                if (math.abs(denom) > RecipePointerPlaneEpsilon)
                {
                    float distance = Vector3.Dot(_recipeListRuntimePosition - rayOrigin, planeNormal) / denom;
                    if (distance >= 0f && distance <= RecipePointerDistanceMeters)
                    {
                        Vector3 worldDelta = (rayOrigin + rayDirection * distance) - _recipeListRuntimePosition;
                        Vector3 localPoint = new Vector3(
                            Vector3.Dot(worldDelta, _recipeListRight) * _recipeListInverseScale.x,
                            Vector3.Dot(worldDelta, _recipeListUp) * _recipeListInverseScale.y,
                            Vector3.Dot(worldDelta, _recipeListForward) * _recipeListInverseScale.z);
                        int visibleRecipeCount = math.min(_recipes.Count, MaxVisibleRecipeEntries);
                        float safeSpacing = math.max(0.0001f, recipeEntrySpacing);
                        int rowIndex = (int)math.round(-localPoint.y / safeSpacing);
                        if ((uint)rowIndex < (uint)visibleRecipeCount)
                        {
                            RecipeListEntry entry = _recipeEntries[rowIndex];
                            if (entry.Root != null && entry.RecipeIndex >= 0)
                            {
                                float rowScale = entry.RecipeIndex == _selectedIndex ? 1.08f : 1f;
                                float halfWidth = RecipePointerHalfWidth * rowScale;
                                float halfHeight = RecipePointerHalfHeight * rowScale;
                                float rowCenterY = -rowIndex * safeSpacing;
                                if (math.abs(localPoint.x) <= halfWidth && math.abs(localPoint.y - rowCenterY) <= halfHeight)
                                {
                                    nextHoveredRecipeIndex = entry.RecipeIndex;
                                    if (_selectedIndex != entry.RecipeIndex)
                                    {
                                        _selectedIndex = entry.RecipeIndex;
                                        selectionChanged = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (_hoveredRecipeIndex != nextHoveredRecipeIndex)
                _hoveredRecipeIndex = nextHoveredRecipeIndex;

            if (selectionChanged)
            {
                InvalidateHologramMatrixCache();
                UpdateDiagnostics();
            }
        }

        private bool TryReadRecipeListFastFailInventory(
            out NativeArray<uint>.ReadOnly itemHashIds,
            out NativeArray<uint>.ReadOnly quantities,
            out int activeSlotCount,
            out ulong currentInventoryMask)
        {
            itemHashIds = default;
            quantities = default;
            activeSlotCount = 0;
            currentInventoryMask = 0UL;
            return playerInventory != null &&
                   playerInventory.TryReadFastFailInventorySoA(out itemHashIds, out quantities, out activeSlotCount, out currentInventoryMask);
        }

        private bool CanCraftRecipe(
            RecipeData recipe,
            int batchMultiplier,
            bool hasFastFailRequirement,
            in RecipeRequirementDTO fastFailRequirement,
            bool hasFastFailInventory,
            NativeArray<uint>.ReadOnly fastFailHashes,
            NativeArray<uint>.ReadOnly fastFailQuantities,
            int fastFailActiveSlotCount,
            ulong fastFailInventoryMask)
        {
            if (recipe == null || recipe.ingredients == null || playerInventory == null)
                return false;

            if (_currentFabricator != null)
            {
                if (hasFastFailInventory &&
                    hasFastFailRequirement &&
                    _currentFabricator.TryCanCraftFastFailPresentation(
                        recipe,
                        batchMultiplier,
                        in fastFailRequirement,
                        fastFailHashes,
                        fastFailQuantities,
                        fastFailActiveSlotCount,
                        fastFailInventoryMask,
                        out bool craftable))
                {
                    return craftable;
                }

                return _currentFabricator.CanCraft(recipe, batchMultiplier);
            }

            if (hasFastFailInventory &&
                hasFastFailRequirement &&
                TryCanCraftRecipeFastFailFallback(
                    recipe,
                    in fastFailRequirement,
                    fastFailHashes,
                    fastFailQuantities,
                    fastFailActiveSlotCount,
                    fastFailInventoryMask,
                    out bool fallbackCraftable))
            {
                return fallbackCraftable;
            }

            InventoryGrid grid = playerInventory.Grid;
            NativeArray<int>.ReadOnly anchorHashIds = grid != null ? grid.AnchorHashIds : default;
            NativeArray<ushort>.ReadOnly stackCounts = playerInventory.GetStackCountsReadOnly();
            NativeArray<ushort>.ReadOnly craftLockedCounts = playerInventory.GetCraftLockedCountsReadOnly();
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
                int requiredAmount = Mathf.Max(1, ingredient.amount) * batchMultiplier;
                for (int anchorIndex = 0; anchorIndex < anchorCount; anchorIndex++)
                {
                    if (anchorHashIds[anchorIndex] != itemHashId)
                        continue;

                    int availableStack = craftLockedCounts.IsCreated && anchorIndex < craftLockedCounts.Length
                        ? Mathf.Max(0, (int)stackCounts[anchorIndex] - craftLockedCounts[anchorIndex])
                        : stackCounts[anchorIndex];
                    availableCount += availableStack;
                    if (availableCount >= requiredAmount)
                        break;
                }

                if (availableCount < requiredAmount)
                    return false;
            }

            return true;
        }

        private bool TryCanCraftRecipeFastFailFallback(
            RecipeData recipe,
            in RecipeRequirementDTO requirement,
            NativeArray<uint>.ReadOnly fastFailHashes,
            NativeArray<uint>.ReadOnly fastFailQuantities,
            int fastFailActiveSlotCount,
            ulong fastFailInventoryMask,
            out bool craftable)
        {
            craftable = false;
            if (recipe == null || recipe.resultItem == null)
                return false;

            ulong unlockedMask = CraftingFastFailValidator.NormalizePlayerUnlockMask(requirement.BlueprintUnlockMask);
            craftable = CraftingFastFailValidator.TryEvaluateRecipeAvailability(
                in requirement,
                fastFailHashes,
                fastFailQuantities,
                fastFailActiveSlotCount,
                fastFailInventoryMask,
                unlockedMask,
                out CraftingFastFailStatus status,
                out _);
            return status != CraftingFastFailStatus.InvalidInput;
        }

        private static int ComputeItemHash(ItemData item)
        {
            return item != null ? item.PersistentHashId : 0;
        }

        private void AdvanceFailurePanelShake(float deltaTime)
        {
            if (!(_failurePanelShakeRemainingSeconds > 0f))
                return;

            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            _failurePanelShakeRemainingSeconds = Mathf.Max(0f, _failurePanelShakeRemainingSeconds - safeDeltaTime);
            _failurePanelShakeElapsedSeconds += safeDeltaTime;
        }

        private Vector3 ResolveFailurePanelShakeOffset(Transform anchor)
        {
            if (anchor == null || !(_failurePanelShakeRemainingSeconds > 0f))
                return Vector3.zero;

            float duration = Mathf.Max(0.001f, failurePanelShakeDurationSeconds);
            float intensity = Mathf.Clamp01(_failurePanelShakeRemainingSeconds / duration);
            float phase = _failurePanelShakeElapsedSeconds * Mathf.Max(0f, failurePanelShakeFrequencyHz) * Mathf.PI * 2f;
            float lateral = FastSignedTriangleWave(phase) * failurePanelShakeAmplitudeMeters * intensity;
            float vertical = FastSignedTriangleWave(phase * 1.73f) * failurePanelShakeAmplitudeMeters * 0.35f * intensity;
            return anchor.right * lateral + anchor.up * vertical;
        }

        private static float FastSignedTriangleWave(float phase)
        {
            float normalized = math.frac(phase * InverseTwoPi);
            return 1f - (4f * math.abs(normalized - 0.5f));
        }

        private static int ResolveDisplayedOutputQuantity(RecipeData recipe, int multiplier)
        {
            if (recipe == null)
                return 0;

            long quantity = (long)Mathf.Max(1, recipe.resultQuantity) * Mathf.Max(1, multiplier);
            return quantity > int.MaxValue ? int.MaxValue : (int)quantity;
        }

        private static int ResolveNextBatchMultiplier(int currentMultiplier, int direction)
        {
            int current = Mathf.Max(1, currentMultiplier);
            if (direction >= 0)
                return current < 5 ? 5 : 1;

            return current > 1 ? 1 : 5;
        }

        private static int AppendLiteral(char value, char[] buffer, int cursor)
        {
            if ((uint)cursor >= (uint)buffer.Length)
                return cursor;

            buffer[cursor] = value;
            return cursor + 1;
        }

        private static int AppendSpan(ReadOnlySpan<char> value, char[] buffer, int cursor)
        {
            if (value.Length == 0 || cursor >= buffer.Length)
                return cursor;

            int writable = Mathf.Min(value.Length, buffer.Length - cursor);
            value.Slice(0, writable).CopyTo(buffer.AsSpan(cursor, writable));
            return cursor + writable;
        }

        private static int CopySpanToBuffer(ReadOnlySpan<char> value, char[] buffer)
        {
            if (buffer == null || value.Length == 0)
                return 0;

            int writable = Mathf.Min(value.Length, buffer.Length);
            value.Slice(0, writable).CopyTo(buffer.AsSpan(0, writable));
            return writable;
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
