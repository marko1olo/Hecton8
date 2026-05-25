# Rationale_SHINOBU_226

Status: IMPLEMENTED_STATIC_VERIFIED_RESIDUAL_AUDIT_FINDINGS_LOOP17

Problem: XML assignment has 19 explicit task nodes because Task 09 is absent.
Solution: Track 19 concrete tasks and record the numbering gap instead of inventing Task 09.
Rejected Alternatives: Inventing a missing task would violate strict parsing and cross-agent boundary discipline.
Scalability potential: No runtime effect; prevents scope creep that would waste engineer time and create merge risk.
Hardware Impact: 0 us runtime; avoids unnecessary source churn on low-end i3/MX350 and high-end machines alike.

Problem: Scanner/lore sync belongs to Echelon 8 Presentation & UX but touches Tools, UI, DataVault, shader presentation, and static data.
Solution: Keep authoritative scan state as owner-local Vault-backed DTOs with cold bootstrap dependency caching and method-local resolves; communicate with neighboring systems only through contracts/Vault/shader scalar surfaces.
Rejected Alternatives: Direct references to concrete tool/PDA/DataMonolith runtime classes, per-target MonoBehaviour state, or managed event/string routes.
Scalability potential: Low uses the same integer state and cheap shader scalar; middle/high/ultra can spend saved CPU on denser diegetic visual noise without changing scan truth.
Hardware Impact: Expected CPU saving from replacing managed string lookup/UI unlock paths with uint hash and bitmask writes is bounded in single-digit microseconds per scan tick; measured proof absent.

Problem: Existing scanner router stored legacy pointer-bearing `VaultBufferHandle<T>` fields, which violates current Vault ledger guidance and can retain stale pointers across compaction generations.
Solution: Replace persistent handles with `VaultGenerationHandle<T>` descriptors and resolve phase-local `NativeArray<T>` views inside `TryResolveVaultViews`.
Rejected Alternatives: Keeping `VaultBufferHandle<T>` as a migration bridge; storing `NativeArray<T>` fields; direct GlobalRegistry lookups inside FastTick resolution.
Scalability potential: Low/middle/high/ultra all use the same pointer-free descriptor path; compaction and low-memory defrag can proceed without stale scanner-owned views.
Hardware Impact: Avoids stale pointer guard faults and defensive pointer refresh work; expected saving is small per tick but removes a failure mode on i3/MX350 and Quest-class ARM64.

Problem: Scan completion still needed a hash-to-lore unlock path that does not route through managed strings or PDA object state.
Solution: Add `ScanProgressDTO`, `ScannerLoreIndexDTO`, `ScannerEncyclopediaStateDTO`, plus `UpdateScanProgressJob` and `EvaluateScanCompletionJob`; completion uses FNV-1a uint keys and atomic OR into a 128-byte bitmask.
Rejected Alternatives: `Dictionary<string,int>`, `target.name`, `GetComponent<ItemData>`, PDA concrete method calls, or managed events carrying titles.
Scalability potential: Low uses the same O(1) native bit write; higher tiers spend saved CPU on shader scanner noise and refresh, not on authoritative unlock logic.
Hardware Impact: Replaces managed lookup/string compare with one bounded native hash probe and one atomic OR; expected saving 2-8 us per completion on low-end silicon, depending on prior UI path pressure.

Problem: Binary tier cadence created a quality switch instead of a continuous hardware response.
Solution: Resolve query cadence from `GlobalQualityWeight` using a smoothstep polynomial and pressure multiplier; scanner HUD globals carry progress, quality, refresh Hz, and dither complexity.
Rejected Alternatives: `if (IsLowEndHardware)`, discrete Low/Mid/High cadence branches, or CPU-side scanner HUD mesh simulation.
Scalability potential: q=0.1 collapses to low refresh/cheap dither; q=0.4-0.7 interpolates cadence and visual density; q=1.0 unlocks full shader refresh/visual overkill.
Hardware Impact: At thermal pressure, query cadence can stretch up to 3x smoothly, protecting i3/MX350/Quest frame time while preserving high-tier visual richness.

Problem: ARM64 layout proof was implicit and the prompt demanded exact byte offsets.
Solution: Convert scanner DTOs to explicit layouts where applicable and add `ValidateScanProgressLayout` plus tests for size and offsets.
Rejected Alternatives: Relying on sequential layout and comments; `[StructLayout(Pack=1)]`, which would risk unaligned ARM64 accesses.
Scalability potential: Layout stability benefits all tiers and rollback snapshots equally.
Hardware Impact: `ScanProgressDTO` is one 64-byte cache line; `ScannerEncyclopediaStateDTO` is 128 bytes of aligned ulong mask words, avoiding unaligned fetch penalties.

Problem: Static verification and editor control were missing for the scanner/lore sync slice.
Solution: Add `ScannerLoreDatabaseSyncTunerWindow` and `ScannerStringInquisitionValidator`; add `SHINOBU_226_SELF_AUDIT.xml` and route card documentation.
Rejected Alternatives: Chat-only reporting or relying on manual grep outside the repo.
Scalability potential: No runtime cost; designers can simulate hash unlocks without recompiling.
Hardware Impact: 0 us runtime; editor-only validation prevents expensive managed hot-path regressions.

