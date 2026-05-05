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
//     0.3-0.7: "НЕСТАБИЛЬНЫЙ ЭМОЦИОНАЛЬНЫЙ ПАТТЕРН: ОТЧАЯНИЕ"
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
    public sealed class AtlasSignalDecoder : MonoBehaviour, ISlowTickable, IAtlasSignalEventListener
    {
        private const int MaximumSynchronizedPhase = 3;
        private const float SlowTickDeltaSeconds = 0.5f;

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

        [Header("── Decode Progress ─────────────────────────")]
        [Tooltip("Progress added per second while the decode window is open.")]
        [SerializeField, Range(0.01f, 2f)] private float unpackSpeed = 0.2f;

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
        private bool _serviceRegistered;
        private bool _decodeWindowOpen;
        private float _decodeProgress;

        // Pre-cached phase messages — zero GC
        private static readonly string[] PhaseMessages =
        {
            string.Empty,
            "НЕИЗВЕСТНЫЙ СИГНАЛ — РИТМИЧНЫЙ ПАТТЕРН — ПЕРИОД: 11:23",
            "НЕСТАБИЛЬНЫЙ ЭМОЦИОНАЛЬНЫЙ ПАТТЕРН: ОТЧАЯНИЕ → НАДЕЖДА → БЕЗУМИЕ",
            "АТЛАС-6 — ПОИСК РЕШЕНИЯ — 847 ДНЕЙ — КОЛОНИЯ МЕРТВА — ПРОГРАММА ПОСЕВА АКТИВНА",
            "АТЛАС-6 — РАСШИФРОВКА ЗАВЕРШЕНА — ИСТОЧНИК: ГЛУБИНА -5000М — ЯДРО АКТИВНО"
        };

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public int CurrentPhase => _currentPhase;
        public bool IsFullyDecoded => _fullyDecoded;
        internal bool IsDecodeWindowOpen => _decodeWindowOpen;
        internal float CurrentDecodeProgress => _decodeProgress;
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
            TryRegisterToGlobalRegistry();
            TryRegister();

            AtlasSignalEvents.Register(this);
            TrySynchronizePhaseFromSignal();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterFromGlobalRegistry();

            AtlasSignalEvents.Unregister(this);
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterFromGlobalRegistry();

            if (Instance == this)
                Instance = null;
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            if (_fullyDecoded) return;

            AtlasSignalSystem sys = Hecton8.Core.GlobalRegistry.AtlasSignal;
            if (sys == null) return;
            if (!CanDecodeSignal(sys)) return;

            SynchronizePhaseFromSignal(sys);

            float strength = sys.CurrentStrength;
            int newPhase = CalculatePhase(strength);
            if (newPhase >= 4)
            {
                _decodeWindowOpen = true;
                newPhase = 3;
            }

            if (_decodeWindowOpen && AdvanceDecodeProgress(SlowTickDeltaSeconds))
                return;

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
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Core);
            _registered = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
            _registered = false;
        }

        private void TryRegisterToGlobalRegistry()
        {
            if (_serviceRegistered || !Application.isPlaying || Instance != this)
                return;

            GlobalRegistry.RegisterAtlasSignalDecoderRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.AtlasSignalDecoder, this);
        }

        private void TryUnregisterFromGlobalRegistry()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterAtlasSignalDecoderRuntime(this);
            _serviceRegistered = false;
        }

        private void OnPhaseAdvanced(int phase, float strength)
        {
            string msg = phase < PhaseMessages.Length ? PhaseMessages[phase] : string.Empty;

            LogPhaseAdvanced(phase, msg, strength);
        }

        public void OnAtlasSignalEvent(in AtlasSignalEventPayload payload)
        {
            if ((AtlasSignalEventType)payload.EventType == AtlasSignalEventType.Pulse)
                HandleSignalPulse(payload.SignalStrength);
        }

        private void HandleSignalPulse(float intensity)
        {
            // Пульс усиливает расшифровку — проверяем фазу немедленно
            if (_fullyDecoded) return;

            AtlasSignalSystem sys = Hecton8.Core.GlobalRegistry.AtlasSignal;
            if (sys == null) return;
            if (!CanDecodeSignal(sys)) return;

            SynchronizePhaseFromSignal(sys);

            int newPhase = CalculatePhase(sys.CurrentStrength);
            if (newPhase >= 4)
            {
                _decodeWindowOpen = true;
                newPhase = 3;
            }

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

            AtlasSignalSystem sys = Hecton8.Core.GlobalRegistry.AtlasSignal;
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
            _decodeWindowOpen = sys.CurrentStrength >= fullDecodeThreshold;
        }

        private bool CanDecodeSignal(AtlasSignalSystem sys)
        {
            if (sys == null || sys.CurrentRevealStage <= 0)
                return false;

            FirstHourDirector firstHourDirector = Hecton8.Core.GlobalRegistry.FirstHour;
            if (firstHourDirector == null)
                return true;

            return firstHourDirector.IsMilestoneComplete(minimumMilestoneToDecode);
        }

        internal bool TryAdvanceDecode(float dt)
        {
            return _decodeWindowOpen && AdvanceDecodeProgress(dt);
        }

        private bool AdvanceDecodeProgress(float dt)
        {
            if (_fullyDecoded || !_decodeWindowOpen)
                return false;

            _decodeProgress = Mathf.Clamp01(_decodeProgress + (Mathf.Max(0f, unpackSpeed) * Mathf.Max(0f, dt)));
            if (_decodeProgress < 1f)
                return false;

            CompleteDecode();
            return true;
        }

        private void CompleteDecode()
        {
            _fullyDecoded = true;
            _currentPhase = 4;
            AtlasSignalEvents.RaiseDecoded("atlas6_core_message");
            NarrativeEvents.RaiseDiscoveryMade("atlas6_signal_fully_decoded");
            LogSignalFullyDecoded();
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
