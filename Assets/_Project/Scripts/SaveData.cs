// ============================================================================
// HECTON-8 — SaveData.cs
// Главный контейнер сохранения. Паттерн DTO (Data Transfer Object).
//
// ВСЕ данные сохранения — здесь. Один объект → одна сериализация.
// Easy Save 3 (ES3) сериализует этот класс целиком.
//
// ДИЗАЙН-РЕШЕНИЯ:
//   • [Serializable] struct для вложенных DTO — минимум heap-аллокаций.
//   • Примитивные типы вместо Vector3/Quaternion — ES3 совместимость
//     и портируемость (JSON, binary, XML).
//   • string ID вместо int InstanceID — стабильность между сессиями.
//   • Версионирование: поле version для миграции данных.
//   • Pre-allocated массивы вместо List — контроль размера.
//
// РАСШИРЕНИЕ:
//   Добавляй новые DTO как поля SaveData. Старые сейвы получат
//   дефолтные значения для новых полей (ES3 обрабатывает gracefully).
// ============================================================================

using System;
using System.Collections.Generic;
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
        public float totalPlayTime;

        /// <summary>Текущая версия формата. Используется для миграции.</summary>
        public const int CurrentVersion = 9; // Increment for discovery latest biome persistence

        // ─────────────────────── DTO Sections ────────────────────

        public PlayerStatsDTO playerStats;
        public InventoryDTO inventory;
        public WorldStateDTO worldState;
        public ProceduralWorldStateDTO proceduralWorldState;
        public ConstructionDTO construction;
        public ScanLogDTO scanLog;
        public BarterDTO barter;
        public FieldOperationLogDTO fieldOperations;
        public BeaconNetworkDTO beaconNetwork;

        /// <summary>Прочность инструментов (toolID → durability). v2.0 ENTERPRISE</summary>
        public Dictionary<string, float> toolDurabilityMap = new Dictionary<string, float>();

        /// <summary>Сломанные инструменты (toolID → broken). v2.0 ENTERPRISE</summary>
        public Dictionary<string, bool> toolBrokenMap = new Dictionary<string, bool>();

        /// <summary>Список ID открытых биомов. v3.0 MASTER GRADE</summary>
        public HashSet<int> discoveredBiomeIds = new HashSet<int>();

        /// <summary>Последний подтвержденный открытый биом для PDA и HUD.</summary>
        public int lastDiscoveredBiomeId = -1;

        // ═════════════════════════════════════════════════════════
        //  Factory — создание нового SaveData с метаданными
        // ═════════════════════════════════════════════════════════

        public static SaveData CreateNew(float playTime)
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
                discoveredBiomeIds = new HashSet<int>(),
                lastDiscoveredBiomeId = -1
            };
        }
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

        public float posX;
        public float posY;
        public float posZ;

        public float rotX;
        public float rotY;
        public float rotZ;
        public float rotW;

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

    // ══════════════════════════════════════════════════════════════════
    //  InventoryDTO
    // ══════════════════════════════════════════════════════════════════

    [Serializable]
    public struct InventoryDTO
    {
        public int cellCount;
        public InventoryCellDTO[] cells;
        public float totalWeight;
        public int gridColumns;
        public int gridRows;

        public const int MaxCells = 128;

        public void EnsureCapacity()
        {
            if (cells == null || cells.Length < MaxCells)
                cells = new InventoryCellDTO[MaxCells];
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
        public const int MaxNodes = 512;

        public void EnsureCapacity()
        {
            if (depletedNodeIds == null || depletedNodeIds.Length < MaxNodes)
                depletedNodeIds = new string[MaxNodes];
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
    public struct ProceduralWorldStateDTO
    {
        public int suppressedPlacementCount;
        public long[] suppressedPlacementKeys;
        public int faunaStateCount;
        public ProceduralFaunaStateDTO[] faunaStates;

        public const int MaxSuppressedPlacements = 8192;
        public const int MaxFaunaStates = 4096;

        public void EnsureCapacity()
        {
            if (suppressedPlacementKeys == null || suppressedPlacementKeys.Length < MaxSuppressedPlacements)
                suppressedPlacementKeys = new long[MaxSuppressedPlacements];

            if (faunaStates == null || faunaStates.Length < MaxFaunaStates)
                faunaStates = new ProceduralFaunaStateDTO[MaxFaunaStates];
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
        public const int MaxModules = 256;

        public void EnsureCapacity()
        {
            if (modules == null || modules.Length < MaxModules)
                modules = new ModuleDTO[MaxModules];
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
    public struct ModuleDTO
    {
        public string prefabId;
        public float posX;
        public float posY;
        public float posZ;
        public float rotX;
        public float rotY;
        public float rotZ;
        public float rotW;
        public float integrity;
        public bool isFlooded;

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
}
