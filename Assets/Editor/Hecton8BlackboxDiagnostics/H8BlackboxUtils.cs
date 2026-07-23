// H8BlackboxUtils.cs — General utilities for Hecton8 Blackbox Diagnostics
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Hecton8.BlackboxDiagnostics
{
    public static class H8Utils
    {
        public const string ToolVersion = "1.0.0";
        public const string OutputRootName = "AI_Diagnostics";

        /// <summary>
        /// Create a timestamped output folder under AI_Diagnostics/ at project root.
        /// Returns the full path.
        /// </summary>
        public static string CreateOutputFolder()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string ts = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string folder = Path.Combine(projectRoot, OutputRootName, $"Hecton8_Blackbox_{ts}");
            Directory.CreateDirectory(folder);
            return folder;
        }

        /// <summary>
        /// Get the project root directory (parent of Assets/).
        /// </summary>
        public static string GetProjectRoot()
        {
            return Path.GetDirectoryName(Application.dataPath);
        }

        /// <summary>
        /// Get the full hierarchy path for a Transform.
        /// </summary>
        public static string GetHierarchyPath(Transform t)
        {
            if (t == null) return "";
            var sb = new StringBuilder(256);
            var current = t;
            while (current != null)
            {
                if (sb.Length > 0) sb.Insert(0, "/");
                sb.Insert(0, current.name);
                current = current.parent;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Get the parent chain as a list of H8ParentInfo.
        /// Walks from direct parent to root.
        /// </summary>
        public static List<H8ParentInfo> GetParentChain(Transform t)
        {
            var chain = new List<H8ParentInfo>();
            if (t == null) return chain;
            var current = t.parent;
            while (current != null)
            {
                chain.Add(new H8ParentInfo(current.name, current.gameObject.activeSelf, current.gameObject.layer));
                current = current.parent;
            }
            return chain;
        }

        /// <summary>
        /// Get the platform-specific Editor.log path.
        /// </summary>
        public static string GetEditorLogPath()
        {
            string localAppData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Unity", "Editor", "Editor.log");
        }

        internal static Func<string, bool> s_FileExists = File.Exists;
        internal static Func<string, FileMode, FileAccess, FileShare, Stream> s_FileStreamFactory =
            (path, mode, access, share) => new FileStream(path, mode, access, share);

        /// <summary>
        /// Read the last N lines of Editor.log. Returns empty string on failure.
        /// </summary>
        public static string ReadEditorLogTail(int lineCount = 200)
        {
            try
            {
                string logPath = GetEditorLogPath();
                if (!s_FileExists(logPath)) return $"Editor.log not found at: {logPath}";

                // Open with shared read access since Unity holds a write lock
                using (var fs = s_FileStreamFactory(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fs, Encoding.UTF8))
                {
                    var lines = new List<string>();
                    string line;
                    while ((line = reader.ReadLine()) != null) lines.Add(line);
                    int start = Math.Max(0, lines.Count - lineCount);
                    var sb = new StringBuilder();
                    for (int i = start; i < lines.Count; i++)
                    {
                        sb.AppendLine(lines[i]);
                    }
                    return sb.ToString();
                }
            }
            catch (Exception e)
            {
                return $"Failed to read Editor.log: {e.Message}";
            }
        }

        /// <summary>
        /// Run a git command safely. Returns stdout or error message.
        /// </summary>
        public static string RunGit(string args, int timeoutMs = 5000)
        {
            try
            {
                var psi = new ProcessStartInfo("git", args)
                {
                    WorkingDirectory = GetProjectRoot(),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(psi))
                {
                    if (proc == null) return "<git_process_null>";
                    string stdout = proc.StandardOutput.ReadToEnd();
                    string stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(timeoutMs);
                    if (!string.IsNullOrEmpty(stderr) && string.IsNullOrEmpty(stdout))
                        return $"<git_error: {stderr.Trim()}>";
                    return stdout;
                }
            }
            catch (Exception e)
            {
                return $"<git_unavailable: {e.Message}>";
            }
        }

        /// <summary>
        /// Check if git is available.
        /// </summary>
        public static bool IsGitAvailable()
        {
            var result = RunGit("--version");
            return result.StartsWith("git version");
        }

        /// <summary>
        /// Convert a layer mask integer to a list of visible layer names.
        /// </summary>
        public static List<string> LayerMaskToVisibleNames(int mask)
        {
            var names = new List<string>();
            for (int i = 0; i < 32; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    string layerName = LayerMask.LayerToName(i);
                    if (!string.IsNullOrEmpty(layerName))
                        names.Add(layerName);
                    else
                        names.Add($"Layer_{i}");
                }
            }
            return names;
        }

        /// <summary>
        /// Convert a layer mask integer to a list of culled (invisible) layer names.
        /// </summary>
        public static List<string> LayerMaskToCulledNames(int mask)
        {
            var names = new List<string>();
            for (int i = 0; i < 32; i++)
            {
                if ((mask & (1 << i)) == 0)
                {
                    string layerName = LayerMask.LayerToName(i);
                    if (!string.IsNullOrEmpty(layerName))
                        names.Add(layerName);
                }
            }
            return names;
        }

        /// <summary>
        /// Truncate a string to maxLen characters.
        /// </summary>
        public static string Truncate(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= maxLen) return s ?? "";
            return s.Substring(0, maxLen) + "...";
        }

        private static readonly List<Component> s_componentScratch = new List<Component>();

        /// <summary>
        /// Build a H8KeyObjectInfo from a GameObject found by a search key.
        /// </summary>
        public static H8KeyObjectInfo BuildKeyObjectInfo(string searchKey, GameObject go)
        {
            var info = new H8KeyObjectInfo();
            info.searchKey = searchKey;

            if (go == null)
            {
                info.exists = false;
                return info;
            }

            info.exists = true;
            info.objectName = go.name;
            info.hierarchyPath = GetHierarchyPath(go.transform);
            info.sceneName = go.scene.IsValid() ? go.scene.name : "N/A";
            info.activeSelf = go.activeSelf;
            info.activeInHierarchy = go.activeInHierarchy;
            info.parentChain = GetParentChain(go.transform);
            info.layerIndex = go.layer;
            info.layerName = LayerMask.LayerToName(go.layer);
            info.tag = go.tag;

            info.components = new List<H8ComponentInfo>();

            s_componentScratch.Clear();
            go.GetComponents(s_componentScratch);

            foreach (var comp in s_componentScratch)
            {
                if (comp == null) continue;
                var ci = new H8ComponentInfo();
                ci.typeName = comp.GetType().FullName;
                ci.isBehaviour = comp is Behaviour;
                ci.enabled = comp is Behaviour beh ? beh.enabled : true;
                info.components.Add(ci);
            }

            return info;
        }

        /// <summary>
        /// Find all GameObjects by exact name across loaded scenes, including inactive.
        /// </summary>
        public static List<GameObject> FindGameObjectsByName(string name, bool includeInactive = true)
        {
            var results = new List<GameObject>();
            if (string.IsNullOrEmpty(name)) return results;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    SearchChildren(root.transform, name, results, includeInactive);
                }
            }
            return results;
        }

        private static void SearchChildren(Transform parent, string name, List<GameObject> results, bool includeInactive)
        {
            if (parent.name == name)
            {
                if (includeInactive || parent.gameObject.activeInHierarchy)
                    results.Add(parent.gameObject);
            }
            for (int i = 0; i < parent.childCount; i++)
            {
                SearchChildren(parent.GetChild(i), name, results, includeInactive);
            }
        }

        /// <summary>
        /// Write a string to a file. Creates directories as needed.
        /// </summary>
        public static void WriteFile(string path, string content)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, content, Encoding.UTF8);
        }

        /// <summary>
        /// Get a timestamp string for filenames.
        /// </summary>
        public static string Timestamp()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        /// <summary>
        /// Count all GameObjects in a scene including inactive.
        /// </summary>
        public static void CountSceneObjects(Scene scene, out int total, out int active, out int inactive)
        {
            total = 0; active = 0; inactive = 0;
            if (!scene.isLoaded) return;
            var transforms = new List<Transform>();
            foreach (var root in scene.GetRootGameObjects())
            {
                root.GetComponentsInChildren<Transform>(true, transforms);
                foreach (var t in transforms)
                {
                    total++;
                    if (t.gameObject.activeInHierarchy) active++;
                    else inactive++;
                }
            }
        }

        /// <summary>
        /// Count components of a given type in a scene.
        /// </summary>
        public static int CountComponentsInScene<T>(Scene scene) where T : Component
        {
            int count = 0;
            if (!scene.isLoaded) return 0;
            var buffer = new List<T>();
            foreach (var root in scene.GetRootGameObjects())
            {
                root.GetComponentsInChildren<T>(true, buffer);
                count += buffer.Count;
            }
            return count;
        }

        /// <summary>
        /// Get console log entries via reflection on the internal LogEntries class.
        /// Returns empty list on failure.
        /// </summary>
        public static List<H8ConsoleEntry> GetConsoleLogs(int maxEntries = 500)
        {
            var entries = new List<H8ConsoleEntry>();
            try
            {
                var logEntriesType = System.Type.GetType("UnityEditor.LogEntries, UnityEditor");
                if (logEntriesType == null) return entries;

                var getCountMethod = logEntriesType.GetMethod("GetCount",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                var startMethod = logEntriesType.GetMethod("StartGettingEntries",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                var getEntryMethod = logEntriesType.GetMethod("GetEntryInternal",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                var endMethod = logEntriesType.GetMethod("EndGettingEntries",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

                if (getCountMethod == null || startMethod == null || endMethod == null) return entries;

                int count = (int)getCountMethod.Invoke(null, null);
                if (count == 0) return entries;

                startMethod.Invoke(null, null);
                try
                {
                    // Unity 6: LogEntry fields
                    var logEntryType = System.Type.GetType("UnityEditor.LogEntry, UnityEditor");
                    if (logEntryType == null && getEntryMethod != null)
                    {
                        // Try older API
                        endMethod.Invoke(null, null);
                        return entries;
                    }

                    int entryCount = Math.Min(count, maxEntries);
                    for (int i = 0; i < entryCount; i++)
                    {
                        try
                        {
                            var entryObj = Activator.CreateInstance(logEntryType);
                            if (getEntryMethod != null)
                            {
                                getEntryMethod.Invoke(null, new object[] { i, entryObj });
                            }

                            var msgField = logEntryType.GetField("message",
                                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                            var modeField = logEntryType.GetField("mode",
                                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

                            string message = msgField?.GetValue(entryObj) as string ?? "";
                            int mode = modeField != null ? (int)modeField.GetValue(entryObj) : 0;

                            var entry = new H8ConsoleEntry();
                            // mode flags: Error=1, Warning=2, Log=4, Exception=8 (approximate)
                            if ((mode & (1 << 0)) != 0 || (mode & (1 << 8)) != 0)
                                entry.type = "Error";
                            else if ((mode & (1 << 1)) != 0)
                                entry.type = "Warning";
                            else
                                entry.type = "Log";

                            // Split message and stack trace
                            int nlIdx = message.IndexOf('\n');
                            if (nlIdx >= 0)
                            {
                                entry.message = Truncate(message.Substring(0, nlIdx), 500);
                                entry.stackTrace = Truncate(message.Substring(nlIdx + 1), 1000);
                            }
                            else
                            {
                                entry.message = Truncate(message, 500);
                            }

                            // Classify
                            entry.category = ClassifyConsoleEntry(entry.message);
                            entries.Add(entry);
                        }
                        catch { /* skip individual entry errors */ }
                    }
                }
                finally
                {
                    endMethod.Invoke(null, null);
                }
            }
            catch (Exception e)
            {
                entries.Add(new H8ConsoleEntry
                {
                    type = "Warning",
                    message = $"Failed to read console logs via reflection: {e.Message}",
                    category = "Tool"
                });
            }

            return entries;
        }

        /// <summary>
        /// Classify a console message into a domain category.
        /// </summary>
        public static string ClassifyConsoleEntry(string message)
        {
            if (string.IsNullOrEmpty(message)) return "Unknown";
            var ml = message.ToLowerInvariant();
            if (ml.Contains("nullreferenceexception")) return "NullReference";
            if (ml.Contains("mapmagic")) return "MapMagic";
            if (ml.Contains("crest") || ml.Contains("ocean")) return "Crest";
            if (ml.Contains("urp") || ml.Contains("render pipeline") || ml.Contains("renderpipeline")) return "URP";
            if (ml.Contains("shader")) return "Shader";
            if (ml.Contains("addressable")) return "Addressables";
            if (ml.Contains("bootstrap")) return "Bootstrap";
            if (ml.Contains("globalregistry") || ml.Contains("registry")) return "Registry";
            if (ml.Contains("missing script") || ml.Contains("missing type")) return "MissingScript";
            if (ml.Contains("managed reference")) return "ManagedReference";
            if (ml.Contains("scene") && ml.Contains("load")) return "SceneLoad";
            if (ml.Contains("compile") || ml.Contains("compiler")) return "Compile";
            return "General";
        }

        /// <summary>
        /// Check if there are any dirty scenes in the hierarchy.
        /// </summary>
        public static bool HasDirtyScenes()
        {
            return GetDirtySceneNames().Count > 0;
        }

        /// <summary>
        /// Get names of all dirty scenes.
        /// </summary>
        public static List<string> GetDirtySceneNames()
        {
            var names = new List<string>();
            for (int i = 0; i < UnityEditor.SceneManagement.EditorSceneManager.sceneCount; i++)
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(i);
                if (scene.isDirty)
                {
                    names.Add(string.IsNullOrEmpty(scene.path) ? scene.name : scene.path);
                }
            }
            return names;
        }

        /// <summary>
        /// Get paths of all currently open scenes.
        /// </summary>
        public static string[] GetOpenScenePaths()
        {
            var paths = new List<string>();
            for (int i = 0; i < UnityEditor.SceneManagement.EditorSceneManager.sceneCount; i++)
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(i);
                if (string.IsNullOrEmpty(scene.path)) continue;
                paths.Add(scene.path);
            }
            return paths.ToArray();
        }

        /// <summary>
        /// Try to restore scenes given their paths. Returns false if fails.
        /// </summary>
        public static bool TryRestoreScenes(string[] paths)
        {
            try
            {
                if (paths == null || paths.Length == 0)
                {
                    Debug.LogWarning("[H8Blackbox] TryRestoreScenes: no paths provided.");
                    return false;
                }
                
                var validPaths = new List<string>();
                foreach (var p in paths)
                {
                    if (File.Exists(p)) validPaths.Add(p);
                    else Debug.LogWarning($"[H8Blackbox] TryRestoreScenes: scene file not found: {p}");
                }

                if (validPaths.Count == 0)
                {
                    Debug.LogWarning("[H8Blackbox] TryRestoreScenes: no valid scene paths to restore.");
                    return false;
                }
                
                // Open first scene single
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(validPaths[0], UnityEditor.SceneManagement.OpenSceneMode.Single);
                
                // Open remaining additively
                for (int i = 1; i < validPaths.Count; i++)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(validPaths[i], UnityEditor.SceneManagement.OpenSceneMode.Additive);
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[H8Blackbox] Failed to restore scenes: {e.Message}");
                return false;
            }
        }
    }
}
