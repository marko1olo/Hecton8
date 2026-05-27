# Flora Pipeline Docs

Date: 2026-05-18
Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Purpose: canonical active bundle for flora execution, prompts, and import-state tracking.

## 2026-05-18 R11 Active Evidence Boundary

- Historical data boundary snapshot: `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`.
- Historical manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.
- Historical actuality manifest: `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json`.
- Current actuality ledger: `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.
- Current documentation/source orientation: `Docs/Reports/2026-05-18_DOCUMENTATION_R15_NAVIGATION_SUPERSESSION_R16_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_ACTIVE_ENTRYPOINT_NAVIGATION_R15_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_BATCH008_BINARY_HYGIENE_R14_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_GENERIC_REPORT_BOUNDARIES_R13_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_ACTIVE_REMAINDER_R11_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_LONGTAIL_INTERIOR_R10_LOCAL.md`, and `Docs/Reports/2026-05-18_DOCUMENTATION_EVIDENCE_LANGUAGE_AND_COUNTERS_R9_LOCAL.md`.
- R11 capture-time static counters supersede older broad source-count text in this bundle where exact values differ: `1742` project C# files, `1689` script C# files, `1725` non-test C# files, `1138660` project source lines, `1119546` script source lines, `1134363` non-test source lines, `296` project-wide interface declaration hits, `294` script interface hits, `63` direct public interfaces in `GlobalRegistryContracts.cs`, and `107` first-party asmdefs. These counts are volatile under concurrent agents; rerun `rg` before treating exact values as current.
- Visual-realistic-fake doctrine snapshot: `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`; re-check `.agents-skills` for newer mandates before implementation.
- May 14/R43 CLI compile notes are historical `CLI_COMPILE` evidence only. They do not certify Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, scene wiring, frame-time, memory, or visual quality under the current dirty workspace.
- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.
- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.
## Historical May 4 Boundary

- Read current stable docs and the DOC_GLOBAL R16/R15/R14/R13/R11/R10/R9 reports before using May 4 / May 6 / May 11 reports as context. The older reports are historical unless current source or a fresh evidence artifact revalidates a claim.
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

[FAIL] Propwash deforms too hard: clamp wake scalar in shader, reduce high-fidelity harmonics first, and avoid adding collision-driven plant motion.
