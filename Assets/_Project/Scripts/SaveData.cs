// ============================================================================
// HECTON-8 — SaveData.cs
// Главный контейнер сохранения. Паттерн DTO (Data Transfer Object).
//
// ВСЕ данные сохранения — здесь. Один объект → одна сериализация.
// Native binary save codecs serialize this class field-by-field.
//
// ДИЗАЙН-РЕШЕНИЯ:
//   • [Serializable] struct для вложенных DTO — минимум heap-аллокаций.
//   • Примитивные типы вместо Vector3/Quaternion — binary compatibility
//     и портируемость (JSON, binary, XML).
//   • string ID вместо int InstanceID — стабильность между сессиями.
//   • Версионирование: поле version для миграции данных.
//   • Pre-allocated массивы вместо List — контроль размера.
//
// РАСШИРЕНИЕ:
//   Добавляй новые DTO как поля SaveData. Старые сейвы получат
//   дефолтные значения для новых полей обрабатываются миграцией и дефолтными инициализаторами.
// ============================================================================

using System;
using System.Collections.Generic;
using Hecton8.Narrative;
using Hecton8.World;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.SaveSystem
{
    /// <summary>
    /// Корневой контейнер всех данных сохранения.
    /// Один экземпляр = одна полная копия игрового состояния.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        // ─────────────────────── Metadata ────────────────────────

        /// <summary>Версия формата. Инкрементируется при изменении структуры DTO.</summary>
        public int version = CurrentVersion;

        /// <summary>Временная метка сохранения (ISO 8601).</summary>
        public string timestamp;

        /// <summary>Общее время игры в секундах.</summary>
        public double totalPlayTime;

        /// <summary>Текущая версия формата. Используется для миграции.</summary>
        public const int CurrentVersion = 60; // v60: Resource scarcity persists item hashes without runtime ID string dependency.

        // ─────────────────────── DTO Sections ────────────────────

        public PlayerStatsDTO playerStats;
        public InventoryDTO inventory;
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
        public ResourceScarcityDTO resourceScarcity;
        public EnvironmentalStrainDTO environmentalStrain;
        public EcosystemStateDTO ecosystemState;
        public VoxelDeltaPersistenceDTO voxelDeltaPersistence;
        public ExternalScavengerSiteDTO[] externalScavengerSites;

        /// <summary>Прочность инструментов (toolID → durability). v2.0 ENTERPRISE</summary>
        public Dictionary<string, float> toolDurabilityMap = new Dictionary<string, float>();

        /// <summary>Сломанные инструменты (toolID → broken). v2.0 ENTERPRISE</summary>
        public Dictionary<string, bool> toolBrokenMap = new Dictionary<string, bool>();

        /// <summary>Legacy set of discovered biome IDs kept only for backward-compatible migration reads.</summary>
        public HashSet<int> discoveredBiomeIds;

        /// <summary>Packed discovery words for all 108 biomes. Two 64-bit words cover the current matrix.</summary>
        public long[] discoveredBiomeBitWords;

        /// <summary>Последний подтвержденный открытый биом для PDA и HUD.</summary>
        public int lastDiscoveredBiomeId = -1;

        /// <summary>Количество narrative-discovery записей, сохраненных в narrativeDiscoveryIds.</summary>
        public int narrativeDiscoveryCount;

        /// <summary>Стабильные narrative-discovery ID для поздних триггеров и повторного входа в сцену.</summary>
        public string[] narrativeDiscoveryIds;

        /// <summary>Максимальный достигнутый narrative depth-tier.</summary>
        public int narrativeDepthTier;

        /// <summary>Список ID обнаруженных аудиодневников. v4.0 LORE</summary>
        public List<string> audioLogDiscoveredIds = new List<string>();

        /// <summary>Packed industrial-lore discovery words for the fixed 50-record archive bank.</summary>
        public long[] industrialLoreUnlockWords;

        /// <summary>Активные квесты. v4.0 QUEST</summary>
        public List<string> questActiveIds = new List<string>();

        /// <summary>Завершённые квесты. v4.0 QUEST</summary>
        public List<string> questCompletedIds = new List<string>();

        /// <summary>Сигнал Атлас-6 когда-либо обнаружен. v4.0 ATLAS</summary>
        public bool atlasSignalDetected;

        /// <summary>Таймер пульса сигнала (для сохранения ритма). v4.0 ATLAS</summary>
        public float atlasSignalPulseTimer;

        /// <summary>Максимальная раскрытая стадия позднего Atlas-manifestation. v4.10 ATLAS</summary>
        public int atlasSignalRevealStage;

        /// <summary>Установленные апгрейды скафандра. v4.1 UPGRADES</summary>
        public List<string> suitInstalledUpgradeIds = new List<string>();

        /// <summary>Разблокированные чертежи апгрейдов. v4.1 UPGRADES</summary>
        public List<string> suitUnlockedBlueprintIds = new List<string>();

        /// <summary>Сломанные, но установленные апгрейды скафандра. v33 WIPEOUT</summary>
        public List<string> suitBrokenUpgradeIds = new List<string>();

        /// <summary>ÐÐºÑ‚Ð¸Ð²Ð½Ñ‹Ð¹ Ð¿Ñ€Ð¾Ñ„Ð¸Ð»ÑŒ ÑÐ°Ð¼Ð¾Ð²Ñ‹Ñ€Ð°Ð¶ÐµÐ½Ð¸Ñ Ð¸Ð³Ñ€Ð¾ÐºÐ°. v4.9 EXPRESSION</summary>
        public string playerExpressionProfileId = string.Empty;

        /// <summary>Статус игрока с точки зрения Атлас-6. v4.2 ATLAS6</summary>
        public int atlas6PlayerStatus;

        /// <summary>Количество бартер-транзакций с Атлас-6. v4.2 ATLAS6</summary>
        public int atlas6BarterCount;

        /// <summary>Конфликт директив был активирован. v4.2 ATLAS6</summary>
        public bool atlas6DirectiveConflictTriggered;

        /// <summary>Полученные корпоративные приказы. v4.3 CORP</summary>
        public List<string> corporateReceivedOrderIds = new List<string>();

        /// <summary>Ожидающие приказы (ID). v4.3 CORP</summary>
        public List<string> corporatePendingOrderIds = new List<string>();

        /// <summary>Таймеры ожидающих приказов (секунды). v4.3 CORP</summary>
        public List<float> corporatePendingOrderTimers = new List<float>();

        /// <summary>Время сессии первого часа (секунды). v4.4 FIRSTHOUR</summary>
        public float firstHourSessionTime;

        /// <summary>Битовая маска выполненных milestone первого часа. v4.4 FIRSTHOUR</summary>
        public int firstHourMilestones;

        /// <summary>Битовая маска уже выданных first-hour guidance/reminder states. v4.11 FIRSTHOUR</summary>
        public int firstHourGuidanceFlags;

        /// <summary>Выбранная концовка. v4.5 ENDING</summary>
        public int endingChoice;

        /// <summary>Концовка завершена. v4.5 ENDING</summary>
        public bool endingComplete;

        /// <summary>Условие концовки выполнено (игрок у ядра). v4.5 ENDING</summary>
        public bool endingConditionMet;

        /// <summary>Активные миссии (MissionManager). v4.6 MISSIONS</summary>
        public List<string> missionActiveIds = new List<string>();

        /// <summary>Завершённые миссии (MissionManager). v4.6 MISSIONS</summary>
        public List<string> missionCompletedIds = new List<string>();

        /// <summary>LOD quality preset (0=Low, 1=Medium, 2=High). v4.7 LOD</summary>
        public int LODQualityPreset = 1; // Default: Medium

        /// <summary>Dynamic resolution scaling enabled. v4.8 LOD</summary>
        public bool DynamicResolutionEnabled = true; // Default: Enabled

        /// <summary>Custom mod payload map persisted inside the official save file. v24 MODDING</summary>
        public Dictionary<string, string> CustomModData = new Dictionary<string, string>();

        // ═════════════════════════════════════════════════════════
        //  Factory — создание нового SaveData с метаданными
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
                inventory     = new InventoryDTO(),
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
                // COLD ALLOC: long[IndustrialLoreBitMask.WordCount] — packed industrial lore discovery persistence — owner: SaveData
                industrialLoreUnlockWords = new long[IndustrialLoreBitMask.WordCount],
                questActiveIds = new List<string>(),
                questCompletedIds = new List<string>(),
                atlasSignalDetected = false,
                atlasSignalPulseTimer = 0f,
                atlasSignalRevealStage = 0,
                suitInstalledUpgradeIds = new List<string>(),
                suitUnlockedBlueprintIds = new List<string>(),
                suitBrokenUpgradeIds = new List<string>(),
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
                CustomModData = new Dictionary<string, string>()
            };
        }

        public const int MaxNarrativeDiscoveries = 128;
    }

    // ══════════════════════════════════════════════════════════════════
    //  PlayerStatsDTO — состояние скафандра и позиция игрока
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

        public bool IsValid => remainingTime > 0f;
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
        public float totalWeight;
        public int gridColumns;
        public int gridRows;

        public const int MaxCells = 128;

        public void EnsureCapacity()
        {
            if (itemHashIds == null || itemHashIds.Length < MaxCells)
                itemHashIds = new int[MaxCells];

            if (packedCellCoordinates == null || packedCellCoordinates.Length < MaxCells)
                packedCellCoordinates = new uint[MaxCells];

            if (stackCounts == null || stackCounts.Length < MaxCells)
                stackCounts = new ushort[MaxCells];

            if (itemStateFlags == null || itemStateFlags.Length < MaxCells)
                itemStateFlags = new ushort[MaxCells];

            if (itemGeneticsWords == null || itemGeneticsWords.Length < MaxCells)
                itemGeneticsWords = new byte[MaxCells];

            if (qualityMilli == null || qualityMilli.Length < MaxCells)
                qualityMilli = new ushort[MaxCells];

            if (lastUpdateUnixSeconds == null || lastUpdateUnixSeconds.Length < MaxCells)
                lastUpdateUnixSeconds = new uint[MaxCells];
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
            if (depletedNodeIds == null || depletedNodeIds.Length < MaxNodes)
                depletedNodeIds = new string[MaxNodes];

            if (depletedPickupChunkKeys == null || depletedPickupChunkKeys.Length < MaxPickupChunks)
                depletedPickupChunkKeys = new long[MaxPickupChunks];

            if (depletedPickupChunkWordStarts == null || depletedPickupChunkWordStarts.Length < MaxPickupChunks)
                depletedPickupChunkWordStarts = new int[MaxPickupChunks];

            if (depletedPickupChunkWordCounts == null || depletedPickupChunkWordCounts.Length < MaxPickupChunks)
                depletedPickupChunkWordCounts = new int[MaxPickupChunks];

            if (depletedPickupWords == null || depletedPickupWords.Length < MaxPickupWords)
                depletedPickupWords = new long[MaxPickupWords];
        }
    }

    [Serializable]
    public struct ProceduralFaunaStateDTO
    {
        public long runtimeKey;
        public float cooldownUntilPlayTime;
        public bool isLargeThreatZone;
        public bool blocked;
    }

    [Serializable]
    public struct HibernatedFaunaStateDTO
    {
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
        public bool isLargeThreat;
    }

    [Serializable]
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
    }

    [Serializable]
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
            if (suppressedPlacementKeys == null || suppressedPlacementKeys.Length < MaxSuppressedPlacements)
                suppressedPlacementKeys = new long[MaxSuppressedPlacements];

            if (faunaStates == null || faunaStates.Length < MaxFaunaStates)
                faunaStates = new ProceduralFaunaStateDTO[MaxFaunaStates];

            if (hibernatedFaunaStates == null || hibernatedFaunaStates.Length < MaxHibernatedFaunaStates)
                hibernatedFaunaStates = new HibernatedFaunaStateDTO[MaxHibernatedFaunaStates];

            if (geologySeamStates == null || geologySeamStates.Length < MaxGeologySeamStates)
                geologySeamStates = new ProceduralGeologySeamStateDTO[MaxGeologySeamStates];

            if (geologyCaveEntrances == null || geologyCaveEntrances.Length < MaxGeologyCaveEntrances)
                geologyCaveEntrances = new ProceduralGeologyCaveEntranceDTO[MaxGeologyCaveEntrances];
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
        public const int MaxModules = 256;
        public const int MaxGraphEdges = MaxModules * 6;

        public void EnsureCapacity()
        {
            if (modules == null || modules.Length < MaxModules)
                modules = new ModuleDTO[MaxModules];

            if (graphNodes == null || graphNodes.Length < MaxModules)
                graphNodes = new ModuleGraphNodeDTO[MaxModules];

            if (graphEdges == null || graphEdges.Length < MaxGraphEdges)
                graphEdges = new ModuleGraphEdgeDTO[MaxGraphEdges];
        }
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
            if (entries == null || entries.Length < MaxEntries)
                entries = new ScanEntryDTO[MaxEntries];

            if (recentEntryIds == null || recentEntryIds.Length < MaxRecentEntries)
                recentEntryIds = new string[MaxRecentEntries];
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
            if (offerStates == null || offerStates.Length < MaxOffers)
                offerStates = new BarterOfferStateDTO[MaxOffers];
            if (recentTransactions == null || recentTransactions.Length < MaxRecentTransactions)
                recentTransactions = new BarterTransactionDTO[MaxRecentTransactions];
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
            if (recentEntries == null || recentEntries.Length < MaxRecentEntries)
                recentEntries = new FieldOperationEntryDTO[MaxRecentEntries];
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
            if (entries == null || entries.Length < MaxEntries)
                entries = new BeaconEntryDTO[MaxEntries];
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

        public const int MaxExploredChunks = 16384;
        public const int DenseChunkSizeMeters = 16;
        public const int MortonMaskAxisBits = 7;
        public const int MortonMaskAxisLength = 1 << MortonMaskAxisBits;
        public const int MortonMaskOriginOffset = MortonMaskAxisLength >> 1;
        public const int MortonMaskBitCount = MortonMaskAxisLength * MortonMaskAxisLength * MortonMaskAxisLength;
        public const int MortonMaskWordCount = MortonMaskBitCount >> 6;
        public const int MortonMaskByteCount = MortonMaskBitCount >> 3;

        public void EnsureCapacity()
        {
            if (exploredChunkKeys == null || exploredChunkKeys.Length < MaxExploredChunks)
                exploredChunkKeys = new long[MaxExploredChunks];

            if (exploredMortonMaskWords == null || exploredMortonMaskWords.Length < MortonMaskWordCount)
                exploredMortonMaskWords = new long[MortonMaskWordCount];

            if (exploredMortonMaskBytes == null)
            {
                exploredMortonMaskBytes = new byte[MortonMaskByteCount];
            }
            else if (exploredMortonMaskBytes.Length < MortonMaskByteCount)
            {
                byte[] expandedBytes = new byte[MortonMaskByteCount];
                Array.Copy(exploredMortonMaskBytes, expandedBytes, exploredMortonMaskBytes.Length);
                exploredMortonMaskBytes = expandedBytes;
            }

            chunkSizeMeters = DenseChunkSizeMeters;
            mortonMaskAxisBits = MortonMaskAxisBits;
            mortonMaskOriginOffset = MortonMaskOriginOffset;
            mortonBuildSalt = SaveBinaryStorage.ExplorationMortonBuildSalt32;
            if (exploredMortonWordCount < 0 || exploredMortonWordCount > MortonMaskWordCount)
                exploredMortonWordCount = 0;

            if (exploredMortonByteCount < 0 || exploredMortonByteCount > MortonMaskByteCount)
                exploredMortonByteCount = 0;
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
            if (entries == null || entries.Length < MaxEntries)
                entries = new PDALogbookEntryDTO[MaxEntries];

            if (seenOriginKeys == null || seenOriginKeys.Length < MaxSeenOrigins)
                seenOriginKeys = new string[MaxSeenOrigins];

            if (seenOriginHashes == null || seenOriginHashes.Length < MaxSeenOrigins)
                seenOriginHashes = new int[MaxSeenOrigins];
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
    public struct PDAMarkerRegistryDTO
    {
        public int markerCount;
        public int nextSequence;
        public PDAMarkerEntryDTO[] entries;

        public const int MaxEntries = 64;

        public void EnsureCapacity()
        {
            if (entries == null || entries.Length < MaxEntries)
                entries = new PDAMarkerEntryDTO[MaxEntries];
        }
    }

    [Serializable]
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
            if (activePlacements == null || activePlacements.Length < MaxActivePlacements)
                activePlacements = new ProceduralLorePlacementDTO[MaxActivePlacements];
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
            if (unlockedIds == null || unlockedIds.Length < MaxUnlockedAchievements)
                unlockedIds = new string[MaxUnlockedAchievements];
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
    public struct ResourceScarcityDTO
    {
        public const int MaxTrackedResources = 96;

        public int entryCount;
        public int[] itemHashIds;
        public string[] itemIds;
        public int[] collectedCounts;

        public void EnsureCapacity()
        {
            if (itemHashIds == null || itemHashIds.Length != MaxTrackedResources)
            {
                int[] replacement = new int[MaxTrackedResources];
                if (itemHashIds != null)
                {
                    int copyCount = itemHashIds.Length < replacement.Length ? itemHashIds.Length : replacement.Length;
                    Array.Copy(itemHashIds, replacement, copyCount);
                }

                itemHashIds = replacement;
            }

            if (itemIds == null || itemIds.Length != MaxTrackedResources)
            {
                string[] replacement = new string[MaxTrackedResources];
                if (itemIds != null)
                {
                    int copyCount = itemIds.Length < replacement.Length ? itemIds.Length : replacement.Length;
                    Array.Copy(itemIds, replacement, copyCount);
                }

                itemIds = replacement;
            }

            if (collectedCounts == null || collectedCounts.Length != MaxTrackedResources)
            {
                int[] replacement = new int[MaxTrackedResources];
                if (collectedCounts != null)
                {
                    int copyCount = collectedCounts.Length < replacement.Length ? collectedCounts.Length : replacement.Length;
                    Array.Copy(collectedCounts, replacement, copyCount);
                }

                collectedCounts = replacement;
            }
        }
    }

    [Serializable]
    public struct EnvironmentalStrainDTO
    {
        public float microplasticStrain;
        public float generalPollution;
        public int recycledPlasticItemCount;
        public int discardedItemCount;
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
            if (infectedChunkKeys == null || infectedChunkKeys.Length != MaxInfectedZones)
            {
                long[] replacement = new long[MaxInfectedZones];
                if (infectedChunkKeys != null)
                {
                    int copyCount = infectedChunkKeys.Length < replacement.Length ? infectedChunkKeys.Length : replacement.Length;
                    Array.Copy(infectedChunkKeys, replacement, copyCount);
                }

                infectedChunkKeys = replacement;
            }

            if (infectedSeverities == null || infectedSeverities.Length != MaxInfectedZones)
            {
                float[] replacement = new float[MaxInfectedZones];
                if (infectedSeverities != null)
                {
                    int copyCount = infectedSeverities.Length < replacement.Length ? infectedSeverities.Length : replacement.Length;
                    Array.Copy(infectedSeverities, replacement, copyCount);
                }

                infectedSeverities = replacement;
            }
        }
    }

    [Serializable]
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
    public struct ModuleGraphEdgeDTO
    {
        public int sourceNodeIndex;
        public int destinationNodeIndex;
    }
}
