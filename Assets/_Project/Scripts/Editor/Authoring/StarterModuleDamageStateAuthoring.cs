// ============================================================================
// HECTON-8 — StarterModuleDamageStateAuthoring.cs
// Gives the three starter-kit modules the damage-state owner their recipes
// already assume, and refuses to pretend the power grid is reachable.
//
// THE DEFECT (source-traced, no Unity run):
//   PFB_Module_CurrentTurbine, PFB_Module_Pylon and PFB_Module_ServicePump each
//   carry exactly one first-party component, ModuleMarker (script guid
//   8feb1a3b87c7bee43b0a4296f492c53e). There is no BaseModule.
//
//   ConstructionManager.cs:824-825 is
//     if (data != null && module.TryGetComponent(out BaseModule baseModule))
//         baseModule.ApplyBuildableTemplate(data);
//   and the save-restore block at ConstructionManager.cs:2871-2908 is guarded
//   the same way, so ApplyBuildableTemplate (BaseModule.cs:4802-4816) AND
//   SetState (ConstructionManager.cs:2897-2907) are both skipped. Those are the
//   ONLY two call sites of ApplyBuildableTemplate in the project. With no
//   BaseModule the module has no integrity, flood, breach, air-reserve, CO2,
//   repair-cap or reef-infestation state at all — not a stale one, none — so it
//   can never flood, break, breach or be repaired.
//   construction.md section 4 requires a damage state on every base module and
//   section 7 rejects a module that ignores pressure and life support.
//
//   Second-order: BaseModulePrefabIntegrityEnforcer.cs:26-27 does
//   `if (!prefabRoot.TryGetComponent(out BaseModule baseModule)) continue;`
//   so the collider/nav integrity enforcer for this exact folder silently skips
//   all three today. Adding BaseModule brings them back under that gate.
//
// WHY BaseModule AND NOT PowerNode — this is the load-bearing decision.
//   BaseModule already implements IPowerComponent (BaseModule.cs:109) and reads
//   the SAME BuildableData.powerRating that PowerNode reads:
//     BaseModule.PowerRating          -> StaticDebuffedPowerRating (BaseModule.cs:1051, :1002-1019)
//     PowerNode.PowerRating           -> _basePowerRating          (PowerNode.cs:192, :341)
//   PowerNode.OnSpawn collects every IPowerComponent on its own GameObject with
//   GetComponents(_components) (PowerNode.cs:280), and PowerGrid walks that list
//   calling AddProducer/AddConsumer once per entry with NO de-duplication by
//   source (PowerGrid.cs:1059-1081). So a GameObject carrying both components
//   registers the base rating TWICE. That defect is already live on the one
//   prefab that carries both, PFB_Module_Corridor. Reproducing it on three more
//   prefabs to chase an unreachable grid would be a regression, not a fix.
//
//   And the grid is unreachable from prefab authoring anyway: PowerGrid
//   membership needs topology, PowerNode.authoredNeighborNodes is a serialized
//   PowerNode[] (PowerNode.cs:63) that cannot name runtime instances, and
//   PowerNode.ConnectAuthoredNeighbor (PowerNode.cs:363) has ZERO non-test
//   callers project-wide — ConstructionManager.cs contains no reference to
//   PowerNode at all. ConnectAuthoredTopology therefore falls to
//   PowerGridManager.CreateGrid(this) (PowerNode.cs:458-462) and every placed
//   module would sit in its own one-node island. A PowerNode here buys a
//   double-counted rating in an island of one. Declined on both grounds.
//
//   BaseModule routes the same rating through PowerRatingForHabitatGraph
//   (BaseModule.cs:999) into HabitatGraphManager.ResolveModulePowerRating
//   (HabitatGraphManager.cs:5887-5890), which is the branch it PREFERS over the
//   raw ModuleMarker fallback at :5893. That path is damage-aware — a
//   reef-infested module generates 0 (BaseModule.cs:1006-1007), a draining one
//   adds floodPumpEnergyCost (BaseModule.cs:1011, :2744-2749) — and it is the
//   power authority that actually reaches the player today, via
//   componentSupply/componentDraw (HabitatGraphManager.cs:5183-5187) ->
//   brownout tier (:5215-5216) -> SetAmbientLightsBrownout (:5232) -> module
//   lights (BaseModule.cs:2885) and siege vulnerability (:5946-5954).
//
// ALSO DECLINED — WaterPumpModule on PFB_Module_ServicePump.
//   BaseModule already owns flood drainage for a module: floodPumpEnergyCost
//   (BaseModule.cs:469) charged while _integrityComponent.IsDraining
//   (BaseModule.cs:2744-2749). WaterPumpModule would be a second drain owner on
//   the same GameObject, it declares [RequireComponent(typeof(PowerNode))]
//   (WaterPumpModule.cs:12) which drags in the double-count above, and it
//   hard-codes powerDrawWatts = 2400f (WaterPumpModule.cs:20) against the
//   recipe's authored powerRating -8 and BaseModuleTemplate_ServicePump
//   powerDrawKW 8. Authoring a 2400 W draw no recipe or template specifies would
//   invent power truth, which construction.md section 8/8A forbids.
//
// WHAT IS DELIBERATELY LEFT ALONE:
//   • ModuleMarker.buildableData stays {fileID: 0}. Zero of the fifteen
//     marker-bearing prefabs bind it, PFB_Module_Corridor and
//     PFB_Module_Foundation included; ConstructionManager.cs:822
//     marker.Initialize(data) is the intended binder.
//   • BaseModule.moduleTemplate stays null. ApplyBuildableTemplate assigns it
//     unconditionally from the recipe (BaseModule.cs:4806), so authoring it is
//     redundant, and both sibling prefabs leave it at {fileID: 0}.
//   • No collider, socket, interiorTrigger or ModuleSocket work. The templates
//     for all three author socketDefinitions as empty, which is a separate
//     owner's problem; BaseModule needs neither (no [RequireComponent], only
//     [DisallowMultipleComponent], BaseModule.cs:108-109).
//
// WHAT IS WRITTEN, and why it is not scope creep:
//   BaseModule.fallbackPowerRating defaults to -10f (BaseModule.cs:655) and
//   powerPriority to 50 (BaseModule.cs:659). ReadBuildablePower falls back to
//   those whenever ModuleMarker.Data is null (BaseModule.cs:4787-4799), which is
//   exactly the pooled-spawn window before ConstructionManager.cs:822 binds the
//   marker. Left at the default, the starter kit's only generator reports as a
//   -10 CONSUMER during that window: the sign is inverted. ApplyBuildableTemplate
//   overwrites powerPriority (BaseModule.cs:4808) but never fallbackPowerRating,
//   so the default survives forever. Both values are copied FROM the recipe that
//   owns them, never invented, which is what PrefabAssemblerEngine.cs:841 does
//   with the same field. Recipes are resolved by their own finalPrefab pointer,
//   so there is no second copy of the prefab->recipe mapping to drift.
//
// PREFAB MUTATION ROUTE — LoadPrefabContents / SaveAsPrefabAsset /
// UnloadPrefabContents, the route AGENTS.md `Evidence Law` and
// hecton8-unity-assets.md require and that StorageEndpointAuthoring.cs:92/177/189
// and BaseModulePrefabIntegrityEnforcer.cs:20/45/50 already use here.
// SerializedObject alone cannot ADD a component, so it is not an alternative for
// this task; it is used only to write the two serialized fields.
//   THE FILEID TRAP IS GUARDED, NOT HOPED AT. All six references to these three
//   prefabs bind the ROOT GameObject fileID, not the format-stable 100100000:
//     Build_Current_Turbine.asset:22   fileID 7498929676155026764
//     Build_Utility_Pylon.asset:22     fileID 64260289537814543
//     Build_Service_Pump.asset:23      fileID 2293229834002279593
//     ProceduralFamily_family_route_power.asset:64 and :70
//     ProceduralFamily_family_service_scar.asset:64
//   If SaveAsPrefabAsset moved a root fileID, all six would break at once and
//   every recipe would spawn nothing. So this tool reads the root's local file
//   identifier with AssetDatabase.TryGetGUIDAndLocalFileIdentifier before and
//   after the write, and re-resolves the recipe's finalPrefab afterwards. A moved
//   id is reported as a hard error naming the assets to check, never as success.
// ============================================================================

