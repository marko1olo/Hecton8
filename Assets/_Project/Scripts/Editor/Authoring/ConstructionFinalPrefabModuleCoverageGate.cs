// ============================================================================
// HECTON-8 — ConstructionFinalPrefabModuleCoverageGate.cs
//
// FIRST_20_MINUTES moment served: "Craft/repair/build". A recipe whose prefab has
// no BaseModule places an object that can never flood, break, breach or be
// repaired, so the one base-support action the vertical slice requires cannot
// change route safety. That is the route blocker this gate makes visible.
//
// THE COVERAGE LIE THIS GATE CLOSES
// ---------------------------------------------------------------------------
// BaseModulePrefabIntegrityEnforcer.EnforceBaseModulePrefabIntegrity
// (BaseModulePrefabIntegrityEnforcer.cs:14-56) opens every prefab under
// Assets/_Project/Prefabs/Construction/Final and then, at :26-27, does
//     if (!prefabRoot.TryGetComponent(out BaseModule baseModule))
//         continue;
// A prefab with no BaseModule is therefore never enforced, never reported, and
// never counted. Worse, the enforcer prints NOTHING at all - no per-prefab line,
// no summary, no result - so "it ran clean" is indistinguishable from "it skipped
// everything". Measured against the live assets, three of the five recipe
// finalPrefabs in that folder are silently skipped on every run:
//     PFB_Module_CurrentTurbine   no BaseModule
//     PFB_Module_Pylon            no BaseModule
//     PFB_Module_ServicePump      no BaseModule
// and the other two are enforced but carry `moduleTemplate: {fileID: 0}`:
//     PFB_Module_Corridor         BaseModule present, template NULL
//     PFB_Module_Foundation       BaseModule present, template NULL
//
// A SECOND, LARGER HOLE THE ENFORCER CANNOT SEE. Its folder constant is
// Assets/_Project/Prefabs/Construction/Final, but only five of the ten recipe
// finalPrefabs live there. The other five resolve to
// Assets/_Project/Art/Baked/Structures/Agent1712 - H8_A1712_Airlock_01,
// _Junction_01, _ReactorRoom_01, _ServiceCap_01, _VerticalShaft_01 - and are
// outside the enforcer's scan entirely, enforced or skipped by nobody. All five
// were checked and all five do carry BaseModule with a non-null template, so
// this hole is currently empty; it is reported anyway, because an empty hole that
// nobody watches is how the first four got there. This gate resolves its target
// set from BuildableData.finalPrefab forward, so it follows a recipe wherever its
// prefab lives.
//
// SKIPPING IS NOT ALWAYS WRONG - the distinction that makes this gate honest.
// Five of the ten prefabs in the enforcer's folder are world dressing that no
// recipe binds: PFB_Debris_ScrapCluster, PFB_Debris_WreckField,
// PFB_Ruin_ClusterMedium, PFB_Ruin_Megastructure, PFB_SargassumCollapseChunk.
// They correctly have no BaseModule and must not acquire one. So the fix is NOT
// "report every skip" - a gate that failed on scrap clusters would be turned off
// within a week. It is "report every skip, and fail only where a BuildableData
// actually binds the prefab as its finalPrefab". Recipe binding is the line
// between a module and a rock.
//
// WHY THE NULL PREFAB-SIDE moduleTemplate IS A REAL DEFECT
// ---------------------------------------------------------------------------
// This contradicts a stated ruling in a sibling tool, so it is argued rather
// than asserted. StarterModuleDamageStateAuthoring.cs:79-81 records
// "BaseModule.moduleTemplate stays null. ApplyBuildableTemplate assigns it
// unconditionally from the recipe (BaseModule.cs:4806), so authoring it is
// redundant". That is true for the ConstructionManager path and only for it.
// ApplyBuildableTemplate has exactly two call sites, ConstructionManager.cs:825
// (placement) and :2873 (save restore). Neither runs on the procedural scatter
// route, and these prefabs are on it:
//     ProceduralFamily_family_ruin_module_single.asset:65  PFB_Module_Foundation
//     ProceduralFamily_family_ruin_module_single.asset:71  PFB_Module_Corridor
//     ProceduralFamily_family_route_power.asset:64         PFB_Module_Pylon
//     ProceduralFamily_family_route_power.asset:70         PFB_Module_CurrentTurbine
//     ProceduralFamily_family_service_scar.asset:64        PFB_Module_ServicePump
// The scatter route instantiates the prefab without a BuildableData, so
// ApplyBuildableTemplate never fires. The only other writer is
// BaseModule.ReadBuildablePower (BaseModule.cs:4787-4799), which assigns
// moduleTemplate from ModuleMarker.Data - and every marker-bearing prefab in the
// kit leaves buildableData at {fileID: 0}. So for every scattered instance
// moduleTemplate is null for the object's whole life, and:
//     • ResolveThermalSurfaceAreaSquareMeters (BaseModule.cs:2207-2219) loses the
//       real envelope and falls back to the interior trigger or a hard 4 m cube;
//     • TryGetDegradationSockets (BaseModule.cs:5782) returns nothing, so a
//       damaged module emits no leak, spark or vent VFX at any integrity;
//     • the module hash falls back off the template (BaseModule.cs:888-889);
//     • ResolveStructuralAnchorRole / ResolveEmergencyAirlockRole (BaseModule.cs:4848,
//       :4861) drop to string comparison against a persistent id.
// Authoring the field costs one object reference and removes all four for the
// scatter route, while changing nothing on the ConstructionManager route, which
// overwrites it anyway. The sibling's reasoning holds for its own scope; the
// conclusion does not generalise to the scattered instances.
//
// OWNERSHIP - WHAT THIS TOOL DOES NOT DO
//   It never adds a BaseModule. StarterModuleDamageStateAuthoring is the single
//   owner of that mutation for the three prefabs that lack one, complete with its
//   own fileID guard and its own PowerNode refusal. Two tools adding the same
//   component to the same prefab is a second owner for one fact. This gate
//   REPORTS the missing component and names that tool as the fix.
//   It also does not touch the enforcer itself: colliders, MeshCollider removal
//   and BaseModuleNavModifier stay that file's business.
//
// POWERGRID DOUBLE-COUNT - DETECTED, NEVER CREATED
//   BaseModule and PowerNode both expose BuildableData.powerRating as
//   IPowerComponent.PowerRating (BaseModule.cs:1051 via :4791/:4807;
//   PowerNode.cs:192 via :341). PowerNode.OnSpawn collects every IPowerComponent
//   on its own GameObject with GetComponents (PowerNode.cs:274-280) and PowerGrid
//   walks that list calling AddProducer/AddConsumer once per entry with no
//   de-duplication by source (PowerGrid.cs:1059-1081). A GameObject carrying both
//   therefore registers its rating twice. That is LIVE today on exactly one
//   prefab, PFB_Module_Corridor (powerRating -6, so it draws -12 kW). It is NOT
//   repaired: PowerGrid and PowerNode are core, the defect is theirs, and picking
//   which of the two components loses its rating is a power-ownership decision
//   this tool has no authority to make.
//   It is reported as ESCALATED, counted separately, and deliberately EXCLUDED
//   from this gate's exit code. Folding a defect the gate cannot fix into its
//   failure count would make the gate permanently red, and a gate that can never
//   pass gets switched off - taking the coverage failures it does own with it. It
//   raises the log level instead, so it stays visible without becoming a wall.
//   Writing moduleTemplate on a BaseModule that already exists creates no new
//   IPowerComponent, so the repair below cannot widen the defect either.
//
// PREFAB MUTATION ROUTE — LoadPrefabContents / SaveAsPrefabAsset /
// UnloadPrefabContents, per AGENTS.md `Evidence Law` and hecton8-unity-assets.md.
// SerializedObject is used only to write the one object-reference field, because
// BaseModule.moduleTemplate is [SerializeField] private (BaseModule.cs:346) with
// a getter and no setter (:874).
//   THE FILEID TRAP IS GUARDED. Every reference to these prefabs binds the ROOT
//   GameObject fileID, not the format-stable 100100000 - the recipe finalPrefab
//   rows and the ProceduralFamily prefab rows listed above all carry an explicit
//   fileID. PFB_Module_Foundation's root is 5760737024609812604 and
//   PFB_Module_Corridor's is 289323947487979299. If SaveAsPrefabAsset moved
//   either, the recipe and the scatter family would both point at nothing and the
//   module would spawn as null. So the root local file identifier is read before
//   and after every write and a move is reported as a hard error naming the
//   assets to check, never as success.
//
// PROOF CLASS: static asset-graph authoring in the Editor. A PASS means every
// recipe finalPrefab carries a module owner bound to its recipe's template. It is
// not Play Mode, placement, or profiler proof - and per
// ConstructionCatalogRepairAuthoring.cs:98-138 the catalog does not reach the
// runtime at all yet, so no Play Mode claim follows from a PASS here.
// ============================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Gameplay;
using Hecton8.Power;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Authoring
{
    /// <summary>
    /// Closes the reporting hole in
    /// <c>BaseModulePrefabIntegrityEnforcer</c> (which silently
    /// <c>continue</c>s past any prefab without a <see cref="BaseModule"/>) and
    /// repairs the prefab-side <c>moduleTemplate</c> binding on the prefabs that
    /// already carry a BaseModule. Idempotent: a second run writes nothing.
    /// </summary>
    public static class ConstructionFinalPrefabModuleCoverageGate
    {
        private const string LogPrefix = "[FinalPrefabModuleCoverage]";

        /// <summary>
        /// The folder BaseModulePrefabIntegrityEnforcer scans
        /// (BaseModulePrefabIntegrityEnforcer.cs:11). Mirrored so this gate can say
        /// which prefabs that enforcer sees and which it never enumerates.
        /// </summary>
        private const string EnforcerFolder = "Assets/_Project/Prefabs/Construction/Final";

        /// <summary>Folder holding the BuildableData recipes. Same literal ConstructionCatalogRepairAuthoring.cs:166 pins.</summary>
        private const string RecipeFolder = "Assets/_Project/Data/Construction";

        /// <summary>Folder holding the ProceduralFamily assets that scatter these prefabs without a BuildableData.</summary>
        private const string ProceduralFamilyFolder = "Assets/_Project/Data/World/ProceduralFamilies";

        /// <summary>Serialized backing field of BaseModule.moduleTemplate (BaseModule.cs:346).</summary>
        private const string ModuleTemplateProperty = "moduleTemplate";

        /// <summary>One prefab in the union of (enforcer folder) and (every recipe finalPrefab).</summary>
        private readonly struct PrefabRow
        {
            public PrefabRow(string prefabPath, GameObject prefabAsset, BuildableData owningRecipe, bool inEnforcerFolder)
            {
                PrefabPath = prefabPath;
                PrefabAsset = prefabAsset;
                OwningRecipe = owningRecipe;
                InEnforcerFolder = inEnforcerFolder;
            }

            public string PrefabPath { get; }

            public GameObject PrefabAsset { get; }

            /// <summary>The BuildableData whose finalPrefab is this prefab, or null for world dressing.</summary>
            public BuildableData OwningRecipe { get; }

            public bool InEnforcerFolder { get; }

            /// <summary>Recipe binding is the line between a module and a rock.</summary>
            public bool MustCarryModule => OwningRecipe != null;
        }

        // ══════════════════════════════════════════════════════════
        //  ENTRY POINT — VERIFY (read-only)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Read-only gate. Writes nothing at all - no AddComponent, no
        /// SerializedObject apply, no SetDirty, no SaveAsPrefabAsset - per the
        /// automated-runner clause in AGENTS.md `Evidence Law`. Batch usage:
        /// -executeMethod Hecton8.Editor.Authoring.ConstructionFinalPrefabModuleCoverageGate.VerifyConstructionFinalPrefabModuleCoverage
        /// </summary>
        [MenuItem("Hecton8/Validation/Verify Construction Final Prefab Module Coverage", priority = 246)]
        public static void VerifyConstructionFinalPrefabModuleCoverage()
        {
            StringBuilder report = new StringBuilder(8192);
            report.AppendLine($"{LogPrefix} FINAL PREFAB MODULE COVERAGE REPORT");
            report.AppendLine(
                "  COVERAGE RULE: BaseModulePrefabIntegrityEnforcer.cs:26-27 continues past any prefab without a " +
                "BaseModule and prints nothing, so its silence covers both 'enforced clean' and 'never looked'. " +
                "A prefab is REQUIRED to carry a module owner if and only if some BuildableData binds it as " +
                "finalPrefab; unbound prefabs in the same folder are world dressing and are listed, not failed.");

            // COLD ALLOC: List<PrefabRow>[24] - union of enforcer-folder prefabs and recipe finalPrefabs - owner: ConstructionFinalPrefabModuleCoverageGate
            List<PrefabRow> rows = new List<PrefabRow>(24);
            CollectPrefabRows(rows);

            int failureCount = 0;
            int warningCount = 0;
            int enforcerSkipCount = 0;
            int outOfEnforcerScopeCount = 0;

            // Counted and printed separately from failureCount ON PURPOSE, and
            // deliberately excluded from the exit code. The BaseModule+PowerNode
            // double-count is a PowerGrid/PowerNode defect (PowerGrid.cs:1059-1081),
            // already escalated, and not repairable from prefab authoring. Folding it
            // into failureCount would leave this gate permanently red for a reason it
            // does not own, and a gate that can never pass is a gate that gets ignored -
            // which would then hide the coverage failures it exists to catch. It is
            // printed as ESCALATED, with its own final line, so it cannot be missed.
            int escalatedCount = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                PrefabRow row = rows[i];
                GameObject prefab = row.PrefabAsset;

                bool hasBaseModule = prefab.TryGetComponent(out BaseModule baseModule);
                bool hasPowerNode = prefab.TryGetComponent(out PowerNode _);
                bool hasMarker = prefab.TryGetComponent(out ModuleMarker marker);
                BaseModuleTemplate prefabTemplate = hasBaseModule ? baseModule.ModuleTemplate : null;
                BaseModuleTemplate recipeTemplate = row.OwningRecipe != null ? row.OwningRecipe.ModuleTemplate : null;
                TryReadRootLocalFileId(prefab, out long rootFileId);

                string enforcerVerdict;
                if (!row.InEnforcerFolder)
                {
                    enforcerVerdict = "OUT-OF-ENFORCER-SCOPE";
                    outOfEnforcerScopeCount++;
                }
                else if (!hasBaseModule)
                {
                    enforcerVerdict = "SILENTLY-SKIPPED";
                    enforcerSkipCount++;
                }
                else
                {
                    enforcerVerdict = "ENFORCED";
                }

                report.AppendLine(
                    $"  PREFAB {row.PrefabPath}: rootFileId={rootFileId}, " +
                    $"recipe={(row.OwningRecipe != null ? row.OwningRecipe.name : "<unbound>")}, " +
                    $"BaseModule={(hasBaseModule ? "present" : "ABSENT")}, " +
                    $"prefabModuleTemplate={(prefabTemplate != null ? prefabTemplate.name : "NULL")}, " +
                    $"recipeModuleTemplate={(recipeTemplate != null ? recipeTemplate.name : "NULL")}, " +
                    $"ModuleMarker={(hasMarker ? "present" : "ABSENT")}" +
                    $"(data={(hasMarker && marker.Data != null ? marker.Data.name : "unbound")}), " +
                    $"PowerNode={(hasPowerNode ? "PRESENT" : "absent")}, enforcer={enforcerVerdict}.");

                // Checked BEFORE the recipe-binding branch below, not after: the
                // double-count is a property of the GameObject's components, not of
                // whether a recipe happens to bind it. An unbound prefab carrying both
                // components double-counts just as hard as a bound one, and putting this
                // after the `continue` would have silently exempted the whole
                // world-dressing set from the one check they can still fail.
                if (hasPowerNode && hasBaseModule)
                {
                    escalatedCount++;
                    report.AppendLine(
                        $"  ESCALATED {row.PrefabPath}: carries BOTH BaseModule and PowerNode. Both expose " +
                        "BuildableData.powerRating as IPowerComponent.PowerRating (BaseModule.cs:1051, " +
                        "PowerNode.cs:192), PowerNode.OnSpawn collects every IPowerComponent on its own " +
                        "GameObject (PowerNode.cs:274-280), and PowerGrid registers each entry without " +
                        $"de-duplicating by source (PowerGrid.cs:1059-1081), so recipe powerRating " +
                        $"{(row.OwningRecipe != null ? row.OwningRecipe.powerRating.ToString() : "n/a")} is " +
                        "counted twice. NOT REPAIRED HERE: PowerGrid and PowerNode are core, and choosing which " +
                        "component loses its rating is a power-ownership decision outside this tool's authority. " +
                        "NOT counted as a failure of this gate and NOT in its exit code - it is a core defect this " +
                        "gate only detects, and it must not hold the coverage gate permanently red.");
                }

                if (!row.MustCarryModule)
                {
                    warningCount++;
                    report.AppendLine(
                        $"  INFO {row.PrefabPath}: no BuildableData binds this prefab as finalPrefab, so it is " +
                        "world dressing and must NOT acquire a BaseModule. The enforcer's skip is correct here. " +
                        "Listed so the skip is visible rather than silent.");
                    continue;
                }

                if (!hasBaseModule)
                {
                    failureCount++;
                    report.AppendLine(
                        $"  FAIL {row.PrefabPath}: bound by recipe '{row.OwningRecipe.name}' but carries NO " +
                        "BaseModule, so BaseModulePrefabIntegrityEnforcer.cs:26-27 has never enforced it and " +
                        "never said so. ConstructionManager.cs:824-825 and the save-restore block at :2871-2908 " +
                        "are both TryGetComponent(out BaseModule)-guarded and are the ONLY two call sites of " +
                        "ApplyBuildableTemplate (BaseModule.cs:4802-4816), so this module has no integrity, " +
                        "flood, breach, air-reserve, CO2, repair-cap or reef state at all. FIX: run " +
                        "Hecton8.Editor.Authoring.StarterModuleDamageStateAuthoring.ApplyStarterModuleDamageState, " +
                        "which owns that mutation for this folder. This gate deliberately does not add the " +
                        "component - one owner per fact.");
                    continue;
                }

                if (recipeTemplate == null)
                {
                    failureCount++;
                    report.AppendLine(
                        $"  FAIL {row.PrefabPath}: recipe '{row.OwningRecipe.name}' has a NULL moduleTemplate, so " +
                        "there is nothing to bind onto the prefab. Repair the recipe first with " +
                        "Hecton8/Authoring/Repair Construction Catalog Bindings " +
                        "(ConstructionCatalogRepairAuthoring), then re-run this gate.");
                }
                else if (prefabTemplate == null)
                {
                    failureCount++;
                    report.AppendLine(
                        $"  FAIL {row.PrefabPath}: BaseModule.moduleTemplate is NULL while its recipe binds " +
                        $"'{recipeTemplate.name}'. ApplyBuildableTemplate would stamp it on the " +
                        "ConstructionManager path, but this prefab is also scattered by the ProceduralFamily " +
                        $"route under '{ProceduralFamilyFolder}', which instantiates it with no BuildableData at " +
                        "all - so no stamp ever happens there and ModuleMarker.buildableData is {fileID: 0} too, " +
                        "which blocks the ReadBuildablePower fallback (BaseModule.cs:4787-4799). Every scattered " +
                        "instance therefore loses its real envelope " +
                        "(ResolveThermalSurfaceAreaSquareMeters, BaseModule.cs:2207-2219), its degradation VFX " +
                        "sockets (TryGetDegradationSockets, BaseModule.cs:5782) and its template-derived hash " +
                        "(BaseModule.cs:888-889). FIX: " +
                        "ApplyConstructionFinalPrefabModuleTemplateBinding below.");
                }
                else if (!IsSameAsset(prefabTemplate, recipeTemplate))
                {
                    warningCount++;
                    report.AppendLine(
                        $"  WARN {row.PrefabPath}: prefab binds template '{prefabTemplate.name}' while recipe " +
                        $"'{row.OwningRecipe.name}' binds '{recipeTemplate.name}'. The recipe wins at placement " +
                        "and save restore (BaseModule.ApplyBuildableTemplate, BaseModule.cs:4806), so the " +
                        "scattered instances and the placed instances would run on two different geometry, " +
                        "socket, mass and air-volume contracts. Not auto-corrected: overwriting a deliberately " +
                        "different prefab-side template would erase authored intent. Decide which one is right.");
                }
            }

            report.AppendLine(
                $"  SUMMARY: failures={failureCount}, warnings={warningCount}, escalated={escalatedCount}, " +
                $"prefabsInspected={rows.Count}, silentlySkippedByEnforcer={enforcerSkipCount}, " +
                $"outsideEnforcerFolder={outOfEnforcerScopeCount}. Static asset-graph proof only - not Play Mode, " +
                "not placement proof, not profiler proof. ConstructionManager.catalog is still [SerializeField] " +
                "private with no setter and GameBootstrapper adds the component bare " +
                "(ConstructionCatalogRepairAuthoring.cs:98-138), so no runtime claim follows from a PASS.");

            if (escalatedCount > 0)
            {
                report.AppendLine(
                    $"  ESCALATION STANDS: {escalatedCount} prefab(s) double-count BuildableData.powerRating " +
                    "through PowerGrid.cs:1059-1081. Outside this gate's ownership, excluded from its exit code, " +
                    "and still broken.");
            }

            if (failureCount > 0)
            {
                report.Append($"{LogPrefix} RESULT: FAIL");
                Debug.LogError(report.ToString());
            }
            else if (warningCount > 0 || escalatedCount > 0)
            {
                // Escalations raise the LOG level but never the exit code, so an operator
                // reading the console sees them while a batch gate on this method still
                // reflects only what this gate owns.
                report.Append($"{LogPrefix} RESULT: PASS WITH WARNINGS");
                Debug.LogWarning(report.ToString());
            }
            else
            {
                report.Append($"{LogPrefix} RESULT: PASS");
                Debug.Log(report.ToString());
            }

            if (Application.isBatchMode)
                EditorApplication.Exit(failureCount > 0 ? 1 : 0);
        }

        // ══════════════════════════════════════════════════════════
        //  ENTRY POINT — APPLY
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Binds each recipe's <see cref="BaseModuleTemplate"/> onto the
        /// <see cref="BaseModule"/> its finalPrefab already carries. Adds no
        /// components, touches no colliders, and declines rather than guessing.
        /// Batch usage:
        /// -executeMethod Hecton8.Editor.Authoring.ConstructionFinalPrefabModuleCoverageGate.ApplyConstructionFinalPrefabModuleTemplateBinding
        /// </summary>
        [MenuItem("Hecton8/Authoring/Bind Final Prefab Module Templates", priority = 223)]
        public static void ApplyConstructionFinalPrefabModuleTemplateBinding()
        {
            // COLD ALLOC: List<PrefabRow>[24] - union of enforcer-folder prefabs and recipe finalPrefabs - owner: ConstructionFinalPrefabModuleCoverageGate
            List<PrefabRow> rows = new List<PrefabRow>(24);
            CollectPrefabRows(rows);

            int written = 0;
            int unchanged = 0;
            int declined = 0;
            int skippedNoBaseModule = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                PrefabRow row = rows[i];
                if (!row.MustCarryModule)
                    continue;

                if (!row.PrefabAsset.TryGetComponent(out BaseModule _))
                {
                    skippedNoBaseModule++;
                    Debug.LogWarning(
                        $"{LogPrefix} SKIPPED '{row.PrefabPath}': no BaseModule to bind a template onto. This " +
                        "tool never adds the component - " +
                        "StarterModuleDamageStateAuthoring.ApplyStarterModuleDamageState owns that mutation for " +
                        "this folder, including its own root-fileID guard and its own PowerNode refusal. Run it " +
                        "first, then re-run this binding.");
                    continue;
                }

                switch (ApplyToPrefab(row))
                {
                    case ApplyOutcome.Wrote:
                        written++;
                        break;

                    case ApplyOutcome.AlreadyBound:
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
                $"{LogPrefix} APPLY COMPLETE: {written} prefabs written, {unchanged} already bound, " +
                $"{declined} declined, {skippedNoBaseModule} skipped for having no BaseModule. No component was " +
                "added and no collider was touched. Any BaseModule+PowerNode double-count " +
                "(PowerGrid.cs:1059-1081) was reported by the verify gate and deliberately left alone: writing " +
                "moduleTemplate on an existing BaseModule adds no IPowerComponent, so this run cannot have " +
                "widened it.");
        }

        private enum ApplyOutcome
        {
            Declined,
            AlreadyBound,
            Wrote
        }

        private static ApplyOutcome ApplyToPrefab(PrefabRow row)
        {
            BaseModuleTemplate recipeTemplate = row.OwningRecipe.ModuleTemplate;
            if (recipeTemplate == null)
            {
                Debug.LogError(
                    $"{LogPrefix} DECLINED '{row.PrefabPath}': recipe '{row.OwningRecipe.name}' has a null " +
                    "moduleTemplate, so there is nothing authored to bind. Run " +
                    "Hecton8/Authoring/Repair Construction Catalog Bindings first. Nothing written.");
                return ApplyOutcome.Declined;
            }

            if (!TryReadRootLocalFileId(row.PrefabAsset, out long rootFileIdBefore))
            {
                Debug.LogError(
                    $"{LogPrefix} DECLINED '{row.PrefabPath}': could not read the root local file identifier " +
                    "before writing, so the recipe and ProceduralFamily bindings could not be protected. " +
                    "Nothing written.");
                return ApplyOutcome.Declined;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(row.PrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError(
                    $"{LogPrefix} DECLINED '{row.PrefabPath}': could not be opened as prefab contents. " +
                    "Nothing written.");
                return ApplyOutcome.Declined;
            }

            bool wroteAsset = false;
            string beforeTemplateName = "NULL";

            try
            {
                if (!prefabRoot.TryGetComponent(out BaseModule baseModule))
                {
                    Debug.LogError(
                        $"{LogPrefix} DECLINED '{row.PrefabPath}': the loaded prefab contents carry no BaseModule " +
                        "although the asset did. The prefab is mid-import. Nothing written.");
                    return ApplyOutcome.Declined;
                }

                SerializedObject moduleObject = new SerializedObject(baseModule);
                SerializedProperty templateProperty = moduleObject.FindProperty(ModuleTemplateProperty);
                if (templateProperty == null)
                {
                    Debug.LogError(
                        $"{LogPrefix} DECLINED '{row.PrefabPath}': BaseModule has no serialized " +
                        $"'{ModuleTemplateProperty}' field. It was renamed in BaseModule.cs (:346) and this tool " +
                        "is stale. Nothing written.");
                    return ApplyOutcome.Declined;
                }

                UnityEngine.Object beforeTemplate = templateProperty.objectReferenceValue;
                beforeTemplateName = beforeTemplate != null ? beforeTemplate.name : "NULL";

                // Hoisted out of the log's interpolation hole on purpose: an `out`
                // declaration inside an interpolated string is legal but unreadable, and
                // this value is a reported condition rather than a formatting detail.
                bool hasPowerNode = prefabRoot.TryGetComponent(out PowerNode _);

                Debug.Log(
                    $"{LogPrefix} BEFORE '{row.PrefabPath}': rootFileId={rootFileIdBefore}, " +
                    $"BaseModule.moduleTemplate={beforeTemplateName}, recipe='{row.OwningRecipe.name}' binds " +
                    $"'{recipeTemplate.name}', PowerNode=" +
                    $"{(hasPowerNode ? "PRESENT (pre-existing double-count, not created here)" : "absent")}.");

                if (IsSameAsset(beforeTemplate, recipeTemplate))
                {
                    Debug.Log(
                        $"{LogPrefix} NO CHANGE '{row.PrefabPath}': already binds '{recipeTemplate.name}'. " +
                        "Prefab not marked dirty, not saved.");
                    return ApplyOutcome.AlreadyBound;
                }

                if (beforeTemplate != null)
                {
                    Debug.LogError(
                        $"{LogPrefix} DECLINED '{row.PrefabPath}': already binds a DIFFERENT template " +
                        $"'{beforeTemplateName}' instead of the recipe's '{recipeTemplate.name}'. Overwriting it " +
                        "would erase authored intent, and which of the two is correct is not derivable from the " +
                        "assets. Resolve the disagreement by hand. Nothing written.");
                    return ApplyOutcome.Declined;
                }

                templateProperty.objectReferenceValue = recipeTemplate;
                moduleObject.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(prefabRoot);
                if (PrefabUtility.SaveAsPrefabAsset(prefabRoot, row.PrefabPath) == null)
                {
                    Debug.LogError(
                        $"{LogPrefix} FAILED '{row.PrefabPath}': SaveAsPrefabAsset returned null. The prefab on " +
                        "disk is unchanged.");
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

            if (!VerifyRootFileIdSurvived(row, rootFileIdBefore, recipeTemplate))
                return ApplyOutcome.Declined;

            Debug.Log(
                $"{LogPrefix} WROTE '{row.PrefabPath}': BaseModule.moduleTemplate {beforeTemplateName} -> " +
                $"'{recipeTemplate.name}' (copied from recipe '{row.OwningRecipe.name}'). No component added, " +
                "no collider touched.");
            return ApplyOutcome.Wrote;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — TARGET RESOLUTION AND FILEID PROTECTION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Builds the union of (a) every prefab in the folder
        /// BaseModulePrefabIntegrityEnforcer scans and (b) every
        /// BuildableData.finalPrefab under the recipe folder, wherever it lives.
        /// (b) is what catches the five Agent1712 finalPrefabs the enforcer never
        /// enumerates; (a) is what catches an unbound prefab sitting in the module
        /// folder. Resolving forward through the live finalPrefab pointer means
        /// there is no second copy of the prefab-to-recipe mapping to drift.
        /// </summary>
        private static void CollectPrefabRows(List<PrefabRow> rows)
        {
            // COLD ALLOC: Dictionary<string,BuildableData>[16] - recipe owner per finalPrefab asset path - owner: ConstructionFinalPrefabModuleCoverageGate
            Dictionary<string, BuildableData> recipeByPrefabPath =
                new Dictionary<string, BuildableData>(16, System.StringComparer.Ordinal);

            // COLD ALLOC: string[] from AssetDatabase.FindAssets - one-shot editor recipe scan - owner: ConstructionFinalPrefabModuleCoverageGate
            string[] recipeGuids = AssetDatabase.FindAssets("t:BuildableData", new[] { RecipeFolder });
            for (int i = 0; i < recipeGuids.Length; i++)
            {
                string recipePath = AssetDatabase.GUIDToAssetPath(recipeGuids[i]);
                BuildableData recipe = AssetDatabase.LoadAssetAtPath<BuildableData>(recipePath);
                if (recipe == null || recipe.finalPrefab == null)
                    continue;

                string prefabPath = AssetDatabase.GetAssetPath(recipe.finalPrefab);
                if (string.IsNullOrEmpty(prefabPath))
                    continue;

                // First claimant wins. A duplicate finalPrefab binding is already a
                // hard failure in ConstructionCatalogRepairAuthoring
                // .ReportDuplicateFinalPrefabBindings (:1046-1075); this gate does not
                // restate it, it just refuses to double-count the prefab.
                if (!recipeByPrefabPath.ContainsKey(prefabPath))
                    recipeByPrefabPath.Add(prefabPath, recipe);
            }

            // COLD ALLOC: HashSet<string> - dedupes the union of the two target sources - owner: ConstructionFinalPrefabModuleCoverageGate
            // Capacity is deliberately omitted: the HashSet<T>(int, IEqualityComparer<T>)
            // overload is netstandard2.1-only, and the in-repo precedent
            // (ModuleSocketLaneVocabularyGate.cs:316) uses the comparer-only ctor. A
            // 24-entry set does not need a reservation.
            HashSet<string> visited = new HashSet<string>(System.StringComparer.Ordinal);

            if (AssetDatabase.IsValidFolder(EnforcerFolder))
            {
                // COLD ALLOC: string[] from AssetDatabase.FindAssets - one-shot editor prefab scan - owner: ConstructionFinalPrefabModuleCoverageGate
                string[] folderGuids = AssetDatabase.FindAssets("t:Prefab", new[] { EnforcerFolder });
                for (int i = 0; i < folderGuids.Length; i++)
                {
                    string prefabPath = AssetDatabase.GUIDToAssetPath(folderGuids[i]);
                    AddRow(rows, visited, recipeByPrefabPath, prefabPath, true);
                }
            }
            else
            {
                Debug.LogError(
                    $"{LogPrefix} '{EnforcerFolder}' is not a valid folder, so the half of the sweep that mirrors " +
                    "BaseModulePrefabIntegrityEnforcer's scan found nothing. Only recipe finalPrefabs were " +
                    "inspected.");
            }

            foreach (KeyValuePair<string, BuildableData> pair in recipeByPrefabPath)
            {
                bool inEnforcerFolder = pair.Key.StartsWith(EnforcerFolder, System.StringComparison.Ordinal);
                AddRow(rows, visited, recipeByPrefabPath, pair.Key, inEnforcerFolder);
            }

            rows.Sort(CompareByPath);
        }

        private static void AddRow(
            List<PrefabRow> rows,
            HashSet<string> visited,
            Dictionary<string, BuildableData> recipeByPrefabPath,
            string prefabPath,
            bool inEnforcerFolder)
        {
            if (string.IsNullOrEmpty(prefabPath) || !visited.Add(prefabPath))
                return;

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError($"{LogPrefix} '{prefabPath}': prefab does not load. Excluded from the sweep.");
                return;
            }

            recipeByPrefabPath.TryGetValue(prefabPath, out BuildableData recipe);
            rows.Add(new PrefabRow(prefabPath, prefabAsset, recipe, inEnforcerFolder));
        }

        private static int CompareByPath(PrefabRow lhs, PrefabRow rhs)
        {
            return string.Compare(lhs.PrefabPath, rhs.PrefabPath, System.StringComparison.Ordinal);
        }

        private static bool TryReadRootLocalFileId(GameObject prefabAsset, out long localFileId)
        {
            localFileId = 0L;
            return prefabAsset != null &&
                   AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefabAsset, out string _, out localFileId);
        }

        /// <summary>
        /// Asset identity by (guid, local file identifier) rather than by managed
        /// reference. SaveAsPrefabAsset triggers a reimport and the AssetDatabase can
        /// legitimately hand back a different managed instance for the same asset
        /// afterwards, so <c>ReferenceEquals</c> would report an intact binding as
        /// broken - the same trap StarterModuleDamageStateAuthoring.cs:518-522 and
        /// :574-575 document for its own post-write check. Two nulls count as equal,
        /// because "both unbound" is a real matching state for the idempotency test.
        /// </summary>
        private static bool IsSameAsset(UnityEngine.Object lhs, UnityEngine.Object rhs)
        {
            bool lhsNull = lhs == null;
            bool rhsNull = rhs == null;
            if (lhsNull || rhsNull)
                return lhsNull && rhsNull;

            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(lhs, out string lhsGuid, out long lhsLocalId))
                return false;

            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(rhs, out string rhsGuid, out long rhsLocalId))
                return false;

            return lhsLocalId == rhsLocalId &&
                   string.Equals(lhsGuid, rhsGuid, System.StringComparison.Ordinal);
        }

        /// <summary>
        /// Proves the root fileID did not move across SaveAsPrefabAsset. Every
        /// reference to these prefabs binds (guid, root fileID) rather than the
        /// format-stable 100100000 - the recipe finalPrefab row and the
        /// ProceduralFamily prefab rows all carry an explicit fileID - so this one
        /// invariant covers all of them, and the recipe re-resolution below is the
        /// end-to-end confirmation.
        /// </summary>
        private static bool VerifyRootFileIdSurvived(PrefabRow row, long rootFileIdBefore, BaseModuleTemplate expectedTemplate)
        {
            GameObject reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(row.PrefabPath);
            if (reloaded == null)
            {
                Debug.LogError(
                    $"{LogPrefix} POST-WRITE CHECK FAILED '{row.PrefabPath}': the prefab no longer loads. " +
                    "Restore it before trusting any recipe or scatter binding.");
                return false;
            }

            if (!TryReadRootLocalFileId(reloaded, out long rootFileIdAfter))
            {
                Debug.LogError(
                    $"{LogPrefix} POST-WRITE CHECK FAILED '{row.PrefabPath}': root local file identifier " +
                    "unreadable after the write. Verify the recipe and ProceduralFamily bindings by hand.");
                return false;
            }

            if (rootFileIdAfter != rootFileIdBefore)
            {
                Debug.LogError(
                    $"{LogPrefix} ROOT FILEID MOVED '{row.PrefabPath}': {rootFileIdBefore} -> {rootFileIdAfter}. " +
                    $"Every reference to this prefab binds the root GameObject fileID, so the recipe under " +
                    $"'{RecipeFolder}' and the scatter catalogs under '{ProceduralFamilyFolder}' now point at " +
                    "nothing and the module will spawn as null. Revert this prefab and rebind before proceeding.");
                return false;
            }

            // Path comparison, not reference comparison: SaveAsPrefabAsset triggers a
            // reimport and the recipe's resolved instance may legitimately differ while
            // the on-disk binding is intact.
            string reboundPath = row.OwningRecipe != null && row.OwningRecipe.finalPrefab != null
                ? AssetDatabase.GetAssetPath(row.OwningRecipe.finalPrefab)
                : string.Empty;
            if (!string.Equals(reboundPath, row.PrefabPath, System.StringComparison.Ordinal))
            {
                Debug.LogError(
                    $"{LogPrefix} RECIPE BINDING BROKEN '{row.PrefabPath}': " +
                    $"'{row.OwningRecipe.name}'.finalPrefab now resolves to " +
                    $"'{(string.IsNullOrEmpty(reboundPath) ? "null" : reboundPath)}' instead. Revert and rebind.");
                return false;
            }

            BaseModuleTemplate boundTemplate = reloaded.TryGetComponent(out BaseModule reloadedModule)
                ? reloadedModule.ModuleTemplate
                : null;
            if (!IsSameAsset(boundTemplate, expectedTemplate))
            {
                Debug.LogError(
                    $"{LogPrefix} TEMPLATE WRITE LOST '{row.PrefabPath}': after the save the prefab binds " +
                    $"'{(boundTemplate != null ? boundTemplate.name : "NULL")}' instead of " +
                    $"'{expectedTemplate.name}'. The write did not survive the reimport. Investigate before " +
                    "re-running.");
                return false;
            }

            Debug.Log(
                $"{LogPrefix} ROOT FILEID STABLE '{row.PrefabPath}': {rootFileIdAfter} unchanged across " +
                $"SaveAsPrefabAsset, '{row.OwningRecipe.name}'.finalPrefab still resolves, and the prefab now " +
                $"binds '{expectedTemplate.name}'. Recipe and ProceduralFamily bindings intact.");
            return true;
        }
    }
}
#endif
