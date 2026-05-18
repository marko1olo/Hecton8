// ============================================================================
// HECTON-8 — PlayerBuilder.cs
// Kontroller stroitelstva modulnoy bazy.
//
// v3.0 — SOCKET SNAP SYSTEM:
//   [ADD] Sistema magnitnogo prilipaniya k tochkam stykovki (ModuleSocket).
//   [ADD] Poisk soketov cherez Physics.OverlapSphereNonAlloc (zero GC).
//   [ADD] Gisterezis: snapRadius=2m, unsnapRadius=2.5m (bez mertsaniya).
//   [ADD] Plavnyy snap/unsnap cherez eksponentsialnoe sglazhivanie.
//   [ADD] Zanyatye sokety (IsOccupied) propuskayutsya pri poiske.
//   [ADD] Pri razmeschenii: blizhayshiy soket pomechaetsya kak occupied.
//   [ADD] socketLayerMask dlya filtratsii (Layer "Sockets").
//
//   POVEDENIE:
//     1. Raycast iz kamery → hitPoint na poverhnosti.
//     2. OverlapSphereNonAlloc vokrug hitPoint na sloe Sockets.
//     3. Esli nayden svobodnyy soket ≤ snapRadius → snap mode:
//        - Pozitsiya prizraka = socket.position
//        - Rotatsiya prizraka = socket.rotation × yawOffset
//     4. Esli rasstoyanie do snapnutogo soketa > unsnapRadius → unsnap:
//        - Plavnyy perehod obratno k raycast-pozitsii.
//     5. Gisterezis (snap=2m, unsnap=2.5m) predotvraschaet mertsanie.
//
//   ZERO GC:
//     • OverlapSphereNonAlloc → predallotsirovannyy Collider[16].
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
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Modding;
using Hecton8.Construction;
using Hecton8.Physics;
using Hecton8.UI;
using Hecton8.World;
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

        [Tooltip("Sloy soketov dlya OverlapSphereNonAlloc.\n" +
                 "Sozday Layer 'Sockets' v Project Settings → Tags & Layers.\n" +
                 "Na kazhdom ModuleSocket: SphereCollider(trigger) + Layer=Sockets.")]
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
        private RaycastHit _hit;
        private readonly RaycastHit[] _buildHits = new RaycastHit[1]; // COLD ALLOC: single surface probe for build targeting.
        private const float StructuralPlacementGridMeters = 4f;
        private const float StructuralPlacementGridInv = 0.25f;
        private const float StructuralRotationStepDegrees = 90f;
        private const float StructuralSnapRadiusMeters = 1f;
        private const float StructuralUnsnapRadiusMeters = 1.25f;
        private int _ghostYawStep;
        private const int BuildGhostProjectionInstanceCount = 1;
        private readonly Matrix4x4[] _buildGhostProjectionMatrices = new Matrix4x4[BuildGhostProjectionInstanceCount];
        private Mesh _buildGhostProjectionMesh;
        private Material _buildGhostValidProjectionMaterial;
        private Material _buildGhostBlockedProjectionMaterial;

        private static readonly Vector3 ViewportCenter = new Vector3(0.5f, 0.5f, 0f);

        // ══════════════════════════════════════════════════════════
        //  SOCKET SNAP STATE (v3.0)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Pre-allocated buffer for OverlapSphereNonAlloc.
        /// 16 soketov — pokryvaet dazhe hab s 8 vyhodami.
        /// Zero GC: massiv sozdaetsya odin raz.
        /// </summary>
        private readonly Collider[] _socketBuffer = new Collider[16];

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
        private ModuleSocket _snappedGhostSocket;
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
        public bool CanPlaceActiveBuildable => activeBuildable != null && IsBuildableBlueprintViewable(activeBuildable) && _currentGhost != null && _currentGhost.CanBuild && _semanticPlacementValid && _terrainSdfPlacementValid && _integrityPlacementValid;
        public bool HasPlacementPreview => _currentGhostObj != null;
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
                    ConstructionManager constructionManager = ResolveConstructionManager();
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
            if (_currentGhostObj != null)
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
            _ghostYawStep        = 0;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[PlayerBuilder] No buildable module assigned!");
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

            if (activeBuildable.ghostPrefab == null)
            {
                if (!ConstructionRuntimeProxyFactory.TryAcquireGhostProxy(
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
        /// Obnovlyaet pozitsiyu prizraka kazhdyy kadr.
        ///
        /// v3.0 ALGORITM:
        ///   1. Raycast iz tsentra kamery → hitPoint.
        ///   2. OverlapSphereNonAlloc vokrug hitPoint na socketLayerMask.
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
        ///   • OverlapSphereNonAlloc → _socketBuffer[16] (pre-allocated).
        ///   • TryGetComponent → zero GC.
        ///   • Struct Ray, RaycastHit, Vector3, Quaternion — stack.
        ///   • Nikakih List, LINQ, lyambd, new.
        /// </summary>
        private void UpdateGhostPosition(float dt)
        {
            if (_terrainSdfBlockHapticCooldown > 0f)
                _terrainSdfBlockHapticCooldown = math.max(0f, _terrainSdfBlockHapticCooldown - math.max(0f, dt));

            if (playerCamera == null || _currentGhostObj == null || _habitatConstructionManager == null)
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
            //  OverlapSphereNonAlloc: ischet kollaydery na sloe Sockets
            //  v radiuse vokrug hitPoint. Pre-allocated buffer → zero GC.
            //
            //  Radius poiska = unsnapRadius (bolshiy iz dvuh) dlya togo,
            //  chtoby poymat soket, ot kotorogo my mogli by otryvatsya.
            //  Fakticheskaya proverka snap/unsnap — po distantsii nizhe.
            // ═══════════════════════════════════════════════════

            float searchRadius = activeUnsnapRadius;
            int resolvedSocketMask = ResolveSocketMask();
            int socketCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                rawTargetPoint,
                searchRadius,
                _socketBuffer,
                resolvedSocketMask,
                QueryTriggerInteraction.Collide // sokety = trigger colliders
            );

            // ── Nayti blizhayshiy svobodnyy soket ──
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

                // ── Poluchaem ModuleSocket (zero GC) ──
                if (!socketCollider.TryGetComponent(out ModuleSocket socket))
                    continue;

                // ── Propuskaem zanyatye ──
                if (socket.IsOccupied)
                    continue;

                // ── Distantsiya ot hitPoint do soketa ──
                if (!_habitatConstructionManager.TryResolveSocketAlignment(
                        _currentGhostObj.transform,
                        _ghostSocketBuffer,
                        socket,
                        _ghostYawStep,
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

            // ── Ochistka bufera (predotvraschaet uderzhanie ssylok) ──
            for (int i = 0; i < socketCount; i++)
                _socketBuffer[i] = null;

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

            if (_isSnapped && _snappedSocket != null)
            {
                // ── SNAP MODE: pozitsiya i rotatsiya ot soketa ──
                targetPos = bestAlignedPosition;

                // Socket.forward = napravlenie stykovki.
                // YawOffset pozvolyaet igroku vraschat modul
                // vokrug osi stykovki (esli nuzhno).
                targetRot = bestAlignedRotation;
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

            Transform t = _currentGhostObj.transform;
            Vector3 previousPosition = t.position;
            Quaternion previousRotation = t.rotation;

            if (_isSnapped)
            {
                t.SetPositionAndRotation(targetPos, targetRot);
            }
            else
            {
                float lerpFactor = ResolveDecayBlend(ghostFollowSpeed, dt);
                t.position = Vector3.Lerp(previousPosition, targetPos, lerpFactor);
                t.rotation = NlerpRotation(previousRotation, targetRot, lerpFactor);
            }

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[PlayerBuilder] Missing resources.");
#endif
                return;
            }

            Vector3 placePos = _currentGhostObj.transform.position;
            Quaternion placeRot = _currentGhostObj.transform.rotation;
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
            PublishConstructionCommitSignals(placedModule, activeBuildable);
            PlaySound(buildSound);
            NotifyBuildPlaced(activeBuildable);

            // ── Sbros snap-sostoyaniya ──
            _isSnapped = false;
            _snappedSocketTransform = null;
            _snappedSocket = null;
            _snappedGhostSocket = null;

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
                ConstructionManager manager = ResolveConstructionManager();
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
            bool terrainValid = UpdateTerrainSdfPlacementState();
            bool finalValid = semanticValid && terrainValid && _integrityPlacementValid;

            if (_currentGhost != null)
                _currentGhost.SetExternalValidity(finalValid);

            return finalValid;
        }

        private bool UpdateTerrainSdfPlacementState()
        {
            _terrainSdfPlacementValid = true;
            _terrainSdfPlacementBlockReason = string.Empty;
            if (!IsStructuralBuildable(activeBuildable) || _currentGhostObj == null || activeBuildable.ModuleTemplate == null)
                return AcceptTerrainSdfPlacement();

            BaseModuleTemplate template = activeBuildable.ModuleTemplate;
            Vector3 proxyBoundsSize = template.ProxyBoundsSize;
            if (proxyBoundsSize.x <= 0.01f || proxyBoundsSize.y <= 0.01f || proxyBoundsSize.z <= 0.01f)
                return AcceptTerrainSdfPlacement();

            Transform ghostTransform = _currentGhostObj.transform;
            if (!TryBuildConstructionValidationPayload(
                    template,
                    ghostTransform,
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

            if (ModularBaseConstructionValidator.TryResolveTelemetryRing(GlobalRegistry.DataVault, out var telemetryRing))
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
            Transform ghostTransform,
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

            if (template == null || ghostTransform == null || activeBuildable == null)
                return false;

            ModularBaseConstructionValidator.TryReadTunerSettingsFromVault(GlobalRegistry.DataVault, out settings);
            float gridSize = ResolveConstructionGridSize();
            double3 rootAup = ResolveConstructionRootAup(ghostTransform.position);
            double3 pivotAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(ghostTransform.position);
            uint moduleHash = (uint)activeBuildable.ModuleHashId;
            uint rotation = ResolveConstructionRotationIndex(ghostTransform.rotation);
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
            ConstructionManager constructionManager = ResolveConstructionManager();
            IReadOnlyList<GameObject> modules = constructionManager != null ? constructionManager.SpawnedModules : null;
            if (modules == null)
                return false;

            float gridSize = settings.GridSizeMeters > 0.001f ? settings.GridSizeMeters : ResolveConstructionGridSize();
            int moduleCount = modules.Count;
            for (int i = 0; i < moduleCount; i++)
            {
                GameObject module = modules[i];
                if (module == null)
                    continue;

                Transform moduleTransform = module.transform;
                if (moduleTransform == null)
                    continue;

                double3 moduleAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(moduleTransform.position);
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
            ConstructionManager constructionManager = ResolveConstructionManager();
            if (constructionManager != null && constructionManager.SpawnedModules != null)
            {
                IReadOnlyList<GameObject> modules = constructionManager.SpawnedModules;
                for (int i = 0, count = modules.Count; i < count; i++)
                {
                    GameObject module = modules[i];
                    if (module != null)
                        return HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(module.transform.position);
                }
            }

            return HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(fallbackRuntimePosition);
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
            if (_currentGhostObj == null || activeBuildable == null)
                return;

            BaseModuleTemplate template = activeBuildable.ModuleTemplate;
            if (template == null)
                return;

            Vector3 proxyBoundsSize = template.ProxyBoundsSize;
            if (proxyBoundsSize.x <= 0.01f || proxyBoundsSize.y <= 0.01f || proxyBoundsSize.z <= 0.01f)
                return;

            EnsureBuildGhostProjectionResources();
            if (_buildGhostProjectionMesh == null)
                return;

            bool placementAllowed =
                _currentGhost != null &&
                _currentGhost.CanBuild &&
                _semanticPlacementValid &&
                _terrainSdfPlacementValid &&
                _integrityPlacementValid;
            Material projectionMaterial = placementAllowed
                ? _buildGhostValidProjectionMaterial
                : _buildGhostBlockedProjectionMaterial;
            if (projectionMaterial == null)
                return;

            Transform ghostTransform = _currentGhostObj.transform;
            Vector3 targetRuntime = ghostTransform.TransformPoint(template.ProxyBoundsCenter);
            double3 targetAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(targetRuntime);
            Vector3 drawPosition = HectonFloatingOrigin.ToRuntimePosition(targetAup);
            _buildGhostProjectionMatrices[0] = Matrix4x4.TRS(drawPosition, ghostTransform.rotation, proxyBoundsSize);
            PublishConstructionPreviewSignal(targetAup, ghostTransform.rotation, proxyBoundsSize, placementAllowed);

            UnityEngine.Graphics.DrawMeshInstanced(
                _buildGhostProjectionMesh,
                0,
                projectionMaterial,
                _buildGhostProjectionMatrices,
                BuildGhostProjectionInstanceCount,
                null,
                UnityEngine.Rendering.ShadowCastingMode.Off,
                false,
                _currentGhostObj.layer,
                playerCamera,
                UnityEngine.Rendering.LightProbeUsage.Off,
                null);
        }

        private void EnsureBuildGhostProjectionResources()
        {
            if (_buildGhostProjectionMesh != null &&
                _buildGhostValidProjectionMaterial != null &&
                _buildGhostBlockedProjectionMaterial != null)
            {
                return;
            }

            if (ConstructionRuntimeProxyFactory.TryGetGhostProjectionResources(
                    out Mesh projectionMesh,
                    out Material validMaterial,
                    out Material blockedMaterial))
            {
                _buildGhostProjectionMesh = projectionMesh;
                _buildGhostValidProjectionMaterial = validMaterial;
                _buildGhostBlockedProjectionMaterial = blockedMaterial;
            }
        }

        private void PublishConstructionPreviewSignal(double3 centerAup, Quaternion rotation, Vector3 proxyBoundsSize, bool placementAllowed)
        {
            EnsureConstructionSignalLanes();
            ConstructionPreviewSignal signal = default;
            signal.CenterAup = AbsoluteUniversePosition.FromAbsolutePosition(centerAup);
            signal.Rotation = new float4(rotation.x, rotation.y, rotation.z, rotation.w);
            signal.Scale = (float3)Vector3.Max(proxyBoundsSize, Vector3.one * 0.001f);
            signal.ModuleHash = activeBuildable != null ? (uint)activeBuildable.ModuleHashId : 0u;
            signal.FailureFlags = _lastConstructionValidationResult.FailureFlags;
            signal.ResultHash = _lastConstructionValidationResult.ResultHash;
            signal.Frame = unchecked((uint)Time.frameCount);
            signal.IsValid = placementAllowed ? (byte)1 : (byte)0;
            signal.Flags = ConstructionPreviewSignal.FlagActive | ConstructionPreviewSignal.FlagFallbackPreview;
            SignalBus<ConstructionPreviewSignal>.TryPush(in signal);
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

            DeconstructRequestSignal request = new DeconstructRequestSignal
            {
                TargetAup = AbsoluteUniversePosition.FromRuntimePosition(modulePosition),
                RayOriginAup = AbsoluteUniversePosition.FromRuntimePosition(rayOrigin),
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
            double3 centerAupDouble = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(centerRuntime);
            AbsoluteUniversePosition centerAup = AbsoluteUniversePosition.FromAbsolutePosition(centerAupDouble);
            float3 extents = (float3)(Vector3.Max(proxySize * 0.5f, Vector3.one * 0.5f) + Vector3.one * 0.25f);
            float radius = math.max(6f, math.cmax(extents) * 4f);
            uint sourceLow = FoldEntityId(EntityId.ToULong(placedModule.GetEntityId()));
            uint moduleHash = (uint)data.ModuleHashId;

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

