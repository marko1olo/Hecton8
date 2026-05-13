# Rationale_CONSOLE_MEDIC

## Decision 001

Problem: Active `Status_CONSOLE_MEDIC.md` and `Rationale_CONSOLE_MEDIC.md` were missing because another process moved the prior loop into `Docs/Archive/Batch004`.
Solution: Read the archived files, recreate active state files, and continue from the archived evidence instead of restarting from memory.
Rejected Alternatives: Reverting the archive move; ignoring the missing active state; writing only to chat.
Scalability potential: Low/Middle/High/Ultra unaffected directly; process continuity prevents duplicate or contradictory fixes.
Hardware Impact: 0 us runtime; process-only guard.

## Decision 002

Problem: New `CURRENT_BATCH.md` exists and may contain fresh assignments, but no `CONSOLE_MEDIC` block was present.
Solution: Treat the user's repeated request as a direct integration/console interrupt and avoid neighboring agents' prompts.
Rejected Alternatives: Hijacking active batch roles such as QUALITY or SIMULATION agents; parsing Polish Mandate before core work.
Scalability potential: Low/Middle/High/Ultra unaffected directly; protects domain boundaries under parallel execution.
Hardware Impact: 0 us runtime; process-only guard.

## Decision 003

Problem: Stale Unity log warnings listed obsolete `GetInstanceID`, `FindFirstObjectByType`, and internal dispatcher time usage, but previous source edits may already have resolved them.
Solution: Search current touched source before editing; no matching stale patterns remained in the touched files.
Rejected Alternatives: Editing line numbers from stale log text; broad project-wide deprecation churn.
Scalability potential: Low/Middle/High/Ultra unaffected until a current defect is confirmed.
Hardware Impact: 0 us runtime; prevents unnecessary reimport/diff churn.

## Decision 004

Problem: `HectonFluidEngine` used `SystemID.Fluid` for DataVault Gerstner buffers, but the memory owner enum had no matching value.
Solution: Added stable `SystemID.Fluid = 66` without renumbering existing owners.
Rejected Alternatives: Reusing `SystemID.Physics` or `SystemID.Vfx`, which would hide ownership in leak telemetry.
Scalability potential: Low/Middle/High/Ultra unaffected in frame time; memory attribution remains precise across all tiers.
Hardware Impact: 0 us runtime; diagnostic owner lookup only.

## Decision 005

Problem: `HectonPlayerMovement` had duplicate registry lifecycle methods after parallel edits, with behavior split between contract registration and dispatcher/dependency binding.
Solution: Kept one implementation that preserves player movement contracts, dispatcher registration, service rebinding, and scalability cache invalidation.
Rejected Alternatives: Deleting either duplicate blindly; adding renamed wrappers that would leave interface ambiguity.
Scalability potential: Low avoids duplicate registration churn; High/Ultra keep cinematic-focus cache invalidation on tier changes.
Hardware Impact: microseconds saved on service rebinding by avoiding duplicate hook execution and compile failure.

## Decision 006

Problem: Unity 6 compile rejected passing `_macroSwarms[0]` directly as an `in` argument and rejected a static brine helper reading instance-cached resource distribution state.
Solution: Copied the first `MacroSwarm` into a stack local before `in` pass and made the brine sink helper instance-scoped.
Rejected Alternatives: Removing `in` from the resolver or bypassing cached resource distribution through a global lookup per swim step.
Scalability potential: Low/Middle keep no-GC mutation signal publishing; High/Ultra retain cached brine sampling for richer swim physics.
Hardware Impact: 0 heap allocations; avoids an extra global service lookup in swim physics.

## Decision 007

Problem: `HectonBiolumZone` emitted a Unity 6 obsolete `GetInstanceID()` warning in LOD bucketing.
Solution: Replaced it with `EntityId.ToULong(GetEntityId())` while preserving the same bucket hash input shape.
Rejected Alternatives: Suppressing the warning or using a random/stable string hash unrelated to the entity.
Scalability potential: Low keeps cheap bucket culling; High/Ultra keep deterministic per-zone update distribution.
Hardware Impact: equivalent per-frame cost; removes warning debt.

## Decision 008

