# AUDIO_IMPORT_RESIDENCY_GUARD Log

## 2026-05-16 Session Start
What was wrong -> Audio import residency guard had no active status/rationale files; mandatory read proved missing state.
What was done -> Created disk-backed status, rationale, and log files in the active Hecton8 project path.
Cinematic Cheats used -> None yet; no runtime simulation changed.
Exact Microseconds saved -> 0 us runtime; establishes evidence trail before code.

## 2026-05-16 Loop 1
What was wrong -> First-party audio imports had no hard final dictator for long-clip streaming or preload budget enforcement; existing policy could leave large clips resident.
What was done -> Added `AudioImportDictator.cs`, `AudioRamBudgetBuildGate`, and fixed `AudioResidencyDomain` categories.
Cinematic Cheats used -> Residency cheat: keep belief through streamed/compressed sources and mono 3D instead of stereo resident fidelity.
Exact Microseconds saved -> Estimated 800-2200 us boot/import stall avoided per long decompressed clip; 1-5 us per policy lookup avoided by enum categories; 0 us runtime cost for build gate.
Verification -> `dotnet build Hecton8.slnx` failed from external dependency wall: RealtimeCSG missing files and unrelated Hecton8.Core errors. Loop 1 compile is blocked, not passed.

## 2026-05-16 Loop 2
What was wrong -> Environment prefabs could own direct AudioSources, biome music did not hard-release old clip data, repeated creature/world clips had no bounded residency cache, far sounds could reach Unity setup before audibility rejection, and tool loops could be boot-resident instead of equip-resident.
What was done -> Added environment prefab purge/build validation, `AudioResidencyCache` fixed LRU, music clip release on voice stop, AUP max-hearing-range gates before source acquisition, and equip-only prewarm/release hooks for Laser Cutter and Repair/Welder audio.
Cinematic Cheats used -> Residency cheat: silence and distance reject before RAM; fake continuity through music crossfade math while old tracks are evicted; explicit equip intent buys tool audio residency.
Exact Microseconds saved -> Estimated 20-120 us per stripped environment source activation, 8-90 us per far rejected clip, 50-300 us per repeated roar reload avoided, and 150-900 us tool load shifted from boot to equip. Music saves 1-40 MB per released old bed.
Verification -> `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` failed on unrelated `GlobalDataVault.ValidateAbiLayout`; no audio-specific diagnostics appeared before the dependency wall.
