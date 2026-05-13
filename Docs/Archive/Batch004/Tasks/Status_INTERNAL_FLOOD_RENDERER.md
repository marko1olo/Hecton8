# INTERNAL_FLOOD_RENDERER Status

Prompt: `INTERNAL_FLOOD_RENDERER`
Role: `HABITAT_ARCHITECT`
Domain: `ECHELON 6: HABITAT & VEHICLES`
Status: PENDING VERIFICATION

## Mandates Read

- `PHYS_Fluid_Incursion_Interior.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## Checklist

- [x] 1. SINGLETON ERADICATION: `rg` found no `FloodVfxManager.Instance`; DOD practice = infection scan before edits; rejected singleton bridge; estimate 0.0us current-frame saved, prevents future hard dependency.
- [x] 2. SIGNAL MIGRATION: `InternalFloodWaterlineRuntime.FastTick` reads `GlobalRegistry.HabitatGraph` and `IHabitatGraphService.RoomWaterLevels`; DOD practice = interface-owned habitat read model; rejected concrete `HabitatGraphManager` scene search; estimate 4-8us saved versus scan.
- [x] 3. ASMDEF ISOLATION: No `Hecton8.Habitat.VFX` asmdef exists; waterline readback stays in Core contracts through `IHabitatGraphService`; DOD practice = use existing contract spine; rejected fake asmdef churn; estimate 0.0us runtime, reduced compile boundary risk.
- [x] 4. DEAD CODE HUNT: `rg` found no `Instantiate(WaterMeshPrefab)` or `WaterMeshPrefab`; DOD practice = purge by static scan; rejected internal water mesh spawning; estimate 35-120us plus draw/GC avoided per room event.
- [x] 5. LOCAL HEIGHT CALCULATION: `FastTick` resolves player predicted AUP, cached RoomID, and habitat fill snapshot; DOD practice = cached room then bounded fallback loop; rejected per-frame object lookup; estimate 4-12us saved in populated bases.
- [x] 6. CAMERA SPLIT: runtime compares camera runtime/AUP Y to room water surface Y from bounds + fill; DOD practice = scalar split, no water simulation; rejected physical plane collision; estimate 20-80us saved versus interior water actors.
- [x] 7. SHADER UPLOAD: runtime pushes `_InternalWaterlineY`, `_InternalWaterColor`, `_InternalWaterlineRuntime`, `_InternalWaterlineDistortion`; DOD practice = global scalar/vector upload; rejected per-renderer material mutation; estimate 5-20us saved.
- [x] 8. POST PROCESS: `HectonVisorUberPostFeature` computes split from camera pitch and `_InternalWaterlineY`; DOD practice = existing Uber-Post pass only; rejected second fullscreen pass; estimate 180-450us saved on MX350.
- [x] 9. UNDERWATER DISTORTION: `HectonVisorUberPost.shader` tints below split and conditionally refracts; DOD practice = single extra sample only when refraction active; rejected spawned water volume shader stack; estimate 40-160us saved.
- [x] 10. WATER DROPLETS: below-to-above crossing sets a 2.0s droplet scalar; shader uses procedural hash noise; DOD practice = timed state machine; rejected particles/coroutines; estimate 12-35us saved per transition.
- [x] 11. O2 BUBBLES: subscribed exhale emits `DebrisSpawnSignal` while submerged; DOD practice = existing NativeQueue signal lane; rejected managed event broadcast/string VFX event; estimate 5-15us saved and 0 B hot path.
- [x] 12. AUP SHIFT SAFETY: implements `IOriginShiftListener` and subtracts `ShiftOffset.y` from cached waterline state; DOD practice = synchronous rebase; rejected waiting for next room query; estimate correctness fix, 0.0us target.
- [x] 13. MATH LOD: low/MX350/unknown tier sets refraction strength to 0 and shader branches around second scene sample; DOD practice = tier gate with visual tint fallback; rejected balanced middle path; estimate 90-220us saved on low tier.
- [x] 14. ZERO-GC: FastTick uses structs, cached fields, static shader IDs, native telemetry; DOD practice = no LINQ/alloc collections/coroutines; rejected per-frame search and droplets objects; estimate 0 B/frame static proof, profiler proof pending.
- [x] 15. BLACKBOX DUMP: fixed `NativeArray<WaterlineTelemetryEntry>[300]` stores waterline/fill/camera/droplet/hash and dumps on non-finite; DOD practice = circular buffer; rejected "last log line" diagnosis; estimate 0.0us nominal, crash diagnosis retained.
- [x] 16. EVENT BUS: camera crossing emits `AcousticPingSignal(WaterSplash)` through `GlobalSignals`; DOD practice = typed NativeQueue signal; rejected `AudioSource.PlayOneShot`; estimate 20-60us saved and no audio object churn.
- [x] 17. CROSS-DOMAIN AUDIT: `IGasDynamicsSolver.TrySetRoomSubmergedFraction` feeds gas job dry fraction so submerged room portion caps O2; DOD practice = scalar cross-domain contract; rejected gas solver concrete reference; estimate 0.5-2us/room.
- [x] 18. TRANSITION LERP: `_currentWaterlineY` lerps toward `_targetWaterlineY` over 0.22s when moving between partially flooded rooms; DOD practice = hysteresis-friendly scalar smoothing; rejected hard pop; estimate 0.0us visual correctness.
- [x] 19. [BLOCKED BY DEPENDENCY] OMEGA COMPILE CHECK: static shader SRP review passes CBUFFER/single-pass requirements; `dotnet build Hecton8.Core.csproj` still fails on 113 unrelated missing type/assembly errors (`Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, `Hecton8.Audio.Propagation`, etc.); Unity validation unavailable (`no_unity_session`).

