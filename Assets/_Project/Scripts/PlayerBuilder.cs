// ============================================================================
// HECTON-8 — PlayerBuilder.cs
// Контроллер строительства модульной базы.
//
// РЕФАКТОРИНГ v2 — НАСЛЕДНИК PlayerTool:
//   • Больше НЕ реализует ITickable. Управляется PlayerToolManager.
//   • OnEquip()    → вход в режим строительства (спавн призрака).
//   • OnUnequip()  → выход из режима строительства (деспавн призрака).
//   • ToolTick(dt) → обновление позиции призрака (каждый кадр).
//   • UsePrimary(dt)   → размещение модуля (ЛКМ / Fire1).
//   • UseSecondary(dt) → вращение призрака на 90° (ПКМ / Fire2).
//
// АРХИТЕКТУРА:
//   • PlayerTool (базовый класс) + IPoolable.
//   • ObjectPoolManager — спавн/деспавн призраков и финальных модулей.
//   • Ресурсы проверяются через InventoryGrid (1x1 ресурсы).
//   • Frame-rate independent lerp для плавного следования.
//
// ПЕРЕКЛЮЧЕНИЕ:
//   • Игрок переключается на Builder через PlayerToolManager (кнопки 1-4).
//   • Кнопка [B] УДАЛЕНА — весь ввод через систему инструментов.
//   • [Escape] для отмены тоже удалён — снятие инструмента через
//     PlayerToolManager (повторное нажатие слота = holster).
//
// ОГРАНИЧЕНИЯ MVP:
//   • Стоимость постройки: только 1×1 ресурсы (Titanium, Glass, etc.)
//   • Один активный модуль (activeBuildable). Меню выбора — будущее.
//   • Нет системы snap-точек (будущее расширение).
// ============================================================================

using System.Collections.Generic;
using Hecton8.Building;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using UnityEngine;

namespace Hecton8.Building
{
    [DisallowMultipleComponent]
    public sealed class PlayerBuilder : PlayerTool
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — REFERENCES
        // ══════════════════════════════════════════════════════════

        [Header("── Builder References ────────────────────────")]
        [Tooltip("Инвентарь игрока для проверки и списания ресурсов")]
        [SerializeField] private PlayerInventory inventory;

        [Tooltip("Камера игрока (от неё пускается Raycast)")]
        [SerializeField] private Camera playerCamera;

        [Tooltip("Точка перед камерой (fallback, если Raycast в пустоту)")]
        [SerializeField] private Transform buildAnchor;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — BUILDING
        // ══════════════════════════════════════════════════════════

        [Header("── Building ──────────────────────────────────")]
        [Tooltip("Активный модуль для строительства. " +
                 "Будущее: заменится на меню выбора модулей.")]
        [SerializeField] private BuildableData activeBuildable;

        [Tooltip("Максимальная дальность размещения (метры)")]
        [SerializeField] private float buildDistance = 8f;

        [Tooltip("Скорость сглаживания движения призрака")]
        [SerializeField] private float ghostFollowSpeed = 12f;

        [Tooltip("Слой поверхности для размещения (Terrain, Default)")]
        [SerializeField] private LayerMask surfaceMask = ~0;

        [Header("── Rotation ──────────────────────────────────")]
        [Tooltip("Угол поворота призрака при нажатии ПКМ (градусы)")]
        [SerializeField] private float rotationStep = 90f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — AUDIO (optional)
        // ══════════════════════════════════════════════════════════

        [Header("── Audio ─────────────────────────────────────")]
        [Tooltip("Источник звука для строительных эффектов")]
        [SerializeField] private AudioSource audioSource;

        [Tooltip("Звук успешной постройки")]
        [SerializeField] private AudioClip buildSound;

        [Tooltip("Звук ошибки (нет ресурсов, нельзя строить)")]
        [SerializeField] private AudioClip errorSound;

        [Tooltip("Звук поворота призрака")]
        [SerializeField] private AudioClip rotateSound;

        // ══════════════════════════════════════════════════════════
        //  CACHED STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>Экземпляр призрака (из пула).</summary>
        private GameObject _currentGhostObj;

        /// <summary>Компонент PlacementGhost на призраке.</summary>
        private PlacementGhost _currentGhost;

        /// <summary>Кэшированный RaycastHit. Struct — zero GC.</summary>
        private RaycastHit _hit;

        /// <summary>Кэшированный center-viewport вектор.</summary>
        private static readonly Vector3 ViewportCenter = new Vector3(0.5f, 0.5f, 0f);

        /// <summary>
        /// Дополнительный поворот призрака, накопленный через UseSecondary.
        /// Сбрасывается при OnUnequip/OnDespawn.
        /// </summary>
        private float _ghostYawOffset;

