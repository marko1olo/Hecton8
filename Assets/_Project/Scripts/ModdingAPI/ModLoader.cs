using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Core.Bridge;
using Hecton8.SaveSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Modding
{
    internal static class ModLoader
    {
        private sealed class BootstrapEventListener : IGameBootstrapperEventListener
        {
            public void OnGameBootstrapperEvent(in GameBootstrapperEventPayload payload)
            {
                if ((GameBootstrapperEventType)payload.EventType == GameBootstrapperEventType.GameReady)
                    HandleGameReady();
            }
        }

        private sealed class SaveEventListener : ISaveEventListener
        {
            public void OnSaveEvent(in SaveEventPayload payload)
            {
                if (payload.Type == SaveEventType.LoadCompleted)
                    HandleLoadCompleted(SaveEvents.ResolveSlotName(payload.SlotHash));
            }
        }

        private const string ManifestFileName = "mod.json";
        private const long MaxManifestBytes = 32L * 1024L;
        private const string MaxManifestBytesLabel = "32768";
        private const int MaxDiscoveredManifestCount = 64;
        private const string MaxDiscoveredManifestCountLabel = "64";
        private const int MaxTopLevelManagedAssemblyCount = 32;
        private const string MaxTopLevelManagedAssemblyCountLabel = "32";

        private const int MaxLocalizationFileCount = 16;
        private const string MaxLocalizationFileCountLabel = "16";
        private const string DefaultAssemblyExtension = ".dll";

        private const string ReservedAssemblyNamePrefix = "Hecton8.";
        private const string ReservedUnityAssemblyNamePrefix = "Unity";
        private const string ReservedAssemblyNameAssemblyCSharp = "Assembly-CSharp";
        private const string ReservedAssemblyNameSystem = "System";
        private const string ReservedAssemblyNameMscorlib = "mscorlib";
        private const string ReservedAssemblyNameNetstandard = "netstandard";
        internal const int CurrentAPIVersion = 2;
        // COLD ALLOC: List<ModRuntimeInfo>[32] — discovered runtime info descriptors for UI and diagnostics — owner: ModLoader
        private static readonly List<ModRuntimeInfo> _runtimeInfos = new List<ModRuntimeInfo>(32);
        // COLD ALLOC: Dictionary<string,int>[32] — modId to runtime info index lookup — owner: ModLoader
        private static readonly Dictionary<uint, int> _runtimeInfoIndexByHash = new Dictionary<uint, int>(32);
        // COLD ALLOC: SaveEventListener[1] — static save-event bridge for mod runtime hooks — owner: ModLoader
        private static readonly SaveEventListener _saveEventListener = new SaveEventListener();
        private static readonly BootstrapEventListener _bootstrapEventListener = new BootstrapEventListener();

        private static bool _bootstrapped;
        private static bool _modsInitialized;
        private static bool _hooksInstalled;
        private static bool _shutdownInvoked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStaticState()
        {
            ShutdownRuntimeForLifecycleReset();
            _runtimeInfos.Clear();
            _runtimeInfoIndexByHash.Clear();
            _bootstrapped = false;
            _modsInitialized = false;
            _hooksInstalled = false;
            _shutdownInvoked = false;
            HectonAPI.ResetRegistryCacheCold();
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void InstallEditorPlayModeShutdownHook()
        {
            EditorApplication.playModeStateChanged -= HandleEditorPlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandleEditorPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeEditorAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeEditorAssemblyReload;
        }

        private static void HandleEditorPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode ||
                state == PlayModeStateChange.EnteredEditMode)
            {
                ShutdownRuntimeForLifecycleReset();
            }
        }

        private static void HandleBeforeEditorAssemblyReload()
        {
            ShutdownRuntimeForLifecycleReset();
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_bootstrapped)
                return;

            _bootstrapped = true;
            _ = LoadMods();
        }

        private static async Awaitable LoadMods()
        {
            try
            {
                InstallHooks();
                await Awaitable.NextFrameAsync();

                await DiscoverAndLoadMods();

                await Awaitable.NextFrameAsync();
                ModLocalizationBridge.FlushPendingInjections();
            }
            catch (Exception ex)
            {
                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] LoadMods failed: ", ex.Message));
            }
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
            uint modHash = ModCommandDispatcher.ComputeModHash(modId);
            if (modHash == 0u || !_runtimeInfoIndexByHash.TryGetValue(modHash, out int index))
                return false;

            directoryPath = _runtimeInfos[index].DirectoryPath;
            return !string.IsNullOrWhiteSpace(directoryPath);
        }

        private static void InstallHooks()
        {
            if (_hooksInstalled)
                return;

            SaveEvents.Register(_saveEventListener);
            ModCommandDispatcher.Initialize();
            BindModRegistryServicesCold();
            if (!ShouldForceFutureCommandEnvelopeOnly())
            {
                ModEventProjectionBridge.InstallGlobal();
                ModResourceRegistry.Initialize();
            }

            GameBootstrapper.Register(_bootstrapEventListener);
            Application.quitting -= HandleApplicationQuitting;
            Application.quitting += HandleApplicationQuitting;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            _hooksInstalled = true;
        }

        private static void UninstallHooks()
        {
            if (!_hooksInstalled)
                return;

            SaveEvents.Unregister(_saveEventListener);
            if (!ShouldForceFutureCommandEnvelopeOnly())
                ModEventProjectionBridge.ShutdownGlobal();

            GameBootstrapper.Unregister(_bootstrapEventListener);
            Application.quitting -= HandleApplicationQuitting;
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            _hooksInstalled = false;
        }


        private static async Awaitable DiscoverAndLoadMods()

        {
#if ENABLE_IL2CPP
            Hecton8.Core.H8Debug.LogWarning("[ModLoader] WARNING: External managed code mods require a Mono scripting backend. IL2CPP builds cannot load runtime assemblies dynamically.");
            return;
#else
            string modsRoot = ResolveModsRoot();
            if (string.IsNullOrEmpty(modsRoot) || !Directory.Exists(modsRoot))
                return;

            // COLD ALLOC: List<string>[MaxDiscoveredManifestCount] - bounded manifest discovery - owner: ModLoader
            List<string> manifestPaths = new List<string>(MaxDiscoveredManifestCount);
            CollectManifestPaths(modsRoot, manifestPaths);
            if (manifestPaths.Count == 0)
                return;

            // COLD ALLOC: List<ModCandidate>[manifest count] — discovered manifests before dependency sort — owner: ModLoader
            List<ModCandidate> candidates = new List<ModCandidate>(manifestPaths.Count);

            // COLD ALLOC: List<Awaitable<ModCandidate>>[manifest count] - async reads - owner: ModLoader
            List<Awaitable<ModCandidate>> tasks = new List<Awaitable<ModCandidate>>(manifestPaths.Count);
            for (int i = 0; i < manifestPaths.Count; i++)
            {

                tasks.Add(TryReadManifestAsync(manifestPaths[i]));
            }

            for (int i = 0; i < tasks.Count; i++)
            {
                ModCandidate candidate = await tasks[i];

                if (candidate != null)
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
                    CatalogPath = candidate.CatalogPath,
                    HasManagedEntry = candidate.HasManagedEntry,
                    HasLocalizationFiles = candidate.LocalizationFiles != null && candidate.LocalizationFiles.Length > 0
                });
            }
