# Rationale_1720

## Session Bootstrap
Problem: Agent-local memory files did not exist.
Solution: Created minimal Status_1720.md and Rationale_1720.md before code edits.
Rejected Alternatives: Reusing other agents' logs; violates strict parsing and batch hygiene.
Scalability potential: No runtime impact.
Hardware Impact: 0 us/frame; documentation-only compliance.

## Mandate Selection
Problem: The task spans editor baking, runtime rendering, SDF geometry, and shader hot paths; using a single generic texture mandate would miss ownership constraints.
Solution: Read rendering fog/dithering, URP hot-path, frame budget/VRAM, zero-GC, native memory/jobs, voxel SDF, telemetry, and math determinism mandates plus relevant root bibles.
Rejected Alternatives: Reading only shader docs; would miss runtime SDF ownership and Data Monolith/GlobalQualityWeight rules.
Scalability potential: Low/Mid/High/Ultra route will be encoded as volume resolution, bake cadence, density octave count, and report metadata rather than runtime branches.
Hardware Impact: Expected runtime saving target is removal of per-frame/per-slowtick 3D SDF scan/upload and shader noise ALU; exact us depends on active consumers.

## Runtime Ownership Route
Problem: `HectonCaveVoxelLightingVolume` currently owns a generated runtime `Texture3D` and feeds global shader state used by multiple consumers.
Solution: Build an editor-only baker and add a prebaked asset bridge so the existing global shader contract can read an immutable `Texture3D` asset.
Rejected Alternatives: Rewriting all shader consumers in one pass; too broad and risks cross-domain breakage while other agents are active.
Scalability potential: Toaster uses R8/low resolution baked SDF and low octave fog; mid/high/ultra use higher resolution, mips, richer flow channels, and optional overkill density detail while runtime sampling path stays stable.
Hardware Impact: Low-end i3/MX350 target avoids runtime volume allocation/upload and spatial contact scanning; expected saving is highest during slow tick and texture upload spikes.

## Offline Fog And Flow Baker
Problem: `Hecton_VolumetricFog.compute` evaluates fBm-style density in the fog grid path; that burns ALU in a path already dominated by raymarch cost.
Solution: Added `FogVolumeBakeJob` in `VolumetricTextureBaker.cs`; it writes `NativeArray<Color32>` with R=density, G/B=flow derivative, A=255.
Rejected Alternatives: Separate density and flow Texture3D assets; that doubles/triples 3D fetches and violates packing mandate.
Scalability potential: Low 32^3 with 1-2 octaves, middle 64^3, high 96^3, ultra 128^3 with 4-5 octaves and stronger flow. The runtime shader still does one packed sample.
Hardware Impact: MX350 fragment/raymarch path removes repeated fBm ALU; estimated saving is per ray step, not measured in this session.

## Periodic Noise Model
Problem: Nonperiodic noise causes visible seams when a volume is repeated through large biome space.
Solution: Periodic value-noise wraps lattice coordinates modulo `(resolution-1)*frequency`; voxel 0 and voxel max resolve to the same lattice corner on all axes.
Rejected Alternatives: Unity `Mathf.PerlinNoise` slices; 2D-only, non-Burst, and not guaranteed seamless in 3D.
Scalability potential: Frequency and octave count scale continuously with GlobalQualityWeight without changing texture contract.
Hardware Impact: 0 us/frame runtime; all cost is editor bake.

## SDF Reuse Route
Problem: A second local SDF baker inside the fog tool would duplicate the existing Static Cave SDF Forge and create competing ownership for cave distance fields.
Solution: `VolumetricTextureBaker` delegates SDF Texture3D export to `StaticCaveSdfBakePipeline` encoded UNorm mode. The existing forge owns triangle extraction, SDF jobs, and voxel export.
Rejected Alternatives: Local flat triangle DTOs, local BVH nodes, `MeshCollider.ClosestPoint`, or Physics.Raycast per voxel; all duplicate or violate the data-local bake route.
Scalability potential: Low through ultra tiers choose authored SDF resolution/export settings through the forge without changing runtime shader contracts.
Hardware Impact: Runtime collision/occlusion consumers can sample an immutable texture instead of raycasting high-poly cave meshes; exact us saved requires scene profiler capture.

