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
