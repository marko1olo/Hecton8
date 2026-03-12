// ============================================================================
// HECTON-8 — ResourceNode.cs
// Ресурсный узел — пулируемый, Zero GC, ICuttable, WorldStateManager.
//
// РЕФАКТОРИНГ v4:
//   • Реализует ICuttable (из Hecton8.Gameplay) для интеграции
//     с LaserCutter и любым другим инструментом резки.
//   • ApplyCutDamage() делегирует в TakeDamage().
//   • uniqueId для идентификации в системе сохранений.
//   • При разрушении: регистрация в WorldStateManager.
//   • При OnSpawn(): проверка IsNodeDepleted → самодеспавн через пул.
//   • При OnEnable(): проверка IsNodeDepleted → самодеактивация
//     (для scene-объектов, не проходящих через пул).
//   • IPoolable — корректная работа с ObjectPoolManager.
//   • Лут спавнится через ObjectPoolManager.Spawn().
//   • НЕТ Update().
//
// УНИКАЛЬНЫЙ ID:
//   Назначается в Inspector (вручную или Editor-скриптом).
//   Рекомендация: формат "scene_objectName" или координатный хэш.
//   Пустой ID = узел не отслеживается системой сохранений.
//
// ПОРЯДОК ВЫЗОВОВ ПРИ СПАВНЕ ИЗ ПУЛА:
//   1. Instantiate (только при Warmup/Expand)
//   2. Awake()
//   3. SetActive(true)         — ObjectPoolManager.Spawn
//   4. OnEnable()              — Unity callback
//   5. OnSpawn()               — IPoolable callback от ObjectPoolManager
//   6. [Если depleted] → OnDespawn() + SetActive(false)
//
// Таким образом, проверка в OnSpawn() гарантирует деспавн
// ПОСЛЕ полной инициализации.
// ============================================================================

