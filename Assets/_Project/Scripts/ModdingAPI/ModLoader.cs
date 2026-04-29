using System;
using System.Collections.Generic;
using System.IO;
using Hecton8.Bootstrap;
using Hecton8.Crafting;
using Hecton8.Items;
using Hecton8.SaveSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Modding
{
    internal static class ModLoader
    {
        private sealed class BootstrapEventListener : ISceneBootstrapEventListener
        {
            public void OnSceneBootstrapEvent(in SceneBootstrapEventPayload payload)
            {
                if ((SceneBootstrapEventType)payload.EventType == SceneBootstrapEventType.GameReady)
                    HandleGameReady();
            }
        }

        private sealed class SaveEventListener : ISaveEventListener
        {
            public void OnSaveEvent(in SaveEventPayload payload)
            {
                if (payload.Type == SaveEventType.LoadCompleted)
                    HandleLoadCompleted(payload.SlotName.ToString());
            }
        }

        private const string ManifestFileName = "mod.json";
        private const string DefaultAssemblyExtension = ".dll";
        private const string DefaultBundleExtension = ".bundle";

        // COLD ALLOC: List<LoadedMod>[16] — successfully instantiated managed mods — owner: ModLoader
        private static readonly List<LoadedMod> _loadedMods = new List<LoadedMod>(16);
        // COLD ALLOC: List<ModRuntimeInfo>[32] — discovered runtime info descriptors for UI and diagnostics — owner: ModLoader
        private static readonly List<ModRuntimeInfo> _runtimeInfos = new List<ModRuntimeInfo>(32);
        // COLD ALLOC: Dictionary<string,int>[32] — modId to runtime info index lookup — owner: ModLoader
        private static readonly Dictionary<string, int> _runtimeInfoIndexById = new Dictionary<string, int>(32);
        // COLD ALLOC: Dictionary<string,Func<IHectonMod>>[16] — explicit boot-registered managed mod factories — owner: ModLoader
        private static readonly Dictionary<string, Func<IHectonMod>> _managedModFactories =
            new Dictionary<string, Func<IHectonMod>>(16);
        // COLD ALLOC: SaveEventListener[1] — static save-event bridge for mod runtime hooks — owner: ModLoader
        private static readonly SaveEventListener _saveEventListener = new SaveEventListener();
        private static readonly BootstrapEventListener _bootstrapEventListener = new BootstrapEventListener();

        private static bool _bootstrapped;
        private static bool _modsInitialized;
        private static bool _hooksInstalled;
        private static bool _shutdownInvoked;

        internal static event Action RuntimeRegistryChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ShutdownLoadedMods();
            UninstallHooks();
            _loadedMods.Clear();
            _runtimeInfos.Clear();
            _runtimeInfoIndexById.Clear();
            _managedModFactories.Clear();
            _bootstrapped = false;
            _modsInitialized = false;
            _hooksInstalled = false;
            _shutdownInvoked = false;
            RuntimeRegistryChanged = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_bootstrapped)
                return;

            _bootstrapped = true;
            InstallHooks();
            DiscoverAndLoadMods();
            ModLocalizationBridge.FlushPendingInjections();
        }

        internal static void CollectRuntimeInfo(List<ModRuntimeInfo> destination)
        {
            if (destination == null)
                return;

            destination.Clear();
            for (int i = 0; i < _runtimeInfos.Count; i++)
                destination.Add(_runtimeInfos[i]);
        }

        internal static bool TryGetModDirectory(string modId, out string directoryPath)
        {
            directoryPath = null;
            if (!_runtimeInfoIndexById.TryGetValue(modId, out int index))
                return false;

            directoryPath = _runtimeInfos[index].DirectoryPath;
            return !string.IsNullOrWhiteSpace(directoryPath);
        }

        internal static bool RegisterManagedFactory(string modId, Func<IHectonMod> factory)
        {
            if (string.IsNullOrWhiteSpace(modId) || factory == null)
                return false;

            _managedModFactories[modId] = factory;
            return true;
        }

        internal static void UnregisterManagedFactory(string modId)
        {
            if (string.IsNullOrWhiteSpace(modId))
                return;

            _managedModFactories.Remove(modId);
        }

        private static void InstallHooks()
        {
            if (_hooksInstalled)
                return;

            SaveEvents.Register(_saveEventListener);
            CraftingEvents.OnCraftCompleted += HandleCraftCompleted;
            SceneBootstrap.Register(_bootstrapEventListener);
            Application.quitting += HandleApplicationQuitting;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            _hooksInstalled = true;
        }

        private static void UninstallHooks()
        {
            if (!_hooksInstalled)
                return;

            SaveEvents.Unregister(_saveEventListener);
            CraftingEvents.OnCraftCompleted -= HandleCraftCompleted;
            SceneBootstrap.Unregister(_bootstrapEventListener);
            Application.quitting -= HandleApplicationQuitting;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            _hooksInstalled = false;
        }

        private static void DiscoverAndLoadMods()
        {
#if ENABLE_IL2CPP
            Debug.LogWarning("[ModLoader] WARNING: External managed code mods require a Mono scripting backend. IL2CPP builds cannot load runtime assemblies dynamically.");
            return;
#else
            string modsRoot = ResolveModsRoot();
            if (string.IsNullOrEmpty(modsRoot) || !Directory.Exists(modsRoot))
                return;

            string[] manifestPaths = Directory.GetFiles(modsRoot, ManifestFileName, SearchOption.AllDirectories);
            if (manifestPaths == null || manifestPaths.Length == 0)
                return;

            // COLD ALLOC: List<ModCandidate>[manifest count] — discovered manifests before dependency sort — owner: ModLoader
            List<ModCandidate> candidates = new List<ModCandidate>(manifestPaths.Length);

            for (int i = 0; i < manifestPaths.Length; i++)
            {
                if (TryReadManifest(manifestPaths[i], out ModCandidate candidate))
                    candidates.Add(candidate);
            }

            if (candidates.Count == 0)
                return;

            // COLD ALLOC: List<ModCandidate>[candidate count] — dependency-sorted mod load order — owner: ModLoader
            List<ModCandidate> loadOrder = new List<ModCandidate>(candidates.Count);
            BuildLoadOrder(candidates, loadOrder);

            for (int i = 0; i < loadOrder.Count; i++)
                TryLoadCandidate(loadOrder[i]);

            for (int i = 0; i < candidates.Count; i++)
            {
                ModCandidate candidate = candidates[i];
                if (!candidate.IsDisabled)
                    continue;

                RecordRuntimeInfo(new ModRuntimeInfo
                {
                    Metadata = candidate.Metadata,
                    Status = ModLoadStatus.Disabled,
                    DirectoryPath = candidate.ModDirectory,
                    StatusMessage = candidate.DisabledReason ?? "Disabled.",
                    AssetBundlePath = candidate.BundlePath,
                    HasManagedEntry = candidate.HasManagedEntry,
                    HasLocalizationFiles = candidate.LocalizationFiles != null && candidate.LocalizationFiles.Length > 0
                });
            }
#endif
        }

        private static bool TryReadManifest(string manifestPath, out ModCandidate candidate)
        {
            candidate = null;

            try
            {
                string json = File.ReadAllText(manifestPath);
                ModManifest manifest = JsonUtility.FromJson<ModManifest>(json);

                if (string.IsNullOrWhiteSpace(manifest.Id))
                {
                    Debug.LogWarning($"[ModLoader] Skipped manifest '{manifestPath}': missing Id.");
                    return false;
                }

                string modDirectory = Path.GetDirectoryName(manifestPath);
                string assemblyPath = ResolveAssemblyPath(modDirectory, manifest);
                bool hasManagedEntry =
                    !string.IsNullOrWhiteSpace(manifest.EntryAssembly) ||
                    !string.IsNullOrWhiteSpace(manifest.EntryType) ||
                    !string.IsNullOrWhiteSpace(assemblyPath);

                candidate = BuildCandidate(manifest, manifestPath, modDirectory, assemblyPath, hasManagedEntry);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ModLoader] Failed to read manifest '{manifestPath}': {ex.Message}");
                return false;
            }
        }

        private static ModCandidate BuildCandidate(
            ModManifest manifest,
            string manifestPath,
            string modDirectory,
            string assemblyPath,
            bool hasManagedEntry)
        {
            return new ModCandidate
            {
                Metadata = new ModMetadata
                {
                    Id = manifest.Id,
                    Name = string.IsNullOrWhiteSpace(manifest.Name) ? manifest.Id : manifest.Name,
                    Version = string.IsNullOrWhiteSpace(manifest.Version) ? "0.0.0" : manifest.Version,
                    Author = manifest.Author ?? string.Empty,
                    Dependencies = manifest.Dependencies ?? Array.Empty<string>()
                },
                EntryAssemblyPath = assemblyPath,
                EntryTypeName = manifest.EntryType ?? string.Empty,
                ManifestPath = manifestPath,
                ModDirectory = modDirectory,
                BundlePath = ResolveBundlePath(modDirectory, manifest.Id),
                LocalizationFiles = ResolveLocalizationFiles(modDirectory),
                HasManagedEntry = hasManagedEntry
            };
        }

        private static string ResolveAssemblyPath(string modDirectory, ModManifest manifest)
        {
            if (string.IsNullOrWhiteSpace(modDirectory))
                return null;

            if (!string.IsNullOrWhiteSpace(manifest.EntryAssembly))
            {
                string explicitPath = Path.Combine(modDirectory, manifest.EntryAssembly);
                return File.Exists(explicitPath) ? explicitPath : null;
            }

            string conventionalPath = Path.Combine(modDirectory, manifest.Id + DefaultAssemblyExtension);
            if (File.Exists(conventionalPath))
                return conventionalPath;

            string[] dllFiles = Directory.GetFiles(modDirectory, "*" + DefaultAssemblyExtension, SearchOption.TopDirectoryOnly);
            return dllFiles != null && dllFiles.Length == 1 ? dllFiles[0] : null;
        }

        private static string ResolveBundlePath(string modDirectory, string modId)
        {
            if (string.IsNullOrWhiteSpace(modDirectory))
                return null;

            string conventionalPath = Path.Combine(modDirectory, modId + DefaultBundleExtension);
            if (File.Exists(conventionalPath))
                return conventionalPath;

            string[] bundleFiles = Directory.GetFiles(modDirectory, "*" + DefaultBundleExtension, SearchOption.TopDirectoryOnly);
            return bundleFiles != null && bundleFiles.Length == 1 ? bundleFiles[0] : null;
        }

        private static string[] ResolveLocalizationFiles(string modDirectory)
        {
            if (string.IsNullOrWhiteSpace(modDirectory) || !Directory.Exists(modDirectory))
                return Array.Empty<string>();

            return Directory.GetFiles(modDirectory, "lang_*.json", SearchOption.TopDirectoryOnly);
        }

        private static void BuildLoadOrder(List<ModCandidate> candidates, List<ModCandidate> loadOrder)
        {
            // COLD ALLOC: Dictionary<string,ModCandidate>[candidate count] — dependency lookup by modId — owner: ModLoader
            Dictionary<string, ModCandidate> byId = new Dictionary<string, ModCandidate>(candidates.Count);
            // COLD ALLOC: HashSet<string>[candidate count] — sorted IDs for dependency resolution — owner: ModLoader
            HashSet<string> sortedIds = new HashSet<string>();

            for (int i = 0; i < candidates.Count; i++)
            {
                ModCandidate candidate = candidates[i];
                if (byId.TryGetValue(candidate.Metadata.Id, out ModCandidate existing))
                {
                    candidate.IsDisabled = true;
                    candidate.DisabledReason = $"Duplicate mod ID. Keeping '{existing.ManifestPath}'.";
                    continue;
                }

                byId.Add(candidate.Metadata.Id, candidate);
            }

            int unresolvedCount = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (!candidates[i].IsDisabled)
                    unresolvedCount++;
            }

            while (unresolvedCount > 0)
            {
                bool progressed = false;

                for (int i = 0; i < candidates.Count; i++)
                {
                    ModCandidate candidate = candidates[i];
                    if (candidate.IsDisabled || candidate.IsProcessed)
                        continue;

                    if (TryDisableForMissingDependency(candidate, byId))
                    {
                        candidate.IsProcessed = true;
                        unresolvedCount--;
                        progressed = true;
                        continue;
                    }

                    if (!AreDependenciesSatisfied(candidate, sortedIds))
                        continue;

                    loadOrder.Add(candidate);
                    sortedIds.Add(candidate.Metadata.Id);
                    candidate.IsProcessed = true;
                    unresolvedCount--;
                    progressed = true;
                }

                if (progressed)
                    continue;

                for (int i = 0; i < candidates.Count; i++)
                {
                    ModCandidate candidate = candidates[i];
                    if (candidate.IsDisabled || candidate.IsProcessed)
                        continue;

                    candidate.IsDisabled = true;
                    candidate.IsProcessed = true;
                    candidate.DisabledReason = "Dependency cycle or unresolved ordering deadlock.";
                    unresolvedCount--;
                    Debug.LogWarning($"[ModLoader] Disabled mod '{candidate.Metadata.Id}': dependency cycle or unresolved ordering deadlock.");
                }
            }
        }

        private static bool TryDisableForMissingDependency(ModCandidate candidate, Dictionary<string, ModCandidate> byId)
        {
            string[] dependencies = candidate.Metadata.Dependencies;
            if (dependencies == null || dependencies.Length == 0)
                return false;

            for (int i = 0; i < dependencies.Length; i++)
            {
                string dependencyId = dependencies[i];
                if (string.IsNullOrWhiteSpace(dependencyId))
                    continue;

                if (byId.TryGetValue(dependencyId, out ModCandidate dependencyCandidate) && !dependencyCandidate.IsDisabled)
                    continue;

                candidate.IsDisabled = true;
                candidate.DisabledReason = $"Missing dependency '{dependencyId}'.";
                Debug.LogWarning($"[ModLoader] Disabled mod '{candidate.Metadata.Id}': missing dependency '{dependencyId}'.");
                return true;
            }

            return false;
        }

        private static bool AreDependenciesSatisfied(ModCandidate candidate, HashSet<string> sortedIds)
        {
            string[] dependencies = candidate.Metadata.Dependencies;
            if (dependencies == null || dependencies.Length == 0)
                return true;

            for (int i = 0; i < dependencies.Length; i++)
            {
                string dependencyId = dependencies[i];
                if (string.IsNullOrWhiteSpace(dependencyId))
                    continue;

                if (!sortedIds.Contains(dependencyId))
                    return false;
            }

            return true;
        }

        private static void TryLoadCandidate(ModCandidate candidate)
        {
            if (candidate.IsDisabled)
                return;

            ModAssetManager.RegisterBundlePath(candidate.Metadata.Id, candidate.BundlePath);
            ModLocalizationBridge.RegisterLocalizationFiles(candidate.Metadata.Id, candidate.LocalizationFiles);

            if (!candidate.HasManagedEntry)
            {
                RecordRuntimeInfo(new ModRuntimeInfo
                {
                    Metadata = candidate.Metadata,
                    Status = ModLoadStatus.Active,
                    DirectoryPath = candidate.ModDirectory,
                    StatusMessage = "Content-only mod loaded.",
                    AssetBundlePath = candidate.BundlePath,
                    HasManagedEntry = false,
                    HasLocalizationFiles = candidate.LocalizationFiles != null && candidate.LocalizationFiles.Length > 0
                });
                return;
            }

            try
            {
                if (!TryCreateRegisteredManagedMod(candidate.Metadata.Id, out IHectonMod modInstance, out string failureReason))
                {
                    DisableCandidate(candidate, failureReason);
                    return;
                }

                LoadedMod loadedMod = new LoadedMod
                {
                    Metadata = candidate.Metadata,
                    Instance = modInstance
                };

                ExecuteModCallback(loadedMod.Metadata.Id, loadedMod.Instance.OnLoad, "OnLoad");
                _loadedMods.Add(loadedMod);

                RecordRuntimeInfo(new ModRuntimeInfo
                {
                    Metadata = candidate.Metadata,
                    Status = ModLoadStatus.Active,
                    DirectoryPath = candidate.ModDirectory,
                    StatusMessage = string.Empty,
                    AssetBundlePath = candidate.BundlePath,
                    HasManagedEntry = true,
                    HasLocalizationFiles = candidate.LocalizationFiles != null && candidate.LocalizationFiles.Length > 0
                });
            }
            catch (Exception ex)
            {
                DisableCandidate(candidate, $"Load failure '{ex.Message}'.");
            }
        }

        private static void DisableCandidate(ModCandidate candidate, string reason)
        {
            candidate.IsDisabled = true;
            candidate.DisabledReason = reason;
            Debug.LogWarning($"[ModLoader] Disabled mod '{candidate.Metadata.Id}': {reason}");

            RecordRuntimeInfo(new ModRuntimeInfo
            {
                Metadata = candidate.Metadata,
                Status = ModLoadStatus.Disabled,
                DirectoryPath = candidate.ModDirectory,
                StatusMessage = reason,
                AssetBundlePath = candidate.BundlePath,
                HasManagedEntry = candidate.HasManagedEntry,
                HasLocalizationFiles = candidate.LocalizationFiles != null && candidate.LocalizationFiles.Length > 0
            });
        }

        private static bool TryCreateRegisteredManagedMod(
            string modId,
            out IHectonMod modInstance,
            out string failureReason)
        {
            modInstance = null;
            failureReason = null;
            if (!_managedModFactories.TryGetValue(modId, out Func<IHectonMod> factory) || factory == null)
            {
                failureReason =
                    "Managed code entry requires explicit boot registration. Runtime assembly reflection loading is disabled for IL2CPP compliance.";
                return false;
            }

            try
            {
                modInstance = factory();
                if (modInstance == null)
                {
                    failureReason = "Managed mod factory returned null.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                failureReason = $"Managed mod factory threw '{ex.Message}'.";
                return false;
            }
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ModWorldPersistenceManager.EnsureRuntimeInstance();
            ModLocalizationBridge.FlushPendingInjections();
        }

        private static void HandleLoadCompleted(string slotName)
        {
            HectonEventBus.Publish(new GameLoadedEvent(slotName));
        }

        private static void HandleCraftCompleted(ItemData itemData)
        {
            if (itemData == null)
                return;

            HectonEventBus.Publish(new ItemCraftedEvent(itemData));
        }

        private static void HandleGameReady()
        {
            ModItemRegistry.FlushPendingRegistrations();
            ModBuildableRegistry.FlushPendingRegistrations();
            ModLocalizationBridge.FlushPendingInjections();
            ModWorldPersistenceManager.EnsureRuntimeInstance();

            if (!_modsInitialized)
            {
                for (int i = 0; i < _loadedMods.Count; i++)
                    ExecuteModCallback(_loadedMods[i].Metadata.Id, _loadedMods[i].Instance.OnInitialize, "OnInitialize");

                _modsInitialized = true;
            }

            ModItemRegistry.FlushPendingRegistrations();
            ModBuildableRegistry.FlushPendingRegistrations();
            ModRecipeRegistry.FlushPendingRegistrations();

            GameObject playerObject = SceneBootstrap.CurrentPlayerObject;
            if (playerObject != null)
                HectonEventBus.Publish(new PlayerSpawnedEvent(playerObject));
        }

        private static void HandleApplicationQuitting()
        {
            ShutdownLoadedMods();
        }

        private static void ShutdownLoadedMods()
        {
            if (_shutdownInvoked)
                return;

            _shutdownInvoked = true;

            for (int i = _loadedMods.Count - 1; i >= 0; i--)
                ExecuteModCallback(_loadedMods[i].Metadata.Id, _loadedMods[i].Instance.OnUnload, "OnUnload");
        }

        private static void ExecuteModCallback(string modId, Action callback, string callbackName)
        {
            if (callback == null)
                return;

            try
            {
                using (ModExecutionScope.Enter(modId))
                {
                    callback();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ModLoader] Mod '{modId}' failed during {callbackName}: {ex}");
            }
        }

        private static void RecordRuntimeInfo(ModRuntimeInfo info)
        {
            if (_runtimeInfoIndexById.TryGetValue(info.Metadata.Id, out int index))
            {
                _runtimeInfos[index] = info;
                RuntimeRegistryChanged?.Invoke();
                return;
            }

            _runtimeInfoIndexById.Add(info.Metadata.Id, _runtimeInfos.Count);
            _runtimeInfos.Add(info);
            RuntimeRegistryChanged?.Invoke();
        }

        private static string ResolveModsRoot()
        {
            string dataPath = Application.dataPath;
            if (string.IsNullOrWhiteSpace(dataPath))
                return null;

            string projectRoot = Path.GetDirectoryName(dataPath);
            return string.IsNullOrWhiteSpace(projectRoot)
                ? null
                : Path.Combine(projectRoot, "Mods");
        }

        [Serializable]
        private struct ModManifest
        {
            public string Id;
            public string Name;
            public string Version;
            public string Author;
            public string[] Dependencies;
            public string EntryAssembly;
            public string EntryType;

            public ModManifest(
                string id,
                string name,
                string version,
                string author,
                string[] dependencies,
                string entryAssembly,
                string entryType)
            {
                Id = id;
                Name = name;
                Version = version;
                Author = author;
                Dependencies = dependencies;
                EntryAssembly = entryAssembly;
                EntryType = entryType;
            }
        }

        private sealed class ModCandidate
        {
            public ModMetadata Metadata;
            public string EntryAssemblyPath;
            public string EntryTypeName;
            public string ManifestPath;
            public string ModDirectory;
            public string BundlePath;
            public string[] LocalizationFiles;
            public bool HasManagedEntry;
            public bool IsDisabled;
            public bool IsProcessed;
            public string DisabledReason;
        }

        private sealed class LoadedMod
        {
            public ModMetadata Metadata;
            public IHectonMod Instance;
        }
    }
}
