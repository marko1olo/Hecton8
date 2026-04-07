# Vodorosli Transfer Ledger

Status: `IN PROGRESS`
Verification: `PENDING VERIFICATION`

## Source Of Truth

- Raw source: `работа с водорослями.md`
- Optimized source: `Vodorosli.md`
- Transfer rule: optimized doc is primary wording, raw doc is used only as completeness cross-check

## Transfer Method

- Claude seaweed/algae code is not copied wholesale into runtime.
- Source docs are treated as concept/data references; project architecture remains the source of runtime ownership.
- New files are created only when HECTON-8 has no equivalent family/rule/placeholder asset yet.
- Runtime integration stays inside existing first-party systems:
  - `WorldProceduralProxyAuthoring`
  - `WorldProceduralPlaceholderAuthoring`
  - `BiomeMatrixBootstrapAuthoring`
  - `WorldProceduralScatterDirector`
  - runtime stack rebuild assets
- Runtime world files own placement choice only. They do not own kelp mesh generation or prefab anatomy creation.
- Editor flora files own kelp shape construction and baked variant generation.
- `Proxy` and `final` are not interchangeable layers.
- Concepts transferred from the docs:
  - canopy/navigation silhouettes
  - shelter patch logic
  - shallow-water readability
  - biome memory motifs
  - density weighting

## Already Integrated

| Source concept | Project adaptation | Target system | Status | Verification |
| --- | --- | --- | --- | --- |
| Tall kelp silhouette | Fixed tall-kelp recipe resolution so `family.kelp.tall` no longer collapses into patch logic | `WorldProceduralPlaceholderAuthoring` | Done | Unity rebuild complete |
| Dense kelp patch biome presence | Added dense kelp warmup coverage and kept patch family active in scatter budgets | `WorldProceduralScatterDirector` | Done | Code + preview rebuild |
| Kelp canopy vertical navigation layer | Added `family.kelp.canopy` + `rule.kelp.canopy` + placeholder prefab/material path | authoring + proxy + placeholder pipeline | Done | Assets generated |
| Kelp canopy as structure memory in reef/littoral biomes | Injected into matrix preferred structure families where design fits | `BiomeMatrixBootstrapAuthoring` | Done | Matrix rebuild + report |
| Seaweed visual variation in shallow/fertile spaces | Improved kelp placeholder recipes for cleaner silhouette spread | `WorldProceduralPlaceholderAuthoring` | Done | Unity rebuild complete |
| Kelp creation-side anatomy pass | Rebuilt kelp proxy prefabs with ribbed stipe base, basal blades, mid fronds, and canopy crowns instead of bare cylinder-only shapes | `WorldProceduralFloraProxyShapeBuilder` via editor proxy authoring | Done | Proxy prefab rebuild + asset readback |
| Baked algae/final intake path | Added dedicated flora-final intake so future photorealistic kelp prefabs can be authored/generated in editor and linked as real final variants | `WorldProceduralFloraFinalVariantAuthoring` + `Assets/_Project/Prefabs/Nature/Flora/Baked` | Done | Runtime-stack rebuild log |
| Baked algae/final validator | Added validation for renderer/material/triangle budgets and forbidden runtime baggage on flora finals before they enter runtime stack | `WorldProceduralFloraFinalVariantValidator` | Done | Validation menu pass |
| Baked algae starter generator | Added dedicated editor-only generator that converts kelp starter forms into owned combined-mesh baked finals under `Baked`, separate from proxy prefabs | `WorldProceduralFloraBakedStarterGenerator` | Done | Unity generator log + asset readback |

## Intentionally Not Ported 1:1

| Claude doc idea | Reason not copied directly | Project-safe adaptation |
| --- | --- | --- |
| Standalone algae spawning subsystem | Duplicates existing procedural world stack | Routed through existing family/rule/matrix pipeline |
| Freeform runtime placement logic | Breaks deterministic scatter path and validation flow | Converted into biome preference + authoring data |
| Runtime kelp mesh generation inside scatter/fill directors | Creates god-object drift and mixes placement with asset construction | Kept creation-side in editor-only flora builder path |
| Decorative-only hero meshes with no family integration | Would become dead assets | Every transferred concept must exist as family/rule/biome content |

## Analog Truth

