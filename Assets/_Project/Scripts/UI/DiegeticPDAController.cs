using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
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
    public sealed class DiegeticPDAController : MonoBehaviour, ILateFrameTickable, IPanelInteractable, IGlobalRegistryHotSwapListener
    {
        private const float HoverRaycastPixelThresholdSq = 16f;
        private const int PointerTargetCapacity = 256;
        private const int PointerDiscoveryCapacity = 512;
        private const int MaxPointerCanvasGroupsPerTarget = 8;
        private const int TabletRendererDiscoveryCapacity = 32;
        private const int TabletColliderDiscoveryCapacity = 16;
        private const int TabletCanvasGroupDiscoveryCapacity = 8;
        private const byte PointerCanvasGroupCacheOverflow = byte.MaxValue;
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
        [SerializeField, Tooltip("Required authored unlit material used by the PDA screen mesh so the panel remains emissive in caves. Runtime material creation is forbidden.")]
        private Material tabletScreenUnlitMaterial;
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

        // COLD ALLOC: GameObject[8] - diegetic PDA tab routing cache - owner: DiegeticPDAController
        [SerializeField] private GameObject[] configuredTabs = new GameObject[8];
        // COLD ALLOC: List<RaycastResult>(16) - reusable diegetic PDA UI hit cache - owner: DiegeticPDAController
        private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>(16);
        // COLD ALLOC: RectTransform[256] - cached PDA pointer bounds - owner: DiegeticPDAController
        private readonly RectTransform[] _pointerTargetRects = new RectTransform[PointerTargetCapacity];
        // COLD ALLOC: GameObject[256] - cached PDA pointer target game objects - owner: DiegeticPDAController
        private readonly GameObject[] _pointerTargetObjects = new GameObject[PointerTargetCapacity];
        // COLD ALLOC: CanvasGroup[256 * 8] - cached PDA pointer visibility gate stack - owner: DiegeticPDAController
        private readonly CanvasGroup[] _pointerTargetCanvasGroups = new CanvasGroup[PointerTargetCapacity * MaxPointerCanvasGroupsPerTarget];
        // COLD ALLOC: byte[256] - CanvasGroup stack count per PDA pointer target - owner: DiegeticPDAController
        private readonly byte[] _pointerTargetCanvasGroupCounts = new byte[PointerTargetCapacity];
        // COLD ALLOC: Selectable[256] - cached PDA pointer interactable gates - owner: DiegeticPDAController
        private readonly Selectable[] _pointerTargetSelectables = new Selectable[PointerTargetCapacity];
        // COLD ALLOC: Graphic[256] - cached PDA pointer draw-depth hints - owner: DiegeticPDAController
        private readonly Graphic[] _pointerTargetGraphics = new Graphic[PointerTargetCapacity];
        // COLD ALLOC: List<Renderer>(32) - reusable PDA tablet visibility cache - owner: DiegeticPDAController
        private readonly List<Renderer> _tabletVisibilityRenderers = new List<Renderer>(TabletRendererDiscoveryCapacity);
        // COLD ALLOC: List<Collider>(16) - reusable PDA tablet visibility cache - owner: DiegeticPDAController
        private readonly List<Collider> _tabletVisibilityColliders = new List<Collider>(TabletColliderDiscoveryCapacity);
        // COLD ALLOC: List<CanvasGroup>(8) - reusable PDA tablet visibility cache - owner: DiegeticPDAController
        private readonly List<CanvasGroup> _tabletVisibilityCanvasGroups = new List<CanvasGroup>(TabletCanvasGroupDiscoveryCapacity);

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
        private bool _hotSwapListenerRegistered;
        private int _pointerTargetCount;
        private int _acceptedPanelId = 1;
        private IPlayerRuntimeContext _cachedPlayerContext;

        private void Awake()
        {
            CharBufferPool.BindDataVaultCold(GlobalRegistry.DataVault);
            CharBufferPool.Prewarm();
            CacheRegistryServicesCold();
            ResolveReferences();
            ConfigureDiegeticPdaShell();
            ApplyPresentationState(PlayerPDA.IsOpen, force: true);
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            ResolveReferences();
            ConfigureDiegeticPdaShell();
            TryRegisterHotSwapListener();
            RegisterToTickManager();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            UnregisterFromTickManager();
            _uiConfigured = false;
            ClearPointerState();
            ClearPointerTargetCache();
            _pointerTargetRoot = null;
            _pointerTargetCacheDirty = true;
            ApplyPresentationCullState(false, false, force: true);
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            if (diegeticPanel != null)
                diegeticPanel.ReleasePresentationRenderTexture();
        }

        private void OnApplicationQuit()
        {
            if (diegeticPanel != null)
                diegeticPanel.ReleasePresentationRenderTexture();
        }

        public void LateFrameTick()
        {
            bool openState = PlayerPDA.IsOpen;
            if (!openState)
            {
                if (openState != _lastOpenState)
                    ApplyPresentationState(false, force: true);
                else
                    ApplyPresentationCullState(false, false, force: false);
                return;
            }

            if (!_uiConfigured || NeedsReferenceResolve())
            {
                ApplyPresentationCullState(openState, false, force: false);
                return;
            }

            if (openState != _lastOpenState)
                ApplyPresentationState(openState, force: true);

            ApplyPresentationCullState(openState, IsPdaVisibleToCamera(openState), force: false);
        }

        public void ReceiveCanvasInput(in DiegeticPanelInputEvent inputEvent)
        {
            if (inputEvent.PanelId != _acceptedPanelId)
                return;

            if (!PlayerPDA.IsOpen || !_lastPresentationActive || !EnsureUiInteractionState(allowColdCreateFallback: false))
                return;

            DiegeticPanelInputEventType pointerAction = DiegeticPanelInputEvent.ResolvePrimaryPointerAction(inputEvent.EventType);
            if (pointerAction == DiegeticPanelInputEventType.None)
                return;

            bool hasFiniteHitPoint = math.all(math.isfinite(inputEvent.CanvasHitPoint));
            if (!hasFiniteHitPoint)
            {
                if (pointerAction == DiegeticPanelInputEventType.Up)
                    HandlePointerUp(null);
                else if (pointerAction == DiegeticPanelInputEventType.Hover)
                    UpdateHoverTarget(null);

                return;
            }

            if (!TryResolveBoundedCanvasHit(inputEvent.CanvasHitPoint, out _, out _))
            {
                if (pointerAction == DiegeticPanelInputEventType.Up)
                    HandlePointerUp(null);
                else if (pointerAction == DiegeticPanelInputEventType.Hover)
                    UpdateHoverTarget(null);

                return;
            }

            if (_pressedTarget == null &&
                (pointerAction == DiegeticPanelInputEventType.Hold ||
                 pointerAction == DiegeticPanelInputEventType.Up))
            {
                return;
            }

            if (pointerAction == DiegeticPanelInputEventType.Hover &&
                _pressedTarget == null &&
                !_dragInProgress &&
                _hasLastPointerRaycastCanvasPosition &&
                math.lengthsq(inputEvent.CanvasHitPoint - _lastPointerRaycastCanvasPosition) <= HoverRaycastPixelThresholdSq)
            {
                return;
            }

            GameObject hitTarget = ResolvePanelHitTarget(inputEvent.CanvasHitPoint);
            UpdateHoverTarget(hitTarget);

            switch (pointerAction)
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

            _registeredToTickManager = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTickManager)
                return;

            SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
            _registeredToTickManager = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                CharBufferPool.BindDataVaultCold(currentService as IDataVault);
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Player)
                return;

            IPlayerRuntimeContext previousContext = _cachedPlayerContext;
            PlayerPDA previousPda = previousContext != null ? previousContext.PlayerPDA : null;
            Transform previousHandAnchor = previousContext != null && previousContext.ToolManager != null
                ? previousContext.ToolManager.HandAnchor
                : null;
            Camera previousCamera = previousContext != null ? previousContext.PlayerCamera : null;
            _cachedPlayerContext = currentService as IPlayerRuntimeContext;

            if (playerPda == null || ReferenceEquals(playerPda, previousPda))
                playerPda = _cachedPlayerContext != null ? _cachedPlayerContext.PlayerPDA : null;

            if (tabletHandAnchor == null || ReferenceEquals(tabletHandAnchor, previousHandAnchor))
                tabletHandAnchor = ResolveCachedPlayerHandAnchor();

            if (_visibilityCamera == null || ReferenceEquals(_visibilityCamera, previousCamera))
                _visibilityCamera = _cachedPlayerContext != null ? _cachedPlayerContext.PlayerCamera : null;

            ResolveReferences();
            ConfigureDiegeticPdaShell();
        }

        private void CacheRegistryServicesCold()
        {
            _cachedPlayerContext = GlobalRegistry.Player;
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
                IPlayerRuntimeContext playerContext = _cachedPlayerContext;
                if (playerContext != null)
                    playerPda = playerContext.PlayerPDA;

                if (playerPda == null)
                    playerPda = ResolveNearestParentComponent<PlayerPDA>(transform);
            }

            if (diegeticPanel == null)
                diegeticPanel = ComponentReferenceUtility.ResolveOwnedComponent<DiegeticPanelController>(transform);

            if (diegeticPanel != null)
                _acceptedPanelId = diegeticPanel.PanelId;

            if (diegeticPanelRoot == null)
            {
                if (diegeticPanelCanvasGroup != null)
                    diegeticPanelRoot = diegeticPanelCanvasGroup.gameObject;
                else if (diegeticPanel != null)
                    diegeticPanelRoot = diegeticPanel.gameObject;
            }

            if (diegeticPanelCanvasGroup == null && diegeticPanelRoot != null)
                diegeticPanelRoot.TryGetComponent(out diegeticPanelCanvasGroup);

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
                tabletHandAnchor = ResolveCachedPlayerHandAnchor();
            }

            if (tabletScreenRenderer == null && tabletRoot != null)
                tabletScreenRenderer = ComponentReferenceUtility.ResolveOwnedComponent<Renderer>(tabletRoot.transform);

            if (!ReferenceEquals(_cachedTabletRoot, tabletRoot))
                RebuildTabletVisibilityCache();

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
            float safeMaxDistanceMeters = ResolveActiveCameraDistanceMeters(activeCameraDistanceMeters);
            double maxDistanceSq = (double)safeMaxDistanceMeters * safeMaxDistanceMeters;
            double visibilityDistanceSq = ResolveAupVisibilityDistanceSq(cameraPosition, anchorPosition);
            if (!IsFiniteNonNegativeDistanceSq(visibilityDistanceSq) ||
                visibilityDistanceSq > maxDistanceSq)
            {
                return false;
            }

            float3 toPda = (float3)(anchorPosition - cameraPosition);
            float distanceSq = math.lengthsq(toPda);
            if (!math.isfinite(distanceSq))
                return false;
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

        private static double ResolveAupVisibilityDistanceSq(Vector3 cameraPosition, Vector3 anchorPosition)
        {
            if (!TryResolveAupFromRuntimeOrigin(cameraPosition, out AbsoluteUniversePosition cameraAup) ||
                !TryResolveAupFromRuntimeOrigin(anchorPosition, out AbsoluteUniversePosition anchorAup))
            {
                return ResolveLocalDistanceSq(cameraPosition, anchorPosition);
            }

            double distanceSq = AbsoluteUniversePosition.DistanceSq(in cameraAup, in anchorAup);
            return IsFiniteNonNegativeDistanceSq(distanceSq)
                ? distanceSq
                : ResolveLocalDistanceSq(cameraPosition, anchorPosition);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition absoluteAup)
        {
            absoluteAup = default;
            if (!float.IsFinite(runtimePosition.x) ||
                !float.IsFinite(runtimePosition.y) ||
                !float.IsFinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            absoluteAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                math.double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return absoluteAup.IsFinite();
        }

        private static double ResolveLocalDistanceSq(Vector3 a, Vector3 b)
        {
            if (!float.IsFinite(a.x) ||
                !float.IsFinite(a.y) ||
                !float.IsFinite(a.z) ||
                !float.IsFinite(b.x) ||
                !float.IsFinite(b.y) ||
                !float.IsFinite(b.z))
            {
                return double.MaxValue;
            }

            Vector3 delta = a - b;
            return (double)delta.x * delta.x +
                   (double)delta.y * delta.y +
                   (double)delta.z * delta.z;
        }

        private static float ResolveActiveCameraDistanceMeters(float distanceMeters)
        {
            return math.isfinite(distanceMeters) ? math.max(0.5f, distanceMeters) : 0.5f;
        }

        private static bool IsFiniteNonNegativeDistanceSq(double distanceSq)
        {
            return !double.IsNaN(distanceSq) &&
                   !double.IsInfinity(distanceSq) &&
                   distanceSq >= 0d;
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

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            _visibilityCamera = playerContext != null ? playerContext.PlayerCamera : null;
            return _visibilityCamera != null && _visibilityCamera.isActiveAndEnabled ? _visibilityCamera : null;
        }

        private Transform ResolveCachedPlayerHandAnchor()
        {
            PlayerToolManager toolManager = _cachedPlayerContext != null ? _cachedPlayerContext.ToolManager : null;
            return toolManager != null ? toolManager.HandAnchor : null;
        }

        private void RebuildTabletVisibilityCache()
        {
            _cachedTabletRoot = tabletRoot;
            _tabletVisibilityInitialized = false;
            if (tabletRoot == null)
            {
                _tabletVisibilityRenderers.Clear();
                _tabletVisibilityColliders.Clear();
                _tabletVisibilityCanvasGroups.Clear();
                return;
            }

            _tabletVisibilityRenderers.Clear();
            _tabletVisibilityColliders.Clear();
            _tabletVisibilityCanvasGroups.Clear();
            tabletRoot.GetComponentsInChildren(true, _tabletVisibilityRenderers);
            tabletRoot.GetComponentsInChildren(true, _tabletVisibilityColliders);
            tabletRoot.GetComponentsInChildren(true, _tabletVisibilityCanvasGroups);
        }

        private void SetTabletVisible(bool visible)
        {
            if (tabletRoot == null || (_tabletVisibilityInitialized && _tabletVisible == visible))
                return;

            List<Renderer> renderers = _tabletVisibilityRenderers;
            int rendererCount = renderers.Count;
            if (rendererCount > 0)
            {
                for (int i = 0; i < rendererCount; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer != null)
                        renderer.enabled = visible;
                }
            }

            List<Collider> colliders = _tabletVisibilityColliders;
            int colliderCount = colliders.Count;
            if (colliderCount > 0)
            {
                for (int i = 0; i < colliderCount; i++)
                {
                    Collider collider = colliders[i];
                    if (collider != null)
                        collider.enabled = visible;
                }
            }

            List<CanvasGroup> canvasGroups = _tabletVisibilityCanvasGroups;
            int canvasGroupCount = canvasGroups.Count;
            if (canvasGroupCount > 0)
            {
                for (int i = 0; i < canvasGroupCount; i++)
                {
                    CanvasGroup canvasGroup = canvasGroups[i];
                    if (canvasGroup == null)
                        continue;

                    canvasGroup.alpha = visible ? 1f : 0f;
                    canvasGroup.interactable = visible;
                    canvasGroup.blocksRaycasts = visible;
                }
            }

            _tabletVisible = visible;
            _tabletVisibilityInitialized = true;
        }

        private bool EnsureUiInteractionState(bool allowColdCreateFallback)
        {
            if (allowColdCreateFallback)
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    GameObject eventSystemRoot = new GameObject("DiegeticPDA_EventSystem", typeof(EventSystem)); // COLD ALLOC: GameObject[1] — PDA pointer-dispatch fallback event system — owner: DiegeticPDAController
                    eventSystemRoot.TryGetComponent(out _eventSystem);
#else
                    return false;
#endif
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
            Vector2 pointerPosition = default;
            pointerPosition.x = canvasHitPoint.x;
            pointerPosition.y = canvasHitPoint.y;
            _pointerEventData.position = pointerPosition;
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
                return false;

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
            RectTransform rectTransform = _pointerTargetRects[index];
            GameObject target = _pointerTargetObjects[index];
            if (rectTransform == null ||
                target == null ||
                !target.activeInHierarchy)
            {
                return false;
            }

            int baseIndex = index * MaxPointerCanvasGroupsPerTarget;
            int groupCount = _pointerTargetCanvasGroupCounts[index];
            if (groupCount == PointerCanvasGroupCacheOverflow)
            {
                return false;
            }

            for (int i = 0; i < groupCount; i++)
            {
                CanvasGroup canvasGroup = _pointerTargetCanvasGroups[baseIndex + i];
                if (canvasGroup != null &&
                    canvasGroup.isActiveAndEnabled &&
                    (!canvasGroup.interactable || !canvasGroup.blocksRaycasts || canvasGroup.alpha <= 0.001f))
                {
                    return false;
                }
            }

            Selectable selectable = _pointerTargetSelectables[index];
            return selectable == null || selectable.IsInteractable();
        }

        private bool TryCanvasHitPointToRootWorld(float2 canvasHitPoint, out Vector3 worldPoint)
        {
            worldPoint = default;
            if (!math.all(math.isfinite(canvasHitPoint)))
                return false;

            if (_panelCanvas == null)
                return false;

            RectTransform canvasRect = _panelCanvas.transform as RectTransform;
            if (canvasRect == null)
                return false;

            Rect rect = canvasRect.rect;
            if (rect.width <= 0.001f || rect.height <= 0.001f)
                return false;

            if (!TryResolveBoundedCanvasHit(canvasHitPoint, out int safeReferenceWidth, out int safeReferenceHeight))
                return false;

            float2 uv = math.float2(
                canvasHitPoint.x / safeReferenceWidth,
                canvasHitPoint.y / safeReferenceHeight);

            Vector3 localPoint = default;
            localPoint.x = rect.xMin + (rect.width * uv.x);
            localPoint.y = rect.yMin + (rect.height * uv.y);
            localPoint.z = 0f;
            worldPoint = canvasRect.TransformPoint(localPoint);
            return true;
        }

        private bool TryResolveBoundedCanvasHit(float2 canvasHitPoint, out int safeReferenceWidth, out int safeReferenceHeight)
        {
            safeReferenceWidth = 1;
            safeReferenceHeight = 1;
            if (!math.all(math.isfinite(canvasHitPoint)))
                return false;

            Vector2Int referenceResolution = diegeticPanel != null
                ? diegeticPanel.ReferenceResolutionPixels
                : SanitizeTabletResolution(tabletRenderTextureResolution);

            safeReferenceWidth = math.max(1, referenceResolution.x);
            safeReferenceHeight = math.max(1, referenceResolution.y);
            if (canvasHitPoint.x < 0f ||
                canvasHitPoint.y < 0f ||
                canvasHitPoint.x > safeReferenceWidth ||
                canvasHitPoint.y > safeReferenceHeight)
            {
                return false;
            }

            return true;
        }

        private void RebuildPointerTargetCache()
        {
            ClearPointerTargetCache();
            _pointerTargetRoot = diegeticPanelRoot;
            _pointerTargetCacheDirty = false;

            if (diegeticPanelRoot == null)
                return;

            CollectPointerTargetsInHierarchy(diegeticPanelRoot.transform);
        }

        private void CollectPointerTargetsInHierarchy(Transform root)
        {
            if (root == null || _pointerTargetCacheOverflow)
                return;

            if (TryResolvePointerDispatchTarget(root.gameObject, out GameObject target))
                AddPointerTarget(target);

            for (int i = 0; i < root.childCount; i++)
                CollectPointerTargetsInHierarchy(root.GetChild(i));
        }

        private static bool TryResolvePointerDispatchTarget(GameObject source, out GameObject target)
        {
            target = null;
            if (source == null)
                return false;

            target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(source);
            if (target != null)
                return true;
            target = ExecuteEvents.GetEventHandler<IPointerDownHandler>(source);
            if (target != null)
                return true;
            target = ExecuteEvents.GetEventHandler<IPointerUpHandler>(source);
            if (target != null)
                return true;
            target = ExecuteEvents.GetEventHandler<IPointerEnterHandler>(source);
            if (target != null)
                return true;
            target = ExecuteEvents.GetEventHandler<IPointerExitHandler>(source);
            if (target != null)
                return true;
            target = ExecuteEvents.GetEventHandler<IDragHandler>(source);
            if (target != null)
                return true;
            target = ExecuteEvents.GetEventHandler<IBeginDragHandler>(source);
            if (target != null)
                return true;
            target = ExecuteEvents.GetEventHandler<IEndDragHandler>(source);
            if (target != null)
                return true;
            target = ExecuteEvents.GetEventHandler<IDropHandler>(source);
            return target != null;
        }

        private void AddPointerTarget(GameObject target)
        {
            if (target == null)
                return;

            RectTransform targetRect = target.transform as RectTransform;
            if (targetRect == null)
                return;

            for (int targetIndex = 0; targetIndex < _pointerTargetCount; targetIndex++)
            {
                if (ReferenceEquals(_pointerTargetObjects[targetIndex], target))
                    return;
            }

            if (_pointerTargetCount >= PointerTargetCapacity)
            {
                _pointerTargetCacheOverflow = true;
                return;
            }

            target.TryGetComponent(out Selectable selectable);
            target.TryGetComponent(out Graphic graphic);

            _pointerTargetRects[_pointerTargetCount] = targetRect;
            _pointerTargetObjects[_pointerTargetCount] = target;
            _pointerTargetSelectables[_pointerTargetCount] = selectable;
            _pointerTargetGraphics[_pointerTargetCount] = graphic;
            CachePointerTargetCanvasGroups(_pointerTargetCount, targetRect);
            _pointerTargetCount++;
        }

        private void CachePointerTargetCanvasGroups(int targetIndex, Transform start)
        {
            int count = 0;
            bool overflow = false;
            Transform current = start;
            Transform rootTransform = diegeticPanelRoot != null ? diegeticPanelRoot.transform : null;
            int baseIndex = targetIndex * MaxPointerCanvasGroupsPerTarget;
            while (current != null)
            {
                if (current.TryGetComponent(out CanvasGroup group) && group != null)
                {
                    if (count < MaxPointerCanvasGroupsPerTarget)
                    {
                        _pointerTargetCanvasGroups[baseIndex + count++] = group;
                    }
                    else
                    {
                        overflow = true;
                    }
                }

                if (current == rootTransform)
                    break;

                current = current.parent;
            }

            _pointerTargetCanvasGroupCounts[targetIndex] = overflow ? PointerCanvasGroupCacheOverflow : (byte)count;
        }

        private static T ResolveNearestParentComponent<T>(Transform start) where T : Component
        {
            for (Transform current = start; current != null; current = current.parent)
            {
                if (current.TryGetComponent(out T component))
                    return component;
            }

            return null;
        }

        private void ClearPointerTargetCache()
        {
            for (int i = 0; i < _pointerTargetCount; i++)
            {
                _pointerTargetRects[i] = null;
                _pointerTargetObjects[i] = null;
                _pointerTargetCanvasGroupCounts[i] = 0;
                _pointerTargetSelectables[i] = null;
                _pointerTargetGraphics[i] = null;

                int baseIndex = i * MaxPointerCanvasGroupsPerTarget;
                for (int groupIndex = 0; groupIndex < MaxPointerCanvasGroupsPerTarget; groupIndex++)
                    _pointerTargetCanvasGroups[baseIndex + groupIndex] = null;
            }

            _pointerTargetCount = 0;
            _pointerTargetCacheOverflow = false;
        }

        private void InvalidatePointerTargetCache()
        {
            _pointerTargetCacheDirty = true;
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
            if (_pressedTarget != null)
                CancelActivePointerGesture();

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
            UnityEngine.Assertions.Assert.IsNotNull(
                tabletScreenUnlitMaterial,
                "Fatal: DiegeticPDAController requires an authored tablet screen material. Runtime material creation is forbidden.");
            return tabletScreenUnlitMaterial;
        }

        private static Vector2Int SanitizeTabletResolution(Vector2Int resolution)
        {
            Vector2Int safeResolution = default;
            safeResolution.x = math.clamp(resolution.x, 64, 512);
            safeResolution.y = math.clamp(resolution.y, 64, 512);
            return safeResolution;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            tabletRenderTextureResolution = SanitizeTabletResolution(tabletRenderTextureResolution);
            activeCameraDistanceMeters = ResolveActiveCameraDistanceMeters(activeCameraDistanceMeters);
            cameraFrustumDotThreshold = math.clamp(cameraFrustumDotThreshold, -0.2f, 0.8f);
        }
#endif
    }
}
