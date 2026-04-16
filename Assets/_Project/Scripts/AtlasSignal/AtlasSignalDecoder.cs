// ============================================================================
// HECTON-8 — AtlasSignalDecoder.cs
// Система расшифровки сигнала Атлас-6.
//
// ЛОР (лор3 Блок З):
//   Чем ближе к ядру — тем яснее "содержание" сигнала:
//   не слова, а эмоциональный паттерн: отчаяние, надежда, безумие.
//   Ритм 11:23 — время перебора всех вариантов "спасения колонии".
//
// МЕХАНИКА:
//   • Три фазы расшифровки по силе сигнала:
//     0.0-0.3: "НЕИЗВЕСТНЫЙ СИГНАЛ — РИТМИЧНЫЙ ПАТТЕРН"
//     0.3-0.7: "СИГНАЛ АТЛАС-6 — ЭМОЦИОНАЛЬНЫЙ ПАТТЕРН: ОТЧАЯНИЕ"
//     0.7-1.0: "АТЛАС-6 — ПОИСК РЕШЕНИЯ — 847 ДНЕЙ — КОЛОНИЯ МЕРТВА"
//   • При достижении 1.0 — полная расшифровка, финальный квест.
//
// ZERO GC:
//   • ISlowTickable — проверка фазы раз в 0.5с.
//   • Cached strings для каждой фазы.
// ============================================================================

