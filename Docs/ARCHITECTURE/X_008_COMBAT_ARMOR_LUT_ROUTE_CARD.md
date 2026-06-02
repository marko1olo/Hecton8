# X_008 Combat Armor LUT Route Card

Date: 2026-05-24
Owner: X_008 / COMBAT_DAMAGE_AND_ARMOR_LUT_OPTIMIZER
Domain: Echelon 5 Combat & Survival Physiology

## Route

`CombatDamageSignal` -> `CombatDamageRuntime.DrainGlobalDamageSignals` -> native `CombatDamageRequest` queue -> Burst damage job -> `ArmorProfileDTO` 8x6 LUT -> CAS health deduction -> owner-phase result dispatch.

Player tool ingress:

- `ToolHitUtility.ApplyDamage` keeps the legacy 5-arg overload, but it now delegates to a source-aware overload.
- Registered targets queue into `CombatDamageRuntime.TryQueueDamage`; unregistered `IDamageReceiver` fallback is compatibility-only.
- Source ids: `PlayerToolImpact=10`, `SurvivalBlade=11`, `Harpoon=12`, `StunPistol=13`, `SalvageSampler=14`.
- Stun pistol metadata: `CombatDamageTypes.Emp + CombatStatusBits.Stunned + ResolveStunDuration()`.
- Salvage sampler metadata: `CombatDamageTypes.MicroFracture`.

Presentation egress is only deferred signals:

- `DeflectSignal`: armor deflection payload, legacy `GlobalSignals.DeflectSignalWriter`, queue capacity 128.
- `ImpactSignal`: visual impact payload, `GlobalSignals.ImpactSignalWriter`, queue capacity 256.
- `EntityDeathSignal`: owner completion path through `SignalBus<EntityDeathSignal>`, capacity 64.

LUT and directional front deflects publish `ImpactSignal` only when quality permits and AUP hit point is finite. Damage job owns no presentation callbacks.

The old `ICombatDamageEventListener`/`ICombatDamageFeedbackReceiver` managed callback route is removed. Presentation systems consume SignalBus snapshots; registered targets keep only the owner-state `IDamageReceiver.ReceiveDamage` handoff after job completion.

## DTO Contract

Current `ArmorProfileDTO` footprint is retained:

- offset 0: `uint SpeciesHashID`
- offset 4: `float BaseHealth`
- offset 8: `float BaseArmor`
- offset 12: `uint _pad0` / future table revision
- offset 16: `fixed byte ArmorGridLUT[48]`
- total size: 64 bytes

Associated `ShinobuArmorPenetrationTable`:

- offset 0: `fixed byte Cells[48]`
- offset 48: `uint Revision`
- offset 52: `uint AuthoringHash`
- offset 56: `ulong _pad0`
- total size: 64 bytes

X_008 semantic contract for the 48 cells:

`lutIndex = materialRow * 6 + angleStep`

- `materialRow`: 0..7 projectile/damage material class.
- `angleStep`: 0..5 quantized from `abs(dot(projectileDirection, armorNormal))`.
- No trig, no real-time deformation, no thickness solver.
- `GlobalQualityWeight` may scale fidelity, cadence, feedback detail, and telemetry, not truth layout or authority route.

Active combat quality policy is continuous:

- `SetCombatVisualQualityWeight(float)` writes `_requestedVisualQualityWeight01`.
- Runtime feedback weight is `saturate(SignalBusRegistry.GlobalQualityWeight01) * saturate(_requestedVisualQualityWeight01)`.
- `SetCombatMathLod(CombatMathLod)` remains only as a legacy 0/1 adapter.
- Removed active `_mathLod`, `_requestedMathLod`, and `ResolveFeedbackMathLod`.

## Math Proof

For each pellet:

1. `direction = projectileDirection * rsqrt(max(lengthsq(projectileDirection), epsilon))`
2. `normal = armorNormal * rsqrt(max(lengthsq(armorNormal), epsilon))`
3. `attackDot = saturate(abs(dot(direction, normal)))`
4. `angleStep = clamp(floor((1 - attackDot) * 6), 0, 5)`
5. `lutIndex = (ReadDamageClass(packedMeta) & 7) * 6 + angleStep`
6. `raw = ArmorGridLUT[lutIndex]`

For 100 pellets, lookup work is 100 independent dot/abs/saturate/floor/clamp/index/load sequences. No source-level `acos`, `asin`, `AxisAngle`, `AngleAxis`, `SignedAngle`, or `Quaternion.Angle` exists in `Gameplay/Combat`.

`COMBAT_OPTIMIZATION_REPORT_X_008.json` records `branchlessArmorLookupProof.sourceControlSurface`.

