# Rationale_ASSET_SCOUT

Started: 2026-05-12
Status: BUDGET COMPLIANT for static `02_HECTON_WORLD` texture dependency snapshot only; GLOBAL ASSET POOL NOT BUDGET COMPLIANT; DOTNET BUILD FAILED ON UNRELATED DEPENDENCIES

## Decision 001: Audit-Only Scope

Problem: The task asks for VRAM/resource scouting, not asset mutation. Directly changing import settings during a multi-agent batch can invalidate art ownership and hide root causes.
Solution: Produce evidence, calculated costs, offenders, and recommended cinematic fakes. Do not modify textures, meshes, audio, materials, scenes, project settings, or Addressables groups.
Rejected Alternatives: Auto-recompressing assets was rejected because Unity import changes can create broad `.meta` churn and can break third-party asset integrity without owner review.
Scalability potential: Low = flag and downscale heavy offenders; Middle = keep compressed 1024/2048 assets with streaming; High = allow hero 2K/4K only with residency proof; Ultra = spend saved VRAM on denser silhouettes and richer materials.
Hardware Impact: On i3/MX350, reporting RGBA32 and read/write duplication targets 2x to 8x VRAM savings per offender before runtime.

## Decision 002: Batch Prompt Fallback

Problem: The mandated `CURRENT_BATCH.md` extraction does not contain `ASSET_SCOUT`, but the user supplied the full XML block inline.
Solution: Record the extraction failure in status, use the inline XML as the active directive, and continue the audit.
Rejected Alternatives: Waiting for a batch-file rewrite was rejected because the full directive is already present and the audit is read-only.
Scalability potential: Low = no delay to catch MX350 risks; Middle/High/Ultra = same report can feed asset-tier policy.
Hardware Impact: Avoids zero-value delay; no runtime cost.

## Decision 003: Static-First Resource Census

Problem: Unity runtime/profiler proof may not be available for every asset, but `.meta`, material, scene, prefab, and import metadata can expose most VRAM crimes.
Solution: Use filesystem scans and Unity metadata first, then supplement with Unity MCP console/editor checks where possible.
Rejected Alternatives: A PlayMode-only memory capture was rejected as incomplete for assets not resident in the current scene.
Scalability potential: Low = catches uncompressed/no-mip/read-write assets before load; Middle = catches duplicate grouping; High/Ultra = identifies where expensive variants are justified.
Hardware Impact: Prevents avoidable 2GB VRAM overflow on MX350 by catching asset-level memory multiplication before GPU residency.

## Decision 004: Split MCP and Static Audit

Problem: A full Unity import audit exceeded MCP execution tolerance before output was written.
Solution: Use PowerShell static metadata parsing for texture import flags and dimensions, then use smaller Unity MCP probes for mesh and prefab data that require actual imported `Mesh` objects.
Rejected Alternatives: One monolithic Unity script was rejected after timeout because it produced no artifact. Pure `.fbx` metadata was rejected for mesh triangles because it cannot prove triangle counts in binary/imported model assets.
Scalability potential: Low = static texture scan catches 4K/auto/uncompressed/readable crimes without editor load; Middle = Unity probes verify prefab LOD ownership; High/Ultra = same CSVs identify where high-fidelity assets may remain if gated by tier.
Hardware Impact: On i3/MX350, the current top texture class shows 4K RGBA-equivalent cost of ~85.333MB each versus ~21.333MB BC7/BC5, a ~64MB save per texture before considering streaming behavior.

## Decision 005: Compile Verification Boundary

Problem: Unity console is not clean, but the active errors are outside ASSET_SCOUT's read-only audit scope.
Solution: Record compile verification as failed by dependency/other-agent state and continue reporting. No ASSET_SCOUT source or import settings were modified.
Rejected Alternatives: Fixing `NativeArenaArrayEditTests.cs` or `SaveBinaryStorage.cs` was rejected as cross-domain sabotage for this VRAM audit prompt.
Scalability potential: Low/Middle/High/Ultra unaffected by scout-only CSV generation.
Hardware Impact: No runtime hardware impact from ASSET_SCOUT artifacts; compile blockers must be handled by owning agents.

## Decision 006: Scene Texture Snapshot Is Not Runtime Residency

Problem: `02_HECTON_WORLD` texture dependencies can be estimated from AssetDatabase, but Unity texture streaming can decide actual resident mips at runtime.
Solution: Report the dependency estimate as a budget snapshot, not as profiler proof. Keep the hard result: 100 texture dependencies, estimated 404.714MB, below the 900MB texture partition but containing 4 flagged import risks.
Rejected Alternatives: Calling 404.714MB "compliant" was rejected because no PlayMode memory/profiler capture was available.
Scalability potential: Low = mip-downgrade and remove 4K offenders first; Middle = keep 2K tiled surfaces; High = allow 4K planet/sky assets only behind scene/zone residency; Ultra = spend saved budget on richer silhouettes and shader detail.
Hardware Impact: On MX350, the snapshot leaves theoretical texture headroom, but 4K direct dependencies such as `MapMagic/Map_Graph/New Gen/heightmap.png` can still pressure streaming and upload bandwidth.

## Decision 007: Crest Keyword Bloat Is Report-Only

