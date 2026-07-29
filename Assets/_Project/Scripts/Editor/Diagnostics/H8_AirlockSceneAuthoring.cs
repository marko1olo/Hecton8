using System.Collections.Generic;
using System.Text;
using Hecton8.Core;
using Hecton8.Core.Generated;
using Hecton8.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools.Diagnostics
{
    /// <summary>
    /// Audits BaseAirlock reachability, authors the one missing airlock prefab asset, and instantiates
    /// it into an open scene. Three separate entry points on purpose: the audit is read-only, the
    /// prefab author writes exactly one new asset and refuses to overwrite, and the scene step never
    /// writes a .unity file at all.
    ///
    /// WHY THIS EXISTS. BaseAirlock (Assets/_Project/Scripts/Gameplay/BaseAirlock.cs:52) is a
    /// 12-interface MonoBehaviour with a fully wired event sidecar - BaseAirlockEvents is prewarmed at
    /// SystemDispatcher.cs:2090, flushed at SystemDispatcher.cs:5658, and reset at
    /// GameBootstrapper.cs:1790 - and its consumer is live: ProgressionRuntimeInstaller.cs:27-28
    /// AddComponents NarrativeProgressionBridge onto the player object from
    /// GameBootstrapper.cs:8107. That bridge's OnBaseAirlockEvent
    /// (Assets/_Project/Scripts/Progression/NarrativeProgressionBridge.cs:225-240) is the ONLY caller of
    /// TryIssueExitLifePodDiscoveryFromAup, which raises DiscoveryMade(first_hour_exit_lifepod) - the
    /// single hash that completes Quest_Arrival and Quest_FirstHour_ExitLifePod and triggers
    /// Quest_StarterDrill, Quest_CopperSample and Quest_FirstHour_CollectTitanium.
    ///
    /// The consumer half of that chain exists. The producer half does not: BaseAirlock's script GUID
    /// 6617cbca100e19646bb6299390f3c6e0 appears in zero .prefab and zero .asset files, and no
    /// AddComponent&lt;BaseAirlock&gt; call site exists anywhere in the project. So the airlock edge of
    /// the spine has never been able to fire. WaterColumnEntryNarrativeBridge.cs is a deliberate SECOND
    /// producer of the same hash added because of exactly this gap; it does not make the airlock edge
    /// work, and its own remarks block says so.
    ///
    /// WHY A PREFAB AND NOT SCENE SURGERY. The production scenes here are serialised as binary
    /// (m_SerializationMode: 2), so a scene-embedded airlock is not reviewable, not diffable and not
    /// greppable - a text search over a binary scene returns zero whether the component is present or
    /// not, and that exact false negative has already produced one retracted verdict on this project. A
    /// prefab asset is text YAML: reviewable, diffable, and re-instantiable into any scene. So this tool
    /// writes the prefab, and the scene instance is a separate deliberate step whose placement is an
    /// authoring decision it will not guess for you.
    ///
    /// WHAT THE AUDIT MEASURES: the loaded scene graph including inactive GameObjects, plus the prefab
    /// asset on disk. It is format agnostic, so binary scene serialisation does not blind it. WHAT IT
    /// DOES NOT MEASURE: runtime composition. Nothing added by AddComponent at play time is visible
    /// here, and no static read can prove the discovery actually reaches QuestStateManager - that needs
    /// a Play Mode or headless run.
    ///
    /// WHY THE PREFAB WRITE IS SAFE UNDER AGENTS.md. The Sandbox Firewall rule forbids automated
    /// scripts from calling PrefabUtility.SaveAsPrefabAsset on PRODUCTION assets, so no pass can wipe
    /// authored work. This tool saves only when the target path holds no asset at all; if anything is
    /// already there it reports and returns without touching it. Creating a file that does not exist
    /// overwrites nobody. It never calls EditorSceneManager.SaveScene and never calls
    /// EditorUtility.SetDirty on an existing asset.
    ///
    /// BATCHMODE CONTRACT: every entry point is a public static void with no arguments, usable with
    /// -executeMethod. No EditorUtility.DisplayDialog, no Selection, no EditorApplication.Exit, no
    /// [InitializeOnLoadMethod]. All three are idempotent. The prefab is constructed inside a preview
    /// scene so building it cannot dirty a scene a sibling agent has open.
    /// </summary>
    public static class H8_AirlockSceneAuthoring
    {
        private const string Marker = "[H8_AIRLOCK]";
        private const string AuditMenuPath = "Hecton8/Diagnostics/Airlock Reachability Audit";
        private const string AuthorMenuPath = "Hecton8/Diagnostics/Author Missing Airlock Prefab";
        private const string InstantiateMenuPath = "Hecton8/Diagnostics/Instantiate Airlock Into Open Scene";
        private const string UndoLabel = "Instantiate base airlock";

        /// <summary>
        /// Sits with its PFB_Module_* siblings, which is where the Editor prefab factories already emit.
        /// </summary>
        private const string PrefabPath = "Assets/_Project/Prefabs/Construction/Final/PFB_Module_Airlock.prefab";
        private const string PrefabParentFolder = "Assets/_Project/Prefabs/Construction/Final";
        private const string PrefabRootName = "PFB_Module_Airlock";
        private const string VisualChildName = "DoorPlate_Visual";
        private const string InteriorSpawnChildName = "InteriorSpawn";
        private const string ExteriorSpawnChildName = "ExteriorSpawn";

        /// <summary>
        /// Serialised field names on BaseAirlock. Private [SerializeField] members are only reachable
        /// through SerializedObject, which is the sanctioned path - hand-editing the prefab YAML is
        /// banned outright.
        /// </summary>
        private const string InteriorSpawnField = "interiorSpawnPoint";
        private const string ExteriorSpawnField = "exteriorSpawnPoint";
        private const string StatusLightField = "statusLightRenderer";
        private const string BulkheadWidthField = "emergencyBulkheadWidthMeters";
        private const string BulkheadHeightField = "emergencyBulkheadHeightMeters";

        /// <summary>
        /// Door plane defaults matching BaseAirlock.cs:142 and :145, so the collider the interaction
        /// raycast hits and the mathematical bulkhead plane KCC reads describe the same doorway.
        /// </summary>
        private const float DoorWidthMeters = 2.6f;
        private const float DoorHeightMeters = 3.2f;
        private const float DoorDepthMeters = 0.35f;

        /// <summary>
        /// Spawn poses in root-local space. The root's +Z is the outward bulkhead normal, because
        /// BaseAirlock.RefreshBulkheadPoseSnapshot (BaseAirlock.cs:1676-1711) publishes transform.forward
        /// as the door normal and transform.up as the plane up hint. Y sits just above the door sill at
        /// -DoorHeightMeters/2 so a teleported player lands on the threshold rather than inside the frame.
        /// </summary>
        private const float SpawnSillClearanceMeters = 0.05f;
        private const float InteriorSpawnDepthMeters = -1.6f;
        private const float ExteriorSpawnDepthMeters = 2.0f;

        /// <summary>
        /// Interaction requires a non-trigger collider on the Interactable layer: PlayerInteraction.cs:686-688
        /// queries InteractableRegistry.TryResolveSpatialTarget with HectonLayerMasks.InteractableLayerMask
        /// and QueryTriggerInteraction.Ignore, and InteractableRegistry.cs:281-282 drops any collider that
        /// is a trigger or off that mask. A visually perfect airlock on the Default layer is uninteractable.
        /// </summary>
        private static int InteractableLayer => HectonLayerMasks.Interactable;

        private sealed class AirlockSighting
        {
            public BaseAirlock Airlock;
            public string ScenePath;
            public string SceneName;
            public string ObjectPath;
            public bool ComponentEnabled;
            public bool GameObjectActiveInHierarchy;
            public bool OnInteractableLayer;
            public bool HasNonTriggerCollider;
            public bool HasInteriorSpawn;
            public bool HasExteriorSpawn;
        }

        [MenuItem(AuditMenuPath)]
        public static void AuditAirlockReachability()
        {
            if (!SelfTestPassed())
                return;

            uint discoveryHash = NarrativeEvents.ComputeDiscoveryHash(H8Hashes.Signals.FirstHourExitLifepodId);
            Debug.Log(
                Marker + " GATE discovery=" + H8Hashes.Signals.FirstHourExitLifepodId + " hash=" + discoveryHash +
                " producer=NarrativeProgressionBridge.OnBaseAirlockEvent requires " +
                "BaseAirlockEventType.EnvironmentChanged with the dry flag CLEAR, i.e. a cycle that ends " +
                "with BaseAirlock.IsPlayerInside == false.");

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefabAsset == null)
            {
                Debug.LogWarning(
                    Marker + " NO PREFAB at " + PrefabPath + ". Create it with the menu item: " + AuthorMenuPath);
            }
            else if (prefabAsset.GetComponent<BaseAirlock>() == null)
            {
                Debug.LogError(
                    Marker + " PREFAB PRESENT BUT INERT at " + PrefabPath +
                    " - the asset exists and carries no BaseAirlock. This tool will not overwrite it. " +
                    "Inspect it by hand.");
            }
            else
            {
                Debug.Log(Marker + " PREFAB OK " + PrefabPath + " carries BaseAirlock.");
            }

            List<AirlockSighting> sightings = CollectSightings(out int loadedSceneCount, out string spawnerAnchorReport);
            ReportSightings(sightings);
            Debug.Log(Marker + " ANCHOR " + spawnerAnchorReport);

            if (sightings.Count == 0)
            {
                Debug.LogWarning(
                    Marker + " NO AIRLOCK INSTANCE in any of the " + loadedSceneCount +
                    " loaded scene(s). The quest spine's airlock edge cannot fire. Absence here is " +
                    "evidence only for the scenes actually loaded - open " +
                    "Assets/_Project/Scenes/02_HECTON_WORLD.unity and run this again.");
                return;
            }

            int inertCount = CountInert(sightings);
            if (inertCount == 0)
            {
                Debug.Log(
                    Marker + " VERDICT " + sightings.Count +
                    " airlock instance(s) are structurally complete. Whether the discovery reaches " +
                    "QuestStateManager is NOT proven here - that needs a Play Mode or headless run.");
                return;
            }

            Debug.LogError(
                Marker + " VERDICT " + inertCount + " of " + sightings.Count +
                " airlock instance(s) are structurally incomplete and cannot raise the discovery. " +
                "Read the per-instance lines above: a missing spawn point makes " +
                "TryResolveTeleportDestination fail and StartCycle return before any event is raised " +
                "(BaseAirlock.cs:847-849, :914-945).");
        }

        [MenuItem(AuthorMenuPath)]
        public static void AuthorAirlockPrefab()
        {
            if (!SelfTestPassed())
                return;

            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                Debug.Log(
                    Marker + " ALREADY AUTHORED " + PrefabPath +
                    " - nothing written. This tool creates the asset only when the path is empty, so it " +
                    "can never overwrite authored work.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(PrefabParentFolder))
            {
                Debug.LogError(
                    Marker + " ABORT parent folder missing: " + PrefabParentFolder +
                    ". Creating project folders is an authoring decision this tool will not make.");
                return;
            }

            Scene previewScene = EditorSceneManager.NewPreviewScene();
            if (!previewScene.IsValid())
            {
                Debug.LogError(
                    Marker + " ABORT EditorSceneManager.NewPreviewScene returned an invalid scene. " +
                    "Refusing to build the prefab in a project scene, because that would leave a " +
                    "hierarchy change in a scene another agent may have open. Nothing was written.");
                return;
            }

            GameObject root = null;
            try
            {
                if (!TryResolveVisualPrimitive(previewScene, out Mesh doorMesh, out Material doorMaterial))
                {
                    Debug.LogError(
                        Marker + " ABORT could not resolve a built-in mesh/material for the door plate. " +
                        "Nothing was written.");
                    return;
                }

                root = BuildAirlockRoot(previewScene, doorMesh, doorMaterial);
                if (root == null)
                    return;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
                if (!success || saved == null)
                {
                    Debug.LogError(Marker + " SAVE FAILED PrefabUtility.SaveAsPrefabAsset returned false for " + PrefabPath);
                    return;
                }

                Debug.Log(
                    Marker + " AUTHORED " + PrefabPath + " root=" + PrefabRootName + " layer=" + InteractableLayer +
                    " door=" + DoorWidthMeters + "x" + DoorHeightMeters + "x" + DoorDepthMeters +
                    "m spawnPoints=" + InteriorSpawnChildName + "," + ExteriorSpawnChildName);

                VerifySavedPrefab();

                Debug.LogWarning(
                    Marker + " VISUAL PLACEHOLDER the door plate uses the built-in cube mesh and the " +
                    "pipeline default material. It is functionally correct and visually unfinished; " +
                    "replacing the mesh and material is the asset lane's work, not this tool's. Nothing " +
                    "about the event path depends on it.");

                Debug.LogWarning(
                    Marker + " KNOWN GAP first interaction cannot raise the discovery. BaseAirlock's " +
                    "_isPlayerInside is a plain private field (BaseAirlock.cs:179) with no [SerializeField], " +
                    "so no authoring pass can start it true. It begins false, the first cycle flips it to " +
                    "true (BaseAirlock.cs:1116) and the payload's dry flag is IsPlayerInside " +
                    "(BaseAirlockEvents.cs:466), which the bridge rejects. The SECOND cycle clears the flag " +
                    "and the discovery fires. Fixing that needs a serialised start-state field on " +
                    "BaseAirlock.cs, which this tool does not own.");
            }
            finally
            {
                if (root != null)
                    Object.DestroyImmediate(root);

                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        [MenuItem(InstantiateMenuPath)]
        public static void InstantiateAirlockIntoOpenScene()
        {
            if (!SelfTestPassed())
                return;

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefabAsset == null)
            {
                Debug.LogWarning(
                    Marker + " NOTHING TO INSTANTIATE - no prefab at " + PrefabPath + ". Run " +
                    AuthorMenuPath + " first.");
                return;
            }

            Scene target = SceneManager.GetActiveScene();
            if (!target.IsValid() || !target.isLoaded)
            {
                Debug.LogWarning(
                    Marker + " NO ACTIVE SCENE loaded, nothing instantiated. Open " +
                    "Assets/_Project/Scenes/02_HECTON_WORLD.unity first.");
                return;
            }

            List<AirlockSighting> sightings = CollectSightings(out _, out string spawnerAnchorReport);
            for (int i = 0; i < sightings.Count; i++)
            {
                if (sightings[i].ScenePath != target.path)
                    continue;

                Debug.Log(
                    Marker + " ALREADY PRESENT " + sightings[i].SceneName + " " + sightings[i].ObjectPath +
                    " - nothing instantiated. Delete that instance first if you meant to replace it.");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, target);
            if (instance == null)
            {
                Debug.LogError(Marker + " INSTANTIATE FAILED PrefabUtility.InstantiatePrefab returned null.");
                return;
            }

            Undo.RegisterCreatedObjectUndo(instance, UndoLabel);
            instance.transform.SetPositionAndRotation(ResolveAnchorPosition(), Quaternion.identity);
            EditorSceneManager.MarkSceneDirty(target);

            Debug.Log(
                Marker + " INSTANTIATED " + instance.name + " into " + target.name + " at " +
                instance.transform.position + " anchor=" + spawnerAnchorReport, instance);

            Debug.LogWarning(
                Marker + " IN-MEMORY ONLY and PLACEMENT UNRESOLVED. The change is recorded for Undo " +
                "(Ctrl+Z) and the scene is marked modified; this tool did NOT write the .unity file - " +
                "press Ctrl+S to commit it. The pose above is an anchor guess, not authored placement: " +
                "move the root so +Z points out of the hull into open water, then confirm " +
                InteriorSpawnChildName + " lands inside and " + ExteriorSpawnChildName + " lands in the " +
                "water column, because BaseAirlock teleports the player to those two transforms verbatim.");
        }

        private static GameObject BuildAirlockRoot(Scene previewScene, Mesh doorMesh, Material doorMaterial)
        {
            GameObject root = new GameObject(PrefabRootName);
            SceneManager.MoveGameObjectToScene(root, previewScene);
            root.layer = InteractableLayer;

            // The root stays at unit scale on purpose. BaseAirlock reads transform.forward/.up as the
            // bulkhead basis and runs the docking snap through frame.InverseTransformPoint /
            // frame.TransformPoint (BaseAirlock.cs:988-992, :1043-1046), so a non-uniformly scaled root
            // would skew the door normal and the interpolated player pose. Size lives on the collider
            // and on a scaled visual child instead.
            MeshFilter rootFilter = root.AddComponent<MeshFilter>();
            rootFilter.sharedMesh = null;
            MeshRenderer rootRenderer = root.AddComponent<MeshRenderer>();
            rootRenderer.sharedMaterial = null;

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.isTrigger = false;
            collider.center = Vector3.zero;
            collider.size = new Vector3(DoorWidthMeters, DoorHeightMeters, DoorDepthMeters);

            // BaseAirlock carries [RequireComponent(typeof(Renderer))] and Renderer is abstract, so Unity
            // cannot satisfy that dependency itself. The MeshRenderer above must already exist or this
            // AddComponent fails and returns null.
            BaseAirlock airlock = root.AddComponent<BaseAirlock>();
            if (airlock == null)
            {
                Debug.LogError(
                    Marker + " ABORT AddComponent<BaseAirlock> returned null. Its " +
                    "[RequireComponent(typeof(Renderer))] was not satisfied before the add. Nothing written.");
                Object.DestroyImmediate(root);
                return null;
            }

            Transform visual = BuildDoorPlateVisual(root.transform, doorMesh, doorMaterial);
            Transform interiorSpawn = BuildSpawnPoint(
                root.transform,
                InteriorSpawnChildName,
                InteriorSpawnDepthMeters,
                Quaternion.Euler(0f, 180f, 0f));
            Transform exteriorSpawn = BuildSpawnPoint(
                root.transform,
                ExteriorSpawnChildName,
                ExteriorSpawnDepthMeters,
                Quaternion.identity);

            SerializedObject serialized = new SerializedObject(airlock);
            AssignObjectReference(serialized, InteriorSpawnField, interiorSpawn);
            AssignObjectReference(serialized, ExteriorSpawnField, exteriorSpawn);
            AssignObjectReference(serialized, StatusLightField, visual.GetComponent<MeshRenderer>());
            AssignFloat(serialized, BulkheadWidthField, DoorWidthMeters);
            AssignFloat(serialized, BulkheadHeightField, DoorHeightMeters);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static Transform BuildDoorPlateVisual(Transform parent, Mesh doorMesh, Material doorMaterial)
        {
            GameObject visual = new GameObject(VisualChildName);
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = new Vector3(DoorWidthMeters, DoorHeightMeters, DoorDepthMeters);

            MeshFilter filter = visual.AddComponent<MeshFilter>();
            filter.sharedMesh = doorMesh;
            MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = doorMaterial;
            return visual.transform;
        }

        private static Transform BuildSpawnPoint(
            Transform parent,
            string childName,
            float localDepthMeters,
            Quaternion localRotation)
        {
            GameObject spawn = new GameObject(childName);
            spawn.transform.SetParent(parent, false);
            spawn.transform.localPosition = new Vector3(
                0f,
                (-DoorHeightMeters * 0.5f) + SpawnSillClearanceMeters,
                localDepthMeters);
            spawn.transform.localRotation = localRotation;
            return spawn.transform;
        }

        private static void AssignObjectReference(SerializedObject serialized, string fieldName, Object value)
        {
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError(
                    Marker + " FIELD MISSING BaseAirlock." + fieldName +
                    " was not found by SerializedObject. The prefab will be saved with that reference " +
                    "unset - re-read BaseAirlock.cs, the field was renamed.");
                return;
            }

            property.objectReferenceValue = value;
        }

        private static void AssignFloat(SerializedObject serialized, string fieldName, float value)
        {
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError(
                    Marker + " FIELD MISSING BaseAirlock." + fieldName +
                    " was not found by SerializedObject. Re-read BaseAirlock.cs, the field was renamed.");
                return;
            }

            property.floatValue = value;
        }

        /// <summary>
        /// Reloads the asset from disk and checks the wiring the event path actually depends on. A tool
        /// that claims it authored something it cannot re-read is worse than no tool.
        /// </summary>
        private static void VerifySavedPrefab()
        {
            GameObject reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (reloaded == null)
            {
                Debug.LogError(Marker + " VERIFY FAILED saved asset does not reload from " + PrefabPath);
                return;
            }

            if (!reloaded.TryGetComponent(out BaseAirlock airlock))
            {
                Debug.LogError(Marker + " VERIFY FAILED reloaded asset carries no BaseAirlock.");
                return;
            }

            SerializedObject serialized = new SerializedObject(airlock);
            bool interiorSet = IsObjectReferenceSet(serialized, InteriorSpawnField);
            bool exteriorSet = IsObjectReferenceSet(serialized, ExteriorSpawnField);
            bool hasCollider = reloaded.TryGetComponent(out Collider collider) && !collider.isTrigger;
            bool onLayer = reloaded.layer == InteractableLayer;

            if (interiorSet && exteriorSet && hasCollider && onLayer)
            {
                Debug.Log(
                    Marker + " VERIFY OK reloaded asset has both spawn points wired, a non-trigger " +
                    "collider and layer " + InteractableLayer + ".");
                return;
            }

            Debug.LogError(
                Marker + " VERIFY FAILED interiorSpawn=" + (interiorSet ? "1" : "0") +
                " exteriorSpawn=" + (exteriorSet ? "1" : "0") +
                " nonTriggerCollider=" + (hasCollider ? "1" : "0") +
                " interactableLayer=" + (onLayer ? "1" : "0") +
                ". The asset was written but is not complete - fix it before trusting it.");
        }

        private static bool IsObjectReferenceSet(SerializedObject serialized, string fieldName)
        {
            SerializedProperty property = serialized.FindProperty(fieldName);
            return property != null && property.objectReferenceValue != null;
        }

        /// <summary>
        /// Harvests the built-in cube mesh and the active pipeline's default material from one throwaway
        /// primitive, rather than hardcoding a built-in resource name that shifts between Unity versions.
        /// CreatePrimitive lands the probe in the ACTIVE scene, so it is moved into the preview scene
        /// immediately - a sibling agent's open scene must not gain a hierarchy change from this tool -
        /// and destroyed before anything else happens.
        /// </summary>
        private static bool TryResolveVisualPrimitive(Scene previewScene, out Mesh mesh, out Material material)
        {
            mesh = null;
            material = null;
            GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (probe == null)
                return false;

            try
            {
                SceneManager.MoveGameObjectToScene(probe, previewScene);

                if (probe.TryGetComponent(out MeshFilter filter))
                    mesh = filter.sharedMesh;

                if (probe.TryGetComponent(out MeshRenderer renderer))
                    material = renderer.sharedMaterial;
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }

            return mesh != null;
        }

        private static Vector3 ResolveAnchorPosition()
        {
            global::HectonPlayerSpawner spawner = ResolveSpawner();
            return spawner != null ? spawner.transform.position : Vector3.zero;
        }

        private static global::HectonPlayerSpawner ResolveSpawner()
        {
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    global::HectonPlayerSpawner spawner =
                        roots[rootIndex].GetComponentInChildren<global::HectonPlayerSpawner>(true);
                    if (spawner != null)
                        return spawner;
                }
            }

            return null;
        }

        private static List<AirlockSighting> CollectSightings(out int loadedSceneCount, out string spawnerAnchorReport)
        {
            var sightings = new List<AirlockSighting>();
            loadedSceneCount = 0;

            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                loadedSceneCount++;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                    Walk(roots[rootIndex].transform, string.Empty, scene, sightings);
            }

            global::HectonPlayerSpawner spawner = ResolveSpawner();
            spawnerAnchorReport = spawner != null
                ? "HectonPlayerSpawner found at " + spawner.transform.position +
                  " - used as the pod anchor guess. Its runtime spawn is a spiral sea-floor search, so " +
                  "this is a starting point for hand placement, not the real spawn."
                : "no HectonPlayerSpawner in any loaded scene - the anchor guess falls back to world origin.";

            return sightings;
        }

        private static void Walk(
            Transform transform,
            string parentPath,
            Scene scene,
            List<AirlockSighting> sightings)
        {
            string path = parentPath.Length == 0 ? transform.name : parentPath + "/" + transform.name;

            if (transform.TryGetComponent(out BaseAirlock airlock))
            {
                SerializedObject serialized = new SerializedObject(airlock);
                bool hasNonTriggerCollider =
                    transform.TryGetComponent(out Collider collider) && !collider.isTrigger;

                sightings.Add(new AirlockSighting
                {
                    Airlock = airlock,
                    ScenePath = scene.path,
                    SceneName = scene.name,
                    ObjectPath = path,
                    ComponentEnabled = airlock.enabled,
                    GameObjectActiveInHierarchy = airlock.gameObject.activeInHierarchy,
                    OnInteractableLayer = airlock.gameObject.layer == InteractableLayer,
                    HasNonTriggerCollider = hasNonTriggerCollider,
                    HasInteriorSpawn = IsObjectReferenceSet(serialized, InteriorSpawnField),
                    HasExteriorSpawn = IsObjectReferenceSet(serialized, ExteriorSpawnField),
                });
            }

            for (int i = 0; i < transform.childCount; i++)
                Walk(transform.GetChild(i), path, scene, sightings);
        }

        private static void ReportSightings(List<AirlockSighting> sightings)
        {
            var line = new StringBuilder();
            for (int i = 0; i < sightings.Count; i++)
            {
                AirlockSighting sighting = sightings[i];
                line.Length = 0;
                line.Append(Marker);
                line.Append(" AIRLOCK ");
                line.Append(sighting.SceneName);
                line.Append(' ');
                line.Append(sighting.ObjectPath);
                line.Append("  componentEnabled=");
                line.Append(sighting.ComponentEnabled ? "1" : "0");
                line.Append("  gameObjectActive=");
                line.Append(sighting.GameObjectActiveInHierarchy ? "1" : "0");
                line.Append("  interactableLayer=");
                line.Append(sighting.OnInteractableLayer ? "1" : "0");
                line.Append("  nonTriggerCollider=");
                line.Append(sighting.HasNonTriggerCollider ? "1" : "0");
                line.Append("  interiorSpawn=");
                line.Append(sighting.HasInteriorSpawn ? "1" : "0");
                line.Append("  exteriorSpawn=");
                line.Append(sighting.HasExteriorSpawn ? "1" : "0");
                Debug.Log(line.ToString(), sighting.Airlock);
            }
        }

        private static int CountInert(List<AirlockSighting> sightings)
        {
            int inert = 0;
            for (int i = 0; i < sightings.Count; i++)
            {
                AirlockSighting sighting = sightings[i];
                if (!sighting.ComponentEnabled ||
                    !sighting.GameObjectActiveInHierarchy ||
                    !sighting.OnInteractableLayer ||
                    !sighting.HasNonTriggerCollider ||
                    !sighting.HasInteriorSpawn ||
                    !sighting.HasExteriorSpawn)
                {
                    inert++;
                }
            }

            return inert;
        }

        /// <summary>
        /// Known-answer checks on the exact predicate the quest spine hangs off, run before this tool
        /// prints or writes anything. NarrativeProgressionBridge.cs:230-236 accepts an airlock event only
        /// when GetEventType says EnvironmentChanged and IsDry is false, and BaseAirlockEvents.cs:466
        /// feeds IsPlayerInside into that dry bit. If that bit packing ever changes, an airlock authored
        /// by this tool would look correct and raise nothing, so a failure here suppresses the run.
        /// </summary>
        private static bool SelfTestPassed()
        {
            uint wetFlags = BaseAirlockEventPayload.BuildStatusFlags(
                BaseAirlockEventType.EnvironmentChanged,
                isDry: false,
                lockedDown: false,
                overrideBlocked: false);
            if (BaseAirlockEventPayload.GetEventType(wetFlags) != BaseAirlockEventType.EnvironmentChanged ||
                BaseAirlockEventPayload.IsDry(wetFlags))
            {
                Debug.LogError(
                    Marker + " SELF-TEST FAILED wet EnvironmentChanged flags did not round-trip: " +
                    wetFlags + ". Run suppressed.");
                return false;
            }

            uint dryFlags = BaseAirlockEventPayload.BuildStatusFlags(
                BaseAirlockEventType.EnvironmentChanged,
                isDry: true,
                lockedDown: false,
                overrideBlocked: false);
            if (BaseAirlockEventPayload.GetEventType(dryFlags) != BaseAirlockEventType.EnvironmentChanged ||
                !BaseAirlockEventPayload.IsDry(dryFlags))
            {
                Debug.LogError(
                    Marker + " SELF-TEST FAILED dry EnvironmentChanged flags did not round-trip: " +
                    dryFlags + ". Run suppressed.");
                return false;
            }

            uint discoveryHash = NarrativeEvents.ComputeDiscoveryHash(H8Hashes.Signals.FirstHourExitLifepodId);
            if (discoveryHash == 0u)
            {
                Debug.LogError(
                    Marker + " SELF-TEST FAILED ComputeDiscoveryHash(" +
                    H8Hashes.Signals.FirstHourExitLifepodId + ") returned 0, which no consumer can match. " +
                    "Run suppressed.");
                return false;
            }

            return true;
        }
    }
}
