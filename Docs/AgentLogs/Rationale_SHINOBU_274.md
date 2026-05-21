# SHINOBU_274 Rationale

## 2026-05-21 Loop 0 - Prompt Intake

Problem: Radiation trigger replacement must integrate with existing survival authority without duplicate health ownership.
Solution: Read current architecture, selected mandates, physiology code, GlobalRegistry contracts, SignalBus route, and DataVault route before coding.
Rejected Alternatives: Direct Unity trigger rewrite and standalone MonoBehaviour damage loop; both violate hot-path zero-GC and one-owner health routing.
Scalability potential: Low uses sparse cadence and simple shielding samples; Middle increases source count and sample count; High increases debug fidelity; Ultra spends saved CPU on visual shader mutation/noise only.
Hardware Impact: Expected low-end i3/MX350 gain is removal of trigger/raycast managed callbacks from runtime radiation path; exact microseconds pending static and compile verification.

## 2026-05-21 Loop 1 - Authority and Memory Route

Problem: Radiation dose needed a single owner route without trigger callbacks or hot GlobalRegistry polling.
Solution: RadiationHazardGrid owns the runtime phase; persistent state, sources, tuning, telemetry, and the pending damage signal use GlobalDataVault lanes under SystemID.GameplayRadiation and BufferID.Shinobu274Radiation*.
Rejected Alternatives: Component-local Lists, scene searches, or GlobalRegistry polling per tick. Those paths allocate, hide ownership, or make radiation depend on current scene shape.
Scalability potential: Low stores the same DTO with sparse cadence; Middle raises source count; High/Ultra can increase source count and SDF sample budget without changing health ownership.
Hardware Impact: Estimated 60 us/frame saved versus callback-heavy radiation volumes; compile/profiler verification pending due CPU gate.

Problem: RadiationStateDTO had to survive native copy, rollback, and ARM64 alignment requirements.
Solution: RadiationStateDTO is an explicit 32-byte struct with fields at fixed offsets; RadiationStateLayoutGuard validates size and critical offsets with UnsafeUtility.
Rejected Alternatives: Auto-layout DTOs, properties, or metadata classes. These risk CS1612 copies and platform-dependent layout.
Scalability potential: Same 32-byte state works on low, middle, high, and ultra; extra visuals read derived scalars rather than changing DTO shape.
Hardware Impact: 0 us/frame direct; prevents rollback/save corruption and cache waste.

## 2026-05-21 Loop 2 - Burst Dose Kernel and Shield Math

Problem: Radiation exposure needed deterministic math while base walls must attenuate dose through Voxel SDF and bulkhead state.
Solution: CalculateRadiationExposureJob runs Burst-compatible inverse-square source integration, double-AUP deltas, SDF density samples, and continuous bulkhead closure/seal shielding.
Rejected Alternatives: Physics.Raycast, trigger zones, collider overlaps, or per-wall MonoBehaviour callbacks. Standard Unity physics queries are not the radiation authority and are too expensive for repeated dose samples.
Scalability potential: Low uses fewer SDF samples and lower cadence; Middle uses moderate source/sample counts; High increases SDF samples; Ultra spends saved physics CPU on denser shielding and richer shader mutation.
Hardware Impact: Estimated 85-120 us/frame saved on i3/MX350-class CPU versus raycast/overlap shielding.

Problem: Burst cannot publish managed events, but health damage must use the existing combat route.
Solution: The job writes one CombatDamageSignal into a DataVault lane; the owner phase reads that lane and pushes SignalBus<CombatDamageSignal> on the main thread.
Rejected Alternatives: Direct health mutation from the job or managed SignalBus calls inside Burst. Both violate ownership or Burst restrictions.
Scalability potential: Signal cost is constant across quality tiers; only math cadence and visuals scale.
Hardware Impact: Estimated 20 us/frame avoided by eliminating service polling and scene target searches.

## 2026-05-21 Loop 3 - Health and Visual Deception

Problem: Radiation sickness had to affect existing HectonPlayerHealth and show visible hand mutation without CPU skin deformation.
Solution: Dose/degradation is applied through PlayerRuntimeContext and HectonPlayerHealth.SetRadiationExposure. UberNoir receives global mutation/dose/tint scalars plus a per-material hand radiation mask, then mutates vertices in the GPU vertex path.
Rejected Alternatives: New radiation health owner, animator parameters, blendshapes, decals, or CPU mesh edits. These create duplicate truth or per-frame CPU work.
Scalability potential: Low keeps the shader mutation small and sparse; Middle increases noise intensity; High/Ultra can make the GPU deformation visually aggressive while keeping gameplay dose identical.
Hardware Impact: Estimated 250 us/frame CPU saved versus CPU mesh/blendshape mutation on visible hands.

