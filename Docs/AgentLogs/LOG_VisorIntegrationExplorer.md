# LOG_VisorIntegrationExplorer

## 2026-05-18 - Static Visor Integration Audit

What was wrong:
- No new source problem was fixed; the request was read-only exploration.
- Integration risk exists because the visor codebase contains multiple scalar publication styles: constant-buffer RenderGraph passes, DataVault shader global bridge, legacy camera command buffers, MaterialPropertyBlock visor mesh updates, and Canvas RawImage overlay code.

What was done:
- Inspected allowed visor/rendering/shader-global paths for a future diegetic visor lens simulation.
- Identified RenderGraph constant-buffer visor passes as the reusable pattern.
- Identified DataVault/global dispatcher as the correct project-wide bridge when state must be visible beyond one render pass.
- Flagged compile and architecture risks: CBUFFER layout mismatch, unsupported SetGlobalConstantBuffer platforms, binary quality keywords, material mutation inside render functions, MPB/SRP batching loss, Canvas Image overlays, and legacy camera command buffers.

Cinematic Cheats used:
- None implemented. Recommended path is shader scalar fakery, stencil rejection, procedural lens/noise masks, and continuous GlobalQualityWeight scaling before any physical simulation.

Exact Microseconds saved:
- 0 us measured, 0 us runtime change. This audit changed no runtime code.
