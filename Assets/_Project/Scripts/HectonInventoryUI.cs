// ============================================================================
// HECTON-8 — HectonInventoryUI.cs
// NASA-Punk Grid Inventory — Shapes Immediate-Mode визуализация (URP).
//
// ────────────────────────────────────────────────────────────────────────────
// RENDERING PIPELINE:
//   Базовый класс — ImmediateModeShapeDrawer (Shapes).
//   Единственная точка отрисовки — DrawShapes(Camera cam).
//   OnPostRender() удалён — URP не вызывает его на не-main камерах.
//
// TICK SYSTEM:
//   ITickable → GameTickManager.  Update() отсутствует.
//   Tick(float dt) — проверка ввода, ничего больше.
//
// КООРДИНАТЫ:
//   Экран: (0,0) = нижний-левый, Y вверх.
//   Сетка: [col=0, row=0] = верхний-левый, row вниз.
//   Маппинг: screenY = gridTopY − row × cellSize
//
// ZERO-GC ГАРАНТИИ:
//   • BitArray64 (ulong bitmask) вместо bool[,] + Array.Clear.
//   • StringBuilder переиспользуется; ToString() — только при смене данных.
//   • Строковые литералы индексов — статический массив, без аллокаций.
//   • RebuildCachedStrings — вызывается только при реальном изменении.
//
// СТИЛЬ: NASA-Punk
//   Тонкие линии, угловые скобки, фиксированная палитра.
//   Угловые скобки на предметах > 1×1 (DrawItemAccents удалён).
//   Glow-эффект на выделенном предмете (selectedCol / selectedRow).
// ============================================================================

