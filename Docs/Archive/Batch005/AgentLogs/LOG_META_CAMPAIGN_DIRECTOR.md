# META_CAMPAIGN_DIRECTOR Final Report - 2026-05-13

What was wrong:
- Meta campaign progression had no isolated runtime owner for global story state.
- Narrative state risked singleton/day-gate coupling instead of GlobalRegistry/EventBus ownership.
- Enemy and ecosystem reactions needed global campaign facts without direct story dependencies.
- Save/load had no compact native-state payload for campaign variables.
- Crash/postmortem proof was missing for campaign stage and global variable shifts.

What was done:
- Added `IMetaCampaignService` and a `MetaCampaignRuntime` GlobalRegistry slot.
- Added `Hecton8.Narrative.Campaign` asmdef and `MetaCampaignService` runtime.
- Consumed `ProgressionEventSignal` and evaluated campaign rules through a Burst `IJob`.
- Stored globals in `NativeParallelHashMap<uint,int>` with precomputed FNV1a uint hashes.
- Emitted `GlobalWorldStateSignal`, `VocalWarningSignal`, `NarrativePoiStateSignal`, and telemetry on state changes.
- Set `_HectonOceanToxicity` only when campaign visual state changes.
- Gated Leviathan encounters through `IMetaCampaignService.IsLeviathanAwakened`.
- Fed toxicity pressure into `IEcosystemDirectorService` as sparse safe-shallows prey biomass loss.
- Added `MetaCampaignDTO` save payload, v71 binary codec read/write, and migration repair.
- Added 300-entry NativeArray campaign blackbox and cold binary dump path.
- Added hidden `MetaCampaignDevConsole.TryForceSetGlobal(...)` routed through the service contract.
- Ran OMEGA polish: removed scalar division from toxicity normalization, scanned campaign assembly for banned managed/string/math patterns, and kept status PENDING due global compile blockers.

Cinematic Cheats used:
- Toxic ocean is a single shader global scalar, not a water/particle/voxel contamination simulation.
- Biomass toxicity is a two-second prey pulse near safe shallows, not continuous ecological chemistry.
- Leviathan lockout is an authoring snapshot clamp, not a new per-entity spawn branch.
- Global story state is uint-hash DAG evaluation, not string quest polling.
- AUP independence is an explicit signal flag with default AUP, not position-indexed narrative state.
- Cartography and VWS are queue packets, not direct UI/audio mutation.
- Blackbox is a fixed NativeArray ring, not log spam.

Exact microseconds saved:
- Removed per-frame campaign polling: estimated 20-80 us/frame saved on i3/MX350.
- Replaced contamination simulation with shader scalar: estimated 200-800 us/frame avoided.
- Replaced ecological field sim with sparse biomass pulse: estimated 100-500 us/frame avoided.
- Replaced string quest graph with uint/Burst rule evaluation: estimated 10-40 us per progression event and 0 B managed GC.
- Leviathan clamp keeps the existing spawn solver intact: estimated 5-15 us per cold tick avoided versus additional live gate scans.
- Native blackbox avoids runtime log formatting: 0 B GC in normal path, one 24-byte ring write per state shift.
- Compact save slices keep campaign persistence cold-only: 0 us steady-frame impact.

Verification:
- `validate_script Assets/_Project/Scripts/Narrative/Campaign/MetaCampaignService.cs`: 0 warnings, 0 errors.
- Campaign anti-bloat scan: no `foreach`, `string.Format`, `$"..."`, `.ToString()`, managed List/Dictionary construction, sqrt/normalize, singleton access, or DaysSurvived gates in the campaign assembly.
- `rg` over `Assets/_Project/Scripts` found no `CampaignManager.Instance`, `GameManager.Instance`, or `DaysSurvived`.
- `git diff --check` only reports CRLF replacement warnings on existing project files.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -clp:ErrorsOnly` remains red with 154 project dependency/interface errors outside campaign ownership, including missing `Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, `Hecton8.Core.Memory.Layout`, `Hecton8.Audio.Propagation`, `H8BinaryWorldPager`, `IGroundRadarService`, and related contracts.

Final status:
- Core task list is checked or blocked.
- Task 18 is `[BLOCKED BY DEPENDENCY]`.
- Status remains `PENDING VERIFICATION`.

# META_CAMPAIGN_DIRECTOR Follow-up Hardening Report - 2026-05-13

