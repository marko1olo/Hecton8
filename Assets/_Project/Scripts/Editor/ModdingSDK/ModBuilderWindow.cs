using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.ModdingSDK
{
    /// <summary>
    /// Editor-side SDK window for packaging a supported HECTON-8 mod folder.
    /// This tool builds an optional AssetBundle, emits <c>mod.json</c>, and copies managed assemblies into the runtime Mods directory.
    /// </summary>
    public sealed class ModBuilderWindow : EditorWindow
    {
        private const string DefaultVersion = "1.0.0";
        private const string DefaultAuthor = "Unknown";
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
        }

        private string _modId = string.Empty;
        private string _modName = string.Empty;
        private string _modVersion = DefaultVersion;
        private string _modAuthor = DefaultAuthor;
        private string _assetFolderPath = string.Empty;
        private BuildTarget _buildTarget = BuildTarget.StandaloneWindows64;
        private Vector2 _scrollPosition;

        // COLD ALLOC: List<string>[4] — managed assembly copy list for SDK builder UI — owner: ModBuilderWindow
        private readonly List<string> _dllPaths = new List<string>(4);
        // COLD ALLOC: List<string>[4] — dependency ID list for SDK builder UI — owner: ModBuilderWindow
        private readonly List<string> _dependencyIds = new List<string>(4);

        /// <summary>
        /// Opens the HECTON mod builder window.
        /// </summary>
        [MenuItem("Hecton/Modding/Mod Builder")]
        public static void ShowWindow()
        {
            ModBuilderWindow window = GetWindow<ModBuilderWindow>("Hecton Mod Builder");
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
                EditorGUILayout.LabelField("HECTON-8 Mod Builder", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Builds a supported mod package into ProjectRoot/Mods/[ModId]. " +
                    "The first DLL is treated as the primary entry assembly. Additional DLLs are copied as support assemblies.",
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
                "Leave Asset Folder empty for code-only mods. " +
                "When a folder is supplied, every buildable asset under that folder is packed into [ModId].bundle.",
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

            if (GUILayout.Button("Add DLL", GUILayout.Width(100f)))
                _dllPaths.Add(string.Empty);
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
            if (!TryValidateConfiguration(out string validationError))
                EditorGUILayout.HelpBox(validationError, MessageType.Error);
            else
                EditorGUILayout.HelpBox("Manifest and file selections are valid.", MessageType.Info);
        }

        private void DrawBuildActions()
        {
            using (new EditorGUI.DisabledScope(!TryValidateConfiguration(out _)))
            {
                if (GUILayout.Button("Build Mod", GUILayout.Height(34f)))
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
            if (!TryValidateConfiguration(out string validationError))
            {
                EditorUtility.DisplayDialog("Invalid Mod Configuration", validationError, "OK");
                return;
            }

            try
            {
                string projectRoot = GetProjectRootPath();
                string modsRoot = Path.Combine(projectRoot, "Mods");
                string outputDirectory = Path.Combine(modsRoot, _modId);
                Directory.CreateDirectory(modsRoot);
                Directory.CreateDirectory(outputDirectory);

                ModManifestData previousManifest = ReadExistingManifest(outputDirectory);
                string bundleOutputPath = BuildBundleIfConfigured(_modId, _assetFolderPath, _buildTarget);
                string[] copiedAssemblies = CopyAssemblies(outputDirectory);

                ModManifestData manifest = new ModManifestData
                {
                    Id = _modId,
                    Name = string.IsNullOrWhiteSpace(_modName) ? _modId : _modName.Trim(),
                    Version = string.IsNullOrWhiteSpace(_modVersion) ? DefaultVersion : _modVersion.Trim(),
                    Author = string.IsNullOrWhiteSpace(_modAuthor) ? DefaultAuthor : _modAuthor.Trim(),
                    Dependencies = CollectNonEmptyEntries(_dependencyIds),
                    EntryAssembly = copiedAssemblies.Length > 0 ? copiedAssemblies[0] : string.Empty,
                    EntryType = string.Empty
                };

                string finalBundlePath = Path.Combine(outputDirectory, _modId + ".bundle");
                if (!string.IsNullOrWhiteSpace(bundleOutputPath))
                    File.Copy(bundleOutputPath, finalBundlePath, true);
                else if (File.Exists(finalBundlePath))
                    File.Delete(finalBundlePath);

                if (!string.IsNullOrWhiteSpace(previousManifest.EntryAssembly) &&
                    !string.Equals(previousManifest.EntryAssembly, manifest.EntryAssembly, StringComparison.Ordinal))
                {
                    string staleEntryAssemblyPath = Path.Combine(outputDirectory, previousManifest.EntryAssembly);
                    if (File.Exists(staleEntryAssemblyPath))
                        File.Delete(staleEntryAssemblyPath);
                }

                WriteManifest(outputDirectory, manifest);
                AssetDatabase.Refresh();

                Debug.Log(
                    $"[ModBuilderWindow] Built mod '{manifest.Id}' into '{outputDirectory}'. " +
                    $"Bundle={(string.IsNullOrWhiteSpace(bundleOutputPath) ? "none" : "present")} Assemblies={copiedAssemblies.Length}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModBuilderWindow] Build failed for mod '{_modId}': {ex}");
                EditorUtility.DisplayDialog("Mod Build Failed", ex.Message, "OK");
            }
        }

        private ModManifestData ReadExistingManifest(string outputDirectory)
        {
            string manifestPath = Path.Combine(outputDirectory, "mod.json");
            if (!File.Exists(manifestPath))
                return default;

            string json = File.ReadAllText(manifestPath);
            return string.IsNullOrWhiteSpace(json)
                ? default
                : JsonUtility.FromJson<ModManifestData>(json);
        }

        private string BuildBundleIfConfigured(string modId, string assetFolderPath, BuildTarget buildTarget)
        {
            if (string.IsNullOrWhiteSpace(assetFolderPath))
                return null;

            string[] assetPaths = CollectBundleAssetPaths(assetFolderPath);
            if (assetPaths == null || assetPaths.Length == 0)
                return null;

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
            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { assetFolderPath });
            if (guids == null || guids.Length == 0)
                return Array.Empty<string>();

            // COLD ALLOC: List<string>[guid count] — AssetBundle asset path collection for mod build — owner: ModBuilderWindow
            List<string> assetPaths = new List<string>(guids.Length);

            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(assetPath) || AssetDatabase.IsValidFolder(assetPath))
                    continue;

                string extension = Path.GetExtension(assetPath);
                if (IsEditorOnlyAssetExtension(extension))
                    continue;

                UnityEngine.Object mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                if (mainAsset == null)
                    continue;

                assetPaths.Add(assetPath);
            }

            return assetPaths.ToArray();
        }

        private string[] CopyAssemblies(string outputDirectory)
        {
            string[] assemblyPaths = CollectNonEmptyEntries(_dllPaths);
            if (assemblyPaths.Length == 0)
                return Array.Empty<string>();

            // COLD ALLOC: List<string>[dll count] — manifest assembly filename list — owner: ModBuilderWindow
            List<string> copiedFileNames = new List<string>(assemblyPaths.Length);

            for (int i = 0; i < assemblyPaths.Length; i++)
            {
                string sourcePath = assemblyPaths[i];
                string fileName = Path.GetFileName(sourcePath);
                string destinationPath = Path.Combine(outputDirectory, fileName);
                File.Copy(sourcePath, destinationPath, true);
                copiedFileNames.Add(fileName);
            }

            return copiedFileNames.ToArray();
        }

        private void WriteManifest(string outputDirectory, ModManifestData manifest)
        {
            string json = JsonUtility.ToJson(manifest, true);
            string manifestPath = Path.Combine(outputDirectory, "mod.json");
            File.WriteAllText(manifestPath, json);
        }

        private bool TryValidateConfiguration(out string validationError)
        {
            if (!TryValidateModId(_modId, out validationError))
                return false;

            if (!string.IsNullOrWhiteSpace(_assetFolderPath))
            {
                if (!AssetDatabase.IsValidFolder(_assetFolderPath))
                {
                    validationError = "Asset folder must point to a valid folder under Assets/.";
                    return false;
                }

                if (!HasBundleEligibleAssets(_assetFolderPath))
                {
                    validationError =
                        "Asset folder does not contain any bundle-eligible assets. " +
                        "Leave it empty for code-only mods or point it to a populated Assets/ subtree.";
                    return false;
                }
            }

            string[] assemblyPaths = CollectNonEmptyEntries(_dllPaths);
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
            }

            validationError = string.Empty;
            return true;
        }

        private bool TryValidateModId(string modId, out string validationError)
        {
            if (string.IsNullOrWhiteSpace(modId))
            {
                validationError = "Mod ID is required.";
                return false;
            }

            string trimmed = modId.Trim();
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
            }

            validationError = string.Empty;
            return true;
        }

        private bool HasBundleEligibleAssets(string assetFolderPath)
        {
            string[] assetPaths = CollectBundleAssetPaths(assetFolderPath);
            return assetPaths != null && assetPaths.Length > 0;
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

            // COLD ALLOC: List<string>[input count] — filtered manifest/build entries — owner: ModBuilderWindow
            List<string> filtered = new List<string>(values.Count);

            for (int i = 0; i < values.Count; i++)
            {
                string value = values[i];
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                filtered.Add(value.Trim());
            }

            return filtered.ToArray();
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
