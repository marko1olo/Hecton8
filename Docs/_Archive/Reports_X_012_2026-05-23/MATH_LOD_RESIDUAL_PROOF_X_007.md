# [ARCHIVE] X_012 Historical Report

Archive date: 2026-05-23
Reason: removed from active documentation corpus by X_012; historical evidence only.
Active index: ../../Reports/README.md

# X_007 Math LOD Residual Proof

Date: 2026-05-23
Scope: decompression exponent, continuous Jacobi iteration caps, branch audit on touched Burst hot paths.

## Decompression Exponent

Implemented code path:
- `Assets/_Project/Scripts/MathLodApproximation.cs:54` implements the branchless `ApproxExpNegPade33Reduced(float4)` core.
- `Assets/_Project/Scripts/Physiology/ShinobuPhysiologyJobs.cs:752` keeps decompression authority at the fixed runtime `TissueCompartmentCount = 3`.
- `Assets/_Project/Scripts/Physiology/ShinobuPhysiologyJobs.cs:793` uses the Padé path in the Haldane/Schreiner update.
- `Assets/_Project/Scripts/Physiology/ShinobuPhysiologyJobs.cs:1007`, `:1174`, and `:1220` remove the remaining direct physiology `math.exp` hot calls.

Approximation:

`P33(x) = (1 - x/2 + x^2/10 - x^3/120) / (1 + x/2 + x^2/10 + x^3/120)`

`ApproxExpNeg(x) = P33(clamp(x, 0, 4) / 4)^4`

The reduction keeps the Padé polynomial inside `[0,1]` and then squares twice. The helper uses `math.select`, `min`, `max`, `rcp`, and `saturate`; there is no `if` in the approximation core.

Float residual scan against `System.Math.Exp(-x)`:

| Domain | Step | Max abs error | Max rel error | Worst x |
|---|---:|---:|---:|---:|
| `[0,1]` | `0.0001` | `4.152223150E-007` | `5.560966549E-007` | `0.0142` |
| `[0,4]` | `0.0001` | `7.629343334E-007` | `4.161334769E-005` | `3.9988` |

Physiology worst-case bounded by existing clamps:
- `dt <= 0.25s`
- `NitrogenUptakeRate <= 16`
- `HaldaneTimeScale <= 16`
- minimum emergency half-time = `300s`
- `x = ln(2) / 300 * 256 * 0.25 = 0.147871399`
- exact `exp(-x) = 0.862542032`
- float approximation = `0.862542093`
- abs error = `6.080794979E-008`

At `1 atm` tissue term scale, this error is below one ten-millionth of an atmosphere. It is not a decompression damage trigger by itself.

## GlobalQualityWeight Drop Proof

Previous risk: decompression tissue state used a quality-dependent active compartment count/grouping. A sharp `GlobalQualityWeight` drop could alter which tissues were evaluated and could change the state derivative.

Current authority path:

`activeCompartments = ShinobuPhysiologyConstants.TissueCompartmentCount`

The current runtime physiology model is a pragmatic 3-lane decompression authority path. All 3 lanes are always evaluated for decompression authority. `GlobalQualityWeight` is no longer an input to the Haldane state update. Therefore:

`nextTissue(q=1.0) - nextTissue(q=0.1) = 0`

for equal physical inputs and previous tissue state. Telemetry may still report a quality budget elsewhere, but bends damage authority does not jump when quality changes. This rejects the unsafe optimization of reducing survival-physiology compartments below the runtime 3-lane authority model.

## Additional Exp Purge

Removed direct physiology `math.exp` calls:

