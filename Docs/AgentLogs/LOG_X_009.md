# LOG X_009

## 2026-05-23 Phase 0 - Physiology Archaeology

What was wrong:
- Physiology decompression authority is still 16-tissue: `ShinobuPhysiologyConstants.TissueCompartmentCount = 16`, `DecompressionStateDTO` is 128 bytes with `fixed float TissueTensionsN2[16]`, and `IntegrateBloodGasTensionsJob` loops over `TissueCompartmentCount`.
- Status authority is fragmented through `uint StatusFlags` and `uint ActiveTraumaMask`; the requested `ulong StatusEffectMask` does not exist in the hot physiology contract.
- Cadence is still effectively per-frame: `ShinobuPhysiologyRuntime` is `IUpdatable`, registers through `TryRegisterUpdatable`, and gates with `AuthoritativeUpdateIntervalSeconds = 0.016f`.
- SlowTick and ColdTick already exist in `SystemDispatcher` at 0.1s and 1.0s. Physiology is not using the 10 Hz lane yet.
- Some runtime publication still uses `GlobalSignals.Publish`; typed `SignalBus` lanes exist and are already used for selected physiology/damage/hypoxia outputs.

What was done:
- Re-extracted `<AGENT_PROMPT id="X_009" ...>` from `Docs/Tasks/CURRENT_BATCH.md` with an attribute-tolerant CLI regex.
- Read selected mandates for survival pressure/O2 logic, ARM64 DTO layout, zero-GC, native jobs, dispatcher phases, registry/signal doctrine, blackbox telemetry, and AUP.
- Built `Docs/Reports/PHYSIOLOGY_OPTIMIZATION_REPORT_X_009.json` with file/line targets, replacement route, DTO byte layout, status bit allocation, and cadence/signal map.
- Updated `Docs/Tasks/Status_X_009.md` with Task 01 blocked for AST execution, Task 02 complete, Task 03 complete.
- Updated `Docs/AgentLogs/Rationale_X_009.md` with non-fluff decision notes and rejected alternatives.

Cinematic Cheats used:
- 3 tissue lanes replace medical fidelity: fast blood/lung, medium muscle/organ, slow bone/fat. This is enough for warning/damage timing if threshold multipliers are tuned and stress-tested.
- Presentation smoothing is moved to VISUAL_SYNC/UI. Truth stays 10 Hz; visual alarms can interpolate without mutating gameplay state.
- Quality scaling affects telemetry/presentation density only. It must not change status bit authority, DTO layout, save identity, or decompression damage route.

Exact microseconds saved:
- Phase 0 changed no runtime code, so measured saved time is 0 us.
- Design target: moving from 0.016s to 0.1s removes about 50 redundant schedule/solve opportunities per second.
- Design target: replacing 16 lanes with 3 removes 13 tissue lane updates per active entity solve and enables removal of `entityCapacity * 16` tissue sidecar rows.
- Estimated low-end gain after implementation: 35-80 us per active player-scale solve on i3/MX350-class CPU, pending profiler proof.

Verification:
- Compile not run. CPU guard reported 100 and no dotnet/csc process was launched.
- AST not completed. Direct Roslyn load failed with `Roslyn.Utilities.StringTable` initializer exception; dotnet fallback was blocked by CPU rule.

## 2026-05-23 Phase 0 - Call Graph Pass

What was wrong:
- The previous Phase 0 report had target files and DTO/cadence design, but the physiology core call graph was not explicit enough for surgical Phase 1 work.
- CPU guard still blocks compiler-backed AST work: latest CPU sample was 99.

What was done:
- Re-read `Status_X_009.md`, `Rationale_X_009.md`, and the X_009 prompt from `Docs/Tasks/CURRENT_BATCH.md`.
- Added `physiologyCallGraph` to `Docs/Reports/PHYSIOLOGY_OPTIMIZATION_REPORT_X_009.json`.
- Captured current chained path: `Tick` -> `MockEnvironmentDropJob` -> `GenerateMockBreathingGasJob` -> `CalculatePartialPressuresJob` -> `PhysiologySignalIngestJob` -> `IntegrateBloodGasTensionsJob` -> `CalculateCnsToxicityJob` -> `OxygenConsumptionJob` -> `LateFrameTick` finalize -> telemetry/publish/dump.
- Captured replacement path: `ISlowTickable.SlowTick` -> 3-lane physiology job -> ulong status job -> no-wait `ILateFrameTickable` finalize -> typed SignalBus publication.

Cinematic Cheats used:
- Keep 10 Hz truth and move warning smoothness to presentation. This buys readable UI without simulating extra decompression compartments.

Exact microseconds saved:
- Runtime saved time remains 0 us because no gameplay code was modified.
- Design target remains removal of about 50 redundant schedule opportunities per second and 13 tissue lane updates per solve.

## 2026-05-23 Phase 0 - Revalidation Under CPU Guard

What was wrong:
- The Phase 0 directive was repeated while Task 01 remains blocked specifically on compiler-backed AST proof.
- CPU guard returned 100. Project law forbids launching dotnet/csc when CPU is above 50.

What was done:
- Re-read `Docs/Tasks/Status_X_009.md`.
- Re-read `Docs/AgentLogs/Rationale_X_009.md`.
- Re-extracted the X_009 XML block from `Docs/Tasks/CURRENT_BATCH.md`.
- Preserved existing Phase 0 report and call graph instead of duplicating or falsifying AST completion.

Cinematic Cheats used:
- None. This pass was verification and state protection only.

Exact microseconds saved:
- 0 us measured. No runtime code changed.
- Implementation target remains unchanged: 3 tissue lanes, `ulong StatusEffectMask`, 10 Hz SlowTick truth.

## 2026-05-23 Phase 0 - Active Compiler Guard

What was wrong:
- The Phase 0 directive was repeated while the machine was already compiling or compiler-adjacent work was active.
- CPU guard returned 99.
- Active processes: `csc` and `dotnet`.

What was done:
- Re-read `Docs/Tasks/Status_X_009.md`.
- Re-read `Docs/AgentLogs/Rationale_X_009.md`.
- Re-extracted the X_009 XML block from `Docs/Tasks/CURRENT_BATCH.md`.
- Did not launch dotnet, csc, Roslyn fallback, or Unity compile.
- Did not touch runtime source code.

Cinematic Cheats used:
- None. Guard pass only.

Exact microseconds saved:
- 0 us measured.
- Avoided adding compiler contention on a saturated machine; no runtime performance claim.

## 2026-05-23 APEX Override - Physiology Source Rewrite

What was wrong:
- Physiology authority still exposed a heavy decompression shape and frame-lane scheduling risk.
- Status truth in physiology was still fragmented through `uint StatusFlags` without a unified `ulong` physiology mask.
- Warning publication could fire every active SlowTick during DCS state.

What was done:
- `TissueCompartmentCount` is now 3.
- `DecompressionStateDTO` is 64B explicit layout with fast/medium/slow N2 lanes and threshold fields; former pad fields now store `LastWarningFrame` and `WarningPulseCount`.
- `StatusEffectStateDTO` is 64B explicit layout with `ulong StatusEffectMask` at offset 0 and no managed references.
- `PhysiologyScalarsDTO` now carries `ulong StatusEffectMask`.
- `IntegrateBloodGasTensionsJob` now iterates exactly 3 scalar lanes, uses local Buhlmann coefficients, writes decompression telemetry, and builds the status mask in Burst.
- `ShinobuPhysiologyRuntime` uses `ISlowTickable` at 0.1s instead of `IUpdatable`/0.016s.
- `ShinobuSensoryImpairmentRuntime` no longer registers an update fallback.
- Warning emission is edge-or-10-SlowTick cadence; barotrauma damage remains 10 Hz truth.
- Static `rg` found no remaining old 16-buffer markers, `IUpdatable` physiology route, `public Tick`, or `float.Parse` in Physiology.

Cinematic Cheats used:
- Three tissue lanes: fast, medium, slow. The missing intermediate conservatism is handled by one 1.05 correction multiplier, not by restoring 16 rows.
- Warning presentation is throttled in the signal lane; gameplay damage remains continuous at the 10 Hz authority cadence.

Exact microseconds saved:
- Measured: 0 us, compile/profiler blocked by CPU guard.
- Estimated source-level: removes 13 tissue lane updates per active solve and about 50 frame-lane schedule checks per second.
- Warning queue pressure reduced from every active SlowTick to state-change plus 1 Hz.

500m numeric smoke:
- Profile: 500m depth, 600s dwell, 60s ascent, 120s surface, dt 0.1.
- Exact exp vs Pade final tissue error: 0.000000000 atm at printed 9-decimal precision.
- Invalid count: 0.
- 16-row local CSV peak risk: 13.594522.
- 3-row uncorrected peak risk: 13.052175, 3.9894% lower.
- 3-row corrected peak risk with multiplier 1.05: 13.704784.

What still does not pass:
- No Unity/dotnet compile was launched. CPU guard returned 100 and compiler processes were active earlier in the override; project law forbids another build under that condition.
- No runtime profiler proof exists yet.

## 2026-05-23 APEX Override Follow-Up - Total Physiology/Status Audit

What was wrong:
- `BuildStatusEffectMask` still only mapped `ShinobuTraumaBits.Laceration` to bleeding. Poison, stun, radiation, and suffocation could be present in combat/status space without appearing in the physiology `ulong` mirror.
- Runtime still referenced the old `buhlmann_zh16_profiles.csv` profile path before this follow-up work. That was a configuration regression vector even though the hot loop was already three lanes.
- CSV suffix parsing clamped any numeric suffix into lane 0..2. A legacy `n2_15` row could overwrite slow lane 2 if the wrong CSV was loaded.
- `HectonSurvivalSystem.cs` still had a stale comment claiming a 16-tissue Vault model.

