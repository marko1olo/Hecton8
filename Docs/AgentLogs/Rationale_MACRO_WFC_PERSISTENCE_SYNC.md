# MACRO_WFC_PERSISTENCE_SYNC Rationale

Status: PENDING VERIFICATION

## Decision 0: Task Scope Identification
Problem: WFC-generated outpost mutable state disappears on reload because generated topology and player mutations are not mapped into the binary delta persistence path.
Solution: Inspect existing save/database/WFC/signal contracts first, then add the thinnest interface-backed persistence bridge that stores mutable bits by absolute sector hash.
Rejected Alternatives: Direct concrete WFC-to-database reference; JSON save sidecar; full grid blob writes every state change. These violate decoupling, SaveManager contract, and microSD write budget.
Scalability potential: Low tier stores exact mutable truth with the smallest bitmask. Middle tier may batch more sectors per write. High/Ultra can spend saved IO on richer restored presentation state without changing persistence truth.
Hardware Impact: Expected i3/MX350 gain versus full 500-byte mutable byte-grid writes is sub-0.01 ms CPU and fewer microSD writes; measured proof absent.

