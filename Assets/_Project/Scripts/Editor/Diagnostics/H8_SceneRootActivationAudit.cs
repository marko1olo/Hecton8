using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools.Diagnostics
{
    /// <summary>
    /// READ-ONLY activation audit: for every root of a scene, reports whether it is active, and for
    /// every deactivated subtree reports how much authored gameplay is buried inside it.
    ///
    /// WHY THIS EXISTS. 02_HECTON_WORLD.unity holds a root named DEPRECATED_STUFF with m_IsActive=0.
    /// Assets/_Project/Editor/H8_SceneCleaner.cs:19-48 created it: that tool walks the scene roots,
    /// keeps only names containing TERRAIN, CAMERA, PLAYER, LIGHT, OCEAN, WATER, SUN, SKY,
    /// ATMOSPHERE, SYSTEM, MANAGER, DIRECTOR, REGISTRY or BOOTSTRAP, and reparents plus
    /// SetActive(false) everything else, then calls EditorSceneManager.SaveScene. "--- WORLD ---"
    /// matches none of those keep-tokens, so the authored world root was moved into the graveyard and
    /// switched off, and the save made it permanent. There is no inverse tool.
    ///
    /// WHAT THAT COSTS. An object under an inactive ancestor has activeInHierarchy == false however
    /// its own m_IsActive reads, so Unity runs no Awake and no OnEnable on any component in the
    /// subtree. Every OnEnable-based registration in it is skipped, and the default
    /// FindObjectsInactive.Exclude of FindObjectsByType / FindAnyObjectByType cannot see it either.
    /// That is why the only Fabricator in the project - Fabricator.OnEnable at
    /// Assets/_Project/Scripts/Fabricator.cs:589-611 owns all of its registration - is reported
    /// missing by runtime probes while being present in the scene asset.
    ///
    /// THE MEASUREMENT THAT DECIDES THE REPAIR is the SUPPRESSED-ACTIVE count: objects authored with
    /// m_IsActive=1 that are dead only because an ancestor is off. Those are the ones an author meant
    /// to be live, so they size the real regression. Objects that are themselves off may have been
    /// switched off on purpose and re-enabling the ancestor would resurrect them too - which is the
    /// blast radius, and the reason this tool reports both numbers separately and changes nothing.
    ///
    /// WHAT IT MEASURES: the authored scene graph, through the editor object model, so binary
    /// serialization is irrelevant. WHAT IT DOES NOT MEASURE: runtime composition - anything created
    /// by AddComponent, prefab instantiation or DontDestroyOnLoad. For that half use
    /// H8_HeadlessPlayModeProbe.
    ///
    /// WHY IT NEVER WRITES. AGENTS.md forbids automated passes from calling
    /// EditorSceneManager.SaveScene / MarkSceneDirty / EditorUtility.SetDirty on production assets.
    /// This tool calls none of them and never touches SetActive or .enabled. It opens scenes, reads,
    /// and prints. Because opening a scene Single would silently discard unsaved in-memory work -
    /// including an unsaved repair left behind by H8_PlacementOwnerEnabledAudit - it REFUSES to run
    /// while any loaded scene is dirty instead of destroying that work to produce a report.
    ///
    /// USAGE
    ///   Unity.exe -batchmode -quit -projectPath . -logFile Logs/root_activation.log \
    ///     -executeMethod Hecton8.EditorTools.Diagnostics.H8_SceneRootActivationAudit.Run \
    ///     [-h8ActivationScenes a.unity,b.unity] [-h8ActivationWatch TypeA,TypeB]
    ///   or the menu item Hecton8/Diagnostics/Scene Root Activation Audit.
    /// </summary>
    public static class H8_SceneRootActivationAudit
    {
        private const string Marker = "[H8_ROOT_ACTIVATION]";
        private const string MenuPath = "Hecton8/Diagnostics/Scene Root Activation Audit";

        private static readonly string[] DefaultScenes =
        {
            "Assets/_Project/Scenes/02_HECTON_WORLD.unity",
        };

        /// <summary>
        /// Type names whose reachability has been an open question, matched by Type.Name so this
        /// audit takes no compile-time dependency on the owning assembly or namespace. A name that
        /// matches no MonoBehaviour in the project is reported as UNRESOLVED rather than counted as
        /// absent, because "no such type" and "type present but buried" are different answers.
        /// </summary>
        private static readonly string[] DefaultWatchedTypeNames =
        {
            "Fabricator",
            "FabricatorPhysicalActuator",
            "WorldProceduralScatterDirector",
            "WorldZoneAnchor",
            "WorldSliceAnchor",
            "WorldContentSocket",
            "PickupItem",
        };

        /// <summary>
        /// How an object's own m_IsActive combines with its ancestor chain. Kept as a pure function
        /// of two bools so the self-test can exercise all four cases without building a scene.
        /// </summary>
        internal enum ActivationVerdict
        {
            /// <summary>Live: authored on, every ancestor on. Awake and OnEnable both run.</summary>
            LiveActive = 0,

            /// <summary>The switch that did it: this object is off while its ancestors are all on.</summary>
            DeactivationFrontier = 1,

            /// <summary>Authored ON but dead because an ancestor is off. The real regression.</summary>
            SuppressedActive = 2,

            /// <summary>Off, under something already off. Re-enabling the ancestor leaves it off.</summary>
            SuppressedInactive = 3,
        }

        internal static ActivationVerdict Classify(bool activeSelf, bool ancestorChainActive)
        {
            if (ancestorChainActive)
                return activeSelf ? ActivationVerdict.LiveActive : ActivationVerdict.DeactivationFrontier;

            return activeSelf ? ActivationVerdict.SuppressedActive : ActivationVerdict.SuppressedInactive;
        }

        /// <summary>
        /// "Gameplay component" criterion, deliberately the same one
        /// H8_WorldSceneCompositionProbe.CollectUnwired uses (that file, the projectOwned /
        /// namespace test around lines 568-575): a project-owned type is one whose namespace mentions
        /// Hecton, or one with no namespace at all, which is how Assembly-CSharp types appear here.
        /// Engine and third-party components are excluded so the count means "authored game logic",
        /// not "Transforms and MeshRenderers".
        /// </summary>
        internal static bool IsProjectOwnedComponentType(Type type)
        {
            if (type == null)
                return false;

            string ns = type.Namespace;
            if (ns == null)
                return true;

            return ns.IndexOf("Hecton", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private sealed class SubtreeTally
        {
            public string Path;
            public bool RootActiveSelf;
            public int Descendants;
            public int SuppressedActiveObjects;
            public int SuppressedInactiveObjects;
            public int ProjectComponents;
            public int MissingScripts;
            public int LifecycleComponents;
            public readonly Dictionary<string, int> ComponentHistogram = new Dictionary<string, int>(StringComparer.Ordinal);
            public readonly List<string> WatchedHits = new List<string>();
        }

        [MenuItem(MenuPath)]
        public static void Run()
        {
            string[] scenes = SplitArg("-h8ActivationScenes", DefaultScenes);
            string[] watched = SplitArg("-h8ActivationWatch", DefaultWatchedTypeNames);

            if (!SelfTestPassed())
                return;

            if (!DirtySceneGuardPassed())
                return;

            var knownTypeNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (Type derived in TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
                knownTypeNames.Add(derived.Name);

            var watchedSet = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < watched.Length; i++)
            {
                if (!knownTypeNames.Contains(watched[i]))
                {
                    Debug.Log(
                        Marker + " WATCH UNRESOLVED " + watched[i] + " - no MonoBehaviour by that name " +
                        "exists in this project, so its absence below is NOT evidence about the scene.");
                    continue;
                }

                watchedSet.Add(watched[i]);
            }

            Debug.Log(
                Marker + " START scenes=" + scenes.Length + " watchedTypes=" + watchedSet.Count +
                " (authored scene graph only, not runtime composition)");

            for (int i = 0; i < scenes.Length; i++)
                AuditScene(scenes[i], watchedSet);

            Debug.Log(Marker + " DONE - nothing was modified, marked dirty or saved.");
        }

        /// <summary>
        /// Opening a scene with OpenSceneMode.Single throws away unsaved in-memory edits without
        /// asking in batchmode. A diagnostic is never worth destroying a repair that has not been
        /// committed yet, so a dirty scene stops the run and names itself.
        /// </summary>
        private static bool DirtySceneGuardPassed()
        {
            var dirty = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.isDirty)
                    dirty.Add(scene.path.Length == 0 ? "<untitled>" : scene.path);
            }

            if (dirty.Count == 0)
                return true;

            Debug.LogError(
                Marker + " REFUSED - " + dirty.Count + " loaded scene(s) have unsaved changes: " +
                string.Join(", ", dirty) + ". This audit opens scenes Single, which would discard " +
                "those edits. Save them (Ctrl+S) or revert them (Ctrl+Z) and run again. No report " +
                "was produced and nothing was changed.");
            return false;
        }

        private static void AuditScene(string scenePath, HashSet<string> watchedSet)
        {
            if (!System.IO.File.Exists(scenePath))
            {
                Debug.LogWarning(Marker + " MISSING SCENE " + scenePath + " - not audited.");
                return;
            }

            Scene scene;
            try
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }
            catch (Exception ex)
            {
                Debug.LogError(Marker + " FAILED to open " + scenePath + ": " + ex.GetType().Name + ": " + ex.Message);
                return;
            }

            if (!scene.IsValid())
            {
                Debug.LogError(Marker + " INVALID SCENE " + scenePath);
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            Debug.Log(Marker + " SCENE " + scenePath + " roots=" + roots.Length);

            var rootNameCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var frontiers = new List<SubtreeTally>();
            int activeRoots = 0;
            int inactiveRoots = 0;
            int totalObjects = 0;

            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];

                rootNameCounts.TryGetValue(root.name, out int seen);
                rootNameCounts[root.name] = seen + 1;

                var tally = new SubtreeTally
                {
                    Path = root.name,
                    RootActiveSelf = root.activeSelf,
                };

                // The chain ABOVE a root is vacuously active, so pass true here regardless of the
                // root's own state. That makes an inactive root classify as DeactivationFrontier -
                // the switch that did it - instead of being miscounted as one of its own victims,
                // while its children still resolve to suppressed.
                Walk(root.transform, root.name, true, tally, watchedSet);
                totalObjects += tally.Descendants;

                if (root.activeSelf)
                    activeRoots++;
                else
                    inactiveRoots++;

                Debug.Log(
                    Marker + "   ROOT " + PadName(root.name) +
                    " activeSelf=" + (root.activeSelf ? "1" : "0") +
                    "  children=" + root.transform.childCount +
                    "  objectsInSubtree=" + tally.Descendants +
                    "  projectComponents=" + tally.ProjectComponents +
                    "  missingScripts=" + tally.MissingScripts,
                    root);

                if (!root.activeSelf)
                    frontiers.Add(tally);
            }

            Debug.Log(
                Marker + "   SUMMARY roots=" + roots.Length + " active=" + activeRoots +
                " INACTIVE=" + inactiveRoots + " objects=" + totalObjects);

            ReportDuplicateRootNames(rootNameCounts);
            ReportNestedFrontiersUnderActiveRoots(roots, watchedSet, frontiers);
            ReportFrontiers(frontiers);
        }

        /// <summary>
        /// A deactivated subtree does not have to be a root. WorldRuntimeBootstrapAuthoring and
        /// FabricationBootstrapAuthoring both locate their targets with GameObject.Find, which the
        /// Unity scripting reference documents as "Only returns active GameObjects", so a switched-off
        /// branch anywhere is invisible to them and they build a fresh duplicate beside it rather than
        /// reusing it. Finding those mid-hierarchy frontiers is what makes duplicate authoring
        /// visible before someone re-runs an authoring menu item and doubles the scene.
        /// </summary>
        private static void ReportNestedFrontiersUnderActiveRoots(
            GameObject[] roots,
            HashSet<string> watchedSet,
            List<SubtreeTally> frontiers)
        {
            for (int i = 0; i < roots.Length; i++)
            {
                if (!roots[i].activeSelf)
                    continue;

                CollectNestedFrontiers(roots[i].transform, roots[i].name, watchedSet, frontiers);
            }
        }

        private static void CollectNestedFrontiers(
            Transform transform,
            string path,
            HashSet<string> watchedSet,
            List<SubtreeTally> frontiers)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                string childPath = path + "/" + child.name;

                if (!child.gameObject.activeSelf)
                {
                    var tally = new SubtreeTally
                    {
                        Path = childPath,
                        RootActiveSelf = false,
                    };
                    // Reached only from an active chain, so the ancestors above this child ARE
                    // active: pass true so the child classifies as the frontier and everything
                    // below it classifies as suppressed.
                    Walk(child, childPath, true, tally, watchedSet);
                    frontiers.Add(tally);
                    continue;
                }

                CollectNestedFrontiers(child, childPath, watchedSet, frontiers);
            }
        }

        private static void ReportDuplicateRootNames(Dictionary<string, int> rootNameCounts)
        {
            bool any = false;
            foreach (KeyValuePair<string, int> entry in rootNameCounts)
            {
                if (entry.Value < 2)
                    continue;

                any = true;
                Debug.LogWarning(
                    Marker + "   DUPLICATE ROOT NAME '" + entry.Key + "' x" + entry.Value +
                    " - GameObject.Find returns whichever of these is active and first, so authoring " +
                    "tools and runtime lookups can disagree about which one they mean.");
            }

            if (!any)
                Debug.Log(Marker + "   no duplicate root names.");
        }

        private static void ReportFrontiers(List<SubtreeTally> frontiers)
        {
            if (frontiers.Count == 0)
            {
                Debug.Log(Marker + "   NO DEACTIVATED SUBTREES - every object in this scene is active in hierarchy.");
                return;
            }

            int totalSuppressedActive = 0;
            int totalProjectComponents = 0;
            int totalLifecycle = 0;

            for (int i = 0; i < frontiers.Count; i++)
            {
                SubtreeTally tally = frontiers[i];
                totalSuppressedActive += tally.SuppressedActiveObjects;
                totalProjectComponents += tally.ProjectComponents;
                totalLifecycle += tally.LifecycleComponents;

                Debug.LogWarning(
                    Marker + "   DEACTIVATED SUBTREE " + tally.Path +
                    "  objects=" + tally.Descendants +
                    "  authoredActiveButSuppressed=" + tally.SuppressedActiveObjects +
                    "  authoredOff=" + tally.SuppressedInactiveObjects +
                    "  projectComponents=" + tally.ProjectComponents +
                    "  ofThoseWithAwakeOrOnEnable=" + tally.LifecycleComponents +
                    "  missingScripts=" + tally.MissingScripts);

                if (tally.ComponentHistogram.Count > 0)
                    Debug.Log(Marker + "     buried component types: " + Summarize(tally.ComponentHistogram, 20));

                for (int h = 0; h < tally.WatchedHits.Count; h++)
                    Debug.LogWarning(Marker + "     WATCHED " + tally.WatchedHits[h]);
            }

            Debug.LogError(
                Marker + "   VERDICT " + frontiers.Count + " deactivated subtree(s) hold " +
                totalProjectComponents + " project component(s), of which " + totalLifecycle +
                " declare Awake or OnEnable and therefore register nothing, across " +
                totalSuppressedActive + " object(s) authored ACTIVE and suppressed only by an " +
                "ancestor. Re-enabling an ancestor also resurrects every authoredOff object beneath " +
                "it, so this is an authoring decision, not a repair this tool will make.");
        }

        private static void Walk(
            Transform transform,
            string path,
            bool ancestorChainActive,
            SubtreeTally tally,
            HashSet<string> watchedSet)
        {
            tally.Descendants++;

            ActivationVerdict verdict = Classify(transform.gameObject.activeSelf, ancestorChainActive);
            if (verdict == ActivationVerdict.SuppressedActive)
                tally.SuppressedActiveObjects++;
            else if (verdict == ActivationVerdict.SuppressedInactive)
                tally.SuppressedInactiveObjects++;

            Component[] components = transform.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];

                // GetComponents returns a null entry where the script asset is missing. Those are
                // real and worth counting, but they have no Type to attribute.
                if (component == null)
                {
                    tally.MissingScripts++;
                    continue;
                }

                Type type = component.GetType();
                if (!IsProjectOwnedComponentType(type))
                    continue;

                tally.ProjectComponents++;

                string typeName = type.Name;
                tally.ComponentHistogram.TryGetValue(typeName, out int n);
                tally.ComponentHistogram[typeName] = n + 1;

                if (DeclaresLifecycleCallback(type))
                    tally.LifecycleComponents++;

                if (watchedSet.Contains(typeName))
                {
                    tally.WatchedHits.Add(
                        typeName + " @ " + path + " (componentEnabled=" +
                        (component is Behaviour behaviour ? (behaviour.enabled ? "1" : "0") : "n/a") +
                        ", objectActiveSelf=" + (transform.gameObject.activeSelf ? "1" : "0") +
                        ", activeInHierarchy=" + (transform.gameObject.activeInHierarchy ? "1" : "0") + ")");
                }
            }

            bool childChainActive = ancestorChainActive && transform.gameObject.activeSelf;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                Walk(child, path + "/" + child.name, childChainActive, tally, watchedSet);
            }
        }

        /// <summary>
        /// Whether the type or any base type declares Awake or OnEnable. Both are private by
        /// convention in this project, so the lookup must include non-public instance methods and
        /// must walk the base chain itself - BindingFlags.FlattenHierarchy does not surface private
        /// members of a base type.
        /// </summary>
        private static bool DeclaresLifecycleCallback(Type type)
        {
            const BindingFlags Flags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            for (Type current = type; current != null && current != typeof(MonoBehaviour); current = current.BaseType)
            {
                if (current.GetMethod("OnEnable", Flags, null, Type.EmptyTypes, null) != null ||
                    current.GetMethod("Awake", Flags, null, Type.EmptyTypes, null) != null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Known-answer cases run before anything is printed. The classification table is the whole
        /// argument this tool makes, and the project-owned filter decides every count, so both are
        /// checked against answers that cannot drift. A failure suppresses the report: an instrument
        /// that cannot classify a case it is pointed at is worse than no instrument.
        /// </summary>
        private static bool SelfTestPassed()
        {
            if (Classify(true, true) != ActivationVerdict.LiveActive ||
                Classify(false, true) != ActivationVerdict.DeactivationFrontier ||
                Classify(true, false) != ActivationVerdict.SuppressedActive ||
                Classify(false, false) != ActivationVerdict.SuppressedInactive)
            {
                Debug.LogError(Marker + " SELF-TEST FAILED activation classification table is wrong. Report suppressed.");
                return false;
            }

            // Transform is an engine type and must never be counted as authored gameplay.
            if (IsProjectOwnedComponentType(typeof(Transform)))
            {
                Debug.LogError(
                    Marker + " SELF-TEST FAILED UnityEngine.Transform was classified as a project " +
                    "component, so every count would be inflated by one per object. Report suppressed.");
                return false;
            }

            // A type from this project's own namespace must be counted. Resolved through TypeCache by
            // name so the self-test does not hard-code an assembly reference it might not have.
            Type probe = null;
            foreach (Type derived in TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
            {
                if (string.Equals(derived.Name, "WorldProceduralScatterDirector", StringComparison.Ordinal))
                {
                    probe = derived;
                    break;
                }
            }

            if (probe == null)
            {
                Debug.LogError(
                    Marker + " SELF-TEST FAILED could not resolve WorldProceduralScatterDirector, so the " +
                    "positive case for the project-owned filter could not run. Report suppressed.");
                return false;
            }

            if (!IsProjectOwnedComponentType(probe))
            {
                Debug.LogError(
                    Marker + " SELF-TEST FAILED " + probe.FullName + " was not classified as a project " +
                    "component, so buried gameplay would be under-counted. Report suppressed.");
                return false;
            }

            if (!DeclaresLifecycleCallback(probe))
            {
                Debug.LogError(
                    Marker + " SELF-TEST FAILED " + probe.FullName + " has an OnEnable and the lifecycle " +
                    "probe missed it, so the lost-registration count would read low. Report suppressed.");
                return false;
            }

            Debug.Log(
                Marker + " SELF-TEST PASSED classification table exact, Transform excluded, " +
                probe.Name + " included with a lifecycle callback detected.");
            return true;
        }

        private static string PadName(string name)
        {
            return name.Length >= 34 ? name : name.PadRight(34);
        }

        private static string Summarize(Dictionary<string, int> counts, int take)
        {
            var list = new List<KeyValuePair<string, int>>(counts);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));

            var sb = new StringBuilder();
            for (int i = 0; i < list.Count && i < take; i++)
            {
                if (sb.Length > 0)
                    sb.Append(", ");
                sb.Append(list[i].Key).Append('=').Append(list[i].Value);
            }

            if (list.Count > take)
                sb.Append(", ...+").Append(list.Count - take).Append(" more");

            return sb.Length == 0 ? "<none>" : sb.ToString();
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
            // Hecton8.Environment shadows System.Environment inside this namespace root.
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
