// ============================================================================
// HECTON-8 — HectonSuitHUD.cs
// Sci-fi костюмный HUD через Shapes (Immediate Mode).
//
// v3.0 — MODULE STATUS PANEL:
//   [ADD] ModuleStatusPanel — показывает статус текущего BaseModule:
//         питание (HasPower), затопление (IsFlooded), O2 (IsLifeSupport).
//   [ADD] ModuleStatusEvents.cs интеграция — подписка на OnModuleEnter/Exit.
//   [ADD] Pre-allocated module status strings (Zero GC).
//   [ADD] DrawModulePanel() — нижняя правая панель с иконками и статусом.
//
// v2.0 — ZERO-GC STRING CACHE (preserved):
//   • PercentStrings[101], DepthStrings[5001], PressureStrings[501], IntStrings[5001]
//   • ~226 KB one-time allocation. Zero GC at runtime.
//
// PRESERVED FROM v2.0:
//   ✓ CornerGrid, CornerBrackets, LeftPanel, RightPanel
//   ✓ ArcIndicators, DepthScale, TimeDisplay, StatusBar
//   ✓ InteractPrompt, CriticalOverlay
//   ✓ All event handlers (Oxygen, Energy, Depth, Integrity, Pressure)
//   ✓ Digital Noise / Glitch coroutine
//   ✓ ImmediateModeShapeDrawer pattern
// ============================================================================

using System;
using System.Collections;
using System.Text;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Shapes;
using TMPro;
using UnityEngine;
using Unity.Mathematics;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
[ExecuteAlways]
public sealed class HectonSuitHUD : ImmediateModeShapeDrawer
{
    // ══════════════════════════════════════════════════════════════════
    //  STATIC PRE-ALLOCATED STRING CACHE
    // ══════════════════════════════════════════════════════════════════

    private const int MaxDepth    = 5000;
    private const int MaxPressure = 500;

    public  static readonly string[] IntStrings;
    private static readonly string[] PercentStrings;
    private static readonly string[] DepthStrings;
    private static readonly string[] PressureStrings;

    // Module status strings — pre-allocated, zero GC
    private static readonly string STR_MODULE_POWERED    = "POWERED";
    private static readonly string STR_MODULE_NO_POWER   = "NO POWER";
    private static readonly string STR_MODULE_DRY        = "DRY";
    private static readonly string STR_MODULE_FLOODED    = "FLOODED";
    private static readonly string STR_MODULE_O2_ACTIVE  = "O2 ACTIVE";
    private static readonly string STR_MODULE_O2_OFFLINE = "O2 OFFLINE";
    private static readonly string STR_MODULE_LABEL      = "BASE MODULE";
    private static readonly string STR_NO_MODULE         = "EXTERIOR";

    static HectonSuitHUD()
    {
        PercentStrings = new string[101];
        for (int i = 0; i <= 100; i++)
            PercentStrings[i] = $"{i} %";

        DepthStrings = new string[MaxDepth + 1];
        for (int i = 0; i <= MaxDepth; i++)
            DepthStrings[i] = $"DEPTH: {i} m";

        PressureStrings = new string[MaxPressure + 1];
        for (int i = 0; i <= MaxPressure; i++)
            PressureStrings[i] = $"ATM: {i} atm";

        IntStrings = new string[MaxDepth + 1];
        for (int i = 0; i <= MaxDepth; i++)
            IntStrings[i] = i.ToString();
    }

    // ══════════════════════════════════════════════════════════════════
    //  INSPECTOR — CORE
    // ══════════════════════════════════════════════════════════════════

    [Header("── Core ──────────────────────────────────────────")]
    [SerializeField] private HectonSurvivalSystem survival;
    [SerializeField] private Camera hudCamera;
    [SerializeField] private TMP_FontAsset hudFont;

    // ══════════════════════════════════════════════════════════════════
    //  INSPECTOR — COLOR CODING
    // ══════════════════════════════════════════════════════════════════

    [Header("── Color Coding ──────────────────────────────────")]
    [ColorUsage(true, true)] [SerializeField] private Color normalColor     = new Color(0f, 0.898f, 1f, 1f);
    [ColorUsage(true, true)] [SerializeField] private Color warningColor    = new Color(1f, 0.878f, 0f, 1f);
    [ColorUsage(true, true)] [SerializeField] private Color criticalColor   = new Color(1f, 0.384f, 0f, 1f);
    [ColorUsage(true, true)] [SerializeField] private Color frameColor      = new Color(0f, 0.6f, 0.8f, 0.6f);
    [ColorUsage(true, true)] [SerializeField] private Color gridColor       = new Color(0f, 0.5f, 0.7f, 0.15f);
    [ColorUsage(true, true)] [SerializeField] private Color depthScaleColor = new Color(0f, 0.8f, 1f, 0.4f);
    [ColorUsage(true, true)] [SerializeField] private Color textDimColor    = new Color(0f, 0.7f, 0.9f, 0.5f);
    [ColorUsage(true, true)] [SerializeField] private Color bgPanelColor    = new Color(0f, 0.05f, 0.1f, 0.25f);

    // Module status colors
    [Header("── Module Status Colors ──────────────────────────")]
    [ColorUsage(true, true)] [SerializeField] private Color modulePoweredColor = new Color(0f, 1f, 0.4f, 1f);
    [ColorUsage(true, true)] [SerializeField] private Color moduleFloodedColor = new Color(0.2f, 0.5f, 1f, 1f);
    [ColorUsage(true, true)] [SerializeField] private Color moduleOfflineColor = new Color(1f, 0.384f, 0f, 1f);
    [ColorUsage(true, true)] [SerializeField] private Color moduleNeutralColor = new Color(0f, 0.6f, 0.8f, 0.6f);

    // ══════════════════════════════════════════════════════════════════
    //  INSPECTOR — LAYOUT
    // ══════════════════════════════════════════════════════════════════

