# LOG_DOC_AUDIT

Top = old. Bottom = new.

## R38 - 2026-05-13 - Active Memory Restart

What was wrong: Active `Docs/Tasks/Status_DOC_AUDIT.md`, `Docs/AgentLogs/Rationale_DOC_AUDIT.md`, and `Docs/AgentLogs/LOG_DOC_AUDIT.md` were absent after Batch005 archive movement, so continuation state could silently drift from disk-backed memory.

What was done: Recreated active DOC_AUDIT working files and linked prior history to `Docs/Archive/Batch005/`.

Cinematic Cheats used: none; documentation/state hygiene only.

Exact Microseconds saved: 0 runtime microseconds. Prevents human/integrator time loss from stale status location.

## R38 - 2026-05-13 - Pager Fault Accounting / WFC Persistence Contract

What was wrong:
- `H8BinaryWorldPager.RunWorkerLoop()` decremented pending write/read counters only after `ProcessWrite()` / `ProcessRead()` returned normally. Unexpected exceptions before inner IO catches could stop the worker with stale pending counters.
- `IAsyncPersistenceService` exposed WFC outpost persistence methods that `SaveManager` did not implement once current `Core.Contracts` source was used.
- R37 full-Core compile success was no longer current: the live worktree now blocks full `Hecton8.Core` on unrelated audio/scanner/submarine-buffer/arena/fluid/UI/fauna churn.

What was done:
- Added per-command pager worker wrappers that decrement pending counters in `finally`, record fault telemetry, fail-close the pager, zero exposed pending counters, and dump black-box telemetry on unexpected command faults.
- Implemented WFC outpost state persistence in `SaveManager` using DataVault `WfcOutpostGrid`, fixed native packed/restore scratch, existing `SaveBinaryPayloadCodec`, and `IMacroDatabaseService.MarkDirty` / `TryGetPayload`.
- Updated stable docs and the R38 X-Ray section to demote stale full-Core proof and list the current blocking files honestly.

Cinematic Cheats used:
- WFC mutable state persists as a compact bitmask payload, not a full simulated outpost object graph.
- Restore reads bounded MacroDB payload handles and unpacks only mutable flags back into the WFC grid.

Exact Microseconds saved:
- Pager normal path: 0 us/frame; only failure accounting changes.
- WFC unchanged snapshot skip: avoids one pager enqueue/write for repeated identical sector state.
- Full-Core proof correction: 0 runtime us; prevents stale build evidence from being treated as current.

## R39 - 2026-05-13 - Generated Project / Asmdef Drift Tripwire

What was wrong:
- Current `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` fails on missing namespaces/types because the generated `Hecton8.Core.csproj` does not reflect the current `Hecton8.Core.asmdef` reference list.
- The source assemblies and asmdefs already exist for the first blocker class, so adding stubs would be false repair.

What was done:
- Added editor-only `CSPROJ001` validation in `HectonComplianceValidator`.
- The validator now compares `Assets/_Project/Scripts/Hecton8.Core.asmdef` against `Hecton8.Core.csproj` and reports missing generated references before agents treat external `dotnet build` failures as code-level evidence.
- Live check found `23` missing first-party generated references in `Hecton8.Core.csproj`.

Cinematic Cheats used:
- None. This is build-surface hygiene, not simulation/rendering.

Exact Microseconds saved:
- Runtime: 0 us/frame.
- Process: prevents duplicate namespace/type stubs for assemblies that already exist under asmdef authority.