## SDF Diagnostics Suppression
Problem: 1720 needs SDF Texture3D output from the existing forge without creating extra JSON/self-audit proof files for this agent path.
Solution: Added a `writeDiagnostics` overload on `StaticCaveSdfBakePipeline.BakeMesh`; existing callers keep the default diagnostic route, while `VolumetricTextureBaker` calls the no-report overload.
Rejected Alternatives: Removing reports from the whole Static Forge, duplicating the SDF baker locally, or accepting extra report I/O in the 1720 baker path.
Scalability potential: No runtime impact; editor bake output remains one binary/Texture3D asset route.
Hardware Impact: Removes avoidable editor disk writes from the 1720 path.

## Texture3D Payload Policy
Problem: Fog volumes must not silently ship as large RGBA32 assets, and `Texture3D.SetPixelData` requires raw data layout to match the texture format.
Solution: Fog bake now packs `Color32` density/flow into raw RGB565 `ushort` payload before `SetPixelData`. SDF export uses the existing forge encoded UNorm R8 route because all shader consumers sample `.r`.
Rejected Alternatives: RGBA32 compatibility fallback for fog, fake BC7 upload without a BC7 block encoder, fake BC4 importer claims for Texture3D `.asset`, or hidden mobile VR memory regression.
Scalability potential: Low uses smaller RGB565 fog volumes; middle/high/ultra can author denser RGB565 fog and R8 SDF assets without changing runtime authority.
Hardware Impact: Target 128^3 RGB565 fog is ~4 MB. A prohibited RGBA32 fog fallback would be ~8 MB and remains blocked.

## SDF Channel Contract
Problem: Cave SDF shader consumers read `_HectonCaveVoxelSdfTex` from `.r`; Alpha-only texture payloads can silently violate that contract on backend-specific swizzles.
Solution: Encoded SDF Texture3D export and legacy runtime fallback now use `TextureFormat.R8`. Unsupported R8/Texture3D support fails closed instead of using Alpha8.
Rejected Alternatives: Keeping Alpha8 and hoping red swizzle matches alpha; changing every shader consumer to `.a`; duplicating channel decode branches.
Scalability potential: One R8 sample path remains stable across low through ultra tiers.
Hardware Impact: 128^3 R8 SDF is ~2 MB and preserves the existing `.r` sampling contract.

## Runtime Prebaked Bridge
Problem: Runtime shader consumers already depend on `_HectonCaveVoxelSdfTex`, world-to-local matrix, half-extents, and range globals.
Solution: Added prebaked serialized Texture3D fields to `HectonCaveVoxelLightingVolume`; when assigned, it publishes the same globals and skips scanning/upload. Legacy runtime generation is behind explicit `allowRuntimeGeneratedFallback`.
Rejected Alternatives: Changing every shader and compute consumer; too broad and not needed because the global contract is already centralized.
Scalability potential: Device tier chooses which baked asset to assign; runtime route stays zero-GC and stable.
Hardware Impact: With prebaked asset assigned, runtime SDF generation/upload cost is 0 us/frame. Legacy fallback remains a documented risk if explicitly enabled.

## Compaction And DataVault Safety
Problem: DataVault native handles can become unsafe if rendering jobs read while a compaction fence relocates data.
Solution: The prebaked route reads no DataVault handles. Legacy fallback now stages occupancy slices and encoded SDF bytes outside DataVault write locks; write locks cover copy-only windows and are released through `try/finally`.
Rejected Alternatives: Storing baked Texture3D bytes inside GlobalDataVault; would turn immutable art data into native heap ownership with compaction risk.
Scalability potential: Low through ultra tiers use immutable Unity assets; no compaction pressure added.
Hardware Impact: 0 us/frame DataVault work on prebaked route.

