# ZERO_GC_UI_PIPELINE
Date: 2026-05-07

Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R47 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

R47 root/architecture authority-spine/runtime-wording/counter-drift correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md`) is the latest local static DOC_GLOBAL boundary for architecture/root documentation. R46 remains the prior interior-authority/route-field/proof-language correction. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41 remains the prior global-authority/internal-residue correction; R40 remains the prior R38-residue/source-counter correction; R39 remains the prior authority-counter/proof-wording correction; R38/R37/R36/R35/R34 remain prior static correction layers. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM. These anchors prove only that the cited local paths exist at this capture time; they are not compile, Unity import, Play Mode, profiler, GC, player-build, save/load, platform, or visual proof.
- `Assets/_Project/Scripts/Core/ZeroGCFormatter.cs`
- `Assets/_Project/Scripts/ZeroGCStringCache.cs`
- `Assets/_Project/Scripts/UI/BlackBoxMetricDashboard.cs`
- `Assets/_Project/Scripts/UI/DiegeticHudTextNode.cs`
- `Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs`
- `Assets/_Project/Scripts/LocRegistry.cs`

## 2026-05-20 DOC_GLOBAL R46 Root/Architecture Boundary Note

R47 root/architecture authority-spine/runtime-wording/counter-drift correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md`) (R46 prior interior-authority/route-field/proof-language correction; R45 prior R43/R44 residue/proof-artifact/source-counter correction) keeps this file as a static UI allocation contract, not GCMonitor, profiler, canvas rebuild, input, or Play Mode proof. Current DOC_GLOBAL boundary is `Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md`; R46 remains prior at `Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md`; R45 remains prior at `Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md`; R44 remains prior at `Docs/Reports/2026-05-20_DOCUMENTATION_R44_ROOT_ARCHITECTURE_INTERNAL_RESIDUE_EXACT_ROUTE_FIELDS_LOCAL.md`; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current static gates: `Tools/AtlasCheck.py` remains red on `ATLAS_CHECK_FAIL references=6781 missing=61` (one Dynamic Decals vendor asset ref, RealtimeCSG vendor icon/readme image refs, missing HectonMaskChannelPacker/HectonMaterialChannelPackValidator editor source refs, and missing HabitatDamageBakePipeline source ref in the current atlas); `Docs/Modding/Validate_Mod_API_Static.ps1` passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only. Runtime proof remains absent.

Verification: PENDING VERIFICATION

2026-05-07 current-state boundary:

- This is the UI zero-GC contract and source-oriented pattern reference, not profiler proof.
- Historical project-state orientation previously started at `Docs/Reports/2026-05-07_MAIN_DOCUMENTATION_CURRENT_STATE_REFRESH.md`, then `Docs/Reports/2026-05-07_FINAL_INQUISITION_NATIVE_SCANNER.md`, `Docs/Reports/2026-05-07_BRUTAL_SYNCHRONIZATION_REPORT.md`, `Docs/Reports/2026-05-06_DOCUMENTATION_SYNCHRONIZATION_PASS.md`, `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`.
- Any claim of `0 B/frame` for HUD/PDA/menu paths still requires fresh GCMonitor or profiler capture.
- Presentation/UI must not own gameplay state transitions without a logic-owned fallback.

## Scope

This project does not push runtime HUD numbers into `TMP_Text.text`. Hot-path UI text is staged through fixed buffers, formatted with `Span<char>`, and committed through `TMP_Text.SetCharArray(...)`.

## 2026-05-19 SHINOBU_150 Babel Subtitle Sync Addendum

Evidence class: STATIC_SOURCE. Runtime proof remains pending Unity import, Burst compile, Play Mode, GCMonitor, and profiler capture.

