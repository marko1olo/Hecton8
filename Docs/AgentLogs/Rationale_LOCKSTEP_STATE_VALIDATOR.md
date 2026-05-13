# LOCKSTEP_STATE_VALIDATOR Rationale

Status: PENDING VERIFICATION

Problem: The batch requires deterministic replay hashing without depending on systems that may not exist or may be owned by other agents.
Solution: Use GlobalRegistry/EventBus boundaries, discover existing contracts first, and implement a narrow determinism assembly if no existing owner exists.
Rejected Alternatives: Directly editing physics, inventory, VFX, or audio owners before interface discovery would create brittle cross-domain coupling and break parallel-agent isolation.
Scalability potential: Low disables normal-play hashing except replay mode; Middle hashes every 300 POST_SIMULATION frames; High keeps full diagnostic subsystem hashes; Ultra can retain extra individual category hashes and replay diagnostics for visual overkill tooling.
Hardware Impact: On i3/MX350, a 300-frame cadence amortizes hashing cost and keeps normal play at 0us when disabled by tier. The target is a bounded POST_SIMULATION spike below 0.1ms on sampled frames, pending Unity/Burst profiler proof.

Problem: Origin shifts can create false desyncs if world-space floats are hashed.
Solution: Hash AUP sector-local state and stable IDs, not shifted `Transform.position` floats.
Rejected Alternatives: Hashing Unity transforms or rigidbody world positions is faster to wire but invalid under AUP rebase and PhysX presentation drift.
Scalability potential: Low stores master hash only; Middle stores subsystem hashes; High/Ultra store individual category hash vectors for replay forensic UI.
Hardware Impact: Avoids repeated transform reads and float instability, saving main-thread work and preventing false replay breakage on MX350/i3.
