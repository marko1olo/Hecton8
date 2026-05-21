# Rationale_SHINOBU_275_AUDIT_SHADER_RG

## Decision 1: Treat RenderGraph external buffer/depth as declared resources
Problem: DeferredDecalPass consumes _GlobalVisorWounds and scene depth, but RecordRenderGraph uses AddBlitPass with material mutation outside pass execution and only declares the depth texture.
Solution: Recommend replacing AddBlitPass with a raster pass that imports the GraphicsBuffer, declares UseBuffer(Read), declares depth/color reads, sets _CameraDepthTexture and _GlobalVisorWounds inside SetRenderFunc, then draws fullscreen.
Rejected Alternatives: Standard Unity material.SetBuffer before AddBlitPass is fast to write but hides the buffer from RenderGraph and can stale under material reuse or graph reordering.
Scalability potential: Low keeps 8 active wounds through Vault quality scaling; Middle/High/Ultra can raise active count while preserving a declared graph route.
Hardware Impact: MX350 gain is correctness and predictable scheduling, not measured frame savings. Estimated CPU submission stability gain: 10-30 us under multi-camera or graph pressure.

## Decision 2: Renderer serialized shader reference is authoritative
Problem: The prompt names HectonVisorUberPost.shader for torn-edge integration, but PC renderer assets serialize the Hecton_VisorGlitchACES shader GUID and the feature defaults deepSeaNoirUnifiedPass to true.
Solution: Recommend either enabling the non-unified HectonVisorUberPost shader path explicitly in renderer assets or porting the torn-edge integration into the active Hecton_VisorGlitchACES/noir pass.
Rejected Alternatives: Trusting the feature default path is invalid because serialized renderer assets are the player route, and editor-only TryAssignNoirShaderEditor intentionally points unified mode at Hecton_VisorGlitchACES.
Scalability potential: Low/Middle keep the one-pass noir route if torn edges are ported; High/Ultra can afford separate UberPost only if profiler/RenderGraph proof accepts the extra fullscreen pass.
Hardware Impact: Avoids shipping inactive shader work. Estimated saved integration waste: one unused shader implementation and one false visual route.

## Decision 3: Keep ABI finding evidence static only
Problem: VisorDecalDTO layout must match HLSL StructuredBuffer stride 80B.
Solution: Verified C# explicit size 80, LocalToWorld offset 0, DecalTypeHash 64, Opacity01 68, BirthTime 72, Flags 76; HLSL uses four float4 columns plus uint/float/float/uint.
Rejected Alternatives: Running a build for ABI proof; sub-agent was audit-only and dotnet/Unity build was not required.
Scalability potential: Stable DTO supports Low/Middle/High/Ultra without changing layout; quality scales count, not ABI.
Hardware Impact: Aligned 80B stride is ARM64-safe by mandate; no runtime gain claimed without profiler.

## Decision 4: Stale duplicate shader is a route-risk, not active binding proof
Problem: Hecton_DeferredDecal.shader still declares Hidden/Hecton8/DeferredDecal while renderer assets point Hecton_VisorWounds.
Solution: Recommend deleting stale shader + meta or renaming it to an explicit deprecated/error shader if a compatibility window is required.
Rejected Alternatives: Leaving the stale hidden shader because current renderer assets do not reference it; future Shader.Find/material assignment can revive it silently.
Scalability potential: One shader authority reduces variant and QA surface across all tiers.
Hardware Impact: Small build/variant hygiene gain; runtime frame cost only if stale shader is accidentally bound.
