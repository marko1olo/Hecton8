# Rationale 1741

Updated: 2026-06-03

## Decisions

1. Scene transition owner
Problem: `PrologueWorldHandoffSceneLoader` delegated to `ISceneService.LoadScene(sceneName)` after whiteout, losing prologue-specific preload/activation control.
Chosen route: Keep the existing owner and replace the call with a stored additive `AsyncOperation`, `allowSceneActivation=false`, priority assignment, per-frame progress polling, ocean-handoff activation, active-scene swap, and async orbit unload.
Rejected route: Do not widen `ISceneService` or create a duplicate orbital controller.
Scaling impact: Low/Mid keep a single async operation and no loading screen. High/Ultra get the same gameplay truth with no extra route cost.
Proof label: BUILD VERIFIED, STATIC VERIFIED.

2. Lighting and reflections
Problem: `01_ORBIT.unity` had `RenderSettings.m_Sun` unset and `Directional Light` shadows disabled; runtime bootstrap forced `LightShadows.None`.
Chosen route: Set the serialized scene sun to the verified light, hard shadows to strength 0.92, low bias/normal bias, and runtime hard-shadow enforcement. Add a cold-created baked reflection probe with `ViaScripting` refresh and skybox default reflections.
Rejected route: Real-time or every-frame reflection probes. Full black ambient was also rejected because TASTE/project bibles require readable player-critical forms.
Scaling impact: Low/Mid get static skybox reflections and hard silhouettes without six-face realtime probe cost. High/Ultra can spend elsewhere; this route does not change gameplay truth.
Proof label: BUILD VERIFIED, STATIC VERIFIED, VISUAL PENDING.

3. Aegir shader ownership
Problem: The active Aegir sky shader used Unity `_Time`; the task required C# owner-driven phase. The physical `GasGiant_Aegir` sphere route is stale in runtime because the director disables that renderer.
Chosen route: Add `_H8AegirFlowPhase` and `_H8AegirFlowPhaseValid` globals. `OrbitalRelativityDirector` advances a deterministic phase from dispatcher delta, `aegirBandFlowSpeed`, and continuous `GlobalQualityWeight`; the shader falls back to `_Time` outside the prologue.
Rejected route: Re-enable and rotate the physical Aegir sphere hierarchy.
Scaling impact: Low keeps slower readable band drift. Mid increases cadence. High/Ultra get faster storm/band motion without changing route ownership.
Proof label: BUILD VERIFIED, STATIC VERIFIED, VISUAL PENDING.

4. Camera interpolation
Problem: The verified drop-pod seat camera route used normalized lerp for rotations. Cinemachine candidate files were not found in the verified path.
Chosen route: Preserve the existing Bezier/dispatcher route and upgrade the quaternion helper to finite-checked `math.slerp`; update the seat controller to call it explicitly.
Rejected route: Inventing a Cinemachine controller without a verified existing owner.
Scaling impact: Low/Mid/High/Ultra use the same deterministic camera truth; no quality branch.
Proof label: BUILD VERIFIED, STATIC VERIFIED.

5. Audio impact route
Problem: Candidate snapshots `Surface_Vacuum` and `Underwater_Muffled` were not present. Existing mixer snapshots are `Surface`, `Underwater`, `BaseInterior`, `SurfaceRain`, and `SurfaceStorm`.
Chosen route: Do not fabricate snapshot names or direct mixer calls. Leave verified `ReentryAcousticStressSignal` / `AcousticZoneController` route for runtime audio handoff.
Rejected route: New unverified direct `AudioMixerSnapshot.TransitionTo` call from the orbit loader.
Scaling impact: No performance or gameplay truth change.
Proof label: STATIC VERIFIED, RUNTIME AUDIO PENDING.

6. Compaction and telemetry
Problem: New data-vault ownership would require compaction-fence proof. The current task did not need new vault access.
Chosen route: No new `GlobalDataVault` path. Existing orbital, sequence, and VFX owners keep their black-box/compaction checks.
Rejected route: Injecting zero-G/player DTO writes outside a verified physics owner.
Scaling impact: No new memory owner; no DTO layout change.
Proof label: STATIC VERIFIED, RACE TEST PENDING.
