# [ARCHIVE] X_012 Historical Report

Archive date: 2026-05-23
Reason: removed from active documentation corpus by X_012; historical evidence only.
Active index: ../../Reports/README.md

# X_007 Math LOD Phase 0 Report

Status: STATIC_SOURCE / PENDING UNITY RUNTIME VERIFICATION
Agent: X_007
Domain: Echelon 1 Scalability Dictator / continuous GlobalQualityWeight Math LOD
Source scan ledger: `Docs/Reports/MATH_LOD_COMPLEXITY_LEDGER_X_007.json`

## Task 01 - Solver Complexity Inquisition

Scan scope: `Assets/_Project/Scripts/**/*.cs`

Files scanned: 2,375 C# files.
Candidate seed files: 1,167.
Recorded candidate files: 728.

Token totals from the static ledger:

| Token Class | Count |
|---|---:|
| `math.exp` / `Math.Exp` | 29 |
| `math.log` / `Math.Log` | 1 |
| `math.pow` / `Math.Pow` | 27 |
| `math.sin` | 243 |
| `math.cos` | 113 |
| `math.sqrt` | 96 |
| `math.length` | 114 |
| `math.normalize` / `.normalized` | 126 |
| deterministic-risk random tokens | 18 |
| `FloatMode.Fast` | 508 |
| `GlobalQualityWeight` / quality route references | 5,911 |
| loop tokens in candidate files | 7,866 |

Highest-priority prompt-aligned files:

| File | Finding | Phase 1 Action |
|---|---|---|
| `Assets/_Project/Scripts/Physiology/ShinobuPhysiologyJobs.cs` | Haldane/Schreiner blood-gas integrator uses `math.exp(-effectiveK * dt)` in deterministic Burst authority. | Candidate for Padé [3/3] with residual telemetry; exact path retained until fuzzer proof. |
| `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` | Base hibernation catch-up uses `math.exp` inside `FloatMode.Fast` Burst job. | Replace leak alpha with Padé [2/2] or [3/3]; change authority job away from fast math if it affects gameplay truth. |
| `Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsJobs.cs` | `ResolveJacobiIterations`, omega, tolerance, residual sample mask, and resolution currently return authoritative constants. Heat source falloff uses `math.pow`. | Implement continuous iteration/resolution/tolerance curves driven by `GlobalQualityWeight`; replace fixed exponent cases where valid. |
| `Assets/_Project/Scripts/Power/PowerGridJacobiContracts.cs` | Power voltage solver already consumes `GlobalQualityWeight` for smoothing but pass count is owner-side, not proven continuous in this file. | Verify runtime scheduler pass count; keep deterministic mode. |
| `Assets/_Project/Scripts/Logistics/FluidPipePressureJobs.cs` | Pipe pressure evaluator is deterministic and loop-heavy; no expensive transcendentals found in the job file. | Candidate for continuous iteration/cadence only, not transcendental replacement. |
| `Assets/_Project/Scripts/HectonBoidController.cs` | GPU boids use `_BoidMathLodMode` from `HomeostasisBrain.GlobalQualityWeight`; also uses frozen tier state from foveated simulation. | Audit compute shader and replace discrete social LOD/frozen gates with continuous weight where simulation, not culling, is affected. |
| `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs` | Large boid runtime with blackbox, jobs, compute path, and quality-sensitive GPU/CPU route. | Verify all quality decisions are continuous and report discrete fallback gates separately. |
| `Assets/_Project/Scripts/HectonCelestialEngine.cs` | Analytical orbit route already uses deterministic visual normalization and blackbox; remaining `sqrt` is mostly presentation distance. | Do not force gameplay-authority rewrite; replace hot visual trig only if measured or in smoke path. |

## Task 02 - Approximation Viability

Residuals computed with 20,001 uniform samples per interval:

