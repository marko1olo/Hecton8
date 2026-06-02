# Rationale_NARRATIVE_RESEARCH

Problem: Active NARRATIVE_RESEARCH status/rationale/log files were moved into `Docs/Archive/Batch015`, while current work still needs a live memory file under `Docs/Tasks` and `Docs/AgentLogs`.
Solution: Recreate active files with Control Pass 81 only and leave the archive untouched.
Rejected Alternatives: Writing no active state; copying the whole archived log back into root; inflating status with old history.
Scalability potential: No runtime effect. Keeps agent coordination stable while multiple agents edit the project.
Hardware Impact: 0 us/frame.

Problem: Fresh Unity Apex showed `scanner_lore_fragment_sources_missing` even though `ScanEvents.cs` exists and the scanner/PDA route is implemented.
Solution: Add missing `GlobalSignals.RuntimeLifecycle.cs` to `H8NarrativeApexVerifier.RuntimeSourcePaths`; `ScanEvents.cs` is already in scope. This makes the verifier parse the actual SignalBus lifecycle validation file it requires.
Rejected Alternatives: Suppressing the route check; adding duplicate runtime lifecycle code; changing scanner/PDA behavior.
Scalability potential: Low/Middle/High/Ultra unchanged. This is editor-only proof scope repair.
Hardware Impact: 0 us/frame.

Problem: `CountInvocationInMethod` compared only simple invocation names, so checks that passed full static/generic names such as `SignalBus<UIRescaleRequestSignal>.GetFrameSnapshot` returned zero even when runtime code was correct.
Solution: Add `MatchesInvocation`, preserving simple-name exact matching while allowing full static/generic invocation text matching for route proofs.
Rejected Alternatives: Editing `PDAEncyclopediaStreamer` and `DiegeticHudManualLayout` despite correct `LateFrameTick` snapshot reads; replacing AST checks with loose global grep.
Scalability potential: Prevents future false phase failures without changing runtime.
Hardware Impact: 0 us/frame.

Problem: `TerminalOsRuntime` dump gate lock uses explicit `Monitor.Enter(_gate, ref lockTaken)` with `finally Monitor.Exit`, but the verifier counted only C# `lock` statements. That produced a false lock finding.
Solution: Count `Monitor.Enter` as a gate lock scope inside `CountLockStatementsInMethod`; keep runtime code unchanged because it already releases in `finally` and writes after lock release.
Rejected Alternatives: Rewriting valid source to `lock {}` for cosmetic verifier compatibility; weakening write-after-release proof.
Scalability potential: Crash dump path remains fault-only and deterministic across low-tier to ultra-tier devices.
Hardware Impact: 0 us/frame normal gameplay.

Problem: Narrative/AppliedLore verifier hard-failed MapMagic/Terrain/TerrainCollider markers in `02_HECTON_WORLD.unity`, but those are world-domain ownership facts. Current scene has no MapMagic/Terrain markers and no TerminalOS placement; hard-failing world markers hides the actual applied-lore issue.
Solution: Convert world-core markers to `scene_world_dependency_warnings` in both C# verifier and Python audit. Keep counters visible. Do not raw-edit scene YAML in a shared workspace.
Rejected Alternatives: Reverting scene changes made by other agents; mutating YAML; pretending world scene is complete.
Scalability potential: Lets Narrative Apex remain a code/protocol verifier while world-domain agents own scalable terrain/ocean composition.
Hardware Impact: 0 us/frame.

Problem: Current `02_HECTON_WORLD.unity` no longer contains scene-owned TerminalOS runtime placement.
Solution: Do not claim integration complete. Keep audit counters visible: `scene_terminal_os_runtime_rows=0`, `scene_terminal_os_runtime_verified_slots=0`, `scene_bindings=0`, `scene_world_dependency_warnings=3`.
Rejected Alternatives: Fabricating a green scene proof; adding runtime scene search/self-healing; unsafe scene edit during parallel agent work.
Scalability potential: Prefab/content pipeline remains usable, but final scene placement must be restored by a scene-authority pass.
Hardware Impact: 0 us/frame until placed.

Problem: The shared world scene now contains the scene-owned TerminalOS runtime again, but all 27 `terminalOsPreviewHash` overrides pointed at message/content packet hashes instead of `TerminalOsHash.HashIndex(index)`. The runtime preview resolver validates terminal identity hash, so those previews could silently fail routing despite renderer/transform slots being present.
Solution: Replace only the 27 serialized `terminalOsPreviewHash` values with the deterministic TerminalOS index hashes. Add a C# Apex verifier check for preview hash/index pairs, hash mismatches, and duplicate indices so C# Apex cannot pass this drift again.
Rejected Alternatives: Changing `TerminalOsRuntime.TryResolveTerminalPreviewIndex`; adding runtime fallback search; using message hash as terminal identity; relying only on Python audit.
Scalability potential: Low/Middle/High/Ultra unchanged. This removes a content identity defect without adding runtime work. The scene still scales through existing TerminalOS preview slots and quality guards.
Hardware Impact: 0 us/frame.

