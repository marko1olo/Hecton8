# Rationale_COMBAT_ARMOR_PENETRATION

Status: PENDING VERIFICATION

## Decision 00 - Bootstrap Scope

Problem: Combat task requires armor penetration, status masks, queue routing, physics pushback, VFX signals, recon, and Burst verification without coupling to other active agents.
Solution: Treat combat as a data-only pipeline: unmanaged damage/status structs, NativeQueue intake, NativeArray LUT/status storage, and signal structs that can later be bridged through the project EventBus/GlobalRegistry without concrete cross-domain references.
Rejected Alternatives: Direct damage calls on fauna/player components were rejected because they create compile-time coupling and bypass the damage queue. Child head colliders were rejected because the prompt demands local-hit fake logic. Managed status effect objects were rejected because zero-GC and Burst jobs require blittable bitmasks.
Scalability potential: Low = single LUT lookup and bit tests only. Middle = same math with extra signals. High = extra deflection/VFX metadata. Ultra = saved CPU can drive denser blood/debris presentation through GPU scatter without changing combat truth.
Hardware Impact: Estimated low-end i3/MX350 gain is 10-40 us per 1k status entities versus object/list status scanning; exact profiler proof absent.

## Decision 01 - Mandate Selection

Problem: The task intersects damage truth, physics impulse routing, zero-GC, Burst/native memory, and black-box evidence.
Solution: Loaded eight scoped mandates: damage feedback, physics ForceMode, zero-GC, cinematic cheat, crash telemetry, native jobs, rsqrt, and GlobalRegistry.
Rejected Alternatives: Reading all registry files was rejected as context noise. Reading only combat prose was rejected because pushback and telemetry obligations are cross-domain constraints.
Scalability potential: Mandate-constrained design keeps Low tier on bitmasks/LUTs while permitting High/Ultra presentation overkill through decoupled VFX signals.
Hardware Impact: Prevents known low-end stalls from managed status objects, direct collision callbacks, and mid-frame job completion.

## Decision 02 - Native Armor/Status Data Layout

Problem: The runtime had a combat queue, LUT, and status job, but the packed status field was too small for the required `Bleeding/Crushed/Irradiated/Hypoxia` bits plus existing poison/burn/stun/brittle/cripple flags.
Solution: Expanded packed status metadata to 9 bits, moved weakspot/detail/damage-class shifts into the remaining uint space, stored armor as native integer slots, and added a second `float4` duration lane for legacy flags. The hot status job remains `IJobParallelFor` over `NativeArray<uint>` with direct bit tests.
Rejected Alternatives: A managed `List<StatusEffect>` or per-entity components were rejected for GC and O(N) scan cost. Reusing the old 6-bit mask was rejected because it would silently drop `Stunned/Brittle/Crippled` after adding the mandated flags.
Scalability potential: Low = one uint mask and two float4 duration lanes. Middle = same bit tests plus legacy poison/burn. High = extra status channels without changing the job shape. Ultra = renderer/VFX can key off richer status flags while combat still pays O(1).
Hardware Impact: Estimated i3/MX350 gain remains 10-40 us per 1k entities versus managed status iteration; exact profiler proof absent because project compile is blocked by unrelated dependencies.

## Decision 03 - Deflection, Death, and Event Routing

Problem: Directional armor needed Burst-side deflection while death needed ecosystem-safe notification without referencing the Ecosystem Director.
Solution: Added unmanaged `DeflectSignal` and `EntityDeathSignal` lanes to `GlobalSignals`. The damage job emits `DeflectSignal` directly through a native queue writer when `dot(AttackDir, TargetForward) < -0.7`; the managed dispatch side emits `EntityDeathSignal(AUP, EntityHash)` after resolving a world point.
Rejected Alternatives: Direct ecosystem calls were rejected as cross-domain coupling. Collider armor panels were rejected as CPU and authoring bloat. Managed C# events from the Burst job were rejected because they are not Burst-compatible.
Scalability potential: Low = scalar dot and one optional queue write. Middle = deflection sound/spark consumers drain the signal. High = richer impact feedback from the same signal. Ultra = GPU scatter/audio can overproduce presentation without changing combat truth.
Hardware Impact: The deflection path is one normalize, one dot, and one queue write only on qualifying hits; estimated low-end cost is below 5 us for typical hit counts, with no heap allocation.

## Decision 04 - Cinematic Fakes for Headshot, Blood, and Pushback

