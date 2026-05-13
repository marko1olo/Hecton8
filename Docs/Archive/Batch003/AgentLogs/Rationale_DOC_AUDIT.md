# DOC_AUDIT Rationale

Status: PENDING VERIFICATION  
Evidence class ceiling: STATIC_SOURCE / STATIC_DOC unless explicitly noted otherwise.  

## Decision 001 - Standalone Audit Identity

Problem: User requested documentation reality audit, but `Docs/Tasks/CURRENT_BATCH.md` contains no `<AGENT_PROMPT id="DOC_AUDIT">` block.
Solution: Use `DOC_AUDIT` as a standalone direct-request identity and mark the missing batch prompt explicitly.
Rejected Alternatives: Hijacking an unrelated batch agent would pollute another domain and violate strict parsing.
Scalability potential: Low/Middle/High/Ultra unaffected; this is documentation governance only.
Hardware Impact: 0 us/frame runtime impact on i3/MX350.

## Decision 002 - Evidence Ceiling

Problem: Documentation often claims runtime readiness without fresh Unity/profiler artifacts.
Solution: Apply `QA_Evidence_Text_Filter_Audit`: static scans prove text/file presence only; runtime claims stay `PENDING VERIFICATION`.
Rejected Alternatives: Treating `rg` hits or `dotnet build` as runtime proof; mandate forbids that.
Scalability potential: Low/Middle/High/Ultra reporting becomes honest: static docs do not pretend hardware validation.
Hardware Impact: 0 us/frame runtime impact on i3/MX350.

## Decision 003 - Missing Build Artifact Demotion

Problem: Stable docs cited `CodexArtifacts/2026-05-11_DOCS_CONTINUATION_CORE_BUILD_R1.summary.txt` and `.log` as current compile evidence, but filesystem search did not find either file.
Solution: Added May 13 override language to stable docs and `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md`; May 11 compile success is now report text only until artifacts are restored or replaced.
Rejected Alternatives: Running a new compile was rejected because the user explicitly deprioritized build errors and requested deeper documentation/source X-ray.
Scalability potential: Low/Middle/High/Ultra unchanged; evidence quality improves because docs stop using missing files as proof.
Hardware Impact: 0 us/frame runtime impact on i3/MX350.

## Decision 004 - Stale Prompt Dump Removal From Active Docs

Problem: A stale Cyrillic-named direct `Docs/*.md` batch prompt dump differed from `Docs/Tasks/CURRENT_BATCH.md` and had no `Date:` / `Status:` header.
Solution: Moved it to `Docs/DEPRECATED/Root_Stale_Batch_Prompt_Dumps_2026-05-13/` and added a local README.
Rejected Alternatives: Leaving it in direct `Docs/` would make active documentation inventories lie; editing a header into a prompt dump would risk making stale prompts look canonical.
Scalability potential: Low/Middle/High/Ultra unchanged; agent context hygiene improves.
Hardware Impact: 0 us/frame runtime impact on i3/MX350.

## Decision 005 - Source Counter Volatility

Problem: Source and asmdef counts changed during this audit because the workspace is live with other agents.
Solution: Record counts as `2026-05-13 STATIC_SOURCE` snapshots and explicitly mark exact source counts volatile. Updated stable atlas counters to the latest observed `1411` first-party C# files, `866103` project lines, `204` interface hits, and `24` first-party asmdefs.
Rejected Alternatives: Keeping May 11/May 12 counters as "current" was false. Claiming stable permanent counts during active churn was also false.
Scalability potential: Low/Middle/High/Ultra unchanged; documentation avoids stale numerical authority.
Hardware Impact: 0 us/frame runtime impact on i3/MX350.

## Decision 006 - Contract File Labels Are Not Source Proof

Problem: `SYSTEMS_CONTRACTS.md` listed target files such as `UnderwaterAudioProcessor.cs`, `BenchmarkRunner.cs`, and `ControlRemapper.cs`; source scan showed many are absent.
Solution: Added a source x-ray table identifying absent labels and current owners such as `SpatialAudioManager.cs`, `CrashTelemetryBuffer.cs`, and `SaveDataMigration*.cs`.
Rejected Alternatives: Treating target file labels as implemented source would create false implementation coverage.
Scalability potential: Low/Middle/High/Ultra unchanged; implementation roadmap is clearer.
Hardware Impact: 0 us/frame runtime impact on i3/MX350.

## Decision 007 - R2 Missing Artifact Demotion In Active Reference Docs

