# Status_HECTON8_ASSET_SURGEON

Status: PENDING VERIFICATION
Domain: Content Optimization & VRAM Asset Pipeline
Mandates read: OPT_Zero_GC_Policy_AllocFree_Mandate, STRM_Asset_Lifecycle_Addressables_Loading_Memory, OPT_Performance_Budgets_FrameTime_VRAM_Limits, OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.

- [x] Task 1 Rasterized UV overlap check | DOD: 256x256 cold editor bitset raster, strict barycentric cell coverage. Rejected exact 64x64 intersection and pure area heuristic because they either stall import or miss overlap. Estimate: 1,200-3,800 us saved per medium mesh import versus exact cell-triangle scans, with overlap detection restored.
- [x] Task 2 Scalability Addressables labels | DOD: importer/menu assigns Tier_High for 2048+ textures and Tier_Low for 512 atlas/sheet assets. Rejected manual label discipline because it drifts under batch import. Estimate: runtime VRAM savings depends on bundle selection, editor overhead only.
- [x] Task 3 Low-tier LOD0 stripping | DOD: build scene preprocessor strips LOD0 when low-tier flags/platform apply. Rejected destructive prefab mutation because build-time scene processing is safer under parallel agent work. Estimate: 50-500 us culling/render setup saved per dense low-tier scene, GPU triangle savings scene-dependent.
- [x] Task 4 8x8 atlas raw row copy guard | DOD: UnsafeUtility.MemCpy row bytes now validated against RGBA32 stride. Rejected SetPixels32/staged managed arrays. Estimate: 10,000+ us and tens of MB avoided during bulk atlas generation.
- [x] Task 5 Mesh-copy API audit in touched UV path | DOD: UV/index reads use GetUVs/GetTriangles list-backed scratch paths. Rejected mesh.vertices-style array copies. Estimate: 100-800 us and one managed array avoided per mesh audit.
- [x] Task 6 M.A.S.K. raw channel packing confirmation | DOD: GetRawTextureData<Color32> and channel & 3 pointer loops remain in use. Rejected GetPixels32 managed staging. Estimate: 4,000-20,000 us and 2x pixel memory avoided for 2K maps.
- [x] Task 7 BC7/BC5 + normal green raw fix confirmation | DOD: texture importer keeps BC7/BC5 enforcement and raw indexed normal green inversion. Rejected managed Color arrays. Estimate: import-time memory reduction, runtime VRAM format compliance.
- [x] Task 8 FBX import repair + UV fail gate | DOD: FBX postprocessor disables read/write extras and now throws on rasterized UV overlap. Rejected report-only import warnings because bad UVs must not enter production.
- [ ] Task 9 Unity compilation | Build blocked by Core/gameplay errors outside asset domain. STATUS: PENDING VERIFICATION.

Latest compile wall:
- `Assets/_Project/Scripts/Core/MathGuard.cs(142,17)`: `DodReplayRecorder` missing.
- `Assets/_Project/Scripts/Core/MathGuard.cs(143,21)`: `DeterministicReplaySeed` missing.
