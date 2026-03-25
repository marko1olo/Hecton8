using UnityEngine;
using UnityEngine.UI;

namespace NASAPunk.Visor
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [AddComponentMenu("Hecton8/HUD/Suit HUD Screen Compositor")]
    public sealed class SuitHUDScreenCompositor : MonoBehaviour
    {
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

        [Header("Diagnostics")]
        [SerializeField] private bool debugCanvasReady;
        [SerializeField] private bool debugOverlayReady;
        [SerializeField] private bool debugTextureAssigned;

        private RawImage _overlayImage;
        private RectTransform _overlayRect;

        private void OnEnable()
        {
            AutoResolveReferences();
            RefreshCompositor();
        }

        private void OnDisable()
        {
            if (_overlayImage != null)
                _overlayImage.enabled = false;

            if (_overlayRect != null)
                _overlayRect.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!Application.isPlaying && !manageCanvasInEditMode)
                return;

            RefreshCompositor();
        }

        private void RefreshCompositor()
        {
            AutoResolveReferences();
            EnsureCanvasState();
            EnsureOverlay();
            EnsureProjection();
            BindTexture();
        }

        private void AutoResolveReferences()
        {
            if (targetCanvas == null)
            {
                Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (canvases[i] != null && canvases[i].name == "Suit_HUD_Canvas")
                    {
                        targetCanvas = canvases[i];
                        break;
                    }
                }
            }

            if (visorController == null)
            {
                visorController = FindFirstObjectByType<VisorHUDController>(FindObjectsInactive.Include);
            }

            if (sharedProjectionTexture == null)
            {
                RenderTexture[] textures = Resources.FindObjectsOfTypeAll<RenderTexture>();
                for (int i = 0; i < textures.Length; i++)
                {
                    if (textures[i] != null && textures[i].name == "RT_HUD_Display")
                    {
                        sharedProjectionTexture = textures[i];
                        break;
                    }
                }
            }
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

            Transform overlayTransform = targetCanvas.transform.Find(overlayName);
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
    }
}
