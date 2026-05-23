# SHINOBU_320 Status

Agent: SHINOBU_320
Role: METABOLISM_CORE_TEMP_INTEGRATOR
Domain: ECHELON 5 COMBAT & SURVIVAL PHYSIOLOGY / DIET & METABOLISM
Prompt task count: 20
Status: BLOCKED BY EXTERNAL DEPENDENCY

## Hygiene

- [x] Extracted own XML block from `Docs/Tasks/CURRENT_BATCH.md` with CLI regex, full block only. DOD: prompt isolation. Rejected: neighboring prompt influence. Estimate: 180 us.
- [x] Read `AGENTS.md`, domain map, and selected mandates before code. DOD: authority spine. Rejected: coding from chat memory. Estimate: 220 us.
- [x] Created `Status_SHINOBU_320.md` and `Rationale_SHINOBU_320.md`. DOD: file-backed memory. Rejected: chat-only state. Estimate: 80 us.

## Phase 0 - Architectural Archaeology

- [x] Task 01: MANDATORY_CODEBASE_GREP_SCAN | Justification: scanned Physiology, missing Player dir, KCC, and `HectonSurvivalSystem`; found existing `ShinobuMetabolismRuntime` owner plus legacy hunger/temp timers. DOD: source grep. Alternatives Rejected: duplicate manager. Estimate: 260 us.
- [x] Task 02: PARTIAL_CLASS_INTEGRATION_MANDATE | Justification: integrated into existing `ShinobuMetabolismRuntime`/jobs instead of creating `HectonMetabolismManager`; editor gizmo is isolated in `ShinobuMetabolismRuntime_DebugGizmo.cs` partial. DOD: owner-local edit. Alternatives Rejected: standalone runtime and full merge into gas/decompression runtime. Estimate: 110 us.
- [x] Task 03: SIGNALBUS_MATRIX_VERIFICATION | Justification: confirmed `SignalBus<T>` is hot lane; staged `CombatDamageSignal` rows for starvation/dehydration/hypothermia/toxicity and publish in LateFrame. DOD: typed signal route. Alternatives Rejected: direct health mutation. Estimate: 180 us.

## Phase 1 - Sanitation And Vault Staging

- [BLOCKED BY DEPENDENCY] Task 04: MONOBEHAVIOUR_SURVIVAL_INQUISITION | Justification: `HectonSurvivalSystem` has timer debt but also owns O2, pressure, radiation, save, UI, and environment read model. Deleting it here would break unrelated domains. DOD: blocker documented. Alternatives Rejected: destructive delete. Estimate: 0 us saved until integrator migration.
- [x] Task 05: HARDCODED_BIOME_TEMP_PURGE | Justification: metabolism owner samples cached `IThermodynamicsService.TryGetThermalGridReadbackAup`, using owner-provided `double3` grid origin and localizing AUP before float grid sampling. DOD: continuous thermal field. Alternatives Rejected: biome string temperature and SHINOBU-side runtime-origin reconstruction. Estimate: 35 us saved versus managed biome branch chain.
- [x] Task 06: EMERGENCY_MOCK_THERMAL_ENVIRONMENT | Justification: added deterministic `GenerateMockThermalEnvironmentJob` for synthetic cold field plus spherical hotspot gradient. DOD: Burst job. Alternatives Rejected: managed temp array fill. Estimate: 60 us per 32k cells versus managed loop.

## Phase 2 - Core Burst Math Solvers