using System;
using System.Text;
using Hecton8.Input;
using Hecton8.Core;
using Hecton8.Inventory;
using Hecton8.Items;
using Shapes;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HectonInventoryUI : ImmediateModeShapeDrawer, ITickable
{
    // ══════════════════════════════════════════════════════════════════
    //  INSPECTOR — REFERENCES
    // ══════════════════════════════════════════════════════════════════

    [Header("── References ────────────────────────────────")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private Camera          hudCamera;
    [SerializeField] private TMP_FontAsset   font;

    // ══════════════════════════════════════════════════════════════════
    //  INSPECTOR — INPUT
    // ══════════════════════════════════════════════════════════════════

    [Header("── Input ─────────────────────────────────────")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    // ══════════════════════════════════════════════════════════════════
    //  INSPECTOR — GRID LAYOUT
    // ══════════════════════════════════════════════════════════════════

    [Header("── Grid Layout ───────────────────────────────")]
    [SerializeField] private float cellSize     = 60f;
    [SerializeField] private float cellGap      = 2f;
    [SerializeField] private float panelPadding = 24f;

    [Tooltip("Горизонтальное смещение сетки от центра экрана (отрицательное = влево)")]
    [SerializeField] private float gridOffsetX  = -40f;

    // ══════════════════════════════════════════════════════════════════
    //  INSPECTOR — COLORS
    // ══════════════════════════════════════════════════════════════════

    [Header("── Colors ────────────────────────────────────")]
    [ColorUsage(true, true)]
    [SerializeField] private Color overlayColor    = new Color(0f,    0f,    0f,    0.55f);
    [ColorUsage(true, true)]
    [SerializeField] private Color panelBgColor    = new Color(0f,    0.03f, 0.06f, 0.92f);
    [ColorUsage(true, true)]
    [SerializeField] private Color frameColor      = new Color(0f,    0.6f,  0.8f,  0.6f);
    [ColorUsage(true, true)]
    [SerializeField] private Color cellBgColor     = new Color(0f,    0.06f, 0.10f, 0.6f);
    [ColorUsage(true, true)]
    [SerializeField] private Color cellBorderColor = new Color(0f,    0.4f,  0.6f,  0.2f);
    [ColorUsage(true, true)]
    [SerializeField] private Color itemFillColor   = new Color(0f,    0.5f,  0.7f,  0.30f);
    [ColorUsage(true, true)]
    [SerializeField] private Color itemBorderColor = new Color(0f,    0.85f, 1f,    0.7f);
    [ColorUsage(true, true)]
    [SerializeField] private Color textColor       = new Color(0f,    0.898f,1f,    1f);
    [ColorUsage(true, true)]
    [SerializeField] private Color dimTextColor    = new Color(0f,    0.7f,  0.9f,  0.5f);
    [ColorUsage(true, true)]
    [SerializeField] private Color headerColor     = new Color(0f,    0.898f,1f,    0.9f);
    [ColorUsage(true, true)]
    [SerializeField] private Color glowColor       = new Color(0f,    0.9f,  1f,    0.35f);

    // ══════════════════════════════════════════════════════════════════
    //  INSPECTOR — TYPOGRAPHY
    // ══════════════════════════════════════════════════════════════════

    [Header("── Typography ────────────────────────────────")]
    [SerializeField] private float fontSize       = 11f;
    [SerializeField] private float fontSizeTiny   = 7f;
    [SerializeField] private float headerFontSize = 18f;
    [SerializeField] private float lineThickness  = 1.5f;
    [SerializeField] private float thinLine       = 1.0f;

    // ══════════════════════════════════════════════════════════════════
    //  INSPECTOR — GLOW
    // ══════════════════════════════════════════════════════════════════

    [Header("── Glow Effect ───────────────────────────────")]
    [SerializeField] private float glowThickness  = 3.0f;
    [SerializeField] private float glowExpand     = 4.0f;

    // ══════════════════════════════════════════════════════════════════
    //  STATIC — INDEX LABELS (hex-style, zero-GC)
    // ══════════════════════════════════════════════════════════════════

    private static readonly string[] IndexLabels =
    {
        "0", "1", "2", "3", "4", "5", "6", "7",
        "8", "9", "A", "B", "C", "D", "E", "F"
    };

    // ══════════════════════════════════════════════════════════════════
    //  CONSTANTS
    // ══════════════════════════════════════════════════════════════════

    /// <summary>Максимальный размер сетки для BitMask. 8×8 = 64 бита → ulong.</summary>
    private const int MaxGridCells = 64;

    // ══════════════════════════════════════════════════════════════════
    //  RUNTIME STATE
    // ══════════════════════════════════════════════════════════════════

    private bool _isOpen;

    /// <summary>Панель инвентаря сейчас видна.</summary>
    public bool IsOpen => _isOpen;

    // ── Selection state (keyboard / mouse) ──
    private int _selectedCol = -1;
    private int _selectedRow = -1;

    // ── String Cache (rebuilt only on data change) ──
    private readonly StringBuilder _sb = new StringBuilder(64);
    private string _weightStr   = "MASS: 0.0 kg";
    private string _capacityStr = "48 / 48 FREE";
    private float  _cachedWeight = -1f;
    private int    _cachedFree   = -1;

    // ── Layout Cache (computed per-frame) ──
    private float _gridOriginX;
    private float _gridTopY;

    // ── Inventory content hash для ленивого ребилда строк ──
    private int _contentHash;

    // ══════════════════════════════════════════════════════════════════
    //  LIFECYCLE — ImmediateModeShapeDrawer
    // ══════════════════════════════════════════════════════════════════

public override void OnEnable()
    {
        base.OnEnable();
        GameTickManager.Instance?.Register((ITickable)this);

        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnInventory += HandleInventoryToggle;
            InputManager.Instance.OnNavigate  += HandleNavigate;
            InputManager.Instance.OnCancel    += HandleCancel;
        }
    }

public override void OnDisable()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnInventory -= HandleInventoryToggle;
            InputManager.Instance.OnNavigate  -= HandleNavigate;
            InputManager.Instance.OnCancel    -= HandleCancel;
        }

        GameTickManager.Instance?.Unregister((ITickable)this);
        _isOpen = false;
        base.OnDisable();
    }

    // ══════════════════════════════════════════════════════════════════
    //  ITickable — ЕДИНСТВЕННАЯ ТОЧКА ЛОГИКИ (НЕТ Update)
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Вызывается GameTickManager каждый кадр.
    /// Только проверка ввода — минимальная нагрузка.
    /// </summary>
public void Tick(float deltaTime)
    {
        // Input is fully event-driven via InputManager
    }

    // ══════════════════════════════════════════════════════════════════
    //  SHAPES — DrawShapes (ImmediateModeShapeDrawer override)
    //  Заменяет OnPostRender(). Корректно работает в URP.
    // ══════════════════════════════════════════════════════════════════

    public override void DrawShapes(Camera cam)
    {
        // ── Гейт: рисуем только на нашей HUD-камере ──
        if (!_isOpen)                                    return;
        if (hudCamera != null && cam != hudCamera)       return;
        if (inventory == null || inventory.Grid == null)  return;

        DrawInventoryScreen(cam);
    }

    // ══════════════════════════════════════════════════════════════════
    //  SELECTION INPUT
    // ══════════════════════════════════════════════════════════════════

