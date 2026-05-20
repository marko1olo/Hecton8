// ============================================================================
// HECTON-8 â€” BeaconHUDElement.cs
// HUD element for displaying deployed beacons on screen.
//
// ARCHITECTURE:
//   â€¢ ITickable for updates (no Update)
//   â€¢ Zero GC: cached camera via GlobalRegistry, pre-allocated arrays
//   â€¢ World-to-screen conversion for icon positioning
//
// FEATURES:
//   â€¢ Displays beacon icons at world positions
//   â€¢ Shows distance in meters
//   â€¢ Fades out when behind camera or too far
// ============================================================================

namespace Hecton8.UI
{
    using System.Collections.Generic;
    using Hecton8.Core;
    using Hecton8.Gameplay;
    using Hecton8.World;
    using Hecton.Localization;
    using Unity.Mathematics;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// HUD element that displays deployed beacons on screen.
    /// Uses ITickable for updates. Zero GC in hot paths.
    /// </summary>
    public class BeaconHUDElement : MonoBehaviour, ITickable, IUpdatable, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
    {
        private static readonly char[] s_EmptyChars = new char[1]; // COLD ALLOC: char[1] — empty TMP payload sentinel — owner: BeaconHUDElement
        private const int BeaconLabelTextCapacity = 96;
        private const int DistanceTextCapacity = 32;
        private const uint LabelHashSeed = 2166136261u;
        private const uint LabelHashPrime = 16777619u;
        // COLD ALLOC: List<Graphic>[8] — temporary prefab graphic raycast pruning scratch — owner: BeaconHUDElement
        private static readonly List<Graphic> s_GraphicRaycastDisableScratch = new List<Graphic>(8);

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ References â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Icon prefab to instantiate for each beacon.")]
        [SerializeField] private GameObject beaconIconPrefab;

        [Tooltip("Parent transform for beacon icons.")]
        [SerializeField] private Transform iconContainer;

        [Header("â”€â”€ Display Settings â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Maximum distance to show beacons (meters).")]
        [SerializeField] private float maxDisplayDistance = 200f;

        [Tooltip("Distance at which icons start to fade.")]
        [SerializeField] private float fadeStartDistance = 150f;

        [Tooltip("Screen margin for clamping icons.")]
        [SerializeField] private float screenMargin = 50f;

        [Tooltip("Show distance label under icon.")]
        [SerializeField] private bool showDistance = true;

        [Tooltip("Show beacon labels when the icon prefab provides a dedicated TMP child named Label.")]
        [SerializeField] private bool showLabel = true;