Problem: Radiation visuals needed continuous quality, not binary minimum/ultra branches.
Solution: Evaluation cadence lerps continuously from 0.2 seconds to 0.016 seconds using GlobalQualityWeight; shader visuals also scale continuously from the same global quality weight.
Rejected Alternatives: Boolean graphics tier switches. Binary switches violate scalability rules and cause state discontinuity.
Scalability potential: Low, Middle, High, and Ultra all use one curve; only cadence, sample budget, and visible deformation amplitude change.
Hardware Impact: Estimated 700 us/second saved at minimum cadence versus every-frame math.

## 2026-05-21 Loop 4 - Tooling, Profiles, and Black Box

Problem: Runtime tuning and profile ingestion needed to avoid managed runtime churn.
Solution: UI Toolkit editor writes RadiationTuningDTO in the vault; CSV profile ingestion parses bytes/spans into fixed DTOs; telemetry writes a fixed 300-frame ring and can dump Dump_SHINOBU_274.bin.
Rejected Alternatives: Runtime debug sliders, JSON/string.Split parsing, Debug.Log history, or List<T> telemetry.
Scalability potential: Low devices use conservative tuning; Middle/High increase source count and SDF samples; Ultra uses visual overkill while preserving the same DTOs.
Hardware Impact: Estimated 35 us/frame avoided in diagnostics by using fixed native telemetry instead of managed log/list history.

Problem: Integrators need proof that the old physics radiation route is gone or isolated.
Solution: RadiationTriggerDebtScanner writes Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_274.json and preserves Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json as a shared pointer/aggregate; remaining generic EnvironmentalHazard trigger/overlap debt is flagged outside the SHINOBU_274 radiation authority.
Rejected Alternatives: Chat-only audit or manual notes. File artifact is required and repeatable from the editor window.
Scalability potential: No runtime cost; prevents future regressions that would tax low-end CPUs and waste high-end visual budget.
Hardware Impact: 0 us/frame; architectural prevention artifact.

## 2026-05-21 Loop 5 - Verification Gate

Problem: Build verification is required, but the batch protocol forbids dotnet/csc when CPU is under work over 50 percent.
Solution: Checked for dotnet/csc and measured CPU load. No csc/dotnet process was found, but CPU load reported 100 percent, so compile was not launched. Ran non-compile checks instead.
Rejected Alternatives: Ignoring the CPU gate and launching dotnet build. That would violate the explicit repository protocol and contend with other agents.
Scalability potential: Verification state is independent of runtime scalability; implementation remains pending compile.
Hardware Impact: Avoided adding build pressure under saturated CPU; runtime gains remain estimates until profiler/compile pass.

## 2026-05-21 Loop 6 - Ultra Polish Static Corrections

Problem: Static audit found the first pass still used FrostTick cadence, private NativeArray allocation paths, same-frame job.Run execution, Time.deltaTime/frameCount, and a managed fallback dose path.
Solution: Routed radiation through SystemDispatcher Simulation/PostSimulation/VisualSync phase adapters. Grid read/write/source buffers now use GlobalDataVault IDs 72749/72750/72751. CalculateRadiationExposureJob and diffusion jobs are scheduled and returned as JobHandles; PostSimulation consumes completed Vault state. The managed fallback was removed; missing Vault now fails closed. Time scaling uses DispatcherTimingDTO.FrameDelta and accumulated seconds. Geiger audio moved to SignalBus<AcousticPingSignal>.
Rejected Alternatives: Keeping FrostTick with larger dt multiplier was rejected because FrostTick is a 5-second maintenance phase, not simulation authority. Keeping local NativeArray fallback was rejected because it creates shadow memory ownership and rollback ambiguity. Completing jobs manually in Tick was rejected because SystemDispatcher already owns the completion window.
Scalability potential: Low quality evaluates at the 0.2 second cadence and still integrates real elapsed seconds. Middle/High/Ultra continuously reduce the interval toward 0.016 seconds and raise SDF/bulkhead sample budgets without changing DTO layout or health authority. Visual overkill remains GPU-only through UberNoir mutation scalars.
Hardware Impact: Static gain estimate remains 85-120 us/frame versus physics shielding and managed trigger/raycast paths on i3/MX350. Removing local fallback allocations prevents session fragmentation on Quest-class unified memory. Compile/profiler proof is still blocked by the CPU gate.

