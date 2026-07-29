using Hecton8.Core;
using Hecton8.Core.Contracts;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Legacy world-space compass ribbon. Diegetic compass service owns heading authority.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Shader Compass Ribbon")]
    public sealed class ShaderCompassRibbon : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const string RootName = "ShaderCompassRibbon";
        private const float RootWidth = 420f;
        private const float RootHeight = 26f;
        private const float OffsetEpsilon = 0.0001f;
        private const float InvFullCircle = 1f / 360f;

        private static readonly int CompassOffsetId = Shader.PropertyToID("_CompassOffset");

        [SerializeField, Tooltip("Required authored compass ribbon material. Runtime material generation is forbidden.")]
        private Material compassMaterial;

        [SerializeField, Tooltip("Optional authored ribbon root. If omitted, ShaderCompassRibbon is resolved under the HUD content root.")]
        private RectTransform _authoredRoot;

        private bool _registered;
        private bool _uiBuilt;
        private RectTransform _root;
        private CanvasGroup _canvasGroup;
        private Image _ribbonImage;
        private Material _resolvedMaterial;
        private IInertialNavigationService _navigation;
        private float _lastOffset = -1f;
        private float _lastRootAlpha = -1f;
        private bool _hotSwapListenerRegistered;
        private bool _missingCompassMaterialAnnounced;

        private void OnEnable()
        {
            EnsureUiBuilt();
            CacheNavigationServiceCold();
            TryRegisterHotSwapListener();
            TryRegister();
        }

        private void Start()
        {
            EnsureUiBuilt();
            CacheNavigationServiceCold();
            TryRegisterHotSwapListener();
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            ApplyRootAlpha(0f);
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            _resolvedMaterial = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.InertialNavigationRuntime)
            {
                _navigation = currentService as IInertialNavigationService;
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            TryUnregister();
            if (isActiveAndEnabled)
            {
                if (currentService != null)
                    TryRegister();
            }
        }

        public void LateFrameTick()
        {
            if (!_uiBuilt || _root == null)
            {
                ApplyRootAlpha(0f);
                return;
            }

            if (_resolvedMaterial == null)
            {
                ApplyRootAlpha(0f);
                return;
            }

            IInertialNavigationService navigation = _navigation;
            if (navigation == null || !navigation.TryGetSnapshot(out InertialNavigationSnapshot snapshot))
            {
                ApplyRootAlpha(0f);
                return;
            }

            float offset = math.frac(snapshot.FalseBearingDegrees * InvFullCircle);
            if (math.abs(offset - _lastOffset) > OffsetEpsilon)
            {
                Shader.SetGlobalFloat(CompassOffsetId, offset);
                _lastOffset = offset;
            }

            ApplyRootAlpha(1f);
        }

        private bool EnsureUiBuilt()
        {
            if (_uiBuilt)
                return true;

            Canvas targetCanvas = ResolveTargetCanvas();
            if (targetCanvas == null || targetCanvas.renderMode != RenderMode.WorldSpace)
                return false;

            RectTransform canvasRoot = HectonUIScaler.ResolveContentRoot(targetCanvas);
            if (canvasRoot == null)
                return false;

            _root = _authoredRoot != null
                ? _authoredRoot
                : FindExistingChild(canvasRoot, RootName);

            if (_root == null)
                return false;

            _root.anchorMin = new Vector2(0.5f, 1f);
            _root.anchorMax = new Vector2(0.5f, 1f);
            _root.pivot = new Vector2(0.5f, 1f);
            _root.anchoredPosition = new Vector2(0f, -24f);
            _root.sizeDelta = new Vector2(RootWidth, RootHeight);
            _root.localScale = Vector3.one;
            _root.SetAsLastSibling();

            _root.TryGetComponent(out _canvasGroup);
            if (_canvasGroup == null)
                return false;

            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.alpha = 0f;

            _root.TryGetComponent(out _ribbonImage);
            if (_ribbonImage == null)
                return false;

            _ribbonImage.sprite = null;
            _ribbonImage.color = Color.white;
            _ribbonImage.raycastTarget = false;
            EnsureAuthoredMaterial();
            if (_resolvedMaterial != null)
                _ribbonImage.material = _resolvedMaterial;

            CacheNavigationServiceCold();
            _uiBuilt = true;
            return true;
        }

        private void CacheNavigationServiceCold()
        {
            if (_navigation != null)
                return;

            _navigation = GlobalRegistry.InertialNavigation;
        }

        /// <summary>
        /// Resolves the authored ribbon material, or reports the gap once without throwing.
        /// </summary>
        /// <remarks>
        /// The <c>UnityEngine.Assertions.Assert.IsNotNull</c> removed from here THREW - nothing under Assets sets
        /// <c>Assert.raiseExceptions = false</c> - and it fired from deep inside <see cref="EnsureUiBuilt"/>
        /// (:167), so the throw unwound past <c>_uiBuilt = true</c> (:172) and past
        /// <see cref="CacheNavigationServiceCold"/> (:171). Both callers reach EnsureUiBuilt before they register:
        /// <see cref="OnEnable"/> (:43) and <see cref="Start"/> (:51) each call it and only then
        /// <see cref="TryRegisterHotSwapListener"/> and <see cref="TryRegister"/> (:45-46, :53-54). One unassigned
        /// material therefore left the ribbon unbuilt, unregistered from the UI late-frame lane, and deaf to
        /// dispatcher hot-swap for the whole session.
        ///
        /// The assert guarded nothing: assigning a null <c>_resolvedMaterial</c> is the designed idle state.
        /// <see cref="LateFrameTick"/> checks it at :101 and fades the ribbon out via <c>ApplyRootAlpha(0f)</c>,
        /// and the <c>_ribbonImage.material</c> assignment at :168 is already null-guarded.
        /// </remarks>
        private void EnsureAuthoredMaterial()
        {
            if (_resolvedMaterial != null)
                return;

            _resolvedMaterial = compassMaterial;
            if (_resolvedMaterial != null || _missingCompassMaterialAnnounced)
                return;

            // Report LAST. EnsureUiBuilt continues to _uiBuilt = true and the caller continues to TryRegister
            // after this returns, so a future re-introduced throw here can no longer strand the ribbon.
            _missingCompassMaterialAnnounced = true;
            LogMissingCompassMaterial();
        }

        /// <summary>
        /// One-shot report of the unassigned authored compass material. The latch guarantees single emission and
        /// the method takes no arguments, so no string work or allocation reaches the late-frame cadence.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMissingCompassMaterial()
        {
            Hecton8.Core.H8Debug.LogError("ShaderCompassRibbon: serialized field 'compassMaterial' is unassigned, so the world-space compass ribbon stays faded to alpha 0 for this session - LateFrameTick bails on the null resolved material. The ribbon UI is still built and the component still ticks and still tracks the inertial navigation service. Runtime material generation is forbidden: assign the authored compass ribbon material in the inspector.");
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            _registered = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
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

        private void TryUnregister()
        {
            if (!_registered)
                return;

            SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
            _registered = false;
        }

        private void ApplyRootAlpha(float alpha)
        {
            if (_canvasGroup == null || math.abs(_lastRootAlpha - alpha) <= 0.0001f)
                return;

            _canvasGroup.alpha = alpha;
            _lastRootAlpha = alpha;
        }

        private static Canvas ResolveTargetCanvas()
        {
            SuitHUDV4CanvasOverlay overlay = null;
            SuitHUDV4CanvasOverlay.TryResolveActiveRuntime(ref overlay);
            if (overlay != null && overlay.TargetCanvas != null)
                return overlay.TargetCanvas;

            if (overlay == null)
                return null;

            overlay.TryGetComponent(out Canvas canvas);
            return canvas;
        }

        private static RectTransform FindExistingChild(Transform parent, string name)
        {
            return UiChildSpanUtility.FindExistingChild(parent, name);
        }
    }
}
