# Rationale_HFI_AUDIT

Agent: HFI_AUDIT
Domain: Cross-domain static integration audit
Status: PENDING VERIFICATION
Date: 2026-05-19

## Decision 001 - Create Active AgentLog For Current Dirty-Tree Findings

Problem: The active `Docs/AgentLogs` folder had no `LOG_HFI_AUDIT.md`, while the user asked that findings be written into AgentLogs. Previous HFI status/rationale were archived under `Docs/Archive/Batch009`, so new dirty-tree findings needed an active append target.

Solution: Create `Docs/AgentLogs/LOG_HFI_AUDIT.md` and `Docs/AgentLogs/Rationale_HFI_AUDIT.md` with evidence-class labels and `PENDING VERIFICATION` status. Keep findings static/read-only and avoid claiming compile, Unity, profiler, or runtime proof.

Rejected Alternatives: Writing findings only to chat was rejected because user asked for disk logs. Editing runtime code was rejected because this slice is audit-only. Running dotnet/Unity was rejected until CPU/compiler guard and integration order are clean.

Scalability potential: Process-only. Low/Middle/High/Ultra runtime behavior is unchanged; the value is preventing global authority, signal, save, and bootstrap drift before it reaches runtime.

Hardware Impact: 0 runtime us. No player-frame path changed.

## Decision 002 - Keep BufferID Collision Response As Audit-Only

Problem: Static audit found likely compile errors and Vault aliasing hazards: missing `BabelSubtitle*` BufferID enum symbols, duplicate numeric values inside `H8Memory.BufferID`, and several local hard-cast BufferID ranges colliding with existing owners.

Solution: Record the collision set in `LOG_HFI_AUDIT.md` with exact evidence class and owner ranges. Do not patch source immediately because many agents are actively writing the same authority surfaces and a blind renumber would require coordinated route-card/ledger/status updates.

Rejected Alternatives: Silent source renumbering was rejected because it can break other active agents and docs without a compile gate. Ignoring the issue was rejected because numeric `BufferID` collision can corrupt `GlobalDataVault` ownership even when code compiles.

Scalability potential: Process/runtime-indirect. Low-tier devices suffer most from hidden Vault aliasing because corruption forces fallback, extra checks, or crash recovery. High/Ultra also lose visual budget if unrelated systems fight over the same memory keys.

Hardware Impact: 0 runtime us in this audit pass. Future impact depends on source repair and profiler/player validation.

## Decision 003 - Treat Narrow Build Claims As Potentially Stale

Problem: Newly added `.cs` files are untracked and absent from all scanned generated `*.csproj` files. Agent logs may claim narrow build success while the new files were not in the generated compile graph.

Solution: Record generated-project inclusion as a separate evidence class in `LOG_HFI_AUDIT.md`. Compile claims for this batch are downgraded unless the artifact proves Unity import/project regeneration included the new files.

Rejected Alternatives: Treating prior `dotnet build` claims as authoritative was rejected because static project text currently excludes the new source files. Running a new build was rejected because project inclusion must be fixed/regenerated first and CPU/compiler gates still apply.

Scalability potential: Process-only. Prevents false readiness claims that would later burn integration time on low-end target validation.

Hardware Impact: 0 runtime us. No player-frame path changed.

## Decision 004 - Pivot From Local Defects To Product Direction

Problem: The user explicitly deprioritized small defects and asked for global direction. The dirty tree contains broad concurrent work across many domains, so continuing to list local compile/static mines would miss the larger product risk.

Solution: Record a global direction snapshot in `Docs/AgentLogs/LOG_HFI_AUDIT.md`: current vector is coherent but too platform/system-surface-heavy until the first 20-minute playable route is proven. Use "First 20 Minutes Vertical Slice" as the default acceptance lens for future work.

Rejected Alternatives: Continuing with BufferID/meta/compile-mines only was rejected because the user asked to ignore small issues. Broad runtime refactor was rejected because the right correction is product gating, not more horizontal architecture churn.

Scalability potential: Process/runtime-indirect. Low-tier success needs one measured route that sheds density predictably; high/ultra success needs the same route to spend saved budget on visible overkill after the baseline is stable.

Hardware Impact: 0 runtime us in this audit pass. Future savings require profiler evidence on the selected vertical slice.

