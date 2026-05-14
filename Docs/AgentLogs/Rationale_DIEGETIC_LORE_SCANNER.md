# Rationale - DIEGETIC_LORE_SCANNER

Status: PENDING VERIFICATION

## Intake

Problem: Scanner prompt requires replacing continuous raycasts with spatial lookup while preserving diegetic scanner UI.
Solution: Inspect current scanner, scan event, GlobalDataVault, dispatcher, AUP, and UI render paths before edits. Use existing `ScanEvents`/GlobalRegistry interfaces where possible.
Rejected Alternatives: Direct concrete dependency on narrative/campaign systems before verifying contracts; raw per-frame Physics.Raycast; string/TMP `.text` writes.
Scalability potential: Low uses percentage-only display and one occlusion query after candidate selection. Middle enables bounded scramble. High/Ultra can add denser decryption glyph visuals while keeping authority in same scan candidate path.
Hardware Impact: Expected low-end i3/MX350 gain is removal of continuous scanner raycasts; exact microseconds are PENDING VERIFICATION until profiler/compile evidence exists.

Problem: Status/rationale files were missing at session start.
Solution: Created fresh batch status and rationale files before code edits.
Rejected Alternatives: Reusing stale logs or chat-only tracking.
Scalability potential: File-backed state survives context compression and supports iterative loops.
Hardware Impact: No runtime impact.

## Loop 2 - Tasks 6-10

Problem: Scanner needed target forgiveness without pixel-accurate ray work.
Solution: Used the highest forward dot product under 15m as the candidate. This is the intended lie: near-crosshair selection, not exact collider picking.
Rejected Alternatives: screen-space projection plus collider tests; multi-ray cone cast; broad managed raycast fan.
Scalability potential: Low tier keeps the same cheap candidate. High/Ultra can spend saved budget on richer RT glyph noise while preserving identical target authority.
Hardware Impact: Expected low-end gain is one Burst linear scan over native arrays plus at most one occlusion command, replacing continuous ray work.

Problem: Scanner decryption UI had to write to the physical tool screen without heap strings.
Solution: The tool RT controller now consumes `ScannerToolActiveSignal`, resolves the lore title by hash, writes into fixed `char[]` staging buffers with `Span<char>`, and scrambles unrevealed characters. Low tier writes percentage only.
Rejected Alternatives: TMP `.text`, managed formatted strings, per-frame `StringBuilder`, and UI `Update()`.
Scalability potential: MX350 gets percentage-only. Mid gets title scramble. High/Ultra can use higher refresh/noise density on the same buffer path.
Hardware Impact: Expected low-end gain is zero managed allocation during active scanner display; exact microseconds PENDING VERIFICATION.

Problem: Lore completion must unlock systems and update campaign without coupling scanner to narrative implementation.
Solution: Completion path publishes `LoreFragmentScannedSignal` and an existing `ProgressionEventSignal` for MetaCampaignService. DataArchaeology remains the commit authority.
Rejected Alternatives: direct MetaCampaignService method call; UI-side unlock; managed UnityEvent chain.
Scalability potential: Additional lore consumers can subscribe to signal lanes without touching scanner code.
Hardware Impact: Completion-only signal traffic; no frame cost during scanning.

## Loop 1 - Tasks 1-5

Problem: Focused scanner path fired a raycast before resolving whether a lore target was even near the reticle.
Solution: Replaced the initial forward raycast with a DataVault-backed lore SOA (`LoreEntityAUPs`, `LoreEntityHashes`) and a Burst dot-product job scheduled from `FastTick`.
Rejected Alternatives: `Physics.Raycast` every resample; managed `FindObjectsOfType`; adding a scanner singleton manager.
Scalability potential: Low/MX350 scans at fixed 1024-node cap with one result slot. Middle/High/Ultra can raise visual decoding density without changing target authority.
Hardware Impact: Expected i3/MX350 win is removal of the old continuous forward raycast per scanner resample; exact microseconds remain PENDING VERIFICATION because Unity compiler/profiler access is blocked.

Problem: Lore completion needed a decoupled signal and campaign progression without direct narrative dependencies.
Solution: Added `LoreFragmentScannedSignal(Hash)` and published it beside `ScanCompleteSignal`; also emits `ProgressionEventSignal` so `MetaCampaignService` consumes through its existing DAG signal lane.
Rejected Alternatives: Direct calls into `MetaCampaignService`; managed UnityEvents; adding another scanner manager.
Scalability potential: SignalBus consumers can fan out to PDA, campaign, telemetry, and UI without scanner knowing those systems.
Hardware Impact: One unmanaged signal push per completed lore scan; no per-frame cost.

Problem: Scanner target metadata had no contract assembly boundary.
Solution: Added `Hecton8.Tools.Scanner.Contracts` with `IScannerLoreTitleReadModel` as the boundary for future scanner UI/read-model extraction; kept current implementation in place to avoid a high-risk assembly move during active multi-agent work.
Rejected Alternatives: Moving `ScannerTool.cs` into a new asmdef immediately, which would drag gameplay, world, UI, inventory, audio, and narrative dependencies into a compile wall.
Scalability potential: Contract assembly allows later extraction of scanner read models without pulling scanner implementation into UI packages.
Hardware Impact: No runtime cost.