Problem: Cross-domain lead shielding reads SHINOBU_220 bulkhead DTO lanes from Radiation Scrubber.
Solution: Added `Docs/ARCHITECTURE/SHINOBU_274_RADIATION_DOSE_ROUTE_CARD.md` to document owner, route, phase, read-only failure mode, and proof artifacts.
Rejected Alternatives: Duplicating bulkhead DTOs in radiation or querying scene colliders. Both create dual truth or PhysX dependency.
Scalability potential: Low uses fewer samples; Middle/High/Ultra increase sample budgets from the same read-only Vault route.
Hardware Impact: No new mutation cost; read-only Vault consumption avoids scene-query stalls.

Problem: UberNoir hand mutation added scalar HLSL logic but had no explicit shader warmup artifact.
Solution: Added `Assets/_Project/Art/Shaders/Variants/Hecton8_UberNoir_RadiationWarmup.shadervariants` using the existing UberNoir shader GUID and no new radiation keyword variants. Verified the YAML contains a `ShaderVariantCollection` root after raw asset creation.
Rejected Alternatives: Adding a new shader_feature keyword for radiation. That would multiply variants and create runtime warmup debt for one scalar visual fake.
Scalability potential: Low/Middle/High/Ultra share the same warmed UberNoir variant path; only scalar intensity changes.
Hardware Impact: Avoids first-use shader hitch risk without adding material instances or CPU mesh deformation.

## 2026-05-21 Loop 7 - Compile Wall Classification

Problem: Compile verification became allowed only after CPU load dropped below the project gate. The build then failed on external/stale dependencies before any SHINOBU_274 source error appeared.
Solution: Ran one throttled `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1`. Classified the failure as dependency compile wall: missing Crest bridge files, missing `Assets/_Project/Scripts/World/Contracts/GroundRadarContracts.cs`, missing `DecryptionBlackBoxDumpWriter`, missing VRAM content service types, missing `LockstepPlayerKinematicState`, and missing `InteractionUiSignal`.
Rejected Alternatives: Editing unrelated Crest/package/world/core/terminal/bulkhead dependencies to force a green build. That is outside Radiation Scrubber authority and would risk architectural sabotage.
Scalability potential: No runtime scalability impact; compile proof remains blocked until integrator restores stale/missing dependencies.
Hardware Impact: Build used single MSBuild worker. `VBCSCompiler.exe` remained active after the failed build, so no further compile retries were launched.

Problem: Loop 8 introduced owner-route corrections after the prior compile wall, but the repository build gate was closed again.
Solution: Probed CPU/compiler state before any retry. CPU first measured 91 percent with known missing dependency files still absent; later probe measured CPU 100 percent with active `dotnet.exe` and `csc.exe`. No SHINOBU_274 build retry was launched.
Rejected Alternatives: Launching a second build under active compiler load or editing unrelated Crest/world/core dependencies. Both violate the local build gate and domain boundary.
Scalability potential: No runtime scalability impact; this is verification discipline.
Hardware Impact: Avoided competing with an active compiler and saturated CPU.

## 2026-05-21 Loop 8 - Owner-Route Correction

Problem: Static audit after compile-wall classification still found direct radiation fatigue mutation in `RandomEventSystem.ApplySolarFlareRadiation` and `TraumaDispatcher.UpdateRadiationFatigue`, plus a future regression lane where direct `HazardZoneManager.RegisterZone(... HazardType.Radiation)` could create old-style radiation volumes.
Solution: Solar flare and radioactive clarity trauma now publish external dose through `RadiationHazardGrid.ReportExternalDose` using player AUP when available. `TraumaDispatcher` no longer stores a local radiation exposure accumulator and no longer clears or mutates `HectonPlayerHealth` radiation fatigue. `HazardZoneManager` validates radiation input and short-circuits radiation registrations to `RadiationHazardGrid.RegisterSource`; unregister also removes matching radiation sources.
Rejected Alternatives: Letting event systems mutate `HectonPlayerHealth` directly was rejected because it creates a second dose owner and bypasses Voxel SDF/source telemetry. Keeping direct radiation volumes was rejected because it preserves trigger-era presentation state as simulation truth.
Scalability potential: Low devices still pay one SignalBus dose scalar for global solar/clarity radiation and keep sparse Burst cadence. Middle/High/Ultra keep the same authority path while GPU mutation/static scalars can become more visible without changing gameplay truth or DTO layout.
Hardware Impact: Removes redundant fatigue updates and legacy volume evaluation for radiation. Estimated 8-25 us/frame saved during active solar/radioactive trauma windows on i3/MX350-class CPU; more importantly, it eliminates conflicting health writes under rollback.

