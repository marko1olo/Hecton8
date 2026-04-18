**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# Gemini Review Findings Summary
## Critical Issues Identified in HECTON-8 Project

Based on Gemini's analysis of the project documentation, here are the critical architectural issues that need to be addressed:

### 🔴 Critical Vulnerabilities (Immediate Attention Required):

1. **Triplanar Shader Hell** (Texture Bandwidth Killer)
   - Triplanar mapping uses 9 texture samples per pixel (3 textures × 3 axes)
   - MX350 limit is ~5-6 samples per pixel comfortably
   - **Fix**: Implement Cheap Triplanar/Biplanar mapping with branch culling or axis dominance

2. **Floating Origin vs GPU Instancer** (Physics Desync)
   - When world shifts, GPU Instancer matrices don't update
   - Causes objects to remain at old coordinates after world shift
   - **Fix**: Use GPUInstancerAPI.SetGlobalPositionOffset() or Burst job for matrix updates

3. **Synchronous Mesh Collider Baking** (Main Thread Spike)
   - Generating MeshCollider on main thread causes 2-3 frame stalls
   - **Fix**: Use Physics.BakeMesh() in background job, assign when complete

4. **Overdraw Death** (Transparency Fill-rate Killer)
   - Multiple transparent layers (water, fog, light beams, particles, refraction) cause 5-6x overdraw
   - **Fix**: Downsample volumetric fog, make particle shaders Opaque/AlphaTest, use _CameraOpaqueTexture for refraction

### 🏗️ Architectural Strengths (What We're Doing Right):
- Separation of rendering and physics (ProximityColliderSystem + GPU Instancer)
- Voxel engine optimization (Marching Cubes buffer reduction ×7.5 → ×2)
- UI on Shaders and Camera Stacking (avoids Canvas rebuilds)
- Data-Driven architecture (DTO + Event Bus, zero-GC hot paths)

### 📋 Immediate Action Items for Code Review:
1. Review all shader implementations for texture sample counts
2. Verify Floating Origin integration with GPU Instancer systems
3. Check voxel pipeline for async physics baking
4. Audit transparency usage in water/fog/particle systems
5. Validate UI string formatting follows dirty-flag patterns
6. Confirm object pooling is used for all frequent spawns
7. Check for any Update()-based distance calculations (should use sqrMagnitude)
8. Verify Addressables usage includes proper Release() calls
9. Ensure Event Bus subscriptions are properly cleaned up
10. Validate physics checkpointing for floating origin compatibility

The review confirms our core architecture is sound but highlights specific integration points where performance bottlenecks could emerge on target hardware (MX350 2GB VRAM).