Problem: Verification is blocked by project-level dependency state, not by scanner-specific diagnostics.
Solution: Ran generated Core build and Unity refresh. Core build fails before scanner-specific validation on missing assemblies (`Hecton8.Environment.Fluids`, `Hecton8.Audio.Virtualization`, etc.). Unity refresh timed out and console reads return `no_unity_session`.
Rejected Alternatives: Reporting green compile without evidence; reverting scanner changes for unrelated dependency failures.
Scalability potential: None; this is build infrastructure state.
Hardware Impact: No runtime impact.

## Loop 3 - Tasks 11-15

Problem: Scanner targeting must survive floating origin and AUP shifts.
Solution: Lore targets are stored as `AbsoluteUniversePosition` in DataVault and compared to the camera with `AbsoluteUniversePosition.ToCameraRelativeFloat3` inside the candidate job.
Rejected Alternatives: Treating `transform.position` as authoritative range data; caching world-space vectors across shifts.
Scalability potential: Low/Middle/High/Ultra share the same stable authority path; visual layers can scale independently from coordinate precision.
Hardware Impact: One AUP-relative conversion per registered lore node during resample; expected cost remains below the removed broad ray work, exact microseconds PENDING VERIFICATION.

Problem: Low-tier hardware cannot spend budget on scanner glyph theater.
Solution: Low/Unknown/MX350 paths bypass title scrambling and write percentage-only text through `ZeroGCFormatter.FastIntToChars`.
Rejected Alternatives: One uniform scramble path across all tiers; managed formatted percentage strings.
Scalability potential: Low uses numeric status. Middle uses deterministic title scramble. High/Ultra can raise decode density and visual noise while preserving the same zero-GC staging buffers.
Hardware Impact: Expected i3/MX350 gain is removal of per-character scramble work on low tier; exact microseconds PENDING VERIFICATION.

Problem: Scanner acquisition and UI presentation needed deterministic phase separation.
Solution: Acquisition runs in `IFastTickable` on the Player lane; scanner state publication runs through `ILateFrameTickable` on the UI lane; tool RT display consumes signals in the existing dispatcher path.
Rejected Alternatives: Unity `Update()` loops in scanner/UI; direct UI calls from acquisition.
Scalability potential: Simulation remains stable while UI refresh rate/effects scale by tier.
Hardware Impact: Phase split prevents UI work from feeding back into acquisition; exact profiler numbers unavailable.

Problem: Required compile proof for `Span<char>` no boxing is unavailable.
Solution: Static audit confirms scanner/UI span writes use stackalloc or fixed char arrays and TMP `SetCharArray`; project compile is dependency-blocked before scanner proof can be produced.
Rejected Alternatives: Marking Task 15 complete without compiler evidence; replacing span path with strings to appease unverifiable build state.
Scalability potential: Once build infrastructure is restored, this path should validate without changing runtime design.
Hardware Impact: No runtime impact from the blocked proof itself.

## OMEGA POLISH CHANGES

Problem: Occlusion command setup still used honest vector magnitude plus divide after the scanner had already selected a single lore candidate.
Solution: Replaced `Vector3.magnitude` and direction division with `math.lengthsq`, `math.rsqrt`, and multiplication in `QueueScientificOcclusionRaycast`.
Rejected Alternatives: Keeping sqrt/division because there is only one ray; adding more occlusion rays for certainty.
Scalability potential: Low/MX350 keeps the one-ray lie as cheap as possible. Middle/High/Ultra can spend the saved budget on scanner RT glyph density, not target authority.
Hardware Impact: Estimated gain is one sqrt and one vector divide removed per candidate occlusion request; exact microseconds PENDING VERIFICATION.

Problem: Polish mandate required proof that the scanner path did not drift back into managed UI/string churn.
Solution: Ran static audits for `foreach`, `.ToString()`, `.text =`, `Physics.Raycast`, `ScannerManager`, and `void Update(` over scanner UI/target files. Scanner RT path remains `Span<char>` plus TMP `SetCharArray`.
Rejected Alternatives: Treating the current implementation as done without re-reading the prompt; moving title formatting into managed strings.
Scalability potential: Low writes numeric percentage only. Middle writes scrambled title. High/Ultra can increase decode animation density without changing allocation behavior.
Hardware Impact: Expected managed allocation remains zero in the active scanner RT write path; exact microseconds PENDING VERIFICATION.

Problem: Some scanner-domain edits touch core signal and memory IDs outside presentation files.
Solution: Kept cross-domain surface limited to stable contract points: `BufferID.LoreEntityAUPs`, `BufferID.LoreEntityHashes`, and fixed-size signal-lane publication. Scanner does not call campaign services directly.
Rejected Alternatives: Direct MetaCampaign singleton dependency; private scanner manager singleton; moving large implementation files across asmdefs during parallel agent work.
Scalability potential: Signal/DataVault contracts let later systems consume scanner events or SOA buffers without coupling back into the UI/tool code.
Hardware Impact: Completion-only signal pushes have no steady-frame scanner cost.

