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
using Hecton8.Gameplay;
using Hecton8.Narrative;
using Hecton8.World;
using Unity.Collections;
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

        /// <summary>Vremennaya metka sohraneniya (ISO 8601).</summary>
        public string timestamp;

        /// <summary>Obschee vremya igry v sekundah.</summary>
        public double totalPlayTime;

        /// <summary>Tekuschaya versiya formata. Ispolzuetsya dlya migratsii.</summary>
        public const int CurrentVersion = 72; // v72: first-hour DTO ABI lock.

        public static void EnsureExactArrayCapacity<T>(ref T[] values, int capacity)
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
        [NonSerialized] internal NativeArray<byte> inventoryShadowPayload;
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
        public float radiationGridCellSizeMeters = 4f;

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
                timestamp     = DateTime.Now.ToString("O"),
                totalPlayTime = playTime,
                playerStats   = new PlayerStatsDTO(),
                playerKinematicState = new PlayerKinematicStateDTO(),
                inventory     = new InventoryDTO(),
                inventoryShadow = new InventoryShadowDTO(),
                worldState    = new WorldStateDTO(),
                proceduralWorldState = new ProceduralWorldStateDTO(),
                construction  = new ConstructionDTO(),
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
                // COLD ALLOC: long[DataArchaeologyDiscoveryBitMask.WordCount] - packed archaeology discovery persistence - owner: SaveData
                dataArchaeologyDiscoveryBitWords = new long[DataArchaeologyDiscoveryBitMask.WordCount],
                dataArchaeologyPartialScanCount = 0,
                // COLD ALLOC: uint[DataArchaeologyRuntime.MaxPartialScanCount] - partial archaeology hashes - owner: SaveData
                dataArchaeologyPartialScanHashes = new uint[DataArchaeologyRuntime.MaxPartialScanCount],
                // COLD ALLOC: ushort[DataArchaeologyRuntime.MaxPartialScanCount] - partial archaeology progress - owner: SaveData
                dataArchaeologyPartialScanProgressPermille = new ushort[DataArchaeologyRuntime.MaxPartialScanCount],
                dataArchaeologyScanStateCount = 0,
                // COLD ALLOC: int[DataArchaeologyRuntime.MaxDiscoveryCount] - data archaeology scan state keys - owner: SaveData
                dataArchaeologyScanStateKeys = new int[DataArchaeologyRuntime.MaxDiscoveryCount],
                // COLD ALLOC: byte[DataArchaeologyRuntime.MaxDiscoveryCount] - data archaeology scan state values - owner: SaveData
                dataArchaeologyScanStateValues = new byte[DataArchaeologyRuntime.MaxDiscoveryCount],
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
                radiationGridCellSizeMeters = 4f,
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
        public const int MaxDataArchaeologyPartialScans = DataArchaeologyRuntime.MaxPartialScanCount;

        /// <summary>Maximum explicit scanner state records. v66 DISCOVERY.</summary>
        public const int MaxDataArchaeologyScanStates = DataArchaeologyRuntime.MaxDiscoveryCount;

        /// <summary>Maximum sparse RLE radiation payload. v68 RADIATION.</summary>
        public const int RadiationGridRleMaxBytes = 81920;

        /// <summary>Maximum persisted RTG decay records. v70 RTG.</summary>
        public const int MaxRtgDecayRecords = 128;

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
    [StructLayout(LayoutKind.Sequential)]
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
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]
    public struct PlayerKinematicStateDTO
    {
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
        public int flags;
        private int _pad0;

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
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
    public struct ExternalScavengerSiteDTO
    {
        public int chunkX;
        public int chunkY;
        public int chunkZ;
        public sbyte offsetX;
        public sbyte offsetY;
        public sbyte offsetZ;
        public byte quantizedRadius;
        public float remainingTime;
        public uint seed;
        private long _pad0;

        public bool IsValid => remainingTime > 0f;
    }

    // ══════════════════════════════════════════════════════════════════
    //  InventoryDTO
    // ══════════════════════════════════════════════════════════════════

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
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
    [StructLayout(LayoutKind.Sequential)]
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
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
    public struct InventoryShadowDTO
    {
        public const byte FlagHasPayload = 1 << 0;
        public const byte SchemaVersion = 1;

        public int cellCount;
        public int payloadLength;
        public uint payloadHash;
        public int gridColumns;
        public int gridRows;
        public float totalWeight;
        public byte flags;
        public byte schemaVersion;
        public ushort reserved0;
        private int _pad0;

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
    [StructLayout(LayoutKind.Sequential)]
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
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
    public struct ProceduralFaunaStateDTO
    {
        public const byte FlagLargeThreatZone = 1 << 0;
        public const byte FlagBlocked = 1 << 1;

        public long runtimeKey;
        public float cooldownUntilPlayTime;
        public byte flags;
        private byte _pad0;
        private ushort _pad1;

        public bool isLargeThreatZone
        {
            get => (flags & FlagLargeThreatZone) != 0;
            set => flags = value ? (byte)(flags | FlagLargeThreatZone) : (byte)(flags & ~FlagLargeThreatZone);
        }

        public bool blocked
        {
            get => (flags & FlagBlocked) != 0;
            set => flags = value ? (byte)(flags | FlagBlocked) : (byte)(flags & ~FlagBlocked);
        }
    }

    [Serializable]
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 112)]
    public struct HibernatedFaunaStateDTO
    {
        public const byte FlagLargeThreat = 1 << 0;

        public int speciesId;
        public int biomeIndex;
        public int creatureTypeIndex;
        public float health;
        public AbsoluteUniversePositionBlit128 position;
        public float rotationX;
        public float rotationY;
        public float rotationZ;
        public float rotationW;
        public float linearVelocityX;
        public float linearVelocityY;
        public float linearVelocityZ;
        public float angularVelocityX;
        public float angularVelocityY;
        public float angularVelocityZ;
        public uint uniqueInstanceUid;
        public byte flags;
        private byte _pad0;
        private ushort _pad1;

        public bool isLargeThreat
        {
            get => (flags & FlagLargeThreat) != 0;
            set => flags = value ? (byte)(flags | FlagLargeThreat) : (byte)(flags & ~FlagLargeThreat);
        }
    }

    [Serializable]
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
    public struct ProceduralGeologySeamStateDTO
    {
        public long runtimeKey;
        public int chunkX;
        public int chunkZ;
        public float absoluteTerrainHeight;
        public float absoluteSeamHeight;
        public float seamBlendRadius;
        public float terrainBlendWeight;
        public float caveBlendWeight;
        public float absolutePositionX;
        public float absolutePositionY;
        public float absolutePositionZ;
        public float absoluteVoxelCenterX;
        public float absoluteVoxelCenterY;
        public float absoluteVoxelCenterZ;
        private int _pad0;
    }

    [Serializable]
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]
    public struct ProceduralGeologyCaveEntranceDTO
    {
        public long runtimeKey;
        public float surfacePositionX;
        public float surfacePositionY;
        public float surfacePositionZ;
        public float inwardDirectionX;
        public float inwardDirectionY;
        public float inwardDirectionZ;
        public float radius;
        public float funnelLength;
        public float innerRadius;
        private int _pad0;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
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
    [StructLayout(LayoutKind.Sequential)]
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

        public void EnsureCapacity()
        {
            SaveData.EnsureExactArrayCapacity(ref modules, MaxModules);
            SaveData.EnsureExactArrayCapacity(ref graphNodes, MaxModules);
            SaveData.EnsureExactArrayCapacity(ref graphEdges, MaxGraphEdges);
            SaveData.EnsureExactArrayCapacity(ref moduleBlitRecords, MaxModules);
            SaveData.EnsureExactArrayCapacity(ref habitatFloodStates, MaxModules);
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
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
    public struct HabitatFloodStateDTO
    {
        public const byte FlagFlooded = 1 << 0;
        public const byte FlagInfested = 1 << 1;

        public int moduleHashId;
        public float integrity;
        public float repairIntegrityCap;
        public float airReserveNormalized;
        public float co2Normalized;
        public float floodedReefFloodSeconds;
        public byte flags;
        public byte failureMode;
        public byte health;
        public byte reserved0;
        private int _pad0;

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
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
    public struct ModuleBlitDTO
    {
        public int prefabHashId;
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
        public byte health;
        public byte flags;
        public byte failureMode;
        public byte reserved;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct ScanEntryDTO
    {
        public string id;
        public string title;
        public string category;
        public string summary;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
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
    [StructLayout(LayoutKind.Sequential)]
    public struct BarterOfferStateDTO
    {
        public string offerId;
        public int executionCount;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct BarterTransactionDTO
    {
        public string offerId;
        public string offerName;
        public string channelName;
        public string costSummary;
        public string rewardSummary;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
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
    [StructLayout(LayoutKind.Sequential)]
    public struct FieldOperationEntryDTO
    {
        public string source;
        public string title;
        public string summary;
        public string severity;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
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
    [StructLayout(LayoutKind.Sequential)]
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
    [StructLayout(LayoutKind.Sequential)]
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
    [StructLayout(LayoutKind.Sequential)]
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
        public const int CartographyCellSizeMeters = 50;
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
    [StructLayout(LayoutKind.Sequential)]
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
    [StructLayout(LayoutKind.Sequential)]
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
    [StructLayout(LayoutKind.Sequential)]
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

        internal bool HasAupPosition => positionEncodingVersion == AupPositionEncodingVersion;

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
    [StructLayout(LayoutKind.Sequential)]
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
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]
    public struct PDAContextualAdvisoryDTO
    {
        public int issuedFlags;
        public int oxygenDeathCount;
        public int inventoryFullAttemptCount;
        public int pressureDeathCount;
        public int baseEmergencyCount;
        public int staleAirIncidentCount;
        public int coldStressIncidentCount;
        public int heatStressIncidentCount;
        public float deepExposureSeconds;
        public float coldStressExposureSeconds;
        public float heatStressExposureSeconds;
        private int _pad0;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
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
    [StructLayout(LayoutKind.Sequential)]
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
    [StructLayout(LayoutKind.Sequential)]
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
    [StructLayout(LayoutKind.Sequential)]
    public struct RunModifiersDTO
    {
        public bool isPermadeath;
        public bool isNightmareMode;
        public bool isDailySeed;
        public bool runMarkedDead;
        public string dailySeedId;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
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
    [StructLayout(LayoutKind.Sequential)]
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
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
    public struct EnvironmentalStrainDTO
    {
        public float microplasticStrain;
        public float generalPollution;
        public int recycledPlasticItemCount;
        public int discardedItemCount;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
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
    [StructLayout(LayoutKind.Sequential)]
    public struct ModuleDTO
    {
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
    [StructLayout(LayoutKind.Sequential)]
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
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
    public struct ModuleGraphEdgeDTO
    {
        public int sourceNodeIndex;
        public int destinationNodeIndex;
        private long _pad0;
    }
}
