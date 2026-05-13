# Flora Pipeline Docs

Date: 2026-05-07
Status: PENDING VERIFICATION

Purpose: canonical active bundle for flora execution, prompts, and import-state tracking.

## 2026-05-11 Current-State Override

- Current data boundary: `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`.
- Current manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.
- Current visual-realistic-fake doctrine: `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`.
- May 13 DOC_AUDIT override: the cited May 11 compile artifact is absent from the current filesystem; treat the May 11 compile-success line as stale report text until restored or replaced. Runtime, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, import, scene wiring, and visual quality remain `PENDING VERIFICATION`.
- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.
- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.
## 2026-05-04 Current-State Boundary

- Read `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`, `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`, `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`, and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` before using this bundle as current project truth.
- This bundle is the active flora execution reference, not proof of final imported textures, materials, GPUI/runtime validation, or scene wiring.
- Active architecture remains `Docs/PROCEDURAL_ASSET_PIPELINE.md` + `Docs/PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md`; do not create a parallel flora runtime.

## Files

- `AI_FLORA_EXECUTION_BRIEF.md` - short entry point and owner stack.
- `FLORA_SYSTEM_PLAN.md` - compact implementation plan.
- `FLORA_NEXT_DIALOG_PROMPT.md` - working prompt for the next flora implementation pass.
- `FLORA_TEXTURE_IMPORT_LOG.md` - imported texture intake history.

## Authority

Use this bundle together with:

1. `../AGENTS.md`
2. `../PROCEDURAL_ASSET_PIPELINE.md`
3. `AI_FLORA_EXECUTION_BRIEF.md`
4. `FLORA_SYSTEM_PLAN.md`
5. `../PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md`
6. `FLORA_NEXT_DIALOG_PROMPT.md`

## 2026-05-12 DOC_VULCAN Technical Requirements

Status: SOURCE-SCANNED, RUNTIME PENDING VERIFICATION.

[SOURCE] Flora visual growth and decay are shader-owned. `Assets/_Project/Art/Shaders/Hecton_KelpMaster.shader` and `Assets/_Project/Art/Shaders/Hecton_CoralMaster.shader` consume `_HectonFloraLifecycleParams` and vertex color age channels. `Assets/_Project/Art/Shaders/FloraCulling.compute` owns GPU culling support.

[REQ] Flora must express growth through age-based vertex morphing, material response, and visibility selection. The CPU must not scale thousands of flora transforms to fake growth. CPU scaling burns transform bandwidth, breaks batching, and creates visible sync errors when BRG or indirect rendering owns the draw.

[REQ] Age must travel as instance data, vertex color, or packed lifecycle parameter. The vertex shader must use that scalar to bend, retract, desaturate, bloom, or fade geometry. The fragment shader must reinforce the fake with albedo, emission, and alpha changes.

[REQ] Propwash, player proximity, submarine wake, and flow response must stay as shader or compute deformation. Do not attach per-plant physics components.

[REQ] Texture memory must use atlased BC7 color and BC5 normal/detail maps. New flora texture families must cap source size at 2048 px unless an explicit top-tier-only material variant proves the value. Unique uncompressed masks are forbidden for runtime flora.

[REQ] Flora LOD must follow Math LOD tiers:

- Low: static impostor or low-frequency vertex sway; shared atlas; coarse culling; no per-instance CPU animation.
- Middle: age morph, flow-field bend, and standard atlas detail.
- High: denser visible instances, richer wind/current harmonics, and stronger emissive fake.
- Ultra: visual overkill through extra shader detail, bloom masks, and particles; still no per-plant Rigidbody or transform loop.

### Whale Fall And Bone Decay Visuals

[REQ] Whale fall flora/scavenger dressing must register a POI through AUP-aware systems, then render decay as weighted scavenger density, color darkening, dissolves, and bone material erosion. The system must not simulate carcass chemistry.

[REQ] Bone-decay shaders must use scalar age, local noise, and cheap mask thresholds. The visual should imply consumption, not model it.

### Flow Field And Seaweed

[REQ] Kelp, plankton weeds, and tendril fields must sample the same abyssal flow authority documented in Scatter Runtime. Flow changes must arrive as buffer or global shader state. Per-object current forces are forbidden for mass flora.

### Indirect Vegetation AUP Contract

[SOURCE] `HectonIndirectVegetationRenderer.cs` implements `IOriginShiftListener` for indirect vegetation.

[REQ] Origin shifts must subtract the shift offset from cached cull-camera position, previous motion-vector camera position, and explicit draw bounds. The renderer must clear previous motion camera, far-culling snapshot, and GPU culling frame index after a shift.

[REQ] Indirect vegetation must refresh presentation caches after AUP shift. Do not rebuild all vegetation buffers as a correctness shortcut. The correct fake is a cache rebase plus one conservative cull refresh.

### Troubleshooting

[FAIL] Flora grows by popping scale: remove CPU transform scaling and route age into packed instance data or shader parameters.

[FAIL] Sway freezes after culling: verify culling output, lifecycle buffer binding, flow-field texture binding, and material keyword parity.

[FAIL] Vegetation pops or smears after AUP shift: verify `IOriginShiftListener` registration, cached cull-camera rebase, motion-vector cache reset, far-cull snapshot invalidation, and explicit bounds shift.

[FAIL] VRAM budget spikes: merge color into BC7 atlases, move normals/detail to BC5, reject 4K unique maps, and audit duplicate materials.

[FAIL] Propwash deforms too hard: clamp wake scalar in shader, reduce high-tier harmonics first, and avoid adding collision-driven plant motion.