Problem: Build verification still cannot prove scanner syntax in isolation because the generated project fails earlier on unrelated missing contracts.
Solution: Re-ran `dotnet build Hecton8.Core.csproj`; it still fails at global missing references (`Hecton8.Environment.Fluids`, audio virtualization/propagation, CCD, persistence, macro database, and other contracts) before a scanner-specific proof can be isolated.
Rejected Alternatives: Marking VERIFIED MASTER GRADE without compiler evidence; reverting scanner work for unrelated dependency failures.
Scalability potential: None; this is project integration state.
Hardware Impact: No runtime impact.

Final Git Diff: `git diff --stat` includes scanner files plus unrelated dirty core signal/memory files from other agents. Scanner-owned hunks are `ScannerTool.cs`, `ScannableTarget.cs`, and `ToolDiegeticDisplayController.cs`; unrelated `GlobalSignals.cs` / `H8Memory.cs` deltas are not claimed as scanner work.

## LOOP 5 HARDENING

Problem: Diegetic scanner RT title rendering still resolved the lore title by scanning the registry every progress repaint.
Solution: Added a fixed `char[96]` scanner-title cache in `ToolDiegeticDisplayController`, keyed by artifact hash. Repaints now copy the cached title into the TMP staging buffer before scrambling.
Rejected Alternatives: Keeping the full hash lookup per repaint; caching managed strings; moving UI lookup into a singleton manager.
Scalability potential: Low tier still skips title lookup entirely. Middle/High/Ultra get stable title decrypt visuals with less CPU overhead as progress buckets change.
Hardware Impact: Estimated i3/MX350 gain is removal of repeated 1024-entry title scans during one focused scan; exact microseconds PENDING VERIFICATION.

Problem: `ScannableTarget.TryGetLoreEntityBuffers()` rewrote lore AUP/hash SOA every call, even if two consumers requested it in the same frame.
Solution: Added a same-frame debounce in `SyncLoreEntityVaultAups()` while keeping edit-mode and first-frame writes intact.
Rejected Alternatives: Removing sync entirely, which would break moving lore targets; adding a new scheduler dependency; widening DataVault ownership outside the UX task.
Scalability potential: Low/Middle/High/Ultra all avoid redundant same-frame SOA rewrites. High/Ultra can still support multiple scanner displays without duplicating registry sync work.
Hardware Impact: Estimated low-end gain is one avoided 1024-target transform/AUP rewrite for each extra same-frame scanner consumer; exact microseconds PENDING VERIFICATION.

Problem: Previous diff report risked blending unrelated core edits into this scanner task.
Solution: Reconciled actual `git diff` with source contents and separated scanner-owned hunks from dirty workspace changes in core memory/signal files.
Rejected Alternatives: Claiming all current dirty files; reverting unrelated edits without authority.
Scalability potential: None; this is integration hygiene.
Hardware Impact: No runtime impact.

Problem: Unity MCP script validation is currently unavailable.
Solution: Attempted `validate_script`; transport failed against `127.0.0.1:8088/mcp`. Re-ran `dotnet build Hecton8.Core.csproj`; it still fails on global missing contracts, latest observed count 128 errors, and no scanner/UI/target syntax errors surfaced before the dependency wall.
Rejected Alternatives: Reporting Unity-verified status without a live editor session.
Scalability potential: None; blocked infrastructure.
Hardware Impact: No runtime impact.

## LOOP 6 CAMERA/TIER HARDENING

Problem: Scanner acquisition used the held tool transform as the forward authority. That can disagree with the player's actual camera/crosshair, causing the "highest dot" lie to feel wrong even if the math is cheap.
Solution: Resolve the acquisition pose from `GlobalRegistry.Player.PlayerCamera.transform` first, then fall back to the cached tool transform only when the player camera is unavailable.
Rejected Alternatives: `Camera.main` lookup, pixel-perfect projection, or returning to continuous raycast authority.
Scalability potential: Low/Middle/High/Ultra share the same camera-space target authority, so presentation quality can scale without changing what gets selected.
Hardware Impact: Expected low-end impact is one registry property read and transform read per resample, replacing no added physics work; exact microseconds PENDING VERIFICATION.

Problem: Focused scanner resampling treated MX350 and Ultra the same.
Solution: Added tiered resample intervals: Low/Unknown/MX350 clamp to a slower cadence, High/Ultra may resample faster for responsiveness while retaining one selected candidate and one occlusion command.
Rejected Alternatives: A balanced middle cadence for every device; more physics queries on high-end hardware.
Scalability potential: Toaster path spends fewer CPU slices on acquisition. High/Ultra spend saved cycles on responsiveness and scanner RT polish without changing deterministic scan completion.
Hardware Impact: Estimated i3/MX350 gain is fewer acquisition resamples under held scan; exact profiler delta PENDING VERIFICATION.

Problem: Prompt extraction initially failed after hardening because the XML tag includes role/chat attributes.
Solution: Switched the verification regex to `<AGENT_PROMPT\s+id="DIEGETIC_LORE_SCANNER"[^>]*>.*?</AGENT_PROMPT>` and re-read the complete assignment.
Rejected Alternatives: Reading neighboring prompts; assuming the task from memory.
Scalability potential: None; process correctness.
Hardware Impact: No runtime impact.