- Runtime subtitle text must resolve through hash-addressed UTF-8 spans, not JSON or `Dictionary<string,string>` lookup.
- Runtime mod localization dictionary injection is disabled; modded localization must wait for a binary/hash Babel envelope route instead of mutating live string tables.
- `LocRegistry.TryWriteVisualSpanFromUtf8` is the hot decode seam. It writes UTF-8 into caller-owned `Span<char>` and supports `^0..^3`, `{0}`, and `{0:format}` token injection through `BabelFormatArgs`.
- `LocRegistry.ReloadBinaryOrMock(...)` is the only registry reload route for Babel. Managed `LocalizationManager` string compatibility tables no longer feed the zero-GC registry.
- The registry fallback decode ring now permits 4096 glyphs per slot; long lore surfaces should still provide their own fixed span sized for the target page.
- The legacy raw-buffer compatibility seam now uses a fixed 16-slot `char[4096]` decode ring instead of a thread-static grow-on-first-use buffer. It exists for old callers only; hot subtitle delivery stays caller-span or `CharBufferPool` based.
- `LocNumericBuffer` now uses a fixed 16-slot prewarmed `char[4096]` ring for numeric template APIs that return `char[]`; the previous thread-static grow buffer and `new char[capacity]` overflow route are removed. Hot HUD paths should still use caller-owned `Span<char>` overloads where possible.
- `CharBufferPool.RequiredBabelTextCapacity` is 512 chars for 500 subtitle slots; megabyte lore uses encyclopedia/page-owned spans instead of expanding subtitle leases.
- `CharBufferPool` resolves the Babel UTF-16 arena from Vault buffer `(BufferID)70540` when available and no longer owns a persistent private `NativeArray<char>` or `NativeBitArray` fallback.
- `BabelSubtitleSyncRuntime` owns the 32-byte `SubtitleCueDTO` vault buffer, 16-byte `SubtitleCueSignal` lane, and 300-frame localization telemetry ring.
- `LocRegistry` registry DTOs/signals are explicit 16/24/32/64-byte layouts, and missing-key suppression is a fixed 256-bit bloom mask rather than a managed `HashSet<int>`.
- `SubtitleManager` no longer uses `List<SubtitleRequest>` for legacy subtitle queueing; all subtitle request lanes are fixed rings or caller-owned spans.
- Subtitle visibility is driven by audio DSP sample frames. `Time.deltaTime` may smooth visual alpha only; it is not subtitle truth.
- `LocalizationManager` PDA corrosion, madness override, and corruption seed buckets now derive from DSP/audio-frame counters instead of `Time.unscaledTime`.
- Cue state is presentation-only and excluded from rollback/Merkle truth by `FlagVisualOnlyNoRollback`.
- Detailed boundary and verification gates live in `Docs/ARCHITECTURE/LOCALIZATION_SUBTITLE_SYNC_ENGINE.md`.

## May 7 Non-Negotiable Checklist

- `.ToString()` is forbidden in every HUD, visor, PDA, subtitle, warning, diagnostics, and diegetic UI update path.
- `.ToString()` remains forbidden even when hidden behind a helper overload, a temporary variable, or an immediate `SetText(...)` call. Cold editor/report writers are outside this hot-path rule; runtime UI is not.
- `string.Format`, string interpolation, string concatenation, and `TMP_Text.text = ...` are forbidden in hot UI paths.
- Numeric values must use `TryFormat` into `Span<char>` backed by a preallocated buffer.
- The final text commit path is `TMP_Text.SetCharArray(buffer, 0, length)`. Any claimed equivalent zero-string API remains `PENDING VERIFICATION` until source and profiler evidence prove it does not allocate.
- Static or localized fragments must be cached outside the hot path and appended as `ReadOnlySpan<char>` or copied from prevalidated static buffers.
- A UI change that cannot prove this path remains `PENDING VERIFICATION`, regardless of visual correctness.
- All HUD Canvas components must be split into Static and Dynamic to prevent full-screen vertex rebuilds on text changes.

## Hard Canvas Split Mandate

All HUD Canvas components must be split into Static and Dynamic to prevent full-screen vertex rebuilds on text changes.

Minimum partition:

- Static Canvas: frames, icons, nonchanging visor art, borders, and decorative overlays.
- Dynamic Low-Cadence Canvas: oxygen, depth, power, inventory counts, and other throttled text.
- Dynamic High-Cadence Canvas: reticles, active warnings, hit markers, and any 60 Hz text or pulse.

Any HUD prefab or runtime-generated HUD root that mixes static art with changing TMP/Text components in one full-screen Canvas is a `CRITICAL UI REBUILD VIOLATION` until split or removed.

## May 7 Source Enforcement Scan

Command:

```powershell
rg -n "\.text\s*=" Assets/_Project/Scripts/UI -g '*.cs'
```

Result:

- matches: `0`
- interpretation: no direct `.text =` assignment currently exists under `Assets/_Project/Scripts/UI`.
- boundary: this does not prove `0 B/frame`; it only proves the direct TMP/Text assignment pattern is absent in the scanned UI folder at source-text time.

Critical rule:

- If this command returns any row in runtime UI code, the row is a `CRITICAL VIOLATION` until the write is replaced with `Span<char>` / `CharBufferPool` / `TMP_Text.SetCharArray(...)` or proven cold/editor-only by surrounding source.

Relevant owners already in source:

- `SuitHUDV4CanvasOverlay`
- `PDALoadoutTab`
- `PauseMenuController`
- `CharBufferPool`

## Pipeline