## Verification Gate
Problem: Full `dotnet build` and Unity validation require host conditions that were not available in the correction pass.
Solution: Did not run `dotnet build`; latest CPU sample was 47%, but `VBCSCompiler` was active. Unity MCP `validate_script` and `read_console` failed with transport error to `127.0.0.1:8088/mcp`. Static syntax balance and source scans were used instead.
Rejected Alternatives: Starting a prohibited build or claiming Unity validation without a working MCP transport.
Scalability potential: No runtime impact.
Hardware Impact: Avoided adding compile load to an already saturated host.

## Console Boundary
Problem: Unity console could not be reread after the correction pass because MCP transport was unavailable.
Solution: No new cross-domain fixes were made from stale console data. 1720 verification is limited to static source gates until Unity MCP or Unity import logs are available.
Rejected Alternatives: Editing marker or unrelated systems from stale evidence; violates domain boundary without critical dependency.
Scalability potential: No change.
Hardware Impact: No 1720 runtime impact; project-wide compile status remains pending fresh Unity import/build evidence.

## Current Source Correction
Problem: The previous memory file described an ID-suffixed baker file, local SDF/BVH duplication, JSON reporting, and uncompressed fallback. The current integration no longer uses those routes.
Solution: Keep `Assets/_Project/Editor/Bakers/VolumetricTextureBaker.cs` as the fog/flow Texture3D baker; delegate SDF Texture3D export to `StaticCaveSdfBakePipeline` encoded UNorm R8 mode; pack fog into RGB565 raw payload; bind the baked fog Texture3D through `HectonVolumetricParticulateFogFeature`; publish prebaked cave SDF through `HectonCaveVoxelLightingVolume`.
Rejected Alternatives: Keeping local SDF DTO/BVH/job classes inside the fog baker; emitting RGBA32 fog assets when compact payload is rejected; polling `GlobalRegistry.DataVault` from `AddRenderPasses`; uploading `Texture3D` while holding a DataVault write lock.
Scalability potential: Low uses smaller authored Texture3D assets; middle/high/ultra use denser RGB565 fog volumes and encoded SDF assets without changing runtime authority or DTO layout.
Hardware Impact: Runtime fog no longer evaluates shader fBm; prebaked SDF route removes runtime voxel scan/upload by default. Exact frame-time gain remains pending Unity/Profiler proof.

## Point-Light Lock Flattening
Problem: `TryWriteAndUploadMockLights` executed mock light generation and stale job-finalization logic while holding the fog point-light DataVault write lock.
Solution: Stage point lights into the existing preallocated `PointLightDTO[8]` before acquiring the writer fence; hold the lock only while copying scratch DTOs into the vault buffer; upload the GPU buffer after release.
Rejected Alternatives: Keeping inline `BuildMockVolumetricLightsJob.Execute()` under lock; adding a new managed helper class; scheduling a tiny job and synchronizing in the same frame.
Scalability potential: Low uses fewer active lights through continuous quality count; mid/high/ultra increase authored point-light count without changing lock duration beyond eight direct assignments.
Hardware Impact: Removes lock-held trigonometric approximation work and job-completion branches from the render feature path; exact microseconds require profiler capture.

## Mock-Light Formula Ownership
Problem: Moving point-light staging outside the NativeArray job risked duplicating the volumetric light layout math in the render feature.
Solution: Extended the existing `BuildMockVolumetricLightsJob` with static `ResolveActivePointLightCount` and `BuildPointLight` methods; the job and managed staging use one formula owner.
Rejected Alternatives: Parallel `BuildMockLightDto` helper class; copy-pasted math in `HectonVolumetricParticulateFogFeature`.
Scalability potential: One formula supports 1-8 active lights as a continuous quality-driven count.
Hardware Impact: No allocation; staging remains a fixed eight-element array.