Problem: Caustics and lockstep assemblies failed under current asmdef/API contracts: service lifecycle types were not visible, `JobHandle.CombineDependencies` used an unsupported four-argument overload, and `UnsafeUtility` was not imported.
Solution: Added the required asmdef reference/import and pairwise-combined job dependencies without managed arrays.
Rejected Alternatives: Allocating a temporary `NativeArray<JobHandle>` or moving caustics back into the root assembly.
Scalability potential: Low avoids extra dependency-array allocation; High/Ultra retain Burst lockstep hashing.
Hardware Impact: 0 heap allocations; dependency combine remains constant-time.

## Decision 009

Problem: `SaveManager` implemented the new page-persistence contract only after parallel edits, but the root asmdef could not see the pager assembly; the pager worker also used an async method inside unsafe context.
Solution: Added `Hecton8.Core.Persistence.Paging` visibility, used a dedicated background worker method instead of `await` in unsafe context, and ensured out tickets are initialized.
Rejected Alternatives: Removing `IAsyncPersistenceService` from `SaveManager` or returning fake success without a pager.
Scalability potential: Low can reject/skip chunk paging cleanly; High/Ultra keep async page persistence available when the file handle is free.
Hardware Impact: hot path remains native queue copy; one cold worker thread only when pager initializes.

## Decision 010

Problem: Runtime bootstrap could fail when `world_data.h8bin` was temporarily locked, and the last remaining Console item was a Burst internal hash/cache exception without a source diagnostic.
Solution: Made pager initialization fail closed with telemetry instead of throwing, allowed read/write file sharing for diagnostics, removed the expected degradation warning, and cleared only generated `Library/BurstCache/JIT` after path validation.
Rejected Alternatives: Disabling Burst, deleting source-generated assemblies, or letting a transient pager lock fail CoreServices boot.
Scalability potential: Low devices get procedural fallback instead of boot failure; High/Ultra regain pager-backed streaming once the handle is available.
Hardware Impact: 0 frame cost when initialized; transient failure avoids boot-time exception and preserves async paging when available.

## Decision 011

Problem: Current Unity Console contained a live Crest warning: primary ocean shadowing could not run because `RenderSettings.sun` / `Directional Light` had `Light.shadows=None`.
Solution: Set the active scene's primary directional light to `Hard` shadows and saved `Assets/_Project/Scenes/02_HECTON_WORLD.unity`; Crest accepts hard or soft shadows for ocean shadowing, and hard avoids the default soft-filter cost.
Rejected Alternatives: Muting Crest validation, disabling ocean shadowing, changing Crest package code, or defaulting to soft shadows without tier evidence.
Scalability potential: Low keeps the cheapest valid shadow mode; Middle/High/Ultra preserve ocean shadowing and can still raise shadow resolution/filtering through existing quality controls.
Hardware Impact: Expected GPU cost is scene/URP dependent; hard directional shadows avoid soft-filter overhead while restoring the missing ocean-shadow path. No C# frame-time cost.

## Decision 012

Problem: `H8BinaryWorldPager` had a partial worker-thread shutdown patch: `_workerThread` and join constants existed, and `Dispose()` waited for them, but `StartWorker()` still launched a fire-and-forget `Awaitable` background loop. That left native queues/arrays exposed to disposal while the worker could still be alive.
Solution: Converted pager execution to an explicit background `Thread`, stored the handle, and made `WaitForWorkerExit()` clear the handle only after spin/join confirmation. Startup failure now marks pager init fault without throwing through boot.
Rejected Alternatives: Keeping the `Awaitable` worker, adding more spin-only waiting, or using managed async cancellation tokens that are not compatible with this native persistence owner.
Scalability potential: Low keeps the pager fail-closed without main-thread stalls; Middle/High/Ultra keep async chunk paging without undefined native-memory races during shutdown/reinitialize.
Hardware Impact: 0 us hot path; one cold thread allocation at pager initialization; avoids rare shutdown/reinit stalls and native memory race failures.

## Decision 013

