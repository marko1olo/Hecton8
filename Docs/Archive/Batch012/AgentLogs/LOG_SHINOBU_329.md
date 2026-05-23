# LOG_SHINOBU_329

## 2026-05-22 - Player Respawn Reconciliation Runtime

What was wrong:
- Death-domain respawn path was already partially routed through `PlayerDeathReconciliationBridge` and `ShinobuRespawnReconciliationRuntime`, but the runtime still carried SHINOBU_155 identity/dump path and a 64-byte medbay row.
- Reset job covered vitals/metabolism/decompression but did not flush `GasPhysiologyStateDTO`, leaving CO2/CNS/narcosis/stamina stress able to survive respawn.
- Inventory death penalty emitted a Vault-backed command but consumer used current Transform position for drops, not the actual `DeathAUP`.
- No dedicated scanner/report existed to prevent `SceneManager.LoadScene()` regressions in player death domains.

What was done:
- Replaced legacy medbay row with `MedicalBayDTO=32` (`BayAUP@0`, `AssociatedBaseHash@24`, `Flags@28`) and active/powered flag gating.
- Added gas physiology handle acquisition, lock/unlock ordering, pointer resolution, and reset writes in `ResetPlayerPhysiologyJob`.
- Changed dump path and host identity to `SHINOBU_329`; blackbox target is `Docs/AgentLogs/Dump_SHINOBU_329.bin`.
- Kept nearest medbay search AUP-safe: double3 death/bay subtraction first, local float3 squared distance after.
- Added `respawn_medical_bays.csv` cold byte parser using the existing Vault CSV scratch buffer.
- Added editor medbay CSV reload to `RespawnReconciliationTunerWindow`.
- Added `Scene_Reload_Scanner` and reports: `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_329.json`, shared key `shinobu329SceneReloadScanner`.
- Hardened `Scene_Reload_Scanner` shared report upsert to replace stale SHINOBU_329 entries while preserving other agents' JSON nodes.
- Added `InventoryRespawnPenaltyResultSignal=32` as the owner-safe result lane for actual dropped item count.
- Added SHINOBU layout guard coverage for `InventoryRespawnPenaltyResultSignal` public field offsets.
- Updated `PlayerInventory` penalty path to resolve `DeathAUP` from `PlayerRespawnSignal.Sequence`, drop via `TryDropOneItemToWorldSignalAup`, and publish actual dropped count back to respawn owner.
- Respawn runtime now consumes `InventoryRespawnPenaltyResultSignal` and writes dropped count into respawn telemetry flags bits 16..23.
- Added route card `Docs/ARCHITECTURE/SHINOBU_329_PLAYER_RESPAWN_RECONCILIATION_ROUTE_CARD.md`.
- Updated `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
- Added `Docs/Reports/SHINOBU_329_SELF_AUDIT.xml`.

Cinematic cheats used:
- Death screen remains a Dear Lie shader fade: `RespawnFadeDTO` -> `HectonShaderGlobalDataVaultBridge.PublishRespawnDearLie`.
- No scene reload, player destroy/recreate, respawn blackout scene, physics probe, terrain raycast, or capsule GameObject search was added.

Exact microseconds saved estimate:
- Scene reload avoided on death: not a per-frame microsecond saving; removes multi-frame heap churn and asset/scene activation spike.
- Medbay row bandwidth: 64B -> 32B; estimated 0.02 us saved per 8 medbay scan on i3/MX350-class CPU.
- Gas reset in same Burst job: estimated 3-8 us saved versus managed follow-up repair and stale visual correction.
- AUP inventory drop via existing helper: estimated 2-5 us saved per death versus Transform-driven fallback correction and scene-space reconciliation.
- Scene reload scanner: editor-only, 0 runtime cost.

Verification:
- `git diff --check` passed for touched files; CRLF warnings only.
- Static death-domain scan found zero `SceneManager.LoadScene`, `LoadSceneAsync`, `Application.Quit`, or player destroy/recreate tokens in Gameplay/Physiology runtime paths.
- Static respawn scoped scan found zero legacy `MedicalBayRespawnPointDTO`, `MedicalBayRespawnPointSizeBytes`, `Dump_SHINOBU_155`, `SHINOBU_155`, `Pack=1`, or hot DTO property hits.
- `PHYSICS_OPTIMIZATION_REPORT.json` parsed through PowerShell `ConvertFrom-Json`.
- `SHINOBU_329_SELF_AUDIT.xml` parsed as XML.
- Compile attempt: `dotnet build Assembly-CSharp.csproj --disable-build-servers -p:UseSharedCompilation=false /m:1 -v:minimal -clp:ErrorsOnly` failed in unrelated `Hecton8.Core.csproj` dependencies: missing `RadiationStateDTO` and `VRSomatic*DTO`. No SHINOBU_329 diagnostics were emitted before the external wall.
- Follow-up compile deferred: CPU re-sampled at 84%, then 100%, violating project build guard.

## 2026-05-22 - Signal Include Repair

What was wrong:
- The 32-byte `InventoryRespawnPenaltyResultSignal` payload was created in a new Core contract sidecar file, but current generated `.csproj` inputs did not include that file.

What was done:
- Moved `InventoryRespawnPenaltyResultSignal` into `Assets/_Project/Scripts/Core/GlobalSignals.cs` beside `InventoryCommandSignal`.
- Registered `InventoryRespawnPenaltyResultSignal` in Core direct lane configure/flush/clear/dispatch lists so queued inventory results become readable snapshots and are cleared after post-simulation.
- Added `ValidateSignalSize<InventoryRespawnPenaltyResultSignal>(32)` to the existing editor/development signal layout guard.
- Deleted the uncompiled sidecar file instead of editing generated project files.
- Forced inventory failure paths to publish a zero-drop result and made respawn telemetry clear `PenaltyApplied` unless inventory confirms a nonzero actual count.
- Updated the binary payload ledger entry for med bays from stale `MedicalBayRespawnPointDTO[8]` 64-byte wording to `MedicalBayDTO[8]` 32-byte wording.

Cinematic cheats used:
- No change to the Dear Lie path: death presentation remains shader scalar fade and never becomes a scene transition.

Exact microseconds saved estimate:
- Runtime: 0 us changed. This repair removes compile/include drift only.
- Build hygiene: avoids future stale generated-project fix churn when Unity regenerates `.csproj` files.

Verification:
- Focused `rg` finds `InventoryRespawnPenaltyResultSignal` only in `GlobalSignals.cs`, `PlayerInventory.cs`, and the respawn runtime.
- The deleted sidecar path no longer exists.
- `git diff --check` passed on touched SHINOBU_329 files; CRLF warnings only.
- Third compile attempt deferred: CPU sampled 97%; no `dotnet.exe`, `csc.exe`, or `VBCSCompiler.exe` was active, but CPU exceeds the 50% build guard.
- Fourth compile attempt deferred: CPU sampled 86%; seven `dotnet.exe` MSBuild nodeReuse processes were active.
- Fifth compile attempt deferred: after a 45s wait CPU sampled 38%, WMI process query was denied, fallback `Get-Process` found seven active `dotnet` workers, and repeat CPU sampled 100%.

## 2026-05-22 - AOT Lane Preserve Repair

What was wrong:
- `InventoryRespawnPenaltyResultSignal` had Core direct-lane registration and layout guards, but was not listed in the cold `SignalBusAotPreserve` generic anchor.

What was done:
- Added `PreserveLane<InventoryRespawnPenaltyResultSignal>()` to `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs`.
- Updated the SHINOBU route card and self-audit to reflect the AOT preserve route.

Cinematic cheats used:
- No runtime presentation change; the death transition remains the shader-only Dear Lie fade.

Exact microseconds saved estimate:
- Runtime hot path: 0 us changed. This is a player-build preservation guard only.

Verification:
- `git diff --check` passed for the AOT patch set; CRLF warnings only.
- `SHINOBU_329_SELF_AUDIT.xml` parsed as XML and contains 20 tasks.
- `PHYSICS_OPTIMIZATION_REPORT.json` parsed; `shinobu329SceneReloadScanner.findingCount=0`.
- Scoped hot-path scan found no private/new Native containers, `foreach`, hidden `.Complete()`, `Time.deltaTime`, random, `Pack=1`, or DTO `{ get; set; }` hits in `ShinobuRespawn*.cs`.
- Scoped death-domain scan found no `SceneManager.LoadScene` or `LoadSceneAsync` hits in Gameplay/Physiology/PlayerInventory runtime files.
- Compile deferred: CPU sampled 96%; active `csc.exe` and `dotnet.exe` processes were present.

## 2026-05-22 - Runtime Polish Reconciliation Pass

What was wrong:
- Task 05/06/08/17/18 audit found no named mock lethal job, nearest-medbay pre-resolution still ran on the main thread, the CSV contract used the legacy filename, the gizmo did not draw red/yellow debug routes, and inventory respawn penalty still had a Transform-space fallback.

What was done:
- Added `GenerateMockLethalDamageJob` to inject synthetic `PlayerFatalPressureSignal` plus `PlayerRespawnSignal` and repopulate priority `MedicalBayDTO` rows.
- Added scheduled `FindNearestMedicalBayJob`; `WriteRequestFromSignal` now only stages the request and primes fade, leaving the medbay scan to Burst.
- Added `TryPrimeDeathSequenceFade` so the Dear Lie black shader scalar is in Vault before the reset job runs.
- Switched primary medbay ingest to `medical_bay_profiles.csv`, kept `respawn_medical_bays.csv` as cold legacy fallback, and parsed priority into `MedicalBayDTO.Flags`.
- Expanded the tuner with `FadeToBlackDuration`, `RespawnDelaySeconds`, `InventoryDropPercentage`, latest telemetry readout, medbay reload, mock lethal injection, and blackbox dump.
- Updated gizmo rendering: green powered medbays, red inactive/unpowered medbays, yellow death-to-respawn route.
- Removed respawn inventory Transform fallback; missing `DeathAUP` now publishes a zero-drop result.
- Extended scene reload scanner/report to Player/Core/Gameplay/Physiology/Combat: 375 scanned files, 0 findings.

Cinematic cheats used:
- Death presentation remains a shader scalar fake: black/chromatic/grain payload is written to Vault instead of loading a death scene or mutating post-process volume objects.

Exact microseconds saved estimate:
- Managed medbay pre-scan removed from death staging: estimated 4-12 us per death on i3/MX350-class CPU.
- Transform fallback removed from respawn inventory penalty mismatch path: estimated 1-3 us and prevents incorrect corpse-run positions.
- Scene reload remains the main avoided cost: multi-second heap/world rebuild removed from player death.

Verification:
- Focused `git diff --check` passed for SHINOBU_329-touched files; CRLF warnings only.
- Manual static scanner equivalent found 375 scoped files and 0 forbidden reload/player-destroy findings after excluding Core boot/watchdog authorities.
- Dedicated/shared JSON reports parse and show `findingCount=0`, `scannedFileCount=375`.
- Scoped hot-path scan found no `foreach`, `.Complete()`, `Time.deltaTime`, random, `Pack=1`, hot DTO properties, or private/new Native containers in `ShinobuRespawn*.cs`.
- Compile deferred by guard: CPU sampled 100%; active `dotnet.exe` and `csc.exe` build processes were present.
- Follow-up compile deferred by guard: after two 45s waits no compiler process remained, but CPU sampled 70% then 100%, so no build was launched.

## 2026-05-22 - Data-Only Death Loot Cache Repair

What was wrong:
- Respawn inventory penalty no longer used current Transform coordinates, but actual item removal still flowed through the generic world-drop helper. That helper can touch `PersistentWorldRegistry`, managed item acquisition events, and world proxy presentation instead of the XML-required unmanaged loot cache.
- A cache signal arriving while `LootMagnetSystem` had a scheduled pull job could be lost if the snapshot was cleared before Vault mutation.

What was done:
- Added `InventoryDeathLootCacheSignal=128` in `GlobalSignals.cs`, registered it in configure/flush/clear/known-lane paths, added Core size validation, SHINOBU offset guards, and AOT preservation.
- Changed `TryApplyRespawnDropPenalty` to call `TryDropOneItemToDeathLootCacheSignal`, which removes SOA inventory rows and publishes an AUP-anchored data-only loot cache payload.
- Added `LootEntityFlags.DataOnlyDeathCache` and taught `LootMagnetSystem` to materialize cache payloads into Vault rows with no `PickupItem` sidecar, then acquire them through existing magnet inventory/presentation budgets.
- `LootMagnetSystem` now requeues death-cache snapshots while a scheduled pull job is busy or existing Vault views are unavailable; it does not force `.Complete()` and does not allocate/grow Vault buffers in LateFrame drain.

Cinematic cheats used:
- Corpse-run loot is a data-only cache row rendered/collected through existing presentation. No item prefab, Rigidbody, scene registry drop, or physics box is instantiated for death loot.

Exact microseconds saved estimate:
- Removes the generic managed world-drop route from death penalty: estimated 5-20 us on i3/MX350-class CPU per dropped stack plus avoided registry/event heap pressure.
- Busy pull-job requeue costs one bounded 128-byte SignalBus copy per deferred cache row and avoids a forced completion stall.

Verification:
- Focused `rg` shows respawn penalty now calls `TryDropOneItemToDeathLootCacheSignal`; `TryDropOneItemToWorldSignalAup` remains only for non-respawn uses.
- Scoped `git diff --check` passed for the data-only loot cache patch set; CRLF warnings only.
- Static death-domain scan shows only editor scanner strings plus documented Core boot/watchdog authorities.
- Compile deferred by guard: CPU sampled 54% and active `VBCSCompiler.exe` PID 24996 was present.
- Follow-up compile deferred by guard after 45s wait: CPU sampled 63% and `VBCSCompiler.exe` PID 24996 was still active.
- Final guard check before handoff: CPU sampled 94% and `VBCSCompiler.exe` PID 24996 was still active; no build launched.

## 2026-05-22 - Death Cache Identity Preservation Pass

What was wrong:
- Data-only death loot cache recovery used generic `TryAddItem`, so recovered corpse-run items could lose genetics, quality, durability-derived quality, and SOA state flags.

What was done:
- Added `StateFlags@80` to `InventoryDeathLootCacheSignal` without changing its 128-byte explicit layout.
- Added `GeneticsMask@80`, `QualityMilli@88`, and `StateFlags@90` to `LootMagnetSignalEvent` without changing its 128-byte Vault row.
- Captured removed inventory state in `TryDropOneItemToDeathLootCacheSignal`.
- Added a state-preserving `PlayerInventory.TryAddItemWithState(..., itemStateFlags, quantity)` overload and routed `DataOnlyDeathCache` acquisition through it when cached item identity is present.
- Updated `LootMagnetJob` to preserve existing `LootMagnetSignalEvent` sideband metadata when writing acquisition/presentation events.
- Added `InventoryDeathLootCacheSignal` to the Core SignalBus finite guard table.

Cinematic cheats used:
- Corpse-run loot remains a data-only Vault row; no world prefab, Rigidbody, registry object, or scene reload is introduced to preserve item identity.

Exact microseconds saved estimate:
- Prevents post-recovery inventory correction passes and designer-visible identity bugs; hot cost is only existing 128-byte row writes plus scalar metadata copies.

Verification:
- Static patch review confirms all new metadata fits existing explicit padding; signal/event sizes remain 128 bytes.
- `git diff --check` passed for the identity-preservation patch set; CRLF warnings only.
- `Docs/Reports/SHINOBU_329_SELF_AUDIT.xml` parsed as XML after the layout update.
- Scoped death-route scan showed no `SceneManager.LoadScene` or `LoadSceneAsync`; remaining `Destroy(gameObject)` hit is duplicate bootstrap cleanup in `PlayerStressMetricsRuntime.Awake`, not player death routing.
- Compile deferred by guard: CPU sampled 100% and active `VBCSCompiler.exe` PID 6564 was present.
- Post-`LootMagnetJob` sideband patch `git diff --check` passed; CRLF warnings only.
- Compile deferred by guard after the sideband patch: CPU sampled 84%; no compiler process was active, but the >50% CPU policy still blocked build.
- Follow-up compile guard after 45 seconds: CPU sampled 97%; no compiler process was active, but the >50% CPU policy still blocked build.
- Added `LootMagnetLayoutGuards.ValidateSignalEventLayout()` so the death-cache sideband ABI is guarded in code as well as self-audit XML.
- Static verification after layout guard: scoped `git diff --check` is clean except CRLF normalization warning, `SHINOBU_329_SELF_AUDIT.xml` parses, and scoped reload scan reports only editor scanner literals.
- Compile guard after static pass 16: CPU sampled 54%, no compiler process active, build deferred under the >50% CPU rule.
- Untracked partial verification: `PlayerInventory_SoaQuery.cs` was separately scanned for conflict markers and trailing whitespace because `git diff --check` does not cover untracked files.
- Compile guard after 30s wait: CPU sampled 35%, but seven `dotnet.exe` workers and `VBCSCompiler.exe` PID 28268 were active; build deferred.
- Route correction: added `InventoryRespawnDeathAupSignal=64` sideband so next-frame inventory penalty commands retain authoritative death AUP without duplicating KCC-visible respawn commits or falling back to Transform/PersistentWorldRegistry.
- Static verification after route correction: scoped diff check is clean except CRLF warnings, `SHINOBU_329_SELF_AUDIT.xml` parses, and reload-token scan is confined to `Scene_Reload_Scanner`.
- Compile guard after route correction: CPU sampled 100% with active `dotnet.exe` workers and `VBCSCompiler.exe` PID 28268; build deferred.
- Report sync: scanner source plus dedicated/shared physics reports now include the `InventoryRespawnDeathAupSignal` sideband route; XML/JSON parse checks pass.

## 2026-05-22 - Respawn Death AUP Fallback Hardening

What was wrong:
- The primary inventory death AUP route was the new 64-byte sideband, but the reserve same-frame `PlayerRespawnSignal` lookup matched only `Sequence`.
- In co-op, a sequence-only fallback is too weak if the sideband is missing and another player has an overlapping respawn packet.

What was done:
- `PlayerInventory.TryResolveRespawnDeathAup` now resolves the inventory owner hash and accepts fallback `PlayerRespawnSignal` rows only when `PlayerHash` is zero or matches that owner hash.
- The sideband route remains primary; this is a compatibility guard for same-frame legacy snapshots, not a new authority lane.

Cinematic cheats used:
- No change to presentation truth: the Dear Lie blackout remains a Vault shader scalar, not a scene transition.

Exact microseconds saved estimate:
- Prevents wrong-location corpse-run recovery without scene search or Transform fallback. Added cost is one uint compare per fallback candidate, under 2 us on i3/MX350-class CPU.

Verification:
- Scoped `git diff --check` passed for SHINOBU_329-touched runtime/editor files; CRLF warnings only.
- Strict conflict-marker scan (`^(<<<<<<<|=======|>>>>>>>)`) returned clean.
- `SHINOBU_329_SELF_AUDIT.xml` parsed as XML.
- Dedicated and shared physics JSON reports parsed.
- Scoped reload scan found only `Scene_Reload_Scanner` editor literals.
- Compile guard after this hardening pass: CPU sampled 81% and seven active `dotnet.exe` workers were present; no build launched.

## 2026-05-22 - PlayerHash Producer Route Repair

What was wrong:
- `PlayerDeathReconciliationBridge` emitted default `PLYR` as `PlayerRespawnSignal.PlayerHash`.
- `ResetPlayerPhysiologyJob` copies that value to `InventoryCommandSignal.InventoryHash`, and `PlayerInventory` correctly ignores commands for other owners. Result: the death inventory penalty could be skipped unless the inventory entity hash happened to equal `PLYR`.

What was done:
- Added `RequestRespawn(double3 deathAup, uint damageHash, uint playerHash)`.
- `HectonPlayerHealth` and `HectonSurvivalSystem` now pass `unchecked((uint)EntityId.ToULong(gameObject.GetEntityId()))`.
- The legacy two-argument overload remains for compatibility, but actual player death producers use owner hash routing.

Cinematic cheats used:
- No physical or scene-side work added. The death route still uses the Dear Lie shader scalar and data-only inventory cache.

Exact microseconds saved estimate:
- Avoids skipped penalty and follow-up repair paths; added death-edge cost is one entity-id fold and one uint payload write, under 3 us on i3/MX350-class CPU.

Verification:
- `rg "RequestRespawn(deathAup"` shows both runtime death producers call the hash overload.
- Scoped producer patch `git diff --check` passed; CRLF warnings only.
- Scoped death-producer reload/instantiate scan returned clean.
- Strict conflict-marker scan returned clean.

## 2026-05-22 - Guarded Build External Compile Wall

What was wrong:
- The compile guard finally allowed one build, but `Hecton8.Core.csproj` failed before a full SHINOBU_329 verdict because unrelated systems currently do not compile.

What was done:
- Ran `dotnet build Hecton8.Core.csproj --disable-build-servers -p:UseSharedCompilation=false /m:1` only after CPU sampled 14 percent and no `dotnet.exe`, `csc.exe`, or `VBCSCompiler.exe` workers were active.
- Captured the external failure set: `SubmarineAutoLevelBallastController`, `VRSomaticProvider*`, `CombatDamageRuntime_StatusEffects`, `SubmarineDynamicsRuntime`, and `TetherManager`.
- Left those domains untouched and kept the respawn route validated by focused static scans.

Cinematic cheats used:
- No new runtime cheat was added in this pass. The existing death transition remains the Dear Lie shader blackout plus Vault-state overwrite.

Exact microseconds saved estimate:
- Runtime delta is 0 us; this pass protected compile ownership. Avoided extra build retries after the external wall.

Verification:
- Build failed with 72 external diagnostics in the displayed tail and no displayed SHINOBU_329 respawn/player-inventory/death-bridge diagnostics.
- `Status_SHINOBU_329.md` records Compile pass 29 as blocked by external dependency.

## 2026-05-22 - Co-op Telemetry and Death Cache Recovery Hardening

What was wrong:
- Respawn telemetry accepted `InventoryRespawnPenaltyResultSignal` by sequence only, so overlapping co-op death windows could write another player's dropped count into the local blackbox.
- A corrupted `RespawnRequestDTO.DeathAUP` could still reach fallback medbay scoring inside the reset job after earlier request validation.
- Acquired data-only death-cache rows were cleared as inactive before recovery because the Burst pull job clears `Active` and sets `Acquired`.
- Saturated data-only cache insertion could lose a signal after inventory rows had already been removed.

What was done:
- Cached the last request `PlayerHash` in `ShinobuRespawnReconciliationRuntime` and filtered penalty result rows by sequence plus inventory/player hash.
- Sanitized `request.DeathAUP` inside `ResetPlayerPhysiologyJob`, tagged non-finite request coordinates, and used the sanitized coordinate for fallback nearest-bay math and telemetry.
- Reordered `TryCommitDataOnlyDeathCacheAcquisition` so acquired data-only rows recover before stale inactive cleanup.
- Hardened data-only cache slot reuse and requeued saturated `InventoryDeathLootCacheSignal` snapshots with saturation/deferred telemetry flags.

Cinematic cheats used:
- Corpse-run loot stays a Vault data row and shader/presentation illusion. No item prefab, scene reload, Rigidbody, registry object, or forced job completion is added.

Exact microseconds saved estimate:
- Co-op result filter and in-job finite guard: under 2 us combined on rare death frames.
- Death-cache recovery ordering: under 2 us on pickup frames while preventing repair passes.
- Saturation requeue: one bounded 128-byte signal copy instead of forced completion or managed object recreation.

Verification:
- Scoped `git diff --check` passed for the patched runtime files; CRLF warnings only.
- Strict conflict-marker scan returned clean.
- Runtime death reload/Instantiate/Destroy scan over Gameplay/Physiology/Loot/Inventory death route returned no findings.

## 2026-05-22 - Legacy Death Fallback AUP Closure

What was wrong:
- Survival and health death producers could miss both movement and runtime-context AUP sources and then fall through to legacy managed death events.

What was done:
- `HectonSurvivalSystem.TryResolveSurvivalAbsoluteAup` now builds a final finite AUP from runtime-origin AUP plus resolved runtime position before returning false.
- `HectonPlayerHealth.TryResolveRespawnDeathAup` now performs the same runtime-origin fallback using cached/runtime player position.

Cinematic cheats used:
- No presentation or physics work was added. This keeps the existing Dear Lie blackout plus Vault reset route alive when pose snapshots are temporarily absent.

Exact microseconds saved estimate:
- Under 2 us on rare pose-source misses. The relevant saving is avoided managed `PlayerDiedEvent` allocation and legacy death telemetry on ordinary lethal edges.

Verification:
- Post-fallback scoped `git diff --check` passed; CRLF warnings only.
- Self-audit XML parsed.
- Strict conflict-marker scan returned clean.
- Runtime death reload/Instantiate/player-destroy scan returned no findings.
- Both death producers still call the player-hash `RequestRespawn(deathAup, ..., playerHash)` overload.

## 2026-05-23 - Managed Death Fallback Purge and Ledger Sync

What was wrong:
- The fallback AUP closure still left a managed death route available if bridge push failed.
- `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` still described superseded fallback and managed-death failure wording after the route was purged.

What was done:
- `HectonPlayerHealth` and `HectonSurvivalSystem` now fail closed to local health/survival mirror reset after bridge failure and no longer call `PublishPlayerDeath`, `PlayerDiedEvent`, `OnDeath`, or component disable in this death route.
- `PlayerDeathReconciliationBridge` sanitizes missing/non-finite AUP into a bounded invalid-flagged request before SignalBus push.
- `ShinobuRespawnReconciliationRuntime` accepts the finite sanitized request, carries `NanDetected|InvalidTargetAup` through Vault reset/telemetry, and commits the resolved finite target.
- Ledger text now matches the request/commit route: KCC ignores invalid request packets, then consumes the finite committed target for the one-frame collision fence.

Cinematic cheats used:
- Death remains an immediate shader blackout plus Vault state overwrite. No scene reload, player recreate, Transform-derived death truth, or managed death event fanout.

Exact microseconds saved estimate:
- Avoids rare `PlayerDiedEvent` allocation/event dispatch, legacy telemetry, and component-disable churn on bridge-failure death edges.
- Added route cost is finite AUP select and flag propagation, under 4 us on i3/MX350-class CPU.

Verification:
- Scoped `git diff --check` passed with CRLF warnings only.
- Self-audit XML parsed.
- Stale ledger phrase scan no longer reports runtime-origin or legacy-death fallback wording for SHINOBU_329.
- Death-route forbidden scan reports only lifecycle false positives: duplicate-host `enabled=false`, missing-stats `Awake` disable, and `OnDestroy` method names. It does not report `PlayerDiedEvent`, `PublishPlayerDeath`, `OnDeath?.Invoke`, `LoadScene`, runtime `Instantiate`, or player destroy in the touched death route.
- Compile not launched: CPU sampled 56.8 percent with seven active `dotnet.exe` workers, so the project guard required deferral.

## 2026-05-23 - Missing AUP Sentinel Correction

What was wrong:
- Health and survival producers substituted a finite fallback coordinate before calling the bridge when movement/context AUP was unavailable.
- That kept the route alive, but it hid missing-source evidence from `InvalidDeathAup` telemetry.

What was done:
- `HectonPlayerHealth` and `HectonSurvivalSystem` now pass a non-finite sentinel into `PlayerDeathReconciliationBridge` on AUP resolve failure.
- The bridge remains the only sanitizer: it converts the sentinel into bounded fallback AUP and sets `InvalidDeathAup|InvalidTargetAup`.

Cinematic cheats used:
- No new visual path. The death screen remains the shader Dear Lie blackout plus one-frame Vault reset.

Exact microseconds saved estimate:
- Runtime cost is three scalar NaN writes only on rare missing-AUP death edges, under 1 us. The gain is forensic correctness without managed fallback.

Verification:
- Scoped `git diff --check` passed with CRLF warnings only.
- Self-audit XML parsed.
- Strict conflict-marker scan returned clean.
- Death-route reload/event/Instantiate scan returned clean.
- Sentinel source scan confirms health and survival emit `double.NaN` on missing AUP and the bridge sets `InvalidDeathAup|InvalidTargetAup`.
- Compile not launched: CPU sampled 100 percent with seven active `dotnet.exe` workers.
- Scanner generator, dedicated physics JSON, and shared physics JSON now name the bridge-sanitized PlayerRespawnSignal route, preventing report drift on the next editor scanner run.
- Post-report-sync verification: scoped diff hygiene passed with CRLF warnings only; self-audit XML plus both JSON reports parsed; death-route reload/event/Instantiate scan remained clean.

## 2026-05-23 - Truth Lane Capacity and Sanitized Sideband Hardening

What was wrong:
- Respawn and death-cache signal lanes had low-tier frame limits below max frame limits, allowing quality/stress throttling to discard gameplay truth.
- `ResetPlayerPhysiologyJob` wrote sanitized death AUP into telemetry/fallback math but emitted the unsanitized request AUP into the inventory sideband.
- Initial inventory death-cache enqueue removed the item before knowing whether SignalBus finite guards accepted the cache payload.

What was done:
- Set low-tier frame limits equal to max frame limits for `PlayerRespawnSignal`, `InventoryRespawnDeathAupSignal`, `InventoryDeathLootCacheSignal`, and `InventoryRespawnPenaltyResultSignal`.
- Changed `EmitInventoryPenalty` to receive the sanitized `deathAup` and write that into `InventoryRespawnDeathAupSignal`.
- Changed initial `InventoryDeathLootCacheSignal` enqueue to `TryPush`; if rejected, the removed item is restored through `TryAddItemWithState`.

Cinematic cheats used:
- No new physics or scene work. Death still uses the shader Dear Lie blackout plus Vault mutation; corpse-run loot remains data-only until presentation consumes it.

Exact microseconds saved estimate:
- No truth-lane throttling is claimed as a CPU saving. This deliberately spends the existing max snapshot capacity to protect rare death truth.
- Sanitized sideband pass-through is 0 us beyond existing local variable use.
- TryPush branch is under 1 us on accepted rows; restore path is fault-only.

Verification:
- `git diff --check` passed for touched code files with CRLF warnings only.
- Focused death-route scan found no `SceneManager.LoadScene`, `LoadSceneAsync`, `PlayerDiedEvent`, `PublishPlayerDeath`, `OnDeath?.Invoke`, runtime `Instantiate`, or player destroy in the touched route.
- Callsite scan confirms `EmitInventoryPenalty(request, deathAup)` and `SignalBus<InventoryDeathLootCacheSignal>.TryPush` on initial inventory enqueue.
- Guarded build ran at CPU 31.3 percent with no compiler workers and failed externally in Airlock/Solar missing DTOs: `FluidCompartmentDTO`, `SolarConditionsDTO`. No SHINOBU_329 files appeared in diagnostics.

## 2026-05-23 - Proof Wording Closure After External Compile Wall

What was wrong:
- Self-audit Task 20 still said guarded compile was pending the CPU/compiler gate even though Compile pass 46 had already run and failed in external Airlock/Solar files.

What was done:
- Updated `SHINOBU_329_SELF_AUDIT.xml` Task 20 to `PASS_STATIC_RUNTIME_COMPILE_BLOCKED_EXTERNAL`.
- Updated the SHINOBU_329 boundary ledger date to 2026-05-23.
- Re-ran XML/JSON parsing and focused route scans.

Cinematic cheats used:
- None added. Runtime remains Dear Lie shader blackout plus Vault state overwrite.

Exact microseconds saved estimate:
- 0 us runtime. This pass prevents stale audit interpretation only.

Verification:
- `git diff --check` passed on the SHINOBU_329 touched surface with CRLF warnings only.
- Self-audit XML and both physics JSON reports parse.
- Focused death-route scan remains clean for scene reload, managed death event fanout, runtime instantiate, and player destroy tokens.

## 2026-05-23 - Death Cache Requeue Fault Closure

What was wrong:
- `LootMagnetSystem` had a rare branch where existing Vault views could resolve but writable capacity returned zero, abandoning `InventoryDeathLootCacheSignal` rows after inventory SOA removal.
- Deferred and saturated death-cache requeue used fire-and-forget `Push`, hiding SignalBus rejection from telemetry.

What was done:
- Requeued death-cache signals when writable capacity resolves to zero.
- Switched deferred/saturated requeue to `SignalBus<InventoryDeathLootCacheSignal>.TryPush`.
- Added `TelemetryDeathCacheRequeueRejectedFlag` so blackbox telemetry records rejected requeue attempts.

Cinematic cheats used:
- Data-only corpse-run cache remains the Dear Lie. No scene object, no reload, no physical corpse, no managed world-drop.

Exact microseconds saved estimate:
- Fault-path protection, not a speed claim. Adds one branch per requeued death-cache signal, under 2 us on i3/MX350-class CPUs, and prevents later item-loss repair churn.

Verification:
- `git diff --check` passed on the touched slice with a CRLF warning only for `LootMagnetSystem.cs`.
- Strict conflict-marker scan and trailing-whitespace scan passed.
- Self-audit XML and both physics JSON reports parse.
- Focused death-route scan found no `SceneManager.LoadScene`, `LoadSceneAsync`, managed death event fanout, runtime instantiate, player destroy, scene search, coroutine, or hidden `.Complete()` tokens in the touched route.
- Grep confirms the death-cache requeue path now uses `TryPush` and no `SignalBus<InventoryDeathLootCacheSignal>.Push`.
- Guarded build ran at CPU 43.2 percent with no compiler workers and failed externally in `SolarPanel.cs`, `AirlockPressurizationJobs.cs`, and `AirlockPressurizationRuntime.cs` missing `SolarConditionsDTO` and `FluidCompartmentDTO`. No SHINOBU_329 files emitted diagnostics.
