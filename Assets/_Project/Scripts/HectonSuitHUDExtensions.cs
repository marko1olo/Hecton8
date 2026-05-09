using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.UI;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[ExecuteAlways]
[AddComponentMenu("Hecton8/HUD/Suit HUD Extensions")]
public sealed class HectonSuitHUDExtensions : MonoBehaviour, ITickable, IUpdatable
{
    // COLD ALLOC: List<HectonSuitHUD_v4>[4] — legacy HUD resolver scratch — owner: HectonSuitHUDExtensions
    private static readonly List<HectonSuitHUD_v4> s_hudResolveBuffer = new List<HectonSuitHUD_v4>(4);
    // COLD ALLOC: List<SuitHUDV4CanvasOverlay>[4] — overlay resolver scratch — owner: HectonSuitHUDExtensions
    private static readonly List<SuitHUDV4CanvasOverlay> s_overlayResolveBuffer = new List<SuitHUDV4CanvasOverlay>(4);

    private const float AutoResolveRetryInterval = 1f;

    [Header("Legacy Compatibility")]
    [SerializeField] private Camera hudCamera;
    [SerializeField] private PlayerFlashlight flashlight;
    [SerializeField] private PlayerToolManager toolManager;
    [SerializeField] private HectonSuitHUD_v4 primaryHud;
    [SerializeField] private SuitHUDV4CanvasOverlay canvasOverlay;

    private float _nextAutoResolveAt;
    private bool _tickRegistered;
    private bool _referencesResolved;
    private Transform _cachedRoot;

    private void OnEnable()
    {
#if UNITY_EDITOR
        if (EditorApplication.isCompiling || !Application.isPlaying)
            return;
#endif

        AutoResolveReferences(force: true);
        RegisterTick();
    }

    private void OnDisable()
    {
        UnregisterTick();
    }

    public void Tick(float deltaTime)
    {
        AutoResolveReferences(force: false);
    }

    private void AutoResolveReferences(bool force)
    {
        if (!force && _referencesResolved && primaryHud != null && canvasOverlay != null && hudCamera != null)
            return;

        float now = Application.isPlaying ? Time.realtimeSinceStartup : 0f;
        if (!force && now < _nextAutoResolveAt)
            return;

        _nextAutoResolveAt = now + AutoResolveRetryInterval;
        Transform preferredRoot = ResolveCachedRoot();

        if (primaryHud == null)
        {
            primaryHud = GetComponent<HectonSuitHUD_v4>();
            if (primaryHud == null)
            {
                HectonSuitHUD_v4.CopyActiveHudsTo(s_hudResolveBuffer);
                primaryHud = FindHudForRoot(s_hudResolveBuffer, preferredRoot);
                s_hudResolveBuffer.Clear();
            }
        }

        if (canvasOverlay == null)
        {
            canvasOverlay = GetComponent<SuitHUDV4CanvasOverlay>();
            if (canvasOverlay == null)
            {
                SuitHUDV4CanvasOverlay.CopyActiveOverlaysTo(s_overlayResolveBuffer);
                canvasOverlay = FindOverlayForRoot(s_overlayResolveBuffer, preferredRoot);
                s_overlayResolveBuffer.Clear();
            }
        }

        if (hudCamera == null)
        {
            hudCamera = GetComponent<Camera>();
            if (hudCamera == null && primaryHud != null)
                hudCamera = primaryHud.HudCamera;
            if (hudCamera == null && canvasOverlay != null)
                hudCamera = canvasOverlay.ProjectionCamera;
        }

        _referencesResolved = primaryHud != null && canvasOverlay != null && hudCamera != null;
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

        GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
        _tickRegistered = GlobalRegistry.Updatables.Contains(this);
    }

    private void UnregisterTick()
    {
        if (!_tickRegistered)
            return;

        GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
        _tickRegistered = false;
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

    private static SuitHUDV4CanvasOverlay FindOverlayForRoot(List<SuitHUDV4CanvasOverlay> overlays, Transform preferredRoot)
    {
        if (overlays == null)
            return null;

        for (int i = 0; i < overlays.Count; i++)
        {
            SuitHUDV4CanvasOverlay candidate = overlays[i];
            if (candidate == null)
                continue;

            if (preferredRoot == null || candidate.transform.root == preferredRoot)
                return candidate;
        }

        return null;
    }
}
