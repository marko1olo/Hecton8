// ============================================================================
// HECTON-8 — SaveData.cs
// Glavnyy konteyner sohraneniya. Pattern DTO (Data Transfer Object).
//
// VSE dannye sohraneniya — zdes. Odin obekt → odna serializatsiya.
// Native binary save codecs serialize this class field-by-field.
//
// DIZAYN-REShENIYa:
//   • [Serializable] struct dlya vlozhennyh DTO — minimum heap-allokatsiy.
//   • Primitivnye tipy vmesto Vector3/Quaternion — binary compatibility
//     i portiruemost (JSON, binary, XML).
//   • string ID vmesto int InstanceID — stabilnost mezhdu sessiyami.
//   • Versionirovanie: pole version dlya migratsii dannyh.
//   • Pre-allocated massivy vmesto List — kontrol razmera.
//
// RASShIRENIE:
//   Dobavlyay novye DTO kak polya SaveData. Starye seyvy poluchat
//   defoltnye znacheniya dlya novyh poley obrabatyvayutsya migratsiey i defoltnymi initsializatorami.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory.Layout;
using Hecton8.Narrative;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.SaveSystem
{
    /// <summary>
    /// Kornevoy konteyner vseh dannyh sohraneniya.
    /// Odin ekzemplyar = odna polnaya kopiya igrovogo sostoyaniya.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        // ─────────────────────── Metadata ────────────────────────

        /// <summary>Versiya formata. Inkrementiruetsya pri izmenenii struktury DTO.</summary>
        public int version = CurrentVersion;

        /// <summary>Low 64 bits of the contract version hash used when this save was written.</summary>
        public ulong contractVersionHashLo = HectonContractVersion.HashLo;

        /// <summary>High 64 bits of the contract version hash used when this save was written.</summary>
        public ulong contractVersionHashHi = HectonContractVersion.HashHi;

        /// <summary>Vremennaya metka sohraneniya (ISO 8601).</summary>
        public string timestamp;

        /// <summary>Obschee vremya igry v sekundah.</summary>
        public double totalPlayTime;

        /// <summary>Save version that first persisted delayed hazard zone toxicity dose state.</summary>
        public const int HazardZoneRuntimePersistenceVersion = 74;
        public const int Atlas6LiabilityPersistenceVersion = 75;
        public const int VoxelDeltaPersistenceVersion = 76;
        public const int VoxelDeltaDenseCellFlagsPersistenceVersion = 77;
        public const int PlayerHealthPersistenceVersion = 78;
        public const int ResourceRecyclerModulePersistenceVersion = 79;
        public const int StorageCrateModulePersistenceVersion = 80;
        public const int FabricatorPendingOutputPersistenceVersion = 81;
        public const int CultivationSeedHashPersistenceVersion = 82;
        public const int ProceduralTerrainIdentityPersistenceVersion = 83;
        public const int CelestialLightPhasePersistenceVersion = 84;
        public const int ProceduralTerrainIdentityContractPersistenceVersion = 85;
        public const float CelestialLightTimeOfDayDefault = 0.25f;
        public const float HazardZoneMaxPersistedToxicityDose = 64f;
        public const float HazardZoneToxicityDamageDoseThreshold = 1f;
        public const float HazardZoneMaxPersistedToxicityPulseSeconds = 0.5f;
        public const float PlayerHealthDefault = 100f;
        public const float PlayerEnvironmentTemperatureDefault = 20f;
        public const float Atlas6LiabilityMaxTrackedSectorYield = 1000000f;
        public const float Atlas6LiabilityMaxBiomatterExposure = 100f;
        public const int RadiationGridPersistenceVersion = 68;
        public const int FirstHourDtoLockPersistenceVersion = 72;
        public const float PlayerKinematicVelocityHardCapMetersPerSecond = 80f;
        public const float PlayerKinematicVelocityHardCapSq =
            PlayerKinematicVelocityHardCapMetersPerSecond * PlayerKinematicVelocityHardCapMetersPerSecond;
        public const float PlayerStatsNitrogenBuildUpHardCap = 160f;
        public const byte PlayerInjuryBleedingFlag = 0x01;
        public const byte PlayerInjuryFractureFlag = 0x02;
        public const byte PlayerInjurySupportedFlagMask = PlayerInjuryBleedingFlag | PlayerInjuryFractureFlag;
        public const byte PlayerLastDeathCauseMaxKnown = (byte)Hecton8.Gameplay.SurvivalDeathCause.IntegrityFailure;
        public const ushort InventoryDefaultQualityMilli = 1000;
        public const byte InventoryItemGeneticsSupportedFlagsMask = 0x0F;
        public const int InventoryShadowPayloadMaxBytes = 16 * 1024;
        internal const byte ModuleFailureModeNone = 0;
        internal const byte ModuleFailureModeMaxKnown = 3;
        public const uint InventoryShadowPayloadHashSeed = 2166136261u;
        public const uint InventoryShadowPayloadHashPrime = 16777619u;

        /// <summary>Tekuschaya versiya formata. Ispolzuetsya dlya migratsii.</summary>
        public const int CurrentVersion = ProceduralTerrainIdentityContractPersistenceVersion; // v85: terrain material/detail identity is persisted.

        internal static string SanitizePersistenceString(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        internal static void EnsureExactArrayCapacity<T>(ref T[] values, int capacity)
        {
            if (values != null && values.Length == capacity)
                return;

            T[] replacement = new T[capacity];
            if (values != null && values.Length > 0)
            {
                int copyCount = values.Length < capacity ? values.Length : capacity;
                Array.Copy(values, replacement, copyCount);
            }

            values = replacement;
        }

        // ─────────────────────── DTO Sections ────────────────────

        public PlayerStatsDTO playerStats;
        public PlayerKinematicStateDTO playerKinematicState;
        public InventoryDTO inventory;
        public InventoryShadowDTO inventoryShadow;
        [NonSerialized] internal byte[] inventoryShadowPayload;
        [NonSerialized] internal int inventoryShadowPayloadLength;
        [NonSerialized] internal uint inventoryShadowPayloadHash;
        [NonSerialized] internal bool hasInventoryShadowPayload;
        public WorldStateDTO worldState;
        public ProceduralWorldStateDTO proceduralWorldState;
        public ConstructionDTO construction;
        public ScanLogDTO scanLog;
        public BarterDTO barter;
        public FieldOperationLogDTO fieldOperations;
        public BeaconNetworkDTO beaconNetwork;
        public ExplorationMapDTO explorationMap;
        public PDALogbookDTO pdaLogbook;
        public PDAMarkerRegistryDTO pdaMarkers;
        public PDAContextualAdvisoryDTO pdaAdvisories;
        public ProceduralLoreStateDTO proceduralLore;
        public AchievementRegistryDTO achievements;
        public RunModifiersDTO runModifiers;
        public MetaCampaignDTO metaCampaign;
        public ResourceScarcityDTO resourceScarcity;
        public EnvironmentalStrainDTO environmentalStrain;
        public EcosystemStateDTO ecosystemState;
        public ProceduralTerrainIdentityDTO proceduralTerrainIdentity;
        public VoxelDeltaPersistenceDTO voxelDeltaPersistence;
        public ExternalScavengerSiteDTO[] externalScavengerSites;
        public HazardZoneRuntimeDTO hazardZones;

        /// <summary>Prochnost instrumentov (toolID → durability). v2.0 ENTERPRISE</summary>
        public Dictionary<string, float> toolDurabilityMap = new Dictionary<string, float>();

        /// <summary>Slomannye instrumenty (toolID → broken). v2.0 ENTERPRISE</summary>
        public Dictionary<string, bool> toolBrokenMap = new Dictionary<string, bool>();

        /// <summary>Legacy set of discovered biome IDs kept only for backward-compatible migration reads.</summary>
        public HashSet<int> discoveredBiomeIds;

        /// <summary>Packed discovery words for all 108 biomes. Two 64-bit words cover the current matrix.</summary>
        public long[] discoveredBiomeBitWords;

        /// <summary>Posledniy podtverzhdennyy otkrytyy biom dlya PDA i HUD.</summary>
        public int lastDiscoveredBiomeId = -1;

        /// <summary>Kolichestvo narrative-discovery zapisey, sohranennyh v narrativeDiscoveryIds.</summary>
        public int narrativeDiscoveryCount;

        /// <summary>Stabilnye narrative-discovery ID dlya pozdnih triggerov i povtornogo vhoda v stsenu.</summary>
        public string[] narrativeDiscoveryIds;

        /// <summary>Maksimalnyy dostignutyy narrative depth-tier.</summary>
        public int narrativeDepthTier;

        /// <summary>One-bit-per-trigger state for AUP narrative radius events. v62 LORE.</summary>
        public ulong narrativeAupTriggeredMask;

        /// <summary>Spisok ID obnaruzhennyh audiodnevnikov. v4.0 LORE</summary>
        public List<string> audioLogDiscoveredIds = new List<string>();

        /// <summary>Packed 1024-bit audio-log discovery mask. Exactly 128 bytes of flag payload. v62 LORE.</summary>
        public long[] audioLogDiscoveryBitWords;

        /// <summary>Number of partial encrypted audio-log recovery records persisted in the fixed hash arrays. v61 LORE.</summary>
        public int audioLogEncryptedFragmentCount;

        /// <summary>Stable audio-log hashes for partial encrypted recovery. v61 LORE.</summary>
        public uint[] audioLogEncryptedFragmentHashes;

        /// <summary>Recovered 4-bit masks for partial encrypted audio logs. v61 LORE.</summary>
        public uint[] audioLogEncryptedFragmentBits;

        /// <summary>Packed industrial-lore discovery words for the fixed 50-record archive bank.</summary>
        public long[] industrialLoreUnlockWords;

        /// <summary>Packed 1024-bit data-archaeology discovery mask. Exactly 128 bytes of flag payload. v64 DISCOVERY.</summary>
        public long[] dataArchaeologyDiscoveryBitWords;

        /// <summary>Number of partial archaeology scan records persisted in fixed arrays. v64 DISCOVERY.</summary>
        public int dataArchaeologyPartialScanCount;

        /// <summary>Stable archaeology hashes for partial scan progress. v64 DISCOVERY.</summary>
        public uint[] dataArchaeologyPartialScanHashes;

        /// <summary>Partial archaeology scan progress in permille. v64 DISCOVERY.</summary>
        public ushort[] dataArchaeologyPartialScanProgressPermille;

        /// <summary>Number of explicit data-archaeology scan-state records. v66 DISCOVERY.</summary>
        public int dataArchaeologyScanStateCount;

        /// <summary>Signed FNV/AUP hashes for explicit scanner state records. v66 DISCOVERY.</summary>
        public int[] dataArchaeologyScanStateKeys;

        /// <summary>Scanner state byte per hash: 0=Unscanned, 1=Scanning, 2=Scanned. v66 DISCOVERY.</summary>
        public byte[] dataArchaeologyScanStateValues;

        /// <summary>Aktivnye kvesty. v4.0 QUEST</summary>
        public List<string> questActiveIds = new List<string>();

        /// <summary>Zavershennye kvesty. v4.0 QUEST</summary>
        public List<string> questCompletedIds = new List<string>();

        /// <summary>Signal Atlas-6 kogda-libo obnaruzhen. v4.0 ATLAS</summary>
        public bool atlasSignalDetected;

        /// <summary>Taymer pulsa signala (dlya sohraneniya ritma). v4.0 ATLAS</summary>
        public float atlasSignalPulseTimer;

        /// <summary>Maksimalnaya raskrytaya stadiya pozdnego Atlas-manifestation. v4.10 ATLAS</summary>
        public int atlasSignalRevealStage;

        /// <summary>Ustanovlennye apgreydy skafandra. v4.1 UPGRADES</summary>
        public List<string> suitInstalledUpgradeIds = new List<string>();

        /// <summary>Razblokirovannye chertezhi apgreydov. v4.1 UPGRADES</summary>
        public List<string> suitUnlockedBlueprintIds = new List<string>();

        /// <summary>Slomannye, no ustanovlennye apgreydy skafandra. v33 WIPEOUT</summary>
        public List<string> suitBrokenUpgradeIds = new List<string>();

        /// <summary>Packed suit upgrade runtime mask. v65 UPGRADES.</summary>
        public ulong suitUpgradeMask;

        /// <summary>ÐÐºÑ‚Ð¸Ð²Ð½Ñ‹Ð¹ Ð¿Ñ€Ð¾Ñ„Ð¸Ð»ÑŒ ÑÐ°Ð¼Ð¾Ð²Ñ‹Ñ€Ð°Ð¶ÐµÐ½Ð¸Ñ Ð¸Ð³Ñ€Ð¾ÐºÐ°. v4.9 EXPRESSION</summary>
        public string playerExpressionProfileId = string.Empty;

        /// <summary>Status igroka s tochki zreniya Atlas-6. v4.2 ATLAS6</summary>
        public int atlas6PlayerStatus;

        /// <summary>Kolichestvo barter-tranzaktsiy s Atlas-6. v4.2 ATLAS6</summary>
        public int atlas6BarterCount;

        /// <summary>Konflikt direktiv byl aktivirovan. v4.2 ATLAS6</summary>
        public bool atlas6DirectiveConflictTriggered;

        /// <summary>Accumulated Xenon-Omega liability yield. v75 ATLAS6-LIABILITY</summary>
        public float atlas6LiabilitySectorXenonOmegaYield;

        /// <summary>Whether Atlas-6 disaster evidence is being carried/transmitted. v75 ATLAS6-LIABILITY</summary>
        public bool atlas6LiabilityHasDisasterEvidence;

        /// <summary>Number of persisted recovered worker tag hashes. v75 ATLAS6-LIABILITY</summary>
        public int atlas6LiabilityRecoveredWorkerTagCount;

        /// <summary>Recovered worker tag hashes for liability dedupe. v75 ATLAS6-LIABILITY</summary>
        public uint[] atlas6LiabilityRecoveredWorkerTagHashes;

        /// <summary>Corporate hostility accumulated by actuarial liability. v75 ATLAS6-LIABILITY</summary>
        public float atlas6LiabilityCorporateHostilityIndex;

        /// <summary>Corporate credit balance after ghost-PDA deductions. v75 ATLAS6-LIABILITY</summary>
        public float atlas6LiabilityCorporateCreditBalance;

        /// <summary>Extraction carrier state as ExtractionCarrierState int. v75 ATLAS6-LIABILITY</summary>
        public int atlas6LiabilityExtractionCarrierState;

        /// <summary>Persisted Xenon-Omega biomatter exposure. v75 ATLAS6-LIABILITY</summary>
        public float atlas6LiabilityBiomatterExposureLevel;

        /// <summary>Whether Haldane quarantine is already raised. v75 ATLAS6-LIABILITY</summary>
        public bool atlas6LiabilityHaldaneLockoutActive;

        /// <summary>Arendt pressure-seal integrity. v75 ATLAS6-LIABILITY</summary>
        public float atlas6LiabilityPressureSealIntegrity;

        /// <summary>Whether Arendt bulkhead lockdown is active. v75 ATLAS6-LIABILITY</summary>
        public bool atlas6LiabilityBulkheadLocked;

        /// <summary>Poluchennye korporativnye prikazy. v4.3 CORP</summary>
        public List<string> corporateReceivedOrderIds = new List<string>();

        /// <summary>Ozhidayuschie prikazy (ID). v4.3 CORP</summary>
        public List<string> corporatePendingOrderIds = new List<string>();

        /// <summary>Taymery ozhidayuschih prikazov (sekundy). v4.3 CORP</summary>
        public List<float> corporatePendingOrderTimers = new List<float>();

        /// <summary>Vremya sessii pervogo chasa (sekundy). v4.4 FIRSTHOUR</summary>
        public float firstHourSessionTime;

        /// <summary>Bitovaya maska vypolnennyh milestone pervogo chasa. v4.4 FIRSTHOUR</summary>
        public int firstHourMilestones;

        /// <summary>Bitovaya maska uzhe vydannyh first-hour guidance/reminder states. v4.11 FIRSTHOUR</summary>
        public int firstHourGuidanceFlags;

        /// <summary>Vybrannaya kontsovka. v4.5 ENDING</summary>
        public int endingChoice;

        /// <summary>Kontsovka zavershena. v4.5 ENDING</summary>
        public bool endingComplete;

        /// <summary>Uslovie kontsovki vypolneno (igrok u yadra). v4.5 ENDING</summary>
        public bool endingConditionMet;

        /// <summary>Aktivnye missii (MissionManager). v4.6 MISSIONS</summary>
        public List<string> missionActiveIds = new List<string>();

        /// <summary>Zavershennye missii (MissionManager). v4.6 MISSIONS</summary>
        public List<string> missionCompletedIds = new List<string>();

        /// <summary>LOD quality preset (0=Low, 1=Medium, 2=High). v4.7 LOD</summary>
        public int LODQualityPreset = 1; // Default: Medium

        /// <summary>Dynamic resolution scaling enabled. v4.8 LOD</summary>
        public bool DynamicResolutionEnabled = true; // Default: Enabled

        /// <summary>Cumulative player radiation dose persisted by RadiationHazardGrid. v68 RADIATION.</summary>
        public float radiationDose;

        /// <summary>Radiation grid AUP origin persisted as absolute doubles. v68 RADIATION.</summary>
        public double radiationGridOriginX;
        public double radiationGridOriginY;
        public double radiationGridOriginZ;

        /// <summary>True when a save carried an authoritative celestial/atmosphere time-of-day phase. v84 LIGHT.</summary>
        public bool celestialLightPhaseSerialized;

        /// <summary>Authoritative time-of-day phase restored by HectonAtmosphereManager. v84 LIGHT.</summary>
        public float celestialLightTimeOfDay01 = CelestialLightTimeOfDayDefault;

        /// <summary>Radiation grid cell size in meters. v68 RADIATION.</summary>
        public float radiationGridCellSizeMeters = RadiationGridDefaultCellSizeMeters;

        /// <summary>Sparse RLE byte count for quantized radiation grid payload. v68 RADIATION.</summary>
        public int radiationGridRleLength;

        /// <summary>Sparse RLE packets: ushort start, sbyte-equivalent byte value, ushort run. v68 RADIATION.</summary>
        public byte[] radiationGridRle;

        /// <summary>Number of persisted radioisotope thermal generators. v70 RTG.</summary>
        public int rtgDecayCount;

        /// <summary>Stable source ids for persisted RTGs. v70 RTG.</summary>
        public int[] rtgDecaySourceIds;

        /// <summary>Absolute H8 unscaled start times in seconds. v70 RTG.</summary>
        public double[] rtgStartTimesSeconds;

        /// <summary>Persisted RTG flags: active/dead/warned/reprocessed. v70 RTG.</summary>
        public byte[] rtgDecayFlags;

        /// <summary>Custom mod payload map persisted inside the official save file. v24 MODDING</summary>
        public Dictionary<string, string> CustomModData = new Dictionary<string, string>();

        // ═════════════════════════════════════════════════════════
        //  Factory — sozdanie novogo SaveData s metadannymi
        // ═════════════════════════════════════════════════════════

        public static SaveData CreateNew(float playTime)
        {
            return CreateNew((double)playTime);
        }

        public static SaveData CreateNew(double playTime)
        {
            var data = new SaveData();
            data.InitializeCore(playTime);
            data.InitializePlayerAndWorld();
            data.InitializeProgressionAndDiscovery();
            data.InitializeAtlas6AndCorporate();
            data.InitializeEnvironment();
            return data;
        }

        private void InitializeCore(double playTime)
        {
            version = CurrentVersion;
            contractVersionHashLo = HectonContractVersion.HashLo;
            contractVersionHashHi = HectonContractVersion.HashHi;
            timestamp = DateTime.Now.ToString("O");
            totalPlayTime = playTime;

            firstHourSessionTime = 0f;
            firstHourMilestones = 0;
            firstHourGuidanceFlags = 0;

            LODQualityPreset = 1; // Default: Medium
            DynamicResolutionEnabled = true; // Default: Enabled
        }

        private void InitializePlayerAndWorld()
        {
            playerStats = new PlayerStatsDTO
            {
                health = PlayerHealthDefault,
                environmentTemperature = PlayerEnvironmentTemperatureDefault
            };
            playerKinematicState = new PlayerKinematicStateDTO();
            inventory = new InventoryDTO();
            inventoryShadow = new InventoryShadowDTO();

            worldState = new WorldStateDTO();
            proceduralWorldState = new ProceduralWorldStateDTO();
            construction = ConstructionDTO.CreatePreallocated();

            runModifiers = new RunModifiersDTO
            {
                dailySeedId = string.Empty
            };
            metaCampaign = MetaCampaignDTO.CreateDefault();

            resourceScarcity = new ResourceScarcityDTO();
            environmentalStrain = new EnvironmentalStrainDTO();
            ecosystemState = new EcosystemStateDTO();
            proceduralTerrainIdentity = new ProceduralTerrainIdentityDTO();
            voxelDeltaPersistence = VoxelDeltaPersistenceDTO.CreateDefault();
            hazardZones = new HazardZoneRuntimeDTO();

            suitInstalledUpgradeIds = new List<string>();
            suitUnlockedBlueprintIds = new List<string>();
            suitBrokenUpgradeIds = new List<string>();
            suitUpgradeMask = 0UL;

            playerExpressionProfileId = string.Empty;
        }

        private void InitializeProgressionAndDiscovery()
        {
            scanLog = new ScanLogDTO();
            barter = new BarterDTO();
            fieldOperations = new FieldOperationLogDTO();
            beaconNetwork = new BeaconNetworkDTO();
            explorationMap = new ExplorationMapDTO();
            pdaLogbook = new PDALogbookDTO();
            pdaMarkers = new PDAMarkerRegistryDTO();
            pdaAdvisories = new PDAContextualAdvisoryDTO();
            proceduralLore = new ProceduralLoreStateDTO();
            achievements = new AchievementRegistryDTO();

            discoveredBiomeIds = null;
            discoveredBiomeBitWords = new long[BiomeDiscoveryBitMask.WordCount];
            lastDiscoveredBiomeId = -1;

            narrativeDiscoveryCount = 0;
            narrativeDiscoveryIds = new string[MaxNarrativeDiscoveries];
            narrativeDepthTier = 0;
            narrativeAupTriggeredMask = 0UL;

            audioLogDiscoveredIds = new List<string>();
            audioLogDiscoveryBitWords = new long[AudioLogDiscoveryBitMask.WordCount];
            audioLogEncryptedFragmentCount = 0;
            audioLogEncryptedFragmentHashes = new uint[MaxEncryptedAudioLogFragments];
            audioLogEncryptedFragmentBits = new uint[MaxEncryptedAudioLogFragments];

            industrialLoreUnlockWords = new long[IndustrialLoreBitMask.WordCount];

            dataArchaeologyDiscoveryBitWords = new long[MaxDataArchaeologyDiscoveryWords];
            dataArchaeologyPartialScanCount = 0;
            dataArchaeologyPartialScanHashes = new uint[MaxDataArchaeologyPartialScans];
            dataArchaeologyPartialScanProgressPermille = new ushort[MaxDataArchaeologyPartialScans];
            dataArchaeologyScanStateCount = 0;
            dataArchaeologyScanStateKeys = new int[MaxDataArchaeologyScanStates];
            dataArchaeologyScanStateValues = new byte[MaxDataArchaeologyScanStates];

            questActiveIds = new List<string>();
            questCompletedIds = new List<string>();

            endingChoice = 0;
            endingComplete = false;
            endingConditionMet = false;

            missionActiveIds = new List<string>();
            missionCompletedIds = new List<string>();
        }

        private void InitializeAtlas6AndCorporate()
        {
            atlasSignalDetected = false;
            atlasSignalPulseTimer = 0f;
            atlasSignalRevealStage = 0;

            atlas6PlayerStatus = 0;
            atlas6BarterCount = 0;
            atlas6DirectiveConflictTriggered = false;

            atlas6LiabilitySectorXenonOmegaYield = 0f;
            atlas6LiabilityHasDisasterEvidence = false;
            atlas6LiabilityRecoveredWorkerTagCount = 0;
            atlas6LiabilityRecoveredWorkerTagHashes = new uint[MaxAtlas6LiabilityWorkerTags];
            atlas6LiabilityCorporateHostilityIndex = 0f;
            atlas6LiabilityCorporateCreditBalance = 5000f;
            atlas6LiabilityExtractionCarrierState = 0;
            atlas6LiabilityBiomatterExposureLevel = 0f;
            atlas6LiabilityHaldaneLockoutActive = false;
            atlas6LiabilityPressureSealIntegrity = 1f;
            atlas6LiabilityBulkheadLocked = false;

            corporateReceivedOrderIds = new List<string>();
            corporatePendingOrderIds = new List<string>();
            corporatePendingOrderTimers = new List<float>();
        }

        private void InitializeEnvironment()
        {
            radiationDose = 0f;
            radiationGridOriginX = 0d;
            radiationGridOriginY = 0d;
            radiationGridOriginZ = 0d;
            radiationGridCellSizeMeters = RadiationGridDefaultCellSizeMeters;
            radiationGridRleLength = 0;
            radiationGridRle = new byte[RadiationGridRleMaxBytes];

            celestialLightPhaseSerialized = false;
            celestialLightTimeOfDay01 = CelestialLightTimeOfDayDefault;

            rtgDecayCount = 0;
            rtgDecaySourceIds = new int[MaxRtgDecayRecords];
            rtgStartTimesSeconds = new double[MaxRtgDecayRecords];
            rtgDecayFlags = new byte[MaxRtgDecayRecords];

            CustomModData = new Dictionary<string, string>();
        }

        public void RefreshFirstHourDtoMirrors()
        {
            SaveDataPlayerSurvivalSanitizer.SanitizePlayerStats(ref playerStats);
            playerKinematicState = PlayerKinematicStateDTO.FromPlayerStats(in playerStats);
            SaveDataPlayerSurvivalSanitizer.SanitizePlayerKinematicState(ref playerKinematicState);
            SaveDataInventorySanitizer.ResolveInventoryShadowPayloadMetadata(
                this,
                out int inventoryShadowPayloadLength,
                out uint inventoryShadowPayloadHash);
            inventoryShadow = SaveDataInventorySanitizer.BuildInventoryShadow(
                in inventory,
                inventoryShadowPayloadLength,
                inventoryShadowPayloadHash,
                inventoryShadowPayloadLength > 0);
            construction.RefreshHabitatFloodStateMirrors();
        }

        public const int MaxNarrativeDiscoveries = 128;

        /// <summary>Maximum persisted partial encrypted audio-log recovery records. v61 LORE.</summary>
        public const int MaxEncryptedAudioLogFragments = 32;

        /// <summary>Maximum persisted partial archaeology scan records. v64 DISCOVERY.</summary>
        public const int MaxDataArchaeologyPartialScans = 256;

        /// <summary>Maximum explicit scanner state records. v66 DISCOVERY.</summary>
        public const int MaxDataArchaeologyScanStates = 1024;

        /// <summary>Maximum persisted Atlas-6 liability worker-tag hashes. v75 ATLAS6-LIABILITY.</summary>
        public const int MaxAtlas6LiabilityWorkerTags = 1024;

        /// <summary>Persisted 1024-bit archaeology discovery mask word count. v64 DISCOVERY.</summary>
        public const int MaxDataArchaeologyDiscoveryWords = MaxDataArchaeologyScanStates / 64;

        /// <summary>Persisted radiation grid resolution per axis. v68 RADIATION.</summary>
        public const int RadiationGridResolution = 32;

        /// <summary>Persisted radiation grid cell count. v68 RADIATION.</summary>
        public const int RadiationGridCellCount = RadiationGridResolution * RadiationGridResolution * RadiationGridResolution;

        /// <summary>Default persisted radiation grid cell size in meters. v68 RADIATION.</summary>
        public const float RadiationGridDefaultCellSizeMeters = 4f;

        /// <summary>Minimum persisted radiation grid cell size in meters. v68 RADIATION.</summary>
        public const float RadiationGridMinCellSizeMeters = 0.5f;

        /// <summary>Maximum persisted radiation grid cell size in meters. v68 RADIATION.</summary>
        public const float RadiationGridMaxCellSizeMeters = 1000f;

        /// <summary>Persisted sparse RLE packet byte width: ushort start, byte value, ushort run. v68 RADIATION.</summary>
        public const int RadiationGridRlePacketSizeBytes = sizeof(ushort) + sizeof(byte) + sizeof(ushort);

        /// <summary>Maximum sparse RLE radiation payload. v68 RADIATION.</summary>
        public const int RadiationGridRleMaxBytes = RadiationGridCellCount * RadiationGridRlePacketSizeBytes;

        /// <summary>Maximum persisted RTG decay records. v70 RTG.</summary>
        public const int MaxRtgDecayRecords = 128;

        /// <summary>Persisted RTG flags: active/dead/warned/reprocessed. v70 RTG.</summary>
        public const byte RtgDecayPersistedFlagMask = 0x0F;

        /// <summary>Maximum persisted external scavenger sites. Runtime capacity is clamped to 16.</summary>
        public const int MaxExternalScavengerSites = 16;

        /// <summary>Maximum legacy tool durability entries. Matches ToolDurabilitySystem fixed slots.</summary>
        public const int MaxToolDurabilityRecords = 32;

        /// <summary>Maximum legacy discovered biome IDs accepted before packed bitmask migration.</summary>
        public const int MaxLegacyDiscoveredBiomeIds = BiomeDiscoveryBitMask.MaxBiomeId - BiomeDiscoveryBitMask.MinBiomeId + 1;

        /// <summary>Maximum legacy discovered audio-log IDs accepted before packed bitmask migration.</summary>
        public const int MaxLegacyAudioLogDiscoveredIds = AudioLogDiscoveryBitMask.MaxLogCount;

        /// <summary>Maximum legacy quest IDs accepted before packed quest-state restoration.</summary>
        public const int MaxLegacyQuestIds = 1024;

        /// <summary>Maximum persisted suit upgrade IDs in each legacy suit list.</summary>
        public const int MaxSuitUpgradeIds = 32;

        /// <summary>Persisted suit upgrade bits supported by current suit resolver.</summary>
        public const ulong SuitUpgradeSupportedMask = Hecton8.Gameplay.SuitUpgradeResolver.SupportedMask;

        /// <summary>Maximum persisted corporate order IDs and pending-order timers.</summary>
        public const int MaxCorporateOrderIds = 16;

        /// <summary>Maximum persisted mission IDs in each mission facade list.</summary>
        public const int MaxMissionIds = 32;

        /// <summary>Maximum custom mod key/value pairs persisted in the root compatibility map.</summary>
        public const int MaxCustomModDataEntries = 64;

        public void EnsureRtgDecayCapacity()
        {
            EnsureExactArrayCapacity(ref rtgDecaySourceIds, MaxRtgDecayRecords);
            EnsureExactArrayCapacity(ref rtgStartTimesSeconds, MaxRtgDecayRecords);
            EnsureExactArrayCapacity(ref rtgDecayFlags, MaxRtgDecayRecords);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  PlayerStatsDTO — sostoyanie skafandra i pozitsiya igroka
    // ══════════════════════════════════════════════════════════════════

    [Serializable]
    public struct PlayerStatsDTO
    {
        public float oxygen;
        public float energy;
        public float integrity;
        public float health;
        public float weight;
        public float hunger;
        public float thirst;
        public double currentLifeDurationSeconds;
        public double currentLifePeakDepthMeters;
        public float currentLifeLowestOxygenNormalized;
        public float currentLifeLowestEnergyNormalized;
        public float currentLifeLowestIntegrityNormalized;
        public byte injuryFlags;
        public float bleedingSecondsRemaining;
        public float bleedingDamagePerSecond;
        public float bleedingSeverity01;
        public float fractureSecondsRemaining;
        public float fracturePenalty01;
        public float environmentTemperature;
        public float coldStressSeverity01;
        public float heatStressSeverity01;
        public float nitrogenBuildUp;
        public bool hasLastDeathRecord;
        public byte lastDeathCause;
        public float lastDeathPosX;
        public float lastDeathPosY;
        public float lastDeathPosZ;
        public double lastDeathLifeDurationSeconds;
        public double lastDeathPeakDepthMeters;
        public float lastDeathLowestOxygenNormalized;
        public float lastDeathLowestEnergyNormalized;
        public float lastDeathLowestIntegrityNormalized;

        public float posX;
        public float posY;
        public float posZ;

        public float rotX;
        public float rotY;
        public float rotZ;
        public float rotW;

        public float velX;
        public float velY;
        public float velZ;

        public Vector3 GetPosition() => new Vector3(posX, posY, posZ);
        public Quaternion GetRotation() => new Quaternion(rotX, rotY, rotZ, rotW);
        public Vector3 GetVelocity() => new Vector3(velX, velY, velZ);

        public void SetPosition(Vector3 pos)
        {
            posX = pos.x; posY = pos.y; posZ = pos.z;
        }

        public void SetRotation(Quaternion rot)
        {
            rotX = rot.x; rotY = rot.y; rotZ = rot.z; rotW = rot.w;
        }

        public void SetVelocity(Vector3 velocity)
        {
            velX = velocity.x;
            velY = velocity.y;
            velZ = velocity.z;
        }

        public Vector3 GetLastDeathPosition() => new Vector3(lastDeathPosX, lastDeathPosY, lastDeathPosZ);

        public void SetLastDeathPosition(Vector3 pos)
        {
            lastDeathPosX = pos.x;
            lastDeathPosY = pos.y;
            lastDeathPosZ = pos.z;
        }
    }

    [Serializable]
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct PlayerKinematicStateDTO
    {
        [FieldOffset(0)] public float posX;
        [FieldOffset(4)] public float posY;
        [FieldOffset(8)] public float posZ;
        [FieldOffset(12)] public float rotX;
        [FieldOffset(16)] public float rotY;
        [FieldOffset(20)] public float rotZ;
        [FieldOffset(24)] public float rotW;
        [FieldOffset(28)] public float velX;
        [FieldOffset(32)] public float velY;
        [FieldOffset(36)] public float velZ;
        [FieldOffset(40)] public int flags;
        [FieldOffset(44)] private int _pad0;

        public static PlayerKinematicStateDTO FromPlayerStats(in PlayerStatsDTO stats)
        {
            PlayerKinematicStateDTO dto = default;
            dto.posX = stats.posX;
            dto.posY = stats.posY;
            dto.posZ = stats.posZ;
            dto.rotX = stats.rotX;
            dto.rotY = stats.rotY;
            dto.rotZ = stats.rotZ;
            dto.rotW = stats.rotW;
            dto.velX = stats.velX;
            dto.velY = stats.velY;
            dto.velZ = stats.velZ;
            dto.flags = 1;
            return dto;
        }

        public void ApplyTo(ref PlayerStatsDTO stats)
        {
            stats.posX = posX;
            stats.posY = posY;
            stats.posZ = posZ;
            stats.rotX = rotX;
            stats.rotY = rotY;
            stats.rotZ = rotZ;
            stats.rotW = rotW;
            stats.velX = velX;
            stats.velY = velY;
            stats.velZ = velZ;
        }
    }

    [Serializable]
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ExternalScavengerSiteDTO
    {
        [FieldOffset(0)] public int chunkX;
        [FieldOffset(4)] public int chunkY;
        [FieldOffset(8)] public int chunkZ;
        [FieldOffset(12)] public sbyte offsetX;
        [FieldOffset(13)] public sbyte offsetY;
        [FieldOffset(14)] public sbyte offsetZ;
        [FieldOffset(15)] public byte quantizedRadius;
        [FieldOffset(16)] public float remainingTime;
        [FieldOffset(20)] public uint seed;
        [FieldOffset(24)] private long _pad0;

        public bool IsValid()
        {
            return remainingTime > 0f;
        }

        internal static bool TrySanitizeForPersistence(
            in ExternalScavengerSiteDTO value,
            out ExternalScavengerSiteDTO sanitized)
        {
            sanitized = default;
            if (float.IsNaN(value.remainingTime) || float.IsInfinity(value.remainingTime) || value.remainingTime <= 0f)
                return false;

            sanitized.chunkX = value.chunkX;
            sanitized.chunkY = value.chunkY;
            sanitized.chunkZ = value.chunkZ;
            sanitized.offsetX = value.offsetX;
            sanitized.offsetY = value.offsetY;
            sanitized.offsetZ = value.offsetZ;
            sanitized.quantizedRadius = value.quantizedRadius;
            sanitized.remainingTime = value.remainingTime;
            sanitized.seed = value.seed;
            sanitized._pad0 = 0L;
            return true;
        }

        internal static bool PersistenceEquals(
            in ExternalScavengerSiteDTO left,
            in ExternalScavengerSiteDTO right)
        {
            return left.chunkX == right.chunkX &&
                   left.chunkY == right.chunkY &&
                   left.chunkZ == right.chunkZ &&
                   left.offsetX == right.offsetX &&
                   left.offsetY == right.offsetY &&
                   left.offsetZ == right.offsetZ &&
                   left.quantizedRadius == right.quantizedRadius &&
                   left.remainingTime == right.remainingTime &&
                   left.seed == right.seed &&
                   left._pad0 == right._pad0;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  InventoryDTO
    // ══════════════════════════════════════════════════════════════════

    [Serializable]
    public struct InventoryDTO
    {
        public int cellCount;
        public int[] itemHashIds;
        public uint[] packedCellCoordinates;
        public ushort[] stackCounts;
        public ushort[] itemStateFlags;
        public byte[] itemGeneticsWords;
        public ushort[] qualityMilli;
        public uint[] lastUpdateUnixSeconds;
        public byte[] itemDurabilityRle;
        public int itemDurabilityRleLength;
        public float totalWeight;
        public int gridColumns;
        public int gridRows;

        public const int MaxCells = 128;
        public const int MaxDurabilityRleBytes = MaxCells * 2;

        public void EnsureCapacity()
        {
            SaveData.EnsureExactArrayCapacity(ref itemHashIds, MaxCells);
            SaveData.EnsureExactArrayCapacity(ref packedCellCoordinates, MaxCells);
            SaveData.EnsureExactArrayCapacity(ref stackCounts, MaxCells);
            SaveData.EnsureExactArrayCapacity(ref itemStateFlags, MaxCells);
            SaveData.EnsureExactArrayCapacity(ref itemGeneticsWords, MaxCells);
            SaveData.EnsureExactArrayCapacity(ref qualityMilli, MaxCells);
            SaveData.EnsureExactArrayCapacity(ref lastUpdateUnixSeconds, MaxCells);
            SaveData.EnsureExactArrayCapacity(ref itemDurabilityRle, MaxDurabilityRleBytes);
        }

        public static uint PackCellCoordinate(int x, int y)
        {
            unchecked
            {
                return ((uint)(ushort)x) | ((uint)(ushort)y << 16);
            }
        }

        public static int UnpackCellX(uint packedCellCoordinate)
        {
            return (ushort)(packedCellCoordinate & 0xFFFFu);
        }

        public static int UnpackCellY(uint packedCellCoordinate)
        {
            return (ushort)(packedCellCoordinate >> 16);
        }
    }

    [Serializable]
    public struct InventoryCellDTO
    {
        public int x;
        public int y;
        public string itemId;
        public int stackCount;
    }

    // ══════════════════════════════════════════════════════════════════
    //  WorldStateDTO
    // ══════════════════════════════════════════════════════════════════

    [Serializable]
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct InventoryShadowDTO
    {
        public const byte FlagHasPayload = 1 << 0;
        public const byte SchemaVersion = 1;

        [FieldOffset(0)] public int cellCount;
        [FieldOffset(4)] public int payloadLength;
        [FieldOffset(8)] public uint payloadHash;
        [FieldOffset(12)] public int gridColumns;
        [FieldOffset(16)] public int gridRows;
        [FieldOffset(20)] public float totalWeight;
        [FieldOffset(24)] public byte flags;
        [FieldOffset(25)] public byte schemaVersion;
        [FieldOffset(26)] public ushort reserved0;
        [FieldOffset(28)] private int _pad0;

        public static InventoryShadowDTO FromInventory(
            in InventoryDTO inventory,
            int shadowPayloadLength,
            uint shadowPayloadHash,
            bool hasShadowPayload)
        {
            InventoryShadowDTO dto = default;
            dto.cellCount = Math.Clamp(inventory.cellCount, 0, InventoryDTO.MaxCells);
            dto.payloadLength = hasShadowPayload && shadowPayloadLength > 0 ? shadowPayloadLength : 0;
            dto.payloadHash = dto.payloadLength > 0 ? shadowPayloadHash : 0u;
            dto.gridColumns = inventory.gridColumns;
            dto.gridRows = inventory.gridRows;
            dto.totalWeight = inventory.totalWeight;
            dto.flags = dto.payloadLength > 0 ? FlagHasPayload : (byte)0;
            dto.schemaVersion = SchemaVersion;
            return dto;
        }
    }

    [Serializable]
    public struct WorldStateDTO
    {
        public int depletedCount;
        public string[] depletedNodeIds;
        public int depletedPickupChunkCount;
        public long[] depletedPickupChunkKeys;
        public int[] depletedPickupChunkWordStarts;
        public int[] depletedPickupChunkWordCounts;
        public int depletedPickupWordCount;
        public long[] depletedPickupWords;
        public const int MaxNodes = 512;
        public const int MaxPickupChunks = 4096;
        public const int MaxPickupWords = 8192;

        public void EnsureCapacity()
        {
            SaveData.EnsureExactArrayCapacity(ref depletedNodeIds, MaxNodes);
            SaveData.EnsureExactArrayCapacity(ref depletedPickupChunkKeys, MaxPickupChunks);
            SaveData.EnsureExactArrayCapacity(ref depletedPickupChunkWordStarts, MaxPickupChunks);
            SaveData.EnsureExactArrayCapacity(ref depletedPickupChunkWordCounts, MaxPickupChunks);
            SaveData.EnsureExactArrayCapacity(ref depletedPickupWords, MaxPickupWords);
        }
    }

    [Serializable]
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct ProceduralFaunaStateDTO
    {
        public const byte FlagLargeThreatZone = 1 << 0;
        public const byte FlagBlocked = 1 << 1;

        [FieldOffset(0)] public long runtimeKey;
        [FieldOffset(8)] public float cooldownUntilPlayTime;
        [FieldOffset(12)] public byte flags;
        [FieldOffset(13)] private byte _pad0;
        [FieldOffset(14)] private ushort _pad1;

        internal static ProceduralFaunaStateDTO SanitizeForPersistence(in ProceduralFaunaStateDTO value)
        {
            ProceduralFaunaStateDTO dto = value;
            dto.cooldownUntilPlayTime = SanitizeNonNegativeFinite(dto.cooldownUntilPlayTime);
            dto.flags = (byte)(dto.flags & (FlagLargeThreatZone | FlagBlocked));
            dto._pad0 = 0;
            dto._pad1 = 0;
            return dto;
        }

        internal static bool PersistenceEquals(in ProceduralFaunaStateDTO left, in ProceduralFaunaStateDTO right)
        {
            return left.runtimeKey == right.runtimeKey &&
                   left.cooldownUntilPlayTime == right.cooldownUntilPlayTime &&
                   left.flags == right.flags &&
                   left._pad0 == right._pad0 &&
                   left._pad1 == right._pad1;
        }

        private static float SanitizeNonNegativeFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) ? Mathf.Max(0f, value) : 0f;
        }
    }

    [Serializable]
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct HazardZoneRuntimeDTO
    {
        [FieldOffset(0)] public float toxicityDose;
        [FieldOffset(4)] public float toxicityPulseAccumulatorSeconds;
    }

    [Serializable]
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 112)]
    public struct HibernatedFaunaStateDTO
    {
        public const byte FlagLargeThreat = 1 << 0;

        [FieldOffset(0)] public int speciesId;
        [FieldOffset(4)] public int biomeIndex;
        [FieldOffset(8)] public int creatureTypeIndex;
        [FieldOffset(12)] public float health;
        [FieldOffset(16)] public AbsoluteUniversePositionBlit128 position;
        [FieldOffset(64)] public float rotationX;
        [FieldOffset(68)] public float rotationY;
        [FieldOffset(72)] public float rotationZ;
        [FieldOffset(76)] public float rotationW;
        [FieldOffset(80)] public float linearVelocityX;
        [FieldOffset(84)] public float linearVelocityY;
        [FieldOffset(88)] public float linearVelocityZ;
        [FieldOffset(92)] public float angularVelocityX;
        [FieldOffset(96)] public float angularVelocityY;
        [FieldOffset(100)] public float angularVelocityZ;
        [FieldOffset(104)] public uint uniqueInstanceUid;
        [FieldOffset(108)] public byte flags;
        [FieldOffset(109)] private byte _pad0;
        [FieldOffset(110)] private ushort _pad1;

        internal static HibernatedFaunaStateDTO SanitizeForPersistence(in HibernatedFaunaStateDTO value)
        {
            HibernatedFaunaStateDTO dto = value;
            dto.health = SanitizeNonNegativeFinite(dto.health);
            SanitizeAup(ref dto.position);
            SanitizeRotation(ref dto);
            dto.linearVelocityX = SanitizeFinite(dto.linearVelocityX, 0f);
            dto.linearVelocityY = SanitizeFinite(dto.linearVelocityY, 0f);
            dto.linearVelocityZ = SanitizeFinite(dto.linearVelocityZ, 0f);
            dto.angularVelocityX = SanitizeFinite(dto.angularVelocityX, 0f);
            dto.angularVelocityY = SanitizeFinite(dto.angularVelocityY, 0f);
            dto.angularVelocityZ = SanitizeFinite(dto.angularVelocityZ, 0f);
            dto.flags = (byte)(dto.flags & FlagLargeThreat);
            dto._pad0 = 0;
            dto._pad1 = 0;
            return dto;
        }

        internal static bool PersistenceEquals(in HibernatedFaunaStateDTO left, in HibernatedFaunaStateDTO right)
        {
            return left.speciesId == right.speciesId &&
                   left.biomeIndex == right.biomeIndex &&
                   left.creatureTypeIndex == right.creatureTypeIndex &&
                   left.health == right.health &&
                   left.position.GridX == right.position.GridX &&
                   left.position.GridY == right.position.GridY &&
                   left.position.GridZ == right.position.GridZ &&
                   left.position.Local.x == right.position.Local.x &&
                   left.position.Local.y == right.position.Local.y &&
                   left.position.Local.z == right.position.Local.z &&
                   left.position.Local.w == right.position.Local.w &&
                   left.position.Reserved == right.position.Reserved &&
                   left.rotationX == right.rotationX &&
                   left.rotationY == right.rotationY &&
                   left.rotationZ == right.rotationZ &&
                   left.rotationW == right.rotationW &&
                   left.linearVelocityX == right.linearVelocityX &&
                   left.linearVelocityY == right.linearVelocityY &&
                   left.linearVelocityZ == right.linearVelocityZ &&
                   left.angularVelocityX == right.angularVelocityX &&
                   left.angularVelocityY == right.angularVelocityY &&
                   left.angularVelocityZ == right.angularVelocityZ &&
                   left.uniqueInstanceUid == right.uniqueInstanceUid &&
                   left.flags == right.flags &&
                   left._pad0 == right._pad0 &&
                   left._pad1 == right._pad1;
        }

        private static void SanitizeAup(ref AbsoluteUniversePositionBlit128 value)
        {
            value.Local.x = SanitizeFinite(value.Local.x, 0f);
            value.Local.y = SanitizeFinite(value.Local.y, 0f);
            value.Local.z = SanitizeFinite(value.Local.z, 0f);
            value.Local.w = 0f;
            value.Reserved = 0UL;
        }

        private static void SanitizeRotation(ref HibernatedFaunaStateDTO dto)
        {
            if (!IsFinite(dto.rotationX) || !IsFinite(dto.rotationY) || !IsFinite(dto.rotationZ) || !IsFinite(dto.rotationW))
            {
                dto.rotationX = 0f;
                dto.rotationY = 0f;
                dto.rotationZ = 0f;
                dto.rotationW = 1f;
                return;
            }

            float lengthSq =
                dto.rotationX * dto.rotationX +
                dto.rotationY * dto.rotationY +
                dto.rotationZ * dto.rotationZ +
                dto.rotationW * dto.rotationW;
            if (!IsFinite(lengthSq) || lengthSq <= 0.000001f)
            {
                dto.rotationX = 0f;
                dto.rotationY = 0f;
                dto.rotationZ = 0f;
                dto.rotationW = 1f;
                return;
            }

            float invLength = 1f / Mathf.Sqrt(lengthSq);
            dto.rotationX *= invLength;
            dto.rotationY *= invLength;
            dto.rotationZ *= invLength;
            dto.rotationW *= invLength;
        }

        private static float SanitizeNonNegativeFinite(float value)
        {
            return IsFinite(value) ? Mathf.Max(0f, value) : 0f;
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    [Serializable]
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ProceduralGeologySeamStateDTO
    {
        [FieldOffset(0)] public long runtimeKey;
        [FieldOffset(8)] public int chunkX;
        [FieldOffset(12)] public int chunkZ;
        [FieldOffset(16)] public float absoluteTerrainHeight;
        [FieldOffset(20)] public float absoluteSeamHeight;
        [FieldOffset(24)] public float seamBlendRadius;
        [FieldOffset(28)] public float terrainBlendWeight;
        [FieldOffset(32)] public float caveBlendWeight;
        [FieldOffset(36)] public float absolutePositionX;
        [FieldOffset(40)] public float absolutePositionY;
        [FieldOffset(44)] public float absolutePositionZ;
        [FieldOffset(48)] public float absoluteVoxelCenterX;
        [FieldOffset(52)] public float absoluteVoxelCenterY;
        [FieldOffset(56)] public float absoluteVoxelCenterZ;
        [FieldOffset(60)] private int _pad0;

        internal static ProceduralGeologySeamStateDTO SanitizeForPersistence(in ProceduralGeologySeamStateDTO value)
        {
            ProceduralGeologySeamStateDTO dto = value;
            dto.absoluteTerrainHeight = SanitizeFinite(dto.absoluteTerrainHeight, 0f);
            dto.absoluteSeamHeight = SanitizeFinite(dto.absoluteSeamHeight, 0f);
            dto.seamBlendRadius = SanitizeNonNegativeFinite(dto.seamBlendRadius);
            dto.terrainBlendWeight = SanitizeUnit01(dto.terrainBlendWeight);
            dto.caveBlendWeight = SanitizeUnit01(dto.caveBlendWeight);
            dto.absolutePositionX = SanitizeFinite(dto.absolutePositionX, 0f);
            dto.absolutePositionY = SanitizeFinite(dto.absolutePositionY, 0f);
            dto.absolutePositionZ = SanitizeFinite(dto.absolutePositionZ, 0f);
            dto.absoluteVoxelCenterX = SanitizeFinite(dto.absoluteVoxelCenterX, 0f);
            dto.absoluteVoxelCenterY = SanitizeFinite(dto.absoluteVoxelCenterY, 0f);
            dto.absoluteVoxelCenterZ = SanitizeFinite(dto.absoluteVoxelCenterZ, 0f);
            dto._pad0 = 0;
            return dto;
        }

        internal static bool PersistenceEquals(in ProceduralGeologySeamStateDTO left, in ProceduralGeologySeamStateDTO right)
        {
            return left.runtimeKey == right.runtimeKey &&
                   left.chunkX == right.chunkX &&
                   left.chunkZ == right.chunkZ &&
                   left.absoluteTerrainHeight == right.absoluteTerrainHeight &&
                   left.absoluteSeamHeight == right.absoluteSeamHeight &&
                   left.seamBlendRadius == right.seamBlendRadius &&
                   left.terrainBlendWeight == right.terrainBlendWeight &&
                   left.caveBlendWeight == right.caveBlendWeight &&
                   left.absolutePositionX == right.absolutePositionX &&
                   left.absolutePositionY == right.absolutePositionY &&
                   left.absolutePositionZ == right.absolutePositionZ &&
                   left.absoluteVoxelCenterX == right.absoluteVoxelCenterX &&
                   left.absoluteVoxelCenterY == right.absoluteVoxelCenterY &&
                   left.absoluteVoxelCenterZ == right.absoluteVoxelCenterZ &&
                   left._pad0 == right._pad0;
        }

        private static float SanitizeNonNegativeFinite(float value)
        {
            return IsFinite(value) ? Mathf.Max(0f, value) : 0f;
        }

        private static float SanitizeUnit01(float value)
        {
            return IsFinite(value) ? Mathf.Clamp01(value) : 0f;
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    [Serializable]
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct ProceduralGeologyCaveEntranceDTO
    {
        [FieldOffset(0)] public long runtimeKey;
        [FieldOffset(8)] public float surfacePositionX;
        [FieldOffset(12)] public float surfacePositionY;
        [FieldOffset(16)] public float surfacePositionZ;
        [FieldOffset(20)] public float inwardDirectionX;
        [FieldOffset(24)] public float inwardDirectionY;
        [FieldOffset(28)] public float inwardDirectionZ;
        [FieldOffset(32)] public float radius;
        [FieldOffset(36)] public float funnelLength;
        [FieldOffset(40)] public float innerRadius;
        [FieldOffset(44)] private int _pad0;

        internal static ProceduralGeologyCaveEntranceDTO SanitizeForPersistence(in ProceduralGeologyCaveEntranceDTO value)
        {
            ProceduralGeologyCaveEntranceDTO dto = value;
            dto.surfacePositionX = SanitizeFinite(dto.surfacePositionX, 0f);
            dto.surfacePositionY = SanitizeFinite(dto.surfacePositionY, 0f);
            dto.surfacePositionZ = SanitizeFinite(dto.surfacePositionZ, 0f);
            SanitizeDirection(ref dto);
            dto.radius = SanitizeNonNegativeFinite(dto.radius);
            dto.funnelLength = SanitizeNonNegativeFinite(dto.funnelLength);
            dto.innerRadius = SanitizeNonNegativeFinite(dto.innerRadius);
            dto._pad0 = 0;
            return dto;
        }

        internal static bool PersistenceEquals(in ProceduralGeologyCaveEntranceDTO left, in ProceduralGeologyCaveEntranceDTO right)
        {
            return left.runtimeKey == right.runtimeKey &&
                   left.surfacePositionX == right.surfacePositionX &&
                   left.surfacePositionY == right.surfacePositionY &&
                   left.surfacePositionZ == right.surfacePositionZ &&
                   left.inwardDirectionX == right.inwardDirectionX &&
                   left.inwardDirectionY == right.inwardDirectionY &&
                   left.inwardDirectionZ == right.inwardDirectionZ &&
                   left.radius == right.radius &&
                   left.funnelLength == right.funnelLength &&
                   left.innerRadius == right.innerRadius &&
                   left._pad0 == right._pad0;
        }

        private static void SanitizeDirection(ref ProceduralGeologyCaveEntranceDTO dto)
        {
            dto.inwardDirectionX = SanitizeFinite(dto.inwardDirectionX, 0f);
            dto.inwardDirectionY = SanitizeFinite(dto.inwardDirectionY, 0f);
            dto.inwardDirectionZ = SanitizeFinite(dto.inwardDirectionZ, 1f);
            float lengthSq =
                dto.inwardDirectionX * dto.inwardDirectionX +
                dto.inwardDirectionY * dto.inwardDirectionY +
                dto.inwardDirectionZ * dto.inwardDirectionZ;
            if (!IsFinite(lengthSq) || lengthSq <= 0.000001f)
            {
                dto.inwardDirectionX = 0f;
                dto.inwardDirectionY = 0f;
                dto.inwardDirectionZ = 1f;
                return;
            }

            float invLength = 1f / Mathf.Sqrt(lengthSq);
            dto.inwardDirectionX *= invLength;
            dto.inwardDirectionY *= invLength;
            dto.inwardDirectionZ *= invLength;
        }

        private static float SanitizeNonNegativeFinite(float value)
        {
            return IsFinite(value) ? Mathf.Max(0f, value) : 0f;
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    [Serializable]
    public struct ProceduralWorldStateDTO
    {
        public int suppressedPlacementCount;
        public long[] suppressedPlacementKeys;
        public int faunaStateCount;
        public ProceduralFaunaStateDTO[] faunaStates;
        public int hibernatedFaunaCount;
        public HibernatedFaunaStateDTO[] hibernatedFaunaStates;
        public int geologySeamStateCount;
        public ProceduralGeologySeamStateDTO[] geologySeamStates;
        public int geologyCaveEntranceCount;
        public ProceduralGeologyCaveEntranceDTO[] geologyCaveEntrances;

        public const int MaxSuppressedPlacements = 8192;
        public const int MaxFaunaStates = 4096;
        public const int MaxHibernatedFaunaStates = 512;
        public const int MaxGeologySeamStates = 512;
        public const int MaxGeologyCaveEntrances = 512;

        public void EnsureCapacity()
        {
            SaveData.EnsureExactArrayCapacity(ref suppressedPlacementKeys, MaxSuppressedPlacements);
            SaveData.EnsureExactArrayCapacity(ref faunaStates, MaxFaunaStates);
            SaveData.EnsureExactArrayCapacity(ref hibernatedFaunaStates, MaxHibernatedFaunaStates);
            SaveData.EnsureExactArrayCapacity(ref geologySeamStates, MaxGeologySeamStates);
            SaveData.EnsureExactArrayCapacity(ref geologyCaveEntrances, MaxGeologyCaveEntrances);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  ConstructionDTO
    // ══════════════════════════════════════════════════════════════════

    [Serializable]
    public struct ConstructionDTO
    {
        public int moduleCount;
        public ModuleDTO[] modules;
        public int graphNodeCount;
        public ModuleGraphNodeDTO[] graphNodes;
        public int graphEdgeCount;
        public ModuleGraphEdgeDTO[] graphEdges;
        public int moduleBlitCount;
        public ModuleBlitDTO[] moduleBlitRecords;
        public int habitatFloodStateCount;
        public HabitatFloodStateDTO[] habitatFloodStates;
        public const int MaxModules = 256;
        public const int MaxGraphEdges = MaxModules * 6;

        public static ConstructionDTO CreatePreallocated()
        {
            ConstructionDTO dto = default;
            dto.EnsureCapacity();
            return dto;
        }

        public void EnsureCapacity()
        {
            SaveData.EnsureExactArrayCapacity(ref modules, MaxModules);
            SaveData.EnsureExactArrayCapacity(ref graphNodes, MaxModules);
            SaveData.EnsureExactArrayCapacity(ref graphEdges, MaxGraphEdges);
            SaveData.EnsureExactArrayCapacity(ref moduleBlitRecords, MaxModules);
            SaveData.EnsureExactArrayCapacity(ref habitatFloodStates, MaxModules);
            EnsureModuleNestedArrayCapacity();
        }

        private void EnsureModuleNestedArrayCapacity()
        {
            if (modules == null)
                return;

            int count = Math.Min(MaxModules, modules.Length);
            for (int i = 0; i < count; i++)
            {
                ModuleDTO module = modules[i];
                module.EnsureNestedArrayCapacity();
                modules[i] = module;
            }
        }

        public void RefreshHabitatFloodStateMirrors()
        {
            SaveData.EnsureExactArrayCapacity(ref habitatFloodStates, MaxModules);

            int safeCount = Math.Clamp(
                moduleCount,
                0,
                modules != null ? Math.Min(MaxModules, modules.Length) : 0);

            habitatFloodStateCount = safeCount;
            for (int i = 0; i < safeCount; i++)
            {
                int moduleHashId = ResolveHabitatFloodStateModuleHashId(i);
                habitatFloodStates[i] = HabitatFloodStateDTO.FromModule(in modules[i], moduleHashId);
            }
        }

        internal int ResolveHabitatFloodStateModuleHashId(int moduleIndex)
        {
            if (moduleIndex < 0)
                return 0;

            int safeBlitCount = Math.Clamp(
                moduleBlitCount,
                0,
                moduleBlitRecords != null ? Math.Min(MaxModules, moduleBlitRecords.Length) : 0);
            if (moduleIndex < safeBlitCount)
            {
                int moduleHashId = moduleBlitRecords[moduleIndex].moduleHashId;
                if (moduleHashId != 0)
                    return moduleHashId;
            }

            int safeGraphNodeCount = Math.Clamp(
                graphNodeCount,
                0,
                graphNodes != null ? Math.Min(MaxModules, graphNodes.Length) : 0);
            return moduleIndex < safeGraphNodeCount
                ? graphNodes[moduleIndex].moduleHashId
                : 0;
        }
    }

    [Serializable]
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct HabitatFloodStateDTO
    {
        public const byte FlagFlooded = 1 << 0;
        public const byte FlagInfested = 1 << 1;

        [FieldOffset(0)] public int moduleHashId;
        [FieldOffset(4)] public float integrity;
        [FieldOffset(8)] public float repairIntegrityCap;
        [FieldOffset(12)] public float airReserveNormalized;
        [FieldOffset(16)] public float co2Normalized;
        [FieldOffset(20)] public float floodedReefFloodSeconds;
        [FieldOffset(24)] public byte flags;
        [FieldOffset(25)] public byte failureMode;
        [FieldOffset(26)] public byte health;
        [FieldOffset(27)] public byte reserved0;
        [FieldOffset(28)] private int _pad0;

        public static HabitatFloodStateDTO FromModule(in ModuleDTO module, int stableModuleHashId)
        {
            HabitatFloodStateDTO dto = default;
            dto.moduleHashId = stableModuleHashId;
            dto.integrity = SanitizeNonNegativeFinite(module.integrity);
            dto.repairIntegrityCap = SanitizeNonNegativeFinite(module.repairIntegrityCap);
            dto.airReserveNormalized = SanitizeUnit01(module.airReserveNormalized);
            dto.co2Normalized = SanitizeUnit01(module.co2Normalized);
            dto.floodedReefFloodSeconds = SanitizeNonNegativeFinite(module.floodedReefFloodSeconds);
            dto.flags = (byte)((module.isFlooded ? FlagFlooded : 0) |
                               (module.interiorReefInfestationActive ? FlagInfested : 0));
            dto.failureMode = SanitizeFailureMode(module.failureMode);
            dto.health = module.health;
            return dto;
        }

        public static HabitatFloodStateDTO Sanitize(in HabitatFloodStateDTO value)
        {
            HabitatFloodStateDTO dto = value;
            dto.integrity = SanitizeNonNegativeFinite(dto.integrity);
            dto.repairIntegrityCap = SanitizeNonNegativeFinite(dto.repairIntegrityCap);
            dto.airReserveNormalized = SanitizeUnit01(dto.airReserveNormalized);
            dto.co2Normalized = SanitizeUnit01(dto.co2Normalized);
            dto.floodedReefFloodSeconds = SanitizeNonNegativeFinite(dto.floodedReefFloodSeconds);
            dto.flags = (byte)(dto.flags & (FlagFlooded | FlagInfested));
            dto.failureMode = SanitizeFailureMode(dto.failureMode);
            dto.reserved0 = 0;
            dto._pad0 = 0;
            return dto;
        }

        internal static bool PersistenceEquals(in HabitatFloodStateDTO left, in HabitatFloodStateDTO right)
        {
            return left.moduleHashId == right.moduleHashId &&
                   left.integrity == right.integrity &&
                   left.repairIntegrityCap == right.repairIntegrityCap &&
                   left.airReserveNormalized == right.airReserveNormalized &&
                   left.co2Normalized == right.co2Normalized &&
                   left.floodedReefFloodSeconds == right.floodedReefFloodSeconds &&
                   left.flags == right.flags &&
                   left.failureMode == right.failureMode &&
                   left.health == right.health &&
                   left.reserved0 == right.reserved0 &&
                   left._pad0 == right._pad0;
        }

        private static float SanitizeNonNegativeFinite(float value)
        {
            return IsFinite(value) ? Mathf.Max(0f, value) : 0f;
        }

        private static float SanitizeUnit01(float value)
        {
            return IsFinite(value) ? Mathf.Clamp01(value) : 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static byte SanitizeFailureMode(byte value)
        {
            return value <= SaveData.ModuleFailureModeMaxKnown ? value : SaveData.ModuleFailureModeNone;
        }
    }

    /// <summary>
    /// Fixed 64-byte construction module record for unmanaged binary/MMF copy.
    /// Existing ModuleDTO remains the managed compatibility DTO because it contains strings and arrays.
    /// </summary>
    [Serializable]
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ModuleBlitDTO
    {
        public const byte FlagFlooded = 1 << 0;
        public const byte FlagInteriorReef = 1 << 1;

        [FieldOffset(0)] public int prefabHashId;
        [FieldOffset(4)] public int moduleHashId;
        [FieldOffset(8)] public long aupGridX;
        [FieldOffset(16)] public long aupGridY;
        [FieldOffset(24)] public long aupGridZ;
        [FieldOffset(32)] public float aupLocalX;
        [FieldOffset(36)] public float aupLocalY;
        [FieldOffset(40)] public float aupLocalZ;
        [FieldOffset(44)] public float rotX;
        [FieldOffset(48)] public float rotY;
        [FieldOffset(52)] public float rotZ;
        [FieldOffset(56)] public float rotW;
        [FieldOffset(60)] public byte health;
        [FieldOffset(61)] public byte flags;
        [FieldOffset(62)] public byte failureMode;
        [FieldOffset(63)] public byte reserved;

        internal static ModuleBlitDTO SanitizeForPersistence(in ModuleBlitDTO value)
        {
            ModuleBlitDTO dto = value;
            dto.aupLocalX = SanitizeFinite(dto.aupLocalX, 0f);
            dto.aupLocalY = SanitizeFinite(dto.aupLocalY, 0f);
            dto.aupLocalZ = SanitizeFinite(dto.aupLocalZ, 0f);
            SanitizeRotation(ref dto);
            dto.flags = (byte)(dto.flags & (FlagFlooded | FlagInteriorReef));
            dto.failureMode = SanitizeFailureMode(dto.failureMode);
            dto.reserved = 0;
            return dto;
        }

        internal static bool PersistenceEquals(in ModuleBlitDTO left, in ModuleBlitDTO right)
        {
            return left.prefabHashId == right.prefabHashId &&
                   left.moduleHashId == right.moduleHashId &&
                   left.aupGridX == right.aupGridX &&
                   left.aupGridY == right.aupGridY &&
                   left.aupGridZ == right.aupGridZ &&
                   left.aupLocalX == right.aupLocalX &&
                   left.aupLocalY == right.aupLocalY &&
                   left.aupLocalZ == right.aupLocalZ &&
                   left.rotX == right.rotX &&
                   left.rotY == right.rotY &&
                   left.rotZ == right.rotZ &&
                   left.rotW == right.rotW &&
                   left.health == right.health &&
                   left.flags == right.flags &&
                   left.failureMode == right.failureMode &&
                   left.reserved == right.reserved;
        }

        private static void SanitizeRotation(ref ModuleBlitDTO dto)
        {
            if (!IsFinite(dto.rotX) || !IsFinite(dto.rotY) || !IsFinite(dto.rotZ) || !IsFinite(dto.rotW))
            {
                dto.rotX = 0f;
                dto.rotY = 0f;
                dto.rotZ = 0f;
                dto.rotW = 1f;
                return;
            }

            float lengthSq = dto.rotX * dto.rotX + dto.rotY * dto.rotY + dto.rotZ * dto.rotZ + dto.rotW * dto.rotW;
            if (!IsFinite(lengthSq) || lengthSq <= 0.000001f)
            {
                dto.rotX = 0f;
                dto.rotY = 0f;
                dto.rotZ = 0f;
                dto.rotW = 1f;
                return;
            }

            float invLength = 1f / Mathf.Sqrt(lengthSq);
            dto.rotX *= invLength;
            dto.rotY *= invLength;
            dto.rotZ *= invLength;
            dto.rotW *= invLength;
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static byte SanitizeFailureMode(byte value)
        {
            return value <= SaveData.ModuleFailureModeMaxKnown ? value : SaveData.ModuleFailureModeNone;
        }
    }

    [Serializable]
    public struct ScanEntryDTO
    {
        public string id;
        public string title;
        public string category;
        public string summary;

        internal static ScanEntryDTO SanitizeForPersistence(in ScanEntryDTO value)
        {
            ScanEntryDTO dto = value;
            dto.id = SaveData.SanitizePersistenceString(dto.id);
            dto.title ??= string.Empty;
            dto.category ??= string.Empty;
            dto.summary ??= string.Empty;
            return dto;
        }

        internal static bool PersistenceEquals(in ScanEntryDTO left, in ScanEntryDTO right)
        {
            return string.Equals(left.id, right.id, StringComparison.Ordinal) &&
                   string.Equals(left.title, right.title, StringComparison.Ordinal) &&
                   string.Equals(left.category, right.category, StringComparison.Ordinal) &&
                   string.Equals(left.summary, right.summary, StringComparison.Ordinal);
        }
    }

    [Serializable]
    public struct ScanLogDTO
    {
        public int entryCount;
        public ScanEntryDTO[] entries;
        public int recentCount;
        public string[] recentEntryIds;

        public const int MaxEntries = 128;
        public const int MaxRecentEntries = 8;

        public void EnsureCapacity()
        {
            SaveData.EnsureExactArrayCapacity(ref entries, MaxEntries);
            SaveData.EnsureExactArrayCapacity(ref recentEntryIds, MaxRecentEntries);
        }
    }

    [Serializable]
    public struct BarterOfferStateDTO
    {
        public string offerId;
        public int executionCount;

        internal static BarterOfferStateDTO SanitizeForPersistence(in BarterOfferStateDTO value)
        {
            BarterOfferStateDTO dto = value;
            dto.offerId = SaveData.SanitizePersistenceString(dto.offerId);
            dto.executionCount = Math.Max(0, dto.executionCount);
            return dto;
        }

        internal static bool PersistenceEquals(in BarterOfferStateDTO left, in BarterOfferStateDTO right)
        {
            return left.offerId == right.offerId &&
                   left.executionCount == right.executionCount;
        }
    }

    [Serializable]
    public struct BarterTransactionDTO
    {
        public string offerId;
        public string offerName;
        public string channelName;
        public string costSummary;
        public string rewardSummary;

        internal static BarterTransactionDTO SanitizeForPersistence(in BarterTransactionDTO value)
        {
            BarterTransactionDTO dto = value;
            dto.offerId = SaveData.SanitizePersistenceString(dto.offerId);
            dto.offerName ??= string.Empty;
            dto.channelName ??= string.Empty;
            dto.costSummary ??= string.Empty;
            dto.rewardSummary ??= string.Empty;
            return dto;
        }

        internal static bool PersistenceEquals(in BarterTransactionDTO left, in BarterTransactionDTO right)
        {
            return string.Equals(left.offerId, right.offerId, StringComparison.Ordinal) &&
                   string.Equals(left.offerName, right.offerName, StringComparison.Ordinal) &&
                   string.Equals(left.channelName, right.channelName, StringComparison.Ordinal) &&
                   string.Equals(left.costSummary, right.costSummary, StringComparison.Ordinal) &&
                   string.Equals(left.rewardSummary, right.rewardSummary, StringComparison.Ordinal);
        }
    }

    [Serializable]
    public struct BarterDTO
    {
        public int stateCount;
        public BarterOfferStateDTO[] offerStates;
        public int recentTransactionCount;
        public BarterTransactionDTO[] recentTransactions;

        public const int MaxOffers = 128;
        public const int MaxRecentTransactions = 8;

        public void EnsureCapacity()
        {
            SaveData.EnsureExactArrayCapacity(ref offerStates, MaxOffers);
            SaveData.EnsureExactArrayCapacity(ref recentTransactions, MaxRecentTransactions);
        }
    }

    [Serializable]
    public struct FieldOperationEntryDTO
    {
        public string source;
        public string title;
        public string summary;
        public string severity;

        internal static FieldOperationEntryDTO SanitizeForPersistence(in FieldOperationEntryDTO value)
        {
            FieldOperationEntryDTO dto = value;
            dto.source ??= string.Empty;
            dto.title ??= string.Empty;
            dto.summary ??= string.Empty;
            dto.severity ??= string.Empty;
            return dto;
        }

        internal static bool PersistenceEquals(in FieldOperationEntryDTO left, in FieldOperationEntryDTO right)
        {
            return string.Equals(left.source, right.source, StringComparison.Ordinal) &&
                   string.Equals(left.title, right.title, StringComparison.Ordinal) &&
                   string.Equals(left.summary, right.summary, StringComparison.Ordinal) &&
                   string.Equals(left.severity, right.severity, StringComparison.Ordinal);
        }
    }

    [Serializable]
    public struct FieldOperationLogDTO
    {
        public int recentCount;
        public FieldOperationEntryDTO[] recentEntries;

        public const int MaxRecentEntries = 12;

        public void EnsureCapacity()
        {
            SaveData.EnsureExactArrayCapacity(ref recentEntries, MaxRecentEntries);
        }
    }

    [Serializable]
    public struct BeaconEntryDTO
    {
        public const float DefaultLightRange = 4f;

        public string id;
        public string label;
        public float posX;
        public float posY;
        public float posZ;
        public float rotX;
        public float rotY;
        public float rotZ;
        public float rotW;
        public float colorR;
        public float colorG;
        public float colorB;
        public float colorA;
        public float lightRange;

        public Vector3 GetPosition() => new Vector3(posX, posY, posZ);
        public Quaternion GetRotation() => new Quaternion(rotX, rotY, rotZ, rotW);
        public Color GetColor() => new Color(colorR, colorG, colorB, colorA <= 0f ? 1f : colorA);

        internal static BeaconEntryDTO SanitizeForPersistence(in BeaconEntryDTO value)
        {
            BeaconEntryDTO dto = value;
            dto.id = SaveData.SanitizePersistenceString(dto.id);
            dto.label = string.IsNullOrWhiteSpace(dto.label) ? string.Empty : dto.label;
            dto.posX = SanitizeFinite(dto.posX, 0f);
            dto.posY = SanitizeFinite(dto.posY, 0f);
            dto.posZ = SanitizeFinite(dto.posZ, 0f);
            SanitizeRotation(ref dto);
            dto.colorR = SanitizeUnit01(dto.colorR);
            dto.colorG = SanitizeUnit01(dto.colorG);
            dto.colorB = SanitizeUnit01(dto.colorB);
            dto.colorA = SanitizeAlpha(dto.colorA);
            dto.lightRange = SanitizePositiveFinite(dto.lightRange, DefaultLightRange);
            return dto;
        }

        internal static bool PersistenceEquals(in BeaconEntryDTO left, in BeaconEntryDTO right)
        {
            return string.Equals(left.id, right.id, StringComparison.Ordinal) &&
                   string.Equals(left.label, right.label, StringComparison.Ordinal) &&
                   left.posX == right.posX &&
                   left.posY == right.posY &&
                   left.posZ == right.posZ &&
                   left.rotX == right.rotX &&
                   left.rotY == right.rotY &&
                   left.rotZ == right.rotZ &&
                   left.rotW == right.rotW &&
                   left.colorR == right.colorR &&
                   left.colorG == right.colorG &&
                   left.colorB == right.colorB &&
                   left.colorA == right.colorA &&
                   left.lightRange == right.lightRange;
        }

        public void SetPosition(Vector3 pos)
        {
            posX = pos.x; posY = pos.y; posZ = pos.z;
        }

        public void SetRotation(Quaternion rot)
        {
            rotX = rot.x; rotY = rot.y; rotZ = rot.z; rotW = rot.w;
        }

        private static void SanitizeRotation(ref BeaconEntryDTO dto)
        {
            dto.rotX = SanitizeFinite(dto.rotX, 0f);
            dto.rotY = SanitizeFinite(dto.rotY, 0f);
            dto.rotZ = SanitizeFinite(dto.rotZ, 0f);
            dto.rotW = SanitizeFinite(dto.rotW, 1f);
            float lengthSq =
                dto.rotX * dto.rotX +
                dto.rotY * dto.rotY +
                dto.rotZ * dto.rotZ +
                dto.rotW * dto.rotW;
            if (!IsFinite(lengthSq) || lengthSq <= 0.000001f)
            {
                dto.rotX = 0f;
                dto.rotY = 0f;
                dto.rotZ = 0f;
                dto.rotW = 1f;
                return;
            }

            float invLength = 1f / Mathf.Sqrt(lengthSq);
            dto.rotX *= invLength;
            dto.rotY *= invLength;
            dto.rotZ *= invLength;
            dto.rotW *= invLength;
        }

        private static float SanitizeUnit01(float value)
        {
            return IsFinite(value) ? Mathf.Clamp01(value) : 0f;
        }

        private static float SanitizeAlpha(float value)
        {
            return IsFinite(value) && value > 0f ? Mathf.Clamp01(value) : 1f;
        }

        private static float SanitizePositiveFinite(float value, float fallback)
        {
            return IsFinite(value) && value > 0f ? value : fallback;
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    [Serializable]
    public struct BeaconNetworkDTO
    {
        public int activeCount;
        public int nextSequence;
        public BeaconEntryDTO[] entries;

        public const int MaxEntries = 32;

        public void EnsureCapacity()
        {
            SaveData.EnsureExactArrayCapacity(ref entries, MaxEntries);
        }
    }

    [Serializable]
    public struct ExplorationMapDTO
    {
        public int exploredChunkCount;
        public long[] exploredChunkKeys;
        public int chunkSizeMeters;
        public int mortonMaskAxisBits;
        public int mortonMaskOriginOffset;
        public uint mortonBuildSalt;
        public int exploredMortonWordCount;
        public long[] exploredMortonMaskWords;
        public int exploredMortonByteCount;
        public byte[] exploredMortonMaskBytes;
        public int cartographyCellSizeMeters;
        public int cartographyMaskAxisBits;
        public int cartographyMaskOriginOffset;
        public int discoveredSectorWordCount;
        public long[] discoveredSectorMaskWords;
        public int discoveredSectorByteCount;
        public byte[] discoveredSectorMaskBytes;

        public const int MaxExploredChunks = 16384;
        public const int DenseChunkSizeMeters = 16;
        public const int MortonMaskAxisBits = 7;
        public const int MortonMaskAxisLength = 1 << MortonMaskAxisBits;
        public const int MortonMaskOriginOffset = MortonMaskAxisLength >> 1;
        public const int MortonMaskBitCount = MortonMaskAxisLength * MortonMaskAxisLength * MortonMaskAxisLength;
        public const int MortonMaskWordCount = MortonMaskBitCount >> 6;
        public const int MortonMaskByteCount = MortonMaskBitCount >> 3;
        public const int CartographyCellSizeMeters = 10;
        public const int CartographyMaskAxisBits = 7;
        public const int CartographyMaskAxisLength = 1 << CartographyMaskAxisBits;
        public const int CartographyMaskOriginOffset = CartographyMaskAxisLength >> 1;
        public const int CartographyMaskBitCount = CartographyMaskAxisLength * CartographyMaskAxisLength * CartographyMaskAxisLength;
        public const int CartographyMaskWordCount = CartographyMaskBitCount >> 6;
        public const int CartographyMaskByteCount = CartographyMaskBitCount >> 3;

        public void EnsureCapacity()
        {
            SaveData.EnsureExactArrayCapacity(ref exploredChunkKeys, MaxExploredChunks);
            SaveData.EnsureExactArrayCapacity(ref exploredMortonMaskWords, MortonMaskWordCount);
            SaveData.EnsureExactArrayCapacity(ref exploredMortonMaskBytes, MortonMaskByteCount);
            SaveData.EnsureExactArrayCapacity(ref discoveredSectorMaskWords, CartographyMaskWordCount);
            SaveData.EnsureExactArrayCapacity(ref discoveredSectorMaskBytes, CartographyMaskByteCount);

            chunkSizeMeters = DenseChunkSizeMeters;
            mortonMaskAxisBits = MortonMaskAxisBits;
            mortonMaskOriginOffset = MortonMaskOriginOffset;
            mortonBuildSalt = SaveBinaryStorage.ExplorationMortonBuildSalt32;
            cartographyCellSizeMeters = CartographyCellSizeMeters;
            cartographyMaskAxisBits = CartographyMaskAxisBits;
            cartographyMaskOriginOffset = CartographyMaskOriginOffset;
            if (exploredMortonWordCount < 0 || exploredMortonWordCount > MortonMaskWordCount)
                exploredMortonWordCount = 0;

            if (exploredMortonByteCount < 0 || exploredMortonByteCount > MortonMaskByteCount)
                exploredMortonByteCount = 0;

            if (discoveredSectorWordCount < 0 || discoveredSectorWordCount > CartographyMaskWordCount)
                discoveredSectorWordCount = 0;

            if (discoveredSectorByteCount < 0 || discoveredSectorByteCount > CartographyMaskByteCount)
                discoveredSectorByteCount = 0;
        }
    }

    [Serializable]
    public struct PDALogbookEntryDTO
    {
        public int sequence;
        public int dayIndex;
        public float dayTimeHours;
        public float playTimeSeconds;
        public int titleHash;
        public int messageHash;
        public int originHash;
        /// <summary>Legacy string field retained only for v53-and-older migration reads.</summary>
        public string title;
        /// <summary>Legacy string field retained only for v53-and-older migration reads.</summary>
        public string message;
        /// <summary>Legacy string field retained only for v53-and-older migration reads.</summary>
        public string originKey;

        internal static PDALogbookEntryDTO SanitizeForPersistence(in PDALogbookEntryDTO value)
        {
            PDALogbookEntryDTO dto = value;
            dto.sequence = Math.Max(0, dto.sequence);
            dto.dayIndex = Math.Max(0, dto.dayIndex);
            dto.dayTimeHours = SanitizeFiniteClamp(dto.dayTimeHours, 0f, 24f);
            dto.playTimeSeconds = SanitizeNonNegativeFinite(dto.playTimeSeconds);
            string title = SaveData.SanitizePersistenceString(dto.title);
            string message = SaveData.SanitizePersistenceString(dto.message);
            string originKey = SaveData.SanitizePersistenceString(dto.originKey);
            if (dto.titleHash == 0 && title.Length > 0)
                dto.titleHash = LocHash.Compute(title);
            if (dto.messageHash == 0 && message.Length > 0)
                dto.messageHash = LocHash.Compute(message);
            if (dto.originHash == 0 && originKey.Length > 0)
                dto.originHash = LocHash.Compute(originKey);
            dto.title = string.Empty;
            dto.message = string.Empty;
            dto.originKey = string.Empty;
            return dto;
        }

        internal static bool PersistenceEquals(in PDALogbookEntryDTO left, in PDALogbookEntryDTO right)
        {
            return left.sequence == right.sequence &&
                   left.dayIndex == right.dayIndex &&
                   left.dayTimeHours == right.dayTimeHours &&
                   left.playTimeSeconds == right.playTimeSeconds &&
                   left.titleHash == right.titleHash &&
                   left.messageHash == right.messageHash &&
                   left.originHash == right.originHash &&
                   string.Equals(left.title, right.title, StringComparison.Ordinal) &&
                   string.Equals(left.message, right.message, StringComparison.Ordinal) &&
                   string.Equals(left.originKey, right.originKey, StringComparison.Ordinal);
        }

        private static float SanitizeFiniteClamp(float value, float min, float max)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value)
                ? Mathf.Clamp(value, min, max)
                : min;
        }

        private static float SanitizeNonNegativeFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value)
                ? Mathf.Max(0f, value)
                : 0f;
        }
    }

    [Serializable]
    public struct PDALogbookDTO
    {
        public int entryCount;
        public int nextSequence;
        public PDALogbookEntryDTO[] entries;
        public int seenOriginCount;
        public int[] seenOriginHashes;
        /// <summary>Legacy string field retained only for v53-and-older migration reads.</summary>
        public string[] seenOriginKeys;

        public const int MaxEntries = 256;
        public const int MaxSeenOrigins = 512;

        public void EnsureCapacity()
        {
            SaveData.EnsureExactArrayCapacity(ref entries, MaxEntries);
            SaveData.EnsureExactArrayCapacity(ref seenOriginKeys, MaxSeenOrigins);
            SaveData.EnsureExactArrayCapacity(ref seenOriginHashes, MaxSeenOrigins);
        }

        internal bool SanitizeSeenOriginsForPersistence()
        {
            int previousCount = seenOriginCount;
            EnsureCapacity();

            int safeCount = Math.Clamp(previousCount, 0, MaxSeenOrigins);
            bool changed = safeCount != previousCount;
            int writeIndex = 0;
            for (int i = 0; i < safeCount; i++)
            {
                int originHash = seenOriginHashes[i];
                string originKey = SaveData.SanitizePersistenceString(seenOriginKeys[i]);
                if (originHash == 0 && originKey.Length > 0)
                    originHash = LocHash.Compute(originKey);

                if (originHash == 0)
                {
                    if (seenOriginKeys[i] != null)
                    {
                        seenOriginKeys[i] = string.Empty;
                        changed = true;
                    }

                    continue;
                }

                string safeOriginKey = originKey;
                if (writeIndex != i ||
                    seenOriginHashes[writeIndex] != originHash ||
                    !string.Equals(seenOriginKeys[writeIndex], safeOriginKey, StringComparison.Ordinal))
                {
                    changed = true;
                }

                seenOriginHashes[writeIndex] = originHash;
                seenOriginKeys[writeIndex] = safeOriginKey;
                writeIndex++;
            }

            for (int i = writeIndex; i < safeCount; i++)
            {
                if (seenOriginHashes[i] != 0)
                {
                    seenOriginHashes[i] = 0;
                    changed = true;
                }

                if (!string.IsNullOrEmpty(seenOriginKeys[i]))
                {
                    seenOriginKeys[i] = string.Empty;
                    changed = true;
                }
            }

            if (seenOriginCount != writeIndex)
            {
                seenOriginCount = writeIndex;
                changed = true;
            }

            return changed;
        }
    }

    [Serializable]
    public struct PDAMarkerEntryDTO
    {
        public const int AupPositionEncodingVersion = 1;

        public uint markerHashId;
        public string markerId;
        public uint titleHashId;
        public string title;
        public int iconType;
        public float posX;
        public float posY;
        public float posZ;
        public bool visibleOnHud;
        public int positionEncodingVersion;
        public long aupGridX;
        public long aupGridY;
        public long aupGridZ;
        public float aupLocalX;
        public float aupLocalY;
        public float aupLocalZ;

        internal bool HasAupPosition()
        {
            return positionEncodingVersion == AupPositionEncodingVersion;
        }

        public Vector3 GetPosition() => new Vector3(posX, posY, posZ);

        public void SetPosition(Vector3 position)
        {
            posX = position.x;
            posY = position.y;
            posZ = position.z;
        }

        internal AbsoluteUniversePosition GetAup()
        {
            return new AbsoluteUniversePosition
            {
                GridX = aupGridX,
                GridY = aupGridY,
                GridZ = aupGridZ,
                LocalX = aupLocalX,
                LocalY = aupLocalY,
                LocalZ = aupLocalZ
            };
        }

        internal void SetAup(in AbsoluteUniversePosition position)
        {
            positionEncodingVersion = AupPositionEncodingVersion;
            aupGridX = position.GridX;
            aupGridY = position.GridY;
            aupGridZ = position.GridZ;
            aupLocalX = position.LocalX;
            aupLocalY = position.LocalY;
            aupLocalZ = position.LocalZ;
        }

        internal static PDAMarkerEntryDTO SanitizeForPersistence(in PDAMarkerEntryDTO value)
        {
            PDAMarkerEntryDTO dto = value;
            dto.markerId = SaveData.SanitizePersistenceString(dto.markerId);
            dto.posX = SanitizeFinite(dto.posX, 0f);
            dto.posY = SanitizeFinite(dto.posY, 0f);
            dto.posZ = SanitizeFinite(dto.posZ, 0f);
            if (dto.positionEncodingVersion != AupPositionEncodingVersion)
            {
                dto.positionEncodingVersion = 0;
                dto.aupGridX = 0L;
                dto.aupGridY = 0L;
                dto.aupGridZ = 0L;
                dto.aupLocalX = 0f;
                dto.aupLocalY = 0f;
                dto.aupLocalZ = 0f;
                return dto;
            }

            dto.aupLocalX = SanitizeFinite(dto.aupLocalX, 0f);
            dto.aupLocalY = SanitizeFinite(dto.aupLocalY, 0f);
            dto.aupLocalZ = SanitizeFinite(dto.aupLocalZ, 0f);
            return dto;
        }

        internal static bool PersistenceEquals(in PDAMarkerEntryDTO left, in PDAMarkerEntryDTO right)
        {
            return left.markerHashId == right.markerHashId &&
                   left.markerId == right.markerId &&
                   left.titleHashId == right.titleHashId &&
                   left.title == right.title &&
                   left.iconType == right.iconType &&
                   left.posX == right.posX &&
                   left.posY == right.posY &&
                   left.posZ == right.posZ &&
                   left.visibleOnHud == right.visibleOnHud &&
                   left.positionEncodingVersion == right.positionEncodingVersion &&
                   left.aupGridX == right.aupGridX &&
                   left.aupGridY == right.aupGridY &&
                   left.aupGridZ == right.aupGridZ &&
                   left.aupLocalX == right.aupLocalX &&
                   left.aupLocalY == right.aupLocalY &&
                   left.aupLocalZ == right.aupLocalZ;
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) ? value : fallback;
        }
    }

    [Serializable]
    public struct PDAMarkerRegistryDTO
    {
        public int markerCount;
        public int nextSequence;
        public PDAMarkerEntryDTO[] entries;

        public const int MaxEntries = 64;

        public void EnsureCapacity()
        {
            SaveData.EnsureExactArrayCapacity(ref entries, MaxEntries);
        }
    }

    [Serializable]
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct PDAContextualAdvisoryDTO
    {
        [FieldOffset(0)] public int issuedFlags;
        [FieldOffset(4)] public int oxygenDeathCount;
        [FieldOffset(8)] public int inventoryFullAttemptCount;
        [FieldOffset(12)] public int pressureDeathCount;
        [FieldOffset(16)] public int baseEmergencyCount;
        [FieldOffset(20)] public int staleAirIncidentCount;
        [FieldOffset(24)] public int coldStressIncidentCount;
        [FieldOffset(28)] public int heatStressIncidentCount;
        [FieldOffset(32)] public float deepExposureSeconds;
        [FieldOffset(36)] public float coldStressExposureSeconds;
        [FieldOffset(40)] public float heatStressExposureSeconds;
        [FieldOffset(44)] private int _pad0;

        internal static PDAContextualAdvisoryDTO SanitizeForPersistence(in PDAContextualAdvisoryDTO value)
        {
            PDAContextualAdvisoryDTO dto = value;
            dto.oxygenDeathCount = Math.Max(0, dto.oxygenDeathCount);
            dto.inventoryFullAttemptCount = Math.Max(0, dto.inventoryFullAttemptCount);
            dto.pressureDeathCount = Math.Max(0, dto.pressureDeathCount);
            dto.baseEmergencyCount = Math.Max(0, dto.baseEmergencyCount);
            dto.staleAirIncidentCount = Math.Max(0, dto.staleAirIncidentCount);
            dto.coldStressIncidentCount = Math.Max(0, dto.coldStressIncidentCount);
            dto.heatStressIncidentCount = Math.Max(0, dto.heatStressIncidentCount);
            dto.deepExposureSeconds = SanitizeNonNegativeFinite(dto.deepExposureSeconds);
            dto.coldStressExposureSeconds = SanitizeNonNegativeFinite(dto.coldStressExposureSeconds);
            dto.heatStressExposureSeconds = SanitizeNonNegativeFinite(dto.heatStressExposureSeconds);
            dto._pad0 = 0;
            return dto;
        }

        internal static bool PersistenceEquals(in PDAContextualAdvisoryDTO left, in PDAContextualAdvisoryDTO right)
        {
            return left.issuedFlags == right.issuedFlags &&
                   left.oxygenDeathCount == right.oxygenDeathCount &&
                   left.inventoryFullAttemptCount == right.inventoryFullAttemptCount &&
                   left.pressureDeathCount == right.pressureDeathCount &&
                   left.baseEmergencyCount == right.baseEmergencyCount &&
                   left.staleAirIncidentCount == right.staleAirIncidentCount &&
                   left.coldStressIncidentCount == right.coldStressIncidentCount &&
                   left.heatStressIncidentCount == right.heatStressIncidentCount &&
                   left.deepExposureSeconds == right.deepExposureSeconds &&
                   left.coldStressExposureSeconds == right.coldStressExposureSeconds &&
                   left.heatStressExposureSeconds == right.heatStressExposureSeconds &&
                   left._pad0 == right._pad0;
        }

        private static float SanitizeNonNegativeFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) ? Mathf.Max(0f, value) : 0f;
        }
    }

    [Serializable]
    public struct ProceduralLorePlacementDTO
    {
        public string discoveryId;
        public string logId;
        public long chunkKey;
        public float posX;
        public float posY;
        public float posZ;

        public Vector3 GetPosition() => new Vector3(posX, posY, posZ);

        public void SetPosition(Vector3 position)
        {
            posX = position.x;
            posY = position.y;
            posZ = position.z;
        }

        internal static ProceduralLorePlacementDTO SanitizeForPersistence(in ProceduralLorePlacementDTO value)
        {
            ProceduralLorePlacementDTO dto = value;
            dto.discoveryId = SaveData.SanitizePersistenceString(dto.discoveryId);
            dto.logId = SaveData.SanitizePersistenceString(dto.logId);
            dto.posX = SanitizeFinite(dto.posX, 0f);
            dto.posY = SanitizeFinite(dto.posY, 0f);
            dto.posZ = SanitizeFinite(dto.posZ, 0f);
            return dto;
        }

        internal static bool PersistenceEquals(in ProceduralLorePlacementDTO left, in ProceduralLorePlacementDTO right)
        {
            return left.discoveryId == right.discoveryId &&
                   left.logId == right.logId &&
                   left.chunkKey == right.chunkKey &&
                   left.posX == right.posX &&
                   left.posY == right.posY &&
                   left.posZ == right.posZ;
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) ? value : fallback;
        }
    }

    [Serializable]
    public struct ProceduralLoreStateDTO
    {
        public int activeCount;
        public int nextSourceIndex;
        public ProceduralLorePlacementDTO[] activePlacements;

        public const int MaxActivePlacements = 12;

        public void EnsureCapacity()
        {
            SaveData.EnsureExactArrayCapacity(ref activePlacements, MaxActivePlacements);
        }
    }

    [Serializable]
    public struct AchievementRegistryDTO
    {
        public float swamDistanceMeters;
        public int craftedItemCount;
        public int discoveredBiomeCount;
        public int unlockedCount;
        public string[] unlockedIds;

        public const int MaxUnlockedAchievements = 32;

        public void EnsureCapacity()
        {
            SaveData.EnsureExactArrayCapacity(ref unlockedIds, MaxUnlockedAchievements);
        }
    }

    [Serializable]
    public struct RunModifiersDTO
    {
        public bool isPermadeath;
        public bool isNightmareMode;
        public bool isDailySeed;
        public bool runMarkedDead;
        public string dailySeedId;
    }

    [Serializable]
    public struct MetaCampaignDTO
    {
        public const int MaxGlobalVariables = 64;

        public int variableCount;
        public uint currentStageHash;
        public int currentStage;
        public int toxicityPermille;
        public uint[] variableHashes;
        public int[] variableValues;
        public byte flags;

        public static MetaCampaignDTO CreateDefault()
        {
            MetaCampaignDTO dto = default;
            dto.EnsureCapacity();
            return dto;
        }

        public void EnsureCapacity()
        {
            SaveData.EnsureExactArrayCapacity(ref variableHashes, MaxGlobalVariables);
            SaveData.EnsureExactArrayCapacity(ref variableValues, MaxGlobalVariables);

            int capacity = Math.Min(variableHashes.Length, variableValues.Length);
            variableCount = Math.Clamp(variableCount, 0, capacity);
            toxicityPermille = Math.Clamp(toxicityPermille, 0, 1000);
        }
    }

    [Serializable]
    public struct ResourceScarcityDTO
    {
        public const int MaxTrackedResources = 96;

        public int entryCount;
        public int[] itemHashIds;
        public string[] itemIds;
        public int[] collectedCounts;

        public void EnsureCapacity()
        {
            SaveData.EnsureExactArrayCapacity(ref itemHashIds, MaxTrackedResources);
            SaveData.EnsureExactArrayCapacity(ref itemIds, MaxTrackedResources);
            SaveData.EnsureExactArrayCapacity(ref collectedCounts, MaxTrackedResources);
        }
    }

    [Serializable]
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct EnvironmentalStrainDTO
    {
        [FieldOffset(0)] public float microplasticStrain;
        [FieldOffset(4)] public float generalPollution;
        [FieldOffset(8)] public int recycledPlasticItemCount;
        [FieldOffset(12)] public int discardedItemCount;

        internal static EnvironmentalStrainDTO SanitizeForPersistence(in EnvironmentalStrainDTO value)
        {
            EnvironmentalStrainDTO dto = value;
            dto.microplasticStrain = SanitizeNonNegativeFinite(dto.microplasticStrain);
            dto.generalPollution = SanitizeNonNegativeFinite(dto.generalPollution);
            dto.recycledPlasticItemCount = Math.Max(0, dto.recycledPlasticItemCount);
            dto.discardedItemCount = Math.Max(0, dto.discardedItemCount);
            return dto;
        }

        internal static bool PersistenceEquals(in EnvironmentalStrainDTO left, in EnvironmentalStrainDTO right)
        {
            return left.microplasticStrain == right.microplasticStrain &&
                   left.generalPollution == right.generalPollution &&
                   left.recycledPlasticItemCount == right.recycledPlasticItemCount &&
                   left.discardedItemCount == right.discardedItemCount;
        }

        private static float SanitizeNonNegativeFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) ? Mathf.Max(0f, value) : 0f;
        }
    }

    [Serializable]
    public struct EcosystemStateDTO
    {
        public const int MaxInfectedZones = 64;

        public int worldSeed;
        public int worldGenerationVersionId;
        public int infectedZoneCount;
        public long[] infectedChunkKeys;
        public float[] infectedSeverities;

        public void EnsureCapacity()
        {
            SaveData.EnsureExactArrayCapacity(ref infectedChunkKeys, MaxInfectedZones);
            SaveData.EnsureExactArrayCapacity(ref infectedSeverities, MaxInfectedZones);
        }
    }

    [Serializable]
    public struct ProceduralTerrainIdentityDTO
    {
        public const uint FlagsMacroGeologyPresent = 1u << 0;
        public const uint FlagsWaterCalibrationPresent = 1u << 1;
        public const uint FlagsDefaultChunkRange = 1u << 2;
        public const uint FlagsTerrainProviderIdentityPresent = 1u << 3;
        public const uint FlagsTerrainHeightPayloadPresent = 1u << 4;
        public const uint FlagsTerrainMaterialContractsPresent = 1u << 5;
        public const uint FlagsTerrainMesoContractsPresent = 1u << 6;

        public uint authoringSeed;
        public int runtimeSeed;
        public int worldGenerationVersionId;
        public uint macroArtifactVersion;
        public float macroChunkSizeMeters;
        public int chunkMinX;
        public int chunkMinZ;
        public int chunkMaxX;
        public int chunkMaxZ;
        public uint chunkArtifactRangeHash;
        public float selectedWaterLevelY;
        public float waterCalibrationTravelMeters;
        public uint waterCalibrationSourceHash;
        public uint terrainProviderFlags;
        public int heightCacheRevision;
        public uint terrainEntityHash;
        public uint surfaceMaterialContractVersion;
        public uint mesoDetailContractVersion;
        public uint detailEligibilityContractVersion;
        public uint mesoParamsHash;
        public uint flags;

        public bool HasMacroIdentity =>
            (flags & FlagsMacroGeologyPresent) != 0u ||
            macroArtifactVersion != 0u ||
            chunkArtifactRangeHash != 0u;
    }

    [Serializable]
    public struct ModuleDTO
    {
        public const int MaxSorterBufferedSlots = 8;
        public const int MaxRecyclerBufferedSlots = 8;
        public const int MaxRecyclerPendingYieldSlots = 16;
        public const int MaxCultivationSlots = 4;
        public const int MaxStorageCrateSlots = 32;
        public const ulong CultivationGeneticsSupportedMask = 0x000000000000000FUL;

        public string prefabId;
        public string slottedToolItemId;
        public string pipeInFlightItemId;
        public int pipeInFlightAmount;
        public float pipeTransitProgress;
        public float pipeExportTimerSeconds;
        public string drillBufferedItemId;
        public int drillBufferedAmount;
        public float drillCycleTimerSeconds;
        public int sorterBufferedSlotCount;
        public string[] sorterBufferedItemIds;
        public int[] sorterBufferedQuantities;
        public int recyclerBufferedSlotCount;
        public string[] recyclerBufferedItemIds;
        public int[] recyclerBufferedQuantities;
        public string recyclerActiveSourceItemId;
        public int recyclerPendingYieldSlotCount;
        public string[] recyclerPendingYieldItemIds;
        public int[] recyclerPendingYieldQuantities;
        public string fabricatorPendingOutputItemId;
        public int fabricatorPendingOutputQuantity;
        public int fabricatorPendingOutputTotalQuantity;
        public bool storageCrateContentsSerialized;
        public int storageCrateSlotCount;
        public string[] storageCrateItemIds;
        public int[] storageCrateQuantities;
        public float posX;
        public float posY;
        public float posZ;
        public float rotX;
        public float rotY;
        public float rotZ;
        public float rotW;
        public float integrity;
        public float repairIntegrityCap;
        public float airReserveNormalized;
        public float co2Normalized;
        public bool isFlooded;
        public byte failureMode;
        public byte health;
        public float floodedReefFloodSeconds;
        public bool interiorReefInfestationActive;
        public int cultivationSlotCount;
        public string[] cultivationSeedItemIds;
        public int[] cultivationSeedItemHashIds;
        public ulong[] cultivationGeneticsMasks;
        public float[] cultivationGrowth01;
        public float[] cultivationQuality01;

        public Vector3 GetPosition() => new Vector3(posX, posY, posZ);
        public Quaternion GetRotation() => new Quaternion(rotX, rotY, rotZ, rotW);

        public void EnsureNestedArrayCapacity()
        {
            SaveData.EnsureExactArrayCapacity(ref sorterBufferedItemIds, MaxSorterBufferedSlots);
            SaveData.EnsureExactArrayCapacity(ref sorterBufferedQuantities, MaxSorterBufferedSlots);
            SaveData.EnsureExactArrayCapacity(ref recyclerBufferedItemIds, MaxRecyclerBufferedSlots);
            SaveData.EnsureExactArrayCapacity(ref recyclerBufferedQuantities, MaxRecyclerBufferedSlots);
            SaveData.EnsureExactArrayCapacity(ref recyclerPendingYieldItemIds, MaxRecyclerPendingYieldSlots);
            SaveData.EnsureExactArrayCapacity(ref recyclerPendingYieldQuantities, MaxRecyclerPendingYieldSlots);
            SaveData.EnsureExactArrayCapacity(ref storageCrateItemIds, MaxStorageCrateSlots);
            SaveData.EnsureExactArrayCapacity(ref storageCrateQuantities, MaxStorageCrateSlots);
            SaveData.EnsureExactArrayCapacity(ref cultivationSeedItemIds, MaxCultivationSlots);
            SaveData.EnsureExactArrayCapacity(ref cultivationSeedItemHashIds, MaxCultivationSlots);
            SaveData.EnsureExactArrayCapacity(ref cultivationGeneticsMasks, MaxCultivationSlots);
            SaveData.EnsureExactArrayCapacity(ref cultivationGrowth01, MaxCultivationSlots);
            SaveData.EnsureExactArrayCapacity(ref cultivationQuality01, MaxCultivationSlots);
        }

        public bool HasNestedArrayCapacity()
        {
            return HasSorterSaveCapacity() &&
                   HasRecyclerSaveCapacity() &&
                   HasStorageCrateSaveCapacity() &&
                   HasCultivationSaveCapacity();
        }

        public bool HasSorterSaveCapacity()
        {
            return sorterBufferedItemIds != null &&
                   sorterBufferedItemIds.Length >= MaxSorterBufferedSlots &&
                   sorterBufferedQuantities != null &&
                   sorterBufferedQuantities.Length >= MaxSorterBufferedSlots;
        }

        public bool HasRecyclerSaveCapacity()
        {
            return recyclerBufferedItemIds != null &&
                   recyclerBufferedItemIds.Length >= MaxRecyclerBufferedSlots &&
                   recyclerBufferedQuantities != null &&
                   recyclerBufferedQuantities.Length >= MaxRecyclerBufferedSlots &&
                   recyclerPendingYieldItemIds != null &&
                   recyclerPendingYieldItemIds.Length >= MaxRecyclerPendingYieldSlots &&
                   recyclerPendingYieldQuantities != null &&
                   recyclerPendingYieldQuantities.Length >= MaxRecyclerPendingYieldSlots;
        }

        public bool HasStorageCrateSaveCapacity()
        {
            return storageCrateItemIds != null &&
                   storageCrateItemIds.Length >= MaxStorageCrateSlots &&
                   storageCrateQuantities != null &&
                   storageCrateQuantities.Length >= MaxStorageCrateSlots;
        }

        public bool HasCultivationSaveCapacity()
        {
            return cultivationSeedItemIds != null &&
                   cultivationSeedItemIds.Length >= MaxCultivationSlots &&
                   cultivationSeedItemHashIds != null &&
                   cultivationSeedItemHashIds.Length >= MaxCultivationSlots &&
                   cultivationGeneticsMasks != null &&
                   cultivationGeneticsMasks.Length >= MaxCultivationSlots &&
                   cultivationGrowth01 != null &&
                   cultivationGrowth01.Length >= MaxCultivationSlots &&
                   cultivationQuality01 != null &&
                   cultivationQuality01.Length >= MaxCultivationSlots;
        }

        public void ResetForConstructionSave()
        {
            string[] sorterItemIds = sorterBufferedItemIds;
            int[] sorterQuantities = sorterBufferedQuantities;
            string[] recyclerItemIds = recyclerBufferedItemIds;
            int[] recyclerQuantities = recyclerBufferedQuantities;
            string[] recyclerYieldItemIds = recyclerPendingYieldItemIds;
            int[] recyclerYieldQuantities = recyclerPendingYieldQuantities;
            string[] storageItemIds = storageCrateItemIds;
            int[] storageQuantities = storageCrateQuantities;
            string[] seedItemIds = cultivationSeedItemIds;
            int[] seedItemHashIds = cultivationSeedItemHashIds;
            ulong[] geneticsMasks = cultivationGeneticsMasks;
            float[] growthValues = cultivationGrowth01;
            float[] qualityValues = cultivationQuality01;

            this = default;

            sorterBufferedItemIds = sorterItemIds;
            sorterBufferedQuantities = sorterQuantities;
            recyclerBufferedItemIds = recyclerItemIds;
            recyclerBufferedQuantities = recyclerQuantities;
            recyclerPendingYieldItemIds = recyclerYieldItemIds;
            recyclerPendingYieldQuantities = recyclerYieldQuantities;
            storageCrateItemIds = storageItemIds;
            storageCrateQuantities = storageQuantities;
            cultivationSeedItemIds = seedItemIds;
            cultivationSeedItemHashIds = seedItemHashIds;
            cultivationGeneticsMasks = geneticsMasks;
            cultivationGrowth01 = growthValues;
            cultivationQuality01 = qualityValues;

            ClearArray(sorterBufferedItemIds);
            ClearArray(sorterBufferedQuantities);
            ClearArray(recyclerBufferedItemIds);
            ClearArray(recyclerBufferedQuantities);
            ClearArray(recyclerPendingYieldItemIds);
            ClearArray(recyclerPendingYieldQuantities);
            ClearArray(storageCrateItemIds);
            ClearArray(storageCrateQuantities);
            ClearArray(cultivationSeedItemIds);
            ClearArray(cultivationSeedItemHashIds);
            ClearArray(cultivationGeneticsMasks);
            ClearArray(cultivationGrowth01);
            ClearArray(cultivationQuality01);
        }

        public void SetPosition(Vector3 pos)
        {
            posX = pos.x; posY = pos.y; posZ = pos.z;
        }

        public void SetRotation(Quaternion rot)
        {
            rotX = rot.x; rotY = rot.y; rotZ = rot.z; rotW = rot.w;
        }

        internal static ModuleDTO SanitizeForPersistence(in ModuleDTO value)
        {
            ModuleDTO dto = SanitizeScalarsForPersistence(in value);
            SanitizeStringArrayCopyOnWrite(ref dto.sorterBufferedItemIds, dto.sorterBufferedSlotCount, MaxSorterBufferedSlots);
            SanitizeSorterQuantitiesCopyOnWrite(ref dto);
            SanitizeStringArrayCopyOnWrite(ref dto.recyclerBufferedItemIds, dto.recyclerBufferedSlotCount, MaxRecyclerBufferedSlots);
            SanitizeRecyclerBufferedQuantitiesCopyOnWrite(ref dto);
            SanitizeStringArrayCopyOnWrite(ref dto.recyclerPendingYieldItemIds, dto.recyclerPendingYieldSlotCount, MaxRecyclerPendingYieldSlots);
            SanitizeRecyclerPendingYieldQuantitiesCopyOnWrite(ref dto);
            SanitizeStringArrayCopyOnWrite(ref dto.storageCrateItemIds, dto.storageCrateSlotCount, MaxStorageCrateSlots);
            SanitizeStorageCrateQuantitiesCopyOnWrite(ref dto);
            SanitizeStringArrayCopyOnWrite(ref dto.cultivationSeedItemIds, dto.cultivationSlotCount, MaxCultivationSlots);
            SanitizeCultivationGeneticsCopyOnWrite(ref dto);
            SanitizeCultivationProgressCopyOnWrite(ref dto);
            return dto;
        }

        internal static ModuleDTO SanitizeScalarsForPersistence(in ModuleDTO value)
        {
            ModuleDTO dto = value;
            dto.prefabId = SanitizePersistenceId(dto.prefabId);
            dto.slottedToolItemId = SanitizePersistenceId(dto.slottedToolItemId);
            dto.pipeInFlightItemId = SanitizePersistenceId(dto.pipeInFlightItemId);
            dto.pipeInFlightAmount = Math.Max(0, dto.pipeInFlightAmount);
            dto.pipeTransitProgress = SanitizeUnit01(dto.pipeTransitProgress);
            dto.pipeExportTimerSeconds = SanitizeNonNegativeFinite(dto.pipeExportTimerSeconds);
            dto.drillBufferedItemId = SanitizePersistenceId(dto.drillBufferedItemId);
            dto.drillBufferedAmount = Math.Max(0, dto.drillBufferedAmount);
            dto.drillCycleTimerSeconds = SanitizeNonNegativeFinite(dto.drillCycleTimerSeconds);
            dto.sorterBufferedSlotCount = Math.Clamp(dto.sorterBufferedSlotCount, 0, MaxSorterBufferedSlots);
            dto.recyclerBufferedSlotCount = Math.Clamp(dto.recyclerBufferedSlotCount, 0, MaxRecyclerBufferedSlots);
            dto.recyclerActiveSourceItemId = SanitizePersistenceId(dto.recyclerActiveSourceItemId);
            dto.recyclerPendingYieldSlotCount = Math.Clamp(dto.recyclerPendingYieldSlotCount, 0, MaxRecyclerPendingYieldSlots);
            dto.fabricatorPendingOutputItemId = SanitizePersistenceId(dto.fabricatorPendingOutputItemId);
            dto.fabricatorPendingOutputQuantity = Math.Max(0, dto.fabricatorPendingOutputQuantity);
            dto.fabricatorPendingOutputTotalQuantity = dto.fabricatorPendingOutputQuantity > 0
                ? Math.Max(dto.fabricatorPendingOutputQuantity, dto.fabricatorPendingOutputTotalQuantity)
                : 0;
            if (dto.fabricatorPendingOutputQuantity <= 0)
                dto.fabricatorPendingOutputItemId = string.Empty;
            dto.storageCrateSlotCount = dto.storageCrateContentsSerialized
                ? Math.Clamp(dto.storageCrateSlotCount, 0, MaxStorageCrateSlots)
                : 0;
            dto.cultivationSlotCount = Math.Clamp(dto.cultivationSlotCount, 0, MaxCultivationSlots);
            dto.posX = SanitizeFinite(dto.posX, 0f);
            dto.posY = SanitizeFinite(dto.posY, 0f);
            dto.posZ = SanitizeFinite(dto.posZ, 0f);
            SanitizeRotation(ref dto);
            dto.integrity = SanitizeNonNegativeFinite(dto.integrity);
            dto.repairIntegrityCap = SanitizeNonNegativeFinite(dto.repairIntegrityCap);
            dto.airReserveNormalized = SanitizeUnit01(dto.airReserveNormalized);
            dto.co2Normalized = SanitizeUnit01(dto.co2Normalized);
            dto.failureMode = SanitizeFailureMode(dto.failureMode);
            dto.floodedReefFloodSeconds = SanitizeNonNegativeFinite(dto.floodedReefFloodSeconds);
            return dto;
        }

        internal static string SanitizePersistenceId(string value)
        {
            return SaveData.SanitizePersistenceString(value);
        }

        internal static bool SanitizeForPersistenceInPlace(ref ModuleDTO value)
        {
            ModuleDTO safeScalars = SanitizeScalarsForPersistence(in value);
            bool changed = !PersistenceScalarsEqual(in value, in safeScalars);
            value = safeScalars;
            changed |= SanitizeStringArrayInPlace(value.sorterBufferedItemIds, value.sorterBufferedSlotCount, MaxSorterBufferedSlots);
            changed |= SanitizeSorterQuantitiesInPlace(ref value);
            changed |= SanitizeStringArrayInPlace(value.recyclerBufferedItemIds, value.recyclerBufferedSlotCount, MaxRecyclerBufferedSlots);
            changed |= SanitizeRecyclerBufferedQuantitiesInPlace(ref value);
            changed |= SanitizeStringArrayInPlace(value.recyclerPendingYieldItemIds, value.recyclerPendingYieldSlotCount, MaxRecyclerPendingYieldSlots);
            changed |= SanitizeRecyclerPendingYieldQuantitiesInPlace(ref value);
            changed |= SanitizeStringArrayInPlace(value.storageCrateItemIds, value.storageCrateSlotCount, MaxStorageCrateSlots);
            changed |= SanitizeStorageCrateQuantitiesInPlace(ref value);
            changed |= SanitizeStringArrayInPlace(value.cultivationSeedItemIds, value.cultivationSlotCount, MaxCultivationSlots);
            changed |= SanitizeCultivationGeneticsInPlace(ref value);
            changed |= SanitizeCultivationProgressInPlace(ref value);
            return changed;
        }

        internal static bool PersistenceEquals(in ModuleDTO left, in ModuleDTO right)
        {
            int leftSorterSlotCount = ResolveSorterPersistenceSlotCount(in left);
            int rightSorterSlotCount = ResolveSorterPersistenceSlotCount(in right);
            int leftRecyclerSlotCount = ResolveRecyclerBufferPersistenceSlotCount(in left);
            int rightRecyclerSlotCount = ResolveRecyclerBufferPersistenceSlotCount(in right);
            int leftRecyclerYieldSlotCount = ResolveRecyclerPendingYieldPersistenceSlotCount(in left);
            int rightRecyclerYieldSlotCount = ResolveRecyclerPendingYieldPersistenceSlotCount(in right);
            int leftStorageCrateSlotCount = ResolveStorageCratePersistenceSlotCount(in left);
            int rightStorageCrateSlotCount = ResolveStorageCratePersistenceSlotCount(in right);
            int leftCultivationSlotCount = ResolveCultivationPersistenceSlotCount(in left);
            int rightCultivationSlotCount = ResolveCultivationPersistenceSlotCount(in right);

            return left.prefabId == right.prefabId &&
                   left.slottedToolItemId == right.slottedToolItemId &&
                   left.pipeInFlightItemId == right.pipeInFlightItemId &&
                   left.pipeInFlightAmount == right.pipeInFlightAmount &&
                   left.pipeTransitProgress == right.pipeTransitProgress &&
                   left.pipeExportTimerSeconds == right.pipeExportTimerSeconds &&
                   left.drillBufferedItemId == right.drillBufferedItemId &&
                   left.drillBufferedAmount == right.drillBufferedAmount &&
                   left.drillCycleTimerSeconds == right.drillCycleTimerSeconds &&
                   leftSorterSlotCount == rightSorterSlotCount &&
                   StringArrayPrefixEquals(left.sorterBufferedItemIds, right.sorterBufferedItemIds, leftSorterSlotCount) &&
                   IntArrayPrefixEquals(left.sorterBufferedQuantities, right.sorterBufferedQuantities, leftSorterSlotCount) &&
                   leftRecyclerSlotCount == rightRecyclerSlotCount &&
                   StringArrayPrefixEquals(left.recyclerBufferedItemIds, right.recyclerBufferedItemIds, leftRecyclerSlotCount) &&
                   IntArrayPrefixEquals(left.recyclerBufferedQuantities, right.recyclerBufferedQuantities, leftRecyclerSlotCount) &&
                   left.recyclerActiveSourceItemId == right.recyclerActiveSourceItemId &&
                   leftRecyclerYieldSlotCount == rightRecyclerYieldSlotCount &&
                   StringArrayPrefixEquals(left.recyclerPendingYieldItemIds, right.recyclerPendingYieldItemIds, leftRecyclerYieldSlotCount) &&
                   IntArrayPrefixEquals(left.recyclerPendingYieldQuantities, right.recyclerPendingYieldQuantities, leftRecyclerYieldSlotCount) &&
                   left.fabricatorPendingOutputItemId == right.fabricatorPendingOutputItemId &&
                   left.fabricatorPendingOutputQuantity == right.fabricatorPendingOutputQuantity &&
                   left.fabricatorPendingOutputTotalQuantity == right.fabricatorPendingOutputTotalQuantity &&
                   left.storageCrateContentsSerialized == right.storageCrateContentsSerialized &&
                   leftStorageCrateSlotCount == rightStorageCrateSlotCount &&
                   StringArrayPrefixEquals(left.storageCrateItemIds, right.storageCrateItemIds, leftStorageCrateSlotCount) &&
                   IntArrayPrefixEquals(left.storageCrateQuantities, right.storageCrateQuantities, leftStorageCrateSlotCount) &&
                   left.posX == right.posX &&
                   left.posY == right.posY &&
                   left.posZ == right.posZ &&
                   left.rotX == right.rotX &&
                   left.rotY == right.rotY &&
                   left.rotZ == right.rotZ &&
                   left.rotW == right.rotW &&
                   left.integrity == right.integrity &&
                   left.repairIntegrityCap == right.repairIntegrityCap &&
                   left.airReserveNormalized == right.airReserveNormalized &&
                   left.co2Normalized == right.co2Normalized &&
                   left.isFlooded == right.isFlooded &&
                   left.failureMode == right.failureMode &&
                   left.health == right.health &&
                   left.floodedReefFloodSeconds == right.floodedReefFloodSeconds &&
                   left.interiorReefInfestationActive == right.interiorReefInfestationActive &&
                   leftCultivationSlotCount == rightCultivationSlotCount &&
                   StringArrayPrefixEquals(left.cultivationSeedItemIds, right.cultivationSeedItemIds, leftCultivationSlotCount) &&
                   IntArrayPrefixEquals(left.cultivationSeedItemHashIds, right.cultivationSeedItemHashIds, leftCultivationSlotCount) &&
                   ULongArrayPrefixEquals(left.cultivationGeneticsMasks, right.cultivationGeneticsMasks, leftCultivationSlotCount) &&
                   FloatArrayPrefixEquals(left.cultivationGrowth01, right.cultivationGrowth01, leftCultivationSlotCount) &&
                   FloatArrayPrefixEquals(left.cultivationQuality01, right.cultivationQuality01, leftCultivationSlotCount);
        }

        private static bool PersistenceScalarsEqual(in ModuleDTO left, in ModuleDTO right)
        {
            return left.prefabId == right.prefabId &&
                   left.slottedToolItemId == right.slottedToolItemId &&
                   left.pipeInFlightItemId == right.pipeInFlightItemId &&
                   left.pipeInFlightAmount == right.pipeInFlightAmount &&
                   left.pipeTransitProgress == right.pipeTransitProgress &&
                   left.pipeExportTimerSeconds == right.pipeExportTimerSeconds &&
                   left.drillBufferedItemId == right.drillBufferedItemId &&
                   left.drillBufferedAmount == right.drillBufferedAmount &&
                   left.drillCycleTimerSeconds == right.drillCycleTimerSeconds &&
                   left.sorterBufferedSlotCount == right.sorterBufferedSlotCount &&
                   left.recyclerBufferedSlotCount == right.recyclerBufferedSlotCount &&
                   left.recyclerActiveSourceItemId == right.recyclerActiveSourceItemId &&
                   left.recyclerPendingYieldSlotCount == right.recyclerPendingYieldSlotCount &&
                   left.fabricatorPendingOutputItemId == right.fabricatorPendingOutputItemId &&
                   left.fabricatorPendingOutputQuantity == right.fabricatorPendingOutputQuantity &&
                   left.fabricatorPendingOutputTotalQuantity == right.fabricatorPendingOutputTotalQuantity &&
                   left.storageCrateContentsSerialized == right.storageCrateContentsSerialized &&
                   left.storageCrateSlotCount == right.storageCrateSlotCount &&
                   left.posX == right.posX &&
                   left.posY == right.posY &&
                   left.posZ == right.posZ &&
                   left.rotX == right.rotX &&
                   left.rotY == right.rotY &&
                   left.rotZ == right.rotZ &&
                   left.rotW == right.rotW &&
                   left.integrity == right.integrity &&
                   left.repairIntegrityCap == right.repairIntegrityCap &&
                   left.airReserveNormalized == right.airReserveNormalized &&
                   left.co2Normalized == right.co2Normalized &&
                   left.failureMode == right.failureMode &&
                   left.floodedReefFloodSeconds == right.floodedReefFloodSeconds &&
                   left.cultivationSlotCount == right.cultivationSlotCount;
        }

        private static int ResolveSorterPersistenceSlotCount(in ModuleDTO value)
        {
            int upperBound = MaxSorterBufferedSlots;
            upperBound = Math.Min(upperBound, value.sorterBufferedItemIds != null ? value.sorterBufferedItemIds.Length : 0);
            upperBound = Math.Min(upperBound, value.sorterBufferedQuantities != null ? value.sorterBufferedQuantities.Length : 0);
            return Math.Clamp(value.sorterBufferedSlotCount, 0, upperBound);
        }

        private static int ResolveRecyclerBufferPersistenceSlotCount(in ModuleDTO value)
        {
            int upperBound = MaxRecyclerBufferedSlots;
            upperBound = Math.Min(upperBound, value.recyclerBufferedItemIds != null ? value.recyclerBufferedItemIds.Length : 0);
            upperBound = Math.Min(upperBound, value.recyclerBufferedQuantities != null ? value.recyclerBufferedQuantities.Length : 0);
            return Math.Clamp(value.recyclerBufferedSlotCount, 0, upperBound);
        }

        private static int ResolveRecyclerPendingYieldPersistenceSlotCount(in ModuleDTO value)
        {
            int upperBound = MaxRecyclerPendingYieldSlots;
            upperBound = Math.Min(upperBound, value.recyclerPendingYieldItemIds != null ? value.recyclerPendingYieldItemIds.Length : 0);
            upperBound = Math.Min(upperBound, value.recyclerPendingYieldQuantities != null ? value.recyclerPendingYieldQuantities.Length : 0);
            return Math.Clamp(value.recyclerPendingYieldSlotCount, 0, upperBound);
        }

        private static int ResolveStorageCratePersistenceSlotCount(in ModuleDTO value)
        {
            if (!value.storageCrateContentsSerialized)
                return 0;

            int upperBound = MaxStorageCrateSlots;
            upperBound = Math.Min(upperBound, value.storageCrateItemIds != null ? value.storageCrateItemIds.Length : 0);
            upperBound = Math.Min(upperBound, value.storageCrateQuantities != null ? value.storageCrateQuantities.Length : 0);
            return Math.Clamp(value.storageCrateSlotCount, 0, upperBound);
        }

        private static int ResolveCultivationPersistenceSlotCount(in ModuleDTO value)
        {
            int upperBound = MaxCultivationSlots;
            upperBound = Math.Min(upperBound, value.cultivationSeedItemIds != null ? value.cultivationSeedItemIds.Length : 0);
            upperBound = Math.Min(upperBound, value.cultivationGeneticsMasks != null ? value.cultivationGeneticsMasks.Length : 0);
            upperBound = Math.Min(upperBound, value.cultivationGrowth01 != null ? value.cultivationGrowth01.Length : 0);
            upperBound = Math.Min(upperBound, value.cultivationQuality01 != null ? value.cultivationQuality01.Length : 0);
            return Math.Clamp(value.cultivationSlotCount, 0, upperBound);
        }

        private static void SanitizeRotation(ref ModuleDTO dto)
        {
            if (!IsFinite(dto.rotX) || !IsFinite(dto.rotY) || !IsFinite(dto.rotZ) || !IsFinite(dto.rotW))
            {
                dto.rotX = 0f;
                dto.rotY = 0f;
                dto.rotZ = 0f;
                dto.rotW = 1f;
                return;
            }

            float lengthSq = dto.rotX * dto.rotX + dto.rotY * dto.rotY + dto.rotZ * dto.rotZ + dto.rotW * dto.rotW;
            if (!IsFinite(lengthSq) || lengthSq <= 0.000001f)
            {
                dto.rotX = 0f;
                dto.rotY = 0f;
                dto.rotZ = 0f;
                dto.rotW = 1f;
                return;
            }

            float invLength = 1f / Mathf.Sqrt(lengthSq);
            dto.rotX *= invLength;
            dto.rotY *= invLength;
            dto.rotZ *= invLength;
            dto.rotW *= invLength;
        }

        private static void SanitizeSorterQuantitiesCopyOnWrite(ref ModuleDTO dto)
        {
            int[] values = dto.sorterBufferedQuantities;
            if (values == null)
                return;

            int[] replacement = null;
            int count = Math.Min(dto.sorterBufferedSlotCount, Math.Min(MaxSorterBufferedSlots, values.Length));
            for (int i = 0; i < count; i++)
            {
                int safeValue = Math.Max(0, values[i]);
                if (safeValue == values[i])
                    continue;

                replacement ??= (int[])values.Clone();
                replacement[i] = safeValue;
            }

            if (replacement != null)
                dto.sorterBufferedQuantities = replacement;
        }

        private static bool SanitizeSorterQuantitiesInPlace(ref ModuleDTO dto)
        {
            int[] values = dto.sorterBufferedQuantities;
            if (values == null)
                return false;

            bool changed = false;
            int count = Math.Min(dto.sorterBufferedSlotCount, Math.Min(MaxSorterBufferedSlots, values.Length));
            for (int i = 0; i < count; i++)
            {
                int safeValue = Math.Max(0, values[i]);
                if (safeValue == values[i])
                    continue;

                values[i] = safeValue;
                changed = true;
            }

            return changed;
        }

        private static void SanitizeRecyclerBufferedQuantitiesCopyOnWrite(ref ModuleDTO dto)
        {
            int[] values = dto.recyclerBufferedQuantities;
            if (values == null)
                return;

            int[] replacement = null;
            int count = Math.Min(dto.recyclerBufferedSlotCount, Math.Min(MaxRecyclerBufferedSlots, values.Length));
            for (int i = 0; i < count; i++)
            {
                if (values[i] != 0)
                    continue;

                replacement ??= (int[])values.Clone();
                replacement[i] = 0;
            }

            if (replacement != null)
                dto.recyclerBufferedQuantities = replacement;
        }

        private static bool SanitizeRecyclerBufferedQuantitiesInPlace(ref ModuleDTO dto)
        {
            int[] values = dto.recyclerBufferedQuantities;
            if (values == null)
                return false;

            bool changed = false;
            int count = Math.Min(dto.recyclerBufferedSlotCount, Math.Min(MaxRecyclerBufferedSlots, values.Length));
            for (int i = 0; i < count; i++)
            {
                if (values[i] != 0)
                    continue;

                values[i] = 0;
                changed = true;
            }

            return changed;
        }

        private static void SanitizeRecyclerPendingYieldQuantitiesCopyOnWrite(ref ModuleDTO dto)
        {
            int[] values = dto.recyclerPendingYieldQuantities;
            if (values == null)
                return;

            int[] replacement = null;
            int count = Math.Min(dto.recyclerPendingYieldSlotCount, Math.Min(MaxRecyclerPendingYieldSlots, values.Length));
            for (int i = 0; i < count; i++)
            {
                int safeValue = Math.Max(0, values[i]);
                if (safeValue == values[i])
                    continue;

                replacement ??= (int[])values.Clone();
                replacement[i] = safeValue;
            }

            if (replacement != null)
                dto.recyclerPendingYieldQuantities = replacement;
        }

        private static bool SanitizeRecyclerPendingYieldQuantitiesInPlace(ref ModuleDTO dto)
        {
            int[] values = dto.recyclerPendingYieldQuantities;
            if (values == null)
                return false;

            bool changed = false;
            int count = Math.Min(dto.recyclerPendingYieldSlotCount, Math.Min(MaxRecyclerPendingYieldSlots, values.Length));
            for (int i = 0; i < count; i++)
            {
                int safeValue = Math.Max(0, values[i]);
                if (safeValue == values[i])
                    continue;

                values[i] = safeValue;
                changed = true;
            }

            return changed;
        }

        private static void SanitizeStorageCrateQuantitiesCopyOnWrite(ref ModuleDTO dto)
        {
            int[] values = dto.storageCrateQuantities;
            if (values == null)
                return;

            int[] replacement = null;
            int count = Math.Min(dto.storageCrateSlotCount, Math.Min(MaxStorageCrateSlots, values.Length));
            for (int i = 0; i < count; i++)
            {
                int safeValue = Math.Max(0, values[i]);
                if (safeValue == values[i])
                    continue;

                replacement ??= (int[])values.Clone();
                replacement[i] = safeValue;
            }

            if (replacement != null)
                dto.storageCrateQuantities = replacement;
        }

        private static bool SanitizeStorageCrateQuantitiesInPlace(ref ModuleDTO dto)
        {
            int[] values = dto.storageCrateQuantities;
            if (values == null)
                return false;

            bool changed = false;
            int count = Math.Min(dto.storageCrateSlotCount, Math.Min(MaxStorageCrateSlots, values.Length));
            for (int i = 0; i < count; i++)
            {
                int safeValue = Math.Max(0, values[i]);
                if (safeValue == values[i])
                    continue;

                values[i] = safeValue;
                changed = true;
            }

            return changed;
        }

        private static void SanitizeStringArrayCopyOnWrite(ref string[] values, int count, int maxCount)
        {
            if (values == null)
                return;

            string[] replacement = null;
            int safeCount = Math.Min(count, Math.Min(maxCount, values.Length));
            for (int i = 0; i < safeCount; i++)
            {
                string safeValue = SanitizePersistenceId(values[i]);
                if (values[i] == safeValue)
                    continue;

                replacement ??= (string[])values.Clone();
                replacement[i] = safeValue;
            }

            if (replacement != null)
                values = replacement;
        }

        private static bool SanitizeStringArrayInPlace(string[] values, int count, int maxCount)
        {
            if (values == null)
                return false;

            bool changed = false;
            int safeCount = Math.Min(count, Math.Min(maxCount, values.Length));
            for (int i = 0; i < safeCount; i++)
            {
                string safeValue = SanitizePersistenceId(values[i]);
                if (values[i] == safeValue)
                    continue;

                values[i] = safeValue;
                changed = true;
            }

            return changed;
        }

        private static ulong SanitizeCultivationGeneticsMask(ulong geneticsMask)
        {
            return geneticsMask & CultivationGeneticsSupportedMask;
        }

        private static void SanitizeCultivationGeneticsCopyOnWrite(ref ModuleDTO dto)
        {
            ulong[] values = dto.cultivationGeneticsMasks;
            if (values == null)
                return;

            ulong[] replacement = null;
            int count = Math.Min(dto.cultivationSlotCount, Math.Min(MaxCultivationSlots, values.Length));
            for (int i = 0; i < count; i++)
            {
                ulong safeValue = SanitizeCultivationGeneticsMask(values[i]);
                if (safeValue == values[i])
                    continue;

                replacement ??= (ulong[])values.Clone();
                replacement[i] = safeValue;
            }

            if (replacement != null)
                dto.cultivationGeneticsMasks = replacement;
        }

        private static bool SanitizeCultivationGeneticsInPlace(ref ModuleDTO dto)
        {
            ulong[] values = dto.cultivationGeneticsMasks;
            if (values == null)
                return false;

            bool changed = false;
            int count = Math.Min(dto.cultivationSlotCount, Math.Min(MaxCultivationSlots, values.Length));
            for (int i = 0; i < count; i++)
            {
                ulong safeValue = SanitizeCultivationGeneticsMask(values[i]);
                if (safeValue == values[i])
                    continue;

                values[i] = safeValue;
                changed = true;
            }

            return changed;
        }

        private static void SanitizeCultivationProgressCopyOnWrite(ref ModuleDTO dto)
        {
            int count = dto.cultivationSlotCount;
            float[] growthValues = dto.cultivationGrowth01;
            if (growthValues != null)
            {
                float[] replacement = null;
                int growthCount = Math.Min(count, Math.Min(MaxCultivationSlots, growthValues.Length));
                for (int i = 0; i < growthCount; i++)
                {
                    float safeValue = SanitizeUnit01(growthValues[i]);
                    if (safeValue == growthValues[i])
                        continue;

                    replacement ??= (float[])growthValues.Clone();
                    replacement[i] = safeValue;
                }

                if (replacement != null)
                    dto.cultivationGrowth01 = replacement;
            }

            float[] qualityValues = dto.cultivationQuality01;
            if (qualityValues != null)
            {
                float[] replacement = null;
                int qualityCount = Math.Min(count, Math.Min(MaxCultivationSlots, qualityValues.Length));
                for (int i = 0; i < qualityCount; i++)
                {
                    float safeValue = SanitizeUnit01(qualityValues[i]);
                    if (safeValue == qualityValues[i])
                        continue;

                    replacement ??= (float[])qualityValues.Clone();
                    replacement[i] = safeValue;
                }

                if (replacement != null)
                    dto.cultivationQuality01 = replacement;
            }
        }

        private static bool SanitizeCultivationProgressInPlace(ref ModuleDTO dto)
        {
            bool changed = false;
            int count = dto.cultivationSlotCount;
            float[] growthValues = dto.cultivationGrowth01;
            if (growthValues != null)
            {
                int growthCount = Math.Min(count, Math.Min(MaxCultivationSlots, growthValues.Length));
                for (int i = 0; i < growthCount; i++)
                {
                    float safeValue = SanitizeUnit01(growthValues[i]);
                    if (safeValue == growthValues[i])
                        continue;

                    growthValues[i] = safeValue;
                    changed = true;
                }
            }

            float[] qualityValues = dto.cultivationQuality01;
            if (qualityValues != null)
            {
                int qualityCount = Math.Min(count, Math.Min(MaxCultivationSlots, qualityValues.Length));
                for (int i = 0; i < qualityCount; i++)
                {
                    float safeValue = SanitizeUnit01(qualityValues[i]);
                    if (safeValue == qualityValues[i])
                        continue;

                    qualityValues[i] = safeValue;
                    changed = true;
                }
            }

            return changed;
        }

        private static bool IntArrayPrefixEquals(int[] left, int[] right, int maxCount)
        {
            if (maxCount <= 0)
                return true;
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null)
                return false;

            int count = Math.Min(maxCount, Math.Min(left.Length, right.Length));
            if (count != maxCount)
                return false;

            for (int i = 0; i < count; i++)
            {
                if (left[i] != right[i])
                    return false;
            }

            return true;
        }

        private static bool StringArrayPrefixEquals(string[] left, string[] right, int maxCount)
        {
            if (maxCount <= 0)
                return true;
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null)
                return false;

            int count = Math.Min(maxCount, Math.Min(left.Length, right.Length));
            if (count != maxCount)
                return false;

            for (int i = 0; i < count; i++)
            {
                if (left[i] != right[i])
                    return false;
            }

            return true;
        }

        private static bool ULongArrayPrefixEquals(ulong[] left, ulong[] right, int maxCount)
        {
            if (maxCount <= 0)
                return true;
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null)
                return false;

            int count = Math.Min(maxCount, Math.Min(left.Length, right.Length));
            if (count != maxCount)
                return false;

            for (int i = 0; i < count; i++)
            {
                if (left[i] != right[i])
                    return false;
            }

            return true;
        }

        private static bool FloatArrayPrefixEquals(float[] left, float[] right, int maxCount)
        {
            if (maxCount <= 0)
                return true;
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null)
                return false;

            int count = Math.Min(maxCount, Math.Min(left.Length, right.Length));
            if (count != maxCount)
                return false;

            for (int i = 0; i < count; i++)
            {
                if (left[i] != right[i])
                    return false;
            }

            return true;
        }

        private static float SanitizeNonNegativeFinite(float value)
        {
            return IsFinite(value) ? Mathf.Max(0f, value) : 0f;
        }

        private static float SanitizeUnit01(float value)
        {
            return IsFinite(value) ? Mathf.Clamp01(value) : 0f;
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static byte SanitizeFailureMode(byte value)
        {
            return value <= SaveData.ModuleFailureModeMaxKnown ? value : SaveData.ModuleFailureModeNone;
        }

        private static void ClearArray<T>(T[] values)
        {
            if (values == null || values.Length == 0)
                return;

            Array.Clear(values, 0, values.Length);
        }
    }

    [Serializable]
    public struct ModuleGraphNodeDTO
    {
        public string prefabId;
        public int moduleHashId;
        public long aupGridX;
        public long aupGridY;
        public long aupGridZ;
        public float aupLocalX;
        public float aupLocalY;
        public float aupLocalZ;
        public float rotX;
        public float rotY;
        public float rotZ;
        public float rotW;

        internal AbsoluteUniversePosition GetAup()
        {
            return new AbsoluteUniversePosition
            {
                GridX = aupGridX,
                GridY = aupGridY,
                GridZ = aupGridZ,
                LocalX = aupLocalX,
                LocalY = aupLocalY,
                LocalZ = aupLocalZ
            };
        }

        public Quaternion GetRotation() => new Quaternion(rotX, rotY, rotZ, rotW);

        internal void SetAup(in AbsoluteUniversePosition aup)
        {
            aupGridX = aup.GridX;
            aupGridY = aup.GridY;
            aupGridZ = aup.GridZ;
            aupLocalX = aup.LocalX;
            aupLocalY = aup.LocalY;
            aupLocalZ = aup.LocalZ;
        }

        public void SetRotation(Quaternion rot)
        {
            rotX = rot.x;
            rotY = rot.y;
            rotZ = rot.z;
            rotW = rot.w;
        }

        internal static ModuleGraphNodeDTO SanitizeForPersistence(in ModuleGraphNodeDTO value)
        {
            ModuleGraphNodeDTO dto = value;
            dto.prefabId = ModuleDTO.SanitizePersistenceId(dto.prefabId);
            dto.aupLocalX = SanitizeFinite(dto.aupLocalX, 0f);
            dto.aupLocalY = SanitizeFinite(dto.aupLocalY, 0f);
            dto.aupLocalZ = SanitizeFinite(dto.aupLocalZ, 0f);
            SanitizeRotation(ref dto);
            return dto;
        }

        internal static bool PersistenceEquals(in ModuleGraphNodeDTO left, in ModuleGraphNodeDTO right)
        {
            return left.prefabId == right.prefabId &&
                   left.moduleHashId == right.moduleHashId &&
                   left.aupGridX == right.aupGridX &&
                   left.aupGridY == right.aupGridY &&
                   left.aupGridZ == right.aupGridZ &&
                   left.aupLocalX == right.aupLocalX &&
                   left.aupLocalY == right.aupLocalY &&
                   left.aupLocalZ == right.aupLocalZ &&
                   left.rotX == right.rotX &&
                   left.rotY == right.rotY &&
                   left.rotZ == right.rotZ &&
                   left.rotW == right.rotW;
        }

        private static void SanitizeRotation(ref ModuleGraphNodeDTO dto)
        {
            if (!IsFinite(dto.rotX) || !IsFinite(dto.rotY) || !IsFinite(dto.rotZ) || !IsFinite(dto.rotW))
            {
                dto.rotX = 0f;
                dto.rotY = 0f;
                dto.rotZ = 0f;
                dto.rotW = 1f;
                return;
            }

            float lengthSq = dto.rotX * dto.rotX + dto.rotY * dto.rotY + dto.rotZ * dto.rotZ + dto.rotW * dto.rotW;
            if (!IsFinite(lengthSq) || lengthSq <= 0.000001f)
            {
                dto.rotX = 0f;
                dto.rotY = 0f;
                dto.rotZ = 0f;
                dto.rotW = 1f;
                return;
            }

            float invLength = 1f / Mathf.Sqrt(lengthSq);
            dto.rotX *= invLength;
            dto.rotY *= invLength;
            dto.rotZ *= invLength;
            dto.rotW *= invLength;
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    [Serializable]
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct ModuleGraphEdgeDTO
    {
        [FieldOffset(0)] public int sourceNodeIndex;
        [FieldOffset(4)] public int destinationNodeIndex;
        [FieldOffset(8)] private long _pad0;

        internal static bool TrySanitizeForPersistence(
            in ModuleGraphEdgeDTO value,
            int graphNodeCount,
            out ModuleGraphEdgeDTO sanitized)
        {
            sanitized = default;
            int safeNodeCount = Math.Clamp(graphNodeCount, 0, ConstructionDTO.MaxModules);
            if (value.sourceNodeIndex < 0 ||
                value.destinationNodeIndex < 0 ||
                value.sourceNodeIndex >= safeNodeCount ||
                value.destinationNodeIndex >= safeNodeCount ||
                value.sourceNodeIndex == value.destinationNodeIndex)
            {
                return false;
            }

            sanitized.sourceNodeIndex = Math.Min(value.sourceNodeIndex, value.destinationNodeIndex);
            sanitized.destinationNodeIndex = Math.Max(value.sourceNodeIndex, value.destinationNodeIndex);
            return true;
        }

        internal static bool PersistenceEquals(in ModuleGraphEdgeDTO left, in ModuleGraphEdgeDTO right)
        {
            return left.sourceNodeIndex == right.sourceNodeIndex &&
                   left.destinationNodeIndex == right.destinationNodeIndex;
        }

        internal static bool ContainsPersistenceEdge(ModuleGraphEdgeDTO[] values, int count, in ModuleGraphEdgeDTO edge)
        {
            if (values == null || count <= 0)
                return false;

            int safeCount = Math.Clamp(count, 0, Math.Min(values.Length, ConstructionDTO.MaxGraphEdges));
            for (int i = 0; i < safeCount; i++)
            {
                if (PersistenceEquals(in values[i], in edge))
                    return true;
            }

            return false;
        }
    }
}
