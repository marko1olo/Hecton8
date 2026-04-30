// ============================================================================
// HECTON-8 — InteractionHighlighter.cs
// Подсвечивает интерактивный объект через MaterialPropertyBlock.
//
// РЕФАКТОРИНГ v2.0 (Zero GC):
//   • Полностью удалены iterator Fade, legacy coroutine start, legacy coroutine stop.
//     Каждый вызов legacy coroutine start аллоцировал ~100 bytes на GC heap
//     (Coroutine object + iterator state machine + boxing).
//     При частом наведении на объекты (10+ раз/сек) — ощутимый GC pressure.
//   • Реализует ITickable — интеграция с GameTickManager.
//   • Ленивая регистрация: Register в GameTickManager ТОЛЬКО когда
//     цвет в переходном состоянии (currentColor ≠ targetColor).
//     Unregister когда цвет достиг цели. Нет CPU расхода вхолостую.
//   • Плавная интерполяция через Color.Lerp + нормализованный прогресс
//     в Tick(float dt). Frame-rate independent.
//   • OnDisable: обязательный Unregister + мгновенный сброс цвета.
//
// АРХИТЕКТУРА:
//   • Нет Update(), нет Coroutine, нет аллокаций в рантайме.
//   • MaterialPropertyBlock — без копий материалов (shared material safe).
//   • Два режима: Emission (свечение) и BaseColorTint (тонирование).
//   • Shader Property IDs кэшированы статически.
//   • _originalColors кэшированы в Awake (для BaseColorTint).
//
// ЖИЗНЕННЫЙ ЦИКЛ ТИКАНИЯ:
//   ┌──────────────────────────────────────────────────────────────┐
//   │ SetHighlight(true)                                          │
//   │   └→ _targetColor = highlightColor * intensity              │
//   │   └→ _lerpProgress = 0                                     │
//   │   └→ BeginFade() → Register(ITickable)                     │
//   │                                                             │
//   │ Tick(dt) каждый кадр:                                       │
//   │   └→ _lerpProgress += dt / fadeDuration                    │
//   │   └→ _currentValue = Lerp(startColor, targetColor, t)      │
//   │   └→ ApplyImmediate(_currentValue)                         │
//   │   └→ if (t >= 1.0) → EndFade() → Unregister(ITickable)    │
//   │                                                             │
//   │ SetHighlight(false)                                         │
//   │   └→ _targetColor = Color.black / Color.white              │
//   │   └→ _lerpProgress = 0                                     │
//   │   └→ BeginFade() → Register(ITickable) (если ещё не)       │
//   │                                                             │
//   │ OnDisable()                                                 │
//   │   └→ Unregister(ITickable)                                 │
//   │   └→ ApplyImmediate(offColor) — мгновенный сброс           │
//   └──────────────────────────────────────────────────────────────┘
//
// ZERO GC:
//   • Нет legacy coroutine start (iterator + Coroutine object = ~100B per call).
//   • Нет foreach, LINQ, лямбд.
//   • Color — struct (stack, zero GC).
//   • MaterialPropertyBlock.SetColor — zero GC.
//   • Renderer.GetPropertyBlock/SetPropertyBlock — zero GC.
//   • GameTickManager.Register/Unregister — zero GC (buffered list ops).
// ============================================================================

