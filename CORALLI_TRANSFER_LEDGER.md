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

## Intentionally Not Ported 1:1

| Claude doc idea | Reason not copied directly | Project-safe adaptation |
| --- | --- | --- |
| Separate coral ecosystem runtime manager | Would bypass existing procedural authoring and create duplicate ownership | Converted to families, rules, matrix preferences |
| Overwritten bespoke spawn logic detached from biomes | Conflicts with matrix-driven world logic | Expressed through preferred categories and pattern-aware rules |
| Pure concept text with no data binding | Not shippable | Only transferred concepts that bind to runtime data/assets |

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
  - `LandmarkCorridor / Fossil Reef Context`: dominant structure `Coral Plate`, structure role mix `bio 7 / cave 3`, but top structure still `Cave Entrance Marker`
  - `LandmarkCorridor / Granite Escarpment Context`: top/dominant structure remained `Landmark Spire`
  - `LandmarkCorridor / Rift Spine Context`: top/dominant structure remained `Cave Entrance Marker`
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

- `Coral Plate` and `Coral Massive` are now verified in live dominant slots across fertile/reef slices and in `LandmarkCorridor / Fossil Reef Context`. Remaining coral-side tuning is narrower: that corridor slice still keeps a single `Cave Entrance Marker` as top structure even after biological dominance is established.
- No profiler GC before/after numbers were captured for this coral integration path.
