// ============================================================================
// HECTON-8 — HectonDirectorAI.cs
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
// АВТОСОХРАНЕНИЕ:
//   • При входе в фазу Relax — автосохранение через SaveManager.
//   • Кулдаун: не чаще чем раз в autoSaveCooldownSeconds (по умолчанию 300с = 5 мин).
//   • Fire-and-forget: async Task запускается, но не await-ится в SlowTick.
//     SaveManager обрабатывает ошибки внутри себя.
//   • Не блокирует главный поток (snapshot <2ms, disk write — background).
//
// ЛОГИКА:
//   TensionScore складывается из:
//     1. Взвешенных дистанций до хищников (saturate(1 - dist/radius)).
//     2. Низкого уровня O₂.
//     3. Низкого уровня энергии.
//     4. Времени, проведённого в спокойствии.
//
//   Фазы:
//     • BuildUp — мягкое нагнетание, редкие находки / заманивание глубже.
//     • Peak    — принудительное событие, если игроку слишком спокойно.
//     • Relax   — отдых после пика, запрет на опасные события.
//                 «Милость Директора»: раз в 30с при очень низком tension
//                 выдаётся RareDiscovery для создания цикла «Страх → Награда».
//                 Автосохранение при входе в фазу.
//
// ZERO GC:
//   • Никаких new/List в SlowTick.
//   • Physics.OverlapSphereNonAlloc.
//   • Кэшированные буферы.
//   • Нет LINQ / foreach по managed-коллекциям в горячем пути.
//   • SaveGameAsync — fire-and-forget, zero alloc в caller.
//
// Namespace: Hecton8.Systems.AI
// ============================================================================