## Telemetry And Profile Lock Scope
Problem: Fog telemetry hashing and default extinction profile construction ran inside DataVault write locks.
Solution: Create `VolumetricFogTelemetryEntry` and default extinction DTO before lock acquisition; locked sections now perform buffer assignment and clear loops only.
Rejected Alternatives: Leaving hash/profile construction under the writer fence because the work is small; that still violates the copy-only lock rule.
Scalability potential: Telemetry capacity remains fixed at 300; quality tiers do not change DTO layout or lock behavior.
Hardware Impact: Removes hash/math work from the writer fence; frame-time delta is small but the deadlock/stall vector is cleaner.

## Cave SDF Upload Readback
Problem: `HectonCaveVoxelLightingVolume.LateFrameTick` used a DataVault write lock to read SDF bytes for `Texture3D` upload.
Solution: Use the existing read-only handle route with compaction fence checks, copy into `_sdfUploadScratch`, and run `SetPixelData/Apply` after the readback.
Rejected Alternatives: Retaining writer lock for readback; uploading directly from a vault-owned NativeArray.
Scalability potential: Prebaked SDF remains zero DataVault work; explicit runtime fallback does not block writers during GPU staging.
Hardware Impact: Avoids writer-fence contention during visual sync upload.

## Build Throttle Update
Problem: Previous status said `dotnet build` was not run, but a later gated build attempt timed out without compiler diagnostics.
Solution: Record the actual state: one build attempt, timeout, orphaned build process stopped, build-server shutdown completed, no compiler processes left; CPU is currently 90%, so no second build was launched.
Rejected Alternatives: Claiming compile success; launching another build on a saturated host.
Scalability potential: No runtime impact.
Hardware Impact: Avoided additional host load and stale compiler processes.

## Volumetric Light Baked Density Route
Problem: `Hecton_VolumetricLight.compute` still evaluated two 3-octave fBm samples inside every god-ray raymarch step after the particulate fog path had moved to baked Texture3D density/flow.
Solution: Removed the local `Hash31`, `ValueNoise3`, and `Fbm3` functions and reused the same packed fog contract: R=density, G/B=flow. `VolumetricLightFeature` now binds an authored Texture3D plus center/world-size/flow-weight vectors.
Rejected Alternatives: Keeping a cheaper single-octave noise fallback; creating a second volumetric light volume owner; allocating RTHandles from `RecordRenderGraph` every frame.
Scalability potential: Low devices can leave the baked volume absent and get constant density at half/quarter res. Middle/high/ultra devices assign denser RGB565 volumes and increase flow weight without changing shader structure.
Hardware Impact: Removes runtime value-noise ALU from each raymarch step. Replacement cost is one Texture3D sample plus an optional second sample when flow detail is enabled.

## Player Runtime SDF Fallback Closure
Problem: `HectonCaveVoxelLightingVolume` still retained an explicit local runtime SDF allocation/generation path. Even disabled by default, it was too easy to re-enable in player builds and recreate the runtime `Texture3D`/DataVault scan path that 1720 is supposed to eliminate.
Solution: Non-editor builds now fail closed unless a prebaked SDF Texture3D is assigned. `EnsureResourcesCold` publishes inactive globals and unregisters before DataVault handles or Texture3D allocation; `HasRuntimeTickWork` returns false outside editor.
Rejected Alternatives: Deleting the editor/debug local scanner immediately; leaving the player fallback disabled by convention only; moving the fallback to another manager.
Scalability potential: All shipped tiers use immutable R8 SDF assets. Editor-only fallback remains available for diagnosis and scene authoring, not runtime gameplay.
Hardware Impact: Player path removes the local SDF texture allocation, spatial contact scan, DataVault volume writes, and GPU upload branch when no prebaked SDF is authored.

## Current Verification Boundary
Problem: The host was under saturated CPU load during the continuation pass.
Solution: Used static source gates only: forbidden-token scans, shader noise scans, bracket/preprocessor balance, and `git diff --check`. No build was launched with CPU at 100%.
Rejected Alternatives: Spamming `dotnet build`; claiming compile success; hiding the missing compiler proof.
Scalability potential: No runtime impact.
Hardware Impact: Avoided adding compile load to a saturated workstation.
