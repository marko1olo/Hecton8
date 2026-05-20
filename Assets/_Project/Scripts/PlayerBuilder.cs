// ============================================================================
// HECTON-8 — PlayerBuilder.cs
// Kontroller stroitelstva modulnoy bazy.
//
// v3.0 — SOCKET SNAP SYSTEM:
//   [ADD] Sistema magnitnogo prilipaniya k tochkam stykovki (ModuleSocket).
//   [ADD] Poisk soketov cherez SHINOBU_217 template/AUP resolver (zero GC target).
//   [ADD] Gisterezis: snapRadius=2m, unsnapRadius=2.5m (bez mertsaniya).
//   [ADD] Plavnyy snap/unsnap cherez eksponentsialnoe sglazhivanie.
//   [ADD] Zanyatye sokety (IsOccupied) propuskayutsya pri poiske.
//   [ADD] Pri razmeschenii: blizhayshiy soket pomechaetsya kak occupied.
//   [ADD] socketLayerMask dlya filtratsii (Layer "Sockets").
//
//   POVEDENIE:
//     1. Raycast iz kamery → hitPoint na poverhnosti.
//     2. Data-oriented socket query vokrug hitPoint.
//     3. Esli nayden svobodnyy soket ≤ snapRadius → snap mode:
//        - Pozitsiya prizraka = socket.position
//        - Rotatsiya prizraka = socket.rotation × yawOffset
//     4. Esli rasstoyanie do snapnutogo soketa > unsnapRadius → unsnap:
//        - Plavnyy perehod obratno k raycast-pozitsii.
//     5. Gisterezis (snap=2m, unsnap=2.5m) predotvraschaet mertsanie.
//
//   ZERO GC:
//     • Socket adapter math → no PhysX socket broadphase.
//     • TryGetComponent<ModuleSocket> → zero GC.
//     • Vse struct math, nikakih List/LINQ/lyambd.
//
// PREDYDUSchIE VERSII (sohraneny):
//   v2.0: PlayerTool inheritance, ghost pool lifecycle.
//   v1.0: Basic placement.
// ============================================================================

using System;
using System.Collections.Generic;
using Hecton8.Audio;
using Hecton8.Building;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Modding;
using Hecton8.Construction;
using Hecton8.Physics;
using Hecton8.UI;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using ConstructionMockWorldSampler = Hecton8.Construction.MockWorldSampler;

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
            SnappedReady = 5,
            BlueprintLocked = 6
        }

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — REFERENCES
        // ══════════════════════════════════════════════════════════

        [Header("── Builder References ────────────────────────")]
        [Tooltip("Inventar igroka dlya proverki i spisaniya resursov")]
        [SerializeField] private PlayerInventory inventory;

        [Tooltip("Kamera igroka (ot nee puskaetsya Raycast)")]
        [SerializeField] private Camera playerCamera;

        [Tooltip("Tochka pered kameroy (fallback, esli Raycast v pustotu)")]
        [SerializeField] private Transform buildAnchor;
        [SerializeField] private HUDNotification hudNotification;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — BUILDING
        // ══════════════════════════════════════════════════════════

        [Header("── Building ──────────────────────────────────")]
        [Tooltip("Aktivnyy modul dlya stroitelstva.")]
        [SerializeField] private BuildableData activeBuildable;
        [SerializeField] private bool autoResolveCatalogSelection = true;

        [Tooltip("Maksimalnaya dalnost razmescheniya (metry)")]
        [SerializeField] private float buildDistance = 8f;

        [Tooltip("Skorost sglazhivaniya dvizheniya prizraka")]
        [SerializeField] private float ghostFollowSpeed = 12f;

        [Tooltip("Sloy poverhnosti dlya razmescheniya (Terrain, Default)")]
        [SerializeField] private LayerMask surfaceMask = HectonLayerMasks.ConstructionSurfaceLayerMask;
        [Tooltip("Rigid world-space grid size used for free placement positions.")]
        [SerializeField] private float constructionGridSize = 4f;
        [Tooltip("Total structural integrity budget available to the current habitat graph.")]
        [SerializeField] private float structuralIntegrityBudget = 240f;
        [Tooltip("Integrity penalty applied for every BFS depth step away from the support root.")]
        [SerializeField] private float structuralDepthPenalty = 0.75f;

        [Header("── Rotation ──────────────────────────────────")]
        [Tooltip("Ugol povorota prizraka pri nazhatii PKM (gradusy)")]
        [SerializeField] private float rotationStep = 90f;

        [Header("── Diagnostics ───────────────────────────────")]
        [Tooltip("Vklyuchit podrobnye BuilderDebug-logi dlya diagnostiki construction loop.")]
        [SerializeField] private bool builderDebugLogging = false;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — SOCKET SNAP (v3.0)
        // ══════════════════════════════════════════════════════════

        [Header("── Socket Snap (v3.0) ────────────────────────")]
        [Tooltip("Radius obnaruzheniya soketov vokrug tochki lucha (metry).\n" +
                 "Kogda hitPoint ≤ snapRadius ot svobodnogo soketa → snap.")]
        [SerializeField] private float snapRadius = 1f;

        [Tooltip("Radius otryva ot soketa (metry).\n" +
                 "Dolzhen byt > snapRadius dlya gisterezisa.\n" +
                 "Kogda hitPoint > unsnapRadius ot snapnutogo soketa → unsnap.")]
        [SerializeField] private float unsnapRadius = 1.25f;

        [Tooltip("Legacy socket layer retained for authored prefabs during migration.\n" +
                 "Sozday Layer 'Sockets' v Project Settings → Tags & Layers.\n" +
                 "SHINOBU_217 runtime proxy sockets no longer require trigger colliders.")]
        [SerializeField] private LayerMask socketLayerMask = HectonLayerMasks.SocketsLayerMask;

        [Tooltip("Skorost prilipaniya k soketu (Lerp factor per second).\n" +
                 "Vyshe = rezche snap. 20 = pochti mgnovenno.")]
        [SerializeField] private float snapSpeed = 20f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — AUDIO
        // ══════════════════════════════════════════════════════════

        [Header("── Audio ─────────────────────────────────────")]
        [SerializeField] private AudioClip buildSound;
        [SerializeField] private AudioClip errorSound;
        [SerializeField] private AudioClip rotateSound;

        [Tooltip("Zvuk prilipaniya k soketu (optsionalno).")]
        [SerializeField] private AudioClip snapSound;

        // ══════════════════════════════════════════════════════════
        //  CACHED STATE
        // ══════════════════════════════════════════════════════════

        private GameObject _currentGhostObj;
        private PlacementGhost _currentGhost;
        private bool _currentGhostUsesRuntimeProxy;
        private bool _builderGhostPreviewActive;
        private bool _builderGhostPreviewCanBuild = true;
        private Vector3 _builderGhostPreviewPosition;
        private Quaternion _builderGhostPreviewRotation = Quaternion.identity;
        private Vector3 _builderGhostPreviewScale = Vector3.one;
        private RaycastHit _hit;
        private readonly RaycastHit[] _buildHits = new RaycastHit[1]; // COLD ALLOC: single surface probe for build targeting.
        private const float StructuralPlacementGridMeters = 4f;
        private const float StructuralPlacementGridInv = 0.25f;
        private const float StructuralRotationStepDegrees = 90f;
        private const float StructuralSnapRadiusMeters = 1f;
        private const float StructuralUnsnapRadiusMeters = 1.25f;
        private int _ghostYawStep;
        private static readonly Vector3 ViewportCenter = new Vector3(0.5f, 0.5f, 0f);

        // ══════════════════════════════════════════════════════════
        //  SOCKET SNAP STATE (v3.0)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// SHINOBU_217 socket adapter state.
        /// 16 soketov — pokryvaet dazhe hab s 8 vyhodami.
        /// Zero GC: massiv sozdaetsya odin raz.
        /// </summary>
        /// <summary>
        /// true kogda prizrak "prilip" k soketu.
        /// Ispolzuetsya dlya gisterezisa (snap/unsnap raznye radiusy).
        /// </summary>
        private bool _isSnapped;

        /// <summary>
        /// Transform soketa, k kotoromu prilip prizrak.
        /// null kogda ne v snap-rezhime.
        /// Keshiruetsya dlya: pozitsii, rotatsii, i otmetki occupied pri razmeschenii.
        /// </summary>
        private Transform _snappedSocketTransform;

        /// <summary>
        /// Keshirovannyy ModuleSocket komponent snapnutogo soketa.
        /// Ispolzuetsya dlya proverki IsOccupied i SetOccupied pri razmeschenii.
        /// </summary>
        private ModuleSocket _snappedSocket;

        /// <summary>
        /// Predyduschiy snap-status. Dlya edge detection (zvuk pri snap/unsnap).
        /// </summary>
        private bool _wasSnapped;
        private ModuleCatalog _buildCatalog;
        private int _activeBuildableIndex = -1;
        // COLD ALLOC: List<MonoBehaviour>[2] — authored placement-rule scan buffer for the active buildable prefab — owner: PlayerBuilder
        private readonly List<MonoBehaviour> _placementRuleBuffer = new List<MonoBehaviour>(2);
        private readonly List<ModuleSocket> _ghostSocketBuffer = new List<ModuleSocket>(8);
        private FixedCharBuffer _builderHudBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - builder HUD notification staging buffer - owner: PlayerBuilder
        private FixedCharBuffer _builderLogTitleBuffer = new FixedCharBuffer(256); // COLD ALLOC: char[256] - builder field-log title staging buffer - owner: PlayerBuilder
        private FixedCharBuffer _builderLogSummaryBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - builder field-log summary staging buffer - owner: PlayerBuilder
        private IBuildPlacementRule _activePlacementRule;
        private bool _semanticPlacementValid = true;
        private string _semanticPlacementBlockReason = string.Empty;
        private bool _terrainSdfPlacementValid = true;
        private string _terrainSdfPlacementBlockReason = string.Empty;
        private const float TerrainSdfBlockHapticCooldownSeconds = 0.35f;
        private const float TerrainSdfBlockHapticPower = 0.35f;
        private const float TerrainSdfBlockHapticRatedPower = 1f;
        private const byte TerrainSdfBlockHapticPriority = 2;
        private bool _terrainSdfWasBlocked;
        private float _terrainSdfBlockHapticCooldown;
        private ConstructionRequestDTO _lastConstructionValidationRequest;
        private StructuralBoundsDTO _lastConstructionValidationBounds;
        private ConstructionValidationSettingsDTO _lastConstructionValidationSettings;
        private ConstructionValidationResultDTO _lastConstructionValidationResult;
        private ConstructionMockWorldSampler _lastConstructionWorldSampler;
        private static bool s_ConstructionSignalLanesInitialized;
        private HabitatConstructionManager _habitatConstructionManager;
        private ConstructionManager _cachedConstructionManager;
        private ModuleSocket _snappedGhostSocket;
        private bool _shinobuHasSnappedPose;
        private Vector3 _shinobuSnappedPosePosition;
        private Quaternion _shinobuSnappedPoseRotation;
        private Transform _shinobuSnappedTargetTransform;
        private Vector3 _shinobuSnappedTargetLocalPosition;
        private ModuleSocketDirection _shinobuSnappedTargetDirection;
        private uint _shinobuSnappedTargetCompatibilityHash;
        private int _shinobuSnappedGhostSocketIndex = -1;
        private float _shinobuDearLieDampen;
        private int _shinobuSocketAdapterCandidateCount;
        private IDataVault _shinobuSocketVault;
        private uint _shinobuSocketVaultSceneHash;
        private int _shinobuSocketVaultModuleCount = -1;
        private int _shinobuSocketVaultTargetCount;
        private uint _shinobuSocketVaultTopologyVersion;
        private uint _shinobuSocketFrameCounter;
        private JobHandle _shinobuSocketSnapHandle;
        private bool _shinobuSocketSnapPending;
        private int _shinobuSocketSnapBestResultIndex;
        private uint _shinobuSocketSnapFrame;
        private uint _shinobuSocketSnapSceneHash;
        private uint _shinobuSocketSnapQueryHash;
        private double3 _shinobuSocketSnapGhostRootAup;
        private long _shinobuSocketSnapStartTicks;
        private float _shinobuSocketCachedBestDistanceSq = float.MaxValue;
        private readonly List<ModuleSocket> _shinobuTargetSocketBuffer = new List<ModuleSocket>(8);
        private bool _integrityPlacementValid = true;
        private bool _integrityValidationDirty;
        private string _integrityPlacementBlockReason = string.Empty;
        private ValidationSnapshot _scheduledValidationSnapshot;
        private ValidationSnapshot _completedValidationSnapshot;
        private bool _hasScheduledValidationSnapshot;
        private bool _hasCompletedValidationSnapshot;
        private uint _lastPlayerInputSignalSequence;
        private const uint PlayerInputSignalSourceHash = 0x504C494Eu;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        public BuildableData ActiveBuildable => activeBuildable;
        public int ActiveBuildableIndex => _activeBuildableIndex;
        public int BuildableCount => _buildCatalog != null ? _buildCatalog.ViewableCount : 0;
        public bool HasResourcesForActiveBuildable => activeBuildable != null && IsBuildableBlueprintViewable(activeBuildable) && HasResources(activeBuildable);
        public bool CanPlaceActiveBuildable => activeBuildable != null && IsBuildableBlueprintViewable(activeBuildable) && _builderGhostPreviewActive && _builderGhostPreviewCanBuild && _semanticPlacementValid && _terrainSdfPlacementValid && _integrityPlacementValid;
        public bool HasPlacementPreview => _builderGhostPreviewActive;
        public BuildReadiness ActiveBuildReadiness => GetActiveBuildReadiness();

        /// <summary>Seychas prizrak prilip k soketu.</summary>
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
            if (_buildCatalog == null || index < 0 || index >= _buildCatalog.ViewableCount)
                return null;

            return _buildCatalog.GetViewableAt(index);
        }

        public BuildableData GetRelativeBuildable(int direction)
        {
            if (_buildCatalog == null || _buildCatalog.ViewableCount <= 0)
                return null;

            int currentIndex = _buildCatalog.IndexOfViewable(activeBuildable);
            if (currentIndex < 0)
                currentIndex = direction >= 0 ? -1 : 0;

            int viewableCount = _buildCatalog.ViewableCount;
            int nextIndex = (currentIndex + direction + viewableCount) % viewableCount;
            return _buildCatalog.GetViewableAt(nextIndex);
        }

        public bool DebugDeployActiveBuildable(Vector3 position, Quaternion rotation, bool consumeCost = true)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (builderDebugLogging)
                LogBuilderDebug($"DebugDeploy enter consumeCost={consumeCost} pos={position}");
#endif
            LogBuilderDebug("DebugDeploy -> ResolveRuntimeReferences");
            ResolveRuntimeReferences();
            LogBuilderDebug("DebugDeploy -> EnsureCatalogSelection");
            EnsureCatalogSelection();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (builderDebugLogging)
                LogBuilderDebug($"DebugDeploy -> active={(activeBuildable != null ? activeBuildable.moduleName : "null")}");
#endif
            if (activeBuildable == null || activeBuildable.finalPrefab == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[BuilderDebug] DebugDeploy aborted: no active buildable/final prefab.");
#endif
                return false;
            }

            if (!IsBuildableBlueprintViewable(activeBuildable))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[BuilderDebug] DebugDeploy aborted: blueprint locked.");
#endif
                return false;
            }

            if (consumeCost && !HasResources(activeBuildable))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[BuilderDebug] DebugDeploy aborted: missing resources for active buildable.");
#endif
                return false;
            }

            if (!TryGetObjectPool(out ObjectPoolManager pool))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[BuilderDebug] DebugDeploy aborted: ObjectPoolManager unavailable.");
#endif
                return false;
            }

            GameObject spawned = SpawnPlacedModule(activeBuildable, position, rotation, pool);
            if (spawned == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[BuilderDebug] DebugDeploy aborted: failed to spawn active buildable.");
#endif
                return false;
            }

            ApplyConstructedModuleSnap(spawned, position, rotation);

            if (consumeCost)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (builderDebugLogging)
                    LogBuilderDebug($"DebugDeploy consuming cost for {activeBuildable.moduleName}.");
