# SHINOBU_319 Status Effects FSM Route Card

Date: 2026-05-22
Owner: `SHINOBU_319 / STATUS_EFFECTS_FSM_ENGINE`
Domain: Echelon 5 Combat & Physiology
Evidence: STATIC_SOURCE / STATIC_DOC. Unity import, Burst Inspector, profiler, GCMonitor, and player-build proof remain pending under CPU/compiler guard.

## Authority

- One fact: active poison, bleed, burn, stun, brittle, hypoxia, crush, irradiation masks and timers.
- One owner: `CombatDamageRuntime` status partial.
- One route: `CombatStatusEffectRequest` queue -> `ApplyStatusEffectRequestsJob` -> Vault `CombatStatusEffectState.StatusEffectMask` -> `EvaluateStatusEffectsJob` -> Vault `CombatDamageSignal[MaxTargets]` staging -> owner completion -> `SignalBus<CombatDamageSignal>` for health truth.
- One proof artifact: Vault `CombatStatusEffectTelemetryEntry[300]` plus `Docs/AgentLogs/Dump_SHINOBU_319.bin`.

## Vault Buffers

| BufferID | Name | Type | Capacity | Owner |
|---:|---|---|---:|---|
| `71260` | `Shinobu319StatusEffectStates` | `CombatStatusEffectState` | `MaxTargets` | `SystemID.GameplayCombat` |
| `71261` | `Shinobu319StatusEffectTelemetryRing` | `CombatStatusEffectTelemetryEntry` | `300` | `SystemID.GameplayCombat` |
| `71262` | `Shinobu319StatusEffectTelemetryCursor` | `int` | `2` | `SystemID.GameplayCombat` |
| `71263` | `Shinobu319StatusEffectTuning` | `CombatStatusEffectTuning` | `1` | `SystemID.GameplayCombat` |
| `71264` | `Shinobu319StatusEffectCounters` | `CombatStatusEffectCounterLane` | `9` | `SystemID.GameplayCombat` |
| `71265` | `Shinobu319StatusEffectCsvProfiles` | reserved ID only; not requested by runtime | `0` | `SystemID.GameplayCombat` |
| `71266` | `Shinobu319StatusEffectScannerReport` | reserved ID only; not requested by runtime | `0` | `SystemID.GameplayCombat` |
| `71267` | `Shinobu319StatusEffectVfxRequests` | `CombatStatusEffectVfxRequest` | `MaxTargets` | `SystemID.GameplayCombat` |
| `71268` | `Shinobu319StatusEffectDamageSignals` | `CombatDamageSignal` | `MaxTargets` | `SystemID.GameplayCombat` |

## ABI

- `CombatStatusEffectRequest`: 64 bytes, `StatusEffectMask@8`, `ImpactAup double3@24`.
- `CombatStatusEffectState`: 64 bytes, `StatusEffectMask@0`, timer `float4` lanes at `8` and `24`, FSM bytes at `48..55`, `StateHash@56`.
- `CombatStatusEffectTuning`: 64 bytes, scalar tuning and `GlobalQualityWeight01@40`.
- `CombatStatusEffectTelemetryEntry`: 64 bytes, `StatusEffectMask@8`, solve microseconds at `56`, bit extraction count in `Reserved@60`.
- `CombatStatusEffectCounterLane`: 64 bytes, `Value@0`; each Interlocked counter occupies one cache line.
- `CombatStatusEffectVfxRequest`: 64 bytes, exact `double3 PositionAup@0`, intensity/radius/frame/source/effect fields at `24..44`.
- `CombatDamageSignal`: existing 64-byte Core signal ABI staged in Vault `71268`; status does not redefine or own its schema.

## Phase Route

- PRE_SIMULATION: `ApplyStatusEffectRequestsJob` drains bounded requests and atomically ORs mask bits.
- If cadence debt has not matured, request-only frame stops here and skips the O(MaxTargets) evaluator.
- It does not require armor `TargetRootAups` and does not lock VFX/damage staging buffers.
- SLOW_TICK / SIMULATION:
  - Job: `EvaluateStatusEffectsJob`.
  - Writes: timer decrement, byte FSM refresh, integrated DoT, telemetry.
  - Stages combat damage into Vault buffer `71268`.
  - Stages toxic bubble requests into Vault buffer `71267`.
  - Armor AUP refresh and VFX/damage staging locks occur only on simulation frames.
  - When armor AUPs are read, status owner borrows the armor Vault lock until owner completion.
- POST_SIMULATION:
  - `CombatDamageRuntime.DispatchStatusResults` records status telemetry only.
  - Health truth is applied later by the central damage router from `SignalBus<CombatDamageSignal>`.
  - Status damage and VFX requests publish from owner completion after the job fence.
  - Simulation worker does not publish.
  - Missing bubble VFX storage suppresses presentation only; request application and DoT truth still proceed.
- VISUAL_SYNC: toxic bubbles consume `SignalBus<BubbleSpawnSignal>` with exact `AbsoluteUniversePosition` payload. No CPU particles or `GameObject` status components.

## Scalability

- `GlobalQualityWeight` continuously lerps status cadence from `1.0s` toward `0.1s`, batch size from `128` toward `32`, and toxic bubble cadence from `48` frames toward `8` frames.
- Request-only frames collapse to the queue-drain job without running the target sweep.
- Damage integrates by accumulated delta, so quality changes cadence and presentation density only; mask truth, DTO layout, save identity, and authority route do not change.

## Failure And Dump

- The 300-entry telemetry ring records active live-mask row count, request count, damage, VFX count, bit extraction count, state hash, anomaly hash, and elapsed microseconds.
- Active row count is captured before result early-out, so stable poison/bleed rows remain visible even if they emit no damage/change row in that frame.
- Per-result telemetry is folded by owner completion after the Burst fence; the parallel job never writes the ring.
- Non-finite health/damage/mask faults, `SignalBus<CombatDamageSignal>.TryPush` backpressure (`0x5319D001`), missing damage SignalBus native storage at publish time (`0x5319D002`), or solve time above `200us` write `Docs/AgentLogs/Dump_SHINOBU_319.bin` in cursor-ordered ring order.
