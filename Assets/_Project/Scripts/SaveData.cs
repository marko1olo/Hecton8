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
        public const int CurrentVersion = 2;

        // ─────────────────────── DTO Sections ────────────────────

        /// <summary>Состояние игрока (HP, O2, позиция).</summary>
        public PlayerStatsDTO playerStats;

        /// <summary>Содержимое инвентаря.</summary>
        public InventoryDTO inventory;

        /// <summary>Состояние мира (уничтоженные узлы).</summary>
        public WorldStateDTO worldState;

        /// <summary>Построенные модули базы.</summary>
        public ConstructionDTO construction;

        /// <summary>Прочность инструментов (toolID → durability). v2.0 ENTERPRISE</summary>
        public ES3SerializableDictionary<string, float> toolDurabilityMap = new ES3SerializableDictionary<string, float>();

        /// <summary>Сломанные инструменты (toolID → broken). v2.0 ENTERPRISE</summary>
        public ES3SerializableDictionary<string, bool> toolBrokenMap = new ES3SerializableDictionary<string, bool>();

        // ═════════════════════════════════════════════════════════
        //  Factory — создание нового SaveData с метаданными
        // ═════════════════════════════════════════════════════════

        /// <summary>
        /// Создаёт новый SaveData с заполненными метаданными.
        /// Вызывается SaveManager перед сбором данных от ISaveable.
        /// </summary>
        public static SaveData CreateNew(float playTime)
        {
            return new SaveData
            {
                version       = CurrentVersion,
                timestamp     = DateTime.Now.ToString("O"), // ISO 8601
                totalPlayTime = playTime,
                playerStats   = new PlayerStatsDTO(),
                inventory     = new InventoryDTO(),
                worldState    = new WorldStateDTO(),
                construction  = new ConstructionDTO()
            };
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  PlayerStatsDTO — состояние скафандра и позиция игрока
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Статы игрока: O2, энергия, целостность, позиция/поворот.
    /// Примитивные типы для портируемости сериализации.
    /// </summary>
    [Serializable]
    public struct PlayerStatsDTO
    {
        // ── Survival Stats ──
        public float oxygen;
        public float energy;
        public float integrity;
        public float weight;

        // ── Position (world space) ──
        public float posX;
        public float posY;
        public float posZ;

        // ── Rotation (quaternion components) ──
        public float rotX;
        public float rotY;
        public float rotZ;
        public float rotW;

        // ── Helpers (not serialized — computed on access) ──

        /// <summary>Восстанавливает Vector3 из сохранённых компонент.</summary>
        public Vector3 GetPosition() => new Vector3(posX, posY, posZ);

        /// <summary>Восстанавливает Quaternion из сохранённых компонент.</summary>
        public Quaternion GetRotation() => new Quaternion(rotX, rotY, rotZ, rotW);

        /// <summary>Сохраняет Vector3 в компоненты.</summary>
        public void SetPosition(Vector3 pos)
        {
            posX = pos.x;
            posY = pos.y;
            posZ = pos.z;
        }

        /// <summary>Сохраняет Quaternion в компоненты.</summary>
        public void SetRotation(Quaternion rot)
        {
            rotX = rot.x;
            rotY = rot.y;
            rotZ = rot.z;
            rotW = rot.w;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  InventoryDTO — содержимое тетрис-инвентаря
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Снимок инвентаря: массив занятых ячеек + вес.
    ///
    /// Каждая InventoryCellDTO представляет якорную ячейку
    /// (верхний-левый угол) одного предмета. Multi-cell предметы
    /// сохраняются как одна запись с (x, y) якоря.
    /// </summary>
    [Serializable]
    public struct InventoryDTO
    {
        /// <summary>Количество валидных записей в массиве cells.</summary>
        public int cellCount;

        /// <summary>
        /// Массив занятых ячеек. Pre-allocated с запасом.
        /// Только первые cellCount записей валидны.
        /// </summary>
        public InventoryCellDTO[] cells;

        /// <summary>Суммарный вес инвентаря.</summary>
        public float totalWeight;

        /// <summary>Размеры сетки (для валидации при загрузке).</summary>
        public int gridColumns;
        public int gridRows;

        /// <summary>Максимальный размер массива cells.</summary>
        public const int MaxCells = 128;

        /// <summary>Инициализирует пустой массив.</summary>
        public void EnsureCapacity()
        {
            if (cells == null || cells.Length < MaxCells)
                cells = new InventoryCellDTO[MaxCells];
        }
    }

    /// <summary>
    /// Одна ячейка инвентаря: позиция в сетке + ID предмета.
    /// </summary>
    [Serializable]
    public struct InventoryCellDTO
    {
        /// <summary>Колонка якорной ячейки.</summary>
        public int x;

        /// <summary>Строка якорной ячейки.</summary>
        public int y;

        /// <summary>
        /// Строковый ID предмета (ItemData.name из ScriptableObject).
        /// Используется для поиска ассета через каталог при загрузке.
        /// </summary>
        public string itemId;
    }

    // ══════════════════════════════════════════════════════════════════
    //  WorldStateDTO — состояние мира
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Состояние разрушаемых объектов мира.
    /// Хранит ID уничтоженных (depleted) ресурсных узлов.
    ///
    /// При загрузке: все узлы в сцене — активны по умолчанию.
    /// Узлы из списка depletedNodeIds — деактивируются.
    /// </summary>
    [Serializable]
    public struct WorldStateDTO
    {
        /// <summary>Количество валидных записей.</summary>
        public int depletedCount;

        /// <summary>ID уничтоженных ресурсных узлов.</summary>
        public string[] depletedNodeIds;

        /// <summary>Максимальный размер массива.</summary>
        public const int MaxNodes = 512;

        public void EnsureCapacity()
        {
            if (depletedNodeIds == null || depletedNodeIds.Length < MaxNodes)
                depletedNodeIds = new string[MaxNodes];
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  ConstructionDTO — построенные модули базы
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Список всех модулей, построенных игроком.
    /// При загрузке: все существующие модули удаляются через пул,
    /// затем спавнятся из сейва.
    /// </summary>
    [Serializable]
    public struct ConstructionDTO
    {
        /// <summary>Количество валидных записей.</summary>
        public int moduleCount;

        /// <summary>Массив модулей.</summary>
        public ModuleDTO[] modules;

        /// <summary>Максимальный размер.</summary>
        public const int MaxModules = 256;

        public void EnsureCapacity()
        {
            if (modules == null || modules.Length < MaxModules)
                modules = new ModuleDTO[MaxModules];
        }
    }

    /// <summary>
    /// Один модуль базы: ID префаба + трансформ + динамическое состояние.
    ///
    /// Поля integrity и isFlooded добавлены в v2.
    /// Старые сейвы (v1) получат дефолтные значения: integrity = 0f, isFlooded = false.
    /// При загрузке integrity == 0f интерпретируется как «полное здоровье» (миграция).
    /// </summary>
    [Serializable]
    public struct ModuleDTO
    {
        /// <summary>Строковый ID префаба (BuildableData.name).</summary>
        public string prefabId;

        // ── Position ──
        public float posX;
        public float posY;
        public float posZ;

        // ── Rotation ──
        public float rotX;
        public float rotY;
        public float rotZ;
        public float rotW;

        // ── Dynamic State (v2) ──

        /// <summary>
        /// Текущая целостность модуля (0..maxIntegrity).
        /// Значение 0f в старых сейвах = «не сохранялось» → трактуется как 100%.
        /// </summary>
        public float integrity;

        /// <summary>Затоплен ли модуль.</summary>
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