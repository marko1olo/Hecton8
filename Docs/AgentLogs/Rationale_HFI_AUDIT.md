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

## Decision 036 - Separate Hardware Scaffold From Hardware Proof

Problem: The project now contains enough ARM64, x86, GPU, XR, and quality-scaling
infrastructure that it is easy to overstate readiness. Android ARM64 IL2CPP,
OpenXR packages, Vulkan settings, GlobalQualityWeight, VRAM governors, foveation
state, and GPU-driven buffers are necessary, but they are not evidence that
Quest, Steam Deck, Mac, weak PC, or high-end RTX paths actually run inside budget.

Solution: Record a hardware portability audit that scores scaffold separately
from runtime proof. Treat package/settings/source evidence as direction only,
and require player logs, profiler frame timing, shader compilation evidence,
memory traces, and device captures before any platform is called adapted.

Rejected Alternatives: Treating Android ARM64 plus XR packages as Quest
readiness was rejected because `m_BuildTargetVRSettings` is empty and no
headset proof exists. Treating Windows x86 as proven was rejected because there
is no current player/profiler proof. Treating GPU-driven code as GPU readiness
was rejected because shader warmup, compute dispatch size, readback cadence, and
device captures are missing.

Scalability potential: Low/Quest/Deck targets benefit because the next work is
forced toward measured proof and runtime debt burn-down instead of broad claims.
Middle/high/ultra targets benefit because visual-overkill work stays gated by
evidence that the survival path is stable first.

Hardware Impact: 0 runtime us claimed. Static audit/reporting only.

## Decision 037 - Improve Portability Discipline Before Runtime Proof

Problem: The user asked what can be improved without runtime proofs. There are
real pre-proof improvements, but some obvious-looking changes are unsafe:
rewiring QualitySettings tiers or changing compute thread groups by hand can
create import or dispatch regressions.

Solution: Split work into safe-now and import-sensitive lanes. Safe-now work is
audit expansion, settings proof, shader/compute risk gates, DataVault
classifier improvement, Burst flag cleanup in leaf jobs, and `.Complete()`
classification. Import-sensitive work is URP quality-tier rewiring, compute
thread-group changes, and native plugin importer edits.

Rejected Alternatives: Blindly editing `QualitySettings.asset` was rejected
because the project has PC and Android quality concerns mixed in the same tier.
Blindly reducing compute shader thread groups was rejected because C# dispatch
callers and shader indexing must be changed together.

Scalability potential: Low/Quest/Deck targets benefit from lower static risk
before measurement. Middle/high/ultra targets benefit because platform gates
stop survival-path debt from hiding behind visual-overkill claims.

Hardware Impact: 0 runtime us claimed. Backlog/reporting only.

## Decision 038 - Add Static Platform Gates Before Runtime Proof

Problem: ARM64/x86/GPU readiness was still too easy to overstate because the
platform audit saw packages and broad settings, but not sustained-performance,
Vulkan-only serialization, Quest URP wiring, shader warmup, or compute kernel
thread group risk.

Solution: Expand `PlatformPortabilityProofAudit.py` to schema
`hecton8.platform_portability_proof_audit.v2` and add explicit readiness flags
for Android sustained performance, Android Vulkan serialization, Quest URP
asset wiring, ShaderVariantCollection warmup, and runtime compute thread groups.

Rejected Alternatives: A chat-only checklist was rejected because it cannot be
used as a hard gate. Treating all compute files as one failure class was
rejected because Editor/Bakery kernels and runtime player kernels have different
hardware impact.

Scalability potential: Low/Quest/Deck targets get static gates for thermal
mode, Vulkan path, mobile URP route, warmup, and compute group size before a
player build exists. Middle/high/ultra targets retain visibility into shader
variant and compute risk without forcing immediate content rewrites.

Hardware Impact: 0 runtime us claimed. Static gate improvement only.

## Decision 039 - Enable Android Sustained Performance As A Standalone Setting

Problem: Android sustained-performance mode was serialized off even though Quest
and Android thermal stability are first-order platform risks.

Solution: Change `ProjectSettings/ProjectSettings.asset` to
`AndroidEnableSustainedPerformanceMode: 1` as a narrow settings-only change.

Rejected Alternatives: Waiting for headset proof was rejected because this is a
platform policy setting with no Unity import topology changes. Hiding the change
inside URP/QualitySettings work was rejected because that would mix a safe
thermal setting with import-sensitive render-route edits.

Scalability potential: Low/Quest devices get a lower thermal-throttle risk.
Middle/high/ultra targets are not affected as gameplay truth, DTO layout, and
quality topology do not change.

Hardware Impact: No frame-time microseconds claimed. Expected benefit is
thermal stability, not direct CPU/GPU instruction reduction.

## Decision 040 - Replace Shader Warmup Presence With Explicit WarmUp Call

Problem: `GameBootstrapper` exposed configured shader variant collections, but
the warmup path only read `collection.isWarmedUp`. That is not evidence that a
collection is warmed before gameplay.

Solution: During boot warmup, call `collection.WarmUp()` when
`!collection.isWarmedUp`. The platform audit now detects explicit
`ShaderVariantCollection.WarmUp()` call sites.

Rejected Alternatives: `Shader.WarmupAllShaders()` was rejected as too broad
and potentially abusive for variant count. Relying on preloaded shader entries
alone was rejected because it does not prove the configured bootstrap path warms
the collections.

Scalability potential: Low/Quest/Deck targets reduce first-use shader hitch
risk. Middle/high/ultra targets keep visual-overkill shader variants visible
behind a boot-phase warmup route rather than gameplay compilation.

Hardware Impact: 0 gameplay us claimed. Work is shifted to boot warmup; actual
stutter reduction requires runtime/player proof.

## Decision 041 - Classify DataVault Native Declarations Before Burn-Down

Problem: The previous DataVault declaration gate counted `[ReadOnly] public
NativeArray<T>` fields inside Burst/job structs as the same kind of debt as
persistent owner fields. That inflated ownership risk and made the burn-down
queue imprecise.

Solution: Add a C# scope classifier that separates persistent owner fields from
job-input native collections, strips comments/strings before scanning, and
emits v3/v2 counters for persistent, job-input, Burst job-input, and unknown
native collection declarations.

