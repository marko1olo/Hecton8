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
