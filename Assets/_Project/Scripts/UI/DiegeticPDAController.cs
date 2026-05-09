using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.World;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Diegetic PDA Controller")]
    public sealed class DiegeticPDAController : MonoBehaviour, ITickable, IUpdatable, IPanelInteractable
    {
        private const string TabletScreenShaderPath = "Assets/_Project/Art/Shaders/Hecton_DiegeticPanelUnlit.shader";
        private const float ReferenceResolveRetryIntervalSeconds = 0.5f;
        private const float HoverRaycastPixelThresholdSq = 16f;
        private const int PointerTargetCapacity = 256;
        private const int PointerDiscoveryCapacity = 512;
        private static readonly bool EnableGraphicRaycasterFallback = false;
        private static readonly string[] s_defaultTabNames =
        {
            "Tab_Inventory",
            "Tab_Loadout",
            "Tab_Construction",
            "Tab_Barter",
            "Tab_DataLog",
            "Tab_Spectrum",
            "Tab_AtlasSignal",
            "Tab_Diagnostics"
        };

        [Header("References")]
        [SerializeField, Tooltip("Existing player PDA shell that owns the tab logic and open/close state.")]
        private PlayerPDA playerPda;
        [SerializeField, Tooltip("Existing diegetic panel presenter that renders the PDA canvas into a physical tablet screen.")]
        private DiegeticPanelController diegeticPanel;
        [SerializeField, Tooltip("World-space tablet root toggled while the PDA is open.")]
        private GameObject tabletRoot;
        [SerializeField, Tooltip("Optional hand anchor used to keep the tablet model bound to the player hand.")]
        private Transform tabletHandAnchor;
        [SerializeField, Tooltip("Optional explicit world-space PDA canvas root routed into the tablet render texture.")]
        private GameObject diegeticPanelRoot;
        [SerializeField, Tooltip("CanvasGroup on the diegetic PDA root used for show/hide without Canvas rebuild spam.")]
        private CanvasGroup diegeticPanelCanvasGroup;
        [SerializeField, Tooltip("Optional explicit unlit material used by the PDA screen mesh so the panel remains emissive in caves.")]
        private Material tabletScreenUnlitMaterial;
        [SerializeField, Tooltip("Optional explicit unlit shader used when no authored PDA screen material is assigned.")]
        private Shader tabletScreenUnlitShader;
        [SerializeField, Tooltip("Optional explicit renderer used for the PDA screen mesh.")]
        private Renderer tabletScreenRenderer;

        [Header("Behavior")]
        [SerializeField, Tooltip("Reparents the tablet root to the resolved hand anchor during cold initialization.")]
        private bool reparentTabletToHandAnchorOnAwake = true;
        [SerializeField, Tooltip("Disables the physical tablet object when the PDA is closed.")]
        private bool hideTabletWhenClosed = true;
        [SerializeField, Tooltip("Fixed PDA render-texture resolution retained across panel disable/enable. VR cap is 512x512.")]
        private Vector2Int tabletRenderTextureResolution = new Vector2Int(512, 512);
        [SerializeField, Tooltip("Optional anchor used for PDA camera visibility and AUP distance checks. Defaults to the tablet root.")]
        private Transform pdaVisibilityAnchor;
        [SerializeField, Min(0.5f), Tooltip("Maximum AUP-safe camera distance before the PDA screen camera is paused.")]
        private float activeCameraDistanceMeters = 6f;
        [SerializeField, Range(-0.2f, 0.8f), Tooltip("Minimum camera-forward dot against camera-to-PDA direction before the PDA RT is allowed to update.")]
        private float cameraFrustumDotThreshold = 0.08f;
        private const bool PausePanelCameraWhenCulled = true;

        // COLD ALLOC: GameObject[8] — diegetic PDA tab routing cache — owner: DiegeticPDAController
        [SerializeField] private GameObject[] configuredTabs = new GameObject[8];
        // COLD ALLOC: List<RaycastResult>(16) — reusable diegetic PDA UI hit cache — owner: DiegeticPDAController
        private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>(16);
        // COLD ALLOC: List<Renderer>[8] — tablet visual visibility cache — owner: DiegeticPDAController
        private readonly List<Renderer> _tabletRenderers = new List<Renderer>(8);
        // COLD ALLOC: List<Collider>[4] — tablet collision visibility cache — owner: DiegeticPDAController
        private readonly List<Collider> _tabletColliders = new List<Collider>(4);
        // COLD ALLOC: List<CanvasGroup>[4] — tablet UI visibility cache — owner: DiegeticPDAController
        private readonly List<CanvasGroup> _tabletCanvasGroups = new List<CanvasGroup>(4);
        // COLD ALLOC: List<MonoBehaviour>(512) — PDA pointer target discovery scratch — owner: DiegeticPDAController
        private readonly List<MonoBehaviour> _pointerHandlerScratch = new List<MonoBehaviour>(PointerDiscoveryCapacity);
        // COLD ALLOC: MonoBehaviour[256] — cached PDA pointer dispatch components — owner: DiegeticPDAController
        private readonly MonoBehaviour[] _pointerTargetHandlers = new MonoBehaviour[PointerTargetCapacity];
        // COLD ALLOC: RectTransform[256] — cached PDA pointer bounds — owner: DiegeticPDAController
        private readonly RectTransform[] _pointerTargetRects = new RectTransform[PointerTargetCapacity];
        // COLD ALLOC: GameObject[256] — cached PDA pointer target game objects — owner: DiegeticPDAController
        private readonly GameObject[] _pointerTargetObjects = new GameObject[PointerTargetCapacity];
        // COLD ALLOC: CanvasGroup[256] — cached PDA pointer visibility gates — owner: DiegeticPDAController
        private readonly CanvasGroup[] _pointerTargetCanvasGroups = new CanvasGroup[PointerTargetCapacity];
        // COLD ALLOC: Selectable[256] — cached PDA pointer interactable gates — owner: DiegeticPDAController
        private readonly Selectable[] _pointerTargetSelectables = new Selectable[PointerTargetCapacity];
        // COLD ALLOC: Graphic[256] — cached PDA pointer draw-depth hints — owner: DiegeticPDAController
        private readonly Graphic[] _pointerTargetGraphics = new Graphic[PointerTargetCapacity];

        private bool _registeredToTickManager;
        private bool _uiConfigured;
        private bool _lastOpenState;
        private bool _lastPresentationActive;
        private bool _tabletVisibilityInitialized;
        private bool _tabletVisible;
        private GameObject _cachedTabletRoot;
        private Camera _visibilityCamera;
        private Canvas _panelCanvas;
        private GraphicRaycaster _panelGraphicRaycaster;
        private EventSystem _eventSystem;
        private PointerEventData _pointerEventData;
        private GameObject _hoverTarget;
        private GameObject _pressedTarget;
        private GameObject _dragTarget;
        private GameObject _pointerTargetRoot;
        private float2 _lastPointerRaycastCanvasPosition;
        private bool _dragInProgress;
        private bool _hasLastPointerRaycastCanvasPosition;
        private bool _pointerTargetCacheDirty = true;
        private bool _pointerTargetCacheOverflow;
        private int _pointerTargetCount;
        private Material _runtimeTabletScreenMaterial;
        private float _referenceResolveRetryTimer;

        private void Awake()
        {
            CharBufferPool.Prewarm();
            ResolveReferences();
            ConfigureDiegeticPdaShell();
            ApplyPresentationState(PlayerPDA.IsOpen, force: true);
        }

        private void OnEnable()
        {
            ResolveReferences();
            _referenceResolveRetryTimer = 0f;
            ConfigureDiegeticPdaShell();
            RegisterToTickManager();
        }

        private void OnDisable()
        {
            UnregisterFromTickManager();
            _uiConfigured = false;
            ClearPointerState();
            ClearPointerTargetCache();
            _pointerTargetRoot = null;
            _pointerTargetCacheDirty = true;
            ApplyPresentationCullState(false, false, force: true);
            DisposeRuntimeScreenMaterial();
        }

        private void OnDestroy()
        {
            if (diegeticPanel != null)
                diegeticPanel.ReleasePresentationRenderTexture();
            DisposeRuntimeScreenMaterial();
        }

        private void OnApplicationQuit()
        {
            if (diegeticPanel != null)
                diegeticPanel.ReleasePresentationRenderTexture();
        }

        public void Tick(float deltaTime)
        {
            float safeDeltaTime = math.max(0f, deltaTime);
            bool openState = PlayerPDA.IsOpen;
            if (!openState)
            {
                if (openState != _lastOpenState)
                    ApplyPresentationState(false, force: true);
                else
                    ApplyPresentationCullState(false, false, force: false);
                return;
            }

            if (openState != _lastOpenState)
                _referenceResolveRetryTimer = 0f;

            ResolveReferencesThrottled(safeDeltaTime);
            ConfigureDiegeticPdaShell();

            if (openState != _lastOpenState)
                ApplyPresentationState(openState, force: true);

            ApplyPresentationCullState(openState, IsPdaVisibleToCamera(openState), force: false);
        }

        public void ReceiveCanvasInput(in DiegeticPanelInputEvent inputEvent)
        {
            if (!PlayerPDA.IsOpen || !_lastPresentationActive || !EnsureUiInteractionState(allowColdCreateFallback: false))
                return;

            if (_pressedTarget == null &&
                (inputEvent.EventType == DiegeticPanelInputEventType.Hold ||
                 inputEvent.EventType == DiegeticPanelInputEventType.Up))
            {
                return;
            }

            if (inputEvent.EventType == DiegeticPanelInputEventType.Hover &&
                _pressedTarget == null &&
                !_dragInProgress &&
                _hasLastPointerRaycastCanvasPosition &&
                math.lengthsq(inputEvent.CanvasHitPoint - _lastPointerRaycastCanvasPosition) <= HoverRaycastPixelThresholdSq)
            {
                return;
            }

            GameObject hitTarget = ResolvePanelHitTarget(inputEvent.CanvasHitPoint);
            UpdateHoverTarget(hitTarget);

            switch (inputEvent.EventType)
            {
                case DiegeticPanelInputEventType.Down:
                    HandlePointerDown(hitTarget);
                    break;
                case DiegeticPanelInputEventType.Up:
                    HandlePointerUp(hitTarget);
                    break;
                case DiegeticPanelInputEventType.Hold:
                    HandlePointerHold();
                    break;
            }
        }

        private void RegisterToTickManager()
        {
            if (_registeredToTickManager || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredToTickManager = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registeredToTickManager = false;
        }

        private void ResolveReferencesThrottled(float deltaTime)
        {
            if (!NeedsReferenceResolve())
            {
                _referenceResolveRetryTimer = 0f;
                return;
            }

            _referenceResolveRetryTimer = math.max(0f, _referenceResolveRetryTimer - deltaTime);
            if (_referenceResolveRetryTimer > 0f)
                return;

            _referenceResolveRetryTimer = ReferenceResolveRetryIntervalSeconds;
            ResolveReferences();
        }

        private bool NeedsReferenceResolve()
        {
            if (playerPda == null ||
                diegeticPanel == null ||
                diegeticPanelRoot == null ||
                diegeticPanelCanvasGroup == null ||
                _panelCanvas == null ||
                (EnableGraphicRaycasterFallback && _panelGraphicRaycaster == null))
            {
                return true;
            }

            if (tabletRoot != null && tabletScreenRenderer == null)
                return true;

            if (reparentTabletToHandAnchorOnAwake && tabletRoot != null && tabletHandAnchor == null)
                return true;

            return tabletRoot != null &&
                   tabletHandAnchor != null &&
                   reparentTabletToHandAnchorOnAwake &&
                   tabletRoot.transform.parent != tabletHandAnchor;
        }

        private void ResolveReferences()
        {
            if (playerPda == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                if (playerContext != null)
                    playerPda = playerContext.PlayerPDA;

                if (playerPda == null)
                    playerPda = GetComponentInParent<PlayerPDA>();
            }

            if (diegeticPanel == null)
                diegeticPanel = GetComponentInChildren<DiegeticPanelController>(true);

            if (diegeticPanelRoot == null)
            {
                if (diegeticPanelCanvasGroup != null)
                    diegeticPanelRoot = diegeticPanelCanvasGroup.gameObject;
                else if (diegeticPanel != null)
                    diegeticPanelRoot = diegeticPanel.gameObject;
            }

            if (diegeticPanelCanvasGroup == null && diegeticPanelRoot != null)
                diegeticPanelCanvasGroup = diegeticPanelRoot.GetComponent<CanvasGroup>();

            if (diegeticPanel != null && _panelCanvas == null)
                _panelCanvas = diegeticPanel.TargetCanvas;

            if (_panelCanvas == null && diegeticPanelRoot != null)
                diegeticPanelRoot.TryGetComponent(out _panelCanvas);

            if (EnableGraphicRaycasterFallback && _panelGraphicRaycaster == null && _panelCanvas != null)
            {
                _panelCanvas.TryGetComponent(out _panelGraphicRaycaster);
                if (_panelGraphicRaycaster == null)
                    _panelGraphicRaycaster = _panelCanvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            if (!ReferenceEquals(_pointerTargetRoot, diegeticPanelRoot))
                InvalidatePointerTargetCache();

            if (tabletHandAnchor == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                PlayerToolManager toolManager = playerContext != null ? playerContext.ToolManager : null;
                if (toolManager != null)
                    tabletHandAnchor = toolManager.HandAnchor;
            }

            if (tabletScreenRenderer == null && tabletRoot != null)
                tabletScreenRenderer = tabletRoot.GetComponentInChildren<Renderer>(true);

            if (!ReferenceEquals(_cachedTabletRoot, tabletRoot))
                RebuildTabletVisibilityCache();

#if UNITY_EDITOR
            if (tabletScreenUnlitShader == null)
                tabletScreenUnlitShader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(TabletScreenShaderPath);
#endif

            if (tabletRoot != null &&
                tabletHandAnchor != null &&
                reparentTabletToHandAnchorOnAwake &&
                tabletRoot.transform.parent != tabletHandAnchor)
            {
                tabletRoot.transform.SetParent(tabletHandAnchor, false);
            }
        }

        private void ConfigureDiegeticPdaShell()
        {
            if (_uiConfigured || playerPda == null || diegeticPanelRoot == null || diegeticPanelCanvasGroup == null)
                return;

            EnsureTabRoutingCache(diegeticPanelRoot.transform);
            playerPda.ConfigureUI(diegeticPanelRoot, diegeticPanelCanvasGroup, configuredTabs);
            EnsureUiInteractionState(allowColdCreateFallback: true);
            RebuildPointerTargetCache();
            bool openState = PlayerPDA.IsOpen;
            bool presentationActive = IsPdaVisibleToCamera(openState);
            ApplyPresentationCullState(openState, presentationActive, force: true);
            if (diegeticPanel != null)
            {
                diegeticPanel.OverridePanelInteractable(this);
                diegeticPanel.OverrideInteractionMode(DiegeticPanelController.PanelInteractionMode.HybridPreferFinger);
                diegeticPanel.OverrideFixedRenderTextureResolution(SanitizeTabletResolution(tabletRenderTextureResolution), retainOnDisable: true);
                diegeticPanel.OverridePhosphorDecay(true, 0.85f);
                diegeticPanel.OverridePanelPresentation(ResolveTabletScreenMaterial(), tabletScreenRenderer);
            }

            if (diegeticPanel != null && presentationActive)
                diegeticPanel.ForceRefreshRenderTexture();

            _uiConfigured = true;
        }

        private void EnsureTabRoutingCache(Transform root)
        {
            if (configuredTabs == null || configuredTabs.Length != s_defaultTabNames.Length)
                configuredTabs = new GameObject[s_defaultTabNames.Length]; // COLD ALLOC: GameObject[8] — diegetic PDA tab routing cache resize — owner: DiegeticPDAController

            for (int tabIndex = 0; tabIndex < s_defaultTabNames.Length; tabIndex++)
            {
                if (configuredTabs[tabIndex] != null)
                    continue;

                Transform tabTransform = root.Find(s_defaultTabNames[tabIndex]);
                configuredTabs[tabIndex] = tabTransform != null ? tabTransform.gameObject : null;
            }
        }

        private void ApplyPresentationState(bool openState, bool force)
        {
            if (!force && _lastOpenState == openState)
                return;

            _lastOpenState = openState;
            bool presentationActive = IsPdaVisibleToCamera(openState);

            if (tabletRoot != null && hideTabletWhenClosed)
                SetTabletVisible(openState);

            if (diegeticPanel != null && diegeticPanel.enabled != openState)
                diegeticPanel.enabled = openState;

            ApplyPresentationCullState(openState, presentationActive, force: true);

            if (openState)
                InvalidatePointerTargetCache();
            else
                ClearPointerState();

            if (openState && presentationActive && diegeticPanel != null)
                diegeticPanel.ForceRefreshRenderTexture();
        }

        private void ApplyPresentationCullState(bool openState, bool visibleToCamera, bool force)
        {
            bool presentationActive = openState && visibleToCamera;
            if (!force && _lastPresentationActive == presentationActive)
                return;

            _lastPresentationActive = presentationActive;

            if (diegeticPanel != null && PausePanelCameraWhenCulled)
                diegeticPanel.SetPresentationPaused(!presentationActive);

            if (presentationActive)
                InvalidatePointerTargetCache();

            if (_panelGraphicRaycaster != null && _panelGraphicRaycaster.enabled)
                _panelGraphicRaycaster.enabled = false;

            if (diegeticPanelCanvasGroup != null)
            {
                diegeticPanelCanvasGroup.alpha = presentationActive ? 1f : 0f;
                diegeticPanelCanvasGroup.interactable = presentationActive;
                diegeticPanelCanvasGroup.blocksRaycasts = presentationActive;
            }

            if (!presentationActive)
                ClearPointerState();
        }

        private bool IsPdaVisibleToCamera(bool openState)
        {
            if (!openState)
                return false;

            Camera camera = ResolveVisibilityCamera();
            if (camera == null)
                return false;

            Transform cameraTransform = camera.transform;
            Transform anchor = ResolveVisibilityAnchor();
            if (cameraTransform == null || anchor == null)
                return false;

            Vector3 cameraPosition = cameraTransform.position;
            Vector3 anchorPosition = anchor.position;
            AbsoluteUniversePosition cameraAup = AbsoluteUniversePosition.FromRuntimePosition(cameraPosition);
            AbsoluteUniversePosition anchorAup = AbsoluteUniversePosition.FromRuntimePosition(anchorPosition);
            double maxDistanceSq = (double)activeCameraDistanceMeters * activeCameraDistanceMeters;
            if (AbsoluteUniversePosition.DistanceSq(in cameraAup, in anchorAup) > maxDistanceSq)
                return false;

            float3 toPda = (float3)(anchorPosition - cameraPosition);
            float distanceSq = math.lengthsq(toPda);
            if (distanceSq <= 0.0001f)
                return true;

            float cameraForwardDot = math.dot((float3)cameraTransform.forward, toPda);
            float frustumThreshold = cameraFrustumDotThreshold;
            float frustumThresholdSq = frustumThreshold * frustumThreshold;
            float forwardDotSq = cameraForwardDot * cameraForwardDot;

            // Squared cone gate: same normalized-dot threshold without the per-tick rsqrt.
            if (frustumThreshold <= 0f)
                return cameraForwardDot >= 0f || forwardDotSq <= distanceSq * frustumThresholdSq;

            return cameraForwardDot > 0f && forwardDotSq >= distanceSq * frustumThresholdSq;
        }

        private Transform ResolveVisibilityAnchor()
        {
            if (pdaVisibilityAnchor != null)
                return pdaVisibilityAnchor;

            if (tabletRoot != null)
                return tabletRoot.transform;

            return transform;
        }

        private Camera ResolveVisibilityCamera()
        {
            if (_visibilityCamera != null && _visibilityCamera.isActiveAndEnabled)
                return _visibilityCamera;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            _visibilityCamera = playerContext != null ? playerContext.PlayerCamera : null;
            return _visibilityCamera != null && _visibilityCamera.isActiveAndEnabled ? _visibilityCamera : null;
        }

        private void RebuildTabletVisibilityCache()
        {
            _cachedTabletRoot = tabletRoot;
            _tabletRenderers.Clear();
            _tabletColliders.Clear();
            _tabletCanvasGroups.Clear();
            _tabletVisibilityInitialized = false;

            if (tabletRoot == null)
                return;

            tabletRoot.GetComponentsInChildren(true, _tabletRenderers);
            tabletRoot.GetComponentsInChildren(true, _tabletColliders);
            tabletRoot.GetComponentsInChildren(true, _tabletCanvasGroups);
        }

        private void SetTabletVisible(bool visible)
        {
            if (tabletRoot == null || (_tabletVisibilityInitialized && _tabletVisible == visible))
                return;

            for (int i = 0; i < _tabletRenderers.Count; i++)
            {
                Renderer target = _tabletRenderers[i];
                if (target != null)
                    target.enabled = visible;
            }

            for (int i = 0; i < _tabletColliders.Count; i++)
            {
                Collider target = _tabletColliders[i];
                if (target != null)
                    target.enabled = visible;
            }

            for (int i = 0; i < _tabletCanvasGroups.Count; i++)
            {
                CanvasGroup target = _tabletCanvasGroups[i];
                if (target == null)
                    continue;

                target.alpha = visible ? 1f : 0f;
                target.interactable = visible;
                target.blocksRaycasts = visible;
            }

            _tabletVisible = visible;
            _tabletVisibilityInitialized = true;
        }

        private bool EnsureUiInteractionState(bool allowColdCreateFallback)
        {
            if (allowColdCreateFallback || _panelCanvas == null)
                ResolveReferences();

            if (_panelCanvas == null)
                return false;

            if (_eventSystem == null)
            {
                if (!allowColdCreateFallback)
                    return false;

                _eventSystem = EventSystem.current;
                if (_eventSystem == null && allowColdCreateFallback)
                {
                    GameObject eventSystemRoot = new GameObject("DiegeticPDA_EventSystem", typeof(EventSystem)); // COLD ALLOC: GameObject[1] — PDA pointer-dispatch fallback event system — owner: DiegeticPDAController
                    _eventSystem = eventSystemRoot.GetComponent<EventSystem>();
                }
            }

            if (_pointerEventData == null)
            {
                if (!allowColdCreateFallback || _eventSystem == null)
                    return false;

                _pointerEventData = new PointerEventData(_eventSystem); // COLD ALLOC: PointerEventData[1] — PDA pointer-dispatch cache — owner: DiegeticPDAController

            }

            return _eventSystem != null && _pointerEventData != null;
        }

        private GameObject ResolvePanelHitTarget(float2 canvasHitPoint)
        {
            PreparePointerEventData(canvasHitPoint);

            if (!_pointerTargetCacheOverflow && TryResolveCachedPointerTarget(canvasHitPoint, out GameObject cachedTarget))
            {
                _lastPointerRaycastCanvasPosition = canvasHitPoint;
                _hasLastPointerRaycastCanvasPosition = true;
                return cachedTarget;
            }

            if (!EnableGraphicRaycasterFallback)
            {
                _pointerEventData.pointerCurrentRaycast = default;
                _lastPointerRaycastCanvasPosition = canvasHitPoint;
                _hasLastPointerRaycastCanvasPosition = true;
                return null;
            }

            GameObject hitTarget = ResolvePanelHitTargetWithRaycaster();
            _lastPointerRaycastCanvasPosition = canvasHitPoint;
            _hasLastPointerRaycastCanvasPosition = true;
            return hitTarget;
        }

        private void PreparePointerEventData(float2 canvasHitPoint)
        {
            _raycastResults.Clear();
            _pointerEventData.Reset();
            _pointerEventData.position = new Vector2(canvasHitPoint.x, canvasHitPoint.y);
            _pointerEventData.delta = Vector2.zero;
            _pointerEventData.button = PointerEventData.InputButton.Left;
            _pointerEventData.scrollDelta = Vector2.zero;
            _pointerEventData.useDragThreshold = false;
        }

        private GameObject ResolvePanelHitTargetWithRaycaster()
        {
            if (_panelGraphicRaycaster == null)
                return null;

            bool wasRaycasterEnabled = _panelGraphicRaycaster.enabled;
            if (!wasRaycasterEnabled)
                _panelGraphicRaycaster.enabled = true;

            _panelGraphicRaycaster.Raycast(_pointerEventData, _raycastResults);
            GameObject hitTarget = _raycastResults.Count > 0 ? _raycastResults[0].gameObject : null;
            if (_raycastResults.Count > 0)
                _pointerEventData.pointerCurrentRaycast = _raycastResults[0];

            if (!wasRaycasterEnabled)
                _panelGraphicRaycaster.enabled = false;

            return hitTarget;
        }

        private bool TryResolveCachedPointerTarget(float2 canvasHitPoint, out GameObject hitTarget)
        {
            hitTarget = null;

            if (_pointerTargetCacheDirty)
                RebuildPointerTargetCache();

            if (_pointerTargetCount <= 0 || _pointerTargetCacheOverflow)
                return false;

            if (!TryCanvasHitPointToRootWorld(canvasHitPoint, out Vector3 worldPoint))
                return false;

            int bestIndex = -1;
            int bestDepth = int.MinValue;
            for (int i = 0; i < _pointerTargetCount; i++)
            {
                if (!IsCachedPointerTargetEnabled(i))
                    continue;

                RectTransform targetRect = _pointerTargetRects[i];
                Vector3 localPoint = targetRect.InverseTransformPoint(worldPoint);
                Rect rect = targetRect.rect;
                if (localPoint.x < rect.xMin ||
                    localPoint.x > rect.xMax ||
                    localPoint.y < rect.yMin ||
                    localPoint.y > rect.yMax)
                {
                    continue;
                }

                Graphic graphic = _pointerTargetGraphics[i];
                int drawDepth = graphic != null ? graphic.depth : 0;
                if (drawDepth > bestDepth || (drawDepth == bestDepth && i > bestIndex))
                {
                    bestDepth = drawDepth;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
                return false;

            hitTarget = _pointerTargetObjects[bestIndex];
            _pointerEventData.pointerCurrentRaycast = default;
            return hitTarget != null;
        }

        private bool IsCachedPointerTargetEnabled(int index)
        {
            MonoBehaviour handler = _pointerTargetHandlers[index];
            RectTransform rectTransform = _pointerTargetRects[index];
            GameObject target = _pointerTargetObjects[index];
            if (handler == null ||
                rectTransform == null ||
                target == null ||
                !handler.isActiveAndEnabled ||
                !target.activeInHierarchy)
            {
                return false;
            }

            CanvasGroup canvasGroup = _pointerTargetCanvasGroups[index];
            if (canvasGroup != null &&
                canvasGroup.isActiveAndEnabled &&
                (!canvasGroup.interactable || !canvasGroup.blocksRaycasts || canvasGroup.alpha <= 0.001f))
            {
                return false;
            }

            Selectable selectable = _pointerTargetSelectables[index];
            return selectable == null || selectable.IsInteractable();
        }

        private bool TryCanvasHitPointToRootWorld(float2 canvasHitPoint, out Vector3 worldPoint)
        {
            worldPoint = default;
            if (_panelCanvas == null)
                return false;

            RectTransform canvasRect = _panelCanvas.transform as RectTransform;
            if (canvasRect == null)
                return false;

            Rect rect = canvasRect.rect;
            if (rect.width <= 0.001f || rect.height <= 0.001f)
                return false;

            Vector2Int referenceResolution = diegeticPanel != null
                ? diegeticPanel.ReferenceResolutionPixels
                : SanitizeTabletResolution(tabletRenderTextureResolution);

            float2 uv = new float2(
                math.saturate(canvasHitPoint.x / math.max(1, referenceResolution.x)),
                math.saturate(canvasHitPoint.y / math.max(1, referenceResolution.y)));

            Vector3 localPoint = new Vector3(
                rect.xMin + (rect.width * uv.x),
                rect.yMin + (rect.height * uv.y),
                0f);
            worldPoint = canvasRect.TransformPoint(localPoint);
            return true;
        }

        private void RebuildPointerTargetCache()
        {
            ClearPointerTargetCache();
            _pointerTargetRoot = diegeticPanelRoot;
            _pointerTargetCacheDirty = false;

            if (diegeticPanelRoot == null)
                return;

            diegeticPanelRoot.GetComponentsInChildren(true, _pointerHandlerScratch);
            for (int i = 0; i < _pointerHandlerScratch.Count; i++)
            {
                MonoBehaviour handler = _pointerHandlerScratch[i];
                if (handler == null || !IsPointerDispatchTarget(handler))
                    continue;

                RectTransform handlerRect = handler.transform as RectTransform;
                if (handlerRect == null)
                    continue;

                GameObject target = handler.gameObject;
                bool duplicateTarget = false;
                for (int targetIndex = 0; targetIndex < _pointerTargetCount; targetIndex++)
                {
                    if (ReferenceEquals(_pointerTargetObjects[targetIndex], target))
                    {
                        duplicateTarget = true;
                        break;
                    }
                }

                if (duplicateTarget)
                    continue;

                if (_pointerTargetCount >= PointerTargetCapacity)
                {
                    _pointerTargetCacheOverflow = true;
                    break;
                }

                CanvasGroup canvasGroup = handler.GetComponentInParent<CanvasGroup>();
                handler.TryGetComponent(out Selectable selectable);
                handler.TryGetComponent(out Graphic graphic);

                _pointerTargetHandlers[_pointerTargetCount] = handler;
                _pointerTargetRects[_pointerTargetCount] = handlerRect;
                _pointerTargetObjects[_pointerTargetCount] = target;
                _pointerTargetCanvasGroups[_pointerTargetCount] = canvasGroup;
                _pointerTargetSelectables[_pointerTargetCount] = selectable;
                _pointerTargetGraphics[_pointerTargetCount] = graphic;
                _pointerTargetCount++;
            }

            _pointerHandlerScratch.Clear();
        }

        private void ClearPointerTargetCache()
        {
            for (int i = 0; i < _pointerTargetCount; i++)
            {
                _pointerTargetHandlers[i] = null;
                _pointerTargetRects[i] = null;
                _pointerTargetObjects[i] = null;
                _pointerTargetCanvasGroups[i] = null;
                _pointerTargetSelectables[i] = null;
                _pointerTargetGraphics[i] = null;
            }

            _pointerTargetCount = 0;
            _pointerTargetCacheOverflow = false;
            _pointerHandlerScratch.Clear();
        }

        private void InvalidatePointerTargetCache()
        {
            _pointerTargetCacheDirty = true;
        }

        private static bool IsPointerDispatchTarget(MonoBehaviour handler)
        {
            return handler is IPointerClickHandler ||
                   handler is IPointerDownHandler ||
                   handler is IPointerUpHandler ||
                   handler is IPointerEnterHandler ||
                   handler is IPointerExitHandler ||
                   handler is IDragHandler ||
                   handler is IBeginDragHandler ||
                   handler is IEndDragHandler ||
                   handler is IDropHandler;
        }

        private void UpdateHoverTarget(GameObject hitTarget)
        {
            if (ReferenceEquals(_hoverTarget, hitTarget))
                return;

            if (_hoverTarget != null)
                ExecuteEvents.ExecuteHierarchy(_hoverTarget, _pointerEventData, ExecuteEvents.pointerExitHandler);

            _hoverTarget = hitTarget;
            if (_hoverTarget != null)
                ExecuteEvents.ExecuteHierarchy(_hoverTarget, _pointerEventData, ExecuteEvents.pointerEnterHandler);
        }

        private void HandlePointerDown(GameObject hitTarget)
        {
            _pressedTarget = hitTarget;
            _dragTarget = null;
            _dragInProgress = false;
            if (_pressedTarget == null)
                return;

            if (_raycastResults.Count > 0)
                _pointerEventData.pointerPressRaycast = _raycastResults[0];
            _pointerEventData.pointerPress = ExecuteEvents.ExecuteHierarchy(_pressedTarget, _pointerEventData, ExecuteEvents.pointerDownHandler);
            _pointerEventData.rawPointerPress = hitTarget;
            _dragTarget = ExecuteEvents.GetEventHandler<IDragHandler>(_pressedTarget);
            _pointerEventData.pointerDrag = _dragTarget;
            if (_dragTarget != null)
                ExecuteEvents.Execute(_dragTarget, _pointerEventData, ExecuteEvents.initializePotentialDrag);
        }

        private void HandlePointerUp(GameObject hitTarget)
        {
            if (_pressedTarget == null)
                return;

            if (_dragInProgress && _pointerEventData.pointerDrag != null)
            {
                ExecuteEvents.Execute(_pointerEventData.pointerDrag, _pointerEventData, ExecuteEvents.endDragHandler);
                if (hitTarget != null)
                {
                    GameObject dropTarget = ExecuteEvents.GetEventHandler<IDropHandler>(hitTarget);
                    if (dropTarget != null)
                        ExecuteEvents.ExecuteHierarchy(dropTarget, _pointerEventData, ExecuteEvents.dropHandler);
                }
            }

            ExecuteEvents.ExecuteHierarchy(_pressedTarget, _pointerEventData, ExecuteEvents.pointerUpHandler);

            if (!_dragInProgress)
            {
                GameObject pressedClickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(_pressedTarget);
                GameObject releasedClickHandler = hitTarget != null ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(hitTarget) : null;
                if (pressedClickHandler != null && ReferenceEquals(pressedClickHandler, releasedClickHandler))
                    ExecuteEvents.ExecuteHierarchy(pressedClickHandler, _pointerEventData, ExecuteEvents.pointerClickHandler);
            }

            _pressedTarget = null;
            _dragTarget = null;
            _dragInProgress = false;
            _pointerEventData.pointerPress = null;
            _pointerEventData.rawPointerPress = null;
            _pointerEventData.pointerDrag = null;
            _pointerEventData.dragging = false;
        }

        private void HandlePointerHold()
        {
            if (_pressedTarget == null || _pointerEventData == null || _pointerEventData.pointerDrag == null)
                return;

            if (!_dragInProgress)
            {
                ExecuteEvents.Execute(_pointerEventData.pointerDrag, _pointerEventData, ExecuteEvents.beginDragHandler);
                _dragInProgress = true;
                _pointerEventData.dragging = true;
            }

            ExecuteEvents.Execute(_pointerEventData.pointerDrag, _pointerEventData, ExecuteEvents.dragHandler);
        }

        private void ClearPointerState()
        {
            if (_pressedTarget != null && _pointerEventData != null)
                CancelActivePointerGesture();

            if (_hoverTarget != null && _pointerEventData != null)
                ExecuteEvents.ExecuteHierarchy(_hoverTarget, _pointerEventData, ExecuteEvents.pointerExitHandler);

            if (_pointerEventData != null)
                _pointerEventData.Reset();

            _hoverTarget = null;
            _pressedTarget = null;
            _dragTarget = null;
            _dragInProgress = false;
            _hasLastPointerRaycastCanvasPosition = false;
            _raycastResults.Clear();
        }

        private void CancelActivePointerGesture()
        {
            if (_pointerEventData == null)
                return;

            if (_dragInProgress && _pointerEventData.pointerDrag != null)
                ExecuteEvents.Execute(_pointerEventData.pointerDrag, _pointerEventData, ExecuteEvents.endDragHandler);

            if (_pressedTarget != null)
                ExecuteEvents.ExecuteHierarchy(_pressedTarget, _pointerEventData, ExecuteEvents.pointerUpHandler);

            _pressedTarget = null;
            _dragTarget = null;
            _dragInProgress = false;
            _pointerEventData.pointerPress = null;
            _pointerEventData.rawPointerPress = null;
            _pointerEventData.pointerDrag = null;
            _pointerEventData.dragging = false;
        }

        private Material ResolveTabletScreenMaterial()
        {
            if (tabletScreenUnlitMaterial != null)
                return tabletScreenUnlitMaterial;

            if (_runtimeTabletScreenMaterial != null)
                return _runtimeTabletScreenMaterial;

            if (tabletScreenUnlitShader == null)
                return null;

            _runtimeTabletScreenMaterial = new Material(tabletScreenUnlitShader)
            {
                name = "DiegeticPDA_Screen_Runtime"
            }; // COLD ALLOC: Material[1] — diegetic PDA emissive screen fallback — owner: DiegeticPDAController
            return _runtimeTabletScreenMaterial;
        }

        private void DisposeRuntimeScreenMaterial()
        {
            if (_runtimeTabletScreenMaterial == null)
                return;

            Destroy(_runtimeTabletScreenMaterial);
            _runtimeTabletScreenMaterial = null;
        }

        private static Vector2Int SanitizeTabletResolution(Vector2Int resolution)
        {
            return new Vector2Int(
                math.clamp(resolution.x, 64, 512),
                math.clamp(resolution.y, 64, 512));
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            tabletRenderTextureResolution = SanitizeTabletResolution(tabletRenderTextureResolution);
            activeCameraDistanceMeters = math.max(0.5f, activeCameraDistanceMeters);
            cameraFrustumDotThreshold = math.clamp(cameraFrustumDotThreshold, -0.2f, 0.8f);
        }
#endif
    }
}
