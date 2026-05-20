# Rationale_SHINOBU_226

Status: IMPLEMENTED_STATIC_VERIFIED_COMPILE_BLOCKED_BY_DEPENDENCY_LOOP8

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
