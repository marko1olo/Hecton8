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

        private void EnsureAuthoredMaterial()
        {
            if (_resolvedMaterial != null)
                return;

            UnityEngine.Assertions.Assert.IsNotNull(compassMaterial, "Fatal: ShaderCompassRibbon requires an authored compass material.");
            _resolvedMaterial = compassMaterial;
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