## Decision 005 - Promote First 20 Minutes As Product Gate

Problem: The project has coherent architecture but too much horizontal system growth before a proven playable chain. Repeating local defects would not answer the user's global-direction concern.

Solution: Create `Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md` and link it from root instructions, architecture indexes, quality gates, runtime plan, roadmap, playtest ledger, marketing workflow, and HFI logs.

Rejected Alternatives: Leaving the route lens only in chat was rejected because agents follow files. Expanding implementation now was rejected because the problem is product focus, not another subsystem.

Scalability potential: Low/Middle/High/Ultra impact is process/runtime-indirect. A measured route gives low-tier load-shed truth first and lets high/ultra visual overkill attach only after the same route is stable.

Hardware Impact: 0 runtime us in this documentation pass. Future impact must be measured on the selected first-20-minutes route.

## Decision 006 - Select Copper Wire As V0 Route

Problem: A general First 20 Minutes contract still leaves agents free to nominate scanner, repair, base, fauna, or visual systems as "route work" without proving the simplest gameplay spine first. Static evidence shows Scanner and Repair Tool recipes are useful but blocked behind `scan.expedition_contact` / `scan.structure_relay` route proof.

Solution: Create `Docs/ARCHITECTURE/FIRST_20_MINUTES_ROUTE_BRIEF.md` and select the Copper Wire route as V0: boot -> world load -> safe exit -> swim -> find copper -> collect cataloged `Data_Copper` -> complete `quest_copper_sample` -> craft `Recipe_CopperWire` -> save/load. Link the route brief into indexes, quality gates, roadmap, playtest ledger, marketing workflow, project x-ray, HFI report, and active HFI log.

Rejected Alternatives: Selecting Scanner was rejected because `Recipe_Scanner` requires `scan.expedition_contact`, whose production unlock route is not statically proven. Selecting Repair Tool was rejected because `Recipe_RepairTool` requires `scan.structure_relay`, also not proven. Leaving only the generic route contract was rejected because it would not stop horizontal system growth.

Scalability potential: Low/Middle/High/Ultra impact is process/runtime-indirect. One measured Copper Wire route gives weak devices a concrete load-shedding target and gives high/ultra devices a real path for visual overkill after the baseline route is stable.

Hardware Impact: 0 runtime us in this documentation pass. Future impact must be measured with Unity/profiler/GC/memory/save-load proof on the route.

## Decision 007 - Record Platform Readiness As Proof Ladder, Not Readiness Claim

Problem: The user asked whether the global authority direction is correct and how ready the project is for PC tiers, macOS, Quest/PICO, PCVR, Steam Deck, and consoles. Static source shows serious scaffolding, but platform settings and payload files still block real readiness claims.

Solution: Create `Docs/Reports/2026-05-19_GLOBAL_AUTHORITY_AND_PLATFORM_PORTABILITY_AUDIT.md` and append R7 to `LOG_HFI_AUDIT.md`. Classify the current state as correct high-risk direction with `PENDING VERIFICATION`, then define a proof ladder starting with Windows/Copper Wire before Linux/Steam Deck, macOS, XR, Quest/PICO, and consoles.

Rejected Alternatives: Claiming broad platform readiness was rejected because XR packages/settings, payloads, device builds, profiler captures, and native plugin parity are absent. Ignoring platform work was rejected because current code already contains XR/foveation/scalability/platform audit surfaces that need governance.

Scalability potential: Process/runtime-indirect. Weak devices need the Copper Wire route and low-tier capture before broad platform promises; high/ultra devices need the same route stable before visual overkill consumers are expanded.

Hardware Impact: 0 runtime us in this audit pass. Future impact must be measured per platform build/profiler artifact.

## Decision 008 - Bootstrap Quest Packages Through Manifest, Not Unity Import

Problem: Quest support was blocked by missing XR Management/OpenXR/Meta OpenXR packages and disabled custom Android manifest/Gradle template. The user installed Android SDK/JDK/NDK through Unity Hub and asked what to download/connect.

Solution: Add stable Unity 6.4-compatible package versions to `Packages/manifest.json`: `com.unity.xr.management` 4.6.0, `com.unity.xr.openxr` 1.17.0, and `com.unity.xr.meta-openxr` 2.5.0. Enable the existing custom Android manifest and Gradle template, replace the Unity template Android package id with `com.danatgames.hecton8`, and set explicit Android target SDK 35.

