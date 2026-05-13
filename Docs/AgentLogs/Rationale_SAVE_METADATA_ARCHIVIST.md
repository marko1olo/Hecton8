# Rationale_SAVE_METADATA_ARCHIVIST

Status: PENDING VERIFICATION  
Owner: SAVE_METADATA_ARCHIVIST  

## Decision 001 - Prompt and Domain Boundary

Problem: The batch file contains neighboring agent prompts and stale batch archives. Reading the wrong block would corrupt ownership.
Solution: Extracted only `<AGENT_PROMPT id="SAVE_METADATA_ARCHIVIST">...</AGENT_PROMPT>` from `Docs/Tasks/CURRENT_BATCH.md` via CLI regex and counted 19 tasks from the tag.
Rejected Alternatives: Manual IDE tab memory; archive `CURRENT_BATCH.md` files; MCP resource read that could truncate or blend context.
Scalability potential: Low tier avoids screenshot cost entirely; high/ultra can spend the saved frame time on richer save metadata presentation once runtime proof exists.
Hardware Impact: Expected save hitch reduction target is removal of the stated 150 ms synchronous readback stall on i3/MX350; measured gain is PENDING.

## Decision 002 - Mandate Set

Problem: Async save screenshots span save persistence, GPU readback, native memory, UI thumbnail streaming, telemetry, and GlobalRegistry/signal boundaries.
Solution: Loaded eight mandates directly covering those seams before source edits.
Rejected Alternatives: Treating screenshot capture as isolated UI work; adding ad hoc singleton service; using dated report text as authority.
Scalability potential: Low = empty metadata screenshot. Middle = async 256x144 compressed thumbnail. High = same core path with richer UI presentation, not larger uncontrolled runtime captures. Ultra = optional visual overkill after profiler proof.
Hardware Impact: Expected low-end gain is avoiding synchronous GPU/CPU readback and PNG encode stalls; expected high-end impact is extra visual metadata without blocking save flow. Exact microseconds PENDING.

