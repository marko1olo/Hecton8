# SHINOBU_329 Rationale

Status: PENDING VERIFICATION

## Decision Log

Problem: Missing disk-backed SHINOBU_329 state files at session start.
Solution: Create `Docs/Tasks/Status_SHINOBU_329.md` and this rationale file before code edits.
Rejected Alternatives: Chat-only tracking violates anti-amnesia and decision journaling requirements.
Scalability potential: Disk-backed state lets later agents continue without relying on compressed chat context.
Hardware Impact: No runtime impact; editor-only documentation write.

Problem: Batch prompt state must survive context compression and repeated user escalation.
Solution: Re-read `Docs/Tasks/CURRENT_BATCH.md` via CLI `Select-String`; the `SHINOBU_329` block is present at lines 3845..3951 and matches the 20-task status matrix.
Rejected Alternatives: Trusting chat memory or neighboring prompt context would violate strict parsing.
Scalability potential: None at runtime; prevents cross-agent prompt contamination.
Hardware Impact: No runtime impact.

Problem: Legacy respawn medbay row was 64 bytes with terrain-clearance fields not required by the assignment ABI.
Solution: Replace it with `MedicalBayDTO` explicit 32 bytes: `double3 BayAUP` at 0, `uint AssociatedBaseHash` at 24, `uint Flags` at 28; active and powered bits gate selection.
Rejected Alternatives: Keeping terrain-clearance validation wastes one extra cache line per two medbays and ties respawn truth to terrain sampling.
Scalability potential: Low: 32-byte rows reduce cache bandwidth; Middle: linear nearest search stays contiguous; High/Ultra: saved CPU budget feeds Dear Lie shader richness rather than extra CPU physics.
Hardware Impact: For i3/MX350 class silicon, medbay scan bandwidth halves versus legacy row size.

Problem: Respawn reset covered vitals/metabolism but did not flush gas physiology toxemia/narcosis state.
Solution: Add the existing `GasPhysiologyStateDTO` Vault row to handle acquisition, lock ordering, pointer resolution, and `ResetPlayerPhysiologyJob`; write surface O2/N2 and zero CO2/CNS/narcosis/stamina flags in the same job.
Rejected Alternatives: Managed post-job cleanup or owner-side polling would create an extra phase dependency and stale one-frame toxic visuals.
Scalability potential: Low through Ultra uses identical truth reset; visual intensity only scales through Dear Lie shader values.
Hardware Impact: Adds one 32-byte write and one buffer lock, replacing multiple possible managed repairs after death.

Problem: Inventory death penalty used current Transform position, not the actual AUP death coordinate.
Solution: Make `PlayerInventory` resolve `DeathAUP` from the existing `PlayerRespawnSignal` snapshot by sequence, then use existing `TryDropOneItemToWorldSignalAup`.
Rejected Alternatives: Direct `PlayerInventory` dependency on physiology DTOs violates compile wall; expanding `InventoryCommandSignal` would break ABI; instantiating loot objects at death would add managed scene churn.
Scalability potential: Low: deterministic small scatter around death AUP; Middle/High/Ultra: same authoritative drop route, presentation can overdraw via VFX without changing inventory truth.
Hardware Impact: One SignalBus snapshot scan per penalty command; avoids Transform dependency as primary position route and avoids cross-domain Vault reads.

Problem: Death-domain scene reload debt needed a repeatable proof artifact, not a chat claim.
Solution: Add `Scene_Reload_Scanner` editor facade plus dedicated/shared JSON report showing zero death-domain reload findings while leaving Core boot/menu scene loading outside scope; shared report upsert now replaces stale SHINOBU_329 entries instead of returning on existing keys.
Rejected Alternatives: Removing `SceneRuntimeService.LoadSceneAsync` would sabotage legitimate scene authority and is unrelated to player death; stale shared report keys are not acceptable proof in a multi-agent batch.
Scalability potential: No runtime cost; prevents future OOP reload regression.
Hardware Impact: No runtime impact; editor-only scanner allocations.

Problem: Respawn telemetry needed actual dropped-item count, but respawn does not own inventory rows.
Solution: Keep inventory as truth owner; after `TryApplyRespawnDropPenalty` performs actual drops, it emits `InventoryRespawnPenaltyResultSignal`; respawn owner consumes that signal and writes bits 16..23 of the latest `RespawnTelemetryEntry.Flags`.
Rejected Alternatives: Guessing drop count in the respawn job would be false telemetry; allowing inventory to write respawn telemetry would violate one-owner Vault law; expanding `InventoryCommandSignal` would break its 32-byte ABI.
Scalability potential: Low through Ultra records identical truth; visual loot presentation can scale independently after the authoritative count is written.
Hardware Impact: One 32-byte result signal plus one locked telemetry row update after an uncommon death event; no per-frame cost.

Problem: Inventory failure paths could return without publishing a result, leaving respawn telemetry with an emitted-penalty flag but no owner-confirmed count.
Solution: Publish `InventoryRespawnPenaltyResultSignal` with `DroppedCount=0` on grid/rule-table failure, and make the respawn telemetry update clear `PenaltyApplied` unless the owner reports a nonzero drop count.
Rejected Alternatives: Keeping the emitted-command flag as telemetry truth would misrepresent actual inventory outcome; letting respawn inspect inventory rows would violate owner boundaries.
Scalability potential: Low through Ultra carries the same 32-byte result payload; no quality-dependent truth split.
Hardware Impact: Adds at most one uncommon 32-byte zero-result signal on a death edge case; no frame-loop cost.

Problem: Compile verification cannot be launched under project guard while CPU/compiler are active.
Solution: Sampled CPU at 77% and detected `VBCSCompiler.exe`; build deferred until guard clears.
Rejected Alternatives: Running `dotnet build` during active compiler load violates local hardware protection and user instruction.
Scalability potential: No runtime effect; protects iteration hardware.
Hardware Impact: Avoided additional compile load on already saturated CPU.

