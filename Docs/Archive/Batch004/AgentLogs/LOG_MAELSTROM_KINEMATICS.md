# LOG_MAELSTROM_KINEMATICS

## 2026-05-13 - Abyssal Whirlpools

Status: PENDING VERIFICATION. Core task surface implemented. Unity/MCP validation unavailable. Local compile blocked by unrelated cross-domain missing contracts after 3 attempts.

### What Was Wrong
- The batch prompt described whirlpool authority drifting toward trigger/AreaEffector/AddForce style behavior.
- No safe `AnomalySpawnedSignal(Maelstrom)` contract existed locally, and `GlobalSignals.cs` was already dirty before this task.
- Existing analytical whirlpool math lacked a compact active maelstrom publishing surface for GPU/boids/post/audio/damage.

### What Was Done
- Added maelstrom authority to `HectonFluidEngine` through fixed analytical whirlpool slots, `TrySetMaelstrom`, compact `NativeArray<float4>` output, full `NativeArray<WhirlpoolFlow>` output, active metadata, AUP rebase, and black-box telemetry.
- Added `HectonAnalyticalFlowField.SampleWhirlpoolVelocity` using squared distance, `math.rsqrt`, bounded inverse-square gain, low-tier tangent suppression, and finite velocity clamps.
- Wired `PlayerKinematicsBodyJob` to apply maelstrom velocity delta without colliders.
- Wired `SubmarineAutoLevelPidJob` to sample maelstrom acceleration and queue it through `PhysicsForceRouter.QueueAmbientForce`.
- Bound compact maelstrom data to `HectonMarineSnowRenderer` and added GPU swirl in `Hecton_MarineSnow.compute`.
- Reused visor pressure warp in `HectonVisorUberPostFeature` when the camera is inside a vortex.
- Emitted `AcousticPingSignal` rumble from the primary maelstrom AUP.
- Emitted packet-native `Core.Signals.CombatDamageSignal` with Pressure damage on event-horizon cadence.
- Routed fauna response by having `SargassumMicroFaunaBoids` read active maelstroms through a cached fluid service and register existing predator fear bursts.

### Cinematic Cheats Used
- Mathematical vortex field replaces physical trigger volumes.
- Inverse-square suction/tangent is clamped, not simulated water.
- Marine snow swirl is GPU presentation fake, capped to two maelstrom samples.
- Visor distortion reuses pressure warp scalar instead of a new camera volume.
- Boid panic reuses massive threat/fear infrastructure instead of bespoke swarm physics.

### Exact Microseconds Saved
- Trigger/OnTriggerStay/AreaEffector broadphase path removed: estimated savings 20-150 us per active hazard cluster on low-end scenes, depending on Rigidbody count.
- Player/submarine hot path cost added: estimated 1-4 us per controlled body for one or two maelstrom samples.
- Low-tier tangent suppression and one-maelstrom cap: estimated savings 0.5-2 us per sampled body versus high-tier two-sample tangent math.
- GPU particle swirl moved off CPU: avoids CPU per-particle work; CPU cost limited to compact buffer upload when active.
- Audio/damage cadence avoids per-frame signal spam: one acoustic signal per 0.45 s and damage check per 0.35 s.

### Regression Model
- CPU: bounded active-count loops only; no collider callbacks added.
- GC: static scan found no added LINQ, ToArray, managed hot collections, string interpolation, string.Format, or ToString in the maelstrom diff.
- Memory: one fixed `NativeArray<float4>`, one 300-entry telemetry NativeArray, one fixed maelstrom GraphicsBuffer.
- Correctness: event-horizon truth remains in GlobalSignals; submarine force remains in PhysicsForceRouter.
- Failure modes: missing maelstrom signal contract blocks task 02; global compile dependency wall blocks task 19 and Unity import proof.

