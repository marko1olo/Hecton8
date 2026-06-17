using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.ModdingSDK
{
    /// <summary>
    /// Internal legacy editor window for packaging a HECTON-8 mod folder.
    /// Public UGC authoring starts from the external starter kit and remains envelope-only.
    /// </summary>
    public sealed class ModBuilderWindow : EditorWindow
    {
        private const string DefaultVersion = "1.0.0";
        private const string DefaultAuthor = "Unknown";
        private const int CurrentRequiredApiVersion = 2;
        private const int DefaultModPriority = 0;
        private const string EnvelopeOnlyRuntimeWarning =
            "Public runtime UGC is envelope-only. Managed DLL entries are legacy/internal and will be disabled by the loader.";
        private const string LegacyBuilderWarning =
            "Internal legacy package builder. Public authors should use SDK Hub -> Create External Starter Kit.";
        private const string ReservedAssemblyNamePrefix = "Hecton8.";
        private const string ReservedUnityAssemblyNamePrefix = "Unity";
        private const string ReservedAssemblyNameAssemblyCSharp = "Assembly-CSharp";
        private const string ReservedAssemblyNameSystem = "System";
        private const string ReservedAssemblyNameMscorlib = "mscorlib";
        private const string ReservedAssemblyNameNetstandard = "netstandard";
        private const int MaxManagedAssemblyInputCount = 32;
        private const int MaxBundleBuildAssetCount = 512;
        private const int MaxStaleAssemblyCleanupScanCount = 128;

        [Serializable]
        private struct ModManifestData
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
        }

        private string _modId = string.Empty;
        private string _modName = string.Empty;
        private string _modVersion = DefaultVersion;
        private string _modAuthor = DefaultAuthor;
        private int _requiredApiVersion = CurrentRequiredApiVersion;
        private int _modPriority = DefaultModPriority;
        private string _assetFolderPath = string.Empty;
        private BuildTarget _buildTarget = BuildTarget.StandaloneWindows64;
        private Vector2 _scrollPosition;

        // COLD ALLOC: List<string>[4] - managed assembly copy list for SDK builder UI - owner: ModBuilderWindow
        private readonly List<string> _dllPaths = new List<string>(4);
        // COLD ALLOC: List<string>[4] - dependency ID list for SDK builder UI - owner: ModBuilderWindow
        private readonly List<string> _dependencyIds = new List<string>(4);

        /// <summary>
        /// Opens the internal legacy HECTON mod builder window.
        /// </summary>
        [MenuItem("Hecton8/Modding/Internal/Legacy Mod Builder")]
        public static void ShowWindow()
        {
            ModBuilderWindow window = GetWindow<ModBuilderWindow>("Hecton Legacy Mod Builder");
            window.minSize = new Vector2(620f, 540f);
        }

        private void OnEnable()
        {
            _buildTarget = EditorUserBuildSettings.activeBuildTarget;

            if (_dllPaths.Count == 0)
                _dllPaths.Add(string.Empty);

            if (_dependencyIds.Count == 0)
                _dependencyIds.Add(string.Empty);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);

            using (EditorGUILayout.VerticalScope _ = new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField("HECTON-8 Legacy Mod Builder", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    LegacyBuilderWarning + " Builds an internal/legacy package into ProjectRoot/Mods/[ModId]. " +
                    "Package manifests are validated against the current loader contract. " +
                    EnvelopeOnlyRuntimeWarning,
                    MessageType.Info);

                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
                DrawIdentitySection();
                EditorGUILayout.Space(10f);
                DrawAssetSection();
                EditorGUILayout.Space(10f);
                DrawAssemblySection();
                EditorGUILayout.Space(10f);
                DrawDependencySection();
                EditorGUILayout.Space(10f);
                DrawValidationSection();
                EditorGUILayout.Space(12f);
                DrawBuildActions();
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawIdentitySection()
        {
            EditorGUILayout.LabelField("Manifest", EditorStyles.boldLabel);
            _modId = EditorGUILayout.TextField("Mod ID", _modId ?? string.Empty);
            _modName = EditorGUILayout.TextField("Name", _modName ?? string.Empty);
            _modVersion = EditorGUILayout.TextField("Version", _modVersion ?? string.Empty);
            _modAuthor = EditorGUILayout.TextField("Author", _modAuthor ?? string.Empty);
            _requiredApiVersion = EditorGUILayout.IntField("Required API Version", _requiredApiVersion);
            _modPriority = EditorGUILayout.IntField("Mod Priority", _modPriority);
            _buildTarget = (BuildTarget)EditorGUILayout.EnumPopup("Bundle Target", _buildTarget);
        }

        private void DrawAssetSection()
        {
            EditorGUILayout.LabelField("Assets", EditorStyles.boldLabel);

            using (EditorGUILayout.HorizontalScope _ = new EditorGUILayout.HorizontalScope())
            {
                _assetFolderPath = EditorGUILayout.TextField("Asset Folder", _assetFolderPath ?? string.Empty);
                if (GUILayout.Button("Browse", GUILayout.Width(96f)))
                    BrowseAssetFolder();
            }

            EditorGUILayout.HelpBox(
                "Leave Asset Folder empty for manifest-only or managed-legacy packages. " +
                "When a folder is supplied, every buildable asset under that folder is packed into [ModId].bundle. " +
                "This is not public runtime content ingress.",
                MessageType.None);
        }

        private void DrawAssemblySection()
        {
            EditorGUILayout.LabelField("Managed Assemblies", EditorStyles.boldLabel);

            for (int i = 0; i < _dllPaths.Count; i++)
            {
                using (EditorGUILayout.HorizontalScope _ = new EditorGUILayout.HorizontalScope())
                {
                    string label = i == 0 ? "Primary DLL" : "Support DLL";
                    _dllPaths[i] = EditorGUILayout.TextField(label, _dllPaths[i] ?? string.Empty);

                    if (GUILayout.Button("Browse", GUILayout.Width(70f)))
                        BrowseDllAtIndex(i);

                    if (GUILayout.Button("X", GUILayout.Width(24f)))
                    {
                        _dllPaths.RemoveAt(i);
                        i--;
                    }
                }
            }

            using (new EditorGUI.DisabledScope(_dllPaths.Count >= MaxManagedAssemblyInputCount))
            {
                if (GUILayout.Button("Add DLL", GUILayout.Width(100f)))
                    _dllPaths.Add(string.Empty);
            }

            if (_dllPaths.Count >= MaxManagedAssemblyInputCount)
            {
                EditorGUILayout.HelpBox(
                    "Managed assembly selection is capped at 32 files to match the runtime package DLL cap.",
                    MessageType.Warning);
            }

            if (HasNonEmptyEntry(_dllPaths))
                EditorGUILayout.HelpBox(EnvelopeOnlyRuntimeWarning, MessageType.Warning);
        }

        private void DrawDependencySection()
        {
            EditorGUILayout.LabelField("Dependencies", EditorStyles.boldLabel);

            for (int i = 0; i < _dependencyIds.Count; i++)
            {
                using (EditorGUILayout.HorizontalScope _ = new EditorGUILayout.HorizontalScope())
                {
                    string label = i == 0 ? "Dependency ID" : string.Empty;
                    _dependencyIds[i] = EditorGUILayout.TextField(label, _dependencyIds[i] ?? string.Empty);

                    if (GUILayout.Button("X", GUILayout.Width(24f)))
                    {
                        _dependencyIds.RemoveAt(i);
                        i--;
                    }
                }
            }

            if (GUILayout.Button("Add Dependency", GUILayout.Width(120f)))
                _dependencyIds.Add(string.Empty);
        }

        private void DrawValidationSection()
        {
            if (!TryValidateConfiguration(false, out string validationError))
                EditorGUILayout.HelpBox(validationError, MessageType.Error);
            else
                EditorGUILayout.HelpBox(
                    "Manifest and file paths are valid. Build Internal Legacy Package performs the deep asset and DLL identity scan.",
                    MessageType.Info);
        }

        private void DrawBuildActions()
        {
            using (new EditorGUI.DisabledScope(!TryValidateConfiguration(false, out _)))
            {
                if (GUILayout.Button("Build Internal Legacy Package", GUILayout.Height(34f)))
                    BuildModPackage();
            }
        }

        private void BrowseAssetFolder()
        {
            string selectedFolder = EditorUtility.OpenFolderPanel("Select Mod Asset Folder", Application.dataPath, string.Empty);
            if (string.IsNullOrWhiteSpace(selectedFolder))
                return;

            if (TryConvertAbsolutePathToAssetPath(selectedFolder, out string assetPath))
            {
                _assetFolderPath = assetPath;
                return;
            }

            EditorUtility.DisplayDialog(
                "Invalid Asset Folder",
                "Selected folder must live under the Unity Assets directory so AssetBundle build input can be resolved through AssetDatabase.",
                "OK");
        }

        private void BrowseDllAtIndex(int index)
        {
            string initialDirectory = GetProjectRootPath();
            string selectedFile = EditorUtility.OpenFilePanel("Select Managed Assembly", initialDirectory, "dll");
            if (string.IsNullOrWhiteSpace(selectedFile))
                return;

            if ((uint)index >= (uint)_dllPaths.Count)
                return;

            _dllPaths[index] = selectedFile;
        }

        private void BuildModPackage()
        {
            if (!TryValidateConfiguration(true, out string validationError))
            {
                EditorUtility.DisplayDialog("Invalid Mod Configuration", validationError, "OK");
                return;
            }

            try
            {
                string modId = _modId.Trim();
                string projectRoot = GetProjectRootPath();
                string modsRoot = Path.Combine(projectRoot, "Mods");
                string outputDirectory = Path.Combine(modsRoot, modId);
                Directory.CreateDirectory(modsRoot);
                Directory.CreateDirectory(outputDirectory);

                string bundleOutputPath = BuildBundleIfConfigured(modId, _assetFolderPath, _buildTarget);
                string[] copiedAssemblies = CopyAssemblies(outputDirectory);
                RemoveStaleAssemblies(outputDirectory, copiedAssemblies);

                ModManifestData manifest = new ModManifestData
                {
                    Id = modId,
                    Name = string.IsNullOrWhiteSpace(_modName) ? modId : _modName.Trim(),
                    Version = string.IsNullOrWhiteSpace(_modVersion) ? DefaultVersion : _modVersion.Trim(),
                    Author = string.IsNullOrWhiteSpace(_modAuthor) ? DefaultAuthor : _modAuthor.Trim(),
                    Dependencies = CollectNonEmptyEntries(_dependencyIds),
                    EntryAssembly = copiedAssemblies.Length > 0 ? copiedAssemblies[0] : string.Empty,
                    EntryType = string.Empty,
                    RequiredAPIVersion = _requiredApiVersion,
                    ModPriority = _modPriority
                };

                string finalBundlePath = Path.Combine(outputDirectory, modId + ".bundle");
                if (!string.IsNullOrWhiteSpace(bundleOutputPath))
                    CopyFileAtomic(bundleOutputPath, finalBundlePath);
                else if (File.Exists(finalBundlePath))
                    File.Delete(finalBundlePath);

                WriteManifest(outputDirectory, manifest);
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[ModBuilderWindow] Built internal legacy mod '{manifest.Id}' into '{outputDirectory}'. " +
                    $"Bundle={(string.IsNullOrWhiteSpace(bundleOutputPath) ? "none" : "present")} Assemblies={copiedAssemblies.Length}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModBuilderWindow] Build failed for mod '{_modId}': {ex}");
                EditorUtility.DisplayDialog("Mod Build Failed", ex.Message, "OK");
            }
        }

        private string BuildBundleIfConfigured(string modId, string assetFolderPath, BuildTarget buildTarget)
        {
            if (string.IsNullOrWhiteSpace(assetFolderPath))
                return null;

            string[] assetPaths = CollectBundleAssetPaths(assetFolderPath);
            if (assetPaths == null || assetPaths.Length == 0)
            {
                throw new InvalidOperationException(
                    "Asset folder does not contain any bundle-eligible assets. Leave Asset Folder empty for manifest-only packages.");
            }

            string tempOutputDirectory = Path.GetFullPath(Path.Combine("Temp", "ModBuilder", modId, buildTarget.ToString()));
            Directory.CreateDirectory(tempOutputDirectory);

            AssetBundleBuild build = new AssetBundleBuild
            {
                assetBundleName = modId + ".bundle",
                assetNames = assetPaths
            };

            BuildAssetBundleOptions options = BuildAssetBundleOptions.StrictMode;
            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                tempOutputDirectory,
                new[] { build },
                options,
                buildTarget);

            if (manifest == null)
                throw new InvalidOperationException("BuildPipeline.BuildAssetBundles returned null.");

            string bundlePath = Path.Combine(tempOutputDirectory, build.assetBundleName);
            if (!File.Exists(bundlePath))
                throw new FileNotFoundException("AssetBundle output was not produced.", bundlePath);

            return bundlePath;
        }

        private string[] CollectBundleAssetPaths(string assetFolderPath)
        {
            if (string.IsNullOrWhiteSpace(assetFolderPath) || !AssetDatabase.IsValidFolder(assetFolderPath))
                return Array.Empty<string>();

            string assetFolderAbsolutePath = ResolveAssetFolderAbsolutePath(assetFolderPath);
            if (!Directory.Exists(assetFolderAbsolutePath))
                return Array.Empty<string>();

            // COLD ALLOC: List<string>[512] - bounded AssetBundle asset path collection for mod build - owner: ModBuilderWindow
            List<string> assetPaths = new List<string>(MaxBundleBuildAssetCount);

            try
            {
                foreach (string filePath in Directory.EnumerateFiles(assetFolderAbsolutePath, "*", SearchOption.AllDirectories))
                {
                    string extension = Path.GetExtension(filePath);
                    if (IsEditorOnlyAssetExtension(extension))
                        continue;

                    if (!TryConvertAbsolutePathToAssetPath(filePath, out string assetPath))
                        continue;

                    UnityEngine.Object mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                    if (mainAsset == null)
                        continue;

                    if (assetPaths.Count >= MaxBundleBuildAssetCount)
                    {
                        throw new InvalidOperationException(
                            $"Asset folder exceeds {MaxBundleBuildAssetCount} bundle-eligible assets. Narrow the folder or split the mod package.");
                    }

                    assetPaths.Add(assetPath);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                throw new InvalidOperationException($"Failed to enumerate mod asset folder '{assetFolderPath}'.", exception);
            }

            assetPaths.Sort(StringComparer.OrdinalIgnoreCase);
            return assetPaths.ToArray();
        }

        private string[] CopyAssemblies(string outputDirectory)
        {
            string[] assemblyPaths = CollectNonEmptyEntries(_dllPaths);
            if (assemblyPaths.Length == 0)
                return Array.Empty<string>();

            if (assemblyPaths.Length > MaxManagedAssemblyInputCount)
            {
                throw new InvalidOperationException(
                    $"Managed assembly selection exceeds {MaxManagedAssemblyInputCount} files.");
            }

            // COLD ALLOC: string[][dll count] - manifest assembly filename list - owner: ModBuilderWindow
            string[] copiedFileNames = new string[assemblyPaths.Length];
            int copiedCount = 0;

            for (int i = 0; i < assemblyPaths.Length; i++)
            {
                string sourcePath = assemblyPaths[i];
                string fileName = Path.GetFileName(sourcePath);
                string destinationPath = Path.Combine(outputDirectory, fileName);
                CopyFileAtomic(sourcePath, destinationPath);
                copiedFileNames[copiedCount++] = fileName;
            }

            if (copiedCount == copiedFileNames.Length)
                return copiedFileNames;

            Array.Resize(ref copiedFileNames, copiedCount);
            return copiedFileNames;
        }

        private static void CopyFileAtomic(string sourcePath, string destinationPath)
        {
            string tempPath = destinationPath + ".tmp";
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                File.Copy(sourcePath, tempPath, false);
                if (File.Exists(destinationPath))
                    File.Replace(tempPath, destinationPath, null, true);
                else
                    File.Move(tempPath, destinationPath);
            }
            catch
            {
                TryDeleteFileNoThrow(tempPath);
                throw;
            }
        }

        private static void WriteTextAtomic(string destinationPath, string text)
        {
            string tempPath = destinationPath + ".tmp";
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                File.WriteAllText(tempPath, text);
                if (File.Exists(destinationPath))
                    File.Replace(tempPath, destinationPath, null, true);
                else
                    File.Move(tempPath, destinationPath);
            }
            catch
            {
                TryDeleteFileNoThrow(tempPath);
                throw;
            }
        }

        private static void TryDeleteFileNoThrow(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is NotSupportedException ||
                exception is System.Security.SecurityException)
            {
            }
        }

        private static void RemoveStaleAssemblies(string outputDirectory, string[] copiedAssemblies)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
                return;

            // COLD ALLOC: HashSet<string>[dll count] - SDK output cleanup - owner: ModBuilderWindow
            HashSet<string> currentAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (copiedAssemblies != null)
            {
                for (int i = 0; i < copiedAssemblies.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(copiedAssemblies[i]))
                        currentAssemblies.Add(copiedAssemblies[i]);
                }
            }

            int scannedCount = 0;
            try
            {
                foreach (string dllPath in Directory.EnumerateFiles(outputDirectory, "*.dll", SearchOption.TopDirectoryOnly))
                {
                    if (scannedCount >= MaxStaleAssemblyCleanupScanCount)
                    {
                        throw new InvalidOperationException(
                            $"Output directory contains more than {MaxStaleAssemblyCleanupScanCount} top-level DLL files. Clean the package directory before rebuilding.");
                    }

                    scannedCount++;
                    string fileName = Path.GetFileName(dllPath);
                    if (!currentAssemblies.Contains(fileName))
                        File.Delete(dllPath);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                throw new InvalidOperationException($"Failed to clean stale managed assemblies under '{outputDirectory}'.", exception);
            }
        }

        private void WriteManifest(string outputDirectory, ModManifestData manifest)
        {
            string json = JsonUtility.ToJson(manifest, true);
            string manifestPath = Path.Combine(outputDirectory, "mod.json");
            WriteTextAtomic(manifestPath, json);
        }

        private bool TryValidateConfiguration(bool includeExpensiveFileContentValidation, out string validationError)
        {
            if (!TryValidateModId(_modId, out validationError))
                return false;

            if (_requiredApiVersion <= 0)
            {
                validationError = "Required API Version must be positive.";
                return false;
            }

            if (_requiredApiVersion > CurrentRequiredApiVersion)
            {
                validationError = $"Required API Version cannot exceed current loader API version {CurrentRequiredApiVersion}.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(_assetFolderPath))
            {
                if (!AssetDatabase.IsValidFolder(_assetFolderPath))
                {
                    validationError = "Asset folder must point to a valid folder under Assets/.";
                    return false;
                }

            }

            string[] assemblyPaths = CollectNonEmptyEntries(_dllPaths);
            if (assemblyPaths.Length > MaxManagedAssemblyInputCount)
            {
                validationError = $"Managed assembly selection exceeds {MaxManagedAssemblyInputCount} files.";
                return false;
            }

            for (int i = 0; i < assemblyPaths.Length; i++)
            {
                string path = assemblyPaths[i];
                if (!File.Exists(path))
                {
                    validationError = $"Managed assembly not found: {path}";
                    return false;
                }

                if (!string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase))
                {
                    validationError = $"Managed assembly must be a .dll file: {path}";
                    return false;
                }

                for (int duplicateIndex = i + 1; duplicateIndex < assemblyPaths.Length; duplicateIndex++)
                {
                    if (string.Equals(
                        Path.GetFileName(path),
                        Path.GetFileName(assemblyPaths[duplicateIndex]),
                        StringComparison.OrdinalIgnoreCase))
                    {
                        validationError = $"Managed assembly file name is selected more than once: {Path.GetFileName(path)}";
                        return false;
                    }
                }

                if (includeExpensiveFileContentValidation &&
                    !TryValidateManagedAssemblyIdentity(path, out string assemblyValidationError))
                {
                    validationError = assemblyValidationError;
                    return false;
                }
            }

            for (int i = 0; i < _dependencyIds.Count; i++)
            {
                string dependencyId = _dependencyIds[i];
                if (string.IsNullOrWhiteSpace(dependencyId))
                    continue;

                if (!TryValidateModId(dependencyId, out string dependencyValidationError))
                {
                    validationError = $"Dependency ID is invalid: {dependencyId}. {dependencyValidationError}";
                    return false;
                }
            }

            validationError = string.Empty;
            return true;
        }

        private static bool TryValidateManagedAssemblyIdentity(string path, out string validationError)
        {
            validationError = string.Empty;

            string fileAssemblyName = Path.GetFileNameWithoutExtension(path);
            if (IsReservedManagedAssemblyName(fileAssemblyName))
            {
                validationError = $"Managed assembly file name is reserved for engine-owned assemblies: {fileAssemblyName}";
                return false;
            }

            try
            {
                global::System.Reflection.AssemblyName assemblyName = global::System.Reflection.AssemblyName.GetAssemblyName(path);
                if (assemblyName != null && IsReservedManagedAssemblyName(assemblyName.Name))
                {
                    validationError = $"Managed assembly identity is reserved for engine-owned assemblies: {assemblyName.Name}";
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
                validationError = $"Managed assembly identity could not be read: {path}";
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

        private bool TryValidateModId(string modId, out string validationError)
        {
            if (string.IsNullOrWhiteSpace(modId))
            {
                validationError = "Mod ID is required.";
                return false;
            }

            string trimmed = modId.Trim();
            if (!string.Equals(modId, trimmed, StringComparison.Ordinal))
            {
                validationError = "Mod ID must not contain leading or trailing whitespace.";
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
                    validationError =
                        "Mod ID may contain only lowercase latin letters, digits, '.', '_' and '-'.";
                    return false;
                }

                if (isSeparator)
                {
                    if (i == 0 || i == trimmed.Length - 1 || previousWasSeparator)
                    {
                        validationError = "Mod ID separators must be between lowercase letters or digits and cannot repeat.";
                        return false;
                    }
                }

                previousWasSeparator = isSeparator;
            }

            if (ContainsReservedModIdentifierSegment(trimmed))
            {
                validationError = "Mod ID contains a reserved filesystem device segment.";
                return false;
            }

            validationError = string.Empty;
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

        private static bool TryConvertAbsolutePathToAssetPath(string absolutePath, out string assetPath)
        {
            assetPath = string.Empty;
            if (string.IsNullOrWhiteSpace(absolutePath))
                return false;

            string normalizedPath = absolutePath.Replace('\\', '/');
            string normalizedAssetsRoot = Application.dataPath.Replace('\\', '/');
            if (!normalizedPath.StartsWith(normalizedAssetsRoot, StringComparison.OrdinalIgnoreCase))
                return false;

            assetPath = "Assets" + normalizedPath.Substring(normalizedAssetsRoot.Length);
            return true;
        }

        private static string ResolveAssetFolderAbsolutePath(string assetFolderPath)
        {
            string normalizedAssetPath = assetFolderPath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(GetProjectRootPath(), normalizedAssetPath));
        }

        private static bool IsEditorOnlyAssetExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return false;

            return string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".asmdef", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".asmref", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".meta", StringComparison.OrdinalIgnoreCase);
        }

        private static string[] CollectNonEmptyEntries(List<string> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<string>();

            // COLD ALLOC: string[][input count] - filtered manifest/build entries - owner: ModBuilderWindow
            int validCount = 0;
            for (int i = 0; i < values.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    validCount++;
            }

            if (validCount == 0)
                return Array.Empty<string>();

            string[] filtered = new string[validCount];
            int filteredIndex = 0;

            for (int i = 0; i < values.Count; i++)
            {
                string value = values[i];
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                filtered[filteredIndex++] = value.Trim();
            }

            return filtered;
        }

        private static bool HasNonEmptyEntry(List<string> values)
        {
            if (values == null || values.Count == 0)
                return false;

            for (int i = 0; i < values.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return true;
            }

            return false;
        }

        private static string GetProjectRootPath()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new InvalidOperationException("Project root could not be resolved from Application.dataPath.");

            return projectRoot;
        }
    }
}