Problem: Active non-report docs under `Docs/AI_Fauna`, `Docs/Flora_Pipeline`, `Docs/Scatter_Runtime`, `Docs/Legacy_*`, and `Docs/ARCHITECTURE` still used `Current compile-only evidence:` for the absent May 11 artifact.
Solution: Bulk-demoted the exact stale line to a May 13 DOC_AUDIT override that states the artifact is absent and all runtime proof remains pending.
Rejected Alternatives: Leaving the lines as-is would make current reference docs lie. Rewriting historical dated reports wholesale was rejected; they remain snapshots with explicit supersession notes.
Scalability potential: Low/Middle/High/Ultra unchanged; evidence hygiene prevents false readiness claims.
Hardware Impact: 0 us/frame runtime impact on i3/MX350.

## Decision 008 - Live Churn Counter Refresh

Problem: R2 readback found source/docs counters moved again during the audit, including a new direct `Docs/PROJECT_STATE_STATIC_XRAY.md` file from concurrent work.
Solution: Updated active authority counters to the latest observed static snapshot: `1411` project C# files, `1365` script C# files, `868545` project source lines, `850990` script source lines, `215` interface hits, `24` asmdefs, `916` Docs markdown files, `536` active markdown files, and `11` direct `Docs/*.md` files.
Rejected Alternatives: Freezing the earlier R2 `908/535/402` docs counters or `866558/849012` source-line counters was rejected because the workspace had already changed.
Scalability potential: Low/Middle/High/Ultra unchanged; current docs now state that counts are volatile snapshot data, not permanent truth.
Hardware Impact: 0 us/frame runtime impact on i3/MX350.

## Decision 009 - New Static X-Ray Root Doc Classification

Problem: `Docs/PROJECT_STATE_STATIC_XRAY.md` appeared during R2 and became a direct `Docs` root document without governance/index placement.
Solution: Added it to `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, and `Docs/ROOT_DOCS_REFERENCE.md` as a static project-state risk register, not runtime proof. Verified its top static inventory claims against current filesystem and corrected the dirty-worktree count.
Rejected Alternatives: Ignoring the new direct root doc would make the documentation spine incomplete. Promoting it to runtime authority was rejected because it explicitly lacks Unity/profiler/player evidence.
Scalability potential: Low/Middle/High/Ultra unchanged; it is a planning/risk document only.
Hardware Impact: 0 us/frame runtime impact on i3/MX350.

## Decision 010 - Interface Count And Archivarius Path Drift

Problem: R3 source scan found `GlobalRegistryContracts.cs` now has `51` direct public interfaces, while active Archivarius dashboard/atlas text still treated `41` as the current count. Archivarius `01_GENERAL_INFO` also linked `INTERFACE_HEALTH_DASHBOARD.md` and `EVENT_FLOW_MAP.md` as local files even though current files live under `02_ACTUAL_REPORTS`.
Solution: Updated the interface dashboard with a May 13 R3 override, current `51`-interface list, and explicit coverage-recount warning. Requalified Archivarius links to `../02_ACTUAL_REPORTS/...` and corrected atlas paths for MapMagic nodes moved under `Scripts/Plugins/MapMagic`.
Rejected Alternatives: Recomputing full implementor coverage was rejected for this pass because it would require a separate source/registry audit and Unity runtime occupancy proof. Leaving `41` as current was false.
Scalability potential: Low/Middle/High/Ultra unchanged; the fix prevents contract coverage dashboards from hiding new service-surface growth.
Hardware Impact: 0 us/frame runtime impact on i3/MX350.

## Decision 011 - R4 Counter Model And Source Path Recheck

Problem: R4 readback found the R3/R4 documentation counters had already drifted again, and the active-doc count model needed explicit exclusion of archive/task/log/deprecated surfaces. Active Archivarius maps also still contained source-path drift around scene runtime, kinematic debug, player movement, and UI folder ownership.
Solution: Recounted with normalized path separators and updated active authority docs to the current static snapshot: `918` Docs markdown files, `283` active markdown files, `203` active non-`Docs/Reports` markdown files, `80` active direct report markdown files, `10` docs JSON files, `1411` first-party C# files, `1365` script C# files, `1401` non-test C# files, `869871` project physical lines, `852315` script physical lines, `867132` non-test physical lines, `215` interface declaration hits, `51` direct `GlobalRegistryContracts.cs` public interfaces, and `24` first-party asmdefs. Corrected live paths to `SceneRuntimeService.cs`, full `Assets/_Project/Scripts/Editor/KinematicGhostDebugger.cs`, `HectonPlayerMovement.cs`, MapMagic plugin paths, and `Assets/_Project/Scripts/UI`.
Rejected Alternatives: Keeping `919/262/129/16` as "current" was rejected because fresh filesystem scans disproved it. Including `Docs/Archive`, `_Archive`, `DEPRECATED`, `AgentLogs`, or `Tasks` in the active-doc count was rejected because it inflates current authority.
Scalability potential: Low/Middle/High/Ultra unchanged; documentation now exposes volatility instead of pretending stale counts are stable architecture truth.
Hardware Impact: 0 us/frame runtime impact on i3/MX350.