        /// <summary>
        /// Защита от многократного вращения за один кадр.
        /// UseSecondary вызывается каждый кадр при зажатой кнопке,
        /// но вращение должно происходить однократно при нажатии.
        /// </summary>
        private bool _secondaryWasPressed;

        /// <summary>
        /// Защита от многократного размещения.
        /// UsePrimary вызывается каждый кадр при зажатой кнопке.
        /// </summary>
        private bool _primaryWasPressed;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>Текущий активный модуль.</summary>
        public BuildableData ActiveBuildable => activeBuildable;

        /// <summary>
        /// Программная смена активного модуля.
        /// Если инструмент экипирован — пересоздаёт призрак.
        /// </summary>
        public void SetActiveBuildable(BuildableData data)
        {
            if (data == null) return;

            bool wasEquipped = IsEquipped;

            if (wasEquipped)
                DespawnGhost();

            activeBuildable = data;

            if (wasEquipped)
                SpawnGhost();
        }

        // ══════════════════════════════════════════════════════════
        //  IPoolable — POOL LIFECYCLE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вызывается ObjectPoolManager при извлечении из пула.
        /// Сбрасывает всё состояние строителя.
        /// </summary>
        public override void OnSpawn()
        {
            base.OnSpawn();

            _ghostYawOffset     = 0f;
            _secondaryWasPressed = false;
            _primaryWasPressed   = false;
        }

        /// <summary>
        /// Вызывается ObjectPoolManager при возврате в пул.
        /// Гарантирует деспавн призрака.
        /// </summary>
        public override void OnDespawn()
        {
            // base.OnDespawn() вызовет OnUnequip() если IsEquipped
            DespawnGhost();

            _ghostYawOffset      = 0f;
            _secondaryWasPressed = false;
            _primaryWasPressed   = false;

            base.OnDespawn();
        }

        // ══════════════════════════════════════════════════════════
        //  TOOL LIFECYCLE — вызывается PlayerToolManager
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вход в режим строительства.
        /// Вызывается PlayerToolManager при экипировке инструмента.
        /// Спавнит призрак постройки.
        /// </summary>
        public override void OnEquip()
        {
            base.OnEquip();

            _ghostYawOffset      = 0f;
            _secondaryWasPressed = false;
            _primaryWasPressed   = false;

            SpawnGhost();
        }

        /// <summary>
        /// Выход из режима строительства.
        /// Вызывается PlayerToolManager при снятии инструмента.
        /// Деспавнит призрак постройки.
        /// </summary>
        public override void OnUnequip()
        {
            DespawnGhost();

            _ghostYawOffset      = 0f;
            _secondaryWasPressed = false;
            _primaryWasPressed   = false;

            base.OnUnequip();
        }

        /// <summary>
        /// Вызывается каждый кадр через PlayerToolManager.Tick().
        /// Обновляет позицию призрака (плавное следование за взглядом).
        ///
        /// Также сбрасывает флаги однократного нажатия, если кнопки
        /// были отпущены (для корректной работы UsePrimary/UseSecondary).
        /// </summary>
        public override void ToolTick(float deltaTime)
        {
            // ── Сброс флагов при отпускании кнопок ──
            if (!Input.GetButton("Fire1"))
                _primaryWasPressed = false;

            if (!Input.GetButton("Fire2"))
                _secondaryWasPressed = false;

            // ── Обновление позиции призрака ──
            if (_currentGhostObj != null)
                UpdateGhostPosition(deltaTime);
        }

        /// <summary>
        /// Основное действие: размещение модуля (ЛКМ / Fire1).
        /// Вызывается каждый кадр, пока зажата кнопка.
        /// Размещение происходит однократно при первом нажатии.
        /// </summary>
        public override void UsePrimary(float deltaTime)
        {
            // ── Защита от многократного размещения ──
            if (_primaryWasPressed)
                return;

            _primaryWasPressed = true;

            TryPlaceModule();
        }

        /// <summary>
        /// Альтернативное действие: вращение призрака (ПКМ / Fire2).
        /// Вызывается каждый кадр, пока зажата кнопка.
        /// Поворот происходит однократно при первом нажатии.
        /// </summary>
        public override void UseSecondary(float deltaTime)
        {
            // ── Защита от многократного вращения ──
            if (_secondaryWasPressed)
                return;

            _secondaryWasPressed = true;

            // ── Вращение призрака по оси Y ──
            _ghostYawOffset += rotationStep;

            // Нормализация (0-360)
            if (_ghostYawOffset >= 360f)
                _ghostYawOffset -= 360f;

            PlaySound(rotateSound);
        }

