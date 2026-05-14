# LOG_INTEGRATION_ASSEMBLY_SURGEON

## 2026-05-14 - Compile Wall Closure

What was wrong:
- First wall log reported 2 stale compile errors: missing `LeviathanTerrainIkJob.TailWhipDurationSeconds` and missing `PrologueSplashdownSineSweepProbeJob`. Current source already contained both symbols.
- Follow-up Core build cleared errors but exposed 30 CS0436 duplicate IK warnings from stale generated assembly/source overlap.
- Raw Core build then succeeded but carried 47 third-party package warnings from URP, GPUInstancer, Crest, ShaderGraph, and WaveHarmonic projects.

What was done:
- Rebuilt before adding shims; stale symbol wall cleared without duplicating runtime code.
- Rechecked duplicate-member targets; only one `DrainChunkDehydratedSignals` and one `OnGlobalRegistryServiceReplaced` implementation exist.
- Confirmed `Directory.Build.targets` has no `Hecton8.Animation.IK` bridge reference in the current bridge surface.
- Added `Directory.Build.props` Core-only default: `BuildProjectReferences=false` unless `HectonBuildProjectReferences=true` is supplied.
- Verified plain `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -v:minimal -clp:Summary` now produces `Temp\bin\Debug\Hecton8.Core.dll`.

Cinematic cheats used:
- Build-graph isolation instead of vendor-source repair. Runtime visuals untouched.
- No simulation, physics, audio, VFX, or NativeContainer hot path was changed.

Exact microseconds saved:
- Runtime frame time: 0 us changed.
- Measured build-wall path: `Build_INTEGRATION_ASSEMBLY_SURGEON_03.log` was 185,800,000 us with 47 warnings; `Build_INTEGRATION_ASSEMBLY_SURGEON_05_RawCoreDefault.log` is 31,300,000 us with 0 warnings.
- Measured build-time reduction: 154,500,000 us on this workstation path.

Verification:
- `Build_INTEGRATION_ASSEMBLY_SURGEON_05_RawCoreDefault.log`: Build succeeded, 0 warnings, 0 errors, 00:00:31.30.
- `git diff --check` on owned files: no whitespace errors; only repository CRLF normalization warnings.
- `Directory.Build.props` anti-bloat scan: no `foreach`, `string.Format`, interpolation, `.ToString(`, `math.sqrt`, `math.normalize`, managed collection construction, `Task.Run`, Addressables instantiate, or unload calls.
- Active `Docs/Tasks/CURRENT_BATCH.md` no longer contains `INTEGRATION_ASSEMBLY_SURGEON` or a polish tag; closure used disk-backed status/rationale instead of neighboring prompts.