| Runtime path | Replacement | Residual proof |
|---|---|---:|
| CNS oxygen extreme accumulation | reciprocal Padé `[3/3]` positive exp, clamped `[0,4]` | max abs `[0,4]` = `2.270059792E-003` |
| Hypothermia cooling | `1 - ApproxExpNegPade33Reduced(x)` | bounded by decompression `[0,4]` scan |
| Blood oxygen saturation blend | `1 - ApproxExpNegPade33Reduced(x)` | bounded by decompression `[0,4]` scan |
| Solar Beer-Lambert attenuation | `ApproxExpNegPade33Wide40(x)` blended from cheap rational by smooth quality curve | max abs `[0,40]` = `3.781904305E-006` |
| Gas leak alpha | `1 - ApproxExpNegPade33Wide40(x)` | max abs `[0,40]` = `3.781904305E-006` |
| Storm attenuation | `intensity * ApproxExpNegPade33Wide40(x)` | max abs `[0,40]` = `3.781904305E-006` |
| AI anxiety fear/aggression decay | local branchless Padé `[3/3]` negative exp | bounded by decompression `[0,4]` scan |
| Graphics DRS smoothing | `1 - ApproxExpNegPade33Wide40(x)` | max abs `[0,40]` = `3.781904305E-006` |
| Visor condensation/breath decay | `ApproxExpNegPade33Wide40(x)` | max abs `[0,40]` = `3.781904305E-006` |
| Wake/flora/kinetic/audio decay | `ApproxExpNegPade33Wide40(x)` | max abs `[0,40]` = `3.781904305E-006` |
| VR somatic smoothing and critical blend | `ApproxExpNegPade33Wide40(x)` inside existing comfort smoothing | max abs `[0,40]` = `3.781904305E-006` |
| Seismic event visual magnitude decay | `magnitude * ApproxExpNegPade33Wide40(x)` | max abs `[0,40]` = `3.781904305E-006` |
| Carrion quality-blended biomass decay | `initialBiomass * ApproxExpNegPade33Wide40(x)` blended by existing continuous quality gate | max abs `[0,40]` = `3.781904305E-006` |
| Water optics editor preview | `ApproxExpNegPade33Wide40(x)` | editor-only preview, bounded by `[0,40]` scan |
| UI audio placeholder editor envelope | `ApproxExpNegPade33Wide40(x)` | editor-only placeholder, bounded by `[0,40]` scan |
| Ballistics drag | `velocity * ApproxExpNegPade33Wide40(drag * distance)` under existing lethality guard | max abs decay error `[0,40]` = `3.781904305E-006`; above `40`, output is clamped near `exp(-40)` |
| Rollback input extrapolation | `ApproxExpNegPade33Wide40(decay * missingTicks)` | max abs decay error `[0,40]` = `3.781904305E-006`; deterministic finite fallback for non-finite input |
| Hydraulic erosion valley | `ApproxExpNegPade33Wide40(abs(...)*8)` | editor-only heightmap generation, bounded by `[0,40]` scan |
| BioForge smooth-min | polynomial smooth-min, radius `8/k` | exp/log removed; geometry blend is not exact log-sum-exp and must be visually rebaked/reviewed |

The CNS positive exp path is clamped at `x <= 4`; its worst absolute error is larger than the negative decay path but remains finite and monotonic for the existing saturated toxicity accumulator. It is not used for decompression damage authority.

AI cognition uses a local helper instead of `MathLodApproximation` because `Hecton8.Core.asmdef` already references `Hecton8.AI.Cognition`; adding the reverse reference would create an assembly cycle. The local helper matches the Padé coefficients and has `if` count `0`.

## Jacobi Iteration Caps

Power/logistics solver:
- `Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs:47` defines `2..50` propagation iterations.
- `Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs:52` resolves iterations from `GlobalQualityWeight`.
- `Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs:666` now reads the method input `globalQualityWeight`; the previous hardcoded authoritative weight was wrong and is fixed.
- `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:399` and `:2731` remove the old `1..10` / `1..8` clamps.
- `Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs:1194` self-audit now validates monotonic iteration/omega/tolerance/mask curves instead of the old constant solver assumptions.

Thermodynamics solver:
- `Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsJobs.cs:111` resolves `2..50` Jacobi passes.
- `Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsSolver.cs:276` derives tolerance/omega/mask from the continuous weight.

Curve:

`s(q) = saturate(q)^2 * (3 - 2 * saturate(q))`

`iterations(q) = round(lerp(2, 50, s(q)))`

Sample outputs:

| q | iterations |
|---:|---:|
| `0.0` | `2` |
| `0.1` | `3` |
| `0.5` | `26` |
| `1.0` | `50` |

Power curve samples with `baseTolerance=0.001`:

| q | iterations | omega | tolerance |
|---:|---:|---:|---:|
| `0.0` | `2` | `0.550000` | `0.032000` |
| `0.1` | `3` | `0.560360` | `0.031118` |
| `0.5` | `26` | `0.735000` | `0.016250` |
| `1.0` | `50` | `0.920000` | `0.000500` |

Stability rule: two iterations do not prove convergence. They only advance a bounded relaxation. The safety proof is bounded output, not false convergence:
- conductance/capacity is sanitized non-negative before use;
- denominator is guarded with `max(..., 1)`;
- pressure/potential is clamped to `[0,1]`;
- non-finite pressure marks the node divergent and writes the previous finite pressure;
- solver omega is damped from `0.55` to `0.92`, never aggressive `> 1`;
- residual tolerance is loose at low quality and strict at high quality.

Thus the low-quality path can be less accurate, but it cannot emit negative voltage, infinite current, or NaN state through the patched pressure lane.

## Bhaskara Trig Approximation