1. Acquire a transient fixed buffer from `CharBufferPool`.
2. Wrap the leased `char[]` in `Span<char>`.
3. Write literals and numeric values directly into the span with `TryFormat(...)`.
4. Push the final character count into `TMP_Text.SetCharArray(buffer, 0, length)`.
5. Release the `CharBufferPool` lease immediately after the write.

There is no intermediate `string`, no `StringBuilder`, and no `TMP_Text.text = ...` in the hot path.

## Cadence Rules

Do not dirty every HUD label every frame.

- `Time.frameCount % 30 == 0`: very low cadence telemetry such as FPS or memory
- `Time.frameCount % 6 == 0`: medium cadence telemetry such as oxygen and depth
- every frame only for genuinely high-frequency visuals such as reticles or critical warning blink state

Even when a cadence gate opens, skip the `SetCharArray(...)` write if the new value is inside the hysteresis threshold:

- oxygen delta `< 0.5%`
- depth delta `< 0.5 m`
- heading delta `< 1 degree`

The goal is not only zero allocations. The goal is avoiding needless TMP dirties and full-canvas rebuild propagation.

## Nested High-Frequency Canvases

Any HUD element that legitimately changes at 60 Hz must live on its own nested canvas.

- `overrideSorting = true`
- static canvas sorting order = `10`
- low-cadence canvas sorting order = `20`
- high-cadence canvas sorting order = `30`
- every HUD root must separate static art, low-cadence telemetry, and high-cadence dynamic text into separate Canvas components

This isolates reticle and warning dirty flags from the rest of the visor hierarchy so the full 1080p canvas does not rebuild when one tiny quad changes.

Rules:

- keep crosshair and critical warning labels on `HighCadence`
- keep depth and supplemental telemetry on `LowCadence`
- disable `GraphicRaycaster` on noninteractive nested canvases
- do not toggle HUD sections with `SetActive(...)`
- do not call `Canvas.ForceUpdateCanvases()`

## 3D Holographic UI Projection

Do not instantiate world-space canvases or per-item GameObjects for diegetic PDA projections.

Rules:

- inventory, tooltips, and diagnostic overlays must project from panel-space pixels into world space through `DiegeticPanelController`
- use `TryProjectCanvasPointToWorld(...)` for anchor positions
- use the panel pixel basis vectors to convert UI pixel width and height into meter scale
- holographic meshes come from `ItemTemplateRegistry.ProxyMeshIndex` or another authored proxy-bank seam
- if multiple entries share one mesh, batch them in one `Graphics.DrawMeshInstanced(...)` call
- if mesh indices differ, group by mesh index and issue one instanced call per mesh group
- never spawn one `Canvas`, `MeshRenderer`, or `GameObject` per tooltip, slot, or inventory item
- keep drag previews and other pointer affordances in the existing UI owner; the 3D projection layer is presentation only
- default inventory lift is `0.05 m` above the panel surface plus any procedural bob offset
- per-frame hologram animation must be matrix-only via `Matrix4x4.TRS(...)`; do not mutate slot `Transform` components
- runtime hologram materials and staging arrays must be created once by the owner and released in `OnDestroy()`
- if the panel owner, proxy mesh bank, or authored `ProxyMeshIndex` is unavailable, fall back to the existing 2D icon path without exceptions

Reference grid math:

```csharp
float pixelWidth = (anchorWidth * CellStep) - CellGap;
float pixelHeight = (anchorHeight * CellStep) - CellGap;
float centerFromTop = topOffset + (anchorY * CellStep) + (pixelHeight * 0.5f);

float canvasX = gridOriginX + (anchorX * CellStep) + (pixelWidth * 0.5f);
float canvasY = panelReferenceHeight - centerFromTop;
```

That canvas-space center is then projected into the physical PDA surface before building the world-space `Matrix4x4.TRS(...)`.

## Pool Contract

`CharBufferPool` is a fixed slot allocator:

- `500` legacy HUD slots at `256` chars
- `500` Babel subtitle slots at `512` chars, with Vault arena `(BufferID)70540` when available and prewarmed TMP bridge fallback when it is not
- `4` encyclopedia/page slots at `32768` chars
- slot allocation is tracked by fixed bitmasks, not a per-frame collection

Acquisition pattern:

```csharp
if (!CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
    return;

try
{
    Span<char> buffer = lease.Buffer;
    // write into buffer
}
finally
{
    CharBufferPool.Release(lease);
}
```

## Numeric Formatting

Use `TryFormat(...)` on the numeric value itself. Do not call `.ToString()`.

