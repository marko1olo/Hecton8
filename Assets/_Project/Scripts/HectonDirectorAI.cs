// ============================================================================
// HECTON-8 — HectonDirectorAI.cs  (v2 — Optimized)
// AI Director системы темпа игры для Submerge.
//
// РОЛЬ:
//   • Управляет ритмом сессии в духе Left 4 Dead Director.
//   • Считает TensionScore (0..100).
//   • Ведёт фазовую машину: BuildUp → Peak → Relax.
//   • Координирует FaunaDirector и ScavengePopulator напрямую
//     (кэшированные ссылки) + публикует decoupled-события для остальных систем.
//   • Работает на ISlowTickable, без Update().
//
// ОПТИМИЗАЦИИ v2:
//   • Instance-level predator buffer вместо static (safe multi-scene).
//   • HashSet<Collider> registration — TryGetComponent убран из горячего цикла.
//   • Ленивый resolve зависимостей — FindAnyObjectByType один раз.
//   • SafeInvoke для event protection при scene transitions.
//   • Единый WeightedRoll метод без копипасты.
//   • Новые event types: WeatherShift, MissionTrigger.
//   • Zero GC в горячем пути. Никаких new/List/LINQ в SlowTick.
//
// АВТОСОХРАНЕНИЕ:
//   • При входе в фазу Relax — автосохранение через SaveManager.
//   • Кулдаун: не чаще чем раз в autoSaveCooldownSeconds (по умолчанию 300с).
//   • Fire-and-forget: async Task запускается, но не await-ится в SlowTick.
//
// Namespace: Hecton8.Systems.AI
// ============================================================================

using System;
using System.Collections.Generic;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.SaveSystem;
using UnityEngine;