Problem: Medical capsule data needed a human-editable source without scene dependencies.
Solution: Add cold `respawn_medical_bays.csv` ingestion through the existing Vault CSV scratch buffer; each row hydrates `MedicalBayDTO` with `AssociatedBaseHash`, AUP, active bit, and powered bit.
Rejected Alternatives: Authoring capsules as scene objects would reintroduce scene search and managed lifecycle coupling; expanding DTO with authoring strings would break blittable layout.
Scalability potential: Low: missing CSV keeps mock lifepod medbays; Middle/High/Ultra: authored dense bay networks remain a contiguous 32-byte scan.
Hardware Impact: Cold file read only; hot path stays fixed-buffer linear scan.

Problem: First compile attempt could not reach SHINOBU_329 diagnostics because `Hecton8.Core.csproj` failed on unrelated missing `RadiationStateDTO` and `VRSomatic*DTO` symbols.
Solution: Treat as external compile wall; do not edit radiation or VR somatic ownership lanes from this task.
Rejected Alternatives: Cross-domain emergency fixes would violate domain boundary and likely collide with active agents.
Scalability potential: No runtime effect.
Hardware Impact: One guarded compile attempt consumed 48.99 seconds; second attempt deferred after CPU rose to 84%.

Problem: `InventoryRespawnPenaltyResultSignal` was initially placed in a new Core contract file that generated project files had not included.
Solution: Move the 32-byte signal payload into the already compiled `GlobalSignals.cs` signal-contract surface beside `InventoryCommandSignal`, add direct lane configure/flush/clear/dispatch entries plus a size guard, and delete the uncompiled sidecar file.
Rejected Alternatives: Editing generated `.csproj` files would be brittle under Unity regeneration; adding a Physiology reference to Inventory would violate compile-wall routing.
Scalability potential: Low through Ultra keeps the result route as one 32-byte SignalBus payload; no gameplay truth changes with quality.
Hardware Impact: Removes a compile/include hazard; hot cost is one uncommon 32-byte queue flush on death result frames.

Problem: The new inventory result lane needed the same offset proof as the older command and respawn signals.
Solution: Add `InventoryRespawnPenaltyResultSignalSizeBytes=32` and explicit public-field offset validation to `ShinobuRespawnLayoutGuards`.
Rejected Alternatives: Relying only on Core `ValidateSignalSize<T>` would prove size but not semantic field offsets.
Scalability potential: No runtime quality split; deterministic telemetry ABI remains stable across all hardware.
Hardware Impact: Editor/development guard only; no player hot-path cost.

Problem: The new inventory result lane was registered, flushed, and validated, but IL2CPP closed-generic preservation still listed only the original signal lanes.
Solution: Add `PreserveLane<InventoryRespawnPenaltyResultSignal>()` to the cold `SignalBusAotPreserve.PreserveGenerics()` anchor.
Rejected Alternatives: Trusting generic use-sites alone creates player-build-only AOT risk; adding reflection or runtime warmup would violate static dispatch discipline.
Scalability potential: No quality split; preserves the same 32-byte signal route on all platforms.
Hardware Impact: 0 us runtime hot-path cost; cold linker anchor only.

Problem: Third compile verification attempt is still blocked by project hardware guard.
Solution: Sampled CPU at 97% and confirmed no `dotnet.exe`, `csc.exe`, or `VBCSCompiler.exe`; build remains deferred because CPU exceeds the 50% policy threshold.
Rejected Alternatives: Launching a build under saturated CPU would violate the explicit user/project instruction and distort diagnostics.
Scalability potential: No runtime effect; protects local iteration stability.
Hardware Impact: Avoided adding compiler load to an already saturated workstation.

Problem: Fourth compile verification attempt is blocked by both CPU and active MSBuild worker processes.
Solution: Sampled CPU at 86% and found seven `dotnet.exe` MSBuild nodeReuse processes; no build launched.
Rejected Alternatives: Competing with active MSBuild workers would violate the compile-wall protection rule and produce noisy diagnostics.
Scalability potential: No runtime effect.
Hardware Impact: Avoided compounding compiler contention on an already loaded workstation.

Problem: Fifth compile verification attempt remained unsafe after waiting.
Solution: Waited 45 seconds; CPU first sampled 38%, but WMI process query was denied, fallback `Get-Process` found seven active `dotnet` workers, and repeat CPU sampled 100%, so build stayed deferred.
Rejected Alternatives: Ignoring active worker processes after a transient low CPU sample risks colliding with another agent's build.
Scalability potential: No runtime effect.
Hardware Impact: Preserved compile-wall discipline and avoided overlapping compiler load.

Problem: Compile verification after the AOT preserve patch is still blocked by project hardware guard.
Solution: Sampled CPU at 96% and found active `csc.exe` plus `dotnet.exe` compiler/MSBuild processes; no build launched.
Rejected Alternatives: Launching another build into active compiler load violates the explicit guard and can corrupt diagnostic ownership.
Scalability potential: No runtime effect.
Hardware Impact: Avoided competing with an active compilation already consuming the workstation.

Problem: Subagent audit found Task 05 lacked the named lethal-pressure test job.
Solution: Added `GenerateMockLethalDamageJob`, a deterministic Burst `IJob` that repopulates contiguous priority `MedicalBayDTO` rows and enqueues both `PlayerFatalPressureSignal` proof and `PlayerRespawnSignal` route packets through typed SignalBus writers.
Rejected Alternatives: Waiting for live drowning tests leaves the respawn math untestable in isolation; using scene objects to fake capsules reintroduces managed lifecycle debt.
Scalability potential: Low through Ultra use the same deterministic mock truth; visual richness remains shader-only.
Hardware Impact: Cold/editor test job only; no gameplay frame cost unless deliberately invoked.