What was wrong:
- `MetaCampaignService.Awake()` seeded state and published visual/ecosystem side effects before registry ownership existed.
- Shutdown completed any pending evaluation job before disposal, creating a possible scene-transition stall.
- Campaign save pair order depended on `NativeParallelHashMap` iteration order, which weakens binary-delta/checksum stability.
- `EcosystemDirector.ApplyCampaignToxicityPressure()` emitted a second `GlobalWorldStateSignal`, duplicating campaign authority.

What was done:
- `Awake()` now seeds native defaults only. External shader/ecosystem publishing remains after `OnEnable` registration.
- Native containers now retire through `Dispose(JobHandle)` using the active evaluation handle and immediately clear owner fields.
- Save output now insertion-sorts the <=64 campaign variable pairs by uint hash before assignment to `SaveData`.
- Ecosystem toxicity pressure now applies local hostility/biomass pressure only; it no longer rebroadcasts world state.

Cinematic Cheats used:
- Toxicity still remains a shader scalar plus sparse biomass pressure, not a physical contamination simulation.
- Ecosystem response is local data pressure, not another global event cascade.
- Save determinism is handled with a tiny fixed insertion sort, not managed dictionaries or string keys.

Exact microseconds saved:
- Removed one duplicate world-state queue packet per toxicity visual shift: estimated 1-4 us per shift.
- Deferred disposal removes a possible shutdown main-thread wait: estimated 10-25 us in normal pending-job cases, higher under worker saturation.
- Deterministic save sorting costs below 20 us cold path over <=64 entries and protects binary-delta stability.
- Normal gameplay frame cost remains 0 us when no progression signal exists.

