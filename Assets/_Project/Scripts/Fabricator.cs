// ============================================================================
// HECTON-8 — Fabricator.cs
// Mashina-verstak dlya krafta predmetov.
//
// REFAKTORING v3 — DINAMIChESKOE PITANIE:
//   • Realizuet IPowerComponent dlya integratsii s PowerGrid.
//   • Pri otsutstvii pitaniya kraft vstaet na PAUZU (ne otmenyaetsya).
//   • PowerRating: 0 v idle, -craftPowerDraw pri krafte.
//   • Pri vosstanovlenii pitaniya kraft prodolzhaetsya s togo zhe mesta.
//   • Pri StartCraft/CompleteCraft/CancelCraft → PowerGrid.UpdateBalance()
//     dlya mgnovennogo perescheta balansa seti.
//
// ZhIZNENNYY TsIKL KRAFTA:
//   1. Igrok navoditsya → OnHoverStart → HUD pokazyvaet prompt
//   2. [E] → Interact → CraftingEvents.TryRaiseFabricatorOpened
//   3. UI vyzyvaet StartCraft(recipe) → CanCraft proverka
//   4. Resursy spisyvayutsya SRAZU -> Vault FabricationJobDTO zapuskaetsya
//      → NotifyGridBalanceChanged() — set pereschityvaet s -100W
//   5. SIMULATION: esli HasPower -> Vault Progress01 prodvigaetsya Burst job
//               esli !HasPower -> PAUZA (Vault Progress01 ne tikaet)
//   6. Zavershenie → rezultat v inventar → OnCraftCompleted
//      → NotifyGridBalanceChanged() — set pereschityvaet bez -100W
//   7. Otmena → resursy vozvraschayutsya → OnCraftCancelled
//      → NotifyGridBalanceChanged() — set pereschityvaet bez -100W
//
// ZERO GC:
//   • Tick: float arifmetika, delegate?.Invoke (no boxing)
//   • CanCraft: for-tsikly s ReferenceEquals, no LINQ
//   • IPowerComponent svoystva: value types only
//   • PowerNode keshirovan v Awake — zero TryGetComponent v goryachem puti
// ============================================================================

