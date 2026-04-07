# Coralli Transfer Ledger

Status: `IN PROGRESS`
Verification: `PENDING VERIFICATION`

## Source Of Truth

- Raw source: `работа с кораллами.md`
- Optimized source: `Coralli.md`
- Transfer rule: optimized doc is primary wording, raw doc is used only as completeness cross-check

## Transfer Method

- Claude coral code is not copied wholesale into runtime.
- Source docs are treated as concept/data references; project architecture remains the source of runtime ownership.
- New files are created only when HECTON-8 has no equivalent family/rule/placeholder asset yet.
- Runtime integration stays inside existing first-party systems:
  - `WorldProceduralProxyAuthoring`
  - `WorldProceduralPlaceholderAuthoring`
  - `BiomeMatrixBootstrapAuthoring`
  - `WorldProceduralScatterDirector`
  - runtime stack rebuild assets
- Runtime world files own coral selection, weighting, and matrix-visible behavior only.
- Any future coral creation/bake work belongs in focused editor-only files, not in scatter/fill directors.
- `Proxy`, `final`, and runtime ownership must stay separate.
- Concepts transferred from the docs:
  - morphology and silhouette classes
  - reef shelter/porosity function
  - depth/readability cues
  - biome memory motifs
  - ecology weighting

## Already Integrated

| Source concept | Project adaptation | Target system | Status | Verification |
| --- | --- | --- | --- | --- |
| Massive coral head silhouettes | Added `family.coral.massive` + `rule.coral.massive` + placeholder recipe/prefab/material path | authoring + proxy + placeholder pipeline | Done | Assets generated |
| Plate coral shelf forms | Added `family.coral.plate` + `rule.coral.plate` + placeholder recipe/prefab/material path | authoring + proxy + placeholder pipeline | Done | Assets generated |
| Coral richness in reef/fertile biomes | Promoted `Coral Massive` and `Coral Plate` into matrix preferred cluster/structure families | `BiomeMatrixBootstrapAuthoring` | Done | Matrix rebuild + report |
| Coral role separation by silhouette | Improved placeholder recipes for low / branching / massive / plate coral families | `WorldProceduralPlaceholderAuthoring` | Done | Unity rebuild complete |
| Coral relevance to hotspot warmup | Added coral families into cheap proxy / warmup coverage where needed | `WorldProceduralScatterDirector` | Done | Code + preview rebuild |
| Coral creation-side anatomy pass | Moved coral proxy anatomy into editor-only flora shape builder with low / branching / massive / plate-specific forms | `WorldProceduralFloraProxyShapeBuilder` via editor proxy authoring | Done | Proxy prefab rebuild + asset readback |
| Baked coral/final intake path | Added dedicated flora-final intake so future photorealistic coral prefabs can be authored/generated in editor and linked as real final variants | `WorldProceduralFloraFinalVariantAuthoring` + `Assets/_Project/Prefabs/Nature/Flora/Baked` | Done | Runtime-stack rebuild log |
| Baked coral/final validator | Added validation for renderer/material/triangle budgets and forbidden runtime baggage on flora finals before they enter runtime stack | `WorldProceduralFloraFinalVariantValidator` | Done | Validation menu pass |
| Baked coral starter generator | Added dedicated editor-only generator that converts coral starter forms into owned combined-mesh baked finals under `Baked`, separate from proxy prefabs | `WorldProceduralFloraBakedStarterGenerator` | Done | Unity generator log + asset readback |

## Intentionally Not Ported 1:1

| Claude doc idea | Reason not copied directly | Project-safe adaptation |
| --- | --- | --- |
| Separate coral ecosystem runtime manager | Would bypass existing procedural authoring and create duplicate ownership | Converted to families, rules, matrix preferences |
| Overwritten bespoke spawn logic detached from biomes | Conflicts with matrix-driven world logic | Expressed through preferred categories and pattern-aware rules |
| Coral creation logic stuffed into large world runtime files | Blurs ownership and increases god-object risk | Keep creation/bake work in dedicated editor-side modules when needed |
| Pure concept text with no data binding | Not shippable | Only transferred concepts that bind to runtime data/assets |

