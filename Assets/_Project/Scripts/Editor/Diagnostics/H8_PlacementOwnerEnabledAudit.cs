using System.Collections.Generic;
using System.Text;
using Hecton8.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools.Diagnostics
{
    /// <summary>
    /// Audits, and on a second deliberate click repairs, the enabled state of the procedural world
    /// placement owner in the scenes that are currently open.
    ///
    /// WHY THIS EXISTS. A scene census run on 2026-07-27 found WorldProceduralScatterDirector present
    /// exactly once in 02_HECTON_WORLD, on [MANAGERS]/WorldGen, with the GameObject ACTIVE and the
    /// COMPONENT DISABLED. Every registration that director owns runs from OnEnable
    /// (Assets/_Project/Scripts/WorldProceduralScatterDirector.cs:757-777), and Unity never calls
    /// OnEnable on a disabled component. So it registers nothing, ticks nothing and places nothing,
    /// while reading as completely correct in code review. Nothing in the project sets its .enabled
    /// to true at runtime, and the authoring tool that builds this stack
    /// (Assets/_Project/Scripts/Editor/WorldRuntimeBootstrapAuthoring.cs:120 and :685-728) resolves
    /// the component with GetOrAddComponent and rewrites its serialized fields but never touches
    /// m_Enabled, so re-running that tool cannot repair a component that is already there and off.
    ///
    /// WHAT IT MEASURES: the loaded scene graph, including inactive GameObjects. It is format
    /// agnostic, so the fact that 02_HECTON_WORLD.unity is serialized as binary does not matter -
    /// unlike a GUID or type-name text grep, which returns zero for everything in that file whether
    /// the component is there or not.
    ///
    /// WHAT IT DOES NOT MEASURE: runtime composition. A director added with AddComponent at runtime,
    /// or living on a DontDestroyOnLoad object, is invisible here. For that half use
    /// H8_HeadlessPlayModeProbe. Neither substitutes for the other.
    ///
    /// WHY IT NEVER SAVES. AGENTS.md forbids automated runners and scripts from calling
    /// EditorSceneManager.SaveScene, PrefabUtility.SaveAsPrefabAsset or EditorUtility.SetDirty on
    /// production assets, so that no automated pass can wipe authored work. The repair here is a
    /// MenuItem a human invokes on purpose, it is recorded with Undo so it can be reverted with
    /// Ctrl+Z, and it marks the scene modified so the normal save prompt takes over. It never writes
    /// the .unity file. Until you press Ctrl+S yourself, nothing on disk has changed.
    /// </summary>
    public static class H8_PlacementOwnerEnabledAudit
    {
        private const string Marker = "[H8_PLACEMENT_OWNER]";
        private const string AuditMenuPath = "Hecton8/Diagnostics/World Placement Owner Audit";
        private const string RepairMenuPath = "Hecton8/Diagnostics/Enable Disabled World Placement Owners";
        private const string UndoLabel = "Enable world placement owner";

        /// <summary>
        /// Production authoring values written by WorldRuntimeBootstrapAuthoring.cs:716-722.
        /// </summary>
        private const int AuthoredRadiusCells = 7;
        private const int AuthoredGroundPlacementsPerCell = 2;
        private const int AuthoredClusterPlacementsPerCell = 1;
        private const int AuthoredStructureCellStride = 2;
        private const int AuthoredStructurePlacementsPerWindow = 1;
        private const int AuthoredSpawnCellStride = 3;
        private const int AuthoredSpawnPlacementsPerWindow = 1;

        /// <summary>
        /// 225 cells * (2 + 1) + ceil(15/2)^2 * 1 + ceil(15/3)^2 * 1 = 675 + 64 + 25.
        /// </summary>
        private const int AuthoredCeilingExpected = 764;

        /// <summary>
        /// Degenerate authoring: radius below the floor, budgets above and below their clamps, both
        /// strides below the floor. 5x5 cells * (4 + 0) + ceil(5/2)^2 * 2 + ceil(5/2)^2 * 0
        /// = 100 + 18 + 0.
        /// </summary>
        private const int DegenerateCeilingExpected = 118;

        private sealed class OwnerSighting
        {
            public WorldProceduralScatterDirector Director;
            public string ScenePath;
            public string SceneName;
            public string ObjectPath;
            public bool ComponentEnabled;
            public bool GameObjectActiveInHierarchy;
            public int PlacementCeiling;
        }

        [MenuItem(AuditMenuPath)]
        public static void Audit()
        {
            if (!SelfTestPassed())
                return;

            List<OwnerSighting> sightings = CollectOwners();
            ReportSightings(sightings);

            int disabled = CountDisabled(sightings);
            if (sightings.Count == 0)
            {
                Debug.LogWarning(
                    Marker + " NO PLACEMENT OWNER in any loaded scene. Open " +
                    "Assets/_Project/Scenes/02_HECTON_WORLD.unity and run this again. Absence here is " +
                    "evidence only for the scenes actually loaded.");
                return;
            }

            if (disabled == 0)
            {
                Debug.Log(Marker + " VERDICT every loaded placement owner is enabled. No repair needed.");
                return;
            }

            Debug.LogError(
                Marker + " VERDICT " + disabled + " placement owner(s) are authored but DISABLED. They " +
                "receive no OnEnable, register nothing and place nothing. Fix with the menu item: " +
                RepairMenuPath + ". Nothing on disk changes until you save the scene yourself.");
        }

        [MenuItem(RepairMenuPath)]
        public static void EnableDisabledOwners()
        {
            if (!SelfTestPassed())
                return;

            List<OwnerSighting> sightings = CollectOwners();
            if (sightings.Count == 0)
            {
                Debug.LogWarning(
                    Marker + " NOTHING TO REPAIR - no placement owner exists in any loaded scene. Open " +
                    "Assets/_Project/Scenes/02_HECTON_WORLD.unity first.");
                return;
            }

            int enabledCount = 0;
            int blockedByInactiveObject = 0;
            var touchedScenes = new HashSet<string>();

            for (int i = 0; i < sightings.Count; i++)
            {
                OwnerSighting sighting = sightings[i];
                if (sighting.ComponentEnabled)
                    continue;

                Undo.RecordObject(sighting.Director, UndoLabel);
                sighting.Director.enabled = true;
                enabledCount++;
                touchedScenes.Add(sighting.ScenePath);

                Debug.Log(
                    Marker + " ENABLED " + sighting.SceneName + " " + sighting.ObjectPath +
                    " - restores up to " + sighting.PlacementCeiling +
                    " placements per scatter window.",
                    sighting.Director);

                // An inactive GameObject still runs no Awake and no OnEnable, so enabling the
                // component alone does not make it live. Activating the object could undo a
                // deliberate authoring decision, so this reports and stops instead of guessing.
                if (!sighting.Director.gameObject.activeInHierarchy)
                {
                    blockedByInactiveObject++;
                    Debug.LogError(
                        Marker + " STILL INERT " + sighting.SceneName + " " + sighting.ObjectPath +
                        " - the component is now enabled but its GameObject is INACTIVE, so Unity still " +
                        "runs no Awake and no OnEnable on it. Activating the object is an authoring " +
                        "decision this tool will not make for you.",
                        sighting.Director);
                }
            }

            if (enabledCount == 0)
            {
                Debug.Log(Marker + " NO CHANGE - every loaded placement owner was already enabled.");
                return;
            }

            MarkTouchedScenes(touchedScenes);

            Debug.LogWarning(
                Marker + " IN-MEMORY ONLY: enabled " + enabledCount + " placement owner(s) across " +
                touchedScenes.Count + " scene(s), " + blockedByInactiveObject +
                " still blocked by an inactive GameObject. The change is recorded for Undo (Ctrl+Z) and " +
                "the scene is marked modified. This tool did NOT write the .unity file - press Ctrl+S to " +
                "commit it, or Ctrl+Z to discard it.");
        }

        private static void MarkTouchedScenes(HashSet<string> touchedScenes)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && touchedScenes.Contains(scene.path))
                    EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        private static List<OwnerSighting> CollectOwners()
        {
            var sightings = new List<OwnerSighting>();
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    Walk(roots[rootIndex].transform, string.Empty, scene, sightings);
                }
            }

            return sightings;
        }

        private static void Walk(
            Transform transform,
            string parentPath,
            Scene scene,
            List<OwnerSighting> sightings)
        {
            string path = parentPath.Length == 0 ? transform.name : parentPath + "/" + transform.name;

            if (transform.TryGetComponent(out WorldProceduralScatterDirector director))
            {
                sightings.Add(new OwnerSighting
                {
                    Director = director,
                    ScenePath = scene.path,
                    SceneName = scene.name,
                    ObjectPath = path,
                    ComponentEnabled = director.enabled,
                    GameObjectActiveInHierarchy = director.gameObject.activeInHierarchy,
                    PlacementCeiling = director.AuthoredScatterWindowPlacementCeiling,
                });
            }

            for (int i = 0; i < transform.childCount; i++)
                Walk(transform.GetChild(i), path, scene, sightings);
        }

        private static void ReportSightings(List<OwnerSighting> sightings)
        {
            var line = new StringBuilder();
            for (int i = 0; i < sightings.Count; i++)
            {
                OwnerSighting sighting = sightings[i];
                line.Length = 0;
                line.Append(Marker);
                line.Append(" OWNER ");
                line.Append(sighting.SceneName);
                line.Append(' ');
                line.Append(sighting.ObjectPath);
                line.Append("  componentEnabled=");
                line.Append(sighting.ComponentEnabled ? "1" : "0");
                line.Append("  gameObjectActive=");
                line.Append(sighting.GameObjectActiveInHierarchy ? "1" : "0");
                line.Append("  authoredPlacementsPerWindow=");
                line.Append(sighting.PlacementCeiling);
                Debug.Log(line.ToString(), sighting.Director);
            }
        }

        private static int CountDisabled(List<OwnerSighting> sightings)
        {
            int disabled = 0;
            for (int i = 0; i < sightings.Count; i++)
            {
                if (!sightings[i].ComponentEnabled)
                    disabled++;
            }

            return disabled;
        }

        /// <summary>
        /// Two known-answer cases for the placement-ceiling math, run before this instrument prints
        /// or changes anything. A tool that reports a number it cannot compute correctly is worse
        /// than no tool, so a failure here suppresses the whole run.
        /// </summary>
        private static bool SelfTestPassed()
        {
            int authored = WorldProceduralScatterDirector.CalculateAuthoredScatterWindowPlacementCeiling(
                AuthoredRadiusCells,
                AuthoredGroundPlacementsPerCell,
                AuthoredClusterPlacementsPerCell,
                AuthoredStructureCellStride,
                AuthoredStructurePlacementsPerWindow,
                AuthoredSpawnCellStride,
                AuthoredSpawnPlacementsPerWindow);
            if (authored != AuthoredCeilingExpected)
            {
                Debug.LogError(
                    Marker + " SELF-TEST FAILED authored case expected " + AuthoredCeilingExpected +
                    " got " + authored + ". Report suppressed.");
                return false;
            }

            int degenerate = WorldProceduralScatterDirector.CalculateAuthoredScatterWindowPlacementCeiling(
                0,
                99,
                -5,
                0,
                7,
                1,
                -3);
            if (degenerate != DegenerateCeilingExpected)
            {
                Debug.LogError(
                    Marker + " SELF-TEST FAILED clamp case expected " + DegenerateCeilingExpected +
                    " got " + degenerate + ". Report suppressed.");
                return false;
            }

            return true;
        }
    }
}
