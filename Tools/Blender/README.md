# HECTON-8 Blender asset forge — operations

Offline procedural asset generation outside Unity. Canonical law is `AGENTS.md`; the asset
specification is `3dmodel.md`, `PROCEDURAL_ASSET_PIPELINE.md`, and the family bible
(`3DMODEL_FLORA_CORAL.md`, `3DMODEL_GEOLOGY_ROCKS.md`, `3DMODEL_HARD_SURFACE_MODULES.md`). This file is
operational only — it does not restate a threshold or a rule. Every number lives once in
`h8forge/law.py` with its bible citation.

API traps that cost real debugging time: `BLENDER_API_TRAPS.md`. Read it before your first change here.

## Why this lane exists

`3dmodel.md` section 0 permits authoring in "Unity Editor tooling **or external offline DCC/bake
tools**". Two reasons this is the external half:

1. **Baked ambient occlusion is mandatory.** `3dmodel.md` and `3DMODEL_FLORA_CORAL.md` both require
   ray-traced occlusion in vertex-colour channel B. A C# generator can only approximate occlusion from
   local curvature; Cycles ray-traces it. "The generator computed something AO-shaped" is a different
   artefact.
2. **Unity holds one project lock and is usually occupied.** A pipeline whose only proof route is an
   in-engine screenshot cannot iterate. Blender renders the geometry it just built and the lead judges
   the PNG directly.

This does **not** replace the in-Unity C# generators — it covers the capability gap. Nothing in this
lane writes `.prefab` or `.unity`; Unity remains their sole writer per `AGENTS.md` `Evidence Law`.
Output is one FBX per package plus a JSON manifest, and the manifest carries the importer contract
Unity must apply.

## Environment

| | |
|---|---|
| Interpreter | `C:\hades\_tools\blender\blender-4.5.9-windows-x64\blender.exe` (4.5.9 LTS) |
| Python | 3.11.11 |
| Available | `bpy`, `bmesh`, `mathutils`, `numpy` 1.26.4 |
| Not available | **PIL/Pillow** — composite and measure with numpy |

The binary is outside the git tree on purpose: a 400 MB dependency must not enter the repo. Do not
vendor it, do not add it to `Assets/`, do not commit a `.blend` of it.

Invocation, always:

```bat
blender.exe -b --factory-startup -P <script> -- <args>
```

`--factory-startup` keeps a developer's user preferences and add-ons out of a deterministic run. It also
means the bundled essentials asset library is NOT loaded — see trap 1 in `BLENDER_API_TRAPS.md`, which
is why one shading operator silently does nothing in this exact invocation.

## Module map

`h8forge/` — the core. `law` is dependency-free and imports outside Blender; everything else needs `bpy`.

| Module | Owns |
|---|---|
| `law.py` | every bible threshold, with citations; families, budgets, bevel ranges, vcol contracts, naming, sway formula. No `bpy` import. |
| `blackbox.py` | preallocated 300-step bake ring + failure dump (`3dmodel.md` section 11); `GenerationAborted` |
| `mesh_ops.py` | weld/clean, bevel policy, shading basis, LOD chain, convex collider proxy, bounds |
| `vertexcolor.py` | channel semantics, Cycles AO bake into a scratch attribute, organic + hard-surface compose, curvature edge wear |
| `validate.py` | 39 pre-save gates over a flat `MeshData` snapshot; `assert_or_abort` aborts the save. No Blender symbols imported. |
| `preview.py` | headless contact sheets, per-channel diagnostic tiles, `measure_channel_png` |
| `export_unity.py` | FBX export with proven Blender→Unity axis/scale conversion, self-verifying round trip, manifest writer |

`generators/` — one asset family per module, no reimplemented thresholds:
`coral_branching.py`, `kelp.py`, `rock.py`.

Two `mesh_ops` helpers exist because a stage that can no-op must report a count, not a status:

- `apply_shading_basis(obj)` shades at DATA level (no operator) and returns
  `ShadingResult(smooth_polygons, sharp_edges, weighted_applied)`. Assert on `smooth_polygons`; the
  operator route it replaced returned `{'CANCELLED'}` headless and shaded nothing. Trap 1 in
  `BLENDER_API_TRAPS.md`.
