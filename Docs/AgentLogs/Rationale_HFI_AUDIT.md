# HFI_AUDIT Rationale

Agent: HFI_AUDIT
Domain: Architecture / Global Authority / Platform Portability Audit
Status: ACTIVE / PENDING VERIFICATION

Historical decisions 001-018 are archived at:

- `Docs/Archive/Batch010/AgentLogs/Rationale_HFI_AUDIT.md`

## Decision 019 - Restore Active Audit Memory After Batch Archive

Problem: Active `Docs/Tasks/Status_HFI_AUDIT.md` and
`Docs/AgentLogs/Rationale_HFI_AUDIT.md` were absent in the current tree, while
the previous HFI audit state exists only under `Docs/Archive/Batch010`. The
current user mandate requires continuing work, not relying on volatile chat
state.

Solution: Recreate concise active status/rationale/log files and link them to
the archived Batch010 HFI files. Start R18 as a fresh static recapture instead
of treating R17 as current.

Rejected Alternatives: Copying the full archive back into active logs was
rejected because it would duplicate old batch history and increase noise.
Continuing from chat memory was rejected because the project explicitly treats
disk files as the source of truth.

Scalability potential: Process/runtime-indirect. Weak devices benefit only if
platform and authority claims stay tied to current evidence; high/ultra targets
must not consume engineering attention before the baseline route is proven.

Hardware Impact: 0 runtime us. Audit-memory restoration only.

## Decision 020 - Preserve Save Entity Delta IDs During R18 BufferID Repair

Problem: Fresh R18 gates found 12 central `BufferID` duplicate numeric values:
`SaveEntityDelta*` IDs `70340..70351` collided with `ConstructionSocket*`.
Duplicate central IDs break DataVault ownership because two route names address
the same native identity.

Solution: Preserve `SaveEntityDelta* = 70340..70357` and move
`ConstructionSocket*` to the free contiguous range `70358..70369`.

Rejected Alternatives: Moving save/entity-delta IDs was rejected because save,
WAL, binary payload, or log compatibility is more likely to depend on those
values. Leaving the collision as documentation debt was rejected because the
gate is precise and the repair is narrow.

Scalability potential: Process/runtime-indirect. Weak and XR devices cannot
afford recovery from aliased native buffers; high/ultra devices should spend
budget on visible overkill, not memory-identity ambiguity.

Hardware Impact: 0 runtime us claimed. The repair removes alias risk only.

## Decision 021 - Add Polish Static Pressure Gate Instead Of Mass Refactor

Problem: The ultra-think mandate correctly points at broad legacy pressure:
Burst flag drift, private native fields, `.Complete()` lines, binary hardware
terms, and DTO/property risks. Blindly mass-editing these across a dirty
workspace would likely create compile walls and cross-agent conflicts.

Solution: Add `Tools/PolishMandateStaticAudit.py` with tests and current report
artifacts. Keep defaults warning-only while exposing hard flags for exact
runtime `Pack=1` and missing Burst flags. Use it as a changed-file review gate
until legacy debt is reduced.

Rejected Alternatives: Promoting all current debt to hard failure was rejected
because it would freeze integration without separating legacy from regression.
Manual grep-only reporting was rejected because it is not repeatable.

Scalability potential: Process/runtime-indirect. The gate protects low-end and
standalone targets by making hot-path risk visible before profiling; high/ultra
targets benefit when saved CPU is available for visual overkill.

Hardware Impact: 0 runtime us. Offline static tool only.

## Decision 022 - Route Scanner Query Finalization Through Dispatcher Fence

Problem: `ScannerDataMiningRouter` had direct `_queryHandle.Complete()` in a
completion helper called after `LateFrameTick()` observed `IsCompleted`. It was
unlikely to block, but it bypassed Core's dispatcher fence helper and looked
like a hot completion pattern.

Solution: Late-frame completion now uses
`DispatcherJobFence.TryFinalizeCompleted(ref _queryHandle)`. Forced completion
is retained only for `OnDisable` teardown through `DispatcherJobFence.TryComplete`
with `forceComplete: true`.

