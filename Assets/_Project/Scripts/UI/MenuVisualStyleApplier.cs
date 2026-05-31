using System;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    internal sealed class MenuVisualStyleApplier
    {
        private const byte GraphicRoleTransparent = 0;
        private const byte GraphicRoleBackground = 1;
        private const byte GraphicRolePanel = 2;
        private const byte GraphicRoleButton = 3;
        private const byte GraphicRolePrimaryText = 4;
        private const byte GraphicRoleSecondaryText = 5;
        private const byte GraphicRoleAccent = 6;
        private const float MinQualityReapplyDelta = 0.025f;
        private const float LowQualityRefreshSeconds = 0.80f;
        private const float HighQualityRefreshSeconds = 0.18f;

        private Graphic[] _graphics = Array.Empty<Graphic>();
        private Color[] _baseGraphicColors = Array.Empty<Color>();
        private byte[] _graphicRoles = Array.Empty<byte>();
        private Selectable[] _selectables = Array.Empty<Selectable>();
        private ColorBlock[] _baseColorBlocks = Array.Empty<ColorBlock>();
        private int _graphicCount;
        private int _selectableCount;
        private MenuVisualStyle _lastStyle = (MenuVisualStyle)byte.MaxValue;
        private float _lastQuality = -1f;
        private float _nextRefreshTime;
        private bool _forceApply;

        public void RebuildCache(Transform root)
        {
            RestoreCachedBaseState();

            _graphicCount = 0;
            _selectableCount = 0;
            _lastStyle = (MenuVisualStyle)byte.MaxValue;
            _lastQuality = -1f;
            _nextRefreshTime = 0f;
            _forceApply = true;

            if (root == null)
            {
                _graphics = Array.Empty<Graphic>();
                _baseGraphicColors = Array.Empty<Color>();
                _graphicRoles = Array.Empty<byte>();
                _selectables = Array.Empty<Selectable>();
                _baseColorBlocks = Array.Empty<ColorBlock>();
                return;
            }

            int graphicCount = 0;
            int selectableCount = 0;
            CountRecursive(root, ref graphicCount, ref selectableCount);

            EnsureGraphicCapacity(graphicCount);
            EnsureSelectableCapacity(selectableCount);
            FillRecursive(root);
        }

        public void Clear()
        {
            RestoreCachedBaseState();
            _graphics = Array.Empty<Graphic>();
            _baseGraphicColors = Array.Empty<Color>();
            _graphicRoles = Array.Empty<byte>();
            _selectables = Array.Empty<Selectable>();
            _baseColorBlocks = Array.Empty<ColorBlock>();
            _graphicCount = 0;
            _selectableCount = 0;
            ForceNextApply();
        }

        public void ForceNextApply()
        {
            _forceApply = true;
            _lastStyle = (MenuVisualStyle)byte.MaxValue;
            _lastQuality = -1f;
            _nextRefreshTime = 0f;
        }

        public void ApplyIfNeeded(MenuVisualStyle style, float globalQualityWeight01, float now)
        {
            float quality = MenuVisualStyleCatalog.Sanitize01(globalQualityWeight01, 1f);
            if (!_forceApply &&
                _lastStyle == style &&
                math.abs(_lastQuality - quality) < MinQualityReapplyDelta &&
                now < _nextRefreshTime)
            {
                return;
            }

            MenuVisualStyleCatalog.Resolve(style, quality, out MenuVisualStyleState state);
            float pulse = ResolveAmbiencePulse(now, state.InterferenceWeight, state.ScanlineWeight);
            float drift = ResolveAmbienceDrift(now, state.WetGlassWeight);
            ApplyGraphics(in state, pulse, drift);
            ApplySelectables(in state, pulse);

            _forceApply = false;
            _lastStyle = style;
            _lastQuality = quality;
            _nextRefreshTime = now + math.lerp(LowQualityRefreshSeconds, HighQualityRefreshSeconds, quality);
        }

        private void EnsureGraphicCapacity(int count)
        {
            if (count <= 0)
            {
                _graphics = Array.Empty<Graphic>();
                _baseGraphicColors = Array.Empty<Color>();
                _graphicRoles = Array.Empty<byte>();
                return;
            }

            if (_graphics.Length == count &&
                _baseGraphicColors.Length == count &&
                _graphicRoles.Length == count)
                return;

            _graphics = new Graphic[count]; // COLD ALLOC: exact menu Graphic cache rebuilt only when menu hierarchy is built/rewired.
            _baseGraphicColors = new Color[count]; // COLD ALLOC: base color cache for zero-GC visual sync writes.
            _graphicRoles = new byte[count]; // COLD ALLOC: compact style role map, no name checks in visual sync.
        }

        private void EnsureSelectableCapacity(int count)
        {
            if (count <= 0)
            {
                _selectables = Array.Empty<Selectable>();
                _baseColorBlocks = Array.Empty<ColorBlock>();
                return;
            }

            if (_selectables.Length == count && _baseColorBlocks.Length == count)
                return;

            _selectables = new Selectable[count]; // COLD ALLOC: exact menu Selectable cache rebuilt only when menu hierarchy is built/rewired.
            _baseColorBlocks = new ColorBlock[count]; // COLD ALLOC: color-block cache for zero-GC visual sync writes.
        }

        private void CountRecursive(Transform node, ref int graphicCount, ref int selectableCount)
        {
            if (node.TryGetComponent(out Graphic graphic) && graphic != null)
                graphicCount++;
            if (node.TryGetComponent(out Selectable selectable) && selectable != null)
                selectableCount++;

            int childCount = node.childCount;
            for (int i = 0; i < childCount; i++)
                CountRecursive(node.GetChild(i), ref graphicCount, ref selectableCount);
        }

        private void FillRecursive(Transform node)
        {
            Selectable selectable = null;
            node.TryGetComponent(out selectable);

            if (node.TryGetComponent(out Graphic graphic) && graphic != null)
            {
                Color baseColor = graphic.color;
                int index = _graphicCount++;
                _graphics[index] = graphic;
                _baseGraphicColors[index] = baseColor;
                _graphicRoles[index] = ResolveGraphicRole(graphic, selectable, baseColor);
            }

            if (selectable != null)
            {
                int index = _selectableCount++;
                _selectables[index] = selectable;
                _baseColorBlocks[index] = selectable.colors;
            }

            int childCount = node.childCount;
            for (int i = 0; i < childCount; i++)
                FillRecursive(node.GetChild(i));
        }

        private void RestoreCachedBaseState()
        {
            for (int i = 0; i < _graphicCount; i++)
            {
                Graphic graphic = _graphics[i];
                if (graphic != null && i < _baseGraphicColors.Length)
                    graphic.color = _baseGraphicColors[i];
            }

            for (int i = 0; i < _selectableCount; i++)
            {
                Selectable selectable = _selectables[i];
                if (selectable != null && i < _baseColorBlocks.Length)
                    selectable.colors = _baseColorBlocks[i];
            }
        }

        private void ApplyGraphics(in MenuVisualStyleState state, float pulse, float drift)
        {
            for (int i = 0; i < _graphicCount; i++)
            {
                Graphic graphic = _graphics[i];
                if (graphic == null)
                    continue;

                Color baseColor = _baseGraphicColors[i];
                Color target = ResolveGraphicColor(_graphicRoles[i], baseColor, in state, pulse, drift);
                target.a = math.saturate(target.a * baseColor.a);
                graphic.color = target;
            }
        }

        private void ApplySelectables(in MenuVisualStyleState state, float pulse)
        {
            float alertWeight = math.saturate(state.InterferenceWeight * (0.04f + pulse * 0.16f));
            float hoverWeight = math.saturate(state.TextGlowWeight * (0.03f + pulse * 0.08f));
            for (int i = 0; i < _selectableCount; i++)
            {
                Selectable selectable = _selectables[i];
                if (selectable == null)
                    continue;

                ColorBlock block = _baseColorBlocks[i];
                block.normalColor = PreserveAlpha(state.ButtonColor, block.normalColor.a);
                block.highlightedColor = PreserveAlpha(LerpColor(state.ButtonHoverColor, state.AccentColor, hoverWeight), block.highlightedColor.a);
                block.selectedColor = PreserveAlpha(LerpColor(state.AccentColor, state.WarningColor, alertWeight), block.selectedColor.a);
                block.pressedColor = PreserveAlpha(state.WarningColor, block.pressedColor.a);
                block.disabledColor = PreserveAlpha(state.SecondaryTextColor, block.disabledColor.a * 0.45f);
                selectable.colors = block;
            }
        }

        private static byte ResolveGraphicRole(Graphic graphic, Selectable selectable, Color baseColor)
        {
            if (baseColor.a <= 0.001f)
                return GraphicRoleTransparent;

            if (graphic is TMP_Text text)
            {
                if ((text.fontStyle & FontStyles.Bold) != 0 || text.fontSize >= 17f)
                    return GraphicRolePrimaryText;

                return GraphicRoleSecondaryText;
            }

            float max = math.max(baseColor.r, math.max(baseColor.g, baseColor.b));
            float min = math.min(baseColor.r, math.min(baseColor.g, baseColor.b));
            if (max <= 0.095f)
                return GraphicRoleBackground;
            if (selectable != null)
                return GraphicRoleButton;
            if (max - min >= 0.22f || baseColor.a <= 0.42f)
                return GraphicRoleAccent;

            return GraphicRolePanel;
        }

        private static Color ResolveGraphicColor(byte role, Color baseColor, in MenuVisualStyleState state, float pulse, float drift)
        {
            switch (role)
            {
                case GraphicRoleTransparent:
                    return new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
                case GraphicRoleBackground:
                    return state.BackgroundColor;
                case GraphicRoleButton:
                    return state.ButtonColor;
                case GraphicRolePrimaryText:
                    return LerpColor(state.PrimaryTextColor, state.AccentColor, state.TextGlowWeight * (0.12f + pulse * 0.10f));
                case GraphicRoleSecondaryText:
                    return LerpColor(state.SecondaryTextColor, state.PrimaryTextColor, state.TextGlowWeight * (0.07f + pulse * 0.08f));
                case GraphicRoleAccent:
                    return LerpColor(state.AccentColor, state.WarningColor, state.InterferenceWeight * (0.05f + pulse * 0.16f));
                default:
                    return LerpColor(state.PanelColor, state.ButtonColor, state.WetGlassWeight * (0.06f + drift * 0.12f));
            }
        }

        private static float ResolveAmbiencePulse(float now, float interferenceWeight, float scanlineWeight)
        {
            float weight = math.saturate(interferenceWeight * 0.65f + scanlineWeight * 0.35f);
            if (weight <= 0.001f)
                return 0f;

            float fast = 0.5f + 0.5f * math.sin(now * 13.0f);
            float slow = 0.5f + 0.5f * math.sin(now * 2.7f + 1.91f);
            return math.saturate((fast * 0.35f + slow * 0.65f) * weight);
        }

        private static float ResolveAmbienceDrift(float now, float wetGlassWeight)
        {
            float weight = math.saturate(wetGlassWeight);
            if (weight <= 0.001f)
                return 0f;

            float waveA = 0.5f + 0.5f * math.sin(now * 1.35f);
            float waveB = 0.5f + 0.5f * math.sin(now * 0.73f + 2.4f);
            return math.saturate((waveA * 0.45f + waveB * 0.55f) * weight);
        }

        private static Color PreserveAlpha(Color color, float alpha)
        {
            color.a = math.saturate(alpha);
            return color;
        }

        private static Color LerpColor(Color from, Color to, float t)
        {
            float weight = math.saturate(t);
            return new Color(
                math.lerp(from.r, to.r, weight),
                math.lerp(from.g, to.g, weight),
                math.lerp(from.b, to.b, weight),
                math.lerp(from.a, to.a, weight));
        }
    }
}