        [Header("â”€â”€ Colors â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField] private Color normalColor = new Color(0f, 0.9f, 1f, 1f);
        [SerializeField] private Color distantColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PRIVATE STATE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private Camera _mainCamera;
        private Transform _cachedTransform;
        private bool _registered;
        private bool _hotSwapListenerRegistered;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private GameLanguage _distanceLanguage = GameLanguage.English;
        private float _cameraRetryTimer;
        private float _idlePollTimer;
        private const float CameraRetryInterval = 2f;
        private const float IdlePollInterval = 0.25f;
        // COLD ALLOC: char[24] — localized distance pattern cache — owner: BeaconHUDElement
        private readonly char[] _distancePatternBuffer = new char[24];
        private int _distancePatternLength = 6;

        // Pre-allocated array for beacon icons
        private BeaconIconDisplay[] _iconDisplays = new BeaconIconDisplay[16]; // COLD ALLOC: BeaconIconDisplay[16] — max visible beacon icon slots — owner: BeaconHUDElement
        private int _activeIconCount;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LIFECYCLE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void Awake()
        {
            _cachedTransform = transform;
            _cameraRetryTimer = 0f; // Allow immediate first resolve in Tick
            _idlePollTimer = 0f;

            // Pre-create icon pool
            if (beaconIconPrefab != null && iconContainer != null)
            {
                for (int i = 0; i < _iconDisplays.Length; i++)
                {
                    GameObject icon = Instantiate(beaconIconPrefab, iconContainer);
                    if (!icon.TryGetComponent(out CanvasGroup canvasGroup))
                    {
                        // COLD ALLOC: CanvasGroup[1] — missing beacon icon visibility proxy — owner: BeaconHUDElement
                        canvasGroup = icon.AddComponent<CanvasGroup>();
                    }
                    DisableGraphicRaycasts(icon);

                    _iconDisplays[i] = new BeaconIconDisplay // COLD ALLOC: BeaconIconDisplay[1] — pooled beacon HUD icon state — owner: BeaconHUDElement
                    {
                        gameObject = icon,
                        transform = icon.transform,
                        canvasGroup = canvasGroup,
                        labelText = ResolveChildText(icon.transform, "Label"),
                        distanceText = ResolveChildText(icon.transform, "Distance"),
                        labelBuffer = new char[BeaconLabelTextCapacity], // COLD ALLOC: char[96] — beacon HUD label text staging buffer — owner: BeaconHUDElement
                        distanceBuffer = new char[DistanceTextCapacity] // COLD ALLOC: char[32] — beacon HUD distance text staging buffer — owner: BeaconHUDElement
                    };

                    ApplyDisplayVisible(_iconDisplays[i], false, 0f);
                }
            }
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            LocalizationEvents.RegisterLanguageListener(this);
            RebuildLocalizationCache();
            RegisterToTick();
        }

        private void OnDisable()
        {
            LocalizationEvents.UnregisterLanguageListener(this);
            TryUnregisterHotSwapListener();
            UnregisterFromTick();
            HideAllIcons();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  ITickable
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public void Tick(float deltaTime)
        {
            float safeDeltaTime = math.max(0f, deltaTime);
            _cameraRetryTimer = math.max(0f, _cameraRetryTimer - safeDeltaTime);
            int beaconCount = BeaconRegistry.Count;
            if (_activeIconCount == 0 && beaconCount <= 0)
            {
                _idlePollTimer = math.max(0f, _idlePollTimer - safeDeltaTime);
                if (_idlePollTimer > 0f)
                    return;

                _idlePollTimer = IdlePollInterval;
            }

            if (!TryResolveCamera())
            {
                HideAllIcons();
                return;
            }

            if (beaconCount <= 0)
            {
                HideAllIcons();
                return;
            }

            if (!TryResolveObserverAup(out AbsoluteUniversePosition observerAup))
            {
                HideAllIcons();
                return;
            }

            DeployableBeacon[] beacons = BeaconRegistry.GetAllBeacons();
            if (beacons == null || beacons.Length <= 0)
            {
                HideAllIcons();
                return;
            }

            beaconCount = math.min(beaconCount, math.min(BeaconRegistry.Count, beacons.Length));
            _idlePollTimer = 0f;

            // Hide excess icons
            for (int i = beaconCount; i < _activeIconCount; i++)
            {
                if (_iconDisplays[i] != null && _iconDisplays[i].gameObject != null)
                    ApplyDisplayVisible(_iconDisplays[i], false, 0f);
            }

            _activeIconCount = math.min(beaconCount, _iconDisplays.Length);
            double maxDisplayDistanceSq = (double)maxDisplayDistance * maxDisplayDistance;
            double fadeStartDistanceSq = (double)fadeStartDistance * fadeStartDistance;
            double fadeDistanceSqSpan = math.max(0.001d, maxDisplayDistanceSq - fadeStartDistanceSq);
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            // Update each beacon icon
            for (int i = 0; i < _activeIconCount; i++)
            {
                DeployableBeacon beacon = beacons[i];
                if (beacon == null || !beacon.isActiveAndEnabled)
                {
                    if (_iconDisplays[i] != null && _iconDisplays[i].gameObject != null)
                        ApplyDisplayVisible(_iconDisplays[i], false, 0f);
                    continue;
                }

                UpdateBeaconIcon(
                    i,
                    beacon,
                    in observerAup,
                    maxDisplayDistanceSq,
                    fadeStartDistanceSq,
                    fadeDistanceSqSpan,
                    screenWidth,
                    screenHeight);
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PRIVATE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private bool TryResolveObserverAup(out AbsoluteUniversePosition observerAup)
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null && playerContext.PlayerMovement != null)
            {
                observerAup = playerContext.PlayerMovement.CurrentAup;
                return true;
            }

            observerAup = default;
            return false;
        }

        private bool TryResolveCamera()
        {
            if (_mainCamera != null && _mainCamera.isActiveAndEnabled)
                return true;

            _mainCamera = null;
            if (_cameraRetryTimer > 0f)
                return false;

            _cameraRetryTimer = CameraRetryInterval;
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null)
                _mainCamera = playerContext.PlayerCamera;

            return _mainCamera != null && _mainCamera.isActiveAndEnabled;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Player)
                return;

            _cachedPlayerContext = currentService as IPlayerRuntimeContext;
            _mainCamera = _cachedPlayerContext != null ? _cachedPlayerContext.PlayerCamera : null;
            _cameraRetryTimer = 0f;
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

        private void CacheRegistryServicesCold()
        {
            _cachedPlayerContext = GlobalRegistry.Player;
            if (_mainCamera == null && _cachedPlayerContext != null)
                _mainCamera = _cachedPlayerContext.PlayerCamera;
        }

        private void UpdateBeaconIcon(
            int index,
            DeployableBeacon beacon,
            in AbsoluteUniversePosition observerAup,
            double maxDisplayDistanceSq,
            double fadeStartDistanceSq,
            double fadeDistanceSqSpan,
            float screenWidth,
            float screenHeight)
        {
            BeaconIconDisplay display = _iconDisplays[index];
            if (display == null || display.gameObject == null)
                return;

            AbsoluteUniversePosition beaconAup = beacon.PositionAup;
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in observerAup, in beaconAup);

            // Check if too far
            if (distanceSq > maxDisplayDistanceSq)
            {
                ApplyDisplayVisible(display, false, 0f);
                return;
            }

            // World to screen
            float3 runtimePosition = beaconAup.ToRuntimeFloat3();
            Vector3 worldPos = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

            // Check if behind camera
            if (screenPos.z < 0f)
            {
                ApplyDisplayVisible(display, false, 0f);
                return;
            }

            // Clamp to screen bounds
            float clampedX = math.clamp(screenPos.x, screenMargin, screenWidth - screenMargin);
            float clampedY = math.clamp(screenPos.y, screenMargin, screenHeight - screenMargin);

            ApplyDisplayPosition(display, clampedX, clampedY);

            // Calculate alpha based on distance
            float alpha = 1f;
            if (distanceSq > fadeStartDistanceSq)
            {
                alpha = 1f - math.saturate((float)((distanceSq - fadeStartDistanceSq) / fadeDistanceSqSpan));
            }

            ApplyDisplayVisible(display, true, alpha);

            // Update distance text
            if (display.labelText != null)
            {
                if (showLabel)
                {
                    string displayLabel = beacon.DisplayLabel;
                    int labelLength = ResolveLabelDisplayLength(displayLabel, display.labelBuffer);
                    bool truncated = IsLabelTruncated(displayLabel, labelLength, display.labelBuffer);
                    uint labelHash = ComputeLabelDisplayHash(displayLabel, labelLength, truncated);
                    if (!display.HasCachedLabel ||
                        display.CachedLabelLength != labelLength ||
                        display.CachedLabelHash != labelHash ||
                        !LabelBufferMatches(display, displayLabel, labelLength, truncated))
                    {
                        WriteLabelToBuffer(displayLabel, display.labelBuffer, labelLength, truncated);
                        display.labelText.SetCharArray(display.labelBuffer, 0, labelLength);
                        display.labelText.UpdateVertexData(TMPro.TMP_VertexDataUpdateFlags.All);
                        display.CachedLabelLength = labelLength;
                        display.CachedLabelHash = labelHash;
                        display.HasCachedLabel = true;
                    }
                }
                else if (display.HasCachedLabel)
                {
                    display.labelText.SetCharArray(s_EmptyChars, 0, 0);
                    display.labelText.UpdateVertexData(TMPro.TMP_VertexDataUpdateFlags.All);
                    display.CachedLabelLength = 0;
                    display.CachedLabelHash = LabelHashSeed;
                    display.HasCachedLabel = false;
                }
            }

            if (display.distanceText != null)
            {
                if (showDistance)
                {
                    float distance = ApproximateDistanceMetersFromSq(distanceSq);
                    int roundedDistance = (int)math.round(distance);
                    if (!display.HasCachedDistance || display.CachedDistanceMeters != roundedDistance)
                    {
                        float localizedDistance = LocalizedMeasurementFormatter.ConvertDistanceMeters(distance, _distanceLanguage);
                        System.ReadOnlySpan<char> distancePattern = System.MemoryExtensions.AsSpan(_distancePatternBuffer, 0, _distancePatternLength);
                        if (!LocNumericBuffer.TryWrite(distancePattern, display.distanceBuffer, LocNumericArg.Float(localizedDistance), out int length))
                            length = 0;

                        display.distanceText.SetCharArray(display.distanceBuffer, 0, length);
                        display.distanceText.UpdateVertexData(TMPro.TMP_VertexDataUpdateFlags.All);
                        display.CachedDistanceMeters = roundedDistance;
                        display.HasCachedDistance = true;
                    }
                }
                else if (display.HasCachedDistance)
                {
                    display.distanceText.SetCharArray(s_EmptyChars, 0, 0);
                    display.distanceText.UpdateVertexData(TMPro.TMP_VertexDataUpdateFlags.All);
                    display.CachedDistanceMeters = 0;
                    display.HasCachedDistance = false;
                }
            }
        }

        private static float ApproximateDistanceMetersFromSq(double distanceSq)
        {
            if (double.IsNaN(distanceSq) || double.IsInfinity(distanceSq))
                return float.PositiveInfinity;
            if (distanceSq <= 0d)
                return 0f;

            float clampedSq = (float)math.min(distanceSq, (double)float.MaxValue);
            uint estimateBits = (math.asuint(clampedSq) >> 1) + 0x1FC00000u;
            float estimate = math.asfloat(estimateBits);
            return 0.5f * (estimate + (clampedSq / math.max(estimate, 0.0001f)));
        }

        private static bool LabelBufferMatches(BeaconIconDisplay display, string displayLabel, int labelLength, bool truncated)
        {
            for (int i = 0; i < labelLength; i++)
            {
                if (display.labelBuffer[i] != ResolveLabelDisplayChar(displayLabel, i, labelLength, truncated))
                    return false;
            }

            return true;
        }

        private static int ResolveLabelDisplayLength(string displayLabel, char[] destination)
        {
            if (string.IsNullOrEmpty(displayLabel) || destination == null || destination.Length == 0)
                return 0;

            return math.min(displayLabel.Length, destination.Length);
        }

        private static bool IsLabelTruncated(string displayLabel, int labelLength, char[] destination)
        {
            return displayLabel != null &&
                   destination != null &&
                   displayLabel.Length > destination.Length &&
                   labelLength >= 3;
        }

        private static void WriteLabelToBuffer(string displayLabel, char[] destination, int labelLength, bool truncated)
        {
            for (int i = 0; i < labelLength; i++)
                destination[i] = ResolveLabelDisplayChar(displayLabel, i, labelLength, truncated);
        }

        private static uint ComputeLabelDisplayHash(string displayLabel, int labelLength, bool truncated)
        {
            uint hash = LabelHashSeed;
            for (int i = 0; i < labelLength; i++)
            {
                hash ^= ResolveLabelDisplayChar(displayLabel, i, labelLength, truncated);
                hash *= LabelHashPrime;
            }

            return hash;
        }

        private static char ResolveLabelDisplayChar(string displayLabel, int index, int labelLength, bool truncated)
        {
            if (truncated && index >= labelLength - 3)
                return '.';

            return displayLabel[index];
        }

        private void HideAllIcons()
        {
            if (_activeIconCount <= 0)
                return;

            int count = math.min(_activeIconCount, _iconDisplays.Length);
            for (int i = 0; i < count; i++)
            {
                if (_iconDisplays[i] != null && _iconDisplays[i].gameObject != null)
                    ApplyDisplayVisible(_iconDisplays[i], false, 0f);
            }

            _activeIconCount = 0;
        }

        private static void ApplyDisplayPosition(BeaconIconDisplay display, float x, float y)
        {
            if (display == null || display.transform == null)
                return;

            int pixelX = (int)math.round(x);
            int pixelY = (int)math.round(y);
            if (display.HasCachedPosition &&
                display.CachedPixelX == pixelX &&
                display.CachedPixelY == pixelY)
            {
                return;
            }

            display.CachedPixelX = pixelX;
            display.CachedPixelY = pixelY;
            display.HasCachedPosition = true;
            display.transform.position = new Vector3(pixelX, pixelY, 0f);
        }

        private static void ApplyDisplayVisible(BeaconIconDisplay display, bool visible, float alpha)
        {
            if (display == null || display.gameObject == null)
                return;

            CanvasGroup canvasGroup = display.canvasGroup;
            if (canvasGroup == null)
                return;

            float targetAlpha = visible ? math.saturate(alpha) : 0f;
            if (display.HasCachedVisibility &&
                display.CachedVisible == visible &&
                math.abs(display.CachedAlpha - targetAlpha) <= 0.0001f &&
                !canvasGroup.blocksRaycasts &&
                !canvasGroup.interactable)
            {
                return;
            }

            if (math.abs(canvasGroup.alpha - targetAlpha) > 0.0001f)
                canvasGroup.alpha = targetAlpha;
            if (canvasGroup.blocksRaycasts)
                canvasGroup.blocksRaycasts = false;
            if (canvasGroup.interactable)
                canvasGroup.interactable = false;
            if (!visible)
                display.HasCachedPosition = false;
            display.CachedVisible = visible;
            display.CachedAlpha = targetAlpha;
            display.HasCachedVisibility = true;
        }

        private static void DisableGraphicRaycasts(GameObject root)
        {
            if (root == null)
                return;

            s_GraphicRaycastDisableScratch.Clear();
            root.GetComponentsInChildren(true, s_GraphicRaycastDisableScratch);
            for (int i = 0; i < s_GraphicRaycastDisableScratch.Count; i++)
            {
                Graphic graphic = s_GraphicRaycastDisableScratch[i];
                if (graphic != null)
                    graphic.raycastTarget = false;
            }

            s_GraphicRaycastDisableScratch.Clear();
        }

        private void RegisterToTick()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
        }

