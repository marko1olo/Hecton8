# RTG_DECAY_SIMULATOR Rationale

Status: IMPLEMENTED / UNITY VALIDATED / PENDING GLOBAL DOTNET BUILD - PROJECT DEPENDENCY WALL

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

## Decision 004 - Save Payload Version
Problem: RTG decay must survive load boundaries, but storing decay as current wattage would create save-scumming drift and reset half-life truth after long sessions.
Solution: Persist fixed source ids, absolute unscaled start times, and compact flags in `SaveData` v70, then recompute output from the Burst SOA after load.
Rejected Alternatives: Persisting current output only; storing per-frame decay deltas; using CustomModData strings. Those options either drift, allocate, or bypass the binary payload authority.
Scalability potential: Low/Middle/High/Ultra all load the same compact truth. High/Ultra can spend presentation budget on thermal/radiation VFX after the same deterministic restore.
Hardware Impact: Save-only arrays of 128 records; runtime cost is one indexed upsert per RTG during save/load, no hot path cost.

## Decision 005 - Reprocessing Boundary
Problem: Fabricator support is required, but directly editing Fabricator during parallel batch work risks merge conflicts and locks crafting to power internals.
Solution: Expose `IRadioisotopeThermalReprocessable` and `TryReprocessForFabricator(Component, out uint)` so Fabricator or item adapters can consume dead RTGs without referencing runtime state fields.
Rejected Alternatives: A direct Fabricator dependency, item-name string matching, or making RTGs inventory items before the crafting owner lands its changes.
Scalability potential: Low tier pays zero until the crafting query; High/Ultra can add better dismantle FX through the same contract.
Hardware Impact: Cold crafting path only; 0 B hot-path allocation.

## Decision 006 - Compile Wall
Problem: Final Unity compilation could not be read through MCP, and direct `dotnet build Hecton8.Core.csproj` fails on existing missing assembly references unrelated to RTG code.
Solution: Treat this as a dependency wall, keep RTG changes isolated, and record the exact build command and failure class in Status.
Rejected Alternatives: Editing unrelated csproj/asmdef references owned by other agents; reverting unrelated dirty files; declaring a clean compile without evidence.
Scalability potential: No runtime impact. The isolated asmdef reduces later integrator blast radius.
Hardware Impact: No device impact; validation is blocked by project composition, not runtime load.

## OMEGA POLISH CHANGES
Problem: Core RTG implementation was complete, but Omega requires proof that no expensive honest math, string churn, hidden per-frame loops, or cross-domain edits remained.
Solution: Re-read `CURRENT_BATCH.md`, then executed scoped scans against RTG runtime/test files. Replaced the remaining RTG runtime divisions with `math.rcp()` multiplication, replaced numeric Burst-job flag literals with named bitmask constants, and routed RTG save-array sizing through `SaveData.EnsureRtgDecayCapacity()`. Confirmed no `math.exp`, Unity per-frame loops, managed string formatting, `foreach`, `math.sqrt`, or `math.normalize` in the RTG scope.
Rejected Alternatives: A 1D LUT for decay was rejected because half-life is inspector-driven and slot-specific; the guarded Pade reciprocal is cheaper than table setup/cache pressure for 128 cold-cadence entries. Full `math.exp` and Taylor-without-denominator-guard remain rejected. Direct Fabricator, pipe runtime, and thermal internals edits remain rejected.
Scalability potential: Low/Unknown/MX350 runs decay through FrostTick with a 10-second gate; Middle/High runs 1 Hz ColdTick; Ultra uses the same deterministic truth and can spend the saved budget on stronger heat/radiation/HUD presentation. Toaster path stays visual-source-summary cheap; top-tier path can layer overkill presentation without changing data truth.
Hardware Impact: Estimated gain on i3/MX350 remains 8-20 us per 64 RTGs versus per-RTG MonoBehaviour ticking, 2-5 us per 64 RTGs by avoiding transcendental exp, 90% low-tier dispatch reduction, and 0 B hot/cadence GC. The Omega division cleanup removes two scalar divide sites from runtime/inspector paths.

Cross-domain justification:
- `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`: added one thermodynamics bridge contract so RTG does not hard-reference `AbyssalThermalManager`.
- `Assets/_Project/Scripts/World/AbyssalThermalManager.cs`: implemented that bridge through existing thermal event/signal flow instead of exposing internals.
- `Assets/_Project/Scripts/SaveData.cs` and `Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs`: added v70 fixed RTG decay start-time/flag payload so decay survives load boundaries.
- `Assets/_Project/Tests/Editor/Hecton8.EditModeTests.asmdef`: added references required for the RTG Pade guard tests.

