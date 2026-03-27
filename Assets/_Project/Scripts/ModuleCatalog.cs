// ============================================================================
// HECTON-8 — ModuleCatalog.cs
// Каталог всех строительных модулей.
//
// ScriptableObject — заполняется в редакторе.
// Используется ConstructionManager при загрузке:
//   saved prefabId → catalog.FindPrefabById() → GameObject prefab
//
// Аналогичен ItemCatalog, но для BuildableData / модулей базы.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Building;
using UnityEngine;

namespace Hecton8.Construction
{
    [CreateAssetMenu(
        fileName = "ModuleCatalog",
        menuName = "Hecton/Module Catalog",
        order    = 101)]
    public sealed class ModuleCatalog : ScriptableObject
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("All buildable modules in the project")]
        [Tooltip("Перетащи сюда все BuildableData ассеты")]
        [SerializeField] private List<BuildableData> allModules = new List<BuildableData>();

        // ══════════════════════════════════════════════════════════
        //  LOOKUP — O(1) поиск
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// name → BuildableData. Строится один раз в OnEnable.
        /// Key = BuildableData.name (имя ассета ScriptableObject).
        /// </summary>
        private Dictionary<string, BuildableData> _lookup;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            RebuildLookup();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RebuildLookup();
        }
#endif

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Ищет BuildableData по строковому ID.
        /// ID = ScriptableObject.name (имя файла ассета без расширения).
        /// </summary>
        /// <param name="prefabId">ID модуля из сохранения.</param>
        /// <returns>BuildableData или null.</returns>
        public BuildableData FindDataById(string prefabId)
        {
            if (string.IsNullOrEmpty(prefabId)) return null;

            if (_lookup == null) RebuildLookup();

            _lookup.TryGetValue(prefabId, out BuildableData result);
            return result;
        }

        /// <summary>
        /// Ищет finalPrefab по строковому ID.
        /// Удобный shortcut для ConstructionManager.LoadFromSaveData.
        /// </summary>
        /// <param name="prefabId">ID модуля из сохранения.</param>
        /// <returns>GameObject finalPrefab или null.</returns>
        public GameObject FindPrefabById(string prefabId)
        {
            BuildableData data = FindDataById(prefabId);
            return data != null ? data.finalPrefab : null;
        }

        /// <summary>Количество зарегистрированных модулей.</summary>
        public int Count => allModules != null ? allModules.Count : 0;

        /// <summary>
        /// Read-only доступ к массиву модулей для runtime-циклов и UI.
        /// </summary>
        public IReadOnlyList<BuildableData> Modules => allModules;

        /// <summary>
        /// Возвращает модуль по индексу или null, если индекс некорректен.
        /// </summary>
        public BuildableData GetAt(int index)
        {
            if (allModules == null) return null;
            if ((uint)index >= (uint)allModules.Count) return null;
            return allModules[index];
        }

        /// <summary>
        /// Возвращает индекс BuildableData в каталоге или -1, если его нет.
        /// </summary>
        public int IndexOf(BuildableData data)
        {
            if (data == null || allModules == null) return -1;

            int count = allModules.Count;
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(allModules[i], data))
                    return i;
            }

            return -1;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private void RebuildLookup()
        {
            int count = allModules != null ? allModules.Count : 0;
            _lookup = new Dictionary<string, BuildableData>(count);

            for (int i = 0; i < count; i++)
            {
                BuildableData data = allModules[i];
                if (data != null && !string.IsNullOrEmpty(data.name))
                {
                    if (!_lookup.ContainsKey(data.name))
                        _lookup.Add(data.name, data);
                    else
                        Debug.LogWarning(
                            $"[ModuleCatalog] Duplicate ID: '{data.name}'. " +
                            "Skipping duplicate entry.");
                }
            }
        }
    }
}