Rejected Alternatives: Removing teardown completion was rejected because it
could leave locked Vault buffers during disable. Deferring all scanner result
processing indefinitely was rejected because the scanner needs a bounded
presentation/route feedback path.

Scalability potential: Low/MX350/Quest benefit from avoiding accidental hot
blocking completions. Middle/high/ultra keep the same result path without adding
new simulation or allocations.

Hardware Impact: Runtime microseconds are unclaimed until profiler proof. Static
benefit is structural: direct hot-looking completion site removed from scanner.

## Decision 023 - Add Assembly Dependency Gate Before Touching Core asmdef

Problem: `Hecton8.Core.asmdef` has broad first-party references. Removing them
blindly in a dirty multi-agent workspace risks Unity import breakage and false
compile-wall "fixes" that only move errors elsewhere.

Solution: Add `Tools/AssemblyDependencyAudit.py` as a read-only graph classifier.
It reports Core concrete sibling runtime refs, wider runtime concrete
cross-domain refs, and first-party asmdef cycles. Defaults remain warning-only;
hard flags are available for integrator-controlled enforcement.

Rejected Alternatives: Editing `Hecton8.Core.asmdef` directly was rejected
because each removed reference needs source call-site classification, contract
facade routing, and Unity import proof. Keeping this as prose in a report was
rejected because graph drift must be machine-repeatable.

Scalability potential: Process/runtime-indirect. Weak devices and XR builds
benefit when Core is not forced to drag unrelated concrete runtime assemblies
through every iteration. High/ultra devices are unaffected at runtime unless
the later migration removes hot coupling or build-time churn.

Hardware Impact: 0 runtime us. Static tool only. It found `0` cycles, `16`
Core concrete sibling refs, and `92` runtime concrete cross-domain refs in the
current serialized asmdef graph.

## Decision 024 - Add Platform Proof Gate To Stop Readiness Inflation

Problem: Platform discussion was repeatedly drifting around package presence,
Android scaffold, XR setup, payloads, native plugins, and missing builds. Without
a machine-readable proof gate, a future report could inflate package/settings
text into Quest, Deck, macOS, or PCVR readiness.

Solution: Add `Tools/PlatformPortabilityProofAudit.py`. It records XR package
presence in manifest/lock, Android ARM64/IL2CPP/SDK serialized settings, XR
provider serialized proof, Addressables content presence, Data Monolith payload,
build artifacts/logs, PICO package candidates, and native plugin surface.

Rejected Alternatives: Running Unity/Android build setup was rejected under the
current mandate because the user explicitly forbade premature rebuilds and the
current task can be improved with static proof first. Keeping platform facts in
prose was rejected because drift is likely as packages/settings change.

Scalability potential: Process/runtime-indirect. Weak devices and standalone XR
need proof gates before content/performance claims; high/ultra devices need the
same artifact ladder before visual-overkill settings are trusted.

Hardware Impact: 0 runtime us. Static tool only. Current result: Android/Quest
scaffold exists, but XR provider proof, Addressables data, Data Monolith, build
artifacts, PICO package, and device runtime proof are absent.

## Decision 025 - Treat Counter Drift As Current Churn, Not A Historical Rewrite

Problem: After adding the new tools and rerunning gates, static counters shifted
again in the dirty multi-agent workspace: C# file count, local BufferID casts,
asmdef count, runtime cross-domain refs, and polish pressure changed. Rewriting
older R18/R19 evidence as if it was the original capture would falsify history.

Solution: Add an R21 current recapture layer. Historical R18/R19 numbers remain
their capture-time evidence, while R21 records the newest no-build counters.

Rejected Alternatives: Editing only old tables was rejected because it hides
source churn. Ignoring the drift was rejected because the user explicitly asked
to watch global direction while many agents are changing the project.

Scalability potential: Process/runtime-indirect. Current counters prevent weak
hardware and XR planning from being based on stale risk levels; high/ultra
planning still depends on runtime proof, not static optimism.