Problem: The prompt required critical hits, blood feedback, and physical knockback without child head colliders, prefab spawning, or job-side Rigidbody writes.
Solution: Implemented headshot as `localHit.y > Height * 0.8`, blood as `DebrisSpawnSignal` plus existing chemical scent, and pushback through `PhysicsForceRouter.QueueForce` after job completion. Momentum uses `math.lengthsq` with a clamp; `math.normalize` was replaced by rsqrt normalization.
Rejected Alternatives: Head child colliders, blood prefab instantiation, direct Rigidbody mutation from jobs, and square-root magnitude paths were rejected because they violate the cinematic-cheat, physics routing, and zero-GC mandates.
Scalability potential: Low = headshot scalar and no spawned object. Middle = blood signal consumers decide cheap particles. High = GPU scatter can create denser blood. Ultra = overkill debris/blood visuals remain downstream of one signal.
Hardware Impact: Local-y headshot saves collider broadphase/narrowphase overhead; estimated low-end saving is 15-60 us per crowded combat burst compared with child hitbox setups.

## Decision 05 - Recon and Compile Wall

Problem: The mandatory exact recon scan had to separate actual `IDamageable`/`SendMessage` hazards from legacy direct damage fallbacks, then compilation had to be verified.
Solution: Logged exact scan results to `RECON_COMBAT_ARMOR_PENETRATION.md`. Local Unity MCP script validation passed for `CombatDamageRuntime.cs` and `GlobalSignals.cs`. `dotnet build Hecton8.Core.csproj` is blocked by unrelated domain errors: missing `SurvivalPhysiologyScalarResult`, `TetherVerletTelemetryEntry`, and interface implementation gaps in `MantaScooter` and `PowerGridManager`.
Rejected Alternatives: Claiming compile success from a static grep was rejected. Editing physiology, tether, vehicle, or power-grid files was rejected as domain overreach for this prompt.
Scalability potential: The combat code is ready for downstream validation once other agents restore compile; no additional runtime bloat was added to work around unrelated compile breaks.
Hardware Impact: No hardware impact from the compile wall itself. Validation evidence is limited to zero diagnostics from targeted Unity script validation, not full Burst compiler proof.

## OMEGA POLISH CHANGES

Problem: The Omega mandate required an anti-bloat audit after all task checkboxes were done or blocked, including fake-first math, scalability, zero-GC purge, and final build evidence.
Solution: Re-read `<POLISH_MANDATE id="OMEGA_POLISH">`, scanned touched code for `math.sqrt`, `math.normalize`, managed `foreach`, string formatting, `.ToString()`, and direct `IDamageable`/`SendMessage` hazards. No matches remained in the touched combat/signal files. Division paths in the combat patch use `math.rcp`; direction normalization uses rsqrt. `git diff --check` reported no whitespace errors, only the repository CRLF warning for `CombatDamageRuntime.cs`.
Rejected Alternatives: Claiming `VERIFIED MASTER GRADE` was rejected because full project compile fails in unrelated domains. Adding compatibility shims for physiology, tether, scooter, or power-grid errors was rejected as cross-domain sabotage.
Scalability potential: Low = dominant-axis directions and bitmask status. Middle = same data path with blood/deflect queue signals. High = high math LOD surface normals and feedback. Ultra = downstream GPU scatter/audio can turn the same native signals into visual overkill.
Hardware Impact: No extra hot-path allocations were introduced. Expected low-end i3/MX350 wins are unchanged: bitmask status saves 10-40 us per 1k entities; fake headshot avoids child collider overhead; blood/deflect/death are fixed-size queue writes.

Exact cinematic cheats used:

- Headshot is a local-y threshold: `localHit.y > Height * 0.8`.
- Armor direction is a scalar dot fake: `dot(AttackDir, TargetForward) < -0.7`.
- Deflection is damage scalar `0.1` plus one native signal, not armor-panel physics.
- Blood feedback is `DebrisSpawnSignal` plus chemical scent, not prefab instantiation.
- Momentum uses `math.lengthsq` with clamp and rsqrt normalization, not sqrt magnitude.

Final Git Diff:

- Modified tracked file: `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` (`693 insertions`, `63 deletions` against current git base; includes pre-existing base drift in this active worktree).
- Untracked project file touched by this agent: `Assets/_Project/Scripts/Core/GlobalSignals.cs` with `DeflectSignal` and `EntityDeathSignal` lanes.
- New logs: `Docs/Tasks/Status_COMBAT_ARMOR_PENETRATION.md`, `Docs/AgentLogs/Rationale_COMBAT_ARMOR_PENETRATION.md`, `Docs/AgentLogs/RECON_COMBAT_ARMOR_PENETRATION.md`.
- Full project build remains blocked: `Hecton8.Core.csproj` errors in `HectonSurvivalSystem.cs`, `TetherInstance.cs`, `MantaScooter.cs`, and `PowerGridManager.cs`. Status remains `PENDING VERIFICATION`, not `VERIFIED MASTER GRADE`, because objective compile proof is absent.

## Decision 06 - Honest R&D Hardening Pass

