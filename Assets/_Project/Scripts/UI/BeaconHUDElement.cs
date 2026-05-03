// ============================================================================
// HECTON-8 â€” BeaconHUDElement.cs
// HUD element for displaying deployed beacons on screen.
//
// ARCHITECTURE:
//   â€¢ ITickable for updates (no Update)
//   â€¢ Zero GC: cached camera via SceneBootstrap, pre-allocated arrays
//   â€¢ World-to-screen conversion for icon positioning
//
// FEATURES:
//   â€¢ Displays beacon icons at world positions
//   â€¢ Shows distance in meters
//   â€¢ Fades out when behind camera or too far
// ============================================================================

namespace Hecton8.UI
{
    using Hecton8.Core;
    using Hecton8.Gameplay;
    using Hecton.Localization;
    using UnityEngine;

    /// <summary>
    /// HUD element that displays deployed beacons on screen.
    /// Uses ITickable for updates. Zero GC in hot paths.
    /// </summary>
    public class BeaconHUDElement : MonoBehaviour, ITickable, IUpdatable, ILocalizationLanguageChangedListener
    {
        private static readonly char[] s_EmptyChars = new char[1];
        private const int BeaconLabelTextCapacity = 96;
        private const uint LabelHashSeed = 2166136261u;
        private const uint LabelHashPrime = 16777619u;

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
        private GameLanguage _distanceLanguage = GameLanguage.English;
        private float _cameraRetryTime;
        private const float CameraRetryInterval = 2f;
        // COLD ALLOC: char[24] â€” localized distance pattern cache â€” owner: BeaconHUDElement
        private readonly char[] _distancePatternBuffer = new char[24];
        private int _distancePatternLength = 6;

        // Pre-allocated array for beacon icons
        private BeaconIconDisplay[] _iconDisplays = new BeaconIconDisplay[16]; // COLD ALLOC: max 16 beacon icons â€” owner: BeaconHUDElement
        private int _activeIconCount;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LIFECYCLE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void Awake()
        {
            _cachedTransform = transform;
            _cameraRetryTime = 0f; // Allow immediate first resolve in Tick

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
                    _iconDisplays[i] = new BeaconIconDisplay
                    {
                        gameObject = icon,
                        transform = icon.transform,
                        canvasGroup = canvasGroup,
                        labelText = ResolveChildText(icon.transform, "Label"),
                        distanceText = ResolveChildText(icon.transform, "Distance"),
                        labelBuffer = new char[BeaconLabelTextCapacity] // COLD ALLOC: char[96] - beacon HUD label text staging buffer - owner: BeaconHUDElement
                    };

                    ApplyDisplayVisible(_iconDisplays[i], false, 0f);
                }
            }
        }

        private void OnEnable()
        {
            LocalizationEvents.RegisterLanguageListener(this);
            RebuildLocalizationCache();
            RegisterToTick();
        }

        private void OnDisable()
        {
            LocalizationEvents.UnregisterLanguageListener(this);
            UnregisterFromTick();
            HideAllIcons();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  ITickable
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public void Tick(float deltaTime)
        {
            if (_mainCamera == null || !_mainCamera.isActiveAndEnabled)
            {
                _mainCamera = null;
                if (Time.time < _cameraRetryTime)
                    return;

                _cameraRetryTime = Time.time + CameraRetryInterval;

                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                if (playerContext != null)
                    _mainCamera = playerContext.PlayerCamera;

                if (_mainCamera == null)
                    return;
            }

            DeployableBeacon[] beacons = BeaconRegistry.GetAllBeacons();
            int beaconCount = BeaconRegistry.Count;

            // Hide excess icons
            for (int i = beaconCount; i < _activeIconCount; i++)
            {
                if (_iconDisplays[i] != null && _iconDisplays[i].gameObject != null)
                    ApplyDisplayVisible(_iconDisplays[i], false, 0f);
            }

            _activeIconCount = Mathf.Min(beaconCount, _iconDisplays.Length);

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

                UpdateBeaconIcon(i, beacon);
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PRIVATE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void UpdateBeaconIcon(int index, DeployableBeacon beacon)
        {
            BeaconIconDisplay display = _iconDisplays[index];
            if (display == null || display.gameObject == null)
                return;

            Vector3 worldPos = beacon.Position;
            Vector3 cameraPos = _mainCamera.transform.position;
            float distance = Vector3.Distance(worldPos, cameraPos);

            // Check if too far
            if (distance > maxDisplayDistance)
            {
                ApplyDisplayVisible(display, false, 0f);
                return;
            }

            // World to screen
            Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

            // Check if behind camera
            if (screenPos.z < 0f)
            {
                ApplyDisplayVisible(display, false, 0f);
                return;
            }

            // Clamp to screen bounds
            float clampedX = Mathf.Clamp(screenPos.x, screenMargin, Screen.width - screenMargin);
            float clampedY = Mathf.Clamp(screenPos.y, screenMargin, Screen.height - screenMargin);

            // Activate and position
            display.transform.position = new Vector3(clampedX, clampedY, 0f);

            // Calculate alpha based on distance
            float alpha = 1f;
            if (distance > fadeStartDistance)
            {
                alpha = 1f - (distance - fadeStartDistance) / (maxDisplayDistance - fadeStartDistance);
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
                    int roundedDistance = Mathf.RoundToInt(distance);
                    if (!display.HasCachedDistance || display.CachedDistanceMeters != roundedDistance)
                    {
                        float localizedDistance = LocalizedMeasurementFormatter.ConvertDistanceMeters(distance, _distanceLanguage);
                        LocNumericBuffer.Write(new System.ReadOnlySpan<char>(_distancePatternBuffer, 0, _distancePatternLength), LocNumericArg.Float(localizedDistance), out char[] buffer, out int length);
                        display.distanceText.SetCharArray(buffer, 0, length);
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

            return Mathf.Min(displayLabel.Length, destination.Length);
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
            for (int i = 0; i < _iconDisplays.Length; i++)
            {
                if (_iconDisplays[i] != null && _iconDisplays[i].gameObject != null)
                    ApplyDisplayVisible(_iconDisplays[i], false, 0f);
            }
            _activeIconCount = 0;
        }

        private static void ApplyDisplayVisible(BeaconIconDisplay display, bool visible, float alpha)
        {
            if (display == null || display.gameObject == null)
                return;

            CanvasGroup canvasGroup = display.canvasGroup;
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = visible ? Mathf.Clamp01(alpha) : 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        private void RegisterToTick()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registered = GlobalRegistry.Updatables.Contains(this);
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
            public int CachedLabelLength;
            public uint CachedLabelHash = LabelHashSeed;
            public bool HasCachedLabel;
            public int CachedDistanceMeters;
            public bool HasCachedDistance;
        }
    }
}