Problem: Crest ocean materials show 15-16 active keywords, above the ASSET_SCOUT >8 keyword threshold, but AGENTS.md forbids runtime wrappers/material clone hacks for complex third-party assets.
Solution: Flag Crest materials for owner review and recommend tiered/curated material variants, not direct ASSET_SCOUT edits.
Rejected Alternatives: Mutating Crest materials in place was rejected because it can break third-party ocean rendering and violates third-party asset integrity.
Scalability potential: Low = strip unused Crest features for MX350 material set; Middle = curated foam/underwater subset; High = richer ocean variants; Ultra = visual overkill only after warmup and VRAM proof.
Hardware Impact: Reducing unnecessary Crest keywords can reduce shader variant memory and warmup stalls on i3/MX350, but exact microseconds require Frame Debugger/profiler capture.

## Decision 008: No Addressables Data Means Blocked Audit, Not Pass

Problem: The mission requires duplicate Addressable group detection, but `Assets/AddressableAssetsData` is absent.
Solution: Mark the Addressables duplicate audit as blocked by missing data and record zero duplicate findings only within that absent-data boundary.
Rejected Alternatives: Inferring Addressables groups from scene dependencies was rejected because that would fabricate bundle ownership.
Scalability potential: Low = Addressables must exist before zone streaming can protect MX350 VRAM; Middle/High/Ultra = group duplication must be eliminated before richer asset tiers are allowed.
Hardware Impact: Unknown. Duplicate bundle VRAM savings cannot be calculated without Addressables group metadata.

## Decision 009: Scene Static AO Failure Boundary

Problem: Task 15 requires baked AO/UV2 on static world assets, but `02_HECTON_WORLD` currently reports 412 mesh renderers and 0 static renderers in the additive scene probe.
Solution: Report scene-level baked AO as unverified. Prefab static UV2 scan found no missing UV2, but scene static/GI tagging appears absent or not represented in the opened scene.
Rejected Alternatives: Marking AO check passed was rejected because zero static renderers means there is nothing scene-static to validate.
Scalability potential: Low = baked AO should replace runtime SSAO/SSDO on MX350; Middle = selective probe/SSDO; High/Ultra = richer dynamic occlusion only after profiler proof.
Hardware Impact: If static GI/AO is missing, MX350 risks paying runtime ambient-occlusion/fill-rate cost instead of using baked texture data.

## Decision 010: Budget Compliance Split

Problem: The prompt demands `BUDGET COMPLIANT`, but the global asset pool contains clear import, size, read/write, audio, transparency, and LOD violations.
Solution: Split the verdict. `02_HECTON_WORLD` static texture dependency estimate is under the texture partition at `404.714MB / 900MB`. The global asset pool is not compliant until offenders are fixed or waived with profiler evidence.
Rejected Alternatives: A blanket `BUDGET COMPLIANT` status was rejected because it would conceal 27 oversized textures, 1,126 texture import risks, 14 large DecompressOnLoad clips, and missing Addressables data.
Scalability potential: Low = must fix top offenders and Addressables before MX350 confidence; Middle = allow selective 2K assets; High = allow high-fidelity variants; Ultra = visual overkill only behind streaming/residency proof.
Hardware Impact: Top-10 texture conversion/downscale targets represent `637.000MB` theoretical RGBA32-to-BC7/BC5 savings; exact runtime impact requires Texture Memory capture.

## OMEGA POLISH CHANGES

Problem: The final mandate requires anti-bloat proof and a build check, but ASSET_SCOUT is a report-only VRAM audit and the workspace contains unrelated compile failures from other domains.
Solution: Performed the polish pass after all 20 checklist items were closed. Confirmed the ASSET_SCOUT change set is documentation/report artifacts only, with no runtime C# code, no import-setting mutation, no asset mutation, no new hot-path loops, no new managed allocations, and no frame-time cost. Ran the mandated `dotnet build` command and recorded the external failure boundary.
Rejected Alternatives: Fixing missing bootstrap, platform, voxel, haptics, or native bridge symbols was rejected as cross-domain work. Marking the build as passed was rejected because the command returned `111 Error(s)` and `3 Warning(s)`.
Scalability potential: Low = audit artifacts identify MX350 blockers before residency; Middle = same reports guide 1024/2048 import policy; High = hero assets can be explicitly waived with profiler proof; Ultra = saved VRAM can be spent on controlled visual overkill instead of accidental 4K bloat.
Hardware Impact: ASSET_SCOUT itself adds `0us` runtime cost. The top-10 offender list identifies `637.000MB` theoretical texture memory reduction if RGBA32-equivalent assets are converted/downscaled to BC7/BC5-class costs. Build blockers have no ASSET_SCOUT hardware impact but prevent clean project verification.

Final scoped diff:
- `Docs/Tasks/Status_ASSET_SCOUT.md`
- `Docs/AgentLogs/Rationale_ASSET_SCOUT.md`
- `Docs/AgentLogs/LOG_ASSET_SCOUT.md`
- `Docs/AgentLogs/RECON_TECH_SCOUT.md`
- `Docs/AgentLogs/ASSET_SCOUT_quality_gate_crossref.md`
- `Docs/AgentLogs/ASSET_SCOUT_top10_vram_offenders.md`
- Generated `Docs/AgentLogs/ASSET_SCOUT_*.csv` and `Docs/AgentLogs/ASSET_SCOUT_*_summary.txt` audit artifacts.
