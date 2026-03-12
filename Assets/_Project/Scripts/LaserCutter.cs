// ============================================================================
// HECTON-8 — LaserCutter.cs
// Лазерный резак — конкретная реализация PlayerTool.
//
// ДВА РЕЖИМА РАБОТЫ:
//   1. Режим Резки (ЛКМ без модификатора):
//      Стандартная резка — ApplyCutDamage через ICuttable.
//
//   2. Режим Разбора (ЛКМ + зажатый R):
//      Если цель — BaseModule с CanDeconstruct() == true:
//        • Накапливает прогресс разбора (_deconstructProgress).
//        • При достижении deconstructThreshold → module.Deconstruct(inventory).
//        • При смене цели или отпускании ЛКМ/R — прогресс сбрасывается.
//      Если цель не BaseModule — ведёт себя как обычная резка.
//
// ZERO GC:
//   • Кэшированные компоненты — получаются один раз в Awake/OnSpawn.
//   • Physics.Raycast — zero GC (не Physics.RaycastAll).
//   • TryGetComponent — zero GC.
//   • Никаких строковых операций в UsePrimary().
//   • _cachedDeconstructTarget — int (InstanceID), zero boxing.
//
// ЗАВИСИМОСТИ:
//   • PlayerTool (базовый класс)
//   • Компоненты на префабе: LineRenderer, ParticleSystem (Sparks)
//   • Raycast target: ICuttable, BaseModule
//   • PlayerInventory (для Deconstruct)
// ============================================================================

namespace Hecton8.Gameplay
{
    using Hecton8.Core;
    using Hecton8.Inventory;
    using UnityEngine;

    [DisallowMultipleComponent]
    public sealed class LaserCutter : PlayerTool
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — LASER SETTINGS
        // ══════════════════════════════════════════════════════════

        [Header("── Laser Settings ────────────────────────────")]
        [Tooltip("Максимальная дальность луча (метры).")]
        [SerializeField] private float maxRange = 5f;

        [Tooltip("Урон в секунду при резке.")]
        [SerializeField] private float damagePerSecond = 25f;

        [Tooltip("LayerMask для рейкаста луча.")]
        [SerializeField] private LayerMask cuttableLayer = ~0;

        [Header("── Deconstruction ────────────────────────────")]
        [Tooltip("Время в секундах непрерывной резки для полного разбора модуля. " +
                 "Прогресс сбрасывается при смене цели или отпускании R/ЛКМ.")]
        [SerializeField] private float deconstructThreshold = 3f;

        [Tooltip("Клавиша модификатора для режима разбора. " +
                 "Зажать вместе с ЛКМ для переключения в режим Deconstruct.")]
        [SerializeField] private KeyCode deconstructModifier = KeyCode.R;

        [Header("── Visual References ─────────────────────────")]
        [Tooltip("LineRenderer для визуализации луча.")]
        [SerializeField] private LineRenderer laserLine;

        [Tooltip("ParticleSystem искр при попадании.")]
        [SerializeField] private ParticleSystem sparksVFX;

        [Header("── Audio ─────────────────────────────────────")]
        [Tooltip("AudioSource для звука резки (loop). Опционально.")]
        [SerializeField] private AudioSource cutAudio;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>Результат рейкаста (переиспользуется, zero GC).</summary>
        private RaycastHit _hitInfo;

        /// <summary>Активен ли луч в текущем кадре.</summary>
        private bool _isFiring;

        /// <summary>Был ли луч активен в предыдущем кадре (для toggle VFX).</summary>
        private bool _wasFiringLastFrame;

        /// <summary>Кэшированный Transform для позиции/направления луча.</summary>
        private Transform _cachedTransform;

        // ── Deconstruct State ──

        /// <summary>
        /// Накопленный прогресс разбора текущей цели (секунды).
        /// Сбрасывается при смене цели, отпускании R или ЛКМ.
        /// </summary>
        private float _deconstructProgress;

        /// <summary>
        /// InstanceID текущей цели разбора. Используется для определения
        /// смены цели без GetComponent (zero GC).
        /// -1 = нет активной цели.
        /// </summary>
        private int _cachedDeconstructTargetId = -1;

