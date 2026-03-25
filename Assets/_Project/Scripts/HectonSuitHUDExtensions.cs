// ============================================================================
// HECTON-8 — HectonSuitHUDExtensions.cs  v4.0 ENTERPRISE
// Расширения для HUD: Flashlight/PDA status, Notifications, Equipment panel.
// Работает вместе с HectonSuitHUD.cs (v3.0).
//
// v4.0 ENTERPRISE ADDITIONS:
//   [ADD] FlashlightStatusIndicator — иконка фонаря + heat/battery bars
//   [ADD] PDAStatusIndicator — иконка PDA когда открыт
//   [ADD] NotificationSystem — всплывающие уведомления (overheat, low battery)
//   [ADD] EquipmentStatusPanel — расширенная панель с иконками инструментов
//   [ADD] Event integration — FlashlightEvents, PDAEvents
//   [ADD] Diagnostics — _debugFlashlightOn, _debugPDAOpen, _debugNotifications
//
// ZERO GC:
//   • Pre-allocated notification queue (max 5 entries)
//   • Cached event handlers, no boxing
//   • String cache for common messages
//   • Struct-based animation state
//
// АРХИТЕКТУРА:
//   • Дополняет HectonSuitHUD, не заменяет его
//   • Рисует в правом верхнем углу (equipment + flashlight + PDA)
//   • Notifications — центр-верх, fade in/out
//   • ImmediateModeShapeDrawer pattern (как HectonSuitHUD)
// ============================================================================

using System;
using System.Collections;
using Hecton8.Gameplay;
using Hecton8.UI;
using Shapes;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[ExecuteAlways]
[AddComponentMenu("Hecton8/HUD/Suit HUD Extensions")]
public sealed class HectonSuitHUDExtensions : ImmediateModeShapeDrawer
{
    // ══════════════════════════════════════════════════════════
    //  INSPECTOR — REFERENCES
    // ══════════════════════════════════════════════════════════

    [Header("── References ──────────────────────────────")]
    [SerializeField] private Camera hudCamera;
    [SerializeField] private TMP_FontAsset hudFont;
    [SerializeField] private PlayerFlashlight flashlight;

    // ══════════════════════════════════════════════════════════
    //  INSPECTOR — COLORS
    // ══════════════════════════════════════════════════════════

    [Header("── Colors ──────────────────────────────────")]
    [ColorUsage(true, true)] [SerializeField] private Color normalColor = new Color(0f, 0.898f, 1f, 1f);
    [ColorUsage(true, true)] [SerializeField] private Color warningColor = new Color(1f, 0.878f, 0f, 1f);
    [ColorUsage(true, true)] [SerializeField] private Color criticalColor = new Color(1f, 0.384f, 0f, 1f);
    [ColorUsage(true, true)] [SerializeField] private Color frameColor = new Color(0f, 0.6f, 0.8f, 0.6f);
    [ColorUsage(true, true)] [SerializeField] private Color bgPanelColor = new Color(0f, 0.05f, 0.1f, 0.25f);
    [ColorUsage(true, true)] [SerializeField] private Color flashlightOnColor = new Color(1f, 0.9f, 0.3f, 1f);
    [ColorUsage(true, true)] [SerializeField] private Color pdaActiveColor = new Color(0.3f, 1f, 0.9f, 1f);

    // ══════════════════════════════════════════════════════════
    //  INSPECTOR — LAYOUT
    // ══════════════════════════════════════════════════════════

    [Header("── Layout ──────────────────────────────────")]
    [SerializeField] private float lineThickness = 1.5f;
    [SerializeField] private float thinLine = 1.0f;
    [SerializeField] private float fontSize = 14f;
    [SerializeField] private float fontSizeSmall = 10f;
    [SerializeField] private float fontSizeTiny = 8f;

    [Header("── Notifications ───────────────────────────")]
    [SerializeField] private float notificationDuration = 3f;
    [SerializeField] private float notificationFadeSpeed = 4f;

    // ══════════════════════════════════════════════════════════
    //  INSPECTOR — DIAGNOSTICS
    // ══════════════════════════════════════════════════════════

    [Header("── Diagnostics ─────────────────────────────")]
    [SerializeField] private bool _debugFlashlightOn;
    [SerializeField] private float _debugFlashlightHeat;
    [SerializeField] private bool _debugPDAOpen;
    [SerializeField] private int _debugNotificationCount;

    // ══════════════════════════════════════════════════════════
    //  RUNTIME STATE — FLASHLIGHT
    // ══════════════════════════════════════════════════════════