using Conditional = System.Diagnostics.ConditionalAttribute;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.AtlasSignal
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-90)]
    public sealed class AtlasSignalDecoder : MonoBehaviour, ISlowTickable
    {
        private const int MaximumSynchronizedPhase = 3;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Thresholds ──────────────────────────────")]
        [SerializeField, Range(0f, 1f)] private float phase1Threshold = 0.05f;
        [SerializeField, Range(0f, 1f)] private float phase2Threshold = 0.30f;
        [SerializeField, Range(0f, 1f)] private float phase3Threshold = 0.70f;
        [SerializeField, Range(0f, 1f)] private float fullDecodeThreshold = 0.95f;

        [Header("── First-Hour Gate ─────────────────────────")]
        [Tooltip("Do not decode or surface Atlas phases before the first-hour spine reaches module-route play.")]
        [SerializeField] private FirstHourMilestone minimumMilestoneToDecode = FirstHourMilestone.FirstModule;

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        public static AtlasSignalDecoder Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Instance = null;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private int  _currentPhase = 0;
        private bool _fullyDecoded;
        private bool _registered;

        // Pre-cached phase messages — zero GC
        private static readonly string[] PhaseMessages =
        {
            string.Empty,
            "НЕИЗВЕСТНЫЙ СИГНАЛ — РИТМИЧНЫЙ ПАТТЕРН — ПЕРИОД: 11:23",
            "СИГНАЛ АТЛАС-6 — ЭМОЦИОНАЛЬНЫЙ ПАТТЕРН: ОТЧАЯНИЕ → НАДЕЖДА → БЕЗУМИЕ",
            "АТЛАС-6 — ПОИСК РЕШЕНИЯ — 847 ДНЕЙ — КОЛОНИЯ МЕРТВА — ПРОГРАММА ПОСЕВА АКТИВНА",
            "АТЛАС-6 — РАСШИФРОВКА ЗАВЕРШЕНА — ИСТОЧНИК: ГЛУБИНА -5000М — ЯДРО АКТИВНО"
        };

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public int CurrentPhase => _currentPhase;
        public bool IsFullyDecoded => _fullyDecoded;
        public string CurrentMessage => _currentPhase < PhaseMessages.Length
            ? PhaseMessages[_currentPhase]
            : string.Empty;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            TryRegister();

            AtlasSignalEvents.OnSignalPulse += HandleSignalPulse;
            TrySynchronizePhaseFromSignal();
        }

        private void OnDisable()
        {
            TryUnregister();

            AtlasSignalEvents.OnSignalPulse -= HandleSignalPulse;
        }

        private void OnDestroy()
        {
            TryUnregister();

            if (Instance == this)
                Instance = null;
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            if (_fullyDecoded) return;

            AtlasSignalSystem sys = AtlasSignalSystem.Instance;
            if (sys == null) return;
            if (!CanDecodeSignal(sys)) return;

            SynchronizePhaseFromSignal(sys);

            float strength = sys.CurrentStrength;
            int newPhase = CalculatePhase(strength);

            if (newPhase <= _currentPhase) return;

            _currentPhase = newPhase;
            OnPhaseAdvanced(newPhase, strength);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private int CalculatePhase(float strength)
        {
            if (strength >= fullDecodeThreshold) return 4;
            if (strength >= phase3Threshold)     return 3;
            if (strength >= phase2Threshold)     return 2;
            if (strength >= phase1Threshold)     return 1;
            return 0;
        }

        private void TryRegister()
        {
            if (_registered)
                return;

            GameTickManager gameTickManager = GameTickManager.Instance;
            if (gameTickManager == null)
                return;

            gameTickManager.Register(this);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GameTickManager gameTickManager = GameTickManager.Instance;
            if (gameTickManager != null)
                gameTickManager.Unregister(this);

            _registered = false;
        }

        private void OnPhaseAdvanced(int phase, float strength)
        {
            string msg = phase < PhaseMessages.Length ? PhaseMessages[phase] : string.Empty;

            // Полная расшифровка
            if (phase >= 4 && !_fullyDecoded)
            {
                _fullyDecoded = true;
                AtlasSignalEvents.RaiseDecoded("atlas6_core_message");

                NarrativeEvents.RaiseDiscoveryMade("atlas6_signal_fully_decoded");

                LogSignalFullyDecoded();
            }

            LogPhaseAdvanced(phase, msg, strength);
        }

        private void HandleSignalPulse(float intensity)
        {
            // Пульс усиливает расшифровку — проверяем фазу немедленно
            if (_fullyDecoded) return;

            AtlasSignalSystem sys = AtlasSignalSystem.Instance;
            if (sys == null) return;
            if (!CanDecodeSignal(sys)) return;

            SynchronizePhaseFromSignal(sys);

            int newPhase = CalculatePhase(sys.CurrentStrength);
            if (newPhase > _currentPhase)
            {
                _currentPhase = newPhase;
                OnPhaseAdvanced(newPhase, sys.CurrentStrength);
            }
        }

        private void TrySynchronizePhaseFromSignal()
        {
            if (_fullyDecoded)
                return;

            AtlasSignalSystem sys = AtlasSignalSystem.Instance;
            if (sys == null)
                return;

            if (!CanDecodeSignal(sys))
                return;

            SynchronizePhaseFromSignal(sys);
        }

        private void SynchronizePhaseFromSignal(AtlasSignalSystem sys)
        {
            if (sys == null || _fullyDecoded)
                return;

            int synchronizedPhase = Mathf.Min(MaximumSynchronizedPhase, CalculatePhase(sys.CurrentStrength));
            if (synchronizedPhase > _currentPhase)
                _currentPhase = synchronizedPhase;
        }

        private bool CanDecodeSignal(AtlasSignalSystem sys)
        {
            if (sys == null || sys.CurrentRevealStage <= 0)
                return false;

            FirstHourDirector firstHourDirector = FirstHourDirector.Instance;
            if (firstHourDirector == null)
                return true;

            return firstHourDirector.IsMilestoneComplete(minimumMilestoneToDecode);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogSignalFullyDecoded()
        {
            Debug.Log("[AtlasDecoder] Signal fully decoded. Atlas-6 core message received.");
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogPhaseAdvanced(int phase, string msg, float strength)
        {
            Debug.Log($"[AtlasDecoder] Phase {phase}: {msg} (strength: {strength:F2})");
        }
    }
}
