# LOG: FAUNA_RETINAL_ADAPTATION

## 2026-05-13 - Predator Headlight Blindness

What was wrong:
- Predator cognition had managed player-light exposure but no event-driven submarine/headlight retinal path.
- Light perception risked drifting toward scene objects, singleton managers, or physics queries instead of Burst math.
- There was no fauna blindness state signal for audio and no fixed 300-frame retinal black-box.

What was done:
- Confirmed no first-party `VisionManager.Instance`, `LightTrigger`, or light-detection `Physics.Raycast` remained in the inspected fauna/gameplay light path.
- Added/verified `SubmarineLightsChangedSignal` and `FaunaStateChangedSignal(Blind)` lanes in `GlobalSignals`.
- Added/verified scooter headlight upsert/remove publishing through `SubmarineLightsChangedSignal`.
- Added/verified `NativeArray<float> _retinalExposure`, `NativeArray<byte> _blindnessState`, `NativeArray<LightSourceData>[4]`, and `NativeArray<RetinalTelemetryEntry>[300]` in `PredatorCognitionDomain`.
- Added/verified Burst retinal glare math: distance squared first, `math.rsqrt` direction, spotlight cone check, glare dot threshold `dot < -0.8`, hysteresis blind trigger, and recovery outside direct glare.
- Added/verified behavior consequences: aversion flinch uses perpendicular lateral vector; frenzy species tuning doubles aggression scalar.
- Added/verified black-box dump path `Docs/AgentLogs/Dump_FAUNA_RETINAL_ADAPTATION.bin` on non-finite retinal telemetry fault.
- Omega polish changed new headlight source id code from `GetInstanceID()` to `GetHashCode()` to avoid adding Unity 6 obsolete warnings.

Cinematic cheats used:
- Four-brightest light registry instead of full scene light simulation.
- Scalar retinal exposure instead of retinal physiology.
- Dot-product cone fake instead of raycast/occlusion light simulation.
- Perpendicular steering fake instead of physics impulse.
- Stale source cull instead of owning logistics brownout queues.

