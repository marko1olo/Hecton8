# QUEST_VULKAN_RENDER_PIPELINE Rationale

Status: PENDING VERIFICATION  
Owner: GRAPHICS_PROGRAMMER

## Baseline Decisions

Problem: Quest TBDR needs bandwidth reduction, but HECTON-8 also targets PC and other platforms.  
Solution: Quest-specific assets, Android/OpenXR compile gates, and runtime guards that require XR/Android evidence before changing texture limits or FFR.  
Rejected Alternatives: Global URP mutation and unconditional runtime `QualitySettings.masterTextureLimit = 1`; both would damage PC/non-Quest builds and violate the user's explicit platform warning.  
Scalability potential: Low uses no depth/opaque copies, 4x MSAA, mip limit, and FFR High; Middle keeps same asset with less aggressive FFR; High/Ultra PC keeps richer post stack and native textures.  
Hardware Impact: Quest 2/3 bandwidth saved by avoiding depth/opaque texture resolves and reducing texture residency; i3/MX350 unaffected unless an explicit low-tier profile selects similar behavior.

Problem: Active batch file does not contain the agent XML prompt.  
Solution: Record the direct dispatch summary in `Status_QUEST_VULKAN_RENDER_PIPELINE.md` and use CLI scans when available; do not invent neighboring batch context.  
Rejected Alternatives: Reading archive batch prompts or applying unrelated agent prompts. That would violate strict parsing and domain boundary.  
Scalability potential: No runtime impact. Preserves task isolation with 20+ agents active.  
Hardware Impact: 0 microseconds runtime; documentation-only.

## Decision Journal

Problem: Shared URP assets would make Quest bandwidth fixes leak into PC and other platforms.  
Solution: Create `URP_Quest_VR.asset` and `Quest_VR_Renderer.asset` as separate assets; disable Depth Texture/Opaque Texture/HDR there and keep 4x MSAA.  
Rejected Alternatives: Editing the existing PC/mobile renderer assets or lowering global quality settings in editor. Both would break non-Quest profiles.  
Scalability potential: Low/Quest uses tile-resident color, no depth/opaque copies, 4x MSAA, FFR, mip clamp. Middle can keep asset and reduce FFR. High/Ultra PC keeps depth fog, shafts, richer post and full texture residency.  
Hardware Impact: Quest saves depth/opaque resolve bandwidth; i3/MX350 unaffected unless a low-tier profile explicitly opts into the asset.

Problem: Oculus/OpenXR packages are absent, so a direct Oculus API call would create a compile dependency that does not exist in this project.  
Solution: Use Unity core `XRDisplaySubsystem.foveatedRenderingLevel` and `SystemInfo.foveatedRenderingCaps`; gate by Android + Vulkan + XR + Quest memory/device evidence.  
Rejected Alternatives: Adding Oculus SDK/OpenXR package dependency during a graphics optimization pass, or using reflection into absent assemblies.  
Scalability potential: Quest gets hardware FFR now; PC and non-XR Android paths do nothing; future Meta package can replace the backend behind the same `OculusFfrEnforcer` surface.  
Hardware Impact: Quest peripheral fragment cost expected down by 0.9-1.8 ms depending eye texture resolution and scene fill.

Problem: The prompt requires `<8000` MB as Quest memory evidence, but Quest 3 can report near 8 GB and non-Quest Android devices also exist.  
Solution: Track strict `<8000` MB as `IsQuestMemoryGate`, add a Quest-family `<9000` MB/device-name gate, and require Android+Vulkan+XR before applying policy.  
Rejected Alternatives: Memory-only heuristic or unconditional Android mip clamp.  
Scalability potential: Cheap Quest follows Low path; higher devices still use Quest asset but can relax FFR/mip limits later; desktop remains High/Ultra.  
Hardware Impact: Low-end unified memory avoids 75% mipmapped texture residency on affected textures.