- `topology_report(obj)` returns `TopologyReport` with component count, boundary edges, non-manifold
  edges and an estimated `irreducible_floor`. Call it when a budget is missed: Decimate collapses edges
  but cannot delete a whole shell, so an object made of many small disconnected pieces has a floor no
  number of passes will beat. "584 tris vs a 300 budget" teaches an author nothing; "76 disconnected
  components" tells them to weld the tip clusters or use an impostor at that level. Currently
  unreferenced — wire it into the budget-miss path rather than re-deriving the census by hand.

Import style inside Blender, where the package is not on `sys.path`:

```python
import sys, os
sys.path.insert(0, os.path.join(<repo>, "Tools", "Blender"))
from h8forge import law, mesh_ops, vertexcolor, preview, validate
```

## Commands

Run from the repo root. `--out` defaults to `Docs/AgentLogs/ForgePreviews`.

```bat
:: branching coral
blender.exe -b --factory-startup -P Tools/Blender/generators/coral_branching.py -- ^
    --seed 1712 --quality 1.0 --variants 1 --height 0.85
:: add --blocking for a path-blocking colony (emits a convex collider); --no-preview to skip renders

:: kelp / seaweed
blender.exe -b --factory-startup -P Tools/Blender/generators/kelp.py -- ^
    --seed 4021 --quality 1.0 --ao-samples 64 --preview-resolution 640

:: geology
blender.exe -b --factory-startup -P Tools/Blender/generators/rock.py -- ^
    --seed 1713 --quality 1.0 --size-class outcrop --process sedimentary
```

Self-tests, both headless, both exit non-zero on failure:

```bat
blender.exe -b --factory-startup -P Tools/Blender/h8forge/_test_validate.py
blender.exe -b --factory-startup -P Tools/Blender/h8forge/_test_export.py
```

`_test_validate.py` asserts every gate fires on data built to violate it AND stays silent on clean
geometry — a gate that never fires is the same defect as one that over-triggers. Last run on 4.5.9:
198 checks, 0 failures, 39 gates declared. It writes a black-box dump into `Docs/AgentLogs/` during the
abort case and deletes it again; a leftover `Dump_h8forge_validate_selftest_*.json` means the test died
mid-case.

`--quality` is `GlobalQualityWeight`, continuous 0..1. It may scale density, samples, and surface
detail. It must never change the silhouette, the family, or gameplay identity — that is why branch
depth and size class are separate arguments rather than quality-derived.

## Stage order is mandatory

From `PROCEDURAL_ASSET_PIPELINE.md` "Generation Order". Skipping or reordering a stage is a rejection,
not a shortcut, and "small asset" is explicitly not an exemption from UVs, normals, tangents, material
IDs, LOD policy, or validation:

1. shape grammar
2. high-detail geometry
3. family topology rules
4. UVs and material IDs
5. bakes and vertex colours
6. shared materials
7. LOD chain
8. collision proxies
9. prefab/package assembly
10. **validation**
11. save

Two orderings inside that sequence are load-bearing and each was learned by breaking it:

- **AO bake before the vertex-colour compose.** The bake overwrites all four channels of its target, so
  baking last erases the analytically authored R/G/A. A destroyed sway gradient is invisible in any lit
  render.
- **Reduce to the LOD0 budget before unwrapping and baking.** Decimating afterwards throws away the UV
  layout and vertex colours; never decimating left coral LOD0 at 206880 triangles against a 6500
  ceiling.

## Known gaps

Recorded rather than implied away. Full evidence in `BLENDER_API_TRAPS.md`.

All four entries below were CLOSED on 2026-07-29 and are kept as a record of what the wrong diagnosis
cost, because three of the four were misattributions rather than gaps.

- ~~**Coral LOD2 lands at 584 triangles against a 300 budget.**~~ CLOSED: it measures **287/300**.
  The cause was never the seam-drop retry and never "roughly 76 disconnected tip-cluster shells" —
  the component count was **4**. It was **144 non-manifold edges**, identical at every LOD, because
  Quadric Edge Collapse will not collapse across one. `weld_and_clean` now keeps the two largest
  faces at any edge shared by three or more and deletes the rest; 584 → 287 with no other change.
  The "76 shells" figure was reasoning from a plausible mechanism instead of reading the number, and
  it survived here and in two commit messages because nobody called the tool.