Problem: Nearest medbay selection was duplicated on the main thread before the reset job.
Solution: Removed the main-thread resolver from `WriteRequestFromSignal`; added `FindNearestMedicalBayJob` and scheduled it before `ResetPlayerPhysiologyJob` over the same locked Vault buffers.
Rejected Alternatives: Keeping managed pre-resolution would violate the Burst nearest-search task and duplicate AUP math.
Scalability potential: Low: linear 32-byte row scan remains cache-dense; Middle/High/Ultra: priority tie-breaks allow authored bay preference without changing nearest-distance truth.
Hardware Impact: Eliminates one managed loop over medbay rows per death and keeps the search under Burst SIMD-friendly pointer reads.

Problem: Death visual fade was previously written by the reset job, allowing audit to interpret it as a post-teleport effect.
Solution: `TryPrimeDeathSequenceFade` now writes a full black/chromatic/grain `RespawnFadeDTO` scalar during request staging before the reset job is scheduled; VisualSync publishes the already-black Vault payload.
Rejected Alternatives: Rendering a visible teleport and fading afterward breaks the Dear Lie; delaying reset across frames risks losing the same-frame inventory AUP signal route.
Scalability potential: Low: solid-black scalar with cheap grain; Middle/High/Ultra: continuous `GlobalQualityWeight` drives chromatic and grain values.
Hardware Impact: One 32-byte Vault write before reset; avoids any scene reload or post-processing object mutation.

Problem: Medical bay CSV source name and schema did not match the XML contract.
Solution: Switched primary cold ingest to `medical_bay_profiles.csv`, retained `respawn_medical_bays.csv` as legacy fallback, and parsed active/powered/priority tokens into the existing 32-byte `MedicalBayDTO.Flags`.
Rejected Alternatives: Adding strings or new DTO fields would break blittable 32-byte ARM64 layout; scene-authored medbay discovery would break zero-GC routing.
Scalability potential: Low through Ultra keeps one fixed row ABI; priority only resolves exact-distance ties and does not alter authority.
Hardware Impact: Cold boot only; hot medbay rows remain 32 bytes.

Problem: Respawn debug gizmo did not expose unpowered bays or the routed death-to-target line.
Solution: The Scene View gizmo now draws active powered bays green, inactive/unpowered rows red, and the last `RespawnRequestDTO.DeathAUP` to `RespawnStateDTO.TargetAUP` route in yellow.
Rejected Alternatives: Spawning marker GameObjects would add editor scene ownership and managed churn.
Scalability potential: Editor-only; no player runtime impact.
Hardware Impact: No gameplay impact; editor visualization reads Vault arrays only.

Problem: Inventory death penalty still had a Transform-space fallback if the respawn signal was missing or sequence-mismatched.
Solution: Removed the Transform fallback from `TryApplyRespawnDropPenalty`; if the inventory owner cannot resolve the authoritative death AUP from `PlayerRespawnSignal`, it publishes a zero-drop result and leaves inventory truth unchanged.
Rejected Alternatives: Dropping loot at current player Transform after respawn corrupts corpse-run location and violates the AUP route.
Scalability potential: Low through Ultra uses identical inventory truth; presentation can scale independently.
Hardware Impact: Removes one Transform read and prevents wrong-location managed drop attempts on mismatch.

Problem: Invalid death AUP signals were rejected before blackbox telemetry.
Solution: Added `TryWriteRejectedDeathTelemetry`, which records sanitized invalid death input into the 300-frame ring with `NanDetected|InvalidTargetAup` flags so PostSimulation can dump the forensic buffer.
Rejected Alternatives: Early return without telemetry violates the blackbox rule and leaves no crash breadcrumb.
Scalability potential: No quality split; telemetry truth remains identical.
Hardware Impact: Fault path only; locks two Vault telemetry buffers and writes one 64-byte entry.

Problem: Scene reload scanner did not cover the mandated Player/Core surface and its report was stale.
Solution: Extended scanner scope to Player/Core/Gameplay/Physiology/Combat while excluding only `SceneRuntimeService` boot/menu authority and `RuntimeWatchdog` fatal process exit; dedicated/shared JSON now report 375 scanned files and 0 findings.
Rejected Alternatives: Deleting Core boot scene loading or watchdog exit from this task would sabotage unrelated authority routes.
Scalability potential: Editor-only proof artifact.
Hardware Impact: No runtime impact.

Problem: Compile verification after the runtime polish patch is still blocked by project hardware guard.
Solution: Sampled CPU at 100% and found active `dotnet.exe` plus `csc.exe`; no build launched.
Rejected Alternatives: Launching another build into active compilation violates the user's explicit guard.
Scalability potential: No runtime effect.
Hardware Impact: Avoided additional compiler contention.

Problem: Follow-up compile verification remained unsafe after waiting for active compilers to exit.
Solution: Waited two 45-second windows; compiler processes were gone, but CPU sampled 70% then 100%, so build stayed deferred under the >50% CPU guard.
Rejected Alternatives: Launching `dotnet build` on a saturated workstation violates the explicit project policy and would produce noisy timings.
Scalability potential: No runtime effect.
Hardware Impact: Avoided adding build load to non-compiler CPU saturation.