Problem: Compile verification was requested but local CPU gate rejected build execution.
Solution: Check `dotnet/csc` process state and CPU load before build; CPU averaged 100%, so no dotnet build was launched.
Rejected Alternatives: Violating the project build gate to force a compile under load.
Scalability potential: Protects developer workstation responsiveness during 20+ agent parallel work.
Hardware Impact: Avoided additional compilation load while host was already saturated.

Problem: Loop 5 treated Task 18 as satisfied by tuner/shader visibility, but the XML explicitly demands an `OnDrawGizmos` hook over Vault scannable rows.
Solution: Add editor-only `ScannerDataMiningRouter.OnDrawGizmos` that resolves current Vault descriptors, reads `ScannerSpatialEntityDTO`, `ScannerLoreIndexDTO`, `ScannerEncyclopediaStateDTO`, and `ActiveScanStateDTO`, then draws blue/yellow/green wire spheres after AUP-local conversion.
Rejected Alternatives: Canvas debug labels, per-target GameObjects, runtime text overlays, or relying only on shader globals.
Scalability potential: Low/middle/high/ultra runtime is unchanged; editor scene debug becomes direct proof of hash/bitmask state without adding gameplay work.
Hardware Impact: 0 us player runtime. Editor-only draw avoids any mobile/i3/MX350 cost and prevents string debug routes from entering the scanner hot path.

Problem: Task 16 required direct designer controls for Unlock All and Lock All, not only a simulated single-hash unlock.
Solution: Extend `ScannerLoreDatabaseSyncTunerWindow` with Vault readout plus buttons that write `ulong.MaxValue` or `0` to all 16 mask words in `ScannerEncyclopediaStateDTO`.
Rejected Alternatives: Recompiling constants, managed PDA calls, or string-key unlock commands.
Scalability potential: No runtime effect; designers can validate low/mid/high/ultra scanner presentation from the same authoritative bitmask.
Hardware Impact: 0 us player runtime; removes manual playthrough setup cost in editor.

Problem: Loop 6 still needed compile discipline after source edits.
Solution: Re-ran scoped static scans and CPU/dotnet gates. Forbidden scanner/PDA string/GetComponent scan returned 0 hits; runtime scanner hot-path scan returned 0 hits for legacy Vault handles, raw `Complete`, Unity random/time, hot private native owners, foreach/LINQ/split/string.Format, and Pack=1. CPU samples returned 91 then 100, so build remains blocked.
Rejected Alternatives: Launching dotnet under the explicit CPU gate or claiming Unity import proof from static scans.
Scalability potential: Protects workstation load during parallel agent execution.
Hardware Impact: Avoided unnecessary compiler CPU/IO pressure while host load was already above threshold.

Problem: Scanner runtime still sampled Unity `Time.frameCount` directly for query cadence, signals, VFX frame stamps, telemetry, and anomaly reports.
Solution: Added a scanner-local frame resolver that reads `TimeSliceScheduler.CurrentFrameId`, the dispatcher-owned frame route already begun from master dispatcher timing, and replaced every scanner-domain direct frame-count call.
Rejected Alternatives: Keeping direct Unity frame reads, adding a scanner-owned frame counter, or editing `SystemDispatcher` core to expose another wrapper.
Scalability potential: Low/middle/high/ultra all share one dispatcher frame fact; no shadow timing owner is introduced, and scanner presentation remains free to scale through `GlobalQualityWeight`.
Hardware Impact: 0 us raw speed gain. Removes a rollback/determinism edge without adding per-frame allocation or cross-domain dependency churn.

Problem: Compile verification became temporarily legal when CPU sampled 19, then illegal again before build launch.
Solution: Rechecked `dotnet/csc` and CPU immediately before build; CPU resampled at 100, 80, 75, 100, 51, then 70, so no dotnet command was launched.
Rejected Alternatives: Starting build from a stale low CPU sample or ignoring the project rule forbidding builds above 50 percent CPU.
Scalability potential: Protects iteration bandwidth while 20+ agents may be active.
Hardware Impact: Avoided compiler CPU/IO pressure on a saturated host; no runtime effect.

Problem: Scanner query construction still read Unity `Transform` pose data directly, and legacy scanner/PDA bridge files still stamped events with `Time.frameCount`.
Solution: Build active scanner rays from cached `PlayerRuntimePoseSnapshot` AUP/forward fields, reject non-finite or near-zero forward vectors before normalization, route mock seed fallback through cached player AUP or global AUP only, and replace scanner/PDA frame stamps with `TimeSliceScheduler.CurrentFrameId`.
Rejected Alternatives: Keeping `transform.forward/position/right` in scanner query construction, inventing a default gaze for gameplay acquisition, or editing core dispatcher APIs.
Scalability potential: Low/middle/high/ultra all share one pose/frame authority route; mock seed data remains deterministic for CI/editor without scene object pose dependence.
Hardware Impact: Avoids Unity native Transform property bridge work per scanner query and removes one more rollback drift edge. Raw speed gain is small and unmeasured; correctness and authority isolation are the primary gains.

