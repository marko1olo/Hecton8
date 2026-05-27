# Unity Mod Registry Cache Pass - UNKNOWN - 2026-05-27

Status: STATIC_SOURCE_PROOF_ONLY
Owner: UNKNOWN
Domain: Core & Memory Infrastructure / Modding Bridge / Global Registry Routes

## Problem

Several mod bridge helpers still resolved owner services through `GlobalRegistry` from runtime-facing routes:

- `ModSettingsRegistry` read `GlobalRegistry.UserOptions` during setting register/apply.
- `ModItemRegistry.ResolveActiveCatalog()` read `GlobalRegistry.PlayerInventory`.
- `ModBuildableRegistry.ResolveActiveCatalog()` read `GlobalRegistry.Logistics`.
- `FutureCommandSandboxValidator.OpenVaultLane()` and rollback checks fell back to `GlobalRegistry.DataVault`.
- Mod slider rows saved options and refreshed the settings registry on every `Slider.onValueChanged`.

These are not measured frame-time regressions. The defect is route ownership: runtime/mod-facing readers should consume cached owner interfaces or cached Vault handles, not poll global identity.

## Changes

- Added cold/hot-swap caches for `UserOptionsPersistence`, `IPlayerInventoryService`, and `ILogisticsService`.
- Added cached storage keys to mod setting entries so apply routes do not rebuild the key string.
- Added cold/hot-swap `IDataVault` binding for `FutureCommandSandboxValidator`.
- Removed the `OpenVaultLane()` `GlobalRegistry.DataVault` fallback.
- Routed cache binding through `ModLoader` and hot-swap propagation through `ModEventProjectionBridge`.
- Changed slider settings to apply live callbacks in memory, then persist and notify once on pointer-up, submit, disable, or destroy.

## Files

- `Assets/_Project/Scripts/ModdingAPI/ModSettingsRegistry.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModRuntimeState.cs`
- `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModLoader.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModEventProjectionBridge.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModMenuSettingSliderView.cs`

## Proof

- `git diff --check` on touched files: no whitespace errors; Git reported LF/CRLF warnings only.
- Brace balance on touched files: `0` delta for all six files.
- Targeted direct-read scan on touched files now finds only cold-bind/install reads:
  `ModSettingsRegistry.BindRegistryServicesCold`, `ModItemRegistry.BindRegistryServicesCold`,
  `ModBuildableRegistry.BindRegistryServicesCold`, `FutureCommandSandboxValidator.BindRegistryServicesCold`,
  and `ModEventProjectionBridge.InstallGlobal`.
- `SignalBusContractAuditCli`: `files=2443`, `shaders=71`, `errors=0`, `confirmedErrors=0`, `warnings=145`, `infos=1172`.
- Touched-file audit findings are info-only: declared/registered local queues, Vault alias, cold/fatal dump I/O, and mod manifest file I/O.
- Documentation gates: `VerifyDocStructure.py pass=true activeDocCount=697 encodingWithoutUtf8Sig=0`;
  `OOP_Doc_Scanner.py finalPass=true activeFileCount=697 sourceSyncPass=true`.

## Boundaries

- Full project compile errors were not fixed or chased by explicit user instruction.
- No runtime profiler, Unity Console, Play Mode, player build, GCMonitor, or device proof was run.
- Runtime microseconds saved claimed: `0`. This is dependency-route correctness and stability work.
