// ============================================================================
// HECTON-8 — PlayerBuilder.cs
// Контроллер строительства модульной базы.
//
// v3.0 — SOCKET SNAP SYSTEM:
//   [ADD] Система магнитного прилипания к точкам стыковки (ModuleSocket).
//   [ADD] Поиск сокетов через Physics.OverlapSphereNonAlloc (zero GC).
//   [ADD] Гистерезис: snapRadius=2m, unsnapRadius=2.5m (без мерцания).
//   [ADD] Плавный snap/unsnap через экспоненциальное сглаживание.
//   [ADD] Занятые сокеты (IsOccupied) пропускаются при поиске.
//   [ADD] При размещении: ближайший сокет помечается как occupied.
//   [ADD] socketLayerMask для фильтрации (Layer "Sockets").
//
//   ПОВЕДЕНИЕ:
//     1. Raycast из камеры → hitPoint на поверхности.
//     2. OverlapSphereNonAlloc вокруг hitPoint на слое Sockets.
//     3. Если найден свободный сокет ≤ snapRadius → snap mode:
//        - Позиция призрака = socket.position
//        - Ротация призрака = socket.rotation × yawOffset
//     4. Если расстояние до снапнутого сокета > unsnapRadius → unsnap:
//        - Плавный переход обратно к raycast-позиции.
//     5. Гистерезис (snap=2m, unsnap=2.5m) предотвращает мерцание.
//
//   ZERO GC:
//     • OverlapSphereNonAlloc → предаллоцированный Collider[16].
//     • TryGetComponent<ModuleSocket> → zero GC.
//     • Все struct math, никаких List/LINQ/лямбд.
//
// ПРЕДЫДУЩИЕ ВЕРСИИ (сохранены):
//   v2.0: PlayerTool inheritance, ghost pool lifecycle.
//   v1.0: Basic placement.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Audio;
using Hecton8.Building;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Input;
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
        [Tooltip("Активный модуль для строительства.")]
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
        //  INSPECTOR — SOCKET SNAP (v3.0)
        // ══════════════════════════════════════════════════════════

        [Header("── Socket Snap (v3.0) ────────────────────────")]
        [Tooltip("Радиус обнаружения сокетов вокруг точки луча (метры).\n" +
                 "Когда hitPoint ≤ snapRadius от свободного сокета → snap.")]
        [SerializeField] private float snapRadius = 2f;

        [Tooltip("Радиус отрыва от сокета (метры).\n" +
                 "Должен быть > snapRadius для гистерезиса.\n" +
                 "Когда hitPoint > unsnapRadius от снапнутого сокета → unsnap.")]
        [SerializeField] private float unsnapRadius = 2.5f;

        [Tooltip("Слой сокетов для OverlapSphereNonAlloc.\n" +
                 "Создай Layer 'Sockets' в Project Settings → Tags & Layers.\n" +
                 "На каждом ModuleSocket: SphereCollider(trigger) + Layer=Sockets.")]
        [SerializeField] private LayerMask socketLayerMask;

        [Tooltip("Скорость прилипания к сокету (Lerp factor per second).\n" +
                 "Выше = резче snap. 20 = почти мгновенно.")]
        [SerializeField] private float snapSpeed = 20f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — AUDIO
        // ══════════════════════════════════════════════════════════

        [Header("── Audio ─────────────────────────────────────")]
        [SerializeField] private AudioClip buildSound;
        [SerializeField] private AudioClip errorSound;
        [SerializeField] private AudioClip rotateSound;

        [Tooltip("Звук прилипания к сокету (опционально).")]
        [SerializeField] private AudioClip snapSound;

        // ══════════════════════════════════════════════════════════
        //  CACHED STATE
        // ══════════════════════════════════════════════════════════

        private GameObject _currentGhostObj;
        private PlacementGhost _currentGhost;
        private RaycastHit _hit;
        private float _ghostYawOffset;

        private static readonly Vector3 ViewportCenter = new Vector3(0.5f, 0.5f, 0f);

        // ══════════════════════════════════════════════════════════
        //  SOCKET SNAP STATE (v3.0)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Pre-allocated buffer for OverlapSphereNonAlloc.
        /// 16 сокетов — покрывает даже хаб с 8 выходами.
        /// Zero GC: массив создаётся один раз.
        /// </summary>
        private readonly Collider[] _socketBuffer = new Collider[16];

        /// <summary>
        /// true когда призрак "прилип" к сокету.
        /// Используется для гистерезиса (snap/unsnap разные радиусы).
        /// </summary>
        private bool _isSnapped;

        /// <summary>
        /// Transform сокета, к которому прилип призрак.
        /// null когда не в snap-режиме.
        /// Кэшируется для: позиции, ротации, и отметки occupied при размещении.
        /// </summary>
        private Transform _snappedSocketTransform;

        /// <summary>
        /// Кэшированный ModuleSocket компонент снапнутого сокета.
        /// Используется для проверки IsOccupied и SetOccupied при размещении.
        /// </summary>
        private ModuleSocket _snappedSocket;

        /// <summary>
        /// Предыдущий snap-статус. Для edge detection (звук при snap/unsnap).
        /// </summary>
        private bool _wasSnapped;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        public BuildableData ActiveBuildable => activeBuildable;

        /// <summary>Сейчас призрак прилип к сокету.</summary>
        public bool IsSnapped => _isSnapped;

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
        //  IPoolable
        // ══════════════════════════════════════════════════════════

        public override void OnSpawn()
        {
            base.OnSpawn();
            ResetBuilderState();
        }

        public override void OnDespawn()
        {
            DespawnGhost();
            ResetBuilderState();
            base.OnDespawn();
        }

        // ══════════════════════════════════════════════════════════
        //  TOOL LIFECYCLE
        // ══════════════════════════════════════════════════════════

        public override void OnEquip()
        {
            base.OnEquip();
            ResetBuilderState();
            
            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnPrimaryAction   += HandlePrimaryAction;
                InputManager.Instance.OnSecondaryAction += HandleSecondaryAction;
            }

            SpawnGhost();
        }

        public override void OnUnequip()
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnPrimaryAction   -= HandlePrimaryAction;
                InputManager.Instance.OnSecondaryAction -= HandleSecondaryAction;
            }

            DespawnGhost();
            ResetBuilderState();
            base.OnUnequip();
        }

        public override void ToolTick(float deltaTime)
        {
            // Position update only — input handled via events
            if (_currentGhostObj != null)
                UpdateGhostPosition(deltaTime);
        }

        private void HandlePrimaryAction()
        {
            if (!IsEquipped) return;
            TryPlaceModule();
        }

        private void HandleSecondaryAction()
        {
            if (!IsEquipped) return;

            _ghostYawOffset += rotationStep;
            if (_ghostYawOffset >= 360f)
                _ghostYawOffset -= 360f;

            PlaySound(rotateSound);
        }

        public override void UsePrimary(float deltaTime)
        {
            // Logic moved to HandlePrimaryAction (one-shot event)
        }

        public override void UseSecondary(float deltaTime)
        {
            // Logic moved to HandleSecondaryAction (one-shot event)
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — STATE RESET
        // ══════════════════════════════════════════════════════════

        private void ResetBuilderState()
        {
            _ghostYawOffset      = 0f;
            _isSnapped           = false;
            _wasSnapped          = false;
            _snappedSocketTransform = null;
            _snappedSocket       = null;
        }

        // ══════════════════════════════════════════════════════════
        //  GHOST MANAGEMENT
        // ══════════════════════════════════════════════════════════

        private void SpawnGhost()
        {
            if (activeBuildable == null || activeBuildable.ghostPrefab == null)
            {
                Debug.LogWarning("[PlayerBuilder] No buildable module assigned!");
                return;
            }

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

            if (_currentGhostObj != null)
            {
                _currentGhostObj.TryGetComponent(out _currentGhost);
            }
        }

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
        //  GHOST POSITIONING (v3.0: Socket Snap System)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Обновляет позицию призрака каждый кадр.
        ///
        /// v3.0 АЛГОРИТМ:
        ///   1. Raycast из центра камеры → hitPoint.
        ///   2. OverlapSphereNonAlloc вокруг hitPoint на socketLayerMask.
        ///   3. Найти ближайший свободный ModuleSocket.
        ///   4. ГИСТЕРЕЗИС:
        ///      - Если НЕ снапнут и dist ≤ snapRadius → SNAP.
        ///      - Если снапнут и dist > unsnapRadius → UNSNAP.
        ///      - Между snapRadius и unsnapRadius → сохранять текущий статус.
        ///   5. Если SNAP → целевая позиция = socket.position,
        ///      целевая ротация = socket.rotation × yawOffset.
        ///   6. Если НЕ SNAP → обычное поведение (raycast surface).
        ///   7. Плавная интерполяция (exp smoothing).
        ///
        /// ZERO GC:
        ///   • OverlapSphereNonAlloc → _socketBuffer[16] (pre-allocated).
        ///   • TryGetComponent → zero GC.
        ///   • Struct Ray, RaycastHit, Vector3, Quaternion — stack.
        ///   • Никаких List, LINQ, лямбд, new.
        /// </summary>
        private void UpdateGhostPosition(float dt)
        {
            if (playerCamera == null) return;

            Ray ray = playerCamera.ViewportPointToRay(ViewportCenter);

            Vector3    targetPos;
            Quaternion targetRot;

            bool rayHit = UnityEngine.Physics.Raycast(
                ray, out _hit, buildDistance, surfaceMask,
                QueryTriggerInteraction.Ignore);

            // ── Точка луча (для поиска сокетов и fallback) ──
            Vector3 hitPoint = rayHit
                ? _hit.point
                : ray.origin + ray.direction * buildDistance;

            // ═══════════════════════════════════════════════════
            //  SOCKET SEARCH (v3.0)
            //
            //  OverlapSphereNonAlloc: ищет коллайдеры на слое Sockets
            //  в радиусе вокруг hitPoint. Pre-allocated buffer → zero GC.
            //
            //  Радиус поиска = unsnapRadius (больший из двух) для того,
            //  чтобы поймать сокет, от которого мы могли бы отрываться.
            //  Фактическая проверка snap/unsnap — по дистанции ниже.
            // ═══════════════════════════════════════════════════

            float searchRadius = unsnapRadius; // ищем в большем радиусе
            int socketCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                hitPoint,
                searchRadius,
                _socketBuffer,
                socketLayerMask,
                QueryTriggerInteraction.Collide // сокеты = trigger colliders
            );

            // ── Найти ближайший свободный сокет ──
            float   bestDist      = float.MaxValue;
            Transform bestTransform = null;
            ModuleSocket bestSocket = null;

            for (int i = 0; i < socketCount; i++)
            {
                Collider col = _socketBuffer[i];
                if (col == null) continue;

                // ── Получаем ModuleSocket (zero GC) ──
                if (!col.TryGetComponent(out ModuleSocket socket))
                    continue;

                // ── Пропускаем занятые ──
                if (socket.IsOccupied)
                    continue;

                // ── Дистанция от hitPoint до сокета ──
                float dist = Vector3.Distance(hitPoint, col.transform.position);

                if (dist < bestDist)
                {
                    bestDist      = dist;
                    bestTransform = col.transform;
                    bestSocket    = socket;
                }
            }

            // ── Очистка буфера (предотвращает удержание ссылок) ──
            for (int i = 0; i < socketCount; i++)
                _socketBuffer[i] = null;

            // ═══════════════════════════════════════════════════
            //  SNAP / UNSNAP DECISION (HYSTERESIS)
            //
            //  Два радиуса предотвращают мерцание:
            //    snapRadius (2m):   hitPoint ≤ 2m от сокета → SNAP
            //    unsnapRadius (2.5m): hitPoint > 2.5m от сокета → UNSNAP
            //    Между 2m и 2.5m: сохраняем текущий статус.
            //
            //  Без гистерезиса: на расстоянии ровно 2m призрак
            //  каждый кадр snap→unsnap→snap→unsnap (flicker).
            // ═══════════════════════════════════════════════════

            if (_isSnapped)
            {
                // ── Сейчас снапнут: проверяем условие ОТРЫВА ──
                if (bestTransform == null || bestDist > unsnapRadius)
                {
                    // Отрываемся: нет сокетов поблизости ИЛИ слишком далеко
                    _isSnapped = false;
                    _snappedSocketTransform = null;
                    _snappedSocket = null;
                }
                else
                {
                    // Обновляем: возможно, ближайший сокет сменился
                    // (игрок навёл на другой сокет того же модуля)
                    _snappedSocketTransform = bestTransform;
                    _snappedSocket = bestSocket;
                }
            }
            else
            {
                // ── Сейчас НЕ снапнут: проверяем условие ПРИЛИПАНИЯ ──
                if (bestTransform != null && bestDist <= snapRadius)
                {
                    _isSnapped = true;
                    _snappedSocketTransform = bestTransform;
                    _snappedSocket = bestSocket;
                }
            }

            // ── Звук snap/unsnap (edge detection) ──
            if (_isSnapped && !_wasSnapped)
            {
                PlaySound(snapSound);
            }
            _wasSnapped = _isSnapped;

            // ═══════════════════════════════════════════════════
            //  TARGET POSITION / ROTATION
            // ═══════════════════════════════════════════════════

            if (_isSnapped && _snappedSocketTransform != null)
            {
                // ── SNAP MODE: позиция и ротация от сокета ──
                targetPos = _snappedSocketTransform.position;

                // Socket.forward = направление стыковки.
                // YawOffset позволяет игроку вращать модуль
                // вокруг оси стыковки (если нужно).
                Quaternion socketRot = _snappedSocketTransform.rotation;
                Quaternion yawRot    = Quaternion.Euler(0f, _ghostYawOffset, 0f);
                targetRot = socketRot * yawRot;
            }
            else if (rayHit)
            {
                // ── SURFACE MODE: обычное поведение (raycast) ──
                targetPos = _hit.point;

                Quaternion surfaceRot = Quaternion.FromToRotation(Vector3.up, _hit.normal);
                Quaternion yawRot     = Quaternion.Euler(0f, _ghostYawOffset, 0f);
                targetRot = surfaceRot * yawRot;
            }
            else
            {
                // ── FALLBACK: призрак висит перед камерой ──
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

            // ═══════════════════════════════════════════════════
            //  SMOOTH INTERPOLATION
            //
            //  Используем разную скорость для snap и non-snap:
            //    Snap: snapSpeed (быстрый, ~20) — "щёлк" к позиции.
            //    Non-snap: ghostFollowSpeed (плавный, ~12) — обычное следование.
            //
            //  Exp smoothing: 1 - exp(-speed * dt) = frame-rate independent.
            // ═══════════════════════════════════════════════════

            Transform t = _currentGhostObj.transform;
            float speed = _isSnapped ? snapSpeed : ghostFollowSpeed;
            float lerpFactor = 1f - Mathf.Exp(-speed * dt);

            t.position = Vector3.Lerp(t.position, targetPos, lerpFactor);
            t.rotation = Quaternion.Slerp(t.rotation, targetRot, lerpFactor);
        }

        // ══════════════════════════════════════════════════════════
        //  MODULE PLACEMENT (v3.0: socket occupied marking)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Попытка установить модуль:
        ///   1. Проверка CanBuild (коллизии)
        ///   2. Проверка ресурсов в инвентаре
        ///   3. Списание ресурсов
        ///   4. Деспавн призрака → спавн финального модуля
        ///   5. v3.0: если снапнуты к сокету → пометить его как occupied
        ///   6. Пересоздание призрака для продолжения строительства
        /// </summary>
        private void TryPlaceModule()
        {
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

            if (!HasResources(activeBuildable))
            {
                PlaySound(errorSound);
                Debug.LogWarning("[PlayerBuilder] Недостаточно ресурсов!");
                return;
            }

            ConsumeResources(activeBuildable);

            Vector3    placePos = _currentGhostObj.transform.position;
            Quaternion placeRot = _currentGhostObj.transform.rotation;

            // ── v3.0: Пометить сокет как занятый ──
            if (_isSnapped && _snappedSocket != null)
            {
                _snappedSocket.SetOccupied(true);
            }

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

            // ── Сброс snap-состояния ──
            _isSnapped = false;
            _snappedSocketTransform = null;
            _snappedSocket = null;

            // ── Пересоздаём призрак ──
            DespawnGhost();
            SpawnGhost();
        }

        // ══════════════════════════════════════════════════════════
        //  RESOURCE CHECKING
        // ══════════════════════════════════════════════════════════

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
                                goto nextCost;
                        }
                    }
                }

                if (found < required)
                    return false;

                nextCost: ;
            }

            return true;
        }

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
            if (clip == null)
                return;

            if (SpatialAudioManager.Instance != null)
                SpatialAudioManager.Instance.PlayStatic2D(clip);
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
            if (snapRadius       < 0.1f) snapRadius     = 0.1f;
            if (unsnapRadius     <= snapRadius) unsnapRadius = snapRadius + 0.5f;
            if (snapSpeed        < 1f) snapSpeed        = 1f;
        }

        private void OnDrawGizmosSelected()
        {
            if (playerCamera == null) return;

            // Визуализация дальности строительства
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.15f);
            Gizmos.DrawWireSphere(playerCamera.transform.position, buildDistance);

            // Визуализация snap-зоны (только в Play Mode при наличии призрака)
            if (Application.isPlaying && _currentGhostObj != null)
            {
                if (_isSnapped && _snappedSocketTransform != null)
                {
                    // Snap active — зелёная линия к сокету
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(_currentGhostObj.transform.position,
                                    _snappedSocketTransform.position);
                    Gizmos.DrawWireSphere(_snappedSocketTransform.position, 0.2f);
                }
            }
        }
#endif
    }
}
