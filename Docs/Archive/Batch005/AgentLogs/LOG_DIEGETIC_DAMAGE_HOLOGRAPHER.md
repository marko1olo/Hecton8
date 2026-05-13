# LOG - DIEGETIC_DAMAGE_HOLOGRAPHER

## 2026-05-13 - Cockpit Wireframe Damage UI

What was wrong:
- Cockpit had no diegetic visibility into exterior `_HectonHullDents`; player could be blind to hull damage while inside the sub.
- A Canvas/submarine-health overlay path would violate the prompt and reintroduce UI rebuild/marker churn.
- There was no LOD3 submarine proxy asset found under Assets/_Project, so the hologram could not truthfully bind to a real art proxy yet.
- Existing cockpit blackbox telemetry did not carry hologram point count/flood/flicker state.

What was done:
- Extended `VehicleSubOsCockpitRuntime` instead of creating a singleton or extra manager.
- Added `Hecton_DamageHologram.compute` to scan capped local proxy vertices against `_HectonHullDents[16]`, append `float4(local xyz, severity)`, and tolerate an all-empty dent array.
- Added `Hecton_DamageHologramInstanced.shader` plus `MAT_Damage_Hologram.mat` for additive red/yellow damage cubes, cyan idle scanline, blue flood tint, and impact flicker.
- Added persistent GPU buffers, `GraphicsBuffer.CopyCount` into indirect args, and `RenderMeshIndirect` in the cockpit render lane.
- Added low-tier MX350 fallback: no compute dispatch, fixed seven-point warning glyph in the same persistent point buffer.
- Added room flood upload through `IHabitatGraphService.RoomWaterLevels`, sequence-gated to avoid per-frame room scans.
- Added hologram telemetry fields to the fixed 300-frame cockpit ring and mirror dump `Dump_DIEGETIC_DAMAGE_HOLOGRAPHER.bin`.
- Added `Hecton8.UI.Diegetic` and `Hecton8.UI.Diegetic.Contracts` asmdef island plus a minimal read-model contract/anchor.
- Ran OMEGA polish: replaced shader divides with `rcp()` multiplication and C# flicker division with `DamageHologramFlickerSecondsInv`.

Cinematic cheats used:
- Local-space proxy point cloud instead of exterior hull rendering or physical simulation.
- Triangle-wave cyan scanline for "system alive" feedback when no dents exist.
- Packed dent radius/depth already supplied by hull shader path, unpacked in compute without CPU geometry truth.
- Flood tint maps room water levels onto proxy X buckets instead of solving real compartment geometry.
- Low-tier warning glyph is seven cubes, not a dynamic diagnostic mesh.
- Impact interference is deterministic hash noise for 0.5 seconds, not a simulation of projector damage.

Exact microseconds saved:
- Exact measured microseconds saved: BLOCKED. Unity profiler/console evidence unavailable because Unity MCP `read_console` reports session not ready.
- Estimate ledger for CTO triage: CPU marker/GameObject path avoided 600 us; CPU per-vertex dent scan avoided 160 us; CPU readback/mesh rebuild avoided 260 us; per-room scene lookup avoided 90 us; coroutine/Animator impact flicker avoided 40 us; OMEGA divide-to-rcp polish under 5 us. Total estimated saved budget: 1,155 us on i3/MX350-class hardware. These are engineering estimates, not profiler measurements.