Problem: The combat system was mechanically complete but still lacked the Black Box evidence required for a critical runtime system, and directional armor was vulnerable to stale target orientation after initial registration.
Solution: Added a fixed 300-entry `NativeArray<CombatTelemetryEntry>` ring plus `NativeArray<uint>` state cursor. Dispatch writes high-level state hashes, health deltas, status flags, local hit positions, and anomaly hashes with no heap allocation. Non-finite result detection triggers a rare binary dump to `Docs/AgentLogs/Dump_COMBAT_ARMOR_PENETRATION.bin`. Added `ICombatHitProfileSource` and per-hit hit-profile refresh so front-armor deflection reads current receiver forward/height. `HectonPlayerHealth` now implements the profile using its cached collider for height.
Rejected Alternatives: Per-frame managed logs were rejected for GC. Writing telemetry only to chat/report files was rejected because it does not survive runtime crash analysis. Updating every registered target transform every frame was rejected as unnecessary O(N) transform traffic; refreshing only the target being damaged is cheaper and more truthful.
Scalability potential: Low = one 64-byte ring write per dispatched combat result. Middle = anomaly dump available for QA. High = richer downstream analysis from same hashes. Ultra = combat analytics can drive more expensive hit feedback without modifying damage truth.
Hardware Impact: Ring memory is 19.2 KB plus 8 bytes of cursor state. Normal runtime cost is a sequential native write per dispatched result, estimated below 1 us per result on i3/MX350. BinaryWriter allocation occurs only on anomaly dump, outside normal hot path.

REGRESSION MODEL:

- CPU: + one native ring write per dispatched combat result; no new per-frame scan.
- GC: 0 B in normal dispatch path; anomaly dump allocates only after NaN/non-finite detection.
- Memory: +19.2 KB telemetry ring plus cursor state.
- Cadence: no new Update/Coroutine; existing combat dispatch window only.
- Correctness: death signal implementation restored; stale target-front risk reduced by per-hit profile refresh.
- Failure Modes: dump path can fail if file system denies `Docs/AgentLogs`; combat truth continues because dump is diagnostic-only.

## Decision 07 - Managed Fanout and Ingress Vaccination

Problem: The damage truth path was Burst/native, but managed side effects still resolved Rigidbody and poison diffusion receivers through component lookups after results were dispatched. Direct public `TryQueueDamage` callers could also inject non-finite magnitudes, directions, or local points, causing bad forces or invalid telemetry before the anomaly dump could be useful.
Solution: Added cold managed mirrors per combat slot: receiver `Transform[]` for world/local conversion and `Rigidbody[]` for pushback. Registration captures those once; unregister slot-swap moves them with the native SoA data. `HectonPlayerHealth` now exposes `ICombatPushbackBodySource` using its cached Rigidbody. Poison diffusion resolves candidates by walking transforms against the existing registered target map instead of asking Unity for `IDamageReceiver` components. `TryQueueDamage` sanitizes non-finite/negative signal fields before queue insertion and publishes `TelemetryAnomalySignal` when it had to vaccinate ingress. Result anomaly telemetry now also publishes a fixed-size signal before attempting the binary dump.
Rejected Alternatives: Per-result `GetComponent<Rigidbody>` was rejected because combat side effects are still a hot fanout path. `GetComponentInParent<IDamageReceiver>` in poison diffusion was rejected because registered target slots already contain authoritative ownership. Rejecting all malformed packets was rejected because status-only packets legitimately carry zero magnitude; sanitizing preserves valid status payloads while preventing non-finite physics values.
Scalability potential: Low = cached body/transform references and sanitized packets only. Middle = telemetry anomaly signal can feed QA HUD without changing combat truth. High = richer hit reactions can read the cached slot body. Ultra = downstream VFX/audio can overproduce from clean anomaly and damage signals without adding component lookups to combat.
Hardware Impact: Removes up to two Unity component lookups per damaging result and receiver lookup per poison candidate. Estimated low-end i3/MX350 saving is 2-12 us per heavy combat burst plus reduced native/managed boundary churn. Ingress sanitizer adds scalar finite checks only at enqueue time; expected cost below 0.5 us per packet.

REGRESSION MODEL:

- CPU: + finite checks on ingress; - per-result Rigidbody lookup and - poison receiver component lookup.
- GC: 0 B in normal path; no LINQ/managed collection added. Cold arrays are fixed `Transform[2048]` and `Rigidbody[2048]`.
- Memory: + two managed reference arrays, about 32 KB on 64-bit runtime.
- Cadence: no new Update, coroutine, or per-frame scan.
- Correctness: non-finite damage values cannot become physics pushback forces; poison diffusion only targets registered combat receivers.
- Failure Modes: targets with Rigidbody only on an uncached parent no longer receive pushback unless their receiver exposes `ICombatPushbackBodySource` or has a Rigidbody on the receiver object. This is intentional; hidden parent lookup was removed from combat fanout.
