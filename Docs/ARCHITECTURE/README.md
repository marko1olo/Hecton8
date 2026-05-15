# HECTON-8 Architecture Index

Date: 2026-05-15
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

2026-05-14 DOC_AUDIT override: `../../Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md`.

The previously cited local compile-only artifact `../../CodexArtifacts/2026-05-11_DOCS_CONTINUATION_CORE_BUILD_R1.summary.txt` is absent from the current filesystem. Treat the May 11 compile-success claim as dated report text. Current May 14 R43 evidence is external root `Hecton8*.csproj` no-restore CLI compile at `0 Warning(s)` / `0 Error(s)` after restore assets and referenced `Temp\bin\Debug` DLLs exist, not Unity runtime proof.

Not proven by this folder: Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality.

## Read Order

1. `CINEMATIC_CHEATS_LEDGER.md` - visual-realistic-fake doctrine and physical-simulation rejection gate.
2. `SYSTEM_INTERCONNECT_MATRIX.md` - cross-system ownership and AUP-sensitive edges.
3. `HECTON_PHI_STATIC_METRIC.md` - static H-Phi definition, formulas, graph debt gate, and evidence boundary.
4. `DISPATCH_PIPELINE.md` - runtime dispatch, tick ownership, and update boundaries.
5. `AUP_PRECISION_STANDARDS.md` and `KINEMATICS_AUP_INTEGRATION.md` - floating-origin and movement correctness.
6. `ZERO_GC_FABRICATION.md` and `ZERO_GC_UI_PIPELINE.md` - allocation discipline for fabrication and UI.
7. `SAVE_V8_BINARY_SPEC.md` and `SAVE_PAGING_PROTOCOL.md` - persistence architecture.
8. Domain docs such as `FLOW_FIELD_MATH.md`, `AUDIO_DSP_PIPELINE.md`, `HABITAT_LOGISTICS_GRAPH.md`, `SUBMARINE_OS_MANUAL.md`, and `URP_SCREENSHOT_PIPELINE.md` as needed by the task.

## Complete Contract Inventory

- [AI_POTENTIAL_FIELD_NAVIGATION.md](AI_POTENTIAL_FIELD_NAVIGATION.md)
- [ARENA_ALLOCATOR_2_0.md](ARENA_ALLOCATOR_2_0.md)
- [AUDIO_DSP_PIPELINE.md](AUDIO_DSP_PIPELINE.md)
- [AUP_PRECISION_STANDARDS.md](AUP_PRECISION_STANDARDS.md)
- [BOOT_SEQUENCE_TOPOLOGY.md](BOOT_SEQUENCE_TOPOLOGY.md)
- [CINEMATIC_CHEATS_LEDGER.md](CINEMATIC_CHEATS_LEDGER.md)
- [CORE_REPLAY_DETERMINISM.md](CORE_REPLAY_DETERMINISM.md)
- [DATA_MONOLITH_H8BIN_SPEC.md](DATA_MONOLITH_H8BIN_SPEC.md)
- [DISPATCH_PIPELINE.md](DISPATCH_PIPELINE.md)
- [DRONE_FLEET_PROTOCOL.md](DRONE_FLEET_PROTOCOL.md)
- [ECS_DOTS_ADOPTION_PLAN.md](ECS_DOTS_ADOPTION_PLAN.md)
- [EQUIPMENT_SOA_LAYOUT.md](EQUIPMENT_SOA_LAYOUT.md)
- [FLOW_FIELD_MATH.md](FLOW_FIELD_MATH.md)
- [GLOBAL_REGISTRY_SERVICE_LOCATOR.md](GLOBAL_REGISTRY_SERVICE_LOCATOR.md)
- [GLOBAL_SIGNAL_CORRIDOR.md](GLOBAL_SIGNAL_CORRIDOR.md)
- [HABITAT_LOGISTICS_GRAPH.md](HABITAT_LOGISTICS_GRAPH.md)
- [HEADLESS_ECOSYSTEM_SIMULATION.md](HEADLESS_ECOSYSTEM_SIMULATION.md)
- [KINEMATICS_AUP_INTEGRATION.md](KINEMATICS_AUP_INTEGRATION.md)
- [KINETIC_ENTANGLEMENT.md](KINETIC_ENTANGLEMENT.md)
- [MIGRATORY_FLORA_SYSTEM.md](MIGRATORY_FLORA_SYSTEM.md)
- [ORGANIC_ENTROPY_MATH.md](ORGANIC_ENTROPY_MATH.md)
- [PROJECT_CONTENT_LEDGER.md](PROJECT_CONTENT_LEDGER.md)
- [QUEST_DAG_PROTOCOL.md](QUEST_DAG_PROTOCOL.md)
- [REACTIVE_ECONOMY_SYSTEM.md](REACTIVE_ECONOMY_SYSTEM.md)
- [SAVE_PAGING_PROTOCOL.md](SAVE_PAGING_PROTOCOL.md)
- [SAVE_V8_BINARY_SPEC.md](SAVE_V8_BINARY_SPEC.md)
- [SCALABILITY_MATRIX.md](SCALABILITY_MATRIX.md)
- [SCANNER_DATA_MINING.md](SCANNER_DATA_MINING.md)
- [SEISMIC_GEOLOGY_SYSTEM.md](SEISMIC_GEOLOGY_SYSTEM.md)
- [SUBMARINE_OS_MANUAL.md](SUBMARINE_OS_MANUAL.md)
- [SYSTEM_INTERCONNECT_MATRIX.md](SYSTEM_INTERCONNECT_MATRIX.md)
- [THIRD_PARTY_POISON.md](THIRD_PARTY_POISON.md)
- [TRAUMA_GLITCH_SYSTEM.md](TRAUMA_GLITCH_SYSTEM.md)
- [URP_SCREENSHOT_PIPELINE.md](URP_SCREENSHOT_PIPELINE.md)
- [ZERO_GC_FABRICATION.md](ZERO_GC_FABRICATION.md)
- [ZERO_GC_UI_PIPELINE.md](ZERO_GC_UI_PIPELINE.md)

## Hard Current Rules

- Default to visual/audio/haptic/UI/proxy fake before physical simulation.
- Physical simulation is allowed only for player-critical collision/control, save-affecting state, combat/damage truth, or gameplay-critical hazards.
- Any single runtime system over `0.1ms` is suspicious until profiler proof and load-shed behavior exist.
- No `Schedule().Complete()` inside the same hot-path method.
- No direct ad hoc `Rigidbody.AddForce` ownership outside the designated physics apply path.
- No Bloom or FSR2/DLSS-class temporal upscaler on MX350/MINIMAL.
- No runtime readiness claim without fresh Unity/profiler/GC/player-build evidence.
- Static H-Phi is an architecture hygiene signal only; do not report it as compile, profiler, GC, player-build, or visual-quality proof.