- [x] Task 07: BURST_THERMODYNAMIC_INTEGRATION_KERNEL | Justification: replaced linear heat loss with Newton cooling `ambient + (core - ambient) * decay`, using deterministic rational exp approximation; Burst job now optionally reads `SuitIntegrityDTO.EquippedSuitHash` from the Vault and resolves suit thermal K by hash/alias before falling back to cached profile index. DOD: Burst deterministic solver + SoA suit identity route. Alternatives Rejected: hardcoded suit K and managed inventory polling. Estimate: 4-7 us per 5k rows.
- [x] Task 08: KINEMATIC_CALORIC_BURN_MATH | Justification: burn now equals basal drain plus `VelocitySq * ExertionMultiplier` plus continuous shiver cost; KCC velocity feeds row 0. DOD: velocity-derived metabolism. Alternatives Rejected: sprint timer. Estimate: 8 us saved versus managed movement polling.
- [x] Task 09: THE_DEAR_LIE_FREEZING_VFX | Justification: existing `PublishShaderGlobals` routes continuous frost scalar to shader float/constant buffer; kept and fed by corrected temperature math. DOD: VISUAL_SYNC scalar. Alternatives Rejected: post volume mutation. Estimate: 25 us saved.
- [x] Task 10: FATIGUE_PENALTY_ROUTING | Justification: `MetabolicStateDTO` now writes explicit `Fatigue01@24` while preserving `_pad0@24` as a stale-DLL mirror; KCC normalizes reserves and consumes the scalar/flag without speed mutation by metabolism. DOD: cross-domain read-only consumer. Alternatives Rejected: metabolism directly mutating KCC speed. Estimate: 3 us saved.
- [x] Task 11: CONTINUOUS_SCALABILITY_TICK_CADENCE | Justification: `ResolveCadenceSeconds(q)` now returns `math.lerp(1f, 0.1f, q)`, matching the required continuous quality curve. DOD: quality-controlled cadence. Alternatives Rejected: fixed 0.5s cadence. Estimate: low tier sheds up to 80% SlowTick jobs.
- [x] Task 12: AUP_PRECISION_GRID_SAMPLING | Justification: thermal owner now exposes grid origin as `double3` AUP; job subtracts entity AUP minus grid AUP in double before localized float3 indexing. DOD: AUP subtraction. Alternatives Rejected: absolute float conversion and local world origin bridge. Estimate: precision fault avoided.
- [x] Task 13: ROLLBACK_NETCODE_STATE_FENCE | Justification: jobs retain Burst `FloatMode.Deterministic`; exp uses rational polynomial instead of platform math exp. DOD: deterministic math. Alternatives Rejected: `math.exp` hot truth path. Estimate: drift risk reduced.
- [x] Task 14: ZERO_INIT_OVERHEAD_BYPASS | Justification: Vault acquisitions remain `NativeArrayOptions.UninitializedMemory`; init jobs overwrite active rows deterministically. DOD: no memclear in hot setup. Alternatives Rejected: ClearMemory/MemClear. Estimate: 40 us on 5k rows.
- [x] Task 15: TELEMETRY_METABOLISM_RECORDER | Justification: aggregate 300-entry ring kept; detail 300-entry ring added for depth, active burn, ambient, thermal K, heat delta, AUP, and suit hash; dump target `Dump_SHINOBU_320.bin` version 2 writes both lanes on NaN/over-0.2ms. DOD: black box fault path. Alternatives Rejected: NaN-only aggregate dump. Estimate: forensic path, not frame saving.

## Phase 3 - Presentation And Facades

- [x] Task 16: METABOLISM_TUNER_EDITOR_WINDOW | Justification: UI Toolkit tuner now includes detail telemetry readout plus stacked burn-vs-heat-loss bar and designer sliders; tuning writes lock the Vault row and mutate through `UnsafeUtility.AsRef`. DOD: editor facade present. Alternatives Rejected: duplicate editor window, runtime debug objects, and unmanaged row copies. Estimate: 0 runtime us.
- [x] Task 17: CSV_THERMAL_PROFILES_INGESTOR | Justification: added cold editor/development-only `suit_thermal_profiles.csv` `ReadOnlySpan<byte>` parser into Vault `MetabolicSuitThermalProfileDTO[32]`, with FNV-1a suit hashes and no `float.Parse`; production player builds compile CSV load bodies to `return false` and must use defaults or future DataMonolith. DOD: cold span parser. Alternatives Rejected: managed parse in tick and production text truth. Estimate: 0 runtime us.
- [x] Task 18: LIVE_THERMAL_DEBUG_GIZMO | Justification: editor `OnDrawGizmos` reads cached Vault state/AUP and draws zero-string temperature bars. DOD: debug view present without runtime GameObjects or managed labels. Alternatives Rejected: runtime debug GameObjects and formatted SceneView labels. Estimate: 0 runtime us.
- [x] Task 19: ARCHITECTURAL_METRIC_VALIDATOR | Justification: `OOP_Survival_Scanner` now uses Roslyn AST with token fallback and reports under `Docs/Reports`; current static report preserves 6 legacy composite survival timer surfaces. DOD: AST proof artifact. Alternatives Rejected: prose-only audit and line-only scanner. Estimate: editor only.
- [x] Task 20: SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION | Justification: static checks, JSON/XML parse, grep checks complete. Latest legal gated `dotnet build Assembly-CSharp.csproj --no-restore` ran after the Fatigue01 overlay at CPU=19.7/no compiler process and failed on one external `BaseAirlock.cs(24,24)` namespace error outside SHINOBU_320 ownership. DOD: compile gate obeyed and dependency break recorded. Alternatives Rejected: cross-domain patching. Estimate: hardware protected.