Problem: Build proof still cannot complete after the camera/tier patch.
Solution: Re-ran `dotnet build Hecton8.Core.csproj`; global missing contracts now report 132 errors. A scanner-file filter over the build output returned no `ScannerTool.cs`, `ScannableTarget.cs`, or `ToolDiegeticDisplayController.cs` matches.
Rejected Alternatives: Calling the build green; reverting scanner work for unrelated global dependency failures.
Scalability potential: None; project integration state.
Hardware Impact: No runtime impact.

## LOOP 7 CONTACT/TITLE HARDENING

Problem: Low-tier resampling was deliberately slowed, but the scan contact grace window still used the serialized base interval. With a small configured interval, MX350/Low could lose held contact between resamples.
Solution: Compute hold timeout from `ResolveFocusedScanResampleInterval()` so contact grace tracks the effective tier cadence.
Rejected Alternatives: Raising the serialized interval globally; increasing ray/occlusion frequency; making low tier use the high-tier cadence.
Scalability potential: Low/Unknown/MX350 get stable held scans at lower sampling cadence. High/Ultra keep tighter acquisition without changing scan authority.
Hardware Impact: Expected low-end gain is stable progress with fewer resamples; exact profiler delta PENDING VERIFICATION.

Problem: `ScheduleScientificConeBatch()` recomputed tier cadence after candidate selection instead of treating it as one per-pass decision.
Solution: Resolve `resampleInterval` once per acquisition pass and reuse it for lore and fallback paths.
Rejected Alternatives: Repeated registry/tier reads within one pass; hardcoding tier cadence at startup.
Scalability potential: Runtime tier changes still apply on the next resample while avoiding duplicate work in the current pass.
Hardware Impact: Estimated gain is one avoided tier lookup per scanner resample; exact microseconds PENDING VERIFICATION.

Problem: The scanner operational summary path could still scan up to 1024 lore targets for the same active artifact title, even though the RT display has its own char cache.
Solution: Added a static last hash/index cache inside `ScannableTarget.TryWriteLoreEntityTitle()`, invalidated when strings refresh or lore registry membership changes.
Rejected Alternatives: Managed dictionary cache; storing managed localized strings in the scanner tool; leaving every summary repaint to full-scan the registry.
Scalability potential: Low tier still bypasses title resolve. Middle/High/Ultra title display resolves the common same-target path in O(1).
Hardware Impact: Estimated low-end gain is avoiding repeated 1024-entry title scans while holding a lore target; exact microseconds PENDING VERIFICATION.

Problem: Build verification became inconsistent after this pass.
Solution: Static scanner checks passed. A captured build filtered for scanner-owned files returned no matches, but a plain minimal build returned `exit 1` without useful diagnostics and a single-thread retry timed out. Verified no leftover dotnet processes remained.
Rejected Alternatives: Calling the project verified from one inconsistent pass; killing unrelated processes; marking Task 15 complete without stable compiler proof.
Scalability potential: None; verification infrastructure state.
Hardware Impact: No runtime impact.

## LOOP 8 LIFECYCLE HARDENING

Problem: `OnUnequip()` published an inactive scanner signal, but `OnDespawn()` and non-quit `OnDestroy()` could clear scanner state without notifying the diegetic RT consumer. That leaves stale decryption text until another scanner packet arrives.
Solution: Added `PublishInactiveScannerTuningSignal()` and call it after focus reset on despawn. On destroy, it only runs in play mode and not after `OnApplicationQuit()` to avoid reinitializing signal queues during shutdown.
Rejected Alternatives: Direct call into the UI controller; adding `ScannerManager.Instance`; unconditional `GlobalSignals.Publish()` from teardown.
Scalability potential: UI consumers remain decoupled and pull from the latest scanner signal. Pool churn or scene swaps do not leave stale RT state on low-end or high-end devices.
Hardware Impact: One completion-only/inactive signal on lifecycle exit; no steady-frame cost.

Problem: Prior compile status was stale after the project graph recovered.
Solution: Re-ran generated-project builds. Filtered build returned exit 0, zero error lines, and no scanner file matches. Plain summary build then passed with 0 warnings and 0 errors.
Rejected Alternatives: Keeping Task 15 blocked after compiler evidence became available; ignoring the previous inconsistent build behavior.
Scalability potential: None; verification state.
Hardware Impact: No runtime impact.

## LOOP 9 TITLE CACHE VERSION HARDENING

Problem: The diegetic RT title cache was keyed only by artifact hash. If a lore target refreshed title data or registry membership changed under the same hash, the RT could keep displaying stale decrypted title characters.
Solution: Added `ScannableTarget.LoreTitleLookupVersion`, incremented from the same invalidation path that clears the static title lookup cache. `ToolDiegeticDisplayController` now refreshes its fixed `char[96]` title cache when either artifact hash or title version changes.
Rejected Alternatives: Managed dictionary keyed by hash; caching localized strings in the UI; removing the fixed cache and scanning 1024 lore targets every repaint.
Scalability potential: Low tier still bypasses title display. Middle/High/Ultra get stable same-target O(1) title copy while runtime title edits or registry churn invalidate correctly.
Hardware Impact: One integer read/compare per scanner title resolve; avoids stale UI without adding managed allocation or steady physics work.