Problem: `HectonVisorUberPost` used depth-driven shafts/brine fog, which can force `_CameraDepthTexture` on TBDR.  
Solution: On mobile shaders, compile out `DeclareDepthTexture`, depth helpers, light shafts, and brine depth fog; compute internal flood waterline from camera ray vs absolute `_InternalWaterlineY` in shader with no depth texture.  
Rejected Alternatives: Sampling `_CameraDepthTexture`, keeping a depth prepass, or using a copied depth RT. All defeat TBDR tile locality.  
Scalability potential: Low/Quest uses the waterline lie and cheap lens wetness; PC keeps full depth fog/shafts; High/Ultra can spend saved Quest path design on richer non-depth visor effects.  
Hardware Impact: Quest avoids a depth resolve path and expected 0.3-0.8 ms bandwidth loss.

Problem: Shader variants for Android were carrying PC lighting affordances that inflate build/runtime memory.  
Solution: Extend `HectonShaderVariantStripper` to strip HDR, soft shadows, directional lightmap, dynamic lightmap, shadowmask/mixed-lighting variants only when active build target is Android.  
Rejected Alternatives: Removing features globally or stripping by shader name only.  
Scalability potential: Low Android keeps a small variant surface; PC/Ultra keeps high-end lighting variants.  
Hardware Impact: Lower shader warmup and memory pressure; no per-frame CPU/GPU tax.

Problem: Runtime telemetry must not allocate per frame, but FFR/eye resolution must be visible after failures.  
Solution: Use a persistent 300-entry `NativeArray<QuestFfrBlackboxEntry>` circular buffer and push sanitized FFR/eye texture state into `HectonXRRuntimeState`. Dump only on invalid telemetry.  
Rejected Alternatives: `Debug.Log`, strings, or managed lists in the runtime path.  
Scalability potential: Same blackbox shape can accept a future Meta-specific FFR backend.  
Hardware Impact: 0 B/frame target; diagnostic file IO only on failure.

Problem: Manual `Camera.targetTexture` and large `RenderTexture` allocations are hidden VRAM debt on unified-memory Quest.  
Solution: Recon report flags targetTexture/manual RT sites for integrator review rather than silently modifying unrelated systems.  
Rejected Alternatives: Blindly resizing UI/VFX/world render targets without owner context.  
Scalability potential: Low Quest can cap or pool these RTs; High/Ultra PC can keep full-res buffers.  
Hardware Impact: Each avoided 1024-2048 square ARGB RT saves roughly 4-16 MB plus bandwidth.

Problem: HLOD compute must not carry DirectX-only assumptions into Vulkan.  
Solution: Audit `InstanceCulling.compute` for compute pragma, structured append buffers, and absence of d3d-only syntax/wave intrinsics/globallycoherent byte address patterns.  
Rejected Alternatives: Deferring all compute validation to device runtime.  
Scalability potential: Vulkan-safe compute remains usable across Quest and PC Vulkan; further mobile tuning can focus on group sizes.  
Hardware Impact: Prevents hard runtime bind/compile failures rather than saving frame time directly.

Problem: Unity console exposed an already-dirty graphics file where duplicate hot-swap listener blocks left `HectonUnderwaterVisuals` structurally invalid.  
Solution: Remove the duplicate malformed blocks and keep the earlier complete listener implementation; do not revert the rest of the user/agent changes in that file.  
Rejected Alternatives: Reverting the dirty file or owning a broad underwater visual refactor.  
Scalability potential: Restores compile progression without changing renderer behavior.  
Hardware Impact: 0 runtime microseconds; build-blocker cleanup only.

Problem: Final Android/Vulkan shader compile is blocked by unrelated missing domains/types.  
Solution: Mark Task 15 blocked by dependency after running `dotnet build Hecton8.Core.csproj --no-restore`; record representative missing dependencies.  
Rejected Alternatives: Claiming verification from local static checks or editing unrelated AI/audio/persistence/domain contracts in this graphics pass.  
Scalability potential: Once dependencies resolve, Quest shader compile can be re-run without changing the render pipeline patch.  
Hardware Impact: No runtime impact; verification gate remains open.