## Analog Truth

- `CoralBootstrap`
  - covered by:
    - `WorldRuntimeBootstrapAuthoring`
    - flora starter generator / final intake / validator
    - runtime stack rebuild path
  - status: `covered by existing bootstrap chain`
- `CoralRenderer`
  - covered by:
    - biome/matrix family placement ownership
    - scatter/runtime selection
    - linked final-ready prefab variants
  - status: `covered for world placement and final hookup`
- `CoralPolyps`
  - no full direct analog exists yet
  - nearest owner layers:
    - scanner/discovery stack
    - fauna/ecology stack
    - flora final authoring stack
  - status: `not fully transferred`

## Pending Transfer

| Source concept | Planned adaptation | Blocker |
| --- | --- | --- |
| Further coral-biome weighting if current spread feels weak | Tune only after live counts/top-family data exists | Needs non-zero placement preview |
| Final shelter/readability polish for coral fields | Tune after actual placement density is visible | Waiting on another live-report pass after latest soft-water weighting update |

## Current Evidence

- `PROCEDURAL_MATRIX_BIOME_CONTENT_REPORT.md` now shows `Coral Massive` in preferred cluster categories and `Coral Plate` in preferred structure categories for representative reef/littoral biomes.
- `PROCEDURAL_MATRIX_BIOME_CONTENT_REPORT.md` now also shows non-zero live preview counts for coral-friendly patterns:
  - `FertileShallows`: cluster `11-17`, structure up to `4`, dominant cluster family often `Coral Branching`
  - `ReefNavigation`: cluster `8-12`, structure `0-4`, top/dominant readback no longer empty
  - `LandmarkCorridor` reef representative: top cluster `Egg Cluster`, dominant ground `Coral Low`, structure/spawn counts populated
- Fresh Unity menu pass `Hecton/Validation/Generate Procedural Matrix Biome Content Report` now completes without the earlier `ResetPlacementGrid` exception.
- Fresh report excerpts confirm coral families are present in live reef/fertile slices, not only in data:
  - `FertileShallows`: preferred cluster includes `Coral Massive`, dominant cluster can be `Coral Branching`
  - `ReefNavigation`: preferred cluster includes `Coral Massive`, preferred structure includes `Coral Plate`
  - `LandmarkCorridor / Fossil Gallows`: preferred structure includes `Coral Plate`, top ground `Kelp Tall`, dominant ground `Coral Low`
- Root cause found and corrected: `WorldProceduralFillDirector` in the live scene had not been rebuilt with the new reef rules/families. After `Hecton/Authoring/Rebuild World Runtime Stack`, the director now contains `rule.coral.massive`, `rule.coral.plate`, `family.coral.massive`, and `family.coral.plate`.
- Fresh report after runtime-stack rebuild shows coral structures and clusters entering live dominant slots:
  - `FertileShallows / White Alabaster Pools`: top structure `Coral Plate`, dominant structure `Coral Plate`, dominant cluster `Coral Massive`, structure role mix `bio 7`
  - `FertileShallows / Fossil Reef`: dominant structure `Coral Plate`, dominant cluster `Coral Massive`, structure role mix `bio 4`
  - `ReefNavigation / Fossil Reef`: top structure `Coral Plate`, dominant structure `Coral Plate`, dominant cluster `Coral Massive`, structure role mix `bio 8`
  - `ReefNavigation / Crystal Growth`: top structure `Coral Plate`, dominant structure `Coral Plate`, structure role mix `bio 4`
- After the follow-up soft-water matrix/scoring pass:
  - `FertileShallows / Fossil Gallows`: top structure `Coral Plate`, dominant structure `Coral Plate`
  - `FertileShallows / Mesa Plateaus`: dominant cluster remains `Coral Massive` while littoral structure top moved to `Kelp Canopy`
- After the landmark-corridor biological reweighting pass:
  - `LandmarkCorridor / Fossil Reef Context`: top and dominant structure `Coral Plate`, structure role mix `bio 7 / cave 3`
  - `LandmarkCorridor / Granite Escarpment Context`: top/dominant structure remained `Landmark Spire`
  - `LandmarkCorridor / Rift Spine Context`: top/dominant structure remained `Cave Entrance Marker`