Problem: Atmospheric survival radiation still entered the accumulator through a runtime `Vector3` fallback and nearby survival helpers still polled `GlobalRegistry.Player` in hot accessors.
Solution: Added `TryResolveSurvivalAup` and routed atmospheric dose to `RadiationHazardGrid.ReportExternalDose(... in AbsoluteUniversePosition)`. Survival AUP fallback now uses cached `_runtimeContext` instead of `GlobalRegistry.Player`.
Rejected Alternatives: Keeping runtime `Vector3` for radiation dose was rejected because it reintroduces precision dependence on floating-origin conversion. Hot registry fallback was rejected because player context is already cached at owner bind.
Scalability potential: Same external dose scalar at all tiers; low devices avoid extra registry lookup, high tiers keep exact AUP for visual/static correlation.
Hardware Impact: Removes one hot registry lookup fallback and preserves double-AUP correctness; microsecond gain is sub-1 us normally, but correctness prevents far-origin dose mismatch.

Problem: Diffusion grid read/write swaps were lost when Vault views were refreshed, and forced/external ticks integrated at least one full quality interval even when frame delta was smaller.
Solution: Added `_gridBuffersSwapped` parity so `RefreshVaultViews` maps Vault read/write handles to the current front/back buffers after diffusion completion. Fixed `ShouldEvaluateRadiationThisTick` to integrate the actual accumulated seconds rather than clamping every forced tick up to `tickInterval`.
Rejected Alternatives: Copying the write grid back into the read grid was rejected because it burns memory bandwidth across the full grid. Leaving clamped forced dt was rejected because external dose could be over-integrated during high frame rates.
Scalability potential: Low quality keeps long intervals without dose inflation. High/Ultra can run near-frame cadence with stable front/back grid parity and no extra grid copy.
Hardware Impact: Avoids a full grid copy after diffusion; estimated 15-60 us saved on mobile/low-end integrated memory depending on grid residency. Correct cadence prevents false radiation spikes that would drive unnecessary shader/health churn.

## 2026-05-21 Loop 9 - Exact Dose and Concurrency Guard

Problem: External dose signals carried exact rads, but the first integration path also fed external intensity into `CalculateRadiationExposureJob` as exposure rate, causing atmospheric/solar/trauma dose to be counted once as exact dose and a second time as `intensity * dt`.
Solution: Added `_pendingExternalDoseRad` and `ExternalDoseDelta`. `DrainExternalDoseSignals` now accumulates exact rads into the pending lane; the Burst job adds that exact dose once while external intensity contributes only to current exposure/visual severity. Iodine reductions subtract pending dose before accumulated dose.
Rejected Alternatives: Treating external radiation as only a rate was rejected because `RandomEventSystem`, `HectonSurvivalSystem`, and `TraumaDispatcher` already compute exact scaled dose before publishing. Keeping both exact dose and rate integration was rejected because it double-counts.
Scalability potential: Low/Middle/High/Ultra keep the same exact dose truth. Quality can still change cadence and visuals without changing total external rads.
Hardware Impact: Removes false dose inflation that would trigger unnecessary health/shader churn. Direct CPU gain is small, but it prevents runaway visual/telemetry work under solar flare windows.

Problem: Source and dose signal drains could mutate Vault source/state lanes while a previous radiation job was still active, and grid rebuild could write read/source buffers while diffusion was still reading them.
Solution: `ScheduleRadiationSimulation` now returns without drains while `_radiationSimulationJobActive` is true, preserving the pending evaluation marker until PostSimulation finalizes. `RebuildSourceGrid` and new diffusion scheduling run only when no diffusion job is active.
Rejected Alternatives: Forcing `Complete()` in Simulation or copying grids defensively. Both violate dispatcher-owned completion windows or burn full-grid memory bandwidth.
Scalability potential: Low devices avoid contention under long frames; high-tier jobs still chain normally and spend saved bandwidth on SDF/bulkhead samples.
Hardware Impact: Prevents race-driven cache corruption and avoids a defensive full-grid copy; estimated 15-60 us protected on low-end UMA during dense source fields.

