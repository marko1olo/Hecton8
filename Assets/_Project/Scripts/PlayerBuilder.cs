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
using Hecton8.Modding;
using Hecton8.Construction;
using Hecton8.Physics;
using Hecton8.UI;
using Unity.Mathematics;
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
        [SerializeField] private LayerMask surfaceMask = HectonLayerMasks.ConstructionSurfaceLayerMask;
        [Tooltip("Rigid world-space grid size used for free placement positions.")]
        [SerializeField] private float constructionGridSize = 2.5f;
        [Tooltip("Total structural integrity budget available to the current habitat graph.")]
        [SerializeField] private float structuralIntegrityBudget = 240f;
        [Tooltip("Integrity penalty applied for every BFS depth step away from the support root.")]
        [SerializeField] private float structuralDepthPenalty = 0.75f;

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
        [SerializeField] private LayerMask socketLayerMask = HectonLayerMasks.SocketsLayerMask;

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
        private bool _currentGhostUsesRuntimeProxy;
        private RaycastHit _hit;
        private readonly RaycastHit[] _buildHits = new RaycastHit[1]; // COLD ALLOC: single surface probe for build targeting.
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
        // COLD ALLOC: List<MonoBehaviour>[2] — authored placement-rule scan buffer for the active buildable prefab — owner: PlayerBuilder
        private readonly List<MonoBehaviour> _placementRuleBuffer = new List<MonoBehaviour>(2);
        private readonly List<ModuleSocket> _ghostSocketBuffer = new List<ModuleSocket>(8);
        private IBuildPlacementRule _activePlacementRule;
        private bool _semanticPlacementValid = true;
        private string _semanticPlacementBlockReason = string.Empty;
        private HabitatConstructionManager _habitatConstructionManager;
        private ModuleSocket _snappedGhostSocket;
        private bool _integrityPlacementValid = true;
        private bool _integrityValidationDirty;
        private string _integrityPlacementBlockReason = string.Empty;
        private ValidationSnapshot _scheduledValidationSnapshot;
        private ValidationSnapshot _completedValidationSnapshot;
        private bool _hasScheduledValidationSnapshot;
        private bool _hasCompletedValidationSnapshot;
        private IInputService _subscribedInputService;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        public BuildableData ActiveBuildable => activeBuildable;
        public int ActiveBuildableIndex => _activeBuildableIndex;
        public int BuildableCount => _buildCatalog != null ? _buildCatalog.Count : 0;
        public bool HasResourcesForActiveBuildable => activeBuildable != null && HasResources(activeBuildable);
        public bool CanPlaceActiveBuildable => _currentGhost != null && _currentGhost.CanBuild && _semanticPlacementValid && _integrityPlacementValid;
        public bool HasPlacementPreview => _currentGhostObj != null;
        public BuildReadiness ActiveBuildReadiness => GetActiveBuildReadiness();

        /// <summary>Сейчас призрак прилип к сокету.</summary>
        public bool IsSnapped => _isSnapped;

        private struct ValidationSnapshot
        {
            public BuildableData Buildable;
            public ModuleSocket TargetSocket;
            public int ModuleCount;
            public Vector3 Position;
            public Quaternion Rotation;
        }

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

            if (!TryGetObjectPool(out ObjectPoolManager pool))
            {
                Debug.LogWarning("[BuilderDebug] DebugDeploy aborted: ObjectPoolManager unavailable.");
                return false;
            }

            GameObject spawned = SpawnPlacedModule(activeBuildable, position, rotation, pool);
            if (spawned == null)
            {
                Debug.LogWarning($"[BuilderDebug] DebugDeploy aborted: failed to spawn {activeBuildable.moduleName}.");
                return false;
            }

            ApplyConstructedModuleSnap(spawned, position, rotation);

            if (consumeCost)
            {
                LogBuilderDebug($"DebugDeploy consuming cost for {activeBuildable.moduleName}.");
                ConsumeResources(activeBuildable);
            }

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
            if (!UpdatePlacementValidityState())
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
                    string blockReason = ResolvePlacementBlockReason();
                    if (!string.IsNullOrEmpty(blockReason))
                        return $"{purpose} {blockReason}.";
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

                int available = inventory != null && cost.item != null
                    ? inventory.CountTotal(Hecton.Localization.LocHash.Compute(cost.item.PersistentId))
                    : 0;
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
            CacheActivePlacementRule();
            SyncActiveBuildableIndex();

            if (wasEquipped)
                SpawnGhost();
        }

        // ══════════════════════════════════════════════════════════
        //  IPoolable
        // ══════════════════════════════════════════════════════════

        public override void OnSpawn()
        {
            if (_habitatConstructionManager == null)
                _habitatConstructionManager = new HabitatConstructionManager();

            base.OnSpawn();
            ResolveRuntimeReferences();
            ResetBuilderState();
        }

        public override void OnDespawn()
        {
            UnsubscribeFromInputService();
            DespawnGhost();
            ResetBuilderState();
            base.OnDespawn();
        }

        private void OnDestroy()
        {
            if (_habitatConstructionManager == null)
                return;

            _habitatConstructionManager.Dispose();
            _habitatConstructionManager = null;
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
            RefreshInputSubscriptions();

            SpawnGhost();
            NotifyBuildableSelection();
        }

        public override void OnUnequip()
        {
            UnsubscribeFromInputService();

            DespawnGhost();
            ResetBuilderState();
            base.OnUnequip();
        }

        public override void ToolTick(float deltaTime)
        {
            // Position update only — input handled via events
            if (_currentGhostObj != null)
            {
                UpdateGhostPosition(deltaTime);
                UpdatePlacementValidationState();
            }
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

            _integrityValidationDirty = true;
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
            _snappedGhostSocket  = null;
            _integrityPlacementValid = true;
            _integrityValidationDirty = false;
            _integrityPlacementBlockReason = string.Empty;
            _hasScheduledValidationSnapshot = false;
            _hasCompletedValidationSnapshot = false;
            _ghostSocketBuffer.Clear();
            _currentGhost?.SetExternalValidity(true);
            _habitatConstructionManager?.ResetValidation();
        }

        // ══════════════════════════════════════════════════════════
        //  GHOST MANAGEMENT
        // ══════════════════════════════════════════════════════════

        private void SpawnGhost()
        {
            if (activeBuildable == null)
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

            if (activeBuildable.ghostPrefab == null)
            {
                if (!ConstructionRuntimeProxyFactory.TryCreateGhostProxy(
                        activeBuildable,
                        spawnPos,
                        Quaternion.identity,
                        ResolveSurfaceMask(),
                        out _currentGhostObj))
                {
                    NotifyBuildBlocked("GHOST PROXY MISSING");
                    return;
                }

                _currentGhostUsesRuntimeProxy = true;
            }
            else
            {
                if (!TryGetObjectPool(out ObjectPoolManager pool))
                {
                    NotifyBuildBlocked("OBJECT POOL OFFLINE");
                    return;
                }

                _currentGhostObj = pool.Spawn(
                    activeBuildable.ghostPrefab, spawnPos, Quaternion.identity);
                _currentGhostUsesRuntimeProxy = false;
            }

            if (_currentGhostObj != null)
            {
                _currentGhostObj.TryGetComponent(out _currentGhost);
                CacheGhostSockets();
                _currentGhost?.SetExternalValidity(true);
            }

            _integrityPlacementValid = true;
            _integrityPlacementBlockReason = string.Empty;
            _integrityValidationDirty = true;
            UpdatePlacementValidationState();
        }

        private void DespawnGhost()
        {
            if (_currentGhostObj == null) return;

            if (_currentGhostUsesRuntimeProxy)
            {
                Object.Destroy(_currentGhostObj);
            }
            else
            {
                ObjectPoolManager pool = GlobalRegistry.ObjectPool;
                if (pool != null)
                    pool.Despawn(_currentGhostObj);
                else
                    Object.Destroy(_currentGhostObj);
            }

            _currentGhostObj = null;
            _currentGhost    = null;
            _currentGhostUsesRuntimeProxy = false;
            _ghostSocketBuffer.Clear();
            _semanticPlacementValid = true;
            _semanticPlacementBlockReason = string.Empty;
            _integrityPlacementValid = true;
            _integrityPlacementBlockReason = string.Empty;
            _integrityValidationDirty = false;
            _hasScheduledValidationSnapshot = false;
            _hasCompletedValidationSnapshot = false;
            _habitatConstructionManager?.ResetValidation();
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
            if (playerCamera == null || _currentGhostObj == null || _habitatConstructionManager == null)
                return;

            Ray ray = playerCamera.ViewportPointToRay(ViewportCenter);

            Vector3 targetPos;
            Quaternion targetRot;

            bool rayHit = TryGetBuildHit(ray, ResolveSurfaceMask(), out _hit);

            // ── Точка луча (для поиска сокетов и fallback) ──
            Vector3 rawTargetPoint = rayHit
                ? _hit.point
                : ray.origin + ray.direction * buildDistance;

            float3 snappedFreePosition = _habitatConstructionManager.SnapWorldPosition(rawTargetPoint, constructionGridSize);
            Vector3 freePlacementPosition = new Vector3(snappedFreePosition.x, snappedFreePosition.y, snappedFreePosition.z);

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
            int resolvedSocketMask = ResolveSocketMask();
            int socketCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                rawTargetPoint,
                searchRadius,
                _socketBuffer,
                resolvedSocketMask,
                QueryTriggerInteraction.Collide // сокеты = trigger colliders
            );

            // ── Найти ближайший свободный сокет ──
            float   bestDist      = float.MaxValue;
            Transform bestTransform = null;
            ModuleSocket bestSocket = null;
            Vector3 bestAlignedPosition = default;
            Quaternion bestAlignedRotation = default;
            ModuleSocket bestGhostSocket = null;

            for (int i = 0; i < socketCount; i++)
            {
                Collider socketCollider = _socketBuffer[i];
                if (socketCollider == null) continue;

                // ── Получаем ModuleSocket (zero GC) ──
                if (!socketCollider.TryGetComponent(out ModuleSocket socket))
                    continue;

                // ── Пропускаем занятые ──
                if (socket.IsOccupied)
                    continue;

                // ── Дистанция от hitPoint до сокета ──
                if (!_habitatConstructionManager.TryResolveSocketAlignment(
                        _currentGhostObj.transform,
                        _ghostSocketBuffer,
                        socket,
                        _ghostYawOffset,
                        out Vector3 alignedPosition,
                        out Quaternion alignedRotation,
                        out ModuleSocket alignedGhostSocket))
                {
                    continue;
                }

                float dist = (socketCollider.transform.position - rawTargetPoint).sqrMagnitude;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestTransform = socketCollider.transform;
                    bestSocket = socket;
                    bestAlignedPosition = alignedPosition;
                    bestAlignedRotation = alignedRotation;
                    bestGhostSocket = alignedGhostSocket;
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

            bool previousSnapState = _isSnapped;
            ModuleSocket previousSocket = _snappedSocket;
            ModuleSocket previousGhostSocket = _snappedGhostSocket;

            if (_isSnapped)
            {
                // ── Сейчас снапнут: проверяем условие ОТРЫВА ──
                if (bestTransform == null || bestDist > (unsnapRadius * unsnapRadius))
                {
                    // Отрываемся: нет сокетов поблизости ИЛИ слишком далеко
                    _isSnapped = false;
                    _snappedSocketTransform = null;
                    _snappedSocket = null;
                    _snappedGhostSocket = null;
                }
                else
                {
                    // Обновляем: возможно, ближайший сокет сменился
                    // (игрок навёл на другой сокет того же модуля)
                    _snappedSocketTransform = bestTransform;
                    _snappedSocket = bestSocket;
                    _snappedGhostSocket = bestGhostSocket;
                }
            }
            else
            {
                // ── Сейчас НЕ снапнут: проверяем условие ПРИЛИПАНИЯ ──
                if (bestTransform != null && bestDist <= (snapRadius * snapRadius))
                {
                    _isSnapped = true;
                    _snappedSocketTransform = bestTransform;
                    _snappedSocket = bestSocket;
                    _snappedGhostSocket = bestGhostSocket;
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

            if (_isSnapped && _snappedSocket != null)
            {
                // ── SNAP MODE: позиция и ротация от сокета ──
                targetPos = bestAlignedPosition;

                // Socket.forward = направление стыковки.
                // YawOffset позволяет игроку вращать модуль
                // вокруг оси стыковки (если нужно).
                targetRot = bestAlignedRotation;
            }
            else if (rayHit)
            {
                // ── SURFACE MODE: обычное поведение (raycast) ──
                targetPos = freePlacementPosition;

                Quaternion surfaceRot = Quaternion.FromToRotation(Vector3.up, _hit.normal);
                Quaternion yawRot     = Quaternion.Euler(0f, _ghostYawOffset, 0f);
                targetRot = surfaceRot * yawRot;
            }
            else
            {
                // ── FALLBACK: призрак висит перед камерой ──
                if (buildAnchor != null)
                {
                    float3 snappedAnchorPosition = _habitatConstructionManager.SnapWorldPosition(buildAnchor.position, constructionGridSize);
                    targetPos = new Vector3(snappedAnchorPosition.x, snappedAnchorPosition.y, snappedAnchorPosition.z);
                    targetRot = buildAnchor.rotation * Quaternion.Euler(0f, _ghostYawOffset, 0f);
                }
                else
                {
                    targetPos = freePlacementPosition;
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
            Vector3 previousPosition = t.position;
            Quaternion previousRotation = t.rotation;
            float speed = _isSnapped ? snapSpeed : ghostFollowSpeed;
            float lerpFactor = 1f - Mathf.Exp(-speed * dt);

            t.position = Vector3.Lerp(previousPosition, targetPos, lerpFactor);
            t.rotation = Quaternion.Slerp(previousRotation, targetRot, lerpFactor);

            if (previousSnapState != _isSnapped ||
                !ReferenceEquals(previousSocket, _snappedSocket) ||
                !ReferenceEquals(previousGhostSocket, _snappedGhostSocket) ||
                (t.position - previousPosition).sqrMagnitude > 0.0001f ||
                Quaternion.Dot(previousRotation, t.rotation) < 0.9999f)
            {
                _integrityValidationDirty = true;
            }
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
            if (activeBuildable == null)
            {
                NotifyBuildBlocked("NO MODULE SELECTED");
                PlaySound(errorSound);
                return;
            }

            if (_currentGhostObj == null || _currentGhost == null)
            {
                NotifyBuildBlocked("PLACEMENT INVALID");
                PlaySound(errorSound);
                return;
            }

            if (!UpdatePlacementValidityState() || !_currentGhost.CanBuild)
            {
                NotifyBuildBlocked(ResolvePlacementBlockReason());
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

            Vector3 placePos = _currentGhostObj.transform.position;
            Quaternion placeRot = _currentGhostObj.transform.rotation;
            TryResolveExactSnappedPlacementPose(ref placePos, ref placeRot);

            if (!TryGetObjectPool(out ObjectPoolManager pool))
            {
                NotifyBuildBlocked("OBJECT POOL OFFLINE");
                PlaySound(errorSound);
                return;
            }

            // ── v3.0: Пометить сокет как занятый ──

            // ── Спавн финального модуля ──
            GameObject placedModule = SpawnPlacedModule(activeBuildable, placePos, placeRot, pool);

            if (placedModule == null)
            {
                NotifyBuildBlocked("MODULE SPAWN FAILED");
                PlaySound(errorSound);
                return;
            }

            ApplyConstructedModuleSnap(placedModule, placePos, placeRot);

            if (!ConsumeResources(activeBuildable))
            {
                ConstructionManager constructionManager = ResolveConstructionManager();
                if (constructionManager != null)
                    constructionManager.DestroyModule(placedModule);
                else
                    pool.Despawn(placedModule);

                NotifyBuildBlocked("RESOURCE TRANSACTION FAILED");
                PlaySound(errorSound);
                return;
            }

            if (_isSnapped && _snappedSocket != null)
            {
                _snappedSocket.SetOccupied(true);
            }

            bool hasModulePose = placedModule != null;
            ulong moduleEntityId = hasModulePose ? EntityId.ToULong(placedModule.GetEntityId()) : 0ul;
            Transform moduleTransform = hasModulePose ? placedModule.transform : null;
            Vector3 modulePosition = moduleTransform != null ? moduleTransform.position : Vector3.zero;
            Quaternion moduleRotation = moduleTransform != null ? moduleTransform.rotation : Quaternion.identity;
            HectonEventBus.Publish(new BaseModulePlacedEvent(
                activeBuildable,
                moduleEntityId,
                modulePosition,
                moduleRotation,
                hasModulePose));
            PlaySound(buildSound);
            NotifyBuildPlaced(activeBuildable);

            // ── Сброс snap-состояния ──
            _isSnapped = false;
            _snappedSocketTransform = null;
            _snappedSocket = null;
            _snappedGhostSocket = null;

            // ── Пересоздаём призрак ──
            DespawnGhost();
            SpawnGhost();
        }

        // ══════════════════════════════════════════════════════════
        //  RESOURCE CHECKING
        // ══════════════════════════════════════════════════════════

        private bool HasResources(BuildableData data)
        {
            if (_habitatConstructionManager == null)
                return false;

            return _habitatConstructionManager.HasBuildResources(inventory, data);
        }

        private void ResolveRuntimeReferences()
        {
            LogBuilderDebug("ResolveRuntimeReferences begin");
            if (_habitatConstructionManager == null)
                _habitatConstructionManager = new HabitatConstructionManager();

            IPlayerRuntimeContext playerContext = ResolvePlayerContext();
            if (inventory == null && playerContext != null)
                inventory = playerContext.Inventory;
            LogBuilderDebug($"ResolveRuntimeReferences inventory={(inventory != null ? "Y" : "N")}");

            if (playerCamera == null && playerContext != null)
                playerCamera = playerContext.PlayerCamera;
            LogBuilderDebug($"ResolveRuntimeReferences camera={(playerCamera != null ? playerCamera.name : "null")}");

            if (buildAnchor == null && playerContext != null)
                buildAnchor = playerContext.HandAnchor;
            LogBuilderDebug($"ResolveRuntimeReferences buildAnchor={(buildAnchor != null ? buildAnchor.name : "null")}");

            if (hudNotification == null && playerContext != null)
                hudNotification = playerContext.HudNotification;
            LogBuilderDebug($"ResolveRuntimeReferences hud={(hudNotification != null ? "Y" : "N")}");

            if (_buildCatalog == null)
                _buildCatalog = ResolveModuleCatalog();
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
                CacheActivePlacementRule();
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

        private bool TryGetObjectPool(out ObjectPoolManager pool)
        {
            pool = GlobalRegistry.ObjectPool;
            return pool != null;
        }

        private GameObject SpawnPlacedModule(BuildableData data, Vector3 placePos, Quaternion placeRot, ObjectPoolManager pool)
        {
            if (data == null)
                return null;

            GameObject placedModule;
            if (data.finalPrefab == null)
            {
                if (!ConstructionRuntimeProxyFactory.TryCreatePlacedProxy(data, placePos, placeRot, out placedModule))
                    return null;

                LogBuilderDebug($"SpawnPlacedModule begin module={data.moduleName} prefab=RUNTIME_PROXY");
            }
            else
            {
                if (pool == null)
                    return null;

                LogBuilderDebug($"SpawnPlacedModule begin module={data.moduleName} prefab={data.finalPrefab.name}");
                LogBuilderDebug("SpawnPlacedModule using pool.");
                placedModule = pool.Spawn(data.finalPrefab, placePos, placeRot);
            }

            if (placedModule != null)
            {
                ConstructionManager manager = ResolveConstructionManager();
                if (manager != null)
                {
                    manager.RegisterModule(placedModule, data);
                    LogBuilderDebug($"SpawnPlacedModule registered moduleCount={manager.ModuleCount}");
                }
            }

            LogBuilderDebug($"SpawnPlacedModule end result={(placedModule != null ? placedModule.name : "null")}");
            return placedModule;
        }

        private int ResolveSurfaceMask()
        {
            return surfaceMask.value != 0
                ? surfaceMask.value
                : HectonLayerMasks.ConstructionSurfaceLayerMask;
        }

        private int ResolveSocketMask()
        {
            return socketLayerMask.value != 0
                ? socketLayerMask.value
                : HectonLayerMasks.SocketsLayerMask;
        }

        private bool TryResolveExactSnappedPlacementPose(ref Vector3 placePos, ref Quaternion placeRot)
        {
            if (!_isSnapped ||
                _snappedSocket == null ||
                _currentGhostObj == null ||
                _habitatConstructionManager == null)
            {
                return false;
            }

            if (!_habitatConstructionManager.TryResolveSocketAlignment(
                    _currentGhostObj.transform,
                    _ghostSocketBuffer,
                    _snappedSocket,
                    _ghostYawOffset,
                    out Vector3 alignedPosition,
                    out Quaternion alignedRotation,
                    out ModuleSocket alignedGhostSocket))
            {
                return false;
            }

            placePos = alignedPosition;
            placeRot = alignedRotation;
            _snappedGhostSocket = alignedGhostSocket;
            _currentGhostObj.transform.SetPositionAndRotation(placePos, placeRot);
            return true;
        }

        private static void ApplyConstructedModuleSnap(GameObject placedModule, Vector3 placePos, Quaternion placeRot)
        {
            if (placedModule == null)
                return;

            if (placedModule.TryGetComponent(out BaseModule baseModule))
            {
                baseModule.ApplyConstructedWeldSnap(placePos, placeRot);
                return;
            }

            if (placedModule.TryGetComponent(out Rigidbody body))
            {
                PhysicsForceRouter.ApplyKinematicWeldSnap(body, placePos, placeRot);
                return;
            }

            placedModule.transform.SetPositionAndRotation(placePos, placeRot);
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

            if (!UpdatePlacementValidityState() || !_currentGhost.CanBuild)
                return BuildReadiness.PlacementBlocked;

            return _isSnapped ? BuildReadiness.SnappedReady : BuildReadiness.Ready;
        }

        private void CacheActivePlacementRule()
        {
            _activePlacementRule = null;
            _semanticPlacementValid = true;
            _semanticPlacementBlockReason = string.Empty;

            if (activeBuildable == null || activeBuildable.finalPrefab == null)
                return;

            _placementRuleBuffer.Clear();
            activeBuildable.finalPrefab.GetComponents(_placementRuleBuffer);

            for (int i = 0; i < _placementRuleBuffer.Count; i++)
            {
                MonoBehaviour behaviour = _placementRuleBuffer[i];
                if (behaviour is IBuildPlacementRule rule)
                {
                    _activePlacementRule = rule;
                    break;
                }
            }

            _placementRuleBuffer.Clear();
        }

        private bool UpdateSemanticPlacementState()
        {
            if (_activePlacementRule == null || _currentGhostObj == null)
            {
                _semanticPlacementValid = true;
                _semanticPlacementBlockReason = string.Empty;
                return true;
            }

            Transform ghostTransform = _currentGhostObj.transform;
            _semanticPlacementValid = _activePlacementRule.ValidatePlacement(
                ghostTransform.position,
                ghostTransform.rotation,
                out _semanticPlacementBlockReason);

            if (_semanticPlacementValid)
                _semanticPlacementBlockReason = string.Empty;

            return _semanticPlacementValid;
        }

        private void CacheGhostSockets()
        {
            _ghostSocketBuffer.Clear();
            if (_currentGhostObj == null)
                return;

            _currentGhostObj.GetComponentsInChildren<ModuleSocket>(true, _ghostSocketBuffer);
        }

        private bool UpdatePlacementValidityState()
        {
            bool semanticValid = UpdateSemanticPlacementState();
            bool finalValid = semanticValid && _integrityPlacementValid;

            if (_currentGhost != null)
                _currentGhost.SetExternalValidity(finalValid);

            return finalValid;
        }

        private void UpdatePlacementValidationState()
        {
            if (_habitatConstructionManager == null || activeBuildable == null || _currentGhostObj == null)
            {
                _integrityPlacementValid = true;
                _integrityPlacementBlockReason = string.Empty;
                UpdatePlacementValidityState();
                return;
            }

            if (_habitatConstructionManager.TryConsumeCompletedValidation())
            {
                _integrityPlacementValid = _habitatConstructionManager.LastPlacementAllowed;
                _integrityPlacementBlockReason = _habitatConstructionManager.LastBlockReason;
                if (_hasScheduledValidationSnapshot)
                {
                    _completedValidationSnapshot = _scheduledValidationSnapshot;
                    _hasCompletedValidationSnapshot = true;
                }
            }

            if (!TryCaptureValidationSnapshot(out ValidationSnapshot snapshot))
            {
                _integrityPlacementValid = true;
                _integrityPlacementBlockReason = string.Empty;
                UpdatePlacementValidityState();
                return;
            }

            if (_habitatConstructionManager.IsValidationPending &&
                _hasScheduledValidationSnapshot &&
                !AreEquivalentSnapshots(snapshot, _scheduledValidationSnapshot))
            {
                _integrityValidationDirty = true;
            }

            bool needsValidation =
                _integrityValidationDirty ||
                !_hasCompletedValidationSnapshot ||
                !AreEquivalentSnapshots(snapshot, _completedValidationSnapshot);

            if (!_habitatConstructionManager.IsValidationPending && needsValidation)
            {
                ConstructionManager constructionManager = ResolveConstructionManager();
                if (_habitatConstructionManager.ScheduleIntegrityValidation(
                        constructionManager,
                        _currentGhostObj,
                        activeBuildable,
                        constructionGridSize,
                        structuralIntegrityBudget,
                        structuralDepthPenalty))
                {
                    _scheduledValidationSnapshot = snapshot;
                    _hasScheduledValidationSnapshot = true;
                    _integrityPlacementValid = false;
                    _integrityPlacementBlockReason = HabitatConstructionManager.PendingReason;
                    _integrityValidationDirty = false;
                }
                else
                {
                    _integrityPlacementValid = _habitatConstructionManager.LastPlacementAllowed;
                    _integrityPlacementBlockReason = _habitatConstructionManager.LastBlockReason;
                    _integrityValidationDirty = false;
                }
            }

            UpdatePlacementValidityState();
        }

        private bool TryCaptureValidationSnapshot(out ValidationSnapshot snapshot)
        {
            snapshot = default;
            if (_currentGhostObj == null || activeBuildable == null)
                return false;

            Transform ghostTransform = _currentGhostObj.transform;
            ConstructionManager constructionManager = ResolveConstructionManager();
            snapshot.Buildable = activeBuildable;
            snapshot.TargetSocket = _snappedSocket;
            snapshot.ModuleCount = constructionManager != null ? constructionManager.ModuleCount : 0;
            snapshot.Position = ghostTransform.position;
            snapshot.Rotation = ghostTransform.rotation;
            return true;
        }

        private static bool AreEquivalentSnapshots(ValidationSnapshot lhs, ValidationSnapshot rhs)
        {
            return ReferenceEquals(lhs.Buildable, rhs.Buildable) &&
                   ReferenceEquals(lhs.TargetSocket, rhs.TargetSocket) &&
                   lhs.ModuleCount == rhs.ModuleCount &&
                   (lhs.Position - rhs.Position).sqrMagnitude <= 0.0001f &&
                   Quaternion.Dot(lhs.Rotation, rhs.Rotation) >= 0.9999f;
        }

        private string ResolvePlacementBlockReason()
        {
            if (!string.IsNullOrEmpty(_semanticPlacementBlockReason))
                return _semanticPlacementBlockReason;

            if (!_integrityPlacementValid && !string.IsNullOrEmpty(_integrityPlacementBlockReason))
                return _integrityPlacementBlockReason;

            return "PLACEMENT INVALID";
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

                int available = inventory != null && cost.item != null
                    ? inventory.CountTotal(Hecton.Localization.LocHash.Compute(cost.item.PersistentId))
                    : 0;
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
            if (!TryGetBuildHit(ray, HectonLayerMasks.ConstructionSurfaceLayerMask, out RaycastHit hit))
                return null;

            return hit.collider != null ? hit.collider.GetComponentInParent<BaseModule>() : null;
        }

        private bool TryGetBuildHit(Ray ray, LayerMask mask, out RaycastHit hit)
        {
            int hitCount = UnityEngine.Physics.RaycastNonAlloc(
                ray,
                _buildHits,
                buildDistance,
                mask,
                QueryTriggerInteraction.Ignore);

            if (hitCount > 0)
            {
                hit = _buildHits[0];
                return true;
            }

            hit = default;
            return false;
        }

        private bool ConsumeResources(BuildableData data)
        {
            if (_habitatConstructionManager == null)
                return false;

            return _habitatConstructionManager.ConsumeBuildResources(inventory, data);
        }

        // ══════════════════════════════════════════════════════════
        //  AUDIO
        // ══════════════════════════════════════════════════════════

        private void RefreshInputSubscriptions()
        {
            IInputService inputService = GlobalRegistry.Input;
            if (ReferenceEquals(_subscribedInputService, inputService))
                return;

            UnsubscribeFromInputService();
            if (inputService == null)
                return;

            inputService.OnPrimaryAction += HandlePrimaryAction;
            inputService.OnSecondaryAction += HandleSecondaryAction;
            inputService.OnInteract += HandleInteract;
            inputService.OnTabNext += HandleTabNext;
            inputService.OnTabPrevious += HandleTabPrevious;
            _subscribedInputService = inputService;
        }

        private void UnsubscribeFromInputService()
        {
            if (_subscribedInputService == null)
                return;

            _subscribedInputService.OnPrimaryAction -= HandlePrimaryAction;
            _subscribedInputService.OnSecondaryAction -= HandleSecondaryAction;
            _subscribedInputService.OnInteract -= HandleInteract;
            _subscribedInputService.OnTabNext -= HandleTabNext;
            _subscribedInputService.OnTabPrevious -= HandleTabPrevious;
            _subscribedInputService = null;
        }

        private static IPlayerRuntimeContext ResolvePlayerContext()
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null)
                return playerContext;

            PlayerRuntimeContextService playerService = PlayerRuntimeContextService.EnsureRuntimeInstance();
            playerService.InitializeService();
            return GlobalRegistry.Player;
        }

        private static IEnvironmentRuntimeContext ResolveEnvironmentContext()
        {
            IEnvironmentRuntimeContext environmentContext = GlobalRegistry.Environment;
            if (environmentContext != null)
                return environmentContext;

            EnvironmentRuntimeContextService environmentService = EnvironmentRuntimeContextService.EnsureRuntimeInstance();
            environmentService.InitializeService();
            return GlobalRegistry.Environment;
        }

        private static ConstructionManager ResolveConstructionManager()
        {
            IEnvironmentRuntimeContext environmentContext = ResolveEnvironmentContext();
            return environmentContext != null ? environmentContext.ConstructionManager : null;
        }

        private static ModuleCatalog ResolveModuleCatalog()
        {
            IEnvironmentRuntimeContext environmentContext = ResolveEnvironmentContext();
            return environmentContext != null ? environmentContext.ModuleCatalog : null;
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip == null)
                return;

            if (Hecton8.Core.GlobalRegistry.Audio != null)
                Hecton8.Core.GlobalRegistry.Audio.PlayStatic2D(clip);
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
            if (constructionGridSize < 0.25f) constructionGridSize = 0.25f;
            if (structuralIntegrityBudget < 1f) structuralIntegrityBudget = 1f;
            if (structuralDepthPenalty < 0.01f) structuralDepthPenalty = 0.01f;
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