- `SeaweedRenderer`
  - covered by:
    - biome/matrix family placement ownership
    - `WorldProceduralScatterDirector`
    - linked final-ready prefab variants
    - streaming/runtime world stack
  - status: `covered for placement and final hookup`
- `SeaweedMeshGenerator`
  - covered by:
    - `WorldProceduralFloraProxyShapeBuilder`
    - `WorldProceduralFloraBakedStarterGenerator`
  - status: `covered for editor-side creation and baked starter generation`
- standalone seaweed runtime ecosystem subsystem
  - covered by:
    - `FaunaBiomeBootstrapAuthoring`
    - `FaunaDirector`
    - `HectonBoidController`
  - status: `covered for ecology pressure only`, not as a 1:1 algae manager

## Pending Transfer

| Source concept | Planned adaptation | Blocker |
| --- | --- | --- |
| Wider shallow-biome authoring nuance from doc text | Possible extra promotion rules if current matrix spread still undersells algae density | Needs live preview evidence first |
| Final in-scene beauty pass for algae readability | Tune only after preview counts and placement distribution are real | Waiting on another live-report pass after latest soft-water weighting update |

## Current Evidence

- `PROCEDURAL_MATRIX_BIOME_CONTENT_REPORT.md` now shows `Kelp Canopy` in preferred structure categories for reef/littoral representative biomes.
- `PROCEDURAL_MATRIX_BIOME_CONTENT_REPORT.md` now also shows non-zero live preview counts for algae-friendly patterns instead of dead-zero preview output:
  - `FertileShallows`: ground `68`, cluster `11-17`, spawn `9`
  - `ReefNavigation`: ground `67`, cluster `8-12`, structure `0-4`, spawn `7-8`
  - `SedimentResources`: structure list includes `Kelp Canopy`, with ground/cluster/structure/spawn counts populated
- Fresh Unity menu pass `Hecton/Validation/Generate Procedural Matrix Biome Content Report` now completes without the earlier `ResetPlacementGrid` exception.
- Fresh console readback from the same report pass shows live scatter converging to non-zero totals up to `desired=265 active=265`.
- Root cause found and corrected: `WorldProceduralFillDirector` in the live scene had not been rebuilt with the new algae/coral families and rules. After `Hecton/Authoring/Rebuild World Runtime Stack`, the director now contains `rule.kelp.canopy` and `family.kelp.canopy`.
- Fresh report after runtime-stack rebuild shows algae structures entering live reef/fertile structure mix instead of staying absent:
  - `FertileShallows / Littoral Karst`: dominant structure `Kelp Canopy`, structure role mix `bio 5`, `cave 2`
  - `ReefNavigation / Littoral Karst`: dominant structure `Kelp Canopy`, structure role mix `bio 7`, `cave 2`
  - `ReefNavigation / Fossil Reef`: preferred structure list keeps `Kelp Canopy`, while reef structure mix reaches `bio 8`
- After the follow-up soft-water matrix/scoring pass:
  - `FertileShallows / Mesa Plateaus`: top structure `Kelp Canopy`, dominant structure `Kelp Canopy`
  - `ReefNavigation / Archipelago Needles`: top structure `Kelp Canopy`, dominant structure `Kelp Canopy`
- After the landmark-corridor biological reweighting pass:
  - `LandmarkCorridor / Fossil Reef Context`: biological structures now dominate the structure mix (`bio 7 / cave 3`), with `Coral Plate` as both top and dominant structure while `Kelp Canopy` remains present in the preferred list
  - `LandmarkCorridor / Granite Escarpment Context`: top/dominant structure remained `Landmark Spire`
  - `LandmarkCorridor / Rift Spine Context`: top/dominant structure remained `Cave Entrance Marker`
- After the creation-side kelp proxy pass:
  - `PFB_family_kelp_tall__stalk.prefab`, `PFB_family_kelp_patch_dense__grove.prefab`, and `PFB_family_kelp_canopy__crown.prefab` were regenerated at `2026-04-07 15:17`
  - Asset readback confirms multi-part kelp anatomy in the generated prefabs, including `Cylinder`, `Cube`, and `Sphere` children instead of single-cylinder silhouettes
- The flora shape builder now owns both kelp and coral proxy anatomy logic, keeping future algae/coral creation-side work out of `WorldProceduralProxyAuthoring`.
- `Rebuild World Runtime Stack` now runs the full algae final pipeline in order:
  - `WorldProceduralFloraBakedStarterGenerator`
  - `WorldProceduralFloraFinalVariantAuthoring`