Problem: Raw YAML scene edits are risky, and `git diff` reports `02_HECTON_WORLD.unity` as binary in this workspace.
Solution: Verify the scene structure command succeeded (`m_RootGameObject` scan), rerun AppliedLore full/source audits, and add C# verifier coverage for the exact serialized field relationship.
Rejected Alternatives: Broad YAML rewrite; touching unrelated scene objects; claiming safety from a binary diff alone.
Scalability potential: No runtime effect. Prevents content drift from breaking terminal previews across all device classes.
Hardware Impact: 0 us/frame.

Problem: A Unity compile/import rerun is required for the new editor verifier code to appear in the menu assembly, but CPU is above the project throttle threshold and Unity Roslyn/dotnet compiler/server processes are active.
Solution: Stop at Unity `validate_script` plus Python full/source audits until CPU drops below the mandated threshold. After CPU dropped and no `csc.exe` existed, request one Unity script refresh/import and run the Apex menu verifier. Do not run `dotnet build`.
Rejected Alternatives: Compile spam; starting a second build while Unity compiler services are live; killing shared compiler/server processes.
Scalability potential: Prevents workstation stalls during multi-agent work.
Hardware Impact: 0 us/frame in game; avoided editor CPU contention.

Problem: The new C# Apex hash guard needed Unity-side proof, not only source parse.
Solution: Run `Hecton8/Lore/Run Narrative Apex Verification` after Unity returned idle. The console proof reports `terminal_os_preview_hash_pairs=27`, `terminal_os_preview_hash_mismatches=0`, `terminal_os_preview_hash_duplicate_indices=0`, `dependency_findings=0`, `phase_findings=0`, `zero_gc_findings=0`, `job_complete_findings=0`, `lock_findings=0`, `fatal_findings=0`, `build_invocations=0`, `analysis=RoslynAST_in_memory`.
Rejected Alternatives: Treating Python audit as the only proof; skipping Unity menu proof after editor import.
Scalability potential: Editor-only verification surface. Runtime cost unchanged across all device classes.
Hardware Impact: 0 us/frame.

Problem: Process scan found three stale `rg.exe` searches from earlier agent prompt extraction/static scans. They were hours old and not tied to the current task.
Solution: Stop only those `rg.exe` PIDs and verify they are gone. Leave Python servers, Unity, and Unity Roslyn/dotnet processes untouched because ownership is not proven.
Rejected Alternatives: Ignoring orphaned search processes; killing broad process classes; killing user services.
Scalability potential: Reduces workstation noise without touching runtime/game code.
Hardware Impact: 0 us/frame; editor/workstation hygiene only.

Problem: Unity Console showed 19 stale `The referenced script (Unknown) on this Behaviour is missing!` entries after import.
Solution: Scan scenes/prefabs/assets for missing script YAML signatures, find zero hits, clear Console, rerun Narrative Apex, and confirm missing-script messages do not return.
Rejected Alternatives: Deleting MonoBehaviour blocks blindly; treating stale Console history as current regression; ignoring the signal without a sweep.
Scalability potential: No runtime effect. Prevents false blocker churn for later scene/content agents.
Hardware Impact: 0 us/frame.

Problem: `MetaCampaignService.PublishCachedVisualState` owned a `Shader.SetGlobalFloat` presentation write, but it could be reached through state side-effect calls outside a strict `LateFrameTick` route.
Solution: Add primitive pending visual state fields and route visual refresh through `QueueCachedVisualState` -> `LateFrameTick` -> `FlushCachedVisualState` -> `PublishCachedVisualState`. Keep campaign truth, save DTO, SignalBus payloads, and DataVault handles unchanged.
Rejected Alternatives: Treating cold API calls as acceptable presentation writes; moving campaign variable truth to UI; adding a new SignalBus lane for a single-owner scalar.
Scalability potential: Low/Middle/High/Ultra unchanged for gameplay truth. The saved timing currency keeps visual shader pressure deterministic and lets high-tier shader response read the same scalar without extra simulation cost.
Hardware Impact: Normal frame: one bool branch in `LateFrameTick`, 0 B GC. Visual refresh frame: existing shader/ecosystem calls shifted to late phase, no new allocation.

Problem: The existing Apex scope did not include several adjacent Narrative owners (`MetaCampaignService`, `CorporateOrderSystem`, `LoreDatabaseManager`, `ProceduralLoreDirector`, `AwaitableDropSequenceDirector`), so APEX compliance could be green while campaign/prologue write-lock or phase drift existed.
Solution: Add those files to `H8NarrativeApexVerifier.RuntimeSourcePaths`. Add DataVault write helper-transfer checks and a MetaCampaign visual phase route guard.
Rejected Alternatives: Expanding runtime coupling; scanning the whole repository and producing false cross-domain failures; leaving helper-transfer lock patterns invisible.
Scalability potential: Editor-only proof surface. Runtime device tiers unchanged.
Hardware Impact: 0 us/frame.

Problem: Unity MCP validation could not complete after the patch because the active Unity compiler/server path was saturated.
Solution: Do not run `dotnet build`; do not spam Unity refresh. Run only AppliedLore audit, scoped hot dependency grep, route token checks, `git diff --check`, and local brace/paren/bracket lexer until CPU/compiler throttle clears.
Rejected Alternatives: Starting another build while CPU is 100%; killing shared Unity/VBCSCompiler processes without ownership proof; claiming Unity proof from a timed-out MCP call.
Scalability potential: Prevents editor/workstation stalls during multi-agent work.
Hardware Impact: 0 us/frame; avoided extra compile contention.
