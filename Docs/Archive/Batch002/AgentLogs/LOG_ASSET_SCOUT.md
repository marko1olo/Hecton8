# LOG_ASSET_SCOUT

Top = old. Bottom = new.

## 2026-05-12 ASSET_SCOUT VRAM Audit

What was wrong:
- Asset pool is not globally budget-compliant. Static scan found `1,126` texture import risks, `27` oversized textures, `10` readable textures, `1,485` readable mesh assets, `186` audio importer offenders, `656` transparent/fill-rate-risk materials, and `1` high-poly prefab without LODGroup.
- `Assets/AddressableAssetsData` is absent, so duplicate Addressables and residency grouping cannot be certified.
- `02_HECTON_WORLD` has `412` mesh renderers but `0` static renderers in the additive scene probe; baked AO/static GI cannot be certified.
- Compile verification is blocked by unrelated current Unity console errors, including `SaveBinaryStorage.cs` Burst `catch` filter and `HectonIndirectVegetationContracts.cs` unassigned out parameter.

What was done:
- Created `Docs/Tasks/Status_ASSET_SCOUT.md`.
- Created/updated `Docs/AgentLogs/Rationale_ASSET_SCOUT.md`.
- Wrote audit artifacts `ASSET_SCOUT_*.csv`, `ASSET_SCOUT_*_summary.txt`, `ASSET_SCOUT_quality_gate_crossref.md`, `ASSET_SCOUT_top10_vram_offenders.md`, and `RECON_TECH_SCOUT.md`.
- Scanned textures, meshes, prefabs, audio, materials, animations, lightmaps, Addressables data, scene dependencies, scene static UV2, material instancing, quality gates, and Unity 6 rendering docs.

Cinematic Cheats used / recommended:
- Replace 4K panel/detail textures with 1024/2048 atlases, channel-packed masks, and distant impostors.
- Use BC5 detail-normal atlases instead of 4K unique normals.
- Move eligible transparent materials to opaque+dither/alpha-clip.
- Replace non-critical high-poly ocean mesh surface with tiled/procedural patch or lower LOD mesh.
- Use baked AO/static lighting on MX350 instead of runtime AO where static geometry exists.

Exact Microseconds saved:
- `0us` directly saved by ASSET_SCOUT because no assets or settings were changed.
- Theoretical top-10 RGBA32-to-BC7/BC5 VRAM savings: `637.000MB`.
- `02_HECTON_WORLD` static texture dependency estimate: `404.714MB / 900MB` texture partition.
- Per-frame microseconds require Frame Debugger / GPU profiler / Texture Memory capture and are not fabricated.

Status:
- `BUDGET COMPLIANT` only for static `02_HECTON_WORLD` texture dependency estimate.
- `NOT BUDGET COMPLIANT` for the global asset pool until listed offenders are fixed or waived with evidence.

## 2026-05-12 ASSET_SCOUT Omega Polish

What was wrong:
- Final project verification is not clean. `dotnet build .\Hecton8\Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` failed with `111 Error(s)` and `3 Warning(s)`.
- Major blocker categories are missing cross-domain symbols: `HectonPersistentPathPolicy`, `PlatformPrecisionClock`, `HectonThreadPriorityPolicy`, `SteamDeckInputPal`, `VoxelChunkModifiedEvents`, `HardwareTierDetector`, native bridge, and haptics types.
- These failures are not caused by ASSET_SCOUT because this agent wrote report files only and changed no runtime sources or assets.

What was done:
- Read the `POLISH_MANDATE` only after all 20 ASSET_SCOUT tasks were checked or blocked.
- Performed anti-bloat review of the ASSET_SCOUT output.
- Confirmed no `Assets/` files, import settings, scenes, prefabs, materials, shaders, Addressables groups, or runtime C# systems were modified by ASSET_SCOUT.
- Recorded the final scoped diff as new/updated ASSET_SCOUT documentation and audit artifacts under `Docs/Tasks` and `Docs/AgentLogs`.

Cinematic Cheats used / recommended:
- Same as core report: downscale 4K non-hero materials, use BC5/BC7/channel packing, convert eligible transparency to opaque+dither, replace heavy non-hero mesh surfaces with impostors or tiered LOD, and use baked AO/static lighting where static geometry is valid.

Exact Microseconds saved:
- `0us` directly saved by ASSET_SCOUT because no runtime path or asset import setting was changed.
- `0us` added by ASSET_SCOUT because generated files are docs/reports only.
- Top-10 texture offender theoretical savings remains `637.000MB`.
- Scene snapshot remains `404.714MB / 900MB` for static `02_HECTON_WORLD` texture dependencies, not runtime residency proof.

Status:
- `BUDGET COMPLIANT` for static `02_HECTON_WORLD` texture dependency snapshot only.
- `NOT BUDGET COMPLIANT` for the global asset pool.
- `BUILD NOT VERIFIED` because the project currently fails on unrelated dependencies.
