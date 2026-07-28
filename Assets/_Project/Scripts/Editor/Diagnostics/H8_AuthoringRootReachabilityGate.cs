using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools.Diagnostics
{
    /// <summary>
    /// READ-ONLY reachability gate for the hierarchy paths the world authoring tools look up.
    ///
    /// WHAT IT ANSWERS. For each path a Rebuild/Validate authoring entry point resolves, it asks the
    /// only question that decides whether that entry point reuses authored content or silently builds
    /// a duplicate beside it: does the FIRST path segment resolve as a SCENE ROOT, and if not, does an
    /// object of that name exist anywhere else in the scene at depth greater than zero?
    ///
    /// WHY A SEPARATE TOOL. Three authoring files were patched to be "inactive-inclusive":
    ///   Assets/_Project/Scripts/Editor/WorldRuntimeBootstrapAuthoring.cs:1139-1159
    ///     (FindSceneRootIncludingInactive), used by EnsureWorldRouteSkeleton:1190 and by
    ///     FindByPathIncludingInactive:1171-1186 for every "--- WORLD ---/..." lookup;
    ///   Assets/_Project/Scripts/Editor/FabricationBootstrapAuthoring.cs:333-360;
    ///   Assets/_Project/Scripts/Editor/ResourceWorldBootstrapAuthoring.cs:209-247.
    /// All three replaced GameObject.Find with a scan of Scene.GetRootGameObjects(), which does see an
    /// INACTIVE root. That closes one blind spot and leaves a second one wide open:
    /// GetRootGameObjects() returns depth-0 objects only, so an object that was REPARENTED under
    /// another root is invisible to it at any activation state. The XML docs on all three helpers
    /// state the cause they were written for as "H8_SceneCleaner.cs REPARENTED --- WORLD --- under
    /// DEPRECATED_STUFF and disabled it" - a reparent is exactly the case a root-only scan cannot see,
    /// so the patch does not cover the state it names.
    ///
    /// WHY THAT MATTERS MORE THAN IT LOOKS. The consequence is not a missing feature, it is an
    /// ordering hazard on a binary scene that cannot be diffed. While the authored root is buried,
    /// running any Rebuild entry point creates a SECOND, ACTIVE "--- WORLD ---" scene root holding
    /// only the bare Transforms EnsureRoutePath (WorldRuntimeBootstrapAuthoring.cs:1221-1236) makes,
    /// and every subsequent path lookup binds to that empty twin. At that point
    /// Assets/_Project/Scripts/Editor/Authoring/H8_WorldRootGraveyardRepair.cs:154-162 REFUSES,
    /// because merging two rival world roots is not a call it will make. So the cheap repair has a
    /// window, and a single misclick closes it. This gate measures whether the window is still open.
    ///
    /// WHAT IT MEASURES: the authored scene graph through the editor object model, so binary
    /// serialization is irrelevant and a text grep on the .unity file is not a substitute.
    /// WHAT IT DOES NOT MEASURE: activation cost, buried component counts, or runtime composition.
    /// Those are H8_SceneRootActivationAudit (which reports activation state per root and per
    /// deactivated subtree) and H8_HeadlessPlayModeProbe. This tool deliberately overlaps neither: it
    /// reports lookup REACHABILITY, not activation.
    ///
    /// WHY IT NEVER WRITES. AGENTS.md forbids automated passes from calling
    /// EditorSceneManager.SaveScene / MarkSceneDirty / EditorUtility.SetDirty on production assets.
    /// This tool calls none of them and never touches SetActive, SetParent or .enabled. Because
    /// opening a scene Single silently discards unsaved in-memory work - including an unsaved repair
    /// left behind by H8_PlacementOwnerEnabledAudit - it REFUSES to run while any loaded scene is
    /// dirty rather than destroying that work to produce a report.
    ///
    /// USAGE
    ///   Unity.exe -batchmode -quit -projectPath . -logFile Logs/root_reachability.log \
    ///     -executeMethod Hecton8.EditorTools.Diagnostics.H8_AuthoringRootReachabilityGate.Run \
    ///     [-h8ReachabilityScenes a.unity,b.unity]
    ///   or the menu item Hecton8/Diagnostics/Authoring Root Reachability Gate.
    /// </summary>
    public static class H8_AuthoringRootReachabilityGate
    {
        private const string Marker = "[H8_ROOT_REACH]";
        private const string MenuPath = "Hecton8/Diagnostics/Authoring Root Reachability Gate";
        private const string WorldRootName = "--- WORLD ---";
        private const string GraveyardRootName = "DEPRECATED_STUFF";
        private const string RepairMenuPath = "Hecton8/Authoring/World Root Graveyard Repair - REPORT ONLY";

        private static readonly string[] DefaultScenes =
        {
            "Assets/_Project/Scenes/02_HECTON_WORLD.unity",
        };

        /// <summary>
        /// One authoring lookup, copied from the owning source rather than invented. Every entry names
        /// the file and line that performs the lookup so a reader can confirm the path is real and has
        /// not drifted. Paths come from these constants and call sites:
        ///   WorldRuntimeBootstrapAuthoring.cs:46 WorldRootName, :42 ManagersRootName,
        ///     :1202-1216 EnsureRoutePath targets, :1350/:1375/:1400/:1425/:1450 slice lookups;
        ///   FabricationBootstrapAuthoring.cs:47 TrialRootName, :55-57, :220-236 call sites, :278;
        ///   ResourceWorldBootstrapAuthoring.cs:15 RootPath.
        /// </summary>
        private readonly struct AuthoringLookup
        {
            public readonly string Path;
            public readonly string Owner;
            public readonly bool CreatesOnMiss;

            public AuthoringLookup(string path, string owner, bool createsOnMiss)
            {
                Path = path;
                Owner = owner;
                CreatesOnMiss = createsOnMiss;
            }
        }

        private static readonly AuthoringLookup[] Lookups =
        {
            new AuthoringLookup(
                WorldRootName,
                "WorldRuntimeBootstrapAuthoring.cs:1190 EnsureWorldRouteSkeleton",
                true),
            new AuthoringLookup(
                "--- WORLD ---/Fabrication_Outpost",
                "FabricationBootstrapAuthoring.cs:376 CreateOrUpdateSceneFabricator (outpost)",
                true),
            new AuthoringLookup(
                "--- WORLD ---/Fabrication_Outpost/Forward_Fabricator",
                "FabricationBootstrapAuthoring.cs:278 ValidateSceneFabricator (outpost)",
                false),
            new AuthoringLookup(
                "Fabrication_Trial",
                "FabricationBootstrapAuthoring.cs:220 CreateOrUpdateSceneFabricator (trial, parentName=null)",
                true),
            new AuthoringLookup(
                "Fabrication_Trial/Trial_Fabricator",
                "FabricationBootstrapAuthoring.cs:277 ValidateSceneFabricator (trial)",
                false),
            new AuthoringLookup(
                "--- WORLD ---/Resource_FieldSources",
                "ResourceWorldBootstrapAuthoring.cs:117 validator RootPath",
                false),
            new AuthoringLookup(
                "--- WORLD ---/Starter_ReefField",
                "WorldRuntimeBootstrapAuthoring.cs:1375 ConfigureStarterReefFieldSlice",
                false),
            new AuthoringLookup(
                "Tool_Staging",
                "WorldRuntimeBootstrapAuthoring.cs:1450 tool staging slice",
                false),
            new AuthoringLookup(
                "[MANAGERS]",
                "WorldRuntimeBootstrapAuthoring.cs:87 managers root",
                true),
        };

        /// <summary>
        /// What a root-only scan concludes versus what the scene actually holds. Kept as a pure
        /// function of the two observations so the self-test can exercise every case without building
        /// a scene.
        /// </summary>
        internal enum ReachVerdict
        {
            /// <summary>Root scan resolved it and nothing of that name hides deeper. Reuse is correct.</summary>
            ResolvedAtRoot = 0,

            /// <summary>Nothing of that name anywhere. A create-on-miss tool legitimately creates it.</summary>
            Absent = 1,

            /// <summary>
            /// The defect: the root scan sees nothing, but the name exists deeper in the scene. A
            /// create-on-miss tool builds a duplicate beside authored content it cannot see.
            /// </summary>
            ShadowedBelowRoot = 2,

            /// <summary>Resolved at root AND present deeper. Two objects answer to one name.</summary>
            AmbiguousDuplicate = 3,
        }

        internal static ReachVerdict Classify(bool resolvedAtRoot, int matchesBelowRoot)
        {
            if (resolvedAtRoot)
                return matchesBelowRoot > 0 ? ReachVerdict.AmbiguousDuplicate : ReachVerdict.ResolvedAtRoot;

            return matchesBelowRoot > 0 ? ReachVerdict.ShadowedBelowRoot : ReachVerdict.Absent;
        }

        /// <summary>
        /// The first "/"-separated segment, which is the only segment resolved by a root scan in all
        /// three authoring tools. Transform.Find handles the remainder and already sees inactive
        /// children, so the remainder is never the blind spot.
        /// </summary>
        internal static string FirstSegment(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            int separator = path.IndexOf('/');
            return separator < 0 ? path : path.Substring(0, separator);
        }

        private sealed class DeepMatch
        {
            public string Path;
            public int Depth;
            public bool ActiveSelf;
            public bool ActiveInHierarchy;
            public bool TailResolved;
            public string TailFailureSegment;
        }

        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (!SelfTestPassed())
                return;

            if (!DirtySceneGuardPassed())
                return;

            string[] scenes = SplitArg("-h8ReachabilityScenes", DefaultScenes);

            Debug.Log(
                Marker + " START scenes=" + scenes.Length + " lookups=" + Lookups.Length +
                " (authored scene graph only; reports lookup reachability, not activation cost)");

            for (int i = 0; i < scenes.Length; i++)
                AuditScene(scenes[i]);

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
                string.Join(", ", dirty) + ". This gate opens scenes Single, which would discard " +
                "those edits. Save them (Ctrl+S) or revert them (Ctrl+Z) and run again. No report " +
                "was produced and nothing was changed.");
            return false;
        }

        private static void AuditScene(string scenePath)
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

            int shadowed = 0;
            int shadowedThatCreate = 0;
            int ambiguous = 0;

            for (int i = 0; i < Lookups.Length; i++)
            {
                AuthoringLookup lookup = Lookups[i];
                string rootName = FirstSegment(lookup.Path);

                GameObject rootMatch = ScanRootsOnly(roots, rootName);
                var deep = new List<DeepMatch>();
                CollectBelowRoot(roots, rootName, lookup.Path, deep);

                ReachVerdict verdict = Classify(rootMatch != null, deep.Count);

                string head =
                    Marker + "   " + verdict + " '" + lookup.Path + "'" +
                    "  rootScan=" + (rootMatch != null ? "HIT(activeSelf=" + (rootMatch.activeSelf ? "1" : "0") + ")" : "MISS") +
                    "  belowRootMatches=" + deep.Count +
                    "  owner=" + lookup.Owner +
                    "  createsOnMiss=" + (lookup.CreatesOnMiss ? "yes" : "no");

                switch (verdict)
                {
                    case ReachVerdict.ResolvedAtRoot:
                        Debug.Log(head, rootMatch);
                        break;

                    case ReachVerdict.Absent:
                        Debug.Log(
                            head + " - no object of this name exists at any depth, so a create-on-miss " +
                            "tool would be creating it for the first time, not duplicating anything.");
                        break;

                    case ReachVerdict.AmbiguousDuplicate:
                        ambiguous++;
                        Debug.LogWarning(
                            head + " - one object answers at scene root and " + deep.Count +
                            " more answer deeper. Authoring tools bind to the root one; anything that " +
                            "walks the hierarchy may bind to another.");
                        break;

                    case ReachVerdict.ShadowedBelowRoot:
                        shadowed++;
                        if (lookup.CreatesOnMiss)
                            shadowedThatCreate++;

                        Debug.LogError(
                            head + " - the root-only scan used by this owner CANNOT see a reparented " +
                            "object, so it reports absence for content that is present. " +
                            (lookup.CreatesOnMiss
                                ? "This owner CREATES on miss: running it now adds a duplicate beside the authored copy."
                                : "This owner VALIDATES on miss: it reports a FALSE ABSENCE."));
                        break;
                }

                for (int d = 0; d < deep.Count; d++)
                {
                    DeepMatch match = deep[d];
                    Debug.Log(
                        Marker + "     belowRoot: " + match.Path +
                        "  depth=" + match.Depth +
                        "  activeSelf=" + (match.ActiveSelf ? "1" : "0") +
                        "  activeInHierarchy=" + (match.ActiveInHierarchy ? "1" : "0") +
                        "  remainderOfPath=" + (match.TailResolved
                            ? "RESOLVES"
                            : "breaks at '" + match.TailFailureSegment + "'"));
                }
            }

            ReportRepairWindow(roots, shadowed, shadowedThatCreate, ambiguous);
        }

        /// <summary>
        /// The decision this whole report exists to inform: is the one-object graveyard repair still
        /// available, or has a Rebuild run already created the duplicate that closes it? The condition
        /// is read straight off H8_WorldRootGraveyardRepair's own guards
        /// (Authoring/H8_WorldRootGraveyardRepair.cs:154-162 refuses on an active duplicate root,
        /// :164-170 no-ops without a graveyard, :172-182 refuses without a direct buried child).
        /// </summary>
        private static void ReportRepairWindow(
            GameObject[] roots,
            int shadowed,
            int shadowedThatCreate,
            int ambiguous)
        {
            GameObject graveyard = null;
            int activeWorldRootsAtSceneRoot = 0;
            int inactiveWorldRootsAtSceneRoot = 0;

            for (int i = 0; i < roots.Length; i++)
            {
                if (string.Equals(roots[i].name, GraveyardRootName, StringComparison.Ordinal))
                {
                    graveyard = roots[i];
                    continue;
                }

                if (!string.Equals(roots[i].name, WorldRootName, StringComparison.Ordinal))
                    continue;

                if (roots[i].activeSelf)
                    activeWorldRootsAtSceneRoot++;
                else
                    inactiveWorldRootsAtSceneRoot++;
            }

            bool buriedWorldRootPresent = false;
            if (graveyard != null)
            {
                // Transform.Find DOES see inactive children, so a hit here is real and a null is a
                // real absence - not the root-scan blind spot this whole tool is about.
                buriedWorldRootPresent = graveyard.transform.Find(WorldRootName) != null;
            }

            Debug.Log(
                Marker + "   REPAIR WINDOW graveyard=" + (graveyard != null ? "present" : "absent") +
                "  buriedWorldRoot=" + (buriedWorldRootPresent ? "present" : "absent") +
                "  worldRootsAtSceneRoot=active:" + activeWorldRootsAtSceneRoot +
                "/inactive:" + inactiveWorldRootsAtSceneRoot);

            if (activeWorldRootsAtSceneRoot > 0 && buriedWorldRootPresent)
            {
                Debug.LogError(
                    Marker + "   WINDOW CLOSED - an ACTIVE '" + WorldRootName + "' already exists at scene " +
                    "root while an authored copy is still buried. " + RepairMenuPath + " refuses this " +
                    "state by design, so resolving it now means deciding by hand which of two world " +
                    "roots is authoritative in a binary scene.");
            }
            else if (buriedWorldRootPresent)
            {
                Debug.LogWarning(
                    Marker + "   WINDOW OPEN - the authored '" + WorldRootName + "' is buried and no rival " +
                    "root exists yet, so the single-object repair is still available. Run " +
                    RepairMenuPath + " BEFORE any Hecton8/Authoring Rebuild entry point. " +
                    shadowedThatCreate + " create-on-miss lookup(s) below would each add a duplicate if " +
                    "run first.");
            }

            if (shadowed == 0 && ambiguous == 0)
            {
                Debug.Log(
                    Marker + "   VERDICT every audited authoring lookup resolves at scene root with no " +
                    "rival of the same name. No root-scan blind spot is active in this scene.");
                return;
            }

            Debug.LogError(
                Marker + "   VERDICT " + shadowed + " lookup(s) name content that exists BELOW scene root " +
                "and is therefore invisible to the root-only scan all three authoring tools use, " +
                shadowedThatCreate + " of them in owners that CREATE on miss, plus " + ambiguous +
                " ambiguous name(s). Fixing the root cause means teaching those three helpers to fall " +
                "back to a full inactive-inclusive hierarchy search when the root scan misses - that is " +
                "a source change in files this diagnostic does not own.");
        }

        private static GameObject ScanRootsOnly(GameObject[] roots, string rootName)
        {
            // Deliberately identical to the three authoring helpers: name compare over
            // GetRootGameObjects() and nothing else. If this ever stops matching them, the report is
            // no longer evidence about their behaviour.
            for (int i = 0; i < roots.Length; i++)
            {
                if (string.Equals(roots[i].name, rootName, StringComparison.Ordinal))
                    return roots[i];
            }

            return null;
        }

        private static void CollectBelowRoot(
            GameObject[] roots,
            string rootName,
            string fullPath,
            List<DeepMatch> results)
        {
            for (int i = 0; i < roots.Length; i++)
                WalkForName(roots[i].transform, roots[i].name, 0, rootName, fullPath, results);
        }

        private static void WalkForName(
            Transform transform,
            string path,
            int depth,
            string rootName,
            string fullPath,
            List<DeepMatch> results)
        {
            // depth 0 is the scene root itself, which ScanRootsOnly already covered. Only strictly
            // deeper hits are the blind spot, so they are the only ones recorded here.
            if (depth > 0 && string.Equals(transform.name, rootName, StringComparison.Ordinal))
            {
                var match = new DeepMatch
                {
                    Path = path,
                    Depth = depth,
                    ActiveSelf = transform.gameObject.activeSelf,
                    ActiveInHierarchy = transform.gameObject.activeInHierarchy,
                    TailResolved = true,
                    TailFailureSegment = string.Empty,
                };

                ResolveTail(transform, fullPath, match);
                results.Add(match);
            }

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                WalkForName(child, path + "/" + child.name, depth + 1, rootName, fullPath, results);
            }
        }

        /// <summary>
        /// Walks the remaining path segments from a below-root match one segment at a time, so the
        /// report can name the exact segment where an authored path stops existing instead of only
        /// saying the path failed. Transform.Find would resolve the whole remainder in one call but
        /// would not say where it broke.
        /// </summary>
        private static void ResolveTail(Transform start, string fullPath, DeepMatch match)
        {
            int separator = fullPath.IndexOf('/');
            if (separator < 0)
                return;

            string[] segments = fullPath.Substring(separator + 1).Split('/');
            Transform current = start;
            for (int i = 0; i < segments.Length; i++)
            {
                Transform next = current.Find(segments[i]);
                if (next == null)
                {
                    match.TailResolved = false;
                    match.TailFailureSegment = segments[i];
                    return;
                }

                current = next;
            }
        }

        /// <summary>
        /// Known-answer cases run before anything is printed. The verdict table and the first-segment
        /// split are the entire argument this tool makes, and the lookup table is the entire input, so
        /// all three are checked against answers that cannot drift. A failure suppresses the report:
        /// an instrument that cannot classify a case it is pointed at is worse than no instrument.
        /// </summary>
        private static bool SelfTestPassed()
        {
            if (Classify(true, 0) != ReachVerdict.ResolvedAtRoot ||
                Classify(false, 0) != ReachVerdict.Absent ||
                Classify(false, 2) != ReachVerdict.ShadowedBelowRoot ||
                Classify(true, 1) != ReachVerdict.AmbiguousDuplicate)
            {
                Debug.LogError(Marker + " SELF-TEST FAILED reachability classification table is wrong. Report suppressed.");
                return false;
            }

            if (!string.Equals(FirstSegment("--- WORLD ---/Fabrication_Outpost"), WorldRootName, StringComparison.Ordinal) ||
                !string.Equals(FirstSegment("Fabrication_Trial"), "Fabrication_Trial", StringComparison.Ordinal) ||
                FirstSegment(string.Empty).Length != 0)
            {
                Debug.LogError(
                    Marker + " SELF-TEST FAILED first-segment split is wrong, so every lookup would be " +
                    "resolved against the wrong root name. Report suppressed.");
                return false;
            }

            for (int i = 0; i < Lookups.Length; i++)
            {
                if (!string.IsNullOrEmpty(Lookups[i].Path) && !string.IsNullOrEmpty(Lookups[i].Owner))
                    continue;

                Debug.LogError(
                    Marker + " SELF-TEST FAILED lookup table entry " + i + " has an empty path or owner, " +
                    "so its result would be unattributable. Report suppressed.");
                return false;
            }

            Debug.Log(
                Marker + " SELF-TEST PASSED verdict table exact, first-segment split exact, " +
                Lookups.Length + " lookup(s) attributed to an owning file and line.");
            return true;
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
