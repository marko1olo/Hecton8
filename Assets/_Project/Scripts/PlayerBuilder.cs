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
using Hecton8.Construction;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.Building
{
    [DisallowMultipleComponent]
    public sealed class PlayerBuilder : PlayerTool
    {
        public enum BuildReadiness
        {
            Offline = 0,
            NoSelection = 1,
            MissingCost = 2,
            PlacementBlocked = 3,
            Ready = 4,
            SnappedReady = 5
        }

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
        [SerializeField] private HUDNotification hudNotification;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — BUILDING
        // ══════════════════════════════════════════════════════════

        [Header("── Building ──────────────────────────────────")]
        [Tooltip("Активный модуль для строительства.")]
        [SerializeField] private BuildableData activeBuildable;
        [SerializeField] private bool autoResolveCatalogSelection = true;

        [Tooltip("Максимальная дальность размещения (метры)")]
        [SerializeField] private float buildDistance = 8f;

        [Tooltip("Скорость сглаживания движения призрака")]
        [SerializeField] private float ghostFollowSpeed = 12f;

        [Tooltip("Слой поверхности для размещения (Terrain, Default)")]
        [SerializeField] private LayerMask surfaceMask = ~0;

        [Header("── Rotation ──────────────────────────────────")]
        [Tooltip("Угол поворота призрака при нажатии ПКМ (градусы)")]
        [SerializeField] private float rotationStep = 90f;

        [Header("── Diagnostics ───────────────────────────────")]
        [Tooltip("Включить подробные BuilderDebug-логи для диагностики construction loop.")]
        [SerializeField] private bool builderDebugLogging = false;

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
        private ModuleCatalog _buildCatalog;
        private int _activeBuildableIndex = -1;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        public BuildableData ActiveBuildable => activeBuildable;
        public int ActiveBuildableIndex => _activeBuildableIndex;
        public int BuildableCount => _buildCatalog != null ? _buildCatalog.Count : 0;
        public bool HasResourcesForActiveBuildable => activeBuildable != null && HasResources(activeBuildable);
        public bool CanPlaceActiveBuildable => _currentGhost != null && _currentGhost.CanBuild;
        public bool HasPlacementPreview => _currentGhostObj != null;
        public BuildReadiness ActiveBuildReadiness => GetActiveBuildReadiness();

        /// <summary>Сейчас призрак прилип к сокету.</summary>
        public bool IsSnapped => _isSnapped;

        public BuildableData GetBuildableAt(int index)
        {
            if (_buildCatalog == null || index < 0 || index >= _buildCatalog.Count)
                return null;

            return _buildCatalog.GetAt(index);
        }

        public BuildableData GetRelativeBuildable(int direction)
        {
            if (_buildCatalog == null || _buildCatalog.Count <= 0)
                return null;

            int currentIndex = _activeBuildableIndex >= 0 ? _activeBuildableIndex : 0;
            int nextIndex = (currentIndex + direction + _buildCatalog.Count) % _buildCatalog.Count;
            return _buildCatalog.GetAt(nextIndex);
        }

        public bool DebugDeployActiveBuildable(Vector3 position, Quaternion rotation, bool consumeCost = true)
        {
            LogBuilderDebug($"DebugDeploy enter consumeCost={consumeCost} pos={position}");
            LogBuilderDebug("DebugDeploy -> ResolveRuntimeReferences");
            ResolveRuntimeReferences();
            LogBuilderDebug("DebugDeploy -> EnsureCatalogSelection");
            EnsureCatalogSelection();
            LogBuilderDebug($"DebugDeploy -> active={(activeBuildable != null ? activeBuildable.moduleName : "null")}");
            if (activeBuildable == null || activeBuildable.finalPrefab == null)
            {
                Debug.LogWarning("[BuilderDebug] DebugDeploy aborted: no active buildable/final prefab.");
                return false;
            }

            if (consumeCost && !HasResources(activeBuildable))
            {
                Debug.LogWarning($"[BuilderDebug] DebugDeploy aborted: missing resources for {activeBuildable.moduleName}.");
                return false;
            }

            if (consumeCost)
            {
                LogBuilderDebug($"DebugDeploy consuming cost for {activeBuildable.moduleName}.");
                ConsumeResources(activeBuildable);
            }

            GameObject spawned = SpawnPlacedModule(activeBuildable, position, rotation);
            LogBuilderDebug($"DebugDeploy spawnResult={(spawned != null ? spawned.name : "null")}");
            return spawned != null;
        }

        public bool DebugRecoverModule(BaseModule module)
        {
            if (module == null || !module.CanDeconstruct())
                return false;

            module.Deconstruct(inventory);
            NotifyModuleDeconstructed(module);
            return true;
        }

        public bool TryGetPlacementPreviewPose(out Vector3 position, out Quaternion rotation)
        {
            if (_currentGhostObj == null)
            {
                position = default;
                rotation = default;
                return false;
            }

            Transform ghostTransform = _currentGhostObj.transform;
            position = ghostTransform.position;
            rotation = ghostTransform.rotation;
            return true;
        }

        public bool TryDeployActiveBuildableFromPreview()
        {
            if (_currentGhost == null || !_currentGhost.CanBuild)
                return false;
            if (activeBuildable == null)
                return false;
            if (!HasResources(activeBuildable))
                return false;

            TryPlaceModuleInternal();
            return true;
        }

        public string GetActiveBuildStatusLabel()
        {
            switch (GetActiveBuildReadiness())
            {
                case BuildReadiness.Offline: return "OFFLINE";
                case BuildReadiness.NoSelection: return "NO MODULE";
                case BuildReadiness.MissingCost: return "MISSING COST";
                case BuildReadiness.PlacementBlocked: return "PLACEMENT BLOCKED";
                case BuildReadiness.SnappedReady: return "SNAPPED READY";
                default: return "READY";
            }
        }

        public string GetActiveBuildAdvice()
        {
            string purpose = DescribeBuildPurpose(activeBuildable);

            switch (GetActiveBuildReadiness())
            {
                case BuildReadiness.Offline:
                    return "Restore builder links before field deployment.";
                case BuildReadiness.NoSelection:
                    return "Pick a buildable from PDA Construction or cycle the catalog.";
                case BuildReadiness.MissingCost:
                    return $"{purpose} Recover materials first. Need {GetActiveCostDigest()}.";
                case BuildReadiness.PlacementBlocked:
                    return IsSnapped
                        ? $"{purpose} Socket alignment is good, but the final volume is obstructed."
                        : $"{purpose} Reposition, rotate, or snap to a valid socket.";
                case BuildReadiness.SnappedReady:
                    return $"{purpose} Placement is socket-locked and ready to deploy.";
                default:
                    return $"{purpose} Placement is clear. Deploy when ready.";
            }
        }

        public string GetActiveCostDigest()
        {
            if (activeBuildable == null || activeBuildable.buildCost == null || activeBuildable.buildCost.Count == 0)
                return "NO COST";

            System.Text.StringBuilder sb = new System.Text.StringBuilder(128);
            for (int i = 0; i < activeBuildable.buildCost.Count; i++)
            {
                InventoryCost cost = activeBuildable.buildCost[i];
                if (cost == null || cost.item == null || cost.amount <= 0)
                    continue;

                int available = inventory != null ? inventory.CountTotal(cost.item) : 0;
                if (sb.Length > 0)
                    sb.Append(" | ");

                string itemName = string.IsNullOrWhiteSpace(cost.item.itemName) ? cost.item.name : cost.item.itemName;
                sb.Append(itemName.ToUpperInvariant());
                sb.Append(' ');
                sb.Append(available);
                sb.Append('/');
                sb.Append(cost.amount);
            }

            return sb.Length > 0 ? sb.ToString() : "NO COST";
        }

        public string GetActiveBuildRoleLabel()
        {
            return DescribePowerRole(activeBuildable);
        }

        public string GetActiveBuildFamilyAndRoleLabel()
        {
            if (activeBuildable == null)
                return "NO FAMILY // NO ROLE";

            return $"{activeBuildable.FamilyShortCode} // {DescribePowerRole(activeBuildable)}";
        }

        public string GetActiveBuildOperationalSummary()
        {
            if (activeBuildable == null)
                return "NO MODULE";

            return $"{activeBuildable.moduleName.ToUpperInvariant()} // {activeBuildable.FamilyShortCode} // {DescribePowerRole(activeBuildable)}";
        }

        public void SetActiveBuildable(BuildableData data)
        {
            if (data == null) return;

            bool wasEquipped = IsEquipped;

            if (wasEquipped)
                DespawnGhost();

            activeBuildable = data;
            SyncActiveBuildableIndex();

            if (wasEquipped)
                SpawnGhost();
        }

        // ══════════════════════════════════════════════════════════
        //  IPoolable
        // ══════════════════════════════════════════════════════════

        public override void OnSpawn()
        {
            base.OnSpawn();
            ResolveRuntimeReferences();
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
            ResolveRuntimeReferences();
            EnsureCatalogSelection();
            ResetBuilderState();
            
            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnPrimaryAction   += HandlePrimaryAction;
                InputManager.Instance.OnSecondaryAction += HandleSecondaryAction;
                InputManager.Instance.OnInteract        += HandleInteract;
                InputManager.Instance.OnTabNext         += HandleTabNext;
                InputManager.Instance.OnTabPrevious     += HandleTabPrevious;
            }

            SpawnGhost();
            NotifyBuildableSelection();
        }

        public override void OnUnequip()
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnPrimaryAction   -= HandlePrimaryAction;
                InputManager.Instance.OnSecondaryAction -= HandleSecondaryAction;
                InputManager.Instance.OnInteract        -= HandleInteract;
                InputManager.Instance.OnTabNext         -= HandleTabNext;
                InputManager.Instance.OnTabPrevious     -= HandleTabPrevious;
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

        private void HandleInteract()
        {
            if (!IsEquipped) return;
            TryDeconstructTargetModule();
        }

        private void HandleTabNext()
        {
            if (!IsEquipped) return;
            CycleBuildable(+1);
        }

        private void HandleTabPrevious()
        {
            if (!IsEquipped) return;
            CycleBuildable(-1);
        }

        private void CycleBuildable(int direction)
        {
            ResolveRuntimeReferences();
            if (_buildCatalog == null || _buildCatalog.Count <= 0)
            {
                NotifyBuildBlocked("MODULE CATALOG OFFLINE");
                return;
            }

            int count = _buildCatalog.Count;
            int startIndex = _activeBuildableIndex;

            if (startIndex < 0 || startIndex >= count)
                startIndex = Mathf.Max(0, _buildCatalog.IndexOf(activeBuildable));

            if (startIndex < 0)
                startIndex = 0;

            for (int step = 1; step <= count; step++)
            {
                int candidateIndex = WrapIndex(startIndex + (step * direction), count);
                BuildableData candidate = _buildCatalog.GetAt(candidateIndex);
                if (candidate == null) continue;

                SetActiveBuildable(candidate);
                _activeBuildableIndex = candidateIndex;
                PlaySound(rotateSound);
                NotifyBuildableSelection();
                return;
            }

            NotifyBuildBlocked("NO VALID MODULES");
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
            TryPlaceModuleInternal();
        }

        private void TryPlaceModuleInternal()
        {
            if (_currentGhost == null || !_currentGhost.CanBuild)
            {
                NotifyBuildBlocked("PLACEMENT INVALID");
                PlaySound(errorSound);
                return;
            }

            if (activeBuildable == null)
            {
                NotifyBuildBlocked("NO MODULE SELECTED");
                PlaySound(errorSound);
                return;
            }

            if (!HasResources(activeBuildable))
            {
                NotifyMissingResources(activeBuildable);
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
            GameObject placedModule = SpawnPlacedModule(activeBuildable, placePos, placeRot);

            if (placedModule != null)
            {
                ConstructionManager manager = ConstructionManager.Instance;
                if (manager != null)
                    manager.RegisterModule(placedModule, activeBuildable);
            }

            PlaySound(buildSound);
            NotifyBuildPlaced(activeBuildable);

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
            List<InventoryCost> costs = data.buildCost;

            for (int c = 0, cCount = costs.Count; c < cCount; c++)
            {
                InventoryCost cost = costs[c];
                if (cost.item == null) continue;
                if (inventory.CountTotal(cost.item) < cost.amount)
                    return false;
            }

            return true;
        }

        private void ResolveRuntimeReferences()
        {
            LogBuilderDebug("ResolveRuntimeReferences begin");
            if (inventory == null)
                inventory = GetComponent<PlayerInventory>() ?? GetComponentInParent<PlayerInventory>();
            LogBuilderDebug($"ResolveRuntimeReferences inventory={(inventory != null ? "Y" : "N")}");

            if (playerCamera == null)
                playerCamera = GetComponentInChildren<Camera>(true) ?? Camera.main;
            LogBuilderDebug($"ResolveRuntimeReferences camera={(playerCamera != null ? playerCamera.name : "null")}");

            if (buildAnchor == null)
            {
                Transform[] children = GetComponentsInChildren<Transform>(true);
                LogBuilderDebug($"ResolveRuntimeReferences childCount={children.Length}");
                for (int i = 0; i < children.Length; i++)
                {
                    Transform child = children[i];
                    if (child != null && child.name == "HandAnchor")
                    {
                        buildAnchor = child;
                        break;
                    }
                }
            }
            LogBuilderDebug($"ResolveRuntimeReferences buildAnchor={(buildAnchor != null ? buildAnchor.name : "null")}");

            if (hudNotification == null)
                hudNotification = FindFirstObjectByType<HUDNotification>();
            LogBuilderDebug($"ResolveRuntimeReferences hud={(hudNotification != null ? "Y" : "N")}");

            if (_buildCatalog == null)
            {
                ConstructionManager manager = ConstructionManager.Instance ?? FindFirstObjectByType<ConstructionManager>();
                if (manager != null)
                    _buildCatalog = manager.Catalog;
            }
            LogBuilderDebug($"ResolveRuntimeReferences catalogCount={(_buildCatalog != null ? _buildCatalog.Count : -1)}");

            if (activeBuildable == null)
                EnsureCatalogSelection();

            SyncActiveBuildableIndex();
            LogBuilderDebug($"ResolveRuntimeReferences end activeIndex={_activeBuildableIndex}");
        }

        private void EnsureCatalogSelection()
        {
            LogBuilderDebug("EnsureCatalogSelection begin");
            if (!autoResolveCatalogSelection) return;
            if (activeBuildable != null) return;
            if (_buildCatalog == null || _buildCatalog.Count <= 0) return;

            for (int i = 0; i < _buildCatalog.Count; i++)
            {
                BuildableData candidate = _buildCatalog.GetAt(i);
                if (candidate == null) continue;

                activeBuildable = candidate;
                _activeBuildableIndex = i;
                LogBuilderDebug($"EnsureCatalogSelection picked={candidate.moduleName} index={i}");
                return;
            }

            LogBuilderDebug("EnsureCatalogSelection end without candidate");
        }

        private void SyncActiveBuildableIndex()
        {
            if (_buildCatalog == null || activeBuildable == null)
            {
                _activeBuildableIndex = -1;
                return;
            }

            _activeBuildableIndex = _buildCatalog.IndexOf(activeBuildable);
        }

        private void NotifyBuildableSelection()
        {
            if (activeBuildable == null)
            {
                NotifyBuildBlocked("NO MODULE SELECTED");
                return;
            }

            string message =
                $"BUILDER // {GetActiveBuildOperationalSummary()} // {GetActiveBuildStatusLabel()} // {GetActiveCostDigest()}";

            if (hudNotification != null)
                hudNotification.ShowInfo(message);
            else
                Debug.Log(message);

            FieldOperationLogSystem.RecordOperation(
                "BUILDER",
                $"BUILDABLE ARMED - {activeBuildable.moduleName.ToUpperInvariant()}",
                $"{GetActiveBuildFamilyAndRoleLabel()} // {GetActiveBuildAdvice()}",
                ActiveBuildReadiness == BuildReadiness.MissingCost ? "WARN" : "INFO");
        }

        private void NotifyMissingResources(BuildableData data)
        {
            if (data == null)
            {
                NotifyBuildBlocked("MISSING COST");
                return;
            }

            string message = $"BUILDER // {data.moduleName.ToUpperInvariant()} // {data.FamilyShortCode} // MISSING COST // {GetCostDigest(data)}";
            if (hudNotification != null)
                hudNotification.ShowWarning(message);
            else
                Debug.LogWarning(message);

            FieldOperationLogSystem.RecordOperation(
                "BUILDER",
                $"MISSING MATERIALS - {data.moduleName.ToUpperInvariant()}",
                $"{DescribeBuildPowerRole(data)} // Required: {GetCostDigest(data)}",
                "WARN");
        }

        private void NotifyBuildBlocked(string reason)
        {
            if (string.IsNullOrEmpty(reason))
                reason = "BUILD BLOCKED";

            string message = activeBuildable != null
                ? $"BUILDER // {activeBuildable.moduleName.ToUpperInvariant()} // {activeBuildable.FamilyShortCode} // {reason}"
                : $"BUILDER // {reason}";
            if (hudNotification != null)
                hudNotification.ShowWarning(message);
            else
                Debug.LogWarning(message);

            FieldOperationLogSystem.RecordOperation(
                "BUILDER",
                reason,
                GetActiveBuildAdvice(),
                "WARN");
        }

        private void NotifyBuildPlaced(BuildableData data)
        {
            if (data == null) return;

            string message = $"BUILDER // {data.moduleName.ToUpperInvariant()} DEPLOYED";
            if (hudNotification != null)
                hudNotification.ShowInfo(message);
            else
                Debug.Log(message);

            FieldOperationLogSystem.RecordOperation(
                "BUILDER",
                $"MODULE DEPLOYED - {data.moduleName.ToUpperInvariant()}",
                $"{DescribeBuildPowerRole(data)} // {DescribeBuildPurpose(data)} {GetCostDigest(data)} consumed.",
                "INFO");
        }

        private GameObject SpawnPlacedModule(BuildableData data, Vector3 placePos, Quaternion placeRot)
        {
            if (data == null || data.finalPrefab == null)
                return null;

            LogBuilderDebug($"SpawnPlacedModule begin module={data.moduleName} prefab={data.finalPrefab.name}");
            GameObject placedModule;
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool != null)
            {
                LogBuilderDebug("SpawnPlacedModule using pool.");
                placedModule = pool.Spawn(data.finalPrefab, placePos, placeRot);
            }
            else
            {
                LogBuilderDebug("SpawnPlacedModule using instantiate.");
                placedModule = Object.Instantiate(data.finalPrefab, placePos, placeRot);
            }

            if (placedModule != null)
            {
                ConstructionManager manager = ConstructionManager.Instance;
                if (manager != null)
                {
                    manager.RegisterModule(placedModule, data);
                    LogBuilderDebug($"SpawnPlacedModule registered moduleCount={manager.ModuleCount}");
                }
            }

            LogBuilderDebug($"SpawnPlacedModule end result={(placedModule != null ? placedModule.name : "null")}");
            return placedModule;
        }

        private void LogBuilderDebug(string message)
        {
            if (!builderDebugLogging)
                return;

            Debug.Log("[BuilderDebug] " + message);
        }

        private void NotifyModuleDeconstructed(BaseModule module)
        {
            string moduleName = module != null ? module.gameObject.name.ToUpperInvariant() : "MODULE";
            string message = $"BUILDER // {moduleName} RECOVERED";
            if (hudNotification != null)
                hudNotification.ShowInfo(message);
            else
                Debug.Log(message);

            FieldOperationLogSystem.RecordOperation(
                "BUILDER",
                $"MODULE RECOVERED - {moduleName}",
                "Construction module was deconstructed and resources were routed back to the expedition economy.",
                "INFO");
        }

        private BuildReadiness GetActiveBuildReadiness()
        {
            if (activeBuildable == null)
                return BuildReadiness.NoSelection;

            if (inventory == null || playerCamera == null)
                return BuildReadiness.Offline;

            if (!HasResources(activeBuildable))
                return BuildReadiness.MissingCost;

            if (_currentGhost == null)
                return BuildReadiness.Ready;

            if (!_currentGhost.CanBuild)
                return BuildReadiness.PlacementBlocked;

            return _isSnapped ? BuildReadiness.SnappedReady : BuildReadiness.Ready;
        }

        private string GetCostDigest(BuildableData data)
        {
            if (data == null || data.buildCost == null || data.buildCost.Count == 0)
                return "NO COST";

            System.Text.StringBuilder sb = new System.Text.StringBuilder(128);
            for (int i = 0; i < data.buildCost.Count; i++)
            {
                InventoryCost cost = data.buildCost[i];
                if (cost == null || cost.item == null || cost.amount <= 0)
                    continue;

                int available = inventory != null ? inventory.CountTotal(cost.item) : 0;
                if (sb.Length > 0)
                    sb.Append(" | ");

                string itemName = string.IsNullOrWhiteSpace(cost.item.itemName) ? cost.item.name : cost.item.itemName;
                sb.Append(itemName.ToUpperInvariant());
                sb.Append(' ');
                sb.Append(available);
                sb.Append('/');
                sb.Append(cost.amount);
            }

            return sb.Length > 0 ? sb.ToString() : "NO COST";
        }

        private static string DescribePowerRole(BuildableData data)
        {
            return DescribeBuildPowerRole(data);
        }

        private static string DescribeBuildPowerRole(BuildableData data)
        {
            if (data == null)
                return "NO ROLE";

            if (data.IsGenerator)
                return "POWER SOURCE";

            if (data.IsConsumer)
            {
                switch (data.family)
                {
                    case BuildableFamily.Habitat: return "LIFE SUPPORT LOAD";
                    case BuildableFamily.Utility: return "UTILITY LOAD";
                    case BuildableFamily.Fabrication: return "FABRICATION LOAD";
                    case BuildableFamily.Logistics: return "LOGISTICS LOAD";
                    case BuildableFamily.Defense: return "DEFENSE LOAD";
                    default: return "ACTIVE LOAD";
                }
            }

            switch (data.family)
            {
                case BuildableFamily.Structure: return "HULL SPINE";
                case BuildableFamily.Habitat: return "CREW VOLUME";
                case BuildableFamily.Utility: return "UTILITY LINK";
                case BuildableFamily.Fabrication: return "FAB PLATFORM";
                case BuildableFamily.Logistics: return "SUPPLY NODE";
                case BuildableFamily.Defense: return "PERIMETER NODE";
                default: return "PASSIVE FRAME";
            }
        }

        private static string DescribeBuildPurpose(BuildableData data)
        {
            if (data == null)
                return string.Empty;

            switch (data.family)
            {
                case BuildableFamily.Structure:
                    return "Use this to extend the hull and secure new traversal space.";
                case BuildableFamily.Habitat:
                    return "Use this to expand safe living volume for the expedition.";
                case BuildableFamily.Utility:
                    return data.IsGenerator
                        ? "Use this to stabilize nearby systems with fresh power."
                        : "Use this to support field systems and service links.";
                case BuildableFamily.Fabrication:
                    return "Use this to add production capacity near powered habitat space.";
                case BuildableFamily.Logistics:
                    return "Use this to improve routing, storage, and traffic flow.";
                case BuildableFamily.Defense:
                    return "Use this to harden exposed approaches and perimeter lanes.";
                default:
                    return "Use this module to extend the expedition footprint.";
            }
        }

        private static int WrapIndex(int value, int count)
        {
            if (count <= 0) return -1;
            int wrapped = value % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }

        private void TryDeconstructTargetModule()
        {
            BaseModule module = GetTargetedModule();
            if (module == null)
            {
                NotifyBuildBlocked("NO MODULE TARGET");
                return;
            }

            if (!module.CanDeconstruct())
            {
                NotifyBuildBlocked("MODULE LOCKED");
                return;
            }

            module.Deconstruct(inventory);
            NotifyModuleDeconstructed(module);
        }

        private BaseModule GetTargetedModule()
        {
            if (playerCamera == null)
                return null;

            Ray ray = playerCamera.ViewportPointToRay(ViewportCenter);
            if (!UnityEngine.Physics.Raycast(ray, out RaycastHit hit, buildDistance, ~0, QueryTriggerInteraction.Ignore))
                return null;

            return hit.collider != null ? hit.collider.GetComponentInParent<BaseModule>() : null;
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
                        if (!ReferenceEquals(grid.GetCell(x, y), cost.item))
                            continue;

                        bool isAnchor =
                            (x == 0 || !ReferenceEquals(grid.GetCell(x - 1, y), cost.item)) &&
                            (y == 0 || !ReferenceEquals(grid.GetCell(x, y - 1), cost.item));

                        if (!isAnchor)
                            continue;

                        int stackCount = Mathf.Max(1, inventory.GetStackCount(x, y));
                        while (stackCount > 0 && remaining > 0)
                        {
                            inventory.RemoveOneItem(x, y);
                            remaining--;
                            stackCount--;
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
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;
#endif
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
