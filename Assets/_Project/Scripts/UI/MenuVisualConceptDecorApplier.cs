using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    internal sealed class MenuVisualConceptDecorApplier
    {
        private const int SlotCount = 12;
        private const float LowQualityRefreshSeconds = 0.95f;
        private const float HighQualityRefreshSeconds = 0.22f;
        private const float MinQualityReapplyDelta = 0.025f;

        private readonly DecorSlot[] _slots = new DecorSlot[SlotCount]; // COLD ALLOC: fixed concept decor slot cache.
        private RectTransform _root;
        private CanvasGroup _group;
        private RectTransform _parent;
        private MenuVisualConcept _lastConcept = (MenuVisualConcept)byte.MaxValue;
        private MenuVisualStyle _lastStyle = (MenuVisualStyle)byte.MaxValue;
        private float _lastQuality = -1f;
        private float _nextRefreshTime;
        private bool _forceApply = true;

        public void Rebuild(RectTransform parent)
        {
            if (parent == null)
                return;

            if (_root == null || !ReferenceEquals(_parent, parent))
                CreateRoot(parent);

            Stretch(_root);
            _root.SetAsFirstSibling();
            ForceNextApply();
        }

        public void ForceNextApply()
        {
            _forceApply = true;
            _lastConcept = (MenuVisualConcept)byte.MaxValue;
            _lastStyle = (MenuVisualStyle)byte.MaxValue;
            _lastQuality = -1f;
            _nextRefreshTime = 0f;
        }

        public void ApplyIfNeeded(MenuVisualConcept concept, MenuVisualStyle style, float globalQualityWeight01, float now)
        {
            if (_root == null)
                return;

            float quality = MenuVisualStyleCatalog.Sanitize01(globalQualityWeight01, 1f);
            if (!_forceApply &&
                _lastConcept == concept &&
                _lastStyle == style &&
                math.abs(_lastQuality - quality) < MinQualityReapplyDelta &&
                now < _nextRefreshTime)
            {
                return;
            }

            MenuVisualStyleCatalog.Resolve(style, quality, out MenuVisualStyleState styleState);
            MenuVisualConceptCatalog.Resolve(concept, quality, out MenuVisualConceptState conceptState);

            ConfigureConcept(concept, in styleState, in conceptState, quality, now);

            _forceApply = false;
            _lastConcept = concept;
            _lastStyle = style;
            _lastQuality = quality;
            _nextRefreshTime = now + math.lerp(LowQualityRefreshSeconds, HighQualityRefreshSeconds, quality);
        }

        private void CreateRoot(RectTransform parent)
        {
            _parent = parent;

            if (_root == null)
            {
                GameObject rootObject = new GameObject("MenuConceptDecorRoot", typeof(RectTransform), typeof(CanvasGroup)); // COLD ALLOC: menu concept decor layer.
                _root = (RectTransform)rootObject.transform;
                _group = rootObject.GetComponent<CanvasGroup>();
            }

            _root.SetParent(parent, false);
            _root.localScale = Vector3.one;
            _root.localRotation = Quaternion.identity;
            _root.gameObject.layer = parent.gameObject.layer;

            if (_group == null)
                _root.gameObject.TryGetComponent(out _group);

            _group.alpha = 1f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
            _group.ignoreParentGroups = false;

            for (int i = 0; i < SlotCount; i++)
                EnsureSlot(i);
        }

        private static readonly string[] SlotNames = new string[12]
        {
            "Decor_TopRail", "Decor_BottomRail", "Decor_LeftRail", "Decor_RightRail",
            "Decor_AngleA", "Decor_AngleB", "Decor_MarkerA", "Decor_MarkerB",
            "Decor_MarkerC", "Decor_MarkerD", "Decor_Scanline", "Decor_Horizon"
        };

        private void EnsureSlot(int index)
        {
            DecorSlot slot = _slots[index];
            if (slot.Rect != null && slot.Image != null)
                return;

            GameObject slotObject = new GameObject(SlotNames[index], typeof(RectTransform), typeof(Image)); // COLD ALLOC: fixed menu concept decor primitive.
            slotObject.transform.SetParent(_root, false);
            slotObject.layer = _root.gameObject.layer;

            RectTransform rect = (RectTransform)slotObject.transform;
            slotObject.TryGetComponent(out Image image);
            image.raycastTarget = false;
            image.color = Color.clear;

            _slots[index] = new DecorSlot(rect, image);
        }

        private void ConfigureConcept(
            MenuVisualConcept concept,
            in MenuVisualStyleState style,
            in MenuVisualConceptState conceptState,
            float quality,
            float now)
        {
            ClearSlots();

            Color primary = WithAlpha(style.PrimaryTextColor, ResolveAlpha(0.16f, 0.34f, quality, style.TextGlowWeight));
            Color secondary = WithAlpha(style.SecondaryTextColor, ResolveAlpha(0.10f, 0.24f, quality, style.ScanlineWeight));
            Color accent = WithAlpha(style.AccentColor, ResolveAlpha(0.13f, 0.32f, quality, style.InterferenceWeight));
            Color warning = WithAlpha(style.WarningColor, ResolveAlpha(0.18f, 0.44f, quality, conceptState.WarningBias));
            Color panel = WithAlpha(style.PanelColor, ResolveAlpha(0.12f, 0.28f, quality, style.WetGlassWeight));

            float pulse = ResolvePulse(now, conceptState.MicroMotion, conceptState.WarningBias);
            float sweep = math.fmod(now * (18f + quality * 42f), 360f);

            switch (concept)
            {
                case MenuVisualConcept.CaptainPdaDock:
                    ConfigurePdaDock(primary, secondary, accent, pulse);
                    return;
                case MenuVisualConcept.HelmetVisorRing:
                    ConfigureVisorRing(primary, accent, secondary, pulse);
                    return;
                case MenuVisualConcept.BlackboxPlayback:
                    ConfigureBlackbox(primary, secondary, warning, pulse);
                    return;
                case MenuVisualConcept.SonarPlotter:
                    ConfigureSonar(primary, accent, secondary, sweep);
                    return;
                case MenuVisualConcept.EmergencyBulkheadPanel:
                    ConfigureBulkhead(warning, accent, panel, pulse);
                    return;
                case MenuVisualConcept.MaintenanceClipboard:
                    ConfigureClipboard(primary, secondary, accent);
                    return;
                case MenuVisualConcept.CargoManifestBoard:
                    ConfigureManifest(primary, secondary, panel);
                    return;
                case MenuVisualConcept.DiveLogLedger:
                    ConfigureLedger(primary, secondary, accent);
                    return;
                case MenuVisualConcept.ReactorConsole:
                    ConfigureReactor(primary, accent, warning, pulse);
                    return;
                case MenuVisualConcept.TrenchMapTable:
                    ConfigureMapTable(primary, secondary, accent);
                    return;
                case MenuVisualConcept.QuarantineEvidenceWall:
                    ConfigureEvidenceWall(primary, warning, accent, pulse);
                    return;
                default:
                    ConfigureModuleOverlay(primary, secondary, panel);
                    return;
            }
        }

        private void ConfigureModuleOverlay(Color primary, Color secondary, Color panel)
        {
            SetSlot(0, new Vector2(0.08f, 1f), new Vector2(0.92f, 1f), new Vector2(0f, -24f), new Vector2(0f, 3f), 0f, primary);
            SetSlot(1, new Vector2(0.08f, 0f), new Vector2(0.92f, 0f), new Vector2(0f, 24f), new Vector2(0f, 3f), 0f, secondary);
            SetSlot(2, new Vector2(0f, 0.18f), new Vector2(0f, 0.82f), new Vector2(32f, 0f), new Vector2(3f, 0f), 0f, panel);
            SetSlot(3, new Vector2(1f, 0.18f), new Vector2(1f, 0.82f), new Vector2(-32f, 0f), new Vector2(3f, 0f), 0f, panel);
        }

        private void ConfigurePdaDock(Color primary, Color secondary, Color accent, float pulse)
        {
            SetSlot(2, new Vector2(0f, 0.05f), new Vector2(0f, 0.95f), new Vector2(38f, 0f), new Vector2(4f, 0f), 0f, primary);
            SetSlot(3, new Vector2(1f, 0.16f), new Vector2(1f, 0.84f), new Vector2(-78f, 0f), new Vector2(26f + pulse * 12f, 0f), 0f, accent);
            SetSlot(0, new Vector2(0.07f, 1f), new Vector2(0.60f, 1f), new Vector2(0f, -42f), new Vector2(0f, 3f), 0f, secondary);
            SetSlot(6, new Vector2(1f, 0.76f), new Vector2(1f, 0.76f), new Vector2(-118f, 0f), new Vector2(70f, 4f), 0f, accent);
            SetSlot(7, new Vector2(1f, 0.24f), new Vector2(1f, 0.24f), new Vector2(-118f, 0f), new Vector2(70f, 4f), 0f, accent);
        }

        private void ConfigureVisorRing(Color primary, Color accent, Color secondary, float pulse)
        {
            float bracket = 84f + pulse * 18f;
            SetCornerBracket(0, new Vector2(0f, 1f), new Vector2(42f, -42f), bracket, 0f, primary);
            SetCornerBracket(2, new Vector2(1f, 1f), new Vector2(-42f, -42f), bracket, 90f, accent);
            SetCornerBracket(4, new Vector2(0f, 0f), new Vector2(42f, 42f), bracket, -90f, accent);
            SetCornerBracket(6, new Vector2(1f, 0f), new Vector2(-42f, 42f), bracket, 180f, primary);
            SetSlot(10, new Vector2(0.50f, 0.50f), new Vector2(0.50f, 0.50f), Vector2.zero, new Vector2(520f, 2f), 0f, secondary);
            SetSlot(11, new Vector2(0.50f, 0.50f), new Vector2(0.50f, 0.50f), Vector2.zero, new Vector2(2f, 300f), 0f, secondary);
        }

        private void ConfigureBlackbox(Color primary, Color secondary, Color warning, float pulse)
        {
            SetSlot(1, new Vector2(0.08f, 0f), new Vector2(0.92f, 0f), new Vector2(0f, 54f), new Vector2(0f, 5f), 0f, primary);
            SetSlot(10, new Vector2(0.14f, 0f), new Vector2(0.88f, 0f), new Vector2(0f, 82f + pulse * 10f), new Vector2(0f, 2f), 0f, warning);
            SetSlot(2, new Vector2(0f, 0.12f), new Vector2(0f, 0.88f), new Vector2(70f, 0f), new Vector2(3f, 0f), 0f, secondary);
            SetSlot(4, new Vector2(0.28f, 0f), new Vector2(0.28f, 0f), new Vector2(0f, 70f), new Vector2(3f, 90f), 0f, secondary);
            SetSlot(5, new Vector2(0.52f, 0f), new Vector2(0.52f, 0f), new Vector2(0f, 70f), new Vector2(3f, 124f), 0f, secondary);
            SetSlot(6, new Vector2(0.76f, 0f), new Vector2(0.76f, 0f), new Vector2(0f, 70f), new Vector2(3f, 70f), 0f, warning);
        }

        private void ConfigureSonar(Color primary, Color accent, Color secondary, float sweep)
        {
            SetSlot(10, new Vector2(0.50f, 0.50f), new Vector2(0.50f, 0.50f), Vector2.zero, new Vector2(760f, 2f), sweep, accent);
            SetSlot(11, new Vector2(0.50f, 0.50f), new Vector2(0.50f, 0.50f), Vector2.zero, new Vector2(2f, 520f), 0f, secondary);
            SetSlot(0, new Vector2(0.18f, 0.50f), new Vector2(0.82f, 0.50f), Vector2.zero, new Vector2(0f, 2f), 0f, secondary);
            SetSlot(2, new Vector2(0.50f, 0.18f), new Vector2(0.50f, 0.82f), Vector2.zero, new Vector2(2f, 0f), 0f, secondary);
            SetSlot(6, new Vector2(0.50f, 0.50f), new Vector2(0.50f, 0.50f), new Vector2(-210f, 130f), new Vector2(78f, 3f), 16f, primary);
            SetSlot(7, new Vector2(0.50f, 0.50f), new Vector2(0.50f, 0.50f), new Vector2(260f, -80f), new Vector2(110f, 3f), -12f, primary);
        }

        private void ConfigureBulkhead(Color warning, Color accent, Color panel, float pulse)
        {
            SetSlot(0, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -34f), new Vector2(0f, 18f + pulse * 4f), 0f, warning);
            SetSlot(1, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 34f), new Vector2(0f, 18f + pulse * 4f), 0f, warning);
            SetSlot(2, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(42f, 0f), new Vector2(16f, 0f), 0f, panel);
            SetSlot(3, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-42f, 0f), new Vector2(16f, 0f), 0f, panel);
            SetSlot(4, new Vector2(0.20f, 1f), new Vector2(0.20f, 1f), new Vector2(0f, -34f), new Vector2(6f, 132f), -35f, accent);
            SetSlot(5, new Vector2(0.80f, 0f), new Vector2(0.80f, 0f), new Vector2(0f, 34f), new Vector2(6f, 132f), -35f, accent);
        }

        private void ConfigureClipboard(Color primary, Color secondary, Color accent)
        {
            SetSlot(0, new Vector2(0.38f, 1f), new Vector2(0.62f, 1f), new Vector2(0f, -48f), new Vector2(0f, 18f), 0f, accent);
            SetSlot(2, new Vector2(0f, 0.10f), new Vector2(0f, 0.90f), new Vector2(88f, 0f), new Vector2(6f, 0f), 0f, primary);
            SetSlot(6, new Vector2(0.16f, 0.70f), new Vector2(0.80f, 0.70f), Vector2.zero, new Vector2(0f, 2f), 0f, secondary);
            SetSlot(7, new Vector2(0.16f, 0.58f), new Vector2(0.73f, 0.58f), Vector2.zero, new Vector2(0f, 2f), 0f, secondary);
            SetSlot(8, new Vector2(0.16f, 0.46f), new Vector2(0.84f, 0.46f), Vector2.zero, new Vector2(0f, 2f), 0f, secondary);
            SetSlot(9, new Vector2(0.16f, 0.34f), new Vector2(0.68f, 0.34f), Vector2.zero, new Vector2(0f, 2f), 0f, secondary);
        }

        private void ConfigureManifest(Color primary, Color secondary, Color panel)
        {
            SetSlot(2, new Vector2(0.18f, 0.12f), new Vector2(0.18f, 0.88f), Vector2.zero, new Vector2(2f, 0f), 0f, primary);
            SetSlot(3, new Vector2(0.72f, 0.12f), new Vector2(0.72f, 0.88f), Vector2.zero, new Vector2(2f, 0f), 0f, secondary);
            SetSlot(0, new Vector2(0.10f, 0.82f), new Vector2(0.90f, 0.82f), Vector2.zero, new Vector2(0f, 3f), 0f, primary);
            SetSlot(6, new Vector2(0.10f, 0.66f), new Vector2(0.90f, 0.66f), Vector2.zero, new Vector2(0f, 2f), 0f, panel);
            SetSlot(7, new Vector2(0.10f, 0.50f), new Vector2(0.90f, 0.50f), Vector2.zero, new Vector2(0f, 2f), 0f, panel);
            SetSlot(8, new Vector2(0.10f, 0.34f), new Vector2(0.90f, 0.34f), Vector2.zero, new Vector2(0f, 2f), 0f, panel);
        }

        private void ConfigureLedger(Color primary, Color secondary, Color accent)
        {
            SetSlot(2, new Vector2(0.14f, 0.08f), new Vector2(0.14f, 0.92f), Vector2.zero, new Vector2(4f, 0f), 0f, accent);
            SetSlot(3, new Vector2(0.50f, 0.10f), new Vector2(0.50f, 0.90f), Vector2.zero, new Vector2(2f, 0f), 0f, secondary);
            SetSlot(6, new Vector2(0.18f, 0.76f), new Vector2(0.84f, 0.76f), Vector2.zero, new Vector2(0f, 2f), 0f, primary);
            SetSlot(7, new Vector2(0.18f, 0.62f), new Vector2(0.84f, 0.62f), Vector2.zero, new Vector2(0f, 2f), 0f, secondary);
            SetSlot(8, new Vector2(0.18f, 0.48f), new Vector2(0.84f, 0.48f), Vector2.zero, new Vector2(0f, 2f), 0f, secondary);
            SetSlot(9, new Vector2(0.18f, 0.34f), new Vector2(0.84f, 0.34f), Vector2.zero, new Vector2(0f, 2f), 0f, secondary);
        }

        private void ConfigureReactor(Color primary, Color accent, Color warning, float pulse)
        {
            SetSlot(1, new Vector2(0.08f, 0f), new Vector2(0.92f, 0f), new Vector2(0f, 74f), new Vector2(0f, 12f), 0f, accent);
            SetSlot(10, new Vector2(0.50f, 0f), new Vector2(0.50f, 0f), new Vector2(0f, 118f), new Vector2(260f + pulse * 70f, 4f), 0f, warning);
            SetSlot(2, new Vector2(0.34f, 0f), new Vector2(0.34f, 0.34f), Vector2.zero, new Vector2(3f, 0f), 0f, primary);
            SetSlot(3, new Vector2(0.66f, 0f), new Vector2(0.66f, 0.34f), Vector2.zero, new Vector2(3f, 0f), 0f, primary);
            SetSlot(4, new Vector2(0.18f, 0f), new Vector2(0.18f, 0.28f), Vector2.zero, new Vector2(2f, 0f), -12f, accent);
            SetSlot(5, new Vector2(0.82f, 0f), new Vector2(0.82f, 0.28f), Vector2.zero, new Vector2(2f, 0f), 12f, accent);
        }

        private void ConfigureMapTable(Color primary, Color secondary, Color accent)
        {
            SetSlot(0, new Vector2(0.12f, 0.72f), new Vector2(0.88f, 0.72f), Vector2.zero, new Vector2(0f, 2f), 0f, secondary);
            SetSlot(1, new Vector2(0.12f, 0.42f), new Vector2(0.88f, 0.42f), Vector2.zero, new Vector2(0f, 2f), 0f, secondary);
            SetSlot(2, new Vector2(0.30f, 0.14f), new Vector2(0.30f, 0.86f), Vector2.zero, new Vector2(2f, 0f), 0f, secondary);
            SetSlot(3, new Vector2(0.66f, 0.14f), new Vector2(0.66f, 0.86f), Vector2.zero, new Vector2(2f, 0f), 0f, secondary);
            SetSlot(4, new Vector2(0.25f, 0.25f), new Vector2(0.25f, 0.25f), new Vector2(0f, 0f), new Vector2(360f, 3f), 24f, primary);
            SetSlot(5, new Vector2(0.73f, 0.72f), new Vector2(0.73f, 0.72f), new Vector2(0f, 0f), new Vector2(300f, 3f), -38f, accent);
        }

        private void ConfigureEvidenceWall(Color primary, Color warning, Color accent, float pulse)
        {
            SetSlot(4, new Vector2(0.22f, 0.72f), new Vector2(0.22f, 0.72f), Vector2.zero, new Vector2(520f, 3f), -24f, primary);
            SetSlot(5, new Vector2(0.78f, 0.68f), new Vector2(0.78f, 0.68f), Vector2.zero, new Vector2(470f, 3f), 34f, warning);
            SetSlot(6, new Vector2(0.18f, 0.78f), new Vector2(0.18f, 0.78f), Vector2.zero, new Vector2(82f + pulse * 20f, 8f), 0f, accent);
            SetSlot(7, new Vector2(0.76f, 0.68f), new Vector2(0.76f, 0.68f), Vector2.zero, new Vector2(96f + pulse * 24f, 8f), 0f, warning);
            SetSlot(8, new Vector2(0.30f, 0.28f), new Vector2(0.30f, 0.28f), Vector2.zero, new Vector2(74f, 8f), 0f, primary);
            SetSlot(9, new Vector2(0.66f, 0.34f), new Vector2(0.66f, 0.34f), Vector2.zero, new Vector2(90f, 8f), 0f, accent);
        }

        private void SetCornerBracket(int firstSlot, Vector2 anchor, Vector2 position, float length, float rotation, Color color)
        {
            SetSlot(firstSlot, anchor, anchor, position, new Vector2(length, 3f), rotation, color);
            SetSlot(firstSlot + 1, anchor, anchor, position, new Vector2(3f, length), rotation, color);
        }

        private void SetSlot(
            int index,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            float rotationZ,
            Color color)
        {
            if ((uint)index >= SlotCount)
                return;

            DecorSlot slot = _slots[index];
            if (slot.Rect == null || slot.Image == null)
                return;

            RectTransform rect = slot.Rect;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.Euler(0f, 0f, rotationZ);

            slot.Image.enabled = color.a > 0.001f;
            slot.Image.color = color;
        }

        private void ClearSlots()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                DecorSlot slot = _slots[i];
                if (slot.Image != null)
                {
                    slot.Image.enabled = false;
                    slot.Image.color = Color.clear;
                }
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static float ResolveAlpha(float low, float high, float quality, float weight)
        {
            return math.saturate(math.lerp(low, high, quality) * (1f + weight * 0.35f));
        }

        private static float ResolvePulse(float now, float microMotion, float warningBias)
        {
            float weight = math.saturate(microMotion + warningBias * 0.35f);
            if (weight <= 0.001f)
                return 0f;

            return (0.5f + 0.5f * math.sin(now * 4.7f + 1.1f)) * weight;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = math.saturate(alpha);
            return color;
        }

        private readonly struct DecorSlot
        {
            public readonly RectTransform Rect;
            public readonly Image Image;

            public DecorSlot(RectTransform rect, Image image)
            {
                Rect = rect;
                Image = image;
            }
        }
    }
}
