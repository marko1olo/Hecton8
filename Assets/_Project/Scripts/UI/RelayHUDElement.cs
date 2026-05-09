using System;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.World;
using TMPro;
using Unity.Mathematics;
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
            Hidden_NoPlayerAup = 4,
            Visible_OnScreen = 5,
            Visible_ClampedToEdge = 6
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
        private HectonPlayerMovement _playerMovement;
        private EmergencyServiceRelay _trackedRelay;
        private bool _registered;
        private bool _isVisible;
        private bool _hasLabelState;
        private bool _hasVisibilityState;
        private bool _hasColorState;
        private bool _hasPositionState;
        private bool _lastColorUsedEdgeState;
        private int _lastPixelX;
        private int _lastPixelY;
        private int _lastDistanceMeters = int.MinValue;
        private int _lastLabelLength;
        private uint _lastLabelHash = LabelHashSeed;
        private RelayMarkerVisibilityState _lastVisibilityState = RelayMarkerVisibilityState.Hidden_NoRouteTarget;
        private float _lastObservedDistance;
        private float _cameraRetryTimer;
        private float _hiddenPollTimer;
        // COLD ALLOC: char[16] - relay HUD distance text staging buffer - owner: RelayHUDElement
        private readonly char[] _distanceBuffer = new char[16];
        // COLD ALLOC: char[96] - relay HUD label text staging buffer - owner: RelayHUDElement
        private readonly char[] _labelBuffer = new char[LabelTextCapacity];
        private const float CameraRetryInterval = 2f;
        private const float HiddenPollInterval = 0.25f;
        private const int LabelTextCapacity = 96;
        private const uint LabelHashSeed = 2166136261u;
        private const uint LabelHashPrime = 16777619u;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            SetVisible(false);
        }

        private void OnEnable()
        {
            TryCacheCamera(0f);

            TryRegister();
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
            _lastLabelLength = 0;
            _lastLabelHash = LabelHashSeed;
            _hasLabelState = false;
            _hasColorState = false;
            _lastColorUsedEdgeState = false;
            _hasVisibilityState = false;
            _hasPositionState = false;
            _isVisible = false;
            _hiddenPollTimer = 0f;
            _cameraRetryTimer = 0f;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return _lastVisibilityState switch
            {
                RelayMarkerVisibilityState.Hidden_NoCamera => "Hidden_NoCamera",
                RelayMarkerVisibilityState.Hidden_NoRouteTarget => "Hidden_NoRouteTarget",
                RelayMarkerVisibilityState.Hidden_TooFar => "Hidden_TooFar",
                RelayMarkerVisibilityState.Hidden_BehindCamera => "Hidden_BehindCamera",
                RelayMarkerVisibilityState.Hidden_NoPlayerAup => "Hidden_NoPlayerAup",
                RelayMarkerVisibilityState.Visible_OnScreen => "Visible_OnScreen",
                RelayMarkerVisibilityState.Visible_ClampedToEdge => "Visible_ClampedToEdge",
                _ => "Hidden_NoRouteTarget"
            };
#else
            return string.Empty;
#endif
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            if (!_isVisible)
            {
                _hiddenPollTimer -= math.max(0f, dt);
                if (_hiddenPollTimer > 0f)
                    return;

                _hiddenPollTimer = HiddenPollInterval;
            }

            if (!TryCacheCamera(dt))
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

            if (!ReferenceEquals(_trackedRelay, routeTarget))
                _hasPositionState = false;

            _trackedRelay = routeTarget;

            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
            {
                _lastVisibilityState = RelayMarkerVisibilityState.Hidden_NoPlayerAup;
                SetVisible(false);
                return;
            }

            AbsoluteUniversePosition relayAup = routeTarget.RelayAup;
            double maxDisplayDistanceSq = (double)maxDisplayDistance * maxDisplayDistance;
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in playerAup, in relayAup);
            if (distanceSq > maxDisplayDistanceSq)
            {
                _lastObservedDistance = maxDisplayDistance + 1f;
                _lastVisibilityState = RelayMarkerVisibilityState.Hidden_TooFar;
                SetVisible(false);
                return;
            }

            float3 relayRuntime = relayAup.ToRuntimeFloat3();
            Vector3 relayPosition = new Vector3(relayRuntime.x, relayRuntime.y, relayRuntime.z);
            Vector3 screenPosition = _mainCamera.WorldToScreenPoint(relayPosition);
            bool behindCamera = screenPosition.z < 0f;
            if (behindCamera && hideWhenBehindCamera)
            {
                _lastVisibilityState = RelayMarkerVisibilityState.Hidden_BehindCamera;
                SetVisible(false);
                return;
            }

            bool clampedToEdge = behindCamera;
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;
            if (behindCamera)
            {
                screenPosition.x = screenWidth - screenPosition.x;
                screenPosition.y = screenHeight - screenPosition.y;
            }

            float minX = screenMargin;
            float maxX = screenWidth - screenMargin;
            float minY = screenMargin;
            float maxY = screenHeight - screenMargin;

            if (screenPosition.x < minX || screenPosition.x > maxX || screenPosition.y < minY || screenPosition.y > maxY)
            {
                clampedToEdge = true;
                screenPosition.x = math.clamp(screenPosition.x, minX, maxX);
                screenPosition.y = math.clamp(screenPosition.y, minY, maxY);
            }

            float distance = ApproximateDistanceMetersFromSq(distanceSq);
            _lastObservedDistance = distance;

            ApplyScreenPosition(screenPosition);
            UpdateLabel(routeTarget.RelayLabel);
            UpdateDistance(distance);
            UpdateColor(clampedToEdge);
            _lastVisibilityState = clampedToEdge
                ? RelayMarkerVisibilityState.Visible_ClampedToEdge
                : RelayMarkerVisibilityState.Visible_OnScreen;
            SetVisible(true);
        }

        private bool TryCacheCamera(float dt)
        {
            if (_mainCamera != null && _mainCamera.isActiveAndEnabled)
                return TryCachePlayerMovement();

            _mainCamera = null;

            _cameraRetryTimer -= math.max(0f, dt);
            if (_cameraRetryTimer > 0f)
                return false;

            _cameraRetryTimer = CameraRetryInterval;

            // Resolve via registry-owned player hierarchy.
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null)
            {
                _mainCamera = playerContext.PlayerCamera;
                _playerMovement = playerContext.PlayerMovement;
                if (_mainCamera != null && _playerMovement != null)
                    return true;
            }

            return false;
        }

        private bool TryCachePlayerMovement()
        {
            if (_playerMovement != null)
                return true;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null)
                _playerMovement = playerContext.PlayerMovement;

            return _playerMovement != null;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            if (TryCachePlayerMovement())
            {
                playerAup = _playerMovement.CurrentAup;
                return true;
            }

            playerAup = default;
            return false;
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

        private void UpdateLabel(string relayLabel)
        {
            if (labelText == null)
                return;

            int displayLength = ResolveLabelDisplayLength(relayLabel, _labelBuffer);
            bool truncated = IsLabelTruncated(relayLabel, displayLength, _labelBuffer);
            uint displayHash = ComputeLabelDisplayHash(relayLabel, displayLength, truncated);
            if (_hasLabelState &&
                _lastLabelLength == displayLength &&
                _lastLabelHash == displayHash &&
                LabelBufferMatches(relayLabel, displayLength, truncated))
            {
                return;
            }

            WriteLabelToBuffer(relayLabel, _labelBuffer, displayLength, truncated);
            labelText.SetCharArray(_labelBuffer, 0, displayLength);
            _lastLabelLength = displayLength;
            _lastLabelHash = displayHash;
            _hasLabelState = true;
        }

        private void UpdateDistance(float distance)
        {
            if (distanceText == null)
                return;

            int distanceMeters = (int)math.round(distance);
            if (_lastDistanceMeters == distanceMeters)
                return;

            _lastDistanceMeters = distanceMeters;
            int length = WriteNonNegativeInt(distanceMeters, _distanceBuffer);

            if (length < _distanceBuffer.Length)
            {
                _distanceBuffer[length] = 'M';
                length++;
            }

            distanceText.SetCharArray(_distanceBuffer, 0, length);
        }

        private static int WriteNonNegativeInt(int value, char[] buffer)
        {
            if (buffer == null || buffer.Length <= 0)
                return 0;

            int safeValue = math.max(0, value);
            return safeValue.TryFormat(new System.Span<char>(buffer), out int length)
                ? length
                : 0;
        }

        private bool LabelBufferMatches(string relayLabel, int displayLength, bool truncated)
        {
            for (int i = 0; i < displayLength; i++)
            {
                if (_labelBuffer[i] != ResolveLabelDisplayChar(relayLabel, i, displayLength, truncated))
                    return false;
            }

            return true;
        }

        private static int ResolveLabelDisplayLength(string relayLabel, char[] destination)
        {
            if (string.IsNullOrEmpty(relayLabel) || destination == null || destination.Length == 0)
                return 0;

            return math.min(relayLabel.Length, destination.Length);
        }

        private static bool IsLabelTruncated(string relayLabel, int displayLength, char[] destination)
        {
            return relayLabel != null &&
                   destination != null &&
                   relayLabel.Length > destination.Length &&
                   displayLength >= 3;
        }

        private static void WriteLabelToBuffer(string relayLabel, char[] destination, int displayLength, bool truncated)
        {
            for (int i = 0; i < displayLength; i++)
                destination[i] = ResolveLabelDisplayChar(relayLabel, i, displayLength, truncated);
        }

        private static uint ComputeLabelDisplayHash(string relayLabel, int displayLength, bool truncated)
        {
            uint hash = LabelHashSeed;
            for (int i = 0; i < displayLength; i++)
            {
                hash ^= ResolveLabelDisplayChar(relayLabel, i, displayLength, truncated);
                hash *= LabelHashPrime;
            }

            return hash;
        }

        private static char ResolveLabelDisplayChar(string relayLabel, int index, int displayLength, bool truncated)
        {
            if (truncated && index >= displayLength - 3)
                return '.';

            return relayLabel[index];
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

        private void ApplyScreenPosition(Vector3 screenPosition)
        {
            if (_rectTransform == null)
                return;

            int pixelX = (int)math.round(screenPosition.x);
            int pixelY = (int)math.round(screenPosition.y);
            if (_hasPositionState &&
                _lastPixelX == pixelX &&
                _lastPixelY == pixelY)
            {
                return;
            }

            _hasPositionState = true;
            _lastPixelX = pixelX;
            _lastPixelY = pixelY;
            screenPosition.x = pixelX;
            screenPosition.y = pixelY;
            _rectTransform.position = screenPosition;
        }

        private void SetVisible(bool visible)
        {
            if (_canvasGroup == null)
                return;

            if (_hasVisibilityState && _isVisible == visible)
                return;

            _hasVisibilityState = true;
            _isVisible = visible;
            if (visible)
                _hiddenPollTimer = 0f;
            else
                _hasPositionState = false;
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
        }
    }
}
