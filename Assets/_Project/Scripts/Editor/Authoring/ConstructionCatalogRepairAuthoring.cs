// ============================================================================
// HECTON-8 — ConstructionCatalogRepairAuthoring.cs
//
// FIRST_20_MINUTES moment served: "Craft/repair/build" — one base-support action
// must consume the resource and change route safety
// (Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md, Required Route
// table). Today the starter construction kit cannot deliver that moment for 8 of
// its 10 recipes. This tool is the data-binding repair for exactly that gap.
//
// WHAT IS ACTUALLY BROKEN (verified against live assets, not a report):
//
//   1. NULL moduleTemplate on three recipes that DO have a finalPrefab —
//      Build_Current_Turbine, Build_Service_Pump, Build_Utility_Pylon all carry
//      `moduleTemplate: {fileID: 0}`. ConstructionBootstrapAuthoring.CreateOrUpdate-
//      Buildable (ConstructionBootstrapAuthoring.cs:2181-2208) never writes that
//      field at all, so it was never authored. Consequences, exactly:
//        • PlayerBuilder.ResolveActivePreviewScale (PlayerBuilder.cs:1120-1130)
//          and the GhostPreviewDTO upload (PlayerBuilder.cs:2592-2604) both fall
//          back to Vector3.one, so the hologram is a 1 m cube no matter how big
//          the real module is. A 2.4 x 6.0 x 2.4 turbine previews as 1 m³.
//        • PlayerBuilder.IsStructuralBuildable (PlayerBuilder.cs:1948-1953)
//          returns false for a Utility-family recipe with a null template, so
//          UpdateTerrainSdfPlacementState (PlayerBuilder.cs:3132) skips terrain
//          and structural-integrity validation entirely.
//        • ModuleMarker.ResolveScannerEntryHash (ModuleMarker.cs:149-157) and
//          BuildableData.ModuleHashId (BuildableData.cs:213-225) fall back to the
//          recipe-name hash instead of the template hash.
//      This is a placement lie, not a cosmetic gap, which is why it is repaired
//      first.
//
//   2. NULL finalPrefab on five recipes — Build_Airlock_Hatch, Build_Junction_T,
//      Build_Junction_X, Build_Moonpool_Bay, Build_MultiPurpose_Room. These five
//      have a correct hand-authored BaseModuleTemplate but nothing to instantiate.
//      ModuleCatalog.FindPrefabById (ModuleCatalog.cs:128-132) returns null for
//      them, so neither placement nor save-restore can produce a module.
//      ConstructionBootstrapAuthoring authors only the other five
//      (ConstructionBootstrapAuthoring.cs:264-303) — that is the whole reason.
//
//   3. FIVE MISSING CATALOG ROWS — ModuleCatalog_Starter.asset lists exactly the
//      five bound recipes. CreateOrUpdateModuleCatalog does
//      `listProp.arraySize = modules.Length` (ConstructionBootstrapAuthoring.cs:2296-2309),
//      which truncates, so the other five were never members. A recipe outside
//      the catalog cannot be resolved on load (ConstructionManager.cs:2762-2777).
//
// WHY THIS FILE AND NOT AN EXTENSION OF ConstructionBootstrapAuthoring:
//   That tool is gated by WorldProceduralFinalPrefabQualityGate.AllowLegacyPrimitive-
//   FinalAuthoring (ConstructionBootstrapAuthoring.cs:46) and its catalog writer
//   truncates to its own five recipes. Running it is how the five missing rows get
//   deleted again, not how they get added. Repair therefore lives in a separate,
//   additive, idempotent owner. See ORDERING WARNING at the bottom of this header.
//
// GHOST PREFABS ARE DELIBERATELY LEFT NULL:
//   BuildableData.ghostPrefab (BuildableData.cs:89-90) is documented as legacy and
//   "Runtime builder holography ignores this field". BuilderHolographyStaticAudit
//   .NoNonZeroGhostPrefabRefs (BuilderHolographyTools.cs:612-644) walks every
//   .asset/.prefab/.unity under Assets/_Project and FAILS the
//   `noNonZeroBuildableGhostPrefabReferences` flag unless every `ghostPrefab:` line
//   is `{fileID: 0`. The placement preview is Graphics.DrawProceduralIndirect over
//   a StructuredBuffer<BuilderGhostStateRaw> (BuilderHolographyTools.cs:519-521),
//   sized from BaseModuleTemplate.ProxyBoundsSize — it instantiates nothing. This
//   tool therefore asserts ghostPrefab == null on the recipes it owns and clears it
//   if it ever drifts non-null.
//
// OWNERSHIP BOUNDARY vs ConstructionCatalogValidator:
//   ConstructionCatalogValidator.ValidateConstructionCatalog
//   (ConstructionCatalogValidator.cs:13-57) already owns the broad content audit:
//   duplicate names, PersistentId collisions, null finalPrefab, buildCost sanity,
//   catalog coverage. It is kept, not duplicated. What it cannot do is (a) check
//   moduleTemplate binding — it has no such check — and (b) fail a batch run with a
//   non-zero exit code; it only logs. VerifyConstructionCatalogBindings below adds
//   exactly those two things over the three binding facts this repair owns, and
//   nothing else.
//
// ORDERING WARNING FOR THE OPERATOR:
//   Re-running Hecton8/Authoring/Rebuild Starter Construction Kit AFTER this repair
//   truncates ModuleCatalog_Starter.asset back to five rows. Run repair last.
//
// UPSTREAM BLOCKER THIS TOOL CANNOT FIX (core territory, not data):
//   Repairing the data is necessary and NOT sufficient. The catalog never reaches the
//   runtime at all. Verified chain, each link read off live source/assets, reachability
//   checked by script GUID because scenes bind by GUID and not by class name:
//     • ModuleCatalog_Starter.asset (guid dfbd85b7b5b39644a82a0be61a5d4240) is
//       referenced by ZERO .unity, .prefab, and .asset files. It is an orphan.
//     • The ConstructionManager script guid b2c01d4999b341d45ae34bab9a99b499 appears in
//       ZERO scenes and ZERO prefabs. The component is created bare —
//       new GameObject("[ConstructionManager]") then AddComponent<ConstructionManager>()
//       in GameBootstrapper.EnsureConstructionServiceRegistered
//       (GameBootstrapper.cs:6376-6378).
//     • ConstructionManager.catalog is [SerializeField] private
//       (ConstructionManager.cs:223) with no setter and no runtime resolver; the only
//       accessor is the read-only Catalog getter (ConstructionManager.cs:432). A bare
//       AddComponent therefore leaves it null for the whole session.
//     • EnvironmentRuntimeContextService assigns its module catalog from
//       _logisticsService.Catalog (EnvironmentRuntimeContextService.cs:200, :349),
//       i.e. from that null field, and _logisticsService is the ConstructionManager
//       registered as GlobalRegistry.Logistics (same file :346-347, :360).
//     • PlayerBuilder.ResolveModuleCatalog reads the environment context
//       (PlayerBuilder.cs:4391-4394) into _buildCatalog (PlayerBuilder.cs:1505), so
//       catalog cycling has nothing to cycle and only the prefab-serialized
//       activeBuildable is ever selectable.
//     • ConstructionManager.LoadFromSaveData hard-aborts on the null catalog
//       (ConstructionManager.cs:2688-2695), while PopulateSaveData has NO such guard
//       (ConstructionManager.cs:2493-2504) — so saves keep writing module rows that can
//       never be restored.
//   ConstructionBootstrapAuthoring.AssignCatalogToScene (:2310-2320) already tries to
//   wire this with FindAnyObjectByType<ConstructionManager>() and silently no-ops,
//   because there is no scene instance to find.
//   MINIMAL CORRECT ASSIGNMENT POINT — named, deliberately NOT implemented here:
//   GameBootstrapper.EnsureConstructionServiceRegistered (:6366-6385), between the
//   AddComponent at :6378 and InitializeService at :6383. That is the only window where
//   the instance exists and nothing has read Catalog yet. It needs a core-owned
//   assignment route, because ConstructionManager exposes no setter today, plus a
//   sanctioned load path: Resources.Load is forbidden by AGENTS.md, and the in-repo
//   precedent for a bootstrap-created service acquiring an SO catalog is
//   ConstructionManager.ResolvePlayerItemCatalog (:3375-3380), which pulls ItemCatalog
//   from the already-wired PlayerInventory rather than loading it. The cheaper
//   alternative is authoring the ConstructionManager component into 00_BOOTSTRAP with
//   catalog assigned, which also makes AssignCatalogToScene start working.
// ============================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using Hecton.Localization;
using Hecton8.Building;
using Hecton8.Construction;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Authoring
{
    /// <summary>
    /// Idempotent repair for the starter construction data bindings: every
    /// <see cref="BuildableData"/> recipe under
    /// <c>Assets/_Project/Data/Construction</c> gets a valid
    /// <see cref="BaseModuleTemplate"/>, a <c>finalPrefab</c> when generated
    /// geometry exists, and a row in <c>ModuleCatalog_Starter.asset</c>.
    /// Running twice changes nothing and says so.
    /// </summary>
    public static class ConstructionCatalogRepairAuthoring
    {
        // ══════════════════════════════════════════════════════════
        //  PATHS
        // ══════════════════════════════════════════════════════════

        private const string ConstructionDataFolder = "Assets/_Project/Data/Construction";

        /// <summary>
        /// Home of the seven hand-authored production templates
        /// (BaseModuleTemplate_Airlock .. _MultiPurposeRoom). New templates created
        /// by this tool land beside them, not in AbandonedModuleTemplates, which is
        /// the procedural ruin set.
        /// </summary>
        private const string StandardTemplateFolder = ConstructionDataFolder + "/StandardModuleTemplates";

        /// <summary>
        /// Catalog path is pinned to the same literal ModuleArchitect1712 uses
        /// (ModuleArchitect1712.cs:47-48) and ConstructionBootstrapAuthoring writes
        /// (ConstructionBootstrapAuthoring.cs:308).
        /// </summary>
        private const string ModuleCatalogPath = ConstructionDataFolder + "/ModuleCatalog_Starter.asset";

        /// <summary>
        /// ModuleArchitect1712Settings.Default.OutputFolder (ModuleArchitect1712.cs:27).
        /// Absent until Hecton8/Structures/Agent 1712/Fabricate Default Module Set Now
        /// has been run at least once.
        /// </summary>
        private const string Agent1712OutputFolder = "Assets/_Project/Art/Baked/Structures/Agent1712";

        private const string LogPrefix = "[ConstructionCatalogRepair]";

        // ══════════════════════════════════════════════════════════
        //  SERIALIZED FIELD NAMES — each read off the owner source
        // ══════════════════════════════════════════════════════════

        /// <summary>BuildableData.moduleTemplate is private (BuildableData.cs:96); only SerializedObject can write it.</summary>
        private const string ModuleTemplateField = "moduleTemplate";

        /// <summary>BuildableData.finalPrefab (BuildableData.cs:93).</summary>
        private const string FinalPrefabField = "finalPrefab";

        /// <summary>BuildableData.ghostPrefab (BuildableData.cs:90).</summary>
        private const string GhostPrefabField = "ghostPrefab";

        /// <summary>ModuleCatalog.allModules is private (ModuleCatalog.cs:32).</summary>
        private const string AllModulesField = "allModules";

        // BaseModuleTemplate private serialized fields, BaseModuleTemplate.cs:73-148.
        private const string TemplateStableIdField = "stableId";                                     // :73
        private const string TemplateHashIdField = "templateHashId";                                 // :76
        private const string TemplateProxyCenterField = "proxyBoundsCenter";                         // :87
        private const string TemplateProxySizeField = "proxyBoundsSize";                             // :90
        private const string TemplatePowerDrawField = "powerDrawKW";                                 // :98
        private const string TemplateAirVolumeField = "airVolumeM3";                                 // :101
        private const string TemplateStructuralAnchorField = "isStructuralAnchor";                   // :105
        private const string TemplateEmergencyAirlockField = "isEmergencyAirlock";                   // :108
        private const string TemplateDefaultIntegrityField = "defaultIntegrityState";                // :112
        private const string TemplateFloodedBelowField = "floodedBelowIntegrityState";               // :115
        private const string TemplateOxygenOfflineField = "oxygenOfflineBelowIntegrityState";        // :118
        private const string TemplateDragAreaField = "projectedDragAreaSquareMeters";                // :122
        private const string TemplateYieldStrengthField = "moduleYieldStrengthNewtons";              // :125
        private const string TemplateBreachAreaField = "breachAreaSquareMeters";                     // :128
        private const string TemplateDryMassField = "structuralDryMassKilograms";                    // :132
        private const string TemplateDisplacementField = "buoyancyDisplacementVolumeCubicMeters";    // :135
        private const string TemplateMaxUnmooredAccelField = "maximumUnmooredAccelerationMetersPerSecondSquared"; // :138
        private const string TemplateMaxComShiftField = "maximumCenterOfMassShiftMeters";            // :141
        private const string TemplateComShiftTauField = "centerOfMassShiftTauSeconds";               // :144

        // ══════════════════════════════════════════════════════════
        //  AUTHORED CONSTANTS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Integrity policy shared verbatim by all seven hand-authored
        /// StandardModuleTemplates assets (defaultIntegrityState 1,
        /// floodedBelowIntegrityState 0.45, oxygenOfflineBelowIntegrityState 0.35).
        /// A newly created template joins that family instead of inventing a
        /// third policy. Note ModuleArchitect1712 deliberately uses weathered
        /// values (0.86-0.96 / 0.42 / 0.32, ModuleArchitect1712.cs:377-379) because
        /// it bakes salvage-condition structures; a player-built utility is pristine.
        /// </summary>
        private const float PristineIntegrityState = 1f;
        private const float FloodedBelowIntegrityState = 0.45f;
        private const float OxygenOfflineBelowIntegrityState = 0.35f;

        /// <summary>Unmoored-physics policy, identical across all seven sibling templates.</summary>
        private const float MaxUnmooredAccelerationMetersPerSecondSquared = 24f;
        private const float MaxCenterOfMassShiftMeters = 0.85f;
        private const float CenterOfMassShiftTauSeconds = 1.2f;

        /// <summary>
        /// Projected drag cross-section as a fraction of the largest bounding face.
        /// Fitted to the seven authored siblings, whose ratio of
        /// projectedDragAreaSquareMeters to largest face area is:
        /// Foundation 12/64=0.19, Corridor 10/32=0.31, Airlock 9/30=0.30,
        /// JunctionT 14/32=0.44, JunctionX 16/32=0.50, MultiPurposeRoom 20/60=0.33,
        /// Moonpool 24/96=0.25. 0.30 is the family centre, not a guess.
        /// </summary>
        private const float DragAreaFractionOfLargestFace = 0.30f;

        /// <summary>
        /// Hull density for solid exterior machinery, taken from
        /// ModuleArchitect1712.GeneratedHullMassDensityKilogramsPerCubicMeter
        /// (ModuleArchitect1712.cs:72) so the project keeps one mass model.
        /// </summary>
        private const float HullMassDensityKilogramsPerCubicMeter = 86f;

        /// <summary>
        /// Mass floor for a small seabed device. ModuleArchitect1712 floors at 6000 kg
        /// (ModuleArchitect1712.cs:367-371) because it bakes room-scale volumes; a
        /// ~6 m³ service pump at that floor would be 1000 kg/m³ of solid steel, so the
        /// floor here is scaled to the device class instead of copied.
        /// </summary>
        private const float MinimumDeviceDryMassKilograms = 800f;

        /// <summary>Seawater density, matching ModuleArchitect1712.cs:71.</summary>
        private const float SeawaterDensityKilogramsPerCubicMeter = 1025f;

        /// <summary>
        /// Displacement-to-self-weight ratio. ModuleArchitect1712 uses lerp(1.04, 1.12)
        /// over its quality weight (ModuleArchitect1712.cs:372); an authored template has
        /// no quality axis, so the midpoint is used and stated.
        /// </summary>
        private const float DisplacementToSelfWeightRatio = 1.08f;

        /// <summary>
        /// Yield strength fit, newtons = YieldBaseNewtons + YieldNewtonsPerCubicMeter * envelope volume,
        /// clamped to the authored family band. Against the seven siblings
        /// (volume -> authored yield): Corridor 128 -> 180000, Airlock 180 -> 170000,
        /// JunctionT 256 -> 200000, JunctionX 256 -> 220000, MultiPurposeRoom 600 -> 260000,
        /// Moonpool 960 -> 280000. ModuleArchitect1712's mass*9.81*16..24 formula
        /// (ModuleArchitect1712.cs:373) returns ~2.3 MN for the corridor — an order of
        /// magnitude off this family — so it is deliberately not reused here.
        /// </summary>
        private const float YieldBaseNewtons = 150000f;
        private const float YieldNewtonsPerCubicMeter = 140f;
        private const float MinimumYieldNewtons = 180000f;
        private const float MaximumYieldNewtons = 400000f;

        /// <summary>
        /// Breach opening for a socketless exterior device. The authored family scales
        /// breachAreaSquareMeters with socket count (Foundation, 1 socket, 0.4 ->
        /// JunctionX, 4 sockets, 1.4). A zero-socket sealed device sits below the
        /// foundation, at the BaseModuleTemplate [Min(0.05f)] guard's practical floor.
        /// </summary>
        private const float SocketlessDeviceBreachAreaSquareMeters = 0.25f;

        /// <summary>
        /// Minimum per-axis proxy bounds this tool will accept from a measured prefab.
        /// ContentSanityValidator.ValidateBaseModuleTemplates rejects any axis
        /// &lt;= 0.01 as degenerate (ContentSanityValidator.cs:2853, :2876-2877) and
        /// BaseModuleTemplate.OnValidate silently overwrites a degenerate size with
        /// (4,4,4) (BaseModuleTemplate.cs:239-240). Declining is correct; writing a
        /// value that gets silently replaced by a 4 m cube is not.
        /// </summary>
        private const float MinimumAcceptedProxyAxisMeters = 0.05f;

        /// <summary>
        /// Ratio beyond which a recipe's template bounds and its bound prefab's real
        /// render bounds are reported as a preview lie. 1.35 tolerates the authored
        /// clearance margin the hand set uses (Corridor template 4x4x8 over a
        /// 2.2x2.2x6.2 body) without hiding a genuine mismatch.
        /// </summary>
        private const float ProxyBoundsMismatchRatio = 1.35f;

        // Editor-only scratch buffers. Reused so a 10-recipe sweep does not churn
        // one List per prefab measurement.
        private static readonly List<MeshFilter> s_MeshFilterScratch = new List<MeshFilter>(64);
        private static readonly List<SkinnedMeshRenderer> s_SkinnedScratch = new List<SkinnedMeshRenderer>(8);

        // ══════════════════════════════════════════════════════════
        //  RECIPE CONTRACT TABLE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// One row per production recipe. This is the authored intent map; it is not
        /// discovered, because "which template belongs to which recipe" and "which
        /// generated mesh is a three-way junction" are design facts, not derivable ones.
        /// </summary>
        private readonly struct RecipeSpec
        {
            public RecipeSpec(
                string recipeAssetName,
                string templateAssetName,
                string templateStableId,
                bool mayCreateTemplate,
                string generatedPrefabName,
                string intent)
            {
                RecipeAssetName = recipeAssetName;
                TemplateAssetName = templateAssetName;
                TemplateStableId = templateStableId;
                MayCreateTemplate = mayCreateTemplate;
                GeneratedPrefabName = generatedPrefabName;
                Intent = intent;
            }

            public string RecipeAssetName { get; }

            public string TemplateAssetName { get; }

            /// <summary>
            /// stableId written when this tool creates the template. For the seven
            /// pre-existing templates this is the value already on disk and is never
            /// rewritten — the template hash is a persisted identity
            /// (BaseModuleTemplate.cs:184-197) and must not move under existing saves.
            /// </summary>
            public string TemplateStableId { get; }

            /// <summary>
            /// True only where a correct template is fully derivable from the bound
            /// prefab: a socketless, free-placed exterior device. A habitat template
            /// needs authored socket topology and compatibility lanes that cannot be
            /// measured, so a missing one is reported, never invented.
            /// </summary>
            public bool MayCreateTemplate { get; }

            /// <summary>ModuleArchitect1712 output prefab name, or empty when this recipe already has geometry.</summary>
            public string GeneratedPrefabName { get; }

            public string Intent { get; }
        }

        /// <summary>
        /// Generated-prefab mapping is by socket topology, read off the ModuleSpec table
        /// at ModuleArchitect1712.cs:133-138:
        ///   H8_A1712_Junction_01     SocketMask.Cross          -> 4-way  -> Build_Junction_X
        ///   H8_A1712_ServiceCap_01   SocketMask.NorthEastWest  -> 3-way  -> Build_Junction_T
        ///   H8_A1712_Airlock_01      NorthSouth, isEmergencyAirlock -> Build_Airlock_Hatch
        ///   H8_A1712_ReactorRoom_01  Cross, largest envelope    -> Build_MultiPurpose_Room
        ///   H8_A1712_VerticalShaft_01 NorthSouth | Vertical     -> Build_Moonpool_Bay
        ///     (Moonpool is the only recipe whose template carries a Bottom-direction
        ///      Dock socket at y = -4, so the vertical-shaft topology is its match.)
        ///   H8_A1712_Corridor_01     NorthSouth -> unused here; Build_Corridor_Straight
        ///     already has a bound prefab and this tool never replaces a working binding.
        /// </summary>
        private static readonly RecipeSpec[] Recipes =
        {
            new RecipeSpec(
                "Build_Foundation_Platform",
                "BaseModuleTemplate_Foundation",
                "base.template.foundation.platform",
                false,
                string.Empty,
                "Structure anchor. Template and prefab already bound; verified only."),
            new RecipeSpec(
                "Build_Corridor_Straight",
                "BaseModuleTemplate_CorridorStraight",
                "base.template.corridor.straight",
                false,
                string.Empty,
                "Habitat connector. Template and prefab already bound; verified only."),
            new RecipeSpec(
                "Build_Current_Turbine",
                "BaseModuleTemplate_CurrentTurbine",
                "base.template.turbine.current",
                true,
                string.Empty,
                "Utility generator. Prefab bound, template missing -> template created from prefab bounds."),
            new RecipeSpec(
                "Build_Service_Pump",
                "BaseModuleTemplate_ServicePump",
                "base.template.pump.service",
                true,
                string.Empty,
                "Utility flood control. Prefab bound, template missing -> template created from prefab bounds."),
            new RecipeSpec(
                "Build_Utility_Pylon",
                "BaseModuleTemplate_UtilityPylon",
                "base.template.pylon.utility",
                true,
                string.Empty,
                "Utility routing node. Prefab bound, template missing -> template created from prefab bounds."),
            new RecipeSpec(
                "Build_Airlock_Hatch",
                "BaseModuleTemplate_Airlock",
                "base.template.airlock.hatch",
                false,
                "H8_A1712_Airlock_01",
                "Template bound, prefab missing -> bind Agent1712 airlock when fabricated."),
            new RecipeSpec(
                "Build_Junction_T",
                "BaseModuleTemplate_JunctionT",
                "base.template.junction.t",
                false,
                "H8_A1712_ServiceCap_01",
                "Template bound, prefab missing -> bind Agent1712 three-way cap when fabricated."),
            new RecipeSpec(
                "Build_Junction_X",
                "BaseModuleTemplate_JunctionX",
                "base.template.junction.x",
                false,
                "H8_A1712_Junction_01",
                "Template bound, prefab missing -> bind Agent1712 cross junction when fabricated."),
            new RecipeSpec(
                "Build_Moonpool_Bay",
                "BaseModuleTemplate_Moonpool",
                "base.template.moonpool.bay",
                false,
                "H8_A1712_VerticalShaft_01",
                "Template bound, prefab missing -> bind Agent1712 vertical shaft when fabricated."),
            new RecipeSpec(
                "Build_MultiPurpose_Room",
                "BaseModuleTemplate_MultiPurposeRoom",
                "base.template.room.multipurpose",
                false,
                "H8_A1712_ReactorRoom_01",
                "Template bound, prefab missing -> bind Agent1712 reactor room when fabricated."),
        };

        // ══════════════════════════════════════════════════════════
        //  ENTRY POINT: REPAIR
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Repairs template bindings, prefab bindings, and catalog membership for the
        /// ten starter construction recipes. Idempotent: a second run detects the
        /// authored state, writes nothing, and reports NO CHANGE per recipe.
        /// Batch usage:
        /// -executeMethod Hecton8.Editor.Authoring.ConstructionCatalogRepairAuthoring.RepairConstructionCatalog
        /// </summary>
        [MenuItem("Hecton8/Authoring/Repair Construction Catalog Bindings", priority = 220)]
        public static void RepairConstructionCatalog()
        {
            ModuleCatalog catalog = AssetDatabase.LoadAssetAtPath<ModuleCatalog>(ModuleCatalogPath);
            if (catalog == null)
            {
                Debug.LogError(
                    $"{LogPrefix} DECLINED: module catalog not found at '{ModuleCatalogPath}'. " +
                    "Run Hecton8/Authoring/Rebuild Starter Construction Kit first, then re-run this repair. " +
                    "Nothing written.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(StandardTemplateFolder))
            {
                Debug.LogError(
                    $"{LogPrefix} DECLINED: template folder '{StandardTemplateFolder}' does not exist. " +
                    "It holds the seven authored production templates; its absence means the construction " +
                    "data set is not the one this repair was written against. Nothing written.");
                return;
            }

            StringBuilder report = new StringBuilder(4096);
            report.AppendLine($"{LogPrefix} REPAIR REPORT ({Recipes.Length} recipes)");

            // Phase A: resolve every recipe and create the templates that are missing
            // AND fully derivable. Deliberately outside StartAssetEditing: creating an
            // asset while asset editing is paused defers its .meta/GUID, and every
            // later step here needs a resolvable asset reference.
            BuildableData[] recipeAssets = new BuildableData[Recipes.Length];
            BaseModuleTemplate[] templateAssets = new BaseModuleTemplate[Recipes.Length];
            bool[] templateCreated = new bool[Recipes.Length];
            int declineCount = 0;

            for (int i = 0; i < Recipes.Length; i++)
            {
                RecipeSpec spec = Recipes[i];
                string recipePath = $"{ConstructionDataFolder}/{spec.RecipeAssetName}.asset";
                BuildableData recipe = AssetDatabase.LoadAssetAtPath<BuildableData>(recipePath);
                if (recipe == null)
                {
                    declineCount++;
                    report.AppendLine($"  {spec.RecipeAssetName}: DECLINED — recipe asset missing at '{recipePath}'.");
                    continue;
                }

                recipeAssets[i] = recipe;

                string templatePath = $"{StandardTemplateFolder}/{spec.TemplateAssetName}.asset";
                BaseModuleTemplate template = AssetDatabase.LoadAssetAtPath<BaseModuleTemplate>(templatePath);
                if (template != null)
                {
                    templateAssets[i] = template;
                    continue;
                }

                if (!spec.MayCreateTemplate)
                {
                    declineCount++;
                    report.AppendLine(
                        $"  {spec.RecipeAssetName}: DECLINED — authored template '{templatePath}' is missing and " +
                        "this recipe needs authored socket topology and compatibility lanes that cannot be measured " +
                        "from geometry. Restore the template asset; this tool will not invent habitat sockets.");
                    continue;
                }

                if (!TryCreateSocketlessDeviceTemplate(spec, recipe, templatePath, out template, out string createFailure))
                {
                    declineCount++;
                    report.AppendLine($"  {spec.RecipeAssetName}: DECLINED — {createFailure}");
                    continue;
                }

                templateAssets[i] = template;
                templateCreated[i] = true;
            }

            // Phase B: batched mutation of the ten recipes plus the catalog. Every write
            // is a SerializedObject property write on an already-imported asset, which is
            // exactly the case StartAssetEditing exists for — it collapses eleven
            // reimports into one. try/finally is mandatory: an escaping exception with
            // asset editing still paused leaves the AssetDatabase locked for the session.
            int changedRecipeCount = 0;
            int catalogAdditionCount = 0;
            bool catalogChanged = false;

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < Recipes.Length; i++)
                {
                    BuildableData recipe = recipeAssets[i];
                    if (recipe == null)
                        continue;

                    if (ApplyRecipeBindings(Recipes[i], recipe, templateAssets[i], templateCreated[i], report))
                        changedRecipeCount++;
                }

                catalogChanged = ApplyCatalogMembership(catalog, recipeAssets, report, out catalogAdditionCount);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            int createdTemplateCount = 0;
            for (int i = 0; i < templateCreated.Length; i++)
            {
                if (templateCreated[i])
                    createdTemplateCount++;
            }

            bool wroteAnything = changedRecipeCount > 0 || catalogChanged || createdTemplateCount > 0;
            if (wroteAnything)
                AssetDatabase.SaveAssets();

            report.AppendLine(
                $"  SUMMARY: templatesCreated={createdTemplateCount}, recipesChanged={changedRecipeCount}, " +
                $"catalogRowsAdded={catalogAdditionCount}, declined={declineCount}, " +
                $"assetsSaved={(wroteAnything ? "yes" : "no — already correct")}.");
            report.Append(
                "  ghostPrefab left null on every recipe by design: BuilderHolographyStaticAudit" +
                ".NoNonZeroGhostPrefabRefs (BuilderHolographyTools.cs:612-644) fails on any non-zero value.");

            if (declineCount > 0)
                Debug.LogWarning(report.ToString());
            else
                Debug.Log(report.ToString());

            if (createdTemplateCount > 0)
            {
                Debug.Log(
                    $"{LogPrefix} {createdTemplateCount} new BaseModuleTemplate asset(s) created. " +
                    "BaseModuleCatalogEditorWindow.BakeCatalogBinary scans every t:BaseModuleTemplate in the " +
                    "project (BaseModuleCatalogEditorTools.cs:130-137), so re-bake " +
                    "Assets/_Project/Data/Construction/BaseModuleCatalog.h8bin through " +
                    "Hecton8/Construction/Base Module Catalog before trusting runtime socket DTOs.");
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ENTRY POINT: VERIFY
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Read-only binding proof for the same ten recipes. Writes nothing. In batch
        /// mode it terminates the editor with exit code 1 when any binding is still
        /// missing, so the operator gets a machine-checkable result instead of a claim.
        /// Scope is deliberately narrow — moduleTemplate, finalPrefab, catalog row,
        /// ghostPrefab gate, hash uniqueness. The broad content audit stays with
        /// ConstructionCatalogValidator.ValidateConstructionCatalog
        /// (ConstructionCatalogValidator.cs:13-57).
        /// Batch usage:
        /// -executeMethod Hecton8.Editor.Authoring.ConstructionCatalogRepairAuthoring.VerifyConstructionCatalogBindings
        /// </summary>
        [MenuItem("Hecton8/Validation/Verify Construction Catalog Bindings", priority = 243)]
        public static void VerifyConstructionCatalogBindings()
        {
            StringBuilder report = new StringBuilder(4096);
            report.AppendLine($"{LogPrefix} VERIFY REPORT ({Recipes.Length} recipes)");

            int failureCount = 0;
            int warningCount = 0;

            ModuleCatalog catalog = AssetDatabase.LoadAssetAtPath<ModuleCatalog>(ModuleCatalogPath);
            if (catalog == null)
            {
                failureCount++;
                report.AppendLine($"  FAIL: module catalog not found at '{ModuleCatalogPath}'.");
            }

            // Template-hash uniqueness across the ten recipes. Binding a template moves a
            // recipe's ModuleHashId from the recipe-name hash to the template hash
            // (BuildableData.cs:213-225), and ModuleCatalog.AddHashAlias treats a collision
            // as lookup ambiguity that disables runtime mod registration entirely
            // (ModuleCatalog.cs:580-598). Two recipes sharing a hash is a hard failure.
            Dictionary<int, string> hashOwners = new Dictionary<int, string>(Recipes.Length);

            for (int i = 0; i < Recipes.Length; i++)
            {
                RecipeSpec spec = Recipes[i];
                string recipePath = $"{ConstructionDataFolder}/{spec.RecipeAssetName}.asset";
                BuildableData recipe = AssetDatabase.LoadAssetAtPath<BuildableData>(recipePath);
                if (recipe == null)
                {
                    failureCount++;
                    report.AppendLine($"  FAIL {spec.RecipeAssetName}: recipe asset missing at '{recipePath}'.");
                    continue;
                }

                BaseModuleTemplate template = recipe.ModuleTemplate;
                if (template == null)
                {
                    failureCount++;
                    report.AppendLine(
                        $"  FAIL {spec.RecipeAssetName}: moduleTemplate is null. The placement hologram falls back " +
                        "to a 1 m cube (PlayerBuilder.cs:1120-1130, :2592-2604) and structural/terrain validation " +
                        "is skipped (PlayerBuilder.cs:1948-1953, :3132).");
                }
                else
                {
                    Vector3 proxySize = template.ProxyBoundsSize;
                    if (proxySize.x <= MinimumAcceptedProxyAxisMeters ||
                        proxySize.y <= MinimumAcceptedProxyAxisMeters ||
                        proxySize.z <= MinimumAcceptedProxyAxisMeters)
                    {
                        failureCount++;
                        report.AppendLine(
                            $"  FAIL {spec.RecipeAssetName}: template '{template.name}' ProxyBoundsSize " +
                            $"{FormatVector(proxySize)} is degenerate; the hologram would have no readable footprint.");
                    }

                    int hashId = template.ResolvePersistentHashId();
                    if (hashId == 0)
                    {
                        failureCount++;
                        report.AppendLine(
                            $"  FAIL {spec.RecipeAssetName}: template '{template.name}' resolves to hash 0, so " +
                            "ModuleCatalog.FindDataByHashId can never return it (ModuleCatalog.cs:134-144).");
                    }
                    else if (hashOwners.TryGetValue(hashId, out string owner))
                    {
                        failureCount++;
                        report.AppendLine(
                            $"  FAIL {spec.RecipeAssetName}: template hash {hashId} collides with '{owner}'. " +
                            "ModuleCatalog.AddHashAlias raises lookup ambiguity on this (ModuleCatalog.cs:580-598).");
                    }
                    else
                    {
                        hashOwners.Add(hashId, spec.RecipeAssetName);
                    }
                }

                if (recipe.finalPrefab == null)
                {
                    failureCount++;
                    report.AppendLine(
                        $"  FAIL {spec.RecipeAssetName}: finalPrefab is null. ModuleCatalog.FindPrefabById returns " +
                        "null (ModuleCatalog.cs:128-132), so the recipe can be neither placed nor restored. " +
                        (string.IsNullOrEmpty(spec.GeneratedPrefabName)
                            ? "No generated source is mapped for this recipe."
                            : $"Fabricate '{spec.GeneratedPrefabName}' via Hecton8/Structures/Agent 1712, then re-run the repair."));
                }
                else if (recipe.ModuleTemplate != null)
                {
                    warningCount += ReportProxyBoundsAgreement(spec, recipe, recipe.ModuleTemplate, report);
                }

                if (recipe.ghostPrefab != null)
                {
                    failureCount++;
                    report.AppendLine(
                        $"  FAIL {spec.RecipeAssetName}: ghostPrefab is non-null ('{recipe.ghostPrefab.name}'). " +
                        "BuilderHolographyStaticAudit.NoNonZeroGhostPrefabRefs (BuilderHolographyTools.cs:612-644) " +
                        "fails the noNonZeroBuildableGhostPrefabReferences flag on any non-zero value.");
                }

                if (catalog != null && !CatalogContains(catalog, recipe))
                {
                    failureCount++;
                    report.AppendLine(
                        $"  FAIL {spec.RecipeAssetName}: absent from '{ModuleCatalogPath}'. " +
                        "ConstructionManager module restore resolves through the catalog only " +
                        "(ConstructionManager.cs:2762-2777).");
                }
            }

            AppendRuntimeReachabilityAdvisory(report);

            report.AppendLine(
                $"  SUMMARY: failures={failureCount}, warnings={warningCount}, recipes={Recipes.Length}. " +
                "Static asset-graph proof only — this is not Unity Play Mode, profiler, or placement proof.");

            if (failureCount > 0)
            {
                report.Append("  RESULT: FAIL");
                Debug.LogError(report.ToString());
            }
            else if (warningCount > 0)
            {
                report.Append("  RESULT: PASS WITH WARNINGS");
                Debug.LogWarning(report.ToString());
            }
            else
            {
                report.Append("  RESULT: PASS");
                Debug.Log(report.ToString());
            }

            if (Application.isBatchMode)
                EditorApplication.Exit(failureCount > 0 ? 1 : 0);
        }

        /// <summary>
        /// Names the upstream blocker every time the gate runs, so a PASS can never be
        /// mistaken for "the player can now build these". Deliberately does NOT count
        /// toward the exit code: assigning ConstructionManager.catalog is core territory,
        /// not a data binding this tool owns, and an un-passable gate is a useless gate.
        /// Reachability is probed by script GUID because scenes and prefabs bind by GUID,
        /// not by class name.
        /// </summary>
        private static void AppendRuntimeReachabilityAdvisory(StringBuilder report)
        {
            ModuleCatalog catalog = AssetDatabase.LoadAssetAtPath<ModuleCatalog>(ModuleCatalogPath);
            if (catalog == null)
                return;

            string catalogGuid = AssetDatabase.AssetPathToGUID(ModuleCatalogPath);
            report.AppendLine(
                $"  ADVISORY (not counted in failures): catalog asset guid '{catalogGuid}' is authored data only. " +
                "ConstructionManager.catalog is [SerializeField] private with no setter and no runtime resolver " +
                "(ConstructionManager.cs:223, :432), and GameBootstrapper creates the component bare via " +
                "AddComponent (GameBootstrapper.cs:6376-6378), so the field stays null at runtime. " +
                "EnvironmentRuntimeContextService reads it from _logisticsService.Catalog " +
                "(EnvironmentRuntimeContextService.cs:200, :349) and PlayerBuilder reads " +
                "that context (PlayerBuilder.cs:4391-4394, :1505), so catalog cycling has nothing to cycle and " +
                "LoadFromSaveData hard-aborts (ConstructionManager.cs:2688-2695). A PASS here proves the asset " +
                "graph is correct, NOT that any of it is reachable in Play Mode. Owner fix belongs in " +
                "GameBootstrapper.EnsureConstructionServiceRegistered (:6366-6385) or by authoring the component " +
                "into 00_BOOTSTRAP with catalog assigned.");
        }

        // ══════════════════════════════════════════════════════════
        //  RECIPE MUTATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Writes the three owned bindings on one recipe. Never overwrites a non-null
        /// binding that disagrees with the spec — a designer's deliberate choice is
        /// reported, not eaten. Returns true when the asset was actually modified.
        /// </summary>
        private static bool ApplyRecipeBindings(
            RecipeSpec spec,
            BuildableData recipe,
            BaseModuleTemplate template,
            bool templateWasCreated,
            StringBuilder report)
        {
            SerializedObject serializedRecipe = new SerializedObject(recipe);
            SerializedProperty templateProperty = serializedRecipe.FindProperty(ModuleTemplateField);
            SerializedProperty finalPrefabProperty = serializedRecipe.FindProperty(FinalPrefabField);
            SerializedProperty ghostPrefabProperty = serializedRecipe.FindProperty(GhostPrefabField);

            if (templateProperty == null || finalPrefabProperty == null || ghostPrefabProperty == null)
            {
                report.AppendLine(
                    $"  {spec.RecipeAssetName}: DECLINED — BuildableData serialized contract changed; " +
                    $"expected '{ModuleTemplateField}', '{FinalPrefabField}', '{GhostPrefabField}' " +
                    "(BuildableData.cs:90-96). Nothing written to this recipe.");
                return false;
            }

            bool changed = false;
            StringBuilder line = new StringBuilder(256);
            line.Append("  ").Append(spec.RecipeAssetName).Append(": ");

            // --- moduleTemplate ---
            // UnityEngine.Object spelled out: this file lives inside the Hecton8.*
            // namespace tree, where a bare framework type name can bind to a project
            // type instead (CONTRIBUTING.md records Hecton8.Environment shadowing
            // System.Environment as a live CS0234 trap here).
            UnityEngine.Object existingTemplate = templateProperty.objectReferenceValue;
            if (existingTemplate == null)
            {
                if (template != null)
                {
                    templateProperty.objectReferenceValue = template;
                    changed = true;
                    line.Append("moduleTemplate BOUND -> '").Append(template.name).Append('\'')
                        .Append(templateWasCreated ? " (template created by this run)" : " (existing authored template)")
                        .Append("; ");
                }
                else
                {
                    line.Append("moduleTemplate STILL NULL (no template available); ");
                }
            }
            else if (template != null && !ReferenceEquals(existingTemplate, template))
            {
                line.Append("moduleTemplate KEPT as '").Append(existingTemplate.name)
                    .Append("' — differs from expected '").Append(template.name)
                    .Append("', left for a human decision; ");
            }
            else
            {
                line.Append("moduleTemplate already '").Append(existingTemplate.name).Append("'; ");
            }

            // --- finalPrefab ---
            UnityEngine.Object existingPrefab = finalPrefabProperty.objectReferenceValue;
            if (existingPrefab != null)
            {
                line.Append("finalPrefab already '").Append(existingPrefab.name).Append("'; ");
            }
            else if (string.IsNullOrEmpty(spec.GeneratedPrefabName))
            {
                line.Append("finalPrefab STILL NULL and no generated source is mapped; ");
            }
            else
            {
                string generatedPath = $"{Agent1712OutputFolder}/{spec.GeneratedPrefabName}.prefab";
                GameObject generatedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(generatedPath);
                if (generatedPrefab == null)
                {
                    line.Append("finalPrefab SKIPPED — '").Append(generatedPath)
                        .Append("' not fabricated yet; run Hecton8/Structures/Agent 1712/Fabricate Default Module Set Now, then re-run; ");
                }
                else
                {
                    finalPrefabProperty.objectReferenceValue = generatedPrefab;
                    changed = true;
                    line.Append("finalPrefab BOUND -> '").Append(generatedPrefab.name).Append("'; ");
                }
            }

            // --- ghostPrefab: enforce the holography gate ---
            if (ghostPrefabProperty.objectReferenceValue != null)
            {
                string clearedName = ghostPrefabProperty.objectReferenceValue.name;
                ghostPrefabProperty.objectReferenceValue = null;
                changed = true;
                line.Append("ghostPrefab CLEARED (was '").Append(clearedName)
                    .Append("'); the runtime preview instantiates nothing and the static audit forbids a non-zero value.");
            }
            else
            {
                line.Append("ghostPrefab null as required.");
            }

            if (changed)
            {
                serializedRecipe.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(recipe);
            }
            else
            {
                line.Append(" NO CHANGE.");
            }

            report.AppendLine(line.ToString());
            return changed;
        }

        // ══════════════════════════════════════════════════════════
        //  CATALOG MEMBERSHIP
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Appends missing recipes to <c>ModuleCatalog.allModules</c>. Strictly additive:
        /// existing rows keep their index and order, because that order is what
        /// ModuleCatalog.GetAt / GetViewableAt hand to the build browser
        /// (ModuleCatalog.cs:350-397) and it is hand-tuned.
        /// </summary>
        private static bool ApplyCatalogMembership(
            ModuleCatalog catalog,
            BuildableData[] recipeAssets,
            StringBuilder report,
            out int additionCount)
        {
            additionCount = 0;

            SerializedObject serializedCatalog = new SerializedObject(catalog);
            SerializedProperty modules = serializedCatalog.FindProperty(AllModulesField);
            if (modules == null || !modules.isArray)
            {
                report.AppendLine(
                    $"  CATALOG: DECLINED — '{AllModulesField}' is not a serialized array on ModuleCatalog " +
                    "(ModuleCatalog.cs:32). Nothing written to the catalog.");
                return false;
            }

            int sizeBefore = modules.arraySize;

            for (int i = 0; i < recipeAssets.Length; i++)
            {
                BuildableData recipe = recipeAssets[i];
                if (recipe == null)
                    continue;

                if (TryFindCatalogIndex(modules, recipe, out int existingIndex))
                {
                    // Repair a row that resolves to the same PersistentId through a stale
                    // or null object reference rather than appending a second row for it.
                    SerializedProperty existingElement = modules.GetArrayElementAtIndex(existingIndex);
                    if (!ReferenceEquals(existingElement.objectReferenceValue, recipe))
                    {
                        existingElement.objectReferenceValue = recipe;
                        additionCount++;
                        report.AppendLine(
                            $"  CATALOG: row {existingIndex} REPOINTED to '{recipe.name}' " +
                            "(matched by PersistentId, object reference was stale).");
                    }

                    continue;
                }

                int appendIndex = modules.arraySize;
                modules.InsertArrayElementAtIndex(appendIndex);
                modules.GetArrayElementAtIndex(appendIndex).objectReferenceValue = recipe;
                additionCount++;
                report.AppendLine($"  CATALOG: APPENDED '{recipe.name}' at row {appendIndex}.");
            }

            if (additionCount <= 0)
            {
                report.AppendLine($"  CATALOG: NO CHANGE — all {sizeBefore} rows already cover every recipe.");
                return false;
            }

            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            report.AppendLine($"  CATALOG: rows {sizeBefore} -> {modules.arraySize}, existing order preserved.");
            return true;
        }

        /// <summary>
        /// Locates a recipe in the catalog array by object reference first and
        /// PersistentId second. The PersistentId probe mirrors
        /// ModuleArchitect1712.TryFindBuildableIndexByPersistentId
        /// (ModuleArchitect1712.cs:468-495) so the two catalog writers agree on what
        /// "already present" means and neither can duplicate the other's row.
        /// </summary>
        private static bool TryFindCatalogIndex(SerializedProperty modules, BuildableData recipe, out int index)
        {
            index = -1;
            string persistentId = recipe.PersistentId;

            for (int i = 0; i < modules.arraySize; i++)
            {
                BuildableData existing = modules.GetArrayElementAtIndex(i).objectReferenceValue as BuildableData;
                if (existing == null)
                    continue;

                if (ReferenceEquals(existing, recipe) ||
                    (!string.IsNullOrWhiteSpace(persistentId) && existing.MatchesPersistentId(persistentId)))
                {
                    index = i;
                    return true;
                }
            }

            return false;
        }

        private static bool CatalogContains(ModuleCatalog catalog, BuildableData recipe)
        {
            // IndexOf walks the authored list plus the runtime overlay and compares by
            // reference (ModuleCatalog.cs:402-424) — the exact membership question here.
            return catalog.IndexOf(recipe) >= 0;
        }

        // ══════════════════════════════════════════════════════════
        //  TEMPLATE CREATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Creates the BaseModuleTemplate for a socketless, free-placed exterior device
        /// and derives its proxy bounds from the recipe's already-bound finalPrefab,
        /// because ProxyBoundsSize is what the hologram is drawn at
        /// (PlayerBuilder.cs:2592-2604) and what the terrain-clearance probe measures
        /// (PlayerBuilder.cs:3326-3332).
        /// </summary>
        private static bool TryCreateSocketlessDeviceTemplate(
            RecipeSpec spec,
            BuildableData recipe,
            string templatePath,
            out BaseModuleTemplate template,
            out string failure)
        {
            template = null;

            GameObject sourcePrefab = recipe.finalPrefab;
            if (sourcePrefab == null)
            {
                failure =
                    $"cannot derive template '{spec.TemplateAssetName}': the recipe has no finalPrefab to measure. " +
                    "Bind geometry first, then re-run.";
                return false;
            }

            if (!TryMeasurePlacedRenderBounds(sourcePrefab, out Bounds measured))
            {
                failure =
                    $"cannot derive template '{spec.TemplateAssetName}': prefab '{sourcePrefab.name}' exposes no " +
                    "MeshFilter+MeshRenderer or SkinnedMeshRenderer geometry to measure.";
                return false;
            }

            Vector3 proxySize = measured.size;
            if (proxySize.x <= MinimumAcceptedProxyAxisMeters ||
                proxySize.y <= MinimumAcceptedProxyAxisMeters ||
                proxySize.z <= MinimumAcceptedProxyAxisMeters)
            {
                failure =
                    $"cannot derive template '{spec.TemplateAssetName}': measured bounds {FormatVector(proxySize)} " +
                    "are degenerate. BaseModuleTemplate.OnValidate would silently replace them with a 4 m cube " +
                    "(BaseModuleTemplate.cs:239-240), so nothing is written.";
                return false;
            }

            BaseModuleTemplate created = ScriptableObject.CreateInstance<BaseModuleTemplate>();
            AssetDatabase.CreateAsset(created, templatePath);

            SerializedObject serializedTemplate = new SerializedObject(created);
            if (!TryWriteSocketlessDeviceFields(serializedTemplate, spec, recipe, measured, out failure))
            {
                // The asset exists but its contract could not be satisfied. Remove it
                // rather than leave a half-authored template that ContentSanityValidator
                // will flag forever.
                AssetDatabase.DeleteAsset(templatePath);
                return false;
            }

            serializedTemplate.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(created);

            template = created;
            failure = string.Empty;
            return true;
        }

        /// <summary>
        /// Writes every serialized field of a socketless device template. Bails out with
        /// a named field on any contract mismatch instead of writing a partial template.
        /// </summary>
        private static bool TryWriteSocketlessDeviceFields(
            SerializedObject serializedTemplate,
            RecipeSpec spec,
            BuildableData recipe,
            Bounds measured,
            out string failure)
        {
            Vector3 size = measured.size;
            float envelopeVolume = Mathf.Max(1f, size.x * size.y * size.z);
            float largestFaceArea = Mathf.Max(size.x * size.y, Mathf.Max(size.z * size.y, size.x * size.z));
            float dryMassKilograms = Mathf.Max(
                MinimumDeviceDryMassKilograms,
                envelopeVolume * HullMassDensityKilogramsPerCubicMeter);
            float displacementCubicMeters = Mathf.Max(
                0.1f,
                dryMassKilograms / SeawaterDensityKilogramsPerCubicMeter * DisplacementToSelfWeightRatio);
            float yieldNewtons = Mathf.Clamp(
                YieldBaseNewtons + YieldNewtonsPerCubicMeter * envelopeVolume,
                MinimumYieldNewtons,
                MaximumYieldNewtons);
            float smallestHalfExtent = Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.5f;

            // powerDrawKW mirrors the recipe's own consumption magnitude, which is the
            // convention every authored sibling follows exactly: Airlock powerRating -6
            // and powerDrawKW 6, Corridor -6/6, JunctionT -8/8, JunctionX -10/10,
            // MultiPurposeRoom -12/12, Moonpool -18/18, Foundation 0/0. A generator
            // (turbine, powerRating +18) draws nothing, and the field is [Min(0f)]
            // (BaseModuleTemplate.cs:98) so generation cannot be expressed here anyway —
            // BuildableData.powerRating stays the single source of generation truth.
            float powerDrawKW = Mathf.Max(0f, -recipe.powerRating);

            return
                TryWriteString(serializedTemplate, TemplateStableIdField, spec.TemplateStableId, out failure) &&
                TryWriteInt(serializedTemplate, TemplateHashIdField, LocHash.Compute(spec.TemplateStableId), out failure) &&
                TryWriteVector3(serializedTemplate, TemplateProxyCenterField, measured.center, out failure) &&
                TryWriteVector3(serializedTemplate, TemplateProxySizeField, size, out failure) &&
                TryWriteFloat(serializedTemplate, TemplatePowerDrawField, powerDrawKW, out failure) &&
                // Exterior machinery contributes no breathable volume. Foundation Platform,
                // the other non-pressurized module in the authored set, uses airVolumeM3 0.
                TryWriteFloat(serializedTemplate, TemplateAirVolumeField, 0f, out failure) &&
                // Only Foundation Platform is a seafloor anchor in this kit. A pylon is a
                // power/data routing node ("for later power/data chains" per its recipe
                // description), not a habitat reachability anchor.
                TryWriteBool(serializedTemplate, TemplateStructuralAnchorField, false, out failure) &&
                TryWriteBool(serializedTemplate, TemplateEmergencyAirlockField, false, out failure) &&
                TryWriteFloat(serializedTemplate, TemplateDefaultIntegrityField, PristineIntegrityState, out failure) &&
                TryWriteFloat(serializedTemplate, TemplateFloodedBelowField, FloodedBelowIntegrityState, out failure) &&
                TryWriteFloat(serializedTemplate, TemplateOxygenOfflineField, OxygenOfflineBelowIntegrityState, out failure) &&
                TryWriteFloat(
                    serializedTemplate,
                    TemplateDragAreaField,
                    Mathf.Max(0.1f, largestFaceArea * DragAreaFractionOfLargestFace),
                    out failure) &&
                TryWriteFloat(serializedTemplate, TemplateYieldStrengthField, yieldNewtons, out failure) &&
                TryWriteFloat(
                    serializedTemplate,
                    TemplateBreachAreaField,
                    SocketlessDeviceBreachAreaSquareMeters,
                    out failure) &&
                TryWriteFloat(serializedTemplate, TemplateDryMassField, dryMassKilograms, out failure) &&
                TryWriteFloat(serializedTemplate, TemplateDisplacementField, displacementCubicMeters, out failure) &&
                TryWriteFloat(
                    serializedTemplate,
                    TemplateMaxUnmooredAccelField,
                    MaxUnmooredAccelerationMetersPerSecondSquared,
                    out failure) &&
                TryWriteFloat(
                    serializedTemplate,
                    TemplateMaxComShiftField,
                    Mathf.Clamp(smallestHalfExtent * 0.38f, 0.12f, MaxCenterOfMassShiftMeters),
                    out failure) &&
                TryWriteFloat(serializedTemplate, TemplateComShiftTauField, CenterOfMassShiftTauSeconds, out failure);
        }

        // ══════════════════════════════════════════════════════════
        //  BOUNDS MEASUREMENT
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Measures a prefab's visible geometry in the frame it will occupy once placed.
        /// Only the root's translation and rotation are removed; the root's own localScale
        /// is deliberately RETAINED, because Object.Instantiate overrides position and
        /// rotation but keeps localScale, and PlayerBuilder consumes ProxyBoundsSize as a
        /// world-space extent (PlayerBuilder.cs:3494, :3515).
        /// Collision proxies, decal projectors, and the leak ParticleSystem are excluded
        /// by construction: only MeshFilter+MeshRenderer pairs and SkinnedMeshRenderers
        /// contribute, so a trigger volume cannot inflate the hologram.
        /// </summary>
        private static bool TryMeasurePlacedRenderBounds(GameObject prefabRoot, out Bounds bounds)
        {
            bounds = default;
            if (prefabRoot == null)
                return false;

            Transform rootTransform = prefabRoot.transform;
            Matrix4x4 rootPoseInverse = Matrix4x4.TRS(
                rootTransform.localPosition,
                rootTransform.localRotation,
                Vector3.one).inverse;

            bool initialized = false;

            // Called on the Transform, not the GameObject: Component.GetComponentsInChildren<T>(bool, List<T>)
            // is the overload this project already uses for renderer sweeps
            // (HarvestableOutcrop.cs, WorldFidelityRoot.cs), so no overload guessing.
            s_MeshFilterScratch.Clear();
            rootTransform.GetComponentsInChildren(true, s_MeshFilterScratch);
            for (int i = 0; i < s_MeshFilterScratch.Count; i++)
            {
                MeshFilter filter = s_MeshFilterScratch[i];
                if (filter == null || filter.sharedMesh == null)
                    continue;

                if (!filter.TryGetComponent(out MeshRenderer _))
                    continue;

                EncapsulateTransformedBounds(
                    filter.sharedMesh.bounds,
                    rootPoseInverse * filter.transform.localToWorldMatrix,
                    ref initialized,
                    ref bounds);
            }

            s_SkinnedScratch.Clear();
            rootTransform.GetComponentsInChildren(true, s_SkinnedScratch);
            for (int i = 0; i < s_SkinnedScratch.Count; i++)
            {
                SkinnedMeshRenderer skinned = s_SkinnedScratch[i];
                if (skinned == null || skinned.sharedMesh == null)
                    continue;

                EncapsulateTransformedBounds(
                    skinned.sharedMesh.bounds,
                    rootPoseInverse * skinned.transform.localToWorldMatrix,
                    ref initialized,
                    ref bounds);
            }

            s_MeshFilterScratch.Clear();
            s_SkinnedScratch.Clear();

            return initialized && IsFinite(bounds.center) && IsFinite(bounds.size);
        }

        /// <summary>
        /// Encapsulates all eight transformed corners rather than the transformed centre
        /// plus extents, so a rotated child cannot under-report the envelope.
        /// </summary>
        private static void EncapsulateTransformedBounds(
            Bounds localBounds,
            Matrix4x4 localToTarget,
            ref bool initialized,
            ref Bounds target)
        {
            Vector3 min = localBounds.min;
            Vector3 max = localBounds.max;

            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 localCorner = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);
                Vector3 point = localToTarget.MultiplyPoint3x4(localCorner);
                if (!IsFinite(point))
                    continue;

                if (!initialized)
                {
                    initialized = true;
                    target = new Bounds(point, Vector3.zero);
                    continue;
                }

                target.Encapsulate(point);
            }
        }

        /// <summary>
        /// Warns when the hologram footprint and the geometry that will actually appear
        /// disagree beyond <see cref="ProxyBoundsMismatchRatio"/>. This is a warning, not
        /// a failure: a bound-but-mismatched prefab is still a placeable module, whereas a
        /// null prefab is not. It matters because BaseModule.ApplyBuildableTemplate
        /// (BaseModule.cs:4802-4816) overwrites the placed module's template with the
        /// RECIPE's template, so the recipe's socket and bounds math is what the placed
        /// geometry has to match — the prefab's own authored template is discarded.
        /// </summary>
        private static int ReportProxyBoundsAgreement(
            RecipeSpec spec,
            BuildableData recipe,
            BaseModuleTemplate template,
            StringBuilder report)
        {
            if (!TryMeasurePlacedRenderBounds(recipe.finalPrefab, out Bounds measured))
                return 0;

            Vector3 authored = template.ProxyBoundsSize;
            Vector3 actual = measured.size;
            float worstRatio = Mathf.Max(
                AxisRatio(authored.x, actual.x),
                Mathf.Max(AxisRatio(authored.y, actual.y), AxisRatio(authored.z, actual.z)));

            if (worstRatio <= ProxyBoundsMismatchRatio)
                return 0;

            // Capped for readability: AxisRatio returns float.MaxValue for a degenerate
            // axis, which is already reported as a hard failure above.
            float reportedRatio = Mathf.Min(worstRatio, 9999f);
            report.AppendLine(
                $"  WARN {spec.RecipeAssetName}: template '{template.name}' ProxyBoundsSize " +
                $"{FormatVector(authored)} vs prefab '{recipe.finalPrefab.name}' render bounds " +
                $"{FormatVector(actual)} — worst axis ratio {reportedRatio:0.00}. The hologram and the placed mesh " +
                "will not agree on footprint, and socket positions come from the template " +
                "(BaseModule.cs:4802-4816), so snapped connections may land off the geometry.");
            return 1;
        }

        private static float AxisRatio(float authored, float actual)
        {
            float a = Mathf.Abs(authored);
            float b = Mathf.Abs(actual);
            if (a <= MinimumAcceptedProxyAxisMeters || b <= MinimumAcceptedProxyAxisMeters)
                return float.MaxValue;

            return Mathf.Max(a / b, b / a);
        }

        // ══════════════════════════════════════════════════════════
        //  SERIALIZED WRITE HELPERS
        // ══════════════════════════════════════════════════════════

        private static bool TryWriteString(SerializedObject target, string field, string value, out string failure)
        {
            SerializedProperty property = target.FindProperty(field);
            if (property == null)
            {
                failure = DescribeMissingField(target, field);
                return false;
            }

            property.stringValue = value;
            failure = string.Empty;
            return true;
        }

        private static bool TryWriteInt(SerializedObject target, string field, int value, out string failure)
        {
            SerializedProperty property = target.FindProperty(field);
            if (property == null)
            {
                failure = DescribeMissingField(target, field);
                return false;
            }

            property.intValue = value;
            failure = string.Empty;
            return true;
        }

        private static bool TryWriteFloat(SerializedObject target, string field, float value, out string failure)
        {
            SerializedProperty property = target.FindProperty(field);
            if (property == null)
            {
                failure = DescribeMissingField(target, field);
                return false;
            }

            property.floatValue = value;
            failure = string.Empty;
            return true;
        }

        private static bool TryWriteBool(SerializedObject target, string field, bool value, out string failure)
        {
            SerializedProperty property = target.FindProperty(field);
            if (property == null)
            {
                failure = DescribeMissingField(target, field);
                return false;
            }

            property.boolValue = value;
            failure = string.Empty;
            return true;
        }

        private static bool TryWriteVector3(SerializedObject target, string field, Vector3 value, out string failure)
        {
            SerializedProperty property = target.FindProperty(field);
            if (property == null)
            {
                failure = DescribeMissingField(target, field);
                return false;
            }

            property.vector3Value = value;
            failure = string.Empty;
            return true;
        }

        private static string DescribeMissingField(SerializedObject target, string field)
        {
            string typeName = target != null && target.targetObject != null
                ? target.targetObject.GetType().Name
                : "unknown";
            return $"serialized field '{typeName}.{field}' not found; the owner source contract moved. Nothing written.";
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
        }
    }
}
#endif