Problem: Respawn inventory penalty still routed actual dropped items through the generic managed world-drop helper, which can touch persistent registry/event presentation lanes instead of a pure data-only loot cache.
Solution: Added `InventoryDeathLootCacheSignal=128`, registered/flushed/cleared/AOT-preserved it beside the existing Core signal lanes, changed respawn penalty removal to publish this AUP-anchored payload, and taught `LootMagnetSystem` to materialize it as `DataOnlyDeathCache` Vault rows without a pickup proxy.
Rejected Alternatives: Keeping `TryDropOneItemToWorldSignalAup` for death loot preserves managed registry/event churn; expanding respawn runtime to mutate inventory-owned arrays violates ownership; forcing a loot job `.Complete()` when the cache signal arrives would risk a hidden frame stall.
Scalability potential: Low: a bounded data-only cache row is enough for corpse-run pickup; Middle/High/Ultra: the same truth row can drive richer magnet/acoustic/wake presentation through existing signal budgets.
Hardware Impact: Removes managed object/registry/event route from the respawn death-drop path; expected uncommon death-edge saving is 5-20 us on i3/MX350-class silicon plus avoided heap/scene churn.

Problem: A death-cache signal can arrive while `LootMagnetSystem` has an outstanding scheduled pull job and locked Vault views.
Solution: Drain uses existing Vault views only (`allowAllocate:false`), and if the pull job is still scheduled or the cache view is unavailable, the current `InventoryDeathLootCacheSignal` snapshot is requeued into SignalBus for the next frame instead of forcing completion or losing removed inventory truth.
Rejected Alternatives: Force-completing the pull job violates dispatcher-owned completion windows; dropping the snapshot corrupts corpse-run loot; allocating/growing Vault buffers in LateFrame violates hot-path memory discipline.
Scalability potential: Low through Ultra preserves the same loot truth; only telemetry marks `TelemetryDeathCacheDeferredFlag` when requeue is required.
Hardware Impact: Requeue cost is one bounded 128-byte signal copy per death-cache row on a busy loot frame, avoiding both job-stall and managed allocation.

Problem: Compile verification after the data-only loot cache patch is still blocked by project guard.
Solution: Sampled CPU at 54% and found active `VBCSCompiler.exe` PID 24996; no build launched.
Rejected Alternatives: Launching `dotnet build` above 50% CPU or while a compiler server is active violates the explicit user/project instruction.
Scalability potential: No runtime effect.
Hardware Impact: Avoided overlapping compiler load on a saturated workstation.

Problem: Follow-up compile verification remained blocked after waiting.
Solution: Waited 45 seconds; CPU sampled 63% and the same `VBCSCompiler.exe` PID 24996 remained active, so build stayed deferred.
Rejected Alternatives: Launching into active compiler load would violate the compile-wall and hardware guard rules.
Scalability potential: No runtime effect.
Hardware Impact: Avoided compiler contention while another compilation server remained live.

Problem: Compile verification remains blocked on the next guard check.
Solution: Sampled CPU at 94% and found `VBCSCompiler.exe` PID 24996 still active; no build launched.
Rejected Alternatives: Starting `dotnet build` above the 50% CPU ceiling or with an active compiler server would violate the explicit project guard.
Scalability potential: No runtime effect.
Hardware Impact: Avoided adding compiler pressure to a saturated workstation.

Problem: Data-only death cache recovery preserved AUP and item hash but re-added loot through the generic inventory add path, losing per-item genetics, quality, and state flags after corpse-run recovery.
Solution: Reused explicit tail padding in `InventoryDeathLootCacheSignal=128` for `StateFlags@80`, reused explicit tail padding in `LootMagnetSignalEvent=128` for genetics/quality/state sideband, and added an explicit state-preserving inventory add overload consumed only by `DataOnlyDeathCache` recovery.
Rejected Alternatives: Adding a new persistent sidecar buffer would enlarge Vault ownership and invite drift; using `_pickupEntityIds` for hidden metadata would be an ambiguous managed sidecar; falling back to `TryAddItem` corrupts inventory SOA truth.
Scalability potential: Low: fixed 128-byte cache rows restore exact item identity with no scene proxies; Middle/High/Ultra: the same row can drive richer pickup presentation while state ownership remains identical.
Hardware Impact: No new allocations or capacity growth; the recovery path pays a few scalar stores in existing cache-line rows and avoids downstream correction churn from lost item state.

Problem: `InventoryDeathLootCacheSignal` was not included in the SignalBus finite guard table after becoming a first-party AUP lane.
Solution: Added a typed finite guard that sanitizes non-finite `PositionAup`, clamps invalid zero quantity to one, clamps quality to 1000, and tags the payload flags with the high fault bit.
Rejected Alternatives: Relying only on downstream loot validation lets corrupted payloads enter frame snapshots and weakens blackbox causality.
Scalability potential: No quality split; corrupted payload handling must be identical on all tiers.
Hardware Impact: One type-cache branch and scalar guard per pushed death-cache signal; death frequency is low and bounded by the signal lane budget.

Problem: Compile verification after the death-cache identity preservation patch remained unsafe.
Solution: Ran the guard check; CPU sampled 100% and `VBCSCompiler.exe` PID 6564 was active, so no `dotnet build` was launched.
Rejected Alternatives: Starting a build above the 50% CPU ceiling or while a compiler server is active violates the explicit compile-wall policy.
Scalability potential: No runtime effect.
Hardware Impact: Avoided adding compiler pressure to a saturated workstation.

Problem: `LootMagnetJob` overwrote `LootMagnetSignalEvent` on acquisition, which would erase the death-cache genetics/quality/state sideband right before `LootMagnetSystem` restored the item.
Solution: Removed the write-only constraint from the signal event row, cached the existing row at the start of each job index, preserved a flagless sideband row when no event is emitted, and copied sideband metadata into emitted acquisition or presentation events.
Rejected Alternatives: Adding a parallel metadata buffer would create a new Vault row family; using managed sidecars would violate the death-cache zero-GC route; skipping job-side preservation would silently corrupt recovered item identity.
Scalability potential: Low through Ultra shares the same metadata preservation; presentation frequency still scales through existing `PresentationSignalStride` and budgets.
Hardware Impact: Adds one 128-byte row read only for `DataOnlyDeathCache` loot indices in the existing loot job, and prevents identity loss without new allocation or synchronization.