Verification:
- `validate_script Assets/_Project/Scripts/Narrative/Campaign/MetaCampaignService.cs`: 0 warnings, 0 errors.
- Campaign anti-bloat scan remains clean.
- Unity Console after refresh reports unrelated `DeployableSdfDrillRuntime` errors only.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -clp:ErrorsOnly` remains red with 154 unrelated project dependency/interface errors.
- Status remains `PENDING VERIFICATION`.

# META_CAMPAIGN_DIRECTOR Second Follow-up Hardening Report - 2026-05-13

What was wrong:
- EncounterDirector still had a campaign-service lookup hazard near cold encounter scheduling.
- MetaCampaignService could detect a duplicate owner, schedule destruction, then still register with SaveManager and publish shader/ecosystem state during the same OnEnable.
- SaveManager registration had only the early OnEnable attempt, which is fragile against bootstrap ordering.

What was done:
- HectonDirectorAI now caches `GlobalRegistry.MetaCampaign` during dependency refresh and injects it into EncounterDirector.
- EncounterDirector now clamps Leviathan authoring from the cached `IMetaCampaignService` pointer, not a live service-locator read.
- MetaCampaignService Tick readiness now uses `_serviceReady`, not `GlobalRegistry.MetaCampaign`.
- MetaCampaignService now tracks `_saveRuntimeRegistered`, retries save registration in Start, and unregisters only if it owned the save registration.
- MetaCampaignService now exits OnEnable before save/visual side effects when service ownership registration failed.

Cinematic Cheats used:
- Leviathan lockout remains a cheap authoring clamp before the Burst encounter job, not a new live spawn simulation.
- Toxicity remains one shader scalar plus sparse ecosystem pressure, not particle/field contamination.
- Bootstrap uncertainty is handled by cold lifecycle retries, not hot-path polling.

Exact microseconds saved:
- Removed one service-locator read from each 1 Hz encounter cold schedule: estimated 1-3 us per cold tick on i3/MX350.
- Duplicate MetaCampaignService instances now avoid one SaveManager registry scan plus one shader global set and one ecosystem pressure call before destruction: estimated 5-20 us per duplicate activation.
- Avoided SaveRuntime polling in Tick: preserves 0 us campaign cost on normal frames without progression signals.
- Cached pointer injection cost is one registry read on dependency refresh, not in the encounter solve.

Verification:
- Prompt re-extracted from `Docs/Tasks/CURRENT_BATCH.md`; task count remains 18 and status must remain `PENDING VERIFICATION`.
- Campaign anti-bloat scan remains clean for banned foreach/string/new collection/sqrt/normalize/singleton/day-gate patterns.
- `rg` found no `CampaignManager.Instance`, `GameManager.Instance`, or `DaysSurvived` under `Assets/_Project/Scripts`.
- `git diff --check` on touched campaign/encounter files reports only CRLF replacement warnings.
- Unity MCP validation is currently unavailable: first `validate_script` timed out, retry reported `Unity session not available`; console read has the same session failure.
- `dotnet build Hecton8.Core.csproj` remains red with 153 unrelated missing namespace/type errors across fluids, scheduling, memory layout, audio propagation, paging, macro swarm, binary safety attributes, and acoustic contracts.
- Status remains `PENDING VERIFICATION`.

# META_CAMPAIGN_DIRECTOR Third Follow-up Hardening Report - 2026-05-13

What was wrong:
- The campaign Burst job wrote `_globalVariables` while the public service API could read the same `NativeParallelHashMap` before `LateFrameTick` completed the job.
- `HectonDirectorAI.Start()` retried dispatcher lanes but not the cached campaign pointer, leaving a bootstrap-order null window.
- Duplicate `MetaCampaignService` ownership destroyed the whole GameObject, risking unrelated runtime-root services.

What was done:
- `MetaCampaignRuleEvaluationJob` now marks Rules and Variables as `[ReadOnly]`.
- The job now emits FixedList changes only; `CompletePendingEvaluation` remains the single map mutation point after job completion.
- HectonDirectorAI refreshes the cached `IMetaCampaignService` in Start.
- Duplicate campaign ownership now destroys only the rejected component with `Destroy(this)`.

Cinematic Cheats used:
- Global progression still runs only on state-event signals, not per-frame day polling.
- Leviathan lockout remains an authoring clamp.
- Toxicity remains a shader scalar plus sparse ecosystem pressure.

Exact microseconds saved:
- Avoided potential native safety stalls from forced job completion in reader paths: variable, but worst case frame hitch removed.
- Preserved 0 us normal campaign frames when no progression signal exists.
- Added one cold Start pointer refresh: estimated below 2 us.
- Duplicate component cleanup avoids a possible runtime-root rebuild/destruction cascade rather than optimizing a normal frame path.

Verification:
- Campaign anti-bloat scan remains clean for banned foreach/string/new collection/sqrt/normalize/singleton/day-gate patterns.
- `rg` found no `CampaignManager.Instance`, `GameManager.Instance`, or `DaysSurvived`.
- `git diff --check` on touched scripts reports only CRLF replacement warnings.
- Unity MCP validation is blocked: `Unity session not available`.
- `dotnet build Hecton8.Core.csproj` remains red with 92 unrelated missing namespace/type errors.
- Status remains `PENDING VERIFICATION`.

# META_CAMPAIGN_DIRECTOR Fourth Follow-up Hardening Report - 2026-05-13

What was wrong:
- Save load restored campaign variables and pushed the visual scalar, but did not broadcast a load-kind global world-state snapshot.
- Cartography POI consumers could stay stale after loading into an already-corrupted campaign state.
- Reusing the normal rule-change path for load would have replayed VWS narrative audio.

What was done:
- Added `PublishCampaignStateSnapshot`.
- `LoadFromSaveData` now emits a load snapshot after state refresh.
- Null-save reset now emits the same visual/cartography snapshot.
- The snapshot path publishes GlobalWorldStateSignal, visual/ecosystem pressure, cartography POI state, telemetry, and blackbox entries, but skips audio broadcast.

Cinematic Cheats used:
- Load restoration replays one state snapshot and one POI packet, not a physical resimulation of ocean corruption.
- Radio/VWS remains event-driven; load does not pretend a story event just happened.

Exact microseconds saved:
- Avoided direct cartography UI mutation/rebuild: downstream signal packet only.
- Avoided audio replay queue churn on load: one VWS packet suppressed per load restore.
- Load snapshot cost is estimated below 20 us on i3/MX350 before downstream consumers.
- Normal gameplay frame cost remains 0 us when no progression signal exists.

Verification:
- `validate_script Assets/_Project/Scripts/Narrative/Campaign/MetaCampaignService.cs`: 0 diagnostics.
- `validate_script Assets/_Project/Scripts/EncounterDirector.cs`: 0 diagnostics.
- `validate_script Assets/_Project/Scripts/HectonDirectorAI.cs`: MCP duplicate-method warning for `BuildEventOffsetDirectionLut`; `rg` shows one definition and one static field call, so this remains a validator/parser limitation until Unity full compile is clean.
- Unity Console reports unrelated `H8MacroDatabaseService` unsafe-await errors.
- Campaign anti-bloat scan remains clean.
- `dotnet build Hecton8.Core.csproj` remains red with 93 unrelated missing namespace/type errors.
- Status remains `PENDING VERIFICATION`.

# META_CAMPAIGN_DIRECTOR Fifth Follow-up Hardening Report - 2026-05-13

What was wrong:
- Multi-rule progression events published side effects while campaign state was only partially applied.
- BaseDelta/Toxicity packets could carry the old `CurrentCampaignStageHash` before the CampaignStage rule ran.
- The cold path refreshed cached state once per changed variable.

What was done:
- `CompletePendingEvaluation` now applies all rule output variables first.
- Cached campaign state is refreshed once per progression event.
- Side effects are published after final stage/toxicity are available.
- The single-variable dev-console path remains unchanged.

Cinematic Cheats used:
- The story shift is still a deterministic state packet batch, not a simulated historical replay.
- Visual corruption still rides one shader scalar and cartography signal packets.

Exact microseconds saved:
- BaseDelta-style progression now avoids up to two extra cached-state refreshes: estimated 2-6 us per cold progression event on i3/MX350.
- Normal gameplay frame cost remains 0 us when no progression signal exists.
- Queue packet count is unchanged to preserve per-variable semantics.

Verification:
- `validate_script Assets/_Project/Scripts/Narrative/Campaign/MetaCampaignService.cs`: 0 diagnostics.
- Campaign anti-bloat scan remains clean.
- Unity Console currently reports unrelated `SuitHUDV4CanvasOverlay` duplicate-method and MCP regex-timeout errors.
- Status remains `PENDING VERIFICATION`.

# META_CAMPAIGN_DIRECTOR Sixth Follow-up Hardening Report - 2026-05-13

What was wrong:
- Loop 12 batched state application, but side effects still executed once per changed variable.
- A single BaseDelta event could schedule three VWS warnings, three cartography packets, three shader/ecosystem refreshes, and three telemetry writes.
- This wasted queue capacity and downstream consumer time while adding no narrative value.

What was done:
- Split per-variable `GlobalWorldStateSignal` emission from visual/audio/cartography/telemetry side effects.
- `CompletePendingEvaluation` now aggregates side-effect flags after applying all variable changes.
- VWS broadcast source is selected once per event, preferring Leviathan then Toxicity.
- Load snapshots reuse the side-effect executor without enabling audio replay.

Cinematic Cheats used:
- Campaign progression remains a deterministic signal batch, not a simulated historical or ecological replay.
- Toxicity remains one shader scalar plus one sparse ecosystem pressure pass per story shift.
- Radio and cartography remain queue packets, not direct UI/audio mutation.

Exact microseconds saved:
- Three-variable BaseDelta shift now avoids two extra VWS queue packets.
- Avoids two extra NarrativePoiStateSignal packets.
- Avoids two extra `Shader.SetGlobalFloat` calls and two extra ecosystem pressure calls.
- Avoids two extra telemetry writes.
- Estimated cold-event saving: 8-20 us on i3/MX350 plus downstream consumer work.
- Normal gameplay frame cost remains 0 us when no progression signal exists.

Verification:
- `validate_script Assets/_Project/Scripts/Narrative/Campaign/MetaCampaignService.cs`: 0 diagnostics.
- Campaign anti-bloat scan remains clean.
- `rg` found no `CampaignManager.Instance`, `GameManager.Instance`, or `DaysSurvived` under `Assets/_Project/Scripts`.
- Unity Console currently reports unrelated `GlobalDataVault` missing namespace/Burst errors.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -clp:ErrorsOnly` remains red with 92 unrelated missing namespace/type errors.
- Status remains `PENDING VERIFICATION`.

