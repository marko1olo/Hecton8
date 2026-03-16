// ============================================================================
// HECTON-8 — ResourceNode.cs
// Ресурсный узел — пулируемый, Zero GC, ICuttable, WorldStateManager, Melt VFX.
//
// v5.0 — MELT EFFECT (GPU-driven dissolve):
//   [ADD] MaterialPropertyBlock интеграция для эффекта плавления.
//   [ADD] В ApplyCutDamage: передаёт _MeltCenter (local space) и
//         _MeltRadius (1 - healthNormalized) в шейдер через PropertyBlock.
//   [ADD] _MeltRadius сбрасывается в 0 при OnSpawn, OnDespawn, ResetState.
//   [ADD] Кэшированный Renderer + MaterialPropertyBlock в Awake (zero GC per-frame).
//   [ADD] Статические Shader.PropertyToID для zero-GC SetVector/SetFloat.
//
//   ШЕЙДЕР (URP):
//     Получает _MeltCenter и _MeltRadius.
//     distance(objectPos, _MeltCenter) < _MeltRadius → clip(-1) (отсечение).
//     На границе среза → HDR emission (жёлтый→красный градиент).
//     Zero CPU overhead — вся визуализация на GPU.
//
// ПРЕДЫДУЩИЕ ВЕРСИИ (сохранены):
//   v4.1: Детерминированная генерация ID, autoGenerateId, chunkSize.
//   v4.0: ICuttable, IPoolable, WorldStateManager, лут через пул.
//
// ZERO GC В RUNTIME:
//   • ApplyCutDamage: MaterialPropertyBlock.SetVector/SetFloat — zero GC.
//   • Renderer.GetPropertyBlock/SetPropertyBlock — zero GC.
//   • Shader.PropertyToID — cached static, zero GC.
//   • Все per-frame пути: zero GC.
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
        //  SHADER PROPERTY IDs — cached once, zero GC
        // ══════════════════════════════════════════════════════════

        private static readonly int _MeltCenterID = Shader.PropertyToID("_MeltCenter");
        private static readonly int _MeltRadiusID = Shader.PropertyToID("_MeltRadius");

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — IDENTITY (v4.1)
        // ══════════════════════════════════════════════════════════

        [Header("── Identity ──────────────────────────────────")]
        [Tooltip("Уникальный ID для системы сохранений. " +
                 "Если autoGenerateId=true, заполняется автоматически из координат. " +
                 "Если autoGenerateId=false, назначается вручную. " +
                 "Пустой ID = узел не сохраняется.")]
        [SerializeField] private string uniqueId;

        [Tooltip("Автоматически генерировать uniqueId из мировых координат.")]
        [SerializeField] private bool autoGenerateId = true;

        [Tooltip("Размер чанка MapMagic (метры).")]
        [SerializeField] private float chunkSize = 1000f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — HEALTH
        // ══════════════════════════════════════════════════════════

        [Header("── Health ────────────────────────────────────")]
        [Tooltip("Максимальное здоровье узла. Определяет время резки.")]
        [SerializeField] private float maxHealth = 100f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — LOOT
        // ══════════════════════════════════════════════════════════

        [Header("── Loot ──────────────────────────────────────")]
        [Tooltip("Префаб куска ресурса (должен быть прогрет в ObjectPoolManager)")]
        [SerializeField] private GameObject lootPrefab;

        [Tooltip("Количество кусков, выпадающих при разрушении")]
        [SerializeField] private int lootCount = 3;

        [Tooltip("Время жизни лута (сек). После — автовозврат в пул.")]
        [SerializeField] private float lootLifetime = 30f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — SCATTER
        // ══════════════════════════════════════════════════════════

        [Header("── Scatter ───────────────────────────────────")]
        [Tooltip("Радиус случайного смещения точки спавна лута")]
        [SerializeField] private float scatterRadius = 0.3f;

        [Tooltip("Сила случайного разброса (AddForce, Impulse)")]
        [SerializeField] private float scatterForce = 2.5f;

        [Tooltip("Дополнительная сила подброса вверх")]
        [SerializeField] private float upwardBias = 1.5f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — MELT VFX (v5.0)
        // ══════════════════════════════════════════════════════════

        [Header("── Melt VFX (v5.0) ──────────────────────────")]
        [Tooltip("Максимальный радиус плавления в локальных единицах. " +
                 "При health=0 → _MeltRadius = maxMeltRadius. " +
                 "Зависит от масштаба меша. Для 1м трубы: ~0.5.")]
        [SerializeField] private float maxMeltRadius = 0.5f;

        [Tooltip("Целевой Renderer для MaterialPropertyBlock. " +
                 "Если не назначен — ищется автоматически в Awake.")]
        [SerializeField] private Renderer targetRenderer;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private Transform _transform;
        private float _currentHealth;
        private bool  _isDepleted;
        private bool  _despawnRequested;

        /// <summary>
        /// Кэшированный MaterialPropertyBlock. Создаётся один раз в Awake.
        /// Переиспользуется во всех вызовах ApplyCutDamage — zero GC.
        /// </summary>
        private MaterialPropertyBlock _propBlock;

        /// <summary>
        /// Последняя точка попадания лазера в локальных координатах.
        /// Обновляется каждый кадр при резке. Передаётся в шейдер как _MeltCenter.
        /// Vector4 (x, y, z, 0) для совместимости с SetVector.
        /// </summary>
        private Vector4 _localHitPoint;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC ACCESSORS
        // ══════════════════════════════════════════════════════════

        public string UniqueId => uniqueId;

        public void SetUniqueId(string id)
        {
#if UNITY_EDITOR
            if (autoGenerateId)
            {
                Debug.LogWarning(
                    $"[ResourceNode] SetUniqueId() called while autoGenerateId=true " +
                    $"on '{gameObject.name}'.", this);
            }
#endif
            uniqueId = id;
        }

        public float CurrentHealth => _currentHealth;
        public float HealthNormalized => maxHealth > 0f ? _currentHealth / maxHealth : 0f;
        public bool IsDepleted => _isDepleted;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _transform = transform;

            // ── MaterialPropertyBlock: один раз, zero GC в рантайме ──
            _propBlock = new MaterialPropertyBlock();

            // ── Авто-поиск Renderer ──
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<Renderer>();
            }

            ResetState();
        }

        private void OnEnable()
        {
            if (autoGenerateId && string.IsNullOrEmpty(uniqueId))
            {
                GenerateDeterministicId();
            }

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
        //  IPoolable
        // ══════════════════════════════════════════════════════════

        public void OnSpawn()
        {
            ResetState();

            if (autoGenerateId && string.IsNullOrEmpty(uniqueId))
            {
                GenerateDeterministicId();
            }

            if (!string.IsNullOrEmpty(uniqueId))
            {
                WorldStateManager wsm = WorldStateManager.Instance;
                if (wsm != null && wsm.IsNodeDepleted(uniqueId))
                {
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

        public void OnDespawn()
        {
            ResetState();

            if (autoGenerateId)
            {
                uniqueId = null;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ICuttable — LASER CUTTER INTEGRATION + MELT VFX (v5.0)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Реализация ICuttable.ApplyCutDamage.
        ///
        /// v5.0: Помимо делегирования в TakeDamage, обновляет
        /// MaterialPropertyBlock для GPU-эффекта плавления:
        ///
        ///   _MeltCenter — точка попадания лазера в ЛОКАЛЬНЫХ координатах меша.
        ///     InverseTransformPoint(hitPoint) переводит мировую координату
        ///     в object-space, совпадающий с вершинами меша.
        ///     Шейдер использует IN.positionOS для Distance().
        ///
        ///   _MeltRadius — радиус расплавленной зоны [0..maxMeltRadius].
        ///     Формула: (1.0 - HealthNormalized) × maxMeltRadius.
        ///     При полном здоровье → 0 (нет эффекта).
        ///     При health=0 → maxMeltRadius (максимальное разрушение).
        ///     Шейдер: distance &lt; _MeltRadius → clip(-1) (отсечение).
        ///
        /// ZERO GC:
        ///   • InverseTransformPoint — struct math (Vector3 → Vector3).
        ///   • MaterialPropertyBlock.SetVector/SetFloat — zero GC.
        ///   • Renderer.GetPropertyBlock/SetPropertyBlock — zero GC.
        ///   • Shader.PropertyToID — cached static readonly int.
        ///
        /// ПОРЯДОК ОПЕРАЦИЙ:
        ///   1. TakeDamage (уменьшает health, может вызвать DestroyNode).
        ///   2. Если не depleted → обновляем MaterialPropertyBlock.
        ///   3. GetPropertyBlock → SetVector + SetFloat → SetPropertyBlock.
        ///      GetPropertyBlock необходим для сохранения других свойств
        ///      (текстуры, цвета), которые могли быть установлены ранее.
        /// </summary>
        public void ApplyCutDamage(float damage, Vector3 hitPoint)
        {
            TakeDamage(damage);

            // ── Обновляем мельт-параметры шейдера (v5.0) ──
            // Только если объект ещё жив И рендерер доступен.
            // После TakeDamage объект мог быть depleted → не трогаем.
            if (!_isDepleted && targetRenderer != null)
            {
                UpdateMeltProperties(hitPoint);
            }
        }

        /// <summary>
        /// Обновляет MaterialPropertyBlock с параметрами плавления.
        /// Вызывается из ApplyCutDamage каждый кадр при резке.
        ///
        /// Zero GC: все операции на struct'ах + cached PropertyBlock.
        /// CPU cost: ~0.001ms (InverseTransformPoint + 2× Set + Apply).
        /// </summary>
        private void UpdateMeltProperties(Vector3 worldHitPoint)
        {
            // ── Мировые → локальные координаты ──
            // InverseTransformPoint учитывает position, rotation, scale объекта.
            // Результат совпадает с vertex position в object-space шейдера.
            Vector3 localPos = _transform.InverseTransformPoint(worldHitPoint);
            _localHitPoint.x = localPos.x;
            _localHitPoint.y = localPos.y;
            _localHitPoint.z = localPos.z;
            _localHitPoint.w = 0f;

            // ── Радиус плавления: растёт по мере уменьшения здоровья ──
            // healthNorm = 1.0 (полное здоровье) → meltRadius = 0 (нет эффекта)
            // healthNorm = 0.5 (половина) → meltRadius = 0.5 × maxMeltRadius
            // healthNorm = 0.0 (уничтожен) → meltRadius = maxMeltRadius
            float meltRadius = (1f - HealthNormalized) * maxMeltRadius;

            // ── Передаём в шейдер через MaterialPropertyBlock ──
            // GetPropertyBlock: загружает текущие override'ы (сохраняет чужие свойства).
            // SetVector/SetFloat: обновляет только наши 2 свойства.
            // SetPropertyBlock: применяет блок к рендереру.
            targetRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetVector(_MeltCenterID, _localHitPoint);
            _propBlock.SetFloat(_MeltRadiusID, meltRadius);
            targetRenderer.SetPropertyBlock(_propBlock);
        }

        /// <summary>
        /// Сбрасывает melt-параметры шейдера в исходное состояние.
        /// Вызывается из ResetState (→ Awake, OnSpawn, OnDespawn).
        ///
        /// _MeltRadius = 0 → шейдер ничего не отсекает и не подсвечивает.
        /// _MeltCenter = origin (0,0,0) → безопасное значение.
        /// </summary>
        private void ResetMeltProperties()
        {
            if (targetRenderer == null) return;
            if (_propBlock == null) return;

            targetRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetVector(_MeltCenterID, Vector4.zero);
            _propBlock.SetFloat(_MeltRadiusID, 0f);
            targetRenderer.SetPropertyBlock(_propBlock);
        }

        // ══════════════════════════════════════════════════════════
        //  DAMAGE
        // ══════════════════════════════════════════════════════════

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
        //  LOOT SPAWNING
        // ══════════════════════════════════════════════════════════

        private void SpawnLoot()
        {
            if (lootPrefab == null) return;

            ObjectPoolManager pool = ObjectPoolManager.Instance;

            if (pool == null)
            {
                SpawnLootFallback();
                return;
            }

            Vector3 origin = _transform.position;

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

        private void SpawnLootFallback()
        {
            Vector3 origin = _transform.position;

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
        //  DETERMINISTIC ID GENERATION (v4.1)
        // ══════════════════════════════════════════════════════════

        private void GenerateDeterministicId()
        {
            Vector3 pos = _transform.position;

            float safeChunkSize = chunkSize > 0f ? chunkSize : 1000f;
            int chunkX = Mathf.FloorToInt(pos.x / safeChunkSize);
            int chunkZ = Mathf.FloorToInt(pos.z / safeChunkSize);

            int mmX = Mathf.RoundToInt(pos.x * 1000f);
            int mmY = Mathf.RoundToInt(pos.y * 1000f);
            int mmZ = Mathf.RoundToInt(pos.z * 1000f);

            uniqueId = $"rn_{chunkX}_{chunkZ}_{mmX}_{mmY}_{mmZ}";
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — STATE RESET (v5.0: + melt reset)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Полный сброс состояния для переиспользования.
        /// v5.0: Добавлен ResetMeltProperties() — сбрасывает
        /// _MeltRadius в 0 на MaterialPropertyBlock.
        /// </summary>
        private void ResetState()
        {
            _currentHealth    = maxHealth;
            _isDepleted       = false;
            _despawnRequested = false;

            // v5.0: Сброс мельт-эффекта
            ResetMeltProperties();
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (maxHealth      < 1f)  maxHealth      = 1f;
            if (lootCount      < 0)   lootCount      = 0;
            if (lootLifetime   < 0f)  lootLifetime   = 0f;
            if (scatterRadius  < 0f)  scatterRadius  = 0f;
            if (scatterForce   < 0f)  scatterForce   = 0f;
            if (chunkSize      < 1f)  chunkSize      = 1f;
            if (maxMeltRadius  < 0f)  maxMeltRadius  = 0f;
        }

        [ContextMenu("Generate Deterministic ID")]
        private void EditorGenerateDeterministicId()
        {
            Vector3 pos = transform.position;
            float safeChunkSize = chunkSize > 0f ? chunkSize : 1000f;
            int chX = Mathf.FloorToInt(pos.x / safeChunkSize);
            int chZ = Mathf.FloorToInt(pos.z / safeChunkSize);
            int mmX = Mathf.RoundToInt(pos.x * 1000f);
            int mmY = Mathf.RoundToInt(pos.y * 1000f);
            int mmZ = Mathf.RoundToInt(pos.z * 1000f);
            uniqueId = $"rn_{chX}_{chZ}_{mmX}_{mmY}_{mmZ}";
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[ResourceNode] Generated deterministic ID: {uniqueId}", this);
        }

        [ContextMenu("Generate Legacy ID (name + position)")]
        private void EditorGenerateLegacyId()
        {
            Vector3 pos = transform.position;
            uniqueId = $"{gameObject.name}_{pos.x:F1}_{pos.y:F1}_{pos.z:F1}";
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[ResourceNode] Generated legacy ID: {uniqueId}", this);
        }

        private void OnDrawGizmos()
        {
            bool hasId = !string.IsNullOrEmpty(uniqueId);

            if (autoGenerateId)
            {
                Gizmos.color = hasId
                    ? new Color(0f, 0.8f, 1f, 0.3f)
                    : new Color(0f, 0.5f, 0.8f, 0.15f);
            }
            else
            {
                Gizmos.color = hasId
                    ? new Color(0f, 1f, 0.5f, 0.3f)
                    : new Color(0.5f, 0.5f, 0.5f, 0.2f);
            }

            Gizmos.DrawWireSphere(transform.position, 0.3f);

            if (hasId)
            {
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 0.5f,
                    uniqueId,
                    new GUIStyle
                    {
                        fontSize  = 9,
                        normal    = { textColor = autoGenerateId
                            ? new Color(0f, 0.8f, 1f, 0.7f)
                            : new Color(0f, 1f, 0.5f, 0.7f) }
                    });
            }
        }
#endif
    }
}