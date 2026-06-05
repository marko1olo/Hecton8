# Radiation Mutation Link SHINOBU_324

Status: STATIC_ROUTE_DOC / RUNTIME_PROOF_PENDING

Evidence class: STATIC_DOC

Owner: Physiology / GameplayPlayer dispatcher route.

`ShinobuRadiationMutationRuntime` reads Agent 274 `RadiationStateDTO` through `Hecton8.Core.Contracts.Physiology`.

Source buffer: `BufferID.Shinobu274RadiationStates`, immutable `TryReadHandle` snapshot. SHINOBU_324 buffers: `75320..75325`.

No renderer material mutation, bone prefab, or per-arm particle object is part of the route.

Gameplay truth is scalar:
- `MutationSeverity01`
- `MaxStaminaPenalty`
- `HealingSuppression01`
- `MutationFlags`

- Presentation is a Dear Lie.
- Scalar publish path: `HectonShaderGlobalDataVaultBridge.PublishRadiationMutation`.
- Shader slot: `22`.
- Mirror global: `_HectonHandRadiationMutation01`.
- `GlobalShaderDispatcher` reads slot 22 in active VisualSync.
- Command buffer publishes `_HectonRadiationMutationParams` and `_HectonHandRadiationMutation01`.
- `Hecton8_UberNoir.hlsl` consumes those globals for quality-gated vertex displacement plus procedural blister tint/subsurface response.
- Low quality uses triangle/hash scars; higher quality admits `ValueNoise3` through a smooth `GlobalQualityWeight` gate.
- Toxic blood is a bounded `DebrisSpawnSignal` using AUP plus `DebrisSpawnSignal.FlagComputeShard`; downstream GPU debris owns rendering.

Phase discipline:
- `SlowTick`: evaluate radiation dose to mutation scalar and write telemetry.
- `PreSimulation`: bridge mutation penalty to metabolism toxicity/fatigue flags before KCC consumers read physiology state.
- Guard: `ShinobuMetabolismVaultContract.MetabolismStateMutationGuardMask`.
- No private guard bit.
- One shared metabolism fact has one guard route.
- `VisualSync`: publish shader globals and toxic blood signal; `LateFrame` is fallback only when dispatcher visual sync is unavailable.

- Runtime job policy:
  - Burst batch jobs remain raw-pointer proof kernels with `[NativeDisableUnsafePtrRestriction, NoAlias]` lanes and explicit counts.
  - Current one-row player path executes shared deterministic kernel directly.
  - Reason: avoid tiny same-frame `.Run()` wrapper.
- No hidden `.Complete()` or `.Schedule()` readback exists in the runtime route.
- `SlowTick()` now fails closed through `HasRuntimeVaultState()` and does not call the cold `EnsureVaultState()` buffer acquisition path.
- `RunEvaluation()` guards resolved Vault lengths before telemetry modulo arithmetic or locks.

Cold CSV tuning uses `FileStream.Read(Span<byte>)` directly into Vault scratch lane.

It does not allocate intermediate `byte[]`, split strings, or call `float.Parse`. Player builds load during Vault initialization; repeated polling is editor-only.

Black box: last 300 `RadiationMutationTelemetryEntry` rows are kept in a Vault ring and dumped to `Docs/AgentLogs/Dump_SHINOBU_324.bin` on non-finite or overbudget detection.

Scanner proof: `RadiationMutationOopScanner` is editor-only and parses C# through Roslyn `CSharpSyntaxTree`.

It reports `scannerUsesRoslynAst: true` and detects mutation-authority forbidden routes from syntax nodes.

Token fallback is used only for shader/HLSL bridge files.

- Compile evidence: guarded `Hecton8.Core.csproj` build was relaunched after CPU/compiler gate cleared (`35.97%`, no active `dotnet`/`csc`/`VBCSCompiler`).
- SHINOBU_324 contract visibility errors were removed by placing the shared radiation ABI in the compiled Core contracts source.
- Latest Core build fails with 53 external errors.
- Error domains: PlayerKinematics, VRSomatic, CombatDamage, KCC/generated-contract coverage.
- No error path points at SHINOBU_324 runtime/data/jobs/editor.
- Also clear: shader bridge, dispatcher slot-22 patch, UberNoir, `RadiationHazardGrid`, `HectonDataSovereigntyContract`.
