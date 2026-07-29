#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hecton8.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools.Authoring
{
    /// <summary>
    /// Enables the world scatter placement owner in a scene and PERSISTS the change to disk.
    ///
    /// WHAT IS WRONG. In Assets/_Project/Scenes/02_HECTON_WORLD.unity the single
    /// WorldProceduralScatterDirector is serialized with m_Enabled = 0. It is the only world owner in
    /// that scene that is off: WorldProceduralFillDirector, WorldProceduralFieldSampler,
    /// WorldContentDirector, WorldStreamingDirector, WorldPopulationDirector, ScatterBudgetController,
    /// WorldProceduralStateRegistry, ResourceDistributionDirector, WorldZoneDirector and
    /// BiomeMatrixDirector are all m_Enabled = 1 in the same file. Unity calls Awake on a disabled
    /// component of an active GameObject but never OnEnable, and the three registrations that make the
    /// director an owner are OnEnable-only - PublishActiveRuntimeInstance
    /// (WorldProceduralScatterDirector.cs:760), TryRegisterRuntimeDirector (:763) and
    /// TryRegisterHotSwapListener (:762). With s_activeRuntimeInstance null, HasRuntimeScatterOwner
    /// (:42) is false, so RuntimeNowSeconds (:37-39) returns a constant 0f and the 0.25 s gate in the
    /// tick path (:706-712) never reopens. The director places nothing while reading as correct.
    ///
    /// THIS IS ONE BUG, NOT TWO. The rule and prefab inventory behind the director is fully populated,
    /// so "disabled owner" must not be reported as "disabled owner over an empty list":
    /// Assets/_Project/Data/World/ProceduralPlacementRules holds 37 WorldProceduralPlacementRule
    /// assets, Assets/_Project/Data/World/ProceduralFamilies holds 33 WorldPrefabFamilyProfile assets
    /// carrying 241 VariantEntry rows over 225 distinct prefab GUIDs with zero dangling references, and
    /// every one of those 70 asset GUIDs is present in the scene's reference table.
    /// WorldRuntimeBootstrapAuthoring.ConfigureProceduralFill (:1379-1385) is what loads them into
    /// WorldProceduralFillDirector.SetRules / SetFamilies. This tool therefore reports the live
    /// Rules.Count and Families.Count next to the enabled flag, so the next reader does not have to
    /// re-derive which half is broken.
    ///
    /// WHY A SEPARATE TOOL FROM H8_PlacementOwnerEnabledAudit. That file
    /// (Assets/_Project/Scripts/Editor/Diagnostics/H8_PlacementOwnerEnabledAudit.cs) measures the same
    /// defect correctly and repairs it IN MEMORY ONLY - its own doc comment :35-40 explains that it
    /// marks the scene dirty and leaves Ctrl+S to a human, on purpose. That makes it unusable from
    /// -batchmode, where nobody presses Ctrl+S and the editor exits discarding the fix. It has
    /// MenuItems and no batchmode entry point. This file is the persisting half: it opens the scene
    /// itself, writes, and reports what landed on disk. Neither replaces the other - run the audit to
    /// measure whatever scenes are already open, run this to fix one named scene end to end.
    ///
    /// WHY WRITING IS PERMITTED HERE. AGENTS.md `Sandbox Firewall Rule` bans automated TEST RUNNERS
    /// from calling EditorSceneManager.SaveScene on production assets so that a test pass cannot wipe
    /// authored work. This is not a test runner and not a pass that runs on its own: the write is
    /// behind a distinct MenuItem or an explicit -h8ApplyScatterOwnerEnable argument, a bare
    /// -executeMethod reports and changes nothing, and the mutation is recorded with Undo. The
    /// established precedent in this folder is H8_WorldRootGraveyardRepair.cs:222-236 and
    /// H8_DuplicateSceneRootAudit.cs:303-316, both of which save the same production scene under the
    /// same split.
    ///
    /// WHY IT REFUSES ON DIRTY SCENES, TWICE. OpenScene(Single) silently discards unsaved in-memory
    /// work, and H8_PlacementOwnerEnabledAudit deliberately leaves exactly that kind of change behind,
    /// so a dirty scene at entry is a refusal. The second check is the one that matters more and that
    /// the sibling repairs do not make: 02_HECTON_WORLD is dirtied on open by editor code that injects
    /// serializable roots into it. Nine H8_PlayModeScreenshotter roots are already cemented into the
    /// scene by unconditional saves (H8_DuplicateSceneRootAudit.cs:17-39, quoting
    /// Logs/omega_rootaudit3.log:996-1092 and :1116). Do NOT read that citation as still naming a live
    /// injector: re-read on 2026-07-29, Assets/_Project/Editor/H8_ScreenshotTaker_PlayMode.cs now
    /// guards its injection with a FindObjectsByType existence check at :62-70 and reuses the existing
    /// root, so the ":20-21, no existence check" claim that H8_DuplicateSceneRootAudit.cs:24 makes is
    /// stale - the GameObject/AddComponent pair moved to :75-76 and is now conditional. The unconditional
    /// EditorSceneManager.SaveScene callers that cemented the nine (H8_SceneCleaner.cs:47,
    /// HectonVisualsConfigurator.cs:107, Rescue02_Final.cs:140-141, Hecton020Fixer.cs:63-66) have not
    /// been repaired, so the refusal stays: if the scene is dirty immediately AFTER opening and before
    /// this tool touches anything, saving would cement somebody else's injection along with the one-byte
    /// fix, and the write is refused unless -h8AllowDirtyScatterOwnerScene is passed deliberately.
    ///
    /// SERIALIZATION FORMAT IS REPORTED, NOT ASSUMED. 02_HECTON_WORLD.unity is currently a BINARY
    /// serialized file while ProjectSettings/EditorSettings.asset carries m_SerializationMode: 2
    /// (ForceText), so the first save through the asset pipeline rewrites it as YAML. That is a large
    /// but wanted consequence - a GUID or type-name grep returns zero for everything inside the binary
    /// file whether a component is present or not, which is how this component was previously reported
    /// as absent - and it must not arrive as a surprise. This tool reads the first bytes of the file
    /// before and after the save and states the format both times instead of predicting it.
    ///
    /// WHAT THIS FIX DOES NOT UNBLOCK. The enabled flag gates the RUNTIME route only. The editor-side
    /// generator, WorldProceduralScatterPreviewBuilder.cs:15-35, calls director.RebuildScatterPreview()
    /// directly, and an explicit call on a disabled MonoBehaviour still executes - so that path was never
    /// blocked by this flag and is not repaired by flipping it. It has a separate first-run blocker:
    /// _memory (WorldProceduralScatterDirector.cs:561) has no inline initializer and is created only by
    /// EnsureWorkingMemory, which runs from Awake (:733) and OnEnable (:764). The class carries no
    /// ExecuteAlways, so in edit mode neither fires, and the preview path reaches ResetPlacementGrid
    /// (:3972-3982) whose :3981 dereferences _memory with no guard - ahead of the _memory == null check
    /// at WorldProceduralScatterDirectorSamplingPipeline.cs:87, which therefore cannot catch it. That is a
    /// one-line fix in Hecton8.Core, not here.
    ///
    /// WHAT IT WILL NOT DO. It never activates a GameObject. A component on an inactive object runs no
    /// Awake and no OnEnable however enabled it is, so the fix is incomplete in that case and this
    /// reports STILL INERT with the exact ancestor that breaks the chain. Activating a subtree is the
    /// decision H8_PlacementOwnerEnabledAudit.cs:150-162 declines and H8_WorldRootGraveyardRepair owns
    /// for the one root where the cause is known. Guessing it here would resurrect content an author
    /// switched off.
    ///
    /// FIRST 20 MINUTES: serves `First exit` and `Resource`. A disabled placement owner is why the
    /// photic route has no alien biota and no readable resource objects to swim to; the 225 prefabs it
    /// distributes already exist on disk.
    ///
    /// QUALITY TIERS: none. This flips one serialized bool. Density, cadence and fidelity stay owned by
    /// the director's own continuous GlobalQualityWeight path, so Low/Middle/High/Ultra behaviour is
    /// unchanged except that a tier now has something to scale.
    ///
    /// USAGE
    ///   Menu: Hecton8/Authoring/Scatter Placement Owner - REPORT ONLY
    ///         Hecton8/Authoring/Scatter Placement Owner - ENABLE AND SAVE
    ///   Batchmode (reports by default):
    ///     Unity.exe -batchmode -quit -projectPath . -logFile Logs/scatterowner.log \
    ///       -executeMethod Hecton8.EditorTools.Authoring.H8_ScatterPlacementOwnerEnableAuthoring.Run \
    ///       [-h8ScatterOwnerScene Assets/_Project/Scenes/02_HECTON_WORLD.unity] \
    ///       [-h8ApplyScatterOwnerEnable] [-h8AllowDirtyScatterOwnerScene]
    /// </summary>
    public static class H8_ScatterPlacementOwnerEnableAuthoring
    {
        private const string Marker = "[H8_SCATTEROWNER]";
        private const string DefaultScenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
        private const string ApplyFlag = "-h8ApplyScatterOwnerEnable";
        private const string SceneFlag = "-h8ScatterOwnerScene";
        private const string AllowDirtyFlag = "-h8AllowDirtyScatterOwnerScene";
        private const string UndoLabel = "Enable world scatter placement owner";
        private const string ReportMenuPath = "Hecton8/Authoring/Scatter Placement Owner - REPORT ONLY";
        private const string ApplyMenuPath = "Hecton8/Authoring/Scatter Placement Owner - ENABLE AND SAVE";

        /// <summary>Bytes of the YAML preamble Unity writes at the head of a text scene.</summary>
        private static readonly byte[] TextSceneSignature = { 0x25, 0x59, 0x41, 0x4D, 0x4C };

        private sealed class OwnerSighting
        {
            public WorldProceduralScatterDirector Director;
            public string ObjectPath;
            public bool ComponentEnabled;
            public bool ActiveInHierarchy;
            public string InactiveAncestor;
            public int PlacementCeiling;
        }

        /// <summary>
        /// Reports and changes nothing. Always run this first - the scene may still be binary, in which
        /// case there is no diff to inspect after a write.
        /// </summary>
        // Priority sweep of all 55 [MenuItem("Hecton8/Authoring/...")] entries under Assets/,
        // 2026-07-29: the submenu's claimed range starts at 168, and inside 168..177 the only free
        // slots are 169, 171, 173 and 174, so this pair takes the adjacent 173/174. Unity inserts a
        // separator whenever consecutive priorities differ by more than 10, so staying inside the
        // claimed band is what keeps this pair in the same visual group as the rest of the world
        // authoring tools. 175 and 176 are NOT free - WorldProceduralFloraTextureAuthoring.cs:51 holds 175 and
        // WorldProceduralFloraMaterialAuthoring.cs:39 holds 176. 180/181 are taken by
        // WorldProceduralScatterPreviewBuilder.cs:17,37 - the editor-side generator this repair is a
        // prerequisite for - and colliding with it would shuffle the two into an arbitrary order.
        [MenuItem(ReportMenuPath, priority = 173)]
        public static void ReportOnly() => Execute(DefaultScenePath, false, false);

        /// <summary>
        /// Enables the owner and saves the scene. A separate menu entry from the report on purpose: a
        /// production scene write must not be one misclick away from a diagnostic.
        /// </summary>
        [MenuItem(ApplyMenuPath, priority = 174)]
        public static void ApplyAndSave() => Execute(DefaultScenePath, true, false);

        /// <summary>
        /// Batchmode entry point. Reports by default; pass -h8ApplyScatterOwnerEnable to write.
        /// </summary>
        public static void Run()
        {
            string scenePath = DefaultScenePath;
            bool apply = false;
            bool allowDirtyOpen = false;

            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], ApplyFlag, System.StringComparison.Ordinal))
                {
                    apply = true;
                    continue;
                }

                if (string.Equals(args[i], AllowDirtyFlag, System.StringComparison.Ordinal))
                {
                    allowDirtyOpen = true;
                    continue;
                }

                if (!string.Equals(args[i], SceneFlag, System.StringComparison.Ordinal))
                    continue;

                if (i + 1 >= args.Length)
                {
                    Debug.LogError($"{Marker} REFUSED - {SceneFlag} was passed with no scene path after it.");
                    return;
                }

                scenePath = args[i + 1];
                i++;
            }

            Execute(scenePath, apply, allowDirtyOpen);
        }

        private static void Execute(string scenePath, bool apply, bool allowDirtyOpen)
        {
            if (string.IsNullOrEmpty(scenePath))
            {
                Debug.LogError($"{Marker} REFUSED - empty scene path.");
                return;
            }

            // The dirty preflight runs BEFORE the self-test, not after. The self-test builds a throwaway
            // GameObject hierarchy in whatever scene is currently open; ordering it first would risk the
            // preflight refusing on this tool's own scaffold. Anything the scaffold leaves behind is
            // discarded a few lines later by OpenScene(Single), which is why that order is safe.
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                Scene loaded = EditorSceneManager.GetSceneAt(i);
                if (!loaded.isDirty)
                    continue;

                Debug.LogError(
                    $"{Marker} REFUSED - scene '{loaded.name}' has unsaved changes. Opening the target " +
                    "scene would discard them. Save or discard first, then re-run.");
                return;
            }

            if (!SelfTestPassed())
                return;

            string formatBeforeOpen = DescribeSceneFileFormat(scenePath);
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError($"{Marker} REFUSED - could not open '{scenePath}'.");
                return;
            }

            bool dirtyOnOpen = scene.isDirty;
            Debug.Log(
                $"{Marker} scene='{scene.name}' path='{scenePath}' onDiskFormat={formatBeforeOpen} " +
                $"editorSerializationMode={EditorSettings.serializationMode} dirtyImmediatelyAfterOpen={dirtyOnOpen}");

            ReportRuleInventory(scene);

            List<OwnerSighting> sightings = CollectOwners(scene);
            ReportSightings(scene, sightings);

            if (sightings.Count == 0)
            {
                Debug.LogError(
                    $"{Marker} NO PLACEMENT OWNER in '{scene.name}'. Nothing to enable. Add one with " +
                    "Assets/_Project/Scripts/Editor/WorldRuntimeBootstrapAuthoring.cs before repairing.");
                return;
            }

            if (sightings.Count > 1)
            {
                Debug.LogError(
                    $"{Marker} REFUSED - {sightings.Count} WorldProceduralScatterDirector components in " +
                    "one scene. Only one may own placement (s_activeRuntimeInstance is a single static " +
                    "slot, WorldProceduralScatterDirector.cs:30). Which one is authoritative is not a " +
                    "call this tool can make; resolve the duplicates first.");
                return;
            }

            OwnerSighting sighting = sightings[0];
            if (sighting.ComponentEnabled)
            {
                Debug.Log($"{Marker} NOTHING TO DO - the placement owner is already enabled.");
                return;
            }

            if (!apply)
            {
                Debug.Log(
                    $"{Marker} REPORT ONLY - would set enabled=true on '{sighting.ObjectPath}', restoring " +
                    $"up to {sighting.PlacementCeiling} placements per scatter window, and save the scene. " +
                    $"Re-run the APPLY entry, or pass {ApplyFlag} in batchmode, to write.");
                return;
            }

            if (dirtyOnOpen && !allowDirtyOpen)
            {
                Debug.LogError(
                    $"{Marker} REFUSED - '{scene.name}' was already dirty immediately after opening, " +
                    "before this tool touched anything, so something injected scene content during load. " +
                    "Saving now would cement that injection alongside the one-byte fix. Identify it with " +
                    "Hecton8/Authoring/Duplicate Scene Roots - REPORT ONLY, then re-run with " +
                    $"{AllowDirtyFlag} if the extra content is genuinely wanted on disk.");
                return;
            }

            Undo.RecordObject(sighting.Director, UndoLabel);
            sighting.Director.enabled = true;

            if (!sighting.Director.enabled)
            {
                Debug.LogError(
                    $"{Marker} REFUSED - setting enabled=true on '{sighting.ObjectPath}' did not take. " +
                    "Nothing was saved. The component is in a state Unity will not enable; inspect it by " +
                    "hand.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            string formatAfterSave = DescribeSceneFileFormat(scenePath);

            if (!saved)
            {
                Debug.LogError(
                    $"{Marker} SaveScene returned false for '{scenePath}'. The change is in memory only. " +
                    "Do not assume it is on disk.");
                return;
            }

            Debug.Log(
                $"{Marker} APPLIED AND SAVED - '{sighting.ObjectPath}' enabled=true, up to " +
                $"{sighting.PlacementCeiling} placements per scatter window restored. onDiskFormat " +
                $"{formatBeforeOpen} -> {formatAfterSave}.",
                sighting.Director);

            if (!sighting.ActiveInHierarchy)
            {
                Debug.LogError(
                    $"{Marker} STILL INERT - the component is now enabled and on disk, but its GameObject " +
                    $"is INACTIVE because ancestor '{sighting.InactiveAncestor}' is switched off, so Unity " +
                    "runs no Awake and no OnEnable on it. Activating a subtree is an authoring decision " +
                    "this tool will not make; for the known '--- WORLD ---' case use " +
                    "Hecton8/Authoring/World Root Graveyard Repair - APPLY AND SAVE.");
                return;
            }

            Debug.LogWarning(
                $"{Marker} STATIC CHANGE ONLY - the serialized flag is fixed. That is not proof the " +
                "director places anything. Enter Play Mode on this scene and confirm the placement counts " +
                "on the director, or the result stays PENDING VERIFICATION.");
        }

        private static void ReportRuleInventory(Scene scene)
        {
            var fillDirectors = new List<WorldProceduralFillDirector>();
            CollectFromScene(scene, fillDirectors);

            if (fillDirectors.Count == 0)
            {
                Debug.LogError(
                    $"{Marker} NO WorldProceduralFillDirector in '{scene.name}'. The scatter owner reads " +
                    "its rules from that component (WorldProceduralScatterDirector.cs:1270-1272), so it " +
                    "would place nothing even fully enabled. That is a SECOND, independent defect.");
                return;
            }

            for (int i = 0; i < fillDirectors.Count; i++)
            {
                WorldProceduralFillDirector fill = fillDirectors[i];
                int ruleCount = fill.Rules == null ? 0 : fill.Rules.Count;
                int familyCount = fill.Families == null ? 0 : fill.Families.Count;
                CountFamilyPrefabs(fill, out int variantCount, out int resolvablePrefabCount);

                string line =
                    $"{Marker} RULE SOURCE {fill.gameObject.name} enabled={(fill.enabled ? "1" : "0")} " +
                    $"rules={ruleCount} families={familyCount} variants={variantCount} " +
                    $"resolvablePrefabs={resolvablePrefabCount}";

                if (ruleCount == 0 || resolvablePrefabCount == 0)
                {
                    Debug.LogError(
                        line + " - EMPTY. This is a SECOND defect independent of the enabled flag. " +
                        "Repopulate with WorldRuntimeBootstrapAuthoring.ConfigureProceduralFill " +
                        "(:1379-1385), which loads Assets/_Project/Data/World/ProceduralPlacementRules " +
                        "and Assets/_Project/Data/World/ProceduralFamilies.",
                        fill);
                    continue;
                }

                Debug.Log(line + " - populated, so an empty inventory is NOT a cause here.", fill);
            }
        }

        private static void CountFamilyPrefabs(
            WorldProceduralFillDirector fill,
            out int variantCount,
            out int resolvablePrefabCount)
        {
            variantCount = 0;
            resolvablePrefabCount = 0;
            IReadOnlyList<WorldPrefabFamilyProfile> families = fill.Families;
            if (families == null)
                return;

            for (int i = 0; i < families.Count; i++)
            {
                WorldPrefabFamilyProfile family = families[i];
                if (family == null || family.variants == null)
                    continue;

                for (int v = 0; v < family.variants.Length; v++)
                {
                    WorldPrefabFamilyProfile.VariantEntry variant = family.variants[v];
                    if (variant == null)
                        continue;

                    variantCount++;
                    if (variant.prefab != null)
                        resolvablePrefabCount++;
                }
            }
        }

        private static List<OwnerSighting> CollectOwners(Scene scene)
        {
            var directors = new List<WorldProceduralScatterDirector>();
            CollectFromScene(scene, directors);

            var sightings = new List<OwnerSighting>(directors.Count);
            for (int i = 0; i < directors.Count; i++)
            {
                WorldProceduralScatterDirector director = directors[i];
                sightings.Add(new OwnerSighting
                {
                    Director = director,
                    ObjectPath = BuildTransformPath(director.transform),
                    ComponentEnabled = director.enabled,
                    ActiveInHierarchy = director.gameObject.activeInHierarchy,
                    InactiveAncestor = FindFirstInactiveAncestorName(director.transform),
                    PlacementCeiling = director.AuthoredScatterWindowPlacementCeiling,
                });
            }

            return sightings;
        }

        private static void ReportSightings(Scene scene, List<OwnerSighting> sightings)
        {
            var line = new StringBuilder();
            for (int i = 0; i < sightings.Count; i++)
            {
                OwnerSighting sighting = sightings[i];
                line.Length = 0;
                line.Append(Marker);
                line.Append(" OWNER ");
                line.Append(scene.name);
                line.Append(' ');
                line.Append(sighting.ObjectPath);
                line.Append("  componentEnabled=");
                line.Append(sighting.ComponentEnabled ? "1" : "0");
                line.Append("  activeInHierarchy=");
                line.Append(sighting.ActiveInHierarchy ? "1" : "0");
                line.Append("  firstInactiveAncestor=");
                line.Append(sighting.InactiveAncestor.Length == 0 ? "<none>" : sighting.InactiveAncestor);
                line.Append("  authoredPlacementsPerWindow=");
                line.Append(sighting.PlacementCeiling);
                Debug.Log(line.ToString(), sighting.Director);
            }
        }

        /// <summary>
        /// Every component of type T in the scene, inactive objects included.
        ///
        /// The List overload of GameObject.GetComponentsInChildren CLEARS the list it is handed before
        /// filling it, so calling it once per scene root into one shared list silently keeps only the
        /// LAST root's hits. In this scene the directors live under `[MANAGERS]`, which is not the last
        /// root, so that mistake reports zero owners and reads exactly like "the component is absent" -
        /// the same false negative a GUID grep produces against the binary scene file. Hence a scratch
        /// list per root and an explicit accumulate.
        /// </summary>
        private static void CollectFromScene<T>(Scene scene, List<T> destination) where T : Component
        {
            destination.Clear();
            var scratch = new List<T>(8);
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                roots[i].GetComponentsInChildren(true, scratch);
                for (int j = 0; j < scratch.Count; j++)
                    destination.Add(scratch[j]);
            }
        }

        private static string BuildTransformPath(Transform transform)
        {
            var path = new StringBuilder(transform.name);
            Transform parent = transform.parent;
            while (parent != null)
            {
                path.Insert(0, '/');
                path.Insert(0, parent.name);
                parent = parent.parent;
            }

            return path.ToString();
        }

        /// <summary>
        /// Name of the nearest transform at or above <paramref name="transform"/> whose own activeSelf
        /// is false, or an empty string when the whole chain is on. Self counts first, because a
        /// component on a self-disabled object is inert for the same reason as one under a disabled
        /// parent and the reader needs the closest cause, not the highest one.
        /// </summary>
        private static string FindFirstInactiveAncestorName(Transform transform)
        {
            Transform current = transform;
            while (current != null)
            {
                if (!current.gameObject.activeSelf)
                    return current.name;

                current = current.parent;
            }

            return string.Empty;
        }

        private static string DescribeSceneFileFormat(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string filePath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(filePath))
                return "absent";

            byte[] head = new byte[TextSceneSignature.Length];
            int read;
            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                read = stream.Read(head, 0, head.Length);
            }

            if (read < TextSceneSignature.Length)
                return "truncated";

            for (int i = 0; i < TextSceneSignature.Length; i++)
            {
                if (head[i] != TextSceneSignature[i])
                    return "binary";
            }

            return "text";
        }

        /// <summary>
        /// Known-answer cases for the one non-trivial pure function in this file, run before anything is
        /// printed or written. The scaffold is HideAndDontSave so no later save in this session can
        /// serialize it into a production scene - the mistake that put nine H8_PlayModeScreenshotter
        /// roots into 02_HECTON_WORLD (H8_DuplicateSceneRootAudit.cs:24-39) - and it is destroyed before
        /// the target scene is opened.
        /// </summary>
        private static bool SelfTestPassed()
        {
            GameObject root = null;
            GameObject mid = null;
            GameObject leaf = null;
            try
            {
                root = new GameObject("H8SelfTestRoot") { hideFlags = HideFlags.HideAndDontSave };
                mid = new GameObject("H8SelfTestMid") { hideFlags = HideFlags.HideAndDontSave };
                leaf = new GameObject("H8SelfTestLeaf") { hideFlags = HideFlags.HideAndDontSave };
                mid.transform.SetParent(root.transform, false);
                leaf.transform.SetParent(mid.transform, false);

                if (!ExpectAncestor(leaf.transform, string.Empty, "all-active chain"))
                    return false;

                mid.SetActive(false);
                if (!ExpectAncestor(leaf.transform, mid.name, "disabled parent"))
                    return false;

                mid.SetActive(true);
                leaf.SetActive(false);
                if (!ExpectAncestor(leaf.transform, leaf.name, "self disabled wins over ancestors"))
                    return false;

                return true;
            }
            finally
            {
                if (leaf != null)
                    UnityEngine.Object.DestroyImmediate(leaf);
                if (mid != null)
                    UnityEngine.Object.DestroyImmediate(mid);
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static bool ExpectAncestor(Transform transform, string expected, string caseName)
        {
            string actual = FindFirstInactiveAncestorName(transform);
            if (string.Equals(actual, expected, System.StringComparison.Ordinal))
                return true;

            Debug.LogError(
                $"{Marker} SELF-TEST FAILED case '{caseName}' expected '{expected}' got '{actual}'. " +
                "Report and repair suppressed - a tool that cannot compute its own diagnosis must not " +
                "write to a production scene.");
            return false;
        }
    }
}
#endif