Verification:
- `mcp__unityMCP__.validate_script` on `Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs`: 0 errors, 0 warnings after final polish.
- `dotnet build Hecton8.Core.csproj`: FAILED with unrelated missing namespaces/types across other agents, including `Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, `Hecton8.Core.Memory.Layout`, `Hecton8.Physics.CCD`, `Hecton8.Audio.Propagation`, `MacroSwarm`, `BrineLayerSample`, `IGroundRadarService`.
- Unity shader import/console verification: PENDING. `refresh_unity` timed out once; `read_console` failed because Unity session was not ready.
- `git diff --check` on owned modified tracked files: only LF-to-CRLF warnings.

Final Git Diff:
- Modified tracked files:
  - `Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs` (+590/-1 tracked diff at time of report)
  - `Docs/Tasks/Status_DIEGETIC_DAMAGE_HOLOGRAPHER.md`
  - `Docs/AgentLogs/Rationale_DIEGETIC_DAMAGE_HOLOGRAPHER.md`
- New files:
  - `Assets/_Project/Art/Shaders/Hecton_DamageHologram.compute`
  - `Assets/_Project/Art/Shaders/Hecton_DamageHologram.compute.meta`
  - `Assets/_Project/Art/Shaders/Hecton_DamageHologramInstanced.shader`
  - `Assets/_Project/Art/Shaders/Hecton_DamageHologramInstanced.shader.meta`
  - `Assets/_Project/Art/Materials/MAT_Damage_Hologram.mat`
  - `Assets/_Project/Art/Materials/MAT_Damage_Hologram.mat.meta`
  - `Assets/_Project/Scripts/UI/Diegetic/DiegeticAssemblyAnchor.cs`
  - `Assets/_Project/Scripts/UI/Diegetic/DiegeticAssemblyAnchor.cs.meta`
  - `Assets/_Project/Scripts/UI/Diegetic/Hecton8.UI.Diegetic.asmdef`
  - `Assets/_Project/Scripts/UI/Diegetic/Hecton8.UI.Diegetic.asmdef.meta`
  - `Assets/_Project/Scripts/UI/Diegetic/Contracts/DamageHologramContracts.cs`
  - `Assets/_Project/Scripts/UI/Diegetic/Contracts/DamageHologramContracts.cs.meta`
  - `Assets/_Project/Scripts/UI/Diegetic/Contracts/Hecton8.UI.Diegetic.Contracts.asmdef`
  - `Assets/_Project/Scripts/UI/Diegetic/Contracts/Hecton8.UI.Diegetic.Contracts.asmdef.meta`
  - `Assets/_Project/Scripts/UI/Diegetic.meta`
  - `Assets/_Project/Scripts/UI/Diegetic/Contracts.meta`
  - `Docs/AgentLogs/LOG_DIEGETIC_DAMAGE_HOLOGRAPHER.md`

Status:
- PENDING VERIFICATION. Owned C# syntax is clean. Full Unity compile/shader import is blocked by active global dependency failures and a non-ready Unity console session.

## 2026-05-13 - Continuation Recheck

What was wrong:
- New C# meta files for the diegetic assembly were incomplete and lacked `MonoImporter`.
- Staged room-water data could remain CPU-side if `_damageRoomWaterBuffer` was created after the latest flood sequence update.
- `FindKernel("KMapHullDents")` could throw on a broken/stale compute import instead of failing closed.
- Damage draw call used `RenderMeshIndirect`; functional in Unity 6, but not the prompt-explicit `Graphics.DrawMeshInstancedIndirect`.

What was done:
- Added `MonoImporter` blocks to `DiegeticAssemblyAnchor.cs.meta` and `DamageHologramContracts.cs.meta`.
- Added immediate upload of `_damageRoomWaterUpload` when `_damageRoomWaterBuffer` is created.
- Added `DamageHologramKernelName` and guarded kernel resolution with `ComputeShader.HasKernel`.
- Switched the damage hologram point draw to `Graphics.DrawMeshInstancedIndirect`.
- Cached damage indirect args writes by mesh and instance count so compute mode no longer rewrites the args header every frame before `GraphicsBuffer.CopyCount`.
- Replaced compute shader reciprocal divisions with literal constants.

Cinematic Cheats used:
- No new simulation. Kept the same proxy point cloud, scanline fake, flood-bucket tint, and low-tier seven-point glyph.

Exact Microseconds saved:
- Measured: still blocked by global compile/profiler availability.
- Estimate: 5-25 us CPU submission saved on stable compute frames by avoiding redundant args-header locks; room-water fix adds one cold 32-float upload and prevents stale visual state. Kernel guard is cold only.

Verification:
- `mcp__unityMCP__.validate_script Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs`: 0 errors, 0 warnings.
- Unity console: 9 current errors, all in `Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs` for missing `_workerThread` / `WorkerShutdownJoinMilliseconds`.
- Console filters for `VehicleSubOsCockpitRuntime` and `Diegetic`: 0 entries. `Hecton_DamageHologram` filter was intermittently blocked by Unity MCP readiness.
- Scoped shader scan for `sqrt`, `normalize`, and ` / ` in damage hologram compute/shader: 0 entries after literal reciprocal patch.

## 2026-05-13 - Assembly Coupling Trim

What was wrong:
- `Hecton8.UI.Diegetic.asmdef` had an unused `Hecton8.Core.Contracts` reference.

What was done:
- Removed the unused Core.Contracts reference. The diegetic assembly now references only `Hecton8.UI.Diegetic.Contracts`.

Cinematic Cheats used:
- None. This is compile graph hygiene.

Exact Microseconds saved:
- Runtime: 0 us. Compile graph reduction is unmeasured.

Verification:
- Asmdef JSON remains valid by inspection.
- Current Unity console sample is blocked by unrelated `WorldChunkResidencyManager` interface mismatch and MCP transport warnings/errors.

## 2026-05-13 - Sparse Proxy Visual Safety

What was wrong:
- `Hecton_DamageHologram.compute` treated "any active dent exists" as a reason to suppress idle scanline output. On sparse fallback/LOD3 proxy data, an active dent can miss every proxy vertex and produce an empty hologram.

What was done:
- Removed the `hasDamage` branch. Damage matches still append red/yellow severity points, and unmatched vertices can still append the cyan diagnostic scanline.
- Replaced owned damage-path reciprocal divisions in `VehicleSubOsCockpitRuntime` with literal constants: flicker inverse, button travel inverse, and 24-bit hash inverse.

Cinematic Cheats used:
- Persistent cyan scanline remains a dashboard projector diagnostic fake. It preserves visual life even when sparse proxy geometry undersamples the real dent field.

Exact Microseconds saved:
- Measured: blocked by unrelated global compile/profiler failures.
- Estimate: neutral to slightly faster shader path by removing one branch. The real gain is preventing false-empty hologram states without increasing the 512-point cap.

Verification:
- Prompt re-extracted from `CURRENT_BATCH.md`.
- Scoped owned scan found no damage-shader `hasDamage`, `sqrt`, `normalize`, CPU readback, Canvas, GameObject marker, or `Instantiate` path.
- Unity MCP `validate_script` on `VehicleSubOsCockpitRuntime.cs`: 0 errors, 0 warnings.
- Console filters for `VehicleSubOsCockpitRuntime` and `Hecton_DamageHologram`: 0 entries.
- Current unfiltered Unity errors are unrelated: `H8MacroDatabaseService.ReadRootNodeOffsetIfOpen` duplicate member, `GlobalDataVault.ElapsedMillisecondsSince` missing helper, and shader errors in `Hecton_OrbitalDropReentryPlasma.shader` / `HectonVisorUberPost.shader`.
- `git diff --check` on owned changed tracked files reports only existing LF-to-CRLF warnings.

## 2026-05-13 - Cockpit Asset Owner Check

What was wrong:
- No prefab or scene entry under `Assets` currently serializes `VehicleSubOsCockpitRuntime`, so the new compute/material fields are not yet proven build-included by a real cockpit owner.

What was done:
- Searched `Assets` for `VehicleSubOsCockpitRuntime`, `damageHologramCompute`, `damageHologramMaterial`, and `damageProxyMeshLod3`.
- Documented the integration requirement instead of creating a fake cockpit prefab outside an authoritative scene/layout owner.

Cinematic Cheats used:
- None. This is build inclusion and ownership verification.

Exact Microseconds saved:
- Runtime: 0 us. Avoids a future dead-on-arrival player build path where editor-only `AssetDatabase` references mask missing serialized assets.

Verification:
- Search found only script references; no prefab/scene serialization exists for the runtime owner.

## 2026-05-13 - Hologram Shader Color Discipline

What was wrong:
- The damage hologram shader double-applied alpha under `Blend SrcAlpha One`.
- Computed cyan scanline and deep-blue flood tint were multiplied by red `_BaseColor.rgb`, making them dim and semantically wrong.
- Cube normals used Unity object-space normal transform while positions used `_HectonDamageHologramLocalToWorld`, so rim response could drift from the dashboard anchor.

What was done:
- Output computed hologram RGB directly and use `_BaseColor.a` only as the material alpha scalar.
- Kept `Blend SrcAlpha One` and removed premultiplied RGB output.
- Transformed normals through `_HectonDamageHologramLocalToWorld`.

Cinematic Cheats used:
- Stronger scanline/flood readability from corrected color math, not extra particles or higher point count.

Exact Microseconds saved:
- Measured: not available while global compile has unrelated blockers.
- Estimate: neutral; one RGB multiply removed, one normal matrix path corrected. Visual gain costs 0 extra draw calls and 0 extra buffers.

Verification:
- Scoped owned scan found no damage-shader `sqrt`, `normalize`, `hasDamage`, CPU readback, Canvas, GameObject marker, or `Instantiate` path.

## 2026-05-13 - Low Tier Warning Truthfulness

What was wrong:
- MX350 fallback always showed the red/yellow warning glyph while powered, even with no known damage/flood/impact state.

What was done:
- Added low-tier state tracking for the existing seven-point upload.
- Clean state now uploads a cyan idle diagnostic glyph.
- Warning state uploads the red/yellow exclamation glyph only when active dents, flood, or impact flicker are known.

Cinematic Cheats used:
- Same seven points, two semantic layouts. No compute, no Canvas, no extra buffers.

Exact Microseconds saved:
- Measured: unavailable due unrelated global compile blockers.
- Estimate: GPU remains seven instances; state check cost is below 1 us CPU. Avoids spending UX attention on a false alarm without increasing frame cost.

Verification:
- Unity MCP `validate_script` on `VehicleSubOsCockpitRuntime.cs`: 0 errors, 0 warnings.
- Console filters for `VehicleSubOsCockpitRuntime` and `Hecton_DamageHologram`: 0 entries.
- Current unfiltered Unity blocker is unrelated `HectonUnderwaterVisuals` interface mismatch plus entry-point errors.
- Scoped owned scan found no allocation/search/readback/Canvas marker pattern in the hologram files.

## 2026-05-13 - Low Tier Blackbox Disambiguation

What was wrong:
- `HoloFlags` did not distinguish MX350 idle diagnostic glyph from active MX350 warning glyph after the low-tier path became state-aware.

What was done:
- Added HoloFlags bit 32 for active low-tier warning state.

Cinematic Cheats used:
- None. This is postmortem clarity for the fixed seven-point presentation fake.

Exact Microseconds saved:
- Runtime cost: one boolean check during telemetry record, below 1 us estimate. No GPU or VRAM change.

Verification:
- Prompt re-extracted from `CURRENT_BATCH.md`.
- Unity MCP `validate_script` on `VehicleSubOsCockpitRuntime.cs`: 0 errors, 0 warnings.
- Console filters for `VehicleSubOsCockpitRuntime` and `Hecton_DamageHologram`: 0 entries.
- `git diff --check` reports only existing LF-to-CRLF warnings.

## 2026-05-13 - Status Evidence Correction

What was wrong:
- Status entries still marked owned compute shader import as pending, even though Unity console filters for `Hecton_DamageHologram` now return 0 entries.

What was done:
- Updated Tasks 6 and 19 to record filtered shader import evidence while keeping global status `PENDING VERIFICATION` for scene ownership and unrelated compile blockers.

Cinematic Cheats used:
- None. Evidence hygiene only.

Exact Microseconds saved:
- Runtime: 0 us.

Verification:
- Previous Unity console filter for `Hecton_DamageHologram`: 0 entries after refresh/import.

## 2026-05-13 - Editor Wiring And Contract Surface

What was wrong:
- Future cockpit prefab owners had to rely on manual asset assignment or editor play-mode lookup for the damage hologram compute/material.
- `IDiegeticDamageHologramReadModel` existed but no runtime implemented it.

What was done:
- Added cold `Reset`/`OnValidate` asset auto-resolution for the owned compute/material references.
- Implemented `IDiegeticDamageHologramReadModel` on `VehicleSubOsCockpitRuntime`.
- Exposed read-only hologram points, proxy count, flood scalar, and flags through the contract.

Cinematic Cheats used:
- None. This is integration hardening and contract hygiene.

Exact Microseconds saved:
- Runtime auto-wiring: 0 us, editor-only.
- Contract query: below 1 us estimate; direct field reads plus one flag mask, no allocation.

Verification:
- Unity MCP `validate_script` on `VehicleSubOsCockpitRuntime.cs`: 0 errors, 0 warnings.
- Later Unity refresh timed out twice, then console/validation commands failed due MCP ping/disconnect instability.
- `git diff --check` reports only existing LF-to-CRLF warnings.
