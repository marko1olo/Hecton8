# SHINOBU_314 Carrion Decay Route

Owner: Echelon 3 Ecosystem and AI
Runtime owner: `NutrientDriftRuntime` partial, file `Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime_Carrion.cs`
Status: STATIC_SOURCE_POLISHED, targeted Core build attempted and blocked by unrelated compile wall.

## Route

- Death ingress: `FaunaBrain.Die()` and existing combat/ecology producers publish `EntityDeathSignal`; `SignalBus<EntityDeathSignal>.GetFrameSnapshot()` is drained on owner phase into `GlobalDataVault`.
- Fauna entity identity: `EntityDeathSignal.EntityHash` comes from fauna-local `ResolveStableFaunaHash(FaunaCarrionDeathHashSalt, 0)`, not Gameplay combat target routing.
- Species profile route: Fauna-owned death signals set `EntityDeathSignal.FlagFaunaBrainCarrion` and carry species hash in `EntityDeathSignal.SourceHash`; carrion ingress treats unflagged signals as generic/ecology fallbacks.
- CSV profile keys: `species_key` accepts `default`, decimal fauna `speciesID`, `0x` hash, or token FNV; unmatched fauna species IDs fall back to the `default` profile keyed by `CarrionRouteHash`.
- Truth storage: `CarrionStateDTO[5000]`, explicit 64-byte layout, buffers `71250-71259`.
- Fault vaccination: death ingress sanitizes biomass/toxicity before DTO creation.
- Invalid active AUP/biomass/age/decay/toxicity marks `CarrionStateDTO.FlagMathFault`.
- The slot retires from active decay.
- Next injection pass consumes it into current-tick telemetry fault word before sanitization.
- Duplicate guard: `ProcessEntityDeathJob` resolves an active `CarrionStateDTO` by `EntityHash` before allocating a new slot and refuses to let an unflagged generic duplicate overwrite a Fauna-owned species row.
- Solver phase: `FrostTick`, after thermal source injection and before nutrient advection.
- Nutrient path: AUP `double3` corpse position minus nutrient grid `double3` origin, then local `float3` cell index; no absolute float downcast.
- Scavenger path: bounded `CarrionAttractionRecordDTO[512]` emitted to `WorldSpatialHashGrid.RegisterTransientEvent` as chemical resource signals.
- Telemetry: `CarrionTelemetryEntry[300]`; `BurstExecutionMicroseconds` is the carrion subchain schedule-to-finalize window; fault dump path `Docs/AgentLogs/Dump_SHINOBU_314.bin`.

## Authority Notes
- No new signal lane was added. Existing `EntityDeathSignal` remains the death truth corridor.
- Fauna presentation never writes carrion state directly; it only publishes the shared death signal once at the death edge.
- `GlobalRegistry` is only read in cold/owner calls already owned by `NutrientDriftRuntime`; Burst jobs receive raw pointers and DTO tuning.
- `GlobalQualityWeight` scales decay approximation continuously: below `0.4`, `math.step` gates out `math.exp`; above it, `math.smoothstep(0.4, 0.95)` blends toward exact exponential. Gameplay identity and DTO layout do not change.
- Spatial hash records are a Dear Lie presentation/query attractor. They are not authoritative save state.
- Scanner proof writes a stable SHINOBU_314 report and upserts `shinobu314CarrionDecay` into the aggregate AI report; it does not erase other agents.
- DataMonolith runtime readiness is not claimed: `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` exists in the current X_012 scan; route-specific boot proof remains pending.

## Buffer Map
- `71250` `ShinobuCarrionStates`
- `71251` `ShinobuCarrionDeathIngress`
- `71252` `ShinobuCarrionRuntimeCounters`
- `71253` `ShinobuCarrionTuning`
- `71254` `ShinobuCarrionTelemetryRing`
- `71255` `ShinobuCarrionAttractionRecords`
- `71256` `ShinobuCarrionProfiles`
- `71257` `ShinobuCarrionCsvScratch`
- `71258` `ShinobuCarrionFaunaStates`
- `71259` `ShinobuCarrionFaultFlags`
