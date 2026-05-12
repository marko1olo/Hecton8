# FILL-RATE_DICTATOR Status

Status: PENDING VERIFICATION
Assignment source: user-supplied HECTON-8 fill-rate prompt, 30 tasks. CURRENT_BATCH.md was not present in this workspace scan.

- [x] 1. Dithered Transparency Only | DOD: converted targeted first-party HUD/leak/smoke alpha blends to AlphaTest/cutout with deterministic dither. Alternative rejected: Transparent queue alpha blending on MX350. Estimate: 120-350 us saved in stacked VFX zones, pending profiler.
- [x] 2. Stencil Masked HUD | DOD: added visor stencil writer and HUD Equal stencil test using ref 1. Alternative rejected: shading hidden HUD pixels under helmet frame. Estimate: 40-120 us saved, pending RenderDoc.
- [BLOCKED BY DEPENDENCY] 3. Z-Prepass For Water | DOD: not edited because water ownership appears tied to Crest/Water layer and AGENTS forbids custom Crest wrappers/material clones. Alternative rejected: direct Crest material override. Estimate: unknown until water renderer ownership is assigned.
- [x] 4. Half-Res VFX Rendering | DOD: existing half-res pass extended with configurable bilateral depth-scale resolve. Alternative rejected: native-res transparent particle storm. Estimate: 300-900 us saved in silt/smoke zones, pending profiler.
- [ ] 5. AAA Noir Contrast | DOD: pending because Hecton_CoreLit.hlsl is already dirty and outside this slice. Alternative rejected: overwriting another agent's shader edits. Estimate: pending.

## Loop 2 - Tasks 6-10
- [ ] 6. Blue Noise Shadow Dither | Pending.
- [ ] 7. Volumetric Fog Jitter | Pending.
- [ ] 8. ALU Caustics Fake | Pending.
- [ ] 9. Depth-Faded Alpha | Pending.
- [ ] 10. TAA Motion Vector Fix | Pending.

## Loop 3 - Tasks 11-16
- [ ] 11. LOD Shader Switching | Pending.
- [x] 12. Stencil Visor Overlay | DOD: visor mask writes stencil 1, HUD shader reads Equal 1. Alternative rejected: alpha-masked visor overlay. Estimate: same as Task 2, pending RenderDoc.
- [ ] 13. Opaque Depth Prepass | Pending.
- [ ] 14. Light Probe Approximation | Pending.
- [x] 15. Shader Variant Stripping | DOD: editor shader stripper now removes POINT/SPOT light variants by default for MX350 builds unless HECTON_MX350_SHADER_STRIP=0. Alternative rejected: runtime keyword disable. Estimate: build/warmup pressure reduction pending Unity import logs.
- [ ] 16. Skybox Noise Injection | Pending.

## Loop 4 - Tasks 17-23
- [x] 17. Bilateral Upsampling | DOD: 2x2 half-res particle upsample weighted by scene-depth delta. Alternative rejected: full-res VFX or 3x3+ upsample. Estimate: included in Task 4, pending profiler.
- [ ] 18. Screen-Space Decals | Pending.
- [ ] 19. Interactive Prop-Wash Masks | Pending.
- [ ] 20. Refraction Math LOD | Pending.
- [ ] 21. Zero-Texture Biolum | Pending.
- [ ] 22. Cloud Shadow Fake | Pending.
- [ ] 23. FOV Distortion Compensation | Pending.

## Loop 5 - Tasks 24-30
- [ ] 24. Dithered Terrain Blending | Pending.
- [ ] 25. Depth-Aware Particle Scaling | Pending.
- [ ] 26. BRG Property Packing | Pending.
- [x] 27. Shader Error Fallback | DOD: new/edited shaders point to a project-local black fallback shader instead of magenta. Alternative rejected: Unity built-in Hidden/InternalError because it is magenta, not black. Estimate: visual failure containment only.
- [ ] 28. VRAM Fragmentation Audit | Pending.
- [ ] 29. Stencil Portal System | Pending.
- [ ] 30. Omega Fill-Rate Gate | Pending.

## Verification
- [x] dotnet build compile check | Assembly-CSharp.csproj and Assembly-CSharp-Editor.csproj passed; warnings were pre-existing/third-party.
- [ ] Unity import/console check
- [ ] Frame Debugger/RenderDoc stencil and overdraw check
- [ ] MX350 profiler capture
