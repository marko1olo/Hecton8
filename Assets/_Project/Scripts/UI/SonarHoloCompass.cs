using Hecton8.Core;
using Hecton8.Visor;
using Hecton8.World;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Player-owned holographic compass that projects active abyssal anchors into the HUD as pulsing sonar contact dots.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Sonar Holo Compass")]
    public sealed class SonarHoloCompass : MonoBehaviour, ITickable
    {
        private const int MaxDots = 16;
        private const float RootWidth = 188f;
        private const float RootHeight = 188f;
        private const float RingRadius = 74f;
        private const float VerticalRadius = 48f;
        private const float DotBaseSize = 8f;
        private const float DotPulseSize = 7f;
        private const float PingDecaySharpness = 4.2f;
        private const float HiddenAlphaCutoff = 0.001f;
        private const string RootName = "SonarHoloCompass";

        private static readonly Color FrameColor = new Color(0.48f, 0.95f, 0.92f, 0.16f);
        private static readonly Color DotFrontColor = new Color(0.70f, 0.98f, 0.96f, 0.94f);
        private static readonly Color DotRearColor = new Color(0.62f, 0.78f, 0.82f, 0.34f);
        private static Sprite s_quadSprite;

        private bool _registeredToTick;
        private bool _uiBuilt;
        private Canvas _targetCanvas;
        private Camera _viewCamera;
        private HectonMapMagicVegetationBridge _vegetationBridge;
        private RectTransform _root;
        private CanvasGroup _canvasGroup;
        private RectTransform[] _dotRects;
        private Image[] _dotImages;
        private float _pingPulse;
        private float _lastRootAlpha = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_quadSprite = null;
        }

        private void OnEnable()
        {
            ResolveOwners();
            EnsureUiBuilt();
            SpectrumEvents.OnSonarPingSent += HandleSonarPingSent;
            RegisterToTickManager();
        }

        private void Start()
        {
            RegisterToTickManager();
        }

        private void OnDisable()
        {
            SpectrumEvents.OnSonarPingSent -= HandleSonarPingSent;
            UnregisterFromTickManager();
            ApplyRootAlpha(0f);
        }

        private void OnDestroy()
        {
            SpectrumEvents.OnSonarPingSent -= HandleSonarPingSent;
            UnregisterFromTickManager();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            ResolveOwners();
            EnsureUiBuilt();

            if (_canvasGroup == null || _root == null || _viewCamera == null || _vegetationBridge == null)
            {
                HideDots();
                ApplyRootAlpha(0f);
                return;
            }

            if (_pingPulse > 0f)
                _pingPulse = Mathf.Max(0f, _pingPulse - (dt * PingDecaySharpness));

            if (!_vegetationBridge.TryGetActiveAbyssalAnchorPayload(out NativeArray<Vector3> anchors, out int activeCount) ||
                !anchors.IsCreated ||
                activeCount <= 0)
            {
                HideDots();
                ApplyRootAlpha(0f);
                return;
            }

            RenderDots(anchors, activeCount);
        }

        private void HandleSonarPingSent(float intensity)
        {
            _pingPulse = Mathf.Max(_pingPulse, Mathf.Clamp01(intensity));
        }

        private void ResolveOwners()
        {
            if (_vegetationBridge == null)
                _vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;

            if (_viewCamera == null)
            {
                if (TryGetComponent(out Camera localCamera))
                    _viewCamera = localCamera;
                else
                    _viewCamera = GetComponentInChildren<Camera>(true);
            }

            if (_targetCanvas == null)
                _targetCanvas = ResolveTargetCanvas();
        }

        private void EnsureUiBuilt()
        {
            if (_uiBuilt || _targetCanvas == null)
                return;

            RectTransform canvasRoot = _targetCanvas.transform as RectTransform;
            if (canvasRoot == null)
                return;

            _root = FindExistingChild(canvasRoot, RootName);
            if (_root == null)
            {
                GameObject rootObject = new GameObject(RootName, typeof(RectTransform), typeof(CanvasGroup));
                rootObject.layer = canvasRoot.gameObject.layer;
                _root = rootObject.GetComponent<RectTransform>();
                _root.SetParent(canvasRoot, false);
            }

            _root.anchorMin = new Vector2(0.5f, 0f);
            _root.anchorMax = new Vector2(0.5f, 0f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.anchoredPosition = new Vector2(0f, 132f);
            _root.sizeDelta = new Vector2(RootWidth, RootHeight);
            _root.localScale = Vector3.one;
            _root.SetAsLastSibling();

            _canvasGroup = _root.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = _root.gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.alpha = 0f;

            ClearChildren(_root);
            CreateFrame();
            CreateDots();
            _uiBuilt = true;
        }

        private void CreateFrame()
        {
            Image outerRing = EnsureImage(CreateRect(_root, "RingOuter").gameObject);
            outerRing.sprite = ResolveQuadSprite();
            outerRing.color = FrameColor;
            outerRing.raycastTarget = false;
            outerRing.type = Image.Type.Simple;
            outerRing.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            outerRing.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            outerRing.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            outerRing.rectTransform.sizeDelta = new Vector2(RootWidth - 18f, RootHeight - 18f);

            Image horizontalRule = EnsureImage(CreateRect(_root, "RuleH").gameObject);
            horizontalRule.sprite = ResolveQuadSprite();
            horizontalRule.color = FrameColor;
            horizontalRule.raycastTarget = false;
            horizontalRule.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            horizontalRule.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            horizontalRule.rectTransform.sizeDelta = new Vector2(0f, 1f);

            Image verticalRule = EnsureImage(CreateRect(_root, "RuleV").gameObject);
            verticalRule.sprite = ResolveQuadSprite();
            verticalRule.color = FrameColor;
            verticalRule.raycastTarget = false;
            verticalRule.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            verticalRule.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            verticalRule.rectTransform.sizeDelta = new Vector2(1f, 0f);
        }

        private void CreateDots()
        {
            // COLD ALLOC: RectTransform[16] — prebuilt abyssal compass marker pool — owner: SonarHoloCompass
            _dotRects = new RectTransform[MaxDots];
            // COLD ALLOC: Image[16] — prebuilt abyssal compass marker visuals — owner: SonarHoloCompass
            _dotImages = new Image[MaxDots];

            for (int i = 0; i < MaxDots; i++)
            {
                RectTransform dotRect = CreateRect(_root, "Dot_" + i);
                dotRect.anchorMin = new Vector2(0.5f, 0.5f);
                dotRect.anchorMax = new Vector2(0.5f, 0.5f);
                dotRect.pivot = new Vector2(0.5f, 0.5f);
                dotRect.sizeDelta = new Vector2(DotBaseSize, DotBaseSize);

                Image dotImage = EnsureImage(dotRect.gameObject);
                dotImage.sprite = ResolveQuadSprite();
                dotImage.color = Color.clear;
                dotImage.raycastTarget = false;

                _dotRects[i] = dotRect;
                _dotImages[i] = dotImage;
            }
        }

        private void RenderDots(NativeArray<Vector3> anchors, int activeCount)
        {
            Vector3 cameraPosition = _viewCamera.transform.position;
            Vector3 cameraRight = _viewCamera.transform.right;
            Vector3 cameraUp = _viewCamera.transform.up;
            Vector3 cameraForward = _viewCamera.transform.forward;
            float pulse = _pingPulse;
            float pulseScale = 1f + (pulse * DotPulseSize / DotBaseSize);

            int visibleDots = Mathf.Min(MaxDots, activeCount);
            for (int i = 0; i < visibleDots; i++)
            {
                Vector3 delta = anchors[i] - cameraPosition;
                float distanceSqr = delta.sqrMagnitude;
                if (distanceSqr <= 0.01f)
                {
                    HideDot(i);
                    continue;
                }

                float inverseDistance = 1f / Mathf.Sqrt(distanceSqr);
                Vector3 direction = delta * inverseDistance;
                float x = Vector3.Dot(cameraRight, direction);
                float y = Vector3.Dot(cameraUp, direction);
                float z = Vector3.Dot(cameraForward, direction);

                RectTransform dotRect = _dotRects[i];
                Image dotImage = _dotImages[i];
                if (dotRect == null || dotImage == null)
                    continue;

                dotRect.anchoredPosition = new Vector2(
                    Mathf.Clamp(x * RingRadius, -RingRadius, RingRadius),
                    Mathf.Clamp(y * VerticalRadius, -VerticalRadius, VerticalRadius));

                float depthBlend = z >= 0f ? 1f : 0.35f;
                float size = DotBaseSize * Mathf.Lerp(0.72f, 1.12f, depthBlend) * pulseScale;
                dotRect.sizeDelta = new Vector2(size, size);
                dotImage.color = Color.Lerp(DotRearColor, DotFrontColor, depthBlend);
            }

            for (int i = visibleDots; i < MaxDots; i++)
                HideDot(i);

            ApplyRootAlpha(1f);
        }

        private void HideDots()
        {
            if (_dotImages == null)
                return;

            for (int i = 0; i < _dotImages.Length; i++)
                HideDot(i);
        }

        private void HideDot(int index)
        {
            if (_dotImages == null || index < 0 || index >= _dotImages.Length)
                return;

            Image image = _dotImages[index];
            if (image != null && image.color.a > HiddenAlphaCutoff)
                image.color = Color.clear;
        }

        private void ApplyRootAlpha(float alpha)
        {
            if (_canvasGroup == null || Mathf.Approximately(_lastRootAlpha, alpha))
                return;

            _canvasGroup.alpha = alpha;
            _lastRootAlpha = alpha;
        }

        private void RegisterToTickManager()
        {
            if (_registeredToTick)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register(this);
            _registeredToTick = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTick)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister(this);

            _registeredToTick = false;
        }

        private static Canvas ResolveTargetCanvas()
        {
            SuitHUDV4CanvasOverlay overlay = Object.FindAnyObjectByType<SuitHUDV4CanvasOverlay>();
            if (overlay != null && overlay.TargetCanvas != null)
                return overlay.TargetCanvas;

            return Object.FindAnyObjectByType<Canvas>();
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
            s_quadSprite.name = "SonarHoloCompassQuad";
            return s_quadSprite;
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

        private static RectTransform CreateRect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        private static Image EnsureImage(GameObject target)
        {
            Image image = target.GetComponent<Image>();
            if (image == null)
                image = target.AddComponent<Image>();
            return image;
        }
    }
}
