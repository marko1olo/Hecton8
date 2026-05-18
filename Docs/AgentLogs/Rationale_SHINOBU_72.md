# Rationale_SHINOBU_72

State: PENDING VERIFICATION

## Decision 00: Work Boundary

Problem: Save compression touches shared persistence code while other agents may be active.
Solution: Limit edits to SaveSystem/Core Memory/Editor facade files and required SHINOBU_72 logs.
Rejected Alternatives: Broad SaveManager rewrite; too much regression surface and unnecessary dependency on inventory/construction agents.
Scalability potential: Low devices avoid cosmetic I/O; middle devices process critical plus sparse cosmetic leaves; high and ultra devices keep richer payloads without blocking the main thread.
Hardware Impact: Target gain on i3/MX350/MicroSD is hitch reduction by moving raw save writes into sparse deltas and throttled WAL writes.

