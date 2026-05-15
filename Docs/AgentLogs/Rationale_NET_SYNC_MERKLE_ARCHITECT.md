# Rationale - NET_SYNC_MERKLE_ARCHITECT - BACKEND_ENGINEER

## Decision 001 - Preserve Contaminated BACKEND_ENGINEER Evidence

Problem: The required `Status_BACKEND_ENGINEER.md` and `Rationale_BACKEND_ENGINEER.md` files already contain prior BACKEND_ENGINEER prompt evidence. Deleting them would destroy existing work; appending unscoped NET_SYNC state would pollute current prompt evidence.
Solution: Keep the existing files intact, create prompt-specific `Status_NET_SYNC_MERKLE_ARCHITECT.md` and `Rationale_NET_SYNC_MERKLE_ARCHITECT.md`, then append the final summary to `LOG_BACKEND_ENGINEER.md` for CTO routing.
Rejected Alternatives: Wiping prior evidence after the user explicitly said to keep work; mixing unlabelled state into the shared BACKEND_ENGINEER files; stopping again on hygiene alone.
Scalability potential: Low/Middle/High/Ultra unaffected; this is evidence isolation.
Hardware Impact: 0 us/frame on i3/MX350. No runtime path touched.

## Decision 002 - Design Artifact Before Runtime Owner

Problem: Source scan showed current AUP, GlobalDataVault, GlobalSignals, and state-hash infrastructure, but no implemented lockstep/Merkle networking owner to extend safely.
Solution: Produce a protocol specification and deterministic offline simulator only. Runtime C# code will need a later owner assignment and interface contract expansion.
Rejected Alternatives: Inventing a concrete `NetSyncManager` now would create direct dependencies and likely collide with other agents. Using existing logistics sync packet layout as the full multiplayer contract would underspecify player inputs and rollback.
Scalability potential: Low tier gets bounded packet/ring budgets; High/Ultra can spend saved network bandwidth on richer non-authority visual interpolation and diagnostics.
Hardware Impact: 0 us/frame in current change. Future runtime target is less than 0.1 ms per networking system tick with 0 B/frame GC.

## Decision 003 - Fixed Binary Packet Records

Problem: Lockstep packets must be cheap to parse and deterministic under Burst/native transport; variable JSON or string command names would allocate, bloat bandwidth, and destroy deterministic replay.
Solution: Defined a 24-byte `H8NetEnvelope`, 16-byte `InputStateRecord`, 32-byte `WorldDeltaHeader`, and 32-byte `WorldDeltaRecord` with little-endian fixed offsets, sequence/ack fields, and strict reject policy.
Rejected Alternatives: JSON packets, Unity object references, display-name RPCs, and per-action event strings were rejected because they violate zero-GC and signal discipline. Reusing the logistics `DeltaPacket` alone was rejected because player input rollback needs tick, sequence, prediction, and redundant input records.
Scalability potential: Low tier parses compact fixed records. Middle/High/Ultra can spend saved bandwidth on richer visual interpolation and diagnostics, not authority bloat.
Hardware Impact: Current change is 0 us/frame. Future packet parsing avoids managed allocation and avoids variable-field scanning; estimated save versus JSON/string payloads is 50-200 us per packet burst on i3/MX350.

## Decision 004 - Merkle Diff Instead Of Whole-World Resend

Problem: `MasterStateHash` mismatch must identify the divergent DataVault page quickly without broadcasting the entire world or trusting a single opaque hash.
Solution: Defined sorted Merkle leaves over input, GlobalDataVault page projections, AUP fence state, and gameplay-affecting signal lane bytes. Compare root, descend only mismatched branches, then apply `WorldDelta` into a back buffer.
Rejected Alternatives: Whole-world snapshots every mismatch were rejected as bandwidth spikes. A single flat root hash was rejected because it detects desync but cannot localize it. Hashing presentation floats was rejected as FLOAT_HASH_CRIME.
Scalability potential: Low probes fewer leaves except on suspected desync; High/Ultra can keep per-tick sibling hashes and richer debug views.
Hardware Impact: Current change is 0 us/frame. Future mismatch recovery sends page deltas instead of full state; savings scale with world size and avoid multi-millisecond resend/decode bursts.

## Decision 005 - 64-Tick Rollback Window

Problem: 200ms latency plus packet loss can produce late inputs. The system needs a bounded rewind rule instead of unbounded replay or silent divergence.
Solution: Defined a 256-tick input ring, 128-tick state snapshot ring, and max rollback of 64 fixed ticks / 1.28 seconds. Beyond that, the peer requests Merkle delta or full snapshot.
Rejected Alternatives: Stalling forever was rejected because it can deadlock the session. Unlimited rollback was rejected because it can create unbounded CPU spikes. Authority smoothing was rejected because smoothing belongs only in presentation.
Scalability potential: Low uses 16-tick input delay to avoid most rollbacks; High/Ultra can use richer prediction displays while preserving the same authority window.
Hardware Impact: Current change is 0 us/frame. Future common path has no replay at the tested 16-tick delay. Stress path proved replay corrections are bounded to 4 ticks in the simulator.

