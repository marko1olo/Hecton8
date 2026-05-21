# SHINOBU_254 Rationale

Status: IMPLEMENTED / TEST RUN BLOCKED BY UNITY BEE BACKEND2 STALL

## Initial Boundary
Problem: KCC physics QA cannot rely on rendered PlayMode because graphics variance masks math cost and manual play misses edge cases.
Solution: Build a headless NUnit smoke harness with preallocated native DTO buffers, deterministic hostile input, SDF-only collision math, and explicit PRE/SIM/POST sequencing.
Rejected Alternatives: Unity scene/collider tests and MonoBehaviour runners introduce scene dependencies, graphics/audio noise, and editor-only state.
Scalability potential: Low tier validates survival math with the same DTO layout; Middle/High/Ultra can add more hostile profiles and denser SDFs without changing runtime authority. This test profile forces Ultra fidelity to stress worst-case math, not to add a runtime binary switch.
Hardware Impact: i3/MX350 gain is indirect: physics regressions over 50 us/frame are caught before shipping, preventing slow collision code from consuming gameplay frame budget.

## Decision 01 - Ocean Runtime Boundary
Problem: Headless physics QA fails if the KCC initialization path touches camera, time, or scene singleton APIs.
Solution: Scanned OceanKinematicsRuntimeService and added an NUnit guard for Camera.main, Time.deltaTime, FindObjectOfType, and GameObject.Find. No production refactor was needed because the inspected service already avoids those hot dependencies.
Rejected Alternatives: Refactoring a clean service would add churn and risk in another owner path.
Scalability potential: Low/Middle/High/Ultra all keep the same pure injected runtime route; only test profile density changes.
Hardware Impact: i3/MX350 gain is 0 us/frame direct, but avoids scene queries that would spike editor and CI runs.

## Decision 02 - SDF Instead Of Unity Physics Scene
Problem: Loading a Unity scene or Physics world would mix graphics/editor variance with KCC math cost.
Solution: Generate a 48x48x48 voxel SDF in a Burst job and inject it into GlobalDataVault using the existing Shinobu KCC buffer lane.
Rejected Alternatives: Physics.CapsuleCast, scene colliders, or baked prefab worlds were rejected because they test Unity state, not deterministic KCC math.
Scalability potential: Low uses coarse SDF/cadence, Middle adds denser hostile profiles, High/Ultra can increase SDF dimension and sweep iterations while preserving DTO layout.
Hardware Impact: i3/MX350 avoids scene load and broadphase overhead; expected saved setup cost is milliseconds per CI invocation, frame cost remains data-local.

## Decision 03 - Fused PRE/SIM/POST Burst Loop
Problem: 10,000 frames with separate per-frame Schedule/Complete calls would measure job scheduler overhead more than collision math.
Solution: Use one Burst IJob with explicit ExecutePreSimulation, ExecuteSimulation, and ExecutePostSimulation phase functions, then schedule/complete the whole headless pass once after warmup.
Rejected Alternatives: 30,000 scheduled jobs were rejected because the 50 us/frame requirement is for KCC integration, not dispatcher benchmarking.
Scalability potential: Low keeps the same phase order with fewer entities/profiles; Ultra keeps quality at 1.0 and max sweep iterations for worst-case proof.
Hardware Impact: i3/MX350 avoids thousands of tiny job fences; saved overhead is estimated in hundreds of milliseconds across the full 10,000-frame run.

## Decision 04 - Sanitized Hostile Input Is Telemetry, Not Failure
Problem: The hostile generator intentionally injects infinity to prove NaN vaccination. Treating sanitized input as ErrorFlags would make the smoke test fail by design.
Solution: Mark sanitized inputs in telemetry only, sanitize to zero/forward vectors, and reserve ErrorFlags for non-finite state, tunneling, performance, allocation, layout, and drift failures.
Rejected Alternatives: Rejecting the whole frame on bad input was rejected because it does not prove the KCC survives hostile input.
Scalability potential: Low can reduce injection cadence, Ultra can increase hostile cadence; gameplay truth and DTO layout remain unchanged.
Hardware Impact: i3/MX350 cost is a finite check and branch per phantom, expected sub-microsecond per frame.

## Decision 05 - Black Box First
Problem: "No tunneling" without postmortem evidence is not debuggable when CI fails.
Solution: Maintain a fixed 300-frame HeadlessKccTelemetryEntry ring and dump it to Docs/AgentLogs/Dump_SHINOBU_254.bin on any failure.
Rejected Alternatives: Debug.Log streams and managed exception-only reports were rejected as GC-heavy and incomplete.
Scalability potential: Low keeps 300 frames; Middle/High/Ultra can increase record detail through extra native fields only if layout is versioned.
Hardware Impact: i3/MX350 hot cost is one ring write per frame, expected below 1 us/frame.

## Decision 06 - Compile Wall Handling
Problem: Unity batchmode imported SHINOBU_254 but stalled in Bee pass 2; direct test compilation then failed on missing Core dependency artifacts; rebuilding Core.Contracts failed on `long3` in AupPrecisionContracts.cs.
Solution: Stop only the Unity/Bee processes launched by SHINOBU_254, record a dependency blocker, and avoid editing Core Contracts from the KCC QA domain.
Rejected Alternatives: Patching `long3` from this agent was rejected as cross-domain ownership violation without a route card.
Scalability potential: Once Core.Contracts compiles, the SHINOBU_254 NUnit test is CI-runnable and can be scaled by phantom count/profile CSV without changing ownership.
Hardware Impact: i3/MX350 no runtime impact; compile wall prevents measured proof until fixed.

## Decision 07 - Core AUP long3 Shim
Problem: The repeated SHINOBU_254 run request was blocked by Core.Contracts using `long3` while Unity.Mathematics in this project does not provide that type.
Solution: Added a minimal `[StructLayout(LayoutKind.Explicit, Size = 24)]` `Hecton8.Core.Contracts.long3` with x/y/z `long` fields and a constructor inside `AupPrecisionContracts.cs`. This mirrors the existing `Hecton8.Modding.long3` precedent but keeps Core.Contracts independent from Modding.
Rejected Alternatives: Referencing `Hecton8.Modding.long3` from Core was rejected because Core must not depend on Modding. Changing `QuantizeMillimeters` to another return type was rejected because it would mutate public Core API shape.
Scalability potential: Low/Middle/High/Ultra all use the same 24-byte int64 AUP quantization payload; no gameplay quality switch or DTO authority route changes.
Hardware Impact: i3/MX350 runtime impact is 0 us/frame; this is a compile contract fix. Hashing cost remains three int64 fields.

## Decision 08 - Unity/Bee Backend2 Stall Boundary
Problem: After the Core.Contracts fix, Unity batchmode rebuilt Core.Contracts and requested a second Tundra run, then Bee backend2 stopped producing diagnostics for 20 minutes and wrote no test result XML.
Solution: Stopped only the Unity/Bee processes launched by SHINOBU_254 and recorded the stall as the remaining blocker. No additional dotnet/Unity jobs were launched in parallel.
Rejected Alternatives: Waiting indefinitely was rejected because it leaves a hidden editor process and violates the no-running-session closeout rule. Killing unrelated processes was rejected.
Scalability potential: Once Bee completes, the same NUnit harness runs unchanged in CI and editor window paths.
Hardware Impact: i3/MX350 no runtime impact; verification remains blocked before measured physics execution.
