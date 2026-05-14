# LOG_INTEGRATION_ASSEMBLY_SURGEON

## 2026-05-15 - No-Dotnet H-Phi Continuation

What was wrong:
- `Directory.Build.props` lacked the Core-only project-reference isolation gate on disk during 2026-05-15 readback.
- `Docs/Tasks/Status_INTEGRATION_ASSEMBLY_SURGEON.md` and `Docs/AgentLogs/Rationale_INTEGRATION_ASSEMBLY_SURGEON.md` still described the older isolated-Core state and had no final log file.
- Generated `Hecton8.Core.csproj` still lists package/vendor project references: URP, GPUInstancer, Crest, WaveHarmonic, EasySave3, VolumetricLightBeam, input projects, and contracts.
- `Assets/_Project/Scripts/Hecton8.Core.asmdef` still references broad leaf/domain assemblies. This is H-Phi debt, but current Core-owned source still uses leaf types, so blind deletion would break the compile lane.

What was done:
- Restored the source-backed `Directory.Build.props` gate for `Hecton8.Core`: default `BuildProjectReferences=false` unless `HectonBuildProjectReferences=true`.
- Rechecked `Directory.Build.targets`: current bridge surface has no `Hecton8.Animation.IK` reference.
- Rechecked current batch: `Docs/Tasks/CURRENT_BATCH.md` no longer contains `INTEGRATION_ASSEMBLY_SURGEON` or a polish tag, so no neighboring prompt was parsed.
- Audited Core asmdef references and recorded the remaining H-Phi debt instead of hiding it.
- Updated status and rationale with the no-dotnet evidence boundary.

Cinematic cheats used:
- Build-graph isolation instead of vendor/package source repair.
- No runtime simulation, rendering, physics, audio, NativeContainer, or gameplay path was changed.

Exact microseconds saved:
- Runtime frame time: 0 us changed.
- Fresh 2026-05-15 build-time savings: not claimed because the user forbade dotnet rebuilds.
- Historical artifact only: `Build_INTEGRATION_ASSEMBLY_SURGEON_05_RawCoreDefault.log` from 2026-05-14 reports 31,300,000 us, 0 warnings, 0 errors after the same Core default isolation pattern.

Verification:
- Evidence class for this continuation: STATIC_SOURCE and STATIC_DOC only.
- No `dotnet build`, `dotnet rebuild`, or `dotnet msbuild` was run during this continuation.
- `Directory.Build.props` now contains the Core `BuildProjectReferences=false` default with `HectonBuildProjectReferences=true` opt-in.
- Owned-file static poison scan found no `foreach`, `string.Format`, interpolation, `.ToString(`, `math.sqrt`, `math.normalize`, managed collection construction, `Task.Run`, Addressables instantiate, or unload calls in `Directory.Build.props`.
- `git diff --check` on owned files reported no whitespace errors, only repository CRLF normalization warnings for `Directory.Build.props`.

Residual risk:
- Fresh compile is PENDING VERIFICATION by user no-dotnet order.
- Unity Editor import, Unity Console, Play Mode, profiler, GCMonitor, and player build were not run.
- Full H-Phi asmdef cleanup requires staged contract extraction for `LeviathanTerrainIkJob`, `MacroSwarm`, `SoundEmissionSignal`, and other cross-domain DTOs before leaf references can be safely removed from `Hecton8.Core.asmdef`.