Rejected Alternatives: Running Unity import/build immediately was rejected because the dirty tree is moving and package resolution can trigger a large compile/import cycle. Adding PICO now was rejected because PICO SDK is not present in Unity Package Registry under the probed package names and official PICO docs require importing the SDK from disk.

Scalability potential: Process/runtime-indirect. Quest 2/3 proof becomes possible only after OpenXR provider settings are generated and a real Android/Quest build is captured; no runtime budget improvement is claimed.

Hardware Impact: 0 runtime us in this edit. Future Quest performance impact must be measured on Quest 2/3 with profiler/thermal/foveation captures.

## Decision 009 - Promote Platform Ladder From Dated Report To Stable Policy

Problem: Stable docs already governed GlobalRegistry, SignalBus, GlobalSignals,
HectonEventBus, and GlobalDataVault well enough, but platform readiness order was
mostly captured in a dated report. Under the project authority spine, dated
reports are evidence snapshots, not permanent policy.

Solution: Add `Docs/ARCHITECTURE/PLATFORM_PORTABILITY_PROOF_LADDER.md`, link it
from both agent instruction files, Docs indexes, Architecture index, and
Quality Gates, and append an R9 static recapture to the active platform/global
report and HFI log.

Rejected Alternatives: Leaving the ladder only in the dated report was rejected
because future agents may miss it or treat it as historical context. Creating a
large platform strategy document was rejected because the needed rule is simple:
Windows/Copper Wire proof first, then climb the device ladder by evidence.

Scalability potential: Process/runtime-indirect. Weak devices get a forced
baseline proof path before visual or XR promises; high-end devices can still add
visual overkill after the same route is stable.

Hardware Impact: 0 runtime us. No player-frame path changed.

## Decision 010 - Update Validators Before More Platform Prose

Problem: The global/platform report still described Quest and PCVR as missing XR packages after `Packages/manifest.json` was already bootstrapped with XR Management, OpenXR, and Meta OpenXR. Leaving that stale language would make later agents chase solved blockers while missing the real remaining blocker: Unity package resolve, XR loader/provider settings, and device proof.

Solution: Refresh the current platform report, create active `Status_HFI_AUDIT.md`, and harden the editor validators. `XrPlatformReadinessValidator` now checks Meta OpenXR package presence, custom Android manifest usage, custom Gradle usage, and ARM64-only Android architecture. `PlatformCompatibilityAudit` now reports Meta OpenXR, custom manifest, custom Gradle, and ARM64-only rows.

Rejected Alternatives: Chat-only verdict was rejected because the project runs from files. Running Unity import/build was rejected because this pass needed static governance refresh, not a large package/compile cycle in a dirty multi-agent workspace. Editing XR ProjectSettings YAML by hand was rejected because Unity should generate XR Plug-in Management provider assets.

Scalability potential: Process/runtime-indirect. Quest, Steam Deck, macOS, and low-end PC proof now fail on the actual remaining evidence gates instead of stale package/setup gates. This prevents broad platform claims before Copper Wire route evidence.

Hardware Impact: 0 runtime us. Validator changes are editor/build-preprocess only; no player-frame path changed.

## Decision 011 - Record R10 As Static Architecture Verdict, Not Risky Refactor

Problem: The user asked for a current global-direction and platform-readiness
verdict while the tree is heavily dirty and many agents are changing authority,
signal, platform, and content surfaces. Static scan found real architectural
pressure: 6169 `GlobalRegistry.` hits, 259 `GlobalSignals.Publish` hits, 48
`HectonEventBus` publish/subscribe hits, 959 persistent native constructor text
hits, 135 exact `Pack = 1` layouts, and unresolved XR package/provider state.

Solution: Update the global/platform report and active HFI log with an R10
classification. Keep the verdict evidence-based: correct direction, high-risk
yellow, not globally failing, missing runtime proof. Avoid source refactors in
this pass because Pack/layout/native-allocation changes require compile,
owner-ledger, Burst/ARM64, and device validation.

