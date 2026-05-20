# SHINOBU_48 Seed Ship Anomaly

Date: 2026-05-18
Status: STATIC_SOURCE ORIENTATION / COMPILE CLAIM REQUIRES ARTIFACT / RUNTIME PROOF PENDING

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or anomaly runtime proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
R46 root/architecture interior-authority/route-field/proof-language correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md`) is the latest local static DOC_GLOBAL boundary for architecture/root documentation. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41 remains the prior global-authority/internal-residue correction; R40 remains the prior R38-residue/source-counter correction; R39 remains the prior authority-counter/proof-wording correction; R38/R37/R36/R35/R34 remain prior static correction layers. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## 2026-05-20 DOC_GLOBAL R46 Root/Architecture Boundary Note

R46 root/architecture interior-authority/route-field/proof-language correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md`) (R45 prior R43/R44 residue/proof-artifact/source-counter correction) keeps this file as static anomaly architecture orientation, not runtime anomaly, scene, profiler, or visual proof. Current DOC_GLOBAL boundary is `Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md`; R45 remains prior at `Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md`; R44 remains prior at `Docs/Reports/2026-05-20_DOCUMENTATION_R44_ROOT_ARCHITECTURE_INTERNAL_RESIDUE_EXACT_ROUTE_FIELDS_LOCAL.md`; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current static gates: `Tools/AtlasCheck.py` remains red on `ATLAS_CHECK_FAIL references=6741 missing=59` (one Dynamic Decals missing vendor asset ref, RealtimeCSG vendor icon/readme image refs, and missing HabitatDamageBakePipeline source ref in the current atlas); `Docs/Modding/Validate_Mod_API_Static.ps1` passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only. Runtime proof remains absent.

The Seed Ship anomaly is a Vault-owned scalar field, not a Unity trigger volume. `AnomalyFieldDTO` stores one AUP epicenter, radius, corruption scalar, and glitch hash. Runtime jobs compare player/mock predator AUP values against the epicenter by subtracting double precision AUPs before converting to `float3`.

Authoritative buffers:

- `BufferID.ShinobuSeedShipAnomalyField`: single `AnomalyFieldDTO`.
- `BufferID.ShinobuSeedShipAnomalyTuning`: single `AnomalyTuningDTO`.
- `BufferID.ShinobuSeedShipAnomalyGlobals`: scalar output consumed by other systems.
- `BufferID.ShinobuSeedShipAnomalyMockLeviathans`: bounded mock predator state for AI-domain proof.
- `BufferID.ShinobuSeedShipAnomalyTelemetryRing`: 300-frame black box.
- `BufferID.ShinobuSeedShipAnomalyIoScratch`: Vault-owned CSV/legacy binary read scratch buffer.
- `BufferID.ShinobuSeedShipAnomalyDumpScratch`: Vault-owned binary blackbox dump staging buffer.

Cross-domain output is decoupled through typed signals and scalar Vault rows: `RadarJamSignal`, `MockHudSignal`, `MockAupRebaseSignal`, `CoreHackedSignal`, `AnomalyProximitySignal`, and radiation signals. SHINOBU-specific cross-domain signal DTOs live in `Hecton8.Core.Contracts`, not the anomaly runtime assembly, so HUD/scanner/AI owners do not need a direct SeedShip runtime reference. No `TriggerCollider`, scene search, or boss-minion concrete dependency is part of the anomaly route.

`GlobalQualityWeight` is consumed continuously. Entity-specific frenzy budget scales with `quality^4` and current corruption; the designer minimum budget floor also smooth-collapses below mid quality so CSV tuning cannot pin low-end hardware to expensive row counts. Global shader/radiation/gravity scalars remain active even when entity budgets shrink.

Assembly isolation is explicit: runtime code is under `Hecton8.SeedShipAnomaly.Runtime`, referencing only `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory`, and Unity Burst/Collections/Jobs/Mathematics. It has no direct AI, HUD, rendering, physics sibling-domain asmdef reference. The editor tuner is isolated in `Hecton8.SeedShipAnomaly.Editor`.

Burst jobs use `FloatMode.Deterministic` because the anomaly writes rollback-visible global simulation truth. Wall-clock compute timing and budget breach flags are local blackbox telemetry only, not authoritative global flags. Visual overkill is intentionally pushed downstream through shader globals instead of fast-mode simulation drift.



