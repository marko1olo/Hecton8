# CARTOGRAPHY_UX_LEAD Rationale

Status: `PENDING VERIFICATION`

## Intake Decision

Problem: PDA/cartography map stores discovery in heavyweight UI/runtime forms and the prompt reports banned `FindObjectOfType<Terrain>()` plus `List<Vector3>` use.
Solution: Inspect existing ownership first, then route discovery through AUP-indexed native bitmasks and decoupled signals rather than UI-side object hunting.
Rejected Alternatives: Standard Unity scene search, managed lists, mesh-per-cell storage, and direct terrain references are rejected because they allocate, couple UI to world terrain, and violate batch isolation.
Scalability potential: Low = 2D height-only cells; Middle = coarse point cloud; High = SDF-gated solids; Ultra = POI overlays and richer shader response.
Hardware Impact: Expected gain for i3/MX350 comes from replacing managed vector storage and map meshes with packed `ulong` masks and GPU append output; exact microsecond proof is pending profiling.

## Loop 1 Decisions - Purge And Macro Grid

Problem: PDA map owned a 3D SDF texture path and compute raymarch, so every map draw depended on heavyweight voxel payloads.
Solution: Removed the `Texture3D`/`_VoxelSdfTexture3D` lane from `PDAMapTab` and routed rendering through `NativeArray<ulong> DiscoveredSectors` uploaded to `Hecton_MapMesh.compute`.
Rejected Alternatives: Keeping SDF raymarch as a high-tier branch was rejected because Task 4 is a purge and Low/MX350 must not pay 3D texture fetch risk.
Scalability potential: Low = stride sector words and one point per word; Middle = two bits per word; High = denser append sampling; Ultra = add richer POI glyph pass after core compile.
Hardware Impact: i3/MX350 avoids 3D texture allocation/fetch and raymarch loops; estimated save is 80-250 us per visible PDA dispatch versus the old 8x8x8 raymarch path.

Problem: Cartography math needed to be reusable without binding UI to world/runtime classes.
Solution: Added `Hecton8.Cartography` with `CartographyAup`, `MapRevealSignal`, 50m grid constants, and Burst `IJob` bitmask writers using `wordIndex = bitIndex >> 6` and `bitOffset = bitIndex & 63`.
Rejected Alternatives: Using `AbsoluteUniversePosition` directly in the new assembly was rejected because that type lives in the runtime world assembly, not contracts.
Scalability potential: Low/Middle/High/Ultra all share the same 1-bit sector truth; render quality scales in compute without changing save data.
Hardware Impact: Bit writes are one `ulong` OR per cell; expected CPU cost is under 10 us for player-cell reveal and bounded by ping radius for scanner reveals.

Problem: POI reveal had no public `PersistentWorldRegistry` POI copy surface.
Solution: Injected PDA marker snapshots first, then used the registry save snapshot deltas as known persistent points while staying inside current assembly access boundaries.
Rejected Alternatives: Adding a new global POI API was rejected as cross-domain churn during a parallel batch.
Scalability potential: Low = reveal POI sector only; Middle = existing marker overlay; High/Ultra = later GPU red glyph buffer.
Hardware Impact: POI injection is capped at 64 records per slow tick; expected cost stays below 20 us on low-end silicon.

## Loop 4 Decisions - Save And Visibility

Problem: Fog-of-war state must persist without building a second save system.
Solution: Extended `ExplorationMapDTO` to v67 with sector mask words and byte payload, then wrote the live `NativeArray<ulong>` through the existing binary payload codec.
Rejected Alternatives: A separate cartography save file or JSON sidecar was rejected because it adds file churn and poor compression.
Scalability potential: Low = sparse byte payload; Middle/High/Ultra = same data with richer draw shaders.
Hardware Impact: Save-time copy is bounded to 262 KB before compression and does not touch frame hot paths.

Problem: Hidden PDA map must cost zero GPU dispatch.
Solution: Kept dispatch inside `LateFrameTick` behind active/enabled, payload-ready, camera-visible checks, then used `Graphics.RenderMeshIndirect` only after compute succeeds.
Rejected Alternatives: Background compute refresh was rejected because the PDA map is not gameplay-critical when hidden.
Scalability potential: Low/hidden = no dispatch; Middle = strided words; High/Ultra = denser append without CPU path change.
Hardware Impact: Hidden PDA path is 0 GPU dispatches; visible MX350 path scans 1/4 sector words.

## Loop 5 Decisions - Verification Wall

