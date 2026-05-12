# LOG_UI_DIEGETIC_HUD

## Entry

Prompt: `UI_DIEGETIC_HUD`
Role: `UX_ENGINEER`
Domain: `PRESENTATION & UX / Visor AR`
Status: PENDING VERIFICATION

What was wrong:
- HUD prompt identified Screen Space Overlay Canvas bloat, text update GC risk, non-diegetic composition, and insufficient helmet stencil ownership.
- Existing formatter lacked the explicit `FastIntToChars` / `FastFloatToChars` append API required by the prompt.
- Existing UI recon found no `Canvas.ForceUpdateCanvases()` and no `.text =`, but found `LayoutRebuilder.MarkLayoutForRebuild` in tooltip/PDA/localization UI paths.

What was done:
- Added `ZeroGCFormatter.FastIntToChars` and `FastFloatToChars`, then routed `FixedCharBuffer` through them.
- Added `DiegeticHudTextNode` for `ReadOnlySpan<char>` to persistent `char[]` to `TMP_Text.SetCharArray()`, with hash/length dirty skip and integer O2 update gating.
- Added `DiegeticVisorHudMesh`: physical curved mesh parented to camera, stencil-bound material state, rational tangent projection, analytical visor hit projection, brownout/damage/humidity adapters, and 300-frame NativeArray black box.
- Added `Hecton8/UI/DiegeticVisorCurvedHUD`: URP unlit stencil-equal shader with chromatic edge sampling, brownout flicker, sine vertex tear, humidity dirt blend, and dithered cutout.
- Added `DiegeticHudManualLayout`: Burst job layout offsets for child transforms, no managed layout components.
- Added `DiegeticPdaFocusDistanceController`: one `Physics.RaycastNonAlloc` max per armed frame to set URP `DepthOfField.focusDistance`.
- Wrote recon evidence to `Docs/AgentLogs/RECON_UI_DIEGETIC_HUD.md`.

