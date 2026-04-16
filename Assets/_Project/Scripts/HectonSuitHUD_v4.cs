using System.Collections.Generic;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.UI;
using Shapes;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[ExecuteAlways]
[AddComponentMenu("Hecton8/HUD/Suit HUD v4")]
public sealed class HectonSuitHUD_v4 : ImmediateModeShapeDrawer, ITickable
{
    private static readonly List<HectonSuitHUD_v4> s_activeHuds = new List<HectonSuitHUD_v4>(4);
    private const int MaxPercent = 100;
    private const int MaxDepth = 5000;
    private const int MaxPressure = 600;
    private const int MaxHeading = 360;
    private const int MinTemperatureDeci = -1000;
    private const int MaxTemperatureDeci = 2000;
    private const int MaxSuitMassKg = 2000;

    private static readonly string[] PercentStrings;
    private static readonly string[] DepthStrings;
    private static readonly string[] PressureStrings;
    private static readonly string[] HeadingStrings;
    private static readonly string[] TemperatureStrings;
    private static readonly string[] MassStrings;

    static HectonSuitHUD_v4()
    {
        PercentStrings = new string[MaxPercent + 1];
        for (int i = 0; i <= MaxPercent; i++)
            PercentStrings[i] = i.ToString() + "%";

        DepthStrings = new string[MaxDepth + 1];
        for (int i = 0; i <= MaxDepth; i++)
            DepthStrings[i] = i.ToString("N0") + " m";

        PressureStrings = new string[MaxPressure + 1];
        for (int i = 0; i <= MaxPressure; i++)
            PressureStrings[i] = i.ToString() + " atm";

        HeadingStrings = new string[MaxHeading + 1];
        for (int i = 0; i <= MaxHeading; i++)
            HeadingStrings[i] = i.ToString("000");

        TemperatureStrings = new string[(MaxTemperatureDeci - MinTemperatureDeci) + 1];
        for (int deci = MinTemperatureDeci; deci <= MaxTemperatureDeci; deci++)
            TemperatureStrings[deci - MinTemperatureDeci] = $"{deci * 0.1f:0.0} C";

        MassStrings = new string[MaxSuitMassKg + 1];
        for (int i = 0; i <= MaxSuitMassKg; i++)
            MassStrings[i] = i.ToString() + " kg";
    }

    [Header("Core")]
    [SerializeField] private Camera hudCamera;
    [SerializeField] private TMP_FontAsset hudFont;
    [SerializeField] private HectonSurvivalSystem survival;
    [SerializeField] private HectonPlayerMovement playerMovement;
    [SerializeField] private PlayerFlashlight flashlight;
    [SerializeField] private HectonUnderwaterVisuals underwaterVisuals;
    [SerializeField] private SuitHUDProfile defaultHudProfile;

    [Header("Palette")]
    [ColorUsage(true, true)] [SerializeField] private Color primaryColor = new Color(0.26f, 0.98f, 1f, 1f);
    [ColorUsage(true, true)] [SerializeField] private Color secondaryColor = new Color(0.16f, 0.62f, 0.78f, 0.62f);
    [ColorUsage(true, true)] [SerializeField] private Color dimColor = new Color(0.7f, 0.95f, 1f, 0.26f);
    [ColorUsage(true, true)] [SerializeField] private Color warningColor = new Color(1f, 0.76f, 0.24f, 1f);
    [ColorUsage(true, true)] [SerializeField] private Color criticalColor = new Color(1f, 0.36f, 0.18f, 1f);
    [ColorUsage(true, true)] [SerializeField] private Color glassGlowColor = new Color(0.3f, 0.95f, 1f, 0.12f);

    [Header("Layout")]
    [SerializeField] private float lineThickness = 1.6f;
    [SerializeField] private float thinLine = 1f;
    [SerializeField] private float gaugeRadius = 42f;
    [SerializeField] private float gaugeThickness = 5f;
    [SerializeField] private float gaugeSpacing = 88f;
    [SerializeField] private float bottomOffset = 92f;
    [SerializeField] private float leftOffset = 82f;
    [SerializeField] private float rightOffset = 58f;

    [Header("Visor-Safe Area")]
    [SerializeField] private bool useVisorSafeArea = true;
    [SerializeField] [Range(0.05f, 0.35f)] private float visorSafeMarginX = 0.18f;
    [SerializeField] [Range(0.05f, 0.35f)] private float visorSafeMarginY = 0.15f;
    [SerializeField] [Range(0.5f, 1f)] private float visorSafeRadialLimit = 0.82f;

    [Header("Telemetry")]
    [SerializeField] private float coldSurfaceTemp = 13.5f;
    [SerializeField] private float abyssTemp = 2.4f;
    [SerializeField] private float abyssReferenceDepth = 1450f;
    [SerializeField] private float tempSmoothing = 4f;

    [Header("Presence")]
    [SerializeField] [Range(0f, 0.25f)] private float visorTintStrength = 0.08f;
    [SerializeField] [Range(0f, 0.35f)] private float edgeGlowStrength = 0.16f;
    [SerializeField] [Range(0f, 6f)] private float bootSequenceDuration = 2.8f;

    [Header("Diagnostics")]
    [SerializeField] private int _debugOxygenPct;
    [SerializeField] private int _debugPowerPct;
    [SerializeField] private int _debugHealthPct;
    [SerializeField] private int _debugDepthMeters;
    [SerializeField] private float _debugTemperature;
    [SerializeField] private bool _debugFlashlightOn;
    [SerializeField] private bool _debugPdaOpen;
    [SerializeField] private int _debugDrawCallCount;
    [SerializeField] private int _debugMatchedDrawCallCount;
    [SerializeField] private int _debugRejectedDrawCallCount;
    [SerializeField] private int _debugLastRenderedFrame;
    [SerializeField] private int _debugLastRenderCameraId;
    [SerializeField] private int _debugLastMatchedCameraId;
    [SerializeField] private bool _debugCameraMatched;
    [SerializeField] private bool _debugForceVisibilityProbe = true;
    [SerializeField] [Range(0.1f, 1f)] private float _debugProbeOpacity = 0.92f;

