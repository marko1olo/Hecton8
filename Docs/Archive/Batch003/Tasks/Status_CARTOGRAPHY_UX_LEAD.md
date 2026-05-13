# CARTOGRAPHY_UX_LEAD Status

Prompt: `CARTOGRAPHY_UX_LEAD`
Role: `UX_ENGINEER`
Domain: `PRESENTATION & UX / Cartography & Fog of War`
Status rule: `PENDING VERIFICATION` until Unity/compiler evidence exists.

## Mandates Selected

- `UI_Data_Streaming_ZeroGC_Optimization.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `GPU_Compute_Kernels_Kernels_Optimization_MX350.txt`
- `REND_GPU_Sovereignty.txt`
- `DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`

## Task Checklist

- [x] 1. SINGLETON ERADICATION | `rg MapManager.Instance` found no UI/runtime dependency to delete. DOD: evidence scan. Rejected: adding replacement singleton. Estimate: 0 us/frame.
- [x] 2. SIGNAL MIGRATION | Added `MapRevealSignal(CartographyAup, RadiusMeters)` and tracker enqueue path. DOD: EventBus listener to native queue. Rejected: direct `Map.Reveal()` calls. Estimate: 1-3 us/enqueue.
- [x] 3. ASMDEF ISOLATION | Added `Hecton8.Cartography.asmdef` with only contract/project-neutral Unity package refs. DOD: separate data/job assembly. Rejected: UI-owned jobs. Estimate: 0 us/frame.
- [x] 4. OBJECT HUNT | `PDAMapTab` has no `Texture3D`; UI scan still shows unrelated HUD `MeshFilter` and icon-scale `List<Vector3>`. DOD: offender purged. Rejected: hiding old SDF branch. Estimate: 80-250 us visible-PDA save.
- [x] 5. 1-BIT MACRO GRID | Added `NativeArray<ulong> _discoveredSectors`, 32768 words, 50m cells. DOD: native persistent mask. Rejected: managed `List<Vector3>`. Estimate: 262 KB fixed.
- [x] 6. AUP TO BITMASK JOB | `SlowTick()` runs `CartographyRevealAupCellJob`; shift is `1UL << (bitIndex & 63)`. DOD: Burst job compile checked in temp project. Rejected: per-frame UI writes. Estimate: <10 us/slow tick.
- [x] 7. SCANNER PING REVEAL | `IAcousticPingEventListener` and `ISonarPingEventListener` enqueue reveal spheres. DOD: decoupled signal lane. Rejected: draining shared global queues. Estimate: radius-bound, capped 16 signals/tick.
- [x] 8. POI INJECTION | PDA markers plus `PersistentWorldRegistry.GetSaveSnapshotArray()` feed capped POI sector reveal. DOD: no new registry dependency. Rejected: new cross-domain POI API. Estimate: <20 us/64 records.
- [x] 9. POINT CLOUD MESHING | Added `Hecton_MapMesh.compute`, iterating sector words and appending visible cells. DOD: GPU append path. Rejected: mesh-per-cell. Estimate: visible radius bounded.
- [x] 10. INDIRECT APPEND | Compute writes `_SonarPointAppendBuffer` and copies append count to indirect args. DOD: append buffer + `GraphicsBuffer.CopyCount`. Rejected: CPU point list. Estimate: 0 GC, GPU-only.
- [x] 11. BRG DRAWING | PDA now calls `Graphics.RenderMeshIndirect`. DOD: indirect draw path. Rejected: `DrawMeshInstancedIndirect` legacy call. Estimate: 1 draw call.
- [x] 12. HEIGHT GRADIENT | Point shader maps local height on Z to deep/high gradient; red pulse retained for predator/hostile signal lane. DOD: shader gradient patch. Rejected: CPU color arrays. Estimate: 0 CPU.
- [x] 13. MMF SAVE DELTA | `ExplorationMapDTO` v67 carries compressed sector byte mask plus words. DOD: binary payload codec extended. Rejected: separate save file. Estimate: RLE-friendly 262 KB max before compression.
- [x] 14. ORIGIN SHIFT SYNC + RECON | Sector bits wrap into the fixed 128^3 page and the renderer resolves wrapped cells nearest to the current player macro cell. Added `Docs/AgentLogs/RECON_CARTOGRAPHY_UX_LEAD.md` for the required UI `Texture3D` purge scan. DOD: no data movement on shift plus evidence artifact. Rejected: unbounded hash-map cartography. Estimate: 0 us on origin shift.
- [x] 15. FRUSTUM CULLING UI | `RenderPointCloud()` exits unless map image is active, PDA tab enabled, and visible to camera. DOD: dispatch gated before compute. Rejected: background GPU refresh. Estimate: 0 GPU when hidden.
- [x] 16. MATH LOD LOW TIER | Low tier strides sector words and disables height colorization; no SDF sampling exists in map compute. DOD: MX350 path avoids 3D fetch. Rejected: balanced SDF fallback. Estimate: 4x fewer word scans.
- [x] 17. ZERO-GC | Static scan: hot paths use persistent native arrays/queues, lock-buffer uploads, and struct jobs; no managed lists in reveal/dispatch. DOD: allocation review. Rejected: CPU point list. Estimate: 0 B/frame after cold init.
- [ ] 18. OMEGA COMPILE CHECK | BLOCKED BY PROJECT GENERATION: isolated cartography job compile passed, but full Unity/dotnet compile is blocked by stale generated csproj/package SDK/unrelated `BootstrapStatus` errors.

## Iteration Log

### Loop 0 - Intake

- Extracted XML prompt from `Docs/Tasks/CURRENT_BATCH.md`.
- Verified no pre-existing status/rationale files for this prompt.
- Domain source read: `Docs/Actual Domains of Project.txt`.

### Loop 1 - Tasks 1-5

- Re-read prompt via CLI after crossing the first 3-task boundary.
- Purged PDA `Texture3D` map path and created isolated cartography job assembly.
- Verification: `rg` shows no `MapManager.Instance`, `Texture3D`, or `FindObjectOfType` in UI; remaining `MeshFilter`/`List<Vector3>` hits are unrelated HUD/icon cache.

### Loop 2 - Tasks 6-10

- Added slow-tick player cell reveal, ping-radius reveal, POI injection, compute append buffer build.
- Verification: temporary `netstandard2.1` compile of `CartographyGridJobs.cs` passed with 0 warnings/errors.

### Loop 3 - Tasks 11-16

- Swapped PDA draw to `Graphics.RenderMeshIndirect`, patched height gradient, added save payload, and gated compute by active/visible PDA frame.
- Verification: project `dotnet build` is blocked by stale Unity project generation and unrelated `BootstrapStatus`/package SDK errors; no cartography job compile errors in isolated check.

### Loop 4 - Re-Read: Bit Shifts And Banned Objects

- Re-read cartography jobs and shader decode. C# writes use `wordIndex = bitIndex >> 6` and `bitOffset = bitIndex & 63`; HLSL splits 64-bit words into `uint2` and shifts only 0-31 per lane.
- Verification: `rg` shows no `Texture3D`, `_VoxelSdfTexture3D`, `CSRaymarch`, `FindObjectOfType`, or `MapManager.Instance` in the PDA map/UI target.

### Loop 5 - Re-Read: GC And Compile Wall

- Re-read hot paths for `new`, managed lists, LINQ, and SetData. New allocations are cold init or exceptional dump only; dispatch uses `GraphicsBufferUploadUtility.UploadNativeArray`.
- Verification: `git diff --check` found no whitespace errors; temporary `cartography_check.csproj` build passed. Full project compile remains blocked by generated-project dependencies outside this prompt.

### Loop 6 - Omega Polish

- Read `<POLISH_MANDATE id="OMEGA_POLISH">` only after core task completion/block marking.
- Replaced shader `sqrt()` falloff with squared-distance `dot * rcp`.
- Re-ran static purge scan and temp cartography compile: no banned PDA map tokens, `CartographyGridJobs.cs` still compiles 0/0.

### Loop 7 - Static Recheck 2026-05-13

- Re-extracted `CARTOGRAPHY_UX_LEAD` from the Cyrillic-named batch file under `Docs/` because `Docs/Tasks/CURRENT_BATCH.md` no longer contains the tag.
- Added missing `RECON_CARTOGRAPHY_UX_LEAD.md`.
- Patched scanner reveal radius clamp to 250m with finite fallback. DOD: prevents accidental million-cell slow tick. Rejected: trusting event radius. Estimate: worst scanner job bounded to 1331 cells per signal.
- Patched low-tier compute dispatch to launch `wordCount / wordStride` words instead of launching all words and returning 75% of threads. DOD: real MX350 thread-count reduction. Rejected: branch-only stride. Estimate: 3/4 fewer low-tier map-build threads.
- Patched cartography save copy to skip native pointer fetch when the sector byte payload is empty. DOD: null/empty save guard. Rejected: relying on initialization side effects. Estimate: 0 runtime us, lower save failure risk.
- Added `_Time.y` sonar sweep line in the point shader. DOD: prompt polish visual, no texture sample, no trig. Rejected: CPU animation buffer. Estimate: <1 us GPU ALU.
- Verification: no dotnet build launched per user instruction. Static purge scan clean; hot-path scan only reports existing `rsqrt` uses; `git diff --check` reports CRLF warnings only.

### Loop 8 - Strict Purge Recheck 2026-05-13

- Re-extraction note: current `Docs/Tasks/CURRENT_BATCH.md` and the prior Cyrillic prompt dump no longer contain the `CARTOGRAPHY_UX_LEAD` XML block; status/rationale remain the persistent assignment source.
- Patched 128^3 sector encoding to wrap macro axes through `WrapMacroAxisToLocal()` instead of rejecting cells outside the origin page. DOD: player reveal no longer stops after crossing the original +/-64 macro-cell window. Rejected: unbounded native hash map. Estimate: 0 extra frame us.
- Patched `Hecton_MapMesh.compute` to resolve wrapped cells to the nearest player page before PDA local projection. DOD: point cloud remains centered after AUP/floating-origin shifts. Rejected: CPU-side rehash on origin shift. Estimate: 0 origin-shift us.
- Deleted the stale `PDAMapTab` headless texture job, legacy material fallback, raymarch names, and unused `_VoxelCellSize` constant-buffer lane. DOD: target scan reports no old SDF/raymarch/Texture3D tokens. Rejected: hidden fallback branch. Estimate: removes cold `Texture2D`/native pixel buffers and avoids future branch resurrection.
- Verification: no dotnet build launched. Target purge scan clean; banned UI map scan clean; hot-path scan only reports existing `rsqrt` normalization paths; `git diff --check` on touched files reports CRLF warnings only.

### Loop 9 - Service Cache And Dirty-Flag Recheck 2026-05-13

- Cached PDA map service references behind liveness guards and passed the already resolved player AUP into the render dispatch. DOD: visible PDA frame avoids duplicate player/context and registry lookups. Rejected: repeated `GlobalRegistry` reads inside the point-cloud dispatch. Estimate: 1-5 us/frame depending on service path.
- Added `NativeArray<int>[1] _cartographyChangeScratch` and wired reveal/POI jobs to set it only when a sector word actually changes. DOD: duplicate sonar/acoustic/POI reveals no longer bump `_cartographyRevision`. Rejected: unconditional revision increments per processed signal. Estimate: avoids 262 KB GPU upload on unchanged reveal slow ticks.
- Restored local `COLD ALLOC` comment separator format to the repo-mandated em dash after mojibake cleanup. DOD: static scan reports no mojibake or hyphenated `COLD ALLOC` variants in touched files. Rejected: keeping near-miss allocation comments.
- Verification: no dotnet build launched. Target purge scan clean; banned UI map scan clean; hot-path scan only reports existing `rsqrt` normalization paths; `git diff --check` on touched files reports CRLF warnings only.