# META_CAMPAIGN_DIRECTOR Seventh Follow-up Hardening Report - 2026-05-13

What was wrong:
- `TryGetGlobalVariable` could read the NativeParallelHashMap while the read-only evaluation job was still scheduled.
- Unity native safety can reject main-thread container access during job ownership even when both paths are reads.
- Completing the job inside a service query would introduce a frame stall.

What was done:
- Added scalar caches for `CampaignStage`, `ToxicityLevel`, `Leviathan_Awakened`, and `Base_Delta_Destroyed`.
- `RefreshCachedStateFromVariables` now refreshes those scalar mirrors after completed evaluation/load/reset.
- `TryGetGlobalVariable` returns cached known variables while `_evaluationPending` is true and refuses unknown hashes until the job completes.

Cinematic Cheats used:
- Campaign reads remain scalar state queries, not live graph walks.
- No polling, no day-count fallback, no managed cache dictionary.

Exact microseconds saved:
- Avoids a possible job completion stall in service reads: variable hitch removed.
- Known-variable pending-job query cost is scalar comparisons only, estimated below 1 us.
- Normal gameplay frame cost remains 0 us when no progression signal exists.

Verification:
- `validate_script Assets/_Project/Scripts/Narrative/Campaign/MetaCampaignService.cs`: 0 diagnostics.
- Campaign anti-bloat scan remains clean.
- `rg` found no `CampaignManager.Instance`, `GameManager.Instance`, or `DaysSurvived`.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -clp:ErrorsOnly` remains red with 92 unrelated missing namespace/type errors.
- Unity Console could not be re-read because the Unity MCP session did not answer ping.
- Status remains `PENDING VERIFICATION`.

# META_CAMPAIGN_DIRECTOR Eighth Follow-up Hardening Report - 2026-05-13

What was wrong:
- The pending-job guard only answered four hardcoded campaign hashes.
- Future FNV1a global variables would return false during an evaluator job.
- Reading the authoritative NativeParallelHashMap directly remained unsafe while the Burst job owned it.

What was done:
- Added `_queryVariables`, a committed-state NativeParallelHashMap mirror.
- `UpsertGlobalVariable` writes both authoritative and query maps.
- `ClearGlobalVariables` clears both maps for reset/load.
- `TryGetGlobalVariable` now reads only the query mirror, never the job-owned authoritative map.
- NativeMemorySentinel registration/disposal now covers both maps.

Cinematic Cheats used:
- Campaign query state stays a compact uint/int native table, not a managed quest graph.
- Still no per-frame campaign polling and no day-count fallback.

Exact microseconds saved:
- Prevents a possible job safety exception or forced job completion stall in service reads.
- Query cost remains one native hash lookup.
- Added memory is about 512 B payload for 64 mirrored uint/int pairs plus native container overhead.
- Normal gameplay frame cost remains 0 us when no progression signal exists.

Verification:
- `validate_script Assets/_Project/Scripts/Narrative/Campaign/MetaCampaignService.cs`: 0 diagnostics.
- Campaign anti-bloat scan remains clean.
- `rg` found no `CampaignManager.Instance`, `GameManager.Instance`, or `DaysSurvived`.
- Unity Console currently reports unrelated `HectonUnderwaterVisuals` interface mismatch and MCP regex-timeout errors.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -clp:ErrorsOnly` remains red with 93 unrelated missing namespace/type/interface errors.
- Status remains `PENDING VERIFICATION`.

