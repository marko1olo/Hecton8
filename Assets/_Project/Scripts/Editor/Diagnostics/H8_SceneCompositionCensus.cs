using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools.Diagnostics
{
    /// <summary>
    /// Answers "is this component authored into this scene, and is it enabled" by walking the
    /// editor object model.
    ///
    /// WHY THIS HAS TO EXIST. ProjectSettings/EditorSettings.asset sets
    /// m_SerializationMode: 2 (ForceBinary), so every .unity file in this project is BINARY on
    /// disk. A GUID or type-name grep over scene files reads nothing and returns nothing, which
    /// looks exactly like "absent". That false negative has already produced one retracted
    /// census (851082186) and cost a second lane a wrong conclusion about
    /// HectonVoxelEngine. Text search cannot audit scene composition here. The object model can.
    ///
    /// WHAT IT MEASURES: authoring. Components placed in the scene asset.
    /// WHAT IT DOES NOT MEASURE: anything created at runtime - AddComponent, prefab
    /// instantiation, or DontDestroyOnLoad objects. A type absent from every scene may still
    /// exist in a running game, and a type present here may never be enabled. For the runtime
    /// question use H8_HeadlessPlayModeProbe; the two answer different halves and neither
    /// substitutes for the other.
    ///
    /// INSTRUMENT SELF-TEST. Two known-answer cases run before any report is printed, one that
    /// must be found and one that can never be. If either misbehaves the report is suppressed,
    /// because a census that cannot detect a component it is pointed at is worse than no census.
    ///
    /// USAGE
    ///   Unity.exe -batchmode -quit -projectPath . -logFile Logs/census.log \
    ///     -executeMethod Hecton8.EditorTools.Diagnostics.H8_SceneCompositionCensus.Run \
    ///     [-h8CensusScenes a.unity,b.unity] [-h8CensusTypes TypeA,TypeB] [-h8CensusHistogram 1]
    ///
    /// Opening a scene costs real time and 020_RENDER_SANDBOX.unity is 60 MB, so the default
    /// scene list is the three the game actually boots through.
    /// </summary>
    public static class H8_SceneCompositionCensus
    {
        private const string Marker = "[H8_CENSUS]";

        private static readonly string[] DefaultScenes =
        {
            "Assets/_Project/Scenes/00_BOOTSTRAP.unity",
            "Assets/_Project/Scenes/01_MAIN_MENU.unity",
            "Assets/_Project/Scenes/02_HECTON_WORLD.unity",
        };

        /// <summary>
        /// Runtime owners whose presence has been an open question. Every name here was checked
        /// against a real class declaration; an unresolvable name is reported as UNKNOWN TYPE
        /// NAME rather than silently counted as absent.
        /// </summary>
        private static readonly string[] DefaultTypes =
        {
            "GameBootstrapper",
            "MainMenuController",
            "MapMagicRuntimeBridge",
            "HectonVoxelEngine",
            "HectonWorldGenerator",
            "HectonFluidEngine",
            "HectonIndirectVegetationRenderer",
            "GpuScatterLodManager",
            "FloraInteractionManager",
            "FaunaGeneticsManager",
            "EcosystemHealthDirector",
        };

        // Must be found in 01_MAIN_MENU: the play-mode probe started a game by calling
        // ReadableStartNewGame on an instance the game itself had in scene '01_MAIN_MENU'.
        private const string SelfTestScene = "Assets/_Project/Scenes/01_MAIN_MENU.unity";
        private const string SelfTestMustExist = "MainMenuController";

        // Can never be found: this class is a static editor type, not a MonoBehaviour. If the
        // matcher reports it, the matcher is matching on something other than component type.
        private const string SelfTestMustNotExist = "H8_SceneCompositionCensus";

        public static void Run()
        {
            string[] scenes = SplitArg("-h8CensusScenes", DefaultScenes);
            string[] types = SplitArg("-h8CensusTypes", DefaultTypes);
            bool histogram = ReadArg("-h8CensusHistogram") != null;

            Debug.Log($"{Marker} START scenes={scenes.Length} types={types.Length} (authoring only, not runtime)");

            var perScene = new Dictionary<string, Dictionary<string, Sighting>>();
            var histograms = new Dictionary<string, Dictionary<string, int>>();
            var knownTypeNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (Type derived in TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
                knownTypeNames.Add(derived.Name);

            foreach (string scenePath in scenes)
            {
                if (!System.IO.File.Exists(scenePath))
                {
                    Debug.Log($"{Marker} MISSING SCENE {scenePath}");
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var sightings = new Dictionary<string, Sighting>(StringComparer.Ordinal);
                var counts = new Dictionary<string, int>(StringComparer.Ordinal);

                foreach (GameObject root in scene.GetRootGameObjects())
                    Walk(root.transform, string.Empty, sightings, counts);

                perScene[scenePath] = sightings;
                histograms[scenePath] = counts;
                Debug.Log(
                    $"{Marker} SCANNED {System.IO.Path.GetFileName(scenePath)} roots={scene.rootCount} " +
                    $"distinctComponentTypes={counts.Count}");
            }

            if (!SelfTestPassed(perScene, knownTypeNames))
                return;

            foreach (string typeName in types)
            {
                if (!knownTypeNames.Contains(typeName))
                {
                    Debug.Log($"{Marker} UNKNOWN TYPE NAME {typeName} - no MonoBehaviour by that name exists, so this row is NOT evidence of absence");
                    continue;
                }

                var line = new StringBuilder();
                line.Append(typeName.PadRight(34));
                foreach (string scenePath in scenes)
                {
                    if (!perScene.TryGetValue(scenePath, out Dictionary<string, Sighting> sightings))
                        continue;

                    string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                    if (sightings.TryGetValue(typeName, out Sighting sighting))
                        line.Append($"  {sceneName}={sighting.Total}(enabled {sighting.Enabled}, active {sighting.ActiveInHierarchy})");
                    else
                        line.Append($"  {sceneName}=absent");
                }

                Debug.Log($"{Marker} TYPE {line}");

                foreach (string scenePath in scenes)
                {
                    if (perScene.TryGetValue(scenePath, out Dictionary<string, Sighting> sightings) &&
                        sightings.TryGetValue(typeName, out Sighting sighting))
                    {
                        Debug.Log($"{Marker}   at {System.IO.Path.GetFileNameWithoutExtension(scenePath)}: {sighting.FirstPath}");
                    }
                }
            }

            if (histogram)
            {
                foreach (KeyValuePair<string, Dictionary<string, int>> entry in histograms)
                {
                    var sorted = new List<KeyValuePair<string, int>>(entry.Value);
                    sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
                    int limit = Math.Min(25, sorted.Count);
                    Debug.Log($"{Marker} HISTOGRAM {System.IO.Path.GetFileNameWithoutExtension(entry.Key)} top {limit} of {sorted.Count}");
                    for (int i = 0; i < limit; i++)
                        Debug.Log($"{Marker}   {sorted[i].Value,5}  {sorted[i].Key}");
                }
            }

            Debug.Log($"{Marker} DONE");
        }

        private static void Walk(
            Transform transform,
            string parentPath,
            Dictionary<string, Sighting> sightings,
            Dictionary<string, int> counts)
        {
            string path = parentPath.Length == 0 ? transform.name : parentPath + "/" + transform.name;

            // GetComponents<MonoBehaviour> returns a null entry for a component whose script is
            // missing. Those are real and worth seeing, but they have no type to attribute.
            MonoBehaviour[] components = transform.GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                MonoBehaviour component = components[i];
                if (component == null)
                {
                    counts.TryGetValue("<missing script>", out int broken);
                    counts["<missing script>"] = broken + 1;
                    continue;
                }

                string typeName = component.GetType().Name;
                counts.TryGetValue(typeName, out int seen);
                counts[typeName] = seen + 1;

                sightings.TryGetValue(typeName, out Sighting sighting);
                sighting.Total++;
                if (component.enabled)
                    sighting.Enabled++;
                if (component.gameObject.activeInHierarchy)
                    sighting.ActiveInHierarchy++;
                sighting.FirstPath ??= path;
                sightings[typeName] = sighting;
            }

            for (int i = 0; i < transform.childCount; i++)
                Walk(transform.GetChild(i), path, sightings, counts);
        }

        private static bool SelfTestPassed(
            Dictionary<string, Dictionary<string, Sighting>> perScene,
            HashSet<string> knownTypeNames)
        {
            if (!perScene.TryGetValue(SelfTestScene, out Dictionary<string, Sighting> menu))
            {
                Debug.Log(
                    $"{Marker} SELF-TEST SKIPPED - {SelfTestScene} was not in the scene list, so the " +
                    "positive case could not run. Results below are UNVALIDATED.");
                return true;
            }

            bool ok = true;
            if (!menu.ContainsKey(SelfTestMustExist))
            {
                Debug.Log(
                    $"{Marker} SELF-TEST FAILED - {SelfTestMustExist} was not found in 01_MAIN_MENU, and the " +
                    "play-mode probe has started a game through an instance the game had in that scene. " +
                    "The walk is missing components. Report suppressed.");
                ok = false;
            }

            foreach (Dictionary<string, Sighting> sightings in perScene.Values)
            {
                if (!sightings.ContainsKey(SelfTestMustNotExist))
                    continue;

                Debug.Log(
                    $"{Marker} SELF-TEST FAILED - {SelfTestMustNotExist} is a static editor class and was " +
                    "reported as a scene component. The matcher is not matching component types. " +
                    "Report suppressed.");
                ok = false;
                break;
            }

            if (ok)
            {
                Debug.Log(
                    $"{Marker} SELF-TEST PASSED - {SelfTestMustExist} found in 01_MAIN_MENU, " +
                    $"{SelfTestMustNotExist} correctly never reported, {knownTypeNames.Count} MonoBehaviour type names resolvable");
            }

            return ok;
        }

        private struct Sighting
        {
            public int Total;
            public int Enabled;
            public int ActiveInHierarchy;
            public string FirstPath;
        }

        private static string[] SplitArg(string name, string[] fallback)
        {
            string raw = ReadArg(name);
            if (string.IsNullOrEmpty(raw))
                return fallback;

            // StringSplitOptions.TrimEntries is .NET 5 and this compiles against netstandard2.1.
            string[] parts = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
                parts[i] = parts[i].Trim();

            return parts;
        }

        private static string ReadArg(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.Ordinal))
                    return args[i + 1];
            }

            return null;
        }
    }
}
