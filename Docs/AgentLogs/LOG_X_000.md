## X_000 Loop 40 - Sargassum Global Drag Final Direct NativeArray Cleanup

What was wrong: `SargassumGlobalDragManager` retained `_densityBuildSources`, `_scavengerMatricesNative`, and `_scavengerBatchMetadata` as persistent direct `NativeArray` fields.
What was done: moved all three to WorldSargassum DataVault descriptors, added BufferIDs 74403..74405, kept density source lock through scheduled job completion, and made scavenger matrix/metadata views method-local with `finally` release.
Cinematic cheats used: no new simulation; preserved existing scavenger cardinal/orbit fake and density-field approximation.
Exact microseconds saved: not measured in Unity Profiler. Static gain is removal of 3 retained unmanaged aliases; expected GC delta 0 B/frame by code inspection.
Verification: `dotnet build Hecton8.Editor.csproj /nr:false -p:UseSharedCompilation=false -v:minimal` succeeded with 0 warnings/0 errors in 00:00:50.01. Roslyn audit hash `91f4d3c62deea775222c8865966da74234c9e9665817e1d3f050b816a2212db9`; SargassumGlobalDragManager has 0 forbidden native fields. Project-wide residual remains 1996 forbidden persistent candidates and 465 MonoBehaviour candidates across 25 files.
