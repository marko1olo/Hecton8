# Rationale - PROCEDURAL_BIOME_BAKER_SHALLOWS

Status: PENDING VERIFICATION

## Decision 1 - Use Existing Bio-Forge Editor Owner

Problem: The prompt requires Safe Shallows L-system `BioRuleData` assets and generated LOD prefabs. Raw `.asset`/`.prefab` YAML edits would risk GUID/fileID corruption and violate the project YAML guard.

Solution: Use the existing editor-only `Hecton8.Editor.ProceduralGen` pipeline and add only the missing Safe Shallows automation surface if needed. This keeps runtime selection/scatter under `WorldProceduralScatterDirector` and keeps generation out of play mode.

Rejected Alternatives: Raw YAML asset creation was rejected because field names/GUID references are fragile. A new runtime scatter stack was rejected because `PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md` explicitly forbids parallel category scatter systems. Placeholder markdown/report output was rejected because `PROCEDURAL_ASSET_PIPELINE.md` forbids reports instead of production assets.

Scalability potential: Low uses static LOD prefabs, shared material, no per-plant CPU animation. Middle adds shader-driven sway/flow. High extends LOD residency and density. Ultra buys visual overkill through richer shader detail and emissions, still without per-flora Rigidbody or transform loops.

Hardware Impact: Expected runtime impact on i3/MX350 is mesh renderer + LODGroup cost only; procedural generation cost is editor-only. Approximate hot-path allocation saved versus runtime generation: all generator allocations, expected >100 us spike avoidance per streamed placement batch, exact profiler proof absent.

## Decision 2 - Visual Fake First For Kelp Motion And Coral Mass

Problem: Flora could be interpreted as physically simulated plants/coral, but the prompt only requires authored assets.

Solution: Bake static meshes with vertex color R height gradients for shader sway/biolum/motion masks. Coral bulk is SDF capsule blending. Kelp is thin upward L-system strips/branches, later animated by shader if material supports it.

Rejected Alternatives: Per-blade physics, Rigidbody collision, and runtime branch growth were rejected as waste. They do not add gameplay truth for scatter flora.

Scalability potential: Low disables expensive deformation and relies on LOD/impostor silhouettes. Middle uses one sway term. High/Ultra can add harmonics/emission masks in shader with unchanged CPU cost.

Hardware Impact: Avoids transform/physics loops for 200 generated prefabs; estimated savings versus 200 active per-object animation scripts is 200-600 us/frame on low-end silicon, pending profiler proof.

## Decision 3 - Deterministic Batch Seeds

Problem: Batch output must be reproducible while still producing unique variants.

Solution: Use deterministic integer seeds with fixed salts per family and variation index. This matches the slot-machine law: no wall-clock, no `UnityEngine.Random`, no object instance IDs as authority.

Rejected Alternatives: Unity random, time-based names, and manual duplicate-copy variants were rejected because they are not replayable and produce unstable assets.

Scalability potential: Low can keep fewer variants in scatter rules; High/Ultra can keep longer LOD residency and richer visible variety from the same deterministic set.

Hardware Impact: Deterministic offline generation has no runtime CPU impact. It prevents cache churn from runtime procedural variation and saves unpredictable streaming stalls; exact microsecond proof absent.