### Verification
- Prompt re-extracted from `Docs/Tasks/CURRENT_BATCH.md` after task groups.
- `rg` over `Assets/_Project` found no AreaEffector, PointEffector, Tornado, WhirlpoolManager, or WhirlpoolManager.Instance references.
- Touched-file scan found no `math.sqrt`.
- Omega diff-only scan found no added `math.normalize`, foreach, LINQ, ToArray, string interpolation, string.Format, or ToString.
- `git diff --check` reports no whitespace errors after status cleanup; only CRLF conversion warnings remain.
- Unity MCP validation: blocked, `no_unity_session`.
- Local compile: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /nr:false /p:UseSharedCompilation=false` still fails with 114 unrelated missing-contract errors (`Environment.Fluids`, `Core.Memory.Layout`, `Physics.CCD`, acoustic propagation, ground radar, inventory corrosion, `TetherFiredSignal`, etc.).

### Final Diff
- 9 files changed.
- 488 insertions.
- 44 deletions.
- Code files: `HectonFluidEngine.cs`, `PlayerKinematicsRuntime.cs`, `SubmarineAutoLevelBallastController.cs`, `HectonMarineSnowRenderer.cs`, `Hecton_MarineSnow.compute`, `HectonVisorUberPostFeature.cs`, `SargassumMicroFaunaBoids.cs`.
- Log/status files: `Status_MAELSTROM_KINEMATICS.md`, `Rationale_MAELSTROM_KINEMATICS.md`.

## 2026-05-13 - Abyssal Whirlpools Second Quality Pass

Status: PENDING VERIFICATION. Core implementation was tightened after self-review. Unity/MCP remains unavailable. Local compile still blocked by global missing contracts, now reporting 113 errors before a maelstrom-specific verdict.

### What Was Wrong
- Low-tier maelstrom selection could starve slot 1 because the first cap sampled only slot 0.
- The shared vortex evaluator trusted already-sanitized data; a bad direct struct caller could still push non-finite center/strength values into rsqrt/cross math.
- Marine snow uploaded compact maelstrom data whenever the binding path ran, even if the payload was unchanged.
- Visor maelstrom warp resolved `GlobalRegistry.Fluid` in the camera/render path.
- `Hecton_MarineSnow.compute` still had older scalar-swizzle zero literals, which are backend-fragile.

### What Was Done
- Low tier now scans both fixed analytical slots and publishes/samples the strongest valid maelstrom only.
- `TrySetWhirlpool` and `SampleWhirlpoolVelocity` reject non-finite radius/strength/center data before math.
- Marine snow maelstrom data is double-buffered and uploaded only when a raw-float hash or active count changes.
- Marine snow and visor now cache fluid service references with throttled refresh instead of resolving the registry in hot presentation paths.
- Shader zero fallbacks are explicit typed `float3`/`float4` constructors.
- Status and rationale were updated with the extra loop evidence.

### Cinematic Cheats Used
- Low-tier truth keeps only the strongest suction field; high-tier spends on extra tangent and presentation.
- Unchanged GPU payloads reuse the last buffer; visible motion continues in the compute shader without CPU re-upload.
- Visor maelstrom intensity remains a scalar pressure-warp fake, not a camera-space simulation.

### Exact Microseconds Saved
- Strongest-only low tier avoids one full tangent/sample path: estimated 0.5-2 us per sampled body on MX350-class scenes.
- Hash-gated GPU upload avoids redundant compact buffer upload: estimated 3-20 us CPU driver overhead when maelstrom state is unchanged.
- Cached visor fluid binding removes one render-path registry lookup per active camera pass: estimated sub-microsecond alone, but removes unpredictable shared-service traffic.
- Typed shader zeros do not save frame time; they reduce backend compile risk.

### Regression Model
- CPU: new strongest scan is fixed at two slots; upload hash scans only the compact active count, max two entries.
- GC: no new managed hot collections, LINQ, foreach, string interpolation, string.Format, ToArray, or ToString in the maelstrom diff.
- GPU: double buffer prevents same-buffer update hazards; inactive state reverts to empty buffer.
- Correctness: low tier still respects authoring priority by strongest intensity, not slot order.
- Failure modes: Unity MCP validation is blocked by `no_unity_session`; local compile remains blocked by unrelated contract gaps.

### Verification
- Prompt re-extracted from `Docs/Tasks/CURRENT_BATCH.md`.
- Banned-pattern scan over touched surfaces: no `math.sqrt`, non-rsqrt `sqrt(`, `OnTriggerStay`, `AreaEffector`, `PointEffector`, `WhirlpoolManager.Instance`, or `Tornado`.
- Diff-only allocation scan: no added `math.normalize`, foreach, LINQ, ToArray, string interpolation, string.Format, or ToString.
- Shader portability scan: no `0.0.xxx` or `0.0.xxxx` remains in `Hecton_MarineSnow.compute`.
- `git diff --check` reports no whitespace errors; CRLF conversion warnings only.
- Unity MCP `validate_script` for `HectonFluidEngine.cs`, `HectonMarineSnowRenderer.cs`, and `HectonVisorUberPostFeature.cs`: blocked, `no_unity_session`.
- Local compile: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /nr:false /p:UseSharedCompilation=false` fails with 113 unrelated missing-contract errors (`Environment.Fluids`, `Core.Memory.Layout`, `Physics.CCD`, acoustic propagation, ground radar, save blittable attributes, inventory corrosion, `TetherFiredSignal`, etc.).

### Diff Scope
- 9 files changed in tracked diff scope at this pass: 776 insertions, 98 deletions.
- Code files: `HectonFluidEngine.cs`, `PlayerKinematicsRuntime.cs`, `SubmarineAutoLevelBallastController.cs`, `HectonMarineSnowRenderer.cs`, `Hecton_MarineSnow.compute`, `HectonVisorUberPostFeature.cs`, `SargassumMicroFaunaBoids.cs`.
- Log/status files: `Status_MAELSTROM_KINEMATICS.md`, `Rationale_MAELSTROM_KINEMATICS.md`, plus this append-only log entry.
