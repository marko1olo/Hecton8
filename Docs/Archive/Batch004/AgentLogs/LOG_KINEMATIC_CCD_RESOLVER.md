# KINEMATIC_CCD_RESOLVER Log

## Session Start

What was wrong: prompt identified high-speed tunneling in kinematic movement at 30 m/s through 0.5 m geometry.
What was done: extracted KINEMATIC_CCD_RESOLVER from CURRENT_BATCH.md; read domain map and task-relevant mandates.
Cinematic Cheats used: impact consequences will prefer event-driven sparks, haptics, camera bias, and audio instead of extra physical simulation.
Exact Microseconds saved: PENDING VERIFICATION; no profiler run yet.

## Session Close - High-G Collision Deflection

What was wrong: kinematic/manual movement paths could apply MovePosition after a discrete tick without a continuous sweep, so player KCC, submarine movement, and Leviathan lunge presentation could tunnel through thin static/voxel geometry at high speed.

What was done: added/verified the isolated Hecton8.Physics.CCD math kernel; routed high-speed movement through deferred CapsulecastCommand where the player/vehicle buffers support it; added hit-fraction rollback, dot-plane slide, low-tier stop-on-hit, two-contact corner halt, kinetic-energy loss, impact/debris/haptic/camera/damage signal emission, Leviathan lunge CCD guard, and blackbox CcdInterventions telemetry.

Cinematic Cheats used: synthetic two-contact corner halt instead of recursive bounce simulation; event-driven sparks/haptics/camera bias instead of direct VFX/device calls; low-tier stop-on-hit instead of slide; rsqrt scalar magnitude instead of sqrt; consequence fan-out through fixed native payloads so high-end visual overkill can be layered downstream.

Exact Microseconds saved: estimates only, profiler blocked. Speed gate saves 25-70 us/frame when below 5 m/s; deferred sweep avoids about 18 us/frame versus same-frame blocking cast; low-tier stop-on-hit saves about 8 us/impact; event-driven debris saves about 40 us/impact versus direct spawn; haptic signal saves about 15 us/impact versus direct device call; blackbox counter costs about 1 us/frame; rsqrt polish saves about 1-2 us per impact cluster.

Verification: Unity MCP refresh/console could not attach to the editor session. Active Unity log reports unrelated compile blockers in FaunaBrain.Foveated, ModEventProjectionBridge, SpectrumSystem, and Burst missing Hecton8.Vehicles.VFX. `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` failed with 102 stale/missing-reference errors including Scheduling, Fluids, Memory.Layout, audio propagation, and generated-csproj missing Hecton8.Physics.CCD. No green compile was claimed.

Status: PENDING VERIFICATION / BLOCKED BY DEPENDENCY. Rationale and task checklist updated on disk.

## Post-Polish Recheck - Leviathan Consequence Parity

What was wrong: Leviathan lunge CCD had collision authority, high-speed impact telemetry, and debris, but did not mirror the player/vehicle path for ImpactSignal audio, directional camera bias, HapticRequest, or massive-impact DamageSignal.

What was done: patched FaunaBrain EmitPredatorLungeCcdImpact to publish ImpactSignal, CameraJuiceSignals.PublishImpact with the exact normalized CCD normal, HapticRequest, and gated Hecton8.Core.Signals.DamageSignal when lost kinetic energy crosses the massive-impact threshold. Debris now uses the same impact intensity scalar so downstream VFX/audio read one coherent severity.

Cinematic Cheats used: native consequence packets instead of direct VFX spawn, direct camera shake, direct haptic device calls, or full lunge contact simulation. High-end overkill is left to downstream consumers; low-tier authority remains one-hit stop/slide with no new query.

Exact Microseconds saved: estimates only. Event-driven debris saves about 40 us/impact versus direct spawn; signal haptics save about 15 us/impact versus direct device call; directional CameraJuice reuse saves about 3 us versus parallel camera math; rsqrt impact speed remains about 1-2 us cheaper per impact cluster than sqrt.

Verification: static CCD grep found no hot-path math.sqrt/new List/string formatting in owned CCD paths except preallocated FaunaBrain fields. Unity MCP validate/read_console still return no_unity_session. Unity log currently blocks on HectonVisorUberPostFeature RuntimeState constructor args, VehicleSubOsCockpitRuntime unassigned groundRadar locals, and Burst Hecton8.Vehicles.VFX resolution. dotnet build Hecton8.Core.csproj now fails with 111 generated-csproj/stale-reference errors, including missing Scheduling, Fluids, Memory.Layout, audio propagation, and Hecton8.Physics.CCD project references. No green compile claimed.

## Post-Polish Recheck - Player Sweep Buffer Hygiene

What was wrong: Player CCD scheduled a four-command capsule batch into a reused 32-hit NativeArray without clearing it first. Vehicle CCD already cleared its reused hit lane; the player path could consume stale hits if Unity wrote fewer contacts on a later sweep.

What was done: added a fixed 32-slot default clear immediately before scheduling the player CapsulecastCommand batch.

Cinematic Cheats used: none. This is authority hygiene, not presentation.

Exact Microseconds saved: estimated 1-3 us added only on high-speed scheduled frames, but it avoids false CCD blocks and prevents unnecessary downstream impact/debris/haptic/camera packets.

Verification: static diff confirms the clear occurs before ScheduleBatch and after all four commands are written. Static CCD grep found no hot-path math.sqrt/string formatting in owned CCD paths; the reported List allocations are preallocated FaunaBrain fields. `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` still fails with 113 generated-csproj/stale-reference errors before CCD can be isolated-verified.

## Post-Polish Recheck - Fauna Target Identity

What was wrong: Leviathan CCD target identity used `hit.collider.GetEntityId()` while player/vehicle CCD used `RaycastHit.colliderEntityId`, leaving fauna with an unnecessary managed collider dereference during impact consequence emission.

What was done: changed Leviathan target hash assignment to `EntityId.ToULong(hit.colliderEntityId)` so all CCD impact emitters use the same RaycastHit value-data identity path.

Cinematic Cheats used: none. This is identity hygiene for native consequence payloads.

Exact Microseconds saved: sub-1 us estimate per Leviathan CCD impact; no profiler proof because compile remains blocked.

Verification: static diff confirms no `hit.collider.GetEntityId()` remains in `EmitPredatorLungeCcdImpact`. Compile remains PENDING VERIFICATION due global project blockers.

## Verification Hygiene - Build Server Shutdown

What was wrong: the repeat `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` pass timed out after 120 seconds behind the same generated-csproj/reference failures and left MSBuild build-server nodes running.

What was done: ran `dotnet build-server shutdown` and confirmed only Unity's own Roslyn compiler server process remains.

Cinematic Cheats used: none.

Exact Microseconds saved: no runtime gameplay saving. Local CPU churn removed from abandoned verification nodes.

Verification: Unity MCP `validate_script` still returns `no_unity_session`; Unity log still shows unrelated `Hecton8.Vehicles.VFX` Burst resolution failure. Status remains PENDING VERIFICATION.