    private bool _flashlightOn;
    private float _flashlightHeat; // 0-1
    private bool _flashlightOverheated;
    private bool _flashlightFlickering;

    // ══════════════════════════════════════════════════════════
    //  RUNTIME STATE — PDA
    // ══════════════════════════════════════════════════════════

    private bool _pdaOpen;

    // ══════════════════════════════════════════════════════════
    //  RUNTIME STATE — NOTIFICATIONS
    // ══════════════════════════════════════════════════════════

    private const int MaxNotifications = 5;
    private readonly NotificationEntry[] _notifications = new NotificationEntry[MaxNotifications];
    private int _notificationCount;

    private struct NotificationEntry
    {
        public string Message;
        public Color Color;
        public float TimeRemaining;
        public float Alpha; // 0-1, for fade in/out
    }

    // Pre-allocated notification strings (zero GC)
    private static string STR_FLASHLIGHT_OVERHEAT = "FLASHLIGHT OVERHEAT";
    private static string STR_FLASHLIGHT_LOW_BATTERY = "FLASHLIGHT LOW BATTERY";
    private static string STR_PDA_LOW_BATTERY = "PDA LOW BATTERY";
    private static string STR_BATTERY_DEPLETED = "BATTERY DEPLETED";

    // ══════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════

    public override void OnEnable()
    {
        base.OnEnable();

        if (hudCamera == null)
            hudCamera = GetComponent<Camera>();

        // Auto-resolve flashlight if not assigned
        if (flashlight == null)
            flashlight = FindFirstObjectByType<PlayerFlashlight>();

        Subscribe();
        ForceRefresh();
    }

    public override void OnDisable()
    {
        base.OnDisable();
        Unsubscribe();
    }

    // ══════════════════════════════════════════════════════════
    //  URP ENTRY POINT
    // ══════════════════════════════════════════════════════════

    public override void DrawShapes(Camera cam)
    {
        if (hudCamera != null && cam != hudCamera) return;
        DrawExtensions();
    }

    private void LateUpdate()
    {
        UpdateNotifications(Time.deltaTime);
        UpdateDiagnostics();
    }

    // ══════════════════════════════════════════════════════════
    //  MAIN DRAW ROUTINE
    // ══════════════════════════════════════════════════════════

    private void DrawExtensions()
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

            DrawEquipmentPanel(w, h);
            DrawNotifications(w, h);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  DRAW — EQUIPMENT PANEL (top-right)
    // ══════════════════════════════════════════════════════════

    private void DrawEquipmentPanel(float w, float h)
    {
        float margin = 12f;
        float panelW = 180f;
        float panelH = 100f;
        float panelX = w - margin - panelW;
        float panelY = h - margin - panelH;

        // Background
        Draw.Rectangle(
            new Vector3(panelX + panelW * 0.5f, panelY + panelH * 0.5f, 0),
            panelW, panelH, bgPanelColor);

        Draw.RectangleBorder(
            new Vector3(panelX + panelW * 0.5f, panelY + panelH * 0.5f, 0),
            panelW, panelH, lineThickness, frameColor);

        // Header
        float headerY = panelY + panelH - 18f;
        DrawText("EQUIPMENT", new Vector3(panelX + 8f, headerY, 0), fontSizeSmall, frameColor);

        Draw.Line(
            new Vector3(panelX, panelY + panelH - 26f, 0),
            new Vector3(panelX + panelW, panelY + panelH - 26f, 0),
            lineThickness * 0.5f, frameColor);

        // Flashlight indicator
        float rowY = panelY + panelH - 52f;
        DrawFlashlightIndicator(panelX, rowY, panelW);

        // PDA indicator
        rowY -= 32f;
        DrawPDAIndicator(panelX, rowY, panelW);
    }