## Decision 006 - Anchor-Relative AUP64

Problem: Sending full `int64x3` AUP plus local fields for every networked position wastes bandwidth, but sending floats or presentation positions corrupts authority.
Solution: Defined `AupLocal64` as three signed 21-bit millimeter local deltas relative to an AUP anchor, with bit 63 as overflow. Overflow requires a cold full-anchor record.
Rejected Alternatives: `Vector3` was rejected because presentation coordinates are not authority. Full `int64x3` on every input/world packet was rejected as wasteful. Clamping overflow was rejected because it hides invalid authority state.
Scalability potential: Low gets compact stable coordinates. High/Ultra can use saved bandwidth for richer non-authority remote visuals and diagnostics.
Hardware Impact: Current change is 0 us/frame. Future common position payload drops to 8 bytes when inside anchor range, saving 16+ bytes per position compared with full anchor payloads.

## Decision 007 - Offline Jitter Simulator With Hash Self-Audit

Problem: Prose alone does not prove the lockstep ring and MasterStateHash sequence converge under latency/loss.
Solution: Added `Tools/NetJitterSim.py` with deterministic integer PRNG, redundant input bundles, rollback replay, Merkle-style root hashing, report export, and AST self-audit for float math inside hash functions.
Rejected Alternatives: Spreadsheet math was rejected because it cannot validate replay sequence. Adding runtime C# was rejected until a source-backed network owner exists.
Scalability potential: Low/Middle/High/Ultra benefit through an offline gate that catches protocol drift before runtime implementation.
Hardware Impact: 0 us/frame. Tool is offline. Baseline payload estimate is 26,903 B/s for the simulated two-client envelope.

## Decision 008 - Add Four-Client Sanity Coverage

Problem: Two-peer lockstep passing is necessary but weak for co-op packet fan-out because packet count and input masks scale with peer count.
Solution: Ran a 4-client 600-tick pass at the same 200ms latency, 40ms jitter, and 5% loss. The run produced 7776 sent packets, 390 lost packets, 0 `MasterStateHash` mismatches, 0 `InputRingBuffer` mismatches, and float hash audit PASS.
Rejected Alternatives: Treating the two-client run as sufficient was rejected because `ExpectedPeerMask` and redundant packet fan-out have different failure surfaces with more peers.
Scalability potential: Low tier remains viable at the two-client envelope; 4-client payload estimate is 161,422 B/s, still acceptable for LAN/normal broadband but requires runtime bandwidth caps and interest management before shipping.
Hardware Impact: 0 us/frame current. Future runtime must profile 4-client packet drain; no runtime claim is made from this offline result.

## Decision 009 - Polish Boundary And Final Evidence

Problem: The batch protocol requires reading `<POLISH_MANDATE>` only after all tasks are checked. The active batch file may or may not include that tag.
Solution: Extracted `<POLISH_MANDATE>` after all seven tasks were checked; result was `POLISH_MANDATE_NOT_FOUND`. Ran final anti-bloat/static checks and appended the CTO report to `Docs/AgentLogs/LOG_BACKEND_ENGINEER.md`.
Rejected Alternatives: Reading polish instructions before task closure would violate the prompt protocol. Skipping final log append would violate the reporting protocol.
Scalability potential: Low/Middle/High/Ultra unaffected; evidence-only step.
Hardware Impact: 0 us/frame.

## Decision 010 - Add Reusable Regression Gate

Problem: One-off simulator reports prove the current run, but they do not prevent future edits from breaking rollback, redundant packet clamping, 4-client fanout, or float-crime detection.
Solution: Added `Tools/test_net_jitter_sim.py` with five unittest cases covering baseline convergence, rollback correction, 4-client convergence, redundant packet tick range clamping, and detection of float constants/division in hash functions.
Rejected Alternatives: Depending only on JSON artifacts was rejected because it allows future simulator changes to regress silently. Adding Unity runtime tests was rejected because no runtime network owner exists yet.
Scalability potential: Low/Middle/High/Ultra benefit from a cheap offline protocol gate before runtime work. High/Ultra debug additions must still pass the same authority tests.
Hardware Impact: 0 us/frame; tests are offline and run under `python -B` to avoid bytecode cache churn.

## Decision 011 - Final Post-Hardening Rerun

Problem: The simulator and docs changed after the first final report, so old evidence could become stale.
Solution: Reran baseline, rollback-stress, and 4-client simulations under `python -B`, reran `Tools.test_net_jitter_sim`, revalidated JSON reports and protocol doc sections, checked NET_SYNC pycache cleanliness, and ran `git diff --check` on every owned NET_SYNC file/log.
Rejected Alternatives: Reusing the pre-test-harness report artifacts was rejected because the simulator file changed, even though the semantic change was only dead-code removal.
Scalability potential: Low/Middle/High/Ultra unchanged; evidence refreshed after hardening.
Hardware Impact: 0 us/frame.