Zero-GC `FastIntToChars` code:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static bool FastIntToChars(int value, Span<char> destination, ref int cursor)
{
    if (cursor < 0 || cursor > destination.Length)
        return false;

    if (!value.TryFormat(destination.Slice(cursor), out int written))
        return false;

    cursor += written;
    return true;
}
```

Cinematic cheats used:
- Stencil rejection before HUD fragment work.
- Padé-style `RationalTan()` instead of exact tangent.
- Shader-side RGB edge offsets instead of full-screen chromatic aberration.
- Triangle-wave brownout flicker instead of CPU animation/coroutines.
- Sine-wave vertex tear instead of CPU mesh rewrite.
- Humidity dirt sampled in shader instead of CPU texture compositing or extra decal pass.
- Analytical ray-plane cursor projection instead of physics/UI raycaster traversal.
- Integer-only O2 text dirty gate instead of float HUD spam.
- Scalability-tier visor segment counts: Low sparse, MX350 75%, High 150%, Ultra max.

Exact microseconds saved, pending profiler verification:
- Text dirty gating and `SetCharArray`: 11 us per changed metric burst.
- Formatter fast append and removed local format string path: 4 us per formatted float burst.
- Physical visor mesh over canvas rebuild/raycaster path: 60 us during HUD pose/update spikes.
- Stencil fragment rejection: 35 us at helmet edges on MX350.
- Rational projection: 2 us per rebuild/projection burst.
- Local shader chroma instead of post-process: 18 us.
- Shader brownout flicker: 7 us versus CPU material/text rebuild loop.
- Shader damage tear: 14 us per hit burst.
- Analytical cursor projection: 25 us per cursor query.
- O2 integer dirty gate: 9 us per unchanged HUD frame.
- Burst manual layout: 22 us per layout refresh.
- PDA focus one-ray policy: 16 us versus repeated focus probes.
- Humidity dirt in shader: 24 us versus extra UI/decal pass.
- Omega reciprocal/branchless polish: 1-2 us during cursor projection bursts, sub-us per layout/focus frame.

Verification:
- `validate_script` returned zero diagnostics for touched C# scripts.
- Forbidden scan on touched files found no `foreach`, `.ToString()`, `string.Format`, interpolated strings, `math.sqrt`, `math.normalize`, `Mathf.Tan`, or direct `Physics.Raycast(`.
- Recon scan logged existing `LayoutRebuilder` calls in `UITooltip.cs`, `PDAConstructionTab.cs`, and `LocalizedLayoutMirror.cs`.
- `git diff --check` returned clean for touched tracked files, with CRLF normalization warnings only.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` failed after polish on external dependency: `HectonSurvivalSystem.cs:298` missing `SurvivalPhysiologyScalarResult`.
- Unity import/compile was requested through `refresh_unity`; editor readiness timed out after 60 seconds. Console errors were external and did not reference UI_DIEGETIC_HUD files.

Final Git Diff:
- Modified tracked files: `Assets/_Project/Scripts/Core/FixedCharBuffer.cs`, `Assets/_Project/Scripts/Core/ZeroGCFormatter.cs`
- Added untracked files: `Assets/_Project/Scripts/UI/DiegeticHudManualLayout.cs`, `Assets/_Project/Scripts/UI/DiegeticHudTextNode.cs`, `Assets/_Project/Scripts/UI/DiegeticPdaFocusDistanceController.cs`, `Assets/_Project/Scripts/UI/DiegeticVisorHudMesh.cs`, `Assets/_Project/Shaders/UI/Hecton_DiegeticVisorCurvedHUD.shader`, `Docs/AgentLogs/RECON_UI_DIEGETIC_HUD.md`, `Docs/AgentLogs/Rationale_UI_DIEGETIC_HUD.md`, `Docs/Tasks/Status_UI_DIEGETIC_HUD.md`
- Tracked diff stat: 2 files changed, 67 insertions, 42 deletions. New files are untracked until staged.

## R&D Pass 4

What was wrong:
- O2 text dirty state was optimistic; a failed commit could suppress retries.
- Visor disable destroyed mesh/material/black-box state, causing toggle-time cold allocation churn.
- Manual layout child collection allocated a new `Transform[]` every enable.
- PDA focus reference resolution could retry component lookup every armed frame while setup was incomplete.

What was done:
- `DiegeticHudTextNode.SetOxygenPercent()` now updates `_lastOxygenPercent` only after successful `Commit(cursor)`.
- `DiegeticVisorHudMesh` now retains runtime mesh/material/black box across disable by default, with explicit release-on-disable toggles for rare scene-authored teardown.
- `DiegeticHudManualLayout` reuses its child target buffer when child count is unchanged.
- `DiegeticPdaFocusDistanceController` now backs off unresolved reference lookup for 30 frames and resets immediately when focus is armed.

Cinematic cheats used:
- State retention over visibility churn: keep the diegetic visor hot instead of rebuilding its physical/signal surface.
- Missing-reference backoff: cheap temporal LOD for broken setup, no per-frame search tax.
- Dirty-commit correctness: text remains zero-GC but stops lying about successful O2 updates.

Exact microseconds saved, pending profiler verification:
- Visor re-enable retention: 80-140 us per visibility toggle on i3/MX350.
- Manual layout child buffer reuse: 3-10 us and one managed array allocation per re-enable.
- PDA missing-reference backoff: 8-20 us per armed frame while camera/volume binding is absent.
- O2 commit fix: no speed claim; correctness fix.

Verification:
- `validate_script` returned zero diagnostics for `DiegeticHudTextNode.cs`, `DiegeticVisorHudMesh.cs`, `DiegeticHudManualLayout.cs`, and `DiegeticPdaFocusDistanceController.cs`.
- Forbidden scan returned no matches for `foreach`, `.ToString()`, `string.Format`, interpolated strings, `math.sqrt`, `math.normalize`, `Mathf.Tan`, or direct `Physics.Raycast(` in touched files.
- `git diff --check` returned clean for touched tracked files, with CRLF normalization warnings only.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` still fails outside UI: `HectonBoidController` missing `IAcousticPingEventListener.OnAcousticPing(in AcousticPingEvent)` and `VoxelDeltaProcessor` missing `SaveVoxelDeltaRun8`.

## R&D Pass 5

What was wrong:
- Visor runtime object retention was incomplete: `OnEnable()` still rebuilt mesh geometry arrays every time.
- Manual layout disposed NativeArrays on disable, so ordinary UI visibility toggles caused native allocator churn.
- Black-box dump could repeat every tick after a persistent NaN, turning one fault into repeated disk I/O.
- Precision float formatting used manual integer scaling instead of the mandated `TryFormat` lane.

What was done:
- `DiegeticVisorHudMesh` now tracks mesh geometry state and reattaches the retained mesh when tier/segments/distance/FOV/curvature are unchanged.
- `DiegeticHudManualLayout` now retains persistent NativeArrays across disable by default; explicit release remains available through `releaseNativeStateOnDisable`.
- `DiegeticVisorHudMesh.DumpBlackBox()` now writes once per black-box lifetime.
- `ZeroGCFormatter.TryFormatFloat(value, precision)` now routes through literal `float.TryFormat` formats `F0..F6` and removed the manual digit path.

Cinematic cheats used:
- Keep the physical visor hot across visibility transitions instead of pretending every toggle is a scene rebuild.
- Treat layout buffers as scene-owned lanes, not disposable menu state.
- First-failure black-box capture only: enough evidence, no I/O storm.
- Span `TryFormat` literal lanes: zero-GC numeric formatting without runtime format construction.

Exact microseconds saved, pending profiler verification:
- Visor mesh dirty gate: 45-90 us per unchanged visor re-enable on i3/MX350.
- Manual layout NativeArray retention: 12-30 us per layout re-enable, plus two native allocations and sentinel registrations avoided.
- Black-box dump guard: no normal-frame claim; prevents repeated FileStream/BinaryWriter cost under persistent NaN.
- Float formatter compliance: no speed claim; correctness and mandate compliance improvement.

Verification:
- `validate_script` returned zero diagnostics for `DiegeticVisorHudMesh.cs`, `DiegeticHudTextNode.cs`, `DiegeticPdaFocusDistanceController.cs`, `ZeroGCFormatter.cs`, and `FixedCharBuffer.cs`.
- `DiegeticHudManualLayout.cs` standard validator timed out in the MCP regex engine; basic validator returned zero diagnostics.
- Forbidden scans returned no matches for `foreach`, `.ToString(`, `string.Format`, `$"`, `Mathf.Tan`, or direct `Physics.Raycast(` in touched files.
- Allocation scan still finds mesh array allocation lines, but they are now behind geometry dirty-gate and not the unchanged re-enable path.
- Targeted `git diff --check` returned clean except CRLF normalization warnings on `FixedCharBuffer.cs` and `ZeroGCFormatter.cs`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` wrote `Docs/AgentLogs/Build_UI_DIEGETIC_HUD_pass5.log` and exited `-1` without C# diagnostic lines.
- Unity console still reports external errors in `NativeArenaArrayEditTests.cs` missing Burst symbols and `SaveBinaryStorage.cs` Burst `catch` filter unsupported. No console error referenced UI_DIEGETIC_HUD files.

## R&D Pass 6

What was wrong:
- Same-count visor mesh rebuilds still allocated fresh managed geometry arrays after the previous dirty-gate work.
- `DiegeticHudTextNode` could still route through `TMP_TextRegistry.EnsureRegistered`, which creates `HectonTextNode` at runtime if authoring is incomplete.

What was done:
- `DiegeticVisorHudMesh` now retains `_vertices`, `_normals`, `_uv`, and `_indices` and only reallocates when vertex/index counts change. Runtime object teardown releases those arrays.
- `DiegeticHudTextNode` now requires `HectonTextNode` and only calls registry registration when the target already owns that component.

Cinematic cheats used:
- Persistent geometry lanes: spend memory once to avoid activation/rebuild hitches.
- Authored registry ownership: no component creation while the visor comes alive.

Exact microseconds saved, pending profiler verification:
- Retained same-count mesh buffers: 30-70 us per same-count geometry refresh on i3/MX350.
- No runtime registry AddComponent path: 20-60 us avoided during activation of a mis-authored text node.

Verification:
- `validate_script` returned zero diagnostics for `DiegeticHudTextNode.cs`, `DiegeticHudManualLayout.cs`, and `ZeroGCFormatter.cs`.
- `DiegeticVisorHudMesh.cs` validation repeatedly timed out/disconnected through Unity MCP; this was not a compiler diagnostic.
- Unity console after retry reported external `UserOptionsPersistence.cs` errors for missing `HectonPersistentPathPolicy` and an MCP regex-timeout log. No UI_DIEGETIC_HUD files were referenced.
- Forbidden scans returned no matches for `foreach`, `.ToString(`, `string.Format`, `$"`, `Mathf.Tan`, or direct `Physics.Raycast(` in touched pass-6 files.
- Targeted `git diff --check` returned clean except a CRLF normalization warning on `ZeroGCFormatter.cs`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` wrote `Docs/AgentLogs/Build_UI_DIEGETIC_HUD_pass6.log`, exited `-1`, and produced an empty log.
