// ============================================================================
// HECTON-8 — InteractionHighlighter.cs
// Podsvechivaet interaktivnyy obekt cherez MaterialPropertyBlock.
//
// REFAKTORING v2.0 (Zero GC):
//   • Polnostyu udaleny iterator Fade, legacy coroutine start, legacy coroutine stop.
//     Kazhdyy vyzov legacy coroutine start allotsiroval ~100 bytes na GC heap
//     (Coroutine object + iterator state machine + boxing).
//     Pri chastom navedenii na obekty (10+ raz/sek) — oschutimyy GC pressure.
//   • Realizuet ITickable — integratsiya s GameTickManager.
//   • Lenivaya registratsiya: Register v GameTickManager TOLKO kogda
//     tsvet v perehodnom sostoyanii (currentColor ≠ targetColor).
//     Unregister kogda tsvet dostig tseli. Net CPU rashoda vholostuyu.
//   • Plavnaya interpolyatsiya cherez scalar math.lerp + normalizovannyy progress
//     v Tick(float dt). Frame-rate independent.
//   • OnDisable: obyazatelnyy Unregister + mgnovennyy sbros tsveta.
//
// ARHITEKTURA:
//   • Net Update(), net Coroutine, net allokatsiy v rantayme.
//   • MaterialPropertyBlock — bez kopiy materialov (shared material safe).
//   • Dva rezhima: Emission (svechenie) i BaseColorTint (tonirovanie).
//   • Shader Property IDs keshirovany staticheski.
//   • _originalColors keshirovany v Awake (dlya BaseColorTint).
//
// ZhIZNENNYY TsIKL TIKANIYa:
//   ┌──────────────────────────────────────────────────────────────┐
//   │ SetHighlight(true)                                          │
//   │   └→ _targetColor = highlightColor * intensity              │
//   │   └→ _lerpProgress = 0                                     │
//   │   └→ BeginFade() → Register(ITickable)                     │
//   │                                                             │
//   │ Tick(dt) kazhdyy kadr:                                       │
//   │   └→ _lerpProgress += dt / fadeDuration                    │
//   │   └→ _currentValue = Lerp(startColor, targetColor, t)      │
//   │   └→ ApplyImmediate(_currentValue)                         │
//   │   └→ if (t >= 1.0) → EndFade() → Unregister(ITickable)    │
//   │                                                             │
//   │ SetHighlight(false)                                         │
//   │   └→ _targetColor = Color.black / Color.white              │
//   │   └→ _lerpProgress = 0                                     │
//   │   └→ BeginFade() → Register(ITickable) (esli esche ne)       │
//   │                                                             │
//   │ OnDisable()                                                 │
//   │   └→ Unregister(ITickable)                                 │
//   │   └→ ApplyImmediate(offColor) — mgnovennyy sbros           │
//   └──────────────────────────────────────────────────────────────┘
//
// ZERO GC:
//   • Net legacy coroutine start (iterator + Coroutine object = ~100B per call).
//   • Net foreach, LINQ, lyambd.
//   • Color — struct (stack, zero GC).
//   • MaterialPropertyBlock.SetColor — zero GC.
//   • Renderer.GetPropertyBlock/SetPropertyBlock — zero GC.
//   • GameTickManager.Register/Unregister — zero GC (buffered list ops).
// ============================================================================