Problem: The workspace has many unrelated dirty files, including `GlobalSignals.cs`, from other agents.
Solution: Limited this pass to scanner-owned `ScannableTarget.cs` and `ToolDiegeticDisplayController.cs`. Did not patch core signal ordering while the core file is being edited by others.
Rejected Alternatives: Editing dirty core signal infrastructure for a theoretical main-thread latest-state race; reverting unrelated agent edits.
Scalability potential: Keeps scanner UI robust without increasing merge risk in the multi-agent workspace.
Hardware Impact: No additional steady-frame cost beyond the title-version compare.

Problem: Verification needed to prove this small cache change did not break compile.
Solution: Ran scanner static checks, filtered build, and plain summary build. Final build passed with 0 warnings and 0 errors.
Rejected Alternatives: Trusting local inspection only.
Scalability potential: None; verification state.
Hardware Impact: No runtime impact.

## LOOP 10 RUNTIME TOOL-HASH HARDENING

Problem: `ScannerToolActiveSignal.ToolHash` always used the synthetic `SCNR` tuning hash. `ToolDiegeticDisplayController.SetToolHashFilter()` is documented as a runtime tool hash filter, so a physical scanner display filtered to `PlayerTool.RuntimeToolId` could reject its own scanner active packet.
Solution: Publish `RuntimeToolId` in `ScannerToolActiveSignal.ToolHash` when available, falling back to `SCNR` only when the runtime id is not ready.
Rejected Alternatives: Broadening UI acceptance to any active scanner packet on any filtered display; direct display binding; scanner manager singleton.
Scalability potential: Multiple tool displays can filter scanner signals by real runtime id. Generic unfiltered displays still receive scanner packets. Authoring that explicitly uses `SCNR` remains compatible because the UI already treats that filter as a scanner wildcard.
Hardware Impact: One uint selection per scanner signal publish; no steady-frame cost beyond existing late-frame signal path.

Problem: Existing scanner signal dedup ignored tool hash.
Solution: Added `_lastPublishedTuningToolHash` to the dedup key so a signal is republished if the scanner runtime id appears after registration.
Rejected Alternatives: Clearing all published cache fields on every lane register; removing dedup entirely.
Scalability potential: Correct packet identity without increasing signal spam.
Hardware Impact: One uint compare per publish attempt; avoids unnecessary duplicate signal traffic.

Problem: The workspace is heavily dirty from parallel agents.
Solution: Kept the edit scoped to `ScannerTool.cs`; did not touch dirty `GlobalSignals.cs` or unrelated systems.
Rejected Alternatives: Refactoring signal infrastructure during concurrent core edits.
Scalability potential: Minimal merge footprint.
Hardware Impact: No additional runtime impact.

Problem: Compile proof had to be refreshed after changing signal identity.
Solution: Ran filtered and plain generated-project builds. Both passed; final summary build reported 0 warnings and 0 errors.
Rejected Alternatives: Trusting static inspection after modifying signal payload identity.
Scalability potential: None; verification state.
Hardware Impact: No runtime impact.

## LOOP 11 SCANNER BLACK BOX / H-PHI HARDENING

Problem: The scanner acquisition path had native candidate state, one-ray occlusion, and diegetic UI signals, but no scanner-local 300-frame postmortem ring. If acquisition pose/progress became non-finite, the system could clear or overwrite transient state before a crash report explained the failure.
Solution: Added `ScannerBlackBoxEntry` as a fixed `NativeArray` ring of 300 entries owned by `ScannerTool`. Each fast tick records frame, runtime tool hash, artifact/blueprint hashes, active and pending lore hashes, progress, battery, dt, contact age, pending occlusion distance, tool pose, active probe, pending occlusion position, flags, and quality tier. Non-finite values are sanitized before the native write.
Rejected Alternatives: Debug logging, managed queues, expanding `GlobalSignals.cs` while it is under parallel-agent ownership, or writing a generic scanner manager singleton.
Scalability potential: Low/MX350 pays one compact sequential native write while the scanner is equipped; no added physics or UI polling. Middle keeps richer title decrypt visuals using the existing signal path. High/Ultra can increase visual decode density while the same black box captures exact target identity and probe state for postmortem triage.
Hardware Impact: Estimated normal-path cost is below 1 us per active scanner fast tick on i3/MX350; memory is 300 fixed entries. Fault dump is one-shot disk I/O only after invalid state.

Problem: Scanner progress and snapshot values could receive non-finite floats from upstream fragment progress, SDF/chemical sampling, or tool delta input, then feed display-facing signals.
Solution: Added finite guard helpers for scanner saturation and non-negative values. `ScannerToolActiveSignal` now publishes sanitized progress/battery. Scientific snapshots sanitize progress, density, density01, toxicity, chemical load, attractant strength, depth, and direction. Held progress accumulation rejects non-finite delta/progress before it reaches archaeology/UI state.
Rejected Alternatives: Letting `math.saturate`/`Mathf.Clamp01` pass NaN through; throwing gameplay exceptions; hiding the issue with UI-only clamps.
Scalability potential: Toaster path avoids poisoned percentage text and spurious dump spam. High/Ultra preserve the same visual overkill path but stop one corrupt sample from contaminating RT display and scanner evidence.
Hardware Impact: Added branch-only finite checks around existing writes. Expected cost is sub-us; avoided cost is crash ambiguity and invalid UI packet fanout.

