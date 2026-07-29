using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hecton8.Core;
using Hecton8.Crafting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools.Diagnostics
{
    /// <summary>
    /// Reachability audit and authoring instrument for <see cref="Fabricator"/>, the runtime crafting owner.
    ///
    /// WHY THIS EXISTS, AND WHY THE OBVIOUS READING OF THE SYMPTOM IS WRONG.
    /// H8_HeadlessWorldDriver.cs latched the CraftRepairBuild row Blocked with
    /// "no live Fabricator component found in 8 scene searches, so no recipe can be started"
    /// (H8_HeadlessWorldDriver.cs:3274). That message is easy to misread as "no Fabricator was ever
    /// authored". It is not what happened, and building a second Fabricator from scratch would not have
    /// moved the row. The measured facts:
    ///
    ///   1. Fabricator IS in the shipping world scene. A binary-aware GUID scan
    ///      (Tools/SceneGuidReachability.py --type Fabricator, guid 65748c03d0baf8a4a95eca4dd9cfa4c4)
    ///      reports PRESENT in Assets/_Project/Scenes/02_HECTON_WORLD.unity and 010_TEST.unity. The world
    ///      scene has no %YAML header, so an ordinary text search over it returns nothing and lies. That
    ///      exact false negative already produced one retracted verdict on this project.
    ///   2. The station names are in the scene bytes too: 02_HECTON_WORLD.unity contains the literal
    ///      strings DEPRECATED_STUFF, "--- WORLD ---", Fabrication_Outpost, Forward_Fabricator and
    ///      Fabrication_Trial, one occurrence each, plus the type name Fabricator three times.
    ///   3. Assets/_Project/Editor/H8_SceneCleaner.cs walks the scene roots, and for every root that does
    ///      not match its keep-list (TERRAIN / CAMERA / PLAYER / LIGHT / OCEAN / WATER / SUN / SKY /
    ///      ATMOSPHERE / SYSTEM / MANAGER / DIRECTOR / REGISTRY / BOOTSTRAP) it does
    ///      SetParent(DEPRECATED_STUFF) then go.SetActive(false), then EditorSceneManager.SaveScene.
    ///      "--- WORLD ---" matches none of those keep patterns. Neither does Fabrication_Trial.
    ///   4. The driver looks the component up with
    ///      FindFirstObjectByType&lt;Fabricator&gt;(FindObjectsInactive.Exclude)
    ///      (H8_HeadlessWorldDriver.cs:3260-3261). Exclude skips components whose GameObject is inactive
    ///      in the hierarchy.
    ///
    /// So the component exists, its payload is intact, and the query is deliberately blind to it because
    /// an ancestor was disabled and the binary scene was saved that way. The row is a REACHABILITY
    /// failure, not an absence. FabricationBootstrapAuthoring.cs:315-341 documents the same cleanup
    /// blinding its own three GameObject.Find lookups, which is independent corroboration from a second
    /// author.
    ///
    /// Re-activating that ancestor chain is NOT free and is NOT this tool's default: it would mean
    /// writing a 6.27 MB binary production scene, which is unreviewable and undiffable, and is exactly
    /// the operation that created this state in the first place.
    ///
    /// WHY A PREFAB RATHER THAN SCENE SURGERY. Four scenes in this project are binary. A prefab asset is
    /// text YAML: reviewable, diffable, and re-instantiable into any scene. So the authoring half of this
    /// tool produces a prefab first, and getting an instance into a scene is a separate operation behind a
    /// separate flag. The prefab also gives the lane a durable artifact that does not live only inside an
    /// unreadable binary blob.
    ///
    /// THREE WRITE PATHS, THREE RISK LEVELS, THREE SWITCHES. They are not interchangeable:
    ///   1. <see cref="AuthorFabricatorPrefab"/> - creates the prefab, and only at an EMPTY path, so it can
    ///      never overwrite authored work. Flag -h8ApplyFabricator.
    ///   2. <see cref="InstantiateFabricatorIntoOpenScene"/> - instantiates into whatever scene is open and
    ///      MARKS IT DIRTY WITHOUT SAVING, the convention FabricationBootstrapAuthoring.cs:240 set. Human
    ///      MenuItem only. Useless from -batchmode, where nobody presses Ctrl+S and the editor exits
    ///      discarding the instance - which is precisely why (3) had to exist.
    ///   3. <see cref="InstantiateFabricatorSceneInstanceFromCommandLine"/> - opens a NAMED scene,
    ///      instantiates an ACTIVE root, and SAVES. Flag -h8ApplyFabricatorSceneInstance. This is the only
    ///      path that calls EditorSceneManager.SaveScene, it refuses on a dirty scene, and on
    ///      02_HECTON_WORLD.unity it rewrites a 6.27 MB binary scene as YAML. Read its own header before
    ///      running it.
    ///
    /// WHY THE WRITES ARE PERMITTED UNDER AGENTS.md:126. The Sandbox Firewall forbids automated TEST
    /// RUNNERS from calling PrefabUtility.SaveAsPrefabAsset, EditorUtility.SetDirty or
    /// EditorSceneManager.SaveScene on production assets, so that a test pass cannot wipe authored work.
    /// None of this is a test runner and none of it runs on its own: every write needs its own flag spelled
    /// out on the command line or a human MenuItem click, a bare -executeMethod reports and changes
    /// nothing, and the scene path additionally registers Undo and refuses on a dirty scene. Precedents for
    /// the same split on this same scene: H8_ScatterPlacementOwnerEnableAuthoring.cs:49-56 and :307-308,
    /// H8_WorldRootGraveyardRepair.cs:222-236, H8_DuplicateSceneRootAudit.cs:303-316.
    ///
    /// BATCHMODE CONTRACT, matching H8_HazardPrefabAuthoring.cs:243-281 and H8_AirlockSceneAuthoring.cs.
    /// Every entry point is a public static void with no arguments, so -executeMethod can reach it - which
    /// is exactly why the default has to be report-only, including for an invocation aimed at something
    /// else that merely happens to name one of these methods. No EditorUtility.DisplayDialog, no
    /// Selection, no EditorApplication.Exit (it would kill the host job), no [InitializeOnLoadMethod].
    /// Every entry point is idempotent and logs one line per action naming what and where.
    ///
    /// WHAT THIS MEASURES: the loaded scene graph INCLUDING inactive GameObjects, plus the prefab asset
    /// on disk. It is serialisation-format agnostic, so a binary scene does not blind it. WHAT IT DOES
    /// NOT MEASURE: runtime composition. Nothing this reports proves a craft completes; that needs a
    /// headless or Play Mode run.
    ///
    /// This assembly is Editor-only (Hecton8.Editor.asmdef, includePlatforms Editor), and none of this
    /// runs on a dispatcher tick, so the hot-path allocation law does not govern it and Debug.Log is the
    /// convention every neighbour in this folder uses.
    /// </summary>
    public static class H8_FabricatorSceneAuthoring
    {
        private const string Marker = "[H8_FABRICATOR]";

        private const string AuditMenuPath = "Hecton8/Diagnostics/Fabricator Reachability Audit";
        private const string AuthorMenuPath = "Hecton8/Diagnostics/Author Missing Fabricator Prefab";
        private const string InstantiateMenuPath = "Hecton8/Diagnostics/Instantiate Fabricator Into Open Scene";

        /// <summary>
        /// Opt-in flag for the write half. Same shape as -h8ApplyHazardComponents
        /// (H8_HazardPrefabAuthoring.cs:123) and -h8ApplyScatterOwnerEnable
        /// (H8_ScatterPlacementOwnerEnableAuthoring.cs:122). Matching the existing convention matters
        /// more than a tidier one: the flag pattern should be learned once, not per tool.
        /// </summary>
        private const string ApplyFlag = "-h8ApplyFabricator";

        /// <summary>
        /// Opt-in flag for the SCENE half, deliberately distinct from <see cref="ApplyFlag"/>. Authoring a
        /// prefab into an empty path overwrites nobody; writing 02_HECTON_WORLD.unity rewrites a 6.27 MB
        /// production scene. Those are not the same risk and must not share one switch, which is the same
        /// split -h8ApplyScatterOwnerEnable / -h8AllowDirtyScatterOwnerScene draws
        /// (H8_ScatterPlacementOwnerEnableAuthoring.cs:122-124). Naming follows the existing three
        /// (-h8ApplyScatterOwnerEnable, -h8ApplyHazardComponents, -h8ApplyFabricator): -h8Apply&lt;Noun&gt;.
        /// </summary>
        private const string SceneApplyFlag = "-h8ApplyFabricatorSceneInstance";

        /// <summary>Scene path override. Same shape as -h8ScatterOwnerScene.</summary>
        private const string SceneFlag = "-h8FabricatorScene";

        /// <summary>
        /// Escape hatch for the dirty-on-open refusal, same shape and same reason as
        /// -h8AllowDirtyScatterOwnerScene. Not a convenience: passing it means accepting that whatever
        /// injected content into the scene during load gets cemented alongside the fabricator.
        /// </summary>
        private const string AllowDirtySceneFlag = "-h8AllowDirtyFabricatorScene";

        /// <summary>
        /// MEASURED, not assumed. H8_HeadlessWorldDriver's craft phase runs in Play Mode after
        /// GameBootstrapper has walked 00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD, and every one of
        /// those loads is LoadSceneMode.Single (GameBootstrapper.cs:3230, :3259, :3331). Single UNLOADS the
        /// previous scene, so an instance placed in 00_BOOTSTRAP or 01_MAIN_MENU is destroyed before
        /// TickCraft ever runs and would be a silently wasted write. Logs/h8_probe7.log:11974 and :22964
        /// both report activeScene='02_HECTON_WORLD' from FirstGameplayTick onward, so this is the only
        /// scene loaded when the driver's lookup fires. It is also the expensive one to write - see
        /// <see cref="DescribeSceneFileFormat"/>.
        /// </summary>
        private const string DefaultScenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";

        private const string SceneInstanceMenuPath =
            "Hecton8/Diagnostics/Fabricator Scene Instance - INSTANTIATE AND SAVE";

        private const string UndoLabel = "Instantiate fabricator into scene";

        /// <summary>Bytes of the YAML preamble Unity writes at the head of a text scene.</summary>
        private static readonly byte[] TextSceneSignature = { 0x25, 0x59, 0x41, 0x4D, 0x4C };

        /// <summary>
        /// Where the persisted instance lands, and why it is not the origin.
        ///
        /// Logs/h8_probe7.log:12408-12412 measures the spawn: Position (0, 16, 0), water level 14.0, ground
        /// height under the player -2.8, water depth 16.8 m. So world origin sits 16 m directly BELOW the
        /// spawn point and 2.8 m above the seabed - which is the exact column the driver's SwimDive phase
        /// descends through, and that phase is currently under measurement by another lane (probe7's
        /// schedule burned its whole 7.000 s SwimDive grant over 25865 ticks). Dropping a 1.6 x 1.2 x 0.9 m
        /// NON-TRIGGER collider into that column could perturb somebody else's numbers, so the instance is
        /// offset 16.97 m laterally (sqrt(12^2 + 12^2)) and out of the dive column entirely.
        ///
        /// y = 0 keeps it submerged, 14 m under the water line, which is right for a seabed station. NOT
        /// MEASURED: the seabed height at column (12, 12) - -2.8 was sampled under the spawn point only, so
        /// this instance may float above or clip into the floor there. That is cosmetic for this lane. The
        /// driver calls StartCraft directly and never walks to the station, and maxUseDistance
        /// (Fabricator.cs:106) gates only human interaction, so no measurement here depends on the pose.
        /// A human placing a real fabrication outpost should move it and re-save.
        /// </summary>
        private static readonly Vector3 DiagnosticInstancePosition = new Vector3(12f, 0f, 12f);

        /// <summary>
        /// Sits with its PFB_Module_* siblings in the folder the Construction prefabs already occupy
        /// (PFB_Module_Corridor, PFB_Module_CurrentTurbine, PFB_Module_Foundation, PFB_Module_Pylon,
        /// PFB_Module_ServicePump are all here). A fabricator is a construction module.
        /// </summary>
        private const string PrefabPath = "Assets/_Project/Prefabs/Construction/Final/PFB_Module_Fabricator.prefab";
        private const string PrefabParentFolder = "Assets/_Project/Prefabs/Construction/Final";
        private const string PrefabRootName = "PFB_Module_Fabricator";

        /// <summary>
        /// Child transforms Fabricator serialises by reference. A fabricator with a null outputSocket
        /// still crafts, but the produced item has no authored drop pose, so both sockets are created and
        /// wired here rather than left for a later pass to discover.
        /// </summary>
        private const string OutputSocketChildName = "Output_Socket";
        private const string DeconstructOutputSocketChildName = "Deconstruct_Output_Socket";
        private const string AssemblyPreviewChildName = "Assembly_Preview";
        private const string BodyVisualChildName = "Body_Visual";

        /// <summary>
        /// Serialised field names on Fabricator. They are private [SerializeField] members
        /// (Fabricator.cs:95-101), so SerializedObject is the only sanctioned way to set them - editing
        /// prefab YAML by hand is banned outright. These names are the same ones
        /// FabricationBootstrapAuthoring.cs:439-454 already drives, so a rename breaks both tools
        /// together and loudly rather than silently here.
        /// </summary>
        private const string RecipesField = "availableRecipes";
        private const string FabricatorNameField = "fabricatorName";
        private const string AssemblyFallbackMeshField = "assemblyFallbackMesh";
        private const string AssemblyPreviewMeshFilterField = "assemblyPreviewMeshFilter";
        private const string AssemblyPreviewRendererField = "assemblyPreviewRenderer";
        private const string OutputSocketField = "outputSocket";
        private const string DeconstructOutputSocketField = "deconstructOutputSocket";

        private const string DisplayName = "Fabricator";

        /// <summary>
        /// Every RecipeData in the project lives in this one folder - 42 assets, counted. Fabricator
        /// reads ONLY its own serialised availableRecipes list (Fabricator.cs:101); there is no
        /// RecipeCatalog and no global registration, so a Fabricator authored with an empty list is a
        /// live component that offers zero recipes and would move the blocked row to a different message
        /// instead of clearing it.
        /// </summary>
        private const string RecipesFolder = "Assets/_Project/Data/Crafting/Recipes";

        private const string HologramMaterialPath = "Assets/_Project/Art/Materials/MAT_FabricatorAssembly_Hologram.asset";
        private const string HologramMaterialField = "hologramAssemblyMaterial";

        private const float StationWidthMeters = 1.6f;
        private const float StationHeightMeters = 1.2f;
        private const float StationDepthMeters = 0.9f;

        /// <summary>
        /// Interaction requires a non-trigger collider on the Interactable layer: InteractableRegistry
        /// drops any collider that is a trigger or off HectonLayerMasks.InteractableLayerMask, so a
        /// fabricator on the Default layer is visible, live, and impossible to use.
        /// </summary>
        private static int InteractableLayer => HectonLayerMasks.Interactable;

        private sealed class FabricatorSighting
        {
            public string SceneName;
            public string ScenePath;
            public string ObjectPath;
            public bool ComponentEnabled;
            public bool GameObjectActiveSelf;
            public bool GameObjectActiveInHierarchy;
            public string HighestInactiveAncestorPath;
            public int InactiveAncestorCount;
            public int RecipeCount;
            public bool HasNonTriggerCollider;
            public int Layer;
        }

        // ------------------------------------------------------------------
        //  BATCHMODE ENTRY POINT
        // ------------------------------------------------------------------

        /// <summary>
        /// The gate. AGENTS.md:126 forbids an automated runner from writing production assets, and a
        /// no-argument public static void is reachable by -executeMethod - including from a batchmode
        /// invocation aimed at something else entirely that merely happens to name this method. So the
        /// default is report-only and the write has to be asked for out loud.
        ///
        /// Note 'System.Environment' spelled in full: Hecton8.Environment exists and shadows
        /// System.Environment inside a Hecton8.* namespace, so the unqualified name would bind to the
        /// wrong type or fail to compile.
        /// </summary>
        public static void AuthorFabricatorFromCommandLine()
        {
            bool apply = false;
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], ApplyFlag, StringComparison.Ordinal))
                {
                    apply = true;
                    break;
                }
            }

            if (!apply)
            {
                Debug.Log(
                    Marker + " REPORT-ONLY no " + ApplyFlag + " argument was passed, so nothing will be " +
                    "written. AGENTS.md:126 forbids an automated pass from writing production assets. " +
                    "Re-run with " + ApplyFlag + " to author the prefab, or use the menu item '" +
                    AuthorMenuPath + "'. Putting an instance into a SCENE is a separate operation with a " +
                    "separate flag - " + nameof(InstantiateFabricatorSceneInstanceFromCommandLine) +
                    " plus " + SceneApplyFlag + " - because a scene write is a different order of risk " +
                    "from creating a file at an empty path. The reachability report follows.");
                ReportFabricatorReachability();
                return;
            }

            Debug.Log(
                Marker + " APPLY " + ApplyFlag + " was passed explicitly, so " + PrefabPath +
                " WILL be created if and only if that path is currently empty. No scene is touched on " +
                "this path and EditorSceneManager.SaveScene is never called.");
            ReportFabricatorReachability();
            AuthorFabricatorPrefab();
        }

        // ------------------------------------------------------------------
        //  BATCHMODE SCENE INSTANCE - opens, instantiates ACTIVE, persists
        // ------------------------------------------------------------------

        /// <summary>
        /// Opens the target scene, puts one ACTIVE fabricator instance in it as a root, and SAVES.
        ///
        /// WHY THIS EXISTS AT ALL, given that this file previously argued the opposite. The earlier position
        /// was that a scene write is human-only. That was correct about the risk and wrong about the
        /// consequence: <see cref="InstantiateFabricatorIntoOpenScene"/> marks dirty and stops, and in
        /// -batchmode nobody presses Ctrl+S, so the editor exits and discards the instance. A tool that can
        /// only be finished by a human is not a batchmode path. This is the persisting half; the MenuItem
        /// half still exists and still refuses to save, and neither replaces the other. That is exactly the
        /// split H8_ScatterPlacementOwnerEnableAuthoring.cs:40-47 draws against
        /// H8_PlacementOwnerEnabledAudit.
        ///
        /// WHY WRITING IS PERMITTED. AGENTS.md `Sandbox Firewall Rule` bans automated TEST RUNNERS from
        /// calling EditorSceneManager.SaveScene on production assets so a test pass cannot wipe authored
        /// work. This is not a test runner and does not run on its own: the write needs
        /// <see cref="SceneApplyFlag"/> spelled out on the command line or a human MenuItem click, a bare
        /// -executeMethod reports and changes nothing, the creation is registered with Undo, and the tool
        /// refuses outright on a dirty scene. Working precedents for saving this same production scene
        /// under this same split: H8_ScatterPlacementOwnerEnableAuthoring.cs:307-308,
        /// H8_WorldRootGraveyardRepair.cs:222-236, H8_DuplicateSceneRootAudit.cs:303-316.
        ///
        /// WHAT THE WRITE COSTS, stated up front because it is large. 02_HECTON_WORLD.unity is currently a
        /// BINARY 6.27 MB file while ProjectSettings/EditorSettings.asset carries m_SerializationMode: 2
        /// (ForceText), so the first save through the asset pipeline rewrites the whole scene as YAML. The
        /// diff will be the entire scene, not one object. That is a wanted consequence - a GUID grep returns
        /// zero against the binary form whether a component is there or not, which is how the existing
        /// fabricators were once reported absent - but it must not arrive as a surprise, so the on-disk
        /// format is read from the file's first bytes before and after and printed both times rather than
        /// predicted.
        ///
        /// Note System.Environment spelled in full: Hecton8.Environment shadows System.Environment inside a
        /// Hecton8.* namespace.
        ///
        /// USAGE (reports by default, writes nothing without the flag):
        ///   Unity.exe -batchmode -quit -projectPath . -logFile Logs/fabricatorscene.log \
        ///     -executeMethod Hecton8.EditorTools.Diagnostics.H8_FabricatorSceneAuthoring.InstantiateFabricatorSceneInstanceFromCommandLine \
        ///     [-h8FabricatorScene Assets/_Project/Scenes/02_HECTON_WORLD.unity] \
        ///     [-h8ApplyFabricatorSceneInstance] [-h8AllowDirtyFabricatorScene]
        /// </summary>
        public static void InstantiateFabricatorSceneInstanceFromCommandLine()
        {
            string scenePath = DefaultScenePath;
            bool apply = false;
            bool allowDirtyOpen = false;

            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], SceneApplyFlag, StringComparison.Ordinal))
                {
                    apply = true;
                    continue;
                }

                if (string.Equals(args[i], AllowDirtySceneFlag, StringComparison.Ordinal))
                {
                    allowDirtyOpen = true;
                    continue;
                }

                if (!string.Equals(args[i], SceneFlag, StringComparison.Ordinal))
                    continue;

                if (i + 1 >= args.Length)
                {
                    Debug.LogError(
                        Marker + " REFUSED " + SceneFlag + " was passed with no scene path after it. " +
                        "Nothing was opened and nothing was written.");
                    return;
                }

                scenePath = args[i + 1];
                i++;
            }

            ExecuteSceneInstance(scenePath, apply, allowDirtyOpen);
        }

        /// <summary>
        /// Human entry point for the same operation. A separate menu item from the dirty-marking one on
        /// purpose: a 6.27 MB production scene write must not be one misclick away from a diagnostic.
        /// </summary>
        [MenuItem(SceneInstanceMenuPath)]
        public static void InstantiateFabricatorSceneInstanceAndSave()
        {
            ExecuteSceneInstance(DefaultScenePath, true, false);
        }

        private static void ExecuteSceneInstance(string scenePath, bool apply, bool allowDirtyOpen)
        {
            if (string.IsNullOrEmpty(scenePath))
            {
                Debug.LogError(Marker + " REFUSED empty scene path. Nothing was opened.");
                return;
            }

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError(
                    Marker + " REFUSED " + PrefabPath + " does not exist, so there is nothing to " +
                    "instantiate and no reason to open a scene. Author it first with " + ApplyFlag +
                    " or the menu item '" + AuthorMenuPath + "'. Nothing was written.");
                return;
            }

            if (!prefabAsset.TryGetComponent(out Fabricator prefabFabricator))
            {
                Debug.LogError(
                    Marker + " REFUSED " + PrefabPath + " carries no " + nameof(Fabricator) +
                    " component, so instantiating it could not clear the CraftRepairBuild row. Nothing " +
                    "was written.");
                return;
            }

            // DIRTY PREFLIGHT, BEFORE ANYTHING ELSE. EditorSceneManager.OpenScene(Single) silently
            // discards unsaved in-memory work. Neighbours in this project deliberately leave exactly that
            // kind of change behind - InstantiateFabricatorIntoOpenScene right in this file marks dirty and
            // stops, and H8_PlacementOwnerEnabledAudit.cs repairs in memory only - so a dirty scene at
            // entry is a refusal, not a gamble. This runs in report mode too: the report opens the scene as
            // well, so the destructive step is identical on both paths.
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                Scene loaded = EditorSceneManager.GetSceneAt(i);
                if (!loaded.isDirty)
                    continue;

                Debug.LogError(
                    Marker + " REFUSED scene '" + loaded.name + "' has UNSAVED changes. Opening '" +
                    scenePath + "' with OpenSceneMode.Single would discard them silently, and something " +
                    "in this project may have put a real in-memory repair there on purpose. Save or " +
                    "discard it deliberately, then re-run. Nothing was opened and nothing was written.");
                return;
            }

            string formatBeforeOpen = DescribeSceneFileFormat(scenePath);

            Debug.Log(
                Marker + " OPENING '" + scenePath + "' with OpenSceneMode.Single. This REPLACES whatever " +
                "scene is currently open, on the report path as well as the write path, because the " +
                "idempotence check has to look inside the target scene to be worth anything. " +
                "onDiskFormat=" + formatBeforeOpen + " editorSerializationMode=" +
                EditorSettings.serializationMode);

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError(
                    Marker + " REFUSED could not open '" + scenePath + "'. Nothing was written.");
                return;
            }

            bool dirtyOnOpen = scene.isDirty;
            Debug.Log(
                Marker + " OPENED scene='" + scene.name + "' rootCount=" +
                scene.GetRootGameObjects().Length + " dirtyImmediatelyAfterOpen=" + dirtyOnOpen);

            // Idempotence has to see inactive objects. That is the whole lesson of this lane: GameObject.Find
            // and FindObjectsInactive.Exclude both went blind the moment an ancestor was disabled, and an
            // idempotence check inheriting that blindness would cheerfully stack a second fabricator next to
            // the buried one on every run.
            List<FabricatorSighting> sightings = CollectSightings();
            int liveInTargetScene = 0;
            int buriedInTargetScene = 0;
            for (int i = 0; i < sightings.Count; i++)
            {
                FabricatorSighting s = sightings[i];
                if (!string.Equals(s.ScenePath, scene.path, StringComparison.Ordinal))
                    continue;

                if (s.GameObjectActiveInHierarchy)
                    liveInTargetScene++;
                else
                    buriedInTargetScene++;
            }

            Debug.Log(
                Marker + " PREEXISTING in '" + scene.name + "': live=" + liveInTargetScene +
                " buried=" + buriedInTargetScene +
                " (inactive-inclusive scan; the driver's Exclude query can only ever see the live ones)");

            if (liveInTargetScene > 0)
            {
                Debug.Log(
                    Marker + " ALREADY LIVE " + liveInTargetScene + " active " + nameof(Fabricator) +
                    " instance(s) already exist in '" + scene.name +
                    "', so FindFirstObjectByType<Fabricator>(FindObjectsInactive.Exclude) already resolves " +
                    "and adding another would only duplicate a crafting station. NOTHING was instantiated " +
                    "and NOTHING was written. Run '" + AuditMenuPath + "' for the per-instance detail.");
                ReportCraftGateReadiness(prefabFabricator);
                return;
            }

            if (buriedInTargetScene > 0)
            {
                Debug.LogWarning(
                    Marker + " BURIED ONLY " + buriedInTargetScene + " " + nameof(Fabricator) +
                    " instance(s) exist in '" + scene.name + "' and every one of them is inactive in " +
                    "hierarchy, so the driver's Exclude query sees none of them. This tool does NOT " +
                    "re-activate them - re-enabling content an author or H8_SceneCleaner switched off is a " +
                    "decision it cannot make - it adds a ROOT instance instead, which has no ancestor that " +
                    "can be disabled out from under it.");
            }

            if (!apply)
            {
                Debug.Log(
                    Marker + " REPORT ONLY no " + SceneApplyFlag + " argument, so nothing was written. " +
                    "WOULD instantiate " + PrefabRootName + " from " + PrefabPath + " as a ROOT of '" +
                    scene.name + "' at " + DiagnosticInstancePosition + ", SetActive(true), then call " +
                    "EditorSceneManager.SaveScene - which rewrites " + scenePath + " from " +
                    formatBeforeOpen + " as " + EditorSettings.serializationMode +
                    ". When those two differ the diff is the ENTIRE file, not one object. Re-run with " +
                    SceneApplyFlag + " to write, or use the menu item '" + SceneInstanceMenuPath + "'.");
                ReportCraftGateReadiness(prefabFabricator);
                return;
            }

            if (dirtyOnOpen && !allowDirtyOpen)
            {
                Debug.LogError(
                    Marker + " REFUSED '" + scene.name + "' was ALREADY DIRTY immediately after opening, " +
                    "before this tool touched anything, so editor code injected content into it during " +
                    "load. Saving now would cement that injection alongside the fabricator, which is how " +
                    "nine H8_PlayModeScreenshotter roots got into this scene in the first place " +
                    "(H8_DuplicateSceneRootAudit.cs:17-39). Identify the injector first, then re-run with " +
                    AllowDirtySceneFlag + " if the extra content is genuinely wanted on disk. Nothing was " +
                    "written.");
                return;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefabAsset, scene) as GameObject;
            if (instance == null)
            {
                Debug.LogError(
                    Marker + " REFUSED PrefabUtility.InstantiatePrefab returned null for " + PrefabPath +
                    ". Nothing was written.");
                return;
            }

            Undo.RegisterCreatedObjectUndo(instance, UndoLabel);

            instance.name = PrefabRootName;
            instance.transform.SetParent(null, true);
            instance.transform.position = DiagnosticInstancePosition;
            instance.transform.rotation = Quaternion.identity;
            instance.SetActive(true);

            if (!instance.activeInHierarchy)
            {
                Debug.LogError(
                    Marker + " REFUSED " + PrefabRootName + " is still not activeInHierarchy after " +
                    "SetActive(true) as a scene root, which should be impossible. The driver's Exclude " +
                    "query would not see it, so saving would write a useless object into a production " +
                    "scene. NOTHING was saved. The instance IS in memory and the scene is now dirty, so " +
                    "discard it before re-running - this tool's own dirty preflight will refuse until you " +
                    "do, which is the intended behaviour and not a second bug.");
                return;
            }

            // Reproduce the driver's exact predicate before committing to a 6.27 MB write. If this does not
            // resolve, the write buys nothing and must not happen. This is the one check that turns "an
            // object was added" into "the query that latched the row now answers".
            Fabricator driverWouldFind = UnityEngine.Object.FindFirstObjectByType<Fabricator>(
                FindObjectsInactive.Exclude);
            if (driverWouldFind == null)
            {
                Debug.LogError(
                    Marker + " REFUSED FindFirstObjectByType<Fabricator>(FindObjectsInactive.Exclude) " +
                    "STILL resolves to NULL with an active root instance in the scene. That contradicts " +
                    "the whole model of this lane, so the write is refused rather than guessed at. " +
                    "NOTHING was saved. The instance IS in memory and the scene is now dirty; discard it " +
                    "before re-running.");
                return;
            }

            Debug.Log(
                Marker + " DRIVER PREDICATE NOW RESOLVES to '" + driverWouldFind.gameObject.name +
                "'. This is byte-for-byte the lookup at H8_HeadlessWorldDriver.cs:3260-3261 " +
                "(TickCraft, FindObjectsInactive.Exclude) that latched CraftRepairBuild Blocked with " +
                "\"no live Fabricator component found in 8 scene searches\" in " +
                "Logs/h8_worldsim_probe5.log:19076.");

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            string formatAfterSave = DescribeSceneFileFormat(scenePath);

            if (!saved)
            {
                Debug.LogError(
                    Marker + " SaveScene returned FALSE for '" + scenePath + "'. The instance is in " +
                    "memory ONLY. Do not assume it is on disk; a batchmode -quit from here discards it.");
                return;
            }

            Debug.Log(
                Marker + " INSTANTIATED AND SAVED " + PrefabRootName + " as a ROOT of '" + scene.name +
                "' at " + instance.transform.position + " activeInHierarchy=" + instance.activeInHierarchy +
                " layer=" + instance.layer + ". Parented to nothing on purpose: every pre-existing " +
                "fabricator in this scene is unreachable because an ANCESTOR is disabled, and a root has " +
                "no ancestor to be disabled by. onDiskFormat " + formatBeforeOpen + " -> " +
                formatAfterSave + ".");

            // Report the INSTANCE's component, not the prefab's. They should be identical, and if a prefab
            // override ever makes them differ, the number that matters is the one the driver will read.
            ReportCraftGateReadiness(
                instance.TryGetComponent(out Fabricator instanceFabricator)
                    ? instanceFabricator
                    : prefabFabricator);

            Debug.LogWarning(
                Marker + " STATIC CHANGE ONLY the instance is on disk and the driver's lookup resolves in " +
                "THIS editor session. That is not proof a craft runs. Fabricator does every registration " +
                "in OnEnable (Fabricator.cs:605-628: RegisterActiveFabricator, " +
                "InteractableRegistry.RegisterTree, BaseLogisticsNetwork.RegisterFabricator, TryRegister " +
                "for SlowTick) and OnEnable only fires in Play Mode or a headless run. Nothing here " +
                "started a craft and no CraftingStartedSignal or ItemAcquiredSignal was observed. Re-run " +
                "the headless world driver before claiming the row changed verdict.");
        }

        /// <summary>
        /// The gate audit the lane brief demanded: an instance that cannot craft must not be reported as a
        /// fix, because it only moves the blocked row to a different message. Fabricator.CanCraft
        /// (Fabricator.cs:737-757) is a chain of eight refusals and this walks all of them, saying plainly
        /// which are satisfied by prefab data, which are runtime state, and which nothing in this file can
        /// reach.
        ///
        /// Everything here is read from serialised asset data. Nothing is executed, so nothing below is
        /// runtime proof.
        /// </summary>
        private static void ReportCraftGateReadiness(Fabricator fabricator)
        {
            if (fabricator == null)
                return;

            IReadOnlyList<RecipeData> recipes = fabricator.AvailableRecipes;
            int recipeCount = recipes == null ? 0 : recipes.Count;

            int ingredientsOk = 0;
            int resultOk = 0;
            int scanGated = 0;
            int biomeLocked = 0;
            int nullEntries = 0;

            for (int i = 0; i < recipeCount; i++)
            {
                RecipeData recipe = recipes[i];
                if (recipe == null)
                {
                    nullEntries++;
                    continue;
                }

                // Mirrors Fabricator.cs:744 exactly - CanCraft only asks whether the list is non-empty at
                // that point; per-ingredient availability is a runtime inventory question, checked later by
                // HasIngredientsFastFailOrLegacy, and is deliberately NOT second-guessed here.
                if (recipe.ingredients != null && recipe.ingredients.Count > 0)
                    ingredientsOk++;

                // Mirrors Fabricator.cs:745.
                if (recipe.resultItem != null && recipe.resultQuantity > 0)
                    resultOk++;

                if (recipe.RequiresScanUnlock)
                    scanGated++;

                if (recipe.RequiresAnchoredBiomeLock)
                    biomeLocked++;
            }

            // Read, not assumed. PassesBiomeLock needs this reference and it is a private [SerializeField]
            // (Fabricator.cs:193), so SerializedObject is the only sanctioned way to see it. Asserting
            // "the prefab leaves it null" would go stale the moment somebody wires a host module.
            string thermalHost = "<unreadable>";
            SerializedProperty thermalHostProp = new SerializedObject(fabricator).FindProperty("thermalHostModule");
            if (thermalHostProp != null && thermalHostProp.propertyType == SerializedPropertyType.ObjectReference)
            {
                thermalHost = thermalHostProp.objectReferenceValue == null
                    ? "null"
                    : thermalHostProp.objectReferenceValue.name;
            }

            Debug.Log(
                Marker + " CRAFT GATES recipes=" + recipeCount + " nullEntries=" + nullEntries +
                " withIngredients=" + ingredientsOk + " withValidResult=" + resultOk +
                " scanUnlockGated=" + scanGated + " anchoredBiomeLocked=" + biomeLocked +
                " thermalHostModule=" + thermalHost +
                " (recipe cache ceiling is " + Fabricator.MaxRecipeCacheEntries +
                " entries, so this list does not overflow it)");

            // THE DECISIVE ONE. Every other gate can pass and CanCraft still returns false here.
            Debug.LogError(
                Marker + " CANNOT CRAFT YET, AND NOT FOR A REASON THIS FILE CAN FIX. " +
                "Fabricator.CanCraft returns false at Fabricator.cs:743 while _playerInventory is null. " +
                "That field (Fabricator.cs:204) is assigned in EXACTLY ONE place in the whole type - " +
                "interactor.TryGetComponent(out _playerInventory) inside IInteractable.Interact, " +
                "Fabricator.cs:682-683. It is not serialised, has no public setter, and has no registry " +
                "fallback. H8_HeadlessWorldDriver.TickCraft calls CanCraft and StartCraft DIRECTLY " +
                "(:3311, :3338) and never calls Interact anywhere in the file, so on the next headless run " +
                "this instance reports live-with-recipes and CanCraft false for all " + recipeCount +
                " of them. The row moves from \"no live Fabricator component found in 8 scene searches\" " +
                "to \"Fabricator is live with visibleRecipes=N ... but CanCraft is false for all of them\" " +
                "(H8_HeadlessWorldDriver.cs:3325-3330). Closing that needs an owner-side change OUTSIDE " +
                "this file: either the driver interacts before sweeping, or Fabricator gains a non-" +
                "interaction inventory route.");

            Debug.Log(
                Marker + " CRAFT GATES, the rest of the chain, so the next reader does not re-derive it. " +
                "POWER IS NOT A BLOCKER: _hasPower initialises to TRUE (Fabricator.cs:297) and only " +
                "OnPowerStatusChanged (:527) ever changes it, so a standalone instance on no power grid is " +
                "never told it lacks power - a grid-connected one could actually be worse off. " +
                "UNLOCK MASK: IsRecipeUnlocked (:3964-3976) is fail-closed and reads the vault buffer " +
                "BufferID.ShinobuFabricatorUnlockedRecipes, which EnsureRecipeUnlockMask (:3810-3856) " +
                "clears and rebuilds, setting a bit only when RecipeData.IsUnlocked returns true - and that " +
                "is true whenever the recipe needs no scan entry (RecipeData.cs:196-202), so the " +
                (recipeCount - scanGated) + " un-scan-gated recipes above self-unlock and the " + scanGated +
                " scan-gated ones need scan-log progression. BIOME LOCK: PassesBiomeLock (:1425-1451) " +
                "returns true unless the recipe demands an anchored biome, and when it does it needs a " +
                "non-null moored thermalHostModule, measured as '" + thermalHost + "' above, so the " +
                biomeLocked + " biome-locked recipes fail whenever that reads null. None of this matters " +
                "until _playerInventory is non-null, which is the gate above.");
        }

        /// <summary>
        /// Reads the first bytes of the scene file and reports the on-disk serialisation format instead of
        /// predicting it. Paths are resolved from Application.dataPath, never hardcoded - AGENTS.md
        /// `Relative Path Requirement`.
        /// </summary>
        private static string DescribeSceneFileFormat(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return "unknown(no path)";

            try
            {
                DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
                if (projectRoot == null)
                    return "unknown(no project root)";

                string absolute = Path.Combine(projectRoot.FullName, assetPath);
                if (!File.Exists(absolute))
                    return "absent";

                var header = new byte[TextSceneSignature.Length];
                int read;
                long length;
                using (FileStream stream = File.OpenRead(absolute))
                {
                    length = stream.Length;
                    read = stream.Read(header, 0, header.Length);
                }

                bool isText = read == TextSceneSignature.Length;
                for (int i = 0; isText && i < TextSceneSignature.Length; i++)
                {
                    if (header[i] != TextSceneSignature[i])
                        isText = false;
                }

                return (isText ? "text-yaml" : "binary") + "(" + length + " bytes)";
            }
            catch (IOException error)
            {
                return "unreadable(" + error.Message + ")";
            }
            catch (UnauthorizedAccessException error)
            {
                return "unreadable(" + error.Message + ")";
            }
        }

        // ------------------------------------------------------------------
        //  REPORT HALF - writes nothing
        // ------------------------------------------------------------------

        /// <summary>
        /// Read-only reachability report. Reproduces the headless driver's exact lookup predicate next to
        /// an inactive-inclusive one, so the delta between them IS the diagnosis rather than something a
        /// reader has to infer.
        /// </summary>
        [MenuItem(AuditMenuPath)]
        public static void ReportFabricatorReachability()
        {
            Debug.Log(
                Marker + " AUDIT BEGIN scanning every loaded scene for " + nameof(Fabricator) +
                ", inactive GameObjects included. Serialisation-format agnostic, so the binary world " +
                "scene does not blind this. Writes nothing.");

            List<FabricatorSighting> sightings = CollectSightings();

            Fabricator driverWouldFind = UnityEngine.Object.FindFirstObjectByType<Fabricator>(
                FindObjectsInactive.Exclude);

            if (sightings.Count == 0)
            {
                Debug.LogWarning(
                    Marker + " AUDIT NO INSTANCES no " + nameof(Fabricator) +
                    " exists in any loaded scene, inactive included. If the world scene is not the open " +
                    "scene right now this is expected and says nothing about shipping content: a " +
                    "binary-aware GUID scan (Tools/SceneGuidReachability.py --type Fabricator) reports " +
                    "the component PRESENT in Assets/_Project/Scenes/02_HECTON_WORLD.unity and " +
                    "010_TEST.unity. Open the world scene and re-run before concluding anything.");
            }

            for (int i = 0; i < sightings.Count; i++)
            {
                FabricatorSighting s = sightings[i];
                StringBuilder line = new StringBuilder(320);
                line.Append(Marker)
                    .Append(s.GameObjectActiveInHierarchy ? " AUDIT LIVE " : " AUDIT UNREACHABLE ")
                    .Append(s.ObjectPath)
                    .Append(" scene=").Append(s.SceneName)
                    .Append(" scenePath=").Append(string.IsNullOrEmpty(s.ScenePath) ? "<unsaved>" : s.ScenePath)
                    .Append(" enabled=").Append(s.ComponentEnabled)
                    .Append(" activeSelf=").Append(s.GameObjectActiveSelf)
                    .Append(" activeInHierarchy=").Append(s.GameObjectActiveInHierarchy)
                    .Append(" recipes=").Append(s.RecipeCount)
                    .Append(" layer=").Append(s.Layer)
                    .Append(" nonTriggerCollider=").Append(s.HasNonTriggerCollider);

                if (s.InactiveAncestorCount > 0)
                {
                    line.Append(" inactiveAncestors=").Append(s.InactiveAncestorCount)
                        .Append(" outermostInactiveAncestor=").Append(s.HighestInactiveAncestorPath);
                }

                if (s.GameObjectActiveInHierarchy)
                    Debug.Log(line.ToString());
                else
                    Debug.LogWarning(line.ToString());

                if (s.InactiveAncestorCount > 0)
                {
                    Debug.LogWarning(
                        Marker + " AUDIT CAUSE " + s.ObjectPath + " is disabled by an ANCESTOR, not by " +
                        "itself: '" + s.HighestInactiveAncestorPath + "' has activeSelf=false. " +
                        "Assets/_Project/Editor/H8_SceneCleaner.cs reparents every scene root outside its " +
                        "keep-list under DEPRECATED_STUFF, calls SetActive(false), then " +
                        "EditorSceneManager.SaveScene. '--- WORLD ---' matches none of its keep patterns, " +
                        "and the fabricator stations are its children. The component and its payload are " +
                        "intact; only the query is blind.");
                }

                if (s.GameObjectActiveInHierarchy && s.RecipeCount == 0)
                {
                    Debug.LogWarning(
                        Marker + " AUDIT EMPTY RECIPES " + s.ObjectPath + " is live but its serialised " +
                        RecipesField + " list is empty. Fabricator reads only its own list " +
                        "(Fabricator.cs:101) - there is no RecipeCatalog to fall back on - so this " +
                        "instance can never start a craft and would produce a DIFFERENT blocked message " +
                        "rather than none.");
                }

                if (s.GameObjectActiveInHierarchy && !s.HasNonTriggerCollider)
                {
                    Debug.LogWarning(
                        Marker + " AUDIT UNINTERACTABLE " + s.ObjectPath + " is live but carries no " +
                        "non-trigger Collider, so InteractableRegistry will drop it and the player " +
                        "cannot open it. StartCraft called directly from a driver still works; a human " +
                        "player is locked out.");
                }
            }

            Debug.Log(
                Marker + " AUDIT DRIVER PREDICATE FindFirstObjectByType<Fabricator>(" +
                "FindObjectsInactive.Exclude) resolves to " +
                (driverWouldFind == null ? "NULL" : driverWouldFind.gameObject.name) +
                ". This is byte-for-byte the lookup at H8_HeadlessWorldDriver.cs:3260-3261 that latched " +
                "the CraftRepairBuild row Blocked after " + "8 attempts. Inactive-inclusive scan found " +
                sightings.Count + " instance(s).");

            if (driverWouldFind == null && sightings.Count > 0)
            {
                Debug.LogError(
                    Marker + " AUDIT VERDICT REACHABILITY, NOT ABSENCE. " + sightings.Count +
                    " Fabricator instance(s) exist in loaded scenes and the driver's Exclude query sees " +
                    "NONE of them. Authoring another Fabricator does not fix this. Two candidate " +
                    "owner-side fixes, neither of them this tool's file: (a) re-activate the buried " +
                    "ancestor chain in the scene, or (b) change the driver's FindObjectsInactive.Exclude " +
                    "to Include. (b) alone is NOT sufficient and would trade one failure for another - " +
                    "Fabricator does every registration in OnEnable (Fabricator.cs:605-629: " +
                    "RegisterActiveFabricator, InteractableRegistry.RegisterTree, " +
                    "BaseLogisticsNetwork.RegisterFabricator, TryRegister for SlowTick), and OnEnable " +
                    "never runs on an inactive GameObject, so the found component would be unregistered " +
                    "and never SlowTick, meaning craft progress could not advance to completion.");
            }

            ReportPrefabState();

            Debug.Log(
                Marker + " AUDIT END nothing was written. This is a static scene/asset read and proves " +
                "nothing about runtime: no craft was started and no signal was observed. That needs a " +
                "headless or Play Mode run.");
        }

        private static List<FabricatorSighting> CollectSightings()
        {
            // COLD ALLOC: editor-only diagnostic, not a dispatcher tick path.
            List<FabricatorSighting> sightings = new List<FabricatorSighting>(8);

            Fabricator[] all = UnityEngine.Object.FindObjectsByType<Fabricator>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < all.Length; i++)
            {
                Fabricator fabricator = all[i];
                if (fabricator == null)
                    continue;

                GameObject go = fabricator.gameObject;

                // A preview scene built by this tool or a sibling is not project content.
                Scene scene = go.scene;
                if (!scene.IsValid())
                    continue;

                ResolveInactiveAncestors(go.transform, out string highestInactivePath, out int inactiveCount);

                bool hasNonTriggerCollider = false;
                Collider[] colliders = go.GetComponents<Collider>();
                for (int c = 0; c < colliders.Length; c++)
                {
                    if (colliders[c] != null && !colliders[c].isTrigger)
                    {
                        hasNonTriggerCollider = true;
                        break;
                    }
                }

                int recipeCount = 0;
                IReadOnlyList<RecipeData> recipes = fabricator.AvailableRecipes;
                if (recipes != null)
                    recipeCount = recipes.Count;

                sightings.Add(new FabricatorSighting
                {
                    SceneName = scene.name,
                    ScenePath = scene.path,
                    ObjectPath = BuildHierarchyPath(go.transform),
                    ComponentEnabled = fabricator.enabled,
                    GameObjectActiveSelf = go.activeSelf,
                    GameObjectActiveInHierarchy = go.activeInHierarchy,
                    HighestInactiveAncestorPath = highestInactivePath,
                    InactiveAncestorCount = inactiveCount,
                    RecipeCount = recipeCount,
                    HasNonTriggerCollider = hasNonTriggerCollider,
                    Layer = go.layer,
                });
            }

            return sightings;
        }

        /// <summary>
        /// Walks strictly upward from the component's own transform and reports the OUTERMOST ancestor
        /// with activeSelf false, because that is the one an operator has to re-enable - re-enabling an
        /// inner node under a disabled parent changes nothing observable.
        /// </summary>
        private static void ResolveInactiveAncestors(
            Transform self,
            out string highestInactivePath,
            out int inactiveCount)
        {
            highestInactivePath = "none";
            inactiveCount = 0;

            if (self == null)
                return;

            Transform cursor = self.parent;
            while (cursor != null)
            {
                if (!cursor.gameObject.activeSelf)
                {
                    inactiveCount++;
                    highestInactivePath = BuildHierarchyPath(cursor);
                }

                cursor = cursor.parent;
            }
        }

        private static string BuildHierarchyPath(Transform target)
        {
            if (target == null)
                return "<null>";

            StringBuilder path = new StringBuilder(128);
            path.Append(target.name);

            Transform cursor = target.parent;
            while (cursor != null)
            {
                path.Insert(0, '/');
                path.Insert(0, cursor.name);
                cursor = cursor.parent;
            }

            return path.ToString();
        }

        private static void ReportPrefabState()
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (asset == null)
            {
                Debug.Log(
                    Marker + " PREFAB ABSENT " + PrefabPath + " does not exist. A prefab is text YAML " +
                    "and therefore reviewable, unlike the binary world scene, so authoring one gives " +
                    "this lane a durable artifact. Create it with the menu item '" + AuthorMenuPath +
                    "' or with " + ApplyFlag + ".");
                return;
            }

            if (asset.TryGetComponent(out Fabricator prefabFabricator))
            {
                IReadOnlyList<RecipeData> recipes = prefabFabricator.AvailableRecipes;
                int count = recipes == null ? 0 : recipes.Count;
                Debug.Log(
                    Marker + " PREFAB OK " + PrefabPath + " carries " + nameof(Fabricator) +
                    " with recipes=" + count + ". A prefab ASSET is not in any scene, so " +
                    "FindFirstObjectByType cannot see it and its existence alone does not clear the " +
                    "CraftRepairBuild row. Use '" + InstantiateMenuPath + "' to place an instance.");
            }
            else
            {
                Debug.LogError(
                    Marker + " PREFAB DEGRADED " + PrefabPath + " exists but has no " +
                    nameof(Fabricator) + " component. This tool will not overwrite it; that is a " +
                    "human decision.");
            }
        }

        // ------------------------------------------------------------------
        //  WRITE HALF - prefab asset only, gated
        // ------------------------------------------------------------------

        /// <summary>
        /// Creates the fabricator prefab, and only when the target path is empty. Creating a file that
        /// does not exist overwrites nobody, which is what keeps this inside AGENTS.md:126. Built inside
        /// an EditorSceneManager preview scene so constructing it cannot dirty a scene another agent has
        /// open - a live session is holding the editor.
        ///
        /// Reachable by a human through the menu item, and from batchmode only through
        /// <see cref="AuthorFabricatorFromCommandLine"/> with the explicit opt-in flag.
        /// </summary>
        [MenuItem(AuthorMenuPath)]
        public static void AuthorFabricatorPrefab()
        {
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
                    ". Creating project folders is an authoring decision this tool will not make. " +
                    "Nothing was written.");
                return;
            }

            RecipeData[] recipes = LoadRecipes();
            if (recipes.Length == 0)
            {
                Debug.LogError(
                    Marker + " ABORT no RecipeData assets found under " + RecipesFolder +
                    ". Fabricator reads only its own serialised list (Fabricator.cs:101), so a prefab " +
                    "authored with zero recipes would be another dead instance that merely changes the " +
                    "blocked message. Nothing was written.");
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
                root = BuildFabricatorRoot(previewScene, recipes, out int wiredRecipeCount);
                if (root == null)
                {
                    Debug.LogError(Marker + " ABORT prefab root construction failed. Nothing was written.");
                    return;
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
                if (!success || saved == null)
                {
                    Debug.LogError(
                        Marker + " SAVE FAILED PrefabUtility.SaveAsPrefabAsset returned false for " +
                        PrefabPath);
                    return;
                }

                Debug.Log(
                    Marker + " AUTHORED " + PrefabPath + " root=" + PrefabRootName +
                    " recipes=" + wiredRecipeCount + " layer=" + InteractableLayer +
                    " collider=BoxCollider(nonTrigger) sockets=" + OutputSocketChildName + "," +
                    DeconstructOutputSocketChildName);

                Debug.LogWarning(
                    Marker + " NOT YET A FIX this prefab is an ASSET on disk. " +
                    "FindFirstObjectByType<Fabricator>(FindObjectsInactive.Exclude) at " +
                    "H8_HeadlessWorldDriver.cs:3260 scans LOADED SCENES only, so the prefab alone does " +
                    "not clear the CraftRepairBuild row. An instance has to be in the scene the driver " +
                    "actually has loaded AND active in hierarchy. From a human editor session use '" +
                    InstantiateMenuPath + "' then save; from batchmode use " +
                    nameof(InstantiateFabricatorSceneInstanceFromCommandLine) + " with " +
                    SceneApplyFlag + ", which opens " + DefaultScenePath + " and persists.");

                Debug.LogWarning(
                    Marker + " VISUAL PLACEHOLDER the station body uses the built-in cube mesh and the " +
                    "pipeline default material. It is functionally correct and visually unfinished; " +
                    "replacing mesh and material is the asset lane's work, not this tool's. Nothing " +
                    "about the crafting path depends on it.");
            }
            finally
            {
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);

                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        /// <summary>
        /// Deterministic recipe load: AssetDatabase.FindAssets returns GUIDs in unspecified order, so the
        /// paths are sorted ordinally before wiring. Without that, two runs of this tool would produce
        /// prefabs whose YAML differs only by list order, which is noise in review.
        /// </summary>
        private static RecipeData[] LoadRecipes()
        {
            if (!AssetDatabase.IsValidFolder(RecipesFolder))
                return Array.Empty<RecipeData>();

            string[] guids = AssetDatabase.FindAssets("t:" + nameof(RecipeData), new[] { RecipesFolder });
            List<string> paths = new List<string>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!string.IsNullOrEmpty(path))
                    paths.Add(path);
            }

            paths.Sort(StringComparer.Ordinal);

            List<RecipeData> loaded = new List<RecipeData>(paths.Count);
            for (int i = 0; i < paths.Count; i++)
            {
                RecipeData recipe = AssetDatabase.LoadAssetAtPath<RecipeData>(paths[i]);
                if (recipe != null)
                    loaded.Add(recipe);
            }

            return loaded.ToArray();
        }

        private static GameObject BuildFabricatorRoot(
            Scene previewScene,
            RecipeData[] recipes,
            out int wiredRecipeCount)
        {
            wiredRecipeCount = 0;

            GameObject root = new GameObject(PrefabRootName);
            SceneManager.MoveGameObjectToScene(root, previewScene);
            root.layer = InteractableLayer;

            Mesh cubeMesh = ResolveBuiltinCubeMesh(previewScene, out Material defaultMaterial);

            // Collider BEFORE the Fabricator, deliberately. Fabricator is
            // [RequireComponent(typeof(Collider))] (Fabricator.cs:62) and Collider is ABSTRACT, so Unity
            // cannot satisfy that requirement itself - AddComponent<Fabricator> on a bare GameObject logs
            // an error instead of quietly adding one. Adding a concrete BoxCollider first makes the
            // requirement already met.
            //
            // The ROOT transform stays at unit scale and the body mesh is scaled on its own child, which
            // is the same split H8_AirlockSceneAuthoring uses (root + DoorPlate_Visual). Two reasons, both
            // load-bearing: BoxCollider.size is in LOCAL space, so scaling the root as well would multiply
            // through and give a 1.6x wider collider than the visible body; and socket local positions
            // stay in true metres instead of silently meaning root-scale units.
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.isTrigger = false;
            collider.size = new Vector3(StationWidthMeters, StationHeightMeters, StationDepthMeters);
            collider.center = new Vector3(0f, StationHeightMeters * 0.5f, 0f);

            Transform bodyVisual = CreateChild(
                root.transform, BodyVisualChildName, new Vector3(0f, StationHeightMeters * 0.5f, 0f));
            bodyVisual.localScale = new Vector3(StationWidthMeters, StationHeightMeters, StationDepthMeters);
            MeshFilter bodyFilter = bodyVisual.gameObject.AddComponent<MeshFilter>();
            MeshRenderer bodyRenderer = bodyVisual.gameObject.AddComponent<MeshRenderer>();
            if (cubeMesh != null)
                bodyFilter.sharedMesh = cubeMesh;
            if (defaultMaterial != null)
                bodyRenderer.sharedMaterial = defaultMaterial;

            // Socket offsets derived from the station dimensions rather than copied as literals, so a
            // dimension change cannot leave a socket floating inside or outside the body. Both sit
            // 0.15 m clear of the front/back faces.
            const float SocketClearanceMeters = 0.15f;
            float socketDepth = (StationDepthMeters * 0.5f) + SocketClearanceMeters;

            Transform outputSocket = CreateChild(
                root.transform,
                OutputSocketChildName,
                new Vector3(0f, StationHeightMeters * 0.55f, socketDepth));
            Transform deconstructSocket = CreateChild(
                root.transform,
                DeconstructOutputSocketChildName,
                new Vector3(0f, StationHeightMeters * 0.45f, -socketDepth));
            Transform previewHost = CreateChild(
                root.transform,
                AssemblyPreviewChildName,
                new Vector3(0f, StationHeightMeters + 0.35f, 0f));

            MeshFilter previewFilter = previewHost.gameObject.AddComponent<MeshFilter>();
            MeshRenderer previewRenderer = previewHost.gameObject.AddComponent<MeshRenderer>();
            if (cubeMesh != null)
                previewFilter.sharedMesh = cubeMesh;

            // Fabricator.cs:3458 disables this renderer itself when no assembly is in flight; starting it
            // disabled means the hologram never shows a stale frame before the first craft.
            previewRenderer.enabled = false;

            Fabricator fabricator = root.AddComponent<Fabricator>();
            if (fabricator == null)
            {
                Debug.LogError(
                    Marker + " ABORT AddComponent<Fabricator> returned null. Check that Hecton8.Editor " +
                    "still references Hecton8.Core, the assembly Fabricator.cs compiles into.");
                UnityEngine.Object.DestroyImmediate(root);
                return null;
            }

            SerializedObject so = new SerializedObject(fabricator);

            SerializedProperty recipesProp = so.FindProperty(RecipesField);
            if (recipesProp == null)
            {
                Debug.LogError(
                    Marker + " ABORT serialised field '" + RecipesField + "' not found on " +
                    nameof(Fabricator) + ". It was renamed; this tool and " +
                    "FabricationBootstrapAuthoring.cs:440 both need updating. Nothing was written.");
                UnityEngine.Object.DestroyImmediate(root);
                return null;
            }

            recipesProp.arraySize = recipes.Length;
            for (int i = 0; i < recipes.Length; i++)
                recipesProp.GetArrayElementAtIndex(i).objectReferenceValue = recipes[i];
            wiredRecipeCount = recipes.Length;

            SerializedProperty nameProp = so.FindProperty(FabricatorNameField);
            if (nameProp != null)
                nameProp.stringValue = DisplayName;

            SetObjectReference(so, OutputSocketField, outputSocket);
            SetObjectReference(so, DeconstructOutputSocketField, deconstructSocket);
            SetObjectReference(so, AssemblyPreviewMeshFilterField, previewFilter);
            SetObjectReference(so, AssemblyPreviewRendererField, previewRenderer);
            SetObjectReference(so, AssemblyFallbackMeshField, cubeMesh);
            SetObjectReference(
                so,
                HologramMaterialField,
                AssetDatabase.LoadAssetAtPath<Material>(HologramMaterialPath));

            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static void SetObjectReference(SerializedObject so, string fieldName, UnityEngine.Object value)
        {
            if (so == null || string.IsNullOrEmpty(fieldName))
                return;

            SerializedProperty property = so.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogWarning(
                    Marker + " FIELD MISSING '" + fieldName + "' is not a serialised member of " +
                    nameof(Fabricator) + " any more. Left unwired rather than guessed.");
                return;
            }

            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                Debug.LogWarning(
                    Marker + " FIELD TYPE '" + fieldName + "' is " + property.propertyType +
                    ", not an object reference. Left unwired.");
                return;
            }

            property.objectReferenceValue = value;
        }

        private static Transform CreateChild(Transform parent, string childName, Vector3 localPosition)
        {
            GameObject child = new GameObject(childName);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.layer = parent.gameObject.layer;
            return child.transform;
        }

        /// <summary>
        /// Borrows the built-in cube mesh and the pipeline default material by creating a primitive
        /// inside the preview scene and stripping it. GraphicsSettings.defaultRenderPipeline is consulted
        /// through the primitive rather than by name so this does not hard-code a URP asset path.
        /// </summary>
        private static Mesh ResolveBuiltinCubeMesh(Scene previewScene, out Material defaultMaterial)
        {
            defaultMaterial = null;

            GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                SceneManager.MoveGameObjectToScene(probe, previewScene);

                Mesh mesh = null;
                if (probe.TryGetComponent(out MeshFilter filter))
                    mesh = filter.sharedMesh;

                if (probe.TryGetComponent(out MeshRenderer renderer))
                    defaultMaterial = renderer.sharedMaterial;

                return mesh;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        // ------------------------------------------------------------------
        //  SCENE INSTANCE - human only, never from batchmode
        // ------------------------------------------------------------------

        /// <summary>
        /// Puts one prefab instance into the ALREADY-OPEN scene and marks it dirty WITHOUT saving, which is
        /// the convention FabricationBootstrapAuthoring.cs:240 set - the human decides whether the scene is
        /// saved. Deliberately not reachable from any command line: it opens nothing, so it would act on
        /// whatever scene a batchmode session happened to have loaded, and it saves nothing, so a -quit
        /// would discard the result anyway.
        ///
        /// For a batchmode run use <see cref="InstantiateFabricatorSceneInstanceFromCommandLine"/> instead.
        /// That one names its target scene, refuses on a dirty scene, and persists. This one stays because
        /// marking dirty and stopping is the right behaviour for a human already working in a scene, and
        /// because it is the only path that does not risk a 6.27 MB binary-to-YAML rewrite.
        /// </summary>
        [MenuItem(InstantiateMenuPath)]
        public static void InstantiateFabricatorIntoOpenScene()
        {
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefabAsset == null)
            {
                Debug.LogWarning(
                    Marker + " NO PREFAB " + PrefabPath + " does not exist yet, so there is nothing to " +
                    "instantiate. Run '" + AuthorMenuPath + "' first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning(
                    Marker + " NO SCENE the active scene is not valid or not loaded, so nothing was " +
                    "instantiated.");
                return;
            }

            // Idempotence has to see inactive objects too. This is the whole lesson of this lane:
            // GameObject.Find and FindObjectsInactive.Exclude both went blind the moment an ancestor was
            // disabled, and an idempotence check that inherited that blindness would happily create a
            // second fabricator next to the buried one every time it ran.
            Fabricator[] present = UnityEngine.Object.FindObjectsByType<Fabricator>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < present.Length; i++)
            {
                if (present[i] == null)
                    continue;

                GameObject go = present[i].gameObject;
                if (go.scene != scene)
                    continue;

                if (!string.Equals(go.name, PrefabRootName, StringComparison.Ordinal))
                    continue;

                Debug.Log(
                    Marker + " ALREADY PRESENT " + BuildHierarchyPath(go.transform) + " in scene '" +
                    scene.name + "' activeInHierarchy=" + go.activeInHierarchy +
                    " - nothing instantiated. If activeInHierarchy is false, an ancestor is disabled and " +
                    "adding another instance is not the fix; run '" + AuditMenuPath + "' to see which " +
                    "ancestor.");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, scene);
            if (instance == null)
            {
                Debug.LogError(
                    Marker + " INSTANTIATE FAILED PrefabUtility.InstantiatePrefab returned null for " +
                    PrefabPath);
                return;
            }

            instance.name = PrefabRootName;
            instance.transform.SetParent(null, true);
            instance.SetActive(true);

            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log(
                Marker + " INSTANTIATED " + PrefabRootName + " as a ROOT of scene '" + scene.name +
                "' at " + instance.transform.position + " activeInHierarchy=" +
                instance.activeInHierarchy + ". Parented to no one on purpose: every existing fabricator " +
                "in the world scene is unreachable because an ANCESTOR is disabled, and a root has no " +
                "ancestor to be disabled by. The scene is marked dirty and NOT saved - saving a binary " +
                "production scene is a human decision.");

            Debug.LogWarning(
                Marker + " UNPROVEN this instance has not been ticked. Nothing here shows a craft " +
                "starting or completing; Fabricator does all of its registration in OnEnable " +
                "(Fabricator.cs:605-629) and that only runs in Play Mode or a headless run. Re-run the " +
                "headless world driver to see whether the CraftRepairBuild row changes verdict.");
        }
    }
}
