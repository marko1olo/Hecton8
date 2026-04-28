using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.SaveSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.PDA
{
    /// <summary>
    /// HUD presenter for player-authored PDA markers when no dedicated compass owner exists.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/PDA/PDA Marker HUD Element")]
    public sealed class PDAMarkerHUDElement : MonoBehaviour, ITickable, IUpdatable
    {
        private sealed class MarkerIconDisplay
        {
            public RectTransform rectTransform;
            public CanvasGroup canvasGroup;
            public Image iconImage;
            public TMP_Text titleText;
            public TMP_Text distanceText;
            public string cachedTitle;
            public string cachedDistance;
        }

        [Header("References")]
        [Tooltip("UI prefab containing optional Image, Label, and Distance children.")]
        [SerializeField] private GameObject markerIconPrefab;
        [Tooltip("Parent transform for instantiated marker icons.")]
        [SerializeField] private RectTransform iconContainer;

        [Header("Display")]
        [Tooltip("Maximum visible HUD distance for PDA markers.")]
        [SerializeField, Min(10f)] private float maxDisplayDistance = 300f;
        [Tooltip("Distance where icons begin to fade toward zero alpha.")]
        [SerializeField, Min(1f)] private float fadeStartDistance = 180f;
        [Tooltip("Screen clamp margin applied to icon positions.")]
        [SerializeField, Min(0f)] private float screenMargin = 48f;
        [Tooltip("Displays marker labels when supported by the prefab.")]
        [SerializeField] private bool showLabels = true;
        [Tooltip("Displays marker distance text when supported by the prefab.")]
        [SerializeField] private bool showDistance = true;

        private const float CameraRetryInterval = 2f;
        private const int MaxCachedDistanceMeters = 2000;

        // COLD ALLOC: string[2001] - cached HUD distance labels - owner: PDAMarkerHUDElement
        private static readonly string[] DistanceLabelCache = BuildDistanceLabelCache();

        // COLD ALLOC: MarkerIconDisplay[64] - UI marker pool - owner: PDAMarkerHUDElement
        private readonly MarkerIconDisplay[] _iconDisplays = new MarkerIconDisplay[PDAMarkerRegistryDTO.MaxEntries];
        // COLD ALLOC: PDAMarkerSnapshot[64] - registry snapshot buffer - owner: PDAMarkerHUDElement
        private readonly PDAMarkerSnapshot[] _markerBuffer = new PDAMarkerSnapshot[PDAMarkerRegistryDTO.MaxEntries];

        private Camera _mainCamera;
        private bool _registeredToTick;
        private float _nextCameraResolveTime;

        private void Awake()
        {
            BuildIconPool();
        }

        private void OnEnable()
        {
            TryRegisterWithTickManager();
        }

        private void OnDisable()
        {
            UnregisterFromTickManager();
            HideAllDisplays();
        }

        private void OnDestroy()
        {
            UnregisterFromTickManager();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (!TryResolveCamera())
            {
                HideAllDisplays();
                return;
            }

            PDAMarkerRegistry markerRegistry = PDAMarkerRegistry.Instance;
            if (markerRegistry == null)
            {
                HideAllDisplays();
                return;
            }

            int markerCount = markerRegistry.CopyMarkers(_markerBuffer, hudOnly: true);
            for (int i = 0; i < _iconDisplays.Length; i++)
            {
                if (i < markerCount)
                    UpdateDisplay(_iconDisplays[i], _markerBuffer[i]);
                else
                    SetDisplayVisible(_iconDisplays[i], false);
            }
        }

        private void BuildIconPool()
        {
            if (markerIconPrefab == null || iconContainer == null)
                return;

            for (int i = 0; i < _iconDisplays.Length; i++)
            {
                // COLD ALLOC: marker HUD icon instance - scene lifetime UI element - owner: PDAMarkerHUDElement
                GameObject iconObject = Instantiate(markerIconPrefab, iconContainer);
                RectTransform rectTransform = iconObject.GetComponent<RectTransform>();
                if (rectTransform == null)
                    rectTransform = iconObject.AddComponent<RectTransform>();

                CanvasGroup canvasGroup = iconObject.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = iconObject.AddComponent<CanvasGroup>();

                MarkerIconDisplay display = new MarkerIconDisplay
                {
                    rectTransform = rectTransform,
                    canvasGroup = canvasGroup,
                    iconImage = iconObject.GetComponent<Image>(),
                    titleText = ResolveChildText(iconObject.transform, "Label"),
                    distanceText = ResolveChildText(iconObject.transform, "Distance"),
                    cachedTitle = string.Empty,
                    cachedDistance = string.Empty
                };

                _iconDisplays[i] = display;
                SetDisplayVisible(display, false);
            }
        }

        private bool TryResolveCamera()
        {
            if (_mainCamera != null && _mainCamera.isActiveAndEnabled)
                return true;

            _mainCamera = null;
            if (Time.unscaledTime < _nextCameraResolveTime)
                return false;

            _nextCameraResolveTime = Time.unscaledTime + CameraRetryInterval;
            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                _mainCamera = ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.PlayerCamera != null) ? Hecton8.Core.GlobalRegistry.Player.PlayerCamera : playerTransform.GetComponent<Camera>());
            }

            return _mainCamera != null && _mainCamera.isActiveAndEnabled;
        }

        private void UpdateDisplay(MarkerIconDisplay display, PDAMarkerSnapshot marker)
        {
            if (display == null || display.rectTransform == null || display.canvasGroup == null || _mainCamera == null)
                return;

            Vector3 cameraPosition = _mainCamera.transform.position;
            float distance = Vector3.Distance(marker.Position, cameraPosition);
            if (distance > maxDisplayDistance)
            {
                SetDisplayVisible(display, false);
                return;
            }

            Vector3 screenPoint = _mainCamera.WorldToScreenPoint(marker.Position);
            if (screenPoint.z <= 0f)
            {
                SetDisplayVisible(display, false);
                return;
            }

            float clampedX = Mathf.Clamp(screenPoint.x, screenMargin, Screen.width - screenMargin);
            float clampedY = Mathf.Clamp(screenPoint.y, screenMargin, Screen.height - screenMargin);
            display.rectTransform.position = new Vector3(clampedX, clampedY, 0f);

            float alpha = 1f;
            if (distance > fadeStartDistance)
            {
                float denominator = Mathf.Max(1f, maxDisplayDistance - fadeStartDistance);
                alpha = 1f - ((distance - fadeStartDistance) / denominator);
            }

            SetDisplayVisible(display, alpha > 0.001f);
            display.canvasGroup.alpha = Mathf.Clamp01(alpha);

            if (display.titleText != null)
            {
                string nextTitle = showLabels ? marker.Title : string.Empty;
                if (!string.Equals(display.cachedTitle, nextTitle, System.StringComparison.Ordinal))
                {
                    display.titleText.text = nextTitle;
                    display.cachedTitle = nextTitle;
                }
            }

            if (display.distanceText != null)
            {
                string nextDistance = showDistance ? ResolveDistanceLabel(Mathf.RoundToInt(distance)) : string.Empty;
                if (!string.Equals(display.cachedDistance, nextDistance, System.StringComparison.Ordinal))
                {
                    display.distanceText.text = nextDistance;
                    display.cachedDistance = nextDistance;
                }
            }

            if (display.iconImage != null)
                display.iconImage.color = ResolveMarkerColor(marker.IconType, alpha);
        }

        private static Color ResolveMarkerColor(MarkerIconType iconType, float alpha)
        {
            Color color;
            switch (iconType)
            {
                case MarkerIconType.Resource:
                    color = new Color(0.20f, 0.85f, 0.55f, alpha);
                    break;
                case MarkerIconType.Hazard:
                    color = new Color(1.00f, 0.34f, 0.22f, alpha);
                    break;
                case MarkerIconType.Shelter:
                    color = new Color(0.42f, 0.72f, 1.00f, alpha);
                    break;
                case MarkerIconType.Objective:
                    color = new Color(1.00f, 0.84f, 0.30f, alpha);
                    break;
                case MarkerIconType.Vehicle:
                    color = new Color(0.74f, 0.64f, 1.00f, alpha);
                    break;
                case MarkerIconType.Beacon:
                    color = new Color(0.30f, 1.00f, 1.00f, alpha);
                    break;
                default:
                    color = new Color(0.90f, 0.95f, 1.00f, alpha);
                    break;
            }

            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private static TMP_Text ResolveChildText(Transform root, string childName)
        {
            if (root == null)
                return null;

            Transform child = root.Find(childName);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        private static void SetDisplayVisible(MarkerIconDisplay display, bool visible)
        {
            if (display == null || display.canvasGroup == null)
                return;

            display.canvasGroup.alpha = visible ? display.canvasGroup.alpha : 0f;
            display.canvasGroup.blocksRaycasts = false;
            display.canvasGroup.interactable = false;
        }

        private static string ResolveDistanceLabel(int meters)
        {
            if (meters <= 0)
                return DistanceLabelCache[0];

            if (meters >= MaxCachedDistanceMeters)
                return DistanceLabelCache[MaxCachedDistanceMeters];

            return DistanceLabelCache[meters];
        }

        private static string[] BuildDistanceLabelCache()
        {
            string[] cache = new string[MaxCachedDistanceMeters + 1];
            for (int i = 0; i < cache.Length; i++)
                cache[i] = i + " m";

            return cache;
        }

        private void HideAllDisplays()
        {
            for (int i = 0; i < _iconDisplays.Length; i++)
                SetDisplayVisible(_iconDisplays[i], false);
        }

        private void TryRegisterWithTickManager()
        {
            if (_registeredToTick)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registeredToTick = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registeredToTick = false;
        }
    }
}