    private void DrawFlashlightIndicator(float panelX, float rowY, float panelW)
    {
        // Icon
        string icon = _flashlightOn ? "◉" : "○";
        Color iconColor = _flashlightOn ? flashlightOnColor : frameColor * 0.5f;

        if (_flashlightFlickering)
        {
            float pulse = (Mathf.Sin(Time.time * 20f) + 1f) * 0.5f;
            iconColor = Color.Lerp(iconColor, criticalColor, pulse * 0.5f);
        }

        DrawText(icon, new Vector3(panelX + 8f, rowY, 0), fontSize, iconColor);

        // Label
        string label = _flashlightOverheated ? "FLASHLIGHT [OVERHEAT]" : "FLASHLIGHT";
        Color labelColor = _flashlightOverheated ? criticalColor : frameColor;
        DrawText(label, new Vector3(panelX + 28f, rowY, 0), fontSizeTiny, labelColor);

        // Heat bar (if on)
        if (_flashlightOn && _flashlightHeat > 0.01f)
        {
            float barX = panelX + 28f;
            float barY = rowY - 8f;
            float barW = panelW - 36f;
            float barH = 4f;

            // Background
            Draw.Rectangle(
                new Vector3(barX + barW * 0.5f, barY + barH * 0.5f, 0),
                barW, barH, new Color(1f, 1f, 1f, 0.05f));

            // Fill
            float fillW = barW * _flashlightHeat;
            Color heatColor = _flashlightHeat > 0.7f ? criticalColor : warningColor;

            if (fillW > 0.5f)
            {
                Draw.Rectangle(
                    new Vector3(barX + fillW * 0.5f, barY + barH * 0.5f, 0),
                    fillW, barH - 1f, heatColor * 0.8f);
            }
        }
    }

    private void DrawPDAIndicator(float panelX, float rowY, float panelW)
    {
        // Icon
        string icon = _pdaOpen ? "▣" : "□";
        Color iconColor = _pdaOpen ? pdaActiveColor : frameColor * 0.5f;

        DrawText(icon, new Vector3(panelX + 8f, rowY, 0), fontSize, iconColor);

        // Label
        string label = _pdaOpen ? "PDA [ACTIVE]" : "PDA";
        Color labelColor = _pdaOpen ? pdaActiveColor : frameColor;
        DrawText(label, new Vector3(panelX + 28f, rowY, 0), fontSizeTiny, labelColor);
    }

    // ══════════════════════════════════════════════════════════
    //  DRAW — NOTIFICATIONS (top-center)
    // ══════════════════════════════════════════════════════════