using System.Collections.Generic;
using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Interaction
{
    [DisallowMultipleComponent]
    public sealed class InteractionHighlighter : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        // ══════════════════════════════════════════════════════════
        //  SETTINGS
        // ══════════════════════════════════════════════════════════

        public enum Mode { Emission, BaseColorTint }

        [Header("── Highlight ─────────────────────────────────")]
        [Tooltip("Rezhim podsvetki:\n" +
                 "• Emission — dobavlyaet svechenie (trebuet Emission v materiale).\n" +
                 "• BaseColorTint — toniruet bazovyy tsvet (universalno).")]
        [SerializeField] private Mode highlightMode = Mode.Emission;

        [Tooltip("Tsvet podsvetki.")]
        [SerializeField] private Color highlightColor = new Color(0.25f, 0.7f, 1f, 1f);

        [Tooltip("Mnozhitel intensivnosti (tolko dlya Emission mode). " +
                 "Znacheniya > 1 dayut HDR-svechenie cherez bloom.")]
        [SerializeField] private float intensity = 2.5f;

        [Tooltip("Dlitelnost perehoda (sekundy). 0 = mgnovenno.")]
        [SerializeField] private float fadeDuration = 0.12f;

        [Header("── Renderers ─────────────────────────────────")]
        [Tooltip("Tselevye renderery. Esli pusto — avto-zapolnyaetsya " +
                 "cherez GetComponentsInChildren<Renderer>() v Awake.")]
        [SerializeField] private Renderer[] targetRenderers;

        // ══════════════════════════════════════════════════════════
        //  SHADER PROPERTY IDs — cached once, zero GC
        // ══════════════════════════════════════════════════════════

        private static readonly int _EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private static readonly int _BaseColorID     = Shader.PropertyToID("_BaseColor");
        private static readonly int _ColorID         = Shader.PropertyToID("_Color");
        private static readonly List<Renderer> _RendererScratch = new List<Renderer>(16);

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>Reusable MaterialPropertyBlock. Created once at owner initialization.</summary>
        private MaterialPropertyBlock _block;

        /// <summary>Logicheskoe sostoyanie: podsvetka vklyuchena.</summary>
        private bool _highlighted;

        /// <summary>Tekuschee znachenie tsveta (interpoliruemoe).</summary>
        private Color _currentValue;

        /// <summary>Tsvet, OT kotorogo nachalsya tekuschiy fade.</summary>
        private Color _fadeFromColor;

        /// <summary>Tsvet, K kotoromu idet tekuschiy fade.</summary>
        private Color _fadeToColor;

        /// <summary>
        /// Normalizovannyy progress interpolyatsii [0..1].
        /// 0 = nachalo fade, 1 = fade zavershen.
        /// Inkrementiruetsya v Tick: _lerpProgress += dt / fadeDuration.
        /// </summary>
        private float _lerpProgress;

        /// <summary>
        /// Flag: obekt zaregistrirovan v GameTickManager kak ITickable.
        /// Predotvraschaet dvoynoy Register i orphan Unregister.
        /// true = Tick() vyzyvaetsya kazhdyy kadr (fade v protsesse).
        /// false = obekt ne tikaetsya (fade zavershen ili ne nachat).
        /// </summary>
        private bool _isTicking;

        private bool _tickDormant;
        private bool _lateFrameRegistered;
        private bool _pendingVisualApply;
        private Color _pendingVisualValue;
        private bool _hotSwapRegistered;

        /// <summary>
        /// Kesh originalnyh tsvetov rendererov (dlya BaseColorTint mode).
        /// Zapolnyaetsya odin raz v Awake. Razmer = targetRenderers.Length.
        /// Color — struct, massiv na managed heap (one-time alloc).
        /// </summary>
        private Color[] _originalColors;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            EnsurePropertyBlock();

            // ── Avto-zapolnenie rendererov ──
            if (targetRenderers == null || targetRenderers.Length == 0)
                AutoCollectRenderers();

            // ── Kesh originalnyh tsvetov (dlya BaseColorTint) ──
            if (highlightMode == Mode.BaseColorTint)
                CacheOriginalColors();

            // ── Nachalnoe sostoyanie: ne podsvechen ──
            _currentValue  = GetOffColor();
            _fadeFromColor = _currentValue;
            _fadeToColor   = _currentValue;
            _lerpProgress  = 1f; // Fade zavershen (nechego interpolirovat)
            _highlighted   = false;
            _isTicking     = false;
            _tickDormant   = false;
        }

        private void AutoCollectRenderers()
        {
            _RendererScratch.Clear();
            GetComponentsInChildren<Renderer>(false, _RendererScratch);
            targetRenderers = _RendererScratch.ToArray();
            _RendererScratch.Clear();
        }

        /// <summary>
        /// OnDisable: garantirovannaya otpiska i sbros vizuala.
        ///
        /// KRITIChNO: esli obekt deaktiviruetsya vo vremya fade —
        /// Tick() perestanet vyzyvatsya, no obekt ostanetsya
        /// v spiske GameTickManager (→ "fake null" auto-cleanup
        /// podberet ego, no luchshe ne polagatsya na eto).
        ///
        /// Poetomu: vsegda Unregister + mgnovennyy sbros tsveta.
        /// </summary>
        private void OnDisable()
        {
            // ── Otpiska ot GameTickManager ──
            StopTicking();
            StopLateFrameTicking();

            if (targetRenderers == null || targetRenderers.Length == 0)
                return;

            // ── Mgnovennyy sbros tsveta ──
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
        /// Vklyuchit / vyklyuchit podsvetku.
        ///
        /// Esli fadeDuration > 0: zapuskaet plavnyy perehod cherez
        /// ITickable.Tick(). Obekt registriruetsya v GameTickManager
        /// tolko na vremya perehoda.
        ///
        /// Esli fadeDuration <= 0: mgnovennoe pereklyuchenie, bez Register.
        ///
        /// Povtornyy vyzov s tem zhe znacheniem — no-op.
        ///
        /// ZERO GC: nikakih allokatsiy. Vse na struct'ah i flagah.
        /// </summary>
        /// <param name="active">true = podsvetit, false = ubrat podsvetku.</param>
        public void SetHighlight(bool active)
        {
            if (_highlighted == active) return;
            _highlighted = active;

            Color target = active ? GetOnColor() : GetOffColor();

            if (fadeDuration <= 0f)
            {
                // ── Mgnovennyy perehod ──
                QueueVisualApply(target);
                _currentValue  = target;
                _fadeToColor   = target;
                _lerpProgress  = 1f;

                // Esli tikalis — ostanavlivaemsya
                StopTicking();
            }
            else
            {
                // ── Plavnyy perehod ──
                BeginFade(_currentValue, target);
            }
        }

        /// <summary>Tekuschee logicheskoe sostoyanie podsvetki.</summary>
        public bool IsHighlighted => _highlighted;

        // ══════════════════════════════════════════════════════════
        //  ITickable — FADE INTERPOLATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Vyzyvaetsya GameTickManager kazhdyy kadr TOLKO vo vremya fade.
        ///
        /// Inkrementiruet _lerpProgress, interpoliruet tsvet,
        /// primenyaet cherez MaterialPropertyBlock.
        ///
        /// Kogda _lerpProgress >= 1.0 — fade zavershen:
        ///   1. Ustanavlivaet tochnyy tselevoy tsvet (bez floating point drift).
        ///   2. Otpisyvaetsya ot GameTickManager (StopTicking).
        ///   → Tick() bolshe ne vyzyvaetsya do sleduyuschego SetHighlight.
        ///   → Zero CPU cost v idle sostoyanii.
        ///
        /// ZERO GC: scalar color lerp — struct math. ApplyImmediate — zero GC.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_tickDormant)
                return;

            // ── Inkrement progressa ──
            // fadeDuration garantirovanno > 0 (BeginFade ne vyzyvaetsya inache).
            // Zaschita ot division by zero cherez max(fadeDuration, epsilon).
            _lerpProgress += deltaTime / math.max(fadeDuration, 0.0001f);

            if (_lerpProgress >= 1f)
            {
                // ── Fade zavershen ──
                _lerpProgress = 1f;
                _currentValue = _fadeToColor;
                QueueVisualApply(_currentValue);

                // Hot Tick only parks. Physical unregister stays in lifecycle/non-Tick paths.
                _tickDormant = true;
            }
            else
            {
                // ── Interpolyatsiya v protsesse ──
                _currentValue = LerpColor(_fadeFromColor, _fadeToColor, _lerpProgress);
                QueueVisualApply(_currentValue);
            }
        }

        private static Color LerpColor(Color from, Color to, float t)
        {
            float clampedT = math.saturate(t);
            return new Color(
                math.lerp(from.r, to.r, clampedT),
                math.lerp(from.g, to.g, clampedT),
                math.lerp(from.b, to.b, clampedT),
                math.lerp(from.a, to.a, clampedT));
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — FADE MANAGEMENT
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Nachinaet plavnyy perehod ot tekuschego tsveta k tselevomu.
        /// Zapominaet nachalnyy i konechnyy tsvet, sbrasyvaet progress,
        /// registriruetsya v GameTickManager (esli esche ne).
        ///
        /// Esli uzhe tikaemsya (predyduschiy fade ne zavershen) —
        /// NE delaem Unregister+Register. Prosto obnovlyaem
        /// _fadeFromColor i _fadeToColor. Perehod plavno
        /// "perenatselivaetsya" s tekuschey pozitsii.
        /// </summary>
        private void BeginFade(Color from, Color to)
        {
            _fadeFromColor = from;
            _fadeToColor   = to;
            _lerpProgress  = 0f;
            _tickDormant   = false;

            StartTicking();
        }

        /// <summary>
        /// Registriruet obekt v GameTickManager kak ITickable.
        /// Vyzyvaetsya pri nachale fade. Idempotentnyy: esli uzhe
        /// zaregistrirovan — no-op (proverka _isTicking).
        ///
        /// GameTickManager.Register vnutri tozhe proveryaet dublikaty
        /// (ContainsRef), no _isTicking ekonomit vyzov metoda.
        /// </summary>
        private void StartTicking()
        {
            if (_isTicking || !Application.isPlaying)
                return;

            _isTicking = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
            if (_isTicking)
            {
                _tickDormant = false;
                TryRegisterHotSwapListener();
            }
        }

        /// <summary>
        /// Snimaet obekt s obnovleniya v GameTickManager.
        /// Vyzyvaetsya kogda fade zavershen ili pri OnDisable.
        /// Idempotentnyy: esli ne zaregistrirovan — no-op.
        /// </summary>
        private void StopTicking()
        {
            if (!_isTicking) return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _isTicking = false;
            _tickDormant = false;
            TryUnregisterHotSwapListenerIfIdle();
        }

        public void LateFrameTick()
        {
            if (_pendingVisualApply)
            {
                _pendingVisualApply = false;
                ApplyImmediate(_pendingVisualValue);
            }

            StopLateFrameTicking();
        }

        private void QueueVisualApply(Color value)
        {
            _pendingVisualValue = value;
            _pendingVisualApply = true;
            StartLateFrameTicking();
        }

        private void StartLateFrameTicking()
        {
            if (_lateFrameRegistered || !Application.isPlaying)
                return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
            if (_lateFrameRegistered)
                TryRegisterHotSwapListener();
        }

        private void StopLateFrameTicking()
        {
            if (!_lateFrameRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _lateFrameRegistered = false;
            TryUnregisterHotSwapListenerIfIdle();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            bool wasTicking = _isTicking;
            bool hadLateFrame = _lateFrameRegistered;
            if (currentService == null)
                return;

            if (wasTicking)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _isTicking = false;
            }

            if (hadLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _lateFrameRegistered = false;
            }

            if (isActiveAndEnabled)
            {
                if (wasTicking)
                    StartTicking();
                if (hadLateFrame)
                    StartLateFrameTicking();
            }

            TryUnregisterHotSwapListenerIfIdle();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListenerIfIdle()
        {
            if (!_hotSwapRegistered || _isTicking || _lateFrameRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — COLOR HELPERS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Vozvraschaet tselevoy tsvet dlya "vklyuchennogo" sostoyaniya.
        /// Emission: highlightColor × intensity (HDR).
        /// BaseColorTint: highlightColor (LDR tonirovanie).
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
        /// Vozvraschaet tselevoy tsvet dlya "vyklyuchennogo" sostoyaniya.
        /// Emission: chernyy (net svecheniya).
        /// BaseColorTint: belyy (originalnyy tsvet × 1 = bez izmeneniy).
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
        /// Mgnovenno primenyaet tsvet ko vsem rendereram cherez MaterialPropertyBlock.
        ///
        /// ZERO GC:
        ///   • Renderer.GetPropertyBlock(block) — fills existing block, zero alloc.
        ///   • MaterialPropertyBlock.SetColor — zero alloc (struct param).
        ///   • Renderer.SetPropertyBlock(block) — zero alloc.
        ///   • for-tsikl po massivu — zero alloc.
        ///
        /// MaterialPropertyBlock NE sozdaet kopiyu materiala.
        /// Vse instances s tem zhe material razdelyayut ego.
        /// PropertyBlock pereopredelyaet svoystva per-renderer.
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
                        // Toniruem originalnyy tsvet: original × tintValue.
                        // Pri value = white (1,1,1,1) → original × 1 = bez izmeneniy.
                        // Pri value = highlightColor → original × tint = podsvechen.
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
        /// Keshiruet originalnye tsveta materialov dlya BaseColorTint mode.
        /// Vyzyvaetsya odin raz v Awake.
        ///
        /// Ispolzuet sharedMaterial.color (NE material.color, kotoryy
        /// sozdaet kopiyu materiala i utekaet esli ne Destroy).
        ///
        /// Esli renderer ili material null — fallback k belomu.
        /// Belyy × tint = tint (korrektnoe povedenie).
        ///
        /// One-time allocation: Color[] na managed heap.
        /// Color — struct (16 bytes), massiv ne sozdaet GC pressure.
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
