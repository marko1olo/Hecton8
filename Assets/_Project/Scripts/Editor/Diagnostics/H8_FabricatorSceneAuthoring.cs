using System;
using System.Collections.Generic;
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
    /// text YAML: reviewable, diffable, and re-instantiable into any scene. So the write half of this
    /// tool produces a prefab, and putting an instance into a scene is a separate, human-only step whose
    /// placement is an authoring decision this tool will not guess. The prefab also gives the lane a
    /// durable artifact that does not live only inside an unreadable binary blob.
    ///
    /// WHY THE PREFAB WRITE IS SAFE UNDER AGENTS.md:126. The Sandbox Firewall forbids an automated runner
    /// from calling PrefabUtility.SaveAsPrefabAsset or EditorUtility.SetDirty on production assets. This
    /// tool saves only when the target path holds no asset at all; if anything is already there it
    /// reports and returns without touching it, so it can never overwrite authored work. It never calls
    /// EditorSceneManager.SaveScene. The scene-instantiate entry point marks the scene dirty and leaves
    /// saving to the human, which is the convention FabricationBootstrapAuthoring.cs:240 already set.
    ///
    /// BATCHMODE CONTRACT, matching H8_HazardPrefabAuthoring.cs:243-281 and H8_AirlockSceneAuthoring.cs.
    /// Every entry point is a public static void with no arguments, so -executeMethod can reach it. A
    /// bare -executeMethod REPORTS AND WRITES NOTHING; the prefab write needs the explicit opt-in flag
    /// -h8ApplyFabricator or a human MenuItem click. No EditorUtility.DisplayDialog, no Selection, no
    /// EditorApplication.Exit (it would kill the host job), no [InitializeOnLoadMethod]. Every entry
    /// point is idempotent and logs one line per action naming what and where.
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
                    AuthorMenuPath + "'. Putting an instance into a scene is human-only and is NOT " +
                    "reachable from batchmode at all. The reachability report follows.");
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
                    "not clear the CraftRepairBuild row. An instance has to be in the open scene AND " +
                    "active in hierarchy. Use '" + InstantiateMenuPath + "'.");

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
        /// Puts one prefab instance into the active scene and marks the scene dirty. Deliberately NOT
        /// reachable from <see cref="AuthorFabricatorFromCommandLine"/>: a scene write is the operation
        /// AGENTS.md:126 is strictest about, three of the four scenes here are binary, and an automated
        /// scene save is precisely what buried the existing fabricators in the first place.
        ///
        /// It marks dirty and stops rather than calling EditorSceneManager.SaveScene, which is the
        /// convention FabricationBootstrapAuthoring.cs:240 already set - the human decides whether the
        /// scene is saved.
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