## Loop Ledger

- Loop 1: prompt isolated, domain confirmed, mandates selected, status/rationale initialized. Compile: not run.
- Loop 2: archaeology found `ShinobuMetabolismRuntime`, `MetabolicStateDTO`, KCC consumer, and legacy `HectonSurvivalSystem`. Compile: not run.
- Loop 3: core math patched for Newton cooling, quality cadence, deterministic mock thermal grid, and multi-slot combat staging. Compile: not run.
- Loop 4: cross-domain KCC read path patched to respect real 0..100 metabolism reserves and new fatigue flag. Compile: not run.
- Loop 5: static scanner/report added; JSON parse passed; build gate sampled CPU 100% with active dotnet/Unity. Compile: BLOCKED BY HARDWARE GATE.
- Loop 6: polish pass added owner-side thermal grid AUP route, suit thermal profiles, detail telemetry, full Vault handle lifecycle, and editor stacked chart. Compile: not run before CPU/dotnet gate.
- Loop 7: suit-equipment audit patched `MetabolicIntegrationJob` to read existing `SuitIntegrityDTO` rows from Vault under an optional read lock and to cache resolved profile indices; no direct concrete runtime dependency added. Compile: BLOCKED BY HARDWARE GATE.
- Loop 8: subagent race audit patched exact metabolism/KCC mutation guard and read-handle route; one gated compile attempted. Compile: FAILED BY EXTERNAL DEPENDENCY.
- Loop 9: re-extracted SHINOBU_320 XML, audited ledger/report/tooling, and upgraded `OOP_Survival_Scanner` from line scan to Roslyn AST with token fallback. Compile: not rerun; external dependency wall still active.
- Loop 10: patched editor/control bridges so tuning and suit-profile command writes lock Vault lanes and mutate rows via `UnsafeUtility.AsRef`. Compile: not rerun; external dependency wall still active.
- Loop 11: renamed private mutable Vault resolver from `TryResolveMetabolismVaultBuffer` to `TryOpenMetabolismVaultBuffer`, leaving read routes on `TryRead*`/`TryGet*`. Compile: not rerun; external dependency wall still active.
- Loop 12: checked Data Monolith presence; `static_data.h8bin` is absent, so SHINOBU_320 ledger/report now state no production DataMonolith claim for CSV profile hydration. Compile: not rerun; external dependency wall still active.
- Loop 13: gated biological/suit CSV loads behind `UNITY_EDITOR || DEVELOPMENT_BUILD` so player runtime cannot use project-root text as production static-data truth. Compile: not rerun; external dependency wall still active.
- Loop 14: recorded the production CSV gate in rationale/log/reports and ran another focused static pass. Compile: not rerun; external dependency wall still active.
- Loop 15: re-extracted SHINOBU_320 XML, re-audited Burst aliasing, and added explicit `Unity.Burst.CompilerServices` import for `[NoAlias]`. Compile: not rerun; external dependency wall still active.
- Loop 16: tightened CSV gate so production builds compile file-IO bodies out instead of only early-returning at runtime. Compile: not rerun; external dependency wall still active.
- Loop 17: appended a fresh post-loop self-audit block to `LOG_SHINOBU_320.md` reflecting CSV compile-out, explicit NoAlias namespace, current build wall, and exact DTO layout. Compile: not rerun; external dependency wall still active.
- Loop 18: audited assembly/using boundaries and gated `System.IO` import behind editor/development builds to keep production metabolism free of file-IO surface. Compile: not rerun; external dependency wall still active.
- Loop 19: audited Roslyn AST scanner assembly references and added explicit editor-only precompiled refs to `Hecton8.Physiology.Editor.asmdef`. Compile: not rerun; external dependency wall still active.
- Loop 20: re-extracted SHINOBU_320 XML with corrected tag regex and hardened `OOP_Survival_Scanner` report writes to non-destructive section upserts; removed dead legacy report builders. Compile: not rerun; external dependency wall still active.
- Loop 21: removed unused public `AcquireMutableStateRef(int)` because it exposed unguarded mutable state outside owner phase; command mutations remain lock-backed `UnsafeUtility.AsRef` routes. Compile: not rerun; external dependency wall still active.
- Loop 22: synchronized current SHINOBU/shared JSON proof artifacts with the scanner's non-destructive section model by adding `survivalOopScanner` and `shinobu320SurvivalOopScanner`. Compile: not rerun; report-only proof sync.
- Loop 23: audited owner thermal-grid flatten order and patched SHINOBU Burst sampler to match `AbyssalThermalManager.ToThermalGridIndex(x,y,z) = x + z*width + y*width*depth`. Compile: not rerun; external dependency wall still active.
- Loop 24: corrected production namespace gating so CSV load bodies still compile out of player builds, while mandatory black-box dump file IO remains compilable. Compile: not rerun; external dependency wall still active.
- Loop 25: hardened `TrySetSuitProfileHash` so an unknown equipment hash returns false without overwriting the cached suit profile index with default 0. Compile: not rerun; external dependency wall still active.
- Loop 26: added retained thermal-grid readback contract and held the thermodynamics owner read buffer until the SHINOBU Burst metabolism job finalizes. Compile: not rerun; external dependency wall still active.
- Loop 27: removed `AbyssalThermalManager.ThermalFlowSample` from `IThermodynamicsService` by adding standalone `ThermodynamicFlowSampleDTO` and explicit owner adapter. Compile: not rerun; external dependency wall still active.
- Loop 28: added editor-only layout validation for 64-byte `ThermodynamicFlowSampleDTO`, including public field offsets and private padding offsets. Compile: not rerun; external dependency wall still active.
- Loop 29: updated `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` SHINOBU_320 section with retained thermal readback, thermal-grid index order, and `ThermodynamicFlowSampleDTO` ABI. Compile: not rerun; external dependency wall still active.
- Loop 30: removed string/`Handles.Label` allocation from live thermal debug gizmo and removed editor gizmo `GlobalRegistry.DataVault` fallback; debug view now draws cached-Vault temperature bars through `Gizmos.DrawCube`. Compile: not rerun; hardware gate active.
- Loop 31: split the editor-only live thermal debug route into `ShinobuMetabolismRuntime_DebugGizmo.cs` under `UNITY_EDITOR`, made `ShinobuMetabolismRuntime` partial, and added the Unity `.meta` for the new file. Compile: not rerun; external dependency wall still active.
- Loop 32: gated rebuild ran after CPU=38/no compiler processes and exposed a SHINOBU-owned KCC compile fault from the new metabolism guard constant; patched KCC to use the same numeric guard locally because the generated Core project references the old Core.Contracts DLL. Rebuild rerun blocked by CPU=55 and active dotnet/VBCSCompiler.
- Loop 33: hardened diagnostics/debug read paths so `TryGetState`, `TryGetEntityAup`, `DumpBlackBoxForEditor`, and editor `OnDrawGizmos` return while a metabolism job is scheduled instead of reading Vault rows during owner mutation. Compile: not rerun; active compiler gate remains.
- Loop 34: corrected `GenerateMockThermalEnvironmentJob` index decoding to the same `x + z * width + y * width * depth` memory order used by owner thermal readback and SHINOBU sampling. Compile: not rerun; CPU gate active.
- Loop 35: synchronized SHINOBU/shared JSON proof artifacts with explicit `mockThermalIndexOrder` and recorded post-patch static checks. Compile: not rerun; proof-artifact sync only.
- Loop 36: appended a bottom-of-file LOG correction after Loop 35 landed above older self-audit content; no runtime code change. Compile: not rerun; audit hygiene only.
- Loop 37: refreshed compileProof after build gate sample CPU=89.8/no compiler processes; rebuild still illegal above the 50 percent CPU threshold.
- Loop 38: re-extracted XML and re-ran ownership/GC/property greps over SHINOBU hot-path files. Compile: not rerun; static proof only.
- Loop 39: removed KCC bridge dependence on newly added `ShinobuMetabolismVaultContract.FlagFatigue` by mirroring the numeric bit locally, matching the earlier local guard-bit compile fence. Compile: not rerun; CPU gate active.
- Loop 40: legal gated rebuild ran after CPU=16.6/no compiler processes; result is one external BaseAirlock namespace error, with no SHINOBU/KCC/thermal changed file in compiler errors.
- Loop 41: wrote standalone `Docs/Reports/SHINOBU_320_SELF_AUDIT.xml` and linked it from SHINOBU/shared optimization reports.
- Loop 42: synchronized `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` with the legal build probe: SHINOBU_320 now records `GUARDED_CORE_BUILD_ATTEMPT` and the exact external `BaseAirlock.cs(24,24)` namespace blocker.
- Loop 43: closed Task 10 scalar gap by adding `Fatigue01@24` to `MetabolicStateDTO`, writing it from the Burst metabolism job, and reading it in KCC through `_pad0@24` for stale generated Core.Contracts CLI compatibility. Compile: initial gate blocked by CPU/process policy, superseded by Loop 45 legal rebuild.
- Loop 44: folded fatigue scalar bits (`_pad0@24`) and metabolism flags into telemetry `StateHash` so black-box rows capture fatigue transitions. Compile: superseded by Loop 45 legal rebuild.
- Loop 45: legal post-overlay rebuild ran after gate sample CPU=19.7/no compiler processes; result remains one external BaseAirlock namespace error, with no SHINOBU/KCC/thermal changed file in compiler errors.

