# Rationale_3103 - WATER_CREST_FOAM_CAUSTIC_OWNER

Decision: classify 3103 as static owner planning, not Unity mutation.

Reason:
- Unity is not running and no Unity MCP/tool lane is available.
- A `dotnet` process is active; build launch is forbidden under the current process gate.
- The task explicitly allows readback only when process gate is clean and Unity tools are available. That condition is false.

Decision: do not patch Crest `_WD_*`, `_MainTex`, or `_Skybox` GUIDs.

Reason:
- The unresolved GUIDs repeat in `Ocean.mat`, `Ocean-Underwater.mat`, and `MAT_H8_SurfaceCrestOcean_1428.mat`.
- Canonical Crest shader and normal GUIDs resolve.
- Static evidence supports runtime/stale Crest wave-data slots, not missing artist texture slots.
- Replacing these slots by raw YAML or artist textures would violate Crest material integrity and could corrupt Crest runtime wave-data ownership.

Decision: keep `Ocean.mat` as the first Unity proof route.

Reason:
- `Ocean_Crest.prefab` binds `Ocean.mat`.
- `02_HECTON_WORLD.unity` overrides the same `_material` to `Ocean.mat` and `_createFoamSim` to `1`.
- `MAT_H8_SurfaceCrestOcean_1428.mat` is not active route proof and has stronger foam/caustic values that can recreate sheet/ribbon artifacts if assigned raw.

Decision: reject raw curtain/slab/caustic activation.

Reason:
- `Ocean_UnderwaterCurtain.mat` has extreme values (`_CausticsStrength: 10`, `_FoamScale: 15`, green foam bubble color) and no current proven volume route.
- `H8_FloorCausticSoft_1443` renderer is disabled; enabling it without a shallow-light owner gate would create unsupported caustic projection.
- Water bible rejects global caustics without a believable light/depth reason.

Regression model:
- CPU/GC: no runtime code changed; measured proof absent.
- Memory/VRAM: no asset binding changed; measured proof absent.
- Cadence: no systems changed.
- Correctness: static owner plan reduces risk of wrong material slot mutation; runtime result still unproven.
- Failure modes: stale Crest slot misclassification, transparent foam sorting, unbounded caustic fake, green curtain reactivation, underwater material overwrite by Crest copy-each-frame route.

Hot path impact:
- None from this agent. No code or Unity asset mutation.

First-20-minutes route relevance:
- Removes a product-face blocker for the first semi-open surface/shallow exit: water, foam, and caustics must be bright, readable, premium, and owner-correct before the route can pass visual floor.
