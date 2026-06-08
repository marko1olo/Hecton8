// ============================================================================
// HECTON-8 — ModuleCatalog.cs
// Katalog vseh stroitelnyh moduley.
//
// ScriptableObject — zapolnyaetsya v redaktore.
// Ispolzuetsya ConstructionManager pri zagruzke:
//   saved prefabId → catalog.FindPrefabById() → GameObject prefab
//
// Analogichen ItemCatalog, no dlya BuildableData / moduley bazy.
// ============================================================================

using System;
using System.Collections.Generic;
using Hecton8.Building;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Construction
{
    [CreateAssetMenu(
        fileName = "ModuleCatalog",
        menuName = "Hecton/Module Catalog",
        order    = 101)]
    public sealed class ModuleCatalog : ScriptableObject, IGlobalRegistryHotSwapListener
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("All buildable modules in the project")]
        [Tooltip("Peretaschi syuda vse BuildableData assety")]
        [SerializeField] private List<BuildableData> allModules = new List<BuildableData>(64);

        // ══════════════════════════════════════════════════════════
        //  LOOKUP — O(1) poisk
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// stable ID / legacy asset name → BuildableData. Stroitsya odin raz v OnEnable.
        /// </summary>
        private Dictionary<string, BuildableData> _lookup;
        private Dictionary<int, BuildableData> _hashLookup;
        private bool _hasLookupAmbiguity;
        private string _lookupAmbiguitySummary;
        private List<BuildableData> _runtimeModules;
        private Dictionary<string, string> _runtimeCategoryByPersistentId;
        private Dictionary<string, string> _runtimeModuleOwnerByPersistentId;
        private List<BuildableData> _combinedModulesView;
        private bool _combinedModulesDirty = true;
        private bool _registeredHotSwap;
        private IQuestSystem _cachedQuestSystem;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            RebuildLookup();
            CacheQuestSystemCold();
            TryRegisterHotSwapListener();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            _cachedQuestSystem = null;
            BuildableData.ConfigureBlueprintQuestSystem(null);
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
        /// Ischet BuildableData po strokovomu ID.
        /// Podderzhivaet authored stable ID i legacy asset name.
        /// </summary>
        /// <param name="prefabId">ID modulya iz sohraneniya.</param>
        /// <returns>BuildableData ili null.</returns>
        public BuildableData FindDataById(string prefabId)
        {
            if (string.IsNullOrWhiteSpace(prefabId)) return null;

            prefabId = prefabId.Trim();
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
        /// Ischet finalPrefab po strokovomu ID.
        /// Udobnyy shortcut dlya ConstructionManager.LoadFromSaveData.
        /// </summary>
        /// <param name="prefabId">ID modulya iz sohraneniya.</param>
        /// <returns>GameObject finalPrefab ili null.</returns>
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

        /// <summary>Kolichestvo zaregistrirovannyh moduley.</summary>
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
                return GetViewableCount(_cachedQuestSystem);
            }
        }

        /// <summary>
        /// Counts modules visible through an already-cached quest owner.
        /// </summary>
        public int GetViewableCount(IQuestSystem questSystem)
        {
            int viewableCount = 0;
            int count = Count;
            for (int i = 0; i < count; i++)
            {
                if (IsModuleBlueprintViewable(GetAt(i), questSystem))
                    viewableCount++;
            }

            return viewableCount;
        }

        /// <summary>
        /// Read-only dostup k massivu moduley dlya runtime-tsiklov i UI.
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
            return TryRegisterRuntimeModule(data, customCategory, string.Empty, out error);
        }

        internal bool TryRegisterRuntimeModule(BuildableData data, string customCategory, string ownerId, out string error)
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

            persistentId = persistentId.Trim();
            if (ContainsRuntimeModule(data))
            {
                RecordRuntimeModuleOwnerIfUnownedOrSameOwner(persistentId, ownerId);
                return true;
            }

            if (HasAliasConflict(persistentId, data, out error))
                return false;

            string legacyAlias = data.name;
            if (!string.IsNullOrWhiteSpace(legacyAlias))
                legacyAlias = legacyAlias.Trim();
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
            RecordRuntimeModuleOwner(persistentId, ownerId);
            AddLookupAlias(persistentId, data);
            AddLookupAlias(legacyAlias, data);
            _combinedModulesDirty = true;
            return !_hasLookupAmbiguity;
        }

        internal bool UnregisterRuntimeModulesForOwner(string ownerId)
        {
            ownerId = NormalizeRuntimeOwnerId(ownerId);
            if (string.IsNullOrEmpty(ownerId) ||
                _runtimeModules == null ||
                _runtimeModules.Count == 0 ||
                _runtimeModuleOwnerByPersistentId == null ||
                _runtimeModuleOwnerByPersistentId.Count == 0)
            {
                return false;
            }

            bool removed = false;
            for (int i = _runtimeModules.Count - 1; i >= 0; i--)
            {
                BuildableData data = _runtimeModules[i];
                string persistentId = NormalizeRuntimeModulePersistentId(data);
                if (string.IsNullOrEmpty(persistentId) ||
                    !_runtimeModuleOwnerByPersistentId.TryGetValue(persistentId, out string registeredOwner) ||
                    !string.Equals(registeredOwner, ownerId, StringComparison.Ordinal))
                {
                    continue;
                }

                _runtimeModuleOwnerByPersistentId.Remove(persistentId);
                _runtimeCategoryByPersistentId?.Remove(persistentId);
                _runtimeModules.RemoveAt(i);
                removed = true;
            }

            if (removed)
                RebuildLookup();

            return removed;
        }

        internal bool TryPromoteRuntimeModuleOwnerIfPresent(BuildableData data, string customCategory, string ownerId)
        {
            string persistentId = NormalizeRuntimeModulePersistentId(data);
            if (string.IsNullOrEmpty(persistentId) || !ContainsRuntimeModule(data))
                return false;

            if (RecordRuntimeModuleOwnerIfUnownedOrSameOwner(persistentId, ownerId))
            {
                if (_runtimeCategoryByPersistentId == null)
                    _runtimeCategoryByPersistentId = new Dictionary<string, string>(16); // COLD ALLOC: Dictionary<string,string>[16] - runtime buildable category map restore during owner promotion - owner: ModuleCatalog

                _runtimeCategoryByPersistentId[persistentId] = NormalizeRuntimeCategory(customCategory);
                _combinedModulesDirty = true;
            }

            return true;
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

            string persistentId = data.PersistentId;
            if (string.IsNullOrWhiteSpace(persistentId))
                return false;

            persistentId = persistentId.Trim();
            return _runtimeCategoryByPersistentId.TryGetValue(persistentId, out customCategory);
        }

        /// <summary>
        /// Vozvraschaet modul po indeksu ili null, esli indeks nekorrekten.
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
            return GetViewableAt(index, _cachedQuestSystem);
        }

        /// <summary>
        /// Returns the buildable at a viewable-only catalog index using an already-cached quest owner.
        /// </summary>
        public BuildableData GetViewableAt(int index, IQuestSystem questSystem)
        {
            if (index < 0)
                return null;

            int viewableIndex = 0;
            int count = Count;
            for (int i = 0; i < count; i++)
            {
                BuildableData data = GetAt(i);
                if (!IsModuleBlueprintViewable(data, questSystem))
                    continue;

                if (viewableIndex == index)
                    return data;

                viewableIndex++;
            }

            return null;
        }

        /// <summary>
        /// Vozvraschaet indeks BuildableData v kataloge ili -1, esli ego net.
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
            return IndexOfViewable(data, _cachedQuestSystem);
        }

        /// <summary>
        /// Returns the viewable-only index using an already-cached quest owner.
        /// </summary>
        public int IndexOfViewable(BuildableData data, IQuestSystem questSystem)
        {
            if (data == null)
                return -1;

            int viewableIndex = 0;
            int count = Count;
            for (int i = 0; i < count; i++)
            {
                BuildableData candidate = GetAt(i);
                if (!IsModuleBlueprintViewable(candidate, questSystem))
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

        private static bool IsModuleBlueprintViewable(BuildableData data, IQuestSystem questSystem)
        {
            return data != null && data.IsBlueprintViewable(questSystem);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.QuestSystem &&
                serviceSlot != GlobalRegistryServiceSlot.QuestRuntime)
            {
                return;
            }

            _cachedQuestSystem = currentService as IQuestSystem;
            BuildableData.ConfigureBlueprintQuestSystem(_cachedQuestSystem);
        }

        private void CacheQuestSystemCold()
        {
            if (!Application.isPlaying)
                return;

            _cachedQuestSystem = GlobalRegistry.QuestSystem;
            BuildableData.ConfigureBlueprintQuestSystem(_cachedQuestSystem);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void AddLookupAlias(string id, BuildableData data)
        {
            if (string.IsNullOrWhiteSpace(id) || data == null)
                return;

            id = id.Trim();
            if (_lookup.TryGetValue(id, out BuildableData existing))
            {
                if (!ReferenceEquals(existing, data))
                {
                    RegisterLookupAmbiguity(id, existing, data);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogWarning($"[ModuleCatalog] Duplicate ID alias '{id}'. Skipping duplicate entry.", data);
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

        private void RecordRuntimeModuleOwner(string persistentId, string ownerId)
        {
            persistentId = NormalizeRuntimeModulePersistentId(persistentId);
            if (string.IsNullOrEmpty(persistentId))
                return;

            ownerId = NormalizeRuntimeOwnerId(ownerId);
            if (string.IsNullOrEmpty(ownerId))
            {
                _runtimeModuleOwnerByPersistentId?.Remove(persistentId);
                return;
            }

            if (_runtimeModuleOwnerByPersistentId == null)
                _runtimeModuleOwnerByPersistentId = new Dictionary<string, string>(16); // COLD ALLOC: Dictionary<string,string>[16] — mod owner index for runtime buildable overlay cleanup — owner: ModuleCatalog

            _runtimeModuleOwnerByPersistentId[persistentId] = ownerId;
        }

        private bool RecordRuntimeModuleOwnerIfUnownedOrSameOwner(string persistentId, string ownerId)
        {
            persistentId = NormalizeRuntimeModulePersistentId(persistentId);
            ownerId = NormalizeRuntimeOwnerId(ownerId);
            if (string.IsNullOrEmpty(persistentId) || string.IsNullOrEmpty(ownerId))
                return false;

            if (_runtimeModuleOwnerByPersistentId != null &&
                _runtimeModuleOwnerByPersistentId.TryGetValue(persistentId, out string registeredOwner) &&
                !string.IsNullOrEmpty(registeredOwner) &&
                !string.Equals(registeredOwner, ownerId, StringComparison.Ordinal))
            {
                return false;
            }

            RecordRuntimeModuleOwner(persistentId, ownerId);
            return true;
        }

        private static string NormalizeRuntimeModulePersistentId(BuildableData data)
        {
            return data != null ? NormalizeRuntimeModulePersistentId(data.PersistentId) : string.Empty;
        }

        private static string NormalizeRuntimeModulePersistentId(string persistentId)
        {
            return string.IsNullOrWhiteSpace(persistentId) ? string.Empty : persistentId.Trim();
        }

        private bool HasAliasConflict(string alias, BuildableData data, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(alias))
                return false;

            alias = alias.Trim();
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

        private static string NormalizeRuntimeOwnerId(string ownerId)
        {
            return string.IsNullOrWhiteSpace(ownerId) ? string.Empty : ownerId.Trim();
        }
    }
}