Covered surfaces: `EvaluateArmorPenetrationJob.Execute`, `EvaluateArmorPenetrationCore`, `ResolveArmorAngleStep`, `BuildArmorPenetrationResolvedHit`, `NormalizeArmorLookup`, `ResolveArmorSurfaceNormal`, `CombatDamageRuntime.ResolveExactDirection`.

Current source proof: zero explicit `if/switch/?`, loops, forbidden trig, or angle APIs in checked blocks.

Hidden-helper gate: `armorRuntimeResolveExactDirectionCallCount=0`, `surfaceNormalUsesNormalizeArmorLookup=true`, `deflectFeedbackUsesNormalizeArmorLookup=true`. Target-machine proof still requires Burst disassembly.

Batch evaluator proof:

- `EvaluateArmorPenetrationJob : IJobParallelFor` consumes pre-resolved `CombatDamageRequest`, `CombatDamageSignalDetail`, AUP hit positions, and target slots.
- The evaluator writes fixed 128B `ArmorPenetrationResolvedHitDTO` records only; it does not mutate health, spawn presentation, or publish managed callbacks.
- `RunArmorPenetrationTortureProof` fills synthetic requests via scheduled `CombatDamageTortureJob`, then schedules and times `EvaluateArmorPenetrationJob` for up to 10,000 LUT evaluations in editor/development builds.
- `CombatDamageTortureJob : IJobParallelFor` is the storm input generator; `EvaluateArmorPenetrationJob : IJobParallelFor` is the measured LUT evaluator.

CAS health deduction:

- observed bits are read with `Interlocked.CompareExchange(ref location, 0, 0)`.
- desired bits are written only if the observed bits still match.
- health is monotonic non-increasing because `safeDamage >= 0` and `nextHealth = max(0, previousHealth - safeDamage)`.
- Retry ceiling: `MaxQueuedSignals=1024`.
- 100 same-target pellets are below the queue cap.
- Each failed CAS means another writer committed.
- A writer can lose at most `K-1` races for `K` same-slot writers under the cap.
- `RunAtomicHealthCasTortureProof` exists in editor/development builds.
- CAS torture path: initialize one health slot to `pelletCount`, schedule `AtomicHealthCasTortureJob : IJobParallelFor`, force all workers to subtract `1 HP` from slot `0`.
- X-Ray editor window exposes `Run 100 CAS Torture`; runtime execution is pending Unity compile/import.

## Implementation Status

Phase 0 found that SHINOBU_318 runtime used spatial lookup:

`lutIndex = row * ArmorGridColumns + column`

X_008 source edit changes the hot-path lookup contract to:

`lutIndex = materialRow * 6 + angleStep`

Loop 15 correction: a previous report named `EvaluateArmorPenetrationJob` before the source contained that separated job. This is corrected. Scanner proof now reports `parallelEvaluatorProof.evaluateJobActuallyScheduled=true` and `tortureJobActuallyScheduled=true`.

Loop 17 correction: CAS now has a concrete same-slot editor/development torture harness. Scanner proof reports `casTortureHarness.developmentApi=true`, `parallelSameSlotJob=true`, `sameSlotWriteRestrictionDisabled=true`, and `editorButton=true`.

Loop 18 correction: project-wide inverse-trig evidence includes shader/compute/hlsl sources.

Sky/firmament `asin` latitude calls became bounded polynomial presentation cheat. Scanner proof: `projectAcosAsinInventoryCount=0`, `shaderAcosAsinCount=0`. Remaining shader trig is presentation/bake inventory.

Loop 19 correction: shader inverse-angle evidence covers `asin/acos/atan/atan2`.

- Alien sky, visor post, phantom drones, and gas-giant presentation paths use local fast `atan2` approximations.
- Scanner proof: `shaderInverseAngleCount=0`.
- Remaining shader `sin/cos` tokens are presentation/bake inventory, not armor solver code.

Loop 20 correction:

- Dead managed damage UnityEvent routes were removed from `EnvironmentalHazard` and `FloraProjectile`.
- Asset scan found no serialized bindings.
- Non-damage hazard presentation hooks remain.
- Projectile damage still queues through `BallisticsRuntime`.

Loop 21 correction: proof harness scratch memory is DataVault-owned.

- `RunArmorPenetrationTortureProof` uses fixed 10k torture request/detail/AUP/slot/resolved-hit buffers.
- `RunAtomicHealthCasTortureProof` uses fixed `CasTortureHealth` and `CasTortureSuccesses` buffers.
- Scanner proof records zero `Allocator.TempJob` tokens inside both proof methods.

