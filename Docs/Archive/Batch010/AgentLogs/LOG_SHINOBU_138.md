# LOG_SHINOBU_138

## 2026-05-19 Chemical Influence Grid Pass

What was wrong: scent authority was still shaped like a 2D/local-buffer compatibility lane. Predator scent could fall back into per-waypoint scans, and there was no Vault-owned 3D diffusion field for blood, pheromones, and toxic runoff.

What was done: `ChemicalInfluenceGrid` now owns a Vault-backed 48x16x48 3D chemical grid with front/back `ChemicalCellDTO` buffers, deterministic Burst injection, Jacobi diffusion, AUP sliding, SDF occlusion, published `float4` samples, editor tuning, CSV profile ingestion, gizmo slices, and a 300-frame telemetry ring. `PredatorCognitionDomain` now samples the published grid first and uses breadcrumbs only as compatibility fallback.

Cinematic Cheats used: no Navier-Stokes, no scent particles, no PhysX trigger field. Abyssal current is an analytic deterministic curl/triangle drift inside the solver. Visual debugging consumes published scalars instead of spawning particle clouds.

Exact Microseconds saved: unprofiled in this pass because build/import was gated by CPU telemetry at 100%. Static estimates recorded in `Docs/Tasks/Status_SHINOBU_138.md`: trigger pair avoidance ~18 us/pair, predator dense-source scan avoidance ~35-220 us/pack frame, low-tier solver pass reduction up to 83% against six-pass desktop mode.

Verification performed: full XML prompt re-read via CLI with `<AGENT_PROMPT id="SHINOBU_138"[^>]*>`; static forbidden-pattern scan on owned runtime/editor/data files; DTO/Burst attribute scan; `git diff --check` on owned files and docs. No `dotnet build` was launched because project policy forbids build under >50% CPU and current counter reported 100%.

