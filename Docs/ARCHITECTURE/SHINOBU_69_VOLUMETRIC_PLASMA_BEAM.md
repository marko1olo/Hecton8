# SHINOBU_69 Volumetric Plasma Beam

Date: 2026-05-19
Status: Blocked by existing non-VFX `Hecton8.Core.csproj` compile errors. Unity/Burst import of the new PlasmaBeam files is still pending because the generated csproj has not picked them up yet.

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

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
- Standard tools use the Dear Lie: a UV-scrolled procedural tube, not true plasma simulation.

Fault path: non-finite beam math writes a 300-frame telemetry ring dump to `Docs/AgentLogs/Dump_LASER_SURGEON.bin`.