What was done:
- Re-ran source grep across `Assets/_Project/Scripts` for `buhlmann_zh16`, `zh16`, `16-tissue`, `TissueTensionsN2`, fixed tissue buffers, `entityCapacity * 16`, and `HaldaneTissueCoefficientDTO[16]`. Runtime source scope is clean.
- Re-ran grep across Physiology and Gameplay/Combat for `float.Parse`/`.Parse(`. No runtime parse calls found in the audited scope.
- Re-ran grep for managed status engines: `List<...Effect>`, `Dictionary<...Effect>`, `StartCoroutine`, `WaitForSeconds`, `System.Timers`, `new Timer`. Hits are editor/scanner string fixtures only, not runtime status authority.
- Re-ran grep for physiology/combat frame routes: no `IUpdatable`, `TryRegisterUpdatable`, `UnregisterUpdatable`, or `public void Tick(` in the audited Physiology/Gameplay Combat scope.
- Added `ShinobuCombatStatusBridgeBits`, expanded `ShinobuTraumaBits` to poison/stun/radiation/suffocation, and made `MockCombatDamageSignal.CombatStatusMask` live at offset 20 without changing the 32B DTO size.
- Updated `PhysiologySignalIngestJob` and `BuildStatusEffectMask` so trauma and combat-status bits merge into one `ulong StatusEffectMask`.
- Updated mock combat injection to support trauma types 0..7 and produce matching status bridge bits for bleed, poison, stun, radiation, and hypoxia.
- Added `buhlmann_3tissue_profiles.csv` with exactly three rows. Runtime now points at it. Digit suffix parsing rejects out-of-range lanes instead of clamping.

Cinematic Cheats used:
- The 3-row model stays active. The offline 16-row model is only a comparison reference, not a runtime dependency.
- Status effects stay as bit flags. Presentation tiers can read the richer mask, but gameplay truth does not branch into managed classes.

Exact microseconds saved:
- Measured: 0 us. No Unity profiler pass or build was launched under CPU/compiler guard.
- Source-level: prevents a future managed poison/stun/radiation bridge and keeps status merge to fixed uint/ulong OR operations.
- The existing 3-lane collapse still removes 13 tissue lane updates per active solve and about 50 frame-lane schedule opportunities per second versus the old source shape.

500m follow-up proof:
- Profile: 500m depth, 600s dwell, 60s ascent, 120s surface, dt 0.1.
- Full-profile exact exp vs Pade max error: 0.000000000080 atm.
- Corrected 3-lane peak risk: 8.374027.
- Local 16-row reference peak risk: 8.230608.
- Warning pulses: 137, status transitions: 1, first warning frame: 6439, last warning frame: 7799.
- Combined poison+bleed+radiation physiology mask sample: `0x00000000000B0000`.

What still does not pass:
- Compile/profiler proof is still absent. I did not launch dotnet/Unity compile because project rule forbids it while CPU/compiler guard is active.
- The untracked legacy `buhlmann_zh16_profiles.csv` remains on disk but is no longer the runtime profile path. I did not delete an unknown untracked file in a dirty multi-agent worktree.

## 2026-05-23 APEX Override Follow-Up - Residual DCS Route Cut

What was wrong:
- `HectonSurvivalSystem` still contained a parallel survival-facade decompression route: old scalar nitrogen loading, local nitrogen build-up mutation, bends status checks, and decompression vomit severity from `_nitrogenBuildUp`.
- `SurvivalPhysiologyScalarJob` still called `SomaticSurvivalMath.ResolveNitrogenTissueLoad`, `ResolvePressureNarcosis01`, and `ShouldApplyBendsDamage`.
- `BaseAtmosphereMath` could emit a bends damage request from atmosphere/breathing hazard math. That was a hidden cross-domain DCS authority.

What was done:
- `HectonSurvivalSystem` no longer opens/executes the scalar physiology buffer route, no longer owns the scalar physiology Vault handle, and now mirrors decompression/narcosis presentation from `SignalBus<PhysiologyStateSignal>` when `SourceHash == SourceShinobuPhysiology`.
- Survival bends status now uses `_physiologyBendsActive` from the SHINOBU physiology signal instead of old scalar thresholds.
- Air pockets refill oxygen only; they no longer decrement local nitrogen build-up.
- Decompression vomit severity now uses `_decompressionRisk01`, which is driven by physiology signal/risk presentation, not the old nitrogen build-up scalar.
- `SurvivalPhysiologyScalarJob` is presentation-only: movement drain, freezing/starving/dehydration/toxicity stay; decompression/narcosis authority outputs are zero.
- `SomaticSurvivalMath` legacy scalar DCS helpers are neutralized to no-op compatibility values.
- `BaseAtmosphereMath` rapid ascent now requests visual blur only. It no longer writes DCS health damage or atmosphere-owned nitrogen tissue loading.
- Smoke expectations were updated so old scalar DCS compatibility calls must return safe no-op values.

Cinematic Cheats used:
- Atmosphere keeps cheap rapid-ascent visual blur as a presentation fake. Decompression damage remains owned by the 3-tissue SHINOBU solver.
- Compatibility helpers remain only to avoid no-build compile churn; they cannot produce damage or narcosis if accidentally called.

Exact microseconds saved:
- Measured: 0 us. Latest CPU guard returned 100 with active `csc` and multiple `dotnet` processes, so no dotnet/csc/Unity compile or profiler run was launched.
- Estimated: removed one survival scalar DCS branch path plus scalar buffer open/execution attempt from the survival facade. The bigger win remains the already-applied 3-lane/10 Hz physiology route.

500m reproof:
- Active CSV: `C:\hades\Hecton8\buhlmann_3tissue_profiles.csv`.
- Active rows: 0, 1, 2.
- Profile: 500m depth, 600s dwell, 60s ascent, 120s surface, dt 0.1.
- Full-profile exact exp vs Padé max error: 0.000000000026 atm.
- Corrected 3-lane peak risk: 13.701998.
- Local 16-row offline reference peak risk: 13.592025.
- Ratio after correction: 1.008091.
- Warning pulses: 137. Status transitions: 1. First warning frame: 6439. Last warning frame: 7799.

What still does not pass:
- Compile/profiler proof is still absent due CPU guard 100 > 50 with active `csc`/`dotnet`.
- Remaining old-named static wrappers are no-op compatibility paths and smoke-test references, not runtime authority.

## 2026-05-23 APEX Override Follow-Up - Toxin/Radiation Cadence Audit

What was wrong:
- `ToxicOutgassingChemistryRuntime` still had an active `IUpdatable.Tick` simulation path while `SlowTick` was empty. That was a real frame-lane toxin/poison route.
- `TraumaDispatcher` reported `RadiationHazardGrid.ReportExternalDose` from frame Tick. The rest of Tick is presentation/interaction, but the dose report is physiology state mutation.
- `HectonSurvivalSystem` and solar flare radiation in `RandomEventSystem` were already SlowTick-routed, but both used stale `0.5f` dt against the dispatcher normal SlowTick interval of `0.1`.
- `HectonSurvivalSystem.Tick` still held rapid-ascent risk, pressure exposure tracking, oxygen grace, and death checks on the frame lane.

What was done:
- Removed `IUpdatable` from `ToxicOutgassingChemistryRuntime`, removed its update registration, and moved the simulation accumulator into `SlowTick` with 0.1 second cadence input.
- Added `ISlowTickable` registration to `TraumaDispatcher`; moved only `UpdateRadiationFatigue` into `SlowTick`, leaving HUD/EMP/LOS and channel decay in frame Tick.
- Changed `HectonSurvivalSystem._slowTickDt` from 0.5 to 0.1.
- Changed `RandomEventSystem` solar flare/event SlowTick dt from 0.5 to 0.1.
- Moved legacy survival rapid-ascent risk, pressure exposure tracking, oxygen grace, pressure hull stress, life telemetry, and death check from `HectonSurvivalSystem.Tick` into `HectonSurvivalSystem.SlowTick`.
- Re-ran source grep. Remaining external radiation dose callsites are only: `TraumaDispatcher.SlowTick`, `HectonSurvivalSystem.SlowTick`, and `RandomEventSystem.SlowTick`.

Cinematic Cheats used:
- Toxin/radiation truth stays 10 Hz. Presentation can remain frame-rate responsive by reading decay/signal state, but it cannot mutate dose truth per frame.
- No binary quality switch was added; cadence is fixed to dispatcher truth and presentation quality remains separate.

Exact microseconds saved:
- Measured: 0 us. CPU guard returned 94.84 with no active compiler processes, still above the project hard limit of 50, so no build/profiler was launched.
- Estimated: toxic outgassing no longer checks/schedules simulation from frame Tick; radiation fatigue no longer reports dose every frame while active. Exact savings require Unity profiler after CPU guard clears.

500m reproof:
- Active CSV: `C:\hades\Hecton8\buhlmann_3tissue_profiles.csv`.
- Rows: 300s, 2298s, 11220s.
- Profile: 500m depth, 600s dwell, 60s ascent, 120s surface, dt 0.1.
- Full-profile exact exp vs runtime Pade33-reduced max error: 0.000000000930 atm.
- Invalid count: 0.
- Corrected raw 3-lane peak risk: 13.701407.
- Local 16-row raw reference peak risk: 13.591379.
- Corrected raw ratio: 1.008095.
- Runtime saturated risk01: 1.0.
- Warning pulses: 137. Status transitions: 1. First warning frame: 6439. Last warning frame: 7799.

What still does not pass:
- Compile/profiler proof is still absent. A guarded `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /p:UseSharedCompilation=false` was attempted after CPU guard cleared, but it timed out after 120s with no diagnostics returned. Orphaned MSBuild/VBCSCompiler workers from that probe were stopped and the final compiler-process check was clear.
- `git diff --check` is not clean at repository level because of existing `.meta` trailing whitespace and LF/CRLF warnings outside X_009 touched scope.

## 2026-05-23 APEX Override Follow-Up - Hidden Hazard Cadence Cut

