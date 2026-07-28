#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools.Authoring
{
    /// <summary>
    /// Lifts the authored world root back out of DEPRECATED_STUFF and switches it on.
    ///
    /// WHAT PUT IT THERE. `Assets/_Project/Editor/H8_SceneCleaner.cs` opens
    /// 02_HECTON_WORLD and, for every scene ROOT whose uppercased name does not contain one of
    /// TERRAIN / CAMERA / PLAYER / LIGHT / OCEAN / WATER / SUN / SKY / ATMOSPHERE / SYSTEM / MANAGER /
    /// DIRECTOR / REGISTRY / BOOTSTRAP, calls `SetParent(deprecatedParent)` and `SetActive(false)`
    /// (:41-42), then `EditorSceneManager.SaveScene(scene)` (:47) and `EditorApplication.Exit(0)`.
    /// Its own comments say why: ":36 Keep Camera / Player / Light / Ocean for visual proof" and
    /// ":38 Keep core systems so the game doesn't crash on load". It is a screenshot convenience tool
    /// that was pointed at the production scene. `[MANAGERS]` contains "MANAGER" so every director
    /// survived; `--- WORLD ---` contains none of the tokens, so the entire authored world was
    /// switched off and the save made it permanent. There is no inverse tool, and the cleaner has no
    /// [MenuItem] - it is -executeMethod only.
    ///
    /// WHY THAT IS THE WHOLE STORY. The surviving managers are present, registered and correct, which
    /// is exactly why code review of this project reads clean; everything they were built to operate on
    /// is inactive one level above them. Measured consequence in Logs/omega_route20.log: the boot
    /// reaches "Step 7: Player Spawn", HectonPlayerSpawner waits on terrain readiness, the readiness
    /// gate needs `HectonMapMagicVegetationBridge.ActiveRuntimeInstance`
    /// (WorldRuntimeReferenceUtility.cs:435-437), Unity runs no OnEnable inside an inactive subtree so
    /// that static is never published, the per-step no-progress deadline cancels the activation, and
    /// GameBootstrapper.cs:7365 ActivatePlayer() never runs. Eight Required Route rows are unreachable
    /// rather than broken.
    ///
    /// WHY NOT THE REBUILD MENU ITEMS. WorldRuntimeBootstrapAuthoring.cs:1119-1121 reuses an existing
    /// root via `GameObject.Find(WorldRootName)`, and GameObject.Find returns only ACTIVE objects. With
    /// the authored root disabled it finds nothing and creates a SECOND, active `--- WORLD ---` beside
    /// the graveyard copy - in a binary scene that cannot be diffed. FabricationBootstrapAuthoring.cs
    /// :331-341 has the same defect and additionally leaves an orphan Fabrication_Outpost at scene root,
    /// because :339 reparents only `if (parent != null)`. ResourceWorldBootstrapAuthoring.cs:208-211
    /// likewise. Note the asymmetry that makes this precise: EnsureChild
    /// (WorldRuntimeBootstrapAuthoring.cs:3036-3038) uses `parent.Find(childName)`, and Transform.Find
    /// DOES see inactive children - so child reuse works and root reuse does not. The obvious repair is
    /// the destructive one.
    ///
    /// WHY THIS IS NARROW ON PURPOSE. The cleaner only ever touched ROOTS: it reparented and disabled
    /// them and left their descendants' own active flags alone. So restoring one root to the scene root
    /// with SetActive(true) reproduces the pre-cleaner state for that subtree exactly, and does not
    /// invent a new one. A blanket re-enable of DEPRECATED_STUFF would instead resurrect anything an
    /// author had deliberately switched off before the cleaner ever ran, which is a different and
    /// unrecoverable decision. This tool moves ONE object and refuses every ambiguous case.
    ///
    /// This mirrors the boundary H8_PlacementOwnerEnabledAudit.cs:150-162 already draws, which declines
    /// to activate an inactive GameObject and calls that "an authoring decision this tool will not make
    /// for you". The difference here is that the decision has now been made, with the cause identified.
    /// </summary>
    public static class H8_WorldRootGraveyardRepair
    {
        private const string Marker = "[H8_WORLDROOTREPAIR]";
        private const string WorldScenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
        private const string GraveyardRootName = "DEPRECATED_STUFF";
        private const string WorldRootName = "--- WORLD ---";

        /// <summary>
        /// Reports what a repair would do and changes nothing. Always run this first: the scene is
        /// binary, so there is no diff to inspect afterwards.
        /// </summary>
        [MenuItem("Hecton8/Authoring/World Root Graveyard Repair - REPORT ONLY", priority = 178)]
        public static void ReportOnly() => Execute(false);

        /// <summary>
        /// Performs the repair and saves the scene. Separate menu entry from the report on purpose -
        /// a binary production scene write should never be one misclick away from a diagnostic.
        /// </summary>
        [MenuItem("Hecton8/Authoring/World Root Graveyard Repair - APPLY AND SAVE", priority = 179)]
        public static void ApplyAndSave() => Execute(true);

        /// <summary>
        /// Batchmode entry point. Reports by default; pass -h8ApplyWorldRootRepair to write.
        /// </summary>
        public static void Run()
        {
            bool apply = false;
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-h8ApplyWorldRootRepair", System.StringComparison.Ordinal))
                {
                    apply = true;
                    break;
                }
            }

            Execute(apply);
        }

        private static void Execute(bool apply)
        {
            // OpenScene(Single) silently discards unsaved in-memory work, and
            // H8_PlacementOwnerEnabledAudit deliberately leaves exactly that kind of change behind -
            // it marks the scene dirty and never saves. Refusing is not paranoia here.
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                Scene loaded = EditorSceneManager.GetSceneAt(i);
                if (!loaded.isDirty)
                    continue;

                Debug.LogError(
                    $"{Marker} REFUSED - scene '{loaded.name}' has unsaved changes. Opening the world " +
                    "scene would discard them. Save or discard first, then re-run.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError($"{Marker} REFUSED - could not open '{WorldScenePath}'.");
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();

            GameObject graveyard = null;
            var activeWorldRoots = new List<GameObject>();
            var inactiveWorldRoots = new List<GameObject>();

            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (string.Equals(root.name, GraveyardRootName, System.StringComparison.Ordinal))
                {
                    graveyard = root;
                    continue;
                }

                if (!string.Equals(root.name, WorldRootName, System.StringComparison.Ordinal))
                    continue;

                if (root.activeSelf)
                    activeWorldRoots.Add(root);
                else
                    inactiveWorldRoots.Add(root);
            }

            Debug.Log(
                $"{Marker} scene='{scene.name}' roots={roots.Length} graveyard={(graveyard != null ? "present" : "absent")} " +
                $"worldRootsAtSceneRoot=active:{activeWorldRoots.Count}/inactive:{inactiveWorldRoots.Count}");

            // A duplicate means somebody already ran one of the three Rebuild menu items after the
            // cleaner, so a second active world root is on disk. Reparenting the buried one beside it
            // would give two rival worlds, and the duplicate carries only bare Transforms because
            // EnsureRoutePath (WorldRuntimeBootstrapAuthoring.cs:1131-1145) creates no components.
            // Which of the two is authoritative is not a call this tool can make.
            if (activeWorldRoots.Count > 0)
            {
                Debug.LogError(
                    $"{Marker} REFUSED - an ACTIVE root named '{WorldRootName}' already exists at scene " +
                    "root. A Rebuild authoring menu item has run since the cleaner and created a " +
                    "duplicate. Resolve which root is authoritative by hand before repairing; this tool " +
                    "will not merge two world roots.");
                return;
            }

            if (graveyard == null)
            {
                Debug.Log(
                    $"{Marker} NOTHING TO DO - no '{GraveyardRootName}' root in this scene. Either the " +
                    "cleaner never ran here or a repair already happened.");
                return;
            }

            Transform buried = graveyard.transform.Find(WorldRootName);
            if (buried == null)
            {
                // Transform.Find DOES see inactive children, so a null here is a real absence and not
                // the GameObject.Find blind spot that caused the duplication bug in the first place.
                Debug.LogError(
                    $"{Marker} REFUSED - '{GraveyardRootName}' exists but holds no direct child named " +
                    $"'{WorldRootName}'. Direct children present: {DescribeChildren(graveyard.transform)}. " +
                    "Run Hecton8/Diagnostics on the scene root activation audit before going further.");
                return;
            }

            int descendants = buried.GetComponentsInChildren<Transform>(true).Length - 1;
            Debug.Log(
                $"{Marker} FOUND '{WorldRootName}' buried under '{GraveyardRootName}': " +
                $"activeSelf={buried.gameObject.activeSelf} directChildren={buried.childCount} " +
                $"descendants={descendants}");

            if (!apply)
            {
                Debug.Log(
                    $"{Marker} REPORT ONLY - would reparent '{WorldRootName}' to scene root and set it " +
                    "active, touching nothing else in the graveyard. Re-run the APPLY entry, or pass " +
                    "-h8ApplyWorldRootRepair in batchmode, to write.");
                return;
            }

            // Undo.SetTransformParent performs the reparent AND records it. Calling Transform.SetParent
            // afterwards would be a second, differently-behaved move rather than a safeguard.
            Undo.SetTransformParent(buried, null, $"{Marker} lift world root out of graveyard");
            Undo.RecordObject(buried.gameObject, $"{Marker} activate world root");
            buried.gameObject.SetActive(true);

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);

            Debug.Log(
                $"{Marker} {(saved ? "APPLIED AND SAVED" : "APPLIED BUT SAVE FAILED")} - '{WorldRootName}' " +
                $"is now a scene root with activeSelf={buried.gameObject.activeSelf}, {descendants} " +
                $"descendants restored to the active states they carried before the cleaner ran. " +
                $"'{GraveyardRootName}' was not otherwise modified.");

            if (!saved)
            {
                Debug.LogError(
                    $"{Marker} SaveScene returned false. The change is in memory only. Do not assume it " +
                    "is on disk.");
            }
        }

        private static string DescribeChildren(Transform parent)
        {
            if (parent.childCount == 0)
                return "<none>";

            var names = new System.Text.StringBuilder();
            for (int i = 0; i < parent.childCount; i++)
            {
                if (i > 0)
                    names.Append(", ");

                names.Append('\'').Append(parent.GetChild(i).name).Append('\'');
            }

            return names.ToString();
        }
    }
}
#endif
