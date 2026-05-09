using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Hecton8.UI;
using Hecton8.World;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.PDA
{
    /// <summary>
    /// HUD presenter for player-authored PDA markers when no dedicated compass owner exists.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/PDA/PDA Marker HUD Element")]
    public sealed class PDAMarkerHUDElement : MonoBehaviour, ITickable, IUpdatable, IPDAEventListener
    {
        private sealed class MarkerIconDisplay
        {
            public const int TitleBufferCapacity = 128;
            public const int DistanceBufferCapacity = 16;

            public RectTransform rectTransform;
            public CanvasGroup canvasGroup;
            public Image iconImage;
            public TMP_Text titleText;
            public TMP_Text distanceText;
            public uint cachedTitleHash;
            public int cachedDistanceMeters;
            public MarkerIconType cachedIconType;
            public byte cachedCanvasAlpha;
            public byte cachedColorAlpha;
            public bool cachedVisible;
            public bool hasCanvasState;
            public char[] titleBuffer;
            public char[] distanceBuffer;
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

        private static readonly Color ResourceMarkerColor = new Color(0.20f, 0.85f, 0.55f, 1f);
        private static readonly Color HazardMarkerColor = new Color(1.00f, 0.34f, 0.22f, 1f);
        private static readonly Color ShelterMarkerColor = new Color(0.42f, 0.72f, 1.00f, 1f);
        private static readonly Color ObjectiveMarkerColor = new Color(1.00f, 0.84f, 0.30f, 1f);
        private static readonly Color VehicleMarkerColor = new Color(0.74f, 0.64f, 1.00f, 1f);
        private static readonly Color BeaconMarkerColor = new Color(0.30f, 1.00f, 1.00f, 1f);
        private static readonly Color GenericMarkerColor = new Color(0.90f, 0.95f, 1.00f, 1f);
        // COLD ALLOC: List<Graphic>[8] - temporary prefab graphic raycast pruning scratch - owner: PDAMarkerHUDElement
        private static readonly List<Graphic> s_GraphicRaycastDisableScratch = new List<Graphic>(8);

        // COLD ALLOC: MarkerIconDisplay[64] - UI marker pool - owner: PDAMarkerHUDElement
        private readonly MarkerIconDisplay[] _iconDisplays = new MarkerIconDisplay[PDAMarkerRegistryDTO.MaxEntries];
        // COLD ALLOC: PDAMarkerSnapshot[64] - registry snapshot buffer - owner: PDAMarkerHUDElement
        private readonly PDAMarkerSnapshot[] _markerBuffer = new PDAMarkerSnapshot[PDAMarkerRegistryDTO.MaxEntries];

        private Camera _mainCamera;
        private bool _registeredToTick;
        private bool _registeredToPDAEvents;
        private bool _markersDirty = true;
        private int _markerCount;
        private int _activeDisplayCount;
        private float _cameraRetryTimer;

        private void Awake()
        {
            BuildIconPool();
        }

        private void OnEnable()
        {
            TryRegisterWithTickManager();
            TryRegisterWithPDAEvents();
            _markersDirty = true;
            _cameraRetryTimer = 0f;
        }

        private void OnDisable()
        {
            UnregisterFromPDAEvents();
            UnregisterFromTickManager();
            HideAllDisplays();
        }

        private void OnDestroy()
        {
            UnregisterFromPDAEvents();
            UnregisterFromTickManager();
            PDAEvents.AssertUnregistered(this, nameof(PDAMarkerHUDElement));
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            float safeDeltaTime = math.max(0f, deltaTime);
            _cameraRetryTimer = math.max(0f, _cameraRetryTimer - safeDeltaTime);

            PDAMarkerRegistry markerRegistry = GlobalRegistry.PDAMarkers;
            if (markerRegistry == null)
            {
                HideAllDisplays();
                return;
            }

            if (_markersDirty)
            {
                _markerCount = markerRegistry.CopyMarkers(_markerBuffer, hudOnly: true);
                _markersDirty = false;
            }

            if (_markerCount <= 0)
            {
                HideAllDisplays();
                return;
            }

            if (!TryResolveCamera())
            {
                HideAllDisplays();
                return;
            }

            if (!TryResolveObserverAup(out AbsoluteUniversePosition observerAup))
            {
                HideAllDisplays();
                return;
            }

            int displayCount = math.min(_markerCount, _iconDisplays.Length);
            double maxDisplayDistanceSq = (double)maxDisplayDistance * maxDisplayDistance;
            double fadeStartDistanceSq = (double)fadeStartDistance * fadeStartDistance;
            double fadeDistanceSqSpan = math.max(0.001d, maxDisplayDistanceSq - fadeStartDistanceSq);
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            for (int i = 0; i < displayCount; i++)
            {
                UpdateDisplay(
                    _iconDisplays[i],
                    _markerBuffer[i],
                    in observerAup,
                    maxDisplayDistanceSq,
                    fadeStartDistanceSq,
                    fadeDistanceSqSpan,
                    screenWidth,
                    screenHeight);
            }

            for (int i = displayCount; i < _activeDisplayCount; i++)
                SetDisplayVisible(_iconDisplays[i], false);

            _activeDisplayCount = displayCount;
        }

        /// <inheritdoc />
        public void OnPDAEvent(in PDAEventPayload payload)
        {
            if ((PDAEventType)payload.EventType == PDAEventType.MarkerChanged)
            {
                _markersDirty = true;
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

                DisableGraphicRaycasts(iconObject);

                MarkerIconDisplay display = new MarkerIconDisplay
                {
                    rectTransform = rectTransform,
                    canvasGroup = canvasGroup,
                    iconImage = iconObject.GetComponent<Image>(),
                    titleText = ResolveChildText(iconObject.transform, "Label"),
                    distanceText = ResolveChildText(iconObject.transform, "Distance"),
                    cachedTitleHash = uint.MaxValue,
                    cachedDistanceMeters = int.MinValue,
                    cachedIconType = (MarkerIconType)byte.MaxValue,
                    cachedCanvasAlpha = byte.MaxValue,
                    cachedColorAlpha = byte.MaxValue,
                    titleBuffer = new char[MarkerIconDisplay.TitleBufferCapacity], // COLD ALLOC: char[128] — per-marker HUD title staging buffer for zero-GC TMP writes — owner: PDAMarkerHUDElement
                    distanceBuffer = new char[MarkerIconDisplay.DistanceBufferCapacity] // COLD ALLOC: char[16] — per-marker HUD distance staging buffer for zero-GC TMP writes — owner: PDAMarkerHUDElement
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
            if (_cameraRetryTimer > 0f)
                return false;

            _cameraRetryTimer = CameraRetryInterval;
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null)
                _mainCamera = playerContext.PlayerCamera;

            return _mainCamera != null && _mainCamera.isActiveAndEnabled;
        }

        private bool TryResolveObserverAup(out AbsoluteUniversePosition observerAup)
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null && playerContext.PlayerMovement != null)
            {
                observerAup = playerContext.PlayerMovement.CurrentAup;
                return true;
            }

            observerAup = default;
            return false;
        }

        private void UpdateDisplay(
            MarkerIconDisplay display,
            PDAMarkerSnapshot marker,
            in AbsoluteUniversePosition observerAup,
            double maxDisplayDistanceSq,
            double fadeStartDistanceSq,
            double fadeDistanceSqSpan,
            float screenWidth,
            float screenHeight)
        {
            if (display == null || display.rectTransform == null || display.canvasGroup == null || _mainCamera == null)
                return;

            AbsoluteUniversePosition markerAup = marker.PositionAup;
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in markerAup, in observerAup);
            if (distanceSq > maxDisplayDistanceSq)
            {
                SetDisplayVisible(display, false);
                return;
            }

            float3 markerRuntime = markerAup.ToRuntimeFloat3();
            Vector3 screenPoint = _mainCamera.WorldToScreenPoint(new Vector3(markerRuntime.x, markerRuntime.y, markerRuntime.z));
            if (screenPoint.z <= 0f)
            {
                SetDisplayVisible(display, false);
                return;
            }

            float clampedX = math.clamp(screenPoint.x, screenMargin, screenWidth - screenMargin);
            float clampedY = math.clamp(screenPoint.y, screenMargin, screenHeight - screenMargin);
            Vector3 iconPosition = display.rectTransform.position;
            iconPosition.x = clampedX;
            iconPosition.y = clampedY;
            iconPosition.z = 0f;
            display.rectTransform.position = iconPosition;

            float alpha = 1f;
            if (distanceSq > fadeStartDistanceSq)
                alpha = 1f - math.saturate((float)((distanceSq - fadeStartDistanceSq) / fadeDistanceSqSpan));

            byte alphaByte = QuantizeAlpha(alpha);
            if (alphaByte == 0)
            {
                SetDisplayVisible(display, false);
                return;
            }

            ApplyDisplayState(display, true, alphaByte);

            if (display.titleText != null)
            {
                uint nextTitleHash = showLabels ? marker.TitleHashID : 0u;
                if (display.cachedTitleHash != nextTitleHash)
                {
                    int titleLength = showLabels ? marker.CopyTitleTo(display.titleBuffer) : 0;
                    display.titleText.SetCharArray(display.titleBuffer, 0, titleLength);
                    display.cachedTitleHash = nextTitleHash;
                }
            }

            if (display.distanceText != null)
            {
                int nextDistanceMeters = showDistance ? (int)math.round(ApproximateDistanceMetersFromSq(distanceSq)) : -1;
                if (display.cachedDistanceMeters != nextDistanceMeters)
                {
                    int distanceLength = showDistance
                        ? WriteDistanceLabel(nextDistanceMeters, display.distanceBuffer)
                        : 0;
                    display.distanceText.SetCharArray(display.distanceBuffer, 0, distanceLength);
                    display.cachedDistanceMeters = nextDistanceMeters;
                }
            }

            if (display.iconImage != null)
                ApplyMarkerColor(display, marker.IconType, alphaByte);
        }

        private static Color ResolveMarkerColor(MarkerIconType iconType, float alpha)
        {
            Color color;
            switch (iconType)
            {
                case MarkerIconType.Resource:
                    color = ResourceMarkerColor;
                    break;
                case MarkerIconType.Hazard:
                    color = HazardMarkerColor;
                    break;
                case MarkerIconType.Shelter:
                    color = ShelterMarkerColor;
                    break;
                case MarkerIconType.Objective:
                    color = ObjectiveMarkerColor;
                    break;
                case MarkerIconType.Vehicle:
                    color = VehicleMarkerColor;
                    break;
                case MarkerIconType.Beacon:
                    color = BeaconMarkerColor;
                    break;
                default:
                    color = GenericMarkerColor;
                    break;
            }

            color.a = math.saturate(alpha);
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
            ApplyDisplayState(display, visible, visible ? (byte)255 : (byte)0);
        }

        private static void ApplyDisplayState(MarkerIconDisplay display, bool visible, byte alphaByte)
        {
            if (display == null || display.canvasGroup == null)
                return;

            if (display.hasCanvasState &&
                display.cachedVisible == visible &&
                display.cachedCanvasAlpha == alphaByte)
            {
                return;
            }

            display.hasCanvasState = true;
            display.cachedVisible = visible;
            display.cachedCanvasAlpha = alphaByte;
            display.canvasGroup.alpha = visible ? DecodeAlpha(alphaByte) : 0f;
            if (display.canvasGroup.blocksRaycasts)
                display.canvasGroup.blocksRaycasts = false;
            if (display.canvasGroup.interactable)
                display.canvasGroup.interactable = false;
        }

        private static void ApplyMarkerColor(MarkerIconDisplay display, MarkerIconType iconType, byte alphaByte)
        {
            if (display == null || display.iconImage == null)
                return;

            if (display.cachedIconType == iconType && display.cachedColorAlpha == alphaByte)
                return;

            display.cachedIconType = iconType;
            display.cachedColorAlpha = alphaByte;
            display.iconImage.color = ResolveMarkerColor(iconType, DecodeAlpha(alphaByte));
        }

        private static byte QuantizeAlpha(float alpha)
        {
            int alphaInt = (int)math.round(math.saturate(alpha) * 255f);
            return (byte)math.clamp(alphaInt, 0, 255);
        }

        private static float DecodeAlpha(byte alphaByte)
        {
            return alphaByte * (1f / 255f);
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

        private static int WriteDistanceLabel(int meters, char[] buffer)
        {
            if (buffer == null || buffer.Length < 3)
                return 0;

            int clampedMeters = math.clamp(meters, 0, MaxCachedDistanceMeters);
            if (!clampedMeters.TryFormat(new System.Span<char>(buffer, 0, buffer.Length), out int cursor))
                return 0;

            if (cursor + 2 > buffer.Length)
                return cursor;

            buffer[cursor++] = ' ';
            buffer[cursor++] = 'm';
            return cursor;
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

        private void HideAllDisplays()
        {
            int count = math.min(_activeDisplayCount, _iconDisplays.Length);
            for (int i = 0; i < count; i++)
                SetDisplayVisible(_iconDisplays[i], false);

            _activeDisplayCount = 0;
        }

        private void TryRegisterWithTickManager()
        {
            if (_registeredToTick)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredToTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registeredToTick = false;
        }

        private void TryRegisterWithPDAEvents()
        {
            if (_registeredToPDAEvents || !Application.isPlaying)
                return;

            PDAEvents.Register(this);
            _registeredToPDAEvents = PDAEvents.IsRegistered(this);
        }

        private void UnregisterFromPDAEvents()
        {
            if (!_registeredToPDAEvents)
                return;

            PDAEvents.Unregister(this);
            _registeredToPDAEvents = false;
        }
    }
}