Loop 22 correction: `EnvironmentalHazard.ApplyDamage()` again publishes non-radiation hazard injury.

- Registered player targets receive `CombatDamageRuntime.TryQueueDamage`.
- Signal metadata: `DamageSourceIds.EnvironmentHazard`, toxic damage, poison status, AUP impact position.
- Direct owner `DamagePacket` path is fallback-only for registration gaps.

Loop 23 correction:

- registered tool hits carry receiver-local hit position into `CombatDamageSignalDetail.LocalPoint`;
- tool hits no longer enter the LUT/weakspot path with zero local point;
- `FaunaBrain` is a registered combat target and owner-side `IDamageReceiver`;
- `FaunaBrain` exposes combat forward, height, and pushback body;
- wound presentation remains owner-side after packet receipt;
- `MantaEmergencyWreck` first tries a registered-fauna collision signal with `DamageSourceIds.MantaEmergencyWreck`, local point, normalized direction, and AUP impact position;
- direct `TakeDamage` remains fallback-only.

Loop 24 correction:

- `EnvironmentalHazard` heat damage now supplies target-local point and keeps owner `DamagePacket` fallback for registration gaps;
- `AbyssalThermalManager` boiling and thermal shock now require a registered combat target and send target-local point plus AUP impact data, not world-position-as-local payloads;
- `SubmarineAtmosphereSystem` boiling fauna spillover first queues registered fauna through `CombatDamageRuntime.TryQueueDamage`;
- new stable source id: `SubmarineAtmosphereBoiling=16`;
- direct `FaunaBrain.TakeDamage(damageAmount)` remains fallback-only when the central route cannot resolve a registered target or AUP.

Loop 25 correction:

- `FaunaBrain.TryQueuePredatorBiteDamage` now sends sanitized contact point, direction, target-local point, and AUP impact data through the central registered-target route;
- `LeviathanTentacleVerletSolver.TryQueueGrabDamage` now sends tentacle-tip AUP impact data through the central registered-target route;
- scanner proof records `predatorBiteCarriesAup=true`, `leviathanGrabCarriesAup=true`, and `directTwoArgQueueCallCount=0` for the fauna attack ingress files.

Loop 26 correction:

- scanner proof now scans all runtime scripts for external two-argument `CombatDamageRuntime.TryQueueDamage(in *, in *)` calls;
- current proof records `projectDirectTwoArgQueueCallCount=0`;
- the internal overload wrapper in `CombatDamageRuntime` remains excluded from the external ingress proof.

Loop 27 correction:

- `LeviathanTentacleVerletSolver.TryQueueGrabDamage` now resolves `AbsoluteUniversePosition impactAupValue`, gates it with `impactAupValue.IsFinite()`, and converts to `double3` only for finite payloads;
- invalid leviathan grab AUP payloads publish `double3.zero` instead of unchecked absolute coordinates;
- scanner proof now requires this finite-check pattern before reporting `faunaRegisteredTargetRoute.leviathanGrabCarriesAup=true`.

Loop 28 correction:

- `ToolHitUtility.TryQueueCentralDamage` no longer treats AUP resolution failure as registered-target route failure;
- registered tool hits sanitize `hitPoint` to `safeHitPoint`, preserve receiver-local `LocalPoint`, and publish `double3.zero` only as degraded AUP metadata;
- scanner proof records `registeredToolAupFailureDoesNotBypassCentralQueue=true`.

Loop 29 correction:

- scanner proof now blocks external one-argument `CombatDamageRuntime.TryQueueDamage(in *)` calls as well as external two-argument calls;
- current proof records `projectDirectOneArgQueueCallCount=0` and `projectDirectTwoArgQueueCallCount=0`.

Loop 30 correction:

- `CombatDamageRuntime.TryQueueDamage` now publishes rate-limited telemetry when queue admission is rejected by an already scheduled damage job or full queue;
- queue reject telemetry uses `TelemetryFlagQueueRejected`, `TelemetryAnomalyQueueBusy`, `TelemetryAnomalyQueueFull`, `_lastQueueRejectFrame`, and `_lastQueueRejectAnomalyHash`;
- scanner proof records `blackBoxTelemetryProof.queueRejectTelemetryRateLimited=true`.

Loop 31 correction:

- `CombatDamageRuntime.CanMutateTargets()` is now a pure scheduled-state guard for register/sync/unregister paths;
- target mutation guards no longer complete/finalize damage jobs or clear scheduled state;
- non-forced damage completion, `FinishArmorPenetrationScheduledCompletion()`, and `DispatchResults()` remain owned by `LateFrameTick()`;
- forced damage completion remains limited to `Shutdown()`;
- scanner proof records `damageRouteManagedMutationAudit.mutatorGuardDoesNotFinalizeJobs=true`, `completionOwner.lateFrameCompletesDamage=true`, and `completionOwner.shutdownForceCompleteOnly=true`.