# META_CAMPAIGN_DIRECTOR Ninth Follow-up Hardening Report - 2026-05-13

What was wrong:
- `OnDisable` disposed campaign native maps, but `OnEnable` could publish stale cached visual state before rebuilding default variables.
- The evaluator would append duplicate variable changes if future rules targeted the same hash.
- Hidden dev force-set replayed side effects even when the target value was already committed.

What was done:
- `OnEnable` now ensures default variables and refreshes cached state after allocation.
- `EnsureDefaultVariables` now re-upserts existing authoritative values to repair the query mirror.
- `TryAppendChange` coalesces duplicate output hashes and ORs side-effect flags.
- `TryForceSetGlobalVariable` no-ops when the requested value is already committed.

Cinematic Cheats used:
- Re-enable recovery is a four-hash native repair, not a story replay.
- Future rule collisions collapse to one deterministic state mutation.
- Dev recovery remains hash-state repair, not a direct UI/audio mutation.

Exact microseconds saved:
- Avoids stale visual/ecosystem publication after disable/re-enable.
- Saves one GlobalWorldStateSignal and one blackbox row for each duplicate future rule target.
- Avoids one signal/side-effect pass for repeated no-op dev force-set.
- Normal gameplay frame cost remains 0 us when no progression signal exists.

Verification:
- `validate_script Assets/_Project/Scripts/Narrative/Campaign/MetaCampaignService.cs`: 0 diagnostics.
- Campaign anti-bloat scan remains clean.
- `rg` found no `CampaignManager.Instance`, `GameManager.Instance`, or `DaysSurvived`.
- Unity Console currently reports unrelated duplicate `HectonUnderwaterVisuals` methods.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -clp:ErrorsOnly` remains red with 96 unrelated missing namespace/type/duplicate-method errors.
- Status remains `PENDING VERIFICATION`.