<SELF_AUDIT agent_id="SHINOBU_138" domain="CHEMICAL_INFLUENCE_GRID_TRACKER">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">No scent-owned `OnTriggerStay` route remains in the new authority path; implementation uses grid emitters and Vault buffers.</TASK>
    <TASK id="02" status="PASS">Predator cognition samples the published grid O(1) before legacy capped breadcrumb fallback.</TASK>
    <TASK id="03" status="PASS">Hot chemical DTOs expose public fields; jobs mutate raw pointers resolved from Vault handles.</TASK>
    <TASK id="04" status="PASS">`ChemicalCellDTO` is explicit 16B and validated with `UnsafeUtility.SizeOf` and field offsets.</TASK>
    <TASK id="05" status="PASS">`GenerateMockScentSourcesJob` creates deterministic mock blood/pheromone emitters from sector hash and frame.</TASK>
    <TASK id="06" status="PASS">`ChemicalInjectionJob` maps emitter AUP/radius to cells and uses CAS atomic float adds plus flag OR.</TASK>
    <TASK id="07" status="PASS">`ChemicalDiffusionSolverJob` uses front/back Jacobi double-buffering and never mutates in-place.</TASK>
    <TASK id="08" status="PASS">Abyssal drift is a deterministic analytic Dear Lie term inside the solver; no physical fluid sim.</TASK>
    <TASK id="09" status="PASS">`SampleChemicalGridJob` exists for AUP request/result arrays and predator route consumes published grid.</TASK>
    <TASK id="10" status="PASS">`ShiftChemicalGridJob` uses async slab `UnsafeUtility.MemMove` after clearing the destination buffer.</TASK>
    <TASK id="11" status="PASS">Iterations use `(int)math.lerp(1f, 6f, GlobalQualityWeight)` with continuous frame cadence scaling.</TASK>
    <TASK id="12" status="PASS">Solver reads `BufferID.VoxelSdfTexture3D` and marks solid/blocked cells as occluded.</TASK>
    <TASK id="13" status="PASS">AUP mapping subtracts grid/root `double3` before casting local deltas to `float3`.</TASK>
    <TASK id="14" status="PASS">Burst jobs use `FloatMode.Deterministic`; DTOs are blittable and fixed-size for memcpy snapshots.</TASK>
    <TASK id="15" status="PASS">Vault buffers request `NativeArrayOptions.UninitializedMemory`; cold zeroing runs in `ColdZeroVaultBuffersJob`.</TASK>
    <TASK id="16" status="PASS">`ChemicalTelemetryEntry[300]` records high-level state and dumps to `Docs/AgentLogs/Dump_CHEMISTRY_SURGEON.bin` on NaN.</TASK>
    <TASK id="17" status="PASS">`AbyssalScentTunerWindow` UI Toolkit editor facade reads telemetry and writes tuning DTO fields.</TASK>
    <TASK id="18" status="PASS">CSV parser uses `ReadOnlySpan<byte>`, FNV-1a names, manual float parsing, and a Vault-backed fixed table. NativeHashMap was intentionally replaced because persistent ownership must stay in Vault buffers.</TASK>
    <TASK id="19" status="PASS">`OnDrawGizmos` draws a bounded 2D scent slice from the published grid.</TASK>
    <TASK id="20" status="PASS">This audit, route card, status file, rationale, and log were written to disk; compile is explicitly gated, not claimed.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <STRUCT name="ChemicalCellDTO" size_bytes="16" alignment_contract="16B">
      <FIELD name="BloodConcentration" offset="0" size="4" type="float" />
      <FIELD name="PheromoneConcentration" offset="4" size="4" type="float" />
      <FIELD name="ToxinConcentration" offset="8" size="4" type="float" />
      <FIELD name="Flags" offset="12" size="4" type="uint" />
      <MATH>4 + 4 + 4 + 4 = 16 bytes. Four cells = 64 bytes per L1 cache line.</MATH>
    </STRUCT>
    <STRUCT name="ChemicalAtomicCounterDTO" size_bytes="64" false_sharing_contract="one cache line">
      <FIELD name="MaxBloodBits" offset="0" size="4" />
      <FIELD name="ActiveEmitterCount" offset="4" size="4" />
      <FIELD name="MockEmitterCount" offset="8" size="4" />
      <FIELD name="JacobiIterations" offset="12" size="4" />
      <FIELD name="NaNFlag" offset="16" size="4" />
      <FIELD name="StateHash" offset="20" size="4" />
      <FIELD name="ActiveCellCount" offset="24" size="4" />
      <FIELD name="_pad0" offset="28" size="4" />
      <FIELD name="_pad1" offset="32" size="8" />
      <FIELD name="_pad2" offset="40" size="8" />
      <FIELD name="_pad3" offset="48" size="8" />
      <FIELD name="_pad4" offset="56" size="8" />
      <MATH>28 bytes counters + 36 bytes manual padding = 64 bytes exactly.</MATH>
    </STRUCT>
    <STRUCT name="ChemicalTelemetryEntry" size_bytes="64" />
    <STRUCT name="ChemicalEmitterDTO" size_bytes="64" />
    <STRUCT name="ChemicalEmitterProfileDTO" size_bytes="64" />
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE_EXPLANATION>
    Low quality collapses work continuously: frame stride lerps toward 12, Jacobi iterations collapse to 1, injection radius scales down, sampling blends to nearest-neighbor below 0.3, and high-tap abyssal drift is gated by `math.step(0.7f, q)`. Mid/high quality enables smoother trilinear sampling and more solver passes. Ultra uses the same authority path with richer published overlay data and stronger drift, not a separate code branch.
  </SCALABILITY_CURVE_EXPLANATION>
  <H_PHI_VAULT_STATUS>
    Runtime declares zero private persistent `NativeArray`, `NativeList`, or `NativeHashMap` fields. Persistent storage is requested through Vault handles `71150` through `71168` listed in the route card. The only `NativeArray<T>` method signature in the runtime is `ResolveArray<T>`, a transient view over Vault-owned memory.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    All raw pointer fields in chemical jobs use `[NoAlias]` and `NativeDisableUnsafePtrRestriction` where applicable. Job graph: optional shift -> optional copy -> prepare -> commit -> mock -> injection -> diffusion x iterations -> publish -> telemetry. The final handle is stored in `_scheduledHandle`, registered through `H8Memory.RegisterActiveJob(SystemID.AISensory, _scheduledHandle)`, and normally finalized non-blocking through `DispatcherJobSwap.TryFinalizeCompleted`.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No new asmdef or sibling runtime assembly reference was added. Runtime route uses existing `GlobalRegistry`, `IDataVault`, `H8Memory`, and public owner APIs. Work stayed in chemical world runtime/editor/data, one predator consumer bridge, and documentation.
  </COMPILE_GUARD>
  <THE_DEAR_LIE_CONFIRMATION>
    The avoided heavy model is object/particle/trigger scent simulation plus possible current particles. The implemented fake is scalar grid diffusion with analytic deterministic abyssal drift. Before: PhysX trigger/object scent plus predator scans can trend O(predators * scent_nodes) and broadphase overhead. After: O(cells * iterations + touched_emitter_cells), predator read O(1).
  </THE_DEAR_LIE_CONFIRMATION>
  <VERIFICATION_LIMITS>
    Build/import not executed. Reason: CPU counter returned 100%, and project policy forbids `dotnet build` or rebuild while CPU is above 50% or compiler processes are active. No `dotnet`/`csc` process was observed during the last check, but CPU alone blocks build.
  </VERIFICATION_LIMITS>
