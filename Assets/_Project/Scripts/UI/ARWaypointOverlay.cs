using System;
using System.Collections.Generic;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.World;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Projects pooled waypoint markers onto the diegetic HUD plane using explicit camera-plane math.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/AR Waypoint Overlay")]
    public sealed class ARWaypointOverlay : MonoBehaviour, ITickable, ISlowTickable, IOriginShiftListener
    {
        private const int MaxAnchorWaypoints = 7;
        private const int MaxExternalWaypoints = 8;
        private const int MaxWaypoints = 1 + MaxAnchorWaypoints + MaxExternalWaypoints;
        private const int MaximumLabelCharacters = 48;
        private const float ScreenMargin = 54f;
        private const float HiddenAlpha = 0f;
        private const float VisibleAlpha = 0.96f;
        private const float OccludedAlpha = 0.32f;
        private const float EdgeAlpha = 0.74f;
        private const float MarkerSize = 18f;
        private const float OutlineSize = 26f;
        private const float EdgeMarkerWidth = 22f;
        private const float EdgeMarkerHeight = 10f;
        private const float EdgeOutlineWidth = 30f;
        private const float EdgeOutlineHeight = 16f;
        private const float ProjectionDepthEpsilon = 0.0001f;
        private const string RootName = "ARWaypointOverlay";
        private const string DefaultRelayLabel = "SERVICE RELAY";
        private const string DefaultAnchorLabel = "ABYSSAL ANCHOR";
        private const string DefaultExternalLabel = "WAYPOINT";

        private static readonly Color RelayColor = new Color(0.64f, 0.94f, 0.98f, 0.96f);
        private static readonly Color AnchorColor = new Color(0.98f, 0.74f, 0.22f, 0.96f);
        private static readonly Color OccludedColor = new Color(0.94f, 0.94f, 0.94f, 0.62f);
        private static readonly List<SuitHUDV4CanvasOverlay> s_overlayResolveBuffer =
            new List<SuitHUDV4CanvasOverlay>(4);
        private static readonly List<RectTransform> s_directChildBuffer =
            new List<RectTransform>(32);

        private static Sprite s_quadSprite;
        private static ARWaypointOverlay s_instance;

        private struct ExternalWaypoint
        {
            public int Id;
            public Transform Target;
            public Vector3 RuntimeWorldPosition;
            public Vector3 CapturedTotalOffset;
            public string Label;
            public Color Color;
            public bool Active;
            public bool UseTransform;
        }

        private struct RuntimeWaypoint
        {
            public Vector3 WorldPosition;
            public string Label;
            public Color Color;
            public bool Active;
            public bool Occluded;
        }

        private struct WaypointSlot
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public RectTransform FillRect;
            public RectTransform OutlineRect;
            public Image Fill;
            public Image Outline;
            public TextMeshProUGUI Label;
            public string CachedLabel;
            public bool CachedEdgeState;
        }

        // COLD ALLOC: ExternalWaypoint[8] — external AR waypoint registry — owner: ARWaypointOverlay
        private readonly ExternalWaypoint[] _externalWaypoints = new ExternalWaypoint[MaxExternalWaypoints];
        // COLD ALLOC: RuntimeWaypoint[16] — projected waypoint payloads — owner: ARWaypointOverlay
        private readonly RuntimeWaypoint[] _runtimeWaypoints = new RuntimeWaypoint[MaxWaypoints];
        // COLD ALLOC: WaypointSlot[16] — pooled waypoint UI markers — owner: ARWaypointOverlay
        private readonly WaypointSlot[] _slots = new WaypointSlot[MaxWaypoints];
        // COLD ALLOC: RaycastHit[1] — waypoint occlusion query buffer — owner: ARWaypointOverlay
        private readonly RaycastHit[] _occlusionHits = new RaycastHit[1];
        // COLD ALLOC: char[48] — transient zero-GC waypoint label formatter buffer — owner: ARWaypointOverlay
        private readonly char[] _labelCharBuffer = new char[MaximumLabelCharacters];

        [SerializeField]
        private LayerMask occlusionMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _uiBuilt;
        private int _waypointCount;
        private Canvas _targetCanvas;
        private RectTransform _targetCanvasRect;
        private RectTransform _root;
        private Camera _viewCamera;
        private Transform _playerTransform;
        private HectonMapMagicVegetationBridge _vegetationBridge;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_instance = null;
            s_quadSprite = null;
            s_overlayResolveBuffer.Clear();
            s_directChildBuffer.Clear();
        }

        /// <summary>
        /// Register or refresh an external waypoint bound to a transform target.
        /// </summary>
        public static void SetWaypoint(int id, Transform target, string label, Color color)
        {
            if (s_instance == null)
                return;

            s_instance.SetExternalWaypointInternal(id, target, default, useTransform: true, label, color);
        }

        /// <summary>
        /// Register or refresh an external waypoint bound to a runtime-space position.
        /// </summary>
        public static void SetWaypoint(int id, Vector3 worldPosition, string label, Color color)
        {
            if (s_instance == null)
                return;

            s_instance.SetExternalWaypointInternal(id, null, worldPosition, useTransform: false, label, color);
        }

        /// <summary>
        /// Remove a previously registered external waypoint.
        /// </summary>
        public static void ClearWaypoint(int id)
        {
            if (s_instance == null)
                return;

            s_instance.ClearExternalWaypointInternal(id);
        }

        private void OnEnable()
        {
            if (s_instance == null || s_instance == this)
                s_instance = this;

            ResolveOwners();
            EnsureUiBuilt();
            HectonFloatingOrigin.RegisterListener(this);
            RegisterToTickManager();
            RegisterToSlowTickManager();
        }

        private void Start()
        {
            RegisterToTickManager();
            RegisterToSlowTickManager();
        }

        private void OnDisable()
        {
            if (s_instance == this)
                s_instance = null;

            HectonFloatingOrigin.UnregisterListener(this);
            UnregisterFromTickManager();
            UnregisterFromSlowTickManager();
            HideAllSlots();
        }

        private void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;

            HectonFloatingOrigin.UnregisterListener(this);
            UnregisterFromTickManager();
            UnregisterFromSlowTickManager();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            ResolveOwners();
            EnsureUiBuilt();
            CollectRuntimeWaypoints();
            RenderWaypoints();
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            ResolveOwners();
            CollectRuntimeWaypoints();
            RefreshOcclusionStates();
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            for (int i = 0; i < _externalWaypoints.Length; i++)
            {
                ExternalWaypoint externalWaypoint = _externalWaypoints[i];
                if (!externalWaypoint.Active || externalWaypoint.UseTransform)
                    continue;

                externalWaypoint.RuntimeWorldPosition = shiftData.RebaseCapturedRuntimePosition(
                    externalWaypoint.RuntimeWorldPosition,
                    externalWaypoint.CapturedTotalOffset);
                externalWaypoint.CapturedTotalOffset = shiftData.NewTotalOffset;
                _externalWaypoints[i] = externalWaypoint;
            }

            _targetCanvas = null;
            _targetCanvasRect = null;
            _viewCamera = null;
            _uiBuilt = false;
            _root = null;
        }

        private void ResolveOwners()
        {
            if (_vegetationBridge == null)
                _vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;

            SuitHUDV4CanvasOverlay overlay = ResolveProjectionOverlay();
            if (overlay != null)
            {
                Canvas overlayCanvas = overlay.TargetCanvas;
                if (!ReferenceEquals(_targetCanvas, overlayCanvas))
                {
                    _targetCanvas = overlayCanvas;
                    _targetCanvasRect = _targetCanvas != null ? _targetCanvas.transform as RectTransform : null;
                    _uiBuilt = false;
                    _root = null;
                }

                Camera overlayCamera = overlay.ProjectionCamera != null ? overlay.ProjectionCamera : (_targetCanvas != null ? _targetCanvas.worldCamera : null);
                if (overlayCamera != null)
                    _viewCamera = overlayCamera;
            }

            if (_targetCanvas == null)
            {
                _targetCanvas = ResolveTargetCanvas();
                _targetCanvasRect = _targetCanvas != null ? _targetCanvas.transform as RectTransform : null;
            }

            if (_viewCamera == null)
            {
                if (_targetCanvas != null && _targetCanvas.worldCamera != null)
                    _viewCamera = _targetCanvas.worldCamera;
                else if (TryGetComponent(out Camera localCamera))
                    _viewCamera = localCamera;
                else if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) && playerTransform != null)
                    _viewCamera =
                        (GlobalRegistry.Player != null && GlobalRegistry.Player.PlayerCamera != null)
                            ? GlobalRegistry.Player.PlayerCamera
                            : playerTransform.GetComponent<Camera>();
            }

            if (_playerTransform == null)
                SceneBootstrap.TryGetCurrentPlayerTransform(out _playerTransform);
        }

        private void EnsureUiBuilt()
        {
            if (_uiBuilt || _targetCanvas == null)
                return;

            RectTransform canvasRoot = HectonUIScaler.ResolveContentRoot(_targetCanvas);
            if (canvasRoot == null)
                return;

            _root = FindExistingChild(canvasRoot, RootName);
            if (_root == null)
            {
                // COLD ALLOC: GameObject[1] — AR waypoint root canvas-space owner — owner: ARWaypointOverlay
                GameObject rootObject = new GameObject(RootName, typeof(RectTransform));
                rootObject.layer = canvasRoot.gameObject.layer;
                _root = rootObject.GetComponent<RectTransform>();
                _root.SetParent(canvasRoot, false);
            }

            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.SetAsLastSibling();

            ClearChildren(_root);
            for (int i = 0; i < _slots.Length; i++)
                _slots[i] = CreateSlot(i, _root, _viewCamera);

            _uiBuilt = true;
        }

        private void CollectRuntimeWaypoints()
        {
            int count = 0;

            EmergencyServiceRelayDirector relayDirector = Hecton8.Core.GlobalRegistry.EmergencyRelay;
            EmergencyServiceRelay relayTarget = relayDirector != null ? relayDirector.GetActiveRouteTarget() : null;
            if (relayTarget != null && relayTarget.isActiveAndEnabled && count < _runtimeWaypoints.Length)
            {
                _runtimeWaypoints[count] = new RuntimeWaypoint
                {
                    WorldPosition = relayTarget.transform.position,
                    Label = DefaultRelayLabel,
                    Color = RelayColor,
                    Active = true,
                    Occluded = count < _waypointCount && _runtimeWaypoints[count].Occluded
                };
                count++;
            }

            if (_vegetationBridge != null &&
                _vegetationBridge.TryGetActiveAbyssalAnchorPayload(out NativeArray<Vector3> anchors, out int anchorCount) &&
                anchors.IsCreated &&
                anchorCount > 0)
            {
                int visibleAnchors = Mathf.Min(MaxAnchorWaypoints, anchorCount);
                for (int i = 0; i < visibleAnchors && count < _runtimeWaypoints.Length; i++)
                {
                    _runtimeWaypoints[count] = new RuntimeWaypoint
                    {
                        WorldPosition = anchors[i],
                        Label = DefaultAnchorLabel,
                        Color = AnchorColor,
                        Active = true,
                        Occluded = count < _waypointCount && _runtimeWaypoints[count].Occluded
                    };
                    count++;
                }
            }

            for (int i = 0; i < _externalWaypoints.Length && count < _runtimeWaypoints.Length; i++)
            {
                ExternalWaypoint externalWaypoint = _externalWaypoints[i];
                if (!externalWaypoint.Active)
                    continue;

                if (externalWaypoint.UseTransform)
                {
                    if (externalWaypoint.Target == null)
                    {
                        externalWaypoint.Active = false;
                        _externalWaypoints[i] = externalWaypoint;
                        continue;
                    }

                    externalWaypoint.RuntimeWorldPosition = externalWaypoint.Target.position;
                }

                _runtimeWaypoints[count] = new RuntimeWaypoint
                {
                    WorldPosition = externalWaypoint.RuntimeWorldPosition,
                    Label = string.IsNullOrEmpty(externalWaypoint.Label) ? DefaultExternalLabel : externalWaypoint.Label,
                    Color = externalWaypoint.Color.a <= 0f ? RelayColor : externalWaypoint.Color,
                    Active = true,
                    Occluded = count < _waypointCount && _runtimeWaypoints[count].Occluded
                };
                count++;
            }

            for (int i = count; i < _runtimeWaypoints.Length; i++)
                _runtimeWaypoints[i].Active = false;

            _waypointCount = count;
        }

        private void RenderWaypoints()
        {
            if (_root == null || _viewCamera == null || _waypointCount <= 0)
            {
                HideAllSlots();
                return;
            }

            for (int i = 0; i < _waypointCount; i++)
            {
                RuntimeWaypoint waypoint = _runtimeWaypoints[i];
                if (!waypoint.Active)
                {
                    HideSlot(i);
                    continue;
                }

                if (!TryProjectWaypointOntoHudPlane(
                        waypoint.WorldPosition,
                        out Vector2 anchoredPosition,
                        out Vector2 clampDirection,
                        out bool clampedToEdge,
                        out float visibility01))
                {
                    HideSlot(i);
                    continue;
                }

                if (visibility01 <= 0.0001f)
                {
                    HideSlot(i);
                    continue;
                }

                WaypointSlot slot = _slots[i];
                if (slot.Root == null || slot.Group == null || slot.Fill == null || slot.Outline == null || slot.Label == null)
                    continue;

                slot.Root.anchoredPosition = anchoredPosition;
                slot.Root.localRotation = clampedToEdge
                    ? Quaternion.Euler(0f, 0f, Mathf.Atan2(clampDirection.y, clampDirection.x) * Mathf.Rad2Deg - 90f)
                    : Quaternion.identity;

                if (slot.CachedEdgeState != clampedToEdge)
                {
                    ApplySlotIconState(ref slot, clampedToEdge);
                    slot.CachedEdgeState = clampedToEdge;
                }

                bool useOutlineOnly = waypoint.Occluded;
                float alpha = waypoint.Occluded
                    ? visibility01 * OccludedAlpha
                    : visibility01 * (clampedToEdge ? EdgeAlpha : VisibleAlpha);

                slot.Group.alpha = alpha;
                slot.Fill.enabled = !useOutlineOnly;
                slot.Outline.enabled = true;
                slot.Fill.color = waypoint.Color;
                slot.Outline.color = useOutlineOnly
                    ? OccludedColor
                    : new Color(waypoint.Color.r, waypoint.Color.g, waypoint.Color.b, 0.22f);

                if (!string.Equals(slot.CachedLabel, waypoint.Label, StringComparison.Ordinal))
                {
                    ApplyLabelText(slot.Label, waypoint.Label);
                    slot.CachedLabel = waypoint.Label;
                }

                _slots[i] = slot;
            }

            for (int i = _waypointCount; i < _slots.Length; i++)
                HideSlot(i);
        }

        private void RefreshOcclusionStates()
        {
            if (_viewCamera == null)
                return;

            Vector3 origin = _viewCamera.transform.position;
            for (int i = 0; i < _waypointCount; i++)
            {
                RuntimeWaypoint waypoint = _runtimeWaypoints[i];
                if (!waypoint.Active)
                    continue;

                Vector3 delta = waypoint.WorldPosition - origin;
                float distance = delta.magnitude;
                if (distance <= 0.1f)
                {
                    waypoint.Occluded = false;
                    _runtimeWaypoints[i] = waypoint;
                    continue;
                }

                Vector3 direction = delta / distance;
                int hitCount = UnityEngine.Physics.RaycastNonAlloc(
                    origin,
                    direction,
                    _occlusionHits,
                    distance - 0.15f,
                    occlusionMask,
                    QueryTriggerInteraction.Ignore);

                waypoint.Occluded = hitCount > 0;
                _runtimeWaypoints[i] = waypoint;
            }
        }

        private bool TryProjectWaypointOntoHudPlane(
            Vector3 worldPosition,
            out Vector2 anchoredPosition,
            out Vector2 clampDirection,
            out bool clampedToEdge,
            out float visibility01)
        {
            anchoredPosition = Vector2.zero;
            clampDirection = Vector2.up;
            clampedToEdge = false;
            visibility01 = 0f;

            if (_viewCamera == null || _root == null || _targetCanvasRect == null)
                return false;

            Transform cameraTransform = _viewCamera.transform;
            Vector3 cameraPosition = cameraTransform.position;
            Vector3 delta = worldPosition - cameraPosition;
            Vector3 cameraForward = cameraTransform.forward;
            float viewDepth = Vector3.Dot(cameraForward, delta);
            float planeDistance = ResolveHudPlaneDistance(cameraTransform, _targetCanvasRect);
            if (planeDistance <= ProjectionDepthEpsilon)
                return false;

            float depthForProjection = Mathf.Abs(viewDepth) > ProjectionDepthEpsilon
                ? viewDepth
                : (viewDepth >= 0f ? ProjectionDepthEpsilon : -ProjectionDepthEpsilon);

            float projectedWorldX = Vector3.Dot(cameraTransform.right, delta) * (planeDistance / depthForProjection);
            float projectedWorldY = Vector3.Dot(cameraTransform.up, delta) * (planeDistance / depthForProjection);

            Vector3 lossyScale = _root.lossyScale;
            float scaleX = Mathf.Max(ProjectionDepthEpsilon, Mathf.Abs(lossyScale.x));
            float scaleY = Mathf.Max(ProjectionDepthEpsilon, Mathf.Abs(lossyScale.y));
            Vector2 projectedCanvasPosition = new Vector2(projectedWorldX / scaleX, projectedWorldY / scaleY);

            bool behindPlayer = viewDepth <= ProjectionDepthEpsilon;
            if (behindPlayer)
                projectedCanvasPosition = -projectedCanvasPosition;

            float halfWidth = Mathf.Max(1f, (_root.rect.width * 0.5f) - ScreenMargin);
            float halfHeight = Mathf.Max(1f, (_root.rect.height * 0.5f) - ScreenMargin);

            bool insideFrustum =
                !behindPlayer &&
                projectedCanvasPosition.x >= -halfWidth &&
                projectedCanvasPosition.x <= halfWidth &&
                projectedCanvasPosition.y >= -halfHeight &&
                projectedCanvasPosition.y <= halfHeight;

            if (insideFrustum)
            {
                anchoredPosition = projectedCanvasPosition;
                clampDirection = projectedCanvasPosition.sqrMagnitude > ProjectionDepthEpsilon
                    ? projectedCanvasPosition.normalized
                    : Vector2.up;
                visibility01 = 1f;
                return true;
            }

            clampDirection = projectedCanvasPosition.sqrMagnitude > ProjectionDepthEpsilon
                ? projectedCanvasPosition.normalized
                : Vector2.up;

            float tx = Mathf.Abs(clampDirection.x) > ProjectionDepthEpsilon
                ? halfWidth / Mathf.Abs(clampDirection.x)
                : float.MaxValue;
            float ty = Mathf.Abs(clampDirection.y) > ProjectionDepthEpsilon
                ? halfHeight / Mathf.Abs(clampDirection.y)
                : float.MaxValue;

            anchoredPosition = clampDirection * Mathf.Min(tx, ty);
            clampedToEdge = true;
            visibility01 = behindPlayer ? 0f : 1f;
            return true;
        }

        private void SetExternalWaypointInternal(
            int id,
            Transform target,
            Vector3 worldPosition,
            bool useTransform,
            string label,
            Color color)
        {
            int freeIndex = -1;
            for (int i = 0; i < _externalWaypoints.Length; i++)
            {
                if (_externalWaypoints[i].Active && _externalWaypoints[i].Id == id)
                {
                    freeIndex = i;
                    break;
                }

                if (freeIndex < 0 && !_externalWaypoints[i].Active)
                    freeIndex = i;
            }

            if (freeIndex < 0)
                return;

            _externalWaypoints[freeIndex] = new ExternalWaypoint
            {
                Id = id,
                Target = target,
                RuntimeWorldPosition = worldPosition,
                CapturedTotalOffset = HectonFloatingOrigin.CurrentTotalOffset,
                Label = label,
                Color = color,
                Active = true,
                UseTransform = useTransform
            };
        }

        private void ClearExternalWaypointInternal(int id)
        {
            for (int i = 0; i < _externalWaypoints.Length; i++)
            {
                if (_externalWaypoints[i].Active && _externalWaypoints[i].Id == id)
                {
                    _externalWaypoints[i].Active = false;
                    break;
                }
            }
        }

        private void HideAllSlots()
        {
            for (int i = 0; i < _slots.Length; i++)
                HideSlot(i);
        }

        private void HideSlot(int index)
        {
            if (index < 0 || index >= _slots.Length)
                return;

            WaypointSlot slot = _slots[index];
            if (slot.Group == null)
                return;

            if (slot.Group.alpha > HiddenAlpha)
                slot.Group.alpha = HiddenAlpha;
        }

        private void RegisterToTickManager()
        {
            if (_registeredTick || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registeredTick = true;
        }

        private void RegisterToSlowTickManager()
        {
            if (_registeredSlowTick || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.UI);
            _registeredSlowTick = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registeredTick = false;
        }

        private void UnregisterFromSlowTickManager()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI);
            _registeredSlowTick = false;
        }

        private static SuitHUDV4CanvasOverlay ResolveProjectionOverlay()
        {
            SuitHUDV4CanvasOverlay.CopyActiveOverlaysTo(s_overlayResolveBuffer);
            for (int i = 0; i < s_overlayResolveBuffer.Count; i++)
            {
                SuitHUDV4CanvasOverlay overlay = s_overlayResolveBuffer[i];
                if (overlay == null || overlay.TargetCanvas == null)
                    continue;

                Canvas targetCanvas = overlay.TargetCanvas;
                if (targetCanvas.renderMode == RenderMode.WorldSpace && overlay.ProjectionCamera != null)
                {
                    s_overlayResolveBuffer.Clear();
                    return overlay;
                }
            }

            s_overlayResolveBuffer.Clear();
            return null;
        }

        private static Canvas ResolveTargetCanvas()
        {
            SuitHUDV4CanvasOverlay overlay = ResolveProjectionOverlay();
            if (overlay != null)
                return overlay.TargetCanvas;

            return SuitHUDV4CanvasOverlay.ActiveRuntimeInstance != null
                ? SuitHUDV4CanvasOverlay.ActiveRuntimeInstance.TargetCanvas
                : null;
        }

        private static WaypointSlot CreateSlot(int index, RectTransform parent, Camera camera)
        {
            GameObject rootObject = new GameObject($"Waypoint_{index}", typeof(RectTransform), typeof(CanvasGroup));
            rootObject.layer = parent.gameObject.layer;
            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(120f, 42f);

            CanvasGroup group = rootObject.GetComponent<CanvasGroup>();
            group.alpha = HiddenAlpha;
            group.blocksRaycasts = false;
            group.interactable = false;

            Image fill = CreateImage(root, "Fill", MarkerSize, MarkerSize, RelayColor, out RectTransform fillRect);
            Image outline = CreateImage(root, "Outline", OutlineSize, OutlineSize, OccludedColor, out RectTransform outlineRect);
            outline.enabled = true;

            TextMeshProUGUI label = CreateLabel(root, camera);
            return new WaypointSlot
            {
                Root = root,
                Group = group,
                FillRect = fillRect,
                OutlineRect = outlineRect,
                Fill = fill,
                Outline = outline,
                Label = label,
                CachedLabel = string.Empty,
                CachedEdgeState = false
            };
        }

        private static Image CreateImage(
            RectTransform parent,
            string name,
            float width,
            float height,
            Color color,
            out RectTransform rect)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;
            rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);

            Image image = go.GetComponent<Image>();
            image.sprite = ResolveQuadSprite();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateLabel(RectTransform parent, Camera camera)
        {
            GameObject go = new GameObject("Label", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 14f);
            rect.sizeDelta = new Vector2(132f, 20f);

            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>(); // COLD ALLOC: TextMeshProUGUI[1] — AR waypoint label owner — owner: ARWaypointOverlay
            label.font = LocalizedFontResolver.ResolveReadableFont(null);
            label.fontSize = 11f;
            label.alignment = TextAlignmentOptions.Bottom;
            label.color = new Color(0.90f, 0.96f, 0.94f, 0.92f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            TMP_TextRegistry.EnsureRegistered(label);

            WorldSpaceTMPSharpnessController sharpnessController = go.AddComponent<WorldSpaceTMPSharpnessController>(); // COLD ALLOC: WorldSpaceTMPSharpnessController[1] — per-label world-space SDF sharpness owner — owner: ARWaypointOverlay
            sharpnessController.Bind(label, camera);
            return label;
        }

        private void ApplyLabelText(TextMeshProUGUI label, string value)
        {
            if (label == null)
                return;

            int length = CopyLabelToBuffer(value, _labelCharBuffer);
            label.SetCharArray(_labelCharBuffer, 0, length);
        }

        private static int CopyLabelToBuffer(string value, char[] destination)
        {
            if (destination == null || destination.Length == 0)
                return 0;

            if (string.IsNullOrEmpty(value))
            {
                destination[0] = '\0';
                return 0;
            }

            int length = Mathf.Min(value.Length, destination.Length);
            for (int i = 0; i < length; i++)
                destination[i] = value[i];

            return length;
        }

        private static void ApplySlotIconState(ref WaypointSlot slot, bool edgeState)
        {
            if (slot.FillRect != null)
            {
                slot.FillRect.sizeDelta = edgeState
                    ? new Vector2(EdgeMarkerWidth, EdgeMarkerHeight)
                    : new Vector2(MarkerSize, MarkerSize);
            }

            if (slot.OutlineRect != null)
            {
                slot.OutlineRect.sizeDelta = edgeState
                    ? new Vector2(EdgeOutlineWidth, EdgeOutlineHeight)
                    : new Vector2(OutlineSize, OutlineSize);
            }
        }

        private static float ResolveHudPlaneDistance(Transform cameraTransform, RectTransform canvasRect)
        {
            if (cameraTransform == null || canvasRect == null)
                return 0f;

            return Mathf.Max(
                ProjectionDepthEpsilon,
                Vector3.Dot(cameraTransform.forward, canvasRect.position - cameraTransform.position));
        }

        private static RectTransform FindExistingChild(Transform parent, string childName)
        {
            if (parent == null)
                return null;

            s_directChildBuffer.Clear();
            parent.GetComponentsInChildren(includeInactive: true, result: s_directChildBuffer);
            for (int i = 0; i < s_directChildBuffer.Count; i++)
            {
                RectTransform child = s_directChildBuffer[i];
                if (child == null || !ReferenceEquals(child.parent, parent))
                    continue;

                if (string.Equals(child.name, childName, StringComparison.Ordinal))
                {
                    s_directChildBuffer.Clear();
                    return child;
                }
            }

            s_directChildBuffer.Clear();
            return null;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
                return;

            s_directChildBuffer.Clear();
            parent.GetComponentsInChildren(includeInactive: true, result: s_directChildBuffer);
            for (int i = s_directChildBuffer.Count - 1; i >= 0; i--)
            {
                RectTransform child = s_directChildBuffer[i];
                if (child == null || !ReferenceEquals(child.parent, parent))
                    continue;

                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }

            s_directChildBuffer.Clear();
        }

        private static Sprite ResolveQuadSprite()
        {
            if (s_quadSprite != null)
                return s_quadSprite;

            s_quadSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            s_quadSprite.name = "ARWaypointQuad";
            return s_quadSprite;
        }
    }
}