Hardware Impact: 0 runtime us. Static recapture only.

## Decision 026 - Promote Gate Policy Out Of Dated Report

Problem: `Docs/Reports/2026-05-20_*` is an evidence snapshot, not a permanent
authority file. If the new assembly/platform gates remain only there, future
agents can ignore them while still technically obeying stable docs.

Solution: Add concise gate references to
`Docs/ARCHITECTURE/PLATFORM_PORTABILITY_PROOF_LADDER.md`,
`Docs/ARCHITECTURE/GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`, root `AGENTS.md`,
and `.codexrules/AGENTS.md`.

Rejected Alternatives: Expanding AGENTS with a long tool list was rejected
because it increases prompt noise. Leaving only `QUALITY_GATES.md` was rejected
because platform and global-authority stable docs should point to their own
audit commands.

Scalability potential: Process/runtime-indirect. Stable gate references reduce
the chance that weak-device or XR claims bypass evidence. High/ultra planning
stays tied to proof instead of prose.

Hardware Impact: 0 runtime us. Documentation policy only.

## Decision 027 - Add Prioritized Hotlist Instead Of Broad Static Noise

Problem: Current gates expose large warning surfaces, but raw totals do not tell
which files deserve the first senior review. Without a ranked map, agents will
chase small local findings and miss the files where multiple risk surfaces
overlap.

Solution: Add `Tools/ArchitectureRiskHotlistAudit.py`. It scores files by
global authority, signal traffic, DataVault/local BufferID/native ownership,
job completion, deterministic time/random, layout, hotpath Update methods, and
platform-tier terms, then writes a top-file report.

Rejected Alternatives: Refactoring the top files immediately was rejected
because the hotlist crosses many owner domains. Static grep-only advice was
rejected because it does not order work or expose overlap.

Scalability potential: Process/runtime-indirect. Low-end/XR planning needs the
highest overlap files reviewed first: inventory, fluid, logistics, audio,
streaming, atmosphere, and core signal lanes. High/ultra planning benefits only
after those owner routes stop leaking private native state and hot barriers.

Hardware Impact: 0 runtime us. Static triage tool only. Current top runtime
review files are `PlayerInventory`, `HectonFluidEngine`,
`LogisticsNetworkGraph`, `PlayerCriticalProceduralAudioRenderer`,
`SpatialAudioManager`, `WorldChunkResidencyManager`,
`SubmarineAtmosphereSystem`, `GasDynamicsSolver`, and `DroneFleetManager`;
`Core/GlobalSignals` also scores high but is an expected central lane owner,
not an automatic deletion target.

## Decision 028 - Create DataVault Baseline Candidate Without Approving It

Problem: The active DataVault no-regression gate fails closed because the
official active baseline path is missing after archival. However, comparing
against the historical Batch007 baseline shows real debt growth, so simply
copying or refreshing the official baseline would hide regression.

Solution: Run the audit against the archived Batch007 baseline and record the
failure. Then write a separate HFI candidate baseline under
`Docs/AgentLogs/DataVaultSovereigntyBaselineCandidate_HFI_AUDIT.json` for
integrator review, without replacing
`DataVaultSovereigntyBaseline_VAULT_SOVEREIGNTY_ENFORCER.json`.

Rejected Alternatives: Overwriting the official baseline was rejected because it
would convert a regression into a pass. Leaving no current candidate was
rejected because the integrator needs a concrete current counter set to decide
whether to approve a reset or enforce burn-down.

Scalability potential: Process/runtime-indirect. Low-end/XR targets are harmed
by unbounded private native memory ownership; high/ultra targets also lose
rollback/memory clarity if the Vault gate is normalized around unchecked growth.

Hardware Impact: 0 runtime us. Static audit only. Current candidate counts:
forbidden constructors `1149`, forbidden declarations `5125`.

## Decision 029 - Add Domain Pressure Before Burn-Down Work