- Rebuild-path verification is now explicit in Unity logs:
  - `Baked flora starters generated. Prefabs=14, MeshesUpdated=28, RemovedAssets=0, Failures=0`
  - `Baked flora final variants applied. FamiliesTouched=0, LinkedVariants=14, RemovedVariants=0, MissingFamilies=0`
- Current intake status is clean and deterministic: `FamiliesTouched=0, LinkedVariants=0, RemovedVariants=0, MissingFamilies=0` because the baked-flora folder is still empty.
- `WorldProceduralFloraBakedStarterGenerator` now generates kelp starter finals as combined-mesh prefabs:
  - `GEN_family_kelp_tall__stalk`
  - `GEN_family_kelp_tall__lean`
  - `GEN_family_kelp_patch_dense__patch`
  - `GEN_family_kelp_patch_dense__patch_tall`
  - `GEN_family_kelp_canopy__crown`
  - `GEN_family_kelp_canopy__frond`
- Generated kelp finals are now LOD-based baked prefabs, not single-mesh roots:
  - each prefab owns `LODGroup`
  - `__LOD0` uses `*_LOD0_Mesh.asset`
  - `__LOD1` uses `*_LOD1_Mesh.asset`
  - verified YAML readback on `GEN_family_kelp_canopy__crown.prefab` confirms `LODGroup` + `__LOD0` / `__LOD1`
- `WorldProceduralFloraFinalVariantValidator` now passes on the generated baked-flora root: `PASS validatedPrefabs=14, warningCount=7`.
- The validator now also exposes algae-side quality state explicitly:
  - `family.kelp.tall=a0/g2`
  - `family.kelp.patch.dense=a0/g2`
  - `family.kelp.canopy=a0/g2`
  - all kelp families are technically covered but still `generated-only`
- Validator budgets are now LOD-aware:
  - highest visible LOD renderers are used for triangle/material-slot/renderer budgets
  - all renderers across the prefab are still scanned for null materials and forbidden runtime baggage
- Validator now also checks kelp final renderer defaults for MX350-safe baseline behavior:
  - `shadowCastingMode=Off`
  - `receiveShadows=false`
  - `lightProbeUsage=Off`
  - `reflectionProbeUsage=Off`
  - `motionVectorGenerationMode=ForceNoMotion`
- Verification after this rule pass stayed clean:
  - `PASS validatedPrefabs=14, warningCount=7`
  - generated kelp starters emitted no extra renderer-default warnings
- `Generate Procedural Flora Final Status Report` now verifies kelp-side baked final state explicitly:
  - all kelp families are linked as real generated finals, not placeholder fallback
  - all kelp prefabs currently have `LODGroup` coverage `2/2`
  - current kelp max budget-triangle readback:
    - `family.kelp.patch.dense` -> `5712`
    - `family.kelp.tall` -> `1928`
    - `family.kelp.canopy` -> `1124`
- Report path:
  - `PROCEDURAL_FLORA_FINAL_STATUS_REPORT.md`
- Linkage semantics are now explicitly corrected for future authored kelp replacements:
  - if a kelp family gets at least one authored flora final, expected linked count becomes `authored only`
  - suppressed `GEN_` starters in that family are no longer treated as a validator mismatch
- `WorldProceduralFloraFinalVariantAuthoring` now links the generated starter finals into flora families as real `finalReady` variants:
  - initial intake pass: `FamiliesTouched=7, LinkedVariants=14, RemovedVariants=0, MissingFamilies=0`
  - follow-up kelp cleanup pass: `FamiliesTouched=1, LinkedVariants=14, RemovedVariants=1, MissingFamilies=0`
- Flora final intake is now quality-aware per family:
  - if a family gets at least one non-`GEN_` authored baked flora prefab, intake ignores `GEN_` starters for that family instead of mixing visual quality tiers
  - clean rebuild still restores generated starters when no authored final exists for the family
- New family asset exists: `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_kelp_canopy.asset`
- New placement rule asset exists: `Assets/_Project/Data/World/ProceduralPlacementRules/ProceduralRule_rule_kelp_canopy.asset`
- Placeholder prefab exists: `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders/Flora/PFB_family_kelp_canopy_Placeholder.prefab`