private void HandleInventoryToggle()
    {
        SetOpenState(!_isOpen);
    }

    private void HandleCancel()
    {
        if (!_isOpen) return;

        if (_selectedCol >= 0)
        {
            _selectedCol = -1;
            _selectedRow = -1;
        }
        else
        {
            SetOpenState(false);
        }
    }

    private void SetOpenState(bool open)
    {
        _isOpen = open;

        if (_isOpen)
            InputManager.Instance?.SwitchToUIInput();
        else
            InputManager.Instance?.SwitchToPlayerInput();
    }

    private void HandleNavigate(Vector2 dir)
    {
        if (!_isOpen) return;
        if (inventory == null || inventory.Grid == null) return;

        InventoryGrid grid = inventory.Grid;
        int cols = grid.Columns;
        int rows = grid.Rows;

        if (_selectedCol < 0 || _selectedRow < 0)
        {
            if (dir.sqrMagnitude > 0.1f)
            {
                _selectedCol = 0;
                _selectedRow = 0;
            }
            return;
        }

        if (dir.x >  0.5f) _selectedCol = Mathf.Min(cols - 1, _selectedCol + 1);
        if (dir.x < -0.5f) _selectedCol = Mathf.Max(0, _selectedCol - 1);
        if (dir.y >  0.5f) _selectedRow = Mathf.Max(0, _selectedRow - 1);
        if (dir.y < -0.5f) _selectedRow = Mathf.Min(rows - 1, _selectedRow + 1);
    }

    // ══════════════════════════════════════════════════════════════════
    //  MAIN DRAW ROUTINE
    // ══════════════════════════════════════════════════════════════════

    private void DrawInventoryScreen(Camera cam)
    {
        InventoryGrid grid = inventory.Grid;
        int cols = grid.Columns;
        int rows = grid.Rows;

        float w = cam.pixelWidth;
        float h = cam.pixelHeight;

        // ── Ленивый ребилд строк ──
        RebuildCachedStrings(grid);

        // ── Вычисление layout ──
        float gridW = cols * cellSize;
        float gridH = rows * cellSize;

        _gridOriginX = (w - gridW) * 0.5f + gridOffsetX;
        _gridTopY    = (h + gridH) * 0.5f + 15f;

        float headerSpace = 48f;
        float footerSpace = 40f;

        float panelL  = _gridOriginX - panelPadding;
        float panelR  = _gridOriginX + gridW + panelPadding;
        float panelT  = _gridTopY + headerSpace;
        float panelB  = _gridTopY - gridH - footerSpace;
        float panelW  = panelR - panelL;
        float panelH  = panelT - panelB;
        float panelCX = (panelL + panelR) * 0.5f;
        float panelCY = (panelT + panelB) * 0.5f;

        using (Draw.Command(cam))
        {
            Draw.ResetAllDrawStates();
            Draw.BlendMode   = ShapesBlendMode.Transparent;
            Draw.Matrix      = Matrix4x4.Ortho(0, w, 0, h, -1, 1);
            Draw.Font        = font;
            Draw.LineEndCaps = LineEndCap.None;

            // ── Overlay ──
            Draw.Rectangle(new Vector3(w * 0.5f, h * 0.5f, 0), w, h, overlayColor);

            // ── Panel background + frame ──
            Draw.Rectangle(new Vector3(panelCX, panelCY, 0), panelW, panelH, panelBgColor);
            Draw.RectangleBorder(new Vector3(panelCX, panelCY, 0),
                                 panelW, panelH, lineThickness, frameColor);

            // ── Структурные элементы ──
            DrawCornerBrackets(panelL, panelR, panelT, panelB);
            DrawHeader(panelCX, panelL, panelR);
            DrawAxisLabels(cols, rows);
            DrawGridCells(cols, rows);
            DrawItems(grid, cols, rows);
            DrawFooter(panelL, panelR, gridH);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  DRAW — CORNER L-BRACKETS (панель)
    // ══════════════════════════════════════════════════════════════════

    private void DrawCornerBrackets(float l, float r, float t, float b)
    {
        float arm = 22f;
        Draw.Thickness = lineThickness;

        // Top-Left
        Draw.Line(new Vector3(l, t, 0), new Vector3(l + arm, t, 0), frameColor);
        Draw.Line(new Vector3(l, t, 0), new Vector3(l, t - arm, 0), frameColor);

        // Top-Right
        Draw.Line(new Vector3(r - arm, t, 0), new Vector3(r, t, 0), frameColor);
        Draw.Line(new Vector3(r, t, 0),       new Vector3(r, t - arm, 0), frameColor);

        // Bottom-Left
        Draw.Line(new Vector3(l, b + arm, 0), new Vector3(l, b, 0), frameColor);
        Draw.Line(new Vector3(l, b, 0),       new Vector3(l + arm, b, 0), frameColor);

        // Bottom-Right
        Draw.Line(new Vector3(r, b + arm, 0), new Vector3(r, b, 0), frameColor);
        Draw.Line(new Vector3(r - arm, b, 0), new Vector3(r, b, 0), frameColor);
    }

    // ══════════════════════════════════════════════════════════════════
    //  DRAW — HEADER ("CARGO BAY")
    // ══════════════════════════════════════════════════════════════════

    private void DrawHeader(float cx, float panelL, float panelR)
    {
        float headerY = _gridTopY + 22f;

        DrawTextCentered("CARGO BAY", new Vector3(cx, headerY, 0),
                         headerFontSize, headerColor);

        // ── Фланговые линии ──
        float flankLen = 55f;
        float flankGap = 75f;
        float flankY   = headerY + headerFontSize * 0.35f;
        Color flankCol = frameColor * 0.5f;

        Draw.Line(new Vector3(cx - flankGap - flankLen, flankY, 0),
                  new Vector3(cx - flankGap, flankY, 0), thinLine, flankCol);
        Draw.Line(new Vector3(cx + flankGap, flankY, 0),
                  new Vector3(cx + flankGap + flankLen, flankY, 0), thinLine, flankCol);

        // ── Системная метка ──
        DrawText("SYS://INV",
                 new Vector3(panelR - 80f, headerY + headerFontSize * 0.6f, 0),
                 fontSizeTiny, dimTextColor * 0.4f);

        // ── Разделитель под заголовком ──
        float sepY = _gridTopY + 6f;
        Draw.Line(new Vector3(panelL + 12f, sepY, 0),
                  new Vector3(panelR - 12f, sepY, 0), thinLine * 0.5f, frameColor * 0.3f);
    }

    // ══════════════════════════════════════════════════════════════════
    //  DRAW — AXIS LABELS (hex индексы по осям)
    // ══════════════════════════════════════════════════════════════════

    private void DrawAxisLabels(int cols, int rows)
    {
        Color labelCol = dimTextColor * 0.45f;

        int maxCol = Mathf.Min(cols, IndexLabels.Length);
        for (int col = 0; col < maxCol; col++)
        {
            float cx = _gridOriginX + col * cellSize + cellSize * 0.5f;
            float cy = _gridTopY + 2f;
            DrawTextCentered(IndexLabels[col], new Vector3(cx, cy, 0), fontSizeTiny, labelCol);
        }

        int maxRow = Mathf.Min(rows, IndexLabels.Length);
        for (int row = 0; row < maxRow; row++)
        {
            float cx = _gridOriginX - 10f;
            float cy = _gridTopY - row * cellSize - cellSize * 0.5f;
            DrawTextCentered(IndexLabels[row], new Vector3(cx, cy, 0), fontSizeTiny, labelCol);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  DRAW — GRID CELLS (фон + тонкие рамки)
    // ══════════════════════════════════════════════════════════════════

    private void DrawGridCells(int cols, int rows)
    {
        float vs       = cellSize - cellGap;
        float halfThin = thinLine * 0.5f;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                float cx = _gridOriginX + col * cellSize + cellSize * 0.5f;
                float cy = _gridTopY    - row * cellSize - cellSize * 0.5f;
                Vector3 center = new Vector3(cx, cy, 0);

                Draw.Rectangle(center, vs, vs, cellBgColor);
                Draw.RectangleBorder(center, vs, vs, halfThin, cellBorderColor);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  DRAW — ITEMS
    //
    //  Оптимизация: вместо bool[,] + Array.Clear используем ulong
    //  BitMask (до 64 ячеек = 8×8 сетка). Для каждой ячейки проверяем
    //  бит; если установлен — предмет уже отрисован. Проверка «якорной»
    //  ячейки: текущая (col, row) должна быть верхней-левой для данного
    //  предмета. Нет аллокаций, нет Array.Clear.
    //
    //  Если сетка > 64 ячеек — фоллбэк на _drawnFallback bool[].
    // ══════════════════════════════════════════════════════════════════

    // Фоллбэк-массив для сеток > 64 ячеек (аллоцируется лениво, один раз)
    private bool[] _drawnFallback;
    private int    _drawnFallbackSize;

    private void DrawItems(InventoryGrid grid, int cols, int rows)
    {
        int totalCells = cols * rows;
        bool useBitmask = totalCells <= MaxGridCells;

        // ── BitMask path (zero-alloc, zero-clear) ──
        ulong drawnMask = 0UL;

        // ── Fallback path ──
        if (!useBitmask)
        {
            EnsureFallbackArray(totalCells);
            Array.Clear(_drawnFallback, 0, totalCells);
        }

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int flatIndex = row * cols + col;

                // ── Проверка: уже отрисована? ──
                if (useBitmask)
                {
                    if ((drawnMask & (1UL << flatIndex)) != 0) continue;
                }
                else
                {
                    if (_drawnFallback[flatIndex]) continue;
                }

                ItemData item = grid.GetCell(col, row);
                if (item == null) continue;

                int iw = item.width;
                int ih = item.height;

                // ── Проверка «якорной» ячейки ──
                // Текущая (col, row) является якорной, только если это
                // верхний-левый угол предмета. Проверяем: ячейка (col-1, row)
                // и (col, row-1) не содержат тот же предмет.
                if (col > 0 && grid.GetCell(col - 1, row) == item) continue;
                if (row > 0 && grid.GetCell(col, row - 1) == item) continue;

                // ── Помечаем все ячейки предмета как отрисованные ──
                int endX = Mathf.Min(col + iw, cols);
                int endY = Mathf.Min(row + ih, rows);

                for (int iy = row; iy < endY; iy++)
                {
                    for (int ix = col; ix < endX; ix++)
                    {
                        int fi = iy * cols + ix;
                        if (useBitmask)
                            drawnMask |= (1UL << fi);
                        else
                            _drawnFallback[fi] = true;
                    }
                }

                // ── Геометрия предмета ──
                float itemW  = iw * cellSize - cellGap;
                float itemH  = ih * cellSize - cellGap;
                float itemCX = _gridOriginX + col * cellSize + iw * cellSize * 0.5f;
                float itemCY = _gridTopY    - row * cellSize - ih * cellSize * 0.5f;
                Vector3 center = new Vector3(itemCX, itemCY, 0);

                // ── Проверка: выбран ли этот предмет? ──
                bool isSelected = (_selectedCol >= col && _selectedCol < endX &&
                                   _selectedRow >= row && _selectedRow < endY);

                // ── Заливка ──
                Draw.Rectangle(center, itemW, itemH, itemFillColor);

                // ── Glow для выбранного предмета ──
                if (isSelected)
                {
                    float gw = itemW + glowExpand * 2f;
                    float gh = itemH + glowExpand * 2f;

                    // Пульсация через синус для NASA-Punk feel
                    float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 4f);
                    Color gc    = glowColor;
                    gc.a *= pulse;

                    Draw.RectangleBorder(center, gw, gh, glowThickness, gc);
                }

                // ── Рамка предмета ──
                Draw.RectangleBorder(center, itemW, itemH, lineThickness, itemBorderColor);

                // ── Угловые скобки (только для предметов > 1×1) ──
                if (iw > 1 || ih > 1)
                    DrawItemCornerBrackets(itemCX, itemCY, itemW, itemH);

                // ── Название предмета ──
                DrawTextCentered(item.itemName, center, fontSize, textColor);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  DRAW — ITEM CORNER BRACKETS (NASA-Punk, замена DrawItemAccents)
    //
    //  Угловые скобки в стиле HUD-прицела: тонкие L-образные
    //  элементы по углам предмета. Рисуются только для
    //  многоклеточных предметов (> 1×1).
    // ══════════════════════════════════════════════════════════════════

    private void DrawItemCornerBrackets(float cx, float cy, float w, float h)
    {
        float hw   = w * 0.5f - 3f;
        float hh   = h * 0.5f - 3f;
        float arm  = Mathf.Min(10f, hw * 0.35f, hh * 0.35f);
        float t    = thinLine;
        Color c    = itemBorderColor * 0.55f;

        // Top-Left ┌
        Draw.Line(new Vector3(cx - hw, cy + hh, 0),
                  new Vector3(cx - hw + arm, cy + hh, 0), t, c);
        Draw.Line(new Vector3(cx - hw, cy + hh, 0),
                  new Vector3(cx - hw, cy + hh - arm, 0), t, c);

        // Top-Right ┐
        Draw.Line(new Vector3(cx + hw - arm, cy + hh, 0),
                  new Vector3(cx + hw, cy + hh, 0), t, c);
        Draw.Line(new Vector3(cx + hw, cy + hh, 0),
                  new Vector3(cx + hw, cy + hh - arm, 0), t, c);

        // Bottom-Left └
        Draw.Line(new Vector3(cx - hw, cy - hh + arm, 0),
                  new Vector3(cx - hw, cy - hh, 0), t, c);
        Draw.Line(new Vector3(cx - hw, cy - hh, 0),
                  new Vector3(cx - hw + arm, cy - hh, 0), t, c);

        // Bottom-Right ┘
        Draw.Line(new Vector3(cx + hw, cy - hh + arm, 0),
                  new Vector3(cx + hw, cy - hh, 0), t, c);
        Draw.Line(new Vector3(cx + hw - arm, cy - hh, 0),
                  new Vector3(cx + hw, cy - hh, 0), t, c);
    }

    // ══════════════════════════════════════════════════════════════════
    //  DRAW — FOOTER (масса + свободные ячейки)
    // ══════════════════════════════════════════════════════════════════

    private void DrawFooter(float panelL, float panelR, float gridH)
    {
        float sepY    = _gridTopY - gridH - 8f;
        float footerY = sepY - 18f;

        Draw.Line(new Vector3(panelL + 12f, sepY, 0),
                  new Vector3(panelR - 12f, sepY, 0), thinLine * 0.5f, frameColor * 0.3f);

        DrawText(_weightStr,   new Vector3(panelL + 18f,   footerY, 0), fontSize, dimTextColor);
        DrawText(_capacityStr, new Vector3(panelR - 140f, footerY, 0), fontSize, dimTextColor);
    }

    // ══════════════════════════════════════════════════════════════════
    //  TEXT HELPERS (Zero-GC — строки не создаются)
    // ══════════════════════════════════════════════════════════════════

    private void DrawText(string text, Vector3 pos, float size, Color color)
    {
        if (string.IsNullOrEmpty(text)) return;
        Draw.Text(pos, Quaternion.identity, text, TextAlign.Left, size, font, color);
    }

    private void DrawTextCentered(string text, Vector3 pos, float size, Color color)
    {
        if (string.IsNullOrEmpty(text)) return;
        Draw.Text(pos, Quaternion.identity, text, TextAlign.Center, size, font, color);
    }

    // ══════════════════════════════════════════════════════════════════
    //  CACHE MANAGEMENT — ZERO GC
    //
    //  RebuildCachedStrings вызывается каждый кадр, но ToString()
    //  выполняется ТОЛЬКО при реальном изменении данных.
    //  Дополнительно: хэш содержимого для обнаружения перестановок.
    // ══════════════════════════════════════════════════════════════════

    private void EnsureFallbackArray(int totalCells)
    {
        if (_drawnFallback != null && _drawnFallbackSize >= totalCells) return;

        _drawnFallback     = new bool[totalCells];
        _drawnFallbackSize = totalCells;
    }

    private void RebuildCachedStrings(InventoryGrid grid)
    {
        // ── Масса ──
        float currentWeight = inventory.TotalWeight;
        if (!Mathf.Approximately(_cachedWeight, currentWeight))
        {
            _cachedWeight = currentWeight;

            int intPart = (int)currentWeight;
            int decPart = (int)((currentWeight - intPart) * 10f);
            if (decPart < 0) decPart = -decPart;

            _sb.Clear();
            _sb.Append("MASS: ");
            _sb.Append(intPart);
            _sb.Append('.');
            _sb.Append(decPart);
            _sb.Append(" kg");
            _weightStr = _sb.ToString();
        }

        // ── Свободные ячейки ──
        int currentFree = grid.FreeCells;
        if (_cachedFree != currentFree)
        {
            _cachedFree = currentFree;

            _sb.Clear();
            _sb.Append(currentFree);
            _sb.Append(" / ");
            _sb.Append(grid.TotalCells);
            _sb.Append(" FREE");
            _capacityStr = _sb.ToString();
        }
    }
}
