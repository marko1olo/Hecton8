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
    /// Canvas-space waypoint overlay that projects world targets into the HUD.
    /// Built-in sources include the active relay route target and abyssal anchors.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/AR Waypoint Overlay")]
    public sealed class ARWaypointOverlay : MonoBehaviour, ITickable, ISlowTickable
    {
        private const int MaxAnchorWaypoints = 12;
        private const int MaxExternalWaypoints = 8;
        private const int MaxWaypoints = 1 + MaxAnchorWaypoints + MaxExternalWaypoints;
        private const float ScreenMargin = 54f;
        private const float HiddenAlpha = 0f;
        private const float VisibleAlpha = 0.96f;
        private const float OccludedAlpha = 0.32f;
        private const float EdgeAlpha = 0.74f;
        private const float MarkerSize = 18f;
        private const float OutlineSize = 26f;
        private const string RootName = "ARWaypointOverlay";
        private const string DefaultRelayLabel = "SERVICE RELAY";
        private const string DefaultAnchorLabel = "ABYSSAL ANCHOR";

        private static readonly Color RelayColor = new Color(0.64f, 0.94f, 0.98f, 0.96f);
        private static readonly Color AnchorColor = new Color(0.98f, 0.74f, 0.22f, 0.96f);
        private static readonly Color OccludedColor = new Color(0.94f, 0.94f, 0.94f, 0.62f);
        private static readonly System.Collections.Generic.List<SuitHUDV4CanvasOverlay> s_overlayResolveBuffer =
            new System.Collections.Generic.List<SuitHUDV4CanvasOverlay>(2);
        private static Sprite s_quadSprite;
        private static ARWaypointOverlay s_instance;

        private struct ExternalWaypoint
        {
            public int Id;
            public Transform Target;
            public Vector3 WorldPosition;
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
            public Image Fill;
            public Image Outline;
            public TextMeshProUGUI Label;
            public string CachedLabel;
            public bool CachedOutlineState;
        }

        // COLD ALLOC: ExternalWaypoint[8] — external AR waypoint registry — owner: ARWaypointOverlay
        private readonly ExternalWaypoint[] _externalWaypoints = new ExternalWaypoint[MaxExternalWaypoints];
        // COLD ALLOC: RuntimeWaypoint[21] — current projected waypoint payloads — owner: ARWaypointOverlay
        private readonly RuntimeWaypoint[] _runtimeWaypoints = new RuntimeWaypoint[MaxWaypoints];
        // COLD ALLOC: WaypointSlot[21] — prebuilt AR waypoint UI pool — owner: ARWaypointOverlay
        private readonly WaypointSlot[] _slots = new WaypointSlot[MaxWaypoints];
        // COLD ALLOC: RaycastHit[1] — AR waypoint occlusion query buffer — owner: ARWaypointOverlay
        private readonly RaycastHit[] _occlusionHits = new RaycastHit[1];

        [SerializeField] private LayerMask occlusionMask = ~0;

        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _uiBuilt;
        private int _waypointCount;
        private Canvas _targetCanvas;
        private RectTransform _root;
        private Camera _viewCamera;
        private Transform _playerTransform;
        private HectonMapMagicVegetationBridge _vegetationBridge;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_instance = null;
            s_quadSprite = null;
        }

        /// <summary>
        /// Register or refresh an external world-space waypoint.
        /// </summary>
        public static void SetWaypoint(int id, Transform target, string label, Color color)
        {
            if (s_instance == null)
                return;

            s_instance.SetExternalWaypointInternal(id, target, default, useTransform: true, label, color);
        }

        /// <summary>
        /// Register or refresh an external static-position waypoint.
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

            UnregisterFromTickManager();
            UnregisterFromSlowTickManager();
            HideAllSlots();
        }

        private void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;

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

        private void ResolveOwners()
        {
            if (_vegetationBridge == null)
                _vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;

            if (_targetCanvas == null)
                _targetCanvas = ResolveTargetCanvas();

            if (_viewCamera == null)
            {
                if (TryGetComponent(out Camera localCamera))
                    _viewCamera = localCamera;
                else if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) && playerTransform != null)
                    _viewCamera = playerTransform.GetComponentInChildren<Camera>(true);
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
                _slots[i] = CreateSlot(i, _root);

            _uiBuilt = true;
        }

        private void CollectRuntimeWaypoints()
        {
            int count = 0;
            EmergencyServiceRelayDirector relayDirector = EmergencyServiceRelayDirector.Instance;
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
                ExternalWaypoint external = _externalWaypoints[i];
                if (!external.Active)
                    continue;

                if (external.UseTransform && external.Target == null)
                {
                    _externalWaypoints[i].Active = false;
                    continue;
                }

                _runtimeWaypoints[count] = new RuntimeWaypoint
                {
                    WorldPosition = external.UseTransform ? external.Target.position : external.WorldPosition,
                    Label = string.IsNullOrEmpty(external.Label) ? "WAYPOINT" : external.Label,
                    Color = external.Color.a <= 0f ? RelayColor : external.Color,
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

            float halfWidth = Mathf.Max(1f, (_root.rect.width * 0.5f) - ScreenMargin);
            float halfHeight = Mathf.Max(1f, (_root.rect.height * 0.5f) - ScreenMargin);

            for (int i = 0; i < _waypointCount; i++)
            {
                RuntimeWaypoint waypoint = _runtimeWaypoints[i];
                if (!waypoint.Active)
                {
                    HideSlot(i);
                    continue;
                }

                Vector3 viewport = _viewCamera.WorldToViewportPoint(waypoint.WorldPosition);
                Vector2 anchoredPosition;
                bool useEdgeClamp;
                if (viewport.z > 0f &&
                    viewport.x >= 0f && viewport.x <= 1f &&
                    viewport.y >= 0f && viewport.y <= 1f)
                {
                    anchoredPosition = new Vector2(
                        (viewport.x - 0.5f) * _root.rect.width,
                        (viewport.y - 0.5f) * _root.rect.height);
                    useEdgeClamp = false;
                }
                else
                {
                    anchoredPosition = ResolveEdgeClampedPosition(waypoint.WorldPosition, halfWidth, halfHeight);
                    useEdgeClamp = true;
                }

                WaypointSlot slot = _slots[i];
                if (slot.Root == null || slot.Group == null)
                    continue;

                slot.Root.anchoredPosition = anchoredPosition;
                slot.Root.localRotation = Quaternion.identity;

                bool useOutline = waypoint.Occluded;
                float alpha = waypoint.Occluded ? OccludedAlpha : (useEdgeClamp ? EdgeAlpha : VisibleAlpha);
                slot.Group.alpha = alpha;
                slot.Fill.color = useOutline ? Color.clear : waypoint.Color;
                slot.Outline.color = useOutline ? OccludedColor : new Color(waypoint.Color.r, waypoint.Color.g, waypoint.Color.b, 0.22f);
                slot.Outline.enabled = true;
                slot.Fill.enabled = !useOutline;

                if (!string.Equals(slot.CachedLabel, waypoint.Label, System.StringComparison.Ordinal))
                {
                    slot.Label.SetText(waypoint.Label);
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

        private Vector2 ResolveEdgeClampedPosition(Vector3 worldPosition, float halfWidth, float halfHeight)
        {
            Vector3 local = _viewCamera.transform.InverseTransformPoint(worldPosition);
            Vector2 planar = new Vector2(local.x, local.y);
            if (local.z < 0f)
                planar = -planar;

            if (planar.sqrMagnitude <= 0.0001f)
                planar = Vector2.up;

            Vector2 direction = planar.normalized;
            float tx = Mathf.Abs(direction.x) > 0.0001f ? halfWidth / Mathf.Abs(direction.x) : float.MaxValue;
            float ty = Mathf.Abs(direction.y) > 0.0001f ? halfHeight / Mathf.Abs(direction.y) : float.MaxValue;
            float distance = Mathf.Min(tx, ty);
            return direction * distance;
        }

        private void SetExternalWaypointInternal(int id, Transform target, Vector3 worldPosition, bool useTransform, string label, Color color)
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
                WorldPosition = worldPosition,
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
            if (_registeredTick)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register((ITickable)this);
            _registeredTick = true;
        }

        private void RegisterToSlowTickManager()
        {
            if (_registeredSlowTick)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register((ISlowTickable)this);
            _registeredSlowTick = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredTick)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister((ITickable)this);

            _registeredTick = false;
        }

        private void UnregisterFromSlowTickManager()
        {
            if (!_registeredSlowTick)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister((ISlowTickable)this);

            _registeredSlowTick = false;
        }

        private static Canvas ResolveTargetCanvas()
        {
            SuitHUDV4CanvasOverlay.CopyActiveOverlaysTo(s_overlayResolveBuffer);
            for (int i = 0; i < s_overlayResolveBuffer.Count; i++)
            {
                SuitHUDV4CanvasOverlay overlay = s_overlayResolveBuffer[i];
                if (overlay != null && overlay.TargetCanvas != null)
                {
                    s_overlayResolveBuffer.Clear();
                    return overlay.TargetCanvas;
                }
            }

            s_overlayResolveBuffer.Clear();
            return Object.FindAnyObjectByType<Canvas>();
        }

        private static WaypointSlot CreateSlot(int index, RectTransform parent)
        {
            GameObject rootObject = new GameObject("Waypoint_" + index, typeof(RectTransform), typeof(CanvasGroup));
            rootObject.layer = parent.gameObject.layer;
            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(120f, 42f);

            CanvasGroup group = rootObject.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Image fill = CreateImage(root, "Fill", MarkerSize, RelayColor);
            Image outline = CreateImage(root, "Outline", OutlineSize, OccludedColor);
            outline.enabled = true;

            TextMeshProUGUI label = CreateLabel(root);
            return new WaypointSlot
            {
                Root = root,
                Group = group,
                Fill = fill,
                Outline = outline,
                Label = label,
                CachedLabel = string.Empty,
                CachedOutlineState = false
            };
        }

        private static Image CreateImage(RectTransform parent, string name, float size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);

            Image image = go.GetComponent<Image>();
            image.sprite = ResolveQuadSprite();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateLabel(RectTransform parent)
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
            return label;
        }

        private static RectTransform FindExistingChild(Transform parent, string childName)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                    return child as RectTransform;
            }

            return null;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (Application.isPlaying)
                    Object.Destroy(child.gameObject);
                else
                    Object.DestroyImmediate(child.gameObject);
            }
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
