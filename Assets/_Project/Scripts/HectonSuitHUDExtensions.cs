using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.UI;
using UnityEngine;
using Shapes;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[ExecuteAlways]
[AddComponentMenu("Hecton8/HUD/Suit HUD Extensions")]
public sealed class HectonSuitHUDExtensions : ImmediateModeShapeDrawer, ITickable, IUpdatable, IGlobalRegistryHotSwapListener, IFlashlightEventListener, IPDAEventListener
{
    // COLD ALLOC: List<HectonSuitHUD_v4>[4] — legacy HUD resolver scratch — owner: HectonSuitHUDExtensions
    private static readonly List<HectonSuitHUD_v4> s_hudResolveBuffer = new List<HectonSuitHUD_v4>(4);

    private const float AutoResolveRetryInterval = 1f;

    [Header("Legacy Compatibility")]
    [SerializeField] private Camera hudCamera;
    [SerializeField] private PlayerFlashlight flashlight;
    [SerializeField] private PlayerToolManager toolManager;
    [SerializeField] private HectonSuitHUD_v4 primaryHud;
    [SerializeField] private SuitHUDV4CanvasOverlay canvasOverlay;

    [Header("Presentation Colors")]
    [SerializeField] private Color normalColor = new Color(0f, 0.9f, 1f, 0.8f);
    [SerializeField] private Color warningColor = new Color(1f, 0.8f, 0f, 0.8f);
    [SerializeField] private Color criticalColor = new Color(1f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color flashlightOnColor = new Color(0.9f, 0.9f, 1f, 1f);
    [SerializeField] private Color pdaActiveColor = new Color(0f, 1f, 0.5f, 1f);

    [Header("Layout Configuration")]
    [SerializeField] private float horizontalMargin = 0.05f;
    [SerializeField] private float verticalMargin = 0.05f;
    [SerializeField] private float panelSpacingY = 0.04f;
    [SerializeField] private float itemSpacingX = 0.012f;
    [SerializeField] private float iconRadius = 0.008f;
    [SerializeField] private float iconThickness = 0.0015f;
    [SerializeField] private float barWidth = 0.04f;
    [SerializeField] private float barHeight = 0.003f;
    [SerializeField] private float fontSize = 0.012f;

    [Header("Notifications Layout")]
    [SerializeField] private float notificationDuration = 3.0f;
    [SerializeField] private float notificationFadeSpeed = 4.0f;
    [SerializeField] private float notificationSpacingY = 0.02f;
    [SerializeField] private float notificationOffsetY = 0.05f;

    [Header("Fonts")]
    [SerializeField] private TMP_FontAsset hudFont;

    [Header("Diagnostics (Read-Only)")]
    [SerializeField] private bool _debugFlashlightOn;
    [SerializeField] private float _debugFlashlightHeat;
    [SerializeField] private bool _debugPDAOpen;
    [SerializeField] private int _debugNotificationCount;

    private float _nextAutoResolveAt;
    private bool _tickRegistered;
    private bool _hotSwapRegistered;
    private bool _referencesResolved;
    private Transform _cachedRoot;

    // Flashlight state (fallbacks if reference is null)
    private bool _flashlightOn;
    private float _flashlightBattery;
    private float _flashlightHeat;
    private bool _flashlightOverheated;
    private bool _flashlightFlickering;

    // PDA state
    private bool _pdaActive;

    // Notification Queue
    private struct NotificationEntry
    {
        public uint MessageHash;
        public string MessageText;
        public float TimeRemaining;
        public float Alpha;
        public Color TextColor;
    }

    private const int MaxNotifications = 5;
    private readonly NotificationEntry[] _notifications = new NotificationEntry[MaxNotifications];
    private bool _notificationsInitialized;

    public override void OnEnable()
    {
#if UNITY_EDITOR
        if (EditorApplication.isCompiling || !Application.isPlaying)
            return;
#endif

        base.OnEnable();
        InitNotificationQueue();
        AutoResolveReferencesCold();
        TryRegisterHotSwapListener();
        RegisterTick();

        FlashlightEvents.Register(this);
        PDAEvents.Register(this);
    }

    public override void OnDisable()
    {
        base.OnDisable();
        UnregisterTick();
        TryUnregisterHotSwapListener();

        FlashlightEvents.Unregister(this);
        PDAEvents.Unregister(this);
    }

    public void OnGlobalRegistryServiceReplaced(
        GlobalRegistryServiceSlot serviceSlot,
        object previousService,
        object currentService)
    {
        if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
            return;

        UnregisterTick();
        if (currentService != null && isActiveAndEnabled)
            RegisterTick();
    }

    public void Tick(float deltaTime)
    {
        RefreshReferencesFromRegistries();
        UpdateNotifications(deltaTime);
        UpdateDiagnostics();
    }

    private void InitNotificationQueue()
    {
        if (_notificationsInitialized)
            return;

        for (int i = 0; i < MaxNotifications; i++)
        {
            _notifications[i].MessageHash = 0;
            _notifications[i].MessageText = null;
            _notifications[i].TimeRemaining = 0f;
            _notifications[i].Alpha = 0f;
        }
        _notificationsInitialized = true;
    }

    private void UpdateNotifications(float deltaTime)
    {
        for (int i = 0; i < MaxNotifications; i++)
        {
            if (_notifications[i].TimeRemaining > 0f)
            {
                _notifications[i].TimeRemaining -= deltaTime;

                float elapsed = notificationDuration - _notifications[i].TimeRemaining;
                if (elapsed < 0.3f)
                {
                    _notifications[i].Alpha = Mathf.Min(1f, elapsed / 0.3f);
                }
                else if (_notifications[i].TimeRemaining < 0.5f)
                {
                    _notifications[i].Alpha = Mathf.Max(0f, _notifications[i].TimeRemaining / 0.5f);
                }
                else
                {
                    _notifications[i].Alpha = 1f;
                }

                if (_notifications[i].TimeRemaining <= 0f)
                {
                    _notifications[i].MessageHash = 0;
                    _notifications[i].MessageText = null;
                }
            }
        }
    }

    private void AddNotification(uint hash, string text, Color color)
    {
        InitNotificationQueue();

        // Check for duplicate
        for (int i = 0; i < MaxNotifications; i++)
        {
            if (_notifications[i].TimeRemaining > 0f && _notifications[i].MessageHash == hash)
            {
                _notifications[i].TimeRemaining = notificationDuration;
                _notifications[i].TextColor = color;
                return;
            }
        }

        // Find empty slot
        int targetSlot = -1;
        for (int i = 0; i < MaxNotifications; i++)
        {
            if (_notifications[i].TimeRemaining <= 0f)
            {
                targetSlot = i;
                break;
            }
        }

        // Overwrite oldest if full
        if (targetSlot == -1)
        {
            float minTime = float.MaxValue;
            for (int i = 0; i < MaxNotifications; i++)
            {
                if (_notifications[i].TimeRemaining < minTime)
                {
                    minTime = _notifications[i].TimeRemaining;
                    targetSlot = i;
                }
            }
        }

        if (targetSlot != -1)
        {
            _notifications[targetSlot].MessageHash = hash;
            _notifications[targetSlot].MessageText = text;
            _notifications[targetSlot].TimeRemaining = notificationDuration;
            _notifications[targetSlot].Alpha = 0f;
            _notifications[targetSlot].TextColor = color;
        }
    }

    private static uint GetMessageHash(string str)
    {
        if (str == null)
            return 0u;
        uint hash = 2166136261u;
        for (int i = 0; i < str.Length; i++)
        {
            hash ^= str[i];
            hash *= 16777619u;
        }
        return hash;
    }

    public void OnFlashlightEvent(in FlashlightEventPayload payload)
    {
        FlashlightEventType eventType = (FlashlightEventType)payload.EventType;
        _flashlightBattery = payload.BatteryPercent;
        _flashlightHeat = payload.Heat01;
        _flashlightOn = FlashlightEventPayload.IsOn(in payload);

        switch (eventType)
        {
            case FlashlightEventType.Toggled:
                break;
            case FlashlightEventType.BatteryDepleted:
                _flashlightOn = false;
                _flashlightOverheated = false;
                _flashlightFlickering = false;
                AddNotification(GetMessageHash("BATTERY DEPLETED"), "BATTERY DEPLETED", criticalColor);
                break;
            case FlashlightEventType.Overheat:
                _flashlightOn = false;
                _flashlightOverheated = true;
                _flashlightFlickering = false;
                AddNotification(GetMessageHash("FLASHLIGHT OVERHEAT"), "FLASHLIGHT OVERHEAT", criticalColor);
                break;
            case FlashlightEventType.FlickerStart:
                _flashlightFlickering = true;
                if (_flashlightBattery < 20f)
                {
                    AddNotification(GetMessageHash("FLASHLIGHT LOW BATTERY"), "FLASHLIGHT LOW BATTERY", warningColor);
                }
                break;
        }
    }

    public void OnPDAEvent(in PDAEventPayload payload)
    {
        PDAEventType eventType = (PDAEventType)payload.EventType;
        switch (eventType)
        {
            case PDAEventType.Opened:
                _pdaActive = true;
                break;
            case PDAEventType.Closed:
                _pdaActive = false;
                break;
            case PDAEventType.LowBatteryShutdown:
                _pdaActive = false;
                AddNotification(GetMessageHash("PDA LOW BATTERY"), "PDA LOW BATTERY", warningColor);
                break;
        }
    }

    private void UpdateDiagnostics()
    {
        // Pull live flashlight values if available to stay in perfect sync
        if (flashlight != null)
        {
            _flashlightOn = flashlight.IsOn;
            _flashlightHeat = flashlight.HeatLevel;
            _flashlightOverheated = flashlight.IsOverheated;
            _flashlightFlickering = flashlight.IsFlickering;
            _flashlightBattery = flashlight.EnergyPercent;
        }

        _debugFlashlightOn = _flashlightOn;
        _debugFlashlightHeat = _flashlightHeat;
        _debugPDAOpen = _pdaActive;

        int activeCount = 0;
        for (int i = 0; i < MaxNotifications; i++)
        {
            if (_notifications[i].TimeRemaining > 0f)
                activeCount++;
        }
        _debugNotificationCount = activeCount;
    }

    public override void DrawShapes(Camera cam)
    {
        if (cam != hudCamera)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            return;
#endif

        InitNotificationQueue();

        using (Draw.Command(cam))
        {
            Draw.Matrix = hudCamera.transform.localToWorldMatrix;

            float zDistance = 0.5f;
            float halfHeight;
            float halfWidth;

            if (hudCamera.orthographic)
            {
                halfHeight = hudCamera.orthographicSize;
                halfWidth = halfHeight * hudCamera.aspect;
            }
            else
            {
                float fovRad = hudCamera.fieldOfView * Mathf.Deg2Rad;
                halfHeight = Mathf.Tan(fovRad * 0.5f) * zDistance;
                halfWidth = halfHeight * hudCamera.aspect;
            }

            DrawEquipmentPanel(halfWidth, halfHeight, zDistance);
            DrawNotifications(halfHeight, zDistance);
        }
    }

    private void DrawEquipmentPanel(float halfWidth, float halfHeight, float zDistance)
    {
        float startX = halfWidth - horizontalMargin;
        float startY = halfHeight - verticalMargin;
        float cursorY = startY;

        DrawFlashlightIndicator(ref cursorY, startX, zDistance);
        cursorY -= panelSpacingY;
        DrawPDAIndicator(ref cursorY, startX, zDistance);
    }

    private void DrawFlashlightIndicator(ref float cursorY, float startX, float zDistance)
    {
        Vector3 iconPos = new Vector3(startX - iconRadius, cursorY, zDistance);
        Vector3 textPos = new Vector3(startX - iconRadius * 2f - itemSpacingX, cursorY, zDistance);

        float flicker = 1f;
        if (_flashlightFlickering || (_flashlightOverheated && _flashlightOn))
        {
            flicker = (Time.time * 30f) % 2 == 0 ? 1f : 0.2f;
        }

        Color activeNormal = normalColor;
        activeNormal.a *= flicker;
        Color activeOn = flashlightOnColor;
        activeOn.a *= flicker;

        // Draw outer ring
        Draw.Ring(iconPos, iconRadius, iconThickness, activeNormal);

        // Draw inner disc if on
        if (_flashlightOn)
        {
            Draw.Disc(iconPos, iconRadius * 0.6f, activeOn);
        }

        // Draw Label
        Draw.Text(null, textPos, "FLASHLIGHT", TextAlign.Right, fontSize, hudFont, _flashlightOn ? activeOn : activeNormal);

        // Draw Heat Bar (if on and heat > 0)
        if (_flashlightOn && _flashlightHeat > 0f)
        {
            float currentBarWidth = barWidth * _flashlightHeat;
            Vector3 barPos = new Vector3(startX - iconRadius * 2f - itemSpacingX - barWidth * 0.5f, cursorY - fontSize * 0.8f, zDistance);
            Vector3 filledBarPos = new Vector3(startX - iconRadius * 2f - itemSpacingX - barWidth + currentBarWidth * 0.5f, cursorY - fontSize * 0.8f, zDistance);

            // Draw track
            Color trackColor = normalColor;
            trackColor.a *= 0.15f * flicker;
            Draw.Rectangle(barPos, new Vector2(barWidth, barHeight), trackColor);

            // Draw filled heat
            Color heatColor = _flashlightHeat > 0.8f ? criticalColor : (_flashlightHeat > 0.5f ? warningColor : normalColor);
            heatColor.a *= flicker;
            Draw.Rectangle(filledBarPos, new Vector2(currentBarWidth, barHeight), heatColor);
        }

        // Draw Overheat label if overheated
        if (_flashlightOverheated)
        {
            Vector3 warningPos = new Vector3(startX - iconRadius * 2f - itemSpacingX, cursorY - fontSize * 1.5f, zDistance);
            Color warningTextColor = criticalColor;
            warningTextColor.a *= flicker;
            Draw.Text(null, warningPos, "OVERHEAT", TextAlign.Right, fontSize * 0.8f, hudFont, warningTextColor);
            cursorY -= fontSize * 1.0f; // Shift down slightly for warning
        }
    }

    private void DrawPDAIndicator(ref float cursorY, float startX, float zDistance)
    {
        Vector3 iconPos = new Vector3(startX - iconRadius, cursorY, zDistance);
        Vector3 textPos = new Vector3(startX - iconRadius * 2f - itemSpacingX, cursorY, zDistance);

        // Draw square icon
        Draw.Rectangle(iconPos, new Vector2(iconRadius * 2f, iconRadius * 2f), iconThickness, normalColor);

        if (_pdaActive)
        {
            Draw.Rectangle(iconPos, new Vector2(iconRadius * 1.2f, iconRadius * 1.2f), pdaActiveColor);
        }

        // Draw Label
        Draw.Text(null, textPos, _pdaActive ? "PDA [ACTIVE]" : "PDA", TextAlign.Right, fontSize, hudFont, _pdaActive ? pdaActiveColor : normalColor);
    }

    private void DrawNotifications(float halfHeight, float zDistance)
    {
        float startY = halfHeight - verticalMargin - notificationOffsetY;
        int drawnCount = 0;

        for (int i = 0; i < MaxNotifications; i++)
        {
            if (_notifications[i].TimeRemaining > 0f)
            {
                float currentY = startY - (drawnCount * notificationSpacingY);
                Vector3 pos = new Vector3(0f, currentY, zDistance);
                Color textColor = _notifications[i].TextColor;
                textColor.a *= _notifications[i].Alpha;

                Draw.Text(null, pos, _notifications[i].MessageText, TextAlign.Center, fontSize * 1.1f, hudFont, textColor);
                drawnCount++;
            }
        }
    }

    private void AutoResolveReferencesCold()
    {
        Transform preferredRoot = ResolveCachedRoot();

        if (primaryHud == null)
            TryGetComponent(out primaryHud);

        if (canvasOverlay == null)
            TryGetComponent(out canvasOverlay);

        if (hudCamera == null)
            TryGetComponent(out hudCamera);

        ResolveMissingReferencesFromRegistries(preferredRoot);
        _referencesResolved = primaryHud != null && canvasOverlay != null && hudCamera != null;
    }

    private void RefreshReferencesFromRegistries()
    {
        if (_referencesResolved && primaryHud != null && canvasOverlay != null && hudCamera != null)
            return;

        float now = Application.isPlaying ? (float)SystemDispatcher.CurrentUnscaledTimeSeconds : 0f;
        if (now < _nextAutoResolveAt)
            return;

        _nextAutoResolveAt = now + AutoResolveRetryInterval;
        Transform preferredRoot = ResolveCachedRoot();
        ResolveMissingReferencesFromRegistries(preferredRoot);
        _referencesResolved = primaryHud != null && canvasOverlay != null && hudCamera != null;
    }

    private void ResolveMissingReferencesFromRegistries(Transform preferredRoot)
    {
        if (primaryHud == null)
        {
            HectonSuitHUD_v4.CopyActiveHudsTo(s_hudResolveBuffer);
            primaryHud = FindHudForRoot(s_hudResolveBuffer, preferredRoot);
            s_hudResolveBuffer.Clear();
        }

        if (canvasOverlay == null)
            canvasOverlay = FindOverlayForRoot(preferredRoot);

        if (hudCamera == null)
        {
            if (hudCamera == null && primaryHud != null)
                hudCamera = primaryHud.HudCamera;
            if (hudCamera == null && canvasOverlay != null)
                hudCamera = canvasOverlay.ProjectionCamera;
        }

        if (flashlight == null && preferredRoot != null)
        {
            preferredRoot.TryGetComponent(out flashlight);
        }
    }

    private Transform ResolveCachedRoot()
    {
        if (_cachedRoot == null)
            _cachedRoot = transform.root;

        return _cachedRoot;
    }

    private void RegisterTick()
    {
        if (_tickRegistered || !Application.isPlaying)
            return;

        if (GlobalRegistry.Dispatcher == null)
            return;

        _tickRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
    }

    private void UnregisterTick()
    {
        if (!_tickRegistered)
            return;

        GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
        _tickRegistered = false;
    }

    private void TryRegisterHotSwapListener()
    {
        if (_hotSwapRegistered || !Application.isPlaying)
            return;

        _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
    }

    private void TryUnregisterHotSwapListener()
    {
        if (!_hotSwapRegistered)
            return;

        GlobalRegistry.TryUnregisterHotSwapListener(this);
        _hotSwapRegistered = false;
    }

    private static HectonSuitHUD_v4 FindHudForRoot(List<HectonSuitHUD_v4> huds, Transform preferredRoot)
    {
        if (huds == null)
            return null;

        for (int i = 0; i < huds.Count; i++)
        {
            HectonSuitHUD_v4 candidate = huds[i];
            if (candidate == null)
                continue;

            if (preferredRoot == null || candidate.transform.root == preferredRoot)
                return candidate;
        }

        return null;
    }

    private static SuitHUDV4CanvasOverlay FindOverlayForRoot(Transform preferredRoot)
    {
        for (int i = 0; i < SuitHUDV4CanvasOverlay.ActiveOverlayCount; i++)
        {
            SuitHUDV4CanvasOverlay candidate = SuitHUDV4CanvasOverlay.GetActiveOverlay(i);
            if (candidate == null)
                continue;

            if (preferredRoot == null || candidate.transform.root == preferredRoot)
                return candidate;
        }

        return null;
    }
}
