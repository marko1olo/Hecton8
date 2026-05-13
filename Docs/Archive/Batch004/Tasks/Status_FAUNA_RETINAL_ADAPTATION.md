# Status: FAUNA_RETINAL_ADAPTATION

Batch prompt: `FAUNA_RETINAL_ADAPTATION`  
Role: `AI_PROGRAMMER`  
Domain: Flora, Fauna & Biota / AI Perception  
Status policy: `PENDING VERIFICATION` until Unity Console / tests / profiler evidence exists.

## Task Checklist

- [x] 1. SINGLETON ERADICATION: Purge `VisionManager.Instance`.
- [x] 2. SIGNAL MIGRATION: Consume `SubmarineLightsChangedSignal`.
- [x] 3. ASMDEF ISOLATION: `Hecton8.AI.Perception` -> Contracts. `[BLOCKED BY DEPENDENCY]`
- [x] 4. DEAD CODE HUNT: Eradicate `Physics.Raycast` used for light detection.
- [x] 5. S.O.A. RETINA STATE: Add `NativeArray<float> RetinalExposure` and `NativeArray<byte> BlindnessState` to Fauna data structures.
- [x] 6. LIGHT SOURCE REGISTRY: Maintain a `NativeArray<LightSourceData>` for the 4 brightest lights.
- [x] 7. DOT PRODUCT SIGHT: In Fauna `SlowTick` job, use distance squared then dot product.
- [x] 8. EXPOSURE INTEGRATION: Integrate exposure when predators look into lights.
- [x] 9. BLINDNESS TRIGGER: Set `BlindnessState = 1` over threshold.
- [x] 10. FLINCH BEHAVIOR: Inject perpendicular flee impulse in `PredatorCognitionDomain`.
- [x] 11. ENRAGE BEHAVIOR: Species hashes can enrage instead of fleeing.
- [x] 12. RECOVERY DECAY: Decay exposure outside direct glare.
- [x] 13. AUP SHIFT SAFETY: Positions survive origin shift frame.
- [x] 14. MATH LOD: Low tier evaluates retinal exposure at 1Hz.
- [x] 15. ZERO-GC: Dot products and state writes allocate 0 bytes.
- [x] 16. BLACKBOX DUMP: Push `TotalBlindPredators` to telemetry.
- [x] 17. EVENT BUS: Emit `FaunaStateChangedSignal(Blind)`.
- [x] 18. CROSS-DOMAIN AUDIT: Brownouts kill light sources in registry.
- [x] 19. OMEGA COMPILE CHECK: Verify normalize uses `math.rsqrt`. `[COMPILE BLOCKED BY PROJECT-WIDE MISSING DEPENDENCIES]`

## Iteration Log

### Loop 0 - Initialization

- Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md` with PowerShell regex.
- Fresh status file created because no prior `Status_FAUNA_RETINAL_ADAPTATION.md` existed.
- Fresh rationale file will be maintained before marking tasks done.

### Loop 1 - Tasks 1-5

- [x] Task 1: Exact search found no first-party `VisionManager.Instance`; DOD practice was evidence search before edit. Rejected creating a compatibility singleton. Estimate: 0.0 us/frame.
- [x] Task 2: Added `SubmarineLightsChangedSignal` lane and fauna consumer entry point. DOD practice was event-bus decoupling. Rejected direct gameplay references. Estimate: ~0.3 us/light signal drain.
- [x] Task 3: `[BLOCKED BY DEPENDENCY]` Whole-fauna asmdef split would require `Hecton8.Core` to reference `Hecton8.AI.Perception` while perception still depends on Core/World/Construction. DOD practice was compile-graph audit. Rejected a fake folder split that would not isolate code. Estimate: N/A.
- [x] Task 4: Exact light-detection search found no `Physics.Raycast`; existing flashlight math uses squared distance and dot product. Rejected touching unrelated predator LOS `RaycastCommand`. Estimate: 0.0 us/frame.
- [x] Task 5: Added `_retinalExposure` and `_blindnessState` persistent native arrays. DOD practice was SoA owner/dispose/sentinel registration. Rejected per-brain managed fields. Estimate: one float + one byte read/write per due predator.
- Compile check after Loop 1: attempted `dotnet build Hecton8.Core.csproj` with 120s timeout; command timed out without diagnostics. Status remains `PENDING VERIFICATION`.

### Loop 2 - Tasks 6-10

- Prompt re-extracted with CLI using the `FAUNA_RETINAL_ADAPTATION` tag after task 6 boundary.
- [x] Task 6: Added 4-entry `NativeArray<LightSourceData>` registry with upsert/remove/stale cull and brightest replacement. DOD practice was fixed-capacity SoA. Rejected scene light scans. Estimate: ~0.4 us per signal drain, no per-predator cost beyond four candidates.
- [x] Task 7: Burst job checks distance squared before cone/dot tests. DOD practice was early reject before rsqrt. Rejected `math.normalize`. Estimate: saves ~0.2-0.5 us per rejected predator/light pair on i3/MX350.
- [x] Task 8: Exposure integrates only on direct glare (`dot < -0.8`) and cone hit. DOD practice was deterministic scalar accumulation. Rejected physical retina simulation. Estimate: four scalar checks per due predator.
- [x] Task 9: `BlindnessState` flips at threshold and holds until recovery floor. DOD practice was hysteresis. Rejected instant flicker state. Estimate: 1 byte write per due predator.
- [x] Task 10: Blind aversion sets override threat and lateral flinch via cross product. DOD practice was cinematic fake over physics impulse. Rejected Rigidbody force injection. Estimate: ~0.05 us when fleeing.
- Compile check after Loop 2: Unity MCP validation unavailable (`no_unity_session`); CLI build still pending longer pass. Status remains `PENDING VERIFICATION`.

### Loop 3 - Tasks 11-15

- Prompt re-extracted with CLI at the Phase 3/4 boundary.
- [x] Task 11: Enrage uses species tuning keyed by species id/hash: `FaunaLightReactionMode.Frenzy` doubles the aggression scalar before utility scoring. DOD practice was authoring-driven species behavior. Rejected hardcoded Deep Sea Stalker magic constants. Estimate: one branch per due predator.
- [x] Task 12: Exposure decays when the predator is no longer in direct/hold glare; direct glare is `dot < -0.8`, recovery path is outside `dot <= -0.5`. DOD practice was hysteresis. Rejected instant reset. Estimate: one float subtract/write.
- [x] Task 13: Headlights enter the registry as `AbsoluteUniversePosition` and are stored as `AbsoluteUniversePositionBlit128`. DOD practice was AUP-safe math. Rejected runtime-only `float3`. Estimate: four AUP reconstructions max per due predator.
- [x] Task 14: Low tier uses `RetinalLowTierEvaluationIntervalSeconds = 1f` for predator retinal cadence. DOD practice was Math LOD. Rejected 10Hz low-tier glare polling. Estimate: halves predator retinal work versus 0.5s cadence.
- [x] Task 15: Hot path uses native arrays, value structs, queue snapshots, `math.rsqrt`, and fixed capacity loops. DOD practice was zero-GC audit. Rejected managed lists, delegates, and scene queries. Estimate: 0 B/frame.
- Compile check after Loop 3: `dotnet build Hecton8.Core.csproj` fails on pre-existing/generated dependency wall; filtered diagnostics show no edited-file retinal errors. Status remains `PENDING VERIFICATION`.

### Loop 4 - Tasks 16-19

- Prompt re-extracted with CLI before Task 16.
- [x] Task 16: Added 300-frame `RetinalTelemetryEntry` ring, `TotalBlindPredators` count, telemetry publish, and cold fault dump. DOD practice was fixed black-box memory. Rejected log-only state. Estimate: O(active slots) post-job scan, one ring write.
- [x] Task 17: Edge-triggered `FaunaStateChangedSignal(Blind)` publish on blindness state changes. DOD practice was event bus handoff. Rejected direct audio call. Estimate: zero cost unless state flips.
- [x] Task 18: Powered/remove headlight deltas and stale cull remove brownout-dead sources without stealing logistics brownout packets. DOD practice was cross-domain ownership. Rejected draining `BrownoutSignal`. Estimate: O(4) registry mutation.
- [x] Task 19: Verified retinal/headlight normalizations use `math.rsqrt`; no `math.normalize` in the implementation. Full compile is blocked by unrelated project-wide missing assemblies/contracts. Status remains `PENDING VERIFICATION`.

### Loop 5 - Omega Polish

- Read `<POLISH_MANDATE id="OMEGA_POLISH">` only after all 19 tasks were checked or blocked.
- Ran anti-bloat search for `math.normalize`, `math.sqrt`, managed `foreach`, `string.Format`, and `.ToString()` across the edited retinal/headlight/global-signal surfaces; no hot-path matches found.
- Removed new `GetInstanceID()` warning risk by switching headlight source id salt to `GetHashCode()`.
- Filtered `dotnet build` diagnostics for edited files after the project-wide compile wall; no retinal/headlight-specific diagnostics were emitted.
- Final status: `PENDING VERIFICATION` due global compile dependencies and unavailable Unity MCP session.

### Loop 6 - AAA Recheck / Upgrade Pass

- Re-read `Status_FAUNA_RETINAL_ADAPTATION.md`, `Rationale_FAUNA_RETINAL_ADAPTATION.md`, and re-extracted the `FAUNA_RETINAL_ADAPTATION` prompt with CLI before further edits.
- Corrected `RetinalExposureDecayPerSecond` to the prompt value `0.1f`; DOD practice was spec reconciliation after code audit. Rejected tuned faster recovery because the batch gave an explicit scalar. Estimate: unchanged branch count, one multiply uses lower constant.
- Added predator job admission tracking so `PredatorCognitionJob` completion and retinal telemetry are reported only when the predator job was actually scheduled; DOD practice was telemetry accuracy over fake success reports. Rejected reporting both jobs off the swarm handle. Estimate: one bool branch in `LateFrameTick`.
- Changed retinal hottest-light telemetry to reconstruct with the active fauna origin offset instead of `float3.zero`; DOD practice was AUP-safe debug data. Rejected zero-origin telemetry because shift-frame dumps become misleading. Estimate: one cached `float3` read per telemetry update.
- Reduced scooter headlight remove-signal spam with a two-bit published-slot mask; DOD practice was event-bus pressure reduction. Rejected per-frame remove packets for inactive slots. Estimate: saves up to two remove packets/frame per inactive scooter.
- Added `DeployableFlare` publisher to the same AUP-safe light lane, because Task 6 names flares explicitly; DOD practice was event-source publishing, not fauna scene scans. Rejected querying `DeployableFlare` from `PredatorCognitionDomain`. Estimate: one fixed upsert packet per burning/fading flare tick, zero managed allocation.
- Re-ran hot-path anti-bloat search on `PredatorCognitionDomain.cs`, `MantaScooter.cs`, and `DeployableFlare.cs` for `math.normalize`, `Vector3.normalized`, `Physics.Raycast`, managed `foreach`, `.ToString()`, and `string.Format`; no matches.
- Re-ran `dotnet build Hecton8.Core.csproj --no-restore`; it timed out after writing the same project-wide dependency wall first errors (`Hecton8.Core.Scheduling`, `Hecton8.Environment.Fluids`, `Hecton8.Core.Memory.Layout`). Filtered diagnostics for edited retinal/headlight/flare files: no matches.
- Re-tried Unity MCP `validate_script` for `PredatorCognitionDomain.cs`, `MantaScooter.cs`, and `DeployableFlare.cs`; all returned `no_unity_session`.
- `git diff --check` passed for edited files; warnings are line-ending normalization only.
- Final status remains `PENDING VERIFICATION` until Unity/compiler verification is available.

### Loop 7 - Strict Recheck / Burst Path Cleanup

- Re-read `Status_FAUNA_RETINAL_ADAPTATION.md`, `Rationale_FAUNA_RETINAL_ADAPTATION.md`, and re-extracted the `FAUNA_RETINAL_ADAPTATION` prompt with a tag-aware CLI regex that tolerates XML attributes.
- Found the Burst retinal loop still calling the outer telemetry reconstruction helper. Corrected it to the nested `ResolveRuntimePosition(...)` helper inside `PredatorCognitionJob`; DOD practice was Burst-local hot-path math. Rejected sharing the telemetry helper with job code because it weakens the compile/Burst proof. Estimate: unchanged runtime cost, lower Burst incompatibility risk.
- Kept `ResolveTelemetryRuntimePosition(...)` only for post-job black-box telemetry. DOD practice was separating hot-path evaluation from cold diagnostics. Estimate: one cold AUP reconstruction per telemetry update.
- Confirmed flare publisher cadence is source-phased at a 4-frame stride after first publish. DOD practice was event-bus pressure reduction. Rejected per-frame flare upserts because the registry stale window already tolerates sparse refresh. Estimate: saves roughly 75% of steady-state flare upsert packets.
- Confirmed play-mode guards prevent flare clear/publish paths from initializing signal queues during editor disable/reset. DOD practice was editor-safe signal ownership. Estimate: 0 us/frame in runtime hot path.
- Re-ran anti-bloat search on `PredatorCognitionDomain.cs`, `MantaScooter.cs`, and `DeployableFlare.cs`; retinal additions have no `math.normalize`, `Vector3.normalized`, `Physics.Raycast`, managed `foreach`, `.ToString()`, or `string.Format`. Matches are pre-existing property expression bodies, one cold `List<IDamageSignalReceiver>` field, and pre-existing `string.Create`.
- Re-ran `dotnet build Hecton8.Core.csproj --no-restore`; it failed with the same project-wide dependency wall (`Hecton8.Core.Scheduling`, `Hecton8.Environment.Fluids`, `Hecton8.Core.Memory.Layout`, `Hecton8.Physics.CCD`, `Hecton8.Audio.Propagation`, generated service contracts). `PredatorCognitionDomain.cs(8,20)` appears only because `Hecton8.Core.Scheduling` is missing at the using/import layer.
- Re-tried Unity MCP `validate_script` for all three edited scripts; all returned `no_unity_session`.
- `git diff --check` passed for edited files; warnings are line-ending normalization only.
- Final status remains `PENDING VERIFICATION` until Unity/compiler verification is available.
