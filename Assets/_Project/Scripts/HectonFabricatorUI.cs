// ============================================================================
// HECTON-8 — HectonFabricatorUI.cs
// NASA-Punk Fabricator UI на базе библиотеки Shapes (Immediate Mode).
//
// ОТВЕТСТВЕННОСТИ:
//   1. Подписка на CraftingEvents: Open/Close/Progress/Complete.
//   2. Навигация по рецептам (W/S, стрелки).
//   3. Запуск крафта (Space/Enter).
//   4. Отмена / закрытие (Escape).
//   5. Отрисовка в Immediate Mode через Shapes DrawShapes.
//   6. Блокировка игрового ввода через IsMenuOpen.
//
// АРХИТЕКТУРА:
//   • ImmediateModeShapeDrawer — базовый класс Shapes для URP-рендера.
//   • ITickable — обработка ввода через GameTickManager.
//   • Event-Driven: UI обновляется только при событиях (открытие,
//     смена выбора, прогресс крафта).
//   • Immediate Mode: каждый кадр перерисовывается полностью
//     через Shapes API (нет persistent UI objects).
//
// ZERO GC:
//   • Pre-cached string arrays: _percentStrings[101], _numStrings[100].
//   • Ingredient status strings пересоздаются ТОЛЬКО при смене выбора
//     (W/S press), не каждый кадр.
//   • Draw.Color/Thickness — value types, zero GC.
//   • Scr() helper — struct math (Vector3), zero GC.
//   • Никаких foreach, LINQ, string concatenation в DrawShapes.
//
// ВИЗУАЛЬНЫЙ СТИЛЬ (NASA-Punk):
//   • Тонкие линии (1-2px), угловатые рамки.
//   • Цветовая палитра: Cyan primary, Amber accent, Red warning.
//   • Пульсирующие bracket-ы вокруг выбранного элемента.
//   • Corner decorations на панелях (L-shaped marks).
//   • Сегментированный progress bar с процентами.
//   • Scan-line эффект на фоне панели.
//
// ЗАВИСИМОСТИ:
//   • Shapes (Freya Holmér) — NuGet/Asset Store.
//   • TextMeshPro — для шрифтов (TMP_FontAsset).
//   • GameTickManager — ITickable registration.
//   • CraftingEvents — статические события крафта.
//   • PlayerInventory — подсчёт ингредиентов.
//
// ПРЕДПОЛАГАЕМЫЕ ВНЕШНИЕ ТИПЫ (определены в проекте):
//
//   namespace Hecton8.Crafting {
//     public static class CraftingEvents {
//       public static event Action<IFabricator> OnFabricatorOpened;
//       public static event Action OnFabricatorClosed;
//       public static event Action<float> OnCraftProgressUpdated; // [0..1]
//       public static event Action<RecipeData> OnCraftCompleted;
//     }
//     public interface IFabricator {
//       List<RecipeData> AvailableRecipes { get; }
//       bool IsCrafting { get; }
//       void StartCraft(RecipeData recipe);
//       void CancelCraft();
//     }
//     [CreateAssetMenu] public class RecipeData : ScriptableObject {
//       public string recipeName;
//       public Sprite icon;
//       public float craftTime;
//       public List<RecipeIngredient> ingredients;
//       public ItemData result;
//     }
//     [Serializable] public struct RecipeIngredient {
//       public ItemData item;
//       public int amount;
//     }
//   }
// ============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using Hecton8.Core;
using Hecton8.Crafting;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Building;
using Shapes;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Hecton.Localization;
using Hecton8.Input;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    public sealed class HectonFabricatorUI : ImmediateModeShapeDrawer, ITickable
    {
        // ══════════════════════════════════════════════════════════
        //  STATIC — PRE-CACHED STRINGS (allocated once at class load)
        // ══════════════════════════════════════════════════════════

        /// <summary>Pre-cached percentage strings "0%" → "100%". Zero GC at runtime.</summary>
        private static readonly string[] PercentStrings;

        /// <summary>Pre-cached integer strings "0" → "99". Zero GC at runtime.</summary>
        private static readonly string[] NumStrings;

        /// <summary>Localized labels — string, zero GC.</summary>
        private string LabelFabricator  = "FABRICATOR";
        private string LabelRecipes     = "RECIPES";
        private string LabelDetails     = "BLUEPRINT";
        private string LabelIngredients = "REQUIRED MATERIALS";
        private string LabelCraftTime   = "FABRICATION TIME";
        private string LabelResult      = "OUTPUT";
        private string LabelCrafting    = "FABRICATING...";
        private string LabelHintNav     = "[W/S] NAVIGATE";
        private string LabelHintCraft   = "[SPACE] FABRICATE";
        private string LabelHintClose   = "[ESC] CLOSE";
        private string LabelNoRecipes   = "NO BLUEPRINTS AVAILABLE";
        private string LabelInsufficient = "INSUFFICIENT";
        private string LabelReady       = "READY";
        private const string LabelSeconds     = "s";
        private const string LabelSlash       = "/";
        private const string LabelDot         = "\u2022"; // bullet •

        static HectonFabricatorUI()
        {
            // ── Percentage strings ──
            PercentStrings = new string[101];
            for (int i = 0; i <= 100; i++)
                PercentStrings[i] = i + "%";

            // ── Number strings ──
            NumStrings = new string[100];
            for (int i = 0; i < 100; i++)
                NumStrings[i] = i.ToString();
        }

        // ══════════════════════════════════════════════════════════
        //  COLOR PALETTE — NASA-Punk
        // ══════════════════════════════════════════════════════════

        private static readonly Color ColorPrimary     = new Color(0.00f, 0.83f, 1.00f, 1.00f); // Cyan
        private static readonly Color ColorPrimaryDim   = new Color(0.00f, 0.50f, 0.65f, 0.70f);
        private static readonly Color ColorAccent      = new Color(1.00f, 0.72f, 0.00f, 1.00f); // Amber
        private static readonly Color ColorWarning     = new Color(1.00f, 0.20f, 0.20f, 1.00f); // Red
        private static readonly Color ColorSuccess     = new Color(0.20f, 1.00f, 0.40f, 1.00f); // Green
        private static readonly Color ColorText        = new Color(0.75f, 0.78f, 0.82f, 1.00f); // Light grey
        private static readonly Color ColorTextBright  = new Color(1.00f, 1.00f, 1.00f, 1.00f); // White
        private static readonly Color ColorTextDim     = new Color(0.35f, 0.40f, 0.50f, 1.00f);
        private static readonly Color ColorBg          = new Color(0.03f, 0.05f, 0.10f, 0.88f); // Dark blue
        private static readonly Color ColorBgPanel     = new Color(0.05f, 0.08f, 0.14f, 0.75f);
        private static readonly Color ColorScanline    = new Color(0.10f, 0.15f, 0.25f, 0.12f);
        private static readonly Color ColorProgress    = new Color(0.00f, 0.83f, 1.00f, 0.80f);
        private static readonly Color ColorProgressBg  = new Color(0.10f, 0.12f, 0.18f, 0.90f);

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Camera ────────────────────────────────────")]
        [Tooltip("HUD Camera для отрисовки интерфейса. " +
                 "Если null — используется Camera.main.")]
        [SerializeField] private Camera hudCamera;

        [Header("── Font ──────────────────────────────────────")]
        [Tooltip("TMP_FontAsset для текста (моноширинный рекомендуется).")]
        [SerializeField] private TMP_FontAsset font;

        [Header("── References ────────────────────────────────")]
        [Tooltip("Инвентарь игрока для проверки ингредиентов.")]
        [SerializeField] private PlayerInventory playerInventory;

        [Header("── Layout ────────────────────────────────────")]
        [Tooltip("Размер шрифта для заголовков (в пикселях).")]
        [SerializeField] private float fontSizeHeader = 22f;
        [Tooltip("Размер шрифта для основного текста.")]
        [SerializeField] private float fontSizeBody = 14f;
        [Tooltip("Размер шрифта для подсказок управления.")]
        [SerializeField] private float fontSizeHint = 11f;
        [Tooltip("Расстояние между элементами списка (пиксели).")]
        [SerializeField] private float listItemSpacing = 28f;

        [Header("── Animation ─────────────────────────────────")]
        [Tooltip("Скорость пульсации bracket-ов (рад/сек).")]
        [SerializeField] private float bracketPulseSpeed = 4f;
        [Tooltip("Амплитуда пульсации bracket-ов (пиксели).")]
        [SerializeField] private float bracketPulseAmplitude = 3f;
        [Tooltip("Скорость сканирующей линии (пиксели/сек).")]
        [SerializeField] private float scanlineSpeed = 60f;

        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private bool _debugIsOpen;
        [SerializeField] private int  _debugSelectedIndex;

        // ══════════════════════════════════════════════════════════
        //  STATIC — GLOBAL UI STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Статический флаг: открыто ли полноэкранное меню.
        /// Все системы ввода (PlayerToolManager, PlayerMovement, etc.)
        /// должны проверять этот флаг перед обработкой игрового ввода.
        /// </summary>
        public static bool IsMenuOpen { get; private set; }

        // ══════════════════════════════════════════════════════════
        //  CACHED STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>Текущий фабрикатор (установлен при открытии).</summary>
        private Fabricator _currentFabricator;

        /// <summary>Список рецептов текущего фабрикатора.</summary>
        private IReadOnlyList<RecipeData> _recipes;

        /// <summary>Индекс выбранного рецепта.</summary>
        private int _selectedIndex;

        /// <summary>Меню открыто.</summary>
        private bool _isOpen;

        /// <summary>Идёт ли крафт.</summary>
        private bool _isCrafting;

        /// <summary>Прогресс крафта [0..1].</summary>
        private float _craftProgress;

        // ── Screen-space transform (re-computed each DrawShapes) ──
        private Vector3 _scrOrigin; // bottom-left world pos
        private Vector3 _scrRight;  // world units per pixel X
        private Vector3 _scrUp;     // world units per pixel Y
        private float   _worldPerPx; // average world-units per pixel

        // ── Screen dimensions (cached per frame) ──
        private float _sw; // screen width in pixels
        private float _sh; // screen height in pixels

        // ── Ingredient cache (rebuilt on selection change) ──
        private string[] _ingredientNameCache;
        private string[] _ingredientStatusCache; // "2/3"
        private bool[]   _ingredientSufficient;
        private int      _ingredientCacheCount;
        private string   _craftTimeCache;        // "5.0s"
        private string   _resultNameCache;       // "Hull Panel"
        private bool     _canCraftCurrent;       // all ingredients sufficient

        // ── StringBuilder for non-per-frame string building ──
        private StringBuilder _sb;

        // ── Input debounce ──
        private bool _navUpPressed;
        private bool _navDownPressed;
        private bool _confirmPressed;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _sb = new StringBuilder(64);

            // Pre-allocate ingredient cache arrays
            _ingredientNameCache   = new string[16];
            _ingredientStatusCache = new string[16];
            _ingredientSufficient  = new bool[16];

            if (hudCamera == null)
                hudCamera = Camera.main;
        }

        public override void OnEnable()
        {
            base.OnEnable();
            GameTickManager.Instance?.Register((ITickable)this);

            // ── Subscribe to InputManager events ──
            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnNavigate += HandleNavigateInput;
                InputManager.Instance.OnSubmit   += HandleSubmitInput;
                InputManager.Instance.OnCancel   += HandleCancelInput;
            }

            RebindingManager.Instance.OnRebindCompleted += HandleRebindCompleted;
            RebindingManager.Instance.OnRebindCanceled += HandleRebindCanceled;
            RebindingManager.Instance.OnOverridesLoaded += HandleRebindOverridesChanged;
            RebindingManager.Instance.OnOverridesCleared += HandleRebindOverridesChanged;

            // ── Subscribe to explicit UI texts ──
            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
            if (LocalizationManager.Instance != null)
                HandleLanguageChanged(LocalizationManager.Instance.CurrentLanguage);

            // ── Subscribe to crafting events ──
            CraftingEvents.OnFabricatorOpened    += HandleFabricatorOpened;
            CraftingEvents.OnFabricatorClosed    += HandleFabricatorClosed;
            CraftingEvents.OnCraftProgressUpdated += HandleCraftProgress;
            CraftingEvents.OnCraftCompleted       += HandleCraftCompleted;
        }

        private void HandleLanguageChanged(GameLanguage lang)
        {
            var loc = LocalizationManager.Instance;
            if (loc == null) return;
            
            LabelFabricator = loc.Get(LocalizationKeys.UI_FABRICATOR);
            LabelRecipes = loc.Get(LocalizationKeys.UI_RECIPES);
            LabelDetails = loc.Get(LocalizationKeys.UI_BLUEPRINT);
            LabelIngredients = loc.Get(LocalizationKeys.UI_REQUIRED_MATERIALS);
            LabelCraftTime = loc.Get(LocalizationKeys.UI_FABRICATION_TIME);
            LabelResult = loc.Get(LocalizationKeys.UI_OUTPUT);
            LabelCrafting = loc.Get(LocalizationKeys.UI_FABRICATING);
            LabelHintNav = loc.Get(LocalizationKeys.UI_HINT_NAVIGATE);
            LabelHintCraft = loc.Get(LocalizationKeys.UI_HINT_FABRICATE);
            LabelHintClose = loc.Get(LocalizationKeys.UI_HINT_CLOSE);
            LabelNoRecipes = loc.Get(LocalizationKeys.UI_NO_BLUEPRINTS);
            LabelInsufficient = loc.Get(LocalizationKeys.UI_INSUFFICIENT);
            LabelReady = loc.Get(LocalizationKeys.UI_READY);
            
            // Rebuild string caches if they are currently displaying something that might have altered.
            if (_recipes != null && _recipes.Count > 0 && _isOpen)
                RebuildIngredientCache();

            // Update input hints as language might change key names
            UpdateInputHints();
        }

        public override void OnDisable()
        {
            base.OnDisable();
            GameTickManager.Instance?.Unregister((ITickable)this);

            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;

            // ── Unsubscribe from InputManager events ──
            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnNavigate -= HandleNavigateInput;
                InputManager.Instance.OnSubmit   -= HandleSubmitInput;
                InputManager.Instance.OnCancel   -= HandleCancelInput;
            }

            RebindingManager.Instance.OnRebindCompleted -= HandleRebindCompleted;
            RebindingManager.Instance.OnRebindCanceled -= HandleRebindCanceled;
            RebindingManager.Instance.OnOverridesLoaded -= HandleRebindOverridesChanged;
            RebindingManager.Instance.OnOverridesCleared -= HandleRebindOverridesChanged;

            CraftingEvents.OnFabricatorOpened    -= HandleFabricatorOpened;
            CraftingEvents.OnFabricatorClosed    -= HandleFabricatorClosed;
            CraftingEvents.OnCraftProgressUpdated -= HandleCraftProgress;
            CraftingEvents.OnCraftCompleted       -= HandleCraftCompleted;

            // ── Safety: close menu if component disabled ──
            if (_isOpen)
                CloseMenu();
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable — INPUT HANDLING
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Обработка навигационного ввода.
        /// Вызывается каждый кадр через GameTickManager.
        ///
        /// Input обрабатывается ТОЛЬКО когда меню открыто.
        /// Используется debounce: действие при GetKeyDown,
        /// сброс при GetKeyUp.
        /// </summary>
        public void Tick(float deltaTime)
        {
            // Input is now handled via HandleNavigateInput, etc. callbacks.

            UpdateDiagnostics();
        }

        // ══════════════════════════════════════════════════════════
        //  EVENT HANDLERS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вызывается при открытии фабрикатора (игрок взаимодействовал).
        /// Инициализирует UI: загружает рецепты, кэширует данные.
        /// </summary>
        private void HandleNavigateInput(Vector2 direction)
        {
            if (!_isOpen || _isCrafting || _recipes == null || _recipes.Count == 0)
                return;

            if (Mathf.Abs(direction.y) < 0.5f)
                return;

            int nextIndex = _selectedIndex + (direction.y > 0f ? -1 : 1);
            SetSelectedIndex(nextIndex);
        }

        private void HandleSubmitInput()
        {
            if (!_isOpen || _isCrafting || _recipes == null || _recipes.Count == 0)
                return;

            if (_selectedIndex < 0 || _selectedIndex >= _recipes.Count)
                return;

            if (!_canCraftCurrent || _currentFabricator == null)
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
                _currentFabricator?.CancelCraft();
                return;
            }

            CloseMenu();
        }

        private void UpdateInputHints()
        {
            string navigateBinding = InputManager.Instance != null
                ? InputManager.Instance.GetBindingDisplayString("Navigate")
                : "W/S";
            string submitBinding = InputManager.Instance != null
                ? InputManager.Instance.GetBindingDisplayString("Submit")
                : "Space";
            string cancelBinding = InputManager.Instance != null
                ? InputManager.Instance.GetBindingDisplayString("Cancel")
                : "Esc";

            LabelHintNav = $"[{navigateBinding}] NAVIGATE";
            LabelHintCraft = $"[{submitBinding}] FABRICATE";
            LabelHintClose = $"[{cancelBinding}] CLOSE";
        }

        private void HandleRebindCompleted(string actionName, string actionMap, int bindingIndex, string display)
        {
            if (!_isOpen) return;
            if (!string.Equals(actionMap, "UI", StringComparison.OrdinalIgnoreCase)) return;
            UpdateInputHints();
        }

        private void HandleRebindCanceled(string actionName, string actionMap, int bindingIndex)
        {
            if (!_isOpen) return;
            if (!string.Equals(actionMap, "UI", StringComparison.OrdinalIgnoreCase)) return;
            UpdateInputHints();
        }

        private void HandleRebindOverridesChanged()
        {
            if (!_isOpen) return;
            UpdateInputHints();
        }

        private void SetSelectedIndex(int nextIndex)
        {
            if (_recipes == null || _recipes.Count == 0)
                return;

            int clamped = Mathf.Clamp(nextIndex, 0, _recipes.Count - 1);
            if (_selectedIndex == clamped)
                return;

            _selectedIndex = clamped;
            RebuildIngredientCache();
        }

        private void HandleFabricatorOpened(Fabricator fabricator)
        {
            if (fabricator == null) return;

            _currentFabricator = fabricator;
            _recipes           = fabricator.AvailableRecipes;
            _selectedIndex     = 0;
            _isCrafting        = false;
            _craftProgress     = 0f;

            _isOpen    = true;
            IsMenuOpen = true;

            // ── Switch to UI input map ──
            if (InputManager.Instance != null)
            {
                InputManager.Instance.SwitchToUIInput();
                UpdateInputHints();
            }

            // ── Unlock cursor for menu ──
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = false; // NASA-Punk: no cursor, only keyboard

            // ── Build initial cache ──
            if (_recipes != null && _recipes.Count > 0)
                RebuildIngredientCache();

            UpdateDiagnostics();
        }

        /// <summary>
        /// Вызывается при закрытии фабрикатора (игрок отошёл или нажал ESC).
        /// </summary>
        private void HandleFabricatorClosed()
        {
            CloseMenu();
        }

        /// <summary>
        /// Обновление прогресса крафта [0..1].
        /// </summary>
        private void HandleCraftProgress(float progress)
        {
            _craftProgress = Mathf.Clamp01(progress);
        }

        /// <summary>
        /// Крафт завершён. Сбрасываем состояние.
        /// </summary>
        private void HandleCraftCompleted(ItemData recipe)
        {
            _isCrafting    = false;
            _craftProgress = 0f;

            // Пересчитываем ингредиенты (ресурсы списались)
            RebuildIngredientCache();
        }

        /// <summary>
        /// Закрывает меню и восстанавливает игровое управление.
        /// </summary>
        private void CloseMenu()
        {
            _isOpen            = false;
            IsMenuOpen         = false;
            _currentFabricator = null;
            _recipes           = null;
            _isCrafting        = false;
            _craftProgress     = 0f;

            // ── Restore cursor for gameplay ──
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;

            UpdateDiagnostics();
        }

        // ══════════════════════════════════════════════════════════
        //  SHAPES DRAWING — MAIN ENTRY POINT
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вызывается Shapes для каждой камеры каждый кадр.
        /// Фильтруем по HUD камере. Рисуем только когда меню открыто.
        ///
        /// Вся отрисовка — Immediate Mode через Shapes API.
        /// Никаких GameObjects, Canvas, RectTransform.
        /// </summary>
        public override void DrawShapes(Camera cam)
        {
            // ── Filter: only HUD camera ──
            if (cam != hudCamera) return;
            if (!_isOpen) return;

            using (Draw.Command(cam))
            {
                // ── Setup ──
                Draw.ZTest    = CompareFunction.Always;
                Draw.BlendMode = ShapesBlendMode.Transparent;

                if (font != null)
                    Draw.Font = font;

                SetupScreenSpace(cam);

                // ── Draw layers (back to front) ──
                DrawBackground();
                DrawScanlines();
                DrawPanelFrame();
                DrawHeader();
                DrawRecipeList();
                DrawRecipeDetails();

                if (_isCrafting)
                    DrawProgressBar();

                DrawControlHints();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  SCREEN SPACE SETUP
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вычисляет трансформацию pixel → world space.
        /// Вызывается один раз per frame в DrawShapes.
        ///
        /// 3 вызова ScreenToWorldPoint — основная стоимость.
        /// Все последующие Scr() используют только struct math.
        /// </summary>
        private void SetupScreenSpace(Camera cam)
        {
            _sw = cam.pixelWidth;
            _sh = cam.pixelHeight;

            float z = cam.nearClipPlane + 0.01f;

            Vector3 bl = cam.ScreenToWorldPoint(new Vector3(0f, 0f, z));
            Vector3 br = cam.ScreenToWorldPoint(new Vector3(_sw, 0f, z));
            Vector3 tl = cam.ScreenToWorldPoint(new Vector3(0f, _sh, z));

            _scrOrigin  = bl;
            _scrRight   = (br - bl) / _sw;
            _scrUp      = (tl - bl) / _sh;
            _worldPerPx = (_scrRight.magnitude + _scrUp.magnitude) * 0.5f;
        }

        /// <summary>Pixel coordinates → World position. Zero GC.</summary>
        private Vector3 Scr(float px, float py)
        {
            return _scrOrigin + _scrRight * px + _scrUp * py;
        }

        /// <summary>Font size in pixels → world units. Zero GC.</summary>
        private float FontW(float pxSize) => pxSize * _worldPerPx;

        /// <summary>Size in pixels → world units (X axis).</summary>
        private float PxW(float px) => _scrRight.magnitude * px;

        /// <summary>Size in pixels → world units (Y axis).</summary>
        private float PxH(float px) => _scrUp.magnitude * px;

        // ══════════════════════════════════════════════════════════
        //  LAYOUT HELPERS
        // ══════════════════════════════════════════════════════════

        // All layout values as fractions of screen size.
        // Computed fresh each frame from _sw/_sh (resolution-independent).

        private float PanelX => _sw * 0.15f;
        private float PanelY => _sw > 0 ? _sh * 0.10f : 0;
        private float PanelW => _sw * 0.70f;
        private float PanelH => _sh * 0.80f;
        private float PanelR => PanelX + PanelW;
        private float PanelT => PanelY + PanelH;

        private float ListX     => PanelX + _sw * 0.02f;
        private float ListW     => PanelW * 0.35f;
        private float DetailX   => PanelX + PanelW * 0.40f;
        private float DetailW   => PanelW * 0.55f;
        private float ContentY  => PanelY + _sh * 0.08f;
        private float ContentT  => PanelT - _sh * 0.08f;

        // ══════════════════════════════════════════════════════════
        //  DRAWING — BACKGROUND
        // ══════════════════════════════════════════════════════════

        private void DrawBackground()
        {
            // ── Semi-transparent dark panel ──
            Draw.Color = ColorBg;
            Draw.Rectangle(
                Scr(PanelX + PanelW * 0.5f, PanelY + PanelH * 0.5f),
                PxW(PanelW), PxH(PanelH));
        }

        // ══════════════════════════════════════════════════════════
        //  DRAWING — SCANLINES (NASA-Punk atmosphere)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Рисует горизонтальные scan-lines поверх фона.
        /// Создаёт атмосферу CRT/visor дисплея.
        /// Scan-lines медленно движутся вверх.
        /// </summary>
        private void DrawScanlines()
        {
            Draw.Color = ColorScanline;

            float spacing  = 4f;          // каждые 4 пикселя
            float offset   = (Time.unscaledTime * scanlineSpeed) % spacing;
            float thickness = 1f * _worldPerPx;

            float y = PanelY + offset;
            float endY = PanelT;

            while (y < endY)
            {
                Draw.Line(Scr(PanelX, y), Scr(PanelR, y), thickness);
                y += spacing;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  DRAWING — PANEL FRAME (angular corners)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Рисует угловатые L-shaped marks на углах панели.
        /// NASA-Punk: вместо скруглённых углов — резкие засечки.
        /// </summary>
        private void DrawPanelFrame()
        {
            Draw.Color = ColorPrimary;
            float t  = 1.5f * _worldPerPx; // line thickness
            float cl = 25f; // corner leg length (px)

            // ── Top-Left ──
            Draw.Line(Scr(PanelX, PanelT), Scr(PanelX + cl, PanelT), t);
            Draw.Line(Scr(PanelX, PanelT), Scr(PanelX, PanelT - cl), t);

            // ── Top-Right ──
            Draw.Line(Scr(PanelR, PanelT), Scr(PanelR - cl, PanelT), t);
            Draw.Line(Scr(PanelR, PanelT), Scr(PanelR, PanelT - cl), t);

            // ── Bottom-Left ──
            Draw.Line(Scr(PanelX, PanelY), Scr(PanelX + cl, PanelY), t);
            Draw.Line(Scr(PanelX, PanelY), Scr(PanelX, PanelY + cl), t);

            // ── Bottom-Right ──
            Draw.Line(Scr(PanelR, PanelY), Scr(PanelR - cl, PanelY), t);
            Draw.Line(Scr(PanelR, PanelY), Scr(PanelR, PanelY + cl), t);

            // ── Vertical divider (list | details) ──
            float divX = PanelX + PanelW * 0.38f;
            Draw.Color = ColorPrimaryDim;
            Draw.Line(Scr(divX, ContentY), Scr(divX, ContentT), 1f * _worldPerPx);
        }

        // ══════════════════════════════════════════════════════════
        //  DRAWING — HEADER
        // ══════════════════════════════════════════════════════════

        private void DrawHeader()
        {
            float headerY = PanelT - _sh * 0.04f;
            float centerX = PanelX + PanelW * 0.5f;

            // ── Title ──
            Draw.Color     = ColorPrimary;
            Draw.FontSize  = FontW(fontSizeHeader);
            Draw.TextAlign = TextAlign.Center;
            Draw.Text(Scr(centerX, headerY), LabelFabricator);

            // ── Underline ──
            float lineY = headerY - fontSizeHeader * 0.8f;
            float lineHalfW = PanelW * 0.35f;
            Draw.Color = ColorPrimary;
            Draw.Line(
                Scr(centerX - lineHalfW, lineY),
                Scr(centerX + lineHalfW, lineY),
                1f * _worldPerPx);

            // ── Decorative dots at line ends ──
            Draw.Disc(Scr(centerX - lineHalfW, lineY), 2f * _worldPerPx);
            Draw.Disc(Scr(centerX + lineHalfW, lineY), 2f * _worldPerPx);

            // ── Section labels ──
            Draw.FontSize  = FontW(fontSizeBody * 0.9f);
            Draw.TextAlign = TextAlign.Left;

            float sectionY = ContentT + fontSizeBody;

            Draw.Color = ColorTextDim;
            Draw.Text(Scr(ListX, sectionY), LabelRecipes);

            Draw.Text(Scr(DetailX, sectionY), LabelDetails);
        }

        // ══════════════════════════════════════════════════════════
        //  DRAWING — RECIPE LIST
        // ══════════════════════════════════════════════════════════

        private void DrawRecipeList()
        {
            if (_recipes == null || _recipes.Count == 0)
            {
                Draw.Color     = ColorTextDim;
                Draw.FontSize  = FontW(fontSizeBody);
                Draw.TextAlign = TextAlign.Left;
                Draw.Text(Scr(ListX + 10f, ContentT - 40f), LabelNoRecipes);
                return;
            }

            float startY = ContentT - 10f;
            int count = _recipes.Count;

            for (int i = 0; i < count; i++)
            {
                float itemY = startY - i * listItemSpacing;

                // Skip if below panel bottom
                if (itemY < ContentY) break;

                RecipeData recipe = _recipes[i];
                if (recipe == null) continue;

                bool isSelected = (i == _selectedIndex);

                // ── Text ──
                Draw.Color     = isSelected ? ColorTextBright : ColorText;
                Draw.FontSize  = FontW(isSelected ? fontSizeBody * 1.05f : fontSizeBody);
                Draw.TextAlign = TextAlign.Left;

                float textX = ListX + 20f;
                Draw.Text(Scr(textX, itemY), recipe.recipeName);

                // ── Selection brackets (animated) ──
                if (isSelected)
                {
                    DrawSelectionBrackets(ListX + 5f, itemY, ListX + ListW - 10f);
                }

                // ── Index dot ──
                if (!isSelected)
                {
                    Draw.Color = ColorPrimaryDim;
                    Draw.Disc(Scr(ListX + 10f, itemY + fontSizeBody * 0.35f),
                              2f * _worldPerPx);
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  DRAWING — SELECTION BRACKETS (animated NASA-Punk)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Рисует пульсирующие angular brackets вокруг выбранного элемента.
        /// 
        ///   ▶────    Recipe Name    ────◀
        ///
        /// Brackets пульсируют по X, создавая эффект "фокусировки".
        /// </summary>
        private void DrawSelectionBrackets(float leftX, float centerY, float rightX)
        {
            float pulse = Mathf.Sin(Time.unscaledTime * bracketPulseSpeed) * bracketPulseAmplitude;
            float t = 1.5f * _worldPerPx;

            float halfH  = fontSizeBody * 0.55f;
            float arrowW = 6f + pulse;
            float arrowH = halfH * 0.7f;

            Draw.Color = ColorAccent;

            // ── Left bracket ▶ ──
            float lx = leftX - pulse;
            Draw.Line(Scr(lx, centerY + arrowH), Scr(lx + arrowW, centerY + fontSizeBody * 0.35f), t);
            Draw.Line(Scr(lx, centerY - arrowH + fontSizeBody * 0.7f), Scr(lx + arrowW, centerY + fontSizeBody * 0.35f), t);

            // Left horizontal dash
            Draw.Line(Scr(lx + arrowW + 2f, centerY + fontSizeBody * 0.35f),
                      Scr(lx + arrowW + 12f, centerY + fontSizeBody * 0.35f), t);

            // ── Right bracket ◀ ──
            float rx = rightX + pulse;
            Draw.Line(Scr(rx, centerY + arrowH), Scr(rx - arrowW, centerY + fontSizeBody * 0.35f), t);
            Draw.Line(Scr(rx, centerY - arrowH + fontSizeBody * 0.7f), Scr(rx - arrowW, centerY + fontSizeBody * 0.35f), t);

            // Right horizontal dash
            Draw.Line(Scr(rx - arrowW - 2f, centerY + fontSizeBody * 0.35f),
                      Scr(rx - arrowW - 12f, centerY + fontSizeBody * 0.35f), t);
        }

        // ══════════════════════════════════════════════════════════
        //  DRAWING — RECIPE DETAILS
        // ══════════════════════════════════════════════════════════

        private void DrawRecipeDetails()
        {
            if (_recipes == null || _recipes.Count == 0) return;
            if (_selectedIndex < 0 || _selectedIndex >= _recipes.Count) return;

            RecipeData recipe = _recipes[_selectedIndex];
            if (recipe == null) return;

            float x = DetailX;
            float y = ContentT - 10f;
            float lineH = fontSizeBody * 1.6f; // line height

            // ══════════════════════════════
            //  RESULT
            // ══════════════════════════════

            // Section label
            Draw.Color     = ColorAccent;
            Draw.FontSize  = FontW(fontSizeBody * 0.85f);
            Draw.TextAlign = TextAlign.Left;
            Draw.Text(Scr(x, y), LabelResult);

            y -= lineH * 0.7f;

            // Result name
            Draw.Color    = ColorTextBright;
            Draw.FontSize = FontW(fontSizeBody * 1.1f);
            Draw.Text(Scr(x + 10f, y), _resultNameCache ?? "--");

            y -= lineH * 0.5f;

            // ── Separator line ──
            Draw.Color = ColorPrimaryDim;
            Draw.Line(Scr(x, y), Scr(x + DetailW - 20f, y), 1f * _worldPerPx);

            y -= lineH * 0.7f;

            // ══════════════════════════════
            //  INGREDIENTS
            // ══════════════════════════════

            Draw.Color     = ColorAccent;
            Draw.FontSize  = FontW(fontSizeBody * 0.85f);
            Draw.Text(Scr(x, y), LabelIngredients);

            y -= lineH * 0.6f;

            // Ingredient list
            for (int i = 0; i < _ingredientCacheCount; i++)
            {
                if (y < ContentY + 60f) break; // safety

                bool hasMats = _ingredientSufficient[i];

                // Bullet
                Draw.Color = hasMats ? ColorSuccess : ColorWarning;
                Draw.Disc(Scr(x + 8f, y + fontSizeBody * 0.3f), 3f * _worldPerPx);

                // Ingredient name
                Draw.Color    = ColorText;
                Draw.FontSize = FontW(fontSizeBody);
                Draw.Text(Scr(x + 20f, y), _ingredientNameCache[i] ?? "--");

                // Status (right-aligned amount)
                Draw.Color     = hasMats ? ColorSuccess : ColorWarning;
                Draw.TextAlign = TextAlign.Right;
                Draw.Text(Scr(x + DetailW - 30f, y), _ingredientStatusCache[i] ?? "--");
                Draw.TextAlign = TextAlign.Left;

                y -= lineH * 0.65f;
            }

            y -= lineH * 0.3f;

            // ── Separator ──
            Draw.Color = ColorPrimaryDim;
            Draw.Line(Scr(x, y), Scr(x + DetailW - 20f, y), 1f * _worldPerPx);

            y -= lineH * 0.7f;

            // ══════════════════════════════
            //  CRAFT TIME
            // ══════════════════════════════

            Draw.Color     = ColorAccent;
            Draw.FontSize  = FontW(fontSizeBody * 0.85f);
            Draw.TextAlign = TextAlign.Left;
            Draw.Text(Scr(x, y), LabelCraftTime);

            Draw.Color = ColorText;
            Draw.Text(Scr(x + 160f, y), _craftTimeCache ?? "--");

            y -= lineH;

            // ══════════════════════════════
            //  CRAFT STATUS
            // ══════════════════════════════

            if (!_isCrafting)
            {
                Draw.Color    = _canCraftCurrent ? ColorSuccess : ColorWarning;
                Draw.FontSize = FontW(fontSizeBody);
                Draw.Text(Scr(x, y), _canCraftCurrent ? LabelReady : LabelInsufficient);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  DRAWING — PROGRESS BAR
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Рисует центральный прогресс-бар крафта.
        /// Сегментированный стиль (NASA-Punk).
        /// </summary>
        private void DrawProgressBar()
        {
            float barX = PanelX + PanelW * 0.15f;
            float barW = PanelW * 0.70f;
            float barY = PanelY + _sh * 0.05f;
            float barH = 24f;

            float centerX = barX + barW * 0.5f;
            float centerY = barY + barH * 0.5f;

            // ── Background ──
            Draw.Color = ColorProgressBg;
            Draw.Rectangle(Scr(centerX, centerY), PxW(barW), PxH(barH));

            // ── Fill ──
            float fillW = barW * _craftProgress;
            if (fillW > 1f)
            {
                Draw.Color = ColorProgress;
                Draw.Rectangle(
                    Scr(barX + fillW * 0.5f, centerY),
                    PxW(fillW), PxH(barH - 4f));
            }

            // ── Border ──
            Draw.Color = ColorPrimary;
            float t = 1.5f * _worldPerPx;

            // Top & bottom lines
            Draw.Line(Scr(barX, barY + barH), Scr(barX + barW, barY + barH), t);
            Draw.Line(Scr(barX, barY), Scr(barX + barW, barY), t);

            // Left & right caps
            Draw.Line(Scr(barX, barY), Scr(barX, barY + barH), t);
            Draw.Line(Scr(barX + barW, barY), Scr(barX + barW, barY + barH), t);

            // ── Segments (vertical tick marks every 10%) ──
            Draw.Color = ColorPrimaryDim;
            float segT = 0.5f * _worldPerPx;
            for (int pct = 10; pct < 100; pct += 10)
            {
                float segX = barX + barW * (pct / 100f);
                Draw.Line(Scr(segX, barY + 2f), Scr(segX, barY + barH - 2f), segT);
            }

            // ── Percentage text ──
            int percentInt = Mathf.Clamp(Mathf.FloorToInt(_craftProgress * 100f), 0, 100);

            Draw.Color     = ColorTextBright;
            Draw.FontSize  = FontW(fontSizeBody * 1.2f);
            Draw.TextAlign = TextAlign.Center;
            Draw.Text(Scr(centerX, centerY + barH + 12f), PercentStrings[percentInt]);

            // ── Label ──
            Draw.Color    = ColorAccent;
            Draw.FontSize = FontW(fontSizeBody * 0.9f);
            float pulse = (Mathf.Sin(Time.unscaledTime * 3f) * 0.5f + 0.5f) * 0.3f + 0.7f;
            Draw.Color = new Color(ColorAccent.r, ColorAccent.g, ColorAccent.b, pulse);
            Draw.Text(Scr(centerX, centerY + barH + 32f), LabelCrafting);
        }

        // ══════════════════════════════════════════════════════════
        //  DRAWING — CONTROL HINTS
        // ══════════════════════════════════════════════════════════

        private void DrawControlHints()
        {
            float y = PanelY + 15f;
            float centerX = PanelX + PanelW * 0.5f;

            Draw.Color     = ColorTextDim;
            Draw.FontSize  = FontW(fontSizeHint);
            Draw.TextAlign = TextAlign.Center;

            // ── Hint line ──
            // Concatenation avoided: draw each hint separately with spacing
            float hintSpacing = PanelW * 0.25f;

            Draw.Text(Scr(centerX - hintSpacing, y), LabelHintNav);
            Draw.Text(Scr(centerX, y), _canCraftCurrent ? LabelHintCraft : "");
            Draw.Text(Scr(centerX + hintSpacing, y), LabelHintClose);

            // ── Separator above hints ──
            Draw.Color = ColorPrimaryDim;
            Draw.Line(
                Scr(PanelX + 20f, y + fontSizeHint + 5f),
                Scr(PanelR - 20f, y + fontSizeHint + 5f),
                0.5f * _worldPerPx);
        }

        // ══════════════════════════════════════════════════════════
        //  CACHE REBUILDING — on selection change
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Пересчитывает кэшированные строки для выбранного рецепта.
        ///
        /// Вызывается ТОЛЬКО при смене выбора (W/S press),
        /// НЕ каждый кадр.
        ///
        /// GC: StringBuilder.ToString() аллоцирует string.
        /// Это допустимо — происходит при нажатии кнопки, не per-frame.
        /// Количество аллокаций = количество ингредиентов (~3-5).
        /// </summary>
        private void RebuildIngredientCache()
        {
            if (_recipes == null || _selectedIndex < 0 || _selectedIndex >= _recipes.Count)
            {
                _ingredientCacheCount = 0;
                _canCraftCurrent      = false;
                _craftTimeCache       = "--";
                _resultNameCache      = "--";
                return;
            }

            RecipeData recipe = _recipes[_selectedIndex];
            if (recipe == null)
            {
                _ingredientCacheCount = 0;
                _canCraftCurrent      = false;
                return;
            }

            // ── Result name ──
            _resultNameCache = recipe.recipeName; // already cached string on SO

            // ── Craft time ──
            _sb.Clear();
            AppendFloat(_sb, recipe.craftTime, 1);
            _sb.Append(LabelSeconds);
            _craftTimeCache = _sb.ToString(); // one allocation

            // ── Ingredients ──
            List<InventoryCost> ingredients = recipe.ingredients;
            int count = (ingredients != null) ? ingredients.Count : 0;

            // Ensure arrays are large enough
            if (_ingredientNameCache.Length < count)
            {
                _ingredientNameCache   = new string[count];
                _ingredientStatusCache = new string[count];
                _ingredientSufficient  = new bool[count];
            }

            _ingredientCacheCount = count;
            _canCraftCurrent = true;

            for (int i = 0; i < count; i++)
            {
                InventoryCost ing = ingredients[i];

                // Name (from ItemData — already cached)
                _ingredientNameCache[i] = (ing.item != null) ? ing.item.itemName : "--";

                // Count in inventory
                int have     = CountItemInInventory(ing.item);
                int required = ing.amount;

                // Status string "2/3"
                _sb.Clear();
                AppendInt(_sb, have);
                _sb.Append(LabelSlash);
                AppendInt(_sb, required);
                _ingredientStatusCache[i] = _sb.ToString(); // one allocation per ingredient

                // Sufficient check
                bool sufficient = have >= required;
                _ingredientSufficient[i] = sufficient;

                if (!sufficient)
                    _canCraftCurrent = false;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  INVENTORY HELPERS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Подсчитывает количество конкретного ItemData в инвентаре.
        ///
        /// Сканирует InventoryGrid — O(cols × rows).
        /// Вызывается ТОЛЬКО при смене выбора (не per-frame).
        ///
        /// ZERO GC: ReferenceEquals, for-цикл, no LINQ.
        /// </summary>
        private int CountItemInInventory(ItemData item)
        {
            if (item == null) return 0;
            if (playerInventory == null || playerInventory.Grid == null) return 0;

            InventoryGrid grid = playerInventory.Grid;
            int cols = grid.Columns;
            int rows = grid.Rows;
            int count = 0;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    if (ReferenceEquals(grid.GetCell(x, y), item))
                        count++;
                }
            }

            return count;
        }

        // ══════════════════════════════════════════════════════════
        //  STRING HELPERS — zero per-frame GC
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Appends an integer to StringBuilder using pre-cached strings.
        /// For values 0-99: zero GC (uses NumStrings cache).
        /// For values ≥100: StringBuilder.Append(int) — minimal GC.
        /// </summary>
        private static void AppendInt(StringBuilder sb, int value)
        {
            if (value >= 0 && value < NumStrings.Length)
                sb.Append(NumStrings[value]);
            else
                sb.Append(value);
        }

        /// <summary>
        /// Appends a float with specified decimal places.
        /// Uses integer math to avoid float→string GC.
        /// </summary>
        private static void AppendFloat(StringBuilder sb, float value, int decimals)
        {
            if (value < 0f)
            {
                sb.Append('-');
                value = -value;
            }

            int intPart = (int)value;
            AppendInt(sb, intPart);

            if (decimals > 0)
            {
                sb.Append('.');

                float frac = value - intPart;
                for (int d = 0; d < decimals; d++)
                {
                    frac *= 10f;
                    int digit = (int)frac;
                    if (digit > 9) digit = 9;
                    sb.Append((char)('0' + digit));
                    frac -= digit;
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics()
        {
            _debugIsOpen        = _isOpen;
            _debugSelectedIndex = _selectedIndex;
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (fontSizeHeader     < 8f)  fontSizeHeader     = 8f;
            if (fontSizeBody       < 6f)  fontSizeBody       = 6f;
            if (fontSizeHint       < 6f)  fontSizeHint       = 6f;
            if (listItemSpacing    < 16f) listItemSpacing    = 16f;
            if (bracketPulseSpeed  < 0f)  bracketPulseSpeed  = 0f;
        }
#endif
    }
}
