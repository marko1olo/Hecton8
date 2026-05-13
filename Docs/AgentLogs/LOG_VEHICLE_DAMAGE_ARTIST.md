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