Problem: Decryption reveal math still used raw scanner progress in both scanner summary text and the physical RT consumer. A NaN progress packet can poison `floor(progress * length)` or suppress repaint because the raw comparison is not meaningful.
Solution: Scanner summary and `ToolDiegeticDisplayController` now sanitize scanner progress before percent buckets, reveal counts, and dirty-state comparison.
Rejected Alternatives: Trusting upstream scanner signal sanitation only; clamping the final integer after `floor` while still letting NaN enter the math.
Scalability potential: Low tier percentage display and high-tier scramble reveal share the same finite progress gate. Ultra visual density can increase without inheriting invalid progress state.
Hardware Impact: One finite branch per scanner repaint/reveal; estimated sub-us on i3/MX350.

Problem: The first black-box write risked treating "no scanner contact yet" as invalid because `_scientificLastContactTime` starts at negative infinity.
Solution: Contact age now records `0` until the first finite contact timestamp exists, so the black box does not dump on normal equip/no-contact state.
Rejected Alternatives: Initializing last-contact time to `Time.time` on equip, which would fake recent contact; suppressing all black-box invalid checks until first target lock.
Scalability potential: Low/Middle/High/Ultra all keep clean postmortem data without false-positive disk writes during normal scanner idle.
Hardware Impact: One finite branch per black-box write; avoids accidental fault-path disk I/O.

Problem: The current batch file no longer contains `DIEGETIC_LORE_SCANNER`.
Solution: Ran a raw CLI search/extraction attempt, confirmed absence, ignored neighboring prompts, and continued from `Status_DIEGETIC_LORE_SCANNER.md` plus this rationale file.
Rejected Alternatives: Reading adjacent agents' prompts or stopping scanner hardening despite persisted scanner state.
Scalability potential: Process hygiene only.
Hardware Impact: No runtime impact.

Problem: User explicitly forbade dotnet rebuilds in this loop.
Solution: Verification is limited to static checks: `git diff --check` and scanner-path bans for `Camera.main`, direct `Physics.Raycast`, `void Update`, `foreach`, `.ToString`, and TMP `.text =`.
Rejected Alternatives: Running `dotnet build` against explicit instruction; claiming Unity/compiler verification from static review.
Scalability potential: None; verification state.
Hardware Impact: No runtime impact.

## LOOP 12 SCOPED H-PHI TIER CADENCE HYGIENE

Problem: Scanner quality-tier decisions were scattered across signal publish, low-tier scanner summary, and acquisition resample cadence. That created multiple `GlobalRegistry.ScalabilityTier` source refs and allowed tier changes to affect presentation/acquisition immediately.
Solution: Added `ResolveScannerQualityTier()` in `ScannerTool`: one global tier ingress, 0.5s probe cadence, and 2s candidate hysteresis. Signal publish, low-tier decryption, black-box tier stamping, and focused resample cadence now share that cached tier.
Rejected Alternatives: Keeping direct tier reads in each helper; adding a new cross-domain tier service; editing core registry contracts during a UX pass.
Scalability potential: Low/MX350 cannot flicker between percentage and scramble presentation from transient tier changes. High/Ultra keep visual-overkill scanner responsiveness after the hysteresis window instead of causing immediate cadence churn.
Hardware Impact: Source-level `GlobalRegistry.ScalabilityTier` refs in `ScannerTool.cs` drop from 3 to 1. Runtime tier polling is capped to 2 Hz per active scanner instead of every publish/resample/helper call. Exact profiler gain PENDING VERIFICATION.

Problem: The physical tool RT display already had low-tier hysteresis, but it still read `GlobalRegistry.ScalabilityTier` every UI tick.
Solution: Added a 0.5s quality-tier probe countdown to `ToolDiegeticDisplayController`. The existing 2s low-tier hysteresis remains; registry polling is throttled while flag-driven low-tier fallback still participates every tick.
Rejected Alternatives: Removing hysteresis, polling every tick, or adding quality tier into `ToolStateChangedSignal` by changing the 32-byte public signal layout.
Scalability potential: Low-tier displays shed RT work predictably. High/Ultra keep richer RT rendering after stable tier confirmation. Public signal layout stays immutable for the current batch.
Hardware Impact: Source-level `GlobalRegistry.ScalabilityTier` refs in `ToolDiegeticDisplayController.cs` drop from 2 to 1. Active-display registry polling drops from 60 Hz to 2 Hz target cadence.

Problem: UI display tick delta used `math.max(0f, deltaTime)`, which does not explicitly reject NaN before timer math.
Solution: Added `SanitizeSeconds()` and routed display tick delta through it before pool retry and tier hysteresis counters.
Rejected Alternatives: Trusting dispatcher delta in a render-facing UI timer; clamping only at timer write sites.
Scalability potential: All tiers avoid NaN-poisoned RT release/pool retry timers.
Hardware Impact: One finite branch per active display tick; no allocation.