Rejected Alternatives: Blindly editing `Pack = 1` structs was rejected because
some may be file/native/GPU contract layouts and changing them without boundary
proof can corrupt data. Moving persistent native allocations into DataVault was
rejected for this pass because many are legitimate cold or owner-local
allocations and need owner-by-owner waivers. Hand-editing XR provider YAML was
rejected because Unity should generate XR Plug-in Management/OpenXR settings
after package import.

Scalability potential: Process/runtime-indirect. Weak devices benefit when
global surfaces are route-carded, signal lanes are configured, and allocations
are owned before profiling. High/Ultra devices benefit only after the same
route is stable enough to spend saved budget on visual overkill instead of
recovering from global drift.

Hardware Impact: 0 runtime us. This was audit/reporting only; no player-frame
code path changed.

## Decision 012 - Add BufferID Gate Instead Of Blind Renumber

Problem: DataVault is architecturally correct only if `BufferID` identity is
unique and owned. Static audit found one duplicate central numeric value and
579 local numeric `(BufferID)N` casts outside `H8Memory.cs`; several casts can
alias existing central enum names.

Solution: Add `Tools/BufferIDSovereigntyAudit.py` with JSON/markdown output and
unit tests, then promote `--fail-on-duplicates` into stable quality gates and
architecture docs. Treat local numeric casts as migration debt now and as a
future hard gate once owners/ranges/lifetimes are recorded or removed.

Rejected Alternatives: Silent enum renumbering was rejected because BufferID
values may be serialized, logged, or assumed by other active agents. Ignoring
the issue was rejected because duplicate integer identities can corrupt Vault
ownership even when the code compiles.

Scalability potential: Process/runtime-indirect. Weak devices cannot afford
debug recovery from aliased native buffers; high/ultra devices should spend
budget on visual overkill, not repairing memory-ownership ambiguity.

Hardware Impact: 0 runtime us in this pass. The new tool is offline static
governance; no player-frame path changed.

## Decision 013 - Repair 70200 BufferID Alias In Favor Of Save Stability

Problem: R11 made `--fail-on-duplicates` a hard gate and proved
`SaveWorldPagerWriteArena` and `ConstructionBuilderOccupancy` both used
`BufferID` value `70200`. Leaving that alias would keep DataVault identity
unsafe even after adding the gate.

Solution: Preserve `SaveWorldPagerWriteArena = 70200` and move
`ConstructionBuilderOccupancy` to the unused construction-adjacent value
`70319`, immediately before `ConstructionPreviewWrite = 70320`. Re-run
`python Tools/BufferIDSovereigntyAudit.py --fail-on-duplicates`, which now
passes with `duplicates=0`.

Rejected Alternatives: Moving the save pager ID was rejected because save
staging IDs are more likely to carry persistence/log compatibility weight.
Leaving the duplicate as documentation-only debt was rejected because the gate
was already precise and the repair had a narrow source scope.

Scalability potential: Process/runtime-indirect. Low-end and standalone VR
targets are most exposed to hidden native-state corruption because recovery
costs consume the small frame budget. High/Ultra targets should spend budget on
visual overkill, not diagnosing aliasing.

Hardware Impact: 0 runtime us claimed. The change removes a Vault identity
collision; runtime performance is unchanged unless the collision would have
caused fallback/corruption.

## Decision 014 - Record R13 As Current Band Report, Not Readiness Claim

Problem: The user asked again for current global direction and platform
readiness after large concurrent changes. Prior R10/R11 text contained useful
evidence, but R12 changed BufferID duplicate status and source counters shifted.

Solution: Append R13 to the dated global/platform report and active AgentLog
with fresh static counters, proof gaps, platform bands, and next proof order.
Keep the language explicit: static bands are not Unity import, build, profiler,
headset, Deck, macOS, PICO, or console proof.

Rejected Alternatives: Updating stable policy again was rejected because the
stable proof ladder and quality gates are already correct. Claiming readiness
from package IDs/source references was rejected because `packages-lock.json`,
XR provider settings, runtime routes, and device captures are still absent.

Scalability potential: Process/runtime-indirect. Weak devices get protected by
the Windows/Copper Wire proof-first order; high/ultra devices only get visual
overkill after the baseline route is measured.

Hardware Impact: 0 runtime us. This is documentation and audit state only.

## Decision 015 - Add Read-Only Global Authority Gate

