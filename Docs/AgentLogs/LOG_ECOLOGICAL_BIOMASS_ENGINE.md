# ECOLOGICAL_BIOMASS_ENGINE Log

## 2026-05-13 - APEX_DIRECTOR - Lotka-Volterra Biomass Pacing

What was wrong:
- `EncounterDirector` had stress/token memory but no ecological memory. Killing predators did not reduce local predator availability.
- `EcosystemDirector` had a 1 km cinematic population table, not the requested 50 m predator/prey biomass macro-grid.
- Legacy `EntityDeathSignal` was destructively drained by the encounter bridge, so ecology could not observe death events safely.
- Save persistence had no biomass depletion payload.
- Flora presentation had no scalar connected to herbivore/prey collapse.

What was done:
- Extended `IEcosystemDirectorService` with `TryGetBiomassAvailability`.
- Added sparse 50 m macro-cell biomass SoA in `EcosystemDirector`: prey/predator front/back `NativeArray<float>`, carrying capacity, macro-cell coords, event flags, pending impact queue, and a 300-entry blackbox.
- Added Burst `BiomassLotkaVolterraJob` using the requested equations:
  - `dPrey = Prey * (BirthRate - PredRate * Predator)`
  - `dPred = Predator * (FeedRate * Prey - DeathRate)`
- Added capacity clamps derived from existing biome/food-density functions.
- Mirrored legacy `EntityDeathSignal` and `ItemAcquiredSignal` into typed `SignalBus<T>` snapshots.
- Added death, predation, apex-kill, and fish-acquisition impacts into local biomass.
- Modified fauna spawn selection and encounter threat authoring:
  - predator biomass below 0.1 doubles Leviathan cost.
  - prey biomass above 0.9 halves Swarm cost.
- Added four-neighbor diffusion with low-tier disable.
- Added `_HectonBiomassOvergrowth` shader scalar for low prey biomass.
- Added Scanner depletion warning through `HUDNotificationSignal` with `Ecological Collapse` hash.
- Added `ProgressionEventSignal` when predator biomass reaches zero, latched per cell.
- Added sbyte 0-100 RLE biomass save runs packed into the existing `EcosystemSectorSaveRecord` array used by `SaveBinaryStorage`.
- Added blackbox dump path `Docs/AgentLogs/Dump_ECOLOGICAL_BIOMASS_ENGINE.bin` on NaN/invalid biomass detection.

Cinematic cheats used:
- Sparse local macro-grid instead of dense world ecology.
- Euler LV integration every 5 seconds, not per frame.
- Shader scalar for kelp overgrowth instead of live flora mesh mutation.
- Four-neighbor diffusion fake for migration instead of simulating individual animals.
- Hash-only HUD warning instead of UI allocation.

Exact microseconds saved:
- Dense 50 m world grid rejected: saves unbounded memory and full-grid iteration; local active set targets 35-70 us per 512-cell FrostTick.
- Low-tier diffusion disable: saves estimated 30 us per 512-cell FrostTick on MX350.
- No GameObject ecology actors: saves per-frame Transform/MonoBehaviour overhead; target 0 us/frame outside signal drains.
- Signal snapshot drain: estimated 1.5 us/frame only when death/fish signals exist.
- Encounter biomass query: estimated 2 us per encounter cold tick.
- Flora shader global: estimated 0.4 us per cold tick versus per-instance flora edits.

Verification:
- Unity MCP validation blocked: `no_unity_session`.
- `dotnet build .\Hecton8.Core.csproj --no-restore /v:minimal` blocked by existing missing references unrelated to this work (`Hecton8.Core.Memory.Layout`, `Hecton8.Audio.Propagation`, `Hecton8.Physics.CCD`, acoustic types).
- Filtered build output showed no new `EcosystemDirector.cs` or `EncounterDirector.cs` diagnostics.
- `git diff --check` passed for edited files; line-ending warnings only.
- `<POLISH_MANDATE>` extraction returned `NO_POLISH_MANDATE`; manual anti-bloat scan found no new TODO/HACK/managed hot-path collections in edited ecology paths.

Blocked:
- Task 3 asmdef isolation is blocked by existing concrete `Hecton8.Core -> Hecton8.Ecosystem` references in registry/bootstrap/fauna/world code. New biomass access stays on `IEcosystemDirectorService` until that dependency is removed.
- Task 19 Burst verification remains blocked until a Unity session is attached.
