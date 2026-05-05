# ZERO_GC_UI_PIPELINE
Date: `2026-05-04`

Status: REFERENCE
Verification: PENDING VERIFICATION

2026-05-04 current-state boundary:

- This is the UI zero-GC contract and source-oriented pattern reference, not profiler proof.
- Current project-state orientation starts at `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md` and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`.
- Any claim of `0 B/frame` for HUD/PDA/menu paths still requires fresh GCMonitor or profiler capture.
- Presentation/UI must not own gameplay state transitions without a logic-owned fallback.

## Scope

This project does not push runtime HUD numbers into `TMP_Text.text`. Hot-path UI text is staged through fixed buffers, formatted with `Span<char>`, and committed through `TMP_Text.SetCharArray(...)`.

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

- `16` slots
- `128` chars per slot
- allocation tracked with `ushort _slotMask`

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

## Verified Search Targets

When auditing a UI file, search for:

```text
\.text\s*=
\.ToString\(
string\.Concat
string\.Format
\$"
```

If any of those remain in `Tick`, `Update`, `LateUpdate`, or other per-frame UI paths, the owner is not zero-GC.
