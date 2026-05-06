using Hecton8.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// One-image compass ribbon. Camera yaw is a material offset; the shader scrolls the ribbon.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Shader Compass Ribbon")]
    public sealed class ShaderCompassRibbon : MonoBehaviour, IUpdatable
    {
        private const string RootName = "ShaderCompassRibbon";
        private const string CompassShaderName = "Hecton8/UI/CompassRibbon";
        private const float RootWidth = 420f;
        private const float RootHeight = 26f;
        private const float OffsetEpsilon = 0.0001f;

        private static readonly int CompassOffsetId = Shader.PropertyToID("_CompassOffset");

        [SerializeField] private Shader compassShader;

        private bool _registered;
        private bool _uiBuilt;
        private Camera _viewCamera;
        private RectTransform _root;
        private CanvasGroup _canvasGroup;
        private Image _ribbonImage;
        private Material _runtimeMaterial;
        private float _lastOffset = -1f;

        private void OnEnable()
        {
            ResolveViewCamera();
            EnsureUiBuilt();
            TryRegister();
        }

        private void Start()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            ApplyRootAlpha(0f);
        }

        private void OnDestroy()
        {
            TryUnregister();
            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
                _runtimeMaterial = null;
            }
        }

        public void Tick(float deltaTime)
        {
            ResolveViewCamera();
            EnsureUiBuilt();

            if (_viewCamera == null || _runtimeMaterial == null)
            {
                ApplyRootAlpha(0f);
                return;
            }

            float yaw = _viewCamera.transform.eulerAngles.y;
            float offset = Mathf.Repeat(yaw / 360f, 1f);
            if (Mathf.Abs(offset - _lastOffset) > OffsetEpsilon)
            {
                _runtimeMaterial.SetFloat(CompassOffsetId, offset);
                _lastOffset = offset;
            }

            ApplyRootAlpha(1f);
        }

        private void ResolveViewCamera()
        {
            if (_viewCamera != null)
                return;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null && playerContext.PlayerCamera != null)
                _viewCamera = playerContext.PlayerCamera;
        }

        private void EnsureUiBuilt()
        {
            if (_uiBuilt)
                return;

            Canvas targetCanvas = ResolveTargetCanvas();
            if (targetCanvas == null)
                return;

            RectTransform canvasRoot = HectonUIScaler.ResolveContentRoot(targetCanvas);
            if (canvasRoot == null)
                return;

            _root = FindExistingChild(canvasRoot, RootName);
            if (_root == null)
            {
                GameObject rootObject = new GameObject(RootName, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                rootObject.layer = canvasRoot.gameObject.layer;
                _root = rootObject.GetComponent<RectTransform>();
                _root.SetParent(canvasRoot, false);
            }

            _root.anchorMin = new Vector2(0.5f, 1f);
            _root.anchorMax = new Vector2(0.5f, 1f);
            _root.pivot = new Vector2(0.5f, 1f);
            _root.anchoredPosition = new Vector2(0f, -24f);
            _root.sizeDelta = new Vector2(RootWidth, RootHeight);
            _root.localScale = Vector3.one;
            _root.SetAsLastSibling();

            _canvasGroup = _root.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = _root.gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.alpha = 0f;

            _ribbonImage = _root.GetComponent<Image>();
            if (_ribbonImage == null)
                _ribbonImage = _root.gameObject.AddComponent<Image>();

            _ribbonImage.sprite = null;
            _ribbonImage.color = Color.white;
            _ribbonImage.raycastTarget = false;
            EnsureRuntimeMaterial();
            if (_runtimeMaterial != null)
                _ribbonImage.material = _runtimeMaterial;

            _uiBuilt = true;
        }

        private void EnsureRuntimeMaterial()
        {
            if (_runtimeMaterial != null)
                return;

            if (compassShader == null)
                compassShader = Shader.Find(CompassShaderName);

            if (compassShader == null)
                return;

            _runtimeMaterial = new Material(compassShader)
            {
                name = "HUD_CompassRibbon_Runtime"
            }; // COLD ALLOC: Material[1] — shader-driven compass ribbon material — owner: ShaderCompassRibbon
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registered = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
        }

        private void ApplyRootAlpha(float alpha)
        {
            if (_canvasGroup != null)
                _canvasGroup.alpha = alpha;
        }

        private static Canvas ResolveTargetCanvas()
        {
            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
            if (overlay != null && overlay.TargetCanvas != null)
                return overlay.TargetCanvas;

            return overlay != null ? overlay.GetComponent<Canvas>() : null;
        }

        private static RectTransform FindExistingChild(Transform parent, string name)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name)
                    return child as RectTransform;
            }

            return null;
        }
    }
}
