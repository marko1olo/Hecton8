# CURRENT_BATCH_AUDIT_20260516

Evidence class: STATIC_DOC / STATIC_GIT
Date: 2026-05-16
Scope: active `Docs/Tasks/CURRENT_BATCH.md` and companion instruction file.

## Git Gate

- `git rev-list --left-right --count origin/main...HEAD`: `0 0` before this audit.
- `git stash list`: empty before this audit.
- `Docs/Archive/Batch005/Tasks_Combined/CURRENT_BATCH.md` was locally emptied. It was restored from `HEAD` before checkpointing because committing an empty archive file would remove 312 archived prompt lines.

## Files Checked

- `Docs/Tasks/CURRENT_BATCH.md`
  - Size: 106637 bytes.
  - Line count: 1480.
  - `<AGENT_PROMPT id="...">` count: 36.
  - `</AGENT_PROMPT>` count: 36.
  - `<POLISH_MANDATE>` count: 0.
- `Docs/Tasks/НЕ ДВИГАТЬ! ИНСТРЫ.txt`
  - Size: 9139 bytes.
  - Instruction list Prompt ID count: 40.
  - Kept in place because filename explicitly says not to move.

## Prompt IDs In Current Batch

1. `INTEGRATION_ASSEMBLY_SURGEON`
2. `SIGNAL_AUTHORITY_VALIDATOR`
3. `VAULT_SOVEREIGNTY_ENFORCER`
4. `AUP_DETERMINISM_WATCHDOG`
5. `SUBMARINE_BALLAST_PID_V2`
6. `HULL_DYNAMIC_DEFORMATION`
7. `VERLET_TOW_WINCH`
8. `GRAB_IK_PROJECTION`
9. `VOLUMETRIC_SILT_ADVECTION`
10. `EXTINCTION_LUT_SAMPLER`
11. `PREDATOR_SENSORY_PUMP`
12. `KCC_SDF_SQUEEZE_RESOLVER`
13. `TOOL_RESAK_SOLVER`
14. `WELDING_REPAIR_ENGINE`
15. `COMPASS_GYRO_STABILIZER`
16. `LADDER_CLIMB_IK`
17. `GPU_SCATTER_LOD_MANAGER`
18. `SCREEN_SPACE_REFRACTION`
19. `FOVEATED_RENDER_COMMANDER`
20. `PATH_FUNNEL_NAVMESH_FIXER`
21. `TRIPWIRE_CRASH_REPORTER`
22. `STP_QUALITY_ADAPTER`
23. `BIOLUM_PULSE_SYNC`
24. `HUD_GLITCH_DISTORTION`
25. `SALINITY_SALT_CRYSTALLIZER`
26. `LEVIATHAN_BITE_IK`
27. `ECOSYSTEM_POPULATION_BALANCER`
28. `BOID_SENSORY_INPUT_PUMP`
29. `WELDING_REPAIR_LOGIC`
30. `LADDER_CLIMB_IK`
31. `SENTINEL_DISPOSAL_GUARD`
32. `FAUNA_BITE_IK_SOLVER`
33. `ECOSYSTEM_MIGRATION_LINK`
34. `RETINAL_ADAPTATION_AI`
35. `ACOUSTIC_ECHO_LOCATION_AI`
36. `SIMULATION_BUCKET_DISTRIBUTOR`

## Duplicate Prompt IDs In Current Batch

- `LADDER_CLIMB_IK`: 2 occurrences.

## Instruction IDs Missing From Current Batch

- `ITEM_MAGNET_SOLVER`
- `DOCKING_AUTOPILOT_SPLINE`
- `UBER_NOIR_INTEGRATOR`
- `VOLUMETRIC_PARTICLE_ADVECTION`
- `INTERACTIVE_WAKE_VFX`
- `DEBRIS_PHYSICS_FAKE`
- `PREDATOR_STALK_DIRECTOR`
- `AMBIENT_BIOTA_DIRECTOR`
- `MEMORY_DEFRAGMENTATION_OVERSEER`
- `CORE_TICK_DILATION`
- `LOCKSTEP_STATE_VALIDATOR`
- `HARDWARE_THROTTLING_DIRECTOR`
- `ARCHITECTURAL_INQUISITOR_SENTINEL`

## Batch-Only IDs Not Listed In Instruction File

- `HULL_DYNAMIC_DEFORMATION`
- `VOLUMETRIC_SILT_ADVECTION`
- `PREDATOR_SENSORY_PUMP`
- `WELDING_REPAIR_ENGINE`
- `TRIPWIRE_CRASH_REPORTER`
- `HUD_GLITCH_DISTORTION`
- `SALINITY_SALT_CRYSTALLIZER`
- `LEVIATHAN_BITE_IK`

## Decision

Do not invent or synthesize missing agent prompts. The active batch is committed as provided, with this audit file documenting the mismatch. Any agent launcher should treat this batch as `PENDING BATCH OWNER REVIEW` until the owner confirms whether the instruction list or `CURRENT_BATCH.md` is authoritative.

## Residual Risk

- Agents listed only in `НЕ ДВИГАТЬ! ИНСТРЫ.txt` will fail to extract a prompt from `CURRENT_BATCH.md`.
- The duplicated `LADDER_CLIMB_IK` ID creates ambiguous extraction if a launcher expects one prompt per ID.
- The missing `<POLISH_MANDATE>` conflicts with the project protocol that expects a polish mandate after core tasks.
