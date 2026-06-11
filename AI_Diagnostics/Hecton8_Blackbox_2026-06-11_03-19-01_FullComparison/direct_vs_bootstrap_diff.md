# Direct vs Bootstrap Runtime Diff

## Executive Verdict
- **SHARED_SCENE_OR_ASSET_ISSUE**: Both modes fail similarly without obvious entry flow or bootstrap differentiation.

## Key Differences Summary
| Metric | Direct | Bootstrap |
|---|---|---|
| Scene | 02_HECTON_WORLD | 00_BOOTSTRAP |
| Registry Phase | Registering | Registering |
| Registry Filled Slots | 2 | 6 |
| Registry Real Null Slots | 16 | 12 |
| Registry Missing Members | 0 | 0 |
| Active Terrains | 0 | 0 |
| Ocean Active | True | False |
| OceanKinematics Registered | False | False |
| Atmosphere Manager Active | False | False |
| Celestial Engine Active | False | False |
| Console Errors | 0 | 0 |

## Minimal Next Check

## Detailed Slot Differences
| Slot | Direct Status | Bootstrap Status | Changed? | Member Found? |
|---|---|---|---|---|
| Input | NULL | NULL | NO | YES |
| Physics | NULL | NULL | NO | YES |
| Audio | NULL | NULL | NO | YES |
| Scene | NULL | FILLED(SceneRuntimeService) | YES | YES |
| Save | NULL | FILLED(SaveManager) | YES | YES |
| UI | NULL | NULL | NO | YES |
| Player | NULL | NULL | NO | YES |
| OceanKinematics | NULL | NULL | NO | YES |
| AtmosphereRuntime | NULL | NULL | NO | YES |
| CelestialEngineRuntime | NULL | NULL | NO | YES |
| MapMagicRuntime | FILLED(MapMagicRuntimeBridge) | NULL | YES | YES |
| TerrainProviderRuntime | FILLED(MapMagicRuntimeBridge) | NULL | YES | YES |
| TickManager | NULL | FILLED(GameTickManager) | YES | YES |
| Dispatcher | NULL | FILLED(SystemDispatcher) | YES | YES |
| RenderDispatcher | NULL | FILLED(RenderDispatcher) | YES | YES |
| ObjectPool | NULL | FILLED(ObjectPoolManager) | YES | YES |
| Environment | NULL | NULL | NO | YES |
| Weather | NULL | NULL | NO | YES |
