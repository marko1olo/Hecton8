# Rationale 2007

Evidence class: STATIC_DOC / STATIC_SOURCE
Unity/MCP/build/import: NOT RUN

## Decisions

1. Treated shoreline foam as a premium screen-space visual fake, not physical water simulation.
Reason: `SHINOBU_277_CREST_SHORELINE_FOAM_GRAFT.md`, `Hidden_Hecton_OceanDepthFoam.shader`, and the cinematic cheat mandate all route shoreline contact through camera depth, DTO rows, and shader math.

2. Kept Crest as vendor boundary.
Reason: AGENTS third-party rule and `CREST_VERSION_QUARANTINE_SHINOBU_260.md` forbid casual Crest mutation. Static scan shows project-side prefab flags for Crest depth/foam disabled, but Unity proof is still required.

3. Marked RenderGraph structure as static-only.
Reason: `RecordRenderGraph` source exists, but source does not prove pass order, active renderer installation, hidden Crest state, GPU cost, or visual result.

4. Required no-Assets screenshot/profiler routing.
Reason: `UNITY_VISUAL_PROOF_CAPTURE_RUNBOOK_20260604.md` and root rules forbid proof artifacts under `Assets` because they trigger Unity import churn and corrupt evidence.

5. Carried 1907 coastline material gaps into blockers.
Reason: ocean proof cannot pass if shore foam, wet basalt, terrain control, waterline masks, and caustic/shallow transition sources remain missing or unbound.

6. Classified atmosphere async readback as risk, not failure.
Reason: source uses opt-in `AsyncGPUReadback.RequestIntoNativeArray`; acceptance requires cadence/no-blocking profiler proof, not static rejection.

7. Treated critique capture as reference only.
Reason: runbook explicitly says `unity_focus_state_20260604_125701.png` is critique target only. It cannot prove before/after, Game View acceptance, profiler, or player truth.

## Low / Middle / High / Ultra Consequences

- Low/Compact: fewer foam rows, lower loop count, simpler normals, lower wake/caustic cadence; ocean color, waterline, wet rock, route silhouettes, and photic clarity still required.
- Middle: default player-facing waterline/foam and shallow readability must look genuinely good.
- High: richer foam breakup, normal perturbation, wake and caustic detail only after profiler proof.
- Ultra: visual overkill only; no gameplay truth, DTO, ownership, save, or rollback changes.

