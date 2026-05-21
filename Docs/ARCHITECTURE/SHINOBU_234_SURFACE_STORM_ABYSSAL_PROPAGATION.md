# SHINOBU_234 Surface Storm Abyssal Propagation

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

Status: SUPERSEDED STATIC NOTE / PENDING VERIFICATION

Current route card:
`Docs/ARCHITECTURE/SURFACE_STORM_ABYSSAL_PROPAGATION_SHINOBU_234.md`

Supersession:
This older note is retained only to prevent stale cross-links from reviving the rejected direct-mutation route. The current implementation does not mutate `FogConstantsDTO`, `BiolumPulseStateDTO`, ocean swell DTOs, or audio DSP objects.

Current owner:
ECHELON 7 Atmosphere & Celestial / Weather & Wind Director.

Current route:
- Surface weather truth remains `BufferID.ShinobuOceanWeatherState` when present; SHINOBU does not create or mutate that upstream row.
- If the upstream weather row is absent or invalid and the emergency mock toggle is enabled, SHINOBU uses its own `MockHurricaneStateDTO` row to feed the same attenuation job for CI/dev stress scenes.
- Storm attenuation writes a hidden 96-byte `StormPropagationWriteSnapshotDTO` containing the 32-byte `StormPropagationDTO` plus four scalar `float4` snapshots.
- Stable read lane is `BufferID.ShinobuStormPropagationState = 71712`; hidden write lane is `BufferID.ShinobuStormPropagationWriteState = 71713`.
- Producer-only scalar lanes are `ShinobuStormPropagationFlowScalar = 71721`, `ShinobuStormPropagationAudioScalar = 71722`, `ShinobuStormPropagationBiolumScalar = 71723`, and `ShinobuStormPropagationFogScalar = 71724`.
- Public scalar lanes are not locked or written by the worker job. Late-frame publication locks the four scalar rows only for the all-or-nothing owner publication window, then copies the stable state and scalar rows after `DispatcherJobFence` finalization.
- Downstream fog, biolum, audio, and flow owners must consume those scalar lanes in their own owner phases; no downstream consumer is claimed in SHINOBU_234 proof.

Phase:
- Active route runs through `ShinobuStormPropagationRuntime`.
- The runtime is scene-local auto-installed after scene load. It does not use `DontDestroyOnLoad`.
- Scheduled work finalizes through `DispatcherJobFence`; no raw `JobHandle.Complete()` is present in the SHINOBU runtime route.

Proof Required:
- Unity import and Console compile.
- Play Mode validation of DataVault buffers and zero GC.
- Profiler sample proving propagation under 5 microseconds on the target scene.
- Downstream owner-phase consumers for the four scalar lanes.