Problem: H-Phi improvement needed objective scoped evidence without fake global claims.
Solution: Counted baseline/current scanner-domain `GlobalRegistry.ScalabilityTier` refs: `ScannerTool.cs` 3 -> 1, `ToolDiegeticDisplayController.cs` 2 -> 1. Did not edit `Docs/Reports/HECTON_PHI_REPORT.md` or claim runtime/global H-Phi.
Rejected Alternatives: Running dotnet rebuilds, running a global H-Phi audit as if it verified runtime, or broad refactoring outside the scanner domain.
Scalability potential: Cleaner source-level synaptic hygiene and lower hot registry polling in the scanner UX path.
Hardware Impact: Exact microseconds PENDING PROFILER; expected low-end gain is small but deterministic.

## LOOP 13 EVENT-LANE H-PHI HARDENING

Problem: The previous H-Phi pass still hid `GlobalRegistry.ScalabilityTier` reads behind helpers called by scanner fast/late/UI tick paths. The cadence was throttled, but it was still polling a global service from active presentation/acquisition loops.
Solution: `ScannerTool` and `ToolDiegeticDisplayController` now implement `IScalabilityChangedEventListener` and consume the existing `ScalabilityEvents` NativeQueue lane. Cold lifecycle reads seed the tier, event callbacks queue candidates, and existing 2s hysteresis accepts stable changes. Both scanner and tool display also use `ISlowTickable` as a fallback for platform pressure code that currently applies `RegisterScalabilityTierOverride()` without raising a scalability event.
Rejected Alternatives: Adding a new scanner quality bus, editing core registry contracts, keeping fast/late/UI registry polling, or changing the 32-byte `ToolStateChangedSignal` layout during a parallel batch.
Scalability potential: Low/MX350 tier drops still shed scanner RT/scramble work after a stable event. High/Ultra keep visual-overkill shader scalar and faster scanner response without global polling in the active scanner/display paths.
Hardware Impact: Active scanner/display tier registry polling leaves fast/late/UI tick paths; silent override checks run on dispatcher SlowTick only. Exact microseconds PENDING PROFILER; expected gain is small but removes a hot-path H-Phi violation while preserving thermal/battery downgrade correctness.

Problem: Focused scanner acquisition read `GlobalRegistry.Player` inside the acquisition pose helper used by held scan resampling.
Solution: Added `_cachedPlayerContext`, refreshed on Awake, OnSpawn, and OnEquip, and used that cached interface for player-camera pose resolution. Fallback remains the tool transform if the player camera/context is unavailable.
Rejected Alternatives: `Camera.main`, direct player concrete references, scene search, or hot registry fallback inside the scan pose helper.
Scalability potential: Low/MX350 avoids a registry read every focused resample. High/Ultra keep camera-authored crosshair acquisition and can spend saved overhead on denser scanner visuals.
Hardware Impact: One `GlobalRegistry.Player` read removed from each focused resample; exact microseconds PENDING PROFILER.

Problem: Static verification had to account for staged and unstaged workspace state while the user forbade dotnet rebuilds.
Solution: Ran `git diff HEAD --check`, `git diff --cached --check`, scanner banned-pattern scans, direct registry source scans, and event-listener source scans. No dotnet or Unity rebuild was run.
Rejected Alternatives: Reporting compiler verification without running it, or modifying unrelated staged work from other agents.
Scalability potential: Process hygiene only.
Hardware Impact: No runtime impact.

## LOOP 14 ATLAS/LOCALIZATION H-PHI COMPILE GUARD

Problem: Scanner operational summary/directive generation still depended on Atlas service state, and the cached Atlas/event pass had two risks: an inert event callback with no registration path, and a compile-risk local typed as `ILocalizationService`, which does not exist in this project.
Solution: Funnel Atlas reads through `ResolveCachedAtlasSignalCold()` so presentation text uses the cached `AtlasSignalSystem` handle after first resolve. Register equipped scanners with `AtlasSignalEvents` and unregister on lane shutdown so Atlas events invalidate scanner operational text immediately. Correct the localization helper local to the actual `LocalizationManager` type while keeping the single registry lookup per call.
Rejected Alternatives: Repeated `GlobalRegistry.AtlasSignal` reads from summary/directive hot paths; leaving `OnAtlasSignalEvent()` inert; adding a new localization interface during a UX scanner pass; reverting to two `GlobalRegistry.Localization` property reads per localized string.
Scalability potential: Low/MX350 avoids avoidable service-locator reads while rendering scanner text and only carries the Atlas listener while equipped. High/Ultra keep Atlas signal-bearing presentation and richer scanner visuals without widening authority or adding a manager singleton.
Hardware Impact: Source-level Atlas service refs in `ScannerTool.cs` are constrained to one cold helper. Atlas event registration is lifecycle-scoped; normal event cost is one cache invalidation when Atlas state changes. Localization resolves use one registry property read instead of repeated access; exact microseconds remain PENDING PROFILER.

Problem: The current batch file still does not contain `DIEGETIC_LORE_SCANNER`, and the user explicitly forbids dotnet rebuilds.
Solution: Re-ran raw PowerShell prompt extraction, confirmed absence, ignored neighboring prompts, and verified with static source checks only.
Rejected Alternatives: Reading other agents' XML blocks; running `dotnet build`; claiming compiler proof without execution.
Scalability potential: Process hygiene only.
Hardware Impact: No runtime impact.