What was wrong:
- `RadiationHazardGrid` used dispatcher SimulationPhase with a quality-weighted evaluation interval down to 0.016s. That let high-quality radiation dose truth become frame-rate again.
- `HazardZoneManager` accumulated toxicity dose and toxic damage pulses from an `IUpdatable` frame accumulator.
- `HectonPlayerHealth` decayed nutritional toxicity and gas physiology bridge timers in frame Tick.
- `EnvironmentalHazard` and `HectonHazardSource` still used frame update registration for toxic/heat/radiation hazard refresh.
- `TraumaDispatcher` still applied parasite spore toxic survival damage from frame Tick.

What was done:
- `RadiationHazardGrid` now implements `ISlowTickable`; Simulation/PostSimulation/VisualSync phase systems remain for job fencing, but radiation evaluation budget comes only from 0.1s SlowTick.
- `RadiationHazardGrid.PostSimulationRadiation` now returns before mutating player radiation dose or publishing dose/geiger telemetry on frames where no 10 Hz evaluation completed.
- `HazardZoneManager` now implements `ISlowTickable`; toxic dose accumulation, damage pulses, exposure job scheduling, and diagnostics run from the 0.1s slow lane.
- `HectonPlayerHealth` keeps frame Tick for invulnerability/combat sync only; nutritional toxicity and gas physiology bridge timers moved to SlowTick.
- `EnvironmentalHazard` and `HectonHazardSource` now register through `TryRegisterSlowTickable`.
- `TraumaDispatcher` now updates parasite spore toxic damage from SlowTick, while frame Tick only decays presentation channels and updates audio/interaction state.

Cinematic Cheats used:
- Truth cadence is fixed to 10 Hz. Visual/audio/HUD channels can keep frame decay for smoothness, but dose/toxin/status mutation cannot run at frame rate.
- High tier can spend quality on radiation SDF/bulkhead sample counts and presentation, not faster gameplay truth.

Exact microseconds saved:
- Measured: 0 us. Compile/profiler were not launched because CPU guard returned 100 with active `csc`, `VBCSCompiler`, and multiple `dotnet` processes.
- Estimated: removes up to ~50 radiation evaluations/sec at high quality and cuts legacy toxic dose/source/timer mutation from frame cadence to 10 Hz.

500m reproof:
- Active CSV: `C:\hades\Hecton8\buhlmann_3tissue_profiles.csv`.
- Rows: 300s, 2298s, 11220s.
- Profile: 500m depth, 600s dwell, 60s ascent, 120s surface, dt 0.1.
- Full-profile exact exp vs runtime Pade33-reduced max error: 0.000000001397 atm.
- Invalid count: 0.
- Corrected raw 3-lane peak risk: 13.703731.
- Local 16-row raw reference peak risk: 13.593089.
- Corrected raw ratio: 1.008140.
- Runtime saturated risk01: 1.0.
- Warning pulses: 137. Status transitions: 2. First warning frame: 6438. Last warning frame: 7793.

What still does not pass:
- Compile/profiler proof is absent. CPU is 100 and compiler processes are active; project rule forbids a build.
- `git diff --check` for X_009 touched C# files reports only LF->CRLF warnings, no whitespace errors.

## 2026-05-23 APEX Override Follow-Up - Final Repeat Audit

What was wrong:
- The previous follow-up still needed a hard source sweep after the scalar DCS compatibility cut.
- The 500m proof needed to distinguish worst-case compressed-air override from the runtime default auto-heliox gas path.

What was done:
- Re-extracted `<AGENT_PROMPT id="X_009">` from `Docs/Tasks/CURRENT_BATCH.md`: 43 lines, 11290 chars, `ENGINEERING_IDENTITY` and `MANDATORY_CONSTRAINTS` present.
- Re-ran scoped `rg` over Physiology, Survival, Atmosphere toxin, Combat status, Radiation, and hazard files.
- Found no active `16-tissue`, `zh16`, `buhlmann_zh16`, `TissueTensionsN2`, `fixed float[16]`, `entityCapacity * 16`, `n2_15`, or runtime `float.Parse` in the audited source.
- Managed status collection hits are editor scanner/report strings only; runtime status state remains `StatusEffectStateDTO` 64B with `ulong StatusEffectMask@0` and Combat status state uses `ulong StatusEffectMask`.
- Reviewed `Tick`/`SlowTick` routes: `HectonSurvivalSystem.Tick`, `TraumaDispatcher.Tick`, and `HectonPlayerHealth.Tick` now retain presentation/combat/context work only; oxygen/pressure/DCS/toxin/radiation mutation paths are in `SlowTick`.
- Updated `Docs/Reports/PHYSIOLOGY_OPTIMIZATION_REPORT_X_009.json` with the latest 500m gas-mode split and verification state.

Cinematic Cheats used:
- Atmosphere rapid-ascent remains a visual-only blur fake; DCS damage is owned by the 3-tissue SHINOBU physiology route.
- Warning signal traffic is edge-or-10-SlowTick cadence. Damage truth remains 10 Hz; no managed cooldown/timer list was added.

Exact microseconds saved:
- Measured: 0 us. Compile/profiler were not launched because the final guard returned CPU 100 with active `VBCSCompiler` PID 29352.
- Estimated: source route removes 13 tissue lanes per decompression solve, cuts frame-lane toxin/radiation/status mutation to 10 Hz, and removes up to ~50 high-quality radiation evaluations/sec. Profiler proof is still absent.

500m proof:
- Active CSV: `C:\hades\Hecton8\buhlmann_3tissue_profiles.csv`.
- Active rows: 300s, 2298s, 11220s.
- Profile: 500m depth, 600s dwell, 60s ascent, 120s surface, dt 0.1.
- Worst-case air override: max exact exp vs runtime Pade33-reduced error 0.00000000049658410717 atm; invalid count 0; corrected raw 3-lane risk 13.703730659583233; offline 16-row raw reference 13.593088775058543; corrected/reference ratio 1.0081395690380321; saturated runtime risk01 1.0.
- DCS-only warning cadence under air override: 137 pulses, 2 transitions, first frame 6439, last pulse frame 7799.
- All warning causes under air override: 781 one-Hz pulses because compressed air at 500m is narcotic from frame 1. That is not false spam; it is cadence-gated persistent fatal status.
- Default auto-heliox: max exact exp vs runtime Pade33-reduced error 0.00000000021009327611 atm; invalid count 0; DCS risk 0; DCS warning pulses 0.

What still does not pass:
- Compile/profiler proof is absent. CPU is 100 and `VBCSCompiler` PID 29352 is active; project rule forbids a build.
- Scoped `git diff --check` on X_009 touched C# files exited 0 with LF->CRLF warnings only; repository-wide diff remains noisy from unrelated agents.

## 2026-05-24 APEX Override Follow-Up - DCS/Gas Warning Split Fix

What was wrong:
- `IntegrateBloodGasTensionsJob` included `Narcosis` in the decompression warning mask, so narcosis alone could drive `CauseDecompression` traffic.
- `CalculateCnsToxicityJob` published a gas physiology signal every SlowTick even when gas status was unchanged or zero.
- The gas DTO already had an unused offset-28 pad, but the editor layout validator still referenced it by the old `_pad0` name after the runtime rename.

What was done:
- `CauseDecompression` is now gated by `DecompressionWarningStatusMask`: bends, fatal bends, hyperbaric override, invalid math. Narcosis is excluded.
- DCS signal stress and fatal severity now use decompression stress only. `Narcosis01` stays in the signal as context, not as a DCS trigger.
- `GasPhysiologyStateDTO.LastWarningFrame@28` gates gas warnings by status edge or 10 SlowTick cadence.
- Toxic gas damage remains outside the warning gate and still runs at 10 Hz when above damage threshold.
- Removed the managed completion hook `PublishLatestGasPhysiologyState`, which otherwise bypassed the job gate and pushed gas status every completed SlowTick.
- `ShinobuMetabolismLayoutValidator` now validates `GasPhysiologyStateDTO.LastWarningFrame` at offset 28.

Cinematic Cheats used:
- Warning traffic is a low-frequency signal stream. Gameplay truth stays 10 Hz; presentation can smooth between pulses.
- No managed cooldown list, no coroutine, no timer class, no DTO growth.

Exact microseconds saved:
- Measured: 0 us. Final CPU guard returned 100 with active `csc` PID 29336 and eight `dotnet` processes; no compile/profiler by project rule.
- Estimated: removes unchanged/zero gas warning queue writes, removes narcosis-only false DCS emissions, and removes one managed gas SignalBus push plus three Vault array opens per completed SlowTick when no gas edge/cadence pulse is due. Damage queue pressure is unchanged where actual toxic injury exists.

500m proof after split:
- Active CSV: `C:\hades\Hecton8\buhlmann_3tissue_profiles.csv`.
- Profile: 500m depth, 600s dwell, 60s ascent, 120s surface, dt 0.1.
- Worst-case air override: exact exp vs runtime Pade33-reduced max error 0.0000000022082531359 atm; invalid count 0.
- Corrected 3-lane raw risk: 13.704257439699663.
- Offline 16-row uncorrected raw reference: 13.593805270969174.
- Corrected/reference ratio: 1.0081251839737893.
- DCS-only warning cadence: 137 pulses, 2 transitions, first frame 6439, last pulse frame 7794.
- Gas/narcosis warning cadence: 657 pulses, 2 transitions, first frame 1, last frame 6552.
- Narcosis-only false `CauseDecompression` emissions: 0.
- Default auto-heliox: max exact-vs-Pade error 0.0000000002564775059 atm, DCS risk 0, DCS warning pulses 0.

What still does not pass:
- Compile/profiler proof is absent. CPU is 100 with active `csc` PID 29336 and eight `dotnet` processes; project rule forbids launching a build.
- Scoped `git diff --check` on X_009 touched C# files exits 0 with LF->CRLF warnings only.

## 2026-05-24 APEX Override Follow-Up - Toxicity Lane And Physiology Source Cleanup