## Verification

- PASS: `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` parses with `ConvertFrom-Json`.
- PASS: grep confirms no `math.sin`, `StageToxicDamage`, fixed combat buffer count check, or hardcoded integration quality remains in SHINOBU_320 hot path except default tuning initialization.
- PASS: grep confirms SHINOBU metabolism runtime has no `Hecton8.World`, `AbsoluteUniversePosition`, `CurrentRuntimeOriginAup`, `TryResolveAupDoubleFromRuntimeOrigin`, or thermal `originWS` bridge.
- PASS: grep confirms no `new NativeArray`, `WaitForSeconds`, `foreach`, `LINQ`, or hidden `.Complete()` in SHINOBU_320 runtime/jobs files.
- PASS: public mutable state ref accessor removed entirely; SHINOBU `Get*`/`Resolve*`/`TryGet*` public accessors are read-only.
- PASS: grep confirms no hot DTO properties or `[StructLayout(Pack=...)]` in SHINOBU_320 data/jobs files.
- PASS: grep confirms new `73340..73342` BufferIDs are unique in `Assets/_Project/Scripts`.
- PASS: proof artifacts corrected to state lane `70238`; `70266` is AUP lane, not state lane.
- PASS: brace-balance scan passed for touched C# files.
- PASS: `git diff --check` passed for SHINOBU_320 touched code files; line-ending warnings only.
- PASS: suit profile route now reads existing `ShinobuSuitIntegrityConstants.StateBuffer` rows when available, locks the borrowed lane during the scheduled metabolism job, and releases the lock in LateFrame/teardown/hot-swap paths.
- PASS: static grep confirms no `new NativeArray`, `WaitForSeconds`, `foreach`, `LINQ`, hidden `.Complete()`, `Hecton8.World`, runtime-origin AUP bridge tokens, or mutable `GetStateRef` in SHINOBU_320 runtime/jobs files.
- PASS: disk-writing editor forensic command renamed from `TryDumpBlackBoxForEditor` to `DumpBlackBoxForEditor`; public SHINOBU `TryGet*` read routes remain read-only.
- PASS: KCC/metabolism shared state route now uses exact `MetabolismStateMutationGuardMask` instead of low-5-bit `ActiveBurstLockMask`; KCC reads via `TryReadHandle` and releases the guard on finalized/aborted batches.
- PASS: `OOP_Survival_Scanner` implementation now advertises and executes `ROSLYN_AST_WITH_TOKEN_FALLBACK`; reports updated from static mirror wording to Roslyn AST proof.
- PASS: post-AST-upgrade `ConvertFrom-Json` passed for SHINOBU and shared reports; focused `git diff --check` and brace balance passed for `OOP_Survival_Scanner.cs`.
- PASS: `TrySetTuning`, `TrySetSuitProfileIndex`, and `TrySetSuitProfileHash` now take explicit Vault locks and mutate target rows via `UnsafeUtility.AsRef`; focused `git diff --check` and brace balance passed.
- PASS: no `TryResolveMetabolismVaultBuffer` symbol remains; mutable Vault views are named `TryOpen*`, while public `TryGet*` accessors return copied read snapshots only.
- PASS: `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` and SHINOBU reports explicitly mark Data Monolith readiness as not claimed because `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is absent.
- PASS: `TryLoadBiologicalProfilesCsv` and `TryLoadSuitThermalProfilesCsv` now compile to `return false` outside editor/development builds; focused `diff --check` and brace balance passed.
- PASS: `Rationale_SHINOBU_320.md` and `LOG_SHINOBU_320.md` now explicitly record the production CSV gate and DataMonolith fallback boundary.
- PASS: SHINOBU/shared JSON reports parse after adding the explicit CSV production gate field.
- PASS: focused `git diff --check` from repo root passed for `ShinobuMetabolismRuntime.cs` and SHINOBU docs; Git reported line-ending normalization warning only.
- PASS: `ShinobuMetabolismRuntime.cs` brace balance remains `191/191` after deleting the helper gate method.
- PASS: all 6 SHINOBU metabolism jobs use deterministic Burst compile attributes; `ShinobuMetabolismJobs.cs` contains 35 `NoAlias` annotations and explicitly imports `Unity.Burst.CompilerServices`.
- PASS: `LOG_SHINOBU_320.md` contains current `<SELF_AUDIT iteration="post_loop_29_static_2026-05-22">` after retained readback, flow DTO, layout guard, and ledger sync patches.
- PASS: latest XML re-extraction found `PromptChars=23412` and all 20 `Task NN:` entries for `SHINOBU_320`.
- PASS: `Hecton8.Physiology.asmdef` references no World/Thermodynamics/KCC/Combat runtime assembly; SHINOBU metabolism files contain no direct sibling-domain namespace imports.
- PASS: production CSV load bodies compile out behind `UNITY_EDITOR || DEVELOPMENT_BUILD`; `System.IO` remains imported because the mandatory black-box dump path must compile in player builds.
- PASS: `TrySetSuitProfileHash` now mutates Vault suit-profile index only after a hash/profile match; failed identity lookups preserve the current row.
- PASS: `OOP_Survival_Scanner` Roslyn AST dependency is now declared in `Hecton8.Physiology.Editor.asmdef`; runtime asmdefs remain untouched.
- PASS: `OOP_Survival_Scanner` now uses `UpsertReportSection` for sidecar/shared JSON and no longer contains dead `BuildReport` or `BuildSharedSectionLegacy` paths.
- PASS: `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_320.json` contains nested `survivalOopScanner`; shared report contains `shinobu320SurvivalOopScanner` without deleting `shinobu320MetabolismScanner`.
- PASS: SHINOBU thermal sampler now matches the thermodynamics owner grid memory order; AUP-localized `float3` cell coordinates index `x + z*width + y*width*depth`, matching `AbyssalThermalManager.ToThermalGridIndex`.
- PASS: `IThermodynamicsService.TryAcquireThermalGridReadbackAup`/`ReleaseThermalGridReadback` now protects SHINOBU's scheduled thermal read pointer; `AbyssalThermalManager` defers read/write buffer swap and disposal while retain count is nonzero.
- PASS: `IThermodynamicsService.SampleThermalFlow` now exposes contract-only `ThermodynamicFlowSampleDTO`; legacy direct callers may still use `AbyssalThermalManager.ThermalFlowSample`, but Core no longer names that nested World runtime type in the service contract.
- PASS: `ShinobuMetabolismLayoutValidator` now checks `ThermodynamicFlowSampleDTO` size 64 and offsets 0/12/16/20/32/36/40/44/45/46/48/56, including private padding via editor-only reflection.
- PASS: `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` SHINOBU_320 section now records retained `TryAcquireThermalGridReadbackAup`, index order, and thermodynamics flow DTO ABI.
- PASS: Task 18 debug view no longer uses `Handles.Label`, string concatenation, `ToString`, `UnityEditor` import, or editor gizmo registry fallback; it reads cached Vault handles and draws color-coded temperature bars with `Gizmos.DrawCube`.
- PASS: Task 18 editor debug code is now isolated in `ShinobuMetabolismRuntime_DebugGizmo.cs` with a matching `.meta`; the runtime source no longer carries the SceneView-only `OnDrawGizmos` method body.
- PASS: `ShinobuMetabolismRuntime_DebugGizmo.cs.meta` GUID `8c1b6cb1542d4ef2ab611e09b046f320` appears only once in tracked/untracked `.meta` search.
- PASS: post-Loop-31 JSON parse passed for SHINOBU/shared optimization reports.
- PASS: post-Loop-31 focused `git diff --check` passed for runtime/debug/meta/status/rationale/log/report files; Git reported line-ending normalization warnings only.
- PASS: post-Loop-31 brace count is runtime `189/189` and debug gizmo `4/4`.
- PASS: post-Loop-31 grep confirms `OnDrawGizmos` exists only in the editor partial file; runtime references are limited to tooltip text and cold `RebindColdServices` Vault bootstrap.
- PASS: `rg` confirms `AcquireMutableStateRef` no longer exists in `ShinobuMetabolismRuntime.cs`; mutation now stays behind owner jobs or explicit command locks.
- PASS: `ShinobuMetabolismRuntime.cs` raw brace count is `191/191` after retained thermal readback fencing.
- PASS: latest focused brace scan is contracts `340/340`, metabolism jobs `79/79`, metabolism runtime `191/191`, thermodynamics `401/401`, layout validator `7/7`.
- PASS: latest focused `git diff --check` passed for touched SHINOBU/Core/Thermal code and JSON report files; Git reported line-ending normalization warnings only.
- PASS: focused `git diff --check` passed for `OOP_Survival_Scanner.cs` and `Hecton8.Physiology.Editor.asmdef`; Git reported line-ending normalization warning only.
- PASS: `rg` found no `foreach`, hidden `.Complete()`, `new NativeArray`, `LINQ`, `BuildReport`, or `BuildSharedSectionLegacy` in the SHINOBU scanner/runtime/jobs check set.
- NOTE: Attempted standalone PowerShell Roslyn load for syntax-only parsing failed because the Unity Roslyn DLL dependency graph does not load cleanly in PowerShell; this is not a Unity compile result. The scanner remains editor-only and the full build remains blocked by external files recorded below.
- BUILD RERUN RESULT: gated `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` ran at CPU=38 with no active compiler processes and failed with 76 errors. One SHINOBU-owned KCC guard-symbol error was patched; remaining errors are external Gameplay/Combat/Vehicle/Tether/Construction compile-wall debt.
- BUILD RERUN DEFERRED AFTER PATCH: latest gate sample is CPU=29 but active `dotnet` and `VBCSCompiler` remain present, so a post-patch rebuild is not legal under AGENTS command discipline.
- PASS: latest XML re-extraction with attribute-tolerant tag regex found `TaskCount=20` and all `Task 01` through `Task 20` entries for `SHINOBU_320`.
- PASS: latest focused `git diff --check` passed for Core contract, Core registry, KCC guard, SHINOBU metabolism/runtime/debug/meta/layout, thermodynamics, reports, ledger, status/rationale/log; Git reported line-ending normalization warnings only.
- PASS: latest focused brace scan is contracts `3/3`, registry `340/340`, KCC `340/340`, metabolism jobs `79/79`, metabolism runtime `189/189`, debug gizmo `4/4`, layout validator `7/7`, thermodynamics `401/401`.
- PASS: latest JSON parse passed for SHINOBU/shared reports after compileProof update.
- PASS: diagnostics/read gizmo paths no longer read state/AUP/telemetry while `_jobScheduled` is true; they return false/skip instead of forcing completion.
- BUILD RERUN DEFERRED AFTER LOOP 33: latest gate sample is CPU=94 with no compiler processes, so rebuild remains forbidden by the 50 percent CPU threshold.
- PASS: mock thermal grid generation now decodes linear indices with y-major/x-z ordering matching `ThermalIndex(x,y,z) = x + z * width + y * width * depth`.
- PASS: SHINOBU/shared optimization reports now include `mockThermalIndexOrder`; both JSON files parse after the proof-artifact update.
- PASS: post-Loop-34 focused `git diff --check` passed for SHINOBU jobs/runtime/debug/meta/status/rationale/log and SHINOBU/shared reports; Git reported line-ending normalization warnings only.
- PASS: post-Loop-34 brace scan is jobs `79/79`, runtime `189/189`, debug gizmo `4/4`.
- BUILD RERUN DEFERRED AFTER LOOP 37: latest gate sample is CPU=89.8 with no compiler processes, so rebuild remains forbidden by the 50 percent CPU threshold.
- PASS: latest XML re-extraction still reports `PromptChars=23412`, `TaskCount=20`, and all Task 01..20 entries for `SHINOBU_320`.
- PASS: latest hot-path ownership grep found no private persistent `NativeArray`/`NativeList`/`NativeHashMap`, `Allocator.Persistent`, hot DTO auto-properties, or `Pack=...` in SHINOBU metabolism runtime/jobs/data/contracts.
- PASS: KCC metabolism bridge now references local `MetabolismFatigueFlag` and local `MetabolismStateMutationGuardMask`; the source Core.Contracts constants remain authoritative for Unity/asmdef builds.
- PASS: SHINOBU/shared optimization reports now include `kccCompileFence` documenting the local KCC numeric mirrors for stale generated Core.Contracts CLI builds.
- BUILD RERUN RESULT AFTER LOOP 40: `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` failed with 1 external `Hecton8.Core.csproj` error at `Assets/_Project/Scripts/Gameplay/BaseAirlock.cs(24,24)`: missing namespace `Hecton8.Gameplay.AirlockPressurization`. No SHINOBU_320/KCC bridge/thermal readback files appeared in the compiler error.
- PASS: standalone `Docs/Reports/SHINOBU_320_SELF_AUDIT.xml` records task reconciliation, layout offsets, quality curve, Vault lanes, dependency graph, compile guard, dear-lie route, and current external build wall.
- PASS: SHINOBU_320 ledger section now distinguishes static/source proof from the legal build attempt and no longer claims compile proof is only CPU/process gated.
- PASS: `MetabolicStateDTO` remains 32 bytes; `Fatigue01` and `_pad0` both map to offset 24, so Task 10 gains a scalar without changing rollback stride.
- PASS: `MetabolismTelemetryJob.StateHash` now includes `Flags` and `_pad0@24` fatigue bits in addition to reserves, core temperature, toxicity, and entity hash.
- BUILD RERUN RESULT AFTER LOOP 45: `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` failed with 1 external `Hecton8.Core.csproj` error at `Assets/_Project/Scripts/Gameplay/BaseAirlock.cs(24,24)`: missing namespace `Hecton8.Gameplay.AirlockPressurization`. No SHINOBU_320/KCC bridge/thermal readback/metabolism report file appeared in the compiler error.
- PASS: post-Loop-45 SHINOBU/shared JSON reports parse, standalone self-audit XML parses, focused `git diff --check` passes with CRLF warnings only, and brace scan remains balanced for contracts `3/3`, registry `340/340`, KCC `340/340`, jobs `79/79`, runtime `189/189`, debug gizmo `4/4`, data `16/16`, layout validator `7/7`, thermodynamics `401/401`.
- PASS: post-Loop-45 owned-path greps found no direct sibling namespace imports, hidden `.Complete()`, hot `new NativeArray/List/HashMap`, `Allocator.Persistent`, `foreach`, LINQ, `math.exp`, `UnityEngine.Random`, `Time.deltaTime`, or `Pack=...` in SHINOBU metabolism runtime/jobs/data/contracts. `ShinobuMetabolismJobs.cs` has 6 deterministic Burst job attributes and 35 `NoAlias` pointer fields.
- PASS: thermodynamics contract scan found no `IThermodynamicsService.SampleThermalFlow` downstream caller still using the old interface DTO; remaining `AbyssalThermalManager.ThermalFlowSample` callsites use the concrete legacy overload.