Problem: The scanner string inquisition only guarded old string/GetComponent mistakes and would not catch future Unity time/random or router Transform pose regressions.
Solution: Extend the editor validator with `Time.frameCount`, `Time.deltaTime`, `UnityEngine.Random`, and router-scoped Transform pose patterns.
Rejected Alternatives: Leaving the validator narrow, or applying Transform bans to every editor UI gizmo and creating false positives outside scanner runtime acquisition.
Scalability potential: No runtime cost; prevents future hot-path regressions from consuming low-end silicon budget.
Hardware Impact: 0 us runtime. Static guard protects the saved scanner query budget on i3/MX350/Quest-class hardware.

Problem: Loop 8 compile verification remained gated after scanner/PDA source edits.
Solution: Re-ran scoped static scans and `git diff --check`; then checked `dotnet/csc` and CPU. Compiler processes were absent, but CPU sampled 71 then 85, so no build command was launched.
Rejected Alternatives: Starting a build above the explicit 50 percent CPU gate.
Scalability potential: Protects local iteration while multiple agents are active.
Hardware Impact: Avoided additional compiler load on a busy host; no runtime effect.

Problem: Compile gate later opened, but the generated project coverage and dependency graph were not clean.
Solution: Verified generated csproj coverage, then ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` when CPU sampled 34/25/19 and no compiler process was active. Build failed with 76 unrelated dependency-wall errors before a clean project build could be proven.
Rejected Alternatives: Claiming Unity import proof from stale csproj coverage, editing unrelated Equipment/Logistics/Docking/Audio/World bridge dependencies, or retrying blindly after a compile-wall failure.
Scalability potential: No runtime effect; keeps this agent confined to scanner/lore sync rather than taking ownership of sibling dependency repair.
Hardware Impact: One guarded build attempt consumed local compile time; no SHINOBU_226-touched file appeared in the reported diagnostics.

Problem: The failed Core build left idle dotnet build-server processes resident.
Solution: Ran `dotnet build-server shutdown` and rechecked process state; `Get-Process dotnet,csc` returned no process output.
Rejected Alternatives: Leaving stale compiler workers alive or killing arbitrary process IDs that could belong to another agent.
Scalability potential: No runtime effect; restores compile-gate hygiene for subsequent agents.
Hardware Impact: Frees build-server resident process overhead after the failed guarded attempt.

Problem: Lore entity scanner synchronization still rebuilt AUP facts from presentation `Transform.position` and `GlobalSignals.CurrentRuntimeOriginAup`, creating a second owner for scanner lore location.
Solution: Moved lore AUP publication to explicit owner-phase calls and changed read access to `TryReadLoreEntityBuffers`, which resolves Vault generation handles without syncing. Added `WorldSpatialHashGrid.TryGetAbsolutePosition` so scanner lore sync reads the spatial owner AUP already registered by the entity.
Rejected Alternatives: Continuing Transform-derived AUP reconstruction, querying GlobalRegistry during candidate evaluation, or adding a scanner-owned position mirror.
Scalability potential: Low/middle/high/ultra all consume the same spatial-owner location fact. Visual scan richness can scale, but lore identity and position authority remain invariant.
Hardware Impact: Avoids Unity Transform bridge and double-precision origin rebuild per lore slot; expected raw saving is sub-1 us to low-single-digit us under candidate-heavy scans, with correctness as the primary gain.

Problem: PDA active UTF8 caching stored a pointer obtained inside a `fixed` block after the block ended, leaving a stale/movable pointer risk.
Solution: `CacheActiveSource` now records only source byte count and source flags; `SelectActiveUtf8Source` reacquires the current span from H8LR, Babel, or mock source each render pass.
Rejected Alternatives: Retaining the pointer field as a fast path, copying text into a managed string cache, or pinning managed arrays across frames.
Scalability potential: Low devices avoid GC/pinning hazards; high/ultra still use the same zero-copy source selection and can spend CPU on richer PDA presentation.
Hardware Impact: 0 us raw speed gain; removes a memory safety failure mode that could corrupt PDA text on any tier.

Problem: Focused lore candidate selection used a one-slot `NativeArray<LoreCandidateResult>` and executed an `IJob` synchronously in the same frame, paying job overhead for tiny candidate work.
Solution: Removed `LoreCandidateDotProductJob` and the persistent result `NativeArray`; selection now uses a bounded scalar loop over Vault arrays with AUP-local camera-relative math and NaN-guarded `rsqrt`.
Rejected Alternatives: Keeping same-frame `job.Execute()`, scheduling then immediately reading back, or broadening the job despite candidate counts being small and data already resident.
Scalability potential: Low/middle devices skip job scheduling overhead; high/ultra can still scale visual scan refresh through `GlobalQualityWeight` while candidate truth remains deterministic.
Hardware Impact: Avoids one native allocation owner and per-resample job setup/readback overhead; estimated 3-15 us saved on low-end CPU paths when focused scan resamples.

Problem: Scanner hot lanes still had service lookup drift through audio/localization/scalability and survival discovery still used a Bootstrapper Transform plus `TryGetComponent` retry path.
Solution: Cached audio, localization, player, atlas, lore database, and survival references through cold/hot-swap lanes; survival reads now use `IPlayerRuntimeContext.SurvivalSystem`; SlowTick no longer polls `GlobalRegistry.ScalabilityTier`.
Rejected Alternatives: Polling GlobalRegistry every scanner tick, using `TryGetComponent` for player survival recovery, or embedding concrete sibling ownership in scanner code.
Scalability potential: Low/middle/high/ultra share the same cold dependency cache while scan cadence and shader presentation continue to scale from the continuous quality weight.
Hardware Impact: Estimated 1-3 us saved on ping/localized scanner paths under service lookup pressure; avoids scene/component bridge retry cost.

Problem: The scanner tool black box and PDA stream still retained legacy native ownership patterns: a private persistent `NativeArray<ScannerBlackBoxEntry>` and pointer-bearing `VaultBufferHandle<T>` descriptors.
Solution: Added `BufferID.ShinobuScannerToolBlackBox=70639`, changed the scanner black box to a `VaultGenerationHandle<ScannerBlackBoxEntry>`, and migrated PDA handles to pointer-free `VaultGenerationHandle<T>` with phase-local `ResolveVaultBuffer` / `GetVaultElementRef` helpers.
Rejected Alternatives: Keeping a special exception for the scanner black box, leaving PDA on obsolete `VaultBufferHandle<T>`, or copying PDA state into managed objects.
Scalability potential: Low/middle/high/ultra all use the same Vault ownership model; compaction and rollback snapshots no longer depend on scanner/PDA private native storage.
Hardware Impact: Raw speed gain is near-zero to 2 us; correctness gain is stale pointer elimination and safer low-memory compaction on constrained devices.

Problem: Loop 9 compile proof was requested after authority and memory-safety fixes.
Solution: Ran scoped static scans and `git diff --check`; checked compiler process state and CPU. `dotnet/csc` returned no visible process, but CPU sampled 100 twice after Loop 9 edits, so no build was launched.
Rejected Alternatives: Violating the explicit build gate under CPU >50 or retrying after the prior dependency-wall failure without a legal gate.
Scalability potential: Protects local iteration bandwidth with multiple active agents.
Hardware Impact: Avoided compiler CPU/IO load on a saturated host; no runtime effect.

Problem: Subagent audit found `ScannableTarget.WriteLoreEntitySlot` still synthesized a lore AUP from `GlobalSignals.CurrentRuntimeOriginAup()` when the spatial owner had no finite position.
Solution: Fail closed for that row by writing default AUP and zero hash, then returning before any entry hash publication. Lore position truth now comes only from `WorldSpatialHashGrid.TryGetAbsolutePosition`.
Rejected Alternatives: Keeping the origin fallback, preserving the last valid AUP without a stale flag, or creating a scanner-owned position mirror.
Scalability potential: Low/middle/high/ultra all see the same absence of a scannable row instead of a false origin contact; visual richness can still scale through scanner shader globals.
Hardware Impact: 0 us raw speed. Removes a correctness hazard that could create false scanner contacts at world origin on every hardware tier.

Problem: Scanner presentation/cooldown paths still read `Time.time` directly even after frame IDs moved to dispatcher state.
Solution: Added `ResolveScannerTimeSeconds()` backed by `SystemDispatcher.CurrentUnscaledTimeSeconds` and replaced direct `Time.time` uses in scanner cooldown, feedback gates, quality hysteresis, black-box timing, raycast response, and legacy operational text generation.
Rejected Alternatives: Keeping Unity time reads because they are presentation-only, adding another scanner-owned clock, or routing through managed stopwatch state.
Scalability potential: One timing route is used across weak, middle, high, and ultra machines; quality hysteresis still scales presentation but no longer owns an independent Unity clock read.
Hardware Impact: 0 us raw speed. Authority and rollback drift risk are reduced without adding allocation or scheduler work.

Problem: PDA encyclopedia mock/text paths still had same-frame job residue and an unused mock lookup result Vault lane after the scanner candidate job was removed.
Solution: Replaced PDA `IJob` structs with bounded scalar helpers, removed Burst/Jobs usings, and removed `_mockLookupResultHandle` plus `MockLookupResultBufferId`.
Rejected Alternatives: Keeping tiny jobs and `.Execute()` readbacks, scheduling and reading back in the same frame, or retaining a one-row result buffer for no remaining producer.
Scalability potential: Low hardware avoids job setup/readback overhead; high/ultra can spend the saved CPU budget on richer PDA presentation without changing text/unlock truth.
Hardware Impact: Estimated 2-10 us saved during mock lookup/typewriter paths under editor or fallback pressure; measured proof absent.

Problem: Subagent audit found `WorldSpatialHashGrid.TryScheduleFarUnload` and `BuildAcousticDensityMap` still poll `GlobalRegistry.Player` from recurring maintenance.
Solution: Recorded as out-of-domain handoff because those lanes belong to the world/spatial owner and are not required to complete the scanner/lore route. SHINOBU_226 touched `WorldSpatialHashGrid` only to add a pure AUP accessor for scanner consumption.
Rejected Alternatives: Expanding this agent into world maintenance ownership, changing player-context caching policy for all spatial grid consumers, or ignoring the finding.
Scalability potential: No scanner runtime change. The proper world-owner fix would cache/inject player AUP once per owner phase for far unload and acoustic density maintenance.
Hardware Impact: No direct SHINOBU_226 change. Residual world maintenance lookup cost remains external debt.

Problem: Loop 10 compile proof was requested after the subagent audit patch and timing route fix.
Solution: Re-ran scoped static scans and `git diff --check`; checked compiler process state and CPU. `Get-Process dotnet,csc` returned `NO_DOTNET_CSC`, but CPU sampled 100, so build stayed blocked by the explicit CPU gate.
Rejected Alternatives: Launching dotnet under CPU >50 or retrying the known dependency-wall compile without a legal gate.
Scalability potential: Protects local iteration bandwidth under multi-agent load.
Hardware Impact: Avoided compiler CPU/IO pressure on a saturated host; no runtime effect.

Problem: Subagent audit found legacy scanner discovery still published managed string metadata through scan pulse events.
Solution: Convert scannable, pickup, and module discovery calls to `ScanEvents.RaiseEntryDiscovered(uint, uint, uint, uint, ScanEntryKind)` only. Pickup/module IDs are hashed with a lower-ASCII prefixed FNV-1a helper without constructing `item.*` or `module.*` strings.
Rejected Alternatives: Keeping the string overload as a compatibility shortcut, building prefixed strings and hashing them, or adding a managed metadata dictionary beside the Vault bitmask.
Scalability potential: Low hardware avoids per-discovery string route pressure; middle/high/ultra spend the same saved budget on shader dither/scanline richness while lore truth remains a uint hash.
Hardware Impact: Estimated 4-20 us avoided per legacy discovery pulse depending on prior event subscribers and metadata path pressure; zero managed allocation on the scanner-owned discovery route.

Problem: Scanner source still carried unused dev/legacy managed formatting helpers including `string.Format`, `string.Create`, prefixed string caches, and module/pickup summary builders.
Solution: Remove the unused formatting chain and leave the operational HUD span writer as the active compatibility path.
Rejected Alternatives: Marking helpers editor-only, keeping them for future debugging, or adding validator exceptions for dead code.
Scalability potential: No direct low/mid/high/ultra runtime change for dead code, but the scanner source no longer contains a ready-made managed string path for future hot-route regressions.
Hardware Impact: 0 us measured hot-path saving because the removed chain was unused; removes a managed allocation risk from scanner-owned code.

Problem: `WriteOperationalDirectiveInternal` still derived bearing from `_cachedTransform.forward`, a Unity Transform orientation read in scanner presentation.
Solution: Use `TryResolveScannerPoseSnapshot` to read the cached player/scanner forward vector and compute bearing from that snapshot.
Rejected Alternatives: Keeping the Transform read as presentation-only, inventing a separate scanner orientation cache, or polling scene state during directive generation.
Scalability potential: Low/middle/high/ultra share the same pose authority. Directive presentation can scale text cadence separately, but orientation truth no longer comes from Unity scene state.
Hardware Impact: Expected sub-1 us saving per directive refresh; correctness and authority isolation are the primary gains.

Problem: Static validator did not catch the latest regressions and the shared construction optimization report is already owned by another agent.
Solution: Extend `ScannerStringInquisitionValidator` to detect direct Unity time, managed formatting, split/LINQ/list/array conversions, string discovery overload calls, and removed prefixed-string helper names. The validator writes `CONSTRUCTION_OPTIMIZATION_REPORT_SHINOBU_226.json` and only writes the shared `CONSTRUCTION_OPTIMIZATION_REPORT.json` when absent or already SHINOBU_226-owned.
Rejected Alternatives: Clobbering the shared report, leaving the validator narrow, or relying on manual rg output only.
Scalability potential: 0 runtime cost. Prevents future hot-path managed scan regressions from stealing low-end frame budget.
Hardware Impact: 0 us runtime; protects the previously saved scanner/presentation budget.

Problem: Loop 11 compile proof was requested after hash-only discovery and validator closure.
Solution: Re-ran scoped rg scans and `git diff --check`; `Get-Process dotnet,csc` returned no visible process, but CPU sampled 100, so no build was launched under the explicit <=50 CPU rule.
Rejected Alternatives: Launching dotnet under CPU >50, or retrying the prior dependency-wall compile without a legal gate and generated-project refresh.
Scalability potential: Protects local iteration bandwidth while parallel agents are active.
Hardware Impact: Avoided compiler CPU/IO pressure on a saturated host; no runtime effect.

Problem: Maxwell audit found scan processing still reached managed strings through `item.PersistentId`, `data.PersistentId`, and `scannable.EntryCategory`.
Solution: ScannerTool now reads cold-baked numeric identifiers: `ItemData.PersistentHashId`, `ModuleMarker.ScannerEntryHash`, `ScannableTarget.CachedEntityHash`, and `ScannableTarget.CachedCategoryKind`. `ModuleMarker` builds its scanner hash in `CacheId`; `ScannableTarget` builds its category enum and entity hash during resolved-string refresh, outside the scan pulse.
Rejected Alternatives: Keeping lower-ASCII prefixed FNV folding in `ScannerTool`, building prefixed strings, or invoking `ScannableCategoryUtility.Classify(scannable.EntryCategory)` during the scan pulse.
Scalability potential: Low devices avoid managed string access during discovery; middle/high/ultra can spend the same saved CPU budget on scanner shader refresh and richer PDA presentation without changing lore truth.
Hardware Impact: Estimated 4-20 us protected per discovery pulse from avoiding managed identity hashing and metadata publication; category enum cache removes another 1-5 us worst-case scan classification path.

Problem: The validator previously claimed a clean string purge even when broader scanner audit findings existed.
Solution: Expanded `ScannerStringInquisitionValidator` to include managed identity patterns, Vault resolve, GlobalSignals, component lookup, tiny job, and forced completion audit strings; JSON summary is conditional on finding count. Regenerated `CONSTRUCTION_OPTIMIZATION_REPORT_SHINOBU_226.json` with residual findings.
Rejected Alternatives: Keeping a narrow string-only report, clobbering the shared construction report, or hiding residual non-string issues to preserve a clean summary.
Scalability potential: 0 runtime cost. The report now separates solved string-route defects from remaining architecture debt that still affects low-end CPU budget.
Hardware Impact: 0 us runtime; prevents false confidence during multi-agent integration.

Problem: `ScannerDataMiningRouter` resolved the same set of Vault handles from hot `FastTick` and completion processing.
Solution: Added a non-owning `ScannerVaultViews` cache refreshed once in owner setup through `TryRefreshVaultViewsCold`; hot routes read `TryReadVaultViews` and no longer fan out 15 `TryResolveHandle` calls per active scan pass.
Rejected Alternatives: Continuing phase-local resolve in every hot tick, adding a new Vault core dependency, or storing raw pointers.
Scalability potential: Low devices avoid repeated Vault resolver work while high/ultra still use the same native arrays for dense scanner visualization and completion proof.
Hardware Impact: Estimated 5-30 us saved per active scanner tick depending on Vault resolver and safety-check cost; no raw pointer ownership introduced.

Problem: Loop 13 compile proof was requested after managed identity and router Vault-cache edits.
Solution: Re-ran scoped static scans and `git diff --check`; checked compiler process state and CPU. `Get-Process dotnet,csc` returned no visible process, but CPU sampled 100, so build was not launched under the explicit <=50 CPU rule.
Rejected Alternatives: Launching dotnet under CPU >50 or retrying the known dependency-wall compile from a saturated host.
Scalability potential: Protects workstation iteration bandwidth while parallel agents are active.
Hardware Impact: Avoided compiler CPU/IO load on a saturated host; no runtime effect.

Problem: Completion evaluation still scheduled two IJob instances for a single completed scan result.
Solution: Execute the existing deterministic job kernels directly over the single native slot and unlock completion buffers immediately. Remove the now-dead completion JobHandle, scheduled flag, finalize method, and forced completion teardown path.
Rejected Alternatives: Keeping a chained JobHandle for one result, inventing a batch queue without profiler proof, or leaving a no-op forced completion branch in OnDisable.
Scalability potential: Weak devices avoid schedule overhead on every completed scan; high/ultra retain the same deterministic native bitmask mutation and can spend saved CPU on VFX richness.
Hardware Impact: Estimated 3-15 us saved per completed scan by avoiding tiny job scheduling and scheduled-lane teardown.

Problem: Loop 14 compile proof was requested after completion scheduling removal.
Solution: Re-ran scoped static scans; `Get-Process dotnet,csc` returned no visible process, but CPU sampled 100, so no build was launched.
Rejected Alternatives: Violating the CPU gate for a compile already known to have dependency-wall noise.
Scalability potential: Protects local iteration bandwidth during parallel agent execution.
Hardware Impact: Avoided compiler CPU/IO load on a saturated host; no runtime effect.

Problem: Residual audit still mixed true hot debt with cold/bootstrap handles, teardown fences, and documented legacy signal bridges.
Solution: Added cold/hot classification to `ScannerStringInquisitionValidator`: cold Vault view refreshes, valid amortized spatial `.Schedule()`, `OnDisable` query teardown, and `GlobalSignals` bridge lanes are filtered; unsafe residuals still fail the report.
Rejected Alternatives: Leaving the report at 40 noisy findings, hiding all residuals, or clobbering another agent's shared report.
Scalability potential: 0 runtime cost. The report now identifies work that actually threatens low-end frame time instead of burying it under cold setup lines.
Hardware Impact: 0 us runtime; improves developer iteration triage.

Problem: Safe scanner completion routes still used direct `GlobalSignals.Publish` even though the hot first-party `SignalBus<T>` lane exists.
Solution: Replaced `ToolAcousticSignal`, `ScanCompleteSignal`, and `ResourceDepletionDeltaSignal` publishes with direct `SignalBus<T>.Push` calls. Kept `AcousticPingSignal`, `ScannerToolActiveSignal`, `AnomalySignal`, and `CrashTelemetrySignal` on documented `GlobalSignals` bridge lanes because active consumers still read latest/dequeue state there.
Rejected Alternatives: Blindly replacing every `GlobalSignals.Publish` and breaking latest-signal bridge consumers, or leaving safe lanes on the managed bridge for convenience.
Scalability potential: Low devices avoid unnecessary bridge overhead on completion-safe lanes; high/ultra keep the same scanner truth and can spend savings on presentation.
Hardware Impact: Estimated 1-4 us avoided per scan completion on the replaced safe lanes; bridge lanes remain as explicit integration debt.

Problem: Scanner black-box, PDA Vault access, and lore entity buffer reads still resolved generation handles from read surfaces.
Solution: Cache non-owning NativeArray views after cold/owner refresh: scanner black-box ring in `EnsureScannerBlackBoxVault`, PDA views in `TryRefreshVaultViewsCold`, and lore entity AUP/hash views in `TryRefreshLoreEntityVaultViewsCold`. Hot readers return the cached views without creating/growing memory or polling the registry.
Rejected Alternatives: Raw pointer caching across Vault relocation, continuing per-read `TryResolveHandle`, or storing owning NativeArray allocations in managers.
Scalability potential: Weak devices avoid repeated Vault metadata checks under active scanner/PDA pressure; ultra devices keep identical truth and spend saved CPU on richer shader/PDA presentation.
Hardware Impact: Estimated 5-25 us avoided under active PDA/scanner read pressure depending on safety-check cost; measured proof pending Unity import/profiler.

Problem: `PdaH8lrLoreStore.TryResolveReadableBasePointer` remains the only reported residual because the Vault mirror fallback re-resolves before exposing a byte span.
Solution: Leave it unresolved for now and report it honestly. The alternative is retaining a raw Vault mirror pointer across frames without a relocation/generation invalidation route, which is a memory-safety regression worse than one UI lookup resolve.
Rejected Alternatives: Caching `_basePointer` blindly, marking the residual clean, or adding a new Vault relocation API outside SHINOBU_226 ownership.
Scalability potential: Low devices pay this only on H8LR mirror UI lookup fallback, not scanner simulation truth. High/ultra behavior is unchanged.
Hardware Impact: Residual cost is unmeasured and bounded to PDA H8LR lookup fallback; no scanner FastTick cost remains from this item.

Problem: Loop 15 compile proof was requested after signal/Vault residual purge.
Solution: Re-ran scoped static scans and `git diff --check`; checked compiler process state and CPU. `Get-Process dotnet,csc` returned no visible compiler process output, but CPU sampled 100, so build stayed blocked by the explicit <=50 CPU gate.
Rejected Alternatives: Launching dotnet under CPU >50, or pretending static scans are Unity import proof.
Scalability potential: Protects local iteration bandwidth during parallel agent execution.
Hardware Impact: Avoided compiler CPU/IO load on a saturated host; no runtime effect.

Problem: `PdaH8lrLoreStore.TryResolveReadableBasePointer` still paid a full Vault handle resolve for every H8LR mirror lookup fallback.
Solution: Use the cached mirror pointer from the cold `TryOpenVaultMirror` load and guard it with `IDataVault.TryGetBufferGeneration((BufferID)handle.BufferID) == handle.Generation` before exposing a span. `GlobalDataVault` bumps buffer generation on relocation, resize, release, and arena relocation, so a stale pointer fails closed instead of being refreshed silently.
Rejected Alternatives: Blind raw pointer caching, continuing `TryResolveHandle` per lookup, or adding a new Vault lease API outside SHINOBU_226 ownership.
Scalability potential: Low devices avoid repeated Vault metadata resolve on PDA H8LR fallback lookups; high/ultra behavior is unchanged and can spend the saved CPU on richer PDA presentation.
Hardware Impact: Estimated 1-5 us avoided per H8LR fallback lookup depending on Vault safety-check cost; runtime profiler proof pending Unity import.

Problem: The sidecar report previously had one honest residual.
Solution: Regenerated `CONSTRUCTION_OPTIMIZATION_REPORT_SHINOBU_226.json` after the generation-fence patch; it now reports zero blocked findings under the scanner/PDA cold/hot classifier.
Rejected Alternatives: Suppressing the residual in validator logic without changing code.
Scalability potential: 0 runtime cost; report now reflects code state instead of exception policy.
Hardware Impact: 0 us runtime; improves regression detection precision.

Problem: Loop 16 compile proof was requested after the H8LR generation fence patch.
Solution: Rechecked compiler process state and CPU. No `dotnet`/`csc` process output was visible, but CPU sampled 100, so no build was launched under the explicit <=50 CPU gate.
Rejected Alternatives: Launching dotnet under CPU >50 after a C# edit, or claiming Unity import proof from static analysis.
Scalability potential: Protects local iteration bandwidth during parallel agent execution.
Hardware Impact: Avoided compiler CPU/IO load on a saturated host; no runtime effect.

Problem: `ScannerDataMiningRouter.OnDisable` still used a forced job completion path for the amortized spatial query.
Solution: Replace teardown completion with a disabled-drain state. Fast/Slow lanes unregister immediately; LateFrame stays registered only while `_queryScheduled` is true and calls `TryFinalizeScheduledQuery()` until `DispatcherJobFence.TryFinalizeCompleted` can reclaim the handle without blocking. Cleanup then unlocks query buffers, unregisters LateFrame, clears the drain flag, and releases descriptors.
Rejected Alternatives: Keeping `forceComplete:true` in teardown, unlocking query buffers before the job has naturally completed, or processing a stale scan result after the router is disabled.
Scalability potential: Low devices avoid an unbounded main-thread stall during scanner disable/despawn; middle/high/ultra keep the same amortized spatial query and visual scanner richness.
Hardware Impact: Removes a potential blocking sync point. Exact microsecond saving is unbounded by static analysis because it depends on job duration at disable; measured proof pending Unity profiler.

Problem: The active scientific scanner snapshot previously needed a final CS1612 audit after context compaction.
Solution: Verified `ScientificScanSnapshot` is raw readonly fields with precomputed `HasFaunaContact` and `HasAttractantTrace`; scanner/PDA target files returned zero `{ get; set; }` or get-only property hits in hot DTO structs.
Rejected Alternatives: Treating MonoBehaviour accessors as hot DTO defects, or leaving active scan state as property-backed methods.
Scalability potential: No runtime feature change; preserves direct field reads for weak devices and keeps high-tier visual spending outside active-state accessor overhead.
Hardware Impact: Prevents defensive-copy/property-call risk in active scan state. Static proof only; no profiler delta.

Problem: Loop 17 compile proof was required after the router teardown patch.
Solution: Re-ran static runtime sweeps and `git diff --check`; `Get-Process dotnet,csc` returned no visible process, but CPU sampled 100, so no build was launched under the explicit <=50 CPU gate.
Rejected Alternatives: Launching dotnet under CPU >50, retrying the known dependency-wall build from a saturated host, or claiming Unity import proof from static analysis.
Scalability potential: Protects workstation iteration bandwidth during parallel agent execution.
Hardware Impact: Avoided compiler CPU/IO load on a saturated host; no runtime effect.

Problem: PDA cached Vault views could be invalidated by a generation mismatch while `_vaultReady` stayed true, preventing `TryColdBootstrap` from reacquiring fresh views.
Solution: `InvalidateCachedVaultViews` now clears `_vaultReady` together with `_vaultViewsCached` and `_vaultViews`; Tick and LateFrameTick re-enter `TryColdBootstrap` and fail closed if fresh descriptors/non-owning views cannot be reacquired.
Rejected Alternatives: Continuing with cached `NativeArray` views after a relocation generation mismatch, or resolving handles from every read surface again.
Scalability potential: Low/middle/high/ultra use the same pointer-free descriptor route. Visual richness can scale independently of Vault relocation safety.
Hardware Impact: 0-5 us depending on avoided stale-fault recovery; correctness and memory safety are primary.

Problem: Scientific occlusion validation still used `hitCollider.transform`, `target.transform`, and `Transform.IsChildOf`, making scanner ownership depend on runtime scene hierarchy traversal.
Solution: `ScannableTarget` caches its runtime GameObject instance id during Awake/OnEnable, and `ScannerTool.IsColliderOwnedByTarget` compares that id against the hit collider GameObject or attached Rigidbody GameObject.
Rejected Alternatives: Keeping Transform hierarchy checks, adding `GetComponentInParent`, or introducing a scanner-owned collider map without profiler proof.
Scalability potential: Weak devices avoid hierarchy traversal on occlusion hit validation; high/ultra keep the same scientific scan truth and spend saved budget on shader/PDA presentation.
Hardware Impact: Estimated sub-1 us per occlusion hit and one scene hierarchy dependency removed.

Problem: Scanner presentation still had a cold `GlobalRegistry.ScalabilityTier` initializer and binary low-tier presentation helper residue.
Solution: Initialize scanner quality tier telemetry to `Unknown`; use incoming quality signals only for black-box/tuning metadata while cadence and lore reveal detail are computed from continuous `GlobalQualityWeight` through `math.smoothstep` and `math.lerp`.
Rejected Alternatives: Preserving a cold binary tier seed, reintroducing `IsLowScannerPresentationTier`, or mapping cadence through discrete tier overloads.
Scalability potential: Low uses longer resample intervals and shorter reveal spans through the same curve; middle tiers interpolate; ultra tightens resampling and reveals more title detail without changing scan truth.
Hardware Impact: 0 us raw speed from the initializer removal; prevents future binary-switch regression in scanner presentation policy.

Problem: The editor validator did not include the exact residuals found by the subagent/static audit.
Solution: Added checks for Transform ownership, direct scalability-tier reads, binary low-tier helper, and discrete tier cadence overload patterns. Refreshed `CONSTRUCTION_OPTIMIZATION_REPORT_SHINOBU_226.json` with the expanded pattern list and zero blocked findings.
Rejected Alternatives: Leaving the sidecar clean while it lacked the latest forbidden residual patterns, or clobbering another agent's shared construction report.
Scalability potential: 0 runtime cost; protects weak-device scanner budget from future managed/hierarchy/binary-quality regressions.
Hardware Impact: 0 us runtime; improves static gate fidelity.

Problem: Loop 18 compile proof was required after scanner/PDA source edits.
Solution: Re-ran runtime source sweeps and `git diff --check`; `Get-Process dotnet,csc` returned `NO_DOTNET_CSC`, but CPU sampled 82 then 100, so no build was launched under the explicit <=50 CPU gate.
Rejected Alternatives: Launching dotnet while CPU was above the project gate, or claiming Unity import proof from static source scans.
Scalability potential: Protects workstation iteration bandwidth during parallel agent execution.
Hardware Impact: Avoided compiler CPU/IO load while host load was above the allowed threshold.
