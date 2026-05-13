# CARTOGRAPHY_UX_LEAD Log

## 2026-05-12 - 1-Bit Radar Purge

What was wrong:
- PDA cartography was tied to a `Texture3D` SDF/raymarch path in `PDAMapTab`, with old compute kernel names and indirect draw flow.
- Discovery state existed as a dense legacy mask but not as the requested 50m 1-bit macro sector truth.
- Ping reveals had no decoupled map signal lane, and POI reveal was not feeding fog-of-war.

What was done:
- Added `Assets/_Project/Scripts/Cartography/Hecton8.Cartography.asmdef`.
- Added `CartographyGridJobs.cs` with `CartographyAup`, `MapRevealSignal`, 50m grid constants, `CartographyRevealAupCellJob`, `CartographyRevealSphereJob`, and `CartographyInjectPoiJob`.
- Extended `PlayerExplorationTracker` with `NativeArray<ulong> _discoveredSectors`, a prewarmed `NativeQueue<MapRevealSignal>`, capped POI staging, and a 300-frame `CartographyBlackBoxEntry` ring dumping to `Docs/AgentLogs/Dump_CARTOGRAPHY_UX_LEAD.bin` on NaN detection.
- Registered tracker with `ISlowTickable`, `IAcousticPingEventListener`, and `ISonarPingEventListener`.
- Removed PDA `Texture3D` usage and `_VoxelSdfTexture3D` binding from `PDAMapTab`.
- Added `Assets/_Project/Art/Shaders/Hecton_MapMesh.compute` to scan sector words and append points to the GPU append buffer.
- Swapped PDA point draw to `Graphics.RenderMeshIndirect`.
- Patched point-cloud shader height gradient to use local height depth.
- Added v67 exploration DTO fields and binary codec support for sector mask bytes/words.

Cinematic cheats used:
- Replaced SDF raymarch with discovered-sector point impostors.
- Low tier strides sector words by 4 and emits at most 1 point per word.
- Rendering pivot uses current player macro cell; origin shifts do not move or rewrite data.

Exact microseconds saved:
- PDA hidden: 0 GPU dispatches, effectively full old map dispatch cost removed.
- Visible low tier estimate: 80-250 us saved versus old 3D SDF/raymarch compute path on MX350-class hardware.
- Player slow-tick reveal estimate: under 10 us for one sector OR.
- POI injection estimate: under 20 us for the 64-record capped pass.

Verification:
- `rg` found no `Texture3D`, `_VoxelSdfTexture3D`, `CSRaymarch`, `FindObjectOfType`, or `MapManager.Instance` in the PDA/UI target.
- `git diff --check` found no whitespace errors in touched files.
- Temporary `netstandard2.1` compile of `CartographyGridJobs.cs` with Unity Burst/Collections/Mathematics references passed with 0 warnings and 0 errors.
- Full `dotnet build` is blocked by generated Unity project/package SDK state and unrelated `BootstrapStatus` errors; not fixed because they are outside this prompt and owned by other agents.

Omega polish:
- Removed compute shader `sqrt()` falloff and replaced it with squared-distance attenuation.
- Updated stale SDF/raymarch comments in `PDAMapTab`.
- Re-ran the temp cartography compile after polish: 0 warnings, 0 errors.

## 2026-05-13 - Static Audit Upgrade

What was wrong:
- `RECON_CARTOGRAPHY_UX_LEAD.md` was missing.
- Low-tier map compute still launched the full 32768-word thread range and discarded 75% by branch.
- Scanner reveal radius was bounded by queue count but not by radius.
- Empty cartography save payloads could still enter the unsafe native pointer copy block.
- The recursive sonar sweep polish was not implemented.

What was done:
- Added `Docs/AgentLogs/RECON_CARTOGRAPHY_UX_LEAD.md`.
- Changed low-tier dispatch to launch `ceil(wordCount / wordStride)` and changed `Hecton_MapMesh.compute` to derive `wordIndex = dispatchThreadId.x * wordStride`.
- Added finite radius clamp with `MaxRevealRadiusMeters = 250f` in both managed signal handling and Burst reveal job.
- Guarded cartography save memcpy behind `byteCount > 0 && _discoveredSectors.IsCreated`.
- Added `_Time.y` driven sweep-line brightness to `Hecton_PDA_SonarPointCloud.shader`.

Cinematic cheats used:
- Bounded reveal truth to 250m instead of accepting full-radius acoustic truth from every event.
- Sweep is a shader-side moving line, not a CPU-generated radar geometry pass.

Exact microseconds saved:
- Low tier visible PDA launches 8192 word threads instead of 32768: estimated 10-30 us saved on MX350 driver/GPU scheduling.
- Radius clamp bounds scanner reveal to 1331 cell tests per signal: prevents millisecond-class SlowTick spikes from malformed pings.
- Save guard: 0 frame us, removes an unsafe empty-payload path.