Problem: `PHYSICS_OPTIMIZATION_REPORT.json` had SHINOBU_274 proof fields that the editor scanner would not reproduce on the next run.
Solution: Updated `RadiationTriggerDebtScanner.WriteReport()` to emit status, scope note, dispatcher route, Vault buffers, shader warmup, owner-route correction, and grid/cadence correction fields. Added SHINOBU_274 to the binary payload ledger.
Rejected Alternatives: Keeping manual JSON-only proof. Reproducible scanner output is required for integration evidence.
Scalability potential: No runtime impact; prevents proof drift across low/high-tier validation runs.
Hardware Impact: Editor-only.

## 2026-05-21 Loop 10 - Radiation Read Route and Artifact Consistency

Problem: Registration paths for `HazardType.Radiation` were sealed into `RadiationHazardGrid`, but read paths through `HectonHazardManager.GetHazardIntensity(... HazardType.Radiation)` still resolved `HazardZoneManager`. `FloraRegrowthDirector` consumed that compatibility read and could observe stale legacy hazard truth.
Solution: Added an AUP overload for `RadiationHazardGrid.TrySampleRadiationIntensity01` and special-cased all `HectonHazardManager.GetHazardIntensity` radiation overloads to sample the grid owner directly. Non-radiation hazards still use `HazardZoneManager`.
Rejected Alternatives: Editing `FloraRegrowthDirector` only was rejected because the compatibility bridge would remain a route leak for the next caller. Restoring radiation volumes in `HazardZoneManager` was rejected because it contradicts the source/DataVault owner model.
Scalability potential: Low devices pay one grid/source sample for radiation flora influence and avoid legacy zone lookup. Middle/High/Ultra preserve the same gameplay truth while shader/static visuals continue scaling from `GlobalQualityWeight`.
Hardware Impact: Removes one hazard-zone manager lookup and legacy volume iteration from radiation reads; estimated 2-12 us per broad radiation query depending on active generic hazard count.

Problem: The local `RadiationHazardGrid` damage metadata constant used wording that looked like a SignalBus lane ID, while the value is only `CombatDamageSignal.SourceId` metadata.
Solution: Renamed only the local combat metadata constant to `RadiationCombatSourceId`; generated `H8Hashes.RadiationSourceSignalId` remains the signal-name hash.
Rejected Alternatives: Renaming generated hash contracts or reusing the ambiguous local name with comments. Signal-name hashes and combat metadata must stay distinguishable.
Scalability potential: No runtime effect; reduces integration error surface across quality tiers.
Hardware Impact: 0 us/frame; naming-only safety correction.

Problem: The checked-in physics optimization report emitted three representative trigger findings but kept `finding_count` at the broad scanner count of 80, making the proof artifact internally inconsistent.
Solution: `finding_count` now equals the emitted list length, `broad_static_finding_count` preserves the total scanner surface, and the editor scanner caps emitted findings consistently while still reporting the broad count.
Rejected Alternatives: Expanding the shared report to all 80 generic findings. That would bloat the cross-agent artifact and obscure the radiation authority proof.
Scalability potential: No runtime impact; keeps validation cheap and readable on low-end editor machines.
Hardware Impact: Editor-only; avoids unnecessary JSON/log churn.

Problem: `PopulateSaveData` forced radiation/diffusion job completion before serializing even though save can use the last completed state and the active diffusion job writes the back buffer, not the read grid being encoded.
Solution: Save now calls only `CompleteDiffusionJobIfReady()` and serializes the current completed read buffer and `_accumulatedRadiationDose`. At Loop 10 the remaining forced completion was limited to load, hot-swap, and disposal; Loop 12 superseded this and moved live load/hot-swap to deferred PostSimulation fences, leaving force-complete for teardown release only.
Rejected Alternatives: Keeping force-complete for every save was rejected because user-visible saves can happen during gameplay and should not create a same-frame dispatcher stall. Encoding the write buffer was rejected because it could be owned by a live diffusion job.
Scalability potential: Low devices avoid a save hitch during long diffusion windows. Middle/High/Ultra keep identical saved gameplay truth from the last completed authoritative state.
Hardware Impact: Avoids a possible 15-60 us save-frame stall on low-end UMA when diffusion is active; no steady-state runtime cost.

## 2026-05-21 Loop 11 - Signal Snapshot Preservation Under Live Jobs