        // ══════════════════════════════════════════════════════════
        //  GHOST MANAGEMENT — через ObjectPoolManager
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Спавнит призрак постройки из пула.
        /// </summary>
        private void SpawnGhost()
        {
            if (activeBuildable == null || activeBuildable.ghostPrefab == null)
            {
                Debug.LogWarning("[PlayerBuilder] No buildable module assigned!");
                return;
            }

            // ── Начальная позиция призрака ──
            Vector3 spawnPos;
            if (buildAnchor != null)
            {
                spawnPos = buildAnchor.position;
            }
            else if (playerCamera != null)
            {
                spawnPos = playerCamera.transform.position
                         + playerCamera.transform.forward * buildDistance;
            }
            else
            {
                spawnPos = transform.position + Vector3.forward * buildDistance;
            }

            // ── Spawn через пул (fallback: Instantiate) ──
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool != null)
            {
                _currentGhostObj = pool.Spawn(
                    activeBuildable.ghostPrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                _currentGhostObj = Object.Instantiate(
                    activeBuildable.ghostPrefab, spawnPos, Quaternion.identity);
            }

            // ── Кэш компонента PlacementGhost ──
            if (_currentGhostObj != null)
            {
                _currentGhostObj.TryGetComponent(out _currentGhost);
            }
        }

        /// <summary>
        /// Деспавнит призрак (возврат в пул).
        /// Безопасно вызывать при отсутствии призрака.
        /// </summary>
        private void DespawnGhost()
        {
            if (_currentGhostObj == null) return;

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool != null)
            {
                pool.Despawn(_currentGhostObj);
            }
            else
            {
                Object.Destroy(_currentGhostObj);
            }