Verification:
- User forbade `dotnet build`; no dotnet build was launched in this pass.
- Static purge scan reports no banned PDA cartography tokens.
- Hot-path scan reports only existing `rsqrt` normalization paths.
- `git diff --check` reports CRLF normalization warnings only.

## 2026-05-13 - Strict Purge Recheck

What was wrong:
- The 128^3 sector encoder rejected AUP macro cells outside the original origin window, so discovery could stop after long travel.
- `PDAMapTab` still contained dead headless texture-job code, a legacy material fallback, raymarch naming, and an unused `_VoxelCellSize` upload lane.
- The prior batch prompt dump path is now absent; `Docs/Tasks/CURRENT_BATCH.md` also does not contain this agent tag.

What was done:
- Wrapped macro-axis encoding into the fixed sector page with `WrapMacroAxisToLocal()`.
- Resolved wrapped GPU decode cells to the nearest current player macro-cell page in `Hecton_MapMesh.compute`.
- Removed the stale PDA texture job/fallback material path and renamed the active compute lane to `BuildMapPoints`.
- Reduced the compute constant buffer from 96B to 80B by deleting the unused voxel-size vector.
- Cleaned mojibake comments introduced by the mechanical block deletion back to ASCII.

Cinematic cheats used:
- Fixed-size toroidal sector page instead of an unbounded world hash map.
- GPU point impostors remain the only map visualization path.

Exact microseconds saved:
- Origin shift: 0 us data migration, renderer resolves nearest wrapped page.
- Legacy fallback purge: removes cold `Texture2D`, native pixel buffer, and material setup risk; frame savings are path-prevention, not a new hot-path delta.
- Constant buffer: 16B less per map constants upload.

Verification:
- User forbade `dotnet build`; no dotnet build was launched.
- Target purge scan reports no `Texture3D`, `_VoxelSdfTexture3D`, `CSRaymarch`, `DrawMeshInstancedIndirect`, `Raymarch`, `SDF`, `VoxelCellSize`, or legacy fallback tokens in `PDAMapTab.cs`, `Hecton_MapMesh.compute`, or cartography jobs.
- Banned UI map scan reports no `Texture3D`, `_VoxelSdfTexture3D`, `CSRaymarch`, `Hecton_SonarMap.compute`, `DrawMeshInstancedIndirect`, `MapManager.Instance`, or `FindObjectOfType` in the PDA map target scope.
- Hot-path scan reports only existing `math.rsqrt` / shader `rsqrt` normalization paths.
- `git diff --check` on touched files reports CRLF normalization warnings only.

## 2026-05-13 - Service Cache And Dirty Flag

What was wrong:
- Visible PDA point-cloud rendering resolved player/exploration services redundantly.
- Predator, marker, audio, and world-seed service reads were repeated instead of cached behind local liveness guards.
- Duplicate reveal signals bumped `_cartographyRevision` even when no new sector bit changed, causing avoidable GPU uploads.
- Touched `COLD ALLOC` comments used ASCII hyphen separators after mojibake cleanup instead of the repo-mandated format.

What was done:
- Cached PDA map service references in `PDAMapTab` and cleared them on disable/destroy.
- Passed the already resolved player AUP into `DispatchSonarPointCloud`.
- Added `_cartographyChangeScratch` and wired reveal/POI jobs to set it only on real bit flips.
- Restored allocation-comment separators in touched files.

Cinematic cheats used:
- No new truth simulation. The fixed 1-bit page remains authoritative; service caching and dirty flags only remove waste around it.

Exact microseconds saved:
- Visible PDA registry/context lookup reduction: estimated 1-5 us/frame.
- Duplicate reveal slow tick: avoids 262 KB sector-buffer upload when no bit changes.
- Comment hygiene: 0 us.

Verification:
- User forbade `dotnet build`; no dotnet build was launched.
- Target purge scan clean.
- Banned UI map scan clean.
- Hot-path scan only reports existing `rsqrt` normalization paths.
- `git diff --check` on touched files reports CRLF normalization warnings only.

## 2026-05-13 - Final Static Recheck

What was wrong:
- No new runtime defect found in the final pass. Remaining risk is compile/runtime validation, which is intentionally not executed because the user forbade `dotnet build`.

What was done:
- Re-read the changed service-cache, dirty-flag, save, and compute-dispatch paths.
- Rechecked the PDA purge scan, hot-path allocation scan, constant-buffer contract, dirty-bit propagation, and graphics-buffer upload contract.

Cinematic cheats used:
- Kept the single GPU point-impostor path; no texture fallback or CPU point-list path was restored.

Exact microseconds saved:
- No additional code delta in this pass.
- Prior savings remain: 1-5 us/frame from cached service resolution, 10-30 us visible low-tier dispatch reduction, and one avoided 262 KB GPU upload on unchanged reveal ticks.

Verification:
- User forbade `dotnet build`; no dotnet build was launched.
- Target purge scan remains clean.
- `git diff --check` on touched runtime/shader files reports CRLF normalization warnings only.