## Iteration Log

- Init: Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md`; mandates selected; no prior status/rationale files found.
- Loop 1 Tasks 1-5: Performed singleton/dead-code scans, verified contract route, inspected `InternalFloodWaterlineRuntime.FastTick` and habitat graph query path. Compile attempt: full `Assembly-CSharp.csproj` timed out at 120s with no diagnostics.
- Loop 2 Tasks 6-10: Inspected camera split, shader globals, Uber-Post integration, tint/refraction/droplet shader. Fix applied: low tier now skips the refracted scene sample instead of sampling with zero blend.
- Loop 3 Tasks 11-15: Inspected signal structs, exhale subscription, origin shift handler, telemetry layout. Fix applied: telemetry entry header corrected to 40 bytes and origin-shift path uses cached fill instead of shader global readback.
- Loop 4 Tasks 16-19: Inspected `AcousticPingSignal`, gas solver submerged O2 clamp, transition smoothing, shader CBUFFER placement. Generated `.csproj` was stale; local ignored project metadata was updated for diagnostics.
- Loop 5 Self-Review: Re-read this status/rationale, re-extracted the XML prompt, reran `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nodeReuse:false /p:UseSharedCompilation=false`; build remains blocked by unrelated dependencies, not by `InternalFloodWaterlineRuntime` missing type.
- Omega Polish: Replaced added internal-water `sin()` offset waves with `CheapSignedTriangle(frac)` terms; confirmed no added `foreach`, string interpolation, `math.sqrt`, or `math.normalize` in waterline C# hot paths.
- Upgrade Pass 2026-05-13: Re-read status/rationale and prompt; fixed repeated inactive shader global writes, cached habitat/gas/tier dependencies on a 30-tick cadence, added pending gas submerged-fraction retry when the gas job is running, retained 2-second surfacing droplets even after the waterline clears, moved droplets to full-visor coverage, and made high-tier droplets use one transient sample while low tier remains additive-only.

## Verification

- Unity MCP `validate_script`: BLOCKED, `Unity session not available; please retry`.
- `dotnet build Hecton8.Core.csproj`: BLOCKED after upgrade pass, still 113 existing dependency/type errors outside this agent's ownership.
- Static scans: `FloodVfxManager.Instance`, `WaterPlaneManager.Instance`, `WaterMeshPrefab`, and `Instantiate(WaterMeshPrefab)` not present in runtime script scan.
- Scoped `git diff --check`: PASS, CRLF warnings only.
- Final report appended: `Docs/AgentLogs/LOG_INTERNAL_FLOOD_RENDERER.md`.