        /// <summary>
        /// Кэшированный BaseModule текущей цели разбора.
        /// Null если цель не является BaseModule или сменилась.
        /// </summary>
        private BaseModule _cachedDeconstructModule;

        /// <summary>
        /// Кэшированный PlayerInventory для передачи в Deconstruct.
        /// Ищется один раз при первом использовании режима разбора.
        /// </summary>
        private PlayerInventory _cachedInventory;
        private bool _inventorySearched;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE — PlayerTool overrides
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _cachedTransform = transform;
            SetVisualsActive(false);
        }

        public override void OnSpawn()
        {
            base.OnSpawn();

            _isFiring = false;
            _wasFiringLastFrame = false;
            ResetDeconstructState();

            SetVisualsActive(false);
        }

        public override void OnDespawn()
        {
            base.OnDespawn();

            _isFiring = false;
            _wasFiringLastFrame = false;
            ResetDeconstructState();

            SetVisualsActive(false);
        }

        public override void OnEquip()
        {
            base.OnEquip();
        }

        public override void OnUnequip()
        {
            _isFiring = false;
            _wasFiringLastFrame = false;
            ResetDeconstructState();

            SetVisualsActive(false);

            base.OnUnequip();
        }

        // ══════════════════════════════════════════════════════════
        //  TOOL ACTIONS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Основное действие: стрельба лазерным лучом.
        /// Вызывается каждый кадр, пока зажата ЛКМ.
        ///
        /// Два режима определяются зажатием deconstructModifier (R):
        ///   • R не зажат → режим Резки (ApplyCutDamage)
        ///   • R зажат → режим Разбора (прогресс → Deconstruct)
        /// </summary>
        public override void UsePrimary(float deltaTime)
        {
            _isFiring = true;

            Vector3 origin    = _cachedTransform.position;
            Vector3 direction = _cachedTransform.forward;

            bool didHit = Physics.Raycast(
                origin,
                direction,
                out _hitInfo,
                maxRange,
                cuttableLayer,
                QueryTriggerInteraction.Ignore);

            // ── Обновляем LineRenderer ──
            UpdateLaserLine(didHit);

            // ── Искры ──
            UpdateSparks(didHit);

            // ── Звук ──
            if (cutAudio != null && !cutAudio.isPlaying)
            {
                cutAudio.Play();
            }

            // ── Режим определяется модификатором ──
            if (didHit)
            {
                bool deconstructMode = Input.GetKey(deconstructModifier);

                if (deconstructMode)
                {
                    ProcessDeconstructMode(deltaTime);
                }
                else
                {
                    // Стандартная резка — сбрасываем прогресс разбора
                    ResetDeconstructState();
                    ApplyCutDamage(deltaTime);
                }
            }
            else
            {
                // Промах — сбрасываем прогресс
                ResetDeconstructState();
            }
        }

        public override void UseSecondary(float deltaTime)
        {
            // TODO: Реализовать альтернативный режим (сварка, фокусировка)
        }

        /// <summary>
        /// Вызывается каждый кадр (даже без нажатия кнопок).
        /// Обрабатывает отключение визуала при отпускании кнопки.
        /// </summary>
        public override void ToolTick(float deltaTime)
        {
            if (_wasFiringLastFrame && !_isFiring)
            {
                SetVisualsActive(false);
                ResetDeconstructState();
            }

            _wasFiringLastFrame = _isFiring;
            _isFiring = false;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — DECONSTRUCT MODE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Обрабатывает режим разбора: накопление прогресса и вызов Deconstruct.
        ///
        /// Логика:
        ///   1. Проверяем, является ли текущий target BaseModule.
        ///   2. Если target сменился — сбрасываем прогресс.
        ///   3. Если BaseModule.CanDeconstruct() == false — резать как обычно.
        ///   4. Накапливаем прогресс.
        ///   5. При достижении порога — Deconstruct().
        ///
        /// ZERO GC:
        ///   • Смена цели определяется через InstanceID (int), без boxing.
        ///   • TryGetComponent вызывается ТОЛЬКО при смене target.
        ///   • PlayerInventory ищется lazy (один раз за жизнь инструмента).
        /// </summary>
        private void ProcessDeconstructMode(float deltaTime)
        {
            if (_hitInfo.collider == null)
            {
                ResetDeconstructState();
                return;
            }

            int targetId = _hitInfo.collider.GetInstanceID();

            // ── Проверка смены цели ──
            if (targetId != _cachedDeconstructTargetId)
            {
                // Новая цель — сбросить прогресс и кэшировать
                _deconstructProgress = 0f;
                _cachedDeconstructTargetId = targetId;
                _cachedDeconstructModule = null;

                // Попытка получить BaseModule на новой цели (один раз)
                _hitInfo.collider.TryGetComponent(out _cachedDeconstructModule);
            }

            // ── Нет BaseModule → обычная резка ──
            if (_cachedDeconstructModule == null)
            {
                ApplyCutDamage(deltaTime);
                return;
            }

            // ── Проверка возможности деконструкции ──
            if (!_cachedDeconstructModule.CanDeconstruct())
            {
                // Модуль нельзя разобрать — режим резки (урон)
                ApplyCutDamage(deltaTime);
                return;
            }

            // ── Накопление прогресса ──
            _deconstructProgress += deltaTime;

            // ── Публикация прогресса (будущее: UI шкала) ──
            // float normalizedProgress = _deconstructProgress / deconstructThreshold;
            // DeconstructEvents.RaiseProgressUpdated(normalizedProgress);

            // ── Завершение разбора ──
            if (_deconstructProgress >= deconstructThreshold)
            {
                // Lazy-поиск PlayerInventory
                EnsurePlayerInventory();

                _cachedDeconstructModule.Deconstruct(_cachedInventory);

                ResetDeconstructState();
            }
        }

        /// <summary>
        /// Сбрасывает состояние режима разбора.
        /// Вызывается при: смене цели, отпускании ЛКМ/R, промахе, Unequip/Despawn.
        /// </summary>
        private void ResetDeconstructState()
        {
            _deconstructProgress = 0f;
            _cachedDeconstructTargetId = -1;
            _cachedDeconstructModule = null;
        }

        /// <summary>
        /// Ленивый поиск PlayerInventory. Ищется один раз за жизнь инструмента.
        /// Ищет на объекте с тегом "Player" — TryGetComponent, zero GC.
        /// </summary>
        private void EnsurePlayerInventory()
        {
            if (_inventorySearched)
                return;

            _inventorySearched = true;

            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                player.TryGetComponent(out _cachedInventory);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — CUT DAMAGE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Применяет урон к объекту под лучом.
        /// Использует TryGetComponent — zero GC.
        /// </summary>
        private void ApplyCutDamage(float deltaTime)
        {
            if (_hitInfo.collider != null &&
                _hitInfo.collider.TryGetComponent(out ICuttable cuttable))
            {
                cuttable.ApplyCutDamage(damagePerSecond * deltaTime, _hitInfo.point);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — VISUAL HELPERS
        // ══════════════════════════════════════════════════════════

        private void UpdateLaserLine(bool didHit)
        {
            if (laserLine == null) return;

            if (!laserLine.enabled)
                laserLine.enabled = true;

            laserLine.SetPosition(0, Vector3.zero);

            if (didHit)
            {
                Vector3 localHitPoint = _cachedTransform.InverseTransformPoint(_hitInfo.point);
                laserLine.SetPosition(1, localHitPoint);
            }
            else
            {
                laserLine.SetPosition(1, Vector3.forward * maxRange);
            }
        }

        private void UpdateSparks(bool didHit)
        {
            if (sparksVFX == null) return;

            if (didHit)
            {
                Transform sparksTransform = sparksVFX.transform;
                sparksTransform.position = _hitInfo.point;
                sparksTransform.rotation = Quaternion.LookRotation(_hitInfo.normal);

                if (!sparksVFX.isPlaying)
                    sparksVFX.Play();
            }
            else
            {
                if (sparksVFX.isPlaying)
                    sparksVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void SetVisualsActive(bool active)
        {
            if (laserLine != null)
                laserLine.enabled = active;

            if (sparksVFX != null)
            {
                if (!active && sparksVFX.isPlaying)
                    sparksVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (cutAudio != null)
            {
                if (!active && cutAudio.isPlaying)
                    cutAudio.Stop();
            }
        }
    }
}