| Approximation | Domain | Max Absolute Error |
|---|---:|---:|
| Padé [2/2] for `exp(-x)` | `x in [0, 0.10]` | `1.257467E-08` |
| Padé [2/2] for `exp(-x)` | `x in [0, 0.25]` | `1.060247E-06` |
| Padé [2/2] for `exp(-x)` | `x in [0, 0.50]` | `2.671734E-05` |
| Padé [3/3] for `exp(-x)` | `x in [0, 0.25]` | `4.727170E-10` |
| Padé [3/3] for `exp(-x)` | `x in [0, 1.00]` | `3.793503E-06` |
| Bhaskara sine | `x in [0, pi]` | `1.631765E-03` |

Recommended formula set:

`exp(-x)` Padé [2/2]:
`(1 - 0.5x + x*x/12) / (1 + 0.5x + x*x/12)`

`exp(-x)` Padé [3/3]:
`(1 - 0.5x + x*x/10 - x*x*x/120) / (1 + 0.5x + x*x/10 + x*x*x/120)`

Bhaskara sine for reduced `x in [0, pi]`:
`16*x*(pi - x) / (5*pi*pi - 4*x*(pi - x))`

Decision:

- Haldane/Schreiner physiology is gameplay authority. Padé [3/3] is viable only with clamped `x = effectiveK * dt`, residual telemetry, and exact/reference torture tests. Do not replace blindly.
- Gas hibernation leak alpha is viable for Padé [2/2] if `elapsedSeconds * leakRate` is bounded to `<= 0.5`; otherwise use Padé [3/3] or chunk the catch-up interval.
- Visual/audio sine paths are viable for Bhaskara/triangle/LUT replacement. Gameplay-truth sine/cos paths require per-file proof before replacement.
- `pow` with fixed exponents is viable for multiply-chain or polynomial replacement. Variable exponents stay exact or low-cadence unless residual proof exists.
- `sqrt`/`length` calls in comparisons are viable for squared-distance replacement. Calls producing actual distance for UI/debug can stay exact outside hot simulation.

## Task 03 - Homeostasis Signal Integration

Current route facts:

- `HomeostasisBrain.GlobalQualityWeight` exists as a sanitized continuous scalar in `Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs`.
- `SystemDispatcher` calls `HomeostasisBrain.PreSimulationTick(unscaledDeltaTime)` before `GlobalSignals.FlushPreSimulation()`.
- `SignalBusRegistry.SetGlobalQualityWeight01()` stores the scalar as a milli-quantized volatile value for SignalBus capacity/coalescing.
- Existing `ScalabilityChangedEvent` is a 16-byte tier-change payload. It does not carry the continuous float.
- Many heavy systems read `HomeostasisBrain.GlobalQualityWeight` directly or pass a cached float into jobs.

Route decision for Phase 1:

- Do not mutate `ScalabilityChangedEvent` layout in Phase 0. It is public contract surface and currently tier-only.
- Preferred no-GC route: owner phase reads `HomeostasisBrain.GlobalQualityWeight` once, writes/caches a sanitized float in owner state or Vault DTO, then passes it into Burst jobs as a plain float field.
- For hot broadcast, add a new layout-stable `ScalabilityWeightChangedSignal` or reuse reserved bytes only with ABI guard and explicit route card. Do not overload tier-change semantics silently.
- Consumers must not poll `GlobalRegistry` or search scene state for quality. Cached owner fields, SignalBusRegistry scalar, or Vault snapshots are accepted.

## Low / Middle / High / Ultra Scalability Shape

Low: Padé [2/2] where bounded; low cadence or interval chunking for authority formulas; minimum solver iterations never below stability floor.
Middle: Padé [3/3] for authority exponentials; squared-distance gates; moderate residual sample cadence.
High: Exact-vs-approx residual sampling retained; more compartments/iterations/resolution through continuous curves.
Ultra: Saved CPU goes to visual overkill only: richer presentation lanes, extra residual telemetry, and higher update residency without changing gameplay truth layout.

## Verification State

Unity runtime proof: absent.
Profiler/GCMonitor proof: absent.
Compile proof: not run; no runtime code was changed in Phase 0.
Static evidence: `Docs/Reports/MATH_LOD_COMPLEXITY_LEDGER_X_007.json`.