Problem: Full Unity compile could not complete from CLI.
Solution: Ran `dotnet build` for the solution and core project, then isolated `CartographyGridJobs.cs` in a temporary `netstandard2.1` project with Unity Burst/Collections/Mathematics references; the cartography job compile passed.
Rejected Alternatives: Editing unrelated `BootstrapStatus`, package SDK, or stale generated csproj files was rejected because those are outside this prompt and owned by other agents.
Scalability potential: Compile wall does not affect runtime design; the local job assembly is independently syntax-checked.
Hardware Impact: No runtime impact; verification risk remains on Unity project generation rather than cartography bit math.

## OMEGA POLISH CHANGES

Problem: The map compute used `sqrt()` for visual intensity falloff even though exact distance was not needed.
Solution: Replaced it with squared-distance falloff using `dot(xz, xz) * rcp(radiusSq)`.
Rejected Alternatives: Keeping linear distance was rejected because the shader only needs stable visual attenuation.
Scalability potential: Low/Middle/High/Ultra all benefit because the same compute kernel serves every tier.
Hardware Impact: Removes one square root per appended sector candidate; MX350 savings are small per point but deterministic.

Problem: Comments and tooltips still described the old SDF/raymarch map path after the purge.
Solution: Updated PDA map comments/tooltips to describe packed sector expansion and the RenderMeshIndirect path.
Rejected Alternatives: Leaving stale comments was rejected because future agents would resurrect the old branch.
Scalability potential: Documentation now points all tiers to the bitmask/compute path.
Hardware Impact: No runtime impact; reduces reintegration risk.

## Loop 7 Static Recheck - 2026-05-13

Problem: The prompt source moved; `Docs/Tasks/CURRENT_BATCH.md` no longer contains `CARTOGRAPHY_UX_LEAD`.
Solution: Re-extracted the exact XML block from the Cyrillic-named batch file under `Docs/` and recorded the source mismatch in status.
Rejected Alternatives: Relying on chat memory was rejected because the batch protocol demands CLI extraction.
Scalability potential: No runtime effect; prevents neighboring-agent prompt bleed.
Hardware Impact: 0 us.

## Loop 9 Service Cache And Dirty Flag - 2026-05-13

Problem: Visible PDA point-cloud rendering resolved player/exploration context more than once per frame and pulled predator/marker/audio/world-seed services directly from `GlobalRegistry` in repeated paths.
Solution: Added cached service fields with Unity-object liveness guards, passed the already resolved player AUP into `DispatchSonarPointCloud`, and kept marker/predator/audio/world-seed access behind local resolver methods.
Rejected Alternatives: Leaving repeated registry reads in visible-frame dispatch was rejected because it violates the two-stage dependency intent and wastes CPU on a path already doing GPU work.
Scalability potential: Low = fewer CPU lookups before strided dispatch; Middle/High/Ultra = same render path with more point density and shader polish.
Hardware Impact: Estimated 1-5 us/frame saved on visible PDA frames depending on service fallback path; no added allocations.

Problem: Duplicate sonar/acoustic/POI reveal events bumped `_cartographyRevision` even when every target sector bit was already set, forcing unnecessary 262 KB sector-buffer uploads.
Solution: Added one persistent `NativeArray<int>` change flag and made reveal/POI jobs set it only when an OR operation changes a word.
Rejected Alternatives: CPU-side word snapshots around every reveal sphere were rejected because that would add more memory reads than the job-side bit-flip detection.
Scalability potential: Low/Middle reduce PCIe bandwidth on repeated pings; High/Ultra can spend saved upload bandwidth on denser visual layers.
Hardware Impact: Avoids one 262 KB GPU upload on unchanged reveal slow ticks; on MX350 this reduces PCIe pressure and driver work.

Problem: Mojibake cleanup left touched `COLD ALLOC` comments in a near-miss hyphenated format.
Solution: Restored the repo-mandated em dash separator on touched allocation comments.
Rejected Alternatives: Keeping ASCII hyphens was rejected because this repo has a strict allocation-comment scanner expectation.
Scalability potential: No runtime effect.
Hardware Impact: 0 us.

Problem: The required RECON artifact was missing.
Solution: Added `Docs/AgentLogs/RECON_CARTOGRAPHY_UX_LEAD.md` with the `Assets/_Project/Scripts/UI` purge scan result.
Rejected Alternatives: Treating status prose as the recon artifact was rejected because the prompt explicitly names a RECON log.
Scalability potential: No runtime effect; purge evidence is now durable.
Hardware Impact: 0 us.

Problem: Low-tier dispatch used `wordStride` only as an early-return branch, so MX350 still launched every word thread.
Solution: Changed the CPU dispatch count to `ceil(wordCount / wordStride)` and the compute kernel to map `dispatchThreadId.x * wordStride`.
Rejected Alternatives: Keeping branch-only stride was rejected because it saves ALU but not scheduler/thread launch cost.
Scalability potential: Low = quarter word dispatch; Middle/High/Ultra = full scan with denser visuals.
Hardware Impact: MX350 launches 8192 word threads instead of 32768 for the PDA map build; estimated visible-PDA save is 10-30 us depending on driver overhead.