using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Gameplay;
using Hecton8.Power;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Authoring
{
    /// <summary>
    /// Attaches the BaseModule damage-state owner to the three starter-kit
    /// construction prefabs that ship with ModuleMarker and nothing else.
    /// Idempotent: a second run detects the authored state and writes nothing.
    /// </summary>
    public static class StarterModuleDamageStateAuthoring
    {
        private const string LogPrefix = "[StarterModuleDamageStateAuthoring]";

        /// <summary>Folder holding the BuildableData recipes that own powerRating/powerPriority.</summary>
        private const string RecipeFolder = "Assets/_Project/Data/Construction";

        /// <summary>Serialized backing field of BaseModule.fallbackPowerRating (BaseModule.cs:655).</summary>
        private const string FallbackPowerRatingProperty = "fallbackPowerRating";

        /// <summary>Serialized backing field of BaseModule.powerPriority (BaseModule.cs:659).</summary>
        private const string PowerPriorityProperty = "powerPriority";

        /// <summary>
        /// The three prefabs this tool owns. Nothing else in the folder is touched:
        /// PFB_Module_Corridor and PFB_Module_Foundation already carry BaseModule,
        /// and the Ruin/Debris marker prefabs are world dressing, not buildables.
        /// </summary>
        private static readonly string[] TargetPrefabPaths =
        {
            "Assets/_Project/Prefabs/Construction/Final/PFB_Module_CurrentTurbine.prefab",
            "Assets/_Project/Prefabs/Construction/Final/PFB_Module_Pylon.prefab",
            "Assets/_Project/Prefabs/Construction/Final/PFB_Module_ServicePump.prefab"
        };

        // ══════════════════════════════════════════════════════════
        //  ENTRY POINT — APPLY
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Authoring entry point. Batch usage:
        /// -executeMethod Hecton8.Editor.Authoring.StarterModuleDamageStateAuthoring.ApplyStarterModuleDamageState
        /// </summary>
        [MenuItem("Hecton8/Authoring/Apply Starter Module Damage State", priority = 219)]
        public static void ApplyStarterModuleDamageState()
        {
            int written = 0;
            int unchanged = 0;
            int declined = 0;

            for (int i = 0; i < TargetPrefabPaths.Length; i++)
            {
                switch (ApplyToPrefab(TargetPrefabPaths[i]))
                {
                    case ApplyOutcome.Wrote:
                        written++;
                        break;

                    case ApplyOutcome.AlreadyAuthored:
                        unchanged++;
                        break;

                    default:
                        declined++;
                        break;
                }
            }

            if (written > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log(
                $"{LogPrefix} APPLY COMPLETE: {written} prefabs written, {unchanged} already authored, " +
                $"{declined} declined, of {TargetPrefabPaths.Length} targets. " +
                "PowerNode deliberately NOT added: BaseModule already implements IPowerComponent from the same " +
                "BuildableData.powerRating (BaseModule.cs:1051 / PowerNode.cs:192) and PowerGrid.cs:1059-1081 " +
                "would count it twice.");
        }

        private enum ApplyOutcome
        {
            Declined,
            AlreadyAuthored,
            Wrote
        }

        private static ApplyOutcome ApplyToPrefab(string prefabPath)
        {
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError($"{LogPrefix} DECLINED '{prefabPath}': prefab not found. Nothing written.");
                return ApplyOutcome.Declined;
            }

            if (!TryReadRootLocalFileId(prefabAsset, out long rootFileIdBefore))
            {
                Debug.LogError(
                    $"{LogPrefix} DECLINED '{prefabPath}': could not read the root local file identifier before " +
                    "writing, so the recipe binding could not be protected. Nothing written.");
                return ApplyOutcome.Declined;
            }

            if (!TryResolveOwningRecipe(prefabAsset, out BuildableData recipe))
            {
                Debug.LogError(
                    $"{LogPrefix} DECLINED '{prefabPath}': no BuildableData under '{RecipeFolder}' has this prefab as " +
                    "its finalPrefab, so powerRating/powerPriority have no authored owner to copy. " +
                    "Fix the recipe binding first. Nothing written.");
                return ApplyOutcome.Declined;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError(
                    $"{LogPrefix} DECLINED '{prefabPath}': could not be opened as prefab contents. Nothing written.");
                return ApplyOutcome.Declined;
            }

            bool wroteAsset = false;
            bool baseModuleExisted = false;

            try
            {
                if (prefabRoot.TryGetComponent(out PowerNode _))
                {
                    Debug.LogError(
                        $"{LogPrefix} DECLINED '{prefabPath}': root already carries a PowerNode. Adding BaseModule " +
                        "beside it would double-count BuildableData.powerRating — PowerNode.cs:280 collects every " +
                        "IPowerComponent on the GameObject and PowerGrid.cs:1059-1081 registers each one without " +
                        "de-duplicating by source. Resolve the power ownership collision first. Nothing written.");
                    return ApplyOutcome.Declined;
                }

                baseModuleExisted = prefabRoot.TryGetComponent(out BaseModule baseModule);

                Debug.Log(
                    $"{LogPrefix} BEFORE '{prefabPath}': {DescribeComponents(prefabRoot)}, " +
                    $"rootFileId={rootFileIdBefore}, recipe='{recipe.name}' " +
                    $"(powerRating={recipe.powerRating}, powerPriority={recipe.powerPriority}).");

                if (!baseModuleExisted)
                {
                    baseModule = prefabRoot.AddComponent<BaseModule>();
                    if (baseModule == null)
                    {
                        Debug.LogError(
                            $"{LogPrefix} DECLINED '{prefabPath}': AddComponent<BaseModule> returned null. " +
                            "BaseModule declares only [DisallowMultipleComponent] (BaseModule.cs:108) so this should " +
                            "not happen; inspect the prefab root for an existing BaseModule. Nothing written.");
                        return ApplyOutcome.Declined;
                    }
                }

                bool changed = !baseModuleExisted;
                changed |= TryAlignFallbackPower(baseModule, recipe, prefabPath);

                if (!changed)
                {
                    Debug.Log(
                        $"{LogPrefix} NO CHANGE '{prefabPath}': already authors BaseModule with " +
                        $"fallbackPowerRating={recipe.powerRating} and powerPriority={recipe.powerPriority}. " +
                        "Prefab not marked dirty, not saved.");
                    return ApplyOutcome.AlreadyAuthored;
                }

                EditorUtility.SetDirty(prefabRoot);
                if (PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath) == null)
                {
                    Debug.LogError(
                        $"{LogPrefix} FAILED '{prefabPath}': SaveAsPrefabAsset returned null. " +
                        "The prefab on disk is unchanged.");
                    return ApplyOutcome.Declined;
                }

                wroteAsset = true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            if (!wroteAsset)
                return ApplyOutcome.Declined;

            if (!VerifyRootFileIdSurvived(prefabPath, rootFileIdBefore, recipe))
                return ApplyOutcome.Declined;

            Debug.Log(
                $"{LogPrefix} WROTE '{prefabPath}': BaseModule {(baseModuleExisted ? "kept" : "added")}, " +
                $"fallbackPowerRating={recipe.powerRating}, powerPriority={recipe.powerPriority} " +
                $"(copied from '{recipe.name}'). PowerNode not added by design.");
            return ApplyOutcome.Wrote;
        }

        /// <summary>
        /// Copies the recipe's authored power figures onto BaseModule's serialized fallbacks.
        /// ReadBuildablePower uses them whenever ModuleMarker.Data is still null
        /// (BaseModule.cs:4787-4799), and ApplyBuildableTemplate never rewrites
        /// fallbackPowerRating (BaseModule.cs:4802-4809), so the -10f default would
        /// otherwise invert the sign of the starter kit's only generator forever.
        /// </summary>
        private static bool TryAlignFallbackPower(BaseModule baseModule, BuildableData recipe, string prefabPath)
        {
            SerializedObject moduleObject = new SerializedObject(baseModule);
            SerializedProperty fallbackRating = moduleObject.FindProperty(FallbackPowerRatingProperty);
            SerializedProperty priority = moduleObject.FindProperty(PowerPriorityProperty);

            if (fallbackRating == null || priority == null)
            {
                Debug.LogWarning(
                    $"{LogPrefix} '{prefabPath}': BaseModule has no serialized '{FallbackPowerRatingProperty}' or " +
                    $"'{PowerPriorityProperty}' field (renamed in BaseModule.cs). Component attachment still stands; " +
                    "the pre-binding power fallback is left at its default.");
                return false;
            }

            bool changed = false;

            if (!Mathf.Approximately(fallbackRating.floatValue, recipe.powerRating))
            {
                fallbackRating.floatValue = recipe.powerRating;
                changed = true;
            }

            if (priority.intValue != recipe.powerPriority)
            {
                priority.intValue = recipe.powerPriority;
                changed = true;
            }

            if (changed)
                moduleObject.ApplyModifiedPropertiesWithoutUndo();

            return changed;
        }

        // ══════════════════════════════════════════════════════════
        //  ENTRY POINT — VERIFY (read-only)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Read-only gate. Prints the component table for every target prefab and,
        /// in batch mode, exits non-zero while any of them is still missing what it
        /// needs. Writes nothing at all — no AddComponent, no SetDirty, no save —
        /// per the automated-runner clause in AGENTS.md `Evidence Law`.
        /// Batch usage:
        /// -executeMethod Hecton8.Editor.Authoring.StarterModuleDamageStateAuthoring.VerifyStarterModuleDamageState
        /// </summary>
        [MenuItem("Hecton8/Validation/Verify Starter Module Damage State", priority = 219)]
        public static void VerifyStarterModuleDamageState()
        {
            int complete = 0;
            int incomplete = 0;

            for (int i = 0; i < TargetPrefabPaths.Length; i++)
            {
                if (VerifyPrefab(TargetPrefabPaths[i]))
                    complete++;
                else
                    incomplete++;
            }

            if (incomplete == 0)
            {
                Debug.Log(
                    $"{LogPrefix} VERIFY PASSED: {complete} of {TargetPrefabPaths.Length} starter modules carry " +
                    "BaseModule with recipe-aligned fallback power. ConstructionManager.cs:824-825 and " +
                    "ConstructionManager.cs:2871-2908 now reach ApplyBuildableTemplate and SetState. " +
                    "STILL UNPROVEN AT RUNTIME: ConstructionManager.catalog is [SerializeField] private with no " +
                    "setter and GameBootstrapper adds the component bare, so no Play Mode claim follows from this.");
                return;
            }

            Debug.LogError(
                $"{LogPrefix} VERIFY FAILED: {incomplete} of {TargetPrefabPaths.Length} starter modules incomplete " +
                $"({complete} complete). Run ApplyStarterModuleDamageState.");

            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }

        private static bool VerifyPrefab(string prefabPath)
        {
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError($"{LogPrefix} VERIFY '{prefabPath}': prefab not found.");
                return false;
            }

            bool hasBaseModule = prefabAsset.TryGetComponent(out BaseModule baseModule);
            bool hasPowerNode = prefabAsset.TryGetComponent(out PowerNode _);
            bool recipeResolved = TryResolveOwningRecipe(prefabAsset, out BuildableData recipe);
            TryReadRootLocalFileId(prefabAsset, out long rootFileId);

            float authoredFallback = 0f;
            int authoredPriority = 0;
            bool fieldsReadable = false;
            if (hasBaseModule)
            {
                SerializedObject moduleObject = new SerializedObject(baseModule);
                SerializedProperty fallbackRating = moduleObject.FindProperty(FallbackPowerRatingProperty);
                SerializedProperty priority = moduleObject.FindProperty(PowerPriorityProperty);
                fieldsReadable = fallbackRating != null && priority != null;
                if (fieldsReadable)
                {
                    authoredFallback = fallbackRating.floatValue;
                    authoredPriority = priority.intValue;
                }
            }

            bool powerAligned = hasBaseModule && fieldsReadable && recipeResolved &&
                                Mathf.Approximately(authoredFallback, recipe.powerRating) &&
                                authoredPriority == recipe.powerPriority;

            // A PowerNode beside BaseModule is a failure, not a bonus: PowerNode.cs:280 plus
            // PowerGrid.cs:1059-1081 would register BuildableData.powerRating twice.
            bool passed = hasBaseModule && !hasPowerNode && powerAligned;

            Debug.Log(
                $"{LogPrefix} VERIFY '{prefabPath}': " +
                $"{DescribeComponents(prefabAsset)}, rootFileId={rootFileId}, " +
                $"recipe={(recipeResolved ? recipe.name : "UNRESOLVED")}, " +
                $"recipePowerRating={(recipeResolved ? recipe.powerRating.ToString() : "n/a")}, " +
                $"recipePowerPriority={(recipeResolved ? recipe.powerPriority.ToString() : "n/a")}, " +
                $"authoredFallbackPowerRating={(fieldsReadable ? authoredFallback.ToString() : "n/a")}, " +
                $"authoredPowerPriority={(fieldsReadable ? authoredPriority.ToString() : "n/a")}, " +
                $"powerAligned={powerAligned} => {(passed ? "COMPLETE" : "INCOMPLETE")}.");

            if (!hasBaseModule)
            {
                Debug.LogError(
                    $"{LogPrefix} VERIFY '{prefabPath}': MISSING BaseModule. ConstructionManager.cs:824-825 and " +
                    "ConstructionManager.cs:2871-2908 are both TryGetComponent(out BaseModule)-guarded, so " +
                    "ApplyBuildableTemplate and SetState are no-ops and this module has no integrity, flood, " +
                    "breach, air-reserve, CO2, repair-cap or reef state. construction.md section 4 requires a " +
                    "damage state; section 7 rejects a module that ignores pressure and life support.");
            }

            if (hasPowerNode)
            {
                Debug.LogError(
                    $"{LogPrefix} VERIFY '{prefabPath}': PowerNode present alongside BaseModule. Both expose " +
                    "BuildableData.powerRating as IPowerComponent.PowerRating (BaseModule.cs:1051, PowerNode.cs:192) " +
                    "and PowerGrid.cs:1059-1081 registers every entry of PowerNode.Components without " +
                    "de-duplicating by source, so this module's rating is counted twice.");
            }

            if (hasBaseModule && !powerAligned)
            {
                Debug.LogError(
                    $"{LogPrefix} VERIFY '{prefabPath}': BaseModule fallback power does not match its recipe. " +
                    "ReadBuildablePower (BaseModule.cs:4787-4799) uses fallbackPowerRating whenever " +
                    "ModuleMarker.Data is null, and ApplyBuildableTemplate never rewrites it, so a stale value " +
                    "misreports this module's generation or draw for the whole pre-binding window.");
            }

            return passed;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — RECIPE RESOLUTION AND FILEID PROTECTION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Finds the BuildableData whose finalPrefab IS this prefab. Resolving forward
        /// through the live binding means this tool never keeps a second copy of the
        /// prefab-to-recipe mapping, and a broken binding surfaces as a decline rather
        /// than as a silent write against the wrong numbers.
        /// </summary>
        private static bool TryResolveOwningRecipe(GameObject prefabAsset, out BuildableData recipe)
        {
            recipe = null;
            if (prefabAsset == null)
                return false;

            string prefabAssetPath = AssetDatabase.GetAssetPath(prefabAsset);
            if (string.IsNullOrEmpty(prefabAssetPath))
                return false;

            // COLD ALLOC: string[] from AssetDatabase.FindAssets - one-shot editor recipe scan - owner: StarterModuleDamageStateAuthoring
            string[] recipeGuids = AssetDatabase.FindAssets("t:BuildableData", new[] { RecipeFolder });
            for (int i = 0; i < recipeGuids.Length; i++)
            {
                string recipePath = AssetDatabase.GUIDToAssetPath(recipeGuids[i]);
                BuildableData candidate = AssetDatabase.LoadAssetAtPath<BuildableData>(recipePath);
                if (candidate == null || candidate.finalPrefab == null)
                    continue;

                // Compared by asset path, not by object reference: a reimport can hand back a
                // different managed instance for the same asset, and a reference test would then
                // report a healthy binding as broken.
                if (!string.Equals(AssetDatabase.GetAssetPath(candidate.finalPrefab), prefabAssetPath, System.StringComparison.Ordinal))
                    continue;

                recipe = candidate;
                return true;
            }

            return false;
        }

        private static bool TryReadRootLocalFileId(GameObject prefabAsset, out long localFileId)
        {
            localFileId = 0L;
            return prefabAsset != null &&
                   AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefabAsset, out string _, out localFileId);
        }

        /// <summary>
        /// Proves the root fileID did not move across SaveAsPrefabAsset. Every reference
        /// to these prefabs binds (guid, root fileID) rather than the format-stable
        /// 100100000, so this single invariant covers all of them at once; the recipe
        /// re-resolution below is the end-to-end confirmation.
        /// </summary>
        private static bool VerifyRootFileIdSurvived(string prefabPath, long rootFileIdBefore, BuildableData recipe)
        {
            GameObject reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (reloaded == null)
            {
                Debug.LogError(
                    $"{LogPrefix} POST-WRITE CHECK FAILED '{prefabPath}': the prefab no longer loads. " +
                    "Restore it before trusting any recipe binding.");
                return false;
            }

            if (!TryReadRootLocalFileId(reloaded, out long rootFileIdAfter))
            {
                Debug.LogError(
                    $"{LogPrefix} POST-WRITE CHECK FAILED '{prefabPath}': root local file identifier unreadable " +
                    "after the write. Verify the recipe and ProceduralFamily bindings by hand.");
                return false;
            }

            if (rootFileIdAfter != rootFileIdBefore)
            {
                Debug.LogError(
                    $"{LogPrefix} ROOT FILEID MOVED '{prefabPath}': {rootFileIdBefore} -> {rootFileIdAfter}. " +
                    "Every reference to this prefab binds the root GameObject fileID, so the build recipe under " +
                    $"'{RecipeFolder}' and the ProceduralFamily variant catalogs under " +
                    "'Assets/_Project/Data/World/ProceduralFamilies' now point at nothing and the module will spawn " +
                    "as null. Revert this prefab and rebind before proceeding.");
                return false;
            }

            // Path comparison, not reference comparison: SaveAsPrefabAsset triggers a reimport and
            // the recipe's resolved instance may legitimately differ while the on-disk binding is intact.
            string reboundPath = recipe != null && recipe.finalPrefab != null
                ? AssetDatabase.GetAssetPath(recipe.finalPrefab)
                : string.Empty;
            if (recipe != null && !string.Equals(reboundPath, prefabPath, System.StringComparison.Ordinal))
            {
                Debug.LogError(
                    $"{LogPrefix} RECIPE BINDING BROKEN '{prefabPath}': '{recipe.name}'.finalPrefab now resolves to " +
                    $"'{(string.IsNullOrEmpty(reboundPath) ? "null" : reboundPath)}' instead. Revert and rebind.");
                return false;
            }

            Debug.Log(
                $"{LogPrefix} ROOT FILEID STABLE '{prefabPath}': {rootFileIdAfter} unchanged across " +
                $"SaveAsPrefabAsset, and '{(recipe != null ? recipe.name : "n/a")}'.finalPrefab still resolves. " +
                "Recipe and ProceduralFamily bindings intact.");
            return true;
        }

        private static string DescribeComponents(GameObject root)
        {
            if (root == null)
                return "no root";

            bool hasMarker = root.TryGetComponent(out ModuleMarker marker);
            bool hasBaseModule = root.TryGetComponent(out BaseModule _);
            bool hasPowerNode = root.TryGetComponent(out PowerNode _);
            bool hasCollider = root.TryGetComponent(out Collider rootCollider);

            return $"ModuleMarker={(hasMarker ? "present" : "ABSENT")}" +
                   $"(data={(hasMarker && marker.Data != null ? marker.Data.name : "unbound")}), " +
                   $"BaseModule={(hasBaseModule ? "present" : "ABSENT")}, " +
                   $"PowerNode={(hasPowerNode ? "present" : "absent-by-design")}, " +
                   $"rootCollider={(hasCollider ? rootCollider.GetType().Name : "ABSENT")}";
        }
    }
}