Problem: Compile verification after the `LootMagnetJob` sideband patch remained unsafe.
Solution: Ran the guard check; no compiler process was active, but CPU sampled 84%, so no `dotnet build` was launched.
Rejected Alternatives: Starting a build above the 50% CPU ceiling violates the user's explicit hardware guard even when no compiler server is active.
Scalability potential: No runtime effect.
Hardware Impact: Avoided adding MSBuild load to an already saturated workstation.

Problem: Follow-up compile verification remained unsafe after waiting.
Solution: Waited 45 seconds; no compiler process was active, but CPU sampled 97%, so build stayed deferred.
Rejected Alternatives: Launching MSBuild during non-compiler CPU saturation violates the same explicit guard and produces unreliable timings.
Scalability potential: No runtime effect.
Hardware Impact: Avoided competing with the saturated workstation.

Problem: `LootMagnetSignalEvent` became part of the death-cache identity ABI, but only the self-audit XML proved its offsets.
Solution: Added `LootMagnetLayoutGuards.ValidateSignalEventLayout()` beside the loot contracts to assert the 128-byte size and offsets for position, event fields, genetics, quality, and state flags.
Rejected Alternatives: Relying on Markdown-only ABI proof is insufficient once the event row carries authoritative corpse-run identity; adding a new respawn-owned guard would increase cross-domain coupling.
Scalability potential: Layout is invariant across quality tiers; presentation budgets may scale, but identity ABI cannot.
Hardware Impact: No hot-path impact unless explicitly called by editor/bootstrap validation; prevents silent ARM64 layout drift.

Problem: The post-layout-guard patch needed verification without violating the compile guard.
Solution: Ran scoped `git diff --check`, XML parse, and reload-token scan; diff hygiene is clean except Git CRLF normalization warning, the self-audit XML parses, and reload strings remain confined to `Scene_Reload_Scanner` editor literals.
Rejected Alternatives: Treating the documentation/code patch as verified without parser/static proof; launching `dotnet build` before CPU/compiler guard clears.
Scalability potential: No runtime quality split; this preserves the no-scene-reload proof surface.
Hardware Impact: Static checks complete in sub-millisecond shell/runtime work compared with a full Unity project build.

Problem: Compile verification after static pass 16 remains blocked by the hardware guard.
Solution: Sampled CPU at 54% and found no `dotnet.exe`, `csc.exe`, or `VBCSCompiler.exe`; no build launched because CPU still exceeds the 50% ceiling.
Rejected Alternatives: Starting MSBuild at 54% CPU would violate the explicit compile-wall guard.
Scalability potential: No runtime effect.
Hardware Impact: Avoided adding build load while the workstation remained above the allowed CPU threshold.

Problem: `PlayerInventory_SoaQuery.cs` is an untracked partial file, so `git diff --check` does not validate its hygiene.
Solution: Ran explicit conflict-marker and trailing-whitespace scans on the file; both returned clean.
Rejected Alternatives: Treating a tracked-file diff check as sufficient while an untracked runtime partial carries death-cache routing code.
Scalability potential: No runtime split; preserves deterministic inventory routing proof.
Hardware Impact: Static scan only; no runtime impact.

Problem: Compile verification remains blocked even after CPU load dropped below the numeric ceiling.
Solution: Waited 30 seconds; CPU sampled 35%, but seven `dotnet.exe` workers and `VBCSCompiler.exe` PID 28268 were active, so build remained deferred under the active-compiler guard.
Rejected Alternatives: Starting a competing build while another compiler server/MSBuild worker set is active would violate diagnostic ownership.
Scalability potential: No runtime effect.
Hardware Impact: Avoided overlapping compiler contention despite transient acceptable CPU load.

Problem: The respawn reset job enqueues `InventoryCommandSignal` after PreSimulation, so inventory can receive the command on the next frame after the original `PlayerRespawnSignal` snapshot has been cleared.
Solution: Added `InventoryRespawnDeathAupSignal=64` as an inventory-specific sideband emitted beside the command, registered/guarded/AOT-preserved the lane, and made inventory resolve DeathAUP from that sideband before same-frame respawn-signal fallback.
Rejected Alternatives: Emitting a duplicate committed `PlayerRespawnSignal` would be visible to KCC/AI consumers and could extend collision bypass; packing double3 AUP into the 32-byte command would break ABI or precision; Transform fallback corrupts corpse-run position.
Scalability potential: Low through Ultra uses one bounded 64-byte sideband signal per death penalty command; visual loot richness still scales independently through the loot presentation budgets.
Hardware Impact: Adds one uncommon 64-byte signal copy on death, preventing zero-drop false negatives without managed lookups or scene search.

Problem: The route correction touched Core signal ABI, respawn jobs, inventory consumption, and XML route documentation.
Solution: Re-ran scoped `git diff --check`, self-audit XML parse, and reload-token scan; results were clean except CRLF normalization warnings and editor scanner literals.
Rejected Alternatives: Waiting for a blocked full build as the only feedback loop would leave obvious XML/static regressions unchecked.
Scalability potential: No runtime split; sideband signal capacity is fixed and bounded.
Hardware Impact: Static verification avoided active compiler contention while covering the modified route surface.

Problem: Compile verification after route correction remained unsafe.
Solution: Sampled CPU at 100% and found active `dotnet.exe` workers plus `VBCSCompiler.exe` PID 28268; build stayed deferred.
Rejected Alternatives: Launching another build under saturated CPU and active compiler server would violate the project guard and contaminate diagnostics.
Scalability potential: No runtime effect.
Hardware Impact: Avoided adding another MSBuild process to an already saturated compiler window.