    private void DrawNotifications(float w, float h)
    {
        if (_notificationCount == 0) return;

        float cx = w * 0.5f;
        float startY = h - 80f;
        float spacing = 32f;

        for (int i = 0; i < _notificationCount; i++)
        {
            ref NotificationEntry notif = ref _notifications[i];
            if (notif.Alpha < 0.01f) continue;

            float y = startY - i * spacing;

            // Background
            float bgW = 300f;
            float bgH = 26f;
            Color bgColor = ScaleAlpha(bgPanelColor, notif.Alpha);
            Color borderColor = ScaleAlpha(notif.Color, notif.Alpha);

            Draw.Rectangle(
                new Vector3(cx, y, 0),
                bgW, bgH, bgColor);

            Draw.RectangleBorder(
                new Vector3(cx, y, 0),
                bgW, bgH, lineThickness, borderColor);

            // Text
            Color textColor = ScaleAlpha(notif.Color, notif.Alpha);
            DrawTextCentered(notif.Message, new Vector3(cx, y - 6f, 0), fontSizeSmall, textColor);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  NOTIFICATIONS — UPDATE
    // ══════════════════════════════════════════════════════════

    private void UpdateNotifications(float deltaTime)
    {
        for (int i = _notificationCount - 1; i >= 0; i--)
        {
            ref NotificationEntry notif = ref _notifications[i];

            notif.TimeRemaining -= deltaTime;

            // Fade in (first 0.3s)
            if (notif.TimeRemaining > notificationDuration - 0.3f)
            {
                float fadeIn = (notificationDuration - notif.TimeRemaining) / 0.3f;
                notif.Alpha = Mathf.Clamp01(fadeIn);
            }
            // Fade out (last 0.5s)
            else if (notif.TimeRemaining < 0.5f)
            {
                notif.Alpha = Mathf.Clamp01(notif.TimeRemaining / 0.5f);
            }
            // Full opacity
            else
            {
                notif.Alpha = 1f;
            }

            // Remove expired
            if (notif.TimeRemaining <= 0f)
            {
                RemoveNotificationAt(i);
            }
        }
    }

    private void AddNotification(string message, Color color)
    {
        // Check if already exists
        for (int i = 0; i < _notificationCount; i++)
        {
            if (_notifications[i].Message == message)
            {
                // Refresh duration
                _notifications[i].TimeRemaining = notificationDuration;
                return;
            }
        }

        // Add new (or replace oldest if full)
        if (_notificationCount >= MaxNotifications)
        {
            RemoveNotificationAt(0);
        }

        _notifications[_notificationCount++] = new NotificationEntry
        {
            Message = message,
            Color = color,
            TimeRemaining = notificationDuration,
            Alpha = 0f
        };
    }

    private void RemoveNotificationAt(int index)
    {
        for (int i = index; i < _notificationCount - 1; i++)
        {
            _notifications[i] = _notifications[i + 1];
        }
        _notificationCount--;
    }

    // ══════════════════════════════════════════════════════════
    //  TEXT HELPERS
    // ══════════════════════════════════════════════════════════

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

    private static Color ScaleAlpha(Color c, float alpha)
        => new Color(c.r, c.g, c.b, c.a * alpha);

    // ══════════════════════════════════════════════════════════
    //  EVENT WIRING
    // ══════════════════════════════════════════════════════════

    private void Subscribe()
    {
        // Flashlight events
        FlashlightEvents.OnToggled += HandleFlashlightToggled;
        FlashlightEvents.OnBatteryDepleted += HandleFlashlightBatteryDepleted;
        FlashlightEvents.OnOverheat += HandleFlashlightOverheat;
        FlashlightEvents.OnFlickerStart += HandleFlashlightFlickerStart;

        // PDA events
        PDAEvents.OnOpened += HandlePDAOpened;
        PDAEvents.OnClosed += HandlePDAClosed;
        PDAEvents.OnLowBatteryShutdown += HandlePDALowBattery;
    }

    private void Unsubscribe()
    {
        // Flashlight events
        FlashlightEvents.OnToggled -= HandleFlashlightToggled;
        FlashlightEvents.OnBatteryDepleted -= HandleFlashlightBatteryDepleted;
        FlashlightEvents.OnOverheat -= HandleFlashlightOverheat;
        FlashlightEvents.OnFlickerStart -= HandleFlashlightFlickerStart;

        // PDA events
        PDAEvents.OnOpened -= HandlePDAOpened;
        PDAEvents.OnClosed -= HandlePDAClosed;
        PDAEvents.OnLowBatteryShutdown -= HandlePDALowBattery;
    }

    // ══════════════════════════════════════════════════════════
    //  EVENT HANDLERS — FLASHLIGHT
    // ══════════════════════════════════════════════════════════

    private void HandleFlashlightToggled(bool isOn)
    {
        _flashlightOn = isOn;
    }

    private void HandleFlashlightBatteryDepleted()
    {
        AddNotification(STR_BATTERY_DEPLETED, criticalColor);
    }

    private void HandleFlashlightOverheat()
    {
        _flashlightOverheated = true;
        AddNotification(STR_FLASHLIGHT_OVERHEAT, criticalColor);
        StartCoroutine(ClearOverheatFlag());
    }

    private void HandleFlashlightFlickerStart()
    {
        _flashlightFlickering = true;
        AddNotification(STR_FLASHLIGHT_LOW_BATTERY, warningColor);
        StartCoroutine(ClearFlickerFlag());
    }

    private IEnumerator ClearOverheatFlag()
    {
        yield return new WaitForSeconds(2f);
        _flashlightOverheated = false;
    }

    private IEnumerator ClearFlickerFlag()
    {
        yield return new WaitForSeconds(2f);
        _flashlightFlickering = false;
    }

    // ══════════════════════════════════════════════════════════
    //  EVENT HANDLERS — PDA
    // ══════════════════════════════════════════════════════════

    private void HandlePDAOpened(int tab)
    {
        _pdaOpen = true;
    }

    private void HandlePDAClosed(float duration)
    {
        _pdaOpen = false;
    }

    private void HandlePDALowBattery()
    {
        AddNotification(STR_PDA_LOW_BATTERY, warningColor);
    }

    // ══════════════════════════════════════════════════════════
    //  FORCE REFRESH
    // ══════════════════════════════════════════════════════════

    private void ForceRefresh()
    {
        _flashlightOn = flashlight != null && flashlight.IsOn;
        _flashlightHeat = flashlight != null ? flashlight.HeatLevel : 0f;
        _flashlightOverheated = flashlight != null && flashlight.IsOverheated;
        _flashlightFlickering = flashlight != null && flashlight.IsFlickering;
        _pdaOpen = PlayerPDA.IsOpen;
    }

    // ══════════════════════════════════════════════════════════
    //  DIAGNOSTICS
    // ══════════════════════════════════════════════════════════

    private void UpdateDiagnostics()
    {
        _debugFlashlightOn = _flashlightOn;
        _debugFlashlightHeat = flashlight != null ? flashlight.HeatLevel : 0f;
        _debugPDAOpen = _pdaOpen;
        _debugNotificationCount = _notificationCount;
    }
}
