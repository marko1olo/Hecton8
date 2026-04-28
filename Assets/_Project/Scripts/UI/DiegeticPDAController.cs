using Hecton8.Core;
using Hecton8.Gameplay;
using System.Collections.Generic;
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
        [SerializeField, Tooltip("Fixed PDA render-texture resolution retained across panel disable/enable so the tablet screen does not churn allocations.")]
        private Vector2Int tabletRenderTextureResolution = new Vector2Int(1024, 512);

        // COLD ALLOC: GameObject[8] — diegetic PDA tab routing cache — owner: DiegeticPDAController
        [SerializeField] private GameObject[] configuredTabs = new GameObject[8];
        // COLD ALLOC: List<RaycastResult>(16) — reusable diegetic PDA UI hit cache — owner: DiegeticPDAController
        private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>(16);

        private bool _registeredToTickManager;
        private bool _uiConfigured;
        private bool _lastOpenState;
        private Canvas _panelCanvas;
        private GraphicRaycaster _panelGraphicRaycaster;
        private EventSystem _eventSystem;
        private PointerEventData _pointerEventData;
        private GameObject _hoverTarget;
        private GameObject _pressedTarget;
        private GameObject _dragTarget;
        private bool _dragInProgress;
        private Material _runtimeTabletScreenMaterial;

        private void Awake()
        {
            ResolveReferences();
            ConfigureDiegeticPdaShell();
            ApplyPresentationState(PlayerPDA.IsOpen, force: true);
        }

        private void OnEnable()
        {
            ResolveReferences();
            ConfigureDiegeticPdaShell();
            RegisterToTickManager();
        }

        private void OnDisable()
        {
            UnregisterFromTickManager();
            _uiConfigured = false;
            ClearPointerState();
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
            ResolveReferences();
            ConfigureDiegeticPdaShell();

            bool openState = PlayerPDA.IsOpen;
            if (openState != _lastOpenState)
                ApplyPresentationState(openState, force: true);
        }

        public void ReceiveCanvasInput(in DiegeticPanelInputEvent inputEvent)
        {
            if (!PlayerPDA.IsOpen || !EnsureUiInteractionState())
                return;

            _panelGraphicRaycaster.enabled = true;
            _raycastResults.Clear();

            _pointerEventData.Reset();
            _pointerEventData.position = new Vector2(inputEvent.CanvasHitPoint.x, inputEvent.CanvasHitPoint.y);
            _pointerEventData.delta = Vector2.zero;
            _pointerEventData.button = PointerEventData.InputButton.Left;
            _pointerEventData.scrollDelta = Vector2.zero;
            _pointerEventData.useDragThreshold = false;

            _panelGraphicRaycaster.Raycast(_pointerEventData, _raycastResults);
            GameObject hitTarget = _raycastResults.Count > 0 ? _raycastResults[0].gameObject : null;
            if (_raycastResults.Count > 0)
                _pointerEventData.pointerCurrentRaycast = _raycastResults[0];
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
            if (_registeredToTickManager)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registeredToTickManager = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registeredToTickManager = false;
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

            if (_panelGraphicRaycaster == null && _panelCanvas != null)
            {
                _panelCanvas.TryGetComponent(out _panelGraphicRaycaster);
                if (_panelGraphicRaycaster == null)
                    _panelGraphicRaycaster = _panelCanvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            if (tabletHandAnchor == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                PlayerToolManager toolManager = playerContext != null ? playerContext.ToolManager : null;
                if (toolManager != null)
                    tabletHandAnchor = toolManager.HandAnchor;
            }

            if (tabletScreenRenderer == null && tabletRoot != null)
                tabletScreenRenderer = tabletRoot.GetComponentInChildren<Renderer>(true);

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
            if (diegeticPanel != null)
            {
                diegeticPanel.OverridePanelInteractable(this);
                diegeticPanel.OverrideFixedRenderTextureResolution(tabletRenderTextureResolution, retainOnDisable: true);
                diegeticPanel.OverridePanelPresentation(ResolveTabletScreenMaterial(), tabletScreenRenderer);
            }
            if (diegeticPanel != null)
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

            if (tabletRoot != null && hideTabletWhenClosed && tabletRoot.activeSelf != openState)
                tabletRoot.SetActive(openState);

            if (diegeticPanel != null && diegeticPanel.enabled != openState)
                diegeticPanel.enabled = openState;

            if (diegeticPanelCanvasGroup != null)
            {
                diegeticPanelCanvasGroup.alpha = openState ? 1f : 0f;
                diegeticPanelCanvasGroup.interactable = openState;
                diegeticPanelCanvasGroup.blocksRaycasts = openState;
            }

            if (!openState)
                ClearPointerState();

            if (openState && diegeticPanel != null)
                diegeticPanel.ForceRefreshRenderTexture();
        }

        private bool EnsureUiInteractionState()
        {
            ResolveReferences();
            if (_panelCanvas == null || _panelGraphicRaycaster == null)
                return false;

            if (_eventSystem == null)
            {
                _eventSystem = EventSystem.current;
                if (_eventSystem == null)
                {
                    GameObject eventSystemRoot = new GameObject("DiegeticPDA_EventSystem", typeof(EventSystem)); // COLD ALLOC: GameObject[1] — PDA pointer-dispatch fallback event system — owner: DiegeticPDAController
                    _eventSystem = eventSystemRoot.GetComponent<EventSystem>();
                }
            }

            if (_pointerEventData == null && _eventSystem != null)
                _pointerEventData = new PointerEventData(_eventSystem); // COLD ALLOC: PointerEventData[1] — PDA pointer-dispatch cache — owner: DiegeticPDAController

            return _eventSystem != null && _pointerEventData != null;
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
            if (_pointerEventData != null)
                _pointerEventData.Reset();

            if (_hoverTarget != null && _pointerEventData != null)
                ExecuteEvents.ExecuteHierarchy(_hoverTarget, _pointerEventData, ExecuteEvents.pointerExitHandler);

            _hoverTarget = null;
            _pressedTarget = null;
            _dragTarget = null;
            _dragInProgress = false;
            _raycastResults.Clear();
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
    }
}
