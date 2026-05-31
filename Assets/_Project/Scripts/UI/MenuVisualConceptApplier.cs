using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    internal enum MenuVisualConceptTargetRole : byte
    {
        Shell = 0,
        Header = 1,
        Content = 2,
        MainPanel = 3,
        SavesPanel = 4,
        HelpPanel = 5,
        SettingsPanel = 6,
        LoadingPanel = 7
    }

    internal sealed class MenuVisualConceptApplier
    {
        private const int MaxTargets = 10;
        private const float LowQualityRefreshSeconds = 0.90f;
        private const float HighQualityRefreshSeconds = 0.20f;
        private const float MinQualityReapplyDelta = 0.025f;

        private readonly TargetState[] _targets = new TargetState[MaxTargets]; // COLD ALLOC: fixed concept transform cache.
        private int _targetCount;
        private MenuVisualConcept _lastConcept = (MenuVisualConcept)byte.MaxValue;
        private float _lastQuality = -1f;
        private float _nextRefreshTime;
        private bool _forceApply = true;

        public void Clear()
        {
            for (int i = 0; i < _targetCount; i++)
            {
                RestoreTarget(in _targets[i]);
                _targets[i] = default;
            }

            _targetCount = 0;
            ForceNextApply();
        }

        public void AddTarget(MenuVisualConceptTargetRole role, RectTransform rect)
        {
            if (rect == null || _targetCount >= MaxTargets)
                return;

            Vector3 localEuler = rect.localEulerAngles;
            _targets[_targetCount++] = new TargetState(
                rect,
                role,
                rect.anchoredPosition,
                rect.localScale,
                localEuler.z);
            ForceNextApply();
        }

        public void ForceNextApply()
        {
            _forceApply = true;
            _lastConcept = (MenuVisualConcept)byte.MaxValue;
            _lastQuality = -1f;
            _nextRefreshTime = 0f;
        }

        public void ApplyIfNeeded(MenuVisualConcept concept, float globalQualityWeight01, float now)
        {
            float quality = MenuVisualStyleCatalog.Sanitize01(globalQualityWeight01, 1f);
            if (!_forceApply &&
                _lastConcept == concept &&
                math.abs(_lastQuality - quality) < MinQualityReapplyDelta &&
                now < _nextRefreshTime)
            {
                return;
            }

            MenuVisualConceptCatalog.Resolve(concept, quality, out MenuVisualConceptState state);
            float pulse = ResolvePulse(now, state.MicroMotion, state.WarningBias);
            for (int i = 0; i < _targetCount; i++)
                ApplyTarget(in _targets[i], in state, pulse);

            _forceApply = false;
            _lastConcept = concept;
            _lastQuality = quality;
            _nextRefreshTime = now + math.lerp(LowQualityRefreshSeconds, HighQualityRefreshSeconds, quality);
        }

        private static void ApplyTarget(in TargetState target, in MenuVisualConceptState state, float pulse)
        {
            RectTransform rect = target.Rect;
            if (rect == null)
                return;

            ResolveRoleTransform(target.Role, in state, out Vector2 offset, out float scale, out float rotation);
            float motion = state.MicroMotion * pulse;
            offset.x += ResolveRoleMotionX(target.Role, motion);
            offset.y += ResolveRoleMotionY(target.Role, motion);
            rotation += ResolveRoleMotionRotation(target.Role, motion, state.WarningBias);

            rect.anchoredPosition = target.BaseAnchoredPosition + offset;
            rect.localScale = new Vector3(target.BaseScale.x * scale, target.BaseScale.y * scale, target.BaseScale.z);
            rect.localRotation = Quaternion.Euler(0f, 0f, target.BaseRotationZ + rotation);
        }

        private static void RestoreTarget(in TargetState target)
        {
            RectTransform rect = target.Rect;
            if (rect == null)
                return;

            rect.anchoredPosition = target.BaseAnchoredPosition;
            rect.localScale = target.BaseScale;
            rect.localRotation = Quaternion.Euler(0f, 0f, target.BaseRotationZ);
        }

        private static void ResolveRoleTransform(
            MenuVisualConceptTargetRole role,
            in MenuVisualConceptState state,
            out Vector2 offset,
            out float scale,
            out float rotation)
        {
            switch (role)
            {
                case MenuVisualConceptTargetRole.Shell:
                    offset = state.ShellOffset;
                    scale = state.ShellScale;
                    rotation = state.ShellRotation;
                    return;
                case MenuVisualConceptTargetRole.Header:
                    offset = state.HeaderOffset;
                    scale = state.HeaderScale;
                    rotation = state.HeaderRotation;
                    return;
                case MenuVisualConceptTargetRole.Content:
                    offset = state.ContentOffset;
                    scale = 1f;
                    rotation = 0f;
                    return;
                case MenuVisualConceptTargetRole.SavesPanel:
                    offset = state.PanelOffset + new Vector2(state.PanelSpread, state.PanelStack);
                    scale = state.PanelScale;
                    rotation = state.PanelRotation * -0.6f;
                    return;
                case MenuVisualConceptTargetRole.HelpPanel:
                    offset = state.PanelOffset + new Vector2(-state.PanelSpread, state.PanelStack * 0.45f);
                    scale = state.PanelScale;
                    rotation = state.PanelRotation * 0.75f;
                    return;
                case MenuVisualConceptTargetRole.SettingsPanel:
                    offset = state.PanelOffset + new Vector2(state.PanelSpread * 0.45f, -state.PanelStack * 0.55f);
                    scale = state.PanelScale;
                    rotation = state.PanelRotation * 0.35f;
                    return;
                case MenuVisualConceptTargetRole.LoadingPanel:
                    offset = state.PanelOffset + new Vector2(0f, state.PanelStack * 0.85f);
                    scale = state.PanelScale;
                    rotation = state.PanelRotation * -0.25f;
                    return;
                default:
                    offset = state.PanelOffset + new Vector2(-state.PanelSpread * 0.35f, 0f);
                    scale = state.PanelScale;
                    rotation = state.PanelRotation;
                    return;
            }
        }

        private static float ResolvePulse(float now, float microMotion, float warningBias)
        {
            float weight = math.saturate(microMotion + warningBias * 0.35f);
            if (weight <= 0.001f)
                return 0f;

            float slow = 0.5f + 0.5f * math.sin(now * 1.07f + 0.4f);
            float fast = 0.5f + 0.5f * math.sin(now * 5.3f + 2.2f);
            return math.saturate((slow * 0.72f + fast * 0.28f) * weight);
        }

        private static float ResolveRoleMotionX(MenuVisualConceptTargetRole role, float motion)
        {
            switch (role)
            {
                case MenuVisualConceptTargetRole.Header: return motion * 4f;
                case MenuVisualConceptTargetRole.SavesPanel: return motion * 8f;
                case MenuVisualConceptTargetRole.HelpPanel: return motion * -6f;
                case MenuVisualConceptTargetRole.SettingsPanel: return motion * 5f;
                default: return motion * 2f;
            }
        }

        private static float ResolveRoleMotionY(MenuVisualConceptTargetRole role, float motion)
        {
            switch (role)
            {
                case MenuVisualConceptTargetRole.Shell: return motion * -3f;
                case MenuVisualConceptTargetRole.Content: return motion * 4f;
                case MenuVisualConceptTargetRole.LoadingPanel: return motion * 7f;
                default: return motion * 2f;
            }
        }

        private static float ResolveRoleMotionRotation(MenuVisualConceptTargetRole role, float motion, float warningBias)
        {
            float sign = role == MenuVisualConceptTargetRole.SavesPanel || role == MenuVisualConceptTargetRole.LoadingPanel ? -1f : 1f;
            return sign * motion * (0.12f + warningBias * 0.24f);
        }

        private readonly struct TargetState
        {
            public readonly RectTransform Rect;
            public readonly MenuVisualConceptTargetRole Role;
            public readonly Vector2 BaseAnchoredPosition;
            public readonly Vector3 BaseScale;
            public readonly float BaseRotationZ;

            public TargetState(RectTransform rect, MenuVisualConceptTargetRole role, Vector2 baseAnchoredPosition, Vector3 baseScale, float baseRotationZ)
            {
                Rect = rect;
                Role = role;
                BaseAnchoredPosition = baseAnchoredPosition;
                BaseScale = baseScale;
                BaseRotationZ = baseRotationZ;
            }
        }
    }
}
