using Hecton8.Core;
using Unity.Mathematics;
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
        private const float RootWidth = 420f;
        private const float RootHeight = 26f;
        private const float OffsetEpsilon = 0.0001f;
        private const float InvFullCircle = 1f / 360f;
        private const float CameraResolveRetryIntervalSeconds = 0.25f;
        private const float MaximumCompassDeltaSeconds = 0.1f;

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
        private float _lastRootAlpha = -1f;
        private float _cameraResolveRetryRemaining;

        private void OnEnable()
        {
            _cameraResolveRetryRemaining = 0f;
            ResolveViewCamera(0f);
            EnsureUiBuilt(allowCreate: true);
            TryRegister();
        }

        private void Start()
        {
            _cameraResolveRetryRemaining = 0f;
            ResolveViewCamera(0f);
            EnsureUiBuilt(allowCreate: true);
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
            float safeDeltaTime = SanitizeDeltaTime(deltaTime);
            ResolveViewCamera(safeDeltaTime);
            if (!EnsureUiBuilt(allowCreate: false))
            {
                ApplyRootAlpha(0f);
                return;
            }

            if (_viewCamera == null || _runtimeMaterial == null)
            {
                ApplyRootAlpha(0f);
                return;
            }

            float yaw = _viewCamera.transform.eulerAngles.y;
            float offset = math.frac(yaw * InvFullCircle);
            if (math.abs(offset - _lastOffset) > OffsetEpsilon)
            {
                _runtimeMaterial.SetFloat(CompassOffsetId, offset);
                _lastOffset = offset;
            }

            ApplyRootAlpha(1f);
        }

        private void ResolveViewCamera(float deltaTime)
        {
            if (_viewCamera != null && _viewCamera.isActiveAndEnabled)
                return;

            _viewCamera = null;
            if (Application.isPlaying)
            {
                if (_cameraResolveRetryRemaining > 0f)
                {
                    _cameraResolveRetryRemaining = math.max(0f, _cameraResolveRetryRemaining - deltaTime);
                    if (_cameraResolveRetryRemaining > 0f)
                        return;
                }

                _cameraResolveRetryRemaining = CameraResolveRetryIntervalSeconds;
            }

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null && playerContext.PlayerCamera != null)
                _viewCamera = playerContext.PlayerCamera;
        }

        private bool EnsureUiBuilt(bool allowCreate)
        {
            if (_uiBuilt)
                return true;

            if (!allowCreate)
                return false;

            Canvas targetCanvas = ResolveTargetCanvas(allowComponentFallback: true);
            if (targetCanvas == null)
                return false;

            RectTransform canvasRoot = HectonUIScaler.ResolveContentRoot(targetCanvas);
            if (canvasRoot == null)
                return false;

            _root = FindExistingChild(canvasRoot, RootName);
            if (_root == null)
            {
                GameObject rootObject = new GameObject(RootName, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                rootObject.layer = canvasRoot.gameObject.layer;
                rootObject.TryGetComponent(out _root);
                _root.SetParent(canvasRoot, false);
            }

            _root.anchorMin = new Vector2(0.5f, 1f);
            _root.anchorMax = new Vector2(0.5f, 1f);
            _root.pivot = new Vector2(0.5f, 1f);
            _root.anchoredPosition = new Vector2(0f, -24f);
            _root.sizeDelta = new Vector2(RootWidth, RootHeight);
            _root.localScale = Vector3.one;
            _root.SetAsLastSibling();

            _root.TryGetComponent(out _canvasGroup);
            if (_canvasGroup == null)
                _canvasGroup = _root.gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.alpha = 0f;

            _root.TryGetComponent(out _ribbonImage);
            if (_ribbonImage == null)
                _ribbonImage = _root.gameObject.AddComponent<Image>();

            _ribbonImage.sprite = null;
            _ribbonImage.color = Color.white;
            _ribbonImage.raycastTarget = false;
            EnsureRuntimeMaterial();
            if (_runtimeMaterial != null)
                _ribbonImage.material = _runtimeMaterial;

            _uiBuilt = true;
            return true;
        }

        private void EnsureRuntimeMaterial()
        {
            if (_runtimeMaterial != null)
                return;

            if (compassShader == null)
                return;

            _runtimeMaterial = new Material(compassShader)
            {
                hideFlags = HideFlags.DontSave
            }; // COLD ALLOC: Material[1] — shader-driven compass ribbon material — owner: ShaderCompassRibbon
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
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
            if (_canvasGroup == null || math.abs(_lastRootAlpha - alpha) <= 0.0001f)
                return;

            _canvasGroup.alpha = alpha;
            _lastRootAlpha = alpha;
        }

        private static Canvas ResolveTargetCanvas(bool allowComponentFallback)
        {
            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
            if (overlay != null && overlay.TargetCanvas != null)
                return overlay.TargetCanvas;

            if (!allowComponentFallback || overlay == null)
                return null;

            overlay.TryGetComponent(out Canvas canvas);
            return canvas;
        }

        private static float SanitizeDeltaTime(float deltaTime)
        {
            return math.isfinite(deltaTime) ? math.clamp(deltaTime, 0f, MaximumCompassDeltaSeconds) : 0f;
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