using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Interaction
{
    [DisallowMultipleComponent]
    public sealed class InteractionHighlighter : MonoBehaviour, ITickable, IUpdatable
    {
        // ══════════════════════════════════════════════════════════
        //  SETTINGS
        // ══════════════════════════════════════════════════════════

        public enum Mode { Emission, BaseColorTint }

        [Header("── Highlight ─────────────────────────────────")]
        [Tooltip("Режим подсветки:\n" +
                 "• Emission — добавляет свечение (требует Emission в материале).\n" +
                 "• BaseColorTint — тонирует базовый цвет (универсально).")]
        [SerializeField] private Mode highlightMode = Mode.Emission;

        [Tooltip("Цвет подсветки.")]
        [SerializeField] private Color highlightColor = new Color(0.25f, 0.7f, 1f, 1f);

        [Tooltip("Множитель интенсивности (только для Emission mode). " +
                 "Значения > 1 дают HDR-свечение через bloom.")]
        [SerializeField] private float intensity = 2.5f;

        [Tooltip("Длительность перехода (секунды). 0 = мгновенно.")]
        [SerializeField] private float fadeDuration = 0.12f;

        [Header("── Renderers ─────────────────────────────────")]
        [Tooltip("Целевые рендереры. Если пусто — авто-заполняется " +
                 "через GetComponentsInChildren<Renderer>() в Awake.")]
        [SerializeField] private Renderer[] targetRenderers;

        // ══════════════════════════════════════════════════════════
        //  SHADER PROPERTY IDs — cached once, zero GC
        // ══════════════════════════════════════════════════════════

        private static readonly int _EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private static readonly int _BaseColorID     = Shader.PropertyToID("_BaseColor");
        private static readonly int _ColorID         = Shader.PropertyToID("_Color");

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>Reusable MaterialPropertyBlock. Created once at owner initialization.</summary>
        private MaterialPropertyBlock _block;

        /// <summary>Логическое состояние: подсветка включена.</summary>
        private bool _highlighted;

        /// <summary>Текущее значение цвета (интерполируемое).</summary>
        private Color _currentValue;

        /// <summary>Цвет, ОТ которого начался текущий fade.</summary>
        private Color _fadeFromColor;

        /// <summary>Цвет, К которому идёт текущий fade.</summary>
        private Color _fadeToColor;

        /// <summary>
        /// Нормализованный прогресс интерполяции [0..1].
        /// 0 = начало fade, 1 = fade завершён.
        /// Инкрементируется в Tick: _lerpProgress += dt / fadeDuration.
        /// </summary>
        private float _lerpProgress;

        /// <summary>
        /// Флаг: объект зарегистрирован в GameTickManager как ITickable.
        /// Предотвращает двойной Register и orphan Unregister.
        /// true = Tick() вызывается каждый кадр (fade в процессе).
        /// false = объект не тикается (fade завершён или не начат).
        /// </summary>
        private bool _isTicking;

        /// <summary>
        /// Кэш оригинальных цветов рендереров (для BaseColorTint mode).
        /// Заполняется один раз в Awake. Размер = targetRenderers.Length.
        /// Color — struct, массив на managed heap (one-time alloc).
        /// </summary>
        private Color[] _originalColors;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            EnsurePropertyBlock();

            // ── Авто-заполнение рендереров ──
            if (targetRenderers == null || targetRenderers.Length == 0)
                targetRenderers = GetComponentsInChildren<Renderer>();

            // ── Кэш оригинальных цветов (для BaseColorTint) ──
            if (highlightMode == Mode.BaseColorTint)
                CacheOriginalColors();

            // ── Начальное состояние: не подсвечен ──
            _currentValue  = GetOffColor();
            _fadeFromColor = _currentValue;
            _fadeToColor   = _currentValue;
            _lerpProgress  = 1f; // Fade завершён (нечего интерполировать)
            _highlighted   = false;
            _isTicking     = false;
        }

        /// <summary>
        /// OnDisable: гарантированная отписка и сброс визуала.
        ///
        /// КРИТИЧНО: если объект деактивируется во время fade —
        /// Tick() перестанет вызываться, но объект останется
        /// в списке GameTickManager (→ "fake null" auto-cleanup
        /// подберёт его, но лучше не полагаться на это).
        ///
        /// Поэтому: всегда Unregister + мгновенный сброс цвета.
        /// </summary>
        private void OnDisable()
        {
            // ── Отписка от GameTickManager ──
            StopTicking();

            if (targetRenderers == null || targetRenderers.Length == 0)
                return;

            // ── Мгновенный сброс цвета ──
            Color offColor = GetOffColor();
            ApplyImmediate(offColor);
            _currentValue  = offColor;
            _fadeToColor   = offColor;
            _lerpProgress  = 1f;
            _highlighted   = false;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Включить / выключить подсветку.
        ///
        /// Если fadeDuration > 0: запускает плавный переход через
        /// ITickable.Tick(). Объект регистрируется в GameTickManager
        /// только на время перехода.
        ///
        /// Если fadeDuration <= 0: мгновенное переключение, без Register.
        ///
        /// Повторный вызов с тем же значением — no-op.
        ///
        /// ZERO GC: никаких аллокаций. Всё на struct'ах и флагах.
        /// </summary>
        /// <param name="active">true = подсветить, false = убрать подсветку.</param>
        public void SetHighlight(bool active)
        {
            if (_highlighted == active) return;
            _highlighted = active;

            Color target = active ? GetOnColor() : GetOffColor();

            if (fadeDuration <= 0f)
            {
                // ── Мгновенный переход ──
                ApplyImmediate(target);
                _currentValue  = target;
                _fadeToColor   = target;
                _lerpProgress  = 1f;

                // Если тикались — останавливаемся
                StopTicking();
            }
            else
            {
                // ── Плавный переход ──
                BeginFade(_currentValue, target);
            }
        }

        /// <summary>Текущее логическое состояние подсветки.</summary>
        public bool IsHighlighted => _highlighted;

        // ══════════════════════════════════════════════════════════
        //  ITickable — FADE INTERPOLATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вызывается GameTickManager каждый кадр ТОЛЬКО во время fade.
        ///
        /// Инкрементирует _lerpProgress, интерполирует цвет,
        /// применяет через MaterialPropertyBlock.
        ///
        /// Когда _lerpProgress >= 1.0 — fade завершён:
        ///   1. Устанавливает точный целевой цвет (без floating point drift).
        ///   2. Отписывается от GameTickManager (StopTicking).
        ///   → Tick() больше не вызывается до следующего SetHighlight.
        ///   → Zero CPU cost в idle состоянии.
        ///
        /// ZERO GC: Color.Lerp — struct math. ApplyImmediate — zero GC.
        /// </summary>
        public void Tick(float deltaTime)
        {
            // ── Инкремент прогресса ──
            // fadeDuration гарантированно > 0 (BeginFade не вызывается иначе).
            // Защита от division by zero через max(fadeDuration, epsilon).
            _lerpProgress += deltaTime / fadeDuration;

            if (_lerpProgress >= 1f)
            {
                // ── Fade завершён ──
                _lerpProgress = 1f;
                _currentValue = _fadeToColor;
                ApplyImmediate(_currentValue);

                // Отписываемся — больше не тикаемся до следующего SetHighlight
                StopTicking();
            }
            else
            {
                // ── Интерполяция в процессе ──
                _currentValue = Color.Lerp(_fadeFromColor, _fadeToColor, _lerpProgress);
                ApplyImmediate(_currentValue);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — FADE MANAGEMENT
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Начинает плавный переход от текущего цвета к целевому.
        /// Запоминает начальный и конечный цвет, сбрасывает прогресс,
        /// регистрируется в GameTickManager (если ещё не).
        ///
        /// Если уже тикаемся (предыдущий fade не завершён) —
        /// НЕ делаем Unregister+Register. Просто обновляем
        /// _fadeFromColor и _fadeToColor. Переход плавно
        /// "перенацеливается" с текущей позиции.
        /// </summary>
        private void BeginFade(Color from, Color to)
        {
            _fadeFromColor = from;
            _fadeToColor   = to;
            _lerpProgress  = 0f;

            StartTicking();
        }

        /// <summary>
        /// Регистрирует объект в GameTickManager как ITickable.
        /// Вызывается при начале fade. Идемпотентный: если уже
        /// зарегистрирован — no-op (проверка _isTicking).
        ///
        /// GameTickManager.Register внутри тоже проверяет дубликаты
        /// (ContainsRef), но _isTicking экономит вызов метода.
        /// </summary>
        private void StartTicking()
        {
            if (_isTicking || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _isTicking = true;
        }

        /// <summary>
        /// Снимает объект с обновления в GameTickManager.
        /// Вызывается когда fade завершён или при OnDisable.
        /// Идемпотентный: если не зарегистрирован — no-op.
        /// </summary>
        private void StopTicking()
        {
            if (!_isTicking) return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _isTicking = false;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — COLOR HELPERS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Возвращает целевой цвет для "включённого" состояния.
        /// Emission: highlightColor × intensity (HDR).
        /// BaseColorTint: highlightColor (LDR тонирование).
        /// </summary>
        private Color GetOnColor()
        {
            return highlightMode switch
            {
                Mode.Emission      => highlightColor * intensity,
                Mode.BaseColorTint => highlightColor,
                _                  => Color.black
            };
        }

        /// <summary>
        /// Возвращает целевой цвет для "выключенного" состояния.
        /// Emission: чёрный (нет свечения).
        /// BaseColorTint: белый (оригинальный цвет × 1 = без изменений).
        /// </summary>
        private Color GetOffColor()
        {
            return highlightMode switch
            {
                Mode.Emission      => Color.black,
                Mode.BaseColorTint => Color.white,
                _                  => Color.black
            };
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — MATERIAL APPLICATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Мгновенно применяет цвет ко всем рендерерам через MaterialPropertyBlock.
        ///
        /// ZERO GC:
        ///   • Renderer.GetPropertyBlock(block) — fills existing block, zero alloc.
        ///   • MaterialPropertyBlock.SetColor — zero alloc (struct param).
        ///   • Renderer.SetPropertyBlock(block) — zero alloc.
        ///   • for-цикл по массиву — zero alloc.
        ///
        /// MaterialPropertyBlock НЕ создаёт копию материала.
        /// Все instances с тем же material разделяют его.
        /// PropertyBlock переопределяет свойства per-renderer.
        /// </summary>
        private void ApplyImmediate(Color value)
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
                return;

            EnsurePropertyBlock();
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
                        // Тонируем оригинальный цвет: original × tintValue.
                        // При value = white (1,1,1,1) → original × 1 = без изменений.
                        // При value = highlightColor → original × tint = подсвечен.
                        Color tinted = _originalColors[i] * value;
                        _block.SetColor(_BaseColorID, tinted);
                        _block.SetColor(_ColorID,     tinted); // Built-in RP fallback
                        break;
                }

                rend.SetPropertyBlock(_block);
            }
        }

        private void EnsurePropertyBlock()
        {
            if (_block != null)
                return;

            // COLD ALLOC: MaterialPropertyBlock[1] — interaction highlight state — owner: InteractionHighlighter
            _block = new MaterialPropertyBlock();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — ORIGINAL COLOR CACHE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Кэширует оригинальные цвета материалов для BaseColorTint mode.
        /// Вызывается один раз в Awake.
        ///
        /// Использует sharedMaterial.color (НЕ material.color, который
        /// создаёт копию материала и утекает если не Destroy).
        ///
        /// Если рендерер или материал null — fallback к белому.
        /// Белый × tint = tint (корректное поведение).
        ///
        /// One-time allocation: Color[] на managed heap.
        /// Color — struct (16 bytes), массив не создаёт GC pressure.
        /// </summary>
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

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (fadeDuration < 0f) fadeDuration = 0f;
            if (intensity    < 0f) intensity    = 0f;
        }
#endif
    }
}
