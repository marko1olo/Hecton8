# Shell / HUD Scene Binding Escalation - 2026-06-05

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_SCENE_YAML_SCAN + STATIC_PREFAB_YAML_SCAN + STATIC_SOURCE_SCAN`.
Scope: escalation of runtime blocker severity after targeted static scene/prefab readback.

No Unity run, Play Mode, player build, profiler, GCMonitor, screenshot, scene save, prefab edit, source edit, or YAML mutation was performed.

## What Was Wrong

The earlier runtime static audit treated `HectonWorldShellController1428` as a shell risk that needed active-route proof. Targeted scene YAML raises that risk: `02_HECTON_WORLD.unity` contains a scene-local active `Player` object with an enabled `HectonWorldShellController1428` component.

That is not runtime proof of dispatcher registration, but it is enough to stop treating the shell as a distant candidate. The scene contains the blocker.

The HUD finding is narrower than the earlier wording. `HUD_Internal.prefab` contains `forceScreenSpaceOverlay: 1`, but the compositor component is serialized as disabled. It remains a latent blocker until Unity readback proves whether any scene/runtime route enables it or clones the same overlay mode.

## Static Evidence

### Scene Shell Binding

`Assets/_Project/Scenes/02_HECTON_WORLD.unity`:

- lines `70220-70225`: scene `GameObject` component list includes component `{fileID: 1568927389}`.
- lines `70227-70232`: scene object name `Player`, tag `Player`, `m_IsActive: 1`.
- lines `70248-70260`: component `{fileID: 1568927389}` is `Hecton8.World.HectonWorldShellController1428`, `m_Enabled: 1`, `cameraRig: {fileID: 1505808849}`.
- lines `70261-70264`: shell movement values are serialized: `moveSpeed: 8`, `verticalSpeed: 4.5`, `lookSpeed: 0.11`, `idleDriftMeters: 0.08`.

`Assets/_Project/Scripts/World/HectonWorldShellController1428.cs`:

- class implements `IUpdatable`.
- `Tick(float deltaTime)` reads look/move input and writes `_transform.rotation`, `_transform.position`, and camera rig position/rotation.
- input polling hits include `Keyboard.current`, `Mouse.current`, `Input.GetKey`, `Input.GetMouseButton`, and `Input.GetAxisRaw`.

### Production Player Prefab Candidate

`Assets/_Project/Prefabs/Player.prefab`:

- lines `915-920`: prefab object name `Player`, tag `Player`, `m_IsActive: 1`.
- lines `1015-1019`: `Hecton8.Gameplay.HectonPlayerMovement` component is enabled.

This proves the production player prefab has movement owner material. It does not prove the prefab is the active scene player source, because the scene shell block is scene-local (`m_PrefabAsset: {fileID: 0}` around the shell component region).

### HUD Internal Latent Overlay

`Assets/_Project/Prefabs/HUD_Internal.prefab`:

- lines `14-19`: prefab object name `HUD_Internal`, `m_IsActive: 1`.
- lines `35-46`: `NASAPunk.Visor.SuitHUDScreenCompositor` component exists.
- line `42`: compositor `m_Enabled: 0`.
- line `53`: `forceScreenSpaceOverlay: 1`.

This is not enough to call the overlay active. It is enough to require Unity readback before any HUD acceptance claim.

## Severity

| Blocker | Static severity | Runtime claim allowed |
|---|---|---|
| `02_HECTON_WORLD` shell controller | `ACTIVE_SCENE_BINDING_STATIC` | none |
| Production player prefab route | `CANDIDATE_PRODUCTION_ROUTE_STATIC` | none |
| `HUD_Internal.forceScreenSpaceOverlay` | `LATENT_PREFAB_BLOCKER_STATIC` | none |

## Required Next Owner Action

Use `taskslocal/runtime_system_20260605/RUNTIME_OWNER_02_SHELL_HUD_BLOCKER_REPAIR_PACKET.md`.

Next Unity/runtime owner must:

1. Read active scene player object and prefab source without mutation.
2. Read whether scene `Player` with fileID `1568927387` is the runtime player, a staging shell, or obsolete scene-local object.
3. Read dispatcher registration for `HectonWorldShellController1428` and production `HectonPlayerMovement`.
4. If shell is active in the production route, classify as `ACTIVE_ROUTE_BLOCKER`; do not patch input inside the shell.
5. Replace/disable only through Unity API after clean process gate and readback, not by raw scene YAML.
6. Read active HUD stack and prove whether `HUD_Internal` is enabled, cloned, or inactive.
7. If any active interactive gameplay HUD uses forced `ScreenSpaceOverlay`, reject it until routed through approved diegetic/visor/world-space ownership or explicitly classified as a noninteractive bridge.

## Regression Model

- CPU: static scan only. Active shell would add direct input/camera/movement work and possible duplicate movement authority.
- GC: static scan only. Direct input shell and HUD mode require runtime GC proof; no `0 B/frame` claim exists.
- Memory: no runtime memory proof. HUD compositor texture/overlay route remains pending.
- Cadence: active shell would run in dispatcher `Environment` lane while production player route may also run, creating phase/authority ambiguity.
- Correctness: scene-local active shell is a stronger blocker than prior candidate-only evidence. Runtime acceptance remains blocked until Unity readback proves or removes it.

## Low / Middle / High / Ultra Consequences

- Low: shell must not own production movement. HUD must remain readable and zero-GC without overlay-only gameplay shortcuts.
- Middle: production movement, swim, input, camera, HUD, and interaction routes must share one owner chain with dispatcher phase proof.
- High: presentation budget may improve visor/camera/haptics only after shell is gone or proven non-production.
- Ultra: richer visual sync only. Quality weight must not switch player authority, action IDs, save identity, DTO layout, or HUD fact ownership.

Final status: `PENDING_VERIFICATION`.