Problem: Route proof artifacts still described only the original respawn signal path after adding the inventory sideband.
Solution: Updated `Scene_Reload_Scanner`, dedicated physics report, shared physics report, route card, ledger, and self-audit to include `InventoryRespawnDeathAupSignal`; XML and JSON parse checks pass.
Rejected Alternatives: Letting scanner output overwrite the corrected report with a stale route string.
Scalability potential: No runtime split; documentation now matches the bounded sideband signal route.
Hardware Impact: No runtime impact; prevents audit drift.

Problem: The reserve `PlayerRespawnSignal` DeathAUP lookup matched only `Sequence`, which is sufficient for the normal sideband route but too weak if the sideband is missing in a co-op frame.
Solution: Keep `InventoryRespawnDeathAupSignal` as the primary route and make the fallback accept `PlayerRespawnSignal` only when `PlayerHash` is zero or matches the inventory owner's resolved hash.
Rejected Alternatives: Removing fallback entirely would lose backward compatibility with same-frame legacy request snapshots; keeping sequence-only fallback risks wrong corpse-run AUP when two players die in overlapping sequence windows.
Scalability potential: Low through Ultra uses identical truth matching; visual loot richness remains downstream presentation-only.
Hardware Impact: Adds one cached owner-hash compare only on death penalty fallback, estimated under 2 us and no allocation.

Problem: Build verification after route hardening still had to obey the CPU/compiler guard.
Solution: Ran static proof instead: scoped diff hygiene, strict conflict-marker scan, XML/JSON parse checks, and reload-token scan; compile stayed deferred because CPU sampled 81% and seven `dotnet.exe` workers were active.
Rejected Alternatives: Launching `dotnet build` under active MSBuild workers violates the explicit compile-wall policy.
Scalability potential: No runtime effect.
Hardware Impact: Avoided overlapping compiler contention.

Problem: Death producers emitted `PlayerRespawnSignal.PlayerHash = PLYR`, while `PlayerInventory` consumes `InventoryCommandSignal` only when the command hash is zero or matches its real owner hash.
Solution: Added a hash overload to `PlayerDeathReconciliationBridge.RequestRespawn` and routed `HectonPlayerHealth` plus `HectonSurvivalSystem` through their player GameObject entity hash.
Rejected Alternatives: Making `PLYR` a wildcard in inventory would corrupt co-op by letting every inventory consume one death command; removing inventory hash filtering would break owner-local routing.
Scalability potential: Low through Ultra uses identical ownership routing; presentation remains independent.
Hardware Impact: One entity-id fold at the rare death edge, estimated under 3 us; prevents skipped inventory penalty without scene search or managed registry fallback.

Problem: Compile verification finally cleared the local CPU/compiler guard, but the project build failed before reaching a clean SHINOBU_329 verdict.
Solution: Ran one guarded `dotnet build Hecton8.Core.csproj --disable-build-servers -p:UseSharedCompilation=false /m:1` at 14 percent sampled CPU with no compiler workers; recorded the 72-error external wall and kept fixes out of SubmarineAutoLevelBallastController, VRSomaticProvider, CombatDamageRuntime_StatusEffects, SubmarineDynamicsRuntime, and TetherManager.
Rejected Alternatives: Editing those unrelated ownership lanes from a respawn reconciliation task would violate the domain boundary and collide with active agents; re-running builds without code changes would waste the compile window.
Scalability potential: No runtime quality split; compile ownership is a production velocity constraint, not a gameplay scaler.
Hardware Impact: One guarded build consumed 47.6 seconds; no additional build launched after the external wall.

Problem: Respawn telemetry consumed `InventoryRespawnPenaltyResultSignal` by sequence only, which is too weak when two co-op players can die in overlapping sequence windows.
Solution: Cache the last respawn request `PlayerHash`, clear it on rejected/cleared requests, and accept inventory penalty result rows only when the sequence matches and the inventory hash is either unspecified or matches the cached player hash.
Rejected Alternatives: Sequence-only matching can write another player's dropped count into the local blackbox; querying inventory owner state from respawn would violate owner-local routing.
Scalability potential: Low through Ultra keeps identical telemetry truth; presentation quality cannot affect the owner hash route.
Hardware Impact: One uint compare on rare penalty result frames, under 1 us on i3/MX350-class CPU.

Problem: `ResetPlayerPhysiologyJob` sanitized `DeathAUP` before request staging, but a corrupted Vault request row could still feed NaN into fallback medbay scoring and telemetry if memory was damaged between phases.
Solution: Sanitize `request.DeathAUP` inside the reset job, tag `NanDetected|InvalidTargetAup` when the original request is not finite, use the sanitized death AUP for fallback medbay delta math, and write sanitized death AUP into telemetry.
Rejected Alternatives: Trusting prevalidation alone weakens blackbox causality; rejecting inside the job would leave physiology state half-routed after the Dear Lie blackout.
Scalability potential: Identical across all quality tiers; fault handling does not change gameplay truth or DTO layout.
Hardware Impact: One finite check and a few scalar selects per death reset, under 1 us; prevents non-finite propagation into KCC/telemetry.

Problem: Acquired data-only death-cache slots were marked `Acquired` with `Active` cleared by the Burst pull job, but owner recovery cleared inactive slots before checking acquired state.
Solution: Reorder `TryCommitDataOnlyDeathCacheAcquisition` so acquired `DataOnlyDeathCache` rows recover through `TryAddItemWithState` before stale inactive cleanup; tighten `IsDataOnlyDeathCacheSlot` to accept active or acquired rows and preserve identity.
Rejected Alternatives: Clearing inactive rows first loses corpse-run items; keeping a managed sidecar of acquired loot would violate zero-GC and duplicate Vault truth.
Scalability potential: Low through Ultra uses the same data-only row; richer presentation can scale downstream without changing recovered item identity.
Hardware Impact: Same-row flag checks only, estimated under 2 us on recovery frames; avoids re-drop or repair churn.