- After the editor-only coral proxy anatomy pass:
  - `PFB_family_coral_low__bed.prefab`, `PFB_family_coral_branching__branch.prefab`, `PFB_family_coral_massive__head.prefab`, and `PFB_family_coral_plate__ledge.prefab` were rewritten on `2026-04-07 15:49`
  - YAML readback confirms family-specific multi-part forms instead of one generic primitive:
    - low coral -> repeated `Sphere` mound composition
    - branching coral -> mixed `Cylinder` + `Sphere` branch tips
    - massive coral -> layered `Sphere` head composition
    - plate coral -> stacked `Cylinder` shelves
- After the flora-final intake pass:
  - `Rebuild World Runtime Stack` now runs the full coral final pipeline in order:
    - `WorldProceduralFloraBakedStarterGenerator`
    - `WorldProceduralFloraFinalVariantAuthoring`
  - Rebuild-path verification is now explicit in Unity logs:
    - `Baked flora starters generated. Prefabs=14, MeshesUpdated=28, RemovedAssets=0, Failures=0`
    - `Baked flora final variants applied. FamiliesTouched=0, LinkedVariants=14, RemovedVariants=0, MissingFamilies=0`
  - Current intake state is clean: `FamiliesTouched=0, LinkedVariants=0, RemovedVariants=0, MissingFamilies=0`
  - `Assets/_Project/Prefabs/Nature/Flora/Baked` is now the dedicated root for future real coral finals instead of proxy/final mixing
- After the flora-final validation pass:
  - `WorldProceduralFloraFinalVariantValidator` now exists as a separate editor-only gate before real flora finals enter runtime stack
  - Current validation state is clean on the generated intake root: `PASS validatedPrefabs=14`
  - current coral-family quality coverage is explicit:
    - `family.coral.low=a0/g2`
    - `family.coral.branching=a0/g2`
    - `family.coral.massive=a0/g2`
    - `family.coral.plate=a0/g2`
    - all coral families are technically covered but still `generated-only`
- After the flora baked-starter generation pass:
  - `WorldProceduralFloraBakedStarterGenerator` created 14 owned starter finals and 28 mesh assets under `Assets/_Project/Prefabs/Nature/Flora/Baked`
  - coral-side generated finals now include:
    - `GEN_family_coral_low__bed`
    - `GEN_family_coral_low__plate`
    - `GEN_family_coral_branching__branch`
    - `GEN_family_coral_branching__mass`
    - `GEN_family_coral_massive__head`
    - `GEN_family_coral_massive__porous`
    - `GEN_family_coral_plate__ledge`
    - `GEN_family_coral_plate__shelf`
  - each generated final is now a `LODGroup`-based baked prefab, not a proxy hierarchy:
    - root `LODGroup`
    - `__LOD0` child renderer with `*_LOD0_Mesh.asset`
    - `__LOD1` child renderer with `*_LOD1_Mesh.asset`
  - validator budget checks are LOD-aware and do not double-count all LOD renderers
- After the flora-final relink pass:
  - initial intake pass: `FamiliesTouched=7, LinkedVariants=14, RemovedVariants=0, MissingFamilies=0`
  - follow-up kelp cleanup pass: `FamiliesTouched=1, LinkedVariants=14, RemovedVariants=1, MissingFamilies=0`
- After the flora-family override verification pass:
  - intake is now quality-aware per family: any non-`GEN_` coral final suppresses generated starters for that family
  - controlled test on `family.coral.plate`:
    - temporary authored coral prefab forced `FamiliesTouched=1, LinkedVariants=13, RemovedVariants=2, MissingFamilies=0`
    - generated coral-plate starters were removed from the family while the authored variant remained linked
    - cleanup + rebuild restored the generated coral-plate pair and kept validator state at `PASS validatedPrefabs=14`
- Current validation state on the generated coral intake root is now:
  - `PASS validatedPrefabs=14, warningCount=7`
  - coral-family coverage remains:
    - `family.coral.low=a0/g2`
    - `family.coral.branching=a0/g2`
    - `family.coral.massive=a0/g2`
    - `family.coral.plate=a0/g2`
