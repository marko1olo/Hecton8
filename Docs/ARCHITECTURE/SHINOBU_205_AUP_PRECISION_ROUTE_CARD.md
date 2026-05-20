# SHINOBU_205 AUP Precision Route Card

Date: 2026-05-20
Owner: SHINOBU_205 / AUP_PRECISION_INSPECTOR
Domain: Core determinism, AUP localization, floating-origin precision
Status: STATIC SOURCE UPDATED / RUNTIME PROOF PENDING

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R47 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- Docs/README.md
- Docs/DOC_GOVERNANCE.md
- Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md
- current source files
- fresh verification logs and artifacts

Current root/architecture boundary is `Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md` as STATIC_DOC/STATIC_SOURCE/FILESYSTEM/PY_TOOL evidence. R46 remains the prior interior-authority/route-field/proof-language correction; R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; runtime proof remains absent.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-20 R47): `Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md` is the latest local static root/architecture authority-spine, runtime-wording, and counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->


## Authority

AUP is the only simulation-scale spatial truth. `Transform.position` is presentation only and is not accepted as a coordinate source for simulation, culling authority, physics integration, rollback hashes, or persistence.

Approved conversion order:

1. Read target and observer in `double3` AUP.
2. Compute `double3 localDelta = targetAup - observerAup`.
3. Downcast only that local delta to `float3`.
4. Run trigonometry, rendering, physics proxies, and presentation in local float space.

## Vault Buffers

Owner system: `SystemID.CoreDeterminism`

| Buffer | Type | Length | Initialization | Purpose |
|---:|---|---:|---|---|
| `73200` | `double3` | capacity | uninitialized | target AUP samples |
| `73201` | `AupPrecisionRuntimeStateDTO` | 1 | clear | observer, quality, cursor, gate |
| `73202` | `float3` | capacity | uninitialized | localized offsets |
| `73203` | `uint` | capacity | uninitialized | result flags |
| `73204` | `AupPrecisionTelemetryEntry` | 300 | clear | black-box ring |
| `73205` | `AupToleranceProfileDTO` | 64 | clear | cold CSV tolerance rows |
| `73206` | `byte` | 16384 | uninitialized | cold CSV scratch |
| `73207` | `double3` | capacity | uninitialized | +/-100 km mock edge samples |
| `73208` | `AupPrecisionFaultCounter64` | 1 | clear | cache-line fault counter |

The route uses `VaultGenerationHandle<T>` and resolves transient `NativeArray<T>` views only at boot, editor facade, parser, dump, or job scheduling boundaries. It stores no private persistent `NativeArray<T>` ownership.

## DTO Layout

`AupPrecisionTelemetryEntry`: explicit 64 bytes. Offsets: `MaxLocalDistanceMeters=0`, `MaxLocalDistanceSq=8`, `Frame=16`, `ActiveCount=20`, `SkippedCount=24`, `NonFiniteCount=28`, `SafeNormalizeFallbackCount=32`, `GlobalQualityWeight=36`, `KernelMicrosecondsEstimate=40`, `GateDistanceMeters=44`, `Flags=48`, `SectorHash=52`, `PositionHash=56`.

`AupPrecisionRuntimeStateDTO`: explicit 64 bytes. Offsets: `ObserverAup=0` (24 bytes), `Frame=24`, `ActiveCount=28`, `TelemetryCursor=32`, `GlobalQualityWeight=36`, `GateDistanceMeters=40`, `MaxLocalCastMeters=44`, `LastKernelMicroseconds=48`, `Flags=52`, `_pad0=56`.

`AupPrecisionFaultCounter64`: explicit 64 bytes. Offsets: `NonFiniteCount=0`, `ClampedCount=4`, `SkippedCount=8`, `SafeNormalizeFallbackCount=12`, `MaxErrorMeters=16`, `Flags=20`, `PositionHash=24`, padding `32..63`.

## Dependency Graph

Input dependency: caller-provided `JobHandle dependency`.

Scheduled graph:

`dependency -> LocalizeAupCoordinatesJob -> AupPrecisionTelemetryFoldJob -> returned JobHandle`

The route does not call `JobHandle.Complete()` in runtime scheduling. The editor X-Ray mock completes only in a cold UI Toolkit button path.