Rejected Alternatives: Filename-only classification was rejected because job
structs and owner classes can coexist in the same file. Refreshing the baseline
was rejected because the current run still shows a true runtime regression:
`Construction/HabitatConstructionManager.cs` forbidden declarations `6 -> 8`.

Scalability potential: Low/Quest/Deck targets get a cleaner runtime ownership
burn-down list instead of wasting time on safe job inputs. Middle/high/ultra
targets still benefit because persistent native ownership ambiguity blocks
rollback, relocation, and memory-forensics proof.

Hardware Impact: 0 runtime us claimed. Static classifier/reporting only.

## Decision 042 - Repair PDA Compute Asset Contract Before Shader Tuning

Problem: `Player.prefab` serialized `pdaSonarMapCompute` to
`Hecton_SonarMap.compute`, while `PDAMapTab` resolves and dispatches
`CSBuildMapPoints`, which exists in `Hecton_MapMesh.compute`. The old compute
asset exposes `CSRaymarch`, not the current cartography kernel contract.

Solution: Repoint the prefab compute reference to `Hecton_MapMesh.compute`
GUID `a3f2b5e8d9a74c34aa4fba2a5ce18277`, matching the runtime C# contract and
the editor fallback path.

Rejected Alternatives: Adding a compatibility C# path for the obsolete
`CSRaymarch` shader was rejected because the current DOD route is packed sector
word scan into an append buffer, not legacy 3D raymarch. Reducing the old
shader's `[numthreads(8,8,8)]` was rejected as a false fix because the current
runtime should not dispatch that asset.

Scalability potential: Low/Quest/Deck targets avoid a silent missing-kernel
fallback that would destroy the intended cheap PDA point-cloud path. Middle,
high, and ultra targets keep the same GPU append route with `GetKernelThreadGroupSizes`.

Hardware Impact: No measured frame-time claim. Structural impact is route
repair: intended `Hecton_MapMesh.compute` kernel is now the serialized runtime
asset.

## Decision 043 - Gate Compute Risk By Runtime Reachability

Problem: The platform audit marked every `.compute` under `Assets` and outside
`Editor` as Runtime risk. That over-counted dormant assets and editor/test-only
assets, creating hard failures for code that the player cannot currently reach.

Solution: Upgrade `PlatformPortabilityProofAudit.py` to schema v3. Risky
thread groups now carry runtime reachability from C# path/name references and
serialized GUID references. The hard compute flag uses runtime-referenced risk;
the report still exposes runtime asset risk separately.

Rejected Alternatives: Whitelisting individual files was rejected because it
would hide future route drift. Keeping path-only Runtime labels was rejected
because it made the gate noisy enough to push agents toward unnecessary shader
rewrites.

Scalability potential: Low/Quest/Deck work now targets actually reachable GPU
pressure first. Middle/high/ultra targets still see dormant high-risk assets in
the report before those assets become active render paths.

Hardware Impact: 0 runtime us. Static gate precision only. Current hard compute
risk changed from 4 runtime path-risk groups to 0 runtime-referenced groups.

## Decision 044 - Collapse HUD Luminance Reduction To A 64-Lane Dear Lie

Problem: `HectonHudFogLuminance.compute` was the only runtime-referenced compute
hard risk: `[numthreads(16,16,1)]` with 256 lanes, 256 shared floats, and 1024
texture loads per throttled readback. It produces a single scalar HUD luminance
value, so full 16x16 reduction is not worth the mobile/TBDR risk.

Solution: Reduce the kernel to `[numthreads(8,8,1)]`, `groupshared[64]`,
64-lane reduction, and divisor `1/64`. Keep four jittered samples per lane so
the output remains a stable optical scalar approximation. Add C# runtime guards
for unsupported compute, missing kernel, unsupported kernel, and thread groups
above 64.

Rejected Alternatives: Keeping 256 lanes was rejected because the output is one
low-frequency perceptual scalar, not gameplay truth. Moving the reduction to
CPU readback was rejected because that would add synchronous bandwidth pressure
and worse frame risk. Adding a high/low binary shader split was rejected because
the current global platform law prefers continuous quality and cheap stable
approximations.

Scalability potential: Low/Quest/Deck targets get the cheap path by default.
Middle/high/ultra targets lose no gameplay truth; visual overkill should be
spent on caustics/fog shaders, not a 0.1s HUD luminance scalar.

Hardware Impact: Per readback dispatch, lanes drop `256 -> 64` and texture
loads drop `1024 -> 256`, saving 192 group lanes and 768 texture loads before
profiler proof. Runtime microseconds are unclaimed until device capture.

## Decision 045 - Report Quest URP Route From The Editor Configurator

Problem: The project has a Quest URP asset and an editor/build configurator, but
Android does not select that asset. Serialized proof shows Android default
quality index `1`, render pipeline GUID `0a1617ac2a1aa74409dd0f7176dffe42`;
Quest URP GUID is `d9c4cd6a763fec04a913c6a149663003`. `XRSettings.asset` is
legacy-only and `ProjectSettings.asset` still has `m_BuildTargetVRSettings: []`.

Solution: Add an Android Quality/Quest URP route section to
`QuestVulkanRenderPipelineConfigurator` and upgrade
`PlatformPortabilityProofAudit.py` to schema v4 with
`questConfiguratorQualityRouteAuditPresent`. The configurator now reports the
Quest GUID, Android default quality index/name, Android default render-pipeline
GUID, and PASS/BLOCKED status whenever the existing Quest configuration path
writes its audit report.

Rejected Alternatives: Hand-editing `ProjectSettings/QualitySettings.asset` was
rejected because Unity owns quality tier serialization and the current Android
row is shared with `Abyss (Low)`, not a dedicated Quest row. Adding a hard build
failure inside every Android preprocess was rejected for this slice because the
project may still need non-VR Android/editor CI paths; the Python hard flag
already provides an explicit CI failure mode.

Scalability potential: Low/Quest targets now expose the exact missing render
route instead of hiding behind packages, Vulkan, and asset presence. Middle,
high, and ultra targets keep PC quality rows untouched until a Unity
import-aware route fix creates or selects a dedicated Android/Quest row.