- Coral final validation now also checks MX350-safe renderer defaults:
  - `shadowCastingMode=Off`
  - `receiveShadows=false`
  - `lightProbeUsage=Off`
  - `reflectionProbeUsage=Off`
  - `motionVectorGenerationMode=ForceNoMotion`
- Verification after this validator tightening stayed clean:
  - `PASS validatedPrefabs=14, warningCount=7`
  - generated coral starters emitted no extra renderer-default warnings
- `Generate Procedural Flora Final Status Report` now verifies coral-side baked final state explicitly:
  - all coral families are linked as real generated finals, not placeholder fallback
  - all coral prefabs currently have `LODGroup` coverage `2/2`
  - current coral max budget-triangle readback:
    - `family.coral.branching` -> `3392`
    - `family.coral.low` -> `2384`
    - `family.coral.massive` -> `2328`
    - `family.coral.plate` -> `320`
- Report path:
  - `PROCEDURAL_FLORA_FINAL_STATUS_REPORT.md`
- Linkage semantics are now explicitly corrected for future authored coral replacements:
  - if a coral family gets at least one authored flora final, expected linked count becomes `authored only`
  - suppressed `GEN_` starters in that family are no longer treated as a validator mismatch
- New family assets exist:
  - `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_coral_massive.asset`
  - `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_coral_plate.asset`
- New placement rule assets exist:
  - `Assets/_Project/Data/World/ProceduralPlacementRules/ProceduralRule_rule_coral_massive.asset`
  - `Assets/_Project/Data/World/ProceduralPlacementRules/ProceduralRule_rule_coral_plate.asset`
- Placeholder prefabs exist:
  - `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders/Flora/PFB_family_coral_massive_Placeholder.prefab`
  - `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders/Flora/PFB_family_coral_plate_Placeholder.prefab`

## Open Blockers

- `Coral Plate` and `Coral Massive` are now verified in live dominant slots across fertile/reef slices, and `LandmarkCorridor / Fossil Reef Context` is now fully coral-led at the top-structure level. Coral now has real generated baked finals in the intake root, but they are starter-quality combined-mesh assets, not photorealistic final art.
- The validator now proves that gap numerically: every coral family currently has authored count `0` and generated count `2`.
- No profiler GC before/after numbers were captured for this coral integration path.
- Report generation still emits `Leak Detected : Persistent allocates 5 individual allocations` without a stack trace.
- Flora-status verification path is no longer blocked by stale editor compile errors.
- The new blocker is session-level, not compile-level:
  - Unity MCP currently exposes `instance_count: 0`
  - direct HTTP readback confirms the same mismatch:
    - `http://127.0.0.1:8088/health` -> server healthy
    - `http://127.0.0.1:8088/api/instances` -> no registered Unity instances
    - Unity Editor process is still alive, so this is registration/session loss, not “editor closed”
  - latest budget-catalog/report enhancements on the coral side remain `PENDING VERIFICATION` until the Unity session reconnects
- Authored coral finals must now follow the explicit baked-root budget policy in `Assets/_Project/Prefabs/Nature/Flora/Baked/README.md`.
- Coral-side intake/report determinism is now hardened in code, pending live Unity confirmation:
  - supported coral family order is explicit, not `HashSet`-driven
  - discovered baked coral prefabs are sorted before `family.variants` linking
  - coral sections in `PROCEDURAL_FLORA_FINAL_STATUS_REPORT.md` are now emitted in stable family/prefab order
- Coral final intake now supports manual metadata tokens in prefab names, pending live Unity confirmation:
  - `__wN` overrides coral variant weight during flora-family linking
  - `__sMIN-MAX` overrides coral uniform scale range in percent
  - validator warns on malformed coral `w` / `s` tokens instead of silently falling back
  - status report now reads back per-prefab intake `weight` and `scale`
- Coral logical variant identity is now separated from intake metadata, pending live Unity confirmation:
  - coral `variantId` now ignores `__wN` / `__sMIN-MAX` suffixes
  - changing only coral intake metadata no longer churns `family.variants`
  - validator now fails duplicate coral prefabs that differ only by metadata tokens
