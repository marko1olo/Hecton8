# RTG_DECAY_SIMULATOR Rationale

Status: PENDING VERIFICATION

## Decision 001 - Runtime Ownership
Problem: RTG power must not become another singleton or concrete dependency in a batch with many agents editing adjacent power/logistics files.
Solution: Build the RTG runtime as an isolated generator component implementing `IPowerComponent`, with Burst-owned SOA buffers and optional contract interfaces for read-only output access.
Rejected Alternatives: `PowerGeneratorManager.Instance` and `RtgManager.Instance`; both violate GlobalRegistry/DI and create direct dependency pressure on other agents' systems.
Scalability potential: Low uses 10-second decay cadence; Middle/High use 1 Hz; Ultra can keep the same truth cadence and spend saved cycles on visual heat, radiation, and HUD response.
Hardware Impact: i3/MX350 avoids per-frame RTG work; estimated hot-path heap impact 0 B and cold-cadence CPU cost below 0.02 ms for 64 units.

## Decision 002 - Decay Math
Problem: `math.exp` per RTG is correct but wasteful for a 1 Hz gameplay decay where visual belief matters more than atomic accuracy.
Solution: Use half-life lambda with a guarded Pade approximation `1 / (1 + x + 0.5x^2)` for non-negative decay. Clamp denominators with `math.max(epsilon, value)` and `math.rcp()`.
Rejected Alternatives: Full `math.exp` every job pass; Taylor polynomial without reciprocal guard; per-real-isotope simulation.
Scalability potential: Low/MX350 cadence saves dispatches; High/Ultra use the saved budget for stronger thermal/radiation presentation rather than more precise isotope math.
Hardware Impact: Approximation removes transcendental calls; estimated low-end gain 2-5 microseconds per 64 RTGs per decay pass.

## Decision 003 - Heat And Radiation Coupling
Problem: RTGs must remain hot and radioactive, including when electrically dead, without making Thermodynamics or Radiation own power state.
Solution: Use existing static signal paths where available: `RadiationHazardGrid.RegisterSource/UnregisterSource` and `GlobalSignals.Publish(TemperatureChangedSignal)`/thermal spatial proxies. Power output remains read-only through `IPowerComponent`.
Rejected Alternatives: Direct mutation of `AbyssalThermalManager` internals; per-frame `GlobalRegistry.Thermodynamics` polling; physical diffusion per generator.
Scalability potential: Low tier gets source intensity summary only; Ultra can layer better VFX from the same output percentage.
Hardware Impact: One cold-cadence source update per RTG, no per-frame work; estimated 4 microseconds per active unit on low-end silicon before Unity signal overhead.