</SELF_AUDIT>

---

What was wrong: a polish review found remaining convenience seams that were not acceptable for deterministic chemical authority: runtime frame/time fallback, `Camera.main` focus fallback, runtime offset reflection, editor telemetry string rebuilds, and source-level Gameplay concrete symbols in `ChemicalInfluenceGrid`.

What was done: chemical scheduling now resolves a deterministic frame id from `HectonArenaAllocator.CurrentFrameSequence` with a local fallback, simulation seconds derive from frame id and tick delta, focus AUP no longer queries `Camera.main`, layout offset checks are editor-only, tuner telemetry uses numeric fields updated only when the telemetry frame advances, and player/submarine data routes through `GlobalRegistry` contract access without `Hecton8.Gameplay` using statements or cached `TryGetComponent` survival lookup.

Cinematic Cheats used: unchanged. Chemical movement remains scalar Jacobi diffusion plus analytic abyssal drift; no particles, trigger fields, Navier-Stokes, or camera-driven scent logic were added.

Exact Microseconds saved: unprofiled. Static risk removed: hidden camera lookup, hidden MonoBehaviour component lookup, editor telemetry string churn, and nondeterministic frame/time reads. Solver savings remain controlled by continuous quality weight: low tier one pass/sparse cadence, ultra tier six passes/richer drift.

Verification performed: static grep after the polish pass found no `using Hecton8.Gameplay`, `HectonSurvivalSystem`, `ISubmarineRuntimeContext`, cached player fields, or `TryGetComponent` in `ChemicalInfluenceGrid.cs`. Static forbidden-pattern scan on chemical runtime/editor now reports only `ResolveArray<T>` as a method-return false-positive and editor-only `Marshal.OffsetOf(typeof(T), fieldName)`. Brace count is balanced and all 11 Burst jobs still use deterministic compile attributes. `git diff --check` passed for the chemical runtime. `dotnet build` was still not launched because project policy forbids build under the current CPU gate.

<SELF_AUDIT_SUPPLEMENT agent_id="SHINOBU_138" pass="LOOP_6_POLISH">
  <COMPILE_GUARD status="PASS">Removed source-level `Hecton8.Gameplay` symbols from the chemical runtime. Bleeding and submarine signals are consumed through `GlobalRegistry` contract members only.</COMPILE_GUARD>
  <DETERMINISM_GUARD status="PASS">Removed `Time.frameCount`, `Time.time`, and `Camera.main` from chemical scheduling/focus fallback. Simulation seconds now derive from deterministic frame id and tick delta.</DETERMINISM_GUARD>
  <ZERO_GC_GUARD status="PASS">Editor tuner telemetry no longer rebuilds display strings on every update; runtime hot jobs remain raw-pointer/Vault-backed.</ZERO_GC_GUARD>
  <STRUCT_LAYOUT_GUARD status="PASS">Runtime layout validation uses `UnsafeUtility.SizeOf`; field-offset reflection is editor-only and not in player hot path.</STRUCT_LAYOUT_GUARD>
  <BUILD_LIMIT status="GATED">No build/import proof claimed. CPU policy still blocks `dotnet build`/rebuild.</BUILD_LIMIT>
</SELF_AUDIT_SUPPLEMENT>
