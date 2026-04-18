// ============================================================================
// HECTON-8 — BeaconHUDElement.cs
// HUD element for displaying deployed beacons on screen.
//
// ARCHITECTURE:
//   • ITickable for updates (no Update)
//   • Zero GC: cached Camera.main, pre-allocated arrays
//   • World-to-screen conversion for icon positioning
//
// FEATURES:
//   • Displays beacon icons at world positions
//   • Shows distance in meters
//   • Fades out when behind camera or too far
// ============================================================================

namespace Hecton8.UI
{
    using Hecton8.Core;
    using Hecton8.Gameplay;
    using UnityEngine;

    /// <summary>
    /// HUD element that displays deployed beacons on screen.
    /// Uses ITickable for updates. Zero GC in hot paths.
    /// </summary>
    public class BeaconHUDElement : MonoBehaviour, ITickable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── References ────────────────────────────────")]
        [Tooltip("Icon prefab to instantiate for each beacon.")]
        [SerializeField] private GameObject beaconIconPrefab;

        [Tooltip("Parent transform for beacon icons.")]
        [SerializeField] private Transform iconContainer;

        [Header("── Display Settings ──────────────────────────")]
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

        [Header("── Colors ─────────────────────────────────────")]
        [SerializeField] private Color normalColor = new Color(0f, 0.9f, 1f, 1f);
        [SerializeField] private Color distantColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private Camera _mainCamera;
        private Transform _cachedTransform;
        private bool _registered;

        // Pre-allocated array for beacon icons
        private BeaconIconDisplay[] _iconDisplays = new BeaconIconDisplay[16]; // COLD ALLOC: max 16 beacon icons — owner: BeaconHUDElement
        private int _activeIconCount;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _cachedTransform = transform;
            _mainCamera = Camera.main;

            // Pre-create icon pool
            if (beaconIconPrefab != null && iconContainer != null)
            {
                for (int i = 0; i < _iconDisplays.Length; i++)
                {
                    GameObject icon = Instantiate(beaconIconPrefab, iconContainer);
                    icon.SetActive(false);
                    _iconDisplays[i] = new BeaconIconDisplay
                    {
                        gameObject = icon,
                        transform = icon.transform,
                        canvasGroup = icon.GetComponent<CanvasGroup>(),
                        labelText = ResolveChildText(icon.transform, "Label"),
                        distanceText = ResolveChildText(icon.transform, "Distance")
                    };
                }
            }
        }

        private void OnEnable()
        {
            RegisterToTick();
        }

        private void OnDisable()
        {
            UnregisterFromTick();
            HideAllIcons();
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable
        // ══════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null)
                    return;
            }

            DeployableBeacon[] beacons = BeaconRegistry.GetAllBeacons();
            int beaconCount = BeaconRegistry.Count;

            // Hide excess icons
            for (int i = beaconCount; i < _activeIconCount; i++)
            {
                if (_iconDisplays[i] != null && _iconDisplays[i].gameObject != null)
                    _iconDisplays[i].gameObject.SetActive(false);
            }

            _activeIconCount = Mathf.Min(beaconCount, _iconDisplays.Length);

            // Update each beacon icon
            for (int i = 0; i < _activeIconCount; i++)
            {
                DeployableBeacon beacon = beacons[i];
                if (beacon == null || !beacon.isActiveAndEnabled)
                {
                    if (_iconDisplays[i] != null && _iconDisplays[i].gameObject != null)
                        _iconDisplays[i].gameObject.SetActive(false);
                    continue;
                }

                UpdateBeaconIcon(i, beacon);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

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
                display.gameObject.SetActive(false);
                return;
            }

            // World to screen
            Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

            // Check if behind camera
            if (screenPos.z < 0f)
            {
                display.gameObject.SetActive(false);
                return;
            }

            // Clamp to screen bounds
            float clampedX = Mathf.Clamp(screenPos.x, screenMargin, Screen.width - screenMargin);
            float clampedY = Mathf.Clamp(screenPos.y, screenMargin, Screen.height - screenMargin);

            // Activate and position
            display.gameObject.SetActive(true);
            display.transform.position = new Vector3(clampedX, clampedY, 0f);

            // Calculate alpha based on distance
            float alpha = 1f;
            if (distance > fadeStartDistance)
            {
                alpha = 1f - (distance - fadeStartDistance) / (maxDisplayDistance - fadeStartDistance);
            }

            // Apply alpha
            if (display.canvasGroup != null)
            {
                display.canvasGroup.alpha = alpha;
            }

            // Update distance text
            if (display.labelText != null)
            {
                if (showLabel)
                {
                    string displayLabel = beacon.DisplayLabel;
                    if (!string.Equals(display.CachedLabel, displayLabel, System.StringComparison.Ordinal))
                    {
                        display.labelText.text = displayLabel;
                        display.CachedLabel = displayLabel;
                    }
                }
                else if (!string.IsNullOrEmpty(display.CachedLabel))
                {
                    display.labelText.text = string.Empty;
                    display.CachedLabel = string.Empty;
                }
            }

            if (display.distanceText != null)
            {
                if (showDistance)
                {
                    int roundedDistance = Mathf.RoundToInt(distance);
                    if (!display.HasCachedDistance || display.CachedDistanceMeters != roundedDistance)
                    {
                        display.distanceText.SetText("{0:0}m", roundedDistance);
                        display.CachedDistanceMeters = roundedDistance;
                        display.HasCachedDistance = true;
                    }
                }
                else if (display.HasCachedDistance)
                {
                    display.distanceText.text = string.Empty;
                    display.CachedDistanceMeters = 0;
                    display.HasCachedDistance = false;
                }
            }
        }

        private void HideAllIcons()
        {
            for (int i = 0; i < _iconDisplays.Length; i++)
            {
                if (_iconDisplays[i] != null && _iconDisplays[i].gameObject != null)
                    _iconDisplays[i].gameObject.SetActive(false);
            }
            _activeIconCount = 0;
        }

        private void RegisterToTick()
        {
            if (_registered)
                return;

            if (GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register(this);
                _registered = true;
            }
        }

        private void UnregisterFromTick()
        {
            if (!_registered)
                return;

            if (GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }
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

        // ══════════════════════════════════════════════════════════
        //  INNER CLASS
        // ══════════════════════════════════════════════════════════

        private class BeaconIconDisplay
        {
            public GameObject gameObject;
            public Transform transform;
            public CanvasGroup canvasGroup;
            public TMPro.TMP_Text labelText;
            public TMPro.TMP_Text distanceText;
            public string CachedLabel;
            public int CachedDistanceMeters;
            public bool HasCachedDistance;
        }
    }
}