    [Header("── Layout ────────────────────────────────────────")]
    [SerializeField] private float lineThickness = 1.5f;
    [SerializeField] private float thinLine      = 1.0f;
    [SerializeField] private float fontSize      = 14f;
    [SerializeField] private float fontSizeSmall = 10f;
    [SerializeField] private float fontSizeTiny  = 8f;

    [Header("── Arc Indicators ────────────────────────────────")]
    [SerializeField] private float arcRadius    = 38f;
    [SerializeField] private float arcThickness = 4f;
    [SerializeField] private float arcSpacing   = 90f;

    [Header("── Critical Alert ────────────────────────────────")]
    [SerializeField] private float pulsePeriod = 1.2f;

    [Header("── Digital Noise ─────────────────────────────────")]
    [SerializeField, Range(3f, 15f)]     private float noiseMinInterval = 5f;
    [SerializeField, Range(5f, 20f)]     private float noiseMaxInterval = 10f;
    [SerializeField, Range(0.05f, 0.3f)] private float noiseDuration    = 0.1f;

    // ══════════════════════════════════════════════════════════════════
    //  INSPECTOR — INTERACT PROMPT
    // ══════════════════════════════════════════════════════════════════

    [Header("── Interact Prompt ───────────────────────────────")]
    [SerializeField] private float promptBracketHalfW = 120f;
    [SerializeField] private float promptBracketHalfH = 26f;
    [SerializeField] private float promptBracketArm   = 18f;

    // ══════════════════════════════════════════════════════════════════
    //  THRESHOLDS
    // ══════════════════════════════════════════════════════════════════

    private const float WarningThreshold  = 0.30f;
    private const float CriticalThreshold = 0.15f;

    // ══════════════════════════════════════════════════════════════════
    //  RUNTIME STATE — SURVIVAL
    // ══════════════════════════════════════════════════════════════════

    private float _oxygenNorm    = 1f;
    private float _energyNorm    = 1f;
    private float _integrityNorm = 1f;

    private int _oxygenPct    = 100;
    private int _energyPct    = 100;
    private int _integrityPct = 100;
    private int _depthInt     = 0;
    private int _pressureInt  = 0;

    private Color _o2Color        = Color.cyan;
    private Color _energyColor    = Color.cyan;
    private Color _integrityColor = Color.cyan;

    private bool  _o2Critical;
    private bool  _integrityCritical;
    private float _pulseTimer;

    private string _statusText = "SYS NOMINAL";

    private float _depthScaleOffset;
    private float _depthScaleTarget;

    // ══════════════════════════════════════════════════════════════════
    //  RUNTIME STATE — MODULE STATUS (v3.0)
    // ══════════════════════════════════════════════════════════════════

    /// <summary>Current module player is inside. Null = exterior.</summary>
    private BaseModule _currentModule;

    /// <summary>Cached module state — updated every frame in LateUpdate.</summary>
    private bool _moduleHasPower;
    private bool _moduleIsFlooded;
    private bool _moduleIsLifeSupport;
    private bool _playerInsideModule;

    /// <summary>Module panel fade [0..1]. Animates in/out on enter/exit.</summary>
    private float _modulePanelAlpha;

    // ══════════════════════════════════════════════════════════════════
    //  RUNTIME STATE — STRINGS
    // ══════════════════════════════════════════════════════════════════

    private string _o2Str        = "100 %";
    private string _energyStr    = "100 %";
    private string _integrityStr = "100 %";
    private string _depthStr     = "DEPTH: 0 m";
    private string _pressureStr  = "ATM: 0 atm";
    private string _timeStr      = "00:00:00";

    private string _interactPromptText;

    // ══════════════════════════════════════════════════════════════════
    //  GLITCH
    // ══════════════════════════════════════════════════════════════════

    private const int SlotCount = 5;
    private readonly string[] _glitchOverlay = new string[SlotCount];

    private static readonly char[] Glyphs =
    {
        '&', '%', '$', '#', '@', '!', '?', 'X',
        '+', '=', '<', '>', '/', ':', '*'
    };

    private readonly StringBuilder _sb       = new StringBuilder(64);
    private readonly StringBuilder _sbGlitch = new StringBuilder(64);
    private int _lastSecond = -1;
    private const int GridLinesPerCorner = 5;

    // ══════════════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════════════

    private Coroutine _noiseHandle;

    public override void OnEnable()
    {
        base.OnEnable();

        if (hudCamera == null)
            hudCamera = GetComponent<Camera>();

        Subscribe();
        ForceRefreshAll();
        _noiseHandle = StartCoroutine(NoiseLoop());

        InteractionEvents.OnHoverChanged += HandleHoverChanged;

        // v3.0: Subscribe to module zone events
        ModuleStatusEvents.OnModuleEnter += HandleModuleEnter;
        ModuleStatusEvents.OnModuleExit  += HandleModuleExit;
    }

    public override void OnDisable()
    {
        base.OnDisable();
        Unsubscribe();

        if (_noiseHandle != null)
        {
            StopCoroutine(_noiseHandle);
            _noiseHandle = null;
        }

        _o2Critical = false;
        _integrityCritical = false;

        InteractionEvents.OnHoverChanged -= HandleHoverChanged;

        // v3.0: Unsubscribe module events
        ModuleStatusEvents.OnModuleEnter -= HandleModuleEnter;
        ModuleStatusEvents.OnModuleExit  -= HandleModuleExit;

        _currentModule = null;
        _playerInsideModule = false;
        _modulePanelAlpha = 0f;
    }

    // ══════════════════════════════════════════════════════════════════
    //  URP ENTRY POINT
    // ══════════════════════════════════════════════════════════════════

    public override void DrawShapes(Camera cam)
    {
        if (hudCamera != null && cam != hudCamera) return;
        DrawHUD();
    }

