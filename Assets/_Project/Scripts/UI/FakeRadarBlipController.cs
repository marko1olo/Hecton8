using Hecton8.AI;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.World;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// HUD-only enemy radar fake: spatial hash contacts, flat XZ math, fixed UI pool.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Fake Radar Blip Controller")]
    public sealed class FakeRadarBlipController : MonoBehaviour, IUpdatable
    {
        private const int MaxBlips = 20;
        private const int MaxCanvasChildProbeCount = 64;
        private const float DefaultRadarRangeMeters = 100f;
        private const float DefaultRadarRadiusPixels = 74f;
        private const float RootSizePixels = 188f;
        private const float BlipSizePixels = 7f;
        private const float HiddenAlphaCutoff = 0.001f;
        private const string RootName = "FakeRadarBlips";

        private static readonly Color BlipColor = new Color(1f, 0.24f, 0.28f, 0.92f);
        private static readonly Color FrameColor = new Color(0.48f, 0.95f, 0.92f, 0.12f);

        private static Sprite s_quadSprite;

        [SerializeField, Min(1f)] private float radarRangeMeters = DefaultRadarRangeMeters;
        [SerializeField, Min(1f)] private float radarRadiusPixels = DefaultRadarRadiusPixels;

        // COLD ALLOC: SpatialQueryHit[20] — fixed hostile radar query buffer — owner: FakeRadarBlipController
        private readonly SpatialQueryHit[] _queryHits = new SpatialQueryHit[MaxBlips];

        private bool _registered;
        private bool _uiBuilt;
        private Transform _playerTransform;
        private RectTransform _root;
        private CanvasGroup _canvasGroup;
        private RectTransform[] _blipRects;
        private Image[] _blipImages;
        private float _lastRootAlpha = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_quadSprite = null;
        }

        private void OnEnable()
        {
            ResolvePlayerTransform();
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
            HideBlips();
            ApplyRootAlpha(0f);
        }

        private void OnDestroy()
        {
            TryUnregister();
        }

        public void Tick(float deltaTime)
        {
            ResolvePlayerTransform();
            EnsureUiBuilt();

            if (_playerTransform == null || _root == null || _canvasGroup == null)
            {
                HideBlips();
                ApplyRootAlpha(0f);
                return;
            }

            Vector3 playerPosition = _playerTransform.position;
            AbsoluteUniversePosition playerAup = AbsoluteUniversePosition.FromRuntimePosition(playerPosition);
            int hitCount = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                in playerAup,
                radarRangeMeters,
                SpatialTargetKind.Bioform,
                _queryHits);

            float range = Mathf.Max(1f, radarRangeMeters);
            float rangeSqr = range * range;
            float radius = Mathf.Max(1f, radarRadiusPixels);
            int visibleCount = 0;

            for (int i = 0; i < hitCount && visibleCount < MaxBlips; i++)
            {
                SpatialQueryHit hit = _queryHits[i];
                if (!(hit.Owner is FaunaBrain brain) || !brain.isAggressive)
                    continue;

                Vector3 enemyPosition = hit.Position;
                Vector2 flatDelta = new Vector2(
                    enemyPosition.x - playerPosition.x,
                    enemyPosition.z - playerPosition.z);
                if (!TryResolveRadarPosition(flatDelta, rangeSqr, range, radius, out Vector2 anchoredPosition))
                    continue;

                ApplyBlip(visibleCount, anchoredPosition);
                visibleCount++;
            }

            for (int i = visibleCount; i < MaxBlips; i++)
                HideBlip(i);

            ApplyRootAlpha(visibleCount > 0 ? 1f : 0f);
        }

        private static bool TryResolveRadarPosition(
            Vector2 flatDelta,
            float rangeSqr,
            float range,
            float radius,
            out Vector2 anchoredPosition)
        {
            anchoredPosition = default;
            float distanceSqr = flatDelta.sqrMagnitude;
            if (distanceSqr <= 0.0001f || distanceSqr > rangeSqr)
                return false;

            Vector2 normalized = flatDelta / range;
            if (normalized.sqrMagnitude > 1f)
                normalized.Normalize();

            anchoredPosition = normalized * radius;
            return true;
        }

        private void ResolvePlayerTransform()
        {
            if (_playerTransform != null)
                return;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null && playerContext.PlayerTransform != null)
            {
                _playerTransform = playerContext.PlayerTransform;
                return;
            }

            SceneBootstrap.TryGetCurrentPlayerTransform(out _playerTransform);
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
                GameObject rootObject = new GameObject(RootName, typeof(RectTransform), typeof(CanvasGroup));
                rootObject.layer = canvasRoot.gameObject.layer;
                _root = rootObject.GetComponent<RectTransform>();
                _root.SetParent(canvasRoot, false);
            }

            _root.anchorMin = new Vector2(1f, 0f);
            _root.anchorMax = new Vector2(1f, 0f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.anchoredPosition = new Vector2(-142f, 132f);
            _root.sizeDelta = new Vector2(RootSizePixels, RootSizePixels);
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
            CreateBlips();
            _uiBuilt = true;
        }

        private void CreateFrame()
        {
            Image horizontalRule = EnsureImage(CreateRect(_root, "RadarRuleH").gameObject);
            horizontalRule.sprite = ResolveQuadSprite();
            horizontalRule.color = FrameColor;
            horizontalRule.raycastTarget = false;
            horizontalRule.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            horizontalRule.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            horizontalRule.rectTransform.sizeDelta = new Vector2(0f, 1f);

            Image verticalRule = EnsureImage(CreateRect(_root, "RadarRuleV").gameObject);
            verticalRule.sprite = ResolveQuadSprite();
            verticalRule.color = FrameColor;
            verticalRule.raycastTarget = false;
            verticalRule.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            verticalRule.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            verticalRule.rectTransform.sizeDelta = new Vector2(1f, 0f);
        }

        private void CreateBlips()
        {
            // COLD ALLOC: RectTransform[20] — prebuilt fake radar blip rects — owner: FakeRadarBlipController
            _blipRects = new RectTransform[MaxBlips];
            // COLD ALLOC: Image[20] — prebuilt fake radar blip images — owner: FakeRadarBlipController
            _blipImages = new Image[MaxBlips];

            for (int i = 0; i < MaxBlips; i++)
            {
                RectTransform blipRect = CreateRect(_root, "FakeRadarBlip_" + i);
                blipRect.anchorMin = new Vector2(0.5f, 0.5f);
                blipRect.anchorMax = new Vector2(0.5f, 0.5f);
                blipRect.pivot = new Vector2(0.5f, 0.5f);
                blipRect.sizeDelta = new Vector2(BlipSizePixels, BlipSizePixels);

                Image blipImage = EnsureImage(blipRect.gameObject);
                blipImage.sprite = ResolveQuadSprite();
                blipImage.color = Color.clear;
                blipImage.raycastTarget = false;

                _blipRects[i] = blipRect;
                _blipImages[i] = blipImage;
            }
        }

        private void ApplyBlip(int index, Vector2 anchoredPosition)
        {
            if (_blipRects == null || _blipImages == null || (uint)index >= (uint)_blipRects.Length)
                return;

            RectTransform blipRect = _blipRects[index];
            Image blipImage = _blipImages[index];
            if (blipRect == null || blipImage == null)
                return;

            blipRect.anchoredPosition = anchoredPosition;
            if (blipImage.color.a < HiddenAlphaCutoff)
                blipImage.color = BlipColor;
        }

        private void HideBlips()
        {
            if (_blipImages == null)
                return;

            for (int i = 0; i < _blipImages.Length; i++)
                HideBlip(i);
        }

        private void HideBlip(int index)
        {
            if (_blipImages == null || (uint)index >= (uint)_blipImages.Length)
                return;

            Image image = _blipImages[index];
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

        private static Canvas ResolveTargetCanvas()
        {
            SuitHUDV4CanvasOverlay overlay = SuitHUDV4CanvasOverlay.ActiveRuntimeInstance;
            if (overlay != null && overlay.TargetCanvas != null)
                return overlay.TargetCanvas;

            return overlay != null ? overlay.GetComponent<Canvas>() : null;
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
            s_quadSprite.name = "FakeRadarBlipQuad";
            return s_quadSprite;
        }

        private static RectTransform FindExistingChild(Transform parent, string name)
        {
            if (parent == null)
                return null;

            int childCount = Mathf.Min(parent.childCount, MaxCanvasChildProbeCount);
            for (int i = 0; i < childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name)
                    return child as RectTransform;
            }

            return null;
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

        private static void ClearChildren(Transform parent)
        {
            int childCount = Mathf.Min(parent.childCount, MaxCanvasChildProbeCount);
            for (int i = childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (Application.isPlaying)
                    Object.Destroy(child.gameObject);
                else
                    Object.DestroyImmediate(child.gameObject);
            }
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