using System;
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
            None = 0,
            SpawnHorde = 1,
            EquipmentGlitch = 2,
            RareDiscovery = 3
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC EVENTS — DECOUPLED COMMAND BUS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Запрос на спавн волны угрозы.
        /// Param: мировая позиция центра события.
        /// </summary>
        public static event Action<Vector3> OnRequestSpawnHorde;

        /// <summary>
        /// Запрос на помехи оборудования / HUD glitch.
        /// Param: интенсивность [0..1].
        /// </summary>
        public static event Action<float> OnRequestEquipmentGlitch;

        /// <summary>
        /// Запрос на редкую находку / подсветку ресурса.
        /// Param: мировая позиция интереса рядом с игроком.
        /// </summary>
        public static event Action<Vector3> OnRequestRareDiscovery;

        /// <summary>
        /// Уведомление о глобальном разрешении/запрете хищного давления.
        /// true  = давление разрешено (BuildUp / Peak).
        /// false = давление запрещено (Relax).
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

        [Tooltip("Ссылка на FaunaDirector. Если null — ищется автоматически в OnEnable.")]
        [SerializeField] private FaunaDirector faunaDirector;

        [Tooltip("Ссылка на ScavengePopulator. Если null — ищется автоматически в OnEnable.")]
        [SerializeField] private ScavengePopulator scavengePopulator;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — TENSION WEIGHTS
        // ══════════════════════════════════════════════════════════

        [Header("── Tension Weights ───────────────────────────")]
        [SerializeField] private float predatorsWeight = 40f;
        [SerializeField] private float oxygenWeight = 25f;
        [SerializeField] private float energyWeight = 20f;
        [SerializeField] private float calmWeight = 15f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — PREDATOR SCAN
        // ══════════════════════════════════════════════════════════

        [Header("── Predator Detection ────────────────────────")]
        [SerializeField] private float predatorScanRadius = 40f;
        [SerializeField] private LayerMask predatorMask = ~0;
        [SerializeField] private int predatorsForMaxTension = 4;

        [Tooltip("true — любой HectonBaseAI в радиусе = угроза. " +
                 "false — только объекты с tag Predator.")]
        [SerializeField] private bool countAnyAIAsPredator = true;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — PHASE THRESHOLDS
        // ══════════════════════════════════════════════════════════

        [Header("── Phase Thresholds ──────────────────────────")]
        [SerializeField] private float lowTensionThreshold = 25f;
        [SerializeField] private float highTensionThreshold = 60f;
        [SerializeField] private float calmBeforePeakSeconds = 90f;
        [SerializeField] private float relaxDurationSeconds = 120f;
        [SerializeField] private float peakCooldownSeconds = 60f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — BUILD-UP
        // ══════════════════════════════════════════════════════════

        [Header("── Build-Up Behaviour ───────────────────────")]
        [SerializeField] private float buildUpEventIntervalSeconds = 45f;
        [Range(0f, 1f)]
        [SerializeField] private float buildUpEventChance = 0.35f;
        [SerializeField] private float deepLureDepthThreshold = 120f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — RELAX MERCY
        // ══════════════════════════════════════════════════════════

        [Header("── Relax Mercy (Director's Grace) ────────────")]
        [Tooltip("Порог tension, ниже которого срабатывает «Милость Директора».")]
        [SerializeField] private float relaxMercyTensionThreshold = 15f;

        [Tooltip("Интервал между discovery-подарками в фазе Relax (сек).")]
        [SerializeField] private float relaxMercyIntervalSeconds = 30f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — PEAK EVENT WEIGHTS
        // ══════════════════════════════════════════════════════════

        [Header("── Peak Event Weights ────────────────────────")]
        [SerializeField] private int spawnHordeWeight = 50;
        [SerializeField] private int equipmentGlitchWeight = 25;
        [SerializeField] private int rareDiscoveryWeight = 25;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — EVENT OUTPUT
        // ══════════════════════════════════════════════════════════

        [Header("── Event Output ──────────────────────────────")]
        [SerializeField] private float eventOffsetRadius = 25f;
        [Range(0f, 1f)]
        [SerializeField] private float glitchIntensity = 0.8f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — AUTOSAVE
        // ══════════════════════════════════════════════════════════

        [Header("── Autosave ──────────────────────────────────")]
        [Tooltip("Минимальный интервал между автосохранениями (секунды). " +
                 "Предотвращает частые записи на диск при быстрой смене фаз. " +
                 "300 = 5 минут.")]
        [SerializeField] private float autoSaveCooldownSeconds = 300f;

        [Tooltip("Имя слота автосохранения.")]
        [SerializeField] private string autoSaveSlotName = "autosave";

        [Tooltip("Включить автосохранение при входе в Relax.")]
        [SerializeField] private bool enableAutoSave = true;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private DirectorPhase _phase = DirectorPhase.BuildUp;
        private DirectorEventType _lastEventType = DirectorEventType.None;

        private float _tensionScore;
        private float _calmTimer;
        private float _phaseTimer;
        private float _peakCooldownTimer;
        private float _buildUpTimer;
        private float _relaxMercyTimer;

        private bool _predatorPressureEnabled = true;

        // Dynamic delta
        private float _lastTickTime;
        private bool _hasTickedOnce;

        // Cached factors (debug + reuse)
        private float _predatorFactor;
        private float _oxygenFactor;
        private float _energyFactor;
        private float _calmFactor;

        // Autosave cooldown
        private float _lastAutoSaveTime = float.NegativeInfinity;

        // Pre-allocated overlap buffer (shared, single-threaded)
        private static readonly Collider[] PredatorBuffer = new Collider[32];

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private float _debugTensionScore;
        [SerializeField] private float _debugPredatorFactor;
        [SerializeField] private float _debugOxygenFactor;
        [SerializeField] private float _debugEnergyFactor;
        [SerializeField] private float _debugCalmFactor;
        [SerializeField] private float _debugCalmTimer;
        [SerializeField] private float _debugPhaseTimer;
        [SerializeField] private float _debugPeakCooldown;
        [SerializeField] private float _debugDeltaTime;
        [SerializeField] private string _debugPhase;
        [SerializeField] private string _debugLastEvent;
        [SerializeField] private bool _debugPredatorPressureEnabled;
        [SerializeField] private bool _debugHasFaunaDirector;
        [SerializeField] private bool _debugHasScavengePopulator;
        [SerializeField] private float _debugTimeSinceLastAutoSave;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            ResolvePlayerAndSurvival();
        }

        private void OnEnable()
        {
            if (faunaDirector == null)
                faunaDirector = FindAnyObjectByType<FaunaDirector>();

            if (scavengePopulator == null)
                scavengePopulator = FindAnyObjectByType<ScavengePopulator>();

            _lastTickTime = Time.time;
            _hasTickedOnce = false;

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

            if (dt < 0.001f) dt = 0.001f;
            if (dt > 5f) dt = 5f;

            if (playerTransform == null)
            {
                ResolvePlayerAndSurvival();
                if (playerTransform == null)
                    return;
            }

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
            _debugTensionScore = _tensionScore;
            _debugPredatorFactor = _predatorFactor;
            _debugOxygenFactor = _oxygenFactor;
            _debugEnergyFactor = _energyFactor;
            _debugCalmFactor = _calmFactor;
            _debugCalmTimer = _calmTimer;
            _debugPhaseTimer = _phaseTimer;
            _debugPeakCooldown = _peakCooldownTimer;
            _debugDeltaTime = dt;
            _debugPhase = _phase.ToString();
            _debugLastEvent = _lastEventType.ToString();
            _debugPredatorPressureEnabled = _predatorPressureEnabled;
            _debugHasFaunaDirector = faunaDirector != null;
            _debugHasScavengePopulator = scavengePopulator != null;
            _debugTimeSinceLastAutoSave = now - _lastAutoSaveTime;
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  TENSION CALCULATION
        // ══════════════════════════════════════════════════════════

        private float ComputeTensionScore()
        {
            _predatorFactor = ComputePredatorFactor();
            _oxygenFactor = ComputeLowOxygenFactor();
            _energyFactor = ComputeLowEnergyFactor();
            _calmFactor = ComputeCalmFactor();

            float tension =
                _predatorFactor * predatorsWeight +
                _oxygenFactor * oxygenWeight +
                _energyFactor * energyWeight +
                _calmFactor * calmWeight;

            if (tension < 0f) tension = 0f;
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
                PredatorBuffer,
                predatorMask,
                QueryTriggerInteraction.Ignore);

            if (hitCount == 0)
                return 0f;

            float tensionSum = 0f;
            float invRadius = 1f / predatorScanRadius;

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = PredatorBuffer[i];
                if (col == null) continue;

                if (!countAnyAIAsPredator)
                {
                    if (!col.CompareTag("Predator"))
                        continue;
                }
                else
                {
                    if (!col.TryGetComponent(out HectonBaseAI _))
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
            _phaseTimer += dt;
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
            _phase = next;
            _phaseTimer = 0f;
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

        /// <summary>
        /// Пытается выполнить автосохранение при входе в фазу Relax.
        ///
        /// Условия:
        ///   1. enableAutoSave == true (Inspector toggle).
        ///   2. Прошло достаточно времени с последнего автосейва (cooldown).
        ///   3. SaveManager доступен и не занят.
        ///
        /// Fire-and-forget: async Task запускается без await.
        /// SaveManager обрабатывает все ошибки внутри SaveGameAsync.
        /// При ошибке — игра продолжает работать, просто лог ошибки.
        ///
        /// ZERO GC:
        ///   • Проверка cooldown — float сравнение.
        ///   • SaveGameAsync — snapshot фаза <2ms (main thread),
        ///     disk write — background thread.
        ///   • Fire-and-forget — Task объект создаётся, но не await-ится
        ///     в SlowTick. GC соберёт Task после завершения.
        ///     Это допустимо: автосохранение происходит раз в 5+ минут.
        /// </summary>
        private void TryAutoSave()
        {
            if (!enableAutoSave)
                return;

            float now = Time.time;

            // ── Cooldown check ──
            if (now - _lastAutoSaveTime < autoSaveCooldownSeconds)
                return;

            SaveManager saveManager = SaveManager.Instance;
            if (saveManager == null)
                return;

            if (saveManager.IsBusy)
                return;

            _lastAutoSaveTime = now;

            // ── Fire-and-forget ──
            // Мы намеренно не await-им Task в SlowTick:
            //   1. SlowTick не async — ISlowTickable.SlowTick() возвращает void.
            //   2. SaveGameAsync snapshot phase выполняется синхронно (<2ms).
            //   3. Disk write phase — background thread, не блокирует.
            //   4. Ошибки обрабатываются внутри SaveGameAsync (try-catch).
            //
            // Suppress CS4014: "Because this call is not awaited..."
            // Это intentional fire-and-forget.
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

            if (survivalSystem != null && survivalSystem.Depth >= deepLureDepthThreshold)
            {
                TriggerRareDiscovery();
                return;
            }

            int total = rareDiscoveryWeight + equipmentGlitchWeight;
            if (total <= 0)
                return;

            int roll = UnityEngine.Random.Range(0, total);

            if (roll < rareDiscoveryWeight)
                TriggerRareDiscovery();
            else
                TriggerEquipmentGlitch(0.35f);
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
            }

            _lastEventType = evt;
            _calmTimer = 0f;
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
        //  EVENT PICKING
        // ══════════════════════════════════════════════════════════

        private DirectorEventType PickPeakEvent()
        {
            int total =
                spawnHordeWeight +
                equipmentGlitchWeight +
                rareDiscoveryWeight;

            if (total <= 0)
                return DirectorEventType.EquipmentGlitch;

            int roll = UnityEngine.Random.Range(0, total);

            if (roll < spawnHordeWeight)
                return DirectorEventType.SpawnHorde;

            roll -= spawnHordeWeight;
            if (roll < equipmentGlitchWeight)
                return DirectorEventType.EquipmentGlitch;

            return DirectorEventType.RareDiscovery;
        }

        // ══════════════════════════════════════════════════════════
        //  EVENT COMMANDS — ORCHESTRATED
        // ══════════════════════════════════════════════════════════

        private void TriggerSpawnHorde()
        {
            Vector3 center = GetEventPositionAroundPlayer();

            if (faunaDirector != null)
                faunaDirector.ForceSpawnHorde(center);

            OnRequestSpawnHorde?.Invoke(center);
        }

        private void TriggerEquipmentGlitch(float intensity)
        {
            OnRequestEquipmentGlitch?.Invoke(intensity);
        }

        private void TriggerRareDiscovery()
        {
            Vector3 hintPos = GetEventPositionAroundPlayer();

            if (scavengePopulator != null)
                scavengePopulator.HighlightNearbyResource(hintPos);

            OnRequestRareDiscovery?.Invoke(hintPos);
        }

        private void PublishPredatorPressure(bool enabled)
        {
            if (_predatorPressureEnabled == enabled)
                return;

            _predatorPressureEnabled = enabled;

            if (faunaDirector != null)
                faunaDirector.SetPredatorPressure(enabled);

            OnPredatorPressureChanged?.Invoke(enabled);
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

        private Vector3 GetEventPositionAroundPlayer()
        {
            if (playerTransform == null)
                return transform.position;

            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float dist = UnityEngine.Random.Range(eventOffsetRadius * 0.4f, eventOffsetRadius);

            Vector3 pos = playerTransform.position;
            pos.x += Mathf.Cos(angle) * dist;
            pos.z += Mathf.Sin(angle) * dist;

            return pos;
        }

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

        /// <summary>
        /// Принудительно вызывает пик-событие.
        /// </summary>
        public void ForcePeak()
        {
            EnterPhase(DirectorPhase.Peak);
            ProcessPeak();
        }

        /// <summary>
        /// Сбрасывает Director в спокойное состояние.
        /// </summary>
        public void ResetDirector()
        {
            _phase = DirectorPhase.BuildUp;
            _lastEventType = DirectorEventType.None;
            _tensionScore = 0f;
            _calmTimer = 0f;
            _phaseTimer = 0f;
            _peakCooldownTimer = 0f;
            _buildUpTimer = 0f;
            _relaxMercyTimer = 0f;
            _hasTickedOnce = false;
            _lastTickTime = Time.time;

            PublishPredatorPressure(true);
        }

        /// <summary>
        /// Принудительно переводит Director в фазу Relax.
        /// </summary>
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
            if (predatorScanRadius < 1f) predatorScanRadius = 1f;
            if (predatorsForMaxTension < 1) predatorsForMaxTension = 1;
            if (calmBeforePeakSeconds < 5f) calmBeforePeakSeconds = 5f;
            if (relaxDurationSeconds < 5f) relaxDurationSeconds = 5f;
            if (peakCooldownSeconds < 0f) peakCooldownSeconds = 0f;
            if (buildUpEventIntervalSeconds < 1f) buildUpEventIntervalSeconds = 1f;
            if (eventOffsetRadius < 1f) eventOffsetRadius = 1f;
            if (relaxMercyIntervalSeconds < 5f) relaxMercyIntervalSeconds = 5f;
            if (relaxMercyTensionThreshold < 0f) relaxMercyTensionThreshold = 0f;
            if (autoSaveCooldownSeconds < 30f) autoSaveCooldownSeconds = 30f;
            if (string.IsNullOrEmpty(autoSaveSlotName)) autoSaveSlotName = "autosave";
        }

        private void OnDrawGizmosSelected()
        {
            Transform t = playerTransform != null ? playerTransform : transform;

            Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.15f);
            Gizmos.DrawWireSphere(t.position, predatorScanRadius);

            Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.12f);
            Gizmos.DrawWireSphere(t.position, eventOffsetRadius);

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