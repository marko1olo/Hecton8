# SHINOBU_69 Volumetric Plasma Beam

Date: 2026-05-19
Status: Blocked by existing non-VFX `Hecton8.Core.csproj` compile errors. Unity/Burst import of the new PlasmaBeam files is still pending because the generated csproj has not picked them up yet.

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
## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM. These anchors prove only that the cited local paths exist at this capture time; they are not compile, Unity import, Play Mode, profiler, GC, player-build, save/load, platform, or visual proof.
- `Assets/_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs`
- `Assets/_Project/Scripts/VFX/PlasmaBeam/Editor/PlasmaBeamTunerWindow.cs`
- `Assets/_Project/Scripts/VFX/PlasmaBeam/Hecton8.VFX.PlasmaBeam.Runtime.asmdef`
- `Assets/_Project/Scripts/VFX/PlasmaBeam/Editor/Hecton8.VFX.PlasmaBeam.Editor.asmdef`

## 2026-05-20 DOC_GLOBAL R46 Root/Architecture Boundary Note

R51 root/architecture encoding/boundary/read-order/route-card/source-counter correction (`Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md`) keeps this file as a static architecture/source contract, not runtime proof. Current DOC_GLOBAL boundary is `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md`; R50 remains prior at `Docs/Reports/2026-05-21_DOCUMENTATION_R50_ROOT_ARCHITECTURE_ATLAS_REGEN_R48_INTERIOR_DUMPTARGET_AND_COUNTER_DRIFT_LOCAL.md`; R49 remains prior at `Docs/Reports/2026-05-21_DOCUMENTATION_R49_ROOT_ARCHITECTURE_ATLASCHECK_BOUNDARY_ROUTE_FIELDS_AND_COUNTER_DRIFT_LOCAL.md`; R48 remains prior at `Docs/Reports/2026-05-21_DOCUMENTATION_R48_ROOT_ARCHITECTURE_DATE_ROLLOVER_ATLASCHECK_AND_COUNTER_REFRESH_LOCAL.md`; R47 remains prior at `Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md`; R46/R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current static gates: `Tools/AtlasCheck.py` remains red on `ATLAS_CHECK_FAIL references=6881 missing=60` (one Dynamic Decals vendor asset ref, RealtimeCSG vendor icon/readme image refs, and missing HectonMaskChannelPacker and HectonMaterialChannelPackValidator source refs in the current atlas); `Docs/Modding/Validate_Mod_API_Static.ps1` passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only. Runtime proof remains absent.

Runtime owner: `Hecton8.VFX.PlasmaBeam.ShinobuPlasmaBeamRuntime`.

Assembly isolation:
- Runtime: `Assets/_Project/Scripts/VFX/PlasmaBeam/Hecton8.VFX.PlasmaBeam.Runtime.asmdef`
- Editor: `Assets/_Project/Scripts/VFX/PlasmaBeam/Editor/Hecton8.VFX.PlasmaBeam.Editor.asmdef`
- Direct sibling runtime assembly references: none. Runtime routes through Core, Core.Contracts, Core.Memory, and typed `SignalBus<T>`.

Rendering path: vault-owned `BeamVertexDTO` triangle-list tube -> `GraphicsBuffer.LockBufferForWrite` -> `Graphics.DrawProceduralIndirect` -> `Hecton8/VFX/PlasmaBeamIndirect`.

Memory ownership:
- `ShinobuPlasmaBeamStates`
- `ShinobuPlasmaBeamVertices`
- `ShinobuPlasmaBeamTrigLut`
- `ShinobuPlasmaBeamRuntimeScalars`
- `ShinobuPlasmaBeamIndirectArgs`
- `ShinobuPlasmaBeamTelemetryRing`
- `ShinobuPlasmaBeamMockSignals`
- `ShinobuPlasmaBeamAcousticTaps`
- `ShinobuPlasmaBeamCsvScratch`

Core constraints:
- No `LineRenderer`, `TrailRenderer`, runtime mesh rebuild, or ParticleSystem beam core.
- `BeamVertexDTO` is explicit 32 bytes: `float3 Position`, `uint ColorPacked`, `float2 UV`, `ulong _pad0`.
- Geometry is AUP-local before float trigonometry.
- `GlobalQualityWeight` controls length segments, radial segments, noise amplitude, and shader intensity. Below 0.3, length density is hard-gated by `math.step` to the 2-segment survival path and Simplex noise is not evaluated.
- `PlasmaBeamRuntimeScalarsDTO` is 64 bytes and carries `SectorHash` for deterministic mock/rollback seeding.
- `VisualSyncTick` is an allocation firewall: boot may allocate GPU buffers/material, but VisualSync only draws if resources are already resident.
- Shader flow uses `_H8PlasmaFrameTime` from dispatcher frame/fixed tick, not Unity `_Time`.
- Vault handles and DTO layout validation are cold-path cached after initialization; steady dispatcher phases use generation-checked handle `Resolve` instead of repeating `GetBufferHandle`.
- Editor tuning and SceneView mesh snapshot APIs refuse vault access while `_simulationScheduled` is true; pending designer edits are staged and applied at the next pre-simulation boundary.
- CSV hot reload is editor-only; player and development gameplay builds do not poll the filesystem from pre-simulation.
- Blackbox dump IO is fail-closed: filesystem failures set `FlagDumpFailed` and do not throw from post-simulation fault handling.
- Standard tools use the Dear Lie: a UV-scrolled procedural tube, not true plasma simulation.

Fault path: non-finite beam math writes a 300-frame telemetry ring dump to `Docs/AgentLogs/Dump_LASER_SURGEON.bin`.

Date: 2026-05-19
Status: Blocked by existing non-VFX `Hecton8.Core.csproj` compile errors. Unity/Burst import of the new PlasmaBeam files is still pending because the generated csproj has not picked them up yet.