- Coral authoring now also collapses duplicate logical variants before validation, pending live Unity confirmation:
  - intake keeps the first sorted coral variant for a duplicate `variantId`
  - later duplicates are skipped with an authoring warning instead of silently overwriting by enumeration order
  - if duplicates compete, coral intake now prefers the prefab with explicit `__w` / `__s` metadata over a default-only copy
- Coral metadata parsing is now stricter, pending live Unity confirmation:
  - one coral prefab name may contain only one `__wN` token and one `__sMIN-MAX` token
  - duplicate coral weight/scale tokens now raise explicit metadata errors
- MCP verification path is restored for coral-side work:
  - live Unity instance `Hecton8@5898b2fd69afdd2d`
  - fresh Unity passes on `2026-04-07`:
    - `Rebuild World Runtime Stack`
    - `Validate Procedural Flora Final Variants` -> `PASS validatedPrefabs=14, warningCount=7`
    - `Generate Procedural Flora Final Status Report`
    - `Rebuild 108 Biome Matrix`
    - `Validate 108 Biome Matrix`
    - `Generate Procedural Matrix Biome Content Report`
- Fresh live coral/runtime confirmation:
  - `FertileShallows / Fossil Gallows` -> top/dominant structure `Coral Plate`
  - `ReefNavigation / Sea-Stack Forest` -> top/dominant structure `Coral Plate`
  - `LandmarkCorridor / Sea-Stack Forest` -> top/dominant structure `Coral Plate`
  - generated coral finals still validate as `a0/g2` across all 4 coral families
- Fresh verified coral starter expansion on `2026-04-07`:
  - widened editor-only coral starter coverage from `2` to `3` forms per coral family
  - rebuild log:
    - `Baked flora starters generated. Prefabs=21, MeshesUpdated=42, RemovedAssets=0, Failures=0`
    - `Baked flora final variants applied. FamiliesTouched=7, LinkedVariants=21, RemovedVariants=0, MissingFamilies=0`
  - validator/report state:
    - `PASS validatedPrefabs=21, warningCount=7`
    - coral coverage is now `a0/g3` for:
      - `family.coral.low`
      - `family.coral.branching`
      - `family.coral.massive`
      - `family.coral.plate`
    - all coral families now report `LODGroup` coverage `3/3`
  - new coral generated forms now present under baked root:
    - `GEN_family_coral_low__knoll`
    - `GEN_family_coral_branching__fan`
    - `GEN_family_coral_massive__boulder`
    - `GEN_family_coral_plate__stack`
  - current coral budget-triangle readback after expansion:
    - `family.coral.branching` -> `4320`
    - `family.coral.low` -> `3840`
    - `family.coral.massive` -> `3840`
    - `family.coral.plate` -> `400`
- Existing fauna stack is now verified as the coral-side ecology bridge:
  - `Build Fauna Biome Datasets` rebuilt `108` fauna datasets
  - `AI_FAUNA_WORLD_INTEGRATION_REPORT.md` now has a dedicated `Reef And Littoral Flora Biomes` section with `None` warnings
  - coral-heavy slices already carry real passive/threat mixes through the existing fauna owner:
    - `Sea-Stack Forest` -> passive `3`, threat `3`, leviathan `1`
    - `Coral-Porous Walls` -> passive `3`, threat `1`
    - `Fossil Gallows` -> passive `3`, threat `2`
  - result: reef shelter / fish-around-coral concepts from the source docs are now explicitly grounded in `FaunaBiomeBootstrapAuthoring` / `FaunaWorldIntegrationReportGenerator`, not deferred to a separate coral ecology runtime manager
- Existing scanner/analyzer stack is now the coral intel bridge for authored flora contacts:
  - `ScannableCategoryUtility` classifies `flora / coral / kelp / seaweed / botany`
  - `ScannerTool` now counts coral/flora scan contacts in expedition sweeps
  - `EnvironmentalAnalyzerTool` now emits flora-specific recommendations instead of generic databank advice