using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.Scavenging
{
    [DisallowMultipleComponent]
    public sealed class ResourceNode : MonoBehaviour, IPoolable, ICuttable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Identity ──────────────────────────────────")]
        [Tooltip("Уникальный ID для системы сохранений. " +
                 "Пустой = узел не сохраняется. " +
                 "Формат: 'zone_pipe_01' или 'hull_panel_north_03'")]
        [SerializeField] private string uniqueId;

        [Header("── Health ────────────────────────────────────")]
        [Tooltip("Максимальное здоровье узла. Определяет время резки.")]
        [SerializeField] private float maxHealth = 100f;

        [Header("── Loot ──────────────────────────────────────")]
        [Tooltip("Префаб куска ресурса (должен быть прогрет в ObjectPoolManager)")]
        [SerializeField] private GameObject lootPrefab;

        [Tooltip("Количество кусков, выпадающих при разрушении")]
        [SerializeField] private int lootCount = 3;

        [Tooltip("Время жизни лута (сек). После — автовозврат в пул.")]
        [SerializeField] private float lootLifetime = 30f;

        [Header("── Scatter ───────────────────────────────────")]
        [Tooltip("Радиус случайного смещения точки спавна лута")]
        [SerializeField] private float scatterRadius = 0.3f;

        [Tooltip("Сила случайного разброса (AddForce, Impulse)")]
        [SerializeField] private float scatterForce = 2.5f;

        [Tooltip("Дополнительная сила подброса вверх")]
        [SerializeField] private float upwardBias = 1.5f;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private float _currentHealth;
        private bool  _isDepleted;

        /// <summary>
        /// Флаг: объект уже запрошен на деспавн через пул.
        /// Предотвращает двойной Despawn (из TakeDamage + из OnSpawn).
        /// </summary>
        private bool _despawnRequested;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC ACCESSORS
        // ══════════════════════════════════════════════════════════

        /// <summary>Уникальный ID узла для системы сохранений.</summary>
        public string UniqueId => uniqueId;
        /// <summary>
        /// Устанавливает уникальный ID извне.
        /// Вызывается ScavengePopulator при спавне из пула.
        /// 
        /// ВАЖНО: вызывать ПОСЛЕ OnSpawn() и ДО любой логики,
        /// зависящей от uniqueId.
        /// 
        /// Безопасно вызывать повторно (перезапись).
        /// </summary>
        public void SetUniqueId(string id)
        {
            uniqueId = id;
        }
        /// <summary>Текущее здоровье узла (0 = разрушен).</summary>    
        public float CurrentHealth => _currentHealth;

        /// <summary>Нормализованное здоровье [0..1] для UI / VFX.</summary>
        public float HealthNormalized => maxHealth > 0f ? _currentHealth / maxHealth : 0f;

        /// <summary>true после разрушения.</summary>
        public bool IsDepleted => _isDepleted;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            ResetState();
        }

        /// <summary>
        /// OnEnable: проверяет WorldStateManager.
        /// Если узел уже был уничтожен в предыдущей сессии —
        /// самодеактивируется немедленно.
        ///
        /// Обеспечивает совместимость со scene-объектами (не из пула).
        /// Для пулированных объектов основная проверка — в OnSpawn().
        /// </summary>
        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(uniqueId))
            {
                WorldStateManager wsm = WorldStateManager.Instance;
                if (wsm != null && wsm.IsNodeDepleted(uniqueId))
                {
                    _isDepleted = true;
                    gameObject.SetActive(false);
                    return;
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  IPoolable — ЖИЗНЕННЫЙ ЦИКЛ ПУЛА
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вызывается ObjectPoolManager ПОСЛЕ SetActive(true) и OnEnable().
        ///
        /// Содержит критическую проверку: если узел уже depleted
        /// в WorldStateManager, объект немедленно деспавнится обратно
        /// в пул. Это защита от появления уже собранных ресурсов
        /// при загрузке чанка.
        ///
        /// ПОРЯДОК: SetActive(true) → OnEnable() → OnSpawn()
        /// Если OnEnable() уже деактивировал — OnSpawn() не вызовется
        /// (ObjectPoolManager вызывает OnSpawn после SetActive).
        /// Но на всякий случай проверяем и здесь (belt and suspenders).
        /// </summary>
        public void OnSpawn()
        {
            ResetState();

            // ── Проверка: был ли узел уже уничтожен (из сейва)? ──
            if (!string.IsNullOrEmpty(uniqueId))
            {
                WorldStateManager wsm = WorldStateManager.Instance;
                if (wsm != null && wsm.IsNodeDepleted(uniqueId))
                {
                    // Узел уже собран — деспавним обратно в пул
                    _isDepleted       = true;
                    _despawnRequested = true;

                    ObjectPoolManager pool = ObjectPoolManager.Instance;
                    if (pool != null)
                    {
                        pool.Despawn(gameObject);
                    }
                    else
                    {
                        gameObject.SetActive(false);
                    }

                    return;
                }
            }
        }

        /// <summary>
        /// Вызывается ObjectPoolManager ПЕРЕД SetActive(false).
        /// Сбрасывает состояние для переиспользования.
        /// </summary>
        public void OnDespawn()
        {
            ResetState();
        }

        // ══════════════════════════════════════════════════════════
        //  ICuttable — ИНТЕГРАЦИЯ С LaserCutter
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Реализация ICuttable.ApplyCutDamage.
        /// Делегирует в TakeDamage. Параметр hitPoint доступен
        /// для будущего расширения (декали, направленные VFX).
        ///
        /// Вызывается LaserCutter.UsePrimary() при рейкаст-попадании.
        /// </summary>
        /// <param name="damage">Урон за кадр (damagePerSecond × deltaTime).</param>
        /// <param name="hitPoint">Мировая точка попадания луча.</param>
        public void ApplyCutDamage(float damage, Vector3 hitPoint)
        {
            TakeDamage(damage);
        }

        // ══════════════════════════════════════════════════════════
        //  DAMAGE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Наносит урон узлу. При health ≤ 0:
        ///   1. Спавнит лут через пул.
        ///   2. Регистрирует уничтожение в WorldStateManager.
        ///   3. Деспавнит себя.
        ///
        /// Защита от двойного вызова: если _isDepleted или
        /// _despawnRequested — ранний выход.
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (_isDepleted)        return;
            if (_despawnRequested)  return;
            if (amount <= 0f)       return;

            _currentHealth -= amount;

            if (_currentHealth <= 0f)
            {
                _currentHealth = 0f;
                _isDepleted    = true;

                SpawnLoot();

                // ── Регистрация в WorldStateManager ──
                if (!string.IsNullOrEmpty(uniqueId))
                {
                    WorldStateManager wsm = WorldStateManager.Instance;
                    if (wsm != null)
                    {
                        wsm.RegisterDepletedNode(uniqueId);
                    }
                }

                DespawnSelf();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  LOOT SPAWNING — через ObjectPoolManager
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Спавнит лут через ObjectPoolManager.
        /// Каждый кусок получает случайное направление разброса.
        ///
        /// Zero GC: Random struct methods, TryGetComponent.
        /// </summary>
        private void SpawnLoot()
        {
            if (lootPrefab == null) return;

            ObjectPoolManager pool = ObjectPoolManager.Instance;

            if (pool == null)
            {
                SpawnLootFallback();
                return;
            }

            Vector3 origin = transform.position;

            for (int i = 0; i < lootCount; i++)
            {
                Vector3    offset   = Random.insideUnitSphere * scatterRadius;
                Vector3    spawnPos = origin + offset;
                Quaternion spawnRot = Random.rotation;

                GameObject loot = pool.Spawn(lootPrefab, spawnPos, spawnRot);

                if (loot == null) continue;

                if (loot.TryGetComponent(out Rigidbody rb))
                {
                    Vector3 force = Random.insideUnitSphere * scatterForce;
                    force.y = Mathf.Abs(force.y) + upwardBias;

                    rb.AddForce(force, ForceMode.Impulse);
                    rb.AddTorque(
                        Random.insideUnitSphere * (scatterForce * 0.5f),
                        ForceMode.Impulse);
                }

                if (lootLifetime > 0f)
                {
                    pool.Despawn(loot, lootLifetime);
                }
            }
        }

        /// <summary>
        /// Fallback для спавна лута, если ObjectPoolManager недоступен.
        /// Использует Instantiate (нарушает Zero GC — только для safety).
        /// </summary>
        private void SpawnLootFallback()
        {
            Vector3 origin = transform.position;

            for (int i = 0; i < lootCount; i++)
            {
                Vector3    offset   = Random.insideUnitSphere * scatterRadius;
                Quaternion spawnRot = Random.rotation;
                GameObject loot     = Instantiate(lootPrefab, origin + offset, spawnRot);

                if (loot != null && loot.TryGetComponent(out Rigidbody rb))
                {
                    Vector3 force = Random.insideUnitSphere * scatterForce;
                    force.y = Mathf.Abs(force.y) + upwardBias;
                    rb.AddForce(force, ForceMode.Impulse);
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  SELF-DESPAWN
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Деспавнит себя через ObjectPoolManager, если объект из пула.
        /// Иначе — просто деактивирует.
        /// Защита от двойного деспавна через _despawnRequested.
        /// </summary>
        private void DespawnSelf()
        {
            if (_despawnRequested) return;
            _despawnRequested = true;

            ObjectPoolManager pool = ObjectPoolManager.Instance;

            if (pool != null &&
                TryGetComponent(out ObjectPoolManager.PoolItemMarker _))
            {
                pool.Despawn(gameObject);
                return;
            }

            gameObject.SetActive(false);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — STATE RESET
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Полный сброс состояния для переиспользования.
        /// Вызывается в Awake, OnSpawn, OnDespawn.
        /// </summary>
        private void ResetState()
        {
            _currentHealth    = maxHealth;
            _isDepleted       = false;
            _despawnRequested = false;
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (maxHealth     < 1f)  maxHealth     = 1f;
            if (lootCount     < 0)   lootCount     = 0;
            if (lootLifetime  < 0f)  lootLifetime  = 0f;
            if (scatterRadius < 0f)  scatterRadius = 0f;
            if (scatterForce  < 0f)  scatterForce  = 0f;
        }

        /// <summary>
        /// Контекстное меню: генерирует uniqueId из имени объекта + позиции.
        /// Правый клик на компоненте → Generate Unique ID.
        /// </summary>
        [ContextMenu("Generate Unique ID")]
        private void GenerateUniqueId()
        {
            Vector3 pos = transform.position;
            uniqueId = $"{gameObject.name}_{pos.x:F1}_{pos.y:F1}_{pos.z:F1}";
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[ResourceNode] Generated ID: {uniqueId}");
        }

        /// <summary>
        /// Визуализация узла в Scene View.
        /// Зелёный = есть ID. Серый = нет ID (не сохраняется).
        /// </summary>
        private void OnDrawGizmos()
        {
            bool hasId = !string.IsNullOrEmpty(uniqueId);
            Gizmos.color = hasId
                ? new Color(0f, 1f, 0.5f, 0.3f)
                : new Color(0.5f, 0.5f, 0.5f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, 0.3f);

            if (hasId)
            {
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 0.5f,
                    uniqueId,
                    new GUIStyle
                    {
                        fontSize  = 9,
                        normal    = { textColor = new Color(0f, 1f, 0.5f, 0.7f) }
                    });
            }
        }
#endif
    }
}