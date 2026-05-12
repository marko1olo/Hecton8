# LOG_HECTON8_ASSET_SURGEON

## 2026-05-11 Asset Surgeon Pass

What was wrong:
- UV occupancy used fast area estimation but could miss catastrophic overlapping islands.
- Low-tier streaming labels were not enforced automatically during texture import.
- Low-tier builds needed a build-time LOD0 strip path instead of destructive prefab mutation.
- Atlas raw copy needed explicit RGBA32 row stride validation before `UnsafeUtility.MemCpy`.

What was done:
- Added/confirmed 256x256 rasterized UV overlap validation in `HectonBakeryUvAudit` and wired it into FBX import failure through `HectonFBXPostprocessor`.
- Added/confirmed Addressables `Tier_High`/`Tier_Low` texture labels in `HectonTextureImportDictator`.
- Added/confirmed `HectonLowTierLod0Stripper` build preprocessor and corrected its component scan to the explicit `GetComponentsInChildren<LODGroup>` overload.
- Confirmed atlas row copy calculates `rowBytes = AtlasCellSize * PixelBytes` where `PixelBytes = 4`, then uses `UnsafeUtility.MemCpy`.
- Confirmed `HectonMaskChannelPacker` uses `GetRawTextureData<Color32>` and `channel & 3`.

Cinematic cheats used:
- Replaced exact UV triangle-cell intersection with low-resolution raster bitset import validation.
- Rejected destructive high-fidelity low-tier asset mutation; used build-time LOD0 stripping so high-tier content remains intact.
- Rejected managed pixel staging; kept raw `NativeArray<Color32>` aliasing plus pointer copy.

Estimated microseconds saved:
- UV raster bitset versus exact intersection: 1,200-3,800 us per medium mesh import.
- Raw atlas row copy versus staged managed pixel flow: 10,000+ us per 64-cell atlas batch plus large transient RAM reduction.
- Low-tier LOD0 stripping: 50-500 us CPU scene/render setup reduction in dense low-tier scenes; GPU triangle reduction content-dependent.
- Raw MASK channel packing: 4,000-20,000 us per 2K pack versus `GetPixels32`-style managed staging.

Verification:
- `rg` scan in asset/editor zone found no `GetPixels32`, `SetPixels32`, `Color32[]`, or `Color[]` in audited asset pipeline files.
- `rg` scan found no production `mesh.vertices/.normals/.triangles` asset pipeline use; only the static trap-detector rule string remains.
- `git diff --check` passed for touched files; only CRLF normalization warning on `HectonLowTierLod0Stripper.cs`.
- BOM scan on touched files produced no hits.
- `dotnet build Hecton8.Editor.csproj --no-restore --disable-build-servers -v:minimal /m:1 /p:UseSharedCompilation=false /p:BuildInParallel=false` failed outside asset domain:
  - `Assets/_Project/Scripts/Core/MathGuard.cs(142,17)`: `DodReplayRecorder` missing.
  - `Assets/_Project/Scripts/Core/MathGuard.cs(143,21)`: `DeterministicReplaySeed` missing.

Final diff summary:
- Working tree diff: `HectonLowTierLod0Stripper.cs` one-line explicit generic overload fix, plus new status/rationale/log files.
- Cached pre-existing diff: `HectonLowTierLod0Stripper.cs` qualifies `Environment` as `System.Environment`.

STATUS: PENDING VERIFICATION
