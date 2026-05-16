# ARCHITECT_EYE_VISUALIZER Rationale

## Decision 0 - Domain and Prompt Source
Problem: The active `Docs/Tasks/CURRENT_BATCH.md` lacks the `ARCHITECT_EYE_VISUALIZER` XML block, while the user supplied the full XML inline.
Solution: Treat inline XML as the assignment source and record the missing batch extraction as evidence. Keep implementation inside `Assets/_Project/Scripts/Core/Diagnostics/Visuals/` except editor/build glue required by CSV bake, watchers, and blackbox playback.
Rejected Alternatives: Reading archived batch prompts as authority. That violates fresh-batch hygiene and risks neighboring-task contamination.
Scalability potential: Low tier keeps diagnostics disabled or 5Hz-bucketed; Middle uses bounded overlays; High adds richer graph samples; Ultra uses overkill heat overlays without touching gameplay truth.
Hardware Impact: Expected hot-path impact is near-zero when disabled; active debug target is bounded under 0.1 ms CPU on i3/MX350 by dirty pages and indirect quads.

## Decision 1 - Rendering Primitive
Problem: Debug HUD must not use UGUI Canvas, and text/heatmaps must avoid per-frame managed strings.
Solution: Build one indirect-quad renderer with fixed CPU-side instance buffers, `GraphicsBuffer`, and `Graphics.DrawMeshInstancedIndirect`. Labels use a fixed bitmap atlas with preformatted glyph indices.
Rejected Alternatives: TMP/UGUI overlays, GameObject labels, `Debug.DrawLine` loops, and `Handles` runtime drawing. These allocate, rebuild canvases, or do not survive player builds.
Scalability potential: Low draws coarse cells and fewer labels; Middle draws sector grids; High draws per-system strips; Ultra increases label density and heatmap detail.
Hardware Impact: One draw per layer and sparse buffer uploads replace hundreds of debug GameObjects; estimated savings versus GameObject/TMP debug is 200-900 microseconds on i3/MX350 in active debug views.

## Decision 2 - CSV Authority Integration
Problem: The prompt requires `Data/Balance/*.csv`, but the existing Data Monolith compiler only read `Assets/_SourceData`.
Solution: Extend the existing compiler to enumerate both roots and extend the existing editor watcher to `Data/Balance`. Hash validation occurs before row conversion so bad authoring data cannot reach `.h8bin`.
Rejected Alternatives: A second baker, runtime CSV parsing, or copying Balance CSV into Assets. Those paths create duplicate truth or player-runtime file I/O.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime because CSV ingest is editor/cold build-time only.
Hardware Impact: 0 us player hot path. Editor bake cost increases only by the number of authored Balance files.

## Decision 3 - Vault Probe Shape
Problem: C# cannot infer the element type of "any NativeArray by ID" from `BufferID` alone without exposing GlobalDataVault internals or reflection.
Solution: Provide a generic `TryReadBufferBytes<T>` and typed handle probe. Callers supply the known buffer type; the utility returns a raw byte span over existing vault memory without allocation.
Rejected Alternatives: Reflection over private vault metadata, `object`-typed NativeArray boxing, or a giant switch for every BufferID. These either allocate or mutate public contracts.
Scalability potential: Low samples tiny slices; Middle samples selected lanes; High/Ultra can scan more entries because the API has no heap cost.
Hardware Impact: Probe cost is a linear native read over caller-selected samples; no persistent memory is allocated by the utility.

## Decision 4 - Compile Wall Classification
Problem: First compile verification fails in `GameBootstrapper` because `Hecton8.Core.Bucketing.ModuloSimulationBucketer` is missing from the codebase, outside the assigned diagnostics domain.
Solution: Record as an existing dependency wall and continue diagnostics implementation until final compile, where a minimal cross-domain bridge may be considered only if required to satisfy `PLATINUM_COMPILE`.
Rejected Alternatives: Editing `GameBootstrapper` immediately or inventing broad Bucketing behavior before diagnostics depends on it. That risks architectural drift in another domain.
Scalability potential: Diagnostics renderer will still self-bucket to 5 Hz using existing tick cadence and fixed counters, independent of the missing bucketer.
Hardware Impact: Avoids unnecessary domain churn now; estimated 0 us player impact from this recording decision.