        private void UnregisterFromTick()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
        }

        private static TMPro.TMP_Text ResolveChildText(Transform root, string childName)
        {
            if (root == null)
                return null;

            Transform child = FindDeepChild(root, childName);
            if (child == null)
                return null;

            child.TryGetComponent(out TMPro.TMP_Text text);
            return text;
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildLocalizationCache();
            InvalidateDisplayCaches();
        }

        private void RebuildLocalizationCache()
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            _distanceLanguage = manager != null ? manager.CurrentLanguage : GameLanguage.English;
            string unitLabel = LocalizedMeasurementFormatter.ResolveDistanceUnitLabel(_distanceLanguage);
            if (string.IsNullOrEmpty(unitLabel))
                unitLabel = "m";

            const string prefix = "{0:0} ";
            int cursor = 0;
            for (int i = 0; i < prefix.Length && cursor < _distancePatternBuffer.Length; i++)
                _distancePatternBuffer[cursor++] = prefix[i];

            for (int i = 0; i < unitLabel.Length && cursor < _distancePatternBuffer.Length; i++)
                _distancePatternBuffer[cursor++] = unitLabel[i];

            _distancePatternLength = cursor;
        }

        private void InvalidateDisplayCaches()
        {
            for (int i = 0; i < _iconDisplays.Length; i++)
            {
                BeaconIconDisplay display = _iconDisplays[i];
                if (display == null)
                    continue;

                display.CachedLabelLength = 0;
                display.CachedLabelHash = LabelHashSeed;
                display.HasCachedLabel = false;
                display.CachedDistanceMeters = 0;
                display.HasCachedDistance = false;
            }
        }

        private static Transform FindDeepChild(Transform root, string childName)
        {
            if (root == null)
                return null;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (string.Equals(child.name, childName, System.StringComparison.Ordinal))
                    return child;

                Transform nested = FindDeepChild(child, childName);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INNER CLASS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private class BeaconIconDisplay
        {
            public GameObject gameObject;
            public Transform transform;
            public CanvasGroup canvasGroup;
            public TMPro.TMP_Text labelText;
            public TMPro.TMP_Text distanceText;
            public char[] labelBuffer;
            public char[] distanceBuffer;
            public int CachedLabelLength;
            public uint CachedLabelHash = LabelHashSeed;
            public bool HasCachedLabel;
            public int CachedDistanceMeters;
            public bool HasCachedDistance;
            public bool CachedVisible;
            public float CachedAlpha = -1f;
            public bool HasCachedVisibility;
            public int CachedPixelX;
            public int CachedPixelY;
            public bool HasCachedPosition;
        }
    }
}
