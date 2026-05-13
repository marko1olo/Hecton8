# QUEST_VULKAN_RENDER_PIPELINE Status

Agent: QUEST_VULKAN_RENDER_PIPELINE  
Domain: GRAPHICS_PROGRAMMER / VR Somatic Comfort / URP Rendering  
Task Count: 15  
Status: PENDING VERIFICATION

## Prompt Source

- Direct chat dispatch received 2026-05-13.
- `Docs/Tasks/CURRENT_BATCH.md` was scanned by CLI for `<AGENT_PROMPT id="QUEST_VULKAN_RENDER_PIPELINE">`; prompt was not present.
- Re-read cadence will use this status file plus the direct assignment summary below because no active batch XML block exists on disk.

## Relevant Mandates Read

- `REND_Foveated_Simulation_LOD.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `REND_DescriptorBinding_Reality_Check.txt`
- `REND_VR_Stencil_Masking.txt`
- `GPU_Compute_Warp_Sizing_Mobile.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## Direct Assignment Summary

1. Create `URP_Quest_VR.asset`; disable Depth Texture and Opaque Texture.
2. Force Single Pass Instanced or Multiview for VR without damaging PC/other platforms.
3. Configure Unity 6 RenderGraph/tile-memory-friendly behavior where project APIs allow.
4. Add `OculusFfrEnforcer.cs` to enable Fixed Foveated Rendering High/HighTop via OpenXR/Oculus API.
5. Validate `HectonVisorUberPost` depth fog under SPI/stereo matrices.
6. Query `SystemInfo.systemMemorySize`; treat `< 8000` MB with Quest guards only when XR/Android evidence exists.
7. Apply Quest-only runtime mip bias via `QualitySettings.masterTextureLimit = 1`.
8. Add Android shader preprocessor stripping HDR, soft shadows, directional lightmap variants.
9. Enable 4x MSAA on the Quest URP asset; avoid FXAA for VR.
10. Replace flood waterline depth dependency with mathematical frustum/Y-plane fake, no `_CameraDepthTexture` on Android.
11. Maintain zero runtime GC.
12. Push VR eye texture resolution and FFR level to Blackbox/telemetry if an existing interface is present.
13. Scan for `Camera.targetTexture`; flag manual RT allocations > 1024x1024 on Quest.
14. Audit `HLOD_INSTANCE_CULLING` compute shaders for Vulkan-safe syntax.
15. Verify shader compilation for Android/Vulkan target.

## State Machine

- [ ] Task 1: URP Quest asset mutation | DOD pending | Alternative rejected pending | Estimate pending
- [ ] Task 2: Single Pass Instanced / Multiview | DOD pending | Alternative rejected pending | Estimate pending
- [ ] Task 3: RenderGraph/TBDR configuration | DOD pending | Alternative rejected pending | Estimate pending
- [ ] Task 4: FFR enforcer | DOD pending | Alternative rejected pending | Estimate pending
- [ ] Task 5: Noir fog stereo sync | DOD pending | Alternative rejected pending | Estimate pending
- [ ] Task 6: Unified memory gate | DOD pending | Alternative rejected pending | Estimate pending
- [ ] Task 7: Quest mipmap bias | DOD pending | Alternative rejected pending | Estimate pending
- [ ] Task 8: Shader stripping | DOD pending | Alternative rejected pending | Estimate pending
- [ ] Task 9: MSAA hardware | DOD pending | Alternative rejected pending | Estimate pending
- [ ] Task 10: Depth resolve fake | DOD pending | Alternative rejected pending | Estimate pending
- [ ] Task 11: Zero-GC audit | DOD pending | Alternative rejected pending | Estimate pending
- [ ] Task 12: Telemetry | DOD pending | Alternative rejected pending | Estimate pending
- [ ] Task 13: RenderTexture reconnaissance | DOD pending | Alternative rejected pending | Estimate pending
- [ ] Task 14: Vulkan HLOD compute audit | DOD pending | Alternative rejected pending | Estimate pending
- [ ] Task 15: Android/Vulkan shader compile check | DOD pending | Alternative rejected pending | Estimate pending

## Loop Log

- Loop 0: Initialized state. No code changed yet. Status remains PENDING VERIFICATION.