## Scalability

`GlobalQualityWeight` is continuous. At `0.0`, localization gate is 1000 m. At `1.0`, localization gate is 5000 m. Precision order does not degrade. Low quality sheds far-row work; high/ultra quality expands visible/localized coverage and telemetry density without changing authority math.

## Dear Lie

The runtime does not simulate floating-origin physics corrections per object. It localizes AUP samples through a batched Burst kernel and treats far rows as sentinel presentation misses. The editor X-Ray intentionally compares the visual lie `float(target)-float(observer)` against the approved double-subtract path to expose jitter without spawning debug GameObjects.


## R43 Review Disposition

| Field | Value |
|---|---|
| Route ID | `SHINOBU_205_AUP_PRECISION` |
| Owner | SHINOBU_205 / AUP_PRECISION_INSPECTOR |
| Instrument | JobHandle-returning AUP localization route, caller-owned coordinate/output buffers, telemetry/fault counters, and source-gate artifact output |
| Producer phase | job scheduling boundary that receives caller-owned `JobHandle dependency` |
| Consumer phase | returned job handle consumer plus telemetry fold after `LocalizeAupCoordinatesJob` |
| Cadence | caller-bounded batch; no runtime `JobHandle.Complete()` in scheduling path |
| Capacity | Caller-bounded coordinate batch, fixed telemetry ring, and Vault/local scratch lanes sized by the owning precision inspector |
| Overflow/failure | clamp or skip non-finite/out-of-gate rows, increment fault counters, keep safe sentinel output, and do not promote static scan results to runtime proof |
| Shutdown/disposal | Runtime path returns ownership through `JobHandle`; cold/editor mock work may complete locally, but runtime scheduling must leave teardown to the route owner/caller |
| Fault dump target | Any AUP precision dump is generated only by a linked gate/runtime fault trigger; no existing dump artifact is implied by this route card |
| Proof required before GREEN | Fresh compile/import artifact, Burst compile proof, Play Mode boundary swim, profiler/GCMonitor proof, ARM64 layout proof, and player-build output tuple |
| Review disposition | YELLOW / STATIC_SOURCE_ONLY until compile, Unity import, Burst compile, Play Mode, profiler, GCMonitor, ARM64 layout, and player-build artifacts exist |
## Verification Boundary
Static scan: direct AUP/double3 `(float3)` cast scan currently returns zero hits under `Assets/_Project/Scripts`.

Static scan: explicit runtime component casts such as `new float3((float)SomeAUP.x, ...)` currently return zero runtime hits. Five editor-only visual/debug casts remain review findings because they are presentation inspectors, not simulation authority.

Static scan: strict `Transform.position` authority reads currently report 79 runtime blockers. SHINOBU_205 removed provable player/camera observer fallbacks, but remaining findings are not silently rewritten because each requires the owning domain's AUP route or DataVault source. The scanner and `Docs/Reports/AUP_PRECISION_SCAN_SHINOBU_205.json` record them as build-blocking owner handoff debt.

Editorless CI gate: `Tools/AupPrecisionGate_SHINOBU_205.py` scans the same source surface without Unity, writes `Docs/Reports/AUP_PRECISION_SCAN_SHINOBU_205.json`, and upserts `Docs/Reports/MATH_OPTIMIZATION_REPORT.json`. Last recorded CLI result in this document is `FAIL_STATIC_GATE`: 1986 files scanned, direct AUP float3 casts 0, runtime component AUP float casts 0, editor presentation reviews 5, strict `Transform.position` authority blockers 79 across 55 files; rerun before using that file count as current.

CLI gate fixture: `Tools/TestAupPrecisionGate_SHINOBU_205.py` is reported as a `STATIC_SOURCE/PY_TOOL` fixture pass for direct-cast, component-cast, editor-review, transform-authority, approved-helper, and self-diagnostic exclusion semantics; proof requires artifact path, command, timestamp, environment, and output.

Pending: Unity import, Burst compile, Play Mode boundary swim, profiler/GC capture, ARM64 device layout proof. Build launch remains blocked by project CPU guard when CPU is above 50% or dotnet/csc is active.