    private void LateUpdate()
    {
        _pulseTimer += Time.deltaTime / pulsePeriod;
        if (_pulseTimer > 1f) _pulseTimer -= 1f;

        _depthScaleOffset = Mathf.Lerp(
            _depthScaleOffset, _depthScaleTarget, Time.deltaTime * 3f);

        UpdateTimeString();

        // v3.0: Poll module state + animate panel alpha
        UpdateModuleState();
    }

    // ══════════════════════════════════════════════════════════════════
    //  MODULE STATE UPDATE (v3.0)
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Called every LateUpdate. Reads module state properties — no GC.
    /// Animates panel fade in/out.
    /// </summary>
    private void UpdateModuleState()
    {
        if (_currentModule != null)
        {
            _moduleHasPower      = _currentModule.HasPower;
            _moduleIsFlooded     = _currentModule.IsFlooded;

            // Life support active = powered + not flooded + player inside
            _moduleIsLifeSupport = _moduleHasPower && !_moduleIsFlooded;
        }

        // Animate alpha: fade in when inside, fade out when outside
        float targetAlpha = _playerInsideModule ? 1f : 0f;
        _modulePanelAlpha = Mathf.MoveTowards(
            _modulePanelAlpha, targetAlpha, Time.deltaTime * 3f);
    }

    // ══════════════════════════════════════════════════════════════════
    //  MAIN DRAW ROUTINE
    // ══════════════════════════════════════════════════════════════════

