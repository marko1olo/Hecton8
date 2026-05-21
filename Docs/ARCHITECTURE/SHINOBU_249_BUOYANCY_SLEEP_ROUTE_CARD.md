# SHINOBU_249 Buoyancy Sleep Route Card

Date: 2026-05-21
Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-21 R51 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, shader import, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-21 R51): `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md` is the latest local static root/architecture encoding repair, boundary-gap, read-order, route-card/static-contract, and source/AtlasCheck orientation correction. R50 remains the prior generated-atlas regeneration, stale R48 interior-boundary, dump-target wording, and source-counter drift correction. R49 remains the prior AtlasCheck-red-state/boundary-gap/route-field/source-counter correction. R48 remains the prior date-rollover/AtlasCheck/source-counter correction. R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current AtlasCheck remains red until `Tools/AtlasCheck.py` exits `0`; runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Evidence class: STATIC_DOC / STATIC_SOURCE. Runtime proof remains absent.

Route ID: `SHINOBU_249_BUOYANCY_SLEEP_STATE`
Owner: `SHINOBU_249`
Owner domain: Hydrodynamic Drag & Buoyancy / Physics Culling Overseer
Owning file/system: `BuoyancyDisplacementRuntime`
Active sleep truth: `BuoyancyStateDTO.Flags` for buoyancy force bypass.
KCC prompt artifact: `EvaluateKinematicSleepStateJob` exists under KCC as an isolated owner-local kernel, but Buoyancy does not schedule it or mutate `KinematicStateDTO` to avoid split authority.

Problem: 50,000 settled debris rows can continue running buoyancy/drag work after grounding.
Why owner-local data is insufficient: grounding, wake, telemetry, and material profile data must be visible to Burst jobs and diagnostic editor tools across the physics owner boundary.
Why direct caller/owner interface is insufficient: wake events can originate from multiple active systems; first-party broadcast snapshot is the correct fan-out route.

Instrument:
- GlobalDataVault / IDataVault
- SignalBus<T> first-party broadcast: `SignalBus<WakeRequestSignal>` existing lane
- Black-box/telemetry route

Producer phase:
- Wake snapshots are produced by the owning wake-capable systems through the existing `SignalBus<WakeRequestSignal>` lane. Cavitation force events already bridge to this core signal; Buoyancy does not import Cavitation `ForcePacketDTO` directly.
- Sleep telemetry is produced after buoyancy evaluation reduction.

Consumer phase:
- Wake snapshot is consumed before buoyancy evaluation in the physics fixed-tick path.
- Visual/static promotion remains a flag for the presentation owner to consume; the physics route does not mutate render state.

Cadence/capacity:
- `BuoyancyStateDTO`: 50,000 rows.
- `SleepStateTelemetryEntry`: 300 rows.
- `SleepSdfDensity`: 65,536 signed-byte cells.
- `MaterialSettlingProfiles`: 512 rows.
- Cold authoring source for material thresholds: `Assets/_Project/Data/Physics/material_settling_profiles.csv`; runtime jobs consume the Vault rows, not the CSV.
- Ambient current wake polling cadence scales continuously from 45 frames at low quality to 8 frames at high quality.

Expected max events/reads per frame:
- Wake requests: bounded by SignalBus snapshot length.
- State rows: stride-scaled active count.
- Telemetry writes: one 64-byte sleep row per frame.
- Force packet drain: evaluator overwrites every scheduled candidate slot with either a valid packet or `default`; compact reads only the scheduled candidate range and writes `Counters[0].ForcePackets`; drain reads only that counter, so stale packet capacity is not a runtime route.

GlobalQualityWeight behavior:
- Low quality inflates sleep thresholds and reduces rest-frame demand.
- Middle quality interpolates thresholds and polling cadence.
- High/ultra quality preserves stricter settling and uses saved CPU for presentation/static promotion evidence.
- Quality does not change DTO layout, save identity, route owner, or authority.

Accessor purity:
- No `Get/TryGet/Resolve/Read` API publishes signals.
- No `Get/TryGet/Resolve/Read` API syncs scene state.
- No `Get/TryGet/Resolve/Read` API allocates/grows buffers in gameplay phases.
- No `Get/TryGet/Resolve/Read` API completes jobs.
- No `Get/TryGet/Resolve/Read` API mutates global state.
- No `Get/TryGet/Resolve/Read` API searches the scene.

Payload/data shape:
- Managed fields present: no.
- UnityEngine.Object fields present: no.
- Layout proof: `BuoyancyStateDTO=64` with `AngularSpeedSq` at offset 56, `BuoyancySleepSdfConfigDTO=64`, `SleepStateTelemetryEntry=64`, `BuoyancyMaterialSettlingProfileDTO=32`, KCC `KinematicStateDTO=64` with `Flags` at offset 52.
- Vault BufferIDs: `71643` sleep SDF density, `71644` sleep SDF config, `71645` sleep telemetry ring, `71646` sleep telemetry cursor, and `71647` material settling profiles.
- Data Monolith status: the material CSV is a cold designer source file and does not imply `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` readiness.
- Endian boundary: runtime Vault rows are in-process little-endian payloads; future `.h8bin`, save, or network hydration must normalize byte order before ABI compatibility is claimed.
- Overflow/failure: absent SDF config fails closed to plane fallback in buoyancy path; non-finite rows flag telemetry and dump black-box ring.
- Authored flow samples are optional for sleep evaluation; absent flow data routes through deterministic analytic flow and still clears force-candidate slots.

Telemetry fields:
- active objects, sleeping objects, forced-awake objects, static-promotion candidates, non-finite count, compute microseconds, quality, SDF grounded count, wake request count, ambient current wakes, max sleep score.

Black-box fields:
- last 300 `SleepStateTelemetryEntry` rows.
- Planned/generated-on-fault dump target: `Docs/AgentLogs/Dump_SHINOBU_249.bin`; no existing dump artifact is implied without command, timestamp, environment, trigger, and output.

Profiler marker:
- Pending Unity profiler proof. Static microsecond estimates only.

GC proof required:
- Profiler/GCMonitor proof of 0 B/frame in Play Mode remains required.

Shutdown/disposal:
- Vault generation handles released from `BuoyancyDisplacementRuntime.ReleaseVaultHandles`.
- Job locks released through `UnlockJobBuffers`.

Scene unload behavior:
- Runtime disables, completes pending solver through existing dispatcher fence, releases descriptors, and clears cold boot state.

Stale-handle behavior:
- Boot/hot-swap phases may adopt/reacquire descriptors before gameplay work starts.
- `FixedTick` does not allocate, recover, or resize Vault descriptors; missing or stale handles fail closed for that tick.

Rejected alternatives:
- owner-local private NativeArrays
- managed C# events
- PhysX sleep APIs
- active/inactive row migration
- new custom one-off signal lane
- render-system direct mutation from physics job

Why this does not increase global monolith risk:
- It uses existing physics owner, fixed BufferIDs, existing `WakeRequestSignal`, and telemetry proof. It adds no GlobalRegistry slot and no HectonEventBus traffic.

H-Phi impact expected:
- Persistent native state remains Vault-owned. Hot broadcast uses typed SignalBus snapshot. No new managed global owner.

Proof required before GREEN:
- Unity import and Console clean.
- Burst Inspector compile for new jobs.
- Play Mode with 50,000 mock rows.
- Profiler/GCMonitor 0 B/frame.
- Fault dump generation test.
- Editor X-Ray smoke.

Reviewer: Integrator / CTO
Review disposition: PENDING
Status: PROPOSED / PENDING VERIFICATION