Problem: Scanner reveal radius accepted any event radius, so a bad ping could force a massive triple-loop in SlowTick.
Solution: Added finite radius clamping with a 250m cartography cap in the managed signal path and Burst job.
Rejected Alternatives: Trusting event producers was rejected because many systems can publish acoustic events.
Scalability potential: Low/Middle keep bounded reveal work; High/Ultra can spend saved cycles on richer shader sweep/POI visuals, not CPU truth expansion.
Hardware Impact: Worst reveal sphere is 11x11x11 = 1331 cell tests per signal instead of unbounded millions on i3/MX350.

Problem: Cartography save copy could fetch the native pointer even when the serialized sector byte payload was empty.
Solution: Guarded the unsafe pointer copy behind `byteCount > 0 && _discoveredSectors.IsCreated`.
Rejected Alternatives: Relying on tracker initialization was rejected because save/load paths must fail closed.
Scalability potential: Same binary format across tiers.
Hardware Impact: No frame impact; reduces save failure risk.

Problem: The recursive polish prompt requested a sonar sweep visual.
Solution: Added a `_Time.y` driven vertical sweep boost in the point-cloud shader using one triangle-wave-like moving line and no texture samples or trig.
Rejected Alternatives: CPU-side sweep points or a separate mesh pass were rejected as bandwidth/draw-call waste.
Scalability potential: Low gets the same readable sweep for near-zero cost; High/Ultra can stack richer point density over the same signal.
Hardware Impact: Adds a few scalar ALU ops per visible point; expected cost under 1 us for the visible bounded point count.

Final Git Diff: see working tree diff for `CartographyGridJobs.cs`, `PlayerExplorationTracker.cs`, `PDAMapTab.cs`, `Hecton_MapMesh.compute`, `Hecton_PDA_SonarPointCloud.shader`, `SaveData.cs`, `SaveBinaryPayloadCodec.cs`, `SaveDataMigration.cs`, status, rationale, and log files.

## Loop 8 Strict Purge Recheck - 2026-05-13

Problem: The 128^3 cartography mask originally encoded only macro cells inside the origin-centered local window, so AUP positions beyond +/-64 macro cells could stop revealing new sectors.
Solution: Added toroidal macro-axis wrapping in `CartographyGridMath.WrapMacroAxisToLocal()` and changed the GPU decode to resolve wrapped cells nearest to the current player macro cell.
Rejected Alternatives: An unbounded `NativeParallelHashMap<int3, byte>` was rejected because the prompt mandates the fixed `NativeArray<ulong>` 1-bit mask and because hash-map serialization would add save churn.
Scalability potential: Low = same fixed 262 KB page; Middle = full page with strided GPU scan; High = denser point emission; Ultra = richer shader/POI overlays without changing truth storage.
Hardware Impact: No extra per-frame CPU cost; on i3/MX350 this prevents map reveal failure after origin movement while keeping the fixed memory budget.

Problem: `PDAMapTab` still contained a dead headless `Texture2D` cartography job, legacy material fallback, raymarch names, and an unused `_VoxelCellSize` constant-buffer lane.
Solution: Deleted the inactive texture pipeline, removed fallback shader/material state, renamed the active compute lane to `BuildMapPoints`, and reduced the constant buffer from 96B to 80B.
Rejected Alternatives: Keeping the branch behind a `true` flag was rejected because future agents could re-enable the old path and because stale names corrupt maintenance decisions.
Scalability potential: Low/Middle/High/Ultra now all share one GPU point-cloud route; quality scales by word stride, emitted bits per word, height colorization, and shader polish instead of alternate pipelines.
Hardware Impact: Removes cold `Texture2D` + pixel `NativeArray` allocations and one dead material path; expected frame impact is 0 us when hidden and lower maintenance/initialization risk when opened.

Problem: The prompt source file moved again; the prior Cyrillic dump path is now absent and `Docs/Tasks/CURRENT_BATCH.md` does not contain this XML block.
Solution: Kept file-backed state in `Status_CARTOGRAPHY_UX_LEAD.md`, `Rationale_CARTOGRAPHY_UX_LEAD.md`, and `LOG_CARTOGRAPHY_UX_LEAD.md`; recorded the missing prompt source as a verification fact.
Rejected Alternatives: Reconstructing directives from memory was rejected. No neighboring prompt was used.
Scalability potential: No runtime effect.
Hardware Impact: 0 us.
