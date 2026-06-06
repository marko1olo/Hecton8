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
        public const float HazardZoneMaxPersistedToxicityDose = 64f;
        public const float HazardZoneToxicityDamageDoseThreshold = 1f;
        public const float HazardZoneMaxPersistedToxicityPulseSeconds = 0.5f;
        public const int RadiationGridPersistenceVersion = 68;
        public const int FirstHourDtoLockPersistenceVersion = 72;
        public const float PlayerKinematicVelocityHardCapMetersPerSecond = 80f;
        public const float PlayerKinematicVelocityHardCapSq =
            PlayerKinematicVelocityHardCapMetersPerSecond * PlayerKinematicVelocityHardCapMetersPerSecond;
        public const float PlayerStatsNitrogenBuildUpHardCap = 160f;

        /// <summary>Tekuschaya versiya formata. Ispolzuetsya dlya migratsii.</summary>
        public const int CurrentVersion = HazardZoneRuntimePersistenceVersion; // v74: delayed hazard zone toxicity state.

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
            return new SaveData
            {
                version       = CurrentVersion,
                contractVersionHashLo = HectonContractVersion.HashLo,
                contractVersionHashHi = HectonContractVersion.HashHi,
                timestamp     = DateTime.Now.ToString("O"),
                totalPlayTime = playTime,
                playerStats   = new PlayerStatsDTO(),
                playerKinematicState = new PlayerKinematicStateDTO(),
                inventory     = new InventoryDTO(),
                inventoryShadow = new InventoryShadowDTO(),
                worldState    = new WorldStateDTO(),
                proceduralWorldState = new ProceduralWorldStateDTO(),
                construction  = ConstructionDTO.CreatePreallocated(),
                scanLog       = new ScanLogDTO(),
                barter        = new BarterDTO(),
                fieldOperations = new FieldOperationLogDTO(),
                beaconNetwork = new BeaconNetworkDTO(),
                explorationMap = new ExplorationMapDTO(),
                pdaLogbook = new PDALogbookDTO(),
                pdaMarkers = new PDAMarkerRegistryDTO(),
                pdaAdvisories = new PDAContextualAdvisoryDTO(),
                proceduralLore = new ProceduralLoreStateDTO(),
                achievements = new AchievementRegistryDTO(),
                runModifiers = new RunModifiersDTO
                {
                    dailySeedId = string.Empty
                },
                metaCampaign = MetaCampaignDTO.CreateDefault(),
                resourceScarcity = new ResourceScarcityDTO(),
                environmentalStrain = new EnvironmentalStrainDTO(),
                ecosystemState = new EcosystemStateDTO(),
                voxelDeltaPersistence = VoxelDeltaPersistenceDTO.CreateDefault(),
                hazardZones = new HazardZoneRuntimeDTO(),
                discoveredBiomeIds = null,
                // COLD ALLOC: long[BiomeDiscoveryBitMask.WordCount] — packed discovered biome persistence — owner: SaveData
                discoveredBiomeBitWords = new long[BiomeDiscoveryBitMask.WordCount],
                lastDiscoveredBiomeId = -1,
                narrativeDiscoveryCount = 0,
                narrativeDiscoveryIds = new string[MaxNarrativeDiscoveries],
                narrativeDepthTier = 0,
                audioLogDiscoveredIds = new List<string>(),
                // COLD ALLOC: long[AudioLogDiscoveryBitMask.WordCount] — packed audio-log discovery persistence — owner: SaveData
                audioLogDiscoveryBitWords = new long[AudioLogDiscoveryBitMask.WordCount],
                audioLogEncryptedFragmentCount = 0,
                audioLogEncryptedFragmentHashes = new uint[MaxEncryptedAudioLogFragments],
                audioLogEncryptedFragmentBits = new uint[MaxEncryptedAudioLogFragments],
                // COLD ALLOC: long[IndustrialLoreBitMask.WordCount] — packed industrial lore discovery persistence — owner: SaveData
                industrialLoreUnlockWords = new long[IndustrialLoreBitMask.WordCount],
                // COLD ALLOC: long[MaxDataArchaeologyDiscoveryWords] - packed archaeology discovery persistence - owner: SaveData
                dataArchaeologyDiscoveryBitWords = new long[MaxDataArchaeologyDiscoveryWords],
                dataArchaeologyPartialScanCount = 0,
                // COLD ALLOC: uint[MaxDataArchaeologyPartialScans] - partial archaeology hashes - owner: SaveData
                dataArchaeologyPartialScanHashes = new uint[MaxDataArchaeologyPartialScans],
                // COLD ALLOC: ushort[MaxDataArchaeologyPartialScans] - partial archaeology progress - owner: SaveData
                dataArchaeologyPartialScanProgressPermille = new ushort[MaxDataArchaeologyPartialScans],
                dataArchaeologyScanStateCount = 0,
                // COLD ALLOC: int[MaxDataArchaeologyScanStates] - data archaeology scan state keys - owner: SaveData
                dataArchaeologyScanStateKeys = new int[MaxDataArchaeologyScanStates],
                // COLD ALLOC: byte[MaxDataArchaeologyScanStates] - data archaeology scan state values - owner: SaveData
                dataArchaeologyScanStateValues = new byte[MaxDataArchaeologyScanStates],
                questActiveIds = new List<string>(),
                questCompletedIds = new List<string>(),
                atlasSignalDetected = false,
                atlasSignalPulseTimer = 0f,
                atlasSignalRevealStage = 0,
                narrativeAupTriggeredMask = 0UL,
                suitInstalledUpgradeIds = new List<string>(),
                suitUnlockedBlueprintIds = new List<string>(),
                suitBrokenUpgradeIds = new List<string>(),
                suitUpgradeMask = 0UL,
                playerExpressionProfileId = string.Empty,
                atlas6PlayerStatus = 0,
                atlas6BarterCount = 0,
                atlas6DirectiveConflictTriggered = false,
                corporateReceivedOrderIds = new List<string>(),
                corporatePendingOrderIds = new List<string>(),
                corporatePendingOrderTimers = new List<float>(),
                firstHourSessionTime = 0f,
                firstHourMilestones = 0,
                firstHourGuidanceFlags = 0,
                endingChoice = 0,
                endingComplete = false,
                endingConditionMet = false,
                missionActiveIds = new List<string>(),
                missionCompletedIds = new List<string>(),
                LODQualityPreset = 1, // Default: Medium
                DynamicResolutionEnabled = true, // Default: Enabled
                radiationDose = 0f,
                radiationGridOriginX = 0d,
                radiationGridOriginY = 0d,
                radiationGridOriginZ = 0d,
                radiationGridCellSizeMeters = RadiationGridDefaultCellSizeMeters,
                radiationGridRleLength = 0,
                radiationGridRle = new byte[RadiationGridRleMaxBytes],
                rtgDecayCount = 0,
                rtgDecaySourceIds = new int[MaxRtgDecayRecords],
                rtgStartTimesSeconds = new double[MaxRtgDecayRecords],
                rtgDecayFlags = new byte[MaxRtgDecayRecords],
                CustomModData = new Dictionary<string, string>()
            };
        }

        public void RefreshFirstHourDtoMirrors()
        {
            playerKinematicState = PlayerKinematicStateDTO.FromPlayerStats(in playerStats);
            inventoryShadow = InventoryShadowDTO.FromInventory(
                in inventory,
                inventoryShadowPayloadLength,
                inventoryShadowPayloadHash,
                hasInventoryShadowPayload);
            construction.RefreshHabitatFloodStateMirrors();
        }

        public const int MaxNarrativeDiscoveries = 128;

        /// <summary>Maximum persisted partial encrypted audio-log recovery records. v61 LORE.</summary>
        public const int MaxEncryptedAudioLogFragments = 32;

        /// <summary>Maximum persisted partial archaeology scan records. v64 DISCOVERY.</summary>
        public const int MaxDataArchaeologyPartialScans = 256;

        /// <summary>Maximum explicit scanner state records. v66 DISCOVERY.</summary>
        public const int MaxDataArchaeologyScanStates = 1024;

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
                int moduleHashId = 0;
                if (moduleBlitRecords != null && i < moduleBlitRecords.Length)
                    moduleHashId = moduleBlitRecords[i].moduleHashId;

                habitatFloodStates[i] = HabitatFloodStateDTO.FromModule(in modules[i], moduleHashId);
            }
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
            dto.integrity = module.integrity;
            dto.repairIntegrityCap = module.repairIntegrityCap;
            dto.airReserveNormalized = module.airReserveNormalized;
            dto.co2Normalized = module.co2Normalized;
            dto.floodedReefFloodSeconds = module.floodedReefFloodSeconds;
            dto.flags = (byte)((module.isFlooded ? FlagFlooded : 0) |
                               (module.interiorReefInfestationActive ? FlagInfested : 0));
            dto.failureMode = module.failureMode;
            dto.health = module.health;
            return dto;
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
    }

    [Serializable]
    public struct ScanEntryDTO
    {
        public string id;
        public string title;
        public string category;
        public string summary;
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
    }

    [Serializable]
    public struct BarterTransactionDTO
    {
        public string offerId;
        public string offerName;
        public string channelName;
        public string costSummary;
        public string rewardSummary;
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

        public void SetPosition(Vector3 pos)
        {
            posX = pos.x; posY = pos.y; posZ = pos.z;
        }

        public void SetRotation(Quaternion rot)
        {
            rotX = rot.x; rotY = rot.y; rotZ = rot.z; rotW = rot.w;
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
    public struct ModuleDTO
    {
        public const int MaxSorterBufferedSlots = 8;
        public const int MaxCultivationSlots = 4;

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
        public ulong[] cultivationGeneticsMasks;
        public float[] cultivationGrowth01;
        public float[] cultivationQuality01;

        public Vector3 GetPosition() => new Vector3(posX, posY, posZ);
        public Quaternion GetRotation() => new Quaternion(rotX, rotY, rotZ, rotW);

        public void EnsureNestedArrayCapacity()
        {
            SaveData.EnsureExactArrayCapacity(ref sorterBufferedItemIds, MaxSorterBufferedSlots);
            SaveData.EnsureExactArrayCapacity(ref sorterBufferedQuantities, MaxSorterBufferedSlots);
            SaveData.EnsureExactArrayCapacity(ref cultivationSeedItemIds, MaxCultivationSlots);
            SaveData.EnsureExactArrayCapacity(ref cultivationGeneticsMasks, MaxCultivationSlots);
            SaveData.EnsureExactArrayCapacity(ref cultivationGrowth01, MaxCultivationSlots);
            SaveData.EnsureExactArrayCapacity(ref cultivationQuality01, MaxCultivationSlots);
        }

        public bool HasNestedArrayCapacity()
        {
            return HasSorterSaveCapacity() && HasCultivationSaveCapacity();
        }

        public bool HasSorterSaveCapacity()
        {
            return sorterBufferedItemIds != null &&
                   sorterBufferedItemIds.Length >= MaxSorterBufferedSlots &&
                   sorterBufferedQuantities != null &&
                   sorterBufferedQuantities.Length >= MaxSorterBufferedSlots;
        }

        public bool HasCultivationSaveCapacity()
        {
            return cultivationSeedItemIds != null &&
                   cultivationSeedItemIds.Length >= MaxCultivationSlots &&
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
            string[] seedItemIds = cultivationSeedItemIds;
            ulong[] geneticsMasks = cultivationGeneticsMasks;
            float[] growthValues = cultivationGrowth01;
            float[] qualityValues = cultivationQuality01;

            this = default;

            sorterBufferedItemIds = sorterItemIds;
            sorterBufferedQuantities = sorterQuantities;
            cultivationSeedItemIds = seedItemIds;
            cultivationGeneticsMasks = geneticsMasks;
            cultivationGrowth01 = growthValues;
            cultivationQuality01 = qualityValues;

            ClearArray(sorterBufferedItemIds);
            ClearArray(sorterBufferedQuantities);
            ClearArray(cultivationSeedItemIds);
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
    }

    [Serializable]
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct ModuleGraphEdgeDTO
    {
        [FieldOffset(0)] public int sourceNodeIndex;
        [FieldOffset(4)] public int destinationNodeIndex;
        [FieldOffset(8)] private long _pad0;
    }
}
