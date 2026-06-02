# Rationale 1619 - Documentation Master And API Spec Curator

Date: 2026-06-01
Status: VERIFIED STATIC DOC
Evidence class: STATIC_DOC / STATIC_FILESYSTEM / STATIC_SOURCE

## Decision 001 - Root Markdown Conflict

Problem: XML Task 10 says root may contain only `AGENTS.md`, `MASTER_RELEASE_WORK_PLAN.md`, and `BUILD_PLAYTEST_ISSUES.md`, but active governance allows `TASTE.md`, and `AGENTS.md` explicitly references root `TASTE.md` and root `textes.md`.

Solution: Do not move `TASTE.md` or `textes.md` during this pass. Treat XML root list as stale against active `DOC_GOVERNANCE.md`, `PROJECT_BASELINE.md`, and `AGENTS.md`. Record the drift and update root-reference docs if needed.

Rejected Alternatives: Moving root `TASTE.md` would break a current authority path. Moving `textes.md` would break the explicit `AGENTS.md` marketing-text route and create silent prompt drift for public-copy agents.

Scalability potential: Low/Middle/High/Ultra unaffected at runtime; preserves agent startup determinism by keeping authority paths stable.

Hardware Impact: Estimated runtime gain on i3/MX350: 0 us; documentation-only risk reduction, no frame-time claim.

## Decision 002 - JSON Report Suppression

Problem: XML tasks mention JSON inventory/report artifacts, but the user explicitly rejected unread JSON proof dumps and named the disk docs as the primary proof.

Solution: Use PowerShell/Python scans for local validation and write durable proof into `Status_1619.md`, `Rationale_1619.md`, `LOG_1619.md`, and the active documentation ledger. Generate JSON only if an existing validator requires it.

Rejected Alternatives: Creating large `Docs/Reports/*.json` files would satisfy legacy prompt wording but violate the user's current proof preference and add report churn.

Scalability potential: Keeps active proof readable for all agent tiers; avoids bloating context and file I/O for parallel agents.

Hardware Impact: Estimated runtime gain on i3/MX350: 0 us; host I/O saved by skipping large report writes.

## Decision 003 - Stable Ledger Instead Of Mass Lore Rewrite

Problem: Active lore and marketing files are concurrently dirty and many are untracked in this workspace. A mass rewrite for bloat would collide with narrative/marketing agents and risk deleting current work.

Solution: Restrict this pass to stable authority docs and API/spec constants. Record bloat/lore findings in `Status_1619.md` and keep lore rewrite/deprecation pending for the owning lore agents.

Rejected Alternatives: Bulk-moving or rewriting `Docs/Lore/**` and `Docs/Marketing/**` would satisfy the literal XML but violate concurrent-agent hygiene and risk overwriting user/agent edits.

Scalability potential: Low/Middle/High/Ultra unaffected at runtime; reduces agent prompt drift by fixing stable source constants first.

Hardware Impact: Estimated runtime gain on i3/MX350: 0 us; prevents documentation conflicts, no frame-time claim.

## Decision 004 - H8DM Source Constants Supersede Old Report Timings

Problem: Current `static_data.h8bin` is `1,804,864` bytes with checksum `0xA85210353432862A`, while active docs still carried older X_002 report values for `1,064,384` bytes and checksum `0x19D880780D6E1B46`.

Solution: Update current artifact/spec rows to the 2026-06-01 byte parse. Preserve old X_002 timing/checksum values only where explicitly labeled historical; require rerun before current performance claims.

Rejected Alternatives: Replacing historical report metrics with new blob values would falsify old report artifacts. Leaving old current rows would mislead future agents about payload identity.

Scalability potential: Low tier keeps static payload route clear; Middle/High/Ultra can add sections without changing save or SignalBus authority when H8DM header stays aligned.

Hardware Impact: Estimated runtime gain on i3/MX350: 0 us; no runtime code changed, but avoids boot/proof mismatch in future work.

## Decision 005 - No Root File Moves

Problem: Root currently contains five `.md` anchors. Older scanner code recognizes only three, active governance previously recognized four, and `AGENTS.md` requires `textes.md`.

Solution: Update governance/index docs to five root text anchors and move nothing. Generated project files, CSVs, `.csproj`, and `.slnx` are not active documentation and were left in place.

Rejected Alternatives: Moving `textes.md` into `Docs/` would break the explicit `AGENTS.md` lookup. Moving Unity/project generated files would be architectural sabotage, not docs cleanup.

Scalability potential: Low/Middle/High/Ultra unaffected at runtime; faster agent onboarding because root policy matches disk reality.

Hardware Impact: Estimated runtime gain on i3/MX350: 0 us; documentation-only.

## Decision 006 - BOM Normalization Scope

