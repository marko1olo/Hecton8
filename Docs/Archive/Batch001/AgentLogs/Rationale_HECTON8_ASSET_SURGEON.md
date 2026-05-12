# Rationale_HECTON8_ASSET_SURGEON

Problem: Area-only UV occupancy was fast but blind to overlapping UV islands.
Solution: Editor-only 256x256 raster bitset. Each UV triangle writes covered pixel-center cells; a second write to an occupied bit fails import as overlapped UVs.
Rejected Alternatives: Exact triangle-cell intersection over high grids was too expensive for bulk import; triangle area heuristic missed artist overlap failures.
Scalability potential: Low tier benefits by rejecting bloated/overlapped assets before bundle creation. Mid/High/Ultra keep higher-res assets but still require clean UV authoring.
Hardware Impact: Saves about 1.2-3.8 ms per medium import versus exact scans while restoring overlap detection.

Problem: Texture tiering could be forgotten during batch imports.
Solution: Texture import postprocess assigns Addressables `Tier_High` to 2048+ textures and `Tier_Low` to 512 atlas/sheet assets, with a manual sync menu.
Rejected Alternatives: Manual Addressables labels are not enforceable under bulk content churn.
Scalability potential: Low-tier bundle can exclude high-res entries; Ultra can keep high-res labels and stream richer content.
Hardware Impact: Runtime savings depend on bundle selection; editor cost is cold path only.

Problem: Low-tier builds should not carry LOD0 render geometry when LOD1 is mandatory top detail.
Solution: Build scene preprocessor strips LOD0 from `LODGroup` objects under low-tier flags, env vars, defines, Android, or WebGL builds.
Rejected Alternatives: Mutating prefab assets was rejected because raw asset edits are dangerous under parallel agents and contaminate high-tier content.
Scalability potential: Minimal/Low builds shrink mesh residency; High/Ultra builds preserve LOD0 visual overkill.
Hardware Impact: Expected culling/render setup savings are 50-500 us in dense scenes, with larger GPU triangle savings depending on content.

Problem: Raw atlas row copies can corrupt memory if row byte stride is implicit or wrong.
Solution: Guard raw data lengths and calculate `rowBytes` as `AtlasCellSize * PixelBytes` with RGBA32 `PixelBytes = 4`.
Rejected Alternatives: `SetPixels32` and full managed staging were rejected due Color32[] heap duplication.
Scalability potential: Low tier generates 512/2048 atlases without editor RAM spikes; Ultra can still build richer atlases with the same stream pattern.
Hardware Impact: Avoids tens of MB transient RAM and roughly 10+ ms bulk-copy overhead during large atlas generation.