    private float _oxygenNorm = 1f;
    private float _powerNorm = 1f;
    private float _healthNorm = 1f;
    private float _depthMeters;
    private float _pressureAtm = 1f;
    private float _displayTemperature = 8f;
    private float _targetTemperature = 8f;
    private float _headingDegrees;
    private bool _flashlightOn;
    private float _flashlightHeat;
    private bool _flashlightCritical;
    private bool _pdaOpen;
    private float _pulseTimer;
    private SuitData _activeSuit;
    private SuitHUDProfile _activeHudProfile;
    private SuitHUDProfile.TelemetryFlags _telemetryFlags;
    private string _suitLabel = "STANDARD";
    private float _gaugeScaleResolved = 1f;
    private float _telemetryScaleResolved = 1f;
    private SuitHUDProfile.VisualStyle _visualStyleResolved;
    private float _depthTrendVelocity;
    private Color _basePrimaryColor;
    private Color _baseSecondaryColor;
    private Color _baseDimColor;
    private Color _baseWarningColor;
    private Color _baseCriticalColor;
    private Color _baseGlassGlowColor;
    private float _timeSinceEnable;
    private float _safeLeft;
    private float _safeRight;
    private float _safeBottom;
    private float _safeTop;
    private float _safeCenterX;
    private float _safeCenterY;
    private float _safeWidth;
    private float _safeHeight;
    private bool _registeredToTickManager;
    private float _previousDepthSample;
    private string _cachedHeadingText = "000";
    private string _cachedTemperatureText = "8.0 C";
    private string _cachedMassText = "0 kg";

    public static void CopyActiveHudsTo(List<HectonSuitHUD_v4> results)
    {
        if (results == null)
            return;

        results.Clear();
        for (int i = 0; i < s_activeHuds.Count; i++)
        {
            HectonSuitHUD_v4 hud = s_activeHuds[i];
            if (hud != null && hud.isActiveAndEnabled)
                results.Add(hud);
        }
    }

    public override void OnEnable()
    {
        base.OnEnable();
        RegisterActiveHud();

        CacheBasePalette();

        if (hudCamera == null)
            hudCamera = GetComponent<Camera>();
        if (hudFont == null)
            hudFont = TMP_Settings.defaultFontAsset;
        if (survival == null)
        {
            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransformRoot))
                survival = playerTransformRoot.GetComponent<HectonSurvivalSystem>();
        }
        if (playerMovement == null)
        {
            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransformRoot))
                playerMovement = playerTransformRoot.GetComponent<HectonPlayerMovement>();
        }
        if (flashlight == null)
        {
            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransformRoot))
                flashlight = playerTransformRoot.GetComponentInChildren<PlayerFlashlight>(true);
        }
        if (underwaterVisuals == null)
        {
            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransformRoot))
                underwaterVisuals = playerTransformRoot.GetComponentInChildren<HectonUnderwaterVisuals>(true);
        }

        Subscribe();
        ForceRefresh();
        _timeSinceEnable = 0f;

        if (Application.isPlaying)
            RegisterToTickManager();
    }

    public override void OnDisable()
    {
        if (Application.isPlaying)
            UnregisterFromTickManager();

        base.OnDisable();
        UnregisterActiveHud();
        Unsubscribe();
    }

    public void SetFallbackProfile(SuitHUDProfile profile)
    {
        if (ReferenceEquals(defaultHudProfile, profile))
            return;

        defaultHudProfile = profile;
        ResolveSuitVariant();
    }

    public void SetHudCamera(Camera camera)
    {
        if (hudCamera == camera || camera == null)
            return;

        hudCamera = camera;
    }

    public override void DrawShapes(Camera cam)
    {
        #pragma warning disable CS0618
        _debugLastRenderCameraId = cam != null ? cam.GetInstanceID() : 0;
        #pragma warning restore CS0618
        _debugCameraMatched = hudCamera != null && cam == hudCamera;

        _debugDrawCallCount++;

        if (hudCamera == null || cam != hudCamera)
        {
            _debugRejectedDrawCallCount++;
            return;
        }

        _debugMatchedDrawCallCount++;
        _debugLastRenderedFrame = Time.frameCount;
        _debugLastMatchedCameraId = _debugLastRenderCameraId;

        float w = hudCamera.pixelWidth;
        float h = hudCamera.pixelHeight;
        ComputeVisorSafeBounds(w, h);

        using (Draw.Command(hudCamera))
        {
            Draw.ResetAllDrawStates();
            Draw.BlendMode = ShapesBlendMode.Transparent;
            Draw.Matrix = Matrix4x4.Ortho(0f, w, 0f, h, -1f, 1f);
            Draw.Font = hudFont;

            if (_debugForceVisibilityProbe)
                DrawVisibilityProbe(w, h);

            DrawVisorEdgeGlow(w, h);
            DrawVisorFrame(w, h);
            DrawSuitHeader(w, h);
            DrawHeadingRibbon(w, h);
            DrawGaugeCluster(w, h);
            DrawTelemetryBlock(w, h);
            DrawCenterReticle(w, h);
            DrawStatusRibbon(w, h);
            DrawMicroStates(w, h);

            if (_timeSinceEnable < bootSequenceDuration)
                DrawBootSequence(w, h);
        }
    }

    public void Tick(float deltaTime)
    {
        UpdateHudRuntime(deltaTime);
    }

#if UNITY_EDITOR
    private void LateUpdate()
    {
        if (Application.isPlaying)
            return;

        UpdateHudRuntime(0.016f);
    }
