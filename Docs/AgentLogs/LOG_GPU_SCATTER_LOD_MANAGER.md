# LOG_GPU_SCATTER_LOD_MANAGER

## 2026-05-16 - 100k Flora Indirect Scatter

What was wrong:
- Procedural flora had no dedicated rendering-domain handoff for 100k OSHINO matrices through `Graphics.RenderMeshIndirect`.
- No production references existed for `FloraManager.Instance` or `Instantiate(KelpPrefab)`, but the regression gates were not documented.
- The renderer needed GPU-side visibility, AUP-safe culling, CopyCount indirect args, homeostasis shedding, and fixed blackbox evidence.

What was done:
- Added `Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs`.
- Added `Assets/_Project/Art/Shaders/GpuScatterLodCull.compute` with kernel `ScatterCullJob`.
- Added DataVault IDs `FloraScatterMatrices`, `FloraScatterMetadata`, and `FloraScatterMotionVectors` to `BufferID`.
- Implemented double-buffered matrix/metadata GPU uploads through `GraphicsBuffer.LockBufferForWrite`.
- Implemented append-visible indices and append-visible matrices.
- Implemented `GraphicsBuffer.CopyCount(_visibleMatrixBuffer, _argsBuffer, sizeof(uint))`.
- Implemented `Graphics.RenderMeshIndirect` submission.
- Implemented `SignalBus<CameraFrustumSignal>` consumption with signal-built fallback frustum planes.
- Implemented low 100m, mid 250m, high/ultra 500m cull tiers with 5m/2s hysteresis.
- Implemented homeostasis shed: `SystemStress01 > 0.8` halves desired cull distance.
- Implemented deterministic GPU sway motion-vector writes.
- Implemented finite matrix validation, zero-scale GPU/Burst rejection, and blackbox dump on non-finite matrix data.
- Implemented 300-frame `NativeArray<ScatterBlackBoxEntry>` with `VisibleFloraCount` from async indirect-args readback.
- Implemented `OnDisable`/`OnDestroy` release for GPU buffers, CPU audit buffers, blackbox, and Vault leases.

Cinematic cheats used:
- Replaced per-flora physical sway with deterministic hash/vector shader data.
- Replaced far-field flora truth on MX350 with 100m distance rejection.
- Kept high-tier visual overkill as longer 500m residency plus crossfade range, not more CPU simulation.

Exact microseconds saved:
- `FloraManager.Instance` removal: 0us measured; no production reference existed.
- `Instantiate(KelpPrefab)` deletion: 0us measured; no production reference existed.
- 100k GameObject/Transform submission avoided: estimated 900-1800us CPU on i3/MX350, PENDING PROFILER.
- AUP shift CPU matrix rebake avoided: estimated 150-400us per shift and 6.4MB upload avoided, PENDING PROFILER.
- CPU compacted visible matrix upload avoided: estimated 6.4MB/frame avoided at 100k capacity, PENDING PROFILER.
- CPU/GPU sync readback avoided for draw args: estimated 200-2000us stall avoided under queue pressure, PENDING PROFILER.

Verification:
- `rg "FloraManager\.Instance" Assets/_Project/Scripts Assets/_Project/Art`: no production matches.
- `rg "Instantiate\s*\(\s*KelpPrefab\s*\)" Assets/_Project/Scripts Assets/_Project/Art`: no production matches.
- `dotnet build Assembly-CSharp.csproj --no-restore`: BLOCKED by pre-existing `Hecton8.Core.csproj` missing dependency contracts.
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1`: BLOCKED by missing dependency DLLs from the same baseline compile wall.
- Filtered `dotnet build Hecton8.Core.csproj --no-restore -m:1` for `GpuScatter`/`FloraScatter`: no scatter-specific errors surfaced before the existing dependency wall.

Integrator note:
- Restore the baseline missing contracts/types first: examples include `ISimulationBucketer`, `IMacroDatabaseService`, `IPlayerMovementContracts`, `IPlayerMovementPoseReadModel`, `H8WorldPageReadTicket`, and related core contract symbols.
- Do not invent stubs in the rendering scatter domain. That would hide a cross-domain dependency failure.
