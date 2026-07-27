// ============================================================================
// HECTON-8 — Fabricator.cs
// Mashina-verstak dlya krafta predmetov.
//
// REFAKTORING v3 — DINAMIChESKOE PITANIE:
//   • Realizuet IPowerComponent dlya integratsii s PowerGrid.
//   • Pri otsutstvii pitaniya kraft vstaet na PAUZU (ne otmenyaetsya).
//   • PowerRating: 0 v idle, -craftPowerDraw pri krafte.
//   • Pri vosstanovlenii pitaniya kraft prodolzhaetsya s togo zhe mesta.
//   • Pri StartCraft/CompleteCraft/CancelCraft ? PowerGrid.UpdateBalance()
//     dlya mgnovennogo perescheta balansa seti.
//
// ZhIZNENNYY TsIKL KRAFTA:
//   1. Igrok navoditsya ? OnHoverStart ? HUD pokazyvaet prompt
//   2. [E] ? Interact ? CraftingEvents.TryRaiseFabricatorOpened
//   3. UI vyzyvaet StartCraft(recipe) ? CanCraft proverka
//   4. Resursy spisyvayutsya SRAZU -> Vault FabricationJobDTO zapuskaetsya
//      ? NotifyGridBalanceChanged() — set pereschityvaet s -100W
//   5. SIMULATION: esli HasPower -> Vault Progress01 prodvigaetsya Burst job
//               esli !HasPower -> PAUZA (Vault Progress01 ne tikaet)
//   6. Zavershenie ? rezultat v inventar ? OnCraftCompleted
//      ? NotifyGridBalanceChanged() — set pereschityvaet bez -100W
//   7. Otmena ? resursy vozvraschayutsya ? OnCraftCancelled
//      ? NotifyGridBalanceChanged() — set pereschityvaet bez -100W
//
// ZERO GC:
//   • Tick: float arifmetika, delegate?.Invoke (no boxing)
//   • CanCraft: for-tsikly s ReferenceEquals, no LINQ
//   • IPowerComponent svoystva: value types only
//   • PowerNode keshirovan v Awake — zero TryGetComponent v goryachem puti
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Audio;
using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Core.Memory;
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
    public sealed partial class Fabricator : MonoBehaviour, IInteractable, IInteractableTextProvider, ISlowTickable, IUpdatable, ILateFrameTickable, IPowerComponent, IFabricator, ILocalizationLanguageChangedListener, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private const int ActiveFabricatorRegistryCapacity = 512;
        private static int s_x001FabricatorSignalPushDropCount;
        private static int s_activeFabricatorRegistryOverflowCount;
        // COLD ALLOC: List<Fabricator>[512] - active fabricator registry for cold-path recipe lookups - owner: Fabricator
        private static readonly List<Fabricator> _activeFabricators = new List<Fabricator>(ActiveFabricatorRegistryCapacity);
        private static readonly int _uiFabricatorLocalizationHash = LocHash.Compute(LocalizationKeys.UI_FABRICATOR);
        private static readonly int _interactUseFabricatorLocalizationHash = LocHash.Compute(LocalizationKeys.INTERACT_USE_FABRICATOR);
        private static bool s_emergencyPowerLockActive;
        private const int InteractTextBufferCapacity = 96;
        private const string LegacyInteractText = "FABRICATOR";
        private const float ExothermicRunningHeatDeltaCelsius = 20f;
        private ModRegistryEventAdapter _modRegistryEventAdapter;
        private bool _activeFabricatorRegistered;
        private bool _activeFabricatorRegistryOverflowed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_x001FabricatorSignalPushDropCount = 0;
            s_activeFabricatorRegistryOverflowCount = 0;
            _activeFabricators.Clear();
            s_emergencyPowerLockActive = false;
        }

        // ----------------------------------------------------------
        //  INSPECTOR
        // ----------------------------------------------------------

        [Header("-- Identity ----------------------------------")]
        [Tooltip("Nazvanie fabrikatora dlya UI prompta")]
        [SerializeField] private string fabricatorName = "Fabrikator";

        [Header("-- Recipes -----------------------------------")]
        [Tooltip("Spisok dostupnyh retseptov na etom verstake")]
        [SerializeField] private List<RecipeData> availableRecipes = new List<RecipeData>();

        [Header("-- Settings ----------------------------------")]
        [Tooltip("Maksimalnaya distantsiya ispolzovaniya (metry). " +
                 "Esli igrok otoydet dalshe — kraft otmenyaetsya.")]
        [SerializeField] private float maxUseDistance = 3.5f;

        [Tooltip("When enabled, a completed recipe immediately queues again if unlocks, ingredients, capacity, and power still pass.")]
        [SerializeField] private bool isContinuous;

        [Header("-- Power -------------------------------------")]
        [Tooltip("Potreblenie energii VO VREMYa KRAFTA (Vatty). " +
                 "V idle fabrikator ne potreblyaet dopolnitelno. " +
                 "Bazovoe potreblenie modulya beretsya iz BuildableData cherez PowerNode.")]
        [SerializeField] private float craftPowerDraw = 100f;

        [Tooltip("Prioritet otklyucheniya pri defitsite. " +
                 "0 = kriticheskiy (ne otklyuchat), 100 = roskosh (otklyuchit pervym).")]
        [Range(0, 100)]
        [SerializeField] private int powerPriority = 50;

        [Header("-- Audio (optional) --------------------------")]
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
        [Tooltip("Required authored fallback mesh for craftable items without a world prefab. Runtime mesh generation is forbidden.")]
        [SerializeField] private Mesh assemblyFallbackMesh;
        [SerializeField, Min(0f)] private float assemblyHeightPadding = 0.02f;
        [SerializeField] private Color assemblyBaseColor = new Color(0.05f, 0.86f, 1f, 0.72f);
        [SerializeField] private Color assemblyPausedColor = new Color(1f, 0.04f, 0.02f, 0.86f);

        [Header("-- Physical Output --------------------------")]
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

        [Header("-- Deconstruction Output ------------------------")]
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

        // ----------------------------------------------------------
        //  CACHED STATE
        // ----------------------------------------------------------

        // COLD ALLOC: char[96] - cached IInteractable prompt staging buffer - owner: Fabricator
        private readonly char[] _interactTextBuffer = new char[InteractTextBufferCapacity];
        private int _interactTextLength;

        /// <summary>Ssylka na inventar tekuschego igroka.</summary>
        private PlayerInventory _playerInventory;

        /// <summary>Transform igroka dlya proverki distantsii.</summary>
        private Transform _playerTransform;
        private HectonPlayerMovement _playerMovement;
        private IPlayerRuntimeContext _cachedPlayerContext;
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
        private IResourceScarcityReadModel _resourceScarcityDirector;
        private IPowerGridService _powerGridService;
        private PersistentWorldRegistry _persistentWorldRegistry;
        private IAudioService _audioService;
        private ILocalizationTextReadModel _localizationManager;
        private uint _observedScanLogRevision;
        private readonly List<RecipeData> _visibleRecipes = new List<RecipeData>(MaxRecipeCacheEntries);
        private ItemData[] _assemblySourceItems;
        private Mesh[] _assemblySourceMeshes;
        private Material[] _assemblySourceMaterials;
        private int _assemblySourceCount;
        private bool _recipeCacheDirty = true;
        private bool _tickRegistered;
        private bool _lateFrameRegistered;
        private bool _hotSwapListenerRegistered;
        private int _lockedRecipeCount;
        private int _overflowRecipeCount;
        private float _activeCraftPowerMultiplier = 1f;
        private int _activeCraftMultiplier = 1;
        private MaterialPropertyBlock _errorFeedbackBlock;
        private float _errorFlashRemainingSeconds;
        private bool _fabricationSparksPlaying;
        private bool _errorFeedbackApplied;
        private int _sparkProxyLightKey;
        private float _sparkProxyLightRemainingSeconds;
        private bool _sparkProxyLightRegistered;
        private bool _sparkProxyLightDirty;
        private bool _sparkProxyLightUnregisterPending;
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
        private int _pendingProceduralAudioPingCount;
        private ProceduralAudioPingRequest _pendingProceduralAudioPing0;
        private ProceduralAudioPingRequest _pendingProceduralAudioPing1;
        private bool _pendingProgressHapticDirty;
        private FabricatorHapticRequest _pendingProgressHaptic;

        // -- Craft State --
        private bool       _isCrafting;
        private bool       _runningExothermicHeatInjected;
        private RecipeData _activeRecipe;
        private float      _craftProgressSecondsMirror;
        private float      _lastPublishedProgress;
        private RecipeData _pendingCraftOutputRecipe;
        private ItemData   _pendingCraftOutputItem;
        private int        _pendingCraftOutputQuantity;
        private int        _pendingCraftOutputTotalQuantity;

        // -- Power State --
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
        internal const int MaxRecipeCacheEntries = MaxUnlockedRecipeWords * 64;
        private const int CraftInventoryScratchCapacity = 128;
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
        private const int FabricatorMemoryTelemetryRingCapacity = 300;
        private const int FabricatorMemoryTelemetryEntrySizeBytes = 64;
        private const int FabricatorVaultFailureDumpThreshold = 3;
        private const uint FabricatorMemoryTelemetryMagic = 0x46313332u; // F132
        private const string FabricatorMemoryDumpPath = "Docs/AgentLogs/Dump_1329_Fabricator.bin";
        private const uint FabricatorVaultFailureEnsure = 1u << 0;
        private const uint FabricatorVaultFailureAcquire = 1u << 1;
        private static readonly ulong FabricatorUnlockedRecipesMutationGuardMask =
            FabricatorMutationGuardBit(BufferID.ShinobuFabricatorUnlockedRecipes);

        [StructLayout(LayoutKind.Explicit, Size = FabricatorMemoryTelemetryEntrySizeBytes)]
        public struct FabricatorMemoryTelemetryEntry
        {
            [FieldOffset(0)] public ulong Sequence;
            [FieldOffset(8)] public ulong StateHash;
            [FieldOffset(16)] public uint Frame;
            [FieldOffset(20)] public uint BufferId;
            [FieldOffset(24)] public uint HandleGeneration;
            [FieldOffset(28)] public uint VaultGeneration;
            [FieldOffset(32)] public uint Flags;
            [FieldOffset(36)] public int Capacity;
            [FieldOffset(40)] public int FailureStreak;
            [FieldOffset(44)] public float GlobalQualityWeight;
            [FieldOffset(48)] public float CpuMicroseconds;
            [FieldOffset(52)] public float GpuMicroseconds;
            [FieldOffset(56)] public uint SystemId;
            [FieldOffset(60)] private byte _pad0;
            [FieldOffset(61)] private byte _pad1;
            [FieldOffset(62)] private byte _pad2;
            [FieldOffset(63)] private byte _pad3;
        }

        private struct ProceduralAudioPingRequest
        {
            public Vector3 Position;
            public float Intensity01;
            public float DurationSeconds;
            public float Transmission01;
            public float PitchCarrierHz;
            public ProceduralAudioPingKind Kind;
        }

        private struct FabricatorHapticRequest
        {
            public float LowFrequencyIntensity;
            public float HighFrequencyIntensity;
            public float DurationSeconds;
            public float PulseFrequencyHz;
            public byte Priority;
            public byte MotorMask;
        }

        private readonly PlayerInventory.CraftReservation[] _localCraftReservations = new PlayerInventory.CraftReservation[MaxLocalCraftReservations];
        private readonly int[] _networkCostItemHashes = new int[MaxNetworkCraftCosts];
        private readonly int[] _networkCostAmounts = new int[MaxNetworkCraftCosts];
        private int _localCraftReservationCount;
        private PlayerInventory _craftReservationOwner;
        private int _networkCostCount;
        private IDataVault _dataVault;
        private int2[] _craftInventoryCountsScratch;
        private int2[] _craftRecipeCostsScratch;
        private byte[] _craftRecipeEvaluationResultScratch;
        private int2[] _deconstructionRecipeOutputsScratch;
        private int[] _deconstructionOutputCountScratch;
        private CraftingTask _activeCraftingTask;
        private bool _hasActiveCraftingTask;
        private int2[] _complexRecipeGraphNodesScratch;
        private int2[] _complexRecipeGraphEdgesScratch;
        private int[] _complexRecipeGraphInDegreesScratch;
        private int[] _complexRecipeGraphQueueScratch;
        private int2[] _complexRecipeRawCostsScratch;
        private int[] _complexRecipeRawCostCountScratch;
        private byte[] _complexRecipeGraphStatusScratch;
        private VaultGenerationHandle<ulong> _unlockedRecipesHandle;
        private VaultGenerationHandle<FabricatorMemoryTelemetryEntry> _fabricatorMemoryTelemetryRingHandle;
        private int _fabricatorMemoryTelemetryCursor;
        private int _fabricatorVaultFailureStreak;
        private bool _fabricatorBlackBoxDumped;
        private bool _unlockMaskDirty = true;

        private BaseLogisticsNetwork.LogisticsReservation _networkReservation;

        /// <summary>Porog publikatsii progressa.</summary>
        private const float ProgressPublishThreshold = 0.01f;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        // ----------------------------------------------------------
        //  PUBLIC API — QUERIES
        // ----------------------------------------------------------

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

        private bool HasPendingCraftOutput => _pendingCraftOutputItem != null && _pendingCraftOutputQuantity > 0;

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
                return _visibleRecipes;
            }
        }

        public int TotalRecipeCount
        {
            get
            {
                if (!Application.isPlaying)
                    return CountAuthoredRecipeReferencesCold();

                return _visibleRecipes.Count + _lockedRecipeCount + _overflowRecipeCount;
            }
        }
        public int LockedRecipeCount
        {
            get
            {
                return _lockedRecipeCount + _overflowRecipeCount;
            }
        }

        /// <summary>Kraft na pauze iz-za otsutstviya pitaniya.</summary>
        public bool IsPausedNoPower => _isCrafting && !HasOperationalPower;

        internal PowerGrid CurrentPowerGrid => _powerNode != null ? _powerNode.Grid : null;

        // ----------------------------------------------------------
        //  IPowerComponent — ENERGOSISTEMA
        // ----------------------------------------------------------

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

        // ----------------------------------------------------------
        //  LIFECYCLE
        // ----------------------------------------------------------

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
            UnityEngine.Assertions.Assert.IsNotNull(
                assemblyFallbackMesh,
                "Fatal: Fabricator requires an authored assembly fallback mesh. Runtime mesh generation is forbidden.");
            FlushEndAssemblyVisual();
            ToolHapticsRuntime.EnsureRuntimeInstance();
            CacheThermalHostModule();
            EnsureCraftingScratchCold();
            RebuildAssemblySourceCacheCold();
            EnsureRecipeCache();
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
            ModRegistryEvents.Register(GetModRegistryEventAdapter());
            RebuildInteractText();
            TryRegister();
            TryRegisterLateFrame();
            TryRegisterSparkLightTick();
            _sparkLightTickSleeping = true;
            CacheThermalHostModule();
            MarkRecipeCacheDirty();
            EnsureCraftingScratchCold();
            RebuildAssemblySourceCacheCold();
            EnsureRecipeCache();
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
            if (_modRegistryEventAdapter != null)
                ModRegistryEvents.Unregister(_modRegistryEventAdapter);
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
            ClearCachedAudioService();
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
            ClearCachedAudioService();
            DisposeCraftingScratch();
            PublishFabricatorActiveCountBlackBox();
        }

        // ----------------------------------------------------------
        //  IInteractable
        // ----------------------------------------------------------

        void IInteractable.Interact(Transform interactor)
        {
            _playerTransform = interactor;
            _playerMovement = null;
            _playerMovementLookupAttempted = false;

            if (_playerInventory == null && interactor != null)
                interactor.TryGetComponent(out _playerInventory);
            TryCachePlayerMovement(interactor);
            EnsureRecipeCache();

            CraftingEvents.TryRaiseFabricatorOpened(this);
            InteractionEvents.TryRaiseInteractionStarted(this, interactor);
        }

        string IInteractable.GetInteractText()
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
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!IsFiniteRuntimePosition(shiftOffset) || !math.isfinite(shiftSqrMagnitude) || shiftSqrMagnitude <= 0.000001f)
                return;

            _fabricatorAupCached = false;
            CacheFabricatorAup();
            if (_assemblyPreviewActive && !_assemblyMaterialSwapped)
            {
                ApplyAssemblyVisualProgress(_assemblyProgress01, IsPausedNoPower);
            }
        }

        // ----------------------------------------------------------
        //  PUBLIC API — CRAFTING
        // ----------------------------------------------------------

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
            if (HasPendingCraftOutput) return false;
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

        internal int CalculateAdjustedIngredientAmount(InventoryCost cost)
        {
            if (cost == null || cost.item == null || cost.amount <= 0)
                return 0;

            int itemHashId = ComputeItemHash(cost.item);
            IResourceScarcityReadModel scarcityDirector = _resourceScarcityDirector;
            CacheFabricatorAup();
            return scarcityDirector != null
                ? scarcityDirector.ResolveInflatedIngredientAmount(itemHashId, cost.amount, in _fabricatorAup, CountAccessibleItem(cost.item))
                : cost.amount;
        }

        internal float CalculateRecipeInflationMultiplier(RecipeData recipe)
        {
            if (recipe == null || recipe.ingredients == null || recipe.ingredients.Count <= 0)
                return 1f;

            float maxMultiplier = 1f;
            for (int i = 0; i < recipe.ingredients.Count; i++)
            {
                InventoryCost cost = recipe.ingredients[i];
                if (cost == null || cost.item == null || cost.amount <= 0)
                    continue;

                int adjustedAmount = CalculateAdjustedIngredientAmount(cost);
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
        /// Posle smeny _isCrafting ? PowerRating menyaetsya s 0 na -craftPowerDraw.
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

            // -- Uvedomlyaem energoset: PowerRating izmenilsya (0 ? -craftPowerDraw) --
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
        /// Posle smeny _isCrafting ? PowerRating menyaetsya s -craftPowerDraw na 0.
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

            // -- Uvedomlyaem energoset: PowerRating izmenilsya (-craftPowerDraw ? 0) --
            NotifyGridBalanceChanged();
            PublishFabricatorActiveCountBlackBox();

            CraftingEvents.TryRaiseCraftCancelled();
            CraftingEvents.TryRaiseCraftProgressUpdated(0f);

            PlaySound(craftCancelSound);
        }

        // ----------------------------------------------------------
        //  ITickable — TAYMER KRAFTA
        // ----------------------------------------------------------

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
                QueueUnregisterSparkProxyLight();
                _sparkLightTickSleeping = true;
                return;
            }

            _sparkProxyLightRemainingSeconds = Mathf.Max(0f, _sparkProxyLightRemainingSeconds - Mathf.Max(0f, deltaTime));
            if (_sparkProxyLightRemainingSeconds > 0f)
            {
                _sparkProxyLightDirty = true;
                return;
            }

            QueueUnregisterSparkProxyLight();
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
            FlushPendingProceduralAudioPings();
            FlushPendingProgressHaptics();
            FlushSparkProxyLightRegistration();
        }

        public void SlowTick()
        {
            RefreshScanLogRevision();
            UpdateErrorFeedback(SlowTickDeltaSeconds);
            TryFlushPendingCraftOutput();

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

            // -- Proverka distantsii (vsegda, dazhe bez pitaniya) --
            if (!IsPlayerInRange())
            {
                CancelCraft();
                return;
            }

            // ---------------------------------------------------
            //  POWER PAUSE: net pitaniya -> Vault Progress01 zamorozhen
            // ---------------------------------------------------
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
                QueueFabricatorProgressHaptics(progress);
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

            // The job belongs to the craft, not to the hologram. Retry while a recipe is active so
            // a fabricator that lost its slot (vault not ready, all assembler slots busy) recovers
            // instead of holding reserved ingredients at zero progress forever.
            if (_fabricationJobSlot < 0 && _activeRecipe != null)
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

        // ----------------------------------------------------------
        //  PRIVATE — CRAFT COMPLETION
        // ----------------------------------------------------------

        /// <summary>
        /// Zavershaet kraft: vydaet rezultat v inventar.
        /// Posle smeny _isCrafting ? PowerRating menyaetsya s -craftPowerDraw na 0.
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
                RefundIngredients();

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

            if (result == null || outputQuantity <= 0)
            {
                RefundIngredients();
                NotifyGridBalanceChanged();
                PublishFabricatorActiveCountBlackBox();
                EndAssemblyVisual();
                TriggerCraftFailureFeedback();
                return;
            }

            PlayerInventory reservationOwner = _craftReservationOwner != null ? _craftReservationOwner : _playerInventory;
            if (_localCraftReservationCount > 0 && reservationOwner == null)
            {
                RefundIngredients();
                NotifyGridBalanceChanged();
                PublishFabricatorActiveCountBlackBox();
                EndAssemblyVisual();
                TriggerCraftFailureFeedback();
                return;
            }

            if (reservationOwner != null && !reservationOwner.CommitCraftReservations(_localCraftReservations, _localCraftReservationCount))
            {
                _localCraftReservationCount = 0;
                _craftReservationOwner = null;
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
            _craftReservationOwner = null;

            if (_networkReservation != null)
            {
                BaseLogisticsNetwork.CommitReserved(_networkReservation);
                _networkReservation = null;
                _networkCostCount = 0;
            }

            // -- Uvedomlyaem energoset: PowerRating izmenilsya (-craftPowerDraw ? 0) --
            NotifyGridBalanceChanged();

            // -- Potreblyaem energiyu iz seti pri zavershenii krafta --
            if (powerCost > 0f && _powerNode != null && _powerNode.Grid != null)
            {
                _powerNode.Grid.ConsumePower(powerCost);
            }

            ApplyCraftingThermodynamics(craftTemperatureDelta);
            CompleteAssemblyVisual();

            int deliveredQuantity = TryDeliverCraftOutput(recipe, result, outputQuantity, out bool outputDeliveryFault);

            if (result != null && deliveredQuantity > 0)
                PublishCraftItemAcquiredSignal(result, deliveredQuantity);

            bool craftOutputFullyDelivered = result != null &&
                                             outputQuantity > 0 &&
                                             deliveredQuantity == outputQuantity;
            bool craftOutputTruthPreserved = craftOutputFullyDelivered;
            if (!craftOutputFullyDelivered && result != null && outputQuantity > 0)
            {
                int pendingQuantity = outputQuantity - Mathf.Max(0, deliveredQuantity);
                craftOutputTruthPreserved = TryStorePendingCraftOutput(recipe, result, pendingQuantity, outputQuantity);
            }

            CraftingEvents.TryRaiseCraftProgressUpdated(craftOutputTruthPreserved ? 1f : 0f);

            if (!craftOutputTruthPreserved)
            {
                if (!outputDeliveryFault)
                    TriggerCraftFailureFeedback();

                PublishFabricatorActiveCountBlackBox();
                return;
            }

            if (!craftOutputFullyDelivered)
            {
                if (!outputDeliveryFault)
                    TriggerCraftFailureFeedback();

                PublishFabricatorActiveCountBlackBox();
                return;
            }

            PublishCraftingCompletedSignal(recipe, result, deliveredQuantity);
            CraftingEvents.TryRaiseCraftCompleted(result);

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

        // ----------------------------------------------------------
        //  PRIVATE — POWER GRID NOTIFICATION
        // ----------------------------------------------------------

        /// <summary>
        /// Uvedomlyaet PowerGrid o neobhodimosti perescheta balansa.
        ///
        /// Vyzyvaetsya pri kazhdom izmenenii PowerRating:
        ///   • StartCraft:    0 ? -craftPowerDraw (nachalo potrebleniya)
        ///   • CompleteCraft: -craftPowerDraw ? 0 (konets potrebleniya)
        ///   • CancelCraft:   -craftPowerDraw ? 0 (otmena potrebleniya)
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

        // ----------------------------------------------------------
        //  PRIVATE — INGREDIENT MANAGEMENT
        // ----------------------------------------------------------

        private void CacheThermalHostModule()
        {
            if (thermalHostModule != null)
                return;

            thermalHostModule = ComponentReferenceUtility.ResolveParentService<BaseModule>(this);
            _thermalHostAupCached = false;
            _thermalHostAupSource = null;
        }

        private bool PassesBiomeLock(RecipeData recipe)
        {
            if (recipe == null || !recipe.RequiresAnchoredBiomeLock)
                return true;

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

        internal bool CanUseRecipeAsRawCostExpansion(RecipeData recipe)
        {
            return recipe != null &&
                   IsRecipeUnlocked(recipe) &&
                   PassesBiomeLock(recipe);
        }

        internal bool TryResolveOwnedRecipeForResultHash(ItemCatalog itemCatalog, int resultHashId, out RecipeData recipe)
        {
            ItemData resultItem = itemCatalog != null && resultHashId != 0
                ? itemCatalog.FindByHash(resultHashId)
                : null;
            return TryResolveOwnedRecipeForResultItem(resultItem, out recipe);
        }

        private bool TryResolveOwnedRecipeForResultItem(ItemData resultItem, out RecipeData recipe)
        {
            recipe = null;
            if (resultItem == null)
                return false;

            int unlockIndex = 0;
            if (availableRecipes != null)
            {
                for (int i = 0; i < availableRecipes.Count; i++)
                {
                    if (!IsUnlockIndexInRange(unlockIndex))
                        return false;

                    RecipeData candidate = availableRecipes[i];
                    unlockIndex++;
                    if (RecipeProducesItem(candidate, resultItem))
                    {
                        recipe = candidate;
                        return true;
                    }
                }
            }

            int runtimeRecipeCount = ModRecipeRegistry.Count;
            for (int i = 0; i < runtimeRecipeCount; i++)
            {
                RecipeData runtimeRecipe = ModRecipeRegistry.GetAt(i);
                if (runtimeRecipe == null || ContainsAuthoredRecipeReference(runtimeRecipe))
                    continue;

                if (!IsUnlockIndexInRange(unlockIndex))
                    return false;

                unlockIndex++;
                if (RecipeProducesItem(runtimeRecipe, resultItem))
                {
                    recipe = runtimeRecipe;
                    return true;
                }
            }

            return false;
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
                return;

            thermalHostModule.TryInjectHostRoomTemperatureDeltaCelsius(deltaCelsius);
        }

        private bool HasIngredients(RecipeData recipe, int multiplier = 1)
        {
            if (recipe == null || _playerInventory == null)
                return false;

            if (!HasCraftingScratchReady())
                return false;

            return CraftingSystem.CanCraft(
                recipe,
                this,
                _playerInventory,
                _craftInventoryCountsScratch,
                _craftRecipeCostsScratch,
                _craftRecipeEvaluationResultScratch,
                _complexRecipeGraphNodesScratch,
                _complexRecipeGraphEdgesScratch,
                _complexRecipeGraphInDegreesScratch,
                _complexRecipeGraphQueueScratch,
                _complexRecipeRawCostsScratch,
                _complexRecipeRawCostCountScratch,
                _complexRecipeGraphStatusScratch,
                Mathf.Max(1, multiplier));
        }

        private bool EnsureCraftingScratchCold()
        {
            if (!EnsureManagedCraftingScratchCold())
                return false;

            if (!TryAcquireDataVaultCold())
                return false;

            if (!TryEnsureFabricatorVaultBuffer(ref _unlockedRecipesHandle, BufferID.ShinobuFabricatorUnlockedRecipes, MaxUnlockedRecipeWords, NativeArrayOptions.ClearMemory))
            {
                // COLD VAULT: ulong[8] recipe unlock bitset for fabricator craft gate.
                return false;
            }

            if (!TryEnsureFabricatorVaultBuffer(ref _fabricatorMemoryTelemetryRingHandle, BufferID.ShinobuFabricatorMemoryTelemetryRing, FabricatorMemoryTelemetryRingCapacity, NativeArrayOptions.ClearMemory))
                return false;

            _fabricatorVaultFailureStreak = 0;
            return true;
        }

        private bool EnsureManagedCraftingScratchCold()
        {
            if (_craftInventoryCountsScratch == null || _craftInventoryCountsScratch.Length < CraftInventoryScratchCapacity)
            {
                // COLD ALLOC: int2[128] — fabricator inventory-count scratch — owner: Fabricator
                _craftInventoryCountsScratch = new int2[CraftInventoryScratchCapacity];
            }

            if (_craftRecipeCostsScratch == null || _craftRecipeCostsScratch.Length < CraftingSystem.MaxRecipeIngredientCount)
            {
                // COLD ALLOC: int2[32] — fabricator direct recipe-cost scratch — owner: Fabricator
                _craftRecipeCostsScratch = new int2[CraftingSystem.MaxRecipeIngredientCount];
            }

            if (_craftRecipeEvaluationResultScratch == null || _craftRecipeEvaluationResultScratch.Length < 1)
            {
                // COLD ALLOC: byte[1] — fabricator recipe availability result scratch — owner: Fabricator
                _craftRecipeEvaluationResultScratch = new byte[1];
            }

            if (_deconstructionRecipeOutputsScratch == null || _deconstructionRecipeOutputsScratch.Length < CraftingSystem.MaxDeconstructionOutputCount)
            {
                // COLD ALLOC: int2[32] — fabricator deconstruction output scratch — owner: Fabricator
                _deconstructionRecipeOutputsScratch = new int2[CraftingSystem.MaxDeconstructionOutputCount];
            }

            if (_deconstructionOutputCountScratch == null || _deconstructionOutputCountScratch.Length < 1)
            {
                // COLD ALLOC: int[1] — fabricator deconstruction output count scratch — owner: Fabricator
                _deconstructionOutputCountScratch = new int[1];
            }

            if (_complexRecipeGraphNodesScratch == null || _complexRecipeGraphNodesScratch.Length < CraftingSystem.MaxComplexRecipeNodeCount)
            {
                // COLD ALLOC: int2[64] — fabricator complex recipe graph nodes — owner: Fabricator
                _complexRecipeGraphNodesScratch = new int2[CraftingSystem.MaxComplexRecipeNodeCount];
            }

            if (_complexRecipeGraphEdgesScratch == null || _complexRecipeGraphEdgesScratch.Length < CraftingSystem.MaxComplexRecipeEdgeCount)
            {
                // COLD ALLOC: int2[128] — fabricator complex recipe graph edges — owner: Fabricator
                _complexRecipeGraphEdgesScratch = new int2[CraftingSystem.MaxComplexRecipeEdgeCount];
            }

            if (_complexRecipeGraphInDegreesScratch == null || _complexRecipeGraphInDegreesScratch.Length < CraftingSystem.MaxComplexRecipeNodeCount)
            {
                // COLD ALLOC: int[64] — fabricator complex recipe graph in-degrees — owner: Fabricator
                _complexRecipeGraphInDegreesScratch = new int[CraftingSystem.MaxComplexRecipeNodeCount];
            }

            if (_complexRecipeGraphQueueScratch == null || _complexRecipeGraphQueueScratch.Length < CraftingSystem.MaxComplexRecipeNodeCount)
            {
                // COLD ALLOC: int[64] — fabricator complex recipe graph queue — owner: Fabricator
                _complexRecipeGraphQueueScratch = new int[CraftingSystem.MaxComplexRecipeNodeCount];
            }

            if (_complexRecipeRawCostsScratch == null || _complexRecipeRawCostsScratch.Length < CraftingSystem.MaxRecipeIngredientCount)
            {
                // COLD ALLOC: int2[32] — fabricator expanded raw-cost scratch — owner: Fabricator
                _complexRecipeRawCostsScratch = new int2[CraftingSystem.MaxRecipeIngredientCount];
            }

            if (_complexRecipeRawCostCountScratch == null || _complexRecipeRawCostCountScratch.Length < 1)
            {
                // COLD ALLOC: int[1] — fabricator expanded raw-cost count scratch — owner: Fabricator
                _complexRecipeRawCostCountScratch = new int[1];
            }

            if (_complexRecipeGraphStatusScratch == null || _complexRecipeGraphStatusScratch.Length < 1)
            {
                // COLD ALLOC: byte[1] — fabricator complex recipe graph status scratch — owner: Fabricator
                _complexRecipeGraphStatusScratch = new byte[1];
            }

            return HasCraftingScratchReady();
        }

        private bool HasCraftingScratchReady()
        {
            return _craftInventoryCountsScratch != null &&
                   _craftInventoryCountsScratch.Length >= CraftInventoryScratchCapacity &&
                   _craftRecipeCostsScratch != null &&
                   _craftRecipeCostsScratch.Length >= CraftingSystem.MaxRecipeIngredientCount &&
                   _craftRecipeEvaluationResultScratch != null &&
                   _craftRecipeEvaluationResultScratch.Length >= 1 &&
                   _deconstructionRecipeOutputsScratch != null &&
                   _deconstructionRecipeOutputsScratch.Length >= CraftingSystem.MaxDeconstructionOutputCount &&
                   _deconstructionOutputCountScratch != null &&
                   _deconstructionOutputCountScratch.Length >= 1 &&
                   _complexRecipeGraphNodesScratch != null &&
                   _complexRecipeGraphNodesScratch.Length >= CraftingSystem.MaxComplexRecipeNodeCount &&
                   _complexRecipeGraphEdgesScratch != null &&
                   _complexRecipeGraphEdgesScratch.Length >= CraftingSystem.MaxComplexRecipeEdgeCount &&
                   _complexRecipeGraphInDegreesScratch != null &&
                   _complexRecipeGraphInDegreesScratch.Length >= CraftingSystem.MaxComplexRecipeNodeCount &&
                   _complexRecipeGraphQueueScratch != null &&
                   _complexRecipeGraphQueueScratch.Length >= CraftingSystem.MaxComplexRecipeNodeCount &&
                   _complexRecipeRawCostsScratch != null &&
                   _complexRecipeRawCostsScratch.Length >= CraftingSystem.MaxRecipeIngredientCount &&
                   _complexRecipeRawCostCountScratch != null &&
                   _complexRecipeRawCostCountScratch.Length >= 1 &&
                   _complexRecipeGraphStatusScratch != null &&
                   _complexRecipeGraphStatusScratch.Length >= 1;
        }

        private void DisposeCraftingScratch()
        {
            RefundIngredients();
            ClearManagedCraftingScratch();
            ClearCraftingScratchHandles();
        }

        private void ClearCraftingScratchHandles()
        {
            _unlockedRecipesHandle = default;
            _fabricatorMemoryTelemetryRingHandle = default;
            _fabricatorMemoryTelemetryCursor = 0;
            _fabricatorVaultFailureStreak = 0;
            _fabricatorBlackBoxDumped = false;
        }

        private void ClearManagedCraftingScratch()
        {
            _craftInventoryCountsScratch = null;
            _craftRecipeCostsScratch = null;
            _craftRecipeEvaluationResultScratch = null;
            _deconstructionRecipeOutputsScratch = null;
            _deconstructionOutputCountScratch = null;
            _complexRecipeGraphNodesScratch = null;
            _complexRecipeGraphEdgesScratch = null;
            _complexRecipeGraphInDegreesScratch = null;
            _complexRecipeGraphQueueScratch = null;
            _complexRecipeRawCostsScratch = null;
            _complexRecipeRawCostCountScratch = null;
            _complexRecipeGraphStatusScratch = null;
        }

        private bool TryAcquireDataVaultCold()
        {
            return _dataVault != null;
        }

        private bool TryEnsureFabricatorVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault == null || requiredLength <= 0)
                return false;

            if (handle.BufferID != 0u &&
                handle.Generation != 0u &&
                vault.TryResolveHandle(in handle, out NativeArray<T> existing) &&
                existing.IsCreated &&
                existing.Length >= requiredLength)
            {
                return true;
            }

            if (vault.IsAllocationLocked)
            {
                if (!vault.TryGetGenerationHandle(bufferId, out handle))
                    return false;
            }
            else
            {
                handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, SystemID.Crafting, options);
            }

            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)SystemID.Crafting &&
                   handle.Generation != 0u &&
                   vault.TryResolveHandle(in handle, out NativeArray<T> buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private bool IsFabricatorVaultBufferReady<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out IDataVault vault) where T : struct
        {
            vault = _dataVault;
            return vault != null &&
                   requiredLength > 0 &&
                   handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)SystemID.Crafting &&
                   handle.Generation != 0u &&
                   vault.TryResolveHandle(in handle, out NativeArray<T> buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private bool TryAcquireFabricatorWrite<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer,
            out IDataVault lockedVault) where T : struct
        {
            buffer = default;
            lockedVault = null;
            if (!IsFabricatorVaultBufferReady(in handle, bufferId, requiredLength, out IDataVault vault))
            {
                RecordFabricatorVaultFailure(bufferId, handle.Generation, FabricatorVaultFailureEnsure, requiredLength);
                return false;
            }

            if (!vault.TryAcquireWriteLock(in handle, SystemID.Crafting, out buffer))
            {
                RecordFabricatorVaultFailure(bufferId, handle.Generation, FabricatorVaultFailureAcquire, requiredLength);
                buffer = default;
                return false;
            }

            bool ownershipTransferred = false;
            try
            {
                if (buffer.IsCreated &&
                    buffer.Length >= requiredLength)
                {
                    _fabricatorVaultFailureStreak = 0;
                    lockedVault = vault;
                    ownershipTransferred = true;
                    return true;
                }

                buffer = default;
            }
            finally
            {
                if (!ownershipTransferred)
                    vault.ReleaseWriteLock(in handle, SystemID.Crafting);
            }

            RecordFabricatorVaultFailure(bufferId, handle.Generation, FabricatorVaultFailureAcquire, requiredLength);
            return false;
        }

        private static void ReleaseFabricatorWrite<T>(IDataVault lockedVault, in VaultGenerationHandle<T> handle) where T : struct
        {
            if (lockedVault != null && handle.BufferID != 0u)
                lockedVault.ReleaseWriteLock(in handle, SystemID.Crafting);
        }

        private bool TryReadFabricatorBuffer<T>(
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   handle.BufferID != 0u &&
                   handle.Generation != 0u &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private void RecordFabricatorVaultFailure(BufferID bufferId, uint handleGeneration, uint flags, int capacity)
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            _fabricatorVaultFailureStreak++;
            if (!IsFabricatorVaultBufferReady(
                    in _fabricatorMemoryTelemetryRingHandle,
                    BufferID.ShinobuFabricatorMemoryTelemetryRing,
                    FabricatorMemoryTelemetryRingCapacity,
                    out IDataVault lockedVault))
            {
                return;
            }

            bool telemetryLocked = lockedVault.TryAcquireWriteLock(in _fabricatorMemoryTelemetryRingHandle, SystemID.Crafting, out NativeArray<FabricatorMemoryTelemetryEntry> telemetry);
            if (!telemetryLocked)
                return;

            try
            {
                if (!telemetry.IsCreated ||
                    telemetry.Length < FabricatorMemoryTelemetryRingCapacity)
                {
                    return;
                }

                int slot = _fabricatorMemoryTelemetryCursor % FabricatorMemoryTelemetryRingCapacity;
                uint vaultGeneration = lockedVault.VaultGenerationID;
                telemetry[slot] = new FabricatorMemoryTelemetryEntry
                {
                    Sequence = unchecked((ulong)_fabricatorMemoryTelemetryCursor + 1UL),
                    Frame = unchecked((uint)Time.frameCount),
                    BufferId = (uint)bufferId,
                    HandleGeneration = handleGeneration,
                    VaultGeneration = vaultGeneration,
                    Flags = flags,
                    Capacity = capacity,
                    FailureStreak = _fabricatorVaultFailureStreak,
                    GlobalQualityWeight = ResolveAssemblyQuality(),
                    CpuMicroseconds = 0f,
                    GpuMicroseconds = 0f,
                    StateHash = MixFabricatorVaultStateHash((uint)bufferId, handleGeneration, vaultGeneration, flags),
                    SystemId = (uint)SystemID.Crafting
                };

                _fabricatorMemoryTelemetryCursor++;
            }
            finally
            {
                lockedVault.ReleaseWriteLock(in _fabricatorMemoryTelemetryRingHandle, SystemID.Crafting);
            }

            if (_fabricatorVaultFailureStreak >= FabricatorVaultFailureDumpThreshold)
                TryDumpFabricatorMemoryBlackBox(lockedVault);
        }

        private void TryDumpFabricatorMemoryBlackBox(IDataVault vault)
        {
            if (_fabricatorBlackBoxDumped ||
                vault == null)
            {
                return;
            }

            if (vault.TryReadOnlyHandle(in _fabricatorMemoryTelemetryRingHandle, out NativeArray<FabricatorMemoryTelemetryEntry>.ReadOnly telemetry) &&
                telemetry.IsCreated)
            {
                if (TryWriteFabricatorMemoryDump(FabricatorMemoryDumpPath, unchecked((uint)Time.frameCount), telemetry))
                {
                    _fabricatorBlackBoxDumped = true;
                    _fabricatorVaultFailureStreak = 0;
                }
            }
        }

        private static bool TryWriteFabricatorMemoryDump(
            string dumpPath,
            uint frame,
            NativeArray<FabricatorMemoryTelemetryEntry>.ReadOnly telemetry)
        {
            const int headerBytes = 16;
            int entryCount = math.min(telemetry.Length, FabricatorMemoryTelemetryRingCapacity);
            int byteCount = headerBytes + (entryCount * FabricatorMemoryTelemetryEntrySizeBytes);
            NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                byteCount,
                nameof(Fabricator),
                "FabricatorMemoryDumpPayload");

            try
            {
                WriteUInt32LittleEndian(payload, 0, FabricatorMemoryTelemetryMagic);
                WriteUInt32LittleEndian(payload, 4, frame);
                WriteUInt32LittleEndian(payload, 8, (uint)SystemID.Crafting);
                WriteInt32LittleEndian(payload, 12, entryCount);

                int offset = headerBytes;
                for (int i = 0; i < entryCount; i++)
                {
                    FabricatorMemoryTelemetryEntry entry = telemetry[i];
                    WriteUInt64LittleEndian(payload, offset, entry.Sequence);
                    WriteUInt64LittleEndian(payload, offset + 8, entry.StateHash);
                    WriteUInt32LittleEndian(payload, offset + 16, entry.Frame);
                    WriteUInt32LittleEndian(payload, offset + 20, entry.BufferId);
                    WriteUInt32LittleEndian(payload, offset + 24, entry.HandleGeneration);
                    WriteUInt32LittleEndian(payload, offset + 28, entry.VaultGeneration);
                    WriteUInt32LittleEndian(payload, offset + 32, entry.Flags);
                    WriteInt32LittleEndian(payload, offset + 36, entry.Capacity);
                    WriteInt32LittleEndian(payload, offset + 40, entry.FailureStreak);
                    WriteFloat32LittleEndian(payload, offset + 44, entry.GlobalQualityWeight);
                    WriteFloat32LittleEndian(payload, offset + 48, entry.CpuMicroseconds);
                    WriteFloat32LittleEndian(payload, offset + 52, entry.GpuMicroseconds);
                    WriteUInt32LittleEndian(payload, offset + 56, entry.SystemId);
                    WriteUInt32LittleEndian(payload, offset + 60, 0u);
                    offset += FabricatorMemoryTelemetryEntrySizeBytes;
                }

                return NativeFaultDumpWriter.TryWriteAll(dumpPath, payload, byteCount);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(Fabricator),
                    "FabricatorMemoryDumpPayload");
            }
        }

        private static void WriteFloat32LittleEndian(NativeArray<byte> destination, int offset, float value)
        {
            WriteUInt32LittleEndian(destination, offset, math.asuint(value));
        }

        private static void WriteInt32LittleEndian(NativeArray<byte> destination, int offset, int value)
        {
            WriteUInt32LittleEndian(destination, offset, unchecked((uint)value));
        }

        private static void WriteUInt32LittleEndian(NativeArray<byte> destination, int offset, uint value)
        {
            destination[offset] = (byte)value;
            destination[offset + 1] = (byte)(value >> 8);
            destination[offset + 2] = (byte)(value >> 16);
            destination[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteUInt64LittleEndian(NativeArray<byte> destination, int offset, ulong value)
        {
            WriteUInt32LittleEndian(destination, offset, unchecked((uint)value));
            WriteUInt32LittleEndian(destination, offset + 4, unchecked((uint)(value >> 32)));
        }

        private static ulong MixFabricatorVaultStateHash(uint bufferId, uint handleGeneration, uint vaultGeneration, uint flags)
        {
            ulong hash = 1469598103934665603UL;
            hash = (hash ^ bufferId) * 1099511628211UL;
            hash = (hash ^ handleGeneration) * 1099511628211UL;
            hash = (hash ^ vaultGeneration) * 1099511628211UL;
            hash = (hash ^ flags) * 1099511628211UL;
            return hash;
        }

        public static bool ValidateFabricatorMemoryTelemetryLayout()
        {
            return Marshal.SizeOf<FabricatorMemoryTelemetryEntry>() == FabricatorMemoryTelemetryEntrySizeBytes &&
                   Marshal.OffsetOf<FabricatorMemoryTelemetryEntry>(nameof(FabricatorMemoryTelemetryEntry.Sequence)).ToInt32() == 0 &&
                   Marshal.OffsetOf<FabricatorMemoryTelemetryEntry>(nameof(FabricatorMemoryTelemetryEntry.StateHash)).ToInt32() == 8 &&
                   Marshal.OffsetOf<FabricatorMemoryTelemetryEntry>(nameof(FabricatorMemoryTelemetryEntry.Frame)).ToInt32() == 16 &&
                   Marshal.OffsetOf<FabricatorMemoryTelemetryEntry>(nameof(FabricatorMemoryTelemetryEntry.BufferId)).ToInt32() == 20 &&
                   Marshal.OffsetOf<FabricatorMemoryTelemetryEntry>(nameof(FabricatorMemoryTelemetryEntry.HandleGeneration)).ToInt32() == 24 &&
                   Marshal.OffsetOf<FabricatorMemoryTelemetryEntry>(nameof(FabricatorMemoryTelemetryEntry.VaultGeneration)).ToInt32() == 28 &&
                   Marshal.OffsetOf<FabricatorMemoryTelemetryEntry>(nameof(FabricatorMemoryTelemetryEntry.Flags)).ToInt32() == 32 &&
                   Marshal.OffsetOf<FabricatorMemoryTelemetryEntry>(nameof(FabricatorMemoryTelemetryEntry.Capacity)).ToInt32() == 36 &&
                   Marshal.OffsetOf<FabricatorMemoryTelemetryEntry>(nameof(FabricatorMemoryTelemetryEntry.FailureStreak)).ToInt32() == 40 &&
                   Marshal.OffsetOf<FabricatorMemoryTelemetryEntry>(nameof(FabricatorMemoryTelemetryEntry.GlobalQualityWeight)).ToInt32() == 44 &&
                   Marshal.OffsetOf<FabricatorMemoryTelemetryEntry>(nameof(FabricatorMemoryTelemetryEntry.CpuMicroseconds)).ToInt32() == 48 &&
                   Marshal.OffsetOf<FabricatorMemoryTelemetryEntry>(nameof(FabricatorMemoryTelemetryEntry.GpuMicroseconds)).ToInt32() == 52 &&
                   Marshal.OffsetOf<FabricatorMemoryTelemetryEntry>(nameof(FabricatorMemoryTelemetryEntry.SystemId)).ToInt32() == 56;
        }

        private int TryDeliverCraftOutput(RecipeData recipe, ItemData result, int outputQuantity, out bool outputDeliveryFault, bool emitFaultFeedback = true)
        {
            outputDeliveryFault = false;
            if (result == null || outputQuantity <= 0)
                return 0;

            if (TrySynthesizeCraftOutput(recipe, result, outputQuantity))
                return outputQuantity;

            int deliveredQuantity = 0;
            if (_playerInventory != null)
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
                    bool overflowDelivered = TryEmitCraftOverflowStack(result, remainingQuantity);
                    if (overflowDelivered)
                        deliveredQuantity += remainingQuantity;
                    else
                        outputDeliveryFault = true;

                    if (emitFaultFeedback)
                    {
                        RaiseStorageCapacityExceededBark();
                        if (!overflowDelivered)
                            TriggerCraftFailureFeedback();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Hecton8.Core.H8Debug.LogWarning("[Fabricator] Craft output overflow; routed to diegetic bark/drop fallback.");
#endif
                    }
                }
            }

            return deliveredQuantity;
        }

        private bool TryStorePendingCraftOutput(RecipeData recipe, ItemData result, int quantity, int totalQuantity)
        {
            if (result == null || quantity <= 0)
                return false;

            if (HasPendingCraftOutput && !ReferenceEquals(_pendingCraftOutputItem, result))
                return false;

            if (HasPendingCraftOutput)
            {
                int mergedQuantity = _pendingCraftOutputQuantity + quantity;
                int mergedTotalQuantity = _pendingCraftOutputTotalQuantity + Mathf.Max(1, totalQuantity);
                if (mergedQuantity <= _pendingCraftOutputQuantity || mergedTotalQuantity <= _pendingCraftOutputTotalQuantity)
                    return false;

                _pendingCraftOutputQuantity = mergedQuantity;
                _pendingCraftOutputTotalQuantity = mergedTotalQuantity;
                if (_pendingCraftOutputRecipe == null)
                    _pendingCraftOutputRecipe = recipe;
                return true;
            }

            _pendingCraftOutputRecipe = recipe;
            _pendingCraftOutputItem = result;
            _pendingCraftOutputQuantity = quantity;
            _pendingCraftOutputTotalQuantity = Mathf.Max(quantity, totalQuantity);
            return true;
        }

        private void ClearPendingCraftOutput()
        {
            _pendingCraftOutputRecipe = null;
            _pendingCraftOutputItem = null;
            _pendingCraftOutputQuantity = 0;
            _pendingCraftOutputTotalQuantity = 0;
        }

        internal void PopulateSaveData(ref ModuleDTO dto)
        {
            dto.fabricatorPendingOutputItemId = string.Empty;
            dto.fabricatorPendingOutputQuantity = 0;
            dto.fabricatorPendingOutputTotalQuantity = 0;
            if (!HasPendingCraftOutput)
                return;

            ItemData result = _pendingCraftOutputItem;
            string persistentId = result != null ? result.PersistentId : string.Empty;
            int quantity = math.max(0, _pendingCraftOutputQuantity);
            if (string.IsNullOrWhiteSpace(persistentId) || quantity <= 0)
                return;

            dto.fabricatorPendingOutputItemId = persistentId;
            dto.fabricatorPendingOutputQuantity = quantity;
            dto.fabricatorPendingOutputTotalQuantity = math.max(quantity, _pendingCraftOutputTotalQuantity);
        }

        internal void RestoreFromSaveData(ModuleDTO dto, ItemCatalog itemCatalog)
        {
            int quantity = math.max(0, dto.fabricatorPendingOutputQuantity);
            if (quantity <= 0)
            {
                ClearPendingCraftOutput();
                return;
            }

            string itemId = dto.fabricatorPendingOutputItemId;
            if (itemCatalog == null || string.IsNullOrWhiteSpace(itemId))
                return;

            ItemData result = itemCatalog.FindById(itemId);
            if (result == null)
                return;

            ClearPendingCraftOutput();
            _pendingCraftOutputItem = result;
            _pendingCraftOutputQuantity = quantity;
            _pendingCraftOutputTotalQuantity = math.max(quantity, dto.fabricatorPendingOutputTotalQuantity);
            if (TryResolveRecipeForResultItem(result, out RecipeData recipe))
                _pendingCraftOutputRecipe = recipe;
        }

        internal bool CanEjectPendingCraftOutput(PlayerInventory inventory, Vector3 dropPosition)
        {
            if (!HasPendingCraftOutput)
                return true;

            ItemData result = _pendingCraftOutputItem;
            int itemHashId = ComputeItemHash(result);
            int quantity = math.max(1, _pendingCraftOutputQuantity);
            if (result == null || itemHashId == 0 || quantity <= 0)
                return false;

            if (inventory != null &&
                inventory.CanAcceptItemQuantity(itemHashId, quantity))
            {
                return true;
            }

            PersistentWorldRegistry registry = _persistentWorldRegistry;
            return IsFiniteRuntimePosition(dropPosition) &&
                   registry != null &&
                   registry.CanRegisterDroppedItem(result, quantity, dropPosition);
        }

        internal bool EjectPendingCraftOutput(PlayerInventory inventory, ref Vector3 dropPosition)
        {
            if (!HasPendingCraftOutput)
                return true;

            if (!CanEjectPendingCraftOutput(inventory, dropPosition))
                return false;

            ItemData result = _pendingCraftOutputItem;
            int itemHashId = ComputeItemHash(result);
            int quantity = math.max(1, _pendingCraftOutputQuantity);
            if (inventory != null &&
                inventory.CanAcceptItemQuantity(itemHashId, quantity) &&
                inventory.TryAddItem(itemHashId, quantity))
            {
                ClearPendingCraftOutput();
                return true;
            }

            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry != null &&
                registry.TryRegisterDroppedItem(result, quantity, dropPosition))
            {
                ClearPendingCraftOutput();
                dropPosition.x += 0.3f;
                return true;
            }

            return false;
        }

        private bool TryFlushPendingCraftOutput()
        {
            if (!HasPendingCraftOutput)
                return true;

            RecipeData recipe = _pendingCraftOutputRecipe;
            ItemData result = _pendingCraftOutputItem;
            int requestedQuantity = _pendingCraftOutputQuantity;
            int completedTotalQuantity = Mathf.Max(requestedQuantity, _pendingCraftOutputTotalQuantity);
            int deliveredQuantity = TryDeliverCraftOutput(recipe, result, requestedQuantity, out _, emitFaultFeedback: false);
            if (deliveredQuantity <= 0)
                return false;

            PublishCraftItemAcquiredSignal(result, deliveredQuantity);

            int remainingQuantity = requestedQuantity - deliveredQuantity;
            if (remainingQuantity > 0)
            {
                _pendingCraftOutputQuantity = remainingQuantity;
                return false;
            }

            ClearPendingCraftOutput();
            if (recipe != null)
                PublishCraftingCompletedSignal(recipe, result, completedTotalQuantity);
            CraftingEvents.TryRaiseCraftCompleted(result);
            PublishFabricatorActiveCountBlackBox();
            PlaySound(craftCompleteSound);
            return true;
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
            PlayerInventory inventory = _playerInventory;
            if (itemHashId == 0 || inventory == null)
                return false;

            Hecton8.SaveSystem.ItemCatalog itemCatalog = inventory.ItemCatalog;
            if (itemCatalog == null)
                return false;

            ItemData targetItem = itemCatalog.FindByHash(itemHashId);
            if (targetItem == null || targetItem.DeconstructYieldCount <= 0)
                return false;

            if (!HasCraftingScratchReady())
                return false;

            int2[] outputs = _deconstructionRecipeOutputsScratch;
            int[] outputCountBuffer = _deconstructionOutputCountScratch;
            if (!CraftingSystem.TryBuildDeconstructionYieldBuffer(
                    targetItem,
                    outputs,
                    outputCountBuffer))
            {
                return false;
            }

            int outputCount = outputCountBuffer[0];
            if (outputCount <= 0)
            {
                return false;
            }

            if (!TryValidateDeconstructionYieldBuffer(outputs, outputCount, itemCatalog))
            {
                return false;
            }

            if (!inventory.TryRemoveFirstMatchingItemByHash(itemHashId))
                return false;

            if (!CanInventoryAcceptDeconstructionYieldBuffer(inventory, outputs, outputCount))
            {
                inventory.TryAddItem(itemHashId, 1);
                return false;
            }

            Span<int> emittedItemHashIds = stackalloc int[CraftingSystem.MaxDeconstructionOutputCount];
            Span<int> emittedQuantities = stackalloc int[CraftingSystem.MaxDeconstructionOutputCount];
            int emittedCount = 0;

            for (int outputIndex = 0; outputIndex < outputCount; outputIndex++)
            {
                int2 output = outputs[outputIndex];
                ItemData outputItem = itemCatalog.FindByHash(output.x);
                if (!TryEmitDeconstructionYield(outputItem, output.x, output.y, inventory))
                {
                    RollbackDeconstructionInventoryOutputs(inventory, emittedItemHashIds, emittedQuantities, emittedCount);
                    inventory.TryAddItem(itemHashId, 1);
                    return false;
                }

                emittedItemHashIds[emittedCount] = output.x;
                emittedQuantities[emittedCount] = output.y;
                emittedCount++;
            }

            if (emittedCount <= 0)
            {
                inventory.TryAddItem(itemHashId, 1);
                return false;
            }

            ResolveDeconstructionOutputPose(out Vector3 spawnPosition, out Vector3 velocityChange);
            for (int outputIndex = 0; outputIndex < outputCount; outputIndex++)
            {
                int2 output = outputs[outputIndex];
                ItemData outputItem = itemCatalog.FindByHash(output.x);
                CraftingEvents.TryRaiseCraftOutputSynthesized(
                    new CraftedItemSynthesisEvent(outputItem, output.y, spawnPosition, velocityChange));
            }

            return true;
        }

        private bool TryValidateDeconstructionYieldBuffer(
            int2[] outputs,
            int outputCount,
            Hecton8.SaveSystem.ItemCatalog itemCatalog)
        {
            if (outputs == null ||
                itemCatalog == null ||
                outputCount <= 0 ||
                outputCount > outputs.Length)
            {
                return false;
            }

            for (int outputIndex = 0; outputIndex < outputCount; outputIndex++)
            {
                int2 output = outputs[outputIndex];
                if (output.x == 0 || output.y <= 0 || itemCatalog.FindByHash(output.x) == null)
                    return false;
            }

            return true;
        }

        private static bool CanInventoryAcceptDeconstructionYieldBuffer(PlayerInventory inventory, int2[] outputs, int outputCount)
        {
            if (outputs == null ||
                outputCount <= 0 ||
                outputCount > outputs.Length ||
                outputCount > CraftingSystem.MaxDeconstructionOutputCount ||
                inventory == null)
            {
                return false;
            }

            Span<int> itemHashIds = stackalloc int[CraftingSystem.MaxDeconstructionOutputCount];
            Span<int> quantities = stackalloc int[CraftingSystem.MaxDeconstructionOutputCount];

            for (int outputIndex = 0; outputIndex < outputCount; outputIndex++)
            {
                int2 output = outputs[outputIndex];
                if (output.x == 0 || output.y <= 0)
                    return false;

                itemHashIds[outputIndex] = output.x;
                quantities[outputIndex] = output.y;
            }

            return inventory.CanAcceptItemQuantityBatch(itemHashIds, quantities, outputCount);
        }

        private static bool TryEmitDeconstructionYield(
            ItemData outputItem,
            int itemHashId,
            int quantity,
            PlayerInventory inventory)
        {
            if (outputItem == null || itemHashId == 0 || quantity <= 0)
                return false;

            return inventory != null && inventory.TryAddItem(itemHashId, quantity);
        }

        private static void RollbackDeconstructionInventoryOutputs(
            PlayerInventory inventory,
            ReadOnlySpan<int> itemHashIds,
            ReadOnlySpan<int> quantities,
            int count)
        {
            if (inventory == null || count <= 0)
                return;

            int safeCount = math.min(count, math.min(itemHashIds.Length, quantities.Length));
            for (int outputIndex = safeCount - 1; outputIndex >= 0; outputIndex--)
            {
                int itemHashId = itemHashIds[outputIndex];
                int quantity = quantities[outputIndex];
                if (itemHashId != 0 && quantity > 0)
                    inventory.TryRemoveQuantity(itemHashId, quantity);
            }
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

                if (_networkCostAmounts[i] > int.MaxValue - amount)
                    return false;

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
                int requiredAmount = CalculateAdjustedIngredientAmount(cost) * safeMultiplier;
                int removableCount = localAvailable < requiredAmount ? localAvailable : requiredAmount;
                total += cost.item.CellArea * removableCount;
            }

            return total;
        }

        private bool ConsumeIngredients(RecipeData recipe, int multiplier = 1)
        {
            if (recipe == null || recipe.ingredients == null || _playerInventory == null || _playerInventory.Grid == null)
                return false;

            RefundIngredients();
            _craftReservationOwner = _playerInventory;
            if (!HasCraftingScratchReady())
                return false;

            int safeMultiplier = Mathf.Max(1, multiplier);
            if (TryReserveDirectFastFailRecipeCosts(recipe, safeMultiplier))
                return true;

            RefundIngredients();

            int2[] recipeCosts = _craftRecipeCostsScratch;
            if (CraftingSystem.TryBuildRecipeCostBuffer(recipe, this, recipeCosts, out int recipeCostCount, safeMultiplier) &&
                TryReserveIngredientCostBuffer(recipeCosts, recipeCostCount))
            {
                return true;
            }

            RefundIngredients();
            int2[] rawCosts = _complexRecipeRawCostsScratch;
            int[] rawCostCount = _complexRecipeRawCostCountScratch;
            if (CraftingSystem.TryBuildTotalRawCostBuffer(
                    recipe,
                    this,
                    _playerInventory.ItemCatalog,
                    _complexRecipeGraphNodesScratch,
                    _complexRecipeGraphEdgesScratch,
                    _complexRecipeGraphInDegreesScratch,
                    _complexRecipeGraphQueueScratch,
                    rawCosts,
                    rawCostCount,
                    _complexRecipeGraphStatusScratch,
                    safeMultiplier))
            {
                if (TryReserveIngredientCostBuffer(rawCosts, rawCostCount[0]))
                    return true;

                RefundIngredients();
            }

            return false;
        }

        private bool TryReserveIngredientCostBuffer(int2[] costs, int costCount)
        {
            if (costs == null || costCount <= 0 || costCount > costs.Length || _playerInventory == null)
                return false;

            PlayerInventory reservationOwner = _playerInventory;
            RefundIngredients();
            _craftReservationOwner = reservationOwner;

            for (int costIndex = 0; costIndex < costCount; costIndex++)
            {
                int2 cost = costs[costIndex];
                if (cost.x == 0 || cost.y <= 0)
                    continue;

                int remaining = cost.y;
                if (!reservationOwner.TryReserveAvailableQuantityForCraft(
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
            PlayerInventory reservationOwner = _craftReservationOwner != null ? _craftReservationOwner : _playerInventory;
            if (reservationOwner != null && _localCraftReservationCount > 0)
                reservationOwner.ReleaseCraftReservations(_localCraftReservations, _localCraftReservationCount);

            _localCraftReservationCount = 0;
            _craftReservationOwner = null;
            if (_networkReservation != null)
            {
                BaseLogisticsNetwork.RollbackReserved(_networkReservation);
                _networkReservation = null;
            }
            _networkCostCount = 0;

        }

        // ----------------------------------------------------------
        //  PRIVATE — DISTANCE CHECK
        // ----------------------------------------------------------

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
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null)
            {
                if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                    (snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                    snapshot.Aup.IsFinite())
                {
                    playerAup = snapshot.Aup;
                    return true;
                }

                if (playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                    (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                    movementState.PredictedAup.IsFinite())
                {
                    playerAup = movementState.PredictedAup;
                    return true;
                }

                playerAup = default;
                return false;
            }

            if (_playerMovement != null)
            {
                AbsoluteUniversePosition currentAup = _playerMovement.CurrentAup;
                if (currentAup.IsFinite())
                {
                    playerAup = currentAup;
                    return true;
                }
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

        // ----------------------------------------------------------
        //  PRIVATE — AUDIO
        // ----------------------------------------------------------

        private void PlaySound(AudioClip clip)
        {
            if (clip == null)
                return;

            _pendingAudioClip = clip;
            _pendingAudioPosition = transform.position;
            _pendingAudioDirty = true;
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

            IAudioService audioService = ResolveAudioService();
            if (audioService != null)
                audioService.PlayAtPoint(clip, position);
        }

        private void QueueProceduralAudioPing(
            Vector3 position,
            float intensity01,
            float durationSeconds,
            float transmission01,
            float pitchCarrierHz,
            ProceduralAudioPingKind kind)
        {
            ProceduralAudioPingRequest request = default;
            request.Position = position;
            request.Intensity01 = Mathf.Clamp01(intensity01);
            request.DurationSeconds = Mathf.Max(0f, durationSeconds);
            request.Transmission01 = Mathf.Clamp01(transmission01);
            request.PitchCarrierHz = Mathf.Max(0f, pitchCarrierHz);
            request.Kind = kind;

            if (_pendingProceduralAudioPingCount == 0)
            {
                _pendingProceduralAudioPing0 = request;
                _pendingProceduralAudioPingCount = 1;
            }
            else
            {
                _pendingProceduralAudioPing1 = request;
                _pendingProceduralAudioPingCount = 2;
            }

        }

        private void FlushPendingProceduralAudioPings()
        {
            int count = _pendingProceduralAudioPingCount;
            if (count <= 0)
                return;

            _pendingProceduralAudioPingCount = 0;
            FlushProceduralAudioPing(in _pendingProceduralAudioPing0);
            if (count > 1)
                FlushProceduralAudioPing(in _pendingProceduralAudioPing1);

            _pendingProceduralAudioPing0 = default;
            _pendingProceduralAudioPing1 = default;
        }

        private static void FlushProceduralAudioPing(in ProceduralAudioPingRequest request)
        {
            ProceduralAudioEvents.TryRaiseAudioPingTriggered(
                request.Position,
                request.Intensity01,
                request.DurationSeconds,
                request.Transmission01,
                request.PitchCarrierHz,
                request.Kind);
        }

        private void RaiseFabricatorProgressAudioPing()
        {
            float pitchCarrierHz = Mathf.Clamp(900f + (_activeCraftPowerMultiplier * 180f), 900f, 2200f);
            QueueProceduralAudioPing(
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

        private void QueueFabricatorProgressHaptics(float progress)
        {
            float finalPulseT = math.saturate((progress - 0.9f) * 10f);
            float finalPulse01 = finalPulseT * finalPulseT * (3f - (2f * finalPulseT));
            _pendingProgressHaptic.LowFrequencyIntensity = math.saturate(math.lerp(0.12f, 0.3f, progress) + finalPulse01 * 0.35f);
            _pendingProgressHaptic.HighFrequencyIntensity = math.saturate(0.025f + finalPulse01 * 0.05f);
            _pendingProgressHaptic.DurationSeconds = 0.18f;
            _pendingProgressHaptic.PulseFrequencyHz = math.lerp(18f, 30f, finalPulse01);
            _pendingProgressHaptic.Priority = finalPulse01 > 0f ? FabricatorFinalHapticPriority : FabricatorHapticPriority;
            _pendingProgressHaptic.MotorMask = FabricatorHapticMotorMask;
            _pendingProgressHapticDirty = true;
        }

        private void FlushPendingProgressHaptics()
        {
            if (!_pendingProgressHapticDirty)
                return;

            FabricatorHapticRequest request = _pendingProgressHaptic;
            _pendingProgressHaptic = default;
            _pendingProgressHapticDirty = false;
            ToolHapticsRuntime.TryEnqueueSinusoidalCommand(
                request.LowFrequencyIntensity,
                request.HighFrequencyIntensity,
                request.DurationSeconds,
                request.PulseFrequencyHz,
                request.Priority,
                request.MotorMask);
        }

        private void TriggerCraftFailureFeedback()
        {
            _errorFlashRemainingSeconds = Mathf.Max(_errorFlashRemainingSeconds, errorFlashDurationSeconds);
            ApplyErrorFeedback(1f);
            CraftingEvents.TryRaiseCraftFailed(this);
            PlaySound(fabricationErrorBuzzerSound);
            QueueProceduralAudioPing(
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
            _sparkProxyLightDirty = true;
        }

        private void FlushSparkProxyLightRegistration()
        {
            if (_sparkProxyLightDirty)
            {
                _sparkProxyLightDirty = false;
                UpdateSparkProxyLightRegistration();
            }

            if (!_sparkProxyLightUnregisterPending)
                return;

            _sparkProxyLightUnregisterPending = false;
            if (!(_sparkProxyLightRemainingSeconds > 0f))
                UnregisterSparkProxyLight();
        }

        private void QueueUnregisterSparkProxyLight()
        {
            _sparkProxyLightUnregisterPending = true;
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
                (float)SystemDispatcher.CurrentUnscaledTimeSeconds);

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
                // construction.md 8A: the hologram preview is presentation. It must not own recipe
                // completion. Tear down the preview only, then keep the fabrication job running
                // headless so an active craft still advances and CompleteCraft() still fires.
                FlushEndAssemblyPresentation();
                if (!_isCrafting || recipe == null)
                {
                    // No craft to keep alive; match the previous full-teardown identity reset.
                    _assemblyTargetHash = 0u;
                    return;
                }

                // Headless assembly has no preview mesh, so drive the vault bounds from the
                // authored padding envelope instead of a stale mesh AABB left by an earlier recipe.
                _assemblyBaseY = 0f;
                _assemblyTopY = Mathf.Max(0.001f, assemblyHeightPadding);
                _assemblyCurrentHeightY = _assemblyBaseY;
                BeginFabricationVaultJob(recipe);
                return;
            }

            assemblyPreviewMeshFilter.sharedMesh = sourceMesh;
            _assemblyActualMaterial = actualMaterial;

            float padding = Mathf.Max(0f, assemblyHeightPadding);
            CalculateAssemblyFabricatorLocalHeightBounds(sourceMesh, assemblyPreviewMeshFilter.transform, padding, out _assemblyBaseY, out _assemblyTopY);
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

            if (item != null)
            {
                for (int index = 0; index < _assemblySourceCount; index++)
                {
                    if (!ReferenceEquals(_assemblySourceItems[index], item))
                        continue;

                    sourceMesh = _assemblySourceMeshes[index];
                    actualMaterial = _assemblySourceMaterials[index];
                    return sourceMesh != null;
                }
            }

            sourceMesh = ResolveAssemblyFallbackMesh();
            return sourceMesh != null;
        }

        private void RebuildAssemblySourceCacheCold()
        {
            if (!EnsureAssemblySourceCacheCapacityCold(MaxRecipeCacheEntries))
                return;

            Array.Clear(_assemblySourceItems, 0, _assemblySourceItems.Length);
            Array.Clear(_assemblySourceMeshes, 0, _assemblySourceMeshes.Length);
            Array.Clear(_assemblySourceMaterials, 0, _assemblySourceMaterials.Length);
            _assemblySourceCount = 0;

            if (availableRecipes != null)
            {
                for (int i = 0; i < availableRecipes.Count; i++)
                    AppendAssemblySourceCacheCold(availableRecipes[i]);
            }

            int runtimeRecipeCount = ModRecipeRegistry.Count;
            for (int i = 0; i < runtimeRecipeCount; i++)
            {
                RecipeData runtimeRecipe = ModRecipeRegistry.GetAt(i);
                if (runtimeRecipe == null || ContainsAuthoredRecipeReference(runtimeRecipe))
                    continue;

                AppendAssemblySourceCacheCold(runtimeRecipe);
            }
        }

        private bool EnsureAssemblySourceCacheCapacityCold(int requiredCapacity)
        {
            if (requiredCapacity <= 0)
                return false;

            if (_assemblySourceItems != null &&
                _assemblySourceMeshes != null &&
                _assemblySourceMaterials != null &&
                _assemblySourceItems.Length >= requiredCapacity &&
                _assemblySourceMeshes.Length >= requiredCapacity &&
                _assemblySourceMaterials.Length >= requiredCapacity)
            {
                return true;
            }

            _assemblySourceItems = new ItemData[requiredCapacity];
            _assemblySourceMeshes = new Mesh[requiredCapacity];
            _assemblySourceMaterials = new Material[requiredCapacity];
            _assemblySourceCount = 0;
            return true;
        }

        private void AppendAssemblySourceCacheCold(RecipeData recipe)
        {
            ItemData item = recipe != null ? recipe.resultItem : null;
            if (item == null || _assemblySourceItems == null || _assemblySourceCount >= _assemblySourceItems.Length)
                return;

            for (int index = 0; index < _assemblySourceCount; index++)
            {
                if (ReferenceEquals(_assemblySourceItems[index], item))
                    return;
            }

            if (!TryCaptureAssemblySourceFromPrefabCold(
                    item.worldPrefab,
                    out Mesh sourceMesh,
                    out Material actualMaterial))
            {
                sourceMesh = ResolveAssemblyFallbackMesh();
                actualMaterial = null;
            }

            if (sourceMesh == null)
                return;

            int targetIndex = _assemblySourceCount++;
            _assemblySourceItems[targetIndex] = item;
            _assemblySourceMeshes[targetIndex] = sourceMesh;
            _assemblySourceMaterials[targetIndex] = actualMaterial;
        }

        private static bool TryCaptureAssemblySourceFromPrefabCold(
            GameObject prefab,
            out Mesh sourceMesh,
            out Material actualMaterial)
        {
            sourceMesh = null;
            actualMaterial = null;
            if (prefab == null)
                return false;

            prefab.TryGetComponent(out MeshFilter sourceFilter);
            prefab.TryGetComponent(out MeshRenderer sourceRenderer);
            if (sourceFilter == null)
                sourceFilter = ComponentReferenceUtility.ResolveOwnedComponent<MeshFilter>(prefab.transform);
            if (sourceRenderer == null)
                sourceRenderer = ComponentReferenceUtility.ResolveOwnedComponent<MeshRenderer>(prefab.transform);

            if (sourceFilter != null)
            {
                sourceMesh = sourceFilter.sharedMesh;
                actualMaterial = sourceRenderer != null ? sourceRenderer.sharedMaterial : null;
            }

            if (sourceMesh != null)
                return true;

            SkinnedMeshRenderer skinnedRenderer = ComponentReferenceUtility.ResolveOwnedComponent<SkinnedMeshRenderer>(prefab.transform);
            if (skinnedRenderer == null)
            {
                return false;
            }

            sourceMesh = skinnedRenderer.sharedMesh;
            actualMaterial = skinnedRenderer.sharedMaterial;
            if (sourceMesh != null)
                return true;

            return false;
        }

        private Mesh ResolveAssemblyFallbackMesh()
        {
            if (assemblyFallbackMesh != null)
                return assemblyFallbackMesh;

            UnityEngine.Assertions.Assert.IsNotNull(
                assemblyFallbackMesh,
                "Fatal: Fabricator cannot resolve an assembly preview mesh and no authored fallback mesh is assigned.");
            return null;
        }

        private void CalculateAssemblyFabricatorLocalHeightBounds(
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
        }

        private void FlushCompleteAssemblyVisual()
        {
            if (!_assemblyPreviewActive)
            {
                // Headless craft: there is no hologram to hand over to the real material, but the
                // assembler slot is still ours and must be released or it stays Active forever.
                FabricationAssemblerRuntime.ClearSlot(_fabricationJobSlot);
                _fabricationJobSlot = -1;
                return;
            }

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
        }

        private void FlushEndAssemblyVisual()
        {
            // Full teardown: the simulation job dies with the presentation only here, where the
            // craft itself has already ended (CompleteCraft/CancelCraft) or the component is
            // being disabled/destroyed.
            FabricationAssemblerRuntime.ClearSlot(_fabricationJobSlot);
            _fabricationJobSlot = -1;
            _assemblyTargetHash = 0u;
            FlushEndAssemblyPresentation();
        }

        /// <summary>
        /// Presentation-only teardown. Leaves the fabrication job slot and its target hash intact
        /// so a craft that cannot render a hologram still runs to completion.
        /// </summary>
        private void FlushEndAssemblyPresentation()
        {
            _assemblyPreviewActive = false;
            _assemblyMaterialSwapped = false;
            _assemblyActualMaterial = null;
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
            uint frame = SystemDispatcher.CurrentFrameId;
            SignalBus<ToolAcousticSignal>.TryPushTracked(new ToolAcousticSignal
            {
                ToolHash = FabricatorToolHash,
                TargetHash = _assemblyTargetHash != 0u ? _assemblyTargetHash : FabricatorWeldingFallbackHash,
                Progress01 = progress,
                PitchScale = math.lerp(fabricationWeldingLoopMinPitch, fabricationWeldingLoopMaxPitch, progress),
                Intensity01 = math.saturate(0.32f + _activeCraftPowerMultiplier * 0.12f),
                Frame = frame,
                State = ToolAcousticStateWelding,
                Flags = IsPausedNoPower ? PowerDrainFlagPaused : (byte)0
            }, ref s_x001FabricatorSignalPushDropCount);
        }

        private void PublishPowerDrainSignal(float progressPerSecond, float progress01, bool paused)
        {
            float speed = math.max(0f, progressPerSecond);
            float watts = math.max(0f, craftPowerDraw * Mathf.Max(1f, _activeCraftPowerMultiplier) * speed);
            if (!(watts > 0f) && !paused)
                return;

            uint frame = SystemDispatcher.CurrentFrameId;
            SignalBus<PowerDrainSignal>.TryPushTracked(new PowerDrainSignal
            {
                ConsumerHash = ResolveFabricatorSignalHash(),
                NetworkHash = 0u,
                Watts = watts,
                Progress01 = math.saturate(progress01),
                Frame = frame,
                Reason = PowerDrainReasonFabrication,
                Flags = paused ? PowerDrainFlagPaused : (byte)0
            }, ref s_x001FabricatorSignalPushDropCount);
        }

        private void PublishCraftingStartedSignal(RecipeData recipe, int multiplier)
        {
            uint frame = SystemDispatcher.CurrentFrameId;
            SignalBus<CraftingStartedSignal>.TryPushTracked(new CraftingStartedSignal
            {
                FabricatorHash = ResolveFabricatorSignalHash(),
                RecipeHash = ComputeRecipeSignalHash(recipe),
                ResultItemHash = recipe != null ? unchecked((uint)ComputeItemHash(recipe.resultItem)) : 0u,
                Frame = frame,
                Multiplier = (ushort)math.min(math.max(1, multiplier), ushort.MaxValue),
                Flags = 0
            }, ref s_x001FabricatorSignalPushDropCount);
        }

        private void PublishCraftingCompletedSignal(RecipeData recipe, ItemData item, int quantity)
        {
            uint frame = SystemDispatcher.CurrentFrameId;
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

            SignalBus<ItemAcquiredSignal>.TryPushTracked(new ItemAcquiredSignal
            {
                PositionAup = positionAup,
                ItemHash = unchecked((uint)itemHash),
                OreHash = 0u,
                Quantity = (ushort)math.min(math.max(1, quantity), ushort.MaxValue),
                SourceKind = ItemAcquiredSourceFabricator,
                Flags = 0,
                Frame = SystemDispatcher.CurrentFrameId
            }, ref s_x001FabricatorSignalPushDropCount);
        }

        private uint ResolveFabricatorSignalHash()
        {
            return unchecked((uint)EntityId.ToULong(GetEntityId()));
        }

        private static uint ComputeRecipeSignalHash(RecipeData recipe)
        {
            return recipe != null ? recipe.RuntimeRecipeHash : 0u;
        }

        private static void PublishFabricatorActiveCountBlackBox()
        {
            GlobalTelemetryBus.PublishModTelemetry(
                FabricatorTelemetryHash,
                FabricatorActiveCountHash,
                _activeFabricators.Count + s_activeFabricatorRegistryOverflowCount);
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

            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
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
            CacheScanLogSystem(Hecton8.Core.GlobalRegistry.ScanLogService);
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

            if (!EnsureRecipeUnlockMask())
            {
                BuildFailClosedRecipeCacheSnapshot();
                return;
            }

            IDataVault vault = _dataVault;
            if (vault == null ||
                !vault.TryAcquireMutationGuard(FabricatorUnlockedRecipesMutationGuardMask))
            {
                RecordFabricatorVaultFailure(
                    BufferID.ShinobuFabricatorUnlockedRecipes,
                    _unlockedRecipesHandle.Generation,
                    FabricatorVaultFailureAcquire,
                    MaxUnlockedRecipeWords);
                BuildFailClosedRecipeCacheSnapshot();
                return;
            }

            bool cacheBuilt = false;
            try
            {
                if (TryReadFabricatorBuffer(in _unlockedRecipesHandle, MaxUnlockedRecipeWords, out NativeArray<ulong>.ReadOnly unlockedRecipes))
                {
                    RebuildRecipeCacheFromUnlockMask(unlockedRecipes);
                    _recipeCacheDirty = false;
                    cacheBuilt = true;
                }
            }
            finally
            {
                vault.ReleaseMutationGuard(FabricatorUnlockedRecipesMutationGuardMask);
            }

            if (!cacheBuilt)
            {
                _unlockMaskDirty = true;
                RecordFabricatorVaultFailure(
                    BufferID.ShinobuFabricatorUnlockedRecipes,
                    _unlockedRecipesHandle.Generation,
                    FabricatorVaultFailureEnsure,
                    MaxUnlockedRecipeWords);
                BuildFailClosedRecipeCacheSnapshot();
            }
        }

        private void RebuildRecipeCacheFromUnlockMask(NativeArray<ulong>.ReadOnly unlockedRecipes)
        {
            _visibleRecipes.Clear();
            _lockedRecipeCount = 0;
            _overflowRecipeCount = 0;

            if (availableRecipes != null)
            {
                for (int i = 0; i < availableRecipes.Count; i++)
                {
                    AppendRecipeToCache(availableRecipes[i], i, unlockedRecipes);
                }
            }

            int runtimeRecipeCount = ModRecipeRegistry.Count;
            int unlockIndex = availableRecipes != null ? availableRecipes.Count : 0;
            for (int i = 0; i < runtimeRecipeCount; i++)
            {
                RecipeData recipe = ModRecipeRegistry.GetAt(i);
                if (recipe == null || ContainsAuthoredRecipeReference(recipe))
                    continue;

                AppendRecipeToCache(recipe, unlockIndex++, unlockedRecipes);
            }
        }

        private bool EnsureRecipeUnlockMask()
        {
            RefreshScanLogRevision();
            if (!_unlockMaskDirty)
                return true;

            if (!TryAcquireFabricatorWrite(
                    ref _unlockedRecipesHandle,
                    BufferID.ShinobuFabricatorUnlockedRecipes,
                    MaxUnlockedRecipeWords,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<ulong> unlockedRecipes,
                    out IDataVault lockedVault))
            {
                return false;
            }

            try
            {
                for (int wordIndex = 0; wordIndex < unlockedRecipes.Length; wordIndex++)
                    unlockedRecipes[wordIndex] = 0UL;

                int unlockIndex = 0;
                if (availableRecipes != null)
                {
                    for (int i = 0; i < availableRecipes.Count && unlockIndex < MaxRecipeCacheEntries; i++)
                        WriteRecipeUnlockBit(availableRecipes[i], unlockIndex++, unlockedRecipes);
                }

                int runtimeRecipeCount = ModRecipeRegistry.Count;
                for (int i = 0; i < runtimeRecipeCount && unlockIndex < MaxRecipeCacheEntries; i++)
                {
                    RecipeData recipe = ModRecipeRegistry.GetAt(i);
                    if (recipe == null || ContainsAuthoredRecipeReference(recipe))
                        continue;

                    WriteRecipeUnlockBit(recipe, unlockIndex++, unlockedRecipes);
                }

                _unlockMaskDirty = false;
                return true;
            }
            finally
            {
                ReleaseFabricatorWrite(lockedVault, in _unlockedRecipesHandle);
            }
        }

        private void WriteRecipeUnlockBit(RecipeData recipe, int unlockIndex, NativeArray<ulong> unlockedRecipes)
        {
            if (recipe == null || !unlockedRecipes.IsCreated)
                return;

            int wordIndex = unlockIndex >> RecipeUnlockWordShift;
            if (wordIndex < 0 || wordIndex >= unlockedRecipes.Length)
                return;

            if (recipe.IsUnlocked(_scanLogSystem))
                unlockedRecipes[wordIndex] = unlockedRecipes[wordIndex] | (1UL << (unlockIndex & RecipeUnlockBitMask));
        }

        private bool TryResolveRecipeUnlockIndex(RecipeData recipe, out int unlockIndex, out bool foundRecipe)
        {
            unlockIndex = -1;
            foundRecipe = false;
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
                        foundRecipe = true;
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
                    foundRecipe = true;
                    return IsUnlockIndexInRange(unlockIndex);
                }

                cursor++;
            }

            return false;
        }

        private static bool IsUnlockIndexInRange(int unlockIndex)
        {
            return unlockIndex >= 0 && unlockIndex < MaxRecipeCacheEntries;
        }

        private bool IsRecipeUnlockBitSet(int unlockIndex)
        {
            if (!IsUnlockIndexInRange(unlockIndex))
            {
                return false;
            }

            IDataVault vault = _dataVault;
            if (vault == null ||
                !vault.TryAcquireMutationGuard(FabricatorUnlockedRecipesMutationGuardMask))
            {
                return false;
            }

            try
            {
                if (!TryReadFabricatorBuffer(in _unlockedRecipesHandle, MaxUnlockedRecipeWords, out NativeArray<ulong>.ReadOnly unlockedRecipes))
                    return false;

                int wordIndex = unlockIndex >> RecipeUnlockWordShift;
                return (unlockedRecipes[wordIndex] & (1UL << (unlockIndex & RecipeUnlockBitMask))) != 0UL;
            }
            finally
            {
                vault.ReleaseMutationGuard(FabricatorUnlockedRecipesMutationGuardMask);
            }
        }

        private static ulong FabricatorMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 63);
        }

        private static bool IsRecipeUnlockBitSet(int unlockIndex, NativeArray<ulong>.ReadOnly unlockedRecipes)
        {
            if (!IsUnlockIndexInRange(unlockIndex) || !unlockedRecipes.IsCreated)
                return false;

            int wordIndex = unlockIndex >> RecipeUnlockWordShift;
            if (wordIndex < 0 || wordIndex >= unlockedRecipes.Length)
                return false;

            return (unlockedRecipes[wordIndex] & (1UL << (unlockIndex & RecipeUnlockBitMask))) != 0UL;
        }

        private bool IsRecipeUnlocked(RecipeData recipe)
        {
            if (recipe == null)
                return false;

            bool hasUnlockIndex = TryResolveRecipeUnlockIndex(recipe, out int unlockIndex, out _);
            if (!_unlockMaskDirty && hasUnlockIndex)
            {
                return IsRecipeUnlockBitSet(unlockIndex);
            }

            return false;
        }

        private void AppendRecipeToCache(RecipeData recipe, int unlockIndex, NativeArray<ulong>.ReadOnly unlockedRecipes)
        {
            if (recipe == null)
                return;

            if (!IsUnlockIndexInRange(unlockIndex) ||
                _visibleRecipes.Count + _lockedRecipeCount + _overflowRecipeCount >= MaxRecipeCacheEntries)
            {
                _overflowRecipeCount++;
                return;
            }

            if (!_unlockMaskDirty && IsRecipeUnlockBitSet(unlockIndex, unlockedRecipes))
                _visibleRecipes.Add(recipe);
            else
                _lockedRecipeCount++;
        }

        private void BuildFailClosedRecipeCacheSnapshot()
        {
            _visibleRecipes.Clear();
            int totalRecipeCount = CountAuthoredRecipeReferencesCold();
            _lockedRecipeCount = math.min(totalRecipeCount, MaxRecipeCacheEntries);
            _overflowRecipeCount = math.max(0, totalRecipeCount - MaxRecipeCacheEntries);
            _recipeCacheDirty = true;
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

        private int CountAuthoredRecipeReferencesCold()
        {
            int count = 0;
            if (availableRecipes != null)
            {
                for (int i = 0; i < availableRecipes.Count; i++)
                {
                    if (availableRecipes[i] != null)
                        count++;
                }
            }

            int runtimeRecipeCount = ModRecipeRegistry.Count;
            for (int i = 0; i < runtimeRecipeCount; i++)
            {
                RecipeData runtimeRecipe = ModRecipeRegistry.GetAt(i);
                if (runtimeRecipe != null && !ContainsAuthoredRecipeReference(runtimeRecipe))
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Handles deferred mod registry events that affect available recipes.
        /// </summary>
        /// <param name="payload">Unmanaged mod registry payload.</param>
        private void HandleModRegistryEvent(in ModRegistryEventPayload payload)
        {
            if ((ModRegistryEventType)payload.EventType != ModRegistryEventType.RecipeRegistryChanged)
                return;

            MarkRecipeCacheDirty();
            RebuildAssemblySourceCacheCold();
            EnsureRecipeCache();
        }

        private ModRegistryEventAdapter GetModRegistryEventAdapter()
        {
            if (_modRegistryEventAdapter == null)
                _modRegistryEventAdapter = new ModRegistryEventAdapter(this); // COLD ALLOC: ModRegistryEventAdapter[1] - internal mod registry invalidation listener bridge - owner: Fabricator

            return _modRegistryEventAdapter;
        }

        private sealed class ModRegistryEventAdapter : IModRegistryEventListener
        {
            private readonly Fabricator _owner;

            public ModRegistryEventAdapter(Fabricator owner)
            {
                _owner = owner;
            }

            void IModRegistryEventListener.OnModRegistryEvent(in ModRegistryEventPayload payload)
            {
                _owner.HandleModRegistryEvent(in payload);
            }
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

        internal static int ActiveFabricatorCount => _activeFabricators.Count;

        internal static Fabricator GetActiveFabricatorAt(int index)
        {
            return index >= 0 && index < _activeFabricators.Count ? _activeFabricators[index] : null;
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

            if (fabricator._activeFabricatorRegistered || fabricator._activeFabricatorRegistryOverflowed)
                return;

            for (int i = 0; i < _activeFabricators.Count; i++)
            {
                if (ReferenceEquals(_activeFabricators[i], fabricator))
                {
                    fabricator._activeFabricatorRegistered = true;
                    return;
                }
            }

            if (_activeFabricators.Count >= ActiveFabricatorRegistryCapacity)
            {
                fabricator._activeFabricatorRegistryOverflowed = true;
                s_activeFabricatorRegistryOverflowCount++;
                return;
            }

            _activeFabricators.Add(fabricator);
            fabricator._activeFabricatorRegistered = true;
        }

        private static void UnregisterActiveFabricator(Fabricator fabricator)
        {
            if (fabricator == null)
                return;

            if (fabricator._activeFabricatorRegistryOverflowed)
            {
                if (s_activeFabricatorRegistryOverflowCount > 0)
                    s_activeFabricatorRegistryOverflowCount--;

                fabricator._activeFabricatorRegistryOverflowed = false;
                return;
            }

            for (int i = _activeFabricators.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_activeFabricators[i], fabricator))
                {
                    _activeFabricators.RemoveAt(i);
                    break;
                }
            }

            fabricator._activeFabricatorRegistered = false;
        }

        private static float ResolveCraftPowerMultiplier(Fabricator owner, RecipeData recipe)
        {
            return owner != null
                ? Mathf.Max(1f, owner.CalculateRecipeInflationMultiplier(recipe))
                : 1f;
        }

        private float ResolveCraftPowerCost(RecipeData recipe)
        {
            return recipe != null && recipe.powerCost > 0f
                ? recipe.powerCost * _activeCraftPowerMultiplier
                : 0f;
        }

        // ----------------------------------------------------------
        //  EDITOR
        // ----------------------------------------------------------

        private void TryRegister()
        {
            if (_tickRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _tickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryRegisterLateFrame()
        {
            if (_lateFrameRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
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

        private void TryUnregisterLateFrame(bool clearPendingPresentation = true)
        {
            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = false;
            }

            if (!clearPendingPresentation)
                return;

            _pendingAssemblyVisualCommand = 0;
            _pendingAssemblyBeginRecipe = null;
            _pendingFabricationSparksDirty = false;
            _pendingErrorFeedbackDirty = false;
            _pendingAudioClip = null;
            _pendingAudioDirty = false;
            _pendingProceduralAudioPingCount = 0;
            _pendingProceduralAudioPing0 = default;
            _pendingProceduralAudioPing1 = default;
            _pendingProgressHapticDirty = false;
            _pendingProgressHaptic = default;
        }

        private void TryRegisterSparkLightTick()
        {
            if (_sparkLightTickRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _sparkLightTickRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterSparkLightTick(bool resetSleepState = true)
        {
            if (!_sparkLightTickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _sparkLightTickRegistered = false;
            if (resetSleepState)
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
                    _resourceScarcityDirector = currentService as IResourceScarcityReadModel;
                    break;
                case GlobalRegistryServiceSlot.PowerGrid:
                    _powerGridService = currentService as IPowerGridService;
                    break;
                case GlobalRegistryServiceSlot.PersistentWorldRegistry:
                    _persistentWorldRegistry = currentService as PersistentWorldRegistry;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                    if (_cachedPlayerContext != null)
                    {
                        _playerMovement = _cachedPlayerContext.PlayerMovement;
                        _playerTransform = _cachedPlayerContext.PlayerTransform;
                        _playerMovementLookupAttempted = _playerMovement != null;
                    }
                    else
                    {
                        _playerMovement = null;
                        _playerTransform = null;
                        _playerMovementLookupAttempted = false;
                    }
                    break;
                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioService(currentService as IAudioService);
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localizationManager = currentService as ILocalizationTextReadModel;
                    RebuildInteractText();
                    break;
                case GlobalRegistryServiceSlot.ScanLogRuntime:
                    CacheScanLogSystem(currentService as IScanLogService);
                    EnsureRecipeCache();
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    _dataVault = currentService as IDataVault;
                    ClearCraftingScratchHandles();
                    MarkRecipeCacheDirty();
                    EnsureCraftingScratchCold();
                    EnsureRecipeCache();
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    RebindFabricatorDispatcherTickRoutes(currentService);
                    break;
            }
        }

        private void CacheRegistryServicesCold()
        {
            _dataVault = GlobalRegistry.DataVault;
            _resourceScarcityDirector = GlobalRegistry.ResourceScarcityReadModel;
            _powerGridService = GlobalRegistry.PowerGrid;
            _persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
            _cachedPlayerContext = GlobalRegistry.Player;
            if (_playerMovement == null && _cachedPlayerContext != null)
            {
                _playerMovement = _cachedPlayerContext.PlayerMovement;
                if (_playerTransform == null)
                    _playerTransform = _cachedPlayerContext.PlayerTransform;
            }
            CacheAudioService(GlobalRegistry.Audio);
            _localizationManager = Hecton8.Core.GlobalRegistry.LocalizationText;
            CacheScanLogSystem(Hecton8.Core.GlobalRegistry.ScanLogService);
        }

        private void CacheAudioService(IAudioService audioService)
        {
            _audioService = IsAudioServiceUsable(audioService) ? audioService : null;
        }

        private IAudioService ResolveAudioService()
        {
            IAudioService audioService = _audioService;
            if (IsAudioServiceUsable(audioService))
                return audioService;

            ClearCachedAudioService();
            return null;
        }

        private void ClearCachedAudioService()
        {
            _audioService = null;
        }

        private static bool IsAudioServiceUsable(IAudioService audioService)
        {
            if (audioService == null || !audioService.IsInitialized)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
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

        private void RebindFabricatorDispatcherTickRoutes(object currentService)
        {
            TryUnregisterSparkLightTick(resetSleepState: false);
            TryUnregisterLateFrame(clearPendingPresentation: false);
            TryUnregister();

            if (currentService == null || !isActiveAndEnabled)
                return;

            TryRegister();
            TryRegisterLateFrame();
            TryRegisterSparkLightTick();
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
