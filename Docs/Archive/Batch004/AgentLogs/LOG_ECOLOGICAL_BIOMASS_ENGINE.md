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

## 2026-05-13 - APEX_DIRECTOR - Second Audit Hardening

What was wrong:
- Fish acquisition signals include fabricator/construction/refund paths, so biomass could be deducted when no fish was caught.
- `SectorCleared` could fire for a cell that never had predator biomass if designers configured zero baseline predators.
- Scanner warning hash did not exactly match the batch text.

What was done:
- Filtered fish biomass drain to source kinds `Unknown` and `ResourceNode`; fabricator/construction source kind `4` no longer drains prey biomass.
- Added `BiomassCellFlagPredatorSeen` so predator-cleared progression only fires after predator biomass existed in that cell.
- Restored predator-seen state from biomass save runs when restored predator biomass is non-zero.
- Swapped scanner warning hash to `Warning: Ecological Collapse`.

Cinematic cheats used:
- One native byte flag carries progression history instead of a separate quest-side tracker.
- Source-kind filtering uses existing signal metadata instead of inventory/history reconstruction.

Exact microseconds saved:
- Avoided downstream false progression/HUD work from empty cells; estimated savings are workload-dependent, with the biomass-side guard below 1 us per 512 active cells.
- Avoided inventory polling for fish validation; signal-source check remains approximately 1 us only when item signals exist.

Verification:
- Unity MCP validation retried for `EcosystemDirector.cs` and remains blocked by `no_unity_session`.
- Filtered `dotnet build` after the patch still reports only existing shared dependency errors in `GlobalSignals.cs` and `GlobalRegistryContracts.cs`; no `EcosystemDirector.cs` or `EncounterDirector.cs` diagnostics appeared.
- `git diff --check` passed after the audit patch; line-ending warnings only.

Blocked:
- Full Unity compile/Burst validation still requires an attached Unity Editor session.

## 2026-05-13 - APEX_DIRECTOR - Persistence And Postmortem Hardening

What was wrong:
- Biomass RLE save capture used active-cell insertion order, so adjacent row cells often failed to merge.
- Biomass records only used the population high bit, which was weaker than a dedicated section marker.
- Lotka-Volterra authored rates were non-negative but not finite-bounded before entering the solver.
- Biomass blackbox dumps wrote raw ring memory without count/stride/start metadata.

What was done:
- Added finite clamping for LV authoring rates through `ClampLotkaVolterraRate`.
- Changed biomass save capture to row-sort active macro-cells before RLE emission.
- Added `BIOM` adaptation marker to biomass records.
- Changed blackbox dump format to write magic, valid entry count, struct size, oldest index, capacity, then valid entries oldest-to-newest.

Cinematic cheats used:
- Save-only O(n^2) row selection avoids an allocation buffer and keeps runtime paths untouched.
- Postmortem dump stays binary and fixed-stride rather than using managed JSON for frame entries.

Exact microseconds saved:
- Runtime: 0 us/frame changed; all work is sanitize/save/dump cold path.
- Save compression: adjacent row runs now collapse instead of writing one record per insertion-order cell; expected save IO reduction depends on overhunted region shape.
- Low-tier protection: finite LV rate cap prevents designer-authored spikes from turning FrostTick into clamp-to-extreme noise.

Verification:
- Unity MCP validation retried and remains blocked by `no_unity_session`.
- Filtered `dotnet build .\Hecton8.Core.csproj --no-restore /v:q /m:1` still reports only existing shared dependency errors in `GlobalSignals.cs` and `GlobalRegistryContracts.cs`; no `EcosystemDirector.cs` or `EncounterDirector.cs` diagnostics appeared.
- `git diff --check` passed after this hardening pass; line-ending warnings only.

Blocked:
- Full Unity compile/Burst validation still requires an attached Unity Editor session.
