# LOG_SHINOBU_275_AUDIT_SHADER_RG

## 2026-05-21 Static Audit

What was wrong:
- DeferredDecalPass records a fullscreen blit while the wound GraphicsBuffer and _CameraDepthTexture binding are not pass-local RenderGraph resources.
- PC_Renderer and PC_High_Renderer serialize HectonVisorUberPostFeature.shader to Hecton_VisorGlitchACES, while the prompt's torn-edge integration file is HectonVisorUberPost.shader.
- Hecton_DeferredDecal.shader remains as a stale duplicate hidden shader name.
- The wound pass event is AfterRenderingOpaques, which composites visor damage before transparents and before the later post stack.

What was done:
- Static source audit only. Read named shader files, DeferredDecalPass.cs, renderer assets, relevant .meta GUIDs, DynamicDecalVaultRuntime layout/quality paths, and task-relevant mandates.
- Verified VisorDecalDTO ABI matches 80B against HLSL field order.
- Verified renderer wound feature points to Hecton_VisorWounds shader GUID 0a2df57d7a4e4d44a95b1b4c4bfb2750.
- Verified no UsePass in the three named shader files; UsePass exists elsewhere in Hecton_DryZoneLit.shader and is outside this audit target.

Cinematic Cheats used:
- No simulation proposed. Findings preserve the screen-space/postprocess fake route.
- Quality route remains continuous through GlobalQualityWeight-driven active decal count.

Exact Microseconds saved:
- No measured microseconds. Static estimate only:
  - Declared RenderGraph buffer/depth route avoids 10-30 us CPU submission instability under multi-camera/material reuse scenarios.
  - Removing stale shader avoids future variant/asset lookup waste, unmeasured.
  - Moving visor wound composite after transparents does not save time; it fixes visual ordering.

Verification:
- No Unity import, shader compile, RenderGraph Viewer, Frame Debugger, profiler, GCMonitor, player build, or dotnet build was run.
Status: PENDING VERIFICATION.
