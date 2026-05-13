# BACKEND_MACRO_DB_COMPACTOR Rationale

Status: PENDING VERIFICATION

## Decision 0: Domain And Mandate Selection
Problem: The prompt asks for B-Tree tombstone sweeping in the append-only macro database without stopping gameplay or corrupting active save files.
Solution: Bound implementation to Core Database / persistence contracts. Read save, async, native memory, zero-GC, telemetry, registry, performance, persistent registry, and arena mandates before touching source.
Rejected Alternatives: A standalone compactor outside the database owner would duplicate ownership and race active writes. A SaveBinaryStorage-only defrag patch would miss the explicit macro database contract.
Scalability potential: Low uses larger dead-byte threshold and fewer disk writes; Middle/High/Ultra can compact earlier and preserve MicroSD on low hardware while keeping high-tier storage clean.
Hardware Impact: Expected low-end i3/MX350 gain is avoided multi-GB save bloat and fewer long read scans; runtime frame gain is zero by design because copy work stays off the main thread.