Problem: After a tool-side `ManageScript.CheckDuplicateMethodSignatures` regex timeout, MCP logged an editor-package error and then disconnected from the active Unity instance. This blocks authoritative final Console polling.
Solution: Treat the message as MCP infrastructure evidence, verify Unity process responsiveness and compile state through local Roslyn sweeps, and avoid editing PackageCache or claiming a clean Console without a successful bridge read.
Rejected Alternatives: Patching `Library/PackageCache/com.coplaydev.unity-mcp`, killing/restarting Unity from the agent, or clearing the Console through an unavailable bridge and calling it verified.
Scalability potential: Low/Middle/High/Ultra unaffected; this is tooling-only.
Hardware Impact: 0 us runtime; process-only guard.

## Decision 014

Problem: Unity entered a long import/script postprocess state after source edits, MCP stayed disconnected, and after waiting the Unity process was no longer present. No new crash dump newer than the existing 16:54 dump was found under Unity temp crashes or Windows CrashDumps.
Solution: Stop claiming Console verification, preserve Roslyn compile evidence, and report the editor/tool state as the blocker. Do not kill or relaunch Unity from the agent without an explicit user command.
Rejected Alternatives: Faking Console clean status, deleting import caches, or terminating/restarting editor processes while other agents may be attached.
Scalability potential: Low/Middle/High/Ultra unaffected; this is editor verification state.
Hardware Impact: 0 us runtime; process-only guard.

## Decision 015

Problem: Current Unity/Bee graph was stale: root compile referenced new systems (`HomeostasisBrain`, `VisorDropletSignal`, diegetic cockpit contracts) before the generated rsp included the new source files and contract reference.
Solution: Added the missing `Hecton8.UI.Diegetic.Contracts` asmdef reference, let the new source files remain standalone, and verified root compile by adding only those pending source inputs to the current Roslyn command.
Rejected Alternatives: Merging `HomeostasisBrain` or `PrologueReentrySignals` into already-included files; editing Bee rsp files; deleting another agent's new source files.
Scalability potential: Low/Middle/High/Ultra unaffected directly; preserves decoupled contract boundaries and avoids future duplicate-type imports.
Hardware Impact: 0 us runtime; assembly-boundary repair only.

## Decision 016

Problem: `BinaryBlittableSafeAttribute` existed locally in the root assembly while current references also exposed it from memory layout, causing CS0436 warning spam; long-term ownership belongs with memory layout/contracts, not the root utility file.
Solution: Removed the duplicate attribute declaration from `MemoryInquisitor`, added the attribute under the memory assembly source tree, and kept `MemoryInquisitor` consuming the existing `Hecton8.Core.Memory.Layout` namespace.
Rejected Alternatives: Suppressing CS0436; leaving the root assembly as the owner; renaming the attribute and breaking existing save/layout annotations.
Scalability potential: Low/Middle/High/Ultra unaffected directly; removes warning noise from binary-layout safety audits.
Hardware Impact: 0 us runtime; compile/type ownership only.

## Decision 017

Problem: `HectonPlayerMovement` still used deprecated `Collider.GetInstanceID()` for ladder snap cache identity.
Solution: Replaced it with the existing project pattern `unchecked((int)EntityId.ToULong(collider.GetEntityId()))`.
Rejected Alternatives: Suppressing CS0618 or replacing the cache with a managed object-reference dictionary.
Scalability potential: Low keeps cheap integer cache lookup; Middle/High/Ultra keep deterministic Unity 6 entity identity without managed allocation.
Hardware Impact: Equivalent hot-path cost; removes one warning and avoids future API removal.

## Decision 018

Problem: `GlobalDataVault` current source referenced `Stopwatch`/`IJob` members during Unity import after parallel edits, but the required imports were missing. The editor log also showed transient stale versions missing the gap audit declarations.
Solution: Added the missing `System.Diagnostics` and `Unity.Jobs` imports and verified the current file compiles cleanly with `Hecton8.Core.Memory.rsp`.
Rejected Alternatives: Removing the defrag watchdog/gap audit path; replacing native audit work with managed allocations; reverting another agent's vault expansion.
Scalability potential: Low keeps direct scan/gap audit cheap; Middle/High/Ultra retain the defrag watchdog and black-box telemetry path.
Hardware Impact: 0 us outside cold defrag; cold audit remains native/no-GC.