Problem: The full active corpus contains `1786` non-BOM files, but converting all of them would touch thousands of files while other agents are writing lore, marketing, code reports, and task logs.

Solution: Normalize only 1619-created/modified files to UTF-8 BOM and record the global non-BOM debt as PENDING VERIFICATION debt.

Rejected Alternatives: Bulk encoding rewrite would create noisy diffs, increase merge conflict probability, and provide no runtime or source-truth improvement.

Scalability potential: Low/Middle/High/Ultra unaffected at runtime; reduces tool/rendering risk for files this pass owns without destabilizing concurrent edits.

Hardware Impact: Estimated runtime gain on i3/MX350: 0 us; no runtime code changed.

## Decision 007 - No 30 Percent Compression Claim

Problem: XML asks for a `>30%` corpus compression proof. Current active corpus includes thousands of dirty/untracked lore and marketing files under other owners. Compressing them now is unsafe.

Solution: Execute targeted stable-doc compression/synchronization and explicitly reject a false corpus-wide reduction claim.

Rejected Alternatives: Claiming `>30%` based on historical X_012 reports or overwriting active lore/marketing files would be fake evidence.

Scalability potential: Keeps authority docs lean enough for agent onboarding while leaving owner-specific bloat to domain owners.

Hardware Impact: Estimated runtime gain on i3/MX350: 0 us; documentation-only.

## Decision 008 - Markdown Proof Instead Of JSON Report

Problem: XML Task 20 requests a JSON optimization report, but the user explicitly rejected unread JSON proof dumps.

Solution: Treat the stable actuality ledger, status, rationale, and LOG files as the proof artifacts. Record the ledger SHA-256 instead of emitting another JSON file.

Rejected Alternatives: Writing `Docs/Reports/DOCUMENTATION_OPTIMIZATION_REPORT_1619.json` would add report churn and contradict the user's latest proof directive.

Scalability potential: Low/Middle/High/Ultra unaffected at runtime; proof remains readable to future agents without loading a large JSON inventory.

Hardware Impact: Estimated runtime gain on i3/MX350: 0 us; host I/O reduced only.

## Decision 009 - Source Scope Override For APEX Verification

Problem: Original 1619 batch was documentation-only, but the user issued a later explicit APEX integrator command requiring C# hardening and source proof.

Solution: Treat the newer command as a bounded source override. Edit only concrete violations: hot dispatcher lookup probes, simulation-phase presentation writes, and one wide DataVault mutation-guard path.

Rejected Alternatives: Refusing C# edits would ignore the latest directive. Broad refactors across all architecture domains would collide with parallel agents and create unprovable churn.

Scalability potential: Low tier benefits from fewer hot lookups and fewer same-frame presentation writes; Middle/High/Ultra keep the same visual path but phase-separated.

Hardware Impact: Estimated low-end gain: no precise runtime microsecond claim without profiler; static risk reduction is lower hot-path lookup pressure and fewer visual writes before VISUAL_SYNC.

## Decision 010 - LateFrame Presentation Transfer

Problem: Phase audit found post-processing, wheel, hatch, strap, and scooter light writes reachable from `Tick` or `FixedTick`.

Solution: Move those writes to `LateFrameTick` through primitive dirty flags and cached rotations/light-restore intent. Simulation phases now update only state scalars and pending values.

Rejected Alternatives: Leaving direct `Transform`, `Light`, and Volume writes in simulation phases violates phase ownership. Adding jobs would be pointless because these are presentation writes, not batch math.

Scalability potential: Low/Middle devices avoid presentation work before simulation settles; High/Ultra can increase visual fidelity inside the same LateFrame route.

Hardware Impact: Estimated low-end gain: unmeasured; no build/profile run by directive. The measurable proof is source-phase separation with `phase=0` in static scan.

## Decision 011 - Construction Job Lock Flattening

Problem: `FluidPipeGraphRuntime` and `HabitatGraphManager` held broad mutation guards over job write/read buffers until job completion.

Solution: Remove the solve/flood-propagation-wide mutation guards and pin each required DataVault buffer with `TryLockBuffer(bufferId, SystemID.Construction)`. Failed acquisition unwinds immediately; completed job release walks the bit mask and unlocks each buffer.

Rejected Alternatives: Keeping a single wide guard maximizes stall radius. Rewriting solver/flood ownership models would be overengineering for this pass.

Scalability potential: Low tier gets smaller contention windows and fail-fast buffer acquisition; Middle/High/Ultra keep the same solver data layout and can scale capacity without changing lock semantics.

Hardware Impact: Estimated low-end gain: unmeasured; expected reduction is deadlock/stall vector removal, not frame-time arithmetic without profiler.

## Decision 012 - Static Guard Instead Of Build Spam

Problem: User forbade `dotnet build`; existing CPU already had external dotnet/Unity processes.