Implemented code path:
- `Assets/_Project/Scripts/MathLodApproximation.cs` implements `ApproxSinBhaskara`, `ApproxCosBhaskara`, and `ApproxSinCosBhaskara`.
- Scoped replacements were applied to storm propagation mock hurricane/wave math and toxic outgassing mock flow/world sampler visual fields.

Approximation:

`wrapped = radians / 2pi - floor(radians / 2pi)`

`x = wrapped * 2pi`

`m = x <= pi ? x : 2pi - x`

`sign = x <= pi ? 1 : -1`

`sin(x) ~= sign * 16*m*(pi-m) / (5*pi^2 - 4*m*(pi-m))`

The C# implementation uses `math.select`, arithmetic, `floor`, `rcp`, and finite clamps. Static audit for the Bhaskara core: `if=0`, ternary `0`.

Residual scan against `System.Math.Sin/Cos`:

| Function | Domain | Step | Max abs error | Worst x |
|---|---:|---:|---:|---:|
| `sin` | `[0, 2pi]` | `0.0001` | `0.001632192` | `6.0800` |
| `cos` | `[0, 2pi]` | `0.0001` | `0.001632311` | `4.5096` |

This is acceptable for the patched storm/toxic visual fields. It is not automatically approved for audio oscillator timbre, IK joint geometry, or combat authority without separate perceptual/residual proof.

## Branch Audit

Claim rejected: the whole jobs are not branchless.

Static `if (` count in audited files:

| File | `if` count |
|---|---:|
| `Assets/_Project/Scripts/MathLodApproximation.cs` | `3` |
| `Assets/_Project/Scripts/Physiology/ShinobuPhysiologyJobs.cs` | `50` |
| `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs` | `206` |
| `Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs` | `205` |
| `Assets/_Project/Scripts/Power/PowerGridJacobiContracts.cs` | `56` |
| `Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsJobs.cs` | `48` |

These branches include bounds checks, graph topology skips, fault handling, and signal emission. Removing them would trade predictable failure handling for undefined state. The approximation core itself is branchless; the simulation jobs are not.

## Extreme Input Behavior

Stress sample:
- input exponent value `1,000,000` clamps to `4`;
- approximation result = `0.018314883430837`;
- finite and saturated.

Extreme approximation scan:

| input | output | finite | `[0,1]` |
|---:|---:|---:|---:|
| `NaN` | `1.000000000` | `true` | `true` |
| `+Infinity` | `1.000000000` | `true` | `true` |
| `-Infinity` | `1.000000000` | `true` | `true` |
| `-1000000000` | `1.000000000` | `true` | `true` |
| `1000000` | `0.018314879` | `true` | `true` |
| `1000` | `0.018314879` | `true` | `true` |
| `4` | `0.018314879` | `true` | `true` |
| `1` | `0.367879450` | `true` | `true` |
| `0` | `1.000000000` | `true` | `true` |

Temperature `1,000,000 C` and pressure `1000 atm` remain domain-specific solver stress inputs. Existing patched lanes clamp/sanitize exponent and pressure/potential math, but this report does not certify every unrelated thermal or atmosphere formula as NaN-proof.

## Torture And Blackbox Contract

Added code:
- `Assets/_Project/Scripts/MathLodApproximation.cs:151` defines `MathLodTortureJob : IJob`.
- `Assets/_Project/Scripts/MathLodApproximation.cs:103` defines `MathLodTelemetryEntry`, explicit 64 bytes.
- `Assets/_Project/Scripts/MathLodApproximation.cs:269` defines cold dump path `Docs/AgentLogs/Dump_SHINOBU_300_MathLOD.bin`.

Current limitation: the job and dump writer are contracts, not yet wired into every solver owner. Full Task 09 remains incomplete until owners call the writer on their non-finite/divergent fault route.

## Verification

Numerical scan: PASS.

Latest static validator:
- scanned C# files: `2390`
- remaining direct transcendental total: `528`
- remaining `Mathf.Exp`: `0`
- remaining `math.exp`: `0`
- remaining `math.log`: `0`
- remaining `math.sin`: `204`
- remaining `math.cos`: `105`
- remaining `math.pow`: `22`
- hard failure remains because `math.sin`, `math.cos`, `math.pow`, `Mathf.Sin`, `Mathf.Cos`, `Mathf.Pow`, and `Mathf.Log` variants still exist.

`git diff --check` on touched files: PASS except repository CRLF normalization warnings.

Compile: NOT RUN. Latest CPU samples stayed above the project `50%` build gate; latest sample was `100%` with active `csc.exe` PID `20208`, so the project rule forbids launching `dotnet build`.