Problem: When a previous radiation job stayed active across the next Simulation phase, `ScheduleRadiationSimulation` returned before draining `RadiationSourceSignal`, `RadiationDoseSignal`, and iodine item snapshots. SignalBus clears snapshots in PostSimulation, so radiation source updates, exact external dose, and iodine treatment events could be lost without any compile error.
Solution: While `_radiationSimulationJobActive` is true, the owner phase now requeues radiation source signals into the typed `SignalBus<RadiationSourceSignal>` for the next PreSimulation flush, drains exact external dose into `_pendingExternalDoseRad`, and converts iodine item events into `_pendingIodineDoseReductionRad`. No live source/state buffer is mutated and no job is force-completed.
Rejected Alternatives: Forcing completion to make drains safe was rejected because it violates dispatcher-owned completion windows. Mutating `_sources` while the job can read it was rejected because unsafe pointer jobs bypass AtomicSafetyHandle protection. Dropping the signals was rejected because source and dose are gameplay truth.
Scalability potential: Low quality and saturated frames can stretch jobs across frame boundaries without losing radiation facts. Middle/High/Ultra keep the same authority route; quality still changes only cadence/sample budgets/visuals.
Hardware Impact: Prevents event loss under low-end CPU stalls without adding a main-thread wait. Requeue cost is bounded by the 64-signal lane and estimated under 3 us in source-storm frames; normal frames pay 0 us.

Problem: The read-only compatibility sampler could read `_sources` while a radiation job was active. The active handle may include a mock-source writer followed by the exposure reader, and those jobs use unsafe pointers rather than Unity safety handles.
Solution: `TrySampleRadiationIntensity01(in AUP)` now reads only the stable diffusion/read grid while a radiation job is active; inverse-square source sampling resumes after PostSimulation finalizes the job.
Rejected Alternatives: Completing the job inside a read accessor was rejected by the Global Systems Doctrine. Reading the source array concurrently was rejected because it can race the emergency mock source writer.
Scalability potential: Low devices prefer stale-but-safe read-grid snapshots during long jobs; high-tier devices usually finalize within the dispatcher window and keep inverse-square source read precision.
Hardware Impact: Avoids a race without adding a stall; low-tier source-read queries may be cheaper during live jobs because they skip the 64-source loop.

## 2026-05-21 Loop 12 - Runtime Route and Tooling Audit Closure

Problem: Read-only audit found that `HazardZoneManager` still exposed radiation reads through the legacy volume interface and could store radiation job output in generic hazard exposure caches.
Solution: `HazardZoneManager.GetHazardIntensity(... Radiation)` delegates directly to `RadiationHazardGrid.TrySampleRadiationIntensity01`; completed generic hazard jobs zero player/vehicle radiation intensity and glitch slots and publish only non-radiation exposure masks.
Rejected Alternatives: Trusting the `HectonHazardManager` bridge alone was rejected because `IHazardZoneReadModel` remains reachable by other systems. Keeping radiation cache values as "debug only" was rejected because cached facts become shadow authority under pressure.
Scalability potential: Low devices avoid generic volume lookup for radiation reads. Middle/High/Ultra keep one gameplay truth while shader radiation visuals continue to scale from `GlobalQualityWeight`.
Hardware Impact: Removes legacy volume traversal from direct hazard read-model radiation queries; estimate remains 2-12 us per broad query depending on generic hazard count.

Problem: Generic hazard unregister paths could emit `RadiationHazardGrid.UnregisterSource(id)` for non-radiation hazard IDs, allowing ID collision to remove a real radiation source.
Solution: Generic `HectonHazardManager.Unregister(int)` now unregisters only generic volumes. Type-aware unregister exists for radiation. `HectonHazardSource` and `EnvironmentalHazard` track local radiation registration before emitting remove signals.
Rejected Alternatives: Leaving unconditional radiation remove as cleanup was rejected because cleanup without ownership proof violates one fact -> one owner.
Scalability potential: No quality-tier behavioral drift; source ownership is stable across weak and high-end devices.
Hardware Impact: 0 steady-state us; prevents rare but severe source deletion under pooled or reused IDs.

Problem: `LoadFromSaveData` and DataVault hot-swap still force-completed active radiation jobs, which violates dispatcher-owned completion windows during gameplay.
Solution: Live load and DataVault replacement now queue pending structural operations and apply them in PostSimulation only when no radiation/diffusion job is active. Force-complete remains only in teardown release.
Rejected Alternatives: Copying all grid buffers defensively or blocking in load/hot-swap. The first wastes memory bandwidth; the second stalls the frame and breaks dispatcher discipline.
Scalability potential: Low devices can survive long radiation/diffusion frames without a load/hot-swap stall. High/Ultra retain the same state route and just resolve the deferred operation sooner.
Hardware Impact: Avoids forced worker wait during live load/hot-swap; estimated 15-60 us hitch avoided on low-end UMA when diffusion is active.

