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
| Kelp creation-side anatomy pass | Rebuilt kelp proxy prefabs with ribbed stipe base, basal blades, mid fronds, and canopy crowns instead of bare cylinder-only shapes | `WorldProceduralProxyAuthoring` | Done | Proxy prefab rebuild + asset readback |

## Intentionally Not Ported 1:1

| Claude doc idea | Reason not copied directly | Project-safe adaptation |
| --- | --- | --- |
| Standalone algae spawning subsystem | Duplicates existing procedural world stack | Routed through existing family/rule/matrix pipeline |
| Freeform runtime placement logic | Breaks deterministic scatter path and validation flow | Converted into biome preference + authoring data |
| Decorative-only hero meshes with no family integration | Would become dead assets | Every transferred concept must exist as family/rule/biome content |

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
  - `LandmarkCorridor / Fossil Reef Context`: biological structures now dominate the structure mix (`bio 7 / cave 3`), with `Coral Plate` as dominant structure while `Kelp Canopy` remains present in the preferred list
  - This pass did not disturb hard corridor slices like `Granite Escarpment` or `Rift Spine`
- After the creation-side kelp proxy pass:
  - `PFB_family_kelp_tall__stalk.prefab`, `PFB_family_kelp_patch_dense__grove.prefab`, and `PFB_family_kelp_canopy__crown.prefab` were regenerated at `2026-04-07 15:17`
  - Asset readback confirms multi-part kelp anatomy in the generated prefabs, including `Cylinder`, `Cube`, and `Sphere` children instead of single-cylinder silhouettes
- New family asset exists: `Assets/_Project/Data/World/ProceduralFamilies/ProceduralFamily_family_kelp_canopy.asset`
- New placement rule asset exists: `Assets/_Project/Data/World/ProceduralPlacementRules/ProceduralRule_rule_kelp_canopy.asset`
- Placeholder prefab exists: `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders/Flora/PFB_family_kelp_canopy_Placeholder.prefab`

## Open Blockers

- `Kelp Canopy` is now verified as both dominant and top structure in the key littoral reef/fertile slices. Remaining algae-side tuning is narrower: `LandmarkCorridor / Fossil Reef Context` is no longer cave-dominant, but its top structure is still `Cave Entrance Marker` instead of a biological silhouette.
- No profiler GC before/after numbers were captured for this flora integration path.
