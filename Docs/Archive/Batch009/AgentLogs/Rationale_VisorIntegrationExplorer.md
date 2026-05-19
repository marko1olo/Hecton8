# Rationale_VisorIntegrationExplorer

Status: COMPLETE - STATIC SOURCE AUDIT ONLY

Problem: Need determine how a diegetic visor lens simulation should publish shader scalars and fit existing URP render-feature patterns without modifying source.
Solution: Read-only audit of allowed visor/rendering/shader-global paths against RenderGraph, stencil, zero-GC, GlobalRegistry/DI, shader aesthetic, and ARM64 layout mandates.
Rejected Alternatives: Adding a new concrete manager, MaterialPropertyBlock path, particle droplet path, Canvas Image path, or compatibility-mode render pass would violate current HECTON laws.
Scalability potential: Low uses scalar shader fakes and stencil rejection; Middle adds controlled post/lens detail; High expands shader math; Ultra can buy richer lens response only through continuous GlobalQualityWeight and measured RenderGraph passes.
Hardware Impact: Runtime impact is 0 us because this pass is audit-only. Future lens scalar publishing must target zero allocation and a sub-0.1 ms pass budget on i3/MX350.

Problem: Select scalar publication route for a new diegetic visor lens simulation without inventing direct dependencies.
Solution: Reuse existing constant-buffer RenderGraph feature pattern for pass-local visor lens scalars, and use the existing HectonShaderGlobalDataVaultBridge/GlobalShaderDispatcher path only for project-wide shader state.
Rejected Alternatives: MaterialPropertyBlock on visor geometry, Canvas RawImage overlay, Camera.AddCommandBuffer, per-frame material property spray, and binary low/high quality keywords. These paths either break SRP batching, bypass RenderGraph, add UI composition debt, or violate continuous GlobalQualityWeight requirements.
Scalability potential: Low uses scalar fakes, stencil rejection, and cheap chroma/dither. Middle adds controlled wetness/distortion lanes. High increases refraction/noise response. Ultra spends saved budget on richer lens response only through continuous weight, not feature forks.
Hardware Impact: Runtime impact remains 0 us for this audit. Expected implementation target is one constant-buffer upload plus one fullscreen RenderGraph pass under 0.1 ms; no measured savings claimed until implemented.