## LOOP 15 REGISTRATION RETRY AND CACHE REBIND HYGIENE

Problem: `ToolDiegeticDisplayController` attempted slow-tick registration from UI `Tick()` whenever `_registeredSlowTick` was false. If the slow-tick bucket or dispatcher was temporarily unavailable, this degraded into a per-frame service-locator/registration retry.
Solution: Add a 0.5s retry fence for failed slow-tick registration while keeping forced immediate attempts from OnEnable/Start. Successful registration clears the retry timer; disable clears the timer and unregisters normally.
Rejected Alternatives: Leaving a hot retry loop; removing the SlowTick fallback and losing silent scalability override handling; adding a new central registration manager.
Scalability potential: Low/MX350 avoids pointless per-frame dispatcher probes under bucket pressure. High/Ultra still receive event/SlowTick quality updates without UI tick registry polling.
Hardware Impact: Worst-case failed retry cadence drops from 60Hz UI tick to 2Hz. Exact microseconds remain PENDING PROFILER.

Problem: Scanner active paths still had avoidable duplicate service-property reads: audio ping read `GlobalRegistry.Audio` twice, and threat-prediction sampling read `GlobalRegistry.LoreDatabase` twice.
Solution: Collapse audio ping to one local service read. Cache `LoreDatabaseManager` behind `ResolveCachedLoreDatabaseCold()` and register the equipped scanner as a `IGlobalRegistryHotSwapListener` so lore, Atlas, player, and localization service replacements rebind cached scanner dependencies.
Rejected Alternatives: Permanent cached service handles without hot-swap invalidation; duplicate service locator reads in the active scanner sample path; direct narrative service dependency outside GlobalRegistry.
Scalability potential: Toaster path sheds duplicate lookups during pulse/threat sampling. High/Ultra keep richer scientific scanner presentation without coupling scanner authority to narrative internals.
Hardware Impact: One duplicate audio property read removed per pulse and one duplicate lore property read removed per threat prediction sample; hot-swap listener is lifecycle-scoped to equipped scanner use.

Problem: Scanner mode labels and summaries are cached strings. A runtime language change while the scanner is held or stowed could leave the operational text in the previous language until mode changed manually.
Solution: `ScannerTool` now implements `ILocalizationLanguageChangedListener`, refreshes mode strings on language events, refreshes on equip, and invalidates operational string caches.
Rejected Alternatives: Rebuilding strings every summary call; keeping stale localized mode strings; polling localization state from `GetOperationalSummary()`.
Scalability potential: Low tier keeps cached text and zero-GC display behavior. High/Ultra presentation stays responsive to language changes through events rather than polling.
Hardware Impact: No per-frame cost. Event-only string refresh when language changes or scanner equips.

Problem: Verification remains source-only by user order.
Solution: Ran `git diff --check` and scanner banned-pattern scans over scanner/UI/target files. No dotnet or Unity rebuild was run.
Rejected Alternatives: Running prohibited dotnet rebuilds; reporting Unity/compiler verification without evidence.
Scalability potential: Process hygiene only.
Hardware Impact: No runtime impact.

## LOOP 16 SERVICE CACHE LIFETIME HARDENING

Problem: Scanner caches player, survival, Atlas, and lore services to remove active-path GlobalRegistry reads. Those cached handles could survive unequip/despawn or a service replacement that happens while the scanner is not registered for hot-swap events.
Solution: Clear cached runtime-service handles on spawn, equip, unequip, despawn, and destroy. Player hot-swap now also clears the cached survival component so the next scientific sample resolves against the current player.
Rejected Alternatives: Re-reading GlobalRegistry on every scanner sample; keeping permanent cached handles across pool reuse; registering scanner hot-swap listeners while unequipped.
Scalability potential: Low/MX350 keeps active scanner reads clean without stale-service risk. High/Ultra can keep richer scanner presentation and cached Atlas/lore use without extra polling.
Hardware Impact: No per-frame cost. Cold lifecycle cache clears and one event-time survival null on player replacement.

Problem: `ToolDiegeticDisplayController` cached the render-texture pool, but a `RenderTexturePoolRuntime` service replacement while the physical display is enabled could leave the display renting/returning through a stale owner.
Solution: Add lifecycle-scoped `IGlobalRegistryHotSwapListener` support to the display. On render-texture pool replacement, release any RT owned by the old pool, bind the new pool, clear fallback retry timers, and mark the display dirty. On disable, unregister and clear the cached pool handle.
Rejected Alternatives: Resolving `GlobalRegistry.RenderTexturePool` every render attempt; assuming the pool never hot-swaps; leaking an RT from the previous pool.
Scalability potential: Toaster path avoids repeated pool lookups and handles pool pressure/replacement cleanly. High/Ultra can keep RT display fidelity without stale pool ownership.
Hardware Impact: Event-only path. Avoids failed/stale pool calls and prevents RT ownership drift; exact microseconds PENDING PROFILER.

Problem: Verification remains limited by the explicit no-rebuild order.
Solution: Used staged diff checks and scanner banned-pattern scans only.
Rejected Alternatives: Running dotnet/Unity compile validation against user instruction.
Scalability potential: Process hygiene only.
Hardware Impact: No runtime impact.
