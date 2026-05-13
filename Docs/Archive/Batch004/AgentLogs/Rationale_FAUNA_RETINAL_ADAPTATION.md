# Rationale: FAUNA_RETINAL_ADAPTATION

Status policy: `PENDING VERIFICATION` until Unity Console / test / profiler evidence exists.

## Mandates Selected

- `AI_Creature_Cognition_States.txt`: predator state change must be utility-gated, not a free managed side effect.
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`: no singleton vision manager; signal lanes or registry contracts only.
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`: headlight positions travel as AUP data, runtime positions reconstructed inside the consumer.
- `MATH_Rsqrt_i3_SIMD.txt`: normalize through `math.rsqrt`; no `Vector3.normalized` in retinal math.
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`: fixed native arrays, no LINQ/delegates/managed allocations in the frame path.
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`: retinal exposure/blindness must live in owner-disposed `NativeArray` state.
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`: last 300 frames retained in a fixed black-box ring.
- `LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt`: powered/off headlight state must be consumed as a brownout-compatible signal, not polled from gameplay.

## Decisions

Problem: Existing fauna perception had managed flashlight exposure but no predator-facing headlight retinal registry.
Solution: Added `SubmarineLightsChangedSignal` and a fixed `NativeArray<LightSourceData>[4]` consumed by `PredatorCognitionDomain`.
Rejected Alternatives: Direct `Light` polling, `VisionManager.Instance`, and scene light searches were rejected because they create cross-domain coupling and frame allocations.
Scalability potential: Low = 1Hz retina cadence and four brightest lights; Middle = current predator cadence; High = same math with more direct headlight stimulus from authored intensities; Ultra = saved cycles can be spent on VFX and animation reactions.
Hardware Impact: i3/MX350 expected hot-path cost is four distance/dot checks per due predator, replacing any raycast-style light query.

Problem: Headlight blindness needs to survive floating-origin shifts.
Solution: Publisher sends `AbsoluteUniversePosition`; job stores `AbsoluteUniversePositionBlit128` and reconstructs runtime position per predator.
Rejected Alternatives: Runtime-only `float3` registry was rejected because origin shift frames would smear the light source.
Scalability potential: Low/Middle/High/Ultra share the same AUP payload; quality changes cadence, not correctness.
Hardware Impact: AUP reconstruction is paid only for four candidate lights in the due cognition job.

Problem: Blind predators need a behavior result without a bespoke AI state tree.
Solution: Retinal blind state boosts existing utility: aversion sets override threat and perpendicular flinch; frenzy doubles aggression and reuses light frenzy attack weighting.
Rejected Alternatives: Adding a new managed `Blind` AI state was rejected because it would fight the existing packed utility output and compatibility bridge.
Scalability potential: Low uses hard lateral fake; Ultra can layer extra animation/VFX on the same `FaunaStateChangedSignal(Blind)`.
Hardware Impact: Lateral flinch is one cross product and one `math.rsqrt`, no NavMesh or physics impulse.

Problem: Black-box mandate requires evidence when retinal math faults.
Solution: Added a fixed 300-entry `NativeArray<RetinalTelemetryEntry>` and cold fault dump path `Docs/AgentLogs/Dump_FAUNA_RETINAL_ADAPTATION.bin`.
Rejected Alternatives: Debug.Log-only reporting was rejected because it does not retain frame history.
Scalability potential: Same ring on every tier; telemetry export can be richer on high-end without changing core math.
Hardware Impact: One compact ring write after completed cognition evaluation; no hot-path file IO.

Problem: Brownout handling cannot consume the shared `BrownoutSignal` queue without stealing packets from logistics/power systems.
Solution: Treat headlight power as authoritative in `SubmarineLightsChangedSignal`; unpowered/remove packets and stale cull erase dead light sources.
Rejected Alternatives: Draining `GlobalSignals.TryDequeueBrownout` in fauna was rejected because it would make fauna an accidental owner of logistics events.
Scalability potential: Low/Middle/High/Ultra all use the same source-state contract; higher tiers can add more publishers without widening fauna coupling.
Hardware Impact: Brownout removal is O(4) registry mutation, no per-predator scan.

Problem: CLI build currently reports project-wide missing assemblies and generated contracts unrelated to this patch.
Solution: Filtered diagnostics for edited files; no retinal-specific diagnostics were emitted, but full verification remains blocked.
Rejected Alternatives: Editing unrelated missing assemblies was rejected because it violates this agent's fauna/perception domain boundary.
Scalability potential: Not applicable; this is integration debt.
Hardware Impact: No runtime effect.

Problem: Task 6 requires flares in the brightest-light registry, but flares were only spatial distractors.
Solution: Added `DeployableFlare` as an AUP-safe publisher to `SubmarineLightsChangedSignal` with omni cone data (`SpotOuterCos = -1f`).
Rejected Alternatives: Fauna-side scene scans or direct flare registry reads were rejected because they add cross-domain coupling and managed lookup pressure.
Scalability potential: Low = one fixed flare packet per active tick; Middle/High = same registry priority selects only the brightest four; Ultra = VFX can exaggerate flare blindness without changing Burst math.
Hardware Impact: i3/MX350 pays one signal packet per active flare and zero additional per-predator work beyond the existing four-light cap.

Problem: The post-job telemetry path reported `PredatorCognitionJob` completion even when admission could fall back to only the swarm handle.
Solution: Added `_predatorEvaluationJobScheduled` and gated predator completion reporting plus retinal telemetry scanning on the actual job admission result.
Rejected Alternatives: Always reporting both jobs was rejected because it creates false profiler evidence.
Scalability potential: Low/Middle/High/Ultra all get accurate admission metrics; higher tiers can tune budgets without corrupted lane data.
Hardware Impact: One bool branch in `LateFrameTick`; avoids unnecessary O(active slots) retinal telemetry scan when predator cognition was not scheduled.

Problem: The hottest-light black-box position was reconstructed with zero origin.
Solution: Reconstruct telemetry positions using the first active cognition input's floating-origin offset.
Rejected Alternatives: Keeping zero-origin telemetry was rejected because shift-frame dumps become hard to trust.
Scalability potential: Same fixed ring on all tiers; better dump quality without widening data.
Hardware Impact: One cached `float3` read per telemetry update.

Problem: Scooter headlights published remove packets for every inactive slot every frame.
Solution: Added a two-bit published-slot mask and only emit remove when a previously published slot retires.
Rejected Alternatives: Per-frame inactive remove packets were rejected because stale cull already covers lost packets and bus pressure is not free.
Scalability potential: Low saves bus traffic; Ultra can run more visual headlight layers while the AI lane stays bounded.
Hardware Impact: Saves up to two remove packets per inactive scooter per frame on low-end CPUs.

Problem: The Burst retinal loop still referenced the outer telemetry AUP helper after the telemetry upgrade.
Solution: Restored the hot path to the nested `ResolveRuntimePosition(...)` helper and reserved `ResolveTelemetryRuntimePosition(...)` for cold black-box reconstruction.
Rejected Alternatives: Sharing one outer helper between Burst evaluation and telemetry was rejected because the job already has a local helper and Burst compatibility should be proven locally.
Scalability potential: Low/Middle/High/Ultra all keep the same math; this reduces integration risk without widening the per-predator light cap.
Hardware Impact: Runtime operation count is unchanged; risk of Burst falling back or refusing compilation is reduced.

Problem: Active flares could publish retinal upserts every tick, creating avoidable signal lane pressure when many flares burn together.
Solution: First publish immediately, then refresh by source-phased 4-frame stride while the light remains active.
Rejected Alternatives: Per-frame upsert spam was rejected because stale cull covers missed packets and the four-light registry does not need frame-perfect flare intensity changes.
Scalability potential: Low = sparse refresh on cheap CPUs; Middle/High = same bounded lane; Ultra = flare VFX can update every frame while AI refresh remains throttled.
Hardware Impact: Saves roughly 75% of steady-state flare upsert packets per active flare.

Problem: Editor disable/reset paths can call flare cleanup before play-mode systems are initialized.
Solution: Added `Application.isPlaying` guards to the flare retinal publish/clear methods.
Rejected Alternatives: Publishing removes from edit-mode lifecycle events was rejected because it can initialize global signal queues outside simulation ownership.
Scalability potential: All tiers avoid editor-only signal churn; runtime behavior is unchanged.
Hardware Impact: No runtime frame cost beyond one cold branch at publish/clear call sites.

## OMEGA POLISH CHANGES

- Replaced new `GetInstanceID()` use in `MantaScooter.ResolveHeadlightSignalSourceId` with `GetHashCode()` to avoid adding Unity 6 obsolete-instance-id warnings.
- Re-scanned edited surfaces for `math.normalize`, `math.sqrt`, managed `foreach`, `string.Format`, and `.ToString()`; no matches in retinal/headlight additions.
- Confirmed cinematic cheats: four-brightest light registry instead of physical light accumulation; distance-squared reject before glare dot; scalar exposure hysteresis instead of retinal simulation; perpendicular flinch vector instead of physics impulse; stale light cull instead of logistics brownout ownership.
- Final diff relevant to this agent: `MantaScooter.cs` one-line source-id warning cleanup plus `Status_FAUNA_RETINAL_ADAPTATION.md` and `Rationale_FAUNA_RETINAL_ADAPTATION.md`. The retinal/global signal implementation is present in the working source and indexed baseline, so `git diff` does not show it as this agent's unstaged delta.
- Verification status: `PENDING VERIFICATION`; `dotnet build Hecton8.Core.csproj` is blocked by project-wide missing assemblies/contracts, and Unity MCP returned `no_unity_session`.
- Second polish pass added flare publishing, exact `0.1f` recovery decay, real-origin retinal telemetry, admission-accurate predator job reporting, and scooter remove-packet masking.
- Third polish pass restored Burst-local AUP reconstruction in the retinal job, retained the outer helper only for telemetry, confirmed flare refresh throttling/play-mode guards, and re-ran CLI/MCP verification. Status remains `PENDING VERIFICATION` because the project build is still blocked by global missing dependencies and Unity MCP has no session.
