# Global Authority Route Card Template

Date: 2026-05-19
Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-19 R32 Root/Architecture R4/Proof-Wording Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

Current root/architecture boundary is `Docs/Reports/2026-05-19_DOCUMENTATION_R32_ARCHITECTURE_R4_AND_PROOF_WORDING_LOCAL.md`. R31 remains the prior current-boundary propagation correction; R30 remains the prior internal-currentness correction; R29 remains the prior stale-gate/global-authority correction; R28 remains the prior interior-boundary correction; R27 remains the latest source-counter/index snapshot only until a newer counter pass reruns it.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler,
Frame Debugger, player build, save/load route, or visual-route proof is implied
unless this document links a fresh evidence artifact. Historical counters and
older version claims inside this file are subordinate to the current authority
spine above.
R32 architecture R4/proof-wording correction is the latest artifact-backed local static DOC_GLOBAL boundary for architecture/root documentation. R31 remains the prior current-boundary propagation layer, R30 remains the prior internal-currentness layer, R29 remains the prior stale-gate/global-authority layer, R28 remains the prior interior-boundary layer, and R27 remains the latest source-counter/index snapshot until rerun.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Evidence class: `STATIC_DOC`. This file is a reusable review template, not
runtime proof.

Parents:

- `GLOBAL_AUTHORITY_BOUNDARIES.md`
- `GLOBAL_AUTHORITY_OPERATING_MODEL.md`
- `GLOBAL_AUTHORITY_SETUP_PLAYBOOK.md`
- `GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md`
- `GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`
- `QUALITY_GATES.md`

## Purpose

Use this template before adding or changing any route through:

- `GlobalRegistry`
- `SignalBus<T>`
- direct `GlobalSignals` queue/bridge
- `HectonEventBus`
- `GlobalDataVault`
- cross-domain native handles/snapshots
- global telemetry or crash-state routes

Do not create a route card for purely owner-local code. Owner-local code should
stay owner-local.

## Route Card

Copy this block into the task status, source rationale, design PR, or review
note. A route with unknown answers is not approved. Use
`GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md` to mark the final result as `GREEN`,
`YELLOW`, `RED`, or `KILL`.

```text
Route ID:
Date:
Owner:
Owner domain:
Owning file/system:

Problem:
Why owner-local data is insufficient:
Why direct caller/owner interface is insufficient:

Instrument:
  [ ] GlobalRegistry cold service/interface
  [ ] SignalBus<T> first-party broadcast
  [ ] GlobalSignals bridge/direct queue
  [ ] HectonEventBus mod/API/cold event
  [ ] GlobalDataVault / IDataVault
  [ ] Black-box/telemetry route

Producer phase:
Consumer phase:
Cadence:
Expected max events/reads per frame:
GlobalQualityWeight behavior:

Payload/data shape:
Managed fields present: yes/no
UnityEngine.Object fields present: yes/no
Layout proof:
Capacity:
Overflow/failure mode:

Telemetry fields:
Black-box fields:
Profiler marker:
GC proof required:

Shutdown/disposal rule:
Scene unload behavior:
Stale-handle behavior:

Rejected alternatives:
  [ ] owner-local field
  [ ] cached owner interface
  [ ] existing SignalBus lane
  [ ] existing Vault buffer
  [ ] cold HectonEventBus hook
  [ ] no global route needed

Why this does not increase global monolith risk:
H-Phi impact expected:
Runtime proof required before acceptance:
Reviewer:
Status: PROPOSED / ACCEPTED / REJECTED / BLOCKED
```

## Approval Rules

The reviewer rejects the route when any answer is missing for:

- owner
- instrument
- producer phase
- consumer phase
- cadence
- capacity or expected max frame count
- overflow/failure mode
- telemetry or black-box fields
- shutdown/disposal rule
- proof required before acceptance

The reviewer rejects the route immediately when:

