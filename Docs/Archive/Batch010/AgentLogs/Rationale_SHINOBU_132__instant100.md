# Rationale_SHINOBU_132

Agent: SHINOBU_132
Domain: Tether and Cable Physics
Session state: ACTIVE_VERIFICATION_LOOP_1

## Decision 00 - Recovery From Missing Active Logs

Problem: Active SHINOBU_132 status/rationale files were absent while archived Batch010 evidence and live SHINOBU_132 source files existed.

Solution: Recreate active files, re-extract the current XML block from `Docs/Tasks/CURRENT_BATCH.md`, and verify the live tree before making code claims.

Rejected Alternatives: Reporting from archive was rejected because active code can drift. Immediate broad rebuild was rejected until source violations were isolated and CPU/dotnet gates checked.

Scalability potential: Low tier uses low iteration counts and spline fakes; middle/high/ultra increase iterations and visual segment density through continuous `GlobalQualityWeight`.

Hardware Impact: 0 us runtime for the recovery itself; prevents wrong-agent work.

## Decision 01 - Solver Alias And Dump Repair

Problem: SHINOBU_132 Burst jobs used raw `CableNodeDTO*` fields without explicit `[NoAlias]`, the queue writer safety suppression lacked the mandated invariant proof, and the fault dump wrote only the agent-named file while Task 16 named `Dump_CABLE_SURGEON.bin`.

Solution: Add `[NoAlias]` to all SHINOBU_132 node pointer fields, add the three safety justification paragraphs above the `NativeDisableContainerSafetyRestriction`, and emit both binary dump filenames from the same 300-entry telemetry ring.

Rejected Alternatives: Adding a new Core event enum or BufferID enum was rejected because the solver already has owner-local numeric Vault IDs and routes force through the existing SignalBus payload path. Keeping one dump filename was rejected because the prompt and blackbox mandate name different aliases.

Scalability potential: NoAlias protects NEON/AVX vectorization as cable count scales. Dual dump output is fault/editor-only I/O and does not affect frame time.

Hardware Impact: i3/MX350 estimate: 1-5 us/frame protected at mock scale; higher when node count increases because alias pessimism no longer blocks vector-friendly memory assumptions.

## Decision 02 - Bio-Root LineRenderer Purge

Problem: `CaveBioRootsGenerator` used per-root `LineRenderer.SetPositions`, which is exactly the kind of CPU mesh rebuild the cable/vine task forbids for bioluminescent hanging roots.

Solution: Remove LineRenderer storage/creation/configuration and submit one procedural spline descriptor per root through the existing `ConnectionSplineBatchRenderer` route.

Rejected Alternatives: Leaving the system as decorative was rejected because SHINOBU_132 includes bio-luminescent vines connecting the environment. Creating a new renderer API was rejected because the core spline batcher already exists.

Scalability potential: Low devices submit cheap spline descriptors; high devices can buy shader detail in the shared spline renderer without adding CPU segments.

Hardware Impact: i3/MX350 estimate: removes up to 32 `LineRenderer.SetPositions` calls in cave-root scenes, expected 20-80 us saved depending on root count and visibility.