Hardware Impact: 0 runtime us claimed. This is route forensics and gate
precision only; no player build, URP import, or headset proof was run.

## Decision 046 - Add Unity-Side Quest Android Quality Route Fixer

Problem: R33 proved the Quest URP route gap but did not provide a safe way to
close it. Android still points at default quality index `1`; the Quest URP
asset exists but is not serialized as Android's selected render pipeline route.
A hand edit to `ProjectSettings/QualitySettings.asset` would be brittle because
Unity owns the quality-row schema, platform include/exclude maps, and per
platform default quality table.

Solution: Extend `QuestVulkanRenderPipelineConfigurator` with
`WireQuestAndroidQualityRouteForCi()`. The method resolves the Quest URP asset,
creates or updates a dedicated `Quest (VR)` quality row through
`QualitySettings.GetQualitySettings()` and `SerializedObject`, assigns the
Quest URP asset, applies Quest-safe quality values, includes Android only on
that row via `QualitySettings.TryIncludePlatformAt`, excludes Android from all
other rows via `QualitySettings.TryExcludePlatformAt`, and sets the serialized
`m_PerPlatformDefaultQuality.Android` entry. `PlatformPortabilityProofAudit.py`
is schema v5 and detects that this fixer route exists before Unity execution.

Rejected Alternatives: Direct YAML rewriting was rejected because it would
bypass Unity's importer and risk corrupting platform route metadata. Reusing the
existing `Abyss (Low)` row was rejected because weak-PC low quality and Quest
standalone VR have different render constraints. Launching Unity or dotnet was
rejected in this slice because the user explicitly forbade rebuilds until
needed and static tool verification was sufficient for the code path.

Scalability potential: Low/Quest gets a dedicated Android VR route with reduced
shadows, mip pressure, texture filtering, terrain/tree distances, and no HDR/MSAA
assumption. Middle/high/ultra desktop rows stay intact so visual overkill on PC
does not get pulled down by Quest constraints. The route changes platform
fidelity selection only; it does not alter gameplay truth, DTO layout, save
identity, or authority flow.

Hardware Impact: 0 runtime us claimed until Unity executes the fixer and a
Quest build/profile exists. Static value is eliminating the riskiest current
path: Android accidentally booting with the PC low URP asset instead of the
Quest VR URP asset.

## Decision 047 - Add Unity-Side Android OpenXR Provider Route Fixer

Problem: XR packages are present, Android is ARM64/IL2CPP/Vulkan-ready, and
the Android manifest contains VR markers, but serialized provider proof is
absent. `ProjectSettings.asset` still has `m_BuildTargetVRSettings: []`, and no
XR Management settings asset exists under `Assets`. Relying on package presence
alone would be a false readiness claim; hand-editing Unity YAML would risk
corrupting XR Management importer-owned references.

Solution: Extend `XrPlatformReadinessValidator` with
`WireAndroidOpenXrProviderRouteForCi()`. The route uses Unity XR Management's
own editor API surface: resolve/create `XRGeneralSettingsPerBuildTarget`,
create Android manager settings if missing, assign
`UnityEngine.XR.OpenXR.OpenXRLoader` through
`XRPackageMetadataStore.AssignLoader`, create Android `OpenXRSettings`, and set
`OpenXRSettings.RenderMode.SinglePassInstanced`. The validator now checks
`XRManagerSettings.activeLoaders` for the OpenXR loader and only treats legacy
empty `m_BuildTargetVRSettings` as fatal when no XR Management route exists.
`Hecton8.Editor.asmdef` now explicitly references the XR package assemblies
needed by this editor-only route. The static audit is schema v6 and reports
`xrProviderRouteFixerPresent` plus `xrProviderRouteValidatorPresent` without
claiming serialized provider proof.

Rejected Alternatives: Direct ProjectSettings/XR asset YAML mutation was
rejected because Unity owns fileIDs and loader assets. Treating
`m_BuildTargetVRSettings` as the only valid proof was rejected because XR
Plug-in Management is the active provider path and may leave the legacy list
empty. Launching Unity or dotnet was rejected because the current mandate
forbids rebuild/import until needed; static source/tooling proof was sufficient
for this slice.

Scalability potential: Low/Quest standalone gets an explicit Android OpenXR
route and single-pass stereo default once the fixer is executed. Middle/high
desktop paths are not touched; PC visual overkill remains on its own quality and
provider routes. The change affects provider/fidelity routing only; it does not
change gameplay truth ownership, DTO layout, save identity, or authority route.

Hardware Impact: 0 runtime us claimed. Expected runtime value after Unity
execution is preventing mobile VR from falling back to no XR provider or
multi-pass stereo, but that remains `PENDING VERIFICATION` until Unity import,
build, and headset/profiler evidence exist.

## Decision 048 - Collapse Route Repair Into One CI Entrypoint

Problem: R34 and R35 added the correct Unity-side fixers, but the route still
depended on two separate execution points: Quest URP/quality routing and Android
OpenXR provider routing. That leaves a human/CI ordering hazard: running one
fixer without the other would still produce red serialized proof and could hide
which route was skipped.

Solution: Add `PlatformPortabilityRouteRepairer.WireAndroidQuestXrRoutesForCi()`.
The orchestrator calls `ConfigureQuestAssetsForCi()`,
`WireQuestAndroidQualityRouteForCi()`,
`WireAndroidOpenXrProviderRouteForCi()`, and then
`ValidateAndroidXrReadinessForCi()` in one editor-only path. A `.meta` file was
added for stable Unity asset identity. `PlatformPortabilityProofAudit.py` is
schema v7 and detects `androidQuestXrRouteRepairerPresent`.

Rejected Alternatives: A shell script that invokes two Unity menu methods was
rejected because it would duplicate Unity routing knowledge outside the editor
assembly and still allow partial execution. Merging all logic into one large
class was rejected because the Quest quality route and XR provider route have
different API owners. Running Unity import now was rejected under the explicit
no-rebuild/no-import mandate.

Scalability potential: Low/Quest devices get one future CI route to wire Quest
URP, Android-only quality, OpenXR provider, and single-pass stereo together.
Desktop visual-overkill routes remain isolated because the orchestrator targets
Android route ownership only. No gameplay truth, DTO layout, save identity, or
authority path changes.

