# MATERIAL_DECAY_ARTIST Log

## 2026-05-13 Dynamic Wear & Tear POM
What was wrong:
Equipment corrosion had an existing scalar publisher, but no material-decay shader consumer with physical-looking pitting. Held-tool placeholder materials were still on URP Lit, so Hecton_CoreLit.hlsl changes would not appear on tools.

What was done:
Added Hecton8.VFX.Materials assembly and MaterialDecayRuntime. The runtime consumes ItemDurabilityChangedSignal, PlayerStressSignal, and UIStateStore.Health01; drives _HectonEquipmentRust01, _HectonMaterialDecayRuntime, _HectonPlayerBloodSplatter, and one shared _RustDetailMap; emits ToolAcousticSignal pitch drops for rusted items; writes a 300-frame NativeArray blackbox and dumps Dump_MATERIAL_DECAY_ARTIST.bin on fault.

Shader work:
Extended Hecton_CoreLit.hlsl with _RustDetailMap R/G/B/A pack, clean-path early-out, 4-step POM gated by rust > 0.3 and low-tier bypass, rust normal/roughness blend, fresnel edge wear, blood glossy patches, wetness smoothness boost, and UV pit distortion. Added Hecton8/Tools/DecayLit and retargeted the 12 existing tool placeholder materials to it. No extra materials or rust decal GameObjects were created.

Cinematic cheats used:
POM/UV pits instead of mesh deformation; fresnel edge rust instead of edge masks; hashed glossy blood patches instead of decals; waterline/recent-wet smoothness instead of wet material swaps; cheap rust normal z approximation instead of exact sqrt.

Exact microseconds saved:
Pending capture. Estimated CPU saved versus decal/material-clone path: 50-300us depending visible tool/decal count. Clean rust==0 shader path now saves one _RustDetailMap sample per shaded tool pixel. Runtime managed allocation after cold bootstrap: 0B/frame by static property IDs, event spans, and change-gated global uploads.

Verification:
MaterialDecayRuntime.cs Unity validate_script returned 0 errors and 0 warnings. Unity compile and dotnet build are blocked by unrelated shared-workspace errors in HectonUnderwaterVisuals.cs, earlier GlobalDataVault.cs, and missing Hecton8.Vehicles.VFX assembly. STATUS remains PENDING VERIFICATION.