using System;
using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Audio;
using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Economy;
using Hecton8.SaveSystem;
using Hecton8.Interaction;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Modding;
using Hecton8.Power;
using Hecton8.Tools;
using Hecton8.UI;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Crafting
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed partial class Fabricator : MonoBehaviour, IInteractable, IInteractableTextProvider, ISlowTickable, IUpdatable, ILateFrameTickable, IPowerComponent, IFabricator, IModRegistryEventListener, ILocalizationLanguageChangedListener, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        // COLD ALLOC: List<Fabricator>[8] - active fabricator registry for cold-path recipe lookups - owner: Fabricator
        private static readonly List<Fabricator> _activeFabricators = new List<Fabricator>(8);
        private static readonly int _uiFabricatorLocalizationHash = LocHash.Compute(LocalizationKeys.UI_FABRICATOR);
        private static readonly int _interactUseFabricatorLocalizationHash = LocHash.Compute(LocalizationKeys.INTERACT_USE_FABRICATOR);
        private static Mesh s_sharedAssemblyFallbackMesh;
        private static bool s_emergencyPowerLockActive;
        private const int InteractTextBufferCapacity = 96;
        private const string LegacyInteractText = "FABRICATOR";
        private const float ExothermicRunningHeatDeltaCelsius = 20f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Identity ──────────────────────────────────")]
        [Tooltip("Nazvanie fabrikatora dlya UI prompta")]
        [SerializeField] private string fabricatorName = "Fabrikator";

        [Header("── Recipes ───────────────────────────────────")]
        [Tooltip("Spisok dostupnyh retseptov na etom verstake")]
        [SerializeField] private List<RecipeData> availableRecipes = new List<RecipeData>();

        [Header("── Settings ──────────────────────────────────")]
        [Tooltip("Maksimalnaya distantsiya ispolzovaniya (metry). " +
                 "Esli igrok otoydet dalshe — kraft otmenyaetsya.")]
        [SerializeField] private float maxUseDistance = 3.5f;

        [Tooltip("When enabled, a completed recipe immediately queues again if unlocks, ingredients, capacity, and power still pass.")]
        [SerializeField] private bool isContinuous;

        [Header("── Power ─────────────────────────────────────")]
        [Tooltip("Potreblenie energii VO VREMYa KRAFTA (Vatty). " +
                 "V idle fabrikator ne potreblyaet dopolnitelno. " +
                 "Bazovoe potreblenie modulya beretsya iz BuildableData cherez PowerNode.")]
        [SerializeField] private float craftPowerDraw = 100f;

        [Tooltip("Prioritet otklyucheniya pri defitsite. " +
                 "0 = kriticheskiy (ne otklyuchat), 100 = roskosh (otklyuchit pervym).")]
        [Range(0, 100)]
        [SerializeField] private int powerPriority = 50;

        [Header("── Audio (optional) ──────────────────────────")]
        [SerializeField] private AudioClip   craftStartSound;
        [SerializeField] private AudioClip   craftCompleteSound;
        [SerializeField] private AudioClip   craftCancelSound;
        [SerializeField] private AudioClip   powerLostSound;

        [Header("Fabrication Feedback")]
        [Tooltip("Pre-authored GPU particle sparks emitted from the nozzle while fabrication advances.")]
        [SerializeField] private ParticleSystem fabricationSparks;
        [SerializeField, Min(0f)] private float fabricationSparksBaseRate = 18f;
        [SerializeField] private AudioSource fabricationWeldingLoopSource;
        [SerializeField] private AudioClip fabricationWeldingLoopClip;
        [SerializeField, Range(0f, 1f)] private float fabricationWeldingLoopMaxVolume = 0.36f;
        [SerializeField, Min(0.01f)] private float fabricationWeldingLoopPitchUpdateSeconds = 0.18f;
        [SerializeField, Min(0.01f)] private float fabricationWeldingLoopMinPitch = 0.86f;
        [SerializeField, Min(0.01f)] private float fabricationWeldingLoopMaxPitch = 1.22f;
        [SerializeField] private AudioClip fabricationErrorBuzzerSound;
        [SerializeField] private Renderer[] errorFeedbackRenderers;
        [SerializeField] private Color errorEmissionColor = new Color(1f, 0.04f, 0.02f, 1f);
        [SerializeField, Min(0.05f)] private float errorFlashDurationSeconds = 0.55f;
        [SerializeField] private Color sparkProxyLightColor = new Color(1f, 0.48f, 0.12f, 1f);
        [SerializeField, Min(0.01f)] private float sparkProxyLightDurationSeconds = 0.1f;
        [SerializeField, Min(0.01f)] private float sparkProxyLightRangeMeters = 2.4f;
        [SerializeField, Min(0f)] private float sparkProxyLightIntensity = 0.72f;

        [Header("Holographic Assembly")]
        [Tooltip("Preview mesh host under the fabricator. The result mesh is assigned here without material cloning.")]
        [SerializeField] private MeshFilter assemblyPreviewMeshFilter;
        [Tooltip("Preview renderer driven by Hecton_HologramAssembly and the SHINOBU_142 Vault shader buffer.")]
        [SerializeField] private MeshRenderer assemblyPreviewRenderer;
        [Tooltip("Shared holographic material using Assets/_Project/Art/Shaders/Hecton_HologramAssembly.shader.")]
        [SerializeField] private Material hologramAssemblyMaterial;
        [Tooltip("Optional authored fallback mesh for craftable items without a world prefab. If null, a tiny shared diamond mesh is generated once.")]
        [SerializeField] private Mesh assemblyFallbackMesh;
        [SerializeField, Min(0f)] private float assemblyHeightPadding = 0.02f;
        [SerializeField] private Color assemblyBaseColor = new Color(0.05f, 0.86f, 1f, 0.72f);
        [SerializeField] private Color assemblyPausedColor = new Color(1f, 0.04f, 0.02f, 0.86f);

        [Header("── Physical Output ──────────────────────────")]
        [Tooltip("Optional socket used as the fabrication output origin.")]
        [SerializeField] private Transform outputSocket;
        [Tooltip("Local output direction used when no dedicated socket forward is authored.")]
        [SerializeField] private Vector3 outputDirectionLocal = Vector3.forward;
        [Tooltip("Meters pushed forward from the output origin before the stack is registered in the world.")]
        [SerializeField] private float outputForwardOffset = 0.45f;
        [Tooltip("Meters lifted above the output origin before the crafted stack is released.")]
        [SerializeField] private float outputLiftOffset = 0.12f;
        [Tooltip("Initial synthesized velocity change along the output direction.")]
        [SerializeField] private float outputVelocityChange = 1.75f;
        [Tooltip("Extra upward velocity change so the crafted stack clears the hatch before falling.")]
        [SerializeField] private float outputUpwardVelocityChange = 0.55f;

        [Header("── Deconstruction Output ────────────────────────")]
        [Tooltip("Optional catch-bin socket used when salvage components are ground back out of the fabricator.")]
        [SerializeField] private Transform deconstructOutputSocket;
        [Tooltip("Local ejection direction for reclaimed salvage when no dedicated catch-bin socket is authored.")]
        [SerializeField] private Vector3 deconstructOutputDirectionLocal = Vector3.forward;
        [Tooltip("Meters pushed forward from the deconstruction socket before reclaimed components register in the world.")]
        [SerializeField] private float deconstructOutputForwardOffset = 0.28f;
        [Tooltip("Meters lifted above the deconstruction socket before reclaimed components are released.")]
        [SerializeField] private float deconstructOutputLiftOffset = 0.08f;
        [Tooltip("Initial velocity change used to pop reclaimed salvage into the catch-bin.")]
        [SerializeField] private float deconstructOutputVelocityChange = 1.1f;
        [Tooltip("Extra upward velocity change used to keep reclaimed salvage from colliding with the grinder lip.")]
        [SerializeField] private float deconstructOutputUpwardVelocityChange = 0.25f;

        [Header("Crafting Thermodynamics")]
        [Tooltip("Base temperature delta injected into the hosting base module when a craft completes.")]
        [SerializeField, Min(0f)] private float craftTemperatureDeltaCelsius = 0.35f;

        [Tooltip("Optional host module receiving the craft heat pulse. If unset, the fabricator resolves the nearest parent module once.")]
        [SerializeField] private BaseModule thermalHostModule;

        // ══════════════════════════════════════════════════════════
        //  CACHED STATE
        // ══════════════════════════════════════════════════════════

        // COLD ALLOC: char[96] - cached IInteractable prompt staging buffer - owner: Fabricator
        private readonly char[] _interactTextBuffer = new char[InteractTextBufferCapacity];
        private int _interactTextLength;

        /// <summary>Ssylka na inventar tekuschego igroka.</summary>
        private PlayerInventory _playerInventory;

        /// <summary>Transform igroka dlya proverki distantsii.</summary>
        private Transform _playerTransform;
        private HectonPlayerMovement _playerMovement;
        private bool _playerMovementLookupAttempted;
        private AbsoluteUniversePosition _fabricatorAup;
        private bool _fabricatorAupCached;
        private BaseModule _thermalHostAupSource;
        private AbsoluteUniversePosition _thermalHostAup;
        private bool _thermalHostAupCached;

        /// <summary>
        /// Keshirovannyy PowerNode na etom zhe GameObject.
        /// Ispolzuetsya dlya mgnovennogo uvedomleniya PowerGrid
        /// pri izmenenii sostoyaniya krafta (PowerRating menyaetsya).
        /// Null-safe: esli PowerNode otsutstvuet — uvedomlenie ne otpravlyaetsya.
        /// </summary>
        private PowerNode _powerNode;
        private IScanLogService _scanLogSystem;
        private ResourceScarcityDirector _resourceScarcityDirector;
        private IPowerGridService _powerGridService;
        private PersistentWorldRegistry _persistentWorldRegistry;
        private IAudioService _audioService;
        private ILocalizationTextReadModel _localizationManager;
        private uint _observedScanLogRevision;
        private readonly List<RecipeData> _visibleRecipes = new List<RecipeData>(16);
        private bool _recipeCacheDirty = true;
        private bool _tickRegistered;
        private bool _lateFrameRegistered;
        private bool _hotSwapListenerRegistered;
        private int _lockedRecipeCount;
        private float _activeCraftPowerMultiplier = 1f;
        private int _activeCraftMultiplier = 1;
        private MaterialPropertyBlock _errorFeedbackBlock;
        private float _errorFlashRemainingSeconds;
        private bool _fabricationSparksPlaying;
        private bool _errorFeedbackApplied;
        private int _sparkProxyLightKey;
        private float _sparkProxyLightRemainingSeconds;
        private bool _sparkProxyLightRegistered;
        private bool _sparkLightTickRegistered;
        private bool _sparkLightTickSleeping;
        private float _weldingLoopNextPitchUpdateTime;
        private float _weldingLoopPitch = 1f;
        private uint _weldingLoopPitchSeed = 0x8F31C2A7u;
        private Material _assemblyActualMaterial;
        private float _assemblyBaseY;
        private float _assemblyTopY = 1f;
        private float _assemblyCurrentHeightY;
        private float _assemblyProgress01;
        private float _assemblyQuality;
        private uint _assemblyTargetHash;
        private int _fabricationJobSlot = -1;
        private bool _assemblyPreviewActive;
        private bool _assemblyMaterialSwapped;
        private bool _assemblyOriginShiftListenerRegistered;
        private RecipeData _pendingAssemblyBeginRecipe;
        private byte _pendingAssemblyVisualCommand;
        private bool _pendingFabricationSparksDirty;
        private bool _pendingFabricationSparksActive;
        private bool _pendingErrorFeedbackDirty;
        private float _pendingErrorFeedbackIntensity;
        private AudioClip _pendingAudioClip;
        private Vector3 _pendingAudioPosition;
        private bool _pendingAudioDirty;

        // ── Craft State ──
        private bool       _isCrafting;
        private bool       _runningExothermicHeatInjected;
        private RecipeData _activeRecipe;
        private float      _craftProgressSecondsMirror;
        private float      _lastPublishedProgress;

        // ── Power State ──
        private bool _hasPower = true;
        private bool _emergencyPowerLockActive;

        internal struct CraftingTask
        {
            public int ResultHashId;
            public int ResultQuantity;
            public float Progress;
            public float DurationSeconds;
            public float PowerMultiplier;
            public int Multiplier;
        }

        private const int MaxLocalCraftReservations = 64;
        private const int MaxNetworkCraftCosts = 32;
        private const int MaxUnlockedRecipeWords = 8;
        private const int RecipeUnlockWordShift = 6;
        private const int RecipeUnlockBitMask = 63;
        private const float SlowTickDeltaSeconds = 0.5f;
        private const float ThermalThrottleTemperatureCelsius = 50f;
        private const float ThermalThrottleProgressMultiplier = 0.5f;
        private const byte FabricatorHapticMotorMask = 0b0001;
        private const byte FabricatorHapticPriority = 2;
        private const byte FabricatorFinalHapticPriority = 3;
        private const byte ToolAcousticStateWelding = 3;
        private const byte PowerDrainReasonFabrication = 1;
        private const byte PowerDrainFlagPaused = 1 << 0;
        private const byte ItemAcquiredSourceFabricator = 4;
        private const uint FabricatorToolHash = 0x46414254u; // FABT
        private const uint FabricatorWeldingFallbackHash = 0x46415744u; // FAWD
        private const uint FabricatorTelemetryHash = 0x46414252u; // FABR
        private const uint FabricatorActiveCountHash = 0x46414354u; // FACT
        private readonly PlayerInventory.CraftReservation[] _localCraftReservations = new PlayerInventory.CraftReservation[MaxLocalCraftReservations];
        private readonly int[] _networkCostItemHashes = new int[MaxNetworkCraftCosts];
        private readonly int[] _networkCostAmounts = new int[MaxNetworkCraftCosts];
        private int _localCraftReservationCount;
        private int _networkCostCount;
        private NativeParallelHashMap<int, int> _craftInventoryCounts;
        private NativeArray<int2> _craftRecipeCosts;
        private NativeArray<byte> _craftRecipeEvaluationResult;
        private NativeArray<int2> _deconstructionRecipeOutputs;
        private NativeArray<int> _deconstructionOutputCount;
        private CraftingTask _activeCraftingTask;
        private bool _hasActiveCraftingTask;
        private NativeArray<int2> _complexRecipeGraphNodes;
        private NativeArray<int2> _complexRecipeGraphEdges;
        private NativeArray<int> _complexRecipeGraphInDegrees;
        private NativeArray<int> _complexRecipeGraphQueue;
        private NativeArray<int2> _complexRecipeRawCosts;
        private NativeArray<int> _complexRecipeRawCostCount;
        private NativeArray<byte> _complexRecipeGraphStatus;
        private NativeArray<ulong> _unlockedRecipes;
        private bool _unlockMaskDirty = true;

        private BaseLogisticsNetwork.LogisticsReservation _networkReservation;

        /// <summary>Porog publikatsii progressa.</summary>
        private const float ProgressPublishThreshold = 0.01f;
        private const string NativeMemoryOwner = nameof(Fabricator);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;
        private const Allocator DataVaultExemptSceneScratchAllocator = Allocator.Persistent;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — QUERIES
        // ══════════════════════════════════════════════════════════

        /// <summary>Idet li seychas protsess krafta.</summary>
        public bool IsCrafting => _isCrafting;

        public bool IsContinuous
        {
            get => isContinuous;
            set => isContinuous = value;
        }

        /// <summary>Normalizovannyy progress (0..1).</summary>
        public float CraftProgress => ResolveCraftProgress01();

        public float CraftingProgress01 => CraftProgress;

        private float ResolveCraftProgress01()
        {
            if (!_isCrafting || _activeRecipe == null)
                return 0f;

            if (_fabricationJobSlot >= 0 &&
                FabricationAssemblerRuntime.TryReadSnapshot(_fabricationJobSlot, out FabricationRuntimeSnapshot snapshot))
            {
                return Mathf.Clamp01(snapshot.Progress01);
            }

            return _assemblyPreviewActive ? Mathf.Clamp01(_assemblyProgress01) : 0f;
        }

        /// <summary>Aktivnyy retsept (null esli ne kraftim).</summary>
        public RecipeData ActiveRecipe => _activeRecipe;

        /// <summary>Spisok dostupnyh retseptov. Read-only dlya UI.</summary>
        public IReadOnlyList<RecipeData> AvailableRecipes
        {
            get
            {
                EnsureRecipeCache();
                return _visibleRecipes;
            }
        }

        public int TotalRecipeCount
        {
            get
            {
                EnsureRecipeCache();
                return _visibleRecipes.Count + _lockedRecipeCount;
            }
        }
        public int LockedRecipeCount
        {
            get
            {
                EnsureRecipeCache();
                return _lockedRecipeCount;
            }
        }

        /// <summary>Kraft na pauze iz-za otsutstviya pitaniya.</summary>
        public bool IsPausedNoPower => _isCrafting && !HasOperationalPower;

        internal PowerGrid CurrentPowerGrid => _powerNode != null ? _powerNode.Grid : null;

        // ══════════════════════════════════════════════════════════
        //  IPowerComponent — ENERGOSISTEMA
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Potreblenie energii fabrikatorom.
        ///
        /// Idle (ne kraftit): 0 Vt.
        ///   Bazovoe potreblenie modulya obespechivaetsya PowerNode
        ///   cherez BuildableData.powerRating.
        ///
        /// Crafting: -craftPowerDraw Vt.
        ///   Dopolnitelnoe potreblenie na rabotu stanka.
        ///
        /// Itogo pri krafte: BuildableData.powerRating + (-craftPowerDraw).
        ///   Primer: -20 (bazovyy) + (-100) (kraft) = -120 Vt.
        /// </summary>
        public float PowerRating => _isCrafting && !_emergencyPowerLockActive ? -craftPowerDraw * _activeCraftPowerMultiplier : 0f;

        /// <summary>Prioritet otklyucheniya.</summary>
        public int PowerPriority => powerPriority;

        /// <summary>Tekuschee sostoyanie pitaniya.</summary>
        public bool HasPower => _hasPower;

        /// <summary>True while the submarine OS has suspended this fabricator from non-essential load service.</summary>
        public bool IsEmergencyPowerLocked => _emergencyPowerLockActive;

        /// <summary>
        /// Uvedomlenie ot PowerGrid ob izmenenii pitaniya.
        ///
        /// Pri potere pitaniya:
        ///   • Kraft ZAMORAZhIVAETSYa (Vault Progress01 ne prodvigaetsya).
        ///   • Kraft NE otmenyaetsya — resursy uzhe spisany.
        ///   • Pri vosstanovlenii — kraft prodolzhitsya.
        ///
        /// Pri vosstanovlenii:
        ///   • Kraft prodolzhaetsya s togo zhe mesta.
        /// </summary>
        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = hasPower;

            if (!hasPower && _isCrafting)
            {
                // Kraft zamorozhen
                ApplyAssemblyVisualProgress(CraftingProgress01, true);
                PlaySound(powerLostSound);
            }
        }

        /// <summary>
        /// Applies or clears the submarine-wide non-essential power lock across all live fabricators.
        /// Active crafts pause without losing inputs and resume automatically once the lock clears.
        /// </summary>
        public static void SetEmergencyPowerLockAll(bool active)
        {
            if (s_emergencyPowerLockActive == active)
                return;

            s_emergencyPowerLockActive = active;
            for (int i = 0; i < _activeFabricators.Count; i++)
            {
                Fabricator fabricator = _activeFabricators[i];
                if (fabricator == null)
                    continue;

                fabricator.ApplyEmergencyPowerLock(active);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            RebuildInteractText();

            // Keshiruem PowerNode dlya mgnovennogo uvedomleniya seti.
            // PowerNode dolzhen byt na tom zhe GameObject, chto i Fabricator.
            TryGetComponent(out _powerNode);
            CacheRegistryServicesCold();
            MarkRecipeCacheDirty();
            _activeCraftPowerMultiplier = 1f;
            _sparkProxyLightKey = unchecked((int)EntityId.ToULong(GetEntityId()) ^ 0x4641424C);
            _errorFeedbackBlock = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - fabricator error emission property staging - owner: Fabricator
            if (assemblyFallbackMesh == null)
                EnsureSharedAssemblyFallbackMesh();
            FlushEndAssemblyVisual();
            ToolHapticsRuntime.EnsureRuntimeInstance();
            EnsureCraftingScratch();
            CacheFabricatorAup();
        }

        private void Start()
        {
            CacheThermalHostModule();
        }

        private void OnEnable()
        {
            RegisterActiveFabricator(this);
            PublishFabricatorActiveCountBlackBox();
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            InteractableRegistry.RegisterTree(this);
            BaseLogisticsNetwork.RegisterFabricator(this, _powerNode);
            LocalizationEvents.RegisterLanguageListener(this);
            ModRegistryEvents.Register(this);
            RebuildInteractText();
            TryRegister();
            MarkRecipeCacheDirty();
            ApplyEmergencyPowerLock(s_emergencyPowerLockActive);
            CacheFabricatorAup();
            TryRegisterAssemblyOriginShiftListener();
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            UnregisterActiveFabricator(this);
            BaseLogisticsNetwork.UnregisterFabricator(this);
            LocalizationEvents.UnregisterLanguageListener(this);
            ModRegistryEvents.Unregister(this);
            TryUnregisterAssemblyOriginShiftListener();
            TryUnregisterHotSwapListener();

            if (_isCrafting)
                CancelCraft();

            FlushSetFabricationSparksActive(false);
            FlushEndAssemblyVisual();
            UnregisterSparkProxyLight();
            TryUnregisterSparkLightTick();
            TryUnregisterLateFrame();
            TryUnregister();
            PublishFabricatorActiveCountBlackBox();
        }

        private void OnDestroy()
        {
            InteractableRegistry.InvalidateTree(this);
            UnregisterActiveFabricator(this);
            BaseLogisticsNetwork.UnregisterFabricator(this);
            TryUnregister();
            TryUnregisterHotSwapListener();
            TryUnregisterAssemblyOriginShiftListener();
            FlushSetFabricationSparksActive(false);
            FlushEndAssemblyVisual();
            UnregisterSparkProxyLight();
            TryUnregisterSparkLightTick();
            TryUnregisterLateFrame();
            DisposeCraftingScratch();
            PublishFabricatorActiveCountBlackBox();
        }

        // ══════════════════════════════════════════════════════════
        //  IInteractable
        // ══════════════════════════════════════════════════════════

        public void OnHoverStart() { }

        public void OnHoverEnd() { }

        public void Interact(Transform interactor)
        {
            _playerTransform = interactor;
            _playerMovement = null;
            _playerMovementLookupAttempted = false;

            if (_playerInventory == null && interactor != null)
                interactor.TryGetComponent(out _playerInventory);
            TryCachePlayerMovement(interactor);

            CraftingEvents.TryRaiseFabricatorOpened(this);
            InteractionEvents.TryRaiseInteractionStarted(this, interactor);
        }

        public string GetInteractText()
        {
            return LegacyInteractText;
        }

        public bool TryCopyInteractText(Span<char> destination, out int length)
        {
            length = _interactTextLength;
            if (length <= 0 || destination.Length < length)
            {
                length = 0;
                return false;
            }

            _interactTextBuffer.AsSpan(0, length).CopyTo(destination);
            return true;
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            _fabricatorAupCached = false;
            CacheFabricatorAup();
            if (_assemblyPreviewActive && !_assemblyMaterialSwapped)
            {
                ApplyAssemblyVisualProgress(_assemblyProgress01, IsPausedNoPower);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — CRAFTING
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Proveryaet, mozhno li skraftit dannyy retsept.
        /// Dobavlena proverka pitaniya: bez pitaniya kraft ne nachinaetsya.
        /// </summary>
        public bool CanCraft(RecipeData recipe)
        {
            return CanCraft(recipe, 1);
        }

        public bool CanCraft(RecipeData recipe, int multiplier)
        {
            if (recipe == null) return false;
            if (_isCrafting) return false;
            if (!HasOperationalPower) return false;
            if (_playerInventory == null || _playerInventory.Grid == null) return false;
            if (recipe.ingredients == null || recipe.ingredients.Count == 0) return false;
            if (recipe.resultItem == null || recipe.resultQuantity <= 0) return false;
            if (!IsRecipeUnlocked(recipe)) return false;
            if (!PassesBiomeLock(recipe)) return false;

            int safeMultiplier = Mathf.Max(1, multiplier);
            if (!HasIngredientsFastFailOrLegacy(recipe, safeMultiplier))
                return false;

            if (IsOutputStorageCapacityExceededFastOrExact(recipe, safeMultiplier))
                return false;

            return true;
        }

        private bool IsStorageCapacityExceededForRecipe(RecipeData recipe, int multiplier)
        {
            if (recipe == null) return false;
            if (_isCrafting) return false;
            if (!HasOperationalPower) return false;
            if (_playerInventory == null || _playerInventory.Grid == null) return false;
            if (recipe.ingredients == null || recipe.ingredients.Count == 0) return false;
            if (recipe.resultItem == null || recipe.resultQuantity <= 0) return false;
            if (!IsRecipeUnlocked(recipe)) return false;
            if (!PassesBiomeLock(recipe)) return false;

            int safeMultiplier = Mathf.Max(1, multiplier);
            return HasIngredientsFastFailOrLegacy(recipe, safeMultiplier) &&
                   IsOutputStorageCapacityExceededFastOrExact(recipe, safeMultiplier);
        }

        private bool IsOutputStorageCapacityExceeded(RecipeData recipe, int multiplier)
        {
            if (recipe == null || recipe.resultItem == null || _playerInventory == null || _playerInventory.Grid == null)
                return false;

            InventoryGrid grid = _playerInventory.Grid;
            int safeMultiplier = Mathf.Max(1, multiplier);
            long neededCells =
                (long)Mathf.Max(1, recipe.resultItem.CellArea) *
                Mathf.Max(1, recipe.resultQuantity) *
                safeMultiplier;
            long ingredientCells = CountReclaimableIngredientCells(recipe, safeMultiplier);
            long availableAfter = (long)grid.FreeCells + ingredientCells;
            return neededCells > availableAfter;
        }

        /// <summary>
        /// Counts ingredient units available to this fabricator across the player inventory and its linked logistics grid.
        /// </summary>
        public int CountAccessibleItem(ItemData item, PlayerInventory inventoryOverride = null)
        {
            if (item == null)
                return 0;

            PlayerInventory inventory = inventoryOverride != null ? inventoryOverride : _playerInventory;
            int count = CountAvailableItemInInventory(inventory, item);
            PowerGrid grid = _powerNode != null ? _powerNode.Grid : null;
            if (grid != null)
                count += BaseLogisticsNetwork.CountAccessibleItem(grid, ComputeItemHash(item));

            return count;
        }

        internal int GetAdjustedIngredientAmount(InventoryCost cost)
        {
            if (cost == null || cost.item == null || cost.amount <= 0)
                return 0;

            int itemHashId = ComputeItemHash(cost.item);
            ResourceScarcityDirector scarcityDirector = _resourceScarcityDirector;
            CacheFabricatorAup();
            return scarcityDirector != null
                ? scarcityDirector.ResolveInflatedIngredientAmount(itemHashId, cost.amount, in _fabricatorAup, CountAccessibleItem(cost.item))
                : cost.amount;
        }

        internal float GetRecipeInflationMultiplier(RecipeData recipe)
        {
            if (recipe == null || recipe.ingredients == null || recipe.ingredients.Count <= 0)
                return 1f;

            float maxMultiplier = 1f;
            for (int i = 0; i < recipe.ingredients.Count; i++)
            {
                InventoryCost cost = recipe.ingredients[i];
                if (cost == null || cost.item == null || cost.amount <= 0)
                    continue;

                int adjustedAmount = GetAdjustedIngredientAmount(cost);
                if (adjustedAmount <= cost.amount)
                    continue;

                float multiplier = (float)adjustedAmount / cost.amount;
                if (multiplier > maxMultiplier)
                    maxMultiplier = multiplier;
            }

            return maxMultiplier;
        }

        /// <summary>
        /// Zapuskaet protsess krafta.
        /// Posle smeny _isCrafting → PowerRating menyaetsya s 0 na -craftPowerDraw.
        /// NotifyGridBalanceChanged() zastavlyaet set mgnovenno pereschitat balans.
        /// </summary>
        public bool StartCraft(RecipeData recipe)
        {
            return StartCraft(recipe, 1);
        }

        public bool StartCraft(RecipeData recipe, int multiplier)
        {
            int safeMultiplier = Mathf.Max(1, multiplier);
            if (!CanCraft(recipe, safeMultiplier))
            {
                if (IsStorageCapacityExceededForRecipe(recipe, safeMultiplier))
                    RaiseStorageCapacityExceededBark();

                TriggerCraftFailureFeedback();
                return false;
            }

            _activeRecipe = recipe;
            _activeCraftMultiplier = safeMultiplier;
            if (!ConsumeIngredients(recipe, safeMultiplier))
            {
                RefundIngredients();
                _activeRecipe = null;
                _activeCraftMultiplier = 1;
                TriggerCraftFailureFeedback();
                return false;
            }

            _activeCraftPowerMultiplier = ResolveCraftPowerMultiplier(this, recipe);
            _craftProgressSecondsMirror = 0f;
            _isCrafting   = true;
            _runningExothermicHeatInjected = false;
            _lastPublishedProgress = -1f;
            FabricationAssemblerRuntime.EnsureRuntime();
            CreateCraftingTaskSlot(recipe, _activeCraftPowerMultiplier, safeMultiplier);
            BeginAssemblyVisual(recipe);
            SetFabricationSparksActive(true);

            // ── Uvedomlyaem energoset: PowerRating izmenilsya (0 → -craftPowerDraw) ──
            NotifyGridBalanceChanged();
            PublishFabricatorActiveCountBlackBox();

            CraftingEvents.TryRaiseCraftStarted(recipe);
            PublishCraftingStartedSignal(recipe, safeMultiplier);
            CraftingEvents.TryRaiseCraftProgressUpdated(0f);
            PlaySound(craftStartSound);

            return true;
        }

        void IFabricator.StartCraft(RecipeData recipe)
        {
            StartCraft(recipe);
        }

        void IFabricator.StartCraft(RecipeData recipe, int multiplier)
        {
            StartCraft(recipe, multiplier);
        }

        /// <summary>
        /// Otmenyaet tekuschiy kraft. Vozvraschaet ingredienty.
        /// Posle smeny _isCrafting → PowerRating menyaetsya s -craftPowerDraw na 0.
        /// NotifyGridBalanceChanged() zastavlyaet set mgnovenno pereschitat balans.
        /// </summary>
        public void CancelCraft()
        {
            if (!_isCrafting) return;

            RefundIngredients();

            _isCrafting   = false;
            _activeRecipe = null;
            _craftProgressSecondsMirror = 0f;
            _activeCraftPowerMultiplier = 1f;
            _activeCraftMultiplier = 1;
            ClearCraftingTaskSlot();
            SetFabricationSparksActive(false);
            EndAssemblyVisual();

            // ── Uvedomlyaem energoset: PowerRating izmenilsya (-craftPowerDraw → 0) ──
            NotifyGridBalanceChanged();
            PublishFabricatorActiveCountBlackBox();

            CraftingEvents.TryRaiseCraftCancelled();
            CraftingEvents.TryRaiseCraftProgressUpdated(0f);

            PlaySound(craftCancelSound);
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable — TAYMER KRAFTA
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Vyzyvaetsya GameTickManager kazhdyy kadr.
        ///
        /// ENERGOPAUZA: esli _hasPower == false i idet kraft:
        ///   • Taymer NE prodvigaetsya.
        ///   • Progress NE publikuetsya (UI pokazyvaet pauzu).
        ///   • Kraft NE otmenyaetsya.
        ///   • Proverka distantsii prodolzhaetsya (igrok mozhet otoyti).
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_sparkLightTickSleeping)
                return;

            if (!(_sparkProxyLightRemainingSeconds > 0f))
            {
                UnregisterSparkProxyLight();
                _sparkLightTickSleeping = true;
                return;
            }

            _sparkProxyLightRemainingSeconds = Mathf.Max(0f, _sparkProxyLightRemainingSeconds - Mathf.Max(0f, deltaTime));
            if (_sparkProxyLightRemainingSeconds > 0f)
            {
                UpdateSparkProxyLightRegistration();
                return;
            }

            UnregisterSparkProxyLight();
            _sparkLightTickSleeping = true;
        }

        public void LateFrameTick()
        {
            byte command = _pendingAssemblyVisualCommand;
            RecipeData beginRecipe = _pendingAssemblyBeginRecipe;
            _pendingAssemblyVisualCommand = 0;
            _pendingAssemblyBeginRecipe = null;

            if (command == 1)
                FlushBeginAssemblyVisual(beginRecipe);
            else if (command == 2)
                FlushCompleteAssemblyVisual();
            else if (command == 3)
                FlushEndAssemblyVisual();

            if (_pendingFabricationSparksDirty)
            {
                bool active = _pendingFabricationSparksActive;
                _pendingFabricationSparksDirty = false;
                FlushSetFabricationSparksActive(active);
            }

            if (_pendingErrorFeedbackDirty)
            {
                float intensity = _pendingErrorFeedbackIntensity;
                _pendingErrorFeedbackDirty = false;
                FlushApplyErrorFeedback(intensity);
            }

            FlushPendingAudio();
        }

        public void SlowTick()
        {
            UpdateErrorFeedback(SlowTickDeltaSeconds);

            if (!_isCrafting)
            {
                SetFabricationSparksActive(false);
                ApplyAssemblyVisualProgress(0f, false);
                return;
            }

            if (_activeRecipe == null)
            {
                CancelCraft();
                return;
            }

            // ── Proverka distantsii (vsegda, dazhe bez pitaniya) ──
            if (!IsPlayerInRange())
            {
                CancelCraft();
                return;
            }

            // ═══════════════════════════════════════════════════
            //  POWER PAUSE: net pitaniya -> Vault Progress01 zamorozhen
            // ═══════════════════════════════════════════════════
            if (!_hasActiveCraftingTask)
            {
                CancelCraft();
                return;
            }

            CraftingTask task = _activeCraftingTask;
            _hasActiveCraftingTask = false;
            if (task.ResultHashId == 0 || task.ResultQuantity <= 0)
            {
                CancelCraft();
                return;
            }

            float previousProgress = task.Progress;
            bool pausedByPower = !HasFabricationProgressPower();
            UpdateFabricationVaultSlot(pausedByPower);

            if (!TryReadFabricationProgress(ref task, out float durationSeconds, out float progress, out bool craftCompleted))
            {
                StoreActiveCraftingTask(in task);
                SetFabricationSparksActive(false);
                ApplyAssemblyVisualProgress(task.Progress, true);
                return;
            }

            if (pausedByPower)
            {
                _craftProgressSecondsMirror = progress * durationSeconds;
                StoreActiveCraftingTask(in task);
                SetFabricationSparksActive(false);
                ApplyAssemblyVisualProgress(progress, true);
                return;
            }

            _activeCraftPowerMultiplier = Mathf.Max(1f, task.PowerMultiplier);
            SetFabricationSparksActive(true);
            ApplyRunningExothermicHeatIfNeeded();
            _craftProgressSecondsMirror = task.Progress * durationSeconds;
            ApplyAssemblyVisualProgress(progress, false);
            PublishPowerDrainSignal((progress - previousProgress) / SlowTickDeltaSeconds, progress, false);
            if (progress < 1f)
                PublishWeldingToolAcoustic(progress);
            if (progress > previousProgress)
            {
                RaiseFabricatorProgressAudioPing();
                RaiseFabricatorProgressHaptics(progress);
                TriggerSparkProxyLight();
            }

            if (progress - _lastPublishedProgress > ProgressPublishThreshold
                || progress >= 1f)
            {
                _lastPublishedProgress = progress;
                CraftingEvents.TryRaiseCraftProgressUpdated(progress);
            }

            if (craftCompleted)
            {
                CompleteCraft();
                return;
            }

            StoreActiveCraftingTask(in task);
        }

        private bool TryReadFabricationProgress(
            ref CraftingTask task,
            out float durationSeconds,
            out float progress,
            out bool craftCompleted)
        {
            durationSeconds = Mathf.Max(0.001f, task.DurationSeconds);
            progress = Mathf.Clamp01(task.Progress);
            craftCompleted = false;

            if (_fabricationJobSlot < 0 && _assemblyPreviewActive && _activeRecipe != null)
                BeginFabricationVaultJob(_activeRecipe);

            if (_fabricationJobSlot < 0)
                return false;

            if (!FabricationAssemblerRuntime.TryReadSnapshot(_fabricationJobSlot, out FabricationRuntimeSnapshot snapshot))
                return false;

            durationSeconds = Mathf.Max(0.001f, snapshot.DurationSeconds);
            progress = Mathf.Clamp01(snapshot.Progress01);
            task.Progress = progress;
            craftCompleted = (snapshot.Flags & FabricationAssemblerFlags.Completed) != 0u || progress >= 1f;
            return true;
        }

        private void UpdateFabricationVaultSlot(bool paused)
        {
            if (_fabricationJobSlot < 0)
                return;

            FabricationAssemblerRuntime.TryUpdateSlot(
                _fabricationJobSlot,
                paused ? 0f : ResolveFabricationPowerPotential01(),
                ResolveCraftThermalThrottleMultiplier(),
                paused,
                transform.worldToLocalMatrix,
                _assemblyBaseY,
                _assemblyTopY);
        }

        private float ResolveFabricationPowerPotential01()
        {
            if (!HasOperationalPower)
                return 0f;

            IPowerGridService powerGrid = _powerGridService;
            if (powerGrid != null && powerGrid.BatterySnapshot.EmergencyReserveActive != 0)
                return 0f;

            PowerGrid grid = CurrentPowerGrid;
            if (grid != null && grid.HasPowerDeficit)
                return Mathf.Clamp01(grid.SupplyRatio);

            return 1f;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — CRAFT COMPLETION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Zavershaet kraft: vydaet rezultat v inventar.
        /// Posle smeny _isCrafting → PowerRating menyaetsya s -craftPowerDraw na 0.
        /// NotifyGridBalanceChanged() zastavlyaet set mgnovenno pereschitat balans.
        /// </summary>
        private void CreateCraftingTaskSlot(RecipeData recipe, float powerMultiplier, int multiplier)
        {
            ClearCraftingTaskSlot();
            if (recipe == null)
                return;

            int safeMultiplier = Mathf.Max(1, multiplier);
            CraftingTask task = default;
            task.ResultHashId = ComputeItemHash(recipe.resultItem);
            task.ResultQuantity = ResolveCraftOutputQuantity(recipe, safeMultiplier);
            task.Progress = 0f;
            task.DurationSeconds = Mathf.Max(0.001f, recipe.craftTime * safeMultiplier);
            task.PowerMultiplier = Mathf.Max(1f, powerMultiplier);
            task.Multiplier = safeMultiplier;
            StoreActiveCraftingTask(in task);
        }

        private void ClearCraftingTaskSlot()
        {
            _activeCraftingTask = default;
            _hasActiveCraftingTask = false;
        }

        private void StoreActiveCraftingTask(in CraftingTask task)
        {
            _activeCraftingTask = task;
            _hasActiveCraftingTask = true;
        }

        private static int ResolveCraftOutputQuantity(RecipeData recipe, int multiplier)
        {
            if (recipe == null)
                return 0;

            long quantity = (long)math.max(1, recipe.resultQuantity) * math.max(1, multiplier);
            return quantity > int.MaxValue ? int.MaxValue : (int)quantity;
        }

        private bool HasFabricationProgressPower()
        {
            if (!HasOperationalPower)
                return false;

            IPowerGridService powerGrid = _powerGridService;
            if (powerGrid != null && powerGrid.BatterySnapshot.EmergencyReserveActive != 0)
                return false;

            PowerGrid grid = CurrentPowerGrid;
            return grid == null || !grid.HasPowerDeficit || grid.SupplyRatio > 0f;
        }

        private void CompleteCraft()
        {
            RecipeData recipe = _activeRecipe;
            int craftMultiplier = Mathf.Max(1, _activeCraftMultiplier);
            if (recipe == null)
            {
                if (_networkReservation != null)
                {
                    BaseLogisticsNetwork.RollbackReserved(_networkReservation);
                    _networkReservation = null;
                }

                _isCrafting = false;
                _runningExothermicHeatInjected = false;
                _craftProgressSecondsMirror = 0f;
                _lastPublishedProgress = 0f;
                _activeCraftPowerMultiplier = 1f;
                _activeCraftMultiplier = 1;
                ClearCraftingTaskSlot();
                SetFabricationSparksActive(false);
                EndAssemblyVisual();
                NotifyGridBalanceChanged();
                PublishFabricatorActiveCountBlackBox();
                return;
            }

            ItemData   result = recipe.resultItem;
            int outputQuantity = ResolveCraftOutputQuantity(recipe, craftMultiplier);
            float powerCost = ResolveCraftPowerCost(recipe) * craftMultiplier;
            float craftTemperatureDelta = ResolveCraftTemperatureDeltaCelsius() * craftMultiplier;

            _isCrafting   = false;
            _runningExothermicHeatInjected = false;
            _activeRecipe = null;
            _craftProgressSecondsMirror = 0f;
            _activeCraftPowerMultiplier = 1f;
            _activeCraftMultiplier = 1;
            ClearCraftingTaskSlot();
            SetFabricationSparksActive(false);

            if (_playerInventory != null && !_playerInventory.CommitCraftReservations(_localCraftReservations, _localCraftReservationCount))
            {
                _localCraftReservationCount = 0;
                if (_networkReservation != null)
                {
                    BaseLogisticsNetwork.RollbackReserved(_networkReservation);
                    _networkReservation = null;
                }

                NotifyGridBalanceChanged();
                PublishFabricatorActiveCountBlackBox();
                EndAssemblyVisual();
                TriggerCraftFailureFeedback();
                return;
            }

            _localCraftReservationCount = 0;

            if (_networkReservation != null)
            {
                BaseLogisticsNetwork.CommitReserved(_networkReservation);
                _networkReservation = null;
            }

            // ── Uvedomlyaem energoset: PowerRating izmenilsya (-craftPowerDraw → 0) ──
            NotifyGridBalanceChanged();

            // ── Potreblyaem energiyu iz seti pri zavershenii krafta ──
            if (powerCost > 0f && _powerNode != null && _powerNode.Grid != null)
            {
                _powerNode.Grid.ConsumePower(powerCost);
            }

            ApplyCraftingThermodynamics(craftTemperatureDelta);
            CompleteAssemblyVisual();

            int deliveredQuantity = 0;
            if (result != null && outputQuantity > 0)
            {
                if (TrySynthesizeCraftOutput(recipe, result, outputQuantity))
                {
                    deliveredQuantity = outputQuantity;
                }
                else if (_playerInventory != null)
                {
                    int resultHashId = ComputeItemHash(result);
                    int addedQuantity = 0;
                    if (resultHashId != 0)
                    {
                        PlayerInventory.ScavengeAttemptResult addResult = _playerInventory.ScavengeAttempt(resultHashId, outputQuantity, null);
                        addedQuantity = addResult.AddedQuantity;
                        deliveredQuantity += addedQuantity;
                    }

                    if (addedQuantity < outputQuantity)
                    {
                        int remainingQuantity = outputQuantity - addedQuantity;
                        if (TryEmitCraftOverflowStack(result, remainingQuantity))
                            deliveredQuantity += remainingQuantity;

                        RaiseStorageCapacityExceededBark();
                        TriggerCraftFailureFeedback();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.LogWarning("[Fabricator] Craft output overflow; routed to diegetic bark/drop fallback.");
#endif
                    }
                }
            }

            if (result != null && deliveredQuantity > 0)
                PublishCraftItemAcquiredSignal(result, deliveredQuantity);

            CraftingEvents.TryRaiseCraftProgressUpdated(1f);

            if (result != null)
            {
                PublishCraftingCompletedSignal(recipe, result, deliveredQuantity);
                CraftingEvents.TryRaiseCraftCompleted(result);
            }

            PublishFabricatorActiveCountBlackBox();
            PlaySound(craftCompleteSound);
            TryRestartContinuousCraft(recipe, craftMultiplier);
        }

        private void TryRestartContinuousCraft(RecipeData recipe, int multiplier)
        {
            if (!isContinuous || recipe == null || _isCrafting)
                return;

            StartCraft(recipe, multiplier);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — POWER GRID NOTIFICATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Uvedomlyaet PowerGrid o neobhodimosti perescheta balansa.
        ///
        /// Vyzyvaetsya pri kazhdom izmenenii PowerRating:
        ///   • StartCraft:    0 → -craftPowerDraw (nachalo potrebleniya)
        ///   • CompleteCraft: -craftPowerDraw → 0 (konets potrebleniya)
        ///   • CancelCraft:   -craftPowerDraw → 0 (otmena potrebleniya)
        ///
        /// Bez etogo vyzova PowerGrid uznal by ob izmenenii tolko
        /// pri sleduyuschem SlowTick (~0.5-1s zaderzhka). S vyzovom —
        /// balans pereschityvaetsya mgnovenno.
        ///
        /// Null-safe: esli PowerNode ili Grid otsutstvuyut — no-op.
        /// </summary>
        private void NotifyGridBalanceChanged()
        {
            if (_powerNode != null && _powerNode.Grid != null)
                _powerNode.Grid.MarkDirty();
        }

        private bool HasOperationalPower => _hasPower && !_emergencyPowerLockActive;

        private void ApplyEmergencyPowerLock(bool active)
        {
            if (_emergencyPowerLockActive == active)
                return;

            _emergencyPowerLockActive = active;
            if (_isCrafting)
            {
                ApplyAssemblyVisualProgress(CraftingProgress01, active);
                NotifyGridBalanceChanged();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — INGREDIENT MANAGEMENT
        // ══════════════════════════════════════════════════════════

        private void CacheThermalHostModule()
        {
            if (thermalHostModule != null)
                return;

            thermalHostModule = GetComponentInParent<BaseModule>();
            _thermalHostAupCached = false;
            _thermalHostAupSource = null;
        }

        private bool PassesBiomeLock(RecipeData recipe)
        {
            if (recipe == null || !recipe.RequiresAnchoredBiomeLock)
                return true;

            if (thermalHostModule == null)
                CacheThermalHostModule();

            if (thermalHostModule == null || thermalHostModule.IsUnmoored || thermalHostModule.IsDetachedDebris)
                return false;

            Vector3 samplePosition = ResolveThermalHostRuntimePosition();
            WorldProceduralFieldSampler sampler = WorldProceduralFieldSampler.ActiveRuntimeInstance;
            if (sampler != null &&
                sampler.TrySampleBiomeInfluence(
                    samplePosition,
                    out WorldProceduralFieldSampler.BiomeInfluenceCell influence,
                    out HectonBiomeMatrixProfile primaryProfile,
                    out HectonBiomeMatrixProfile secondaryProfile))
            {
                return MatchesRecipeBiomeLock(recipe, primaryProfile, primaryProfile != null ? primaryProfile.matrixIndex : 0) ||
                       MatchesRecipeBiomeLock(recipe, secondaryProfile, secondaryProfile != null ? secondaryProfile.matrixIndex : 0);
            }

            BiomeMatrixDirector matrixDirector = BiomeMatrixDirector.ActiveRuntimeInstance;
            return matrixDirector != null && MatchesRecipeBiomeLock(
                recipe,
                matrixDirector.CurrentProfile,
                matrixDirector.CurrentProfile != null ? matrixDirector.CurrentProfile.matrixIndex : 0);
        }

        private Vector3 ResolveThermalHostRuntimePosition()
        {
            if (!_thermalHostAupCached || !ReferenceEquals(_thermalHostAupSource, thermalHostModule))
            {
                _thermalHostAupCached = TryResolveAupFromRuntimeOrigin(
                    thermalHostModule.transform.position,
                    out _thermalHostAup);
                _thermalHostAupSource = _thermalHostAupCached ? thermalHostModule : null;
            }

            if (!_thermalHostAupCached)
                return thermalHostModule.transform.position;

            float3 runtime = _thermalHostAup.ToRuntimeFloat3();
            return new Vector3(runtime.x, runtime.y, runtime.z);
        }

        private static bool MatchesRecipeBiomeLock(RecipeData recipe, HectonBiomeMatrixProfile profile, int biomeId)
        {
            if (recipe == null || profile == null)
                return false;

            if (recipe.requiredAnchoredBiomeMatrixId > 0 && biomeId == recipe.requiredAnchoredBiomeMatrixId)
                return true;

            int requiredFamilyHashId = recipe.RequiredAnchoredBiomeFamilyHashId;
            if (requiredFamilyHashId == 0)
                return false;

            if (profile.FamilyHashId == requiredFamilyHashId)
            {
                return true;
            }

            HectonBiomeFamilyProfile family = profile.familyProfile;
            return family != null && family.FamilyHashId == requiredFamilyHashId;
        }

        private float ResolveCraftTemperatureDeltaCelsius()
        {
            if (!(craftTemperatureDeltaCelsius > 0f) || !float.IsFinite(craftTemperatureDeltaCelsius))
                return 0f;

            float delta = craftTemperatureDeltaCelsius * Mathf.Max(1f, _activeCraftPowerMultiplier);
            return float.IsFinite(delta) ? delta : 0f;
        }

        private float ResolveCraftThermalThrottleMultiplier()
        {
            if (thermalHostModule == null)
                CacheThermalHostModule();

            if (thermalHostModule == null)
                return 1f;

            float hostRoomTemperatureCelsius = thermalHostModule.ResolveHostRoomTemperatureCelsius();
            return hostRoomTemperatureCelsius > ThermalThrottleTemperatureCelsius
                ? ThermalThrottleProgressMultiplier
                : 1f;
        }

        private void ApplyRunningExothermicHeatIfNeeded()
        {
            if (_runningExothermicHeatInjected)
                return;

            ApplyCraftingThermodynamics(ExothermicRunningHeatDeltaCelsius);
            _runningExothermicHeatInjected = true;
        }

        private void ApplyCraftingThermodynamics(float deltaCelsius)
        {
            if (!(deltaCelsius > 0f))
                return;

            if (thermalHostModule == null)
                CacheThermalHostModule();

            if (thermalHostModule == null)
                return;

            thermalHostModule.TryInjectHostRoomTemperatureDeltaCelsius(deltaCelsius);
        }

        private bool HasIngredients(RecipeData recipe, int multiplier = 1)
        {
            if (recipe == null || _playerInventory == null)
                return false;

            EnsureCraftingScratch();
            return CraftingSystem.CanCraft(
                recipe,
                this,
                _playerInventory,
                _craftInventoryCounts,
                _craftRecipeCosts,
                _craftRecipeEvaluationResult,
                _complexRecipeGraphNodes,
                _complexRecipeGraphEdges,
                _complexRecipeGraphInDegrees,
                _complexRecipeGraphQueue,
                _complexRecipeRawCosts,
                _complexRecipeRawCostCount,
                _complexRecipeGraphStatus,
                Mathf.Max(1, multiplier));
        }

        private void EnsureCraftingScratch()
        {
            if (!_craftInventoryCounts.IsCreated)
            {
                // COLD ALLOC: NativeParallelHashMap<Int32,Int32>[128] — temporary per-craft accessible item counts — owner: Fabricator
                _craftInventoryCounts = new NativeParallelHashMap<int, int>(128, DataVaultExemptSceneScratchAllocator);
                NativeMemorySentinel.RegisterNativeParallelHashMap(_craftInventoryCounts, NativeMemoryOwner, nameof(_craftInventoryCounts), NativeMemoryLifetime);
            }

            if (!_craftRecipeCosts.IsCreated)
            {
                // COLD ALLOC: NativeArray<int2>[32] — flattened recipe ingredient cost buffer — owner: Fabricator
                _craftRecipeCosts = new NativeArray<int2>(CraftingSystem.MaxRecipeIngredientCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_craftRecipeCosts, nameof(_craftRecipeCosts));
            }

            if (!_craftRecipeEvaluationResult.IsCreated)
            {
                // COLD ALLOC: NativeArray<byte>[1] — Burst crafting-availability result cell — owner: Fabricator
                _craftRecipeEvaluationResult = new NativeArray<byte>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_craftRecipeEvaluationResult, nameof(_craftRecipeEvaluationResult));
            }

            if (!_deconstructionRecipeOutputs.IsCreated)
            {
                // COLD ALLOC: NativeArray<int2>[32] — deconstruction output yield scratch — owner: Fabricator
                _deconstructionRecipeOutputs = new NativeArray<int2>(CraftingSystem.MaxDeconstructionOutputCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_deconstructionRecipeOutputs, nameof(_deconstructionRecipeOutputs));
            }

            if (!_deconstructionOutputCount.IsCreated)
            {
                // COLD ALLOC: NativeArray<int>[1] — deconstruction output count cell — owner: Fabricator
                _deconstructionOutputCount = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_deconstructionOutputCount, nameof(_deconstructionOutputCount));
            }

            if (!_complexRecipeGraphNodes.IsCreated)
            {
                _complexRecipeGraphNodes = new NativeArray<int2>(CraftingSystem.MaxComplexRecipeNodeCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_complexRecipeGraphNodes, nameof(_complexRecipeGraphNodes));
            }

            if (!_complexRecipeGraphEdges.IsCreated)
            {
                _complexRecipeGraphEdges = new NativeArray<int2>(CraftingSystem.MaxComplexRecipeEdgeCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_complexRecipeGraphEdges, nameof(_complexRecipeGraphEdges));
            }

            if (!_complexRecipeGraphInDegrees.IsCreated)
            {
                _complexRecipeGraphInDegrees = new NativeArray<int>(CraftingSystem.MaxComplexRecipeNodeCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_complexRecipeGraphInDegrees, nameof(_complexRecipeGraphInDegrees));
            }

            if (!_complexRecipeGraphQueue.IsCreated)
            {
                _complexRecipeGraphQueue = new NativeArray<int>(CraftingSystem.MaxComplexRecipeNodeCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_complexRecipeGraphQueue, nameof(_complexRecipeGraphQueue));
            }

            if (!_complexRecipeRawCosts.IsCreated)
            {
                _complexRecipeRawCosts = new NativeArray<int2>(CraftingSystem.MaxRecipeIngredientCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_complexRecipeRawCosts, nameof(_complexRecipeRawCosts));
            }

            if (!_complexRecipeRawCostCount.IsCreated)
            {
                _complexRecipeRawCostCount = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_complexRecipeRawCostCount, nameof(_complexRecipeRawCostCount));
            }

            if (!_complexRecipeGraphStatus.IsCreated)
            {
                _complexRecipeGraphStatus = new NativeArray<byte>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_complexRecipeGraphStatus, nameof(_complexRecipeGraphStatus));
            }

            if (!_unlockedRecipes.IsCreated)
            {
                // COLD ALLOC: NativeArray<UInt64>[8] - recipe unlock bitset for fabricator craft gate - owner: Fabricator
                _unlockedRecipes = new NativeArray<ulong>(MaxUnlockedRecipeWords, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(_unlockedRecipes, nameof(_unlockedRecipes));
                _unlockMaskDirty = true;
            }
        }

        private void DisposeCraftingScratch()
        {
            if (_networkReservation != null)
            {
                BaseLogisticsNetwork.RollbackReserved(_networkReservation);
                _networkReservation = null;
            }

            if (_craftInventoryCounts.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelHashMap(NativeMemoryOwner, nameof(_craftInventoryCounts));
                _craftInventoryCounts.Dispose();
            }

            DisposeTrackedNativeArray(ref _craftRecipeCosts);
            DisposeTrackedNativeArray(ref _craftRecipeEvaluationResult);
            DisposeTrackedNativeArray(ref _deconstructionRecipeOutputs);
            DisposeTrackedNativeArray(ref _deconstructionOutputCount);
            DisposeTrackedNativeArray(ref _complexRecipeGraphNodes);
            DisposeTrackedNativeArray(ref _complexRecipeGraphEdges);
            DisposeTrackedNativeArray(ref _complexRecipeGraphInDegrees);
            DisposeTrackedNativeArray(ref _complexRecipeGraphQueue);
            DisposeTrackedNativeArray(ref _complexRecipeRawCosts);
            DisposeTrackedNativeArray(ref _complexRecipeRawCostCount);
            DisposeTrackedNativeArray(ref _complexRecipeGraphStatus);
            DisposeTrackedNativeArray(ref _unlockedRecipes);
        }

        private static void RegisterTrackedNativeArray<T>(NativeArray<T> array, string label) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.RegisterNativeArray(
                array,
                NativeMemoryOwner,
                label,
                NativeMemoryLifetime);
        }

        private static void DisposeTrackedNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        private bool TrySynthesizeCraftOutput(RecipeData recipe, ItemData result, int quantityOverride)
        {
            if (recipe == null || result == null)
                return false;

            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry == null)
                return false;

            int quantity = math.max(1, quantityOverride);
            ResolveCraftOutputPose(out Vector3 spawnPosition, out Vector3 velocityChange);
            bool synthesized = registry.TryRegisterDroppedItem(result, quantity, spawnPosition, Vector3.zero, velocityChange);
            if (!synthesized)
                return false;

            CraftingEvents.TryRaiseCraftOutputSynthesized(
                new CraftedItemSynthesisEvent(result, quantity, spawnPosition, velocityChange));
            return true;
        }

        private bool TryEmitCraftOverflowStack(ItemData result, int quantity)
        {
            if (result == null || quantity <= 0)
                return false;

            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry == null)
                return false;

            ResolveCraftOutputPose(out Vector3 spawnPosition, out Vector3 velocityChange);
            bool synthesized = registry.TryRegisterDroppedItem(result, quantity, spawnPosition, Vector3.zero, velocityChange);
            if (!synthesized)
                return false;

            CraftingEvents.TryRaiseCraftOutputSynthesized(
                new CraftedItemSynthesisEvent(result, quantity, spawnPosition, velocityChange));
            return true;
        }

        /// <summary>
        /// Grinds one crafted item back into authored salvage stacks.
        /// </summary>
        public bool TryDeconstructItem(int itemHashId)
        {
            if (itemHashId == 0 || _playerInventory == null)
                return false;

            Hecton8.SaveSystem.ItemCatalog itemCatalog = _playerInventory.ItemCatalog;
            if (itemCatalog == null)
                return false;

            ItemData targetItem = itemCatalog.FindByHash(itemHashId);
            if (targetItem == null || targetItem.DeconstructYieldCount <= 0)
                return false;

            if (!_playerInventory.TryRemoveFirstMatchingItemByHash(itemHashId))
                return false;

            EnsureCraftingScratch();
            if (!CraftingSystem.TryBuildDeconstructionYieldBuffer(
                    targetItem,
                    _deconstructionRecipeOutputs,
                    _deconstructionOutputCount))
            {
                _playerInventory.TryAddItem(itemHashId, 1);
                return false;
            }

            int outputCount = _deconstructionOutputCount[0];
            if (outputCount <= 0)
            {
                _playerInventory.TryAddItem(itemHashId, 1);
                return false;
            }

            ResolveDeconstructionOutputPose(out Vector3 spawnPosition, out Vector3 velocityChange);
            bool emittedAny = false;

            for (int outputIndex = 0; outputIndex < outputCount; outputIndex++)
            {
                int2 output = _deconstructionRecipeOutputs[outputIndex];
                if (output.x == 0 || output.y <= 0)
                    continue;

                ItemData outputItem = itemCatalog.FindByHash(output.x);
                if (outputItem == null)
                    continue;

                if (!TryEmitDeconstructionYield(outputItem, output.x, output.y, spawnPosition, velocityChange))
                    continue;

                CraftingEvents.TryRaiseCraftOutputSynthesized(
                    new CraftedItemSynthesisEvent(outputItem, output.y, spawnPosition, velocityChange));
                emittedAny = true;
            }

            if (!emittedAny)
                _playerInventory.TryAddItem(itemHashId, 1);

            return emittedAny;
        }

        private bool TryEmitDeconstructionYield(
            ItemData outputItem,
            int itemHashId,
            int quantity,
            Vector3 spawnPosition,
            Vector3 velocityChange)
        {
            if (outputItem == null || itemHashId == 0 || quantity <= 0)
                return false;

            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry != null &&
                registry.TryRegisterDroppedItem(outputItem, quantity, spawnPosition, Vector3.zero, velocityChange))
            {
                return true;
            }

            return quantity == 1 && _playerInventory != null && _playerInventory.TryAddItem(itemHashId, 1);
        }

        private void ResolveCraftOutputPose(out Vector3 spawnPosition, out Vector3 velocityChange)
        {
            Transform origin = outputSocket != null ? outputSocket : transform;
            Vector3 localDirection = NormalizeOrFallbackFast(outputDirectionLocal, Vector3.forward);
            Vector3 worldDirection = origin.TransformDirection(localDirection);
            worldDirection = NormalizeOrFallbackFast(worldDirection, origin.forward);

            spawnPosition = origin.position + worldDirection * outputForwardOffset + Vector3.up * outputLiftOffset;
            velocityChange = worldDirection * outputVelocityChange + Vector3.up * outputUpwardVelocityChange;
        }

        private void ResolveDeconstructionOutputPose(out Vector3 spawnPosition, out Vector3 velocityChange)
        {
            Transform origin = deconstructOutputSocket != null ? deconstructOutputSocket : (outputSocket != null ? outputSocket : transform);
            Vector3 localDirection = NormalizeOrFallbackFast(deconstructOutputDirectionLocal, Vector3.forward);
            Vector3 worldDirection = origin.TransformDirection(localDirection);
            worldDirection = NormalizeOrFallbackFast(worldDirection, origin.forward);

            spawnPosition = origin.position + worldDirection * deconstructOutputForwardOffset + Vector3.up * deconstructOutputLiftOffset;
            velocityChange = worldDirection * deconstructOutputVelocityChange + Vector3.up * deconstructOutputUpwardVelocityChange;
        }

        private static Vector3 NormalizeOrFallbackFast(Vector3 direction, Vector3 fallback)
        {
            float sqrMagnitude = direction.sqrMagnitude;
            if (sqrMagnitude <= 0.0001f)
                return fallback;

            if (math.abs(sqrMagnitude - 1f) <= 0.02f)
                return direction;

            return direction * math.rsqrt(sqrMagnitude);
        }

        private static int CountAvailableItemInInventory(PlayerInventory inventory, ItemData item)
        {
            if (inventory == null || item == null)
                return 0;

            return inventory.CountAvailableTotal(ComputeItemHash(item));
        }

        private static int ComputeItemHash(ItemData item)
        {
            return item != null ? item.PersistentHashId : 0;
        }

        private bool TryAccumulateNetworkCost(int itemHashId, int amount)
        {
            if (itemHashId == 0 || amount <= 0)
                return false;

            for (int i = 0; i < _networkCostCount; i++)
            {
                if (_networkCostItemHashes[i] != itemHashId)
                    continue;

                _networkCostAmounts[i] += amount;
                return true;
            }

            if (_networkCostCount >= MaxNetworkCraftCosts)
                return false;

            _networkCostItemHashes[_networkCostCount] = itemHashId;
            _networkCostAmounts[_networkCostCount] = amount;
            _networkCostCount++;
            return true;
        }

        private int CountReclaimableIngredientCells(RecipeData recipe, int multiplier = 1)
        {
            if (recipe == null || recipe.ingredients == null || _playerInventory == null)
                return 0;

            int total = 0;
            List<InventoryCost> costs = recipe.ingredients;
            int safeMultiplier = Mathf.Max(1, multiplier);

            for (int i = 0, count = costs.Count; i < count; i++)
            {
                InventoryCost cost = costs[i];
                if (cost == null || cost.item == null) continue;

                int localAvailable = CountAvailableItemInInventory(_playerInventory, cost.item);
                int requiredAmount = GetAdjustedIngredientAmount(cost) * safeMultiplier;
                int removableCount = localAvailable < requiredAmount ? localAvailable : requiredAmount;
                total += cost.item.CellArea * removableCount;
            }

            return total;
        }

        private bool ConsumeIngredients(RecipeData recipe, int multiplier = 1)
        {
            if (recipe == null || recipe.ingredients == null || _playerInventory == null || _playerInventory.Grid == null)
                return false;

            EnsureCraftingScratch();
            _localCraftReservationCount = 0;
            _networkCostCount = 0;

            if (_networkReservation != null)
            {
                BaseLogisticsNetwork.RollbackReserved(_networkReservation);
                _networkReservation = null;
            }

            int safeMultiplier = Mathf.Max(1, multiplier);
            if (TryReserveDirectFastFailRecipeCosts(recipe, safeMultiplier))
                return true;

            RefundIngredients();

            if (CraftingSystem.TryBuildRecipeCostBuffer(recipe, this, _craftRecipeCosts, out int recipeCostCount, safeMultiplier) &&
                TryReserveIngredientCostBuffer(_craftRecipeCosts, recipeCostCount))
                return true;

            RefundIngredients();

            if (CraftingSystem.TryBuildTotalRawCostBuffer(
                    recipe,
                    this,
                    _playerInventory.ItemCatalog,
                    _complexRecipeGraphNodes,
                    _complexRecipeGraphEdges,
                    _complexRecipeGraphInDegrees,
                    _complexRecipeGraphQueue,
                    _complexRecipeRawCosts,
                    _complexRecipeRawCostCount,
                    _complexRecipeGraphStatus,
                    safeMultiplier))
            {
                if (TryReserveIngredientCostBuffer(_complexRecipeRawCosts, _complexRecipeRawCostCount[0]))
                    return true;

                RefundIngredients();
            }

            return false;
        }

        private bool TryReserveIngredientCostBuffer(NativeArray<int2> costs, int costCount)
        {
            if (!costs.IsCreated || costCount <= 0 || _playerInventory == null)
                return false;

            _localCraftReservationCount = 0;
            _networkCostCount = 0;
            if (_networkReservation != null)
            {
                BaseLogisticsNetwork.RollbackReserved(_networkReservation);
                _networkReservation = null;
            }

            for (int costIndex = 0; costIndex < costCount; costIndex++)
            {
                int2 cost = costs[costIndex];
                if (cost.x == 0 || cost.y <= 0)
                    continue;

                int remaining = cost.y;
                if (!_playerInventory.TryReserveAvailableQuantityForCraft(
                        cost.x,
                        remaining,
                        _localCraftReservations,
                        ref _localCraftReservationCount,
                        out int localTake))
                {
                    return false;
                }

                if (localTake > 0)
                    remaining -= localTake;

                if (remaining > 0 && !TryAccumulateNetworkCost(cost.x, remaining))
                    return false;
            }

            if (_networkCostCount <= 0)
                return true;

            PowerGrid gridRef = _powerNode != null ? _powerNode.Grid : null;
            return BaseLogisticsNetwork.TryReserveResources(
                gridRef,
                _networkCostItemHashes,
                _networkCostAmounts,
                _networkCostCount,
                out _networkReservation);
        }

        private void RefundIngredients()
        {
            if (_activeRecipe == null || _playerInventory == null || _playerInventory.Grid == null) return;

            _playerInventory.ReleaseCraftReservations(_localCraftReservations, _localCraftReservationCount);
            _localCraftReservationCount = 0;

            if (_networkReservation != null)
            {
                BaseLogisticsNetwork.RollbackReserved(_networkReservation);
                _networkReservation = null;
            }
            _networkCostCount = 0;

        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — DISTANCE CHECK
        // ══════════════════════════════════════════════════════════

        private bool IsPlayerInRange()
        {
            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return false;

            CacheFabricatorAup();
            double maxUseDistanceSq = (double)maxUseDistance * maxUseDistance;
            return AbsoluteUniversePosition.DistanceSq(in playerAup, in _fabricatorAup) <= maxUseDistanceSq;
        }

        private void CacheFabricatorAup()
        {
            if (_fabricatorAupCached)
                return;

            _fabricatorAupCached = TryResolveAupFromRuntimeOrigin(transform.position, out _fabricatorAup);
        }

        private static bool TryResolveAupFromRuntimeOrigin(
            Vector3 runtimePosition,
            out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFiniteRuntimePosition(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        private static bool TryOffsetAupByRuntimeDelta(
            in AbsoluteUniversePosition referenceAup,
            Vector3 referenceRuntimePosition,
            Vector3 targetRuntimePosition,
            out AbsoluteUniversePosition targetAup)
        {
            targetAup = default;
            if (!referenceAup.IsFinite() ||
                !IsFiniteRuntimePosition(referenceRuntimePosition) ||
                !IsFiniteRuntimePosition(targetRuntimePosition))
            {
                return false;
            }

            double3 localDelta = new double3(
                (double)targetRuntimePosition.x - referenceRuntimePosition.x,
                (double)targetRuntimePosition.y - referenceRuntimePosition.y,
                (double)targetRuntimePosition.z - referenceRuntimePosition.z);
            targetAup = AbsoluteUniversePosition.OffsetMeters(in referenceAup, localDelta);
            return targetAup.IsFinite();
        }

        private static bool IsFiniteRuntimePosition(Vector3 position)
        {
            return float.IsFinite(position.x) &&
                   float.IsFinite(position.y) &&
                   float.IsFinite(position.z);
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            if (_playerMovement != null)
            {
                playerAup = _playerMovement.CurrentAup;
                return true;
            }

            TryCachePlayerMovement(_playerTransform);
            if (_playerMovement != null)
            {
                playerAup = _playerMovement.CurrentAup;
                return true;
            }

            playerAup = default;
            return false;
        }

        private void TryCachePlayerMovement(Transform interactor)
        {
            if (_playerMovementLookupAttempted || interactor == null)
                return;

            interactor.TryGetComponent(out _playerMovement);
            _playerMovementLookupAttempted = true;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — AUDIO
        // ══════════════════════════════════════════════════════════

        private void PlaySound(AudioClip clip)
        {
            if (clip == null)
                return;

            _pendingAudioClip = clip;
            _pendingAudioPosition = transform.position;
            _pendingAudioDirty = true;
            TryRegisterLateFrame();
        }

        private void FlushPendingAudio()
        {
            if (!_pendingAudioDirty)
                return;

            AudioClip clip = _pendingAudioClip;
            Vector3 position = _pendingAudioPosition;
            _pendingAudioClip = null;
            _pendingAudioDirty = false;
            if (clip == null)
                return;

            _audioService?.PlayAtPoint(clip, position);
        }

        private void RaiseFabricatorProgressAudioPing()
        {
            float pitchCarrierHz = Mathf.Clamp(900f + (_activeCraftPowerMultiplier * 180f), 900f, 2200f);
            ProceduralAudioEvents.TryRaiseAudioPingTriggered(
                transform.position,
                Mathf.Clamp01(0.18f + _activeCraftPowerMultiplier * 0.08f),
                0.08f,
                1f,
                pitchCarrierHz,
                ProceduralAudioPingKind.MechanicalWhirr);
        }

        private static void RaiseStorageCapacityExceededBark()
        {
            AcousticEcholocationBarkEvents.RaiseStorageCapacityExceeded();
        }

        private static void RaiseFabricatorProgressHaptics(float progress)
        {
            float finalPulseT = math.saturate((progress - 0.9f) * 10f);
            float finalPulse01 = finalPulseT * finalPulseT * (3f - (2f * finalPulseT));
            float lowFrequencyIntensity = math.saturate(math.lerp(0.12f, 0.3f, progress) + finalPulse01 * 0.35f);
            float highFrequencyIntensity = math.saturate(0.025f + finalPulse01 * 0.05f);
            float pulseFrequencyHz = math.lerp(18f, 30f, finalPulse01);
            ToolHapticsRuntime.TryEnqueueSinusoidalCommand(
                lowFrequencyIntensity,
                highFrequencyIntensity,
                0.18f,
                pulseFrequencyHz,
                finalPulse01 > 0f ? FabricatorFinalHapticPriority : FabricatorHapticPriority,
                FabricatorHapticMotorMask);
        }

        private void TriggerCraftFailureFeedback()
        {
            _errorFlashRemainingSeconds = Mathf.Max(_errorFlashRemainingSeconds, errorFlashDurationSeconds);
            ApplyErrorFeedback(1f);
            CraftingEvents.TryRaiseCraftFailed(this);
            PlaySound(fabricationErrorBuzzerSound);
            ProceduralAudioEvents.TryRaiseAudioPingTriggered(
                transform.position,
                0.85f,
                0.12f,
                1f,
                180f,
                ProceduralAudioPingKind.MechanicalWhirr);
        }

        private void TriggerSparkProxyLight()
        {
            _sparkProxyLightRemainingSeconds = Mathf.Max(_sparkProxyLightRemainingSeconds, Mathf.Max(0.01f, sparkProxyLightDurationSeconds));
            _sparkLightTickSleeping = false;
            UpdateSparkProxyLightRegistration();
            TryRegisterSparkLightTick();
        }

        private void UpdateSparkProxyLightRegistration()
        {
            if (_sparkProxyLightKey == 0 || !(_sparkProxyLightRemainingSeconds > 0f))
                return;

            Transform origin = outputSocket != null ? outputSocket : transform;
            if (origin == null)
                return;

            Vector3 position = origin.position;
            if (!TryResolveAupFromRuntimeOrigin(position, out AbsoluteUniversePosition positionAup))
            {
                UnregisterSparkProxyLight();
                return;
            }

            float normalizedLifetime = Mathf.Clamp01(_sparkProxyLightRemainingSeconds / Mathf.Max(0.01f, sparkProxyLightDurationSeconds));
            float intensity = sparkProxyLightIntensity * normalizedLifetime * Mathf.Max(1f, _activeCraftPowerMultiplier);
            ProxyLightData lightData = ProxyLightData.CreateTransientPoint(
                positionAup,
                position,
                sparkProxyLightColor.linear,
                sparkProxyLightRangeMeters,
                intensity,
                Time.unscaledTime);

            _sparkProxyLightRegistered = ProxyLightRegistry.RegisterOrUpdate(_sparkProxyLightKey, in lightData) || _sparkProxyLightRegistered;
        }

        private void UnregisterSparkProxyLight()
        {
            if (!_sparkProxyLightRegistered || _sparkProxyLightKey == 0)
                return;

            ProxyLightRegistry.Unregister(_sparkProxyLightKey);
            _sparkProxyLightRegistered = false;
        }

        private void BeginAssemblyVisual(RecipeData recipe)
        {
            _pendingAssemblyBeginRecipe = recipe;
            _pendingAssemblyVisualCommand = 1;
            TryRegisterLateFrame();
        }

        private void FlushBeginAssemblyVisual(RecipeData recipe)
        {
            _assemblyTargetHash = recipe != null ? unchecked((uint)ComputeItemHash(recipe.resultItem)) : FabricatorWeldingFallbackHash;
            _assemblyMaterialSwapped = false;
            _assemblyActualMaterial = null;
            _assemblyProgress01 = 0f;
            _fabricationJobSlot = -1;

            if (assemblyPreviewMeshFilter == null ||
                assemblyPreviewRenderer == null ||
                hologramAssemblyMaterial == null ||
                recipe == null ||
                recipe.resultItem == null ||
                !TryResolveAssemblySource(recipe.resultItem, out Mesh sourceMesh, out Material actualMaterial))
            {
                FlushEndAssemblyVisual();
                return;
            }

            assemblyPreviewMeshFilter.sharedMesh = sourceMesh;
            _assemblyActualMaterial = actualMaterial;

            float padding = Mathf.Max(0f, assemblyHeightPadding);
            ResolveAssemblyFabricatorLocalHeightBounds(sourceMesh, assemblyPreviewMeshFilter.transform, padding, out _assemblyBaseY, out _assemblyTopY);
            _assemblyCurrentHeightY = _assemblyBaseY;
            _assemblyQuality = ResolveAssemblyQuality();

            assemblyPreviewRenderer.sharedMaterial = hologramAssemblyMaterial;
            assemblyPreviewRenderer.shadowCastingMode = ShadowCastingMode.Off;
            assemblyPreviewRenderer.receiveShadows = false;
            assemblyPreviewRenderer.enabled = true;
            _assemblyPreviewActive = true;
            BeginFabricationVaultJob(recipe);
            ApplyAssemblyVisualProgress(0f, false);
        }

        private void BeginFabricationVaultJob(RecipeData recipe)
        {
            if (recipe == null)
                return;

            Transform target = outputSocket != null ? outputSocket : transform;
            CacheFabricatorAup();
            if (!_fabricatorAupCached ||
                !TryOffsetAupByRuntimeDelta(in _fabricatorAup, transform.position, target.position, out AbsoluteUniversePosition targetAup))
            {
                return;
            }

            AbsoluteUniversePosition fabricatorAup = _fabricatorAup;
            float duration = Mathf.Max(0.001f, recipe.craftTime * Mathf.Max(1, _activeCraftMultiplier));
            float powerDrainWatts = Mathf.Max(0f, craftPowerDraw * Mathf.Max(1f, _activeCraftPowerMultiplier));

            if (FabricationAssemblerRuntime.TryBeginJob(
                    ResolveFabricatorSignalHash(),
                    _assemblyTargetHash,
                    targetAup.ToAbsoluteDouble3(),
                    fabricatorAup.ToAbsoluteDouble3(),
                    duration,
                    ResolveCraftThermalThrottleMultiplier(),
                    powerDrainWatts,
                    _assemblyBaseY,
                    _assemblyTopY,
                    transform.worldToLocalMatrix,
                    false,
                    out int slot))
            {
                _fabricationJobSlot = slot;
            }
        }

        private bool TryResolveAssemblySource(ItemData item, out Mesh sourceMesh, out Material actualMaterial)
        {
            sourceMesh = null;
            actualMaterial = null;
            GameObject prefab = item != null ? item.worldPrefab : null;
            if (prefab == null)
            {
                sourceMesh = ResolveAssemblyFallbackMesh();
                return sourceMesh != null;
            }

            MeshFilter sourceFilter = prefab.GetComponent<MeshFilter>();
            MeshRenderer sourceRenderer = prefab.GetComponent<MeshRenderer>();
            if (sourceFilter == null)
                sourceFilter = prefab.GetComponentInChildren<MeshFilter>(true);
            if (sourceRenderer == null)
                sourceRenderer = prefab.GetComponentInChildren<MeshRenderer>(true);

            if (sourceFilter != null)
            {
                sourceMesh = sourceFilter.sharedMesh;
                actualMaterial = sourceRenderer != null ? sourceRenderer.sharedMaterial : null;
            }

            if (sourceMesh != null)
                return true;

            SkinnedMeshRenderer skinnedRenderer = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skinnedRenderer == null)
            {
                sourceMesh = ResolveAssemblyFallbackMesh();
                return sourceMesh != null;
            }

            sourceMesh = skinnedRenderer.sharedMesh;
            actualMaterial = skinnedRenderer.sharedMaterial;
            if (sourceMesh != null)
                return true;

            sourceMesh = ResolveAssemblyFallbackMesh();
            actualMaterial = null;
            return sourceMesh != null;
        }

        private Mesh ResolveAssemblyFallbackMesh()
        {
            if (assemblyFallbackMesh != null)
                return assemblyFallbackMesh;

            return EnsureSharedAssemblyFallbackMesh();
        }

        private static Mesh EnsureSharedAssemblyFallbackMesh()
        {
            if (s_sharedAssemblyFallbackMesh != null)
                return s_sharedAssemblyFallbackMesh;

            // COLD ALLOC: Mesh[1] - shared hologram fallback for craftables without world prefab - owner: Fabricator
            Mesh mesh = new Mesh
            {
                name = "GEN_FabricatorAssemblyFallbackMesh",
                hideFlags = HideFlags.HideAndDontSave
            };

            // COLD ALLOC: Vector3[6] - one-time octahedral fallback vertices - owner: Fabricator
            Vector3[] vertices =
            {
                new Vector3(0f, 0.36f, 0f),
                new Vector3(0f, -0.36f, 0f),
                new Vector3(0f, 0f, 0.28f),
                new Vector3(0.28f, 0f, 0f),
                new Vector3(0f, 0f, -0.28f),
                new Vector3(-0.28f, 0f, 0f)
            };

            // COLD ALLOC: int[24] - one-time octahedral fallback triangles - owner: Fabricator
            int[] triangles =
            {
                0, 2, 3,
                0, 3, 4,
                0, 4, 5,
                0, 5, 2,
                1, 3, 2,
                1, 4, 3,
                1, 5, 4,
                1, 2, 5
            };

            // COLD ALLOC: Vector3[6] - one-time octahedral fallback normals - owner: Fabricator
            Vector3[] normals =
            {
                Vector3.up,
                Vector3.down,
                Vector3.forward,
                Vector3.right,
                Vector3.back,
                Vector3.left
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            s_sharedAssemblyFallbackMesh = mesh;
            return s_sharedAssemblyFallbackMesh;
        }

        private void ResolveAssemblyFabricatorLocalHeightBounds(
            Mesh sourceMesh,
            Transform previewTransform,
            float padding,
            out float baseY,
            out float topY)
        {
            Bounds meshBounds = sourceMesh != null ? sourceMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            if (sourceMesh == null || previewTransform == null)
            {
                baseY = meshBounds.min.y - padding;
                topY = Mathf.Max(baseY + 0.001f, meshBounds.max.y + padding);
                return;
            }

            Matrix4x4 meshToFabricator = transform.worldToLocalMatrix * previewTransform.localToWorldMatrix;
            Vector3 center = meshBounds.center;
            Vector3 extents = meshBounds.extents;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;

            AccumulateAssemblyCornerY(meshToFabricator, new Vector3(center.x - extents.x, center.y - extents.y, center.z - extents.z), ref minY, ref maxY);
            AccumulateAssemblyCornerY(meshToFabricator, new Vector3(center.x + extents.x, center.y - extents.y, center.z - extents.z), ref minY, ref maxY);
            AccumulateAssemblyCornerY(meshToFabricator, new Vector3(center.x - extents.x, center.y + extents.y, center.z - extents.z), ref minY, ref maxY);
            AccumulateAssemblyCornerY(meshToFabricator, new Vector3(center.x + extents.x, center.y + extents.y, center.z - extents.z), ref minY, ref maxY);
            AccumulateAssemblyCornerY(meshToFabricator, new Vector3(center.x - extents.x, center.y - extents.y, center.z + extents.z), ref minY, ref maxY);
            AccumulateAssemblyCornerY(meshToFabricator, new Vector3(center.x + extents.x, center.y - extents.y, center.z + extents.z), ref minY, ref maxY);
            AccumulateAssemblyCornerY(meshToFabricator, new Vector3(center.x - extents.x, center.y + extents.y, center.z + extents.z), ref minY, ref maxY);
            AccumulateAssemblyCornerY(meshToFabricator, new Vector3(center.x + extents.x, center.y + extents.y, center.z + extents.z), ref minY, ref maxY);

            if (minY > maxY)
            {
                minY = meshBounds.min.y;
                maxY = meshBounds.max.y;
            }

            baseY = minY - padding;
            topY = Mathf.Max(baseY + 0.001f, maxY + padding);
        }

        private static void AccumulateAssemblyCornerY(Matrix4x4 meshToFabricator, Vector3 corner, ref float minY, ref float maxY)
        {
            float y = meshToFabricator.MultiplyPoint3x4(corner).y;
            if (y < minY)
                minY = y;
            if (y > maxY)
                maxY = y;
        }

        private static float ResolveAssemblyQuality()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private void ApplyAssemblyVisualProgress(float progress01, bool paused)
        {
            if (!_assemblyPreviewActive || _assemblyMaterialSwapped || assemblyPreviewRenderer == null)
                return;

            _assemblyProgress01 = math.saturate(progress01);
            _assemblyCurrentHeightY = math.lerp(_assemblyBaseY, _assemblyTopY, _assemblyProgress01);
            UpdateFabricationVaultSlot(paused);
        }

        private void CompleteAssemblyVisual()
        {
            _pendingAssemblyVisualCommand = 2;
            TryRegisterLateFrame();
        }

        private void FlushCompleteAssemblyVisual()
        {
            if (!_assemblyPreviewActive)
                return;

            ApplyAssemblyVisualProgress(1f, false);
            if (assemblyPreviewRenderer != null)
            {
                if (_assemblyActualMaterial != null)
                {
                    assemblyPreviewRenderer.sharedMaterial = _assemblyActualMaterial;
                }

                assemblyPreviewRenderer.shadowCastingMode = ShadowCastingMode.Off;
                assemblyPreviewRenderer.receiveShadows = false;
                assemblyPreviewRenderer.enabled = true;
            }

            _assemblyProgress01 = 1f;
            _assemblyCurrentHeightY = _assemblyTopY;
            _assemblyMaterialSwapped = true;
            FabricationAssemblerRuntime.ClearSlot(_fabricationJobSlot);
            _fabricationJobSlot = -1;
        }

        private void EndAssemblyVisual()
        {
            _pendingAssemblyVisualCommand = 3;
            _pendingAssemblyBeginRecipe = null;
            TryRegisterLateFrame();
        }

        private void FlushEndAssemblyVisual()
        {
            FabricationAssemblerRuntime.ClearSlot(_fabricationJobSlot);
            _fabricationJobSlot = -1;
            _assemblyPreviewActive = false;
            _assemblyMaterialSwapped = false;
            _assemblyActualMaterial = null;
            _assemblyTargetHash = 0u;
            _assemblyProgress01 = 0f;
            _assemblyCurrentHeightY = _assemblyBaseY;

            if (assemblyPreviewRenderer != null)
            {
                assemblyPreviewRenderer.shadowCastingMode = ShadowCastingMode.Off;
                assemblyPreviewRenderer.receiveShadows = false;
                assemblyPreviewRenderer.enabled = false;
            }

            if (assemblyPreviewMeshFilter != null)
                assemblyPreviewMeshFilter.sharedMesh = null;
        }

        private void PublishWeldingToolAcoustic(float progress01)
        {
            float progress = math.saturate(progress01);
            uint frame = unchecked((uint)math.max(0, SystemDispatcher.CurrentFrameIndex));
            SignalBus<ToolAcousticSignal>.TryPush(new ToolAcousticSignal
            {
                ToolHash = FabricatorToolHash,
                TargetHash = _assemblyTargetHash != 0u ? _assemblyTargetHash : FabricatorWeldingFallbackHash,
                Progress01 = progress,
                PitchScale = math.lerp(fabricationWeldingLoopMinPitch, fabricationWeldingLoopMaxPitch, progress),
                Intensity01 = math.saturate(0.32f + _activeCraftPowerMultiplier * 0.12f),
                Frame = frame,
                State = ToolAcousticStateWelding,
                Flags = IsPausedNoPower ? PowerDrainFlagPaused : (byte)0
            });
        }

        private void PublishPowerDrainSignal(float progressPerSecond, float progress01, bool paused)
        {
            float speed = math.max(0f, progressPerSecond);
            float watts = math.max(0f, craftPowerDraw * Mathf.Max(1f, _activeCraftPowerMultiplier) * speed);
            if (!(watts > 0f) && !paused)
                return;

            uint frame = unchecked((uint)math.max(0, SystemDispatcher.CurrentFrameIndex));
            SignalBus<PowerDrainSignal>.TryPush(new PowerDrainSignal
            {
                ConsumerHash = ResolveFabricatorSignalHash(),
                NetworkHash = 0u,
                Watts = watts,
                Progress01 = math.saturate(progress01),
                Frame = frame,
                Reason = PowerDrainReasonFabrication,
                Flags = paused ? PowerDrainFlagPaused : (byte)0
            });
        }

        private void PublishCraftingStartedSignal(RecipeData recipe, int multiplier)
        {
            uint frame = unchecked((uint)math.max(0, SystemDispatcher.CurrentFrameIndex));
            SignalBus<CraftingStartedSignal>.TryPush(new CraftingStartedSignal
            {
                FabricatorHash = ResolveFabricatorSignalHash(),
                RecipeHash = ComputeRecipeSignalHash(recipe),
                ResultItemHash = recipe != null ? unchecked((uint)ComputeItemHash(recipe.resultItem)) : 0u,
                Frame = frame,
                Multiplier = (ushort)math.min(math.max(1, multiplier), ushort.MaxValue),
                Flags = 0
            });
        }

        private void PublishCraftingCompletedSignal(RecipeData recipe, ItemData item, int quantity)
        {
            uint frame = unchecked((uint)math.max(0, SystemDispatcher.CurrentFrameIndex));
            CraftingSignalRoute.TryQueueCompleted(new CraftingCompletedSignal
            {
                FabricatorHash = ResolveFabricatorSignalHash(),
                RecipeHash = ComputeRecipeSignalHash(recipe),
                ResultItemHash = unchecked((uint)ComputeItemHash(item)),
                Frame = frame,
                Quantity = (ushort)math.min(math.max(0, quantity), ushort.MaxValue),
                Flags = 0
            });
        }

        private void PublishCraftItemAcquiredSignal(ItemData item, int quantity)
        {
            int itemHash = ComputeItemHash(item);
            if (itemHash == 0 || quantity <= 0)
                return;

            ResolveCraftOutputPose(out Vector3 spawnPosition, out _);
            if (!TryResolveAupFromRuntimeOrigin(spawnPosition, out AbsoluteUniversePosition positionAup))
                return;

            SignalBus<ItemAcquiredSignal>.TryPush(new ItemAcquiredSignal
            {
                PositionAup = positionAup,
                ItemHash = unchecked((uint)itemHash),
                OreHash = 0u,
                Quantity = (ushort)math.min(math.max(1, quantity), ushort.MaxValue),
                SourceKind = ItemAcquiredSourceFabricator,
                Flags = 0,
                Frame = unchecked((uint)math.max(0, SystemDispatcher.CurrentFrameIndex))
            });
        }

        private uint ResolveFabricatorSignalHash()
        {
            return unchecked((uint)EntityId.ToULong(GetEntityId()));
        }

        private static uint ComputeRecipeSignalHash(RecipeData recipe)
        {
            return recipe != null && !string.IsNullOrWhiteSpace(recipe.name)
                ? unchecked((uint)LocHash.Compute(recipe.name))
                : 0u;
        }

        private static void PublishFabricatorActiveCountBlackBox()
        {
            GlobalTelemetryBus.PublishModTelemetry(
                FabricatorTelemetryHash,
                FabricatorActiveCountHash,
                _activeFabricators.Count);
        }

        private void TryRegisterAssemblyOriginShiftListener()
        {
            if (_assemblyOriginShiftListenerRegistered || !Application.isPlaying)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _assemblyOriginShiftListenerRegistered = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        private void TryUnregisterAssemblyOriginShiftListener()
        {
            if (!_assemblyOriginShiftListenerRegistered)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _assemblyOriginShiftListenerRegistered = false;
        }

        private void SetFabricationSparksActive(bool active)
        {
            _pendingFabricationSparksActive = active;
            _pendingFabricationSparksDirty = true;
            TryRegisterLateFrame();
        }

        private void FlushSetFabricationSparksActive(bool active)
        {
            UpdateWeldingAudioLoop(active, Mathf.Max(1f, _activeCraftPowerMultiplier));

            if (fabricationSparks == null)
                return;

            ParticleSystem.EmissionModule emission = fabricationSparks.emission;
            float rate = active ? fabricationSparksBaseRate * Mathf.Max(1f, _activeCraftPowerMultiplier) : 0f;
            emission.rateOverTime = rate;

            if (active)
            {
                _fabricationSparksPlaying = fabricationSparks.isPlaying;
                return;
            }

            _sparkProxyLightRemainingSeconds = 0f;
            UnregisterSparkProxyLight();
            TryUnregisterSparkLightTick();
            if (_fabricationSparksPlaying)
            {
                fabricationSparks.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                _fabricationSparksPlaying = false;
            }
        }

        private void UpdateWeldingAudioLoop(bool active, float intensity)
        {
            AudioSource source = fabricationWeldingLoopSource;
            if (source == null)
                return;

            if (!active)
            {
                if (source.isPlaying)
                    source.Stop();
                return;
            }

            if (fabricationWeldingLoopClip != null && source.clip != fabricationWeldingLoopClip)
                source.clip = fabricationWeldingLoopClip;

            source.loop = true;
            source.volume = math.saturate(intensity * 0.18f) * math.saturate(fabricationWeldingLoopMaxVolume);

            float now = Time.unscaledTime;
            if (now >= _weldingLoopNextPitchUpdateTime)
            {
                _weldingLoopPitchSeed = (_weldingLoopPitchSeed * 1664525u) + 1013904223u;
                float randomTilt = math.lerp(0.96f, 1.04f, (_weldingLoopPitchSeed & 0x00FFFFFFu) * (1f / 16777215f));
                float pitch01 = math.saturate(intensity * 0.25f);
                _weldingLoopPitch = math.clamp(
                    math.lerp(fabricationWeldingLoopMinPitch, fabricationWeldingLoopMaxPitch, pitch01) * randomTilt,
                    fabricationWeldingLoopMinPitch,
                    fabricationWeldingLoopMaxPitch);
                _weldingLoopNextPitchUpdateTime = now + Mathf.Max(0.01f, fabricationWeldingLoopPitchUpdateSeconds);
            }

            source.pitch = _weldingLoopPitch;
            if (!source.isPlaying)
                source.Play();
        }

        private void UpdateErrorFeedback(float deltaSeconds)
        {
            if (!(_errorFlashRemainingSeconds > 0f))
            {
                if (_errorFeedbackApplied)
                    ApplyErrorFeedback(0f);
                return;
            }

            _errorFlashRemainingSeconds = Mathf.Max(0f, _errorFlashRemainingSeconds - Mathf.Max(0f, deltaSeconds));
            float intensity = Mathf.Clamp01(_errorFlashRemainingSeconds / Mathf.Max(0.001f, errorFlashDurationSeconds));
            ApplyErrorFeedback(intensity);
        }

        private void ApplyErrorFeedback(float intensity)
        {
            _pendingErrorFeedbackIntensity = Mathf.Clamp01(intensity);
            _pendingErrorFeedbackDirty = true;
            TryRegisterLateFrame();
        }

        private void FlushApplyErrorFeedback(float intensity)
        {
            if (errorFeedbackRenderers == null || errorFeedbackRenderers.Length == 0)
            {
                _errorFeedbackApplied = intensity > 0f;
                return;
            }

            Color color = errorEmissionColor * Mathf.Clamp01(intensity);
            for (int index = 0; index < errorFeedbackRenderers.Length; index++)
            {
                Renderer renderer = errorFeedbackRenderers[index];
                if (renderer == null)
                    continue;

                renderer.GetPropertyBlock(_errorFeedbackBlock);
                _errorFeedbackBlock.SetColor(EmissionColorId, color);
                renderer.SetPropertyBlock(_errorFeedbackBlock);
            }

            _errorFeedbackApplied = intensity > 0f;
        }

        private void EnsureScanLogSystem()
        {
            CacheScanLogSystem(_scanLogSystem);
        }

        private void CacheScanLogSystem(IScanLogService current)
        {
            if (ReferenceEquals(_scanLogSystem, current))
                return;

            _scanLogSystem = current;
            _observedScanLogRevision = current != null ? current.ChangeRevision : 0u;
            MarkRecipeCacheDirty();
        }

        private void RefreshScanLogRevision()
        {
            EnsureScanLogSystem();
            uint revision = _scanLogSystem != null ? _scanLogSystem.ChangeRevision : 0u;
            if (revision == _observedScanLogRevision)
                return;

            _observedScanLogRevision = revision;
            MarkRecipeCacheDirty();
        }

        private void MarkRecipeCacheDirty()
        {
            _recipeCacheDirty = true;
            _unlockMaskDirty = true;
        }

        private void EnsureRecipeCache()
        {
            RefreshScanLogRevision();
            if (!_recipeCacheDirty)
                return;

            _visibleRecipes.Clear();
            _lockedRecipeCount = 0;

            if (availableRecipes != null)
            {
                for (int i = 0; i < availableRecipes.Count; i++)
                {
                    AppendRecipeToCache(availableRecipes[i]);
                }
            }

            int runtimeRecipeCount = ModRecipeRegistry.Count;
            for (int i = 0; i < runtimeRecipeCount; i++)
            {
                RecipeData recipe = ModRecipeRegistry.GetAt(i);
                if (recipe == null || ContainsAuthoredRecipeReference(recipe))
                    continue;

                AppendRecipeToCache(recipe);
            }

            _recipeCacheDirty = false;
        }

        private void EnsureRecipeUnlockMask()
        {
            EnsureCraftingScratch();
            RefreshScanLogRevision();
            if (!_unlockMaskDirty || !_unlockedRecipes.IsCreated)
                return;

            for (int wordIndex = 0; wordIndex < _unlockedRecipes.Length; wordIndex++)
                _unlockedRecipes[wordIndex] = 0UL;

            int unlockIndex = 0;
            if (availableRecipes != null)
            {
                for (int i = 0; i < availableRecipes.Count && unlockIndex < MaxUnlockedRecipeWords * 64; i++)
                    WriteRecipeUnlockBit(availableRecipes[i], unlockIndex++);
            }

            int runtimeRecipeCount = ModRecipeRegistry.Count;
            for (int i = 0; i < runtimeRecipeCount && unlockIndex < MaxUnlockedRecipeWords * 64; i++)
            {
                RecipeData recipe = ModRecipeRegistry.GetAt(i);
                if (recipe == null || ContainsAuthoredRecipeReference(recipe))
                    continue;

                WriteRecipeUnlockBit(recipe, unlockIndex++);
            }

            _unlockMaskDirty = false;
        }

        private void WriteRecipeUnlockBit(RecipeData recipe, int unlockIndex)
        {
            if (recipe == null || !_unlockedRecipes.IsCreated)
                return;

            int wordIndex = unlockIndex >> RecipeUnlockWordShift;
            if (wordIndex < 0 || wordIndex >= _unlockedRecipes.Length)
                return;

            if (recipe.IsUnlocked(_scanLogSystem))
                _unlockedRecipes[wordIndex] = _unlockedRecipes[wordIndex] | (1UL << (unlockIndex & RecipeUnlockBitMask));
        }

        private bool TryResolveRecipeUnlockIndex(RecipeData recipe, out int unlockIndex)
        {
            unlockIndex = -1;
            if (recipe == null)
                return false;

            int cursor = 0;
            if (availableRecipes != null)
            {
                for (int i = 0; i < availableRecipes.Count; i++)
                {
                    if (ReferenceEquals(availableRecipes[i], recipe))
                    {
                        unlockIndex = cursor;
                        return IsUnlockIndexInRange(unlockIndex);
                    }

                    cursor++;
                }
            }

            int runtimeRecipeCount = ModRecipeRegistry.Count;
            for (int i = 0; i < runtimeRecipeCount; i++)
            {
                RecipeData runtimeRecipe = ModRecipeRegistry.GetAt(i);
                if (runtimeRecipe == null || ContainsAuthoredRecipeReference(runtimeRecipe))
                    continue;

                if (ReferenceEquals(runtimeRecipe, recipe))
                {
                    unlockIndex = cursor;
                    return IsUnlockIndexInRange(unlockIndex);
                }

                cursor++;
            }

            return false;
        }

        private static bool IsUnlockIndexInRange(int unlockIndex)
        {
            return unlockIndex >= 0 && unlockIndex < MaxUnlockedRecipeWords * 64;
        }

        private bool IsRecipeUnlockBitSet(int unlockIndex)
        {
            if (!_unlockedRecipes.IsCreated || !IsUnlockIndexInRange(unlockIndex))
                return false;

            int wordIndex = unlockIndex >> RecipeUnlockWordShift;
            return (_unlockedRecipes[wordIndex] & (1UL << (unlockIndex & RecipeUnlockBitMask))) != 0UL;
        }

        private bool IsRecipeUnlocked(RecipeData recipe)
        {
            if (recipe == null)
                return false;

            EnsureRecipeUnlockMask();
            if (TryResolveRecipeUnlockIndex(recipe, out int unlockIndex))
                return IsRecipeUnlockBitSet(unlockIndex);

            return recipe.IsUnlocked(_scanLogSystem);
        }

        private void AppendRecipeToCache(RecipeData recipe)
        {
            if (recipe == null)
                return;

            if (IsRecipeUnlocked(recipe))
                _visibleRecipes.Add(recipe);
            else
                _lockedRecipeCount++;
        }

        private bool ContainsAuthoredRecipeReference(RecipeData recipe)
        {
            if (recipe == null || availableRecipes == null)
                return false;

            for (int i = 0; i < availableRecipes.Count; i++)
            {
                if (ReferenceEquals(availableRecipes[i], recipe))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Handles deferred mod registry events that affect available recipes.
        /// </summary>
        /// <param name="payload">Unmanaged mod registry payload.</param>
        public void OnModRegistryEvent(in ModRegistryEventPayload payload)
        {
            if ((ModRegistryEventType)payload.EventType != ModRegistryEventType.RecipeRegistryChanged)
                return;

            MarkRecipeCacheDirty();
            EnsureRecipeCache();
        }

        internal static bool TryResolveRecipeForResultItem(ItemData resultItem, out RecipeData recipe)
        {
            if (resultItem != null)
            {
                for (int i = 0; i < _activeFabricators.Count; i++)
                {
                    Fabricator fabricator = _activeFabricators[i];
                    if (fabricator == null)
                        continue;

                    if (TryResolveRecipeForResultItem(fabricator.availableRecipes, resultItem, out recipe))
                        return true;
                }

                int runtimeRecipeCount = ModRecipeRegistry.Count;
                for (int i = 0; i < runtimeRecipeCount; i++)
                {
                    RecipeData runtimeRecipe = ModRecipeRegistry.GetAt(i);
                    if (RecipeProducesItem(runtimeRecipe, resultItem))
                    {
                        recipe = runtimeRecipe;
                        return true;
                    }
                }
            }

            recipe = null;
            return false;
        }

        internal static bool TryResolveRecipeForResultHash(ItemCatalog itemCatalog, int resultHashId, out RecipeData recipe)
        {
            ItemData resultItem = itemCatalog != null && resultHashId != 0
                ? itemCatalog.FindByHash(resultHashId)
                : null;
            return TryResolveRecipeForResultItem(resultItem, out recipe);
        }

        internal static bool TryGetActiveFabricator(string targetName, out Fabricator fabricator)
        {
            bool matchAny = string.IsNullOrWhiteSpace(targetName);
            for (int i = 0; i < _activeFabricators.Count; i++)
            {
                Fabricator candidate = _activeFabricators[i];
                if (candidate == null)
                    continue;

                if (matchAny || candidate.name == targetName)
                {
                    fabricator = candidate;
                    return true;
                }
            }

            fabricator = null;
            return false;
        }

        private static bool TryResolveRecipeForResultItem(List<RecipeData> recipes, ItemData resultItem, out RecipeData recipe)
        {
            if (recipes != null && resultItem != null)
            {
                for (int i = 0; i < recipes.Count; i++)
                {
                    RecipeData candidate = recipes[i];
                    if (RecipeProducesItem(candidate, resultItem))
                    {
                        recipe = candidate;
                        return true;
                    }
                }
            }

            recipe = null;
            return false;
        }

        private static bool RecipeProducesItem(RecipeData recipe, ItemData resultItem)
        {
            if (recipe == null || recipe.resultItem == null || resultItem == null)
                return false;

            if (ReferenceEquals(recipe.resultItem, resultItem))
                return true;

            return recipe.resultItem.PersistentHashId != 0 &&
                   recipe.resultItem.PersistentHashId == resultItem.PersistentHashId;
        }

        private static void RegisterActiveFabricator(Fabricator fabricator)
        {
            if (fabricator == null)
                return;

            for (int i = 0; i < _activeFabricators.Count; i++)
            {
                if (ReferenceEquals(_activeFabricators[i], fabricator))
                    return;
            }

            _activeFabricators.Add(fabricator);
        }

        private static void UnregisterActiveFabricator(Fabricator fabricator)
        {
            if (fabricator == null)
                return;

            for (int i = _activeFabricators.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_activeFabricators[i], fabricator))
                {
                    _activeFabricators.RemoveAt(i);
                    break;
                }
            }
        }

        private static float ResolveCraftPowerMultiplier(Fabricator owner, RecipeData recipe)
        {
            return owner != null
                ? Mathf.Max(1f, owner.GetRecipeInflationMultiplier(recipe))
                : Mathf.Max(1f, ResourceScarcityDirector.ResolveCraftPowerMultiplier(recipe));
        }

        private float ResolveCraftPowerCost(RecipeData recipe)
        {
            return recipe != null && recipe.powerCost > 0f
                ? recipe.powerCost * _activeCraftPowerMultiplier
                : 0f;
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

        private void TryRegister()
        {
            if (_tickRegistered || !Application.isPlaying)
                return;

            _tickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryRegisterLateFrame()
        {
            if (_lateFrameRegistered || !Application.isPlaying)
                return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _tickRegistered = false;
        }

        private void TryUnregisterLateFrame()
        {
            if (!_lateFrameRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _lateFrameRegistered = false;
            _pendingAssemblyVisualCommand = 0;
            _pendingAssemblyBeginRecipe = null;
            _pendingFabricationSparksDirty = false;
            _pendingErrorFeedbackDirty = false;
            _pendingAudioClip = null;
            _pendingAudioDirty = false;
        }

        private void TryRegisterSparkLightTick()
        {
            if (_sparkLightTickRegistered || !Application.isPlaying)
                return;

            _sparkLightTickRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterSparkLightTick()
        {
            if (!_sparkLightTickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _sparkLightTickRegistered = false;
            _sparkLightTickSleeping = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.ResourceScarcityRuntime:
                    _resourceScarcityDirector = currentService as ResourceScarcityDirector;
                    break;
                case GlobalRegistryServiceSlot.PowerGrid:
                    _powerGridService = currentService as IPowerGridService;
                    break;
                case GlobalRegistryServiceSlot.PersistentWorldRegistry:
                    _persistentWorldRegistry = currentService as PersistentWorldRegistry;
                    break;
                case GlobalRegistryServiceSlot.Audio:
                    _audioService = currentService as IAudioService;
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localizationManager = currentService as ILocalizationTextReadModel;
                    RebuildInteractText();
                    break;
                case GlobalRegistryServiceSlot.ScanLogRuntime:
                    CacheScanLogSystem(currentService as IScanLogService);
                    break;
            }
        }

        private void CacheRegistryServicesCold()
        {
            _resourceScarcityDirector = GlobalRegistry.ResourceScarcity;
            _powerGridService = GlobalRegistry.PowerGrid;
            _persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
            _audioService = GlobalRegistry.Audio;
            _localizationManager = Hecton8.Core.GlobalRegistry.LocalizationText;
            CacheScanLogSystem(Hecton8.Core.GlobalRegistry.ScanLogService);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void RebuildInteractText()
        {
            ReadOnlySpan<char> fallbackName = string.IsNullOrWhiteSpace(fabricatorName)
                ? ResolveLocalizedSpan(_uiFabricatorLocalizationHash, "FABRICATOR".AsSpan())
                : fabricatorName.AsSpan();
            ReadOnlySpan<char> pattern = ResolveLocalizedSpan(_interactUseFabricatorLocalizationHash, "Use {0}".AsSpan());

            _interactTextLength = WriteInteractTemplate(pattern, fallbackName, _interactTextBuffer);
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildInteractText();
        }

        private ReadOnlySpan<char> ResolveLocalizedSpan(int keyHash, ReadOnlySpan<char> fallback)
        {
            ILocalizationTextReadModel manager = _localizationManager;
            if (manager == null)
                return fallback;

            ReadOnlySpan<char> localized = manager.GetRawSpanOrFallback(keyHash, fallback);
            return localized.IsEmpty ? fallback : localized;
        }

        private static int WriteInteractTemplate(ReadOnlySpan<char> template, ReadOnlySpan<char> value, char[] destination)
        {
            if (destination == null || destination.Length == 0)
                return 0;

            int cursor = 0;
            int placeholderIndex = template.IndexOf("{0}".AsSpan());
            if (placeholderIndex < 0)
            {
                cursor = AppendSpan(template, destination, cursor);
                if (cursor < destination.Length)
                    destination[cursor++] = ' ';
                return AppendSpan(value, destination, cursor);
            }

            cursor = AppendSpan(template.Slice(0, placeholderIndex), destination, cursor);
            cursor = AppendSpan(value, destination, cursor);
            return AppendSpan(template.Slice(placeholderIndex + 3), destination, cursor);
        }

        private static int AppendSpan(ReadOnlySpan<char> source, char[] destination, int cursor)
        {
            if (destination == null || destination.Length == 0)
                return 0;

            if (cursor >= destination.Length || source.IsEmpty)
                return Mathf.Clamp(cursor, 0, destination.Length);

            int writable = Mathf.Min(source.Length, destination.Length - cursor);
            source.Slice(0, writable).CopyTo(destination.AsSpan(cursor));
            return cursor + writable;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (maxUseDistance < 1f) maxUseDistance = 1f;
            if (craftPowerDraw < 0f) craftPowerDraw = 0f;
            if (string.IsNullOrEmpty(fabricatorName)) fabricatorName = "Fabrikator";

            RebuildInteractText();
            MarkRecipeCacheDirty();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, maxUseDistance);
        }
#endif
    }
}

