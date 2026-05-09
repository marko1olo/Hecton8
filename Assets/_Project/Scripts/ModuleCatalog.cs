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

using System;
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
        /// stable ID / legacy asset name → BuildableData. Строится один раз в OnEnable.
        /// </summary>
        private Dictionary<string, BuildableData> _lookup;
        private Dictionary<int, BuildableData> _hashLookup;
        private bool _hasLookupAmbiguity;
        private string _lookupAmbiguitySummary;
        private List<BuildableData> _runtimeModules;
        private Dictionary<string, string> _runtimeCategoryByPersistentId;
        private List<BuildableData> _combinedModulesView;
        private bool _combinedModulesDirty = true;

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
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            RebuildLookup();
        }
#endif

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Ищет BuildableData по строковому ID.
        /// Поддерживает authored stable ID и legacy asset name.
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

        internal bool HasLookupAmbiguity
        {
            get
            {
                if (_lookup == null) RebuildLookup();
                return _hasLookupAmbiguity;
            }
        }

        internal string LookupAmbiguitySummary
        {
            get
            {
                if (_lookup == null) RebuildLookup();
                return _lookupAmbiguitySummary;
            }
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

        public BuildableData FindDataByHashId(int moduleHashId)
        {
            if (moduleHashId == 0)
                return null;

            if (_hashLookup == null)
                RebuildLookup();

            _hashLookup.TryGetValue(moduleHashId, out BuildableData result);
            return result;
        }

        /// <summary>Количество зарегистрированных модулей.</summary>
        public int Count
        {
            get
            {
                int authoredCount = allModules != null ? allModules.Count : 0;
                int runtimeCount = _runtimeModules != null ? _runtimeModules.Count : 0;
                return authoredCount + runtimeCount;
            }
        }

        /// <summary>
        /// Number of modules whose blueprint gate is currently open.
        /// </summary>
        public int ViewableCount
        {
            get
            {
                int viewableCount = 0;
                int count = Count;
                for (int i = 0; i < count; i++)
                {
                    if (IsModuleBlueprintViewable(GetAt(i)))
                        viewableCount++;
                }

                return viewableCount;
            }
        }

        /// <summary>
        /// Read-only доступ к массиву модулей для runtime-циклов и UI.
        /// </summary>
        public IReadOnlyList<BuildableData> Modules
        {
            get
            {
                if (_runtimeModules == null || _runtimeModules.Count == 0)
                    return allModules;

                EnsureCombinedModulesView();
                return _combinedModulesView;
            }
        }

        /// <summary>
        /// Registers a runtime-only buildable overlay without mutating the authored ScriptableObject asset list.
        /// Runtime registrations extend lookup, cycling, and save-facing module resolution through the active catalog.
        /// </summary>
        /// <param name="data">Runtime buildable asset to expose through the active module catalog.</param>
        /// <param name="customCategory">Runtime-only category label stored as sidecar metadata for mod-facing browsers.</param>
        /// <param name="error">Human-readable failure reason when the registration is rejected.</param>
        /// <returns>True when the buildable was accepted into the runtime overlay.</returns>
        public bool TryRegisterRuntimeModule(BuildableData data, string customCategory, out string error)
        {
            error = null;

            if (data == null)
            {
                error = "BuildableData is null.";
                return false;
            }

            if (_lookup == null)
                RebuildLookup();

            if (_hasLookupAmbiguity)
            {
                error = LookupAmbiguitySummary;
                return false;
            }

            string persistentId = data.PersistentId;
            if (string.IsNullOrWhiteSpace(persistentId))
            {
                error = "PersistentId is empty.";
                return false;
            }

            if (ContainsRuntimeModule(data))
                return true;

            if (HasAliasConflict(persistentId, data, out error))
                return false;

            string legacyAlias = data.name;
            if (!string.Equals(legacyAlias, persistentId, StringComparison.Ordinal) &&
                HasAliasConflict(legacyAlias, data, out error))
            {
                return false;
            }

            if (_runtimeModules == null)
                _runtimeModules = new List<BuildableData>(16); // COLD ALLOC: List<BuildableData>[16] — runtime-only mod buildable overlay — owner: ModuleCatalog

            if (_runtimeCategoryByPersistentId == null)
                _runtimeCategoryByPersistentId = new Dictionary<string, string>(16); // COLD ALLOC: Dictionary<string,string>[16] — runtime buildable category map — owner: ModuleCatalog

            _runtimeModules.Add(data);
            _runtimeCategoryByPersistentId[persistentId] = NormalizeRuntimeCategory(customCategory);
            AddLookupAlias(persistentId, data);
            AddLookupAlias(legacyAlias, data);
            _combinedModulesDirty = true;
            return !_hasLookupAmbiguity;
        }

        /// <summary>
        /// Returns the runtime-only custom category assigned during mod registration.
        /// Authored modules without runtime metadata return false.
        /// </summary>
        /// <param name="data">Buildable asset to inspect.</param>
        /// <param name="customCategory">Resolved runtime category when metadata exists.</param>
        /// <returns>True when the buildable was injected through the runtime mod overlay.</returns>
        public bool TryGetRuntimeCategory(BuildableData data, out string customCategory)
        {
            customCategory = string.Empty;
            if (data == null || _runtimeCategoryByPersistentId == null)
                return false;

            return _runtimeCategoryByPersistentId.TryGetValue(data.PersistentId, out customCategory);
        }

        /// <summary>
        /// Возвращает модуль по индексу или null, если индекс некорректен.
        /// </summary>
        public BuildableData GetAt(int index)
        {
            int authoredCount = allModules != null ? allModules.Count : 0;
            if (index < 0)
                return null;

            if (index < authoredCount)
                return allModules[index];

            int runtimeIndex = index - authoredCount;
            if (_runtimeModules == null || (uint)runtimeIndex >= (uint)_runtimeModules.Count)
                return null;

            return _runtimeModules[runtimeIndex];
        }

        /// <summary>
        /// Returns the buildable at a viewable-only catalog index, skipping locked blueprints.
        /// </summary>
        public BuildableData GetViewableAt(int index)
        {
            if (index < 0)
                return null;

            int viewableIndex = 0;
            int count = Count;
            for (int i = 0; i < count; i++)
            {
                BuildableData data = GetAt(i);
                if (!IsModuleBlueprintViewable(data))
                    continue;

                if (viewableIndex == index)
                    return data;

                viewableIndex++;
            }

            return null;
        }

        /// <summary>
        /// Возвращает индекс BuildableData в каталоге или -1, если его нет.
        /// </summary>
        public int IndexOf(BuildableData data)
        {
            if (data == null)
                return -1;

            int count = allModules != null ? allModules.Count : 0;
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(allModules[i], data))
                    return i;
            }

            if (_runtimeModules != null)
            {
                for (int i = 0; i < _runtimeModules.Count; i++)
                {
                    if (ReferenceEquals(_runtimeModules[i], data))
                        return count + i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Returns the viewable-only index of a buildable, or -1 when the module is locked or absent.
        /// </summary>
        public int IndexOfViewable(BuildableData data)
        {
            if (data == null)
                return -1;

            int viewableIndex = 0;
            int count = Count;
            for (int i = 0; i < count; i++)
            {
                BuildableData candidate = GetAt(i);
                if (!IsModuleBlueprintViewable(candidate))
                    continue;

                if (ReferenceEquals(candidate, data))
                    return viewableIndex;

                viewableIndex++;
            }

            return -1;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private void RebuildLookup()
        {
            int authoredCount = allModules != null ? allModules.Count : 0;
            int runtimeCount = _runtimeModules != null ? _runtimeModules.Count : 0;
            _lookup = new Dictionary<string, BuildableData>((authoredCount + runtimeCount) * 2);
            _hashLookup = new Dictionary<int, BuildableData>(authoredCount + runtimeCount);
            _hasLookupAmbiguity = false;
            _lookupAmbiguitySummary = string.Empty;
            _combinedModulesDirty = true;

            for (int i = 0; i < authoredCount; i++)
            {
                BuildableData data = allModules[i];
                if (data == null)
                    continue;

                AddLookupAlias(data.PersistentId, data);
                AddLookupAlias(data.name, data);
                AddHashAlias(data.ModuleHashId, data);
            }

            if (_runtimeModules == null)
                return;

            for (int i = 0; i < _runtimeModules.Count; i++)
            {
                BuildableData runtimeModule = _runtimeModules[i];
                if (runtimeModule == null)
                    continue;

                AddLookupAlias(runtimeModule.PersistentId, runtimeModule);
                AddLookupAlias(runtimeModule.name, runtimeModule);
                AddHashAlias(runtimeModule.ModuleHashId, runtimeModule);
            }
        }

        private static bool IsModuleBlueprintViewable(BuildableData data)
        {
            return data != null && data.IsBlueprintViewable();
        }

        private void AddLookupAlias(string id, BuildableData data)
        {
            if (string.IsNullOrEmpty(id) || data == null)
                return;

            if (_lookup.TryGetValue(id, out BuildableData existing))
            {
                if (!ReferenceEquals(existing, data))
                {
                    RegisterLookupAmbiguity(id, existing, data);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning($"[ModuleCatalog] Duplicate ID alias '{id}'. Skipping duplicate entry.", data);
#endif
                }

                return;
            }

            _lookup.Add(id, data);
        }

        private void RegisterLookupAmbiguity(string alias, BuildableData existing, BuildableData incoming)
        {
            _hasLookupAmbiguity = true;

            if (!string.IsNullOrEmpty(_lookupAmbiguitySummary))
                return;

            string existingName = existing != null ? existing.name : "null";
            string incomingName = incoming != null ? incoming.name : "null";
            _lookupAmbiguitySummary =
                $"Alias '{alias}' resolves to both '{existingName}' and '{incomingName}'.";
        }

        private void AddHashAlias(int moduleHashId, BuildableData data)
        {
            if (moduleHashId == 0 || data == null)
                return;

            if (_hashLookup.TryGetValue(moduleHashId, out BuildableData existing))
            {
                if (!ReferenceEquals(existing, data))
                {
                    _hasLookupAmbiguity = true;
                    if (string.IsNullOrEmpty(_lookupAmbiguitySummary))
                        _lookupAmbiguitySummary = $"Module hash '{moduleHashId}' resolves to both '{existing.name}' and '{data.name}'.";
                }

                return;
            }

            _hashLookup.Add(moduleHashId, data);
        }

        private void EnsureCombinedModulesView()
        {
            if (!_combinedModulesDirty && _combinedModulesView != null)
                return;

            int totalCount = Count;
            if (_combinedModulesView == null)
                _combinedModulesView = new List<BuildableData>(totalCount); // COLD ALLOC: List<BuildableData>[catalog count] — combined authored/runtime module view — owner: ModuleCatalog
            else
                _combinedModulesView.Clear();

            if (allModules != null)
            {
                for (int i = 0; i < allModules.Count; i++)
                    _combinedModulesView.Add(allModules[i]);
            }

            if (_runtimeModules != null)
            {
                for (int i = 0; i < _runtimeModules.Count; i++)
                    _combinedModulesView.Add(_runtimeModules[i]);
            }

            _combinedModulesDirty = false;
        }

        private bool ContainsRuntimeModule(BuildableData data)
        {
            if (_runtimeModules == null || data == null)
                return false;

            for (int i = 0; i < _runtimeModules.Count; i++)
            {
                if (ReferenceEquals(_runtimeModules[i], data))
                    return true;
            }

            return false;
        }

        private bool HasAliasConflict(string alias, BuildableData data, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(alias))
                return false;

            if (_lookup.TryGetValue(alias, out BuildableData existing) && !ReferenceEquals(existing, data))
            {
                error = $"Alias '{alias}' already belongs to '{existing.name}'.";
                return true;
            }

            return false;
        }

        private static string NormalizeRuntimeCategory(string customCategory)
        {
            return string.IsNullOrWhiteSpace(customCategory) ? "Mods" : customCategory.Trim();
        }
    }
}
