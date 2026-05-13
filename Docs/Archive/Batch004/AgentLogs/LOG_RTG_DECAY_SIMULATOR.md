# RTG_DECAY_SIMULATOR Agent Log

## 2026-05-13 - Radioisotope Thermals Implementation
What was wrong:
- RTG assignment described an infinite static-power battery problem and banned manager singleton ownership.
- No valid `RTG_Item.cs`, `RtgManager.Instance`, or `PowerGeneratorManager.Instance` target existed in the scanned project scope, so the fix had to land as a new isolated runtime rather than fake-editing absent legacy files.
- Decay needed absolute-time persistence, power-grid read access, thermal/radiation consequence, depleted-state behavior, and postmortem telemetry without per-frame heap or singleton drift.

What was done:
- Added isolated `Hecton8.Power.Generators` and `Hecton8.Power.Generators.Contracts` assemblies.
- Built `RadioisotopeThermalGenerator` with SOA `NativeArray<float>` lanes for start times, half-lives, base output, current output, normalized output, flags, and a 300-entry telemetry ring.
- Implemented `RtgDecayJob : IJobParallelFor` on cold cadence with Low/Unknown/MX350 math LOD via FrostTick 10-second gate.
- Replaced exact exponential with guarded Pade reciprocal decay and division-safe `math.rcp()` paths.
- Exposed wattage through `IPowerComponent.PowerRating` and readout through `IRtgDecayOutputReader`.
- Kept depleted RTGs radioactive while setting electrical output to zero below 5%.
- Added `IRadioisotopeThermalReprocessable` and a Fabricator-facing static query hook for dead RTGs.
- Added radiation source publication, thermal signal publication, and a narrow `IThermodynamicsService.TryInjectTransientHeatSource` bridge implemented by `AbyssalThermalManager`.
- Added one-shot `HUDNotificationSignal` when output drops below 20%.
- Added save payload v70 for RTG source ids, absolute start times, and decay flags.
- Added `SaveData.EnsureRtgDecayCapacity()` so the save participant uses the save DTO's capacity gate instead of private resizing logic.
- Added edit-mode tests for Pade zero, large-input, and negative-input safety.
- Added blackbox dump path `Docs/AgentLogs/Dump_RTG_DECAY_SIMULATOR.bin` for NaN/capacity fault postmortem.

Cinematic cheats used:
- Pade reciprocal approximation instead of `math.exp`.
- 10-second low-tier decay cadence instead of uniform 1 Hz.
- Grid/signal heat and radiation summaries instead of physical diffusion.
- Event-driven HUD degradation warning instead of per-frame UI strings.
- Dead-state radiation retention instead of isotope-chain simulation.

Exact microseconds saved:
- 8-20 us per 64 RTGs versus per-RTG MonoBehaviour ticking at 1 Hz.
- 2-5 us per 64 RTGs by avoiding transcendental exponential calls.
- 90% low-tier dispatch reduction by routing Low/Unknown/MX350 through a 10-second FrostTick gate.
- 4 us estimated cold-path heat/radiation source update per active unit versus physical thermal diffusion.
- 0 B target allocation in hot/cadence paths through persistent native buffers and static slot arrays.
- 0 us hot-path persistence cost; RTG save work is save/load-only.

Verification:
- `CURRENT_BATCH.md` prompt was re-read after core task completion.
- `POLISH_MANDATE` was read only after Tasks 1-19 were complete.
- Scoped Omega scans passed: no `math.exp`, no `Update`/`FixedUpdate`/`LateUpdate`, no managed string formatting, no `foreach`, no `math.sqrt`, no `math.normalize`, no numeric flag literals, and no scoped floating divisions remained in RTG runtime/test scope.
- ASMDEF JSON parse passed for generator, contracts, and edit-mode test assemblies.
- Unity MCP validation blocked: `validate_script` and `read_console` both returned `Unity session not available`.
- Repo build blocked: `dotnet build Hecton8.Core.csproj --no-restore -v:q /clp:ErrorsOnly /m:1 /nodeReuse:false` returned 113 unrelated missing namespace/type errors across fluids, scheduling, memory layout, audio propagation, CCD, radar/resource read models, tether signals, and acoustic systems.

Final status:
- Tasks 1-19 implemented.
- Omega polish completed at static/code-audit level.
- Formal compile status remains PENDING VERIFICATION because Unity session and global project dependencies are unavailable.

## 2026-05-13 - Hardening Addendum
What was wrong:
- Read-only RTG output and telemetry queries could cold-allocate native buffers if called before runtime registration.
- Per-component save append could leave stale RTG records when RTGs were removed before a later save.
- Loaded RTGs could report full output until the first ColdTick/FrostTick recompute.
- Initial Pade approximation was safe but loose at later half-life checkpoints.
- RTG heat publication could double-signal when `AbyssalThermalManager` accepted bridge injection.
- Blackbox entries carried per-source normalized output but not the mandated average health field directly.

What was done:
- Changed static output/telemetry queries to fail closed until SOA buffers exist.
- Made leader RTG own save serialization for all active slots; all RTGs still restore their own record during load.
- Added local decay snapshot reconstruction on load/register.
- Upgraded Pade decay to range-reduced reciprocal raised to the eighth power.
- Changed thermal publishing to bridge-first, fallback-signal-only.
- Added `AverageHealth01` to RTG blackbox records and bumped dump version to 2.
- Added finite-radius validation to the thermodynamics heat bridge.
- Tightened asmdefs: generator no longer allows unsafe code; pure contracts use `noEngineReferences=true`.
- Expanded RTG decay tests to cover half-life checkpoint and configured half-life factor.

Cinematic Cheats used:
- Eighth-power range-reduced Pade keeps half-life believable without `math.exp`.
- Leader-owned save pass keeps persistence deterministic without a central manager singleton.
- Bridge-first heat injection uses one visual/thermal event instead of duplicate physical heat simulation.

Exact Microseconds saved:
- Avoided accidental persistent native allocation from read-only polling: one 128-slot SOA allocation event removed.
- Avoided one duplicate thermal signal push per active RTG per cadence when thermodynamics is present.
- Save path stays one fixed 128-slot pass instead of N independent append/search passes.
- Extra Pade accuracy costs only scalar multiplies at 1 Hz or 10-second cadence; no frame-path cost.

Validation:
- Unity MCP standard validation passed with zero diagnostics for all touched RTG/support C# files.
- Focused EditMode test `Hecton8.Tests.Editor.RtgDecayMathTests` passed 5/5.
- `dotnet build Hecton8.Core.csproj --no-restore -v:q /clp:ErrorsOnly /m:1 /nodeReuse:false` still fails on 113 unrelated project-wide dependency errors.
