// ============================================================================
// HECTON-8 — ItemCatalog.cs
// Каталог всех ItemData в игре. Нужен для save/load:
// сохраняем string ID → загружаем → ищем ItemData по ID.
//
// ScriptableObject. Заполняется вручную или автоматически
// через Editor-скрипт, собирающий все ItemData из проекта.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Items;
using UnityEngine;

namespace Hecton8.SaveSystem
{
    [CreateAssetMenu(
        fileName = "ItemCatalog",
        menuName = "Hecton/Item Catalog",
        order    = 100)]
    public sealed class ItemCatalog : ScriptableObject
    {
        [Header("All item assets in the project")]
        [SerializeField] private List<ItemData> allItems = new List<ItemData>();

        /// <summary>
        /// Словарь: name → ItemData. Строится один раз в OnEnable.
        /// Используется для O(1) поиска при загрузке инвентаря.
        /// </summary>
        private Dictionary<string, ItemData> _lookup;

        private void OnEnable()
        {
            RebuildLookup();
        }

        /// <summary>
        /// Ищет ItemData по строковому ID (ScriptableObject.name).
        /// Возвращает null, если не найден.
        /// </summary>
        public ItemData FindById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            if (_lookup == null) RebuildLookup();

            _lookup.TryGetValue(id, out ItemData result);
            return result;
        }

        private void RebuildLookup()
        {
            _lookup = new Dictionary<string, ItemData>(allItems.Count);
            for (int i = 0, count = allItems.Count; i < count; i++)
            {
                ItemData item = allItems[i];
                if (item != null && !_lookup.ContainsKey(item.name))
                    _lookup.Add(item.name, item);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            RebuildLookup();
        }
#endif
    }
}