#endif
        }

        private static void CollectManifestPaths(string modsRoot, List<string> manifestPaths)
        {
            if (manifestPaths == null || string.IsNullOrWhiteSpace(modsRoot))
                return;

            try
            {
                foreach (string manifestPath in Directory.EnumerateFiles(modsRoot, ManifestFileName, SearchOption.AllDirectories))
                {
                    if (manifestPaths.Count >= MaxDiscoveredManifestCount)
                    {
                        Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Manifest discovery capped at ", MaxDiscoveredManifestCountLabel, " packages under '", modsRoot, "'."));
                        break;
                    }

                    manifestPaths.Add(manifestPath);
                }
            }
            catch (UnauthorizedAccessException exception)
            {
                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Manifest discovery skipped inaccessible path under '", modsRoot, "': ", exception.Message));
            }
            catch (IOException exception)
            {
                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Manifest discovery failed under '", modsRoot, "': ", exception.Message));
            }
            catch (Exception exception)
            {
                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Manifest discovery aborted under '", modsRoot, "': ", exception.Message));
            }
        }

        private static async Awaitable<ModCandidate> TryReadManifestAsync(string manifestPath)
        {


            try
            {
                if (!TryValidateManifestFileSize(manifestPath))
                    return null;

                string json = await File.ReadAllTextAsync(manifestPath);
                ModManifest manifest = JsonUtility.FromJson<ModManifest>(json);

                if (!TryValidateModIdentifier(manifest.Id, out string modIdError))
                {
                    Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Skipped manifest '", manifestPath, "': invalid Id. ", modIdError));
                    return null;
                }

                bool hasDeclaredEntryAssembly = !string.IsNullOrWhiteSpace(manifest.EntryAssembly);
                string manifestContractError = null;
                bool entryAssemblyFileNameValid = TryValidateEntryAssemblyFileName(
                    manifest.EntryAssembly,
                    out string entryAssemblyError);
                if (!entryAssemblyFileNameValid)
                    manifest.EntryAssembly = string.Empty;

                if (!TryValidateManifestDependencies(manifest.Dependencies, out string dependencyError))
                {
                    manifestContractError = dependencyError;
                }
                else if (!entryAssemblyFileNameValid)
                {
                    manifestContractError = entryAssemblyError;
                }

                string modDirectory = Path.GetDirectoryName(manifestPath);
                bool envelopeOnly = ShouldForceFutureCommandEnvelopeOnly();
                string[] managedAssemblyIdentityScanPaths = ResolveManagedAssemblyIdentityScanPaths(
                    modDirectory,
                    manifest,
                    out string managedAssemblyDiscoveryError);
                if (string.IsNullOrWhiteSpace(manifestContractError) &&
                    !string.IsNullOrWhiteSpace(managedAssemblyDiscoveryError))
                {
                    manifestContractError = managedAssemblyDiscoveryError;
                }

                string assemblyPath = envelopeOnly
                    ? string.Empty
                    : ResolveAssemblyPath(modDirectory, manifest);
                bool hasManagedEntry =
                    hasDeclaredEntryAssembly ||
                    !string.IsNullOrWhiteSpace(manifest.EntryType) ||
                    managedAssemblyIdentityScanPaths.Length > 0 ||
                    (!envelopeOnly && !string.IsNullOrWhiteSpace(assemblyPath));

                string catalogPath = envelopeOnly
                    ? string.Empty
                    : ResolveCatalogPath(modDirectory, manifest.Id);
                string[] localizationFiles = ResolveLocalizationFiles(modDirectory);

                return BuildCandidate(
                    manifest,
                    manifestPath,
                    modDirectory,
                    assemblyPath,
                    managedAssemblyIdentityScanPaths,
                    manifestContractError,
                    hasManagedEntry,
                    catalogPath,
                    localizationFiles);


            }
            catch (Exception ex)
            {
                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Failed to read manifest '", manifestPath, "': ", ex.Message));
                return null;
            }
        }

        private static ModCandidate BuildCandidate(
            ModManifest manifest,
            string manifestPath,
            string modDirectory,
            string assemblyPath,
            string[] managedAssemblyIdentityScanPaths,
            string manifestContractError,
            bool hasManagedEntry,
            string catalogPath,
            string[] localizationFiles)
        {
            ModCandidate candidate = new ModCandidate
            {
                Metadata = new ModMetadata
                {
                    Id = manifest.Id,
                    Name = string.IsNullOrWhiteSpace(manifest.Name) ? manifest.Id : manifest.Name,
                    Version = string.IsNullOrWhiteSpace(manifest.Version) ? "0.0.0" : manifest.Version,
                    Author = manifest.Author ?? string.Empty,
                    Dependencies = manifest.Dependencies ?? Array.Empty<string>(),
                    RequiredAPIVersion = manifest.RequiredAPIVersion,
                    StableIdHash = ModCommandDispatcher.ComputeModHash(manifest.Id),
                    ModPriority = manifest.ModPriority
                },
                EntryAssemblyPath = assemblyPath,
                EntryTypeName = manifest.EntryType ?? string.Empty,
                ManifestPath = manifestPath,
                ModDirectory = modDirectory,
                CatalogPath = catalogPath ?? string.Empty,
                LocalizationFiles = localizationFiles ?? Array.Empty<string>(),
                HasManagedEntry = hasManagedEntry
            };

            if (!string.IsNullOrWhiteSpace(manifestContractError))
            {
                candidate.IsDisabled = true;
                candidate.DisabledReason = manifestContractError;
            }
            else if (manifest.RequiredAPIVersion <= 0)
            {
                candidate.IsDisabled = true;
                candidate.DisabledReason = "Missing RequiredAPIVersion.";
            }
            else if (manifest.RequiredAPIVersion > CurrentAPIVersion)
            {
                candidate.IsDisabled = true;
                candidate.DisabledReason = "RequiredAPIVersion exceeds engine API version.";
            }
            else if (!TryValidateManagedAssemblyIdentity(
                         manifest.EntryAssembly,
                         assemblyPath,
                         managedAssemblyIdentityScanPaths,
                         out string assemblyIdentityError))
            {
                candidate.IsDisabled = true;
                candidate.DisabledReason = assemblyIdentityError;
            }

            return candidate;
        }

        private static bool TryValidateManifestFileSize(string manifestPath)
        {
            if (string.IsNullOrWhiteSpace(manifestPath))
                return false;

            try
            {
                // COLD ALLOC: FileInfo[1] - mod manifest byte cap gate - owner: ModLoader
                FileInfo fileInfo = new FileInfo(manifestPath);
                if (!fileInfo.Exists || fileInfo.Length <= 0L)
                {
                    Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Skipped manifest '", manifestPath, "': manifest file is missing or empty."));
                    return false;
                }

                if (fileInfo.Length > MaxManifestBytes)
                {
                    Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Skipped manifest '", manifestPath, "': manifest exceeds ", MaxManifestBytesLabel, " byte cap."));
                    return false;
                }
            }
            catch (IOException exception)
            {
                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Failed to inspect manifest '", manifestPath, "': ", exception.Message));
                return false;
            }
            catch (Exception exception)
            {
                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Rejected invalid manifest path '", manifestPath, "': ", exception.Message));
                return false;
            }

            return true;
        }

        private static bool TryValidateModIdentifier(string modId, out string disabledReason)
        {
            disabledReason = string.Empty;

            if (string.IsNullOrWhiteSpace(modId))
            {
                disabledReason = "Mod ID is required.";
                return false;
            }

            string trimmed = modId.Trim();
            if (!string.Equals(modId, trimmed, StringComparison.Ordinal))
            {
                disabledReason = "Mod ID must not contain leading or trailing whitespace.";
                return false;
            }

            bool previousWasSeparator = false;
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                bool isLowerLetter = c >= 'a' && c <= 'z';
                bool isDigit = c >= '0' && c <= '9';
                bool isSeparator = c == '.' || c == '_' || c == '-';
                if (!isLowerLetter && !isDigit && !isSeparator)
                {
                    disabledReason = "Mod ID may contain only lowercase latin letters, digits, '.', '_' and '-'.";
                    return false;
                }

                if (isSeparator)
                {
                    if (i == 0 || i == trimmed.Length - 1 || previousWasSeparator)
                    {
                        disabledReason = "Mod ID separators must be between lowercase letters or digits and cannot repeat.";
                        return false;
                    }
                }

                previousWasSeparator = isSeparator;
            }

            if (ContainsReservedModIdentifierSegment(trimmed))
            {
                disabledReason = "Mod ID contains a reserved filesystem device segment.";
                return false;
            }

            return true;
        }

        private static bool TryValidateManifestDependencies(string[] dependencies, out string disabledReason)
        {
            disabledReason = string.Empty;
            if (dependencies == null || dependencies.Length == 0)
                return true;

            for (int i = 0; i < dependencies.Length; i++)
            {
                string dependencyId = dependencies[i];
                if (string.IsNullOrWhiteSpace(dependencyId))
                    continue;

                if (!TryValidateModIdentifier(dependencyId, out string dependencyError))
                {
                    disabledReason = string.Concat("Invalid dependency ID '", dependencyId, "': ", dependencyError);
                    return false;
                }
            }

            return true;
        }

        private static bool TryValidateEntryAssemblyFileName(string entryAssembly, out string disabledReason)
        {
            disabledReason = string.Empty;
            if (string.IsNullOrWhiteSpace(entryAssembly))
                return true;

            string trimmed = entryAssembly.Trim();
            if (!string.Equals(entryAssembly, trimmed, StringComparison.Ordinal))
            {
                disabledReason = "EntryAssembly must not contain leading or trailing whitespace.";
                return false;
            }

            if (Path.IsPathRooted(trimmed) ||
                trimmed.IndexOf(Path.DirectorySeparatorChar) >= 0 ||
                trimmed.IndexOf(Path.AltDirectorySeparatorChar) >= 0 ||
                !string.Equals(Path.GetFileName(trimmed), trimmed, StringComparison.Ordinal))
            {
                disabledReason = "EntryAssembly must be a package-local DLL file name, not a path.";
                return false;
            }

            if (!string.Equals(Path.GetExtension(trimmed), DefaultAssemblyExtension, StringComparison.OrdinalIgnoreCase))
            {
                disabledReason = "EntryAssembly must reference a .dll file.";
                return false;
            }

            return true;
        }

        private static bool ContainsReservedModIdentifierSegment(string modId)
        {
            int segmentStart = 0;
            for (int i = 0; i <= modId.Length; i++)
            {
                if (i < modId.Length && modId[i] != '.' && modId[i] != '_' && modId[i] != '-')
                    continue;

                string segment = modId.Substring(segmentStart, i - segmentStart);
                if (IsReservedFilesystemDeviceName(segment))
                    return true;

                segmentStart = i + 1;
            }

            return false;
        }

        private static bool IsReservedFilesystemDeviceName(string segment)
        {
            if (string.IsNullOrEmpty(segment))
                return false;

            return string.Equals(segment, "con", StringComparison.Ordinal) ||
                   string.Equals(segment, "prn", StringComparison.Ordinal) ||
                   string.Equals(segment, "aux", StringComparison.Ordinal) ||
                   string.Equals(segment, "nul", StringComparison.Ordinal) ||
                   IsReservedDeviceRange(segment, "com") ||
                   IsReservedDeviceRange(segment, "lpt");
        }

        private static bool IsReservedDeviceRange(string segment, string prefix)
        {
            return segment.Length == 4 &&
                   segment.StartsWith(prefix, StringComparison.Ordinal) &&
                   segment[3] >= '1' &&
                   segment[3] <= '9';
        }

        private static string[] ResolveManagedAssemblyIdentityScanPaths(
            string modDirectory,
            ModManifest manifest,
            out string disabledReason)
        {
            disabledReason = string.Empty;
            if (string.IsNullOrWhiteSpace(modDirectory) || !Directory.Exists(modDirectory))
                return Array.Empty<string>();

            string[] dllFiles = CollectTopLevelFiles(
                modDirectory,
                "*" + DefaultAssemblyExtension,
                MaxTopLevelManagedAssemblyCount,
                MaxTopLevelManagedAssemblyCountLabel,
                "managed assembly",
                out bool managedAssemblyCapExceeded,
                out bool managedAssemblyDiscoveryFailed);
            if (managedAssemblyCapExceeded)
                disabledReason = string.Concat("Package contains more than ", MaxTopLevelManagedAssemblyCountLabel, " top-level managed assemblies.");
            else if (managedAssemblyDiscoveryFailed)
                disabledReason = "Package top-level managed assembly discovery failed.";

            if (string.IsNullOrWhiteSpace(manifest.EntryAssembly))
                return dllFiles;

            string explicitPath = Path.Combine(modDirectory, manifest.EntryAssembly);
            if (!File.Exists(explicitPath))
                return dllFiles;

            for (int i = 0; i < dllFiles.Length; i++)
            {
                if (string.Equals(dllFiles[i], explicitPath, StringComparison.OrdinalIgnoreCase))
                    return dllFiles;
            }

            // COLD ALLOC: List<string>[dll count + explicit entry] - package identity scan - owner: ModLoader
            List<string> scanPaths = new List<string>(dllFiles.Length + 1) { explicitPath };
            for (int i = 0; i < dllFiles.Length; i++)
                scanPaths.Add(dllFiles[i]);
            return scanPaths.ToArray();
        }

        private static bool TryValidateManagedAssemblyIdentity(
            string entryAssembly,
            string assemblyPath,
            string[] identityScanPaths,
            out string disabledReason)
        {
            disabledReason = string.Empty;

            if (!string.IsNullOrWhiteSpace(entryAssembly) &&
                IsReservedManagedAssemblyName(Path.GetFileNameWithoutExtension(entryAssembly)))
            {
                disabledReason = "Managed assembly name is reserved for engine-owned assemblies.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(assemblyPath))
            {
                if (identityScanPaths == null || identityScanPaths.Length == 0)
                    return true;
            }
            else if (!TryValidateManagedAssemblyIdentityPath(assemblyPath, out disabledReason))
            {
                return false;
            }

            if (identityScanPaths == null)
                return true;

            for (int i = 0; i < identityScanPaths.Length; i++)
            {
                string scanPath = identityScanPaths[i];
                if (string.IsNullOrWhiteSpace(scanPath) ||
                    string.Equals(scanPath, assemblyPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!TryValidateManagedAssemblyIdentityPath(scanPath, out disabledReason))
                    return false;
            }

            return true;
        }

        private static bool TryValidateManagedAssemblyIdentityPath(string assemblyPath, out string disabledReason)
        {
            disabledReason = string.Empty;

            string fileAssemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
            if (IsReservedManagedAssemblyName(fileAssemblyName))
            {
                disabledReason = "Managed assembly file name is reserved for engine-owned assemblies.";
                return false;
            }

            try
            {
                AssemblyName assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
                if (assemblyName != null && IsReservedManagedAssemblyName(assemblyName.Name))
                {
                    disabledReason = "Managed assembly identity is reserved for engine-owned assemblies.";
                    return false;
                }
            }
            catch (Exception ex) when (
                ex is BadImageFormatException ||
                ex is FileLoadException ||
                ex is FileNotFoundException ||
                ex is IOException ||
                ex is UnauthorizedAccessException)
            {
                disabledReason = "Managed assembly identity could not be read.";
                return false;
            }

            return true;
        }

        private static bool IsReservedManagedAssemblyName(string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
                return false;

            return assemblyName.StartsWith(ReservedAssemblyNamePrefix, StringComparison.Ordinal) ||
                   assemblyName.StartsWith(ReservedUnityAssemblyNamePrefix, StringComparison.Ordinal) ||
                   string.Equals(assemblyName, ReservedAssemblyNameAssemblyCSharp, StringComparison.Ordinal) ||
                   string.Equals(assemblyName, ReservedAssemblyNameSystem, StringComparison.Ordinal) ||
                   string.Equals(assemblyName, ReservedAssemblyNameMscorlib, StringComparison.Ordinal) ||
                   string.Equals(assemblyName, ReservedAssemblyNameNetstandard, StringComparison.Ordinal);
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

            string[] dllFiles = CollectTopLevelFiles(
                modDirectory,
                "*" + DefaultAssemblyExtension,
                MaxTopLevelManagedAssemblyCount,
                MaxTopLevelManagedAssemblyCountLabel,
                "managed assembly",
                out _,
                out _);
            return dllFiles != null && dllFiles.Length == 1 ? dllFiles[0] : null;
        }

        private static string ResolveCatalogPath(string modDirectory, string modId)
        {
            if (string.IsNullOrWhiteSpace(modDirectory))
                return null;

            // Addressables content catalogs are typically named 'catalog.json'
            string conventionalPath = Path.Combine(modDirectory, "catalog.json");
            if (File.Exists(conventionalPath))
                return conventionalPath;

            return null;
        }

        private static string[] ResolveLocalizationFiles(string modDirectory)
        {
            if (string.IsNullOrWhiteSpace(modDirectory) || !Directory.Exists(modDirectory))
                return Array.Empty<string>();

            return CollectTopLevelFiles(
                modDirectory,
                "lang_*.json",
                MaxLocalizationFileCount,
                MaxLocalizationFileCountLabel,
                "localization file",
                out _,
                out _);
        }

        private static string[] CollectTopLevelFiles(
            string directory,
            string searchPattern,
            int maxCount,
            string maxCountLabel,
            string fileKind,
            out bool capExceeded,
            out bool discoveryFailed)
        {
            capExceeded = false;
            discoveryFailed = false;
            if (string.IsNullOrWhiteSpace(directory) ||
                string.IsNullOrWhiteSpace(searchPattern) ||
                maxCount <= 0 ||
                !Directory.Exists(directory))
            {
                return Array.Empty<string>();
            }

            // COLD ALLOC: List<string>[maxCount] - bounded top-level package file discovery - owner: ModLoader
            List<string> files = new List<string>(maxCount);
            try
            {
                foreach (string filePath in Directory.EnumerateFiles(directory, searchPattern, SearchOption.TopDirectoryOnly))
                {
                    if (files.Count >= maxCount)
                    {
                        capExceeded = true;
                        Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Top-level ", fileKind, " discovery capped at ", maxCountLabel, " files under '", directory, "'."));
                        break;
                    }

                    files.Add(filePath);
                }
            }
            catch (UnauthorizedAccessException exception)
            {
                discoveryFailed = true;
                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Top-level ", fileKind, " discovery skipped inaccessible path under '", directory, "': ", exception.Message));
                return Array.Empty<string>();
            }
            catch (IOException exception)
            {
                discoveryFailed = true;
                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Top-level ", fileKind, " discovery failed under '", directory, "': ", exception.Message));
                return Array.Empty<string>();
            }
            catch (Exception exception)
            {
                discoveryFailed = true;
                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Top-level ", fileKind, " discovery aborted under '", directory, "': ", exception.Message));
                return Array.Empty<string>();
            }

            if (files.Count == 0)
                return Array.Empty<string>();

            if (files.Count > 1)
                files.Sort(StringComparer.OrdinalIgnoreCase);

            return files.ToArray();
        }

        private static void BuildLoadOrder(List<ModCandidate> candidates, List<ModCandidate> loadOrder)
        {
            // COLD ALLOC: Dictionary<string,ModCandidate>[candidate count] — dependency lookup by modId — owner: ModLoader
            Dictionary<uint, ModCandidate> byId = new Dictionary<uint, ModCandidate>(candidates.Count);
            // COLD ALLOC: HashSet<string>[candidate count] — sorted IDs for dependency resolution — owner: ModLoader
            HashSet<uint> sortedIds = new HashSet<uint>(candidates.Count);

            for (int i = 0; i < candidates.Count; i++)
            {
                ModCandidate candidate = candidates[i];
                uint candidateHash = candidate.Metadata.StableIdHash != 0u
                    ? candidate.Metadata.StableIdHash
                    : ModCommandDispatcher.ComputeModHash(candidate.Metadata.Id);
                ModCandidate existing = null;
                if (candidateHash == 0u || byId.TryGetValue(candidateHash, out existing))
                {
                    candidate.IsDisabled = true;
                    candidate.DisabledReason = existing != null
                        ? string.Concat("Duplicate mod ID. Keeping '", existing.ManifestPath, "'.")
                        : "Invalid mod ID hash.";
                    continue;
                }

                candidate.Metadata.StableIdHash = candidateHash;
                byId.Add(candidateHash, candidate);
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
                    sortedIds.Add(candidate.Metadata.StableIdHash);
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
                    Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Disabled mod '", candidate.Metadata.Id, "': dependency cycle or unresolved ordering deadlock."));
                }
            }
        }

        private static bool TryDisableForMissingDependency(ModCandidate candidate, Dictionary<uint, ModCandidate> byId)
        {
            string[] dependencies = candidate.Metadata.Dependencies;
            if (dependencies == null || dependencies.Length == 0)
                return false;

            for (int i = 0; i < dependencies.Length; i++)
            {
                string dependencyId = dependencies[i];
                if (string.IsNullOrWhiteSpace(dependencyId))
                    continue;

                uint dependencyHash = ModCommandDispatcher.ComputeModHash(dependencyId);
                if (dependencyHash != 0u && byId.TryGetValue(dependencyHash, out ModCandidate dependencyCandidate) && !dependencyCandidate.IsDisabled)
                    continue;

                candidate.IsDisabled = true;
                candidate.DisabledReason = string.Concat("Missing dependency '", dependencyId, "'.");
                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Disabled mod '", candidate.Metadata.Id, "': missing dependency '", dependencyId, "'."));
                return true;
            }

            return false;
        }

        private static bool AreDependenciesSatisfied(ModCandidate candidate, HashSet<uint> sortedIds)
        {
            string[] dependencies = candidate.Metadata.Dependencies;
            if (dependencies == null || dependencies.Length == 0)
                return true;

            for (int i = 0; i < dependencies.Length; i++)
            {
                string dependencyId = dependencies[i];
                if (string.IsNullOrWhiteSpace(dependencyId))
                    continue;

                uint dependencyHash = ModCommandDispatcher.ComputeModHash(dependencyId);
                if (dependencyHash == 0u || !sortedIds.Contains(dependencyHash))
                    return false;
            }

            return true;
        }
        private static void TryLoadCandidate(ModCandidate candidate)
        {
            if (candidate.IsDisabled)
                return;

            ModAssetManager.RegisterCatalogPath(candidate.Metadata.Id, candidate.CatalogPath);
            ModLocalizationBridge.RegisterLocalizationFiles(candidate.Metadata.Id, candidate.LocalizationFiles);

            if (!candidate.HasManagedEntry)
            {
                RecordRuntimeInfo(new ModRuntimeInfo
                {
                    Metadata = candidate.Metadata,
                    Status = ModLoadStatus.Active,
                    DirectoryPath = candidate.ModDirectory,
                    StatusMessage = "Data-driven mod loaded.",
                    CatalogPath = candidate.CatalogPath,
                    HasManagedEntry = false,
                    HasLocalizationFiles = candidate.LocalizationFiles != null && candidate.LocalizationFiles.Length > 0
                });
                return;
            }

            DisableCandidate(candidate, "Managed DLL mods are strictly banned by envelope-only runtime policy.");
        }

        private static bool ShouldForceFutureCommandEnvelopeOnly()
        {
            return true;
        }

        internal static bool GetIsFutureCommandEnvelopeOnly()
        {
            return ShouldForceFutureCommandEnvelopeOnly();
        }

        private static void DisableCandidate(ModCandidate candidate, string reason)
        {
            candidate.IsDisabled = true;
            candidate.DisabledReason = reason;
            ModAssetManager.UnregisterCatalogPath(candidate.Metadata.Id);
            ModResourceRegistry.UnregisterModResources(candidate.Metadata.Id);
            ModSettingsRegistry.UnregisterModSettings(candidate.Metadata.Id);
            ModItemRegistry.UnregisterModItems(candidate.Metadata.Id);
            ModRecipeRegistry.UnregisterModRecipes(candidate.Metadata.Id);
            ModRecycleRegistry.UnregisterModRecycleYields(candidate.Metadata.Id);
            ModEcosystemRegistry.UnregisterModBiomeMutations(candidate.Metadata.Id);
            ModBuildableRegistry.UnregisterModBuildables(candidate.Metadata.Id);
            Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Disabled mod '", candidate.Metadata.Id, "': ", reason));

            RecordRuntimeInfo(new ModRuntimeInfo
            {
                Metadata = candidate.Metadata,
                Status = ModLoadStatus.Disabled,
                DirectoryPath = candidate.ModDirectory,
                StatusMessage = reason,
                CatalogPath = candidate.CatalogPath,
                HasManagedEntry = candidate.HasManagedEntry,
                HasLocalizationFiles = candidate.LocalizationFiles != null && candidate.LocalizationFiles.Length > 0
            });
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (ShouldForceFutureCommandEnvelopeOnly())
                return;

            GlobalRegistry.ModWorldPersistence?.InitializeService();
            ModLocalizationBridge.FlushPendingInjections();
        }

        private static void HandleLoadCompleted(string slotName)
        {
            uint slotHash = string.IsNullOrWhiteSpace(slotName) ? 0u : H8BridgeHashes.ComputeFnv1A(slotName);
            SessionLifecycleSignalRoute.PublishGameLoadedHash(slotHash);

            if (ShouldForceFutureCommandEnvelopeOnly())
                return;

            HectonEventBus.Publish(new GameLoadedEvent(slotName));
        }

        private static void HandleGameReady()
        {
            GameObject playerObject = GameBootstrapper.CurrentPlayerObject;
            bool hasPlayer = playerObject != null;
            ulong playerEntityId = 0ul;
            Vector3 playerPosition = Vector3.zero;
            if (hasPlayer)
            {
                Transform playerTransform = playerObject.transform;
                playerEntityId = EntityId.ToULong(playerObject.GetEntityId());
                playerPosition = playerTransform != null ? playerTransform.position : Vector3.zero;
                SessionLifecycleSignalRoute.PublishPlayerSpawned(playerEntityId, playerPosition);
            }

            if (ShouldForceFutureCommandEnvelopeOnly())
                return;

            BindModRegistryServicesCold();
            ModEventProjectionBridge.InstallGlobal();
            ModItemRegistry.FlushPendingRegistrations();
            ModBuildableRegistry.FlushPendingRegistrations();
            ModLocalizationBridge.FlushPendingInjections();
            GlobalRegistry.ModWorldPersistence?.InitializeService();
            if (!_modsInitialized)
            {
                _modsInitialized = true;
            }

            ModItemRegistry.FlushPendingRegistrations();
            ModBuildableRegistry.FlushPendingRegistrations();
            ModRecipeRegistry.FlushPendingRegistrations();

            if (hasPlayer)
                HectonEventBus.Publish(new PlayerSpawnedEvent(playerEntityId, playerPosition));
        }

        private static void BindModRegistryServicesCold()
        {
            ModCommandDispatcher.BindRegistryServicesCold();
            HectonAPI.BindRegistryServicesCold();
            ModSettingsRegistry.BindRegistryServicesCold();
            ModItemRegistry.BindRegistryServicesCold();
            ModBuildableRegistry.BindRegistryServicesCold();
        }

        private static void HandleApplicationQuitting()
        {
            ShutdownRuntimeForLifecycleReset();
        }

        private static void ShutdownRuntimeForLifecycleReset()
        {
            // _shutdownInvoked was already being cleared at the end of this method, i.e. it was
            // written as an in-flight re-entrancy guard, but nothing ever set or read it so the
            // guard did not exist. The subsystem Shutdown() calls below can raise teardown events,
            // so a nested lifecycle reset must be a no-op rather than tearing down twice.
            if (_shutdownInvoked)
                return;

            _shutdownInvoked = true;
            try
            {
                UninstallHooks();
                ModCommandDispatcher.Shutdown();
                if (!ShouldForceFutureCommandEnvelopeOnly())
                    ModResourceRegistry.Shutdown();

                _bootstrapped = false;
                _modsInitialized = false;
            }
            finally
            {
                _shutdownInvoked = false;
            }
        }


        internal static void DisableMod(string modId, string reason)
        {
            if (string.IsNullOrWhiteSpace(modId))
                return;

            HectonEventBus.DisableSubscriber(modId);
            ModCommandDispatcher.QuarantineMod(modId);
            ModAssetManager.UnregisterCatalogPath(modId);
            ModResourceRegistry.UnregisterModResources(modId);
            ModSettingsRegistry.UnregisterModSettings(modId);
            ModItemRegistry.UnregisterModItems(modId);
            ModRecipeRegistry.UnregisterModRecipes(modId);
            ModRecycleRegistry.UnregisterModRecycleYields(modId);
            ModEcosystemRegistry.UnregisterModBiomeMutations(modId);
            ModBuildableRegistry.UnregisterModBuildables(modId);

            uint modHash = ModCommandDispatcher.ComputeModHash(modId);
            if (modHash != 0u && _runtimeInfoIndexByHash.TryGetValue(modHash, out int index))
            {
                ModRuntimeInfo info = _runtimeInfos[index];
                info.Status = ModLoadStatus.Disabled;
                info.StatusMessage = reason ?? "Disabled manually or due to fatal error.";
                _runtimeInfos[index] = info;
                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Disabled mod '", modId, "': ", info.StatusMessage));
            }
        }

        private static void RecordRuntimeInfo(ModRuntimeInfo info)
        {
            if (info.Metadata.StableIdHash == 0u)
                info.Metadata.StableIdHash = ModCommandDispatcher.ComputeModHash(info.Metadata.Id);

            if (info.Metadata.StableIdHash != 0u && _runtimeInfoIndexByHash.TryGetValue(info.Metadata.StableIdHash, out int index))
            {
                _runtimeInfos[index] = info;
                ModRegistryEvents.NotifyRuntimeRegistryChanged(info.Metadata.StableIdHash);
                return;
            }

            if (info.Metadata.StableIdHash != 0u)
                _runtimeInfoIndexByHash.Add(info.Metadata.StableIdHash, _runtimeInfos.Count);

            _runtimeInfos.Add(info);
            ModRegistryEvents.NotifyRuntimeRegistryChanged(info.Metadata.StableIdHash);
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
            public int RequiredAPIVersion;
            public int ModPriority;

            public ModManifest(
                string id,
                string name,
                string version,
                string author,
                string[] dependencies,
                string entryAssembly,
                string entryType,
                int requiredApiVersion,
                int modPriority)
            {
                Id = id;
                Name = name;
                Version = version;
                Author = author;
                Dependencies = dependencies;
                EntryAssembly = entryAssembly;
                EntryType = entryType;
                RequiredAPIVersion = requiredApiVersion;
                ModPriority = modPriority;
            }
        }

        private sealed class ModCandidate
        {
            public ModMetadata Metadata;
            public string EntryAssemblyPath;
            public string EntryTypeName;
            public string ManifestPath;
            public string ModDirectory;
            public string CatalogPath;
            public string[] LocalizationFiles;
            public bool HasManagedEntry;
            public bool IsDisabled;
            public bool IsProcessed;
            public string DisabledReason;
        }
    }
}
