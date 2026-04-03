using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.UI;
using UnityEngine;
using UnityEngine.UI;

namespace NASAPunk.Visor
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [AddComponentMenu("Hecton8/HUD/Suit HUD Screen Compositor")]
    public sealed class SuitHUDScreenCompositor : MonoBehaviour, ITickable
    {
        private static readonly List<SuitHUDScreenCompositor> s_activeCompositors = new List<SuitHUDScreenCompositor>(2);
        private static readonly List<SuitHUDV4CanvasOverlay> s_overlayResolveBuffer = new List<SuitHUDV4CanvasOverlay>(4);
        private static readonly List<VisorHUDController> s_controllerResolveBuffer = new List<VisorHUDController>(2);
        private const float AutoResolveRetryInterval = 1f;

        [Header("References")]
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private RenderTexture sharedProjectionTexture;
        [SerializeField] private VisorHUDController visorController;

        [Header("Presentation")]
        [SerializeField] private string overlayName = "HUD_RT_Compositor";
        [SerializeField] [Range(0f, 1f)] private float overlayAlpha = 1f;
        [SerializeField] private bool forceCanvasActive;
        [SerializeField] private bool forceScreenSpaceOverlay = true;
        [SerializeField] private bool forceSharedProjection = true;
        [SerializeField] private bool hideWhenTextureMissing;
        [SerializeField] private bool preserveExistingChildren = true;
        [SerializeField] private int overlaySortingOrder = 80;
        [SerializeField] private bool manageCanvasInEditMode = true;
        [SerializeField] private bool showAsInsetPreview = true;
        [SerializeField] private Vector2 insetSize = new Vector2(340f, 340f);
        [SerializeField] private Vector2 insetMargin = new Vector2(18f, 18f);

        // Inspector-only compositor diagnostics for visor HUD bring-up.
#pragma warning disable CS0414
        [Header("Diagnostics")]
        [SerializeField] private bool debugCanvasReady;
        [SerializeField] private bool debugOverlayReady;
        [SerializeField] private bool debugTextureAssigned;
#pragma warning restore CS0414

        private RawImage _overlayImage;
        private RectTransform _overlayRect;
        private float _nextAutoResolveAt;
        private bool _tickRegistered;
        private bool _pendingRefresh = true;
        private string _appliedOverlayName;

        public static void CopyActiveCompositorsTo(List<SuitHUDScreenCompositor> results)
        {
            if (results == null)
                return;

            results.Clear();
            for (int i = 0; i < s_activeCompositors.Count; i++)
            {
                SuitHUDScreenCompositor compositor = s_activeCompositors[i];
                if (compositor != null && compositor.isActiveAndEnabled)
                    results.Add(compositor);
            }
        }

        private void OnEnable()
        {
            RegisterActiveCompositor();
            AutoResolveReferences(true);
            _pendingRefresh = true;
            RefreshCompositor();
            TryRegisterRuntimeTick();
        }

        private void OnDisable()
        {
            UnregisterActiveCompositor();
            UnregisterRuntimeTick();

            if (_overlayImage != null)
                _overlayImage.enabled = false;

            if (_overlayRect != null)
                _overlayRect.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                TryRegisterRuntimeTick();
                return;
            }

            if (!manageCanvasInEditMode)
                return;

            RefreshCompositor();
            _pendingRefresh = false;
        }

        private void OnValidate()
        {
            _pendingRefresh = true;
        }

        public void Tick(float deltaTime)
        {
            if (!_pendingRefresh && !NeedsAutoResolve())
            {
                UnregisterRuntimeTick();
                return;
            }

            RefreshCompositor();
            _pendingRefresh = false;
        }

        private void RefreshCompositor()
        {
            AutoResolveReferences();
            EnsureCanvasState();
            EnsureOverlay();
            EnsureProjection();
            BindTexture();
        }

        private void AutoResolveReferences(bool force = false)
        {
            if (!force && !NeedsAutoResolve())
                return;

            float now = Time.realtimeSinceStartup;
            if (!force && now < _nextAutoResolveAt)
                return;

            _nextAutoResolveAt = now + AutoResolveRetryInterval;

            if (targetCanvas == null)
            {
                SuitHUDV4CanvasOverlay.CopyActiveOverlaysTo(s_overlayResolveBuffer);
                Transform root = transform.root;
                for (int i = 0; i < s_overlayResolveBuffer.Count; i++)
                {
                    SuitHUDV4CanvasOverlay overlay = s_overlayResolveBuffer[i];
                    Canvas candidateCanvas = overlay != null ? overlay.TargetCanvas : null;
                    if (candidateCanvas == null || candidateCanvas.transform.root != root)
                        continue;

                    if (candidateCanvas.name == "Suit_HUD_Canvas")
                    {
                        targetCanvas = candidateCanvas;
                        break;
                    }
                }

                if (targetCanvas == null)
                {
                    for (int i = 0; i < s_overlayResolveBuffer.Count; i++)
                    {
                        SuitHUDV4CanvasOverlay overlay = s_overlayResolveBuffer[i];
                        Canvas candidateCanvas = overlay != null ? overlay.TargetCanvas : null;
                        if (candidateCanvas != null && candidateCanvas.name == "Suit_HUD_Canvas")
                        {
                            targetCanvas = candidateCanvas;
                            break;
                        }
                    }
                }

                s_overlayResolveBuffer.Clear();
            }

            if (visorController == null)
            {
                Transform parent = transform.parent;
                if (parent != null)
                {
                    Transform visor = parent.Find("Suit_Visor");
                    if (visor != null)
                        visorController = visor.GetComponent<VisorHUDController>();
                }

                if (visorController == null)
                {
                    VisorHUDController.CopyActiveControllersTo(s_controllerResolveBuffer);
                    Transform root = transform.root;
                    for (int i = 0; i < s_controllerResolveBuffer.Count; i++)
                    {
                        VisorHUDController controller = s_controllerResolveBuffer[i];
                        if (controller != null && controller.transform.root == root)
                        {
                            visorController = controller;
                            break;
                        }
                    }

                    if (visorController == null && s_controllerResolveBuffer.Count > 0)
                        visorController = s_controllerResolveBuffer[0];

                    s_controllerResolveBuffer.Clear();
                }
            }

            if (sharedProjectionTexture == null && visorController != null)
                sharedProjectionTexture = visorController.SharedRenderTexture;
        }

        private bool NeedsAutoResolve()
        {
            return targetCanvas == null ||
                   visorController == null ||
                   sharedProjectionTexture == null;
        }

        private void EnsureCanvasState()
        {
            debugCanvasReady = targetCanvas != null;
            if (targetCanvas == null)
                return;

            if (forceCanvasActive && !targetCanvas.gameObject.activeSelf)
                targetCanvas.gameObject.SetActive(true);

            RectTransform rect = targetCanvas.transform as RectTransform;
            if (rect != null && rect.localScale == Vector3.zero)
                rect.localScale = Vector3.one;

            if (forceScreenSpaceOverlay)
            {
                targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                targetCanvas.worldCamera = null;
            }

            targetCanvas.overrideSorting = true;
            targetCanvas.sortingOrder = overlaySortingOrder;
        }

        private void EnsureOverlay()
        {
            debugOverlayReady = false;
            if (targetCanvas == null)
                return;

            Transform overlayTransform = _overlayRect != null && _appliedOverlayName == overlayName
                ? _overlayRect.transform
                : targetCanvas.transform.Find(overlayName);
            if (overlayTransform == null)
            {
                GameObject overlayObject = new GameObject(overlayName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                overlayObject.transform.SetParent(targetCanvas.transform, false);
                overlayTransform = overlayObject.transform;
            }

            if (showAsInsetPreview || !preserveExistingChildren)
                overlayTransform.SetAsLastSibling();
            else
                overlayTransform.SetAsFirstSibling();

            _overlayRect = overlayTransform as RectTransform;
            _overlayImage = overlayTransform.GetComponent<RawImage>();
            _appliedOverlayName = overlayName;

            if (_overlayRect == null || _overlayImage == null)
                return;

            if (!_overlayRect.gameObject.activeSelf)
                _overlayRect.gameObject.SetActive(true);

            if (showAsInsetPreview)
            {
                _overlayRect.anchorMin = new Vector2(1f, 1f);
                _overlayRect.anchorMax = new Vector2(1f, 1f);
                _overlayRect.pivot = new Vector2(1f, 1f);
                _overlayRect.sizeDelta = insetSize;
                _overlayRect.anchoredPosition = new Vector2(-insetMargin.x, -insetMargin.y);
            }
            else
            {
                _overlayRect.anchorMin = Vector2.zero;
                _overlayRect.anchorMax = Vector2.one;
                _overlayRect.offsetMin = Vector2.zero;
                _overlayRect.offsetMax = Vector2.zero;
                _overlayRect.anchoredPosition3D = Vector3.zero;
            }

            _overlayRect.localScale = Vector3.one;
            _overlayRect.localRotation = Quaternion.identity;

            Color color = new Color(0.92f, 1f, 0.96f, overlayAlpha);
            _overlayImage.color = color;
            _overlayImage.raycastTarget = false;
            debugOverlayReady = true;
        }

        private void EnsureProjection()
        {
            if (!forceSharedProjection || visorController == null)
                return;

            visorController.SetSharedRenderTexture(sharedProjectionTexture);
            visorController.SetProjectionMode(VisorHUDController.ProjectionMode.SharedRenderTexture);
        }

        private void BindTexture()
        {
            debugTextureAssigned = false;
            if (_overlayImage == null)
                return;

            _overlayImage.texture = sharedProjectionTexture;
            debugTextureAssigned = sharedProjectionTexture != null;
            _overlayImage.enabled = !hideWhenTextureMissing || sharedProjectionTexture != null;
        }

        private void TryRegisterRuntimeTick()
        {
            if (!Application.isPlaying || _tickRegistered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register((ITickable)this);
            _tickRegistered = true;
        }

        private void UnregisterRuntimeTick()
        {
            if (!_tickRegistered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister((ITickable)this);

            _tickRegistered = false;
        }

        public void SetSharedProjectionTexture(RenderTexture texture)
        {
            if (sharedProjectionTexture == texture)
                return;

            sharedProjectionTexture = texture;
            MarkDirty();
        }

        public void SetOverlayAlpha(float alpha)
        {
            float clampedAlpha = Mathf.Clamp01(alpha);
            if (Mathf.Approximately(overlayAlpha, clampedAlpha))
                return;

            overlayAlpha = clampedAlpha;
            MarkDirty();
        }

        private void MarkDirty()
        {
            _pendingRefresh = true;
            TryRegisterRuntimeTick();
        }

        private void RegisterActiveCompositor()
        {
            if (s_activeCompositors.Contains(this))
                return;

            s_activeCompositors.Add(this);
        }

        private void UnregisterActiveCompositor()
        {
            s_activeCompositors.Remove(this);
        }
    }
}
