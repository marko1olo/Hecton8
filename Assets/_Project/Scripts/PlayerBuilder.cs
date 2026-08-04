// ============================================================================
// HECTON-8 — PlayerBuilder.cs
// Kontroller stroitelstva modulnoy bazy.
//
// v3.0 — SOCKET SNAP SYSTEM:
//   [ADD] Sistema magnitnogo prilipaniya k Vault socket rows.
//   [ADD] Poisk soketov cherez SHINOBU_217 template/AUP resolver (zero GC target).
//   [ADD] Gisterezis: snapRadius=2m, unsnapRadius=2.5m (bez mertsaniya).
//   [ADD] Plavnyy snap/unsnap cherez eksponentsialnoe sglazhivanie.
//   [ADD] Zanyatye sokety propuskayutsya po SocketStateDTO.ConnectionStatus.
//   [ADD] Pri razmeschenii: zanyatost pishetsya v SocketConnectionPairDTO.
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
//     • Vault-owned SocketStateDTO/SocketConnectionPairDTO → no ModuleSocket authority branch.
//     • Vse struct math, nikakih List/LINQ/lyambd.
//
// PREDYDUSchIE VERSII (sohraneny):
//   v2.0: PlayerTool inheritance, ghost pool lifecycle.
//   v1.0: Basic placement.
// ============================================================================

using System;
using Hecton8.Audio;
using Hecton8.Building;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Construction;
using Hecton8.UI;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using ConstructionTerrainSampler = Hecton8.Construction.ConstructionTerrainSampler;

namespace Hecton8.Building
{
    [DisallowMultipleComponent]
    public sealed class PlayerBuilder : PlayerTool, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private static int s_x001PlayerBuilderSignalPushDropCount;
        private const double DefaultSeaLevelAupY = 14.02d;

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

        private bool _builderGhostPreviewActive;
        private bool _builderGhostPreviewCanBuild = true;
        private Vector3 _builderGhostPreviewPosition;
        private Quaternion _builderGhostPreviewRotation = Quaternion.identity;
        private Vector3 _builderGhostPreviewScale = Vector3.one;
        private JobHandle _builderGhostValidationHandle;
        private bool _builderGhostValidationPending;
        private uint _builderGhostValidationSelectionGeneration;
        private uint _builderGhostValidationQueryHash;
        private uint _builderGhostValidationFrame;
        private long _builderGhostValidationStartTicks;
        private float _builderGhostValidationQuality;
        private float _builderGhostValidationMinSdf;
        private uint _builderGhostValidationSolidCornerCount;
        private InteractionSurfaceHit _hit;
        private const float StructuralPlacementGridMeters = 4f;
        private const float StructuralPlacementGridInv = 0.25f;
        private const float StructuralRotationStepDegrees = 90f;
        private const float StructuralSnapRadiusMeters = 1f;
        private const float StructuralUnsnapRadiusMeters = 1.25f;
        private const int BuildCostDigestCapacity = 32;
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
        /// Predyduschiy snap-status. Dlya edge detection (zvuk pri snap/unsnap).
        /// </summary>
        private bool _wasSnapped;
        private ModuleCatalog _buildCatalog;
        private int _activeBuildableIndex = -1;
        private FixedCharBuffer _builderHudBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - builder HUD notification staging buffer - owner: PlayerBuilder
        private FixedCharBuffer _builderLogTitleBuffer = new FixedCharBuffer(256); // COLD ALLOC: char[256] - builder field-log title staging buffer - owner: PlayerBuilder
        private FixedCharBuffer _builderLogSummaryBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - builder field-log summary staging buffer - owner: PlayerBuilder
        private const byte PlacementRuleKindNone = 0;
        private const byte PlacementRuleKindDeepDrill = 1;
        private const byte PlacementRuleKindAutonomousExtractor = 2;
        private byte _activePlacementRuleKind;
        private DeepDrillModule _activeDeepDrillRule;
        private AutonomousExtractorModule _activeAutonomousExtractorRule;
        private bool _semanticPlacementValid = true;
        private string _semanticPlacementBlockReason = string.Empty;
        private uint _activeBuildableGeneration;
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
        private ConstructionTerrainSampler _lastConstructionWorldSampler;
        private static bool s_ConstructionSignalLanesInitialized;
        private HabitatConstructionManager _habitatConstructionManager;
        private ConstructionManager _cachedConstructionManager;
        private IObjectPoolService _cachedObjectPool;
        private IHabitatDeconstructionSystem _cachedHabitatDeconstructionSystem;
        private IInteractionSignalService _cachedInteractionSignalService;
        private AutonomousExtractorSystem _cachedAutonomousExtractorSystem;
        private IAudioService _cachedAudioService;
        private IHectonOceanKinematicsService _cachedOceanKinematicsService;
        private AudioClip _pendingBuilderAudio0;
        private AudioClip _pendingBuilderAudio1;
        private AudioClip _pendingBuilderAudio2;
        private AudioClip _pendingBuilderAudio3;
        private int _pendingBuilderAudioCount;
        private bool _lateFrameRegistered;
        private IQuestSystem _cachedQuestSystem;
        private ulong _buildRayRequesterId;
        private bool _shinobuHasSnappedPose;
        private Vector3 _shinobuSnappedPosePosition;
        private Quaternion _shinobuSnappedPoseRotation;
        private Transform _shinobuSnappedTargetTransform;
        private Vector3 _shinobuSnappedTargetLocalPosition;
        private ModuleSocketDirection _shinobuSnappedTargetDirection;
        private uint _shinobuSnappedTargetCompatibilityHash;
        private int _shinobuSnappedTargetSocketIndex = -1;
        private int _shinobuSnappedGhostSocketIndex = -1;
        private float _shinobuDearLieDampen;
        private int _shinobuSocketAdapterCandidateCount;
        private IDataVault _shinobuSocketVault;
        private uint _shinobuSocketVaultSceneHash;
        private int _shinobuSocketVaultModuleCount = -1;
        private int _shinobuSocketVaultTargetCount;
        private int _shinobuSocketVaultConnectionCount;
        private bool _shinobuSocketVaultHasRootAup;
        private double3 _shinobuSocketVaultRootAup;
        private uint _shinobuSocketVaultTopologyVersion;
        private uint _shinobuBuilderFrameCounter;
        private uint _shinobuSocketFrameCounter;
        private JobHandle _shinobuSocketSnapHandle;
        private bool _shinobuSocketSnapPending;
        private uint _shinobuSocketSnapSelectionGeneration;
        private int _shinobuSocketSnapBestResultIndex;
        private uint _shinobuSocketSnapFrame;
        private uint _shinobuSocketSnapSceneHash;
        private uint _shinobuSocketSnapQueryHash;
        private double3 _shinobuSocketSnapGhostRootAup;
        private long _shinobuSocketSnapStartTicks;
        private float _shinobuSocketCachedBestDistanceSq = float.MaxValue;
        private bool _integrityPlacementValid = true;
        private bool _integrityValidationDirty;
        private string _integrityPlacementBlockReason = string.Empty;
        private bool _cachedHasResourcesForActiveBuildable;
        private BuildReadiness _cachedBuildReadiness = BuildReadiness.Offline;
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
        public int BuildableCount => _buildCatalog != null ? _buildCatalog.GetViewableCount(_cachedQuestSystem) : 0;
        public bool HasResourcesForActiveBuildable => _cachedHasResourcesForActiveBuildable;
        public bool CanPlaceActiveBuildable => activeBuildable != null && IsBuildableBlueprintViewable(activeBuildable) && _builderGhostPreviewActive && _builderGhostPreviewCanBuild && _semanticPlacementValid && _terrainSdfPlacementValid && _integrityPlacementValid;
        public bool HasPlacementPreview => _builderGhostPreviewActive;
        public BuildReadiness ActiveBuildReadiness => _cachedBuildReadiness;

        /// <summary>Seychas prizrak prilip k soketu.</summary>
        public bool IsSnapped => _isSnapped;

        private struct ValidationSnapshot
        {
            public BuildableData Buildable;
            public int TargetSocketIndex;
            public int ModuleCount;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        public BuildableData GetBuildableAt(int index)
        {
            int viewableCount = _buildCatalog != null ? _buildCatalog.GetViewableCount(_cachedQuestSystem) : 0;
            if (_buildCatalog == null || index < 0 || index >= viewableCount)
                return null;

            return _buildCatalog.GetViewableAt(index, _cachedQuestSystem);
        }

        public BuildableData GetRelativeBuildable(int direction)
        {
            int viewableCount = _buildCatalog != null ? _buildCatalog.GetViewableCount(_cachedQuestSystem) : 0;
            if (_buildCatalog == null || viewableCount <= 0)
                return null;

            int currentIndex = _buildCatalog.IndexOfViewable(activeBuildable, _cachedQuestSystem);
            if (currentIndex < 0)
                currentIndex = direction >= 0 ? -1 : 0;

            int nextIndex = (currentIndex + direction + viewableCount) % viewableCount;
            return _buildCatalog.GetViewableAt(nextIndex, _cachedQuestSystem);
        }

        public bool DebugDeployActiveBuildable(Vector3 position, Quaternion rotation, bool consumeCost = true)
        {
            EnsureCatalogSelection();
            if (activeBuildable == null || activeBuildable.finalPrefab == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[BuilderDebug] DebugDeploy aborted: no active buildable/final prefab.");
#endif
                return false;
            }

            if (!IsBuildableBlueprintViewable(activeBuildable))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[BuilderDebug] DebugDeploy aborted: blueprint locked.");
#endif
                return false;
            }

            if (consumeCost && !HasResources(activeBuildable))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[BuilderDebug] DebugDeploy aborted: missing resources for active buildable.");
#endif
                return false;
            }