## Open Blockers

- `Kelp Canopy` is verified in the key littoral reef/fertile slices, and the fossil-reef landmark corridor is now biologically led by `Coral Plate`. Kelp now has real generated baked finals in the intake root, but they are starter-quality combined-mesh assets, not photorealistic final art.
- The validator now proves that gap directly: every kelp family currently has authored count `0` and generated count `2`.
- No profiler GC before/after numbers were captured for this flora integration path.
- Report generation still emits `Leak Detected : Persistent allocates 5 individual allocations` without a stack trace.
- Flora-status verification path is no longer blocked by stale editor compile errors.
- The new blocker is session-level, not compile-level:
  - Unity MCP currently exposes `instance_count: 0`
  - direct HTTP readback confirms the same mismatch:
    - `http://127.0.0.1:8088/health` -> server healthy
    - `http://127.0.0.1:8088/api/instances` -> no registered Unity instances
    - Unity Editor process is still alive, so this is registration/session loss, not “editor closed”
  - latest budget-catalog/report enhancements on the kelp side remain `PENDING VERIFICATION` until the Unity session reconnects
- Authored kelp finals must now follow the explicit baked-root budget policy in `Assets/_Project/Prefabs/Nature/Flora/Baked/README.md`.
- Kelp-side intake/report determinism is now hardened in code, pending live Unity confirmation:
  - supported kelp family order is explicit, not `HashSet`-driven
  - discovered baked kelp prefabs are sorted before `family.variants` linking
  - kelp sections in `PROCEDURAL_FLORA_FINAL_STATUS_REPORT.md` are now emitted in stable family/prefab order
- Kelp final intake now supports manual metadata tokens in prefab names, pending live Unity confirmation:
  - `__wN` overrides kelp variant weight during flora-family linking
  - `__sMIN-MAX` overrides kelp uniform scale range in percent
  - validator warns on malformed kelp `w` / `s` tokens instead of silently falling back
  - status report now reads back per-prefab intake `weight` and `scale`
- Kelp logical variant identity is now separated from intake metadata, pending live Unity confirmation:
  - kelp `variantId` now ignores `__wN` / `__sMIN-MAX` suffixes
  - changing only kelp intake metadata no longer churns `family.variants`
  - validator now fails duplicate kelp prefabs that differ only by metadata tokens
- Kelp authoring now also collapses duplicate logical variants before validation, pending live Unity confirmation:
  - intake keeps the first sorted kelp variant for a duplicate `variantId`
  - later duplicates are skipped with an authoring warning instead of silently overwriting by enumeration order
  - if duplicates compete, kelp intake now prefers the prefab with explicit `__w` / `__s` metadata over a default-only copy
- Kelp metadata parsing is now stricter, pending live Unity confirmation:
  - one kelp prefab name may contain only one `__wN` token and one `__sMIN-MAX` token
  - duplicate kelp weight/scale tokens now raise explicit metadata errors
- MCP verification path is restored for kelp-side work:
  - live Unity instance `Hecton8@5898b2fd69afdd2d`
  - fresh Unity passes on `2026-04-07`:
    - `Rebuild World Runtime Stack`
    - `Validate Procedural Flora Final Variants` -> `PASS validatedPrefabs=14, warningCount=7`
    - `Generate Procedural Flora Final Status Report`
    - `Rebuild 108 Biome Matrix`
    - `Validate 108 Biome Matrix`
    - `Generate Procedural Matrix Biome Content Report`
- Fresh live kelp/runtime confirmation:
  - `FertileShallows / Mesa Plateaus` -> top/dominant structure `Kelp Canopy`
  - `ReefNavigation / Archipelago Needles` -> top/dominant structure `Kelp Canopy`
  - generated kelp finals still validate as `a0/g2` across all 3 kelp families
