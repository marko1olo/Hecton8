# ASYNC_PERSISTENCE_SURGEON Rationale

Status: PENDING VERIFICATION

## Decision 0: Batch Scope
Problem: Save hitches are reported at 200ms from main-thread binary serialization and LZ4 work.
Solution: Treat this as Data Archivist persistence work. Main thread may only perform bounded snapshot copy into a persistent staging buffer; compression and disk IO must run from an owned async persistence service.
Rejected Alternatives: Keep SaveManager.Instance and hide File.WriteAllBytes behind a helper. Rejected because singleton access violates the registry mandate and direct writes still risk main-thread stalls.
Scalability potential: Low tier uses the same persistence correctness with minimal status signals; Middle tier can record extra telemetry; High and Ultra can spend saved frame time on richer UI feedback without increasing save truth cost.
Hardware Impact: i3/MX350 target saves the 200ms hitch by removing compression and file IO from the frame; expected main-thread budget is a bounded copy under 5ms pending profiler proof.

## Decision 1: LZ4 Dictionary Scope
Problem: Prompt requires LZ4, but mandate forbids dictionary mode without offline corpus proof and native dictionary bindings.
Solution: Use existing baseline LZ4 binding or project codec surface only. Do not add LZ4 dictionary APIs unless the codebase already owns them.
Rejected Alternatives: Add dictionary compression now. Rejected because current mandate says dictionary APIs are not bound/version-pinned and no benchmark exists.
Scalability potential: Low/Middle/High/Ultra all use the same save format for compatibility; future Ultra compression can be a versioned format upgrade after corpus proof.
Hardware Impact: Avoids risky codec churn on low-end silicon; expected gain is stability, not ratio inflation.