    private void DrawHUD()
    {
        if (hudCamera == null) return;

        float w = hudCamera.pixelWidth;
        float h = hudCamera.pixelHeight;

        using (Draw.Command(hudCamera))
        {
            Draw.ResetAllDrawStates();
            Draw.BlendMode = ShapesBlendMode.Transparent;
            Draw.Matrix = Matrix4x4.Ortho(0, w, 0, h, -1, 1);
            Draw.Font = hudFont;

            DrawCornerGrid(w, h);
            DrawCornerBrackets(w, h);
            DrawLeftPanel(w, h);
            DrawRightPanel(w, h);
            DrawArcIndicators(w, h);
            DrawDepthScale(w, h);
            DrawTimeDisplay(w, h);
            DrawStatusBar(w, h);
            DrawInteractPrompt(w, h);
            DrawCriticalOverlay(w, h);

            // v3.0: Module status panel (bottom-right, fades in/out)
            if (_modulePanelAlpha > 0.01f)
                DrawModulePanel(w, h, _modulePanelAlpha);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  DRAW — MODULE STATUS PANEL (v3.0)
    //
    //  Layout (bottom-right corner):
    //
    //  ┌─────────────────────────────────┐
    //  │ BASE MODULE                     │
    //  ├─────────────────────────────────┤
    //  │ [●] POWERED    [◌] NO POWER     │
    //  │ [~] DRY        [≋] FLOODED      │
    //  │ [O] O2 ACTIVE  [X] O2 OFFLINE   │
    //  └─────────────────────────────────┘
    //
    //  Panel fades in on module enter, fades out on exit.
    //  Colors: green = good, orange = warn, cyan = info.
    // ══════════════════════════════════════════════════════════════════

    private void DrawModulePanel(float w, float h, float alpha)
    {
        const float panelW = 210f;
        const float panelH = 130f;
        const float margin  = 12f;

        float panelX = w - margin - 20f - panelW;
        float panelY = margin + 30f + 130f + 8f; // below RightPanel

        // ── Background ──
        Color bg = ScaleAlpha(bgPanelColor, alpha);
        Color border = ScaleAlpha(frameColor, alpha);

        Draw.Rectangle(
            new Vector3(panelX + panelW * 0.5f, panelY + panelH * 0.5f, 0),
            panelW, panelH, bg);

        Draw.RectangleBorder(
            new Vector3(panelX + panelW * 0.5f, panelY + panelH * 0.5f, 0),
            panelW, panelH, lineThickness, border);

        // ── Header ──
        float headerY = panelY + panelH - 18f;
        DrawText(STR_MODULE_LABEL,
                 new Vector3(panelX + 8f, headerY, 0),
                 fontSizeSmall,
                 ScaleAlpha(frameColor, alpha));

        Draw.Line(
            new Vector3(panelX, panelY + panelH - 26f, 0),
            new Vector3(panelX + panelW, panelY + panelH - 26f, 0),
            lineThickness * 0.5f, ScaleAlpha(frameColor, alpha));

        // ── Row 1: Power status ──
        float rowY = panelY + panelH - 52f;
        DrawModuleStatusRow(
            panelX, rowY, panelW,
            _moduleHasPower ? STR_MODULE_POWERED : STR_MODULE_NO_POWER,
            _moduleHasPower ? modulePoweredColor : moduleOfflineColor,
            _moduleHasPower ? "●" : "◌",
            alpha);

        // ── Row 2: Flood status ──
        rowY -= 30f;
        DrawModuleStatusRow(
            panelX, rowY, panelW,
            _moduleIsFlooded ? STR_MODULE_FLOODED : STR_MODULE_DRY,
            _moduleIsFlooded ? moduleFloodedColor : modulePoweredColor,
            _moduleIsFlooded ? "≋" : "~",
            alpha);

        // ── Row 3: Life support status ──
        rowY -= 30f;
        DrawModuleStatusRow(
            panelX, rowY, panelW,
            _moduleIsLifeSupport ? STR_MODULE_O2_ACTIVE : STR_MODULE_O2_OFFLINE,
            _moduleIsLifeSupport ? modulePoweredColor : moduleOfflineColor,
            _moduleIsLifeSupport ? "○" : "×",
            alpha);

        // ── Pulse dot on active life support ──
        if (_moduleIsLifeSupport)
        {
            float pulse = PulseSin01();
            float dotAlpha = Mathf.Lerp(0.3f, 1f, pulse) * alpha;
            float dotX = panelX + 8f + 10f; // align with icon column
            float dotY = panelY + panelH - 112f; // row 3

            Color dotColor = ScaleAlpha(modulePoweredColor, dotAlpha);
            Draw.Disc(new Vector3(dotX, dotY + 5f, 0), 3f, dotColor);
        }
    }

    /// <summary>Draws one status row: [icon] status text.</summary>
    private void DrawModuleStatusRow(float panelX, float rowY, float panelW,
                                      string statusText, Color statusColor,
                                      string icon, float alpha)
    {
        Color iconCol   = ScaleAlpha(statusColor, alpha);
        Color statusCol = ScaleAlpha(statusColor, alpha * 0.9f);
        Color dimCol    = ScaleAlpha(textDimColor, alpha);

        // Horizontal separator
        Draw.Line(
            new Vector3(panelX + 6f, rowY + 22f, 0),
            new Vector3(panelX + panelW - 6f, rowY + 22f, 0),
            thinLine * 0.3f,
            ScaleAlpha(frameColor, alpha * 0.2f));

        // Icon
        DrawText(icon,       new Vector3(panelX + 8f,  rowY + 4f, 0), fontSizeSmall, iconCol);
        // Status text
        DrawText(statusText, new Vector3(panelX + 24f, rowY + 4f, 0), fontSizeSmall, statusCol);
    }

    // ══════════════════════════════════════════════════════════════════
    //  MODULE EVENT HANDLERS (v3.0)
    // ══════════════════════════════════════════════════════════════════

    private void HandleModuleEnter(BaseModule module)
    {
        _currentModule     = module;
        _playerInsideModule = true;
    }

    private void HandleModuleExit(BaseModule module)
    {
        // Only clear if it's the same module we're tracking
        if (_currentModule == module)
        {
            _currentModule     = null;
            _playerInsideModule = false;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  DRAW — CORNER GRID
    // ══════════════════════════════════════════════════════════════════

    private void DrawCornerGrid(float w, float h)
    {
        float margin  = 12f;
        float gridLen = Mathf.Min(w, h) * 0.08f;
        float step    = gridLen / GridLinesPerCorner;

        Draw.LineEndCaps = LineEndCap.None;
        Draw.Thickness = thinLine * 0.5f;

        for (int i = 0; i <= GridLinesPerCorner; i++)
        {
            float offset = i * step;

            // Top-left
            Draw.Line(new Vector3(margin, h - margin - offset, 0),
                      new Vector3(margin + gridLen, h - margin - offset, 0), gridColor);
            Draw.Line(new Vector3(margin + offset, h - margin, 0),
                      new Vector3(margin + offset, h - margin - gridLen, 0), gridColor);
            // Top-right
            Draw.Line(new Vector3(w - margin - gridLen, h - margin - offset, 0),
                      new Vector3(w - margin, h - margin - offset, 0), gridColor);
            Draw.Line(new Vector3(w - margin - offset, h - margin, 0),
                      new Vector3(w - margin - offset, h - margin - gridLen, 0), gridColor);
            // Bottom-left
            Draw.Line(new Vector3(margin, margin + offset, 0),
                      new Vector3(margin + gridLen, margin + offset, 0), gridColor);
            Draw.Line(new Vector3(margin + offset, margin, 0),
                      new Vector3(margin + offset, margin + gridLen, 0), gridColor);
            // Bottom-right
            Draw.Line(new Vector3(w - margin - gridLen, margin + offset, 0),
                      new Vector3(w - margin, margin + offset, 0), gridColor);
            Draw.Line(new Vector3(w - margin - offset, margin, 0),
                      new Vector3(w - margin - offset, margin + gridLen, 0), gridColor);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  DRAW — CORNER BRACKETS
    // ══════════════════════════════════════════════════════════════════

    private void DrawCornerBrackets(float w, float h)
    {
        float bracketLen = Mathf.Min(w, h) * 0.06f;
        float margin = 12f;

        Draw.Thickness = lineThickness;
        Draw.LineEndCaps = LineEndCap.None;

        // Top-left
        Draw.Line(new Vector3(margin, h - margin - bracketLen, 0),
                  new Vector3(margin, h - margin, 0), frameColor);
        Draw.Line(new Vector3(margin, h - margin, 0),
                  new Vector3(margin + bracketLen, h - margin, 0), frameColor);
        // Top-right
        Draw.Line(new Vector3(w - margin, h - margin - bracketLen, 0),
                  new Vector3(w - margin, h - margin, 0), frameColor);
        Draw.Line(new Vector3(w - margin, h - margin, 0),
                  new Vector3(w - margin - bracketLen, h - margin, 0), frameColor);
        // Bottom-left
        Draw.Line(new Vector3(margin, margin + bracketLen, 0),
                  new Vector3(margin, margin, 0), frameColor);
        Draw.Line(new Vector3(margin, margin, 0),
                  new Vector3(margin + bracketLen, margin, 0), frameColor);
        // Bottom-right
        Draw.Line(new Vector3(w - margin, margin + bracketLen, 0),
                  new Vector3(w - margin, margin, 0), frameColor);
        Draw.Line(new Vector3(w - margin, margin, 0),
                  new Vector3(w - margin - bracketLen, margin, 0), frameColor);
    }

    // ══════════════════════════════════════════════════════════════════
    //  DRAW — LEFT PANEL
    // ══════════════════════════════════════════════════════════════════

    private void DrawLeftPanel(float w, float h)
    {
        float margin = 12f;
        float lpX = margin + 20f;
        float lpY = margin + 30f;
        float lpW = 220f;
        float lpH = 180f;

        Draw.Rectangle(new Vector3(lpX + lpW * 0.5f, lpY + lpH * 0.5f, 0), lpW, lpH, bgPanelColor);
        Draw.RectangleBorder(new Vector3(lpX + lpW * 0.5f, lpY + lpH * 0.5f, 0),
                             lpW, lpH, lineThickness, frameColor);

        float headerY = lpY + lpH - 18f;
        DrawText("LIFE SUPPORT", new Vector3(lpX + 8f, headerY, 0), fontSizeSmall, frameColor);
        Draw.Line(new Vector3(lpX, lpY + lpH - 26f, 0),
                  new Vector3(lpX + lpW, lpY + lpH - 26f, 0), lineThickness * 0.5f, frameColor);

        float barX = lpX + 50f;
        float barW = 150f;
        float barH = 12f;
        float rowY = lpY + lpH - 52f;

        string o2Display = GetSlotDisplay(0, _o2Str);
        Color o2Col = GetPulsedColor(_o2Color, _o2Critical);
        DrawBarRow("O2", o2Display, barX, rowY, barW, barH, _oxygenNorm, o2Col, lpX);

        rowY -= 40f;
        string pwrDisplay = GetSlotDisplay(1, _energyStr);
        DrawBarRow("PWR", pwrDisplay, barX, rowY, barW, barH, _energyNorm, _energyColor, lpX);

        rowY -= 40f;
        string hullDisplay = GetSlotDisplay(2, _integrityStr);
        Color hullCol = GetPulsedColor(_integrityColor, _integrityCritical);
        DrawBarRow("HULL", hullDisplay, barX, rowY, barW, barH, _integrityNorm, hullCol, lpX);
    }

    private void DrawBarRow(string label, string valueStr, float barX, float y,
                             float barW, float barH, float norm, Color col, float labelX)
    {
        DrawText(label, new Vector3(labelX + 8f, y + 2f, 0), fontSizeSmall, textDimColor);
        DrawText(valueStr, new Vector3(barX + barW + 6f, y + 2f, 0), fontSizeSmall, col);

        Draw.Rectangle(new Vector3(barX + barW * 0.5f, y + barH * 0.5f, 0),
                       barW, barH, new Color(1f, 1f, 1f, 0.05f));
        Draw.RectangleBorder(new Vector3(barX + barW * 0.5f, y + barH * 0.5f, 0),
                             barW, barH, thinLine * 0.5f, frameColor * 0.5f);

        float fillW = barW * Mathf.Clamp01(norm);
        if (fillW > 0.5f)
        {
            Draw.Rectangle(new Vector3(barX + fillW * 0.5f, y + barH * 0.5f, 0),
                           fillW, barH - 2f, col * 0.8f);
        }

        Draw.Thickness = thinLine * 0.5f;
        for (int i = 1; i < 4; i++)
        {
            float tickX = barX + barW * (i * 0.25f);
            Draw.Line(new Vector3(tickX, y, 0),
                      new Vector3(tickX, y + barH, 0), frameColor * 0.3f);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  DRAW — RIGHT PANEL
    // ══════════════════════════════════════════════════════════════════

    private void DrawRightPanel(float w, float h)
    {
        float margin = 12f;
        float rpW = 200f;
        float rpX = w - margin - 20f - rpW;
        float rpY = margin + 30f;
        float rpH = 120f;

        Draw.Rectangle(new Vector3(rpX + rpW * 0.5f, rpY + rpH * 0.5f, 0), rpW, rpH, bgPanelColor);
        Draw.RectangleBorder(new Vector3(rpX + rpW * 0.5f, rpY + rpH * 0.5f, 0),
                             rpW, rpH, lineThickness, frameColor);

        float headerY = rpY + rpH - 18f;
        DrawText("ENVIRONMENT", new Vector3(rpX + 8f, headerY, 0), fontSizeSmall, frameColor);
        Draw.Line(new Vector3(rpX, rpY + rpH - 26f, 0),
                  new Vector3(rpX + rpW, rpY + rpH - 26f, 0), lineThickness * 0.5f, frameColor);

        float rowY = rpY + rpH - 52f;
        string depthDisplay = GetSlotDisplay(3, _depthStr);
        DrawText(depthDisplay, new Vector3(rpX + 10f, rowY, 0), fontSize, normalColor);

        rowY -= 32f;
        string pressDisplay = GetSlotDisplay(4, _pressureStr);
        DrawText(pressDisplay, new Vector3(rpX + 10f, rowY, 0), fontSize, normalColor);
    }

    // ══════════════════════════════════════════════════════════════════
    //  DRAW — ARC INDICATORS
    // ══════════════════════════════════════════════════════════════════

    private void DrawArcIndicators(float w, float h)
    {
        float centerX = w * 0.5f;
        float arcY = h - 60f;

        Vector3 o2Center = new Vector3(centerX - arcSpacing, arcY, 0);
        Color o2Col = GetPulsedColor(_o2Color, _o2Critical);
        DrawArcIndicator(o2Center, _oxygenNorm, o2Col, "O2", _o2Str);

        Vector3 enCenter = new Vector3(centerX + arcSpacing, arcY, 0);
        DrawArcIndicator(enCenter, _energyNorm, _energyColor, "PWR", _energyStr);
    }

    private void DrawArcIndicator(Vector3 center, float norm, Color col,
                                   string label, string valueStr)
    {
        Draw.Arc(center, arcRadius, arcThickness,
                 0f, Mathf.PI * 2f, new Color(1f, 1f, 1f, 0.05f));

        Draw.Arc(center, arcRadius, arcThickness * 0.5f,
                 Mathf.PI * 0.5f, Mathf.PI * 0.5f - Mathf.PI * 2f,
                 frameColor * 0.2f);

        float angle = Mathf.Clamp01(norm) * Mathf.PI * 2f;
        if (angle > 0.01f)
        {
            Draw.Arc(center, arcRadius, arcThickness,
                     Mathf.PI * 0.5f, Mathf.PI * 0.5f - angle, col);
        }

        DrawText(valueStr, new Vector3(center.x - 16f, center.y - 4f, 0), fontSizeSmall, col);
        DrawText(label, new Vector3(center.x - 8f, center.y - arcRadius - 14f, 0),
                 fontSizeTiny, textDimColor);
    }

    // ══════════════════════════════════════════════════════════════════
    //  DRAW — DEPTH SCALE
    // ══════════════════════════════════════════════════════════════════

    private void DrawDepthScale(float w, float h)
    {
        float scaleX   = w - 40f;
        float scaleTop = h * 0.75f;
        float scaleBot = h * 0.25f;
        float scaleH   = scaleTop - scaleBot;

        Draw.Line(new Vector3(scaleX, scaleBot, 0),
                  new Vector3(scaleX, scaleTop, 0), thinLine, depthScaleColor);

        float tickSpacing = 8f;
        float offset      = (_depthScaleOffset % 10f) * (tickSpacing / 10f);
        int   tickCount   = (int)(scaleH / tickSpacing) + 2;
        float baseDepth   = Mathf.Floor(_depthScaleOffset / 10f) * 10f;

        for (int i = -1; i < tickCount; i++)
        {
            float tickY = scaleBot + i * tickSpacing + offset;
            if (tickY < scaleBot || tickY > scaleTop) continue;

            float depth     = baseDepth + (tickCount - 1 - i) * 10f;
            bool  majorTick = (Mathf.Abs(depth % 50f) < 0.1f);
            float tickW     = majorTick ? 12f : 6f;
            Color tickCol   = majorTick ? depthScaleColor : depthScaleColor * 0.5f;

            Draw.Line(new Vector3(scaleX - tickW, tickY, 0),
                      new Vector3(scaleX, tickY, 0), thinLine, tickCol);

            if (majorTick)
            {
                int depthIdx = Mathf.Clamp((int)depth, 0, MaxDepth);
                DrawText(IntStrings[depthIdx],
                         new Vector3(scaleX - tickW - 30f, tickY - 4f, 0),
                         fontSizeTiny, depthScaleColor);
            }
        }

        float markerY = (scaleTop + scaleBot) * 0.5f;
        Draw.Triangle(new Vector3(scaleX + 4f, markerY + 5f, 0),
                      new Vector3(scaleX + 4f, markerY - 5f, 0),
                      new Vector3(scaleX + 12f, markerY, 0),
                      normalColor);

        DrawText(_depthStr, new Vector3(scaleX + 15f, markerY - 5f, 0), fontSizeSmall, normalColor);
    }

    // ══════════════════════════════════════════════════════════════════
    //  DRAW — TIME DISPLAY
    // ══════════════════════════════════════════════════════════════════

    private void DrawTimeDisplay(float w, float h)
    {
        float cx = w * 0.5f;
        float ty = h - 20f;

        DrawText(_timeStr, new Vector3(cx - 24f, ty, 0), fontSizeSmall, textDimColor);

        float lineW = 40f;
        Draw.Line(new Vector3(cx - 60f, ty + 5f, 0),
                  new Vector3(cx - 60f + lineW, ty + 5f, 0), thinLine * 0.5f, frameColor * 0.3f);
        Draw.Line(new Vector3(cx + 20f, ty + 5f, 0),
                  new Vector3(cx + 20f + lineW, ty + 5f, 0), thinLine * 0.5f, frameColor * 0.3f);
    }

    // ══════════════════════════════════════════════════════════════════
    //  DRAW — STATUS BAR
    // ══════════════════════════════════════════════════════════════════

    private void DrawStatusBar(float w, float h)
    {
        float cx = w * 0.5f;
        float sy = 20f;

        Color statusCol = (_o2Critical || _integrityCritical)
            ? GetPulsedColor(criticalColor, true)
            : normalColor;

        DrawText(_statusText, new Vector3(cx - 80f, sy, 0), fontSizeSmall, statusCol);

        Draw.Line(new Vector3(cx - 120f, sy + 5f, 0),
                  new Vector3(cx - 90f,  sy + 5f, 0), thinLine, frameColor * 0.5f);
        Draw.Line(new Vector3(cx + 90f,  sy + 5f, 0),
                  new Vector3(cx + 120f, sy + 5f, 0), thinLine, frameColor * 0.5f);
    }

    // ══════════════════════════════════════════════════════════════════
    //  DRAW — CRITICAL OVERLAY
    // ══════════════════════════════════════════════════════════════════

    private void DrawCriticalOverlay(float w, float h)
    {
        if (!_o2Critical && !_integrityCritical) return;

        float pulse01 = PulseSin01();
        float alpha   = pulse01 * 0.15f;

        Color overlay = new Color(criticalColor.r, criticalColor.g, criticalColor.b, alpha);
        Draw.Rectangle(new Vector3(w * 0.5f, h * 0.5f, 0), w, h, overlay);

        float edgeAlpha = pulse01 * 0.4f;
        Color edgeCol   = new Color(criticalColor.r, criticalColor.g, criticalColor.b, edgeAlpha);
        float edgeThick = 3f;

        Draw.Line(new Vector3(0, 0, 0), new Vector3(w, 0, 0), edgeThick, edgeCol);
        Draw.Line(new Vector3(0, h, 0), new Vector3(w, h, 0), edgeThick, edgeCol);
        Draw.Line(new Vector3(0, 0, 0), new Vector3(0, h, 0), edgeThick, edgeCol);
        Draw.Line(new Vector3(w, 0, 0), new Vector3(w, h, 0), edgeThick, edgeCol);
    }

    // ══════════════════════════════════════════════════════════════════
    //  DRAW — INTERACT PROMPT
    // ══════════════════════════════════════════════════════════════════

    private void DrawInteractPrompt(float w, float h)
    {
        if (string.IsNullOrEmpty(_interactPromptText)) return;

        float cx = w * 0.5f;
        float cy = h * 0.5f;
        float hw  = promptBracketHalfW;
        float hh  = promptBracketHalfH;
        float arm = promptBracketArm;

        float l = cx - hw;
        float r = cx + hw;
        float t = cy + hh;
        float b = cy - hh;

        Draw.LineEndCaps = LineEndCap.None;
        Draw.Rectangle(new Vector3(cx, cy, 0), hw * 2f, hh * 2f,
                       new Color(bgPanelColor.r, bgPanelColor.g, bgPanelColor.b, bgPanelColor.a * 0.4f));
        Draw.Thickness = lineThickness;

        Draw.Line(new Vector3(l, t, 0),        new Vector3(l + arm, t, 0),  normalColor);
        Draw.Line(new Vector3(l, t, 0),        new Vector3(l, t - arm, 0),  normalColor);
        Draw.Line(new Vector3(r - arm, t, 0),  new Vector3(r, t, 0),        normalColor);
        Draw.Line(new Vector3(r, t, 0),        new Vector3(r, t - arm, 0),  normalColor);
        Draw.Line(new Vector3(l, b + arm, 0),  new Vector3(l, b, 0),        normalColor);
        Draw.Line(new Vector3(l, b, 0),        new Vector3(l + arm, b, 0),  normalColor);
        Draw.Line(new Vector3(r, b + arm, 0),  new Vector3(r, b, 0),        normalColor);
        Draw.Line(new Vector3(r - arm, b, 0),  new Vector3(r, b, 0),        normalColor);

        float crossLen = 8f;
        float crossGap = 3f;
        Draw.Thickness = thinLine;

        Draw.Line(new Vector3(cx - crossLen, cy, 0), new Vector3(cx - crossGap, cy, 0), normalColor);
        Draw.Line(new Vector3(cx + crossGap, cy, 0), new Vector3(cx + crossLen, cy, 0), normalColor);
        Draw.Line(new Vector3(cx, cy + crossLen, 0), new Vector3(cx, cy + crossGap, 0), normalColor);
        Draw.Line(new Vector3(cx, cy - crossGap, 0), new Vector3(cx, cy - crossLen, 0), normalColor);

        float tick = 5f;
        Color tickC = normalColor * 0.35f;

        Draw.Line(new Vector3(l, cy, 0),        new Vector3(l + tick, cy, 0), tickC);
        Draw.Line(new Vector3(r - tick, cy, 0), new Vector3(r, cy, 0),        tickC);
        Draw.Line(new Vector3(cx, t, 0),        new Vector3(cx, t - tick, 0), tickC);
        Draw.Line(new Vector3(cx, b, 0),        new Vector3(cx, b + tick, 0), tickC);

        float qTick = 3f;
        float q1 = cx - hw * 0.5f;
        float q3 = cx + hw * 0.5f;

        Draw.Line(new Vector3(q1, t, 0), new Vector3(q1, t - qTick, 0), tickC);
        Draw.Line(new Vector3(q3, t, 0), new Vector3(q3, t - qTick, 0), tickC);
        Draw.Line(new Vector3(q1, b, 0), new Vector3(q1, b + qTick, 0), tickC);
        Draw.Line(new Vector3(q3, b, 0), new Vector3(q3, b + qTick, 0), tickC);

        float textY = b - fontSize - 6f;
        DrawTextCentered(_interactPromptText, new Vector3(cx, textY, 0), fontSize, normalColor);

        float flankLen = 30f;
        float flankGap = 90f;
        Draw.Thickness = thinLine * 0.5f;
        Color flankCol = frameColor * 0.4f;
        float flankY = textY + fontSize * 0.4f;

        Draw.Line(new Vector3(cx - flankGap - flankLen, flankY, 0),
                  new Vector3(cx - flankGap, flankY, 0), flankCol);
        Draw.Line(new Vector3(cx + flankGap, flankY, 0),
                  new Vector3(cx + flankGap + flankLen, flankY, 0), flankCol);
    }

    // ══════════════════════════════════════════════════════════════════
    //  TEXT HELPERS
    // ══════════════════════════════════════════════════════════════════

    private void DrawText(string text, Vector3 position, float size, Color color)
    {
        if (string.IsNullOrEmpty(text)) return;
        Draw.Text(position, Quaternion.identity, text, TextAlign.Left, size, hudFont, color);
    }

    private void DrawTextCentered(string text, Vector3 position, float size, Color color)
    {
        if (string.IsNullOrEmpty(text)) return;
        Draw.Text(position, Quaternion.identity, text, TextAlign.Center, size, hudFont, color);
    }

    // ══════════════════════════════════════════════════════════════════
    //  PULSE / COLOR HELPERS
    // ══════════════════════════════════════════════════════════════════

    private float PulseSin01()
        => (Mathf.Sin(_pulseTimer * Mathf.PI * 2f) + 1f) * 0.5f;

    private Color GetPulsedColor(Color baseCol, bool critical)
    {
        if (!critical) return baseCol;
        float a = Mathf.Lerp(0.2f, 1f, PulseSin01());
        return new Color(baseCol.r, baseCol.g, baseCol.b, a);
    }

    private Color EvalStatColor(float normalized)
    {
        if (normalized > WarningThreshold)  return normalColor;
        if (normalized > CriticalThreshold) return warningColor;
        return criticalColor;
    }

    private string GetSlotDisplay(int slot, string clean)
        => _glitchOverlay[slot] ?? clean;

    /// <summary>Returns color with alpha scaled by factor. Zero GC (struct).</summary>
    private static Color ScaleAlpha(Color c, float alpha)
        => new Color(c.r, c.g, c.b, c.a * alpha);

    // ══════════════════════════════════════════════════════════════════
    //  TIME STRING
    // ══════════════════════════════════════════════════════════════════

    private void UpdateTimeString()
    {
        var now = DateTime.Now;
        if (now.Second == _lastSecond) return;
        _lastSecond = now.Second;

        _sb.Clear();
        AppendTwoDigit(_sb, now.Hour);
        _sb.Append(':');
        AppendTwoDigit(_sb, now.Minute);
        _sb.Append(':');
        AppendTwoDigit(_sb, now.Second);
        _timeStr = _sb.ToString();
    }

    private static void AppendTwoDigit(StringBuilder sb, int val)
    {
        if (val < 10) sb.Append('0');
        sb.Append(val);
    }

    // ══════════════════════════════════════════════════════════════════
    //  EVENT WIRING
    // ══════════════════════════════════════════════════════════════════

    private void Subscribe()
    {
        if (survival == null) return;
        survival.OnOxygenChanged    += HandleOxygen;
        survival.OnEnergyChanged    += HandleEnergy;
        survival.OnDepthChanged     += HandleDepth;
        survival.OnIntegrityChanged += HandleIntegrity;
        survival.OnPressureChanged  += HandlePressure;
        survival.OnOxygenCritical   += HandleOxygenCritical;
        survival.OnDeath            += HandleDeath;
    }

    private void Unsubscribe()
    {
        if (survival == null) return;
        survival.OnOxygenChanged    -= HandleOxygen;
        survival.OnEnergyChanged    -= HandleEnergy;
        survival.OnDepthChanged     -= HandleDepth;
        survival.OnIntegrityChanged -= HandleIntegrity;
        survival.OnPressureChanged  -= HandlePressure;
        survival.OnOxygenCritical   -= HandleOxygenCritical;
        survival.OnDeath            -= HandleDeath;
    }

    // ══════════════════════════════════════════════════════════════════
    //  EVENT HANDLERS
    // ══════════════════════════════════════════════════════════════════

    private void HandleOxygen(float value)
    {
        _oxygenNorm = value / survival.Stats.MaxOxygen;
        _oxygenPct  = Mathf.Clamp((int)(_oxygenNorm * 100f), 0, 100);
        _o2Str      = PercentStrings[_oxygenPct];
        _o2Color    = EvalStatColor(_oxygenNorm);

        if (_oxygenNorm >= CriticalThreshold && _o2Critical)
        {
            _o2Critical = false;
            _statusText = "SYS NOMINAL";
        }
    }

    private void HandleEnergy(float value)
    {
        _energyNorm  = value / survival.Stats.MaxEnergy;
        _energyPct   = Mathf.Clamp((int)(_energyNorm * 100f), 0, 100);
        _energyStr   = PercentStrings[_energyPct];
        _energyColor = EvalStatColor(_energyNorm);
    }

    private void HandleIntegrity(float value)
    {
        _integrityNorm     = value / survival.Stats.MaxIntegrity;
        _integrityPct      = Mathf.Clamp((int)(_integrityNorm * 100f), 0, 100);
        _integrityStr      = PercentStrings[_integrityPct];
        _integrityColor    = EvalStatColor(_integrityNorm);
        _integrityCritical = _integrityNorm < CriticalThreshold;
    }

    private void HandleDepth(float value)
    {
        _depthInt         = (int)value;
        _depthScaleTarget = value;
        int idx           = Mathf.Clamp(_depthInt, 0, MaxDepth);
        _depthStr         = DepthStrings[idx];
    }

    private void HandlePressure(float value)
    {
        _pressureInt = (int)value;
        int idx      = Mathf.Clamp(_pressureInt, 0, MaxPressure);
        _pressureStr = PressureStrings[idx];
    }

    private void HandleOxygenCritical(float normalizedPct)
    {
        _o2Critical = true;
        _statusText = normalizedPct < 0.05f
            ? ">> LIFE SUPPORT FAILURE <<"
            : ">> O2 CRITICAL <<";
    }

    private void HandleDeath()
    {
        if (_noiseHandle != null)
        {
            StopCoroutine(_noiseHandle);
            _noiseHandle = null;
        }
        _o2Critical        = false;
        _integrityCritical = false;
        _statusText        = ">> SIGNAL LOST <<";
    }

    private void HandleHoverChanged(IInteractable target)
        => _interactPromptText = target != null ? target.GetInteractText() : null;

    // ══════════════════════════════════════════════════════════════════
    //  FORCE REFRESH
    // ══════════════════════════════════════════════════════════════════

    private void ForceRefreshAll()
    {
        if (survival == null) return;
        HandleOxygen(survival.Oxygen);
        HandleEnergy(survival.Energy);
        HandleDepth(survival.Depth);
        HandleIntegrity(survival.Integrity);
        HandlePressure(survival.Pressure);
        _statusText = "SYS NOMINAL";
    }

    // ══════════════════════════════════════════════════════════════════
    //  DIGITAL NOISE — GLITCH
    // ══════════════════════════════════════════════════════════════════

    private IEnumerator NoiseLoop()
    {
        var glitchPause = new WaitForSeconds(noiseDuration);
        int[] picks = new int[3];

        while (true)
        {
            yield return new WaitForSeconds(
                Random.Range(noiseMinInterval, noiseMaxInterval));

            int count = Random.Range(1, math.min(4, SlotCount + 1));
            for (int i = 0; i < count; i++)
                picks[i] = Random.Range(0, SlotCount);

            for (int i = 0; i < count; i++)
            {
                int idx = picks[i];
                string clean = GetCleanString(idx);
                if (clean == null) continue;
                _glitchOverlay[idx] = Corrupt(clean);
            }

            yield return glitchPause;

            for (int i = 0; i < count; i++)
                _glitchOverlay[picks[i]] = null;
        }
    }

    private string GetCleanString(int slot)
    {
        switch (slot)
        {
            case 0: return _o2Str;
            case 1: return _energyStr;
            case 2: return _integrityStr;
            case 3: return _depthStr;
            case 4: return _pressureStr;
            default: return null;
        }
    }

    private string Corrupt(string src)
    {
        _sbGlitch.Clear();
        _sbGlitch.Append(src);
        int hits = math.max(1, _sbGlitch.Length / 3);
        for (int i = 0; i < hits; i++)
        {
            int pos = Random.Range(0, _sbGlitch.Length);
            _sbGlitch[pos] = Glyphs[Random.Range(0, Glyphs.Length)];
        }
        return _sbGlitch.ToString();
    }
}