Problem: File-only hotlists identify bad overlap files but do not show whether
the problem is a scattered set of local issues or a domain ownership shape. The
user explicitly asked to ignore small defects and judge global direction.

Solution: Upgrade `ArchitectureRiskHotlistAudit.py` to schema
`hecton8.architecture_risk_hotlist.v2` with per-file domain tagging and domain
pressure totals. Add `GLOBAL_AUTHORITY_BURN_DOWN_PLAN.md` so the next work is
ordered by owner-domain slices, not by broad grep noise.

Rejected Alternatives: Refactoring `PlayerInventory`, `GlobalSignals`,
`HectonFluidEngine`, or `WorldChunkResidencyManager` immediately was rejected
because those files cross different owners and need route-card/proof slices.
Keeping the domain conclusion only in the dated report was rejected because
future agents read stable docs first.

Scalability potential: Low/Quest/Deck targets benefit when Root/World/Core
ownership is reduced before platform claims. Middle/high/ultra targets benefit
when saved CPU/memory budget can be spent on visual overkill instead of
duplicated native state, broad signal fan-out, and hidden compile-wall coupling.

Hardware Impact: 0 runtime us. Static audit/tooling/docs only.

## Decision 030 - Test Hotlist Logic Without Filesystem Temp Writes

Problem: Python unit tests that created temporary `.cs` files failed under the
sandbox with permission denial in temp directories, while the production audit
itself could write its known report artifacts.

Solution: Split scanning into `scan_source(rel, source)` and aggregation into
`aggregate_payload(...)`. Tests now validate category scoring and domain
extraction in memory, avoiding filesystem writes and `__pycache__` with
`PYTHONDONTWRITEBYTECODE=1`.

Rejected Alternatives: Requesting broad filesystem escalation for unit tests was
rejected because the code path can be tested without new files. Dropping tests
was rejected because the hotlist is now a stable gate input.

Scalability potential: Process/runtime-indirect. Faster, deterministic static
tests keep the architecture gate usable on busy multi-agent machines without
blocking platform or runtime work.

Hardware Impact: 0 runtime us. Test harness change only.

## Decision 031 - Remove Generic Registry Bridge Lookups Instead Of Waiving Gate

Problem: Fresh R26 `GlobalAuthorityGate.py` found four generic
`GlobalRegistry.TryGet<T>` call sites after concurrent source churn. The calls
were cold bridge lookups in Core, but the hard gate exists specifically to stop
generic registry access from returning after it reached zero.

Solution: Replace the four calls with existing typed registry slots:
`GlobalRegistry.PersistentWorldRegistry` for
`ISceneTransitionWorldResidencyBridge` and
`IRuntimeWatchdogWorldHealthBridge`, and `GlobalRegistry.Atmosphere` for
`IAtmosphereRenderSettingsBridge`. No new slot, no new API, no asmdef change.

Rejected Alternatives: Whitelisting cold generic lookups was rejected because it
would weaken a hard gate that was already clean. Adding new typed bridge access
methods was rejected because the existing slots already carry the same owners.
Moving ownership or changing registration was rejected because this is a narrow
gate repair, not a route migration.

Scalability potential: Process/runtime-indirect. Weak devices and XR benefit
from keeping Core discovery explicit and non-generic; high/ultra targets avoid
compile-wall drift without changing runtime behavior.

Hardware Impact: 0 runtime us claimed. Static hard-gate restoration only.

## Decision 032 - Treat Candidate DataVault Regression As Real, Not Noise

Problem: A no-flag DataVault candidate run reported PASS, but the proper
candidate no-regression command with `--fail-on-regression` failed on forbidden
field declaration growth from `5125` to `5130`.

Solution: Record the failure and name the files:
`Construction/ShinobuSocketConstructionData.cs`,
`Construction/ShinobuSocketConstructionJobs.cs`,
`Construction/SumpPumpPipeGridJobs.cs`, and
`Core/Data/H8StaticDataContracts.cs`. Do not reset the candidate baseline and do
not patch other agents' owner domains from the audit lane.