Problem: When the data-only loot cache had no reusable slot, `LootMagnetSystem` could drop a signal after `PlayerInventory` had already removed the item.
Solution: Requeue the saturated `InventoryDeathLootCacheSignal` and set deferred/saturated telemetry flags; `FindInactiveLootCacheSlot` now refuses rows carrying active, acquired, item-hash, or quantity metadata.
Rejected Alternatives: Losing the signal corrupts inventory truth; forcing loot job completion would violate dispatcher-owned completion windows; growing buffers in the hot path violates Vault law.
Scalability potential: Low through Ultra keeps the same bounded signal route; quality can only affect presentation cadence, not cache truth.
Hardware Impact: Saturation path copies one 128-byte signal and avoids both heap churn and a forced completion stall.

Problem: If the primary movement/context AUP source was temporarily unavailable at the lethal edge, survival and health producers could fall back to legacy managed death events instead of requesting respawn reconciliation.
Solution: Add a final finite AUP fallback built from `AbsoluteUniversePosition.FromRuntimePosition(...)` using cached/runtime position before the legacy death path. The signal route still receives a double3 AUP and player hash when the runtime origin is valid.
Rejected Alternatives: Allocating `PlayerDiedEvent` on ordinary pose-source misses keeps heap pressure in the death path; using current Transform as the primary route would violate the AUP authority order.
Scalability potential: Low through Ultra uses identical death authority; quality only changes Dear Lie presentation.
Hardware Impact: One runtime-origin AUP conversion on rare pose-source misses, estimated under 2 us; avoids managed event allocation on those death edges.

Problem: Subagent audit found that bridge failure could still enter legacy managed death handlers.
Solution: Remove death-route calls to `PublishPlayerDeath`, `OnDeath`, `PlayerDiedEvent`, and `enabled=false`; make `PlayerDeathReconciliationBridge` pre-sanitize invalid or missing AUP into a bounded fallback request with invalid flags; let `ShinobuRespawnReconciliationRuntime` accept that request into Vault reset and carry `NanDetected|InvalidTargetAup` through the job and commit transformer.
Rejected Alternatives: Retaining a managed failure path creates heap/event churn exactly at death; deriving death truth from `Transform.position` keeps scene state in the authority route; rejecting invalid AUP requests writes blackbox only but leaves physiology/KCC unreset.
Scalability potential: Low through Ultra uses identical fail-closed authority routing; only Dear Lie shader intensity scales with `GlobalQualityWeight`.
Hardware Impact: Removes rare `PlayerDiedEvent` allocation/event fanout and one managed Transform fallback from death; adds two uint flag operations and one sanitized double3 select on the death edge, under 4 us on i3/MX350-class CPUs.

Problem: Ledger proof still described the superseded runtime-origin fallback and legacy-death failure route after code had been purged.
Solution: Update `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` to state the actual sanitized invalid-flagged SignalBus/Vault route, no Transform/runtime-origin fallback, no managed death event fallback, and the KCC request/commit distinction for `InvalidDeathAup`.
Rejected Alternatives: Editing KCC to accept invalid request packets would let malformed request-phase packets consume the collision-bypass generation; leaving ledger stale would create false audit evidence.
Scalability potential: Low through Ultra uses identical KCC truth: invalid request packets wait for finite committed target, while shader detail still scales continuously.
Hardware Impact: Runtime code unchanged in this pass; proof correction prevents future regression. The already-applied route saves rare managed event fanout and costs only bounded flag propagation.

Problem: Post-purge compile verification would collide with active local build workers.
Solution: Sampled CPU at 56.8 percent and found seven `dotnet.exe` workers; deferred `dotnet build` under the explicit >50% CPU / active compiler guard.
Rejected Alternatives: Launching another build would violate user policy, compete with existing MSBuild nodes, and not produce clean ownership diagnostics.
Scalability potential: No runtime impact; preserves iteration hardware and diagnostic ownership.
Hardware Impact: Avoided adding MSBuild load to an already active workstation.

Problem: Health/survival producers still hid missing AUP by substituting a finite fallback before calling the bridge, so `InvalidDeathAup` forensic flags were not guaranteed on pose-source failure.
Solution: Replace producer-side finite fallback with a non-finite sentinel; `PlayerDeathReconciliationBridge` remains the single sanitizer, converts the sentinel to bounded fallback AUP, and tags `InvalidDeathAup|InvalidTargetAup`.
Rejected Alternatives: Keeping finite producer fallback would make missing AUP indistinguishable from a legitimate fallback-coordinate death; moving sanitation into both producers would duplicate authority and drift from the bridge proof.
Scalability potential: Low through Ultra uses identical truth route; only shader intensity scales, while invalid-source telemetry stays deterministic.
Hardware Impact: Three scalar NaN writes on rare missing-AUP death edges; no per-frame cost and no allocation.

Problem: Compile verification after the missing-AUP sentinel patch would collide with active local build workers.
Solution: Sampled CPU at 100 percent and found seven `dotnet.exe` workers; deferred build under the same compile-wall guard.
Rejected Alternatives: Running `dotnet build` under saturated CPU and active MSBuild workers violates the user policy and would contaminate diagnostics.
Scalability potential: No runtime impact.
Hardware Impact: Avoided adding build load to a saturated workstation.