- the route exists to improve H-Phi only
- the route hides a one-caller request/response behind a broadcast
- `GlobalRegistry` is used for live state polling
- `HectonEventBus` is used for first-party hot gameplay
- `GlobalSignals` grows without bridge-migration rationale
- `GlobalDataVault` is used for local scratch or absent systems
- a signal payload carries managed collections, strings, delegates, or Unity
  object references
- no black-box or telemetry state can explain failure

## Instrument-Specific Minimums

### GlobalRegistry

Required:

- interface or service slot name
- bootstrap owner
- shutdown owner
- whether dependency is cached after injection
- rebound behavior if service changes live
- proof that the route is not read in Tick/FixedTick/LateUpdate/render/audio jobs

Reject when:

- slot represents a future/absent system
- slot exposes a concrete leaf-domain class without interface need
- route replaces an owner-local reference only for convenience

### SignalBus<T>

Required:

- payload struct name
- owner assembly/domain
- producer and consumer phases
- max events per frame
- overflow policy
- retention policy
- duplicate lane-name scan
- unmanaged/layout proof
- finite-value sanitization for floats
- pushed/dropped/coalesced telemetry

Reject when:

- one private caller is the only consumer
- payload is catch-all enum/switch state
- payload carries Unity objects or managed data
- another lane already owns the same truth

### GlobalSignals Bridge

Required:

- bridge owner
- source lane
- target typed `SignalBus<T>` lane
- drain phase
- migration stop condition
- telemetry for retained bridge traffic

Reject when:

- bridge has no migration target
- bridge becomes permanent default traffic
- direct queue is expanded for new gameplay

### HectonEventBus

Required:

- mod/API/cold reason
- callback watchdog relevance
- proof path is not hot gameplay
- payload id/hash
- external mod scope if applicable

Reject when:

- used from Tick/FixedTick/LateUpdate/UI refresh/audio/physics/render upload
- used to avoid unmanaged SignalBus payload design
- reported as zero-GC hot path

### GlobalDataVault

Required:

- `BufferID`
- `SystemID`
- owner
- capacity
- generation rule
- relocation/defrag behavior
- stale-handle behavior
- disposal/release rule
- reader fencing
- crash/telemetry fields

Reject when:

- data is owner-local scratch
- buffer represents an absent future system
- raw native reference crosses domain instead of handle/snapshot
- no unload baseline or stale-handle proof is planned

## Filled Example - Proposed SignalBus Route

```text
Route ID: PLAYER_SURFACE_STATE_CHANGED
Owner: HectonPlayerMovement
Owner domain: Player Kinematics
Instrument: SignalBus<PlayerSurfaceStateChangedSignal>
Producer phase: POST_SIMULATION
Consumer phase: VISUAL_SYNC / UI
Cadence: dirty only, max 1/player/frame
Payload/data shape: unmanaged 32-byte struct, player id, state enum byte, depth01, frame id
Capacity: 8
Overflow/failure mode: coalesce by player id, keep latest
Telemetry fields: pushed, coalesced, invalid-depth count
Shutdown/disposal rule: lane shutdown by GlobalSignals/SystemDispatcher teardown
Why owner-local data is insufficient: atmosphere, audio, HUD, and survival presentation need fan-out
Rejected alternatives: registry polling by every consumer
Proof required before acceptance: Play Mode transition sweep, Profiler/GC 0 B, overflow counter clean
Status: PROPOSED
```

## Filled Example - Rejected Registry Route

```text
Route ID: GLOBAL_CURRENT_DEPTH
Owner: HectonPlayerMovement
Instrument: GlobalRegistry service/property
Cadence: every UI/audio/visual frame
Reason for rejection: live state polling through registry; consumers need a cached dirty signal snapshot
Required replacement: SignalBus<PlayerSurfaceStateChangedSignal> plus cached owner interface for rare direct query
Status: REJECTED
```

## Storage

Route cards may live in:

- `Docs/Tasks/Status_[ID].md` for agent work
- `Docs/AgentLogs/Rationale_[ID].md` for decisions
- source comments only when the route is small and stable
- a domain architecture doc when the route is a long-lived public contract

Do not store route cards only in chat.
