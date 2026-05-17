# Rationale_CTO_META_COORDINATOR

## Decision: Treat CTO role as META/INTEGRATION

Problem: The repeated prompt references `[YourID]`, but no `<AGENT_PROMPT id="CTO_META_COORDINATOR">` exists in `Docs/Tasks/CURRENT_BATCH.md`, and no CTO status/rationale files existed.

Solution: Use `CTO_META_COORDINATOR` as the disk identity for architecture oversight, record the missing-state defect, and continue from filesystem evidence only.

Rejected Alternatives: Using another agent's ID would corrupt that agent's audit trail. Treating chat memory as authority violates the prompt and AGENTS.md.

Scalability potential: This is tooling/process state only. Low-end and high-end runtime behavior is unchanged.

Hardware Impact: 0 us runtime.

## Decision: Demote stale green compile claims

Problem: Older logs claim `Hecton8.Core` green, but the current disk build failed with `EcosystemDirector` errors and duplicate contract source warnings.

Solution: Report current-disk compile as the authority and generate a focused `ECOSYSTEM_INDEX_REPAIR_SURGEON` task instead of broad optimism.

Rejected Alternatives: Trusting older `Build succeeded` artifacts would hide current breakage. Editing the World file from the CTO coordinator would violate the no-code architecture role.

Scalability potential: Restoring compile is prerequisite for all platform builds.

Hardware Impact: 0 us runtime; compile integrity only.

## Decision: Keep RealtimeCSG graph purge as first integration lane

Problem: Tool Resak removed `Assets/RealtimeCSG`, but `RealtimeCSG.csproj` still references 233 deleted source paths. Full Unity project validation cannot progress through that poison.

Solution: Assign a graph-purge prompt that removes stale generated/project references without restoring CSG or inventing dummy files.

Rejected Alternatives: Recreating CSG source files or stubbing vendor APIs would restore deprecated technology and mask the actual graph defect.

Scalability potential: Removing CSG graph debt unblocks full-project compile and runtime verification.

Hardware Impact: 0 us runtime from graph purge itself; prior CSG stall removal remains a Tool Resak estimate pending profiler proof.

## Decision: Continue debt-strike prompts, not feature expansion

Problem: Current debt counters remain high: 1168 direct NativeArray constructors, 428 non-Pack=1 layout declarations, 1661 signal/event debt lines, and 170 hygiene-name hits.

Solution: Issue targeted executor prompts with before/after count requirements and no runtime microsecond claims.

Rejected Alternatives: Adding more visual features now would increase integration surface while compile/runtime proof is still unstable.

Scalability potential: Native ownership, ABI stability, signal lane hygiene, and script quarantine directly affect Quest/Android, Steam Deck, and low-memory PC reliability.

Hardware Impact: No measured runtime gain claimed until profiler/GCMonitor artifacts exist.
