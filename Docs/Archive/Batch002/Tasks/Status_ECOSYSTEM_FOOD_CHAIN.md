# Status_ECOSYSTEM_FOOD_CHAIN

Agent: ECOSYSTEM_FOOD_CHAIN
Role: APEX_DIRECTOR
Domain: FLORA, FAUNA & BIOTA (Ecosystem and AI)
Status: PENDING VERIFICATION

## Task Checklist

- [x] 1. BOID CONSUMPTION MATH | DOD: Burst IJob scans mirrored BoidData and writes BoidKillSignal to NativeQueue when distancesq <= BiteRangeSq | Rejected: Transform/ObjectPool prey proxies | Estimate: 80 us bite-frame cap on i3/MX350
- [x] 2. SWARM DECREMENT | DOD: drained KillSignals patch consumed boid indices in CPU mirror and both GPU buffers; existing compute/shader consumed path scales to 0 | Rejected: compute readback or GameObject count mutation | Estimate: 12 us per killed boid GPU buffer patch
- [x] 3. GORE DECAL SPAWN | DOD: each consumed boid publishes DebrisSpawnSignal with blood kind and AUP plus direct capped fluid-decal registration | Rejected: particle prefab spawning | Estimate: 8 us signal enqueue, decal manager cost bounded by existing cap
- [x] 4. LEVIATHAN WHALE FALL | DOD: existing Leviathan death logic mutes AI on same GameObject, keeps sinking-to-MapMagic-floor path, and extends corpse presentation for whale-fall duration | Rejected: new Corpse GameObject | Estimate: 0 us hot-path; death-only state transition
- [x] 5. POI REGISTRATION | DOD: EcosystemDirector/PersistentWorldRegistry/MigrationDirector register whale-fall AUP for 7200s and apply 50x scavenger weighting | Rejected: direct sector data mutation | Estimate: slow-tick POI falloff, no per-frame sector scan
- [x] 6. DYNAMIC CRAB SPAWN | DOD: whale-fall AUP patches existing GPU boids into deterministic MapMagic ground-hugging scavenger ring | Rejected: prefab crab/eel spawning or new manager | Estimate: 1.1 ms one-shot patch for 96 boids on i3/MX350
- [x] 7. CORPSE DEGRADATION | DOD: Leviathan shader `_DecayAmount` driven by 7200s death age and rots toward bone with crawl-noise fake | Rejected: mesh swap/material churn | Estimate: sub-5 us CPU material update, shader-only fragment cost
- [x] 8. FEAR PROPAGATION | DOD: each kill emits +100 byte-scale fear through existing GPU massive-threat/scatter path | Rejected: new CPU spatial hash for micro-fauna | Estimate: existing threat upload cost, no extra hot polling
- [x] 9. FEEDING FRENZY AUDIO | DOD: >5 kill signals in 1s enqueue AcousticPingSignal with frenzy channel | Rejected: direct audio DSP call | Estimate: one NativeQueue enqueue per frenzy
- [x] 10. S.O.A. NUTRITION SYNC | DOD: prey consumption resets predator hunger byte through utility brain SetHunger01(0f) | Rejected: direct NativeArray slot mutation from FaunaBrain | Estimate: one byte write per feed
- [x] 11. HUNGER SPEED SCALAR | DOD: PredatorCognitionDomain multiplies speed by 0.7 when hunger byte threshold > 200 | Rejected: Rigidbody drag/MonoBehaviour clamps | Estimate: one branch+multiply in cognition job
- [x] 12. MATH LOD (THE DEAR LIE) | DOD: whale-fall boid burst only runs at Full swarm LOD; low tier uses `_DecayAmount` crawl-noise shader fake | Rejected: low-tier individual crab/eel proxies | Estimate: zero extra boid cost on low tier
- [x] 13. NO OBJECT SPAWNING | DOD: death path mutes AI/animator/rigidbody on existing Leviathan and extends same GameObject corpse window | Rejected: new corpse prefab or pooled replacement | Estimate: death-only state writes
- [x] 14. RECONNAISSANCE PROTOCOL | DOD: CLI regex scan logged to RECON_ECOSYSTEM_FOOD_CHAIN.md with no Update/coroutine matches | Rejected: manual-only inspection | Estimate: audit-only, 0 us runtime
- [ ] 15. OMEGA COMPILE CHECK | [BLOCKED BY DEPENDENCY] dotnet and Unity compile attempted; ECOSYSTEM-local signal import fixed, remaining errors are Survival/Visor/Construction/Thermal outside domain | Rejected: cross-domain repair | Estimate: verification blocked externally

## Loop Log