Problem: The editor scanner wrote the shared cross-agent physics report directly, counted comment/string hits, and the tuner read state slot zero rather than the 300-frame telemetry ring requested by the XML.
Solution: Scanner output now targets `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_274.json`, keeps a shared-report pointer, sorts deterministically, and masks comments/strings before counting trigger tokens. The tuner reads `Shinobu274RadiationTelemetryRing` plus `Shinobu274RadiationTelemetryCursor` and displays the latest ring entry.
Rejected Alternatives: Continuing to overwrite `PHYSICS_OPTIMIZATION_REPORT.json` was rejected because it can erase other agents' sections. Reading `RadiationStateDTO` was rejected because Task 16 specifically requires telemetry inspection.
Scalability potential: Editor-only; no runtime tier effect. The dedicated report keeps low-end editor validation smaller and deterministic.
Hardware Impact: Editor-only; scanner mirror reports 1666 scanned files, 532 ignored editor files, 220 candidates, and 78 broad findings after comment/string masking.

## 2026-05-21 Loop 13 - Runtime Race and Tooling Drift Audit

Problem: Radiation dose math still trusted profile/source/SDF/bulkhead scalar inputs enough that a corrupt CSV row, stale SDF descriptor, or non-finite source could propagate NaN through inverse-square dose and shader mutation scalars.
Solution: `CalculateRadiationExposureJob` now sanitizes non-negative tuning values, 0..1 shield effectiveness, simulation tick delta, external dose delta, previous accumulated dose, source AUP/intensity/radius, double-distance deltas, SDF origin/cell/range, and bulkhead plane/segment widths before reciprocal or SDF sampling.
Rejected Alternatives: Relying on content validation or Unity editor import checks was rejected because runtime Vault data can be restored from save, hot-reloaded, or produced by another owner phase. A single bad lane must fail closed inside the Burst kernel.
Scalability potential: Low devices collapse to the same cheap finite-safe inverse-square/SDF sample path; Middle/High/Ultra can increase SDF samples without changing NaN policy or DTO layout.
Hardware Impact: Adds a small number of branchless finite/clamp checks in the dose kernel, estimated under 2 us/frame at 64 sources, and prevents catastrophic NaN propagation into shader globals and rollback state.

Problem: `HazardZoneManager` could receive a DataVault service replacement while its generic hazard exposure job was still reading the Vault-owned result buffer. The old hot-swap path attempted release/rebind immediately, which could tombstone the handle before the worker completed.
Solution: `OnGlobalRegistryServiceReplaced` records `_pendingDataVault` while `_jobRunning`; `ConsumeCompletedJob` applies the swap only after `DispatcherJobSwap.TryFinalizeCompleted`. `DisposeNativeState` force-completes only in teardown before releasing the owned result buffer.
Rejected Alternatives: Completing the job inside the hot-swap callback was rejected because live service replacement is not a dispatcher completion window. Leaving the immediate release was rejected because handle ownership would be lost under an active unsafe job pointer.
Scalability potential: Low devices with long worker frames defer the swap without a stall; Middle/High/Ultra typically finalize sooner but follow the same route.
Hardware Impact: Avoids a 15-60 us live-frame stall and prevents a rare Vault result leak or dangling handle under saturated CPU.

Problem: Loop 12 removed unconditional radiation unregister from `HectonHazardManager.Unregister(int)`, but legacy callers that registered radiation through the untyped facade could then leak their source if they also unregistered through the untyped facade.
Solution: Added a fixed cold `int[1024]` owner table for untyped radiation facade IDs. Radiation facade registration tracks the ID; untyped unregister removes radiation only when that exact ID is tracked. Type-aware radiation unregister remains direct and also clears the table.
Rejected Alternatives: Restoring unconditional radiation unregister was rejected because non-radiation ID collisions could delete an unrelated radiation source. A managed `HashSet<int>` was rejected because this bridge must stay allocation-free after cold type initialization.
Scalability potential: Low/Middle/High/Ultra share the same compatibility route; quality never changes ownership or source identity.
Hardware Impact: O(n) scan over a 1024-entry cold table only on register/unregister, not per frame. Runtime frame cost is 0 us; it prevents persistent source leaks from old untyped callers.

