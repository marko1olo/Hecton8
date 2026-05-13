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