Cinematic cheats used:
- Pade reciprocal `1 / (1 + x + 0.5x^2)` replaces exact exponential decay.
- Low-tier 10-second FrostTick replaces 1 Hz truth refresh.
- Thermal/radiation sources are summarized into existing grids/signals instead of physical diffusion.
- HUD warning is a one-shot signal below 20%, not a per-frame UI/string update.
- Dead RTG keeps radiation while wattage drops to zero, preserving player consequence without simulating isotope chains.

Final Git Diff:
- Tracked diff stat: `GlobalRegistryContracts.cs` +6, `SaveBinaryPayloadCodec.cs` +67/-1, `SaveData.cs` +63/-9, `AbyssalThermalManager.cs` +22, `Hecton8.EditModeTests.asmdef` +2.
- New untracked RTG files: `Assets/_Project/Scripts/Power/Generators/Hecton8.Power.Generators.asmdef`, `Assets/_Project/Scripts/Power/Generators/Contracts/Hecton8.Power.Generators.Contracts.asmdef`, `Assets/_Project/Scripts/Power/Generators/Contracts/RtgGeneratorContracts.cs`, `Assets/_Project/Scripts/Power/Generators/RadioisotopeThermalGenerator.cs`, `Assets/_Project/Tests/Editor/RtgDecayMathTests.cs`.
- Agent tracking files: `Docs/Tasks/Status_RTG_DECAY_SIMULATOR.md`, `Docs/AgentLogs/Rationale_RTG_DECAY_SIMULATOR.md`, `Docs/AgentLogs/LOG_RTG_DECAY_SIMULATOR.md`.

Verification result:
- PASS: ASMDEF JSON parsing for generator, contracts, and edit-mode tests.
- PASS: scoped Omega static scans for RTG code.
- PASS: Unity MCP `validate_script` returned zero diagnostics for all touched C# files in RTG/support scope.
- PASS: `Hecton8.Tests.Editor.RtgDecayMathTests` passed 5/5 focused EditMode tests.
- BLOCKED: `dotnet build Hecton8.Core.csproj --no-restore -v:q /clp:ErrorsOnly /m:1 /nodeReuse:false` returned 113 existing project-wide errors before RTG-specific compilation proof.

## Decision 007 - AAA Hardening Pass
Problem: The first implementation met the prompt, but deeper review found quality risks: read-only query methods could allocate SOA buffers, per-component save writes could leave stale records, loaded RTGs could briefly report full power before cadence, the cheap Pade curve was too loose at long ages, heat could be double-signaled when thermodynamics accepted injection, and the blackbox did not carry average health directly.
Solution: Removed allocation from read-only output/telemetry queries; made the leader save all active RTG slots in one pass while all RTGs still participate in load restore; added local decay reconstruction on load/register; upgraded Pade to an eighth-power range-reduced reciprocal; changed heat publication to bridge-first with one fallback signal; added `AverageHealth01` to dump entries and bumped the dump version; hardened the thermodynamics bridge against NaN radius; tightened asmdefs by disabling unsafe code and removing Unity engine references from pure contracts.
Rejected Alternatives: Keeping per-component save append was rejected because removed RTGs could persist stale records. Exact `math.exp` was rejected because the range-reduced Pade now passes half-life checkpoint tests without transcendental cost. A LUT was rejected because inspector half-lives are continuous and table/cache pressure is not justified for 128 cold-cadence slots. Duplicate thermal signaling was rejected because it wastes event bandwidth and can overstate local heat.
Scalability potential: Low/MX350 still pays only a 10-second cadence and now avoids accidental native allocation from UI/logistics reads. Middle/High/Ultra get tighter decay believability and cleaner heat/radiation hooks without changing the data contract.
Hardware Impact: i3/MX350 avoids one persistent native allocation from accidental read access, avoids duplicate heat signal push per active RTG cadence, keeps save to one 128-slot pass, and pays only a few extra scalar multiplies per RTG for much better Pade accuracy. Focused tests prove the half-life point within 0.01.

Validation Addendum:
- `validate_script` zero diagnostics: `RadioisotopeThermalGenerator.cs`, `RtgGeneratorContracts.cs`, `RtgDecayMathTests.cs`, `SaveData.cs`, `SaveBinaryPayloadCodec.cs`, `AbyssalThermalManager.cs`, `GlobalRegistryContracts.cs`.
- EditMode: `Hecton8.Tests.Editor.RtgDecayMathTests` total 5, passed 5, failed 0, skipped 0, duration 0.3635108 seconds.
- Console after test run: no RTG compile diagnostics; Unity returned only the test-result save line.
- Dotnet project build remains blocked by unrelated generated/project reference wall: 113 errors across fluids, scheduling, memory layout, audio propagation, CCD, radar/resource read models, tether signal, acoustic types.