Problem: The editor scanner still had report-path coupling and generator drift: one private class owned the paths, the scanner could not reuse them without access errors, domain filtering happened before the comment/string mask, and generated JSON lacked the microsecond estimate block required by the report.
Solution: Introduced `RadiationShieldingReportPaths` as the shared editor-only path owner, moved ignore-mask construction before domain filtering, replaced raw token domain filtering with `ContainsAnyUnignored`, and added `microseconds_saved_estimate` to the generated report payload.
Rejected Alternatives: Keeping manual checked-in JSON as the only proof was rejected because the editor scanner must reproduce the same evidence. Making the path constants public on the window was rejected because the scanner is the report writer, not a UI child of the tuner.
Scalability potential: Editor-only. Smaller deterministic output keeps low-end editor validation cheaper, while high-tier validation can still inspect the broad static count.
Hardware Impact: Editor-only; no player frame cost. Static scanner remains bounded by file text scan and top-three finding emission.

## 2026-05-21 Loop 14 - Fail-Closed Sampler and Compatibility Audit

Problem: Subagent runtime audit found that the read-only radiation compatibility sampler and save/load lane could still accept non-finite state outside the already-hardened Burst kernel.
Solution: `SampleGridNearest` now fails closed on non-finite grid cells; `SampleInverseSquare` skips non-finite source AUP/intensity/radius and guards squared distance before reciprocal math. Save/load clamps dose and grid cell size through explicit finite checks. Source registration rejects non-finite AUP and inactive/zero-intensity sources before touching grid state.
Rejected Alternatives: Trusting the Burst kernel alone or relying on content import validation. Read accessors and save hydration are independent ingress points and must not be able to poison health, shader globals, or telemetry.
Scalability potential: Low devices get the same cheap fail-closed nearest/source sampler; Middle/High/Ultra can still raise source counts and SDF/bulkhead samples without changing the safety policy.
Hardware Impact: Adds only bounded scalar finite checks in read/save lanes, estimated under 1 us per compatibility sample, and prevents unbounded NaN propagation cost.

Problem: Stale naming and visual/health scalar lanes still allowed policy drift: `doseScalePerFrostTick` contradicted the Simulation phase route, and raw dose values could flow into player context or shader globals if corrupted before presentation.
Solution: Renamed the serialized field to `doseScalePerSimulationSecond` with `FormerlySerializedAs("doseScalePerFrostTick")` to preserve existing serialized data. Player context and shader global dose now finite-guard before fatigue and GPU mutation scalar calculation.
Rejected Alternatives: Breaking serialized scenes/prefabs with a raw rename or leaving the FrostTick name as documentation debt. The route is Simulation seconds, not maintenance tick cadence.
Scalability potential: The scalar route remains continuous across quality tiers; only the sanitized dose value feeds health and UberNoir mutation strength.
Hardware Impact: No measurable frame cost; avoids false shader/health escalation from corrupt persisted dose.

Problem: Compatibility bridge audit found generic hazard state still used `FloatMode.Fast` and the step loop called a cold resolver that could fall back to `GlobalRegistry.Player`.
Solution: `EvaluateHazardExposureJob` now uses deterministic Burst mode. `AdvanceHazardStep` uses `RefreshPlayerContextSnapshot`, which reads the runtime context snapshot and cached references only; the old full resolver remains for Awake/OnEnable cold binding.
Rejected Alternatives: Treating heat/toxicity/biohazard as pure presentation was rejected because these values can affect survival/trauma state. Keeping the cold resolver in the step loop was rejected because registry fallback belongs to bootstrap/hot-swap, not runtime cadence.
Scalability potential: Low devices avoid registry fallback and scene search in the runtime loop; Middle/High/Ultra keep deterministic generic hazard exposure while radiation remains routed to the RadiationHazardGrid owner.
Hardware Impact: Removes a hot cold-path branch and possible registry access from each hazard step; estimate 1-4 us per step under active hazard manager on low-end CPU.

Problem: Tooling proof drift remained between the editor scanner generator, dedicated SHINOBU_274 JSON report, shared physics report, and early rationale text.
Solution: Aligned `finding_list_policy` text across generator, dedicated report, and shared report. Corrected the early rationale entry to state that the scanner writes the dedicated report and preserves the shared aggregate/pointer.
Rejected Alternatives: Allowing the next scanner run to rewrite evidence text again. Deterministic proof artifacts must be reproducible.
Scalability potential: Editor-only; no runtime tier effect.
Hardware Impact: Editor-only.
