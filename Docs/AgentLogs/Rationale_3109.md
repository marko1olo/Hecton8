# Rationale 3109 - Full UI / Player Movement

Evidence class: `STATIC_SOURCE`. Runtime status: `PENDING PLAYMODE + PROFILER PROOF`.

## Decisions

- Keep 3109 blocked on runtime binding proof. Static source still shows a tagged scene `Player` shell with `HectonWorldShellController1428`; accepting that as production movement would violate player/input/UI mandates.
- Do not mutate `02_HECTON_WORLD.unity` from this lane. Static report notes large scene churn, and raw YAML scene edits are forbidden without scoped proof. Correct route is Unity API mutation by a Unity owner after readback proves the shell wins.
- Do not launch `dotnet build`. CPU reported `100` and an active `dotnet` process exists; AGENTS forbids build launch under this gate.
- Do not claim Unity proof. MCP resource check returned no mounted Unity resources, so no Play Mode readback was available in this session.
- Treat `GameBootstrapper.ResolveSceneActivationReferences`, `SpawnPlayerAsync`, and `PublishPlayerRuntimeReference` as the primary proof/fix decision points. They determine whether a tagged scene object or production prefab reaches `BootstrapState.CurrentPlayerObject`.
- Treat `PlayerRuntimeContextService` as a consumer, not the owner of player truth. It must bind after the bootstrap owner publishes the correct player.
- Keep `HUD_Internal.prefab` `forceScreenSpaceOverlay: 1` and `SuitHUDV4CanvasOverlay` `GraphicRaycaster` re-enable paths under runtime readback scrutiny. Static source alone cannot prove they are inactive or bridge-only.
- Keep legacy `Hecton8.UI.InteractionUI` suspect until active scene absence is proven. Prefer the prefab-bound `Hecton8.Interaction.InteractionUI` only after runtime activation is shown.

## First-20-Minutes Route Impact

This removes a route blocker for the first playable descent: the player must move, look, swim, surface/shore transition, interact, and read oxygen/depth/pressure/route state through the production player/HUD graph. A shell controller with direct input is not acceptable proof.

## Regression Model

- CPU: no new runtime work added; future fix must not add hot bootstrap polling or extra per-frame scene searches.
- GC: no code changed; future proof must show 0 B/frame for input/HUD/prompt hot paths.
- Memory: no scene/prefab mutation; future HUD proof must capture RT/canvas/raycaster state.
- Cadence: future UI readouts must keep 60Hz only for immediate cursor/warning, 10Hz for oxygen/depth/pressure, 2Hz/event-driven for low-priority data.
- Correctness: highest risk is publishing scene shell as `BootstrapState.CurrentPlayerObject`, causing runtime context, UI, interaction, and tools to bind to the wrong object.

## Failure Modes

- Shell wins bootstrap and production movement/HUD never activates.
- Production player spawns but tagged shell remains enabled and steals input/camera.
- Runtime context binds before production player publication and keeps stale component nulls.
- HUD canvas remains interactive `ScreenSpaceOverlay` or has enabled gameplay `GraphicRaycaster`.
- Interaction prompt path uses legacy string route or duplicate prompt owners.
- PDA/pause focus disables movement or fails to restore input ownership.

## Why Kept / Rejected

- Kept: static blocker, because source and scene evidence agree.
- Rejected: movement/UI acceptance, because no Play Mode, GCMonitor, profiler, or capture proof exists.
- Rejected: scene mutation, because no safe scoped Unity API path was executed and raw YAML edit would be reckless.
