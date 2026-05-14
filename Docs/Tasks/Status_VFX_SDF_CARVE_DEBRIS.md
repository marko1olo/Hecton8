# Status - VFX_SDF_CARVE_DEBRIS

Agent: VFX_TECHNICAL_ARTIST
Domain: ECHELON 7 #66 Marine Snow/Silt Compute VFX with ECHELON 2 SDF/Carve/Flow integration
Prompt: `Docs/Tasks/CURRENT_BATCH.md` / `<AGENT_PROMPT id="VFX_SDF_CARVE_DEBRIS">`
Status: PENDING VERIFICATION

## Mandates Read Before Coding

- `REND_VFX_Fluid_Aesthetics_Compute_Particles.txt`
- `GPU_Compute_Kernels_Kernels_Optimization_MX350.txt`
- `GPU_Compute_Warp_Sizing_Mobile.txt`
- `REND_GPU_Sovereignty.txt`
- `VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt`
- `VOX_Voxel_World_Logic_Carving_Persistence.txt`
- `CORE_Weather_Abyssal_FlowField_Currents.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

## Core Tasks

- [x] 1. Singleton eradication: N/A. DOD: no singleton added; runtime registers through `GlobalRegistry.TryRegisterUpdatable`. Rejected alternative: static active renderer owner. Estimate: 0 us steady-state dependency cost.
- [x] 2. Signal migration: `VoxelCarveEvent` now implements `ISignal` and is pushed through `SignalBus<VoxelCarveEvent>` after queue validation. Rejected alternative: consuming legacy `DebrisSpawnSignal` only, because prompt requires carve-radius ingress. Estimate: 4-8 us per carve push.
- [x] 3. ASMDEF isolation: added `Hecton8.VFX.Debris` with `Hecton8.Core.Contracts` reference plus Core/Core.Memory for registry, signals, and DataVault. Rejected alternative: dumping the renderer into root Core assembly. Estimate: 0 us runtime.
- [x] 4. Debris buffer S.O.A.: compute file now has `StructuredBuffer<float4>`/`RWStructuredBuffer<float4>` position-lifetime and velocity lanes, capacity fixed at 4096. Rejected alternative: AoS debris struct with flags. Estimate: 16-byte coalesced reads, ~10-20 us saved per full dispatch on MX350 versus AoS.
- [x] 5. C# injection job: Burst `CarveDebrisInjectJob` scans `w <= 0` slots and injects 64 high-tier / 16 low-tier particles. Rejected alternative: managed list of free slots, because it creates sync complexity and mutation churn. Estimate: 15-35 us per 4096-slot burst scan on i3.
- [x] 6. Random jitter: `CarveDebrisInjectJob` uses `Unity.Mathematics.Random` seeded from frame + volume id + absolute carve hit + radius. Rejected alternative: `UnityEngine.Random`, non-deterministic and managed. Estimate: <3 us per 64 samples.
- [x] 7. GPU advection: `AdvectCarveDebris` in `Hecton_FluidAdvection.compute` applies gravity, flow drag, and dynamic wake flow path. Rejected alternative: CPU integration. Estimate: 20-45 us for 4096 slots on MX350.
- [x] 8. SDF collision: compute samples `VoxelSdfTexture3D` via existing SDF uniforms; on density hit velocity zeroes and life decays 6x. Low tier bypasses SDF. Rejected alternative: physics colliders/raycast. Estimate: Low saves one 3D texture sample per live particle.
- [x] 9. BRG/indirect render: `Graphics.RenderMeshIndirect` renders persistent octahedron rock mesh with `Hecton_CarveDebrisIndirect.shader` including `Hecton_CoreLit.hlsl`. Rejected alternative: GameObject mesh instances. Estimate: 150-400 us saved on burst frames versus transform-spawned chips.
- [x] 10. AUP shift safety: renderer drains `SignalBus<AupShiftSignal>`, accumulates negative shift, and applies it inside compute before integration. Rejected alternative: CPU full-buffer rebase upload. Estimate: saves 4096 CPU writes after origin shifts.
- [x] 11. H-PHI sovereignty: renderer requests `BufferID.CarveDebris` and `BufferID.CarveDebrisVelocity` from `GlobalRegistry.DataVault`. Rejected alternative: private native ownership. Estimate: 0 us steady-state after cold resolve.
- [x] 12. Math LOD: Low tier/MX350 injects 16 particles and passes SDF inactive to compute. Rejected alternative: same 64-particle/SDF path on every device. Estimate: saves 48 random writes per carve plus one SDF sample per live particle.
- [x] 13. Zero-GC: persistent NativeArrays/GraphicsBuffers; no `GetData`/`SetData`; only cold fallback mesh/material and crash dump allocate. Rejected alternative: managed emitter/list. Estimate: 0 B/frame hot path.
- [x] 14. Blackbox dump: 300-entry `NativeArray<CarveDebrisTelemetryEntry>` records `ActiveCarveDebrisCount`; invalid state dumps `Dump_VFX_SDF_CARVE_DEBRIS.bin`. Rejected alternative: log-only diagnostics. Estimate: 1 ring write per frame.
- [x] 15. Omega compile check: indirect args logic statically verified: clear kernel writes 5 indexed args, cull kernel atomically increments instance count, render uses `Graphics.RenderMeshIndirect`. Unity 6000.4.1f1 batchmode compile passed on second attempt after restoring `Hecton8.World` for AUP conversion. Rejected alternative: CPU `GetData` counter verification. Estimate: GPU-only visible count avoids readback stalls.

## Loop Log

- Loop 0: Prompt extracted, status missing, rationale missing. Fresh files created. Code not touched yet.
- Loop 1: Tasks 1-5 implemented. Prompt re-read from `CURRENT_BATCH.md` lines 997-1036 before continuing.
- Loop 2: Tasks 6-10 implemented. Unity MCP compile check attempted; editor session unavailable, static pass continuing.
- Loop 3: Tasks 11-15 implemented/blocked for Unity compile tooling. Static checks found no `GetData`/`SetData` in the hot lane.
- Loop 4: OMEGA mandate extracted only after all core tasks were checked/blocked. Float lifetime divisions replaced with reciprocal multiplies; dispatch-group math moved to setup/auditable helper. DOD: targeted static scan found no `GetData`, `SetData`, `foreach`, interpolated strings, `math.sqrt`, `math.normalize`, `dt /`, or `1f /` in touched VFX files. Rejected alternative: CPU readback validation. Estimate: avoids millisecond-scale readback stalls; reciprocal change is sub-microsecond but removes repeated scalar divisions.
- Loop 5: Final strict pass re-read renderer, compute shader, material shader, signal bridge, DataVault IDs, and asmdef references. Unity MCP validation still fails at `http://127.0.0.1:8088/mcp`; `Temp/UnityLockfile` is present with active Unity processes, so batchmode compile remains blocked. DOD: `git diff --check` reports only line-ending normalization warning on `CarveDebrisComputeRenderer.cs`.
- Loop 6: Patient second-pass upgrade re-read status, rationale, prompt excerpt, renderer, compute shader, asmdef, `HectonFluidEngine` flow contract, marine snow flow binding precedent, and cave SDF publication contract. DOD: low tier now dispatches/ages/culls only 1024 active slots, high tier remains 4096; false flow activation from empty fallback buffers is removed; published `HectonFluidEngine` buffer/texture payloads are bound when available; dynamic wake buffers are defensively bound with zero active slots; fallback mesh/material creation is cold-started in `Awake`/`OnEnable`; GPU velocity is clamped to 3.5 m/s and 0.20 m/frame. Rejected alternatives: same 4096 scan on low tier, CPU readback verification, direct access to internal `HectonCaveVoxelLightingVolume` from the isolated asmdef, and ParticleSystem fallback. Estimate: low-tier dispatch groups drop from 64 to 16, saving about 25-35 us GPU on MX350; idle CPU mirror aging skip saves about 10-25 us when no debris is alive; velocity clamp prevents SDF tunneling without substeps.
- Loop 7: Verification retry and failure classification. DOD: static scan still finds no `GetData`, `SetData`, `ParticleSystem`, `ComputeBuffer`, `foreach`, `.ToString`, `string.Format`, or interpolated strings in the touched VFX lane; shader scan shows reciprocal/`rsqrt` math and no new hot `sqrt`/`pow`/`exp`/`log` path. Unity MCP remains unavailable; no `Hecton8.VFX.Debris.csproj` has been generated; `dotnet build Hecton8.Core.csproj --no-restore` fails on unrelated symbols outside this VFX asmdef. Rejected alternative: reporting the unrelated root csproj failure as a VFX compile failure. Estimate: verification blocked by tooling/project integration state, not by observed carve debris errors.
- Loop 8: Real Unity batchmode verification. DOD: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` passes; first Unity batchmode run found `CS0103 AbsoluteUniversePosition` in `CarveDebrisComputeRenderer.cs`; fix restored `using Hecton8.World`; second Unity 6000.4.1f1 batchmode run exits with return code 0 and no `error CS` entries. Current `Docs/Tasks/CURRENT_BATCH.md` no longer contains this agent tag, so prompt re-extraction from active batch is unavailable; assignment remains preserved in this status/rationale trail. Rejected alternatives: leaving compile blocked after tooling recovered, deleting Bee/Library cache, or killing other agents' MSBuild worker nodes. Estimate: compile fix has 0 us runtime cost.

## Second-Pass Upgrade Status

- [x] Low/MX350 path uses an active 1024-slot cap for mirror aging, injection, compute dispatch, GPU cull, and indirect max instances while preserving 4096 storage for high/ultra.
- [x] Flow binding is tied to real `HectonFluidEngine` GPU publication or an authored override; the one-element fallback buffer no longer marks flow active.
- [x] Dynamic wake buffers are explicitly bound even when no wake publisher is present; `_DynamicWakeParams.x = 0` prevents out-of-range fallback reads.
- [x] Cold fallback mesh/material creation moved out of first active draw where possible.
- [x] Compute velocity clamp added to cap chip travel per frame and reduce SDF miss-through without adding substeps.
- [x] `AgeCarveDebrisMirrorJob` preserves existing blackbox flags instead of wiping invalid-state bits during an otherwise normal age pass.
- [x] Root build failure classified as unrelated: `Hecton8.Core.csproj` does not include `Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs`, and its errors are missing symbols in UI/fauna/world/core systems.
- [x] Unity batchmode compile recovered and passed after the AUP namespace import fix; MCP port 8088 still reports shutdown noise only.

## OMEGA Polish Status

- [x] Prompt-specific polish mandate parsed after core completion/block.
- [x] Touched VFX code scanned for hot managed bloat patterns.
- [x] No CPU GPU readback path introduced.
- [x] Final Unity batchmode compile passed locally; status remains `PENDING VERIFICATION` until runtime visual/profiler capture exists.
