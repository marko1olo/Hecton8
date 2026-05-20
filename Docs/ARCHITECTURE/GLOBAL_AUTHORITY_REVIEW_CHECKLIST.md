# Global Authority Review Checklist

Date: 2026-05-19
Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R46 Root/Architecture Actuality Boundary
This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

Current root/architecture boundary is `Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md` as STATIC_DOC/STATIC_SOURCE/FILESYSTEM/PY_TOOL evidence. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction. R42 remains the prior counter/route-boundary/proof-label correction. R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers; runtime proof remains absent.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler,
Frame Debugger, player build, save/load route, or visual-route proof is implied
unless this document links a fresh evidence artifact. Historical counters and
older version claims inside this file are subordinate to the current authority
spine above.
R46 root/architecture interior-authority/route-field/proof-language correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md`) is the latest local static DOC_GLOBAL boundary for architecture/root documentation. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41 remains the prior global-authority/internal-residue correction; R40 remains the prior R38-residue/source-counter correction; R39 remains the prior authority-counter/proof-wording correction; R38/R37/R36/R35/R34 remain prior static correction layers. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Evidence class: `STATIC_DOC`. This is a review/merge checklist, not runtime
proof.

Parents:

- `GLOBAL_AUTHORITY_BOUNDARIES.md`
- `GLOBAL_AUTHORITY_OPERATING_MODEL.md`
- `GLOBAL_AUTHORITY_SETUP_PLAYBOOK.md`
- `GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`
- `GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`
- `QUALITY_GATES.md`

## Purpose

Use this file when reviewing any change that adds, removes, or changes:

- `GlobalRegistry`
- `SystemDispatcher`
- `SignalBus<T>`
- direct `GlobalSignals`
- `HectonEventBus`
- `GlobalDataVault` / `IDataVault`
- global telemetry, crash rings, replay, save/load, or cross-domain native state

The review stance is not "are globals bad?" Correctly bounded globals are
required. The question is whether the route has one owner, one purpose, one
instrument, one phase contract, and one proof path.

## Senior Review Rule

Default stance is "not `GREEN`" until the route proves:

```text
owner-local is insufficient
owner interface is insufficient
selected global instrument is the narrowest correct instrument
runtime/proof debt is not hidden behind H-Phi movement
```

No route is accepted because it is convenient, future-proof, easier to grep, or
improves a static metric.

## Triage Result

Use exactly one result in review notes:

| Result | Meaning |
|---|---|
| `GREEN` | Route card complete, narrow instrument, required static evidence attached, and runtime/profiler/player proof attached when the route is runtime-facing; a proof plan alone is `YELLOW` unless the changed surface is documentation-only |
| `YELLOW` | Concept is valid, but proof/telemetry/capacity/shutdown fields are incomplete; merge blocked until fixed |
| `RED` | Wrong instrument or ownership model; rewrite before merge |
| `KILL` | Route creates global monolith behavior, managed hot traffic, mutable global heap, or H-Phi gaming |

## Fast Decision Matrix

| Need | Correct First Move | Global Instrument Only If |
|---|---|---|
| one owner, local state | owner-local fields/native collections | never |
| one caller needs command/data | owner interface | caller lifecycle crosses bootstrap or domain assembly boundary |
| many first-party systems need state-change notification | owner interface plus typed event DTO | use `SignalBus<T>` after fan-out and phase are real |
| external mod/API/plugin needs notification | cold managed API event | use `HectonEventBus` with watchdog/callback depth proof |
| persistent or job-visible cross-domain native state | owner buffer and snapshot contract | use `GlobalDataVault` with BufferID/SystemID/generation proof |
| stable service identity needed by bootstrap/dispatcher/save/telemetry | owner interface | use `GlobalRegistry` for cold discovery only |
| order/completion issue | local phase ownership | use `SystemDispatcher` when phase barrier is cross-system |
| crash explanation needed | local ring first | use global telemetry when failure crosses subsystem boundary |

## Immediate Rejection

Reject the change immediately when any item is true:

- no route card for a new or changed global route
- no owner domain
- no producer phase or consumer phase
- no cadence or max-per-frame estimate
- no shutdown/disposal/unregister behavior
- no overflow/failure behavior
- no telemetry or black-box fields
- `GlobalRegistry` is read from Tick/FixedTick/LateUpdate/render/audio jobs
- `HectonEventBus` carries first-party hot gameplay traffic
- direct `GlobalSignals` grows without bridge owner and migration stop condition
- `SignalBus<T>` payload carries managed data, strings, delegates, or Unity objects
- `SignalBus<T>` is used for one private caller
- `GlobalDataVault` stores local scratch or speculative future buffers
- `GlobalDataVault` lacks BufferID/SystemID/generation/stale-handle behavior
- global route exists only to raise H-Phi
- old debt is hidden while new global surface is added

## Instrument Review

### GlobalRegistry

Accept only for cold identity. The service must be cached by the consumer after
injection/bootstrap. A reviewer must find a named owner for registration,
unregistration, and absent-service behavior.

Reject if it becomes:

- live state lookup
- mutable settings store
- concrete leaf-domain warehouse
- per-frame capability query

### SystemDispatcher

Accept only for time/phase/completion ownership. A reviewer must see the phase
name, order, max work budget, and `.Complete()`/drain window.

Reject if it becomes:

- hidden general job runner
- random work queue
- place to bury blocking completions

### SignalBus<T>

Accept only for typed first-party broadcast. A reviewer must see payload layout,
lane owner, duplicate-lane scan, max events, overflow, retention, finite-float
sanitization, and pushed/dropped/coalesced counters.

Reject if it becomes:

- request/response API
- catch-all enum event stream
- route for managed/Unity-object payloads
- replacement for one direct interface call

### GlobalSignals Direct Queues

Accept only as bridge or explicitly owned low-level infrastructure. A reviewer
must see bridge owner, drain phase, typed target lane, and migration stop
condition.

Reject if it becomes:

- default new traffic path
- undocumented NativeQueue expansion
- permanent mixed event corridor

### HectonEventBus

Accept only for mod/API/cold managed isolation. A reviewer must see external
scope, payload id/hash, callback watchdog reason, and proof it is outside hot
gameplay.

Reject if it becomes:

- gameplay bus
- UI/audio/render/physics refresh path
- way to avoid unmanaged SignalBus payload design

### GlobalDataVault

Accept only for real cross-domain, persistent, job-visible, relocation-relevant,
or replay/crash-visible native state. A reviewer must see BufferID, SystemID,
owner, capacity, generation, stale-handle behavior, disposal, defrag/release
behavior, and black-box fields.

Reject if it becomes:

- local scratch allocator
- global mutable heap
- H-Phi padding
- ownerless unmanaged memory dump

## Evidence Required

Minimum evidence before `GREEN`:

- route card path or pasted card
- source diff path
- owner domain and owning file/system
- static grep showing no forbidden hot-path access
- compile/Unity Console artifact if runtime source changed; a plan alone keeps the route `YELLOW`
- profiler/GC/player proof artifact for hot route; a plan alone is not `GREEN` evidence
- telemetry fields for failure reconstruction

H-Phi is allowed only as static pressure evidence. It cannot convert `YELLOW`,
`RED`, or `KILL` into `GREEN`.

## Review Commands

Use commands as orientation. Update them when file layout changes.

```powershell
rg -n "GlobalRegistry\.|GlobalRegistry.Get<" Assets/_Project/Scripts --glob "*.cs"
rg -n "HectonEventBus\.|GlobalSignals\.Publish|SignalBus<.*>\.(Push|TryPush|Subscribe)" Assets/_Project/Scripts --glob "*.cs"
rg -n "new NativeArray<|NativeArray<|NativeList<|GlobalDataVault|IDataVault" Assets/_Project/Scripts --glob "*.cs"
rg -n "\.Complete\(|CompleteDependency|JobHandle" Assets/_Project/Scripts --glob "*.cs"
rg -n "GLOBAL_AUTHORITY_ROUTE_CARD|Route ID:|Why owner-local data is insufficient" Docs Assets/_Project --glob "*.md" --glob "*.cs"
```

These scans do not prove runtime behavior. They identify review targets only.

## Review Note Template

```text
Global authority review:
Result: GREEN / YELLOW / RED / KILL
Route ID:
Owner:
Instrument:
Reason:
Required fixes:
Proof still missing:
Reviewer:
Date:
```

## Hard Rule

If a reviewer cannot explain the route in one sentence without using the words
"global", "generic", "future", or "convenient", the route is not narrow enough.