- Fresh verified kelp starter expansion on `2026-04-07`:
  - widened editor-only kelp starter coverage from `2` to `3` forms per kelp family
  - rebuild log:
    - `Baked flora starters generated. Prefabs=21, MeshesUpdated=42, RemovedAssets=0, Failures=0`
    - `Baked flora final variants applied. FamiliesTouched=7, LinkedVariants=21, RemovedVariants=0, MissingFamilies=0`
  - validator/report state:
    - `PASS validatedPrefabs=21, warningCount=7`
    - kelp coverage is now `a0/g3` for:
      - `family.kelp.tall`
      - `family.kelp.patch.dense`
      - `family.kelp.canopy`
    - all kelp families now report `LODGroup` coverage `3/3`
  - new kelp generated forms now present under baked root:
    - `GEN_family_kelp_tall__ribbon`
    - `GEN_family_kelp_patch_dense__ring`
    - `GEN_family_kelp_canopy__fan`
  - current kelp budget-triangle readback after expansion:
    - `family.kelp.patch.dense` -> `9576`
    - `family.kelp.tall` -> `1952`
    - `family.kelp.canopy` -> `1160`
- Fresh verified kelp mesh-builder replacement on `2026-04-07`:
  - added `WorldProceduralSeaweedMeshBuilder` as a focused editor-only kelp mesh owner instead of pushing more anatomy logic into the general flora generator
  - `WorldProceduralFloraBakedStarterGenerator` now routes kelp families through procedural mesh generation and keeps coral on the existing proxy-combine path
  - verified Unity passes:
    - `Generate Procedural Flora Baked Starters` -> `Prefabs=21, MeshesUpdated=42, RemovedAssets=0, Failures=0`
    - `Apply Procedural Flora Final Variants` -> `FamiliesTouched=0, LinkedVariants=21, RemovedVariants=0, MissingFamilies=0`
    - `Validate Procedural Flora Final Variants` -> `PASS validatedPrefabs=21, warningCount=7`
    - `Generate Procedural Flora Final Status Report`
  - verified kelp budget-triangle readback after the mesh-builder swap:
    - `family.kelp.tall` -> `584`
    - `family.kelp.patch.dense` -> `496`
    - `family.kelp.canopy` -> `684`
  - verified kelp LOD cascade after the same pass:
    - each generated kelp prefab now carries `4` LOD levels
    - status report readback is now `renderers=4`, `lodGroups=1`, `lodLevels=4` for:
      - `family.kelp.tall`
      - `family.kelp.patch.dense`
      - `family.kelp.canopy`
    - coral generated starters still stay on `2` LOD levels by design
  - verified shader/material path:
    - added dedicated shader `Assets/_Project/Art/Shaders/Hecton_KelpMaster.shader`
    - added editor-only material owner `WorldProceduralFloraMaterialAuthoring`
    - kelp materials were re-authored onto the kelp shader with instancing enabled
    - verified authoring pass: `Applied flora materials. TouchedMaterials=3`
    - flora validator still passes after the shader switch
  - verified procedural kelp texture stack on `2026-04-07`:
    - added editor-only texture owner `WorldProceduralFloraTextureAuthoring`
    - kelp now gets generated `Base`, `Detail`, `Normal`, and `Mask` textures per family in `Assets/_Project/Art/Textures/WorldProceduralFlora`
    - kelp shader now consumes `_NormalMap` and `_MaskMap`, not only flat albedo/detail breakup
    - verified Unity passes:
      - `Generate Procedural Flora Textures` -> `TouchedTextures=12`
      - `Apply Procedural Flora Materials` -> `TouchedMaterials=3`
      - `Generate Procedural Flora Baked Starters` -> `Prefabs=21, MeshesUpdated=60, RemovedAssets=0, Failures=0`
      - `Validate Procedural Flora Final Variants` -> `PASS validatedPrefabs=21, warningCount=7`
    - verified material readback:
      - `MAT_family_kelp_tall.mat` now binds `_BaseMap`, `_DetailMap`, `_NormalMap`, `_MaskMap`
      - kelp material tuning now includes `_NormalStrength`, `_ThicknessStrength`, `_SpecularNoiseStrength`
  - validator hardening on `2026-04-07` exposed a non-kelp defect:
    - once kelp material completeness checks were added, validator also started warning about flora materials with instancing disabled
    - real hit: coral materials `MAT_family_coral_*` were all shipping with instancing off
    - fixed in `WorldProceduralFloraMaterialAuthoring` by hardening all flora materials, not just kelp
    - verified Unity pass after the fix:
      - `Apply Procedural Flora Materials` -> `TouchedMaterials=7`
      - `Validate Procedural Flora Final Variants` -> `PASS validatedPrefabs=21, warningCount=7`
    - verified readback:
      - `MAT_family_coral_low.mat` now has `m_EnableInstancingVariants: 1`
      - `_ReceiveShadows`, `_EnvironmentReflections`, `_SpecularHighlights` are now zeroed on that coral material