Loop 32 correction:

- `SargassumMicroFaunaBoids.ApplyLeviathanPhysicalStrike()` no longer uses direct player-health damage as the primary route for registered targets;
- registered player strike damage queues `CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup)` with `DamageSourceIds.FaunaLeviathanBite`;
- the signal carries impact damage metadata, finite direction, finite non-negative impulse magnitude, target-local point, and finite-gated AUP;
- direct `_playerHealth.TakeLeviathanDamage(leviathanStrikeDamage)` remains fallback-only for unregistered player targets;
- registered targets do not direct-fallback on queue rejection; queue reject telemetry is the proof route;
- scanner proof records `leviathanStrikeDamageRouteProof.centralQueueBeforeFallback=true`, `registeredTargetDoesNotDirectFallbackOnQueueReject=true`, `localPointAndAup=true`, and `stableSourceId=true`.

Loop 33 correction:

- `MantaEmergencyWreck.TryQueueFaunaCollisionDamage`, `SubmarineAtmosphereSystem.TryQueueBoilingFaunaDamage`, and `EnvironmentalHazard.TryQueueCentralHazardDamage` no longer return central route failure after registration solely because AUP metadata failed or queue admission returned false;
- registered routes publish `CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup)`, degrade invalid AUP to `double3.zero`, and return `true`;
- direct `faunaBrain.TakeDamage` / `playerHealth.ReceiveDamage` fallback remains unregistered/invalid setup compatibility only;
- scanner proof records Manta registered no-fallback, heat registered no-fallback, heat AUP degradation no-bypass, submarine boiling registered no-fallback, and submarine boiling AUP degradation no-bypass as `true`.

Loop 34 correction:

- project-wide exact scan now gates `return CombatDamageRuntime.TryQueueDamage(...)`;
- `ToolHitUtility.TryQueueCentralDamage`, `FaunaBrain.TryQueuePredatorBiteDamage`, and `LeviathanTentacleVerletSolver.TryQueueGrabDamage` call the queue and return `true` after registration;
- predator bite and leviathan grab AUP payloads now validate converted `double3` before publishing; invalid payloads degrade to `double3.zero`;
- scanner proof records `projectDirectReturnQueueCallCount=0`, registered tool no-fallback, predator bite no-fallback, leviathan grab no-fallback, and fauna direct return queue count `0`.

Loop 35 correction:

- branchless source proof now includes helper normalization instead of only top-level evaluator blocks;
- `ResolveArmorSurfaceNormal` and deflect `FrontDot` in the armor partial no longer call shared `ResolveExactDirection`;
- shared `ResolveExactDirection` and `ResolveApproximateDirection` use `math.select` fallback instead of ternary fallback for remaining production callers;
- scanner proof records `hiddenHelperGate.armorRuntimeResolveExactDirectionCallCount=0`, `surfaceNormalUsesNormalizeArmorLookup=true`, and `deflectFeedbackUsesNormalizeArmorLookup=true`;
- `NormalizeArmorLookup`, `ResolveArmorSurfaceNormal`, and `CombatDamageRuntime.ResolveExactDirection` each report zero explicit control tokens, zero loop tokens, zero forbidden trig, and zero angle APIs.

Loop 36 correction:

- resident fauna hydration no longer restores saved health via artificial `TakeDamage`;
- `FaunaBrain.ApplyHibernationHealthSnapshot` writes finite/clamped saved health, marks the combat mirror dirty, and calls `Die()` only for dead snapshots;
- the snapshot route intentionally does not trigger hit flash, immediate hit reaction, parental-defense/fear signals, or wound presentation;
- scanner proof records `faunaRegisteredTargetRoute.hibernationRestoreUsesHealthSnapshot=true` and `hibernationSnapshotNoDamageSideEffects=true`;
- project damage-bypass candidate count is now `63`.

Loop 37 correction:

- `FaunaBrain.ApplyFaunaInteraction` no longer applies multiplier bonus damage through blind `TakeDamage(bonusDamage)`;
- interaction bonus damage now calls `TakeDamageFromSource(bonusDamage, sourcePosition)`;
- source-aware reaction, parental-defense/fear stimulus, combat mirror sync, and impact presentation metadata stay tied to the actual interaction source;
- scanner proof records `faunaRegisteredTargetRoute.interactionBonusUsesSourceAwareDamage=true`;
- project damage-bypass candidate count is now `62`.