What was wrong:
- `ToxicityExposureSignal` was produced by `ToxicOutgassingChemistryRuntime`, but SHINOBU physiology did not ingest it into the toxemia/status pipeline.
- The atmosphere runtime also emitted `PhysiologyStateSignal` packets with `SourceHash=0`, outside the SHINOBU physiology owner route.
- Stress-only systems were using `PhysiologyStateSignal` as a generic stress lane: ladder climb, SDF squeeze, volcanic heat, and player stress metrics.
- `GlobalShaderDispatcher` accepted decompression visual payloads by cause alone, without verifying SHINOBU source.

What was done:
- Added `ShinobuPhysiologyRuntime.IngestAtmosphereToxicitySignals`: typed `SignalBus<ToxicityExposureSignal>` -> `MockToxemiaSignal` native buffer -> `PhysiologySignalIngestJob` -> `StatusEffectStateDTO.StatusEffectMask`.
- Removed atmosphere-authored `PhysiologyStateSignal` publish and prewarm from `ToxicOutgassingChemistryRuntime`.
- Converted ladder climb, player kinematics squeeze, volcanic heat, and player stress metrics to publish stress through `PlayerStressSignal` only.
- `GlobalShaderDispatcher` now requires `SourceHash == PhysiologyStateSignal.SourceShinobuPhysiology` for decompression visuals.
- `ShinobuMetabolismJobs` now writes `ShinobuMetabolismConstants.SourceHash` into `PhysiologyStateSignal.SourceHash` instead of an entity hash.

Cinematic Cheats used:
- Stress remains a cheap presentation/control signal. It does not masquerade as authoritative physiology.
- Toxic atmosphere exposure is a typed event feeding one 10 Hz native physiology truth route; no per-effect managed objects or timers were added.

Exact microseconds saved:
- Measured: 0 us. Build/profiler not launched; latest guard reports CPU 79 with no active compiler processes, above the project limit.
- Estimated: removes one bogus `PhysiologyStateSignal` push per toxic exposure, removes four stress-only physiology publishers from the authoritative lane, and reduces visual physiology queue scan noise. The hard correctness change is poison now enters the 64B status DTO mask through SHINOBU.

Verification:
- `rg` found no `PhysiologyStateSignal` references in `ToxicOutgassingChemistryRuntime`.
- Runtime source scan now shows one `ToxicityExposureSignal` consumer: `ShinobuPhysiologyRuntime.IngestAtmosphereToxicitySignals`.
- Non-SHINOBU stress producers no longer push `PhysiologyStateSignal`; remaining outside-SHINOBU runtime push is `ShinobuMetabolismRuntime`, now tagged with `ShinobuMetabolismConstants.SourceHash`.
- Scoped `git diff --check` on touched C# files exits 0 with LF->CRLF warnings only.

## 2026-05-24 APEX Override Follow-Up - Direct Toxic Damage And Radiation Bridge

What was wrong:
- `HazardZoneManager.ApplyToxicityDamagePulse` still hit `HectonSurvivalSystem.TakeDamage` directly after toxic dose accumulation.
- `HectonSurvivalSystem.HandleNutritionalToxicity` still subtracted survival integrity in a local loop.
- Radiation dose updated player health/radiation presentation but did not enter SHINOBU `StatusEffectStateDTO.StatusEffectMask` through the same bitmask path.
- `HectonPlayerHealth.UpdateGasPhysiologyBridge` could re-read stale `TryGetLatest` gas packets and keep gas stress pinned after fresh SHINOBU signals stopped.

What was done:
- `HazardZoneManager` now resolves the player combat target, publishes `ToxicityExposureSignal` with AUP/entity id, and queues `CombatStatusBits.Poisoned64`.
- Removed direct toxic survival damage from `HazardZoneManager`.
- `HectonSurvivalSystem` no longer applies nutritional toxicity as direct integrity damage; applying food toxicity queues poison status and publishes `ToxicityExposureSignal`.
- `ShinobuPhysiologyRuntime` now drains `RadiationDoseSignal` into `MockCombatDamageSignal.CombatStatusMask |= Irradiated`; the existing Burst ingest path turns it into the unified status mask and `RadiationDose01`.
- `HectonPlayerHealth` now accepts latest gas physiology only when sequence is new and the signal frame is within a 12-frame hold window; stale latest packets decay out.

Cinematic Cheats used:
- Toxin/radiation injury is represented as status bits plus low-frequency physiology state, not as independent damage loops.
- Gas UI gets a short scalar hold window so 1 Hz warning cadence does not flicker, but stale signals cannot become permanent truth.

Exact microseconds saved:
- Measured: 0 us. Build/profiler not launched; CPU guard returned 68 with no active compiler processes, above the project limit of 50.
- Estimated: removes two direct toxic integrity branches, avoids a parallel radiation state lane, and prevents stale gas bridge churn. Runtime work added is fixed struct signal writes and one native status queue request per toxic application/pulse.

Verification:
- `rg` found no `nutritionalToxicityTimer`, `toxicityDamageTimer`, runtime `StatusTimer`, runtime `EffectTimer`, status list, or status class engine hits in audited runtime files. Remaining hits are editor scanner/facade fixtures.
- `rg` confirms `HazardZoneManager.ApplyToxicityDamagePulse` routes through `ToxicityExposureSignal` and `CombatStatusBits.Poisoned64`.
- `rg` confirms `ShinobuPhysiologyRuntime.IngestRadiationDoseSignals` consumes `SignalBus<RadiationDoseSignal>`.
- Scoped `git diff --check` on touched C# files exits 0 with LF->CRLF warnings only.

## 2026-05-24 APEX Override Follow-Up - Parasite Spore Toxic Route

What was wrong:
- `TraumaDispatcher.UpdateActiveParasiteSporeHazard` still applied parasite spore toxicity by calling `_survivalSystem.TakeDamage(...)`.
- That route was already on SlowTick, but still bypassed `CombatStatusBits.Poisoned64`, `ToxicityExposureSignal`, and SHINOBU `StatusEffectStateDTO`.

What was done:
- Added player health target caching to `TraumaDispatcher`.
- Replaced direct parasite spore survival damage with `CombatDamageRuntime.TryQueueStatusEffect(... Poisoned64 ...)`.
- Published `ToxicityExposureSignal` from parasite spores using `_playerMovement.CurrentAup`, player entity id, and a fixed toxemia delta scale.

Cinematic Cheats used:
- Parasite spores are represented as poison state plus toxemia scalar. No bespoke parasite damage integrator.

Exact microseconds saved:
- Measured: 0 us. CPU guard returned 99.81 with no active compiler processes; no build/profiler.
- Estimated: removes one direct survival damage call per parasite interval and keeps all toxic injury on the same status queue/physiology mask route.

Verification:
- `rg` over `TraumaDispatcher`, `HazardZoneManager`, `HectonSurvivalSystem`, and `RandomEventSystem` now leaves only the survival API definition and non-status random thermal eruption `TakeDamage(5f)`.
- Scoped `git diff --check` on touched C#/docs files exits 0 with LF->CRLF warnings only.

## 2026-05-24 APEX Override Follow-Up - Random Thermal Status Route

What was wrong:
- `RandomEventSystem.TryTriggerThermalEruption` directly called `survivalSystem.TakeDamage(5f)` for a burn hazard.

What was done:
- Replaced the direct survival damage with `CombatDamageRuntime.TryQueueStatusEffect(... Burning64 ...)`.
- Target resolution uses cached `IPlayerRuntimeContext.PlayerHealth`, with survival object fallback for target id only.

Cinematic Cheats used:
- Thermal eruption is now a short burning status bit. No custom thermal damage loop.

Exact microseconds saved:
- Measured: 0 us. Final CPU guard returned 82.69 with active `VBCSCompiler` PID 29512; no build/profiler.
- Estimated: negligible CPU change; correctness gain is removal of the last direct status-like `TakeDamage` call in the audited slice.

Verification:
- `rg TakeDamage(` across `TraumaDispatcher`, `HazardZoneManager`, `HectonSurvivalSystem`, and `RandomEventSystem` now leaves only `HectonSurvivalSystem.TakeDamage` API definition.
- Scoped `git diff --check` exits 0 with LF->CRLF warnings only.

## 2026-05-24 APEX Override Follow-Up - Player Status Bypass Sweep

What was wrong:
- `PlayerInventory.DispatchInventoryThermalRunaway` used survival direct damage for radioactive thermal runaway.
- `BioReactor` meltdown used survival direct damage for player collider hits.
- `ModuleLifeSupportComponent` fire cascade used survival direct damage.
- `PlayerTool.HandleRuntimeOverchargeFailure` used `HectonPlayerHealth.TakeDamage` directly.
- `EnvironmentalHazard` still carried a commented future direct survival damage integration path.

What was done:
- Inventory runaway now queues `Burning64` and `Irradiated64` and publishes `RadiationDoseSignal` with player AUP.
- BioReactor meltdown now queues `Burning64` and `Irradiated64` and publishes `RadiationDoseSignal`; structural module damage remains direct module integrity damage.
- Life-support fire now queues `Burning64`.
- Tool overcharge now queues `Stunned64 | Burning64`.
- Removed the stale `EnvironmentalHazard` direct-damage comment.

Cinematic Cheats used:
- Fire, electrical overcharge, and radiation side effects are compressed into mask bits plus scalar magnitude. No bespoke per-system damage timers.

Exact microseconds saved:
- Measured: 0 us. CPU guard returned 65.74, above the 50 threshold; no build/profiler.
- Estimated: negligible per rare event; correctness gain is removal of four direct player status/health bypasses and one forbidden future route comment.

Verification:
- `rg survival.TakeDamage|trackedPlayerSurvival.TakeDamage|playerHealth.TakeDamage` over `Assets/_Project/Scripts` returns no hits.
- Scoped `git diff --check` exits 0 with LF->CRLF warnings only.

## 2026-05-24 APEX Override Follow-Up - Survival Parse Surface