- Loop 0: Prompt extracted from Docs/Tasks/CURRENT_BATCH.md. Status file initialized. Rationale file initialized. Core work not started.
- Loop 1: Tasks 1-5 implemented. Compile verification attempted through dotnet and Unity; global build remains blocked by unrelated Survival/Visor/Construction/Thermal errors. Ecosystem-local signal namespace errors were fixed.
- Loop 2: Tasks 6-10 implemented. Unity compile retry reports no ECOSYSTEM_FOOD_CHAIN file errors; remaining blockers are outside assigned domain.
- Loop 3: Tasks 11-13 implemented and code-read for missed object spawning/LOD issues.
- Loop 4: Task 14 reconnaissance complete. RECON_ECOSYSTEM_FOOD_CHAIN.md created.
- Loop 5: Task 15 compile check attempted. Build remains globally blocked by unrelated files; ECOSYSTEM_FOOD_CHAIN files no longer appear in Unity compiler errors.
- Loop 6: OMEGA polish executed after all tasks were checked/blocked. Replaced new event-path divisions with reciprocal multiplication and re-ran dotnet/Unity compile checks. Status remains PENDING VERIFICATION because external compile blockers persist.
- Loop 7: Honest R&D hardening pass executed. Added fixed 300-frame food-chain black-box telemetry, moved predator bite job completion to dispatcher LateFrame swap, prewarmed/registered the kill-signal NativeQueue, and replaced new single-boid GPU SetData patches with LockBufferForWrite writes. Unity MCP script validation passed for SargassumMicroFaunaBoids.cs; broad project compile remains blocked by external core/platform/combat symbols.
- Loop 8: Honest R&D crab IK hardening pass executed after the editor surfaced stale in-domain crab IK errors. Current disk version of ProceduralCrabLegIKRuntime.cs validates clean. Added finite input vaccination for data-only crab pose state and replaced live origin-shift forced completion/rebase jobs with non-blocking pending rebase applied in the dispatcher late-frame swap. Unity console now reports external SaveBinaryStorage and World vegetation-contract blockers only.

## Honest R&D Continuation

- [x] R&D-1. FOOD-CHAIN BLACK BOX | DOD: `NativeArray<FoodChainTelemetryEntry>[300]` records frame, state hash, flags, active/consumed counts, LOD, field center, event position, and anomaly hash; NaN/non-finite writes dump `Docs/AgentLogs/Dump_ECOSYSTEM_FOOD_CHAIN.bin` | Rejected: Debug.Log-only postmortem and managed List history | Estimate: <1 us/frame native ring write on i3/MX350
- [x] R&D-2. DISPATCHER-SAFE KILL JOB | DOD: predator consumption job now schedules on bite request and drains in `LateFrameTick()` through `DispatcherJobSwap`, with forced completion only during teardown | Rejected: same-method `Schedule()+Complete()` stall | Estimate: removes up to 40-80 us bite-frame sync stall risk
- [x] R&D-3. GPU PATCH BANDWIDTH HYGIENE | DOD: consumed/scavenger single-boid writes use `GraphicsBuffer.LockBufferForWrite` instead of direct `SetData`; kill queue is registered with `NativeMemorySentinel` and prewarmed to the 8-signal cap | Rejected: per-boid `SetData` driver path and untracked NativeQueue allocation | Estimate: 3-8 us saved per consumed/scavenger boid patch, pending profiler proof
- [x] R&D-4. CRAB IK ORIGIN-SHIFT STALL REMOVAL | DOD: `OnOriginShift` no longer schedules rebase jobs and immediately completes them; live pipelines now queue a finite shift offset and apply it only after `DispatcherJobSwap` drains in `LateFrameTick()` | Rejected: origin-shift `Schedule()+Complete()` barrier inside the listener | Estimate: avoids a rare 80-180 us forced sync during floating-origin shifts on i3/MX350
- [x] R&D-5. CRAB IK NATIVE INPUT VACCINATION | DOD: crab registration/pose/avoidance writes sanitize non-finite positions, rotations, velocities, dt, and scalar tuning before writing NativeArray/rendering state; telemetry entries are sanitized before black-box dump | Rejected: trusting external fauna pose producers and recording NaN payloads directly | Estimate: <1 us per pose update; prevents NaN propagation into raycast/indirect draw buffers
- [x] R&D-6. CRAB IK VERIFICATION TRIAGE | DOD: Unity MCP `validate_script` returned 0 diagnostics for `ProceduralCrabLegIKRuntime.cs`; Unity console after refresh reports only `SaveBinaryStorage.cs` Burst catch-filter and `HectonIndirectVegetationContracts.cs` out-parameter blockers | Rejected: cross-domain edits to Save/World contracts | Estimate: verification-only, 0 us runtime
