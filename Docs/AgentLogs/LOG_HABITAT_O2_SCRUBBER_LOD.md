# HABITAT_O2_SCRUBBER_LOD Log

## 2026-05-15 - Dalton Base Hibernation Pass
STATUS: PENDING VERIFICATION

What was wrong:
- Existing Dalton gas solver advanced every room and diffusion edge regardless of base distance/occupancy.
- Power graph evaluation had no native awake-mask hook, so outpost power could keep solving while the base was uninspectable.
- No typed player-base transition lane existed for atmosphere hibernation.
- Base awake state was not exposed through H-Phi/DataVault.

What was done:
- Added typed unmanaged `PlayerBaseEnterSignal` and `PlayerBaseExitSignal` lanes with finite AUP guards.
- Expanded `IGasDynamicsSolver` with base hibernation snapshots/configuration and read-only `BaseAwakeState`.
- Added `BaseAwakeState`, room-to-base mapping, base center AUP, player-inside flags, hibernation timestamps, battery watt-seconds, idle draw, leak rate, and ambient O2 lanes.
- Stored `BaseAwakeState` through `GlobalDataVault` via `BufferID.HabitatBaseAwakeState` and `SystemID.HabitatAtmosphere`, with local fallback only if the vault is unavailable.
- Registered the solver as `IFrostTickable`; FrostTick drains base signals, applies AUP distance hibernation with hysteresis, records dispatcher unscaled time, and wakes on inside/near events.
- Gas Burst job skips metabolism and bulkhead diffusion for rooms whose base awake byte is 0; telemetry records sleeping rooms in the fixed ring.
- Added Burst `BaseHibernationWakeCatchUpJob`: `alpha = 1 - exp(-elapsed * leakRate)`, battery drain = `idleDrawWatts * elapsedSeconds`, battery clamp to 0, and O2 forced to 0 on depleted battery.
- Added power graph read-only awake-mask binding and WFC outpost power boot binding to gas base 0 when available.

Cinematic Cheats used:
- Analytical O2 decay replaces replaying every missed gas diffusion tick.
- FrostTick distance hibernation replaces per-frame distance checks.
- Power graph hibernation returns stable summary state instead of simulating invisible Jacobi relaxation.

Exact microseconds saved:
- Measured proof absent. Static model: 20-80us per sleeping 64-room gas base per gas cadence, plus avoided bound power Jacobi slices. Signal batch cost model: 1-3us per FrostTick transition batch. No Unity profiler or Burst console was run because the user explicitly disallowed dotnet rebuilds and no Unity MCP compiler is available.

Blocked items:
- ASMDEF isolation is blocked by existing `GlobalRegistry` concrete references to `HectonSurfaceWeatherDirector` and `HectonAtmosphereManager`; a real split requires integrator interface work first.
- Burst `math.exp` compile verification is blocked by the no-rebuild/no-Unity-tool constraint. Code path is present in a `[BurstCompile]` job, but verification remains pending.

## 2026-05-15 - Static Recheck Addendum
STATUS: PENDING VERIFICATION

What was rechecked:
- Re-read the modified gas solver hibernation state, signal drain, wake catch-up job, native disposal, and telemetry gates.
- Re-read the power graph awake-mask binding and WFC gas binding.
- Re-read GlobalSignals payload sizing and H-Phi buffer identifiers.

What was found:
- No new code patch required from this pass.
- `ReadOnlySpan<T>` signal snapshots and `NativeArray<T>.ReadOnly` job fields are already established in the project.
- `AbsoluteUniversePosition` is explicitly 48 bytes, so the 64-byte base enter/exit signal layout is correctly bounded.
- Touched-file `git diff --check` exits 0 with CRLF warnings only.

Exact microseconds saved:
- No additional runtime saving claimed; this pass was risk reduction. Dotnet rebuild was not run.

## 2026-05-15 - H-Phi Hygiene Addendum
STATUS: PENDING VERIFICATION

What was wrong:
- Gas solver cold-initialized base transition signal lanes even though `GlobalSignals` owns lane capacity/hash configuration.
- `TryConfigureBase` could leave stale room-to-base mappings when a nonzero base was reconfigured.
- A newly configured base could inherit a sleeping byte and old hibernation timestamp, causing first wake to charge against full session uptime.
- Black-box dump filename was generic instead of agent-specific.

What was done:
- Removed consumer-side signal lane initialization; empty snapshot reads remain safe until owner initialization.
- Kept `HabitatBaseAwakeState` on append-only `BufferID` 63.
- Cleared stale nonzero base room mappings before remap and initialized new bases awake at dispatcher time.
- Changed gas black-box output to `Docs/AgentLogs/Dump_HABITAT_O2_SCRUBBER_LOD.bin`.

Cinematic Cheats used:
- None new. This was H-Phi and cold-path correctness work.

Exact microseconds saved:
- Frame-time saving not claimed. Cold allocation pressure is lower because the solver no longer prewarms two signal lanes in scenes without base transition traffic. No dotnet rebuild was run.

## 2026-05-15 - Signal Producer Addendum
STATUS: PENDING VERIFICATION

What was wrong:
- Atmosphere hibernation consumed base transition signals, but no runtime module producer emitted them.

What was done:
- `BaseModule` now publishes `PlayerBaseEnterSignal` when the player is confirmed inside a dry module trigger.
- `BaseModule` now publishes `PlayerBaseExitSignal` when the tracked player leaves the module interior.
- Payloads use AUP center from the module interior probe, current frame, resolvable room id, and base id 0 as the current single-base bridge.

Cinematic Cheats used:
- None new. This is a decoupled event bridge.

Exact microseconds saved:
- No frame-time claim. This avoids gas polling gameplay modules and keeps wake override event-driven. No dotnet rebuild was run.

## 2026-05-15 - Same-Frame Handoff Addendum
STATUS: PENDING VERIFICATION

What was wrong:
- Enter and exit signals were drained in an order that could let a same-frame module exit override a same-frame module enter.

What was done:
- Gas drains exit packets first, then enter packets. Enter wins for module-to-module trigger handoffs.

Cinematic Cheats used:
- None new.

Exact microseconds saved:
- No runtime saving claimed; packet count is unchanged. No dotnet rebuild was run.

## 2026-05-15 - Native Inside Count Addendum
STATUS: PENDING VERIFICATION

What was wrong:
- A base-level boolean could still be wrong if enter and exit signals for overlapping modules arrived on adjacent frames.

What was done:
- Added `_basePlayerInsideCount` as a native per-base SOA lane.
- Enter signals increment the count; exit signals decrement it with a zero clamp.
- `_basePlayerInside` is now derived from count for signal traffic, while direct API setters remain explicit authority.

Cinematic Cheats used:
- None new.

Exact microseconds saved:
- No frame-time saving claimed. Cost is one int lane per base and one integer update per transition packet. No dotnet rebuild was run.

## 2026-05-15 - H-Phi Audit Addendum
STATUS: PENDING VERIFICATION

What was checked:
- Ran `Tools/Architecture/HectonPhiAudit.ps1 -Summary`; first two-minute run timed out, longer retry completed.

Result:
- Evidence class: STATIC_SOURCE.
- RuntimeHPhiRisk: 0.000573574.
- DataSovereignty: 0.021057035.
- DataVaultRefs: 151.
- NativeArrayRefs: 7020.

Decision:
- Kept DataVault ownership scoped to `BaseAwakeState`. Moving the front/back Dalton gas arrays now would require generation-handle and job-swap redesign outside this prompt.

Exact microseconds saved:
- None claimed. This was audit evidence, not runtime work. No dotnet rebuild was run.