Problem: Current architecture review used multiple independent scans. That is
easy to misreport: an agent can show `GlobalRegistry.Get<T> = 0` while ignoring
SignalBus gaps, `GlobalSignals.Publish`, local `BufferID` casts, `Pack = 1`, or
raw native allocation pressure.

Solution: Add `Tools/GlobalAuthorityGate.py`, a read-only stdout gate that
aggregates the major global-authority pressure counters and reuses the BufferID
parser for duplicate detection. Default hard failures are narrow and currently
practical: generic `GlobalRegistry.Get<T>`/`TryGet<T>` usage and duplicate
central `BufferID` values. Broader debt remains warnings until migration
baselines make it safe to hard-fail.

Rejected Alternatives: Making every warning a hard failure now was rejected
because existing debt is large and would block all integration noise instead of
targeted regression prevention. Writing another markdown report by default was
rejected because this gate is intended for fast read-only checks and CI-style
stdout.

Scalability potential: Process/runtime-indirect. Weak devices benefit when
global authority pressure is visible before runtime profiling. High/Ultra
devices benefit by reserving budget for visible overkill instead of recovery
from hidden global coupling.

Hardware Impact: 0 runtime us. Offline static gate only.

## Decision 016 - Separate Platform Scaffold From Proven Readiness

Problem: Static platform references can make the project look more ready than
it is. The dedicated platform review showed that even where scaffolding exists,
proven runtime readiness remains near zero because package lock, XR provider,
player build, profiler, native plugin parity, content payload, and device-run
artifacts are absent.

Solution: Append R15 to the dated report and AgentLog with two columns:
static scaffold and proven runtime readiness. Verify key blockers locally:
missing XR package lock entries, empty `m_BuildTargetVRSettings`, legacy-only
`XRSettings.asset`, Windows-only native plugins, empty Addressables data, and
missing `static_data.h8bin`.

Rejected Alternatives: Leaving R13 bands alone was rejected because they are
useful for direction but too generous for platform planning. Claiming Quest,
Deck, macOS, or PCVR progress from source references was rejected because
device/platform artifacts are the proof boundary.

Scalability potential: Process/runtime-indirect. Low-tier and standalone VR
targets need honest proof gates first; high/ultra targets get overkill only
after the baseline route and content payload are actually measured.

Hardware Impact: 0 runtime us. This is classification/reporting only.

## Decision 017 - Rerun Gates Instead Of Rewriting Architecture Policy

Problem: The user repeated the global direction/platform request while many
agents are still changing the workspace. The stable policy did not need another
rewrite, but current counters could be stale.

Solution: Rerun `GlobalAuthorityGate.py`,
`BufferIDSovereigntyAudit.py --fail-on-duplicates`, and
`DataVaultSovereigntyAudit.py --fail-on-regression`; append R16 current counts
to the dated report and active AgentLog. Keep the conclusion evidence-based:
hard global-authority gates are clean, warning pressure remains high, platform
readiness remains proof-blocked.

Rejected Alternatives: More policy documents were rejected because the
authority spine already has the right rules. Runtime refactoring was rejected
because the current issue is proof and debt burn-down, not a new abstraction.

Scalability potential: Process/runtime-indirect. Weak devices benefit from
keeping the warning pressure visible before profiling. High/Ultra remains
blocked from overkill claims until the low baseline route is measured.

Hardware Impact: 0 runtime us. Static verification and documentation only.

## Decision 018 - Final Recapture Without New Refactor

Problem: The user repeated the same current-direction request again while the
workspace continued to move. Current gates needed a fresh run, but the hard
findings did not justify another source refactor.

Solution: Re-run global authority, BufferID duplicate, and DataVault
no-regression gates; append R17 to the dated report and active AgentLog. Keep
the answer stable: direction correct, hard gates clean, warning pressure high,
platform proof missing.

Rejected Alternatives: Adding another architecture tool was rejected because
`GlobalAuthorityGate.py` already covers the current static pressure surface.
Changing runtime systems was rejected because no new narrow hard defect appeared
after the existing BufferID alias repair.

Scalability potential: Process/runtime-indirect. The value is keeping weak-device
and XR readiness tied to measured proof, not static surface optimism.

Hardware Impact: 0 runtime us. Audit-only recapture.
