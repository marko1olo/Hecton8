# Rationale_SHINOBU_132

Agent: SHINOBU_132
Domain: Tether and Cable Physics
Session state: ACTIVE_VERIFICATION_RECONSTRUCTION

## Decision 00 - Recovery From Missing Active Logs

Problem: The active `Docs/Tasks/Status_SHINOBU_132.md` and `Docs/AgentLogs/Rationale_SHINOBU_132.md` files were absent, while archived Batch010 evidence and live SHINOBU_132 source files exist. Continuing from chat memory would violate the anti-amnesia protocol.

Solution: Recreate active status/rationale files, re-extract the current XML block from `Docs/Tasks/CURRENT_BATCH.md`, read the relevant mandates, and verify the live tree before editing. Treat archived Batch010 records as historical evidence only, not as current proof.

Rejected Alternatives: Reporting from archived logs was rejected because active code may have drifted. Re-running a broad build immediately was rejected because the user forbade unnecessary builds and AGENTS forbids build under active dotnet/csc or high CPU.

Scalability potential: Low tier requires cheap Verlet iteration counts and spline fakes; middle/high/ultra must preserve the same data route while increasing iterations and visual segment density through continuous `GlobalQualityWeight`.

Hardware Impact: i3/MX350 impact is evidence-driven; expected savings only count after scans prove Unity joints and LineRenderer cable paths are out of first-party hot paths.

## Decision 01 - Batch Tag Parsing

Problem: A strict `<AGENT_PROMPT id="SHINOBU_132">` regex failed because the current batch tag includes `role` and `chat_name` attributes.

Solution: Use content search and a broader extraction pattern for `<AGENT_PROMPT id="SHINOBU_132" ...>...</AGENT_PROMPT>`.

Rejected Alternatives: Assuming the block was missing was rejected after `rg` found the exact source lines at `Docs/Tasks/CURRENT_BATCH.md:1827`.

Scalability potential: None at runtime; this protects task routing.

Hardware Impact: 0 us runtime; prevents coding against the wrong agent contract.

## Decision 02 - First Live Violations

Problem: Live SHINOBU_132 code already contains most solver surfaces, but pointer fields in jobs lack explicit `[NoAlias]`, `NativeDisableContainerSafetyRestriction` on the SignalBus writer lacks the mandated justification, and the blackbox writer emits only `Dump_SHINOBU_132.bin` while Task 16 names `Dump_CABLE_SURGEON.bin`.

Solution: Patch only the SHINOBU_132 solver to add NoAlias to raw node pointers, document the queue writer safety invariant, and write both dump filenames from the same telemetry ring.

Rejected Alternatives: Adding new core event enums or global BufferID enum entries was rejected because SHINOBU_132 already uses owner-local numeric Vault IDs and an existing SignalBus route. Leaving only the agent-named dump was rejected because the task explicitly names the Cable Surgeon alias.

Scalability potential: NoAlias gives Burst a clearer vectorization contract across low and high hardware. Dual dump naming is cold fault I/O only.

Hardware Impact: i3/MX350 estimate: NoAlias removes a conservative aliasing barrier in node iteration; expected gain is small at mock scale, roughly 1-5 us/frame, but protects NEON/AVX lanes as cable counts scale.

## Decision 03 - Cave Bio-Root LineRenderer

Problem: `CaveBioRootsGenerator` uses `LineRenderer` per tick for bioluminescent hanging root/vine visuals. This falls under the task's cable/vine visual purge even though it is not a tow cable solver.

Solution: Replace the root LineRenderer path with the existing procedural `ConnectionSplineBatchRenderer` spline submission. The visual becomes a mathematical spline descriptor over cached root endpoints rather than a Unity mesh rebuild each tick.

Rejected Alternatives: Keeping LineRenderer as "decorative only" was rejected because the SHINOBU_132 prompt explicitly includes bio-luminescent vines connecting the environment. Creating a new renderer domain API was rejected because an existing core spline batch route already exists.

Scalability potential: Low tier gets one spline descriptor per root instead of per-root LineRenderer mesh updates; high/ultra can improve shader detail through the spline renderer without more CPU nodes.

Hardware Impact: i3/MX350 estimate: removes `LineRenderer.SetPositions` mesh rebuild pressure for up to 32 cave roots, expected 20-80 us saved in root-heavy caves depending on visibility.
