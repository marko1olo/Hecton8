using System;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// HUD element that renders the active service-relay handoff target on screen.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class RelayHUDElement : MonoBehaviour, ITickable, IUpdatable
    {
        private enum RelayMarkerVisibilityState : byte
        {
            Hidden_NoCamera = 0,
            Hidden_NoRouteTarget = 1,
            Hidden_TooFar = 2,
            Hidden_BehindCamera = 3,
            Visible_OnScreen = 4,
            Visible_ClampedToEdge = 5
        }

        [Header("â”€â”€ References â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Icon used for the relay route marker.")]
        [SerializeField] private Image markerIcon;

        [Tooltip("Distance readout for the current relay target.")]
        [SerializeField] private TMP_Text distanceText;

        [Tooltip("Label for the current relay target.")]
        [SerializeField] private TMP_Text labelText;

        [Header("â”€â”€ Routing â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Hide the marker when the route target is farther away than this distance.")]
        [SerializeField, Min(10f)] private float maxDisplayDistance = 450f;

        [Tooltip("Hide the marker when the target is behind the camera instead of clamping to the screen edge.")]
        [SerializeField] private bool hideWhenBehindCamera = false;

        [Tooltip("Margin in pixels used when clamping the marker to the screen edge.")]
        [SerializeField, Min(0f)] private float screenMargin = 64f;

        [Header("â”€â”€ Visual â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Marker color while the relay target is on-screen and inside comfortable range.")]
        [SerializeField] private Color onScreenColor = new Color(0.26f, 0.86f, 1f, 0.95f);

        [Tooltip("Marker color while the relay target is off-screen or far away.")]
        [SerializeField] private Color edgeColor = new Color(0.18f, 0.6f, 0.88f, 0.82f);

        private Camera _mainCamera;
        private CanvasGroup _canvasGroup;
        private RectTransform _rectTransform;
        private Transform _playerTransform;
        private EmergencyServiceRelay _trackedRelay;
        private bool _registered;
        private bool _isVisible;
        private bool _hasVisibilityState;
        private bool _hasColorState;
        private bool _lastColorUsedEdgeState;
        private int _lastDistanceMeters = int.MinValue;
        private string _lastLabel = string.Empty;
        private RelayMarkerVisibilityState _lastVisibilityState = RelayMarkerVisibilityState.Hidden_NoRouteTarget;
        private float _lastObservedDistance;
        private float _cameraRetryTime;
        // COLD ALLOC: char[16] - relay HUD distance text staging buffer - owner: RelayHUDElement
        private readonly char[] _distanceBuffer = new char[16];
        private const float CameraRetryInterval = 2f;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            SetVisible(false);
        }

        private void OnEnable()
        {
            TryCacheCamera();

            if (!_registered && Application.isPlaying && GlobalRegistry.Dispatcher != null)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
                _registered = true;
            }
        }

        private void OnDisable()
        {
            if (_registered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _registered = false;
            }

            _trackedRelay = null;
            _lastDistanceMeters = int.MinValue;
            _lastLabel = string.Empty;
            _hasColorState = false;
            _lastColorUsedEdgeState = false;
            _hasVisibilityState = false;
            _isVisible = false;
            SetVisible(false);
        }

        /// <summary>
        /// Binds runtime-created UI references when the marker is injected as a fail-safe.
        /// </summary>
        public void ConfigureRuntimeBindings(Image icon, TMP_Text distance, TMP_Text label)
        {
            markerIcon = icon;
            distanceText = distance;
            labelText = label;
        }

        /// <summary>Returns the latest relay marker visibility state for diagnostics.</summary>
        public string DescribeDebugState()
        {
            return _lastVisibilityState + " distance=" + _lastObservedDistance.ToString("0.0");
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            if (!TryCacheCamera())
            {
                _lastVisibilityState = RelayMarkerVisibilityState.Hidden_NoCamera;
                SetVisible(false);
                return;
            }

            EmergencyServiceRelayDirector relayDirector = Hecton8.Core.GlobalRegistry.EmergencyRelay;
            EmergencyServiceRelay routeTarget = relayDirector != null
                ? relayDirector.GetActiveRouteTarget()
                : null;
            if (routeTarget == null || !routeTarget.isActiveAndEnabled)
            {
                _trackedRelay = null;
                _lastVisibilityState = RelayMarkerVisibilityState.Hidden_NoRouteTarget;
                SetVisible(false);
                return;
            }

            _trackedRelay = routeTarget;

            Vector3 playerPosition = _playerTransform.position;
            Vector3 relayPosition = routeTarget.transform.position;
            float distance = Vector3.Distance(playerPosition, relayPosition);
            _lastObservedDistance = distance;
            if (distance > maxDisplayDistance)
            {
                _lastVisibilityState = RelayMarkerVisibilityState.Hidden_TooFar;
                SetVisible(false);
                return;
            }

            Vector3 screenPosition = _mainCamera.WorldToScreenPoint(relayPosition);
            bool behindCamera = screenPosition.z < 0f;
            if (behindCamera && hideWhenBehindCamera)
            {
                _lastVisibilityState = RelayMarkerVisibilityState.Hidden_BehindCamera;
                SetVisible(false);
                return;
            }

            bool clampedToEdge = behindCamera;
            if (behindCamera)
            {
                screenPosition.x = Screen.width - screenPosition.x;
                screenPosition.y = Screen.height - screenPosition.y;
            }

            float minX = screenMargin;
            float maxX = Screen.width - screenMargin;
            float minY = screenMargin;
            float maxY = Screen.height - screenMargin;

            if (screenPosition.x < minX || screenPosition.x > maxX || screenPosition.y < minY || screenPosition.y > maxY)
            {
                clampedToEdge = true;
                screenPosition.x = Mathf.Clamp(screenPosition.x, minX, maxX);
                screenPosition.y = Mathf.Clamp(screenPosition.y, minY, maxY);
            }

            _rectTransform.position = screenPosition;
            UpdateLabel(routeTarget.RelayLabel);
            UpdateDistance(distance);
            UpdateColor(clampedToEdge);
            _lastVisibilityState = clampedToEdge
                ? RelayMarkerVisibilityState.Visible_ClampedToEdge
                : RelayMarkerVisibilityState.Visible_OnScreen;
            SetVisible(true);
        }

        private bool TryCacheCamera()
        {
            if (_mainCamera != null && _mainCamera.isActiveAndEnabled)
            {
                if (_playerTransform == null)
                {
                    if (!SceneBootstrap.TryGetCurrentPlayerTransform(out _playerTransform) || _playerTransform == null)
                        _playerTransform = _mainCamera.transform;
                }
                return _playerTransform != null;
            }

            _mainCamera = null;

            if (Time.time < _cameraRetryTime)
                return false;

            _cameraRetryTime = Time.time + CameraRetryInterval;

            // Resolve via registry-owned player hierarchy.
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null && playerContext.PlayerTransform != null)
            {
                Transform playerTransform = playerContext.PlayerTransform;
                _mainCamera = playerContext.PlayerCamera;
                if (_mainCamera != null)
                {
                    _playerTransform = playerTransform;
                    return true;
                }
            }

            // Fallback: walk local hierarchy (self, children, parent)
            _mainCamera = GetComponent<Camera>();
            if (_mainCamera == null)
            {
                Transform parent = transform.parent;
                if (parent != null)
                    parent.TryGetComponent(out _mainCamera);
            }

            if (_mainCamera == null)
                return false;

            if (_playerTransform == null)
            {
                if (!SceneBootstrap.TryGetCurrentPlayerTransform(out _playerTransform) || _playerTransform == null)
                    _playerTransform = _mainCamera.transform;
            }

            return _playerTransform != null;
        }

        private void UpdateLabel(string relayLabel)
        {
            if (labelText == null)
                return;

            relayLabel ??= string.Empty;
            if (string.Equals(_lastLabel, relayLabel))
                return;

            _lastLabel = relayLabel;
            labelText.SetText(relayLabel);
        }

        private void UpdateDistance(float distance)
        {
            if (distanceText == null)
                return;

            int distanceMeters = Mathf.RoundToInt(distance);
            if (_lastDistanceMeters == distanceMeters)
                return;

            _lastDistanceMeters = distanceMeters;
            if (!distanceMeters.TryFormat(_distanceBuffer.AsSpan(), out int length))
                length = 0;

            if (length < _distanceBuffer.Length)
            {
                _distanceBuffer[length] = 'M';
                length++;
            }

            distanceText.SetCharArray(_distanceBuffer, 0, length);
            distanceText.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
        }

        private void UpdateColor(bool clampedToEdge)
        {
            Color textColor = clampedToEdge ? edgeColor : onScreenColor;
            if (_hasColorState && _lastColorUsedEdgeState == clampedToEdge)
                return;

            _hasColorState = true;
            _lastColorUsedEdgeState = clampedToEdge;

            if (markerIcon != null)
                markerIcon.color = textColor;

            if (distanceText != null)
                distanceText.color = textColor;

            if (labelText != null)
                labelText.color = textColor;
        }

        private void SetVisible(bool visible)
        {
            if (_canvasGroup == null)
                return;

            if (_hasVisibilityState && _isVisible == visible)
                return;

            _hasVisibilityState = true;
            _isVisible = visible;
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }
    }
}