What was wrong:
- `HectonSurvivalSystem` still used `float.TryParse` for survival database numeric columns.
- This was cold parsing, not `StatusEffectStateDTO`, but it left a survival-domain parse hit that could mask real runtime regressions later.

What was done:
- Replaced numeric `float.TryParse` calls with `TryParseSurvivalFloat`.
- Parser consumes `ReadOnlySpan<char>`, supports sign/decimal/exponent, clamps exponent parsing to float-safe bounds, and rejects NaN/infinity.

Cinematic Cheats used:
- None. This is source hygiene and cold parsing determinism, not visual simulation.

Exact microseconds saved:
- Measured: 0 us. CPU guard remained closed at 100 after the patch.
- Estimated: hot-path 0 us; cold parser avoids culture parser route and removes audit noise.

Verification:
- `rg float.TryParse|float.Parse|double.TryParse|double.Parse` in `HectonSurvivalSystem`, `HectonPlayerHealth`, and runtime Physiology leaves only an editor scanner string fixture.
- `git diff --check -- Assets/_Project/Scripts/HectonSurvivalSystem.cs` exits 0 with LF->CRLF warning only.

## 2026-05-24 APEX Override Follow-Up - DCS Architecture Doc Sync

What was wrong:
- `Docs/ARCHITECTURE/DECOMPRESSION_SICKNESS_SHINOBU_321.md` still documented the obsolete 128-byte/16-tissue runtime route.

What was done:
- Updated the active architecture doc to the current 64-byte/3-tissue route.
- Documented `buhlmann_3tissue_profiles.csv`, `ThreeTissueRiskCorrection`, SHINOBU-only authority, gas/DCS warning split, and `StatusEffectStateDTO.StatusEffectMask@0`.

Cinematic Cheats used:
- The documented model is the cheap three-lane approximation; visual/audio overkill is downstream only.

Exact microseconds saved:
- Measured: 0 us.
- Estimated: 0 us; prevents future 16-lane regression.

Verification:
- `rg` now finds obsolete 16-tissue wording only in editor scanner strings, archives, historical logs, and the old comparison CSV, not in active runtime source.

## 2026-05-24 APEX Override Follow-Up - Toxic Flora Physiology Bridge

What was wrong:
- `FloraInteractionManager` applied toxic spore poison through combat status only.
- SHINOBU toxemia/status DTO did not receive a `ToxicityExposureSignal` for that flora poison route.

What was done:
- Added toxic spore `ToxicityExposureSignal` publication with player AUP, target entity id, exposure scalar, toxemia delta, and stable chemical hash.
- Kept the existing combat poison route so combat status and physiology status both observe the same exposure.

Cinematic Cheats used:
- Toxic flora exposure is a scalar toxemia pulse plus poison bit. No flora-specific physiology solver.

Exact microseconds saved:
- Measured: 0 us.
- Estimated: added work is one fixed signal write per toxic spore exposure event; correctness gain is closing the flora-poison bridge to SHINOBU.

Verification:
- Source read confirms `TryApplyToxicSporePoisonStatus` queues `Poisoned64` through `TryQueueStatusEffect` and then calls `PublishToxicSporeToxicityExposure`.

## 2026-05-24 APEX Override Follow-Up - Survival Fallback Toxic Damage

What was wrong:
- `HectonSurvivalSystem.HandleToxicity` still had a fallback branch for missing `HazardZones` that subtracted `integrity` directly.
- `HectonPlayerHealth.Heal` still converted high-toxicity healing into direct `TakeDamage(positiveAmount, true)`.

What was done:
- Added `PublishEnvironmentalToxicityStatus` in `HectonSurvivalSystem`.
- Environmental fallback toxicity now queues `CombatStatusBits.Poisoned64` and publishes `ToxicityExposureSignal` with AUP, entity id, exposure scalar, toxemia delta, and environmental toxin hash.
- High blood toxicity now suppresses healing instead of causing direct health damage.

Cinematic Cheats used:
- Environmental poison is a scalar toxemia pulse plus poison bit. No fallback suit-integrity toxin solver.

Exact microseconds saved:
- Measured: 0 us. CPU guard returned 100 with active `dotnet` PID 13852; no build/profiler.
- Estimated: removes one fallback toxic integrity subtraction branch and one heal-to-damage branch; added work is one bounded native status request plus one fixed signal push during fallback toxic exposure.

Verification:
- `rg` confirms `HandleToxicity` now calls `PublishEnvironmentalToxicityStatus`.
- `rg` confirms `HectonPlayerHealth.Heal` no longer contains `TakeDamage(positiveAmount, true)`.
- Final scoped `rg` found forbidden 16-tissue/parse/status-timer markers only in `Physiology/Editor/OOP_Bends_Scanner.cs` scanner strings, not in active runtime source.
- Scoped `git diff --check` exits 0 with LF->CRLF warnings only.
- Compile was not launched: CPU 100 with active `csc` PID 11092 and active `dotnet` PIDs 6224, 11444, 13068, 13672, 16164, 24008, 25436, 26068.

## 2026-05-24 APEX Override Follow-Up - Seed Ship Radiation Cadence

What was wrong:
- `SeedShipAnomalyRuntime` published radiation source/dose from late-frame completion while corruption was active.
- Dose export was not tied to the 0.1s physiology cadence and could write `RadiationDoseSignal` on every completed frame.

What was done:
- Added a SlowTick-armed radiation export gate.
- Scaled anomaly radiation dose by 0.1s before publishing.
- Sanitized radiation intensity and emit source removal when corruption stops.

Cinematic Cheats used:
- Radiation remains a scalar source/dose pulse. Frame-rate anomaly visuals can stay rich, but physiology dose truth is 10 Hz.

Exact microseconds saved:
- Measured: 0 us. Build/profiler were not launched.
- Estimated: removes up to roughly 50 redundant radiation dose signal writes/sec while the anomaly is active.

Verification:
- Source read confirms `RadiationDoseSignal` export is behind `_radiationExportRequested`, which is set by `SlowTick()`.
- Scoped `git diff --check -- Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs` exits 0 with LF->CRLF warning only.

## 2026-05-24 APEX Override Follow-Up - Survival SHINOBU Snapshot Freshness

What was wrong:
- `HectonSurvivalSystem` consumed raw latest `PhysiologyStateSignal` from a mixed signal lane.
- Stale SHINOBU packets could pin nitrogen/narcosis presentation and oxygen drain scaling after authoritative physiology expired.

What was done:
- Added SHINOBU-only snapshot scan with frame freshness.
- Raw latest fallback is accepted only when it is fresh and `SourceHash == PhysiologyStateSignal.SourceShinobuPhysiology`.
- Stale/missing SHINOBU authority clears local bends, narcosis, nitrogen build-up, nitrogen load, and warning latch.

Cinematic Cheats used:
- None. This is authority routing and stale-state removal.

Exact microseconds saved:
- Measured: 0 us.
- Estimated: CPU delta negligible; prevents false persistent physiology penalties and mixed-source reads.

Verification:
- `HectonSurvivalSystem.TryApplyPhysiologyAuthoritySnapshot` now calls `TryGetLatestShinobuPhysiologySignal`.
- `ResolvePsychoMetricsOxygenDrainScale` uses the same SHINOBU-only helper.
- Scoped `git diff --check -- Assets/_Project/Scripts/HectonSurvivalSystem.cs Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs` exits 0 with LF->CRLF warnings only.
- Compile was not launched: CPU 82 with no active `dotnet`, `csc`, `VBCSCompiler`, or `MSBuild` processes, still above the project threshold of 50.

## 2026-05-24 APEX Override Follow-Up - Final Toxic/Radiation Bypass Cut

What was wrong:
- `GasDynamicsSolver` still exposed a dead `NativeQueue<ToxicitySignal>` route.
- `ToxicOutgassingChemistryRuntime` and `RadiationHazardGrid` still staged toxic/radiation damage as direct combat packets before completion.
- `ShinobuMetabolismRuntime` published toxic metabolism slot damage directly.
- `CalculateCnsToxicityJob` emitted toxic `CombatDamageSignal` even though gas toxemia/status bits already existed.

What was done:
- Retired the gas dynamics toxicity queue without deleting the interface contract.
- Converted toxic outgassing corrosion to `Poisoned64`.
- Converted critical radiation degradation to `Irradiated64`.
- Converted metabolic toxic slot to `ToxicityExposureSignal`.
- Removed gas toxic damage emission from `CalculateCnsToxicityJob`.
- Removed toxic damage type reuse from starvation/dehydration.
- Converted toxic flora poison from zero-damage `TryQueueDamage` to `TryQueueStatusEffect(Poisoned64)`.
- Updated SHINOBU_274 route docs and radiation scanner text to say irradiated status, not direct combat damage.

Cinematic Cheats used:
- Poison/radiation are scalar status pulses and toxemia/dose lanes. No per-effect managed class, no managed timer, no fallback health subtraction.

Exact microseconds saved:
- Measured: 0 us. CPU guard returned 94 then 100; no build/profiler.
- Estimated: removes dead gas toxicity queue math/writes, one gas toxic combat enqueue route, and direct toxic/radiation completion publishes.

Verification:
- `rg` found no `CombatDamageTypeToxic` or `DamageType = *Toxic` in active Physiology runtime.
- `rg` found `TryDequeueToxicitySignal` only at the retired method/comment plus interface definition; no consumer exists.
- `rg` found no direct `SignalBus<CombatDamageSignal>.TryPush` in toxic outgassing or radiation grid completion paths.
- Scoped `git diff --check` on touched X_009 files exits 0 with LF->CRLF warnings only.
- Compile was not launched: CPU 100 with no active `dotnet`, `csc`, `VBCSCompiler`, or `MSBuild`, still above the project threshold of 50.

## 2026-05-24 APEX Override Follow-Up - Toxic Outgassing Status Naming Purge

What was wrong:
- Toxic outgassing behavior already queued `Poisoned64`, but the internal staged DTO and job fields still used combat/damage names.
- The stale names made source sweeps look like a hidden combat damage lane.

