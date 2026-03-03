namespace Hecton.Interaction
{
    using System.Collections;
    using UnityEngine;

    /// <summary>
    /// Подсвечивает объект через MaterialPropertyBlock (без копий материалов).
    /// Поддерживает два режима:
    ///   • Emission — добавляет свечение (требует включённой Emission в материале).
    ///   • BaseColor — тонирует базовый цвет (универсально).
    /// Плавная интерполяция через Coroutine.
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractionHighlighter : MonoBehaviour
    {
        // ─────────────────────── Settings ────────────────────────
        public enum Mode { Emission, BaseColorTint }

        [Header("Highlight")]
        [SerializeField] private Mode    highlightMode  = Mode.Emission;
        [SerializeField] private Color   highlightColor = new Color(0.25f, 0.7f, 1f, 1f);
        [SerializeField] private float   intensity      = 2.5f;
        [SerializeField] private float   fadeDuration   = 0.12f;

        [Header("Renderers (авто-заполняется, если пусто)")]
        [SerializeField] private Renderer[] targetRenderers;

        // ─────────────────────── Internals ───────────────────────
        private MaterialPropertyBlock _block;
        private bool       _highlighted;
        private Color      _currentValue = Color.black;
        private Coroutine  _fadeRoutine;

        // Shader property IDs — кешируются один раз
        private static readonly int _EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private static readonly int _BaseColorID     = Shader.PropertyToID("_BaseColor");
        private static readonly int _ColorID         = Shader.PropertyToID("_Color");

        // Для BaseColorTint: сохраняем оригинальные цвета
        private Color[] _originalColors;

        // ═════════════════════════════════════════════════════════
        private void Awake()
        {
            _block = new MaterialPropertyBlock();

            if (targetRenderers == null || targetRenderers.Length == 0)
                targetRenderers = GetComponentsInChildren<Renderer>();

            if (highlightMode == Mode.BaseColorTint)
                CacheOriginalColors();
        }

        private void OnDisable()
        {
            // Гарантированно снять подсветку при отключении
            if (_highlighted)
            {
                if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
                _highlighted = false;
                ApplyImmediate(GetTargetColor(false));
            }
        }

        // ─────────────────────── Public API ──────────────────────
        /// <summary>Включить / выключить подсветку.</summary>
        public void SetHighlight(bool active)
        {
            if (_highlighted == active) return;
            _highlighted = active;

            Color target = GetTargetColor(active);

            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);

            if (fadeDuration <= 0f)
            {
                ApplyImmediate(target);
            }
            else
            {
                _fadeRoutine = StartCoroutine(Fade(_currentValue, target));
            }
        }

        // ─────────────────────── Internals ───────────────────────
        private Color GetTargetColor(bool active)
        {
            return highlightMode switch
            {
                Mode.Emission      => active ? highlightColor * intensity : Color.black,
                Mode.BaseColorTint => active ? highlightColor            : Color.white,
                _                  => Color.black
            };
        }

        private IEnumerator Fade(Color from, Color to)
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                Color current = Color.Lerp(from, to, t);
                ApplyImmediate(current);
                yield return null;
            }
            ApplyImmediate(to);
        }

        private void ApplyImmediate(Color value)
        {
            _currentValue = value;

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer rend = targetRenderers[i];
                if (rend == null) continue;

                rend.GetPropertyBlock(_block);

                switch (highlightMode)
                {
                    case Mode.Emission:
                        _block.SetColor(_EmissionColorID, value);
                        break;

                    case Mode.BaseColorTint:
                        // Тонируем оригинальный цвет
                        Color tinted = _originalColors[i] * value;
                        _block.SetColor(_BaseColorID, tinted);
                        _block.SetColor(_ColorID,     tinted); // Built-in fallback
                        break;
                }

                rend.SetPropertyBlock(_block);
            }
        }

        private void CacheOriginalColors()
        {
            _originalColors = new Color[targetRenderers.Length];
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer rend = targetRenderers[i];
                if (rend != null && rend.sharedMaterial != null)
                    _originalColors[i] = rend.sharedMaterial.color;
                else
                    _originalColors[i] = Color.white;
            }
        }
    }
}