#endif
                if (!ConsumeResources(activeBuildable))
                {
                    ConstructionManager constructionManager = ResolveCachedConstructionManager();
                    if (constructionManager != null)
                        constructionManager.DestroyModule(spawned);
                    else
                        pool.Despawn(spawned);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning("[BuilderDebug] DebugDeploy aborted: resource transaction failed.");
#endif
                    return false;
                }
            }

            PublishConstructionCommitSignals(spawned, activeBuildable);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (builderDebugLogging)
                LogBuilderDebug($"DebugDeploy spawnResult={(spawned != null ? spawned.name : "null")}");
#endif
            return spawned != null;
        }

        public bool DebugRecoverModule(BaseModule module)
        {
            if (module == null || !module.CanDeconstruct())
                return false;

            Vector3 modulePosition = module.transform.position;
            if (!TryRequestModuleDeconstruction(module, modulePosition + Vector3.up, Vector3.down, 0f, 1))
                return false;

            NotifyModuleDeconstructionQueued(module);
            return true;
        }

        public bool TryGetPlacementPreviewPose(out Vector3 position, out Quaternion rotation)
        {
            if (!_builderGhostPreviewActive)
            {
                position = default;
                rotation = default;
                return false;
            }

            position = _builderGhostPreviewPosition;
            rotation = _builderGhostPreviewRotation;
            return true;
        }

        public bool TryDeployActiveBuildableFromPreview()
        {
            if (!_builderGhostPreviewActive || !_builderGhostPreviewCanBuild)
                return false;
            if (!UpdatePlacementValidityState())
                return false;
            if (activeBuildable == null)
                return false;
            if (!IsBuildableBlueprintViewable(activeBuildable))
                return false;
            if (!HasResources(activeBuildable))
                return false;

            TryPlaceModuleInternal();
            return true;
        }

        public override void WriteOperationalSummary(ref FixedCharBuffer buffer)
        {
            AppendText(ref buffer, "BUILDER // ");
            WriteActiveBuildOperationalSummary(ref buffer);
            AppendText(ref buffer, " // ");
            WriteActiveBuildStatusLabel(ref buffer);
        }

        public override void WriteOperationalDirective(ref FixedCharBuffer buffer)
        {
            WriteActiveBuildAdvice(ref buffer);
        }

        public string GetActiveBuildStatusLabel()
        {
            switch (GetActiveBuildReadiness())
            {
                case BuildReadiness.Offline: return "OFFLINE";
                case BuildReadiness.NoSelection: return "NO MODULE";
                case BuildReadiness.BlueprintLocked: return "BLUEPRINT LOCKED";
                case BuildReadiness.MissingCost: return "MISSING COST";
                case BuildReadiness.PlacementBlocked: return "PLACEMENT BLOCKED";
                case BuildReadiness.SnappedReady: return "SNAPPED READY";
                default: return "READY";
            }
        }

        public string GetActiveBuildAdvice()
        {
            _builderLogSummaryBuffer.Clear();
            WriteActiveBuildAdvice(ref _builderLogSummaryBuffer);
            return _builderLogSummaryBuffer.ToString();
        }

        public void WriteActiveBuildStatusLabel(ref FixedCharBuffer buffer)
        {
            switch (GetActiveBuildReadiness())
            {
                case BuildReadiness.Offline:
                    AppendText(ref buffer, "OFFLINE");
                    return;
                case BuildReadiness.NoSelection:
                    AppendText(ref buffer, "NO MODULE");
                    return;
                case BuildReadiness.BlueprintLocked:
                    AppendText(ref buffer, "BLUEPRINT LOCKED");
                    return;
                case BuildReadiness.MissingCost:
                    AppendText(ref buffer, "MISSING COST");
                    return;
                case BuildReadiness.PlacementBlocked:
                    AppendText(ref buffer, "PLACEMENT BLOCKED");
                    return;
                case BuildReadiness.SnappedReady:
                    AppendText(ref buffer, "SNAPPED READY");
                    return;
                default:
                    AppendText(ref buffer, "READY");
                    return;
            }
        }

        public void WriteActiveBuildAdvice(ref FixedCharBuffer buffer)
        {
            string purpose = DescribeBuildPurpose(activeBuildable);

            switch (GetActiveBuildReadiness())
            {
                case BuildReadiness.Offline:
                    AppendText(ref buffer, "Restore builder links before field deployment.");
                    return;
                case BuildReadiness.NoSelection:
                    AppendText(ref buffer, "Pick a buildable from PDA Construction or cycle the catalog.");
                    return;
                case BuildReadiness.BlueprintLocked:
                    AppendText(ref buffer, "Recover the linked quest signal before this blueprint can deploy.");
                    return;
                case BuildReadiness.MissingCost:
                    AppendText(ref buffer, purpose);
                    AppendText(ref buffer, " Recover materials first. Need ");
                    WriteActiveCostDigest(ref buffer);
                    AppendText(ref buffer, ".");
                    return;
                case BuildReadiness.PlacementBlocked:
                    AppendText(ref buffer, purpose);
                    AppendText(ref buffer, " ");
                    string blockReason = ResolvePlacementBlockReason();
                    if (!string.IsNullOrEmpty(blockReason))
                    {
                        AppendText(ref buffer, blockReason);
                        AppendText(ref buffer, ".");
                        return;
                    }

                    AppendText(
                        ref buffer,
                        IsSnapped
                            ? "Socket alignment is good, but the final volume is obstructed."
                            : "Reposition, rotate, or snap to a valid socket.");
                    return;
                case BuildReadiness.SnappedReady:
                    AppendText(ref buffer, purpose);
                    AppendText(ref buffer, " Placement is socket-locked and ready to deploy.");
                    return;
                default:
                    AppendText(ref buffer, purpose);
                    AppendText(ref buffer, " Placement is clear. Deploy when ready.");
                    return;
            }
        }

        public void WriteActiveCostDigest(ref FixedCharBuffer buffer)
        {
            WriteCostDigest(activeBuildable, ref buffer);
        }

        public string GetActiveBuildRoleLabel()
        {
            return DescribePowerRole(activeBuildable);
        }

        public string GetActiveBuildFamilyAndRoleLabel()
        {
            if (activeBuildable == null)
                return "NO FAMILY // NO ROLE";

            _builderLogSummaryBuffer.Clear();
            AppendText(ref _builderLogSummaryBuffer, activeBuildable.FamilyShortCode);
            AppendText(ref _builderLogSummaryBuffer, " // ");
            AppendText(ref _builderLogSummaryBuffer, DescribePowerRole(activeBuildable));
            return _builderLogSummaryBuffer.ToString();
        }

        public string GetActiveBuildOperationalSummary()
        {
            _builderLogSummaryBuffer.Clear();
            WriteActiveBuildOperationalSummary(ref _builderLogSummaryBuffer);
            return _builderLogSummaryBuffer.ToString();
        }

        public void WriteActiveBuildOperationalSummary(ref FixedCharBuffer buffer)
        {
            if (activeBuildable == null)
            {
                AppendText(ref buffer, "NO MODULE");
                return;
            }

            string moduleName = activeBuildable.moduleName;
            if (string.IsNullOrWhiteSpace(moduleName))
                AppendText(ref buffer, "MODULE");
            else
                AppendUpperInvariant(ref buffer, moduleName);

            AppendText(ref buffer, " // ");
            AppendText(ref buffer, activeBuildable.FamilyShortCode);
            AppendText(ref buffer, " // ");
            AppendText(ref buffer, DescribePowerRole(activeBuildable));
        }

        public void SetActiveBuildable(BuildableData data)
        {
            if (data == null) return;
            if (!IsBuildableBlueprintViewable(data)) return;

            bool wasEquipped = IsEquipped;
            CompleteShinobuSocketSnapForTeardown();

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
            DespawnGhost();
            ResetBuilderState();
            base.OnDespawn();
        }

        private void OnDestroy()
        {
            CompleteShinobuSocketSnapForTeardown();

            if (_habitatConstructionManager != null)
            {
                _habitatConstructionManager.Dispose();
                _habitatConstructionManager = null;
            }

            _cachedConstructionManager = null;
            _shinobuSocketVault = null;
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
            BaselineBuilderInputSignalSequence();

            SpawnGhost();
            NotifyBuildableSelection();
        }

        public override void OnUnequip()
        {
            DespawnGhost();
            ResetBuilderState();
            base.OnUnequip();
        }

        public override void ToolTick(float deltaTime)
        {
            ConsumeBuilderInputSignals();
            // Position update only; edge input is consumed from PlayerInputSignal.
            if (_builderGhostPreviewActive)
            {
                UpdateGhostPosition(deltaTime);
                UpdatePlacementValidationState();
                DrawBuildGhostProjection();
            }
        }

        private void ConsumeBuilderInputSignals()
        {
            ReadOnlySpan<PlayerInputSignal> signals = SignalBus<PlayerInputSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PlayerInputSignal signal = signals[i];
                if (signal.SourceHash != PlayerInputSignalSourceHash ||
                    !IsNewerInputSequence(signal.Sequence, _lastPlayerInputSignalSequence))
                    continue;

                if (!TryConsumeBuilderInputCommand(signal.Command))
                    continue;

                _lastPlayerInputSignalSequence = signal.Sequence;
            }
        }

        private void BaselineBuilderInputSignalSequence()
        {
            ReadOnlySpan<PlayerInputSignal> signals = SignalBus<PlayerInputSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PlayerInputSignal signal = signals[i];
                if (signal.SourceHash == PlayerInputSignalSourceHash &&
                    IsNewerInputSequence(signal.Sequence, _lastPlayerInputSignalSequence))
                    _lastPlayerInputSignalSequence = signal.Sequence;
            }
        }

        private bool TryConsumeBuilderInputCommand(byte command)
        {
            switch (command)
            {
                case PlayerInputSignalCommands.PrimaryAction:
                    HandlePrimaryAction();
                    return true;
                case PlayerInputSignalCommands.SecondaryAction:
                    HandleSecondaryAction();
                    return true;
                case PlayerInputSignalCommands.Interact:
                    HandleInteract();
                    return true;
                case PlayerInputSignalCommands.TabNext:
                    HandleTabNext();
                    return true;
                case PlayerInputSignalCommands.TabPrevious:
                    HandleTabPrevious();
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsNewerInputSequence(uint candidate, uint current)
        {
            return candidate != 0u && candidate != current && unchecked(candidate - current) < 0x80000000u;
        }

        private void HandlePrimaryAction()
        {
            if (!IsEquipped) return;
            TryPlaceModule();
        }

        private void HandleSecondaryAction()
        {
            if (!IsEquipped) return;

            _ghostYawStep = (_ghostYawStep + (rotationStep >= 0f ? 1 : 3)) & 3;
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
            if (_buildCatalog == null || _buildCatalog.ViewableCount <= 0)
            {
                NotifyBuildBlocked("MODULE CATALOG OFFLINE");
                return;
            }

            int count = _buildCatalog.ViewableCount;
            int startIndex = _buildCatalog.IndexOfViewable(activeBuildable);

            if (startIndex < 0)
                startIndex = direction >= 0 ? -1 : 0;

            for (int step = 1; step <= count; step++)
            {
                int candidateIndex = WrapIndex(startIndex + (step * direction), count);
                BuildableData candidate = _buildCatalog.GetViewableAt(candidateIndex);
                if (candidate == null) continue;

                SetActiveBuildable(candidate);
                _activeBuildableIndex = _buildCatalog.IndexOf(candidate);
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
            CompleteShinobuSocketSnapForTeardown();

            _ghostYawStep        = 0;
            _isSnapped           = false;
            _wasSnapped          = false;
            _snappedSocketTransform = null;
            _snappedSocket       = null;
            _snappedGhostSocket  = null;
            InvalidateShinobuCachedSnapPose();
            _shinobuSocketAdapterCandidateCount = 0;
            _shinobuSocketVaultSceneHash = 0u;
            _shinobuSocketVaultModuleCount = -1;
            _shinobuSocketVaultTargetCount = 0;
            _shinobuSocketVaultTopologyVersion = 0u;
            _shinobuSocketFrameCounter = 0u;
            _shinobuSocketSnapHandle = default;
            _shinobuSocketSnapPending = false;
            _shinobuSocketSnapBestResultIndex = 0;
            _shinobuSocketSnapFrame = 0u;
            _shinobuSocketSnapSceneHash = 0u;
            _shinobuSocketSnapQueryHash = 0u;
            _shinobuSocketSnapGhostRootAup = default;
            _shinobuSocketSnapStartTicks = 0L;
            _integrityPlacementValid = true;
            _integrityValidationDirty = false;
            _integrityPlacementBlockReason = string.Empty;
            _hasScheduledValidationSnapshot = false;
            _hasCompletedValidationSnapshot = false;
            _ghostSocketBuffer.Clear();
            _shinobuTargetSocketBuffer.Clear();
            _currentGhost?.SetExternalValidity(true);
            _builderGhostPreviewActive = false;
            _builderGhostPreviewCanBuild = true;
            _builderGhostPreviewPosition = default;
            _builderGhostPreviewRotation = Quaternion.identity;
            _builderGhostPreviewScale = Vector3.one;
            _habitatConstructionManager?.ResetValidation();
        }

        // ══════════════════════════════════════════════════════════
        //  GHOST MANAGEMENT
        // ══════════════════════════════════════════════════════════

        private void SpawnGhost()
        {
            if (activeBuildable == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[PlayerBuilder] No buildable module assigned!");
#endif
                return;
            }

            ReleaseLegacyGhostObject();
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

            _builderGhostPreviewActive = true;
            _builderGhostPreviewCanBuild = true;
            _builderGhostPreviewPosition = spawnPos;
            _builderGhostPreviewRotation = Quaternion.identity;
            _builderGhostPreviewScale = ResolveActivePreviewScale();
            _currentGhostObj = null;
            _currentGhost = null;
            _currentGhostUsesRuntimeProxy = false;
            _ghostSocketBuffer.Clear();
            _integrityPlacementValid = true;
            _integrityPlacementBlockReason = string.Empty;
            _integrityValidationDirty = true;
            UpdatePlacementValidationState();
        }

        private void DespawnGhost()
        {
            ReleaseLegacyGhostObject();
            _builderGhostPreviewActive = false;
            _builderGhostPreviewCanBuild = true;
            _builderGhostPreviewPosition = default;
            _builderGhostPreviewRotation = Quaternion.identity;
            _builderGhostPreviewScale = Vector3.one;
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

        private void ReleaseLegacyGhostObject()
        {
            if (_currentGhostObj == null)
                return;

            if (_currentGhostUsesRuntimeProxy)
            {
                ConstructionRuntimeProxyFactory.ReleaseGhostProxy(_currentGhostObj);
            }
            else
            {
                ObjectPoolManager pool = GlobalRegistry.ObjectPool;
                if (pool != null)
                    pool.Despawn(_currentGhostObj);
                else
                    UnityEngine.Object.Destroy(_currentGhostObj);
            }

            _currentGhostObj = null;
            _currentGhost    = null;
            _currentGhostUsesRuntimeProxy = false;
        }

        // ══════════════════════════════════════════════════════════
        //  GHOST POSITIONING (v3.0: Socket Snap System)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Obnovlyaet pozitsiyu prizraka kazhdyy kadr.
        ///
        /// v3.0 ALGORITM:
        ///   1. Raycast iz tsentra kamery → hitPoint.
        ///   2. SHINOBU_217 template/AUP socket resolver vokrug hitPoint.
        ///   3. Nayti blizhayshiy svobodnyy ModuleSocket.
        ///   4. GISTEREZIS:
        ///      - Esli NE snapnut i dist ≤ snapRadius → SNAP.
        ///      - Esli snapnut i dist > unsnapRadius → UNSNAP.
        ///      - Mezhdu snapRadius i unsnapRadius → sohranyat tekuschiy status.
        ///   5. Esli SNAP → tselevaya pozitsiya = socket.position,
        ///      tselevaya rotatsiya = socket.rotation × yawOffset.
        ///   6. Esli NE SNAP → obychnoe povedenie (raycast surface).
        ///   7. Plavnaya interpolyatsiya (exp smoothing).
        ///
        /// ZERO GC:
        ///   • SHINOBU_217: socket candidates now resolve through template/AUP math.
        ///   • TryGetComponent → zero GC.
        ///   • Struct Ray, RaycastHit, Vector3, Quaternion — stack.
        ///   • Nikakih List, LINQ, lyambd, new.
        /// </summary>
        private Vector3 ResolveActivePreviewScale()
        {
            if (activeBuildable != null &&
                activeBuildable.ModuleTemplate != null)
            {
                Vector3 size = activeBuildable.ModuleTemplate.ProxyBoundsSize;
                if (size.x > 0.001f && size.y > 0.001f && size.z > 0.001f)
                    return size;
            }

            return Vector3.one;
        }

        private void UpdateGhostPosition(float dt)
        {
            if (_terrainSdfBlockHapticCooldown > 0f)
                _terrainSdfBlockHapticCooldown = math.max(0f, _terrainSdfBlockHapticCooldown - math.max(0f, dt));

            if (playerCamera == null || !_builderGhostPreviewActive || _habitatConstructionManager == null)
                return;

            Ray ray = playerCamera.ViewportPointToRay(ViewportCenter);

            Vector3 targetPos;
            Quaternion targetRot;

            bool rayHit = TryGetBuildHit(ray, ResolveSurfaceMask(), out _hit);

            // ── Tochka lucha (dlya poiska soketov i fallback) ──
            Vector3 rawTargetPoint = rayHit
                ? _hit.point
                : ray.origin + ray.direction * buildDistance;

            float activeGridSize = ResolveActiveGridSize();
            float3 snappedFreePosition = _habitatConstructionManager.SnapWorldPosition(rawTargetPoint, activeGridSize);
            Vector3 freePlacementPosition = new Vector3(snappedFreePosition.x, snappedFreePosition.y, snappedFreePosition.z);
            bool isStructuralPreview = IsStructuralBuildable(activeBuildable);
            float activeSnapRadius = ResolveActiveSnapRadius(isStructuralPreview);
            float activeUnsnapRadius = ResolveActiveUnsnapRadius(isStructuralPreview, activeSnapRadius);
            float activeSnapRadiusSq = activeSnapRadius * activeSnapRadius;
            float activeUnsnapRadiusSq = activeUnsnapRadius * activeUnsnapRadius;
            Quaternion yawRotation = ResolveGhostYawRotation(_ghostYawStep);

            // ═══════════════════════════════════════════════════
            //  SOCKET SEARCH (v3.0)
            //
            //  SHINOBU_217: socket candidates come from template/AUP math.
            //  v radiuse vokrug hitPoint. Pre-allocated buffer → zero GC.
            //
            //  Radius poiska = unsnapRadius (bolshiy iz dvuh) dlya togo,
            //  chtoby poymat soket, ot kotorogo my mogli by otryvatsya.
            //  Fakticheskaya proverka snap/unsnap — po distantsii nizhe.
            // ═══════════════════════════════════════════════════

            bool foundSocket = TryResolveShinobuSocketAlignment(
                rawTargetPoint,
                activeUnsnapRadius,
                out float bestDist,
                out Transform bestTransform,
                out Vector3 bestAlignedPosition,
                out Quaternion bestAlignedRotation);

            // ── Nayti blizhayshiy svobodnyy soket ──
            ModuleSocket bestSocket = null;
            ModuleSocket bestGhostSocket = null;

            if (!foundSocket)
                InvalidateShinobuCachedSnapPose();

            // ═══════════════════════════════════════════════════
            //  SNAP / UNSNAP DECISION (HYSTERESIS)
            //
            //  Dva radiusa predotvraschayut mertsanie:
            //    snapRadius (2m):   hitPoint ≤ 2m ot soketa → SNAP
            //    unsnapRadius (2.5m): hitPoint > 2.5m ot soketa → UNSNAP
            //    Mezhdu 2m i 2.5m: sohranyaem tekuschiy status.
            //
            //  Bez gisterezisa: na rasstoyanii rovno 2m prizrak
            //  kazhdyy kadr snap→unsnap→snap→unsnap (flicker).
            // ═══════════════════════════════════════════════════

            bool previousSnapState = _isSnapped;
            ModuleSocket previousSocket = _snappedSocket;
            ModuleSocket previousGhostSocket = _snappedGhostSocket;

            if (_isSnapped)
            {
                // ── Seychas snapnut: proveryaem uslovie OTRYVA ──
                if (bestTransform == null || bestDist > activeUnsnapRadiusSq)
                {
                    // Otryvaemsya: net soketov poblizosti ILI slishkom daleko
                    _isSnapped = false;
                    _snappedSocketTransform = null;
                    _snappedSocket = null;
                    _snappedGhostSocket = null;
                    InvalidateShinobuCachedSnapPose();
                }
                else
                {
                    // Obnovlyaem: vozmozhno, blizhayshiy soket smenilsya
                    // (igrok navel na drugoy soket togo zhe modulya)
                    _snappedSocketTransform = bestTransform;
                    _snappedSocket = bestSocket;
                    _snappedGhostSocket = bestGhostSocket;
                }
            }
            else
            {
                // ── Seychas NE snapnut: proveryaem uslovie PRILIPANIYa ──
                if (bestTransform != null && bestDist <= activeSnapRadiusSq)
                {
                    _isSnapped = true;
                    _snappedSocketTransform = bestTransform;
                    _snappedSocket = bestSocket;
                    _snappedGhostSocket = bestGhostSocket;
                }
            }

            // ── Zvuk snap/unsnap (edge detection) ──
            if (_isSnapped && !_wasSnapped)
            {
                PlaySound(snapSound);
            }
            _wasSnapped = _isSnapped;

            // ═══════════════════════════════════════════════════
            //  TARGET POSITION / ROTATION
            // ═══════════════════════════════════════════════════

            if (_isSnapped && (_shinobuHasSnappedPose || _snappedSocket != null))
            {
                // ── SNAP MODE: pozitsiya i rotatsiya ot soketa ──
                targetPos = _shinobuHasSnappedPose ? _shinobuSnappedPosePosition : bestAlignedPosition;

                // Socket.forward = napravlenie stykovki.
                // YawOffset pozvolyaet igroku vraschat modul
                // vokrug osi stykovki (esli nuzhno).
                targetRot = _shinobuHasSnappedPose ? _shinobuSnappedPoseRotation : bestAlignedRotation;
            }
            else if (rayHit)
            {
                // ── SURFACE MODE: obychnoe povedenie (raycast) ──
                targetPos = freePlacementPosition;

                if (isStructuralPreview)
                {
                    targetRot = yawRotation;
                }
                else
                {
                    Quaternion surfaceRot = Quaternion.FromToRotation(Vector3.up, _hit.normal);
                    targetRot = surfaceRot * yawRotation;
                }
            }
            else
            {
                // ── FALLBACK: prizrak visit pered kameroy ──
                if (buildAnchor != null)
                {
                    float3 snappedAnchorPosition = _habitatConstructionManager.SnapWorldPosition(buildAnchor.position, activeGridSize);
                    targetPos = new Vector3(snappedAnchorPosition.x, snappedAnchorPosition.y, snappedAnchorPosition.z);
                    targetRot = buildAnchor.rotation * yawRotation;
                }
                else
                {
                    targetPos = freePlacementPosition;
                    targetRot = yawRotation;
                }
            }

            // ═══════════════════════════════════════════════════
            //  SMOOTH INTERPOLATION
            //
            //  Ispolzuem raznuyu skorost dlya snap i non-snap:
            //    Snap: snapSpeed (bystryy, ~20) — "schelk" k pozitsii.
            //    Non-snap: ghostFollowSpeed (plavnyy, ~12) — obychnoe sledovanie.
            //
            //  Cheap cinematic smoothing: x/(1+x) avoids exp() in the placement tick.
            // ═══════════════════════════════════════════════════

            if (isStructuralPreview)
            {
                targetPos = QuantizePosition(targetPos, StructuralPlacementGridMeters);
                targetRot = QuantizeRotation(targetRot, StructuralRotationStepDegrees);
            }

            Vector3 previousPosition = _builderGhostPreviewPosition;
            Quaternion previousRotation = _builderGhostPreviewRotation;

            if (_isSnapped)
            {
                _builderGhostPreviewPosition = targetPos;
                _builderGhostPreviewRotation = targetRot;
            }
            else
            {
                float lerpFactor = ResolveDecayBlend(ghostFollowSpeed, dt);
                _builderGhostPreviewPosition = Vector3.Lerp(previousPosition, targetPos, lerpFactor);
                _builderGhostPreviewRotation = NlerpRotation(previousRotation, targetRot, lerpFactor);
            }

            if (_currentGhostObj != null)
                _currentGhostObj.transform.SetPositionAndRotation(_builderGhostPreviewPosition, _builderGhostPreviewRotation);

            if (previousSnapState != _isSnapped ||
                !ReferenceEquals(previousSocket, _snappedSocket) ||
                !ReferenceEquals(previousGhostSocket, _snappedGhostSocket) ||
                (_builderGhostPreviewPosition - previousPosition).sqrMagnitude > 0.0001f ||
                Quaternion.Dot(previousRotation, _builderGhostPreviewRotation) < 0.9999f)
            {
                _integrityValidationDirty = true;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  MODULE PLACEMENT (v3.0: socket occupied marking)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Popytka ustanovit modul:
        ///   1. Proverka CanBuild (kollizii)
        ///   2. Proverka resursov v inventare
        ///   3. Spisanie resursov
        ///   4. Despavn prizraka → spavn finalnogo modulya
        ///   5. v3.0: esli snapnuty k soketu → pometit ego kak occupied
        ///   6. Peresozdanie prizraka dlya prodolzheniya stroitelstva
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

            if (!IsBuildableBlueprintViewable(activeBuildable))
            {
                NotifyBuildBlocked("BLUEPRINT LOCKED");
                PlaySound(errorSound);
                return;
            }

            if (!_builderGhostPreviewActive)
            {
                NotifyBuildBlocked("PLACEMENT INVALID");
                PlaySound(errorSound);
                return;
            }

            if (!UpdatePlacementValidityState() || !_builderGhostPreviewCanBuild)
            {
                NotifyBuildBlocked(ResolvePlacementBlockReason());
                PlaySound(errorSound);
                return;
            }

            if (!HasResources(activeBuildable))
            {
                NotifyMissingResources(activeBuildable);
                PlaySound(errorSound);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[PlayerBuilder] Missing resources.");
#endif
                return;
            }

            Vector3 placePos = _builderGhostPreviewPosition;
            Quaternion placeRot = _builderGhostPreviewRotation;
            TryResolveExactSnappedPlacementPose(ref placePos, ref placeRot);
            ApplyStructuralPlacementQuantization(ref placePos, ref placeRot);

            if (!TryGetObjectPool(out ObjectPoolManager pool))
            {
                NotifyBuildBlocked("OBJECT POOL OFFLINE");
                PlaySound(errorSound);
                return;
            }

            // ── v3.0: Pometit soket kak zanyatyy ──

            // ── Spavn finalnogo modulya ──
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
                ConstructionManager constructionManager = ResolveCachedConstructionManager();
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
            else if (_isSnapped && _shinobuHasSnappedPose)
            {
                TryMarkShinobuTargetSocketOccupied();
                TryMarkShinobuPlacedGhostSocketOccupied(placedModule);
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
            PublishConstructionCommitSignals(placedModule, activeBuildable);
            PlaySound(buildSound);
            NotifyBuildPlaced(activeBuildable);

            // ── Sbros snap-sostoyaniya ──
            _isSnapped = false;
            _snappedSocketTransform = null;
            _snappedSocket = null;
            _snappedGhostSocket = null;
            InvalidateShinobuCachedSnapPose();
            _shinobuSocketAdapterCandidateCount = 0;

            // ── Peresozdaem prizrak ──
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

        private static bool IsBuildableBlueprintViewable(BuildableData data)
        {
            return data != null && data.IsBlueprintViewable();
        }

        private void ResolveRuntimeReferences()
        {
            LogBuilderDebug("ResolveRuntimeReferences begin");
            if (_habitatConstructionManager == null)
                _habitatConstructionManager = new HabitatConstructionManager();

            ModularBaseConstructionValidator.InitializeVault(GlobalRegistry.DataVault);
            if (_shinobuSocketVault == null)
                _shinobuSocketVault = GlobalRegistry.DataVault;
            EnsureConstructionSignalLanes();

            IPlayerRuntimeContext playerContext = ResolvePlayerContext();
            if (inventory == null && playerContext != null)
                inventory = playerContext.Inventory;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (builderDebugLogging)
                LogBuilderDebug($"ResolveRuntimeReferences inventory={(inventory != null ? "Y" : "N")}");
#endif

            if (playerCamera == null && playerContext != null)
                playerCamera = playerContext.PlayerCamera;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (builderDebugLogging)
                LogBuilderDebug($"ResolveRuntimeReferences camera={(playerCamera != null ? playerCamera.name : "null")}");
#endif

            if (buildAnchor == null && playerContext != null)
                buildAnchor = playerContext.HandAnchor;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (builderDebugLogging)
                LogBuilderDebug($"ResolveRuntimeReferences buildAnchor={(buildAnchor != null ? buildAnchor.name : "null")}");
#endif

            if (hudNotification == null && playerContext != null)
                hudNotification = playerContext.HudNotification;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (builderDebugLogging)
                LogBuilderDebug($"ResolveRuntimeReferences hud={(hudNotification != null ? "Y" : "N")}");
#endif

            if (_buildCatalog == null)
                _buildCatalog = ResolveModuleCatalog();
            if (_cachedConstructionManager == null)
                _cachedConstructionManager = ResolveConstructionManager();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (builderDebugLogging)
                LogBuilderDebug($"ResolveRuntimeReferences catalogCount={(_buildCatalog != null ? _buildCatalog.Count : -1)}");
#endif

            if (activeBuildable == null)
                EnsureCatalogSelection();

            SyncActiveBuildableIndex();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (builderDebugLogging)
                LogBuilderDebug($"ResolveRuntimeReferences end activeIndex={_activeBuildableIndex}");
#endif
        }

        private static void EnsureConstructionSignalLanes()
        {
            if (s_ConstructionSignalLanesInitialized)
                return;

            SignalBus<ConstructionPreviewSignal>.Configure(
                expectedCapacity: 4,
                maxFrameSignals: 8,
                lowTierFrameSignals: 1,
                laneHash: ConstructionPreviewSignal.LaneHash);
            SignalBus<FloraExclusionSignal>.Configure(
                expectedCapacity: 4,
                maxFrameSignals: 8,
                lowTierFrameSignals: 1,
                laneHash: FloraExclusionSignal.LaneHash);
            SignalBus<ConstructionPreviewSignal>.EnsureInitialized();
            SignalBus<FloraExclusionSignal>.EnsureInitialized();
            s_ConstructionSignalLanesInitialized = true;
        }

        private void EnsureCatalogSelection()
        {
            LogBuilderDebug("EnsureCatalogSelection begin");
            if (!autoResolveCatalogSelection) return;
            if (activeBuildable != null && IsBuildableBlueprintViewable(activeBuildable)) return;
            if (_buildCatalog == null || _buildCatalog.ViewableCount <= 0) return;

            activeBuildable = null;
            _activeBuildableIndex = -1;

            int viewableCount = _buildCatalog.ViewableCount;
            for (int i = 0; i < viewableCount; i++)
            {
                BuildableData candidate = _buildCatalog.GetViewableAt(i);
                if (candidate == null) continue;

                activeBuildable = candidate;
                CacheActivePlacementRule();
                _activeBuildableIndex = _buildCatalog.IndexOf(candidate);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (builderDebugLogging)
                    LogBuilderDebug($"EnsureCatalogSelection picked={candidate.moduleName} index={i}");
#endif
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

            _activeBuildableIndex = IsBuildableBlueprintViewable(activeBuildable)
                ? _buildCatalog.IndexOf(activeBuildable)
                : -1;
        }

        private void NotifyBuildableSelection()
        {
            if (activeBuildable == null)
            {
                NotifyBuildBlocked("NO MODULE SELECTED");
                return;
            }

            _builderHudBuffer.Clear();
            AppendText(ref _builderHudBuffer, "BUILDER // ");
            WriteActiveBuildOperationalSummary(ref _builderHudBuffer);
            AppendText(ref _builderHudBuffer, " // ");
            WriteActiveBuildStatusLabel(ref _builderHudBuffer);
            AppendText(ref _builderHudBuffer, " // ");
            WriteActiveCostDigest(ref _builderHudBuffer);
            PublishBuilderInfo();

            _builderLogTitleBuffer.Clear();
            AppendText(ref _builderLogTitleBuffer, "BUILDABLE ARMED - ");
            AppendUpperInvariant(ref _builderLogTitleBuffer, activeBuildable.moduleName);
            _builderLogSummaryBuffer.Clear();
            AppendText(ref _builderLogSummaryBuffer, activeBuildable.FamilyShortCode);
            AppendText(ref _builderLogSummaryBuffer, " // ");
            AppendText(ref _builderLogSummaryBuffer, DescribePowerRole(activeBuildable));
            AppendText(ref _builderLogSummaryBuffer, " // ");
            WriteActiveBuildAdvice(ref _builderLogSummaryBuffer);
            BuildReadiness readiness = ActiveBuildReadiness;
            FieldOperationLogSystem.RecordOperation(
                "BUILDER",
                in _builderLogTitleBuffer,
                in _builderLogSummaryBuffer,
                readiness == BuildReadiness.MissingCost ||
                readiness == BuildReadiness.BlueprintLocked ||
                readiness == BuildReadiness.PlacementBlocked
                    ? "WARN"
                    : "INFO");
        }

        private void NotifyMissingResources(BuildableData data)
        {
            if (data == null)
            {
                NotifyBuildBlocked("MISSING COST");
                return;
            }

            _builderHudBuffer.Clear();
            AppendText(ref _builderHudBuffer, "BUILDER // ");
            AppendUpperInvariant(ref _builderHudBuffer, data.moduleName);
            AppendText(ref _builderHudBuffer, " // ");
            AppendText(ref _builderHudBuffer, data.FamilyShortCode);
            AppendText(ref _builderHudBuffer, " // MISSING COST // ");
            WriteCostDigest(data, ref _builderHudBuffer);
            PublishBuilderWarning();

            _builderLogTitleBuffer.Clear();
            AppendText(ref _builderLogTitleBuffer, "MISSING MATERIALS - ");
            AppendUpperInvariant(ref _builderLogTitleBuffer, data.moduleName);
            _builderLogSummaryBuffer.Clear();
            AppendText(ref _builderLogSummaryBuffer, DescribeBuildPowerRole(data));
            AppendText(ref _builderLogSummaryBuffer, " // Required: ");
            WriteCostDigest(data, ref _builderLogSummaryBuffer);
            FieldOperationLogSystem.RecordOperation(
                "BUILDER",
                in _builderLogTitleBuffer,
                in _builderLogSummaryBuffer,
                "WARN");
        }

        private void NotifyBuildBlocked(string reason)
        {
            if (string.IsNullOrEmpty(reason))
                reason = "BUILD BLOCKED";

            _builderHudBuffer.Clear();
            AppendText(ref _builderHudBuffer, "BUILDER // ");
            if (activeBuildable != null)
            {
                AppendUpperInvariant(ref _builderHudBuffer, activeBuildable.moduleName);
                AppendText(ref _builderHudBuffer, " // ");
                AppendText(ref _builderHudBuffer, activeBuildable.FamilyShortCode);
                AppendText(ref _builderHudBuffer, " // ");
            }

            AppendText(ref _builderHudBuffer, reason);
            PublishBuilderWarning();

            _builderLogTitleBuffer.Clear();
            AppendText(ref _builderLogTitleBuffer, reason);
            _builderLogSummaryBuffer.Clear();
            WriteActiveBuildAdvice(ref _builderLogSummaryBuffer);
            FieldOperationLogSystem.RecordOperation(
                "BUILDER",
                in _builderLogTitleBuffer,
                in _builderLogSummaryBuffer,
                "WARN");
        }

        private void NotifyBuildPlaced(BuildableData data)
        {
            if (data == null) return;

            _builderHudBuffer.Clear();
            AppendText(ref _builderHudBuffer, "BUILDER // ");
            AppendUpperInvariant(ref _builderHudBuffer, data.moduleName);
            AppendText(ref _builderHudBuffer, " DEPLOYED");
            PublishBuilderInfo();

            _builderLogTitleBuffer.Clear();
            AppendText(ref _builderLogTitleBuffer, "MODULE DEPLOYED - ");
            AppendUpperInvariant(ref _builderLogTitleBuffer, data.moduleName);
            _builderLogSummaryBuffer.Clear();
            AppendText(ref _builderLogSummaryBuffer, DescribeBuildPowerRole(data));
            AppendText(ref _builderLogSummaryBuffer, " // ");
            AppendText(ref _builderLogSummaryBuffer, DescribeBuildPurpose(data));
            AppendText(ref _builderLogSummaryBuffer, " ");
            WriteCostDigest(data, ref _builderLogSummaryBuffer);
            AppendText(ref _builderLogSummaryBuffer, " consumed.");
            FieldOperationLogSystem.RecordOperation(
                "BUILDER",
                in _builderLogTitleBuffer,
                in _builderLogSummaryBuffer,
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (builderDebugLogging)
                    LogBuilderDebug($"SpawnPlacedModule begin module={data.moduleName} prefab=RUNTIME_PROXY");
#endif
            }
            else
            {
                if (pool == null)
                    return null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (builderDebugLogging)
                    LogBuilderDebug($"SpawnPlacedModule begin module={data.moduleName} prefab={data.finalPrefab.name}");
#endif
                LogBuilderDebug("SpawnPlacedModule using pool.");
                placedModule = pool.Spawn(data.finalPrefab, placePos, placeRot);
            }

            if (placedModule != null)
            {
                ConstructionManager manager = ResolveCachedConstructionManager();
                if (manager != null)
                {
                    manager.RegisterModule(placedModule, data);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (builderDebugLogging)
                        LogBuilderDebug($"SpawnPlacedModule registered moduleCount={manager.ModuleCount}");
#endif
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (builderDebugLogging)
                LogBuilderDebug($"SpawnPlacedModule end result={(placedModule != null ? placedModule.name : "null")}");
#endif
            return placedModule;
        }

        private int ResolveSurfaceMask()
        {
            return surfaceMask.value != 0
                ? surfaceMask.value
                : HectonLayerMasks.ConstructionSurfaceLayerMask;
        }

        private float ResolveActiveGridSize()
        {
            return IsStructuralBuildable(activeBuildable)
                ? StructuralPlacementGridMeters
                : math.max(0.001f, constructionGridSize);
        }

        private float ResolveActiveSnapRadius(bool isStructuralPreview)
        {
            return isStructuralPreview
                ? StructuralSnapRadiusMeters
                : math.max(0.1f, snapRadius);
        }

        private float ResolveActiveUnsnapRadius(bool isStructuralPreview, float activeSnapRadius)
        {
            float authoredUnsnapRadius = isStructuralPreview
                ? StructuralUnsnapRadiusMeters
                : math.max(activeSnapRadius, unsnapRadius);
            return math.max(activeSnapRadius + 0.01f, authoredUnsnapRadius);
        }

        private int ResolveSocketMask()
        {
            return socketLayerMask.value != 0
                ? socketLayerMask.value
                : HectonLayerMasks.SocketsLayerMask;
        }

        private void ApplyStructuralPlacementQuantization(ref Vector3 position, ref Quaternion rotation)
        {
            if (!IsStructuralBuildable(activeBuildable))
                return;

            position = QuantizePosition(position, StructuralPlacementGridMeters);
            rotation = QuantizeRotation(rotation, StructuralRotationStepDegrees);
        }

        private static Vector3 QuantizePosition(Vector3 position, float gridSize)
        {
            float safeGrid = math.max(0.001f, gridSize);
            float invGrid = math.abs(safeGrid - StructuralPlacementGridMeters) <= 0.0001f
                ? StructuralPlacementGridInv
                : math.rcp(safeGrid);
            float3 snapped = math.floor((float3)position * invGrid + new float3(0.5f)) * safeGrid;
            return new Vector3(snapped.x, snapped.y, snapped.z);
        }

        private static Quaternion QuantizeRotation(Quaternion rotation, float stepDegrees)
        {
            float safeStep = math.max(1f, stepDegrees);
            float invStep = math.rcp(safeStep);
            Vector3 euler = rotation.eulerAngles;
            float3 snapped = math.floor((float3)euler * invStep + new float3(0.5f)) * safeStep;
            return Quaternion.Euler(
                snapped.x,
                snapped.y,
                snapped.z);
        }

        private static Quaternion ResolveGhostYawRotation(int yawStep)
        {
            const float halfSqrt = 0.7071067811865476f;
            switch (yawStep & 3)
            {
                case 1: return new Quaternion(0f, halfSqrt, 0f, halfSqrt);
                case 2: return new Quaternion(0f, 1f, 0f, 0f);
                case 3: return new Quaternion(0f, -halfSqrt, 0f, halfSqrt);
                default: return Quaternion.identity;
            }
        }

        private static Quaternion ResolveShinobuSocketYawRotation(int yawStep)
        {
            const float halfSqrt = 0.7071067811865476f;
            switch (yawStep & 3)
            {
                case 0: return new Quaternion(0f, 1f, 0f, 0f);
                case 1: return new Quaternion(0f, -halfSqrt, 0f, halfSqrt);
                case 2: return Quaternion.identity;
                default: return new Quaternion(0f, halfSqrt, 0f, halfSqrt);
            }
        }

        private static Quaternion NlerpRotation(Quaternion from, Quaternion to, float t)
        {
            if (Quaternion.Dot(from, to) < 0f)
            {
                to.x = -to.x;
                to.y = -to.y;
                to.z = -to.z;
                to.w = -to.w;
            }

            float clampedT = math.saturate(t);
            Quaternion blended = new Quaternion(
                math.lerp(from.x, to.x, clampedT),
                math.lerp(from.y, to.y, clampedT),
                math.lerp(from.z, to.z, clampedT),
                math.lerp(from.w, to.w, clampedT));

            float lengthSq =
                blended.x * blended.x +
                blended.y * blended.y +
                blended.z * blended.z +
                blended.w * blended.w;
            float invLength = math.rsqrt(math.max(lengthSq, 0.00000001f));
            return new Quaternion(
                blended.x * invLength,
                blended.y * invLength,
                blended.z * invLength,
                blended.w * invLength);
        }

        private static bool IsStructuralBuildable(BuildableData data)
        {
            return data != null &&
                   (data.ModuleTemplate != null ||
                    data.family == BuildableFamily.Structure ||
                    data.family == BuildableFamily.Habitat);
        }

        private bool TryResolveExactSnappedPlacementPose(ref Vector3 placePos, ref Quaternion placeRot)
        {
            if (_isSnapped && _shinobuHasSnappedPose)
            {
                placePos = _shinobuSnappedPosePosition;
                placeRot = _shinobuSnappedPoseRotation;
                ApplyStructuralPlacementQuantization(ref placePos, ref placeRot);
                _builderGhostPreviewPosition = placePos;
                _builderGhostPreviewRotation = placeRot;
                if (_currentGhostObj != null)
                    _currentGhostObj.transform.SetPositionAndRotation(placePos, placeRot);
                return true;
            }

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
                    _ghostYawStep,
                    out Vector3 alignedPosition,
                    out Quaternion alignedRotation,
                    out ModuleSocket alignedGhostSocket))
            {
                return false;
            }

            placePos = alignedPosition;
            placeRot = alignedRotation;
            ApplyStructuralPlacementQuantization(ref placePos, ref placeRot);
            _snappedGhostSocket = alignedGhostSocket;
            _builderGhostPreviewPosition = placePos;
            _builderGhostPreviewRotation = placeRot;
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

        private void PublishBuilderInfo()
        {
            if (hudNotification != null)
            {
                hudNotification.ShowInfo(in _builderHudBuffer);
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(_builderHudBuffer.ToString());
#endif
        }

        private void PublishBuilderWarning()
        {
            if (hudNotification != null)
            {
                hudNotification.ShowWarning(in _builderHudBuffer);
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(_builderHudBuffer.ToString());
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogBuilderDebug(string message)
        {
            if (!builderDebugLogging)
                return;

            Debug.Log("[BuilderDebug] " + message);
        }

        private void NotifyModuleDeconstructionQueued(BaseModule module)
        {
            string moduleName = module != null ? module.gameObject.name : "MODULE";
            _builderHudBuffer.Clear();
            AppendText(ref _builderHudBuffer, "BUILDER // ");
            AppendUpperInvariant(ref _builderHudBuffer, moduleName);
            AppendText(ref _builderHudBuffer, " RECOVERY QUEUED");
            PublishBuilderInfo();

            _builderLogTitleBuffer.Clear();
            AppendText(ref _builderLogTitleBuffer, "MODULE RECOVERY QUEUED - ");
            AppendUpperInvariant(ref _builderLogTitleBuffer, moduleName);
            _builderLogSummaryBuffer.Clear();
            AppendText(ref _builderLogSummaryBuffer, "Construction module recovery request was queued for authoritative habitat rollback validation.");
            FieldOperationLogSystem.RecordOperation(
                "BUILDER",
                in _builderLogTitleBuffer,
                in _builderLogSummaryBuffer,
                "INFO");
        }

        private BuildReadiness GetActiveBuildReadiness()
        {
            if (activeBuildable == null)
                return BuildReadiness.NoSelection;

            if (!IsBuildableBlueprintViewable(activeBuildable))
                return BuildReadiness.BlueprintLocked;

            if (inventory == null || playerCamera == null)
                return BuildReadiness.Offline;

            if (!HasResources(activeBuildable))
                return BuildReadiness.MissingCost;

            if (!_builderGhostPreviewActive)
                return BuildReadiness.Ready;

            if (!UpdatePlacementValidityState() || !_builderGhostPreviewCanBuild)
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
            if (_activePlacementRule == null || !_builderGhostPreviewActive)
            {
                _semanticPlacementValid = true;
                _semanticPlacementBlockReason = string.Empty;
                return true;
            }

            _semanticPlacementValid = _activePlacementRule.ValidatePlacement(
                _builderGhostPreviewPosition,
                _builderGhostPreviewRotation,
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

        private bool TryResolveShinobuSocketAlignment(
            Vector3 rawTargetPoint,
            float activeUnsnapRadius,
            out float bestDistanceSq,
            out Transform bestTargetTransform,
            out Vector3 bestAlignedPosition,
            out Quaternion bestAlignedRotation)
        {
            bestDistanceSq = float.MaxValue;
            bestTargetTransform = null;
            bestAlignedPosition = default;
            bestAlignedRotation = default;
            _shinobuSocketAdapterCandidateCount = 0;

            if (!_builderGhostPreviewActive || activeBuildable == null || activeBuildable.ModuleTemplate == null)
                return false;

            BaseModuleTemplate.SocketDefinition[] ghostSockets = activeBuildable.ModuleTemplate.SocketDefinitions;
            if (ghostSockets == null || ghostSockets.Length == 0)
                return false;

            ConstructionManager constructionManager = ResolveCachedConstructionManager();
            IReadOnlyList<GameObject> modules = constructionManager != null ? constructionManager.SpawnedModules : null;
            if (modules == null || modules.Count == 0)
                return false;

            float quality = ShinobuSocketConstructionRuntime.ResolveGlobalQualityWeight();
            ConstructionSocketTuningDTO tuning = ShinobuSocketConstructionRuntime.GetTuning();
            return TryResolveShinobuSocketAlignmentFromVault(
                rawTargetPoint,
                activeUnsnapRadius,
                modules,
                ghostSockets,
                quality,
                tuning,
                out bestDistanceSq,
                out bestTargetTransform,
                out bestAlignedPosition,
                out bestAlignedRotation);
        }

        private bool TryResolveShinobuSocketAlignmentFromVault(
            Vector3 rawTargetPoint,
            float activeUnsnapRadius,
            IReadOnlyList<GameObject> modules,
            BaseModuleTemplate.SocketDefinition[] ghostSockets,
            float quality,
            ConstructionSocketTuningDTO tuning,
            out float bestDistanceSq,
            out Transform bestTargetTransform,
            out Vector3 bestAlignedPosition,
            out Quaternion bestAlignedRotation)
        {
            bestDistanceSq = float.MaxValue;
            bestTargetTransform = null;
            bestAlignedPosition = default;
            bestAlignedRotation = default;

            if (modules == null ||
                ghostSockets == null ||
                ghostSockets.Length == 0 ||
                !TryResolveShinobuSocketVault(out IDataVault vault) ||
                !ShinobuSocketConstructionRuntime.TryResolveVaultViews(vault, out ConstructionSocketVaultViews views))
            {
                return false;
            }

            uint sceneHash = ComputeShinobuSocketSceneHash(modules);
            uint queryHash = ComputeShinobuSocketQueryHash(sceneHash, rawTargetPoint, ghostSockets);
            if ((_shinobuHasSnappedPose || _shinobuSocketCachedBestDistanceSq < float.MaxValue) &&
                (_shinobuSocketSnapSceneHash != sceneHash || _shinobuSocketSnapQueryHash != queryHash))
            {
                InvalidateShinobuCachedSnapPose();
            }

            if (_shinobuSocketSnapPending)
            {
                if (TryFinalizeShinobuSocketSnap(
                        modules,
                        views,
                        ghostSockets,
                        quality,
                        sceneHash,
                        queryHash,
                        out bestDistanceSq,
                        out bestTargetTransform,
                        out bestAlignedPosition,
                        out bestAlignedRotation))
                {
                    return true;
                }

                if (_shinobuSocketSnapPending)
                    return TryUseCachedShinobuSocketSnap(sceneHash, queryHash, out bestDistanceSq, out bestTargetTransform, out bestAlignedPosition, out bestAlignedRotation);
            }

            uint solverFrame = unchecked(++_shinobuSocketFrameCounter);
            if (!TryHydrateShinobuTargetSocketVault(modules, views, quality, tuning, sceneHash, out int targetCount) ||
                targetCount <= 0 ||
                !TryHydrateShinobuGhostSocketVault(rawTargetPoint, ghostSockets, views, quality, tuning, solverFrame, out int ghostCount, out double3 ghostRootAup, out quaternion ghostRootRotation) ||
                ghostCount <= 0 ||
                !views.SnapResults.IsCreated ||
                views.SnapResults.Length <= ghostCount)
            {
                return TryUseCachedShinobuSocketSnap(sceneHash, queryHash, out bestDistanceSq, out bestTargetTransform, out bestAlignedPosition, out bestAlignedRotation);
            }

            ConstructionSocketTuningDTO jobTuning = tuning;
            jobTuning.GlobalQualityWeight = ShinobuSocketConstructionRuntime.SanitizeQuality(quality);
            jobTuning.SearchRadiusLowMeters = math.max(activeUnsnapRadius, tuning.SearchRadiusLowMeters);
            jobTuning.SearchRadiusUltraMeters = math.max(jobTuning.SearchRadiusLowMeters, tuning.SearchRadiusUltraMeters);

            int bestResultIndex = views.SnapResults.Length - 1;
            EvaluateSocketSnappingJob evaluateJob = new EvaluateSocketSnappingJob
            {
                TargetSockets = views.SocketStates,
                TargetSocketAups = views.SocketAups,
                GhostSockets = views.GhostSocketStates,
                GhostSocketAups = views.GhostSocketAups,
                SocketCsrRanges = views.SocketCsrRanges,
                SocketCsrTargetIndices = views.SocketCsrTargetIndices,
                Results = views.SnapResults,
                Tuning = jobTuning,
                GhostRootAup = ghostRootAup,
                RuntimeOriginAup = GlobalSignals.CurrentRuntimeOriginAup().ToAbsoluteDouble3(),
                GhostRootRotation = ghostRootRotation,
                GhostModuleHash = ResolveShinobuModuleHash(activeBuildable),
                TargetCount = targetCount,
                GhostCount = ghostCount,
                SocketCsrRangeOffset = ShinobuSocketConstructionRuntime.SocketDirectionCount
            };
            long solverStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            int batchCount = SystemDispatcher.ResolveInnerloopBatchCount(ghostCount, 1, 16);
            JobHandle evaluateHandle = evaluateJob.Schedule(ghostCount, batchCount);

            SelectBestSocketSnapJob selectJob = new SelectBestSocketSnapJob
            {
                Results = views.SnapResults,
                ResultCount = ghostCount,
                ResultSinkIndex = bestResultIndex
            };
            _shinobuSocketSnapHandle = selectJob.Schedule(evaluateHandle);
            _shinobuSocketSnapPending = true;
            _shinobuSocketSnapBestResultIndex = bestResultIndex;
            _shinobuSocketSnapFrame = solverFrame;
            _shinobuSocketSnapSceneHash = sceneHash;
            _shinobuSocketSnapQueryHash = queryHash;
            _shinobuSocketSnapGhostRootAup = ghostRootAup;
            _shinobuSocketSnapStartTicks = solverStartTicks;
            H8Memory.RegisterActiveJob(SystemID.Construction, _shinobuSocketSnapHandle);

            if (TryFinalizeShinobuSocketSnap(
                    modules,
                    views,
                    ghostSockets,
                    quality,
                    sceneHash,
                    queryHash,
                    out bestDistanceSq,
                    out bestTargetTransform,
                    out bestAlignedPosition,
                    out bestAlignedRotation))
            {
                return true;
            }

            return TryUseCachedShinobuSocketSnap(sceneHash, queryHash, out bestDistanceSq, out bestTargetTransform, out bestAlignedPosition, out bestAlignedRotation);
        }

        private bool TryFinalizeShinobuSocketSnap(
            IReadOnlyList<GameObject> modules,
            ConstructionSocketVaultViews views,
            BaseModuleTemplate.SocketDefinition[] ghostSockets,
            float quality,
            uint sceneHash,
            uint queryHash,
            out float bestDistanceSq,
            out Transform bestTargetTransform,
            out Vector3 bestAlignedPosition,
            out Quaternion bestAlignedRotation)
        {
            bestDistanceSq = float.MaxValue;
            bestTargetTransform = null;
            bestAlignedPosition = default;
            bestAlignedRotation = default;

            if (!_shinobuSocketSnapPending)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _shinobuSocketSnapHandle))
                return false;

            _shinobuSocketSnapPending = false;
            _shinobuSocketSnapHandle = default;

            if (sceneHash != _shinobuSocketSnapSceneHash ||
                queryHash != _shinobuSocketSnapQueryHash ||
                !views.SnapResults.IsCreated ||
                (uint)_shinobuSocketSnapBestResultIndex >= (uint)views.SnapResults.Length)
            {
                return false;
            }

            SocketSnappingResultDTO best = views.SnapResults[_shinobuSocketSnapBestResultIndex];
            _shinobuSocketAdapterCandidateCount = (int)best.EvaluatedCandidates;
            long solverEndTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            float solverMicroseconds = (float)((solverEndTicks - _shinobuSocketSnapStartTicks) * 1000000.0 / System.Diagnostics.Stopwatch.Frequency);
            if (views.Telemetry.IsCreated && views.Telemetry.Length > 0)
            {
                ShinobuSocketConstructionRuntime.WriteTelemetry(
                    views.Telemetry,
                    _shinobuSocketSnapFrame,
                    _shinobuSocketSnapGhostRootAup,
                    (uint)math.max(0, _shinobuSocketVaultTargetCount),
                    best.EvaluatedCandidates,
                    (best.Flags & ConstructionSocketFlags.ValidSnap) != 0u ? 1u : 0u,
                    solverMicroseconds,
                    best.DistanceSq,
                    best.Flags,
                    best.ResultHash,
                    quality,
                    _shinobuSocketVaultTopologyVersion);
            }

            if ((best.Flags & ConstructionSocketFlags.ValidSnap) == 0u ||
                best.TargetSocketIndex < 0 ||
                best.GhostSocketIndex < 0 ||
                best.GhostSocketIndex >= ghostSockets.Length ||
                !math.isfinite(best.DistanceSq))
            {
                InvalidateShinobuCachedSnapPose();
                return false;
            }

            if (!TryApplyShinobuVaultSnapResult(
                modules,
                views,
                ghostSockets,
                in best,
                _shinobuSocketSnapFrame,
                out bestDistanceSq,
                out bestTargetTransform,
                out bestAlignedPosition,
                out bestAlignedRotation))
            {
                InvalidateShinobuCachedSnapPose();
                return false;
            }

            _shinobuSocketCachedBestDistanceSq = best.DistanceSq;
            return true;
        }

        private bool TryUseCachedShinobuSocketSnap(
            uint sceneHash,
            uint queryHash,
            out float bestDistanceSq,
            out Transform bestTargetTransform,
            out Vector3 bestAlignedPosition,
            out Quaternion bestAlignedRotation)
        {
            bestDistanceSq = float.MaxValue;
            bestTargetTransform = null;
            bestAlignedPosition = default;
            bestAlignedRotation = default;

            if (!_shinobuHasSnappedPose ||
                _shinobuSnappedTargetTransform == null ||
                _shinobuSocketSnapSceneHash != sceneHash ||
                _shinobuSocketSnapQueryHash != queryHash ||
                !math.isfinite(_shinobuSocketCachedBestDistanceSq) ||
                _shinobuSocketCachedBestDistanceSq >= float.MaxValue)
            {
                return false;
            }

            bestDistanceSq = _shinobuSocketCachedBestDistanceSq;
            bestTargetTransform = _shinobuSnappedTargetTransform;
            bestAlignedPosition = _shinobuSnappedPosePosition;
            bestAlignedRotation = _shinobuSnappedPoseRotation;
            return true;
        }

        private void InvalidateShinobuCachedSnapPose()
        {
            _shinobuHasSnappedPose = false;
            _shinobuSnappedTargetTransform = null;
            _shinobuSnappedTargetLocalPosition = default;
            _shinobuSnappedTargetDirection = ModuleSocketDirection.North;
            _shinobuSnappedTargetCompatibilityHash = 0u;
            _shinobuSnappedGhostSocketIndex = -1;
            _shinobuDearLieDampen = 0f;
            _shinobuSocketCachedBestDistanceSq = float.MaxValue;
        }

        private void CompleteShinobuSocketSnapForTeardown()
        {
            if (!_shinobuSocketSnapPending)
                return;

            DispatcherJobFence.TryComplete(ref _shinobuSocketSnapHandle, forceComplete: true);
            _shinobuSocketSnapHandle = default;
            _shinobuSocketSnapPending = false;
        }

        private bool TryResolveShinobuSocketVault(out IDataVault vault)
        {
            vault = _shinobuSocketVault;
            return vault != null;
        }

        private bool TryHydrateShinobuTargetSocketVault(
            IReadOnlyList<GameObject> modules,
            ConstructionSocketVaultViews views,
            float quality,
            ConstructionSocketTuningDTO tuning,
            uint sceneHash,
            out int targetSocketCount)
        {
            targetSocketCount = 0;
            if (modules == null ||
                !views.SocketStates.IsCreated ||
                !views.SocketAups.IsCreated ||
                !views.Modules.IsCreated ||
                !views.Counters.IsCreated ||
                views.Counters.Length < 4)
            {
                return false;
            }

            int moduleCount = modules.Count;
            if (moduleCount == _shinobuSocketVaultModuleCount &&
                _shinobuSocketVaultTargetCount > 0 &&
                sceneHash == _shinobuSocketVaultSceneHash)
            {
                targetSocketCount = _shinobuSocketVaultTargetCount;
                return BuildShinobuSocketCsrIndex(views, targetSocketCount);
            }

            int moduleWrite = 0;
            int socketWrite = 0;
            int socketCapacity = math.min(views.SocketStates.Length, views.SocketAups.Length);
            for (int sceneIndex = 0; sceneIndex < modules.Count && moduleWrite < views.Modules.Length && socketWrite < socketCapacity; sceneIndex++)
            {
                GameObject moduleObject = modules[sceneIndex];
                if (moduleObject == null ||
                    !moduleObject.TryGetComponent(out ModuleMarker marker) ||
                    marker == null ||
                    marker.Data == null ||
                    marker.Data.ModuleTemplate == null)
                {
                    continue;
                }

                BaseModuleTemplate template = marker.Data.ModuleTemplate;
                BaseModuleTemplate.SocketDefinition[] sockets = template.SocketDefinitions;
                if (sockets == null || sockets.Length == 0)
                    continue;

                Transform moduleTransform = moduleObject.transform;
                Vector3 position = moduleTransform.position;
                Quaternion rotation = moduleTransform.rotation;
                quaternion moduleRotation = new quaternion(rotation.x, rotation.y, rotation.z, rotation.w);
                if (!TryResolveConstructionPivotAup(position, out double3 rootAup))
                    continue;
                int socketStart = socketWrite;
                uint moduleHash = ResolveShinobuModuleHash(marker.Data);
                _shinobuTargetSocketBuffer.Clear();
                moduleObject.GetComponentsInChildren<ModuleSocket>(true, _shinobuTargetSocketBuffer);

                for (int socketIndex = 0; socketIndex < sockets.Length && socketWrite < socketCapacity; socketIndex++)
                {
                    BaseModuleTemplate.SocketDefinition definition = sockets[socketIndex];
                    byte direction = (byte)definition.Direction;
                    bool directionValid = ShinobuSocketConstructionRuntime.IsDirectionValid(direction);
                    float3 local = new float3(definition.LocalPosition.x, definition.LocalPosition.y, definition.LocalPosition.z);
                    float3 normal = directionValid
                        ? ShinobuSocketConstructionRuntime.DirectionToNormal(direction)
                        : float3.zero;
                    float3 rotatedNormal = math.rotate(moduleRotation, normal);
                    float3 rotatedOffset = math.rotate(moduleRotation, local);
                    bool finiteSocket = directionValid &&
                                        math.all(math.isfinite(local)) &&
                                        math.all(math.isfinite(rotatedNormal)) &&
                                        math.all(math.isfinite(rotatedOffset));

                    SocketStateDTO socket;
                    socket.LocalOffset = new double3(local.x, local.y, local.z);
                    socket.NormalDirection = finiteSocket ? rotatedNormal : float3.zero;
                    socket.AllowedConnectionBitmask = ShinobuSocketConstructionRuntime.PackAllowedConnectionBitmask(
                        direction,
                        ShinobuSocketConstructionRuntime.HashCompatibility(definition.CompatibleType));
                    socket.ParentModuleHash = moduleHash;
                    socket.ConnectionStatus = finiteSocket
                        ? IsShinobuAuthoredSocketOccupied(moduleTransform, definition, _shinobuTargetSocketBuffer)
                            ? ConstructionSocketFlags.Connected
                            : 0u
                        : ConstructionSocketFlags.NonFinite | ConstructionSocketFlags.CollisionBlocked;
                    socket._pad0 = 0u;
                    socket._pad1 = 0u;
                    socket._pad2 = 0u;
                    socket._pad3 = 0u;
                    views.SocketStates[socketWrite] = socket;
                    views.SocketAups[socketWrite] = rootAup + new double3(rotatedOffset.x, rotatedOffset.y, rotatedOffset.z);
                    socketWrite++;
                }
                _shinobuTargetSocketBuffer.Clear();

                ConstructionSocketModuleDTO module;
                module.RootAup = rootAup;
                module.Rotation = moduleRotation;
                module.BoundsCenter = new float3(template.ProxyBoundsCenter.x, template.ProxyBoundsCenter.y, template.ProxyBoundsCenter.z);
                module.BoundsExtents = new float3(
                    math.max(0.001f, template.ProxyBoundsSize.x * 0.5f),
                    math.max(0.001f, template.ProxyBoundsSize.y * 0.5f),
                    math.max(0.001f, template.ProxyBoundsSize.z * 0.5f));
                module.ModuleHash = moduleHash;
                module.SocketStart = socketStart;
                module.SocketCount = socketWrite - socketStart;
                module.Flags = 0u;
                module.TopologyVersion = _shinobuSocketVaultTopologyVersion;
                module.DearLieDampen = 0f;
                module.ConnectedMask = 0u;
                module.SceneModuleListIndex = sceneIndex;
                views.Modules[moduleWrite++] = module;
            }

            _shinobuSocketVaultTopologyVersion = unchecked(_shinobuSocketVaultTopologyVersion + 1u);
            _shinobuSocketVaultSceneHash = sceneHash;
            _shinobuSocketVaultModuleCount = moduleCount;
            _shinobuSocketVaultTargetCount = socketWrite;
            views.Counters[0] = moduleWrite;
            views.Counters[1] = socketWrite;
            views.Counters[2] = unchecked((int)_shinobuSocketVaultTopologyVersion);
            views.Counters[3] = (int)ConstructionSocketFlags.TopologyDirty;
            if (!BuildShinobuSocketCsrIndex(views, socketWrite))
                return false;

            if (views.Tuning.IsCreated && views.Tuning.Length > 0)
            {
                tuning.GlobalQualityWeight = ShinobuSocketConstructionRuntime.SanitizeQuality(quality);
                views.Tuning[0] = tuning;
            }

            targetSocketCount = socketWrite;
            return socketWrite > 0;
        }

        private bool TryHydrateShinobuGhostSocketVault(
            Vector3 rawTargetPoint,
            BaseModuleTemplate.SocketDefinition[] ghostSockets,
            ConstructionSocketVaultViews views,
            float quality,
            ConstructionSocketTuningDTO tuning,
            uint solverFrame,
            out int ghostSocketCount,
            out double3 ghostRootAup,
            out quaternion ghostRootRotation)
        {
            ghostSocketCount = 0;
            if (!TryResolveConstructionPivotAup(rawTargetPoint, out ghostRootAup))
            {
                ghostRootRotation = quaternion.identity;
                return false;
            }

            Quaternion yawRotation = ResolveShinobuSocketYawRotation(_ghostYawStep);
            ghostRootRotation = new quaternion(yawRotation.x, yawRotation.y, yawRotation.z, yawRotation.w);
            if (ghostSockets == null ||
                !views.GhostSocketStates.IsCreated ||
                !views.GhostSocketAups.IsCreated ||
                !views.SnapResults.IsCreated ||
                views.SnapResults.Length <= 1)
            {
                return false;
            }

            uint moduleHash = ResolveShinobuModuleHash(activeBuildable);
            int resultSlotCapacity = math.max(0, views.SnapResults.Length - 1);
            int count = math.min(
                ghostSockets.Length,
                math.min(resultSlotCapacity, math.min(views.GhostSocketStates.Length, views.GhostSocketAups.Length)));
            for (int i = 0; i < count; i++)
            {
                BaseModuleTemplate.SocketDefinition definition = ghostSockets[i];
                byte direction = (byte)definition.Direction;
                bool directionValid = ShinobuSocketConstructionRuntime.IsDirectionValid(direction);
                float3 local = new float3(definition.LocalPosition.x, definition.LocalPosition.y, definition.LocalPosition.z);
                float3 normal = directionValid
                    ? ShinobuSocketConstructionRuntime.DirectionToNormal(direction)
                    : float3.zero;
                float3 rotatedNormal = math.rotate(ghostRootRotation, normal);
                float3 rotatedOffset = math.rotate(ghostRootRotation, local);
                bool finiteSocket = directionValid &&
                                    math.all(math.isfinite(local)) &&
                                    math.all(math.isfinite(rotatedNormal)) &&
                                    math.all(math.isfinite(rotatedOffset));
                SocketStateDTO socket;
                socket.LocalOffset = new double3(local.x, local.y, local.z);
                socket.NormalDirection = finiteSocket ? rotatedNormal : float3.zero;
                socket.AllowedConnectionBitmask = ShinobuSocketConstructionRuntime.PackAllowedConnectionBitmask(
                    direction,
                    ShinobuSocketConstructionRuntime.HashCompatibility(definition.CompatibleType));
                socket.ParentModuleHash = moduleHash;
                socket.ConnectionStatus = finiteSocket
                    ? 0u
                    : ConstructionSocketFlags.NonFinite | ConstructionSocketFlags.CollisionBlocked;
                socket._pad0 = 0u;
                socket._pad1 = 0u;
                socket._pad2 = 0u;
                socket._pad3 = 0u;
                views.GhostSocketStates[i] = socket;
                views.GhostSocketAups[i] = (socket.ConnectionStatus & ConstructionSocketFlags.NonFinite) == 0u
                    ? ghostRootAup + new double3(rotatedOffset.x, rotatedOffset.y, rotatedOffset.z)
                    : ghostRootAup;
                if (views.SocketCsrRanges.IsCreated &&
                    (uint)(ShinobuSocketConstructionRuntime.SocketDirectionCount + i) < (uint)views.SocketCsrRanges.Length)
                {
                    byte targetDirection = ShinobuSocketConstructionRuntime.InvertDirection(ShinobuSocketConstructionRuntime.ExtractDirection(socket));
                    views.SocketCsrRanges[ShinobuSocketConstructionRuntime.SocketDirectionCount + i] =
                        (socket.ConnectionStatus & (ConstructionSocketFlags.NonFinite | ConstructionSocketFlags.CollisionBlocked)) == 0u &&
                        ShinobuSocketConstructionRuntime.IsDirectionValid(targetDirection)
                            ? views.SocketCsrRanges[targetDirection]
                            : new int2(0, 0);
                }

                ghostSocketCount++;
            }

            if (views.GhostPreviews.IsCreated && views.GhostPreviews.Length > 0)
            {
                Vector3 boundsScale = Vector3.one;
                if (activeBuildable != null && activeBuildable.ModuleTemplate != null)
                    boundsScale = activeBuildable.ModuleTemplate.ProxyBoundsSize;

                GhostPreviewDTO preview;
                preview.CenterAup = ghostRootAup;
                preview.Rotation = ghostRootRotation;
                preview.BoundsScale = new float3(
                    math.max(0.001f, boundsScale.x),
                    math.max(0.001f, boundsScale.y),
                    math.max(0.001f, boundsScale.z));
                preview.SnappingRadius = math.max(0.001f, tuning.SnappingRadius);
                preview.ModuleHash = moduleHash;
                preview.SocketStart = 0;
                preview.SocketCount = ghostSocketCount;
                preview.DearLieDampen = _shinobuDearLieDampen;
                preview.GlobalQualityWeight = ShinobuSocketConstructionRuntime.SanitizeQuality(quality);
                preview.Flags = _shinobuHasSnappedPose ? ConstructionSocketFlags.DearLieActive : 0u;
                preview.BoundsCenter = float3.zero;
                preview.Frame = solverFrame;
                views.GhostPreviews[0] = preview;
            }

            if (views.Tuning.IsCreated && views.Tuning.Length > 0)
            {
                tuning.GlobalQualityWeight = ShinobuSocketConstructionRuntime.SanitizeQuality(quality);
                views.Tuning[0] = tuning;
            }

            return ghostSocketCount > 0;
        }

        private static bool IsShinobuAuthoredSocketOccupied(
            Transform moduleTransform,
            BaseModuleTemplate.SocketDefinition definition,
            List<ModuleSocket> authoredSockets)
        {
            if (moduleTransform == null || authoredSockets == null || authoredSockets.Count == 0)
                return false;

            uint definitionCompatibility = ShinobuSocketConstructionRuntime.HashCompatibility(definition.CompatibleType);
            Vector3 definitionLocal = definition.LocalPosition;
            for (int i = 0; i < authoredSockets.Count; i++)
            {
                ModuleSocket socket = authoredSockets[i];
                if (socket == null || !socket.IsOccupied || socket.Direction != definition.Direction)
                    continue;

                uint socketCompatibility = ShinobuSocketConstructionRuntime.HashCompatibility(socket.CompatibleType);
                if (!ShinobuSocketConstructionRuntime.AreCompatibilityHashesCompatible(definitionCompatibility, socketCompatibility))
                    continue;

                Vector3 socketLocal = moduleTransform.InverseTransformPoint(socket.transform.position);
                Vector3 delta = socketLocal - definitionLocal;
                if (delta.sqrMagnitude <= 0.0004f)
                    return true;
            }

            return false;
        }

        private static bool BuildShinobuSocketCsrIndex(ConstructionSocketVaultViews views, int targetSocketCount)
        {
            return ShinobuSocketConstructionRuntime.BuildSocketDirectionCsr(
                views.SocketStates,
                targetSocketCount,
                views.SocketCsrRanges,
                views.SocketCsrTargetIndices);
        }

        private bool TryApplyShinobuVaultSnapResult(
            IReadOnlyList<GameObject> modules,
            ConstructionSocketVaultViews views,
            BaseModuleTemplate.SocketDefinition[] ghostSockets,
            in SocketSnappingResultDTO best,
            uint solverFrame,
            out float bestDistanceSq,
            out Transform bestTargetTransform,
            out Vector3 bestAlignedPosition,
            out Quaternion bestAlignedRotation)
        {
            bestDistanceSq = best.DistanceSq;
            bestTargetTransform = null;
            bestAlignedPosition = default;
            bestAlignedRotation = default;
            if (!views.SocketStates.IsCreated ||
                !views.SocketAups.IsCreated ||
                !views.Modules.IsCreated ||
                (uint)best.TargetSocketIndex >= (uint)views.SocketStates.Length ||
                (uint)best.TargetSocketIndex >= (uint)views.SocketAups.Length ||
                (uint)best.GhostSocketIndex >= (uint)ghostSockets.Length)
            {
                return false;
            }

            int moduleCount = views.Counters.IsCreated && views.Counters.Length > 0
                ? math.clamp(views.Counters[0], 0, views.Modules.Length)
                : 0;
            int sceneIndex = ResolveShinobuSceneModuleIndexForSocket(views.Modules, best.TargetSocketIndex, moduleCount);
            if (modules == null || (uint)sceneIndex >= (uint)modules.Count || modules[sceneIndex] == null)
                return false;

            Transform targetTransform = modules[sceneIndex].transform;
            SocketStateDTO targetSocket = views.SocketStates[best.TargetSocketIndex];
            BaseModuleTemplate.SocketDefinition ghostSocket = ghostSockets[best.GhostSocketIndex];
            byte targetDirectionByte = ShinobuSocketConstructionRuntime.ExtractDirection(targetSocket);
            if (!TryToShinobuSocketDirection(targetDirectionByte, out ModuleSocketDirection targetDirection) ||
                !ShinobuSocketConstructionRuntime.IsDirectionValid((byte)ghostSocket.Direction))
            {
                return false;
            }

            Quaternion targetSocketRotation = targetTransform.rotation * ModuleSocketTopology.RotationFromDirection(targetDirection);
            Quaternion desiredSocketRotation = targetSocketRotation * ResolveShinobuSocketYawRotation(_ghostYawStep);
            Quaternion ghostLocalRotation = ModuleSocketTopology.RotationFromDirection(ghostSocket.Direction);
            Quaternion candidateRotation = desiredSocketRotation * Quaternion.Inverse(ghostLocalRotation);
            Vector3 rotatedLocalOffset = candidateRotation * ghostSocket.LocalPosition;
            double3 candidateRootAup = views.SocketAups[best.TargetSocketIndex] - new double3(rotatedLocalOffset.x, rotatedLocalOffset.y, rotatedLocalOffset.z);
            double3 runtimeOriginAup = GlobalSignals.CurrentRuntimeOriginAup().ToAbsoluteDouble3();
            double3 candidateRuntimeDouble = candidateRootAup - runtimeOriginAup;
            if (!math.all(math.isfinite(candidateRootAup)) ||
                !math.all(math.isfinite(candidateRuntimeDouble)) ||
                math.any(math.abs(candidateRuntimeDouble) > (double)float.MaxValue))
            {
                return false;
            }

            bestTargetTransform = targetTransform;
            bestAlignedPosition = new Vector3(
                (float)candidateRuntimeDouble.x,
                (float)candidateRuntimeDouble.y,
                (float)candidateRuntimeDouble.z);
            bestAlignedRotation = candidateRotation;
            _shinobuHasSnappedPose = true;
            _shinobuSnappedPosePosition = bestAlignedPosition;
            _shinobuSnappedPoseRotation = bestAlignedRotation;
            _shinobuSnappedTargetTransform = bestTargetTransform;
            _shinobuSnappedGhostSocketIndex = best.GhostSocketIndex;
            _shinobuSnappedTargetLocalPosition = new Vector3(
                (float)targetSocket.LocalOffset.x,
                (float)targetSocket.LocalOffset.y,
                (float)targetSocket.LocalOffset.z);
            _shinobuSnappedTargetDirection = targetDirection;
            _shinobuSnappedTargetCompatibilityHash = ShinobuSocketConstructionRuntime.ExtractCompatibilityHash24(targetSocket);
            _shinobuDearLieDampen = best.DearLieDampen;
            if (views.GhostPreviews.IsCreated && views.GhostPreviews.Length > 0)
            {
                GhostPreviewDTO preview = views.GhostPreviews[0];
                preview.CenterAup = candidateRootAup;
                preview.Rotation = new quaternion(candidateRotation.x, candidateRotation.y, candidateRotation.z, candidateRotation.w);
                preview.DearLieDampen = best.DearLieDampen;
                preview.Flags |= ConstructionSocketFlags.ValidSnap | ConstructionSocketFlags.DearLieActive;
                preview.Frame = solverFrame;
                views.GhostPreviews[0] = preview;
            }

            return true;
        }

        private static int ResolveShinobuSceneModuleIndexForSocket(NativeArray<ConstructionSocketModuleDTO> modules, int socketIndex, int moduleCount)
        {
            if (!modules.IsCreated || socketIndex < 0 || moduleCount <= 0)
                return -1;

            int count = math.min(moduleCount, modules.Length);
            for (int i = 0; i < count; i++)
            {
                ConstructionSocketModuleDTO module = modules[i];
                if (module.SocketCount <= 0 || socketIndex < module.SocketStart || socketIndex >= module.SocketStart + module.SocketCount)
                    continue;

                return module.SceneModuleListIndex;
            }

            return -1;
        }

        private static uint ComputeShinobuSocketSceneHash(IReadOnlyList<GameObject> modules)
        {
            uint hash = 2166136261u;
            if (modules == null)
                return hash;

            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, (uint)modules.Count);
            for (int i = 0; i < modules.Count; i++)
            {
                GameObject moduleObject = modules[i];
                if (moduleObject == null)
                    continue;

                Transform tr = moduleObject.transform;
                Vector3 p = tr.position;
                Quaternion r = tr.rotation;
                hash = ShinobuSocketConstructionRuntime.FoldHash(hash, unchecked((uint)moduleObject.GetInstanceID()));
                hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(p.x));
                hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(p.y));
                hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(p.z));
                hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(r.x));
                hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(r.y));
                hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(r.z));
                hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(r.w));
            }

            return hash;
        }

        private uint ComputeShinobuSocketQueryHash(
            uint sceneHash,
            Vector3 rawTargetPoint,
            BaseModuleTemplate.SocketDefinition[] ghostSockets)
        {
            uint hash = ShinobuSocketConstructionRuntime.FoldHash(2166136261u, sceneHash);
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(rawTargetPoint.x));
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(rawTargetPoint.y));
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(rawTargetPoint.z));
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, unchecked((uint)_ghostYawStep));
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, ResolveShinobuModuleHash(activeBuildable));
            int count = ghostSockets != null ? ghostSockets.Length : 0;
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, (uint)count);
            for (int i = 0; i < count; i++)
            {
                BaseModuleTemplate.SocketDefinition socket = ghostSockets[i];
                Vector3 local = socket.LocalPosition;
                hash = ShinobuSocketConstructionRuntime.FoldHash(hash, (uint)(byte)socket.Direction);
                hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(local.x));
                hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(local.y));
                hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(local.z));
                hash = ShinobuSocketConstructionRuntime.FoldHash(hash, ShinobuSocketConstructionRuntime.HashCompatibility(socket.CompatibleType));
            }

            return hash;
        }

        private static uint ResolveShinobuModuleHash(BuildableData data)
        {
            if (data == null)
                return 0u;

            uint hash = unchecked((uint)data.ModuleHashId);
            if (hash == 0u && data.ModuleTemplate != null)
                hash = unchecked((uint)data.ModuleTemplate.TemplateHashId);
            return hash;
        }

        private static bool TryToShinobuSocketDirection(byte direction, out ModuleSocketDirection result)
        {
            switch (direction)
            {
                case 0:
                    result = ModuleSocketDirection.North;
                    return true;
                case 1:
                    result = ModuleSocketDirection.South;
                    return true;
                case 2:
                    result = ModuleSocketDirection.East;
                    return true;
                case 3:
                    result = ModuleSocketDirection.West;
                    return true;
                case 4:
                    result = ModuleSocketDirection.Top;
                    return true;
                case 5:
                    result = ModuleSocketDirection.Bottom;
                    return true;
                default:
                    result = ModuleSocketDirection.North;
                    return false;
            }
        }

        private bool TryMarkShinobuTargetSocketOccupied()
        {
            if (_shinobuSnappedTargetTransform == null)
                return false;

            _shinobuTargetSocketBuffer.Clear();
            _shinobuSnappedTargetTransform.GetComponentsInChildren<ModuleSocket>(true, _shinobuTargetSocketBuffer);
            for (int i = 0; i < _shinobuTargetSocketBuffer.Count; i++)
            {
                ModuleSocket socket = _shinobuTargetSocketBuffer[i];
                if (socket == null || socket.IsOccupied || socket.Direction != _shinobuSnappedTargetDirection)
                    continue;

                uint compatibilityHash = ShinobuSocketConstructionRuntime.HashCompatibility(socket.CompatibleType);
                if (!ShinobuSocketConstructionRuntime.AreCompatibilityHashesCompatible(compatibilityHash, _shinobuSnappedTargetCompatibilityHash))
                    continue;

                Vector3 localPosition = _shinobuSnappedTargetTransform.InverseTransformPoint(socket.transform.position);
                if ((localPosition - _shinobuSnappedTargetLocalPosition).sqrMagnitude > 0.0004f)
                    continue;

                socket.SetOccupied(true);
                _shinobuTargetSocketBuffer.Clear();
                return true;
            }

            _shinobuTargetSocketBuffer.Clear();
            return false;
        }

        private bool TryMarkShinobuPlacedGhostSocketOccupied(GameObject placedModule)
        {
            if (placedModule == null ||
                activeBuildable == null ||
                activeBuildable.ModuleTemplate == null ||
                activeBuildable.ModuleTemplate.SocketDefinitions == null ||
                (uint)_shinobuSnappedGhostSocketIndex >= (uint)activeBuildable.ModuleTemplate.SocketDefinitions.Length)
            {
                return false;
            }

            Transform placedTransform = placedModule.transform;
            BaseModuleTemplate.SocketDefinition definition = activeBuildable.ModuleTemplate.SocketDefinitions[_shinobuSnappedGhostSocketIndex];
            _shinobuTargetSocketBuffer.Clear();
            placedModule.GetComponentsInChildren<ModuleSocket>(true, _shinobuTargetSocketBuffer);
            bool marked = TryMarkShinobuAuthoredSocketOccupied(placedTransform, definition, _shinobuTargetSocketBuffer);
            _shinobuTargetSocketBuffer.Clear();
            return marked;
        }

        private static bool TryMarkShinobuAuthoredSocketOccupied(
            Transform moduleTransform,
            BaseModuleTemplate.SocketDefinition definition,
            List<ModuleSocket> authoredSockets)
        {
            if (moduleTransform == null || authoredSockets == null || authoredSockets.Count == 0)
                return false;

            uint definitionCompatibility = ShinobuSocketConstructionRuntime.HashCompatibility(definition.CompatibleType);
            Vector3 definitionLocal = definition.LocalPosition;
            for (int i = 0; i < authoredSockets.Count; i++)
            {
                ModuleSocket socket = authoredSockets[i];
                if (socket == null || socket.Direction != definition.Direction)
                    continue;

                uint socketCompatibility = ShinobuSocketConstructionRuntime.HashCompatibility(socket.CompatibleType);
                if (!ShinobuSocketConstructionRuntime.AreCompatibilityHashesCompatible(definitionCompatibility, socketCompatibility))
                    continue;

                Vector3 socketLocal = moduleTransform.InverseTransformPoint(socket.transform.position);
                Vector3 delta = socketLocal - definitionLocal;
                if (delta.sqrMagnitude > 0.0004f)
                    continue;

                socket.SetOccupied(true);
                return true;
            }

            return false;
        }

        private bool UpdatePlacementValidityState()
        {
            bool semanticValid = UpdateSemanticPlacementState();
            bool terrainValid = UpdateTerrainSdfPlacementState();
            bool finalValid = semanticValid && terrainValid && _integrityPlacementValid;
            _builderGhostPreviewCanBuild = finalValid;

            if (_currentGhost != null)
                _currentGhost.SetExternalValidity(finalValid);

            return finalValid;
        }

        private bool UpdateTerrainSdfPlacementState()
        {
            _terrainSdfPlacementValid = true;
            _terrainSdfPlacementBlockReason = string.Empty;
            if (!IsStructuralBuildable(activeBuildable) || !_builderGhostPreviewActive || activeBuildable.ModuleTemplate == null)
                return AcceptTerrainSdfPlacement();

            BaseModuleTemplate template = activeBuildable.ModuleTemplate;
            Vector3 proxyBoundsSize = template.ProxyBoundsSize;
            if (proxyBoundsSize.x <= 0.01f || proxyBoundsSize.y <= 0.01f || proxyBoundsSize.z <= 0.01f)
                return AcceptTerrainSdfPlacement();

            if (!TryBuildConstructionValidationPayload(
                    template,
                    _builderGhostPreviewPosition,
                    _builderGhostPreviewRotation,
                    out ConstructionRequestDTO request,
                    out StructuralBoundsDTO bounds,
                    out ConstructionValidationSettingsDTO settings,
                    out ConstructionSipBudgetDTO sipBudget,
                    out ConstructionMockWorldSampler worldSampler))
            {
                _terrainSdfPlacementValid = false;
                _terrainSdfPlacementBlockReason = "PLACEMENT NAN";
                TryQueueTerrainSdfBlockHaptic();
                _terrainSdfWasBlocked = true;
                return false;
            }

            ConstructionValidationResultDTO result = ModularBaseConstructionValidator.ValidatePlacementNoOccupancy(
                in request,
                in bounds,
                in settings,
                in worldSampler,
                in sipBudget);

            if (TryFindOccupiedConstructionGridCell(
                    in request,
                    in settings,
                    out int occupiedCellHash))
            {
                ModularBaseConstructionValidator.ApplyFailureFlags(
                    ref result,
                    in request,
                    (uint)ConstructionValidationFlags.OccupiedGridCell,
                    result.MinSdfDistance,
                    occupiedCellHash);
            }

            if (TryFindVoxelSdfIntersection(
                    in request,
                    in bounds,
                    in settings,
                    out float voxelDensity))
            {
                ModularBaseConstructionValidator.ApplyFailureFlags(
                    ref result,
                    in request,
                    (uint)ConstructionValidationFlags.TerrainIntersection,
                    -math.abs(voxelDensity),
                    result.OccupiedCellHash);
            }

            _lastConstructionValidationRequest = request;
            _lastConstructionValidationBounds = bounds;
            _lastConstructionValidationSettings = settings;
            _lastConstructionValidationResult = result;
            _lastConstructionWorldSampler = worldSampler;

            IDataVault telemetryVault = _shinobuSocketVault;
            if (ModularBaseConstructionValidator.TryResolveTelemetryRing(telemetryVault, out var telemetryRing))
            {
                ModularBaseConstructionValidator.WriteTelemetry(
                    telemetryRing,
                    (uint)Time.frameCount,
                    in request,
                    in result,
                    0f,
                    0u);
            }

            if ((result.FailureFlags & (uint)ConstructionValidationFlags.NonFiniteInput) != 0u)
            {
                _terrainSdfPlacementValid = false;
                _terrainSdfPlacementBlockReason = "PLACEMENT NAN";
                TryQueueTerrainSdfBlockHaptic();
                _terrainSdfWasBlocked = true;
                return false;
            }

            if ((result.FailureFlags & (uint)ConstructionValidationFlags.OutsideBounds) != 0u)
            {
                _terrainSdfPlacementValid = false;
                _terrainSdfPlacementBlockReason = "GRID LIMIT";
                TryQueueTerrainSdfBlockHaptic();
                _terrainSdfWasBlocked = true;
                return false;
            }

            if ((result.FailureFlags & (uint)ConstructionValidationFlags.OccupiedGridCell) != 0u)
            {
                _terrainSdfPlacementValid = false;
                _terrainSdfPlacementBlockReason = "GRID OCCUPIED";
                TryQueueTerrainSdfBlockHaptic();
                _terrainSdfWasBlocked = true;
                return false;
            }

            if ((result.FailureFlags & (uint)ConstructionValidationFlags.TerrainIntersection) == 0u)
                return AcceptTerrainSdfPlacement();

            _terrainSdfPlacementValid = false;
            _terrainSdfPlacementBlockReason = "TERRAIN SDF";
            TryQueueTerrainSdfBlockHaptic();
            _terrainSdfWasBlocked = true;
            return false;
        }

        private bool TryBuildConstructionValidationPayload(
            BaseModuleTemplate template,
            Vector3 previewPosition,
            Quaternion previewRotation,
            out ConstructionRequestDTO request,
            out StructuralBoundsDTO bounds,
            out ConstructionValidationSettingsDTO settings,
            out ConstructionSipBudgetDTO sipBudget,
            out ConstructionMockWorldSampler worldSampler)
        {
            request = default;
            bounds = default;
            settings = ModularBaseConstructionValidator.GetTunerSettings();
            sipBudget = default;
            worldSampler = default;

            if (template == null || activeBuildable == null)
                return false;

            ModularBaseConstructionValidator.TryReadTunerSettingsFromVault(_shinobuSocketVault, out settings);
            float gridSize = ResolveConstructionGridSize();
            if (!TryResolveConstructionPivotAup(previewPosition, out double3 pivotAup))
                return false;

            double3 rootAup = ResolveConstructionRootAup(previewPosition);
            uint moduleHash = ResolveShinobuModuleHash(activeBuildable);
            uint rotation = ResolveConstructionRotationIndex(previewRotation);
            if (!ModularBaseConstructionValidator.TryBuildRequestFromAup(
                    rootAup,
                    pivotAup,
                    moduleHash,
                    rotation,
                    gridSize,
                    out request))
                return false;

            settings.GridSizeMeters = gridSize;
            settings.GlobalQualityWeight = ModularBaseConstructionValidator.ResolveGlobalQualityWeight();
            settings.Frame = (uint)Time.frameCount;
            settings.CandidatePortMask = ToConstructionPortMask(template.SocketMask);

            bounds = ModularBaseConstructionValidator.BuildBounds(
                (float3)template.ProxyBoundsCenter,
                (float3)template.ProxyBoundsSize,
                moduleHash);

            float localBottomY = ModularBaseConstructionValidator.GridToLocal(in request, gridSize).y +
                                 template.ProxyBoundsCenter.y -
                                 template.ProxyBoundsSize.y * 0.5f;
            worldSampler = ModularBaseConstructionValidator.CreateMockWorldSampler(
                rootAup,
                localBottomY - settings.TerrainClearanceMargin,
                moduleHash);

            sipBudget.TotalBaseSIP = structuralIntegrityBudget;
            sipBudget.AddedSIPCost = EstimateAddedSipCost(template);
            sipBudget.DepthPressure = EstimateDepthPressure(pivotAup);
            sipBudget.StructuralWarningRatio = 1f;
            sipBudget.BaseHash = moduleHash;
            sipBudget.Flags = 0u;
            sipBudget._pad0 = 0u;
            sipBudget._pad1 = 0u;
            return true;
        }

        private bool TryFindOccupiedConstructionGridCell(
            in ConstructionRequestDTO request,
            in ConstructionValidationSettingsDTO settings,
            out int occupiedCellHash)
        {
            occupiedCellHash = 0;
            ConstructionManager constructionManager = ResolveCachedConstructionManager();
            IReadOnlyList<GameObject> modules = constructionManager != null ? constructionManager.SpawnedModules : null;
            if (modules == null)
                return false;

            float gridSize = settings.GridSizeMeters > 0.001f ? settings.GridSizeMeters : ResolveConstructionGridSize();
            int moduleCount = modules.Count;
            uint frame = settings.Frame != 0u ? settings.Frame : unchecked((uint)Time.frameCount);
            IDataVault vault = _shinobuSocketVault;
            if (vault != null &&
                ModularBaseConstructionValidator.TryResolveOccupancyHashTable(vault, out _) &&
                vault.TryLockBuffer(BufferID.ConstructionBuilderOccupancy, SystemID.Construction))
            {
                bool resolvedLockedTable = ModularBaseConstructionValidator.TryResolveOccupancyHashTable(
                    vault,
                    out NativeArray<BaseModuleOccupancyDTO> occupancyTable);
                bool hydrated = resolvedLockedTable;
                bool foundInVaultTable = false;
                int vaultOccupiedCellHash = 0;
                if (resolvedLockedTable)
                {
                    for (int i = 0; i < moduleCount; i++)
                    {
                        GameObject module = modules[i];
                        if (module == null)
                            continue;

                        Transform moduleTransform = module.transform;
                        if (moduleTransform == null)
                            continue;

                        if (!TryResolveConstructionPivotAup(moduleTransform.position, out double3 moduleAup))
                            continue;

                        if (!ModularBaseConstructionValidator.TryBuildRequestFromAup(
                                request.RootAUP,
                                moduleAup,
                                0u,
                                0u,
                                gridSize,
                                out ConstructionRequestDTO existing))
                        {
                            continue;
                        }

                        BaseModuleOccupancyDTO entry;
                        entry.GridPos = existing.GridPos;
                        entry.ModuleHash = existing.ModuleHash;
                        entry.PortMask = ConstructionPortMask.AllCardinal;
                        entry.NodeIndex = 0;
                        entry.Flags = 0u;
                        entry._pad0 = 0u;
                        hydrated &= ModularBaseConstructionValidator.TryInsertOccupancyCell(
                            occupancyTable,
                            in entry,
                            frame);
                    }

                    if (hydrated)
                    {
                        foundInVaultTable = ModularBaseConstructionValidator.TryFindOccupiedCell(
                            occupancyTable,
                            request.GridPos,
                            frame,
                            out vaultOccupiedCellHash);
                    }
                }

                vault.TryUnlockBuffer(BufferID.ConstructionBuilderOccupancy, SystemID.Construction);
                if (foundInVaultTable)
                {
                    occupiedCellHash = vaultOccupiedCellHash;
                    return true;
                }

                if (hydrated)
                    return false;
            }

            for (int i = 0; i < moduleCount; i++)
            {
                GameObject module = modules[i];
                if (module == null)
                    continue;

                Transform moduleTransform = module.transform;
                if (moduleTransform == null)
                    continue;

                if (!TryResolveConstructionPivotAup(moduleTransform.position, out double3 moduleAup))
                    continue;

                if (!ModularBaseConstructionValidator.TryBuildRequestFromAup(
                        request.RootAUP,
                        moduleAup,
                        0u,
                        0u,
                        gridSize,
                        out ConstructionRequestDTO existing))
                {
                    continue;
                }

                if (!math.all(existing.GridPos == request.GridPos))
                    continue;

                occupiedCellHash = ModularBaseConstructionValidator.HashGrid(request.GridPos);
                return true;
            }

            return false;
        }

        private bool TryFindVoxelSdfIntersection(
            in ConstructionRequestDTO request,
            in StructuralBoundsDTO bounds,
            in ConstructionValidationSettingsDTO settings,
            out float maxDensity)
        {
            maxDensity = 0f;
            int probeCount = ModularBaseConstructionValidator.ResolveTerrainProbeCount(settings.GlobalQualityWeight);
            for (int i = 0; i < probeCount; i++)
            {
                float3 localProbe = ModularBaseConstructionValidator.ResolveTerrainProbeLocal(
                    i,
                    in request,
                    in bounds,
                    in settings);
                double3 probeAup = request.RootAUP + new double3(localProbe.x, localProbe.y, localProbe.z);
                Vector3 probeRuntime = HectonFloatingOrigin.ToRuntimePosition(probeAup);
                float3 probeRuntimeFloat = new float3(probeRuntime.x, probeRuntime.y, probeRuntime.z);
                if (!math.all(math.isfinite(probeRuntimeFloat)))
                    return true;

                if (!HectonVoxelVolume.TrySampleRuntimeSdfDensity(probeRuntime, out float density))
                    continue;

                if (density > 0f)
                {
                    maxDensity = math.max(maxDensity, density);
                    return true;
                }
            }

            return false;
        }

        private double3 ResolveConstructionRootAup(Vector3 fallbackRuntimePosition)
        {
            ConstructionManager constructionManager = ResolveCachedConstructionManager();
            if (constructionManager != null && constructionManager.SpawnedModules != null)
            {
                IReadOnlyList<GameObject> modules = constructionManager.SpawnedModules;
                for (int i = 0, count = modules.Count; i < count; i++)
                {
                    GameObject module = modules[i];
                    if (module != null &&
                        TryResolveConstructionPivotAup(module.transform.position, out double3 moduleAup))
                    {
                        return moduleAup;
                    }
                }
            }

            return TryResolveConstructionPivotAup(fallbackRuntimePosition, out double3 fallbackAup)
                ? fallbackAup
                : GlobalSignals.CurrentRuntimeOriginAup().ToAbsoluteDouble3();
        }

        private static bool TryResolveConstructionPivotAup(Vector3 runtimePosition, out double3 pivotAup)
        {
            pivotAup = default;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
            AbsoluteUniversePosition resolvedAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            if (!MathGuard.IsFinite(in resolvedAup))
                return false;

            pivotAup = resolvedAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(pivotAup));
        }

        private float ResolveConstructionGridSize()
        {
            return constructionGridSize > 0.001f ? constructionGridSize : StructuralPlacementGridMeters;
        }

        private static uint ResolveConstructionRotationIndex(Quaternion rotation)
        {
            float yaw = rotation.eulerAngles.y;
            int yawStep = Mathf.RoundToInt(yaw / StructuralRotationStepDegrees);
            return (uint)(yawStep & 3);
        }

        private static uint ToConstructionPortMask(ModuleSocketMask socketMask)
        {
            uint mask = ConstructionPortMask.None;
            if ((socketMask & ModuleSocketMask.East) != 0)
                mask |= ConstructionPortMask.PosX;
            if ((socketMask & ModuleSocketMask.West) != 0)
                mask |= ConstructionPortMask.NegX;
            if ((socketMask & ModuleSocketMask.Top) != 0)
                mask |= ConstructionPortMask.PosY;
            if ((socketMask & ModuleSocketMask.Bottom) != 0)
                mask |= ConstructionPortMask.NegY;
            if ((socketMask & ModuleSocketMask.North) != 0)
                mask |= ConstructionPortMask.PosZ;
            if ((socketMask & ModuleSocketMask.South) != 0)
                mask |= ConstructionPortMask.NegZ;
            return mask != 0u ? mask : ConstructionPortMask.AllCardinal;
        }

        private static float EstimateDepthPressure(double3 pivotAup)
        {
            float depthMeters = math.isfinite(pivotAup.y) ? math.max(0f, -(float)pivotAup.y) : 0f;
            return depthMeters * 0.0125f;
        }

        private static float EstimateAddedSipCost(BaseModuleTemplate template)
        {
            if (template == null)
                return 0f;

            float yieldSip = math.isfinite(template.ModuleYieldStrengthNewtons)
                ? template.ModuleYieldStrengthNewtons * 0.0001f
                : 0f;
            float volumeSip = template.ProxyBoundsSize.x * template.ProxyBoundsSize.y * template.ProxyBoundsSize.z * 0.015625f;
            return math.clamp(yieldSip + volumeSip, 1f, 100f);
        }

        private bool IsCurrentGhostCollider(Collider candidate)
        {
            return candidate != null &&
                   _currentGhostObj != null &&
                   candidate.transform != null &&
                   candidate.transform.IsChildOf(_currentGhostObj.transform);
        }

        private bool AcceptTerrainSdfPlacement()
        {
            _terrainSdfWasBlocked = false;
            return true;
        }

        private void TryQueueTerrainSdfBlockHaptic()
        {
            if (_terrainSdfWasBlocked || _terrainSdfBlockHapticCooldown > 0f)
                return;

            QueueToolHapticFeedback(
                TerrainSdfBlockHapticPower,
                TerrainSdfBlockHapticRatedPower,
                TerrainSdfBlockHapticPriority);
            _terrainSdfBlockHapticCooldown = TerrainSdfBlockHapticCooldownSeconds;
        }

        private void DrawBuildGhostProjection()
        {
            if (!_builderGhostPreviewActive || activeBuildable == null)
                return;

            BaseModuleTemplate template = activeBuildable.ModuleTemplate;
            if (template == null)
                return;

            Vector3 proxyBoundsSize = template.ProxyBoundsSize;
            if (proxyBoundsSize.x <= 0.01f || proxyBoundsSize.y <= 0.01f || proxyBoundsSize.z <= 0.01f)
                return;

            bool placementAllowed =
                _builderGhostPreviewCanBuild &&
                _semanticPlacementValid &&
                _terrainSdfPlacementValid &&
                _integrityPlacementValid;
            Vector3 targetRuntime = _builderGhostPreviewPosition + (_builderGhostPreviewRotation * template.ProxyBoundsCenter);
            if (!TryResolveConstructionPivotAup(targetRuntime, out double3 targetAup))
                return;

            _builderGhostPreviewScale = proxyBoundsSize;
            PublishConstructionPreviewSignal(targetAup, _builderGhostPreviewRotation, proxyBoundsSize, placementAllowed);
        }

        private void PublishConstructionPreviewSignal(double3 centerAup, Quaternion rotation, Vector3 proxyBoundsSize, bool placementAllowed)
        {
            EnsureConstructionSignalLanes();
            ConstructionPreviewSignal signal = default;
            signal.CenterAup = AbsoluteUniversePosition.FromAbsolutePosition(centerAup);
            signal.Rotation = new float4(rotation.x, rotation.y, rotation.z, rotation.w);
            signal.Scale = (float3)Vector3.Max(proxyBoundsSize, Vector3.one * 0.001f);
            signal.ModuleHash = ResolveShinobuModuleHash(activeBuildable);
            signal.FailureFlags = _lastConstructionValidationResult.FailureFlags;
            signal.ResultHash = _lastConstructionValidationResult.ResultHash;
            signal.Frame = unchecked((uint)Time.frameCount);
            signal.IsValid = placementAllowed ? (byte)1 : (byte)0;
            float signalQuality = ShinobuSocketConstructionRuntime.ResolveGlobalQualityWeight();
            ConstructionSocketTuningDTO socketTuning = ShinobuSocketConstructionRuntime.GetTuning();
            signal.DearLieDampen = math.clamp(math.isfinite(_shinobuDearLieDampen) ? _shinobuDearLieDampen : 0f, 0f, 1f);
            signal.GlobalQualityWeight = ShinobuSocketConstructionRuntime.SanitizeQuality(signalQuality);
            signal.DearLieWiggleSpeed = math.clamp(math.isfinite(socketTuning.DearLieWiggleSpeed) ? socketTuning.DearLieWiggleSpeed : 18f, 0f, 90f);
            signal.Flags = ConstructionPreviewSignal.FlagActive | ConstructionPreviewSignal.FlagFallbackPreview;
            if (_isSnapped)
                signal.Flags |= ConstructionPreviewSignal.FlagSocketSnap;
            if (signal.DearLieDampen > 0.0001f)
                signal.Flags |= ConstructionPreviewSignal.FlagDearLieActive;
            SignalBus<ConstructionPreviewSignal>.TryPush(in signal);
        }

        private void UpdatePlacementValidationState()
        {
            if (_habitatConstructionManager == null || activeBuildable == null || !_builderGhostPreviewActive)
            {
                _integrityPlacementValid = true;
                _integrityPlacementBlockReason = string.Empty;
                UpdatePlacementValidityState();
                return;
            }

            if (_currentGhostObj == null)
            {
                _integrityPlacementValid = true;
                _integrityPlacementBlockReason = string.Empty;
                _integrityValidationDirty = false;
                _hasScheduledValidationSnapshot = false;
                _hasCompletedValidationSnapshot = false;
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
                ConstructionManager constructionManager = ResolveCachedConstructionManager();
                if (_habitatConstructionManager.ScheduleIntegrityValidation(
                        constructionManager,
                        _currentGhostObj,
                        activeBuildable,
                        ResolveActiveGridSize(),
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
            if (!_builderGhostPreviewActive || activeBuildable == null)
                return false;

            ConstructionManager constructionManager = ResolveCachedConstructionManager();
            snapshot.Buildable = activeBuildable;
            snapshot.TargetSocket = _snappedSocket;
            snapshot.ModuleCount = constructionManager != null ? constructionManager.ModuleCount : 0;
            snapshot.Position = _builderGhostPreviewPosition;
            snapshot.Rotation = _builderGhostPreviewRotation;
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

            if (!_terrainSdfPlacementValid && !string.IsNullOrEmpty(_terrainSdfPlacementBlockReason))
                return _terrainSdfPlacementBlockReason;

            if (!_integrityPlacementValid && !string.IsNullOrEmpty(_integrityPlacementBlockReason))
                return _integrityPlacementBlockReason;

            return "PLACEMENT INVALID";
        }

        private void WriteCostDigest(BuildableData data, ref FixedCharBuffer buffer)
        {
            if (data == null || data.buildCost == null || data.buildCost.Count == 0)
            {
                AppendText(ref buffer, "NO COST");
                return;
            }

            bool wroteAny = false;
            for (int i = 0; i < data.buildCost.Count; i++)
            {
                InventoryCost cost = data.buildCost[i];
                if (cost == null || cost.item == null || cost.amount <= 0)
                    continue;

                if (wroteAny)
                    AppendText(ref buffer, " | ");

                string itemName = string.IsNullOrWhiteSpace(cost.item.itemName) ? cost.item.name : cost.item.itemName;
                AppendUpperInvariant(ref buffer, itemName);
                AppendText(ref buffer, " ");

                int available = inventory != null
                    ? inventory.CountTotal(Hecton.Localization.LocHash.Compute(cost.item.PersistentId))
                    : 0;
                buffer.AppendInt(available);
                AppendText(ref buffer, "/");
                buffer.AppendInt(cost.amount);
                wroteAny = true;
            }

            if (!wroteAny)
                AppendText(ref buffer, "NO COST");
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

        private static bool AppendText(ref FixedCharBuffer buffer, string value)
        {
            return string.IsNullOrEmpty(value) || buffer.Append(value);
        }

        private static bool AppendUpperInvariant(ref FixedCharBuffer buffer, string value)
        {
            if (string.IsNullOrEmpty(value))
                return true;

            Span<char> scratch = stackalloc char[1];
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                scratch[0] = c == '_' ? ' ' : char.ToUpperInvariant(c);
                if (!buffer.Append(scratch))
                    return false;
            }

            return true;
        }

        private static float ResolveDecayBlend(float speed, float deltaTime)
        {
            float x = math.max(0f, speed) * math.max(0f, deltaTime);
            return math.saturate(x / (1f + x));
        }

        private static int WrapIndex(int value, int count)
        {
            if (count <= 0) return -1;
            int wrapped = value % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }

        private void TryDeconstructTargetModule()
        {
            if (playerCamera == null)
            {
                NotifyBuildBlocked("NO MODULE TARGET");
                return;
            }

            Ray ray = playerCamera.ViewportPointToRay(ViewportCenter);
            if (!TryGetBuildHit(ray, HectonLayerMasks.ConstructionSurfaceLayerMask, out RaycastHit hit))
            {
                NotifyBuildBlocked("NO MODULE TARGET");
                return;
            }

            BaseModule module = hit.collider != null ? hit.collider.GetComponentInParent<BaseModule>() : null;
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

            if (!TryRequestModuleDeconstruction(module, ray.origin, ray.direction, buildDistance, 1))
            {
                NotifyBuildBlocked("DECONSTRUCTION OFFLINE");
                return;
            }

            NotifyModuleDeconstructionQueued(module);
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

        private bool TryRequestModuleDeconstruction(
            BaseModule module,
            Vector3 rayOrigin,
            Vector3 rayDirection,
            float maxDistance,
            byte toolKind)
        {
            if (module == null)
                return false;

            IHabitatDeconstructionSystem deconstructionSystem = GlobalRegistry.HabitatDeconstruction;
            if (deconstructionSystem == null || !deconstructionSystem.IsInitialized)
                return false;

            Vector3 modulePosition = module.transform.position;
            float directionLengthSq = rayDirection.sqrMagnitude;
            if (directionLengthSq <= 0.0001f)
                rayDirection = Vector3.down;
            else
                rayDirection *= math.rsqrt(directionLengthSq);

            if (!TryResolveConstructionPivotAup(modulePosition, out double3 targetAupDouble) ||
                !TryResolveConstructionPivotAup(rayOrigin, out double3 rayOriginAupDouble))
            {
                return false;
            }

            DeconstructRequestSignal request = new DeconstructRequestSignal
            {
                TargetAup = AbsoluteUniversePosition.FromAbsolutePosition(targetAupDouble),
                RayOriginAup = AbsoluteUniversePosition.FromAbsolutePosition(rayOriginAupDouble),
                TargetEntityId = unchecked((uint)EntityId.ToULong(module.gameObject.GetEntityId())),
                RequesterEntityId = unchecked((uint)EntityId.ToULong(gameObject.GetEntityId())),
                MaxDistance = Mathf.Max(0f, maxDistance),
                RayDirection = new float3(rayDirection.x, rayDirection.y, rayDirection.z),
                Frame = (uint)Mathf.Max(0, Time.frameCount),
                ToolKind = toolKind,
                Flags = 0
            };

            return deconstructionSystem.EnqueueDeconstruction(in request);
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

        private void PublishConstructionCommitSignals(GameObject placedModule, BuildableData data)
        {
            if (placedModule == null || data == null)
                return;

            EnsureConstructionSignalLanes();
            Transform moduleTransform = placedModule.transform;
            BaseModuleTemplate template = data.ModuleTemplate;
            Vector3 localCenter = template != null ? template.ProxyBoundsCenter : Vector3.zero;
            Vector3 proxySize = template != null ? template.ProxyBoundsSize : Vector3.one;
            Vector3 centerRuntime = moduleTransform.TransformPoint(localCenter);
            if (!TryResolveConstructionPivotAup(centerRuntime, out double3 centerAupDouble))
                return;

            AbsoluteUniversePosition centerAup = AbsoluteUniversePosition.FromAbsolutePosition(centerAupDouble);
            float3 extents = (float3)(Vector3.Max(proxySize * 0.5f, Vector3.one * 0.5f) + Vector3.one * 0.25f);
            float radius = math.max(6f, math.cmax(extents) * 4f);
            uint sourceLow = FoldEntityId(EntityId.ToULong(placedModule.GetEntityId()));
            uint moduleHash = ResolveShinobuModuleHash(data);

            AcousticPingSignal clunk = default;
            clunk.PositionAup = centerAup;
            clunk.RadiusMeters = radius;
            clunk.Intensity01 = math.saturate(0.35f + radius * 0.025f);
            clunk.SourceId = sourceLow != 0u ? sourceLow : moduleHash;
            clunk.Channel = AcousticPingSignal.ChannelMetalStress;
            clunk.Flags = 0;
            GlobalSignals.Publish(in clunk);

            FloraExclusionSignal flora = default;
            flora.CenterAup = centerAup;
            flora.Extents = extents;
            flora.ModuleHash = moduleHash;
            flora.SourceEntityLow = sourceLow;
            flora.Frame = unchecked((uint)Time.frameCount);
            flora.Operation = FloraExclusionSignal.OperationApply;
            flora.Flags = 0;
            flora._pad0 = 0;
            flora._pad1 = 0u;
            SignalBus<FloraExclusionSignal>.TryPush(in flora);
        }

        private static uint FoldEntityId(ulong entityId)
        {
            return unchecked((uint)entityId ^ (uint)(entityId >> 32));
        }

        // ══════════════════════════════════════════════════════════
        //  AUDIO
        // ══════════════════════════════════════════════════════════

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

        private ConstructionManager ResolveCachedConstructionManager()
        {
            if (_cachedConstructionManager == null)
                _cachedConstructionManager = ResolveConstructionManager();

            return _cachedConstructionManager;
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
            if (rotationStep     != StructuralRotationStepDegrees) rotationStep = StructuralRotationStepDegrees;
            if (constructionGridSize < StructuralPlacementGridMeters) constructionGridSize = StructuralPlacementGridMeters;
            if (structuralIntegrityBudget < 1f) structuralIntegrityBudget = 1f;
            if (structuralDepthPenalty < 0.01f) structuralDepthPenalty = 0.01f;
            if (snapRadius       != StructuralSnapRadiusMeters) snapRadius = StructuralSnapRadiusMeters;
            if (unsnapRadius     < StructuralUnsnapRadiusMeters) unsnapRadius = StructuralUnsnapRadiusMeters;
            if (snapSpeed        < 1f) snapSpeed        = 1f;
        }

        private void OnDrawGizmosSelected()
        {
            if (playerCamera == null) return;

            // Vizualizatsiya dalnosti stroitelstva
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.15f);
            Gizmos.DrawWireSphere(playerCamera.transform.position, buildDistance);

            // Vizualizatsiya snap-zony (tolko v Play Mode pri nalichii prizraka)
            if (Application.isPlaying && _currentGhostObj != null)
            {
                if (_isSnapped && _snappedSocketTransform != null)
                {
                    // Snap active — zelenaya liniya k soketu
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