            _currentGhostObj = null;
            _currentGhost    = null;
        }

        // ══════════════════════════════════════════════════════════
        //  GHOST POSITIONING — Raycast + Smooth Follow
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Обновляет позицию призрака каждый кадр.
        ///
        /// Алгоритм:
        ///   1. Raycast из центра камеры на surfaceMask.
        ///   2. Если попал — целевая позиция = hit.point,
        ///      ориентация по hit.normal + ghostYawOffset.
        ///   3. Если промах — целевая позиция = buildAnchor или
        ///      camera.forward × buildDistance.
        ///   4. Плавная интерполяция (frame-rate independent).
        ///
        /// Zero GC: struct Ray/RaycastHit, кэшированный ViewportCenter.
        /// </summary>
        private void UpdateGhostPosition(float dt)
        {
            if (playerCamera == null) return;

            Ray ray = playerCamera.ViewportPointToRay(ViewportCenter);

            Vector3    targetPos;
            Quaternion targetRot;

            if (UnityEngine.Physics.Raycast(ray, out _hit, buildDistance, surfaceMask,
                                QueryTriggerInteraction.Ignore))
            {
                // ── Попали в поверхность: размещаем на ней ──
                targetPos = _hit.point;

                // Ориентация по нормали + пользовательский поворот
                Quaternion surfaceRot = Quaternion.FromToRotation(Vector3.up, _hit.normal);
                Quaternion yawRot     = Quaternion.Euler(0f, _ghostYawOffset, 0f);
                targetRot = surfaceRot * yawRot;
            }
            else
            {
                // ── Промах: призрак висит перед камерой ──
                if (buildAnchor != null)
                {
                    targetPos = buildAnchor.position;
                    targetRot = buildAnchor.rotation * Quaternion.Euler(0f, _ghostYawOffset, 0f);
                }
                else
                {
                    targetPos = ray.origin + ray.direction * buildDistance;
                    targetRot = Quaternion.Euler(0f, _ghostYawOffset, 0f);
                }
            }

            // ── Frame-rate independent exponential smoothing ──
            // 1 - exp(-speed × dt) даёт одинаковую скорость при любом fps
            Transform t = _currentGhostObj.transform;
            float lerpFactor = 1f - Mathf.Exp(-ghostFollowSpeed * dt);

            t.position = Vector3.Lerp(t.position, targetPos, lerpFactor);
            t.rotation = Quaternion.Slerp(t.rotation, targetRot, lerpFactor);
        }

        // ══════════════════════════════════════════════════════════
        //  MODULE PLACEMENT
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Попытка установить модуль:
        ///   1. Проверка CanBuild (коллизии)
        ///   2. Проверка ресурсов в инвентаре
        ///   3. Списание ресурсов
        ///   4. Деспавн призрака → спавн финального модуля
        ///   5. Пересоздание призрака для продолжения строительства
        /// </summary>
        private void TryPlaceModule()
        {
            // ── Guard: нет призрака или нельзя строить ──
            if (_currentGhost == null || !_currentGhost.CanBuild)
            {
                PlaySound(errorSound);
                return;
            }

            if (activeBuildable == null)
            {
                PlaySound(errorSound);
                return;
            }

            // ── Guard: недостаточно ресурсов ──
            if (!HasResources(activeBuildable))
            {
                PlaySound(errorSound);
                Debug.LogWarning("[PlayerBuilder] Недостаточно ресурсов!");
                return;
            }

            // ── Списание ресурсов ──
            ConsumeResources(activeBuildable);

            // ── Запоминаем трансформ призрака ──
            Vector3    placePos = _currentGhostObj.transform.position;
            Quaternion placeRot = _currentGhostObj.transform.rotation;

            // ── Спавн финального модуля ──
            if (activeBuildable.finalPrefab != null)
            {
                ObjectPoolManager pool = ObjectPoolManager.Instance;
                if (pool != null)
                {
                    pool.Spawn(activeBuildable.finalPrefab, placePos, placeRot);
                }
                else
                {
                    Object.Instantiate(activeBuildable.finalPrefab, placePos, placeRot);
                }
            }

            PlaySound(buildSound);

            // ── Пересоздаём призрак для продолжения строительства ──
            // (деспавн старого + спавн нового)
            DespawnGhost();
            SpawnGhost();
        }

        // ══════════════════════════════════════════════════════════
        //  RESOURCE CHECKING — сканирование InventoryGrid
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Проверяет наличие всех ресурсов для постройки.
        ///
        /// Сканирует InventoryGrid, считает ячейки с нужным ItemData.
        /// Для 1×1 ресурсов: одна ячейка = один ресурс.
        ///
        /// ОГРАНИЧЕНИЕ: корректно работает только с 1×1 ресурсами.
        /// Для multi-cell ресурсов потребуется anchor-tracking.
        ///
        /// Zero GC: for-циклы, ReferenceEquals, no LINQ.
        /// Вызывается ОДНОКРАТНО при нажатии ЛКМ (не per-frame).
        /// </summary>
        private bool HasResources(BuildableData data)
        {
            if (data.buildCost == null || data.buildCost.Count == 0) return true;
            if (inventory == null || inventory.Grid == null) return false;

            InventoryGrid grid = inventory.Grid;
            int cols = grid.Columns;
            int rows = grid.Rows;
            List<InventoryCost> costs = data.buildCost;

            for (int c = 0, cCount = costs.Count; c < cCount; c++)
            {
                InventoryCost cost = costs[c];
                if (cost.item == null) continue;

                int found    = 0;
                int required = cost.amount;

                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < cols; x++)
                    {
                        if (ReferenceEquals(grid.GetCell(x, y), cost.item))
                        {
                            found++;
                            if (found >= required)
                                goto nextCost; // ранний выход
                        }
                    }
                }

                // Недостаточно данного ресурса
                if (found < required)
                    return false;

                nextCost: ;
            }

            return true;
        }

        /// <summary>
        /// Списывает ресурсы из инвентаря.
        ///
        /// Сканирует сетку, находит ячейки с нужным ItemData,
        /// удаляет через PlayerInventory.RemoveItem (с пересчётом веса).
        ///
        /// ВАЖНО: вызывать ТОЛЬКО после успешного HasResources().
        ///
        /// ОГРАНИЧЕНИЕ: корректно работает только с 1×1 ресурсами.
        /// </summary>
        private void ConsumeResources(BuildableData data)
        {
            if (data.buildCost == null) return;
            if (inventory == null || inventory.Grid == null) return;

            InventoryGrid grid = inventory.Grid;
            int cols = grid.Columns;
            int rows = grid.Rows;
            List<InventoryCost> costs = data.buildCost;

            for (int c = 0, cCount = costs.Count; c < cCount; c++)
            {
                InventoryCost cost = costs[c];
                if (cost.item == null) continue;

                int remaining = cost.amount;

                for (int y = 0; y < rows && remaining > 0; y++)
                {
                    for (int x = 0; x < cols && remaining > 0; x++)
                    {
                        if (ReferenceEquals(grid.GetCell(x, y), cost.item))
                        {
                            // RemoveItem обновляет TotalWeight и survival
                            inventory.RemoveItem(cost.item, x, y);
                            remaining--;
                        }
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  AUDIO
        // ══════════════════════════════════════════════════════════

        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip);
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (buildDistance     < 1f) buildDistance     = 1f;
            if (ghostFollowSpeed < 1f) ghostFollowSpeed = 1f;
            if (rotationStep     < 1f) rotationStep     = 1f;
        }

        private void OnDrawGizmosSelected()
        {
            if (playerCamera == null) return;

            // Визуализация дальности строительства
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.15f);
            Gizmos.DrawWireSphere(playerCamera.transform.position, buildDistance);
        }
#endif
    }
}