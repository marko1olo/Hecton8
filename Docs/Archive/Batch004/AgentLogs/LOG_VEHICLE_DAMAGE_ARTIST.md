# VEHICLE_DAMAGE_ARTIST Log

## 2026-05-13 - Session Start

What was wrong -> Hull damage prompt requires visible shader dents, but current source truth still needed confirmation. No existing status/rationale file existed for this agent.

What was done -> Extracted prompt, read domain authority, read task mandates, and created status/rationale/log files. Implementation not yet started.

Cinematic Cheats used -> Chosen direction is shader deformation and visual darkening; collider and mesh CPU truth rejected.

Exact Microseconds saved -> Projection only: 100-800 us per impact burst compared with CPU mesh deformation/collider rebuild. Measured proof absent. STATUS: PENDING VERIFICATION.

## 2026-05-13 - Shader Hull Dents Execution Report

What was wrong -> Leviathan/submarine damage had gameplay integrity consequences without localized hull trauma. CPU mesh deformation, mesh swapping, and MeshCollider rebake would violate frame budget and allocator/collider discipline. World-space dents would be unsafe under AUP/floating-origin shifts.

What was done -> Verified/finished fixed `Vector4[16]` global hull dent authority with `_HectonHullDents`; xyz is submarine local impact point, w packs radius/depth. Verified `CombatDamageSignal` ingestion, local-space conversion, fixed ring-buffer overwrite, dirty-only `Shader.SetGlobalVectorArray`, and no per-renderer `MaterialPropertyBlock`. Verified `Hecton_CoreLit.hlsl` hull dent path uses `[unroll]` 16-loop and squared-distance `dot(delta, delta)`; no `distance()` call in the dent path. Wired `Hecton_DryZoneLit.shader` surface response to dent shadow and added low-tier texture scar using `_DetailMask` when vertex loop is bypassed. Verified repair fade path reads `ISubmarineHullBreachReadModel` active local breach outputs. Verified `HullDeformedSignal` and `CrashTelemetryBuffer.ReportHullDentState` paths for audio groan hooks and black-box `ActiveHullDents`. Omega polish replaced controller divisions with reciprocal multiplies via `math.rcp`.

Cinematic Cheats used -> Shader-only vertex depression. Pristine physical collider. No normal recalculation. Albedo/smoothness darkening to fake dent shadowing. Packed radius/depth in one float. Low-tier MX350 texture scar instead of vertex dent loop.

Exact Microseconds saved -> Mesh vertex deformation avoided: estimated 100-800 us per impact burst. MeshCollider rebuild avoided: estimated 200 us to multiple milliseconds per impact on dense hull meshes. Per-renderer MPB walk avoided: estimated 40-160 us per 8-renderer impact burst. AUP rebase correction avoided: estimated 5-20 us/frame while dents are active. Repair coupling retained cost: worst-case 16 dents x 64 breaches, estimated 6-20 us only when active. Omega reciprocal cleanup: sub-1 us per accepted impact.

Verification -> Source audits passed for no `MeshCollider`, no `Mesh.vertices`, no LINQ/managed collections in impact path, no `distance()` in dent HLSL path, and fixed `[unroll]` 16-loop. `dotnet build Hecton8.Core.csproj --no-restore` is blocked by existing global project reference failures (`Hecton8.Core.Scheduling`, `Hecton8.Core.Memory.Layout`, audio propagation, physics CCD, fluid/brine contracts). Unity MCP refresh compile timed out twice and console access returned `no_unity_session`.

Status -> Tasks 1-18 complete. Task 19 marked `[BLOCKED BY DEPENDENCY]`. Final state: PENDING VERIFICATION, not VERIFIED MASTER GRADE.

## 2026-05-13 - AAA Recheck Improvement Report

What was wrong -> The first hull dent pass worked as a visual lie, but the recheck found avoidable polish defects: low-tier scar math could become broad uniform darkening, repaired zero-depth slots could make an active-count shader loop miss later live dents, quality-tier refresh dirtied shader globals every 60 frames without a state change, and the shared shader safe-normal path still used `normalize()`.

What was done -> Low-tier vertex deformation now returns zero dent shadow and the fragment scar fetch is gated by both low-tier flag and nonzero scar scalar. The HLSL dent loop now scans all 16 fixed slots and skips zero-depth entries, preserving ring-buffer correctness after repairs. The shared safe-normal path now uses `value * rsqrt(lenSq)` after finite length validation. `HullDentShaderController.RefreshQualityTier` now uploads globals only when the quality byte or low-tier flag changes. Re-ran source audits and narrowed vehicle VFX compilation.

Cinematic Cheats used -> Shader-only vertex depression. Fixed 16-slot impact buffer. Zero-depth slot skip instead of CPU compaction. Low-tier `_DetailMask` scar instead of vertex loop. Albedo/smoothness darkening instead of normal rebuild. Pristine collider remains gameplay truth.

Exact Microseconds saved -> Avoided stable-tier global vector-array uploads once per 60 frames: estimated sub-10 us CPU per skipped refresh plus reduced native shader-property churn. Avoided unconditional low-tier scar texture fetch outside active damage: one texture sample per hull fragment removed on non-damage frames. Repaired-slot fix trades compact active-count cutoff for a fixed 16-slot scan, preserving correctness at a bounded max of 16 dot tests per vertex on high tier. `rsqrt` normalization replaces shader `normalize()` under the existing finite length guard.

Verification -> `dotnet csc` with `Temp/QualityIntegrator/Hecton8.Vehicles.VFX.validation.rsp` and `Hecton8.Core.validation.ref.dll` passed for current `HullDentShaderController.cs`. Source scans show no `distance()`, no `sqrt()`, no `normalize()`, no `activeCount`, and no `i >= activeCount` in the hull dent shader path. Controller audit shows no LINQ, no managed collection allocation, no `MaterialPropertyBlock`, no `MeshCollider`, and no mesh vertex mutation; the lone `GetComponent` is cold `OnEnable` breach-model fallback. Full `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` still fails on unrelated missing assemblies/contracts. Unity MCP `validate_script` returned `no_unity_session`.

Status -> Vehicle VFX narrow compile passed. Global build and Unity editor proof remain blocked, so final state remains PENDING VERIFICATION per prompt.
