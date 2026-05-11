# HECTON-8 Architecture Index

Date: 2026-05-11
Status: PENDING VERIFICATION

Purpose: stable index for `Docs/ARCHITECTURE`. These files are long-lived system contracts. They are not dated progress reports.

## Authority Boundary

- Global rules start at `../../AGENTS.md`.
- Task rules start at `../../.agents-skills/README.md` plus task-relevant mandate files.
- Project navigation starts at `../README.md`, `../HECTON8_GLOBAL_ARCHITECTURE_MAP.md`, `../HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`, `../SYSTEMS_CONTRACTS.md`, and `../QUALITY_GATES.md`.
- This folder owns stable architecture contracts and the cinematic-cheat ledger.
- `.diff` files in this folder are evidence/provenance only. They are not policy by themselves.
- Dated reports under `../Reports/` and `../ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/` are evidence snapshots. Promote durable policy into stable files before treating it as project doctrine.

## Current Proof Boundary

Latest local compile-only evidence: `../../CodexArtifacts/2026-05-11_DOCS_CONTINUATION_CORE_BUILD_R1.summary.txt`.

Known proven slice: local Core dependency build succeeded with `0 Warning(s)` and `0 Error(s)`.

Not proven by this folder: Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality.

## Read Order

1. `CINEMATIC_CHEATS_LEDGER.md` - visual-realistic-fake doctrine and physical-simulation rejection gate.
2. `SYSTEM_INTERCONNECT_MATRIX.md` - cross-system ownership and AUP-sensitive edges.
3. `DISPATCH_PIPELINE.md` - runtime dispatch, tick ownership, and update boundaries.
4. `AUP_PRECISION_STANDARDS.md` and `KINEMATICS_AUP_INTEGRATION.md` - floating-origin and movement correctness.
5. `ZERO_GC_FABRICATION.md` and `ZERO_GC_UI_PIPELINE.md` - allocation discipline for fabrication and UI.
6. `SAVE_V8_BINARY_SPEC.md` and `SAVE_PAGING_PROTOCOL.md` - persistence architecture.
7. Domain docs such as `FLOW_FIELD_MATH.md`, `AUDIO_DSP_PIPELINE.md`, `HABITAT_LOGISTICS_GRAPH.md`, `SUBMARINE_OS_MANUAL.md`, and `URP_SCREENSHOT_PIPELINE.md` as needed by the task.

## Hard Current Rules

- Default to visual/audio/haptic/UI/proxy fake before physical simulation.
- Physical simulation is allowed only for player-critical collision/control, save-affecting state, combat/damage truth, or gameplay-critical hazards.
- Any single runtime system over `0.1ms` is suspicious until profiler proof and load-shed behavior exist.
- No `Schedule().Complete()` inside the same hot-path method.
- No direct ad hoc `Rigidbody.AddForce` ownership outside the designated physics apply path.
- No Bloom or FSR2/DLSS-class temporal upscaler on MX350/MINIMAL.
- No runtime readiness claim without fresh Unity/profiler/GC/player-build evidence.