Loop 38 correction:

- player predator-bite damage now has owner fallback only when `TryQueuePredatorBiteDamage` fails before `CombatDamageRuntime` target registration;
- registered player bite targets still call the central queue and return true, so queue rejection cannot bypass LUT/CAS through direct damage;
- fallback route sends `DamagePacket` to `HectonPlayerHealth.ReceiveDamage` with finite local point, `CombatDamageTypes.Impact`, and source id `FaunaBite` or `FaunaLeviathanBite`;
- scanner proof records `faunaRegisteredTargetRoute.predatorBiteUnregisteredOwnerFallback=true` and keeps `predatorBiteDoesNotDirectFallbackOnQueueReject=true`;
- project damage-bypass candidate count remains `62`.

Loop 39 correction:

- `MantaEmergencyWreck` unregistered fauna fallback no longer calls blind `faunaBrain.TakeDamage(damage)`;
- `SubmarineAtmosphereSystem` boiling spillover fallback no longer calls blind `faunaBrain.TakeDamage(damageAmount)`;
- both registration-gap fallbacks now send owner `DamagePacket` payloads to `FaunaBrain.ReceiveDamage` with source id, damage type, and local point;
- registered Manta and boiling routes still call central `CombatDamageRuntime.TryQueueDamage` and return true after registration;
- scanner proof records `mantaWreckFaunaDamageRouteProof.unregisteredFallbackUsesOwnerPacket=true` and `thermalDamageRouteProof.submarineBoilingUnregisteredFallbackUsesOwnerPacket=true`;
- project damage-bypass candidate count is now `60`.

Loop 40 correction:

- `SargassumMicroFaunaBoids.ApplyLeviathanPhysicalStrike` registration-gap fallback no longer calls direct `_playerHealth.TakeLeviathanDamage`;
- fallback now sends owner `DamagePacket` to `HectonPlayerHealth.ReceiveDamage` with source id `FaunaLeviathanBite`, impact damage type, and target-local point;
- registered leviathan strike targets still call central `CombatDamageRuntime.TryQueueDamage` and return true after registration;
- scanner proof records `leviathanStrikeDamageRouteProof.unregisteredFallbackUsesOwnerPacket=true` and keeps registered no-direct-fallback proof true;
- project damage-bypass candidate count remains `60`.

Loop 41 correction:

- scanner now has a dedicated runtime external direct health-wrapper gate;
- `toolDamageRouteProof.projectExternalDirectTakeDamageCallCount=0`;
- `.TakeDamage(...)` / `.TakeLeviathanDamage(...)` external runtime calls must stay absent; broad damage-bypass inventory remains as owner-domain inventory, not a direct-call proof.

Compile/profiler proof: pending.

- Full build: `dotnet build Assembly-CSharp.csproj --no-restore /nr:false -p:UseSharedCompilation=false -v:minimal` failed on unrelated `Assets/_Project/Scripts/Editor/WorldProceduralGeologyFinalAuthoring.cs(235,17): CS0104 Object ambiguous` in `Hecton8.Editor.csproj`.
- Latest scoped runtime build: `dotnet build Assembly-CSharp.csproj --no-restore /nr:false -p:UseSharedCompilation=false -p:BuildProjectReferences=false -v:minimal` passed in `00:00:10.15`; warning `MSB9008` for missing `Hecton8.Input.csproj` remains.
- Loop 41 compile: not launched because latest guard sample was CPU `8.0%`, but 8 active compiler processes (`dotnet` plus `VBCSCompiler`) were present.
- Build rule: no build above `50%` CPU or while compiler processes run.
- Build status: CLI runtime slice compiled only; full Editor compile and Unity runtime proof were blocked by unrelated dependency.

## Forbidden

- Managed arrays, dictionaries, `UnityEvent`, or `TakeDamage` callbacks in the pellet fanout path.
- Direct particle, audio, renderer, or wound presentation calls from Burst jobs.
- `HectonEventBus` for hot combat truth.
- Trig angle-of-attack calculation.
- `GlobalRegistry` hot polling.

## Verification Status

Static scan artifact: `Docs/Reports/COMBAT_DAMAGE_PIPELINE_TARGET_LIST_X_008.json`.
Deferred feedback proof: `Docs/Reports/COMBAT_OPTIMIZATION_REPORT_X_008.json` section `deferredFeedbackProof`.
Compile/profiler evidence: pending.

- CPU guard blocked dotnet build while sampled CPU exceeded 50%.
- CPU guard also blocks while compiler processes are active.