```csharp
private static bool WriteBatteryPercent(float normalized, Span<char> buffer, out int length)
{
    int cursor = 0;
    int batteryPercent = Mathf.Clamp(Mathf.RoundToInt(normalized * 100f), 0, 100);

    if (!"BAT ".AsSpan().TryCopyTo(buffer))
    {
        length = 0;
        return false;
    }

    cursor += 4;
    if (!batteryPercent.TryFormat(buffer.Slice(cursor), out int written))
    {
        length = 0;
        return false;
    }

    cursor += written;
    if (cursor >= buffer.Length)
    {
        length = 0;
        return false;
    }

    buffer[cursor++] = '%';
    length = cursor;
    return true;
}
```

## TMP Commit

Once the span is populated, write directly into TMP:

```csharp
private static void ApplyBufferedText(TMP_Text label, char[] buffer, int length)
{
    if (label == null || buffer == null)
        return;

    int safeLength = Mathf.Clamp(length, 0, buffer.Length);
    label.SetCharArray(buffer, 0, safeLength);
}
```

## Localized Labels

Localized static fragments should arrive as `ReadOnlySpan<char>` or cached strings resolved outside the per-frame loop. Numeric suffixes still append through the hot buffer.

```csharp
private static bool WriteDepthLabel(ReadOnlySpan<char> prefix, float depthMeters, Span<char> buffer, out int length)
{
    length = 0;
    if (!prefix.TryCopyTo(buffer))
        return false;

    int cursor = prefix.Length;
    if (cursor >= buffer.Length)
        return false;

    buffer[cursor++] = ' ';
    int roundedDepth = Mathf.Max(0, Mathf.RoundToInt(depthMeters));
    if (!roundedDepth.TryFormat(buffer.Slice(cursor), out int written))
        return false;

    cursor += written;
    if (cursor + 1 > buffer.Length)
        return false;

    buffer[cursor++] = 'm';
    length = cursor;
    return true;
}
```

## Failure Policy

If `CharBufferPool.TryAcquire(...)` fails:

- skip the noncritical visual update for that frame
- do not allocate a fallback `string`
- do not allocate a fallback `char[]`

The pool stall is preferable to heap churn in the HUD path.

## Bar Hysteresis

Fill bars should not snap directly to the target every frame.

```csharp
displayValue = Mathf.Lerp(displayValue, targetValue, dampFactor * dt);
if (Mathf.Abs(displayValue - targetValue) <= 0.01f)
    displayValue = targetValue;
```

Current HUD damping contract:

- health = `8.0`
- battery = `6.0`
- oxygen = higher responsiveness but still smoothed by the same no-snap rule

Do not write the fill image if the visible fill delta is effectively noise.

## Forbidden Patterns

Do not use these in hot UI paths:

- `TMP_Text.text = someString`
- `value.ToString()`
- `$"..."` interpolation
- `string.Concat(...)`
- `string.Format(...)`
- `new StringBuilder(...)`

## Static Search Targets

When auditing a UI file, search for:

```text
\.text\s*=
\.ToString\(
string\.Concat
string\.Format
\$"
```

If any of those remain in `Tick`, `Update`, `LateUpdate`, or other per-frame UI paths, the owner is not zero-GC.

## SHINOBU_137 Terminal Projection

Diegetic submarine terminals use the `TerminalOS` projection path, not World Space Canvas:

- terminal screen truth lives in vault buffers: `TerminalPlaneDTO`, `GazeRayDTO`, `TerminalInteractionDTO`, and `ButtonAABBDTO`
- gaze interaction is Burst AUP ray-plane math, not `Physics.Raycast`, `GraphicRaycaster`, or `PhysicsRaycaster`
- dynamic terminal visuals upload dirty DTO ranges through mapped `GraphicsBuffer` writes into a texture array
- `Hecton_DiegeticTerminal.shader` sells low-res terminal slices with scanlines, vignette, and glow; visual cadence scales with `HomeostasisBrain.GlobalQualityWeight`
- interaction jobs stay frame-responsive; visual formatting/texture refresh uses `framesBetweenUpdates = round(lerp(1, 15, 1 - GlobalQualityWeight))`
- `GlobalQualityWeight` scalar/cadence reads happen every frame; only heavy texture-resolution/resource refresh is cadence-gated
- attention-culling a dirty terminal defers its texture upload without clearing `IsDirty`, so offscreen terminal changes are uploaded when the terminal becomes visible again
- Intended terminal layout source `Assets/StreamingAssets/terminal_layouts.csv` is absent in the current checkout; when present, `terminal_layouts.csv` button rows are parsed from `ReadOnlySpan<byte>` into unmanaged AABB DTOs without managed string splitting. Layout CSV wiring remains pending artifact proof.

Known boundary: legacy `DiegeticPanelController`/fabricator/PDA canvases remain separate owner debt. `TerminalOS` must not reintroduce World Space Canvas dependencies.

Status: PENDING VERIFICATION