namespace Hecton8.Systems.AI
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4500)]
    public sealed class HectonDirectorAI : MonoBehaviour, ISlowTickable
    {
        // ══════════════════════════════════════════════════════════
        //  TYPES
        // ══════════════════════════════════════════════════════════

        private enum DirectorPhase
        {
            BuildUp,
            Peak,
            Relax
        }

        private enum DirectorEventType
        {
            None             = 0,
            SpawnHorde       = 1,
            EquipmentGlitch  = 2,
            RareDiscovery    = 3,
            WeatherShift     = 4,
            MissionTrigger   = 5
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC EVENTS — DECOUPLED COMMAND BUS
        // ══════════════════════════════════════════════════════════

        /// <summary>Запрос на спавн волны угрозы. Param: мировая позиция центра.</summary>
        public static event Action<Vector3> OnRequestSpawnHorde;

        /// <summary>Запрос на помехи оборудования / HUD glitch. Param: интенсивность [0..1].</summary>
        public static event Action<float> OnRequestEquipmentGlitch;

        /// <summary>Запрос на редкую находку. Param: мировая позиция интереса.</summary>
        public static event Action<Vector3> OnRequestRareDiscovery;

        /// <summary>Запрос на смену погоды / условий среды. Param: интенсивность [0..1].</summary>
        public static event Action<float> OnRequestWeatherShift;

        /// <summary>Запрос на mission trigger / narrative beat. Param: мировая позиция.</summary>
        public static event Action<Vector3> OnRequestMissionTrigger;

        /// <summary>
        /// Уведомление о глобальном разрешении/запрете хищного давления.
        /// true = давление разрешено (BuildUp / Peak), false = запрещено (Relax).
        /// </summary>
        public static event Action<bool> OnPredatorPressureChanged;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — REFERENCES
        // ══════════════════════════════════════════════════════════

        [Header("── References ────────────────────────────────")]
        [Tooltip("Transform игрока. Если null — будет найден по тегу Player.")]
        [SerializeField] private Transform playerTransform;

        [Tooltip("Система выживания для чтения O2 и энергии.")]
        [SerializeField] private HectonSurvivalSystem survivalSystem;

        [Tooltip("Ссылка на FaunaDirector. Если null — ищется автоматически один раз.")]
        [SerializeField] private FaunaDirector faunaDirector;

        [Tooltip("Ссылка на ScavengePopulator. Если null — ищется автоматически один раз.")]
        [SerializeField] private ScavengePopulator scavengePopulator;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — TENSION WEIGHTS
        // ══════════════════════════════════════════════════════════

        [Header("── Tension Weights ───────────────────────────")]
        [SerializeField] private float predatorsWeight = 40f;
        [SerializeField] private float oxygenWeight    = 25f;
        [SerializeField] private float energyWeight    = 20f;
        [SerializeField] private float calmWeight      = 15f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — PREDATOR SCAN
        // ══════════════════════════════════════════════════════════

        [Header("── Predator Detection ────────────────────────")]
        [SerializeField] private float     predatorScanRadius       = 40f;
        [SerializeField] private LayerMask predatorMask              = ~0;
        [SerializeField] private int       predatorsForMaxTension    = 4;

        [Tooltip("true — HashSet registration mode (рекомендуется).\n" +
                 "false — fallback на CompareTag(\"Predator\").")]
        [SerializeField] private bool useRegistrationMode = true;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — PHASE THRESHOLDS
        // ══════════════════════════════════════════════════════════

        [Header("── Phase Thresholds ──────────────────────────")]
        [SerializeField] private float lowTensionThreshold    = 25f;
        [SerializeField] private float highTensionThreshold   = 60f;
        [SerializeField] private float calmBeforePeakSeconds  = 90f;
        [SerializeField] private float relaxDurationSeconds   = 120f;
        [SerializeField] private float peakCooldownSeconds    = 60f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — BUILD-UP
        // ══════════════════════════════════════════════════════════

        [Header("── Build-Up Behaviour ───────────────────────")]
        [SerializeField] private float buildUpEventIntervalSeconds = 45f;
        [Range(0f, 1f)]
        [SerializeField] private float buildUpEventChance          = 0.35f;
        [SerializeField] private float deepLureDepthThreshold      = 120f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — RELAX MERCY
        // ══════════════════════════════════════════════════════════

        [Header("── Relax Mercy (Director's Grace) ────────────")]
        [Tooltip("Порог tension, ниже которого срабатывает «Милость Директора».")]
        [SerializeField] private float relaxMercyTensionThreshold  = 15f;

        [Tooltip("Интервал между discovery-подарками в фазе Relax (сек).")]
        [SerializeField] private float relaxMercyIntervalSeconds   = 30f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — PEAK EVENT WEIGHTS
        // ══════════════════════════════════════════════════════════

        [Header("── Peak Event Weights ────────────────────────")]
        [SerializeField] private int spawnHordeWeight       = 40;
        [SerializeField] private int equipmentGlitchWeight  = 20;
        [SerializeField] private int rareDiscoveryWeight    = 15;
        [SerializeField] private int weatherShiftWeight     = 15;
        [SerializeField] private int missionTriggerWeight   = 10;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — EVENT OUTPUT
        // ══════════════════════════════════════════════════════════

        [Header("── Event Output ──────────────────────────────")]
        [SerializeField] private float eventOffsetRadius = 25f;
        [Range(0f, 1f)]
        [SerializeField] private float glitchIntensity   = 0.8f;
        [Range(0f, 1f)]
        [SerializeField] private float weatherIntensity  = 0.6f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — AUTOSAVE
        // ══════════════════════════════════════════════════════════

        [Header("── Autosave ──────────────────────────────────")]
        [Tooltip("Минимальный интервал между автосохранениями (секунды).")]
        [SerializeField] private float  autoSaveCooldownSeconds = 300f;

        [Tooltip("Имя слота автосохранения.")]
        [SerializeField] private string autoSaveSlotName        = "autosave";

        [Tooltip("Включить автосохранение при входе в Relax.")]
        [SerializeField] private bool   enableAutoSave          = true;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — PLAYER POS SHADER SYNC
        // ══════════════════════════════════════════════════════════

        [Header("── Shader Integration ────────────────────────")]
        [Tooltip("Публиковать позицию игрока в Shader.SetGlobalVector " +
                 "для proximity reaction в бiolum-шейдерах.")]
        [SerializeField] private bool publishPlayerPosToShader = true;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private DirectorPhase     _phase          = DirectorPhase.BuildUp;
        private DirectorEventType _lastEventType  = DirectorEventType.None;

        private float _tensionScore;
        private float _calmTimer;
        private float _phaseTimer;
        private float _peakCooldownTimer;
        private float _buildUpTimer;
        private float _relaxMercyTimer;

        private bool _predatorPressureEnabled = true;

        // Dynamic delta
        private float _lastTickTime;
        private bool  _hasTickedOnce;

        // Cached factors (debug + reuse)
        private float _predatorFactor;
        private float _oxygenFactor;
        private float _energyFactor;
        private float _calmFactor;

        // Autosave cooldown
        private float _lastAutoSaveTime = float.NegativeInfinity;

        // Lazy resolve flag
        private bool _resolvedDirectors;

        // ══════════════════════════════════════════════════════════
        //  PREDATOR REGISTRATION — REPLACES TryGetComponent
        // ══════════════════════════════════════════════════════════

        // Instance-level buffer — safe for multi-scene / multiple Directors
        private readonly Collider[] _predatorScanBuffer = new Collider[32];

        // Registered predator colliders — O(1) lookup instead of TryGetComponent
        private static readonly HashSet<Collider> _registeredPredators = new(64);

        /// <summary>
        /// Регистрирует коллайдер как хищник. Вызывается из HectonBaseAI.OnEnable.
        /// Zero GC: HashSet.Add на pre-allocated set.
        /// </summary>
        public static void RegisterPredator(Collider c)
        {
            if (c != null)
                _registeredPredators.Add(c);
        }

        /// <summary>
        /// Снимает регистрацию хищника. Вызывается из HectonBaseAI.OnDisable.
        /// </summary>
        public static void UnregisterPredator(Collider c)
        {
            if (c != null)
                _registeredPredators.Remove(c);
        }

        /// <summary>
        /// Очищает все регистрации. Вызывается при смене сцены.
        /// </summary>
        public static void ClearAllPredatorRegistrations()
        {
            _registeredPredators.Clear();
        }

        // Shader property ID — cached once
        private static readonly int ShaderPlayerPosID = Shader.PropertyToID("_PlayerPos");

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private float  _debugTensionScore;
        [SerializeField] private float  _debugPredatorFactor;
        [SerializeField] private float  _debugOxygenFactor;
        [SerializeField] private float  _debugEnergyFactor;
        [SerializeField] private float  _debugCalmFactor;
        [SerializeField] private float  _debugCalmTimer;
        [SerializeField] private float  _debugPhaseTimer;
        [SerializeField] private float  _debugPeakCooldown;
        [SerializeField] private float  _debugDeltaTime;
        [SerializeField] private string _debugPhase;
        [SerializeField] private string _debugLastEvent;
        [SerializeField] private bool   _debugPredatorPressureEnabled;
        [SerializeField] private bool   _debugHasFaunaDirector;
        [SerializeField] private bool   _debugHasScavengePopulator;
        [SerializeField] private float  _debugTimeSinceLastAutoSave;
        [SerializeField] private int    _debugRegisteredPredatorCount;
#endif

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            ResolvePlayerAndSurvival();
        }

        private void OnEnable()
        {
            _lastTickTime  = Time.time;
            _hasTickedOnce = false;
            _resolvedDirectors = false;

            GameTickManager.Instance?.Register((ISlowTickable)this);

            PublishPredatorPressure(true);
        }

        private void OnDisable()
        {
            GameTickManager.Instance?.Unregister((ISlowTickable)this);
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            // ── Dynamic Delta ──
            float now = Time.time;
            float dt;

            if (!_hasTickedOnce)
            {
                dt = 0.5f;
                _hasTickedOnce = true;
            }
            else
            {
                dt = now - _lastTickTime;
            }

            _lastTickTime = now;

            // Clamp delta to sane range
            if (dt < 0.001f) dt = 0.001f;
            if (dt > 5f)     dt = 5f;

            // ── Resolve player ──
            if (playerTransform == null)
            {
                ResolvePlayerAndSurvival();
                if (playerTransform == null)
                    return;
            }

            // ── Lazy resolve directors (one time) ──
            if (!_resolvedDirectors)
                ResolveDirectors();

            // ── Publish player pos to shaders ──
            if (publishPlayerPosToShader)
            {
                Vector3 pp = playerTransform.position;
                Shader.SetGlobalVector(ShaderPlayerPosID,
                    new Vector4(pp.x, pp.y, pp.z, 1f)); // w=1 = valid
            }

            // ── Core logic ──
            UpdateTimers(dt);
            _tensionScore = ComputeTensionScore();
            UpdateCalmTimer(dt);
            UpdatePhaseMachine();

            switch (_phase)
            {
                case DirectorPhase.BuildUp:
                    ProcessBuildUp();
                    break;

                case DirectorPhase.Peak:
                    ProcessPeak();
                    break;

                case DirectorPhase.Relax:
                    ProcessRelax(dt);
                    break;
            }

#if UNITY_EDITOR
            WriteDebugFields(now, dt);
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  TENSION CALCULATION
        // ══════════════════════════════════════════════════════════

        private float ComputeTensionScore()
        {
            _predatorFactor = ComputePredatorFactor();
            _oxygenFactor   = ComputeLowOxygenFactor();
            _energyFactor   = ComputeLowEnergyFactor();
            _calmFactor     = ComputeCalmFactor();

            float tension =
                _predatorFactor * predatorsWeight +
                _oxygenFactor   * oxygenWeight    +
                _energyFactor   * energyWeight    +
                _calmFactor     * calmWeight;

            // Manual clamp — no Mathf call overhead
            if (tension < 0f)   tension = 0f;
            if (tension > 100f) tension = 100f;

            return tension;
        }

        private float ComputePredatorFactor()
        {
            if (playerTransform == null)
                return 0f;

            Vector3 playerPos = playerTransform.position;

            int hitCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                playerPos,
                predatorScanRadius,
                _predatorScanBuffer,
                predatorMask,
                QueryTriggerInteraction.Ignore);

            if (hitCount == 0)
                return 0f;

            float tensionSum = 0f;
            float invRadius  = 1f / predatorScanRadius;

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = _predatorScanBuffer[i];
                if (col == null) continue;

                // ── Predator identification ──
                // Registration mode: O(1) HashSet lookup, zero GC
                // Fallback mode: CompareTag (no alloc)
                if (useRegistrationMode)
                {
                    if (!_registeredPredators.Contains(col))
                        continue;
                }
                else
                {
                    if (!col.CompareTag("Predator"))
                        continue;
                }

                float dist = Vector3.Distance(playerPos, col.transform.position);
                float contribution = 1f - (dist * invRadius);

                if (contribution < 0f) contribution = 0f;
                if (contribution > 1f) contribution = 1f;

                tensionSum += contribution;
            }

            if (predatorsForMaxTension <= 0)
                return tensionSum > 0f ? 1f : 0f;

            float factor = tensionSum / predatorsForMaxTension;
            if (factor > 1f) factor = 1f;
            return factor;
        }

        private float ComputeLowOxygenFactor()
        {
            if (survivalSystem == null)
                return 0f;

            float normalized = survivalSystem.OxygenNormalized;
            if (normalized < 0f) normalized = 0f;
            if (normalized > 1f) normalized = 1f;
            return 1f - normalized;
        }

        private float ComputeLowEnergyFactor()
        {
            if (survivalSystem == null)
                return 0f;

            float normalized = survivalSystem.EnergyNormalized;
            if (normalized < 0f) normalized = 0f;
            if (normalized > 1f) normalized = 1f;
            return 1f - normalized;
        }

        private float ComputeCalmFactor()
        {
            if (calmBeforePeakSeconds <= 0.01f)
                return 1f;

            float factor = _calmTimer / calmBeforePeakSeconds;
            if (factor > 1f) factor = 1f;
            return factor;
        }

        // ══════════════════════════════════════════════════════════
        //  TIMERS
        // ══════════════════════════════════════════════════════════

        private void UpdateTimers(float dt)
        {
            _phaseTimer   += dt;
            _buildUpTimer += dt;

            if (_peakCooldownTimer > 0f)
            {
                _peakCooldownTimer -= dt;
                if (_peakCooldownTimer < 0f)
                    _peakCooldownTimer = 0f;
            }
        }

        private void UpdateCalmTimer(float dt)
        {
            if (_tensionScore <= lowTensionThreshold)
            {
                _calmTimer += dt;
                return;
            }

            if (_tensionScore >= highTensionThreshold)
            {
                _calmTimer = 0f;
                return;
            }

            // Mid-range: slow decay
            _calmTimer -= dt * 0.5f;
            if (_calmTimer < 0f)
                _calmTimer = 0f;
        }

        // ══════════════════════════════════════════════════════════
        //  PHASE MACHINE
        // ══════════════════════════════════════════════════════════

        private void UpdatePhaseMachine()
        {
            switch (_phase)
            {
                case DirectorPhase.BuildUp:
                {
                    if (_calmTimer >= calmBeforePeakSeconds &&
                        _peakCooldownTimer <= 0f)
                    {
                        EnterPhase(DirectorPhase.Peak);
                    }
                    break;
                }

                case DirectorPhase.Peak:
                    // Peak is processed and exited in ProcessPeak
                    break;

                case DirectorPhase.Relax:
                {
                    if (_phaseTimer >= relaxDurationSeconds)
                    {
                        EnterPhase(DirectorPhase.BuildUp);
                    }
                    break;
                }
            }
        }

        private void EnterPhase(DirectorPhase next)
        {
            _phase          = next;
            _phaseTimer     = 0f;
            _relaxMercyTimer = 0f;

            switch (next)
            {
                case DirectorPhase.BuildUp:
                    PublishPredatorPressure(true);
                    break;

                case DirectorPhase.Peak:
                    PublishPredatorPressure(true);
                    break;

                case DirectorPhase.Relax:
                    PublishPredatorPressure(false);
                    TryAutoSave();
                    break;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  AUTOSAVE
        // ══════════════════════════════════════════════════════════

        private void TryAutoSave()
        {
            if (!enableAutoSave)
                return;

            float now = Time.time;

            if (now - _lastAutoSaveTime < autoSaveCooldownSeconds)
                return;

            SaveManager saveManager = SaveManager.Instance;
            if (saveManager == null || saveManager.IsBusy)
                return;

            _lastAutoSaveTime = now;

#pragma warning disable CS4014
            saveManager.SaveGameAsync(autoSaveSlotName);
#pragma warning restore CS4014

            Debug.Log(
                $"[HectonDirectorAI] Autosave triggered on Relax phase entry " +
                $"(slot: '{autoSaveSlotName}').");
        }

        // ══════════════════════════════════════════════════════════
        //  PHASE PROCESSING
        // ══════════════════════════════════════════════════════════

        private void ProcessBuildUp()
        {
            if (_buildUpTimer < buildUpEventIntervalSeconds)
                return;

            _buildUpTimer = 0f;

            if (UnityEngine.Random.value > buildUpEventChance)
                return;

            // Deep lure: rare discovery at depth
            if (survivalSystem != null && survivalSystem.Depth >= deepLureDepthThreshold)
            {
                TriggerRareDiscovery();
                return;
            }

            // Weighted pick between soft build-up events
            int pick = WeightedRoll(
                rareDiscoveryWeight,
                equipmentGlitchWeight,
                weatherShiftWeight);

            switch (pick)
            {
                case 0: TriggerRareDiscovery();        break;
                case 1: TriggerEquipmentGlitch(0.35f); break;
                case 2: TriggerWeatherShift(0.3f);     break;
            }
        }

        private void ProcessPeak()
        {
            DirectorEventType evt = PickPeakEvent();

            switch (evt)
            {
                case DirectorEventType.SpawnHorde:
                    TriggerSpawnHorde();
                    break;

                case DirectorEventType.EquipmentGlitch:
                    TriggerEquipmentGlitch(glitchIntensity);
                    break;

                case DirectorEventType.RareDiscovery:
                    TriggerRareDiscovery();
                    break;

                case DirectorEventType.WeatherShift:
                    TriggerWeatherShift(weatherIntensity);
                    break;

                case DirectorEventType.MissionTrigger:
                    TriggerMissionTrigger();
                    break;
            }

            _lastEventType     = evt;
            _calmTimer         = 0f;
            _peakCooldownTimer = peakCooldownSeconds;

            EnterPhase(DirectorPhase.Relax);
        }

        private void ProcessRelax(float dt)
        {
            _relaxMercyTimer += dt;

            if (_tensionScore > relaxMercyTensionThreshold)
                return;

            if (_relaxMercyTimer < relaxMercyIntervalSeconds)
                return;

            _relaxMercyTimer = 0f;
            TriggerRareDiscovery();
        }

        // ══════════════════════════════════════════════════════════
        //  EVENT PICKING — UNIFIED WEIGHTED RANDOM
        // ══════════════════════════════════════════════════════════

        private DirectorEventType PickPeakEvent()
        {
            int pick = WeightedRoll(
                spawnHordeWeight,
                equipmentGlitchWeight,
                rareDiscoveryWeight,
                weatherShiftWeight,
                missionTriggerWeight);

            switch (pick)
            {
                case 0:  return DirectorEventType.SpawnHorde;
                case 1:  return DirectorEventType.EquipmentGlitch;
                case 2:  return DirectorEventType.RareDiscovery;
                case 3:  return DirectorEventType.WeatherShift;
                case 4:  return DirectorEventType.MissionTrigger;
                default: return DirectorEventType.EquipmentGlitch;
            }
        }

        /// <summary>
        /// Unified weighted random selection. Zero GC.
        /// Accepts 2-5 weights via params-free overloads.
        /// Returns index of chosen weight (0-based).
        /// </summary>
        private static int WeightedRoll(int w0, int w1)
        {
            int total = w0 + w1;
            if (total <= 0) return 0;
            int roll = UnityEngine.Random.Range(0, total);
            return roll < w0 ? 0 : 1;
        }

        private static int WeightedRoll(int w0, int w1, int w2)
        {
            int total = w0 + w1 + w2;
            if (total <= 0) return 0;
            int roll = UnityEngine.Random.Range(0, total);
            if (roll < w0) return 0;
            roll -= w0;
            return roll < w1 ? 1 : 2;
        }

        private static int WeightedRoll(int w0, int w1, int w2, int w3, int w4)
        {
            int total = w0 + w1 + w2 + w3 + w4;
            if (total <= 0) return 0;
            int roll = UnityEngine.Random.Range(0, total);
            if (roll < w0) return 0;
            roll -= w0;
            if (roll < w1) return 1;
            roll -= w1;
            if (roll < w2) return 2;
            roll -= w2;
            return roll < w3 ? 3 : 4;
        }

        // ══════════════════════════════════════════════════════════
        //  EVENT COMMANDS — SAFE INVOKE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Safe event invocation. Catches exceptions from destroyed subscribers
        /// during scene transitions. Zero GC in normal path.
        /// </summary>
        private static void SafeInvoke<T>(Action<T> action, T arg)
        {
            if (action == null) return;
            try
            {
                action.Invoke(arg);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void TriggerSpawnHorde()
        {
            Vector3 center = GetEventPositionAroundPlayer();

            if (faunaDirector != null)
                faunaDirector.ForceSpawnHorde(center);

            SafeInvoke(OnRequestSpawnHorde, center);
        }

        private void TriggerEquipmentGlitch(float intensity)
        {
            SafeInvoke(OnRequestEquipmentGlitch, intensity);
        }

        private void TriggerRareDiscovery()
        {
            Vector3 hintPos = GetEventPositionAroundPlayer();

            if (scavengePopulator != null)
                scavengePopulator.HighlightNearbyResource(hintPos);

            SafeInvoke(OnRequestRareDiscovery, hintPos);
        }

        private void TriggerWeatherShift(float intensity)
        {
            SafeInvoke(OnRequestWeatherShift, intensity);
        }

        private void TriggerMissionTrigger()
        {
            Vector3 pos = GetEventPositionAroundPlayer();
            SafeInvoke(OnRequestMissionTrigger, pos);
        }

        private void PublishPredatorPressure(bool enabled)
        {
            if (_predatorPressureEnabled == enabled)
                return;

            _predatorPressureEnabled = enabled;

            if (faunaDirector != null)
                faunaDirector.SetPredatorPressure(enabled);

            SafeInvoke(OnPredatorPressureChanged, enabled);
        }

        // ══════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════

        private void ResolvePlayerAndSurvival()
        {
            if (playerTransform == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                    playerTransform = player.transform;
            }

            if (survivalSystem == null && playerTransform != null)
            {
                playerTransform.TryGetComponent(out survivalSystem);
            }
        }

        /// <summary>
        /// Lazy resolve of FaunaDirector and ScavengePopulator.
        /// Called once per enable cycle. FindAnyObjectByType is expensive
        /// but only runs once.
        /// </summary>
        private void ResolveDirectors()
        {
            _resolvedDirectors = true;

            if (faunaDirector == null)
                faunaDirector = FindAnyObjectByType<FaunaDirector>();

            if (scavengePopulator == null)
                scavengePopulator = FindAnyObjectByType<ScavengePopulator>();
        }

        private Vector3 GetEventPositionAroundPlayer()
        {
            if (playerTransform == null)
                return transform.position;

            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float dist  = UnityEngine.Random.Range(eventOffsetRadius * 0.4f, eventOffsetRadius);

            Vector3 pos = playerTransform.position;
            pos.x += Mathf.Cos(angle) * dist;
            pos.z += Mathf.Sin(angle) * dist;

            return pos;
        }

#if UNITY_EDITOR
        private void WriteDebugFields(float now, float dt)
        {
            _debugTensionScore            = _tensionScore;
            _debugPredatorFactor          = _predatorFactor;
            _debugOxygenFactor            = _oxygenFactor;
            _debugEnergyFactor            = _energyFactor;
            _debugCalmFactor              = _calmFactor;
            _debugCalmTimer               = _calmTimer;
            _debugPhaseTimer              = _phaseTimer;
            _debugPeakCooldown            = _peakCooldownTimer;
            _debugDeltaTime               = dt;
            _debugPhase                   = _phase.ToString();
            _debugLastEvent               = _lastEventType.ToString();
            _debugPredatorPressureEnabled = _predatorPressureEnabled;
            _debugHasFaunaDirector        = faunaDirector != null;
            _debugHasScavengePopulator    = scavengePopulator != null;
            _debugTimeSinceLastAutoSave   = now - _lastAutoSaveTime;
            _debugRegisteredPredatorCount = _registeredPredators.Count;
        }
#endif

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>Текущий уровень напряжения 0..100.</summary>
        public float TensionScore => _tensionScore;

        /// <summary>Находится ли Director в фазе отдыха.</summary>
        public bool IsRelaxPhase => _phase == DirectorPhase.Relax;

        /// <summary>Разрешено ли сейчас хищное давление.</summary>
        public bool IsPredatorPressureEnabled => _predatorPressureEnabled;

        /// <summary>Текущая фаза в виде строки (для внешней диагностики).</summary>
        public string CurrentPhaseName => _phase.ToString();

        /// <summary>Принудительно вызывает пик-событие.</summary>
        public void ForcePeak()
        {
            EnterPhase(DirectorPhase.Peak);
            ProcessPeak();
        }

        /// <summary>Сбрасывает Director в спокойное состояние.</summary>
        public void ResetDirector()
        {
            _phase              = DirectorPhase.BuildUp;
            _lastEventType      = DirectorEventType.None;
            _tensionScore       = 0f;
            _calmTimer          = 0f;
            _phaseTimer         = 0f;
            _peakCooldownTimer  = 0f;
            _buildUpTimer       = 0f;
            _relaxMercyTimer    = 0f;
            _hasTickedOnce      = false;
            _resolvedDirectors  = false;
            _lastTickTime       = Time.time;

            PublishPredatorPressure(true);
        }

        /// <summary>Принудительно переводит Director в фазу Relax.</summary>
        public void ForceRelax()
        {
            EnterPhase(DirectorPhase.Relax);
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;
#endif
            if (predatorScanRadius          < 1f)  predatorScanRadius          = 1f;
            if (predatorsForMaxTension      < 1)   predatorsForMaxTension      = 1;
            if (calmBeforePeakSeconds       < 5f)  calmBeforePeakSeconds       = 5f;
            if (relaxDurationSeconds        < 5f)  relaxDurationSeconds        = 5f;
            if (peakCooldownSeconds         < 0f)  peakCooldownSeconds         = 0f;
            if (buildUpEventIntervalSeconds < 1f)  buildUpEventIntervalSeconds = 1f;
            if (eventOffsetRadius           < 1f)  eventOffsetRadius           = 1f;
            if (relaxMercyIntervalSeconds   < 5f)  relaxMercyIntervalSeconds   = 5f;
            if (relaxMercyTensionThreshold  < 0f)  relaxMercyTensionThreshold  = 0f;
            if (autoSaveCooldownSeconds     < 30f) autoSaveCooldownSeconds     = 30f;

            if (string.IsNullOrEmpty(autoSaveSlotName))
                autoSaveSlotName = "autosave";

            // Weight sanity
            if (spawnHordeWeight      < 0) spawnHordeWeight      = 0;
            if (equipmentGlitchWeight < 0) equipmentGlitchWeight = 0;
            if (rareDiscoveryWeight   < 0) rareDiscoveryWeight   = 0;
            if (weatherShiftWeight    < 0) weatherShiftWeight    = 0;
            if (missionTriggerWeight  < 0) missionTriggerWeight  = 0;
        }

        private void OnDrawGizmosSelected()
        {
            Transform t = playerTransform != null ? playerTransform : transform;

            // Predator scan radius
            Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.15f);
            Gizmos.DrawWireSphere(t.position, predatorScanRadius);

            // Event offset radius
            Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.12f);
            Gizmos.DrawWireSphere(t.position, eventOffsetRadius);

            // Phase indicator
            switch (_phase)
            {
                case DirectorPhase.BuildUp:
                    Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.25f);
                    break;
                case DirectorPhase.Peak:
                    Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.35f);
                    break;
                case DirectorPhase.Relax:
                    Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.2f);
                    break;
            }

            Gizmos.DrawSphere(t.position + Vector3.up * 3f, 0.5f);
        }
#endif
    }
}