What was done:
- Renamed `ToxicityCombatDamageSignal` to `ToxicityStatusSignal`.
- Renamed `CombatSignals` to `StatusSignals`, `DamageType` to `StatusType`, `ToxicDamageType` to `ToxicStatusType`, and `CombatSignalBufferId` to `StatusSignalBufferId`.
- Preserved BufferID value `70811`; only the ownership/name surface changed.

Cinematic Cheats used:
- None. This is source authority cleanup for the scalar poison status route.

Exact microseconds saved:
- Measured: 0 us.
- Estimated: 0 us. This prevents route regression, not frame cost.

Verification:
- `rg "CombatSignal|CombatDamage|DamageType|ToxicDamageType|ToxicityCombat"` in toxic outgassing runtime/types returns only the status API call `CombatDamageRuntime.TryQueueStatusEffect`.
- Active Physiology/Gameplay/Atmosphere 16-tissue grep found no runtime route; remaining hits are editor scanner strings and architecture text that labels the 16-row CSV as archive/comparison.
- Cadence grep found no `IUpdatable`, `TryRegisterUpdatable`, or runtime `Tick()` authority hit in audited Physiology, toxic outgassing, or radiation grid files.
- Compile was not launched: CPU 90 with no active `dotnet`, `csc`, `VBCSCompiler`, or `MSBuild`, still above the project threshold of 50.

## 2026-05-24 APEX Override Follow-Up - Transient Status Latch And Target Route Fix

What was wrong:
- SHINOBU status bridge could latch one-shot bleeding/radiation trauma bits in `PhysiologyDTO.ActiveTraumaMask`.
- `CombatDamageSignal` conversion preferred 16-bit `TargetId` over full `TargetHash`, risking misrouted poison/radiation/status damage for high entity ids.

What was done:
- Reused `PhysiologyDTO` pad fields as `ActiveTraumaRefreshMask` and `LastTraumaRefreshFrame`; layout remains 32B explicit.
- Added refresh marking in `PhysiologySignalIngestJob`.
- Added transient cleanup in `OxygenConsumptionJob`: bleeding clears after 6s without refresh, stun after 0.75s, radiation after 40 SlowTicks without a new dose/status pulse.
- Changed `CombatDamageRuntime.TryBuildCombatSignal` to prefer `TargetHash`, with `TargetId` as fallback only.

Cinematic Cheats used:
- Status truth stays scalar: `ulong` mask plus fixed refresh windows. No managed timers, no per-effect classes.

Exact microseconds saved:
- Measured: 0 us.
- Estimated: CPU delta is negligible; prevents stuck mask bits and target truncation regressions.

Verification:
- `rg` confirms `ActiveTraumaRefreshMask` is written in ingest, consumed in `ClearExpiredTransientTraumaMask`, then cleared in `OxygenConsumptionJob`.
- `rg` confirms active 16-tissue markers remain absent outside editor scanner strings.
- `git diff --check -- Assets/_Project/Scripts/Physiology/ShinobuPhysiologyData.cs Assets/_Project/Scripts/Physiology/ShinobuPhysiologyJobs.cs Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs Assets/_Project/Scripts/Physiology/ShinobuRespawnJobs.cs Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` exits 0 with LF->CRLF warnings only.
- Compile was not launched: CPU 100 with active `csc` PID 19052 and `dotnet` PID 28132.
- Final build guard recheck was still blocked: CPU 100 with active `csc` PID 30664 and `dotnet` PIDs 7032, 9936, 12604, 16768, 24396, 24404, 24680, 26116.
- Pre-final build guard stayed blocked: CPU 96; no compiler process table was returned, but project rule still forbids compile above CPU 50.

## 2026-05-24 APEX Override Follow-Up - Radiation Critical Staging Purge

What was wrong:
- `RadiationHazardGrid` no longer pushed direct radioactive combat damage, but the Burst job still staged critical degradation as a `CombatDamageSignal`.
- The staged row still carried `DamageType = CombatDamageTypes.Radioactive`, which kept a hidden radiation damage DTO in physiology/status code.

What was done:
- Replaced the one-row radiation `_damageSignalLane` with a 32B unmanaged `RadiationStatusSignal`.
- `CalculateRadiationExposureJob` now writes `TargetId`, `SourceId`, `Magnitude01`, and `Frame` only.
- PostSimulation queues `CombatStatusBits.Irradiated64` through `CombatDamageRuntime.TryQueueStatusEffect`.
- Radiation source/dose producers in `RadiationHazardGrid` now use explicit `TryPush` instead of `Push`.
- Updated the SHINOBU_274 route card to state that the staging lane is `RadiationStatusSignal`, not `CombatDamageSignal`.

Cinematic Cheats used:
- Radiation sickness stays a scalar status pulse plus dose state. No direct radioactive damage packet, no managed timer, no per-effect class.

Exact microseconds saved:
- Measured: 0 us.
- Estimated: 32 bytes saved in the single radiation staging row; CPU delta is negligible.

Verification:
- `rg` finds no `CombatDamageSignal`, `_damageSignal`, `DamageType = CombatDamageTypes.Radioactive`, or `DirectRuntimeFlag` in `RadiationHazardGrid.cs`.
- Scoped toxic/radiation direct-damage grep across Physiology, Atmosphere, `RadiationHazardGrid`, flora, and survival returns no active forbidden hits.
- `rg` finds no `SignalBus<RadiationSourceSignal>.Push` or `SignalBus<RadiationDoseSignal>.Push` in `RadiationHazardGrid.cs`.
- `git diff --check -- Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs` exits 0 with LF->CRLF warning only.
- Compile was not launched: CPU sampled 100, above the project threshold of 50.

## 2026-05-24 APEX Override Follow-Up - Metabolism SlowTick Fallback Correction

What was wrong:
- `ShinobuMetabolismConstants.NominalSlowTickSeconds` still stored `0.5f`.
- Burst metabolism jobs use that constant as fallback when dt is non-finite, creating a hidden fivefold step against the real 0.1s SlowTick cadence.

What was done:
- `NominalSlowTickSeconds` now aliases `DispatcherSlowTickSeconds`.
- Normal finite dt behavior is unchanged; the bad-input fallback is no longer stale.

Cinematic Cheats used:
- None. This is cadence correctness for scalar metabolism.

Exact microseconds saved:
- Measured: 0 us.
- Estimated: 0 us in normal operation; prevents rare fivefold toxin/starvation/dehydration/hypothermia jumps after invalid dt.

Verification:
- `rg` finds no `NominalSlowTickSeconds = 0.5f` or `DispatcherSlowTickSeconds = 0.5f`.
- Scoped `git diff --check` exits 0 with LF->CRLF warnings only.
- Compile was not launched: CPU remained above threshold.

## APEX Override Continued Audit - Metabolism Combat Damage Bypass Removal

What was wrong: `ShinobuMetabolismRuntime` staged metabolic starvation/dehydration/hypothermia through `CombatDamageSignal`. Starvation/dehydration used `DamageType=0`, which the combat bridge treated as Impact damage.

What was done: Replaced the metabolism combat staging lane with 64B `MetabolicExposureSignalDTO` while preserving four slots per entity on BufferID `70275`. Toxic overflow uses slot 3 and publishes only `ToxicityExposureSignal`; starvation/dehydration/hypothermia remain physiology state signals and no longer become combat damage packets.

Cinematic Cheats used: none. This is authority cleanup, not presentation.

Exact Microseconds saved: not measured. Source-level queue reduction is up to 3 false combat packets per active metabolism row under fatigue/hypothermia.

Verification: `rg` found no active `CombatDamageSignal`, `SignalBus<CombatDamageSignal>`, `MetabolismCombatSignalsBuffer`, `CombatSignal*`, `CombatDamageType*`, or `DamageType` in `ShinobuMetabolism*.cs`. `git diff --check` passed on touched metabolism/radiation files with LF->CRLF warnings only. Compile not launched: CPU 100 with active compiler processes.

## APEX Override Continued Audit - Radiation Status Naming Purge

What was wrong: radiation critical status staging still referenced the stale `Shinobu274RadiationDamageSignal` enum name and local damage-scale naming.

What was done: kept BufferID `72748`, but runtime source now uses local `RadiationStatusSignalBuffer` and `RadiationStatusMagnitudeScale`.

Cinematic Cheats used: none.

Exact Microseconds saved: 0 us. Regression prevention.

Verification: scoped grep found no stale radiation damage staging names in `RadiationHazardGrid.cs`; diff hygiene passed.

## APEX Override Continued Audit - Player Stress Combat Signal Filter

What was wrong: `PlayerStressMetricsRuntime` used the latest global `CombatDamageSignal` as player stress input without target or AUP relevance. Damage elsewhere could raise player stress, and a later unrelated hit could hide an earlier real player hit.

What was done: Replaced raw latest read with a bounded current-frame snapshot scan, `IsCombatDamageNearPlayer`, and an 8m AUP gate before applying damage impulse. The signal is still consumed read-only on SlowTick.

Cinematic Cheats used: AUP-radius approximation instead of combat registry polling.

Exact Microseconds saved: not measured. Added a bounded snapshot scan per new combat damage generation; removed false stress spikes.

Verification: scoped grep shows no combat damage latest read remains in Physiology; `git diff --check` passed with LF->CRLF warning only.

## APEX Override Continued Audit - Player Stress Mixed-Latest Input Cut

What was wrong: `PlayerStressMetricsRuntime` still read latest `AcousticPingSignal` and latest `PlayerStateSignal` from shared lanes. A same-frame nearby ping/squeeze could be lost if a later unrelated packet became latest.

What was done: Acoustic and player-state stress now consume `SignalBus<T>.GetFrameSnapshot()` behind `SnapshotGeneration` guards. Squeeze stress requires `StateSqueezing`, `FlagSqueezing`, finite AUP, and a 5m player AUP gate. No managed buffer or status timer was added.