Exact microseconds saved:
- Rejecting by distance squared before `rsqrt`: estimated 0.2-0.5 us per rejected predator/light pair on i3/MX350.
- Four-light cap versus scene query: bounded to four pairs per due predator; avoids unbounded light iteration.
- Low-tier 1Hz retinal cadence versus 0.5s predator cadence: roughly halves retinal work on MX350 tier.
- Event-bus remove/stale cull versus polling gameplay/power graph: O(4) registry mutation, no per-predator grid polling.
- Zero managed allocations in the retinal hot path: 0 B/frame targeted.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore` fails on project-wide missing generated assemblies/contracts (`Hecton8.Core.Scheduling`, `Hecton8.Core.Memory.Layout`, `Hecton8.Audio.Propagation`, `Hecton8.Physics.CCD`, etc.).
- Filtered build diagnostics for `PredatorCognitionDomain.cs`, `MantaScooter.cs`, and `GlobalSignals.cs` after the compile wall; no retinal/headlight-specific diagnostics were emitted.
- Unity MCP script validation returned `no_unity_session`.
- Status remains `PENDING VERIFICATION`.

## 2026-05-13 - AAA Recheck / Upgrade Pass

What was wrong:
- Batch Task 6 explicitly included flares, but flare objects were not publishing into the retinal light lane.
- Recovery decay had drifted from the explicit prompt scalar `0.1f`.
- Retinal black-box hottest-light position used zero-origin reconstruction, making shift-frame diagnostics weaker.
- Job telemetry could report predator cognition completion when only the swarm job was scheduled.
- Scooter headlights were emitting inactive-slot remove packets every frame.

What was done:
- Added AUP-safe `DeployableFlare` upsert/remove publishing through `SubmarineLightsChangedSignal` with omni cone data.
- Set `RetinalExposureDecayPerSecond` to `0.1f`.
- Added `_predatorEvaluationJobScheduled` and gated predator completion reporting plus retinal post-eval telemetry on real admission.
- Reconstructed hottest-light telemetry with an active cognition origin offset.
- Added `_publishedHeadlightSignalMask` to reduce scooter headlight remove-packet churn.

Cinematic cheats used:
- Flares use a single omni retinal light packet instead of physical light sampling.
- Headlights and flares still compete through the fixed four-brightest registry.
- Stale cull remains the fallback for lost remove packets.

Exact microseconds saved:
- Scooter inactive slots now save up to two remove packets/frame per scooter.
- Skipping retinal telemetry when predator cognition was not admitted avoids O(active slots) post-scan on AI budget fallback frames.
- Flare support adds one fixed upsert packet per active flare tick but does not widen the per-predator four-light cap.

Verification:
- Prompt re-extracted from `CURRENT_BATCH.md`.
- Hot-path search on edited files found no `math.normalize`, `Vector3.normalized`, `Physics.Raycast`, managed `foreach`, `.ToString()`, or `string.Format`.
- `dotnet build Hecton8.Core.csproj --no-restore` timed out after reaching the same project-wide dependency wall first errors: `Hecton8.Core.Scheduling`, `Hecton8.Environment.Fluids`, `Hecton8.Core.Memory.Layout`.
- Filtered build diagnostics for `PredatorCognitionDomain.cs`, `MantaScooter.cs`, `DeployableFlare.cs`, and `SubmarineLightsChangedSignal`: no matches.
- Unity MCP `validate_script` was retried for all three edited scripts and returned `no_unity_session`.
- `git diff --check` passed; only line-ending normalization warnings.
- Status remains `PENDING VERIFICATION`.

## 2026-05-13 - Strict Recheck / Burst Path Cleanup

What was wrong:
- The prompt extraction regex used during the recheck assumed no attributes after `id`, while `CURRENT_BATCH.md` stores role/chat metadata on the same XML tag.
- The Burst retinal light loop still called the outer telemetry AUP helper after the telemetry origin fix.
- Active flares needed a lane-pressure audit after being added as retinal publishers.

What was done:
- Re-extracted the full `FAUNA_RETINAL_ADAPTATION` block using an attribute-tolerant CLI regex and reconfirmed 19 tasks.
- Restored retinal job light-position reconstruction to the nested `ResolveRuntimePosition(...)` helper.
- Kept `ResolveTelemetryRuntimePosition(...)` for post-job black-box telemetry only.
- Confirmed flare retinal publishing uses immediate first upsert, source-phased 4-frame refresh, play-mode guards, and AUP-safe omni light data.
- Re-ran anti-bloat search across `PredatorCognitionDomain.cs`, `MantaScooter.cs`, and `DeployableFlare.cs`.

Cinematic cheats used:
- Burst path remains distance-squared first, then `math.rsqrt`, then cone/glare dot.
- Flares are modeled as one omni retinal stimulus instead of physical light sampling.
- Four-brightest registry remains the hard cap for headlights plus flares.
- Flare refresh throttling uses signal staleness tolerance instead of frame-perfect AI light animation.

Exact microseconds saved:
- Burst-local helper cleanup: 0 us/frame direct speedup; removes a Burst compatibility risk from the hot path.
- Flare 4-frame refresh: roughly 75% fewer steady-state flare upsert packets.
- Scooter published-slot mask remains up to two remove packets/frame saved per inactive scooter.
- Skipping retinal telemetry when predator cognition is not admitted avoids O(active slots) post-scan on budget fallback frames.

Verification:
- `rg` confirmed the retinal job now uses `float3 lightPosition = ResolveRuntimePosition(...)`.
- Hot-path search found no `math.normalize`, `Vector3.normalized`, `Physics.Raycast`, managed `foreach`, `.ToString()`, or `string.Format` in retinal additions. Remaining matches are pre-existing property expression bodies, one cold `List<IDamageSignalReceiver>` field, and pre-existing `string.Create`.
- `dotnet build Hecton8.Core.csproj --no-restore` exited 1 on the existing global dependency wall: `Hecton8.Core.Scheduling`, `Hecton8.Environment.Fluids`, `Hecton8.Core.Memory.Layout`, `Hecton8.Physics.CCD`, `Hecton8.Audio.Propagation`, generated service contracts. `PredatorCognitionDomain.cs(8,20)` is only the missing `Hecton8.Core.Scheduling` import.
- Unity MCP `validate_script` returned `no_unity_session` for all three edited scripts.
- `git diff --check` passed; warnings are line-ending normalization only.
- Status remains `PENDING VERIFICATION`.