#endif

    private void UpdateHudRuntime(float dt)
    {
        _pulseTimer += dt;
        _timeSinceEnable += dt;
        PollRuntimeData();
        _depthTrendVelocity = _depthMeters - _previousDepthSample;
        _previousDepthSample = _depthMeters;
        ResolveSuitVariant();
        UpdateTemperature(dt);
        RefreshCachedHudStrings();
        UpdateDiagnostics();
    }

    private void RegisterToTickManager()
    {
        if (_registeredToTickManager)
            return;

        GameTickManager tickManager = GameTickManager.Instance;
        if (tickManager == null)
            return;

        tickManager.Register(this);
        _registeredToTickManager = true;
    }

    private void UnregisterFromTickManager()
    {
        if (!_registeredToTickManager)
            return;

        GameTickManager tickManager = GameTickManager.Instance;
        if (tickManager != null)
            tickManager.Unregister(this);

        _registeredToTickManager = false;
    }

    private void RegisterActiveHud()
    {
        if (s_activeHuds.Contains(this))
            return;

        s_activeHuds.Add(this);
    }

    private void UnregisterActiveHud()
    {
        s_activeHuds.Remove(this);
    }

    private void PollRuntimeData()
    {
        if (survival == null)
        {
            if (playerMovement != null)
                _depthMeters = Mathf.Max(0f, playerMovement.CurrentDepth);

            _pressureAtm = 1f + (_depthMeters / 10f);
        }

        if (playerMovement != null)
            _headingDegrees = Mathf.Repeat(playerMovement.CameraYaw, 360f);
        else if (hudCamera != null)
            _headingDegrees = Mathf.Repeat(hudCamera.transform.eulerAngles.y, 360f);

        if (flashlight != null)
        {
            _flashlightHeat = Mathf.Clamp01(flashlight.HeatLevel);
            _flashlightCritical = flashlight.IsOverheated || flashlight.IsFlickering;
        }
    }

    private void UpdateTemperature(float dt)
    {
        float depth01 = Mathf.Clamp01(_depthMeters / Mathf.Max(abyssReferenceDepth, 1f));
        depth01 = depth01 * depth01 * (3f - 2f * depth01);

        float estimated = Mathf.Lerp(coldSurfaceTemp, abyssTemp, depth01);

        if (underwaterVisuals != null)
            estimated -= (1f - underwaterVisuals.CurrentLightFactor) * 0.8f;

        _targetTemperature = estimated;
        _displayTemperature = Mathf.Lerp(_displayTemperature, _targetTemperature, 1f - Mathf.Exp(-tempSmoothing * dt));
    }

    private void ComputeVisorSafeBounds(float w, float h)
    {
        if (!useVisorSafeArea)
        {
            _safeLeft = 0f;
            _safeRight = w;
            _safeBottom = 0f;
            _safeTop = h;
        }
        else
        {
            _safeLeft = w * visorSafeMarginX;
            _safeRight = w * (1f - visorSafeMarginX);
            _safeBottom = h * visorSafeMarginY;
            _safeTop = h * (1f - visorSafeMarginY);
        }

        float radialMarginX = w * Mathf.Clamp01((1f - visorSafeRadialLimit) * 0.5f);
        float radialMarginY = h * Mathf.Clamp01((1f - visorSafeRadialLimit) * 0.5f);
        _safeLeft = Mathf.Max(_safeLeft, radialMarginX);
        _safeRight = Mathf.Min(_safeRight, w - radialMarginX);
        _safeBottom = Mathf.Max(_safeBottom, radialMarginY);
        _safeTop = Mathf.Min(_safeTop, h - radialMarginY);

        _safeCenterX = (_safeLeft + _safeRight) * 0.5f;
        _safeCenterY = (_safeBottom + _safeTop) * 0.5f;
        _safeWidth = Mathf.Max(1f, _safeRight - _safeLeft);
        _safeHeight = Mathf.Max(1f, _safeTop - _safeBottom);
    }

    private void DrawGaugeCluster(float w, float h)
    {
        float safeOffsetX = Mathf.Min(leftOffset, _safeWidth * 0.14f);
        float safeOffsetY = Mathf.Min(bottomOffset, _safeHeight * 0.17f);
        Vector3 origin = new Vector3(_safeLeft + safeOffsetX, _safeBottom + safeOffsetY, 0f);
        float spacing = gaugeSpacing * _gaugeScaleResolved;
        float radius = gaugeRadius * _gaugeScaleResolved;
        int gaugeIndex = 0;

        if (HasTelemetry(SuitHUDProfile.TelemetryFlags.Oxygen))
            DrawGauge(origin + new Vector3(spacing * gaugeIndex++, 0f, 0f), "O2", "AIR", _oxygenNorm, _debugOxygenPct, PickVitalColor(_oxygenNorm), radius);

        if (HasTelemetry(SuitHUDProfile.TelemetryFlags.Power))
            DrawGauge(origin + new Vector3(spacing * gaugeIndex++, 0f, 0f), "PWR", "CELL", _powerNorm, _debugPowerPct, PickVitalColor(_powerNorm), radius);

        if (HasTelemetry(SuitHUDProfile.TelemetryFlags.Health))
            DrawGauge(origin + new Vector3(spacing * gaugeIndex++, 0f, 0f), "HLT", "BIO", _healthNorm, _debugHealthPct, PickVitalColor(_healthNorm), radius);

        if (gaugeIndex > 1)
        {
            Draw.Line(
                new Vector3(origin.x + radius * 1.25f, origin.y - 10f, 0f),
                new Vector3(origin.x + spacing * (gaugeIndex - 0.15f), origin.y - 10f, 0f),
                thinLine,
                ScaleAlpha(secondaryColor, 0.5f));
        }
    }

    private void DrawGauge(Vector3 center, string topLabel, string smallCode, float norm, int percent, Color accent, float radius)
    {
        float thickness = gaugeThickness * Mathf.Lerp(0.9f, 1.15f, _gaugeScaleResolved - 0.75f);
        DrawGaugeHalo(center, radius);
        DrawGaugeTicks(center, radius);

        Draw.Arc(center, radius, thickness,
            0f, Mathf.PI * 2f, ScaleAlpha(Color.white, 0.05f));

        Draw.Arc(center, radius, thickness * 0.6f,
            Mathf.PI * 0.5f, Mathf.PI * 0.5f - Mathf.PI * 2f, ScaleAlpha(secondaryColor, 0.45f));

        float angle = Mathf.Clamp01(norm) * Mathf.PI * 2f;
        if (angle > 0.01f)
        {
            Draw.Arc(center, radius, thickness,
                Mathf.PI * 0.5f, Mathf.PI * 0.5f - angle, accent);
        }

        Draw.Disc(center, radius - 9f, ScaleAlpha(primaryColor, 0.06f));
        Draw.Disc(center + new Vector3(0f, 16f, 0f), 1.8f, ScaleAlpha(primaryColor, 0.8f));

        DrawTextCentered(topLabel, center + new Vector3(0f, radius + 14f, 0f), 14f, ScaleAlpha(primaryColor, 0.88f));
        DrawTextCentered(smallCode, center + new Vector3(0f, 10f, 0f), 10f, ScaleAlpha(primaryColor, 0.92f));
        DrawTextCentered(PercentStrings[Mathf.Clamp(percent, 0, MaxPercent)], center + new Vector3(0f, -12f, 0f), 16f, accent);
    }

    private void DrawTelemetryBlock(float w, float h)
    {
        float safeRightInset = Mathf.Min(rightOffset, _safeWidth * 0.1f);
        float safeBottomInset = Mathf.Min(bottomOffset, _safeHeight * 0.14f);
        float blockRight = _safeRight - safeRightInset;
        float blockBaseY = _safeBottom + safeBottomInset;
        float rowStep = 24f * _telemetryScaleResolved;
        float pulse = (Mathf.Sin(_pulseTimer * 2.3f) + 1f) * 0.5f;
        Color telemetryColor = Color.Lerp(primaryColor, warningColor, Mathf.Clamp01((1f - _powerNorm) * 0.65f));
        telemetryColor = Color.Lerp(telemetryColor, criticalColor, _healthNorm < 0.18f ? pulse * 0.45f : 0f);

        float panelWidth = HasTelemetry(SuitHUDProfile.TelemetryFlags.Mass) ? 214f : 180f;
        DrawTelemetryGlow(blockRight - 90f, blockBaseY + 28f);
        DrawCornerBracket(blockRight - panelWidth - 20f, blockBaseY + 18f, panelWidth, 64f, ScaleAlpha(secondaryColor, 0.5f));
        Draw.Line(new Vector3(blockRight - panelWidth - 6f, blockBaseY + 48f, 0f), new Vector3(blockRight - 10f, blockBaseY + 48f, 0f), thinLine, ScaleAlpha(dimColor, 0.12f));

        int rowIndex = 0;
        if (HasTelemetry(SuitHUDProfile.TelemetryFlags.Depth))
        {
            DrawTelemetryRow("DEPTH", DepthStrings[Mathf.Clamp(Mathf.RoundToInt(_depthMeters), 0, MaxDepth)],
                blockRight, blockBaseY + 40f - (rowStep * rowIndex++), 16f, 22f, telemetryColor);
        }

        if (HasTelemetry(SuitHUDProfile.TelemetryFlags.Pressure))
        {
            DrawTelemetryRow("PRESSURE", PressureStrings[Mathf.Clamp(Mathf.RoundToInt(_pressureAtm), 0, MaxPressure)],
                blockRight, blockBaseY + 40f - (rowStep * rowIndex++), 11f, 16f, ScaleAlpha(primaryColor, 0.95f));
        }

        if (HasTelemetry(SuitHUDProfile.TelemetryFlags.Temperature))
        {
            DrawTelemetryRow("TEMPERATURE", _cachedTemperatureText,
                blockRight, blockBaseY + 40f - (rowStep * rowIndex++), 11f, 16f, ScaleAlpha(primaryColor, 0.95f));
        }

        if (HasTelemetry(SuitHUDProfile.TelemetryFlags.Mass) && _activeSuit != null)
        {
            DrawTelemetryRow("SUIT MASS", _cachedMassText,
                blockRight, blockBaseY + 40f - (rowStep * rowIndex++), 11f, 16f, ScaleAlpha(primaryColor, 0.95f));
        }

        if (HasTelemetry(SuitHUDProfile.TelemetryFlags.DepthTrend))
        {
            string trend = ResolveDepthTrendLabel();
            DrawTelemetryRow("VERTICAL", trend,
                blockRight, blockBaseY + 40f - (rowStep * rowIndex++), 11f, 14f, ScaleAlpha(dimColor, 0.92f));
        }
    }

    private void DrawVisorFrame(float w, float h)
    {
        switch (_visualStyleResolved)
        {
            case SuitHUDProfile.VisualStyle.AtlasIndustrial:
                DrawIndustrialVisorFrame(w, h);
                return;
            case SuitHUDProfile.VisualStyle.Expedition:
                DrawExpeditionVisorFrame(w, h);
                return;
            default:
                DrawCyanVisorFrame(w, h);
                return;
        }
    }

    private void DrawCyanVisorFrame(float w, float h)
    {
        float topY = _safeTop;
        float left = _safeLeft;
        float right = _safeRight;
        float cornerSize = 48f;

        Draw.Line(new Vector3(left, topY, 0f), new Vector3(left + cornerSize, topY, 0f), lineThickness, ScaleAlpha(glassGlowColor, 0.9f));
        Draw.Line(new Vector3(left, topY, 0f), new Vector3(left, topY - cornerSize, 0f), lineThickness, ScaleAlpha(glassGlowColor, 0.6f));

        Draw.Line(new Vector3(right - cornerSize, topY, 0f), new Vector3(right, topY, 0f), lineThickness, ScaleAlpha(glassGlowColor, 0.9f));
        Draw.Line(new Vector3(right, topY, 0f), new Vector3(right, topY - cornerSize, 0f), lineThickness, ScaleAlpha(glassGlowColor, 0.6f));

        Draw.Line(new Vector3(_safeCenterX - 22f, _safeTop - 16f, 0f), new Vector3(_safeCenterX + 22f, _safeTop - 16f, 0f), thinLine, ScaleAlpha(secondaryColor, 0.4f));
        Draw.Line(new Vector3(_safeCenterX, _safeTop - 26f, 0f), new Vector3(_safeCenterX, _safeTop - 8f, 0f), thinLine, ScaleAlpha(secondaryColor, 0.28f));
    }

    private void DrawExpeditionVisorFrame(float w, float h)
    {
        DrawCyanVisorFrame(w, h);

        float y = _safeBottom + 14f;
        Draw.Line(new Vector3(_safeCenterX - 120f, y, 0f), new Vector3(_safeCenterX - 52f, y, 0f), thinLine, ScaleAlpha(dimColor, 0.18f));
        Draw.Line(new Vector3(_safeCenterX + 52f, y, 0f), new Vector3(_safeCenterX + 120f, y, 0f), thinLine, ScaleAlpha(dimColor, 0.18f));
        Draw.Disc(new Vector3(_safeCenterX, y, 0f), 2f, ScaleAlpha(primaryColor, 0.7f));
    }

    private void DrawIndustrialVisorFrame(float w, float h)
    {
        float topY = _safeTop;
        float left = _safeLeft;
        float right = _safeRight;
        float railDepth = 66f;

        Draw.Line(new Vector3(left, topY, 0f), new Vector3(right, topY, 0f), lineThickness * 1.2f, ScaleAlpha(glassGlowColor, 0.75f));
        Draw.Line(new Vector3(left, topY, 0f), new Vector3(left, topY - railDepth, 0f), lineThickness * 1.2f, ScaleAlpha(glassGlowColor, 0.55f));
        Draw.Line(new Vector3(right, topY, 0f), new Vector3(right, topY - railDepth, 0f), lineThickness * 1.2f, ScaleAlpha(glassGlowColor, 0.55f));

        DrawCornerBracket(left + 10f, _safeBottom + 12f, 80f, 38f, ScaleAlpha(secondaryColor, 0.32f));
        DrawCornerBracket(right - 90f, _safeBottom + 12f, 80f, 38f, ScaleAlpha(secondaryColor, 0.32f));

        Draw.Line(new Vector3(_safeCenterX - 36f, _safeTop - 18f, 0f), new Vector3(_safeCenterX + 36f, _safeTop - 18f, 0f), thinLine, ScaleAlpha(secondaryColor, 0.42f));
        Draw.Line(new Vector3(_safeCenterX, _safeTop - 30f, 0f), new Vector3(_safeCenterX, _safeTop, 0f), thinLine, ScaleAlpha(secondaryColor, 0.32f));
    }

    private void DrawVisorEdgeGlow(float w, float h)
    {
        float glowAlpha = visorTintStrength * 0.4f;

        Draw.Line(
            new Vector3(_safeLeft + 20f, _safeTop - 2f, 0f),
            new Vector3(_safeRight - 20f, _safeTop - 2f, 0f),
            2.5f,
            ScaleAlpha(glassGlowColor, glowAlpha));

        Draw.Line(
            new Vector3(_safeLeft + 40f, _safeBottom + 2f, 0f),
            new Vector3(_safeRight - 40f, _safeBottom + 2f, 0f),
            2.5f,
            ScaleAlpha(glassGlowColor, glowAlpha * 0.7f));

        Draw.Line(
            new Vector3(_safeLeft + 2f, _safeBottom + 40f, 0f),
            new Vector3(_safeLeft + 2f, _safeTop - 40f, 0f),
            1.5f,
            ScaleAlpha(dimColor, edgeGlowStrength * 0.15f));

        Draw.Line(
            new Vector3(_safeRight - 2f, _safeBottom + 40f, 0f),
            new Vector3(_safeRight - 2f, _safeTop - 40f, 0f),
            1.5f,
            ScaleAlpha(dimColor, edgeGlowStrength * 0.15f));

        float dotAlpha = glowAlpha * 1.2f;
        Draw.Disc(new Vector3(_safeLeft + 8f, _safeTop - 8f, 0f), 2.2f, ScaleAlpha(primaryColor, dotAlpha));
        Draw.Disc(new Vector3(_safeRight - 8f, _safeTop - 8f, 0f), 2.2f, ScaleAlpha(primaryColor, dotAlpha));
        Draw.Disc(new Vector3(_safeLeft + 8f, _safeBottom + 8f, 0f), 2.2f, ScaleAlpha(primaryColor, dotAlpha * 0.6f));
        Draw.Disc(new Vector3(_safeRight - 8f, _safeBottom + 8f, 0f), 2.2f, ScaleAlpha(primaryColor, dotAlpha * 0.6f));
    }

    private void DrawBootSequence(float w, float h)
    {
        float t = Mathf.Clamp01(_timeSinceEnable / Mathf.Max(bootSequenceDuration, 0.001f));
        float fade = 1f - t;
        float pulse = 0.6f + 0.4f * Mathf.Sin(_pulseTimer * 7.5f);
        float centerX = _safeCenterX;
        float centerY = _safeCenterY + 24f;
        Color accent = ScaleAlpha(primaryColor, fade * pulse * 0.95f);
        Color dim = ScaleAlpha(dimColor, fade * 0.8f);

        Draw.Line(new Vector3(centerX - 72f, centerY, 0f), new Vector3(centerX - 18f, centerY, 0f), lineThickness, accent);
        Draw.Line(new Vector3(centerX + 18f, centerY, 0f), new Vector3(centerX + 72f, centerY, 0f), lineThickness, accent);
        Draw.Line(new Vector3(centerX, centerY - 38f, 0f), new Vector3(centerX, centerY - 10f, 0f), lineThickness, accent);
        Draw.Line(new Vector3(centerX, centerY + 10f, 0f), new Vector3(centerX, centerY + 38f, 0f), lineThickness, accent);
        Draw.Disc(new Vector3(centerX, centerY, 0f), 2.4f, accent);

        DrawTextCentered("SUIT LINK", new Vector3(centerX, centerY - 64f, 0f), 12f, accent);
        DrawTextCentered("VISOR CALIBRATING", new Vector3(centerX, centerY - 82f, 0f), 9f, dim);
    }

    private void DrawVisibilityProbe(float w, float h)
    {
        float pulse = 0.72f + 0.28f * (Mathf.Sin(_pulseTimer * 4.8f) * 0.5f + 0.5f);
        Color fill = ScaleAlpha(new Color(0.03f, 0.92f, 0.86f, 1f), _debugProbeOpacity * 0.12f * pulse);
        Color edge = ScaleAlpha(new Color(0.66f, 1f, 0.96f, 1f), _debugProbeOpacity * 0.7f);
        Color hot = ScaleAlpha(new Color(1f, 0.92f, 0.42f, 1f), _debugProbeOpacity * 0.95f);

        Draw.Rectangle(new Rect(_safeLeft, _safeBottom, _safeWidth, _safeHeight), fill);
        Draw.RectangleBorder(new Rect(_safeLeft, _safeBottom, _safeWidth, _safeHeight), 3f, edge);

        Draw.Rectangle(new Rect(_safeCenterX - 120f, _safeCenterY - 20f, 240f, 40f), ScaleAlpha(new Color(0f, 0.08f, 0.1f, 1f), _debugProbeOpacity * 0.85f));
        Draw.RectangleBorder(new Rect(_safeCenterX - 120f, _safeCenterY - 20f, 240f, 40f), 2f, hot);
        DrawTextCentered("V4 VISOR PROBE", new Vector3(_safeCenterX, _safeCenterY + 2f, 0f), 15f, hot);
        DrawTextCentered("PROJECTED PATH ACTIVE", new Vector3(_safeCenterX, _safeCenterY - 14f, 0f), 9f, edge);
    }

    private void DrawSuitHeader(float w, float h)
    {
        if (!HasTelemetry(SuitHUDProfile.TelemetryFlags.SuitLabel))
            return;

        float x = _safeLeft + 12f;
        float y = _safeTop - 18f;
        DrawText(_suitLabel, new Vector3(x, y, 0f), 10f, ScaleAlpha(dimColor, 0.92f));
        Draw.Line(new Vector3(x, y - 8f, 0f), new Vector3(x + 94f, y - 8f, 0f), thinLine, ScaleAlpha(secondaryColor, 0.35f));
    }

    private void DrawHeadingRibbon(float w, float h)
    {
        if (!HasTelemetry(SuitHUDProfile.TelemetryFlags.Heading))
            return;

        float y = _safeTop - 36f;
        string cardinal = ResolveCardinal(_headingDegrees);

        DrawTextCentered(cardinal, new Vector3(_safeCenterX, y + 8f, 0f), 10f, ScaleAlpha(dimColor, 0.9f));
        DrawTextCentered(_cachedHeadingText, new Vector3(_safeCenterX, y - 8f, 0f), 13f, ScaleAlpha(primaryColor, 0.95f));
        Draw.Line(new Vector3(_safeCenterX - 48f, y - 18f, 0f), new Vector3(_safeCenterX + 48f, y - 18f, 0f), thinLine, ScaleAlpha(secondaryColor, 0.28f));
        Draw.Line(new Vector3(_safeCenterX - 4f, y - 18f, 0f), new Vector3(_safeCenterX + 4f, y - 18f, 0f), lineThickness, ScaleAlpha(primaryColor, 0.42f));
    }

    private void DrawCenterReticle(float w, float h)
    {
        float x = _safeCenterX;
        float y = _safeCenterY * 0.97f;
        float arm = _visualStyleResolved == SuitHUDProfile.VisualStyle.AtlasIndustrial ? 20f : 14f;
        float gap = 8f;

        Draw.Disc(new Vector3(x, y, 0f), 1.4f, ScaleAlpha(primaryColor, 0.82f));
        Draw.Line(new Vector3(x - arm, y, 0f), new Vector3(x - gap, y, 0f), thinLine, ScaleAlpha(dimColor, 0.18f));
        Draw.Line(new Vector3(x + gap, y, 0f), new Vector3(x + arm, y, 0f), thinLine, ScaleAlpha(dimColor, 0.18f));
        Draw.Line(new Vector3(x, y - arm, 0f), new Vector3(x, y - gap, 0f), thinLine, ScaleAlpha(dimColor, 0.18f));
        Draw.Line(new Vector3(x, y + gap, 0f), new Vector3(x, y + arm, 0f), thinLine, ScaleAlpha(dimColor, 0.18f));
    }

    private void DrawStatusRibbon(float w, float h)
    {
        float x = _safeCenterX;
        float y = _safeBottom + 42f;
        string text = ResolveStatusRibbonText();
        Color color = ResolveStatusRibbonColor();

        Draw.Line(new Vector3(x - 116f, y - 6f, 0f), new Vector3(x - 34f, y - 6f, 0f), thinLine, ScaleAlpha(secondaryColor, 0.16f));
        Draw.Line(new Vector3(x + 34f, y - 6f, 0f), new Vector3(x + 116f, y - 6f, 0f), thinLine, ScaleAlpha(secondaryColor, 0.16f));
        DrawTextCentered(text, new Vector3(x, y, 0f), 9f, color);
    }

    private void DrawMicroStates(float w, float h)
    {
        bool showLamp = HasTelemetry(SuitHUDProfile.TelemetryFlags.FlashlightState);
        bool showPda = HasTelemetry(SuitHUDProfile.TelemetryFlags.PdaState);
        if (!showLamp && !showPda)
            return;

        float x = _safeCenterX;
        float y = _safeBottom + 18f;

        string leftState = _flashlightOn ? "LAMP ONLINE" : "LAMP STANDBY";
        string rightState = _pdaOpen ? "PDA ACTIVE" : "SUIT LINK STABLE";

        Color leftColor = _flashlightCritical ? Color.Lerp(warningColor, criticalColor, (Mathf.Sin(_pulseTimer * 14f) + 1f) * 0.5f) : ScaleAlpha(dimColor, 0.86f);
        Color rightColor = _pdaOpen ? ScaleAlpha(primaryColor, 0.94f) : ScaleAlpha(dimColor, 0.72f);

        if (showLamp)
            DrawTextCentered(leftState, new Vector3(x - 108f, y, 0f), 9f, leftColor);
        if (showPda)
            DrawTextCentered(rightState, new Vector3(x + 108f, y, 0f), 9f, rightColor);
        Draw.Line(new Vector3(x - 36f, y + 2f, 0f), new Vector3(x + 36f, y + 2f, 0f), thinLine, ScaleAlpha(secondaryColor, 0.25f));
    }

    private void DrawTelemetryRow(string label, string value, float blockRight, float y, float labelSize, float valueSize, Color valueColor)
    {
        DrawText(label, new Vector3(blockRight - 168f, y, 0f), labelSize, ScaleAlpha(dimColor, 0.9f));
        DrawText(value, new Vector3(blockRight - 8f, y, 0f), valueSize, valueColor);
    }

    private void DrawGaugeHalo(Vector3 center, float radius)
    {
        Draw.Disc(center, radius + 10f, ScaleAlpha(glassGlowColor, 0.075f));
    }

    private void DrawGaugeTicks(Vector3 center, float radius)
    {
        for (int i = 0; i < 12; i++)
        {
            float angle = Mathf.PI * 0.5f - ((Mathf.PI * 2f) / 12f) * i;
            Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Vector3 start = center + dir * (radius + 4f);
            Vector3 end = center + dir * (radius + ((i % 3 == 0) ? 10f : 7f));
            Draw.Line(start, end, thinLine, ScaleAlpha(dimColor, i % 3 == 0 ? 0.35f : 0.18f));
        }
    }

    private void DrawTelemetryGlow(float x, float y)
    {
        Draw.Disc(new Vector3(x, y, 0f), 74f, ScaleAlpha(glassGlowColor, 0.08f));
    }

    private void DrawCornerBracket(float x, float y, float width, float height, Color color)
    {
        float arm = 18f;
        float left = x;
        float right = x + width;
        float top = y + height;
        float bottom = y;

        Draw.Line(new Vector3(left, top, 0f), new Vector3(left + arm, top, 0f), thinLine, color);
        Draw.Line(new Vector3(left, top, 0f), new Vector3(left, top - arm, 0f), thinLine, color);

        Draw.Line(new Vector3(right - arm, top, 0f), new Vector3(right, top, 0f), thinLine, color);
        Draw.Line(new Vector3(right, top, 0f), new Vector3(right, top - arm, 0f), thinLine, color);

        Draw.Line(new Vector3(left, bottom + arm, 0f), new Vector3(left, bottom, 0f), thinLine, color);
        Draw.Line(new Vector3(left, bottom, 0f), new Vector3(left + arm, bottom, 0f), thinLine, color);
    }

    private Color PickVitalColor(float norm)
    {
        if (norm <= 0.15f)
            return Color.Lerp(criticalColor, warningColor, (Mathf.Sin(_pulseTimer * 10f) + 1f) * 0.5f);
        if (norm <= 0.35f)
            return warningColor;
        return primaryColor;
    }

    private void ResolveSuitVariant()
    {
        SuitData suit = playerMovement != null ? playerMovement.CurrentSuit : null;
        SuitHUDProfile overrideProfile = PlayerExpressionManager.ActiveHudProfileOverride;
        SuitHUDProfile profile = overrideProfile != null
            ? overrideProfile
            : (suit != null && suit.HudProfile != null ? suit.HudProfile : defaultHudProfile);
        if (ReferenceEquals(_activeSuit, suit) && ReferenceEquals(_activeHudProfile, profile))
            return;

        _activeSuit = suit;
        _activeHudProfile = profile;
        _gaugeScaleResolved = profile != null ? profile.GaugeScale : 1f;
        _telemetryScaleResolved = profile != null ? profile.TelemetryScale : 1f;
        _visualStyleResolved = profile != null ? profile.Style : SuitHUDProfile.VisualStyle.CyanVisor;

        if (profile != null)
        {
            _telemetryFlags = profile.VisibleTelemetry;
            string runtimeLabelOverride = PlayerExpressionManager.ActiveSuitLabelOverride;
            _suitLabel = !string.IsNullOrWhiteSpace(runtimeLabelOverride)
                ? runtimeLabelOverride.ToUpperInvariant()
                : (string.IsNullOrWhiteSpace(profile.DisplayNameOverride)
                    ? SanitizeSuitName(suit != null ? suit.name : "STANDARD")
                    : profile.DisplayNameOverride.ToUpperInvariant());
            ApplyProfilePalette(profile);
            return;
        }

        RestoreBasePalette();
        _telemetryFlags =
            SuitHUDProfile.TelemetryFlags.Oxygen |
            SuitHUDProfile.TelemetryFlags.Power |
            SuitHUDProfile.TelemetryFlags.Health |
            SuitHUDProfile.TelemetryFlags.Depth |
            SuitHUDProfile.TelemetryFlags.Temperature |
            SuitHUDProfile.TelemetryFlags.Heading |
            SuitHUDProfile.TelemetryFlags.FlashlightState |
            SuitHUDProfile.TelemetryFlags.PdaState |
            SuitHUDProfile.TelemetryFlags.SuitLabel |
            SuitHUDProfile.TelemetryFlags.DepthTrend;

        if (suit != null)
        {
            _suitLabel = SanitizeSuitName(suit.name);

            if (suit.mass >= 220f || suit.maxSwimSpeed <= 6.5f)
            {
                _telemetryFlags |= SuitHUDProfile.TelemetryFlags.Pressure;
                _telemetryFlags |= SuitHUDProfile.TelemetryFlags.Mass;
                _visualStyleResolved = SuitHUDProfile.VisualStyle.AtlasIndustrial;
                _telemetryScaleResolved = 1.05f;
            }
            else if (suit.maxSwimSpeed >= 8.5f)
            {
                _visualStyleResolved = SuitHUDProfile.VisualStyle.Expedition;
            }
        }
        else
        {
            _suitLabel = "STANDARD";
        }

        ApplyStylePalette(_visualStyleResolved);
    }

    private bool HasTelemetry(SuitHUDProfile.TelemetryFlags flag)
    {
        return (_telemetryFlags & flag) != 0;
    }

    private static string SanitizeSuitName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return "STANDARD";

        return rawName.Replace('_', ' ').ToUpperInvariant();
    }

    private static string ResolveCardinal(float heading)
    {
        if (heading >= 315f || heading < 45f)
            return "N";
        if (heading < 135f)
            return "E";
        if (heading < 225f)
            return "S";
        return "W";
    }

    private string ResolveDepthTrendLabel()
    {
        if (_depthTrendVelocity > 0.04f)
            return "DESCENDING";
        if (_depthTrendVelocity < -0.04f)
            return "ASCENDING";
        return "STABLE";
    }

    private string ResolveStatusRibbonText()
    {
        if (_healthNorm <= 0.18f)
            return "HULL INTEGRITY CRITICAL";
        if (_oxygenNorm <= 0.2f)
            return "OXYGEN RESERVE LOW";
        if (_powerNorm <= 0.2f)
            return "POWER CELLS BELOW SAFE MARGIN";
        if (_flashlightCritical)
            return "LAMP THERMAL LIMIT";
        if (_pdaOpen)
            return "SUIT LINK ROUTING PDA";
        return _visualStyleResolved == SuitHUDProfile.VisualStyle.AtlasIndustrial
            ? "ATLAS SYSTEMS NOMINAL"
            : "LIFE SUPPORT STABLE";
    }

    private Color ResolveStatusRibbonColor()
    {
        if (_healthNorm <= 0.18f)
            return ScaleAlpha(criticalColor, 0.96f);
        if (_oxygenNorm <= 0.2f || _powerNorm <= 0.2f || _flashlightCritical)
            return ScaleAlpha(warningColor, 0.94f);
        return ScaleAlpha(dimColor, 0.86f);
    }

    private void CacheBasePalette()
    {
        _basePrimaryColor = primaryColor;
        _baseSecondaryColor = secondaryColor;
        _baseDimColor = dimColor;
        _baseWarningColor = warningColor;
        _baseCriticalColor = criticalColor;
        _baseGlassGlowColor = glassGlowColor;
    }

    private void RestoreBasePalette()
    {
        primaryColor = _basePrimaryColor;
        secondaryColor = _baseSecondaryColor;
        dimColor = _baseDimColor;
        warningColor = _baseWarningColor;
        criticalColor = _baseCriticalColor;
        glassGlowColor = _baseGlassGlowColor;
    }

    private void ApplyProfilePalette(SuitHUDProfile profile)
    {
        if (profile.OverridePalette)
        {
            primaryColor = profile.PrimaryColor;
            secondaryColor = profile.SecondaryColor;
            dimColor = profile.DimColor;
            warningColor = profile.WarningColor;
            criticalColor = profile.CriticalColor;
            glassGlowColor = profile.GlassGlowColor;
            return;
        }

        ApplyStylePalette(profile.Style);
    }

    private void ApplyStylePalette(SuitHUDProfile.VisualStyle style)
    {
        RestoreBasePalette();

        switch (style)
        {
            case SuitHUDProfile.VisualStyle.Expedition:
                primaryColor = new Color(0.48f, 1f, 0.94f, 1f);
                secondaryColor = new Color(0.18f, 0.74f, 0.66f, 0.62f);
                dimColor = new Color(0.74f, 1f, 0.94f, 0.28f);
                glassGlowColor = new Color(0.26f, 1f, 0.84f, 0.14f);
                break;

            case SuitHUDProfile.VisualStyle.AtlasIndustrial:
                primaryColor = new Color(0.62f, 0.94f, 1f, 1f);
                secondaryColor = new Color(0.34f, 0.62f, 0.78f, 0.78f);
                dimColor = new Color(0.86f, 0.95f, 1f, 0.32f);
                warningColor = new Color(1f, 0.68f, 0.22f, 1f);
                glassGlowColor = new Color(0.42f, 0.78f, 1f, 0.16f);
                break;
        }
    }

    private void Subscribe()
    {
        if (survival != null)
        {
            survival.OnOxygenChanged += HandleOxygenChanged;
            survival.OnEnergyChanged += HandleEnergyChanged;
            survival.OnIntegrityChanged += HandleIntegrityChanged;
            survival.OnDepthChanged += HandleDepthChanged;
            survival.OnPressureChanged += HandlePressureChanged;
        }

        FlashlightEvents.OnToggled += HandleFlashlightToggled;
        PDAEvents.OnOpened += HandlePdaOpened;
        PDAEvents.OnClosed += HandlePdaClosed;
    }

    private void Unsubscribe()
    {
        if (survival != null)
        {
            survival.OnOxygenChanged -= HandleOxygenChanged;
            survival.OnEnergyChanged -= HandleEnergyChanged;
            survival.OnIntegrityChanged -= HandleIntegrityChanged;
            survival.OnDepthChanged -= HandleDepthChanged;
            survival.OnPressureChanged -= HandlePressureChanged;
        }

        FlashlightEvents.OnToggled -= HandleFlashlightToggled;
        PDAEvents.OnOpened -= HandlePdaOpened;
        PDAEvents.OnClosed -= HandlePdaClosed;
    }

    private void ForceRefresh()
    {
        if (survival != null)
        {
            HandleOxygenChanged(survival.Oxygen);
            HandleEnergyChanged(survival.Energy);
            HandleIntegrityChanged(survival.Integrity);
            HandleDepthChanged(survival.Depth);
            HandlePressureChanged(survival.Pressure);
        }
        else
        {
            _pressureAtm = 1f + (_depthMeters / 10f);
        }

        if (flashlight != null)
        {
            _flashlightOn = flashlight.IsOn;
            _flashlightHeat = flashlight.HeatLevel;
            _flashlightCritical = flashlight.IsOverheated || flashlight.IsFlickering;
        }

        _pdaOpen = PlayerPDA.IsOpen;
        PollRuntimeData();
        _previousDepthSample = _depthMeters;
        ResolveSuitVariant();
        UpdateTemperature(0f);
        RefreshCachedHudStrings();
    }

    private void HandleOxygenChanged(float _) => _oxygenNorm = survival != null ? Mathf.Clamp01(survival.OxygenNormalized) : _oxygenNorm;
    private void HandleEnergyChanged(float _) => _powerNorm = survival != null ? Mathf.Clamp01(survival.EnergyNormalized) : _powerNorm;
    private void HandleIntegrityChanged(float _) => _healthNorm = survival != null ? Mathf.Clamp01(survival.IntegrityNormalized) : _healthNorm;
    private void HandleDepthChanged(float value) => _depthMeters = Mathf.Max(0f, value);
    private void HandlePressureChanged(float value) => _pressureAtm = Mathf.Max(1f, value);
    private void HandleFlashlightToggled(bool isOn) => _flashlightOn = isOn;
    private void HandlePdaOpened(int _) => _pdaOpen = true;
    private void HandlePdaClosed(float _) => _pdaOpen = false;

    private void UpdateDiagnostics()
    {
        _debugOxygenPct = Mathf.RoundToInt(_oxygenNorm * 100f);
        _debugPowerPct = Mathf.RoundToInt(_powerNorm * 100f);
        _debugHealthPct = Mathf.RoundToInt(_healthNorm * 100f);
        _debugDepthMeters = Mathf.RoundToInt(_depthMeters);
        _debugTemperature = _displayTemperature;
        _debugFlashlightOn = _flashlightOn;
        _debugPdaOpen = _pdaOpen;
    }

    private void RefreshCachedHudStrings()
    {
        int heading = Mathf.Clamp(Mathf.RoundToInt(_headingDegrees), 0, MaxHeading);
        _cachedHeadingText = HeadingStrings[heading];

        int temperatureDeci = Mathf.Clamp(Mathf.RoundToInt(_displayTemperature * 10f), MinTemperatureDeci, MaxTemperatureDeci);
        _cachedTemperatureText = TemperatureStrings[temperatureDeci - MinTemperatureDeci];

        int massKg = _activeSuit != null ? Mathf.Clamp(Mathf.RoundToInt(_activeSuit.mass), 0, MaxSuitMassKg) : 0;
        _cachedMassText = MassStrings[massKg];
    }

    private void DrawText(string text, Vector3 position, float size, Color color)
    {
        if (hudFont == null || string.IsNullOrEmpty(text))
            return;

        Draw.Text(position, Quaternion.identity, text, TextAlign.Left, size, hudFont, color);
    }

    private void DrawTextCentered(string text, Vector3 position, float size, Color color)
    {
        if (hudFont == null || string.IsNullOrEmpty(text))
            return;

        Draw.Text(position, Quaternion.identity, text, TextAlign.Center, size, hudFont, color);
    }

    private static Color ScaleAlpha(Color color, float alpha)
    {
        color.a *= alpha;
        color.a = Mathf.Clamp01(color.a);
        return color;
    }
}