## Decision 019

Problem: Active Unity log repeatedly warned that MapMagic terrain preview shaders had all subshaders stripped under the current import render-pipeline/API mask.
Solution: Added minimal editor-preview fallback SubShaders to `TerrainPreview.shader` and `TerrainPreviewURP.shader`; the primary terrain passes remain first, and fallback is only used when Unity strips those passes.
Rejected Alternatives: Muting shader warnings, disabling MapMagic preview import, or rewriting MapMagic editor shader routing.
Scalability potential: Low/Middle/High/Ultra runtime unaffected because these are editor preview shaders; editor import no longer depends on a single pipeline-specific terrain pass.
Hardware Impact: 0 us runtime; editor-only fallback pass.

## Decision 020

Problem: Final Unity Console polling failed because MCP reported `no_unity_session`; the active editor log shows MCP WebSocket disconnects and "Server no longer running; ending orphaned session."
Solution: Use Unity's active log plus manual Roslyn as evidence, and explicitly mark authoritative Console polling blocked by tooling transport. Do not patch PackageCache or restart the editor while other agents may be attached.
Rejected Alternatives: Reporting a clean Console without `read_console`, killing/restarting Unity, or editing MCP package transport code.
Scalability potential: Low/Middle/High/Ultra unaffected; tooling-only.
Hardware Impact: 0 us runtime.

## Decision 021

Problem: A later `Hecton8.Core.Memory` compile pass caught concurrent `GlobalDataVault` drift: reset code referenced removed `_defragCursor`, the gap audit carried a `Unity.Burst.BurstCompile` attribute without a Burst asmdef reference, and the audit used `Unity.Mathematics.math.rcp` without a Mathematics reference.
Solution: Remove the stale cursor reset and external package calls; keep the existing synchronous native audit but use only assembly-local APIs and a plain float divide.
Rejected Alternatives: Adding Burst/Mathematics asmdef references for a synchronous one-shot audit, or reworking defrag relocation policy while acting as console medic.
Scalability potential: Low/Middle/High/Ultra keep the same cold audit behavior without widening dependency surface.
Hardware Impact: 0 us hot path; cold audit remains native and allocation-free.

## Decision 022

Problem: Current `GlobalDataVault` also pointed the defrag black-box dump at `Dump_AGENT_HOMEOSTASIS_METABOLISM.bin` and retained an unused high-tier bypass residue.
Solution: Restore `Docs/AgentLogs/Dump_MEMORY_DEFRAGMENTATION_OVERSEER.bin` and remove the unused bypass constant/method.
Rejected Alternatives: Leaving postmortem evidence under another agent name, or preserving a bypass that status/rationale from the memory owner had already rejected.
Scalability potential: Low/Middle/High/Ultra all retain truthful crash telemetry; high-end machines no longer carry dead bypass intent.
Hardware Impact: 0 us runtime; dump path is fault-only.

## Decision 023

Problem: Unity's latest log showed the MapMagic preview shader warning source had shifted from missing fallback subshaders to mixed line endings after fallback insertion.
Solution: Normalize only `TerrainPreview.shader` and `TerrainPreviewURP.shader` to CRLF and verify no mixed line endings remain by byte audit.
Rejected Alternatives: Ignoring import hygiene, or rewriting shader content beyond the existing editor-preview fallback.
Scalability potential: Low/Middle/High/Ultra runtime unaffected; editor import diagnostics become cleaner once Unity refreshes the normalized files.
Hardware Impact: 0 us runtime; editor/import-only cleanup.

## Decision 024

Problem: Unity MCP remained unreliable after `refresh_unity(wait_for_ready=true)`: the refresh request timed out after 60 seconds, `read_console` returned `no_unity_session`, and the active log stopped updating at the previous 23:36 refresh.
Solution: Preserve manual Roslyn evidence as the authoritative compile proof and mark Console polling blocked by transport instead of claiming a clean Console.
Rejected Alternatives: Killing/restarting Unity while other agents are attached, editing MCP PackageCache transport code, or treating stale log lines as current Console state.
Scalability potential: Low/Middle/High/Ultra unaffected; tooling-only.
Hardware Impact: 0 us runtime.