- ~~**`topology_report` has no callers.**~~ CLOSED: **five call sites**, on every LOD and bracketing
  the decimation. That bracketing immediately paid for itself — see the next entry.
- ~~**The AO bake does not restore `world.light_settings.distance`.**~~ CLOSED: it restores it, proven
  leak-free by a 0.04 / 9.0 / 0.04 sequence returning 1.00000 / 0.50000 / 1.00000. The replacement
  trap is a *hardcoded* ray distance: 0.22 m on a 0.55 m colony is 40% of the asset and collapses
  the bake to a global sky term (measured 0.792 sparse → 0.023 dense). Derive it from feature size.
- **NEW, and it is the one still open: `reduce_to_budget` introduces boundary and non-manifold
  edges.** Measured across the decimation on a clean input — in 34096 tris / boundary 0 /
  non-manifold 0, out 5865 / boundary 2 / non-manifold 1. Blender's Decimate/COLLAPSE makes them.
  A small count after a LOD build is a decimator artefact; do not hunt it in the growth grammar.
  Judged not worth chasing at 1 edge, but it must stop being attributed to generators.

## Previews and the visual verification loop

Sheets land in `Docs/AgentLogs/ForgePreviews/`. Naming is
`<Asset>_SHEET_<mode>.png` for the composite and `<Asset>_<mode>_<view>.png` / `<Asset>_chan<i>_<label>.png`
for tiles. Modes: `studio` (mid-roughness dielectric, reveals bevel response), `flat` (neutral matte,
silhouette only — `3DMODEL_FLORA_CORAL.md` section 10 requires this shot), `normals`, `material`, and
the four `vcol_*` channel modes. The channel sheet carries **no burnt-in labels**; tile order left to
right is R, G, B, A, and `PreviewResult.notes` plus the tile filenames carry the semantic mapping from
the family's contract in `law.py`.

The loop, in order:

1. **Generate.** Stale `.png`/`.log` in the output directory are physically deleted first
   (`AGENTS.md` `Atomic File Delete Rule`) — otherwise you audit the previous run.
2. **Render** the contact sheet and the channel sheet.
3. **Measure** with `preview.measure_channel_png`. It masks by ALPHA and linearises out the display
   encoding, so the numbers are comparable to the bible's 0..1 linear thresholds. `has_gradient` and
   `subject_visible` are the two guards that separate a real reading from an empty frame or a
   background measurement.
4. **Open the PNG with your own visual modality.** `AGENTS.md` `[REQ] Direct Media Reading` makes a
   visual verdict without inspecting the image a compliance failure, and `[RULE] Never Trust Automated
   Assertions Alone` makes the existence of a PNG and a zero exit code worth nothing. The numbers and
   the image answer different questions: measurement catches a collapsed channel, the image catches a
   silhouette that reads as programmer output.

A channel that went flat is the single most likely silent failure in an organic generator — the mesh is
correct, the attribute exists, the validator sees data, and the shader animates the whole organism as a
rigid body. It is invisible in a lit render and only the raw channel tile shows it. That is why the
channel sheet is not optional.

Step 1 is narrower than it looks, and the reason is worth keeping: `clear_render_dir` decides staleness
by MTIME against process start, not by filename prefix. Prefix matching deleted the studio sheet when the
flat pass ran under the same asset name — the file was created, then removed by the very stale-artifact
rule meant to protect it, with no error anywhere. `ForgePreviews` still shows the evidence: a coral flat
sheet and channel sheet with no studio sheet. Do not reintroduce prefix-only deletion.

## What this lane does not do

- Write `.prefab`, `.unity`, `.mat`, or `.asset` — Unity only.
- Replace Unity/profiler/device proof. A green self-test and a rendered sheet are offline evidence;
  status stays `PENDING VERIFICATION` until in-engine proof exists (`Docs/QUALITY_GATES.md`).
- Hold a threshold. If a number is not in `law.py` with a bible citation, it is drift.