- Another real validator blind spot was removed on `2026-04-08`:
  - before the fix, material completeness checks ran only on `budgetRenderers`, which for LOD prefabs effectively meant `LOD0`
  - lower kelp LOD renderers could have drifted to the wrong shader or missing textures and still passed validation
  - validator now checks material completeness across all prefab renderers, while budget counts still use the LOD0/budget slice only
  - verified Unity pass after the fix:
    - `Validate Procedural Flora Final Variants` -> `PASS validatedPrefabs=21, warningCount=7`
  - result: the remaining warnings are still only the authored photoreal gap, not a hidden LOD material regression
- Status report blind spot was also removed on `2026-04-08`:
  - before the fix, `PROCEDURAL_FLORA_FINAL_STATUS_REPORT.md` could say a prefab looked healthy while saying nothing about shader/material integrity
  - report generation now publishes:
    - family-level `Material Ready`
    - per-prefab `material=...`
    - per-prefab `renderState=...`
  - verified Unity pass:
    - `Generate Procedural Flora Final Status Report`
  - verified readback:
    - all 3 kelp families currently show `Material Ready 3/3`
    - current kelp starters read back as `material=ok | renderState=ok`
- LOD cascade blind spot was also removed on `2026-04-08`:
  - before the fix, kelp/coral starters only proved that an `LODGroup` existed; there was no proof that later LOD meshes were actually cheaper
  - validator now fails non-descending LOD triangle cascades
  - status report now shows `lodTriangles=` per prefab and `LOD Cascade` per family
  - verified Unity passes:
    - `Validate Procedural Flora Final Variants` -> `PASS validatedPrefabs=21, warningCount=7`
    - `Generate Procedural Flora Final Status Report`
  - verified kelp readback examples:
    - `GEN_family_kelp_tall__stalk` -> `584/302/128/50`
    - `GEN_family_kelp_patch_dense__patch` -> `414/184/78/24`
    - `GEN_family_kelp_canopy__crown` -> `684/380/184/82`
- Authored-final intake is now fail-closed for broken metadata on `2026-04-08`:
  - before the fix, an authored kelp prefab with malformed `__w/__s` tokens could still be linked with fallback defaults
  - `WorldProceduralFloraFinalVariantAuthoring` now skips invalid metadata prefabs entirely and logs the reason
  - controlled Unity test:
    - temporary prefab: `PFB_family_kelp_tall__hero_bad__s92-108__s80-120.prefab`
    - authoring log confirmed skip because of duplicate scale token
    - family linkage stayed stable at `LinkedVariants=21`, with no generated kelp finals displaced
  - delta vs previous generated set:
    - `family.kelp.tall` was `1952`
    - `family.kelp.patch.dense` was `9576`
    - `family.kelp.canopy` was `1160`
  - import hygiene also improved:
    - generated kelp mesh object names now match mesh asset filenames, so the previous name-mismatch warning path is gone
- Existing fauna stack is now verified as the kelp-side ecology bridge:
  - `Build Fauna Biome Datasets` rebuilt `108` fauna datasets
  - `AI_FAUNA_WORLD_INTEGRATION_REPORT.md` now has a dedicated `Reef And Littoral Flora Biomes` section with `None` warnings
  - kelp-heavy slices already carry real passive/threat mixes through the existing fauna owner:
    - `Archipelago Needles` -> `Shore Skimmer`, `Kelp Raylet`, `Brine Siphoner`, `Nursery Shellguard`, `Needle Hunter`
    - `Mesa Plateaus` -> passive `3`, threat `1`
- Existing scanner/analyzer stack is now the kelp intel bridge for authored flora contacts:
  - `ScannableCategoryUtility` classifies `flora / coral / kelp / seaweed / botany`
  - `ScannerTool` now counts kelp/flora scan contacts in expedition sweeps
  - `EnvironmentalAnalyzerTool` now emits flora-specific recommendations instead of generic databank advice
  - result: fish/shelter ecology around kelp is now explicitly tied to `FaunaBiomeBootstrapAuthoring` / `FaunaWorldIntegrationReportGenerator`, not left as a future standalone algae-fauna subsystem