Cinematic Cheats used: scalar stress belief from proximity-weighted cues, not a physical panic simulation.

Exact Microseconds saved: measured 0 us. Runtime cost is a bounded snapshot scan only on changed lane generation; correctness gain is removal of mixed-latest false stress.

Verification: scoped grep shows no acoustic, player-state, or combat-damage latest read remains in `PlayerStressMetricsRuntime.cs`; `git diff --check` passed with LF->CRLF warning only.

## APEX Override Continued Audit - Survival Bleeding Timer Severance

What was wrong: `HectonSurvivalSystem` still owned bleeding as a local timer and direct integrity damage path via `_bleedingSecondsRemaining` and `_bleedingDamagePerSecond`.

What was done: New bleeding trauma queues `CombatStatusBits.Bleeding64` through `CombatDamageRuntime.TryQueueStatusEffect`. Old in-memory bleeding is converted once in `HandleInjuries`; legacy saved bleeding timer fields are cleared on load. `IsBleeding` now reads the combat status mask using the cached player combat target id.

Cinematic Cheats used: bleeding remains a scalar status and UI/fauna scent cue. Damage/duration truth is the combat status mask job, not a survival-side medical simulation.

Exact Microseconds saved: measured 0 us. Removes one survival-side bleeding timer/damage branch per active bleeding SlowTick.

Verification: scoped grep shows no `_injuryStatus |= PlayerInjuryStatus.Bleeding`, no `_bleedingSecondsRemaining = Mathf.Max(...)` start path, and no direct `_bleedingDamagePerSecond` integrity attrition in `HectonSurvivalSystem.cs`.

## APEX Override Continued Audit - Remaining Latest Read Removal

What was wrong:
- `PlayerStressMetricsRuntime` still consumed latest light level.
- `HectonPlayerHealth` still had a latest gas physiology fallback after snapshot scanning.
- `HectonSurvivalSystem` still used global latest as a DCS/gas presentation fallback.

What was done:
- Player stress light now uses `SignalBus<LightLevelSignal>.GetFrameSnapshot()` with `SnapshotGeneration`, `CaveVoxelSdf` filtering, and 12-frame freshness.
- Player health gas bridge now uses only the frame snapshot plus its local 12-frame hold.
- Survival physiology presentation now uses a local cached fresh SHINOBU snapshot instead of global latest.

Cinematic Cheats used:
- Scalar snapshot/cached belief for light/gas/DCS presentation. No managed history list and no direct owner polling.

Exact Microseconds saved:
- Measured: 0 us.
- Estimated: negligible CPU delta; removes stale-state false positives and global-latest order dependence.

Verification:
- Scoped `rg` found no active `TryGetLatest(` hits in Physiology, Atmosphere, `HectonSurvivalSystem`, `HectonPlayerHealth`, or `RadiationHazardGrid`.
- `git diff --check` passed on the touched source files with LF->CRLF warnings only.

## APEX Override Continued Audit - Nutritional Poison Timer Removal

What was wrong:
- `HectonSurvivalSystem` and `HectonPlayerHealth` still owned local nutritional poison countdown fields after poison had already been routed to combat status and toxemia.
- Health healing/stress and survival toxicity could diverge from `CombatStatusBits.Poisoned64`.

What was done:
- Removed `_nutritionalToxicitySecondsRemaining` and `_nutritionalToxicitySeverity01` from survival and health.
- Food poison now queues `CombatStatusBits.Poisoned64` and publishes `ToxicityExposureSignal`; health/survival read active poison from `CombatDamageRuntime.TryGetStatusEffectMask`.
- `HectonPlayerHealth.ApplyNutritionalToxicity` remains as a compatibility route, but it only queues the status bit.

Cinematic Cheats used:
- Poison is a bitmask status plus scalar toxemia signal, not a parallel medical countdown simulation.

Exact Microseconds saved:
- Measured: 0 us.
- Estimated: removes two local poison timer branches and one duplicate health-side status owner.

Verification:
- `rg` found no `_nutritionalToxicity`, `UpdateNutritionalToxicity`, or `nutritionalToxicityTimer` in `Assets/_Project/Scripts`.
- Scoped toxic/radiation direct damage grep found no active `DamageType = CombatDamageTypes.Toxic/Radioactive`, `CombatDamageTypeToxic`, `ToxicDamageType`, `ToxicityCombatDamageSignal`, or direct `SignalBus<CombatDamageSignal>.TryPush` in audited physiology/toxin/radiation source.
- Build not launched: CPU sampled 59 with no active compiler processes, then final guard sampled CPU 100 with no compiler process table returned. Both are above the project threshold of 50.

## APEX Override Continued Audit - Survival Frame Owner Severance

What was wrong: `HectonSurvivalSystem` still sat on the frame update dispatcher via `ITickable`/`IUpdatable` and `GlobalRegistry.TryRegisterUpdatable`. The body was mostly context/publish work, but it kept a hot survival physiology lane alive.

What was done: Removed the frame update interfaces, `_registeredUpdatable`, register/unregister calls, and `Tick(float)`. Blood-scent spatial refresh moved to `SlowTick` after depth/pressure calculation. `LateFrameTick` remains presentation-only for narcosis shader flush.

Cinematic Cheats used: 10 Hz scent/status refresh instead of frame-rate survival context churn.

Exact Microseconds saved:
- Measured: 0 us.
- Estimated: removes one frame dispatcher owner and five per-frame survival publish/context calls; adds one bounded scent refresh per SlowTick.

Verification:
- Scoped grep found no `ITickable`, `IUpdatable`, `_registeredUpdatable`, `TryRegisterUpdatable`, `UnregisterUpdatable`, or `public void Tick(` in `HectonSurvivalSystem.cs`.
- Domain sweeps found no active 16-tissue, `TryGetLatest`, toxic/radiation direct damage, or managed status timer/list hits in the audited physiology/survival/toxin/radiation files; remaining markers are editor scanner strings.
- `git diff --check -- Assets/_Project/Scripts/HectonSurvivalSystem.cs` passed with LF->CRLF warning only.
- Independent 500m smoke: active 3-row CSV, compressed-air override, dt 0.1, max exact-vs-Pade error `1.3969838619232178E-09 atm`, invalid count 0, corrected 3-lane raw risk `13.703730659582087` vs offline 16-row raw reference `13.593088775061581`, ratio `1.0081395690377226`, warning pulses 138, transitions 3.
- Build not launched: CPU sampled 82 with no active compiler process table; project threshold is 50.
- Final build guard: CPU sampled 100 with active `csc` PID 29976 and active `dotnet` PID 19856; project rule still blocks compile.

## APEX Override Continued Audit - Survival Fracture Timer Severance

What was wrong: `HectonSurvivalSystem` still had a fracture duration owner outside the combat status mask. `_fractureSecondsRemaining` was a local status timer and `HasFracture`/`FracturePenalty01` could diverge from the `ulong` status engine.

What was done: Added `CombatStatusBits.Fractured64`, stored `FractureSeconds` in `CombatStatusEffectState` at offset 60 without growing the 64B DTO, and routed survival fracture trauma through `CombatDamageRuntime.TryQueueStatusEffect`. Survival now clears legacy saved fracture timers, reads fracture/bleeding/poison presentation from a cached combat status mask, and no longer decrements a local fracture timer.

Follow-up: removed `_bleedingSecondsRemaining` and `_bleedingDamagePerSecond` from `HectonSurvivalSystem` as well. Save compatibility writes zero values; legacy in-memory bleeding is converted once into `Bleeding64` from severity-derived duration/DPS.

Cinematic Cheats used: fracture is a scalar status bit plus movement penalty read model, not a separate bone/medical simulation. The cache is only presentation stability while the status job is scheduled.

Exact Microseconds saved: measured 0 us. Estimated: one local fracture countdown branch removed per SlowTick and one false movement/UI reset path removed when status storage is temporarily unavailable.

Verification:
- Scoped `rg` found no local `_fractureSecondsRemaining`, `_bleedingSecondsRemaining`, `_bleedingDamagePerSecond`, no `_injuryStatus |= PlayerInjuryStatus.Fracture`, and no bleeding integrity attrition in `HectonSurvivalSystem.cs`.
- Runtime forbidden marker scan found only editor scanner strings for `buhlmann_zh16`, `entityCapacity * 16`, `float.Parse`, `StatusTimer`, and `EffectTimer`.
- `git diff --check` on tracked touched files passed with LF->CRLF warnings only; untracked status-effect split files had no trailing whitespace hits.
- Build not launched: CPU sampled 100 with active `csc` and `dotnet`, above the project threshold of 50 and compiler guard.

## APEX Override Continued Audit - Health/Thermal/Environmental Route Cut

What was wrong:
- `HectonPlayerHealth` poison presentation could read the combat status mask directly during status job scheduling and falsely clear poison/healing suppression.
- `AbyssalThermalManager.EmitThermalShock` direct-published `CombatDamageSignal` with `DirectRuntimeFlag`.
- `EnvironmentalHazard` still had a direct toxic `DamagePacket` fallback and a helper path that could classify non-heat hazards as `CombatDamageTypes.Toxic`.

What was done:
- Health poison state now uses a SlowTick-refreshed cached combat status mask; queued nutritional poison marks the read model immediately after `Poisoned64` queue success.
- Thermal shock now queues through `CombatDamageRuntime.TryQueueDamage` with `CombatStatusBits.Burning`, retaining AUP impact.
- Environmental Toxicity/Biohazard now publish `ToxicityExposureSignal` and queue `CombatStatusBits.Poisoned64`; central damage helper is heat-only and no longer returns Poisoned/Toxic.

Cinematic Cheats used:
- Toxic exposure is one scalar toxemia pulse plus one status bit. No local health damage fallback, no local poison timer, no managed status object.

Exact Microseconds saved:
- Measured: 0 us.
- Estimated: removes one fallback damage packet branch, one direct thermal combat signal route, and one phase-sensitive status read. Main gain is route consistency and false-state prevention, not arithmetic time.