Problem: Scene_Reload_Scanner report generation still described the pre-sentinel route as a generic PlayerRespawnSignal path.
Solution: Update the editor scanner generator and both dedicated/shared JSON proof artifacts to state the bridge-sanitized PlayerRespawnSignal route.
Rejected Alternatives: Hand-editing only JSON reports would be overwritten by the next scanner run and create audit drift.
Scalability potential: No runtime impact; keeps static proof aligned across low/mid/high/ultra routes.
Hardware Impact: Editor/report-only text change.

Problem: Respawn and death-cache signal lanes carried gameplay truth but their low-tier frame limits were lower than max frame limits, so signal flushing could theoretically discard death/loot truth under low quality or system stress.
Solution: Set `LowTierFrameSignals` equal to `MaxFrameSignals` for `PlayerRespawnSignal`, `InventoryRespawnDeathAupSignal`, `InventoryDeathLootCacheSignal`, and `InventoryRespawnPenaltyResultSignal`.
Rejected Alternatives: Relying on priority CSV/runtime tuning is not acceptable for authority truth; `GlobalQualityWeight` may scale presentation, not respawn identity, inventory loss, or corpse-run truth.
Scalability potential: Low through Ultra keep identical truth capacity. Presentation still scales through Dear Lie shader scalars and downstream loot presentation budgets.
Hardware Impact: Worst-case snapshot capacity is unchanged at existing max values; low-end devices no longer gain microseconds by corrupting rare death-route truth.

Problem: `ResetPlayerPhysiologyJob` sanitized corrupted `request.DeathAUP` for medbay fallback and telemetry, but emitted the unsanitized request value in `InventoryRespawnDeathAupSignal`.
Solution: Pass the already sanitized `deathAup` into `EmitInventoryPenalty` and write that value into the sideband.
Rejected Alternatives: Letting inventory reject non-finite sideband AUP after a successful Vault reset would create a respawn with missing corpse-run truth; duplicating sanitation in inventory would split authority.
Scalability potential: Fault handling is invariant across all tiers; only visual richness scales.
Hardware Impact: No new math beyond passing an existing `double3`; prevents a rare zero-drop false negative after corrupted Vault request state.

Problem: Inventory removed an item before publishing `InventoryDeathLootCacheSignal`; if SignalBus finite guards rejected the cache signal, inventory truth was already lost.
Solution: Use `SignalBus<InventoryDeathLootCacheSignal>.TryPush` for the initial enqueue and restore the removed item with `TryAddItemWithState` if the signal is rejected.
Rejected Alternatives: Keeping fire-and-forget `Push` hides rejection; forcing LootMagnet completion or direct cross-domain Vault mutation would violate dispatcher/Vault ownership.
Scalability potential: Low through Ultra keep the same data-only loot route; downstream cache saturation still requeues without gameplay-truth loss.
Hardware Impact: Adds one boolean branch on rare death-drop rows. Restore path is fault-only and replaces item loss with bounded inventory mutation.

Problem: Compile verification after the truth-lane patch reached the project build but failed outside SHINOBU_329.
Solution: Ran one guarded build at CPU 31.3 percent with no compiler workers; recorded external missing `FluidCompartmentDTO` and `SolarConditionsDTO` diagnostics.
Rejected Alternatives: Editing Airlock/Solar ownership lanes from respawn reconciliation would violate domain boundary and collide with active agents.
Scalability potential: No runtime effect.
Hardware Impact: One guarded build consumed 23.2 seconds; no further build retries launched after external wall.

Problem: Self-audit Task 20 still described compile as pending CPU/compiler gate after Compile pass 46 had already run and failed externally.
Solution: Update self-audit wording to `PASS_STATIC_RUNTIME_COMPILE_BLOCKED_EXTERNAL`, update the SHINOBU_329 ledger boundary date to 2026-05-23, and parse XML/JSON proof artifacts after the change.
Rejected Alternatives: Leaving stale proof text would mislead the integrator into waiting for a CPU gate instead of resolving Airlock/Solar missing DTO contracts.
Scalability potential: No runtime effect; proof discipline protects authority-route ownership.
Hardware Impact: Documentation-only. No build rerun because code did not change after the guarded external compile wall.

Problem: `LootMagnetSystem` could drop already-removed corpse-run loot truth if existing Vault views resolved but writable capacity collapsed to zero, and deferred/saturated requeue used fire-and-forget `Push`.
Solution: Requeue `InventoryDeathLootCacheSignal` on zero writable capacity, switch deferred/saturated requeue to `TryPush`, and set `TelemetryDeathCacheRequeueRejectedFlag` if SignalBus rejects the payload.
Rejected Alternatives: Returning on zero capacity loses inventory-owned truth; force-completing the pull job violates dispatcher-owned completion windows; mutating inventory directly from the loot fault path duplicates ownership.
Scalability potential: Low through Ultra keep identical corpse-run truth capacity. Quality can still scale presentation, acoustic/wake VFX, and telemetry cadence, never the death-cache authority route.
Hardware Impact: Adds one branch per rare requeued death-cache signal and avoids a potential item-loss repair path; estimated under 2 us on i3/MX350-class CPUs.

Problem: Compile verification after the requeue hardening patch reached MSBuild but still failed before SHINOBU_329 ownership.
Solution: Ran one guarded `dotnet build .\Hecton8.Core.csproj --disable-build-servers -p:UseSharedCompilation=false /m:1` at 43.2 percent CPU with no compiler workers; recorded the unchanged external Airlock/Solar DTO wall.
Rejected Alternatives: Editing `SolarPanel` or Airlock pressurization DTO ownership from a player respawn task violates the domain boundary and would collide with active agents.
Scalability potential: No runtime effect; preserves compile-wall ownership while keeping respawn code diagnostics separated from external dependencies.
Hardware Impact: One guarded build consumed 28.98 seconds; no further build retry launched after the external wall.