Rejected Alternatives: Refreshing the candidate baseline was rejected because it
would hide growth. Fixing construction/static-data owner files here was rejected
because those are active owner domains and need their own route-carded slices.

Scalability potential: Low/Quest/Deck targets are sensitive to unchecked native
field ownership growth. High/ultra targets also need the same Vault discipline
for rollback, relocation, and memory-debug proof.

Hardware Impact: 0 runtime us. Static audit finding only.

## Decision 033 - Add DataVault Regression Drilldown Instead Of Resetting Baseline

Problem: The HFI candidate DataVault baseline was already unapproved, and fresh
no-regression runs now show broader growth: direct constructors and field-like
`NativeArray<T>` declarations increased across Physics, Construction, Editor,
Power, World, Core, and Habitat. A single FAIL line no longer gives enough
owner-domain signal.

Solution: Extend `DataVaultSovereigntyAudit.py` to produce a structured
machine-readable report, including regression deltas by domain and exact
file-level details. Write the current artifact to
`Docs/AgentLogs/DataVaultSovereigntyAudit_HFI_AUDIT_candidate.md/json`.

Rejected Alternatives: Refreshing the candidate baseline was rejected because it
would normalize active growth. Patching owner-domain files from the HFI audit
lane was rejected because the changed files belong to Physics, Construction,
Editor baker, Power, World, Core data, and Habitat owners and need their own
route-carded migration slices.

Scalability potential: Low/Quest/Deck targets benefit because native ownership
growth is now attributable before it becomes memory fragmentation, rollback
state ambiguity, or warm-load spikes. Middle/high/ultra targets benefit because
saved engineering time can target owner slices instead of reading flat grep
output.

Hardware Impact: 0 runtime us. Static tooling/reporting only.

## Decision 034 - Keep No-Build Verification Narrow And Repeatable

Problem: The worktree is changing under multiple agents. Running a Unity/dotnet
build for audit-only Python/docs changes would add noise and violate the
current no-rebuild mandate, while skipping tests would make the new gate output
untrusted.

Solution: Use `python -B` audit unit tests and static gates only. Keep build,
Unity import, player artifact, profiler, GC, memory, headset, Deck, macOS,
Linux, and console claims out of the report.

Rejected Alternatives: Launching dotnet or Unity was rejected because no C#
compile claim is needed for this slice. Treating older R26 counters as current
was rejected because the fresh gates show changed C# file count, local BufferID
casts, and DataVault regression domains.

Scalability potential: Process/runtime-indirect. Weak-device readiness depends
on current evidence rather than stale counters; high/ultra readiness still
requires runtime artifacts before visual-overkill claims mean anything.

Hardware Impact: 0 runtime us. Verification discipline only.

## Decision 035 - Split DataVault Regression By Execution Surface

Problem: The DataVault regression report grouped by domain, but that mixed
runtime frame-path risk with editor/offline-baker risk. For platform direction,
those are different failures: runtime native ownership growth can hurt Quest,
Deck, and weak-PC memory/frame budgets; editor-baker growth can still violate
Data Monolith discipline but does not directly execute in player frames.

Solution: Add `extract_execution_surface(...)`, store `executionSurface` on
each regression detail, aggregate `regressionByExecutionSurface` in the JSON
report, and add a markdown section before the domain table.

Rejected Alternatives: Leaving the report as domain-only was rejected because
it overstates editor/baker allocations as runtime frame risk and understates
new runtime regressions such as `Tools/LaserCutterDodJobs.cs` and
`Gameplay/ScannerDataMiningRouter.cs`. Whitelisting editor files was rejected
because offline binary generation still needs Data Monolith and alignment
discipline.

Scalability potential: Low/Quest/Deck targets get a sharper runtime burn-down
queue. Middle/high/ultra targets still benefit because editor/offline payload
hygiene prevents stale, misaligned, or unbounded baked data from becoming a
runtime streaming problem.

Hardware Impact: 0 runtime us. Static report-schema expansion only.