Verification:
- `git diff --check` on `EnvironmentalHazard.cs`, `HectonPlayerHealth.cs`, and `AbyssalThermalManager.cs` passed with LF->CRLF warnings only.
- Scoped `rg` in `EnvironmentalHazard.cs` found no `CombatDamageTypes.Toxic`, `DamagePacket`, `ReceiveDamage`, or non-64 `CombatStatusBits.Poisoned` route.
- Broader audited physiology/toxin/radiation grep leaves only SHINOBU barotrauma/suit direct damage lanes and combat status internals; no toxic/radiation hazard direct-damage fallback was found.
- Build not launched: CPU sampled 83 with active `csc` PID 15096 and active `dotnet` PID 28236.
- Final build guard after documentation update: CPU sampled 66 with active `dotnet` PID 28236, still above the project threshold and still blocked by an active build process.

## APEX Override Continued Audit - Trauma/Flora/Environmental Closure

What was wrong:
- `TraumaDispatcher` still owned status-adjacent mutation from frame update.
- `FloraInteractionManager` still ran toxic spore poison/toxemia exposure from frame Tick.
- `EnvironmentalHazard` still had a leftover `DamagePacket -> HectonPlayerHealth.ReceiveDamage` fallback after the earlier toxic route patch.

What was done:
- Removed `ITickable`/`IUpdatable`, update registration, unregister logic, and frame `Tick` from `TraumaDispatcher`; status/radiation/toxin/audio read model now advances from `SlowTick`.
- Moved `UpdateToxicSporeExposure` to `FloraInteractionManager.SlowTick` with 0.1s cadence while keeping visual flora interaction in frame Tick.
- Deleted `EnvironmentalHazard.ApplyOwnerHazardDamageFallback`; toxic/biohazard remains typed exposure plus `Poisoned64`, heat goes through the central combat queue only.

Cinematic Cheats used:
- Poison/radiation/status truth is 10 Hz scalar/mask work. Visual flora and trauma presentation can remain richer without owning gameplay truth.

Exact Microseconds saved:
- Measured: 0 us.
- Estimated: removes one frame dispatcher owner, frame-rate toxic spore scan/status publish, and one direct health damage fallback branch. Main gain is ownership stability and queue noise reduction, not raw arithmetic.

Verification:
- `rg` found no `ITickable`, `IUpdatable`, update registration, or frame `Tick` in `TraumaDispatcher.cs`.
- `rg` found `UpdateToxicSporeExposure` only at the SlowTick call site and method definition in `FloraInteractionManager.cs`.
- `rg` found no `DamagePacket`, `ReceiveDamage`, `ApplyOwnerHazardDamageFallback`, `CombatDamageTypes.Toxic`, or non-64 poison route in `EnvironmentalHazard.cs`.
- Active 16-tissue/parse/status-timer scan remains clean in audited runtime source; remaining marker hits are editor scanner strings.
- Direct toxic/radiation damage scan leaves only SHINOBU barotrauma/suit direct damage lanes.
- 500m smoke: active 3-row CSV, compressed-air override, dt 0.1, max exact-vs-Pade error `4.656612873077393E-10 atm`, invalid count 0, corrected 3-lane raw risk `13.704784138684754` vs offline 16-row raw reference `13.594521697764373`, ratio `1.008110799583226`, warning pulses 137, transitions 2.
- `git diff --check -- Assets/_Project/Scripts/Gameplay/EnvironmentalHazard.cs Assets/_Project/Scripts/Gameplay/TraumaDispatcher.cs Assets/_Project/Scripts/World/FloraInteractionManager.cs` passed with LF->CRLF warnings only.
- Build not launched: final guard sampled CPU 100, above the project threshold of 50; no compiler process table was returned.

## APEX Override Continued Audit - Gas/Health/Fauna Owner Tightening

What was wrong:
- `GasDynamicsSolver` still registered as a frame `IUpdatable` and retained a retired `_toxicitySignals` `NativeQueue<ToxicitySignal>` even though gas toxicity is now handled through SHINOBU gas/status lanes.
- `HectonPlayerHealth` still used frame countdown fields for invulnerability and survival-grace lockout.
- `FaunaBrain` predator bite and `SargassumMicroFaunaBoids` leviathan strike still had direct `DamagePacket -> HectonPlayerHealth.ReceiveDamage` fallback branches.

What was done:
- Removed `GasDynamicsSolver` frame `Tick`, update registration/unregistration, and the retired toxicity queue allocation/audit/dispose/prewarm/trim path. `FixedTick` captures base transition and hull repair signals while a gas job is running, preserving signal intake without a frame owner.
- Replaced player health countdown timers with absolute expiry timestamps and removed `ITickable`/`IUpdatable` from `HectonPlayerHealth`. Combat target retry, dirty combat health sync, poison cache refresh, and gas bridge run from `SlowTick`.
- Removed player direct damage fallback methods from predator bite and leviathan strike. Those routes now attempt only `CombatDamageRuntime.TryQueueDamage`.

Cinematic Cheats used:
- Gas toxicity legacy queue is retired instead of simulated twice. Invulnerability is timestamp math, not a frame timer. Player fauna impacts route through the combat owner; presentation AI/world ticks remain outside physiology truth.

Exact Microseconds saved:
- Measured: 0 us.
- Estimated: one gas frame owner removed, one player health frame owner removed, two frame countdown branches removed, one cold native queue allocation removed, and two direct player-integrity fallback branches removed. No profiler proof due CPU/build guard.

Verification:
- Scoped `rg` found no `IUpdatable`, `ITickable`, `public void Tick`, update registration, `_invulnerabilityTimer`, `_survivalGraceLockoutTimer`, `_toxicitySignals`, or `NativeQueue<ToxicitySignal>` in `GasDynamicsSolver`/`HectonPlayerHealth`.
- Scoped `rg` found no `ApplyPredatorBiteOwnerFallbackDamage`, `ApplyLeviathanStrikeOwnerFallbackDamage`, `new DamagePacket`, or `ReceiveDamage(in packet)` in `FaunaBrain.cs` and `SargassumMicroFaunaBoids.cs`.
- Audited physiology/survival/toxin/radiation runtime source remains clean for active 16-tissue/parser/status-timer markers; remaining hits are editor scanner strings.
- 500m smoke: active rows `n2_0:300`, `n2_1:2298`, `n2_2:11220`, compressed-air override, dt 0.1, max exact-vs-Pade error `1.39698386192322E-09 atm`, invalid count 0, raw 3-lane peak `13.0511720567456`, corrected peak `13.7037306595828`, offline 16-row raw reference `13.5930887748826`, corrected/reference ratio `1.00813956905105`, DCS warning pulses 137, transitions 1, first frame 6439, last frame 7799.
- `git diff --check -- Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs Assets/_Project/Scripts/Fauna/FaunaBrain.cs Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs` passed with LF->CRLF warnings only.
- Build not launched: final guard sampled CPU 37, but many `dotnet` processes were already active, so the project compiler guard blocked build launch.

## APEX Override Continued Audit - Shader/Tool Status Leak Closure

What was wrong:
- `GlobalShaderDispatcher` still used a latest/global physiology fallback after reading the frame snapshot, so stale SHINOBU DCS/gas packets could keep shader stress active after fresh physiology stopped.
- `ToolHitUtility` allowed direct `DamagePacket` fallback for status-bearing tool hits when the central combat target was not registered. `DamagePacket` cannot carry the `ulong` status duration/mask state.

What was done:
- Removed the physiology latest fallback from shader visual payload resolution. The dispatcher now reads only fresh SHINOBU frame-snapshot packets, tracks last decompression/gas signal frames, and clears each payload after a 24-frame hold.
- Changed `TryApplyUnregisteredDamageReceiverPacket` to reject any hit with known runtime status bits. Stun pistol and other status tools now either enter the combat/status owner route or do not apply a silent direct packet fallback.

Cinematic Cheats used:
- DCS/gas shader persistence is a bounded visual hold, not a second physiology owner. Status-bearing tool effects are forced through the packed mask path instead of simulated through legacy packet damage.

Exact Microseconds saved:
- Measured: 0 us.
- Estimated: removes one latest-lane physiology read/reapply path per shader dispatch and prevents hidden status fallback work. Main gain is false-warning prevention and owner-route correctness.

Verification:
- `rg` found no `SignalBus<PhysiologyStateSignal>.TryGetLatest` or `GlobalSignals.TryGetLatestPhysiologyStateSignal` in `GlobalShaderDispatcher.cs`.
- Scoped audit found no active runtime `buhlmann_zh16`, fixed 16-float tissue buffer, `float.Parse`, retired gas toxicity queue, or player health frame timer fields in audited physiology/survival/health/gas/render/tool source. Remaining marker hits are editor scanner strings.
- Tool sweep confirmed stun pistol still calls `ToolHitUtility.ApplyDamage` with `CombatStatusBits.Stunned`, while the unregistered packet fallback now rejects nonzero known status bits before building `DamagePacket`.
- Seed ship radiation export remains SlowTick-armed: `_radiationExportRequested` is set in `SlowTick`, dose is scaled by `RadiationExportSlowTickSeconds = 0.1f`, and radiation source removal is gated on the same request.
- 500m smoke after this patch: active 3-row CSV, compressed-air override, dt `0.1`, frames `7800`, max exact-vs-Pade tissue error `1.86264514923096E-09 atm`, raw 3-lane peak `13.0521753701745`, unsaturated corrected/reference ratio vs offline 16-row raw reference `1.0081107995964`, warning pulses `137`, transitions `1`, first warning frame `6439`, last `7799`.
- `git diff --check -- Assets/_Project/Scripts/Rendering/GlobalShaderDispatcher.cs Assets/_Project/Scripts/ToolHitUtility.cs` passed with LF->CRLF warnings only.
- Build not launched: guard sampled CPU 100, above the project threshold; no compiler launch attempted.