Solution: Validate with Python/PowerShell source scans, `rg`, balance checks, whitespace checks, and an editor NUnit source guard. No JSON, no binary dump, no compile command.

Rejected Alternatives: Running build would violate resource throttling. Creating another report artifact would violate the proof preference.

Scalability potential: Keeps verification cheap for parallel agents and leaves CPU for actual coding lanes.

Hardware Impact: Host build CPU saved; runtime gain not claimed.

## Decision 013 - Targeted Deep Audit After Broad Timeout

Problem: A full transitive source scan across every runtime file exceeded the local timeout and would become resource noise if repeated blindly.

Solution: Reduce the second pass to files that contain both hot-method markers and dangerous dependency/phase tokens in Construction, Gameplay, Interaction, Vehicles, and Audio. The targeted pass checked 320 runtime files and found 0 direct hot dependencies, 0 transitive helper dependency candidates, and 0 runtime transitive phase candidates.

Rejected Alternatives: Re-running the same broad parser would waste host CPU. Patching all remaining `GlobalRegistry.Dispatcher` registration helpers without proof would create churn in cold lifecycle paths.

Scalability potential: Low tier keeps hot loops clean without broad code churn; Middle/High/Ultra retain existing registration semantics while the editor source guard catches future drift.

Hardware Impact: Host CPU saved by narrowing to token-positive files; runtime gain not claimed without profiler.

## Decision 014 - Non-Construction Nested Lock Candidates

Problem: The stricter lock-order scan found direct nested acquire-before-release patterns in `SpatialAudioManager.TryCopyAcousticSdfLeaseToSnapshot` and `WorldProceduralScatterDirectorMigratorySargassum.TryLockMigratorySargassumJobBuffers`.

Solution: In `SpatialAudioManager`, copy the SDF snapshot under the mutation guard, release that guard, then pin the snapshot buffer. In migratory Sargassum, replace direct double `TryLockBuffer` acquisition with a pin-mask helper and `finally` unwind.

Rejected Alternatives: Ignoring them as non-construction would leave the APEX proof incomplete. Holding a broad guard over the whole audio or vegetation path would increase stall radius.

Scalability potential: Low tier gets fewer direct lock-order stall vectors; Middle/High/Ultra keep the same audio occlusion and drifting canopy features without adding simulation cost.

Hardware Impact: Runtime gain unmeasured; static proof after patch is `nestedLocks=0` across 173 runtime lock-bearing files.

## Decision 015 - Editor Guard Must Cover Real DataVault Lock APIs

Problem: The APEX editor guard originally checked only `TryAcquireWriteLock`, while the actual runtime lock-flattening risks found in this pass used `TryAcquireMutationGuard` and `TryLockBuffer`.

Solution: Extend `ApexIntegratorVerification1619EditTests` so all three DataVault write-lock acquire forms share one lock-order scanner, and so local acquire/release methods require a `finally` release scope.

Rejected Alternatives: Leaving the guard narrow would allow future regressions through the same APIs that caused this pass. Running `dotnet build` to compensate would violate the user throttle and still would not prove lock-order semantics.

Scalability potential: Low tier gets earlier static rejection of stall/deadlock regressions; Middle/High/Ultra keep the same code path and can add richer visuals without broadening write-lock lifetime.

Hardware Impact: Runtime gain unmeasured; host compile CPU saved. Static evidence: guard source now contains `TryAcquireMutationGuard`, `TryLockBuffer`, `ReleaseMutationGuard`, `TryUnlockBuffer`, and `FindNextToken`; `git diff --check` on the guard file is clean.

## Decision 016 - Hot Registry Guard Covers Convenience Properties

Problem: The APEX editor guard originally named only `GlobalRegistry.Get<T>()`, `GlobalRegistry.Get(...)`, and `GlobalRegistry.Dispatcher`. HECTON-8 doctrine treats the entire registry as cold identity/DI, so `GlobalRegistry.Player`, `GlobalRegistry.Services`, or any future convenience property would still be illegal inside `Tick`, `FixedUpdate`, `LateFrameTick`, or `Execute`.

Solution: Add generic `GlobalRegistry.` to the hot dependency token list in `ApexIntegratorVerification1619EditTests`. This makes every registry access fail in hot methods and direct local helpers unless it is cold-cached outside the hot phase.

Rejected Alternatives: Listing known convenience properties would rot as the registry evolves. Running a full compile to compensate would violate the user's compilation throttle and would not prove hot-path dependency semantics.

Scalability potential: Low devices avoid accidental service-locator polling in frame loops; Middle/High/Ultra keep registry-backed systems decoupled while spending saved frame budget on visual fidelity through cached interfaces and SignalBus lanes.

Hardware Impact: Runtime gain unmeasured; static risk removed. Validation used source token scans only. No `dotnet build`, `msbuild`, or Unity batch compile was launched.