Hardware Impact: 0 runtime us claimed. The operational gain is reducing future
route-repair error surface from two manual calls to one CI method. Device impact
remains `PENDING VERIFICATION` until Unity executes the method and Quest
captures exist.

## Decision 049 - Split Data Monolith Route Proof From Payload Proof

Problem: The platform audit previously exposed only `dataMonolithPresent`.
That correctly failed because
`Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is absent, but
it hid a useful distinction: the editor bake/validation machinery exists while
the active runtime payload does not. Without that split, the next engineer sees
only a red artifact and cannot tell whether the missing work is tool creation,
Unity execution, source data population, or payload validation.

Solution: Upgrade `PlatformPortabilityProofAudit.py` to schema v8 with
`dataMonolithBakeRoute` evidence and two readiness flags:
`dataMonolithBakeRoutePresent` and `dataMonolithValidationRoutePresent`. The
route detector requires the compiler path, output path token, source/balance
folder tokens, command-line bake route, prebuild gate, output checksum
validation, atomic temp-write/replace path, little-endian editor guard,
production coverage gate, and the external `Tools/h8bin_validator.py` static
validator. `dataMonolithPresent` remains tied only to the active
`static_data.h8bin` payload file.

Rejected Alternatives: Generating a dummy `.h8bin` was rejected because it would
fabricate runtime payload proof. Treating compiler presence as green readiness
was rejected because Data Monolith readiness requires the active payload plus
import/bake/boot validation. Running Unity bake/import was rejected in this
slice under the explicit no-rebuild/no-Unity mandate; static audit proof was
sufficient to improve the forensic surface.

Scalability potential: Low/MX350/Quest paths need compact binary tables and
fast fail if the active payload is missing. Middle/high/ultra can carry richer
editor manifests and validation reports, but runtime truth remains the same
binary payload route. This change does not touch gameplay truth ownership, DTO
layout, save identity, or authority route.

Hardware Impact: 0 runtime us claimed. The gain is diagnostic precision: the
audit now separates "route exists" from "payload exists", so CI can fail on the
missing runtime artifact without losing evidence that the bake path is already
present. Runtime/device impact remains `PENDING VERIFICATION` until Unity
executes the bake/import path and boot/device validation artifacts exist.

## Decision 050 - Split Addressables Route Proof From Content Artifact Proof

Problem: Addressables was reported as a binary content blocker only through
`addressablesContentPresent`. The package, ContentAuthority validator, runtime
prewarm, and handle lifecycle routes already exist, but the actual
`Assets/AddressableAssetsData` content artifact is empty. A single red flag hid
whether the missing work was package installation, validation route creation,
runtime lifecycle discipline, or Unity-generated settings/groups/catalog data.

Solution: Upgrade `PlatformPortabilityProofAudit.py` to schema v9 with
Addressables package, content route, and runtime lifecycle route fields.
`addressablesContentRoutePresent` requires the package plus
ContentAuthority validation, prebuild gate, required tier-group gate, and
`ContentAssetHashMap` hash-first route. `addressablesRuntimeLifecycleRoutePresent`
requires package presence plus bootstrap dependency prewarm,
AssetLifecycleGovernor async load tracking, blind-frame release, and telemetry
dump route. `addressablesContentPresent` remains tied only to real files inside
`Assets/AddressableAssetsData`.

Rejected Alternatives: Creating Addressables settings/groups from Python or
hand-writing Unity `.asset` files was rejected because Unity owns the importer
schema and fileIDs. Treating package/runtime route presence as streaming
readiness was rejected because `Docs/QUALITY_GATES.md` requires content proof.
Running Unity Addressables build/import was rejected in this slice under the
current no-Unity/no-rebuild mandate.

Scalability potential: Low/MX350/Quest paths need compact Core groups,
RequestedAssetAndDependencies load mode, and aggressive blind-frame release.
Middle/high/ultra can add High_Res and Overkill content groups after Unity
generates valid settings/catalogs. This change does not alter gameplay truth,
DTO layout, save identity, or authority route; it separates Unity object/visual
delivery from Data Monolith world truth.

Hardware Impact: 0 runtime us claimed. The diagnostic gain is precise CI
classification: Addressables package/runtime discipline can be tracked without
claiming streaming content readiness. Runtime/device impact remains
`PENDING VERIFICATION` until Unity-generated Addressables settings/groups,
catalog build output, Memory Profiler, and target-device storage captures exist.

## Decision 051 - Classify Job Completion Before Refactoring Runtime Sites

Problem: The previous `.Complete()` evidence surface was too blunt. A raw
`JobHandle.Complete()` in `Update()` is a frame-path stall candidate, a forced
completion in teardown is a lifecycle concern, and a cold generator API barrier
has a different failure mode. Treating all sites as equal encourages either
false panic or suppressing real frame-path blockers.

Solution: Add `Tools/JobCompletionAudit.py` and wire it into
`Docs/QUALITY_GATES.md`. The gate now classifies editor/test/offline,
teardown, dispatcher-polled, frame-path raw/forced, and raw runtime completion
sites separately. Current project scan: findings `531`, frame-path blockers
`0`, raw runtime blockers `6`. The six owner-review sites are two canonical
Core dispatcher helper completions and four MapMagic cold sync generator
barriers.

Rejected Alternatives: Rewriting the MapMagic generator completions blindly was
rejected because the MapMagic `Generate` API must return concrete matrix/object
products; changing that route requires dispatch caller review. Marking all
`.Complete()` sites green was rejected because the raw runtime queue still
contains unmanaged barriers that need owner review. Running Unity/player
profiling was rejected in this static slice under the no-rebuild/no-Unity
mandate.

Scalability potential: Low/MX350/Quest benefits from preventing accidental
same-frame schedule/readback stalls in hot phases. Middle/high/ultra can keep
dispatcher-polled fences while using saved main-thread budget for richer
visuals. The classifier changes proof routing only; it does not change gameplay
truth ownership, DTO layout, save identity, or authority route.

Hardware Impact: 0 runtime us claimed. The measurable gain is future frame-path
risk isolation: `--fail-on-frame-path` is currently green, while
`--fail-on-raw-runtime-complete` remains red for six owner-review sites.
Runtime/device impact remains `PENDING VERIFICATION` until profiler captures
prove the cold sync barriers are not active frame stutters.

## Decision 052 - Burn Down Burst Flags In Leaf Slices Only

Problem: Burst flag debt remained broad, but changing giant domains in one pass
would mix syntax-risk, authority-risk, and performance-policy risk. The safe
lane was attr-only leaf cleanup where existing math mode could be preserved or
deterministic mode was clearly required by save/inventory/kinematics truth.

Solution: Added explicit Burst flags to 15 small or attr-only files:
`BrineLayerMath`, `ItemSalinityCorrosionJob`,
`SaveIndexedSectorBoundsMath`, `AcousticEcholocationRaymarch`,
`LogisticsPipeRoutingKernel`, `LogisticsPipeTransportScheduler`,
`SurfaceWeatherMath`, `FluidImpulseJob`, `FaunaTentacleConstrainedIk`,
`QuestStateManager`, `ToolKinematicsContracts`, `SaveBinaryStorage`,
`SomaticKinematicsRuntime`, `InventorySoAUtility`, and `PlayerInventory`.
Existing `FloatMode.Fast` was preserved for visual/tooling math. Save,
inventory, and somatic kinematics jobs now use `FloatMode.Deterministic`.

Rejected Alternatives: Editing `CombatDamageRuntime.cs` and
`Inventory/Shinobu19EconomyLedger.cs` in the same pass was rejected because
those are larger owner domains and need focused review. Suppressing the audit
or resetting a baseline was rejected because the remaining debt is real. A
Unity/dotnet rebuild was rejected because static syntax and audit checks were
the required scope here.

Scalability potential: Low/MX350/Quest avoids Burst safe-default drift on small
jobs and keeps deterministic truth jobs stable across ARM64/x86. Middle/high
can still use the same job contracts without binary quality switches. Ultra
does not get different gameplay truth; it can spend saved CPU/GPU budget on
visual overkill elsewhere.

Hardware Impact: 0 runtime us claimed. Static debt moved from
`burstMissingCompileSynchronously=94` to `67`, `burstMissingFloatMode=33` to
`24`, and `burstMissingFloatPrecision=35` to `26`. Runtime speed and thermal
impact remain `PENDING VERIFICATION` until Burst Inspector/player profiler
evidence exists.

## Decision 053 - Keep DataVault Red Instead Of Resetting Baseline

Problem: DataVault counters changed again under parallel agent churn. A stale
answer would understate current risk, and a baseline reset would hide the exact
kind of native ownership regression that hurts ARM64/Quest memory stability.

Solution: Re-run DataVault gates after the R39/R40 work. The default gate fails
closed because no active baseline is configured. Candidate baseline checks also
fail: forbidden constructors are now `1233` versus candidate baselines `1149`
and `1141`; forbidden field declarations are `1739` versus v3 baseline `1719`.
The largest constructor growth is editor/offline bake surface. Runtime field
growth still exists and includes `HabitatConstructionManager`, `MapMagicBridge`,
`ModularEquipmentEngine`, `GlobalShaderDispatcher`, and `ScannerTool`.

Rejected Alternatives: Resetting the baseline was rejected because it would
convert real regressions into accepted debt. Fixing every DataVault hit in this
slice was rejected because many hits are owner-domain or editor/offline bake
surfaces and require separate ownership review. Treating job-input declarations
as persistent leaks was rejected; the v3 classifier still separates
persistent declarations from job inputs.

Scalability potential: Low/MX350/Quest needs predictable native ownership and
flat persistent memory. Middle/high/ultra can tolerate larger editor/offline
bake scratch, but runtime truth must still use owner-local buffers or Vault
routes. This decision preserves the proof boundary instead of diluting it.

Hardware Impact: 0 runtime us claimed. Current static risk is high:
`persistentDeclarations=1053` and `jobInputDeclarations=3952`. Runtime/device
impact remains `PENDING VERIFICATION` until owner-domain burn-down and
Memory Profiler/NativeMemorySentinel proof exist.

## Decision 054 - Separate Runtime DataVault Regression From Bake-Surface Noise

Problem: The DataVault gate was correctly red, but the constructor side still
mixed runtime native ownership with editor/offline bake constructors. That made
the burn-down order noisy: bake pipelines can be fixed by cold allocator and
sentinel policy, while runtime field declarations directly affect Quest/Deck
memory ownership and frame stability.

Solution: `DataVaultSovereigntyAudit.py` now strips comments/string literals
before constructor matching, emits constructor totals by execution surface, and
adds `--fail-on-runtime-regression`. The total no-regression gate still fails
globally; the runtime-only gate isolates the current five runtime field
declaration deltas: `HabitatConstructionManager`, `MapMagicBridge`,
`ModularEquipmentEngine`, `GlobalShaderDispatcher`, and `ScannerTool`.

Rejected Alternatives: Resetting the baseline was rejected because it would
accept new debt. Rewriting editor bake systems in this pass was rejected
because it is a different ownership problem from runtime persistent memory.
Suppressing `DispatcherJobFence` raw completes was rejected; the audit now
reports them as `DispatcherFenceInternalRawComplete` so the canonical fence is
visible while owner-domain blockers remain real.

Scalability potential: Low/MX350/Quest needs the runtime queue first because
flat native ownership controls memory pressure and thermal stability. Middle
and high tiers still need the same runtime ownership route; editor/offline bake
scratch does not buy runtime visual overkill until it is converted into baked
assets and Data Monolith/import proof exists. Ultra may keep heavier offline
bake quality only when runtime payloads remain fixed and aligned.

Hardware Impact: 0 runtime us claimed. Static proof improved: runtime
forbidden constructors are `800`, editor/offline forbidden constructors are
`402`, plugin forbidden constructors are `30`, and runtime field-declaration
regression is now exactly five file deltas. Job completion raw runtime blockers
fell from `6` to `4` by separating the canonical Core fence implementation.
Runtime/device impact remains `PENDING VERIFICATION`.

## Decision 055 - Burn Down ScannerTool NativeArray Field Without Losing Black Box

Problem: After separating native view structs from persistent owner fields, the
runtime-only DataVault regression was reduced to one real owner leak:
`ScannerTool` cached `_scannerBlackBoxRing` as a `NativeArray` class field even
though the ring is already owned by `GlobalDataVault` through
`BufferID.ShinobuScannerToolBlackBox`.

Solution: Removed the cached `NativeArray<ScannerBlackBoxEntry>` field.
`ScannerTool` keeps only `_scannerBlackBoxHandle` plus `_scannerBlackBoxVault`.
`EnsureScannerBlackBoxVault` and `TryReadScannerBlackBoxRing` resolve the
generation handle into local views for write/dump operations. The 300-frame
black-box behavior remains intact, but persistent native ownership stays in the
Vault route.

Rejected Alternatives: Deleting the scanner black-box ring was rejected because
postmortem telemetry is required. Suppressing `ScannerTool.cs` in the audit was
rejected because the class field was a real ownership smell. Moving the ring to
a new private allocation was rejected because it would violate the Vault route
and make ownership worse.

Scalability potential: Low/MX350/Quest benefits from one less persistent
NativeArray alias in a UI/tool runtime owner. Middle/high/ultra keep the same
black-box fidelity and can still use the scanner telemetry for forensic dumps.
No quality-tier branch or gameplay truth change was introduced.

Hardware Impact: 0 runtime us claimed. Static DataVault runtime no-regression
now passes against the HFI v3 candidate baseline. Persistent declaration count
fell to `1052`; forbidden declaration count fell to `1305`. Runtime/device
impact remains `PENDING VERIFICATION` until Unity/player memory proof exists.

## Decision 056 - Keep MapMagic Sync Barriers Visible Without Mislabeling Them

Problem: After canonical Core fence internals were separated, four raw
`JobHandle.Complete()` sites remained. Source review showed they are inside
MapMagic `Generate` paths with existing `COLD SYNC JOB` comments and concrete
matrix/product publication requirements before returning to the plugin graph.
Counting those as generic owner-domain runtime raw blockers made the hard gate
noisy and encouraged a dangerous blind async rewrite.

Solution: `JobCompletionAudit.py` now classifies MapMagic plugin graph
generation barriers as `PluginSynchronousGeneratorRawComplete` and exposes a
separate `pluginSyncCompleteCount`. The normal raw-runtime hard gate now
isolates owner-domain raw runtime blockers, while
`--fail-on-plugin-sync-complete` remains available when the plugin graph
lifecycle itself is being reviewed.

Rejected Alternatives: Hiding the MapMagic sites was rejected because they are
still synchronous barriers. Rewriting them to async was rejected because
MapMagic `Generate` must return concrete products and the caller lifecycle was
not changed. Treating them as frame-path blockers was rejected because the
static evidence points to cold generator publication, not Tick/Update hot-path
completion.

Scalability potential: Low/MX350/Quest benefits from a cleaner scheduler gate:
real frame/raw owner blockers can be blocked without conflating cold content
generation. Middle/high/ultra can still use heavier MapMagic bake products,
but plugin sync cost remains a review surface until a measured graph handoff
exists.

Hardware Impact: 0 runtime us claimed. Static JobCompletion recapture now
reports frame-path blockers `0`, raw runtime blockers `0`, and plugin
synchronous generator review sites `4`. Runtime/device impact remains
`PENDING VERIFICATION` because no Unity import, player build, profiler, or
device run was executed.

## Decision 057 - Split Editor Bake Scratch From Persistent Native Ownership

Problem: The DataVault global no-regression gate still failed on editor/offline
growth, but the report did not distinguish `Allocator.TempJob` bake scratch
from `Allocator.Persistent` editor caches/sessions. That made the next burn
queue misleading and risked pushing disposable bake-local buffers into
`GlobalDataVault`, which would be fake global ownership.

Solution: `DataVaultSovereigntyAudit.py` now records allocator kind for direct
`new NativeArray<T>` findings and emits allocator splits for forbidden
constructors. It also classifies editor/offline multi-frame bake session fields
as `editorOfflineSessionScratchField`, while static preview caches are
`editorOfflinePersistentPreviewField` and remain gate-relevant.

Rejected Alternatives: Allowlisting all editor/offline native collections was
rejected because `HadalTrenchPreviewStore` is a real static native cache.
Migrating local `TempJob` bake buffers to `GlobalDataVault` was rejected
because DataVault is cross-domain native ownership, not a dumping ground for
throwaway editor scratch. Resetting the baseline was rejected again.

Scalability potential: Low/MX350/Quest does not directly benefit from editor
bake scratch changes, but it benefits from preventing fake runtime ownership
routes and preserving Data Monolith discipline. Middle/high/ultra can keep
heavier offline bake products, provided runtime payloads are fixed, aligned,
and loaded through the owner route.

Hardware Impact: 0 runtime us claimed. Static proof improved: runtime-only
DataVault regression remains PASS; editor/offline constructor split is now
`Persistent=30`, `Temp=31`, `TempJob=317`, `Unknown=24`. Forbidden
declarations dropped to `1279`; persistent declarations dropped to `1022`;
editor session scratch declarations are `22`; editor persistent preview
declarations are `4`. Runtime/device impact remains `PENDING VERIFICATION`.

## Decision 058 - Track Hadal Trench Editor Preview Cache Through H8Memory

Problem: `HadalTrenchPreviewStore` was a true static editor preview cache with
two `Allocator.Persistent` `NativeArray` fields and two direct constructors.
It had reload/quit disposal, but ownership was still invisible to the native
memory tracker and the DataVault audit correctly kept it gate-relevant.

Solution: The preview store now allocates `s_faults` and `s_vents` through
`H8Memory.Allocate<T>` with `SystemID.ContentAuthority`, releases through
`H8Memory.Release`, and carries the `H8MEMORY_TRACKED_EDITOR_PREVIEW` marker.
The editor-only Hadal Trench asmdef references `Hecton8.Core.Memory`; the
runtime Hadal Trench asmdef was not changed.

Rejected Alternatives: Leaving raw constructors and suppressing the file was
rejected because it would hide real editor persistent ownership. Moving preview
arrays into `GlobalDataVault` was rejected because this is editor preview
scratch, not runtime cross-domain truth. Adding a runtime assembly dependency
was rejected to preserve the compile wall.

Scalability potential: Low/MX350/Quest runtime behavior is unchanged because
this is editor-only. The value is preventing editor bake tooling from masking
native leaks before content is transformed into runtime payloads. Middle/high
and ultra tiers keep the same preview fidelity while making ownership visible.

Hardware Impact: 0 runtime us claimed. Static DataVault recapture improved:
direct constructors `1238 -> 1236`, forbidden constructors `1232 -> 1230`,
editor/offline forbidden constructors `402 -> 400`, forbidden declarations
`1279 -> 1277`, and editor/offline `Persistent` constructor hits `30 -> 28`.
`AssemblyDependencyAudit.py` reports cycles `0`. Runtime/device impact remains
`PENDING VERIFICATION`.

## Decision 059 - Recover DataVault No-Regression Without Baseline Reset

Problem: After R46, runtime DataVault no-regression was green, but full
candidate no-regression still failed on two editor/offline files:
`GeographySanityPipeline.cs` and `TopographyForgeGenerator.cs`. Both held
multi-frame `Allocator.Persistent` arrays through direct constructors, so they
were real ownership visibility debt even though they are editor/offline tools.

Solution: Route persistent editor/offline arrays in those files through
`H8Memory.Allocate<T>` and `H8Memory.Release` with `SystemID.ContentAuthority`.
Keep `TempJob` bake-local arrays local and reportable as transient scratch.
Regenerate runtime and full DataVault reports without changing the baseline.

Rejected Alternatives: A baseline reset was rejected because it hides debt.
Moving short-lived `TempJob` scratch arrays into `GlobalDataVault` was rejected
because DataVault is not a global heap. Suppressing whole editor folders was
rejected because static preview/session ownership can leak across editor
reloads if it is not tracked.

Scalability potential: Low/MX350/Quest runtime is unchanged. The value is
preventing editor bake tooling from masking native ownership before the output
is shipped into Data Monolith/runtime payloads. Middle/high/ultra retain the
same bake quality while ownership proof becomes machine-checkable.

Hardware Impact: 0 runtime us claimed. Static DataVault proof now reports
full no-regression PASS; direct constructors `1215`, forbidden constructors
`850`, runtime forbidden constructors `800`, editor/offline forbidden
constructors `20`. Runtime/device impact remains `PENDING VERIFICATION`.

## Decision 060 - Split Runtime-Asset Compute Risk From Runtime-Referenced Risk

Problem: `PlatformPortabilityProofAudit.py` reported risky compute thread
groups, but the hard failure flag only blocked runtime-referenced assets.
`Hecton_SonarMap.compute:59` uses `[numthreads(8,8,8)]` = `512` threads and
lives under runtime assets, yet current static reachability says
`UnreferencedAsset`. That is still a portability risk: a serialized prefab or
caller can reconnect it later without changing the compute source.

Solution: Upgrade the platform audit to schema v10 and add
`--fail-on-runtime-asset-high-risk-compute`. Keep
`--fail-on-high-risk-compute` for the narrower runtime-referenced gate. This
makes dormant runtime compute debt visible without forcing a blind shader edit.

Rejected Alternatives: Changing `[numthreads]` in `Hecton_SonarMap.compute`
was rejected because the kernel math is tied to an 8x8 XY lane for predator
slots and there is no active dispatch caller proof in this slice. Treating
unreferenced runtime compute assets as safe was rejected because asset
reachability can change through Unity serialization.

Scalability potential: Low/Quest/TBDR benefits from blocking latent 512-thread
runtime compute assets before they enter a mobile route. Middle/high/ultra can
still keep larger kernels where dispatch review proves they are gated,
referenced intentionally, and have a mobile variant or limiter.

Hardware Impact: 0 runtime us claimed. Static platform audit now reports
schema v10, sustained performance `true`, runtime asset risky compute groups
`3`, runtime-referenced risky compute groups `0`, Quest URP wired `false`, and
XR provider serialized proof `false`. Runtime/device impact remains
`PENDING VERIFICATION`.

## Decision 061 - Add File-Level Compute Dispatch Thread-Group Query Gate

Problem: The platform audit caught risky `[numthreads]` declarations but did
not prove whether C# dispatch callers size workgroups from shader metadata.
Hardcoded dispatch constants such as `(count + 63) >> 6` can silently break
when a shader kernel changes, and they hide mobile/TBDR portability risk even
when the compute asset itself has a portable group size.

Solution: Upgrade `PlatformPortabilityProofAudit.py` to schema v11 and add a
C# compute dispatch caller surface. The audit scans runtime and editor C#
files for `.Dispatch` / `.DispatchCompute`, classifies caller execution surface,
and reports files whose dispatch caller lacks file-level
`GetKernelThreadGroupSizes`. Add
`--fail-on-runtime-compute-dispatch-without-threadgroup-query` as a separate
hard flag from runtime asset and runtime-referenced `[numthreads]` gates.

Rejected Alternatives: Editing `Hecton_SonarMap.compute` was rejected because
its 8x8 lane math has no active caller proof in this slice. Treating dispatch
groups as safe because a constant equals 64 was rejected because shader kernels
are assets and can change without C# compile failure. Counting arbitrary
custom `Dispatch` methods as compute was rejected; the scanner now ignores
plain `.Dispatch(` unless the file or line has compute-shader context.

Scalability potential: Low/Quest/TBDR gets a hard gate before unchecked
dispatch callers enter mobile builds. Middle/high/ultra retain large kernels
where caller code proves it reads kernel metadata and scales dispatch groups
from actual shader thread sizes.

Hardware Impact: 0 runtime us claimed. Static result: compute dispatch calls
`115`, runtime dispatch calls `111`, dispatch calls without file-level
thread-group query `69`, runtime `65`, caller files without query `25`,
runtime `23`. Runtime/device impact remains `PENDING VERIFICATION`.

## Decision 062 - Quest Quality Route Blocked by Existing Unity Compile Wall

Problem: Quest URP remains unwired to Android default quality. The correct
route is the existing Unity Editor method
`QuestVulkanRenderPipelineConfigurator.WireQuestAndroidQualityRouteForCi`,
because manual YAML edits risk corrupting PC quality tiers. A batchmode Unity
attempt was launched only after CPU was below 50% and no Unity/dotnet/csc
process was active, but the project did not reach method execution.

Solution: Capture the compile blockers exactly and avoid fake settings claims.
Patch only narrow editor/API compatibility sites exposed by the route attempt:
remove nonexistent `MeshUpdateFlags.DontRecalculateNormals` from
`WreckageForgeWindow.cs`, `VoxelTerrainSeamPreviewGizmo.cs`, and
`VoxelTerrainSeamBinderPipeline.cs`; add the missing `UnityEditor.UIElements`
import for `ObjectField` in `HabitatDamageBakePipeline.cs`; and replace removed
`Mesh.MeshData.GetVertexAttribute` calls with Unity 6000
`GetVertexAttributeFormat`, `GetVertexAttributeDimension`, and
`GetVertexAttributeStream` accessors in Habitat/Interior offline bakers. Stop
the orphan Unity-owned Roslyn `dotnet` process after Unity exited and the
parent process was gone.

Rejected Alternatives: Manual `QualitySettings.asset` editing was rejected.
Retaining invalid Unity 6000 API calls was rejected because they block all
Unity Editor automation. Broadly rewriting OfflineWreckage, Habitat,
VoxelTerrain, InteriorClutter, or MockDomain code was rejected because HFI owns
portability proof; the Burst ILPP exception remains a separate owner-domain
compile wall unless a narrow owner-approved fix is identified.

Scalability potential: Low/Quest cannot receive the proper URP quality route
until Unity import succeeds. Middle/high/ultra PC quality rows were left
unchanged. The valid path remains import-aware Unity API mutation, not serialized
file surgery.

Hardware Impact: 0 runtime us claimed. Static proof still reports Quest URP
wired `false`. Unity compile/import proof remains `FAIL` from the captured
attempt; local Unity 6000 API blockers were reduced, but no rerun was launched
after CPU preflight reported `81%`, above the project gate.

## Decision 063 - Reconfirm Job Completion Classification Before Touching Call Sites

Problem: The platform queue included `.Complete()` classification, but a broad
source rewrite would be unsafe. Raw textual `.Complete()` hits include editor
tools, teardown drains, dispatcher fence internals, and plugin generator
barriers; changing them without owner-domain context can break deterministic
completion windows.

Solution: Re-run the existing `JobCompletionAudit.py` and tests. Treat
frame-path blockers and raw runtime blockers as hard risk, while leaving
editor/test, teardown, canonical dispatcher fence, and MapMagic plugin
synchronous generator barriers in separate classifications.

Rejected Alternatives: Replacing every `.Complete()` with dispatcher polling
was rejected because teardown must be allowed to drain and the canonical fence
helper owns its internal completion window. Suppressing all textual hits was
rejected because plugin generator barriers still need review if that graph is
edited.

Scalability potential: Low/Quest/MX350 avoids accidental main-thread stalls by
keeping frame-path blockers at `0`. Middle/high/ultra can still perform
explicit synchronization in owned teardown or offline generator paths where the
frame budget is not the runtime contract.

Hardware Impact: 0 runtime us claimed. Static recapture reports findings
`534`, frame-path blockers `0`, raw runtime blockers `0`, and plugin sync
completes `4`. Runtime/device impact remains `PENDING VERIFICATION`.

## Decision 064 - Remove MockDomain Static Burst Function-Pointer Trigger

Problem: The Quest route Unity log captured a Burst ILPP exception in
`Hecton8.MockDomain.Runtime`. The suspect source was a static readonly
`BurstCompiler.CompileFunctionPointer<PhysicsApplyForceDelegate>` initializer
whose target method was an empty no-op callback. That adds IL post-processing
risk to a mock compile-wall proof assembly without buying runtime behavior.

Solution: Remove the static function-pointer compilation and no-op
`[BurstCompile]` callback from `MockContractImplementation.cs`. Keep the
`PhysicsFacade` contract shape and return a facade with a default no-op
function pointer plus the supplied buffer handle.

Rejected Alternatives: Rewriting `GlobalContracts.PhysicsFacade` was rejected
because it is a core contract surface. Keeping the static initializer was
rejected because it reproduces the ILPP trigger in a mock domain. Creating a
managed delegate fallback was rejected because the facade contract is unmanaged
function-pointer oriented.

Scalability potential: Low/Quest gets lower import risk by removing an
unnecessary Burst ILPP path from a no-op mock. Middle/high/ultra behavior is
unchanged because this mock implementation did not apply force before the
change.

Hardware Impact: 0 runtime us claimed. Static source now has no
`BurstCompiler`, `CompileFunctionPointer`, `FunctionPointer<`, `[BurstCompile]`,
`using Unity.Burst`, or `using Unity.Mathematics` token in
`MockContractImplementation.cs`. Unity import proof remains `PENDING` because
CPU preflight blocked rerun.

## Decision 065 - Burn Down Burst Flags In Leaf Files Only

Problem: `PolishMandateStaticAudit.py` still reported Burst attribute drift,
but a broad rewrite across hot owner domains would create merge and semantic
risk. The safe slice was files with already-correct `FloatMode.Fast` and
`FloatPrecision.Standard` that only lacked `CompileSynchronously`.

Solution: Add `CompileSynchronously = true` to four editor erosion bake jobs
and ten VFX debris jobs. These attributes now match the HECTON-8 Burst mandate
without changing job fields, schedules, math, memory ownership, or gameplay
truth.

Rejected Alternatives: Bulk editing every Burst attribute was rejected because
deterministic/rollback domains need owner review for `FloatMode.Deterministic`.
Changing any job math or dispatch cadence was rejected because this slice only
addresses compiler directive drift.

Scalability potential: Low/Quest/MX350 gets more predictable Burst compilation
behavior for touched jobs. Middle/high/ultra behavior is unchanged; this does
not alter quality curves, payload layout, or output identity.

Hardware Impact: 0 runtime us claimed. Static audit reduced
`burstMissingCompileSynchronously` from `67` to `53`; `burstMissingFloatMode`
remains `24`, and `burstMissingFloatPrecision` remains `26`. Runtime/device
impact remains `PENDING VERIFICATION`.
