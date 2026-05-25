# SHINOBU_302 Utility AI Cognition Route

Owner: SHINOBU_302
Domain: UTILITY_AI_COGNITION_CORE
Runtime route: SignalBus snapshots -> owner-staged cognition signal DTOs -> UtilityAICognitionVault -> Burst jobs -> ActionHash outputs.

## Hot Path

- `UtilityAICognitionJobs.cs` owns branchless polynomial evaluation for hunger, fear, and aggression. Hot job source has no `?`, `&&`, `||`, `switch`, or `case` tokens after the branchless guard polish.
- `CognitionStateDTO` is fixed at 32 bytes: hunger, fear, aggression, action hash, target hash, cooldown, padding.
- `EvaluateUtilityCognitionJob` emits `CognitionActionOutputDTO.ActionHash`; no managed action object or FSM transition is created.
- `IntegrateCognitionSensoryInputJob` reads staged DTO copies of `MovementAcousticSignal` and `CombatDamageSignal`; the Core owner remains responsible for copying first-party `SignalBus<T>` snapshots into the DTO lane before scheduling.
- Target choice is the Dear Lie route.
- At most four local bucket candidates.
- Selection uses deterministic score comparison, `math.select`, bitwise masks, and AUP double subtract.
- No scene searches.
- Sensory distance clamps sanitize corrupt double distances to `float.MaxValue` before proximity math, so invalid AUP data cannot multiply a NaN by a later zero validity mask.
- Runtime tuning writes use direct Vault memory mutation through `UnsafeUtility.AsRef<CognitionUtilityTuningDTO>()`; the editor sliders do not create managed action state.

## Vault Buffers

Buffers are owned by `SystemID.AICognition`.

- `71960` `CognitionStateDTO[4096]`
- `71961` `CognitionAupDTO[4096]`
- `71962` `CognitionTargetCandidateDTO[4096]`
- `71963` `int[4096]` target next links
- `71964` `int[1024]` bucket heads
- `71965` `CognitionUtilityTuningDTO[1]`
- `71966` `CognitionActionOutputDTO[4096]`
- `71967` `CognitionTelemetryEntry[300]`
- `71968` `int[1]` telemetry cursor
- `71969` `CognitionProfileDTO[128]`
- `71970` `byte[16384]` CSV scratch

## Rollback And Quality

`GlobalQualityWeight` is continuous. `ResolveQuality()` sanitizes with `math.select`, gates tiny values with `math.step`, eases via `smoothstep`, then drives cadence, taps, and candidate budget:

- weak device: 1 target candidate, lower signal tap count, cognitive interval near 1.5 seconds.
- middle device: 2 candidates and moderate tap count.
- high device: 3 candidates and faster cadence.
- ultra device: 4 candidates, max tap count, cognitive interval near 0.1 seconds.

Quality does not change DTO layout, action hash identity, save identity, or authority route.

## Verification Boundaries

- Runtime csc proof: narrow Unity csc response plus three same-asmdef SHINOBU_312 anxiety inputs returned exit 0 after branchless guard polish.
- Artifact: `Library/Bee/artifacts/1900b0aEDbg.dag/SHINOBU_302_Hecton8.AI.Cognition.Test.dll`, 90112 bytes.
- Editor csc proof: pending CPU/compiler gate. Generated `Hecton8.AI.Cognition.Editor.rsp` includes all six editor inputs and no `Hecton8.Core.ref.dll`.
- Data Monolith readiness: `static_data.h8bin` exists in current X_012 scan.
- Route-specific boot proof remains pending.
- SHINOBU_302 can run from Vault/mock/csv lanes.
- Static-data boot validation remains project-level blocker outside this AI patch.

## Black Box

- `CognitionTelemetryEntry[300]` records average fear, average hunger, average aggression, hunting count, max utility, fault flags, Burst microseconds.
- Fault dump path: `Docs/AgentLogs/Dump_SHINOBU_302.bin`.
- Dump format: raw `ReadOnlySpan<byte>` header plus telemetry ring.

## Legacy Quarantine

`FaunaBrain`, `FaunaStateMachine`, and `MesofaunaBehavioralStateMachine` remain compatibility shells. They are scanner candidates, not deletion targets. New cognition work must schedule `UtilityAICognitionVault` jobs.

## Editor Facade

- `CognitionUtilityTunerWindow` is editor-only.
- It exposes continuous quality, polynomial coefficients, action biases, sensory gains, and target radii.
- It draws a stacked action-distribution chart from `CognitionActionOutputDTO` rows and a SceneView debug line from each creature AUP to the AUP-resolved `TargetEntityHash`.
- One-shot editor buttons use a local deterministic editor frame counter and may complete jobs for inspection; player runtime scheduling returns `JobHandle` to the dispatcher chain.

`OOP_FSM_Scanner` writes mandated shared report path and stable per-agent copy.

Per-agent copy: `Docs/Reports/SHINOBU_302_AI_OPTIMIZATION_REPORT.json`. The shared `AI_OPTIMIZATION_REPORT.json` is contested by active agents.

Scanner strips comments/strings before method-body scan. Roslyn is intentionally not added.

Assembly route: runtime `Hecton8.AI.Cognition` depends only on Core Contracts/Memory plus Burst/Collections/Jobs/Mathematics.

Editor tooling resolves latest Vault through Core.Memory diagnostics and does not reference direct `Hecton8.Core`.
