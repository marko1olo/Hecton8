#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools.Authoring
{
    /// <summary>
    /// Reports every duplicate-named scene ROOT in the loaded scenes, and - behind a separate menu
    /// entry - removes the redundant copies of the one root shape that is provably fungible.
    ///
    /// WHY THIS EXISTS. Logs/omega_rootaudit3.log:996-1092 lists NINE scene roots named
    /// H8_PlayModeScreenshotter in 02_HECTON_WORLD, each `activeSelf=1 children=0
    /// objectsInSubtree=1 projectComponents=1 missingScripts=0`, and :1116 flags the group:
    /// "DUPLICATE ROOT NAME 'H8_PlayModeScreenshotter' x9". H8_SceneRootActivationAudit reports that
    /// group and deliberately writes nothing (its own doc comment, :43-48). This file is the write
    /// half, and only for that group.
    ///
    /// WHAT AUTHORED THEM. Assets/_Project/Editor/H8_ScreenshotTaker_PlayMode.cs:20-21 -
    ///     var go = new GameObject("H8_PlayModeScreenshotter");
    ///     go.AddComponent&lt;H8_PlayModeScreenshotter&gt;();
    /// with NO existence check of any kind, and it is the only AddComponent of that type anywhere
    /// under Assets. The comment above it (:19) says the injection is "in the unsaved editor scene
    /// state only", but the object is created with default hideFlags, so it is a fully serializable
    /// scene root sitting in a scene that was just opened Single and is now dirty. Contrast
    /// Assets/_Project/Scripts/Editor/H8_RouteCaptureStation.cs:674-677, which gives its temporary
    /// camera `HideFlags.HideAndDontSave` precisely so that "it cannot be saved into a production
    /// scene by any later save the user performs". Any of the many unconditional
    /// EditorSceneManager.SaveScene callers that open this scene in the same editor session cements
    /// one copy - Assets/_Project/Editor/H8_SceneCleaner.cs:47,
    /// Assets/_Project/Editor/HectonVisualsConfigurator.cs:107,
    /// Assets/_Project/Editor/Rescue02_Final.cs:140-141,
    /// Assets/_Project/Editor/Hecton020Fixer.cs:63-66 are four of them. The next injection then
    /// opens a scene that already has one and adds a second. Nine cycles, nine roots.
    ///
    /// So removal here is symptom repair. The recurrence is fixed in the injector, not in this file.
    /// Every report this tool prints names that file and line for exactly that reason.
    ///
    /// WHY REMOVAL IS SAFE FOR THIS GROUP AND NOT IN GENERAL. Hecton8.Tools.H8_PlayModeScreenshotter
    /// (Assets/_Project/Scripts/Tools/H8_PlayModeScreenshotter.cs:158-408) has NO serialized state:
    /// every field is private with no [SerializeField] (:198-201), and the only shared field,
    /// ExternalSessionOwner (:194), is static. Nine instances are therefore bit-identical and
    /// interchangeable, so "keep one, drop the rest" loses nothing. That reasoning does not transfer.
    /// Two roots named `--- WORLD ---` are NOT interchangeable, and
    /// H8_WorldRootGraveyardRepair.cs:171-179 refuses that exact case rather than picking a winner.
    /// This tool therefore reports every duplicate group and will only DELETE from an allowlist of
    /// one type, after a per-instance shape check. Everything else is reported and refused by name.
    ///
    /// WHY IT REFUSES ON A DIRTY SCENE instead of opening or saving anyway. Removal ends in
    /// EditorSceneManager.SaveScene, which rewrites the whole binary scene - so a save would cement
    /// whatever unsaved work another lane left in memory along with the removal. Report never
    /// refuses; it prints the dirty flag so the reader knows what state was measured.
    ///
    /// WHY REPORT AND REMOVE ARE DIFFERENT MENU ENTRIES. 02_HECTON_WORLD.unity is BINARY. There is
    /// no diff to inspect after the fact, so a production scene write must not be one misclick away
    /// from a diagnostic.
    ///
    /// USAGE
    ///   Menu: Hecton8/Authoring/Duplicate Scene Roots - REPORT ONLY
    ///         Hecton8/Authoring/Duplicate Scene Roots - REMOVE EXTRA SCREENSHOTTERS AND SAVE
    ///   Batchmode (reports by default):
    ///     Unity.exe -batchmode -quit -projectPath . -logFile Logs/duproot.log \
    ///       -executeMethod Hecton8.EditorTools.Authoring.H8_DuplicateSceneRootAudit.Run \
    ///       [-h8DuplicateRootScene Assets/_Project/Scenes/02_HECTON_WORLD.unity] \
    ///       [-h8RemoveDuplicateScreenshotters | -h8RemoveAllScreenshotters]
    ///
    /// -h8RemoveAllScreenshotters is intentionally batchmode-only and has no menu entry. Keeping
    /// zero is the stronger position - H8_ScreenshotTaker_PlayMode injects its own copy at :20-21, so
    /// the capture route needs no authored one, and any authored copy left in the scene arms
    /// EditorApplication.Exit(0) (H8_PlayModeScreenshotter.cs:270-271) against every ordinary Play
    /// button press roughly PlayerWaitSeconds + SettleSeconds later, because a manual session sets no
    /// ExternalSessionOwner. That is a scene-content decision for the lead, so it is available but
    /// not clickable.
    /// </summary>
    public static class H8_DuplicateSceneRootAudit
    {
        private const string Marker = "[H8_DUPROOT]";

        /// <summary>
        /// The single allowlisted removable root shape.
        ///
        /// A `typeof` and not a hard-coded string on purpose: if a sibling moves or renames the
        /// runtime class, this file fails to COMPILE. A string would silently match nothing, the
        /// tool would report "no removable duplicates", and that reads exactly like success. Silent
        /// degeneracy is the failure mode this project loses the most time to.
        /// </summary>
        private static readonly Type RemovableRootType = typeof(Hecton8.Tools.H8_PlayModeScreenshotter);

        /// <summary>Transform plus the allowlisted component, and nothing else.</summary>
        private const int ExpectedComponentCount = 2;

        private enum GroupDisposition
        {
            /// <summary>Extras may be deleted; the group passed the full shape check.</summary>
            RemovableExtras = 0,

            /// <summary>Duplicated, but the type is not on the allowlist. Reported only.</summary>
            RefusedNotAllowlisted = 1,

            /// <summary>Allowlisted name, but at least one instance failed the shape check.</summary>
            RefusedShapeMismatch = 2,
        }

        private sealed class RootRecord
        {
            // Deliberately NOT named `GameObject` / `HideFlags`. A field with the same identifier as
            // its own type compiles, but it shadows that type for every later reader of this class,
            // and `HideFlags.None` inside it would then resolve to the field.
            public GameObject Root;
            public int SiblingIndex;
            public bool ActiveSelf;
            public int ChildCount;
            public int SubtreeObjects;
            public int MissingScripts;
            public HideFlags RootHideFlags;
            public string ComponentList;
            public int ComponentCount;
            public bool CarriesAllowlistedType;
        }

        private sealed class DuplicateGroup
        {
            public string Name;
            public List<RootRecord> Instances = new List<RootRecord>();
            public GroupDisposition Disposition;
            public string RefusalReason;
        }

        [MenuItem("Hecton8/Authoring/Duplicate Scene Roots - REPORT ONLY", priority = 300)]
        public static void ReportOnly() => Execute(RemovalMode.None);

        [MenuItem("Hecton8/Authoring/Duplicate Scene Roots - REMOVE EXTRA SCREENSHOTTERS AND SAVE", priority = 301)]
        public static void RemoveExtraScreenshottersAndSave() => Execute(RemovalMode.KeepOne);

        private enum RemovalMode
        {
            None = 0,
            KeepOne = 1,
            KeepNone = 2,
        }

        /// <summary>
        /// Batchmode entry point. Reports unless a removal flag is present.
        ///
        /// System.Environment written in full: Hecton8.Environment shadows System.Environment inside
        /// the Hecton8.* namespace root and a bare `Environment` here fails CS0234.
        /// </summary>
        public static void Run()
        {
            string[] args = System.Environment.GetCommandLineArgs();

            RemovalMode mode = RemovalMode.None;
            string scenePath = null;

            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-h8RemoveDuplicateScreenshotters", StringComparison.Ordinal))
                    mode = RemovalMode.KeepOne;
                else if (string.Equals(args[i], "-h8RemoveAllScreenshotters", StringComparison.Ordinal))
                    mode = RemovalMode.KeepNone;
                else if (string.Equals(args[i], "-h8DuplicateRootScene", StringComparison.Ordinal) && i + 1 < args.Length)
                    scenePath = args[i + 1];
            }

            if (scenePath != null && !TryOpenSceneWithoutDiscardingWork(scenePath))
                return;

            Execute(mode);
        }

        /// <summary>
        /// OpenScene(Single) discards unsaved in-memory work without asking in batchmode. Refusing is
        /// the correct outcome - the same interest H8_RouteCaptureStation.cs:459-471 and
        /// H8_WorldRootGraveyardRepair.cs:119-129 protect.
        /// </summary>
        private static bool TryOpenSceneWithoutDiscardingWork(string scenePath)
        {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                Scene loaded = EditorSceneManager.GetSceneAt(i);
                if (!loaded.IsValid() || !loaded.isDirty)
                    continue;

                Debug.LogError(
                    $"{Marker} REFUSED to open '{scenePath}': loaded scene '{loaded.name}' has unsaved " +
                    "changes and opening Single would discard them. Save or discard deliberately, then re-run.");
                return false;
            }

            try
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Marker} FAILED to open '{scenePath}': {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        private static void Execute(RemovalMode mode)
        {
            // Play-mode objects are clones. Destroying one changes nothing on disk, and the operator
            // would read the success line as a scene repair that never happened.
            if (mode != RemovalMode.None && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError(
                    $"{Marker} REFUSED - the editor is in or entering play mode. Scene objects are " +
                    "clones there, so a removal would not change the scene asset. Exit play mode and re-run.");
                return;
            }

            // Saving a scene while scripts are broken is not the moment to rewrite a binary asset.
            if (mode != RemovalMode.None && EditorUtility.scriptCompilationFailed)
            {
                Debug.LogError(
                    $"{Marker} REFUSED - scripts failed to compile. Fix compilation before writing " +
                    "02_HECTON_WORLD.unity; the shape check below cannot see types that did not build.");
                return;
            }

            int sceneCount = EditorSceneManager.sceneCount;
            int totalGroups = 0;
            int totalDuplicateRoots = 0;
            int totalRemovable = 0;
            int totalRefused = 0;
            int totalDestroyed = 0;
            int scenesSaved = 0;

            for (int s = 0; s < sceneCount; s++)
            {
                Scene scene = EditorSceneManager.GetSceneAt(s);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    Debug.Log($"{Marker} SKIP scene index {s.ToString(CultureInfo.InvariantCulture)} - not a valid loaded scene.");
                    continue;
                }

                bool dirtyBefore = scene.isDirty;
                GameObject[] roots = scene.GetRootGameObjects();
                List<DuplicateGroup> groups = CollectDuplicateGroups(roots);

                Debug.Log(
                    $"{Marker} SCENE '{scene.name}' path='{scene.path}' roots={roots.Length.ToString(CultureInfo.InvariantCulture)} " +
                    $"dirtyBeforeThisTool={(dirtyBefore ? "TRUE" : "false")} duplicateGroups={groups.Count.ToString(CultureInfo.InvariantCulture)}");

                for (int g = 0; g < groups.Count; g++)
                {
                    DuplicateGroup group = groups[g];
                    totalGroups++;
                    totalDuplicateRoots += group.Instances.Count;

                    if (group.Disposition == GroupDisposition.RemovableExtras)
                    {
                        // Mode-aware: in KeepNone every instance goes, so reporting Count-1 would
                        // understate the write by exactly one root per group.
                        totalRemovable += mode == RemovalMode.KeepNone
                            ? group.Instances.Count
                            : group.Instances.Count - 1;
                    }
                    else
                    {
                        totalRefused++;
                    }

                    ReportGroup(scene, group, mode);
                }

                if (groups.Count == 0)
                {
                    Debug.Log($"{Marker}   no duplicate root names in '{scene.name}'.");
                    continue;
                }

                if (mode == RemovalMode.None)
                    continue;

                if (dirtyBefore)
                {
                    Debug.LogError(
                        $"{Marker} REFUSED to write '{scene.name}' - it was ALREADY dirty before this tool " +
                        "ran. SaveScene rewrites the whole binary scene, so the save would cement another " +
                        "lane's unsaved work together with the removal. Save or discard that work " +
                        "deliberately, then re-run.");
                    continue;
                }

                int destroyed = RemoveExtras(groups, mode);
                totalDestroyed += destroyed;

                if (destroyed == 0)
                {
                    Debug.Log($"{Marker}   nothing removable in '{scene.name}'; scene not written.");
                    continue;
                }

                EditorSceneManager.MarkSceneDirty(scene);
                bool saved = EditorSceneManager.SaveScene(scene);
                if (saved)
                    scenesSaved++;

                Debug.Log(
                    $"{Marker} {(saved ? "SAVED" : "SAVE FAILED FOR")} '{scene.name}' after destroying " +
                    $"{destroyed.ToString(CultureInfo.InvariantCulture)} root(s). " +
                    $"rootsNow={scene.GetRootGameObjects().Length.ToString(CultureInfo.InvariantCulture)}");

                if (!saved)
                {
                    Debug.LogError(
                        $"{Marker} EditorSceneManager.SaveScene returned false for '{scene.name}'. The " +
                        "removal is in memory ONLY. Do not treat it as on disk, and do not let another " +
                        "tool open a scene Single over it.");
                }
            }

            Debug.Log(
                $"{Marker} SUMMARY mode={mode} scenes={sceneCount.ToString(CultureInfo.InvariantCulture)} " +
                $"duplicateGroups={totalGroups.ToString(CultureInfo.InvariantCulture)} " +
                $"duplicateRoots={totalDuplicateRoots.ToString(CultureInfo.InvariantCulture)} " +
                $"removableExtras={totalRemovable.ToString(CultureInfo.InvariantCulture)} " +
                $"refusedGroups={totalRefused.ToString(CultureInfo.InvariantCulture)} " +
                $"destroyed={totalDestroyed.ToString(CultureInfo.InvariantCulture)} " +
                $"scenesSaved={scenesSaved.ToString(CultureInfo.InvariantCulture)}");

            if (mode == RemovalMode.None && totalRemovable > 0)
            {
                Debug.Log(
                    $"{Marker} REPORT ONLY - nothing was changed. " +
                    $"{totalRemovable.ToString(CultureInfo.InvariantCulture)} extra root(s) are removable. " +
                    "Run 'Hecton8/Authoring/Duplicate Scene Roots - REMOVE EXTRA SCREENSHOTTERS AND SAVE', " +
                    "or pass -h8RemoveDuplicateScreenshotters in batchmode, to write.");
            }

            if (totalRemovable > 0 || totalDestroyed > 0)
            {
                Debug.LogWarning(
                    $"{Marker} RECURRENCE NOT FIXED BY THIS TOOL. Assets/_Project/Editor/" +
                    "H8_ScreenshotTaker_PlayMode.cs:20-21 adds a new root every call with no existence " +
                    "check and no HideFlags.HideAndDontSave, so the next -executeMethod " +
                    "TakeScreenshotAndExit followed by any unconditional EditorSceneManager.SaveScene " +
                    "re-authors one. Fix that call site or this repair is a treadmill.");
            }
        }

        /// <summary>
        /// Groups roots by exact name and keeps only the groups with more than one member. Ordinal
        /// comparison: Unity object names are not culture-sensitive identifiers and a culture-aware
        /// compare could merge two genuinely different names on a Turkish-locale machine.
        /// </summary>
        private static List<DuplicateGroup> CollectDuplicateGroups(GameObject[] roots)
        {
            var byName = new Dictionary<string, DuplicateGroup>(StringComparer.Ordinal);
            var ordered = new List<DuplicateGroup>();

            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null)
                    continue;

                if (!byName.TryGetValue(root.name, out DuplicateGroup group))
                {
                    group = new DuplicateGroup { Name = root.name };
                    byName.Add(root.name, group);
                    ordered.Add(group);
                }

                group.Instances.Add(Describe(root, i));
            }

            var duplicates = new List<DuplicateGroup>();
            for (int i = 0; i < ordered.Count; i++)
            {
                DuplicateGroup group = ordered[i];
                if (group.Instances.Count < 2)
                    continue;

                Classify(group);
                duplicates.Add(group);
            }

            return duplicates;
        }

        private static RootRecord Describe(GameObject root, int siblingIndex)
        {
            var entry = new RootRecord
            {
                Root = root,
                SiblingIndex = siblingIndex,
                ActiveSelf = root.activeSelf,
                ChildCount = root.transform.childCount,
                SubtreeObjects = root.GetComponentsInChildren<Transform>(true).Length,
                RootHideFlags = root.hideFlags,
            };

            // GetComponents<Component> returns a null entry for every component whose script cannot
            // be resolved. Counting those separately is the whole point: a root with a missing script
            // is not a root whose contents this tool understands.
            Component[] components = root.GetComponents<Component>();
            entry.ComponentCount = components.Length;

            var names = new StringBuilder(96);
            for (int i = 0; i < components.Length; i++)
            {
                if (i > 0)
                    names.Append(',');

                Component component = components[i];
                if (component == null)
                {
                    entry.MissingScripts++;
                    names.Append("<MISSING SCRIPT>");
                    continue;
                }

                Type type = component.GetType();
                names.Append(type.FullName);

                if (type == RemovableRootType)
                    entry.CarriesAllowlistedType = true;
            }

            entry.ComponentList = names.ToString();
            return entry;
        }

        /// <summary>
        /// Decides whether a group's extras may be deleted. Every instance must pass, not just the
        /// ones being deleted: if the group is not uniform then "which copy is the real one" is a
        /// question, and a tool that guesses the answer to that question in a binary scene is worse
        /// than one that refuses.
        /// </summary>
        private static void Classify(DuplicateGroup group)
        {
            if (!string.Equals(group.Name, RemovableRootType.Name, StringComparison.Ordinal))
            {
                group.Disposition = GroupDisposition.RefusedNotAllowlisted;
                group.RefusalReason =
                    "root name is not on the removal allowlist ('" + RemovableRootType.Name + "'), so " +
                    "which instance is authoritative is an authoring decision this tool will not make";
                return;
            }

            for (int i = 0; i < group.Instances.Count; i++)
            {
                RootRecord entry = group.Instances[i];
                string at = "instance at sibling index " + entry.SiblingIndex.ToString(CultureInfo.InvariantCulture);

                if (entry.MissingScripts != 0)
                {
                    group.Disposition = GroupDisposition.RefusedShapeMismatch;
                    group.RefusalReason =
                        at + " has " + entry.MissingScripts.ToString(CultureInfo.InvariantCulture) +
                        " missing script(s); its real contents are unknown";
                    return;
                }

                if (!entry.CarriesAllowlistedType)
                {
                    group.Disposition = GroupDisposition.RefusedShapeMismatch;
                    group.RefusalReason =
                        at + " does not carry " + RemovableRootType.FullName +
                        ", so the name collision is with something else";
                    return;
                }

                if (entry.ChildCount != 0 || entry.SubtreeObjects != 1)
                {
                    group.Disposition = GroupDisposition.RefusedShapeMismatch;
                    group.RefusalReason =
                        at + " has children (" + entry.SubtreeObjects.ToString(CultureInfo.InvariantCulture) +
                        " objects in subtree); somebody parented content under a throwaway root and " +
                        "deleting it would delete that content";
                    return;
                }

                if (entry.ComponentCount != ExpectedComponentCount)
                {
                    group.Disposition = GroupDisposition.RefusedShapeMismatch;
                    group.RefusalReason =
                        at + " carries " + entry.ComponentCount.ToString(CultureInfo.InvariantCulture) +
                        " components (" + entry.ComponentList + "); expected exactly Transform plus " +
                        RemovableRootType.Name;
                    return;
                }

                if (entry.RootHideFlags != HideFlags.None)
                {
                    // A hidden object is not part of the scene asset, so destroying it would not
                    // change what is on disk while the success line would say it did.
                    group.Disposition = GroupDisposition.RefusedShapeMismatch;
                    group.RefusalReason =
                        at + " has hideFlags=" + entry.RootHideFlags + "; it is a transient object, not " +
                        "authored scene content, and removing it would not change the scene on disk";
                    return;
                }
            }

            group.Disposition = GroupDisposition.RemovableExtras;
        }

        /// <summary>
        /// The role label is mode-aware on purpose. Labelling instance [0] "KEEP" and then printing a
        /// DESTROY line for it under -h8RemoveAllScreenshotters would make the log contradict itself,
        /// and a log that contradicts itself is how a wrong conclusion survives review.
        /// </summary>
        private static void ReportGroup(Scene scene, DuplicateGroup group, RemovalMode mode)
        {
            string headline =
                $"{Marker}   DUPLICATE GROUP '{group.Name}' x{group.Instances.Count.ToString(CultureInfo.InvariantCulture)} " +
                $"in '{scene.name}' -> {group.Disposition}";

            if (group.Disposition == GroupDisposition.RemovableExtras)
                Debug.Log(headline);
            else
                Debug.LogWarning(headline + " - " + group.RefusalReason);

            for (int i = 0; i < group.Instances.Count; i++)
            {
                RootRecord entry = group.Instances[i];

                string role;
                if (group.Disposition != GroupDisposition.RemovableExtras)
                    role = "reported";
                else if (mode == RemovalMode.KeepNone)
                    role = "EXTRA";
                else
                    role = i == 0 ? "KEEP" : "EXTRA";

                Debug.Log(
                    $"{Marker}     [{i.ToString(CultureInfo.InvariantCulture)}] {role} " +
                    $"siblingIndex={entry.SiblingIndex.ToString(CultureInfo.InvariantCulture)} " +
                    $"activeSelf={(entry.ActiveSelf ? "1" : "0")} " +
                    $"children={entry.ChildCount.ToString(CultureInfo.InvariantCulture)} " +
                    $"subtreeObjects={entry.SubtreeObjects.ToString(CultureInfo.InvariantCulture)} " +
                    $"hideFlags={entry.RootHideFlags} " +
                    $"missingScripts={entry.MissingScripts.ToString(CultureInfo.InvariantCulture)} " +
                    $"components={entry.ComponentList}",
                    entry.Root);
            }
        }

        /// <summary>
        /// Destroys the extras through Undo.DestroyObjectImmediate and collapses the whole pass into
        /// ONE undo step, so a single Ctrl+Z restores every removed root rather than eight.
        ///
        /// The kept instance is the one with the LOWEST sibling index. The nine copies are
        /// interchangeable - the component has no serialized state - so the choice is arbitrary and
        /// what matters is that it is deterministic and reproducible across runs and machines, which
        /// GameObject.Find order and FindObjectsByType order are not.
        /// </summary>
        private static int RemoveExtras(List<DuplicateGroup> groups, RemovalMode mode)
        {
            int firstRemovableIndex = mode == RemovalMode.KeepNone ? 0 : 1;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(Marker + " remove duplicate scene roots");

            int destroyed = 0;

            for (int g = 0; g < groups.Count; g++)
            {
                DuplicateGroup group = groups[g];
                if (group.Disposition != GroupDisposition.RemovableExtras)
                    continue;

                // Descending so the surviving records' sibling indices stay meaningful in the log
                // while the list is being consumed.
                for (int i = group.Instances.Count - 1; i >= firstRemovableIndex; i--)
                {
                    RootRecord entry = group.Instances[i];

                    // UnityEngine.Object's overloaded == is what makes this check correct: it reports
                    // true for an object that was already destroyed, which ReferenceEquals would not.
                    if (entry.Root == null)
                        continue;

                    Debug.Log(
                        $"{Marker}   DESTROY '{group.Name}' siblingIndex=" +
                        entry.SiblingIndex.ToString(CultureInfo.InvariantCulture) +
                        " components=" + entry.ComponentList);

                    Undo.DestroyObjectImmediate(entry.Root);
                    destroyed++;
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            return destroyed;
        }
    }
}
#endif
