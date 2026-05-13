# Status: FAUNA_RETINAL_ADAPTATION

Batch prompt: `FAUNA_RETINAL_ADAPTATION`  
Role: `AI_PROGRAMMER`  
Domain: Flora, Fauna & Biota / AI Perception  
Status policy: `PENDING VERIFICATION` until Unity Console / tests / profiler evidence exists.

## Task Checklist

- [x] 1. SINGLETON ERADICATION: Purge `VisionManager.Instance`.
- [x] 2. SIGNAL MIGRATION: Consume `SubmarineLightsChangedSignal`.
- [ ] 3. ASMDEF ISOLATION: `Hecton8.AI.Perception` -> Contracts. `[BLOCKED BY DEPENDENCY]`
- [x] 4. DEAD CODE HUNT: Eradicate `Physics.Raycast` used for light detection.
- [x] 5. S.O.A. RETINA STATE: Add `NativeArray<float> RetinalExposure` and `NativeArray<byte> BlindnessState` to Fauna data structures.
- [x] 6. LIGHT SOURCE REGISTRY: Maintain a `NativeArray<LightSourceData>` for the 4 brightest lights.
- [x] 7. DOT PRODUCT SIGHT: In Fauna `SlowTick` job, use distance squared then dot product.
- [x] 8. EXPOSURE INTEGRATION: Integrate exposure when predators look into lights.
- [x] 9. BLINDNESS TRIGGER: Set `BlindnessState = 1` over threshold.
- [x] 10. FLINCH BEHAVIOR: Inject perpendicular flee impulse in `PredatorCognitionDomain`.
- [ ] 11. ENRAGE BEHAVIOR: Species hashes can enrage instead of fleeing.
- [ ] 12. RECOVERY DECAY: Decay exposure outside direct glare.
- [ ] 13. AUP SHIFT SAFETY: Positions survive origin shift frame.
- [ ] 14. MATH LOD: Low tier evaluates retinal exposure at 1Hz.
- [ ] 15. ZERO-GC: Dot products and state writes allocate 0 bytes.
- [ ] 16. BLACKBOX DUMP: Push `TotalBlindPredators` to telemetry.
- [ ] 17. EVENT BUS: Emit `FaunaStateChangedSignal(Blind)`.
- [ ] 18. CROSS-DOMAIN AUDIT: Brownouts kill light sources in registry.
- [ ] 19. OMEGA COMPILE CHECK: Verify normalize uses `math.rsqrt`.

## Iteration Log

### Loop 0 - Initialization

- Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md` with PowerShell regex.
- Fresh status file created because no prior `Status_FAUNA_RETINAL_ADAPTATION.md` existed.
- Fresh rationale file will be maintained before marking tasks done.

### Loop 1 - Tasks 1-5

- [x] Task 1: Exact search found no first-party `VisionManager.Instance`; DOD practice was evidence search before edit. Rejected creating a compatibility singleton. Estimate: 0.0 us/frame.
- [x] Task 2: Added `SubmarineLightsChangedSignal` lane and fauna consumer entry point. DOD practice was event-bus decoupling. Rejected direct gameplay references. Estimate: ~0.3 us/light signal drain.
- [ ] Task 3: `[BLOCKED BY DEPENDENCY]` Whole-fauna asmdef split would require `Hecton8.Core` to reference `Hecton8.AI.Perception` while perception still depends on Core/World/Construction. DOD practice was compile-graph audit. Rejected a fake folder split that would not isolate code. Estimate: N/A.
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