            if (!TryGetObjectPool(out IObjectPoolService pool))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[BuilderDebug] DebugDeploy aborted: ObjectPoolManager unavailable.");
#endif
                return false;
            }

            GameObject spawned = SpawnPlacedModule(activeBuildable, position, rotation, pool);
            if (spawned == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[BuilderDebug] DebugDeploy aborted: failed to spawn active buildable.");
#endif
                return false;
            }

            ApplyConstructedModuleSnap(spawned, position, rotation);

            if (consumeCost)
            {
                if (!ConsumeResources(activeBuildable))
                {
                    ConstructionManager constructionManager = GetCachedConstructionManager();
                    if (constructionManager != null)
                        constructionManager.DestroyModule(spawned);
                    else
                        pool.Despawn(spawned);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogWarning("[BuilderDebug] DebugDeploy aborted: resource transaction failed.");
#endif
                    return false;
                }
            }

            PublishConstructionCommitSignals(spawned, activeBuildable, position, rotation);

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
            switch (ActiveBuildReadiness)
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
            switch (ActiveBuildReadiness)
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

            switch (ActiveBuildReadiness)
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

            if (wasEquipped)
                DespawnGhost(forceValidationReset: false);

            AssignActiveBuildable(data);
            SyncActiveBuildableIndex();
            RefreshActiveBuildReadiness();

            if (wasEquipped)
                SpawnGhost();
        }

        // ══════════════════════════════════════════════════════════
        //  IPoolable
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            EnsureHabitatConstructionManagerCold();
            _cachedQuestSystem = GlobalRegistry.QuestSystem;
        }

        public override void OnSpawn()
        {
            EnsureHabitatConstructionManagerCold();
            base.OnSpawn();
            BindRuntimeReferences();
            ResetBuilderState();
        }

        public override void OnDespawn()
        {
            DespawnGhost();
            ResetBuilderState();
            ClearPendingBuilderAudioSync();
            TryUnregisterLateFrameTick();
            base.OnDespawn();
        }

        private void OnDestroy()
        {
            ClearPendingBuilderAudioSync();
            TryUnregisterLateFrameTick();
            CompleteShinobuSocketSnapForTeardown();
            CompleteBuilderGhostValidationForTeardown();

            if (_habitatConstructionManager != null)
            {
                _habitatConstructionManager.Dispose();
                _habitatConstructionManager = null;
            }

            _cachedConstructionManager = null;
            _cachedObjectPool = null;
            _cachedHabitatDeconstructionSystem = null;
            _cachedInteractionSignalService = null;
            _cachedAutonomousExtractorSystem = null;
            _cachedOceanKinematicsService = null;
            ClearCachedAudioService();
            _cachedQuestSystem = null;
            _shinobuSocketVault = null;
        }

        protected override void OnToolRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            base.OnToolRegistryServiceReplaced(serviceSlot, previousService, currentService);

            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    bool needsLateFrame = _lateFrameRegistered || _pendingBuilderAudioCount > 0;
                    TryUnregisterLateFrameTick();
                    if (currentService != null && isActiveAndEnabled && needsLateFrame)
                        TryRegisterLateFrameTick();
                    break;
                case GlobalRegistryServiceSlot.ObjectPool:
                    CacheObjectPoolService(currentService as ObjectPoolManager);
                    break;
                case GlobalRegistryServiceSlot.HabitatDeconstructionRuntime:
                    _cachedHabitatDeconstructionSystem = currentService as IHabitatDeconstructionSystem;
                    break;
                case GlobalRegistryServiceSlot.InteractionSignals:
                    _cachedInteractionSignalService = currentService as IInteractionSignalService;
                    break;
                case GlobalRegistryServiceSlot.AutonomousExtractorRuntime:
                    _cachedAutonomousExtractorSystem = currentService as AutonomousExtractorSystem;
                    break;
                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioService(currentService as IAudioService);
                    break;
                case GlobalRegistryServiceSlot.OceanKinematics:
                    _cachedOceanKinematicsService = currentService as IHectonOceanKinematicsService;
                    _integrityValidationDirty = true;
                    RefreshActiveBuildReadiness();
                    break;
                case GlobalRegistryServiceSlot.QuestSystem:
                    _cachedQuestSystem = currentService as IQuestSystem;
                    RefreshActiveBuildReadiness();
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    RebindBuilderDataVault(currentService as IDataVault);
                    break;
            }
        }

        private void CacheAudioService(IAudioService audioService)
        {
            _cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null;
        }

        private IAudioService ResolveAudioService()
        {
            IAudioService audioService = _cachedAudioService;
            if (IsAudioServiceUsable(audioService))
                return audioService;

            ClearCachedAudioService();
            return null;
        }

        private void ClearCachedAudioService()
        {
            _cachedAudioService = null;
        }

        private static bool IsAudioServiceUsable(IAudioService audioService)
        {
            if (audioService == null || !audioService.IsAudioRuntimeReady)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private void RebindBuilderDataVault(IDataVault vault)
        {
            CompleteShinobuSocketSnapForTeardown();
            CompleteBuilderGhostValidationForTeardown();
            _shinobuSocketVault = vault;
            _shinobuSocketVaultSceneHash = 0u;
            _shinobuSocketVaultModuleCount = -1;
            _shinobuSocketVaultTargetCount = 0;
            _shinobuSocketVaultConnectionCount = 0;
            _shinobuSocketVaultHasRootAup = false;
            _shinobuSocketVaultRootAup = default;
            _shinobuSocketVaultTopologyVersion = 0u;
            ModularBaseConstructionValidator.InitializeVault(vault);
            _habitatConstructionManager?.BindCatalogVault(vault);
            if (vault != null)
                ShinobuSocketConstructionRuntime.InitializeVault(vault);
            if (_builderGhostPreviewActive)
                _integrityValidationDirty = true;
        }

        // ══════════════════════════════════════════════════════════
        //  TOOL LIFECYCLE
        // ══════════════════════════════════════════════════════════

        public override void OnEquip()
        {
            base.OnEquip();
            BindRuntimeReferences();
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
            else
            {
                RefreshActiveBuildReadiness();
            }
        }

        public void LateFrameTick()
        {
            FlushPendingBuilderAudio();

            if (_pendingBuilderAudioCount <= 0)
                TryUnregisterLateFrameTick();
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
            int viewableCount = _buildCatalog != null ? _buildCatalog.GetViewableCount(_cachedQuestSystem) : 0;
            if (_buildCatalog == null || viewableCount <= 0)
            {
                NotifyBuildBlocked("MODULE CATALOG OFFLINE");
                return;
            }

            int count = viewableCount;
            int startIndex = _buildCatalog.IndexOfViewable(activeBuildable, _cachedQuestSystem);

            if (startIndex < 0)
                startIndex = direction >= 0 ? -1 : 0;

            for (int step = 1; step <= count; step++)
            {
                int candidateIndex = WrapIndex(startIndex + (step * direction), count);
                BuildableData candidate = _buildCatalog.GetViewableAt(candidateIndex, _cachedQuestSystem);
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
            CompleteBuilderGhostValidationForTeardown();

            _ghostYawStep        = 0;
            _isSnapped           = false;
            _wasSnapped          = false;
            InvalidateShinobuCachedSnapPose();
            _shinobuSocketAdapterCandidateCount = 0;
            _shinobuSocketVaultSceneHash = 0u;
            _shinobuSocketVaultModuleCount = -1;
            _shinobuSocketVaultTargetCount = 0;
            _shinobuSocketVaultConnectionCount = 0;
            _shinobuSocketVaultHasRootAup = false;
            _shinobuSocketVaultRootAup = default;
            _shinobuSocketVaultTopologyVersion = 0u;
            _shinobuBuilderFrameCounter = 0u;
            _shinobuSocketFrameCounter = 0u;
            _shinobuSocketSnapHandle = default;
            _shinobuSocketSnapPending = false;
            _shinobuSocketSnapSelectionGeneration = 0u;
            _shinobuSocketSnapBestResultIndex = 0;
            _shinobuSocketSnapFrame = 0u;
            _shinobuSocketSnapSceneHash = 0u;
            _shinobuSocketSnapQueryHash = 0u;
            _shinobuSocketSnapGhostRootAup = default;
            _shinobuSocketSnapStartTicks = 0L;
            _shinobuSnappedTargetSocketIndex = -1;
            _shinobuSnappedGhostSocketIndex = -1;
            _integrityPlacementValid = true;
            _integrityValidationDirty = false;
            _integrityPlacementBlockReason = string.Empty;
            _hasScheduledValidationSnapshot = false;
            _hasCompletedValidationSnapshot = false;
            _builderGhostPreviewActive = false;
            _builderGhostPreviewCanBuild = true;
            _builderGhostPreviewPosition = default;
            _builderGhostPreviewRotation = Quaternion.identity;
            _builderGhostPreviewScale = Vector3.one;
            _builderGhostValidationHandle = default;
            _builderGhostValidationPending = false;
            _builderGhostValidationSelectionGeneration = 0u;
            _builderGhostValidationQueryHash = 0u;
            _builderGhostValidationFrame = 0u;
            _builderGhostValidationStartTicks = 0L;
            _builderGhostValidationQuality = 0f;
            _builderGhostValidationMinSdf = 0f;
            _builderGhostValidationSolidCornerCount = 0u;
            _habitatConstructionManager?.ResetValidation();
            RefreshActiveBuildReadiness();
        }

        // ══════════════════════════════════════════════════════════
        //  GHOST MANAGEMENT
        // ══════════════════════════════════════════════════════════

        private void SpawnGhost()
        {
            if (activeBuildable == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[PlayerBuilder] No buildable module assigned!");
#endif
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

            _builderGhostPreviewActive = true;
            _builderGhostPreviewCanBuild = true;
            _builderGhostPreviewPosition = spawnPos;
            _builderGhostPreviewRotation = Quaternion.identity;
            _builderGhostPreviewScale = ResolveActivePreviewScale();
            if (_habitatConstructionManager != null && _habitatConstructionManager.IsValidationPending)
            {
                _integrityPlacementValid = false;
                _integrityPlacementBlockReason = HabitatConstructionManager.PendingReason;
            }
            else
            {
                _integrityPlacementValid = true;
                _integrityPlacementBlockReason = string.Empty;
            }
            _integrityValidationDirty = true;
            UpdatePlacementValidationState();
        }

        private void DespawnGhost(bool forceValidationReset = true)
        {
            _builderGhostPreviewActive = false;
            _builderGhostPreviewCanBuild = true;
            _builderGhostPreviewPosition = default;
            _builderGhostPreviewRotation = Quaternion.identity;
            _builderGhostPreviewScale = Vector3.one;
            _semanticPlacementValid = true;
            _semanticPlacementBlockReason = string.Empty;
            _integrityPlacementValid = true;
            _integrityPlacementBlockReason = string.Empty;
            _integrityValidationDirty = false;
            _hasScheduledValidationSnapshot = false;
            _hasCompletedValidationSnapshot = false;
            if (forceValidationReset || _habitatConstructionManager == null || !_habitatConstructionManager.IsValidationPending)
                _habitatConstructionManager?.ResetValidation();
            RefreshActiveBuildReadiness();
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
        ///   3. Nayti blizhayshiy svobodnyy SocketStateDTO row.
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
        ///   • Struct Ray, InteractionSurfaceHit, Vector3, Quaternion — stack.
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

            bool foundSocket = TryUpdateShinobuSocketAlignment(
                rawTargetPoint,
                activeUnsnapRadius,
                out float bestDist,
                out Transform bestTransform,
                out Vector3 bestAlignedPosition,
                out Quaternion bestAlignedRotation);

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
            int previousTargetSocketIndex = _shinobuSnappedTargetSocketIndex;

            bool hasSocketCandidate = foundSocket && math.isfinite(bestDist);
            if (_isSnapped)
            {
                // ── Seychas snapnut: proveryaem uslovie OTRYVA ──
                if (!hasSocketCandidate || bestDist > activeUnsnapRadiusSq)
                {
                    // Otryvaemsya: net soketov poblizosti ILI slishkom daleko
                    _isSnapped = false;
                    InvalidateShinobuCachedSnapPose();
                }
                else
                {
                    // Obnovlyaem: vozmozhno, blizhayshiy soket smenilsya
                    // (igrok navel na drugoy soket togo zhe modulya)
                }
            }
            else
            {
                // ── Seychas NE snapnut: proveryaem uslovie PRILIPANIYa ──
                if (hasSocketCandidate && bestDist <= activeSnapRadiusSq)
                {
                    _isSnapped = true;
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

            if (_isSnapped && _shinobuHasSnappedPose)
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

            if (previousSnapState != _isSnapped ||
                previousTargetSocketIndex != _shinobuSnappedTargetSocketIndex ||
                (_builderGhostPreviewPosition - previousPosition).sqrMagnitude > 0.0001f ||
                Quaternion.Dot(previousRotation, _builderGhostPreviewRotation) < 0.9999f)
            {
                _integrityValidationDirty = true;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  MODULE PLACEMENT (v3.0: Vault socket occupancy commit)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Popytka ustanovit modul:
        ///   1. Proverka CanBuild (kollizii)
        ///   2. Proverka resursov v inventare
        ///   3. Spisanie resursov
        ///   4. Despavn prizraka → spavn finalnogo modulya
        ///   5. v3.0: esli snapnuty k soketu → zapisat SocketConnectionPairDTO
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
                Hecton8.Core.H8Debug.LogWarning("[PlayerBuilder] Missing resources.");
#endif
                return;
            }

            Vector3 placePos = _builderGhostPreviewPosition;
            Quaternion placeRot = _builderGhostPreviewRotation;
            TryResolveExactSnappedPlacementPose(ref placePos, ref placeRot);
            ApplyStructuralPlacementQuantization(ref placePos, ref placeRot);

            if (!TryGetObjectPool(out IObjectPoolService pool))
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
                ConstructionManager constructionManager = GetCachedConstructionManager();
                if (constructionManager != null)
                    constructionManager.DestroyModule(placedModule);
                else
                    pool.Despawn(placedModule);

                NotifyBuildBlocked("RESOURCE TRANSACTION FAILED");
                PlaySound(errorSound);
                return;
            }

            if (_isSnapped && _shinobuHasSnappedPose)
            {
                if (!TryCommitShinobuSnapOccupancy(placePos, placeRot))
                {
                    _shinobuSocketVaultSceneHash = 0u;
                    _shinobuSocketVaultModuleCount = -1;
                    _shinobuSocketVaultTargetCount = 0;
                    _shinobuSocketVaultHasRootAup = false;
                    _shinobuSocketVaultRootAup = default;
                }
            }

            PublishConstructionCommitSignals(placedModule, activeBuildable, placePos, placeRot);
            PlaySound(buildSound);
            NotifyBuildPlaced(activeBuildable);

            // ── Sbros snap-sostoyaniya ──
            _isSnapped = false;
            InvalidateShinobuCachedSnapPose();
            _shinobuSocketAdapterCandidateCount = 0;

            // ── Peresozdaem prizrak ──
            DespawnGhost(forceValidationReset: false);
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

        private bool IsBuildableBlueprintViewable(BuildableData data)
        {
            return data != null && data.IsBlueprintViewable(_cachedQuestSystem);
        }

        private void BindRuntimeReferences()
        {
            HabitatConstructionManager habitatConstructionManager = _habitatConstructionManager;

            IDataVault dataVault = GlobalRegistry.DataVault;
            ModularBaseConstructionValidator.InitializeVault(dataVault);
            habitatConstructionManager?.BindCatalogVault(dataVault);
            if (_shinobuSocketVault == null)
                _shinobuSocketVault = dataVault;
            if (_shinobuSocketVault != null)
                ShinobuSocketConstructionRuntime.InitializeVault(_shinobuSocketVault);
            EnsureConstructionSignalLanes();
            _cachedQuestSystem = GlobalRegistry.QuestSystem;

            IPlayerRuntimeContext playerContext = ResolvePlayerRuntimeContext();
            if (inventory == null && playerContext != null)
                inventory = playerContext.Inventory;

            if (playerCamera == null && playerContext != null)
                playerCamera = playerContext.PlayerCamera;

            if (buildAnchor == null && playerContext != null)
                buildAnchor = playerContext.HandAnchor;

            if (hudNotification == null && playerContext != null)
                hudNotification = playerContext.HudNotification;

            if (_buildCatalog == null)
                _buildCatalog = ResolveModuleCatalog();
            if (_cachedConstructionManager == null)
                _cachedConstructionManager = ResolveConstructionManager();
            if (_cachedObjectPool == null)
                CacheObjectPoolService(null);
            if (_cachedHabitatDeconstructionSystem == null)
                _cachedHabitatDeconstructionSystem = GlobalRegistry.HabitatDeconstruction;
            if (_cachedInteractionSignalService == null)
                _cachedInteractionSignalService = GlobalRegistry.InteractionSignals;
            if (_cachedAutonomousExtractorSystem == null)
                _cachedAutonomousExtractorSystem = GlobalRegistry.AutonomousExtractors;
            if (_cachedOceanKinematicsService == null)
                _cachedOceanKinematicsService = GlobalRegistry.OceanKinematics;
            if (!IsAudioServiceUsable(_cachedAudioService))
                CacheAudioService(GlobalRegistry.Audio);

            if (activeBuildable == null)
                EnsureCatalogSelection();

            SyncActiveBuildableIndex();
        }

        private void EnsureHabitatConstructionManagerCold()
        {
            if (_habitatConstructionManager != null)
                return;

            _habitatConstructionManager = new HabitatConstructionManager();
        }

        private static void EnsureConstructionSignalLanes()
        {
            if (s_ConstructionSignalLanesInitialized)
                return;

            SignalBus<ConstructionPreviewSignal>.Configure(
                expectedCapacity: ConstructionPreviewSignal.ExpectedCapacity,
                maxFrameSignals: ConstructionPreviewSignal.MaxFrameSignals,
                lowTierFrameSignals: ConstructionPreviewSignal.LowTierFrameSignals,
                laneHash: ConstructionPreviewSignal.LaneHash);
            SignalBus<ConstructionPreviewSignal>.EnsureInitialized();
            SignalBus<FloraExclusionSignal>.Configure(
                expectedCapacity: FloraExclusionSignal.ExpectedCapacity,
                maxFrameSignals: FloraExclusionSignal.MaxFrameSignals,
                lowTierFrameSignals: FloraExclusionSignal.LowTierFrameSignals,
                laneHash: FloraExclusionSignal.LaneHash);
            SignalBus<FloraExclusionSignal>.EnsureInitialized();
            s_ConstructionSignalLanesInitialized = true;
        }

        private void AssignActiveBuildable(BuildableData data)
        {
            if (!ReferenceEquals(activeBuildable, data))
                _activeBuildableGeneration = unchecked(_activeBuildableGeneration + 1u);

            activeBuildable = data;
            CacheActivePlacementRule();
        }

        private void EnsureCatalogSelection()
        {
            if (!autoResolveCatalogSelection) return;
            if (activeBuildable != null && IsBuildableBlueprintViewable(activeBuildable)) return;
            int viewableCount = _buildCatalog != null ? _buildCatalog.GetViewableCount(_cachedQuestSystem) : 0;
            if (_buildCatalog == null || viewableCount <= 0) return;

            AssignActiveBuildable(null);
            _activeBuildableIndex = -1;

            for (int i = 0; i < viewableCount; i++)
            {
                BuildableData candidate = _buildCatalog.GetViewableAt(i, _cachedQuestSystem);
                if (candidate == null) continue;

                AssignActiveBuildable(candidate);
                _activeBuildableIndex = _buildCatalog.IndexOf(candidate);
                return;
            }

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

        private bool TryGetObjectPool(out IObjectPoolService pool)
        {
            return TryResolveCachedObjectPool(out pool);
        }

        private void CacheObjectPoolService(ObjectPoolManager candidate)
        {
            ObjectPoolManager pool = candidate;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(pool) ||
                ObjectPoolManager.TryResolveActiveRuntime(ref pool))
            {
                _cachedObjectPool = pool;
                return;
            }

            _cachedObjectPool = null;
        }

        private bool TryResolveCachedObjectPool(out IObjectPoolService pool)
        {
            ObjectPoolManager cached = _cachedObjectPool as ObjectPoolManager;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached))
            {
                pool = cached;
                return true;
            }

            ObjectPoolManager resolved = cached;
            if (ObjectPoolManager.TryResolveActiveRuntime(ref resolved))
            {
                _cachedObjectPool = resolved;
                pool = resolved;
                return true;
            }

            _cachedObjectPool = null;
            pool = null;
            return false;
        }

        private GameObject SpawnPlacedModule(BuildableData data, Vector3 placePos, Quaternion placeRot, IObjectPoolService pool)
        {
            if (data == null)
                return null;

            GameObject placedModule;
            if (data.finalPrefab == null)
            {
                if (!ConstructionRuntimeProxyFactory.TryCreatePlacedProxy(data, placePos, placeRot, out placedModule))
                    return null;

            }
            else
            {
                if (pool == null)
                    return null;

                placedModule = pool.Spawn(data.finalPrefab, placePos, placeRot);
            }

            if (placedModule != null)
            {
                ConstructionManager manager = GetCachedConstructionManager();
                if (manager != null)
                {
                    manager.RegisterModule(placedModule, data);
                }
            }

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
            if (!_isSnapped || !_shinobuHasSnappedPose)
                return false;

            placePos = _shinobuSnappedPosePosition;
            placeRot = _shinobuSnappedPoseRotation;
            ApplyStructuralPlacementQuantization(ref placePos, ref placeRot);
            _builderGhostPreviewPosition = placePos;
            _builderGhostPreviewRotation = placeRot;
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
                IPhysicsService physicsService = GlobalRegistry.Physics;
                if (physicsService != null && physicsService.ApplyKinematicWeldSnap(body, placePos, placeRot))
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
            Hecton8.Core.H8Debug.Log(_builderHudBuffer.ToString());
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
            Hecton8.Core.H8Debug.LogWarning(_builderHudBuffer.ToString());
#endif
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

        private void RefreshActiveBuildReadiness()
        {
            _cachedHasResourcesForActiveBuildable = ComputeActiveBuildableResourceSnapshot();
            _cachedBuildReadiness = ComputeActiveBuildReadinessSnapshot(_cachedHasResourcesForActiveBuildable);
        }

        private bool ComputeActiveBuildableResourceSnapshot()
        {
            return activeBuildable != null &&
                   IsBuildableBlueprintViewable(activeBuildable) &&
                   inventory != null &&
                   HasResources(activeBuildable);
        }

        private BuildReadiness ComputeActiveBuildReadinessSnapshot(bool hasResources)
        {
            if (activeBuildable == null)
                return BuildReadiness.NoSelection;

            if (!IsBuildableBlueprintViewable(activeBuildable))
                return BuildReadiness.BlueprintLocked;

            if (inventory == null || playerCamera == null)
                return BuildReadiness.Offline;

            if (!hasResources)
                return BuildReadiness.MissingCost;

            if (!_builderGhostPreviewActive)
                return BuildReadiness.Ready;

            if (!_builderGhostPreviewCanBuild ||
                !_semanticPlacementValid ||
                !_terrainSdfPlacementValid ||
                !_integrityPlacementValid)
            {
                return BuildReadiness.PlacementBlocked;
            }

            return _isSnapped ? BuildReadiness.SnappedReady : BuildReadiness.Ready;
        }

        private void CacheActivePlacementRule()
        {
            _activePlacementRuleKind = PlacementRuleKindNone;
            _activeDeepDrillRule = null;
            _activeAutonomousExtractorRule = null;
            _semanticPlacementValid = true;
            _semanticPlacementBlockReason = string.Empty;

            if (activeBuildable == null || activeBuildable.finalPrefab == null)
                return;

            if (activeBuildable.finalPrefab.TryGetComponent(out DeepDrillModule deepDrillRule))
            {
                _activePlacementRuleKind = PlacementRuleKindDeepDrill;
                _activeDeepDrillRule = deepDrillRule;
                return;
            }

            if (activeBuildable.finalPrefab.TryGetComponent(out AutonomousExtractorModule extractorRule))
            {
                _activePlacementRuleKind = PlacementRuleKindAutonomousExtractor;
                _activeAutonomousExtractorRule = extractorRule;
            }
        }

        private bool UpdateSemanticPlacementState()
        {
            if (_activePlacementRuleKind == PlacementRuleKindNone || !_builderGhostPreviewActive)
            {
                _semanticPlacementValid = true;
                _semanticPlacementBlockReason = string.Empty;
                return true;
            }

            switch (_activePlacementRuleKind)
            {
                case PlacementRuleKindDeepDrill:
                    _semanticPlacementValid = _activeDeepDrillRule != null &&
                        _activeDeepDrillRule.ValidatePlacementWithService(
                            _cachedInteractionSignalService,
                            _builderGhostPreviewPosition,
                            _builderGhostPreviewRotation,
                            out _semanticPlacementBlockReason);
                    break;

                case PlacementRuleKindAutonomousExtractor:
                    _semanticPlacementValid = _activeAutonomousExtractorRule != null &&
                        _activeAutonomousExtractorRule.ValidatePlacementWithRuntime(
                            _builderGhostPreviewPosition,
                            _builderGhostPreviewRotation,
                            _cachedAutonomousExtractorSystem,
                            out _semanticPlacementBlockReason);
                    break;

                default:
                    _semanticPlacementValid = true;
                    _semanticPlacementBlockReason = string.Empty;
                    break;
            }

            if (_semanticPlacementValid)
                _semanticPlacementBlockReason = string.Empty;

            return _semanticPlacementValid;
        }

        private bool TryUpdateShinobuSocketAlignment(
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

            float quality = ShinobuSocketConstructionRuntime.ResolveGlobalQualityWeight();
            ConstructionSocketTuningDTO tuning = ShinobuSocketConstructionRuntime.GetTuning();
            return TryUpdateShinobuSocketAlignmentFromVault(
                rawTargetPoint,
                activeUnsnapRadius,
                ghostSockets,
                quality,
                tuning,
                out bestDistanceSq,
                out bestTargetTransform,
                out bestAlignedPosition,
                out bestAlignedRotation);
        }

        private bool TryUpdateShinobuSocketAlignmentFromVault(
            Vector3 rawTargetPoint,
            float activeUnsnapRadius,
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

            if (ghostSockets == null ||
                ghostSockets.Length == 0 ||
                !TryResolveShinobuSocketVault(out IDataVault vault) ||
                !ShinobuSocketConstructionRuntime.TryResolveVaultViews(vault, out ConstructionSocketVaultViews views))
            {
                return false;
            }

            uint sceneHash = ComputeShinobuSocketVaultHash(views);
            uint queryHash = ComputeShinobuSocketQueryHash(sceneHash, rawTargetPoint, ghostSockets);
            if ((_shinobuHasSnappedPose || _shinobuSocketCachedBestDistanceSq < float.MaxValue) &&
                (_shinobuSocketSnapSceneHash != sceneHash || _shinobuSocketSnapQueryHash != queryHash))
            {
                InvalidateShinobuCachedSnapPose();
            }

            if (_shinobuSocketSnapPending)
            {
                if (TryFinalizeShinobuSocketSnap(
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
            if (!TryPrepareShinobuTargetSocketVault(views, sceneHash, out int targetCount) ||
                targetCount <= 0 ||
                !TryHydrateShinobuGhostSocketVault(rawTargetPoint, ghostSockets, views, quality, tuning, solverFrame, out int ghostCount, out double3 ghostRootAup, out quaternion ghostRootRotation) ||
                ghostCount <= 0 ||
                !TryResolveRuntimeOriginAup(out double3 runtimeOriginAup) ||
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
                RuntimeOriginAup = runtimeOriginAup,
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
            _shinobuSocketSnapSelectionGeneration = _activeBuildableGeneration;
            _shinobuSocketSnapBestResultIndex = bestResultIndex;
            _shinobuSocketSnapFrame = solverFrame;
            _shinobuSocketSnapSceneHash = sceneHash;
            _shinobuSocketSnapQueryHash = queryHash;
            _shinobuSocketSnapGhostRootAup = ghostRootAup;
            _shinobuSocketSnapStartTicks = solverStartTicks;
            H8Memory.RegisterActiveJob(SystemID.Construction, _shinobuSocketSnapHandle);

            return TryUseCachedShinobuSocketSnap(sceneHash, queryHash, out bestDistanceSq, out bestTargetTransform, out bestAlignedPosition, out bestAlignedRotation);
        }

        private bool TryFinalizeShinobuSocketSnap(
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

            if (_shinobuSocketSnapSelectionGeneration != _activeBuildableGeneration ||
                sceneHash != _shinobuSocketSnapSceneHash ||
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
                _shinobuSocketSnapSelectionGeneration != _activeBuildableGeneration ||
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
            _shinobuSnappedTargetSocketIndex = -1;
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
            _shinobuSocketSnapSelectionGeneration = 0u;
        }

        private void CompleteBuilderGhostValidationForTeardown()
        {
            if (!_builderGhostValidationPending)
                return;

            DispatcherJobFence.TryComplete(ref _builderGhostValidationHandle, forceComplete: true);
            _builderGhostValidationHandle = default;
            _builderGhostValidationPending = false;
            _builderGhostValidationSelectionGeneration = 0u;
        }

        private bool TryResolveShinobuSocketVault(out IDataVault vault)
        {
            vault = _shinobuSocketVault;
            return vault != null;
        }

        private bool TryPrepareShinobuTargetSocketVault(
            ConstructionSocketVaultViews views,
            uint sceneHash,
            out int targetSocketCount)
        {
            targetSocketCount = 0;
            if (!views.SocketStates.IsCreated ||
                !views.SocketAups.IsCreated ||
                !views.Modules.IsCreated ||
                !views.Counters.IsCreated ||
                views.Counters.Length <= 4)
            {
                return false;
            }

            int socketCapacity = math.min(views.SocketStates.Length, views.SocketAups.Length);
            int moduleCount = math.clamp(views.Counters[0], 0, views.Modules.Length);
            int socketCount = math.clamp(views.Counters[1], 0, socketCapacity);
            if (moduleCount <= 0 || socketCount <= 0)
                return false;

            if (moduleCount == _shinobuSocketVaultModuleCount &&
                _shinobuSocketVaultTargetCount > 0 &&
                _shinobuSocketVaultTargetCount == socketCount &&
                sceneHash == _shinobuSocketVaultSceneHash)
            {
                targetSocketCount = socketCount;
                return true;
            }

            _shinobuSocketVaultTopologyVersion = unchecked((uint)views.Counters[2]);
            _shinobuSocketVaultSceneHash = sceneHash;
            _shinobuSocketVaultModuleCount = moduleCount;
            _shinobuSocketVaultTargetCount = socketCount;
            _shinobuSocketVaultConnectionCount = views.Connections.IsCreated
                ? math.clamp(views.Counters[4], 0, views.Connections.Length)
                : 0;
            if (moduleCount > 0)
            {
                ConstructionSocketModuleDTO firstModule = views.Modules[0];
                _shinobuSocketVaultRootAup = firstModule.RootAup;
                _shinobuSocketVaultHasRootAup = math.all(math.isfinite(firstModule.RootAup));
            }
            else
            {
                _shinobuSocketVaultRootAup = default;
                _shinobuSocketVaultHasRootAup = false;
            }

            ApplyShinobuConnectionPairsToVault(views, socketCount);
            if (!BuildShinobuSocketCsrIndex(views, socketCount))
                return false;

            targetSocketCount = socketCount;
            return true;
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
                SocketStateDTO socket = default;
                socket.LocalOffset = new double3(local.x, local.y, local.z);
                socket.NormalDirection = finiteSocket ? rotatedNormal : float3.zero;
                socket.AllowedConnectionBitmask = ShinobuSocketConstructionRuntime.PackAllowedConnectionBitmask(
                    direction,
                    ShinobuSocketConstructionRuntime.HashCompatibility(definition.CompatibleType));
                socket.ParentModuleHash = moduleHash;
                socket.ConnectionStatus = finiteSocket
                    ? 0u
                    : ConstructionSocketFlags.NonFinite | ConstructionSocketFlags.CollisionBlocked;
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

        private static int WriteShinobuModuleSocketsToVault(
            BaseModuleTemplate template,
            uint moduleHash,
            double3 rootAup,
            quaternion moduleRotation,
            int socketStart,
            int socketCapacity,
            int consumedSocketIndex,
            ConstructionSocketVaultViews views)
        {
            BaseModuleTemplate.SocketDefinition[] sockets = template != null ? template.SocketDefinitions : null;
            if (sockets == null ||
                !views.SocketStates.IsCreated ||
                !views.SocketAups.IsCreated ||
                socketStart < 0 ||
                socketStart >= socketCapacity)
            {
                return 0;
            }

            int socketWrite = socketStart;
            int count = math.min(sockets.Length, socketCapacity - socketStart);
            for (int socketIndex = 0; socketIndex < count; socketIndex++)
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

                SocketStateDTO socket = default;
                socket.LocalOffset = new double3(local.x, local.y, local.z);
                socket.NormalDirection = finiteSocket ? rotatedNormal : float3.zero;
                socket.AllowedConnectionBitmask = ShinobuSocketConstructionRuntime.PackAllowedConnectionBitmask(
                    direction,
                    ShinobuSocketConstructionRuntime.HashCompatibility(definition.CompatibleType));
                socket.ParentModuleHash = moduleHash;
                socket.ConnectionStatus = finiteSocket
                    ? (socketIndex == consumedSocketIndex ? ConstructionSocketFlags.Connected : 0u)
                    : ConstructionSocketFlags.NonFinite | ConstructionSocketFlags.CollisionBlocked;
                views.SocketStates[socketWrite] = socket;
                views.SocketAups[socketWrite] = rootAup + new double3(rotatedOffset.x, rotatedOffset.y, rotatedOffset.z);
                socketWrite++;
            }

            return socketWrite - socketStart;
        }

        private static void ApplyShinobuConnectionPairsToVault(ConstructionSocketVaultViews views, int socketCount)
        {
            if (!views.Connections.IsCreated ||
                !views.SocketStates.IsCreated ||
                !views.Counters.IsCreated ||
                views.Counters.Length <= 4 ||
                socketCount <= 0)
            {
                return;
            }

            int count = math.clamp(views.Counters[4], 0, views.Connections.Length);
            for (int i = 0; i < count; i++)
            {
                SocketConnectionPairDTO connection = views.Connections[i];
                if ((connection.Flags & ConstructionSocketFlags.ValidSnap) == 0u ||
                    (uint)connection.TargetSocketIndex >= (uint)socketCount ||
                    (uint)connection.GhostSocketIndex >= (uint)socketCount)
                {
                    continue;
                }

                SocketStateDTO target = views.SocketStates[connection.TargetSocketIndex];
                SocketStateDTO ghost = views.SocketStates[connection.GhostSocketIndex];
                if (target.ParentModuleHash != connection.TargetModuleHash ||
                    ghost.ParentModuleHash != connection.GhostModuleHash)
                {
                    continue;
                }

                uint flags = ConstructionSocketFlags.Connected | (connection.ConnectionKind & (ConstructionSocketFlags.CorridorRoom | ConstructionSocketFlags.Hatch));
                target.ConnectionStatus |= flags;
                ghost.ConnectionStatus |= flags;
                views.SocketStates[connection.TargetSocketIndex] = target;
                views.SocketStates[connection.GhostSocketIndex] = ghost;
            }
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

            SocketStateDTO targetSocket = views.SocketStates[best.TargetSocketIndex];
            byte targetDirectionByte = ShinobuSocketConstructionRuntime.ExtractDirection(targetSocket);
            if (!TryToShinobuSocketDirection(targetDirectionByte, out ModuleSocketDirection targetDirection))
            {
                return false;
            }

            float3 matrixForward = best.SnappingMatrix.c2.xyz;
            float3 matrixUp = best.SnappingMatrix.c1.xyz;
            if (!math.all(math.isfinite(matrixForward)) ||
                !math.all(math.isfinite(matrixUp)) ||
                math.lengthsq(matrixForward) <= 0.000001f ||
                math.lengthsq(matrixUp) <= 0.000001f)
            {
                return false;
            }

            Quaternion candidateRotation = Quaternion.LookRotation(
                new Vector3(matrixForward.x, matrixForward.y, matrixForward.z),
                new Vector3(matrixUp.x, matrixUp.y, matrixUp.z));
            double3 candidateRootAup = best.SnappedRootAup;
            if (!TryResolveRuntimeOriginAup(out double3 runtimeOriginAup))
                return false;

            double3 candidateRuntimeDouble = candidateRootAup - runtimeOriginAup;
            if (!math.all(math.isfinite(candidateRootAup)) ||
                !math.all(math.isfinite(candidateRuntimeDouble)) ||
                math.any(math.abs(candidateRuntimeDouble) > (double)float.MaxValue))
            {
                return false;
            }

            bestTargetTransform = null;
            bestAlignedPosition = new Vector3(
                (float)candidateRuntimeDouble.x,
                (float)candidateRuntimeDouble.y,
                (float)candidateRuntimeDouble.z);
            bestAlignedRotation = candidateRotation;
            _shinobuHasSnappedPose = true;
            _shinobuSnappedPosePosition = bestAlignedPosition;
            _shinobuSnappedPoseRotation = bestAlignedRotation;
            _shinobuSnappedTargetTransform = bestTargetTransform;
            _shinobuSnappedTargetSocketIndex = best.TargetSocketIndex;
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

        private static uint ComputeShinobuSocketVaultHash(ConstructionSocketVaultViews views)
        {
            uint hash = 2166136261u;
            if (!views.Counters.IsCreated || views.Counters.Length <= 4)
                return hash;

            int moduleCount = views.Modules.IsCreated
                ? math.clamp(views.Counters[0], 0, views.Modules.Length)
                : math.max(0, views.Counters[0]);
            int socketCount = views.SocketStates.IsCreated
                ? math.clamp(views.Counters[1], 0, views.SocketStates.Length)
                : math.max(0, views.Counters[1]);
            int connectionCount = views.Connections.IsCreated
                ? math.clamp(views.Counters[4], 0, views.Connections.Length)
                : math.max(0, views.Counters[4]);

            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, (uint)moduleCount);
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, (uint)socketCount);
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, unchecked((uint)views.Counters[2]));
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, unchecked((uint)views.Counters[3]));
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, (uint)connectionCount);

            if (!views.Modules.IsCreated)
                return hash;

            for (int i = 0; i < moduleCount; i++)
            {
                ConstructionSocketModuleDTO module = views.Modules[i];
                hash = ShinobuSocketConstructionRuntime.FoldHash(hash, module.ModuleHash);
                hash = ShinobuSocketConstructionRuntime.FoldHash(hash, unchecked((uint)module.SocketStart));
                hash = ShinobuSocketConstructionRuntime.FoldHash(hash, unchecked((uint)module.SocketCount));
                hash = ShinobuSocketConstructionRuntime.FoldHash(hash, module.TopologyVersion);
                hash = ShinobuSocketConstructionRuntime.FoldHash(hash, module.Flags);
            }

            if (views.Connections.IsCreated)
            {
                for (int i = 0; i < connectionCount; i++)
                {
                    SocketConnectionPairDTO connection = views.Connections[i];
                    hash = ShinobuSocketConstructionRuntime.FoldHash(hash, unchecked((uint)connection.TargetSocketIndex));
                    hash = ShinobuSocketConstructionRuntime.FoldHash(hash, unchecked((uint)connection.GhostSocketIndex));
                    hash = ShinobuSocketConstructionRuntime.FoldHash(hash, connection.TargetModuleHash);
                    hash = ShinobuSocketConstructionRuntime.FoldHash(hash, connection.GhostModuleHash);
                    hash = ShinobuSocketConstructionRuntime.FoldHash(hash, connection.ResultHash);
                    hash = ShinobuSocketConstructionRuntime.FoldHash(hash, connection.Flags);
                }
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

        private static uint ComputeBuilderGhostValidationQueryHash(
            uint moduleHash,
            Vector3 previewPosition,
            Quaternion previewRotation,
            Vector3 boundsCenter,
            Vector3 boundsSize,
            uint validationFlags)
        {
            uint hash = ShinobuSocketConstructionRuntime.FoldHash(2166136261u, moduleHash);
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(previewPosition.x));
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(previewPosition.y));
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(previewPosition.z));
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(previewRotation.x));
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(previewRotation.y));
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(previewRotation.z));
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(previewRotation.w));
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(boundsCenter.x));
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(boundsCenter.y));
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(boundsCenter.z));
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(boundsSize.x));
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(boundsSize.y));
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, math.asuint(boundsSize.z));
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, validationFlags);
            return hash;
        }

        private static uint ResolveShinobuModuleHash(BuildableData data)
        {
            if (data == null)
                return 0u;

            uint hash = unchecked((uint)data.ModuleHashId);
            if (hash == 0u && data.ModuleTemplate != null)
                hash = unchecked((uint)data.ModuleTemplate.ResolvePersistentHashId());
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

        private bool TryCommitShinobuSnapOccupancy(Vector3 placedRootPosition, Quaternion placedRootRotation)
        {
            if (activeBuildable == null ||
                activeBuildable.ModuleTemplate == null ||
                activeBuildable.ModuleTemplate.SocketDefinitions == null ||
                _shinobuSnappedTargetSocketIndex < 0 ||
                (uint)_shinobuSnappedGhostSocketIndex >= (uint)activeBuildable.ModuleTemplate.SocketDefinitions.Length ||
                !TryResolveShinobuSocketVault(out IDataVault vault) ||
                !ShinobuSocketConstructionRuntime.TryResolveVaultViews(vault, out ConstructionSocketVaultViews views) ||
                !views.SocketStates.IsCreated ||
                !views.SocketAups.IsCreated ||
                !views.Modules.IsCreated ||
                !views.Connections.IsCreated ||
                !views.Counters.IsCreated ||
                views.Counters.Length <= 4)
            {
                return false;
            }

            int socketCapacity = math.min(views.SocketStates.Length, views.SocketAups.Length);
            int targetSocketCount = math.clamp(views.Counters[1], 0, socketCapacity);
            if ((uint)_shinobuSnappedTargetSocketIndex >= (uint)targetSocketCount)
                return false;

            int moduleCount = math.clamp(views.Counters[0], 0, views.Modules.Length);
            if (moduleCount >= views.Modules.Length || targetSocketCount >= socketCapacity)
                return false;

            int connectionCount = math.clamp(views.Counters[4], 0, views.Connections.Length);
            if (connectionCount >= views.Connections.Length)
            {
                views.Counters[3] = (int)ConstructionSocketFlags.CapacityExceeded;
                return false;
            }

            if (!math.isfinite(placedRootRotation.x) ||
                !math.isfinite(placedRootRotation.y) ||
                !math.isfinite(placedRootRotation.z) ||
                !math.isfinite(placedRootRotation.w) ||
                !TryResolveConstructionPivotAup(placedRootPosition, out double3 rootAup))
            {
                return false;
            }

            BaseModuleTemplate template = activeBuildable.ModuleTemplate;
            quaternion rawModuleRotation = new quaternion(
                placedRootRotation.x,
                placedRootRotation.y,
                placedRootRotation.z,
                placedRootRotation.w);
            float rotationLengthSq = math.lengthsq(rawModuleRotation.value);
            if (!math.isfinite(rotationLengthSq) || rotationLengthSq <= 0.00000001f)
                return false;

            quaternion moduleRotation = new quaternion(rawModuleRotation.value * math.rsqrt(math.max(rotationLengthSq, 0.00000001f)));
            uint moduleHash = ResolveShinobuModuleHash(activeBuildable);
            int socketStart = targetSocketCount;
            int placedSocketCount = template.SocketDefinitions.Length;
            if (placedSocketCount <= 0 || targetSocketCount > socketCapacity - placedSocketCount)
                return false;

            int writtenSockets = WriteShinobuModuleSocketsToVault(
                template,
                moduleHash,
                rootAup,
                moduleRotation,
                socketStart,
                socketCapacity,
                _shinobuSnappedGhostSocketIndex,
                views);
            if (writtenSockets <= 0 || _shinobuSnappedGhostSocketIndex >= writtenSockets)
                return false;

            SocketStateDTO targetSocket = views.SocketStates[_shinobuSnappedTargetSocketIndex];
            targetSocket.ConnectionStatus |= ConstructionSocketFlags.Connected;
            views.SocketStates[_shinobuSnappedTargetSocketIndex] = targetSocket;

            uint topologyFlags = ConstructionSocketFlags.TopologyDirty | ConstructionSocketFlags.RollbackFence;
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
            module.SocketCount = writtenSockets;
            module.Flags = topologyFlags;
            module.TopologyVersion = unchecked(_shinobuSocketVaultTopologyVersion + 1u);
            module.DearLieDampen = _shinobuDearLieDampen;
            module.ConnectedMask = 0u;
            module.SceneModuleListIndex = -1;
            views.Modules[moduleCount] = module;

            int ghostSocketGlobalIndex = socketStart + _shinobuSnappedGhostSocketIndex;
            if (!TryWriteShinobuConnectionPair(views, _shinobuSnappedTargetSocketIndex, ghostSocketGlobalIndex, targetSocket.ParentModuleHash, moduleHash))
            {
                topologyFlags |= ConstructionSocketFlags.CapacityExceeded;
                module.Flags = topologyFlags;
                views.Modules[moduleCount] = module;
            }

            _shinobuSocketVaultConnectionCount = views.Counters[4];

            int newSocketCount = targetSocketCount + writtenSockets;
            _shinobuSocketVaultTopologyVersion = unchecked(_shinobuSocketVaultTopologyVersion + 1u);
            views.Counters[0] = moduleCount + 1;
            views.Counters[1] = newSocketCount;
            views.Counters[2] = unchecked((int)_shinobuSocketVaultTopologyVersion);
            views.Counters[3] = (int)topologyFlags;

            _shinobuSocketVaultSceneHash = ComputeShinobuSocketVaultHash(views);
            _shinobuSocketVaultModuleCount = moduleCount + 1;
            _shinobuSocketVaultTargetCount = newSocketCount;
            _shinobuSocketVaultHasRootAup = true;
            _shinobuSocketVaultRootAup = rootAup;
            ApplyShinobuConnectionPairsToVault(views, newSocketCount);
            return BuildShinobuSocketCsrIndex(views, newSocketCount);
        }

        private static bool TryWriteShinobuConnectionPair(
            ConstructionSocketVaultViews views,
            int targetSocketIndex,
            int ghostSocketIndex,
            uint targetModuleHash,
            uint ghostModuleHash)
        {
            if (!views.Connections.IsCreated || !views.Counters.IsCreated || views.Counters.Length <= 4)
                return false;

            int connectionIndex = math.clamp(views.Counters[4], 0, views.Connections.Length);
            if (connectionIndex >= views.Connections.Length)
            {
                views.Counters[3] = (int)ConstructionSocketFlags.CapacityExceeded;
                return false;
            }

            uint hash = ShinobuSocketConstructionRuntime.FoldHash(2166136261u, (uint)targetSocketIndex);
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, (uint)ghostSocketIndex);
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, targetModuleHash);
            hash = ShinobuSocketConstructionRuntime.FoldHash(hash, ghostModuleHash);

            SocketConnectionPairDTO pair = default;
            pair.TargetSocketIndex = targetSocketIndex;
            pair.GhostSocketIndex = ghostSocketIndex;
            pair.TargetModuleHash = targetModuleHash;
            pair.GhostModuleHash = ghostModuleHash;
            pair.ConnectionKind = 0u;
            pair.Flags = ConstructionSocketFlags.ValidSnap;
            pair.ResultHash = hash;
            views.Connections[connectionIndex] = pair;
            views.Counters[4] = connectionIndex + 1;
            return true;
        }

        private bool UpdatePlacementValidityState()
        {
            bool semanticValid = UpdateSemanticPlacementState();
            bool terrainValid = UpdateTerrainSdfPlacementState();
            bool finalValid = semanticValid && terrainValid && _integrityPlacementValid;
            _builderGhostPreviewCanBuild = finalValid;
            RefreshActiveBuildReadiness();

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
                    out ConstructionTerrainSampler worldSampler))
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

            if (TryFindOccupiedConstructionGridCellInSocketVault(
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

            if (TryRunBuilderGhostBurstValidation(
                    template,
                    _builderGhostPreviewPosition,
                    _builderGhostPreviewRotation,
                    moduleHash: ResolveShinobuModuleHash(activeBuildable),
                    out BuilderGhostStateDTO builderGhostState))
            {
                if ((builderGhostState.ValidationFlags & BuilderGhostValidationFlags.NonFinite) != 0u)
                {
                    ModularBaseConstructionValidator.ApplyFailureFlags(
                        ref result,
                        in request,
                        (uint)ConstructionValidationFlags.NonFiniteInput,
                        result.MinSdfDistance,
                        result.OccupiedCellHash);
                }

                if ((builderGhostState.ValidationFlags & BuilderGhostValidationFlags.SdfBlocked) != 0u)
                {
                    ModularBaseConstructionValidator.ApplyFailureFlags(
                        ref result,
                        in request,
                        (uint)ConstructionValidationFlags.TerrainIntersection,
                        math.min(result.MinSdfDistance, -0.01f),
                        result.OccupiedCellHash);
                }

                if ((builderGhostState.ValidationFlags & BuilderGhostValidationFlags.BoundsBlocked) != 0u)
                {
                    ModularBaseConstructionValidator.ApplyFailureFlags(
                        ref result,
                        in request,
                        (uint)ConstructionValidationFlags.OccupiedGridCell,
                        result.MinSdfDistance,
                        result.OccupiedCellHash);
                }
            }

            _lastConstructionValidationRequest = request;
            _lastConstructionValidationBounds = bounds;
            _lastConstructionValidationSettings = settings;
            _lastConstructionValidationResult = result;
            _lastConstructionWorldSampler = worldSampler;

            IDataVault telemetryVault = _shinobuSocketVault;
            ModularBaseConstructionValidator.TryWriteTelemetryToVault(
                telemetryVault,
                settings.Frame != 0u ? settings.Frame : CaptureShinobuFrameId(),
                in request,
                in result,
                0f,
                0u);

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
            out ConstructionTerrainSampler worldSampler)
        {
            request = default;
            bounds = default;
            settings = ModularBaseConstructionValidator.GetTunerSettings();
            sipBudget = default;
            worldSampler = default;

            if (template == null || activeBuildable == null)
                return false;

            if (!ModularBaseConstructionValidator.TryReadTunerSettingsFromVault(_shinobuSocketVault, out settings))
                settings = ModularBaseConstructionValidator.GetTunerSettings();
            float gridSize = ResolveConstructionGridSize();
            if (!TryResolveConstructionPivotAup(previewPosition, out double3 pivotAup))
                return false;

            double3 rootAup = TryUpdateConstructionRootAupFromSocketVault(out double3 vaultRootAup)
                ? vaultRootAup
                : BuildFallbackConstructionRootAup(previewPosition);
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
            settings.Frame = CaptureShinobuFrameId();
            settings.CandidatePortMask = ToConstructionPortMask(template.SocketMask);

            bounds = ModularBaseConstructionValidator.BuildBounds(
                (float3)template.ProxyBoundsCenter,
                (float3)template.ProxyBoundsSize,
                moduleHash);

            float localBottomY = ModularBaseConstructionValidator.GridToLocal(in request, gridSize).y +
                                 template.ProxyBoundsCenter.y -
                                 template.ProxyBoundsSize.y * 0.5f;
            worldSampler = ModularBaseConstructionValidator.CreateTerrainSampler(
                rootAup,
                localBottomY - settings.TerrainClearanceMargin,
                moduleHash);

            sipBudget.TotalBaseSIP = structuralIntegrityBudget;
            sipBudget.AddedSIPCost = EstimateAddedSipCost(template);
            sipBudget.DepthPressure = EstimateDepthPressure(pivotAup);
            sipBudget.StructuralWarningRatio = 1f;
            sipBudget.BaseHash = moduleHash;
            sipBudget.Flags = 0u;
            return true;
        }

        private bool TryFindOccupiedConstructionGridCellInSocketVault(
            in ConstructionRequestDTO request,
            in ConstructionValidationSettingsDTO settings,
            out int occupiedCellHash)
        {
            occupiedCellHash = 0;
            float gridSize = settings.GridSizeMeters > 0.001f ? settings.GridSizeMeters : ResolveConstructionGridSize();
            if (!TryResolveShinobuSocketVault(out IDataVault vault) ||
                !ShinobuSocketConstructionRuntime.TryResolveVaultViews(vault, out ConstructionSocketVaultViews views) ||
                !views.Modules.IsCreated ||
                !views.Counters.IsCreated ||
                views.Counters.Length <= 0)
            {
                return false;
            }

            int moduleCount = math.clamp(views.Counters[0], 0, views.Modules.Length);
            for (int i = 0; i < moduleCount; i++)
            {
                ConstructionSocketModuleDTO module = views.Modules[i];
                if (!math.all(math.isfinite(module.RootAup)))
                    continue;

                if (!ModularBaseConstructionValidator.TryBuildRequestFromAup(
                        request.RootAUP,
                        module.RootAup,
                        module.ModuleHash,
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
            int probeCount = ModularBaseConstructionValidator.ResolveTerrainProbeCount();
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

                if (!HectonVoxelVolume.TryReadRuntimeSdfDensity(probeRuntime, out float density))
                    continue;

                if (density > 0f)
                {
                    maxDensity = math.max(maxDensity, density);
                    return true;
                }
            }

            return false;
        }

        private bool TryRunBuilderGhostBurstValidation(
            BaseModuleTemplate template,
            Vector3 previewPosition,
            Quaternion previewRotation,
            uint moduleHash,
            out BuilderGhostStateDTO state)
        {
            state = default;
            if (template == null)
                return false;

            if (!TryResolveShinobuSocketVault(out IDataVault vault) ||
                !ShinobuSocketConstructionRuntime.TryResolveVaultViews(vault, out ConstructionSocketVaultViews views) ||
                !views.BuilderGhostStates.IsCreated ||
                !views.BuilderGhostVisuals.IsCreated ||
                !views.BuilderGhostSdfSamples.IsCreated)
            {
                return false;
            }

            uint flags = BuilderGhostValidationFlags.Active |
                         BuilderGhostValidationFlags.PresentationOnly |
                         BuilderGhostValidationFlags.RollbackExcluded |
                         BuilderGhostValidationFlags.GridSnapped;
            if (_isSnapped)
                flags |= BuilderGhostValidationFlags.SocketSnap | BuilderGhostValidationFlags.DearLieActive;
            uint queryHash = ComputeBuilderGhostValidationQueryHash(
                moduleHash,
                previewPosition,
                previewRotation,
                template.ProxyBoundsCenter,
                template.ProxyBoundsSize,
                flags);
            if (TryFinalizeBuilderGhostValidation(views, queryHash, out state))
                return true;

            if (_builderGhostValidationPending)
                return false;

            float quality = ShinobuSocketConstructionRuntime.ResolveGlobalQualityWeight();
            if (!TryHydrateBuilderGhostSdfSamples(
                    template,
                    previewPosition,
                    previewRotation,
                    views.BuilderGhostSdfSamples,
                    out float minSdf,
                    out uint solidCornerCount))
            {
                return false;
            }

            Vector3 centerRuntime = previewPosition + (previewRotation * template.ProxyBoundsCenter);
            if (!TryResolveConstructionPivotAup(centerRuntime, out double3 centerAup) ||
                !TryResolveConstructionPivotAup(Vector3.zero, out double3 originAup))
            {
                return false;
            }

            quaternion rotation = new quaternion(previewRotation.x, previewRotation.y, previewRotation.z, previewRotation.w);

            long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            uint frame = CaptureShinobuFrameId();
            BuildBuilderGhostStateJob buildJob = new BuildBuilderGhostStateJob
            {
                States = views.BuilderGhostStates,
                Visuals = views.BuilderGhostVisuals,
                TargetAup = centerAup,
                RuntimeOriginAup = originAup,
                Rotation = rotation,
                BoundsScale = (float3)Vector3.Max(template.ProxyBoundsSize, Vector3.one * 0.001f),
                GridSizeMeters = ResolveConstructionGridSize(),
                PrefabHashID = moduleHash,
                ValidationFlags = flags,
                AnimationPhase = ResolveShinobuAnimationPhase(frame),
                GlobalQualityWeight = quality,
                DearLieDampen = _shinobuDearLieDampen,
                DearLieWiggleSpeed = ShinobuSocketConstructionRuntime.GetTuning().DearLieWiggleSpeed,
                ValidColor = new float4(0.08f, 1f, 0.72f, 0.72f),
                InvalidColor = new float4(1f, 0.18f, 0.12f, 0.78f),
                Frame = frame,
                StateIndex = 0
            };
            JobHandle buildHandle = buildJob.Schedule();

            ValidateBuilderGhostPlacementJob validateJob = new ValidateBuilderGhostPlacementJob
            {
                States = views.BuilderGhostStates,
                Visuals = views.BuilderGhostVisuals,
                ExistingBounds = views.Bounds,
                VoxelSdfSamples = views.BuilderGhostSdfSamples,
                BoundsExtents = (float3)(Vector3.Max(template.ProxyBoundsSize, Vector3.one * 0.001f) * 0.5f),
                ExistingCount = views.Counters.IsCreated && views.Counters.Length > 0 ? math.max(0, views.Counters[0]) : math.max(0, _shinobuSocketVaultModuleCount),
                SolidSdfThreshold = 0f,
                GlobalQualityWeight = quality,
                StateIndex = 0
            };
            _builderGhostValidationHandle = validateJob.Schedule(buildHandle);
            H8Memory.RegisterActiveJob(SystemID.Construction, _builderGhostValidationHandle);
            _builderGhostValidationPending = true;
            _builderGhostValidationSelectionGeneration = _activeBuildableGeneration;
            _builderGhostValidationQueryHash = queryHash;
            _builderGhostValidationFrame = frame;
            _builderGhostValidationStartTicks = startTicks;
            _builderGhostValidationQuality = quality;
            _builderGhostValidationMinSdf = minSdf;
            _builderGhostValidationSolidCornerCount = solidCornerCount;
            return false;
        }

        private bool TryFinalizeBuilderGhostValidation(
            ConstructionSocketVaultViews views,
            uint queryHash,
            out BuilderGhostStateDTO state)
        {
            state = default;
            if (!_builderGhostValidationPending)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _builderGhostValidationHandle))
                return false;

            _builderGhostValidationHandle = default;
            _builderGhostValidationPending = false;
            if (_builderGhostValidationSelectionGeneration != _activeBuildableGeneration ||
                queryHash != _builderGhostValidationQueryHash ||
                !views.BuilderGhostStates.IsCreated ||
                views.BuilderGhostStates.Length <= 0)
            {
                return false;
            }

            state = views.BuilderGhostStates[0];
            long endTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            float solverMicroseconds = (float)((endTicks - _builderGhostValidationStartTicks) * 1000000.0 / System.Diagnostics.Stopwatch.Frequency);
            if (views.HolographyTelemetry.IsCreated)
            {
                ShinobuSocketConstructionRuntime.WriteHolographyTelemetry(
                    views.HolographyTelemetry,
                    _builderGhostValidationFrame,
                    state.AUP_TargetPosition,
                    state.PrefabHashID,
                    (uint)ShinobuSocketConstructionRuntime.BuilderGhostSdfCornerCount,
                    state.ValidationFlags,
                    solverMicroseconds,
                    _builderGhostValidationSolidCornerCount > 0u ? math.min(_builderGhostValidationMinSdf, -0.01f) : _builderGhostValidationMinSdf,
                    state.ValidationStateHash,
                    _builderGhostValidationQuality);
            }

            return true;
        }

        private bool TryHydrateBuilderGhostSdfSamples(
            BaseModuleTemplate template,
            Vector3 previewPosition,
            Quaternion previewRotation,
            NativeArray<byte> sdfSamples,
            out float minSdf,
            out uint solidCornerCount)
        {
            minSdf = float.MaxValue;
            solidCornerCount = 0u;
            if (template == null ||
                !sdfSamples.IsCreated ||
                sdfSamples.Length < ShinobuSocketConstructionRuntime.BuilderGhostSdfCornerCount)
            {
                return false;
            }

            Vector3 center = template.ProxyBoundsCenter;
            Vector3 extents = Vector3.Max(template.ProxyBoundsSize, Vector3.one * 0.001f) * 0.5f;
            for (int i = 0; i < ShinobuSocketConstructionRuntime.BuilderGhostSdfCornerCount; i++)
                sdfSamples[i] = 127;

            for (int sampleOrdinal = 0; sampleOrdinal < ShinobuSocketConstructionRuntime.BuilderGhostSdfCornerCount; sampleOrdinal++)
            {
                int i = ShinobuSocketConstructionRuntime.ResolveBuilderGhostCornerIndex(sampleOrdinal);
                float sx = (i & 1) == 0 ? -1f : 1f;
                float sy = (i & 2) == 0 ? -1f : 1f;
                float sz = (i & 4) == 0 ? -1f : 1f;
                Vector3 local = center + new Vector3(extents.x * sx, extents.y * sy, extents.z * sz);
                Vector3 runtime = previewPosition + (previewRotation * local);
                float3 runtime3 = new float3(runtime.x, runtime.y, runtime.z);
                if (!math.all(math.isfinite(runtime3)))
                {
                    sdfSamples[i] = unchecked((byte)-127);
                    solidCornerCount++;
                    minSdf = -1f;
                    continue;
                }

                if (!HectonVoxelVolume.TryReadRuntimeSdfDensity(runtime, out float density))
                {
                    sdfSamples[i] = 127;
                    continue;
                }

                minSdf = math.min(minSdf, density);
                if (density > 0f)
                {
                    sdfSamples[i] = unchecked((byte)-127);
                    solidCornerCount++;
                }
                else
                {
                    sdfSamples[i] = 127;
                }
            }

            if (minSdf == float.MaxValue)
                minSdf = 1f;
            return true;
        }

        private bool TryUpdateConstructionRootAupFromSocketVault(out double3 rootAup)
        {
            rootAup = default;
            if (TryResolveShinobuSocketVault(out IDataVault vault) &&
                ShinobuSocketConstructionRuntime.TryResolveVaultViews(vault, out ConstructionSocketVaultViews views) &&
                views.Modules.IsCreated &&
                views.Counters.IsCreated &&
                views.Counters.Length > 0)
            {
                int moduleCount = math.clamp(views.Counters[0], 0, views.Modules.Length);
                for (int i = 0; i < moduleCount; i++)
                {
                    double3 candidate = views.Modules[i].RootAup;
                    if (!math.all(math.isfinite(candidate)))
                        continue;

                    _shinobuSocketVaultRootAup = candidate;
                    _shinobuSocketVaultHasRootAup = true;
                    rootAup = candidate;
                    return true;
                }
            }

            if (_shinobuSocketVaultHasRootAup && math.all(math.isfinite(_shinobuSocketVaultRootAup)))
            {
                rootAup = _shinobuSocketVaultRootAup;
                return true;
            }

            return false;
        }

        private static double3 BuildFallbackConstructionRootAup(Vector3 fallbackRuntimePosition)
        {
            if (TryResolveConstructionPivotAup(fallbackRuntimePosition, out double3 fallbackAup))
                return fallbackAup;

            return TryResolveRuntimeOriginAup(out double3 originAup) ? originAup : double3.zero;
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

            if (!TryResolveRuntimeOriginAup(out double3 originAup))
                return false;

            pivotAup = originAup + new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            return math.all(math.isfinite(pivotAup));
        }

        private static bool TryResolveRuntimeOriginAup(out double3 originAup)
        {
            originAup = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            return math.all(math.isfinite(originAup));
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

        private float EstimateDepthPressure(double3 pivotAup)
        {
            float depthMeters = math.isfinite(pivotAup.y)
                ? (float)math.max(0d, ResolveBuilderSeaLevelAupY() - pivotAup.y)
                : 0f;
            return depthMeters * 0.0125f;
        }

        private double ResolveBuilderSeaLevelAupY()
        {
            IHectonOceanKinematicsService oceanKinematicsService = _cachedOceanKinematicsService;
            IHectonOceanKinematics oceanKinematics = oceanKinematicsService != null && oceanKinematicsService.IsInitialized
                ? oceanKinematicsService.ActiveProvider
                : null;
            if (oceanKinematics != null &&
                oceanKinematics.IsAvailable &&
                TryResolveSeaLevelAupY(oceanKinematics.SeaLevel, out double seaLevelAupY))
            {
                return seaLevelAupY;
            }

            return DefaultSeaLevelAupY;
        }

        private static bool TryResolveSeaLevelAupY(float candidateSeaLevelY, out double seaLevelAupY)
        {
            if (math.isfinite(candidateSeaLevelY) &&
                math.abs(candidateSeaLevelY) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                seaLevelAupY = candidateSeaLevelY;
                return true;
            }

            seaLevelAupY = DefaultSeaLevelAupY;
            return false;
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
            signal.Frame = CaptureShinobuFrameId();
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
            SignalBus<ConstructionPreviewSignal>.TryPushTracked(in signal, ref s_x001PlayerBuilderSignalPushDropCount);
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
                ConstructionManager constructionManager = GetCachedConstructionManager();
                if (_habitatConstructionManager.ScheduleIntegrityValidation(
                        constructionManager,
                        activeBuildable,
                        _builderGhostPreviewPosition,
                        _builderGhostPreviewRotation,
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

            ConstructionManager constructionManager = GetCachedConstructionManager();
            snapshot.Buildable = activeBuildable;
            snapshot.TargetSocketIndex = _shinobuSnappedTargetSocketIndex;
            snapshot.ModuleCount = constructionManager != null ? constructionManager.ModuleCount : 0;
            snapshot.Position = _builderGhostPreviewPosition;
            snapshot.Rotation = _builderGhostPreviewRotation;
            return true;
        }

        private static bool AreEquivalentSnapshots(ValidationSnapshot lhs, ValidationSnapshot rhs)
        {
            return ReferenceEquals(lhs.Buildable, rhs.Buildable) &&
                   lhs.TargetSocketIndex == rhs.TargetSocketIndex &&
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

            Span<int> costHashes = stackalloc int[BuildCostDigestCapacity];
            Span<int> costAmounts = stackalloc int[BuildCostDigestCapacity];
            Span<int> costIndices = stackalloc int[BuildCostDigestCapacity];
            int groupedCostCount = PrepareBuildCostDigestGroups(data, costHashes, costAmounts, costIndices);
            if (groupedCostCount < 0)
            {
                AppendText(ref buffer, "COST OVERFLOW");
                return;
            }

            bool wroteAny = false;
            for (int i = 0; i < groupedCostCount; i++)
            {
                InventoryCost cost = data.buildCost[costIndices[i]];
                if (cost == null || cost.item == null)
                    continue;

                if (wroteAny)
                    AppendText(ref buffer, " | ");

                string itemName = string.IsNullOrWhiteSpace(cost.item.itemName) ? cost.item.name : cost.item.itemName;
                AppendUpperInvariant(ref buffer, itemName);
                AppendText(ref buffer, " ");

                int available = inventory != null
                    ? inventory.CountAvailableTotal(costHashes[i])
                    : 0;
                buffer.AppendInt(available);
                AppendText(ref buffer, "/");
                buffer.AppendInt(costAmounts[i]);
                wroteAny = true;
            }

            if (!wroteAny)
                AppendText(ref buffer, "NO COST");
        }

        private static int PrepareBuildCostDigestGroups(
            BuildableData data,
            Span<int> costHashes,
            Span<int> costAmounts,
            Span<int> costIndices)
        {
            if (data == null || data.buildCost == null || data.buildCost.Count == 0)
                return 0;

            int groupedCount = 0;
            int sourceCount = data.buildCost.Count;
            for (int i = 0; i < sourceCount; i++)
            {
                InventoryCost cost = data.buildCost[i];
                if (cost == null || cost.item == null || cost.amount <= 0)
                    continue;

                int itemHashId = ItemData.ResolvePersistentHashId(cost.item);
                if (itemHashId == 0)
                    continue;

                int groupIndex = FindBuildCostDigestGroup(costHashes, groupedCount, itemHashId);
                if (groupIndex < 0)
                {
                    if (groupedCount >= costHashes.Length ||
                        groupedCount >= costAmounts.Length ||
                        groupedCount >= costIndices.Length)
                    {
                        return -1;
                    }

                    groupIndex = groupedCount;
                    costHashes[groupIndex] = itemHashId;
                    costAmounts[groupIndex] = 0;
                    costIndices[groupIndex] = i;
                    groupedCount++;
                }

                int current = costAmounts[groupIndex];
                if (current > int.MaxValue - cost.amount)
                    return -1;

                costAmounts[groupIndex] = current + cost.amount;
            }

            return groupedCount;
        }

        private static int FindBuildCostDigestGroup(Span<int> costHashes, int groupedCount, int itemHashId)
        {
            for (int i = 0; i < groupedCount; i++)
            {
                if (costHashes[i] == itemHashId)
                    return i;
            }

            return -1;
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
            if (!TryGetBuildHit(ray, HectonLayerMasks.ConstructionSurfaceLayerMask, out InteractionSurfaceHit hit))
            {
                NotifyBuildBlocked("NO MODULE TARGET");
                return;
            }

            if (!TryResolveTargetModule(hit.collider, out BaseModule module))
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
            if (!TryGetBuildHit(ray, HectonLayerMasks.ConstructionSurfaceLayerMask, out InteractionSurfaceHit hit))
                return null;

            return TryResolveTargetModule(hit.collider, out BaseModule module) ? module : null;
        }

        private static bool TryResolveTargetModule(Collider collider, out BaseModule module)
        {
            module = null;
            return collider != null && LaserCutterTargetRegistry.TryResolveModule(collider, out module);
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

            IHabitatDeconstructionSystem deconstructionSystem = _cachedHabitatDeconstructionSystem;
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
                Frame = CaptureShinobuFrameId(),
                ToolKind = toolKind,
                Flags = 0
            };

            return deconstructionSystem.EnqueueDeconstruction(in request);
        }

        private bool TryGetBuildHit(Ray ray, LayerMask mask, out InteractionSurfaceHit hit)
        {
            hit = default;
            IInteractionSignalService interactionService = _cachedInteractionSignalService;
            if (interactionService == null || !interactionService.IsInitialized)
                return false;

            float3 origin = new float3(ray.origin.x, ray.origin.y, ray.origin.z);
            float3 direction = new float3(ray.direction.x, ray.direction.y, ray.direction.z);
            float directionLengthSq = math.lengthsq(direction);
            if (!math.all(math.isfinite(origin)) ||
                !math.all(math.isfinite(direction)) ||
                directionLengthSq <= 0.000001f ||
                !math.isfinite(buildDistance) ||
                buildDistance <= 0f)
            {
                return false;
            }

            direction *= math.rsqrt(math.max(directionLengthSq, 0.000001f));
            if (_buildRayRequesterId == 0UL)
                _buildRayRequesterId = EntityId.ToULong(gameObject.GetEntityId()) ^ 0x4255494C44524159UL;

            return interactionService.RequestPrimarySurfaceHit(
                _buildRayRequesterId,
                ray.origin,
                new Vector3(direction.x, direction.y, direction.z),
                buildDistance,
                mask.value,
                QueryTriggerInteraction.Ignore,
                out hit);
        }

        private bool ConsumeResources(BuildableData data)
        {
            if (_habitatConstructionManager == null)
                return false;

            return _habitatConstructionManager.ConsumeBuildResources(inventory, data);
        }

        private void PublishConstructionCommitSignals(
            GameObject placedModule,
            BuildableData data,
            Vector3 modulePosition,
            Quaternion moduleRotation)
        {
            if (placedModule == null || data == null)
                return;

            EnsureConstructionSignalLanes();
            BaseModuleTemplate template = data.ModuleTemplate;
            Vector3 localCenter = template != null ? template.ProxyBoundsCenter : Vector3.zero;
            Vector3 proxySize = template != null ? template.ProxyBoundsSize : Vector3.one;
            Vector3 centerRuntime = modulePosition + moduleRotation * localCenter;
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
            SignalBus<AcousticPingSignal>.TryPushTracked(in clunk, ref s_x001PlayerBuilderSignalPushDropCount);

            FloraExclusionSignal flora = default;
            flora.CenterAup = centerAup;
            flora.Extents = extents;
            flora.ModuleHash = moduleHash;
            flora.SourceEntityLow = sourceLow;
            flora.Frame = CaptureShinobuFrameId();
            flora.Operation = FloraExclusionSignal.OperationApply;
            flora.Flags = 0;
            SignalBus<FloraExclusionSignal>.TryPushTracked(in flora, ref s_x001PlayerBuilderSignalPushDropCount);
        }

        private uint CaptureShinobuFrameId()
        {
            uint dispatcherFrame = TimeSliceScheduler.CurrentFrameId;
            if (dispatcherFrame != 0u)
            {
                if (dispatcherFrame > _shinobuBuilderFrameCounter)
                    _shinobuBuilderFrameCounter = dispatcherFrame;
                return dispatcherFrame;
            }

            _shinobuBuilderFrameCounter = unchecked(_shinobuBuilderFrameCounter + 1u);
            return _shinobuBuilderFrameCounter != 0u ? _shinobuBuilderFrameCounter : 1u;
        }

        private static float ResolveShinobuAnimationPhase(uint frame)
        {
            return math.frac(frame * (1f / 120f));
        }

        private static uint FoldEntityId(ulong entityId)
        {
            return unchecked((uint)entityId ^ (uint)(entityId >> 32));
        }

        // ══════════════════════════════════════════════════════════
        //  AUDIO
        // ══════════════════════════════════════════════════════════

        private static IPlayerRuntimeContext ResolvePlayerRuntimeContext()
        {
            return GlobalRegistry.Player;
        }

        private static IEnvironmentRuntimeContext ResolveEnvironmentRuntimeContext()
        {
            return GlobalRegistry.Environment;
        }

        private static ConstructionManager ResolveConstructionManager()
        {
            IEnvironmentRuntimeContext environmentContext = ResolveEnvironmentRuntimeContext();
            return environmentContext != null ? environmentContext.ConstructionManager : null;
        }

        private ConstructionManager GetCachedConstructionManager()
        {
            return _cachedConstructionManager;
        }

        private static ModuleCatalog ResolveModuleCatalog()
        {
            IEnvironmentRuntimeContext environmentContext = ResolveEnvironmentRuntimeContext();
            return environmentContext != null ? environmentContext.ModuleCatalog : null;
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip == null)
                return;

            QueueBuilderAudioCue(clip);
        }

        private void QueueBuilderAudioCue(AudioClip clip)
        {
            switch (_pendingBuilderAudioCount)
            {
                case 0:
                    _pendingBuilderAudio0 = clip;
                    _pendingBuilderAudioCount = 1;
                    break;
                case 1:
                    _pendingBuilderAudio1 = clip;
                    _pendingBuilderAudioCount = 2;
                    break;
                case 2:
                    _pendingBuilderAudio2 = clip;
                    _pendingBuilderAudioCount = 3;
                    break;
                default:
                    _pendingBuilderAudio3 = clip;
                    _pendingBuilderAudioCount = 4;
                    break;
            }

            TryRegisterLateFrameTick();
        }

        private void FlushPendingBuilderAudio()
        {
            int count = _pendingBuilderAudioCount;
            if (count <= 0)
                return;

            IAudioService audioService = ResolveAudioService();
            if (audioService != null)
            {
                if (count > 0 && _pendingBuilderAudio0 != null)
                    audioService.PlayStatic2D(_pendingBuilderAudio0);
                if (count > 1 && _pendingBuilderAudio1 != null)
                    audioService.PlayStatic2D(_pendingBuilderAudio1);
                if (count > 2 && _pendingBuilderAudio2 != null)
                    audioService.PlayStatic2D(_pendingBuilderAudio2);
                if (count > 3 && _pendingBuilderAudio3 != null)
                    audioService.PlayStatic2D(_pendingBuilderAudio3);
            }

            ClearPendingBuilderAudioSync();
        }

        private void ClearPendingBuilderAudioSync()
        {
            _pendingBuilderAudio0 = null;
            _pendingBuilderAudio1 = null;
            _pendingBuilderAudio2 = null;
            _pendingBuilderAudio3 = null;
            _pendingBuilderAudioCount = 0;
        }

        private void TryRegisterLateFrameTick()
        {
            if (_lateFrameRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterLateFrameTick()
        {
            if (!_lateFrameRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _lateFrameRegistered = false;
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
            if (Application.isPlaying && _builderGhostPreviewActive)
            {
                if (_isSnapped && _shinobuHasSnappedPose)
                {
                    // Snap active — green marker at Vault-derived snapped pose.
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(_builderGhostPreviewPosition,
                                    _shinobuSnappedPosePosition);
                    Gizmos.DrawWireSphere(_shinobuSnappedPosePosition, 0.2f);
                }
            }
        }
#endif
    }
}

